# Persistence system todo list

> System key: `persistence`
> Review date: 2026-07-19
> Evidence: `.omo/backups/system-doc-split/docs/todo/todolist.md`, `.omo/evidence/split-system-docs/task-3/ownership-map.json`, current workspace source, and the legacy `docs/todo/todolist.md`.
> Principle: Owns save schema, migrations, slot safety, registration lifecycle, and load transaction boundaries.

## Status Summary

| Legacy ID | Finding | Current status | Disposition |
|---|---|---|---|
<a id="persistence2"></a>
| 2 | 存档丢失 `RoadType` | 已修复；v2 命名迁移未完成 | 补兼容性测试；字段改名延期 |
<a id="persistence11"></a>
| 11 | 命名过时且 `RoadType` 视觉样式未落地 | 事实成立；RoadType 产品功能暂不需要 | 命名迁移独立处理；分级样式和类型选择延期 |
<a id="persistenceroadtype"></a>
| RoadType | 数据和存档已完成；视觉、选择与升级当前不需要 | 基础模型保留；产品功能延期 | 0.3 只补兼容回归；D5.1～D5.3 与 P6.5 等产品需求确认后启用 |
<a id="persistencesavesystem"></a>
| SaveSystem | 当前保存路径、槽名输入和整槽加载事务边界不满足生产存档安全 | 未完成 | 0.9～0.11 在 0.1、0.5、0.8 基础上补测试、路径迁移、槽名校验和整槽预检 |

### Design Coverage Matrix

| Design scope | Current fact | Related todo or baseline |
|---|---|---|
<a id="persistence111e2827dafb"></a>
| §10 迁移与存档兼容 | A/B 主体迁移已完成；旧 JSON 字段兼容存在；`SaveManager` 仍写入 `res://saves/<slotName>`、直接插值槽名、顺序恢复已注册系统，缺少版本拒绝、损坏数据保护、路径迁移、槽名边界和整槽事务 | 0.3～0.5、0.9～0.11、5.3、6.1～6.3 |

## Execution Order

### 阶段 0：建立回归保护

<a id="persistence0.3"></a>
- [ ] **0.3 固化 `RoadType` 存档往返行为（原问题 2）**
  - 性质：类型写入与旧存档回退已完成，本项仅补兼容性回归证据。
  - 当前证据：Edge 与 Group 的类型分别在 `Scripts/Road/RoadGraph.cs:204`、`Scripts/Road/RoadGraph.cs:217` 写入；恢复兼容逻辑位于 `Scripts/Road/RoadGraph.cs:746`。
  - 测试场景：Dirt、Street、Arterial、Highway 分别保存并恢复；另加载无 `Type` 的旧存档。
  - 验收：v2 往返保留全部类型；旧存档稳定回退为 `Street`。

  - Source key: `todo:item:0.3`.

<a id="persistence0.4"></a>
- [ ] **0.4 固化路网与清单的存档版本策略**
  - 当前问题：`RoadGraph` 写出 `version = 2`，manifest 写出 `schemaVersion = 1`，但 `RoadGraph.RestoreState` 和 `SaveManager.Load` 都没有依据版本执行迁移或拒绝加载。
  - 修改：为已知版本建立显式分派；缺少版本的旧存档走兼容路径；未知未来版本必须以可诊断错误失败。
  - 测试：当前 v2 路网、缺少 `version` 的旧存档、未知路网版本、未知 manifest schema。
  - 验收：支持版本走确定的迁移路径；未知不兼容版本不会被静默读取；原问题 2 的旧存档兼容保持不变。

  - Source key: `todo:item:0.4`.

<a id="persistence0.5"></a>
- [ ] **0.5 为 `RoadGraph` 恢复增加引用校验与失败保护**
  - 当前问题：`RestoreState` 在 `Scripts/Road/RoadGraph.cs:228` 先清空当前图，再直接信任存档中的 Node、Edge、Group ID；缺失端点、重复 ID、悬空 Group/Edge 引用没有统一校验。
  - 修改：先反序列化并校验临时数据，全部通过后再替换当前图；失败时保留加载前状态并返回可诊断错误。
  - 校验：所有 Edge 两端节点存在；Group/Edge 双向引用一致；实体 ID 不重复；枚举值合法；`NextID` 大于全部实体 ID。
  - 验收：损坏存档不会产生半恢复图；加载失败后原图的节点、边、Group 和 ID 分配状态不变。

  - Source key: `todo:item:0.5`.

