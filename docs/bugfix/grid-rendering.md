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

---

<a id="grid-rendering-bug-3"></a>
## BUG-3：已提交的表现 attempt 仍会接纳迟到失败

### 症状

`RoadPresentationTokenTracker.CommitDesired()` 已把目标 token 发布为 `PresentedToken` 后，同一 token、同一 attempt 的迟到异常仍会被 `ReportBuildFailure()` 转换为 `RoadPresentationFailure`。这会让已经同代呈现的表现重新带上 failure 状态，而不是把提交后的迟到结果视为失效结果。

### 根因分析

`ReportBuildFailure()` 只校验 failure token 是否仍为 `DesiredToken`，以及 attempt 编号是否仍为当前编号；它没有排除该 token 已经成为 `PresentedToken` 的情况。因此成功提交不会终止同一 attempt 的 failure admission 窗口。

### 修复方案

在 `Scripts/Road/RoadRenderToken.cs` 的 `RoadPresentationTokenTracker.ReportBuildFailure()` 中增加 `PresentedToken == token` 拒绝条件。目标尚未提交时，当前 attempt 的真实构建异常仍进入既有 stalled/retry 流程；目标已经提交后，同一 attempt 的迟到异常返回 `null`，不会重新写入 `CurrentFailure`。`RoadRenderTokenTests.CommittedAttemptRejectsLateFailure` 固定该提交边界。

### 影响范围

修复只收窄已呈现 token 的迟到 failure admission。普通构建在提交前的失败、同 token 重试、新 token 取代、六分量身份比较和 reserved Load 提交语义不变。

---

## 验证状态（BUG-3）

- 修复前，`dotnet test Tests/SimpleCities.RoadGraph.Tests/SimpleCities.RoadGraph.Tests.csproj --no-restore --filter "FullyQualifiedName~RoadRenderTokenTests.CommittedAttemptRejectsLateFailure"`：1/1 失败，实际返回了 `RoadPresentationFailure`。
- 修复后，`RoadRenderTokenTests`：22/22 通过；`dotnet test SimpleCities.sln --no-restore`：941/941 通过。
- `dotnet build SimpleCities.sln --no-restore -c Debug` 与 `-c ExportRelease`：均为 0 个警告、0 个错误。
- Roslyn CodeLens compiler/analyzer：0 diagnostics；`git diff --check`：通过。
- 本修复属于纯 C# token tracker 状态门禁，按 `godot-csharp-qa` Tier 1 收口；未重复运行 Godot/Vulkan。

---

<a id="grid-rendering-bug-4"></a>
## BUG-4：普通表现已提交后仍返回构建失败

### 症状

普通 rebuild 已经交换新 mesh、node batch 与 surface，并由 `CommitDesired()` 把目标 token 发布为 `PresentedToken` 后，如果同一 `try` 块内的提交后工作再抛出异常，`TryRebuildStaticBatches()` 仍返回 `false`。此时表现状态实际为 ready、资源已由表现层持有，返回值却声称构建失败。

### 根因分析

`TryRebuildStaticBatches()` 的 `catch` 过去无条件返回 `false`。`grid-rendering:BUG-3` 已让 `ReportBuildFailure()` 拒绝已提交 token 的迟到异常，因此该路径不会错误进入 stalled；但 catch 没有把“failure 被拒绝且目标 token/surface 已完整提交”转换为成功结果。性能快照发布、`QueueRedraw()` 及未来同类提交后工作都位于 `CommitDesired()` 之后、同一异常边界之内，故异常会造成状态与返回值分裂。

### 修复方案

在 `Scripts/Road/RoadRenderer.cs` 为当前 attempt 增加本地 `presentationCommitted` 标志，只在 `CommitDesired()` 返回后置位。catch 入口要求该标志成立，并同时检查 `PresentedToken == targetToken` 与 `_presentedSurface.RenderToken == targetToken`；三项都匹配时，目标表现已经由本次 attempt 完整接管，异常降为 `Road presentation post-commit work failed` warning，并返回 `true`，`finally` 继续保持已转交资源的表现层所有权。任一条件不匹配时仍走原有 `ReportBuildFailure()` 与 stalled/取代逻辑并返回 `false`，不会把尚未进入提交的冗余 current rebuild 误判为成功。

Debug-only `RoadRendererUpdateTokenFailureProbe` 在 `CommitDesired()` 之后、性能快照发布之前注入一次性异常，并通过真实同步 rebuild 暴露返回值；`ExportRelease` 不包含该探针。

### 影响范围

修复只改变普通表现已经完成 token/surface 提交后的异常返回语义。提交前 prepare、Resource factory、snapshot、token 取代、stalled/retry，以及 aggregate Load 的 non-yield commit 协议不变。

---

## 验证状态（BUG-4）

- 修复前，聚焦 `OrdinaryPostCommitFailureStillReportsTheCommittedPresentationAsSuccessful`：1/1 失败，源码中没有已提交目标的成功返回分支。
- 修复前，隔离 `APPDATA` 的 `road_render_token_runtime_contract.gd`：退出码 1，真实状态已经 ready，但输出 `A fully committed ordinary presentation reported rebuild failure`。
- 修复后，同一真实 Godot 4.7 headless 契约退出码为 0，输出 `UPDATE_TOKEN_POSTCOMMIT_FAILURE_RESULT resource_before=77 resource_after=77 trigger_count=1 returned_success=true` 与 `PASS road render token runtime contract`；仅包含本场景预期的 post-commit warning、stalled-observer warning 和既有 `ConstructionDock` warning。
- `RoadRendererLifecycleContractTests`：72/72 通过；`dotnet test SimpleCities.sln --no-restore`：947/947 通过。
- `dotnet build SimpleCities.sln --configuration Debug --no-restore` 与 `--configuration ExportRelease --no-restore`：均为 0 个警告、0 个错误。
- Roslyn CodeLens production/test compiler/analyzer：0 diagnostics；目标 GDScript diagnostics：0；Debug DLL 中 `PostCommitFailure` 匹配 12 次，`ExportRelease` 为 0。
- `godot` MCP addon 未连接，因而没有刷新 editor log 与 DAP console 门；真实 CLI 契约输出和退出码已完成本次运行时行为验证。
