using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models.WebPage
{
    /// <summary>
    /// 浏览器扩展通过 WebSocket 发送给 Tai 的网页浏览数据。
    ///
    /// 协议版本 1：
    ///   Url         — 当前浏览的网页 URL（必填）
    ///   Title       — 网页标题
    ///   Icon        — 网页 favicon URL
    ///   Duration    — 浏览时长（秒，必填，>0）
    ///   ActiveTime  — 浏览起始时间（Unix 时间戳，秒）
    ///   Version     — 协议版本号（预留，用于前后向兼容性检查）
    /// </summary>
    public class NotifyWeb
    {
        /// <summary>当前浏览的网页 URL（必填）</summary>
        public string Url { get; set; }

        /// <summary>网页标题</summary>
        public string Title { get; set; }

        /// <summary>网页 favicon URL</summary>
        public string Icon { get; set; }

        /// <summary>浏览起始时间（Unix 时间戳，秒）</summary>
        public int ActiveTime { get; set; }

        /// <summary>浏览时长（秒，必填，>0）</summary>
        public int Duration { get; set; }

        /// <summary>
        /// 协议版本号。当前为 1。
        /// 浏览器扩展发送数据时应包含此字段，便于桌面端进行兼容性检查。
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// 将 Unix 时间戳转换为本地 DateTime。
        /// </summary>
        public DateTime ActiveDateTime
        {
            get
            {
                DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                return dateTime.AddSeconds(ActiveTime).ToLocalTime();
            }
        }
    }
}
