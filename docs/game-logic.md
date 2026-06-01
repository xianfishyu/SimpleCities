# 游戏总体逻辑

> 最后更新：2026-06-01

---

## 1. 系统架构总览

```mermaid
flowchart LR
    subgraph Autoload["自动加载"]
        ImGuiRoot["ImGuiRoot"]
        SaveMgr["SaveManager"]
    end

    subgraph SceneTree["主场景 MapTest.tscn"]
        MapBg["MapBackground<br/>CanvasLayer"]
        RoadSys["RoadSystem<br/>Node2D"]
        ToolMgr["ToolManager<br/>Node2D"]
        GameHUD["GameHUD<br/>CanvasLayer"]
    end

    subgraph RoadInternal["RoadSystem 内部"]
        RoadBuilder["RoadBuilder<br/>输入处理"]
        RoadRenderer["RoadRenderer<br/>事件渲染"]
    end

    subgraph Data["纯数据层"]
        RoadNetwork["RoadNetwork<br/>ISaveable"]
        RoadConfig["RoadConfig<br/>共享 .tres 资源"]
    end

    subgraph Util["静态工具类"]
        GridSystem["GridSystem"]
        DirectionUtil["DirectionUtil"]
    end

    RoadSys --> RoadBuilder
    RoadSys --> RoadRenderer
    RoadSys --> RoadNetwork
    RoadSys -->|注入 Config| GridSystem

    RoadBuilder -->|调用 API| RoadNetwork
    RoadBuilder -->|设置预览| RoadRenderer
    RoadBuilder -.读取.-> RoadConfig
    RoadBuilder -.调用.-> GridSystem
    RoadBuilder -.调用.-> DirectionUtil

    RoadNetwork -->|SegmentAdded SegmentRemoved| RoadRenderer
    RoadNetwork -.调用.-> GridSystem
    RoadNetwork -.调用.-> DirectionUtil

    RoadRenderer -.读取.-> RoadConfig

    ToolMgr -->|转发输入| RoadBuilder
    GameHUD -.读取统计.-> RoadNetwork
    GameHUD -.读取工具.-> ToolMgr

    SaveMgr -->|CaptureState RestoreState| RoadNetwork
```

---

## 2. 道路铺设完整流程

```mermaid
flowchart TD
    subgraph Phase1["拖拽开始"]
        S1["玩家按下左键"] --> S2["ToolManager 转发至 RoadBuilder"]
        S2 --> S3["RoadBuilder.BeginDrag"]
        S3 --> S4["GridSystem.SnapToGrid 吸附"]
        S4 --> S5{"格点上有 Segment?"}
        S5 -->|否| S6["FindNearestRoadPoint 半格点回退"]
        S5 -->|是| S7["记录起点 PreviewFrom"]
        S6 --> S7
        S7 --> S8["RoadRenderer.QueueRedraw"]
    end

    subgraph Phase2["拖拽中每帧"]
        D1["鼠标移动"] --> D2["RoadBuilder.UpdateProjection"]
        D2 --> D3{"半格起点?"}
        D3 -->|是| D4["仅 NE SE SW NW 对角候选"]
        D4 --> D5["DirectionUtil.GetDisplacement 计算步长"]
        D3 -->|否| D5
        D5 --> D6["投影选最长方向格数"]
        D6 --> D7["更新 PreviewTo QueueRedraw"]
    end

    subgraph Phase3["释放提交"]
        C1["玩家释放左键"] --> C2["RoadBuilder.EndDragAndCommit"]
        C2 --> C3{"半格起点?"}
        C3 -->|是| C4["锚定反方向整格 waypoints落整格"]
        C4 --> C5["构建 waypoints 数组"]
        C3 -->|否| C5
        C5 --> C6["RoadNetwork.AddRoad"]
        C6 --> C7["8方向合法性校验"]
        C7 --> C8["IsPathFullyCovered 重叠预检"]
        C8 --> C9["ResolveInteriorCrossings 交叉劈分"]
        C9 --> C10["SplitSegmentAtWaypoint 劈开旧Segment"]
        C10 --> C11["按路口切段生成 Segments"]
        C11 --> C12["TryMergeAtJunction 合并降级"]
        C12 --> C13["SegmentAdded 事件触发"]
        C13 --> C14["RoadRenderer 创建 Line2D"]
        C14 --> C15["ClearPreview QueueRedraw"]
    end

    Phase1 --> Phase2
    Phase2 --> Phase3
```

---

## 3. 道路拆除 + 拓扑修复

