# 实施路线图

> 状态：实施中 | 最后更新：2026-06-02

---

## 阶段划分总览

```
Phase 0: 基础设施        ✅ 已完成
Phase 1: 网格与道路       ✅ 基本完成
Phase 2: 分区系统（原子单元）
Phase 3: 时间与基础模拟
Phase 4: 人口与经济
Phase 5: 服务与公用事业
Phase 6: 深度交通
Phase 7: 外部世界与多人
Phase 8: 环境与事件
Phase 9: 打磨与优化
```

---

## Phase 0：基础设施 ✅（已完成）

### 0.1 项目脚手架
- [x] Godot 4.6 + C# 项目初始化
- [x] ImGui 调试 UI 集成
- [x] 主场景 `MapTest.tscn`

### 0.2 相机系统
- [x] WASD 键盘平移
- [x] 中键拖拽平移
- [x] 滚轮缩放（0.125× ~ 4×）
- [x] Lerp 平滑过渡

### 0.3 输入映射
- [x] `KeyBoard_MoveUp/Down/Left/Right`

---

## Phase 1：网格与道路 ✅（基本完成）

### 1.1 网格管理器

| 任务 | 文件 | 状态 | 说明 |
|------|------|------|------|
| 8 方向枚举 | `Scripts/Road/Direction.cs` | ✅ 完成 | 方向枚举 + 位移表 + 单位/任意距离方向检测 |
| 统一网格系统 | `Scripts/Grid/GridSystem.cs` | ✅ 完成 | 静态类，`SnapToGrid()` + `IsSnapGrid()`，集中管理 CellSize |

> ⚠️ 原始计划中的 `GridCoord.cs` / `GridMap.cs` / `GridManager.cs` 三层架构已简化，由 `GridSystem` 静态类替代。网格数据不需要稀疏存储——当前阶段道路就是唯一网格占用者，由 RoadNetwork 内部字典管理。

### 1.2 网格渲染

| 任务 | 文件 | 状态 | 说明 |
|------|------|------|------|
| Shader 网格线 | `Scripts/Grid/MapBackground.cs` | ✅ 完成 | CanvasLayer + ShaderMaterial，支持主/次/点三层网格，相机自适应 |
| `_Draw()` 网格线 | `Scripts/Grid/GridRenderer.cs` | ❌ 未实现 | 由 GPU shader 方案替代，性能远优于 CPU 逐帧绘制 |
| 单元格高亮 | — | ❌ 未实现 | 低优先级——HUD 已显示鼠标格点坐标 |

> 💡 原始计划使用 `_Draw()` 绘制调试网格线，实际采用 shader-based 渲染：无限网格对齐原点，缩放/平移自动适配，无需视口裁剪逻辑。

### 1.3 道路系统

| 任务 | 文件 | 状态 | 说明 |
|------|------|------|------|
| 道路图数据结构 | `Scripts/Road/RoadNetwork.cs` | ✅ 完成 | Junction + Segment + Road 三层模型，邻接表 + 反向索引 |
| 道路铺设 | `Scripts/Road/RoadBuilder.cs` | ✅ 完成 | 鼠标拖拽 8 方向投影，半格点吸附，预览虚线 |
| 道路拆除 | 同上 | ✅ 完成 | 点击 Segment 格点拆除单段，悬停高亮（金黄半透明折线+端点圈） |
| 道路渲染 | `Scripts/Road/RoadRenderer.cs` | ✅ 完成 | Line2D 矢量绘制 + `_junctionLayer.Draw` 交叉口圆点 + 拆除悬停高亮 |
| 路网根节点 | `Scripts/Road/RoadSystem.cs` | ✅ 完成 | Node2D，持有 RoadNetwork 实例，注入 Config + GridSystem 给子节点 |
| 共享配置 | `Scripts/Road/RoadConfig.cs` | ✅ 完成 | GlobalClass Resource（.tres），CellSize + 道路/路口/悬停颜色与尺寸 |
| 逻辑路 | `Scripts/Road/Road.cs` | ✅ 完成 | 一次画线操作的 Segment 集合，支持连通分量拆分 |
| 几何边 | `Scripts/Road/Segment.cs` | ✅ 完成 | 两端 Junction + 中间 waypoints + 总长度 |
| 路口节点 | `Scripts/Road/Junction.cs` | ✅ 完成 | ConnectionCount + 类型推断（Endpoint/Straight/Curve/T/Cross/XCross/MultiWay） |

