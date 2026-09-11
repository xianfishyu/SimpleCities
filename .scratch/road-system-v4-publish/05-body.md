## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

在 V4 地图按下、拖动、松开建造一条独立道路，看到正确路面，能拾取来源位置并保存重载。

## Acceptance criteria

- [ ] 主格点支持八方向、跨多格的单笔直线预览，松开一次提交，成功后结束手势；无有效长度不保留旧多次点击会话。
- [ ] 道路位置采用米制 binary64，Godot 显示采用显式转换；四种内置道路类型能够创建并显示对应样式。
- [ ] 请求经私有 Plan 和唯一 Commit 发布，实体身份、来源 token 与单次变更结果可通过公开读取验证。
- [ ] 纯表现准备和可执行资源预检置于提交前，画面及表面命中使用同一份已发布几何和版本。
- [ ] 核心公开行为、真实输入到画面、当前格式保存重载均验证一条独立道路的端点、类型和位置。
- [ ] 隔离验证支持本票的独立道路范围，不把尚未交付的交叉或合并伪装成已完成。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/5 — V4-04 创建并保存 V4 空地图

<!-- simple-cities:v4-ticket:05 -->
