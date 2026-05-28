# UI 系统架构

> 状态：已实施 | 最后更新：2026-05-28

---

## 1. 架构概览

UI 系统采用**三层叠加 + 面板管理**架构：

```
┌──────────────────────────────────────────────┐
│  Layer 3: 模态弹窗（PushModal / PopModal）     │  ← 阻塞游戏输入
│  · 设置面板  · 离线报告  · 确认对话框          │
├──────────────────────────────────────────────┤
│  Layer 2: 按需面板（Show / Hide / Toggle）    │  ← 玩家主动打开
│  · 建筑菜单  · 详情面板  · 数据图层切换        │
│  · 速度控制  · 预算面板  · 图表                │
├──────────────────────────────────────────────┤
│  Layer 1: HUD 常驻层（始终可见）               │  ← 不可关闭
│  · FPS  · 资金/人口  · RCI 需求条             │
│  · 日期/时间  · 工具选择  · 路网统计           │
├──────────────────────────────────────────────┤
│  Game World（Camera2D 视口）                   │
└──────────────────────────────────────────────┘
```

**核心原则**：
- UI 渲染独立于游戏世界（`CanvasLayer` 隔离）
- 面板生命周期由 `UIManager` 集中管理
- 仿真数据 → UI 的流向支持「轮询」和「事件驱动」两种模式
- 共享 UI 工厂方法确保视觉一致性

---

## 2. 文件结构

```
Scripts/UI/
├── UIHelpers.cs       ← 静态工厂：CreateLabel / CreateToolButton / CreateDarkPanel
├── UIManager.cs       ← 面板生命周期管理器（全局单例）
├── GameHUD.cs         ← 主 HUD：FPS、工具、格点、路网统计、工具按钮
│
├── InfoPanel.cs       ← 🔜 点击对象弹出详情
├── ToolPanel.cs       ← 🔜 根据 ToolType 显示对应子面板
├── SpeedControl.cs    ← 🔜 时间速度控制
├── DataOverlay.cs     ← 🔜 数据图层切换
└── ModalDialog.cs     ← 🔜 通用模态对话框基类

Scenes/UI/              ← 🔜 未来 .tscn 场景文件
├── GameHUD.tscn
├── InfoPanel.tscn
└── ...
```

---

## 3. 数据流

### 3.1 当前模式：轮询（Polling）

UI 每帧从仿真单例读取最新数据，适合高频/简单场景：

```
GameHUD._Process(delta)
  ├── ToolManager.Instance.CurrentTool      → 工具 Label
  ├── MainCamera.Instance.GetGlobalMousePos → 鼠标格点 Label
  ├── RoadSystem.Instance.Network           → 路网统计 Label
  └── Engine.GetFramesPerSecond()           → FPS Label
```

### 3.2 推荐演进：事件驱动 + 脏标记

随着仿真系统增多，全量轮询会浪费 CPU。Phase 4+ 应引入分层更新：

| 数据频率 | 推荐模式 | 示例 |
|---------|---------|------|
| 每帧 | 轮询 | FPS、鼠标位置、拖拽预览 |
| 每 0.5~1s | 脏标记 | 资金、人口、RCI 需求 |
| 事件触发 | C# event | 分区属性变更、建筑完成、破产警告 |

**事件驱动示例**（推荐模式）：

```csharp
// — 仿真侧 —
public class EconomySystem
{
    public event Action<BudgetData>? BudgetChanged;
    private void UpdateBudget() => BudgetChanged?.Invoke(_budget);
}

// — UI 侧 —
public override void _Ready()
{
    SimulationEngine.Instance.Economy.BudgetChanged += OnBudgetChanged;
}
private void OnBudgetChanged(BudgetData data)
{
    _moneyLabel.Text = $"¥{data.Balance:N0}";
    _moneyLabel.SelfModulate = data.Balance < 0 ? Colors.Red : Colors.White;
}
```

---

## 4. UIManager — 面板生命周期管理器

### 4.1 职责

- **注册**：面板在 `_Ready()` 中调用 `UIManager.Instance.Register("name", panel)` 注册自身
- **显示/隐藏**：`Show()` / `Hide()` / `Toggle()` 控制可见性
- **模态栈**：`PushModal()` 推入模态面板 → 阻塞游戏输入 → `PopModal()` 关闭
- **查询**：`IsVisible()` 查询状态，`GetPanel<T>()` 获取面板引用

### 4.2 使用示例

```csharp
// 注册面板（在面板自身的 _Ready 中）
public override void _Ready()
{
    UIManager.Instance.Register("InfoPanel", this);
    Visible = false; // 初始隐藏
}

// 显示面板（在任何地方）
UIManager.Instance.Show("InfoPanel");

// 模态弹窗
UIManager.Instance.PushModal("ConfirmDialog");

// 输入处理中判断是否被模态阻塞
public override void _Input(InputEvent @event)
{
    if (UIManager.Instance.IsModalActive) return;
    // ... 正常输入处理
}
```

### 4.3 单例初始化

`UIManager` 由 `GameHUD._Ready()` 自动创建并添加到场景树。构造函数中设置 `Instance`，避免 `_Ready` 时序问题。任何节点在 `_Process()` 之后访问 `UIManager.Instance` 都是安全的。

