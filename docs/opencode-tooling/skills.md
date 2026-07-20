# 项目 Skill 说明

本文介绍 SimpleCities 仓库中的项目级 OpenCode Skill：它们何时触发、解决什么问题、会修改哪些文档，以及彼此如何配合。实际执行规则以各 Skill 的 `SKILL.md` 为准；本文是面向维护者的索引和使用说明。

## 存放与发现

项目 Skill 位于：

```text
.opencode/skills/<skill-name>/SKILL.md
```

OpenCode 在当前工作区的新会话中发现这些目录，并读取 `SKILL.md` 顶部的 `name` 和 `description` 判断何时使用。`description` 中的 `MUST USE` 表示任务匹配时必须加载该 Skill，但是否触发仍取决于当前请求和实际改动范围。

当前项目 Skill：

| Skill | 主要职责 | 典型触发时机 | 权威规则 |
|---|---|---|---|
| `godot-csharp-qa` | 验证 C#、Godot 编辑器和运行时行为 | 修改 C#、场景、资源、项目设置或用户要求 QA | [SKILL.md](../../.opencode/skills/godot-csharp-qa/SKILL.md) |
| `bugfix-recorder` | 记录已经实现并验证的 Bug 修复 | Bug 修复完成且验证通过后 | [SKILL.md](../../.opencode/skills/bugfix-recorder/SKILL.md) |
| `todo-manager` | 维护跨会话、按系统拆分的项目路线图 | 新增、评审、延期、完成或重开长期事项 | [SKILL.md](../../.opencode/skills/todo-manager/SKILL.md) |
| `git-master` | Git 历史调查和明确提交工作流 | 用户要求提交、追溯历史、blame、bisect、rebase 或 Git 证据 | [SKILL.md](../../.opencode/skills/git-master/SKILL.md) |
| `session-recorder` | 记录当前会话的本地连续性笔记 | 用户要求整理、保存或交接当前会话 | [SKILL.md](../../.opencode/skills/session-recorder/SKILL.md) |

## `godot-csharp-qa`

`godot-csharp-qa` 是验证流程 Skill，不负责决定产品需求，也不自动授权实现、提交或推送。它根据改动风险选择最小但充分的 QA 层级。规则以 [Skill 源文件](../../.opencode/skills/godot-csharp-qa/SKILL.md) 为准。

### 项目约定

- 工作区根目录包含 `SimpleCities.sln` 和 `project.godot`。
- 项目使用 Godot 4.7 C#，主场景是 `Scenes/MapTest.tscn`。
- C# 语言服务是 `csharp-ls`，通过 Oh My OpenAgent `lsp-daemon` MCP 访问，不是由 OpenCode 原生 LSP 自动启动。
- Godot 编辑器、场景和运行时检查通过 `godot` MCP 完成。
- GDScript 诊断和运行中游戏的 DAP console 通过 `godot-minimal` MCP 完成。
- C# 构建命令必须从工作区根目录运行：

```powershell
dotnet build SimpleCities.sln
```

### 触发

- 修改 `*.cs`、`*.csproj`、`*.sln`。
- 修改 `project.godot`、场景、资源、shader、插件或非第三方 GDScript。
- 修改玩法、输入、存档、渲染或其他运行时行为。
- 用户明确要求测试、验证、QA 或 smoke test。

### 不触发

- 纯文档修改。
- 纯 Git 操作。
- 只修改 OpenCode 配置或 Skill，且不影响 Godot 项目。
- 尚未实现、只在调查的行为。

### QA 层级

- **Tier 1**：对改动的 C# 文件运行 `csharp-ls` 聚焦诊断，并执行 `dotnet build SimpleCities.sln`。
- **Tier 2**：在适用的 Tier 1 基础上，验证 Godot 编辑器、场景、资源、项目设置和错误日志。
- **Tier 3**：在适用的低层级基础上，运行真实游戏场景，驱动输入或时间，检查结构化运行态、编辑器日志和 DAP console，并清理测试产物。

#### Tier 1：C# 静态检查和构建

适合 DTO、纯 helper、内部重构等能由语言诊断和编译完整覆盖，且没有运行时行为变化的改动。

必须：

1. 对每个改过的 `.cs` 文件运行聚焦 `csharp-ls` 诊断。
2. 跨文件 API 改动还要检查入口或消费者。
3. 运行 `dotnet build SimpleCities.sln`，退出码必须为 0。
4. 对比改动前后的诊断与构建输出，不接受新增且无法解释的错误或警告。

示例：修改 `SaveData` 的字段类型后，检查所有相关 C# 文件并构建解决方案，报告当次错误数、警告数和新增诊断。

#### Tier 2：Godot 编辑器和资源集成

适合场景、资源、导出属性、autoload、项目设置、shader、插件、节点路径或 Godot 生命周期接线变更。

在适用的 Tier 1 基础上，还必须：

1. 确认 Godot 编辑器连接的是当前项目。
2. 直接修改 `.tscn` 后重新加载打开的场景。
3. 修改 `project.godot`、autoload、input map、`@tool`、addon、plugin 或缓存 shader 后，按需重启编辑器。
4. 读取编辑器错误日志，确认没有新增的任务引起错误。
5. 检查 Godot 实际加载的节点、资源或项目属性，不只相信文本文件。

