# V4-20 重新测量的验证记录

日期：2026-09-21。工单#21，基点`2b942a922bc0a3303bb80287d695385ecfb001b6`。开始时本地与远程同指该检查点，#19/#20已关闭，#21及#24/25/26已重新打开。只恢复并复核本票必要的计时和基线工具，未恢复后续容量或优化实现。无关`docs/ui/README.md`、`docs/ui/road-design-workbench/`保留。

## 实现与证据

新增分段时长与渲染帧计数；夹具经公开planner及严格codec，GPU样本240边。四档各独立进程、5秒预热、10秒窗口、60Hz受控输入，包含建造、撤销、重做、改造、删除和Esc；焦点/最小化逐帧记录，原始采样全部保留。独立历史CLI测四档8类场景，保持同一当前快照后释放历史测边际堆，分开记录取消与强制GC成本。

25米合法格心路口的预检阻塞已先复现、再修复：`junction-red.log`四档中25米失败；`junction-green.log`路口套件11/11通过。修复记录`road-rendering:BUG-12`，不是通过放宽预检或省略失败格长获得基线。

| 门 | 本次结果 |
| --- | --- |
| 初始Debug构建 | 0警告0错误 |
| Debug最终构建 | `build.log`：0警告0错误 |
| 全部.NET测试 | `tests.log`：Core322/322、Application968/968 |
| ExportRelease构建 | `build-release.log`：0警告0错误；不等同于Release性能采样 |
| 独立历史CLI Release | 构建0警告0错误、退出0，160组历史、40次token取消、20次计划释放，`history.json` |
| 四档真实GPU基线 | `runs.json`各进程退出0、passed=true，stderr均为空；482971帧、9944次响应。这里passed是测量完整性，不是性能目标总门 |
| 表现故障回归 | `v4_display_retry_runtime_contract`：96项通过，退出0、stderr为空 |
| 历史回归 | `v4_history_runtime_contract`：39项通过，退出0、stderr为空 |
| 异步操作回归 | `v4_async_operation_runtime_contract`：全部断言通过，退出0、stderr为空 |
| Roslyn语义及分析器 | 当前工具未暴露，未运行；使用本地编译门，没有csharp-ls替代声明 |
| 编辑器/DAP/LSP | 当前工具未暴露，未运行；独立Vulkan不冒充编辑器输入/DAP验证 |

GPU计时未与其他构建、历史测量或Godot测试并行。硬件快照、源文件与被测Debug程序集SHA256分别见`hardware.json`、`source-identity.json`。运行后保留用户编辑器PID72664，没有测试游戏残留；各回归清理其自身槽位和gate。

最终四档及冒烟的JSON归档只压缩空白，使用System.Text.Json逐元素写出并以JsonNode.DeepEquals验证前后值相等；原始数值、顺序和样本未筛选。探索轮以zip保留其原始文件。

## 复现

```powershell
dotnet build SimpleCities.sln --no-restore
& ./.scratch/v4-20-20260921/run-baseline.ps1
& ./.scratch/v4-20-20260921/summarize.ps1
dotnet test SimpleCities.sln --no-restore
dotnet build SimpleCities.sln -c ExportRelease --no-restore
```

历史CLI命令在`tests/SimpleCities.RoadCore.Performance/README.md`。回归命令使用`godot_console --path <repo> --rendering-method forward_plus --rendering-driver vulkan --audio-driver Dummy --script res://tests/godot/<contract>.gd`。

## 评审及边界

Standards初审0项。Spec初审1项P2：缺少指南要求的单笔渲染帧数；已加入输入/结果的Engine帧计数、非负整数差值验证以及Esc帧计数和token，增量代码审查通过。补字段后完整重跑四档；第一轮探索记录以zip及summary保留，最大76.053ms长尾没有隐藏。

最终Standards与Spec均无剩余发现。报告复核纠正了“所有P99低于预算”的误述，100/200米取消短窗口P99为11.609/11.920ms，明确超预算；统计数值和原始样本未改动。当前240边P95与响应门通过，仍有75个超预算帧、61.160ms长尾；10K/9680准入未实现，100K未加载。8MiB/256KiB/取消时限只是候选。#24/#25/#26继续承接后续工作，不因本报告关闭#22或父#1，也不改V3正式场景及灰色背景。
