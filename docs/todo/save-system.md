# 存档系统待办清单

> 系统 key：`save-system`
> 复核日期：2026-08-03
> 证据：`Scripts/Core/SaveManager.cs`、`Scripts/Core/SaveData.cs`、`Scripts/Road/RoadGraph.Persistence.cs`、当前存档测试及 `docs/manuals/road-system-v2-gen.md` 附录 D。
> 主导原则：第二代提供多个玩家命名的道路网络存档；道路数据独立 JSON，其他系统以后按相同注册机制扩展，但不纳入第二代保存内容。

## 状态总览

<a id="save-system2"></a>
<a id="save-system11"></a>
<a id="save-systemroadtype"></a>
<a id="save-systemsavesystem"></a>

| ID | 发现 | 当前状态 | 处置方式 |
|---|---|---|---|
| 0.3 | V2 道路 JSON 曾保存 RoadType 并包含旧格式回退 | 已完成 | 运行时、schema 和旧 DTO 均已移除类型与回退字段 |
| 0.4 | 路网和 manifest 没有严格版本拒绝 | 部分完成 | RoadGraph 已精确拒绝版本；manifest 仍待收敛 |
| 0.5 | RoadGraph 恢复前缺少完整引用校验 | 已完成 | 临时解析、全量校验后一次提交 |
| 0.8 | SaveManager 场景注册生命周期 | 已完成 | 保留注册和注销基线 |
| 0.9 | SaveManager 缺少稳定自动化契约 | 未完成 | 建立道路槽位成功与失败测试 |
| 0.10 | 文件夹槽名与玩家显示名称混为一体 | 部分完成 | 分离安全内部 ID 和玩家可命名显示名 |
| 0.11 | 加载会在完整预检前调用 RestoreState | 未完成 | 先预检 manifest 和道路 JSON，再提交 RoadGraph |
| 1.1 | 没有可列举的命名存档目录与完整元数据 | 未完成 | 建立存档目录 API 和 manifest 元数据 |
| 1.2 | 暂停菜单只固定保存/加载 autosave | 未完成 | 实现另存为、覆盖确认、加载和删除工作流 |
| 1.3 | 没有独立自动存档策略 | 未完成 | 自动槽不得覆盖玩家命名存档 |
| 1.4 | 当前注册对象会把镜头等状态写入同一槽 | 未完成 | 第二代只持久化 RoadGraph，同时保留未来独立 JSON 扩展点 |
| 5.3 | 活动道路 schema 仍使用 Junction/Segment/Road 旧字段 | 已完成 | 新 V2 schema 直接使用 Node/Edge/Group，不迁移旧存档 |
| 6.3 | 存档参考文档与最终 V2 范围不一致 | 未完成 | 同步命名槽、元数据、新 schema 和失败语义 |

### 设计覆盖矩阵

<a id="save-system111e2827dafb"></a>

| 设计范围 | 当前事实 | 关联待办 |
|---|---|---|
| 多命名存档 | SaveManager 接受 slotName，但没有列举 API、显示名模型或管理界面 | 0.9～0.10、1.1～1.3 |
| 道路网络持久化 | RoadGraph 已使用严格 Node/Edge/Group schema 和事务式恢复；manifest 与整槽预检仍待完成 | 0.3～0.5、0.11、5.3 |
| 可扩展边界 | ISaveable 注册和 manifest 文件列表已存在；当前场景同时注册 RoadGraph 与相机 | 0.8、1.4 |
| 原生曲线 | RoadGraph 已原生往返六类几何，并完成交点、重叠和参数化拆分 | `road-graph:2.5`～`road-graph:2.6`、5.3 |

## 执行顺序

### 阶段 0：建立新 V2 道路存档契约

<a id="save-system0.3"></a>

