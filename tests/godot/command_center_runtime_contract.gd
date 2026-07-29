extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const HUD_SCENE := "res://Scenes/UI/GameHUD.tscn"

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	DisplayServer.window_set_size(Vector2i(1362, 600))
	root.size = Vector2i(1362, 600)
	var map_scene: PackedScene = load(MAP_SCENE)
	if map_scene == null:
		fail("MapTest scene did not load")
		return

	var map: Node = map_scene.instantiate()
	root.add_child(map)
	await process_frame
	await process_frame

	var hud: CanvasLayer = map.get_node("GameHUD")
	var manager: Node = map.get_node("ToolManager")
	var dock: Control = hud.get_node("ConstructionDock")
	var tray: Control = dock.get_node("DockPanel/DockStack/ToolTray")
	var tool_scroll: ScrollContainer = dock.get_node("DockPanel/DockStack/ToolTray/TrayMargin/ToolScroll")
	var category_button: Button = dock.get_node("DockPanel/DockStack/CategoryBar/RoadsCategoryButton")
	var context: Control = hud.get_node("ToolContextPanel")
	var context_entry: Button = context.get_node("PanelMargin/Rows/ContextFocusEntryButton")
	var debug_panel: Control = hud.get_node("DebugPanel")
	var debug_button: Button = debug_panel.get_node("PanelMargin/Rows/DebugToggleButton")
	var debug_content: Control = debug_panel.get_node("PanelMargin/Rows/DebugContent")
	var system: Control = hud.get_node("SystemControls")
	var save_button: Button = system.get_node("PanelMargin/Controls/Buttons/SaveButton")
	var load_button: Button = system.get_node("PanelMargin/Controls/Buttons/LoadButton")
	var status_label: Label = system.get_node("PanelMargin/Controls/StatusLabel")

	assert_true(tool_scroll != null, "ToolTray is missing ToolScroll")
	assert_true(not tray.visible, "Tray should start collapsed")
	assert_true(not debug_content.visible, "Debug content should start collapsed")
	assert_true(dock.find_children("*ToolButton", "Button", true, false).size() == 3, "Dock should create exactly 3 live tool buttons")
	for name in ["SelectToolButton", "RoadToolButton", "RoadRemoveToolButton"]:
		assert_true(dock.find_child(name, true, false) != null, "Missing live tool button %s" % name)

	var before_tool: Variant = manager.get("CurrentTool")
	category_button.emit_signal("pressed")
	await process_frame
	assert_true(tray.visible, "Roads click should open tray")
	assert_true(manager.get("CurrentTool") == before_tool, "Roads click changed current tool")
	assert_true(dock.find_child("SelectToolButton", true, false).text == "选择 Esc", "Select label not catalog-driven Chinese")
	assert_true(dock.find_child("RoadToolButton", true, false).text == "铺路 R", "Road label not catalog-driven Chinese")
	assert_true(dock.find_child("RoadRemoveToolButton", true, false).text == "拆路 E", "Remove label not catalog-driven Chinese")

	manager._Input(key_event(KEY_R))
	await process_frame
	assert_true(manager.get("CurrentTool") == 1, "R did not select Road")
	assert_true(context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/CurrentToolRow/CurrentToolValue").text == "铺路", "Context did not read Road catalog display after R")

	dock.find_child("RoadToolButton", true, false).emit_signal("pressed")
	await process_frame
	assert_true(manager.get("CurrentTool") == 1, "Road button did not set Road tool")
	assert_true(context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/CurrentToolRow/CurrentToolValue").text == "铺路", "Context did not read Road catalog display")
	assert_true(context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/OperationRow/OperationValue").text.contains("拖拽铺设道路"), "Context did not read Road catalog description")
	assert_true(context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/ShortcutRow/ShortcutValue").text == "R", "Context did not read Road shortcut")

	category_button.emit_signal("pressed")
	await process_frame
	assert_true(not tray.visible, "Roads second click should close tray")
	assert_true(manager.get("CurrentTool") == 1, "Roads close changed current tool")
	assert_focus_link(category_button, context_entry, "collapsed category -> context")

	manager._Input(key_event(KEY_E))
	await process_frame
	assert_true(manager.get("CurrentTool") == 2, "E did not select RoadRemove")
	manager._Input(key_event(KEY_ESCAPE))
	await process_frame
	assert_true(manager.get("CurrentTool") == 0, "Esc did not select Select")

	category_button.emit_signal("pressed")
	await process_frame
	assert_focus_link(category_button, dock.find_child("SelectToolButton", true, false), "category -> select")
	assert_focus_link(dock.find_child("SelectToolButton", true, false), dock.find_child("RoadToolButton", true, false), "select -> road")
	assert_focus_link(dock.find_child("RoadToolButton", true, false), dock.find_child("RoadRemoveToolButton", true, false), "road -> remove")
	assert_focus_link(dock.find_child("RoadRemoveToolButton", true, false), context_entry, "remove -> context")
	assert_focus_link(context_entry, save_button, "context -> save")
	assert_focus_link(save_button, load_button, "save -> load")
	assert_focus_link(load_button, debug_button, "load -> debug")

	category_button.grab_focus()
	await process_frame
	Input.parse_input_event(action_event("ui_focus_next"))
	await process_frame
	assert_true(root.gui_get_focus_owner() == dock.find_child("SelectToolButton", true, false), "Tab traversal did not move category -> select")

	debug_button.emit_signal("pressed")
	await process_frame
	assert_true(debug_content.visible, "Debug toggle did not expand")
	save_button.emit_signal("pressed")
	await process_frame
	assert_true(status_label.text.contains("已保存") or status_label.text.contains("存档失败"), "Save button did not update status")
	load_button.emit_signal("pressed")
	await process_frame
	assert_true(status_label.text.contains("已加载") or status_label.text.contains("读档失败"), "Load button did not update status")

	assert_default_bounds(dock, context, system, debug_panel, tray)
	var dock_scene: PackedScene = load("res://Scenes/UI/ConstructionDock.tscn")
	var sub_viewport := SubViewport.new()
	sub_viewport.size = Vector2i(640, 480)
	var small_dock: Control = dock_scene.instantiate()
	sub_viewport.add_child(small_dock)
	root.add_child(sub_viewport)
	await process_frame
	var small_category: Button = small_dock.get_node("DockPanel/DockStack/CategoryBar/RoadsCategoryButton")
	small_category.emit_signal("pressed")
	await process_frame
	assert_small_dock_bounds(small_dock, sub_viewport)
	sub_viewport.queue_free()
	await process_frame
	await test_small_viewport_context()
	await test_command_center_theme_font()
	await test_two_hud_ui_manager_isolation()
	await test_malformed_dock()

	map.queue_free()
	await process_frame
	await test_missing_dependencies()

	print("PASS command center runtime contract")
	quit(0)

func test_missing_dependencies() -> void:
	var hud_scene: PackedScene = load(HUD_SCENE)
	var hud: CanvasLayer = hud_scene.instantiate()
	root.add_child(hud)
	await process_frame
	await process_frame
	var system: Control = hud.get_node("SystemControls")
	var status_label: Label = system.get_node("PanelMargin/Controls/StatusLabel")
	assert_true(hud.get_node_or_null("ConstructionDock") != null, "Isolated HUD failed to instantiate")
	assert_true(status_label.text.length() > 0, "Isolated HUD status unavailable")
	hud.queue_free()
	await process_frame

func test_small_viewport_context() -> void:
	var hud_scene: PackedScene = load(HUD_SCENE)
	var sub_viewport := SubViewport.new()
	sub_viewport.size = Vector2i(640, 480)
	var hud: CanvasLayer = hud_scene.instantiate()
	sub_viewport.add_child(hud)
	root.add_child(sub_viewport)
	await process_frame
	await process_frame
	var context: Control = hud.get_node("ToolContextPanel")
	var context_entry: Button = context.get_node("PanelMargin/Rows/ContextFocusEntryButton")
	var context_scroll: ScrollContainer = context.get_node("PanelMargin/Rows/ContextContentScroll")
	var context_content: Control = context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent")
	var system: Control = hud.get_node("SystemControls")
	var dock: Control = hud.get_node("ConstructionDock")
	var tray: Control = dock.get_node("DockPanel/DockStack/ToolTray")
	var category_button: Button = dock.get_node("DockPanel/DockStack/CategoryBar/RoadsCategoryButton")
	var debug_panel: Control = hud.get_node("DebugPanel")
	context.set("ReservedBottomTop", dock.position.y)
	context.ApplyResponsiveLayoutForViewport(Vector2(640, 480))
	await process_frame
	await process_frame
	var compact_rect := actual_rect(context)
	assert_true(is_equal_approx(compact_rect.size.x, 44.0), "Actual compact ContextPanel width is %.1f, expected 44" % compact_rect.size.x)
	assert_rect_in_viewport(compact_rect, Vector2(640, 480), "compact context")
	assert_true(not context_scroll.visible, "Compact ContextContentScroll should not contribute minimum size")
	assert_true(system.position.x + system.size.x <= 640.0, "SystemControls overflows small viewport")
	assert_true(dock.position.y + dock.size.y <= 480.0, "Dock overflows small viewport")
	assert_true(debug_panel.position.x >= 0.0 and debug_panel.position.y >= 0.0, "Debug panel is outside small viewport")
	assert_rect_non_overlapping(compact_rect, actual_rect(system), "compact context overlaps system controls")
	assert_rect_non_overlapping(compact_rect, actual_rect(debug_panel), "compact context overlaps debug panel")
	assert_rect_non_overlapping(compact_rect, actual_rect(dock), "compact context overlaps collapsed dock")
	category_button.emit_signal("pressed")
	await process_frame
	assert_true(tray.visible, "Small dock tray did not expand for compact assertion")
	context.set("ReservedBottomTop", dock.position.y)
	context.ApplyResponsiveLayoutForViewport(Vector2(640, 480))
	await process_frame
	await process_frame
	var compact_with_tray_rect := actual_rect(context)
	assert_true(is_equal_approx(compact_with_tray_rect.size.x, 44.0), "Actual compact ContextPanel with tray width is %.1f, expected 44" % compact_with_tray_rect.size.x)
	assert_rect_in_viewport(compact_with_tray_rect, Vector2(640, 480), "compact context with tray")
	assert_rect_non_overlapping(compact_with_tray_rect, actual_rect(dock), "compact context overlaps expanded dock")
	category_button.emit_signal("pressed")
	await process_frame
	assert_true(not tray.visible, "Small dock tray did not collapse before expanded context assertion")
	context.ToggleCompactExpandedForViewport(Vector2(640, 480))
	context.set("ReservedBottomTop", dock.position.y)
	context.ApplyResponsiveLayoutForViewport(Vector2(640, 480))
	await process_frame
	await process_frame
	assert_true(context_scroll.visible, "Expanded compact context should show ContextContentScroll")
	assert_true(context_scroll.vertical_scroll_mode != ScrollContainer.SCROLL_MODE_DISABLED, "ContextContentScroll vertical scroll is disabled")
	var expanded_collapsed_tray_rect := actual_rect(context)
	assert_true(expanded_collapsed_tray_rect.size.x > 44.0, "Compact context did not expand from focus entry with collapsed tray")
	assert_rect_in_viewport(expanded_collapsed_tray_rect, Vector2(640, 480), "expanded compact context with collapsed tray")
	assert_rect_non_overlapping(expanded_collapsed_tray_rect, actual_rect(dock), "expanded compact context overlaps collapsed dock")
	assert_rect_non_overlapping(expanded_collapsed_tray_rect, actual_rect(system), "expanded compact context overlaps system controls")
	assert_rect_non_overlapping(expanded_collapsed_tray_rect, actual_rect(debug_panel), "expanded compact context overlaps debug panel")
	category_button.emit_signal("pressed")
	await process_frame
	assert_true(tray.visible, "Small dock tray did not expand")
	context.set("ReservedBottomTop", dock.position.y)
	context.ApplyResponsiveLayoutForViewport(Vector2(640, 480))
	await process_frame
	await process_frame
	var expanded_rect := actual_rect(context)
	assert_true(expanded_rect.size.x > 44.0, "Compact context did not stay expanded with tray visible")
	assert_rect_in_viewport(expanded_rect, Vector2(640, 480), "expanded compact context")
	assert_rect_non_overlapping(expanded_rect, actual_rect(dock), "expanded compact context overlaps dock")
	assert_rect_non_overlapping(expanded_rect, actual_rect(system), "expanded compact context overlaps system controls")
	assert_rect_non_overlapping(expanded_rect, actual_rect(debug_panel), "expanded compact context overlaps debug panel")
	assert_true(context_scroll.size.y <= expanded_rect.size.y, "ContextContentScroll exceeds panel height")
	assert_true(context_scroll.size.y > 0.0, "ContextContentScroll has no usable viewport height")
	var scroll_bar := context_scroll.get_v_scroll_bar()
	scroll_bar.value = scroll_bar.max_value
	await process_frame
	assert_true(context_content.get_node("CellSizeRow").get_global_rect().position.y < expanded_rect.position.y + expanded_rect.size.y, "Lower context content is not reachable through scroll")
	sub_viewport.queue_free()
	await process_frame

func test_command_center_theme_font() -> void:
	var theme: Theme = load("res://Scenes/UI/Themes/CommandCenterTheme.tres")
	assert_true(theme != null, "CommandCenterTheme did not load")
	var font: Font = theme.default_font
	assert_true(font != null, "CommandCenterTheme has no default font")
	assert_true(font.has_char("道".unicode_at(0)), "CommandCenterTheme default font cannot resolve glyph 道")

func test_two_hud_ui_manager_isolation() -> void:
	var hud_scene: PackedScene = load(HUD_SCENE)
	var hud_one: CanvasLayer = hud_scene.instantiate()
	var hud_two: CanvasLayer = hud_scene.instantiate()
	root.add_child(hud_one)
	root.add_child(hud_two)
	await process_frame
	await process_frame
	var manager_one: Node = hud_one.get_node("UIManager")
	var manager_two: Node = hud_two.get_node("UIManager")
	var one_context: Control = hud_one.get_node("ToolContextPanel")
	var one_debug: Control = hud_one.get_node("DebugPanel")
	var one_system: Control = hud_one.get_node("SystemControls")
	assert_true(manager_one != manager_two, "HUDs shared one UIManager")
	assert_true(manager_one.GetPanel("ContextPanel") == one_context, "HUD one manager resolved wrong ContextPanel")
	assert_true(manager_one.GetPanel("DebugPanel") == one_debug, "HUD one manager resolved wrong DebugPanel")
	assert_true(manager_one.GetPanel("SystemControls") == one_system, "HUD one manager resolved wrong SystemControls")
	assert_true(manager_two.GetPanel("ContextPanel") == hud_two.get_node("ToolContextPanel"), "HUD two manager resolved wrong ContextPanel")
	hud_two.queue_free()
	await process_frame
	assert_true(manager_one.GetPanel("ContextPanel") == one_context, "Freeing HUD two corrupted HUD one ContextPanel")
	assert_true(manager_one.GetPanel("DebugPanel") == one_debug, "Freeing HUD two corrupted HUD one DebugPanel")
	assert_true(manager_one.GetPanel("SystemControls") == one_system, "Freeing HUD two corrupted HUD one SystemControls")
	hud_one.queue_free()
	await process_frame

func test_malformed_dock() -> void:
	var dock_scene: PackedScene = load("res://Scenes/UI/ConstructionDock.tscn")
	var dock: Control = dock_scene.instantiate()
	dock.set("Category", null)
	root.add_child(dock)
	await process_frame
	assert_true(dock.find_children("*ToolButton", "Button", true, false).is_empty(), "Malformed dock created tool buttons")
	assert_true(dock.get_node("DockPanel/DockStack/ToolTray").visible == false, "Malformed dock opened tray")
	dock.queue_free()
	await process_frame

func assert_default_bounds(dock: Control, context: Control, system: Control, debug_panel: Control, tray: Control) -> void:
	var viewport := root.get_viewport().get_visible_rect().size
	assert_true(dock.position.y + dock.size.y <= viewport.y, "Dock overflows default viewport")
	assert_true(context.position.x + context.size.x <= viewport.x, "Context overflows default viewport")
	assert_true(system.position.x + system.size.x <= viewport.x, "SystemControls overflows default viewport")
	assert_true(debug_panel.position.x + debug_panel.size.x < dock.position.x or debug_panel.position.y + debug_panel.size.y < dock.position.y, "Debug overlaps dock")
	assert_true(system.position.y + system.size.y <= context.position.y, "SystemControls overlaps ContextPanel")
	assert_true(dock.position.x + dock.size.x <= context.position.x or context.position.x + context.size.x <= dock.position.x or dock.position.y + dock.size.y <= context.position.y or context.position.y + context.size.y <= dock.position.y, "Expanded dock overlaps ContextPanel")
	assert_true(tray.size.y <= floor(viewport.y / 3.0) + 1.0, "Tray exceeds viewport-third cap")

func assert_small_dock_bounds(dock: Control, viewport_node: SubViewport) -> void:
	var tray: Control = dock.get_node("DockPanel/DockStack/ToolTray")
	var tool_scroll: ScrollContainer = dock.get_node("DockPanel/DockStack/ToolTray/TrayMargin/ToolScroll")
	var viewport := Vector2(viewport_node.size)
	assert_true(dock.size.x <= viewport.x, "Dock overflows small viewport width")
	assert_true(dock.position.y + dock.size.y <= viewport.y, "Dock overflows small viewport height")
	assert_true(tray.size.y <= floor(viewport.y / 3.0) + 1.0, "Small tray exceeds viewport-third cap")
	assert_true(tool_scroll.vertical_scroll_mode != ScrollContainer.SCROLL_MODE_DISABLED, "ToolTray scroll is disabled")

func assert_focus_link(from_control: Control, to_control: Control, label: String) -> void:
	assert_true(from_control.focus_next == to_control.get_path(), "Bad focus_next %s: %s != %s" % [label, from_control.focus_next, to_control.get_path()])
	assert_true(to_control.focus_previous == from_control.get_path(), "Bad focus_previous %s" % label)

func assert_control_in_viewport(control: Control, viewport: Vector2, label: String) -> void:
	assert_true(control.position.x >= 0.0, "%s left outside viewport" % label)
	assert_true(control.position.y >= 0.0, "%s top outside viewport" % label)
	assert_true(control.position.x + control.size.x <= viewport.x, "%s right outside viewport" % label)
	assert_true(control.position.y + control.size.y <= viewport.y, "%s bottom outside viewport" % label)

func actual_rect(control: Control) -> Rect2:
	return control.get_global_rect()

func assert_rect_in_viewport(rect: Rect2, viewport: Vector2, label: String) -> void:
	assert_true(rect.position.x >= 0.0, "%s left outside viewport" % label)
	assert_true(rect.position.y >= 0.0, "%s top outside viewport" % label)
	assert_true(rect.position.x + rect.size.x <= viewport.x, "%s right outside viewport" % label)
	assert_true(rect.position.y + rect.size.y <= viewport.y, "%s bottom outside viewport" % label)

func assert_non_overlapping(a: Control, b: Control, message: String) -> void:
	assert_true(a.position.x + a.size.x <= b.position.x or b.position.x + b.size.x <= a.position.x or a.position.y + a.size.y <= b.position.y or b.position.y + b.size.y <= a.position.y, message)

func assert_rect_non_overlapping(a: Rect2, b: Rect2, message: String) -> void:
	assert_true(a.position.x + a.size.x <= b.position.x or b.position.x + b.size.x <= a.position.x or a.position.y + a.size.y <= b.position.y or b.position.y + b.size.y <= a.position.y, message)

func key_event(keycode: int) -> InputEventKey:
	var event := InputEventKey.new()
	event.keycode = keycode
	event.pressed = true
	return event

func action_event(action_name: String) -> InputEventAction:
	var event := InputEventAction.new()
	event.action = action_name
	event.pressed = true
	return event

func assert_true(condition: bool, message: String) -> void:
	if not condition:
		fail(message)

func fail(message: String) -> void:
	push_error(message)
	quit(1)
