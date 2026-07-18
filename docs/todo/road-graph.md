# RoadGraph system todo list

> System key: `road-graph`
> Review date: 2026-07-19
> Evidence: `.omo/backups/system-doc-split/docs/todo/todolist.md`, `.omo/evidence/split-system-docs/task-3/ownership-map.json`, current workspace source, and the legacy `docs/todo/todolist.md`.
> Principle: Owns topology, geometry queries, spatial index behavior, public graph API, delete transactions, and the parallel-edge policy.

## Status Summary

| Legacy ID | Finding | Current status | Disposition |
|---|---|---|---|
<a id="road-graph1"></a>
| 1 | `AddRoad` 返回 `-1` 时泄漏副作用 | 已修复 | 补回归测试，不再修改流程 |
<a id="road-graph3"></a>
| 3 | 几何查询仍有全表扫描 | 成立 | 优化候选边查询并建立性能基线 |
<a id="road-graph4"></a>
| 4 | 数据层强制 8 方向 | 成立 | 将方向约束移回 `RoadBuilder` |
<a id="road-graph5"></a>
| 5 | `RemoveEdge` 自动清理节点导致合并补回节点 | 部分成立，属于架构债务 | 在行为测试保护下重构删除事务 |
<a id="road-graph6"></a>
| 6 | `RoadGroup` 在合并时丢失用户操作语义 | 成立 | 禁止跨 Group/Type 自动合并 |
<a id="road-graph7"></a>
| 7 | `FindClosestEdge` 只命中离散采样点 | 成立 | 改为候选筛选 + 点到折线精确距离 |
<a id="road-graph9"></a>
| 9 | `GetNeighborIDs().Distinct()` 隐藏平行边 | 当前为设计选择 | 保留；明确邻居查询与边查询语义 |
<a id="road-graph10"></a>
| 10 | 交点判断使用严格浮点相等 | 成立，低风险 | 统一改用 epsilon 判断 |
<a id="road-graphp1"></a>
| P1 | 图是节点、边和分组的唯一事实来源 | 主体已完成；一致性未自动验证 | 保留为基线；由 0.1、0.5、4.1、4.2 验证和收紧 |
<a id="road-graphp3"></a>
| P3 | SpatialIndex 是可重建查询服务 | 部分完成；尚未表达线段占据范围 | 由 1.2、3.1～3.3 完成真实边查询和局部候选 |
<a id="road-graphp4"></a>
| P4 | 删除操作不触发拓扑修复链 | 未完成 | 由 0.6、4.1、4.3、4.4 移除删除后的自动合并 |
<a id="road-graphp5"></a>
| P5 | 最小化并可验证图不变式 | 部分完成；缺少自动化入口 | 由阶段 0 与阶段 4 建立校验、事务和封装边界 |
<a id="road-graphapi"></a>
| API | 文档定义的公共 `AddEdge` 契约 | 未实现 | 2.4 先验证契约，再实现或明确以文档修订取代 |

### Design Coverage Matrix

| Design scope | Current fact | Related todo or baseline |
|---|---|---|
<a id="road-graph8e20b5c5228b"></a>
| §2 P1、§3 纯图架构、§4 数据结构 | `RoadGraph`、`GraphNode`、`GraphEdge`、`RoadGroup` 已落地；仍需封装可变状态并验证跨容器一致性 | 0.1、0.5、4.1、4.2；已解决基线 |
<a id="road-graphadbff35eb926"></a>
| §2 P3、§5 SpatialIndex | `UniformGrid` 可从图重建，但边只按端点/waypoint 索引，查询仍有全表扫描 | 1.2、3.1～3.3 |
<a id="road-graph5a1f82051412"></a>
| §2 P4、§6.2 删除算法 | 旧位置字典和连通分量拆分已删除，但单边和整组删除仍触发 `TryMergeAtNode` | 0.6、4.1、4.3、4.4 |
<a id="road-graph07fc815075a3"></a>
| §2 P5 不变式最小化 | 旧位置字典不变式已消失；节点邻接、Group、空间引用、事件和存档仍需事务性同步 | 0.4～0.7、4.1～4.4 |
<a id="road-graph6c685ce73a7e"></a>
| §6.1 AddRoad 与交叉/覆盖算法 | 主流程已落地；完整覆盖检查已前置，交叉与 waypoint 拆分已有修复 | 0.2、0.6、2.1～2.4、3.3；已解决基线 |
<a id="road-graph541e1cb3f3d8"></a>
| §6.3、§7 查询和公共 API | 最近节点 API 已有；最近边语义不完整；文档中的公共 `AddEdge` 缺失 | 1.2、2.4、3.2、4.2 |

