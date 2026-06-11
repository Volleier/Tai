using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace UI.Controls.Models
{
    /// <summary>
    /// 页面缓存模型。
    /// 区分页面类型以优化缓存策略：
    /// - IndexPage（主导航页）→ 持久缓存，退回主页时不销毁
    /// - DetailPage（详情/子页）→ 临时缓存，退回时释放
    /// </summary>
    public class PageModel
    {
        /// <summary>
        /// 页面实例
        /// </summary>
        public Page Instance { get; set; }

        /// <summary>
        /// 滚动条位置（用于返回时恢复滚动位置）
        /// </summary>
        public double ScrollValue { get; set; }

        /// <summary>
        /// 页面类型：true 表示主导航页（持久缓存），false 表示详情页（可释放）。
        /// </summary>
        public bool IsIndexPage { get; set; }

        /// <summary>
        /// 页面是否已被释放。
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// 释放页面资源：解绑 DataContext、置空 Content、调用 ViewModel Dispose。
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;

            if (Instance?.DataContext is UI.Models.ModelBase vm)
            {
                vm.Dispose();
            }
            if (Instance != null)
            {
                Instance.Content = null;
                Instance.DataContext = null;
            }
            Instance = null;
        }
    }
}
