# 第三代道路系统下一步执行计划

> 整理日期：2026-08-16
> 目的：按依赖和风险排序，给出 V3 重构剩余工作的具体下一步，避免重复扫描路线图。
> 权威工作项仍以 `docs/todo/v3/` 为准。

## 1. 恢复验证环境

- 恢复 NuGet 包缓存或网络后，依次执行：
  - `dotnet restore SimpleCities.sln`
  - `dotnet build SimpleCities.sln`
  - `dotnet test tests/SimpleCities.RoadGraph.Tests/SimpleCities.RoadGraph.Tests.csproj`
  - `godot --headless --path . --script tests/godot/command_center_runtime_contract.gd`
- 目标：重新确认 1254+ 测试、0 警告/0 错误、`PASS command center runtime contract`。
- 离线恢复选项：
  - 从同版本开发机复制 `%USERPROFILE%\.nuget\packages` 到当前用户；
  - 或使用本地 NuGet 源：`dotnet restore SimpleCities.sln --source D:\path\to\packages`；
  - 或从 CI/缓存恢复 `project.assets.json` 对应的包目录。
- 网络源不可达时可尝试镜像：
  - `dotnet restore SimpleCities.sln --source https://nuget.cdn.azure.cn/v3/index.json`
  - `dotnet restore SimpleCities.sln --source https://mirrors.cloud.tencent.com/nuget/v3/index.json`
- 若 restore 无错误信息但退出码非 0，可尝试：
  - `dotnet restore SimpleCities.sln --disable-parallel`
  - 清理 NuGet 缓存后重试：`dotnet nuget locals all --clear`

## 2. 完成 `v3-ui:1.2` / `v3-tool-input:2.2` 的 surface hit 选择

- 用真实 `MapTest` 验证 Ribbon / Cap / JunctionPatch / SemanticJoin 的单击、连续与矩形选择。
- 覆盖 self-loop、parallel Edge、失效 render token、semantic boundary 合并后的 Edge ID 失效。
- 提交前确认 RoadGraph 不变；成功批次只产生一条历史。

## 3. 接入 query fragment 诊断

- 将 `RoadSpatialIndexV3` / `RoadQueryFragmentBuilder` 接入 `RoadGraphV3Revision` 或 facade 的派生索引。
- 在 `RoadGraphV3Diagnostics` 增加 `QueryFragmentCount`，DebugPanel 增加对应行。
- 验证 10k/100k 下局部查询不随远端增长线性退化。

## 4. 完成 `v3-ui:1.4` 异步存档状态机

- 消费 `V3SaveOperation` token/result，实现 Publish/Load/Delete 的 busy、取消与二次确认。
- 确保 Load 的 Admission/Prepare/Preflight 期间 Escape 只取消一次，commit 期间不可取消。
- 验证 V2/Foreign 槽不进入普通操作列表。

## 5. 最终组合验收 `v3-road-graph:8.6`

- 在真实 `MapTest` 中完成连续折线、环路、四类型建造/改造、撤销重做、保存/加载、表面命中与混合 junction。
- 完成 Vulkan 视觉、10k 硬门槛、100k 压测和 Windows 导出证据。
- 将最终证据写回 `docs/manuals/road-system-v3-gen.md` 附录 D。

## 6. 验收检查清单

- [ ] 环境恢复后完整 QA 通过（restore/build/test/headless 契约）
- [ ] `v3-ui:1.2` / `v3-tool-input:2.2` surface hit 选择验收通过
- [ ] `RoadGraphV3Diagnostics.QueryFragmentCount` 接入并显示
- [ ] `v3-ui:1.4` 异步存档状态机验收通过
- [ ] `v3-road-graph:8.6` 最终组合验收证据写回指南附录 D

## 7. 风险与缓解

- 环境/网络不可用导致无法 restore/build：恢复 NuGet 缓存或网络后立即重跑完整 QA。
- surface hit 选择在混合宽度 junction 上语义复杂：先复用已有 `RoadSurfaceHitTester` / `HitProvider` 测试，再补真实场景。
- query fragment 接入可能引入性能回退：以 10k/100k 基线对比，禁止回退全图扫描。
- 异步存档 UI 并发错误：统一使用 `V3SaveOperation` token/result，并用 generation 防护旧 continuation。

## 8. 里程碑

- M1：环境恢复后完整 QA 通过。
- M2：`v3-ui:1.2` / `v3-tool-input:2.2` surface hit 选择验收通过（单元测试覆盖已完成，待环境验证）。
- M3：`RoadGraphV3Diagnostics.QueryFragmentCount` 接入并显示（实现完成，待环境验证）。
- M4：`v3-ui:1.4` 异步存档状态机验收通过。
  - 准备：先梳理 PauseMenu 当前同步 Save/Load/Delete 调用点，再映射 `V3SaveOperation` 阶段到 UI busy/取消/结果状态。
- M5：`v3-road-graph:8.6` 最终组合验收证据写回指南附录 D。

## 9. 相关文档

- 架构与验收规范：`docs/manuals/road-system-v3-gen.md`
- 当前实现与验证状态：`docs/manuals/v3-current-implementation.md`
- QA 运行手册：`docs/manuals/v3-qa-runbook.md`
- 关键决策记录：`docs/manuals/v3-decisions.md`
- 术语表：`docs/manuals/v3-glossary.md`
- 代码地图：`docs/manuals/v3-code-map.md`

## 10. 完成定义

- 上述检查清单全部勾选。
- `docs/todo/v3/` 中对应工作项标记为已完成，并记录实际验证证据。
- `docs/manuals/road-system-v3-gen.md` 附录 D 写入最终组合验收证据。
- 工作树无未提交改动，所有相关提交使用仓库既定提交规范。
