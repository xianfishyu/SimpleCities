# Camera Bug 修复记录

> 日期：2026-08-06
> 影响文件：`Scripts/MainCamera.cs`、`tests/godot/camera_zoom_runtime_contract.gd`

---

## BUG-1：滚轮缩放平滑速度依赖帧率

### 症状

摄像机接收相同滚轮缩放输入后，缩放值向目标值收敛所需的真实时间会随刷新率或 `_Process` 调用频率改变。复现中，30 FPS 运行一秒后的 `Zoom.X` 为 `1.957609`，120 FPS 为 `1.999997`，差异为 `0.042388`；因此低刷新率下缩放明显更慢、更不连贯。

### 根因分析

`MainCamera.ScaleUpdate()` 原先每次 `_Process()` 都执行固定权重的 `Zoom.Lerp(..., 0.1f)`，却没有使用传入的 `delta`。较高的更新频率会在同一段真实时间内执行更多次插值，导致收敛速度随帧率上升。

### 修复方案

`ScaleUpdate(double delta)` 采用指数插值权重：`1 - pow(1 - 0.1, delta * 60)`。该公式在每帧 `delta = 1/60` 时保持原有的 `0.1` 插值权重，同时使任意更新频率在相同真实时间内产生一致的缩放收敛结果。

### 影响范围

仅调整滚轮缩放的平滑过程。滚轮输入的目标缩放增量、最小/最大缩放限制、鼠标中键平移、键盘移动和摄像机存档恢复逻辑均未改变。

---

## 验证状态

- 修复前，`godot.exe --headless --path . --log-file .godot\qa-camera-zoom-red.log --script tests\godot\camera_zoom_runtime_contract.gd`：真实 `MapTest` 与隔离摄像机场景在 30 FPS/120 FPS 一秒后的缩放差异均为 `0.042388`。
- 修复后，`godot.exe --headless --path . --log-file .godot\qa-camera-zoom-green.log --script tests\godot\camera_zoom_runtime_contract.gd`：两个场景在 30 FPS/120 FPS 一秒后的缩放值均为 `1.998203`，差异均为 `0.000000`，输出 `PASS camera zoom runtime contract`。
- `dotnet build SimpleCities.sln`：构建成功，0 errors；仅有两个既有的 `NU1900` NuGet 漏洞源不可达警告。
- Roslyn CodeLens 与本测试 GDScript 诊断均无 warnings/errors。测试构造 `MapTest` 时出现既有 `ConstructionDock: ToolManager.Instance is missing` 降级警告，不影响摄像机断言。

---

## BUG-2：键盘平移速度依赖帧率

### 症状

按住移动键一秒时，30 FPS 与 120 FPS 的摄像机平移距离不同；高刷新率会推进更多次移动目标并执行更多次固定权重插值。

### 根因分析

`KeyPosUpdate()` 以每次 `_Process()` 的次数累加 `nextPos`，并使用固定 `Position.Lerp(..., 0.1f)`，两个步骤都没有以真实经过时间为基准。

### 修复方案

以 60 FPS 的原有参数为参考，将目标平移转换为每秒速度，并使用移动目标匀速前进时的一阶平滑解析解更新 `Position`。目标停止时保持指数收敛；中键拖拽期间不执行键盘平移。

### 影响范围

仅影响键盘移动的时间步进与平滑。60 FPS 的配置含义保留，缩放、鼠标拖拽和存档格式不变。

---

## BUG-3：中键拖拽会保留旧的键盘方向

### 症状

先按 WASD 再按住中键拖拽时，摄像机可能继续沿旧方向键盘平移。

### 根因分析

原实现只在未按中键时刷新 `moveInput`，但 `_Process()` 仍无条件运行键盘平移；它也依赖全局鼠标按键状态，而非摄像机收到的拖拽事件。

### 修复方案

新增 `isMiddleDragging`，在中键按下时清空 `moveInput`，拖拽状态下跳过 `KeyPosUpdate()`，释放中键后重新读取当前移动动作。

