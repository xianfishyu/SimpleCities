# 第三代工具输入系统待办清单

> 系统 key：`v3-tool-input`
> 整理日期：2026-08-13
> 证据：当前 `RoadBuilder`、`RoadPlacementSession`、`RoadRemovalSession`、`RoadEditHistory`、工具路由、相关自动化与 `docs/manuals/road-system-v3-gen.md`。
> 主导原则：输入策略只负责生成几何草稿；第三代工具层负责闭环手势、显式类型状态、基于已呈现路面的选择、有界历史和 full-reset 失效边界，但不定义拓扑、样式或磁盘事务。

## 状态总览

| ID | 发现 | 当前状态 | 处置方式 |
|---|---|---|---|
| 2.0 | `RoadPlacementSession` 无法确认闭合道路 | 开放 | 首锚点吸附、闭合预览和环路提交/取消生命周期 |
| 2.1 | `RoadBuilder` 没有与网格策略解耦的类型选择状态 | 开放 | 会话开始冻结类型并显式提交 |
| 2.2 | 既有道路没有先选择后提交的类型改造工作流 | 开放 | 独立 RoadUpgrade 工具、批量选择、取消和撤销重做 |
| 2.3 | 64 项历史为每项保留 before/after 完整 JSON | 开放 | 消费 mutation delta，以 entry/字节双预算替换全图字符串 |
| 2.4 | 外部 Load 可能让旧图工具状态或旧画面继续接受输入 | 开放 | full-reset Preflight 预建空工具 root，并在联合 commit 中一次接管 |

### 设计覆盖矩阵

| 设计范围 | 当前事实 | 关联待办 |
|---|---|---|
| V3 闭环与类型化编辑 | 当前重复点规则拒绝闭合，`RoadBuilder` 只提交几何，工具路由没有 RoadUpgrade | 2.0～2.2、`v3-road-graph:8.3`～`8.5`、`v3-grid-rendering:2.0`～`2.2`、`v3-ui:1.1`～`1.2` |
| V3 操作历史存储 | 当前每次 Execute 前后捕获完整严格 JSON，64 项最多持有 128 份全图字符串 | 2.3、`v3-road-graph:8.5`、`v3-save-system:2.2` |
| V3 加载生命周期 | 外部 Load 可直接 full restore；工具只在 undo/redo 主动取消部分会话，没有统一旧图失效边界 | 2.4、`v3-save-system:2.3`、`v3-road-graph:8.5` |

## V3 实施记录

### 2026-08-13：2.0/2.1 类型化铺路会话基础（部分）

- 新增 `Scripts/Road/V3/RoadPlacementSessionV3.cs`：固定拐点 + 当前末端草稿，提交时生成带目标 `RoadType` 的 `RoadBuildRequest`；零长度拐点拒绝，非法类型构造时抛错；新增 `TryClose()` 显式闭合到首锚点与 `HasSelfIntersection` 检测。
- 新增 12 个 xUnit 用例；完整测试套件 1052/1052 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：首锚点身份半径吸附、`RoadBuilder` 真实接线与完整工具生命周期。

### 2026-08-13：2.1/2.2 工具状态基础（部分）

- 新增 `Scripts/Road/V3/RoadToolState.cs`：定义 `RoadToolType`（Place/Remove/Upgrade）与当前工具/已选 RoadType 状态，非法类型选择被拒绝。
- 新增 4 个 xUnit 用例；完整测试套件 977/977 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：`RoadBuilder` 真实接线、改造选择生命周期与完整工具路由。

### 2026-08-13：2.2 道路改造选择会话（部分）

- 新增 `Scripts/Road/V3/RoadUpgradeSessionV3.cs`：维护目标 RoadType 与已选 canonical Edge ID 集合，支持选择/取消/清空/一次性提交。
- 新增 6 个 xUnit 用例；完整测试套件 983/983 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：`RoadBuilder` 真实接线、surface hit 选择与批量改造提交。

### 2026-08-13：2.0/2.2 道路拆除选择会话（部分）

- 新增 `Scripts/Road/V3/RoadRemovalSessionV3.cs`：维护已选 canonical Edge ID 集合，支持选择/取消/清空/一次性提交。
- 新增 6 个 xUnit 用例；完整测试套件 997/997 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：`RoadBuilder` 真实接线与 surface hit 选择。

### 2026-08-13：2.0～2.2 工具命令执行器（部分）

