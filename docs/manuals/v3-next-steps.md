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
