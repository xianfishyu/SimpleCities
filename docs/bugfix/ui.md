# UI Bug 修复记录

> 日期：2026-07-30
> 影响文件：`Scripts/UI/ConstructionCategoryDefinition.cs`, `Scripts/UI/ConstructionDock.cs`, `Scripts/UI/ToolContextPanel.cs`, `Scripts/UI/GameHUD.cs`, `Scenes/UI/ConstructionDock.tscn`, `Scenes/UI/RoadsConstructionCategory.tres`, `tests/godot/command_center_runtime_contract.gd`
> 关联事项：5-lane review 阻塞项复查

---

## BUG-1：运行时道路工具托盘只保留 Select

> 历史记录：本节记录早期三工具 Roads catalog 阶段的故障和当时修复。当前命令中心已由后续五分类 / 单 city-road catalog 设计取代；不要把本节中的三按钮描述视为当前实现契约，当前事实以 BUG-6、[`../ui/design-system.md`](../ui/design-system.md) 和 [`../ui/architecture.md`](../ui/architecture.md) 为准。

### 症状

`Scenes/UI/RoadsConstructionCategory.tres` 声明了 Select / Road / RoadRemove 三个工具，但 fresh `MapTest` 运行时最终只观察到一个 live 工具按钮。该问题会让玩家无法通过命令中心托盘选择铺路或拆路工具。

### 根因分析

道路 catalog 原先通过普通 C# 数组暴露给 Godot 资源系统，文件文本看起来包含三个 subresource，但运行时 C# 侧迭代路径和 Godot 资源数组不一致，实际按钮构建没有以 Godot 运行时资源数组作为唯一事实来源。旧测试主要检查 `.tscn` / `.tres` 字符串，没有实例化 actual scene 并断言 live 按钮节点。

### 修复方案

当时的修复是将 `ConstructionCategoryDefinition.Tools` 改为 Godot 资源数组形态并在 `TryValidate()` 中 fail closed；`ConstructionDock.BuildToolButtons()` 只在 category 通过验证后按 catalog 的 `SortOrder` 创建 live 按钮。当时新增的 Godot headless integration test 启动 actual `MapTest` / `GameHUD` / `ConstructionDock`，断言当时存在的 live `SelectToolButton`、`RoadToolButton`、`RoadRemoveToolButton` 三个按钮和 `ToolScroll` 路径。当前实现已 superseded：Roads 托盘只保留一个 `RoadToolButton` / “城市道路 R”，Select / RoadRemove 为键盘-only 文案。

### 影响范围

影响 UI catalog 读取、道路建造坞按钮生成和上下文显示。未修改 `ToolType`、`ToolManager`、`RoadBuilder`、`RoadGraph`、`SaveManager` 业务逻辑。

---

## BUG-2： malformed catalog / missing dependency 可能导致 UI 启动崩溃或误导状态

### 症状

当 catalog `Tools` 为空、包含空工具引用，或 HUD 在缺少 `ToolManager` / `RoadSystem` / `SaveManager` 的隔离场景中加载时，UI 可能空引用、继续 dereference 无效资源，或者不能给出清晰 degraded state。

### 根因分析

`TryValidate()` 之前假设 `Tools` 永不为 null；`ConstructionDock` 在 invalid category 后仍缺少完整初始化短路；`GameHUD` 直接读取单例而未先确认实例仍有效。Godot 生命周期中静态单例引用也可能指向已释放对象，调试面板读取 `MainCamera.Instance` 时同样需要确认实例有效。

### 修复方案

`ConstructionCategoryDefinition.TryValidate()` 显式拒绝 null `Tools`、空工具引用和重复 ID；`ConstructionDock` 在 invalid category 时禁用分类按钮、隐藏托盘并不创建工具按钮；`GameHUD` / `ConstructionDock` / `DebugPanel` 使用 `GodotObject.IsInstanceValid(...)` 区分可用单例和 degraded state，Save/Load 在缺少 SaveManager 时更新 UI 状态并记录 warning。

### 影响范围

影响 UI 启动和只读状态反馈。没有改变 Road/ToolManager/SaveManager 的业务规则；bounded bundled catalog 仍是当前安全假设，没有添加任意数量上限。

---

## BUG-3：小窗口展开上下文和折叠焦点链会遮挡或指向隐藏按钮

> 历史记录：本节中的焦点链示例属于早期三工具托盘阶段。当前五分类焦点链已由 BUG-6 更新为 `Roads -> Zoning -> Facilities -> Transit -> Landscaping -> RoadToolButton? -> Context`，折叠反向焦点从 Context 回到 Landscaping。

### 症状

640x480 下 `ToolContextPanel` 从 44px compact 入口展开后可能与底部 expanded `ConstructionDock` 或右上 `SystemControls` 重叠；旧测试只用 offset 推导矩形，实际 `Control.get_global_rect()` 曾出现 compact context `x=580, width=132` 并向右溢出。ToolTray 折叠后，反向焦点链仍可能从 context 指回隐藏工具按钮。

### 根因分析

`ToolContextPanel` 的 compact-expanded 高度只看视口高度，没有使用 dock 的实际顶部边界作为底部保留区；compact 状态还保留了 `MarginContainer`、内容 VBox 和面板 style 的最小尺寸贡献，导致实际控件宽度不等于 44。焦点链只在初始配置时写入，ToolTray 收起时 context 的 `FocusPrevious` 没有同步改回 `RoadsCategoryButton`。

### 修复方案

`GameHUD` 将 `ConstructionDock.Position.Y` 传给 `ToolContextPanel.ApplyResponsiveLayoutForViewport(...)`，让 compact-expanded context 按 dock 实际顶部限制底边。`ContextContent` 外包 `ContextContentScroll`，compact 时隐藏 scroll/content、清零 margin 并覆盖 panel style，保证实际 global rect 为 44px；展开时用 ScrollContainer 承载内容最小高度，底部内容通过滚动可达。当时 `ConstructionDock` 发出 `TrayVisibilityChanged`，`GameHUD` 立即重建三工具阶段焦点链；当前五分类阶段的焦点链已由 BUG-6 supersede。

### 影响范围

影响 HUD 小窗口布局和键盘焦点可达性。未改变道路工具业务行为、输入快捷键含义或保存/加载逻辑。

---

## BUG-4：多个 GameHUD 共存时 UIManager 注册会串线

### 症状

两个 `GameHUD` 同时实例化时，旧 `UIManager.Instance` 会被后创建的 manager 覆盖。第二个 HUD 释放或注销面板时，可能影响第一个 HUD 的 panel registry，使第一个 HUD 不能再解析自己的 `ContextPanel`、`DebugPanel`、`SystemControls`。

### 根因分析

