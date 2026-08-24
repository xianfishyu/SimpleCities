# SimpleCities 第三代道路系统 100K 压力测试与技术原理分析

> 测试日期：2026-08-24
> 被测提交：`0b39871`
> 环境：Microsoft Windows 10 教育版 10.0.19045（64 位）；AMD Ryzen 9 9950X3D；Godot 4.7.stable.mono；Vulkan 1.4.341 Forward+；NVIDIA GeForce RTX 5080
> 契约：`tests/godot/road_rendering_performance_contract.gd`
> 定位：100K 是额外压力测试，不是 Phase 8 的通过硬门；10K 仍是 16.67 ms 连续帧门槛。

## 摘要

本文在真实 `MapTest`、V3 存档加载、`RoadRenderer` 与 Vulkan 渲染路径上，对 grid、junction-dense、geometry-dense 和 owner-dense 四类 100,000 Edge 数据集进行了独立进程测试。四档均完整写入 fixture、校验 manifest/hash、完成 aggregate Load、构建同源 mesh/surface，并通过 camera、preview、highlight 运行时断言。全部连续帧 P95 低于 `0.8 ms`，draw calls 保持 `4/5/4`，静态 renderer 节点保持 `2`；这说明批处理后的稳态渲染成本没有随 Edge 数转化为同数量级的 Godot 节点或 draw call。

压力主要集中在 Load 的 worker Prepare 阶段。geometry-dense 每条 Edge 含 8 个 line geometry segment，该阶段为 `24150.849 ms`，总 Load 为 `24429.000 ms`，约为 grid 的 `4.615` 倍；相反，四档真正交换 graph/tool/mesh/surface/token/slot 引用的 reference commit 仅为 `6.082～8.342 ms`。当前计时粒度尚未把 worker 阶段中的槽文件读取与校验、JSON/graph 准备、geometry tessellation、surface primitive 生成和索引构建彼此分开，因此只能确定该阶段及 geometry-dense 路径是瓶颈，不能直接把 24 秒归因于其中一个子步骤。后续优化应先细分这一阶段，而不是削弱原子 commit 或拆散已经保持常数节点数的静态批次。

## 测试方法

每个数据集使用独立 APPDATA，避免槽位、缓存和恢复状态互相污染；测试进程串行运行，避免 GPU、磁盘和 .NET 构建产物争用。命令形式如下：

```powershell
godot --path . --script tests/godot/road_rendering_performance_contract.gd -- `
  --dataset-kind=<grid|junction-dense|geometry-dense|owner-dense> `
  --dataset-size=100000 --enforce-budget
```

owner-dense 另加 `--measure-owner-hits`。`--enforce-budget` 在 100K 不套用 10K 的 16.67 ms 退出阈值；此处的 PASS 表示结构、token、资源、查询、渲染统计和测试清理均满足契约。每档只采集本次独立进程的单轮 Load，连续帧场景则由契约内部按固定预热和样本数统计。因此结果适合定位数量级和瓶颈，不是跨机器的稳定基准分数。

## 结果

### Load 分段与连续帧

| 数据集 | worker Prepare ms | Preflight ms | reference commit ms | aggregate commit ms | 观测 Load ms | camera P95 ms | preview P95 ms | highlight P95 ms | 静态节点 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| grid | 5114.105 | 157.581 | 7.684 | 9.880 | 5292.983 | 0.736 | 0.770 | 0.764 | 2 |
| junction-dense | 5120.324 | 131.382 | 7.800 | 10.090 | 5271.321 | 0.662 | 0.676 | 0.668 | 2 |
| geometry-dense | 24150.849 | 257.935 | 6.082 | 8.463 | 24429.000 | 0.780 | 0.741 | 0.679 | 2 |
| owner-dense | 5913.952 | 157.031 | 8.342 | 10.667 | 6091.620 | 0.656 | 0.648 | 0.655 | 2 |

以表中互不重叠的 `worker Prepare + Preflight + aggregate commit` 为分母，worker Prepare 占 `96.83%～98.91%`，Preflight 占 `1.06%～2.98%`，aggregate commit 占 `0.035%～0.192%`。reference commit 是 aggregate commit 的内部子集，不能再次加入分母；它占上述三段合计的 `0.025%～0.148%`。grid 与 junction-dense 的观测 Load 几乎相同（junction/grid 比值 `0.996`），说明固定量化 junction tessellation 和拓扑分支没有在该夹具中引入新的数量级；owner-dense 比 grid 高约 `15.1%`。geometry-dense 的 primitive 数达到 `2,000,004`，但现有分段只能把额外成本定位到 worker 阶段，仍需内部 profiling 才能区分 graph 准备、tessellation、primitive materialization 与索引构建的占比。

### owner-dense surface 查询

每种 owner 执行 20 批、每批 1,000 次匹配查询。表中的每个样本先用一批总耗时除以 1,000，P95 再从 20 个“摊销单次均值”样本计算；它不是 20,000 次独立调用延迟的 P95：

| owner kind | 20 批摊销单次均值 P95 ms | 20 批摊销单次均值的平均 ms | surface primitives |
|---|---:|---:|---:|
| `EdgeRibbon` | 0.038881 | 0.017837 | 412,500 |
| `TerminalCap` | 0.017858 | 0.017811 | 412,500 |
| `SemanticJoin` | 0.110267 | 0.026442 | 412,500 |
| `JunctionPatch` | 0.017454 | 0.019619 | 412,500 |

即使 surface primitive 超过 40 万，四类查询的批量吞吐仍对应亚毫秒级摊销单次均值。`SemanticJoin` 的 20 批均值 P95 最高，为约 `0.11 ms`；这支持“先用 AABB 层级裁剪候选，再做 triangle/disc 精确测试”的设计，而不是按全体 primitive 逐项测试，但不构成单次调用尾延迟承诺。

