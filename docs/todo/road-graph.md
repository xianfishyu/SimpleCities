# RoadGraph 系统待办清单

> 系统 key：`road-graph`
> 复核日期：2026-08-03
> 证据：当前工作区源码、RoadGraph 自动化测试及 `docs/manuals/road-system-v2-gen.md` 附录 D。
> 主导原则：负责拓扑、原生曲线几何、空间索引、公共路径 API、删除事务和第二代最终集成验收；不负责 RoadType 或交通模拟。

## 状态总览

| 遗留 ID                    | 发现                                        | 当前状态                                         | 处置方式                                                       |
| -------------------------- | ------------------------------------------- | ------------------------------------------------ | -------------------------------------------------------------- |
| 0.1                        | RoadGraph 自动化测试入口                    | 已完成                                           | xUnit 测试项目已接入解决方案，可由单条`dotnet test` 命令运行 |
| 0.6                        | 交叉、waypoint 拆分和删除不自动合并回归    | 已完成                                           | 五类场景已验证拓扑、Group、空间命中和删除后 Edge ID 单调减少   |
| 0.7                        | 节点身份吸附半径与多候选规则                | 已完成                                           | 半径包含边界；最近优先，近似等距时选择较小 Node ID             |
| <a id="road-graph1"></a>   |                                             |                                                  |                                                                |
| 1                          | `AddRoad` 返回 `-1` 时泄漏副作用        | 已修复并有自动化回归                             | 保持前置覆盖检查，不再修改流程                                 |
| <a id="road-graph3"></a>   |                                             |                                                  |                                                                |
| 3                          | 几何查询仍有全表扫描                        | 成立                                             | 10k Edge 按 60 FPS 硬门槛优化；100k Edge 压力测试              |
| <a id="road-graph4"></a>   |                                             |                                                  |                                                                |
| 4                          | 数据层强制 8 方向                           | 已完成                                           | 2.1、2.2 已验证任意折线和结构化非法路径拒绝                    |
| <a id="road-graph5"></a>   |                                             |                                                  |                                                                |
| 5                          | `RemoveEdge` 自动清理节点导致合并补回节点 | 部分成立，属于架构债务                           | 在行为测试保护下重构删除事务                                   |
| <a id="road-graph6"></a>   |                                             |                                                  |                                                                |
| 6                          | `RoadGroup` 在合并时丢失用户操作语义      | 已修复并有自动化回归                             | 1.1 已锁定 Group 边界；2.7 负责从第二代契约移除 RoadType        |
| <a id="road-graph7"></a>   |                                             |                                                  |                                                                |
| 7                          | `FindClosestEdge` 只命中离散采样点        | 成立                                             | 改为候选筛选 + 点到折线精确距离                                |
| <a id="road-graph9"></a>   |                                             |                                                  |                                                                |
| 9                          | `GetNeighborIDs().Distinct()` 隐藏平行边  | 当前为设计选择                                   | 保留；明确邻居查询与边查询语义                                 |
| <a id="road-graph10"></a>  |                                             |                                                  |                                                                |
| 10                         | 交点判断使用严格浮点相等                    | 已修复并有自动化回归                             | 1.3 统一使用距离平方 epsilon，并保留真实内部交叉                |
| <a id="road-graphp1"></a>  |                                             |                                                  |                                                                |
| P1                         | 图是节点、边和分组的唯一事实来源            | 主体已完成；一致性未自动验证                     | 保留为基线；由 0.1、0.5、4.1、4.2 验证和收紧                   |
| <a id="road-graphp3"></a>  |                                             |                                                  |                                                                |
| P3                         | SpatialIndex 是可重建查询服务               | 部分完成；尚未表达线段占据范围                   | 由 1.2、3.1～3.3 完成真实边查询和局部候选                      |
| <a id="road-graphp4"></a>  |                                             |                                                  |                                                                |
| P4                         | 删除操作不触发拓扑修复链                    | 未完成                                           | 0.6 已锁定外部行为；由 4.1、4.3、4.4 收敛内部删除事务          |
| <a id="road-graphp5"></a>  |                                             |                                                  |                                                                |
| P5                         | 最小化并可验证图不变式                      | 部分完成；自动化入口已建立，图不变式验证仍待补齐 | 由阶段 0 与阶段 4 建立校验、事务和封装边界                     |
| <a id="road-graphapi"></a> |                                             |                                                  |                                                                |
| API                        | 独立于 RoadBuilder 的公共路径提交契约       | RoadPath 请求和完整变更摘要已建立；原生曲线写入尚未实现 | 2.4 继续补曲线路径校验、写入和事件验收            |
| Geometry                   | Edge 已保存原生几何并由新 V2 schema 往返    | 几何族与持久化已完成；曲线拓扑运算未完成                 | 2.5～2.6 继续完成权威拆分、交叉和查询                 |
| RoadType                   | GraphEdge/RoadGroup/API 仍包含类型字段       | 超出第二代范围                                   | 2.7 从 V2 契约移除，第三代重新引入                              |
| V2                         | 完整系统评估                                | 等待全部前置项                                   | 7.1 负责跨图、输入、渲染和存档的最终验收                       |

