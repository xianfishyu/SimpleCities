# 第三代存档系统待办清单

> 系统 key：`v3-save-system`
> 整理日期：2026-08-13
> 证据：当前工作区 `SaveManager`、`SaveSlotStore`、RoadGraph 持久化源码与存档自动化，V2 历史路线图 `docs/todo/save-system.md`，以及 `docs/manuals/road-system-v3-gen.md` 第 10 节。
> 主导原则：第三代存档必须以严格 schema、确定字节、有界流式 I/O、证据保全和 prepared aggregate 保护长连续 Edge；`PublishV2`、`ResolveLegacyState` 与 `Load` 永远是三个独立事务、三个 operation token 和三个结果，磁盘解决不能伪装成场景加载。

## 状态总览

<a id="v3-save-system2"></a>

| ID | 发现 | 当前状态 | 处置方式 |
|---|---|---|---|
| 2.1 | V2 schema 1 仍保存 Group、拒绝 self-loop 且没有 RoadType | 开放 | 以独立旧 codec 严格迁移到 canonical Node/Edge schema 2 |
| 2.2 | 完整 DTO/字符串、manifest v1 和无容量上限不能可靠承载长 geometry，也不能证明中断现场 | 开放 | 建立确定流式容器、同句柄校验、五类 occupant 和 intent-first 恢复 |
| 2.3 | 同步 save/load/autosave、固定事务路径和顺序 Restore 缺少并发及原子会话协议 | 开放 | 建立保存根 coordinator、prepared aggregate 与一次 non-yield Load commit |

### 设计覆盖矩阵

| 设计范围 | 当前事实 | 关联待办 |
|---|---|---|
| schema 2 与 V2 迁移 | 当前严格 schema 1 是 Node/Edge/Group，Edge 无类型且拒绝相同端点；V3 图要求无 Group、RoadType、self-loop、parallel Edge 和 canonical geometry | `v3-save-system:2.1`、`v3-road-graph:8.0`～`8.5` |
| 长连续 Edge | 当前 capture、缩进 UTF-16 字符串和重复完整解析会放大峰值；读取前没有文件、实体和 geometry 预算 | `v3-save-system:2.2`、`v3-road-graph:8.0`、`v3-road-graph:8.5` |
| 容器完整性与恢复 | manifest v1 只列文件名；当前 slot/backup 恢复不能证明候选完整，也没有 legacy/unknown/unsafe 证据保全协议 | `v3-save-system:2.2` |
| 三事务权限边界 | Publish、legacy 解决和场景 Load 若共享 token 或组合结果，会混淆磁盘已发布与活动会话已交换 | `v3-save-system:2.2`～`2.3`、`v3-ui:1.4` |
| Load 生命周期 | 当前逐个 restore 不能保证 graph、tool、mesh、surface、hit index 和可写目标全有或全无 | `v3-save-system:2.3`、`v3-road-graph:8.5`、`v3-grid-rendering:2.2`、`v3-tool-input:2.4`、`v3-ui:1.4` |

## 执行顺序

### 阶段 2：第三代道路 payload、容器与操作协议

<a id="v3-save-system2.1"></a>

