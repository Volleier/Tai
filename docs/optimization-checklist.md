## P0 优先级

### P0-1：修复解决方案无法稳定构建的问题

#### 现象

当前在 `dotnet msbuild` 下无法完成完整构建，主要报错包括：

- `Core` 缺失本地 `packages` 目录中的 SQLite targets
- `Updater` 无法解析 `Newtonsoft.Json`
- `TaiBug` 在当前构建链下出现 `InitializeComponent` / 入口点异常

#### 影响

- 新开发者无法直接接手
- 无法建立 CI
- 修改后难以验证是否引入回归
- 发版可靠性低

#### 建议修改文件

- [Core/Core.csproj](/e:/Tai/Core/Core.csproj:1)
- [UI/UI.csproj](/e:/Tai/UI/UI.csproj:1)
- [Updater/Updater.csproj](/e:/Tai/Updater/Updater.csproj:1)
- [TaiBug/TaiBug.csproj](/e:/Tai/TaiBug/TaiBug.csproj:1)
- `Core/packages.config`
- `UI/packages.config`
- `Updater/packages.config`
- 视情况新增：
  `NuGet.config`
  `Directory.Build.props`
  `Directory.Packages.props`

#### 建议改法

1. 先补齐缺失的 `packages` 恢复流程
2. 优先验证 Visual Studio 构建链与 `dotnet msbuild` 构建链的差异
3. 中期将 `packages.config` 迁移为 `PackageReference`
4. 为 `TaiBug` 和 `Updater` 单独做一次最小构建验证
5. 增加一份 `docs/build.md` 或在 `README.md` 中写清楚构建步骤

#### 建议验收标准

- `Tai.sln` 在一台干净 Windows 开发机可恢复依赖并编译
- `UI`、`Core`、`Updater`、`TaiBug` 可以独立编译
- 构建步骤无需手工复制 DLL

### P0-2：重构自定义弹窗的等待机制，移除忙等轮询

#### 问题描述

当前确认弹窗和输入弹窗使用 `Task.Run + while + Thread.Sleep(10)` 等待关闭。

#### 风险

- 不必要占用线程池线程
- 弹窗状态变量容易产生竞态
- 取消/关闭窗口等场景不容易正确收口
- 后续扩展弹窗类型会越来越难维护

#### 涉及文件

- [UI/Controls/Window/DefaultWindow.cs](/e:/Tai/UI/Controls/Window/DefaultWindow.cs:561)
- [UI/Controls/Window/DefaultWindow.cs](/e:/Tai/UI/Controls/Window/DefaultWindow.cs:643)
- [UI/Servicers/UIServicer.cs](/e:/Tai/UI/Servicers/UIServicer.cs:8)
- [UI/Servicers/IUIServicer.cs](/e:/Tai/UI/Servicers/IUIServicer.cs:8)

#### 建议改法

1. 使用 `TaskCompletionSource<bool>` 替代确认弹窗忙等
2. 使用 `TaskCompletionSource<string>` 替代输入弹窗忙等
3. 弹窗确认、取消、窗口关闭时统一 `TrySetResult` / `TrySetException`
4. 移除 `IsDialogConfirm`、`IsShowConfirmDialog`、`IsShowInputModal` 之间过度耦合的状态判断
5. 统一抽象“显示弹窗”和“完成结果”的生命周期

#### 建议改动方向

- 将 `ShowConfirmDialogAsync()` 改为真正的事件驱动异步
- 将 `ShowInputModalAsync()` 的取消行为统一改为返回 `null` 或 `bool + string` 的结果对象，避免直接抛普通 `Exception`

### P0-3：修复 UI 线程模型，避免后台线程直接更新绑定属性

#### 问题描述

多个 ViewModel 在 `Task.Run` 中直接修改绑定属性，而 `INotifyPropertyChanged` 没有切回 UI 线程。

#### 影响

