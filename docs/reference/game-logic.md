# 游戏总体逻辑

> 最后更新：2026-07-19
>
> 当前实现范围：本文的道路、HUD、相机和存档流程已按当前 Godot/C# 源码校准。分区、经济、时间和交通模拟仍是设计文档中的未来系统，不是已实现功能。

---

## 1. 系统架构总览

```mermaid
flowchart LR
    subgraph Autoload["自动加载"]
        ImGuiRoot["ImGuiRoot"]
        SaveMgr["SaveManager"]
        MCP["MCPGameBridge"]
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
        RoadGraph["RoadGraph<br/>ISaveable"]
        RoadConfig["RoadConfig<br/>共享 .tres 资源"]
    end

    subgraph Util["静态工具类"]
        GridSystem["GridSystem"]
        DirectionUtil["DirectionUtil"]
    end

    RoadSys --> RoadBuilder
    RoadSys --> RoadRenderer
    RoadSys --> RoadGraph
    RoadSys -->|注入 Config| GridSystem

    RoadBuilder -->|调用 API| RoadGraph
    RoadBuilder -->|设置预览| RoadRenderer
    RoadBuilder -.读取.-> RoadConfig
    RoadBuilder -.调用.-> GridSystem
    RoadBuilder -.调用.-> DirectionUtil

    RoadGraph -->|EdgeAdded EdgeRemoved GraphCleared| RoadRenderer
    RoadGraph -.调用.-> DirectionUtil

    RoadRenderer -.读取.-> RoadConfig

    ToolMgr -->|转发输入| RoadBuilder
    GameHUD -.读取统计.-> RoadGraph
    GameHUD -.读取工具.-> ToolMgr

    SaveMgr -->|CaptureState RestoreState| RoadGraph
```

---

## 2. 道路铺设完整流程

```mermaid
flowchart TD
    subgraph Phase1["拖拽开始"]
        S1["玩家按下左键"] --> S2["ToolManager 转发至 RoadBuilder"]
        S2 --> S3["RoadBuilder.BeginDrag"]
        S3 --> S4["GridSystem.SnapToGrid 吸附"]
        S4 --> S5{"格点上有 GraphEdge?"}
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
        C5 --> C6["RoadGraph.AddRoad"]
        C6 --> C8["IsPathFullyCovered 重叠预检"]
        C8 --> C9["ResolveIntersections 交叉劈分"]
        C9 --> C10["SplitEdgesAtPathAnchors 劈开旧 Edge"]
        C10 --> C11["按节点锚点生成 GraphEdges"]
        C11 --> C12["TryMergeAtNode 合并降级"]
        C12 --> C13["EdgeAdded 事件触发"]
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
        E2 --> E3["RoadGraph.FindClosestEdge 按snap位置查询"]
        E3 --> E4{"命中?"}
        E4 -->|否| E5["FindNearestRoadPoint 几何最近点回退"]
        E5 --> E6["设置 HoveredEdgeID"]
        E4 -->|是| E6
        E6 --> E7["RoadRenderer.QueueRedraw 悬停高亮"]
    end

    subgraph Phase2["点击拆除 拓扑修复"]
        R1["玩家点击左键"] --> R2["RoadBuilder.HandleRemoveInput"]
        R2 --> R3["RoadGraph.FindClosestEdge 得到 edgeID"]
        R3 --> R4["RoadGraph.RemoveEdge"]
        R4 --> R5["清 _edges 与空间索引"]
        R5 --> R6["断开 NodeA/NodeB 邻接"]
        R6 --> R7["清理孤立 GraphNode"]
        R7 --> R8["从 RoadGroup 摘除 Edge"]
        R8 --> R11["EdgeRemoved 事件触发"]
        R11 --> R12["RoadRenderer 回收 Line2D"]
        R12 --> R13["TryMergeAtNode 两端 EdgeCount=2 对向合并"]
    end

    Phase1 --> Phase2
```

---

## 4. 路网数据模型

