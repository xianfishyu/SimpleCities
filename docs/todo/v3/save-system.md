# 第三代存档系统待办清单

> 系统 key：`v3-save-system`
> 整理日期：2026-08-20
> 证据：当前工作区 `SaveManager`、`SaveSlotStore`、RoadGraph 持久化源码与存档自动化，V2 历史路线图 `docs/todo/save-system.md`，以及 `docs/manuals/road-system-v3-gen.md` 第 10 节。
> 主导原则：V3 建立唯一的新运行时存档契约、独立保存根和 `simple-cities-v3` format v1；不读取、迁移、转换、覆盖或删除 V2 存档，也不保留旧 DTO/接口适配器。严格版本、容量、原子发布、崩溃恢复和 aggregate Load 是 V3 自身的正确性边界。

## 状态总览

<a id="v3-save-system2"></a>

| ID | 发现 | 当前状态 | 处置方式 |
|---|---|---|---|
| 2.1 | V2 schema、保存根和 DTO 无法表达 V3 canonical Edge | 已完成 | 已建立隔离的 V3 format v1，并直接拒绝所有非 V3 格式 |
| 2.2 | 流式快照、严格 manifest、PNG 展示资产与目录事务需要统一的预算和恢复边界 | 已完成 | 有界 token reader、descriptor/digest 恢复、删除 tombstone、OS 根锁与故障矩阵均已验证 |
| 2.3 | 同步入口和顺序 prepared commit 曾缺少并发及原子会话协议 | 开放（部分实现） | async coordinator、等待取消防护、四参与者 aggregate、worker-prepared 四类 surface/index/token 与 placement/拆除/改造 admission 已落地；补齐其余命令、第二 saveable 和故障矩阵 |

### 设计覆盖矩阵

| 设计范围 | 当前事实 | 关联待办 |
|---|---|---|
| V3 格式与根隔离 | 生产运行已统一使用 `user://saves-v3`；manifest/payload 均为严格 `simple-cities-v3` format v1，RoadGraph reader/writer 可表达 canonical self-loop、parallel Edge、四类 `roadType` 和完整原生几何锚；V2/未知目录在 V3 根只分类为 `Foreign` | `v3-save-system:2.1`、`v3-road-graph:8.0`～`8.5` |
| 长连续 Edge | `IStreamingSaveable` 以 O(1) 捕获 immutable root；writer 直接输出无缩进 UTF-8，`V3JsonStreamReader` 在固定 buffer 上执行 byte/token/depth/lexeme 与 RoadGraph 容量预算，同一 payload 句柄完成长度/SHA-256/EOF 终检 | `v3-save-system:2.2`、`v3-road-graph:8.0`、`v3-road-graph:8.5` |
| 发布、恢复与删除 | operation-specific publish/delete descriptor 绑定 digest 与固定路径；五类 occupant、quarantine、删除 tombstone、跨进程 OS 根锁及 cleanup-pending/typed recovery-blocked 路径均已实现 | `v3-save-system:2.2` |
| Load 生命周期 | `SaveManager` 已把 RoadGraph root、空工具/history、基础 `ArrayMesh`/`MultiMesh`、带 canonical `RoadLocation` 和不可变 AABB 索引的同源 `EdgeRibbon` + `TerminalCap` + `SemanticJoin` + `JunctionPatch` `RoadSurfaceSnapshot`、matching 六分量 `RoadRenderToken` 与 `CurrentSlotID` 纳入一次 `PreparedAggregateLoad` 引用交换；surface triangle/disc 复制、Junction Patch 细分与建树在 worker Prepare 完成，placement、拆除与改造会话已按同一 token 边界失效 | `v3-save-system:2.3`、`v3-road-graph:8.5`、`v3-grid-rendering:2.2`、`v3-tool-input:2.4`、`v3-ui:1.4` |
| 操作权限 | `SaveManager` 已只公开 `StartSave/StartSaveAs/StartLoad/StartDeleteSlot/StartAutosave` token 入口，并发布结构化 phase/result；coordinator 实现进程内 gate、手动优先、pending autosave 合并、取消与退出收敛 | `v3-save-system:2.3`、`v3-ui:1.4` |

## 执行顺序

### 阶段 2：第三代道路 payload、容器与操作协议

