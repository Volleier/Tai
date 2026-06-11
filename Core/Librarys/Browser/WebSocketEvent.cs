using Core.Event;
using Core.Models.WebPage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Librarys.Browser
{
    /// <summary>
    /// WebSocket 事件中心。将浏览器扩展发来的网页浏览数据分发给订阅者。
    /// 原先继承 WebSocketBehavior（不合理，该类仅作静态事件枢纽），现已移除。
    /// </summary>
    public static class WebSocketEvent
    {
        /// <summary>
        /// 收到浏览器扩展的网页浏览日志时触发。
        /// </summary>
        public static event WebServerEventHandler OnWebLog;

        /// <summary>
        /// 触发 OnWebLog 事件。
        /// </summary>
        /// <param name="args_">浏览器扩展发来的消息数据</param>
        public static void Invoke(NotifyWeb args_)
        {
            OnWebLog?.Invoke(args_);
        }
    }
}
