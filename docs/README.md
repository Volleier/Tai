# Tai 项目审查文档

本文档目录用于存放本次对 `Tai` 项目的结构梳理与优化建议。

## 文档清单

1. [项目结构与运行机制](./project-structure.md)
   说明解决方案结构、各项目职责、启动流程、UI 导航、数据流和浏览器扩展协作方式。

2. [优化清单与优先级建议](./optimization-checklist.md)
   按优先级整理待优化项，细化到建议修改的文件、问题影响和建议做法。

## 阅读顺序建议

1. 先阅读 `project-structure.md`
   适合快速建立对项目整体的理解。

2. 再阅读 `optimization-checklist.md`
   适合安排修复顺序、拆分任务和建立迭代计划。

## 本次审查范围

- 解决方案工程结构
- WPF 主程序 `UI`
- 核心逻辑库 `Core`
- 更新器 `Updater`
- 崩溃提示程序 `TaiBug`
- 浏览器扩展 `WebExtensions`

## 说明

- 本次审查时间：`2026-06-10`
- 已确认并修复一处 UI 问题：
  `UI/Themes/Window/DefaultWindow.xaml` 中确认弹窗和输入弹窗在暗色主题下存在白底白字问题，现已补充显式文字颜色。
