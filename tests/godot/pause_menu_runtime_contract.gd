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
	var authored_pause_menu: Control = map.get_node("GameHUD/PauseMenu")
	assert_true(not authored_pause_menu.visible, "PauseMenu instance must be hidden before _Ready for editor authoring")
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
	var main_content: Control = pause_menu.get_node("Center/MainPanel/MainContent")
	var save_management_content: Control = pause_menu.get_node("Center/MainPanel/SaveManagementContent")
	var save_name_input: LineEdit = pause_menu.get_node("Center/MainPanel/SaveManagementContent/SaveNameRow/SaveNameInput")
	var save_as_button: Button = pause_menu.get_node("Center/MainPanel/SaveManagementContent/SaveNameRow/SaveAsButton")
	var save_slot_list: ItemList = pause_menu.get_node("Center/MainPanel/SaveManagementContent/SaveSlotList")
	var save_slot_summary: Label = pause_menu.get_node("Center/MainPanel/SaveManagementContent/SaveSlotSummaryLabel")
	var overwrite_save_button: Button = pause_menu.get_node("Center/MainPanel/SaveManagementContent/SaveActions/OverwriteButton")
	var load_save_button: Button = pause_menu.get_node("Center/MainPanel/SaveManagementContent/SaveActions/LoadButton")
	var delete_save_button: Button = pause_menu.get_node("Center/MainPanel/SaveManagementContent/SaveActions/DeleteButton")
	var save_status: Label = pause_menu.get_node("Center/MainPanel/SaveManagementContent/SaveStatusLabel")
	var save_management_back_button: Button = pause_menu.get_node("Center/MainPanel/SaveManagementContent/BackButton")
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

	await activate_focused_with_keyboard(save_button)
	assert_true(paused and pause_menu.visible and save_management_content.visible, "Keyboard Save did not open save management")
	assert_true(root.gui_get_focus_owner() == save_name_input, "Save management did not focus the name input")
	assert_rect_in_viewport(pause_menu.get_node("Center/MainPanel").get_global_rect(), Vector2(1600, 900), "Save management panel")
	DisplayServer.window_set_size(Vector2i(435, 480))
	root.size = Vector2i(435, 480)
	await process_frame
	await process_frame
	assert_rect_in_viewport(pause_menu.get_node("Center/MainPanel").get_global_rect(), Vector2(435, 480), "Small save management panel")
	DisplayServer.window_set_size(Vector2i(1600, 900))
	root.size = Vector2i(1600, 900)
	await process_frame
	await process_frame
	if cleanup_runtime_ui_slots(save_manager, save_slot_list):
		save_management_back_button.emit_signal("pressed")
		await activate_focused_with_keyboard(save_button)

	save_name_input.text = "Runtime UI duplicate"
	await mouse_click(save_as_button)
	var first_ui_slot_id: String = save_manager.get("CurrentSlotID")
	assert_true(first_ui_slot_id.begins_with("manual-"), "Mouse Save As did not create the first manual slot")
	assert_true(save_status.text.contains("已创建"), "First Save As did not report success")
	save_name_input.text = "Runtime UI duplicate"
	await mouse_click(save_as_button)
	var second_ui_slot_id: String = save_manager.get("CurrentSlotID")
	assert_true(second_ui_slot_id.begins_with("manual-") and second_ui_slot_id != first_ui_slot_id, "Duplicate display name did not create an independent slot")
	assert_true(count_items_with_prefix(save_slot_list, "Runtime UI duplicate") >= 2, "Duplicate display names are not both visible")

	var first_ui_index := find_item_by_metadata(save_slot_list, first_ui_slot_id)
	assert_true(first_ui_index >= 0, "First manual slot is missing from save management")
	await mouse_click_item(save_slot_list, first_ui_index)
	assert_selected_slot(save_slot_list, first_ui_slot_id, "first slot before overwrite")
	var first_manifest_path := "res://saves/%s/manifest.json" % first_ui_slot_id
	var first_manifest_before_cancel := FileAccess.get_file_as_string(first_manifest_path)
	await mouse_click(overwrite_save_button)
	await process_frame
	assert_true(confirmation_content.visible and confirmation_message(pause_menu).contains("Runtime UI duplicate"), "Overwrite confirmation omitted the target summary")
	await mouse_click(cancel_button)
	assert_true(save_management_content.visible and FileAccess.get_file_as_string(first_manifest_path) == first_manifest_before_cancel, "Cancel overwrite modified the target slot")

	await mouse_click(overwrite_save_button)
	await process_frame
	await activate_focused_with_keyboard(confirm_button)
	assert_true(save_management_content.visible and save_status.text.contains("已覆盖"), "Keyboard confirmation did not overwrite the selected slot")
	assert_true(save_manager.get("CurrentSlotID") == first_ui_slot_id, "Overwrite selected the wrong slot")

	var second_ui_index := find_item_by_metadata(save_slot_list, second_ui_slot_id)
	assert_true(second_ui_index >= 0, "Second manual slot is missing after overwrite refresh")
	await mouse_click_item(save_slot_list, second_ui_index)
	assert_selected_slot(save_slot_list, second_ui_slot_id, "second slot before load")
	await mouse_click(load_save_button)
	await process_frame
	assert_true(confirmation_content.visible and confirmation_message(pause_menu).contains("Runtime UI duplicate"), "Load confirmation omitted the target summary")
	await mouse_click(cancel_button)
	assert_true(save_manager.get("CurrentSlotID") == first_ui_slot_id, "Cancel load changed CurrentSlotID")
	assert_selected_slot(save_slot_list, second_ui_slot_id, "second slot after cancel load")
	await mouse_click(load_save_button)
	await process_frame
	await activate_focused_with_keyboard(confirm_button)
	assert_true(save_manager.get("CurrentSlotID") == second_ui_slot_id and save_status.text.contains("已加载"), "Confirmed load did not select the target slot")

	first_ui_index = find_item_by_metadata(save_slot_list, first_ui_slot_id)
	await mouse_click_item(save_slot_list, first_ui_index)
	assert_selected_slot(save_slot_list, first_ui_slot_id, "first slot before delete")
	await mouse_click(delete_save_button)
	await process_frame
	assert_true(confirmation_content.visible and confirmation_message(pause_menu).contains("Runtime UI duplicate"), "Delete confirmation omitted the target summary")
	await mouse_click(cancel_button)
	assert_true(save_manager.SaveSlotExists(first_ui_slot_id), "Cancel delete removed the target slot")
	await mouse_click(delete_save_button)
	await process_frame
	await mouse_click(confirm_button)
	assert_true(not save_manager.SaveSlotExists(first_ui_slot_id) and save_status.text.contains("已删除"), "Confirmed mouse delete retained the target slot")

	save_name_input.text = "Runtime UI damaged"
	await mouse_click(save_as_button)
	var damaged_ui_slot_id: String = save_manager.get("CurrentSlotID")
	var damaged_manifest := FileAccess.open("res://saves/%s/manifest.json" % damaged_ui_slot_id, FileAccess.WRITE)
	assert_true(damaged_manifest != null, "Could not open manual manifest for damaged-slot UI contract")
	damaged_manifest.store_string("{broken")
	damaged_manifest.close()
	await mouse_click(save_management_back_button)
	await activate_focused_with_keyboard(load_button)
	var damaged_ui_index := find_item_by_metadata(save_slot_list, damaged_ui_slot_id)
	assert_true(damaged_ui_index >= 0, "Damaged slot is missing from save management")
	await mouse_click_item(save_slot_list, damaged_ui_index)
	assert_true(save_slot_summary.text.contains("损坏存档"), "Damaged slot did not show an explicit diagnostic")
	assert_true(overwrite_save_button.disabled and load_save_button.disabled and not delete_save_button.disabled, "Damaged slot actions are not safely constrained")
	await mouse_click(delete_save_button)
	await process_frame
	await mouse_click(confirm_button)
	assert_true(not save_manager.SaveSlotExists(damaged_ui_slot_id), "Damaged slot could not be deleted through save management")
	assert_true(save_manager.DeleteSlot(second_ui_slot_id), "Runtime duplicate slot cleanup failed")
	await mouse_click(save_management_back_button)
	assert_true(main_content.visible and paused, "Save management Back did not return to the paused main view")

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
	var manifest_file := FileAccess.open("res://saves/autosave/manifest.json", FileAccess.READ)
	assert_true(manifest_file != null, "V2 autosave manifest is missing")
	var manifest: Dictionary = JSON.parse_string(manifest_file.get_as_text())
	manifest_file.close()
	assert_true(manifest.get("files", []) == ["road_network.json"], "V2 manifest must contain only road_network.json")
	assert_true(not FileAccess.file_exists("res://saves/autosave/camera.json"), "V2 autosave unexpectedly contains camera.json")
	var runtime_camera: Camera2D = current_scene.get_node("Camera2D")
	runtime_camera.position = Vector2(321.0, 654.0)
	assert_true(save_manager.Load("autosave"), "Loading after returning through MainMenu failed")
	assert_true(runtime_camera.position == Vector2(321.0, 654.0), "V2 load changed excluded camera state")
	assert_true(save_manager.SaveAs("Runtime prevalidation"), "Creating runtime manual slot failed")
	var manual_slot_id: String = save_manager.get("CurrentSlotID")
	assert_true(manual_slot_id.begins_with("manual-"), "SaveAs did not select a generated manual slot ID")
	assert_true(not save_manager.Save("missing-slot"), "Saving a nonexistent manual slot should fail")
	assert_true(save_manager.get("CurrentSlotID") == manual_slot_id, "Failed save changed CurrentSlotID")
	assert_true(not save_manager.DeleteSlot("../escape"), "Deleting an unsafe slot ID should fail")
	assert_true(save_manager.get("CurrentSlotID") == manual_slot_id, "Failed delete changed CurrentSlotID")
	var road_file := FileAccess.open("res://saves/autosave/road_network.json", FileAccess.WRITE)
	assert_true(road_file != null, "Could not corrupt autosave road file for prevalidation contract")
	road_file.store_string("{\"schemaVersion\":1,\"nextID\":1,\"nodes\":[{\"id\":0,\"x\":0,\"y\":0}],\"edges\":[],\"groups\":[]}")
	road_file.close()
	assert_true(not save_manager.Load("autosave"), "Corrupt RoadGraph payload was accepted")
	assert_true(save_manager.get("CurrentSlotID") == manual_slot_id, "Failed load changed CurrentSlotID")
	assert_true(save_manager.DeleteSlot(manual_slot_id), "Runtime manual slot cleanup failed")
	assert_true(save_manager.Save("autosave"), "Autosave cleanup after corrupt-load contract failed")

	print("PASS pause menu runtime contract")
	quit(0)