> 💡 数据结构比原始计划更精细：原始设想 `RoadGraph`（节点+边+邻接表），实际实现为三层模型——`Road`（逻辑单位，一次画线）→ `Segment`（几何边，被路口劈分）→ `Junction`（连接点，含方向）。关键算法包括：X 形交叉自动劈分（`ResolveInteriorCrossings`）、任意位置劈分（`SplitSegmentAtPosition`）、对向直通合并降级（`TryMergeAtJunction`）、移除段后连通分量拆分（`SplitRoadIntoConnectedComponents`）。

### 1.4 工具系统

| 任务 | 文件 | 状态 | 说明 |
|------|------|------|------|
| 工具枚举 | `Scripts/Tools/ToolType.cs` | ✅ 完成 | Select / Road / RoadRemove |
| 工具管理器 | `Scripts/Tools/ToolManager.cs` | ✅ 完成 | 键盘切换（R/E/Esc），输入转发，拖拽取消保护 |

> ⚠️ 原始计划中的 `ITool` 接口未实现——工具种类少（当前仅 3 种），直接 switch 分发比接口模式更简洁。Phase 2 加入分区工具后若复杂度上升可重新评估。

### 1.5 ImGui 调试面板 → HUD

| 任务 | 文件 | 状态 | 说明 |
|------|------|------|------|
| HUD 浮层 | `Scripts/UI/GameHUD.cs` | ✅ 完成 | FPS / 工具 / 鼠标格点（含路口检测）/ 路网统计（Road/Segment/Junction 数） |
| 工具切换按钮 | 同上 | ✅ 完成 | 选择 / 铺路 / 拆路 按钮 + 快捷键 F5 保存 / F9 加载 |
| UI 工厂 | `Scripts/UI/UIHelpers.cs` | ✅ 完成 | 统一 Label / Button / Panel 样式 |
| 面板管理器 | `Scripts/UI/UIManager.cs` | ✅ 完成 | 注册/显示/隐藏/模态面板生命周期 |

> 💡 原始计划使用 ImGui 调试面板，实际改用 Godot 原生 CanvasLayer + Control 树（`Scenes/UI/GameHUD.tscn`），编辑器可视化布局，更易维护。

### 1.6 半格点系统（计划外新增）

道路铺设过程中引入"半格点"概念——Junction 可落在非整数网格位置（如两条斜线交叉的几何交点），解决 X 形十字路口的锚定问题。

| 核心能力 | 实现位置 | 说明 |
|------|------|------|
| 半格点检测 | `GridSystem.IsSnapGrid()` | 位置不在 cellSize 整数倍上即为半格点 |
| 任意距离方向判定 | `DirectionUtil.FromDisplacementAnyLength()` | 归一化向量与 8 单位方向余弦匹配，支持非单位位移（如半格段） |
| 半格起点约束 | `RoadBuilder.UpdateProjection()` | 半格起点仅允许对角方向延伸（`IsDiagonal` 过滤） |
| 半格点延伸锚定 | `RoadBuilder.EndDragAndCommit()` | `anchor = start - disp*cellSize/2`，终点落在整格 |
| 半格点劈分回退 | `RoadNetwork.FindSegmentAtIncludingHalfGrid()` | 字典未命中时扫 waypoint + Junction 的 ConnectedSegmentIDs |
| 半格 Junction 检测 | `RoadNetwork.IsAnyJunctionAt()` | 含几何扫描，覆盖半格 Junction（不在 `_posToJunctionID` 中） |
| 半格 Junction 存储 | `RoadNetwork.GetOrCreateJunction()` | 半格 Junction 不进位置字典，仅通过 ID 访问 |