### 设计覆盖矩阵

| 设计范围                            | 当前事实                                                                                              | 关联待办或基线                      |
| ----------------------------------- | ----------------------------------------------------------------------------------------------------- | ----------------------------------- |
| <a id="road-graph8e20b5c5228b"></a> |                                                                                                       |                                     |
| §2 P1、§3 纯图架构、§4 数据结构  | `RoadGraph`、`GraphNode`、`GraphEdge`、`RoadGroup` 已落地；仍需封装可变状态并验证跨容器一致性 | 0.1、0.5、4.1、4.2；已解决基线      |
| <a id="road-graphadbff35eb926"></a> |                                                                                                       |                                     |
| §2 P3、§5 SpatialIndex            | `UniformGrid` 可从图重建，但边只按端点/waypoint 索引，查询仍有全表扫描                              | 1.2、3.1～3.3                       |
| <a id="road-graph5a1f82051412"></a> |                                                                                                       |                                     |
| §2 P4、§6.2 删除算法              | 旧位置字典和连通分量拆分已删除，但单边和整组删除仍触发`TryMergeAtNode`                              | 0.6、4.1、4.3、4.4                  |
| <a id="road-graph07fc815075a3"></a> |                                                                                                       |                                     |
| §2 P5 不变式最小化                 | 旧位置字典不变式已消失；节点邻接、Group、空间引用、事件和存档仍需事务性同步                           | 0.4～0.7、4.1～4.4                  |
| <a id="road-graph6c685ce73a7e"></a> |                                                                                                       |                                     |
| §6.1 AddRoad 与交叉/覆盖算法       | 主流程已落地；完整覆盖检查已前置，交叉与 waypoint 拆分已有修复                                        | 0.2、0.6、2.1～2.4、3.3；已解决基线 |
| <a id="road-graph541e1cb3f3d8"></a> |                                                                                                       |                                     |
| §6.3、§7 查询和公共 API           | 最近节点 API 已有；最近边语义不完整；文档中的公共`AddEdge` 缺失                                     | 1.2、2.4、3.2、4.2                  |
| 附录 D 原生曲线与 V2 API           | Edge 与存档已保留原生几何；提交、交叉、查询、删除等拓扑算法仍按折线路径工作                           | 2.4～2.7、3.1～3.3、4.1～4.4       |

## 执行顺序

### 阶段 0：建立回归保护

<a id="road-graph0.1"></a>

- [X] **0.1 建立 RoadGraph 自动化测试入口**
  - 当前问题：仓库中尚未发现道路系统自动化测试项目或测试文件；后续行为修改缺少可重复的保护入口。
  - 范围：新增独立测试项目或项目现有工具链认可的 Godot headless 测试入口。
  - 覆盖：纯图逻辑不依赖场景树，可直接创建 `RoadGraph`。
  - 验收：测试可由单条命令运行；失败时返回非零退出码；不依赖人工点击。
  - 完成证据（2026-07-22）：`tests/SimpleCities.RoadGraph.Tests/SimpleCities.RoadGraph.Tests.csproj` 已接入 `SimpleCities.sln` 并引用真实主工程；`RoadGraphSmokeTests.Constructor_DoesNotRequireSceneTree` 直接构造 `RoadGraph`，`RoadGraphSmokeTests.NewGraph_HasNoEntities` 验证空图。`dotnet test tests/SimpleCities.RoadGraph.Tests/SimpleCities.RoadGraph.Tests.csproj --configuration Debug` 与 `dotnet test SimpleCities.sln --configuration Debug --no-restore` 均执行 2 个测试且 0 失败、0 跳过；临时失败探针使同一测试命令以退出码 1 结束，删除探针后恢复全绿；`dotnet build SimpleCities.sln --configuration Debug` 为 0 警告、0 错误。
  - 来源 key：`todo:item:0.1`。

<a id="road-graph0.2"></a>

- [X] **0.2 固化已修复的 `AddRoad` 无副作用行为（原问题 1）**
  - 性质：源码行为已完成，本项仅补自动化回归证据，不重复修改主流程。
  - 当前证据：`Scripts/Road/RoadGraph.cs:48` 在 `ResolveIntersections`、`SplitEdgesAtPathAnchors` 之前执行 `IsPathFullyCovered`。
  - 测试场景：先创建已有道路，再提交完全覆盖路径。
  - 验收：返回 `-1`，且节点、边、Group、ID 分配状态均不变化。
  - 完成证据（2026-07-22）：`RoadGraphCoverageTests` 覆盖完全重复路径、带内部锚点的完全覆盖路径及拒绝后继续铺路的控制图对比；三个场景均断言返回 `-1`，并通过 `CaptureState()` 全量比较节点、边、Group 与 `nextID`。临时移除 `AddRoad` 的前置覆盖检查后，锚点场景和 ID 场景按预期失败（`nextID` 分别从 `4` 偏至 `13`、从 `8` 偏至 `17`，测试命令退出码 1）；恢复生产代码后聚焦测试 3/3 通过。
  - 来源 key：`todo:item:0.2`。

