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

---

<a id="road-rendering-bug-6"></a>
## BUG-6：Load Preflight 在主线程重复构建道路 surface 空间索引

> 修复日期：2026-08-14
> 影响文件：`Scripts/Road/RoadSurfaceSnapshot.cs`、`Scripts/Road/RoadRenderer.LoadCommit.cs`、`tests/SimpleCities.RoadGraph.Tests/RoadRendererLoadPrepareTests.cs`
> 关联事项：`v3-grid-rendering:2.2`、`v3-save-system:2.3`

### 症状

`RoadSurfaceSnapshot` 接入不可变 AABB 层级后，普通查询已不再逐 triangle 线性扫描，但 aggregate Load 的后台 `RoadRendererLoadPreparer.Prepare()` 仍只返回原始 `RoadSurfaceTriangle[]`。主线程 `PreflightPreparedLoad()` 随后调用 snapshot 构造函数，在创建 Godot mesh 和 node batch 的同一阶段再次复制全部 triangle 并构建空间索引。surface 越大，这段本可预先完成的派生工作越会延长主线程 Preflight 窗口，并违反“Load 在后台 Prepare 预建 surface/hit index”的契约。

### 根因分析

空间索引最初封装在 `RoadSurfaceSnapshot` 的 token 构造函数中，索引数据和 `RoadRenderToken` 没有分离。普通 mutation 从 triangle 直接构造 snapshot 时这一路径成立，但 Load 需要先在 worker 生成无 Godot 对象的派生数据，之后才在主线程创建保留 token 和 RID。`RoadRendererPreparedLoad` 暴露 triangle 数组而不是已准备的 surface 数据，使 Preflight 只能重新进入带复制和建树副作用的构造函数。

### 修复方案

`RoadSurfaceSnapshot.PreparedData` 现在私有持有 defensive-copy triangle、primitive bounds、稳定空间节点和 primitive index 数组；`RoadSurfaceSnapshot.Prepare()` 一次完成复制和建树。`RoadRendererLoadPreparer.Prepare()` 在 worker 阶段生成该不透明载荷，`RoadRendererPreparedLoad` 只携带它，`PreflightPreparedLoad()` 则用 reserved `RoadRenderToken` 直接绑定 prepared 数据，不再复制 triangle 或构建索引。普通 mutation 的原构造入口继续委托 `Prepare()`，因此 defensive-copy、精确 triangle 判定、canonical `RoadLocation` 和确定性破同值语义不变。

### 影响范围

影响 aggregate Load 的 renderer Prepare/Preflight 分工和 prepared payload 形状。普通 mutation 仍在其既有表现重建路径建立 snapshot；mesh 顶点/索引、node marker、surface owner、查询结果、存档格式和 commit/notification 顺序均不变。

## BUG-6 验证状态

- 新测试契约在旧实现上准确 RED，均为 `RoadRendererPreparedLoad` 缺少 `RoadSurface`；实现后 `RoadRendererLoadPrepareTests` 与 `RoadSurfaceSnapshotTests` 合计 27/27，通过 worker prepared 数据直接绑定 token，并在 128 条远隔道路中把点查询候选限制为 2～8 个、精确测试限制为 2 个。
- `dotnet test tests/SimpleCities.RoadGraph.Tests/SimpleCities.RoadGraph.Tests.csproj -c Debug --no-build --no-restore`：782/782 通过；Debug 与 `ExportRelease` build 均为 0 警告、0 错误；Roslyn compiler/analyzer 为 0 diagnostics。
- 隔离 `APPDATA` 的 `road_render_token_runtime_contract.gd` 输出 `PASS road render token runtime contract`。Godot MCP 在真实 `MapTest` 中验证普通 mutation、样式刷新与 aggregate Load 后 desired/presented/hit token 完全匹配；Load 命中为 `Edge 2 / geometry 0 / parameter 0.5`，`surfacePrimitiveCount=2`，旧 surface 已失效。
- GDScript workspace scan 为 0 diagnostics；Godot editor 没有新增错误，DAP `stderr` 为空。测试槽、动态探针、隔离用户目录和临时日志均已清理。headless 输出的 Windows root certificate store 错误与独立夹具缺少 `ToolManager.Instance` warning 为既有环境/夹具信息。
- 本修复验证了线程分工、查询局部性和 Load 行为，没有重跑 10k/100k Vulkan 性能契约，因此不更新既有规模数据，也不据此关闭仍缺完整 owner/工具/UI/故障矩阵的协作事项。