> ⚠️ 约束：半格起点拖出的路锚定到反方向整格（`anchor = start - disp*cellSize/2`），终点和 waypoints 落在整格。仅对角方向（NE/SE/SW/NW）可拖拽，正交方向被 `UpdateProjection` 过滤。

### Phase 1 里程碑
> 🎯 **里程碑达成**：支持 8 方向铺路、半格点锚定、X 形交叉自动劈分、交叉口类型化渲染（Endpoint/Straight/Curve/T/Cross/XCross）、工具切换（铺路/拆除/选择）、HUD 实时统计、存档/读档——Phase 1 核心功能完备。

---

## Phase 2：分区系统（原子单元）

> **关键设计**：不渲染个体建筑。分区色块是玩家看到的唯一视觉元素，所有建筑数据聚合在分区多边形内。

### 2.1 分区数据层 `ZoneSystem`

| 任务 | 文件 | 说明 |
|------|------|------|
| 分区类型定义 | `Scripts/Zone/ZoneType.cs` | R / C / I / O / M / G 枚举 + 颜色映射 |
| 分区数据结构 | `Scripts/Zone/ZoneData.cs` | 多边形顶点 + 聚合指标（人口/岗位/地价/污染/幸福度） |
| 分区管理器 | `Scripts/Zone/ZoneManager.cs` | 分区 CRUD、点选查询、指标更新 |
| 密度等级 | `Scripts/Zone/DensityLevel.cs` | 低/中/高，影响人口容量和岗位密度系数 |

### 2.2 分区绘制工具

| 任务 | 文件 | 说明 |
|------|------|------|
| 自由绘制工具 | `Scripts/Zone/ZoneDrawTool.cs` | 鼠标点击添加顶点 → 闭合形成分区多边形 |
| 一键填充工具 | `Scripts/Zone/ZoneFillTool.cs` | 点击道路闭合区域 → 泛洪填充为该分区类型 |
| 分区擦除工具 | 同上 | 右键擦除分区 |

### 2.3 分区渲染

| 任务 | 文件 | 说明 |
|------|------|------|
| 分区色块渲染 | `Scripts/Zone/ZoneRenderer.cs` | `DrawColoredPolygon()` 半透明填充，各类型颜色 |
| 分区边框 | 同上 | 细线描边，选中时高亮 |
| 密度视觉 | 同上 | 可选：低密度浅色、高密度深色（同色系） |

### 2.4 道路闭合区域检测

| 任务 | 文件 | 说明 |
|------|------|------|
| 环路检测 | `Scripts/Zone/RoadCycleDetector.cs` | 图论层面检测道路构成的闭合多边形 |
| 泛洪填充 | `Scripts/Zone/FloodFill.cs` | 对闭合区域内部网格单元批量标记分区类型 |

### Phase 2 里程碑
> 🎯 可自由绘制分区色块 + 一键填充道路包围区 → 城市是一张彩色规划图。

---

## Phase 3：时间与基础模拟

### 3.1 时间系统 `TimeSystem`

| 任务 | 文件 | 说明 |
|------|------|------|
| 游戏时钟 | `Scripts/Core/TimeManager.cs` | 1:1 现实时钟，公历日历，年-月-日-时-分-秒 |
| 速度控制 | `Scripts/UI/SpeedControl.cs` | 暂停 / 1×（实时） / ×10 / ×100 / ×1000 / ×10000 |
| 时间事件调度 | `Scripts/Core/TimeManager.cs` | 按秒/分/时/日/月/年触发回调 |

### 3.2 模拟引擎基础 `SimulationEngine`

