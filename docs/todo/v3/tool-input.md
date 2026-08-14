# 第三代工具输入系统待办清单

> 系统 key：`v3-tool-input`
> 整理日期：2026-08-14
> 证据：当前 `RoadBuilder`、`RoadPlacementSession`、`RoadRemovalSession`、`RoadEditHistory`、工具路由、相关自动化与 `docs/manuals/road-system-v3-gen.md`。
> 主导原则：输入策略只负责生成几何草稿；第三代工具层负责闭环手势、显式类型状态、基于已呈现路面的选择、有界历史和 full-reset 失效边界，但不定义拓扑、样式或磁盘事务。

## 状态总览

| ID | 发现 | 当前状态 | 处置方式 |
|---|---|---|---|
| 2.0 | `RoadPlacementSession` 无法确认闭合道路 | 已完成 | 已建立首锚点吸附、闭合预览、单次提交与完整取消生命周期 |
| 2.1 | `RoadBuilder` 没有与网格策略解耦的类型选择状态 | 开放 | 会话开始冻结类型并显式提交 |
| 2.2 | 既有道路没有先选择后提交的类型改造工作流 | 开放 | 独立 RoadUpgrade 工具、批量选择、取消和撤销重做 |
| 2.3 | 64 项历史曾为每项保留 before/after 完整 JSON | 已完成 | delta/双预算已替换全图字符串，真实 V3 Load 会换 lineage 并清空旧历史/token |
| 2.4 | 外部 Load 可能让旧图工具状态或旧画面继续接受输入 | 开放（部分实现） | placement/removal/history 与 matching ribbon surface/token 已加入 full-reset aggregate；补齐 upgrade、surface selection 与命令门禁 |

### 设计覆盖矩阵

| 设计范围 | 当前事实 | 关联待办 |
|---|---|---|
| V3 闭环与类型化编辑 | 已完成三种策略共享的闭环草稿、单次提交和取消生命周期；领域层已有显式 `RoadBuildRequest`，但 `RoadBuilder` 仍固定提交 `Street`，类型选择/会话冻结与 RoadUpgrade 尚未接入 | 2.1～2.2、`v3-road-graph:8.4`～`8.5`、`v3-grid-rendering:2.0`～`2.2`、`v3-ui:1.1`～`1.2` |
| V3 操作历史存储 | `RoadEditHistory` 只保留可逆 delta 与完整 state token，以 entry/估算字节双预算在提交前 admission；真实 V3 Load 创建新 lineage，full reset 立即清空 undo/redo 并让旧 token 失效 | 2.3、`v3-road-graph:8.5`、`v3-save-system:2.1` |
| V3 加载生命周期 | `ToolManager` / `RoadBuilder` 已提供 generation-guarded full-reset plan；成功 aggregate 清空 placement/removal/history，并与基础 mesh、同 mesh 的 Edge ribbon surface、matching desired/presented render token 一次交换，失败 Load 保留旧会话。RoadUpgrade、surface hover/selection 和命令门禁尚未存在 | 2.4、`v3-save-system:2.3`、`v3-grid-rendering:2.2`、`v3-road-graph:8.5` |

## 执行顺序

### 阶段 3：第三代闭环、类型化建造与道路改造

<a id="v3-tool-input2.0"></a>

- [x] **2.0 让铺路会话显式创建和取消闭合道路**
  - 完成前问题：`RoadPlacementSession` 与 RoadGraph 重复点契约会拒绝回到首锚点，玩家无法通过现有工具创建简单环；直接追加首点还可能产生零长度末段或预览/提交差异。
  - 已实现：共享 `RoadPlacementSession` 在已有固定段时，以当前 `IRoadInputStrategy.InteractionRadius` 对指针执行首锚点闭合判定；半径内及边界上精确复用 `StartPosition`，由同一 `RoadPath` 生成 preview 与提交，不追加零长度段，闭合后不再接受额外点。`RoadBuilder` 只在成功确认后结束会话并记录一次 history；`SelfOverlap` 等失败保留可编辑会话。右键、切换输入策略/工具、暂停、undo/redo 与 graph 注入均清空完整 placement/preview，未把 topology 规则放入三种策略。
  - 依赖：`v3-road-graph:8.3`；真实 closed ribbon 与表面命中在 Phase 7 由 `v3-grid-rendering:2.0` 集成，不阻塞本项的纯草稿/提交契约。
  - 集成负责人：`v3-tool-input`；端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证证据（2026-08-13）：`RoadPlacementSessionTests` 覆盖首点半径内/边界上/边界外、精确首锚、闭合后零长度保护和失败回走的图/history/ID 原子性；`AlternativeRoadInputStrategyTests` 让米字型、六边形和三角形策略均通过共享 session 生成 rooted loop，且每个合法闭环只产生 1 条 history。最终 solution 自动化为 586/586，build 为 0 警告/0 错误，Roslyn analyzer 为 0 diagnostics，相关 GDScript 逐文件为 0 diagnostics。
  - Godot Tier 3（2026-08-13）：冻结运行真实 `MapTest`，闭合 preview 为 5 点，确认返回成功，history 从 0 增至 1，session/preview 清零，renderer 为 1 Edge/10 mesh 顶点。完全回走返回 `SelfOverlap` 并保留 1 个固定段的可编辑会话，history/renderer 不变；带固定段的右键取消、切出 Road 工具和打开暂停菜单均清空 session/preview 且不增加 history。修改后的 `road_input_strategy_runtime_contract.gd` 完整运行输出 `PASS`，存档、删除、撤销重做边界继续通过，临时槽已删除。
  - 验收结果：合法闭环只有一次提交和一条 history；失败或取消不修改图、不消耗 ID、不留下 preview；三种输入策略继续只生成几何。closed ribbon 的 Phase 7 视觉与表面命中仍归 `v3-grid-rendering:2.0`，最终跨系统判定仍归 `v3-road-graph:8.6`。

