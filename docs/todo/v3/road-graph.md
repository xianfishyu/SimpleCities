# 第三代 RoadGraph 系统待办清单

> 系统 key：`v3-road-graph`
> 整理日期：2026-08-14
> 证据：当前工作区源码、RoadGraph 自动化测试、`docs/manuals/road-system-v2-gen.md` 附录 D 及 `docs/manuals/road-system-v3-gen.md`。
> 主导原则：负责第三代道路的数值与容量边界、连续拓扑存储、原生几何、自环/平行边、空间索引、RoadType、不可变事务以及最终跨系统集成验收；不负责交通模拟。

## 状态总览

| ID | 发现 | 当前状态 | 处置方式 |
|---|---|---|---|
| 8.0 | 数值、吸附/聚类、精确几何和 ID 分配没有统一边界 | 已完成 | 已建立 mutation 共用数值/容量基础、确定交点规划和 geometry canonicalizer；Load 接入由 `v3-save-system:2.1`～`2.2` 消费 |
| 8.1 | 邻接不能区分 self-loop 的 A/B incidence | 已完成 | 已引入端接角色、环路度数、六类原生反向、权威几何锚和 typed 方向 key |
| 8.2 | waypoint、原生段和 Group 仍碎片化 Edge | 已完成 | 已建立最大连续 Edge、半开 query fragment，并完整移除 RoadGroup |
| 8.3 | 闭合与自交路径没有规范提交格式 | 已完成 | 已建立 closed/full-turn 提交、incoming/incoming 交点规划、rooted seam 与原子重叠/歧义拒绝 |
| 8.4 | 规范 Edge 尚无稳定 RoadType 领域契约 | 已完成 | 已建立类型化建造、语义边界和拓扑替换继承 |
| 8.5 | 改造、不可变 root 与规范化没有统一事务身份 | 已完成 | 已建立结构共享 root、原子改造、统一 delta 事件与 lineage/revision/sequence 防护 |
| 8.6 | 第三代跨系统能力尚未组合验收 | 开放 | 负责 V3 道路存储、环路、类型、输入、UI、渲染和存档最终集成 |

### 设计覆盖矩阵

| 设计范围 | 当前事实 | 关联待办 |
|---|---|---|
| V3 规范存储、环路与道路分级 | 已完成 mutation 数值/容量、exact-sign line、endpoint-role incidence、六类原生方向、最大连续 Edge、删除后重归一化、半开 query fragment、RoadGroup 移除、公共闭合/自交提交、Edge 级 RoadType 及不可变 root/delta；严格 V3 format v1 reader/writer 已由 `v3-save-system:2.1` 接入，有界 token/恢复协议仍开放 | 8.6、`v3-save-system:2.2`～`2.3`、`v3-grid-rendering:2.0`～`2.3`、`v3-tool-input:2.1`～`2.4`、`v3-ui:1.1`～`1.4` |

## 执行顺序

### 阶段 8：第三代规范存储、环路、分级与集成

<a id="v3-road-graph8.0"></a>