示例：给 `Scenes/MapTest.tscn` 增加资源引用后，重新加载场景、检查有效节点属性并读取编辑器错误日志。构建通过不能证明资源接线正确。

#### Tier 3：运行时行为

适合玩法、输入、渲染、存档和加载、计时、物理、场景切换、运行时状态、运行时错误或任何玩家可见行为。

在适用的 Tier 1 和 Tier 2 基础上，还必须执行一个真实场景：

1. 场景前清空 `godot-minimal` 控制台缓冲。
2. 停止旧运行实例，再启动正确场景；能确定初始条件时使用冻结时间。
3. 只用运行时执行工具建立测试前提，不把调试 hook 写进生产代码。
4. 使用真实输入、命名 action、原始输入或确定性时间步驱动行为。
5. 通过运行时状态、节点属性、信号、profiler 或截图观察结果。
6. 分别读取 Godot 编辑器错误日志和运行中游戏的 DAP console。
7. 停止游戏并清理临时节点、计时器、测试存档、截图和其他 QA 产物。

示例：修改道路铺设输入后，冻结启动 `Scenes/MapTest.tscn`，执行一次真实拖拽，通过运行时状态确认 `GraphEdge` 数量变化，再读取编辑器日志和 DAP console。

### 标准 QA 流程

1. **记录基线**：记录当前工作树状态、相关 LSP 诊断和 `dotnet build SimpleCities.sln` 输出。
2. **运行聚焦诊断**：检查每个改过的 `.cs` 文件，以及跨文件改动的入口或消费者。
3. **构建真实解决方案**：构建退出码必须为 0，并报告实际错误数和警告数。
4. **验证编辑器状态**：确认项目、Godot 版本、当前场景、资源和运行状态。
5. **执行运行时场景**：先定义 Precondition、Action、Observable、Pass 和 Failure，再执行行为。
6. **检查两个日志渠道**：编辑器日志用于 addon、导入和编辑器失败；DAP console 用于运行中游戏的打印、错误、警告和堆栈。
7. **清理并报告**：停止游戏、移除临时产物，并区分任务改动与既有脏文件。

### QA 证据选择

优先使用便宜、结构化、可复查的证据：

1. 运行时状态 digest 或明确节点属性。
2. 信号和字段 watch。
3. profiler 数据，用于性能合同。
4. 截图，只用于布局、颜色或渲染结果等视觉合同。

如果程序化 3D 网格黑屏、过暗、缺面或单面可见，且日志没有错误，应先做 mesh validation，再考虑灯光或材质。

### QA 完成和报告

QA 只有在所选层级的全部门槛通过后才算完成。最终报告必须说明：

- 选择的 Tier 和原因；
- 覆盖的文件和行为；
- 实际执行的诊断、构建、Godot MCP 和运行时操作；
- 真实错误数、警告数和运行时可观察值；
- 编辑器日志与 DAP console 的发现；
- 清理内容和剩余无关脏文件；
- 被阻塞或跳过的门槛及其影响。

没有执行的检查不能写成通过。Godot、MCP 或 LSP 不可用时，应报告阻塞门槛和缺少的证据。MCP 与 `csharp-ls` 的真实调用链见 [OpenCode MCP 与 LSP 架构排障指南](opencode-mcp-lsp.md)。

## `bugfix-recorder`

`bugfix-recorder` 只记录已经完成并验证的修复。调查结果、猜测、未通过验证的补丁和普通功能开发不能写成“已修复”。

### 完成门槛

写入 Bugfix 文档前必须有以下事实：

1. 症状已复现，或有等价的失败证据。
2. 根因已定位到代码或数据流。
3. 已实施最小且合适的修复。
4. 相关诊断、测试、构建或手工场景已实际执行。

### 文档路由

修复记录写入：

```text
docs/bugfix/<system>.md
```

`<system>` 表示拥有被破坏不变量的系统，而不一定是出现症状的文件。例如：

- 道路拓扑错误属于 `road-graph`。
- 保存和加载错误属于 `save-system`。
- 网格显示错误属于 `grid-rendering`。
- Godot addon 连接错误属于 `godot-integration`。

每个系统文档独立使用 `BUG-N` 编号。跨文档引用必须带系统名，例如 `save-system:BUG-2`。一个修复跨越多个独立系统时，按系统分别记录并互相引用，不创建 `misc-fixes.md` 之类的混合文档。

保存和加载相关内容迁移后只使用 `save-system`，不要再把 `save` 或 `persistence` 当作项目系统别名。

### 必要内容

记录应解释：

- 症状和最小复现条件；
- 根因及失效路径；
- 实际修复和正确性；
- 影响范围；
- 真实执行的验证及结果。

Bugfix 文档是修复后的长期证据，不代替测试和运行时验证。

## `todo-manager`

`todo-manager` 管理需要跨会话保存的项目事项，不是当前会话的临时执行清单。它可以新增、拆分、排序、延期、完成、取消、取代或重开长期工作。

### 文档路由

长期事项写入：