<a id="v3-tool-input2.1"></a>

- [ ] **2.1 让铺路会话显式提交选中的 RoadType**
  - 当前问题：`v3-road-graph:8.4` 已要求 `RoadBuilder.ConfirmPlace` 构造 `RoadBuildRequest`，但当前请求仍固定使用 `RoadType.Street`；工具层没有可选择状态或会话冻结值。把类型塞入 `IRoadInputStrategy` 仍会污染已验证的三种网格可替换边界。
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

- [x] **2.3 用有界可逆 delta 替换完整 JSON 编辑历史**
  - 完成前问题：`RoadEditHistory` 在每次尝试前后调用 `CaptureState`，每个成功 entry 独立保存 before/after JSON，撤销/重做再次完整解析并重建全图，外部分叉也靠再次序列化比较。64 项历史对稳定 JSON 大小呈约 128 份全图字符串的增长。
  - 修改：消费 `v3-road-graph:8.5` mutation plan 产生的 `RoadGraphDelta`，记录 created/removed/updated Node/Edge 的完整前后实体和 `BeforeRevisionID` / `AfterRevisionID`；历史项维护下一次合法方向所需的完整 `(LineageID, DomainRevisionID, ChangeSequence)` token。undo/redo 校验 token 后应用逆/正 delta，经同一事务不变式、索引维护和一次 `GraphChanged`，恢复相应内容 revision 但获得新 sequence；revision allocator、ID watermark 与 sequence 永不回退/复用，redo 可重插历史实体原 ID，新分叉分配更高 ID 并清 redo。历史同时限制 entry 数和估算字节；提交前完成 admission，最旧优先淘汰，单命令超过上限时在图提交前拒绝。外部 Load 创建新 lineage 并在 full-reset commit 清空历史。V3 不提供完整图 prepared snapshot 兼容阶段。
  - 依赖：`v3-road-graph:8.2`～`8.5`、`v3-save-system:2.1`；磁盘 streaming/恢复预算仍由开放的 `v3-save-system:2.2` 独立负责，不改变历史的完成状态。
  - 集成负责人：`v3-tool-input`；delta 的领域生成与应用由 `v3-road-graph` 提供，端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：建造、交叉拆分、删除支路后合并、简单环、八字形、四类型改造/semantic boundary 消失、失败/NoChanges、新编辑清空 redo；`R0/S0 -> edit R1/S1 -> undo R0/S2 -> redo R1/S3`、错误方向/旧 sequence/重复 replay、外部 full reset 前 token、revision 分叉、undo 后新分叉不复用 ID、容量/字节淘汰和单命令超预算。记录 64 次 geometry-dense 小编辑的 retained bytes、临时分配、undo/redo 时间和事件次数，并与 JSON 基线比较。
  - 验收：每次成功道路命令精确可撤销/重做且只存变化实体；错误 token 返回 `StaleGraphState` 无副作用；不保留 before/after 全图 JSON；超预算不会产生“编辑成功但不可撤销”；恢复相同实体 ID、loop seam、原生几何、类型和空间命中，但 revision/ID allocator 与 sequence 只增不减，full reset 后旧历史不能作用于新 lineage。
  - 已实现：`RoadEditHistory` 消费 `RoadGraphDelta`/`GraphStateToken`，不再调用已删除的 `CaptureState`/`RestoreState`；建造、交叉拆分、连续/矩形及 64 Edge 批量删除、简单环、八字形、类型边界合并、失败、分叉和 full reset 都经过同一 admission/undo/redo 边界。entry 数与估算字节双预算最旧优先淘汰，单项超预算在 root、token、watermark 和事件变化前拒绝；外部 mutation/full reset 立即清空两栈。V3 `CommitPreparedLoad` 创建新 lineage 并发布一次 full reset，旧 token 无法作用于新图。
  - 验证证据（2026-08-14）：`RoadEditHistoryTests` 为 16/16，完整 solution 自动化最终为 637/637；Debug build 为 0 警告/0 错误，Roslyn compiler/analyzer 为 0 diagnostics。Release 的 64 项、每项 1,024 geometry 历史保留 12,308.0 KiB，对比旧 128 份完整 JSON 98,300.4 KiB；64 edit + 64 undo + 64 redo 共发布 192 次普通事件、0 full reset。真实 `MapTest` 的建造/undo/redo 为 `1 Edge/4 vertices/1:0 -> 0/0/0:1 -> 1/4/1:0`；随后 V3 Save、继续编辑、Load 恢复原图，history 变为 undo/redo `0/0`，Load 前 token 与两栈均不能作用于新 lineage。editor 与 DAP 增量错误通道为空，测试槽已清理。
  - 验收结果：每次成功命令只保存变化实体；错误/旧 token 无副作用，超预算不会产生不可撤销编辑；undo/redo 恢复实体 ID、loop seam、原生几何、类型和空间命中，但 allocator/sequence 不回退。真实 V3 full reset 后旧历史与 token 均失效。