- [x] **8.0 固化 RoadGraph 数值、容量与确定性基础**
  - 完成前问题：V2 运行时主要检查 `float.IsFinite`，但极大有限坐标仍可使距离平方、bounds、bucket 坐标和长度累计溢出；`_nextID++` 没有 checked reservation。交点候选按浮点参数/集合遍历处理，节点身份吸附和交点 epsilon 尚未隔离；line 合并若沿用普通 float cross/近似角度会吞掉 1 ULP 折点；圆弧也没有逐 bit full-turn 格式。近似相交代表坐标、`-0` 和重复 canonicalize 结果没有唯一基础契约。
  - 已实现：新增 `RoadNumericPolicy`、`RoadGraphCapacity` 和 checked `RoadGraphIDReservation`，限制坐标、六类原生控制参数、单 geometry/Edge/全图长度、实体/geometry/query fragment/bucket/ref 及单次 mutation work；距离和累计使用 finite double。`SubmitPolyline`、`SubmitPath`、交点规划和直接 subdivision 在 mutation 前完成 admission，Debug invariant 复核实际容量，结构化返回 `NumericOutOfRange`、`CapacityExceeded` 或 `AmbiguousIntersection`。`NodeSnap` 使用精确 double 距离且只在逐值同距时按最小 Node ID 破同值；intersection epsilon 与 `NodeSnapRadius` 分离。
  - 确定性基础：新增 binary32 exact-sign `Orient2D` / `DotSign` 和 geometry canonicalizer，只合并精确同向共线 line，保留 1 ULP 折点与回头，统一 `-0 => +0` 并保证第二次 canonicalize 无变化。交点 cluster 使用对称 witness、稳定 key、connected component、最大直径、既有 Node 优先和容量门禁；现阶段进入 mutation admission 并拒绝歧义。`CircularArc` 只把逐 bit `+/-Mathf.Tau` 识别为 full-turn，`End` / `GetPosition(1)` 直接复用 `Start`。
  - 后续归属：六类 geometry 原生反向和 typed direction key 属于 8.1；把 cluster 的单一代表坐标重锚到所有 split geometry、形成 canonical mutation plan 以及 query fragment locality 属于 8.2～8.3；不同历史下的 canonical graph 等价属于 8.2～8.3；V3 format v1 的有界逐值往返和 Load 复用本策略属于 `v3-save-system:2.1`～`2.2`。这些验收未删除，也不计入本基础项的完成证据。
  - 关联：`v3-save-system:2.2` 消费本项容量策略并负责磁盘字节与解析预算；这不是本项的前置依赖。
  - 集成负责人：`v3-road-graph`。
  - 验证证据（2026-08-13）：新增 6 个 V3 测试文件并扩充原生路径回归；`dotnet test tests/SimpleCities.RoadGraph.Tests/SimpleCities.RoadGraph.Tests.csproj --no-restore` 为 538/538 通过，`dotnet build SimpleCities.sln --no-restore` 为 0 警告、0 错误，Roslyn CodeLens 含 analyzer 为 0 diagnostics，`git diff --check` 通过。冻结运行 `Scenes/MapTest.tscn`：合法提交得到 3 Node、2 Edge、1 Group，renderer 接管 2 Edge/8 mesh 顶点；越界提交在 DAP `stdout` 明确打印 `NumericOutOfRange`，拒绝前后 payload SHA-256、`nextID = 6`、实体计数、renderer/mesh 和 undo 计数逐值不变；editor log 与 DAP `stderr` 无错误，临时槽、会话和进程均已清理。
  - 验收结果：本项负责的任何超限或歧义均在图/ID/事件变化前结构化拒绝；合法 mutation 不写入 NaN/Infinity/负 ID/`-0`，精确 full-turn seam 逐值闭合，1 ULP 折点不被近似吞并，附近无关 Node 不被 cluster 吸附，候选枚举扰动不改变 cluster 结果，geometry canonicalizer 第二次不产生变化。

<a id="v3-road-graph8.1"></a>

