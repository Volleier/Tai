using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using UI.Controls.Models;
using UI.Models;

namespace UI.Controls
{
    /// <summary>
    /// 页面导航容器。
    ///
    /// 缓存策略：
    /// - IndexPage（主导航页，在 IndexUriList 中定义）→ 持久缓存，不自动销毁。
    /// - DetailPage（详情/子页）→ 返回时从缓存中释放（调用 ViewModel.Dispose 并移除引用）。
    ///
    /// 职责分离：
    /// - 导航历史管理（前进/后退）
    /// - 页面缓存与释放（Back 时销毁 DetailPage，ClearCache 时销毁所有页面）
    /// - 滚动位置恢复
    /// - 加载动画触发
    /// </summary>
    public class PageContainer : Control
    {
        /// <summary>DetailPage 缓存上限（防止内存泄漏）</summary>
        private const int MAX_DETAIL_CACHE = 20;

        #region 依赖属性
        public List<string> IndexUriList
        {
            get { return (List<string>)GetValue(IndexUriListProperty); }
            set { SetValue(IndexUriListProperty, value); }
        }
        public static readonly DependencyProperty IndexUriListProperty =
            DependencyProperty.Register("IndexUriList", typeof(List<string>), typeof(PageContainer));

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title",
                typeof(string),
                typeof(PageContainer));

        public IServiceProvider ServiceProvider
        {
            get { return (IServiceProvider)GetValue(ServiceProviderProperty); }
            set { SetValue(ServiceProviderProperty, value); }
        }
        public static readonly DependencyProperty ServiceProviderProperty =
            DependencyProperty.Register("ServiceProvider", typeof(IServiceProvider), typeof(PageContainer));

        public object Content
        {
            get { return (object)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }
        public static readonly DependencyProperty ContentProperty =
            DependencyProperty.Register("Content", typeof(object), typeof(PageContainer), new PropertyMetadata("Content undefined!"));

        public string Uri
        {
            get { return (string)GetValue(UriProperty); }
            set { SetValue(UriProperty, value); }
        }
        public static readonly DependencyProperty UriProperty =
            DependencyProperty.Register("Uri", typeof(string), typeof(PageContainer), new PropertyMetadata(string.Empty, new PropertyChangedCallback(OnUriChanged)));

        private static void OnUriChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as PageContainer;
            if (e.NewValue != e.OldValue)
            {
                string oldUri = e.OldValue.ToString();
                if (control.PageCache.ContainsKey(oldUri))
                {
                    PageModel page = control.PageCache[oldUri];
                    page.ScrollValue = control.ScrollViewer.VerticalOffset;
                }
                control.LoadPage();
            }
        }

        public bool IsShowTilteBar
        {
            get { return (bool)GetValue(IsShowTilteBarProperty); }
            set { SetValue(IsShowTilteBarProperty, value); }
        }
        public static readonly DependencyProperty IsShowTilteBarProperty =
            DependencyProperty.Register("IsShowTilteBar",
                typeof(bool),
                typeof(PageContainer), new PropertyMetadata(false, new PropertyChangedCallback(OnIsShowTitleBarChanged)));