<a id="road-graph0.6"></a>

- [X] **0.6 固化交叉、waypoint 拆分和删除不自动合并的目标行为**
  - 场景：正交交叉、对角交叉、交点恰好位于 waypoint、单边删除、整组删除。
  - 验收：交点产生唯一节点；边正确拆分；不存在悬空 EdgeRef；删除单边或整组后不创建替代 Edge、不自动合并相邻边，且图不变式成立。
  - 完成证据（2026-08-02）：`RoadGraphRegressionTests` 的五类目标场景均调用共享 `AssertGraphInvariants`，显式验证 Edge 两端节点、Node EdgeRef 双向关系、Group 与 Edge 归属、无孤立节点，并通过 `FindClosestNode`/`FindClosestEdge` 验证存活实体仍可由空间索引命中；两个删除场景还断言删除后所有 Edge ID 都来自操作前集合，被删 Edge 不再存在，因此没有自动合并产生的替代 Edge。聚焦测试 17/17 通过，解决方案测试 52/52 通过，`dotnet build SimpleCities.sln --configuration Debug --no-restore` 为 0 警告、0 错误。完整删除事务级不变式仍由 `road-graph:4.1`～`road-graph:4.4` 负责。
  - 来源 key：`todo:item:0.6`。

<a id="road-graph0.7"></a>

- [X] **0.7 明确并固化节点身份吸附半径**
  - 当前问题：`RoadGraph.GetOrCreateNode` 使用私有 `SnapRadius = 0.5f` 将半径内坐标隐式焊接为同一节点，但设计文档的“连续空间”没有定义这一身份规则及多候选选择规则。
  - 测试：距离小于、等于、大于 `0.5f` 的位置；半径内存在多个候选节点；保存加载后在近似位置继续铺路。
  - 验收：节点复用边界被测试锁定；多候选时选择规则明确且稳定；该容差不与 `CellSize` 或 UI 网格吸附混为一谈。
  - 约束：本项先定义并锁定契约，不在缺少行为证据时随意修改 `0.5f`。
  - 完成证据（2026-08-02）：`FindClosestNode` 与 `GetOrCreateNode` 统一使用 `FindClosestIndexedNode`；`SnapRadius = 0.5f` 保持为 RoadGraph 连续空间中的节点身份容差，半径边界包含在内，多候选先按几何距离选择，`Mathf.IsEqualApprox` 判定等距时选择较小 Node ID。`RoadGraphNodeIdentityTests` 覆盖边界内、边界上、边界外、双候选最近节点、反转存档节点恢复顺序后的等距决胜，以及恢复后继续近点铺路；修复前 7 项中 3 项失败，修复后聚焦测试 7/7、解决方案测试 59/59 通过，`dotnet build SimpleCities.sln --configuration Debug --no-restore` 为 0 警告、0 错误。
  - 来源 key：`todo:item:0.7`。

### 阶段 1：修复当前语义与交互问题

<a id="road-graph1.1"></a>

- [X] **1.1 禁止跨 RoadGroup 自动合并（原问题 6）**
  - 问题：自动合并不能破坏 RoadGroup 表示“一次用户提交”的语义；RoadType 已移至第三代，不再决定 V2 合并行为。
  - 修改：只有同一 Group 内满足几何连续条件的边才可合并；不同 Group 始终保留独立边和节点语义。
  - 测试：同 Group 共线可合并；同类型或不同类型的不同 Group 均不合并。RoadType 从第二代契约移除后的回归由 `road-graph:2.7` 负责。
  - 验收：RoadGroup 始终保持用户提交边界，不会因自动合并静默消失。
  - 完成证据（2026-08-02）：`TryMergeAtNode` 在几何合并前要求两条 Edge 的 `GroupID` 相同；`RoadGraphRegressionTests.AddRoad_CollinearSameTypeRoadsFromSeparateOperations_PreserveBothGroups` 隔离验证相同 RoadType 不会掩盖 Group 边界，原有不同 Group/Type 场景和 `AddRoad_ArbitraryAngleCollinearSegments_MergeWithinTheSameGroup` 分别验证不同 Group 保留及同 Group 合并。聚焦回归 18/18、解决方案测试 60/60 通过，`dotnet build SimpleCities.sln --configuration Debug --no-restore` 为 0 警告、0 错误。
  - 关联引用：`road-graph:1.1`。
  - 来源 key：`todo:item:1.1`。

<a id="road-graph1.2"></a>

> 当前进展（2026-08-01）：`RoadGraphRegressionTests.FindClosestEdge_LongStraightEdge_HitsItsMiddle`、`FindClosestEdge_LongDiagonalEdge_HitsItsMiddle`、`FindClosestEdge_ChoosesTheGeometricallyNearestCandidate`、`FindClosestEdge_EdgeAtRadiusBoundary_IsIncluded` 与 `FindClosestEdge_OutsideRadius_ReturnsNull` 已通过；正交和对角长边中点均可命中，重叠查询候选会选择几何距离最近的边，半径外返回 `null`。折线拐角场景仍待补齐。

