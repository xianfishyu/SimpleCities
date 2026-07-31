extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const MAIN_MENU_SCENE := "res://Scenes/MainMenu.tscn"

var failed := false

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var packed_map: PackedScene = load(MAP_SCENE)
	assert_true(packed_map != null, "MapTest scene did not load")
	var map: Node = packed_map.instantiate()
	root.add_child(map)
	current_scene = map
	await process_frame
	await process_frame

	var hud: CanvasLayer = map.get_node("GameHUD")
	var manager: Node = map.get_node("ToolManager")
	var pause_menu: Control = hud.get_node("PauseMenu")
	var continue_button: Button = pause_menu.get_node("Center/MainPanel/MainContent/ContinueButton")
	var save_button: Button = pause_menu.get_node("Center/MainPanel/MainContent/SaveButton")
	var load_button: Button = pause_menu.get_node("Center/MainPanel/MainContent/LoadButton")
	var settings_button: Button = pause_menu.get_node("Center/MainPanel/MainContent/SettingsButton")
	var exit_game_button: Button = pause_menu.get_node("Center/MainPanel/MainContent/ExitGameButton")
	var exit_desktop_button: Button = pause_menu.get_node("Center/MainPanel/MainContent/ExitDesktopButton")
	var status_label: Label = pause_menu.get_node("Center/MainPanel/MainContent/StatusLabel")
	var main_content: Control = pause_menu.get_node("Center/MainPanel/MainContent")
	var settings_content: Control = pause_menu.get_node("Center/MainPanel/SettingsContent")
	var confirmation_content: Control = pause_menu.get_node("Center/MainPanel/ConfirmationContent")
	var volume_slider: HSlider = pause_menu.get_node("Center/MainPanel/SettingsContent/MasterVolumeSlider")
	var volume_value: Label = pause_menu.get_node("Center/MainPanel/SettingsContent/MasterVolumeValue")
	var mute_toggle: CheckButton = pause_menu.get_node("Center/MainPanel/SettingsContent/MuteToggle")
	var key_bindings_button: Button = pause_menu.get_node("Center/MainPanel/SettingsContent/KeyBindingsButton")
	var settings_back_button: Button = pause_menu.get_node("Center/MainPanel/SettingsContent/BackButton")
	var bindings_content: Control = pause_menu.get_node("Center/MainPanel/BindingsContent")
	var binding_status: Label = pause_menu.get_node("Center/MainPanel/BindingsContent/BindingStatusLabel")
	var reset_bindings_button: Button = pause_menu.get_node("Center/MainPanel/BindingsContent/BindingActions/ResetBindingsButton")
	var bindings_back_button: Button = pause_menu.get_node("Center/MainPanel/BindingsContent/BindingActions/BackButton")
	var confirm_button: Button = pause_menu.get_node("Center/MainPanel/ConfirmationContent/ConfirmationButtons/ConfirmButton")
	var cancel_button: Button = pause_menu.get_node("Center/MainPanel/ConfirmationContent/ConfirmationButtons/CancelButton")
	var focus_source: Button = hud.get_node("ConstructionDock/DockPanel/DockStack/CategoryScroll/CategoryBar/RoadsCategoryButton")
	var save_manager: Node = root.get_node("SaveManager")

	manager.set("CurrentTool", 1)
	focus_source.grab_focus()
	await process_frame
	hud._Input(key_event(KEY_ESCAPE))
	await process_frame
	assert_true(pause_menu.visible, "Esc did not show PauseMenu")
	assert_true(paused, "Esc did not pause SceneTree")
	assert_true(manager.get("CurrentTool") == 1, "Pause menu changed current tool")
	assert_true(root.gui_get_focus_owner() == continue_button, "Opening PauseMenu did not focus ContinueButton")
	pause_menu._Input(key_event(KEY_ESCAPE))
	await process_frame
	assert_true(not pause_menu.visible and not paused, "Esc did not continue from the pause menu main view")
	assert_true(root.gui_get_focus_owner() == focus_source, "Closing PauseMenu did not restore the previous focus owner")
	hud._Input(key_event(KEY_ESCAPE))
	await process_frame
	assert_true(pause_menu.visible and paused, "Esc did not reopen PauseMenu after continuing")

	settings_button.emit_signal("pressed")
	assert_true(not main_content.visible and settings_content.visible and not confirmation_content.visible, "Settings button did not switch view")
	var master_bus := AudioServer.get_bus_index("Master")
	if master_bus >= 0:
		var original_volume := AudioServer.get_bus_volume_db(master_bus)
		var original_muted := AudioServer.is_bus_mute(master_bus)
		volume_slider.value = 35.0
		assert_true(volume_value.text == "35%", "Volume value label did not update")
		mute_toggle.emit_signal("toggled", not original_muted)
		assert_true(AudioServer.is_bus_mute(master_bus) == not original_muted, "Mute toggle did not update Master bus")
		AudioServer.set_bus_volume_db(master_bus, original_volume)
		AudioServer.set_bus_mute(master_bus, original_muted)
	key_bindings_button.emit_signal("pressed")
	assert_true(bindings_content.visible and not settings_content.visible, "Key bindings button did not open bindings view")
	var road_binding: Button = pause_menu.find_child("tool_road_BindingButton", true, false)
	var select_binding: Button = pause_menu.find_child("tool_select_BindingButton", true, false)
	var pause_binding: Button = pause_menu.find_child("pause_menu_BindingButton", true, false)
	assert_true(road_binding != null and select_binding != null and pause_binding != null, "Bindings view did not create required binding buttons")
	reset_bindings_button.emit_signal("pressed")
	assert_true(road_binding.text == "R" and select_binding.text == "Q" and pause_binding.text == "Escape", "Reset did not establish deterministic defaults")
	DisplayServer.window_set_size(Vector2i(435, 480))
	root.size = Vector2i(435, 480)
	await process_frame
	await process_frame
	var bindings_panel: Control = pause_menu.get_node("Center/MainPanel")
	assert_rect_in_viewport(bindings_panel.get_global_rect(), Vector2(435, 480), "Bindings panel")
	for binding_row in pause_menu.find_children("*_BindingRow", "HBoxContainer", true, false):
		var action_label: Control = binding_row.get_child(0)
		var action_button: Control = binding_row.get_child(1)
		assert_true(action_label.get_global_rect().end.x <= action_button.get_global_rect().position.x + 1.0, "%s label overlaps its binding button" % binding_row.name)
		assert_true(action_button.size.x >= 132.0, "%s binding button width is unstable" % binding_row.name)
	DisplayServer.window_set_size(Vector2i(1600, 900))
	root.size = Vector2i(1600, 900)
	await process_frame

	road_binding.emit_signal("pressed")
	assert_true(road_binding.text == "等待输入...", "Road binding did not enter capture state")
	pause_menu._Input(key_event(KEY_T))
	assert_true(road_binding.text == "T", "Road binding did not accept T")
	assert_true(binding_status.text.contains("已绑定为 T"), "Successful binding did not report status")
	var binding_config := ConfigFile.new()
	assert_true(binding_config.load("user://input_bindings.cfg") == OK, "Binding config was not persisted")
	assert_true(int(binding_config.get_value("bindings", "tool_road")) == KEY_T, "Persisted road binding is not T")

	select_binding.emit_signal("pressed")
	pause_menu._Input(key_event(KEY_T))
	assert_true(select_binding.text == "等待输入...", "Conflicting key should keep capture active")
	assert_true(binding_status.text.contains("已绑定到"), "Conflicting key did not report the existing owner")
	select_binding.emit_signal("pressed")
	assert_true(select_binding.text == "Q", "Repeating the active binding button did not cancel capture")
	pause_binding.emit_signal("pressed")
	assert_true(pause_binding.text == "等待输入...", "Pause binding did not enter capture state")
	pause_menu._Input(key_event(KEY_F10))
	assert_true(pause_binding.text == "F10", "Pause binding did not accept F10")
	assert_true(binding_config.load("user://input_bindings.cfg") == OK and int(binding_config.get_value("bindings", "pause_menu", -1)) == KEY_F10, "Persisted pause binding is not F10")

	bindings_back_button.emit_signal("pressed")
	settings_back_button.emit_signal("pressed")
	assert_true(main_content.visible and not settings_content.visible and not bindings_content.visible, "Bindings back path did not return to main view")
	continue_button.emit_signal("pressed")
	assert_true(not pause_menu.visible and not paused, "Continue after rebinding did not resume the game")
	manager.set("CurrentTool", 0)
	hud._Input(key_event(KEY_T))
	await process_frame
	assert_true(manager.get("CurrentTool") == 1, "Rebound T key did not select the Road tool")
	var shortcut_value: Label = hud.get_node("ToolContextPanel/PanelMargin/Rows/ContextContentScroll/ContextContent/ShortcutRow/ShortcutValue")
	assert_true(shortcut_value.text == "T", "Tool context did not reflect the rebound key")

	hud._Input(key_event(KEY_ESCAPE))
	await process_frame
	assert_true(not pause_menu.visible and not paused, "Old Escape binding still opened PauseMenu after rebinding")
	hud._Input(key_event(KEY_F10))
	await process_frame
	assert_true(pause_menu.visible and paused, "Rebound F10 key did not open PauseMenu")
	pause_menu._Input(key_event(KEY_F10))
	await process_frame
	assert_true(not pause_menu.visible and not paused, "Rebound F10 key did not close PauseMenu")
	hud._Input(key_event(KEY_F10))
	await process_frame
	assert_true(pause_menu.visible and paused, "Rebound F10 key did not reopen PauseMenu")
	settings_button.emit_signal("pressed")
	key_bindings_button.emit_signal("pressed")
	reset_bindings_button.emit_signal("pressed")
	assert_true(road_binding.text == "R" and pause_binding.text == "Escape", "Reset bindings did not restore Road and Pause defaults")
	assert_true(int(binding_config.get_value("bindings", "tool_road", -1)) == KEY_T, "Config snapshot should remain unchanged until reload")
	assert_true(binding_config.load("user://input_bindings.cfg") == OK and int(binding_config.get_value("bindings", "tool_road", -1)) == KEY_R, "Reset Road default was not persisted")
	assert_true(int(binding_config.get_value("bindings", "pause_menu", -1)) == KEY_ESCAPE, "Reset Pause default was not persisted")
	bindings_back_button.emit_signal("pressed")
	settings_back_button.emit_signal("pressed")
	assert_true(main_content.visible and pause_menu.visible and paused, "Reset flow did not return to the paused main view")

	save_button.emit_signal("pressed")
	assert_true(paused and pause_menu.visible, "Saving should keep pause menu open")
	assert_true(status_label.text.contains("已保存"), "Pause save did not report success")
	load_button.emit_signal("pressed")
	assert_true(paused and pause_menu.visible, "Loading should keep pause menu open")
	assert_true(status_label.text.contains("已加载"), "Pause load did not report success")

	exit_desktop_button.emit_signal("pressed")
	await process_frame
	assert_true(confirmation_content.visible, "Desktop exit did not request confirmation")
	assert_true(root.gui_get_focus_owner() == cancel_button, "Desktop exit confirmation did not default focus to CancelButton")
	cancel_button.emit_signal("pressed")
	assert_true(main_content.visible and not confirmation_content.visible, "Desktop exit cancel did not return to main view")
	exit_game_button.emit_signal("pressed")
	assert_true(confirmation_content.visible, "Game exit did not request confirmation")
	cancel_button.emit_signal("pressed")
	assert_true(main_content.visible and not confirmation_content.visible, "Game exit cancel did not return to main view")

	continue_button.emit_signal("pressed")
	assert_true(not pause_menu.visible and not paused, "Continue did not close the pause menu")
	hud._Input(key_event(KEY_ESCAPE))
	exit_game_button.emit_signal("pressed")
	confirm_button.emit_signal("pressed")
	await process_frame
	await process_frame
	assert_true(not paused, "Returning to main menu left the scene tree paused")
	assert_true(current_scene != null and current_scene.scene_file_path == MAIN_MENU_SCENE, "Confirmed game exit did not load MainMenu")
	assert_true(save_manager.get("RegisteredSaveableCount") == 0, "Returning to MainMenu retained scene saveables")

	var start_button: Button = current_scene.get_node("Center/MainPanel/Content/StartButton")
	start_button.emit_signal("pressed")
	await process_frame
	await process_frame
	assert_true(current_scene != null and current_scene.scene_file_path == MAP_SCENE, "MainMenu did not start a new MapTest session")
	assert_true(save_manager.get("RegisteredSaveableCount") == 2, "New MapTest session did not register exactly camera and road graph")
	assert_true(save_manager.Save("autosave"), "Saving after returning through MainMenu failed")
	assert_true(save_manager.Load("autosave"), "Loading after returning through MainMenu failed")

	print("PASS pause menu runtime contract")
	quit(0)

func key_event(keycode: int) -> InputEventKey:
	var event := InputEventKey.new()
	event.keycode = keycode
	event.physical_keycode = keycode
	event.pressed = true
	return event

func assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	push_error(message)
	failed = true
	quit(1)

func assert_rect_in_viewport(rect: Rect2, viewport: Vector2, label: String) -> void:
	assert_true(rect.position.x >= 0.0, "%s left edge is outside the viewport" % label)
	assert_true(rect.position.y >= 0.0, "%s top edge is outside the viewport" % label)
	assert_true(rect.end.x <= viewport.x, "%s right edge is outside the viewport" % label)
	assert_true(rect.end.y <= viewport.y, "%s bottom edge is outside the viewport" % label)
