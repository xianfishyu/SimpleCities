# 第三代存档系统待办清单

> 系统 key：`v3-save-system`
> 整理日期：2026-08-13
> 证据：当前工作区 `SaveManager`、`SaveSlotStore`、RoadGraph 持久化源码与存档自动化，V2 历史路线图 `docs/todo/save-system.md`，以及 `docs/manuals/road-system-v3-gen.md` 第 10 节。
> 主导原则：V3 建立唯一的新运行时存档契约、独立保存根和 `simple-cities-v3` format v1；不读取、迁移、转换、覆盖或删除 V2 存档，也不保留旧 DTO/接口适配器。严格版本、容量、原子发布、崩溃恢复和 aggregate Load 是 V3 自身的正确性边界。

## 状态总览

<a id="v3-save-system2"></a>

| ID | 发现 | 当前状态 | 处置方式 |
|---|---|---|---|
| 2.1 | 当前 V2 schema、保存根和 DTO 无法表达 V3 canonical Edge | 开放 | 建立隔离的 V3 format v1，并直接拒绝所有非 V3 格式 |
| 2.2 | 完整 DTO/字符串、无容量上限和弱恢复判据不能可靠承载长 geometry | 开放 | 建立确定流式容器、同句柄校验、publish descriptor 与恢复矩阵 |
| 2.3 | 同步 save/load/delete/autosave 和顺序 Restore 缺少并发及原子会话协议 | 开放 | 建立保存根 coordinator、prepared aggregate 与一次 non-yield Load commit |

### 设计覆盖矩阵

| 设计范围 | 当前事实 | 关联待办 |
|---|---|---|
| V3 格式与根隔离 | 当前编辑器写 `res://saves`、导出写 `user://saves`，payload 是 V2 Node/Edge/Group | `v3-save-system:2.1`、`v3-road-graph:8.0`～`8.5` |
| 长连续 Edge | 当前 capture、缩进 UTF-16 字符串和重复完整解析放大峰值；读取前没有分层预算 | `v3-save-system:2.2`、`v3-road-graph:8.0`、`v3-road-graph:8.5` |
| 发布、恢复与删除 | manifest 只列文件名；slot/backup 并存时不能证明候选完整，删除也没有独立恢复边界 | `v3-save-system:2.2` |
| Load 生命周期 | 当前逐个 restore 不能保证 graph、tool、mesh、surface、hit index 和当前槽全有或全无 | `v3-save-system:2.3`、`v3-road-graph:8.5`、`v3-grid-rendering:2.2`、`v3-tool-input:2.4`、`v3-ui:1.4` |
| 操作权限 | Publish、Load 与 Delete 若共享 bool 结果或 continuation，会混淆磁盘和活动会话是否已提交 | `v3-save-system:2.2`～`2.3`、`v3-ui:1.4` |

## V3 实施记录

> 本段记录已落地且经过验证的 V3 存档模块；完整工作项仍以「状态总览」和「执行顺序」为准。

### 2026-08-13：2.1 V3 槽 ID 校验（部分）

- 新增 `Scripts/Core/V3/V3SlotId.cs`：校验 1～128 个 `[A-Za-z0-9_-]` ASCII 字符，作为 V3 槽 ID 与目录名校验基础。
- 新增 10 个 xUnit 用例；完整测试套件 668/668 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：V3 保存根常量/隔离、manifest v1、format v1 严格 reader 与保存根 coordinator。

### 2026-08-13：2.1 V3 保存根常量（部分）

- 新增 `Scripts/Core/V3/V3SaveRoot.cs`：编辑器/导出统一 `user://saves-v3`，声明 `simple-cities-v3` / schemaVersion 1，并提供 V2 根识别。
- 新增 4 个 xUnit 用例；完整测试套件 672/672 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：根隔离操作、manifest v1、严格 reader 与保存根 coordinator。

### 2026-08-13：2.2 manifest v1 基础校验（部分）

- 新增 `Scripts/Core/V3/V3Manifest.cs`：`V3Manifest` / `V3ManifestFile` / `V3ManifestValidator`，校验 family/version、槽 ID、文本字段、UTC timestamp、population/funds、缩略图与文件 metadata（sha256/长度/名称）。
- 新增 8 个 xUnit 用例；完整测试套件 680/680 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：manifest JSON 编解码、文件集合与 payload digest 绑定、严格 reader 与保存根 coordinator。

### 2026-08-13：2.2 manifest codec（部分）

- 新增 `Scripts/Core/V3/V3ManifestCodec.cs`：camelCase JSON 序列化/反序列化，并在反序列化后执行 `V3ManifestValidator`。
- 新增 4 个 xUnit 用例；完整测试套件 684/684 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：文件集合与 payload digest 绑定、严格 reader 与保存根 coordinator。

### 2026-08-13：2.1 V3 槽路径派生（部分）

- 新增 `Scripts/Core/V3/V3SlotPath.cs`：从已验证 V3 root 派生槽路径，槽 ID 不合法或 root 为空时拒绝，避免路径逃逸。
- 新增 4 个 xUnit 用例；完整测试套件 688/688 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：目录分类、manifest/payload digest 绑定、严格 reader 与保存根 coordinator。

### 2026-08-13：2.2 occupant 分类基础（部分）

- 新增 `Scripts/Core/V3/V3SlotOccupant.cs`：`V3SlotOccupant` 枚举与 `V3SlotClassifier`，按目录/manifest 声明/校验/payload 校验分类为 `CompleteV3`、`CorruptV3`、`Foreign`、`Unsafe`。
- 新增 5 个 xUnit 用例；完整测试套件 693/693 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实文件系统分类、manifest/payload digest 绑定、严格 reader 与保存根 coordinator。

### 2026-08-13：2.2 payload digest（部分）

- 新增 `Scripts/Core/V3/V3PayloadDigest.cs`：计算 SHA-256 小写 hex，并校验 manifest 文件项与字节内容（长度 + hash）。
- 新增 4 个 xUnit 用例；完整测试套件 697/697 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：同句柄读取/计数/hash/EOF、严格 reader 与保存根 coordinator。

### 2026-08-13：2.3 operation token/result 类型（部分）

- 新增 `Scripts/Core/V3/V3SaveOperation.cs`：`V3SaveOperationKind` / `Phase` / `Token` / `Result`，区分 Publish/Load/Delete，提供成功、提交前失败、observer warning 结果。
- 新增 4 个 xUnit 用例；完整测试套件 701/701 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：coordinator、gate/lock、取消边界与 prepared aggregate 协议。

### 2026-08-13：2.2 JSON lexeme 基础校验（部分）

- 新增 `Scripts/Core/V3/V3JsonLexeme.cs`：canonical integer token（无正号/前导零/小数点/指数/`-0`）与有限 binary32 float token 校验。
- 新增 20 个 xUnit 用例；完整测试套件 721/721 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：duplicate-aware token reader、同句柄 length/hash/EOF、严格 format v1 reader。

### 2026-08-13：2.2 payload 分层预算（部分）

- 新增 `Scripts/Core/V3/V3PayloadBudget.cs`：manifest/payload/整槽字节、实体/geometry 数量、JSON 深度与字符串长度预算。
- 新增 3 个 xUnit 用例；完整测试套件 724/724 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：duplicate-aware token reader、同句柄 length/hash/EOF、严格 format v1 reader。

### 2026-08-13：2.3 autosave 合并策略（部分）

- 新增 `Scripts/Core/V3/V3AutosavePolicy.cs`：忙时至多一个 pending，手动/更晚成功后可丢弃，返回 RunNow/QueuePending/SkipBusy。
- 新增 4 个 xUnit 用例；完整测试套件 728/728 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：coordinator/gate/lock、取消边界与 prepared aggregate 协议。

