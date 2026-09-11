## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

为现有保存、加载增加通用上下文与预备载荷形式，同时让 V3 场景继续完整保存和加载；这是先行重构的扩展步骤。

## Acceptance criteria

- [ ] 新增通用形式与旧入口并存，由现有 V3 适配器桥接；不引入第二个活动路网或第二次加载发布。
- [ ] 真实 V3 场景能通过新形式完成保存、预检和加载，内容与工具状态不因扩展改变。
- [ ] 加载准备失败仍保留原路网、显示和槽位；旧调用方保持可用。
- [ ] 通用契约不要求持有具体道路实体或 Godot 表现实现类型，资源及最终发布职责仍由既有层负责。
- [ ] 运行受影响的公开加载契约与适用场景检查，证明扩展步骤独立保持构建和行为正确。

## Blocked by

None (can start immediately).

<!-- simple-cities:v4-ticket:01 -->