- [x] **0.3 从第二代道路存档移除 RoadType 数据**
  - 当前问题：Edge 和 Group JSON 写入 Type，恢复时还为旧存档回退 Street；道路分级已明确移至第三代。
  - 修改：第二代 RoadGraph API 和 JSON 不保存、不恢复 RoadType；第三代以后通过新 schema 版本重新引入。
  - 依赖：`road-graph:2.7`。
  - 集成负责人：`save-system`。
  - 测试：新道路存档不包含类型字段；保存加载不依赖默认 Street；现有含 Type 的旧文件按不兼容版本拒绝。
  - 验收：第二代 schema 中不存在道路分级字段或兼容回退分支。
  - 完成证据（2026-08-03）：`RoadGraphSaveData`、Edge 和 Group DTO 不包含 `RoadType`、`type` 或旧格式类型回退；`GraphEdge`、`RoadGroup`、兼容 `AddRoad` 和恢复链也不再持有类型。`Scripts/Core/SaveData.cs` 已删除无人引用的旧 `RoadNetworkData`、`JunctionData`、`SegmentData`、`RoadData` 和 `Vector2Data`，避免遗留 DTO 保留错误契约。源码边界测试锁定道路生产源码与存档 DTO 无 `RoadType` 且旧类型文件不存在；相关聚焦测试 98/98、解决方案测试 359/359、构建 0 警告/0 错误、Godot 主场景两轮 autosave 运行契约通过。含旧字段的 payload 继续由严格 schema 未映射字段拒绝。
  - 来源 key：`todo:item:0.3`（已按 2026-08-02 范围决定取代原 RoadType 往返回归）。

<a id="save-system0.4"></a>

- [ ] **0.4 固化只接受新第二代格式的版本策略**
  - 当前问题：RoadGraph 写出 version，manifest 写出 schemaVersion，但加载没有统一版本分派或拒绝规则。
  - 修改：为新的 V2 manifest 和 road graph schema 设定明确版本；缺少版本、旧版本和未知未来版本都返回可诊断失败，不执行迁移。
  - 测试：当前版本、缺少版本、旧版本、未来版本及版本字段类型错误。
  - 验收：只有精确支持的版本进入数据校验；不兼容存档不会调用 RoadGraph 恢复。
  - 当前进展（2026-08-03）：RoadGraph schema 使用 `schemaVersion = 1`，缺失、旧版、未来版和错误字段类型均在任何图变更前以 `JsonException` 拒绝；旧 `version/junctions/segments/roads` payload 不迁移。manifest 的精确版本拒绝仍未实现，因此本项保持开放。
  - 来源 key：`todo:item:0.4`。

<a id="save-system0.5"></a>

- [x] **0.5 为 RoadGraph 恢复增加引用与曲线参数校验**
  - 当前问题：RestoreState 会先清空当前图，再信任 Node、Edge、Group 和 waypoint 数据。
  - 修改：先解析临时模型，校验 ID 唯一性、端点、邻接/Group 引用、NextID、有限数值、几何段类型和控制参数，全部通过后一次替换当前图。
  - 依赖：使用 `road-graph:2.5` 已完成的几何序列化契约；加载事务已独立验证，不再依赖删除事务 `road-graph:4.1`。
  - 集成负责人：`save-system`。
  - 测试：缺失端点、重复 ID、悬空引用、非法曲线类型、NaN/Infinity、退化曲线和错误 NextID。
  - 验收：任何损坏存档失败后，加载前的道路拓扑、曲线参数和 ID 分配状态完全不变。
  - 完成证据（2026-08-03）：`RoadGraph.Persistence.cs` 在临时 Node/Edge/Group 字典中校验全局 ID 唯一性、有限坐标、端点与 Group 引用、双向 Group 成员关系、孤立节点、原生几何版本/类型/参数、段连续性、节点端点和 `nextID`，全部成功后才清空并一次提交。`RoadGraphPersistenceV2Tests` 覆盖重复/跨类型 ID、悬空引用、成员不一致、非法与退化几何、非有限坐标、连续性/端点错误和错误 `nextID`；失败后序列化状态与 `GraphCleared` 计数保持不变。解决方案测试 204/204、构建 0 警告/0 错误，Godot 主场景暂停菜单契约两轮保存加载通过。
  - 来源 key：`todo:item:0.5`。

<a id="save-system0.8"></a>

- [x] **0.8 SaveManager 注册生命周期已绑定场景退出。** Register/Unregister 和重复文件名拒绝已经存在，后续 1.4 只调整第二代实际保存范围。
  - 来源 key：`todo:item:0.8`。

<a id="save-system0.9"></a>

- [ ] **0.9 建立 SaveManager 道路槽位自动化契约测试**
  - 当前问题：保存、加载、manifest、临时文件替换、列举和失败返回缺少统一自动化入口。
  - 修改：以隔离目录和测试 RoadGraph 覆盖 Save、Load、SaveSlotExists、ListSlots、DeleteSlot、CurrentSlotID、manifest 与失败清理。
  - 测试：成功保存/加载、manifest 缺失、道路 JSON 缺失、序列化失败、恢复失败、重复显示名和删除失败。
  - 验收：单条命令稳定运行全部槽位契约；失败不会伪造成功日志、改变当前槽位或留下可见半成品。
  - 来源 key：`todo:item:0.9`。

