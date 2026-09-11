# 存档系统 Bug 修复记录

> 日期：2026-06-06
> 影响文件：`Scripts/Road/RoadGraph.cs`
> 关联重构：road-system-v2-gen（阶段 A+B）

---

<a id="save-system-bug-1"></a>
## BUG-1：道路类型未写入存档，加载后统一退化为 Street

关联文档：`road-graph:BUG-8`

### 症状

使用 Dirt、Arterial 或 Highway 等非默认类型建设道路并保存后，再次加载存档，道路组和边的类型都会变成 `RoadType.Street`。道路几何与拓扑仍能恢复，因此问题容易表现为加载后道路分级样式或后续分级逻辑静默丢失。

### 根因分析

`RoadGraph.CaptureState()` 原先只记录边和道路组的 ID、连接关系、几何点与长度，`SegmentData` 和 `RoadData` 中没有道路类型字段。`RestoreFromSavedData()` 重建 `RoadGroup` 和 `GraphEdge` 时只能硬编码使用 `RoadType.Street`，导致非默认类型无法完成存档往返。

### 修复方案

在存档 DTO 中为边和道路组增加可空的 `Type` 字段，并在捕获状态时分别写入 `edge.Type` 与 `group.Type`：

```csharp
public class SegmentData
{
    [JsonPropertyName("type")]
    public int? Type { get; set; }
}

public class RoadData
{
    [JsonPropertyName("type")]
    public int? Type { get; set; }
}
```

恢复时优先使用边自身保存的类型，其次使用所属道路组的类型；旧存档没有 `type` 字段时，可空值保持为 `null`，最终兼容性回退到 `RoadType.Street`：

```csharp
RoadType edgeType;
if (edgeData.Type.HasValue)
    edgeType = (RoadType)edgeData.Type.Value;
else if (_groups.TryGetValue(edgeData.RoadID, out var existingGroup))
    edgeType = existingGroup.Type;
else
    edgeType = RoadType.Street;
```

### 影响范围

影响 `RoadGraph` 的存档捕获与恢复，以及 `SegmentData`、`RoadData` 的 JSON 结构。新存档能够保留道路类型；缺少 `type` 字段的旧存档仍按 Street 加载，不需要迁移旧文件。

---

## 验证状态

### BUG-1

- 关联提交：`6ec0a66`（`修复：保持道路类型存档并避免重复铺路副作用`）
- `dotnet build SimpleCities.sln`：构建成功，0 个错误，4 个既有的 `Scripts/Grid/MapBackground.cs` nullable 警告
- 已核对当前代码路径：存档捕获与恢复均处理 `RoadType`，且完整覆盖检查位于 `ResolveIntersections`、`SplitEdgesAtPathAnchors` 等变更操作之前
- 当前仓库未发现覆盖上述两个场景的自动化测试；本次未执行 Godot 运行时存档往返或重复铺路手工测试，因此不声明运行时回归验证已完成

### BUG-2

- `dotnet build SimpleCities.sln --no-restore`：成功，0 个警告，0 个错误。
- `dotnet test SimpleCities.sln --no-build --no-restore`：34 个测试全部通过，无失败或跳过。
- `godot --headless --path . --log-file .godot/qa-pause-menu-final.log --script tests/godot/pause_menu_runtime_contract.gd`：输出 `PASS pause menu runtime contract`；`MapTest -> MainMenu -> MapTest` 中注册数依次验证为 2、0、2，新场景随后保存和加载均成功。
- `godot --headless --path . --log-file .godot/qa-command-center-final.log --script tests/godot/command_center_runtime_contract.gd`：输出 `PASS command center runtime contract`，既有 HUD、输入、道路建造和同实例重进契约保持通过。
- Godot 编辑器桥接确认项目路径为当前仓库、Godot 4.7、主场景为 `MapTest`，`project.godot` 无磁盘/编辑器差异；磁盘版 `PauseMenu.tscn` 的四个视图已由编辑器有效场景树读取。
- 当前会话未暴露 `csharp-ls`，因此没有逐文件 C# LSP 诊断；真实编译、完整 .NET 测试和 Godot 运行时契约均已通过。Headless 输出中的 Windows 根证书读取错误和缺失依赖降级警告与本修复无关，契约仍以 0 退出并输出 PASS。既有编辑器运行会话持续报告 `Remote debugger: Packet too large`，导致 live runtime 查询超时；minimal DAP `stderr` 缓冲为空，因此没有把该连接故障当作本修复的运行时通过证据。

### BUG-3

- `dotnet test tests/SimpleCities.RoadGraph.Tests/SimpleCities.RoadGraph.Tests.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~RoadGraphPersistenceV2Tests|FullyQualifiedName~RoadGraphNodeIdentityTests|FullyQualifiedName~GraphEdgeGeometryTests"`：24 个聚焦测试全部通过。
- `dotnet test SimpleCities.sln --configuration Debug --no-restore`：204 个测试全部通过，无失败或跳过。
- `dotnet build SimpleCities.sln --configuration Debug --no-restore`：成功，0 个警告、0 个错误。
- `godot --headless --path . --log-file .godot/qa-roadgraph-v2-persistence.log --script tests/godot/pause_menu_runtime_contract.gd`：输出 `PASS pause menu runtime contract`；主场景完成两轮 `autosave` 保存加载。两条未挂载 `ToolManager` 的 HUD 警告来自契约脚本既有隔离场景，与 RoadGraph 恢复无关。
- 当前会话未暴露 `csharp-ls` MCP，逐文件 LSP 诊断被阻塞；编译器、xUnit 和 Godot 主场景运行时验证均已通过。

---

<a id="save-system-bug-2"></a>
## BUG-2：返回主菜单后旧场景 saveable 仍留在全局注册表

关联事项：`save-system:0.8`

### 症状