<a id="v3-save-system2.1"></a>

- [x] **2.1 建立隔离的 V3 format v1 并删除旧格式入口**
  - 完成前问题：过渡期 `schemaVersion = 3` 没有 format family、独立根和 self-loop reader；编辑器/导出根分叉，旧 `ISaveable`/DTO/恢复入口仍可能形成兼容路径。
  - 已实现：生产环境统一使用 `user://saves-v3`，自动化可注入隔离根；manifest 与 RoadGraph payload 都严格写入 `formatFamily: "simple-cities-v3"` 和整数 `schemaVersion: 1`，payload 另要求 `payloadType: "road-network"`。`IStreamingSaveable` 从不可变 `RoadGraphRevision` 写无缩进 UTF-8；reader 只接受 canonical Node/Edge、四类 `roadType`、六类原生 geometry、正长度 self-loop、非覆盖 parallel Edge 和合法 `nextID`，成功 Load 创建新 runtime lineage。旧 `ISaveable`、旧 RoadGraph DTO/reader、Group 字段及兼容恢复入口均已删除。
  - 隔离边界：V3 的 List/Save/Save As/Load/Delete/autosave/恢复/启动清理只能从已验证的 V3 root capability 派生路径；不得枚举、打开、哈希、移动、转换、覆盖或删除 `res://saves`、`user://saves`。V2 槽不出现在 V3 UI；把 V2/未知目录手工复制到 V3 根只得到 `Foreign` 分类。
  - 依赖：`v3-road-graph:8.0`～`8.5`；V2 `save-system:0.4`、`0.5`、`5.3` 只作为历史事实，不是代码或格式依赖。
  - 集成负责人：`v3-save-system`；最终端到端判定由 `v3-road-graph:8.6` 负责。
  - 验证证据（2026-08-14）：V3 persistence 聚焦组、slot/manifest 契约及完整 solution 自动化最终为 637/637；开放 Edge、rooted self-loop、parallel Edge、full-turn、四种 RoadType、六类 geometry、watermark、确定往返和新 lineage 均通过。family/version/schema token、Group/groupID、未知/重复字段、非规范拓扑、内部交叉、损坏引用、长度/hash 不符均在提交前拒绝；V2 根 canary 和源码扫描证明 V3 操作不读取、改写或删除 V2 数据。
  - 运行证据（2026-08-14）：真实 `MapTest` 完成 V3 Save、修改、Load 恢复，Load 后 history 为 undo/redo `0/0`；命名槽、autosave、暂停菜单、曲线和最终组合契约均使用 V3 payload 并清理测试槽。该切片最初只有 Windows Debug QA 包的非 editor V3 根、manifest family/schema/长度/hash 和删除证据；同日后续 `v3-save-system:2.2` 已补齐 Windows Desktop QA 导出包的可写与只读 ACL 契约，原 Release 导出缺口不再开放。
  - 验收结果：只有精确 V3 format v1 能进入 prepared state；合法图逐值保留 incidence、rooted seam、parallel Edge、原生 geometry、RoadType 和 watermark，非法输入不改变活动图。生产运行只写独立 V3 根，V2/未知目录复制到该根只得到 `Foreign`，不触发迁移或兼容加载。

<a id="v3-save-system2.2"></a>