### 2026-08-13：2.2 事务路径派生（部分）

- 新增 `Scripts/Core/V3/V3TransactionPath.cs`：从已验证 root 派生 operation-specific transaction 目录及 staging/backup/publish.json 路径，拒绝非法 slot/operation。
- 新增 4 个 xUnit 用例；完整测试套件 732/732 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：publish descriptor 内容/恢复矩阵、同句柄校验与 coordinator。

### 2026-08-13：2.2 publish descriptor（部分）

- 新增 `Scripts/Core/V3/V3PublishDescriptor.cs`：不可变 descriptor 记录（operation/slot/new/old digest/staging/backup）与基础校验。
- 新增 3 个 xUnit 用例；完整测试套件 735/735 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：恢复矩阵、descriptor JSON 编解码、同句柄校验与 coordinator。

### 2026-08-13：2.2 publication 恢复矩阵（部分）

- 新增 `Scripts/Core/V3/V3PublicationRecovery.cs`：按 slot/backup/staging digest 匹配返回 `PublishComplete`、`PreserveOldIsolateStaging`、`CompleteStagingToSlot`、`RestoreOldFromBackup` 或 `Blocked`。
- 新增 5 个 xUnit 用例；完整测试套件 740/740 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：descriptor JSON 编解码、同句柄校验与 coordinator。

### 2026-08-13：2.2 publish descriptor codec（部分）

- 新增 `Scripts/Core/V3/V3PublishDescriptorCodec.cs`：camelCase JSON 序列化/反序列化 publish descriptor，并在反序列化后执行基础校验。
- 新增 4 个 xUnit 用例；完整测试套件 744/744 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：同句柄校验、严格 format v1 reader 与 coordinator。

### 2026-08-13：2.2 文件集合校验（部分）

- 新增 `Scripts/Core/V3/V3FileSetValidator.cs`：比较 manifest 声明 payload 与实际文件集合，缺失或未声明文件均失败。
- 新增 3 个 xUnit 用例；完整测试套件 747/747 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：同句柄 length/hash/EOF、严格 format v1 reader 与 coordinator。

### 2026-08-13：2.2 槽聚合验证（部分）

- 新增 `Scripts/Core/V3/V3SlotVerifier.cs`：manifest 校验 + 文件集合匹配 + 每个 payload length/hash 校验的聚合验证。
- 新增 4 个 xUnit 用例；完整测试套件 751/751 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：同句柄读取/计数/hash/EOF、严格 format v1 reader 与 coordinator。

### 2026-08-13：2.2 delete descriptor（部分）

- 新增 `Scripts/Core/V3/V3DeleteDescriptor.cs`：不可变删除 descriptor（operation/slot/digest/tombstone/confirmation）与基础校验。
- 新增 3 个 xUnit 用例；完整测试套件 754/754 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：delete 恢复/清理矩阵、同句柄校验与 coordinator。

### 2026-08-13：2.2 deletion 恢复决策（部分）

- 新增 `Scripts/Core/V3/V3DeletionRecovery.cs`：槽仍存在返回 `NotDeleted`，槽缺失且 tombstone 匹配返回 `ContinueCleanup`，否则 `Blocked`。
- 新增 3 个 xUnit 用例；完整测试套件 757/757 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：同句柄 length/hash/EOF、严格 format v1 reader 与 coordinator。

### 2026-08-13：2.3 coordinator gate（部分）

- 新增 `Scripts/Core/V3/V3CoordinatorGate.cs`：进程内保存根 gate，同一时间只允许一个目录事务，支持 autosave pending 标记。
- 新增 4 个 xUnit 用例；完整测试套件 761/761 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：跨进程 lock、取消边界、prepared aggregate 与真实 coordinator。

### 2026-08-13：2.3 prepared aggregate 摘要（部分）

- 新增 `Scripts/Core/V3/V3PreparedAggregate.cs`：记录 required/prepared participants 与 warnings，`AllPrepared`/`CanCommit` 决定是否可进入 non-yield commit。
- 新增 3 个 xUnit 用例；完整测试套件 764/764 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：跨进程 lock、取消边界、真实 coordinator 与完整 Load commit 协议。

### 2026-08-13：2.3 Load 四阶段状态机（部分）

- 新增 `Scripts/Core/V3/V3LoadProtocol.cs`：Admission -> Prepare -> Preflight -> Commit -> Completed 状态机，禁止跳阶段，失败进入 Failed。
- 新增 4 个 xUnit 用例；完整测试套件 768/768 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：跨进程 lock、取消边界、真实 coordinator 与完整 Load commit 协议。

### 2026-08-13：2.3 根锁路径（部分）

- 新增 `Scripts/Core/V3/V3SaveRootLock.cs`：返回 `<root>/.save-root.lock` 路径，供跨进程排他目录事务使用。
- 新增 3 个 xUnit 用例；完整测试套件 771/771 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：跨进程 lock 实现、取消边界、真实 coordinator 与完整 Load commit 协议。

### 2026-08-13：2.1 槽列表摘要（部分）

- 新增 `Scripts/Core/V3/V3SlotSummary.cs`：槽列表项摘要（slot/display/occupant/timestamp），`IsUsable` 仅对 `CompleteV3` 为 true。
- 新增 3 个 xUnit 用例；完整测试套件 774/774 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实目录枚举、跨进程 lock 实现、真实 coordinator 与完整 Load commit 协议。

### 2026-08-13：2.2 manifest 构造（部分）

- 新增 `Scripts/Core/V3/V3ManifestBuilder.cs`：从槽元数据与 payload 字节构造 manifest v1，自动计算 encodedLength/SHA-256 并按文件名排序。
- 新增 3 个 xUnit 用例；完整测试套件 777/777 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实目录枚举、跨进程 lock 实现、真实 coordinator 与完整 Load commit 协议。

### 2026-08-13：2.2 缩略图校验（部分）

- 新增 `Scripts/Core/V3/V3ThumbnailValidator.cs`：PNG signature 与像素预算校验。
- 新增 4 个 xUnit 用例；完整测试套件 781/781 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实目录枚举、跨进程 lock 实现、真实 coordinator 与完整 Load commit 协议。

### 2026-08-13：2.1 槽列表生成（部分）

- 新增 `Scripts/Core/V3/V3SlotLister.cs`：将已分类 direct child 集合转换为有序 `V3SlotSummary` 列表。
- 新增 2 个 xUnit 用例；完整测试套件 783/783 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实目录枚举、跨进程 lock 实现、真实 coordinator 与完整 Load commit 协议。

### 2026-08-13：2.2 槽文件组装（部分）

- 新增 `Scripts/Core/V3/V3SlotWriter.cs`：将 manifest 与 payload 字节组装为槽内文件字典（manifest.json + payloads），缺失/未声明 payload 拒绝。
- 新增 3 个 xUnit 用例；完整测试套件 786/786 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实目录枚举、跨进程 lock 实现、真实 coordinator 与完整 Load commit 协议。

### 2026-08-13：2.2 槽文件读取（部分）

- 新增 `Scripts/Core/V3/V3SlotReader.cs`：从槽文件字典读取 manifest 与 payload（排除 manifest.json），缺失/非法 manifest 失败。
- 新增 3 个 xUnit 用例；完整测试套件 789/789 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实目录枚举、跨进程 lock 实现、真实 coordinator 与完整 Load commit 协议。

### 2026-08-13：2.1/2.2 内存槽存储（部分）