`UIManager` 是 process-global static singleton，但它管理的是 HUD-local Control 节点。`GameHUD.RegisterManagedPanels()` 和 `_ExitTree()` 通过静态 `UIManager.Instance` 注册/注销，导致多个 HUD 的生命周期共享同一可变 registry。

### 修复方案

移除 `UIManager.Instance` 依赖。每个 `GameHUD` 创建或解析自己的 `UIManager` 子节点，保存为 `_uiManager` 引用，并只对该 manager 调用 `Register` / `Unregister` / `GetPanel`。保留原有面板管理 API，并新增 GDScript 可调用的 `GetPanel(string)` 用于运行时隔离回归测试。

### 影响范围

影响 HUD 面板生命周期管理。未改变 `UIManager` 的显示、隐藏、模态栈语义；只是把 ownership 从进程全局改为每个 HUD 实例本地。

---

## BUG-5：命令中心主题没有可验证的中文字体回退

### 症状

命令中心使用中文 catalog 文案，但 `CommandCenterTheme.tres` 没有 `default_font`，也没有 bundled font。不同系统环境下可能退回到不含 CJK 字形的默认字体，导致中文按钮和上下文文本缺字。

### 根因分析

主题只声明颜色、字号和控件 style，没有声明 Godot-native `Font` 资源。项目也没有二进制字体资产，因此必须通过 `SystemFont` 和显式系统字体候选列表约束运行时字体选择。

### 修复方案

在 `Scenes/UI/Themes/CommandCenterTheme.tres` 添加 `SystemFont` subresource，并设置 `default_font`。候选顺序覆盖 Windows、Linux、macOS 常见中文字体：`Microsoft YaHei UI`、`Noto Sans CJK SC`、`PingFang SC`、`Source Han Sans SC`、`sans-serif`，并启用 system fallback。运行时契约断言主题存在 default font 且 `font.has_char("道".unicode_at(0))` 为 true。

### 影响范围

影响命令中心主题的默认字体解析。没有添加或下载字体文件、外部依赖、项目设置或二进制资源。

---

## BUG-6：建造坞分类、焦点、键盘工具文案与实际面板几何回归

### 症状

post-implementation review 发现 `ConstructionDock` 的四类 UI 合约回归：重复点击当前分类只会保持 ToolTray 打开，不能折叠/重开；折叠状态下 ContextPanel 反向焦点仍可能回到 Roads 或隐藏工具，而不是五分类正向链的镜像末端“景观”；Esc / E 对应的 Select / RoadRemove 虽然不应进入 Roads 子菜单，却缺少玩家可读中文 dock/context 文案；桌面和 640x480 下实际 themed `DockPanel` 会因为共享 Theme 的 20px panel margin 与 `DockStack` 8px separation 超出 64px 折叠 / `64 + trayHeight` 展开根几何。

新增回归断言的首次 RED 为：`godot --headless --path . --script tests/godot/command_center_runtime_contract.gd` 报告 `default roads expanded DockPanel top escapes root`、`default roads expanded DockPanel bottom escapes root` 和 `default roads expanded actual DockPanel bottom outside viewport`。

### 根因分析

`ConstructionDock.OnCategoryPressed()` 每次分类点击都写入 `_activeCategoryId`、重渲染菜单并 `SetTrayVisible(true)`，无法区分“重复当前分类”与“切换不同分类”。`GameHUD.ConfigureFocusChain()` 在折叠状态将 context previous 固定为 Roads 按钮，没有使用 dock 侧已知道的最后一个分类焦点控件。Select / RoadRemove 不再是 catalog 条目后，`ToolContextPanel.UpdateContext()` 和 dock 当前工具标签只剩 enum fallback。几何上，`ConstructionDock.ApplyDockLayout()` 计算的是 64px / `64 + trayHeight` 根尺寸，但实例化的 `DockPanel` 使用共享 `CommandCenterTheme.tres` 20px margin 和 `DockStack` 8px separation，实际 content minimum size 会把 panel 推出根节点。

### 修复方案

`OnCategoryPressed()` 现在仅在点击同一个 active category 时切换 ToolTray 可见性并避免重建内容；点击不同分类时重渲染目标分类内容并保持/打开托盘。`GameHUD.ConfigureFocusChain()` 统一从 `ConstructionDock.GetLastDockFocusControl()` 推导 context previous，因此折叠态会指向 `LandscapingCategoryButton`，展开 Roads 时指向 `RoadToolButton`。`ConstructionDock` 增加只读内建玩家文案表，供 dock 当前标签和 `ToolContextPanel` 对 Select / RoadRemove fallback 使用，不新增 catalog resource 或 submenu button。`Scenes/UI/ConstructionDock.tscn` 只为本地 `DockPanel` 覆盖 command-center 风格 `StyleBoxFlat`，content margin 为水平 12px / 垂直 8px，并将 `DockStack` separation 设为 0；共享 theme 未放宽。

### 影响范围

影响命令中心建造坞交互、键盘焦点、上下文显示和 dock scene 几何。未修改 `ToolType`、`ToolManager`、`RoadBuilder`、`RoadGraph`，也未新增 Select / RoadRemove submenu 项或未来系统资源。更早的五分类整合已经将 `RoadsConstructionCategory.tres` 从三工具 catalog 收敛为一个 `city-road` 条目；本 BUG-6 窄修阶段没有进一步改变该资源行为。

### 后续修正

最终复查又发现一个状态组合：未来分类已激活时，按 E / Esc 会切换 `ToolManager.CurrentTool`，但 `GameHUD._Process()` 仍把 `ToolContextPanel` 刷成内建拆路/选择文案，覆盖了未来分类的“尚未开放”上下文。修正为只在 `ConstructionDock.UsesCatalogContext` 为 true 时按当前工具刷新 catalog/built-in 上下文；未来分类上下文完全由 `ConstructionDock.ContextDisplayChanged` 的 unavailable-category 通知维持。因此未来分类下 E / Esc 只改变工具状态，不改变右侧 active category、`尚未开放`、隐藏 shortcut 和隐藏 cell-size 的玩家上下文。

---

## 验证状态

