## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

道路计算期间画面仍响应，超时显示等待；玩家在提交前按 Esc 能真实取消这一笔操作。

## Acceptance criteria

- [ ] 规划及纯表现准备由命令触发，主线程继续响应相机与光标；延后调用不能冒充后台执行。
- [ ] 从松开提交到结果绘制记录端到端时间，超过300 ms继续处理而不自动失败；等待阶段提示与真实状态匹配。
- [ ] 选择或预提交阶段 Esc 可取消，无需等到300 ms；已接受取消的晚到结果不得发布。
- [ ] 最终同步提交与取消有唯一先后判定；已提交后 Esc 不恢复旧路网，也不自动调用 undo。
- [ ] 清理未完成可显示正在取消，只有安全后恢复输入；同一时刻不排队另一笔道路写入。
- [ ] 公开状态与真实场景验证取消/提交竞争、长计算和晚到结果，资源创建仍限于允许线程。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/6 — V4-05 建造并重载一条独立道路

<!-- simple-cities:v4-ticket:16 -->