        private static void OnIsShowTitleBarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as PageContainer;
            if (e.NewValue != e.OldValue)
            {
                control.TitleBarVisibility = control.IsShowTilteBar ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        public Visibility TitleBarVisibility
        {
            get { return (Visibility)GetValue(TitleBarVisibilityProperty); }
            set { SetValue(TitleBarVisibilityProperty, value); }
        }
        public static readonly DependencyProperty TitleBarVisibilityProperty =
            DependencyProperty.Register("TitleBarVisibility",
                typeof(Visibility),
                typeof(PageContainer), new PropertyMetadata(Visibility.Collapsed));

        public Command BackCommand
        {
            get { return (Command)GetValue(BackCommandProperty); }
            set { SetValue(BackCommandProperty, value); }
        }
        public static readonly DependencyProperty BackCommandProperty =
            DependencyProperty.Register("BackCommand",
                typeof(Command),
                typeof(PageContainer));


        public PageContainer Instance { get { return (PageContainer)GetValue(InstanceProperty); } set { SetValue(InstanceProperty, value); } }

        public static readonly DependencyProperty InstanceProperty = DependencyProperty.Register("Instance", typeof(PageContainer), typeof(PageContainer));
        #endregion

        /// <summary>
        /// 加载页面完成后发生
        /// </summary>
        public event EventHandler OnLoadPaged;

        private readonly string ProjectName;
        private List<string> Historys;
        public int Index = 0, OldIndex = 0;
        private Dictionary<string, PageModel> PageCache;
        private bool IsBack = false;
        private ScrollViewer ScrollViewer;
        private Frame Frame;
        public PageContainer()
        {
            DefaultStyleKey = typeof(PageContainer);
            ProjectName = "UI";
            Historys = new List<string>();
            BackCommand = new Command(new Action<object>(OnBackCommand));
            NavigationCommands.BrowseBack.InputGestures.Clear();
            NavigationCommands.BrowseForward.InputGestures.Clear();
            PageCache = new Dictionary<string, PageModel>();

            CreateAnimations();
        }


        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            ScrollViewer = GetTemplateChild("ScrollViewer") as ScrollViewer;
            Frame = GetTemplateChild("Frame") as Frame;
            Frame.NavigationService.Navigated += NavigationService_Navigated;
            Frame.NavigationService.LoadCompleted += NavigationService_LoadCompleted;
            Loaded += PageContainer_Loaded;
        }

        private void NavigationService_LoadCompleted(object sender, NavigationEventArgs e)
        {
            animation.Begin();
        }

        private void NavigationService_Navigated(object sender, NavigationEventArgs e)
        {
            Frame.NavigationService.RemoveBackEntry();
        }

