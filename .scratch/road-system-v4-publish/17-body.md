## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

在具有道路、选择及历史的场景中异步加载 V4 存档，成功统一切换状态，失败保留当前地图。

## Acceptance criteria

- [ ] 规范存档包含地图格长、实体、类型目录版本与 watermark；加载使用存档参数而非当前新地图默认值。
- [ ] 严格拒绝未知/重复字段、非法类型、悬空引用、未建节点交点、越界、错误闭环及不支持的几何。
- [ ] codec 只准备目标，不改活动路网；场景、工具、表现及槽位参与者完成预检后再统一发布。
- [ ] 引用提交不 yield、不创建资源或读写文件、不调用用户回调；预检失败保留原内容与工具状态。
- [ ] 成功加载产生新 lineage，清空旧历史和选择，晚到编辑或显示结果不能跨代发布。
- [ ] 普通保存失败保留已有槽位，不读取或迁移V3存档；实际场景验证正常往返及代表性失败边界。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/16 — V4-15 64 次撤销与重做
- https://github.com/xianfishyu/SimpleCities/issues/17 — V4-16 后台等待、单笔门禁与 Esc 取消

<!-- simple-cities:v4-ticket:17 -->
