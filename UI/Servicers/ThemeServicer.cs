using Core.Librarys;
using Core.Servicers.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using UI.ViewModels;

namespace UI.Servicers
{
    public class ThemeServicer : IThemeServicer
    {
        /// <summary>
        /// 当前主题名称
        /// </summary>
        private string themeName;
        private MainWindow mainWindow;
        private Collection<ResourceDictionary> MergedDictionaries;
        private readonly string[] themeOptions = { "Light", "Dark" };
        private readonly IAppConfig appConfig;
        /// <summary>
        /// 当前已加载的主题资源字典和控件字典的引用。
        /// 使用直接引用替换原先的 Source.OriginalString.Contains 字符串匹配，
        /// 避免因 URI 格式差异导致匹配失败或误删除。
        /// </summary>
        private ResourceDictionary _currentThemeDict;
        private ResourceDictionary _currentControlDict;
        /// <summary>
        /// 防抖保存定时器：窗口尺寸变化时延迟 500ms 再落盘。
        /// </summary>
        private System.Windows.Threading.DispatcherTimer _saveSizeTimer;

        public event EventHandler OnThemeChanged;

        public ThemeServicer(IAppConfig appConfig)
        {
            this.appConfig = appConfig;

            appConfig.ConfigChanged += AppConfig_ConfigChanged;

            MergedDictionaries = Application.Current.Resources.MergedDictionaries;

            //  初始化防抖保存定时器：窗口尺寸变化停止 500ms 后才落盘
            _saveSizeTimer = new System.Windows.Threading.DispatcherTimer();
            _saveSizeTimer.Interval = TimeSpan.FromMilliseconds(500);
            _saveSizeTimer.Tick += (s, e) =>
            {
                _saveSizeTimer.Stop();
                SaveWindowSize();
            };
        }

        private void AppConfig_ConfigChanged(Core.Models.Config.ConfigModel oldConfig, Core.Models.Config.ConfigModel newConfig)
        {
            if (oldConfig.General.Theme != newConfig.General.Theme)
            {
                LoadTheme(themeOptions[newConfig.General.Theme]);
                OnThemeChanged?.Invoke(this, EventArgs.Empty);
            }

            if (oldConfig.General.ThemeColor != newConfig.General.ThemeColor)
            {
                LoadTheme(themeOptions[newConfig.General.Theme], true);
                OnThemeChanged?.Invoke(this, EventArgs.Empty);
            }

            if (oldConfig.General.IsSaveWindowSize != newConfig.General.IsSaveWindowSize)
            {
                HandleWindowSizeChangedEvent();
            }

        }
        public void Init()
        {
            LoadTheme(themeOptions[appConfig.GetConfig().General.Theme]);
        }
        public void LoadTheme(string themeName, bool isRefresh = false)
        {
            if (string.IsNullOrEmpty(themeName))
            {
                //  设置默认主题
                themeName = themeOptions[0];
            }

            if (themeName == this.themeName && !isRefresh)
            {
                return;
            }

            var themeDict = GetResourceDictionary($"pack://application:,,,/Tai;component/Resources/Themes/{themeName}.xaml");
            var controlDict = GetResourceDictionary($"pack://application:,,,/Tai;component/Themes/Generic.xaml");
            if (themeDict == null || controlDict == null)
            {
                return;
            }

            //  通过直接引用移除旧字典（不再依赖 Source.OriginalString.Contains 字符串匹配）
            if (_currentThemeDict != null)
            {
                MergedDictionaries.Remove(_currentThemeDict);
            }
            if (_currentControlDict != null)
            {
                MergedDictionaries.Remove(_currentControlDict);
            }

            //  首次加载时，还需清理 App.xaml 中预加载的 Light.xaml 等旧主题字典。
            //  这些字典不在引用追踪范围内（_currentThemeDict 初始为 null），
            //  若不清理会排在 MergedDictionaries 最前面，导致新主题被覆盖失效。
            if (this.themeName == null)
            {
                var preloaded = MergedDictionaries
                    .Where(m => m.Source != null
                        && themeOptions.Any(t => m.Source.OriginalString.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
                    .ToList();
                foreach (var dict in preloaded)
                {
                    MergedDictionaries.Remove(dict);
                }
            }

            MergedDictionaries.Add(themeDict);
            MergedDictionaries.Add(controlDict);

            _currentThemeDict = themeDict;
            _currentControlDict = controlDict;
            this.themeName = themeName;

            UpdateWindowStyle();
            UpdateThemeColor();

            Debug.WriteLine("已加载主题：" + themeName);
        }

        /// <summary>
        /// 刷新主题颜色
        /// </summary>
        private void UpdateThemeColor()
        {

            var config = appConfig.GetConfig();
            if (string.IsNullOrEmpty(config.General.ThemeColor))
            {
                StateData.ThemeColor = ((System.Windows.Media.Color)Application.Current.Resources["ThemeColor"]).ToString();
                return;
            }

            StateData.ThemeColor = config.General.ThemeColor;
            Application.Current.Resources["ThemeColor"] = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(config.General.ThemeColor);
            Application.Current.Resources["ThemeBrush"] = UI.Base.Color.Colors.GetFromString(config.General.ThemeColor);
        }

        public void SetMainWindow(MainWindow mainWindow)
        {
            this.mainWindow = mainWindow;
            HandleWindowSizeChangedEvent();
        }

        private void HandleWindowSizeChangedEvent()
        {
            if (mainWindow == null || mainWindow.IsWindowClosed) return;

            mainWindow.SizeChanged -= MainWindow_SizeChanged;

            var config = appConfig.GetConfig();
            if (config.General.IsSaveWindowSize)
            {
                //  保存窗口大小信息
                mainWindow.SizeChanged += MainWindow_SizeChanged;
            }
        }

        /// <summary>
        /// 窗口尺寸变化事件。
        /// 原先每次 SizeChanged 都直接调用 appConfig.Save() 落盘，
        /// 拖拽窗口时可能触发数十次/秒的磁盘 IO。现改为防抖：缓存尺寸，
        /// 停止拖拽 500ms 后才执行一次保存。
        /// </summary>
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //  仅缓存最新尺寸，不立即落盘
            var config = appConfig.GetConfig();
            config.General.WindowWidth = mainWindow.ActualWidth;
            config.General.WindowHeight = mainWindow.ActualHeight;

            //  重启防抖定时器：每次 SizeChanged 都重置倒计时
            _saveSizeTimer.Stop();
            _saveSizeTimer.Start();
        }

        /// <summary>
        /// 实际执行窗口尺寸落盘。
        /// </summary>
        private void SaveWindowSize()
        {
            appConfig.Save();
        }

        public void UpdateWindowStyle()
        {
            if (mainWindow != null)
            {
                var themeStyle = Application.Current.Resources["WindowStyle"] as Style;
                if (themeStyle != null)
                {
                    mainWindow.Style = themeStyle;
                }
            }
        }

        private ResourceDictionary GetResourceDictionary(string uri)
        {
            try
            {
                return new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
            }
            catch (Exception e)
            {
                Logger.Error(e.ToString());
                return null;
            }
        }
    }
}
