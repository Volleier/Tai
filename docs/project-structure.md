# Tai 项目结构与运行机制

## 1. 项目概览

`Tai` 是一个 Windows 桌面端使用时长统计工具，核心目标是：

- 统计前台应用的使用时长
- 统计浏览器网页/站点的访问时长
- 按天、周、月、年展示统计结果
- 支持分类、白名单、忽略规则、导出和主题设置

整个解决方案由 5 个主要部分组成：

| 模块 | 路径 | 作用 |
|---|---|---|
| 主程序 UI | `UI/` | WPF 前端、页面展示、托盘交互、主题、弹窗、自定义控件 |
| 核心逻辑 | `Core/` | 应用监听、网页统计、数据库访问、配置、分类、日志、导出等 |
| 更新器 | `Updater/` | 版本检查、下载新版本、解压升级 |
| 崩溃提示程序 | `TaiBug/` | 主程序崩溃后展示说明窗口，引导用户反馈日志 |
| 浏览器扩展 | `WebExtensions/Chrome/` | 采集网页信息，通过 WebSocket 发送给主程序 |

解决方案文件是 [Tai.sln](/e:/Tai/Tai.sln:1)。

## 2. 顶层目录结构

### 2.1 根目录

| 路径 | 说明 |
|---|---|
| `Core/` | 核心业务库 |
| `UI/` | 主界面与 WPF 控件 |
| `Updater/` | 更新器程序 |
| `TaiBug/` | 崩溃提示程序 |
| `WebExtensions/` | 浏览器扩展 |
| `README.md` | 项目说明 |
| `privacy.txt` | 隐私说明 |
| `Tai.sln` | Visual Studio 解决方案 |

### 2.2 工程依赖关系

当前核心依赖关系是：

- `UI -> Core`
- `Updater` 独立运行
- `TaiBug` 独立运行
- `WebExtensions` 通过网络协议和 `Core` 通信，不直接项目引用

## 3. UI 项目结构

`UI` 是主程序入口，负责：

- 应用启动
- 依赖注入
- 托盘图标与主窗口
- 页面切换
- 主题切换
- 自定义控件与样式

### 3.1 入口文件

| 文件 | 作用 |
|---|---|
| [UI/App.xaml](/e:/Tai/UI/App.xaml:1) | Application 资源入口，默认加载 Light 主题与通用资源 |
| [UI/App.xaml.cs](/e:/Tai/UI/App.xaml.cs:24) | 应用启动、DI 注册、单实例控制、全局异常处理 |
| [UI/MainWindow.xaml](/e:/Tai/UI/MainWindow.xaml:1) | 主窗口布局，继承自自定义 `DefaultWindow` |
| [UI/MainWindow.xaml.cs](/e:/Tai/UI/MainWindow.xaml.cs:19) | 主窗口代码隐藏文件 |

### 3.2 View / ViewModel / Model

该项目整体使用 MVVM 风格，但实现上是“轻量 MVVM + 自定义控件 + 服务层”。

| 目录 | 说明 |
|---|---|
| `UI/Views/` | 页面视图，如概览、统计、详细、分类、设置 |
| `UI/ViewModels/` | 页面逻辑、命令、数据装配 |
| `UI/Models/` | UI 绑定模型、通知基类、页面状态模型 |

主要页面包括：

- `IndexPage`
  概览页，展示高频应用/网站摘要

- `ChartPage`
  图表页，展示天/周/月/年维度的统计图

- `DataPage`
  详细数据页，展示表格型明细

- `CategoryPage`
  分类管理页

- `SettingPage`
  设置页

- `DetailPage`
  单应用详情页

- `WebSiteDetailPage`
  单网站详情页

### 3.3 自定义控件与主题

`UI/Controls/` 与 `UI/Themes/` 是这个项目很重要的一部分。

#### 控件层

| 目录 | 作用 |
|---|---|
| `UI/Controls/Window/` | 自定义窗口基类、Toast、确认弹窗、输入弹窗 |
| `UI/Controls/Navigation/` | 左侧导航栏 |
| `UI/Controls/Charts/` | 自绘图表控件 |
| `UI/Controls/Select/` | 下拉选择、日期选择、图片选择 |
| `UI/Controls/SettingPanel/` | 设置面板组件 |
| `UI/Controls/Input/` | 输入框组件 |
| `UI/Controls/Base/` | 基础图标、图片、文字、占位、颜色选择等 |
| `UI/Controls/PageContainer.cs` | 页面容器、路由、历史、缓存 |

