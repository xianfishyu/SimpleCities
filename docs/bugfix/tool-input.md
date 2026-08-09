# Tool Input Bug 修复记录

> 日期：2026-08-10
> 影响文件：`Scripts/Road/RoadBuilder.cs`、`Scripts/Road/RoadConfig.cs`、`tests/SimpleCities.RoadGraph.Tests/RoadInputStrategyTests.cs`、`tests/godot/road_curve_rendering_runtime_contract.gd`、`tests/godot/road_input_strategy_runtime_contract.gd`
> 来源：`docs/bugfix/session-2026-08-05.md` 中 `SESSION-BUG-14`、`SESSION-BUG-15`

---

## BUG-1：原生曲线内部吸附回退到远端端点

### 症状

指针位于 Bezier 等原生曲线内部且仍在道路交互半径内时，如果网格吸附点没有命中道路，开始铺路会从曲线端点起步，而不是从指针附近的曲线位置起步。

### 根因分析

回退查询先用原生几何确认附近存在 Edge，随后却通过 `GraphEdge.GetFullPath()` 选择最近点。单段原生曲线没有旧 waypoint，返回路径只有两个端点，因此权威曲线内部位置在最后一步丢失。

### 修复方案

`RoadBuilder.FindNearestRoadPoint()` 改为遍历目标 Edge 的 `GeometrySegments`，对每段调用 `FindClosestPoint(pointerPosition)`，按 `DistanceSquared` 选择最近的权威几何位置。折线段、Bezier、圆弧和其他原生段共用同一接口。

### 影响范围

影响 `BeginPlace()` 的道路内部回退吸附。节点优先级、网格吸附、道路删除和 RoadGraph 提交语义不变。

## BUG-1 验证状态

- `RoadBuilderCurveFallbackUsesNativeClosestPointInsteadOfPathAnchors` 确认实现调用 `segment.FindClosestPoint()` 且不再走 `GetFullPath()`。
- `road_curve_rendering_runtime_contract.gd -- --snap-only` 在真实 Bezier 夹具中观察到 pointer `(155,-285)`、网格点 `(200,-300)`、结果 `(132.7145,-210.0608)`，证明回退选择曲线内部位置，输出 `PASS road builder native curve snap runtime contract`。

---

## BUG-2：CellSize 无效时道路输入策略初始化失败

### 症状

`RoadConfig.CellSize` 为 0、负数或非有限值时，`RoadBuilder._Ready()` 构造 `SquareEightRoadInputStrategy` 会抛出参数异常，道路工具随后无法开始或提交铺路。

### 根因分析

共享配置没有运行时有限正数约束，建造器直接把导出值传给明确拒绝非法 cell size 的输入策略构造函数。

### 修复方案

`RoadConfig.NormalizeRuntimeValues()` 对 `CellSize` 建立正有限约束，无效值恢复为 `DefaultCellSize = 64` 并报告警告。`RoadBuilder._Ready()` 在创建输入策略之前调用该方法；渲染器也调用同一规范化入口，保证共享资源不受节点初始化顺序影响。

### 影响范围

影响非法配置下道路工具的启动和降级行为。合法网格尺寸、八方向路径策略、预览/提交一致性及配置序列化字段不变。共享的 `RoadWidth` 问题记录在 `road-rendering:BUG-5`。

## BUG-2 验证状态

- `road_input_strategy_runtime_contract.gd` 把真实共享配置的 `CellSize` 和 `RoadWidth` 设为 0，确认两者恢复为正有限值，随后 `BeginPlace()`、更新、提交和可见 mesh 全部成功，输出 `PASS road input strategy runtime contract`。
- `dotnet test SimpleCities.sln --no-restore`：492/492 通过；`dotnet build SimpleCities.sln --no-restore`：0 警告、0 错误。
- Roslyn CodeLens 解决方案诊断为 0 error、0 warning；两个改动 GDScript 诊断为 0，Godot 4.7 editor 错误日志为 0。
- headless Godot 的 Windows root certificate store 读取失败和独立场景 `ToolManager.Instance` 警告不属于本系统失败；运行时契约均以明确 `PASS` 结束。