`SaveManager` 是跨场景存活的 Autoload，而 `RoadGraph` 和 `MainCamera` 随 `MapTest` 创建。结束当前城市返回 `MainMenu` 后，如果再次进入 `MapTest`，旧场景对象仍可能留在 `_saveables`；新场景再次注册 `road_network` 和 `camera` 时会形成同名活动对象，后续保存可能重复写同一文件，加载也可能调用已经退出场景的对象。

### 根因分析

原有注册表只有 `Register(ISaveable)`，没有与 Godot 场景退出对应的注销入口。`RoadSystem._Ready()` 每次进入场景都会创建新的 `RoadGraph`，`MainCamera` 也会生成新的场景实例，但两者退出树时都没有从 Autoload 注册表移除。由于对象引用不同，原有的“同一引用不重复添加”判断无法保护跨场景重进。

### 修复方案

`SaveManager` 增加 `Unregister(ISaveable)` 和 `RegisteredSaveableCount`。`Register` 对同一实例保持幂等，同时按 `SaveFileName` 拒绝第二个活动实例并返回 `false`，把重复文件所有权从静默冲突改为明确错误。

`RoadSystem._ExitTree()` 注销其当前 `Graph`，`MainCamera._ExitTree()` 注销自身；两者同时只在静态 `Instance` 指向当前对象时清理单例。场景重进相关的 `ToolManager` 与 `MapBackground` 也按相同所有权规则清理各自单例，但不参与 `_saveables`。

### 影响范围

修复影响 `SaveManager` 的活动注册生命周期，以及 `RoadSystem`、`MainCamera` 的场景退出行为。保存格式、manifest、槽名、RoadGraph JSON 和现有 `autosave` 内容均未改变。重复 `SaveFileName` 现在会拒绝后注册者，而不是允许两个对象竞争同一文件。

---

<a id="save-system-bug-3"></a>
## BUG-3：损坏的 RoadGraph 存档会在校验失败前清空活动道路图

> 修复日期：2026-08-03
> 关联事项：`save-system:0.5`、`save-system:5.3`

### 症状

加载包含缺失端点、悬空 Group、重复 ID、非法几何或错误 `nextID` 的道路 JSON 时，`RestoreState()` 可能先删除当前城市的全部道路，再在重建过程中抛出异常。调用方虽然会报告加载失败，但玩家加载前的有效道路图已经丢失，后续 ID 分配状态也可能改变。

### 根因分析

旧实现反序列化 `RoadGraphSaveData` 后立即调用 `ClearGraph()` 并写入 `_nextID`，随后才逐项创建 Node、Group 和 Edge。DTO 没有在提交前检查全局 ID 唯一性、双向 Group 成员关系、几何参数、段连续性、节点端点或 `nextID` 上界，因此任何中途异常都会暴露一个空图或部分恢复图。

### 修复方案

将持久化逻辑集中到 `Scripts/Road/RoadGraph.Persistence.cs`。`ParseAndValidateState()` 使用严格 `schemaVersion = 1` 的 Node/Edge/Group DTO，在临时字典中完成全部结构、引用和原生几何校验，并构造完整的 `GraphNode`、`GraphEdge`、`RoadGroup` 集合。只有临时状态全部有效时，`RestoreState()` 才清空活动图、复制实体、恢复 `_nextID`、重建邻接与空间索引并发出一次 `GraphCleared`。

失败路径不再执行任何活动图写入。回归测试将恢复前后的 `CaptureState()` JSON 逐字比较，因此同时覆盖拓扑、原生曲线参数和下一 ID 分配状态；并断言失败时不发出 `GraphCleared`。

### 影响范围

影响 RoadGraph 道路 JSON 的捕获与恢复。新 schema 不迁移旧 `junctions/segments/roads` payload，也不保存 `RoadType`、waypoint 或派生长度；六类原生几何直接保存类型和控制参数。`SaveManager` 的 manifest、槽目录和多系统加载顺序尚未改变，整槽预检仍由 `save-system:0.4`、`0.11` 后续完成。

---

<a id="save-system-bug-4"></a>
## BUG-4：缺失 manifest 版本被默认值当作当前版本

> 修复日期：2026-08-04
> 关联事项：`save-system:0.4`

### 症状

manifest 缺少 `schemaVersion`，或只提供大小写错误的 `SchemaVersion` 时，加载入口仍把它当作当前版本继续处理。旧格式或结构不完整的槽位因此可能越过版本门禁并进入文件收集与系统恢复阶段。

### 根因分析

`ManifestData.SchemaVersion` 原为非空 `int` 且初始化为 `1`。反序列化未映射到版本字段时保留该默认值，导致“缺失”与“当前版本”不可区分；`SaveManager.Load` 反序列化后也没有显式比较受支持版本。

### 修复方案

将 `ManifestData.SchemaVersion` 改为无默认值的可空字段，只有 `WriteManifest` 在保存时显式写入 `ManifestSchemaVersion = 1`。`SaveManager.ParseAndValidateManifest` 使用大小写敏感的专用 `JsonSerializerOptions`，在构造加载集合和调用任何 `RestoreState` 之前，只接受精确版本 1；空内容、缺失、旧版、未来版、错误类型和错误大小写均抛出可诊断 `JsonException`。

### 影响范围

影响 `SaveManager` 的 manifest 写入与加载前版本门禁。当前版本 autosave 格式保持不变；RoadGraph 私有 schema、自身临时恢复事务、多槽位命名和完整槽位预检范围未改变，后两者仍由后续待办负责。

## BUG-4 验证状态

- 修复前 `SaveManagerManifestVersionTests` 6 项中 2 项失败：缺失版本和错误大小写字段被默认值误接受；修复后 6/6 通过。
- `dotnet test SimpleCities.sln --configuration Debug --no-restore`：378 通过、0 失败、0 跳过。
- `dotnet build SimpleCities.sln --configuration Debug --no-restore`：0 警告、0 错误。
- `godot --headless --path . --log-file .godot/qa-save-manifest-version.log --script tests/godot/pause_menu_runtime_contract.gd`：输出 `PASS pause menu runtime contract`，两轮 autosave 保存/加载通过；两条 `ConstructionDock` 缺少 `ToolManager.Instance` 警告来自既有隔离场景。
- 当前会话未提供 `csharp-ls` MCP，无法执行逐文件 C# LSP 诊断。

