# 构建指南

## 前置依赖

| 依赖 | 最低版本 | 说明 |
|------|---------|------|
| .NET SDK | 6.0+ | 提供 `dotnet build` 命令和 MSBuild 工具链 |
| .NET Framework 4.8 Runtime | 4.8 | 运行 Tai 所需（Win10+ 已内置） |

**无需安装 Visual Studio** — 仅需 .NET SDK 即可完成构建。

下载安装 [.NET SDK](https://dotnet.microsoft.com/en-us/download) 后验证：

```bash
dotnet --version   # 应输出 6.0 或更高版本
```

## 快速开始

```bash
# 克隆仓库
git clone https://github.com/Planshit/Tai.git
cd Tai

# 构建整个解决方案（Debug）
dotnet build Tai.sln

# 构建 Release 版本
dotnet build Tai.sln -c Release

# 单独构建某个项目
dotnet build Core/Core.csproj
dotnet build UI/UI.csproj
dotnet build Updater/Updater.csproj
dotnet build TaiBug/TaiBug.csproj
```

NuGet 包会在首次构建时自动恢复，无需额外步骤。

## 构建产物

| 项目 | 输出 | 路径 |
|------|------|------|
| Core | Core.dll | `Core/bin/Debug/net48/` |
| UI | Tai.exe | `UI/bin/Debug/net48/` |
| Updater | Updater.exe | `Updater/bin/Debug/net48/` |
| TaiBug | TaiBug.exe | `TaiBug/bin/Debug/net48/` |

## 项目结构

所有项目已迁移为 **SDK 风格 .csproj** 格式（`<Project Sdk="...">`），使用 `<PackageReference>` 管理 NuGet 依赖。

```
Tai.sln
├── NuGet.config               # NuGet 源配置
├── Directory.Build.props      # 共享 MSBuild 属性
├── Core/Core.csproj           # 类库 (Microsoft.NET.Sdk)
│   └── 依赖：CsvHelper, EntityFramework 6, Newtonsoft.Json,
│             NPOI, SharpZipLib, SQLite 系列, WebSocketSharp 等
├── UI/UI.csproj               # WPF 主程序 → Tai.exe
│   ├── 依赖：Core 项目引用, Microsoft.Extensions.DI,
│   │         Expression.Blend.Sdk, Newtonsoft.Json 等
│   └── SDK: Microsoft.NET.Sdk.WindowsDesktop + UseWPF
├── Updater/Updater.csproj     # WPF 更新器 → Updater.exe
│   ├── 依赖：Newtonsoft.Json
│   └── SDK: Microsoft.NET.Sdk.WindowsDesktop + UseWPF
└── TaiBug/TaiBug.csproj       # WPF 崩溃报告工具 → TaiBug.exe
    ├── 依赖：无外部 NuGet 包
    └── SDK: Microsoft.NET.Sdk.WindowsDesktop + UseWPF
```

## 已知问题排查

### 构建时提示 .NET Framework 引用缺失

如果错误提示找不到 `System.Windows.Forms`、`System.Drawing` 等 Framework 程序集，通常是因为缺少 .NET Framework 4.8 运行时。

```bash
# 检查已安装的 .NET 运行时
dotnet --list-runtimes
```

安装 [.NET Framework 4.8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48) 即可解决。

### NuGet 恢复失败

```bash
# 手动恢复
dotnet restore Tai.sln

# 清除 NuGet 缓存后重试
dotnet nuget locals all --clear
dotnet restore Tai.sln
```

### 构建产物找不到 SQLite.Interop.dll

`Stub.System.Data.SQLite.Core.NetFramework` 包会在构建时自动复制 native interop DLL 到输出目录。

```bash
# 清理后重新构建
dotnet clean Tai.sln
dotnet build Tai.sln
```

## 修改历史

- **2026-06-10**: 将 4 个项目从 `packages.config` + 旧式 .csproj 迁移至 SDK 风格 + `PackageReference`，解决 `dotnet build` 无法稳定构建的问题。
  - 移除 3 个 `packages.config` 文件
  - 移除所有 `HintPath` 引用（由 NuGet 自动管理）
  - 将 `UIAutomationClient` COM 引用替换为直接 Framework 引用（消除 `ResolveComReference` 不兼容问题）
  - 新增 `NuGet.config` 和 `Directory.Build.props`
