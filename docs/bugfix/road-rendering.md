# Road Rendering Bug 修复记录

> 日期：2026-08-10
> 影响文件：`Scripts/Road/RoadRenderer.cs`、`Scripts/Road/RoadConfig.cs`、`tests/SimpleCities.RoadGraph.Tests/RoadRendererLifecycleContractTests.cs`、`tests/godot/road_renderer_lifecycle_runtime_contract.gd`、`tests/godot/road_input_strategy_runtime_contract.gd`
> 来源：`docs/bugfix/session-2026-08-05.md` 中 `SESSION-BUG-09` 至 `SESSION-BUG-13`

---

## BUG-1：SetGraph 绑定已有道路图时不初始化渲染缓存

### 症状

向已经包含 Edge 的 `RoadGraph` 绑定新 `RoadRenderer` 时，`_edgePoints` 和静态 mesh 仍为空。只有绑定后新增边或图主动触发 `GraphCleared`，已有道路才开始显示。

### 根因分析

旧 `SetGraph()` 只处理旧图事件解绑和新图事件订阅，没有清空旧缓存、遍历新图的 `GetAllEdges()`，也没有重建静态批次。绑定前已经发生的 `EdgeAdded` 不会补发。

### 修复方案

`SetGraph()` 现在先解绑旧图，替换图引用并取消旧延迟重建状态，再清空 `_edgePoints`、缓存新图全部现有边；节点已进入场景树且批次节点有效时立即调用 `RebuildStaticBatches()`。空图初始化和后续事件增量更新继续使用原路径。

### 影响范围

影响预填充图绑定、运行时重绑定和依赖注入测试。RoadGraph 数据、Edge 事件定义和显示采样算法不变。

---

## BUG-2：RoadRenderer 退出树后仍接收 RoadGraph 事件

### 症状

渲染器节点已释放、RoadGraph 仍存活时，加载存档触发 `GraphCleared`，旧回调继续访问已释放的 `MeshInstance2D`，导致 `SaveManager.Load()` 失败并报告 disposed object。

### 根因分析

`SetGraph()` 订阅 `EdgeAdded`、`EdgeRemoved` 和 `GraphCleared`，但节点生命周期没有对应解绑。RoadGraph 持有的委托因此超过渲染节点寿命。

### 修复方案

新增幂等的 `SubscribeGraphEvents()` / `UnsubscribeGraphEvents()` 和 `_graphEventsSubscribed` 状态。`_EnterTree()` 恢复有效图订阅，`_ExitTree()` 解除全部订阅并取消延迟批次标记；`SetGraph()` 重绑定时也走同一套生命周期逻辑。

### 影响范围

影响渲染器单独移除、重入场景树和运行时换图。整棵 RoadSystem 正常退出及图自身存档格式不变。

---

## BUG-3：道路端点高亮错误使用路口半径

### 症状

拆除工具高亮普通道路端点时固定使用 `JunctionRadius * 1.3`。当端点和路口半径配置不同时，端点会出现明显过大的高亮圆。

### 根因分析

静态节点批次会按节点类型选择半径，`DrawEdgeHighlight()` 却对两端无条件读取 `JunctionRadius`，动态高亮与静态标记使用了不同分类规则。

### 修复方案

新增 `GetNodeMarkerRadius()` 并让静态批次和动态高亮共用。单连接端点返回 `EndpointRadius`，真路口返回 `JunctionRadius`，不需要标记的节点返回 0；高亮仅在半径为正时绘制，并保留 1.3 倍高亮比例。

### 影响范围

影响道路悬停和拆除预览的端点圆尺寸。道路 ribbon、拓扑、命中半径和配置资源格式不变。

---

## BUG-4：直通二度节点被渲染为路口

### 症状

一条直路被拆成两条共线 Edge 后，中间二度节点显示为完整路口圆，即使它没有转弯或分支。

### 根因分析

旧静态批次把所有 `EdgeCount >= 2` 节点都视为 junction，没有按两条连接在节点处的切线方向区分直通与转弯。

### 修复方案

新增 `IsJunctionNode()`：三度及以上节点始终为路口；二度节点读取两条权威几何段在节点处的单位切线，只有方向不是近似对向时才是路口。直通二度节点不创建 marker；无法取得有效方向时保守显示为路口。

### 影响范围

影响静态节点 MultiMesh 的实例数量、尺寸和颜色，以及动态高亮对同一节点的分类。RoadGraph 拆分语义和 Edge ribbon 不变。

---

## BUG-5：无效 RoadWidth 允许提交不可见道路

### 症状

`RoadWidth` 为 0、负数或非有限值时，道路提交仍可成功，但 ribbon 的左右顶点重合或几何计算失效，形成逻辑存在、视觉不可用的道路。

### 根因分析

`RoadConfig.RoadWidth` 没有运行时有限正数约束，`RoadRenderer` 直接把它用于 ribbon 半宽计算。

### 修复方案

`RoadConfig.NormalizeRuntimeValues()` 为 `RoadWidth` 建立正有限约束，无效值恢复为 `DefaultRoadWidth = 12` 并报告警告。`RoadRenderer._Ready()` 与共享同一资源的 `RoadBuilder._Ready()` 都在构造运行时组件前调用规范化，避免初始化顺序决定结果。

### 影响范围

影响无效道路配置的运行时降级；合法自定义宽度、资源序列化字段和道路提交规则不变。`CellSize` 的同源输入问题记录在 `tool-input:BUG-2`。

---

## 验证状态

- `RoadRendererLifecycleContractTests` 覆盖已有边同步、退出树解绑、端点/路口半径选择，以及直通二度节点、二度转弯和端点的拓扑分类。
- `road_renderer_lifecycle_runtime_contract.gd` 释放真实 `RoadRenderer` 后加载有效槽位成功，输出 `PASS road renderer lifecycle runtime contract`，不再访问 disposed mesh。
- `road_input_strategy_runtime_contract.gd` 把 `RoadWidth` 和 `CellSize` 设为 0，确认两者恢复为正值、道路可提交且 mesh 顶点数大于 0，输出 `PASS`。
- `road_system_v2_final_runtime_contract.gd` 和 `road_curve_rendering_runtime_contract.gd -- --snap-only` 均输出 `PASS`；5 个改动 GDScript 的诊断均为 0。
- `dotnet test SimpleCities.sln --no-restore`：492/492 通过；`dotnet build SimpleCities.sln --no-restore`：0 警告、0 错误。Roslyn CodeLens 为 0 error、0 warning，Godot editor 错误日志为 0。
- `road_rendering_performance_contract.gd` 在 10k/100k Edge 数据集上约三分钟没有完成输出，随后被终止；性能门未验证通过，不能由上述功能契约替代。
- headless Godot 的 Windows root certificate store 读取失败和独立场景中 `ConstructionDock` 缺少 `ToolManager.Instance` 属于环境/夹具输出；相关契约均以明确 `PASS` 结束。