---

<a id="road-rendering-bug-7"></a>
## BUG-7：owner-dense 夹具的 JunctionPatch 查询点不可命中

> 修复日期：2026-08-21
> 影响文件：tests/godot/road_rendering_performance_contract.gd
> 关联事项：v3-grid-rendering:2.3

### 症状

owner-dense 10k Vulkan 契约已经完成 Load、EdgeRibbon、TerminalCap 和 SemanticJoin 查询，但在 JunctionPatch 探针附近报告 No JunctionPatch hit was found near (-7520.0, -4240.0)，因此脚本未输出 PASS。失败发生在查询阶段，不是 fixture 写入或 RoadGraph Load 阶段。

### 根因分析

夹具第一单元的三条锐角 junction Edge 使用 Dirt/Street/Arterial 宽度组合。相同的锐角几何在既有 junction runtime 契约中使用 Dirt/Highway/Street；较窄的浅东边使 patch 在该查询窗口内无法与相邻 ribbon 的表面命中区分，导致四类 owner 矩阵少一类。生产 RoadJunctionTessellator、surface tie-break 和 Load 路径均未发生变化。

### 修复方案

将 owner-dense 夹具的浅东 junction Edge RoadType 从 street 调整为 highway，保留 8 Edge/13 Node 单元拓扑、其余 Edge 类型和查询批次不变，使 JunctionPatch 保留可命中的暴露区域。

### 影响范围

只影响性能契约 fixture 的 RoadType 宽度分布；不改变生产道路图、渲染器、surface owner 或存档格式。owner-dense 10k/100k 的四类 owner 查询现在都能执行完整矩阵。

## BUG-7 验证状态

- 首次 10k 运行在三个 owner kind 完成后于 JunctionPatch 探针失败；修正后真实 Vulkan Forward+ 10k 输出 PASS，camera/preview/highlight P95 为 0.482/0.467/0.468 ms，四类 owner 均完成 20 批 × 1,000 次查询，JunctionPatch P95 为 0.065123 ms。
- 真实 Vulkan Forward+ 100k 输出 PASS，camera/preview/highlight P95 为 0.573/0.625/0.561 ms，Load/renderer rebuild 为 6135.119 ms；JunctionPatch P95 为 0.040361 ms，surface primitive 为 412,500，静态 renderer 节点为 2。
- mcp__godot_minimal__get_diagnostics 对该 GDScript 返回 0 diagnostics；Godot editor 为 Godot 4.7、MapTest.tscn、未运行，editor error 日志为 0。日志中唯一 warning 是既有 ConstructionDock: ToolManager.Instance is missing，未归因于本修复。

---

<a id="road-rendering-bug-8"></a>
## BUG-8：道路高亮被静态 mesh 覆盖且 GDScript 无法绑定 nullable hover 属性

> 修复日期：2026-08-21
> 影响文件：`Scripts/Road/RoadRenderer.cs`、`tests/godot/road_parallel_edge_runtime_contract.gd`
> 关联事项：`v3-grid-rendering:2.0`

### 症状

平行 Edge 契约可以通过 surface owner、拆除和改造验证，但给 `RoadRenderer.HoveredEdgeID` 设置 Edge ID 后 Vulkan 截图没有任何高亮变化。原契约通过 `set("HoveredEdgeID", edgeID)` 设置 C# `int?` 属性时，读回值仍为 `null`，即使绕过该绑定问题，高亮也会被静态道路 mesh 覆盖。

### 根因分析

