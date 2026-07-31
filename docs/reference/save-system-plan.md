# 存档系统当前参考

> 状态：当前实现参考 + 未来目标 | 最后核对：2026-07-31

本文记录当前可运行的存档系统，而不是旧提案。除“未来目标”章节外，所有描述都以当前源码为准。

## 0. 文档责任与导航

本文是存档系统的唯一详细参考入口。其它文档只保留角色化摘要，并链接回本文。

| 文档 | 责任 |
|---|---|
| `docs/reference/save-system-plan.md` | 当前存档契约、运行时事实、验证状态、已知限制与未来目标的主参考。 |
| `docs/reference/class-reference.md` | 类与 API 的速查索引，只保留接口级摘要。 |
| `docs/reference/game-logic.md` | 游戏流程概览，只保留存档流程的高层视图。 |
| `docs/manuals/infrastructure-guide.md` | 基础设施说明，只保留实现边界和维护提示。 |
| `docs/todo/save-system.md` | 存档系统的长期待办与未完成验收。 |
| `docs/bugfix/save-system.md` | 存档系统已验证修复记录。 |
| `docs/bugfix/` | 已验证修复记录索引，不承担当前契约说明。 |

## 1. 验证状态

以下状态以本次复核记录和当前源码为准。

| 项目 | 状态 |
|---|---|
| 编辑器保存到 `res://saves` | 已在运行时验证。 |
| 槽名仅允许 ASCII 字母、数字、`_` 和 `-` | 已在运行时验证。 |
| 当前仅注册并保存 `RoadGraph` 和 `MainCamera` | 已按注册路径和运行时生成文件核对。 |
| 场景退出会注销 `RoadGraph` 和 `MainCamera` | 已通过 `MapTest -> MainMenu -> MapTest` 运行时契约验证：主菜单为 0 个，新城市为 2 个，并可继续存读档。 |
| 每个文件先写 `.tmp` 再替换正式文件 | 已按源码核对。 |
| 整个存档槽的原子事务 | 不存在，当前未实现。 |
| 加载顺序为逐文件顺序恢复，失败后不回滚已恢复对象 | 已按源码核对，失败场景尚无自动化回归。 |
| `schemaVersion` 与 `RoadGraph.version` 未做强制版本分派或拒绝 | 已按源码核对。 |
| `DeleteSlot` 对非空槽目录的实际删除结果 | 未验证。 |
| 导出可执行文件旁的 `saves` 行为 | 未验证，原因是 Godot 4.7 Mono export templates 缺失。 |
| `dotnet build SimpleCities.sln` | 已通过。 |

## 2. 当前架构

存档入口是 `Scripts/Core/SaveManager.cs`。它是 `SaveManager` Autoload 单例，维护一个运行时 `_saveables` 列表。子系统实现 `Scripts/Core/ISaveable.cs` 后，在初始化时调用 `SaveManager.Instance.Register(this)` 加入保存和加载流程，并在离开场景树时调用 `Unregister(this)`。不同活动对象不能占用同一个 `SaveFileName`。

当前公开 API 为：

| API | 当前行为 |
|---|---|
| `Register(ISaveable saveable)` | 同一对象可幂等注册；不同活动对象使用相同 `SaveFileName` 时拒绝并返回 `false`。 |
| `Unregister(ISaveable saveable)` | 移除离开场景树的对象；对象不存在时安全返回 `false`。 |
| `Save(string slotName = "autosave")` | 保存所有已注册对象，成功返回 `true`，异常时记录错误并返回 `false`。 |
| `Load(string slotName = "autosave")` | 读取 manifest 后按已注册对象匹配文件并依次恢复，成功返回 `true`。 |
| `SaveSlotExists(string slotName)` | 检查该槽位下是否存在 `manifest.json`。 |
| `DeleteSlot(string slotName)` | 当前调用 `DirAccess.RemoveAbsolute(slotDir)` 删除槽目录。 |
| `CurrentSlotName` | 记录最近成功保存或加载的槽名，默认是 `autosave`。 |
| `RegisteredSaveableCount` | 公开当前注册数量，供运行时生命周期契约检查。 |

`ISaveable` 当前接口如下：

```csharp
public interface ISaveable
{
    string SaveFileName { get; }
    object CaptureState();
    void RestoreState(string json);
}
```

`RestoreState` 接收的是原始 JSON 字符串。每个实现自己用 `SaveJson.Deserialize<T>()` 反序列化为对应 DTO。

