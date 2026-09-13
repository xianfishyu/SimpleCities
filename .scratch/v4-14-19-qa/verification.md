# V4-14 / V4-19 批量编辑及局部查询验证

2026-09-13；GitHub #15、#20；固定审查基线 `387997cab35642afdaff96b74c9654f163d51070`。

## 交付与验收

| 要求 | 实现及证据 |
| --- | --- |
| 整组来源校验、去重、一次发布 | `RoadNetwork` 的批量 PlanRemove/PlanChangeProfile 先核对每个原始条目，再去重；私有草稿按规范边合并切点并整体冻结。14个批量核心案例通过，包含同token异候选、stale、取消、晚提交和后续分配失败。 |
| 保留未选区间、跨路口与返拖 | 真实一次鼠标motion跨8格，返拖仍8格；release前路网不变，后台期间保留冻结高亮，完成仅一个token变化，两端未选区间保留。 |
| 混合类型和无变化 | 十字路口四个水平格段中预先一格为Highway，拖动只高亮另外三格，改造后垂直四格仍为Street；再次拖选同目标类型无高亮、无新操作、token与计时不变。 |
| 完整领域差量 | `RoadPlan.ChangeSet` 保存变化实体及前后地图、token、水位，未变远端实体不进入delta；公开codec消费者正反向重建字节完全一致，不保存snapshot、索引或mesh。undo/redo留给后续票。 |
| 结果及保存重载 | 批量删除、混合类型改造均经真实输入、显示更新、保存、Load、重存字节相等及测试槽删除验证。 |
| 有界派生索引 | 核心100米片段、来源geometry/parameter和起终角色；snapshot缓存实体、邻接、绝对弧长。radius/bounds/segment精确判断读取原始网格几何；显示拾取读取实际绘制的float polygon与owner。 |
| 局部工作量 | 20条远端路增长前后固定点及四格拖选全部计数不变。长线段逐桶访问，不按路径AABB全覆盖或全边回退。 |
| 状态和超限 | InvalidParameters/BudgetExceeded带reason且结果无部分集合；实机raw input的不可索引坐标清空已有选择，抬起不会提交子集。 |
| 复杂拓扑和长路 | 核心覆盖自环端接、不同路径平行边、八岔路口、8公里对角DDA；既有loop和selection实机回归保护相应表现/来源。25米格长8公里道路一次motion选择320格。 |

## 公开查询指标

Vulkan Forward+，Godot 4.7 Mono，NVIDIA RTX 5080。指标是工作量，不是时间或帧率承诺。`hits`为不同Edge数量，格段数量另报。默认每次查询预算为4096桶、4096候选、16384精确检查；trace累计真实子查询工作。

| 场景 | 桶访问 | 候选片段 | 精确检查 | 整边遍历 | 命中Edge | 格段 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 1000米路上450米处拾取，增加远端道路前后均相同 | 1 | 2 | 1 | 0 | 1 | — |
| 350米至650米短轨迹，增加远端道路前后均相同 | 10 | 17 | 7 | 0 | 1 | 4 |
| 25米格长，-3987.5米至3987.5米长轨迹 | 402 | 725 | 323 | 0 | 1 | 320 |

复现入口：`v4_local_query_runtime_contract.gd`，包含20条远端路的真实建设、查询参数和完整结构结果。公开场景接口为 `QueryRoad`、`TraceRoadSpans`、`GetQueryState`。此数据交给后续 #21 冻结性能数据集及时间门；不声明144 FPS或100–300ms已经达标。当前核心容量仍为512节点、256边、2048点。

## 编译与静态检查