- [x] **8.1 建立 endpoint-role incidence 与 self-loop 图基础**
  - 完成前问题：`EdgeRef` 只有 Edge ID 和邻居 Node ID；`AddEdge`、恢复和不变式拒绝相同端点，无法区分 self-loop 在同一节点上的 A/B 端，detach 也只移除首个同 ID 引用。
  - 已实现：`GraphNode` 改为有序 `EdgeIncidence(EdgeID, Endpoint, NeighborNodeID)`；`IncidenceCount` / `Degree` 按端接计数，`IncidentEdgeCount` 单独统计不同 Edge，自环在同一 Node 注册 A/B 各一次。邻接 attach/detach/rebuild、删除、diagnostics、空间引用检查和 `RoadRenderer` 切线均按 endpoint role 处理；`PreparedRoadGraphTopology` / `RoadGraph.FromPreparedTopology` 提供先完整校验再发布的内部图边界，可表达 self-loop、平行 Edge 和普通边。非环 Edge 按 Node ID 升序定向，六类 `RoadGeometrySegment` 均实现不经显示采样的原生 `Reverse()`，self-loop 只比较当前链与逐段反向后的链，以版本化 primitive kind、规范周期角、canonical `-0` 和 binary32 typed token 确定方向，排除 ID、Group、JSON、长度、bounds、显示采样和空间引用。
  - 精确几何契约：圆弧持有逐 bit `Start` / `End` 锚及权威 `EndAngle`，clothoid 持有逐 bit `End` 锚及权威 `ReverseStartHeading`；split、reverse 和 geometry codec 交换或保留这些值，不用三角函数/积分重新猜测端点。方向 key 纳入这些权威字段，`GraphEdge` 在定向后再次验证相邻 primitive 逐 bit 连续，reverse twice 与 codec 往返均保持逐值稳定。旧 V2 geometry payload 仅在一整组新增锚字段全部缺失时兼容推导，部分提供则拒绝；V2 RoadGraph reader 继续拒绝 self-loop，V3 format v1 reader 仍由 `v3-save-system:2.1` 实现。
  - 依赖：`v3-road-graph:8.0`。
  - 验证证据（2026-08-13）：新增 `RoadGeometryDirectionV3Tests`、`RoadGraphIncidenceV3Tests` 并扩充圆弧、clothoid、codec、原生拆分和路径回归，覆盖六类及混合链反向、严格 reverse twice、full-turn seam/sweep、周期角、负零、ID/JSON 属性顺序扰动、self-loop、self-loop 加支路、parallel Edge、非环 ID 定向、逐 bit primitive join、codec 往返、incidence rebuild、重复 endpoint role 和缺失 endpoint 拒绝。最终 `dotnet test SimpleCities.sln --no-restore` 为 559/559，`dotnet build SimpleCities.sln --no-restore` 为 0 警告/0 错误，Roslyn CodeLens 含 analyzer 为 0 diagnostics，`git diff --check` 通过。
  - Godot Tier 3（2026-08-13）：冻结运行 `Scenes/MapTest.tscn`，用内部 prepared topology 构造 full-turn self-loop 与同端点对的 line/Bézier 平行 Edge，并绑定真实 `RoadRenderer`。结构化结果为 `pass = true`、3 Node、3 Edge、2 Group、6 incidence、seam roles `[A, B]`、`seamIsJunction = false`、附近平行 Edge `[5, 6]`、环查询命中 Edge 1、renderer 3 Edge/26 采样点/52 mesh 顶点；editor log 与 DAP `stderr` 均无错误。随后恢复原图、清空 exec holder、停止项目并删除临时 bridge/UID。
  - 验收结果：self-loop 已能作为一等 `GraphEdge` 存活于内部准备图、空间查询、渲染和完整不变式，平行 Edge 保持独立 incidence；当前拓扑代码不再用去重 neighbor 代替 degree。公共闭合提交、最大连续 Edge、Group 删除和 V3 reader 分别留在 8.2～8.3 与 `v3-save-system:2.1`，不计入本项。

<a id="v3-road-graph8.2"></a>

