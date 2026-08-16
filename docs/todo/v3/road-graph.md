# 第三代 RoadGraph 系统待办清单

> 系统 key：`v3-road-graph`
> 整理日期：2026-08-13
> 证据：当前工作区源码、RoadGraph 自动化测试、`docs/manuals/road-system-v2-gen.md` 附录 D 及 `docs/manuals/road-system-v3-gen.md`。
> 主导原则：负责第三代道路的数值与容量边界、连续拓扑存储、原生几何、自环/平行边、空间索引、RoadType、不可变事务以及最终跨系统集成验收；不负责交通模拟。

## 状态总览

| ID | 发现 | 当前状态 | 处置方式 |
|---|---|---|---|
| 8.0 | 数值、吸附/聚类、精确几何和 ID 分配没有统一边界 | 开放 | 建立 mutation/load 共用数值容量与确定 canonicalizer |
| 8.1 | 邻接不能区分 self-loop 的 A/B incidence | 开放 | 引入端接角色、环路度数、原生反向和 typed 方向 key |
| 8.2 | waypoint、原生段和 Group 仍碎片化 Edge | 开放 | 建立 rooted 最大连续 Edge、半开 query fragment 并移除 RoadGroup |
| 8.3 | 闭合与自交路径没有规范提交格式 | 开放 | 支持 loop seam、self-loop、parallel Edge 和离散自交 |
| 8.4 | 规范 Edge 尚无稳定 RoadType 领域契约 | 开放 | 类型化建造、语义边界和拓扑替换继承 |
| 8.5 | 改造、不可变 root 与规范化没有统一事务身份 | 开放 | 原子 root 替换并发布 lineage/revision/sequence 防护的 delta |
| 8.6 | 第三代跨系统能力尚未组合验收 | 开放 | 负责 V3 道路存储、环路、类型、输入、UI、渲染和存档最终集成 |

### 设计覆盖矩阵

| 设计范围 | 当前事实 | 关联待办 |
|---|---|---|
| V3 规范存储、环路与道路分级 | 当前非共线 waypoint/原生段会碎片化为 Edge；Group 阻止跨提交合并；self-loop 被拒绝且 EdgeRef 无端接角色；数值/ID/长 Edge 查询没有 V3 容量契约；schema 和 Edge 均无类型 | 8.0～8.6、`v3-save-system:2.1`～`2.3`、`v3-grid-rendering:2.0`～`2.3`、`v3-tool-input:2.0`～`2.4`、`v3-ui:1.1`～`1.4` |

## V3 实施记录

> 本段记录已落地且经过验证的 V3 模块；完整工作项仍以「状态总览」和「执行顺序」为准。

### 2026-08-13：8.0 数值、容量与精确谓词基础（部分）

- 新增 `Scripts/Road/V3/` 基础模块：
  - `RoadNumericPolicy.cs`：坐标/长度范围、`-0` 规范化、受检 double 距离、full-turn 逐 bit 判定与 start angle 规范化。
  - `RoadGraphCapacity.cs`：mutation 与 format v1 load 共用的实体、geometry、query fragment、bucket/ref 与 ID 上限。
  - `RoadIDAllocator.cs`：只前进、不复用、checked 的 ID allocator，支持批量预留。
  - `ExactLinePredicates.cs`：`BitwiseEquals`、`Orient2D`、同向 dot 与 line 无损合并谓词。
  - `RoadType.cs`：稳定 `Dirt/Street/Arterial/Highway` 与 wire name 映射。
- 新增 `tests/SimpleCities.RoadGraph.Tests/V3/` 共 19 个 xUnit 用例，覆盖 `-0`、full-turn 边界、容量校验、ID 耗尽/预留和 1 ULP 折点不合并；完整测试套件 511/511 通过。
- 尚未完成（仍属 8.0）：全 primitive 精确合并契约、graph canonicalizer 接入真实 RoadGraph、mutation/load 容量接线。

### 2026-08-13：8.0 NodeSnap（部分）

- 新增 `Scripts/Road/V3/RoadNodeSnap.cs`：一次解析全部 request anchor，只读不写图；使用受检 double 距离，等距时选择最小 Node ID，`NodeSnapRadius` 与 cluster epsilon 分离。
- 新增 6 个 xUnit 用例；完整测试套件 539/539 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成（仍属 8.0）：全 primitive 精确合并契约、graph canonicalizer 接入真实 RoadGraph、mutation/load 容量接线。

### 2026-08-13：8.0 full-turn 接入 CircularArc（部分）

- 更新 `Scripts/Road/Geometry/CircularArcRoadGeometrySegment.cs`：`IsFullTurn` 按逐 bit canonical binary32 `+/-Tau` 判定；`End` / `GetPosition(1)` 直接复用 `Start`，`GetUnitTangent(1)` 与起点一致；`BitDecrement(Tau)` 不是 full-turn，`BitIncrement(Tau)` 仍被拒绝。
- 新增 5 个 xUnit 用例；完整测试套件 544/544 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成（仍属 8.0）：全 primitive 精确合并契约、graph canonicalizer 接入真实 RoadGraph、mutation/load 容量接线。

### 2026-08-13：8.0 确定性交点 cluster（部分）

- 新增 `Scripts/Road/V3/RoadIntersectionCluster.cs`：`IntersectionWitness` / `IntersectionCluster` / `IntersectionClusterResult`；按稳定 key 排序后以 connected component 聚类，与输入顺序无关；多个既有 Node 或组件直径超限返回结构化失败；无既有 Node 时选择稳定 key 最小的代表并规范 `-0`。
- 新增 7 个 xUnit 用例；完整测试套件 551/551 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成（仍属 8.0）：全 primitive 精确合并契约、graph canonicalizer 接入真实 RoadGraph、mutation/load 容量接线。

### 2026-08-13：8.0/8.2 geometry line canonicalizer（部分）