---

<a id="save-system-bug-5"></a>
## BUG-5：系统文件名和槽目录链接可绕过存档根目录

> 修复日期：2026-08-04
> 关联事项：`save-system:0.10`

### 症状

虽然槽位名只接受安全 ASCII 字符，注册对象仍可通过带目录分隔符的 `ISaveable.SaveFileName` 让系统 JSON 写到槽目录之外；预先把合法槽目录替换为目录链接时，保存、读取或删除也可能跟随链接访问存档根之外的位置。

### 根因分析

`SaveSlotStore` 只校验了槽位字符串，随后直接对 `SaveFileName + ".json"` 使用 `Path.Combine`，没有约束系统文件名必须是单个安全标识。槽目录路径也没有核对为存档根的直接子项，且没有拒绝 Windows 重解析点。

### 修复方案

`SaveSlotStore` 现在分别校验内部槽位 ID 与系统文件名，只允许 ASCII 字母、数字、下划线和连字符；系统文件名在创建目录和捕获状态前完成校验。槽目录通过 `Path.GetFullPath` 和 `Path.GetRelativePath` 确认是根目录的直接子项，并拒绝已有的 `FileAttributes.ReparsePoint` 目录。玩家显示名与两类路径标识完全分离，只写入 manifest。

### 影响范围

影响所有槽位保存、加载、存在性检查和删除入口，以及注册系统 JSON 的文件名边界。现有 `autosave`、`road_network` 和 `camera` 标识仍合法；包含路径字符的玩家显示名继续被允许，因为它不会参与任何路径计算。

## BUG-5 验证状态

- `SaveManagerSlotContractTests` 与 `SaveManagerManifestVersionTests` 聚焦测试：28/28 通过，覆盖正反斜杠路径穿越、盘符路径、非 ASCII/空格槽 ID、系统文件名逃逸、重复自由显示名、名称长度和不可写基础路径。
- `dotnet test SimpleCities.sln --configuration Debug --no-restore`：400 通过、0 失败、0 跳过。
- `dotnet build SimpleCities.sln --configuration Debug --no-restore`：0 警告、0 错误。
- `godot --headless --path . --log-file .godot/qa-save-slot-identity.log --script tests/godot/pause_menu_runtime_contract.gd`：输出 `PASS pause menu runtime contract`，两轮 autosave 保存/加载通过；两条 `ConstructionDock` 缺少 `ToolManager.Instance` 警告来自既有隔离场景。
- 当前会话未提供 `csharp-ls` MCP，无法执行逐文件 C# LSP 诊断；只读 ACL 和真实 Windows 导出包仍由 `save-system:0.10` 后续验证。

---

<a id="save-system-bug-6"></a>
## BUG-6：后续存档文件损坏时前序系统已被恢复

> 修复日期：2026-08-04
> 关联事项：`save-system:0.11`

### 症状

一个槽位包含多个注册系统时，`SaveSlotStore.Load` 按文件顺序立即调用 `RestoreState`。如果后面的 JSON 缺失、语法损坏或 RoadGraph 引用非法，前面的系统已经修改运行时状态；加载最终虽然返回失败，却留下部分恢复结果。

### 根因分析

旧加载循环把文件存在性检查、读取、业务 schema 校验和状态提交混在同一次遍历中。RoadGraph 自身虽有失败原子性的临时模型，但 `SaveSlotStore` 没有在调用第一个恢复入口前证明整槽全部可用，因此无法把该保证提升到槽位层。

### 修复方案

新增 `IPreparedSaveable` 的准备/提交契约。`SaveSlotStore.Load` 先严格校验 manifest 元数据与必需文件集合，读取并解析全部 JSON，再调用全部准备入口；只有这些步骤全部成功后才进入提交循环。`RoadGraph` 复用完整的 Node/Edge/Group 临时模型，`MainCamera` 预先构造并校验 `CameraData`。普通扩展系统至少在恢复前经过整槽 JSON 语法预检。

### 影响范围

影响 SaveManager 注册系统的加载顺序与失败语义，不改变 JSON 业务字段。预检失败不再调用任何提交入口，也不会改变 `CurrentSlotID` 或写回槽目录。第二代仍不承诺多个未来业务系统在提交阶段抛出异常后的跨系统回滚；该剩余边界继续由 `save-system:0.9` 跟踪。

## BUG-6 验证状态

- `dotnet test tests/SimpleCities.RoadGraph.Tests/SimpleCities.RoadGraph.Tests.csproj --filter "FullyQualifiedName~SaveManagerSlotContractTests|FullyQualifiedName~SaveManagerManifestVersionTests|FullyQualifiedName~RoadGraphPersistenceV2Tests"`：55/55 通过，覆盖后续 JSON 损坏零恢复、准备失败零提交、RoadGraph 损坏引用状态/文件不变、manifest 文件表和有效往返。
- `dotnet test SimpleCities.sln --configuration Debug --no-restore`：415 通过、0 失败、0 跳过。
- `dotnet build SimpleCities.sln --configuration Debug --no-restore`：0 警告、0 错误。
- `godot --headless --path . --log-file .godot/qa-save-slot-prevalidation-final.log --script tests/godot/pause_menu_runtime_contract.gd`：输出预期的损坏 RoadGraph 加载错误及 `PASS pause menu runtime contract`；失败后 `CurrentSlotID` 保持手动槽，临时手动槽已删除，autosave 已成功重建。两条 `ConstructionDock` 警告来自既有隔离场景。
- 当前会话未提供 `csharp-ls` MCP，无法执行逐文件 C# LSP 诊断。

---

<a id="save-system-bug-7"></a>
## BUG-7：覆盖存档中途失败会留下新旧文件混合槽

> 修复日期：2026-08-04
> 关联事项：`save-system:0.9`

### 症状