- 全方案 `dotnet test SimpleCities.sln --no-restore --verbosity minimal` 的应用968/968通过；首次全套发现核心Resolve的额外舍入，日志保留为 `dotnet-test.log`。修复后核心完整重跑301/301通过，见 `core-final.log`，没有放宽原精确断言。
- Debug及ExportRelease `dotnet build SimpleCities.sln --no-restore --verbosity minimal` 均退出0，0警告、0错误；见 `debug-build.log`、`export-build.log`。
- Roslyn `rebuild_solution` 编译5项目；session trust后的全方案compiler/analyzer诊断为空。16个改动C#文件逐个 `get_file_overview` 诊断为空，见 `roslyn-files.json`。
- 五个改动GDScript通过Godot LSP检查，无语法或类型错误。旧 `v4_span_selection_runtime_contract.gd` 的局部变量 `wrap` 遮蔽内建函数警告在基线已经存在；本轮仅改截图输出目录，没有修改该变量。

## 真实运行时

独立进程全部使用 `godot --path <repo> --rendering-method forward_plus --rendering-driver vulkan --audio-driver Dummy --script res://tests/godot/<contract>.gd`。汇总见 `runtime-summary.json`，每组都有原始stdout/stderr。

| 契约 | 结果 |
| --- | --- |
| v4_batch_edit_runtime_contract | 41/41，PASS，退出0，stderr空 |
| v4_local_query_runtime_contract | 41/41，PASS，退出0，stderr空 |
| v4_single_span_edit_runtime_contract | 59/59，PASS，退出0，stderr空 |
| v4_span_selection_runtime_contract | 35/35，PASS，退出0，stderr空 |
| v4_loop_runtime_contract | PASS，退出0，stderr空 |
| v4_async_operation_runtime_contract | PASS，退出0，stderr空 |

`batch-highlight.png` 已视觉检查：黄色高亮只覆盖三个待改变格段，已是目标类型的一格按原路面显示，垂直分支未高亮；灰色背景方格样式保留。旧脚本支持 `V4_QA_OUTPUT`，最终回归指向本目录，未覆盖上一批证据。

编辑器MCP确认项目路径和V4MapTest节点结构，背景实际使用 `Shaders/MapTerrain.gdshader`。成功的一次冻结场景验证以临时Node驱动真实Input事件，20帧完成建造和三格批量删除：heldThreeWithoutWrite=true、结果两条保留边、current=true、passed=true；DAP记录 `V4_BRIDGE_BATCH_RESULT`，stderr为空，editor cursor254之后无项目错误。临时Node已clear，游戏已停止。

工具边界：初次连接因另一个客户端占用而失败，用户处理后连接恢复。LSP重复检查期间editor cursor308记录一次 `Client is opening already opened file`，属于工具协议重复打开提示；cursor309之后未出现新增项目错误。最终快速启动复测中，三个过早发送的首请求超时，不记作通过。只读诊断确认run仅等待编辑器帧而不等待游戏bridge就绪；改为先确认本次DAP的 `ready to drive` 日志，再请求status/exec/step后均成功：frozen=true、新地图current=true、空图QueryRoad为Ready、推进2帧成功、DAP stderr为空、editor cursor324之后无新错误。没有修改addon或重启用户编辑器。已完成的桥接输入验证和六组最终独立运行时结果分别保留，不用其中一种代替另一种。

## 修复、审查与清理

本轮TDD先复现批量API不存在、实机拖8格仅保留1格；实现后通过。随后公共回归暴露DDA终桶越过、索引插值零半径漏选、Resolve额外舍入；双轴审查发现桶跨度int溢出。均修复并保留严格回归，详见 `docs/bugfix/road-graph.md` BUG-24–27。构造器巨幅参数的旧循环未运行，以静态算术反例确认问题。

Standards最终：0硬性违规、0剩余heuristic/正确性问题。Spec最终：0剩余代码发现。最终新增raw pointer失败清选择案例也已实机通过。

未改正式V3 MapTest、schema6或曲线范围。QA保存槽均删除，桥接测试节点已清理，最终无任务启动的Godot进程。用户原有Godot编辑器、VS构建host保留。未触碰已有 `docs/ui/README.md` 与 `docs/ui/road-design-workbench/` 用户改动。