- [ ] **2.1 提升道路 schema 并迁移严格合法的 V2 存档**
  - 当前问题：`RoadGraph.Persistence.cs` 只接受 `schemaVersion = 1`，保存 Node/Edge/Group、拒绝 `NodeAID == NodeBID` 且 Edge 没有 RoadType；直接删字段或放宽端点会破坏 V2 已发布格式的严格边界。
  - 修改：实现 schema 2 的独立 codec 和 immutable prepared model，payload 只包含 Node/Edge；Edge 必填大小写稳定的小写 `roadType` 和原生 geometry，允许正长度 self-loop 及几何不同的 parallel Edge。schema 1 必须先由独立旧 DTO 按已发布规则完整校验 Group/Edge 双向成员、端点、几何、全局 ID 和 `nextID`，再在临时模型中删除 Group、为全部 Edge 赋 `Street`，按 `v3-road-graph:8.2`～`8.3` 的 ID 与 rooted seam 规则 canonicalize，并执行 schema 2 全部不变式。prepared `RoadGraphRevision` 精确保留合法 payload 的 `nextID`；full reset 建立新 runtime lineage，不与旧活动图取最大 watermark。schema 2 严格拒绝未知字段、Group/groupID、非规范二度节点、非法 self-loop/full-turn、内部交叉、重复覆盖和非法 RoadType。生产 writer 在原子 cutover 前只写 manifest v1/payload schema 1，cutover 后只写 manifest v2/payload schema 2，永不生成混合组合；任何 Load 都不原地升级来源槽。
  - 依赖：`v3-road-graph:8.0`～`8.5`；沿用 V2 已完成的严格门禁 `save-system:0.4`、临时模型校验 `save-system:0.5` 和 Node/Edge/Group 命名基线 `save-system:5.3`，但不修改其历史结论。
  - 集成负责人：`v3-save-system`；最终端到端判定由 `v3-road-graph:8.6` 负责。
  - 验证：schema 1 同/跨 Group 碎片、非共线折线、六类几何和合法闭合输入迁移；schema 2 开放 Edge、rooted self-loop、parallel Edge、精确 full-turn、四种 RoadType 和确定 JSON 往返；manifest/payload 的 v1/s1、v1/s2、v2/s1、v2/s2 四组合；Group/groupID、大小写、规范整数 token、未知类型、缺字段、内部交叉、可合并二度节点、损坏引用、allocator watermark、`-0`、加载较小 `nextID`、旧 lineage delta 拒绝、失败活动图不变及迁移后 Save As。
  - 验收：严格合法的 schema 1 确定迁移为无 Group 的 canonical `Street` 图；schema 2 精确保留 incidence、rooted loop seam、parallel Edge、原生 geometry 和 RoadType；Load 建立新 lineage，首个新 ID 精确使用槽内 watermark；四种版本组合遵守固定矩阵，生产 writer 不制造混合格式；非法输入在图事件和活动状态变化前失败。

<a id="v3-save-system2.2"></a>