- 新增 `Scripts/Road/V3/RoadToolCommandExecutor.cs`：把 `RoadPlacementSessionV3` 转换为控制器 `TryBuild`（支持 snapRadius 节点吸附），把 `RoadUpgradeSessionV3` 选择转换为控制器批量 `TryUpgradeSelection`（单次可撤销历史），把 `RoadRemovalSessionV3` 选择转换为控制器批量 `TryRemoveSelection`（单次可撤销历史）；操作前先校验所有 Edge ID 存在，避免部分写入。
- 新增 6 个 xUnit 用例；完整测试套件 1026/1026 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：`RoadBuilder` 真实接线与 surface hit 选择。

### 2026-08-13：2.0 场景最小连续铺路输入处理器（部分）

- 新增 `Scripts/Road/V3/RoadGraphV3InputHandler.cs`：左键添加拐点，回到首锚点 `CloseRadius` 内自动闭合并提交，右键移除最后拐点，Enter 提交当前连续铺路会话，Esc 取消，数字键 1-4 切换 RoadType，Ctrl+Z/Y 撤销/重做；自交路径不提交；`_Draw` 绘制当前会话预览。
- 已将 `RoadGraphV3InputHandler` 作为 `RoadGraphV3System` 子节点加入 `Scenes/MapTest.tscn`；编辑器加载与冻结运行无 stderr 错误。
- 完整测试套件 1054/1054 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：结构化闭合预览与 surface hit 选择。

### 2026-08-13：2.0 铺路会话首锚点闭合半径与结构化闭合预览（部分）

- `RoadPlacementSessionV3` 新增 `IsWithinCloseRadius` / `TryGetClosedDraft` / `TryClose(pointerPosition, closeRadius)`：把首锚点身份半径纳入会话，可返回由同一 `RoadPath` 派生的闭合预览，且不追加退化段；`RoadGraphV3InputHandler` 改用会话级 `TryClose` 处理回到首锚点。
- 新增 10 个 xUnit 用例；完整测试套件 1097/1097 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误；Godot 编辑器加载 `MapTest` 并冻结运行 1 帧无新增 stderr 错误。
- 尚未完成：`RoadBuilder` 真实接线、完整工具生命周期与 surface hit 选择。

### 2026-08-13：2.2 改造/拆除会话表面命中选择（部分）

- `RoadUpgradeSessionV3` / `RoadRemovalSessionV3` 新增 `TrySelectHit`：只接受有效且带稳定 Edge ID 的 `RoadSurfaceHit`，无 Edge owner、非法命中或过期表现直接拒绝；配合 `RoadSurfaceHitProvider.TryResolveEdge` 可在工具层先验证已呈现表现再进入选择集合。
- 新增 6 个 xUnit 用例；完整测试套件 1107/1107 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：`RoadBuilder` 真实接线、完整工具生命周期与真实 surface hit 输入路由。

### 2026-08-13：2.2 应用与场景暴露表面命中提供器（部分）

- `RoadPresentationController` 新增 `HitProvider`，`RoadGraphV3Application` / `RoadGraphV3System` 暴露 `HitProvider`，使工具层可从当前已呈现表现直接解析稳定 Edge ID。
- 新增 1 个 xUnit 用例；完整测试套件 1108/1108 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误；Godot 编辑器加载 `MapTest` 并冻结运行 1 帧无新增 stderr 错误。
- 尚未完成：`RoadBuilder` 真实接线、完整工具生命周期与真实 surface hit 输入路由。

### 2026-08-13：2.4 empty tool root 计划（部分）

- 新增 `Scripts/Road/V3/RoadToolFullReset.cs`：在 Preflight 阶段捕获需要保留的 `RoadToolType` / `RoadType`，形成不携带活动会话、选择或预览的 empty tool root 计划，并提供 `TryApplyTo` 在 commit 时应用到新工具状态。
- 新增 5 个 xUnit 用例；完整测试套件 1113/1113 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：将 empty tool root 接入 `V3RoadLoadPipeline` / `RoadGraphV3Application` 的 aggregate Load commit。

### 2026-08-13：2.3/2.4 道路 Load 管线携带 empty tool root 计划（部分）

- `V3RoadLoadPipelineResult` 新增 `ToolPlan`；`Load` 支持传入 `RoadToolState` 生成 `RoadToolFullReset`，并新增 `TryLoadIntoController` 重载在 full reset 后应用 empty tool root。
- 新增 2 个 xUnit 用例；完整测试套件 1115/1115 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：`RoadGraphV3Application` 的 aggregate Load commit 与 renderer participant。

