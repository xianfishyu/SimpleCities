## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

删除先行重构中已无调用者的旧契约，让存档协调器通过道路适配器工作，为 V4 接入提供唯一的通用装配入口。

## Acceptance criteria

- [ ] 协调器和通用预备载荷不再直接依赖 RoadGraph、RoadGraphRevision 或具体 renderer prepared 类型。
- [ ] 旧具体入口及重复转发已移除，V3 仍通过适配器提供唯一生产保存加载路径。
- [ ] 保存根与 payload 策略可由当前装配提供，不在通用协调器内硬编码道路代际。
- [ ] 真实 V3 场景正常保存加载、失败隔离和工具状态通过回归；未因收拢提前切换到 V4。
- [ ] 检查程序集和实际调用方，证明 expand–migrate–contract 三步完成且没有遗留双发布入口。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/3 — V4-02 迁移现有存档与工具调用方

<!-- simple-cities:v4-ticket:03 -->
