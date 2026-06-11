using Core.Servicers.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;

namespace Core.Librarys
{
    /// <summary>
    /// 统一日志系统。支持带标签的结构化日志，便于搜索和过滤。
    ///
    /// 标准标签（Tag）：
    ///   [Startup]   — 启动流程
    ///   [Config]    — 配置变更
    ///   [Web]       — 浏览器扩展消息
    ///   [DB]        — 数据库操作
    ///   [Sleep]     — 睡眠检测
    ///   [AppTimer]  — 应用计时统计
    /// </summary>
    public static class Logger
    {
        private const int threshold = 50;

        private static readonly object writeLock = new object();

        private static List<string> loggers = new List<string>();


        static Logger()
        {
            //  创建计时器，定时保存log

            DispatcherTimer dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Interval = new TimeSpan(0, 5, 0);
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Start();
        }

        private static void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            Save(true);
        }

        private enum LogLevel
        {
            Info,
            Warn,
            Error,
        }

        /// <summary>
        /// 记录信息日志（可附带标签）。
        /// </summary>
        public static void Info(string message, [CallerLineNumber] int callerLineNumber = -1, [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
        {
            message = message + "\r\nLine:" + callerLineNumber + ",File:" + callerFilePath + ",name:" + callerMemberName;
            message = Fromat(LogLevel.Info, message);
            loggers.Add(message);
        }

        /// <summary>
        /// 记录警告日志（可附带标签）。
        /// </summary>
        public static void Warn(string message, [CallerLineNumber] int callerLineNumber = -1, [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
        {
            message = message + "\r\nLine:" + callerLineNumber + ",File:" + callerFilePath + ",name:" + callerMemberName;
            message = Fromat(LogLevel.Warn, message);
            loggers.Add(message);
        }

        /// <summary>
        /// 记录错误日志（可附带标签）。
        /// </summary>
        public static void Error(string message, [CallerLineNumber] int callerLineNumber = -1, [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
        {
            message = message + "\r\nLine:" + callerLineNumber + ",File:" + callerFilePath + ",name:" + callerMemberName;
            message = Fromat(LogLevel.Error, message);
            loggers.Add(message);
        }

        /// <summary>
        /// 带标签的结构化日志。标签格式： [TAG] message
        /// 示例：Logger.Tag("[Startup]", "应用初始化完成");
        /// </summary>
        public static void Tag(string tag, string message, [CallerLineNumber] int callerLineNumber = -1, [CallerFilePath] string callerFilePath = null, [CallerMemberName] string callerMemberName = null)
        {
            message = tag + " " + message + "\r\nLine:" + callerLineNumber + ",File:" + callerFilePath + ",name:" + callerMemberName;
            message = Fromat(LogLevel.Info, message);
            loggers.Add(message);
        }

        private static string Fromat(LogLevel logLevel, string message)
        {
            message = HandleMessage(message);
            string logText = $"[{logLevel}] {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}\r\n{message}\r\n------------------------\r\n\r\n";
            Debug.WriteLine(logText);
            return logText;
        }

        /// <summary>
        /// 将log写入文件
        /// </summary>
        /// <param name="isNow">是否强制立即写入</param>
        public static void Save(bool isNow = false)
        {
            if ((!isNow && loggers.Count < threshold) || loggers.Count == 0)
            {
                return;
            }

            try
            {
                Write(string.Join(string.Empty, loggers));
                loggers.Clear();
            }
            catch (Exception ec)
            {
                Error(ec.ToString());
            }
        }

        private static void Write(string log_)
        {
            try
            {
                lock (writeLock)
                {
                    string loggerName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                 "Log", DateTime.Now.ToString("yyyy-MM-dd") + ".log");
                    string dir = Path.GetDirectoryName(loggerName);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    if (!File.Exists(loggerName))
                    {
                        List<string> clientInfo = new List<string>(5);

                        //  记录客户端信息
                        clientInfo.Add(FromatItem("Core Version", Assembly.GetExecutingAssembly().GetName().Version.ToString()));
                        clientInfo.Add(FromatItem("OS Name", SystemCommon.GetWindowsVersionName()));
                        clientInfo.Add(FromatItem("Computer Type", GetComputerType()));
                        clientInfo.Add(FromatItem("Screen", GetScreenSize()));
                        clientInfo.Add("\r\n++++++++++++++++++++++++++++++++++++++++++++++++++\r\n\r\n");

                        File.WriteAllText(loggerName, string.Join("\r\n", clientInfo), Encoding.UTF8);
                    }
                    File.AppendAllText(loggerName, log_, Encoding.UTF8);
                }
            }
            catch (Exception ec)
            {
                loggers.Add(log_);
                Error(ec.ToString());
            }
        }


        private static string GetScreenSize()
        {
            return SystemInformation.VirtualScreen.Width + "*" + SystemInformation.VirtualScreen.Height;
        }

        /// <summary>
        /// 获取电脑设备类型
        /// </summary>
        /// <returns>Desktop台式机，Laptop笔记本</returns>
        private static string GetComputerType()
        {
            if (SystemInformation.PowerStatus.BatteryChargeStatus == BatteryChargeStatus.NoSystemBattery)
            {
                return "Desktop";
            }
            else
            {
                return "Laptop";
            }
        }
        private static string FromatItem(string name, string text)
        {
            return $"{name}:{text}";
        }

        private static string HandleMessage(string message)
        {
            message = message.Replace("\\u", "\\\\u");
            return message;
        }
    }
}