- [x] **2.2 为长连续 Edge 建立确定、有界且可恢复的存储管线**
  - 已实现：format v1 继续使用 geometry 内联 Edge 的无 BOM、无缩进 canonical UTF-8 JSON，不引入拓扑 chunk。writer 从不可变 `RoadGraphRevision` 直接写 operation-specific staging；duplicate-aware `V3JsonStreamReader` 从同一个受保护句柄执行属性原始字节、number lexeme、initial/declared/consumed length、EOF 与 SHA-256 校验，并在集合分配和图提交前执行 manifest、payload、整槽、token、深度、字符串、实体、geometry、坐标、长度、ID 与索引容量预算。thumbnail 作为独立 PNG 展示资产校验 signature、chunk/CRC、尺寸、解码扫描线和资源预算，缺失或损坏只产生占位 warning。
  - 发布与恢复：`SaveSlotStore` 在整个同步存储操作期间持有跨进程 `.save-root.lock`，并为每个请求使用 `.save-transactions/<slot>/<operation-id>/`。完整 staging 复核后先持久发布不可变 `publish.json`，绑定 slot、新旧 aggregate digest、固定 staging/backup 路径和 operation token，再允许 canonical move。五类 occupant、无 descriptor quarantine、new/old digest 恢复矩阵、typed `SavePublicationRecoveryException` 阻塞、`PublishedWithCleanupPending` 和旧槽回退均已实现；没有按时间戳猜测赢家。进程内 async gate、publish lease 和公开 operation state 仍严格属于 `v3-save-system:2.3`。
  - 删除：删除授权绑定明确目标、UI generation、operation token、occupant kind/digest 和确认摘要；`delete.json` 在 `slot -> tombstone` 前发布。越界后槽在逻辑上已删除，清理失败返回 `DeletedWithCleanupPending` 且恢复继续删除；canonical/tombstone 歧义通过 typed `SaveDeletionRecoveryException` 保留现场。`Foreign` / `Unsafe` 不能取得删除授权，`CorruptV3` 只能经当前列表 generation 的明确确认删除。
  - 依赖：`v3-road-graph:8.0`、`v3-road-graph:8.5`、`v3-save-system:2.1`；预算使用 Phase 0 的 junction-dense/geometry-dense 数据，V2 性能记录只作同机比较。
  - 集成负责人：`v3-save-system`；编辑历史内存属于 `v3-tool-input:2.3`，最终完成判定由 `v3-road-graph:8.6` 负责。
  - 验证证据（2026-08-14）：保存/manifest/PNG/persistence/export 聚焦组 118/118，`SaveManagerSlotContractTests` 45/45，完整 solution 698/698；Debug 与 `ExportRelease` build 均为 0 警告、0 错误。故障矩阵覆盖首次/覆盖 publish 的未越界、旧槽已移至 backup、canonical 已发布、cleanup pending 与 digest 歧义，delete descriptor 前后、tombstone cleanup 与后来替换槽冲突，无 descriptor staging、部分写入后 `ERROR_DISK_FULL`/ENOSPC、`.save-transactions` 文件占位、同进程/独立进程根锁、Windows 只读 ACL 和 V2 根逐字节/时间戳 canary。Windows QA 导出包在显示驱动下通过可写与只读 profile 契约，包内测试资源只保留 exported-save 契约及其 fixture。
  - 验收结果：合法长 Edge 保持一个 Edge ID 并在预算内往返，同向连续 line 只写一个 primitive；超限输入在大额业务分配和图提交前拒绝；恢复只处理 descriptor 明确拥有且 digest 可证明的路径，歧义现场完整保留；已复核发布或已越界删除不因 cleanup 失败被误报或反向恢复。当前只声明进程失败原子且 crash recoverable，不声明目录元数据 sudden-power-loss durable，事务 backup 也不冒充长期备份。

<a id="v3-save-system2.3"></a>