- 有概率触发跨线程 UI 异常
- 页面刷新结果可能不稳定
- 后续一旦改成 `ObservableCollection` 更容易出错

#### 高风险文件

- [UI/Models/UINotifyPropertyChanged.cs](/e:/Tai/UI/Models/UINotifyPropertyChanged.cs:11)
- [UI/ViewModels/DataPageVM.cs](/e:/Tai/UI/ViewModels/DataPageVM.cs:150)
- [UI/ViewModels/ChartPageVM.cs](/e:/Tai/UI/ViewModels/ChartPageVM.cs:235)
- [UI/ViewModels/IndexPageVM.cs](/e:/Tai/UI/ViewModels/IndexPageVM.cs:178)
- [UI/ViewModels/DetailPageVM.cs](/e:/Tai/UI/ViewModels/DetailPageVM.cs:487)
- [UI/ViewModels/WebSiteDetailPageVM.cs](/e:/Tai/UI/ViewModels/WebSiteDetailPageVM.cs:286)
- [UI/ViewModels/CategoryAppListPageVM.cs](/e:/Tai/UI/ViewModels/CategoryAppListPageVM.cs:156)
- [UI/ViewModels/CategoryWebSiteListPageVM.cs](/e:/Tai/UI/ViewModels/CategoryWebSiteListPageVM.cs:53)

#### 建议改法

1. 后台线程只负责取数和计算
2. 计算完成后统一切回 `Application.Current.Dispatcher`
3. 把 UI 更新集中在一个方法里，例如 `ApplyData(...)`
4. 如果需要大量列表更新，考虑统一使用 `ObservableCollection` 并在 UI 线程批量替换
5. 视情况给 `UINotifyPropertyChanged` 增加线程保护，但不要把所有问题都压到基类上

#### 推荐落地方案

- 优先改 `ChartPageVM`、`DataPageVM`
- 再改明细页和分类页
- 最后统一清理其他 `Task.Run` 中的属性写入

### P0-4：重构数据库并发控制，避免 `Thread.Sleep` 轮询与上下文误释放

#### 问题描述

数据库读写协调当前采用手写布尔状态位和 `Thread.Sleep(1000)` 轮询等待。

更高风险的是：

- `GetReaderContext()` 会尝试释放 `_writerContext`
- `_readerNum` 与状态位维护方式比较脆弱

#### 涉及文件

- [Core/Servicers/Instances/Database.cs](/e:/Tai/Core/Servicers/Instances/Database.cs:15)
- [Core/Servicers/Interfaces/IDatabase.cs](/e:/Tai/Core/Servicers/Interfaces/IDatabase.cs:1)
- [Core/Librarys/SQLite/TaiDbContext.cs](/e:/Tai/Core/Librarys/SQLite/TaiDbContext.cs:1)
- [Core/Servicers/Instances/WebData.cs](/e:/Tai/Core/Servicers/Instances/WebData.cs:77)
- [Core/Servicers/Instances/Data.cs](/e:/Tai/Core/Servicers/Instances/Data.cs:1)

#### 影响

- 高并发时可能阻塞严重
- 写入时机不透明
- 可能产生资源释放时序错误
- 后期排查“偶发无响应”会很痛苦

#### 建议改法

1. 先明确 `IDatabase` 是“每次请求创建新上下文”还是“共享写上下文”
2. 推荐改成每次操作独立 `DbContext`
3. 若必须做串行写入，建议用 `SemaphoreSlim`
4. 不要在 Reader 获取逻辑里释放 Writer 上下文
5. 统一让 `using` 负责上下文生命周期，尽量取消 `CloseWriter()` / `CloseReader()` 这种外部关闭模式

### P0-5：治理 `WebData` 中 fire-and-forget 写库逻辑，保证数据一致性

#### 问题描述

`WebData` 中多处通过 `Task.Run` 异步写入数据库，调用方拿不到执行结果，也没有统一的队列和失败恢复机制。

#### 涉及文件

