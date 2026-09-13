# V4-20 验证记录

日期：2026-09-13。基点 `2b942a922bc0a3303bb80287d695385ecfb001b6`，分支 `feat/road-network-v4`。范围为 #21 计时、合法当前容量夹具、性能/历史基线，以及测量中发现的格心表现退化修复。正式场景仍为 V3。

## 功能与工具门

- QA Tier 3：改变运行时计时，修复真实 Godot 路面资源准备。
- `dotnet build SimpleCities.sln --no-restore`：0 警告、0 错误，见 `build.log`。
- `dotnet test SimpleCities.sln --no-restore`：Core 322/322、Application 968/968，见 `tests.log`。
- `dotnet build SimpleCities.csproj -c ExportRelease --no-restore`：0 警告、0 错误，见 `export-release-build.log`。没有声称已导出或运行 Release 性能。
- 独立 `tests/SimpleCities.RoadCore.Performance` Release 构建：0 警告、0 错误，见 `history-release-build.log`。它有意不加入主 solution，独立控制台的 CodeLens/分析器覆盖受限，未以空项目查询冒充通过。
- Roslyn：`V4MapScene.cs`、`V4RoadOperation.cs`、`RoadPresentation.cs`、`PrimaryJunctionPresentationTests.cs`、`V4PerformanceProbe.cs`、`V4DisplayPreflightProbe.cs` 文件诊断均为空；可信主方案 `get_diagnostics(includeAnalyzers=true)` 返回 error=0、warning=0。性能 GDScript 的 LSP 诊断为空。
- 实际 Vulkan / Forward+ / RTX 5080 下 `v4_display_retry_runtime_contract.gd` 和 `v4_async_operation_runtime_contract.gd` 均退出 0。`junction-red.log` 保存修复前新增偏移格心的真实资源失败，最终 `display-regression.log` 通过；核心红绿证据见 `junction-core-*.txt`。
- 编辑器运行25米240边夹具，以实际鼠标事件新增一条四格道路，观察241条边、Drawn、当前presented token、有效分阶段计时。`editor-console-final.json` 为 DAP 输出（桥接客户端将同一条输出重复转发三次，不计为三次实验），passed=true；stderr为空。
- 编辑器错误 cursor 634→692 出现两次 LSP“Client is opening already opened file”（seq 660、691），属于多客户端工具问题；`rescan` 返回 UNKNOWN_COMMAND，临时场景仍直接从磁盘成功加载。未声称这些工具问题已修复或编辑器错误日志为空。
- 编辑器测试游戏已停止，临时 `.gd/.tscn` 已移除，仅保留 `.txt` 复现源；没有重启用户编辑器。最小执行桥仍未作为本轮证据，使用直接场景驱动输入和 DAP。

## 基线与限制

最终四档在一个 Debug GPU 进程中依次采样，`render.log` 退出0且四档passed=true；原始数据为 `render-{25,50,100,200}.json`。`summarize.py` 对原始帧数组重算P95/P99及超帧数，生成 `summary.json`，核对通过。passed只表示正确性和测量完整性。

四档均为合法240边、109节点、491链点；计时结果与硬件、窗口、手势、统计定义见 `docs/performance/v4-20-baseline.md`。每类30次操作均低于100ms，全部样本最大22.7411ms。各帧窗口P95/P99满足6.9444ms，仍有超预算帧；最高32.361ms，故未宣称稳定144FPS或目标规模达标。

`history.json` 为关闭分层编译的独立Release进程结果：100组64次历史、40次核心取消、20组未提交计划释放。修复计时器在两次堆测之间的分配后重测，计划边际保留量由205,968B校正为206,008B；历史数值未变。强制GC耗时不是取消或Dispose时长。四档Esc另有40/40成功，最大1.962ms，仅为小手势链路。

`prior-render-*.log`、`prior-run-summary.json` 是几何修复和细分计时前的探索证据，其中50米窗口与诊断编译重叠，200米出现1.154秒帧。它们不参与最终表格，不作为修复因果对照；最终采样暂停了所有任务编译和其他基准。`smoke/` 为短窗口脚本正确性验证，亦非正式基线。初始25米失败见 `fixture25-failure.*`；真实失败没有删除。

## 审查和后续

Standards：0剩余发现。Spec：初次发现阶段合并P2，补齐domain/presentation prepare/reference commit/presentation commit及加总校验后复核0剩余发现。审查包含工作区及新文件，排除用户原有UI文档。

用户确认25/50/100米各10K、200米9,680边满铺，保持帧率及响应目标。#24容量、#25长尾和规模复验、#26预算确认已发布，并作为#22正式切换的原生阻塞；#26为needs-info，建议值没有变成生产拒绝规则。#1保持开放。

保留的无关修改：`docs/ui/README.md` 与 `docs/ui/road-design-workbench/`。本次提交不会清理或纳入这些文件。
