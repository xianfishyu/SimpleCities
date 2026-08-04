# 存档系统当前参考

> 状态：第二代当前实现参考 | 最后核对：2026-08-04

本文记录当前可运行的第二代存档契约。源码和自动化输出是最终事实来源；未来工作只在“扩展边界与已知限制”中说明，不与已实现能力混写。

## 0. 文档责任与导航

本文是存档系统的唯一详细参考入口。其它文档只保留角色化摘要，并链接回本文。

| 文档 | 责任 |
|---|---|
| `docs/reference/save-system-plan.md` | 当前槽位、manifest、道路 schema、事务、UI、自动存档和扩展边界。 |
| `docs/reference/class-reference.md` | 类与 API 速查。 |
| `docs/reference/game-logic.md` | 游戏流程中的高层保存/加载路径。 |
| `docs/manuals/infrastructure-guide.md` | 基础设施接入约定。 |
| `docs/manuals/road-system-v2-gen.md` 附录 D | 第二代最终产品范围。 |
| `docs/todo/save-system.md` | 长期待办、未完成验收和已解决基线。 |
| `docs/bugfix/save-system.md` | 已验证 bug 修复记录。 |

## 1. 第二代范围与当前状态

第二代提供多个玩家命名的手动槽和一个独立自动槽。存档的唯一业务载荷是 `RoadGraph` 的 `road_network.json`；人口、资金和缩略图只有 manifest 元数据位置，当前使用明确占位值。`MainCamera` 仍实现并注册 `ISaveable`，但不在第二代持久化配置中，因此不会写入或恢复 `camera.json`。

| 能力 | 当前状态 |
|---|---|
| 命名手动槽 | 支持自由显示名、同名槽、另存为、列表、覆盖、加载和删除。 |
| 自动槽 | 保留内部 ID `autosave`，场景内默认每 300 秒覆盖一次，不切换当前手动槽。 |
| 槽位发布 | 全部文件和 manifest 通过同级 staging/backup 目录整槽发布；失败恢复旧槽。 |
| 加载 | 先完成 manifest、全部必需文件、JSON 和临时模型预检，再提交 RoadGraph。 |
| 版本 | manifest、RoadGraph 和每个原生几何段都只接受精确当前版本。 |
| 旧存档 | 不迁移；旧 Junction/Segment/Road、RoadType 或缺失版本 payload 安全拒绝。 |
| 第二代业务数据 | 只有道路网络；相机、经济、人口、时间等运行状态不进入槽位。 |

## 2. 当前架构

| 组件 | 责任 |
|---|---|
| `SaveManager` | Autoload 适配层；维护注册生命周期、选择 V2 保存配置、更新 `CurrentSlotID` 并输出 Godot 日志。 |
| `SaveSlotStore` | 不依赖 Godot Node 的文件边界；管理路径、manifest、整槽发布/恢复、列举和递归删除。 |
| `ISaveable` | 定义稳定 `SaveFileName`、状态捕获和 JSON 恢复入口。 |
| `IPreparedSaveable` | 在不修改运行时状态的前提下准备恢复模型，再由统一提交阶段应用。 |
| `RoadGraph` | 当前 V2 唯一业务系统；捕获并严格恢复 Node/Edge/Group 与原生几何。 |
| `AutosaveController` | `MapTest` 场景内的周期调度器；调用 `SaveManager.SaveAutosave()`。 |
| `PauseMenu` | 命名槽管理 UI；显示摘要并确认覆盖、加载和删除。 |

`RoadSystem` 和 `MainCamera` 在进入/离开场景树时注册和注销。相同实例可幂等注册；不同活动对象不能使用相同 `SaveFileName`。注册表是扩展机制，不等于所有注册对象都会进入当前 V2 槽。

### 2.1 SaveManager API

| API | 当前行为 |
|---|---|
| `Register/Unregister` | 维护活动 `ISaveable`，拒绝文件名冲突。 |
| `Save(slotID)` | 覆盖已存在槽并在成功后选择该槽；首次创建只允许保留的 `autosave`。 |
| `SaveAs(displayName)` | 生成独立 `manual-<GUID>`，保存后选择新手动槽。 |
| `SaveAutosave()` | 覆盖保留自动槽，但不改变 `CurrentSlotID`。 |
| `Load(slotID)` | 整槽预检成功后恢复道路，并选择加载的槽。 |
| `SaveSlotExists(slotID)` | 检查恢复后的槽目录是否含 manifest。 |
| `ListSlots()` | 只读取目录和 manifest，返回有效及损坏槽摘要。 |
| `DeleteSlot(slotID)` | 递归删除有效或损坏的非空槽；删除当前槽后回到 `autosave`。 |