| 任务 | 文件 | 说明 |
|------|------|------|
| 模拟循环 | `Scripts/Core/SimulationEngine.cs` | 协调各子系统按 Tick 执行 |
| 离线模拟 | 同上 | 玩家离线后城市继续运行，上线时结算离线收益 |
| 离线报告 | UI 弹窗 | 显示离线期间的关键变化（人口、收入、事件） |

### Phase 3 里程碑
> 🎯 时间开始流动，城市按自己的节奏运转，玩家离线回来后城市已变化。

---

## Phase 4：人口与经济

### 4.1 人口模拟 `PopulationSystem`

| 任务 | 说明 |
|------|------|
| 人口统计模型 | 总人口、年龄结构、教育水平（统计层面，非 Agent） |
| 迁入/迁出模型 | 基于就业、住房、满意度 |
| 市民分配 | 市民 → 住宅 → 工作地点 映射（统计层面） |

### 4.2 经济系统 `EconomySystem`

| 任务 | 说明 |
|------|------|
| 城市预算 | 收入（税收）— 支出（维护费）= 余额 |
| 税率面板 | ImGui 滑块调节各分区税率 |
| 贷款系统 | 可借款，计利息，信用额度 |

### 4.3 RCI 需求面板

| 任务 | 说明 |
|------|------|
| 需求计算 | RCI 需求基于人口统计和就业率 |
| 需求条 UI | 3 色需求条（绿/蓝/黄）显示在 HUD 上 |

### Phase 4 里程碑
> 🎯 城市有了人口、财政和 RCI 需求反馈循环，玩家可以"管理"而非仅仅"画图"。

---

## Phase 5：服务与公用事业

### 5.1 城市服务 `ServiceSystem`

| 任务 | 设施 |
|------|------|
| 服务设施放置 | 警局、消防站、医院、学校、公园 |
| 覆盖计算 | 沿道路距离的覆盖半径 |
| 效果模拟 | 犯罪率↓ 火灾风险↓ 健康↑ 教育↑ 幸福度↑ |

### 5.2 公用事业 `UtilitySystem`

| 任务 | 说明 |
|------|------|
| 电力网络 | 发电厂 → 沿道路传播 → 建筑供电 |
| 供水网络 | 水源 → 沿道路传播 → 建筑供水 |
| 排污系统 | 建筑 → 污水管 → 处理厂 |

### Phase 5 里程碑
> 🎯 市民幸福度与服务覆盖挂钩，缺电缺水有可见后果。

---

## Phase 6：深度交通

### 6.1 通勤模拟

| 任务 | 说明 |
|------|------|
| 通勤路径计算 | 每个住宅 → 工作地点 A* 寻路 |
| 路段流量统计 | 汇总所有路径，计算每段道路的流量 |
| 拥堵模型 | 流量 > 容量 → 通行时间惩罚 |

### 6.2 道路升级

| 任务 | 说明 |
|------|------|
| 道路升级工具 | 土路 → 普通街 → 主干道 → 高速 |
| 单向道 | 方向箭头渲染 |
| 公交线路 | 线路绘制 + 公交站放置 + 载客量模拟 |

### Phase 6 里程碑
> 🎯 交通拥堵成为玩家需要解决的核心挑战。

---

## Phase 7：外部世界与多人

### 7.1 外部世界连接

| 任务 | 说明 |
|------|------|
| 地图边界出口 | 高速/铁路连接至"外部世界"，车辆/市民从边界进出 |
| 区域贸易 | 进出口资源（电力、水、商品） |
| 外部需求 | 外部城市对本地产业的需求波动 |

### 7.2 多人架构（远期）

| 任务 | 说明 |
|------|------|
| 服务器权威模式 | 模拟在服务端运行，客户端为操作终端 |
| 多城市共存 | 同一世界多个玩家各自经营城市 |
| 城市间交互 | 贸易协定、移民、资源共享、合作/竞争 |
| 超大地图 | 地图分块（Chunk），按需加载，支持近乎无限扩展 |

### Phase 7 里程碑
> 🎯 城市不再是孤岛，外部力量影响城市发展。