- [ ] **2.2 为长连续 Edge 建立确定、有界且可恢复的存储管线**
  - 当前问题：当前保存先物化完整 DTO，再生成缩进 UTF-16 字符串并整体保留到 staging 写入；加载又经过 `ReadAllText -> JsonDocument.Parse -> DTO deserialize` 的重复完整表示。读取前没有文件字节、Node/Edge、单 Edge geometry、全图 geometry 和索引引用上限；manifest v1 只有文件名，slot/backup 并存时也不能证明候选完整或未被静默截断。
  - 修改：schema 2 保持 geometry 内联 Edge 的无 BOM、无缩进 UTF-8 JSON，不引入拓扑 chunk。新增 streaming saveable contract，并为其他小型 `ISaveable` 提供有界适配器；canonical writer 直接从不可变 `RoadGraphRevision` 写 operation-specific staging。严格 token reader 从同一个禁止共享写/删的句柄验证属性原始字节、重复/未知字段、number lexeme、initial/declared/consumed length、EOF 与 SHA-256，并在元素加入集合前执行文件、槽、实体、geometry、坐标、长度、ID、fragment/index ref、深度和字符串预算。manifest v2 严格约束 `slotId`、`displayName`、`timestamp`、`cityName`、`population`、`funds`、`thumbnailFile` 及按名称排序的 payload metadata；thumbnail 独立校验，缺失或损坏只降级为占位 warning。
  - 恢复协议：恢复必须先处理 pending resolution intent，再将 canonical slot 和 backup 分别且仅分类为 `Absent | CompleteV2 | ManifestV1 | OtherUnproven | Unsafe`，随后按 `unsafe stop -> legacy freeze -> v2 matrix -> preserve unknown` 收敛，最后才隔离不属于 intent 的 partial staging。任一 slot/backup 含 `ManifestV1`、`OtherUnproven` 或 `Unsafe` 时，普通覆盖和自动恢复均不得移动、删除或覆盖现场；Save As 只能选择两个 occupant 都为 `Absent` 的新逻辑槽。
  - legacy 协议：`LoadLegacyCandidate` 属于只读 `Load`，执行前必须复核 `LegacyStateToken`。该 token 绑定规范保存根 identity、逻辑槽 ID、slot/backup 及固定 direct-child 的 ordinal 名称集合和对象类型、每个普通文件的 length/raw SHA-256、manifest 原始字节 hash 与 occupant 分类、所有合法 candidate 的 prepared aggregate digest、operation generation 和用户选择。任一值变化都返回 `StaleLegacyState` 且无副作用；成功后活动会话是“只读来源、无可写目标”，普通 Save 只能转为 Save As。
  - 三事务边界：`PublishV2` 只发布已通过恢复扫描的正常 v2 staging；`ResolveLegacyState` 只归档/解决磁盘 legacy occupant 并发布所选内容的 v2 表示；`Load` 只读磁盘并交换易失活动会话。三者必须分别取得新的 operation token、分别返回结果，禁止复用 token、组合 `Resolve-and-Load` 或把其中一个成功推断成另一个成功。Resolve 不加载 graph/tool/mesh、不修改 `CurrentSlotID` 或可写目标；Load 不移动、删除、归档、升级或写回任何槽。
  - intent 与发布：`ResolveLegacyState` 使用独立 staging、不可覆盖归档路径和固定不可变 crash-recovery intent；intent 只记录 token/digest、所选 prepared digest、staging digest、目标 v2 描述和固定路径，不记录可变 phase，进度由路径与 digest 推导，完成另写只增不改的 marker。intent 发布是 Resolve 的不可取消点；之后幂等归档、发布、终检或恢复候选。持续 ENOSPC、ACL、路径占用、digest 变化或归档冲突返回 `ResolutionRecoveryBlocked` 并保留 intent、staging、归档和全部 occupant 证据。首次 `PublishV2`、覆盖 `PublishV2` 的不可取消点分别是 `staging -> slot`、`slot -> backup`；最终路径 v2 已完整复核后即视为发布，后续 cleanup 失败返回 `PublishedWithCleanupPending`，不得回滚新槽或误报发布失败。只有文件与目录元数据 flush 及断电测试都有平台证据时才可称 durable；否则 API、UI 和验收只能称 crash recovery。
  - 依赖：`v3-road-graph:8.0`、`v3-road-graph:8.5`、`v3-save-system:2.1`；实现前以 Phase 0 的 junction-dense/geometry-dense fixture 和同机 V2 数据固定预算，最终数值由 `v3-road-graph:8.6` 消费，不反向等待 8.6。
  - 集成负责人：`v3-save-system`；编辑历史内存属于 `v3-tool-input:2.3`，最终完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：确定输出与四版本组合；manifest null/range/长度/lexeme、重复字段、大小写、string-number 和 UTF-8 边界；同句柄 length/hash/EOF、替换/删除、额外/缺失文件及 thumbnail 门禁；1/N/上下限 geometry、自环、parallel Edge、O(1) capture 和峰值分配；五类 occupant 全矩阵、pending intent 优先、三个事务及各自不可取消点；`LegacyStateToken` 任一绑定值变化；intent publish、各归档 move、v2 publish、final verify、completion marker 和 cleanup 崩溃；持续 ENOSPC/ACL/路径冲突、Save As、命名/自动槽、双进程和 Windows 导出。绕过锁的写入者只用于记录非协作威胁边界，不作为认证或防回滚证明。
  - 验收：合法长 Edge 保持一个 Edge ID 并在预算内往返，同向连续 line 只写一个 primitive；受保护句柄证明实际解析字节与 manifest 一致，超限输入在大额分配和图提交前拒绝；恢复不移动 legacy/unsafe/unknown、不提升 staging，pending intent 必须先收敛；任何 legacy occupant 冻结普通覆盖但允许 Save As，只读 legacy Load 不留下可写目标；过期 token 无副作用，Resolve 只能幂等完成、恢复候选或明确返回 `ResolutionRecoveryBlocked`；已复核发布不因 cleanup 失败被回滚。无目录元数据耐久证据时不宣称 durable，事务 backup 不冒充长期备份。

<a id="v3-save-system2.3"></a>

