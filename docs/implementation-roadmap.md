# 实施路线图

> 状态：草案 | 最后更新：2026-05-23

---

## 阶段划分总览

```
Phase 0: 基础设施        ✅ 已完成
Phase 1: 网格与道路       🔜 下一步
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

## Phase 1：道路网络 🔜（下一步）

### 1.1 道路数据结构

| 任务 | 文件 | 说明 |
|------|------|------|
| 8 方向枚举与工具 | `Scripts/Road/Direction.cs` | `enum Direction` + `DirectionUtil` 位移/长度/方向判定 |
| 路口 | `Scripts/Road/Junction.cs` | `class Junction`：Id, Position, Type, 邻接表 |
| 路段 | `Scripts/Road/Road.cs` | `class Road`：两端 JunctionId, Direction, Length |
| 路网容器 | `Scripts/Road/RoadNetwork.cs` | `class RoadNetwork`：Junction/Road 管理 + 事件 + 增删查 |

### 1.2 道路渲染

| 任务 | 文件 | 说明 |
|------|------|------|
| 道路矢量渲染 | `Scripts/Road/RoadRenderer.cs` | `_Draw()` 绘制路段矩形 + 交叉口圆，订阅事件自动重绘 |

### 1.3 道路铺设/拆除工具

| 任务 | 文件 | 说明 |
|------|------|------|
| 铺路交互 | `Scripts/Road/RoadBuilder.cs` | 鼠标拖拽铺设，8 方向连续放置，网格对齐 snap |
| 拆路交互 | 同上 | 切换模式后点击拆除路段 |

### 1.4 工具系统

| 任务 | 文件 | 说明 |
|------|------|------|
| 工具枚举 | `Scripts/Tools/ToolType.cs` | Select / Road / RoadRemove（Phase 1 仅此三个） |
| 工具管理器 | `Scripts/Tools/ToolManager.cs` | 工具切换（R/E/Esc），输入转发到当前工具 |

### 1.5 Godot 原生调试面板

| 任务 | 说明 |
|------|------|
| 调试面板 | `GameHUD : CanvasLayer`，代码构建 Panel + Label + Button |
| 道路网络统计 | 路口数、路段数 |
| 工具状态 | 当前激活工具、鼠标格点坐标 |
| 工具切换按钮 | "选择 (Esc)" / "铺路 (R)" / "拆路 (E)"，点击切换工具 |

### Phase 1 里程碑
> 🎯 可以用鼠标在 8 方向上自由画路，道路在视觉上正确渲染，交叉口自动合成。

### Phase 1 关键设计决策
- **网络模型**：Junction + Road + RoadNetwork，不存逐格 GridMap
- **网格仅用于对齐**：`SnapToGrid()` 纯函数对齐鼠标坐标
- **事件驱动渲染**：RoadAdded/RoadRemoved → QueueRedraw
- **文件清单（6 个）**：Direction.cs, Junction.cs, Road.cs, RoadNetwork.cs, RoadRenderer.cs, RoadBuilder.cs + ToolType.cs, ToolManager.cs, GameHUD.cs

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

## 文件结构规划

```
Scripts/
├── MainCamera.cs                 # ✅ 已有
├── DebugGUI.cs                   # 🔜 待创建（ImGui 调试面板入口）
│
├── Core/
│   ├── GameManager.cs            # 游戏主控制器（单例）
│   ├── SimulationEngine.cs       # 模拟循环调度
│   └── TimeManager.cs            # 游戏时钟 + 日历
│
├── Grid/
│   ├── GridCoord.cs              # 网格坐标系统
│   ├── GridMap.cs                # 网格数据存储
│   ├── GridManager.cs            # 网格管理器
│   ├── GridRenderer.cs           # 网格渲染
│   └── Direction.cs              # 8 方向定义 + 工具方法
│
├── Road/
│   ├── RoadGraph.cs              # 道路图数据结构
│   ├── RoadBuilder.cs            # 道路铺设/拆除逻辑
│   ├── RoadRenderer.cs           # 道路 + 交叉口渲染
│   └── Pathfinding.cs            # A* 寻路
│
├── Zone/
│   ├── ZoneType.cs               # 分区类型枚举 + 颜色
│   ├── ZoneData.cs               # 分区数据结构（聚合指标）
│   ├── ZoneManager.cs            # 分区 CRUD + 查询
│   ├── ZoneRenderer.cs           # 分区色块渲染
│   ├── ZoneDrawTool.cs           # 自由绘制多边形
│   ├── ZoneFillTool.cs           # 一键填充道路闭合区
│   ├── RoadCycleDetector.cs      # 道路闭合环路检测
│   └── FloodFill.cs              # 泛洪填充算法
│
├── Simulation/
│   ├── PopulationSystem.cs       # 人口模拟
│   ├── EconomySystem.cs          # 经济系统
│   ├── DemandSystem.cs           # RCI 需求
│   ├── TrafficSystem.cs          # 交通模拟
│   ├── UtilitySystem.cs          # 电力 + 供水
│   ├── ServiceSystem.cs          # 城市服务
│   ├── EnvironmentSystem.cs      # 污染 + 环境
│   └── EventSystem.cs            # 随机事件
│
├── Tools/
│   ├── ToolType.cs               # 工具枚举
│   ├── ToolManager.cs            # 工具管理
│   └── ITool.cs                  # 工具接口
│
└── UI/
    ├── HUD.cs                    # 顶部信息栏
    ├── InfoPanel.cs              # 详情面板
    ├── DataOverlay.cs            # 数据图层
    └── SpeedControl.cs           # 速度控制
```

---

## 下一步行动

当前应聚焦 **Phase 1**，按顺序推进：

1. `Direction.cs` — 8 方向枚举 + 位移表
2. `GridCoord.cs` — 坐标系统
3. `GridMap.cs` — 网格数据层
4. `GridManager.cs` + `GridRenderer.cs` — 网格显示
5. `RoadGraph.cs` — 道路图数据结构
6. `ToolManager.cs` + `ITool.cs` — 工具系统框架
7. `RoadBuilder.cs` + `RoadRenderer.cs` — 道路铺设
8. `DebugGUI.cs` — ImGui 调试面板（修复 autoload 报错）