- `csharp-ls --solution SimpleCities.sln --diagnose`：solution loaded，未报告 diagnostics。
- `dotnet test SimpleCities.sln`：17 passed，0 failed，0 skipped。
- `dotnet build SimpleCities.sln`：0 warnings，0 errors。
- 历史 BUG-1 `godot --headless --path . --script tests/godot/roads_construction_category_contract.gd`：当时 PASS，验证三工具 catalog 和 malformed resource validation；当前已被 BUG-6 的 one-city-road catalog 契约 supersede。
- 历史 BUG-1/BUG-3 `godot --headless --path . --script tests/godot/command_center_runtime_contract.gd`：当时 PASS，验证三 live tool button、tool sync、focus traversal、context catalog sync、640x480 actual `Control.get_global_rect()` bounds、context scroll path/reachability、CJK theme glyph、malformed dock 和 missing ToolManager/RoadSystem degraded state；当前 live button 数量和焦点链已被 BUG-6 supersede。
- 历史 Fresh editor/runtime QA：当时 `MapTest` 运行时观察到三个 live 工具按钮、中文 catalog 文案、默认 debug collapsed、Save/Load F5/F9 状态、默认 viewport bounds 不重叠；当前不再声明三个 live 工具按钮，当前事实见 BUG-6。
- BUG-6 `csharp-ls --solution SimpleCities.sln --diagnose`：solution loaded，未报告 diagnostics。
- BUG-6 `dotnet test tests\SimpleCities.RoadGraph.Tests\SimpleCities.RoadGraph.Tests.csproj --filter ConstructionDockContractTests`：3 passed，0 failed，0 skipped。
- BUG-6 `dotnet test tests\SimpleCities.RoadGraph.Tests\SimpleCities.RoadGraph.Tests.csproj --filter "ConstructionDockContractTests|ConstructionCategoryDefinitionTests"`：11 passed，0 failed，0 skipped。
- BUG-6 `dotnet test SimpleCities.sln`：18 passed，0 failed，0 skipped。
- BUG-6 `dotnet build SimpleCities.sln`：0 warnings，0 errors。
- BUG-6 `godot-minimal_get_diagnostics` 针对 `tests/godot/command_center_runtime_contract.gd` 与 `tests/godot/roads_construction_category_contract.gd`：均无 diagnostics；workspace GDScript scan 扫描到 0 个 GDScript 文件、0 issues。
- BUG-6 `godot --headless --path . --script tests/godot/roads_construction_category_contract.gd`：PASS，验证 Roads catalog 仍只有 `city-road` 映射 `ToolType.Road`，Select / RoadRemove 不进入 catalog。
- BUG-6 `godot --headless --path . --script tests/godot/command_center_runtime_contract.gd`：PASS，覆盖重复 active category 折叠/重开、不同分类切换保持打开、未来分类 E/Esc 只改变 `ToolManager` 而 context 保持 active category + `尚未开放` 且 shortcut/cell-size 隐藏、折叠反向 focus `ContextFocusEntryButton -> LandscapingCategoryButton`、展开反向 focus `ContextFocusEntryButton -> RoadToolButton`、真实 `ui_focus_prev`、Roads 下 Esc/E 中文 dock/context 文案且无 submenu 按钮、桌面和 640x480 actual `DockPanel` containment。隔离 malformed/missing dependency 场景仍输出预期 degraded warning。
- BUG-6 continuation RED `godot --headless --path . --script tests/godot/command_center_runtime_contract.gd`：新增未来分类 E/Esc 断言后，修复前失败于 `ZoningCategoryButton after E context category changed`、`context tool should remain unavailable`、`context operation should remain unavailable`、`context shortcut should stay hidden`、`context cell size should stay hidden`。
- BUG-6 continuation GREEN `godot --headless --path . --script tests/godot/command_center_runtime_contract.gd`：PASS，确认区域 / 公共设施 / 交通 / 景观激活时，E 将 `ToolManager.CurrentTool` 切到 `RoadRemove`、Esc 切回 `Select`，但 context 仍保持对应分类名、`尚未开放`，并保持 shortcut/cell-size 隐藏；同时验证展开态 `ui_focus_prev` 从 `ContextFocusEntryButton` 回到 `RoadToolButton`。
- BUG-6 continuation deterministic `MapTest` runtime：区域、公共设施、交通、景观四个 future category 下，E 后 `tool=2`、Esc 后 `tool=0`；每次 context 分别保持该分类名、`current=尚未开放`、`operation=尚未开放`、`shortcut_visible=false`、`cell_visible=false`。Roads 展开时 `ContextFocusEntryButton.focus_previous` 等于 live `RoadToolButton` 路径。
- BUG-6 Godot editor：打开并 reload `res://Scenes/UI/ConstructionDock.tscn` 后 `DockPanel.theme_override_styles/panel = res://Scenes/UI/ConstructionDock.tscn::StyleBoxFlat_dock_panel`，`DockStack.theme_override_constants/separation = 0`，editor log 无新消息。
- BUG-6 deterministic `MapTest` runtime：默认运行中 `viewport=(1066,600)`，expanded root/panel `[P: (233.0, 360.0), S: (600.0, 224.0)]`，collapsed panel `[P: (233.0, 520.0), S: (600.0, 64.0)]`；repeat click collapsed 为 true，switch Zoning kept tray visible 且 placeholders=2，E 显示 `当前: 拆路` / `拆路` / `点击已有道路进行拆除。`，Esc 显示 `当前: 选择` / `选择` / `查看当前状态，取消建造操作。`。
- BUG-6 640x480 runtime subviewport：collapsed root/panel `[P: (12.0, 400.0), S: (616.0, 64.0)]`，expanded root/panel `[P: (12.0, 280.0), S: (616.0, 184.0)]`，context compact rect `[P: (580.0, 148.0), S: (44.0, 44.0)]`，均在视口内；临时 runtime viewport 已 queue_free 并确认 exec holder 为空。
- BUG-6 editor/runtime log：Godot editor log 无新消息；minimal DAP console unavailable because the MCP reported no active debug session for its console channel after the scenario, so runtime errors were checked through headless output and editor log instead。

---

## BUG-7：R/E 仍会切换道路工具且空快捷键行可见

### 症状

输入规则要求只有 Esc 返回 Select，但 `ToolManager._Input()` 仍将原始 R/E 分别切到 Road/RoadRemove；Roads catalog 和 RoadRemove 内建文案也继续暴露 R/E 提示，导致“城市道路 R”和空提示迁移后的快捷键行不符合当前交互契约。

新增回归契约后的 RED 结果为：focused C# tests 3 failed / 1 passed，分别命中 `case Key.R:`、`ShortcutHint = "R"` 和 RoadRemove 的 `"E"` 内建提示；roads catalog headless contract 报告 `Unexpected display data for tool city-road`；command-center runtime contract 报告 `Road label should not include a removed shortcut hint`。

### 根因分析

`ToolManager._Input()` 的按键 switch 仍显式处理 `Key.R` 和 `Key.E`。同时 `Scenes/UI/RoadsConstructionCategory.tres` 与 `ConstructionDock.BuiltInToolPresentations` 各自保存 R/E 字符串；`ToolContextPanel.UpdateContext()` 在进入正常工具上下文时无条件显示 `ShortcutRow`，没有根据最终解析出的 hint 决定行可见性。

### 修复方案