### 2026-08-13：2.4 应用 Load/LoadIntoCurrent 应用 empty tool root（部分）

- `V3RoadSaveLoadCoordinator` 新增 `LoadResult`，`RoadGraphV3Application.Load` / `LoadIntoCurrent` 现在会在成功 full reset 后把 `RoadToolFullReset` 应用到 `ToolState`。
- 新增 2 个 xUnit 用例；完整测试套件 1117/1117 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：`RoadGraphV3Application` 的 aggregate Load commit 与 renderer participant。

### 2026-08-13：2.0 闭合自交检测与提交前校验（部分）

- `RoadPlacementSessionV3` 重构自交检测为 `PathHasSelfIntersection`，新增 `HasClosedSelfIntersection(pointer, closeRadius)`；闭合预览的首尾 seam 相邻段不再误报自交，开放折线的首尾交叉仍正常检测。
- `RoadGraphV3InputHandler` 改为先用 `TryGetClosedDraft` 生成闭合预览并校验自交，通过后才 `TryClose` 并提交，失败不会把会话留在已闭合状态。
- 新增 3 个 xUnit 用例；完整测试套件 1130/1130 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误；Godot 编辑器加载 `MapTest` 并冻结运行 1 帧无新增 stderr 错误。
- 尚未完成：`RoadBuilder` 真实接线、完整工具生命周期与真实 surface hit 输入路由。

### 2026-08-13：2.0 闭合预览高亮（部分）

- `RoadGraphV3InputHandler._Draw` 在指针进入首锚点闭合半径时，使用 `TryGetClosedDraft` 检测闭合候选，并以半透明颜色绘制从当前锚点到首锚点的闭合预览线段。
- 完整测试套件 1138/1138 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误；Godot 编辑器加载 `MapTest` 并冻结运行 1 帧无新增 stderr 错误。
- 尚未完成：`RoadBuilder` 真实接线、完整工具生命周期与真实 surface hit 输入路由。

### 2026-08-13：2.2 中心线命中测试基础（部分）

- 新增 `RoadSurfaceHitTester`，为工具层提供基于权威 revision 中心线的最近 Edge 命中候选；后续可与 `RoadSurfaceHitProvider` 组合，确保只消费与已呈现表现同代的命中。
- 新增 5 个 xUnit 用例；完整测试套件 1152/1152 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：`RoadBuilder` 真实接线、完整工具生命周期与真实 surface hit 输入路由。

### 2026-08-13：2.2 应用/场景暴露已呈现表面命中查询（部分）

- `RoadGraphV3Application.TryFindClosestSurfaceHit` 与 `RoadGraphV3System` 转发入口：工具层可先获取中心线候选，再经 `HitProvider` 确认与已呈现表现同代后才进入选择。
- 新增 2 个 xUnit 用例；完整测试套件 1154/1154 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：`RoadBuilder` 真实接线、完整工具生命周期与真实 surface hit 输入路由。

### 2026-08-13：2.0～2.2 工具输入路由器（部分）

- 新增 `RoadToolInputRouter`：根据当前工具维护铺路/改造/拆除会话，将左键解析为放置拐点/闭合或表面命中选择；提供 `TryTake*Session` 让调用方取走会话后交给执行器提交。
- 新增 8 个 xUnit 用例；完整测试套件 1162/1162 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误；Godot 编辑器加载 `MapTest` 并冻结运行 1 帧无新增 stderr 错误。
- 尚未完成：将路由器接入 `RoadGraphV3InputHandler` 真实输入事件与提交链路。

### 2026-08-13：2.0～2.2 输入处理器接入路由器（部分）

- `RoadGraphV3InputHandler` 改用 `RoadToolInputRouter`：左键按当前工具处理放置/闭合或表面命中选择，右键移除拐点，Enter 提交当前会话，Esc 取消，P/R/U 切换工具。
- 完整测试套件 1162/1162 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误；Godot 编辑器加载 `MapTest` 并冻结运行 1 帧无新增 stderr 错误。
- 尚未完成：真实输入自动化（鼠标点击）与端到端工具验收。

### 2026-08-13：2.2 改造/拆除选择高亮（部分）

