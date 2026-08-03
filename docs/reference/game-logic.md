# 游戏总体逻辑

> 最后更新：2026-08-04
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
        RoadBuilder["RoadBuilder<br/>输入与会话协调"]
        RoadRenderer["RoadRenderer<br/>事件渲染"]
        DisplaySampler["RoadGeometryDisplaySampler<br/>只读显示细分"]
    end

    subgraph RoadInput["铺路输入层"]
        Placement["RoadPlacementSession<br/>固定拐点 + 活动末端"]
        Strategy["IRoadInputStrategy<br/>吸附与单段草稿"]
    end

    subgraph EditHistory["道路编辑历史"]
        History["RoadEditHistory<br/>容量 64 的前后状态事务"]
    end

    subgraph Data["纯数据层"]
        RoadGraph["RoadGraph<br/>IPreparedSaveable"]
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

    RoadBuilder -->|铺路或拆路提交| History
    History -->|SubmitPath / RemoveEdges| RoadGraph
    History -->|Undo / Redo RestoreState| RoadGraph
    RoadBuilder -->|有效草稿原生段| DisplaySampler
    DisplaySampler -->|完整 PreviewPoints| RoadRenderer
    RoadBuilder -.读取.-> RoadConfig
    RoadBuilder --> Placement
    Placement --> Strategy
    Strategy -.默认米字型实现.-> DirectionUtil

    RoadGraph -->|EdgeAdded EdgeRemoved GraphCleared| RoadRenderer
    RoadRenderer -->|Edge 原生段| DisplaySampler
    RoadGraph -->|外部 GraphCleared 清空旧历史| History

    RoadRenderer -.读取.-> RoadConfig

    ToolMgr -->|转发输入| RoadBuilder
    GameHUD -->|edit_undo / edit_redo| ToolMgr
    GameHUD -.读取统计.-> RoadGraph
    GameHUD -.读取工具.-> ToolMgr

    SaveMgr -->|CaptureState RestoreState| RoadGraph
```

---

## 2. 道路铺设完整流程

```mermaid
flowchart TD
    subgraph Phase1["建立会话"]
        S1["玩家按下左键"] --> S2["ToolManager 转发至 RoadBuilder"]
        S2 --> S3["CanvasTransform 反变换为世界坐标"]
        S3 --> S4["IRoadInputStrategy.SnapPointer"]
        S4 --> S5{"吸附点附近已有道路?"}
        S5 -->|否| S6["FindNearestRoadPoint 几何锚点回退"]
        S5 -->|是| S7["创建 RoadPlacementSession"]
        S6 --> S7
        S7 --> S8["发布单点 PreviewPoints"]
    end

    subgraph Phase2["编辑完整路径"]
        D1["鼠标移动"] --> D2["策略从当前 anchor 生成活动草稿"]
        D2 --> D3["会话组合固定草稿和活动草稿"]
        D3 --> D4["统一采样器生成完整显示点列"]
        D4 --> D4A["RoadRenderer 绘制完整多段虚线"]
        D4A --> D5{"下一输入"}
        D5 -->|左键| D6["固定活动草稿为新拐点"]
        D5 -->|右键且有拐点| D7["回退最后拐点并重建活动末端"]
        D5 -->|右键且零拐点| D8["取消会话 不修改图"]
        D6 --> D1
        D7 --> D1
    end

    subgraph Phase3["一次确认提交"]
        C1["Enter / 双击 / 旧式拖拽释放"] --> C2["确认完整 RoadPathDraft"]
        C2 --> C3{"Path 存在?"}
        C3 -->|否| C4["保留会话 等待有效末端"]
        C3 -->|是| C5["RoadEditHistory 捕获事务前状态"]
        C5 --> C6["RoadGraph.SubmitPath 一次调用"]
        C6 --> C7{"提交成功?"}
        C7 -->|否| C8["不入历史 图不变并保留会话"]
        C7 -->|是| C9["状态变化时前后状态进入撤销栈<br/>并清空重做栈"]
        C9 --> C10["发布 Edge 事件并清空会话预览"]
        C10 --> C11["RoadRenderer 从原生段采样并创建 Line2D"]
    end

    Phase1 --> Phase2
    Phase2 --> Phase3
```

---

## 3. 道路拆除选择 + 批量事务

```mermaid
flowchart TD
    subgraph Phase1["切到拆除工具"]
        E1["ToolManager SetRemoveHoverActive true"] --> E2["每帧 _Process"]
        E2 --> E3["RoadGraph.FindClosestEdge 按snap位置查询"]
        E3 --> E4{"命中?"}
        E4 -->|否| E5["按原始世界指针再次查询"]
        E5 --> E6["设置 HoveredEdgeID"]
        E4 -->|是| E6
        E6 --> E7["RoadRenderer.QueueRedraw 悬停高亮"]
    end

    subgraph Phase2["先选择 后提交"]
        R1["玩家按下左键"] --> R2{"Shift 是否按下"}
        R2 -->|否| R3["连续模式 沿轨迹累积圆命中"]
        R2 -->|是| R4["矩形模式 按当前框重建选择"]
        R3 --> R5["排序去重的稳定 Edge ID 集"]
        R4 --> R5
        R5 --> R6["RoadRenderer 绘制选择高亮和框线"]
        R6 --> R7{"后续输入"}
        R7 -->|右键或切出工具| R8["取消选择 图不变"]
        R7 -->|松开左键| R9["RoadEditHistory 捕获事务前状态"]
        R9 --> R10["RoadGraph.RemoveEdges 一次调用"]
        R10 --> R11["批量 DetachEdge"]
        R11 --> R12["一次清理孤立 Node 和空 Group"]
        R12 --> R13["验证不变式后按 ID 发布 EdgeRemoved"]
        R13 --> R14["成功状态进入撤销栈并清空重做栈"]
        R14 --> R15["RoadRenderer 回收 Line2D 并清预览"]
    end

    Phase1 --> Phase2