- [ ] **1.2 将 FindClosestEdge 改为权威几何距离查询（原问题 7）**
  - 问题：当前只比较折线端点和 waypoint；第二代还必须命中原生曲线的中段和高曲率区域。
  - 修改：空间索引只收集候选 Edge，最终结果按 2.5/2.6 的权威几何最近点计算排序，不使用显示采样作为真相。
  - 测试：长直线中点、折线拐角、Bézier/样条/圆弧/回旋线中段、相邻道路选择、半径边界和半径外返回 null。
  - 验收：只要权威道路几何与查询圆相交就能命中，并返回几何距离最近的 Edge。
  - 关联引用：`road-graph:2.5`、`road-graph:2.6`、`road-graph:3.2`。
  - 来源 key：`todo:item:1.2`。

<a id="road-graph1.3"></a>

- [X] **1.3 用 epsilon 替代交点端点的严格相等（原问题 10）**
  - 问题：`TryComputeInteriorCross` 使用 `Vector2 ==` 排除共享端点，无法识别几何 epsilon 内的近似共享端点。
  - 修改：使用统一的距离平方 epsilon 辅助函数判断端点近似重合。
  - 测试：完全相同端点、epsilon 内偏差、epsilon 外的真实内部交叉。
  - 验收：近似共享端点不产生重复交点；真实交叉仍被识别。
  - 完成证据（2026-08-02）：`TryComputeInteriorCross` 通过 `ArePositionsApproximatelyEqual` 对四种端点组合执行 `DistanceSquaredTo < GeometryEpsilon` 判断。`AddRoad_EndpointWithinGeometryEpsilon_DoesNotSplitExistingEdge` 验证完全相同和偏移 `0.005f` 的端点不会重建既有 Edge；`AddRoad_IntersectionOutsideEndpointEpsilon_CreatesFourWayNode` 验证远离端点的真实交叉仍产生四连接节点。修复前聚焦回归 21 项中 1 项失败，修复后 21/21、解决方案测试 63/63 通过，`dotnet build SimpleCities.sln --configuration Debug --no-restore` 为 0 警告、0 错误。
  - 来源 key：`todo:item:1.3`。

### 阶段 2：解除数据层方向约束

<a id="road-graph2.1"></a>

- [X] **2.1 为任意 R² 折线路径补数据层测试（原问题 4）**
  - 当前问题：AddRoad 已能接受任意角度直线和非 8 方向多段折线，但原实现未统一校验重复点、自相交、回到已有路径点和非有限坐标，数据层有效路径契约不清晰。
  - 测试：任意角度直线、非 8 方向多段折线、重复点、自相交、回到已有路径点和非有限坐标。
  - 验收定义：任意非零角度路径可进入数据层；重复点和明确禁止的退化路径以结构化原因拒绝。
  - 完成证据（2026-08-02）：新增不含网格参数的 `SubmitPolyline` 与 `RoadPathSubmissionResult`，在任何写图前区分点数不足、非有限坐标、几何退化、`SnapRadius` 节点身份塌缩、重复点、自交/相邻折返和完整覆盖；旧 `AddRoad` 作为兼容适配器复用同一校验与写入链。`RoadGraphContinuousSpaceTests` 继续验证任意角度直线和多段折线，`RoadGraphPathSubmissionTests` 19/19 验证结构化结果、失败前后完整状态相等、失败不触发 Edge 事件及兼容返回值；解决方案测试 85/85 通过，构建 0 警告、0 错误。原生曲线请求和完整变更摘要仍由 `road-graph:2.4` 负责。
  - 关联引用：`road-graph:2.4`。
  - 来源 key：`todo:item:2.1`。

<a id="road-graph2.2"></a>

- [X] **2.2 从 `RoadGraph.IsPathValid` 移除 8 方向判断**
  - 修改：删除或重写未使用的 IsPathValid，使数据层只校验有限数值、非零段、重复点及必要几何不变式；网格投影完全由可替换输入策略负责。
  - 验收：RoadGraph 不引用 Direction/DirectionUtil/GridSystem/CellSize；当前米字型玩法由 `tool-input:1.2` 保持，公共路径 API 可添加任意角度和原生曲线。
  - 完成证据（2026-08-02）：删除未被调用的私有 `IsPathValid` 后，`Scripts/Road/RoadGraph.cs` 对 `Direction`、`DirectionUtil`、`GridSystem`、`CellSize` 和 `IsPathValid` 的扫描均无命中；`RoadGraphContinuousSpaceTests` 通过源码契约测试锁定该分层边界，并以任意角度直线和非 8 方向多段折线验证数据层正向行为。聚焦测试 3/3、解决方案测试 66/66 通过，`dotnet build SimpleCities.sln --configuration Debug --no-restore` 为 0 警告、0 错误。非法路径的结构化拒绝仍由 `road-graph:2.1`、`road-graph:2.4` 负责。
  - 来源 key：`todo:item:2.2`。

<a id="road-graph2.3"></a>

