# 持久化系统方案

> 状态：待确认 | 最后更新：2026-05-30

---

## 0. 设计决策

| 决策 | 选择 | 理由 |
|------|------|------|
| 架构 | `ISaveable` 接口 + `SaveManager` Autoload | 框架先行，各子系统渐进接入，不耦合 |
| 文件格式 | **JSON** | 标准格式，零依赖，`System.Text.Json` 原生支持 |
| 文件组织 | **分文件**，每子系统一个 `.json` | 独立读写减少锁冲突；单文件损坏不影响其他系统 |
| 版本兼容 | 暂不做 | 开发阶段 schema 变动频繁，旧档直接报错提示 |

---

## 1. 存档目录结构

```
user://saves/
├── {slot_name}/
│   ├── manifest.json         ← 存档元信息 + 清单
│   ├── road_network.json     ← 路网数据
│   ├── camera.json           ← 相机状态
│   ├── time.json             ← 时间系统（Phase 3）
│   ├── zones.json            ← 分区系统（Phase 2）
│   └── ...                   ← 其他子系统
```

`slot_name` 当前固定为 `autosave`，未来扩展手动存档槽。

---

## 2. 核心接口

```csharp
// Scripts/Core/ISaveable.cs
public interface ISaveable
{
    /// <summary>存档文件名（不含扩展名），如 "road_network"</summary>
    string SaveFileName { get; }

    /// <summary>捕获当前状态，返回纯数据 DTO</summary>
    object CaptureState();

    /// <summary>从 DTO 恢复状态</summary>
    void RestoreState(object state);
}
```

---

## 3. JSON 读写工具

```csharp
// Scripts/Core/SaveJson.cs  (静态工具类)

static readonly JsonSerializerOptions Options = new()
{
    WriteIndented = true,
    PropertyNameCaseInsensitive = true
};

static string Serialize(object data) =>
    JsonSerializer.Serialize(data, Options);

static T Deserialize<T>(string json) =>
    JsonSerializer.Deserialize<T>(json, Options)!;
```

---

## 4. 各子系统数据 DTO

### 4.1 路网 `road_network.json`

```json
{
  "nextID": 42,
  "cellSize": 64.0,
  "junctions": [
    { "id": 0, "x": 32.0, "y": 32.0 }
  ],
  "segments": [
    {
      "id": 0,
      "fromJunctionID": 0,
      "toJunctionID": 1,
      "roadID": 0,
      "waypoints": [{"x":96,"y":32}],
      "totalLength": 128.0
    }
  ],
  "roads": [
    { "id": 0, "segmentIDs": [0, 1] }
  ]
}
```

**恢复逻辑要点**：
- 按存档 ID 重建 Junction/Segment/Road
- 设置 `_nextID = max(allIDs) + 1`
- 重建所有反向索引词典（`_posToJunctionID`, `_posToSegmentID`）
- 恢复 `_lastCellSize`

> ⚠️ `Junction` 内部的 `_connections` 词典（以 SegmentID 为键）需要从 Segment 数据**反向重建**：遍历所有 Segment，对每条 Segment 的 from/to Junction 调用 `AddSegmentConnection`。

### 4.2 相机 `camera.json`

```json
{
  "positionX": 500.0,
  "positionY": 300.0,
  "zoom": 1.5
}
```

### 4.3 时间系统 `time.json`（Phase 3 接入）

```json
{
  "year": 2026, "month": 1, "day": 1,
  "hour": 8, "minute": 0, "second": 0,
  "speed": 1
}
```

---

## 5. SaveManager 设计

```
SaveManager : Node (Autoload)
│
├── Register(ISaveable)           ← 各系统 _Ready 中调用
├── Save(string slotName)
├── Load(string slotName)         ← 返回 bool 是否成功
├── DeleteSlot(string slotName)
├── SaveSlotExists(string slotName) → bool
└── CurrentSlotName → string
```

### 保存流程

1. 确保 `user://saves/{slotName}/` 目录存在
 2. 遍历 `_saveables`，逐个调用 `CaptureState()` → 写入对应 `.json`
 3. **先写 `.tmp`，成功后再 rename**（防断电 → 写坏文件）
 4. 最后写 `manifest.json`

```
Save("autosave"):
  for each saveable:
    write: autosave/road_network.json.tmp  →  rename →  road_network.json
    write: autosave/camera.json.tmp        →  rename →  camera.json
  write: autosave/manifest.json
```

### 加载流程

1. 读取 `manifest.json` 获取文件清单
2. 遍历清单中的每个 `.json` 文件
3. 读到 DTO → 分发给对应 `ISaveable.RestoreState()`
4. 任一文件缺失/损坏 → 放弃本次加载，`return false`

### `manifest.json`