- `RoadGraphV3InputHandler._Draw` 在 Upgrade/Remove 会话存在时，以黄色/红色折线高亮已选 Edge；没有活动放置会话时仍可绘制选择高亮。
- 完整测试套件 1162/1162 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误；Godot 编辑器加载 `MapTest` 并冻结运行 1 帧无新增 stderr 错误。
- 尚未完成：真实输入自动化（鼠标点击）与端到端工具验收。

### 2026-08-13：2.2 批量表面命中选择（部分）

- `RoadUpgradeSessionV3` / `RoadRemovalSessionV3` 新增 `TrySelectHits`：一次处理多个 `RoadSurfaceHit`，只选择有效且带稳定 Edge ID 的命中，返回实际选中数量。
- 新增 4 个 xUnit 用例；完整测试套件 1166/1166 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：连续/矩形批量选择输入与端到端工具验收。

### 2026-08-13：2.4 输入处理器外部重置入口（部分）

- `RoadGraphV3InputHandler` 新增 `ResetTools()`，供 Load/NewCity 等外部生命周期清除当前工具会话。
- 完整测试套件 1207/1207 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误；Godot 编辑器加载 `MapTest` 并冻结运行 1 帧无新增 stderr 错误。
- 尚未完成：真实输入自动化（鼠标点击）与端到端工具验收。

### 2026-08-13：2.4 Load/NewCity 清除工具会话（部分）

- `RoadGraphV3System` 在 `Load` / `LoadIntoCurrent` / `TryCommitPreparedLoad` / `NewCity` 成功后调用 `RoadGraphV3InputHandler.ResetTools()`，避免旧图工具会话残留。
- 完整测试套件 1207/1207 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误；Godot 编辑器加载 `MapTest` 并冻结运行 1 帧无新增 stderr 错误。
- 尚未完成：真实输入自动化（鼠标点击）与端到端工具验收。

### 2026-08-13：2.2 输入处理器暴露工具路由器（部分）

- `RoadGraphV3InputHandler` 新增 `ToolRouter` 只读属性，供 UI/QA 检查当前工具会话。
- 完整测试套件 1208/1208 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实输入自动化（鼠标点击）与端到端工具验收。

### 2026-08-13：2.4 工具状态快照与 Load 原子交换基础（部分）

- `RoadToolState` 新增 `Capture` / `Restore`；Load non-yield commit 应用 empty tool root 失败时回滚旧工具状态。
- 新增 1 个 xUnit 用例；完整测试套件 1216/1216 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实输入自动化与端到端工具验收。

### 2026-08-13：2.2 路由器批量与矩形选择入口（部分）

- `RoadToolInputRouter` 新增 `HandleSelectionHits` 与 `HandleSelectionRect`，改造/拆除会话可一次接收多个 `RoadSurfaceHit` 或矩形解析结果，复用会话去重与提交边界。
- 新增 4 个 xUnit 用例；完整测试套件 1223/1223 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实输入处理器矩形拖拽接线与端到端工具验收。

### 2026-08-13：2.2 矩形表面命中查询与应用/场景暴露（部分）

- `RoadSurfaceHitTester` 新增 `TryFindAllInRect`，按中心线采样段与矩形相交返回每个 Edge 一个 Ribbon hit；`RoadGraphV3Application` / `RoadGraphV3System` 暴露 `TryFindSurfaceHitsInRect`。
- 新增 4 个 xUnit 用例；完整测试套件 1227/1227 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实输入处理器矩形拖拽接线与端到端工具验收。

### 2026-08-13：2.2 输入处理器矩形拖拽选择（部分）

- `RoadGraphV3InputHandler` 在 Upgrade/Remove 工具下按住左键拖拽：移动时绘制矩形，松开时小位移按单击处理，大位移调用 `HandleSelectionRect` 批量选择。
- `RoadGraphV3System.TryFindSurfaceHitsInRect` 作为矩形解析器接入。
- 完整测试套件 1227/1227 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误；Godot `MapTest` 冻结运行无新增错误。
- 尚未完成：真实鼠标拖拽的自动化输入验收与端到端工具验收。

### 2026-08-13：2.2 统一表面命中解析（部分）

- `RoadGraphV3Application.TryFindClosestSurfaceHit` 现在同时考虑 Ribbon、JunctionPatch 与 SemanticJoin，并选择最近的有效命中；工具路由器通过同一入口即可命中 junction/semantic owner。
- 完整测试套件 1234/1234 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实输入自动化与端到端工具验收。

