using Core.Librarys;
using Core.Models;
using Core.Models.Config;
using Core.Servicers.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Core.Servicers.Instances
{
    /// <summary>
    /// 应用过滤服务。
    /// 从 Main.cs 中提取出的过滤逻辑，负责判断某个进程是否应被统计。
    /// 单一职责：进程过滤与白名单/黑名单/正则匹配。
    /// </summary>
    public class AppFilterService
    {
        private readonly IAppData _appData;

        /// <summary>默认忽略的进程</summary>
        private static readonly string[] DefaultIgnoreProcess = new string[] {
            "Tai", "SearchHost", "Taskmgr", "ApplicationFrameHost",
            "StartMenuExperienceHost", "ShellExperienceHost", "OpenWith",
            "Updater", "LockApp", "dwm", "SystemSettingsAdminFlows"
        };

        /// <summary>忽略进程缓存（正则匹配通过的缓存，避免重复计算）</summary>
        private readonly List<string> _ignoreProcessCache = new List<string>();

        private List<string> _configIgnoreProcessList = new List<string>();
        private List<string> _configIgnoreProcessRegxList = new List<string>();
        private List<string> _configProcessNameWhiteList = new List<string>();
        private List<string> _configProcessRegexWhiteList = new List<string>();

        public AppFilterService(IAppData appData)
        {
            _appData = appData;
        }

        /// <summary>
        /// 根据配置更新过滤规则列表。
        /// </summary>
        public void UpdateFromConfig(ConfigModel config)
        {
            if (config == null) return;

            _configIgnoreProcessList = config.Behavior.IgnoreProcessList
                .Where(m => !IsRegex(m)).ToList();
            _configIgnoreProcessRegxList = config.Behavior.IgnoreProcessList
                .Where(m => IsRegex(m)).ToList();

            _configProcessNameWhiteList = config.Behavior.ProcessWhiteList
                .Where(m => !IsRegex(m)).ToList();
            _configProcessRegexWhiteList = config.Behavior.ProcessWhiteList
                .Where(m => IsRegex(m)).ToList();

            _ignoreProcessCache.Clear();
        }

        private static bool IsRegex(string str)
        {
            return Regex.IsMatch(str, @"[\.|\*|\?|\{|\\|\[|\^|\|]");
        }

        /// <summary>
        /// 检查应用是否需要记录数据。
        /// 返回 null 表示过滤通过但 app 尚未创建（需要调用方创建）。
        /// </summary>
        public AppFilterResult CheckApp(string processName, string description, string file, ConfigModel config)
        {
            if (string.IsNullOrEmpty(file) || string.IsNullOrEmpty(processName)
                || DefaultIgnoreProcess.Contains(processName)
                || _ignoreProcessCache.Contains(processName))
            {
                return AppFilterResult.Ignored;
            }

            //  名称黑名单
            if (_configIgnoreProcessList.Contains(processName))
            {
                return AppFilterResult.Ignored;
            }

            //  正则表达式黑名单
            foreach (string reg in _configIgnoreProcessRegxList)
            {
                if (RegexHelper.IsMatch(processName, reg) || RegexHelper.IsMatch(file, reg))
                {
                    _ignoreProcessCache.Add(processName);
                    return AppFilterResult.Ignored;
                }
            }

            //  应用白名单过滤
            if (config.Behavior.IsWhiteList && config.Behavior.ProcessWhiteList.Count > 0)
            {
                bool isWhite = _configProcessNameWhiteList.Contains(processName);
                if (!isWhite)
                {
                    foreach (string reg in _configProcessRegexWhiteList)
                    {
                        if (RegexHelper.IsMatch(processName, reg) || RegexHelper.IsMatch(file, reg))
                        {
                            isWhite = true;
                            break;
                        }
                    }
                }
                if (!isWhite) return AppFilterResult.Ignored;
            }

            return AppFilterResult.Passed;
        }
    }

    /// <summary>
    /// 应用过滤结果。
    /// </summary>
    public enum AppFilterResult
    {
        /// <summary>过滤通过，允许统计</summary>
        Passed,
        /// <summary>被过滤规则拦截</summary>
        Ignored
    }
}