## Execution Order

### 阶段 0：建立回归保护

<a id="road-graph0.1"></a>
- [ ] **0.1 建立 RoadGraph 自动化测试入口**
  - 当前问题：仓库中尚未发现道路系统自动化测试项目或测试文件；后续行为修改缺少可重复的保护入口。
  - 范围：新增独立测试项目或项目现有工具链认可的 Godot headless 测试入口。
  - 覆盖：纯图逻辑不依赖场景树，可直接创建 `RoadGraph`。
  - 验收：测试可由单条命令运行；失败时返回非零退出码；不依赖人工点击。

  - Source key: `todo:item:0.1`.

<a id="road-graph0.2"></a>
- [ ] **0.2 固化已修复的 `AddRoad` 无副作用行为（原问题 1）**
  - 性质：源码行为已完成，本项仅补自动化回归证据，不重复修改主流程。
  - 当前证据：`Scripts/Road/RoadGraph.cs:48` 在 `ResolveIntersections`、`SplitEdgesAtPathAnchors` 之前执行 `IsPathFullyCovered`。
  - 测试场景：先创建已有道路，再提交完全覆盖路径。
  - 验收：返回 `-1`，且节点、边、Group、ID 分配状态均不变化。

  - Source key: `todo:item:0.2`.

<a id="road-graph0.6"></a>
- [ ] **0.6 固化交叉、waypoint 拆分和删除不自动合并的目标行为**
  - 场景：正交交叉、对角交叉、交点恰好位于 waypoint、单边删除、整组删除。
  - 验收：交点产生唯一节点；边正确拆分；不存在悬空 EdgeRef；删除单边或整组后不创建替代 Edge、不自动合并相邻边，且图不变式成立。

  - Source key: `todo:item:0.6`.

<a id="road-graph0.7"></a>
- [ ] **0.7 明确并固化节点身份吸附半径**
  - 当前问题：`RoadGraph.GetOrCreateNode` 使用私有 `SnapRadius = 0.5f` 将半径内坐标隐式焊接为同一节点，但设计文档的“连续空间”没有定义这一身份规则及多候选选择规则。
  - 测试：距离小于、等于、大于 `0.5f` 的位置；半径内存在多个候选节点；保存加载后在近似位置继续铺路。
  - 验收：节点复用边界被测试锁定；多候选时选择规则明确且稳定；该容差不与 `CellSize` 或 UI 网格吸附混为一谈。
  - 约束：本项先定义并锁定契约，不在缺少行为证据时随意修改 `0.5f`。

  - Source key: `todo:item:0.7`.

### 阶段 1：修复当前语义与交互问题

<a id="road-graph1.1"></a>
- [ ] **1.1 禁止跨 `RoadGroup` 或跨 `RoadType` 自动合并（原问题 6）**
  - 问题：`TryMergeAtNode` 当前用较小 Group ID，并无条件沿用 `edgeA.Type`，见 `Scripts/Road/RoadGraph.cs:625`。
  - 修改：合并前要求 `edgeA.GroupID == edgeB.GroupID` 且 `edgeA.Type == edgeB.Type`；不满足时保留节点和两条边。
  - 测试：同 Group 同 Type 可合并；不同 Group 不合并；不同 Type 不合并。
  - 验收：`RoadGroup` 始终保持“用户一次操作的标签”语义，合并不会让另一个 Group 静默消失，也不会覆盖道路类型。

  - Related refs: `road-graph:1.1`.
  - Source key: `todo:item:1.1`.