- 新增 `Scripts/Road/V3/RoadGeometryCanonicalizer.cs`：对相邻 `LineRoadGeometrySegment` 按 exact-sign 契约无损合并，输出规范 `-0`；非 line 曲线保留原样，不采样降级；单次扫描可级联合并，重复执行幂等。
- 新增 8 个 xUnit 用例；完整测试套件 559/559 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成（仍属 8.0/8.2）：全 primitive 精确合并契约、graph canonicalizer 接入真实 RoadGraph/format v1。

### 2026-08-13：8.2/8.3 self-loop rooted chain（部分）

- 新增 `Scripts/Road/V3/RoadSelfLoopChain.cs`：以 seam 为数组首尾公共点做 rooted chain 规范化；数组内部可合并，禁止跨 seam 合并或循环移位；最后用 typed direction key 选择规范存储方向。
- 新增 6 个 xUnit 用例；完整测试套件 565/565 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：将 self-loop chain 接入图级规范化、环路提交与 format v1 往返。

### 2026-08-13：8.2 graph canonicalizer（部分）

- 新增 `Scripts/Road/V3/RoadGraphCanonicalizer.cs`：`RoadCanonicalNode` / `RoadCanonicalEdge` / `RoadCanonicalGraph` 抽象图模型；按 merge key 合并二 incidence 节点，self-loop seam 保留，远端相同形成 self-loop，非环结果按 `NodeAID < NodeBID` 定向，合并保留最小 Edge ID。
- 新增 7 个 xUnit 用例；完整测试套件 572/572 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：将 graph canonicalizer 接入真实 RoadGraph/format v1，并补齐拆分保留 ID、全 primitive 精确合并契约。

### 2026-08-13：8.1 incidence 与原生几何反向基础（部分）

- 新增 `Scripts/Road/V3/EdgeIncidence.cs`：`EdgeEndpoint` / `EdgeIncidence`，为 self-loop 在同一节点的 A/B incidence 提供值类型基础。
- 新增 `Scripts/Road/V3/RoadGeometryReverser.cs`：六类原生几何（Line/Bezier/Hermite/Arc/Clothoid/RationalQuadratic）的权威反向契约；全圆保留 seam，Clothoid 正确转换朝向与有符号曲率。
- 新增 15 个 xUnit 用例；完整测试套件 526/526 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成（仍属 8.1）：把 V3 incidence 图骨架接入真实 `RoadGraph`/不可变 root、detach/rebuild 与切线查询。

### 2026-08-13：8.1 V3 incidence 图骨架（部分）

- 新增 `Scripts/Road/V3/RoadGraphV3.cs`：`RoadGraphV3Node` / `RoadGraphV3Edge` / `RoadGraphV3` 最小 incidence 图运行时；支持 self-loop A/B incidence、平行 Edge、degree/distinct-edge/neighbor 查询、删除时一次移除全部 incidence，并使用 `RoadIDAllocator` 分配 ID。
- 新增 7 个 xUnit 用例；完整测试套件 579/579 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：将骨架升级为不可变 root、事务摘要、detach/rebuild 与切线查询。

### 2026-08-13：8.1 typed direction key（部分）

- 新增 `Scripts/Road/V3/RoadDirectionKey.cs`：版本化 typed binary32 token key，规范 `-0` 与周期角度，使用 IEEE total order 比较；`SelectCanonicalDirection` 在正向/反向链中选择 key 较小者。
- 新增 7 个 xUnit 用例；完整测试套件 533/533 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：将 graph canonicalizer 接入真实 RoadGraph/format v1。

### 2026-08-13：8.4 RoadType 提交与改造结果类型（部分）

- 新增 `Scripts/Road/V3/RoadBuildRequest.cs`：类型化建造请求 `RoadBuildRequest(Path, RoadType)`，拒绝空几何、null 段和非法枚举。
- 新增 `Scripts/Road/V3/RoadTypeChange.cs`：`RoadTypeChangeResult`（Success/NoChanges/Changed/Removed/Created）与 `RoadTypeChangeValidator`（枚举校验、选择排序去重）。
- 新增 7 个 xUnit 用例；完整测试套件 586/586 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：将请求接入 mutation plan、改造后重归一化与统一 `GraphChanged` 摘要。

### 2026-08-13：8.5/Phase 5 V3 format v1 minimal codec（部分）

- 新增 `Scripts/Road/V3/RoadGraphV3Codec.cs`：`RoadGraphV3Data` / `NodeData` / `EdgeData` 与 `RoadGraphV3CodecResult`；序列化 `RoadCanonicalGraph` 为 `simple-cities-v3` / `road-network` / schemaVersion 1 JSON，反序列化执行 family/version、ID 唯一、端点存在、RoadType、geometry 与 `nextID` 基础校验。
- 新增 7 个 xUnit 用例；完整测试套件 593/593 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：严格 token/lexeme reader、有界 I/O、manifest v1、独立保存根与真实 RoadGraph 接入（属 `v3-save-system`）。

### 2026-08-13：8.5 事务摘要与状态 token（部分）

- 新增 `Scripts/Road/V3/RoadGraphTransaction.cs`：`RoadGraphV3ChangeSummary`（created/removed/updated + IsFullReset + ChangeSequence）、`GraphStateToken`（LineageID / DomainRevisionID / ChangeSequence）与 `RoadGraphV3ChangeSummaryFactory`（排序去重增量摘要）。
- 新增 3 个 xUnit 用例；完整测试套件 596/596 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：将摘要/token 接入真实 mutation、undo/redo 与 full-reset load。

### 2026-08-13：8.5/10.7 可逆 delta 基础（部分）

- 新增 `Scripts/Road/V3/RoadGraphDelta.cs`：`RoadGraphV3EntityChange<T>`（Before/After、created/updated/removed 分类）与 `RoadGraphV3Delta`（BeforeRevisionID/AfterRevisionID、Node/Edge changes、`Invert()`）。
- 新增 4 个 xUnit 用例；完整测试套件 600/600 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：将 delta 接入 mutation plan、有界历史与 undo/redo token 校验。

### 2026-08-13：8.5/10.7 有界 delta 历史（部分）

