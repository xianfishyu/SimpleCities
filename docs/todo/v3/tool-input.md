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

- 新增 `Scripts/Road/V3/RoadPlacementSessionV3.cs`：固定拐点 + 当前末端草稿，提交时生成带目标 `RoadType` 的 `RoadBuildRequest`；零长度拐点拒绝，非法类型构造时抛错。
- 新增 7 个 xUnit 用例；完整测试套件 973/973 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：首锚点闭合吸附、自交拒绝、`RoadBuilder` 真实接线与完整工具生命周期。

### 2026-08-13：2.1/2.2 工具状态基础（部分）

- 新增 `Scripts/Road/V3/RoadToolState.cs`：定义 `RoadToolType`（Place/Remove/Upgrade）与当前工具/已选 RoadType 状态，非法类型选择被拒绝。
- 新增 4 个 xUnit 用例；完整测试套件 977/977 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：`RoadBuilder` 真实接线、改造选择生命周期与完整工具路由。

### 2026-08-13：2.2 道路改造选择会话（部分）

- 新增 `Scripts/Road/V3/RoadUpgradeSessionV3.cs`：维护目标 RoadType 与已选 canonical Edge ID 集合，支持选择/取消/清空/一次性提交。
- 新增 6 个 xUnit 用例；完整测试套件 983/983 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：`RoadBuilder` 真实接线、surface hit 选择与批量改造提交。

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
