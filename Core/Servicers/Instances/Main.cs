using Core.Enums;
using Core.Event;
using Core.Librarys;
using Core.Librarys.Browser;
using Core.Librarys.Browser.Favicon;
using Core.Librarys.SQLite;
using Core.Models;
using Core.Models.Config;
using Core.Models.Config.Link;
using Core.Servicers.Interfaces;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Threading;

namespace Core.Servicers.Instances
{
    public class Main : IMain
    {
        private readonly IAppObserver appObserver;
        private readonly IData data;
        private readonly ISleepdiscover sleepdiscover;
        private readonly IAppConfig appConfig;
        private readonly IAppData appData;
        private readonly ICategorys categories;
        private readonly IWebFilter _webFilter;
        private readonly IAppTimerServicer _appTimer;
        private readonly IWebServer _webServer;
        private readonly IWebData _webData;
        /// <summary>应用过滤服务（从 Main 中提取，单一职责）</summary>
        private readonly AppFilterService _appFilter;

        /// <summary>
        /// 睡眠状态
        /// </summary>
        private SleepStatus sleepStatus;

        /// <summary>
        /// app config
        /// </summary>
        private ConfigModel config;

        public event EventHandler OnUpdateTime;
        public event EventHandler OnStarted;

        //  更新应用日期
        private DateTime updadteAppDateTime_ = DateTime.Now.Date;
        //  已经更新过的应用列表
        private List<string> updatedAppList = new List<string>();
        public Main(
            IAppObserver appObserver,
            IData data,
            ISleepdiscover sleepdiscover,
            IAppConfig appConfig,
            IDateTimeObserver dateTimeObserver,
            IAppData appData, ICategorys categories,
            IWebFilter webFilter_,
            IAppTimerServicer appTimer_,
            IWebServer webServer_,
            IWebData webData_)
        {
            this.appObserver = appObserver;
            this.data = data;
            this.sleepdiscover = sleepdiscover;
            this.appConfig = appConfig;
            this.appData = appData;
            this.categories = categories;
            _webFilter = webFilter_;
            _appTimer = appTimer_;
            _webServer = webServer_;
            _webData = webData_;
            _appFilter = new AppFilterService(appData);

            sleepdiscover.SleepStatusChanged += Sleepdiscover_SleepStatusChanged;
            appConfig.ConfigChanged += AppConfig_ConfigChanged;
            _appTimer.OnAppDurationUpdated += _appTimer_OnAppDurationUpdated;
            WebSocketEvent.OnWebLog += WebSocketEvent_OnWebLog;
        }

        private void AppConfig_ConfigChanged(ConfigModel oldConfig, ConfigModel newConfig)
        {
            if (oldConfig != newConfig)
            {
                Logger.Tag("[Config]", "配置已变更");
                //  处理开机自启
                try
                {
                    SystemCommon.SetStartup(newConfig.General.IsStartatboot);
                }
                catch (Exception ex)
                {
                    Logger.Error("[Config] 设置开机自启失败: " + ex.Message);
                }

                //  更新忽略规则
                UpdateConfigIgnoreProcess();

                //  更新白名单
                UpdateConfigProcessWhiteList();

                //  处理web记录功能启停
                HandleWebServiceConfig();
            }
        }

        public async Task Run()
        {
            await Task.Run(() =>
             {
                 CreateDirectory();

                 //  数据库自检
                 using (var db = new TaiDbContext())
                 {
                     db.SelfCheck();
                 }

                 //  加载app信息
                 appData.Load();

                 // 加载分类信息
                 categories.Load();

                 AppState.IsLoading = false;
             });
            Logger.Tag("[Startup]", "Tai 初始化完成");



            //  加载应用配置（确保配置文件最先加载
            appConfig.Load();
            config = appConfig.GetConfig();
            UpdateConfigIgnoreProcess();
            UpdateConfigProcessWhiteList();

            //  初始化过滤器
            _webFilter.Init();

            //  启动主服务
            Start();

            OnStarted?.Invoke(this, EventArgs.Empty);
            Logger.Tag("[Startup]", "所有服务已启动");
        }
        public void Start()
        {
            //  appTimer必须比Observer先启动*
            _appTimer.Start();
            appObserver.Start();
            if (config.General.IsWebEnabled)
            {
                _webServer.Start();
            }
            if (config.Behavior.IsSleepWatch)
            {
                //  启动睡眠监测
                sleepdiscover.Start();
            }
        }
        public void Stop()
        {
            appObserver.Stop();
            _appTimer.Stop();
            _webServer.Stop();
        }
        public void Exit()
        {
            appObserver?.Stop();
        }