覆盖已有槽位时，每个系统文件会依次替换。如果前面的文件已经写入，而后续系统在捕获、序列化或写盘时失败，旧 manifest 仍然可见，却指向部分新、部分旧的业务 JSON；加载可能得到跨保存时刻混合的城市状态。

### 根因分析

旧 `SaveSlotStore.Save` 直接在活动槽目录内逐文件写 `.tmp` 并替换正式文件，只把 manifest 放在最后写入。manifest 的延迟发布只能隐藏新槽的失败，无法回滚覆盖过程中已经替换的旧文件，也没有进程中断后的目录级恢复记录。

### 修复方案

保存现在先在内存中完成全部捕获与序列化，再将所有 JSON 和 manifest 写入同级 staging 目录。发布时把旧槽改名为 backup，再把完整 staging 改名为正式槽；任何发布异常都会把 backup 恢复为原槽。读、列举、存在性检查、删除和后续保存都会恢复中断留下的 backup 并清理 staging；保留事务目录不作为玩家槽列出。

### 影响范围

影响新建、覆盖、读取、列举和删除槽位的磁盘事务。活动槽的公开目录结构和 manifest 字段不变；成功发布后的 backup 清理失败不会把完整新槽误报为保存失败，残留会在下一次入口清理。

## BUG-7 验证状态

- `dotnet test tests/SimpleCities.RoadGraph.Tests/SimpleCities.RoadGraph.Tests.csproj --filter "FullyQualifiedName~SaveManagerSlotContractTests|FullyQualifiedName~SaveManagerManifestVersionTests"`：48/48 通过，覆盖序列化失败保留旧槽、旧槽移至 backup 后故障注入回滚、读入口恢复崩溃残留、事务目录隐藏和删除失败保护。
- `dotnet test SimpleCities.sln --configuration Debug --no-restore`：420 通过、0 失败、0 跳过。
- `dotnet build SimpleCities.sln --configuration Debug --no-restore`：0 警告、0 错误。
- `godot --headless --path . --log-file .godot/qa-save-slot-atomic-publish.log --script tests/godot/pause_menu_runtime_contract.gd`：不存在手动槽保存、非法槽删除和损坏 RoadGraph 加载均产生预期错误且不改变 `CurrentSlotID`；临时手动槽删除、autosave 重建后输出 `PASS pause menu runtime contract`。两条 `ConstructionDock` 警告来自既有隔离场景。
- 当前会话未提供 `csharp-ls` MCP，无法执行逐文件 C# LSP 诊断。

---

<a id="save-system-bug-8"></a>
## BUG-8：业务系统可使用保留名称覆盖槽位 manifest

> 修复日期：2026-08-10
> 来源：`docs/bugfix/session-2026-08-05.md#session-bug-03系统文件名-manifest-会覆盖存档-manifest`

### 症状

`ISaveable.SaveFileName` 为 `manifest` 时，业务 payload 与槽位元数据都会写入 `manifest.json`。后写入的 manifest 覆盖业务状态，文件表却仍把该路径当作业务文件，加载时会把槽位元数据交给业务系统。

### 根因分析

`GetDataFileName()` 允许生成保留路径 `manifest.json`，保存流程又在业务 payload 之后写 manifest。系统文件名校验和 manifest 文件表解析均没有保留名称规则。

### 修复方案

`SaveSlotStore.Save()` 在捕获任何业务状态、创建目录或改写旧槽之前，预先解析全部系统文件名并以大小写不敏感方式拒绝 `manifest.json`。`ValidateManifestFileName()` 同样拒绝外部 manifest 中的 `manifest.json` 及大小写变体，防止绕过保存入口构造冲突文件表。

### 影响范围

影响系统注册名称和 manifest 文件表校验。现有 `road_network`、`camera` 等名称、槽位 manifest 结构和正常存读档流程不变。

## BUG-8 验证状态

- `Save_RejectsReservedManifestNameBeforeCapturingState` 覆盖 `manifest` 与 `Manifest`，确认拒绝发生在 `CaptureState()` 和文件系统写入之前。
- `ParseAndValidateManifest_InvalidMetadataIsRejected` 覆盖外部 `Manifest.json` 变体。
- `dotnet test SimpleCities.sln --no-restore`：492/492 通过；`dotnet build SimpleCities.sln --no-restore`：0 警告、0 错误。

---

<a id="save-system-bug-9"></a>
## BUG-9：manifest 声明的业务文件缺失时槽位仍显示有效

> 修复日期：2026-08-10
> 来源：`docs/bugfix/session-2026-08-05.md#session-bug-04manifest-所列业务文件缺失时槽位仍被列为有效`

### 症状

槽位的 `manifest.json` 可解析、但文件表中的业务 JSON 已缺失时，`ListSlots()` 仍返回 `IsValid = true`。UI 会把不可加载的槽位展示为有效，直到用户实际加载才失败。

### 根因分析

列表入口只解析 manifest 元数据，没有执行加载入口已有的文件存在性检查，因此“可列为有效”和“可加载”使用了不同的完整性门槛。

### 修复方案

`ListSlots()` 在构造有效摘要前逐项检查 manifest 文件表：业务文件必须存在，且不能是文件系统链接。失败继续沿现有无副作用路径生成 `IsValid = false` 的摘要，不调用任何 `RestoreState()`。

### 影响范围

影响手动槽和 autosave 的列表有效性及错误信息。加载阶段的 schema 校验、状态准备/提交协议和损坏槽的保留策略不变。

## BUG-9 验证状态

- `ListSlots_MissingManifestDataFileMarksSlotInvalidWithoutRestoringState` 删除 `road_network.json` 后确认摘要无效、错误文本存在且恢复次数为 0。
- `pause_menu_runtime_contract.gd` 输出 `PASS pause menu runtime contract`，正常槽位的保存、列举和加载路径通过。
- 该运行时契约会按设计记录损坏槽加载失败并重写测试 autosave；这些输出不是本条回归失败。

---