- [x] **8.2 形成最大连续 Edge 并移除 RoadGroup**
  - 完成前问题：`SubmitPolyline` 为非共线 waypoint 创建独立 Edge，`SubmitPathCore` 为每个 `NativePathPiece` 创建 Edge；`TryMergeAtNode` 只合并共线同 Group 边，导致输入采样、原生段边界和跨提交接缝都成为拓扑边界。索引按完整 geometry AABB 占桶，局部查询可能聚合 Edge 后再扫描整条 geometry，压缩后会随远端长度或段数退化。
  - 已实现：`RoadGeometryCanonicalizer` 以 8.0 exact-sign 规则只合并精确同向共线 line，其他原生 primitive 保持类型和参数；`RoadGraph.Canonicalization` 在提交、交叉拆分和删除后按受影响 Node ID 收敛所有非结构性二 incidence 节点，折角与混合 primitive 保持一条 Edge 的有序 geometry 链。拆分保留 A 侧原 Edge ID，合并保留最小 Edge ID；self-loop seam 是不跨越、不旋转的硬边界。六类 geometry 均通过 `ReanchorChain` 把 cluster/Node 的规范坐标类型化写回链端，定向后保持逐 bit 连续。
  - Group-free 与查询：已删除 `RoadGroup` 类型及 UID、`GraphEdge.GroupID`、Group 字典/查询/事件、提交 Group ID 和 Group 变更摘要；DebugPanel、renderer、输入、性能程序、Godot contract 与正常 fixture 只使用 Node/Edge/geometry。空间索引改为携带 `(EdgeID, GeometryIndex, ParameterStart, ParameterEnd)` 的 query fragment，fragment cut、primitive join、非环 B 端和 self-loop seam 使用半开所有权产生唯一 `RoadLocation`；closest/radius/rectangle/交点只对命中 fragment 做精确测试后再聚合 Edge，容量超限不回退全图扫描。
  - 依赖：`v3-road-graph:8.0`、`v3-road-graph:8.1`。
  - 集成负责人：`v3-road-graph`；V3 format v1 与旧持久化代码删除属于 `v3-save-system:2.1`，DebugPanel 属于 `v3-ui:1.3`。
  - 验证证据（2026-08-13）：`dotnet test SimpleCities.sln --no-restore` 为 572/572；`dotnet build SimpleCities.sln --no-restore` 为 0 警告/0 错误；Roslyn CodeLens compiler/analyzer 为 0 diagnostics；6 个修改过的 GDScript 逐文件为 0 diagnostics；`git diff --check` 通过，生产源码和正常 fixture 无 Group 残余。自动化覆盖同向 line 折叠、折角/混合链、跨提交延伸、交叉拆分、删除后重归一化、六类原生细分/重锚、ID 规则、事件摘要、半开边界唯一命中与索引容量。
  - 局部性与性能（2026-08-13）：单 Edge 的 line 长度由 4,096 扩到 65,536、geometry 数由 64 扩到 1,024 后，首/中/尾窗口均为 1 candidate fragment、1 exact test、1 aggregated Edge、0 full scan/visit；两次 Release 复跑的 P95 为 0.0003～0.0007 ms。10k junction-dense 全场景硬门槛均通过，多交叉提交 P95 为 8.281～9.118 ms、平均分配 6,881.2 KiB；100k 压测为 20.152～20.184 ms、46,878.7 KiB。批量交叉只执行 1 次 mutation admission pass，避免逐 Edge 重复扫描整图。
  - Godot Tier 3（2026-08-13）：冻结运行 `Scenes/MapTest.tscn`，通过真实 `RoadBuilder` 提交 16 条平行道路及一条贯穿道路；最终为 50 Node、49 Edge，`RoadRenderer` 接管 49 Edge/196 mesh 顶点/2 static render nodes。editor 增量错误和 DAP `stderr` 均为空，exec holder、测试运行和临时进程已清理。
  - 验收结果：无分支同向 line 形成 2 Node/1 Edge/1 line geometry，折角和复合曲线形成 1 Edge 与不可约原生链；成功 mutation 后不保留非结构性二 incidence Node，rooted self-loop 不跨 seam 归并。边界命中无漏失/重复，固定窗口精确访问不随同一 Edge 远端规模增长，Group 已从当前生产图和公共结果中完整移除。当前过渡期内部持久化已随 8.4 升为严格 `schemaVersion = 3` 并要求 `roadType`，但仍不是 V3 format v1；`v3-save-system:2.1` 保持开放。

<a id="v3-road-graph8.3"></a>

- [x] **8.3 支持闭合、自交路径及确定环路规范形**
  - 完成前问题：`ValidatePolyline` / `ValidateNativePath` 将首尾闭合判为重复点，交点规划只比较 incoming 与 existing；图不能形成简单环、棒棒糖、两路口环或八字形的稳定表示。
  - 已实现：公共折线/原生路径允许正长度首尾逐 bit 闭合和精确 `+/-Mathf.Tau` full-turn。交点规划统一 incoming/incoming、incoming/existing、离散交叉、相切与 overlap 边界，离散自交提升为 junction，连续自重叠返回 `SelfOverlap`。cluster 规范坐标在 mutation 前写入 incoming piece 与既有 subdivision；既有端点 provenance 仅在权威端点的 cluster 直径内继承，附近无关 Node 不参与二次吸附；同一 split parameter 的冲突规范坐标返回 `AmbiguousIntersection`。合法结果经 8.2 归一化形成 rooted self-loop、parallel Edge 或 junction 间 Edge，纯二度闭合分量保留最小 Node ID seam。
  - 依赖：`v3-road-graph:8.1`、`v3-road-graph:8.2`。
  - 集成负责人：`v3-road-graph`；玩家闭合草稿属于 `v3-tool-input:2.0`，闭合 ribbon 属于 `v3-grid-rendering:2.0`。
  - 验证证据（2026-08-13）：新增 `RoadGraphClosedPathV3Tests` 并扩充原生交点/重叠/细分回归，覆盖简单环、精确正反 full-turn、棒棒糖、端点与内部八字形、两路口环、多个离散交点、环与既有路交叉、覆盖接续、顺序稳定、无关 Node provenance、端点 solver 误差及冲突 split parameter。最终 `dotnet test SimpleCities.sln --no-restore` 为 586/586，`dotnet build SimpleCities.sln --no-restore` 为 0 警告/0 错误，Roslyn CodeLens 含 analyzer 为 0 diagnostics，6 个修改过的 GDScript 逐文件为 0 diagnostics，`git diff --check` 通过。
  - 性能与运行时（2026-08-13）：最终 Release 复跑的 10k 全场景均低于 16.67 ms，多交叉提交 P95 为 10.268 ms；100k 多交叉为 56.806 ms，仅作压力记录；固定窗口仍为 1 fragment、1 exact test、0 full scan/visit。冻结运行真实 `Scenes/MapTest.tscn`，四段闭环通过 `RoadBuilder` 一次确认，renderer 接管 1 Edge 并在两帧后生成 10 mesh 顶点；完全回走打印预期 `SelfOverlap` 且图、history 与 renderer 不变。editor 增量错误和 DAP `stderr` 均为空，exec holder、运行会话及测试槽均已清理。
  - 验收结果：所有声明的合法环路达到指南第 5 节规范格式；非法重叠或交点歧义在 Node/Edge/ID/事件变化前结构化拒绝；self-loop 与 parallel Edge 不再因端点相同被误判重复。闭合 ribbon 的 Phase 7 专属视觉/表面契约仍由 `v3-grid-rendering:2.0` 负责，不属于本项。