- 新增 `Scripts/Core/V3/V3SlotStore.cs`：内存版槽存储，使用槽文件字典模拟 Save/Load/Delete/List，便于无文件系统环境验证协议。
- 新增 4 个 xUnit 用例；完整测试套件 793/793 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实目录枚举、跨进程 lock 实现、真实 coordinator 与完整 Load commit 协议。

### 2026-08-13：2.3 内存保存协调器（部分）

- 新增 `Scripts/Core/V3/V3SaveCoordinator.cs`：组合内存槽存储与进程内 gate，Save/Load/Delete 排他执行并返回 operation result。
- 新增 3 个 xUnit 用例；完整测试套件 796/796 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实目录枚举、跨进程 lock 实现、真实 coordinator 与完整 Load commit 协议。

### 2026-08-13：2.2 同句柄校验模拟（部分）

- 新增 `Scripts/Core/V3/V3SameHandleVerifier.cs`：模拟同句柄校验，长度、hash 与 EOF 同时满足才成功。
- 新增 3 个 xUnit 用例；完整测试套件 799/799 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实文件句柄读取/计数/hash/EOF、严格 format v1 reader 与真实 coordinator。

### 2026-08-13：2.2/2.3 publish lease（部分）

- 新增 `Scripts/Core/V3/V3PublishLease.cs`：一次性 publish lease（operation/slot/签发时间）与基础校验。
- 新增 3 个 xUnit 用例；完整测试套件 802/802 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实文件句柄读取/计数/hash/EOF、严格 format v1 reader 与真实 coordinator。

### 2026-08-13：2.3 跨进程文件锁（部分）

- 新增 `Scripts/Core/V3/V3FileLock.cs`：以独占打开 `.save-root.lock` 实现跨进程排他；释放后其他进程可获取。
- 新增 2 个 xUnit 用例；完整测试套件 812/812 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实文件句柄读取/计数/hash/EOF、严格 format v1 reader 与完整 coordinator。

### 2026-08-13：2.1/2.2 文件槽存储（部分）

- 新增 `Scripts/Core/V3/V3FileSlotStore.cs`：基于真实文件系统的 V3 槽存储，在 root 下按 slotId 保存 manifest/payload，使用根锁文件排他，支持 Save/Load/Delete/List。
- 新增 3 个 xUnit 用例；完整测试套件 815/815 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：同句柄 length/hash/EOF、严格 format v1 reader 与完整 coordinator。

### 2026-08-13：2.2 严格道路 payload reader（部分）

- 新增 `Scripts/Core/V3/V3StrictRoadPayloadReader.cs`：结合 format v1 codec 与分层预算读取道路 payload，字节/实体超限拒绝。
- 新增 3 个 xUnit 用例；完整测试套件 818/818 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：同句柄 length/hash/EOF、完整严格 token reader 与真实 coordinator。

### 2026-08-13：2.2 文件 payload 同句柄校验（部分）

- 新增 `Scripts/Core/V3/V3FilePayloadVerifier.cs`：从文件路径读取字节并执行同句柄 length/hash/EOF 校验。
- 新增 3 个 xUnit 用例；完整测试套件 821/821 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实 coordinator 与完整 Load commit 协议。

### 2026-08-13：2.3 文件保存协调器（部分）

- 新增 `Scripts/Core/V3/V3FileSaveCoordinator.cs`：基于文件槽存储与进程内 gate 的 Save/Load/Delete 排他协调器。
- 新增 3 个 xUnit 用例；完整测试套件 824/824 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、完整 Load commit 协议与跨进程 coordinator 集成。

### 2026-08-13：2.2 槽完整性校验（部分）

- 新增 `Scripts/Core/V3/V3SlotIntegrity.cs`：验证槽目录的 manifest 与每个 payload 文件同句柄校验。
- 新增 3 个 xUnit 用例；完整测试套件 827/827 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、完整 Load commit 协议与跨进程 coordinator 集成。

### 2026-08-13：2.3 文件协调器（部分）

- 新增 `Scripts/Core/V3/V3FileCoordinator.cs`：组合进程内 gate 与跨进程文件锁，两者都获取后才算持有。
- 新增 3 个 xUnit 用例；完整测试套件 830/830 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、完整 Load commit 协议与真实应用装配。

### 2026-08-13：2.3 Load commit 门（部分）

- 新增 `Scripts/Core/V3/V3LoadCommitter.cs`：仅当协议处于 Preflight 且所有 participant 已准备时允许进入 non-yield commit。
- 新增 3 个 xUnit 用例；完整测试套件 833/833 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.1/2.2 道路槽工厂（部分）

- 新增 `Scripts/Core/V3/V3RoadSlotFactory.cs`：从 `RoadGraphV3Revision` 构造完整 V3 道路槽（manifest + road_network.json payload）。
- 新增 2 个 xUnit 用例；完整测试套件 835/835 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.2 JSON 重复键检测（部分）

- 新增 `Scripts/Core/V3/V3JsonDuplicateDetector.cs`：使用 Utf8JsonReader 检测任意深度 JSON 对象中的重复属性名。
- 新增 4 个 xUnit 用例；完整测试套件 839/839 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.2 manifest 严格读取（部分）

- 新增 `Scripts/Core/V3/V3ManifestStrictReader.cs`：先拒绝重复键，再执行 manifest codec/validator。
- 新增 3 个 xUnit 用例；完整测试套件 842/842 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.2 道路 payload 严格读取（部分）

- 新增 `Scripts/Core/V3/V3RoadPayloadStrictReader.cs`：先拒绝重复键，再执行 codec + 分层预算读取。
- 新增 3 个 xUnit 用例；完整测试套件 845/845 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.3 槽加载服务（部分）

- 新增 `Scripts/Core/V3/V3SlotLoadService.cs`：从文件槽读取、严格解析 road payload、重建 `RoadGraphV3Revision`。
- 新增 3 个 xUnit 用例；完整测试套件 848/848 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.2 manifest 严格文件读取（部分）

- 新增 `Scripts/Core/V3/V3ManifestStrictFileReader.cs`：从文件读取 manifest 并执行严格读取（重复键 + codec/validator）。
- 新增 3 个 xUnit 用例；完整测试套件 851/851 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.2 道路 payload 严格文件读取（部分）

- 新增 `Scripts/Core/V3/V3RoadPayloadStrictFileReader.cs`：从文件读取道路 payload 并执行严格读取。
- 新增 3 个 xUnit 用例；完整测试套件 854/854 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.3 槽保存服务（部分）

- 新增 `Scripts/Core/V3/V3SlotSaveService.cs`：将 `RoadGraphV3Revision` 保存为文件槽。
- 新增 2 个 xUnit 用例；完整测试套件 856/856 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.1 槽列表服务（部分）

- 新增 `Scripts/Core/V3/V3SlotListService.cs`：列出文件槽存储中的槽摘要。
- 新增 2 个 xUnit 用例；完整测试套件 858/858 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.3 槽删除服务（部分）

- 新增 `Scripts/Core/V3/V3SlotDeleteService.cs`：从文件槽存储删除指定槽。
- 新增 2 个 xUnit 用例；完整测试套件 860/860 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.3 autosave 服务（部分）

- 新增 `Scripts/Core/V3/V3SlotAutosaveService.cs`：结合 autosave 合并策略与槽保存服务，维护单个 pending 标记。
- 新增 3 个 xUnit 用例；完整测试套件 863/863 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.2 槽聚合 digest（部分）

- 新增 `Scripts/Core/V3/V3SlotDigest.cs`：按文件名排序后对 name/length/hash 计算 SHA-256 聚合 digest。
- 新增 3 个 xUnit 用例；完整测试套件 866/866 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.2 发布服务（部分）