---

## Phase 8：环境与事件

### 8.1 环境系统

| 任务 | 说明 |
|------|------|
| 污染扩散 | 空气污染（工业+车流），水污染 |
| 噪声地图 | 沿主干道和商业区传播 |
| 绿地效益 | 公园和森林吸收污染、提升幸福度 |

### 8.2 事件与灾难（占位）

| 任务 | 说明 |
|------|------|
| 事件框架 | 可扩展的事件触发/响应系统 |
| 随机事件（可选） | 火灾、犯罪潮、流行病、经济衰退 |
| 城市政策 | 玩家颁布政策（节能令、禁塑令、教育补贴等） |
| 灾难（占位） | 预留接口，暂不实现 |

---

## Phase 9：打磨与优化

### 9.1 性能优化

| 任务 | 说明 |
|------|------|
| 空间哈希 / 四叉树 | 大规模地图的网格查询加速 |
| 对象池 | 渲染节点复用 |
| 模拟分帧 | 将重计算分散到多帧执行 |
| 地图分块 | 支持超大地图的按需加载（Chunk 系统） |

### 9.2 视觉打磨

| 任务 | 说明 |
|------|------|
| 完整调色板 | 定义极简主义色彩方案 |
| 建筑图形多样化 | 同一分区多种建筑外观变体 |
| 过渡动画 | 建筑建造/升级/拆除动画 |

### 9.3 UI 完善

| 任务 | 说明 |
|------|------|
| 主菜单 | 新游戏 / 加载 / 设置 |
| HUD | 人口、资金、RCI 需求条、日期、速度控制 |
| 信息面板 | 点击建筑/道路显示详情 |
| 数据图层 | 交通流量图、地价热力图、污染图、幸福度图 |
| 离线报告 | 上线时弹出离线期间城市变化摘要 |

---

## 文件结构规划（2026-06-01 更新）