<a id="persistence0.8"></a>
- [ ] **0.8 为 `SaveManager` 增加注销机制并绑定场景生命周期**
  - 当前问题：`SaveManager.Register` 只登记对象引用，没有 `Unregister`；`RoadSystem._Ready` 每次创建并注册新的 `RoadGraph`，场景重载后可能保留过期 saveable。
  - 修改：增加 `Unregister(ISaveable)`；`RoadSystem`、`MainCamera` 等注册者在退出树时注销对应实例；同一 `SaveFileName` 的重复活动注册需要明确拒绝或替换策略。
  - 测试：连续加载/卸载主场景两次后保存与加载。
  - 验收：注册表只包含当前场景的一份路网和相机；不会重复写同名文件，也不会调用已退出场景的对象。

  - Source key: `todo:item:0.8`.

<a id="persistence0.9"></a>
- [ ] **0.9 建立 `SaveManager` 自动化契约测试**
  - 依赖：0.1 的自动化测试入口可运行。
  - 当前问题：`SaveManager` 的保存、加载、manifest、`.tmp` 替换、缺失文件和错误返回行为缺少可重复测试；后续路径迁移和整槽事务修改没有保护网。
  - 修改：为 `Scripts/Core/SaveManager.cs` 建立可注入或可隔离的测试场景，使用测试 `ISaveable` 验证 `Register`、`Save`、`Load`、`SaveSlotExists`、`CurrentSlotName`、manifest 文件清单和失败返回，不改变当前运行时目标行为。
  - 测试：成功保存两个 saveable；manifest 缺失；manifest 列出文件但 JSON 文件缺失；saveable 恢复抛错；保存失败时返回 `false` 且不伪造成功日志。
  - 验收：单条自动化命令能稳定验证 `SaveManager` 当前契约；至少一个失败路径断言 `Load` 返回 `false` 且 `CurrentSlotName` 不更新；本项不要求 0.10 或 0.11 的新行为已经实现。

  - Related refs: `persistence:0.9`.
  - Source key: `todo:item:0.9`.

<a id="persistence0.10"></a>
- [ ] **0.10 迁移到 `user://saves` 并限制槽名边界**
  - 依赖：0.9 已锁定当前保存契约和失败路径。
  - 当前问题：`SaveManager` 当前写入 `res://saves/<slotName>`，导出后可能不可写；`slotName` 直接插入路径，缺少目录穿越、绝对路径和路径分隔符防护。
  - 修改：将生产保存根目录迁移为 `user://saves`；集中实现槽名验证或规范化，拒绝空槽名、`.`、`..`、绝对路径、含路径分隔符或平台非法字符的槽名；为现有开发槽定义一次性发现、复制、冲突处理和失败提示策略，旧路径只作为迁移来源，不作为新写入位置。
  - 测试：默认 `autosave` 写入 `user://saves/autosave`；含 `../`、反斜杠、正斜杠、绝对路径和空白槽名均失败且不创建外部目录；存在 `res://saves/autosave` 且 `user://saves/autosave` 不存在时按迁移策略处理；目标槽已存在时按冲突规则保留或拒绝。
  - 验收：所有成功保存都落在 `user://saves` 根目录内；非法槽名不会读写根目录外文件；旧开发存档迁移结果可由日志和文件存在性判定；0.9 的既有契约测试继续通过。

  - Source key: `todo:item:0.10`.