- 新增 `Scripts/Road/V3/RoadEditHistoryV3.cs`：`RoadGraphV3DeltaSizeEstimator` 与 `RoadEditHistoryV3`；按 entry 数与估算字节双预算淘汰最旧 undo，单条超预算拒绝入历史，push 清空 redo。
- 新增 6 个 xUnit 用例；完整测试套件 606/606 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：将历史接入真实 mutation、undo/redo token 校验与 full-reset 清空。

### 2026-08-13：8.2 query fragment 与边界所有权（部分）

- 新增 `Scripts/Road/V3/RoadQueryFragment.cs`：`RoadLocation` / `RoadQueryFragment` 与 `RoadQueryOwnership.NormalizeBoundary`；实现 primitive join 映射、self-loop seam 映射、非环末 fragment 拥有 `t=1`。
- 新增 6 个 xUnit 用例；完整测试套件 612/612 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：将 fragment 接入空间索引、容量门禁与局部查询基准。

### 2026-08-13：8.2 uniform grid 空间索引骨架（部分）

- 新增 `Scripts/Road/V3/RoadSpatialIndexV3.cs`：按 query fragment 保守 bounds 放入 uniform grid bucket，支持 radius/rect 查询、容量门禁（fragment/bucket/ref）与查询候选计数；后续再接长 geometry 参数区间切分。
- 新增 6 个 xUnit 用例；完整测试套件 618/618 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：长 Edge 参数区间 fragment 切分、局部查询基准与真实 RoadGraph 接线。

### 2026-08-13：8.2 line fragment 切分（部分）

- 新增 `Scripts/Road/V3/RoadQueryFragmentBuilder.cs`：按 uniform grid bucket 边界切分 line primitive，生成严格递增、无空洞覆盖 `[0,1]` 的参数区间 fragment 与保守 bounds，避免整条斜线 AABB 污染无关 bucket。
- 新增 6 个 xUnit 用例；完整测试套件 624/624 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：曲线 fragment 策略、局部查询基准与真实 RoadGraph 接线。

### 2026-08-13：8.5 不可变 root 骨架（部分）

- 新增 `Scripts/Road/V3/RoadGraphV3Revision.cs`：不可变 root 骨架，AddNode/AddEdge/RemoveEdge/ChangeRoadType 均返回新 revision，旧 revision 永不被修改；当前为全量字典复制，后续改为持久化页/copy-on-write 结构共享。
- 新增 5 个 xUnit 用例；完整测试套件 629/629 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：结构共享、O(1) snapshot capture、事务摘要/delta 与真实 facade 接线。

### 2026-08-13：8.5 稳定 facade 与事务提交（部分）

- 新增 `Scripts/Road/V3/RoadGraphV3Facade.cs`：稳定 facade 持有不可变 root；普通 mutation 原子替换 root、递增 DomainRevisionID/ChangeSequence 并生成 `RoadGraphV3ChangeSummary`；full reset 创建新 lineage。
- 新增 6 个 xUnit 用例；完整测试套件 635/635 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：结构共享、O(1) snapshot capture、delta 历史接入与真实应用装配。

### 2026-08-13：8.5 快照捕获（部分）

- 新增 `Scripts/Road/V3/RoadGraphV3Snapshot.cs`，并在 `RoadGraphV3Facade` 增加 `CaptureSnapshot()`：保存 root 引用与当前 token，后续编辑不影响旧快照。
- 新增 2 个 xUnit 用例；完整测试套件 637/637 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：结构共享优化、delta 历史接入与真实应用装配。

### 2026-08-13：8.5/10.7 delta applier（部分）

- 新增 `Scripts/Road/V3/RoadGraphV3DeltaApplier.cs`：将可逆 delta 应用到不可变 root，支持 created/removed/updated，allocator watermark 只前进不回退；失败时返回原 revision。
- 新增 5 个 xUnit 用例；完整测试套件 642/642 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：将 applier 接入 facade undo/redo 与历史 token 校验。

### 2026-08-13：8.5/10.7 facade+delta 控制器（部分）

- 新增 `Scripts/Road/V3/RoadGraphV3Controller.cs`：组合 facade 与有界 delta 历史；普通 mutation 记录 delta，`TryUndo` 应用逆 delta，`TryRedo` 应用正 delta，均递增 ChangeSequence。
- 在 `RoadGraphV3Facade` 增加 `TryApplyDelta`，在 `RoadGraphV3ChangeSummaryFactory` 增加 `FromDelta`。
- 新增 5 个 xUnit 用例；完整测试套件 647/647 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：历史 admission 前校验/回滚、full-reset 清空历史与真实应用装配。

### 2026-08-13：8.5/10.7 历史拒绝回滚（部分）

- 在 `RoadGraphV3Facade` 增加 `Restore(RoadGraphV3Snapshot)`；`RoadGraphV3Controller` 在 mutation 后若历史预算拒绝，通过快照精确回滚 facade 并返回失败。
- 新增 1 个 xUnit 用例；完整测试套件 648/648 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：full-reset 清空历史与真实应用装配。

### 2026-08-13：8.5 full-reset 清空历史（部分）

- 在 `RoadGraphV3Controller` 增加 `ReplaceWithFullReset`：调用 facade full reset、清空 undo/redo，并返回 `IsFullReset` 摘要。
- 新增 1 个 xUnit 用例；完整测试套件 649/649 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与严格 format v1 接入。

### 2026-08-13：8.5/Phase 5 persistence 桥接（部分）

- 新增 `Scripts/Road/V3/RoadGraphV3Persistence.cs`：将 `RoadGraphV3Revision` 序列化为 format v1 JSON，并从 codec 结果重建 revision；保留加载的 `nextID` 作为 allocator watermark。
- 新增 4 个 xUnit 用例；完整测试套件 653/653 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：严格有界 I/O、manifest v1、独立保存根与真实应用装配（属 `v3-save-system`）。

### 2026-08-13：8.2/8.5 revision canonicalizer（部分）

- 新增 `Scripts/Road/V3/RoadGraphV3Canonicalizer.cs`：将不可变 root 转为 canonical graph，执行 graph canonicalizer，再重建为新 root；链式合并、语义边界保留、self-loop seam 保留。
- 新增 5 个 xUnit 用例；完整测试套件 658/658 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：将 canonicalizer 接入 facade mutation 后的自动规范化与 delta 摘要。

