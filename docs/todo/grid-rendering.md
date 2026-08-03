# 网格渲染系统待办清单

> 系统 key：`grid-rendering`
> 复核日期：2026-08-04
> 证据：`Scripts/Road/RoadGeometryDisplaySampler.cs`、`RoadRenderer.cs`、`RoadBuilder.cs`、`RoadConfig.cs`、`tests/SimpleCities.RoadGraph.Tests/RoadGeometryDisplaySamplerTests.cs`、`tests/godot/road_curve_rendering_runtime_contract.gd` 及 `docs/manuals/road-system-v2-gen.md` 附录 D。
> 主导原则：负责道路权威几何的可视化、建造预览和大规模渲染；道路分级样式属于第三代。

## 状态总览

| ID | 发现 | 当前状态 | 处置方式 |
|---|---|---|---|
| 1.1 | RoadRenderer 只消费折线点，不能按原生曲线参数渲染 | 已完成 | 六类 V2 几何共用确定的只读显示细分 |
| 1.2 | 10k Edge 的渲染与预览没有 60 FPS 验收 | 未完成 | 建立帧时间与绘制对象基线并优化 |
| 5.3 | Junction 视觉资源字段仍使用旧命名 | 非 V2 阻塞项 | 后续资源清理时单独决定资源兼容策略 |
| D5.1～D5.2 | RoadType 分级样式 | 第三代 | 第二代统一道路视觉 |

### 设计覆盖矩阵

<a id="grid-rendering7a82ab6271cd"></a>

| 设计范围 | 当前事实 | 关联待办 |
|---|---|---|
| V2 原生曲线显示 | 六类权威几何已由统一采样器生成稳定显示折线；提交道路、拆除高亮和有效建造预览复用同一点列 | 1.1（已完成）、`road-graph:2.5`～`road-graph:2.6`（已完成） |
| V2 规模验收 | RoadGraph 操作性能已通过 10k 门槛并记录 100k 压测；事件驱动渲染尚无同规模数据 | 1.2、`road-graph:3.1`～`road-graph:3.3`（已完成） |

## 执行顺序

### 阶段 1：曲线显示和规模验收

<a id="grid-rendering1.1"></a>

- [x] **1.1 按权威曲线几何渲染道路与建造预览**
  - 当前问题：RoadRenderer 只能将端点和 waypoint 作为 Line2D 点序列，无法保留 Bézier、样条、圆弧/圆锥曲线或回旋线等缓和曲线的几何语义。
  - 修改：为 V2 几何段提供统一求值/细分接口；渲染器按屏幕误差或稳定容差生成显示采样，权威控制参数仍保留在 RoadGraph；预览复用相同求值路径。
  - 依赖：`road-graph:2.5`、`road-graph:2.6`。
  - 关联：`tool-input:1.4` 使用同一求值路径生成建造预览。
  - 集成负责人：`grid-rendering`。
  - 测试：直线、Bézier、样条、圆弧/圆锥曲线和回旋线等缓和曲线在不同缩放下显示；交点、拆分点和预览端点与权威几何重合。
  - 验收：显示采样不是存档事实来源；缩放或重建不会改变曲线控制参数，预览与提交后的道路形状一致。
  - 完成证据（2026-08-04）：`RoadGeometryDisplaySampler` 通过六类几何共同的 `GetPosition` / `Split` 契约，以默认 `0.25` 世界单位误差和最多 16 层递归生成确定折线；line 保持精确两点，每个原生段末点固定为权威 `End`。`RoadRenderer` 的 Edge、拆除高亮与 `RoadBuilder` 的有效 `RoadPathDraft` 预览复用同一采样结果，采样前后严格几何 JSON 不变。聚焦测试 8/8、完整测试 473/473、Debug 构建 0 警告/0 错误；Godot 真实渲染契约加载六类几何，在 0.125x/4x 缩放、保存和重载后点列稳定并输出 62,082 字节截图，最终打印 `PASS road curve rendering runtime contract`。道路输入、命令中心和暂停菜单回归契约均通过。

<a id="grid-rendering1.2"></a>

- [ ] **1.2 验证 10k Edge 的 60 FPS 渲染门槛并记录 100k 压测**
  - 当前问题：每条 Edge 独立 Line2D 的规模成本尚未测量，无法证明成熟城市下的交互帧率。
  - 修改：建立固定镜头、固定可见范围和固定曲线细分容差的 10k/100k Edge 场景；记录平均帧时间、P95 帧时间、绘制对象数量和预览更新成本，并依据结果批处理或裁剪。
  - 依赖：`grid-rendering:1.1`、`road-graph:3.1`～`road-graph:3.3`。
  - 集成负责人：`grid-rendering`。
  - 验证：10k Edge 场景执行镜头移动、铺路预览、命中高亮和图重建；100k 使用相同方法压力测试。
  - 验收：固定 10k Edge 场景的 P95 总帧时间不超过 16.67 ms；100k 结果完整记录但不阻塞第二代完成。

## 暂不执行

### 第三代 RoadType 产品功能

<a id="grid-renderingb75f1d496647"></a>
<a id="grid-renderingd5.1"></a>

- [ ] **D5.1 定义 RoadType 分级样式**
  - 延期原因：道路分级数据和体验全部属于第三代。
  - 保持现状：第二代使用统一道路样式，曲线渲染不按 RoadType 分支。
  - 重新开启条件：第三代道路等级和视觉语言确定。

<a id="grid-renderingd5.2"></a>

- [ ] **D5.2 让 RoadRenderer 按 RoadType 渲染**
  - 延期原因：依赖第三代 D5.1 和新的 RoadType 数据契约。
  - 保持现状：不为第二代增加类型样式映射。
  - 重新开启条件：D5.1 完成且第三代数据 schema 落地。

### 非阻塞命名清理

<a id="grid-rendering5.3"></a>

**5.3 Junction 到 Node 的渲染资源命名迁移**

- 当前判断：`JunctionRadius` 和 `JunctionColor` 是公开资源命名遗留，但不影响第二代曲线、拓扑、输入或存档契约。
- 处置：不作为第二代完成门槛；后续修改资源字段时决定是否提供 `.tres` 兼容别名。

## 已解决基线

<a id="grid-rendering0854f0250cc2"></a>

- [x] **事件驱动 Edge 渲染与加载后全量重建已经落地。** `RoadRenderer.SetGraph` 监听 `EdgeAdded`、`EdgeRemoved` 和 `GraphCleared`。
- [x] **六类 V2 原生几何共享只读显示采样。** 显示容差只影响派生 `Line2D` / 预览点列，缩放、重建和存档往返不修改权威控制参数。

## 完成标准

1. 1.1 和 1.2 通过自动化、Godot 运行时和视觉检查。
2. 直线与全部 V2 曲线几何共享同一权威求值契约，渲染采样不会反向修改图。
3. 10k Edge 满足 60 FPS 硬门槛；100k 只记录压力测试结果。
4. RoadType 样式和 5.3 命名清理不阻塞第二代完成。