- 新增 `Scripts/Core/V3/V3PublishService.cs`：保存新槽并生成 publish descriptor（旧/新聚合 digest）。
- 新增 3 个 xUnit 用例；完整测试套件 869/869 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.2 删除服务（部分）

- 新增 `Scripts/Core/V3/V3DeleteService.cs`：删除槽并生成 delete descriptor（含 digest/tombstone）。
- 新增 2 个 xUnit 用例；完整测试套件 871/871 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.2 槽备份/恢复服务（部分）

- 新增 `Scripts/Core/V3/V3SlotBackupService.cs`：在 root 与 backupRoot 之间复制/恢复整个槽目录。
- 新增 3 个 xUnit 用例；完整测试套件 874/874 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.2 槽恢复服务（部分）

- 新增 `Scripts/Core/V3/V3SlotRecoveryService.cs`：当槽缺失时从 backupRoot 恢复。
- 新增 3 个 xUnit 用例；完整测试套件 877/877 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.1/2.2 槽完整性扫描（部分）

- 新增 `Scripts/Core/V3/V3SlotIntegrityScanner.cs`：扫描 V3 根，使用槽完整性校验将 direct child 分类为 CompleteV3/CorruptV3/Foreign/Unsafe。
- 新增 3 个 xUnit 用例；完整测试套件 880/880 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.2/2.3 槽事务服务（部分）

- 新增 `Scripts/Core/V3/V3SlotTransactionService.cs`：统一 Publish / Delete / Recover 操作入口。
- 新增 2 个 xUnit 用例；完整测试套件 882/882 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.1 槽健康检查（部分）

- 新增 `Scripts/Core/V3/V3SlotHealthCheck.cs`：统计 V3 根中各 occupant 分类数量。
- 新增 1 个 xUnit 用例；完整测试套件 883/883 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.1 单槽状态服务（部分）

- 新增 `Scripts/Core/V3/V3SlotStatusService.cs`：返回指定槽的 occupant 摘要（CompleteV3/CorruptV3/Absent/Unsafe）。
- 新增 4 个 xUnit 用例；完整测试套件 887/887 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.1 槽 manifest 服务（部分）

- 新增 `Scripts/Core/V3/V3SlotManifestService.cs`：从文件槽读取 manifest。
- 新增 2 个 xUnit 用例；完整测试套件 889/889 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.1 槽 payload 服务（部分）

- 新增 `Scripts/Core/V3/V3SlotPayloadService.cs`：从文件槽读取指定 payload 字节。
- 新增 2 个 xUnit 用例；完整测试套件 891/891 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.1 槽快照服务（部分）

- 新增 `Scripts/Core/V3/V3SlotSnapshotService.cs`：读取槽目录全部文件（含 manifest）为字典。
- 新增 2 个 xUnit 用例；完整测试套件 893/893 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.1 槽复制服务（部分）

- 新增 `Scripts/Core/V3/V3SlotCopyService.cs`：将槽从 sourceRoot 复制到 destinationRoot。
- 新增 2 个 xUnit 用例；完整测试套件 895/895 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.1 槽移动服务（部分）

- 新增 `Scripts/Core/V3/V3SlotMoveService.cs`：复制到目标根后删除源槽。
- 新增 2 个 xUnit 用例；完整测试套件 897/897 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.1 槽重命名服务（部分）

- 新增 `Scripts/Core/V3/V3SlotRenameService.cs`：将槽目录移动到新 ID。
- 新增 2 个 xUnit 用例；完整测试套件 899/899 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.2/8.5 道路保存管线（部分）

- 新增 `Scripts/Core/V3/V3RoadSavePipeline.cs`：将 `RoadGraphV3Revision`/Controller 保存为槽，并从槽加载为新 Controller。
- 新增 2 个 xUnit 用例；完整测试套件 901/901 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.2/2.3 槽事务协调器（部分）

- 新增 `Scripts/Core/V3/V3SlotTransactionCoordinator.cs`：在进程内 `V3CoordinatorGate` 持有期间统一执行 Publish/Delete/Recover/List/GetStatus；文件槽存储各自使用根锁保证跨进程排他。
- 新增 7 个 xUnit 用例；完整测试套件 1015/1015 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：完整严格 token reader、真实应用装配与端到端 Load commit。

### 2026-08-13：2.2 严格 token reader（部分）

- 新增 `Scripts/Core/V3/V3StrictTokenReader.cs`：从禁止共享写/删的同一句柄读取完整字节，校验无 BOM、严格 UTF-8、重复属性名、canonical number lexeme、初始/已消费长度、EOF 与 SHA-256。
- `V3ManifestStrictFileReader` 与 `V3RoadPayloadStrictFileReader` 改用该 reader 后再进入 codec/validator 与预算读取。
- 新增 7 个 xUnit 用例；完整测试套件 911/911 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与端到端 Load commit。

### 2026-08-13：2.2 槽加载 payload 摘要校验（部分）

- `V3SlotLoadService` 在解析 road payload 前先按 manifest `Files` 校验每个 payload 的存在性与 SHA-256/长度，防止未列文件缺失或摘要不符仍进入图解析。
- 新增 1 个 xUnit 用例；完整测试套件 912/912 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与端到端 Load commit。

### 2026-08-13：2.3 道路 Load 管线（部分）

- 新增 `Scripts/Core/V3/V3RoadLoadPipeline.cs`：按 `V3LoadProtocol` 的 Admission -> Prepare -> Preflight -> Commit 四阶段加载槽，Prepare 通过 `V3SlotLoadService` 构造不可变 revision，Commit 阶段创建新 facade/controller；任何失败使协议进入 `Failed`。
- 新增 2 个 xUnit 用例；完整测试套件 914/914 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与端到端 Load commit。

### 2026-08-13：2.2 槽读取严格 manifest（部分）

- `V3SlotReader` 改用 `V3ManifestStrictReader` 读取 manifest，槽目录中的重复键 manifest 在进入业务读取前即被拒绝。
- 新增 1 个 xUnit 用例；完整测试套件 915/915 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与端到端 Load commit。

### 2026-08-13：2.2 槽完整性严格 manifest（部分）

- `V3SlotIntegrity` 改用 `V3ManifestStrictFileReader` 读取 manifest，完整性扫描同样拒绝重复键/非 canonical JSON。
- 新增 1 个 xUnit 用例；完整测试套件 916/916 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与端到端 Load commit。

### 2026-08-13：2.1 文件槽列表按完整性分类（部分）

- `V3FileSlotStore.List` 对含 manifest 的目录执行 `V3SlotIntegrity.Verify`，损坏 payload 的槽分类为 `CorruptV3` 而非 `CompleteV3`。
- 新增 1 个 xUnit 用例；完整测试套件 917/917 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与端到端 Load commit。

### 2026-08-13：2.1/2.2 槽 ID 一致性与重命名更新 manifest（部分）

- `V3SlotIntegrity` 校验目录名与 manifest `SlotId` 一致，目录与内部声明不符的槽分类为 `CorruptV3`。
- `V3SlotRenameService` 在目录移动后重写 manifest 的 `SlotId`，避免重命名产生内部声明不一致的槽。
- 新增 2 个 xUnit 用例；完整测试套件 918/918 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与端到端 Load commit。

### 2026-08-13：2.3 道路保存/加载协调器（部分）

- 新增 `Scripts/Core/V3/V3RoadSaveLoadCoordinator.cs`：在进程内 gate 持有期间统一执行道路槽 Save/Load，为真实应用装配提供同步入口。
- 新增 3 个 xUnit 用例；完整测试套件 921/921 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与端到端 Load commit。

