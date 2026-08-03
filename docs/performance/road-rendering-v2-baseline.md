# 道路渲染 V2 性能基线

> 采集日期：2026-08-04
> 环境：Godot 4.7.stable.mono；Vulkan Forward+；NVIDIA GeForce RTX 5080；1600 x 900 viewport
> 契约：`tests/godot/road_rendering_performance_contract.gd`
> 命令：`godot --path . --log-file .godot/qa-road-rendering-performance-final.log --script tests/godot/road_rendering_performance_contract.gd -- --enforce-budget`

## 数据集与口径

- 使用真实 `Scenes/MapTest.tscn`、`SaveManager.Load` 和 `RoadRenderer`，不是隔离的渲染替身。
- 固定数据集由互不连接的水平 line Edge 组成，Edge 长 8 世界单位、间距 32 世界单位，规模为 10k 和 100k。
- 相机固定为 `0.125x`；每个规模预热 10 帧，镜头移动采样 120 帧，铺路预览和命中高亮各采样 60 帧。
- 每个样本从设置状态到 `RenderingServer.frame_post_draw`，包含场景树更新和该帧渲染提交。专用 QA 进程关闭 VSync、帧率上限和低处理器模式，避免 60 Hz 等待时间掩盖实际成本。
- 镜头、预览和高亮状态彼此隔离；draw calls、rendered objects 与 primitives 在各场景最后一个活动帧完成后立即读取。
- 记录平均/P95 帧时间、图恢复及静态批次重建时间、draw calls、rendered objects、primitives 和 `RoadRenderer` 子节点数。
- 10k 的镜头、预览和高亮 P95 必须分别不超过 16.67 ms；100k 完整记录但不参与第二代通过判定。重建时间是独立的一次性加载指标，不计入连续帧 P95。

## 优化前结果

优化前每条 Edge 使用一个 `Line2D`，节点标记由单个 CanvasItem 逐个 `DrawCircle`。10k 已超过帧预算；100k 在绘制 200k 个端点标记时触发 RenderingDevice RID 元素上限，未能完成。

| Edge | 场景 | 平均 ms | P95 ms | 重建 ms | draw calls | objects | primitives | 渲染子节点 | 结果 |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---|
| 10,000 | 镜头移动 | 30.938 | 33.426 | 226.479 | 30,002 | 30,002 | 1,300,004 | 10,001 | 未通过 |
| 10,000 | 铺路预览 | 30.639 | 32.057 | 226.479 | 30,002 | 30,002 | 1,300,004 | 10,001 | 未通过 |
| 10,000 | 命中高亮 | 30.856 | 32.586 | 226.479 | 30,002 | 30,002 | 1,300,004 | 10,001 | 未通过 |
| 100,000 | 同组场景 | 未完成 | 未完成 | 未完成 | 未完成 | 未完成 | 未完成 | 100,001（计划值） | RID 上限 |

优化前进程保留默认 VSync；由于渲染已经错过 60 Hz 刷新窗口，表中的帧时间只能作为当时的端到端观测值，不能与关闭 VSync 的优化后数值直接计算百分比。

## 批处理设计

- `RoadRenderer` 继续缓存按 Edge ID 排序的确定显示点列，高亮直接复用该缓存。
- 所有 Edge 点列构造成一个连续 `ArrayMesh` ribbon。每个采样点生成左右两个边界顶点，内部点使用有上限的 miter，索引三角形共享边界，因此曲线没有矩形段缝或圆点珠状轮廓。
- 道路 shader 只对 ribbon 两侧做基于 `fwidth` 的像素抗锯齿；端点和交叉口由一个带圆形遮罩的 `MultiMeshInstance2D` 批处理。
- 同一事件循环中的 `EdgeAdded` / `EdgeRemoved` 突发只安排一次延迟批次重建，避免批量删除或交叉拆分为每个事件重复重建全图；`GraphCleared` 仍同步完成一次全量重建。
- 静态道路渲染固定为 2 个 `RoadRenderer` 子节点，不再随 Edge 数线性增加。拆除高亮、矩形选择和施工预览仍由 `RoadRenderer._Draw()` 动态绘制。

## 优化后结果

`--enforce-budget` 最终退出码为 0，并输出 `PASS road rendering performance contract`。

| Edge | 场景 | 平均 ms | P95 ms | 重建 ms | draw calls | objects | primitives | 渲染子节点 | 10k 门槛 |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---|
| 10,000 | 镜头移动 | 0.568 | 0.788 | 159.151 | 4 | 4 | 60,004 | 2 | 通过 |
| 10,000 | 铺路预览 | 0.456 | 0.717 | 159.151 | 5 | 56 | 60,056 | 2 | 通过 |
| 10,000 | 命中高亮 | 0.369 | 0.436 | 159.151 | 4 | 4 | 60,004 | 2 | 通过 |
| 100,000 | 镜头移动 | 4.495 | 5.240 | 1,170.055 | 4 | 4 | 600,004 | 2 | 不适用 |
| 100,000 | 铺路预览 | 4.191 | 4.612 | 1,170.055 | 5 | 56 | 600,056 | 2 | 不适用 |
| 100,000 | 命中高亮 | 4.326 | 4.739 | 1,170.055 | 4 | 4 | 600,004 | 2 | 不适用 |

## 结论

- 10k 的三个连续帧 P95 都低于 1 ms，满足 16.67 ms 硬门槛。
- 100k 首次完整通过同一 Vulkan 场景，三个 P95 均低于 5.3 ms；该规模仍只作为压力记录。
- 静态/高亮帧的 draw calls / objects 从优化前 10k 的 30,002 降为 4 / 4；预览帧为 5 / 56。`RoadRenderer` 静态子节点从 10,001 降为 2，100k 保持相同对象数量。
- 10k 和 100k 的全图恢复及批次重建分别为 159.151 ms 和 1,170.055 ms。它们不是连续帧门槛，但后续若优化加载或大图编辑停顿，应以此作为回归基线。
- 真实 Vulkan 曲线截图契约同时验证 line、cubic Bézier、cubic Hermite、circular arc、clothoid 和 rational quadratic 的连续 ribbon，无段缝且不修改权威几何参数。