- [ ] **2.3 建立非阻塞、排他的保存、加载与删除协议**
  - 完成前问题：`SaveManager.Save/Load/SaveAutosave/ConfirmDeleteSlot` 和 Timer autosave 是同步 bool API；长图的 JSON/hash/I/O 及 prepared graph 构建发生在调用线程，且场景/应用退出没有收敛边界。底层 operation-specific 事务和跨进程 OS 根锁已经由 `2.2` 完成，但当时没有进程内 async coordinator、scene/participant generation、取消边界、publish lease、公开结构化 operation state 或 aggregate commit。
  - 修改：以新接口实现保存根级 `SaveOperationCoordinator`、不可变 operation state/result、进程内 async gate 和跨进程 lock；不保留同步 bool API 适配器。Save admission 在主线程 O(1) 捕获 immutable root 与 generation，后台 serialize/hash/I/O，再由 `PublishV3` token 取得一次性 publish lease。Load 和 Delete 各有独立 token、取消点与结果；Timer busy 时至多合并一个 pending autosave，手动操作优先；场景退出停止 admission、取消未越界 worker并等待已越界事务收敛。
  - Load 协议：每次 Load 固定经过 Admission、Prepare、Preflight、Non-yield commit/notification。Admission 冻结新道路命令但逐值保留 graph、草稿、选择、hover、overlay、历史和 `CurrentSlotID`；Prepare 从受保护句柄构造完整 immutable aggregate，预建 graph root、empty tool root、纯 CLR tessellation、surface snapshot 与 hit-index 数据；Preflight 重新验证全部 generation/capacity/token 并创建隐藏 Mesh/RID 和不可抛 commit plan。任何关键失败都发生在 Preflight，活动状态逐值不变。
  - Commit 边界：不可 yield 临界区只交换已验证的 graph root/lineage、empty tool/overlay root、hidden Mesh/RID、surface snapshot、hit index、presentation token、diagnostics 和 `CurrentSlotID`，随后发布一次 matching full reset 与 `PresentationReady`。普通 observer 异常逐个隔离，结果提升为 `SucceededWithObserverWarnings`；不存在提交后关键 participant 失败或恢复页。Publish、Load、Delete 的 token、取消边界、结果和 UI 阶段不得合并。
  - 依赖：`v3-save-system:2.2`、`v3-road-graph:8.5`；工具 full reset 属于 `v3-tool-input:2.4`，隐藏资源与 surface 一次交换属于 `v3-grid-rendering:2.2`，busy/error/result 呈现属于 `v3-ui:1.4`。
  - 集成负责人：`v3-save-system`；真实场景端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：O(1) capture、后台 Godot Object 零访问、save I/O 中继续编辑；进程内/双进程 gate/lock、Timer 合并、手动优先和退出收敛；Publish/Load/Delete 分别产生唯一 token/result；fake graph/tool/presentation/第二 saveable aggregate、generation 失配、每个关键 Preflight 失败、commit 不抛、普通 observer 抛错、删除越界前后取消和 `CurrentSlotID` 结果。联合测试由协作系统覆盖真实隐藏资源及一次交换。
  - 验收：同一 V3 根跨进程最多一个目录事务；主线程不执行长 JSON/hash/I/O；Prepare/Preflight 失败逐值保留活动状态，non-yield commit 全有或全无且只通知一次；Load 只有成功、observer warning 或提交前失败/取消；autosave busy 有界；任何操作都不调用 V2 API 或触碰 V2 根。
  - 阶段进展（2026-08-14）：`SaveOperationCoordinator` 已实现进程内根 gate、结构化 token/state/result、手动请求优先、单 pending autosave、取消点和 shutdown；公开同步 bool 入口已删除。Save/Load/Delete 的长磁盘与 prepared 工作在 `Task.Run` 中执行，主线程 capture 只取得 immutable root。`SaveManager` 用 scene generation 跟踪任务，返回主菜单先 drain，窗口/菜单/主菜单退出统一等待未越界取消或已越界事务收敛；新请求在关闭期得到 typed rejection。
  - 等待取消修复（2026-08-14）：手动请求从 `_rootGate.WaitAsync()` 返回后、创建 `SaveOperationLease` 前，会在 coordinator 锁内重新检查外部 cancellation token；若取消与 gate 释放竞争，则立即释放已取得的 gate 并返回 Admission 阶段 `Canceled`。被取消的 waiter 不再成为无人终结的活动 lease，也不会让 scene drain 或 coordinator dispose 无限等待；见 `save-system:BUG-13`。
  - Aggregate 进展（2026-08-15）：Load 已按 Admission/Prepare/Preflight/Commit 运行，`PreparedAggregateLoad` 在同一 commit lease 中交换 RoadGraph 新 lineage、RoadBuilder 空 placement/removal/upgrade 与新 history、RoadRenderer 预建的基础 `ArrayMesh`/`MultiMesh`、含 `EdgeRibbon` + `TerminalCap` + `SemanticJoin` + `JunctionPatch` 的 `RoadSurfaceSnapshot`、matching desired/presented `RoadRenderToken`，以及 `CurrentSlotID`；明确的 `CurrentTool` 与 `SelectedRoadType` 保留。`SaveManager` 在 scene registration 时把 `SceneGeneration` 注入 renderer。render request/facade generation 在 admission 预留，generation 失配会在 commit 前拒绝；observer 逐个隔离为 warning。`RoadRendererLoadPreparer.Prepare()` 在 worker 中完成 ribbon/join/patch triangle、terminal disc、Junction Patch 的固定量化 Clipper2 细分、defensive copy 与统一 AABB 层级；Preflight 只用 reserved token 绑定 prepared 数据，不重复细分、复制或建树。
  - 仍缺（保持开放）：当前 renderer participant 已覆盖分级 open/closed ribbon、degree-1 terminal cap、degree-2 semantic join、degree≥3 junction patch、节点批次、带 canonical `RoadLocation` 和基础空间索引的同代 surface 与 matching 六分量 token；placement、拆除与 RoadUpgrade 会话均已消费并校验 provider token，full reset 保留 `CurrentTool` 与 `SelectedRoadType`。还需协同 `v3-grid-rendering:2.2`、`v3-tool-input:2.4` 完成排队 continuation、其余命令 admission、第二 saveable 和关键资源逐点故障矩阵，不能把当前 aggregate 或三类道路会话切片视为 `2.3` 完成。
  - 当前证据（2026-08-15）：完整 `dotnet test SimpleCities.sln` 为 833/833，RoadUpgrade 聚焦组合为 17/17，类型化建造聚焦组合为 60/60，拆除/history/surface/renderer 聚焦组合为 51/51；Debug 与 `ExportRelease` build 均为 0 错误，各有 1 条既有 `NU1900`（当前环境无法访问 NuGet 漏洞数据源），Roslyn compiler/analyzer 与 GDScript workspace scan 均为 0 diagnostics。`RoadJunctionTessellatorTests`、`RoadRendererLoadPrepareTests` 和生命周期契约证明 worker prepared Junction Patch 可与 ribbon/cap/join 一起绑定 matching token；既有 coordinator、pause menu、renderer lifecycle、closed ribbon 与 V3 综合运行时契约继续覆盖提交前失败和 aggregate。隔离的 `road_render_token_runtime_contract.gd` 与 `road_input_strategy_runtime_contract.gd` 均 PASS，后者验证四类建造、RoadUpgrade 目标冻结/选择/NoChanges/token 门禁，以及 active upgrade 上的 full-reset Load：`CurrentTool = RoadUpgrade`、`SelectedRoadType = Arterial`，session/preview/history 全空且 matching token ready。既有 current/旧 surface 的拆除与改造门禁及 pending 一次发布证据继续有效；它们不改变 Load 必须在 Preflight 失败、且没有提交后表现重试的边界。测试槽、动态探针、临时用户目录和日志均已清理，editor 与 DAP 错误通道为空；本轮未重跑 10k/100k。
  - Placement 协作证据（2026-08-20）：placement 在开始时冻结 matching presented token，所有后续草稿/确认入口复核 provider 与 graph 同代状态；Load admission 期间拒绝命令但逐值保留旧会话，成功 full-reset commit 才清空 session、preview 与冻结 token。完整自动化 840/840、双配置 build 0 警告/0 错误，两项隔离道路运行时契约均 PASS。当前会话未暴露 Roslyn/Godot MCP 与 DAP，未刷新对应通道；该证据只完成工具协作切片，不替代第二 saveable 或关键资源故障矩阵。