`RoadRenderer._Draw()` 在父节点绘制预览和高亮，`_roadBatchLayer` 却以相同的 `ZIndex` 作为子节点随后绘制，静态 ribbon 因而覆盖父级 overlay。与此同时，Godot GDScript 对 public C# nullable 属性的 Variant setter 没有把整数稳定转换回 `int?`，测试脚本和外部脚本不能可靠驱动悬停状态。

### 修复方案

将静态道路 mesh 的相对 `ZIndex` 调为 `-1`，使父节点 `_Draw()` 的预览/高亮位于 mesh 上方，节点 marker 继续保留更高层级。新增 `SetHoveredEdgeID(int)` 和 `ClearHoveredEdgeID()` 作为可绑定入口，并在入口内统一触发 `QueueRedraw()`；生产 `RoadBuilder` 的直接属性路径保持不变。

### 影响范围

影响道路悬停、拆除和改造预览的可见层级以及 GDScript 驱动的高亮测试入口。道路 mesh 几何、surface owner、命中 token、保存格式和普通/Load 接管协议不变。

## BUG-8 验证状态

- 修复前的真实 Vulkan 复现：平行 Edge base/first/second 三张图字节大小相同，区域差值均为 `0`；契约在高亮隔离断言处失败。
- `dotnet build SimpleCities.sln --no-restore`：成功，0 个警告，0 个错误。
- `godot --headless --path . --check-only --script tests/godot/road_parallel_edge_runtime_contract.gd`：脚本检查通过。
- 隔离 `APPDATA` 的真实 Vulkan `godot --path . --script tests/godot/road_parallel_edge_runtime_contract.gd`：输出 `PASS road parallel edge runtime contract`；高亮区域差值为 first `62.125491`、second `36.211765`，两侧交叉区域均为 `0`；缩放 `0.25/4.0/1.0`、同槽 Load 重建后的 geometry、mesh vertex count 和 surface owner 均保持稳定，并写出 `.godot/qa-road-parallel-edge-visual.png`。
- 运行日志唯一 warning 为既有 `ConstructionDock: ToolManager.Instance is missing`，未归因于本修复；本轮未暴露 Roslyn CodeLens、Godot editor MCP 或 DAP 工具，未将这些门禁声称为通过。

---

<a id="road-rendering-bug-9"></a>
## BUG-9：共用路口的两个自环丢失端接表面来源

> 修复日期：2026-09-12
> 影响文件：`SimpleCities.RoadCore/RoadPresentation.cs`、`tests/SimpleCities.RoadCore.Tests/LoopRoadTests.cs`
> 关联事项：V4-10 / GitHub #11

### 症状

两条自环共用一个真实路口时，领域读取正确返回四个独立端接，但生成的路口表面缺少其中一个端接的来源。`TwoLoopsSharingOneJunction_KeepFourDistinctEndConnections` 在按每个 `(EdgeId, Role)` 检查路口表面时失败；仅检查路口位置或不同 EdgeId 数量无法发现同一条自环的起终角色被合并。

### 根因分析

路口表面用道路口两侧角点的凸包封闭。不同端接可能贡献同一坐标的角点，原路径在凸包角点去重时仅保留排序在前的来源，并据此分配表面 owner。角点坐标相同不代表端接相同，因此合法的几何去重同时丢弃了另一个端接的身份；自环 Start 与 End 共用 EdgeId 时同样必须保留角色区别。

### 修复方案

保留原凸包轮廓，按端接离开路口的方向排序，并使用相邻方向的角平分线切分既有凸包三角区域。每块按其方向分配到对应的 `(EdgeId, Role)`，角色映射到该规范道路边的参数0或1，确定性同值排序继续使用 EdgeId 与角色。修复只细分已有表面的来源区域，不通过叠加额外表面或扩大凸包补足缺失的 owner。

### 影响范围

影响V4路口表面的端接归属，包含自环和多个端接贡献重复凸包角点的场景。领域拓扑、规范道路边身份、凸包覆盖范围及存档内容不由本修复改变；领域端接读取作为本回归的外部对照。

## BUG-9 验证状态