仅移除 `ToolManager._Input()` 的 R/E switch case，保留 Escape 的 Select 行为和按 `CurrentTool` 转发到 `RoadBuilder.HandlePlaceInput()` / `HandleRemoveInput()` 的路径。Road catalog 与 RoadRemove 内建 hint 改为空字符串；`ToolContextPanel` 对 catalog 和 built-in 两条解析路径都按 `string.IsNullOrWhiteSpace()` 隐藏空 hint 的整行。Road 仍由 `RoadToolButton` 选择，RoadRemove 仍可通过程序设置 `CurrentTool` 进入。

### 影响范围

影响工具键盘入口和命令中心快捷键展示。未移除 `ToolType.RoadRemove`，未改变 `RoadBuilder`、`RoadGraph`、拆路行为、项目窗口设置或未来分类 unavailable 上下文，也未新增 RoadRemove 按钮。

### 验证状态

- Focused RED：C# 3 failed / 1 passed；catalog headless contract 失败于 `Unexpected display data for tool city-road`；runtime headless contract 失败于 `Road label should not include a removed shortcut hint`。
- Focused GREEN：C# 4 passed / 0 failed；`godot --headless --path . --script tests/godot/roads_construction_category_contract.gd` 与 `godot --headless --path . --script tests/godot/command_center_runtime_contract.gd` 均 PASS。
- `dotnet test SimpleCities.sln --no-restore`：19 passed，0 failed，0 skipped。
- `dotnet build SimpleCities.sln`：0 warnings，0 errors，与修改前 baseline 一致。
- `godot-minimal_get_diagnostics`：两个变更的 GDScript contract 均无 diagnostics；workspace scan 扫描到 0 个 GDScript 文件、0 issues。`csharp-ls` MCP 连接关闭，focused C# LSP diagnostics 被阻塞，未用 build 冒充 LSP 结果。
- Deterministic `MapTest` raw-input QA：Select 下 raw R/E 后仍为 0；通过 `RoadToolButton` 进入 Road 后 raw R/E 仍为 1；程序设置 RoadRemove 后 raw R/E 仍为 2；三种初始工具下 raw Esc 最终均为 Select。Road 按钮文本为“城市道路”，Road/RoadRemove shortcut row 隐藏，Select 显示 Esc。
- Future-category QA：区域激活后注入 raw R/E/Esc，context 保持“区域 / 尚未开放”，shortcut/cell-size row 均隐藏。
- Editor/runtime log：从测试前 editor cursor 4 起无新 error；清空后的 DAP stderr 和 console 均为空。测试结束后停止运行项目并清理 runtime holder。

---

## BUG-8：同一 HUD 中 Debug 折叠后保留展开高度并遮挡建造坞

### 症状

同一个 `GameHUD` 实例按固定顺序复现：1600x900 下展开 Debug，折叠 Debug，展开 Roads 建造托盘，再把同一 viewport resize 到 640x480。修复前两帧和四帧后的几何都保持同一个错误状态：折叠后的 Debug combined minimum 已是 92，但外层 rect 仍是 `(324,204,300,209)`；expanded `ConstructionDock` rect 为 `(0,358,640,122)`，Debug 底边到 y=413，直接覆盖 dock 顶部。

Focused RED 来自 `tests/godot/command_center_runtime_contract.gd` 中同实例回归，失败信息记录为 `Debug outer height did not contract to collapsed combined minimum` 和 `ConstructionDock overlaps DebugPanel`；对应 RED、cause toggle 与 GREEN 数值已汇总在本节验证状态中。

### 根因分析

`DebugPanel` 展开后外层 assigned height 变为 209。折叠只把 `GetCombinedMinimumSize().Y` 降到 92，没有主动缩小已经分配给外层 panel 的 `Size.Y`。随后 `GameHUD.PlaceTopRightPanels()` 通过 `EffectiveSize(_debugPanel)` 取尺寸，而 `EffectiveSize()` 对高度使用 `Mathf.Max(panel.Size.Y, minimumSize.Y)`，所以折叠后的 Debug 继续按 209 高度参与布局。

640x480 下，这个 stale height 让 Debug 无法进入右上 stack 的正常空隙分支，fallback 把它放到 `(324,204)` 且仍保留 `(300,209)`。等待四帧也不会改变 assigned height，因此这不是 container 延迟 settling，而是 Debug 折叠最小尺寸变化没有触发同一实例重新收缩的布局状态错误。

### 修复方案

最小修复位于 `Scripts/UI/GameHUD.cs`。`PlaceTopRightPanels()` 保留 Debug 既有宽度来源，但对 `_debugPanel` 单独把 desired height 改为 `_debugPanel.GetCombinedMinimumSize().Y`，并在分支选择前把该 corrected size 写回 Debug。这样折叠后的 Debug 会按 92 高度参与同一次布局，展开宽度和其它面板尺寸算法不变。

同一文件还把 `_debugPanel` 的 `Control.SignalName.MinimumSizeChanged` idempotent connect 到已有 `OnPanelResized()` deferred responsive refresh，并在 `_ExitTree()` / `DisconnectLayoutSignals()` 中 idempotent disconnect。该连接只覆盖 Debug 最小尺寸变化复用现有刷新路径，没有改 `EffectiveSize()` 的全局 max 行为，也没有改 `SystemControls`、`ToolContextPanel`、`DebugPanel` 视觉实现、scene、theme 或项目设置。

### 影响范围

影响 `GameHUD` 对右侧 Debug panel 与底部 `ConstructionDock` 的 responsive 几何。修复边界限制在 HUD 布局调度和 Debug desired height，未改变 `DebugPanel` 的展开内容、文案、尺寸资源、`SystemControls`、`ToolContextPanel`、命令中心 catalog、道路工具逻辑、存档或 RoadGraph 行为。

### 验证状态

- BUG-8 RED：同一个 HUD 实例执行 Debug expand -> collapse、tray expanded、1600x900 -> 640x480 resize 后，两帧和四帧均复现 stale Debug rect `(324,204,300,209)`、collapsed combined minimum 92、dock rect `(0,358,640,122)`，并报告 Debug 与 `ConstructionDock` overlap。
- BUG-8 cause toggle：修复应用后 GREEN；仅移除 causal fix lines 后，重建仍为 0 warnings、0 errors，原始 209 vs 92 stale rect 和 overlap 在两帧、四帧恢复；重新套回 exact fix patch 后再次 GREEN。每个状态之间都重新 build，排除 cached DLL 成功。
- BUG-8 `csharp-ls --solution SimpleCities.sln --diagnose --loglevel error`：solution loaded successfully，未报告 diagnostics。
- BUG-8 `dotnet build SimpleCities.sln`：exit 0，0 warnings，0 errors。
- BUG-8 Godot minimal diagnostics 针对 `tests/godot/command_center_runtime_contract.gd`：无 diagnostics。
- BUG-8 `godot.exe --headless --path . --script res://tests/godot/roads_construction_category_contract.gd`：PASS roads construction category contract。
- BUG-8 `godot.exe --headless --path . --script res://tests/godot/command_center_runtime_contract.gd`：PASS command center runtime contract；最终 serial run clean。该回归在同一 HUD 实例断言两帧和四帧 checkpoint。
- BUG-8 live geometry：1600x900 下 `ConstructionDock`、`DebugPanel`、`SystemControls`、`ToolContextPanel` 均 in bounds 且 non-overlap；同实例状态化 resize 到 640x480 后，两帧时 Debug rect 为 `(280,141,300,92)`，dock rect 为 `(0,358,640,122)`，`in_bounds=true` 且 `non_overlap=true`；四帧保持相同正确几何。
- BUG-8 `git diff --check -- Scripts/UI/GameHUD.cs tests/godot/command_center_runtime_contract.gd`：clean。
- BUG-8 限制：Todo 13 full live matrix 仍属于后续恢复执行者，未在本记录中声明完成；`godot-minimal` DAP console 当时返回 `No active debug session`，归类为工具集成限制，不能作为 clean runtime console 证据，也不声明 DAP 通过。

