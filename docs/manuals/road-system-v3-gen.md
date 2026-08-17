# 第三代道路系统迭代指南

> 文档状态：实施中；Phase 1～4 与 Phase 6 已完成，Phase 5 的 `v3-save-system:2.1`～`2.2` 已完成、`2.3` 部分实现，Phase 7 的 `v3-grid-rendering:2.1`、分级 ribbon、六分量表现 token、同代四类 surface、canonical `RoadLocation`、基础 surface 空间索引、Junction Patch、普通 mutation stalled/retry、拆除与 RoadUpgrade surface/token admission，以及类型化建造的选择/会话冻结/显式提交子切片已完成，Load 协作参与者部分接入；当前入口为 RoadType/RoadUpgrade UI、类型化建造的表现门禁、其余道路命令 admission、排队 continuation 及完整故障/性能矩阵
>
> 编写日期：2026-08-15
>
> 事实基线：2026-08-17 当前工作树中的 `Scripts/Road/`、`Scripts/Tools/`、`Scripts/UI/`、`Scripts/Core/Save*`、道路与存档自动化、Godot MCP 历史运行时证据、本轮 CLI/Vulkan/导出复验、V2 性能基线及 [第二代道路系统迭代设计指南](road-system-v2-gen.md)。
>
> 路线图入口：[第三代道路系统路线图](../todo/v3/README.md)；`v3-road-graph:8.0`～`8.6` 负责领域实现和最终集成，跨系统工作分别记录在 `docs/todo/v3/` 的 owning system 文档中。

> 当前实施记录（2026-08-15）：Phase 1～4 已完成 mutation 数值/容量门禁、endpoint-role incidence、self-loop/parallel Edge、六类原生方向与权威锚、最大连续 Edge、半开 query fragment locality、RoadGroup 移除、closed/self-intersection、四类 `RoadType`、原子 `ChangeRoadType`、不可变 `RoadGraphRevision` root、统一 `GraphChanged`/可逆 delta，以及 lineage/domain revision/change sequence 身份。Phase 5 已完成独立 `user://saves-v3` 根、严格 `simple-cities-v3` format v1、有界 streaming reader、同句柄完整性校验、PNG 预算、五类 occupant、publish/delete descriptor、tombstone、OS 根锁和 digest 恢复矩阵；`SaveOperationCoordinator`、结构化 token/state/result、手动优先与单 pending autosave、取消/退出收敛，以及 RoadGraph + 空工具/history + renderer mesh/四类 surface + 槽目标的 aggregate Load 已实现，等待 gate 时外部取消不会再产生占锁 lease。Phase 6 的 delta/token 与 entry/估算字节双预算已完成。
>
> Phase 7 已部分实现普通与 Load 共用的 closed ribbon、循环 seam join、纯 self-loop seam marker 隐藏，以及两路口环、八字形和删除支路后的 seam 重定位；`v3-grid-rendering:2.1` 已完成四类 `RoadTypeStyle` 资源、严格唯一覆盖/查询、生产 `.tres` 往返与场景启动校验，`2.2` 已让普通 rebuild 与后台 Load preparer 从同一不可变值快照向单 mesh 写入 per-edge width/color，建立六分量 `RoadRenderToken`，并从真实 mesh triangle 同步发布带 matching token 的不可变 `EdgeRibbon` surface。显示 sampler 为每个可见 span 保留源 `GeometryIndex + ParameterStart/ParameterEnd`，普通与 Load triangle 用半开所有权把 geometry join、开放 B 端和 self-loop seam 唯一映射为 canonical `RoadLocation`。degree-1 Node 现按相邻 `RoadTypeStyle.Width / 2` 生成解析 `RoadSurfaceDisc`，以同一宽度/颜色绘制端点 marker，并把 A/B `TerminalCap` 命中固定映射到 canonical 参数 `0/1`；端点高亮也使用相同样式半宽。两条不同 Edge、不同 `RoadType` 的 degree-2 Node 现从实际可见 ribbon 端截面生成同源 `SemanticJoin`，degree≥3 Node 则由固定量化的 Clipper2 `JunctionPatch` 覆盖。patch 以 exact incidence 顺序、RoadType/Edge ID 同值规则与相邻边界弧长中点划分 owner sector，self-loop A/B 分别参与；每个 triangle 携带稳定 Node/Edge/Endpoint、sector 和 canonical endpoint `RoadLocation`。基础 surface 查询由构造期不可变 AABB 层级统一索引 triangle 与 disc；普通 rebuild 与 Load worker 共用 join/patch helper，mesh triangle、surface triangle、颜色、索引、AABB 数据和 token 同代发布，degree≥3 静态圆形 marker 已移除。普通 rebuild 现在复用同一纯 preparer，只重新采样 created/updated Edge；失败绑定完整 desired token、attempt 与异常信息，保留上一代完整 mesh/surface/cache 但让 provider 拒绝 hit，并可在同一 token 上显式重试。拆除与 RoadUpgrade 的 hover、连续轨迹与 Shift 矩形选择现统一消费 `IRoadSurfaceSelectionProvider`；各自会话冻结完整 render token，pending、stalled 或被新请求取代时立即失效并清空选择，确认前还校验当前 graph 的 `FacadeID` 与 `ChangeSequence`。RoadUpgrade 另冻结目标类型，通过单次 `ChangeRoadType()` 与一条历史提交批量改造；NoChanges、取消和旧 token 均无副作用。`RoadBuilder.SelectedRoadType` 默认 `Street`，placement 会话开始时冻结合法类型；合法切换取消未提交会话，full reset 清空 placement/removal/upgrade/history 与 overlay，但保留 `CurrentTool` 和选择。`v3-save-system:2.3` 及其余 Phase 7 协作项仍开放，因为仍缺 RoadType/RoadUpgrade UI、类型化建造的表现门禁、排队 continuation、其余道路命令 admission、缩放/重建视觉矩阵、平行 Edge 完整工具矩阵与故障/性能矩阵。
>
> 完整自动化当前为 833/833，RoadUpgrade 聚焦组合为 17/17，类型化建造聚焦组合为 60/60，拆除/history/surface/renderer 聚焦组合为 51/51；Debug 与 `ExportRelease` build 均为 0 错误，各有 1 条既有 `NU1900`（当前环境无法访问 NuGet 漏洞数据源），Roslyn compiler/analyzer 与 GDScript workspace scan 为 0 diagnostics。隔离的 `road_render_token_runtime_contract.gd` 与 `road_input_strategy_runtime_contract.gd` 均输出 PASS：除既有四类 surface aggregate、普通 mutation 三次 attempt、拆除同代发布和四类建造外，输入契约还验证 RoadUpgrade 目标类型冻结、连续/矩形选择、NoChanges、旧 token、semantic boundary 合并、单次 undo/redo，以及 active upgrade 上的 full-reset Load。Godot MCP 进一步验证连续改造 sequence `4 -> 5`、异类型接续从 `6 Edge` 合并为 `5 Edge`、undo/redo 为 `6 -> 5`；真实 Save/Load 后 `CurrentTool = RoadUpgrade`、`SelectedRoadType = Arterial`，session/selection/preview/undo/redo 全空，`5 Edge` 与 matching token ready。此前混合锐角 Load 发布 `10 ribbon + 2 SemanticJoin + 4 JunctionPatch + 5 cap = 21` 个 primitive、`38 vertices / 5 markers`，两路口环普通/Load 与删除支路后分别为 `4 Edge / 38 vertices / 2 markers / 20 primitives` 和 `2 Edge / 21 vertices / 1 marker / 14 primitives`。测试槽、隔离用户目录和日志已清理，editor 无新增错误且 DAP `stderr`/`console` 为空。headless 的 Windows root certificate store error 与缺少 `ToolManager.Instance` warning 为既有环境/夹具输出。本轮未重跑 10k/100k；此前 TerminalCap 的 1k Vulkan 数据与第 11 节分级 ribbon 规模数据继续作为历史证据。Phase 8 附录 D 保持为空。
>
> 性能与构建收口复验（2026-08-17）：`UniformGridInvariantTests + RoadGraphPersistenceV3Tests` 为 33/33，完整自动化仍为 833/833；恢复缺失的 Clipper2 NuGet 缓存后，Debug 与 `ExportRelease` build 均为 0 警告、0 错误。Vulkan 10k 硬门的 camera/preview/highlight P95 为 0.461/0.482/0.491 ms，Load 与 renderer rebuild 为 648.221 ms；独立 12k/20k/40k/60k/80k/100k 重建为 779.503/1193.439/2428.374/3431.815/4463.697/5129.679 ms，100k 三类 P95 为 0.660/0.658/0.675 ms，各级均 PASS、静态 renderer 节点为 2。Release C# 10k/100k 多交叉为 7.848/8.363 ms。renderer lifecycle、V3 综合、render token、RoadType style/mesh、输入、闭环和六类原生几何运行时契约均 PASS；Windows Desktop QA release 导出退出码为 0，导出包的 writable `user://` manifest/hash/删除契约也 PASS。当前会话未暴露 Roslyn/Godot MCP 调用工具，因此没有刷新历史 MCP/DAP 证据；headless editor 在默认主场景可正常启动的同时仍记录启动期 main-scene UID 提前解析错误，ImGui 导出插件也按自身禁用导出分支打印预期 GDExtension error，这两项与两个既有损坏 QA 槽警告均原样保留。

---

## 1. 结论先行

第三代首先重构道路的**规范存储单位**，然后才在这个稳定单位上增加道路分级。一个 `GraphEdge` 不再对应一次点击、一个网格单位或一个原生几何段，而是对应两个结构边界之间的最大连续道路：

```text
路口或端点 A
    -> Line / Bezier / Arc / Clothoid 等有序几何链
    -> 可包含任意数量的直行和转弯
路口或端点 B
```

只要中间没有真实交叉、终点或道路属性边界，直路、折线和曲线都只保存为一条 Edge。几何复杂度由 `GeometrySegments` 表达，不再通过额外 `GraphNode` 和 Edge 表达。

V3 是一次**破坏式架构替换**，不是对 V2 的增量升级。本文中的 `RoadGraph`、`GraphEdge` 等名称描述领域职责，不要求保留现有类型、文件布局或调用签名；实现可以重写整个道路运行时、应用装配和存档系统。V3 只保留经本文重新声明的玩家能力与领域不变式，不保留任何源码、二进制、事件或数据格式兼容性。`RoadGroup`、旧 `AddRoad()`、逐 Edge 事件、旧 DTO 和旧恢复入口可以直接删除，禁止为它们增加适配器、双写、feature gate 或长期并行路径。

V3 的交付顺序必须是：

1. 将节点邻接从“邻居节点引用”提升为可区分 A/B 端的 incidence 模型。
2. 支持自环和同一节点对之间的平行 Edge，并固定环路规范形。
3. 在每次提交、交叉、删除、拆分和恢复后形成最大连续 Edge，移除 `RoadGroup` 对拓扑的约束。
4. 在独立保存根中建立 V3 format v1，只保存规范化的 Node/Edge、原生几何链和 Edge 级 `RoadType`。
5. 在规范 Edge 上实现类型化建造、批量改造、差异化渲染、UI 和端到端验证。

V2 存档不属于 V3 输入：不扫描、不列出、不迁移、不只读加载、不通过 Save As 转换，也不覆盖或删除。V3 对自己的格式仍执行严格版本拒绝、容量门禁、原子发布、崩溃恢复和全有或全无 Load；这些是当前格式的正确性要求，不是向后兼容层。

以下能力仍不属于 V3：`TrafficGraph`、A*、速度/容量/拥堵、建造与维护费用、单向和车道、信号灯、建筑接入限制、桥梁/隧道/立交、高程层及自由曲线控制点编辑器。

---

## 2. 当前源码事实与问题边界

当前工作树已完成 Phase 3 的道路图与闭合输入垂直切片、Phase 4 的类型化建造与不可变事务、Phase 5 的 format/root/storage 切片、由真实 V3 Load 验证的 Phase 6 delta/history，并部分接入 Phase 5/7 的异步 aggregate Load；剩余边界如下：

| 当前事实                                                 | 证据位置                                                      | 对 V3 的含义                               |
| -------------------------------------------------------- | ------------------------------------------------------------- | ------------------------------------------ |
| `GraphEdge` 已持有多个连续 `RoadGeometrySegment`     | `GraphEdge.GeometrySegments`                                | 不需要发明新的道路几何容器                 |
| 折线、原生路径和跨提交接缝已归一化为最大连续 Edge      | `SubmitPathCore`、`FinalizeMutation`、`TryMergeAtNode`    | Phase 2 已完成；后续类型边界必须进入 merge key |
| query fragment 保留 geometry/参数身份到精确测试后       | `SpatialIndex.cs`、`RoadGraph.cs`、`RoadGraph.NativePathIntersections.cs` | 长 Edge 的局部查询不扫描远端 geometry |
| `GraphNode` 已保存 `EdgeIncidence(edgeID, endpoint, neighborNodeID)` | `GraphNode.cs`、`RoadGraph.PreparedTopology.cs` | 8.1 已能区分自环 A/B；后续算法必须继续按 incidence 工作 |
| 公共提交与 V3 reader 均允许 canonical rooted self-loop、parallel Edge 与离散自交 | `RoadGraph.PreparedTopology.cs`、`RoadGraph.PathSubmission.cs`、`RoadGraph.NativePathIntersections.cs`、`RoadGraph.Persistence*.cs` | Phase 3 与 `v3-save-system:2.1`～`2.2` 已完成；reader 与 mutation 共享容量和规范形门禁 |
| `RoadGroup`、Group API/事件/结果和 Group 指标已删除     | `GraphEdge.cs`、`RoadGraph.cs`、`RoadPathSubmissionResult.cs` | 生产图不再携带提交来源身份                  |
| 删除后会按受影响节点恢复最大连续 Edge                   | `RemoveEdgesCore`、`FinalizeMutation`                     | 支路移除不会遗留非结构性二度节点            |
| renderer 已从 `EdgeEndpoint` 选择首/末端切线            | `RoadRenderer.TryGetOutgoingDirection`                      | self-loop A/B 得到各自的出射方向；Phase 7 已完成基础 closed ribbon，复杂表面矩阵仍开放 |
| 闭合与自交路径已进入公共提交，连续重叠结构化拒绝           | `ValidateNativePath`、`PlanNativePathIntersections`、`RoadPlacementSession` | Phase 3 已完成；RoadType 与 closed ribbon 分属 Phase 4/7 |
| Edge 已强制携带合法 `RoadType`，提交、拆分、交叉、批量改造和归一化均传播类型 | `RoadBuildRequest`、`GraphEdge`、`SubmitPathCore`、`ChangeRoadType` | Phase 4 已完成；`RoadBuilder` 的类型选择/placement 冻结与 RoadUpgrade 输入生命周期已接入，玩家可见选择器和改造工具呈现仍属 Phase 7 |
| `RoadConfig` 已恰好提供四类合法 `RoadTypeStyle`，普通/Load renderer 以不可变值快照消费 | `RoadTypeStyle.cs`、`RoadConfig.cs`、`RoadRenderer*.cs`、`road_config.tres` | `v3-grid-rendering:2.1`、2.2 分级 ribbon、四类 surface/index、普通 mutation stalled/retry 及拆除 provider/token admission 已验证；其余工具/命令接管仍属开放的 2.2/`v3-tool-input:2.4` |
| 活动图由不可变 root、统一 delta 和完整 state token 表达 | `RoadGraphRevision`、`RoadGraph.Transactions.cs`、`GraphChanged` | Phase 4 已完成；V3 writer 可直接 O(1) 捕获 root，表现层已建立独立六分量 generation token |
| 保存以 O(1) 捕获不可变 revision，并直接写无缩进 UTF-8 流 | `IStreamingSaveable`、`RoadGraph.CaptureSnapshot`、`WriteSnapshot` | 已消除完整业务 DTO/字符串副本；async coordinator 在后台执行 serialize/hash/I/O |
| Save/Load/Delete/autosave 只公开 token 入口和结构化 state/result | `SaveOperationCoordinator`、`SaveManager.Start*`、`AutosaveController`、`PauseMenu` | 进程内排他、手动优先、pending autosave、取消与退出收敛已接入；完整表现结果仍属开放的联合项 |
| Load 以固定 buffer reader 准备 V3 root，并在同一 aggregate 中交换 graph/tool/basic mesh/四类 surface/token/slot | `V3JsonStreamReader`、`PreparedAggregateLoad`、`RoadGraph.LoadCommit.cs`、`RoadRenderer.LoadCommit.cs`、`ToolManager.LoadCommit.cs` | 基础 mesh、同源的 indexed `EdgeRibbon` + `TerminalCap` + `SemanticJoin` + `JunctionPatch` snapshot 与 matching desired/presented token 已原子接管；工具状态与关键资源故障矩阵仍开放 |
| 撤销项只保存可逆 delta、完整 token 和估算字节             | `RoadEditHistory`                                         | Phase 6 已完成；真实 V3 Load 已证明新 lineage 清空旧历史与 token |
| manifest 绑定 payload 名称、长度和 SHA-256，operation-specific transaction 绑定 publish/delete descriptor | `V3ManifestCodec`、`V3PublicationDescriptorCodec`、`V3DeletionDescriptorCodec`、`SaveSlotStore` | OS 根锁、quarantine/tombstone、digest 恢复矩阵和 cleanup-pending 已由 `v3-save-system:2.2` 完成 |