## 技术原理分析

### 1. 线性覆盖校验替代逐引用全索引扫描

BUG-21 的关键修复位于 `RoadGraph.AssertInvariants()` 与 `UniformGrid.HasExactCoverage(...)`。前者先以引用 identity 汇总每个 Node/query fragment 的预期 bounds；后者为每个引用计算一次 bucket coverage，再单次扫描实际 bucket entries，同时拒绝缺失、额外、错桶、同桶重复和内部计数不一致。复杂度由近似 `引用数 × bucket 数` 降为 `预期引用数 + 实际 bucket entry 数`，因此严格 Debug invariant 可以继续保留，而不必用关闭诊断换取大图可用性。

### 2. 不可变 revision 把捕获成本和派生计算分离

`RoadGraphRevision` 持有不可变 Node/Edge map、空间索引快照、lineage/revision/sequence 与容量计数；`CaptureRevision()` 只返回当前 root 引用。保存和 renderer 因而能在主线程 O(1) 捕获一致状态，把 JSON、采样、tessellation 和索引构建交给 worker。结构共享同时让局部 mutation 复制受影响的实体和 bucket 页，而不是复制远端全图。

### 3. worker、Preflight 与原子 commit 分层

Load 先在 worker 中完成槽文件读取与校验、graph 解析以及纯 CLR presentation/surface 准备，再在主线程 Preflight 创建隐藏 `ArrayMesh`、`MultiMesh` 和不可抛 commit plan。`PreparedAggregateLoad.Commit()` 在进入 commit 前和 `CrossCommitBoundary()` 内各复核一次 participant generation，随后按固定顺序交换引用、`MarkCommitted()`，最后才隔离 observer 与 cleanup warning。

100K 数据验证了这种分层的成本分布：geometry-dense 的 24 秒几乎全部位于 worker Prepare，而四方 reference commit 仍约 6 ms。该分段不能继续区分 worker 内部 I/O、反序列化、graph 与 renderer 准备的比例。长总耗时不应被描述为“主线程 24 秒 commit”；同样，也不能因为 commit 很短就隐瞒玩家等待完整 Load 的总时间。

### 4. 常数节点批处理隔离稳态帧成本

`RoadRenderer` 将全部道路 ribbon 合并为一个 `ArrayMesh`，节点标记使用一个 `MultiMeshInstance2D`；动态 preview/highlight 只增加固定少量 draw/object。四档 100K 均保持静态子节点 `2`、draw calls `4/5/4`，所以连续帧 P95 仍低于 0.8 ms。全量重建随 primitive 数增长，但稳态场景树和提交批次数不随 Edge 数线性增长，这正是“Load 慢、镜头移动仍快”能够同时成立的原因。

### 5. 同源 surface 与 AABB 索引避免视觉/交互分叉

mesh 构建同步生成 `EdgeRibbon`、`TerminalCap`、`SemanticJoin`、`JunctionPatch` 的 owner、canonical `RoadLocation` 与 bounds。`RoadSurfaceSnapshot` 使用 leaf capacity 8 的平衡空间层级，并以固定深度栈裁剪不相交节点；只有候选 primitive 才执行精确距离或矩形相交测试。工具 hover、拆除和 RoadUpgrade 因而查询与已呈现 mesh 同源、同 token 的 surface，而不是另建中心线近似或按端点去重平行 Edge。

### 6. 六分量 token 阻止异步结果混代

`RoadRenderToken` 同时绑定 `SceneGeneration`、`GraphFacadeID`、`GraphFacadeGeneration`、`ChangeSequence`、`RoadStyleRevision` 与 `RenderRequestID`。普通 rebuild 只有完全等于当前 desired token 才能提交；Load reservation 也必须在 Preflight 与 commit 时保持 current。这个机制把“结果是否仍属于当前世界”变成精确值比较，避免旧场景、旧图、旧样式或旧请求的异步结果覆盖新状态。

## 限制与后续方向

- 本轮只代表一台 Windows/NVIDIA 机器和一次独立进程样本；100K 没有固定硬阈值，不能据此承诺所有硬件的时延。
- fixture 强调道路拓扑、geometry 和 surface，不包含交通模拟、建筑、车辆或完整城市渲染负载。
- geometry-dense 的 24 秒总 Load 是明确的体验风险。下一步优化应先测量槽读取/hash、JSON/graph prepare、geometry sampling、tessellation、primitive materialization 和 AABB build 的内部比例，再考虑并行纯 CLR Prepare、缓存或有界分块；任何方案都必须保留 canonical geometry、同源 owner、token 与 aggregate 原子性。
- 分块 mesh 只有在测量证明单批次的资源创建或局部更新成为主要瓶颈时才值得引入；当前连续帧、draw call 和节点数已经不是 100K 的主矛盾。
- 日志中的 `ConstructionDock: ToolManager.Instance is missing` 来自性能契约直接实例化场景的既有夹具边界；四档退出码均为 0，但不把原始 stderr 宣称为全局干净。

## 结论

第三代道路系统的扩展性不是来自单一算法，而是来自不变量、状态所有权和执行阶段的共同约束：线性严格校验保证正确性可保留；不可变 root 提供一致的低成本捕获；worker/Preflight/commit 分层限制主线程原子区；批处理维持常数节点与 draw call；同源 surface 索引让工具查询保持局部；六分量 token 与 aggregate commit 防止异步混代。

100K 结果表明这些边界在规模上仍成立，但也清楚暴露 geometry-dense Prepare 是当前最值得优化的路径。性能工作应围绕这一已测瓶颈推进，而不是牺牲严格不变量、原子加载或 mesh/surface 同代语义。