### 2026-08-13：8.5 facade 自动规范化（部分）

- 在 `RoadGraphV3Facade` 增加 `TryNormalize()`：应用 `RoadGraphV3Canonicalizer`，有变化时递增 DomainRevisionID/ChangeSequence 并生成增量摘要。
- 新增 2 个 xUnit 用例；完整测试套件 804/804 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：将自动规范化接入 controller mutation 后自动调用与完整 delta 历史记录。

### 2026-08-13：8.5 controller 规范化入口（部分）

- 在 `RoadGraphV3Controller` 增加 `TryNormalize()` 转发 facade 规范化；可通过 controller 在批量编辑后主动规范化。
- 新增 1 个 xUnit 用例；完整测试套件 805/805 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：mutation 后自动调用与规范化 delta 历史记录。

### 2026-08-13：8.5 delta builder（部分）

- 新增 `Scripts/Road/V3/RoadGraphV3DeltaBuilder.cs`：从前后不可变 root 构建可逆 delta，支持规范化/批量变更历史记录。
- 新增 2 个 xUnit 用例；完整测试套件 807/807 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：将 delta builder 接入 controller 自动规范化与历史记录。

### 2026-08-13：8.5 controller 规范化入历史（部分）

- 在 `RoadGraphV3Controller` 增加 `NormalizeAndRecord()`：规范化后通过 `RoadGraphV3DeltaBuilder` 生成 delta 并入历史，失败时回滚快照。
- 新增 1 个 xUnit 用例；完整测试套件 808/808 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：mutation 后自动调用规范化（无需手动调用）。

### 2026-08-13：8.5 mutation 自动规范化（部分）

- 在 `RoadGraphV3Controller` 的 AddEdge/RemoveEdge/ChangeRoadType 成功后自动调用 `NormalizeAndRecord`，使链式编辑在提交后自动达到规范形并记录 delta。
- 新增 1 个 xUnit 用例；完整测试套件 809/809 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：规范化失败/历史拒绝时整笔操作回滚语义细化与真实应用装配。

### 2026-08-13：8.5 规范化失败整笔回滚（部分）

- 在 `RoadGraphV3Controller` 引入 `TryNormalizeAndRecord`，mutation 后规范化/历史拒绝时弹出原始 delta 并恢复快照，保证整笔操作原子。
- 新增 1 个 xUnit 用例；完整测试套件 810/810 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与严格 format v1 接入。

### 2026-08-13：8.5 控制器快照恢复（部分）

- 在 `RoadGraphV3Controller` 增加 `Restore(RoadGraphV3Snapshot)`：恢复 facade 状态并清空 undo/redo，供 load/full-reset 回滚与外部快照恢复使用。
- 新增 1 个 xUnit 用例；完整测试套件 929/929 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与严格 format v1 接入。

### 2026-08-13：8.5 规范化差异改用 delta 检测（更新）

- `RoadGraphV3Facade.TryNormalize` 改为通过 `RoadGraphV3DeltaBuilder.BuildDelta` 判断规范化前后是否实际变化，并直接用 delta 生成摘要，避免只比较数量/键时漏掉节点位置或 Edge 几何变化。
- 完整测试套件 929/929 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与严格 format v1 接入。

### 2026-08-13：8.5 控制器类型化建造入口（部分）

- `RoadGraphV3Controller` 增加 `TryBuild(RoadBuildRequest, float snapRadius)`：从 `RoadPath` 的首末端经 `RoadNodeSnap` 复用近邻节点或创建节点，并以整条 geometry 提交类型化 Edge，供 V3 铺路会话直接消费。
- 新增 3 个 xUnit 用例；完整测试套件 1025/1025 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与严格 format v1 接入。

### 2026-08-13：8.5 批量类型改造入口（部分）

- `RoadGraphV3Facade.TryChangeRoadTypes` 以单个不可变 root 替换完成多 Edge 类型改造，`RoadGraphV3Controller.TryUpgradeSelection` 将其作为一次可撤销 delta 入历史并继续自动规范化。
- 新增 2 个 xUnit 用例；完整测试套件 1000/1000 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与严格 format v1 接入。

### 2026-08-13：8.5 批量删除入口（部分）

- `RoadGraphV3Facade.TryRemoveEdges` 以单个不可变 root 替换完成多 Edge 删除，`RoadGraphV3Controller.TryRemoveSelection` 将其作为一次可撤销 delta 入历史并继续自动规范化。
- 新增 2 个 xUnit 用例；完整测试套件 1002/1002 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与严格 format v1 接入。

### 2026-08-13：8.5 token 校验 undo/redo（部分）

- `RoadGraphV3Controller` 增加 `TryUndo(GraphStateToken, ...)` / `TryRedo(GraphStateToken, ...)` 重载，仅在调用方 token 与当前状态完全匹配时执行，过期 token 无副作用失败。
- 新增 2 个 xUnit 用例；完整测试套件 1004/1004 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与严格 format v1 接入。

### 2026-08-13：8.6 真实场景根节点（部分）

