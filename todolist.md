# 道路系统待办清单

> 整理日期：2026-07-15
> 依据：当前工作区源码、`docs/manuals/road-system-next-gen.md`、`docs/bugfix/road-graph-post-refactor.md`
> 原则：以当前实现为准；已修复问题只保留为回归基线，不重复进入开发队列。

## 状态总览

| 原编号 | 结论 | 当前状态 | 处置 |
|---|---|---|---|
| 1 | `AddRoad` 返回 `-1` 时泄漏副作用 | 已修复 | 补回归测试，不再修改流程 |
| 2 | 存档丢失 `RoadType` | 已修复；v2 命名迁移未完成 | 补兼容性测试；字段改名延期 |
| 3 | 几何查询仍有全表扫描 | 成立 | 优化候选边查询并建立性能基线 |
| 4 | 数据层强制 8 方向 | 成立 | 将方向约束移回 `RoadBuilder` |
| 5 | `RemoveEdge` 自动清理节点导致合并补回节点 | 部分成立，属于架构债务 | 在行为测试保护下重构删除事务 |
| 6 | `RoadGroup` 在合并时丢失用户操作语义 | 成立 | 禁止跨 Group/Type 自动合并 |
| 7 | `FindClosestEdge` 只命中离散采样点 | 成立 | 改为候选筛选 + 点到折线精确距离 |
| 8 | `RoadBuilder` 仍有半格特殊分支 | 事实成立，但属于 UI 约束 | 当前不改；连续输入需求出现时再设计 |
| 9 | `GetNeighborIDs().Distinct()` 隐藏平行边 | 当前为设计选择 | 保留；明确邻居查询与边查询语义 |
| 10 | 交点判断使用严格浮点相等 | 成立，低风险 | 统一改用 epsilon 判断 |
| 11 | 命名过时且 `RoadType` 视觉样式未落地 | 部分成立 | 优先完成分级样式；命名迁移独立处理 |

## 执行顺序

### 阶段 0：建立回归保护

- [ ] **0.1 建立 RoadGraph 自动化测试入口**
  - 范围：新增独立测试项目或项目现有工具链认可的 Godot headless 测试入口。
  - 覆盖：纯图逻辑不依赖场景树，可直接创建 `RoadGraph`。
  - 验收：测试可由单条命令运行；失败时返回非零退出码；不依赖人工点击。

- [ ] **0.2 固化已修复的 `AddRoad` 无副作用行为（原问题 1）**
  - 当前证据：`Scripts/Road/RoadGraph.cs:48` 在 `ResolveIntersections`、`SplitEdgesAtPathAnchors` 之前执行 `IsPathFullyCovered`。
  - 测试场景：先创建已有道路，再提交完全覆盖路径。
  - 验收：返回 `-1`，且节点、边、Group、ID 分配状态均不变化。

- [ ] **0.3 固化 `RoadType` 存档往返行为（原问题 2）**
  - 当前证据：Edge 与 Group 的类型分别在 `Scripts/Road/RoadGraph.cs:204`、`Scripts/Road/RoadGraph.cs:217` 写入；恢复兼容逻辑位于 `Scripts/Road/RoadGraph.cs:746`。
  - 测试场景：Dirt、Street、Arterial、Highway 分别保存并恢复；另加载无 `Type` 的旧存档。
  - 验收：v2 往返保留全部类型；旧存档稳定回退为 `Street`。

- [ ] **0.4 固化路网与清单的存档版本策略**
  - 当前问题：`RoadGraph` 写出 `version = 2`，manifest 写出 `schemaVersion = 1`，但 `RoadGraph.RestoreState` 和 `SaveManager.Load` 都没有依据版本执行迁移或拒绝加载。
  - 修改：为已知版本建立显式分派；缺少版本的旧存档走兼容路径；未知未来版本必须以可诊断错误失败。
  - 测试：当前 v2 路网、缺少 `version` 的旧存档、未知路网版本、未知 manifest schema。
  - 验收：支持版本走确定的迁移路径；未知不兼容版本不会被静默读取；原问题 2 的旧存档兼容保持不变。