Phase 1 已把 `NodeA == NodeB`、邻接、度数、方向、查询和现有 renderer 的图基础作为同一垂直切片完成；Phase 2 已在此基础上完成最大连续 Edge、Group 移除与局部 fragment 索引；Phase 3 已让公共闭合/自交提交和共享 placement 生命周期消费同一契约；Phase 4 已让类型化建造、改造、不可变 root、delta 和事务身份消费相同规范 Edge；Phase 5 已让 V3 reader/writer、独立保存根和 async coordinator 消费 canonical root，并以 aggregate 连接 graph/tool/renderer/slot；Phase 6 已用真实 V3 Load 验证历史 lineage 边界；Phase 7 已让基础 closed ribbon 在普通 mutation 与 Load 中共享同一 seam 语义，覆盖两路口环、八字形和删除支路后的 seam 重定位，固定、校验及消费四类 `RoadTypeStyle` 生成单 mesh 分级 ribbon，建立六分量 desired/presented token，并把同源 `EdgeRibbon` triangle、解析 `TerminalCap` disc、degree-2 `SemanticJoin` triangle、degree≥3 `JunctionPatch` triangle、canonical `RoadLocation` 与不可变 AABB 查询索引纳入普通/Load 同代交换；普通 mutation 的表现失败也已具备保留旧代、provider 拒绝 hit 和同 token 重试的 stalled 门禁，拆除与 RoadUpgrade 的 hover/连续/矩形选择及确认也已消费并校验同一代 surface token。后续缩放/重建视觉矩阵、其余道路命令 admission、排队 continuation 及完整故障矩阵必须继续保留 seam、typed direction、规范交点坐标、RoadType、root/token 和失败原子性，不能重新引入提交来源身份、默认类型、伪拓扑边界、二次模糊吸附、全图 JSON 历史或整 Edge 局部扫描。

---

## 3. 术语与存储层级

### 3.1 四个不同层级

| 名称                    | 定义                                               | 是否持久化 | 是否是拓扑身份 |
| ----------------------- | -------------------------------------------------- | ---------- | -------------- |
| `RoadGeometrySegment` | 一段可求值、求切线、拆分和序列化的原生几何         | 是         | 否             |
| `GraphEdge`           | 两个结构边界之间的最大连续道路，由有序几何段链组成 | 是         | 是             |
| `GraphNode`           | terminal、junction、semantic boundary 或 loop seam | 是         | 是             |
| 临时 piece/subsegment   | 交点规划、覆盖计算和事务组装期间的算法片段         | 否         | 否             |

“一单位长道路”只能是输入采样或临时 piece，不能自动成为一个 Edge，也不应自动成为持久化 geometry。连续共线且同向、无中间结构边界的 line pieces 必须无损折叠为从整段起点到终点的一个 `LineRoadGeometrySegment`；真正的折角仍可在同一 Edge 中保存为相邻两个 line geometry。waypoint、曲线控制点、原生段接缝和网格步长也都不是 GraphNode。

### 3.2 结构边界

V3 只在以下位置保留节点：

1. **terminal**：道路真实结束，incidence 数为 1。
2. **junction/intersection**：三条及以上端接在同一点连接；二维交叉、T 形接入和自交均按 incidence 计数。
3. **semantic boundary**：相邻道路不能合并的属性边界；V3 只有不同 `RoadType`，未来高程、方向或车道契约若加入，也必须显式进入 merge key。
4. **loop seam**：没有其他结构边界的闭环必须保留一个表示起止的结构缝合点。

下面这些情况不能保留节点：

- 玩家连续铺路时的中间点击；
- 一个网格单位的起止；
- 两个原生几何段的公共锚点；
- 没有支路的锐角或平滑转弯；
- 两次提交的接缝；
- 删除支路后退化为普通连续道路的旧路口。

### 3.3 Edge 的规范不变式

每次事务提交后必须同时满足：

1. 每条 Edge 至少包含一个正长度原生几何段，几何段按 A 到 B 有序且位置连续。
2. 第一段起点匹配 `NodeA.Position`，最后一段终点匹配 `NodeB.Position`；自环允许两者为同一节点。
3. Edge 内部不能包含未提升为 GraphNode 的真实交叉或语义边界。
4. 除 loop seam 和 semantic boundary 外，不存在仍可合并的二 incidence 节点。
5. 合并不要求共线或 G1 连续；C0 位置连续即可，尖角仍是同一条 Edge 内的合法转弯。
6. 一条 Edge 的全部几何共享同一个 `RoadType`。
7. 空间索引按原生几何的参数区间建立无拓扑身份的 query fragment；压缩 Edge 不得让局部精确查询退化为扫描整条 Edge。
8. geometry 链也是规范化的：可无损合并的相邻同类 primitive 必须合并。这里的“相邻”只指根化数组中的 `(i, i + 1)`；self-loop 的数组尾和数组首隔着 loop seam，是硬边界，禁止跨 seam 合并或旋转数组寻找更短表示。V3 首先要求连续共线同向 line 合并；其他原生曲线只有在能以相同类型和参数精确表示、且不改变方向、长度或求值结果时才允许合并，禁止用采样近似减少段数。
9. 非自环 Edge 始终令 `NodeAID < NodeBID`；若拓扑操作得到相反方向，必须用原生几何反向契约翻转完整 geometry 链。geometry 的相邻端点在提交态使用同一个规范 binary32 坐标值，不能只依赖近似相等；定向后必须再次验证逐 bit 连续。

圆弧和 clothoid 不能只把可重算参数当作完整权威表示。部分圆弧除 `center/radius/startAngle/sweepAngle` 外，还保存逐 bit `Start` / `End` 锚和权威 `EndAngle`；clothoid 除起点、heading、曲率和弧长外，还保存逐 bit `End` 锚和权威 `ReverseStartHeading`。这些字段是 primitive 已承诺的 binary32 表示，不是 Phase 2/3 的 intersection cluster 重锚：split、reverse 和 codec 只能交换或继承它们，不能用三角函数或数值积分重新推导公共端点。

line primitive 的压缩不使用几何 epsilon。`Line(A, M)` 与 `Line(M, B)` 只有在公共端点逐 bit 相同，并对有限 binary32 坐标满足 overflow-safe 精确谓词 `Orient2D(A, M, B) == 0` 且 `DotSign(M - A, B - M) > 0` 时，才能替换为 `Line(A, B)`。谓词可以使用精确展开、受检整数化或等价的 exact-sign 实现，但不能使用普通 float cross、`IsEqualApprox` 或角度近似；因此偏移 1 ULP 的折点、回头和反向重叠都必须保留。

### 3.4 派生 query fragment 不是新路段

当前 `UniformGrid.InsertGeometry` 把一个 geometry 的完整 AABB 填入所有 bucket，查询又先聚合为 Edge ID，再由 `FindClosestEdge` 或矩形选择扫描该 Edge 的全部 geometry。连续存储会放大这两个问题：一条很长的斜线会用巨大矩形污染无关 bucket，一条含大量转弯的 Edge 即使只命中开头，也可能遍历整条道路。

V3 保留 canonical Edge 和原生 geometry 作为唯一事实源，但为空间查询生成可丢弃、可重建的区间引用：

```text
RoadQueryFragment(
    EdgeID,
    GeometryIndex,
    FragmentIndex,
    ParameterStart,
    ParameterEnd,
    ConservativeBounds)
```

- 直线按穿越 bucket 的参数区间或有界长度切分，不能再用整条斜线 AABB 的笛卡尔积填桶。
- 曲线按保守 bounds 和固定误差策略递归产生 fragment；fragment 只缩小候选，最终距离、矩形相交和交点仍调用限定参数区间的权威原生几何算法。
- 查询先对 fragment key 去重并精确测试，再按 Edge ID 聚合最佳结果；不能先聚合 Edge ID 后扫描该 Edge 的远端 geometry。
- fragment、BVH 节点、bucket 引用和显示采样都不持久化，不进入 `RoadGraphDelta`，也不能产生 GraphNode 或新 Edge。
- `RoadGraphCapacity` 同时限制 fragment 数、bucket 数和引用总数；超限 mutation/load 在发布前失败，不能退化成无索引全图扫描。

每个 geometry 的 fragment 参数必须严格递增、无空洞地覆盖 `[0, 1]`，且 `ConservativeBounds` 包含两端；bounds 的闭合性只用于保守候选，不代表重复拥有边界命中。点命中所有权固定为左闭右开 `[p_i, p_{i+1})`，只有非环 Edge 的最后一个 fragment 拥有最终 `t = 1`。primitive 公共边界把 `(i, 1)` 规范映射为 `(i + 1, 0)`；self-loop 的 `(last, 1)` 映射为 `(0, 0)` seam。受限 solver 先把边界结果钳到精确 cut parameter，再按所有权过滤，最后以 canonical `RoadLocation(EdgeID, GeometryIndex, Parameter)` 去重；fragment cut、primitive join、非环 B 端和 self-loop seam 都必须恰好产生一次命中。

性能门禁固定一个局部查询窗口，在同一 Edge 的首部、中部和尾部命中。保持窗口内几何不变，只增加远端长度或远端 geometry 数时，精确访问的 fragment/geometry 数必须保持有界；基准同时记录 bucket、fragment candidate、exact test 和 Edge aggregate 数，不能只记录最终 Edge 命中数。

---

## 4. 邻接、端接角色与方向

### 4.1 incidence 是度数的基本单位

Phase 1 已把现有 `EdgeRef` 替换为下面的逻辑形状：

```csharp
public enum EdgeEndpoint
{
    A,
    B,
}

public readonly record struct EdgeIncidence(
    int EdgeID,
    EdgeEndpoint Endpoint,
    int NeighborNodeID);
```

自环 Edge 在同一 GraphNode 中注册两条 incidence：一条 `A`，一条 `B`。因此：

- `Degree` 或 `IncidenceCount` 统计 incidence，自环贡献 2。
- `IncidentEdgeCount` 统计不同 Edge ID，自环只贡献 1。
- `GetNeighborIDs()` 可以继续返回去重邻居，但不能用于拓扑度数、环路遍历或交通映射。
- 删除自环必须一次移除 A/B 两条 incidence；不能复用当前只删除首个 Edge ID 的逻辑。

### 4.2 切线与原生几何反向

端接方向必须由 `EdgeEndpoint` 决定：

```text
A incidence -> firstGeometry.GetUnitTangent(0)
B incidence -> -lastGeometry.GetUnitTangent(1)
```

规范化需要把任意 Edge 定向后拼接；Phase 1 已为 `RoadGeometrySegment` 建立保留原生类型的反向契约。六类几何均满足：

- 反向后 Start/End 交换，轨迹集合和长度不变；
- 反向两次得到与原对象逐值相同的权威控制参数和锚字段；
- Bézier 交换控制点，Hermite 交换并反向端切线，圆弧反转 sweep，clothoid 正确转换朝向和有符号曲率；
- 绝不通过显示采样点重建权威几何。

其中圆弧反向交换 `Start` / `End`，以原 `EndAngle` 作为反向起始角并保留可再次交换回去的权威角；clothoid 反向交换 `Start` / `End`，以 `ReverseStartHeading` 作为新起始 heading，同时把原 `StartHeading` 的反向值保留为新的 `ReverseStartHeading`。反向链在段顺序翻转后必须逐 bit 连续；任何“数学上接近”但改变 join bit pattern 的实现都不满足本契约。

---

## 5. 环路与特殊拓扑的规范格式

### 5.1 必须支持的表示

| 场景                 | 规范 Node/Edge 形状                        | 说明                             |
| -------------------- | ------------------------------------------ | -------------------------------- |
| 无路口简单闭环       | 1 个 seam Node + 1 条 self-loop Edge       | Edge 的 A/B 都指向 seam          |
| 只有一个真实路口的环 | 1 个 junction Node + 1 条 self-loop Edge   | 环的两个端接加支路后 degree 为 3 |
| 两个路口的环         | 2 个 Node + 2 条平行 Edge                  | 两条 Edge 分别表示两个方向的环弧 |
| 三个及以上路口的环   | 每对相邻路口之间 1 条 Edge                 | 几何控制点仍留在 Edge 内部       |
| 棒棒糖形             | junction 上 1 条 self-loop + 1 条尾部 Edge | junction degree 为 3             |
| 单交点八字形         | 1 个 junction + 2 条 self-loop Edge        | 两个环各占一条 Edge，degree 为 4 |
| 多交点自交路径       | 交点之间的 Edge 集                         | 自交点必须提升为 junction        |

平行 Edge 和 self-loop 都是合法拓扑，不是重复数据。覆盖判断必须比较实际几何占据，不能仅因端点对相同就拒绝第二条 Edge。

### 5.2 loop seam 选择

纯二度闭合分量需要在合并前先选定唯一 seam，避免归一化顺序产生不同结果：

1. 若闭环包含真实 junction 或 semantic boundary，不创建额外 seam。
2. 若所有节点都可合并，保留该闭合分量中最小 Node ID 作为 seam。
3. 新建简单闭环时，第一个已吸附或新建的路径锚点参与相同 ID 规则；从 V3 payload 准备的图必须已经满足同一规范规则，否则拒绝加载。
4. seam 是存储结构，不是道路端点或路口，renderer 不绘制 endpoint/junction 标记。
5. self-loop 先从 topology seam 根化为 seam A 到 seam B 的线性 geometry 数组；数组末尾和开头禁止合并，也不得通过循环移位改变 seam。seam 两侧即使是共线同向 line 也保留两个 primitive，只有数组内部的相邻项适用 3.3 的压缩规则。
6. self-loop 在 seam 固定后仍有正反两个等价方向；只比较“当前原生链”与“反转段顺序并逐段原生反向”的链，选择规范数值 key 较小者作为存储方向。key 是带版本和 primitive kind tag 的 typed numeric token 序列：每段先验证有限值、把 `-0` 规范为 `+0`、把周期角度/heading 规范到约定区间，再按 schema 权威字段顺序写 binary32 token，并按明确的 IEEE total order 比较。圆弧 token 包含逐 bit `Start` / `End`、`startAngle` / `endAngle` 和 sweep，clothoid token 包含逐 bit `Start` / `End`、`startHeading` / `reverseStartHeading`、两端曲率和弧长。key 排除 Node/Edge ID、RoadType、JSON 文本、`Length`、`Bounds`、显示采样和 query fragment；它不试图统一不同 Bézier 参数化或同比例 rational weights。

该规则依赖稳定 ID 和规范原生参数，而不是浮点坐标极值或显示采样，因此保存、加载和同一事务重放能得到相同结果。两个不同编辑历史可能为语义相同的对象分配不同 ID；V3 保留已有身份优先，不会为了制造全图逐字相同而重编号未修改实体。跨提交顺序测试因此比较“按 ID 重命名后的拓扑/几何等价”；相同图、schema 往返和同一 delta 重放才要求 ID 与 payload 字节保持不变。

### 5.3 闭合与自交输入

公共 `SubmitPath` 必须区分合法闭合与非法重复：

- 只允许首尾锚点表示同一个节点；其他重复锚点仍按自交规划或非法重叠处理。
- 单个 360 度圆弧虽然 Start 与 End 重合，只要原生几何长度为正就不是退化段。
- 非相邻几何的离散交叉或相切按 V2 二维规则形成 junction。
- 非相邻几何的连续自重叠拒绝为 `SelfOverlap`，不能生成方向或所有权不明确的重复道路。
- `A -> B -> A` 这类完全回走不是有效闭环；应由重叠检测拒绝且无副作用。

`CircularArc` 的 full-turn 必须是精确的数值格式，而不是“足够接近一圈”：只有 `abs(sweepAngle)` 与 canonical binary32 `Tau` 逐 bit 相等时才是 full-turn；`BitDecrement(Tau)` 仍是开弧，`BitIncrement(Tau)` 越界拒绝。full-turn 的 `End` 和 `GetPosition(1)` 直接返回同一个权威 `Start` 锚，禁止再次三角计算制造 seam 偏差。反向 full-turn 交换同一个 seam 锚并翻转 sweep 符号；部分圆弧反向使用保存的 `EndAngle` 与交换后的精确锚，而不是重新以 `Normalize(startAngle + sweepAngle)` 猜测新起点。通用的 `Start ~= End` 退化检查必须给正长度 full-turn 明确例外。

---

## 6. 规范化事务

### 6.1 先规划，后一次提交

建造、拆分、删除、改造和 V3 load preparation 共用同一规范化判定。开始规划前先区分三种不得混用的吸附/聚类协议：输入策略的 grid/angle snap 在 RoadGraph 外完成；`NodeSnap` 只把 request anchor 对 mutation 前的不可变图解析为已有 Node，使用受检 double 距离、等距时取最小 Node ID，并一次解析锁定全部 anchor；随后才收集 incoming/incoming 与 incoming/existing witness 并执行 intersection cluster。`NodeSnapRadius` 表示用户意图，cluster epsilon 表示数值误差，二者禁止复用、取最大值或执行二次吸附；只有显式 snapped anchor 或几何端接 witness 能把已有 Node 带入 cluster，附近无关 Node 不参与。

```text
验证输入与属性
    -> 吸附端点但不写图
    -> 计算 incoming/incoming 与 incoming/existing 交点和重叠
    -> 按结构边界拆分原生几何
    -> 在临时 mutation plan 中组装原子 piece
    -> 删除覆盖片段并应用目标属性
    -> 对候选 Edge 执行无损 primitive canonicalize
    -> 对受影响连通区域执行 canonicalize
    -> 验证完整不变式
    -> 一次替换并发布 GraphChanged
```

任何一步失败都不得留下新 Node、拆过的 Edge、空间引用、ID 消耗或事件。

### 6.2 合并规则

对受影响节点维护按 Node ID 排序的工作队列。一个节点恰有两个 incidence 时：

1. 若两条 incidence 来自同一 self-loop，它是当前闭环 seam，保留。
2. 若来自两条不同 Edge，且两条 Edge 的 merge key 相同，则删除中间节点并拼接原生几何链。
3. merge key 在 V3 只包含 `RoadType`；`RoadGroup`、提交时间、输入策略和视觉样式不参与。
4. 两条 Edge 的远端可以相同；此时合并结果是 self-loop，不能沿用当前 `farAID == farBID` 拒绝逻辑。
5. 两条 Edge 不要求共线。它们可组成折角、复合曲线或不同原生几何类型的连续链。
6. 若 merge key 不同，节点作为 semantic boundary 保留。

删除也必须执行归一化。删除 T 形支路后，剩余两段若语义相同就重新合成一条 Edge；删除环上的最后一条外接支路后，原 junction 退化为 loop seam。这是 V3 对 V2“删除不自动合并”基线的有意取代，不是回归。