- [Core/Servicers/Instances/WebData.cs](/e:/Tai/Core/Servicers/Instances/WebData.cs:39)
- [Core/Servicers/Instances/WebData.cs](/e:/Tai/Core/Servicers/Instances/WebData.cs:164)
- [Core/Servicers/Instances/WebData.cs](/e:/Tai/Core/Servicers/Instances/WebData.cs:366)
- [Core/Servicers/Instances/WebData.cs](/e:/Tai/Core/Servicers/Instances/WebData.cs:426)
- [Core/Servicers/Interfaces/IWebData.cs](/e:/Tai/Core/Servicers/Interfaces/IWebData.cs:1)

#### 影响

- 网页统计可能出现丢数或时序错乱
- 异常只写日志，调用链无法感知
- 多线程同时命中相同 URL/站点时更难保证正确性

#### 建议改法

1. 明确哪些操作必须同步完成，哪些允许后台处理
2. 对写库操作建立串行消费队列
3. 给 URL 创建、站点创建、日志累加这类关键路径增加幂等性保护
4. 统一网页日志入队入口，避免分散 `Task.Run`
5. 在高频日志链路中引入批处理或按时间片合并

## 4. P1 优先级

### P1-1：减少 `async void` 业务方法，统一异步错误处理

#### 问题描述

项目里存在较多 `async void`，其中不少不是单纯事件处理器。

#### 涉及文件示例

- [Core/Servicers/Instances/Main.cs](/e:/Tai/Core/Servicers/Instances/Main.cs:139)
- [Core/Servicers/Instances/Sleepdiscover.cs](/e:/Tai/Core/Servicers/Instances/Sleepdiscover.cs:230)
- [UI/Servicers/StatusBarIconServicer.cs](/e:/Tai/UI/Servicers/StatusBarIconServicer.cs:123)
- [UI/ViewModels/CategoryPageVM.cs](/e:/Tai/UI/ViewModels/CategoryPageVM.cs:314)
- [UI/ViewModels/DataPageVM.cs](/e:/Tai/UI/ViewModels/DataPageVM.cs:150)
- [UI/ViewModels/DetailPageVM.cs](/e:/Tai/UI/ViewModels/DetailPageVM.cs:65)

#### 建议改法

1. 非事件处理器改为 `async Task`
2. 命令层若不支持 `Task`，考虑引入异步命令封装
3. 统一记录异步异常
4. 避免“看起来异步，实际上无人等待”的调用方式

### P1-2：优化托盘加载等待逻辑，去掉空转循环

#### 问题描述

托盘加载状态观察使用无限循环，不包含节流等待。

#### 涉及文件

- [UI/Servicers/StatusBarIconServicer.cs](/e:/Tai/UI/Servicers/StatusBarIconServicer.cs:123)
- [Core/AppState.cs](/e:/Tai/Core/AppState.cs:1)

#### 风险

- 启动期间空转占用 CPU
- 文本更新频率不可控

#### 建议改法

1. 在循环中加入 `Task.Delay`
2. 或使用事件通知方式替代轮询
3. 把 `AppState` 进度变更改为事件驱动

### P1-3：重构主题加载机制，减少资源字典重复替换风险

#### 问题描述

当前主题切换通过移除/添加资源字典实现，可工作，但后期扩展高。

#### 涉及文件

- [UI/App.xaml](/e:/Tai/UI/App.xaml:19)
- [UI/Servicers/ThemeServicer.cs](/e:/Tai/UI/Servicers/ThemeServicer.cs:63)
- [UI/Resources/Themes/Light.xaml](/e:/Tai/UI/Resources/Themes/Light.xaml:1)
- [UI/Resources/Themes/Dark.xaml](/e:/Tai/UI/Resources/Themes/Dark.xaml:1)
- [UI/Themes/Generic.xaml](/e:/Tai/UI/Themes/Generic.xaml:1)

#### 建议改法