- [ ] **0.5 为 `RoadGraph` 恢复增加引用校验与失败保护**
  - 当前问题：`RestoreState` 在 `Scripts/Road/RoadGraph.cs:228` 先清空当前图，再直接信任存档中的 Node、Edge、Group ID；缺失端点、重复 ID、悬空 Group/Edge 引用没有统一校验。
  - 修改：先反序列化并校验临时数据，全部通过后再替换当前图；失败时保留加载前状态并返回可诊断错误。
  - 校验：所有 Edge 两端节点存在；Group/Edge 双向引用一致；实体 ID 不重复；枚举值合法；`NextID` 大于全部实体 ID。
  - 验收：损坏存档不会产生半恢复图；加载失败后原图的节点、边、Group 和 ID 分配状态不变。

- [ ] **0.6 固化交叉、waypoint 拆分和删除合并行为**
  - 场景：正交交叉、对角交叉、交点恰好位于 waypoint、单边删除、整组删除。
  - 验收：交点产生唯一节点；边正确拆分；不存在悬空 EdgeRef；删除后图不变式成立。

- [ ] **0.7 明确并固化节点身份吸附半径**
  - 当前问题：`RoadGraph.GetOrCreateNode` 使用私有 `SnapRadius = 0.5f` 将半径内坐标隐式焊接为同一节点，但设计文档的“连续空间”没有定义这一身份规则及多候选选择规则。
  - 测试：距离小于、等于、大于 `0.5f` 的位置；半径内存在多个候选节点；保存加载后在近似位置继续铺路。
  - 验收：节点复用边界被测试锁定；多候选时选择规则明确且稳定；该容差不与 `CellSize` 或 UI 网格吸附混为一谈。
  - 约束：本项先定义并锁定契约，不在缺少行为证据时随意修改 `0.5f`。

- [ ] **0.8 为 `SaveManager` 增加注销机制并绑定场景生命周期**
  - 当前问题：`SaveManager.Register` 只登记对象引用，没有 `Unregister`；`RoadSystem._Ready` 每次创建并注册新的 `RoadGraph`，场景重载后可能保留过期 saveable。
  - 修改：增加 `Unregister(ISaveable)`；`RoadSystem`、`MainCamera` 等注册者在退出树时注销对应实例；同一 `SaveFileName` 的重复活动注册需要明确拒绝或替换策略。
  - 测试：连续加载/卸载主场景两次后保存与加载。
  - 验收：注册表只包含当前场景的一份路网和相机；不会重复写同名文件，也不会调用已退出场景的对象。

### 阶段 1：修复当前语义与交互问题

- [ ] **1.1 禁止跨 `RoadGroup` 或跨 `RoadType` 自动合并（原问题 6）**
  - 问题：`TryMergeAtNode` 当前用较小 Group ID，并无条件沿用 `edgeA.Type`，见 `Scripts/Road/RoadGraph.cs:625`。
  - 修改：合并前要求 `edgeA.GroupID == edgeB.GroupID` 且 `edgeA.Type == edgeB.Type`；不满足时保留节点和两条边。
  - 测试：同 Group 同 Type 可合并；不同 Group 不合并；不同 Type 不合并。
  - 验收：`RoadGroup` 始终保持“用户一次操作的标签”语义，合并不会让另一个 Group 静默消失，也不会覆盖道路类型。

- [ ] **1.2 将 `FindClosestEdge` 改为真实折线距离查询（原问题 7）**
  - 问题：当前只比较端点和 waypoint 的距离，见 `Scripts/Road/RoadGraph.cs:135` 与 `Scripts/Road/RoadGraph.cs:684`。
  - 修改：空间索引只负责收集候选 Edge ID；最终结果按鼠标点到 Edge 完整折线各子段的最小距离排序。
  - 注意：候选查询必须覆盖穿过查询圆但所有采样点都在圆外的线段，不能仅在现有 `EdgePointRef` 查询结果上做精算。
  - 测试：长正交边中点、长对角边中点、折线拐角、两条相邻道路的最近边选择、半径外返回 `null`。
  - 验收：只要可见线段与查询圆相交就能命中，并返回几何距离最近的 Edge。