- 新增 `Scripts/Road/V3/RoadGraphV3System.cs`：Godot `Node2D` 根节点，`_Ready` 时用 `user://saves-v3` 与可选 `RoadConfigV3` 构造 `RoadGraphV3Application`，维护静态 `Instance`，并暴露 `Controller` / `Revision` / `CurrentNodeCount` / `CurrentEdgeCount` / `CurrentGeometrySegmentCount` / `CurrentSelfLoopCount` / `CurrentRoadTypeCounts` / `CanUndo` / `CanRedo` / `ClearHistory` / `CurrentSlotID` / `HasCurrentSlot` / `CurrentSlotSummary` / `CurrentSlotDisplayName` / `CurrentSlotTimestamp` / `CurrentSlotOccupant` / `CurrentSlotIsComplete` / `CurrentSlotIsCorrupt` / `CurrentSlotIsAbsent` / `CurrentSlotIsUnsafe` / `CurrentSlotIsForeign` / `CurrentSlotIsOperable` / `CurrentSlotManifest` / `CurrentSlotFormatFamily` / `CurrentSlotSchemaVersion` / `CurrentSlotPayload` / `CurrentSlotCityName` / `CurrentSlotPopulation` / `CurrentSlotFunds` / `CurrentSlotThumbnailFile` / `CurrentSlotHasThumbnail` / `CurrentSlotFileCount` / `CurrentSlotHasFiles` / `CurrentSlotFiles` / `GetCurrentSlotFile` / `CurrentSlotHasRoadNetwork` / `CurrentSlotRoadNetworkJson` / `CurrentSlotFileNames` / `CurrentSlotTotalBytes` / `ListUsableSlots` / `HitProvider` / `ToolState` / `TryAddNode` / `TryAddEdge` / `TryBuild` / `TryBuildFromPolyline`（含 snapRadius）/ `TryUpgradeEdges` / `TryChangeRoadType` / `TryRemoveEdge` / `TryRemoveEdges` / `TryUndo` / `TryRedo` / `TryAutosaveCurrent` / `SaveCurrent` / `SaveAs` / `Load` / `DeleteCurrentSlot` / `GetManifest` / `GetPayload` 便捷属性。
- 已将 `RoadGraphV3System` 节点加入 `Scenes/MapTest.tscn`，并挂载默认 `Scenes/road_config_v3.tres`、`RoadGraphV3Renderer` 与 `RoadGraphV3InputHandler` 子节点；Godot 编辑器中场景加载成功，冻结运行无 stderr 错误。
- 完整测试套件 1038/1038 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：替换 V2 RoadSystem 与端到端验收。

### 2026-08-13：8.6 场景根节点工具/表现便捷入口（部分）

- `RoadGraphV3System` 新增 `TryUpgrade` / `TryRemove` / `TryRequestPresentation` / `BuildDefaultSurfaceSnapshot` / `TryApplyPresentationFullReset` 转发入口，供工具与渲染层直接消费。
- 完整测试套件 1146/1146 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误；Godot 编辑器加载 `MapTest` 并冻结运行 1 帧无新增 stderr 错误。
- 尚未完成：替换 V2 RoadSystem 与端到端验收。

### 2026-08-13：8.6 场景根节点加载/新建便捷入口（部分）

- `RoadGraphV3System` 新增 `LoadIntoCurrent` / `NewCity` / `List` 转发入口，补齐场景层对 Load 生命周期与槽列表的直接访问。
- 完整测试套件 1162/1162 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误；Godot 编辑器加载 `MapTest` 并冻结运行 1 帧无新增 stderr 错误。
- 尚未完成：替换 V2 RoadSystem 与端到端验收。

### 2026-08-13：8.6 场景 Load 后应用 presentation full-reset（部分）

- `RoadGraphV3System` 在 `Load` / `LoadIntoCurrent` 成功后构建当前 presentation 计划并应用到 `RoadGraphV3Renderer`，使场景层在 Load 后立即采用新 mesh 数据。
- 完整测试套件 1182/1182 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误；Godot 编辑器加载 `MapTest` 并冻结运行 1 帧无新增 stderr 错误。
- 尚未完成：替换 V2 RoadSystem 与端到端验收。

### 2026-08-13：8.6 场景根节点暴露 Load Preflight（部分）

- `RoadGraphV3System` 新增 `TryPrepareLoad` 转发入口，场景层可在 commit 前获取 Preflight 计划。
- 完整测试套件 1206/1206 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误；Godot 编辑器加载 `MapTest` 并冻结运行 1 帧无新增 stderr 错误。
- 尚未完成：替换 V2 RoadSystem 与端到端验收。

### 2026-08-13：8.6 场景根节点提交 Prepared Load（部分）

- `RoadGraphV3System` 新增 `TryCommitPreparedLoad` 转发入口，成功提交 Preflight 计划后应用当前 presentation。
- 完整测试套件 1207/1207 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：替换 V2 RoadSystem 与端到端验收。

### 2026-08-13：8.6 NewCity 刷新渲染表现（部分）

- `RoadGraphV3System.NewCity` 在新建城市后应用当前 presentation 到渲染器，清空旧道路 mesh。
- 完整测试套件 1207/1207 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误；Godot 编辑器加载 `MapTest` 并冻结运行 1 帧无新增 stderr 错误。
- 尚未完成：替换 V2 RoadSystem 与端到端验收。

### 2026-08-13：8.6 TryBuildFromPolyline 支持闭合折线（部分）

- `RoadGraphV3Application.TryBuildFromPolyline` 识别首尾重合的闭合折线，先添加中间拐点再 `TryClose`，避免把末点当作零长度拐点拒绝。
- 新增 1 个 xUnit 用例；完整测试套件 1208/1208 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：替换 V2 RoadSystem 与端到端验收。

### 2026-08-13：8.6 场景根节点暴露子节点访问器（部分）

- `RoadGraphV3System` 新增 `Renderer` / `InputHandler` 只读属性，供外部直接访问 V3 子节点。
- 完整测试套件 1208/1208 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：替换 V2 RoadSystem 与端到端验收。

### 2026-08-13：8.6 场景根节点刷新表现入口（部分）

- `RoadGraphV3System` 新增 `RefreshPresentation()`，供外部在普通 mutation 后主动重建当前 presentation/mesh。
- 完整测试套件 1208/1208 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：替换 V2 RoadSystem 与端到端验收。

### 2026-08-13：8.6 当前槽可用性便捷属性（部分）

- `RoadGraphV3Application` / `RoadGraphV3System` 新增 `CurrentSlotIsUsable`（等价于 `CurrentSlotIsComplete`）。
- 完整测试套件 1208/1208 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：替换 V2 RoadSystem 与端到端验收。

### 2026-08-13：8.6 当前图为空便捷属性（部分）

- `RoadGraphV3Application` / `RoadGraphV3System` 新增 `CurrentGraphIsEmpty`。
- 新增 1 个 xUnit 用例；完整测试套件 1211/1211 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：替换 V2 RoadSystem 与端到端验收。

