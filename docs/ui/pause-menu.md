# 暂停菜单

`Scenes/UI/PauseMenu.tscn` 是 `GameHUD` 的全屏模态子场景，脚本为 `Scripts/UI/PauseMenu.cs`。正常游戏中由 `GameHUD._Input()` 处理 `pause_menu` 动作（默认 Esc）：将 `PauseMenu` 推入该 HUD 私有 `UIManager` 的模态栈，再调用 `PauseMenu.Open()`。打开时菜单将 `SceneTree.Paused` 设为 `true`；菜单自身使用 `ProcessModeEnum.Always`，所以暂停后仍可操作按钮和当前暂停绑定。

## 编辑器内编排

`PauseMenu.tscn` 的根节点保持可见，便于单独打开该场景设计全屏遮罩、面板和各级内容；`GameHUD.tscn` 中的 `PauseMenu` 实例则必须覆盖为 `visible = false`。普通 C# 场景脚本不会在编辑器编排 `MapTest` 时执行 `_Ready()`，因此不能只依赖 `PauseMenu._Ready()` 隐藏菜单，否则全屏遮罩会挡住主场景。运行时 `PauseMenu.Open()` 会显式恢复可见性，所以该实例覆盖不影响 Esc 打开菜单。

直接修改 `GameHUD.tscn` 后，如果该场景已经在 Godot 标签中打开，应先确认没有未保存修改，再从磁盘重载 `GameHUD` 或重新打开项目；否则编辑器仍可能显示旧的内存场景。

## 主菜单操作

| 操作 | 行为 |
| --- | --- |
| 继续游戏 | 关闭暂停菜单、弹出模态栈并恢复 `SceneTree.Paused = false`；地图、相机和当前工具保持不变。当前暂停绑定在主菜单页执行同一操作。 |
| 保存 | 调用 `SaveManager.Instance.Save("autosave")`，结果回显到暂停菜单；菜单保持暂停和打开。 |
| 读档 | 调用 `SaveManager.Instance.Load("autosave")`，结果回显到暂停菜单；菜单保持暂停和打开。 |
| 设置 | 切换至设置页。当前暂停绑定在设置页返回暂停菜单主页。 |
| 退出游戏 | 先显示确认页；确认后关闭菜单并切换到 `Scenes/MainMenu.tscn`。这是结束当前城市并返回主菜单，不会自动保存。 |
| 退出到桌面 | 先显示确认页；确认后关闭菜单并调用 `SceneTree.Quit()`，不会自动保存。 |

确认页的默认焦点在“取消”上，当前暂停绑定也会取消确认并回到暂停菜单主页。打开菜单时焦点进入“继续游戏”；关闭后，如果打开前的控件仍有效、可见且可聚焦，焦点会延后恢复到该控件。

## 设置页

设置页提供会话内音频控制，并可进入独立的按键设置页：

| 控件 | 运行时效果 | 持久化 |
| --- | --- | --- |
| 主音量 | 将 0--100% 映射为 Master 总线的 -60--0 dB，并调用 `AudioServer.SetBusVolumeDb()` | 不持久化 |
| 静音 | 调用 `AudioServer.SetBusMute()` 切换 Master 总线静音 | 不持久化 |
| 按键设置 | 打开镜头、工具和系统动作列表 | 绑定成功后立即持久化 |

若运行时不存在 `Master` 总线，音量滑块和静音开关会禁用并显示“不可用”。

### 按键设置

`InputBindingManager` 是 `project.godot` 注册的 autoload，管理 8 个单键动作：镜头移动默认 W/A/S/D，选择、铺路和拆路默认 Q/R/E，暂停菜单默认 Esc。点击绑定按钮后，下一次无修饰键的键盘输入会替换该动作；已被其他动作使用的键会被拒绝，不会覆盖两项绑定。再次点击正在监听的按钮或离开按键页会取消监听，“恢复默认”会一次恢复全部 8 项。

每项绑定在成功修改或恢复默认后写入 `user://input_bindings.cfg`。启动时若配置缺失则使用默认值；配置包含不可用键或重复键时保留整套默认绑定并输出警告。

## 输入边界

`ToolManager` 不读取键盘绑定，只按当前工具转发建造输入。`GameHUD` 通过 `InputBindingManager` 消费 `tool_select`、`tool_road`、`tool_remove` 和 `pause_menu`；暂停菜单打开和关闭不会改变当前工具。`ToolContextPanel` 从绑定管理器读取当前工具键位，因此重绑后无需修改资源即可立即更新显示。

## 返回主菜单

`Scenes/MainMenu.tscn` 是暂停菜单的最小返回目标，不替换项目当前 `MapTest` 启动场景。它提供“进入城市”（加载 `Scenes/MapTest.tscn`）和“退出到桌面”。

## 验证

静态动作目录、持久化和消费边界由 `tests/SimpleCities.RoadGraph.Tests/InputBindingManagerContractTests.cs`、`PauseMenuContractTests.cs` 与 `GameHUDCompositionContractTests.cs` 验证。`tests/godot/command_center_runtime_contract.gd` 实例化真实 `MapTest`，验证默认绑定显示、暂停生命周期及现有 HUD 回归。`tests/godot/pause_menu_runtime_contract.gd` 在节点进入场景树前验证 HUD 实例默认隐藏，并覆盖焦点进入与恢复、T 工具重绑、F10 暂停重绑、旧暂停键失效、新暂停键开关菜单、冲突拒绝、配置落盘、上下文同步、435x480 布局、恢复默认、存取档、确认流程、返回主菜单后的 saveable 注销，以及再次进入城市后的存读档。