<a id="road-graph1.2"></a>
- [ ] **1.2 将 `FindClosestEdge` 改为真实折线距离查询（原问题 7）**
  - 问题：当前只比较端点和 waypoint 的距离，见 `Scripts/Road/RoadGraph.cs:135` 与 `Scripts/Road/RoadGraph.cs:684`。
  - 修改：空间索引只负责收集候选 Edge ID；最终结果按鼠标点到 Edge 完整折线各子段的最小距离排序。
  - 注意：候选查询必须覆盖穿过查询圆但所有采样点都在圆外的线段，不能仅在现有 `EdgePointRef` 查询结果上做精算。
  - 测试：长正交边中点、长对角边中点、折线拐角、两条相邻道路的最近边选择、半径外返回 `null`。
  - 验收：只要可见线段与查询圆相交就能命中，并返回几何距离最近的 Edge。

  - Related refs: `road-graph:3.2`.
  - Source key: `todo:item:1.2`.

<a id="road-graph1.3"></a>
- [ ] **1.3 用 epsilon 替代交点端点的严格相等（原问题 10）**
  - 问题：`TryComputeInteriorCross` 在 `Scripts/Road/RoadGraph.cs:913` 使用 `Vector2 ==` 排除共享端点。
  - 修改：使用统一的距离平方 epsilon 辅助函数判断端点近似重合。
  - 测试：完全相同端点、epsilon 内偏差、epsilon 外的真实内部交叉。
  - 验收：近似共享端点不产生重复交点；真实交叉仍被识别。

  - Source key: `todo:item:1.3`.

### 阶段 2：解除数据层方向约束

<a id="road-graph2.1"></a>
- [ ] **2.1 为任意 R² 折线路径补数据层测试（原问题 4）**
  - 当前问题：`IsPathValid` 在 `Scripts/Road/RoadGraph.cs:500` 调用 `DirectionUtil.FromDisplacementAnyLength`，拒绝非 8 方向线段。
  - 测试：任意角度直线、非 8 方向多段折线、重复点、自相交或回到已有路径点。
  - 验收定义：任意非零角度路径可进入数据层；重复点和明确禁止的退化路径仍被拒绝。

  - Source key: `todo:item:2.1`.

<a id="road-graph2.2"></a>
- [ ] **2.2 从 `RoadGraph.IsPathValid` 移除 8 方向判断**
  - 修改：数据层只校验非零段、重复点及必要的几何不变式；8 方向投影继续由 `RoadBuilder.UpdateProjection` 保证。
  - 验收：`RoadBuilder` 的玩家操作仍保持 8 方向；直接调用 `RoadGraph.AddRoad` 可添加任意角度道路。

  - Source key: `todo:item:2.2`.

<a id="road-graph2.3"></a>
- [ ] **2.3 复核依赖方向枚举的合并逻辑**
  - 问题：`TryMergeAtNode` 在 `Scripts/Road/RoadGraph.cs:617` 仍通过 `DirectionUtil` 判断反向。
  - 修改：改用向量叉积/点积判断两侧是否共线且反向，使合并支持任意角度。
  - 验收：任意角度共线边可按 Group/Type 规则合并；小角度转弯不被误合并。

  - Source key: `todo:item:2.3`.

<a id="road-graph2.4"></a>
- [ ] **2.4 落实设计文档定义的公共 `AddEdge` 契约**
  - 当前问题：`RoadGraph` 只有接收 `GraphNode` 的私有 `AddEdge`；§7.1 定义的 `AddEdge(Vector2, Vector2, Vector2[], int, RoadType)` 尚未提供，边创建规则只能通过 `AddRoad` 间接使用。
  - 修改：先定义有效边、近邻节点复用、自环拒绝、未知 Group、事件、Group 归属和空间索引更新契约；实现统一的公共边创建入口并让内部创建路径复用，或在确认不需要公开原语后由 6.1 明确修订设计文档。
  - 测试：合法边、近节点复用、自环返回 `-1`、未知 Group 策略、`EdgeAdded`、Group membership、节点邻接和空间查询。
  - 验收：边创建只有一条权威写入路径，失败无副作用，成功后字典、邻接、Group、空间索引和事件全部一致；若拒绝该 API，必须有明确架构决定和同步后的文档。

  - Related refs: `road-graph:6.1`.
  - Source key: `todo:item:2.4`.

