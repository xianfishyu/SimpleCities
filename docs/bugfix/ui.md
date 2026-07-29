# UI Bug 修复记录

> 日期：2026-07-30
> 影响文件：`Scripts/UI/ConstructionCategoryDefinition.cs`, `Scripts/UI/ConstructionDock.cs`, `Scripts/UI/ToolContextPanel.cs`, `Scripts/UI/GameHUD.cs`, `Scenes/UI/ConstructionDock.tscn`, `Scenes/UI/RoadsConstructionCategory.tres`, `tests/godot/command_center_runtime_contract.gd`
> 关联事项：5-lane review 阻塞项复查

---

## BUG-1：运行时道路工具托盘只保留 Select

### 症状

`Scenes/UI/RoadsConstructionCategory.tres` 声明了 Select / Road / RoadRemove 三个工具，但 fresh `MapTest` 运行时最终只观察到一个 live 工具按钮。该问题会让玩家无法通过命令中心托盘选择铺路或拆路工具。

### 根因分析

道路 catalog 原先通过普通 C# 数组暴露给 Godot 资源系统，文件文本看起来包含三个 subresource，但运行时 C# 侧迭代路径和 Godot 资源数组不一致，实际按钮构建没有以 Godot 运行时资源数组作为唯一事实来源。旧测试主要检查 `.tscn` / `.tres` 字符串，没有实例化 actual scene 并断言 live 按钮节点。

### 修复方案

`ConstructionCategoryDefinition.Tools` 改为 Godot 资源数组形态并在 `TryValidate()` 中 fail closed；`ConstructionDock.BuildToolButtons()` 只在 category 通过验证后按 catalog 的 `SortOrder` 创建 live 按钮。新增 Godot headless integration test 启动 actual `MapTest` / `GameHUD` / `ConstructionDock`，断言 live `SelectToolButton`、`RoadToolButton`、`RoadRemoveToolButton` 三个按钮和 `ToolScroll` 路径存在。

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

### 症状

640x480 下 `ToolContextPanel` 从 44px compact 入口展开后可能与底部 expanded `ConstructionDock` 或右上 `SystemControls` 重叠；旧测试只用 offset 推导矩形，实际 `Control.get_global_rect()` 曾出现 compact context `x=580, width=132` 并向右溢出。ToolTray 折叠后，反向焦点链仍可能从 context 指回隐藏工具按钮。

### 根因分析

`ToolContextPanel` 的 compact-expanded 高度只看视口高度，没有使用 dock 的实际顶部边界作为底部保留区；compact 状态还保留了 `MarginContainer`、内容 VBox 和面板 style 的最小尺寸贡献，导致实际控件宽度不等于 44。焦点链只在初始配置时写入，ToolTray 收起时 context 的 `FocusPrevious` 没有同步改回 `RoadsCategoryButton`。

### 修复方案

`GameHUD` 将 `ConstructionDock.Position.Y` 传给 `ToolContextPanel.ApplyResponsiveLayoutForViewport(...)`，让 compact-expanded context 按 dock 实际顶部限制底边。`ContextContent` 外包 `ContextContentScroll`，compact 时隐藏 scroll/content、清零 margin 并覆盖 panel style，保证实际 global rect 为 44px；展开时用 ScrollContainer 承载内容最小高度，底部内容通过滚动可达。`ConstructionDock` 发出 `TrayVisibilityChanged`，`GameHUD` 立即重建焦点链：展开时 Category -> Select -> Road -> RoadRemove -> Context，折叠时 Category -> Context，反向链同步镜像。

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

## 验证状态

- `csharp-ls --solution SimpleCities.sln --diagnose`：solution loaded，未报告 diagnostics。
- `dotnet test SimpleCities.sln`：17 passed，0 failed，0 skipped。
- `dotnet build SimpleCities.sln`：0 warnings，0 errors。
- `godot --headless --path . --script tests/godot/roads_construction_category_contract.gd`：PASS，验证 catalog 三工具和 malformed resource validation。
- `godot --headless --path . --script tests/godot/command_center_runtime_contract.gd`：PASS，验证 actual scene live 三按钮、tool sync、focus traversal、context catalog sync、640x480 actual `Control.get_global_rect()` bounds、context scroll path/reachability、CJK theme glyph、malformed dock 和 missing ToolManager/RoadSystem degraded state。该测试会为隔离 degraded 场景输出预期 warning。
- Fresh editor/runtime QA：`MapTest` 运行时观察到三个 live 工具按钮、中文 catalog 文案、默认 debug collapsed、Save/Load F5/F9 状态、默认 viewport bounds 不重叠；editor error log 和 DAP stderr 无新错误。