<a id="save-system-bug-10"></a>
## BUG-10：仅大小写不同的系统文件名在 Windows 上相互覆盖

> 修复日期：2026-08-10
> 来源：`docs/bugfix/session-2026-08-05.md#session-bug-16仅大小写不同的系统文件名会相互覆盖`

### 症状

同时保存 `Economy` 与 `economy` 两个系统时，manifest 可列出两个名称，但 Windows 文件系统只保留一个物理 JSON；加载后两个系统都会得到后写入的状态。

### 根因分析

保存前唯一性、manifest 文件表、活动 `ISaveable` 注册与加载映射都使用大小写敏感比较，和目标文件系统的路径身份规则不一致。

### 修复方案

保存开始前使用 `StringComparer.OrdinalIgnoreCase` 验证完整文件表并拒绝冲突，且在捕获状态前完成。`SaveManager` 的活动注册冲突、manifest 重复文件检查以及 `SaveSlotStore.Load()` 的文件集和系统映射也统一为大小写不敏感比较，避免外部构造 manifest 在加载侧重现歧义。

### 影响范围

影响多业务系统注册、保存和加载时的文件身份判断。合法且唯一的系统名称、文件内容和 manifest schema 不变；冲突保存不会改写既有槽或留下 staging/backup 目录。

## BUG-10 验证状态

- `Save_RejectsCaseInsensitiveFileCollisionAndPreservesExistingSlot` 确认冲突系统均未捕获状态，既有槽逐文件保持不变且无事务目录残留。
- `ParseAndValidateManifest_CaseInsensitiveDuplicateFilesAreRejected` 确认外部大小写重复文件表被拒绝。
- `dotnet test SimpleCities.sln --no-restore`：492/492 通过；`dotnet build SimpleCities.sln --no-restore`：0 警告、0 错误。
- Roslyn CodeLens 解决方案诊断为 0 error、0 warning；Godot 4.7 editor 错误日志为 0。

---

<a id="save-system-bug-11"></a>
## BUG-11：缩略图 PNG 可接受非法 chunk type 与 reserved bit

> 修复日期：2026-08-14
> 影响文件：`Scripts/Core/V3PngValidator.cs`、`tests/SimpleCities.RoadGraph.Tests/V3PngValidatorTests.cs`
> 关联事项：`v3-save-system:2.2`

### 症状

可选存档缩略图的 PNG chunk type 包含非 ASCII 字母，或第三个字符把 PNG 保留位设为 1 时，旧验证器仍可能继续按字符串解释 chunk。该文件不是规范 PNG，却可能被当作可展示缩略图路径返回；业务 payload 本身仍完整，因此问题只影响缩略图完整性和占位回退判定。

### 根因分析

`V3PngValidator.ValidateEncodedPng()` 已校验 signature、chunk 长度、CRC、顺序、尺寸、像素和解码扫描线，但读取 4-byte chunk type 后没有先执行 PNG 结构位规则。CRC 只能证明 type/data 未意外改变，不能证明 type 的四个字节都是字母，也不能证明第三个字节的 reserved bit 为 0。

### 修复方案

新增 `ValidateChunkType()` 并在读取每个 chunk 后、CRC 和语义分派前调用：四个字节必须全部属于 ASCII `A-Z` 或 `a-z`，第三个字节必须为大写字母。违规输入抛出 `InvalidDataException`，上层继续沿既有缩略图 warning/占位路径处理，不把可选展示资产错误提升为业务槽损坏。

### 影响范围

只收紧 V3 可选 PNG 缩略图的容器校验。合法 critical/ancillary chunk、业务 `manifest.json`、`road_network.json`、aggregate digest 和 Load/Publish/Delete 语义不变。

## BUG-11 验证状态

- `V3PngValidatorTests.ValidateEncodedPng_RejectsInvalidChunkTypeOrReservedBit` 构造 `a0Bc` 非字母 type 与 `abcD` 非法 reserved bit，两者均在验证阶段拒绝。
- 保存/manifest/PNG/persistence/export 聚焦测试：118/118 通过；完整 `dotnet test SimpleCities.sln --no-restore`：698/698 通过。
- `dotnet build SimpleCities.sln --configuration Debug --no-restore` 与 `--configuration ExportRelease`：均为 0 警告、0 错误。
- Windows Desktop QA 导出包的可写与只读 ACL 存档契约均输出 PASS；缩略图修复未改变业务槽的发布、加载或删除行为。Godot 全库 GDScript 诊断无 error，另有 3 条与本修复无关的既有 warning。

---

<a id="save-system-bug-12"></a>
## BUG-12：场景或应用退出可能在异步存档操作收敛前销毁参与者

> 修复日期：2026-08-14
> 影响文件：`Scripts/Core/SaveManager.cs`、`Scripts/Core/SaveOperationCoordinator.cs`、`Scripts/UI/GameHUD.cs`、`Scripts/UI/MainMenu.cs`、`Scripts/UI/PauseMenu.cs`
> 关联事项：`v3-save-system:2.3`；UI 焦点伴随修复见 `ui:BUG-16`

### 症状

保存、加载或删除已经进入异步流程时，返回主菜单、窗口关闭和退出到桌面原先没有共用存档收敛边界。场景切换可能先移除 `RoadGraph`、`ToolManager` 或 `RoadRenderer`，应用退出也可能先销毁 `SaveManager`；同时，busy 期间合并的 autosave 仍可能在旧场景关闭后被唤醒。结果是未越过提交边界的任务继续引用失效场景，或已越界的目录事务来不及完成发布/恢复。

### 根因分析

场景与应用生命周期只表达“立即切换/退出”，没有把 scene generation、操作取消点和 coordinator shutdown 纳入同一协议。缺少 scene-closing admission gate、按场景 generation 跟踪的完成任务以及应用级 await；因此 `GetTree().ChangeSceneToFile(...)`、窗口关闭和 `GetTree().Quit()` 无法证明后台任务已经取消或收敛。

### 修复方案