#### 样式层

| 目录 | 作用 |
|---|---|
| `UI/Themes/Generic.xaml` | 全局控件样式入口 |
| `UI/Themes/Window/` | 自定义窗口模板 |
| `UI/Themes/Base/` | 基础控件模板 |
| `UI/Themes/Button/` | 按钮样式 |
| `UI/Themes/Charts/` | 图表样式 |
| `UI/Themes/Navigation/` | 导航样式 |
| `UI/Themes/Input/` | 输入框样式 |
| `UI/Resources/Themes/Light.xaml` | Light 主题资源 |
| `UI/Resources/Themes/Dark.xaml` | Dark 主题资源 |
| `UI/Resources/Themes/Common.xaml` | 通用资源 |

#### 典型机制

- `DefaultWindow`
  通过 [UI/Controls/Window/DefaultWindow.cs](/e:/Tai/UI/Controls/Window/DefaultWindow.cs:19) 和 [UI/Themes/Window/DefaultWindow.xaml](/e:/Tai/UI/Themes/Window/DefaultWindow.xaml:1) 实现统一窗口皮肤、标题栏、Toast、确认弹窗和输入弹窗。

- `PageContainer`
  通过 [UI/Controls/PageContainer.cs](/e:/Tai/UI/Controls/PageContainer.cs:18) 管理页面切换、历史栈和页面缓存，承担简化版路由职责。

### 3.4 UI 服务层

`UI/Servicers/` 是前端服务层，负责把 Core 和界面连接起来。

主要服务：

| 文件 | 作用 |
|---|---|
| [UI/Servicers/MainServicer.cs](/e:/Tai/UI/Servicers/MainServicer.cs:10) | 启动 UI 侧服务并衔接 Core 启动完成事件 |
| [UI/Servicers/StatusBarIconServicer.cs](/e:/Tai/UI/Servicers/StatusBarIconServicer.cs:18) | 托盘图标、托盘菜单、显示主窗口 |
| [UI/Servicers/ThemeServicer.cs](/e:/Tai/UI/Servicers/ThemeServicer.cs:17) | 主题切换、窗口样式、主题色更新 |
| [UI/Servicers/UIServicer.cs](/e:/Tai/UI/Servicers/UIServicer.cs:8) | 对外暴露确认弹窗、输入弹窗接口 |
| `AppContextMenuServicer.cs` | 应用上下文菜单 |
| `WebSiteContextMenuServicer.cs` | 网站上下文菜单 |
| `InputServicer.cs` | 输入监听与快捷操作 |

## 4. Core 项目结构

`Core` 是业务核心，负责：

- 前台窗口/应用监听
- 使用时长累积
- 网页访问日志接收与存储
- 配置、分类、过滤规则
- SQLite 数据访问
- 导入导出与日志

### 4.1 核心入口

最重要的核心类是 [Core/Servicers/Instances/Main.cs](/e:/Tai/Core/Servicers/Instances/Main.cs:28)。

它承担以下职责：

1. 初始化程序数据目录
2. 数据库自检
3. 加载应用信息与分类信息
4. 加载配置
5. 初始化网页过滤器
6. 启动应用观察器、计时器、网页服务、睡眠监测
7. 响应配置变化
8. 响应睡眠/唤醒
9. 响应网页日志事件

### 4.2 服务分层

`Core/Servicers/Interfaces/` 定义接口，`Core/Servicers/Instances/` 提供实现。

重点服务如下：

| 文件 | 作用 |
|---|---|
| `Main.cs` | Core 启动协调器 |
| `AppObserver.cs` | 监听前台窗口切换 |
| `AppTimerServicer.cs` | 定时累计应用时长 |
| `AppManager.cs` | 获取应用信息、进程信息 |
| `WindowManager.cs` | 获取窗口标题等窗口信息 |
| `Data.cs` | 应用统计数据读取与聚合 |
| `WebData.cs` | 网站/URL 数据存储与查询 |
| `WebServer.cs` | WebSocket 服务端，接收浏览器扩展数据 |
| `WebFilter.cs` | 网页过滤规则处理 |
| `Sleepdiscover.cs` | 离开电脑/睡眠状态检测 |
| `AppConfig.cs` | 配置加载、保存、配置变更通知 |
| `Categorys.cs` | 分类数据管理 |
| `Database.cs` | SQLite 上下文创建与读写协调 |
| `DateTimeObserver.cs` | 时间变化观察 |
| `AppData.cs` | 应用数据缓存与加载 |