### 6.3 ID 与变更摘要

为避免无意义的全图 ID 抖动，V3 固定以下策略：

- 完全未改变的 canonical Edge 保留 ID。
- Edge 被拆分时，靠近原 A 端的第一个存活片段保留原 ID，其余片段分配新 ID。
- 多条 Edge 合并时，结果保留参与 Edge 中最小的 ID，其余 ID 被移除。
- 保留 ID 但几何或类型变化的 Edge 进入 `UpdatedEdgeIDs`。
- 移除的节点/Edge 和新增的节点/Edge 分别进入排序去重的 created/removed 集。
- 同一运行时 `GraphLineage` 内，`_nextID` 只前进，不复用因规范化、undo 或分叉消失的 ID；失败和 `NoChanges` 不推进 watermark。外部 load/full reset 创建新 lineage，并精确采用 prepared payload 中已验证的 `nextID`，允许低于旧活动图；不得取新旧 watermark 的最大值。lineage 是运行时防串用身份，不持久化。

`RoadGraphChangeSummary` 还包含 `IsFullReset` 和单调递增、永不回退的 `ChangeSequence`：普通 mutation 以及 delta undo/redo 的 `IsFullReset` 为 `false`；外部存档恢复的一次性全图替换为 `true`。三层身份必须分开：`DomainRevisionID` 标识可逆的领域内容状态，其 allocator 在 lineage 内不复用；`ChangeSequence` 标识每次成功 commit，包含 undo、redo 和 full reset，只增不减；运行时 `GraphStateToken` 至少携带 `LineageID`、当前 `DomainRevisionID` 和最新 `ChangeSequence`。delta 固定保存 before/after 实体和 revision，历史项保存下一次允许操作的完整 token；应用时同时校验 lineage、方向对应 revision 和最新 sequence，错误方向、重复重放或 full reset 前 token 返回 `StaleGraphState` 且无副作用。undo/redo 可以恢复原有内容 revision 和实体 ID，但不能回退 sequence 或 allocator watermark；例如 `R0/S0 -> edit R1/S1 -> undo R0/S2 -> redo R1/S3`。V3 payload 的 `nextID` 是新 lineage 的 allocator watermark，而不是“当前最大 ID + 1”的可重算缓存；lineage、revision 和 sequence 均不写入存档。

`GraphChanged` 是 V3 唯一事务事件；消费者对普通摘要增量处理 created/removed/updated，对 full reset 丢弃缓存并从活动图重建。领域状态、邻接和索引先原子提交，再递增 sequence 并同步发布不可变摘要。事件发布期间拒绝 mutation 重入；单个订阅者异常被隔离和记录，不能回滚已经提交的图，也不能阻止其他订阅者收到同一 sequence。异步派生任务不得只凭 sequence 发布：每个消费者还必须组合其 graph facade/scene/style/request generation，renderer 的最低完整 token 见 9.5。旧 `EdgeAdded` / `EdgeRemoved` / `GraphCleared` 不进入 V3；所有消费者随架构替换直接改用新事件。

### 6.4 数值规范、交点聚类与容量

`float.IsFinite` 只是第一道门禁。极大但有限的坐标仍会让距离平方、curve bounds、bucket 坐标、长度累计和 `_nextID++` 溢出。V3 建立 mutation 与 format v1 load 共用的 `RoadNumericPolicy` / `RoadGraphCapacity`：

截至 2026-08-14，mutation 一侧的策略、admission、实际容量 invariant、exact-sign predicate、geometry canonicalizer、确定 cluster 规划和六类 typed `ReanchorChain` 已完成；cluster/Node 的共享规范坐标会写入 split geometry 的公共端点，不通过显示采样或无类型平移猜测控制参数。format v1 reader 已通过 `v3-save-system:2.1`～`2.2` 复用同一容量与 prepared-topology 门禁。

- 所有 Node 坐标、原生控制参数、半径、曲率派生 bounds 和查询半径都受命名范围限制；距离与累计长度使用受检的 double 中间值，最终值还必须落回许可范围。
- 限制单 geometry 长度、单 Edge 权威长度、全图权威长度、Node/Edge/geometry 数、mutation candidate/split 数、query fragment/bucket/ref 数和准备态峰值估算。
- mutation plan 在分配前以 checked arithmetic 一次预留最坏情况下的新 ID；`nextID` 接近上限时返回结构化 `CapacityExceeded`，不得溢出为负数或部分消耗 ID。继续使用 32 位 ID，因为实际容量远低于其上限；V3 不为未证实的规模收益把全部 API 扩成 64 位。
- 所有写入权威状态的 `-0` 规范为 `+0`；非自环 Edge 先按 3.3 定向，再做 primitive canonicalize。canonicalizer 对自己的输出再次运行必须产生空 delta。

近似交点不能按“浮点参数排序后取第一个”决定 Node。每个交点 witness 必须携带规范化的 `(existing/new, EdgeID 或 incoming key, GeometryIndex, parameter)` provenance；pair query 对交换两条几何保持对称，并从双方求值结果生成同一个候选位置。候选先以稳定 key 排序，再把距离不超过 cluster epsilon 的 witness 建成无向图，以 connected component 形成与遍历顺序无关的 cluster；若传递闭包使 component 直径超过明确的最大 cluster diameter，则返回 `AmbiguousIntersection`，不能链式吞并远点：

1. cluster 中若含唯一既有 Node，复用该 Node 的精确坐标；若含多个不同既有 Node，整次事务以歧义失败，不能在一次铺路中偷偷合并既有身份。
2. 没有既有 Node 时，从排序后的对称 witness 选择 stable key 最小的唯一代表，并以 double 中间值、固定舍入和 `-0` 规范化得到一次性共享坐标；所有拆分 geometry 的公共端点写入该同一个值。
3. tolerance、cluster 传递性、代表点最大偏移和曲线重新锚定误差必须是命名契约并有边界测试，不能依赖 `Dictionary`/`HashSet` 枚举顺序。

确定性分成两层：给定相同存储 bit pattern、已有 ID 和 mutation request，结果 Node 坐标、ID 选择及 delta 必须相同；不同历史顺序产生的语义等价图只要求存在保持 RoadType、拓扑和原生轨迹的 ID 重命名。正向/反向输入、候选枚举扰动、canonicalizer 幂等、V3 format v1 往返和 delta 正反应用分别测试这两层契约。

---

## 7. RoadGroup 的处置

### 7.1 为什么单值 GroupID 与规范 Edge 冲突

假设玩家先铺 A 到 C，再铺 C 到 B。C 没有支路且属性相同，规范结果应是一条 A 到 B 的 Edge。但这条 Edge 同时来自两次提交，无法选择唯一 `GroupID`：

- 保留第一组会丢失第二次提交来源；
- 保留第二组会丢失第一组来源；
- 保存多个 Group 会引入 many-to-many 成员关系，整组删除会不知道该删除哪段几何；
- 禁止跨 Group 合并则继续保留无意义节点，直接违反本指南核心目标。

因此 V3 从 canonical RoadGraph 和 format v1 中移除 `RoadGroup`、`GraphEdge.GroupID`、`GetGroup`、`GetAllGroups`、`RemoveRoadGroup`、`RoadPathSubmissionResult.GroupID` 及 Group 变更摘要。这些类型和入口直接删除，不提供兼容 facade 或返回值映射。

### 7.2 用户操作语义放在哪里

- `RoadEditHistory` 已以完整 before/after 图快照表达一次用户命令，不依赖 Group。
- 拆除和改造会话按 canonical Edge ID 集提交，不需要 Group 扩张。
- `RoadPathSubmissionResult.Changes.CreatedEdgeIDs` 表达本次提交留下的 Edge；若规范化合并到既有 Edge，则该 Edge 出现在 `UpdatedEdgeIDs`。
- DebugPanel 用 Node、Edge、geometry segment、self-loop 等结构指标取代 RoadGroup 数量。
- 若未来需要审计来源，应建立独立、非拓扑的命令日志；不能把 provenance 重新塞回 Edge 合并键。

---

## 8. RoadType 与属性边界

### 8.1 稳定领域值

```csharp
public enum RoadType
{
    Dirt = 0,
    Street = 1,
    Arterial = 2,
    Highway = 3,
}
```

JSON 使用 `dirt`、`street`、`arterial`、`highway`，不保存展示名或枚举整数。V3 只定义名称和视觉样式；速度、容量、维护费和通行规则留给后续 owning system。

### 8.2 类型化建造

`RoadPath` 继续只描述几何。提交使用显式请求：

```csharp
public sealed record RoadBuildRequest(RoadPath Path, RoadType RoadType);
public RoadPathSubmissionResult SubmitPath(RoadBuildRequest request);
```

建造命令只给新占据的几何赋目标类型：

- 完全覆盖既有几何仍返回 `FullyCovered`，不暗中改造。
- 部分覆盖时，既有覆盖段保持原类型，新几何使用请求类型。
- 新旧几何同类型且相接时，规范化可跨提交合并。
- 新旧几何类型不同时，在接缝保留 semantic boundary。

### 8.3 批量改造不是纯属性原地更新

```csharp
public RoadTypeChangeResult ChangeRoadType(
    IEnumerable<int> edgeIDs,
    RoadType targetType);
```

命令先排序、去重并完整预检。缺失 ID、非法类型或空选择整批失败；全部已经是目标类型时返回 `NoChanges`，不发事件、不入历史。

改造的选择粒度是整条 canonical Edge，即一个路口/端点到下一个边界。类型变化后必须再次规范化：若相邻 semantic boundary 因类型相同而消失，Edge 可以合并，ID 和 Node 集也可按 6.3 改变。因此 V3 不再承诺“改造永远保持 Edge ID”；它承诺的是一次原子事务、完整变更摘要和一次可逆历史记录。

---

## 9. 输入、UI 与渲染

### 9.1 闭环建造体验

`IRoadInputStrategy` 继续只负责吸附和几何草稿。闭环能力属于共享 `RoadPlacementSession`：

- 指针回到首锚点身份半径内时显示闭合预览并精确吸附首点。
- 确认后提交首尾相同的显式闭合 `RoadPath`，不额外添加零长度段。
- 右键、切换工具或暂停取消整个未提交闭环。
- 简单闭环、单交点八字形和非法自重叠必须得到不同的结构化结果。

### 9.2 类型选择与改造

由 `RoadBuilder` 或独立 `RoadToolState` 保存 `SelectedRoadType`，初始为 `Street`。铺路会话开始时冻结类型；切换类型先取消未提交会话。`ToolContextPanel` 提供四段式名称与颜色选择器，ConstructionDock 提供独立道路改造工具。

改造沿用“先选择、后提交”，但选中的 ID 只保证在提交前有效。成功后 UI 根据 `GraphChanged` 清理或重映射选择，不缓存已被规范化删除的 Edge。

### 9.3 self-loop 渲染

`RoadRenderer` 必须按 self-loop 构造闭合 ribbon：

- 首尾采样点使用循环相邻方向计算 join，不能生成两个独立端帽或可见裂缝。
- loop seam 不绘制 endpoint/junction 圆点。
- self-loop 加支路后，同一节点按 degree 3 绘制 junction。
- 两路口环的两条平行 Edge 必须都可见、可高亮和可命中。

节点视觉只按 topology role 推导：degree 1 是 endpoint，degree 大于等于 3 是 junction，degree 2 的 loop seam 或 semantic boundary 不绘制节点标记。弯道已经进入 Edge 内，不再依靠 degree 2 切线夹角判断“路口”。

### 9.4 分级样式与可见路面命中

`v3-grid-rendering:2.1` 已实现 `[GlobalClass] RoadTypeStyle`，首版只含 `RoadType`、展示名称、颜色和正有限宽度。`RoadConfig` 与生产 `road_config.tres` 恰好提供 `Dirt / 土路 / #8A6652 / 14`、`Street / 街道 / #60727C / 20`、`Arterial / 主干道 / #D7A928 / 26`、`Highway / 高速道路 / #C84B3A / 32`；校验拒绝缺失、空引用、重复或非法类型、空名称、非有限/透明颜色和非法宽度，查询在资源无效或目标非法时严格失败。`RoadRenderer._Ready()` 会输出非法映射诊断。

`v3-grid-rendering:2.2` 的分级 ribbon 切片已经让普通 `RebuildStaticBatches` 与后台 `RoadRendererLoadPreparer` 消费同一个不可变 `RoadTypeStyleSnapshot`：每条 Edge 用对应 half-width 和 vertex color 写入一个道路 `ArrayMesh`，道路层使用白色 modulate，不按类型创建四套 renderer；Load prepared payload 同步携带颜色数组，Godot Resource 不进入 worker。renderer 现已提供显式 `RefreshRoadStyles()`，以 `RoadStyleRevision` 和新的 render request 刷新批次。`EdgeRibbon` triangle 与真实 mesh triangle 同源，从显示 span provenance 返回 canonical `RoadLocation`；degree-1 Node 另由相邻 Edge 样式生成解析圆形 `TerminalCap` primitive，半径为 `Width / 2`，marker 和端点高亮使用同一宽度，A/B 命中分别返回该 Edge 的 canonical 起点/终点。两条不同 Edge、不同 RoadType 的 degree-2 Node 由实际 ribbon 端截面生成 `SemanticJoin` triangle；degree≥3 Node 则由 `RoadJunctionTessellator` 生成按 incidence sector 着色和归属的 `JunctionPatch` triangle。四类 primitive 共用构造期不可变 AABB 层级及点/矩形查询，并携带稳定 Edge/Node/Endpoint、sector 与 canonical endpoint location。

宽路交互不能只使用 `max(0, centerlineDistance - width / 2)`：该近似没有 terminal cap、miter/bevel、semantic join 或 junction patch，会让画面可见区域与 hover、拆除、改造和框选产生分歧。renderer 必须从与实际 mesh 同源的不可变 `RoadSurfaceSnapshot` 建立派生表面索引；每个 primitive 携带完整 render token 和稳定 owner，点查询返回统一的 `RoadSurfaceHit(RenderToken, OwnerKind, NodeID?, EdgeID?, Endpoint?, SurfaceDistance, CenterlineDistance, RoadLocation?)`。`OwnerKind` 至少区分 Edge ribbon、terminal cap、semantic join 和 junction patch；工具只接受 token 等于当前 `PresentedRenderToken` 的命中。

Edge ribbon/cap 归所属 Edge；semantic join 以规范切分线划分所有权；junction patch 按确定 incidence sector 划分到主 Edge，self-loop 的 A/B incidence 分别参与。point hit 按 `SurfaceDistance`、`CenterlineDistance`、规范 sector 顺序、Edge ID 排序；只有几何与视觉完全同值时才允许 Edge ID 最终破同值。矩形“接触可见路面”选择直接测试这些带 owner 的实际表面 primitive，并返回排序去重的 Edge ID；不能先按中心线选最近 Edge，也不能让 Node patch 成为无法执行命令的匿名命中。RoadGraph 的 query fragment 仍可用 `interactionRadius + maxLegalSurfaceOutset` 缩小中心线相关候选，但最终交互真相是已呈现表面；视觉宽度和 surface snapshot 都不写入 RoadGraph，也不改变 `NodeSnapRadius`。

### 9.5 junction surface 与异步派生显示

逐 Edge ribbon 只能覆盖道路中段，不能独自定义节点附近的完整表面。V3 的道路 mesh 按拓扑角色组合互不嵌套的派生 primitive：Edge ribbon、degree-1 terminal cap、degree-2 semantic join 与 degree≥3 junction patch。规则固定如下：

- terminal 使用与 Edge 宽度一致的稳定端帽；loop seam 没有端帽或 patch。
- degree 2 semantic boundary 不画“路口圆点”，但宽度不同时必须生成无洞、无自交的过渡 join；颜色边界落在规范切分线上。
- degree 大于等于 3 时，先按每条 incidence 的端点切线与 half-width 生成裁切截面，再构造覆盖截面之间空隙的 junction patch；T/X/锐角、self-loop 加支路和平行 Edge 都走同一 incidence 输入。
- patch 的轮廓和三角化只依赖规范坐标、RoadType 样式和按 outward direction 的 exact half-plane/cross comparator 排序的 incidence；换 Edge ID、反向存储或改变字典遍历顺序不得改变像素结果。实现使用固定版本的成熟多边形库，例如用 Clipper2 在 `RoadNumericPolicy` 约束下的整数坐标完成 offset/union，再用固定版本的受审计 triangulator 处理规范 ring；量化尺度、溢出上限和最大视觉误差在 Phase 0 固化，不手写未经验证的多边形布尔。混合类型 patch 按弧长联合裁切并使用固定 RoadType 优先级划分颜色/owner sector，只有视觉完全相同时才以 Edge ID 破同值，不能依赖最后写入的 Edge。
- miter 长度必须有上限，退化或近共线输入使用固定 bevel/round fallback；任何 NaN、翻转三角形、洞或无限尖刺都在派生构建中结构化失败并保留上一份完整 mesh。

当前实现固定使用 Clipper2 2.0.0。`RoadJunctionTessellator` 在 `RoadNumericPolicy` 合法坐标内以 `1024` 比例量化，单坐标最大误差为 `0.5 / 1024`，显示宽度范围为 `4 / 1024` 到 `1_000_000`；轮廓经 integer union 后由同一库三角化。incidence 使用 exact half-plane/orientation 排序，self-loop A/B 分别参与，同向 incidence 以 RoadType 降序、Edge ID 最终破同值；miter 最长为最大 half-width 的 4 倍，锐角超限时使用有界 fallback。owner sector 以相邻边界弧长中点划分，surface centerline 从量化后的 NodePosition 指向所属 incidence。普通 rebuild 与 Load worker 共用 `AppendJunctionPatch`，degree≥3 静态圆形 marker 已移除。

