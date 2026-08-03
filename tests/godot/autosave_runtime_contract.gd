extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const AUTOSAVE_SLOT_ID := "autosave"
const AUTOSAVE_DISPLAY_NAME := "自动存档"

var failed := false

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var packed_map: PackedScene = load(MAP_SCENE)
	assert_true(packed_map != null, "MapTest scene did not load")
	var map: Node = packed_map.instantiate()
	var controller: Node = map.get_node("AutosaveController")
	controller.set("IntervalSeconds", 0.01)
	root.add_child(map)
	current_scene = map
	await process_frame
	await process_frame

	var save_manager: Node = root.get_node("SaveManager")
	assert_true(await wait_for_success_count(controller, 1), "First periodic autosave did not run")
	assert_true(FileAccess.file_exists("res://saves/autosave/manifest.json"), "First periodic autosave did not create manifest")
	assert_true(FileAccess.file_exists("res://saves/autosave/road_network.json"), "First periodic autosave did not create RoadGraph payload")
	assert_true(read_manifest_display_name() == AUTOSAVE_DISPLAY_NAME, "Autosave manifest is not clearly named")
	assert_true(await wait_for_success_count(controller, 2), "Second periodic autosave did not run")

	controller.SetAutosaveEnabled(false)
	var manual_created: bool = save_manager.SaveAs(AUTOSAVE_DISPLAY_NAME)
	var manual_slot_id: String = save_manager.get("CurrentSlotID")
	assert_true(manual_created and manual_slot_id.begins_with("manual-"), "Same-named manual slot was not isolated")
	assert_true(controller.RunAutosaveNow(), "Immediate autosave after manual slot failed")
	assert_true(save_manager.get("CurrentSlotID") == manual_slot_id, "Autosave replaced the selected manual slot")
	assert_true(save_manager.SaveSlotExists(AUTOSAVE_SLOT_ID), "Reserved autosave disappeared after manual save")
	await assert_slot_kind_labels(map, AUTOSAVE_SLOT_ID, manual_slot_id)

	var manifest_before_failure := FileAccess.get_file_as_string("res://saves/autosave/manifest.json")
	var roads_before_failure := FileAccess.get_file_as_string("res://saves/autosave/road_network.json")
	var failure_marker_path := "res://saves/.autosave.staging"
	var failure_marker_absolute := ProjectSettings.globalize_path(failure_marker_path)
	if FileAccess.file_exists(failure_marker_path):
		DirAccess.remove_absolute(failure_marker_absolute)
	var failure_marker := FileAccess.open(failure_marker_path, FileAccess.WRITE)
	assert_true(failure_marker != null, "Could not create autosave publication failure marker")
	failure_marker.store_string("autosave failure injection")
	failure_marker.close()
	var failed_count_before: int = controller.get("FailedSaveCount")
	assert_true(not controller.RunAutosaveNow(), "Autosave unexpectedly succeeded with a blocked staging path")
	assert_true(controller.get("FailedSaveCount") == failed_count_before + 1, "Autosave failure was not recorded")
	assert_true(FileAccess.get_file_as_string("res://saves/autosave/manifest.json") == manifest_before_failure, "Failed autosave changed the last valid manifest")
	assert_true(FileAccess.get_file_as_string("res://saves/autosave/road_network.json") == roads_before_failure, "Failed autosave changed the last valid RoadGraph payload")
	assert_true(DirAccess.remove_absolute(failure_marker_absolute) == OK, "Autosave failure marker cleanup failed")

	assert_true(save_manager.Load(AUTOSAVE_SLOT_ID), "Valid autosave could not be loaded after a failed cycle")
	assert_true(save_manager.get("CurrentSlotID") == AUTOSAVE_SLOT_ID, "Loading autosave did not select the reserved slot")
	assert_true(save_manager.DeleteSlot(manual_slot_id), "Manual autosave-name test slot cleanup failed")
	controller.SetAutosaveEnabled(false)
	map.queue_free()
	await process_frame
	assert_true(save_manager.get("RegisteredSaveableCount") == 0, "Autosave runtime cleanup retained saveables")

	print("PASS autosave runtime contract")
	quit(0)

func wait_for_success_count(controller: Node, expected: int) -> bool:
	for _attempt in 120:
		if int(controller.get("SuccessfulSaveCount")) >= expected:
			return true
		await create_timer(0.01).timeout
	return false

func read_manifest_display_name() -> String:
	var manifest_file := FileAccess.open("res://saves/autosave/manifest.json", FileAccess.READ)
	if manifest_file == null:
		return ""
	var manifest: Variant = JSON.parse_string(manifest_file.get_as_text())
	manifest_file.close()
	return str(manifest.get("displayName", "")) if manifest is Dictionary else ""

func assert_slot_kind_labels(map: Node, autosave_slot_id: String, manual_slot_id: String) -> void:
	var pause_menu: Control = map.get_node("GameHUD/PauseMenu")
	pause_menu.Open()
	await process_frame
	pause_menu.get_node("Center/MainPanel/MainContent/LoadButton").emit_signal("pressed")
	await process_frame
	var item_list: ItemList = pause_menu.get_node("Center/MainPanel/SaveManagementContent/SaveSlotList")
	var autosave_index := find_item_by_metadata(item_list, autosave_slot_id)
	var manual_index := find_item_by_metadata(item_list, manual_slot_id)
	assert_true(autosave_index >= 0 and item_list.get_item_text(autosave_index).begins_with("自动"), "Autosave row is not explicitly marked")
	assert_true(manual_index >= 0 and item_list.get_item_text(manual_index).begins_with("手动"), "Same-named manual row is not explicitly marked")
	pause_menu.Close()
	await process_frame

func find_item_by_metadata(item_list: ItemList, slot_id: String) -> int:
	for index in item_list.item_count:
		if str(item_list.get_item_metadata(index)) == slot_id:
			return index
	return -1

func assert_true(condition: bool, message: String) -> void:
	if condition:
		return
	push_error(message)
	failed = true
	quit(1)
