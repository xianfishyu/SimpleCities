extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const SOURCE_SLOT_NAME := "Road renderer lifecycle source"
const ACTIVE_SLOT_NAME := "Road renderer lifecycle active"
const V3_SAVE_FIXTURE := preload("res://tests/godot/v3_save_fixture.gd")
const RESULT_FAILED := 2
const PHASE_COMMIT := 5

var failed := false
var save_manager: Node
var source_slot_id := ""
var active_slot_id := ""
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
	require(await V3_SAVE_FIXTURE.save_as(save_manager, SOURCE_SLOT_NAME), "Could not create lifecycle source slot")
	source_slot_id = str(save_manager.get("CurrentSlotID"))

	var builder: Node = map.get_node("RoadSystem/RoadBuilder")
	var renderer: Node = map.get_node("RoadSystem/RoadRenderer")
	require(builder.BeginPlace(Vector2(200, 350)), "Could not begin the active graph fixture")
	builder.UpdatePlace(Vector2(600, 350))
	require(builder.CommitPlace(Vector2(600, 350)), "Could not commit the active graph fixture")
	await process_frame
	require(renderer.GetRenderedEdgeCount() == 1, "Active graph fixture did not render one edge")
	require(await V3_SAVE_FIXTURE.save_as(save_manager, ACTIVE_SLOT_NAME), "Could not create lifecycle active slot")
	active_slot_id = str(save_manager.get("CurrentSlotID"))
	var source_payload := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(source_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	var active_payload_before := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	require(not source_payload.is_empty(), "Lifecycle source payload was not readable")
	require(not active_payload_before.is_empty(), "Lifecycle active payload was not readable")
	require(source_payload != active_payload_before, "Lifecycle source and active graphs were not distinct")

	var undo_count_before: int = builder.GetUndoEditCount()
	require(undo_count_before == 1, "Active graph fixture did not create one history entry")
	require(builder.BeginPlace(Vector2(800, 300)), "Could not begin the preserved placement fixture")
	require(builder.AddPlacePoint(Vector2(900, 300)), "Could not fix the preserved placement segment")
	require(builder.GetFixedCornerCount() == 1, "Preserved placement fixture has the wrong fixed-corner count")

	renderer.queue_free()
	await process_frame
	await process_frame
	require(not is_instance_valid(renderer), "RoadRenderer was not released")

	var load_token: String = save_manager.StartLoad(source_slot_id)
	var load_result: Dictionary = await V3_SAVE_FIXTURE.wait_for_operation(save_manager, load_token)
	require(await V3_SAVE_FIXTURE.wait_for_idle(save_manager), "Renderer-missing Load did not return to idle")
	require(not load_result.is_empty(), "Renderer-missing Load did not publish a result")
	require(int(load_result.get("resultKind", -1)) == RESULT_FAILED, "Renderer-missing Load was not rejected as a failure")
	require(not bool(load_result.get("committed", true)), "Renderer-missing Load crossed its commit boundary")
	require(int(load_result.get("finalPhase", PHASE_COMMIT)) < PHASE_COMMIT, "Renderer-missing Load failed after commit began")
	require(not str(load_result.get("error", "")).is_empty(), "Renderer-missing Load did not report its participant failure")
	require(str(save_manager.get("CurrentSlotID")) == active_slot_id, "Failed Load changed the active slot")
	require(
		builder.HasActivePlaceSession() and builder.GetFixedCornerCount() == 1,
		"Failed Load changed the active placement session")
	require(
		builder.GetUndoEditCount() == undo_count_before and builder.GetRedoEditCount() == 0,
		"Failed Load changed road edit history")

	require(await V3_SAVE_FIXTURE.save(save_manager, active_slot_id), "Could not recapture the graph after failed Load")
	var active_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	require(active_payload_after == active_payload_before, "Failed Load changed the active RoadGraph")
	require(
		builder.HasActivePlaceSession() and builder.GetFixedCornerCount() == 1,
		"Post-failure Save changed the preserved placement session")

	await cleanup()
	await process_frame
	await process_frame
	require(save_manager.get("RegisteredSaveableCount") == 0, "Lifecycle cleanup retained saveables")
	await finish()

func cleanup() -> void:
	if save_manager != null and not active_slot_id.is_empty():
		await V3_SAVE_FIXTURE.delete_slot(save_manager, active_slot_id)
		active_slot_id = ""
	if save_manager != null and not source_slot_id.is_empty():
		await V3_SAVE_FIXTURE.delete_slot(save_manager, source_slot_id)
		source_slot_id = ""
	if map != null and is_instance_valid(map):
		map.queue_free()

func require(condition: bool, message: String) -> void:
	if condition:
		return
	failed = true
	push_error("FAIL road renderer lifecycle runtime contract: %s" % message)

func finish() -> void:
	if failed:
		await cleanup()
		quit(1)
		return
	print("PASS road renderer lifecycle runtime contract")
	quit(0)