### 2026-08-13：2.3 道路 Load 注入现有控制器（部分）

- `V3RoadLoadPipeline.TryLoadIntoController` 将新加载的 revision 以 full reset 注入现有 `RoadGraphV3Controller`，并清空 undo/redo、切换到新 lineage。
- 新增 1 个 xUnit 用例；完整测试套件 922/922 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与端到端 Load commit。

### 2026-08-13：2.1 槽复制仅允许完整槽（部分）

- `V3SlotCopyService.Copy` 先执行 `V3SlotIntegrity.Verify`，损坏或 ID 不一致的源槽不会被复制到目标根。
- 新增 1 个 xUnit 用例；完整测试套件 923/923 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与端到端 Load commit。

### 2026-08-13：2.3 autosave 协调器（部分）

- 新增 `Scripts/Core/V3/V3SlotAutosaveCoordinator.cs`：根据进程内 gate 是否可获取推导 `isBusy`，并在 gate 持有期间保持 autosave 合并策略。
- 新增 4 个 xUnit 用例；完整测试套件 927/927 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与端到端 Load commit。

### 2026-08-13：2.1 槽移动损坏回归（测试）

- 新增 `V3SlotMoveServiceTests.Move_CorruptSlot_FailsAndKeepsSource`：损坏源槽不会被移动，也不会在目标根留下副本。
- 新增 1 个 xUnit 用例；完整测试套件 928/928 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实应用装配与端到端 Load commit。

### 2026-08-13：2.3 道路应用门面（部分）

- 新增 `Scripts/Core/V3/RoadGraphV3Application.cs`：持有当前 controller、`Revision`、`CurrentNodeCount`/`CurrentEdgeCount`/`CurrentGeometrySegmentCount`/`CurrentSelfLoopCount`/`CurrentRoadTypeCounts`、`CanUndo`/`CanRedo`、`ClearHistory`、`RoadToolState`、`DefaultStyles`、`Presentation`、`HitProvider`、保存根与共享 gate 的 `V3RoadSaveLoadCoordinator` / `V3SlotAutosaveCoordinator` / `V3SlotTransactionCoordinator`，提供 `Save`/`SaveCurrent`/`SaveAs`/`Load`/`LoadIntoCurrent`/`Delete`/`DeleteCurrentSlot`/`List`/`ListUsableSlots`/`GetStatus`/`GetManifest`/`GetPayload`/`CurrentSlotManifest`/`CurrentSlotPayload`/`CurrentSlotCityName`/`CurrentSlotPopulation`/`CurrentSlotFunds`/`CurrentSlotThumbnailFile`/`CurrentSlotHasThumbnail`/`CurrentSlotFileCount`/`CurrentSlotHasFiles`/`CurrentSlotFiles`/`GetCurrentSlotFile`/`CurrentSlotHasRoadNetwork`/`CurrentSlotRoadNetworkJson`/`CurrentSlotFileNames`/`CurrentSlotTotalBytes`/`CurrentSlotFormatFamily`/`CurrentSlotSchemaVersion`/`CurrentSlotSummary`/`CurrentSlotDisplayName`/`CurrentSlotTimestamp`/`CurrentSlotOccupant`/`CurrentSlotIsComplete`/`CurrentSlotIsCorrupt`/`CurrentSlotIsAbsent`/`CurrentSlotIsUnsafe`/`CurrentSlotIsForeign`/`CurrentSlotIsOperable`/`HasCurrentSlot`/`TryAutosave`/`TryAutosaveCurrent`/`NewCity`/`TryAddNode`/`TryAddEdge`/`TryUndo`（含 token）/`TryRedo`（含 token）/`TryBuild`（含 snapRadius）/`TryBuildFromPolyline`/`TryUpgrade`/`TryUpgradeEdges`/`TryChangeRoadType`/`TryRemove`/`TryRemoveEdge`/`TryRemoveEdges`/`BuildSurfaceSnapshot`/`BuildDefaultSurfaceSnapshot`/`BuildRibbonMeshes`/`BuildDefaultRibbonMeshes`/`BuildJunctionPatches`/`BuildDefaultJunctionPatches`/`BuildPresentationFullReset`/`TryPrepareLoad`/`TryCommitPreparedLoad`/`TryRequestPresentation`/`TryFindClosestSurfaceHit`/`TryFindClosestJunctionSurfaceHit`/`CurrentTool`/`SelectedRoadType` 同步入口；构造函数支持注入 `RoadTypeStyleCatalogResult` 或 `RoadConfigV3`。
- 新增 74 个 xUnit 用例；完整测试套件 1087/1087 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Godot 场景装配与端到端 Load commit。

### 2026-08-13：2.3 道路 Load 管线携带 empty tool root 计划（部分）

- `V3RoadLoadPipelineResult` 新增 `ToolPlan`，`Load` 支持传入 `RoadToolState` 在 Preflight/Commit 产物中携带 `RoadToolFullReset`；新增 `TryLoadIntoController` 重载在 full reset 后应用 empty tool root。
- 新增 2 个 xUnit 用例；完整测试套件 1115/1115 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Godot 场景装配、renderer participant 与端到端 aggregate Load commit。

### 2026-08-13：2.3 应用 Load/LoadIntoCurrent 应用 empty tool root（部分）

- `V3RoadSaveLoadCoordinator` 新增 `LoadResult`（gate 内返回完整管线结果），`RoadGraphV3Application.Load` / `LoadIntoCurrent` 现在会携带当前 `ToolState` 生成 `RoadToolFullReset`，并在成功 full reset 后应用到工具状态。
- 新增 2 个 xUnit 用例；完整测试套件 1117/1117 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Godot 场景装配、renderer participant 与端到端 aggregate Load commit。

### 2026-08-13：2.3 道路 Load 管线携带 presentation full-reset 计划（部分）

- `V3RoadLoadPipelineResult` 新增 `PresentationPlan`，`Load` 支持在 Preflight 用加载 revision 构建 `RoadSurfaceSnapshot` 并生成 `RoadPresentationFullReset`；`RoadGraphV3Application.Load` 成功后应用该计划。
- 新增 2 个 xUnit 用例；完整测试套件 1126/1126 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Godot 场景装配、renderer participant 与端到端 aggregate Load commit。

### 2026-08-13：2.3 presentation full-reset 携带 mesh 数据（部分）

- `RoadPresentationFullReset` 新增 `RibbonMeshes` / `JunctionPatches` / `HasMeshData`；`V3RoadLoadPipeline` 在 Preflight 同时生成 ribbon/junction mesh 数据放入 presentation 计划。
- 新增 1 个 xUnit 用例；完整测试套件 1181/1181 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Godot 场景装配、renderer participant 与端到端 aggregate Load commit。

### 2026-08-13：2.3 LoadIntoCurrent 应用 presentation full-reset（部分）

- `RoadGraphV3Application.LoadIntoCurrent` 改为直接使用 `V3RoadLoadPipeline.Load`，在 full reset 后同时应用 `ToolPlan` 与 `PresentationPlan`，与 `Load` 的 Load 生命周期行为保持一致。
- 新增 1 个 xUnit 用例；完整测试套件 1127/1127 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Godot 场景装配、renderer participant 与端到端 aggregate Load commit。

### 2026-08-13：2.3 Load aggregate 协调器（部分）

- 新增 `Scripts/Core/V3/V3LoadAggregateCoordinator.cs`：组合 `V3LoadProtocol` 与 `V3PreparedAggregate`，要求全部 required participant 在 Preflight 前就绪，并在 non-yield commit 中执行一次不可抛动作；commit 抛错进入 `Failed`。
- 新增 7 个 xUnit 用例；完整测试套件 1137/1137 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：将协调器接入 `V3RoadLoadPipeline` / 真实 Godot 场景装配与 renderer participant。

