# UI 架构契约

本文只记录当前可运行 UI 和未来扩展边界。视觉令牌、颜色、字号、间距和基础焦点样式以 [`DESIGN.md`](../../DESIGN.md) 为准。

## 当前命令中心组成

`Scenes/UI/GameHUD.tscn` 是当前 HUD 组合根，挂在 `Scenes/MapTest.tscn` 的 `GameHUD` 节点下。

```text
GameHUD (CanvasLayer, Scripts/UI/GameHUD.cs)
+-- UIManager (Scripts/UI/UIManager.cs, 每个 GameHUD 自有)
+-- ConstructionDock (Scenes/UI/ConstructionDock.tscn)
|   +-- DockPanel/DockStack/ToolTray
|   |   +-- TrayMargin/ToolScroll/ToolList
|   |       +-- SelectToolButton
|   |       +-- RoadToolButton
|   |       +-- RoadRemoveToolButton
|   +-- DockPanel/DockStack/CategoryBar
|       +-- RoadsCategoryButton
+-- ToolContextPanel (Scripts/UI/ToolContextPanel.cs)
|   +-- PanelMargin/Rows/ContextFocusEntryButton
|   +-- PanelMargin/Rows/ContextContent
+-- SystemControls (Scripts/UI/SystemControls.cs)
|   +-- PanelMargin/Controls/Buttons/SaveButton
|   +-- PanelMargin/Controls/Buttons/LoadButton
+-- DebugPanel (Scripts/UI/DebugPanel.cs)
    +-- PanelMargin/Rows/DebugToggleButton
    +-- PanelMargin/Rows/DebugContent
```

历史迁移说明：旧左上 `Panel/VBox` HUD、旧 `WireButtons()` 硬编码工具栏和 `Scripts/UI/UIHelpers.cs` 已被命令中心组合、catalog 资源和 `Scenes/UI/Themes/CommandCenterTheme.tres` 取代。它们不是当前实现。

## Catalog 和工具

首个 live 阶段只渲染 Roads 分类，且只渲染现有可用工具：`Select`、`Road`、`RoadRemove`。

当前 bundled catalog 是 `Scenes/UI/RoadsConstructionCategory.tres`：

| 工具 ID | `ToolType` | 玩家显示 | 快捷键 | 说明来源 |
|---|---|---|---|---|
| `select` | `Select` | `选择` | `Esc` | catalog `Description` |
| `road` | `Road` | `铺路` | `R` | catalog `Description` |
| `road-remove` | `RoadRemove` | `拆路` | `E` | catalog `Description` |

`ConstructionCategoryDefinition.TryValidate()` 必须拒绝空分类 ID、空显示名、null `Tools`、空工具引用、空工具 ID/显示名和重复工具 ID。`ConstructionDock` 只在 catalog 验证通过后创建按钮；验证失败时隐藏 ToolTray、禁用 Roads 按钮并显示 degraded 文案。

## UIManager 所有权

`UIManager` 不是进程全局单例。每个 `GameHUD` 创建或解析自己的 `UIManager` 子节点，并只向该 manager 注册自己的 `ContextPanel`、`DebugPanel`、`SystemControls`。`ConstructionDock` 始终可见，不注册到 `UIManager`。

`UIManager` 保留面板 API：`Register`、`Unregister`、`Show`、`Hide`、`Toggle`、`IsVisible`、`HideAll`、`PushModal`、`PopModal`、`GetPanel<T>` 和 GDScript 可调用的 `GetPanel(string)`。两个 `GameHUD` 同时存在时，它们的 manager 不得互相覆盖或注销对方的面板。

## 输入和数据流

| 输入或事件 | 所有者 | 当前效果 |
|---|---|---|
| `R` | `ToolManager._Input()` | `CurrentTool = ToolType.Road` |
| `E` | `ToolManager._Input()` | `CurrentTool = ToolType.RoadRemove` |
| `Esc` | `ToolManager._Input()` | `CurrentTool = ToolType.Select` |
| `SelectToolButton` / `RoadToolButton` / `RoadRemoveToolButton` | `ConstructionDock` | 请求 `ToolManager.CurrentTool` 切换 |
| `RoadsCategoryButton` | `ConstructionDock` | 展开或收起 ToolTray，不改变当前工具 |
| `F5` / `SaveButton` | `GameHUD` / `SystemControls` | 调用 `SaveManager.Instance.Save("autosave")` 并更新状态 |
| `F9` / `LoadButton` | `GameHUD` / `SystemControls` | 调用 `SaveManager.Instance.Load("autosave")` 并更新状态 |

`ToolContextPanel` 不维护重复 switch 文案。它从 `ConstructionDock.Category` 的 `ConstructionToolDefinition` 读取当前工具显示名、说明和快捷键；找不到定义时显示安全 fallback。`DebugPanel` 显示 FPS、鼠标格点、RoadGroup、GraphEdge、GraphNode，并在缺失或释放的 `MainCamera` / `RoadGraph` 状态下显示 `--`。

## 响应式和焦点

默认桌面布局：`ConstructionDock` 底部居中并向上展开；`SystemControls` 在右上；`ToolContextPanel` 在右侧；`DebugPanel` 默认折叠在左上。展开后的 dock 不得与右侧 context/system 面板重叠。

低于 760px 宽度时：

| 区域 | 当前规则 |
|---|---|
| `ConstructionDock` | 保持底部，宽度收敛到 `viewport_width - 24px`，ToolTray 仍向上展开 |
| `ToolTray` | 高度不超过视口三分之一，内容通过 `ToolScroll` 滚动 |
| `ToolContextPanel` | 默认折叠为 44px 右侧入口；展开时根据 dock 实际顶部边界限制高度，避免与 dock/system/debug 重叠 |
| `SystemControls` | 保持独立右上系统操作区，不进入 dock |
| `DebugPanel` | 默认折叠，展开内容不进入主焦点路径 |

焦点顺序由代码显式设置，并在 ToolTray 可见性变化时立即更新：

```text
展开：RoadsCategoryButton -> SelectToolButton -> RoadToolButton -> RoadRemoveToolButton -> ContextFocusEntryButton -> SaveButton -> LoadButton -> DebugToggleButton -> RoadsCategoryButton
折叠：RoadsCategoryButton -> ContextFocusEntryButton -> SaveButton -> LoadButton -> DebugToggleButton -> RoadsCategoryButton
```

反向焦点链必须与上述顺序互为镜像；折叠时隐藏工具按钮必须被 forward 和 reverse traversal 同时绕过。

## 新分类上线规则

新 live 分类必须完成全部步骤后才能从未来示例进入 catalog：

1. 定义分类 ID、中文显示名、排序和说明。
2. 定义每个工具 ID、中文显示名、快捷键、`ToolType`、排序和说明。
3. 接入真实行为所有者，不能只有按钮或禁用占位。
4. 准备统一 Theme / 图标 / 文案资源，禁止 emoji 占位。
5. 验证鼠标、键盘、Tab、Enter、Space、Esc 与现有快捷键冲突。
6. 验证保存/加载、资源加载、可访问性和小窗口布局。
7. 更新本文件和相关 class/reference 文档。

未来示例只能留在文档中，不能作为 live 按钮、灰色 tab、空托盘或“即将推出”卡片渲染：Zoning、Public Facilities、Transit、Landscaping、道路等级变体、桥梁、隧道、预算/人口/RCI、速度控制。
