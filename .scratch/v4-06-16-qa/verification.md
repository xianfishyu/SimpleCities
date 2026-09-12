# V4-06 / V4-16 并行实现验证

2026-09-12，GitHub #7与#17，基线 `b13da5080285a323c76f17d1b25005e5cd97b5fe`。两票均只依赖已关闭#6；并行分为核心规划、codec、表现和操作控制，整合后串行执行.NET构建与真实引擎验证。

## 交付及边界

- 从开放端点正向或反向续建，同类型二度连接合并为含折点的规范道路边，普通转弯不永久保留结构节点；不同类型保留结构分界。边按端点ID定向，点序随之翻转，位置参数采用整条边弧长归一化。
- 当前仅接受一条连通、不分叉、不闭合的开放折线，可有不同profile的多条规范道路边。内部交叉、重叠、闭环或分支明确拒绝；完整重叠标红、路口和闭环继续由后续票交付。
- schema 3增加edge.points，拒绝schema 1/2，无迁移器。当前上限512节点、256边、2048链点、1 MiB载荷、JSON深度6，是本阶段显式预算，不代表最终容量或性能基线。
- 纯表现生成片段矩形、有限bevel连接及开放方帽。主线程预检后只交换资源引用，绘制与命中共享同一份binary32几何、来源edge和弧长区间。灰色背景网格保持原风格。
- 单笔操作从鼠标松开开始计时；超过300 ms继续等待，提交前提示Esc。接受取消立即禁止发布并请求后台退出，清理期间保持busy。提交后Esc不撤销；只有目标实际经过_Draw，随后首次frame_post_draw到达，才记录完成耗时并开放下一笔。

## 核心与构建

- codec首个tracer因Points未实现而编译red；操作状态tracer因V4RoadOperation缺失而red；转弯表面tracer因Pieces缺失而red，分别实施后转green。
- 最终 `dotnet test SimpleCities.sln --no-restore --verbosity minimal`：核心88/88、应用968/968，无跳过。覆盖4档格长续建、共线简化、同异类型与反向、弧长位置、非法链拒绝、schema往返、非seekable读取预算、表面连接与端帽、取消/提交先后和绘制门禁。
- Debug及 `dotnet build SimpleCities.sln --configuration ExportRelease --no-restore --verbosity minimal` 均0警告0错误；Roslyn加载5个项目，compiler/analyzer诊断为空。
- 新的两个GDScript运行契约LSP诊断为空。V4OperationWorkProbe仅Debug编译，程序集扫描Debug存在/ExportRelease不存在；探针和运行脚本从QA导出资源排除。

## 真实Forward+/Vulkan

- `v4_endpoint_continuation_runtime_contract.gd`：退出0、明确PASS；合并后2nodes/1edge，转弯外侧无缺口，反向续建和异类型分界正确，整边弧长拾取正确，保存重载一致，越界对角拖动保持方向，端帽可自然伸出界外。turning-road.png在完成绘制后取得，已检查连接和灰色网格。
- `v4_async_operation_runtime_contract.gd`：可控工作gate在后台线程阻塞，不使用Thread.Sleep伪造工作；画面持续推进且相机响应，第二笔/新地图/加载不排队。Esc分派后立即禁止发布，gate未释放时仍busy，清理后恢复；提交后Esc保留道路。场景销毁后晚到worker没有污染新场景。
- 成功操作故意等待超过350 ms，再隐藏场景：引用已提交时跨两个post_draw仍不算目标已绘制；恢复可见后真实_Draw完成才释放门禁。最终published_not_drawn、drawn、exit_safe等全部true；完成绘制391.7663 ms，即时取消观测0.1058 ms。受控故障等待不代表正常性能达标。
- 初次异步契约在Input.parse_input_event同栈读取取消状态，出现cancelling/immediate_cancel=false，但late_rejected=true。改为等待实际输入分派后一帧再观测，后台gate仍关闭，严格取消断言通过；初始日志保留为async-observation-failure。
- 旧 `v4_independent_road_runtime_contract.gd` 与 `v4_empty_map_runtime_contract.gd` 均在当前schema3/装配下再次退出0并PASS，stderr为空。截图重定向到本目录，未覆盖历史证据。

## 编辑器与验证限制

MCP确认当前V4场景，已reload资源；运行时读取phase=Idle、busy=false、格长100、presented与core一致，标题为端点续建。编辑器无错误。冻结步进一次超时，后续场景读取正常；未把超时记为成功。DAP缓冲未捕获主动打印的标记，因此该通道本次未验证，真实错误检查依据独立Godot进程stderr及结构化结果。

## Standards

初评及增量复核无硬违规或阻断建议。Phase字符串可改为枚举是非阻断建议，本轮无实际错误，不额外扩展。

## Spec

初评1项P2：取消只禁止发布，没有请求后台退出。已加入独立CTS、后台与核心循环检查点和正常取消结果，公开取消回归及真实场景通过；增量复核0项剩余问题。修复记录为tool-input:BUG-3。

## 清理

所有独立运行进程均已退出，MCP测试游戏已停止，原编辑器保留。所有临时V4槽位通过正常删除入口清理，saves-v4仅剩框架根锁文件；工作gate在完成后释放等待资源，没有改动V3用户存档。