### 阶段 3：消除几何查询的全图扫描

<a id="road-graph3.1"></a>
- [ ] **3.1 建立当前 `AddRoad` 性能基线（原问题 3）**
  - 当前热点：`CollectExistingSubSegments` 在 `Scripts/Road/RoadGraph.cs:555`、`FindEdgesContainingInteriorPoint` 在 `Scripts/Road/RoadGraph.cs:584`、`FindEdgesWithWaypointAt` 在 `Scripts/Road/RoadGraph.cs:485` 均遍历全部 Edge。
  - 场景：1k、10k、50k Edge 下添加短路、长路、完全覆盖道路和多交叉道路。
  - 验收：记录耗时、候选 Edge 数与全表遍历次数，作为优化前基线。

  - Source key: `todo:item:3.1`.

<a id="road-graph3.2"></a>
- [ ] **3.2 为“线段经过的空间桶”建立候选查询能力**
  - 修改：扩展 `UniformGrid`，让每个 Edge 子线段占据其穿越的全部 bucket，或提供等价可靠的 AABB/线段候选索引；查询结果按 Edge ID 去重，避免使用过大的圆形范围退化成区域全扫。
  - 设计修正：索引必须表达线段占据的桶或提供可靠的线段/AABB 查询，不能仅依赖端点和 waypoint 采样；文档中的 `O(1 + k)` 只可作为受桶数与桶内元素数约束的平均情况，不能作为无条件保证。
  - 测试：一条没有中间 waypoint 的长边被短路从中点穿过；长边跨越多个空桶；不同 bucket size 下重复同一交叉场景。
  - 验收：不会遗漏跨桶长边或其中点交叉；候选数量主要随查询覆盖桶数和局部密度变化，而不是随全图 Edge 总数线性增长。

  - Source key: `todo:item:3.2`.

<a id="road-graph3.3"></a>
- [ ] **3.3 优化覆盖与交点查询**
  - 修改：`IsPathCovered` 仅扫描路径段附近候选边；`FindEdgesContainingInteriorPoint` 使用局部候选；`FindEdgesWithWaypointAt` 直接查询 waypoint 空间引用；AddRoad 的交叉、覆盖、锚点和 waypoint 查询路径不再遍历 `_edges.Values`。
  - 测试：优化前阶段 0 的全部几何场景必须保持一致。
  - 验收：局部短路操作不再调用 `_edges.Values` 全表扫描；性能基线显示增长由全图规模主导转为局部候选规模主导。

  - Source key: `todo:item:3.3`.

### 阶段 4：整理删除与合并事务（原问题 5）

<a id="road-graph4.1"></a>
- [ ] **4.1 为删除过程定义并验证图不变式**
  - 不变式：Edge 两端节点存在；Node EdgeRef 与 `_edges` 双向一致；空间引用与实体一致；空 Group 被清理；提交后无孤立节点。
  - 验收：单删、批量删、拆边、合并和失败路径均通过不变式检查。

  - Source key: `todo:item:4.1`.