```json
{
  "schemaVersion": 1,
  "slotName": "autosave",
  "timestamp": "2026-05-30T12:00:00Z",
  "cityName": "My City",
  "files": [
    "road_network.json",
    "camera.json"
  ]
}
```

---

## 6. RoadNetwork 序列化细节

### 需要保存的私有状态

| 字段 | 类型 | 说明 |
|------|------|------|
| `_junctions` | `Dictionary<int, Junction>` | 需要完整序列化 |
| `_segments` | `Dictionary<int, Segment>` | 需要完整序列化 |
| `_roads` | `Dictionary<int, Road>` | 需要完整序列化 |
| `_nextID` | `int` | 必须恢复，避免 ID 冲突 |
| `_lastCellSize` | `float` | 恢复以便 RemoveSegment 使用 |

### 不需要保存的（可重建）

| 字段 | 说明 |
|------|------|
| `_posToJunctionID` | 从 Junction.Position 重建 |
| `_posToSegmentID` | 从 Segment 的 waypoints + 端点重建 |
| Junction._connections | 从 Segment 的 from/to 关系反向构建 |
| `_inMergeOperation` | 运行时标志，恢复时为 `false` |

### 需要添加的内部方法

```csharp
// RoadNetwork.cs - 新增方法
internal void RebuildIndexes(float cellSize)
{
    // 重建 _posToJunctionID
    // 重建 _posToSegmentID
    // 重建每个 Junction 的 _connections
}
```

---

## 7. 现有代码改动清单

| 文件 | 改动类型 | 说明 |
|------|----------|------|
| **新建** `Scripts/Core/ISaveable.cs` | 新建 | 接口定义 |
| **新建** `Scripts/Core/SaveManager.cs` | 新建 | Autoload 单例，协调保存/加载 |
| **新建** `Scripts/Core/SaveJson.cs` | 新建 | JSON 读写静态工具 |
| **新建** `Scripts/Core/SaveData.cs` | 新建 | 所有 DTO 定义（纯数据类） |
| `Scripts/Road/RoadNetwork.cs` | 修改 | 实现 `ISaveable` + `RebuildIndexes()` |
| `Scripts/Road/RoadSystem.cs` | 修改 | `_Ready` 中注册到 SaveManager |
| `Scripts/MainCamera.cs` | 修改 | 实现 `ISaveable` |
| `project.godot` | 修改 | 添加 `SaveManager` Autoload |

---

## 8. 实施步骤

```
Step 1: SaveManager 骨架
  - SaveJson 工具类（JSON 读写）
  - SaveData DTO 定义
  - ISaveable 接口
  - SaveManager autoload（注册 + 基本 Save/Load 循环）
  - project.godot 注册 autoload

Step 2: RoadNetwork 接入
  - RoadNetworkData DTO
  - RoadNetwork.CaptureState() / RestoreState()
  - RoadNetwork.RebuildIndexes()
  - RoadSystem 注册到 SaveManager

Step 3: Camera 接入
  - CameraData DTO
  - MainCamera.CaptureState() / RestoreState()

Step 4: 触发点
  - ImGui 按钮：Save / Load
  - 快捷键：F5 快速保存 / F9 快速加载
  - 退出时自动保存（_Notification(NOTIFICATION_WM_CLOSE_REQUEST)）
```

---

## 9. 未来接入点（无需改动框架）

各 Phase 完成后只需：

1. 在对应 Manager 类上实现 `ISaveable`
2. 在 `_Ready` 中 `SaveManager.Instance.Register(this)`
3. 定义该系统的 DTO（加进 `SaveData.cs` 或系统自有文件）

| Phase | 系统 | 接入文件 |
|-------|------|----------|
| 1 | 网格系统 | `GridManager.cs` |
| 2 | 分区系统 | `ZoneManager.cs` |
| 3 | 时间系统 | `TimeManager.cs` |
| 4 | 人口/经济 | `PopulationSystem.cs`, `EconomySystem.cs` |
| 5+ | 后续 | 各 Manager |

---

## 10. 风险 & 注意事项

- **ID 冲突**：加载后 `_nextID` 必须 ≥ 所有已用 ID 的最大值 + 1，否则新铺设道路会产生 ID 碰撞
- **字典顺序**：JSON 反序列化 Dictionary 时键是字符串，RoadNetwork 用 `int` 做键——DTO 中改用数组 `[{id, ...}]` 而非字典
- **浮点精度**：Junction Position / Waypoints 坐标可能因浮点序列化产生微小漂移 → 加载后需要调用 `SnapToGrid` 修正所有坐标
- **事件重放**：`RestoreState` 不会触发 `SegmentAdded` 事件 → 加载完成后渲染器需手动全量重建显示（`RoadRenderer.ReDrawAll()`）
- **文件编码**：使用 UTF-8 without BOM，显式指定 `Encoding.UTF8`