### 2026-08-13：8.6 当前槽缩略图字节便捷属性（部分）

- `RoadGraphV3Application` / `RoadGraphV3System` 新增 `CurrentSlotThumbnailBytes`。
- 完整测试套件 1211/1211 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：替换 V2 RoadSystem 与端到端验收。

## 执行顺序

### 阶段 8：第三代规范存储、环路、分级与集成

<a id="v3-road-graph8.0"></a>

- [ ] **8.0 固化 RoadGraph 数值、容量与确定性基础**
  - 当前问题：当前 V2 运行时主要检查 `float.IsFinite`，但极大有限坐标仍可使距离平方、bounds、bucket 坐标和长度累计溢出；`_nextID++` 没有 checked reservation。交点候选按浮点参数/集合遍历处理，节点身份吸附和交点 epsilon 尚未隔离；line 合并若沿用普通 float cross/近似角度会吞掉 1 ULP 折点；圆弧也没有逐 bit full-turn 格式。近似相交代表坐标、`-0`、Edge 方向和重复 canonicalize 结果没有唯一契约。
  - 修改：先写失败回归，再建立 mutation 与 V3 format v1 Load 共用的 `RoadNumericPolicy` / `RoadGraphCapacity`，限制坐标/控制参数、单段/单 Edge/全图长度、实体/geometry/索引引用和 mutation 最坏候选；距离与累计使用受检 double 中间值，ID 在写图前 checked 预留。明确 grid/angle snap 在 RoadGraph 外，`NodeSnap` 一次解析 mutation 前 anchor，intersection cluster 后置且不得复用 `NodeSnapRadius`。line primitive 使用 overflow-safe `Orient2D == 0` 与同向 `DotSign > 0` exact-sign 谓词；`CircularArc` 只把逐 bit canonical binary32 `+/-Tau` 认作 full-turn，并让 `End` / `GetPosition(1)` 直接复用 `Start`。按指南 6.4 用对称 witness、稳定 key、connected component 与最大 cluster diameter 生成交点；规范 `-0` 和 primitive 链，canonicalizer 对自身输出必须为空 delta。
  - 关联：`v3-save-system:2.2` 消费本项容量策略并负责磁盘字节与解析预算；这不是本项的前置依赖。
  - 集成负责人：`v3-road-graph`。
  - 验证：坐标/半径/曲率/长度恰好在界内外、平方/累计溢出、fragment/ref 上限、一次 mutation 最坏拆分、ID 恰好可预留/耗尽、`-0`；水平/垂直/斜线合并、回头/重叠、1 ULP 折点和坐标上限；`+/-Tau`、`BitDecrement(Tau)`、`BitIncrement(Tau)`、full-turn 反向/拆分/往返；Node 最近距离与 ID tie-break、无关近邻、候选枚举扰动、近似交点链式聚类/歧义、已有 Node 优先、canonicalizer 幂等和 V3 format v1 往返。分别断言“相同 ID 输入逐值确定”和“不同历史只需 ID 重命名后等价”。
  - 验收：任何超限或歧义在图/ID/事件变化前返回结构化错误；合法结果没有 NaN/Infinity/负 ID/`-0`，full-turn seam 逐值闭合、1 ULP 折点不被近似吞并、附近无关 Node 不被 cluster 吸附、共享交点只写一个规范坐标；同一请求重放和存档往返稳定，规范化第二次不产生变化。

<a id="v3-road-graph8.1"></a>

- [ ] **8.1 建立 endpoint-role incidence 与 self-loop 图基础**
  - 当前问题：`EdgeRef` 只有 Edge ID 和邻居 Node ID；`AddEdge`、恢复和不变式拒绝相同端点，无法区分 self-loop 在同一节点上的 A/B 端，当前 detach 也只移除首个同 ID 引用。
  - 修改：先写失败回归，再引入 `EdgeEndpoint` / `EdgeIncidence`；degree 按 incidence 计数，自环注册 A/B 各一次并贡献 2；允许同一节点对的平行 Edge；为六类 `RoadGeometrySegment` 增加不降级、reverse twice 逐值稳定的反向契约。self-loop 只比较当前链及其原生 reverse，以版本化 primitive kind + schema 字段 binary32 typed token、规范角度/heading、`-0 => +0` 和明确 IEEE total order 选择方向；排除 ID、RoadType、JSON、Length/Bounds、显示采样与 fragment。重写邻接、detach/rebuild、切线和 diagnostics。
  - 依赖：`v3-road-graph:8.0`。
  - 验证：普通边、自环、两条平行边、自环加支路、六类及混合链双向反向、`+/-0`、周期角度、full-turn CW/CCW、JSON 格式与 ID 扰动、重复 detach、恢复重建、缺失/重复端接和空间引用一致性；非环 Edge 满足 `NodeAID < NodeBID`，self-loop 正反 key 唯一且 reverse twice 稳定。
  - 验收：self-loop 可作为一等 GraphEdge 存活于图、查询和不变式；任何拓扑算法不再用 `GetNeighborIDs().Distinct()` 代替 incidence。

<a id="v3-road-graph8.2"></a>

