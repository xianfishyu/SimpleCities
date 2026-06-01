# 游戏总体逻辑

> 最后更新：2026-06-01

---

## 1. 系统架构总览

```mermaid
graph TB
    subgraph Godot["Godot 场景树"]
        MapTest["MapTest.tscn<br/>Node2D 根"]
        MapBg["MapBackground<br/>CanvasLayer"]
        RoadSys["RoadSystem<br/>Node2D"]
        ToolMgr["ToolManager<br/>Node2D"]
        GameHUD["GameHUD<br/>CanvasLayer"]
        ImGuiRoot["ImGuiRoot<br/>自动加载"]
        SaveMgr["SaveManager<br/>自动加载"]
    end

    subgraph RoadSys_internal["RoadSystem 内部"]
        RoadBuilder["RoadBuilder<br/>输入处理"]
        RoadRenderer["RoadRenderer<br/>事件驱动渲染"]
    end

    subgraph Data["纯数据层"]
        RoadNetwork["RoadNetwork<br/>ISaveable"]
        RoadConfig["RoadConfig<br/>GlobalClass Resource"]
    end

    subgraph StaticUtils["静态工具"]
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
    RoadSys -->|"注入 Config"| GridSystem

    RoadBuilder -->|"调用 API"| RoadNetwork
    RoadBuilder -->|"设置预览"| RoadRenderer
    RoadBuilder -.->|"读取 Config"| RoadConfig

    RoadNetwork -->|"SegmentAdded<br/>SegmentRemoved<br/>NetworkReloaded"| RoadRenderer

    RoadRenderer -.->|"读取 Config"| RoadConfig
    RoadBuilder -.->|"调用"| GridSystem
    RoadBuilder -.->|"调用"| DirectionUtil
    RoadNetwork -.->|"调用"| GridSystem
    RoadNetwork -.->|"调用"| DirectionUtil

    ToolMgr -->|"转发输入"| RoadBuilder
    GameHUD -.->|"读取统计"| RoadNetwork
    GameHUD -.->|"读取工具"| ToolMgr

    SaveMgr -->|"CaptureState<br/>RestoreState"| RoadNetwork
    SaveMgr -->|"CaptureState<br/>RestoreState"| MainCamera["MainCamera<br/>ISaveable"]

    style MapBg fill:#1a1a2e,stroke:#16213e,color:#e0e0e0
    style RoadSys fill:#0f3460,stroke:#16213e,color:#e0e0e0
    style RoadNetwork fill:#533483,stroke:#3d2c6b,color:#e0e0e0
    style RoadBuilder fill:#e94560,stroke:#c23152,color:#fff
    style RoadRenderer fill:#e94560,stroke:#c23152,color:#fff
    style GridSystem fill:#1a1a2e,stroke:#16213e,color:#e0e0e0
    style DirectionUtil fill:#1a1a2e,stroke:#16213e,color:#e0e0e0
```

---

## 2. 道路铺设完整流程

```mermaid
sequenceDiagram
    actor User as Player
    participant Input as Input
    participant TM as ToolManager
    participant RB as RoadBuilder
    participant RU as DirectionUtil
    participant GS as GridSystem
    participant RN as RoadNetwork
    participant RR as RoadRenderer
    participant Scene as SceneTree

    rect rgb(200, 200, 255)
        Note over User,Scene: drag start
        User->>Input: left mouse down
        Input->>TM: _Input
        TM->>RB: HandlePlaceInput
        RB->>RB: BeginDrag
        RB->>GS: SnapToGrid
        GS-->>RB: snap pos
        alt half-grid start
            RB->>RN: FindNearestRoadPoint
            RN-->>RB: nearest pt
        end
        RB->>RR: PreviewFrom / PreviewTo
        RR->>Scene: QueueRedraw
    end

    rect rgb(200, 255, 200)
        Note over User,Scene: dragging per frame
        loop each frame
            User->>Input: mouse move
            RB->>RB: UpdateProjection
            RB->>GS: IsSnapGrid
            GS-->>RB: bool
            alt half-grid
                RB->>RB: filter diagonal only
            end
            RB->>RU: GetDisplacement
            RU-->>RB: step length
            RB->>RB: project to longest dir
            RB->>RR: PreviewFrom / PreviewTo
            RR->>Scene: QueueRedraw
        end
    end

    rect rgb(255, 200, 200)
        Note over User,Scene: commit
        User->>Input: left mouse up
        Input->>TM: _Input
        TM->>RB: HandlePlaceInput
        RB->>RB: EndDragAndCommit
        alt half-grid
            RB->>RB: anchor to int grid
        end
        RB->>RB: build waypoints
        RB->>RN: AddRoad
        RN->>RU: FromDisplacementAnyLength
        RU-->>RN: direction
        RN->>RN: IsPathFullyCovered
        RN->>RN: ResolveInteriorCrossings
        RN->>RN: SplitSegmentAtWaypoint
        RN->>RN: IsAnyJunctionAt
        RN->>RN: cut to segments
        RN->>RN: TryMergeAtJunction
        RN-->>RB: RoadID
        RN->>RR: SegmentAdded
        RR->>Scene: create Line2D
        RB->>RR: ClearPreview
        RR->>Scene: QueueRedraw
    end
```

