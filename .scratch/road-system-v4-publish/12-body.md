## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

在删除工具中选择一个道路格段并抬起，只删除该区间，保留长道路的其余部分。

## Acceptance criteria

- [ ] 删除请求使用来源绑定的位置区间，必要时切分规范道路边，不直接删除整个命中 Edge。
- [ ] 普通格段及格心路口单侧均可独立删除，未选区间和其他分支保留。
- [ ] 抬起前只有高亮，抬起后私有草稿一次发布，失败或过期请求不留下部分删除。
- [ ] 删除后的连接、端帽、owner 与位置更新一致，旧位置不能继续按旧 Edge 解释。
- [ ] 通过公开核心操作、真实点击和保存重载验证局部删除；多选批量执行留给第14票，不逐格提前写入。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/12 — V4-11 格段预选、拖选与高亮

<!-- simple-cities:v4-ticket:12 -->