## 3. 槽位身份、名称与目录

内部槽位 ID、玩家显示名和文件路径是三个独立概念。

| 概念 | 规则 |
|---|---|
| 自动槽 ID | 固定为 `autosave`。 |
| 手动槽 ID | `manual-` 加无连字符 GUID，例如 `manual-0123456789abcdef0123456789abcdef`。 |
| 内部 ID 字符 | 只允许 ASCII 字母、数字、`_`、`-`。 |
| 显示名 | 非空白，最多 128 个 UTF-16 字符；可含中文、空格、重复名称和路径字符。 |
| 身份判定 | 自动/手动只按内部 ID 判定，不按显示名推断。 |
| 路径边界 | 槽必须是存档根的直接子目录，不能解析到根外，也不能是重解析点/文件系统链接。 |

因此玩家可以创建显示名同为“自动存档”的手动槽；它仍使用独立手动 ID，不会被周期自动保存覆盖。

编辑器使用全局化的 `res://saves`。导出版本使用 Godot 全局化后的 `user://saves`，在 Windows 上位于当前 profile 的 `Godot/app_userdata/SimpleCities/saves`，不依赖可执行文件所在目录可写。一个典型目录如下：

```text
<save-root>/
├── autosave/
│   ├── manifest.json
│   └── road_network.json
├── manual-0123456789abcdef0123456789abcdef/
│   ├── manifest.json
│   └── road_network.json
├── .autosave.staging/    # 仅事务期间存在，不进入列表
└── .autosave.backup/     # 仅覆盖/恢复期间存在，不进入列表
```

## 4. `manifest.json`

当前写出的逻辑形状如下；时间值每次保存都会变化。

```json
{
  "schemaVersion": 1,
  "slotId": "autosave",
  "displayName": "自动存档",
  "timestamp": "2026-08-04T00:00:00.0000000Z",
  "cityName": "Unknown City",
  "population": null,
  "funds": null,
  "thumbnailFile": null,
  "files": [
    "road_network.json"
  ]
}
```

| 字段 | 当前契约 |
|---|---|
| `schemaVersion` | 必须是数值 `1`；字段名区分大小写，缺失、旧版、未来版和错误类型均拒绝。 |
| `slotId` | 必须与所在目录名精确一致。 |
| `displayName` | 玩家可见名称；遵守 1～128 字符边界，但不参与路径计算。 |
| `timestamp` | `DateTime.UtcNow.ToString("O")`；必须能解析为 UTC 且偏移为零。 |
| `cityName` | 当前固定占位 `Unknown City`，不能为空白。 |
| `population` / `funds` | 当前为 `null`，表示尚无业务数据源。 |
| `thumbnailFile` | 当前为 `null`；非空时只接受槽内直接文件，缺失、越界或链接降级为无缩略图。 |
| `files` | 安全、无重复的系统 JSON 文件名；V2 必须包含 `road_network.json`。 |

`ListSlots()` 不加载道路数据。有效槽按保存时间倒序，同一时间按内部 ID 排序；损坏槽位于有效槽之后并携带诊断，不阻断其它槽。UI 对暂无人口、资金和缩略图显示占位信息。

## 5. 保存与整槽发布

`Save`、`SaveAs` 和 `SaveAutosave` 使用同一文件事务：

1. 从注册表精确选择当前 V2 配置要求的 `road_network`；缺少必需系统时在写盘前失败。
2. 对全部选中系统执行 `CaptureState()` 并在内存中完成 JSON 序列化；任一失败都不会创建可见槽。
3. 恢复该槽可能遗留的 staging/backup 事务目录。
4. 在同级 `.<slotID>.staging` 中写完全部系统 JSON 和 `manifest.json`。
5. 覆盖时把旧槽移动为 `.<slotID>.backup`，再把完整 staging 目录移动为正式槽。
6. 发布成功后清理 backup；发布失败时恢复旧槽并清理 staging。

`Exists`、`ReadManifest`、`Load`、`ListSlots`、`Delete` 和后续 `Save` 都会先恢复可识别的中断事务。若正式槽缺失则恢复 backup；若正式槽完整则保留正式槽并清理旧 backup。事务目录不会显示为玩家槽。

保存成功后的槽只包含同一次捕获产生的一组文件。失败不会把新旧 `road_network.json` 与 manifest 混成一个可见槽，也不会破坏上一份有效 autosave。

## 6. 加载与失败原子性

当前 `Load(slotID)` 流程：