```text
docs/todo/<system>.md
```

按拥有需求或不变量的系统分类，不按“可能改哪个文件”分类。每份文档只能维护一个系统。跨系统工作需要拆成可独立验证的事项，用 `<system>:<id>` 表示依赖，并明确最终集成验收由哪个系统负责。

存档 schema、槽位安全、版本迁移和加载事务统一归 `save-system`，规范路径为 `docs/todo/save-system.md`。不要再使用 `save` 或旧 key `persistence` 作为系统别名。

### 事项要求

每个活动事项至少要说明：

- **Why**：当前问题、需求或依赖；
- **Where/How**：系统、文件、符号或实现策略；
- **Verification**：测试、构建、指标或手工场景；
- **Expected result**：可判定或可度量的验收结果。

模糊想法、当前会话临时步骤、重复事项和没有验收条件的任务不能加入路线图。设计文档只能作为意图，新增或关闭事项前必须核对当前源码和实际行为。

## `session-recorder`

`session-recorder` 用于保存当前会话的本地连续性笔记，不是项目 Bugfix 或 Todo 的 canonical 文档。它把会话结果写到可纳入版本控制的 `docs/session-notes/`，适合用户要求整理、保存或交接当前会话时使用。

### 输出规则

- 文件名使用 `YYYY-MM-DD-<简短中文主题>.md`，存放在 `docs/session-notes/`。`OpenCode`、`MCP`、`LSP`、`C#` 等技术名可以保留原拼写；不要使用 Windows 文件名禁用字符。同名时追加 `-2`、`-3`，不要覆盖旧记录。
- 必须区分已完成、已验证、未完成、阻塞和既有工作区状态。
- 已验证内容必须来自实际命令、诊断、测试、运行场景或工具输出。
- 不复制完整 transcript、原始日志、密钥、凭证、个人数据或不必要的机器绝对路径。
- 写入前确认 `.omo/` 仍由 `.gitignore` 忽略。

### 与其他 Skill 的边界

- 已验证 Bug 修复仍由 `bugfix-recorder` 写入 `docs/bugfix/<system>.md`。
- 跨会话项目事项仍由 `todo-manager` 写入 `docs/todo/<system>.md`。
- `session-recorder` 可以链接或摘要这些记录，但不改变它们的 canonical 状态。
- 它可以记录 Git 状态，但不执行暂存、提交、推送、重置或清理。

### 状态规则

- **Open**：使用 `- [ ]`；部分完成但未满足全部验收条件时仍保持打开。
- **Completed**：只有全部验收条件有真实证据时才能改为 `- [x]`。
- **Deferred**：记录当前延期原因、保持不变的内容和明确的重开条件。
- **Cancelled/Superseded**：保留历史原因和替代事项，不静默删除。
- **Reopened**：恢复为打开状态，记录失效假设或回归，并补充新的验证标准。

## 项目 Skill 如何配合

一次典型 Bug 修复可能按以下顺序使用：

```text
用户报告 Bug
    -> 调查并实现最小修复
    -> godot-csharp-qa 验证静态、编辑器或运行时行为
    -> bugfix-recorder 写入 docs/bugfix/<system>.md
    -> todo-manager 仅在已有事项完成或仍有后续工作时更新 docs/todo/<system>.md
    -> session-recorder 在用户要求时整理本次会话到 docs/session-notes/
```

关键边界：

- `godot-csharp-qa` 提供验证证据，但不写 Bugfix 或 Todo 记录。
- `bugfix-recorder` 只记录已验证修复，不负责维护未来路线图。
- `todo-manager` 维护未来工作和状态，不把已完成 Bug 重复包装成待办。
- `session-recorder` 保存本地会话上下文，不替代 Bugfix、Todo 或项目文档。
- 这些 Skill 都不授权 Git 提交、推送或实现用户未要求的功能。
- 当前会话的临时 todo 工具与 `docs/todo/` 是两套不同机制。

## 新会话中的使用

新会话开始后，项目 Skill 会从 `.opencode/skills/` 重新发现。为了判断是否真的可用，可以：

1. 确认对应 `SKILL.md` 文件存在。
2. 检查当前会话的可用 Skill 列表中是否出现对应名称。
3. 对匹配任务观察 Agent 是否加载了 Skill。
4. 对 `godot-csharp-qa`，还要分别确认 LSP、构建和 Godot MCP 通道实际可用，不能只依赖 Skill 被发现。

`.opencode/` 当前由项目 `.gitignore` 忽略，因此这些项目 Skill 是当前工作区的本地配置，不会自动随普通 Git clone 分发到另一台机器。`docs/session-notes/` 可以进入 Git；`docs/opencode-tooling/` 下的说明文档也可以进入 Git，但它们不能代替实际的 `SKILL.md`。

## 维护规则

- 行为规则改动应先修改对应 `SKILL.md`，再同步本说明文档。
- 本说明与 `SKILL.md` 冲突时，以 `SKILL.md` 为准。
- 不在说明文档中写机器绝对路径、永久警告数量或未执行的验证结果。
- 新增项目 Skill 时，在本文件的总览表中登记，并说明与现有 Skill 的边界。