1. 规范“主题资源”和“控件模板资源”的边界
2. 为白底、深底、危险色、提示色建立统一语义资源
3. 减少控件模板内硬编码 `White`、`Gray`、`#ccc`
4. 在 `App.xaml` 中明确初始主题加载策略

### P1-4：完善 WebSocket 错误处理与协议边界

#### 问题描述

`WebServer.OnMessage()` 中直接吞掉异常，不利于定位扩展侧问题。

#### 涉及文件

- [Core/Servicers/Instances/WebServer.cs](/e:/Tai/Core/Servicers/Instances/WebServer.cs:62)
- [Core/Librarys/Browser/WebSocketEvent.cs](/e:/Tai/Core/Librarys/Browser/WebSocketEvent.cs:1)
- `WebExtensions/Chrome/service-worker.js`
- `WebExtensions/Chrome/manifest.json`

#### 建议改法

1. 解析失败时记录原始消息摘要
2. 校验消息字段合法性
3. 为协议版本预留字段
4. 明确扩展端和桌面端的消息格式文档

### P1-5：减少主窗口尺寸变化时的频繁配置落盘

#### 问题描述

窗口尺寸变化时每次 `SizeChanged` 都触发保存配置。

#### 涉及文件

- [UI/Servicers/ThemeServicer.cs](/e:/Tai/UI/Servicers/ThemeServicer.cs:169)
- [Core/Servicers/Instances/AppConfig.cs](/e:/Tai/Core/Servicers/Instances/AppConfig.cs:1)

#### 风险

- 拖拽窗口时频繁写文件
- 增加 IO 压力
- 容易产生不必要的配置写入

#### 建议改法

1. 使用防抖保存
2. 在 `SizeChanged` 中只缓存尺寸
3. 在窗口关闭或停止拖拽后统一保存

## 5. P2 优先级

### P2-1：梳理页面缓存与释放策略，减少页面实例生命周期复杂度

#### 问题描述

`PageContainer` 同时负责历史、缓存、滚动恢复和页面销毁，职责偏多。

#### 涉及文件

- [UI/Controls/PageContainer.cs](/e:/Tai/UI/Controls/PageContainer.cs:18)
- [UI/Controls/Models/PageModel.cs](/e:/Tai/UI/Controls/Models/PageModel.cs:1)
- 各页面 `ViewModel.Dispose()` 实现

#### 建议改法

1. 区分“主导航页缓存”和“详情页临时页”
2. 统一页面销毁协议
3. 检查事件解绑是否完整
4. 避免缓存页面持有过多后台任务或事件订阅

### P2-2：整理 Core 主协调器 `Main.cs`，降低单类复杂度

#### 问题描述

`Main.cs` 承担了过多职责：初始化、配置变更、应用判断、网页统计、睡眠处理、服务启停等。

#### 涉及文件

- [Core/Servicers/Instances/Main.cs](/e:/Tai/Core/Servicers/Instances/Main.cs:28)
- 相关接口：
  `IMain.cs`
  `IAppTimerServicer.cs`
  `IWebFilter.cs`
  `IWebData.cs`

#### 建议改法

1. 拆出启动编排器
2. 拆出应用过滤策略服务
3. 拆出网页事件处理服务
4. 拆出配置变更处理器

### P2-3：统一导入导出与用户提示方式

#### 问题描述

有的地方用自定义 Toast / Dialog，有的地方直接 `MessageBox.Show`，交互风格不统一。

#### 涉及文件

- [UI/Controls/SettingPanel/SettingPanel.cs](/e:/Tai/UI/Controls/SettingPanel/SettingPanel.cs:558)
- [TaiBug/MainWindow.xaml.cs](/e:/Tai/TaiBug/MainWindow.xaml.cs:46)
- 多个 ViewModel 和 Servicer 文件

#### 建议改法

1. 主程序统一使用 `IUIServicer`
2. 将重要确认类操作统一为自定义确认弹窗
3. 将成功/失败提示统一为 Toast 或统一样式弹窗

