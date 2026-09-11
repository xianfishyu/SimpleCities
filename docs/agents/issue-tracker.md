# Issue tracker: GitHub

规格和工单发布到 GitHub 仓库 xianfishyu/SimpleCities，使用 gh CLI。操作时显式指定 --repo xianfishyu/SimpleCities，并核对当前 origin。

## 工作流

- 读取工单时取得正文、评论和标签；创建前搜索现有工单，避免重复发布。
- 发布规格或切片时，每份规格或每张票创建一个 issue。多行正文先写入 UTF-8 文件，再使用 gh issue create --title "<标题>" --body-file "<正文文件>" --repo xianfishyu/SimpleCities。
- 读取使用 gh issue view <number> --comments；列表使用 gh issue list 并请求所需 JSON 字段。标签使用 gh issue edit 的 --add-label / --remove-label。上述操作均指定目标仓库。
- 工单按阻塞顺序发布。优先使用 GitHub 原生 issue dependencies；关联时使用工单 database id，不把 issue 编号或 node_id 当作 database id。
- 不支持原生依赖时，在正文的 Blocked by 中列出真实工单编号。父子关系优先使用 sub-issues；不可用时以明确的父 issue 引用表达。
- to-tickets 只发布获准的切片，不关闭或改写父 issue。开始工作前检查阻塞项是否完成。
- 分流标签取自 triage-labels.md；配置文件中的名称不代表远程标签已经创建。
- CLI 未认证或 API 操作失败时报告原因，保留本地草稿，不将其报告为已发布。

## Pull requests as a triage surface

**PRs as a request surface: no.**

## Wayfinding

wayfinder 的 map 使用带 wayfinder:map 标签的 issue，子票以 sub-issue 关联并使用对应 wayfinder:<type> 标签。读取 map 的未关闭子票及阻塞项，从无未完成阻塞且未被认领的票中选择下一项；认领、结果评论、关闭和 map 更新遵循当前任务授权。