### 2026-08-13：2.3 工具/渲染 Load 参与者包装（部分）

- 新增 `V3ToolLoadParticipant` / `V3RendererLoadParticipant`：分别包装 `RoadToolFullReset` 与 `RoadPresentationFullReset`，暴露 `IsPrepared` / `CanCommit`。
- 新增 6 个 xUnit 用例；完整测试套件 1188/1188 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误；Godot 编辑器加载 `MapTest` 并冻结运行 1 帧无新增 stderr 错误。
- 尚未完成：将参与者包装接入 `V3LoadAggregateCoordinator` 的 prepared/commit 流程。

### 2026-08-13：2.3 aggregate 协调器接受参与者包装（部分）

- `V3LoadAggregateCoordinator` 新增 `TryPrepare(V3ToolLoadParticipant)` / `TryPrepare(V3RendererLoadParticipant)` 重载；渲染参与者必须 `CanCommit` 才会标记 prepared。
- 新增 3 个 xUnit 用例；完整测试套件 1191/1191 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：在 `V3RoadLoadPipeline` 中改用参与者包装并完成 non-yield commit。

### 2026-08-13：2.3 道路 Load 管线改用参与者包装（重构）

- `V3RoadLoadPipeline` 的 tool/renderer prepared 流程改为使用 `V3ToolLoadParticipant` / `V3RendererLoadParticipant`，渲染参与者必须 `CanCommit` 才会进入 aggregate。
- 完整测试套件 1191/1191 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Load 隐藏资源 Preflight 与 non-yield 联合交换。

### 2026-08-13：2.3 参与者包装提供应用入口（部分）

- `V3ToolLoadParticipant.TryApplyTo` / `V3RendererLoadParticipant.TryApplyTo` 可将 prepared 计划应用到 `RoadToolState` / `RoadPresentationState`。
- 新增 4 个 xUnit 用例；完整测试套件 1195/1195 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Load 隐藏资源 Preflight 与 non-yield 联合交换。

### 2026-08-13：2.3 Load 结果统一应用参与者（部分）

- `V3RoadLoadPipelineResult` 新增 `TryApplyParticipants`：一次性把 `ToolPlan` / `PresentationPlan` 应用到 `RoadToolState` / `RoadPresentationState`。
- 新增 1 个 xUnit 用例；完整测试套件 1196/1196 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Load 隐藏资源 Preflight 与 non-yield 联合交换。

### 2026-08-13：2.3 NewCity 重置表现状态（部分）

- `RoadPresentationController` 新增 `Reset` 与 `State` 属性；`RoadGraphV3Application.NewCity` 在新建城市时重置 presentation 到空快照，避免旧表现残留。
- 新增 1 个 xUnit 用例；完整测试套件 1197/1197 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Load 隐藏资源 Preflight 与 non-yield 联合交换。

### 2026-08-13：2.3 应用 Load 改用统一参与者应用（重构）

- `RoadGraphV3Application.Load` / `LoadIntoCurrent` 改为先通过 `V3RoadLoadPipelineResult.TryApplyParticipants` 应用 tool/presentation 计划，成功后再替换 controller，避免应用失败时已切换图状态。
- 完整测试套件 1197/1197 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Load 隐藏资源 Preflight 与 non-yield 联合交换。

### 2026-08-13：2.3 应用提供 full-reset 应用便捷入口（部分）

- `RoadGraphV3Application` 新增 `TryApplyToolFullReset` / `TryApplyPresentationFullReset`，供外部 Load 参与者直接应用 prepared 计划。
- 新增 2 个 xUnit 用例；完整测试套件 1199/1199 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Load 隐藏资源 Preflight 与 non-yield 联合交换。

### 2026-08-13：2.3 Load Preflight 计划（部分）

- 新增 `V3LoadPreflightPlan`：持有已解析 revision 与 tool/renderer 参与者计划，`CanCommit` 要求全部参与者可提交，并提供 `CreateController` 创建新控制器。
- 新增 3 个 xUnit 用例；完整测试套件 1202/1202 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误；Godot 编辑器加载 `MapTest` 并冻结运行 1 帧无新增 stderr 错误。
- 尚未完成：将 `V3RoadLoadPipeline` 重构为 Preflight 计划 + non-yield commit。

### 2026-08-13：2.3 道路 Load 管线使用 Preflight 计划（重构）

- `V3RoadLoadPipeline.Load` 改为先构建 `V3LoadPreflightPlan`（revision + tool/renderer participants），校验 `CanCommit` 后用它创建 controller 并完成 non-yield commit。
- 完整测试套件 1202/1202 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Load 隐藏资源 Preflight 与端到端 aggregate Load commit。

### 2026-08-13：2.3 道路 Load 分离 Prepare/Commit（部分）

- 新增 `V3RoadLoadPrepareResult` 与 `V3RoadLoadPipeline.Prepare`：在 Admission/Prepare/Preflight 后返回 `V3LoadPreflightPlan` 与处于 Preflight 的 coordinator；`Load` 改为先 Prepare 再 Commit。
- 新增 2 个 xUnit 用例；完整测试套件 1204/1204 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Load 隐藏资源 Preflight 与端到端 aggregate Load commit。

### 2026-08-13：2.3 公开 Commit 入口（部分）

- `V3RoadLoadPipeline` 新增 `Commit(V3RoadLoadPrepareResult, long lineageID)`：对已 Preflight 的计划执行 non-yield commit 并返回 controller；`Load` 现为 Prepare + Commit。
- 新增 1 个 xUnit 用例；完整测试套件 1205/1205 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Load 隐藏资源 Preflight 与端到端 aggregate Load commit。

### 2026-08-13：2.3 保存协调器使用 Prepare/Commit（重构）

- `V3RoadSaveLoadCoordinator.LoadResult` 改为在 gate 持有期间先 `V3RoadLoadPipeline.Prepare` 再 `Commit`，保持原子 Load 生命周期。
- 完整测试套件 1205/1205 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Load 隐藏资源 Preflight 与端到端 aggregate Load commit。

### 2026-08-13：2.3 应用暴露 Load Preflight（部分）

- `V3RoadSaveLoadCoordinator` 新增 `Prepare`；`RoadGraphV3Application.TryPrepareLoad` 暴露 Preflight 计划，供真实场景在 commit 前预检。
- 新增 1 个 xUnit 用例；完整测试套件 1206/1206 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Load 隐藏资源 Preflight 与端到端 aggregate Load commit。

### 2026-08-13：2.3 应用提交 Prepared Load（部分）

- `V3RoadLoadPrepareResult` 新增 `SlotId`；`RoadGraphV3Application.TryCommitPreparedLoad` 对 Preflight 计划执行 Commit 并切换 controller/当前槽。
- `RoadGraphV3System` 转发 `TryCommitPreparedLoad`，成功后应用当前 presentation。
- 新增 1 个 xUnit 用例；完整测试套件 1207/1207 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Load 隐藏资源 Preflight 与端到端 aggregate Load commit。

### 2026-08-13：2.3 Prepared Load 提交应用参与者（修复）

- `RoadGraphV3Application.TryCommitPreparedLoad` 在切换 controller 前应用 `result.TryApplyParticipants`，确保 tool/presentation 与图同时生效。
- 更新 1 个 xUnit 用例断言；完整测试套件 1207/1207 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Load 隐藏资源 Preflight 与端到端 aggregate Load commit。