> 已完成（2026-08-01）：`RoadGraphRegressionTests.AddRoad_ArbitraryAngleCollinearSegments_MergeWithinTheSameGroup` 与 `AddRoad_SlightlyBentSegments_DoNotMergeAtTheBend` 已通过；同一 Group/Type 的任意角度共线边会合并，0.01f 偏移的浅角转弯保留两个边段。

- [x] **2.3 复核依赖方向枚举的合并逻辑**
  - 问题：`TryMergeAtNode` 在 `Scripts/Road/RoadGraph.cs:617` 仍通过 `DirectionUtil` 判断反向。
  - 修改：改用向量叉积/点积判断两侧是否共线且反向，使合并支持任意角度。
  - 验收：任意角度共线边可按 Group/Type 规则合并；小角度转弯不被误合并。
  - 验证：`dotnet test tests/SimpleCities.RoadGraph.Tests/SimpleCities.RoadGraph.Tests.csproj --no-restore`，2026-08-01，46 通过、0 失败。
  - 来源 key：`todo:item:2.3`。

<a id="road-graph2.4"></a>

- [ ] **2.4 提供独立于 RoadBuilder 的公共路径提交 API**
  - 当前问题：外部调用方只能使用面向当前折线实现的 `AddRoad(start, end, waypoints, type)`，没有统一的原生几何路径请求和结构化失败结果。
  - 修改：定义不含网格或 RoadType 的 `RoadPath`/`RoadGeometrySegment` 请求，以及包含成功状态、创建实体、变更摘要和错误原因的结果；内部边创建继续只有一条权威写入路径。
  - 依赖：`road-graph:2.5`。
  - 集成负责人：`road-graph`。
  - 测试：合法直线/曲线路径、近节点复用、自环、退化段、未知几何类型、重复覆盖、交叉、事件和失败原子性。
  - 验收：测试、未来工具和输入策略可直接提交任意合法路径；失败无副作用且原因可诊断，成功后拓扑、Group、空间索引和事件一致。
  - 当前进展（2026-08-03）：`SubmitPolyline(IReadOnlyList<Vector2>)` 已提供无 RoadType/网格参数的结构化折线入口，覆盖非法输入与完整覆盖的无副作用拒绝，并让旧 `AddRoad` 复用权威写入链。新增不可变 `RoadPath` 原生段请求快照；`RoadPathSubmissionResult` 改为只能通过内部工厂创建的 sealed record class，并以 `RoadGraphChangeSummary` 返回确定排序的创建/删除 Node、Edge、Group ID。现有折线提交按操作前后权威实体集合计算摘要，交叉拆边和合并产生的替换实体不会遗漏；失败结果始终带非空且无变化的摘要。路径提交与拓扑聚焦测试 47/47、解决方案测试 192/192、构建 0 警告/0 错误。仍缺 `SubmitPath(RoadPath)` 的原生曲线校验/写入、未知类型实际拒绝，以及曲线成功事件一致性验收，本项保持开放。
  - 关联引用：`tool-input:1.2`、`road-graph:6.1`。
  - 来源 key：`todo:item:2.4`。

<a id="road-graph2.5"></a>

- [ ] **2.5 建立保留真实语义的原生曲线几何模型**
  - 当前问题：GraphEdge 只保存 waypoint 数组，曲线只能被预先离散为折线，无法恢复控制点、曲率或缓和曲线参数。
  - 修改：定义可序列化的直线、Bézier、样条、圆弧/圆锥曲线和铁路常用缓和曲线段，缓和曲线至少包含回旋线/clothoid；每种段提供参数域、位置、切线、包围盒、长度和无损拆分契约。显示采样不得成为权威数据。
  - 测试：各几何段构造、有限参数校验、端点/切线连续性、长度、包围盒、拆分后重组等价和序列化往返。
  - 验收：GraphEdge 保存曲线类型与控制参数；捕获、恢复、拆分和重建后保持同一几何语义，而非只保留采样点。
  - 当前进展（2026-08-03）：新增统一 `RoadGeometrySegment` 契约及 line、cubic Bézier、cubic Hermite、circular arc、clothoid 和 rational quadratic 实现，覆盖附录 D 要求的直线、Bézier、样条、圆弧/一般圆锥曲线和铁路回旋线。所有类型统一使用 `[0, 1]` 参数域并提供位置、单位切线、正向包围盒、长度和开放区间同类型拆分；权威数据不包含显示采样。rational quadratic 以三个控制点和三个正齐次权重表达一般圆锥曲线，解析求导数极值包围盒，以正权重凸包上下界计算长度，并通过齐次 De Casteljau 保持拆分闭包。clothoid 以弧长和线性曲率保存缓和曲线语义，一般位置使用自适应 Simpson 积分。`RoadGeometryData` 与 `RoadGeometrySerializer` 以显式版本和稳定判别字段往返全部类型，并在构造运行时对象前结构化拒绝无效数据；`GraphEdge.GeometrySegments` 以只读连续段列表保存权威运行时几何。新 `RoadGraph` V2 schema 已直接捕获每个 Edge 的原生几何段，并在严格预检后恢复；六类类型和控制参数完成双重往返，未知类型、非法参数、段不连续和节点端点不一致均安全拒绝。全部几何聚焦测试 103/103，最新解决方案测试 204/204、构建 0 警告/0 错误，Godot 主场景保存加载契约通过。存档缺口已关闭；RoadGraph 的交叉、拆分和重建路径仍未原生处理曲线，因此本项保持开放并继续由 `road-graph:2.6` 集成。

