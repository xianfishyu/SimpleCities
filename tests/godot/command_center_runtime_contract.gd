extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const HUD_SCENE := "res://Scenes/UI/GameHUD.tscn"
const PRIMARY_VIEWPORT := Vector2i(1600, 900)
const SMALL_VIEWPORT := Vector2i(640, 480)
const ICON_PATHS := [
	"res://Assets/UI/Icons/construction-road.svg",
	"res://Assets/UI/Icons/construction-zoning.svg",
	"res://Assets/UI/Icons/construction-facilities.svg",
	"res://Assets/UI/Icons/construction-transit.svg",
	"res://Assets/UI/Icons/construction-landscaping.svg",
]
const CATEGORY_BUTTONS := [
	{"name": "RoadsCategoryButton", "text": "道路", "placeholder_count": 0},
	{"name": "ZoningCategoryButton", "text": "区域", "placeholder_count": 2, "items": ["住宅区", "商业区"]},
	{"name": "FacilitiesCategoryButton", "text": "公共设施", "placeholder_count": 2, "items": ["学校", "诊所"]},
	{"name": "TransitCategoryButton", "text": "交通", "placeholder_count": 2, "items": ["公交站", "地铁站"]},
	{"name": "LandscapingCategoryButton", "text": "景观", "placeholder_count": 2, "items": ["公园", "广场"]},
]
var failed := false

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	DisplayServer.window_set_size(PRIMARY_VIEWPORT)
	root.size = PRIMARY_VIEWPORT
	var map_scene: PackedScene = load(MAP_SCENE)
	if map_scene == null:
		fail("MapTest scene did not load")
		return
	await test_missing_dependencies()

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
	assert_category_buttons(dock)

	var before_tool: Variant = manager.get("CurrentTool")
	category_button.emit_signal("pressed")
	await process_frame
	assert_true(tray.visible, "Roads click should open tray")
	assert_true(manager.get("CurrentTool") == before_tool, "Roads click changed current tool")
	assert_roads_menu(dock, tray, manager)
	assert_actual_dock_contains_panel(dock, "default roads expanded")
	category_button.emit_signal("pressed")
	await process_frame
	assert_true(not tray.visible, "Repeating the active Roads category should collapse tray")
	assert_true(dock.find_children("*ToolButton", "Button", true, false).size() == 1, "Collapsing active category should not rebuild or clear Roads menu")
	assert_actual_dock_contains_panel(dock, "default roads collapsed after repeat")
	category_button.emit_signal("pressed")
	await process_frame
	assert_true(tray.visible, "Repeating Roads after collapse should reopen tray")
	var zoning_button: Button = dock.get_node("DockPanel/DockStack/CategoryBar/ZoningCategoryButton")
	zoning_button.emit_signal("pressed")
	await process_frame
	assert_true(tray.visible, "Switching from Roads to Zoning should keep tray open")
	assert_true(dock.find_children("*Placeholder", "Button", true, false).size() == 2, "Switching category should replace tray content with placeholders")
	assert_true(context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/CategoryRow/CategoryValue").text == "区域", "Switching category did not update context category")
	category_button.emit_signal("pressed")
	await process_frame
	assert_true(tray.visible, "Switching from Zoning back to Roads should keep tray open")
	assert_roads_menu(dock, tray, manager)

	dock.find_child("RoadToolButton", true, false).emit_signal("pressed")
	await process_frame
	assert_true(manager.get("CurrentTool") == 1, "Road button did not set Road tool")
	assert_true(context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/CurrentToolRow/CurrentToolValue").text == "城市道路", "Context did not read Road catalog display")
	assert_true(context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/OperationRow/OperationValue").text.contains("拖拽铺设道路"), "Context did not read Road catalog description")
	assert_true(not context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/ShortcutRow").visible, "Road context should hide its empty shortcut row")
	await assert_removed_shortcuts_are_no_op(manager, dock, context)

	await assert_future_menus_do_not_change_tool(dock, tray, manager, context)
	category_button.emit_signal("pressed")
	await process_frame
	assert_roads_menu(dock, tray, manager)
	assert_true(context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/CurrentToolRow/CurrentToolValue").text == "城市道路", "Returning to Roads did not restore city road context")

	manager.set("CurrentTool", 2)
	await process_frame
	assert_true(manager.get("CurrentTool") == 2, "Programmatic RoadRemove selection is unavailable")
	assert_builtin_context(context, "拆路", "点击已有道路进行拆除。", "", "Programmatic RoadRemove")
	assert_true(dock.find_child("RoadRemoveToolButton", true, false) == null, "RoadRemove must stay absent from submenu")
	manager._Input(key_event(KEY_ESCAPE))
	await process_frame
	assert_true(manager.get("CurrentTool") == 0, "Esc did not select Select")
	assert_builtin_context(context, "选择", "查看当前状态，取消建造操作。", "Esc", "Esc Select")
	assert_true(dock.find_child("SelectToolButton", true, false) == null, "Esc Select must stay absent from submenu")

	category_button.emit_signal("pressed")
	await process_frame
	assert_true(not tray.visible, "Repeating Roads before focus assertions should collapse tray")
	assert_focus_link(category_button, dock.get_node("DockPanel/DockStack/CategoryBar/ZoningCategoryButton"), "roads -> zoning")
	assert_focus_link(dock.get_node("DockPanel/DockStack/CategoryBar/ZoningCategoryButton"), dock.get_node("DockPanel/DockStack/CategoryBar/FacilitiesCategoryButton"), "zoning -> facilities")
	assert_focus_link(dock.get_node("DockPanel/DockStack/CategoryBar/FacilitiesCategoryButton"), dock.get_node("DockPanel/DockStack/CategoryBar/TransitCategoryButton"), "facilities -> transit")
	assert_focus_link(dock.get_node("DockPanel/DockStack/CategoryBar/TransitCategoryButton"), dock.get_node("DockPanel/DockStack/CategoryBar/LandscapingCategoryButton"), "transit -> landscaping")
	assert_focus_link(dock.get_node("DockPanel/DockStack/CategoryBar/LandscapingCategoryButton"), context_entry, "landscaping -> context collapsed")
	context_entry.grab_focus()
	await process_frame
	Input.parse_input_event(action_event("ui_focus_prev"))
	await process_frame
	assert_true(root.gui_get_focus_owner() == dock.get_node("DockPanel/DockStack/CategoryBar/LandscapingCategoryButton"), "Reverse focus from collapsed context did not move to Landscaping")
	category_button.emit_signal("pressed")
	await process_frame
	assert_true(tray.visible, "Roads category should reopen tray for expanded focus assertions")
	assert_focus_link(dock.get_node("DockPanel/DockStack/CategoryBar/LandscapingCategoryButton"), dock.find_child("RoadToolButton", true, false), "landscaping -> road tool")
	assert_focus_link(dock.find_child("RoadToolButton", true, false), context_entry, "road -> context")
	context_entry.grab_focus()
	await process_frame
	Input.parse_input_event(action_event("ui_focus_prev"))
	await process_frame
	assert_true(root.gui_get_focus_owner() == dock.find_child("RoadToolButton", true, false), "Reverse focus from expanded context did not move to RoadToolButton")
	assert_focus_link(context_entry, save_button, "context -> save")
	assert_focus_link(save_button, load_button, "save -> load")
	assert_focus_link(load_button, debug_button, "load -> debug")

	category_button.grab_focus()
	await process_frame
	Input.parse_input_event(action_event("ui_focus_next"))
	await process_frame
	assert_true(root.gui_get_focus_owner() == dock.get_node("DockPanel/DockStack/CategoryBar/ZoningCategoryButton"), "Tab traversal did not move roads -> zoning")

	debug_button.emit_signal("pressed")
	await process_frame
	assert_true(debug_content.visible, "Debug toggle did not expand")
	save_button.emit_signal("pressed")
	await process_frame
	assert_true(status_label.text.contains("已保存") or status_label.text.contains("存档失败"), "Save button did not update status")
	load_button.emit_signal("pressed")
	await process_frame
	assert_true(status_label.text.contains("已加载") or status_label.text.contains("读档失败"), "Load button did not update status")
	await assert_debug_metrics_continuity(map, debug_panel)

	assert_default_bounds(dock, context, system, debug_panel, tray)
	var dock_scene: PackedScene = load("res://Scenes/UI/ConstructionDock.tscn")
	var sub_viewport := SubViewport.new()
	sub_viewport.size = SMALL_VIEWPORT
	var small_dock: Control = dock_scene.instantiate()
	sub_viewport.add_child(small_dock)
	root.add_child(sub_viewport)
	await process_frame
	var small_category: Button = small_dock.get_node("DockPanel/DockStack/CategoryBar/RoadsCategoryButton")
	assert_category_buttons(small_dock)
	assert_actual_dock_contains_panel(small_dock, "small collapsed")
	small_category.emit_signal("pressed")
	await process_frame
	assert_small_dock_bounds(small_dock, sub_viewport)
	assert_actual_dock_contains_panel(small_dock, "small expanded")
	sub_viewport.queue_free()
	await process_frame
	await test_small_viewport_context()
	await test_same_hud_lifecycle_reentry()
	await test_command_center_theme_font()
	await test_two_hud_ui_manager_isolation()
	await test_malformed_dock()
	await assert_k_runtime_contract(hud)
	if failed:
		return

	map.queue_free()
	await process_frame

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
	sub_viewport.size = SMALL_VIEWPORT
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
	await process_frame
	await process_frame
	assert_reserved_bottom_matches_dock(context, dock, "small collapsed automatic layout")
	var compact_rect := actual_rect(context)
	assert_true(is_equal_approx(compact_rect.size.x, 44.0), "Actual compact ContextPanel width is %.1f, expected 44" % compact_rect.size.x)
	assert_rect_in_viewport(compact_rect, SMALL_VIEWPORT, "compact context")
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
	await process_frame
	assert_reserved_bottom_matches_dock(context, dock, "small expanded automatic layout")
	var compact_with_tray_rect := actual_rect(context)
	assert_true(is_equal_approx(compact_with_tray_rect.size.x, 44.0), "Actual compact ContextPanel with tray width is %.1f, expected 44" % compact_with_tray_rect.size.x)
	assert_rect_in_viewport(compact_with_tray_rect, SMALL_VIEWPORT, "compact context with tray")
	assert_rect_non_overlapping(compact_with_tray_rect, actual_rect(dock), "compact context overlaps expanded dock")
	context_entry.emit_signal("pressed")
	await process_frame
	await process_frame
	assert_true(context_scroll.visible, "Expanded compact context should show ContextContentScroll")
	assert_true(context_scroll.vertical_scroll_mode != ScrollContainer.SCROLL_MODE_DISABLED, "ContextContentScroll vertical scroll is disabled")
	assert_true(tray.visible, "Small dock tray did not stay expanded")
	assert_reserved_bottom_matches_dock(context, dock, "small compact-expanded automatic layout")
	var expanded_rect := actual_rect(context)
	assert_true(expanded_rect.size.x > 44.0, "Compact context did not stay expanded with tray visible")
	assert_rect_in_viewport(expanded_rect, SMALL_VIEWPORT, "expanded compact context")
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

func test_same_hud_lifecycle_reentry() -> void:
	var hud_scene: PackedScene = load(HUD_SCENE)
	var sub_viewport := SubViewport.new()
	sub_viewport.size = SMALL_VIEWPORT
	var hud: CanvasLayer = hud_scene.instantiate()
	sub_viewport.add_child(hud)
	root.add_child(sub_viewport)
	await process_frame
	await process_frame
	var dock: Control = hud.get_node("ConstructionDock")
	var tray: Control = dock.get_node("DockPanel/DockStack/ToolTray")
	var roads_button: Button = dock.get_node("DockPanel/DockStack/CategoryBar/RoadsCategoryButton")
	if not tray.visible:
		roads_button.emit_signal("pressed")
		await process_frame
	assert_true(tray.visible, "Lifecycle setup could not expand Roads tray")

	for cycle in range(2):
		sub_viewport.remove_child(hud)
		await process_frame
		sub_viewport.add_child(hud)
		await process_frame
		await process_frame
		await assert_reentered_hud_contract(hud, sub_viewport, cycle + 1)

	hud.queue_free()
	await process_frame
	sub_viewport.queue_free()
	await process_frame

func assert_reentered_hud_contract(hud: CanvasLayer, sub_viewport: SubViewport, cycle: int) -> void:
	var dock: Control = hud.get_node("ConstructionDock")
	var tray: Control = dock.get_node("DockPanel/DockStack/ToolTray")
	var roads_button: Button = dock.get_node("DockPanel/DockStack/CategoryBar/RoadsCategoryButton")
	var context: Control = hud.get_node("ToolContextPanel")
	var context_entry: Button = context.get_node("PanelMargin/Rows/ContextFocusEntryButton")
	var context_scroll: ScrollContainer = context.get_node("PanelMargin/Rows/ContextContentScroll")
	var system: Control = hud.get_node("SystemControls")
	var save_button: Button = system.get_node("PanelMargin/Controls/Buttons/SaveButton")
	var load_button: Button = system.get_node("PanelMargin/Controls/Buttons/LoadButton")
	var status_label: Label = system.get_node("PanelMargin/Controls/StatusLabel")
	var debug_panel: Control = hud.get_node("DebugPanel")
	var debug_button: Button = debug_panel.get_node("PanelMargin/Rows/DebugToggleButton")
	var debug_content: Control = debug_panel.get_node("PanelMargin/Rows/DebugContent")
	var label := "Lifecycle re-entry cycle %d" % cycle

	assert_reserved_bottom_matches_dock(context, dock, "%s restored" % label)
	if tray.visible:
		roads_button.emit_signal("pressed")
		await process_frame
		await process_frame
	assert_true(not tray.visible, "%s Roads press did not collapse tray exactly once" % label)
	assert_reserved_bottom_matches_dock(context, dock, "%s collapsed" % label)
	roads_button.emit_signal("pressed")
	await process_frame
	await process_frame
	assert_true(tray.visible, "%s Roads press did not expand tray exactly once" % label)
	assert_reserved_bottom_matches_dock(context, dock, "%s expanded" % label)

	var debug_before := debug_content.visible
	debug_button.emit_signal("pressed")
	await process_frame
	assert_true(debug_content.visible != debug_before, "%s Debug content did not change exactly once per press" % label)
	debug_button.emit_signal("pressed")
	await process_frame
	assert_true(debug_content.visible == debug_before, "%s second Debug press did not change content exactly once" % label)
	await process_frame

	status_label.text = "lifecycle-save-sentinel"
	save_button.emit_signal("pressed")
	await process_frame
	assert_true(status_label.text != "lifecycle-save-sentinel" and (status_label.text.contains("已保存") or status_label.text.contains("存档失败")), "%s Save status did not respond" % label)
	status_label.text = "lifecycle-load-sentinel"
	load_button.emit_signal("pressed")
	await process_frame
	assert_true(status_label.text != "lifecycle-load-sentinel" and (status_label.text.contains("已加载") or status_label.text.contains("读档失败")), "%s Load status did not respond" % label)

	var context_before := context_scroll.visible
	context_entry.emit_signal("pressed")
	await process_frame
	assert_true(context_scroll.visible != context_before, "%s compact Context content did not change exactly once per press" % label)
	context_entry.emit_signal("pressed")
	await process_frame
	assert_true(context_scroll.visible == context_before, "%s second compact Context press did not change content exactly once" % label)
	await process_frame
	assert_focus_link(context_entry, save_button, "%s context -> save" % label)
	assert_focus_link(save_button, load_button, "%s save -> load" % label)
	assert_focus_link(load_button, debug_button, "%s load -> debug" % label)

	sub_viewport.size = Vector2i(SMALL_VIEWPORT.x, SMALL_VIEWPORT.y + 1)
	await process_frame
	await process_frame
	assert_reserved_bottom_matches_dock(context, dock, "%s resized" % label)
	sub_viewport.size = SMALL_VIEWPORT
	await process_frame
	await process_frame
	assert_reserved_bottom_matches_dock(context, dock, "%s resize restored" % label)

func assert_reserved_bottom_matches_dock(context: Control, dock: Control, label: String) -> void:
	var reserved_bottom_top: float = context.get("ReservedBottomTop")
	assert_true(is_equal_approx(reserved_bottom_top, dock.position.y), "%s ReservedBottomTop %.1f != dock y %.1f" % [label, reserved_bottom_top, dock.position.y])

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
	assert_true(dock.find_children("*Placeholder", "Button", true, false).is_empty(), "Malformed dock created placeholder buttons")
	assert_true(dock.get_node("DockPanel/DockStack/ToolTray").visible == false, "Malformed dock opened tray")
	dock.queue_free()
	await process_frame

func assert_debug_metrics_continuity(map: Node, debug_panel: Control) -> void:
	var manager: Node = map.get_node("ToolManager")
	var groups: Label = debug_panel.get_node("PanelMargin/Rows/DebugContent/RoadGroupRow/RoadGroupValue")
	var edges: Label = debug_panel.get_node("PanelMargin/Rows/DebugContent/GraphEdgeRow/GraphEdgeValue")
	var nodes: Label = debug_panel.get_node("PanelMargin/Rows/DebugContent/GraphNodeRow/GraphNodeValue")
	debug_panel.UpdateMetrics()
	var before := Vector3i(int(groups.text), int(edges.text), int(nodes.text))
	manager.set("CurrentTool", 1)
	await process_frame
	var start_motion := mouse_motion_event(Vector2(320, 320))
	Input.parse_input_event(start_motion)
	manager._Input(start_motion)
	await process_frame
	manager._Input(mouse_button_event(true, Vector2(320, 320)))
	await process_frame
	var end_motion := mouse_motion_event(Vector2(384, 320))
	Input.parse_input_event(end_motion)
	manager._Input(end_motion)
	await process_frame
	manager._Input(mouse_button_event(false, Vector2(384, 320)))
	await process_frame
	debug_panel.UpdateMetrics()
	var after := Vector3i(int(groups.text), int(edges.text), int(nodes.text))
	if after.x != before.x + 1:
		fail("Debug RoadGroup metric did not continue after graph mutation: %s -> %s" % [before, after])
		return
	assert_true(after.y > before.y, "Debug GraphEdge metric did not continue after graph mutation: %s -> %s" % [before, after])
	assert_true(after.z > before.z, "Debug GraphNode metric did not continue after graph mutation: %s -> %s" % [before, after])

func assert_k_runtime_contract(hud: CanvasLayer) -> void:
	var dock: Control = hud.get_node("ConstructionDock")
	var tray: Control = dock.get_node("DockPanel/DockStack/ToolTray")
	var category_bar: Control = dock.get_node("DockPanel/DockStack/CategoryBar")
	var tool_scroll: ScrollContainer = dock.get_node("DockPanel/DockStack/ToolTray/TrayMargin/ToolScroll")
	var tool_list: Control = dock.get_node("DockPanel/DockStack/ToolTray/TrayMargin/ToolScroll/ToolList")
	var roads_button: Button = dock.get_node("DockPanel/DockStack/CategoryBar/RoadsCategoryButton")
	await collapse_primary_roads_dock(dock, tray, roads_button)
	if failed:
		return
	if not assert_k_dock_geometry(dock, tray, category_bar, PRIMARY_VIEWPORT, false, "1600x900 collapsed"):
		return
	assert_hud_pairwise_bounds(hud, PRIMARY_VIEWPORT, "1600x900 collapsed")
	if failed:
		return
	roads_button.emit_signal("pressed")
	await process_frame
	if not assert_k_dock_geometry(dock, tray, category_bar, PRIMARY_VIEWPORT, true, "1600x900 expanded"):
		return
	assert_true(tool_list is HBoxContainer, "K asset list must be a horizontal HBoxContainer")
	assert_k_asset_scroll_modes(tool_scroll, "1600x900 expanded")
	assert_k_icon_label_structure(dock)
	if failed:
		return
	assert_k_resources(dock)
	if failed:
		return
	assert_k_theme_palette(dock)
	if failed:
		return
	await assert_k_states(dock, roads_button)
	if failed:
		return
	assert_hud_pairwise_bounds(hud, PRIMARY_VIEWPORT, "1600x900 expanded")
	var hud_scene: PackedScene = load(HUD_SCENE)
	assert_true(hud_scene != null, "K runtime HUD scene did not load")
	await assert_k_small_hud_contract(hud_scene)

func collapse_primary_roads_dock(dock: Control, tray: Control, roads_button: Button) -> void:
	if not roads_button.button_pressed or not tray.visible:
		roads_button.emit_signal("pressed")
		await process_frame
	if not roads_button.button_pressed or not tray.visible:
		fail("K primary dock could not normalize to expanded Roads through category interaction")
		return
	roads_button.emit_signal("pressed")
	await process_frame
	assert_true(not tray.visible, "K primary dock reset must hide ToolTray before collapsed geometry assertions")
	assert_true(absf(actual_rect(dock).size.y - 76.0) <= 1.0, "K primary dock reset height must be 76: %.1f" % actual_rect(dock).size.y)

func assert_k_small_hud_contract(hud_scene: PackedScene) -> void:
	var viewport := SubViewport.new()
	viewport.size = PRIMARY_VIEWPORT
	var hud: CanvasLayer = hud_scene.instantiate()
	viewport.add_child(hud)
	root.add_child(viewport)
	await process_frame
	await process_frame
	var dock: Control = hud.get_node("ConstructionDock")
	var tray: Control = dock.get_node("DockPanel/DockStack/ToolTray")
	var category_bar: Control = dock.get_node("DockPanel/DockStack/CategoryBar")
	var tool_scroll: ScrollContainer = dock.get_node("DockPanel/DockStack/ToolTray/TrayMargin/ToolScroll")
	var debug_panel: Control = hud.get_node("DebugPanel")
	var debug_button: Button = debug_panel.get_node("PanelMargin/Rows/DebugToggleButton")
	var debug_content: Control = debug_panel.get_node("PanelMargin/Rows/DebugContent")
	debug_button.emit_signal("pressed")
	await process_frame
	assert_true(debug_content.visible, "K stateful small HUD setup must expand Debug content")
	assert_true(debug_panel.size.y > debug_panel.get_combined_minimum_size().y - 1.0, "K expanded Debug outer height must reflect expanded content")
	var expanded_debug_height := debug_panel.size.y
	debug_button.emit_signal("pressed")
	await process_frame
	assert_true(not debug_content.visible, "K stateful small HUD setup must collapse Debug content")
	var collapsed_debug_height := debug_panel.get_combined_minimum_size().y
	assert_true(collapsed_debug_height < expanded_debug_height, "K regression setup requires collapsed Debug minimum %.1f below expanded outer height %.1f" % [collapsed_debug_height, expanded_debug_height])
	var roads_button: Button = dock.get_node("DockPanel/DockStack/CategoryBar/RoadsCategoryButton")
	roads_button.emit_signal("pressed")
	await process_frame
	assert_true(tray.visible, "K stateful small HUD setup must expand the construction tray")
	viewport.size = SMALL_VIEWPORT
	await process_frame
	await process_frame
	var after_two_frames := capture_hud_layout(hud)
	await process_frame
	await process_frame
	var after_four_frames := capture_hud_layout(hud)
	assert_k_stateful_small_hud_layout(after_two_frames, collapsed_debug_height, "after two frames")
	assert_k_stateful_small_hud_layout(after_four_frames, collapsed_debug_height, "after four frames")
	if failed:
		return
	roads_button.emit_signal("pressed")
	await process_frame
	if not assert_k_dock_geometry(dock, tray, category_bar, SMALL_VIEWPORT, false, "640x480 collapsed"):
		return
	assert_hud_pairwise_bounds(hud, SMALL_VIEWPORT, "640x480 collapsed")
	if failed:
		return
	roads_button.emit_signal("pressed")
	await process_frame
	if not assert_k_dock_geometry(dock, tray, category_bar, SMALL_VIEWPORT, true, "640x480 expanded"):
		return
	assert_k_asset_scroll_modes(tool_scroll, "640x480 expanded")
	assert_hud_pairwise_bounds(hud, SMALL_VIEWPORT, "640x480 expanded")
	viewport.queue_free()
	await process_frame

func capture_hud_layout(hud: CanvasLayer) -> Dictionary:
	return {
		"ConstructionDock": actual_rect(hud.get_node("ConstructionDock")),
		"ToolContextPanel": actual_rect(hud.get_node("ToolContextPanel")),
		"SystemControls": actual_rect(hud.get_node("SystemControls")),
		"DebugPanel": actual_rect(hud.get_node("DebugPanel")),
	}

func assert_k_stateful_small_hud_layout(layout: Dictionary, collapsed_debug_height: float, label: String) -> void:
	var debug_rect: Rect2 = layout.DebugPanel
	var dock_rect: Rect2 = layout.ConstructionDock
	assert_top_left_debug_rect(debug_rect, "K stateful 640x480 %s" % label)
	assert_true(absf(debug_rect.size.y - collapsed_debug_height) <= 1.0, "K stateful 640x480 %s Debug outer height did not contract to collapsed combined minimum: rect=%s minimum=%.1f dock=%s" % [label, debug_rect, collapsed_debug_height, dock_rect])
	var panels := ["ConstructionDock", "ToolContextPanel", "SystemControls", "DebugPanel"]
	for panel_name in panels:
		assert_rect_in_viewport(layout[panel_name], SMALL_VIEWPORT, "K stateful 640x480 %s %s" % [label, panel_name])
	for first_index in range(panels.size()):
		for second_index in range(first_index + 1, panels.size()):
			assert_rect_non_overlapping(layout[panels[first_index]], layout[panels[second_index]], "K stateful 640x480 %s %s overlaps %s" % [label, panels[first_index], panels[second_index]])

func assert_k_dock_geometry(dock: Control, tray: Control, category_bar: Control, viewport: Vector2, expanded: bool, label: String) -> bool:
	var dock_rect := actual_rect(dock)
	var panel_rect := actual_rect(dock.get_node("DockPanel"))
	var expected_height := 122.0 if expanded else 76.0
	if absf(dock_rect.position.x) > 1.0:
		fail("K %s dock must start at viewport left: x=%.1f" % [label, dock_rect.position.x])
		return false
	assert_true(absf(dock_rect.size.x - viewport.x) <= 1.0, "K %s dock must span viewport width: %.1f != %.1f" % [label, dock_rect.size.x, viewport.x])
	assert_true(absf(dock_rect.end.y - viewport.y) <= 1.0, "K %s dock must be bottom flush: %.1f != %.1f" % [label, dock_rect.end.y, viewport.y])
	assert_true(absf(dock_rect.size.y - expected_height) <= 1.0, "K %s dock height must be %.0f: %.1f" % [label, expected_height, dock_rect.size.y])
	assert_true(absf(panel_rect.size.x - viewport.x) <= 1.0, "K %s DockPanel must span viewport width: %.1f != %.1f" % [label, panel_rect.size.x, viewport.x])
	assert_true(absf(panel_rect.end.y - viewport.y) <= 1.0, "K %s DockPanel must be bottom flush" % label)
	assert_true(absf(category_bar.size.y - 76.0) <= 1.0, "K %s CategoryBar height must be 76: %.1f" % [label, category_bar.size.y])
	if expanded:
		assert_true(tray.visible, "K %s ToolTray should be visible" % label)
		assert_true(absf(tray.size.y - 46.0) <= 1.0, "K %s ToolTray height must be 46: %.1f" % [label, tray.size.y])
	else:
		assert_true(not tray.visible, "K %s ToolTray should be collapsed" % label)
	return not failed

func assert_k_icon_label_structure(dock: Control) -> void:
	for category in CATEGORY_BUTTONS:
		var button: Button = dock.get_node("DockPanel/DockStack/CategoryBar/%s" % category.name)
		var layout := first_descendant_of_type(button, "VBoxContainer")
		var icon := first_descendant_of_type(button, "TextureRect")
		var label := first_descendant_of_type(button, "Label")
		if layout == null:
			fail("K %s must contain a reusable VBoxContainer" % category.name)
			return
		if icon == null:
			fail("K %s must contain a TextureRect icon" % category.name)
			return
		if label == null or label.text != category.text:
			fail("K %s must contain exact Chinese label %s" % [category.name, category.text])
			return
		assert_true(button.text == "", "K %s native Button.text must stay empty so only the custom CJK label row renders" % category.name)
		assert_true(icon.get_parent() == layout and label.get_parent() == layout, "K %s icon and label must share the reusable VBoxContainer" % category.name)
		assert_true(icon.get_index() < label.get_index(), "K %s icon must be above its Chinese label" % category.name)
	var road_button: Button = dock.find_child("RoadToolButton", true, false)
	if road_button == null:
		fail("K Roads tray must contain RoadToolButton before icon inspection")
		return
	var road_button_script: Script = road_button.get_script()
	if road_button_script == null or road_button_script.resource_path != "res://Scripts/UI/ConstructionDockButton.cs":
		fail("K city-road asset must use reusable ConstructionDockButton")
		return
	assert_true(road_button.get("DisplayText") == "城市道路", "K city-road asset must expose exact native display label 城市道路")
	assert_true(road_button.get("IconTexture") != null, "K city-road asset must expose a non-null native IconTexture")

func assert_k_resources(dock: Control) -> void:
	var new_definition := ConstructionToolDefinition.new()
	assert_true(new_definition.Icon == null, "K new ConstructionToolDefinition Icon must default to null in engine")
	var loaded_category: Resource = dock.get("Category")
	if loaded_category == null:
		fail("K primary dock must expose its loaded category resource before Icon inspection")
		return
	var tools: Variant = loaded_category.get("Tools")
	if tools == null:
		fail("K loaded Roads category Tools must be non-null before Icon inspection")
		return
	var city_road_definition: Variant = null
	for tool in tools:
		if tool != null and tool.get("Id") == "city-road":
			city_road_definition = tool
			break
	if city_road_definition == null:
		fail("K loaded Roads category must contain city-road before Icon inspection")
		return
	var city_road_icon: Variant = city_road_definition.get("Icon")
	if city_road_icon == null:
		fail("K loaded city-road definition Icon must be non-null")
		return
	if not city_road_icon is Texture2D:
		fail("K loaded city-road definition Icon must be Texture2D")
		return
	assert_texture_is_production_icon(city_road_icon, "K loaded city-road definition Icon")
	if failed:
		return
	for path in ICON_PATHS:
		assert_true(path.begins_with("res://Assets/UI/Icons/"), "K icon path escaped production icon directory: %s" % path)
		if not ResourceLoader.exists(path, "Texture2D"):
			fail("K icon resource must exist as Texture2D: %s" % path)
			return
		var texture: Texture2D = load(path)
		if texture == null:
			fail("K icon resource must load as Texture2D: %s" % path)
			return
	for category in CATEGORY_BUTTONS:
		var button: Button = dock.get_node("DockPanel/DockStack/CategoryBar/%s" % category.name)
		var icon: TextureRect = first_descendant_of_type(button, "TextureRect")
		if icon == null:
			fail("K %s is missing TextureRect before texture inspection" % category.name)
			return
		assert_texture_is_production_icon(icon.texture, "K %s texture" % category.name)
		if failed:
			return
	var road_button: Button = dock.find_child("RoadToolButton", true, false)
	if road_button == null:
		fail("K Roads tray must contain RoadToolButton before texture inspection")
		return
	var road_button_script: Script = road_button.get_script()
	if road_button_script == null or road_button_script.resource_path != "res://Scripts/UI/ConstructionDockButton.cs":
		fail("K city-road asset must use reusable ConstructionDockButton before texture inspection")
		return
	assert_texture_is_production_icon(road_button.get("IconTexture"), "K city-road native IconTexture")

func assert_k_theme_palette(dock: Control) -> void:
	var theme: Theme = dock.theme
	if theme == null:
		fail("K ConstructionDock must keep its local Theme resource")
		return
	assert_theme_color(theme, "base_color", "ConstructionDock", Color(0.0588235, 0.0705882, 0.0901961, 0.92), "dock base")
	assert_theme_color(theme, "asset_strip_color", "ConstructionDockAssetStrip", Color(0.0823529, 0.101961, 0.133333, 0.96), "asset strip")
	assert_theme_color(theme, "divider_color", "ConstructionDock", Color(0.141176, 0.160784, 0.2, 0.5), "divider")
	assert_theme_color(theme, "primary_color", "ConstructionDock", Color(0.94902, 0.956863, 0.968627, 1), "primary semantic")
	assert_theme_color(theme, "disabled_color", "ConstructionDock", Color(0.345098, 0.376471, 0.419608, 1), "disabled semantic")
	assert_theme_color(theme, "hover_color", "ConstructionDock", Color(1, 0.823529, 0.478431, 1), "hover semantic")
	assert_theme_color(theme, "selected_color", "ConstructionDock", Color(1, 0.760784, 0.34902, 1), "selected semantic")
	assert_theme_color(theme, "selected_label_color", "ConstructionDock", Color(1, 0.760784, 0.34902, 1), "selected label semantic")
	assert_theme_color(theme, "icon_hover_color", "ConstructionDockButton", Color(1, 0.823529, 0.478431, 1), "button hover icon")
	assert_theme_color(theme, "icon_pressed_color", "ConstructionDockButton", Color(1, 0.760784, 0.34902, 1), "button pressed icon")
	assert_theme_color(theme, "icon_disabled_color", "ConstructionDockButton", Color(0.345098, 0.376471, 0.419608, 1), "button disabled icon")
	assert_stylebox_color(theme, "panel", "ConstructionDock", Color(0.0588235, 0.0705882, 0.0901961, 0.92), "outer dock panel")
	assert_stylebox_color(theme, "panel", "ConstructionDockAssetStrip", Color(0.0823529, 0.101961, 0.133333, 0.96), "asset strip panel")
	assert_stylebox_color(theme, "hover", "ConstructionDockButton", Color(0.141176, 0.188235, 0.254902, 1), "button hover background")
	assert_stylebox_color(theme, "pressed", "ConstructionDockButton", Color(0.168627, 0.215686, 0.282353, 1), "button pressed background")
	assert_stylebox_color(theme, "disabled", "ConstructionDockButton", Color(0.0666667, 0.0823529, 0.105882, 0.72), "button disabled background")
	var focus := theme.get_stylebox("focus", "ConstructionDockButton") as StyleBoxFlat
	if focus == null:
		fail("K ConstructionDockButton focus style must be a StyleBoxFlat")
		return
	assert_color_approx(focus.border_color, Color(1, 0.878431, 0.541176, 1), "button focus ring")

func assert_texture_is_production_icon(texture: Texture2D, label: String) -> void:
	if texture == null:
		fail("%s is null" % label)
		return
	assert_true(texture.resource_path.begins_with("res://Assets/UI/Icons/"), "%s path must be under res://Assets/UI/Icons/: %s" % [label, texture.resource_path])

func assert_theme_color(theme: Theme, color_name: StringName, theme_type: StringName, expected: Color, label: String) -> void:
	assert_true(theme.has_color(color_name, theme_type), "K theme is missing %s color %s" % [theme_type, color_name])
	assert_color_approx(theme.get_color(color_name, theme_type), expected, label)

func assert_stylebox_color(theme: Theme, style_name: StringName, theme_type: StringName, expected: Color, label: String) -> void:
	var stylebox := theme.get_stylebox(style_name, theme_type) as StyleBoxFlat
	if stylebox == null:
		fail("K theme %s style %s must be a StyleBoxFlat" % [theme_type, style_name])
		return
	assert_color_approx(stylebox.bg_color, expected, label)

func assert_color_approx(actual: Color, expected: Color, label: String) -> void:
	var tolerance := 0.002
	assert_true(absf(actual.r - expected.r) <= tolerance and absf(actual.g - expected.g) <= tolerance and absf(actual.b - expected.b) <= tolerance and absf(actual.a - expected.a) <= tolerance, "%s color mismatch: actual=%s expected=%s" % [label, actual, expected])

func assert_k_states(dock: Control, selected: Button) -> void:
	var default_button: Button = dock.get_node("DockPanel/DockStack/CategoryBar/ZoningCategoryButton")
	default_button.emit_signal("pressed")
	await process_frame
	var disabled: Button = dock.find_child("*Placeholder", true, false)
	assert_true(default_button.button_pressed, "K selected category must expose structural pressed state")
	assert_true(not selected.button_pressed, "K default category must differ structurally from selected state")
	if disabled == null:
		fail("K future tray must contain a disabled placeholder for state inspection")
		return
	assert_true(disabled.disabled, "K disabled asset must expose structural disabled state")
	var selected_style := default_button.get_theme_stylebox("pressed")
	var default_style := selected.get_theme_stylebox("normal")
	var disabled_style := disabled.get_theme_stylebox("disabled")
	var focus_style := selected.get_theme_stylebox("focus")
	assert_true(selected_style != null and default_style != null and selected_style != default_style, "K selected and default theme states must be distinct")
	assert_true(disabled_style != null and disabled_style != default_style, "K disabled and default theme states must be distinct")
	assert_true(focus_style != null and focus_style != selected_style, "K keyboard focus ring must be independent from selected state")

func assert_hud_pairwise_bounds(hud: CanvasLayer, viewport: Vector2, label: String) -> void:
	var panels: Array[Control] = [
		hud.get_node("ConstructionDock"),
		hud.get_node("ToolContextPanel"),
		hud.get_node("SystemControls"),
		hud.get_node("DebugPanel"),
	]
	for panel in panels:
		assert_rect_in_viewport(actual_rect(panel), viewport, "%s %s" % [label, panel.name])
	for first_index in range(panels.size()):
		for second_index in range(first_index + 1, panels.size()):
			assert_rect_non_overlapping(actual_rect(panels[first_index]), actual_rect(panels[second_index]), "%s %s overlaps %s" % [label, panels[first_index].name, panels[second_index].name])

func assert_k_asset_scroll_modes(tool_scroll: ScrollContainer, label: String) -> void:
	assert_true(tool_scroll.horizontal_scroll_mode != ScrollContainer.SCROLL_MODE_DISABLED, "K %s asset strip horizontal scrolling must be enabled" % label)
	assert_true(tool_scroll.vertical_scroll_mode == ScrollContainer.SCROLL_MODE_DISABLED, "K %s asset strip vertical scrolling must be disabled" % label)

func assert_default_bounds(dock: Control, context: Control, system: Control, debug_panel: Control, tray: Control) -> void:
	var viewport := root.get_viewport().get_visible_rect().size
	assert_top_left_debug_rect(actual_rect(debug_panel), "default viewport")
	assert_true(dock.position.y + dock.size.y <= viewport.y, "Dock overflows default viewport")
	assert_true(context.position.x + context.size.x <= viewport.x, "Context overflows default viewport")
	assert_true(system.position.x + system.size.x <= viewport.x, "SystemControls overflows default viewport")
	assert_true(debug_panel.position.x + debug_panel.size.x < dock.position.x or debug_panel.position.y + debug_panel.size.y < dock.position.y, "Debug overlaps dock")
	assert_true(system.position.y + system.size.y <= context.position.y, "SystemControls overlaps ContextPanel")
	assert_true(dock.position.x + dock.size.x <= context.position.x or context.position.x + context.size.x <= dock.position.x or dock.position.y + dock.size.y <= context.position.y or context.position.y + context.size.y <= dock.position.y, "Expanded dock overlaps ContextPanel")
	assert_true(tray.size.y <= floor(viewport.y / 3.0) + 1.0, "Tray exceeds viewport-third cap")

func assert_top_left_debug_rect(debug_rect: Rect2, label: String) -> void:
	assert_true(absf(debug_rect.position.x - 16.0) <= 1.0, "%s DebugPanel x must remain top-left margin 16: %.1f" % [label, debug_rect.position.x])
	assert_true(absf(debug_rect.position.y - 16.0) <= 1.0, "%s DebugPanel y must remain top-left margin 16: %.1f" % [label, debug_rect.position.y])

func assert_small_dock_bounds(dock: Control, viewport_node: SubViewport) -> void:
	var tray: Control = dock.get_node("DockPanel/DockStack/ToolTray")
	var category_bar: Control = dock.get_node("DockPanel/DockStack/CategoryBar")
	var viewport := Vector2(viewport_node.size)
	assert_true(dock.size.x <= viewport.x, "Dock overflows small viewport width")
	assert_true(dock.position.y + dock.size.y <= viewport.y, "Dock overflows small viewport height")
	assert_true(category_bar.size.x <= dock.size.x, "CategoryBar exceeds dock width at small viewport: %.1f > %.1f" % [category_bar.size.x, dock.size.x])
	for category in CATEGORY_BUTTONS:
		var category_button: Control = dock.get_node("DockPanel/DockStack/CategoryBar/%s" % category.name)
		assert_true(category_button.get_global_rect().end.x <= dock.get_global_rect().end.x + 1.0, "%s overflows small dock actual rect" % category.name)
	assert_true(tray.size.y <= floor(viewport.y / 3.0) + 1.0, "Small tray exceeds viewport-third cap")

func assert_actual_dock_contains_panel(dock: Control, label: String) -> void:
	var dock_rect := dock.get_global_rect()
	var panel: Control = dock.get_node("DockPanel")
	var panel_rect := panel.get_global_rect()
	assert_true(dock_rect.size.x <= dock.get_viewport_rect().size.x + 1.0, "%s dock exceeds viewport width: %.1f > %.1f" % [label, dock_rect.size.x, dock.get_viewport_rect().size.x])
	assert_true(panel_rect.position.x >= dock_rect.position.x - 1.0, "%s DockPanel left escapes root: panel %.1f root %.1f" % [label, panel_rect.position.x, dock_rect.position.x])
	assert_true(panel_rect.position.y >= dock_rect.position.y - 1.0, "%s DockPanel top escapes root: panel %.1f root %.1f" % [label, panel_rect.position.y, dock_rect.position.y])
	assert_true(panel_rect.end.x <= dock_rect.end.x + 1.0, "%s DockPanel right escapes root: panel %.1f root %.1f" % [label, panel_rect.end.x, dock_rect.end.x])
	assert_true(panel_rect.end.y <= dock_rect.end.y + 1.0, "%s DockPanel bottom escapes root: panel %.1f root %.1f" % [label, panel_rect.end.y, dock_rect.end.y])
	assert_rect_in_viewport(panel_rect, dock.get_viewport_rect().size, "%s actual DockPanel" % label)

func assert_category_buttons(dock: Control) -> void:
	for category in CATEGORY_BUTTONS:
		var button: Button = dock.get_node("DockPanel/DockStack/CategoryBar/%s" % category.name)
		assert_true(control_display_text(button) == category.text, "%s text mismatch" % category.name)
		assert_true(not button.disabled, "%s should be enabled" % category.name)
		assert_true(button.focus_mode == Control.FOCUS_ALL, "%s should receive focus" % category.name)

func assert_roads_menu(dock: Control, tray: Control, manager: Node) -> void:
	assert_true(tray.visible, "Roads menu should keep the shared tray open")
	assert_true(dock.find_children("*ToolButton", "Button", true, false).size() == 1, "Roads menu should expose exactly one real tool")
	assert_true(dock.find_child("SelectToolButton", true, false) == null, "Roads menu must not show Select")
	assert_true(dock.find_child("RoadRemoveToolButton", true, false) == null, "Roads menu must not show RoadRemove")
	var road_button: Button = dock.find_child("RoadToolButton", true, false)
	assert_true(road_button != null, "Roads menu is missing city road button")
	assert_true(control_display_text(road_button) == "城市道路", "Road label should not include a removed shortcut hint")
	assert_true(not road_button.disabled, "City road should be enabled")
	assert_true(road_button.focus_mode == Control.FOCUS_ALL, "City road should be focusable")
	if manager != null:
		var before_tool: Variant = manager.get("CurrentTool")
		road_button.emit_signal("pressed")
		assert_true(manager.get("CurrentTool") == 1, "City road button did not select Road")
		manager.set("CurrentTool", before_tool)

func assert_builtin_context(context: Control, expected_tool: String, expected_operation: String, expected_shortcut: String, label: String) -> void:
	assert_true(context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/CurrentToolRow/CurrentToolValue").text == expected_tool, "%s context tool mismatch" % label)
	assert_true(context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/OperationRow/OperationValue").text == expected_operation, "%s context operation mismatch" % label)
	assert_true(context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/ShortcutRow/ShortcutValue").text == expected_shortcut, "%s context shortcut mismatch" % label)
	assert_true(context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/ShortcutRow").visible == not expected_shortcut.is_empty(), "%s context shortcut row visibility mismatch" % label)

func assert_removed_shortcuts_are_no_op(manager: Node, dock: Control, context: Control) -> void:
	for initial_tool in [0, 1, 2]:
		manager.set("CurrentTool", initial_tool)
		await process_frame
		for keycode in [KEY_R, KEY_E]:
			manager._Input(key_event(keycode))
			await process_frame
			assert_true(manager.get("CurrentTool") == initial_tool, "%s changed tool %d" % [OS.get_keycode_string(keycode), initial_tool])

	manager.set("CurrentTool", 0)
	await process_frame
	manager._Input(key_event(KEY_ESCAPE))
	await process_frame
	assert_true(manager.get("CurrentTool") == 0, "Esc changed Select away from Select")
	assert_builtin_context(context, "选择", "查看当前状态，取消建造操作。", "Esc", "Select Esc")

	dock.find_child("RoadToolButton", true, false).emit_signal("pressed")
	await process_frame
	manager._Input(key_event(KEY_ESCAPE))
	await process_frame
	assert_true(manager.get("CurrentTool") == 0, "Esc did not return Road to Select")

	manager.set("CurrentTool", 2)
	await process_frame
	manager._Input(key_event(KEY_ESCAPE))
	await process_frame
	assert_true(manager.get("CurrentTool") == 0, "Esc did not return RoadRemove to Select")

func assert_future_menus_do_not_change_tool(dock: Control, tray: Control, manager: Node, context: Control) -> void:
	for category in CATEGORY_BUTTONS:
		if category.name == "RoadsCategoryButton":
			continue
		manager.set("CurrentTool", 1)
		var button: Button = dock.get_node("DockPanel/DockStack/CategoryBar/%s" % category.name)
		button.emit_signal("pressed")
		await process_frame
		assert_true(tray.visible, "%s should open the shared tray" % category.name)
		assert_true(manager.get("CurrentTool") == 1, "%s changed current tool" % category.name)
		assert_true(dock.find_children("*ToolButton", "Button", true, false).is_empty(), "%s created real tool buttons" % category.name)
		var placeholders := dock.find_children("*Placeholder", "Button", true, false)
		assert_true(placeholders.size() == category.placeholder_count, "%s placeholder count mismatch" % category.name)
		for placeholder in placeholders:
			assert_true(placeholder.disabled, "%s should be disabled" % placeholder.name)
			assert_true(placeholder.focus_mode == Control.FOCUS_NONE, "%s should be non-focusable" % placeholder.name)
			assert_true(placeholder.tooltip_text == "尚未开放", "%s tooltip mismatch" % placeholder.name)
			placeholder.emit_signal("pressed")
			await process_frame
			assert_true(manager.get("CurrentTool") == 1, "%s changed current tool through forced signal" % placeholder.name)
		assert_true(context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/CategoryRow/CategoryValue").text == category.text, "%s did not update context category" % category.name)
		assert_true(context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/CurrentToolRow/CurrentToolValue").text == "尚未开放", "%s context should show unavailable state" % category.name)
		assert_true(not context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/ShortcutRow").visible, "%s context should hide shortcut" % category.name)
		assert_true(not context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/CellSizeRow").visible, "%s context should hide road config" % category.name)
		manager._Input(key_event(KEY_R))
		await process_frame
		assert_true(manager.get("CurrentTool") == 1, "%s R changed current tool" % category.name)
		assert_future_context_unchanged(context, category.text, "%s after R" % category.name)
		manager._Input(key_event(KEY_E))
		await process_frame
		assert_true(manager.get("CurrentTool") == 1, "%s E changed current tool" % category.name)
		assert_future_context_unchanged(context, category.text, "%s after E" % category.name)
		manager._Input(key_event(KEY_ESCAPE))
		await process_frame
		assert_true(manager.get("CurrentTool") == 0, "%s Esc did not select Select" % category.name)
		assert_future_context_unchanged(context, category.text, "%s after Esc" % category.name)

func assert_future_context_unchanged(context: Control, category_text: String, label: String) -> void:
	assert_true(context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/CategoryRow/CategoryValue").text == category_text, "%s context category changed" % label)
	assert_true(context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/CurrentToolRow/CurrentToolValue").text == "尚未开放", "%s context tool should remain unavailable" % label)
	assert_true(context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/OperationRow/OperationValue").text == "尚未开放", "%s context operation should remain unavailable" % label)
	assert_true(not context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/ShortcutRow").visible, "%s context shortcut should stay hidden" % label)
	assert_true(not context.get_node("PanelMargin/Rows/ContextContentScroll/ContextContent/CellSizeRow").visible, "%s context cell size should stay hidden" % label)

func assert_focus_link(from_control: Control, to_control: Control, label: String) -> void:
	assert_true(from_control.focus_next == to_control.get_path(), "Bad focus_next %s: %s != %s" % [label, from_control.focus_next, to_control.get_path()])
	assert_true(to_control.focus_previous == from_control.get_path(), "Bad focus_previous %s" % label)

func control_display_text(button: Button) -> String:
	var label := first_descendant_of_type(button, "Label")
	if label != null:
		return label.text
	return button.text

func first_descendant_of_type(node: Node, type_name: String) -> Node:
	var matches := node.find_children("*", type_name, true, false)
	return matches[0] if not matches.is_empty() else null

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

func mouse_button_event(pressed: bool, position: Vector2 = Vector2.ZERO) -> InputEventMouseButton:
	var event := InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_LEFT
	event.pressed = pressed
	event.position = position
	event.global_position = position
	return event

func mouse_motion_event(position: Vector2) -> InputEventMouseMotion:
	var event := InputEventMouseMotion.new()
	event.position = position
	event.global_position = position
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
	failed = true
	push_error(message)
	quit(1)