<a id="save-system0.10"></a>

- [ ] **0.10 分离内部槽位 ID、玩家存档名称和存储路径**
  - 当前问题：slotName 同时充当文件夹名和显示名，只接受 ASCII 字母、数字、下划线和连字符，无法安全支持玩家自由命名。
  - 修改：使用不可冲突的安全内部 ID 作为目录名，将玩家输入名称写入 manifest；所有目录操作验证目标位于存档根目录内，并确认编辑器和导出版本的可写路径。
  - 测试：中文、空格、同名存档、超长名称、路径字符、空名称、只读目录和真实 Windows 导出包。
  - 验收：合法玩家名称不直接成为文件路径；任何名称都不能越过存档根目录；不可写位置明确失败。
  - 来源 key：`todo:item:0.10`。

<a id="save-system0.11"></a>

- [ ] **0.11 在修改 RoadGraph 前完成槽位预检**
  - 当前问题：Load 读取 manifest 后立即逐个调用 RestoreState，缺少完整道路文件和元数据预检。
  - 修改：先读取并校验 manifest、版本、道路文件存在性、JSON 可解析性和 RoadGraph 临时模型，再执行一次提交；第二代不要求多个业务系统之间的回滚事务。
  - 测试：manifest/道路文件缺失、版本错误、损坏 JSON、损坏引用、缩略图缺失和全部有效。
  - 验收：任何预检失败都不会改变当前道路图、当前槽位或存档目录；有效存档完整加载。
  - 来源 key：`todo:item:0.11`。

### 阶段 1：多个命名存档体验

<a id="save-system1.1"></a>

- [ ] **1.1 建立可列举的存档目录和元数据模型**
  - 当前问题：只有 SaveSlotExists，没有按时间排序的槽位列表；manifest 只有 slotName、timestamp、cityName 和 files。
  - 修改：提供 ListSlots，并记录内部 ID、存档名称、UTC 保存时间、城市名称、人口、资金、缩略图引用和文件列表；无实际系统数据时城市名称、人口和资金使用明确占位值。
  - 测试：零个/多个存档、同名显示名、损坏 manifest、缺失缩略图、排序稳定性和占位字段。
  - 验收：存档列表无需加载 RoadGraph 即可安全读取全部摘要；单个损坏槽不会阻断其他槽显示。

<a id="save-system1.2"></a>

- [ ] **1.2 实现命名保存、另存为、覆盖确认、加载和删除工作流**
  - 当前问题：暂停菜单固定调用 autosave，没有存档列表、名称输入、覆盖确认或删除确认。
  - 修改：增加存档管理界面并接入 1.1；新名称创建独立槽，选中已有槽可明确覆盖，加载和删除都显示目标摘要；删除必须安全移除非空槽目录。
  - 依赖：`save-system:0.9`～`save-system:0.11`、`save-system:1.1`。
  - 集成负责人：`save-system`。
  - 测试：新建、另存为、同名处理、确认/取消覆盖、加载、确认/取消删除、损坏槽提示和键盘/鼠标操作。
  - 验收：玩家可管理多个命名存档；任何破坏性操作都不会在未确认时发生。

<a id="save-system1.3"></a>

- [ ] **1.3 实现独立自动存档槽**
  - 当前问题：autosave 只是暂停菜单硬编码名称，没有独立触发规则或与手动槽隔离的契约。
  - 修改：定义可配置的自动存档触发周期；自动槽使用保留内部 ID，并在列表中明确标识，绝不覆盖玩家命名手动槽。
  - 依赖：`save-system:1.1`、`save-system:1.2`。
  - 集成负责人：`save-system`。
  - 测试：首次自动保存、周期触发、手动同名显示、自动保存失败和加载自动槽。
  - 验收：自动存档可识别、可加载且与手动存档隔离；失败不影响最近一次有效自动存档。

<a id="save-system1.4"></a>