### 2026-08-13：2.2 UI 工具切换同步（部分）

- `ToolType` 新增 `RoadUpgrade`；`ToolTypeExtensions` 将 `Select/Road/RoadRemove/RoadUpgrade` 稳定映射到 `RoadToolType.Select/Place/Remove/Upgrade`；`ToolManager` 在工具切换和 `_Ready` 时同步 V3 `ToolState`，UI“道路改造”按钮可进入 Upgrade。
- `RoadToolType` 新增 `Select`，使非道路工具可映射到无操作工具状态。
- `InputBindingManager` 新增 `tool_road_upgrade`（默认 U），`GameHUD` 通过 U 切换到 `RoadUpgrade`；运行时验证 `CurrentTool` 0 → 3。
- 验证：Godot `MapTest` 冻结运行后点击“道路改造”按钮 `pressed=true` 且上下文显示“道路改造”；完整测试套件 1254/1254 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 surface hit 选择、矩形批量改造、self-loop/parallel Edge 选择、失效 token 与端到端工具验收。

### 2026-08-13：2.1/2.2 切换类型取消未提交铺路并同步改造目标（部分）

- `RoadToolInputRouter.TrySelectRoadType` 在类型变化时取消未提交的 placement 会话，并把活动 upgrade 会话的目标类型更新为新类型；`RoadUpgradeSessionV3` 新增 `TrySetTargetType`。
- 新增 4 个 xUnit 用例；完整测试套件 1254/1254 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误，Godot `MapTest` 冻结运行无新增错误。
- 尚未完成：真实 surface hit 选择与端到端工具验收。

## 执行顺序

### 阶段 3：第三代闭环、类型化建造与道路改造

<a id="v3-tool-input2.0"></a>

- [ ] **2.0 让铺路会话显式创建和取消闭合道路**
  - 当前问题：`RoadPlacementSession` 与 RoadGraph 重复点契约会拒绝回到首锚点，玩家无法通过现有工具创建简单环；若直接追加首点还可能产生零长度末段或预览/提交差异。
  - 修改：在共享 placement 生命周期中识别指针进入首锚点身份半径，显示由同一 `RoadPath` 派生的闭合预览，并在确认时精确复用首锚点而不追加退化段。简单闭环和允许的离散自交提交 RoadGraph；完全回走或连续自重叠显示结构化拒绝。取消、切换工具和暂停清空完整草稿。
  - 依赖：`v3-road-graph:8.3`；真实 closed ribbon 与表面命中在 Phase 7 由 `v3-grid-rendering:2.0` 集成，不阻塞本项的纯草稿/提交契约。
  - 集成负责人：`v3-tool-input`；端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：折线环、全圆原生弧、首点边界内/上/外、最后一步零长度保护、八字形、完全回走、自重叠、右键取消、切换网格策略/工具、暂停和预览/提交几何一致。
  - 验收：合法闭环只产生一次提交和一条历史；失败或取消不修改图、不消耗 ID、不留下预览；三种输入策略不承担环路拓扑规则。

<a id="v3-tool-input2.1"></a>

- [ ] **2.1 让铺路会话显式提交选中的 RoadType**
  - 当前问题：`RoadBuilder.ConfirmPlace` 只把 `RoadPath` 交给 RoadGraph；把类型塞入 `IRoadInputStrategy` 会污染已验证的三种网格可替换边界。
  - 修改：在 `RoadBuilder` 或独立 `RoadToolState` 保存 `SelectedRoadType`，初始为 `Street`；铺路会话开始时冻结类型，并构造显式类型化请求。切换类型先取消未提交会话；三种输入策略和 `RoadPathDraft` 继续只负责几何。
  - 依赖：`v3-road-graph:8.4`、`v3-tool-input:2.0`。
  - 集成负责人：`v3-tool-input`；UI 控件属于 `v3-ui:1.1`，端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：四类建造、会话中切换类型、取消/失败、同异类型接续、交叉、覆盖、闭环、三种输入策略和保存加载往返。
  - 验收：每次成功建造只使用会话冻结类型；切换类型不造成混合提交或部分图写入；更换输入策略不改变 RoadType 状态或 RoadGraph 契约。

<a id="v3-tool-input2.2"></a>