        /// <summary>
        /// 创建程序目录
        /// </summary>
        private void CreateDirectory()
        {
            string dir = Path.Combine(FileHelper.GetRootDirectory(), "Data");
            Directory.CreateDirectory(dir);
        }

        private void UpdateConfigIgnoreProcess()
        {
            _appFilter.UpdateFromConfig(config);
        }

        private void UpdateConfigProcessWhiteList()
        {
            _appFilter.UpdateFromConfig(config);
        }

        private void Sleepdiscover_SleepStatusChanged(Enums.SleepStatus sleepStatus)
        {
            this.sleepStatus = sleepStatus;

            Logger.Tag("[Sleep]", $"睡眠状态变更 → {sleepStatus}");
            if (sleepStatus == SleepStatus.Sleep)
            {
                //  进入睡眠状态
                Debug.WriteLine("进入睡眠状态");

                //  通知sokcet客户端
                _webServer?.SendMsg("sleep");
                //  停止服务
                Stop();

                //  更新时间
                UpdateAppDuration();
            }
            else
            {
                //  从睡眠状态唤醒
                Debug.WriteLine("从睡眠状态唤醒");

                _webServer?.SendMsg("wake");

                Start();
            }
        }


        /// <summary>
        /// 检查应用是否需要记录数据，并在需要时创建/更新应用信息。
        /// 过滤逻辑已委托给 AppFilterService。
        /// </summary>
        private bool IsCheckApp(string processName, string description, string file)
        {
            var filterResult = _appFilter.CheckApp(processName, description, file, config);
            if (filterResult == AppFilterResult.Ignored)
            {
                return false;
            }

            //  过滤通过后：创建或更新应用信息
            AppModel app = appData.GetApp(processName);
            if (app == null)
            {
                string iconFile = Iconer.ExtractFromFile(file, processName, description);
                appData.AddApp(new AppModel()
                {
                    Name = processName,
                    Description = description,
                    File = file,
                    CategoryID = 0,
                    IconFile = iconFile,
                });
            }
            else
            {
                if (updadteAppDateTime_ != DateTime.Now.Date)
                {
                    updadteAppDateTime_ = DateTime.Now.Date;
                    updatedAppList.Clear();
                }

                if (!updatedAppList.Contains(processName))
                {
                    app.IconFile = Iconer.ExtractFromFile(file, processName, description);
                    if (app.Description != description) app.Description = description;
                    if (app.File != file) app.File = file;
                    appData.UpdateApp(app);
                    updatedAppList.Add(processName);
                }
            }

            return true;
        }

        private void HandleLinks(string processName, int seconds, DateTime time)
        {
            Task.Run(() =>
            {
                try
                {
                    List<LinkModel> links = config.Links != null ? config.Links : new List<LinkModel>();
                    foreach (LinkModel link in links)
                    {
                        if (link.ProcessList != null
                            && link.ProcessList.Count >= 2
                            && link.ProcessList.Contains(processName))
                        {
                            //  属于关联进程
                            foreach (string linkProcess in link.ProcessList)
                            {
                                if (linkProcess != processName)
                                {
                                    if (IsProcessRuning(linkProcess))
                                    {
                                        //  同步更新
                                        data.UpdateAppDuration(linkProcess, seconds, time);
                                    }

                                }
                            }
                            break;
                        }
                    }

                }
                catch (Exception ex)
                {
                    Logger.Error(ex.Message + "，关联进程更新错误，Process Name: " + processName + "，Time: " + seconds);
                }
            });
        }