### 4.3 数据模型

`Core/Models/` 下包含多类模型：

| 子目录 | 说明 |
|---|---|
| `AppObserver/` | 前台应用、窗口信息模型 |
| `Config/` | 配置模型，如主题、行为、开机自启、白名单 |
| `Db/` | 数据库实体模型 |
| `Data/` | 图表与统计聚合结果模型 |
| `WebPage/` | 网站/网页通知模型 |

数据库相关实体主要包括：

- `WebBrowseLogModel`
- `WebSiteModel`
- `WebUrlModel`
- `WebSiteCategoryModel`

另外还有：

- `AppModel`
- `DailyLogModel`
- `HoursLogModel`

### 4.4 基础库与工具类

`Core/Librarys/` 包含大量底层工具：

| 文件 | 作用 |
|---|---|
| `Logger.cs` | 文件日志记录 |
| `FileHelper.cs` | 文件目录相关辅助 |
| `ProcessHelper.cs` | 启动进程 |
| `RegexHelper.cs` | 正则封装 |
| `Shortcut.cs` | 快捷方式处理 |
| `Iconer.cs` | 图标提取 |
| `SystemCommon.cs` | 系统级操作，如开机自启 |
| `Time.cs` | 时间格式化、日期范围计算 |
| `Win32API.cs` | Win32 API 封装 |
| `Browser/` | URL、网页事件、favicon 下载 |
| `SQLite/` | EF + SQLite 相关配置和上下文 |

## 5. Updater 项目结构

`Updater` 是独立程序，不和主 UI 共进程运行。

主要职责：

- 访问 GitHub Release
- 对比当前版本和最新版本
- 下载更新包
- 解压文件
- 引导完成升级

关键文件：

| 文件 | 作用 |
|---|---|
| `Updater/App.xaml.cs` | 更新器启动入口 |
| `Updater/MainWindow.xaml` | 更新器界面 |
| [Updater/GithubRelease.cs](/e:/Tai/Updater/GithubRelease.cs:14) | 获取 GitHub 最新版本信息 |
| `Updater/Unzip.cs` | 解压更新包 |
| `Updater/MainModel.cs` | 更新器界面绑定模型 |

## 6. TaiBug 项目结构

`TaiBug` 是崩溃提示程序，用于在主程序崩溃后展示说明窗口。

触发来源：

- [UI/App.xaml.cs](/e:/Tai/UI/App.xaml.cs:46) 捕获 `DispatcherUnhandledException`
- 写入日志
- 启动 `TaiBug.exe`
- 主程序退出

`TaiBug` 的主要职责：

- 告知用户发生了异常
- 引导定位日志文件
- 引导提交 Issue
- 提供重启主程序入口

## 7. WebExtensions 浏览器扩展结构

当前仓库里可见的是 Chrome 扩展：

| 文件 | 作用 |
|---|---|
| `WebExtensions/Chrome/manifest.json` | 扩展清单 |
| `WebExtensions/Chrome/service-worker.js` | 后台脚本 |
| `WebExtensions/Chrome/icons/*` | 图标资源 |

扩展的大致职责是：

1. 监听标签页和活动网页变化
2. 收集 URL、标题、favicon 等信息
3. 将数据通过 WebSocket 发送到本机的 `8908` 端口
4. 主程序 `Core.WebServer` 接收到消息后转换为站点/网页访问日志

服务端入口在 [Core/Servicers/Instances/WebServer.cs](/e:/Tai/Core/Servicers/Instances/WebServer.cs:19)。

## 8. 项目是如何运作的

这里按“从启动到统计展示”的顺序说明。

### 8.1 应用启动流程

1. WPF 应用启动，进入 [UI/App.xaml.cs](/e:/Tai/UI/App.xaml.cs:24)
2. 注册依赖注入容器，注入 `Core` 和 `UI` 层服务
3. 执行单实例检查
4. 获取 `IMainServicer` 并调用 `Start()`
5. `MainServicer` 初始化托盘图标
6. `MainServicer` 调用 `Core.Main.Run()`
7. `Core.Main` 完成数据库自检、应用数据加载、分类数据加载、配置加载
8. `Core.Main` 启动应用监听、计时器、网页服务、睡眠检测
9. `Core.Main` 触发 `OnStarted`
10. `MainServicer` 在收到事件后初始化主题、输入服务、上下文菜单，并按配置决定是否显示主窗口