- [ ] **1.3 用 epsilon 替代交点端点的严格相等（原问题 10）**
  - 问题：`TryComputeInteriorCross` 在 `Scripts/Road/RoadGraph.cs:913` 使用 `Vector2 ==` 排除共享端点。
  - 修改：使用统一的距离平方 epsilon 辅助函数判断端点近似重合。
  - 测试：完全相同端点、epsilon 内偏差、epsilon 外的真实内部交叉。
  - 验收：近似共享端点不产生重复交点；真实交叉仍被识别。

### 阶段 2：解除数据层方向约束

- [ ] **2.1 为任意 R² 折线路径补数据层测试（原问题 4）**
  - 当前问题：`IsPathValid` 在 `Scripts/Road/RoadGraph.cs:500` 调用 `DirectionUtil.FromDisplacementAnyLength`，拒绝非 8 方向线段。
  - 测试：任意角度直线、非 8 方向多段折线、重复点、自相交或回到已有路径点。
  - 验收定义：任意非零角度路径可进入数据层；重复点和明确禁止的退化路径仍被拒绝。

- [ ] **2.2 从 `RoadGraph.IsPathValid` 移除 8 方向判断**
  - 修改：数据层只校验非零段、重复点及必要的几何不变式；8 方向投影继续由 `RoadBuilder.UpdateProjection` 保证。
  - 验收：`RoadBuilder` 的玩家操作仍保持 8 方向；直接调用 `RoadGraph.AddRoad` 可添加任意角度道路。

- [ ] **2.3 复核依赖方向枚举的合并逻辑**
  - 问题：`TryMergeAtNode` 在 `Scripts/Road/RoadGraph.cs:617` 仍通过 `DirectionUtil` 判断反向。
  - 修改：改用向量叉积/点积判断两侧是否共线且反向，使合并支持任意角度。
  - 验收：任意角度共线边可按 Group/Type 规则合并；小角度转弯不被误合并。

### 阶段 3：消除几何查询的全图扫描

- [ ] **3.1 建立当前 `AddRoad` 性能基线（原问题 3）**
  - 当前热点：`CollectExistingSubSegments` 在 `Scripts/Road/RoadGraph.cs:555`、`FindEdgesContainingInteriorPoint` 在 `Scripts/Road/RoadGraph.cs:584`、`FindEdgesWithWaypointAt` 在 `Scripts/Road/RoadGraph.cs:485` 均遍历全部 Edge。
  - 场景：1k、10k、50k Edge 下添加短路、长路、完全覆盖道路和多交叉道路。
  - 验收：记录耗时、候选 Edge 数与全表遍历次数，作为优化前基线。

- [ ] **3.2 为“线段经过的空间桶”建立候选查询能力**
  - 修改：扩展 `UniformGrid`，支持按 AABB 或线段穿越桶获取候选引用；避免使用过大的圆形范围退化成区域全扫。
  - 设计修正：索引必须表达线段占据的桶或提供可靠的线段/AABB 查询，不能仅依赖端点和 waypoint 采样；文档中的 `O(1 + k)` 只可作为受桶数与桶内元素数约束的平均情况，不能作为无条件保证。
  - 测试：一条没有中间 waypoint 的长边被短路从中点穿过；长边跨越多个空桶；不同 bucket size 下重复同一交叉场景。
  - 验收：不会遗漏跨桶长边或其中点交叉；候选数量主要随查询覆盖桶数和局部密度变化，而不是随全图 Edge 总数线性增长。

- [ ] **3.3 优化覆盖与交点查询**
  - 修改：`IsPathCovered` 仅扫描路径段附近候选边；`FindEdgesContainingInteriorPoint` 使用局部候选；`FindEdgesWithWaypointAt` 直接查询 waypoint 空间引用。
  - 测试：优化前阶段 0 的全部几何场景必须保持一致。
  - 验收：局部短路操作不再调用 `_edges.Values` 全表扫描；性能基线显示增长由全图规模主导转为局部候选规模主导。

### 阶段 4：整理删除与合并事务（原问题 5）

