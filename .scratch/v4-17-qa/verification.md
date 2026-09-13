# V4-17 完整编辑状态下的异步加载验收

日期：2026-09-13。工单：[GitHub #18](https://github.com/xianfishyu/SimpleCities/issues/18)。基点：`9980cca866bd237fa423300fa7718c912213eec0`。依赖#16、#17已关闭。

## 实现与验收边界

复用严格schema 6 codec及现有SaveManager联合加载。worker准备存档和纯表现数据；network、tool、presentation及slot-target全部预检后在同一主线程提交已准备引用，不yield、不创建Resource、不做文件IO或调用用户回调。

本批修复加载期间道路输入破坏旧工具状态、必要清理依赖通知，以及预览先于加载完成时的恢复缺口。工具在Load admission期间冻结；成功提交同步切换会话和预览，Load采用新lineage、存档格长及水位并清历史。失败保留原图、历史、工具及当前槽位；预览的成功/异常完成结果统一暂存，失败解除准入后自动恢复，成功Load后清除。通知异常仍报告已提交且有warning。修复原因和结果见 `docs/bugfix/save-system.md` 的BUG-18/19/20。

## 检查结果

| 检查 | 结果和证据 |
| --- | --- |
| 基线 | HEAD为9980cca；仅用户原有docs/ui改动；初始Debug build零警告零错误，Roslyn全方案诊断为空 |
| 新核心加载验证 | V4LoadValidationTests的5类非法原始载荷全部拒绝：越界、非八方向、错误edge水位、重复profile、显式bezier字段；同时保留活动图的undo/redo及codec字节 |
| 完整 .NET 测试 | `dotnet test SimpleCities.sln --no-restore`：核心318/318、应用968/968，`full-tests.log` |
| Debug构建 | `dotnet build SimpleCities.sln --no-restore`：0警告0错误，最终`build-preview-green.log` |
| ExportRelease构建 | `dotnet build SimpleCities.sln -c ExportRelease --no-restore`：0警告0错误，`build-release.log`；新probe仅Debug编译 |
| Roslyn | 7个改动C#文件及RoadSaveParticipant、SaveManager两消费者均无诊断，`roslyn-files.json`；全方案includeAnalyzers诊断totalCount=0，`roslyn-analyzers.json` |
| GDScript LSP | 新v4_load_runtime_contract.gd诊断为空 |
| 加载运行契约 | `load-preview-green.stdout.log`：39/39 PASS，退出0、stderr空 |
| 相邻运行回归 | `v4_history_runtime_contract` 39/39；`v4_span_selection_runtime_contract` 35/35；`v4_async_operation_runtime_contract`和`v4_overlap_runtime_contract`各检查通过，均退出0、stderr空 |
| 编辑器/DAP | Godot4.7正确V4场景，真实输入建路→保存→不同格长建路和选择→加载目标，38步内通过；最终cell=50、history=0、selection=0、current=true、fixtureDeleted=true，见`editor-bridge.json` |
| 编辑器错误 | 基线cursor445无error；桥接运行和清理后cursor475无新增error |
| 视觉 | 检查历史回归保存的`history-restored-loop.png`，完整环路、面板和灰色背景正常；加载的preview/selection清理使用实际显示strokes、token及owner结构断言 |
| 清理 | 所有加载/预览gate释放，文件锁释放，临时payload/manifest恢复，两个测试槽位与编辑器测试槽删除；bridge bot清除、game停止，最终只保留用户编辑器PID47204 |

运行命令使用真实Vulkan：

```text
godot --path <repo> --rendering-method forward_plus --rendering-driver vulkan --audio-driver Dummy --script res://tests/godot/<contract>.gd
```

汇总为`runtime-summary.json`。旧overlap脚本写入上一批截图，本轮复制到本目录`overlap.png`后恢复上一批已提交原图。原有UI工作区改动不纳入提交。

## RED到GREEN

- 第一切片13项：`load-red.*`中2项失败，加载期间鼠标抬起/UI悬停/Esc清空3格选择；修复后`load-selection-green.*`相同13项全过。
- 第二切片19项：`load-notification-red.*`中首通知观察到旧工具状态；修复引用提交后`load-notification-green.*`全过。测试在任何生产通知前通过公开状态观察一致性，并注入外部通知异常；不是在引用提交里回调。
- 第三切片29项：`load-boundaries.*`全过，覆盖成功Load后释放旧preview、普通保存锁失败及重试、更新完整性信息后送入reader的未知profile拒绝；仅修改本测试创建的V4槽。
- 最终切片39项：`load-preview-red.*`两项复现预览成功被丢弃、异常提前显示；`load-preview-green.*`全过。预览先完成时连续12帧保持Load中的Pending，失败后无鼠标移动恢复当前500米请求的Ready/Rejected。

Debug测试探针首次构建的System.Environment/Godot.Environment歧义已限定类型消除；不是生产缺陷。编辑器首次输入起点落在1064×599内嵌视口的HUD内，没有建路；按实际视口修正测试相机后完成上述桥接场景。一次错误holder路径的工具查询已改为实查路径，未修改产品。

## Standards

0项硬性违规、0项剩余heuristic或正确性问题。初审P2预览恢复问题经失败回归与增量复核关闭。

## Spec

0项剩余发现。六项要求有实现及验证对应；初审P2关闭。

## 限制

不迁移或读取V3存档，不切换正式MapTest。schema6、1 MiB载荷准入和既有类型/水位校验保持原样；新增测试补齐代表性输入，不重复整个codec矩阵。没有声明144 FPS、100–300 ms或大地图加载耗时通过。道路编辑提交后的显示失败及手动重试属于下一票#19。