---

## BUG-9：ConstructionDock 分类按钮重复文字且 DebugPanel 被移动到右侧

### 症状

`MapTest` HUD 底部分类按钮同时使用 Godot `Button.Text` 和 `ConstructionDockButton.DisplayText`，导致原生静态文字与自定义 `Presentation/Label` 同时参与显示。用户可见结果是每个分类图标下方出现重复中文文字行，而设计只允许 icon-over-label 的自定义行。

同一 HUD 中，`Scenes/UI/GameHUD.tscn` 把 `DebugPanel` 设计在左上 `(16,16)`，但 `GameHUD.PlaceTopRightPanels()` 把 `_debugPanel` 作为右侧避让面板重新定位。1600x900 与 640x480 的折叠/展开状态都不应把 Debug 移到右侧；`SystemControls` 和 `ToolContextPanel` 才属于右侧布局。

### 根因分析

`Scripts/UI/ConstructionDock.cs` 在 `BuildCategoryBar()` 中执行 `button.Text = category.DisplayName`，随后又设置 `dockButton.DisplayText = category.DisplayName`。动态生成的道路工具和未来分类 placeholder 也给 `ConstructionDockButton` 赋过原生 `Text`，所以只改场景序列化无法覆盖所有路径。

`Scripts/UI/GameHUD.cs` 的 `PlaceTopRightPanels()` 先放置 `SystemControls`，再根据 `ToolContextPanel` 和 dock 空间把 `_debugPanel` 放到右侧、相邻或 context 下方。该算法与 scene 中 Debug 左上位置和当前设计文档的 top-left 事实冲突。

### 修复方案

`ConstructionDockButton` 在 `_Ready()` 和 `SynchronizePresentation()` 中强制 `Text = string.Empty`，把 native text invariant 收到可复用按钮自身；`ConstructionDock` 同时停止为分类、placeholder 和 road tool 填充原生 `Text`，只保留 `DisplayText`、tooltip、focus、disabled、pressed/selected 状态。

`GameHUD` 保留右侧 `SystemControls` / `ToolContextPanel` 布局，但新增 top-left Debug 放置路径：`DebugPanel` 始终回到 `new Vector2(PanelMargin, PanelMargin)`，高度仍取 `GetCombinedMinimumSize().Y` 以保留 BUG-8 的折叠高度修复；640x480 下只约束 Debug 可用宽度，并在右侧 `SystemControls` 会与 Debug 相交时把 SystemControls 右对齐放到 Debug 下方，不把 Debug 移右。`docs/ui/design-system.md` 仅把 Debug 描述从 right-side placement 校正为 top-left placement。

### 影响范围

影响 HUD 分类按钮文字渲染和 `GameHUD` 响应式几何。未改变 `DebugPanel.cs`、scene tree、主题、图标资源、`ToolContextPanel` 内容行为、`SystemControls` 功能、Construction catalog、道路工具逻辑、存档或 RoadGraph。

### 验证状态

- BUG-9 baseline：源代码确认 `ConstructionDock.cs` 同时存在 `button.Text = category.DisplayName` 与 `dockButton.DisplayText = category.DisplayName`；`GameHUD.tscn` Debug 初始 offset 为 `(16,16)`，`GameHUD.PlaceTopRightPanels()` 负责移动 `_debugPanel`。
- BUG-9 focused RED：新增 xUnit 后，`ConstructionDockScript_KeepsNativeButtonTextEmptyForCustomDockButtons` 失败于 `button.Text = category.DisplayName`；`GameHUDScript_PreservesDebugPanelTopLeftOutsideRightSidePlacement` 失败于缺少 `PlaceTopLeftDebugPanel`。
- BUG-9 focused GREEN：`dotnet test tests\\SimpleCities.RoadGraph.Tests\\SimpleCities.RoadGraph.Tests.csproj --filter "FullyQualifiedName=SimpleCities.Tests.ConstructionDockContractTests.ConstructionDockScript_KeepsNativeButtonTextEmptyForCustomDockButtons|FullyQualifiedName=SimpleCities.Tests.GameHUDCompositionContractTests.GameHUDScene_KeepsDebugPanelAtDesignedTopLeftMargin|FullyQualifiedName=SimpleCities.Tests.GameHUDCompositionContractTests.GameHUDScript_PreservesDebugPanelTopLeftOutsideRightSidePlacement"`：3 passed，0 failed。
- BUG-9 focused class note：包含整个 `ConstructionDockContractTests|GameHUDCompositionContractTests` 的过滤运行中，本次新增断言已通过，但当前 dirty workspace 仍有两个既有 contract failure：`ConstructionDockButtonScene_DefaultsToEnabledAndFocusable` 缺少 serialized `focus_mode = 2`，`RoadsCatalog_ContainsOnlyCityRoadMappedToRoadTool` 缺少 serialized `ShortcutHint = ""`；未在本修复中处理。
- BUG-9 `dotnet build SimpleCities.sln`：exit 0，0 warnings，0 errors。
- BUG-9 GDScript diagnostics：`godot-minimal_get_diagnostics` 针对 `tests/godot/command_center_runtime_contract.gd` 无 diagnostics；C# LSP diagnostics gate 因当前会话 `lsp_diagnostics` 连接关闭/Not connected 被阻塞，未用 build 冒充 LSP 通过。
- BUG-9 headless contracts：`godot --headless --path . --script tests/godot/roads_construction_category_contract.gd` PASS；`godot --headless --path . --script tests/godot/command_center_runtime_contract.gd` PASS（输出的 missing dependency / malformed dock warnings 为该 contract 的预期 degraded-state 场景）。
- BUG-9 live `MapTest` runtime inspection：1600x900 collapsed 分类按钮 native `text` 均为空，child label 分别为 `道路/区域/公共设施/交通/景观`；Debug rect collapsed `(16,16,300,92)`，expanded with dock open `(16,16,300,209)`，dock expanded `(0,778,1600,122)`。临时 640x480 `SubViewport` MapTest collapsed Debug rect `(16,16,276,92)`、SystemControls `(304,16,320,113)`、dock `(0,404,640,76)`；expanded Debug rect `(16,16,276,209)`、dock `(0,358,640,122)`，保持 non-overlap 且 Debug 未移右。
- BUG-9 editor/runtime logs：运行前清空 DAP console；实时场景后 `godot-minimal` stderr 与 console 均为空，Godot editor log 无消息。截图捕获因当前模型会话不支持读取工具返回图片而阻塞，未作为数值或通过证据。运行项目已停止；临时 SubViewport 随运行停止清理。