<a id="road-graph4.2"></a>
- [ ] **4.2 封闭 `RoadGraph` 的可变内部状态暴露**
  - 当前问题：`GetAllEdges`/`GetAllNodes`/`GetAllGroups` 返回实时字典视图；`GraphEdge.Points` 暴露可原地修改的数组；端点缺失时 `GetFullPath` 返回不完整的 `Points`，会掩盖损坏拓扑。
  - 修改：公共遍历返回稳定快照或不可变快照；Edge 几何对外只读或防御性复制；缺失端点时返回明确失败而不是部分路径。
  - 测试：尝试修改已取得的 Points；取得遍历结果后修改图；构造缺失端点的损坏 Edge。
  - 验收：外部代码不能绕过图 API 改变几何、长度、空间索引或存档内容；图变化不会使既有快照枚举失效；损坏边不会伪装成有效折线。

  - Source key: `todo:item:4.2`.

<a id="road-graph4.3"></a>
- [ ] **4.3 将底层删边与孤立节点清理解耦**
  - 当前症状：`RemoveEdge` 在 `Scripts/Road/RoadGraph.cs:276` 立即删除孤立节点，`TryMergeAtNode` 随后又在 `Scripts/Road/RoadGraph.cs:641` 将远端节点补回。
  - 修改：引入内部“仅断开并删除 Edge”的原语；由顶层操作在事务末尾统一清理孤立节点和空 Group。
  - 验收：`TryMergeAtNode` 不再包含远端节点 revive 逻辑；内部 detach 原语不执行 merge 或孤立节点清理；公开删除操作按文档目标不再自动压缩拓扑。

  - Source key: `todo:item:4.3`.

<a id="road-graph4.4"></a>
- [ ] **4.4 统一单边删除、整组删除、拆分与合并的清理阶段**
  - 修改：所有复合操作显式收集受影响 Node/Group，在操作完成后执行一次清理与不变式验证；从 `RemoveEdge`、`RemoveRoadGroup` 移除自动 `TryMergeAtNode` 调用和依赖 `suppressMerge` 的删除时序。
  - 事件契约：渲染器可继续接收增量事件；未来 `TrafficGraph` 等消费者必须接收事务后事件、批量变更摘要，或有明确且可测试的事件顺序。
  - 验收：不再依赖 `suppressMerge` 触发时序来避免中间状态破坏；事件处理期间查询到的图满足不变式；复合操作不会让外部消费者永久缓存中间拓扑。

  - Source key: `todo:item:4.4`.

### 阶段 6：校准下一代道路设计文档

<a id="road-graph6.1"></a>
- [ ] **6.1 区分历史架构、当前实现与未来路线图**
  - 当前问题：`docs/manuals/road-system-v2-gen.md` 仍将 `RoadNetwork`/`Junction`/`Segment` 到 `RoadGraph`/`GraphNode`/`GraphEdge` 的迁移描述成未来任务，但当前代码已完成主要命名与 SpatialIndex 迁移。
  - 修改：旧结构移入“历史问题”或“迁移记录”；当前状态使用实际类名和 API；增加阶段 A/B/C 对照表，将各迁移交付物标记为已完成、部分完成、活动项、已取代或延期；Phase 6 的 `TrafficGraph`、A*、道路升级工具继续明确标注为未来规划。
  - 验收：读者可明确区分已落地行为、当前技术债和未来功能，不会重复实施已完成迁移。

  - Source key: `todo:item:6.1`.

<a id="road-graph6.2"></a>
- [ ] **6.2 同步当前合并、命中和空间索引语义**
  - 修正：文档明确当前 Add/Remove 都可能触发 `TryMergeAtNode`；`FindClosestEdge` 当前只基于 EdgePoint；`UniformGrid.QueryRadius` 成本取决于覆盖桶数与桶内元素数，Remove 还会扫描桶内 List。
  - 关联：最终语义以阶段 1、3、4 完成后的实现为准，避免先把即将变化的缺陷固化成长期设计。
  - 验收：文档描述可由对应测试或代码位置验证，不再宣称无条件 `O(1)` 删除或 `O(1 + k)` 查询。

  - Source key: `todo:item:6.2`.

## Deferred

<a id="road-graph121c26a6947a"></a>
### 原问题 9：`GetNeighborIDs().Distinct()`

