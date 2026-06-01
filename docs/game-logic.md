# 游戏总体逻辑

> 最后更新：2026-06-01

---

## 1. 系统架构总览

```mermaid
graph TB
    subgraph Scene["Godot Scene Tree"]
        MapTest["MapTest.tscn Node2D"]
        MapBg["MapBackground CanvasLayer"]
        RoadSys["RoadSystem Node2D"]
        ToolMgr["ToolManager Node2D"]
        GameHUD["GameHUD CanvasLayer"]
        SaveMgr["SaveManager autoload"]
    end

    subgraph RoadSysInternal["RoadSystem internal"]
        RoadBuilder["RoadBuilder input"]
        RoadRenderer["RoadRenderer render"]
    end

    subgraph DataLayer["Pure data layer"]
        RoadNetwork["RoadNetwork ISaveable"]
        RoadConfig["RoadConfig GlobalClass"]
    end

    subgraph Utils["Static utils"]
        GridSystem["GridSystem"]
        DirectionUtil["DirectionUtil"]
    end

    MapTest --> MapBg
    MapTest --> RoadSys
    MapTest --> ToolMgr
    MapTest --> GameHUD

    RoadSys --> RoadBuilder
    RoadSys --> RoadRenderer
    RoadSys --> RoadNetwork
    RoadSys --> GridSystem

    RoadBuilder --> RoadNetwork
    RoadBuilder --> RoadRenderer
    RoadBuilder --> RoadConfig
    RoadRenderer --> RoadConfig
    RoadBuilder --> GridSystem
    RoadBuilder --> DirectionUtil
    RoadNetwork --> GridSystem
    RoadNetwork --> DirectionUtil

    RoadNetwork --> RoadRenderer
    ToolMgr --> RoadBuilder
    GameHUD --> RoadNetwork
    GameHUD --> ToolMgr
    SaveMgr --> RoadNetwork
    SaveMgr --> MainCamera["MainCamera ISaveable"]
```

---

## 2. 道路铺设流程

```mermaid
sequenceDiagram
    actor User
    participant Input
    participant TM as ToolManager
    participant RB as RoadBuilder
    participant RU as DirectionUtil
    participant GS as GridSystem
    participant RN as RoadNetwork
    participant RR as RoadRenderer
    participant Scene as SceneTree

    User->>Input: left mouse down
    Input->>TM: forward event
    TM->>RB: HandlePlaceInput
    RB->>RB: BeginDrag
    RB->>GS: SnapToGrid
    GS-->>RB: snap pos
    RB->>RN: FindNearestRoadPoint
    RN-->>RB: nearest point
    RB->>RB: calc waypoints
    RB->>RN: AddRoad
    RN->>RU: FromDisplacementAnyLength
    RU-->>RN: direction
    RN->>RN: check overlap
    RN->>RN: split crossings
    RN->>RN: split at waypoints
    RN->>RN: check junctions
    RN->>RN: create segments
    RN->>RN: TryMergeAtJunction
    RN-->>RB: RoadID
    RN->>RR: SegmentAdded event
    RR->>Scene: create Line2D
```

---

## 3. 道路拆除流程

```mermaid
sequenceDiagram
    actor User
    participant TM as ToolManager
    participant RB as RoadBuilder
    participant RN as RoadNetwork
    participant RR as RoadRenderer

    TM->>RB: SetRemoveHoverActive true
    RB->>RB: UpdateRemoveHover per frame
    RB->>RN: FindSegmentAt
    RB->>RR: HoveredSegmentID
    RR->>RR: QueueRedraw

    User->>TM: left click
    TM->>RB: HandleRemoveInput
    RB->>RN: FindSegmentAt
    RN-->>RB: segmentID
    RB->>RN: RemoveSegment
    RN->>RN: clear index
    RN->>RN: disconnect junctions
    RN->>RN: remove orphan junctions
    RN->>RN: reindex junctions
    RN->>RN: remove from Road
    RN->>RN: split into components BFS
    RN-->>RR: SegmentRemoved event
    RR->>RR: recycle Line2D
    RN->>RN: TryMergeAtJunction
```

---

## 4. 路网数据模型