- [ ] **1.4 将第二代保存内容限定为 RoadGraph 并保留扩展接口**
  - 当前问题：SaveManager 会保存全部已注册对象，当前场景还注册相机；这超出“第二代只保存道路网络”的范围。
  - 修改：第二代槽只要求 road graph JSON；相机和其他系统不作为 V2 验收数据。保留每系统独立 SaveFileName 和 manifest 文件列表，使未来系统可新增独立 JSON。
  - 测试：保存后必有且仅要求道路 JSON；加载只恢复道路，不改变未纳入的相机或其他状态；注册未来测试系统时生成独立文件且不修改道路 schema。
  - 验收：第二代保存/加载的业务状态只有道路网络；扩展新系统无需修改 RoadGraph DTO。

### 阶段 5：新 schema 命名

<a id="save-system5.3"></a>

- [x] **5.3 让新第二代道路 schema 使用 Node、Edge 和 Group 命名**
  - 当前问题：活动 JSON 仍使用 junctions、segments、roads、FromJunctionID 和 RoadID 等旧字段。
  - 修改：新 V2 schema 直接使用 nodes、edges、groups 和对应 ID 字段，并保存原生曲线段类型及控制参数；不提供旧 JSON 字段迁移。
  - 依赖：`road-graph:2.5`、`save-system:0.4`。
  - 集成负责人：`save-system`。
  - 测试：新 schema 往返、旧字段文件拒绝、曲线参数往返和未知几何段拒绝。
  - 验收：新存档公共语义统一，旧存档明确失败而不是部分读取。
  - 完成证据（2026-08-03）：新 JSON 只使用 `schemaVersion`、`nextID`、`nodes`、`edges`、`groups`、`nodeAID`、`nodeBID`、`groupID`、`geometry` 和 `edgeIDs`；Edge 的每个原生几何段使用版本化 `RoadGeometryData` 保存类型与控制参数。测试完成 line、cubic Bézier、cubic Hermite、circular arc、clothoid、rational quadratic 六类语义往返，并拒绝旧字段与未知几何类型。解决方案测试 204/204、构建 0 警告/0 错误。
  - 来源 key：`todo:item:5.3`。

### 阶段 6：同步存档文档

<a id="save-system6.3"></a>

- [ ] **6.3 同步命名槽、元数据和新道路 schema 文档**
  - 修改：更新 `docs/reference/save-system-plan.md` 及相关导航，描述内部槽位 ID、显示名、元数据占位、自动槽、道路唯一业务载荷、新版本拒绝和未来独立 JSON 扩展边界。
  - 依赖：`save-system:0.3`～`save-system:1.4`、`save-system:5.3`。
  - 验证：文档示例 JSON 与实际 manifest/road graph 自动化输出逐字段一致。
  - 验收：文档不再宣称旧存档兼容、RoadType 往返或第二代保存相机。
  - 来源 key：`todo:item:6.3`。

## 暂不执行

### 第三代道路分级存档

- RoadType 数据、样式、选择和升级全部在第三代以新 schema 版本重新设计；不得复用第二代的兼容回退作为既定契约。

### 后续多系统事务

- 第二代保留独立 JSON 扩展机制，但只验收 RoadGraph。多个业务系统同时提交和失败回滚的整槽事务在新增第二个正式持久化系统时重新开启。

## 已解决基线

<a id="save-system878b6f92c0cc"></a>

- [x] **SaveManager 已支持 ISaveable 注册/注销和每系统独立文件名。**
- [x] **单文件保存使用临时文件后替换。** 后续修改必须继续保证失败不会把半写入 JSON 暴露为有效槽位。
- [x] **RoadGraph 损坏 payload 不得改变活动图。** 恢复必须先完成 schema、引用、成员关系、几何和 `nextID` 全量校验，再一次提交并发出 `GraphCleared`。
- [x] **第二代道路 JSON 使用 Node/Edge/Group 与原生几何参数。** 不得重新写入旧 Junction/Segment/Road、waypoint、长度或 RoadType 字段。

## 完成标准

<a id="save-systemaf4fd4e8bade"></a>

1. 0.3～0.5、0.9～0.11、1.1～1.4、5.3 和 6.3 全部通过自动化与 Godot 运行时验证。
2. 玩家可以创建、列出、覆盖、加载和删除多个命名手动存档；自动存档与手动存档隔离。
3. 列表包含存档名称、保存时间、城市名称、人口、资金和缩略图，暂无来源的数据使用明确占位值。
4. 第二代只恢复道路网络；旧、缺失版本、未来版本和损坏存档均安全拒绝且不改变当前道路图。
5. RoadType 和旧存档迁移不属于第二代完成条件。