- [ ] **4.1 为删除过程定义并验证图不变式**
  - 不变式：Edge 两端节点存在；Node EdgeRef 与 `_edges` 双向一致；空间引用与实体一致；空 Group 被清理；提交后无孤立节点。
  - 验收：单删、批量删、拆边、合并和失败路径均通过不变式检查。

- [ ] **4.2 封闭 `RoadGraph` 的可变内部状态暴露**
  - 当前问题：`GetAllEdges`/`GetAllNodes`/`GetAllGroups` 返回实时字典视图；`GraphEdge.Points` 暴露可原地修改的数组；端点缺失时 `GetFullPath` 返回不完整的 `Points`，会掩盖损坏拓扑。
  - 修改：公共遍历返回稳定快照或不可变快照；Edge 几何对外只读或防御性复制；缺失端点时返回明确失败而不是部分路径。
  - 测试：尝试修改已取得的 Points；取得遍历结果后修改图；构造缺失端点的损坏 Edge。
  - 验收：外部代码不能绕过图 API 改变几何、长度、空间索引或存档内容；图变化不会使既有快照枚举失效；损坏边不会伪装成有效折线。

- [ ] **4.3 将底层删边与孤立节点清理解耦**
  - 当前症状：`RemoveEdge` 在 `Scripts/Road/RoadGraph.cs:276` 立即删除孤立节点，`TryMergeAtNode` 随后又在 `Scripts/Road/RoadGraph.cs:641` 将远端节点补回。
  - 修改：引入内部“仅断开并删除 Edge”的原语；由顶层操作在事务末尾统一清理孤立节点和空 Group。
  - 验收：`TryMergeAtNode` 不再包含远端节点 revive 逻辑；公开 `RemoveEdge` 的最终外部行为保持不变。

- [ ] **4.4 统一单边删除、整组删除、拆分与合并的清理阶段**
  - 修改：所有复合操作显式收集受影响 Node/Group，在操作完成后执行一次清理与不变式验证。
  - 事件契约：渲染器可继续接收增量事件；未来 `TrafficGraph` 等消费者必须接收事务后事件、批量变更摘要，或有明确且可测试的事件顺序。
  - 验收：不再依赖 `suppressMerge` 触发时序来避免中间状态破坏；事件处理期间查询到的图满足不变式；复合操作不会让外部消费者永久缓存中间拓扑。

### 阶段 5：完成 RoadType 视觉层（原问题 11）

- [ ] **5.1 在 `RoadConfig` 定义每种 `RoadType` 的样式**
  - 当前问题：`RoadRenderer` 在 `Scripts/Road/RoadRenderer.cs:94` 统一使用 `RoadWidth`/`RoadColor`。
  - 修改：新增可序列化的 `RoadTypeStyle`，至少包含颜色和宽度，并提供缺省回退。
  - 验收：四种 `RoadType` 均能解析到确定样式；配置缺失时不会崩溃。

- [ ] **5.2 让 `RoadRenderer` 按 `edge.Type` 渲染**
  - 修改：`CreateEdgeLine` 使用 `Config.GetStyle(edge.Type)`；悬停高亮继续保持独立样式。
  - 验收：不同类型道路在同一场景中具有可观察的颜色或宽度差异；存档恢复后样式保持一致。

- [ ] **5.3 独立处理 Junction → Node 命名迁移**
  - 范围：`JunctionRadius`/`JunctionColor` 及旧存档字段 `Junctions`、`Segments`、`Roads`。
  - 约束：命名迁移不能破坏旧 `.tres` 与旧存档兼容；必要时保留旧 JSON 字段或提供版本迁移器。
  - 验收：旧存档、旧资源可加载；新代码公共语义统一使用 Node/Edge/Group。

### 阶段 6：校准下一代道路设计文档

- [ ] **6.1 区分历史架构、当前实现与未来路线图**
  - 当前问题：`docs/manuals/road-system-next-gen.md` 仍将 `RoadNetwork`/`Junction`/`Segment` 到 `RoadGraph`/`GraphNode`/`GraphEdge` 的迁移描述成未来任务，但当前代码已完成主要命名与 SpatialIndex 迁移。
  - 修改：旧结构移入“历史问题”或“迁移记录”；当前状态使用实际类名和 API；Phase 6 的 `TrafficGraph`、A*、道路升级工具继续明确标注为未来规划。
  - 验收：读者可明确区分已落地行为、当前技术债和未来功能，不会重复实施已完成迁移。