当前实际注册并保存的实现只有两个：

| 实现 | 注册位置 | 文件名 |
|---|---|---|
| `RoadGraph` | `Scripts/Road/RoadSystem.cs` 创建 `Graph` 后注册，`_ExitTree()` 注销 | `road_network.json` |
| `MainCamera` | `Scripts/MainCamera.cs` 的 `_Ready()` 注册，`_ExitTree()` 注销 | `camera.json` |

不要把旧 `RoadNetwork` 当成活动模型。当前道路数据层是 `RoadGraph`、`GraphNode`、`GraphEdge` 和 `RoadGroup`。

## 3. 存档目录和文件

当前保存根目录按运行环境分流：Godot 编辑器版本通过 `ProjectSettings.GlobalizePath("res://saves")` 写入项目目录；导出版本通过 `OS.GetExecutablePath()` 定位可执行文件所在目录，并写入其旁边的 `saves`。默认槽位结构是：

```text
<save-root>/autosave/
├── manifest.json
├── road_network.json
└── camera.json
```

槽名由调用方传入。当前 `GameHUD` 的保存和加载都使用固定槽名 `autosave`。

槽名当前只允许 ASCII 字母、数字、`_` 和 `-`，因此不能包含路径分隔符、`.`、空白或绝对路径前缀。导出版本采用用户指定的可执行文件旁存档方案；游戏若安装在只读目录（例如受保护的 `Program Files`）会保存失败，因此发布形式需要保证游戏目录可写。

## 4. 保存流程

`Save(slotName)` 当前流程：

1. 校验槽名，并根据运行环境计算 `<save-root>/<slotName>`：编辑器使用全局化的 `res://saves`，导出版本使用可执行文件旁的 `saves`。
2. 用 `DirAccess.MakeDirRecursiveAbsolute(slotDir)` 确保槽目录存在。
3. 遍历 `_saveables`。
4. 对每个对象调用 `CaptureState()`，再用 `SaveJson.Serialize(state)` 生成格式化 JSON。
5. 每个子系统先写 `<file>.json.tmp`，写完后删除旧 `<file>.json`，再把 `.tmp` 移动为正式文件。这只保护单个文件，不是整槽原子提交。
6. 收集本次写出的文件名。
7. 最后写 `manifest.json`。
8. 更新 `CurrentSlotName`。

`.tmp` 保护只作用于单个子系统文件。当前实现没有整个槽位级事务，也没有 staging 目录或回滚清单。如果保存中途失败，已经替换成功的某些子系统文件会留在槽里，`manifest.json` 只有在所有子系统文件写完后才会重新写入。换句话说，单文件写入降低了半写文件风险，但不保证整个存档槽原子更新。

`manifest.json` 本身当前直接写最终文件，没有 `.tmp` 替换步骤，也不参与整槽事务。

## 5. 加载流程

`Load(slotName)` 当前流程：

1. 校验槽名，解析当前运行环境的 `<save-root>/<slotName>`，并检查槽目录是否存在。
2. 检查并读取 `manifest.json`。
3. 反序列化为 `ManifestData`。
4. 从 `manifest.files` 建立文件集合。
5. 遍历当前 `_saveables`，只把 `SaveFileName + ".json"` 出现在 manifest 中的对象加入加载映射。
6. 按映射逐个读取对应 JSON 文件。
7. 调用对应对象的 `RestoreState(json)`。
8. 全部 dispatch 完成后更新 `CurrentSlotName`。

加载是顺序执行的。当前没有整个槽位级回滚，也没有先验证所有子系统再提交的两阶段恢复。如果某个已匹配文件缺失，或某个 `RestoreState` 抛出异常，`Load` 会返回 `false`，但之前已经成功恢复的子系统不会自动回滚到加载前状态。

manifest 中列出但当前未注册的文件会被忽略。当前已注册对象如果不在 manifest 文件清单中，也不会加载。

## 6. JSON 工具和通用 manifest

`Scripts/Core/SaveJson.cs` 统一使用 `System.Text.Json`：

```csharp
private static readonly JsonSerializerOptions Options = new()
{
    WriteIndented = true,
    PropertyNameCaseInsensitive = true
};
```

当前 manifest DTO 在 `Scripts/Core/SaveData.cs`：