### 8.2 前台应用统计流程

1. `AppObserver` 注册 Win32 前台窗口切换钩子
2. 当前台窗口变化时，获取进程与窗口信息
3. 把活动应用切换事件抛给 `Core.Main`
4. `Core.Main` 根据忽略规则、白名单、正则规则判断是否记录
5. `AppTimerServicer` 或相关统计逻辑按时间累积时长
6. 数据写入 SQLite
7. UI 页面通过 `Data` 服务读取聚合后的数据并展示

应用监听入口在 [Core/Servicers/Instances/AppObserver.cs](/e:/Tai/Core/Servicers/Instances/AppObserver.cs:18)。

### 8.3 浏览器网页统计流程

1. 浏览器扩展检测网页变化
2. 扩展把网页日志通过 WebSocket 发给本地服务
3. `WebServer` 接收消息并转成 `NotifyWeb`
4. `WebSocketEvent` 广播网页日志事件
5. `Core.Main` 或 `WebData` 侧接收事件并处理站点、URL 和浏览时长
6. 统计结果落库
7. UI 图表页和详情页读取网站统计并展示

### 8.4 UI 展示流程

1. 用户通过托盘图标显示主窗口
2. 主窗口绑定 `MainViewModel`
3. 导航栏切换修改 `Uri`
4. `PageContainer` 根据 `Uri` 创建或复用页面实例
5. 页面对应的 ViewModel 从 `Core` 服务拉取数据
6. 页面通过自定义图表控件、列表控件渲染数据

`PageContainer` 在这个流程里是关键中枢，负责路由、历史和页面缓存。

### 8.5 主题切换流程

1. 用户修改配置中的主题
2. `AppConfig` 触发 `ConfigChanged`
3. `ThemeServicer` 收到事件
4. 动态替换 `Application.Resources.MergedDictionaries`
5. 更新 `ThemeColor` 与 `ThemeBrush`
6. 更新主窗口样式与上下文菜单样式

主题切换核心实现见 [UI/Servicers/ThemeServicer.cs](/e:/Tai/UI/Servicers/ThemeServicer.cs:17)。

### 8.6 崩溃处理流程

1. UI 层出现未处理异常
2. `App_DispatcherUnhandledException` 捕获异常
3. 使用 `Logger` 保存日志
4. 启动 `TaiBug.exe`
5. 主程序退出

## 9. 页面与业务的大致映射关系

| 页面 | ViewModel | 依赖的核心服务 |
|---|---|---|
| `IndexPage` | `IndexPageVM` | `IData`, `IWebData`, `ICategorys` |
| `ChartPage` | `ChartPageVM` | `IData`, `IWebData`, `ICategorys` |
| `DataPage` | `DataPageVM` | `IData`, `IWebData` |
| `DetailPage` | `DetailPageVM` | `IData`, `IAppData`, `IUIServicer` |
| `WebSiteDetailPage` | `WebSiteDetailPageVM` | `IWebData`, `IUIServicer` |
| `CategoryPage` | `CategoryPageVM` | `ICategorys`, `IAppConfig`, `IUIServicer` |
| `SettingPage` | `SettingPageVM` | `IAppConfig`, `IMain`, `IUIServicer` |

## 10. 当前结构的优点

- 职责分层总体清晰，`UI` 与 `Core` 已经做了项目级拆分
- 核心业务逻辑集中在 `Core`，方便未来测试和重构
- 自定义控件和主题资源较完整，界面具有独立风格
- 支持托盘常驻、网页统计、分类和导出，功能面比较完整

## 11. 当前结构的主要风险

从结构上看，当前项目的主要风险点有：

- 工程体系仍偏旧，构建依赖本地环境
- `UI` 页面与 `ViewModel` 中存在较多后台线程直接改绑定数据的写法
- `Core` 的并发控制和异步模型不够稳定
- 自定义控件中承担了较多交互和状态职责，后续维护成本会逐步上升

这些问题的具体拆解见：

- [优化清单与优先级建议](./optimization-checklist.md)
