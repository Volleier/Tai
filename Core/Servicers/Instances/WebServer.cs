using Core.Event;
using Core.Librarys;
using Core.Librarys.Browser;
using Core.Models.WebPage;
using Core.Servicers.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Core.Servicers.Instances
{
    public class WebServer : WebSocketBehavior, IWebServer
    {
        private WebSocketServer _webSocket;
        private bool _isStart = false;

        /// <summary>
        /// 协议版本号。当消息格式发生变化时递增，用于前后向兼容性检查。
        /// </summary>
        private const int PROTOCOL_VERSION = 1;

        public void Start()
        {
            if (_isStart) return;
            try
            {
                _webSocket = new WebSocketServer(8908, false);
                _webSocket.AddWebSocketService<WebServer>("/TaiWebSentry");
                _webSocket.Start();
                _isStart = true;
            }
            catch (Exception ex)
            {
                Librarys.Logger.Error("无法启动浏览器服务，" + ex);
            }
        }

        public void Stop()
        {
            if (!_isStart) return;
            _webSocket?.Stop();
            _isStart = false;
        }

        public void SendMsg(string msg_)
        {
            try
            {
                if (!_isStart) return;

                _webSocket.WebSocketServices.Broadcast(msg_);
            }
            catch (Exception ec)
            {
                Librarys.Logger.Error(ec.ToString());
            }
        }


        protected override void OnMessage(MessageEventArgs e)
        {
            //  处理心跳 ping/pong（纯文本，非 JSON）
            if (string.IsNullOrEmpty(e.Data))
            {
                return;
            }
            if (e.Data.Trim() == "ping")
            {
                //  回复 pong 以保持连接活跃（可选）
                // Send("pong");
                return;
            }

            try
            {
                var log = JsonConvert.DeserializeObject<NotifyWeb>(e.Data);

                //  校验反序列化结果
                if (log == null)
                {
                    LogParseFailure("反序列化结果为 null", e.Data);
                    return;
                }

                //  校验必要字段
                if (string.IsNullOrEmpty(log.Url))
                {
                    LogParseFailure("缺少必要字段 Url", e.Data);
                    return;
                }
                if (log.Duration <= 0)
                {
                    //  时长为 0 或负数，跳过（可能是浏览器焦点切换时的零时长事件）
                    return;
                }

                WebSocketEvent.Invoke(log);
            }
            catch (JsonException ex)
            {
                LogParseFailure($"JSON 解析失败: {ex.Message}", e.Data);
            }
            catch (Exception ex)
            {
                LogParseFailure($"未预期的异常: {ex.Message}", e.Data);
            }
        }

        /// <summary>
        /// 记录消息解析失败日志，包含原始消息摘要（截断至 200 字符防止日志膨胀）。
        /// </summary>
        private void LogParseFailure(string reason, string rawData)
        {
            string summary = rawData.Length > 200
                ? rawData.Substring(0, 200) + "..."
                : rawData;
            Librarys.Logger.Error($"[WebServer] {reason}，原始消息摘要: {summary}");
        }
    }
}