---

## BUG-10：ConstructionDock 调色板从历史命令中心琥珀回归为青色

### 症状

当前全宽 `ConstructionDock` 保留了正确布局、图标、标签和交互，但 `Scenes/UI/Themes/ConstructionDockTheme.tres` 使用 blue-black / cyan 本地调色板。用户期望恢复 887fe37 之前共享 `CommandCenterTheme.tres` 的深中性表面与琥珀状态色，同时保留当前全宽 13px 几何和本地 Theme 隔离。

### 根因分析

887fe37 引入 `ConstructionDockTheme.tres` 时把 dock 从共享命令中心主题拆出，并把本地 token 改成 `#07111A`、`#0B1A25`、`#61D8EE`、`#9DEBFA` 等 cyan contract。`docs/ui/design-system.md` 同步把该差异描述为“唯一 cyan exception”，运行时 contract 只验证状态结构不同，缺少对实际 Theme 色值的精确断言。

### 修复方案

仅替换 `Scenes/UI/Themes/ConstructionDockTheme.tres` 中 palette 相关 theme color 和 `StyleBoxFlat` 色值：外层 dock/panel 恢复 `#0F1217` alpha 0.92，asset strip 恢复 `#151A22` alpha 0.96，divider 恢复 `#242933` alpha 0.50，默认文字/图标、disabled、hover、pressed/selected、focus 与背景状态恢复历史命令中心中性/琥珀值。`tests/godot/command_center_runtime_contract.gd` 新增 exact effective Theme color assertions；`docs/ui/design-system.md` 改为说明 ConstructionDock 使用历史 command-center neutral/amber palette，但仍保留本地 Theme resource 做作用域隔离。

### 影响范围

影响 ConstructionDock 本地视觉 palette 与对应文档/运行时契约。未修改 `CommandCenterTheme.tres`、scene layout、图标、按钮文字、focus 链、Debug 位置、输入行为、`project.godot`、道路工具或存档逻辑。

### 验证状态

- BUG-10 stale cyan search：`#07111A`、`#0B1A25`、`#1B3342`、`#61D8EE`、`#9DEBFA`、`#617884`、`#0E202B` 及对应 `.tres` normalized cyan values 在 live dock contract 文件中无匹配。
- BUG-10 `godot-minimal_get_diagnostics`：`tests/godot/command_center_runtime_contract.gd` 与 `tests/godot/roads_construction_category_contract.gd` 均无 diagnostics。
- BUG-10 `dotnet build SimpleCities.sln`：exit 0，0 warnings，0 errors。
- BUG-10 `dotnet test SimpleCities.sln`：27 passed，0 failed，0 skipped；首次并行运行曾因测试和 build 同时写 `.godot/mono/temp/obj/Debug/SimpleCities.dll` 触发文件锁，串行重跑通过。
- BUG-10 `godot --headless --path . --script tests/godot/command_center_runtime_contract.gd`：PASS，包含新增 exact Theme palette assertions 和既有布局/交互/focus/state contract；输出的 missing dependency / malformed dock warnings 是该 contract 的预期 degraded-state 场景。
- BUG-10 `godot --headless --path . --script tests/godot/roads_construction_category_contract.gd`：PASS。
- BUG-10 live frozen `MapTest` runtime：`ConstructionDock.theme.resource_path = res://Scenes/UI/Themes/ConstructionDockTheme.tres`；effective colors 为 base `(0.0588,0.0706,0.0902,0.92)`、asset strip `(0.0824,0.102,0.1333,0.96)`、primary `(0.949,0.9569,0.9686,1)`、hover `(1,0.8235,0.4784,1)`、selected `(1,0.7608,0.349,1)`、disabled `(0.3451,0.3765,0.4196,1)`、focus border `(1,0.8784,0.5412,1)`；selected/default/disabled styleboxes remained structurally distinct。
- BUG-10 editor/runtime logs：scene reload 后 editor error log 无 task-caused errors；focused runtime `godot-minimal` stderr 为空；exec holder cleanup removed 0 temporary nodes and the test run was stopped。

---

## BUG-11：窄窗口中 ConstructionDock 分类按钮被裁剪

### 症状

当窗口宽度缩小到截图所示的约 435px 时，五个固定 104px 宽的分类按钮连同间距总宽超过底栏。`HBoxContainer` 居中后两端内容离开可视区域，分类栏看起来错位并出现文字、图标裁剪。

### 根因分析

`ConstructionDock` 只为分类按钮指定了固定 `DockButtonWidth = 104f`，但没有根据 dock 实际宽度重新分配按钮宽度。分类栏自身没有足够的窄屏几何约束，因此其最小内容宽度超过父级。

### 修复方案

分类栏放入仅负责裁剪边界的 `CategoryScroll`，并禁用横向滚动条以免其占用底栏高度。`ConstructionDock.ApplyDockLayout()` 根据实际 `Size.X` 把五个按钮宽度从 104px 按可用空间等比收缩，最低保持 72px；宽度向下取整，避免五个按钮的像素取整总和反向撑宽父容器。分类栏仍保留原有 76px 高度和宽窗口的居中布局。

### 影响范围

影响 `ConstructionDock` 的分类栏 scene tree、运行时尺寸计算、HUD 中道路分类按钮路径及相应的 C#/GDScript 契约。未改变分类内容、道路工具、主题、底栏展开高度、输入逻辑或存档。

### 验证状态

- `dotnet build SimpleCities.sln --no-restore`：exit 0，0 warnings，0 errors。
- `dotnet test SimpleCities.sln --no-restore`：27 passed，0 failed，0 skipped。
- `godot --headless --path . --log-file .tmp-godot-qa/command-center-final-5.log --script tests/godot/command_center_runtime_contract.gd`：PASS。新增 435x480 展开态断言确认五个分类按钮均未越出 dock、DockPanel 宽度正确、底边贴合且分类栏仍为 76px 高。
- Godot 输出的 root certificate store error，以及 missing dependency / fallback Config warnings 来自环境和现有降级场景；运行时 contract 本身通过。