<a id="v3-road-graph8.4"></a>

- [x] **8.4 在规范 Edge 上建立 RoadType 与类型化提交**
  - 完成前问题：规范化 Edge、提交 API 和 merge key 没有道路等级；若先把类型塞进碎片化 Edge，后续存储重构会重复改写并产生不稳定语义边界。
  - 已实现：固定 `Dirt = 0`、`Street = 1`、`Arterial = 2`、`Highway = 3` 及严格小写存储 token；`GraphEdge`、prepared topology 和拆分结果都强制携带合法类型，不提供静默默认值。公共原生路径提交只保留 `SubmitPath(RoadBuildRequest)`，折线入口 `SubmitPolyline(RoadType, points)` 同样要求显式类型；新占据几何使用请求类型，既有拆分继承原类型，类型进入 merge key。异类型二 incidence 节点保留为 semantic boundary，同类型接续继续归一化；完全覆盖返回 `FullyCovered`，不改造既有 Edge 或消耗 ID。
  - 依赖：`v3-road-graph:8.2`、`v3-road-graph:8.3`。
  - 验证证据（2026-08-13）：新增 `RoadGraphRoadTypeV3Tests` 并迁移所有原生路径调用者到显式请求，覆盖构造/非法枚举、开放与闭合路径、六类几何、同/异类型接续与交叉、semantic boundary、完全覆盖、部分重叠、历史/过渡期 schema 3 往返和失败无副作用。最终 `dotnet test SimpleCities.sln --no-restore` 为 611/611，`dotnet build SimpleCities.sln --no-restore` 为 0 警告/0 错误，Roslyn CodeLens 含 analyzer 为 0 diagnostics，相关 GDScript 逐文件为 0 diagnostics，`git diff --check` 通过；旧 `SubmitPath(RoadType, RoadPath)` 引用为 0。
  - Godot Tier 3（2026-08-13）：冻结运行真实 `Scenes/MapTest.tscn`，8 次合法类型提交得到 10 Edge/16 Node，类型计数为 Dirt 3、Street 2、Arterial 2、Highway 3；异类型接缝保留 2 条 Street/Arterial incidence，交叉点的 4 条 incidence 分别保持既有 Highway 与新 Dirt。异类型完全覆盖返回 `FullyCovered` 且 payload、ID watermark 不变；两帧后 renderer 接管 10 Edge/40 mesh 顶点/2 static nodes，editor log 与 DAP 两个输出通道无错误，测试图和临时 bridge 均已清理。
  - 验收结果：每条活动 Edge 有且仅有一个合法类型；不同类型边界保留，同类型且无结构边界的相邻 Edge 合并。`RoadBuilder` 目前显式构造固定 `Street` 请求，类型选择和会话冻结仍由开放的 `v3-tool-input:2.1` 负责；过渡期 `schemaVersion = 3` 只服务当前内部快照，不计作 V3 format v1。

