# V4-10/11 闭环与格段选择验证

2026-09-12，GitHub #11、#12；固定审查基线 `ff6a99634ea711d4d287738df325a84d3315afff`。

## 范围与逐项验收

| 工单要求 | 实现及实际证据 |
| --- | --- |
| #11 纯环确定 seam 和方向 | `LoopClosure_KeepsTheSmallestExistingNodeAndChoosesAStableDirection` 覆盖不同起点和反向闭合；`loop-final.stdout.log` 的 pure 通过。按最小既有 NodeId 保留 seam，不要求不同建造历史获得相同 ID。 |
| #11 自环双端接、平行边身份 | `TwoLoopsSharingOneJunction_KeepFourDistinctEndConnections` 验证四端接、十六转向；`ThreeDifferentPathsBetweenJunctions_KeepDistinctEdgeIdentitiesAndLocations` 与实机 parallel 验证三条独立 EdgeId 及位置读取。 |
| #11 分支和格心闭环 | `BranchOnClosedRoad_MovesTheOldSeamToTheRealJunction`、四档格长的 `ClosingAtCellCenter_CreatesALoopWithTwoDiagonalRolesAndOneBranch`；实机 branch、cell-center 均通过。 |
| #11 闭合路面与 owner | `PureLoopSeam_JoinsBothEndRolesWithoutEndpointCaps`、双自环 owner 回归；运行时检查路口表面拾取与唯一端角色相符，截图 `loop.png`。 |
| #11 规范保存与重载 | `LoopCodecTests` 覆盖四种形状往返、坏格式拒绝及身份水位耗尽；实机四种形状保存、Load、再次保存字节一致且测试槽删除通过。schema 为6，拒绝旧格式，不提供迁移。 |
| #12 准确区间及格心半段 | `RoadSpanQueryTests` 覆盖四档格长、普通格心折点、类型分界、真实路口和透明纯环 seam；实机 long_edge_internal_grid_hover、cell_center_halves_independent、synthetic_center_seam_is_one_wrap_span 通过。 |
| #12 预选、累积、保持与去重 | `RoadSpanSelectionTests` 及实机 held_drag_accumulates_three、hover_and_selected_distinct、release_and_leave_preserve_selection、return_drag_deduplicates 通过；截图 `selection.png` 显示黄色已选和青色悬停。 |
| #12 路口歧义与分支隔离 | 实机 junction_center_is_ambiguous、moving_toward_branch_selects_only_it、fast_drag_crosses_without_side_branches、center_crossing_only_two_diagonal_halves 通过。 |
| #12 快速轨迹不能漏选 | TraceSpans 按可见表面及主格边界分割输入轨迹；single_motion_covers_ten_spans 用恰好一个 motion 从第一格到第十格，不加入中间输入采样。 |
| #12 会话状态、来源与 Esc | 核心拒绝过期/外来位置和混合来源批次；实机 escape_clears_without_network_change、selection_preserves_codec_bytes、load_invalidates_selection_source 通过。 |
| #12 真实输入 | `v4_span_selection_runtime_contract.gd` 使用真实 Input.parse_input_event 驱动长边、格心和路口场景；通过公开状态与实际绘制数据验证，未用反射或可写内部测试入口。 |

本轮只完成两票的闭环和选择范围。正式 `MapTest` 仍使用V3，背景保持灰色方格。抬起后删除/改造、历史、性能规模和正式切换留给后续工单。

## 测试与构建

- `dotnet test SimpleCities.sln --no-restore --verbosity minimal`：核心263/263、应用968/968通过，无失败、无跳过。
- `dotnet build SimpleCities.sln --no-restore --verbosity minimal`：Debug成功，0警告、0错误。
- `dotnet build SimpleCities.sln --no-restore --verbosity minimal -c ExportRelease`：成功，0警告、0错误。
- 两个新增运行脚本分别执行 `godot --headless --path . --check-only --script res://tests/godot/<脚本>.gd`：退出0，stderr为空。此检查是引擎语法加载，不等同于LSP诊断。
- `git diff --check`：通过。

新增闭环能力和两个自环端接来源回归先出现失败，再通过实现修复；具体表面修复记录在 `docs/bugfix/road-rendering.md#road-rendering-bug-9`。

首次格段运行脚本同步注入事件后立即读取状态，读到了尚未分发输入的旧状态。失败日志保留为 `v4_span_selection_runtime_contract.stdout.log` / `.stderr.log`。仅在观察前等待process_frame后通过，保留全部原断言和单次motion条件；最终日志为 `v4_span_selection_runtime_contract-final.stdout.log`。未通过修改生产选择行为或增加中间事件隐藏失败。

## 真实运行与工具边界

Godot 4.7 Mono、Forward+/Vulkan、NVIDIA GeForce RTX 5080、Dummy音频。使用独立引擎进程：

`godot --path . --rendering-method forward_plus --rendering-driver vulkan --audio-driver Dummy --script res://tests/godot/<脚本>.gd`

- `v4_loop_runtime_contract`：四种形状全部PASS，最终PID49388退出0，stderr为空。
- `v4_span_selection_runtime_contract`：全部断言PASS，最终PID46880结束，stderr为空。
- `v4_overlap_runtime_contract`：PASS，PID22648结束，stderr为空；历史截图恢复，本轮截图另存 `overlap-regression.png`。
- `v4_async_operation_runtime_contract`：PASS，PID58732结束，stderr为空。
- 两个新增脚本和UID均排除出 QA 导出包；全量应用测试的导出契约通过。

本会话未暴露Roslyn CodeLens、Godot编辑器桥接、GDScript LSP及DAP工具；聚焦语义/分析器诊断、编辑器reload与新错误读取、DAP控制台门禁未完成。已执行上述本地构建、引擎语法检查及真实运行，不把这些结果声称为缺失门禁通过。场景和有效资源由独立运行实例加载，未声称检查了用户编辑器中的缓存状态。未运行或宣称144FPS及大规模性能验收。

## Standards

固定基线的tracked diff与新增文件经独立审查，最终0项硬性违规、0项阻断问题。已删除无调用的SetHovered兼容入口并复核。

## Spec

独立核对GitHub #11/#12、CONTEXT、ADR及指南 §4.2/§6.1，最终0项剩余发现。指南schema、实现状态和后续范围已同步。

## 清理

实机测试槽通过正常删除入口清理，saves-v4仅剩框架 `.save-root.lock`。最终进程检查仅保留用户原有编辑器PID47204，未停止或重启该编辑器；所有本轮测试进程结束。保留成功/初次失败日志及指定截图用于复核。