### 影响范围

中键拖拽与键盘平移改为互斥。拖拽位置计算与 WASD 绑定保持不变。

---

## BUG-4：缩放边界未覆盖存档恢复且与场景配置冲突

### 症状

正数且有限的存档缩放值可绕过 `minScale`/`maxScale`；此外，Inspector 的最小提示值 `0.01` 排除了 `Scenes/MapTest.tscn` 已使用的 `minScale = 0.009`。

### 根因分析

边界只在滚轮输入路径中手工钳制，`RestorePreparedState()` 直接赋值。导出范围与场景资源的有效配置没有统一来源。

### 修复方案

新增 `NormalizeZoomConfiguration()` 和 `ClampZoom()`，在初始化、滚轮输入和恢复存档时统一使用。导出范围下限调整为 `0.001`，保留现有 `0.009` 场景配置，并规范化无效或反向的最小/最大边界。

### 影响范围

影响缩放目标、存档恢复和 Inspector 编辑范围；不改变有效边界内的缩放倍率或存档 JSON 结构。

---

## BUG-5：已由 UI 消费的滚轮事件仍会缩放摄像机

### 症状

摄像机原先在 `_Input()` 中处理滚轮，UI 有滚动或消费鼠标事件时仍可能同时触发地图缩放。

### 根因分析

`_Input()` 发生在 GUI 输入处理之前，不能区分最终由 UI 消费的事件。

### 修复方案

将摄像机输入入口改为 `_UnhandledInput()`，只处理未被 GUI 或其他节点消费的事件。

### 影响范围

UI 控件优先处理其滚轮和键盘输入；未处理的地图输入仍由摄像机处理。

---

## BUG-6：缩小视图时键盘平移的屏幕速度明显下降

### 症状

`Camera2D.Zoom` 缩小后，按住键盘移动键时的世界坐标移动量不足以补偿缩放比例，画面中的相对移动速度明显变慢。使用 `defaultScale = 0.125` 时，原公式得到的屏幕速度仅约为 `defaultScale = 1` 时的 23%。

### 根因分析

`KeyPosUpdate()` 原先使用 `pow(2, -defaultScale)` 调整世界坐标速度。屏幕位移还会乘以 `Camera2D.Zoom`，因此最终速度为 `defaultScale * pow(2, -defaultScale)`，会随着缩放值改变，而非保持恒定。

### 修复方案

将目标世界速度改为以 `1 / defaultScale` 补偿缩放：`keyMoveFactor * moveSpeed * KeyboardMotionReferenceFps / defaultScale`。这使世界坐标速度与 `Camera2D.Zoom` 相乘后保持一致；同时在运行契约中比较 `0.125` 和 `4.0` 缩放下的一秒屏幕位移。

### 影响范围

仅改变键盘平移在不同缩放下的世界坐标速度，保持 `keyMoveFactor` 在缩放为 `1` 时的原有基准速度。滚轮缩放、鼠标中键拖拽、平移的帧率一致性和存档格式不变。

---

## BUG-7：缩放过渡期间键盘平移再次变慢

### 症状

BUG-6 保证摄像机稳定在目标缩放后具有一致的屏幕平移速度，但滚轮放大尚未收敛时，键盘平移仍明显偏慢。回归场景中，从 `0.125` 向 `4.0` 放大时的一帧屏幕位移为 `0.173898`，而稳定在 `0.125` 时为 `0.635971`。

### 根因分析

键盘速度使用目标值 `defaultScale` 做倒数补偿，而画面实际使用仍在平滑变化的 `Zoom.X`。放大开始后，目标值立即变大，世界坐标速度被提前降低；同时 `_Process()` 在更新实际 `Zoom` 之前计算键盘位移，使补偿基准与该帧最终渲染缩放不一致。

### 修复方案