```

---

## 4. 路网数据模型

```mermaid
flowchart LR
    subgraph Aggregate["RoadGraph 聚合根"]
        RN["RoadGraph<br/>IPreparedSaveable<br/>+SubmitPath<br/>+RemoveEdges<br/>+空间选择查询"]
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

    Select -->|edit_undo / edit_redo 默认 Z/Y| Edit["撤销 / 重做道路编辑<br/>当前工具不变"]
    Road -->|edit_undo / edit_redo 默认 Z/Y| Edit
    Remove -->|edit_undo / edit_redo 默认 Z/Y| Edit

    Road -.生命周期.-> RN["Enter: 等待输入<br/>Input: 旧式拖拽提交或点击式连续会话<br/>Exit: CancelPlaceSession"]
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

### 7.1 默认米字型策略的偏移起点限制

```mermaid
flowchart TD
    Start["RoadBuilder.BeginPlace"] --> Snap["SquareEightRoadInputStrategy.SnapPointer"]
    Snap --> Check{"吸附点附近有道路"}
    Check -->|是| Direct["使用策略吸附点"]
    Check -->|否| Fallback["FindNearestRoadPoint"]
    Fallback -->|找到| HalfGrid["使用道路原生锚点"]
    Fallback -->|未找到| Direct

    HalfGrid --> Session["创建 RoadPlacementSession"]
    Direct --> Session
    Session --> Update["鼠标事件触发 BuildDraft"]
    Update --> Offset{"起点不在主格点"}
    Offset -->|是| Filter["策略内部过滤为对角候选"]
    Offset -->|否| Project["策略内部遍历 8 方向"]
    Filter --> DiagOnly["仅 NE SE SW NW 候选"]
    DiagOnly --> Project["投影选最长"]
    Project --> Anchor["半格 anchor 生成连续原生 line 草稿"]
    Anchor --> Compose["会话组合完整预览和 RoadPath"]
    Compose --> Commit["ConfirmPlace 经 SubmitPath 一次提交"]
```

### 7.2 对向合并降级(TryMergeAtNode)

```mermaid
flowchart TD
    Start{"GraphNode EdgeCount equal 2"} -->|否| Skip["跳过"]
    Start -->|是| SegAB["取两条 GraphEdge"]
    SegAB --> Guard1{"同一 Edge 或不同 Group"}
    Guard1 -->|是| Skip
    Guard1 -->|否| Orient["OrientTowardsNode 获取两侧完整点列"]
    Orient --> Guard2{"远端不同且点列完整"}
    Guard2 -->|否| Skip
    Guard2 -->|是| Guard3{"AreOppositeCollinear 几何对向共线"}
    Guard3 -->|否| Skip
    Guard3 -->|是| Merge["DetachEdge A/B<br/>AddEdge farA to farB<br/>保留 GroupID 和中间点"]
    Merge --> Commit["CommitEdgeMutation 清理并验证"]
    Commit --> Events["EdgeRemoved A/B 后 EdgeAdded replacement"]
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
        HUD -->|edit_undo / edit_redo| EditCommand[ToolManager 委托道路编辑历史]
        TM[ToolManager._Input]
        TM -->|按 CurrentTool| Forward[转发]
        Forward -->|Road| Place[RoadBuilder.HandlePlaceInput]
        Forward -->|RoadRemove| Remove[RoadBuilder.HandleRemoveInput]
    end

    subgraph Data["数据层"]
        Place --> History["RoadEditHistory.Execute"]
        Remove --> History
        EditCommand --> History
        History --> Add["RoadGraph.SubmitPath"]
        History --> Del["RoadGraph.RemoveEdges"]
        History --> Restore["RoadGraph.RestoreState"]
        Add --> Event1["EdgeAdded 事件"]
        Del --> Event2["EdgeRemoved 事件"]
        Restore --> Event3["GraphCleared 事件"]
        Add --> Merge["TryMergeAtNode"]
        Del --> Cleanup["CommitEdgeMutation 一次清理"]
    end

    subgraph Render["渲染层"]
        Event1 --> RR_Add["RoadRenderer.OnEdgeAdded<br/>创建 Line2D"]
        Event2 --> RR_Del["RoadRenderer.OnEdgeRemoved<br/>回收 Line2D"]
        Event3 --> RR_Rebuild["RoadRenderer.OnGraphCleared<br/>全量重建 Line2D"]
        RR_Add --> Sample["RoadGeometryDisplaySampler<br/>原生段 -> 确定显示点列"]
        RR_Rebuild --> Sample
        Sample --> Draw["Line2D.Points / QueueRedraw"]
        RR_Del --> Draw
    end

    subgraph UI["UI 刷新"]
        Place --> HUD["GameHUD._Process<br/>刷新统计"]
        Remove --> HUD
        Place --> Preview["RoadRenderer._Draw<br/>清除预览虚线"]
        Remove --> RemovalPreview["RoadRenderer._Draw<br/>选择高亮与矩形框线"]
    end
```
