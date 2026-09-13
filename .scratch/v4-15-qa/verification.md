# V4-15 撤销与重做验收

日期：2026-09-13。工单：[GitHub #16](https://github.com/xianfishyu/SimpleCities/issues/16)。审查基点：`0da56ea1893c87284a0999187b91edc67f46b046`。

## 实现范围

- `RoadEditHistory` 保留至多64条领域差量和游标，undo/redo共用记录；新编辑截掉redo，超容量淘汰最旧记录。取消、失败、无变化和仅准备未提交不写历史。
- `RoadPublishedState` 将快照、历史及内容版本高水位预先构造后一次发布；没有提交后普通回调。Undo/redo恢复实体和历史内容版本，同时递增ChangeSequence并保留同lineage的ID高水位；新分支分配新内容版本。旧计划、其他network和Load之前的请求被拒绝。
- 隔离V4场景接入按钮、Ctrl+Z、Ctrl+Shift+Z、Ctrl+Y，与现有后台准备、预检和首帧绘制协调；按住手势和busy时拒绝历史请求。提交前Esc可取消，提交后Esc不恢复。
- Load更换lineage并清历史，schema保持6，历史不写入调试存档。正式MapTest仍为V3；保留灰色方格背景。

## 验证结果

| 检查 | 证据与结果 |
| --- | --- |
| RED→GREEN | `core-red.log` 记录缺少历史API的编译失败；`history-red.*`记录真实Ctrl+Z未撤销。最终相关核心和运行用例全部通过 |
| 核心历史用例 | `RoadEditHistoryTests` 12/12；覆盖64/65记录、分叉、ID和版本水位、取消与失败、过期计划、Load、闭环seam、路口两侧批量编辑和codec |
| 完整 .NET 套件 | `dotnet test SimpleCities.sln --no-restore`：核心313/313、应用968/968；`full-tests.log` |
| Debug构建 | `dotnet build SimpleCities.sln --no-restore`：0警告0错误；`build-debug.log` |
| ExportRelease构建 | `dotnet build SimpleCities.sln -c ExportRelease --no-restore`：0警告0错误；`build-release.log` |
| Roslyn | 已加载本仓库5项目；6个改动C#文件无诊断，见`roslyn-files.json`；全方案includeAnalyzers诊断totalCount=0 |
| GDScript LSP | `v4_history_runtime_contract.gd`诊断为空 |
| 新运行契约 | `history-final.stdout.log`：39/39 PASS，退出0、stderr空；按钮、快捷键、批量删改、无变化、取消、环与分支恢复、存档重载清历史 |
| 相邻回归 | 同目录`v4_*_runtime_contract.stdout.log`：批量41/41、单格59/59、选择35/35、异步操作各布尔断言通过；四进程均退出0、stderr空 |
| 编辑器与DAP | 正确V4场景、Godot4.7，重载成功；撤销按钮36像素高、tooltip和初始disabled符合配置。`editor-properties.json`和`editor-errors.json`。真实输入19帧内完成建造→Ctrl+Z→Ctrl+Shift+Z，`editor-bridge.json`记录passed、undoAndButton、current均true |
| 视觉 | `history-restored-loop.png`人工检查：1600×900中环路及面板完整，历史按钮和帮助内容未截断，灰色背景格线正常 |
| 清理 | 各脚本删除测试存档，临时bridge bot清除、game停止；只保留原用户编辑器PID47204，无任务游戏进程 |

运行契约采用真实Forward+/Vulkan与Dummy音频：

```text
godot --path <repo> --rendering-method forward_plus --rendering-driver vulkan --audio-driver Dummy --script res://tests/godot/<contract>.gd
```

所有汇总见`runtime-summary.json`。批量旧脚本将截图写到上一批目录，本次运行后复制为本目录`batch-highlight.png`并恢复上一批已提交原图。

## 修复及测试诊断

`tool-input:BUG-6`：首帧完成时busy已释放，按钮要等下帧_Process才启用。修复前11项中2项失败，`history-buttons-fixed.*`中原断言11/11通过；生产代码在完成绘制、操作清理及新建地图时同步控件状态。没有增加测试等待。

`history-batch.*`的两项失败来自测试用旧token比较undo/redo后的快照；undo/redo应递增ChangeSequence。测试先核对完整领域内容，再捕获redo后的新token作为无变化手势基线，保持整字典相等断言。最终39/39通过；该问题不记为产品缺陷。

## Standards

最终审查0项硬性违规、0项可行动heuristic；含新增运行脚本取消、闭环与存档段。

## Spec

最终审查0项发现。64条领域历史、取消和失败、版本/ID水位、来源隔离、显示同步、Load清历史和成本边界均符合#16。

## 成本与验证边界

`RoadEditHistory.EstimatedBytes`是领域载荷系数估算：每条192、每槽32、节点每端48、边每端80加每点16字节；共享实体重复计数，不含活动快照、空间索引或绘图资源。单笔双端点直路观测为496字节。不是堆内存实测，没有采用16 MiB预算或新的单笔超限策略；代表性测量及预算仍属V4-20/#21。

异步受控等待的drawn_elapsed_ms=408.6401、immediate_cancel_elapsed_ms=0.082仅是故障测试观测，不证明144 FPS或100–300 ms性能门通过。本次没有做正式场景切换。

编辑器cursor370–383记录工具自身重复客户端连接警告，既有客户端仍正常服务；场景reload后无新error。旧LSP重复打开文件错误和旧选择脚本命名警告属于已记录基线，不归因于历史实现。DAP相同时间戳输出重复按同一事件计数，不当作多次编辑。

用户原有 `docs/ui/README.md` 与 `docs/ui/road-design-workbench/` 保留，排除本批提交。