- [ ] **2.2 实现既有道路的批量改造选择与编辑生命周期**
  - 当前问题：工具路由只有建造和拆除，玩家无法预览、取消或一次提交既有 Edge 的目标类型。
  - 修改：新增 `ToolType.RoadUpgrade`；沿用“先选择后提交”的连续轨迹与 Shift 矩形语义，选择期间只保留提交前有效的 canonical Edge ID 和目标类型快照，松开后一次调用 `ChangeRoadType` 并通过 `RoadEditHistory` 记录。hover、连续选择和矩形接触统一消费 `v3-grid-rendering:2.2` 当前 `PresentedRenderToken` 的 `RoadSurfaceHit`，不能各自以中心线近似重做宽路、cap 或 junction 命中；junction/semantic sector owner 映射到可执行的稳定 Edge ID。成功改造可能消除 semantic boundary 并合并 Edge，UI 必须消费 `GraphChanged` 清理失效 ID；可在回归保护后抽取纯选择会话，但删除和改造命令保持独立。
  - 依赖：`v3-road-graph:8.5`、`v3-tool-input:2.1`、`v3-grid-rendering:2.2`。
  - 集成负责人：`v3-tool-input`；工具呈现属于 `v3-ui:1.2`，最终完成判定属于 `v3-road-graph:8.6`。
  - 验证：单击、连续、矩形，ribbon/cap/miter/semantic join/junction patch，self-loop、parallel Edge、重复 Edge、失效/过期 render token、NoChanges、semantic boundary 合并、右键取消、切换工具、暂停、单次撤销重做和视觉表面边界命中。
  - 验收：四种道路工具命中与当前 mesh owner 一致且不接收过期 surface hit；提交前 RoadGraph 不变；成功批次达到 canonical form 且只产生一条历史，选择不缓存已移除 Edge；失败、取消和 NoChanges 无事件、无历史、无残留预览。

### 阶段 6：可逆 delta 历史

<a id="v3-tool-input2.3"></a>

- [ ] **2.3 用有界可逆 delta 替换完整 JSON 编辑历史**
  - 当前问题：`RoadEditHistory` 在每次尝试前后调用 `CaptureState`，每个成功 entry 独立保存 before/after JSON，撤销/重做再次完整解析并重建全图，外部分叉也靠再次序列化比较。64 项历史对稳定 JSON 大小呈约 128 份全图字符串的增长；长 canonical Edge 只减少 Node/Edge 元数据，不会消除 geometry 数组或捕获峰值。
  - 修改：消费 `v3-road-graph:8.5` mutation plan 产生的 `RoadGraphDelta`，记录 created/removed/updated Node/Edge 的完整前后实体和 `BeforeRevisionID` / `AfterRevisionID`；历史项维护下一次合法方向所需的完整 `(LineageID, DomainRevisionID, ChangeSequence)` token。undo/redo 校验 token 后应用逆/正 delta，经同一事务不变式、索引维护和一次 `GraphChanged`，恢复相应内容 revision 但获得新 sequence；revision allocator、ID watermark 与 sequence 永不回退/复用，redo 可重插历史实体原 ID，新分叉分配更高 ID 并清 redo。历史同时限制 entry 数和估算字节；提交前完成 admission，最旧优先淘汰，单命令超过上限时在图提交前拒绝。外部 Load 创建新 lineage 并在 full-reset commit 清空历史。V3 不提供完整图 prepared snapshot 兼容阶段。
  - 依赖：`v3-road-graph:8.2`～`8.5`；磁盘 payload 与资源预算属于 `v3-save-system:2.1`～`2.2`。
  - 集成负责人：`v3-tool-input`；delta 的领域生成与应用由 `v3-road-graph` 提供，端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：建造、交叉拆分、删除支路后合并、简单环、八字形、四类型改造/semantic boundary 消失、失败/NoChanges、新编辑清空 redo；`R0/S0 -> edit R1/S1 -> undo R0/S2 -> redo R1/S3`、错误方向/旧 sequence/重复 replay、外部 full reset 前 token、revision 分叉、undo 后新分叉不复用 ID、容量/字节淘汰和单命令超预算。记录 64 次 geometry-dense 小编辑的 retained bytes、临时分配、undo/redo 时间和事件次数，并与 JSON 基线比较。
  - 验收：每次成功道路命令精确可撤销/重做且只存变化实体；错误 token 返回 `StaleGraphState` 无副作用；不保留 before/after 全图 JSON；超预算不会产生“编辑成功但不可撤销”；恢复相同实体 ID、loop seam、原生几何、类型和空间命中，但 revision/ID allocator 与 sequence 只增不减，full reset 后旧历史不能作用于新 lineage。