func key_event(keycode: int) -> InputEventKey:
	var event := InputEventKey.new()
	event.keycode = keycode
	event.physical_keycode = keycode
	event.pressed = true
	return event

func activate_focused_with_keyboard(control: Control) -> void:
	await process_frame
	control.grab_focus()
	await process_frame
	assert_true(root.gui_get_focus_owner() == control, "%s did not receive keyboard focus" % control.name)
	assert_true(control.is_visible_in_tree(), "%s is not visible for keyboard activation" % control.name)
	if control is BaseButton:
		assert_true(not control.disabled, "%s is disabled for keyboard activation" % control.name)
	var press := InputEventAction.new()
	press.action = &"ui_accept"
	press.pressed = true
	press.strength = 1.0
	Input.parse_input_event(press)
	await process_frame
	var release := press.duplicate()
	release.pressed = false
	release.strength = 0.0
	Input.parse_input_event(release)
	await process_frame

func mouse_click(control: Control) -> void:
	assert_true(control is BaseButton, "%s is not a mouse-activatable button" % control.name)
	assert_true(control.is_visible_in_tree(), "%s is not visible for mouse activation" % control.name)
	assert_true(not control.disabled, "%s is disabled for mouse activation" % control.name)
	assert_true(control.get_global_rect().has_point(control.get_global_rect().get_center()), "%s has no mouse hit area" % control.name)
	control.emit_signal("pressed")
	await process_frame