`SaveManager` 为每个公开操作跟踪 token、scene generation、取消源和完成任务。`BeginSceneClose()` 先关闭 admission、丢弃 pending autosave、取消当前场景 token，并请求取消尚未越过提交边界的活动操作；新请求分别返回 `RejectedSceneClosing` 或 `RejectedShuttingDown`。

返回主菜单时，`GameHUD.ReturnToMainMenu()` 先设置退出收敛状态并 `await DrainCurrentSceneOperationsAsync()`，只有当前 generation 的任务全部结束后才切换场景；切换失败则显式恢复 scene admission。应用退出时，`SceneTree.AutoAcceptQuit` 被关闭，窗口关闭、暂停菜单和 `MainMenu` 都路由到 `RequestApplicationQuit()`；该入口同时等待 `SaveOperationCoordinator.BeginShutdownAsync()` 与全部 tracked operation。未越界操作取消，已越界操作完成同一事务后才调用 `GetTree().Quit()`。

### 影响范围

影响 V3 Save/Load/Delete/autosave 的场景关闭和应用退出生命周期，以及暂停菜单退出期间的输入禁用。磁盘 format、descriptor/digest 恢复矩阵和操作自身的不可取消点不变。完整 surface/hit-index 与六分量 presentation token 尚未完成，因此 `v3-save-system:2.3` 仍保持开放。

## BUG-12 验证状态

- `SaveOperationCoordinatorTests.Shutdown_CancelsUncommittedOperationWaitsAndRejectsNewAdmission` 验证 shutdown 会取消未越界操作、等待完成并拒绝新请求；`Shutdown_WaitsForCommittedOperationRejectsWaiterAndDropsPendingAutosave` 验证已越界操作必须收敛、等待者被拒绝且 pending autosave 被丢弃。
- `PauseMenuContractTests.ExitFlows_DrainSceneOperationsAndRouteApplicationQuitThroughSaveManager` 锁定场景切换前 drain、窗口关闭接管、pending autosave 丢弃和 awaited coordinator shutdown，禁止恢复 fire-and-forget 退出。
- `dotnet test SimpleCities.sln --no-restore`：720/720 通过；Debug 与 `ExportRelease` build 均为 0 警告、0 错误；Roslyn compiler/analyzer diagnostics 均为 0。
- `godot --headless --path . --log-file .godot/qa-v3-exit-convergence.log --script tests/godot/pause_menu_runtime_contract.gd`：输出 `PASS pause menu runtime contract`，覆盖暂停菜单确认返回主菜单、场景注销、重新进入和后续 V3 Save/Load；日志只有契约预期的缺依赖/损坏槽 warning，没有退出收敛错误。
- 当前验证把 coordinator 的越界语义与真实场景退出分别覆盖；尚未用每个磁盘故障注入点逐一触发“退出发生在该点”的完整运行时矩阵，因此不据此关闭 `v3-save-system:2.3`。

---

<a id="save-system-bug-13"></a>
## BUG-13：等待根 gate 的请求在取消竞争中返回占锁 lease 并阻塞收敛

> 修复日期：2026-08-14
> 影响文件：`Scripts/Core/SaveOperationCoordinator.cs`、`tests/SimpleCities.RoadGraph.Tests/SaveOperationCoordinatorTests.cs`
> 关联事项：`v3-save-system:2.3`

### 症状

完整测试并行运行时，`SceneStyleDrain_DiscardsPendingCancelsWaitersAndRemainsReusable` 在前 718 项通过后可能不再结束。场景取消一个正在等待 `_rootGate` 的手动请求，同时活动 operation 释放 gate；等待请求偶尔返回 `SaveOperationLease`，而不是 Admission 阶段的 `Canceled`。调用方按取消结果结束流程后，这个意外 lease 仍持有 coordinator 的活动操作，测试断言失败后的 `DisposeAsync()` 和真实 scene drain 都会继续等待它，表现为无响应。

### 根因分析

`SemaphoreSlim.WaitAsync(cancellationToken)` 只保证在等待尚未成功完成时响应取消。gate 释放与外部 token 取消并发发生时，await 可以先按“成功取得 gate”返回，而 token 随即进入已取消状态。旧 `AdmitManualAsync()` 在 await 后只检查 `_stopping`，没有在 `CreateLeaseLocked()` 前重新检查调用方 token，因此把已经取消的 waiter 提升成新的活动 lease。

### 修复方案

`AdmitManualAsync()` 记录 gate 是否已取得，并在进入 coordinator 锁后首先重新检查外部 cancellation token。若已取消，则释放刚取得的 `_rootGate`，返回 `SaveOperationPhase.Admission` 的结构化 `Canceled` 结果，不创建 lease；随后才检查 shutdown 状态并处理正常 admission。回归测试在失败诊断路径也会先终结任何意外 lease，确保未来断言失败不会再次把测试进程伪装成永久无响应。

### 影响范围

影响手动 Save、Load、Delete 等等待进程内根 gate 时与 scene cancellation/shutdown 竞争的 admission。已活动 operation 的提交边界、跨进程根锁、pending autosave 合并、Publish/Delete 磁盘事务和成功 lease 生命周期不变。

## BUG-13 验证状态

- 修复前完整套件在 718 项通过后停住；聚焦回归捕获到 waiter 获得已取消 lease。修复后 `SceneStyleDrain_DiscardsPendingCancelsWaitersAndRemainsReusable` 返回 `Canceled`、无 lease，并继续证明 coordinator 可被下一场景复用。
- `dotnet test SimpleCities.sln --no-restore`：727/727 在约 2 秒内通过；Debug 与 `ExportRelease` build 均为 0 警告、0 错误；Roslyn compiler/analyzer 为 0 diagnostics。
- `pause_menu_runtime_contract.gd` 输出 `PASS pause menu runtime contract`；场景取消、退出收敛及后续 Save/Load 路径没有新增错误。日志中的损坏测试槽和缺失 `ToolManager` warning 为契约预期或既有隔离场景输出。
- 本修复只关闭等待取消竞争，不补齐 `RoadSurfaceSnapshot`、完整 `RoadRenderToken`、第二 saveable 或逐关键资源故障矩阵，因此 `v3-save-system:2.3` 保持开放。