`GraphChanged` 可以触发后台纯数据 tessellation 或分帧构建，但读取的必须是该提交的不可变 render snapshot。一次请求的 `RoadRenderToken` 至少包含 `SceneGeneration`、`GraphFacadeID`、`GraphFacadeGeneration`、`ChangeSequence`、`RoadStyleRevision` 和单调 `RenderRequestID`；`GraphFacadeID` 标识稳定 facade 实例，generation 在外部 full reset 或 renderer 改绑 facade 时递增，而 `ChangeSequence` 在同一 facade 生命周期内跨 lineage/full reset 全局递增。后台结果只有与 renderer 当前 `DesiredToken` 完全相等时才能发布，不能只比较 sequence。Godot RID、Mesh 和节点只在主线程创建/交换；mesh、surface snapshot 和 `PresentedRenderToken` 必须在一次 presentation commit 中同时替换，禁止新 ribbon/旧 patch/旧 hit index 混合。

当前 token、四类 surface 与普通 mutation stalled/retry 已实现：`RoadGraph.FacadeID` 在进程内稳定唯一且 allocator 不回绕，`RoadPresentationTokenTracker` 独立推进六个身份分量，并为当前 desired token 记录 build attempt 与完整 `RoadPresentationFailure`。普通 mutation 先推进 desired，再让 `RoadRendererLoadPreparer` 从不可变 revision 构建目标表现；created/updated Edge 才重新采样，未变化点列与 provenance 数组按引用复用。只有 prepare、Mesh/MultiMesh、`RoadSurfaceSnapshot` 全部成功且 graph/token 仍匹配时，才一次交换完整 cache/mesh/node batch/surface 并推进 presented；失败保留上一代完整表现，`GetPresentationState()` 公开 `phase = stalled`、attempt、token、异常类型和消息，provider 拒绝 hit，`RetryRoadPresentation()` 在同一 desired token 上重试，新请求则取代旧失败并拒绝迟到结果。样式刷新仍单独推进 style revision。Load admission 预留下一次 facade generation/request，worker preparer 先生成持有 triangle/disc defensive copy、统一 bounds、节点和 primitive index 的 `RoadSurfaceSnapshot.PreparedData`，Preflight 只构造 matching token 并绑定该 prepared 数据；aggregate commit 同时交换基础 mesh、带 canonical `RoadLocation` 和空间索引的 `EdgeRibbon` + `TerminalCap` + `SemanticJoin` + `JunctionPatch` snapshot 与 desired/presented token，并以 graph state token 消重随后发布的同一次 full reset。内部 `IRoadSurfaceSelectionProvider` 只在 expected token 同时等于 desired、presented 和 snapshot token 时提供查询；`RoadBuilder` 的拆除与改造 hover、连续采样和矩形选择均通过该接口，`RoadRemovalSession` 与 `RoadUpgradeSession` 分别冻结完整 token，并在 pending、stalled 或 superseded 时清空选择。`ConfirmRemove()` 与 `ConfirmUpgrade()` 都要求 token 的 `GraphFacadeID` 与 `ChangeSequence` 等于当前 graph；改造还冻结目标 `RoadType`，一次提交批量 `ChangeRoadType()`，NoChanges 不产生 history，因而两类命令 admission 均已完成。类型化建造已完成 `SelectedRoadType`、会话冻结和显式提交，但该命令尚未纳入表现 pending/stalled 门禁；排队 continuation 和全命令 admission 也仍未完成，不能提前声称 9.4～9.5 的完整工具接管契约完成。

普通 mutation 先提交领域 root，再异步派生表现，因此从 `DesiredToken` 前进到 matching `PresentedRenderToken` 之间必然存在旧 mesh 窗口。所有道路 hover、选择和命令 admission 必须同时满足 `hit.RenderToken == PresentedRenderToken == DesiredToken`，且 token 的 facade/generation/sequence 等于当前图状态；不能因为旧 hit 仍等于旧 presented token 就接受。构建失败保留上一份完整表现，进入可诊断、可重试的 `RoadPresentationStalled`，并持续禁用全部道路交互，不能回滚已发布且可能已被观察的普通图事务。

外部 Load 不允许产生上述提交后窗口。它在不修改可见状态时先完成纯数据 tessellation，并在主线程预建可直接交换的隐藏 Mesh/RID、surface snapshot 和命中索引；只有 graph、empty tool/overlay root 与表现资源全部 preflight 成功，才在 10.6 的一次不可抛、不可 yield commit 中同步交换。`PresentationReady(token)` 是该交换后的 matching acknowledgment，而不是允许关键表现稍后失败的补救阶段。真实 Vulkan 截图必须覆盖四类宽度的 T/X/锐角、semantic boundary、简单环、棒棒糖和两路口环。

### 9.6 Load UI 与输入接管

手动 Load 从 admission 到 matching presentation commit 始终保持 PauseMenu 打开和场景暂停。UI continuation 必须同时匹配 `SceneGeneration`、`MenuOpenGeneration` 和 `OperationToken`；旧菜单实例、旧请求或重复按钮回调不能关闭新菜单或覆盖新结果。Prepare/Preflight 阶段按一次 Escape 只发送一次取消请求并保持菜单；进入短的 non-yield commit 后，Escape 仍由 PauseMenu 独占消费，但不能插入该临界区、关闭菜单、恢复游戏或重复取消。失败和提交前取消回到原菜单、原槽选择和原道路会话；`SucceededWithObserverWarnings` 在 matching acknowledgment 后关闭菜单并展示非阻塞 warning。关键表现资源必须在 commit 前失败，因此 V3 没有“图已加载但表现失败”的正常结果分支。

full reset 的临界区统一清除所有绑定旧图实体的状态：placement/removal/upgrade 会话，hover 与 `_lastHoveredEdgeID`，point/rectangle selection，preview/highlight/selection bounds，undo/redo，renderer Edge cache、dynamic overlay、旧 surface index，DebugPanel/Inspector 的旧实体引用，以及排队的道路命令或异步 continuation。`CurrentTool`、`SelectedRoadType`、输入绑定和其他不引用图实体的用户偏好可以保留。普通 delta 只按 removed/updated owner 清理或重映射；不能把它误当 full reset。

---

## 10. V3 独立存档格式与加载事务

### 10.1 格式代际与保存根隔离

V3 不提升 V2 的 manifest 或 RoadGraph schema，也不复用其保存根。它建立新的格式代际：

| 边界 | V2 历史值 | V3 唯一值 |
|---|---|---|
| 编辑器保存根 | `res://saves` | `user://saves-v3` |
| 导出版本保存根 | `user://saves` | `user://saves-v3` |
| 容器 family | 无 | `simple-cities-v3` |
| V3 初始 schema | 不适用 | `schemaVersion = 1` |

V3 在编辑器和导出中统一使用 `user://saves-v3`，测试只能通过依赖注入使用临时根；生产代码不再把存档写入仓库。V3 根与两个 V2 根互不包含，避免任一代把另一代目录误认成槽位。V3 的 list、Save、Load、Delete、autosave、恢复扫描和事务清理只能解析 V3 根的 direct child；不得探测、打开、哈希、移动、转换、覆盖或删除 V2 根中的任何内容。V2 槽不会出现在 V3 UI，也没有导入、只读加载、Save As 转换或后台升级入口。

`formatFamily` 和 `schemaVersion` 是共同 admission gate：缺失、大小写错误、非 `simple-cities-v3` family、旧版本或未知未来版本都在业务 DTO、图构建和场景事件之前结构化拒绝。即使用户把 V2 槽手工复制到 V3 根，也只会得到不兼容格式错误，不触发格式猜测。未来 V3 自身若升级格式，必须单独决定“拒绝”或新增显式导入工具；本指南不预留通用 migration hook。

### 10.2 V3 format v1 逻辑形状

```json
{
  "formatFamily": "simple-cities-v3",
  "payloadType": "road-network",
  "schemaVersion": 1,
  "nextID": 12,
  "nodes": [
    { "id": 1, "x": 10.0, "y": 0.0 }
  ],
  "edges": [
    {
      "id": 7,
      "nodeAID": 1,
      "nodeBID": 1,
      "roadType": "street",
      "geometry": [
        {
          "version": 1,
          "kind": "circularArc",
          "start": { "x": 10.0, "y": 0.0 },
          "end": { "x": 10.0, "y": 0.0 },
          "center": { "x": 0.0, "y": 0.0 },
          "radius": 10.0,
          "startAngle": 0.0,
          "endAngle": 0.0,
          "sweepAngle": 6.2831855
        }
      ]
    }
  ]
}
```

format v1 不包含 `groups`、`groupID` 或提交来源字段。`nodeAID == nodeBID` 是合法 self-loop；其 geometry 必须为正长度闭合链。平行 Edge 允许共享同一端点对，但必须具有不同 ID 和不同的非覆盖几何。所有 geometry 写出方向 key 使用的完整权威字段：圆弧必须同时写 `start`、`end`、`startAngle`、`endAngle` 与 `sweepAngle`，clothoid 必须同时写 `start`、`end`、`startHeading`、`reverseStartHeading`、两端曲率与弧长；V3 reader 不接受缺少整组锚字段的旧推导形状，也不重算端点。

严格校验至少包括：

- 全局 ID 唯一、`nextID` 大于所有实体 ID、无孤立节点；
- A/B incidence 精确存在一次，自环在同一节点精确存在 A/B 各一次；
- 原生几何类型、控制参数、长度、链连续性和端点匹配合法；
- `RoadType` 字符串合法且大小写精确；
- 无未知字段、悬空引用、非法内部交叉、重复覆盖或可继续合并的非规范二度节点；
- 所有校验在临时模型完成，失败时活动图和任何事件均不变化。

V3 reader 不通过 canonicalize 修复非规范 payload。保存 writer 必须只产生规范图；Load 重新验证同一组不变式，遇到可合并二度节点、非法 seam 或非规范方向就拒绝。这使“写入格式”和“领域完成态”只有一个定义，也避免隐藏的数据清洗改变 ID 或玩家道路。

### 10.3 连续 Edge 的磁盘表示

V3 format v1 使用 UTF-8 JSON，`geometry` 内联在所属 Edge 中。一个 Edge 无论包含 1 个还是 10 万个原生几何段，都只有一个 Edge ID 和一个有序 geometry 数组；不得为了写盘大小、流式解析或编辑器分页而插入伪 Node、伪 Edge 或持久化 chunk ID。

这是三个不同问题：

| 问题 | V3 决定 |
|---|---|
| 拓扑边界 | 只由 terminal、junction、semantic boundary 和 loop seam 决定。 |
| 内存/磁盘传输 | 允许流式读取、流式写入和固定大小 I/O buffer，但 buffer/chunk 没有领域身份。 |
| 渲染/空间索引批次 | 可以按 geometry 段或采样点分批，仍引用同一个 Edge ID。 |

JSON 在 V3 仍比自定义二进制格式合适：六类几何需要严格、可诊断的字段契约，存档仍只有少量业务文件，而且当前没有证据表明解析 CPU 或磁盘体积是主要瓶颈。二进制格式会同时引入新 reader 和调试工具，却不会减少 geometry 的真实数量。

初始 format v1 不启用压缩。若 geometry-dense 基准证明磁盘体积或 I/O 是门槛，压缩只能加在**文件容器层**：manifest 显式声明 codec、编码后/解码后字节数和校验信息，解压前执行上限与压缩比门禁。压缩不得改变 format v1 的逻辑 JSON，也不得把压缩块变成 GraphEdge 边界。这类容器变化必须提升 V3 manifest schema，不能用改扩展名或魔数静默切换。

### 10.4 确定序列化、资源上限与一次解析

RoadGraph 需要专用的 canonical JSON writer，不再依赖全局 `SaveJson` 的缩进设置：

1. 根字段和实体字段使用固定顺序，Node/Edge 按 ID 升序，geometry 保持路径方向顺序。
2. 使用无 BOM、无缩进的 UTF-8；数值由同一 writer 以 invariant 规则输出，RoadType 只写第 8.1 节的小写名称。
3. 相同活动图必须产生逐字相同的 payload；这用于回归比较和内容诊断，但领域等价性仍由结构比较定义，不能反向依赖任意 JSON 字符串。
4. self-loop 不复制首尾 Node；平行 Edge 各写一次完整 geometry。几何端点与 Node 重复属于局部严格校验所需，不抽成跨 Edge 共享表。

严格 reader 同时约束 token 类型和原始 UTF-8 lexeme，而不只是反序列化后的 CLR 值。ID、`nextID`、count 和 byte length 必须是无正号、无前导零、无小数点、无指数、无 `-0` 的十进制整数 token，并在转换前检查 lexeme 长度和目标范围；float 字段必须是有 lexeme 长度上限的 JSON number，解析后须为有限 binary32、落在 `RoadNumericPolicy` 范围且把 `-0` 规范为 `+0`，拒绝字符串化数字、`NaN`、`Infinity` 和溢出，但 reader 不要求输入文本逐字等于 canonical writer 的唯一输出。属性名按 UTF-8 原字节识别，重复已知字段、大小写变体、未知字段以及类型错误在进入 prepared model 前失败。

加载必须在分配大对象前执行分层预算。具体数值由 junction-dense/geometry-dense 基线和 Windows 导出峰值内存确定，并在 V3 合并前固化为命名常量；不得把未经测量的任意数字写成长期格式承诺。预算至少包括：

- manifest 字节、单 payload 编码字节和整槽总字节；
- Node、Edge、单 Edge geometry、全图 geometry 和 manifest 文件项数量；
- JSON 最大深度、字符串长度以及未来压缩格式的解码字节和压缩比；
- 准备模型、邻接和空间索引重建所需的峰值分配。

manifest 和每个 payload 都只从各自一个受限句柄完成 byte budget、duplicate-aware parse、counting/hash 和 EOF 终检；句柄以 deny-write/delete sharing 请求打开，该排他语义必须逐目标平台用替换/删除测试验证，不能从 Windows `FileShare` 泛化到所有文件系统。流程固定为：先枚举一次 ordinal direct-child exact set；打开并保留 manifest 句柄，从这次 parse 得到唯一 payload metadata；打开全部预期 payload 句柄后第二次枚举；从各句柄读取初始 length 并执行预算，再把同一字节序列一次送入 counting/hash/token reader；全部 prepare 完成后、释放句柄前第三次枚举。每个 payload 必须同时满足 manifest `encodedLength`、句柄初始 length、实际消费字节、EOF 和 SHA-256；Load 的终检发生在场景 Preflight 前，Save 的终检发生在第一次 canonical move 前。不能在 length/hash/parse 之间按路径重开文件。流式 reader 每读到一个数组元素就在加入集合前检查单项与总量。运行时 mutation 与加载使用同一 `RoadGraphCapacity`，保证任何可由玩家合法建造的图都可保存并重新加载，不能等到自动存档时才发现活动图超过持久化上限。容量还必须覆盖第 6.4 节的坐标/长度、ID reservation 和第 3.4 节的 fragment/bucket/ref；geometry-dense 门禁包含“单条超长 Edge 恰好低于/高于上限”，环路和平行 Edge 不获得额外配额。

活动 `RoadGraph` 是稳定 facade，权威状态保存在完全不可变的 `RoadGraphRevision` root 中；Entity、geometry、邻接、incidence、query fragment、空间索引和 diagnostics 都不得在发布后原地修改。mutation plan 或 load prepare 构造并验证新 root，再原子替换引用。不可变不等于每次编辑深拷贝全图：普通 mutation 必须使用持久化映射/集合、固定大小页或等价的 copy-on-write 结构，只复制受影响的 Entity、邻接/incidence 项、query fragment、bucket/reference 页和到 root 的索引路径，未触碰的子树、页与 geometry 数组在相邻 revision 间按引用共享；任何可变 builder 只能存在于未发布 plan 内，发布前冻结，不能把可写集合或缓冲区泄漏给旧/新 root。full load/full reset 可以线性建立全新 root，但单点建造、拆除、改造和 delta undo/redo 的时间与分配必须由受影响拓扑及空间覆盖决定，不得随无关远端图总量线性增长。共享对象的回收由仍存活的活动 root、后台 save/render snapshot 和有预算的历史 delta/root 引用共同决定，完成、取消或淘汰后必须释放其所有权，不能用永久 revision 表保留整图。

保存只需在主线程 O(1) 捕获当前 root 引用及其 runtime token，随后直接把 canonical UTF-8 流式写入隐藏 staging；玩家之后的编辑构造新 root，不改变已捕获快照。禁止为了“异步保存”仍在主线程深拷贝大图或物化完整 payload 字符串。当前 RoadGraph reader 已从受限句柄直接构造完整 prepared root，移除了 `ReadAllText -> JsonDocument.Parse -> DTO deserialize` 的重复完整表示；`v3-save-system:2.3` 仍需把这条能力放入后台 aggregate Prepare，并补齐跨 participant 的 Preflight/commit。

### 10.5 发布、恢复与完整性边界

V3 有三类互不混淆的操作结果：`PublishV3` 改变 V3 磁盘槽，`Load` 只读 V3 磁盘并交换易失活动会话，`DeleteV3` 删除明确授权的 V3 槽。Load 不移动、删除、修复或写回来源槽；Publish 成功也不暗示场景已加载；Delete 不参与 Load commit。

`PublishV3` 使用整槽 `staging -> slot`、覆盖时旧槽 `slot -> backup` 的发布模型；长 Edge 不需要增量覆盖或分片文件。每个操作使用唯一、operation-specific transaction 目录，而不是争用 `<slot>.staging`。流式写 staging 不降低原子性，因为 staging 在全部 payload 和 manifest 成功前不可见。V3 manifest v1 保留列表摘要字段，并用按名称排序的业务 payload 描述项证明内容：

```json
{
  "formatFamily": "simple-cities-v3",
  "schemaVersion": 1,
  "slotId": "city-001",
  "displayName": "河湾城",
  "timestamp": "2026-08-12T08:00:00.0000000Z",
  "cityName": "河湾城",
  "population": 1200,
  "funds": 50000,
  "thumbnailFile": "thumbnail.png",
  "files": [
    {
      "name": "road_network.json",
      "encodedLength": 12345,
      "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
    }
  ]
}
```