### 阶段 7：Load 工具参与者

<a id="v3-tool-input2.4"></a>

- [ ] **2.4 将成功 full reset 作为旧图工具状态的原子失效边界**
  - 当前问题：基础 aggregate Load 已能原子替换当前已有的 placement、removal、history、renderer overlay、基础 mesh、同 mesh triangle 的 Edge ribbon surface 与 matching 六分量 render token，但 RoadUpgrade、基于已呈现 surface 的 hover/selection、排队道路命令和道路交互门禁尚未实现，因而还不能证明设计列出的全部旧图状态都在同一边界失效。若在解析开始时就取消，损坏、超限或取消的 Load 仍会无故破坏当前会话。
  - 修改：作为 `v3-save-system:2.3` aggregate Load 的关键参与者，实现无副作用 `PreflightFullReset`，从当前状态准备 empty tool root 和不可抛交换 plan。Admission 冻结新道路命令但逐值保留现有状态；全部 payload、工具和 renderer 资源准备成功且 generation 有效后，短 non-yield commit 同时交换 graph root、empty tool root、隐藏 mesh/RID、surface/hit index、matching presented token 和 `CurrentSlotID`。empty tool root 不包含 placement/removal/upgrade、hover、selection、preview/highlight/bounds、历史、排队道路命令或旧异步 continuation；`CurrentTool`、`SelectedRoadType` 和输入绑定可按明确契约保留。关键表现失败只能在 Preflight，失败、取消或 generation 失配逐值保留旧图、工具和表现；成功 commit 内即发布 matching `PresentationReady`，不存在提交后表现失败或重试分支。普通 mutation 仍按 removed/updated surface owner 清理或重映射，并可经历正常的异步表现门禁。
  - 依赖：`v3-road-graph:8.5`、`v3-save-system:2.3`、`v3-grid-rendering:2.2`、`v3-tool-input:2.0`～`2.3`。
  - 集成负责人：`v3-tool-input`；暂停菜单 busy/结果呈现属于 `v3-ui:1.4`，端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：placement 有固定拐点/闭环预览、continuous/rectangle removal、upgrade、hover、selection bounds、undo/redo、排队命令非空时分别执行成功、损坏、超限、取消、scene/menu/saveable generation 失配 Load；每个关键 Preflight 资源失败；commit 同帧输入、旧 surface hit、连续两个 Load、Load 后第一条编辑，以及普通 mutation 的表现延迟/失败/重试。记录 operation、graph、tool、render token 与事件顺序。
  - 验收：失败或未提交 Load 不改变图、草稿、选择、hover、preview、历史、renderer 或当前槽；成功 commit 后所有新根和 token 同时生效，不存在可提交的旧实体/命令/overlay，也没有 graph-new/mesh-old 窗口；Load 接口不存在提交后关键 participant 失败结果。普通 mutation 不被误当 full reset。
  - 阶段进展（2026-08-14）：`ToolManager.BeginLoadAdmission()` 捕获当前工具和 generation，`RoadBuilder.PreflightFullReset()` 预建绑定同一 graph facade 的空 `RoadEditHistory`。aggregate commit 清空 placement/removal、鼠标手势标志与旧 history，保留明确的 `CurrentTool`；renderer 同一计划清空 preview、removal preview、selection bounds 和 hover，并交换基础 mesh 与 matching desired/presented `RoadRenderToken`。失败/取消/generation 失配只 dispose replacement，旧状态不变。
  - Surface 进展（2026-08-14）：renderer 已在普通 rebuild 和 aggregate Load 中将不可变 `EdgeRibbon` `RoadSurfaceSnapshot` 与 mesh/matching token 一次交换，并公开点命中与矩形 owner 查询；provider 在 presentation 不 current 或 snapshot token 不匹配时拒绝查询。此切片只建立工具可消费的基础边界，没有改动 removal 的既有中心线选择，也没有提前接入 upgrade。
  - 当前证据（2026-08-14）：`PreparedAggregateLoadTests`、`RoadRendererLoadPrepareTests`、`RoadSurfaceSnapshotTests`、history/load/token 契约与完整 774/774 自动化通过。`road_renderer_lifecycle_runtime_contract.gd` 在 renderer participant 缺失时要求 Load 提交前失败，并逐值验证活动 payload、undo/redo、未完成 placement 与 `CurrentSlotID` 不变；成功 V3 Load 已验证创建新 lineage、history 归零且旧 graph token 失效。Godot MCP 进一步验证 Load 在同一次 aggregate 中推进 render facade generation/change sequence，并让 desired/presented/surface hit token 完全匹配；Load 前新增道路的 surface 已失效。`road_closed_ribbon_runtime_contract.gd` 还在已加载图中通过真实 removal 会话选择 seam 侧支路，确认后从 `4 Edge / 20 vertices / 4 markers` 收敛为 `2 Edge / 12 vertices / 2 markers`，原 seam 不再出现伪 marker。
  - 仍缺（保持开放）：尚未实现的 RoadUpgrade/`SelectedRoadType`、完整 surface owner/空间索引/canonical location、surface hover/selection、排队 continuation 和道路交互门禁无法由当前切片覆盖；也未完成每类工具状态与每个关键 renderer Preflight 故障点的联合矩阵。

