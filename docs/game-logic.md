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
flowchart TD
    subgraph Phase1["Phase 1: 拖拽开始"]
        direction TB
        S1["玩家按下左键"] --> S2["ToolManager 转发至 RoadBuilder"]
        S2 --> S3["RoadBuilder.BeginDrag"]
        S3 --> S4["GridSystem.SnapToGrid 吸附"]
        S4 --> S5{"格点上有 Segment?"}
        S5 -->|否| S6["FindNearestRoadPoint<br>半格点回退"]
        S5 -->|是| S7["记录起点<br>PreviewFrom = start"]
        S6 --> S7
        S7 --> S8["RoadRenderer.QueueRedraw"]
    end

    subgraph Phase2["Phase 2: 拖拽中 (每帧)"]
        direction TB
        D1["鼠标移动"] --> D2["RoadBuilder.UpdateProjection"]
        D2 --> D3["GridSystem.IsSnapGrid 检测"]
        D3 --> D4{"半格起点?"}
        D4 -->|是| D5["过滤: 仅 NE/SE/SW/NW<br>对角方向候选"]
        D5 --> D6["DirectionUtil.GetDisplacement<br>计算步长"]
        D4 -->|否| D6
        D6 --> D7["投影选最长方向 → 格数"]
        D7 --> D8["更新 PreviewTo<br>RoadRenderer.QueueRedraw"]
    end

    subgraph Phase3["Phase 3: 释放提交"]
        direction TB
        C1["玩家释放左键"] --> C2["RoadBuilder.EndDragAndCommit"]
        C2 --> C3["最终方向/格数确认"]
        C3 --> C4{"半格起点?"}
        C4 -->|是| C5["锚定到反方向整格<br>waypoints 全部落整格"]
        C5 --> C6["构建 waypoints 数组"]
        C4 -->|否| C6
        C6 --> C7["RoadNetwork.AddRoad"]
        C7 --> C8["DirectionUtil.FromDisplacementAnyLength<br>8方向合法性校验"]
        C8 --> C9["IsPathFullyCovered<br>完全重叠预检"]
        C9 --> C10["ResolveInteriorCrossings<br>X形几何交叉劈分"]
        C10 --> C11["SplitSegmentAtWaypoint<br>中段穿过劈开旧Segment"]
        C11 --> C12["IsAnyJunctionAt<br>半格路口检测"]
        C12 --> C13["按路口切段生成 Segments"]
        C13 --> C14["TryMergeAtJunction<br>接入前cc=1→合并降级"]
        C14 --> C15["SegmentAdded 事件触发"]
        C15 --> C16["RoadRenderer 创建 Line2D 节点"]
        C16 --> C17["ClearPreview + QueueRedraw"]
    end

    Phase1 --> Phase2
    Phase2 --> Phase3
```

---

## 3. 道路拆除 + 拓扑修复

```mermaid
flowchart TD
    subgraph Phase1["Phase 1: 切到拆除工具"]
        direction TB
        E1["ToolManager<br>SetRemoveHoverActive true"] --> E2["每帧 _Process"]
        E2 --> E3["RoadNetwork.FindSegmentAt<br>按 snap 格点反查"]
        E3 --> E4{"命中?"}
        E4 -->|否| E5["FindNearestRoadPoint<br>几何最近点回退"]
        E5 --> E6["设置 HoveredSegmentID"]
        E4 -->|是| E6
        E6 --> E7["RoadRenderer.QueueRedraw<br>悬停高亮显示"]
    end

    subgraph Phase2["Phase 2: 点击拆除 → 拓扑修复"]
        direction TB
        R1["玩家点击左键"] --> R2["RoadBuilder.HandleRemoveInput"]
        R2 --> R3["RoadNetwork.FindSegmentAt<br>→ segmentID"]
        R3 --> R4["RoadNetwork.RemoveSegment<br>segmentID"]
        R4 --> R5["清 _posToSegmentID 索引"]
        R5 --> R6["断开 from/to Junction 连接"]
        R6 --> R7["清理孤立 Junction (cc=0)"]
        R7 --> R8["MaybeReindexJunctionInPosDict<br>补回共享路口索引"]
        R8 --> R9["从 Road 摘除 Segment"]
        R9 --> R10["SplitRoadIntoConnectedComponents<br>BFS 连通分量拆分"]
        R10 --> R11["SegmentRemoved 事件触发"]
        R11 --> R12["RoadRenderer 回收 Line2D 节点"]
        R12 --> R13["TryMergeAtJunction<br>两端如降至 cc=2 对向合并"]
    end

    Phase1 --> Phase2
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
flowchart LR
    Start((启动)) --> Select

    Select["Select 选择工具<br>默认状态,无操作"] -->|"按 R"| Road["Road 铺路工具"]
    Select -->|"按 E"| Remove["RoadRemove 拆除工具"]

    Road -->|"按 Esc"| Select
    Road -->|"按 E"| Remove
    Remove -->|"按 Esc"| Select
    Remove -->|"按 R"| Road

    Road -...-> RN["
    Road 生命周期:
    Enter: 无操作
    Tick: BeginDrag→UpdateProjection→EndDragAndCommit
    Exit: CancelPlaceDrag 取消拖拽
    "]

    Remove -...-> RMN["
    RoadRemove 生命周期:
    Enter: SetRemoveHoverActive true
    Tick: UpdateRemoveHover 悬停检测
    Exit: SetRemoveHoverActive false 清除高亮
    "]
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