func mouse_click_item(item_list: ItemList, index: int) -> void:
	var item_rect := item_list.get_item_rect(index)
	assert_true(item_list.is_visible_in_tree(), "%s is not visible for mouse selection" % item_list.name)
	assert_true(Rect2(Vector2.ZERO, item_list.size).has_point(item_rect.get_center()), "%s item is outside the mouse hit area" % item_list.name)
	item_list.select(index)
	item_list.emit_signal("item_selected", index)
	await process_frame

func find_item_by_metadata(item_list: ItemList, slot_id: String) -> int:
	for index in item_list.item_count:
		if str(item_list.get_item_metadata(index)) == slot_id:
			return index
	return -1

func count_items_with_prefix(item_list: ItemList, prefix: String) -> int:
	var count := 0
	for index in item_list.item_count:
		if item_list.get_item_text(index).begins_with(prefix):
			count += 1
	return count

func assert_selected_slot(item_list: ItemList, slot_id: String, label: String) -> void:
	var selected := item_list.get_selected_items()
	assert_true(selected.size() == 1 and str(item_list.get_item_metadata(selected[0])) == slot_id, "%s selected the wrong slot" % label)

func cleanup_runtime_ui_slots(save_manager: Node, item_list: ItemList) -> bool:
	var removed := false
	for index in item_list.item_count:
		if not item_list.get_item_text(index).begins_with("Runtime UI "):
			continue
		removed = save_manager.DeleteSlot(str(item_list.get_item_metadata(index))) or removed
	return removed

func confirmation_message(pause_menu: Control) -> String:
	return pause_menu.get_node("Center/MainPanel/ConfirmationContent/ConfirmationMessage").text

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
