extends Node

const ROAD_CONFIG := "res://Scenes/road_config.tres"
const ROAD_SYSTEM_SCRIPT := "res://Scripts/Road/RoadSystem.cs"
const ROAD_RENDERER_SCRIPT := "res://Scripts/Road/RoadRenderer.cs"
const ROAD_BUILDER_SCRIPT := "res://Scripts/Road/RoadBuilder.cs"
const TOOL_MANAGER_SCRIPT := "res://Scripts/Tools/ToolManager.cs"
const DISPLAY_NAME := "导出城市 / Summer: 2026"
const V3_SAVE_FIXTURE := preload("res://tests/godot/v3_save_fixture.gd")

var test_fixture: Node
var save_manager: Node
var slot_id := ""
var failure_started := false

func _ready() -> void:
	run.call_deferred()

func run() -> void:
	var expect_read_only := OS.get_cmdline_user_args().has("--expect-read-only")
	if not require(not OS.has_feature("editor"), "Contract must run from an exported package"):
		return
	if not require(OS.has_feature("windows"), "Contract requires a Windows export"):
		return

	var executable_dir := OS.get_executable_path().get_base_dir().replace("\\", "/").trim_suffix("/")
	var save_root := ProjectSettings.globalize_path("user://saves-v3").replace("\\", "/").trim_suffix("/")
	if not require(
		not save_root.begins_with(executable_dir + "/"),
		"Exported saves still resolve beside the executable"):
		return

	test_fixture = create_save_fixture()
	if not require(test_fixture != null, "Minimal RoadGraph fixture did not load from the export package"):
		return
	add_child(test_fixture)
	await get_tree().process_frame

	save_manager = get_node("/root/SaveManager")
	if not require(save_manager.get("RegisteredSaveableCount") == 1, "RoadGraph fixture was not registered"):
		return
	var current_slot_before: String = save_manager.get("CurrentSlotID")
	if expect_read_only:
		if not require(not await V3_SAVE_FIXTURE.save_as(save_manager, DISPLAY_NAME), "Read-only save root unexpectedly accepted SaveAs"):
			return
		if not require(
			save_manager.get("CurrentSlotID") == current_slot_before,
			"Failed read-only save changed CurrentSlotID"):
			return
		if not require(save_root_has_no_entries(save_root), "Failed read-only save published filesystem content"):
			return
		await cleanup()
		print("PASS exported save read-only ACL contract")
		get_tree().quit(0)
		return

	if not require(await V3_SAVE_FIXTURE.save_as(save_manager, DISPLAY_NAME), "Exported SaveAs failed in writable user data"):
		return
	slot_id = save_manager.get("CurrentSlotID")
	if not require(slot_id.begins_with("manual-") and slot_id.length() == 39, "Exported manual slot ID is unsafe"):
		return
	var manifest_path := "user://saves-v3/%s/manifest.json" % slot_id
	var road_path := "user://saves-v3/%s/road_network.json" % slot_id
	if not require(FileAccess.file_exists(manifest_path), "Exported manifest is missing from user data"):
		return
	if not require(FileAccess.file_exists(road_path), "Exported RoadGraph payload is missing from user data"):
		return
	var manifest: Variant = JSON.parse_string(FileAccess.get_file_as_string(manifest_path))
	if not require(manifest is Dictionary, "Exported manifest is not a JSON object"):
		return
	if not require(manifest.slotId == slot_id, "Exported manifest slotId does not match its directory"):
		return
	if not require(manifest.displayName == DISPLAY_NAME, "Exported display name did not round-trip"):
		return
	var files: Array = manifest.get("files", [])
	if not require(
		manifest.get("formatFamily", "") == "simple-cities-v3" and
		manifest.get("schemaVersion", -1) == 1 and
		files.size() == 1 and
		files[0] is Dictionary and
		files[0].get("name", "") == "road_network.json" and
		int(files[0].get("encodedLength", -1)) > 0 and
		str(files[0].get("sha256", "")).length() == 64,
		"Exported V3 manifest metadata is invalid"):
		return
	if not require(await V3_SAVE_FIXTURE.delete_slot(save_manager, slot_id), "Exported fixture slot cleanup failed"):
		return
	slot_id = ""
	await cleanup()
	print("PASS exported save writable user data contract")
	get_tree().quit(0)

func save_root_has_no_entries(save_root: String) -> bool:
	var directory := DirAccess.open(save_root)
	if directory == null:
		return true
	directory.list_dir_begin()
	var entry := directory.get_next()
	directory.list_dir_end()
	return entry.is_empty()

func create_save_fixture() -> Node:
	var road_config: Resource = load(ROAD_CONFIG)
	var road_system_script: Script = load(ROAD_SYSTEM_SCRIPT)
	var road_renderer_script: Script = load(ROAD_RENDERER_SCRIPT)
	var road_builder_script: Script = load(ROAD_BUILDER_SCRIPT)
	var tool_manager_script: Script = load(TOOL_MANAGER_SCRIPT)
	if road_config == null or road_system_script == null or road_renderer_script == null or road_builder_script == null or tool_manager_script == null:
		return null

	var fixture := Node2D.new()
	fixture.name = "ExportedSaveFixture"
	var road_system := Node2D.new()
	road_system.name = "RoadSystem"
	road_system.set_script(road_system_script)
	var road_renderer := Node2D.new()
	road_renderer.name = "RoadRenderer"
	road_renderer.set_script(road_renderer_script)
	road_renderer.set("Config", road_config)
	var road_builder := Node2D.new()
	road_builder.name = "RoadBuilder"
	road_builder.set_script(road_builder_script)
	road_builder.set("Config", road_config)
	road_system.add_child(road_renderer)
	road_system.add_child(road_builder)
	fixture.add_child(road_system)
	var tool_manager := Node2D.new()
	tool_manager.name = "ToolManager"
	tool_manager.set_script(tool_manager_script)
	fixture.add_child(tool_manager)
	return fixture

func cleanup() -> void:
	if save_manager != null and not slot_id.is_empty():
		await V3_SAVE_FIXTURE.delete_slot(save_manager, slot_id)
		slot_id = ""
	if test_fixture != null:
		test_fixture.queue_free()
		await get_tree().process_frame
		await get_tree().process_frame
		test_fixture = null
	if save_manager != null:
		require(save_manager.get("RegisteredSaveableCount") == 0, "RoadGraph fixture cleanup retained a saveable")

func require(condition: bool, message: String) -> bool:
	if condition:
		return true
	fail(message)
	return false

func fail(message: String) -> void:
	push_error(message)
	if failure_started:
		return
	failure_started = true
	cleanup_failed_run.call_deferred()

func cleanup_failed_run() -> void:
	if save_manager != null and not slot_id.is_empty():
		await V3_SAVE_FIXTURE.delete_slot(save_manager, slot_id)
		slot_id = ""
	if test_fixture != null:
		test_fixture.queue_free()
		await get_tree().process_frame
	get_tree().quit(1)