```json
{
  "schemaVersion": 1,
  "slotName": "autosave",
  "timestamp": "2026-07-19T00:00:00.0000000Z",
  "cityName": "My City",
  "files": [
    "road_network.json",
    "camera.json"
  ]
}
```

字段说明：

| 字段 | 当前来源和含义 |
|---|---|
| `schemaVersion` | `ManifestData.SchemaVersion` 默认值，当前为 `1`。加载时尚未按该值做版本分派或拒绝。 |
| `slotName` | 保存时传入的槽名。 |
| `timestamp` | `DateTime.UtcNow.ToString("O")`。 |
| `cityName` | DTO 默认值 `"My City"`，当前没有 UI 或城市系统写入真实城市名。 |
| `files` | 本次保存成功写出的子系统 JSON 文件名列表。 |

## 7. `road_network.json`

活动实现是 `RoadGraph`，文件名是 `road_network.json`。它的运行时类型已经是 Node、Edge、Group，但 JSON 字段名继续沿用兼容名称 `junctions`、`segments`、`roads`。

当前 `RoadGraph.CaptureState()` 写出的 payload 版本是 `2`：

```json
{
  "version": 2,
  "nextID": 42,
  "junctions": [
    { "id": 1, "x": 0.0, "y": 0.0 }
  ],
  "segments": [
    {
      "id": 2,
      "fromJunctionID": 1,
      "toJunctionID": 3,
      "roadID": 4,
      "waypoints": [
        { "x": 64.0, "y": 0.0 }
      ],
      "totalLength": 128.0,
      "type": 1
    }
  ],
  "roads": [
    {
      "id": 4,
      "segmentIDs": [2],
      "type": 1
    }
  ]
}
```

字段说明：

| JSON 字段 | 当前运行时含义 |
|---|---|
| `version` | `RoadGraphSaveData.Version`，当前写出 `2`。加载时尚未拒绝未知版本。 |
| `nextID` | `RoadGraph` 的下一个实体 ID。恢复后还会用当前最大 ID 修正，避免小于已加载实体。 |
| `junctions` | 兼容命名，表示 `GraphNode` 列表。 |
| `segments` | 兼容命名，表示 `GraphEdge` 列表。 |
| `roads` | 兼容命名，表示 `RoadGroup` 列表。 |
| `segments[].type` | Edge 的 `RoadType` 整数值。nullable，旧存档缺失时回退。 |
| `roads[].type` | Group 的 `RoadType` 整数值。nullable，旧存档缺失时回退。 |

恢复流程在 `RoadGraph.RestoreState(string json)` 中完成：

1. 反序列化 `RoadGraphSaveData`。
2. `ClearGraph()` 清空 `_nodes`、`_edges`、`_groups`、`_nodeRefs`、`_edgeRefs` 和 `_spatialIndex`。
3. 把 `_nextID` 设为存档中的 `nextID`。
4. `RestoreFromSavedData(data)` 先恢复 `GraphNode`，再恢复 `RoadGroup`，最后恢复 `GraphEdge`。
5. `RebuildNodeEdges()` 根据 Edge 两端节点重建节点邻接表。
6. `RebuildSpatialIndex()` 清空并重建节点与边途经点的空间索引。
7. `EnsureNextIDBeyondLoadedEntities()` 确保 `_nextID` 大于已加载 Node、Edge、Group 的最大 ID。
8. 触发 `GraphCleared`。

`GraphCleared` 是加载后通知渲染层重建显示的当前机制。`RoadRenderer.SetGraph(Graph)` 订阅该事件，收到后清空现有线条并按 `GetAllEdges()` 全量重建。

兼容规则：

| 情况 | 当前行为 |
|---|---|
| v2 存档有 `segments[].type` | Edge 使用该类型。 |
| Edge 缺少 `type` 但所属 Group 有 `type` | Edge 使用 Group 类型。 |
| Edge 和 Group 都缺少 `type` | 回退为 `RoadType.Street`。 |
| 缺少或未知 `version` | 当前没有显式版本拒绝或迁移分派。 |
| 缺失端点、重复 ID、悬空引用 | 当前没有预校验和失败保护，后续图状态取决于恢复过程。 |

## 8. `camera.json`

`MainCamera` 的文件名是 `camera.json`。当前保存位置、缩放值，并在加载时恢复 `Position`、`nextPos` 和 `defaultScale`。

当前 JSON：

```json
{
  "positionX": 500.0,
  "positionY": 300.0,
  "zoom": 1.5
}
```

字段说明：