### 2026-08-13：2.3 Prepare 结果可应用参与者（部分）

- `V3RoadLoadPrepareResult` 新增 `TryApplyParticipants`，可在 commit 前把 tool/presentation 计划应用到目标状态。
- 新增 1 个 xUnit 用例；完整测试套件 1209/1209 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Load 隐藏资源 Preflight 与端到端 aggregate Load commit。

### 2026-08-13：2.3 道路 Load 管线改用 aggregate 协调器（重构）

- `V3RoadLoadPipeline.Load` 改为使用 `V3LoadAggregateCoordinator` 管理 Admission/Prepare/Preflight/Commit，不再手写 `V3LoadProtocol` + `V3PreparedAggregate`；行为与既有测试保持一致。
- 完整测试套件 1137/1137 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Godot 场景装配、renderer participant 与端到端 aggregate Load commit。

### 2026-08-13：2.3 道路 Load 管线按可选参与者纳入 aggregate（部分）

- `V3RoadLoadPipeline` 新增 `ToolParticipant` / `RendererParticipant` 常量；传入 `RoadToolState` 时要求 `tool` 参与者，传入样式与目标 token 时要求 `renderer` 参与者，全部 prepared 后才进入 Preflight/Commit。
- 新增 1 个 xUnit 用例；完整测试套件 1138/1138 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Godot 场景装配、renderer participant 与端到端 aggregate Load commit。

### 2026-08-13：2.1 槽状态返回 manifest 摘要（更新）

- `V3SlotStatusService.GetStatus` 对完整槽读取 manifest，返回 `DisplayName` 与 `Timestamp`，不再只返回 slotId 和 null。
- 更新 1 个 xUnit 用例断言；完整测试套件 935/935 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Godot 场景装配与端到端 Load commit。

### 2026-08-13：2.1 槽列表返回 manifest 摘要（更新）

- `V3FileSlotStore.List` 对完整槽读取 manifest，返回 `DisplayName` 与 `Timestamp`，同时保留按完整性分类。
- 更新 1 个 xUnit 用例断言；完整测试套件 935/935 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Godot 场景装配与端到端 Load commit。

### 2026-08-13：2.1 完整性扫描返回 manifest 摘要（更新）

- `V3SlotIntegrityScanner.Scan` 对完整槽读取 manifest，返回 `DisplayName` 与 `Timestamp`，与文件槽列表保持一致。
- 更新 1 个 xUnit 用例断言；完整测试套件 935/935 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Godot 场景装配与端到端 Load commit。

### 2026-08-13：2.1 槽 manifest 服务直接严格读取（更新）

- `V3SlotManifestService.GetManifest` 改为直接读取 `manifest.json` 并执行严格 reader，不再加载整个槽的 payload。
- 完整测试套件 935/935 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Godot 场景装配与端到端 Load commit。

### 2026-08-13：2.1 槽 payload 服务直接校验读取（更新）

- `V3SlotPayloadService.GetPayload` 改为直接读取目标 payload，并在返回前校验 manifest 声明的 length/hash；未声明、缺失或摘要不符的文件返回 null。
- 完整测试套件 935/935 通过，`dotnet build SimpleCities.sln` 0 警告/0 错误。
- 尚未完成：真实 Godot 场景装配与端到端 Load commit。

## 执行顺序

### 阶段 2：第三代道路 payload、容器与操作协议

<a id="v3-save-system2.1"></a>

- [ ] **2.1 建立隔离的 V3 format v1 并删除旧格式入口**
  - 当前问题：`RoadGraph.Persistence.cs` 只接受 V2 `schemaVersion = 1`，保存 Node/Edge/Group、拒绝 self-loop 且 Edge 没有 RoadType；`SaveManager` 在编辑器与导出中使用不同 V2 根。延续版本号或复用根会迫使新实现保留格式探测和迁移分派。
  - 修改：生产环境在编辑器和导出中统一使用 `user://saves-v3`，自动化通过构造参数注入临时根，禁止再以 `res://` 作为 V3 写入位置。manifest 与每个业务 payload 都必填 `formatFamily: "simple-cities-v3"` 和各自 `schemaVersion: 1`，道路 payload 另有 `payloadType: "road-network"`；只保存 canonical Node/Edge、Edge 级 `roadType` 和原生 geometry，允许正长度 self-loop 及非覆盖 parallel Edge。prepared `RoadGraphRevision` 精确保留合法 `nextID`，Load 创建新 runtime lineage。删除 V2 DTO、codec 分派、迁移/导入/只读加载入口和旧 `ISaveable` 恢复适配器；reader 对错误 family/version、Group/groupID、未知字段、非规范二度节点、非法 seam/full-turn、内部交叉、重复覆盖和非法 RoadType 直接拒绝，不在 Load 中 canonicalize 或补默认值。
  - 隔离边界：V3 的 List/Save/Save As/Load/Delete/autosave/恢复/启动清理只能从已验证的 V3 root capability 派生路径；不得枚举、打开、哈希、移动、转换、覆盖或删除 `res://saves`、`user://saves`。V2 槽不出现在 V3 UI；把 V2/未知目录手工复制到 V3 根只得到 `Foreign` 分类。
  - 依赖：`v3-road-graph:8.0`～`8.5`；V2 `save-system:0.4`、`0.5`、`5.3` 只作为历史事实，不是代码或格式依赖。
  - 集成负责人：`v3-save-system`；最终端到端判定由 `v3-road-graph:8.6` 负责。
  - 验证：开放 Edge、rooted self-loop、parallel Edge、精确 full-turn、四种 RoadType、极值 `nextID` 和确定 JSON 往返；family/version 缺失、大小写、旧/未来版本、Group/groupID、未知/重复字段、错误 token、非规范图和损坏引用拒绝。分别在两个 V2 根放置 canary，执行全部 V3 操作及崩溃恢复，比较 direct-child 集合、文件字节和时间戳均未变化；自动化确认源码没有 V2 DTO/reader/import API。
  - 验收：只有精确 V3 format v1 能进入 prepared aggregate；合法图逐值保留 incidence、rooted seam、parallel Edge、原生 geometry、RoadType 和 watermark；非法或非 V3 输入在大额分配、图事件和活动状态变化前失败；生产运行只写 `user://saves-v3`，V2 根零访问。

<a id="v3-save-system2.2"></a>