<a id="persistence0.11"></a>
- [ ] **0.11 建立整槽加载预检与提交边界**
  - 当前问题：`SaveManager.Load` 只依据 manifest 顺序匹配当前注册对象，逐个读取并立即调用 `RestoreState`；某个文件缺失或某个系统恢复失败时，之前已经恢复的系统不会自动回到加载前状态。
  - 修改：在 `SaveManager` 层引入整槽预检，先读取 manifest、校验 schema、确认所有将加载的已注册系统文件存在并可读取，再进入提交阶段；与 0.5 的 `RoadGraph` 内部预校验互补，0.11 只定义跨 saveable 的槽位边界，不重复实现 RoadGraph 引用校验。
  - 测试：manifest 缺失；manifest schema 未知；已注册系统文件缺失；第二个 saveable 恢复失败；manifest 包含未注册文件；所有文件有效时完整加载。
  - 验收：预检失败时任何已注册 saveable 都不会收到 `RestoreState`；manifest 中未注册系统的文件继续忽略且不阻断已注册系统加载；提交阶段失败时返回 `false`，`CurrentSlotName` 不更新，并且测试记录已提交系统的状态边界；RoadGraph 损坏引用仍由 0.5 的专用测试负责。

  - Related refs: `persistence:0.11`.
  - Source key: `todo:item:0.11`.

### 阶段 5：兼容性命名整理（原问题 11）

<a id="persistence5.3"></a>
- [ ] **5.3 独立处理 Junction → Node 命名迁移**
  - 范围：`JunctionRadius`/`JunctionColor` 及旧存档字段 `Junctions`、`Segments`、`Roads`。
  - 约束：命名迁移不能破坏旧 `.tres` 与旧存档兼容；必要时保留旧 JSON 字段或提供版本迁移器。
  - 验收：旧存档、旧资源可加载；新代码公共语义统一使用 Node/Edge/Group。

  - Integration owner: `persistence`.
  - Related refs: `grid-rendering:5.3`.
  - Source key: `todo:item:5.3`.

### 阶段 6：校准下一代道路设计文档

<a id="persistence6.3"></a>
- [ ] **6.3 同步活动存档 schema 与迁移策略**
  - 当前进展：`docs/persistence-plan.md` 已在 2026-07-19 同步为当前实现参考，记录 `SaveManager`、`ISaveable`、manifest、`RoadGraph` v2、`res://saves` 风险、注册生命周期和整槽事务限制。
  - 当前问题：`docs/class-reference.md`、`docs/manuals/infrastructure-guide.md` 和 `docs/game-logic.md` 的相关存档章节仍需按同一事实口径校准；运行时迁移、版本拒绝和预检事务仍由 0.4、0.5、0.9～0.11 保持开放。
  - 修改：把上述文档中的活动 schema、旧字段兼容边界、manifest 与路网版本职责、保存路径目标、槽名边界和整槽加载边界同步到源码和 `docs/persistence-plan.md`；旧 JSON 字段名作为兼容基线保留，除非另有经过测试的迁移器。
  - 验收：所有相关文档中的示例 JSON、版本分派、保存路径和加载失败语义与实际加载测试一致；旧 DTO 明确标注为遗留结构而非活动序列化入口。

  - Related refs: `persistence:0.4`, `persistence:0.11`.
  - Source key: `todo:item:6.3`.

## Deferred

No deferred item from the legacy list belongs to this system.

## Solved Baselines

<a id="persistence878b6f92c0cc"></a>
- [x] **原问题 2：Edge/Group 的 `RoadType` 已写入并兼容恢复。** `Scripts/Road/RoadGraph.cs:195`、`Scripts/Road/RoadGraph.cs:211`、`Scripts/Road/RoadGraph.cs:738`
  - Source key: `todo:baseline:878b6f92c0cc`.

## Completion Criteria

<a id="persistenceaf4fd4e8bade"></a>
- 5. 在 Godot 主场景真实完成铺路、交叉、拆除、保存、加载、非法槽名拒绝和加载失败保护验证，并记录实际观察结果；RoadType 产品功能启用后再增加类型选择和样式验证。
  - Related refs: `persistence:0.10`, `persistence:0.11`, `grid-rendering:D5.1`, `tool-input:D5.3`.
  - Source key: `todo:completion:af4fd4e8bade`.
