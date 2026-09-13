# V4-18 表现预检与显示重试验收

日期：2026-09-13。工单：GitHub #19。基点：`d3f8f23804dc1d30c57553c20fbd8f3cd78266a7`；依赖#18已关闭。

## 结果与范围

普通编辑提交前检查表面owner、弧长区间与完整ribbon覆盖、有限凸多边形、顶点/索引和目标派生资源上界，再创建并校验Godot mesh。资源准入界限由当前准备器结构推导，不代表实际内存或性能预算测量。

失败有持久提示，禁用道路工具/拾取/history，保留相机与调试保存。手动重试单飞，只准备当前已提交snapshot的表现，不改变token、ID或history。新地图/Load使旧后台结果失效。普通发布和Load均把旧资源保留到首次frame_post_draw确认；候选Draw抛错时同帧清除失败命令并绘制旧完整资源。设备故障无法保证旧资源仍可用，不宣称保存了不可绘制的画面。

## 验证

| 检查 | 结果 |
| --- | --- |
| 基线Debug构建与Roslyn | 0警告0错误；全方案诊断为空 |
| Debug构建 | `build-expanded.log`，0警告0错误 |
| ExportRelease构建 | `build-release.log`，0警告0错误；故障probe仅Debug编译并排除导出 |
| 全部.NET测试 | `full-tests.log`，核心318/318、应用968/968 |
| C#定向语义诊断 | 6个修改/新增C#文件及RoadSaveParticipant、SaveManager共8个文件均无诊断，`roslyn-files.json` |
| 全方案分析器 | `roslyn-analyzers.json`，totalCount=0 |
| GDScript LSP | 新增运行契约诊断为空 |
| 真实Vulkan故障契约 | `display-isolated.stdout.log`：92/92，通过且进程退出0、stderr为空；含25项直接资源预检 |
| 相邻真实Vulkan回归 | Load39项、History39项、Selection35项和Async operation全部通过，四进程退出0、stderr为空 |
| 编辑器场景 | V4MapTest重新从磁盘加载；检查新增Retry按钮实际属性 |
| 编辑器真实输入 | 中断前建路→注入发布失败→点击Retry恢复，核心与历史保持不变；`editor-before-ui.json`。调整保存位置后另一次真实输入到Failed，1064×599下两个按钮完整可见，`editor-ui.json` |
| 最终编辑器/DAP复核 | 2026-09-13临时继承V4场景直接驱动真实输入，建路→发布失败→点击Retry恢复，DAP输出passed=true、buttonsFullyVisible=true、saveEnabled=true；核心与历史不变。stderr为空，编辑器cursor621后无新增error；`editor-closeout.json` |

运行命令：

```text
dotnet build SimpleCities.sln --no-restore
dotnet build SimpleCities.sln -c ExportRelease --no-restore
dotnet test SimpleCities.sln --no-restore
godot --path <repo> --rendering-method forward_plus --rendering-driver vulkan --audio-driver Dummy --script res://tests/godot/<contract>.gd
```

## 失败证据与修复

首轮`display-red`在15项中4项失败：缺乏明确恢复状态、持久提示及完整工具禁用。修复后`display-first-green`相同15项通过。扩展验收89项通过后，再增加提交后/重试中/恢复成功的无Esc取消提示断言，`display-final`92项通过。

并行评审各发现一条P2：提交后异常仍残留Esc取消提示；Load在首帧确认前释放旧资源。两条均修复并经独立增量复核关闭，Standards最终0项，Spec最终0项。

嵌入式1064×599视口实测原Save按钮y=576、高40，底部超出视口；将既有Save节点移到Recovery下方，y=229、高40，Retry为y=181、高36，两按钮均完整可见。节点路径和保存行为保持一致。

中断期间编辑器与独立契约同时运行的`display-ui-final`失败11项，集中在后续Retry点击未启动及其依赖断言；保留原始日志，不将其报告为通过。在停止其他运行后，同一代码与脚本独立重跑`display-isolated`92项全部通过。尚未确定该次失败的具体外部干扰来源，不能据一次重跑宣称定位了根因。

## 工具限制与清理

中断后编辑器桥接超时。最终日志cursor621出现调试器`Max client limits reached`及`Remote debugger: Packet too large (1953394499 > 8388612 bytes)`；最后一次DAP console复核及调整布局后的编辑器完整恢复流程受阻，未声称对应门禁通过。此前已取得实际输入恢复证据、调整后的控件实际范围以及独立Godot运行的完整行为和stderr结果。没有重启或终止用户编辑器来掩盖工具问题。

编辑器测试游戏已停止；中断前bot已清除，后续bot随游戏进程退出回收。运行契约清理其测试槽位并释放所有gate，最终核对仅保留用户编辑器。用户原有`docs/ui/README.md`和`docs/ui/road-design-workbench/`不纳入提交。

本批不切换正式V3主场景，不改变schema6或灰色背景，不声明144 FPS、100–300 ms或大图资源预算已验收。

## 收尾补验：2026-09-13

DAP重新连接后能收到本次游戏的日志，但`godot_exec`和`godot_game_time`仍超时。只读排查确认DAP连接6006、LSP连接6008正确，游戏进程存活且CPU继续运行；未取得执行桥超时的确切根因，不宣称该工具已修复。

为完成行为验证，临时场景继承未经修改的`V4MapTest.tscn`，挂载仅用于验收的输入驱动Node，由编辑器正常启动，在真实渲染帧内发送鼠标事件。完成两笔建造、注入第二笔发布失败、读取禁用状态与按钮可见范围、点击实际Retry按钮并等待Current。DAP取得明确通过结果，恢复前后核心和历史完全一致；日志有重复传送但没有stderr，编辑器无新增error。保存原始DAP结果为`editor-closeout.json`，输入脚本作为`editor-closeout-source.txt`保留。没有把执行桥恢复当作此次通过的前提。

本次只补验既有代码，未改生产文件；不重复已经通过的完整测试。临时`.tscn`、`.gd`及UID已删除，游戏停止且只保留用户编辑器PID47204。修复记录补入`docs/bugfix/road-rendering.md`的BUG-10/11。至此#19的剩余编辑器/DAP门禁完成，前述中断期间失败和工具限制仍作为历史证据保留。