<a id="v3-road-graph8.5"></a>

- [x] **8.5 实现改造后重归一化与统一事务摘要**
  - 完成前问题：图 API 只能增删 Edge；类型改造可能消除 semantic boundary 并触发 Edge/Node 合并，单纯 `UpdatedEdgeIDs` 或“ID 永远不变”无法表达真实结果。可变字典也无法让后台保存 O(1) 捕获稳定状态，revision、事件 sequence 和外部 load 后的图身份尚未分离。
  - 已实现：稳定 `RoadGraph` facade 以不可变 `RoadGraphRevision` 为唯一权威 root；Node/Edge 使用 immutable map，空间索引使用 copy-on-write bucket 页，root 同时保存 lineage、domain revision、change sequence、ID watermark、资源计数和总 geometry 长度。所有普通 mutation 先在未发布 working state 中完成 admission、规范化和不变式，再原子替换 root；未触碰 Entity、geometry 和 bucket 页跨 revision 按引用共享，旧 root 不由永久 revision 表保留。`CaptureRevision()` 为 O(1) 引用捕获，DebugPanel 从同一 root 读取 O(1) 计数。
  - 改造与事件：`ChangeRoadType` 对排序去重后的整批 Edge 做全量预检，合法批次改型后再次 canonicalize；所有 mutation 生成包含 Node/Edge 前后实体及 before/after content revision 的 `RoadGraphDelta`，只发布一次 `GraphChanged`。摘要包含排序去重的 created/removed/updated、`IsFullReset` 和单调 `ChangeSequence`；旧逐 Edge/`GraphCleared` 事件已删除。NoChanges 和失败不换 root，也不推进 ID、revision、sequence 或事件。
  - 身份与失败边界：`ApplyDelta` 用完整 `(LineageID, DomainRevisionID, ChangeSequence)` 校验方向、旧 sequence、重复 replay 和旧 lineage；undo/redo 恢复内容 revision，但 sequence、revision allocator 和 ID watermark 不回退。full reset 创建新 lineage、精确采用 prepared payload 的合法 `nextID`；事件发布期拒绝所有 mutation 入口重入，逐个隔离 observer 异常且不回滚已发布 root。RoadGraph 暴露完整 state token；异步表现结果的 consumer-specific generation/token 接管仍由 `v3-grid-rendering:2.2` 负责，不计入本领域项的完成证据。
  - 依赖：`v3-road-graph:8.2`～`8.4`。
  - 集成负责人：`v3-road-graph`；工具生命周期属于 `v3-tool-input:2.2`，历史 admission/容量属于 `v3-tool-input:2.3`。
  - 验证证据（2026-08-14）：`RoadGraphRoadTypeChangeV3Tests`、`RoadGraphTransactionV3Tests`、`RoadGraphMutationEventTests` 和 `RoadEditHistoryTests` 覆盖空集/重复/失效 ID、NoChanges、四类互转、semantic boundary 消失、rooted loop、事件内最终图不变式、所有普通 mutation 的 delta 正反应用、`R0/S0 -> R1/S1 -> R0/S2 -> R1/S3`、错误方向/旧 sequence/重复 replay、undo 后分叉、较小 watermark full reset、重入和首个/中间 observer 异常。最终 `dotnet test SimpleCities.sln --no-restore` 为 633/633；Debug build 为 0 警告/0 错误；Roslyn compiler/analyzer 为 0 diagnostics；`git diff --check` 通过。
  - 性能证据（2026-08-14，Release）：1k/10k/100k geometry 远端扩展下，固定局部类型改造分别分配 12.2/12.8/13.5 KiB，delta 固定为 0 Node/1 Edge，复制 2 个 Edge 对象与 2 个 bucket 页；未触碰 Entity 分别共享 2,999/29,999/299,999 个，100,000 次 root capture 均为 0 bytes，三档旧 root 均可释放。10k 全场景 P95 低于 16.67 ms，最坏多交叉为 7.997 ms；100k 结果完整记录但不作为硬门槛。
  - Godot Tier 3（2026-08-14）：冻结运行真实 `Scenes/MapTest.tscn`，真实 `RoadBuilder` 提交后为 1 rendered Edge/4 mesh 顶点/history 1；undo 后为 0 Edge/0 顶点、undo 0/redo 1；redo 后恢复 1 Edge/4 顶点、undo 1/redo 0。editor 增量日志与 DAP console/stdout/stderr 均为空，exec holder 为 0，项目已停止。
  - 验收结果：批次全有或全无，活动 root 一次替换且旧 root 不被修改；普通局部编辑复制与分配由受影响拓扑和 bucket 页决定，未随远端图线性增长。每次 commit sequence 唯一递增，错误 token 返回 `StaleGraphState` 且无副作用，同 lineage 不复用 revision/ID，full reset 换 lineage并采用 payload watermark；失败和 NoChanges 不污染 root、事件、allocator 或历史，observer 不能重入或回滚图，保存可 O(1) 捕获 root。