```mermaid
flowchart TD
    subgraph Phase1["切到拆除工具"]
        E1["ToolManager SetRemoveHoverActive true"] --> E2["每帧 _Process"]
        E2 --> E3["RoadNetwork.FindSegmentAt 按snap格点反查"]
        E3 --> E4{"命中?"}
        E4 -->|否| E5["FindNearestRoadPoint 几何最近点回退"]
        E5 --> E6["设置 HoveredSegmentID"]
        E4 -->|是| E6
        E6 --> E7["RoadRenderer.QueueRedraw 悬停高亮"]
    end

    subgraph Phase2["点击拆除 拓扑修复"]
        R1["玩家点击左键"] --> R2["RoadBuilder.HandleRemoveInput"]
        R2 --> R3["RoadNetwork.FindSegmentAt 得到segmentID"]
        R3 --> R4["RoadNetwork.RemoveSegment"]
        R4 --> R5["清 _posToSegmentID 索引"]
        R5 --> R6["断开 from/to Junction 连接"]
        R6 --> R7["清理孤立 Junction cc=0"]
        R7 --> R8["MaybeReindexJunctionInPosDict 补回索引"]
        R8 --> R9["从 Road 摘除 Segment"]
        R9 --> R10["SplitRoadIntoConnectedComponents BFS拆分"]
        R10 --> R11["SegmentRemoved 事件触发"]
        R11 --> R12["RoadRenderer 回收 Line2D"]
        R12 --> R13["TryMergeAtJunction 两端cc=2对向合并"]
    end

    Phase1 --> Phase2
```

---

## 4. 路网数据模型

```mermaid
flowchart LR
    subgraph Aggregate["RoadNetwork 聚合根"]
        RN["RoadNetwork<br/>ISaveable<br/>+AddRoad<br/>+RemoveSegment<br/>+FindSegmentAt"]
        Idx1["_junctions<br/>Dictionary id-Junction"]
        Idx2["_segments<br/>Dictionary id-Segment"]
        Idx3["_roads<br/>Dictionary id-Road"]
        Idx4["_posToJunctionID<br/>Vector2 to id"]
        Idx5["_posToSegmentID<br/>Vector2 to id"]
        RN --> Idx1
        RN --> Idx2
        RN --> Idx3
        RN --> Idx4
        RN --> Idx5
    end

    subgraph Entity["核心实体"]
        J["Junction<br/>+Position<br/>+Type<br/>+ConnectionCount"]
        S["Segment<br/>+FromJunctionID<br/>+ToJunctionID<br/>+RoadID<br/>+Waypoints"]
        R["Road<br/>+SegmentCount<br/>HashSet 段ID集合"]
    end

    subgraph Util["静态工具"]
        DU["DirectionUtil<br/>8方向枚举与位移"]
        GS["GridSystem<br/>SnapToGrid IsSnapGrid"]
    end

    subgraph Caller["调用方"]
        TM["ToolManager"]
        RB["RoadBuilder"]
    end

    Idx1 -.持有.-> J
    Idx2 -.持有.-> S
    Idx3 -.持有.-> R

    S -->|FromJunctionID| J
    S -->|ToJunctionID| J
    S -->|RoadID| R

    RN -->|8方向校验| DU
    RN -->|Snap 判定| GS
    J -->|方向判定| DU

    TM -->|转发输入| RB
    RB -->|调用 API| RN
    RB -->|方向投影| DU
    RB -->|格点判断| GS
```

---

## 5. 工具状态机

```mermaid
flowchart LR
    Start((启动)) --> Select

    Select["Select<br/>选择工具"] -->|按 R| Road["Road<br/>铺路工具"]
    Select -->|按 E| Remove["RoadRemove<br/>拆除工具"]

    Road -->|按 Esc| Select
    Road -->|按 E| Remove
    Remove -->|按 Esc| Select
    Remove -->|按 R| Road

    Road -.生命周期.-> RN["Enter: 无操作<br/>Tick: BeginDrag - UpdateProjection - EndDragAndCommit<br/>Exit: CancelPlaceDrag"]
    Remove -.生命周期.-> RMN["Enter: SetRemoveHoverActive true<br/>Tick: UpdateRemoveHover 悬停检测<br/>Exit: SetRemoveHoverActive false 清除高亮"]
```

---

## 6. 存档 / 读档流程

```mermaid
flowchart LR
    subgraph Save["存档 F5"]
        SM_S[SaveManager] -->|Register| IS[ISaveable]
        SM_S -->|遍历已注册| RN_S[RoadNetwork]
        SM_S -->|CaptureState| RN_Data[RoadNetworkData]
        SM_S -->|遍历已注册| MC_S[MainCamera]
        SM_S -->|CaptureState| MC_Data[CameraData]
        RN_Data -->|SaveJson.Serialize| JSON_J["road_network.json"]
        MC_Data -->|SaveJson.Serialize| JSON_C["main_camera.json"]
    end

    subgraph Load["读档 F9"]
        JSON_J_R["road_network.json"] -->|SaveJson.Deserialize| RN_Data_R[RoadNetworkData]
        JSON_C_R["main_camera.json"] -->|SaveJson.Deserialize| MC_Data_R[CameraData]
        RN_Data_R -->|RestoreState| RN_L[RoadNetwork]
        MC_Data_R -->|RestoreState| MC_L[MainCamera]
        RN_L -->|RebuildIndexes| RN_L
        RN_L -->|NetworkReloaded 事件| RR[RoadRenderer 重建显示]
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
    Commit --> Anchor["锚定到反方向整格<br/>waypoints全落整格"]
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
    Guard4 -->|是| Merge["inMergeOperation true<br/>RemoveSegment A and B<br/>AddSegment farA to farB<br/>小RoadID吸收大RoadID"]
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
