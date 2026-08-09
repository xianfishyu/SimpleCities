extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const SLOT_NAME := "Road renderer lifecycle runtime contract"

var failed := false
var save_manager: Node
var slot_id := ""
var map: Node

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var packed_map: PackedScene = load(MAP_SCENE)
	require(packed_map != null, "MapTest scene did not load")
	if packed_map == null:
		finish()
		return

	map = packed_map.instantiate()
	map.get_node("AutosaveController").set("AutosaveEnabled", false)
	root.add_child(map)
	current_scene = map
	await process_frame
	await process_frame

	save_manager = root.get_node("SaveManager")
	require(save_manager.SaveAs(SLOT_NAME), "Could not create lifecycle slot")
	slot_id = str(save_manager.get("CurrentSlotID"))
	var renderer: Node = map.get_node("RoadSystem/RoadRenderer")
	renderer.queue_free()
	await process_frame
	await process_frame
	require(not is_instance_valid(renderer), "RoadRenderer was not released")
	require(save_manager.Load(slot_id), "Loading after RoadRenderer exit still invoked disposed render nodes")

	cleanup()
	await process_frame
	await process_frame
	require(save_manager.get("RegisteredSaveableCount") == 0, "Lifecycle cleanup retained saveables")
	finish()

func cleanup() -> void:
	if save_manager != null and not slot_id.is_empty():
		save_manager.DeleteSlot(slot_id)
		slot_id = ""
	if map != null and is_instance_valid(map):
		map.queue_free()

func require(condition: bool, message: String) -> void:
	if condition:
		return
	failed = true
	push_error("FAIL road renderer lifecycle runtime contract: %s" % message)

func finish() -> void:
	if failed:
		cleanup()
		quit(1)
		return
	print("PASS road renderer lifecycle runtime contract")
	quit(0)