1. 校验内部 ID并恢复中断发布。
2. 读取 manifest，验证版本、槽 ID、UTC 时间、显示名和文件表。
3. 确认当前 V2 要求的 `road_network.json` 已列入 manifest 且实际存在。
4. 读取全部目标 JSON，并先用 `JsonDocument` 验证语法。
5. 对 `IPreparedSaveable` 调用 `PrepareRestoreState(json)`；RoadGraph 在此阶段构造完整临时图并执行全部语义校验。
6. 所有准备成功后调用 `RestorePreparedState(...)` 提交。
7. 仅在全部完成后更新 `CurrentSlotID`。

RoadGraph 准备阶段至少校验：大小写敏感的 schema、未知字段、全局 ID 唯一性、非负引用、有限坐标、端点与 Group 存在性、Group/Edge 双向成员一致、无孤立节点、原生几何版本/类型/参数、几何连续性、几何端点与 Node 一致，以及 `nextID` 大于全部实体 ID。

任何读取、JSON 或准备失败都不会清空活动图、改变 ID 分配状态、触发 `GraphCleared` 或切换当前槽。成功提交会重建节点邻接和空间索引，再触发 `GraphCleared` 让渲染层全量重建。

当前 V2 只有 RoadGraph 一个业务提交。框架会先准备未来多个系统，但提交阶段还没有跨系统回滚；引入第二个正式持久化系统时必须重新开启该事务边界。

## 7. 命名槽 UI 与自动存档

`PauseMenu` 的单一存档管理视图提供名称输入、槽位列表、摘要和操作按钮。

| 槽状态 | 允许操作 |
|---|---|
| 有效手动槽 | 覆盖、加载、删除；每个破坏性操作先显示目标摘要并确认。 |
| 有效自动槽 | 与手动槽一样可加载、覆盖或删除，但列表和确认文案明确标识“自动”。 |
| 损坏槽 | 显示诊断，禁用覆盖和加载，只允许确认删除。 |

取消覆盖不会写文件；取消加载不会改变 `CurrentSlotID` 或活动道路；取消删除不会移除目录。列表行、摘要和确认文案都按内部 ID显示“自动”或“手动”。

`AutosaveController` 挂载在 `MapTest`，默认 `IntervalSeconds = 300`，可通过导出属性配置正有限周期。内部 `Timer` 继承场景树暂停状态，因此暂停菜单打开时不推进；离开游戏场景后随节点释放。控制器还提供显式启停、立即触发、成功/失败计数和 `AutosaveCompleted(bool)` 信号。周期保存只调用 `SaveAutosave()`，不会覆盖或选择玩家当前手动槽。

## 8. `road_network.json`

当前道路根 schema 只使用 Node、Edge 和 Group 词汇。下面示例是一条直线 Edge 的完整逻辑形状：

```json
{
  "schemaVersion": 1,
  "nextID": 4,
  "nodes": [
    { "id": 0, "x": 0.0, "y": 0.0 },
    { "id": 1, "x": 8.0, "y": 3.0 }
  ],
  "edges": [
    {
      "id": 2,
      "nodeAID": 0,
      "nodeBID": 1,
      "groupID": 3,
      "geometry": [
        {
          "version": 1,
          "kind": "line",
          "start": { "x": 0.0, "y": 0.0 },
          "control1": null,
          "control2": null,
          "end": { "x": 8.0, "y": 3.0 },
          "startTangent": null,
          "endTangent": null,
          "center": null,
          "radius": null,
          "startAngle": null,
          "sweepAngle": null,
          "startHeading": null,
          "startCurvature": null,
          "endCurvature": null,
          "arcLength": null,
          "startWeight": null,
          "controlWeight": null,
          "endWeight": null
        }
      ]
    }
  ],
  "groups": [
    { "id": 3, "edgeIDs": [2] }
  ]
}
```

每个 Edge 的 `geometry` 至少包含一个连续原生几何段。几何段 `version` 当前固定为 `1`，`kind` 与参数必须精确匹配：

| `kind` | 必需参数 |
|---|---|
| `line` | `start`、`end` |
| `cubicBezier` | `start`、`control1`、`control2`、`end` |
| `cubicHermite` | `start`、`startTangent`、`end`、`endTangent` |
| `circularArc` | `center`、`radius`、`startAngle`、`sweepAngle` |
| `clothoid` | `start`、`startHeading`、`startCurvature`、`endCurvature`、`arcLength` |
| `rationalQuadratic` | `start`、`startWeight`、`control1`、`controlWeight`、`end`、`endWeight` |