### 阶段 7：Load 工具参与者

<a id="v3-tool-input2.4"></a>

- [ ] **2.4 将成功 full reset 作为旧图工具状态的原子失效边界**
  - 当前问题：外部 Load 可能在 placement、removal、RoadUpgrade、hover、矩形框、preview、selection 与 history 仍持有旧 Edge ID 时替换 RoadGraph。若在解析开始时就取消，损坏、超限或取消的 Load 又会无故破坏当前会话。
  - 修改：作为 `v3-save-system:2.3` aggregate Load 的关键参与者，实现无副作用 `PreflightFullReset`，从当前状态准备 empty tool root 和不可抛交换 plan。Admission 冻结新道路命令但逐值保留现有状态；全部 payload、工具和 renderer 资源准备成功且 generation 有效后，短 non-yield commit 同时交换 graph root、empty tool root、隐藏 mesh/RID、surface/hit index、matching presented token 和 `CurrentSlotID`。empty tool root 不包含 placement/removal/upgrade、hover、selection、preview/highlight/bounds、历史、排队道路命令或旧异步 continuation；`CurrentTool`、`SelectedRoadType` 和输入绑定可按明确契约保留。关键表现失败只能在 Preflight，失败、取消或 generation 失配逐值保留旧图、工具和表现；成功 commit 内即发布 matching `PresentationReady`，不存在提交后表现失败或重试分支。普通 mutation 仍按 removed/updated surface owner 清理或重映射，并可经历正常的异步表现门禁。
  - 依赖：`v3-road-graph:8.5`、`v3-save-system:2.3`、`v3-grid-rendering:2.2`、`v3-tool-input:2.0`～`2.3`。
  - 集成负责人：`v3-tool-input`；暂停菜单 busy/结果呈现属于 `v3-ui:1.4`，端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：placement 有固定拐点/闭环预览、continuous/rectangle removal、upgrade、hover、selection bounds、undo/redo、排队命令非空时分别执行成功、损坏、超限、取消、scene/menu/saveable generation 失配 Load；每个关键 Preflight 资源失败；commit 同帧输入、旧 surface hit、连续两个 Load、Load 后第一条编辑，以及普通 mutation 的表现延迟/失败/重试。记录 operation、graph、tool、render token 与事件顺序。
  - 验收：失败或未提交 Load 不改变图、草稿、选择、hover、preview、历史、renderer 或当前槽；成功 commit 后所有新根和 token 同时生效，不存在可提交的旧实体/命令/overlay，也没有 graph-new/mesh-old 窗口；Load 接口不存在提交后关键 participant 失败结果。普通 mutation 不被误当 full reset。

## 暂不执行

### 新输入网格和高级改造手势

- 延期原因：第三代首轮只要求既有三种策略复用、闭环和明确的批量类型改造；没有新的网格或刷子式编辑产品需求。
- 保持现状：`IRoadInputStrategy` / `RoadPathDraft` 只输出几何，不持有拓扑或 RoadType。
- 重新开启条件：出现可独立验收的新网格、编辑手势或可访问性需求。

## 已解决基线

- [x] **相机、工具和暂停输入已有统一可重绑入口。** 输入动作由 `InputBindingManager` 管理。
- [x] **三种输入网格共用同一提交边界。** 米字型、三角形和六边形策略只产生 `RoadPathDraft`；交叉、拆分、不变式和存档由 RoadGraph 处理。
- [x] **连续铺路、批量拆除和完整图 JSON 历史已建立 V2 行为基线。** V3 在替换存储方式时必须保留用户可见的确认、取消、撤销与重做语义。

## 完成标准

1. 2.0～2.4 通过闭环草稿、显式类型建造、基于 presented surface 的 canonical Edge 批量改造、取消、token 防护有界 delta 和 full-reset 联合接管测试。
2. 环路拓扑与 RoadType 不进入 `IRoadInputStrategy`；三种既有策略继续通过共享契约。
3. 磁盘 JSON 不再充当编辑历史，超预算命令在图提交前失败，外部 Load 后旧历史不能作用于新 lineage。
4. Load 成功时 graph/tool/mesh/surface/token/`CurrentSlotID` 一次交换；关键失败只发生在 Preflight，失败或取消逐值保留当前会话。
5. `v3-road-graph:8.6` 负责与存档、渲染和 UI 的最终组合验收。
