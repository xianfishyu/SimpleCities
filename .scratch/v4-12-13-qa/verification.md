# V4-12/13 单格段删除与类型改造验证

2026-09-12；GitHub #13、#14；审查基线 `8da0bcf36c662f71350b220ee1d39ef5369666c8`。

## 逐项验收

| 工单要求 | 实现与证据 |
| --- | --- |
| #13 来源绑定的局部删除 | `PlanRemove(RoadGridSpan)` 在私有草稿切分；`RemoveMiddleGridSpan_PreservesBothRemainingRoadsAndPublishesOnlyOnCommit` 验证1000米长路仅删400～500米，提交前活动快照不变。 |
| #13 普通格段、格心单侧与分支 | 核心 `CellCenterJunction_EditAffectsOnlySelectedHalfAndPreservesOtherBranches`、`RemovingBranch_RejoinsRemainingRoadAndRemovingLastRoadLeavesAnEmptyNetwork` 通过；实机 middle_span_removed、center_delete_preserves_other_three_branches 通过。 |
| #13 抬起前仅高亮，失败不部分发布 | 实机 press_only_previews、drag_only_previews_current_span、escape_then_release_does_not_delete、后台取消及迟到结果拒绝通过；核心foreign/stale/cancelled/superseded请求不改活动状态。 |
| #13 连接、端帽、owner与旧位置 | 核心旧位置Resolve返回空、分支删除后重新合并；实机 cut_caps_have_current_distinct_owners 验证缺口两侧端帽及当前token，重载后继续可命中。 |
| #13 核心、真实点击、保存 | 上述公开核心测试和 `v4_single_span_edit_runtime_contract.gd` 真实Input事件；removed_middle与center_local_edits均保存、Load、重存字节一致并删除测试槽。 |
| #14 四类型双向局部改造 | `EveryBuiltInProfile_CanChangeToEveryOtherBuiltInProfile` 验证全部12种不同类型组合及往返；实机执行8次代表性变化，最后改回street消除边界。 |
| #14 只改选中格段 | 核心中间400～500米改造保留900米原类型；实机只改200～300米，格心只改一侧，其他分支类型保留。 |
| #14 同目标静默无变化 | 核心 `SameProfile_ReturnsSilentNoChangeWithoutPublishingOrReservingIdentities` 返回NoChange且无plan；实机无悬停/选中高亮，抬起后操作阶段、两项计时、token和codec字节均不变。 |
| #14 高亮范围与实际表现 | 实机 type_press_only_highlights_middle_span、new_width_matches_changed_span、center_change_highlight_is_one_half 通过；Highway半宽16米内命中、17米处不命中；截图 `single-span-edits.png` 已检查。 |
| #14 核心、鼠标与保存 | 全部类型变更由真实release触发；changed_middle保存重载和再次保存字节相同，修改后道路profile、格长、端接与显示来源一致。 |

额外覆盖纯环格心seam两段参数区间的删除、改造及改回纯环；建造与编辑共用 `RoadMutationDraft` 的切分、规范化及Freeze，未公开可写草稿。

范围限定：删除/改造工具本票一次选择当前一格，按住期间不写入；“选择格段”模式仍可拖选但不执行批量修改。#15批量、#16历史及后续查询/性能另行实现，正式MapTest继续使用V3。schema保持6，无存档迁移。

## 测试与构建

- `dotnet test SimpleCities.sln --no-restore --verbosity minimal`：核心275/275、应用968/968，0失败、0跳过、退出0，见 `dotnet-test.log`。
- `dotnet build SimpleCities.sln --no-restore --verbosity minimal`：Debug退出0，0警告、0错误。
- `dotnet build SimpleCities.sln --no-restore --verbosity minimal -c ExportRelease`：退出0，0警告、0错误，见 `export-build.log`。
- `godot --headless --path . --check-only --script res://tests/godot/v4_single_span_edit_runtime_contract.gd`：退出0，stderr为空。这是引擎语法检查，不是LSP检查。
- 新脚本明确排除出QA导出包，应用工程导出契约测试通过。

测试沿已确认的公开RoadNetwork、快照、codec及真实输入边界推进：删除、类型改造、同目标无变化各自先红后绿。公开候选Target同token不同内容的新增用例先报告Expected Rejected / Actual Ready，修复后通过；入口现在重新查询当前格段并核对Key、完整Ranges与Points，不扩大为全局token重构。

## 实机及审查修复

Godot 4.7 Mono、Forward+/Vulkan、NVIDIA GeForce RTX 5080、Dummy音频。执行入口：

`godot --path . --rendering-method forward_plus --rendering-driver vulkan --audio-driver Dummy --script res://tests/godot/<脚本>.gd`

- 新契约最终PID61020退出0，`review-green.stdout.log` 明确PASS，59/59行通过，stderr为空。保留tracer-red、profile-red和review-red等失败证据。
- Spec审查发现后台范围被提前清除、同目标仍高亮并启动后台两项问题；review-red实际出现4条失败，修复后原断言全部通过。接受Esc后高亮立即清除，取消清理仍等待worker；同类型操作与上次完成阶段和计时完全相同。
- 旧闭环、拖选、异步操作契约分别PID72344、72868、72532退出0并PASS，stderr为空；历史截图已恢复，日志保存在本目录。
- 截图检查确认格心一侧已删除、另一侧为宽金色公路，青色预选范围正确，灰色背景网格保留。

## Standards

最终0项硬性违规、0项剩余heuristic。初审P3重复冻结步骤已收入共用 `RoadMutationDraft.Freeze`，复核通过。

## Spec

最终0项剩余发现。后台准确范围反馈和同类型跳过两项问题已补实机回归；单格能力与后续批量/历史边界一致。

## 工具边界与清理

本轮没有Roslyn CodeLens、Godot编辑器桥接、GDScript LSP或DAP工具；聚焦语义/分析器诊断、编辑器reload与错误读取、DAP控制台门禁未完成。上述构建、独立引擎语法检查及实机状态不能替代缺失工具检查；未声称完整QA或144FPS性能验收通过。

所有运行测试槽通过正常删除入口清理，saves-v4仅剩框架 `.save-root.lock`；测试进程全部结束，只保留用户原有编辑器PID47204。修复按系统记录于 `docs/bugfix/road-graph.md#road-graph-bug-23`、`docs/bugfix/tool-input.md#tool-input-bug-4` 和 `#tool-input-bug-5`。