<a id="road-graph31883cfb1c78"></a>
- [ ] **交通模拟设计时明确平行边策略**
  - 当前判断：设计文档 `docs/manuals/road-system-v2-gen.md:233` 明确邻居 ID 应去重，因此当前实现不是偏差。
  - 保留原则：拓扑算法若需要区分平行边，应遍历 `GraphNode.Edges`，而不是修改 `GetNeighborIDs` 的集合语义。
  - 重新开启条件：引入 `TrafficGraph` 时决定是否允许同一节点对之间存在多条 Edge；若禁止，应在 `AddEdge` 增加不变式检查；若允许，应提供显式的 Edge 查询 API。

  - Related refs: `traffic-simulation:P6.1`.
  - Source key: `todo:deferred:31883cfb1c78`.

## Solved Baselines

<a id="road-graphcb2d49752724"></a>
- [x] **原问题 1：覆盖路径检查已前置。** `Scripts/Road/RoadGraph.cs:48`
  - Source key: `todo:baseline:cb2d49752724`.
<a id="road-graphb3bbd674df7c"></a>
- [x] **waypoint 交叉、锚点拆分和 waypoint 精确拆边已有修复。** `Scripts/Road/RoadGraph.cs:401`、`Scripts/Road/RoadGraph.cs:464`、`Scripts/Road/RoadGraph.cs:298`
  - Source key: `todo:baseline:b3bbd674df7c`.
<a id="road-graph4efd03c22f37"></a>
- [x] **P1 主体和纯图数据模型已经落地。** 权威实体位于 `_nodes`、`_edges`、`_groups`；旧位置字典已移除，空间索引可重建。
  - Source key: `todo:baseline:4efd03c22f37`.

## Completion Criteria

<a id="road-graph822fd09c14ca"></a>
- 1. 当前清理里程碑要求阶段 0～6 中保留的活动项全部完成，包括 2.4；RoadType 产品功能 D5.1～D5.3、`P6.*` 与其他需求触发项明确排除，直到满足各自启用条件。
  - Related refs: `road-graph:0.1`, `persistence:0.3`, `grid-rendering:D5.1`, `tool-input:D5.3`, `traffic-simulation:P6.1`.
  - Source key: `todo:completion:822fd09c14ca`.
<a id="road-graph57b3e1c6c3fa"></a>
- 2. 每个行为项先有失败的自动化测试，再做最小实现并通过完整回归；不能仅凭源码检查标记完成。
  - Related refs: `road-graph:0.1`, `persistence:0.5`, `persistence:0.9`.
  - Source key: `todo:completion:57b3e1c6c3fa`.
<a id="road-graph936efe9cdd8b"></a>
- 3. 几何、拓扑、删除事务、存档兼容、`SaveManager` 契约、保存路径边界、整槽加载预检和公共 API 测试全部通过；当前只要求 RoadType 数据/旧存档兼容回归，不要求类型样式或选择 UI。
  - Related refs: `persistence:0.5`, `persistence:0.3`, `persistence:0.9`, `persistence:0.10`, `persistence:0.11`.
  - Source key: `todo:completion:936efe9cdd8b`.
<a id="road-graphc78164d23e9b"></a>
- 4. `dotnet build` 退出码为 0，修改文件无新增诊断；构建成功不能替代自动化或运行时测试证据。
  - Related refs: `road-graph:0.1`, `persistence:0.9`.
  - Source key: `todo:completion:c78164d23e9b`.
<a id="road-graphbfd7554b1b07"></a>
- 6. 10k+ Edge 性能场景有可复现的优化前后数据，且局部查询不再随全图规模线性退化。
  - Related refs: `road-graph:3.1`, `road-graph:3.3`.
  - Source key: `todo:completion:bfd7554b1b07`.
<a id="road-graphcb8d79634afd"></a>
- 7. 文档项必须引用最终代码或测试事实；只有在对应测试和必要的 Godot 运行验证完成后，才能把目标描述为已实现。
  - Related refs: `road-graph:6.1`, `persistence:6.3`.
  - Source key: `todo:completion:cb8d79634afd`.