根对象、Node、Edge、Group 和几何对象都拒绝未知字段。序列化会把当前类型未使用的已知几何字段写为 `null`；加载拒绝这些字段携带非 `null` 的多余参数。`junctions`、`segments`、`roads`、`fromJunctionID`、`waypoints`、`totalLength`、`roadID`、`type` 和 RoadType 回退都不属于当前 schema。

## 9. 版本与旧数据策略

| 层级 | 当前版本 | 不兼容处理 |
|---|---:|---|
| `manifest.json` | `schemaVersion = 1` | 缺失、旧版、未来版、错误类型或错误大小写字段拒绝。 |
| `road_network.json` | `schemaVersion = 1` | 同上，并拒绝未知根/实体字段。 |
| 原生几何段 | `version = 1` | 缺失、未知版本、未知 `kind`、缺失/多余参数或非法几何拒绝。 |

第二代没有旧道路存档迁移。旧 `version/junctions/segments/roads` payload 不会尝试映射到新模型，也不会把缺失 RoadType 静默回退为 Street。未来改变任何一层契约时必须提升对应版本并明确选择迁移或拒绝策略。

## 10. 添加新可存档系统

注册新 `ISaveable` 只会让它进入活动注册表；不会自动改变第二代槽内容。正式扩展 V2 之后的持久化配置时需要同时完成：

1. 使用稳定、安全且唯一的 `SaveFileName`，生成独立 `<name>.json`。
2. 用纯 DTO 捕获状态，不直接序列化 Godot Node 或缓存对象。
3. 实现 `IPreparedSaveable`，在准备阶段完成全部解析和校验，提交阶段只应用已验证模型。
4. 把文件名显式加入新的保存配置；不要修改 `RoadGraph` DTO 来容纳其它系统。
5. 更新 manifest 文件表、版本策略、加载失败场景和槽级自动化。
6. 在第二个正式业务系统进入同一槽时，设计跨系统提交失败的回滚或无失败提交保证。
7. 更新本文、类参考、游戏流程和系统待办。

未来加入 `economy.json`、`population.json` 等文件时，每个系统继续拥有独立 schema。道路 schema 不因新增系统而变化。

## 11. 验证状态

截至 2026-08-04：

| 证据 | 结果 |
|---|---|
| 存档版本、槽位、预检、发布、元数据和导出路径聚焦测试 | `SaveManagerSlotContractTests` 与 `SaveManagerManifestVersionTests` 合计 53/53 通过。 |
| `dotnet test SimpleCities.sln --configuration Debug --no-build --no-restore` | 474/474 通过。 |
| `dotnet build SimpleCities.sln --configuration Debug --no-restore` | 0 警告、0 错误。 |
| `tests/godot/autosave_runtime_contract.gd` | 输出 `PASS autosave runtime contract`；覆盖周期、隔离、失败保护与加载。 |
| `tests/godot/pause_menu_runtime_contract.gd` | 输出 `PASS pause menu runtime contract`；覆盖命名槽、确认/取消、损坏槽和小视口。 |
| Windows QA 导出包可写 profile | 输出 `PASS exported save writable user data contract`；中文/路径字符显示名、内部 ID、manifest、RoadGraph-only 文件和清理通过。 |
| Windows QA 导出包只读 ACL profile | 对实际 `user://saves` 添加拒绝写入 ACE 后输出 `PASS exported save read-only ACL contract`；失败不切换槽位，移除 ACL 后目录为空。 |
| 正式 Windows 导出预设 | 不包含 `tests/`、`saves/` 或 `docs/`，短启动继续加载 `MapTest`。 |
| `csharp-ls --diagnose --solution SimpleCities.sln --loglevel warning` | 退出码 0，成功加载解决方案且未报告诊断；当前仍没有逐文件 C# LSP/MCP 通道。 |
| Godot/GDScript | 导出契约逐文件诊断为空；独立 editor scan 加载当前项目，用户编辑器 error buffer 为空。 |

## 12. 已知限制与后续边界

| 限制 | 当前影响 |
|---|---|
| 元数据来源 | 城市名为 `Unknown City`，人口、资金和缩略图为暂无；尚未接入真实城市系统或截图生成。 |
| 自动存档配置入口 | 周期是场景导出属性，当前没有玩家设置 UI。 |
| 多系统提交回滚 | 当前 V2 只有 RoadGraph；新增第二个正式业务系统前必须补齐跨系统提交失败语义。 |
| 旧存档 | 明确不兼容，不提供迁移工具。 |

可执行后续工作与完成证据以 `docs/todo/save-system.md` 为准。
