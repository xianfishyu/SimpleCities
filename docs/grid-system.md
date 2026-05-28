# 网格系统设计

> 状态：草案 | 最后更新：2026-05-23

---

## 1. 设计目标

- 支持 **8 方向** 道路铺设：正交（N/S/E/W）+ 斜交（NE/NW/SE/SW）
- 网格单元为正方形，斜交方向沿正方形对角线移动
- 完全俯视（Top-Down），不使用等距投影
- 矢量极简渲染：线条 + 纯色填充，无纹理

---

## 2. 坐标系

### 2.1 世界坐标 → 网格坐标

```
World Position:  (x, y)   ∈ ℝ²    (Godot 2D 坐标系，Y 轴向下)
Grid Coordinate: (c, r)   ∈ ℤ²    (列, 行)

转换规则：
  c = floor(x / CellSize)
  r = floor(y / CellSize)
```

### 2.2 网格单元结构

```csharp
// 概念定义
struct GridCell
{
    int Column;       // c
    int Row;          // r
    Vector2 Center;   // 世界坐标中心点
    CellType Type;    // 空地 / 道路 / 建筑 / 水域 / 不可建造
}
```

### 2.3 8 方向位移表

```
方向     (dc, dr)    角度
N        ( 0, -1)    0°
NE       (+1, -1)    45°
E        (+1,  0)    90°
SE       (+1, +1)    135°
S        ( 0, +1)    180°
SW       (-1, +1)    225°
W        (-1,  0)    270°
NW       (-1, -1)    315°
```

---

## 3. 道路网络拓扑

### 3.1 连通规则

道路段连接相邻两个网格单元。每个单元格可拥有最多 8 条出边（8 方向）。

```
道路段 = (CellA, CellB) where |dc|≤1, |dr|≤1, (dc,dr)≠(0,0)
```

### 3.2 道路类型

| 类型 | 方向限制 | 示例 |
|------|----------|------|
| 正交道路 | 仅 N/S/E/W | 主干道、高速公路 |
| 斜交道路 | 仅 NE/NW/SE/SW | 对角线捷径 |
| 全向交叉 | 8 方向均可 | 普通街道、交叉口 |

### 3.3 交叉口处理

当多条道路交汇于同一单元格时，渲染为交叉口节点：

- **T 型路口**：3 条道路交汇
- **十字路口**：4 条正交道路交汇（上/下/左/右）
- **X 型路口**：4 条斜交道路交汇
- **多岔路口**：5+ 条道路交汇 → 渲染为圆形枢纽节点
- **弯道**：正交→斜交过渡

---

## 4. 数据结构

### 4.1 网格存储

```
GridMap
├── int Width, Height                # 网格尺寸（单元数）
├── float CellSize                   # 单元边长（世界单位）
├── Dictionary<(int,int), GridCell>  # 稀疏存储（仅存储非空单元格）
└── Dictionary<(int,int), Node2D>    # 网格单元 → 渲染节点映射
```

### 4.2 道路图

```
RoadGraph
├── Dictionary<int, RoadNode>        # 道路节点（交叉口 / 端点）
├── Dictionary<int, RoadEdge>        # 道路段
└── 方法：
    ├── AddRoad(c1,r1, c2,r2)        # 铺设道路
    ├── RemoveRoad(c1,r1, c2,r2)     # 拆除道路
    ├── FindPath(from, to)           # 寻路（A*）
    └── GetConnectedComponent(node)  # 连通分量查询
```

### 4.3 节点与边

```csharp
class RoadNode
{
    int Id;
    int Column, Row;
    HashSet<int> NeighborIds;  // 邻接节点 ID
    NodeType Type;             // 端点 / T型 / 十字 / X型 / 多岔 / 弯道
}

class RoadEdge
{
    int Id;
    int FromNodeId, ToNodeId;
    RoadType Type;             // 正交 / 斜交
    int SpeedLimit;            // 限速（影响通行时间）
    float Length;              // 世界单位长度（正交=CellSize, 斜交=CellSize×√2）
}
```

---

## 5. 寻路系统

基于 A* 算法，在 8 方向网格上寻路。

### 5.1 启发函数

采用 **Octile 距离**（允许对角线移动的网格启发式）：

```
h(n) = D × max(|dx|, |dy|) + (D₂ - D) × min(|dx|, |dy|)

其中 D  = 正交移动代价（= CellSize）
     D₂ = 斜交移动代价（= CellSize × √2）
```

### 5.2 移动代价

| 移动方向 | 代价 |
|----------|------|
| N/S/E/W（正交） | `CellSize` |
| NE/NW/SE/SW（斜交） | `CellSize × √2` |
| 交叉口等待 | 额外惩罚值（模拟红绿灯 / 拥堵） |

---

## 6. 渲染方案

### 6.1 矢量绘制策略

使用 Godot 内置的 `CanvasItem._Draw()` API 直接绘制几何图形：

| 元素 | 绘制方式 |
|------|----------|
| 网格线 | `DrawLine()` — 浅灰色细线，仅开发模式显示 |
| 道路（正交） | `DrawRect()` — 填充矩形 |
| 道路（斜交） | `DrawColoredPolygon()` — 填充旋转矩形 |
| 交叉口 | `DrawCircle()` / 自定义多边形 |
| 建筑轮廓 | `DrawRect()` / `DrawPolyline()` |
| 区域高亮 | 半透明 `DrawRect()` 叠加 |

### 6.2 渲染层级

```
Layer 0: 地形底色（水域 / 绿地 / 不可建造区）
Layer 1: 道路网络（道路段 + 交叉口）
Layer 2: 建筑底面
Layer 3: 建筑图形（矢量几何形状）
Layer 4: UI 叠加（选中高亮、范围指示、网格调试）
```

### 6.3 视口裁剪

仅渲染相机可见范围内的网格单元，大规模地图下保证性能。

---

## 7. 已确认决策

- [x] 地图地形由 Godot 编辑器手工制作（非程序生成）
- [x] 分区支持自由绘制 + 一键填充道路闭合区

## 8. 待定问题

- [ ] 斜交道路是否与正交道路有不同属性（如限速更低、造价更贵）？
- [ ] 建筑是否需要严格对齐网格，还是可以自由旋转 / 偏移？
- [ ] 是否需要网格细分（SubCell）用于建筑内部布局？
- [ ] 最小道路闭合区域面积？（一键填充时需要下限避免 1×1 孤立格）