- [ ] **8.2 形成最大连续 Edge 并移除 RoadGroup**
  - 当前问题：`SubmitPolyline` 为非共线 waypoint 创建独立 Edge，`SubmitPathCore` 为每个 `NativePathPiece` 创建 Edge；`TryMergeAtNode` 只合并共线同 Group 边，导致一次单位输入、原生段边界和两次提交接缝都成为拓扑边界。当前索引按完整 geometry AABB 填 bucket，`FindClosestEdge`/矩形选择又可能先聚合 Edge ID 再扫描整条 geometry；压缩后局部查询会随同一 Edge 的远端长度或段数退化。
  - 修改：先写失败回归，再建立 geometry/graph 两层 canonicalizer。geometry 层按 8.0 的 exact-sign 规则合并 line，其他原生曲线只在存在严格精确合并契约时合并，绝不采样降级；self-loop 先以 topology seam 根化为线性 geometry 数组，尾/首是禁止合并或循环移位的硬边界。graph 层按受影响 Node ID 排序，merge key 不含提交来源，二 incidence 同语义节点无论转角或几何类型都合并，远端相同则形成 self-loop。固定拆分保留 A 侧原 ID、合并保留最小 Edge ID 和纯环最小 Node ID seam。删除 `RoadGroup`、`GraphEdge.GroupID`、Group API、提交 GroupID 和 Group 变更摘要，Debug 指标由 `v3-ui:1.3` 替换。空间索引改为 `(EdgeID, GeometryIndex, ParameterRange, ConservativeBounds)` query fragment：bounds 闭合保守包含端点，点命中按左闭右开所有权，只有非环末 fragment 拥有 `t=1`，primitive join 与 self-loop seam 规范为唯一 `RoadLocation`；半径/矩形/交点查询保留 fragment 身份到限定参数精确测试后才聚合 Edge，索引仍可重建且不持久化。
  - 依赖：`v3-road-graph:8.0`、`v3-road-graph:8.1`。
  - 集成负责人：`v3-road-graph`；V3 format v1 与旧持久化代码删除属于 `v3-save-system:2.1`，DebugPanel 属于 `v3-ui:1.3`。
  - 验证：同向单位 line 折叠、反向/回头/折角 line 保留、非共线折线、六类复合几何、跨提交延伸、交叉拆分、T 形支路删除、纯环 seam 两侧共线 line 保留而数组内部合并、环路退化、正反输入、canonicalizer 幂等、ID 规则、无 Group 源码/API 和完整不变式；fragment cut、primitive join、非环 B 端、self-loop seam、相切圆/矩形各恰好一次命中；固定窗口命中超长 Edge 首/中/尾，只增加远端 line 长度或远端 geometry 数，记录 bucket、fragment candidate/exact test、聚合 Edge 与时间。
  - 验收：N 个同向无分支单位 line 得到 2 Node、1 Edge、1 个起点到终点的 line geometry；折角和复合曲线得到 1 Edge 与不可约原生链；rooted self-loop 不跨 seam 压缩或移动 Node；每次成功 mutation 后不存在合法数组内部可精确合并的 primitive，也不存在除 semantic boundary/loop seam 外的可合并二 incidence 节点；边界命中无漏失/重复，局部 exact geometry 访问不随远端增长，索引超限不回退全图扫描。

<a id="v3-road-graph8.3"></a>

- [ ] **8.3 支持闭合、自交路径及确定环路规范形**
  - 当前问题：`ValidatePolyline` / `ValidateNativePath` 将首尾闭合判为重复点，交点规划只比较 incoming 与 existing；当前图不能形成简单环、棒棒糖、两路口环或八字形的稳定表示。
  - 修改：先写失败回归，再允许正长度首尾闭合和 8.0 定义的精确 full-turn 原生圆弧；通用 `Start ~= End` 退化门禁只对该格式例外。统一 incoming/incoming 与 incoming/existing 的离散交点、相切和 overlap 规划；离散自交提升为 junction，连续自重叠返回结构化 `SelfOverlap`；按指南第 5 节生成 rooted seam self-loop、parallel Edge 或 junction 间 arc，并以 8.1 的 typed geometry key 固定 self-loop 正反方向。
  - 依赖：`v3-road-graph:8.1`、`v3-road-graph:8.2`。
  - 集成负责人：`v3-road-graph`；玩家闭合草稿属于 `v3-tool-input:2.0`，闭合 ribbon 属于 `v3-grid-rendering:2.0`。
  - 验证：简单环、单 junction 环、两 junction 环、棒棒糖、单交点八字形、多交点路径、完全回走、自重叠、环与既有路交叉、覆盖重提和操作顺序稳定性。
  - 验收：所有合法环路达到指南规范格式；非法重叠无 Node/Edge/ID/事件副作用；parallel Edge 不因端点相同被误判重复。

<a id="v3-road-graph8.4"></a>

- [ ] **8.4 在规范 Edge 上建立 RoadType 与类型化提交**
  - 当前问题：规范化 Edge、提交 API 和 merge key 尚无道路等级；若先把类型塞进碎片化 Edge，后续存储重构会重复改写并产生不稳定语义边界。
  - 修改：按指南固定 `Dirt`、`Street`、`Arterial`、`Highway`；类型只保存在 Edge，构造不提供静默默认值；增加显式 `RoadBuildRequest`，新几何使用请求类型、既有拆分继承原类型，类型进入 merge key，完全覆盖不转为隐式改造。
  - 依赖：`v3-road-graph:8.2`、`v3-road-graph:8.3`。
  - 验证：构造/非法枚举、开放与闭合路径、六类几何、同/异类型接续与交叉、semantic boundary、完全覆盖、部分重叠和失败无副作用。
  - 验收：每条活动 Edge 有且仅有一个合法类型；不同类型边界保留，同类型且无结构边界的相邻 Edge 必须合并。

<a id="v3-road-graph8.5"></a>