<a id="v3-road-graph8.6"></a>

- [ ] **8.6 完成第三代道路系统端到端评估**
  - 当前问题：领域测试不能证明规范存储、环路、类型选择、改造手势、混合渲染、V3 独立存档和命名槽在真实主场景共同成立。
  - 修改：按 `docs/manuals/road-system-v3-gen.md` Phase 8 建立组合契约，并把最终证据写回该指南附录 D。
  - 依赖：`v3-road-graph:8.0`～`8.5`、`v3-save-system:2.1`～`2.3`、`v3-grid-rendering:2.0`～`2.3`、`v3-tool-input:2.0`～`2.4`、`v3-ui:1.1`～`1.4`。
  - 集成负责人：`v3-road-graph`。
  - 验证：串行运行完整自动化和构建；执行真实 `MapTest` 的连续折线、跨提交延伸、精确 full-turn、简单环/棒棒糖/两路口环/八字形、支路删除重归一化、四类型建造与改造、token 防护 delta 撤销重做、不可变 root 结构共享/释放、V3 family/version format v1 有界往返、V2 根 canary 隔离、跨进程并发/自动存档、损坏/超限/ENOSPC 拒绝、publish/delete 中断恢复、成功/提交前失败/observer warning 且不存在提交后关键表现失败结果的 Load 生命周期、共享表面命中、混合 junction patch、presentation barrier、Vulkan 视觉、junction-dense/geometry-dense 存储与运行性能和 Windows 导出边界。
  - 验收：全部硬门禁有持久证据；TrafficGraph、A*、拥堵、高程道路和其他 V3 排除项不参与完成判定，也不得被宣称已实现。
  - 阶段证据（2026-08-14）：两路口环、八字形和删除支路后的 seam 重定位已进入领域/renderer 回归，真实 `MapTest` 的两路口环从 `4 Edge / 20 vertices / 4 markers` 收敛为 `2 Edge / 12 vertices / 2 markers`；完整自动化为 727/727，双配置构建及 Roslyn diagnostics 为 0。该证据只完成 Phase 7 的复杂 closed-ribbon 子矩阵；`v3-grid-rendering:2.0` 的缩放/重建与平行 Edge 表面命中、`2.1`～`2.3`、`v3-save-system:2.3`、`v3-tool-input:2.1`～`2.2`/`2.4` 和 `v3-ui:1.1`～`1.4` 尚未全部验收，因此 8.6 保持开放，附录 D 继续为空。

## 暂不执行

### 交通模拟

- 延期原因：V3 先完成领域合法的 self-loop、parallel Edge、RoadType 和统一事务摘要；模拟层不能反向收窄这些契约。
- 保持现状：本路线图不实现 `TrafficGraph`、寻路、速度、容量或拥堵。
- 重新开启条件：`v3-road-graph:8.6` 完成后，由根层 `docs/todo/traffic-simulation.md` 定义模拟映射。

## 已解决基线