## 暂不执行

### 二进制、压缩与持久化 geometry chunk

- 延期原因：当前没有证据证明 JSON 解析 CPU 或磁盘体积是主要瓶颈；引入新容器会扩大 reader 和调试成本。
- 保持现状：V3 format v1 使用内联 geometry 的 UTF-8 JSON；I/O buffer、渲染批次和 query fragment 没有持久领域身份，也不能制造伪 Node/Edge。
- 重新开启条件：geometry-dense 基准证明体积或 I/O 未达门槛后，先定义显式 codec、编码/解码长度和压缩比预算，并提升 V3 manifest 版本。

### 敌对写入认证、防回滚与长期介质恢复

- 延期原因：保存根锁和 SHA-256 只覆盖协作实例、意外损坏与中断恢复，不是敌对本机进程隔离、来源认证或版本新鲜度证明；事务 backup 也不是长期备份。
- 保持现状：准确声明目录级 TOCTOU、无密钥 hash 和临时 backup 的边界，不把 crash recovery 写成 durable 或 bit-rot 自动恢复。
- 重新开启条件：产品明确要求敌对环境或长期代际恢复时，另行设计受保护密钥/签名、可信单调版本、目录隔离和保留代际，并提升容器协议。

### 第二个正式持久化业务系统

- 延期原因：V3 当前业务 payload 仍只有 RoadGraph；本路线图先用 fake second saveable 固化 prepared aggregate 协议。
- 保持现状：保留每系统独立 payload 扩展边界，禁止回退为逐系统可抛 `RestoreState`。
- 重新开启条件：第二个业务系统进入产品范围时，在其所属系统路线图新增工作项，并以 `v3-save-system:2.3` 的 aggregate Preflight/commit 故障注入作为集成前置条件。