---

## 3. 道路拆除 + 拓扑修复

```mermaid
sequenceDiagram
    actor User as Player
    participant TM as ToolManager
    participant RB as RoadBuilder
    participant RN as RoadNetwork
    participant RR as RoadRenderer

    rect rgb(200, 200, 255)
        Note over User,RR: enter remove tool
        TM->>RB: SetRemoveHoverActive true
        loop each frame
            RB->>RB: UpdateRemoveHover
            RB->>RN: FindSegmentAt
            alt miss
                RB->>RB: FindNearestRoadPoint
            end
            RB->>RR: HoveredSegmentID
            RR->>RR: QueueRedraw
        end
    end

    rect rgb(255, 200, 200)
        Note over User,RR: click to remove
        User->>TM: left click
        TM->>RB: HandleRemoveInput
        RB->>RN: FindSegmentAt
        RN-->>RB: segmentID
        RB->>RN: RemoveSegment
        RN->>RN: clear posToSegmentID index
        RN->>RN: disconnect junctions
        RN->>RN: remove orphan junctions
        RN->>RN: MaybeReindexJunction
        RN->>RN: remove from Road
        RN->>RN: SplitRoadIntoComponents BFS
        RN-->>RR: SegmentRemoved
        RR->>RR: recycle Line2D node
        RN->>RN: TryMergeAtJunction
    end
```

---

## 4. 路网数据模型

```mermaid
classDiagram
    class RoadNetwork {
        -Dictionary~int,Junction~ _junctions
        -Dictionary~int,Segment~ _segments
        -Dictionary~int,Road~ _roads
        -Dictionary~Vector2,int~ _posToJunctionID
        -Dictionary~Vector2,int~ _posToSegmentID
        -int _nextID
        +event SegmentAdded
        +event SegmentRemoved
        +event NetworkReloaded
        +AddRoad(from,to,waypoints,cellSize) int
        +RemoveSegment(segmentID) bool
        +RemoveRoad(roadID) bool
        +FindSegmentAt(pos) int
        -FindSegmentAtIncludingHalfGrid(pos) int
        -IsAnyJunctionAt(pos) bool
        -GetOrCreateJunction(pos) Junction
        -SplitSegmentAtWaypoint(id,pos) void
        -SplitSegmentAtPosition(id,pos) void
        -TryMergeAtJunction(junctionID) void
        -SplitRoadIntoConnectedComponents(road) void
        -ResolveInteriorCrossings(path) List
        -IsPathFullyCovered(path) bool
        -IsApproachColinearWithSegment(...) bool
        -MaybeReindexJunctionInPosDict(j) void
    }

    class Junction {
        +int ID
        +Vector2 Position
        +JunctionType Type
        +int ConnectionCount
        -Dictionary~int,Connection~ _connections
        +AddSegmentConnection(id,neighbor,dir) void
        +RemoveSegmentConnection(id) void
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
        -HashSet~int~ _segmentIDs
        +AddSegment(id) void
        +RemoveSegment(id) void
        +ContainsSegment(id) bool
    }

    class DirectionUtil {
        +All Direction[]
        +GetDisplacement(d) Vector2I
        +FromDisplacement(from,to,cellSize) Direction?
        +FromDisplacementAnyLength(from,to) Direction?
        +IsOrthogonal(d) bool
        +IsDiagonal(d) bool
        +Length(d,cellSize) float
    }

    class GridSystem {
        +Config RoadConfig
        +CellSize float
        +SnapToGrid(pos) Vector2
        +IsSnapGrid(pos) bool
    }

    class ToolManager {
        +Instance ToolManager
        +CurrentTool ToolType
        -_HandleInput(event) void
    }

    class RoadBuilder {
        +HandlePlaceInput(event) void
        +HandleRemoveInput(event) void
        -BeginDrag() void
        -UpdateProjection() void
        -EndDragAndCommit() void
        -FindNearestRoadPoint(mouse) (pos,segID)?
    }

    RoadNetwork "1" --> "*" Junction : _junctions
    RoadNetwork "1" --> "*" Segment : _segments
    RoadNetwork "1" --> "*" Road : _roads
    Segment --> Road : RoadID
    Segment --> Junction : FromJunctionID
    Segment --> Junction : ToJunctionID
    Junction --> DirectionUtil : 方向判定
    RoadNetwork --> DirectionUtil : 8方向校验
    RoadNetwork --> GridSystem : Snap/IsSnap
    RoadBuilder --> RoadNetwork : 调用API
    RoadBuilder --> DirectionUtil : 方向投影
    RoadBuilder --> GridSystem : 格点判断
    ToolManager --> RoadBuilder : 转发输入
```

---

## 5. 工具状态机

```mermaid
stateDiagram-v2
    [*] --> Select: startup

    Select --> Road: R key
    Select --> RoadRemove: E key

    Road --> Select: Esc key
    Road --> RoadRemove: E key
    note right of Road: onEnter: nop | onTick: BeginDrag to UpdateProjection | onExit: CancelPlaceDrag

    RoadRemove --> Select: Esc key
    RoadRemove --> Road: R key
    note right of RoadRemove: onEnter: SetRemoveHoverActive true | onTick: UpdateRemoveHover | onExit: SetRemoveHoverActive false
```

