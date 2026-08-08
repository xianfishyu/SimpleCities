# Grid Rendering Bug 修复记录

> 日期：2026-07-18
> 影响文件：`Scripts/Grid/MapBackground.cs`
> 关联事项：用户报告的 nullable 警告

---

<a id="grid-rendering-bug-1"></a>
## BUG-1：Godot 生命周期字段触发 nullable 初始化警告

### 症状

启用 nullable 引用类型后，`MapBackground.Instance`、Inspector 注入的 `Display`，以及在 `_Ready()` 中获取的 `_shaderMaterial` 无法从声明处证明已初始化或始终非空。

### 根因分析

`Instance` 和 `Display` 分别由 Godot 节点生命周期及场景反序列化赋值，C# 的构造时静态分析无法识别这些保证。`Display.Material as ShaderMaterial` 则确实可能返回 `null`，原字段类型没有表达这一运行时状态。

### 修复方案

为生命周期保证注入的 `Instance` 和 `Display` 添加 `null!` 初始化，保留它们的非空使用契约；将 `_shaderMaterial` 声明为 `ShaderMaterial?`，并继续使用 `_Process()` 中已有的空值检查保护后续访问。未改变场景结构或渲染逻辑。

### 影响范围

修改仅影响 `MapBackground` 的 nullable 类型标注。网格参数、Shader 参数更新、相机读取和显示行为不受影响。

---

## 验证状态

- `dotnet build SimpleCities.sln`：构建成功，0 个警告，0 个错误。
- 未执行场景手工验证；本次修改不改变运行时控制流。

---

## BUG-2：全屏地图背景截断摄像机鼠标输入

### 症状

`MapTest` 的地图中心没有可见交互控件，但中键事件无法进入摄像机的 `_UnhandledInput()`。运行时命中检查显示鼠标实际悬浮在 `MapBackground/ColorRect`，其矩形覆盖整个 `1600 x 900` viewport。

### 根因分析

`MapBackground` 使用 `CanvasLayer` 和全屏 `ColorRect` 承载屏幕空间网格 Shader，这种渲染结构本身合理；但 `ColorRect` 未声明 `mouse_filter`，因此继承 `MouseFilter.Stop`。`CanvasLayer.layer = -100` 只控制绘制顺序，不会使 `Control` 退出 GUI 命中检测，纯显示背景因而消费了整个地图区域的鼠标事件。

### 修复方案

在 `Scenes/map_background.tscn` 中将 `MapBackground/ColorRect.mouse_filter` 设置为 `Ignore`。同时撤销摄像机抢在 GUI 前处理所有中键事件的绕行方案，将中键恢复到 `_UnhandledInput()`：地图背景不再截断拖拽，而真实按钮、滚动区和面板仍保持输入优先级。摄像机历史绕行见 `docs/bugfix/camera.md#bug-9中键拖拽会被较早的输入消费者截断`。

### 影响范围

网格 Shader、背景绘制顺序和视觉效果不变。地图区域允许中键拖拽，拖拽从真实 UI 控件上开始时不会移动摄像机；滚轮仍遵循相同的 UI 优先规则。

---

## 验证状态（BUG-2）

- 修复前，`godot.exe --headless --path . --log-file .godot\qa-camera-background-input-red.log --script tests\godot\camera_middle_drag_input_contract.gd`：地图背景过滤值为 `Stop(0)`；地图拖拽距离 `94.340`，但从真实 `DebugToggleButton` 开始仍移动 `20.000`，两项契约断言失败。
- 修复后，同一运行时契约：背景过滤值为 `Ignore(2)`，地图拖拽距离 `94.340`，UI 上拖拽距离 `0.000`，输出 `PASS camera middle drag input contract`。
- `dotnet build SimpleCities.sln`：成功，0 errors；仅有 2 个既有 `NU1900` 警告。
- `camera_zoom_runtime_contract.gd` 的中键、缩放与平移路径无新增失败；综合契约仍有一项无关失败：当前未提交的 `Scenes/MapTest.tscn` 已移除 `minScale = 0.009`，与旧断言不一致。
- 当前会话未提供 Roslyn CodeLens、Godot editor MCP 或 DAP console；headless 运行仍报告既有根证书读取错误和 `ConstructionDock: ToolManager.Instance is missing` 警告。