---

<a id="save-system-bug-14"></a>
## BUG-14：未提交的道路表现资源没有确定性释放

> 修复日期：2026-08-20
> 影响文件：`Scripts/Road/RoadRenderer.cs`、`Scripts/Road/RoadRenderer.LoadCommit.cs`、`tests/SimpleCities.RoadGraph.Tests/RoadRendererLifecycleContractTests.cs`
> 关联事项：`v3-save-system:2.3`、`v3-grid-rendering:2.2`

### 症状

普通道路表现构建在创建 `ArrayMesh` 或 `MultiMesh` 后若被更新 token 取代、后续创建失败或交换前抛错，未挂载资源只会离开局部变量；Load Preflight 已创建的隐藏 mesh/node batch 若后续步骤失败，或完整 plan 在 commit 前被取消/失效，原 `RoadRendererLoadCommitPlan.Dispose()` 也只释放 admission。对应 Godot Resource/RID 必须等待托管 GC 才释放，连续失败或取消可在运行期间累积不可见原生资源。

### 根因分析

资源工厂、普通 rebuild 和 Load plan 之间没有明确的所有权转移状态。`CreateRoadMesh()` 与 `CreateNodeBatch()` 在构造中途抛错时不负责清理已经创建的 Godot Resource；`PreflightPreparedLoad()` 在 mesh 创建后继续绑定 token/snapshot/plan，却没有异常回收；未提交 plan 的 `Dispose()` 只归还 renderer admission，没有释放自己持有的 `_roadMesh` 与 `_nodeBatch`。

### 修复方案

普通 `TryRebuildStaticBatches()` 现在在 try 外跟踪两个 prepared resource，只有 mesh/node batch 都挂载到表现层后才标记 ownership transferred；其余返回或异常路径由 `finally` 统一释放。`CreateRoadMesh()` 和 `CreateNodeBatch()` 各自捕获构造中途异常并释放已创建资源，node marker 的 `QuadMesh` 也在赋给 batch 后立即释放局部引用。

Load Preflight 在 plan 成功接管资源前捕获全部后续异常并释放 mesh/node batch。`RoadRendererLoadCommitPlan` 增加幂等 `_disposed` 门禁；未 commit 的 plan 在 `Dispose()` 中先释放隐藏表现资源，再在 `finally` 归还 admission。成功 `CommitReferences()` 后资源已经转交 `MeshInstance2D`/`MultiMeshInstance2D`，plan 的完成或重复释放不再销毁已挂载资源。

### 影响范围

影响普通道路表现失败、Load Preflight 异常及未提交 aggregate plan 的 Godot Resource 生命周期，不改变 V3 payload、RoadGraph、surface 数据、render token 或成功 commit 的可见结果。当前切片只建立资源所有权与清理基线；每个关键 Preflight 故障点、真实 generation 失配及 observer/cleanup 联合矩阵仍由开放路线图继续覆盖。

## BUG-14 验证状态

- `dotnet test tests/SimpleCities.RoadGraph.Tests/SimpleCities.RoadGraph.Tests.csproj --no-restore --filter FullyQualifiedName~RoadRendererLifecycleContractTests`：10/10 通过，锁定普通 build 转交前 cleanup、两个资源工厂的异常自清理、Preflight 异常回收、未提交 plan 释放、重复 Dispose 幂等及成功 commit 后不释放已挂载资源。
- `dotnet test SimpleCities.sln --no-restore`：846/846 通过；Debug 与 `ExportRelease` build 均为 0 警告、0 错误。
- 隔离 `APPDATA` 的 `road_renderer_lifecycle_runtime_contract.gd` 与 `road_render_token_runtime_contract.gd` 均输出 PASS；后者验证普通 mutation、stalled/retry 和连续 Load 的成功资源转交保持可用。生命周期故障注入脚本主动移除 renderer 后仍产生其既有的 `RoadBuilder` 查询 disposed renderer 错误，因此不把该脚本记作干净的 editor/DAP 错误通道。
- 隔离用户目录和日志均已清理，原有 Godot PID `74652` 未受影响。当前会话未暴露 Roslyn CodeLens、Godot MCP 或 DAP，focused semantic diagnostics、editor bridge 与 DAP console 均未记为通过。

---

<a id="save-system-bug-15"></a>
## BUG-15：已提交的 aggregate Load 因后续辅助异常被误报为失败

> 修复日期：2026-08-24
> 影响文件：`Scripts/Core/SaveManager.cs`、`tests/SimpleCities.RoadGraph.Tests/RoadRendererLifecycleContractTests.cs`、`tests/godot/RoadLoadObserverFailureProbe.cs`、`tests/godot/road_load_observer_cleanup_runtime_contract.gd`
> 关联事项：`v3-save-system:2.3`

### 症状

aggregate Load 已通过 `aggregate.Commit(aggregateOperationLease)` 联合提交 RoadGraph、道路表现和工具状态后，如果槽位列表失效或性能指标等提交后辅助工作抛出异常，公开结果仍返回 `SaveOperationResultKind.Failed`，同时 `Committed = true`。调用方因此收到互相矛盾的失败结果，但运行时实际上已经切换到目标槽，不能安全重试或回滚。

### 根因分析

`SaveManager.RunLoadAsync()` 的通用 `catch` 同时覆盖提交前准备、`aggregate.Commit()` 和提交后的槽位列表/性能指标更新。联合提交一旦完成就已经越过不可逆边界；后续辅助异常落入同一个失败分支，会丢失“目标状态已经生效”的终态语义。

### 修复方案