- `TwoLoopsSharingOneJunction_KeepFourDistinctEndConnections` 先复现表面端接来源缺失，修复后通过：两条自环、四个独立incidence和十六个turn读取成立，每个端接均有对应的路口表面片。
- `dotnet test SimpleCities.sln --no-restore --verbosity minimal`：核心263/263、应用968/968通过；ExportRelease构建为0警告、0错误。
- 真实Godot 4.7 Forward+/Vulkan的 `v4_loop_runtime_contract.gd` 输出 `PASS V4 loop runtime contract`，覆盖纯环、有分支环、不同路径平行边和格心闭环，保存重载与测试槽清理均通过。具体端接owner回归由上述核心测试证明；运行时矩阵不替代该断言。日志见 `.scratch/v4-10-11-qa/v4_loop_runtime_contract.stdout.log`。
- 双轴审查均无剩余发现。本轮Roslyn、编辑器MCP及DAP工具未暴露，对应门禁未完成；本记录不据此宣称完整道路性能已验证。

---

<a id="road-rendering-bug-10"></a>
## BUG-10：V4提交后的显示失败缺少恢复入口并残留取消提示

> 修复日期：2026-09-13
> 影响文件：`Scripts/Road/V4/V4MapScene.cs`、`Scripts/Road/V4/V4MapScene.Presentation.cs`、`Scenes/V4MapTest.tscn`
> 关联事项：V4-18 / GitHub #19

### 症状

道路核心已提交但显示发布抛异常时，场景不能继续编辑，却没有可执行的手动恢复入口。单一状态栏还可能被保存结果覆盖，或残留“正在准备道路… Esc 取消”，误导玩家以为已提交道路仍可取消。

### 根因分析

原流程依赖核心与表现token不一致来阻止后续操作，未建立独立的表现失败状态与恢复流程。发布异常发生在提交后状态栏更新之前，异常处理也未统一更新所有相关提示和控件。

### 修复方案

增加独立表现状态、持久错误提示和手动重试入口，统一控制道路工具、拾取及历史操作的可用性，同时保留相机与调试保存。重试只对当前已提交snapshot重新准备、预检和发布表现，不再次执行道路规划或提交；单个worker与代次/快照核对阻止重复点击及跨新地图、Load的晚到结果。失败、重试开始和恢复成功均同步更新状态栏，不再保留提交前的Esc取消提示。

### 影响范围

影响V4表现失败后的交互与恢复，不改变道路核心数据、ID水位、历史或存档格式。正式V3场景不受影响。

## BUG-10 验证状态

- `display-red.stdout.log` 的15项中4项失败，修复后相同15项通过；最终`display-isolated.stdout.log`的92项包含worker/预检/发布失败、单飞、保存、无取消提示及跨新地图/Load的晚到结果，全部通过。
- 核心318/318、应用968/968通过，Debug/ExportRelease构建均0警告0错误，6个改动C#及2个消费者的Roslyn诊断、全方案分析器与新脚本LSP均为空。
- 2026-09-13补齐编辑器/DAP验证：临时继承V4场景驱动真实鼠标建路、触发发布失败并点击Retry，DAP输出`V4_DISPLAY_CLOSEOUT`且`passed=true`；核心和历史保持不变、表现恢复Current、保存与重试按钮完整可见。stderr为空，编辑器cursor621之后无新增错误。临时场景已清理，详见`.scratch/v4-18-qa/editor-closeout.json`。

---

<a id="road-rendering-bug-11"></a>
## BUG-11：V4新表现首帧失败时旧完整资源已被释放

> 修复日期：2026-09-13
> 影响文件：`Scripts/Road/V4/V4MapView.cs`、`Scripts/Road/V4/V4MapScene.cs`、`Scripts/Road/V4/V4MapScene.Presentation.cs`
> 关联事项：V4-18 / GitHub #19

### 症状

候选表现引用发布后首次绘制仍可能失败。普通编辑和加载若提前释放旧mesh，就无法恢复上一份可用的完整道路画面；仅在下一帧安排重绘也会留下短暂缺失的画面。