- [ ] **6.2 同步当前合并、命中和空间索引语义**
  - 修正：文档明确当前 Add/Remove 都可能触发 `TryMergeAtNode`；`FindClosestEdge` 当前只基于 EdgePoint；`UniformGrid.QueryRadius` 成本取决于覆盖桶数与桶内元素数，Remove 还会扫描桶内 List。
  - 关联：最终语义以阶段 1、3、4 完成后的实现为准，避免先把即将变化的缺陷固化成长期设计。
  - 验收：文档描述可由对应测试或代码位置验证，不再宣称无条件 `O(1)` 删除或 `O(1 + k)` 查询。

- [ ] **6.3 同步活动存档 schema 与迁移策略**
  - 当前问题：文档仍以旧 `RoadNetworkData` 为活动格式，而当前路网使用私有 `RoadGraphSaveData` v2，manifest 另有 schema version。
  - 修改：记录当前 v2 字段、旧字段兼容边界、manifest 与路网版本的职责，以及阶段 0.4/0.5 定义的拒绝和迁移规则。
  - 验收：文档中的示例 JSON 和版本分派与实际加载测试一致；旧 DTO 明确标注为遗留结构而非活动序列化入口。

## 暂不执行

### 原问题 8：RoadBuilder 半格分支

- [ ] **需求触发后再重新设计连续输入**
  - 当前判断：半格判断存在于 `RoadBuilder`，没有重新侵入 `RoadGraph`；这符合“离散化属于 UI 层”的核心分层。
  - 暂不修改原因：当前产品交互明确是 8 方向网格铺路，从非格点交叉口限制输入方向属于 UI 规则，不是数据层错误。
  - 重新开启条件：支持自由角度、曲线道路，或产品要求从任意交点向任意方向延伸。

### 原问题 9：`GetNeighborIDs().Distinct()`

- [ ] **交通模拟设计时明确平行边策略**
  - 当前判断：设计文档 `docs/manuals/road-system-next-gen.md:233` 明确邻居 ID 应去重，因此当前实现不是偏差。
  - 保留原则：拓扑算法若需要区分平行边，应遍历 `GraphNode.Edges`，而不是修改 `GetNeighborIDs` 的集合语义。
  - 重新开启条件：引入 `TrafficGraph` 时决定是否允许同一节点对之间存在多条 Edge；若禁止，应在 `AddEdge` 增加不变式检查；若允许，应提供显式的 Edge 查询 API。

## 已解决基线

- [x] **原问题 1：覆盖路径检查已前置。** `Scripts/Road/RoadGraph.cs:48`
- [x] **原问题 2：Edge/Group 的 `RoadType` 已写入并兼容恢复。** `Scripts/Road/RoadGraph.cs:195`、`Scripts/Road/RoadGraph.cs:211`、`Scripts/Road/RoadGraph.cs:738`
- [x] **waypoint 交叉、锚点拆分和 waypoint 精确拆边已有修复。** `Scripts/Road/RoadGraph.cs:401`、`Scripts/Road/RoadGraph.cs:464`、`Scripts/Road/RoadGraph.cs:298`
- [x] **`RemoveRoadGroup` 删除后会执行 merge repair。** `Scripts/Road/RoadGraph.cs:101`

## 完成标准

道路系统清理完成需同时满足：

1. 阶段 0～6 的执行项全部完成，暂不执行项获得明确产品需求后才进入开发。
2. 几何、拓扑、存档兼容与 RoadType 样式测试全部通过。
3. `dotnet build` 退出码为 0，修改文件无新增诊断。
4. 在 Godot 主场景完成铺路、交叉、拆除、保存、加载和类型样式的人工验证。
5. 10k+ Edge 性能场景有优化前后数据，且局部查询不再随全图规模线性退化。