| 字段 | 当前含义 |
|---|---|
| `positionX` | `MainCamera.Position.X`。 |
| `positionY` | `MainCamera.Position.Y`。 |
| `zoom` | 内部 `defaultScale`，加载后由 `_Process()` 中的缩放插值反映到 `Camera2D.Zoom`。 |

## 9. 玩家触发点

`Scripts/UI/GameHUD.cs` 负责当前用户入口：

| 入口 | 当前行为 |
|---|---|
| 暂停菜单保存 | 调用 `SaveManager.Instance.Save("autosave")`。 |
| 暂停菜单读档 | 调用 `SaveManager.Instance.Load("autosave")`。 |

因此当前可观察行为是暂停菜单将当前 RoadGraph 和 MainCamera 保存到 `autosave`，或从同一槽读取。没有手动槽选择 UI，也没有多槽列表 UI。

## 10. 添加新可存档系统

新增系统时按当前框架接入：

1. 定义只包含 JSON 数据的 DTO，避免直接序列化 Godot 节点或带循环引用的运行时对象。
2. 在系统类实现 `ISaveable`。
3. 让 `SaveFileName` 返回不含扩展名的稳定文件名。
4. 在 `CaptureState()` 中返回 DTO。
5. 在 `RestoreState(string json)` 中调用 `SaveJson.Deserialize<T>()`，再恢复运行时状态。
6. 在系统初始化完成后调用 `SaveManager.Instance.Register(this)`，并处理重复 `SaveFileName` 导致的 `false` 结果。
7. 在对象离开场景树时调用 `SaveManager.Instance.Unregister(this)`，重复注销可以安全忽略。
8. 如果恢复后有缓存、邻接关系、空间索引、渲染对象或事件订阅，必须在 `RestoreState` 中重建或发出明确事件。
9. 设计 JSON 字段名时优先保持现有存档兼容。重命名运行时类型不等于可以重命名已经写出的 JSON 字段。

## 11. 已知限制

当前限制不是未来目标的完成状态，不能写成已实现能力：

| 限制 | 当前风险 |
|---|---|
| 导出版本写入可执行文件旁 | 便于绿色版携带存档，但游戏目录只读时保存会失败；不适用于要求沙盒用户目录的平台。 |
| 子系统文件有 `.tmp`，manifest 没有 | 子系统单文件降低半写风险，manifest 仍是直接覆盖。 |
| 没有整个槽位事务 | 保存失败可能留下新旧文件混合的槽位。 |
| 加载没有整体回滚 | 某个子系统加载失败时，之前恢复成功的子系统不会自动回到加载前。 |
| 版本只写不管 | manifest `schemaVersion` 和 RoadGraph `version` 目前没有显式迁移或未知版本拒绝。 |
| RoadGraph 恢复缺少预校验 | 悬空引用、重复 ID、非法枚举等损坏数据没有统一失败保护。 |
| 删除槽目录实现有限 | `DeleteSlot` 直接调用 `DirAccess.RemoveAbsolute(slotDir)`，对非空目录的实际删除能力需要再验证。 |
| 当前只保存 RoadGraph 和 MainCamera | 时间、分区、经济、人口等系统尚未接入。 |

## 12. 未来目标

以下是下一步实现目标，不是当前实现。优先级按数据安全和路线图依赖排序。

1. 明确版本策略。为 manifest schema 和 RoadGraph payload 建立版本分派，当前 v2 走确定路径，缺少版本的旧数据走兼容路径，未知未来版本给出可诊断失败。
2. 增加 RoadGraph 恢复校验和失败保护。加载前校验端点、Group/Edge 双向引用、重复 ID、枚举值和 `NextID`，全部通过后再替换当前图，失败时保留加载前状态。
3. 固化发布路径兼容边界。用真实 Windows 导出包验证可执行文件旁的 `saves`，定义只读安装目录失败提示，并在支持沙盒平台前重新评估平台专用路径策略。
4. 加强槽位写入原子性。考虑槽级 staging 目录、manifest `.tmp` 替换、提交标记或旧槽回滚，避免保存失败后出现混合槽位。
5. 建立加载事务边界。先读取和验证所有要加载文件，再统一提交到各子系统，或让每个子系统提供可回滚的临时恢复路径。
6. 校准活动 schema 文档和测试。把 `junctions`、`segments`、`roads` 作为兼容字段写入回归测试，避免运行时命名迁移破坏旧 JSON。