```mermaid
flowchart LR
    subgraph Aggregate["RoadGraph 聚合根"]
        RN["RoadGraph<br/>ISaveable<br/>+AddRoad<br/>+RemoveEdge<br/>+FindClosestEdge"]
        Idx1["_nodes<br/>Dictionary id-GraphNode"]
        Idx2["_edges<br/>Dictionary id-GraphEdge"]
        Idx3["_groups<br/>Dictionary id-RoadGroup"]
        Idx4["_nodeRefs / _edgeRefs"]
        Idx5["_spatialIndex<br/>UniformGrid"]
        RN --> Idx1
        RN --> Idx2
        RN --> Idx3
        RN --> Idx4
        RN --> Idx5
    end

    subgraph Entity["核心实体"]
        J["GraphNode<br/>+Position<br/>+EdgeCount"]
        S["GraphEdge<br/>+NodeA<br/>+NodeB<br/>+GroupID<br/>+Points"]
        R["RoadGroup<br/>+EdgeCount<br/>HashSet EdgeID"]
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

    S -->|NodeA| J
    S -->|NodeB| J
    S -->|GroupID| R

    RN -->|共线合并判定| DU

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

    Select["Select<br/>选择工具"] -->|RoadToolButton 或 tool_road 默认 R| Road["Road<br/>铺路工具"]
    Select -->|tool_remove 默认 E| Remove["RoadRemove<br/>拆除工具"]
    Road -->|tool_select 默认 Q| Select
    Road -->|tool_remove 默认 E| Remove
    Remove -->|tool_select 默认 Q| Select
    Remove -->|tool_road 默认 R| Road

    Select -->|pause_menu 默认 Esc| Pause["PauseMenu<br/>不改变当前工具"]
    Road -->|pause_menu 默认 Esc| Pause
    Remove -->|pause_menu 默认 Esc| Pause
    Pause -->|继续游戏| Previous["恢复先前工具状态"]

    Road -.生命周期.-> RN["Enter: 无操作<br/>Tick: BeginDrag - UpdateProjection - EndDragAndCommit<br/>Exit: CancelPlaceDrag"]
    Remove -.生命周期.-> RMN["Enter: SetRemoveHoverActive true<br/>Tick: UpdateRemoveHover 悬停检测<br/>Exit: SetRemoveHoverActive false 清除高亮"]
```

---

## 6. 存档 / 读档流程

本节只说明游戏流程中的保存和加载路径。存档目录、槽名限制、manifest、schema、验证状态和未完成事务边界见 [存档系统当前参考](save-system-plan.md)。

```mermaid
flowchart LR
    subgraph Save["命名保存或周期自动保存"]
        Entry["PauseMenu / AutosaveController"] --> SM_S["SaveManager<br/>选择 V2 配置"]
        SM_S -->|只选择 road_network| RN_S[RoadGraph]
        RN_S -->|CaptureState| RN_Data["RoadGraphSaveData<br/>Node / Edge / Group"]
        RN_Data --> Stage["完整 staging 槽<br/>road_network.json + manifest.json"]
        Stage --> Publish["整槽发布<br/>失败恢复 backup"]
    end

    subgraph Load["暂停菜单：加载"]
        Slot["manifest.json + road_network.json"] --> Validate["版本、文件表、JSON 与 RoadGraph 临时模型预检"]
        Validate -->|全部成功后提交| RN_L[RoadGraph]
        RN_L -->|RebuildNodeEdges + RebuildSpatialIndex| RN_L
        RN_L -->|GraphCleared 事件| RR[RoadRenderer 重建显示]
    end
```

---

## 7. 关键决策流程

### 7.1 半格点拖拽方向限制

```mermaid
flowchart TD
    Start["BeginDrag 记录起点"] --> Snap["SnapToGrid"]
    Snap --> Check{"格点有 GraphEdge"}
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

### 7.2 对向合并降级(TryMergeAtNode)

```mermaid
flowchart TD
    Start{"GraphNode EdgeCount equal 2"} -->|否| Skip["跳过"]
    Start -->|是| SegAB["取两条 GraphEdge"]
    SegAB --> Guard1{"自环"}
    Guard1 -->|是| Skip
    Guard1 -->|否| Orient["OrientTowardsJunction"]
    Orient --> Dir["取 Junction to 邻点 方向"]
    Dir --> Guard2{"两侧方向对向"}
    Guard2 -->|否| Skip
    Guard2 -->|是| Guard3{"合并后8方向连续"}
    Guard3 -->|否| Skip
    Guard3 -->|是| Guard4{"farA 不等于 farB"}
    Guard4 -->|否| Skip
    Guard4 -->|是| Merge["RemoveEdge A and B<br/>AddEdge farA to farB<br/>保留 GroupID"]
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
        HUD[GameHUD._Input]
        HUD -->|pause_menu 当前绑定| Pause[PauseMenu 打开并暂停场景树]
        HUD -->|tool_select / tool_road / tool_remove| ToolState[设置 ToolManager.CurrentTool]
        TM[ToolManager._Input]
        TM -->|按 CurrentTool| Forward[转发]
        Forward -->|Road| Place[RoadBuilder.HandlePlaceInput]
        Forward -->|RoadRemove| Remove[RoadBuilder.HandleRemoveInput]
    end

    subgraph Data["数据层"]
        Place --> Add["RoadGraph.AddRoad"]
        Remove --> Del["RoadGraph.RemoveEdge"]
        Add --> Event1["EdgeAdded 事件"]
        Del --> Event2["EdgeRemoved 事件"]
        Add --> Merge["TryMergeAtNode"]
        Del --> Merge2["TryMergeAtNode"]
    end

    subgraph Render["渲染层"]
        Event1 --> RR_Add["RoadRenderer.OnEdgeAdded<br/>创建 Line2D"]
        Event2 --> RR_Del["RoadRenderer.OnEdgeRemoved<br/>回收 Line2D"]
        RR_Add --> Draw["QueueRedraw"]
        RR_Del --> Draw
    end

    subgraph UI["UI 刷新"]
        Place --> HUD["GameHUD._Process<br/>刷新统计"]
        Remove --> HUD
        Place --> Preview["RoadRenderer._Draw<br/>清除预览虚线"]
    end
```