<a id="road-graph2.6"></a>

- [ ] **2.6 让拓扑操作完整支持原生曲线**
  - 当前问题：覆盖、最近点、交叉、锚点插入和拆边全部按直线子段运算，无法正确处理原生曲线。
  - 修改：基于 2.5 的统一几何接口实现曲线最近点、点上判定、曲线/曲线交点、重叠、切触和参数位置拆分；同一二维平面内的几何交叉一律创建拓扑节点。
  - 依赖：`road-graph:2.5`、`road-graph:1.3`。
  - 集成负责人：`road-graph`。
  - 测试：直线-曲线、Bézier-Bézier、样条、圆弧/圆锥曲线和回旋线等缓和曲线的端点接触、内部交叉、多交点、相切、重叠与无交叉。
  - 验收：交叉节点位置和拆分后的子曲线参数稳定；桥梁、隧道和高程不在本项建模，二维交叉始终连接。

<a id="road-graph2.7"></a>

- [ ] **2.7 从第二代 RoadGraph 契约移除 RoadType**
  - 当前问题：GraphEdge、RoadGroup、AddRoad、合并和保存均携带 RoadType，但道路分级已明确属于第三代。
  - 修改：第二代图实体、公共路径 API、合并规则、事件和存档不依赖 RoadType；第三代通过新契约和 schema 版本重新引入分级。
  - 依赖：`road-graph:2.4`。
  - 集成负责人：`road-graph`。
  - 测试：无类型参数的新增、交叉、合并、拆分、删除和保存加载；搜索第二代运行路径不再存在类型分支。
  - 验收：RoadType 不再是 V2 图状态或行为的一部分；移除后既有拓扑和统一视觉继续工作。

### 阶段 3：消除几何查询的全图扫描

<a id="road-graph3.1"></a>

- [ ] **3.1 建立当前 `AddRoad` 性能基线（原问题 3）**
  - 当前热点：`CollectExistingSubSegments` 在 `Scripts/Road/RoadGraph.cs:555`、`FindEdgesContainingInteriorPoint` 在 `Scripts/Road/RoadGraph.cs:584`、`FindEdgesWithWaypointAt` 在 `Scripts/Road/RoadGraph.cs:485` 均遍历全部 Edge。
  - 场景：1k、10k 和 100k Edge 下添加短路、长路、原生曲线、完全覆盖道路和多交叉道路，并执行命中和删除。
  - 指标：记录平均/P95 操作耗时、候选 Edge 数、全表遍历次数和分配量；10k 场景的交互操作 P95 不超过 16.67 ms，100k 只记录压力测试结果。
  - 验收：形成固定数据集和可重复命令，明确优化前基线；10k 满足 60 FPS 硬门槛，100k 结果不阻塞完成。
  - 来源 key：`todo:item:3.1`。

<a id="road-graph3.2"></a>

> 当前进展（2026-08-01）：`RoadGraphRegressionTests.AddRoad_CrossingLongUnsegmentedEdge_CreatesConnectedIntersectionNode` 与 `AddRoad_CrossingLongEdge_RemainsConnectedAcrossIndexBucketSizes` 已通过，作为长边中点候选检索的回归基线；在 8、64 和 256 单位 bucket 中，交叉均创建四连接节点。候选数量与局部密度的性能测量仍待补充。

- [ ] **3.2 为直线和原生曲线建立完整空间占据索引**
  - 修改：索引每个几何段的保守包围范围或自适应子区间，使直线、Bézier、样条、圆弧/圆锥曲线和回旋线等缓和曲线覆盖其穿越的全部 bucket；查询结果按 Edge ID 去重。
  - 设计修正：索引只能作为可重建候选服务，不能以采样点代替权威曲线；文档中的复杂度必须受查询桶数、局部密度和曲线细分上界约束。
  - 依赖：`road-graph:2.5`～`road-graph:2.6`。
  - 测试：跨多个空桶的长直线和大曲率曲线、包围盒边界、不同 bucket size、多曲线重叠候选和索引重建。
  - 验收：不会遗漏曲线中段交叉或命中；候选数量主要随覆盖范围和局部密度变化，而不是随全图 Edge 总数线性增长。
  - 来源 key：`todo:item:3.2`。

<a id="road-graph3.3"></a>

- [ ] **3.3 优化覆盖与交点查询**
  - 修改：覆盖、最近点、交点、锚点和拆分查询只精确计算 3.2 返回的候选；直线与曲线共享同一局部查询入口，不再遍历 `_edges.Values`。
  - 测试：阶段 0 和 2.6 的全部几何场景在优化前后结果一致，并在 10k/100k 固定数据集重复测量。
  - 验收：局部操作不执行全图 Edge 扫描；10k 满足 60 FPS 单帧预算，100k 压测记录候选规模和耗时。
  - 来源 key：`todo:item:3.3`。