---

## BUG-12：ConstructionDock 子工具出现在托盘最左侧且文字被裁剪

### 症状

点击底栏“道路”分类后，`ToolTray` 虽然展开，但“城市道路”工具按钮出现在整条托盘的最左侧，没有从当前“道路”分类上方出现。按钮的 32px 图标、标签和状态下划线同时放入 46px 高的托盘后，标签底部越出按钮并被裁剪，因此视觉上像是子工具没有弹出。

### 根因分析

`ToolList` 是横向扩展的 `HBoxContainer`，默认从左侧排列内容；`ConstructionDock` 没有根据 `_activeCategoryId` 为托盘内容计算水平起点。与此同时，`BuildDockButtonPresentation()` 给动态工具图标设置了 32px 最小尺寸，`TextureRect` 默认还会保留纹理原始尺寸，导致动态图标与 13px 标签的组合高度超过 `ToolTrayHeight = 46f`。

### 修复方案

`ConstructionDock.ApplyDockLayout()` 现在调用 `AlignToolTrayToActiveCategory()`：根据分类组的居中位置、当前分类索引和工具组宽度计算 `TrayMargin` 的左边距，并在两侧窗口边界内钳制。道路分类只有一个 104px 工具按钮时，其中心与“道路”分类中心对齐；窄窗口下则贴边但不越界。

动态工具和 placeholder 的图标改为 20px 紧凑尺寸，`TextureRect.ExpandMode` 设为 `IgnoreSize`，并将动态图标、标签之间的 `VBoxContainer` separation 设为 0，使图标和标签完整落在 46px 托盘内。分类栏自身的 32px 图标不受影响。

### 影响范围

影响 `Scripts/UI/ConstructionDock.cs` 中动态子工具的水平定位和紧凑展示，以及 `tests/godot/command_center_runtime_contract.gd` 的布局回归断言。未改变分类顺序、分类按钮尺寸、底栏 76/122px 折叠与展开高度、工具选择行为、主题、道路系统或存档。

### 验证状态

- BUG-12 focused RED：1600x900 展开态中 `RoadToolButton.x = 0`，按“道路”分类中心计算的预期值为 `524`；标签 rect 为 `(550,810,52,18.14584)`，按钮 rect 为 `(524,778,104,46)`，标签底部越过按钮底部。
- BUG-12 `dotnet build SimpleCities.sln --no-restore`：exit 0，0 warnings，0 errors。
- BUG-12 `godot --headless --path . --log-file .godot/qa-command-center-green-2.log --script tests/godot/command_center_runtime_contract.gd`：PASS。1600x900、640x480 和 435x480 展开态均验证工具组相对当前道路分类居中或按窗口边界钳制，工具按钮、图标、标签全部位于 46px 托盘内。
- BUG-12 `dotnet test SimpleCities.sln --no-restore`：27 passed，0 failed，0 skipped。
- BUG-12 `godot --headless --path . --log-file .godot/qa-roads-category.log --script tests/godot/roads_construction_category_contract.gd`：PASS。
- 当前会话未暴露 `csharp-ls`、Godot editor bridge 或 `godot-minimal` DAP console，因此 focused LSP diagnostics、editor log 和 DAP console gate 无法执行；未用成功 build 或 headless contract 冒充这些 gate。Godot CLI 的 root certificate store error 为环境既有输出，两个运行时 contract 均通过。

---

## BUG-13：ConstructionDock 二级菜单偏离全局中心且两级选中标记混用

### 症状

二级工具组以当前一级分类的位置为锚点，因此切换到处于不同横向位置的分类时，二级内容会跟随分类左右移动，而不是稳定出现在整个底栏的视觉中心。与此同时，一级分类和二级工具共用 3px 下划线；二级下划线悬在两层菜单交界处，一级标记也没有落到 ConstructionDock 的绝对底边。原 46px 二级层还会迫使图标和中文标签使用过度紧凑的尺寸。

### 根因分析

`ConstructionDock` 把二级列表的水平位置耦合到 `_activeCategoryId` 和分类按钮几何，而 `ToolList` 本身没有全宽居中契约。`ConstructionDockButton` 又只提供通用 `SelectedUnderline`，并把它放在图标、标签所在的 `VBoxContainer` 中，组件无法区分“一级分类”和“二级工具”这两种不同层级的选中语义。122px 展开高度和 46px `ToolTray` 则延续了这套紧凑布局约束。

### 修复方案

展开高度改为 140px，`ToolTray` / `ToolScroll` 改为 64px；`ToolList` 作为全宽扩展的 `HBoxContainer` 使用 `Alignment = Center`，移除分类相对偏移，使所有二级工具和未来 placeholder 都按完整 dock 的水平中心排列。二级图标固定为 24px，工具按钮保持 104x64px。

`ConstructionDockButton` 新增显式 `VisualRole`。一级分类使用 `PrimaryCategory`，直接在按钮根下放置 4px `PrimarySelectionIndicator` 并锚到按钮及 dock 绝对底边；二级工具使用 `SecondaryTool`，选中时只保留 pressed surface 和琥珀色图标/文字，不显示下划线。一级分类在托盘折叠后仍保持选中和底部标记。分类行在窄窗口中按 104px 到 72px 响应式缩放，低于可容纳宽度时使用不占高度的隐藏式横向滚动。

### 影响范围

影响 `ConstructionDock` 的展开几何、二级列表对齐、可复用按钮角色、一级/二级选中表现、分类滚动容器路径、HUD 焦点路径以及对应 UI 契约和文档。未改变道路 catalog 数据、`ToolManager` 的工具切换规则、道路建造输入、存档内容、Debug 指标或非 dock 主题。

### 验证状态

- BUG-13 focused RED：静态契约对旧 122/46 几何、缺失底部 indicator 和缺失 `ToolList` 居中产生 3 个失败；1600x900 运行时中 Zoning placeholder 组实际范围为 `[580, 796]`，未与 1600px dock 中心对齐。
- BUG-13 `dotnet build SimpleCities.sln --no-restore`：exit 0，0 warnings，0 errors。
- BUG-13 focused `ConstructionDockContractTests`：9 passed，0 failed，覆盖 140/64 几何、全局居中配置、按钮角色和 4px 绝对底部 indicator 结构。
- BUG-13 `dotnet test SimpleCities.sln --no-restore`：28 passed，0 failed，0 skipped。
- BUG-13 `godot --headless --path . --log-file .godot/qa-construction-dock-final.log --script tests/godot/command_center_runtime_contract.gd`：PASS。1600x900、640x480 和 435x480 下验证 76/140px dock、64px 托盘、道路工具和各 placeholder 组的全局中心、24px 二级图标、一级底部 indicator、二级无下划线、折叠选中状态及 HUD non-overlap；既有 focus、Escape、save/load、道路拖拽和生命周期断言继续通过。
- BUG-13 `godot --headless --path . --log-file .godot/qa-roads-category-final.log --script tests/godot/roads_construction_category_contract.gd`：PASS。
- BUG-13 `git diff --check`：clean。
- 当前会话未暴露 `csharp-ls`、Godot editor bridge 或 `godot-minimal` DAP console，因此 focused LSP diagnostics、editor scene reload/effective-property inspection 和 DAP console gate 被阻塞，未声明通过。Godot CLI 的 root certificate store error 为环境输出；command-center contract 中 missing dependency / fallback Config warnings来自契约刻意覆盖的降级场景，contract 本身通过。

