# V4 #24 容量扩展验证

2026-09-13；仓库 `D:/Code/GodotProject/simple-cities`，分支 `feat/road-network-v4`；审查固定基点 `8a484cbfd1c21074571954f7c64d6bd83451d423`，范围包含本轮工作区与新增容量文件。排除用户已有 `docs/ui/README.md` 和 `docs/ui/road-design-workbench/`。

## 范围与结果

生产准入扩为32,768节点、16,384规范道路边、131,072链点和32 MiB载荷。拓扑通过半格整数候选缩小精确相交比较范围；Codec继续有界严格读取。公共生成器交付25/50/100米各10K边、200米9,680边满铺与独立320条L折线；报告见 `docs/performance/v4-24-capacity.md`。

采用项目 `godot-csharp-qa` Tier 3：容量变化影响正常存档、实际表现与编辑行为，必须有运行证据。此次未修改正式主场景和用户背景样式。

## 静态与构建

- 前后方案构建均零警告/错误。本轮最终 `dotnet build SimpleCities.sln --no-restore` 成功，见 `build.log`。
- `dotnet test SimpleCities.sln --no-build --no-restore`：Core 339/339、Application 968/968，未跳过，见 `tests.log`。
- `dotnet build SimpleCities.csproj -c ExportRelease --no-restore`：零警告/错误，见 `export-release-build.log`。
- `dotnet build tests/SimpleCities.RoadCore.Performance -c Release --no-restore`：零警告/错误，见 `performance-release-build.log`。
- Roslyn确认SimpleCities.sln为活动方案，5项目无跳过，方案已信任；8个改动C#的focused诊断和全方案分析器均为空，见 `roslyn.json`。
- 独立Performance工具的两个改动C#不在solution的CodeLens覆盖范围，未伪称已通过该门。独立Release构建及实际数据集运行通过。
- 新容量脚本与临时编辑器脚本LSP均无诊断；重复打开临时文件产生的编辑器LSP日志另记，不视为脚本语义错误。

## 边界与回归证据

新增17项Core容量测试。精确32,768节点、16,384边的正常Codec载入成功；真实满边数状态下路口/环路切分整笔拒绝。32,769节点数组使用null哨兵证明先检查数量，不宣称这些哨兵是合法实体。131,072点合法锯齿往返成功，131,073点只因点预算拒绝。非seekable载荷精确32 MiB接受、32 MiB+1哨兵拒绝，活动状态保持。`codec-red.log`保留原1 MiB入口拒绝证据，`codec-green.log`为修复后定向通过。

候选索引仍调用原ValidatePair正文；覆盖半格交叉、闭端点、长对角、正长度重叠、相邻折线、自环、公开成路口及取消。完整回归继续覆盖坏字段、地图范围、非法方向、未节点化交点、规范化与历史。

## 五套数据与真实Godot

生成器通过公共PlanBuild/TryCommit生成五套合法载荷；严格读取、再次写出逐字节一致、正常核心Load、单格删改与undo/redo/undo内容哈希均通过。四次独立Release构造记录为 `capacity-25.log`、`capacity-50.log`、`capacity-100.log`、`capacity-200.log`；200米进程另生成补充折线。

运行命令：

```powershell
& 'D:\Program Files\Godot\godot_console.exe' --path . --rendering-method forward_plus --rendering-driver vulkan --resolution 1600x900 --script res://tests/godot/v4_capacity_runtime_contract.gd
```

Godot 4.7 stable Mono Debug，实际Forward+/Vulkan 1.4.351、RTX5080、1600×900。`capacity-runtime.log` / `.exit`：退出0，`V4_CAPACITY_RESULT`为5datasets、106checks、passed=true、remainingSlot为空。`godot-result.json`保存每项断言、选择状态和操作阶段；全部严格载入、实际ArrayMesh绘制、真实按下选1格段/松开改造或删除、API撤销、正常SaveManager保存/加载/清理通过。GPU和首次绘制是实际运行结果，纯表现统计未代替此门。

`capacity-selection-red.*`保留测试选点失败：25米四分之一格位置命中路口区域，改用水平中点符合分支选择规则。L折线删除一腿可能不改变规范边数，最终断言实际位置消失并撤销恢复，保留token变化和恰好1格段断言；没有放宽生产选择或删除规则。`godot-runtime.log`是早期同类失败，不是最终结果。

## 编辑器与DAP

确认Godot4.7编辑器当前V4MapTest、停止状态。清minimal console后，由根代理通过 `godot_editor_edit run` 非冻结启动临时继承场景 `.scratch/v4-24-qa/editor_probe.tscn`，使用 `_process` 驱动真实Input事件；不使用此前超时的执行桥。子代理独立Godot MCP无连接，根代理连接正常，最终门由根代理启动/停止并取编辑器日志完成。

100米10K加载到Current；拾取(50,0)为parameter0.5、junctionNodeId0；选中范围(0,0)至(100,0)恰好1格段，显示选择状态。松开改为Highway并Drawn；Undo恢复Street、10K边、Current、undo0/redo1。`editor-result.json`与DAP stdout同token通过，stderr为空，见 `editor-dap.json`。运行前cursor847至858无新增error，见 `editor-errors.json`；较早seq820为“LSP Client is opening already opened file”工具重复打开记录。

停止游戏并确认is_playing=false；删除临时editor_probe.gd/.tscn（uid未生成）。可复现源码保留为 `editor-probe-source.txt` / `editor-probe-scene.txt`。无测试进程残留，仅原用户编辑器PID47204；五套运行存档均清理完毕。

## 审查

Standards：0项可行动问题。按AGENTS、领域文档及Fowler smell基线审阅固定基点以来差异与新增容量文件。

Spec：0项可行动问题。依据#24正文及用户确认的200米9,680边，审阅公共构造、独立边界、场景占比、加载/历史和Godot106项结果。

两轴由独立代理只读完成，不以审查代替构建或运行验收。

## 结论边界

#24容量和正确性完成，数据可供#25/#26使用；#22正式切换与父#1仍未完成。100K超过当前16,384边预算，不在本票必需规模内。

本轮不是受控性能实验。编辑器单次改造输入到首绘343.2942ms（主线程预检144.4783ms），撤销API到首绘239.5449ms，保留超300ms证据；#25仍须优化并做目标规模Release、帧长尾与连续操作复验。历史/取消产品预算由#26确认，未使用本轮提高容量推断预算通过。

#25只读预诊断发现原240边panHover每帧两次motion均触发查询，Street同类型静默跳过导致没有可见高亮；周期长尾只是分配/GC候选线索，无EventPipe或GC因果证据。已在旧基线报告澄清覆盖边界，不声称根因或修复完成。