在 `_Process()` 中先执行 `ScaleUpdate(delta)`，再使用更新后的 `Zoom.X` 计算 `KeyPosUpdate(delta)`，并用 `MinimumZoomScale` 防止除零。键盘响应权重改为可导出的 `panSmoothing`，默认值从原固定的 `0.1` 提高到 `0.25`，允许在 Inspector 中调整启动和停止的跟随速度。

### 影响范围

影响缩放过渡期间的键盘世界坐标速度，以及键盘平移的默认响应速度。稳定缩放下的屏幕速度、帧率一致性、滚轮缩放目标、中键拖拽和存档格式不变。

### 后续重构

后续摄像机手感重构保留“使用该帧实际 `Zoom.X` 计算移动”的修复约束，并将 `keyMoveFactor`、`moveSpeed` 和 `panSmoothing` 替换为屏幕空间的 `panSpeed`、`zoomInfluence`、`accelerationTime` 与 `decelerationTime`。BUG-6 和 BUG-7 中的旧字段名仅描述当时已经验证并提交的实现。

---

## BUG-8：小于 0.001 的缩放下限被截断

### 症状

在 Inspector 中配置 `minScale = 0.0001` 时，编辑范围无法精确表达该数值；即使通过场景或代码写入，摄像机进入场景树后也会把 `minScale` 和 `defaultScale` 都提升为 `0.001`。

### 根因分析

三个缩放导出属性的 `PropertyHint.Range` 下限和步进均为 `0.001`，同时 `MinimumZoomScale` 也固定为 `0.001f`。Inspector 精度限制和 `_Ready()` 中的 `NormalizeZoomConfiguration()` 分别在编辑期与运行期截断更小的正缩放值。

### 修复方案

将 `MinimumZoomScale`、缩放导出下限和编辑步进统一调整为 `0.000001`，并为大跨度范围启用指数滑杆。保留严格大于零的安全下限，避免平移与鼠标锚定缩放中的除零。

### 影响范围

`defaultScale`、`minScale` 和 `maxScale` 可精确配置到六位小数；现有 `0.009`、`0.125` 与 `4.0` 配置及缩放边界语义不变。

---

## 验证状态（BUG-2 至 BUG-8）

- `godot.exe --headless --path . --log-file .godot\qa-camera-motion-red-green.log --script tests\godot\camera_zoom_runtime_contract.gd`：首次时间步进修复仍得到 30 FPS `321.806305`、120 FPS `317.332520`，差异 `4.473785`，证明简单的逐帧目标推进不足。
- `godot.exe --headless --path . --log-file .godot\qa-camera-bounds-green.log --script tests\godot\camera_zoom_runtime_contract.gd`：通过平移、中键拖拽、缩放恢复与边界断言；平移差异为 `0.000061`。
- `godot.exe --headless --path . --log-file .godot\qa-camera-ui-input-green.log --script tests\godot\camera_zoom_runtime_contract.gd`：消费滚轮的 UI 控件不会改变摄像机目标缩放；全部摄像机契约通过。
- `godot.exe --headless --path . --log-file .godot\qa-camera-screen-pan.log --script tests\godot\camera_zoom_runtime_contract.gd`：`defaultScale = 0.125` 与 `4.0` 时的一秒屏幕位移均为 `631.572876`，差异为 `0.000000`；平移的 30 FPS/120 FPS 差异为 `0.000122`，全部摄像机契约通过。
- BUG-7 修复前，`godot.exe --headless --path . --log-file .godot\qa-camera-pan-zoom-transition-red.log --script tests\godot\camera_zoom_runtime_contract.gd`：稳定与放大过渡的一帧屏幕位移差异为 `0.462073`，触发回归断言。
- BUG-7 修复后，`godot.exe --headless --path . --log-file .godot\qa-camera-pan-zoom-transition-green.log --script tests\godot\camera_zoom_runtime_contract.gd`：稳定与放大过渡的一帧屏幕位移均为 `1.637314`，差异为 `0.000000`；`panSmoothing = 0.05` 与 `0.5` 的一帧响应分别为 `0.315170` 与 `3.483156`，全部摄像机契约通过。
- BUG-8 修复前，`godot.exe --headless --path . --log-file .godot\qa-camera-min-scale-red.log --script tests\godot\camera_zoom_runtime_contract.gd`：预设的 `0.0001` 在 `_Ready()` 后被截断为 `0.001000`，触发两项回归断言。
- BUG-8 修复后，`godot.exe --headless --path . --log-file .godot\qa-camera-min-scale-green.log --script tests\godot\camera_zoom_runtime_contract.gd`：`minScale` 与 `defaultScale` 均保持 `0.000100`，Inspector 范围精度断言和全部摄像机契约通过。
- `dotnet build SimpleCities.sln`：成功，0 errors；仅有 2 个既有 `NU1900` 警告。headless 运行中仍有根证书存储读取错误和 `ConstructionDock: ToolManager.Instance is missing` 警告，均不影响摄像机断言。