```
Scripts/
├── MainCamera.cs                      # ✅ 相机控制 + ISaveable
│
├── Core/
│   ├── GameManager.cs                 # 🔜 游戏主控制器（单例）
│   ├── SimulationEngine.cs            # 🔜 模拟循环调度
│   ├── TimeManager.cs                 # 🔜 游戏时钟 + 日历
│   ├── ISaveable.cs                   # ✅ 存档接口（Phase 1 新增）
│   ├── SaveManager.cs                 # ✅ 存档管理器（Phase 1 新增）
│   ├── SaveData.cs                    # ✅ 存档数据结构（Phase 1 新增）
│   └── SaveJson.cs                    # ✅ JSON 序列化工具（Phase 1 新增）
│
├── Grid/
│   ├── GridSystem.cs                  # ✅ 统一网格系统（静态类，替代原 GridCoord + GridMap + GridManager）
│   ├── MapBackground.cs               # ✅ Shader 网格渲染（替代原 GridRenderer._Draw()）
│   ├── ⚠️ GridCoord.cs               # 已弃用 — 由 GridSystem.SnapToGrid() 替代
│   ├── ⚠️ GridMap.cs                 # 已弃用 — 道路占用由 RoadNetwork 内部字典管理
│   ├── ⚠️ GridManager.cs             # 已弃用 — 由 GridSystem 静态类替代
│   └── ⚠️ GridRenderer.cs            # 已弃用 — Shader 网格由 MapBackground 渲染
│
├── Road/
│   ├── Direction.cs                   # ✅ 8 方向枚举 + 位移表 + FromDisplacementAnyLength
│   ├── RoadNetwork.cs                 # ✅ 路网数据层（Junction + Segment + Road 三层模型）
│   ├── RoadBuilder.cs                 # ✅ 道路铺设/拆除 + 半格点吸附
│   ├── RoadRenderer.cs                # ✅ Line2D 道路 + 交叉口渲染
│   ├── RoadSystem.cs                  # ✅ 路网根节点（Node2D，注入 Config + Network）
│   ├── RoadConfig.cs                  # ✅ 共享配置资源（GlobalClass .tres）
│   ├── Road.cs                        # ✅ 逻辑路（Segment 聚合 + 连通分量拆分）
│   ├── Segment.cs                     # ✅ 几何边（Junction 间 + waypoints + 长度）
│   ├── Junction.cs                    # ✅ 路口节点（ConnectionCount + 类型推断）
│   ├── ⚠️ RoadGraph.cs               # 已重命名 → RoadNetwork（数据结构更丰富）
│   └── Pathfinding.cs                 # 🔜 A* 寻路（Phase 6 交通系统）
│
├── Zone/
│   ├── ZoneType.cs                    # 🔜 分区类型枚举 + 颜色
│   ├── ZoneData.cs                    # 🔜 分区数据结构（聚合指标）
│   ├── ZoneManager.cs                 # 🔜 分区 CRUD + 查询
│   ├── ZoneRenderer.cs                # 🔜 分区色块渲染
│   ├── ZoneDrawTool.cs                # 🔜 自由绘制多边形
│   ├── ZoneFillTool.cs                # 🔜 一键填充道路闭合区
│   ├── RoadCycleDetector.cs           # 🔜 道路闭合环路检测
│   └── FloodFill.cs                   # 🔜 泛洪填充算法
│
├── Simulation/
│   ├── PopulationSystem.cs            # 🔜 人口模拟
│   ├── EconomySystem.cs               # 🔜 经济系统
│   ├── DemandSystem.cs                # 🔜 RCI 需求
│   ├── TrafficSystem.cs               # 🔜 交通模拟
│   ├── UtilitySystem.cs               # 🔜 电力 + 供水
│   ├── ServiceSystem.cs               # 🔜 城市服务
│   ├── EnvironmentSystem.cs           # 🔜 污染 + 环境
│   └── EventSystem.cs                 # 🔜 随机事件
│
├── Tools/
│   ├── ToolType.cs                    # ✅ 工具枚举（Select / Road / RoadRemove）
│   ├── ToolManager.cs                 # ✅ 工具管理 + 输入转发
│   └── ⚠️ ITool.cs                   # 未实现 — 当前工具少，switch 分发更简洁
│
└── UI/
    ├── GameHUD.cs                     # ✅ HUD 浮层（FPS / 工具 / 格点 / 路网统计 + 按钮）
    ├── UIHelpers.cs                   # ✅ UI 工厂（Label / Button / Panel 统一样式）
    ├── UIManager.cs                   # ✅ 面板生命周期管理（注册/显示/模态）
    ├── HUD.cs                         # ⚠️ 已重命名 → GameHUD（功能更完整）
    ├── ⚠️ DebugGUI.cs                # 已弃用 — Godot 原生 UI 替代 ImGui
    ├── InfoPanel.cs                   # 🔜 详情面板
    ├── DataOverlay.cs                 # 🔜 数据图层
    └── SpeedControl.cs                # 🔜 速度控制
```

---

## 下一步行动

Phase 1 核心已就绪，当前应聚焦 **Phase 2：分区系统**：

1. `ZoneType.cs` + `ZoneData.cs` — 分区类型枚举与数据结构定义
2. `ZoneManager.cs` — 分区 CRUD + 空间查询
3. `ZoneRenderer.cs` — 半透明色块渲染多边形
4. `ZoneDrawTool.cs` — 自由多边形绘制（点击顶点 → 闭合）
5. `RoadCycleDetector.cs` — 图论层面检测道路闭合环
6. `FloodFill.cs` — 道路闭合区域泛洪填充
7. ToolManager 扩展：加入 Zone / ZoneFill 工具

Phase 1 遗留事项（低优先级）：
- 单元格悬停高亮（当前 HUD 已显示格点坐标，优先级低）
- ITool 接口（待工具种类 ≥ 5 时引入，当前 3 种 switch 够用）
- A* 寻路（归入 Phase 6 深度交通统一实现）