hash 覆盖磁盘中的原始 payload 字节；初始无压缩时 encoded/decoded length 相同，不重复保存后者。未来 codec 必须增加显式 `codec` 和 `decodedLength` 后再提升 manifest 版本。reader 严格拒绝未知字段、重复已知属性、大小写冲突、重复或大小写等价的文件项、非规范 hex、越界长度、未声明业务文件、reparse point 和路径逃逸；manifest 自身不列入 `files`。仅依赖普通 DTO 加 `JsonExtensionData` 不足以发现重复已知属性，必须由受限 token reader 或等价的 duplicate-aware 层执行。

摘要字段也属于严格 schema，不能由 CLR 默认值吞掉非法输入：`slotId` 是 1～128 个 `[A-Za-z0-9_-]` ASCII 字符并逐字匹配目录名；`displayName` 与 `cityName` 都是非 null、无首尾空白/控制字符、1～128 个 Unicode scalar 且 UTF-8 不超过 512 byte 的字符串，reader 不做 Unicode normalization；`timestamp` 是精确 UTC `yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'`，writer 不接受本地 offset 或不同小数位；`population` 是 null 或 `0..Int64.MaxValue` 的规范十进制整数 token；`funds` 是 null 或 `Decimal.MinValue..Decimal.MaxValue`、小数位不超过 2 的规范十进制 number，禁止正号、指数、前导零、尾随小数点和 `-0`。writer 对 null 元数据显式写 null，不用缺字段表达未知；reader 对缺失、错误类型和越界 lexeme 均拒绝。

`thumbnailFile` 是独立的可选 PNG 展示资产，不是业务 `files` 成员，也不参与业务 aggregate commit。若非 null，它必须是槽根下唯一、大小写精确、安全的普通 `.png` 文件，并经过字节、PNG signature、尺寸/像素预算和解码门禁；缺失或损坏只产生缩略图 warning 并回退占位，不使已完整的业务槽失效。容器的允许文件集合是 manifest、声明的业务 payload 和这个可选缩略图，其他普通文件仍拒绝。

V3 根同时使用进程内 async gate 和根内 `.save-root.lock` 的 OS 独占句柄；锁文件以禁止共享写/删的方式持有到恢复、发布或删除完全收敛，进程崩溃由 OS 释放句柄。外部实例占用时返回结构化 `BusyExternalInstance`。每次发布使用 `.save-transactions/<slot>/<operation-id>/staging`、`backup` 和不可变 `publish.json`；descriptor 绑定槽 ID、新旧 aggregate digest、固定路径和 operation token，不记录可变 phase，进度由 canonical slot、staging、backup 的存在性和 digest 推导。descriptor 先写临时文件、flush，再以同目录原子 rename 发布；未成功发布 descriptor 的路径没有恢复授权。不同请求不共享事务目录。V3 根、槽和事务路径都必须拒绝 reparse point；任何触发恢复扫描或目录变更的 list 都必须取得同一把锁。

这是一项面向协作实例和意外损坏的正确性契约，不是对本机恶意写入者的安全隔离。根锁只约束遵守协议的 SimpleCities 实例；对拥有保存根写权限、绕过锁并能替换目录项或修改已打开文件的进程，目录允许文件集合的多次检查不是原子快照，仍存在目录级 TOCTOU。禁止共享写/删的同一 payload 句柄只证明 prepared state 来自该句柄实际解析、计数并与 length/hash 相符的字节；实现必须在持锁时对允许文件集合至少于 prepare 前和 commit/publish 前复核，但不能据此宣称抵御非协作进程。SHA-256 是无密钥完整性校验，既不认证存档来源，也不防止攻击者同步改写 payload 与 manifest，更不提供版本新鲜度或防回滚；若未来需要敌对环境保证，必须另行引入受保护密钥/签名、可信单调版本和更强的目录隔离，并提升容器协议。

发布和恢复需要明确区分：

- **进程失败原子性**：异常或进程中止后只能看到完整旧槽或完整新槽；这是 V3 必须保留的契约。
- **突然断电耐久性**：仅靠 `Directory.Move` 和 manifest 存在不能证明数据已落盘。若平台门禁要求该保证，需在发布前对文件执行 durable flush，并如实记录平台无法保证的目录元数据边界。
- **长期介质损坏**：per-file SHA-256 能发现绝大多数静默损坏，但不是防篡改签名。事务 backup 只保证发布窗口，成功验证新槽后会被删除，因此 V3 能检测之后发生的损坏，却不能承诺从长期 bit rot 自动恢复；若产品要求该能力，应另设保留代际，不得把临时 `.backup` 冒充长期备份。

V3 根中的 canonical direct child 只分类为 `Absent | CompleteV3 | CorruptV3 | Foreign | Unsafe`。`CompleteV3` 要求 family、版本、文件集合、长度/hash 和全部业务 payload 均通过第 10.4 节；声明 V3 family 但不完整的是 `CorruptV3`；其他 family、V2 形状或未知目录是 `Foreign`；文件、reparse point、路径逃逸、无法安全枚举的目录或类型变化是 `Unsafe`。`Foreign` 和 `Unsafe` 从不自动移动、覆盖或删除，也不出现在正常槽列表；`CorruptV3` 可以作为损坏槽显示，并只允许用户按明确目标确认后删除。Save/自动恢复不能用“可解析一部分”覆盖任一非 `CompleteV3` occupant。

恢复先按 operation ID 的 ordinal 顺序验证每个 `publish.json`，再只处理 descriptor 明确拥有的三条路径：

1. canonical slot 已匹配 new digest：发布成功；完整复核后只清理 descriptor 所属 backup/staging。
2. canonical slot 仍匹配 old digest：尚未越过覆盖边界；保留旧槽并隔离该事务的 staging，不能把 staging 自动提升成新槽。
3. canonical slot 缺失、backup 匹配 old digest 且 staging 匹配 new digest：已经越过 `slot -> backup`；继续 `staging -> slot`，若无法完成则把 old backup 恢复到 canonical 路径。
4. canonical slot 缺失且只有匹配 old digest 的 backup：恢复旧槽；首次保存中 slot 缺失且 staging 仍存在表示尚未越界，只隔离 staging。
5. canonical、staging、backup 与 descriptor 的存在性或 digest 发生其他组合：停止并返回 `PublicationRecoveryBlocked`，完整保留现场，不按时间戳或目录名猜测赢家。

无 descriptor 的 partial transaction 不是候选槽；确认它不与任何 descriptor 关联后，原子改名到唯一 quarantine 诊断路径，失败则停止。manifest 必须最后写入 staging；每个 payload 写完后由 writer 实际记录 byte count/hash，关闭句柄并按平台策略 flush，随后写并 flush manifest。staging 完整复核后、第一次 canonical move 前，回到主线程重新验证 scene/saveable generation 和目标槽意图，并授予一次性 `PublishLease`。首次保存的不可取消点是 `staging -> slot`，覆盖 `CompleteV3` 槽的是 `slot -> backup`；越界后必须在同一 gate/lock/lease 内完成新槽发布或恢复可证明的旧槽。

`DeleteV3` 也使用 operation-specific transaction 和原子发布的 `delete.json`。descriptor 绑定槽 ID、待删 `CompleteV3`/已确认 `CorruptV3` occupant digest、固定 tombstone 路径、UI/operation generation 和确认摘要；`Foreign`、`Unsafe` 不能取得删除授权。删除在锁内先恢复目标槽并重新验证 descriptor，随后以 `slot -> tombstone` 作为不可取消点：rename 成功后槽在逻辑上已删除，不再进入列表，也不得因后续递归清理失败而移回。若删除的是当前可写目标，主线程在同一不可抛结果提交中仅当 slot ID 与捕获 generation 仍匹配时清空 `CurrentSlotID`；其他活动 graph/tool 状态不变。tombstone 清理失败返回 `DeletedWithCleanupPending`，启动恢复按 descriptor/digest 继续删除；descriptor 与 tombstone 不匹配时返回 `DeletionRecoveryBlocked` 并保留现场。

从最终 slot 路径完整复核 new digest 后即为已发布；之后 backup/quarantine/transaction 清理失败不得把新槽误报为失败或回滚，返回 `PublishedWithCleanupPending` 并让下次恢复继续幂等清理。只有清理也完成时返回 `Published`。容器完整不等于业务合法：Load 还要准备完整 aggregate，全部成功后才提交活动 root；恢复扫描不修复或 canonicalize RoadGraph。突然断电耐久性也不同于进程失败原子性：文件 flush、descriptor/rename 的目录元数据 flush 和断电故障测试均有平台证据时才能称 **durable**；只有文件 flush 与原子 rename 证据时称 **crash recoverable**。临时 backup 仍不冒充长期 bit-rot 备份。

### 10.6 快照、并发、取消与加载提交

当前生产入口已经删除同步 bool API：`SaveManager.StartSave/StartSaveAs/StartLoad/StartDeleteSlot/StartAutosave` 返回唯一 operation token，`SaveOperationCoordinator` 用进程内 gate 调度不可变 state/result，`SaveSlotStore` 继续在每次目录操作内持有跨进程 `.save-root.lock`。Save 在主线程 O(1) 捕获 immutable root，serialize/hash/I/O 与 Load reader/basic tessellation 在后台执行；Load 回到主线程完成 Preflight 和 non-yield commit。手动请求优先，Timer busy 只合并一个 pending autosave；scene/menu generation、取消边界、退出 drain/shutdown 和 typed rejection 已接入。Publish、Load、Delete 与 Autosave 保持独立 kind、phase、token 和结果，不再压成 bool。

当前 `v3-save-system:2.3` 仍不能关闭：`PreparedAggregateLoad` 已覆盖 RoadGraph、新的空工具/history、分级基础 `ArrayMesh`/`MultiMesh`、带 canonical `RoadLocation` 和不可变空间索引的同源 `EdgeRibbon` + `TerminalCap` + `SemanticJoin` + `JunctionPatch` `RoadSurfaceSnapshot`、matching 六分量 desired/presented token 和 `CurrentSlotID`，并在清空 placement/removal/upgrade/history 时保留 `CurrentTool` 与 `SelectedRoadType`。renderer 的 worker Prepare 已完成 patch 细分、triangle/disc defensive copy 与统一索引建树，主线程 Preflight 只绑定 token。现有证据已经覆盖 generation 门禁、提交前失败逐值保留、mesh/四类 surface/index/token 一次引用交换、observer warning 隔离，以及拆除/改造会话对 current/superseded token 的接收与拒绝；仍未完成类型化建造的表现门禁、排队 continuation、第二 saveable 与每个关键资源故障点的联合矩阵。完整设计仍按下述四阶段收口，不能把当前 renderer participant、拆除/改造 consumer 或类型化建造切片当作最终表现契约。

Save 走 capture/prepare/`PublishV3`；Load 固定为以下四阶段：

1. **Admission**：主线程验证请求、`SceneGeneration`、saveable/participant generation 和资源预算，取得独占 graph-command admission；冻结新道路命令但逐值保留当前 placement/removal/upgrade 草稿、选择、hover、overlay 和历史。此阶段不构造 JSON、不深拷贝图，也不改变 `CurrentSlotID`。
2. **Prepare**：后台从第 10.4 节受保护句柄构造完整 prepared aggregate。RoadGraph root 已含实体、邻接、incidence、query fragment、空间索引、diagnostics 和准确容量；工具参与者准备空的 replacement state；renderer 完成纯 CLR tessellation、surface snapshot 与 hit-index 数据。后台不访问 Godot Object、活动 facade 或场景树。
3. **Preflight**：回到主线程重新验证 operation/scene/saveable/participant generation，预建可直接交换的隐藏 Mesh/RID 和 renderer 资源，并让 graph、tool、renderer、slot-target participant 验证各自不可抛 commit plan。任何关键资源、容量、Vulkan/Godot 创建或 generation 失败都发生在这里；失败/取消释放 admission，活动 graph/tool/mesh/surface/token、菜单选择和 `CurrentSlotID` 逐值不变。
4. **Non-yield commit/notification**：在一个不可 yield、只执行已验证引用交换和计数更新的临界区，同时交换 facade root/lineage、empty tool/overlay root、hidden mesh/RID、surface snapshot、hit index、`DesiredToken == PresentedRenderToken`、diagnostics 和可写 save target，递增稳定 facade 的 `ChangeSequence`，然后发布一次 `IsFullReset` 与 matching `PresentationReady`。普通 observer 在完整状态可见后通知；异常被逐个隔离并只把结果提升为 `SucceededWithObserverWarnings`。关键 participant 没有提交后工作，因此 Load 只有 `Succeeded`、`SucceededWithObserverWarnings` 或提交前失败/取消，不存在图已交换而关键表现稍后失败的分支。

V3 当前业务 payload 只有 RoadGraph，但协议必须从一开始就是 prepared aggregate swap。未来增加 saveable 时，全部系统先 Prepare，再 Preflight，最后只交换已经准备好的不可变引用；禁止顺序调用可能抛异常的 `RestoreState` 把前半系统留在新状态。稳定 `RoadGraph` facade 不因 load 换对象，RoadSystem、RoadBuilder 和查询服务继续持有同一 facade；full reset 通过新 lineage 和 generation 使旧 delta、命中和异步任务失效。

当前 aggregate 已按这一路径交换稳定 RoadGraph facade 的新 lineage、RoadBuilder 的空 placement/removal/upgrade/history、RoadRenderer 的预建基础 mesh/node batch、同源 triangle/disc 的 indexed `EdgeRibbon` + `TerminalCap` + `SemanticJoin` + `JunctionPatch` surface、matching token 和槽目标；移除 renderer 的生命周期回归会在 commit 前失败，并逐值保留活动图、未完成 placement、undo/redo 和 `CurrentSlotID`。拆除与 RoadUpgrade consumer 已验证旧 token 不能在样式刷新、普通 mutation pending 或 full reset 后提交，但这项证据仍不证明排队 continuation、第二 saveable 或关键资源故障矩阵。

并发策略固定如下：

- 手动 save/load/delete 优先于尚未开始的 autosave；Timer 在 busy 时只合并为一个 pending autosave，不排队多个周期。当前场景仍有效且没有更晚成功 autosave 时，gate 释放后执行一次；否则丢弃并记录 `SkippedBusy`，不算 I/O 失败。
- 同一 snapshot 的序列化、hash 和发布要么完成为一个槽版本，要么恢复旧槽；不同请求不能共享 staging/backup。手动 save/save-as 成功可更新可写 target；成功 V3 Load 在统一 commit 中设置 `CurrentSlotID`。autosave、失败、取消和跳过不改变活动会话的可写 target；Delete 在 `slot -> tombstone` 成功前也不改变，越界后仅当被删 slot ID 与捕获的当前 target generation 仍匹配时，在不可抛结果提交中清空 `CurrentSlotID`。
- 取消在对应不可取消点之前生效：普通 Save 以 10.5 的首次/覆盖 canonical move 为界，Load 以 non-yield commit 开始为界；Delete 在首次移动或删除 canonical 槽前完成全部确认与恢复扫描，越界后必须收敛。取得 gate 或 lease 本身不是不可取消点。场景退出先拒绝新请求，取消未越界 worker，并等待已越界目录事务收敛；Load commit 中 Escape 只能被 UI 消费，不能插入临界区、关闭菜单或制造第二个取消请求。
- snapshot capture 和 Load 的统一 graph/tool/mesh/surface swap分别设主线程帧预算；serialize/read/hash/parse/index build/tessellation 与隐藏资源 preflight 设总耗时和峰值分配指标。普通 mutation 的旧 mesh 窗口继续遵守 9.5 的 `RoadPresentationStalled` 门禁；Load 不暴露该窗口。

### 10.7 编辑历史不是存档副本

`RoadEditHistory` 不持久化到槽位，也不再复用磁盘 JSON 作为内部数据结构。完成前的 64 个 entry 各持有 before/after 字符串；当图的稳定 JSON 约为 `S` 个 ASCII 字节时，仅字符串字符存储的量级就接近 `128 * 2S = 256S`，还未计算每次捕获的 DTO、临时字符串和 GC 峰值。canonical Edge 只减少 Node/Edge 元数据，不会消除长 geometry 数组，因此这个历史问题不能靠新存档格式自然解决。

V3 使用可逆 `RoadGraphDelta`：

1. mutation plan 在提交时记录 created/removed/updated Node 与 Edge 的完整前后实体和 `BeforeRevisionID` / `AfterRevisionID`；revision 内容状态可由 undo/redo 恢复，但 revision allocator、allocator watermark 和 `ChangeSequence` 都不回退或复用。
2. undo/redo 先以 6.3 的完整 `GraphStateToken` 校验 lineage、方向 revision 和最新 sequence，再应用逆/正 delta；成功后恢复对应内容 revision、分配新的 sequence，并走与普通事务相同的不变式、索引更新和单次 `GraphChanged`，不经 JSON。错误 token 返回 `StaleGraphState` 且无副作用。
3. canonicalize 导致的 seam、ID、合并或拆分全部进入同一 delta；不能试图重新执行用户输入来“推导”旧图。
4. 外部加载创建新 lineage，并在 full-reset commit 时立即使全部旧 token 和历史失效，不再靠完整序列化比较检测分叉。
5. 历史同时受 entry 数和估算字节预算约束；提交前确保新 delta 可接纳，再按最旧优先淘汰。单个命令超过单项预算时整次编辑结构化失败，不能成功修改图却悄悄失去撤销能力。

当前实现直接使用 delta，没有完整图 snapshot 兼容阶段。加载 V3 format v1 后清空旧历史；首次编辑以新 lineage 的 canonical 活动状态为 revision 起点。撤销/重做恢复相同实体 ID、loop seam、原生几何、类型和空间命中；同一 lineage 内 `nextID` 高于历史值，而外部 load 的新 lineage 精确采用槽内 watermark。结构相同比较不把 allocator watermark、lineage、revision 或 sequence 当成领域内容差异。

---

## 11. 性能与规模口径

压缩 Edge 后，单报 Edge 数会掩盖真实几何量。V3 所有基准同时记录：

