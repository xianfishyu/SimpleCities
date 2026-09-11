## Parent

https://github.com/xianfishyu/SimpleCities/issues/1

## What to build

道路在主格点穿越或接入已有道路时形成正确路口，玩家看到并能查询同一份连接关系。

## Acceptance criteria

- [ ] 十字、T 形和合法主格点斜向接入创建或复用正确结构节点，旧路段按交点切分并一次发布。
- [ ] 端接按道路身份与端点角色区分，稳定读取路口方向和几何转向描述，不引入交通许可。
- [ ] 路口表面与连接道路的 owner、位置及 token 一致，视觉交叉不是孤立的重叠网格。
- [ ] 保存重载保持相同连通性和规范内容，重新读取不会新增重复路口。
- [ ] 核心公开查询和真实场景验证允许交叉、拒绝区间重叠以及失败不部分发布。

## Blocked by

- https://github.com/xianfishyu/SimpleCities/issues/8 — V4-07 共线重叠标红并整笔拒绝

<!-- simple-cities:v4-ticket:08 -->