- [ ] **2.3 建立非阻塞、排他的保存与加载操作协议**
  - 当前问题：`SaveManager.Save/Load/SaveAutosave/DeleteSlot` 和 Timer autosave 同步执行；长图会阻塞主线程，同一保存根的请求会争用固定 staging/backup。当前 Load 只能顺序 restore，缺少 scene/saveable/participant generation、取消边界和一次不可失败的 aggregate commit；异步结果也没有完整的 `CurrentSlotID` 与 autosave busy 语义。
  - 修改：引入保存根级 `SaveOperationCoordinator`、不可变 operation state/result、进程内 async gate 和 `.save-root.lock`；所有操作路径由 `v3-save-system:2.2` 分配唯一 operation-specific 位置。Save admission 在主线程 O(1) 捕获不可变 root 与 generation，后台完成 serialize/hash/I/O，再由独立 `PublishV2` token 取得一次性 publish lease 发布。Timer busy 时至多合并一个 pending autosave，手动操作优先；场景退出停止 admission，取消未越界 worker，并等待已经越过磁盘不可取消点的事务收敛。
  - Load 协议：每次 Load 使用自己的 token 和结果，固定经过 Admission、Prepare、Preflight、Non-yield commit/notification。Admission 冻结新道路命令但逐值保留当前 graph、工具草稿、选择、hover、overlay、历史和 `CurrentSlotID`；Prepare 从受保护句柄构造完整 immutable aggregate，预建 graph root、empty tool root、纯 CLR tessellation、surface snapshot 与 hit-index 数据，后台不得访问 Godot Object；Preflight 重新验证全部 generation、容量和 token，并创建隐藏 Mesh/RID 等所有关键表现资源及不可抛 commit plan。任何 graph、tool、renderer、surface/hit index、Vulkan/Godot 资源或可写目标关键失败必须发生在 Preflight，活动状态逐值不变。
  - Commit 边界：一个不可 yield 的临界区只交换已经验证的 graph facade root/lineage、empty tool/overlay root、hidden Mesh/RID、surface snapshot、hit index、presentation token、diagnostics 和 save target/`CurrentSlotID`，随后发布一次 matching full-reset 与 `PresentationReady`。commit 后不存在关键 participant 或关键表现失败分支；只允许普通 observer 在完整状态可见后抛错并被逐个隔离，结果提升为 `SucceededWithObserverWarnings`，不得回滚或误报图未加载。正常 v2 Load 设置可写目标，legacy Load 清空目标；Resolve 不参与这次 commit，也不修改活动目标。
  - 永久分离约束：coordinator 可以统一调度，但不能合并 `PublishV2`、`ResolveLegacyState` 和 `Load` 的 token、取消边界或结果。Resolve 成功后需要新的 Load 请求；Load 成功不能暗示来源已迁移或磁盘冲突已解决；UI 只能分别呈现三个结果。接口不得增加组合成功结果，也不得增加提交后关键表现错误或针对该错误的独立恢复状态。
  - 依赖：`v3-save-system:2.2`、`v3-road-graph:8.5`；工具 full reset 属于 `v3-tool-input:2.4`，隐藏资源与 surface 一次交换属于 `v3-grid-rendering:2.2`，busy/error/result 呈现属于 `v3-ui:1.4`。
  - 集成负责人：`v3-save-system`；真实场景端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：O(1) capture、后台 Godot Object 零访问、save I/O 中继续编辑；进程内/双进程 gate/lock、Timer 合并、手动优先和退出收敛；三个事务分别产生不同 operation token/结果和各自取消边界；fake graph/tool/presentation/第二 saveable aggregate、generation 失配、每个关键 Preflight 失败、commit 不抛、首个/中间普通 observer 抛错、正常/legacy `CurrentSlotID` 结果。联合测试由 `v3-tool-input:2.4`、`v3-ui:1.4`、`v3-grid-rendering:2.2` 和 `v3-road-graph:8.6` 覆盖真实隐藏资源及一次交换。
  - 验收：同一保存根跨进程最多一个目录事务；主线程不执行长 JSON/hash/I/O；Prepare/Preflight 失败逐值保留活动状态，non-yield commit 全有或全无且只通知一次；Load 只有 `Succeeded`、`SucceededWithObserverWarnings` 或提交前失败/取消，observer warning 不回滚，接口没有提交后关键表现失败结果；autosave busy 有界。fake aggregate 验收属于本项，真实 Load 组合验收由 `v3-road-graph:8.6` 负责。

## 暂不执行

### 二进制、压缩与持久化 geometry chunk

- 延期原因：当前没有证据证明 JSON 解析 CPU 或磁盘体积是主要瓶颈；引入新容器会同时扩大 reader、迁移和调试成本。
- 保持现状：schema 2 使用内联 geometry 的 UTF-8 JSON；允许固定大小 I/O buffer、渲染批次和 query fragment，但它们没有持久领域身份，也不能制造伪 Node/Edge。
- 重新开启条件：geometry-dense 基准证明体积或 I/O 未达门槛后，先定义显式 codec、编码/解码长度、压缩比预算并提升 manifest 版本。