        private void PageContainer_Loaded(object sender, RoutedEventArgs e)
        {
            Instance = this;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.ChangedButton == MouseButton.XButton1)
            {
                Back();
            }
        }
        private void OnBackCommand(object obj)
        {
            Back();
        }

        public void Back()
        {
            if (Index - 1 >= 0)
            {
                OldIndex = Index;
                Index--;
                string uri = Historys[Index];

                int preIndex = Index + 1;

                //  从缓存中释放上一页（DetailPage 销毁，IndexPage 保留）
                var pageUri = Historys[preIndex];
                if (PageCache.ContainsKey(pageUri))
                {
                    var page = PageCache[pageUri];
                    if (!page.IsIndexPage)
                    {
                        //  DetailPage：彻底释放
                        page.Dispose();
                        PageCache.Remove(pageUri);
                    }
                    //  IndexPage：保留在缓存中，仅移除历史记录
                }
                Historys.RemoveRange(preIndex, 1);

                IsBack = true;

                Uri = uri;

            }
        }

        public void ClearHistorys()
        {
            Historys.Clear();
        }

        /// <summary>
        /// 判断给定 URI 是否为主导航页。
        /// </summary>
        private bool IsIndexUri(string uri)
        {
            return IndexUriList != null && IndexUriList.Contains(uri);
        }

        private void LoadPage()
        {
            if (Uri != string.Empty)
            {
                if (IsIndexUri(Uri))
                {
                    Historys.Clear();
                    Index = 0;
                    OldIndex = 0;
                    Historys.Add(Uri);
                    if (!IsBack)
                    {
                        //  导航到主页 → 清理所有 DetailPage 缓存，保留 IndexPage
                        EvictDetailPages();
                    }
                }
                else
                {
                    //处理历史记录
                    if (OldIndex == Index)
                    {
                        //新开
                        Historys.Add(Uri);
                        Index++;
                    }
                    OldIndex = Index;
                }
                PageModel page = GetPage();


                if (page != null)
                {
                    Content = page.Instance;

                    //  加入缓存
                    if (!PageCache.ContainsKey(Uri))
                    {
                        PageCache.Add(Uri, page);
                        //  限制 DetailPage 缓存上限，防止内存泄漏
                        if (!page.IsIndexPage)
                        {
                            TrimDetailCache();
                        }
                    }

                    //  滚动条位置处理
                    if (IsBack)
                    {
                        ScrollViewer.ScrollToVerticalOffset(page.ScrollValue);
                    }
                    else
                    {
                        ScrollViewer?.ScrollToVerticalOffset(0);
                    }

                    OnLoadPaged?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    Debug.WriteLine("找不到Page：" + Uri + "，请确认已被注入");
                }
            }
            IsBack = false;
        }

        private PageModel GetPage()
        {
            Page page = null;
            if (PageCache.ContainsKey(Uri))
            {
                return PageCache[Uri];
            }
            Type pageType = Type.GetType(ProjectName + ".Views." + Uri);
            if (pageType != null && ServiceProvider != null)
            {
                page = ServiceProvider.GetService(pageType) as Page;
            }
            var newPage = new PageModel()
            {
                Instance = page,
                ScrollValue = 0,
                IsIndexPage = IsIndexUri(Uri)
            };

            return newPage;
        }

        /// <summary>
        /// 释放所有非主导航页（DetailPage）的缓存。
        /// 导航到主页时调用，确保返回主页后子页面被正确释放。
        /// </summary>
        private void EvictDetailPages()
        {
            var detailKeys = PageCache.Where(kvp => !kvp.Value.IsIndexPage)
                                      .Select(kvp => kvp.Key)
                                      .ToList();
            foreach (var key in detailKeys)
            {
                PageCache[key].Dispose();
                PageCache.Remove(key);
            }
        }

        /// <summary>
        /// 限制 DetailPage 缓存数量：超出上限时释放最早的非首页缓存。
        /// </summary>
        private void TrimDetailCache()
        {
            var detailKeys = PageCache.Where(kvp => !kvp.Value.IsIndexPage)
                                      .Select(kvp => kvp.Key)
                                      .ToList();
            if (detailKeys.Count > MAX_DETAIL_CACHE)
            {
                var oldestKey = detailKeys.First();
                PageCache[oldestKey].Dispose();
                PageCache.Remove(oldestKey);
            }
        }

        /// <summary>
        /// 清除所有页面缓存（包括主导航页），释放所有 ViewModel。
        /// </summary>
        private void ClearCache()
        {
            if (PageCache != null)
            {
                foreach (var kvp in PageCache)
                {
                    kvp.Value.Dispose();
                }
                PageCache.Clear();
            }
        }

        /// <summary>
        /// 释放 PageContainer 自身资源。
        /// </summary>
        public void Dispose()
        {
            //  取消事件订阅防止内存泄漏
            if (Frame?.NavigationService != null)
            {
                Frame.NavigationService.Navigated -= NavigationService_Navigated;
                Frame.NavigationService.LoadCompleted -= NavigationService_LoadCompleted;
            }
            Loaded -= PageContainer_Loaded;

            ClearCache();
            Content = null;
            DataContext = null;
            Instance = null;
        }

        private Storyboard animation;
        private void CreateAnimations()
        {
            ClipToBounds = true;

            ScaleTransform scaleTransform = new ScaleTransform();
            TranslateTransform translateTransform = new TranslateTransform();

            TransformGroup tfg = new TransformGroup();
            tfg.Children.Add(scaleTransform);
            tfg.Children.Add(translateTransform);
            RenderTransform = tfg;
            RenderTransformOrigin = new Point(.5, .5);

            animation = new Storyboard();

            DoubleAnimation scrollAnimation = new DoubleAnimation();
            scrollAnimation.From = 50;
            scrollAnimation.To = 0;
            scrollAnimation.Duration = new Duration(TimeSpan.FromSeconds(0.14));

            Storyboard.SetTarget(scrollAnimation, this);
            Storyboard.SetTargetProperty(scrollAnimation, new PropertyPath("RenderTransform.Children[1].Y"));
            animation.Children.Add(scrollAnimation);
        }
    }
}