- [x] **V3 数值、容量与确定性基础已建立。** mutation 在写图前执行坐标、控制参数、长度、资源、work 与 ID admission；exact-sign line、确定交点 cluster、full-turn 识别和 canonical `-0` 已由 538 项自动化及真实 `MapTest` 拒绝场景验证。后续 canonical root、query fragment 和 V3 reader 必须消费该策略，不能另建不一致阈值。
- [x] **V3 endpoint-role incidence 与原生方向基础已建立。** self-loop 在同一 Node 拥有 A/B 两条 incidence，平行 Edge 保持独立；六类原生几何可逆，arc/clothoid 以权威 binary32 锚和反向参数保持 mixed-chain join，非环与 self-loop 均有确定存储方向。后续 canonical Edge、闭合提交、V3 reader 和 renderer 必须沿用这些字段与 endpoint role，不能从邻居去重或显示采样反推拓扑与方向。
- [x] **V3 最大连续 Edge、Group-free 图与 fragment locality 已建立。** 提交、拆分和删除后恢复规范二 incidence 形；折角/混合 primitive 留在一条 Edge，连续同向 line 精确折叠。query fragment 对 cut/join/B 端/seam 使用半开唯一所有权，远端 line/geometry 扩大 16 倍不增加固定窗口 exact test；后续闭合、自交、RoadType、不可变 root 与 V3 reader 必须沿用该 canonical Edge 和索引身份，不能恢复 Group 或整 Edge 局部扫描。
- [x] **V3 公共闭合、自交与环路规范形已建立。** closed/full-turn、incoming/incoming intersection、离散自交 junction、rooted self-loop 和 parallel Edge 已进入公共提交；连续重叠与 canonical split 歧义在 mutation 前结构化拒绝。后续 RoadType、V3 reader、renderer 和 delta 必须保留 seam、endpoint-role incidence、typed direction 及失败原子性。
- [x] **V3 Edge 级 RoadType 与类型化提交已建立。** 四个稳定领域值、严格 token、显式 `RoadBuildRequest`、拆分继承、typed merge key、semantic boundary 和覆盖不改造已由 611 项自动化及真实 `MapTest` 混合类型场景验证。后续改造、renderer、V3 reader 和工具选择必须消费 Edge 级类型，不能引入默认类型或把类型塞入几何草稿。
- [x] **V3 不可变 root、原子改造和统一 delta 身份已建立。** 普通 mutation 结构共享未触碰 Entity/geometry/bucket 页，`CaptureRevision()` 为 O(1)，`GraphChanged` 是唯一事务事件；lineage/revision/sequence token、ID watermark、observer 隔离和重入拒绝均有自动化、Release 远端扩展基准及真实 `MapTest` undo/redo 证据。后续 V3 writer、renderer token 和 full-reset aggregate 必须直接消费该 root/token，不能重新捕获可变图或恢复逐 Edge 事件。
- [x] **空间索引精确覆盖不变式已改为线性批量校验。** `AssertInvariants()` 先按引用 identity 汇总预期 bounds，再由 `UniformGrid.HasExactCoverage(...)` 单次扫描 bucket entries；缺失、额外、错桶、同桶重复和内部计数不符仍严格拒绝。修复后 12k～100k V3 Load/renderer rebuild 逐级完成，100k 为 5069.431 ms，不再因 `reference × bucket` 二次扫描进入 AppHang；完整自动化为 637/637。该修复不关闭 `v3-road-graph:8.6`，Godot 10k 冷启动帧门、Phase 7 表现契约和 Windows Release 导出仍需最终组合验收。
- [x] **V2 道路数据层不依赖输入层方向或网格概念。** 任意角度直线、折线和结构化非法路径拒绝已有自动化保护。
- [x] **V2 原生曲线、二维交叉、查询、删除事务、渲染和存档已通过最终验收。** 这些是 V3 需要以新接口重新验证的玩家能力基线，不要求复用 V2 代码或读取 V2 数据。
- [x] **V2 规模基线已记录。** V3 性能门槛必须使用同机、同口径对照，不能把后台总耗时误报为主线程无卡顿。

## 完成标准

1. 8.0～8.5 的数值、容量、incidence、连续 Edge、环路、RoadType、不可变事务和 delta 契约均通过各自自动化及性能门禁。
2. V3 runtime、format v1、renderer、工具、UI 和事件消费者只使用一套新契约；旧 Group/API/事件/DTO/writer 已删除，不存在兼容适配器、双写或生产路径选择。
3. `v3-save-system`、`v3-grid-rendering`、`v3-tool-input` 与 `v3-ui` 的依赖项全部通过自身验收。
4. 8.6 在真实 `MapTest`、Vulkan、10k 门槛、100k 压测和 Windows 导出环境完成最终组合验证，证据写回 V3 指南附录 D。
5. `v3-road-graph:8.6` 是唯一最终集成负责人；交通模拟和其他明确排除项不阻塞 V3。