### 敌对写入认证、防回滚与长期介质恢复

- 延期原因：保存根锁和 SHA-256 只覆盖协作实例、意外损坏与中断恢复，不是敌对本机进程隔离、来源认证或版本新鲜度证明；事务 backup 也不是长期备份。
- 保持现状：准确声明非协作目录级 TOCTOU、无密钥 hash 和临时 backup 的边界，不把 crash recovery 写成 durable 或 bit-rot 自动恢复。
- 重新开启条件：产品明确要求敌对环境或长期代际恢复时，另行设计受保护密钥/签名、可信单调版本、目录隔离和保留代际，并提升容器协议。

### 第二个正式持久化业务系统

- 延期原因：V3 当前业务 payload 仍只有 RoadGraph；本路线图先用 fake second saveable 固化 prepared aggregate 协议。
- 保持现状：保留每系统独立 payload 扩展边界，禁止回退为逐系统可抛 `RestoreState`。
- 重新开启条件：第二个业务系统进入产品范围时，在其所属系统路线图新增工作项，并以 `v3-save-system:2.3` 的 aggregate Preflight/commit 故障注入作为集成前置条件。

## 已解决基线

- [x] **V2 schema 与 manifest 已有严格版本门禁。** `save-system:0.4` 只接受精确当前版本；V3 migration codec 必须保留这项历史边界，不能把旧字段或未来格式静默吞入 schema 2。
- [x] **V2 RoadGraph 已先完整准备再提交。** `save-system:0.5` 与 `save-system:0.11` 已验证损坏引用、几何或整槽预检失败不改变活动图；V3 将该原则提升为跨 graph/tool/renderer/slot-target 的 prepared aggregate。
- [x] **V2 已有整槽 staging/backup 发布和命名槽隔离。** `save-system:0.9`～`0.10`、`save-system:1.1`～`1.3` 已覆盖槽生命周期、显示名/内部 ID、自动槽和 Windows 可写路径；V3 必须保留这些行为，同时替换不充分的恢复判据。
- [x] **V2 保存范围已限定为 RoadGraph，同时保留扩展接口。** `save-system:1.4` 是历史产品边界；V3 的 fake aggregate 不代表第二个业务 payload 已进入存档。
- [x] **V2 道路 payload 使用 Node/Edge/Group 和六类原生几何。** `save-system:5.3` 是 schema 1 迁移的事实源；Group 与无 RoadType 是 legacy 输入事实，不是 schema 2 输出契约。

## 完成标准

1. `v3-save-system:2.1`～`2.3` 的 schema、迁移、容量、流式 I/O、恢复、并发、取消与 prepared aggregate 自动化全部通过，并保留可复现的预算及故障注入证据。
2. 严格合法 schema 1 只能由独立 legacy codec 迁移；生产 writer 在 cutover 前后分别只生成 v1/s1 与 v2/s2，schema 2 完整表达 canonical Edge、self-loop、parallel Edge、RoadType 和精确 `nextID`。
3. manifest v2、同句柄读取、五类 occupant、pending-intent-first、legacy freeze、`LegacyStateToken`、`ResolutionRecoveryBlocked` 和 `PublishedWithCleanupPending` 均通过全矩阵与中断点验证，未知/unsafe 现场不被猜测或破坏。
4. `PublishV2`、`ResolveLegacyState` 和 `Load` 在 API、operation token、取消点、结果与 UI 呈现上永久分离；Resolve 只改磁盘且不加载/不改 `CurrentSlotID`，Load 只读磁盘且只交换活动会话。
5. Load 的关键 graph/tool/表现/slot-target 失败全部发生在 Preflight；non-yield commit 一次交换全部引用，提交后只有普通 observer warning，不存在任何关键 participant 的后置失败或部分恢复状态。
6. 主线程 capture 保持 O(1)，长 JSON/hash/I/O 在后台执行；同一保存根跨进程排他，autosave busy 有界，场景退出和三个不可取消点均可收敛。
7. `v3-road-graph:8.6` 联合 `v3-grid-rendering:2.2`、`v3-tool-input:2.4` 和 `v3-ui:1.4` 在真实 `MapTest`、Windows 导出及 junction-dense/geometry-dense 场景完成最终集成；未取得目录元数据耐久证据时，任何层级都不得宣称 durable。