---

## 5. GameHUD — 主 HUD 实现

### 5.1 结构

```
CanvasLayer (GameHUD)
└── UIManager (Node)              ← 自动创建，子节点
└── Panel (半透明深色背景)          ← UIHelpers.CreateDarkPanel
    └── VBoxContainer
        ├── Label: FPS
        ├── Label: 工具
        ├── Label: 鼠标格点
        ├── HSeparator
        ├── Label: 道路统计
        ├── Label: 路段统计
        ├── Label: 路口统计
        ├── Label (spacer)
        └── HBoxContainer (工具按钮栏)
            ├── Button: 选择(Esc)
            ├── Button: 铺路(R)
            └── Button: 拆路(E)
```

### 5.2 代码组织

- `_Ready()` — 解析依赖 → 初始化 UIManager → 构建 UI
- `BuildUI()` → `BuildInfoSection()` / `BuildStatsSection()` / `BuildToolBar()` — 一次性控件创建
- `_Process()` → `UpdateFPS()` / `UpdateToolInfo()` / `UpdateMousePos()` / `UpdateRoadStats()` — 帧更新

### 5.3 依赖

| 依赖 | 用途 | 访问方式 |
|------|------|---------|
| `RoadConfig` | 格点尺寸 | `[Export]` 注入 |
| `ToolManager` | 当前工具 | `ToolManager.Instance` |
| `RoadSystem` → `RoadNetwork` | 路网数据 | `RoadSystem.Instance.Network` |
| `MainCamera` | 鼠标世界坐标 | `MainCamera.Instance` |
| `UIManager` | 面板管理器 | `UIManager.Instance` |

---

## 6. UIHelpers — 共享工厂

确保所有 UI 面板的控件外观一致：

```csharp
// 创建统一样式的 Label
var label = UIHelpers.CreateLabel("文本", fontSize: 13);

// 创建工具切换按钮
var btn = UIHelpers.CreateToolButton("铺路(R)", ToolType.Road, tool => {
    ToolManager.Instance.CurrentTool = tool;
});

// 创建半透明背景面板
var panel = UIHelpers.CreateDarkPanel(pos, size, alpha: 0.88f);
```

---

## 7. 添加新 UI 面板指南

### 7.1 创建面板

```csharp
using Godot;

public partial class InfoPanel : Control
{
    public override void _Ready()
    {
        // 注册到 UIManager
        UIManager.Instance.Register("InfoPanel", this);
        Visible = false; // 初始隐藏

        // 构建 UI（使用 UIHelpers 保持一致性）
        var bg = UIHelpers.CreateDarkPanel(Vector2.Zero, new Vector2(300, 200));
        AddChild(bg);
        // ...
    }
}
```

### 7.2 挂载到场景

1. 在 Godot 编辑器中，在 `Scenes/UI/` 下创建 `.tscn` 场景
2. 根节点设为对应的 Control 类型，挂载 C# 脚本
3. 在 `MapTest.tscn` 中添加该场景实例（作为 GameHUD 的子节点或同级节点）

### 7.3 控制显示

```csharp
// 快捷键切换
if (Input.IsKeyPressed(Key.I))
    UIManager.Instance.Toggle("InfoPanel");

// 点击对象显示
UIManager.Instance.Show("InfoPanel");
var panel = UIManager.Instance.GetPanel<InfoPanel>("InfoPanel");
panel?.ShowFor(selectedObject);
```

---

## 8. 设计决策记录

| 决策 | 理由 |
|------|------|
| `UIManager` 用构造函数设 Instance（而非 `_Ready`） | 避免 `AddChild` 后 `_Ready` 未执行的时序问题 |
| `UIHelpers` 为静态类（非 Godot Node） | 纯工厂方法，无生命周期依赖 |
| HUD 不注册到 UIManager | HUD 始终可见，不需要显示/隐藏管理 |
| GameHUD 程序化构建 UI（暂不迁移 .tscn） | 当前控件数量少（6 Label + 3 Button），迁移收益不显著；.tscn 迁移在控件数 > 15 时考虑 |
| `_Process` 轮询而非事件驱动（当前阶段） | Phase 1 仅 6 个动态 Label，轮询开销可忽略；Phase 4+ 引入事件驱动 |

---

## 9. 后续计划

| Phase | 任务 | 涉及文件 |
|-------|------|---------|
| Phase 2 | InfoPanel — 点击道路/分区弹出详情 | `InfoPanel.cs`, `InfoPanel.tscn` |
| Phase 3 | SpeedControl — 时间速度控制 | `SpeedControl.cs` |
| Phase 3 | 离线报告模态弹窗 | `OfflineReportDialog.cs` |
| Phase 4 | RCI 需求条 + 资金面板 | `GameHUD.cs` 扩展 |
| Phase 4 | 事件驱动迁移（资金/人口） | 添加 C# event 到 Simulation 系统 |
| Phase 5+ | 数据图层叠加系统 | `DataOverlay.cs` |
| Phase 9 | GameHUD 迁移到 .tscn 场景 | `Scenes/UI/GameHUD.tscn` |