- [ ] **2.2 为长连续 Edge 建立确定、有界且可恢复的存储管线**
  - 当前问题：当前保存先物化完整 DTO，再生成缩进 UTF-16 字符串；加载经过 `ReadAllText -> JsonDocument.Parse -> DTO deserialize`。文件、实体、geometry 和索引没有预分配门禁，manifest 不能证明 payload 完整，固定 staging/backup 也无法可靠归属一次操作。
  - 修改：format v1 使用 geometry 内联 Edge 的无 BOM、无缩进 canonical UTF-8 JSON，不引入拓扑 chunk。新增 streaming saveable contract；writer 从不可变 `RoadGraphRevision` 直接写 operation-specific staging。duplicate-aware token reader 从同一个禁止共享写/删的句柄完成属性原始字节、number lexeme、initial/declared/consumed length、EOF 与 SHA-256 校验，并在元素进入集合前执行文件、槽、实体、geometry、坐标、长度、ID、fragment/index ref、深度和字符串预算。manifest v1 严格约束槽摘要及按名称排序的 payload metadata；thumbnail 独立校验，缺失或损坏只产生占位 warning。
  - 发布与恢复：保存根使用进程内 async gate、跨进程 `.save-root.lock` 和 `.save-transactions/<slot>/<operation-id>/`。完整 staging 复核后先持久发布不可变 `publish.json`，绑定 slot、新旧 aggregate digest、staging/backup 固定路径和 token，再允许 canonical move。occupant 只分类为 `Absent | CompleteV3 | CorruptV3 | Foreign | Unsafe`；恢复只按 descriptor 的 new/old digest 矩阵完成发布、恢复旧槽、清理或返回 `PublicationRecoveryBlocked`，绝不按时间戳猜测。无 descriptor 的 partial transaction 只能隔离，不能提升为槽。首次/覆盖不可取消点分别是 `staging -> slot` 与 `slot -> backup`；最终路径完整复核后 cleanup 失败返回 `PublishedWithCleanupPending`，不回滚新槽。
  - 删除：`DeleteV3` 只接受明确目标、有效 UI generation 和 operation token；在锁内完整恢复该槽后，将 canonical 目录原子移动到 operation-specific deletion tombstone 作为不可取消点，再递归清理。越界后槽在逻辑上已删除；清理失败返回 `DeletedWithCleanupPending` 并由恢复继续，不把槽移回。`Foreign` / `Unsafe` 不允许 API 删除，`CorruptV3` 仅在 UI 显示精确目标并二次确认后可删除。
  - 依赖：`v3-road-graph:8.0`、`v3-road-graph:8.5`、`v3-save-system:2.1`；预算使用 Phase 0 的 junction-dense/geometry-dense 数据，V2 性能记录只作同机比较。
  - 集成负责人：`v3-save-system`；编辑历史内存属于 `v3-tool-input:2.3`，最终完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：确定输出；manifest null/range/lexeme、重复字段、UTF-8 与文件集合；同句柄替换/删除、length/hash/EOF；1/N/上下限 geometry、自环、parallel Edge、O(1) capture 和峰值分配；五类 occupant、首次/覆盖 descriptor 全中断点、descriptor/digest 歧义、无 descriptor staging、ENOSPC/ACL/路径冲突、cleanup pending、删除 tombstone、双进程和 Windows 导出。所有场景同时验证 V2 canary 未触碰；绕过锁写入者只记录为非协作威胁边界。
  - 验收：合法长 Edge 保持一个 Edge ID 并在预算内往返，同向连续 line 只写一个 primitive；超限输入在大额分配和图提交前拒绝；恢复只处理可证明属于 descriptor 的路径，歧义现场完整保留；已复核发布或已越界删除不因 cleanup 失败被误报或反向恢复。没有目录元数据耐久证据时只称 crash recoverable，事务 backup 不冒充长期备份。

<a id="v3-save-system2.3"></a>

- [ ] **2.3 建立非阻塞、排他的保存、加载与删除协议**
  - 当前问题：`SaveManager.Save/Load/SaveAutosave/DeleteSlot` 和 Timer autosave 同步执行；长图阻塞主线程，同一保存根的请求争用固定事务路径。当前 Load 顺序 restore，缺少 scene/saveable/participant generation、取消边界和一次不可失败的 aggregate commit。
  - 修改：以新接口实现保存根级 `SaveOperationCoordinator`、不可变 operation state/result、进程内 async gate 和跨进程 lock；不保留同步 bool API 适配器。Save admission 在主线程 O(1) 捕获 immutable root 与 generation，后台 serialize/hash/I/O，再由 `PublishV3` token 取得一次性 publish lease。Load 和 Delete 各有独立 token、取消点与结果；Timer busy 时至多合并一个 pending autosave，手动操作优先；场景退出停止 admission、取消未越界 worker并等待已越界事务收敛。
  - Load 协议：每次 Load 固定经过 Admission、Prepare、Preflight、Non-yield commit/notification。Admission 冻结新道路命令但逐值保留 graph、草稿、选择、hover、overlay、历史和 `CurrentSlotID`；Prepare 从受保护句柄构造完整 immutable aggregate，预建 graph root、empty tool root、纯 CLR tessellation、surface snapshot 与 hit-index 数据；Preflight 重新验证全部 generation/capacity/token 并创建隐藏 Mesh/RID 和不可抛 commit plan。任何关键失败都发生在 Preflight，活动状态逐值不变。
  - Commit 边界：不可 yield 临界区只交换已验证的 graph root/lineage、empty tool/overlay root、hidden Mesh/RID、surface snapshot、hit index、presentation token、diagnostics 和 `CurrentSlotID`，随后发布一次 matching full reset 与 `PresentationReady`。普通 observer 异常逐个隔离，结果提升为 `SucceededWithObserverWarnings`；不存在提交后关键 participant 失败或恢复页。Publish、Load、Delete 的 token、取消边界、结果和 UI 阶段不得合并。
  - 依赖：`v3-save-system:2.2`、`v3-road-graph:8.5`；工具 full reset 属于 `v3-tool-input:2.4`，隐藏资源与 surface 一次交换属于 `v3-grid-rendering:2.2`，busy/error/result 呈现属于 `v3-ui:1.4`。
  - 集成负责人：`v3-save-system`；真实场景端到端完成判定由 `v3-road-graph:8.6` 负责。
  - 验证：O(1) capture、后台 Godot Object 零访问、save I/O 中继续编辑；进程内/双进程 gate/lock、Timer 合并、手动优先和退出收敛；Publish/Load/Delete 分别产生唯一 token/result；fake graph/tool/presentation/第二 saveable aggregate、generation 失配、每个关键 Preflight 失败、commit 不抛、普通 observer 抛错、删除越界前后取消和 `CurrentSlotID` 结果。联合测试由协作系统覆盖真实隐藏资源及一次交换。
  - 验收：同一 V3 根跨进程最多一个目录事务；主线程不执行长 JSON/hash/I/O；Prepare/Preflight 失败逐值保留活动状态，non-yield commit 全有或全无且只通知一次；Load 只有成功、observer warning 或提交前失败/取消；autosave busy 有界；任何操作都不调用 V2 API 或触碰 V2 根。

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
- [x] **V2 payload 与保存根只作历史证据。** Node/Edge/Group、V2 `schemaVersion = 1`、`res://saves` 和 `user://saves` 均不是 V3 输入或实现依赖。

## 完成标准

1. `v3-save-system:2.1`～`2.3` 的新格式、根隔离、容量、流式 I/O、发布/删除恢复、并发、取消与 prepared aggregate 自动化全部通过。
2. V3 writer 只生成 `simple-cities-v3` format v1；reader 只接受精确 family/version，并完整表达 canonical Edge、self-loop、parallel Edge、RoadType 和 `nextID`。
3. 五类 occupant、publish/delete descriptor、删除 tombstone、`PublicationRecoveryBlocked`、`DeletionRecoveryBlocked`、`PublishedWithCleanupPending` 和 `DeletedWithCleanupPending` 通过全矩阵与中断点验证。
4. V2 两个保存根在 V3 List/Save/Save As/Load/Delete/autosave/恢复/启动清理中均零访问；不存在迁移、导入、只读加载、兼容 DTO 或双写入口。
5. Publish、Load 与 Delete 在 API、operation token、取消点、结果和 UI 呈现上分离；Load 的关键失败全部发生在 Preflight，commit 一次交换全部引用。
6. 主线程 capture 保持 O(1)，长 JSON/hash/I/O 在后台执行；同一 V3 根跨进程排他，autosave busy 有界，场景退出和各不可取消点均可收敛。
7. `v3-road-graph:8.6` 联合 `v3-grid-rendering:2.2`、`v3-tool-input:2.4` 和 `v3-ui:1.4` 在真实 `MapTest`、Windows 导出及 junction-dense/geometry-dense 场景完成最终集成；未取得目录元数据耐久证据时不得宣称 durable。