---

## 6. 存档 / 读档流程

```mermaid
flowchart LR
    subgraph Save["💾 存档 (F5)"]
        SM_S[SaveManager] -->|"Register()"| IS[ISaveable]
        SM_S -->|"遍历已注册"| RN_S[RoadNetwork]
        SM_S -->|"CaptureState()"| RN_Data[RoadNetworkData]
        SM_S -->|"遍历已注册"| MC_S[MainCamera]
        SM_S -->|"CaptureState()"| MC_Data[CameraData]
        RN_Data -->|"SaveJson.Serialize"| JSON_J["JSON 文件<br/>user://road_network.json"]
        MC_Data -->|"SaveJson.Serialize"| JSON_C["JSON 文件<br/>user://main_camera.json"]
    end

    subgraph Load["📂 读档 (F9)"]
        JSON_J_R["user://road_network.json"] -->|"SaveJson.Deserialize"| RN_Data_R[RoadNetworkData]
        JSON_C_R["user://main_camera.json"] -->|"SaveJson.Deserialize"| MC_Data_R[CameraData]
        RN_Data_R -->|"RestoreState()"| RN_L[RoadNetwork]
        MC_Data_R -->|"RestoreState()"| MC_L[MainCamera]
        RN_L -->|"RebuildIndexes()"| RN_L
        RN_L -->|"NetworkReloaded 事件"| RR[RoadRenderer 重建显示]
    end
```

---

## 7. 关键决策流程

### 7.1 半格点拖拽方向限制

```mermaid
flowchart TD
    Start["BeginDrag 记录起点"] --> Snap["SnapToGrid"]
    Snap --> Check{"格点有Segment"}
    Check -->|是| Direct["使用吸附格点"]
    Check -->|否| Fallback["FindNearestRoadPoint"]
    Fallback -->|找到| HalfGrid["半格起点"]
    Fallback -->|未找到| Direct

    HalfGrid --> Update["UpdateProjection 每帧"]
    Update --> Filter["遍历8方向 IsDiagonal过滤"]
    Filter --> DiagOnly["仅 NE SE SW NW 候选"]
    DiagOnly --> Project["投影选最长"]
    Project --> Commit["EndDragAndCommit"]
    Commit --> Anchor["锚定到反方向整格<br>waypoints全落整格"]
```

### 7.2 对向合并降级(TryMergeAtJunction)

```mermaid
flowchart TD
    Start{"Junction cc equal 2"} -->|否| Skip["跳过"]
    Start -->|是| SegAB["取两段 Segment"]
    SegAB --> Guard1{"自环"}
    Guard1 -->|是| Skip
    Guard1 -->|否| Orient["OrientTowardsJunction"]
    Orient --> Dir["取 Junction to 邻点 方向"]
    Dir --> Guard2{"dispA 加 dispB 等于 0"}
    Guard2 -->|否| Skip
    Guard2 -->|是| Guard3{"合并后8方向连续"}
    Guard3 -->|否| Skip
    Guard3 -->|是| Guard4{"farA 不等于 farB"}
    Guard4 -->|否| Skip
    Guard4 -->|是| Merge["inMergeOperation true<br>RemoveSegment A and B<br>AddSegment farA to farB<br>小RoadID吸收大RoadID"]
```

---

## 8. 事件流

```mermaid
flowchart LR
    subgraph Input["用户输入"]
        Mouse[鼠标事件]
        Key[键盘事件]
    end

    subgraph Routing["输入路由"]
        TM[ToolManager._Input]
        TM -->|R/E/Esc| TM_Self[切换工具]
        TM -->|按 CurrentTool| Forward[转发]
        Forward -->|Road| Place[RoadBuilder.HandlePlaceInput]
        Forward -->|RoadRemove| Remove[RoadBuilder.HandleRemoveInput]
    end

    subgraph Data["数据层"]
        Place --> Add["RoadNetwork.AddRoad"]
        Remove --> Del["RoadNetwork.RemoveSegment"]
        Add --> Event1["SegmentAdded 事件"]
        Del --> Event2["SegmentRemoved 事件"]
        Add --> Merge["TryMergeAtJunction"]
        Del --> Split["SplitRoadIntoConnectedComponents"]
        Del --> Merge2["TryMergeAtJunction"]
    end

    subgraph Render["渲染层"]
        Event1 --> RR_Add["RoadRenderer.OnSegmentAdded<br/>创建 Line2D"]
        Event2 --> RR_Del["RoadRenderer.OnSegmentRemoved<br/>回收 Line2D"]
        RR_Add --> Draw["QueueRedraw"]
        RR_Del --> Draw
    end

    subgraph UI["UI 刷新"]
        Place --> HUD["GameHUD._Process<br/>刷新统计"]
        Remove --> HUD
        Place --> Preview["RoadRenderer._Draw<br/>清除预览虚线"]
    end
```