- Node 数、Edge 数、`RoadGeometrySegment` 数和空间引用数；
- query fragment、占用 bucket、bucket reference、局部 exact test 和聚合 Edge 数；
- self-loop、平行 Edge、junction 和 semantic boundary 数；
- canonical JSON 字节数，以及未来可选容器的编码/解码字节数；
- snapshot capture、serialize、write、read、parse、validate、prepare 和 commit 分段时间；
- 每阶段及端到端峰值分配、prepared graph 大小、历史 delta 字节和 GC 次数；
- 主线程 O(1) root capture/load root swap/presentation commit 时间、后台 I/O 时间、busy autosave 合并次数，以及 renderer barrier/重建、帧时间、draw calls、objects、primitives 和分配量。

必须保留两类数据集：

1. **junction-dense**：继续使用 10k Edge 的 V2 最坏拓扑门槛，连续交互 P95 不超过 16.67 ms；100k 只记录压力结果。
2. **geometry-dense**：分成两组。共线组验证 N 个同向单位 line 最终只有 2 个 Node、1 条 Edge、1 个 line geometry；转弯/混合曲线组验证 N 个不可无损合并的 primitive 仍只有 2 个 Node、1 条 Edge、N 个 geometry。两组都记录 V3 format v1 的实体数、序列化体积、单次解析和历史开销，并在固定局部窗口下验证远端增长不增加 exact geometry 访问；V2 已记录数据只作同机参考，不进入格式兼容验收。

另加简单环、棒棒糖、两路口环、八字形、批量删除后重归一化，以及刚好低于/超过各项资源上限的数据集。先记录基线再优化；不得在没有测量时提前引入 chunk graph、压缩容器或多套 renderer。

2026-08-14 的 100k V3 Load 诊断还固定了一条实现约束：Debug 不变式不能为每个空间引用再次遍历全部 bucket。`AssertInvariants()` 现在先建立 reference identity 到预期 bucket coverage/count 的映射，再由 `UniformGrid.HasExactCoverage(...)` 单次扫描 bucket entries；它仍拒绝缺失、额外、错桶、同桶重复和内部计数不符，但复杂度从约 `reference × bucket` 降为 `reference + bucket entry`。逐级 12k～100k Load/renderer rebuild 为 906.549/1244.115/2563.902/3429.621/4327.669/5069.431 ms，100k 不再 AppHang。

同日 Godot 正式 `--enforce-budget` 默认入口第一次冷启动虽完成 10k 与 100k，但 10k camera/preview/highlight P95 为 20.041/17.278/20.505 ms，三项均超过 16.67 ms，必须保留为失败证据；原样复跑为 16.471/14.702/13.750 ms 并通过，随后 100k 为 18.641/19.901/15.825 ms、重建 4393.478 ms。独立 Release C# 性能入口复跑通过，10k 最坏多交叉 P95 为 7.556 ms，100k 多交叉为 10.797 ms。冷启动抖动因此仍是 Phase 7/8 的性能风险，不能用热复跑覆盖或宣称门槛无条件稳定。

2026-08-17 使用同一契约重新逐级运行，12k、20k、40k、60k、80k、100k 的 Load/renderer rebuild 分别为 779.503、1193.439、2428.374、3431.815、4463.697、5129.679 ms；100k camera/preview/highlight P95 为 0.660/0.658/0.675 ms。独立 10k `--enforce-budget` 为 0.461/0.482/0.491 ms、重建 648.221 ms，所有规模均输出 PASS、静态节点保持 2。Release C# 10k 最坏多交叉 P95 为 7.848 ms，100k 为 8.363 ms。该复验再次证明 BUG-21 的 Load 二次退化未回归，但简单独立直线数据集不包含完整 junction-dense/geometry-dense、四类 owner、类型改造时延或 token 故障扰动，不能关闭 `v3-grid-rendering:2.3`，也不能抹去 2026-08-14 的首次冷启动失败。

closed-ribbon 复杂环路切片收口复跑（2026-08-14）中，Vulkan 契约再次 PASS：10k camera/preview/highlight P95 为 0.657/0.779/0.672 ms，Load 与 renderer rebuild 为 608.025 ms；首轮 100k 为 13.517/0.722/0.699 ms、重建 4108.956 ms，独立 100k 复跑为 0.616/0.661/0.637 ms、重建 4492.629 ms，静态 renderer 节点始终为 2。两组 100k 都通过压力契约，但首轮 camera 的 13.517 ms 尾延迟必须与复跑值一并保留。普通 `MapTest` 提交与实际 aggregate Load 都把方形自环呈现为 `1 Edge / 8 mesh vertices / 0 node markers`；aggregate Load 后构造的两路口环在删除 seam 侧支路前为 `4 Edge / 20 mesh vertices / 4 node markers`，删除后为 `2 Edge / 12 mesh vertices / 2 node markers`，八字形的两个 closed ribbon 也由 C# renderer 回归覆盖。`road_input_strategy_runtime_contract.gd` 和 `road_closed_ribbon_runtime_contract.gd` 输出 PASS。完整自动化为 727/727，Debug/`ExportRelease` build 与 Roslyn diagnostics 继续为 0。该结果补齐了两路口环、八字形和支路删除后 seam 重定位，但既不抹去前述冷启动失败，也不替代 Phase 7 的缩放/重建视觉矩阵、平行 Edge 独立命中、mixed-width junction/surface/token 门禁。

per-edge 分级 ribbon 切片复跑（2026-08-14）中，真实 Vulkan `road_type_mesh_style_runtime_contract.gd` 通过 aggregate Load 生成土路、街道、主干道、高速道路四条单 mesh ribbon，逐项验证 14/20/26/32 宽度、vertex color、白色 modulate 和 packed color 的 8-bit 精度，再以普通 `Street` mutation 触发全批次重建并保持四类映射；截图人工确认颜色和宽度可辨识。独立 10k camera/preview/highlight P95 为 0.564/0.827/0.833 ms、Load 与 renderer rebuild 为 665.452 ms，随后独立 100k 为 8.674/0.977/0.737 ms、重建 4719.153 ms，两档均 PASS，draw call 为 4/5/4、静态节点为 2。完整自动化为 750/750，双配置 build 与 Roslyn/GDScript diagnostics 为 0，Godot MCP editor error 和 DAP `stderr` 为空。该证据只关闭 per-edge ribbon width/color 切片，不证明 junction/surface/token 或类型改造离散时延已经完成。

TerminalCap 增量检查（2026-08-14）只运行 1k Vulkan 数据集：camera/preview/highlight P95 为 0.393/0.378/0.339 ms，renderer rebuild 为 135.918 ms，静态 renderer 节点为 2，契约输出 PASS。它证明在当前小规模门槛中加入 degree-1 terminal 的解析 disc 没有引入静态节点数线性增长或交互帧超限；本轮没有重跑 10k/100k，不能用该结果刷新上面的规模基线。当时该结果也不证明后来独立完成的 stalled/retry，更不能关闭仍要求完整 owner 工具接管、道路命令 admission 和类型改造离散时延的 `v3-grid-rendering:2.3`。

---

## 12. 实施阶段

### Phase 0：测试基础设施、引用清单与基线

Phase 0 不一次写出依赖未来类型、导致测试工程整体无法编译的“全红矩阵”。先建立可复用且可编译的 V3 基础设施：全新的 format-family/manifest/payload fixture、canonical/loop/mixed-width/junction-dense/geometry-dense 数据生成器、保存根隔离探针、故障注入文件系统、跨进程锁 helper、确定 scheduler/main-thread dispatcher、fake save/load/tool/presentation participant，以及 Vulkan pixel/owner oracle。现有 V2 性能记录只作为同机比较基线；V2 byte fixture 不进入 V3 reader 测试。

实施前对所有 `RoadGroup`、`GroupID`、Group 查询、旧 mutation 事件、返回 Group ID 的 `AddRoad()`、V2 DTO、V2 保存根和相关测试消费者建立可重跑引用清单；逐项只有“由 V3 契约替换”或“删除”两种处置。`AddRoad()`、旧事件和 V2 DTO 不提供适配器。DebugPanel、Godot contract、性能程序、场景装配和公共调用者都必须在清单内，不在指南中固化会漂移的文件数量。

每个后续切片严格执行“最小可编译契约骨架 -> 一个可归因的失败测试 -> 生产实现 -> 聚焦与回归转绿”。长期矩阵覆盖 exact-sign、incidence/loop/typed key、fragment locality、canonical Edge、Group 删除、lineage/revision/sequence、V3 family/version 拒绝、五类 V3 occupant、Publish/Load/Delete 边界、故障 I/O、结构共享、delta admission、full-reset participant、完整 render token、共享 surface、junction patch 和 closed ribbon。

V3 在实现分支中按阶段保持可编译，但产品装配始终只有一套道路系统。不得通过 feature gate、兼容 facade、双事件、双 DTO、双 writer 或运行时选择器维持旧路径；需要尚未完成的下游能力时使用测试 fake 或未装配的 V3 接口，不让 V2 实现充当生产 fallback。V2 行为只存在于 Git 历史、V2 文档和独立保存根。

### Phase 1：incidence 与自环基础

1. **已完成（`v3-road-graph:8.0`，2026-08-13）**：固化 `RoadNumericPolicy`、`RoadGraphCapacity`、checked ID reservation、`-0`、full-turn 识别和 exact-sign line predicate；分离 grid snap、NodeSnap 与确定交点 clustering，验证 canonicalizer 幂等。
2. **已完成（`v3-road-graph:8.1`，2026-08-13）**：引入 `EdgeEndpoint` / `EdgeIncidence`，重写邻接、不变式、detach/rebuild 和切线查询。
3. **已完成（`v3-road-graph:8.1`，2026-08-13）**：内部 prepared topology 允许 `NodeA == NodeB`，自环注册和删除 A/B 两条 incidence。
4. **已完成（`v3-road-graph:8.1`，2026-08-13）**：明确 degree、distinct edge 和 neighbor 查询，允许平行 Edge。
5. **已完成（`v3-road-graph:8.1`，2026-08-13）**：六类原生几何实现严格可逆反向，以包含 arc/clothoid 权威锚字段的版本化 typed token key 固定非环 Edge 和 self-loop 存储方向。

独立验收（2026-08-13）：559/559 自动化、0 警告/0 错误 build、0 analyzer diagnostics 与 Godot Tier 3 已证明 prepared topology 可正确表达并查询/渲染 self-loop、parallel Edge 和普通边；临时运行时 bridge 已清理。该检查点当时尚无 V3 format v1 reader；现已由 `v3-save-system:2.1` 接入，不以历史 V2 reader 代替。

### Phase 2：最大连续 Edge 与 Group 移除

1. **已完成（`v3-road-graph:8.2`，2026-08-13）**：参数区间 query fragment 携带 geometry/parameter 身份到精确测试，cut/join/B 端/seam 使用半开唯一所有权；closest/radius/rectangle/intersection 最后才聚合 Edge。
2. **已完成（`v3-road-graph:8.2`，2026-08-13）**：`FinalizeMutation` 按受影响 Node ID 归一化所有非结构性二 incidence 节点，提交、交叉拆分和删除后均恢复最大连续 Edge。
3. **已完成（`v3-road-graph:8.2`，2026-08-13）**：拆分保留 A 侧原 ID、合并保留最小 Edge ID；self-loop seam 为硬边界。连续同向 line 只按 exact-sign 合并，其他 primitive 保持无损链，六类 geometry 用 typed reanchor 写入规范端点。
4. **已完成（`v3-road-graph:8.2`，2026-08-13）**：删除 `RoadGroup` 类型/UID、`GraphEdge.GroupID`、Group 查询/事件、提交结果和所有生产消费者；DebugPanel、fixture 与 contract 改用 Node/Edge/geometry 事实。

独立验收（2026-08-13）：572/572 自动化、0 警告/0 错误 build、0 Roslyn analyzer diagnostics、6 个 GDScript 零诊断和 `git diff --check` 通过。line 4,096/65,536 与 geometry 64/1,024 的首/中/尾固定窗口均为 1 fragment、1 exact test、1 Edge、0 full scan/visit；两次 Release 复跑的 10k 多交叉 P95 为 8.281～9.118 ms，全部场景低于 16.67 ms，100k 多交叉为 20.152～20.184 ms。Godot Tier 3 的 16 路批量交叉得到 50 Node、49 Edge 和 49 rendered Edge/196 mesh 顶点，两个错误通道为空。生产源码和正常 fixture 无 Group 残余；V3 format v1 当时仍由 Phase 5 / `v3-save-system:2.1` 待实现，现已完成该切片并严格拒绝旧 `groups/groupID`。

### Phase 3：闭合与自交路径提交

1. **已完成（`v3-road-graph:8.3`，2026-08-13）**：放宽正长度首尾逐 bit 闭合和精确 full-turn 验证，保持其他退化段拒绝。
2. **已完成（`v3-road-graph:8.3`，2026-08-13）**：统一 incoming/incoming、incoming/existing、相切和 overlap 边界的 witness/cluster 规划，并在 mutation 前完成最终 typed reanchor 与歧义门禁。
3. **已完成（`v3-road-graph:8.3`，2026-08-13）**：离散自交形成 junction，纯二度环形成 rooted seam self-loop，两路口环形成 parallel Edge；连续自重叠结构化返回 `SelfOverlap`。
4. **已完成（`v3-tool-input:2.0`，2026-08-13）**：`RoadPlacementSession` 为三种输入策略提供首锚闭合 preview、精确确认、单次 history，以及右键、工具切换和暂停取消。

独立验收（2026-08-13）：586/586 自动化、0 警告/0 错误 build、0 Roslyn analyzer diagnostics、6 个 GDScript 零诊断与 `git diff --check` 通过。简单环、full-turn、单 junction 环、两 junction 环、棒棒糖、两种八字形、多交点、既有路交叉、顺序稳定和无关 Node provenance 均达到第 5 节规范格式；重叠与冲突 split parameter 无 Node/Edge/ID/事件副作用。最终 Release 10k 多交叉 P95 为 10.268 ms。真实 `MapTest` 闭环为 1 rendered Edge/10 mesh 顶点/1 history，失败回走保留可编辑会话，右键、工具切换和暂停清空 preview；完整 `road_input_strategy_runtime_contract.gd` 输出 `PASS`，两个错误通道为空，临时槽和运行状态已清理。Phase 3 完成，下一实施入口为 Phase 4。

### Phase 4：Edge 级 RoadType 与改造事务

1. **已完成（`v3-road-graph:8.4`，2026-08-13）**：新增稳定 `RoadType`、严格小写 token 和唯一显式 `RoadBuildRequest` 原生路径提交；新几何使用请求类型，既有拆分继承原类型，完全覆盖不隐式改造。
2. **已完成（`v3-road-graph:8.4`，2026-08-13）**：把类型加入 merge key、`GraphEdge` / prepared topology 严格不变式和过渡期 schema 3；同类型接续合并，异类型接缝保留 semantic boundary。
3. **已完成（`v3-road-graph:8.5`，2026-08-14）**：实现全有或全无的 `ChangeRoadType`，成功后再次 canonicalize；空集、重复/失效 ID、NoChanges、四类互转、semantic boundary 合并与 rooted loop 均有回归。
4. **已完成（`v3-road-graph:8.5`，2026-08-14）**：用唯一 `GraphChanged` 发布 created/removed/updated；拆分 lineage、可逆 content revision 与单调 `ChangeSequence`，用完整 `GraphStateToken` 防止错误方向、旧 sequence、重复 delta 和旧 lineage，加入 mutation 重入拒绝和订阅者异常隔离。
5. **已完成（`v3-road-graph:8.5` / `v3-tool-input:2.3`，2026-08-14）**：固定 `prepare mutation plan -> history admission -> root commit`；单命令 delta 超预算在 root、ID watermark、revision、sequence 和事件变化前拒绝，历史以 entry/字节双预算保留 delta；真实 V3 Load 已证明 full reset 创建新 lineage 并清空旧 history/token。

检查点验收：类型化建造、覆盖、异类型边界、改造合并和 NoChanges 符合第 8 节；本阶段只验证 plan/admission/commit 和准确摘要，不提前把撤销/重做算作完成。

Phase 4 独立验收（2026-08-14）：633/633 自动化、0 警告/0 错误 Debug build、0 Roslyn compiler/analyzer diagnostics 与 `git diff --check` 通过。Release 远端扩展中 1k/10k/100k 固定局部改造分别分配 12.2/12.8/13.5 KiB，固定复制 2 个 bucket 页，100,000 次 root capture 为 0 bytes，三档旧 root 均释放；10k 全场景 P95 低于 16.67 ms。真实 `MapTest` 的建造/undo/redo 同步得到 renderer/history 的 `1 Edge/4 vertices/1:0 -> 0/0/0:1 -> 1/4/1:0`，两个错误通道为空。Phase 4 完成；该检查点当时的下一入口为 Phase 5 / `v3-save-system:2.1`。

### Phase 5：V3 独立 format v1