`aggregate.Commit()` 成功后先保留参与者返回的 warning，再用独立 `try/catch` 隔离槽位列表失效、性能指标和相同级别的提交后辅助工作。该区域抛出的异常追加 `Load post-commit work failed: ...` warning，并以 `SucceededWithWarnings` 完成 lease；没有 warning 时仍返回 `Succeeded`。提交前异常继续沿原有 `Failed` 分支，不改变 admission、preflight 或联合提交门禁。

Debug-only probe 在 commit 后、辅助工作前注入一次性异常；真实连续 Load 回归同时证明 warning 结果保留已提交状态、全部参与者解除 admission，下一次干净 Load 可再次成功。

### 影响范围

只调整 V3 aggregate Load 越过联合提交边界后的结果分类和诊断信息。Save、Delete、提交前 Load 失败、payload/manifest、参与者 commit 顺序和成功 Load 的状态内容不变；Debug probe 不进入 `ExportRelease` 产物。

## BUG-15 验证状态

- 修复前新增聚焦源码契约 1/1 失败；真实 Godot 故障注入返回 `resultKind = 2`、`committed = true` 和 `Injected aggregate Load post-commit work failure.`，确认矛盾终态可复现。
- `RoadRendererLifecycleContractTests`：73/73 通过；`dotnet test SimpleCities.sln --configuration Debug --no-restore`：948/948 通过。
- `dotnet build SimpleCities.sln --configuration Debug --no-restore` 与 `--configuration ExportRelease --no-restore`：均为 0 警告、0 错误；production/test Roslyn compiler 与 analyzer diagnostics 均为 0。
- `tests/godot/road_load_observer_cleanup_runtime_contract.gd` 的 GDScript `--check-only` 退出码为 0；Godot 4.7 CLI 真实运行退出码为 0，输出 `post_commit_warning_result_kind=1`、`post_commit_clean_result_kind=0`、`post_commit_trigger_count=1` 和 `PASS road load observer cleanup runtime contract`。首次行为已 PASS 但退出时触发既有托管 finalizer 访问冲突，测试清理复用 `FlushPendingManagedFinalizers()` 后复跑干净退出。
- Debug/`ExportRelease` 隔离检查确认 `ArmNextAggregateLoadPostCommitFailure` 分别出现 1/0 次，`RoadLoadObserverFailureProbe` 分别出现 3/0 次；测试探针未进入发布程序集。
- 当前 Godot editor MCP 未连接，Godot LSP 与 DAP 未运行，因此 editor log、LSP 和 DAP 门未刷新，未记为通过。

---

<a id="save-system-bug-16"></a>
## BUG-16：场景切换保存根后旧删除授权仍可用于同名槽位

> 修复日期：2026-09-12
> 影响文件：`Scripts/Core/SaveManager.cs`、`tests/godot/SceneStoragePolicyProbe.cs`、`tests/godot/scene_storage_authorization_runtime_contract.gd`、`SimpleCities.csproj`、`export_presets.cfg`
> 关联事项：GitHub #4（V4-03 收拢存档协调器）的提交前评审

### 症状

V4-03 尚未提交的场景存储策略实现中，在保存根 A 获取槽位摘要并授权删除，随后绑定保存根 B，若 B 存在槽位 ID 和内容摘要均相同的副本，旧摘要仍可重新授权，旧授权也能删除 B 的槽位。该问题在提交前评审发现，仅使用隔离临时目录中的克隆槽位复现，不属于此前已发布版本的回归，也未删除真实用户存档。

### 根因分析

场景重新装配已切换操作所用的保存根，但注册和注销未推进 `_slotListGeneration`，也未清除待删除授权。删除校验依赖列表代际、槽位 ID、内容摘要及操作 token；两根含相同槽位时，这些条件不足以识别授权来自旧场景。

### 修复方案

`RegisterSceneLoad()` 安装新上下文后，以及 `UnregisterSceneLoad()` 开始关闭旧场景后，统一调用既有 `InvalidateSlotListing()`，同时推进列表代际并清除待删除授权。旧摘要不能重新授权，旧 token 也不能启动删除。

新增真实 Godot 回归通过公开列表及删除入口验证两个拒绝条件，并检查两根的槽位均保留。测试探针仅在 Debug 编译，探针和脚本均从 QA 导出资源排除；清理时恢复默认 V3 装配，并验证临时根的绝对路径前缀后删除本测试目录。

### 影响范围

修复场景存储策略注册与注销边界上的删除授权失效行为。保存和加载继续使用请求捕获的保存根；当前生产路径仍为 V3，没有提前接入 V4 存储。

## BUG-16 验证状态

- Godot 4.7 Mono 使用 `--rendering-method forward_plus --rendering-driver vulkan --audio-driver Dummy --script res://tests/godot/scene_storage_authorization_runtime_contract.gd`：修复前退出 1，旧摘要与旧授权均未拒绝，B 槽位被删除；修复后退出 0，两个拒绝条件和两槽保留均为 true，并输出明确 PASS。stderr 的 stale authorization 错误为本回归预期拒绝。
- `dotnet test SimpleCities.sln --no-restore --verbosity minimal`：最终 965/965 通过。首次收尾发现新脚本遗漏 QA 导出排除，补齐后原有导出契约通过。
- Debug 构建及 `dotnet build SimpleCities.csproj --configuration ExportRelease --no-restore --verbosity minimal` 均为 0 警告、0 错误；Roslyn compiler/analyzer diagnostics 为 0。程序集检查确认 `SceneStoragePolicyProbe` 在 Debug 存在，在 ExportRelease 不存在。
- 修复后再次运行 `road_load_generation_runtime_contract.gd`：Forward+/Vulkan 退出 0，输出 PASS；存储装配切换未破坏既有加载代际契约。测试进程已退出，临时槽位已清理。
- Godot MCP 编辑器桥接被另一客户端占用，编辑器桥接与 DAP 检查未执行；真实行为依据独立引擎进程验证。日志中已有初始化 ToolManager 提示及两份旧存档时间戳警告，未修改这些旧存档。
- Standards 和 Spec 双线复核均无剩余发现。红/绿及加载回归原始证据见 `.scratch/v4-03-qa/`。