---

## BUG-14：编辑 MapTest 时暂停菜单遮挡整个主场景

### 症状

在 Godot 编辑器中打开 `MapTest.tscn` 时，全屏暂停遮罩和菜单直接显示在画布上，主场景被压暗并遮挡；只有运行游戏后，菜单才会按预期由初始化逻辑隐藏。

### 根因分析

`PauseMenu.tscn` 的根节点默认可见，便于单独设计菜单。此前 `GameHUD.tscn` 实例化该子场景时没有覆盖可见性，只依赖非 `@tool` 的 `PauseMenu._Ready()` 在运行时执行 `Visible = false`。编辑器编排 `MapTest` 时不会执行这段 C# 生命周期代码，因此继承到的默认可见状态一直生效。

### 修复方案

仅在 `Scenes/UI/GameHUD.tscn` 的 `PauseMenu` 实例上序列化 `visible = false`，保留 `PauseMenu.tscn` 根节点的可见默认值，使独立菜单场景仍可正常设计。`PauseMenu.Open()` 继续在运行时显式设置 `Visible = true`，所以 Esc 弹出行为不变。

`GameHUDCompositionContractTests` 精确检查 `PauseMenu` 实例节点块中的隐藏覆盖；`pause_menu_runtime_contract.gd` 在 `MapTest` 实例进入场景树、执行 `_Ready()` 之前检查有效 `visible` 属性，防止运行时隐藏逻辑掩盖编辑期回归。

### 影响范围

影响 `GameHUD` 内暂停菜单的编辑期初始可见性、对应静态/运行时契约和暂停菜单设计文档。未改变暂停菜单独立场景的设计可见性、运行时 Esc 输入、暂停状态、存读档、设置、退出流程或底栏布局。

### 验证状态

- BUG-14 `dotnet build SimpleCities.sln --no-restore`：exit 0，0 warnings，0 errors。
- BUG-14 focused `GameHUDCompositionContractTests|PauseMenuContractTests`：7 passed，0 failed，包含 `GameHUDScene_HidesPauseMenuInstanceWhileAuthoringHud`。
- BUG-14 `dotnet test SimpleCities.sln --no-build`：35 passed，0 failed，0 skipped。
- BUG-14 `csharp-ls --solution SimpleCities.sln --diagnose`：exit 0，成功加载解决方案；当前会话未暴露按文件请求 diagnostics 的 LSP 通道，因此未把该命令声明为 `GameHUDCompositionContractTests.cs` 的 focused diagnostics。
- BUG-14 `godot-minimal_get_diagnostics` 针对 `tests/godot/pause_menu_runtime_contract.gd`：无 diagnostics。
- BUG-14 `godot --headless --path . --log-file .godot/qa-pause-menu-editor-visibility.log --script tests/godot/pause_menu_runtime_contract.gd`：PASS；新增断言在 `_Ready()` 前确认 `GameHUD/PauseMenu.visible == false`，既有暂停、重绑、存读档、确认和场景切换流程继续通过。输出的 missing ToolManager warning 来自契约刻意覆盖的独立 HUD 场景。
- BUG-14 `godot --headless --path . --log-file .godot/qa-command-center-editor-visibility.log --script tests/godot/command_center_runtime_contract.gd`：PASS；既有 HUD 响应式布局、焦点、Escape、save/load、道路拖拽和生命周期契约保持通过，degraded-state warnings 为该契约的预期场景。
- Godot editor bridge 已确认连接到 `SimpleCities` 的正确项目；未执行会丢弃未保存修改的强制重载，而是在当前 `GameHUD` 内存场景中同步、读回 `/root/GameHUD/PauseMenu.visible == false` 并保存。父级 `MapTest` 标签仍持有旧子场景缓存，尝试同步该内存实例时 bridge transport 关闭，因此当前 `MapTest` 画布刷新未声明通过；磁盘场景的 pre-`_Ready()` 有效属性已由独立 Godot 契约验证。两次运行游戏后的结构读取也因 bridge timeout 被阻塞，项目均已停止，DAP 控制台无 error。

---

## BUG-15：暂停菜单捕获按键时 Escape 被 HUD 提前消费

> 修复日期：2026-08-10
> 来源：`docs/bugfix/session-2026-08-05.md#session-bug-05按键绑定捕获时暂停键被-gamehud-提前消费`

### 症状

暂停菜单已经打开并进入 `pause_menu` 按键捕获后，按当前绑定的 Escape，按钮仍停留在“等待输入...”，捕获流程无法完成。

### 根因分析

父级 `GameHUD._Input()` 在 `PauseMenu._Input()` 之前匹配全局暂停动作。菜单已打开时 `OpenPauseMenu()` 虽立即返回，HUD 仍把 Escape 标为已处理，导致负责捕获的暂停菜单收不到事件。

### 修复方案

`GameHUD._Input()` 在检查全局暂停动作前先判断 `_pauseMenu.IsOpen`。菜单打开时 HUD 完全让出输入，由 `PauseMenu` 处理按键捕获、关闭及菜单内交互；菜单关闭时原有 Escape 打开暂停菜单的入口保持不变。

### 影响范围

影响暂停菜单打开期间的 HUD 输入优先级和按键绑定捕获。菜单关闭后的暂停动作、撤销/重做、道路工具快捷键及 UI modal 规则不变。

### 验证状态

- `PauseMenuContractTests` 增加静态顺序契约，确认 `_pauseMenu.IsOpen` 判断位于全局暂停动作匹配之前。
- `pause_menu_runtime_contract.gd` 真实进入 Escape 捕获状态后注入按键，按钮恢复为 `Escape`，菜单保持可见且场景继续暂停，输出 `PASS pause menu runtime contract`。
- `dotnet test SimpleCities.sln --no-restore`：492/492 通过；`dotnet build SimpleCities.sln --no-restore`：0 警告、0 错误。
- 该运行时契约在沙箱外执行以允许写 `user://input_bindings.cfg`，并把绑定重置为默认值；独立 HUD 场景的 `ToolManager.Instance` 缺失警告属于契约预期环境。