## 暂不执行

### 新输入网格和高级改造手势

- 延期原因：第三代首轮只要求既有三种策略复用、闭环和明确的批量类型改造；没有新的网格或刷子式编辑产品需求。
- 保持现状：`IRoadInputStrategy` / `RoadPathDraft` 只输出几何，不持有拓扑或 RoadType。
- 重新开启条件：出现可独立验收的新网格、编辑手势或可访问性需求。

## 已解决基线

- [x] **相机、工具和暂停输入已有统一可重绑入口。** 输入动作由 `InputBindingManager` 管理。
- [x] **三种输入网格共用同一提交边界。** 米字型、三角形和六边形策略只产生 `RoadPathDraft`；交叉、拆分、不变式和存档由 RoadGraph 处理。
- [x] **闭合 placement 已共用同一预览、提交与取消生命周期。** 三种输入策略均可经共享 session 精确回到首锚点并只产生一次 history；失败回走保留会话，右键、工具切换和暂停不留下 preview 或图副作用。
- [x] **编辑历史已从全图 JSON 替换为有界 delta。** 建造、交叉、环路、八字形、类型合并和批量删除均可经完整 token 撤销重做；entry/字节淘汰、提交前超预算拒绝和 64 项 geometry-dense 内存对比已有自动化与 Release 证据，真实 V3 Load 的新 lineage 已验证清空两栈并拒绝旧 token。
- [x] **连续铺路、批量拆除和完整图 JSON 历史已建立 V2 行为基线。** V3 在替换存储方式时必须保留用户可见的确认、取消、撤销与重做语义。

## 完成标准

1. 2.0～2.4 通过闭环草稿、显式类型建造、基于 presented surface 的 canonical Edge 批量改造、取消、token 防护有界 delta 和 full-reset 联合接管测试。
2. 环路拓扑与 RoadType 不进入 `IRoadInputStrategy`；三种既有策略继续通过共享契约。
3. 磁盘 JSON 不再充当编辑历史，超预算命令在图提交前失败，外部 Load 后旧历史不能作用于新 lineage。
4. Load 成功时 graph/tool/mesh/surface/token/`CurrentSlotID` 一次交换；关键失败只发生在 Preflight，失败或取消逐值保留当前会话。
5. `v3-road-graph:8.6` 负责与存档、渲染和 UI 的最终组合验收。