- [ ] **8.5 实现改造后重归一化与统一事务摘要**
  - 当前问题：图 API 只能增删 Edge；类型改造可能消除 semantic boundary 并触发 Edge/Node 合并，单纯 `UpdatedEdgeIDs` 或“ID 永远不变”无法表达真实结果。当前可变字典也无法让后台保存 O(1) 捕获稳定状态，revision、事件 sequence 和外部 load 后的图身份尚未分离。
  - 修改：把稳定 `RoadGraph` facade 的权威状态封装为不可变 `RoadGraphRevision` root，实体、邻接、incidence、query fragment、空间索引和 diagnostics 随 mutation plan 一次构造/验证后原子替换，发布后不原地修改。root 必须以持久化映射/固定页/copy-on-write 等结构共享未触碰的子树、页和 geometry，只复制受影响拓扑、空间引用及到 root 的路径；可变 builder 不得越过发布边界，后台 snapshot 和历史淘汰后释放所有权，禁止普通小编辑深拷贝全图或由永久 revision 表保留旧 root。增加全量预检的 `ChangeRoadType`；从 mutation plan 生成含完整前后实体、`BeforeRevisionID` / `AfterRevisionID` 的可逆 `RoadGraphDelta`，以一次 `GraphChanged` 发布排序去重的 created/removed/updated、`IsFullReset` 和单调 `ChangeSequence`。同一 `GraphLineage` 内 revision allocator、ID watermark 和 sequence 不回退/复用；undo/redo 以完整 `(LineageID, DomainRevisionID, ChangeSequence)` token 校验方向，恢复内容 revision 但分配新 sequence；外部 Load/full reset 创建新 lineage、精确采用 prepared payload 的合法 `nextID`，允许低于旧图。发布期间拒绝 mutation 重入，订阅者异常隔离且不回滚 root；异步消费者使用自己的完整 generation token。V3 只提供 `GraphChanged`，直接删除逐 Edge/`GraphCleared` 事件；NoChanges 不换 root、不推进 ID/revision/sequence。
  - 依赖：`v3-road-graph:8.2`～`8.4`。
  - 集成负责人：`v3-road-graph`；工具生命周期属于 `v3-tool-input:2.2`，历史 admission/容量属于 `v3-tool-input:2.3`。
  - 验证：空集、重复/失效 ID、混合当前类型、NoChanges、四类互转、semantic boundary 消失、环路改造；不可变 root 旧引用不变、O(1) snapshot capture、未触碰 Entity/geometry/索引页引用相同且被触碰页不共享可写别名；在固定局部 mutation 下把远端图从 1k 扩到 10k/100k geometry，记录提交时间、分配量、复制页/实体数和旧 root 释放后的保留内存，防止 O(全图) 回归；事件内不变式；`R0/S0 -> edit R1/S1 -> undo R0/S2 -> redo R1/S3`、错误方向/旧 sequence/重复 delta、undo 后分叉、full reset 前 token、加载较小 `nextID` 后首个分配、重复 load/save；mutation 重入、首个/中间订阅者抛错、异步过期结果、诊断快照，以及所有 mutation 的 delta 正反应用。
  - 验收：批次全有或全无，活动 root 一次替换且旧 root 永不被修改；普通局部编辑的复制/分配由受影响拓扑、fragment 和 bucket 页决定，远端图增长不导致线性全图复制，未触碰 immutable 数据跨 revision 共享且无 alias mutation；消费者只观察最终规范图且每次 commit sequence 唯一递增；错误 token 返回 `StaleGraphState` 无副作用；同 lineage 不复用 revision/ID，外部 load 换 lineage 并精确采用槽内 watermark；失败和 NoChanges 不污染 root、事件、allocator 或历史；订阅者不能重入或回滚图，保存可 O(1) 捕获 root，DebugPanel 可 O(1) 读取同一事务计数。

<a id="v3-road-graph8.6"></a>

- [ ] **8.6 完成第三代道路系统端到端评估**
  - 当前问题：领域测试不能证明规范存储、环路、类型选择、改造手势、混合渲染、V3 独立存档和命名槽在真实主场景共同成立。
  - 修改：按 `docs/manuals/road-system-v3-gen.md` Phase 8 建立组合契约，并把最终证据写回该指南附录 D。
  - 依赖：`v3-road-graph:8.0`～`8.5`、`v3-save-system:2.1`～`2.3`、`v3-grid-rendering:2.0`～`2.3`、`v3-tool-input:2.0`～`2.4`、`v3-ui:1.1`～`1.4`。
  - 集成负责人：`v3-road-graph`。
  - 验证：串行运行完整自动化和构建；执行真实 `MapTest` 的连续折线、跨提交延伸、精确 full-turn、简单环/棒棒糖/两路口环/八字形、支路删除重归一化、四类型建造与改造、token 防护 delta 撤销重做、不可变 root 结构共享/释放、V3 family/version format v1 有界往返、V2 根 canary 隔离、跨进程并发/自动存档、损坏/超限/ENOSPC 拒绝、publish/delete 中断恢复、成功/提交前失败/observer warning 且不存在提交后关键表现失败结果的 Load 生命周期、共享表面命中、混合 junction patch、presentation barrier、Vulkan 视觉、junction-dense/geometry-dense 存储与运行性能和 Windows 导出边界。
  - 验收：全部硬门禁有持久证据；TrafficGraph、A*、拥堵、高程道路和其他 V3 排除项不参与完成判定，也不得被宣称已实现。

## 暂不执行

### 交通模拟

- 延期原因：V3 先完成领域合法的 self-loop、parallel Edge、RoadType 和统一事务摘要；模拟层不能反向收窄这些契约。
- 保持现状：本路线图不实现 `TrafficGraph`、寻路、速度、容量或拥堵。
- 重新开启条件：`v3-road-graph:8.6` 完成后，由根层 `docs/todo/traffic-simulation.md` 定义模拟映射。

## 已解决基线

- [x] **V2 道路数据层不依赖输入层方向或网格概念。** 任意角度直线、折线和结构化非法路径拒绝已有自动化保护。
- [x] **V2 原生曲线、二维交叉、查询、删除事务、渲染和存档已通过最终验收。** 这些是 V3 需要以新接口重新验证的玩家能力基线，不要求复用 V2 代码或读取 V2 数据。
- [x] **V2 规模基线已记录。** V3 性能门槛必须使用同机、同口径对照，不能把后台总耗时误报为主线程无卡顿。

## 完成标准

1. 8.0～8.5 的数值、容量、incidence、连续 Edge、环路、RoadType、不可变事务和 delta 契约均通过各自自动化及性能门禁。
2. V3 runtime、format v1、renderer、工具、UI 和事件消费者只使用一套新契约；旧 Group/API/事件/DTO/writer 已删除，不存在兼容适配器、双写或生产路径选择。
3. `v3-save-system`、`v3-grid-rendering`、`v3-tool-input` 与 `v3-ui` 的依赖项全部通过自身验收。
4. 8.6 在真实 `MapTest`、Vulkan、10k 门槛、100k 压测和 Windows 导出环境完成最终组合验证，证据写回 V3 指南附录 D。
5. `v3-road-graph:8.6` 是唯一最终集成负责人；交通模拟和其他明确排除项不阻塞 V3。