## 已解决基线

- [x] **V2 已证明严格版本门禁是必要的。** `save-system:0.4` 的历史证据用于要求 V3 精确校验 family/version；V3 不复用 V2 reader 或版本号语义。
- [x] **V2 RoadGraph 已先完整准备再提交。** `save-system:0.5` 与 `save-system:0.11` 的失败保护是行为基线；V3 以新接口扩展为 graph/tool/renderer/slot-target prepared aggregate。
- [x] **V2 已验证命名槽、自动槽和 staging/backup 的用户语义。** V3 重新实现这些能力，并用 descriptor、独立根和跨进程协调替代旧内部架构。
- [x] **V2 保存范围已限定为 RoadGraph。** `save-system:1.4` 是历史产品边界；V3 的 fake aggregate 不代表第二个业务 payload 已进入存档。
- [x] **V2/过渡期 payload 与保存根只作历史证据。** 历史 Node/Edge/Group、当前 Group-free 且含 `roadType` 的严格 `schemaVersion = 3`、`res://saves` 和 `user://saves` 均不是 V3 format v1 输入或实现依赖。
- [x] **V3 format v1 与独立根已成为唯一生产存档入口。** `IStreamingSaveable`、严格 manifest/payload family、`user://saves-v3`、canonical RoadGraph reader/writer、新 lineage Load 和 V2 根隔离均已有自动化、真实 `MapTest` 与 Debug 导出运行证据；后续 2.2/2.3 只能扩展有界 I/O、恢复与并发协议，不能恢复旧格式或同步兼容接口。
- [x] **V3 同步存储原语已经确定、有界且可恢复。** `V3JsonStreamReader`、同句柄 length/hash/EOF、PNG 展示资产预算、operation-specific publish/delete descriptor、五类 occupant、OS 根锁、quarantine/tombstone 与 digest 恢复矩阵已有自动化和 Windows 导出/ACL 证据；`2.3` 只能在其上增加异步 coordinator、publish lease 和 aggregate Load，不能绕过这些磁盘不变量。

## 完成标准

1. `v3-save-system:2.1`～`2.3` 的新格式、根隔离、容量、流式 I/O、发布/删除恢复、并发、取消与 prepared aggregate 自动化全部通过。
2. V3 writer 只生成 `simple-cities-v3` format v1；reader 只接受精确 family/version，并完整表达 canonical Edge、self-loop、parallel Edge、RoadType 和 `nextID`。
3. 五类 occupant、publish/delete descriptor、删除 tombstone、`PublicationRecoveryBlocked`、`DeletionRecoveryBlocked`、`PublishedWithCleanupPending` 和 `DeletedWithCleanupPending` 通过全矩阵与中断点验证。
4. V2 两个保存根在 V3 List/Save/Save As/Load/Delete/autosave/恢复/启动清理中均零访问；不存在迁移、导入、只读加载、兼容 DTO 或双写入口。
5. Publish、Load 与 Delete 在 API、operation token、取消点、结果和 UI 呈现上分离；Load 的关键失败全部发生在 Preflight，commit 一次交换全部引用。
6. 主线程 capture 保持 O(1)，长 JSON/hash/I/O 在后台执行；同一 V3 根跨进程排他，autosave busy 有界，场景退出和各不可取消点均可收敛。
7. `v3-road-graph:8.6` 联合 `v3-grid-rendering:2.2`、`v3-tool-input:2.4` 和 `v3-ui:1.4` 在真实 `MapTest`、Windows 导出及 junction-dense/geometry-dense 场景完成最终集成；未取得目录元数据耐久证据时不得宣称 durable。