### 根因分析

原资源生命周期把“引用已发布”等同于“新画面已成功绘制”。加载表现参与者也在`CompleteCommit`释放旧资源，早于实际`frame_post_draw`确认。

### 修复方案

由View持有前一份完整资源，普通编辑和Load都在新表现实际完成绘制后才释放。候选绘制抛错时恢复旧表现引用，清除本帧已发出的失败绘图命令并同帧绘制可用旧mesh；失败状态使依赖表现的操作暂停。若旧资源也无法绘制，停止自动尝试并保留明确失败状态，不宣称设备故障下仍有可用画面。

### 影响范围

影响V4表现资源交接，包括加载表现参与者的资源清理；加载已经提交的核心、槽位和历史清空语义不回滚。表现恢复的所有权在View，SaveManager仍使用既有联合提交协议。

## BUG-11 验证状态

- 真实Vulkan故障契约对普通编辑及不同格长Load的首个失败帧作像素区域对照，覆盖旧道路不消失、新道路不混入、Load仍报告已提交及新lineage成立；重试不重复加载或增加历史。
- `display-isolated.stdout.log`：92/92通过、进程退出0、stderr为空；相邻加载39项、历史39项、选择35项及异步操作回归通过。双轴审查原Load首帧P2已关闭。
- 编辑器/DAP收尾结果见BUG-10及`.scratch/v4-18-qa/verification.md`。执行桥超时未宣称已修复，收尾采用临时场景直接驱动真实输入，未修改生产代码或重启用户编辑器。

---

<a id="road-rendering-bug-12"></a>
## BUG-12：偏移格心路口的近重合角点产生退化三角形

> 修复日期：2026-09-21
> 影响文件：`SimpleCities.RoadCore/RoadPresentation.cs`、`tests/SimpleCities.RoadCore.Tests/PrimaryJunctionPresentationTests.cs`、`tests/godot/V4DisplayPreflightProbe.cs`
> 关联事项：回退后重新执行V4-20 / GitHub #21

### 症状

在25米格长、偏移位置的合法对角交叉路口，纯表现数据含有仅相差一个binary64 ULP的两个凸包角点。它们转换为Godot使用的binary32坐标后重合，产生零面积三角形，导致整份合法路网被表现预检拒绝。

### 根因分析

路口口部沿方向延伸与法线偏移可通过不同浮点运算得到同一理论角点。凸包前的精确坐标去重无法消除其舍入差，短凸包边随后被分配owner并构造成表面三角形。错误属于表现准备，不是道路拓扑或格心身份不合法。

### 修复方案

在构建凸包之前，以`16 × 2^-52 × max(reach, |center.X|, |center.Y|, 1)`为舍入误差界合并近重合角点，再保留原凸包与端接owner分区流程。此误差界仅用于表现角点，不改变主格点/格心身份、道路几何或规划准入容差，也不放宽Godot资源预检。

### 影响范围

影响V4偏移路口表面的可表示性，包含小格长及地图边缘；道路核心状态、存档格式和资源容量上限保持不变。修复按当前源码重新复现并验证，历史提交中的旧成绩没有代替本次结果。

## BUG-12 验证状态

- 新增四档及四种偏移的`OffsetCellCenterCross_HasRepresentablePatchesForEveryIncidence`：修复前25米档失败，4个表面片转换后零面积；修复后所在路口套件11/11通过。检查转换后非退化与一致绕向、四端接来源和路口覆盖，证据为本轮`junction-red.log`、`junction-green.log`。
- 完整核心322/322、应用968/968通过；Debug/ExportRelease构建0警告0错误。真实Vulkan表现故障契约96项、历史39项及异步操作回归通过，进程退出0、stderr为空；四档240边性能夹具均通过严格codec与表现预检。
- 当前会话未提供Roslyn、Godot编辑器及DAP工具，未运行对应专项诊断；本记录仅声明上述本地编译、单元测试和真实独立Vulkan证据，不将其称为编辑器输入验证。完整范围见`.scratch/v4-20-20260921/verification.md`。