### P2-4：为统计链路补充更系统的日志点

#### 问题描述

当前虽有 `Logger`，但关键信息并未系统化。

#### 涉及文件

- [Core/Librarys/Logger.cs](/e:/Tai/Core/Librarys/Logger.cs:1)
- [Core/Servicers/Instances/Main.cs](/e:/Tai/Core/Servicers/Instances/Main.cs:28)
- [Core/Servicers/Instances/WebData.cs](/e:/Tai/Core/Servicers/Instances/WebData.cs:26)
- [Core/Servicers/Instances/WebServer.cs](/e:/Tai/Core/Servicers/Instances/WebServer.cs:19)

#### 建议改法

1. 对启动、配置变更、网页消息、数据库写入失败建立统一日志标签
2. 增加关键 ID 或 URL 摘要
3. 统一错误日志格式，便于搜索

## 6. P3 优先级

### P3-1：统一命名与目录拼写

#### 问题描述

项目中存在 `Servicers`、`Librarys` 等非标准拼写。

#### 涉及范围

- `UI/Servicers/`
- `Core/Servicers/`
- `Core/Librarys/`
- `UI/Librays/`

#### 建议改法

1. 新代码先不要继续沿用旧拼写
2. 中期通过重构工具统一迁移到 `Services`、`Libraries`
3. 同步修正命名空间

### P3-2：清理硬编码颜色，提升主题一致性

#### 问题描述

大量 XAML 直接写死 `White`、`Gray`、`#ccc`、`#5c5c5c` 等颜色。

#### 涉及文件示例

- `UI/Views/*.xaml`
- `UI/Themes/**/*.xaml`
- [UI/Resources/Themes/Light.xaml](/e:/Tai/UI/Resources/Themes/Light.xaml:1)
- [UI/Resources/Themes/Dark.xaml](/e:/Tai/UI/Resources/Themes/Dark.xaml:1)

#### 建议改法

1. 增加语义资源：
   `CardTextBrush`
   `DangerTextBrush`
   `DialogTextBrush`
   `MutedTextBrush`
2. 页面里优先使用 `DynamicResource`
3. 避免同一种语义颜色在多个文件手写

### P3-3：修复 README 编码与文档可读性

#### 问题描述

当前 `README.md` 在当前环境下存在乱码显示。

#### 涉及文件

- [README.md](/e:/Tai/README.md:1)

#### 建议改法

1. 统一为 UTF-8 编码
2. 明确依赖环境、安装方式、浏览器扩展安装方式
3. 增加开发说明和构建说明

### P3-4：补充开发文档

#### 建议新增文档

- `docs/build.md`
- `docs/runtime-flow.md`
- `docs/web-extension-protocol.md`
- `docs/database-schema.md`

## 7. 推荐执行顺序

如果按两到三轮迭代推进，建议顺序如下。

### 第一轮

- P0-1 构建修复
- P0-2 弹窗等待机制重构
- P0-3 UI 线程模型修复
- P0-4 数据库并发控制重构

### 第二轮

- P0-5 网页统计写入链路治理
- P1-1 `async void` 清理
- P1-2 托盘加载轮询优化
- P1-5 配置落盘防抖

### 第三轮

- P1-3 主题资源治理
- P1-4 WebSocket 协议治理
- P2-1 页面缓存治理
- P2-2 `Main.cs` 拆分
- P3 类规范与文档整理

## 8. 最值得优先安排的人天

如果只能先投入一小段时间，建议优先做下面 4 件事：

1. 让解决方案能稳定构建
2. 重构 `DefaultWindow` 弹窗异步模型
3. 修复 `ViewModel` 后台线程更新 UI 属性的问题
4. 重构 `Database` 读写并发控制

这 4 项会明显提高：

- 可维护性
- 回归验证效率
- 崩溃排查效率
- 后续迭代稳定性