### 阶段 4：整理删除与合并事务（原问题 5）

<a id="road-graph4.1"></a>

- [ ] **4.1 为删除过程定义并验证图不变式**
  - 不变式：Edge 两端节点存在；Node EdgeRef 与 `_edges` 双向一致；空间引用与实体一致；空 Group 被清理；提交后无孤立节点。
  - 验收：单删、批量删、拆边、合并和失败路径均通过不变式检查。
  - 来源 key：`todo:item:4.1`。

<a id="road-graph4.2"></a>

> 当前进展（2026-08-01）：`RoadGraphRegressionTests.GraphEdgePoints_CannotMutateGraphStateOutsideRoadGraphApi` 已通过；外部修改取得的 `Points` 数组不会改变图或存档状态。

- [ ] **4.2 封闭 `RoadGraph` 的可变内部状态暴露**
  - 当前问题：`GetAllEdges`/`GetAllNodes`/`GetAllGroups` 返回实时字典视图；`GraphEdge.Points` 暴露可原地修改的数组；端点缺失时 `GetFullPath` 返回不完整的 `Points`，会掩盖损坏拓扑。
  - 修改：公共遍历返回稳定快照或不可变快照；Edge 几何对外只读或防御性复制；缺失端点时返回明确失败而不是部分路径。
  - 测试：尝试修改已取得的 Points；取得遍历结果后修改图；构造缺失端点的损坏 Edge。
  - 验收：外部代码不能绕过图 API 改变几何、长度、空间索引或存档内容；图变化不会使既有快照枚举失效；损坏边不会伪装成有效折线。
  - 来源 key：`todo:item:4.2`。

<a id="road-graph4.3"></a>

- [ ] **4.3 将底层删边与孤立节点清理解耦**
  - 当前症状：`RemoveEdge` 在 `Scripts/Road/RoadGraph.cs:276` 立即删除孤立节点，`TryMergeAtNode` 随后又在 `Scripts/Road/RoadGraph.cs:641` 将远端节点补回。
  - 修改：引入内部“仅断开并删除 Edge”的原语；由顶层操作在事务末尾统一清理孤立节点和空 Group。
  - 验收：`TryMergeAtNode` 不再包含远端节点 revive 逻辑；内部 detach 原语不执行 merge 或孤立节点清理；公开删除操作按文档目标不再自动压缩拓扑。
  - 来源 key：`todo:item:4.3`。

<a id="road-graph4.4"></a>

- [ ] **4.4 统一单边删除、整组删除、拆分与合并的清理阶段**
  - 修改：所有复合操作显式收集受影响 Node/Group，在操作完成后执行一次清理与不变式验证；从 `RemoveEdge`、`RemoveRoadGroup` 移除自动 `TryMergeAtNode` 调用和依赖 `suppressMerge` 的删除时序。
  - 事件契约：渲染器可继续接收增量事件；未来 `TrafficGraph` 等消费者必须接收事务后事件、批量变更摘要，或有明确且可测试的事件顺序。
  - 验收：不再依赖 `suppressMerge` 触发时序来避免中间状态破坏；事件处理期间查询到的图满足不变式；复合操作不会让外部消费者永久缓存中间拓扑。
  - 来源 key：`todo:item:4.4`。

### 阶段 6：校准下一代道路设计文档

<a id="road-graph6.1"></a>

- [ ] **6.1 区分历史架构、当前实现与未来路线图**
  - 当前问题：`docs/manuals/road-system-v2-gen.md` 仍将 `RoadNetwork`/`Junction`/`Segment` 到 `RoadGraph`/`GraphNode`/`GraphEdge` 的迁移描述成未来任务，但当前代码已完成主要命名与 SpatialIndex 迁移。
  - 修改：旧结构移入“历史问题”或“迁移记录”；当前状态使用实际类名和 API；增加阶段 A/B/C 对照表，将各迁移交付物标记为已完成、部分完成、活动项、已取代或延期；Phase 6 的 `TrafficGraph`、A*、道路升级工具继续明确标注为未来规划。
  - 验收：读者可明确区分已落地行为、当前技术债和未来功能，不会重复实施已完成迁移。
  - 来源 key：`todo:item:6.1`。

<a id="road-graph6.2"></a>

- [ ] **6.2 同步当前合并、命中和空间索引语义**
  - 修正：文档明确当前 Add/Remove 都可能触发 `TryMergeAtNode`；`FindClosestEdge` 当前只基于 EdgePoint；`UniformGrid.QueryRadius` 成本取决于覆盖桶数与桶内元素数，Remove 还会扫描桶内 List。
  - 关联：最终语义以阶段 1～4 完成后的实现为准，并同步原生曲线、可替换输入、10k/100k 性能和 RoadType 移出 V2 的决定。
  - 验收：文档描述可由对应测试或代码位置验证，不再宣称无条件 `O(1)` 删除或 `O(1 + k)` 查询。
  - 来源 key：`todo:item:6.2`。