1. **已完成（`v3-save-system:2.1`，2026-08-14）**：统一 `user://saves-v3` 生产根与可注入临时测试根；两个 V2 根保持零枚举、零写入。
2. **已完成（`v3-save-system:2.1`，2026-08-14）**：format v1 codec 只表达 canonical Node/Edge、RoadType、self-loop/parallel Edge 和六类原生几何链；旧 `ISaveable`、V2 DTO/reader、Group 字段、版本分派和迁移入口已删除。
3. **已完成（`v3-save-system:2.1`，2026-08-14）**：reader 严格拒绝错误 family/version、未知/重复字段、Group/groupID、非法 token 和非规范图，不在 Load 中修复或转换数据。
4. **已完成（`v3-save-system:2.2`，2026-08-14）**：canonical writer 直接生成无缩进 UTF-8；固定 buffer 的 `V3JsonStreamReader` 在同一 payload 句柄上完成 byte/token/depth/lexeme、初始/声明/消费长度、SHA-256 和 EOF 验证，实体、geometry、索引和字符串预算在进入集合及提交前执行。manifest 与可选 PNG thumbnail 也分别执行编码/解码预算和严格结构校验。
5. **已完成（`v3-save-system:2.2`，2026-08-14）**：manifest v1 绑定文件名、encoded length/SHA-256；`Absent | CompleteV3 | CorruptV3 | Foreign | Unsafe` 分类、operation-specific publish/delete descriptor、new/old digest 恢复矩阵、quarantine、tombstone、跨进程 OS 根锁、cleanup-pending 与 typed recovery-blocked 路径均已实现并完成故障注入。
6. **部分完成（`v3-save-system:2.3` 开放）**：`SaveOperationCoordinator` 已实现进程内根 gate、结构化 token/state/result、手动优先、pending autosave、取消与退出收敛；公开同步 bool API 已删除。Save/Load/Delete 的长工作移到后台，`PreparedAggregateLoad` 已一次交换 graph、空工具/history、基础 renderer mesh、带 canonical `RoadLocation` 和不可变空间索引的 `EdgeRibbon` + `TerminalCap` + `SemanticJoin` + `JunctionPatch` surface、matching `RoadRenderToken` 和 slot target；patch 细分、surface triangle/disc 复制与建树在 worker Prepare 完成。拆除工具已消费 current surface 并执行 token/graph admission；尚缺其余工具接管、排队 continuation、第二 saveable 与关键资源全矩阵，不能勾选完成。
7. **已验证边界（2026-08-14）**：命名槽、autosave、暂停菜单、V3 Save/Load/Delete、损坏/外来槽拒绝和 V2 根 canary 通过；Windows Desktop QA 导出包证明非 editor 进程使用 V3 根并正确校验 manifest family/schema/长度/hash 后删除槽，可写 profile 完成 Save As/删除，只读 ACL profile 在失败后保持 `CurrentSlotID` 且不发布内容。QA 导出过滤只保留 exported-save 契约与 fixture，并由源码回归测试锁定。

Phase 5 当前仍为部分完成。`v3-save-system:2.1`～`2.2` 已由存档聚焦组、双配置构建、真实 `MapTest` 和 Windows Desktop QA 导出证明；`2.3` 的 coordinator、结构化状态、后台工作、退出收敛及 basic mesh/四类 surface/index/token aggregate 已由完整 833/833 自动化、运行时契约和隔离用户目录的 `road_render_token_runtime_contract.gd` 证明。混合锐角 Load 一次发布 `10 ribbon + 2 SemanticJoin + 4 JunctionPatch + 5 cap = 21` 个 primitive，patch hit 的 token、owner 与 endpoint location 保持同代；Godot MCP 另覆盖普通 T junction。等待 gate 的手动请求会在创建 lease 前重新检查外部取消并释放 gate，场景 drain 不再因取消竞争遗留活动 lease；Load renderer 的 `PreparedData` 也已在 worker 完成 patch 细分和统一 AABB 层级，Preflight 不再承担细分、复制或建树。普通 mutation stalled/retry、拆除与 RoadUpgrade surface/token admission，以及类型化建造的选择/冻结/提交已作为独立切片完成，不改变 Load 禁止提交后表现重试的边界。下一入口不再是“创建 coordinator”“建立 render token”“补 ribbon canonical location”“建立基础 surface index”“生成 terminal cap”“生成 semantic join”“生成 junction patch”“补普通 stalled/retry”“让拆除消费 surface”“实现 RoadUpgrade”或“让类型化建造冻结 RoadType”，而是与 `v3-grid-rendering:2.2`、`v3-tool-input:2.4`、`v3-ui:1.1`、`v3-ui:1.4` 一起完成 RoadType/RoadUpgrade UI、类型化建造的表现门禁、排队 continuation、全命令 admission 和全部故障矩阵。

### Phase 6：可逆 delta 历史

1. **已完成（2026-08-14）**：mutation plan 生成含实体前后值和 content revision 的可逆 delta；完整 token 验证方向和 lineage，revision ID allocator、allocator watermark 与 `ChangeSequence` 不回退或复用。
2. **已完成（2026-08-14）**：建造、交叉拆分、删除、改造和 canonicalize 已纳入同一 history admission/commit 边界。
3. **已完成（2026-08-14）**：entry 数与字节双预算已替换 before/after JSON；任意外部 full reset/new lineage 立即清空历史，真实 V3 Load 已验证旧 undo/redo 和 token 失效。
4. **已完成（2026-08-14）**：简单环、八字形、类型合并、64 Edge 批量删除和超预算拒绝均有直接 `RoadEditHistory` 撤销/重做测试。

Phase 6 验收（2026-08-14）：`RoadEditHistoryTests` 为 16/16，检查点完整套件为 637/637。64 次、每次 1,024 geometry 的既有基线中，delta retained 为 12,308.0 KiB，64 edit + 64 undo + 64 redo 共 192 个普通事件、0 full reset；超预算命令在提交前失败。真实 `MapTest` 先验证建造/undo/redo 与 renderer 一致，再执行 V3 Save、继续编辑和 Load，恢复后 history 为 undo/redo `0/0`，Load 前 token 与两栈不能作用于新 lineage。`v3-tool-input:2.3` 因此完成；后来完成的 `v3-save-system:2.2` 磁盘 token reader/恢复预算不重新打开 Phase 6。

### Phase 7：渲染、类型 UI 与规模门禁

1. 修复 closed ribbon join、seam 标记和 self-loop/parallel Edge 表面生成。
2. 四类样式资源、per-edge width/color、六分量 desired/presented token，以及同源 `EdgeRibbon` triangle + `TerminalCap` disc + degree-2 `SemanticJoin` triangle + degree≥3 `JunctionPatch` triangle 的 `RoadSurfaceSnapshot` / `RoadSurfaceHit` / canonical `RoadLocation` / 不可变空间索引已完成；普通 mutation stalled/retry 与拆除命令 admission 也已完成，继续让其余工具消费 provider 并执行全命令 admission。
3. 拆除与 RoadUpgrade 的 hover、连续轨迹和矩形框选已消费同一已呈现表面；继续增加类型选择器及 RoadUpgrade 的 catalog、图标、按钮、快捷键和上下文联动，并让其余成功普通事务清理失效 owner。
4. 接入 full-reset tool participant 与 PauseMenu；Load 在 Prepare/Preflight 完成 graph、empty tool root、隐藏 mesh/RID、surface/hit index 后一次 non-yield 交换并通知，关键表现失败只发生在 commit 前。
5. 完成唯一 V3 应用装配：只注册新的 graph/renderer/tool/save/UI 实现和必填 `RoadBuildRequest`；完整构建与源码契约证明旧 Group/API/事件/DTO/writer 已删除，没有适配器、双消费、双写或运行时版本选择。
6. 执行 junction-dense、geometry-dense、环路、四工具表面命中和混合类型视觉/性能契约。

Phase 7 当前已有十五个可验证切片。其一，普通 mutation 与 `RoadRendererLoadPreparer` 共用 closed ribbon：显示点列仍保留首尾 seam，mesh 只生成唯一逻辑点的顶点，以循环相邻方向计算 seam miter 并补上末段回首段索引；纯 self-loop 的 A/B incidence 不绘制 marker，self-loop 加支路仍是 junction。方形环、`+Tau` 全圆弧、棒棒糖、两路口环、八字形、支路删除后 seam 重定位、开放 ribbon 和 worker/direct 确定性已有自动化；真实 `MapTest` 的普通提交与 aggregate Load 均为 `1 Edge / 8 vertices / 0 markers`，aggregate Load 后的两路口环从删除前 `4 Edge / 20 vertices / 4 markers` 收敛为删除后 `2 Edge / 12 vertices / 2 markers`。其二，基础 Load participant 已在 Preflight 创建未发布 open/closed ribbon mesh/node batch，`ToolManager`/`RoadBuilder` 已准备空 placement/removal/upgrade/history，PauseMenu 已接入 token/generation/busy/Escape 与退出收敛；`road_renderer_lifecycle_runtime_contract.gd` 证明 renderer 缺失时 Load 在 commit 前失败且旧图/历史/会话/槽不变。其三，`v3-grid-rendering:2.1` 已建立四类 `RoadTypeStyle` 及生产资源，严格校验唯一覆盖和字段合法性，非法查找不 fallback；资源往返与真实 Vulkan `MapTest` 契约均 PASS。其四，`2.2` 已让普通与 Load 路径用不可变样式快照生成同一个 per-edge width/color mesh；聚焦 9/9、完整 750/750、双配置 build、Roslyn/GDScript 0 diagnostics、真实 Vulkan mixed-type 契约与独立 10k/100k 压测均通过。其五，`RoadRenderToken`、稳定 facade ID、desired/presented tracker、样式 revision 与 Load reservation 已完成；完整 763/763，Godot MCP 逐步验证普通 mutation、样式刷新和 Load 的 matching token。其六，`RoadSurfaceSnapshot` 从每个真实 ribbon mesh triangle 同步生成 `EdgeRibbon` owner，提供稳定点命中和矩形 Edge 查询；普通 rebuild 与 aggregate Load 都把 snapshot 和 matching token 一次交换。完整 774/774，Godot MCP 验证 mutation/style/Load hit token 同代、旧 surface 在 Load 后消失，editor 与 DAP 错误通道为空。其七，内部 display path 为每个可见 span 保留源 geometry/参数区间，普通 cache 与 Load prepared payload 共用 provenance；ribbon triangle 以半开所有权唯一映射 geometry join、开放 B 端和 self-loop seam，并在同值候选中优先返回 canonical `RoadLocation`。完整 779/779、双配置 build 与静态诊断均通过；隔离用户目录的 GDScript 契约和 Godot MCP mutation/style/Load 场景都验证 hit location 与 matching token，测试槽和临时用户目录已清理。其八，`RoadSurfaceSnapshot` 在构造期建立稳定 AABB 层级，点/矩形查询只精确测试局部候选；Load worker 通过不透明 `PreparedData` 完成 triangle defensive copy 与索引建树，Preflight 只绑定 token。聚焦组合 27/27、完整 782/782、双配置 build 与 Roslyn/GDScript 诊断通过；隔离 headless 契约及 Godot MCP aggregate Load 均保持 matching token 和 canonical location。其九，degree-1 Node 以相邻 Edge 的 `RoadTypeStyle` 生成同宽同色 marker 和解析 `TerminalCap` disc，A/B owner 携带 Node/Endpoint 并返回 canonical 参数 `0/1`；普通 rebuild 与 Load worker 产生相同 primitive，triangle/disc 使用分离 defensive array 和统一 AABB 查询。聚焦组合 34/34、完整 786/786、双配置 build 与静态诊断通过；隔离运行时契约覆盖 mutation/style/Load，Godot MCP 验证单路 `surfacePrimitiveCount=4`、A/B cap 命中及 matching token，本轮 1k Vulkan P95/rebuild 也通过。其十，两条不同 Edge、不同 RoadType 的 degree-2 Node 从实际 ribbon 端截面生成 `SemanticJoin`：非共线 bevel 按外边中点划分两个纯色 sector，同向方向使用固定 RoadType 顺序和最大半宽 fallback，精确对向不生成多余 primitive；每个 join triangle 保留 Edge/Node/Endpoint、sector 和固定 canonical endpoint location，且不绘制 degree-2 marker。聚焦组合 41/41、完整 793/793、双配置 build 与静态诊断通过；隔离混合边界 Load 发布 `4 ribbon + 2 cap + 2 SemanticJoin = 8` 个 primitive，并验证 matching hit token/owner/location。本轮未重跑 10k/100k。

其十一，degree≥3 Node 已使用固定量化的 Clipper2 `RoadJunctionTessellator` 生成 `JunctionPatch`：exact incidence 排序、self-loop A/B、同向 RoadType/Edge ID 规则、4 倍 half-width miter 上限、弧长中点 owner sector、反向存储和 ID 重命名均有直接回归；普通与 Load 共用 `AppendJunctionPatch` 并移除旧圆形 junction marker。完整自动化 813/813、双配置 build 与静态诊断通过；两个隔离运行时契约均 PASS。混合锐角 Load 为 `10 ribbon + 2 SemanticJoin + 4 JunctionPatch + 5 cap = 21 primitives / 38 vertices / 5 markers`，两路口环普通/Load 与支路删除分别为 `4 Edge / 38 vertices / 2 markers / 20 primitives` 和 `2 Edge / 21 vertices / 1 marker / 14 primitives`；Godot MCP 普通 T junction 为 `3 Edge / 21 vertices / 3 markers / 12 primitives`，截图无洞、尖刺或旧圆点。本轮未重跑 10k/100k。

其十二，普通 mutation 已复用 Load 的纯 `RoadRendererLoadPreparer`，只重采样 created/updated Edge，并在目标 revision/token 仍匹配时一次交换完整 cache/mesh/node batch/surface。失败保留上一代完整表现，公开 stalled token、attempt 和异常信息，provider 拒绝 hit；同一 desired token 可显式重试，新请求会取代失败并拒绝迟到结果。`RoadRenderTokenTests`、preparer/lifecycle/display sampler 契约和隔离 GDScript 运行时覆盖 attempt 1 stalled、attempt 2 同 token 失败、修复后 attempt 3 同 token 成功；当前完整自动化 833/833，双配置 build 0 错误、Roslyn/GDScript 0 diagnostics，editor 与 DAP 增量错误通道为空。

其十三，拆除 hover、连续轨迹与 Shift 矩形框选已从 RoadGraph centerline 查询切换到内部 `IRoadSurfaceSelectionProvider`。会话开始冻结完整 `RoadRenderToken`；每次查询都要求 expected token 同时等于 desired、presented 和 snapshot token，失配会清空稳定 Edge ID 集并结束预览。`ConfirmRemove()` 在调用 `RoadGraph.RemoveEdges()` 前再次校验 provider current、`GraphFacadeID` 和 `ChangeSequence`，空选择或旧 token 不产生 history。拆除/history/surface/renderer 聚焦组合 51/51、完整自动化 833/833、双配置 build 和静态诊断通过；两个隔离道路运行时契约均 PASS。Godot MCP 验证样式刷新后旧确认失败且 undo 不变、当前 token 删除成功，以及 mutation pending 期间旧 edge/cache/mesh/surface 完整保留并在两帧后一次发布空图。

其十四，`RoadBuilder.SelectedRoadType` 为类型化建造提供与网格策略解耦的单一状态，新 Builder 默认 `Street`；`SetSelectedRoadType()` 拒绝未定义枚举，同值重选保持活动会话，合法变化取消未提交 placement。`RoadPlacementSession` 构造时校验并冻结只读 `RoadType`，`ConfirmPlace()` 只用 `session.RoadType` 构造请求，`IRoadInputStrategy` 与 `RoadPathDraft` 仍只负责几何。full-reset Load 清空 placement/removal/history 但保留明确选择。类型化建造聚焦组合 60/60、完整自动化 833/833、双配置 build 和 Roslyn/GDScript 诊断通过；隔离 `road_input_strategy_runtime_contract.gd` 验证四类保存/Load 与 full-reset 选择保留，Godot MCP 验证默认/同值/无效/切换生命周期及 `Highway` 提交后的 `1 Edge / 4 vertices / 1 undo`。

其十五，独立 `RoadUpgradeSession` 在开始时冻结合法目标类型与完整 `RoadRenderToken`；连续轨迹以有界采样查询 current presented surface，Shift 矩形替换排序去重的 canonical Edge ID 集。`ConfirmUpgrade()` 在 graph facade/change sequence 仍匹配时一次调用 `ChangeRoadType()` 并记录一条历史；NoChanges、空选择、取消和旧 token 均无图事件或历史。工具/类型/策略切换、暂停、undo/redo、graph 注入与 full reset 都清空独立会话和 overlay，成功 Load 保留 `CurrentTool` 与 `SelectedRoadType`。RoadUpgrade 聚焦组合 17/17、完整自动化 833/833、双配置 build 和 Roslyn/GDScript 诊断通过；隔离输入契约与 Godot MCP 验证连续/矩形改造、semantic boundary 合并、单次 undo/redo、样式刷新后的旧 token 拒绝，以及 active upgrade 上真实 Save/Load 后 transient state/history 全空且 matching token ready。

`v3-grid-rendering:2.0` 仍缺缩放/重建视觉矩阵和完整平行 Edge 工具矩阵；拆除与 RoadUpgrade 已经消费 `RoadSurfaceSnapshot`/`RoadSurfaceHit` 的 canonical `RoadLocation`、四类 owner、统一基础空间索引和 provider 级 stalled/retry 门禁，类型化建造也已完成选择/冻结/提交基线；但 RoadType 选择器、RoadUpgrade 的 catalog/图标/按钮/快捷键/上下文联动、类型化建造的 pending/stalled 表现门禁、排队 continuation、其余道路命令 admission 和最终 UI 仍未完成。因此 `v3-grid-rendering:2.0`、`2.2`、`v3-tool-input:2.4`、`v3-ui:1.1`、`v3-ui:1.2`、`v3-ui:1.4` 和 `v3-save-system:2.3` 均保持开放，现有拆除、改造、类型化建造、普通重试聚焦证据、历史 1k 增量 PASS 与既有 100k 历史 PASS 都不能替代本阶段完整门禁。

### Phase 8：最终组合验收

在同一 `MapTest` 实例中完成连续折线路、跨提交延伸、简单环、棒棒糖、两路口环、八字形、支路删除重归一化、四类型建造与改造、token 防护的 delta 撤销重做、不可变 root 结构共享/释放、V3 family/version 有界往返、跨进程锁/publish lease/descriptor 恢复、共享表面命中与 junction patch、损坏/超限拒绝、并发 autosave、取消和 observer warning，以及成功/提交前失败且无提交后关键表现失败分支的 Load 生命周期。额外证明 V2 根未被枚举或修改、手工复制的 V2/未知格式被拒绝。再完成 Vulkan 视觉、10k 硬门槛、100k 压测和 Windows 导出验证。最终证据写回附录 D；`v3-road-graph:8.6` 是唯一集成负责人。

---

## 13. V3 跨系统所有权