        #region 判断进程是否在运行中
        /// <summary>
        /// 判断进程是否在运行中
        /// </summary>
        /// <param name="processName"></param>
        /// <returns></returns>
        private bool IsProcessRuning(string processName)
        {
            Process[] process = Process.GetProcessesByName(processName);
            return process != null && process.Length > 0;
        }
        #endregion

        #region 处理网站数据记录配置项开关
        /// <summary>
        /// 处理网站数据记录配置项开关
        /// </summary>
        private void HandleWebServiceConfig()
        {
            if (config == null)
            {
                return;
            }

            if (config.General.IsWebEnabled)
            {
                _webServer.Start();
            }
            else
            {
                _webServer.Stop();
            }
        }
        #endregion

        private void _appTimer_OnAppDurationUpdated(object sender, Event.AppDurationUpdatedEventArgs e)
        {
            UpdateAppDuration(e);
        }

        private void UpdateAppDuration()
        {
            UpdateAppDuration(_appTimer.GetAppDuration());
        }
        private void UpdateAppDuration(AppDurationUpdatedEventArgs e)
        {
            if (e == null) return;

            try
            {
                var app = e.App;
                int duration = e.Duration;
                DateTime startTime = e.ActiveTime;

                bool isCheck = IsCheckApp(app.Process, app.Description, app.ExecutablePath);
                if (isCheck)
                {
                    //  更新统计时长
                    data.UpdateAppDuration(app.Process, duration, startTime);
                    //  关联进程更新
                    HandleLinks(app.Process, duration, startTime);
                    OnUpdateTime?.Invoke(this, null);
                    //  自动分类
                    DispatchCateogry(app.Process, app.ExecutablePath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }

        #region 浏览器记录
        private void WebSocketEvent_OnWebLog(Models.WebPage.NotifyWeb args)
        {
            try
            {
                if (_webFilter.IsIgnore(args.Url))
                {
                    Debug.WriteLine($"URL已被过滤，{args.Url}");
                    return;
                }
                Logger.Tag("[Web]", $"收到网页统计: Url={UrlHelper.GetDomain(args.Url)}, Duration={args.Duration}s");

                //  记录数据
                var site = new Models.WebPage.Site()
                {
                    Url = args.Url,
                    Title = args.Title
                };

                _webData.AddUrlBrowseTime(site, args.Duration, args.ActiveDateTime);

                //  处理图标
                Task.Run(async () =>
                {
                    string saveName = UrlHelper.GetName(args.Url) + DateTime.Now.ToString("yyyyMM") + ".ico";
                    string path = await FaviconDownloader.DownloadAsync(args.Icon, saveName);
                    _webData.UpdateUrlFavicon(site, path);
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }
        #endregion

        #region 自动分类
        /// <summary>
        /// 自动分类
        /// </summary>
        /// <param name="processName_">进程名称</param>
        private void DispatchCateogry(string processName_, string executablePath_)
        {
            try
            {
                AppModel app = appData.GetApp(processName_);
                if (app != null)
                {
                    var categoryList = categories.GetCategories().Where(c => c.IsDirectoryMath && c.DirectoryList.Count > 0).ToList();
                    CategoryModel mathCategory = null;
                    foreach (var category in categoryList)
                    {
                        if (mathCategory != null)
                        {
                            break;
                        }
                        foreach (var item in category.DirectoryList)
                        {
                            string path = item.Replace("\\", "\\\\");
                            if (Regex.IsMatch(executablePath_, @"^" + path))
                            {
                                mathCategory = category;
                                Debug.WriteLine("匹配成功：" + category.Name);
                                break;
                            }
                        }

                    }
                    if (mathCategory != null)
                    {
                        //  匹配成功
                        app.Category = mathCategory;
                        app.CategoryID = mathCategory.ID;
                        appData.UpdateApp(app);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
            }
        }
        #endregion
    }
}