```mermaid
classDiagram
    class RoadNetwork {
        -Dictionary _junctions
        -Dictionary _segments
        -Dictionary _roads
        -Dictionary _posToJunctionID
        -Dictionary _posToSegmentID
        +AddRoad() int
        +RemoveSegment() bool
        +RemoveRoad() bool
        +FindSegmentAt() int
        -TryMergeAtJunction() void
        -SplitRoadIntoConnectedComponents() void
    }

    class Junction {
        +int ID
        +Vector2 Position
        +JunctionType Type
        +int ConnectionCount
        +AddSegmentConnection() void
        +RemoveSegmentConnection() void
        +RecalculateType() void
    }

    class Segment {
        +int ID
        +int FromJunctionID
        +int ToJunctionID
        +int RoadID
        +Vector2[] Waypoints
        +float TotalLength
    }

    class Road {
        +int ID
        +int SegmentCount
        +bool IsEmpty
        +AddSegment() void
        +RemoveSegment() void
        +ContainsSegment() bool
    }

    class DirectionUtil {
        +All Direction[]
        +GetDisplacement() Vector2I
        +FromDisplacement() Direction?
        +FromDisplacementAnyLength() Direction?
        +IsOrthogonal() bool
        +IsDiagonal() bool
        +Length() float
    }

    class GridSystem {
        +Config RoadConfig
        +CellSize float
        +SnapToGrid() Vector2
        +IsSnapGrid() bool
    }

    class ToolManager {
        +Instance ToolManager
        +CurrentTool ToolType
    }

    class RoadBuilder {
        +HandlePlaceInput() void
        +HandleRemoveInput() void
        -BeginDrag() void
        -UpdateProjection() void
        -EndDragAndCommit() void
    }

    RoadNetwork --> Junction
    RoadNetwork --> Segment
    RoadNetwork --> Road
    Segment --> Road
    Segment --> Junction
    Junction --> DirectionUtil
    RoadNetwork --> DirectionUtil
    RoadNetwork --> GridSystem
    RoadBuilder --> RoadNetwork
    RoadBuilder --> DirectionUtil
    RoadBuilder --> GridSystem
    ToolManager --> RoadBuilder
```

---

## 5. 工具状态机

```mermaid
stateDiagram-v2
    [*] --> Select

    Select --> Road: R key
    Select --> RoadRemove: E key

    Road --> Select: Esc key
    Road --> RoadRemove: E key

    RoadRemove --> Select: Esc key
    RoadRemove --> Road: R key
```

**Select 状态**
- 默认状态，无操作

**Road 状态**
- onEnter: 无
- onTick: BeginDrag -> UpdateProjection
- onExit: CancelPlaceDrag

**RoadRemove 状态**
- onEnter: SetRemoveHoverActive true
- onTick: UpdateRemoveHover
- onExit: SetRemoveHoverActive false

---

## 6. 存档与读档

```mermaid
flowchart LR
    subgraph Save["Save F5"]
        SM[SaveManager]
        SM --> RN_S[RoadNetwork]
        SM --> MC_S[MainCamera]
        RN_S --> RN_D[RoadNetworkData JSON]
        MC_S --> MC_D[CameraData JSON]
    end

    subgraph Load["Load F9"]
        RN_D2[RoadNetworkData JSON]
        MC_D2[CameraData JSON]
        RN_D2 --> RN_L[RoadNetwork]
        MC_D2 --> MC_L[MainCamera]
        RN_L --> RI[RebuildIndexes]
        RN_L --> EV[NetworkReloaded event]
        EV --> RR[RoadRenderer rebuild]
    end
```

---

## 7. 关键决策流程

### 7.1 半格点拖拽方向限制

```mermaid
flowchart TD
    A[BeginDrag] --> B[SnapToGrid]
    B --> C{has segment}
    C -->|yes| D[use snap]
    C -->|no| E[FindNearestRoadPoint]
    E -->|found| F[half-grid start]
    E -->|not found| D
    F --> G[UpdateProjection]
    G --> H[filter diagonal only]
    H --> I[NE SE SW NW]
    I --> J[project to longest]
    J --> K[EndDragAndCommit]
    K --> L[anchor to int grid]
```

### 7.2 对向合并降级 TryMergeAtJunction

```mermaid
flowchart TD
    A{cc equals 2} -->|no| Z[skip]
    A -->|yes| B[get two segments]
    B --> C{self loop}
    C -->|yes| Z
    C -->|no| D[OrientTowardsJunction]
    D --> E[get directions]
    E --> F{opposite}
    F -->|no| Z
    F -->|yes| G{8-dir continuous}
    G -->|no| Z
    G -->|yes| H{farA not farB}
    H -->|no| Z
    H -->|yes| I[merge and absorb]
```

---

## 8. 事件流

```mermaid
flowchart LR
    subgraph Input["User Input"]
        Mouse
        Key
    end

    subgraph Routing["Input Routing"]
        TM[ToolManager]
        TM --> TM2[switch tool]
        TM --> Fwd[forward]
        Fwd --> Place[RoadBuilder]
        Fwd --> Remove[RoadBuilder]
    end

    subgraph Data["Data Layer"]
        Place --> Add[RoadNetwork.AddRoad]
        Remove --> Del[RoadNetwork.RemoveSegment]
        Add --> E1[SegmentAdded]
        Del --> E2[SegmentRemoved]
        Add --> M1[TryMergeAtJunction]
        Del --> Split[SplitRoadIntoConnectedComponents]
        Del --> M2[TryMergeAtJunction]
    end

    subgraph Render["Render Layer"]
        E1 --> RR1[RoadRenderer create Line2D]
        E2 --> RR2[RoadRenderer recycle Line2D]
        RR1 --> Draw[QueueRedraw]
        RR2 --> Draw
    end

    subgraph UI["UI Update"]
        Place --> HUD[GameHUD stats]
        Remove --> HUD
        Place --> Preview[clear preview]
    end
```