| 需求                                           | owning system          | 关键位置                                                   | 集成关系                                    |
| ---------------------------------------------- | ---------------------- | ---------------------------------------------------------- | ------------------------------------------- |
| 数值/容量、incidence、自环、规范 Edge、查询 fragment、事件序列 | `v3-road-graph` | `GraphNode`、`GraphEdge`、`SpatialIndex`、`RoadGraph*` | V3 领域前置和最终集成负责人 |
| V3 format/manifest v1、独立保存根、有界 I/O、跨进程锁、发布/恢复和 aggregate load | `v3-save-system` | `RoadGraph.Persistence.cs`、`SaveManager`、`SaveSlotStore`、`AutosaveController` | 消费最终规范形；负责操作结果，不自行定义拓扑 |
| closed ribbon、junction patch、分级 mesh、surface snapshot、presentation 与性能 | `v3-grid-rendering` | `RoadConfig`、`RoadRenderer`、Godot 渲染契约 | 发布 `PresentationReady`，向 `v3-road-graph:8.6` 提供渲染门禁 |
| 闭合草稿、类型状态、改造选择、有界历史和 full-reset 输入清理 | `v3-tool-input` | `RoadBuilder`、`RoadPlacementSession`、`RoadEditHistory` | 消费 RoadGraph token/delta 和 presented surface，不定义拓扑 |
| 类型控件、改造工具、存档 busy/presentation 状态和 DebugPanel 指标 | `v3-ui` | `ConstructionDock`、`ToolContextPanel`、`PauseMenu`、`DebugPanel` | 独占 load 期间 Escape 与暂停，不直接写图 |

速度、容量、寻路与拥堵不属于 V3 owning system；它们在 V3 完成后由根层 [`traffic-simulation`](../todo/traffic-simulation.md) 路线图启用，并必须理解 self-loop 与 parallel Edge。

---

## 14. 主要风险与防护

### 14.1 只压缩折线，曲线被降级

防护：先实现六类原生几何反向；合并只拼接原生对象，不经显示采样重建。

### 14.2 自环只在 Edge 层放行

防护：incidence、degree、detach、diagnostics、persistence、renderer 和查询必须作为一个垂直切片通过测试。

### 14.3 归一化依赖遍历顺序

防护：节点工作队列按 ID 排序；纯环 seam、拆分保留 ID 和合并保留 ID 都有确定规则；等价输入做结构比较。

### 14.4 RoadGroup 以隐藏形式回流

防护：merge key 和 schema 均禁止 submission/provenance 字段；操作来源只存在于编辑历史或未来独立日志。

### 14.5 改造后消费者仍缓存旧 Edge ID

防护：统一 `GraphChanged` 同时表达 created/removed/updated；选择、renderer 和未来 TrafficGraph 都以事务后状态重取实体。

### 14.6 闭环被误判为退化或重复覆盖

防护：退化以权威长度和几何合法性判断；首尾重合是显式闭合语义；覆盖按原生几何 overlap 而非端点对或 JSON 字符串判断。

### 14.7 reader 静默修复非规范数据

防护：V3 format v1 先验证 family/version 和全部领域不变式；非规范输入直接拒绝，不在加载时 canonicalize、补默认类型或猜测旧字段。

### 14.8 长 Edge 让局部查询退化

防护：RoadGraph 索引 query fragment 的 geometry/parameter 身份保留到精确测试后；已呈现 `EdgeRibbon` + `TerminalCap` surface 也用构造期不可变 AABB 层级限制 triangle/disc 候选，Load 在 worker Prepare 预建该索引。固定窗口基准随远端长度或远端 surface primitive 增长保持有界，容量超限不回退全图扫描。

### 14.9 manifest 存在被误当成完整发布

防护：V3 manifest v1 描述每个 payload 的 encoded length/SHA-256 并最后写入；不可变 publish descriptor 绑定新旧 aggregate digest 和唯一事务路径。恢复只按 descriptor 矩阵处理其拥有的 slot/staging/backup；`Foreign`、`Unsafe` 或无法证明的组合保留现场并返回结构化阻塞。

### 14.10 后台结果覆盖更新状态

防护：后台只读不可变 root/snapshot，Godot 对象和图 swap 留在主线程；render 使用包含 scene、facade identity/generation、sequence、style 和 request generation 的完整 token，save/load 使用 operation/generation/publish lease，过期结果丢弃。首次保存、覆盖、Delete 与 Load 各自只在声明的不可取消点越界，之后必须完成发布、恢复、删除收敛或 non-yield swap。

### 14.11 混合宽度路口出现洞或尖刺

防护：Edge ribbon 在 incidence 截面裁切，Node junction patch 统一填充；排序、miter fallback 和颜色归属独立于遍历顺序和任意 ID 数值。更换等价图的 ID 后像素必须相同，surface owner 则按同一 ID 重命名映射等价，不能要求 owner 仍携带旧 ID。

### 14.12 新图已经提交但旧画面仍可交互

防护：普通 mutation 的 root commit 与异步表现接管之间以 `DesiredToken != PresentedToken` 禁用全部道路交互，失败进入 `RoadPresentationStalled`。Load 不允许该窗口：graph、empty tool root、隐藏 mesh/RID、surface snapshot、hit index 和 token 全部在 Preflight 准备并在同一 non-yield commit 交换；关键表现失败只能发生在提交前。

### 14.13 不可变 root 退化成每次复制全图

防护：Entity、geometry 和索引采用持久化页/子树结构共享；局部 mutation 的复制页数、分配和保留内存纳入 1k/10k/100k 远端扩展基准，后台 snapshot 与历史淘汰后验证旧 root 可释放，不能只测试 O(1) 引用捕获。

### 14.14 保存根隔离失效

防护：V3 只解析 `user://saves-v3`，所有操作从已验证的 V3 root capability 派生路径；测试在两个 V2 根放置 canary，并覆盖 list、Load、Save、Save As、Delete、autosave、恢复和启动清理，断言 canary 的目录项、时间和字节均未变化。手工复制到 V3 根的 V2/未知内容只分类为 `Foreign`，不触发导入或删除。

### 14.15 把协作完整性误报成安全保证

防护：测试和文档明确根锁只协调守约实例，同句柄只绑定实际解析字节，SHA-256 只检测无密钥完整性；绕过锁且有写权限的进程、来源认证和防回滚不在 V3 保证内。

---

## 15. 完成定义

只有同时满足以下条件，V3 才能标记完成：

1. N 个同向无分支单位直线输入最终保存为 2 个 Node、1 条 Edge 和 1 个起点到终点的 line geometry；含折角或不同原生曲线的无分支道路仍为 1 条 Edge，但保存无损且不可再合并的 geometry 链；弯道不制造拓扑节点。
2. junction、terminal、semantic boundary 和 loop seam 是唯一合法 Edge 边界。
3. self-loop、parallel edge、精确 `+/-Tau` full-turn、简单环、棒棒糖、两路口环和八字形通过自动化与真实渲染验证；self-loop 以 rooted chain 保持 seam 硬边界和稳定 typed direction key。
4. `EdgeIncidence` 能区分 A/B；自环 degree 为 2，删除和重建不残留引用。
5. 每次建造、拆分、删除、改造和恢复后都达到规范形；失败无副作用。
6. RoadGroup 已从 V3 canonical graph、公共提交结果、DebugPanel 指标和 format v1 移除；旧 API、事件和 DTO 不再编译。
7. 六类原生几何在反向、拼接、拆分、存档和显示间不降级、不丢控制参数。
8. 四类 RoadType、semantic boundary、类型化建造和原子改造符合第 8 节。
9. V3 format v1 严格往返；full reset 创建新 lineage 并精确采用槽内 `nextID`；错误 family/version、`Foreign`、未知和损坏内容不改变活动图，也不被转换或修复。
10. V3 manifest v1 保留受限摘要元数据并通过同句柄长度/hash/token lexeme、跨进程锁和 publish lease 门禁；恢复遵守五类 V3 occupant、publish descriptor 和 delete descriptor 矩阵。`PublicationRecoveryBlocked`、`DeletionRecoveryBlocked`、`PublishedWithCleanupPending`、`DeletedWithCleanupPending` 和 crash-recoverable/durable 声明边界均通过故障测试；V2 根在所有 V3 操作中逐字节未触碰。
11. closed ribbon 无 seam 裂缝或伪端点；平行 Edge 可见、可命中、可选择；混合宽度 junction/semantic boundary 无洞、尖刺或遍历顺序差异；hover、拆除、改造和框选共用与 mesh 同 token 的 `RoadSurfaceHit`。
12. query fragment 以半开所有权让 cut/join/B 端/seam 恰好一次命中，局部查询不随同一 Edge 的远端长度/geometry 数线性增长；exact-sign line predicate、极值坐标、长度、索引容量和 ID 耗尽在事务前结构化失败。
13. delta 用 lineage/revision/sequence token 拒绝错误方向、重复重放和旧 lineage；成功 full reset 原子清理全部旧图工具/overlay/历史状态，失败 load 逐值保留当前会话；后台派生结果不能通过旧 render token 覆盖新状态。
14. junction-dense 10k 硬门槛通过，100k 与 geometry-dense 存储/归一化数据完整记录，并包含主线程 snapshot/load/mesh 接管卡顿指标。
15. 完整自动化、Debug 构建、Godot 主场景、Vulkan 视觉、命名/自动存档和 Windows 导出门禁有持久证据。
16. V3 format v1 使用确定 UTF-8 和有界单次解析；不可变 root 允许保存 O(1) 捕获且加载预建全部派生索引，局部 mutation 通过结构共享避免复制无关远端图且已失效 root 可释放；长 Edge 不产生存储伪节点；编辑历史不再保留 before/after 全图 JSON，超预算编辑在提交前拒绝。
17. Load 经过 Admission、Prepare、Preflight 和一次 non-yield commit/notification；graph、tool、mesh/RID、surface/hit index、token 与 `CurrentSlotID` 同时交换，提交后只允许普通 observer warning，不存在关键表现失败分支。PauseMenu 的 Escape/generation/token 状态机无旧 continuation。
18. `TrafficGraph`、A*、拥堵和高程道路等排除项没有被误报为 V3 能力。

---

## 附录 A：实施文件地图

| 文件                                                  | V3 预期职责                                          |
| ----------------------------------------------------- | ---------------------------------------------------- |
| `Scripts/Road/GraphNode.cs`                         | endpoint-role incidence、degree 与 self-loop 邻接    |
| `Scripts/Road/GraphEdge.cs`                         | canonical Edge、原生几何链和 RoadType                |
| `Scripts/Road/Geometry/`                            | 六类原生几何反向契约                                 |
| `Scripts/Road/RoadGraph.cs`                         | 数值/容量、删除、查询、统一事件、Group 移除和 canonicalize 调度 |
| `Scripts/Road/SpatialIndex.cs`                      | 参数区间 query fragment、bucket/ref 预算和局部查询指标 |
| `Scripts/Road/RoadGraph.PathSubmission.cs`          | 开放/闭合路径提交和 mutation plan                    |
| `Scripts/Road/RoadGraph.NativePathIntersections.cs` | incoming 自交、既有交点与 overlap 规划               |
| `Scripts/Road/RoadGraph.NativeSubdivision.cs`       | 拆分 ID、原生几何和 incidence 一致性                 |
| `Scripts/Road/RoadGraph.Diagnostics.cs`             | self-loop、parallel edge、规范形断言和 O(1) 诊断快照 |
| `Scripts/Road/RoadGraph.Persistence.cs`             | 无 Group 的 V3 format v1 与严格 family/version reader |
| `Scripts/Core/SaveManager.cs`、`AutosaveController.cs` | 保存根排他 gate、snapshot/load 提交和 autosave 合并 |
| `Scripts/Core/SaveSlotStore.cs`                     | V3 manifest v1、独立保存根、同句柄流式校验、publish descriptor 与恢复矩阵 |
| `Scripts/Road/RoadPathSubmissionResult.cs`          | 无 Group 的 created/removed/updated 事务摘要         |
| `Scripts/Road/RoadTypeStyle.cs`                     | 四类 RoadType 的首版展示字段、合法性校验与不可变值快照 |
| `Scripts/Road/RoadConfig.cs`                        | 四类样式唯一覆盖、严格查询、资源校验与主线程快照捕获 |
| `Scripts/Road/RoadRenderer.cs`                      | 已实现 closed ribbon、per-edge width/color、四类 surface、mesh/surface/token 同代接管、普通 mutation stalled/retry 与拆除使用的 current provider 边界 |
| `Scripts/Road/RoadJunctionTessellator.cs`           | 固定量化、exact incidence 排序、Clipper2 union/triangulation、miter fallback 与确定 owner sector |
| `Scripts/Road/RoadSurfaceSnapshot.cs`               | 已实现四类 owner/hit、canonical location、统一不可变 AABB 点/矩形查询及 Load `PreparedData` |
| `Scripts/Road/RoadBuilder.cs`                       | 闭合确认、类型状态，以及拆除/改造的 surface/token admission 与事务提交 |
| `Scripts/Road/Input/`                               | 闭合 placement、surface provider，以及拆除/改造各自冻结 token 的 canonical Edge 选择生命周期 |
| `Scripts/Road/Input/RoadEditHistory.cs`             | entry/字节双预算的可逆 delta 历史                     |
| `Scripts/Tools/`                                    | 已注册稳定 `ToolType.RoadUpgrade`；工具路由保持 placement/removal/upgrade 互斥 |
| `Scripts/UI/`                                       | RoadType 控件、RoadUpgrade 呈现和 DebugPanel V3 指标 |
| `tests/SimpleCities.RoadGraph.Tests/`               | 领域、规范化、事务、格式和保存根隔离契约             |
| `tests/godot/`                                      | 主场景、环路视觉、性能和导出契约                     |

## 附录 B：关键行为矩阵

| 操作                                      | 规范结果                                                               |
| ----------------------------------------- | ---------------------------------------------------------------------- |
| 一次提交共线 `A -> p1 -> p2 -> B`      | 2 Node、1 Edge、1 个从 A 到 B 的 line geometry                         |
| 一次提交`A -> bend1 -> bend2 -> B`      | 2 Node、1 Edge；只保留不可无损合并的折角 line geometry                  |
| 先提交`A -> C`，再同类型提交 `C -> B` | 2 Node、1 Edge；C 消失                                                 |
| 上述第二次提交类型不同                    | 3 Node、2 Edge；C 是 semantic boundary                                 |
| 新路接入既有 Edge 内部                    | 接入点成为 junction，既有 Edge 在该处形成两个 canonical run            |
| 删除 T 形支路                             | 原 junction 若退化为同语义二度节点则合并                               |
| 简单闭环                                  | 1 seam Node、1 self-loop Edge                                          |
| 给简单环增加一条支路                      | seam 所在接入点成为 junction，仍为 1 self-loop + 1 branch              |
| 环上再增加第二个支路                      | 两 junction 之间形成 2 条平行 Edge                                     |
| 删除两路口环的一条支路                    | 退化 junction 被消除，两条环弧经该点合成另一个 junction 上的 self-loop |
| 完全覆盖异类型道路                        | `FullyCovered`，不改类型                                             |
| 改造后与邻 Edge 同类型                    | semantic boundary 消失并规范化合并，摘要报告 updated/removed           |
| V3 format v1 canonical payload              | 严格往返并保持 Node/Edge、rooted seam、RoadType、原生 geometry 和 `nextID` |
| 缺失/错误 family 或 version                 | 在业务 DTO、图准备和场景事件前拒绝，无副作用 |
| V2 根存在任意槽和 canary                    | V3 list/Save/Load/Delete/autosave/恢复均不枚举、不打开、不修改 |
| V2/未知目录被复制到 V3 根                   | 分类为 `Foreign`，不显示为正常槽，不加载、不转换、不自动删除 |
| occupant 是 `CorruptV3`                     | 可显示为损坏槽；不加载或覆盖，只能在明确确认后删除目标 |
| occupant 是 `Unsafe`                        | 停止且保留全部证据，不移动、覆盖或删除 |
| slot/staging/backup 匹配 publish descriptor | 按 new/old digest 矩阵完成发布、恢复旧槽或清理 |
| descriptor 与路径/digest 不一致             | `PublicationRecoveryBlocked`；保留现场且不猜测赢家 |
| 首次 Save / 覆盖 Save                       | 不可取消点分别为 `staging -> slot` / `slot -> backup` |
| 最终 V3 槽已复核但 cleanup 失败             | `PublishedWithCleanupPending`；不回滚或误报发布失败 |
| Delete 越过 `slot -> tombstone`             | 槽逻辑删除；匹配当前目标时清空 `CurrentSlotID`，cleanup 失败不移回 |
| delete descriptor/tombstone 不一致          | `DeletionRecoveryBlocked`；保留现场且不猜测或重复删除 |
| Load 关键 participant 失败                  | 只允许在 Preflight 失败，活动 graph/tool/mesh/token/slot 逐值不变 |
| Load 普通 observer 抛异常                   | 完整 commit 保持，返回 `SucceededWithObserverWarnings` |

## 附录 C：V2 历史边界

- V2 的完成证据保持原样，不因 V3 重构而改写为失败。
- 连续空间、六类原生几何、精确查询、命名槽和批处理渲染是需要以 V3 新契约重新验证的行为基线，不构成 API 或格式兼容要求。
- V3 可以完全重写 V2 架构，并有意取代 waypoint 可成为 Node、Group 阻止跨提交合并、删除后不自动归一化等决定。
- V2 保存根和格式是只保留、不读取的历史数据；V3 不提供迁移工具，也不向 V2 分支回填 self-loop、RoadType 或 Group 移除。

## 附录 D：最终验收记录

> Phase 8 前保持为空。各中间切片的证据记录在对应 `docs/todo/v3/` 工作项及本文顶部“当前实施记录”；只有全部 V3 系统工作项的声明门禁实际通过后，才能在本附录记录最终命令、测试数量、性能数据、截图和运行时结果，不得用单个基础切片或设计完成代替最终实现完成。