---

## BUG-9：中键拖拽会被较早的输入消费者截断

### 症状

`MapTest` 中间区域可以收到中键拖拽，但当 UI 或其他节点在 `_UnhandledInput()` 之前消费中键事件时，摄像机不会进入拖拽状态。针对性复现中摄像机移动距离为 `0.000`，较早的输入消费者收到了中键按下和释放两个事件。复现时 `GameHUD/PauseMenu` 的 `visible` 为 `false`，因此隐藏的暂停菜单不是直接消费者。

### 根因分析

`MainCamera` 将滚轮、键盘和中键统一放在 `_UnhandledInput()` 中。该入口只接收尚未被 `_Input()`、GUI 或快捷键阶段处理的事件，因此中键拖拽的开始与结束可能在到达摄像机前被截断。

### 修复方案

将中键按下与释放单独移至 `_Input()`，通过 `SetMiddleDragging()` 统一更新拖拽状态和键盘速度，并在处理后调用 `SetInputAsHandled()`。滚轮和键盘仍保留在 `_UnhandledInput()`，因此 UI 上的滚轮不会误触发地图缩放。

### 影响范围

中键拖拽现在优先于 UI 和其他后续输入消费者；滚轮缩放、键盘平移、缩放边界及存档格式不变。

---

## 验证状态（BUG-9）

- 修复前，`godot.exe --headless --path . --log-file .godot\qa-camera-middle-ui-red.log --script tests\godot\camera_middle_drag_input_contract.gd`：摄像机移动距离为 `0.000`，较早的消费者收到 `2` 个中键事件，两项契约断言失败；`PauseMenu visible=false`。
- 修复后，同一运行时契约：摄像机移动距离为 `94.340`，后续消费者收到 `0` 个中键事件，输出 `PASS camera middle drag input contract`；`PauseMenu visible=false`。
- `dotnet build SimpleCities.sln`：成功，0 errors；仅有 2 个既有 `NU1900` 警告。
- `camera_zoom_runtime_contract.gd` 的中键、平移和缩放路径无新增失败；综合契约仍有一项无关失败：当前未提交的 `Scenes/MapTest.tscn` 已移除 `minScale = 0.009`，与旧断言不一致。
- 当前会话未提供 Roslyn CodeLens、Godot editor MCP 或 DAP console，相关编辑器诊断与交互式编辑器验证未执行；headless 运行仍报告既有根证书读取错误和 `ConstructionDock: ToolManager.Instance is missing` 警告。

### BUG-9 后续根因修正（2026-08-10）

后续运行时命中检查确认，真正截断地图区域中键事件的是纯显示用的全屏 `MapBackground/ColorRect`，而不是摄像机输入阶段或隐藏的暂停菜单。该节点已在 `grid-rendering:BUG-2` 中改为 `MouseFilter.Ignore`；摄像机中键相应恢复到 `_UnhandledInput()`，使真实 UI 继续优先处理鼠标事件。BUG-9 中的 `_Input()` 方案保留为历史修复过程，不再代表当前实现。