### 阶段 7：第二代完整系统评估

<a id="road-graph7.1"></a>

- [ ] **7.1 完成第二代道路系统端到端评估**
  - 当前问题：单项自动化通过不足以证明铺路、曲线、删除、撤销重做、渲染和多个命名存档在真实场景中共同成立。
  - 修改：在所有所属系统条目完成后执行一次完整评估，并将结果永久记录在 `docs/manuals/road-system-v2-gen.md` 附录 D；不删除该附录。
  - 依赖：`road-graph:0.6`～`road-graph:6.2` 中的活动项、`tool-input:1.2`～`tool-input:1.6`、`grid-rendering:1.1`～`grid-rendering:1.2`、`save-system:0.3`～`save-system:1.4`、`save-system:5.3`、`save-system:6.3`。
  - 集成负责人：`road-graph`。
  - 验证：运行完整自动化、dotnet build、Godot 运行时场景、10k 硬门槛和 100k 压测；在主场景手工验证连续铺路、全部原生曲线、二维交叉、单删/连续删/框选删、撤销重做、命名保存、另存为、覆盖、删除、自动存档和损坏加载保护。
  - 验收：所有硬性场景通过且证据已记录；100k 只要求压测结果；RoadType、交通模拟、高程道路和旧存档兼容不参与第二代完成判定。

## 暂不执行

<a id="road-graph121c26a6947a"></a>

### 原问题 9：`GetNeighborIDs().Distinct()`

<a id="road-graph31883cfb1c78"></a>

- [ ] **交通模拟设计时明确平行边策略**
  - 当前判断：设计文档 `docs/manuals/road-system-v2-gen.md:233` 明确邻居 ID 应去重，因此当前实现不是偏差。
  - 保留原则：拓扑算法若需要区分平行边，应遍历 `GraphNode.Edges`，而不是修改 `GetNeighborIDs` 的集合语义。
  - 重新开启条件：引入 `TrafficGraph` 时决定是否允许同一节点对之间存在多条 Edge；若禁止，应在 `AddEdge` 增加不变式检查；若允许，应提供显式的 Edge 查询 API。
  - 关联引用：`traffic-simulation:P6.1`。
  - 来源 key：`todo:deferred:31883cfb1c78`。

## 已解决基线

<a id="road-graphcb2d49752724"></a>

- [X] **原问题 1：覆盖路径检查已前置。** `Scripts/Road/RoadGraph.cs:48`
  - 来源 key：`todo:baseline:cb2d49752724`。
    <a id="road-graphb3bbd674df7c"></a>
- [X] **waypoint 交叉、锚点拆分和 waypoint 精确拆边已有修复。** `Scripts/Road/RoadGraph.cs:401`、`Scripts/Road/RoadGraph.cs:464`、`Scripts/Road/RoadGraph.cs:298`
  - 来源 key：`todo:baseline:b3bbd674df7c`。
    <a id="road-graph4efd03c22f37"></a>
- [X] **P1 主体和纯图数据模型已经落地。** 权威实体位于 `_nodes`、`_edges`、`_groups`；旧位置字典已移除，空间索引可重建。
  - 来源 key：`todo:baseline:4efd03c22f37`。

- [X] **RoadGraph 数据层不依赖输入层的方向或网格概念。** `RoadGraphContinuousSpaceTests` 锁定源码依赖边界并验证任意角度直线和折线；`RoadGraphPathSubmissionTests` 锁定非法折线路径的结构化、无副作用拒绝。
  - 来源 key：`todo:baseline:continuous-space`。

## 完成标准

<a id="road-graph822fd09c14ca"></a>
<a id="road-graph57b3e1c6c3fa"></a>
<a id="road-graph936efe9cdd8b"></a>
<a id="road-graphc78164d23e9b"></a>
<a id="road-graphbfd7554b1b07"></a>
<a id="road-graphcb8d79634afd"></a>

1. RoadGraph 所有活动项以及 `tool-input:1.2`～`1.6`、`grid-rendering:1.1`～`1.2` 和 `save-system` 的第二代活动项均已由实际证据完成。
2. 原生曲线的类型、控制参数和拆分结果在图、渲染和存档之间保持一致；所有二维几何交叉形成拓扑连接。
3. 当前米字型玩法可用，三角形与六边形策略证明输入约束可替换；连续铺路、批量拆路和撤销重做通过主场景验证。
4. 10k Edge 的交互路径满足 60 FPS 硬门槛；100k Edge 完成压力测试并记录结果，但不作为完成阻塞项。
5. 多个命名道路存档、自动存档和损坏加载保护通过；旧存档不兼容且不执行迁移。
6. `dotnet build`、完整自动化、Godot 运行时和 `road-graph:7.1` 全部通过；最终证据保留在第二代设计文档附录 D。
7. RoadType、交通模拟、桥梁/隧道/立交和旧存档兼容明确排除，不得作为第二代完成条件。
