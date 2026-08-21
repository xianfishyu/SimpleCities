extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const TEST_SLOT_NAME := "Road parallel edge runtime contract"
const V3_SAVE_FIXTURE := preload("res://tests/godot/v3_save_fixture.gd")

var test_map: Node
var save_manager: Node
var slot_id := ""
var failure_cleanup_started := false

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var packed_map: PackedScene = load(MAP_SCENE)
	if not require(packed_map != null, "MapTest scene did not load"):
		return
	test_map = packed_map.instantiate()
	test_map.get_node("AutosaveController").set("AutosaveEnabled", false)
	root.add_child(test_map)
	current_scene = test_map
	await process_frame
	await process_frame
	test_map.get_node("AutosaveController").SetAutosaveEnabled(false)

	save_manager = root.get_node("SaveManager")
	var renderer: Node = test_map.get_node("RoadSystem/RoadRenderer")
	var builder: Node = test_map.get_node("RoadSystem/RoadBuilder")
	if not require(
		await V3_SAVE_FIXTURE.save_as(save_manager, TEST_SLOT_NAME),
		"Parallel Edge fixture slot was not created"):
		return
	slot_id = str(save_manager.get("CurrentSlotID"))
	if not require(
		V3_SAVE_FIXTURE.publish_payload(slot_id, build_fixture()),
		"Parallel Edge fixture payload could not be published"):
		return
	if not require(
		await V3_SAVE_FIXTURE.load_slot(save_manager, slot_id),
		"Parallel Edge fixture did not load"):
		return
	if not await wait_for_presentation(renderer, "Parallel Edge fixture Load"):
		return

	var first_position := Vector2(40.0, -16.0)
	var second_position := Vector2(40.0, 16.0)
	var first_hit: Dictionary = renderer.FindRoadSurfaceHit(first_position, 0.0)
	var second_hit: Dictionary = renderer.FindRoadSurfaceHit(second_position, 0.0)
	var first_location: Dictionary = first_hit.get("location", {})
	var second_location: Dictionary = second_hit.get("location", {})
	var first_edge_id: int = int(first_location.get("edgeID", -1))
	var second_edge_id: int = int(second_location.get("edgeID", -1))
	if not require(
		renderer.GetRenderedEdgeCount() == 2 and
		first_hit.get("ownerKind", "") == "EdgeRibbon" and
		second_hit.get("ownerKind", "") == "EdgeRibbon" and
		first_edge_id > 0 and
		second_edge_id > 0 and
		first_edge_id != second_edge_id,
		"Parallel Edge surface queries did not expose two distinct EdgeRibbon owners"):
		return

	if not require(
		builder.BeginRemove(first_position, false) and
		builder.GetRemovalSelectionCount() == 1 and
		renderer.GetRemovalPreviewEdgeCount() == 1,
		"Parallel Edge removal did not select exactly one arc"):
		return
	if not require(
		builder.ConfirmRemove(first_position),
		"Parallel Edge removal did not commit"):
		return
	if not await wait_for_presentation(renderer, "Parallel Edge removal"):
		return
	if not require(
		renderer.GetRenderedEdgeCount() == 1,
		"Parallel Edge removal changed the wrong number of Edges"):
		return
	if not require(builder.UndoLastEdit(), "Parallel Edge removal undo did not start"):
		return
	if not await wait_for_presentation(renderer, "Parallel Edge removal undo"):
		return
	if not require(
		renderer.GetRenderedEdgeCount() == 2,
		"Parallel Edge removal undo did not restore both arcs"):
		return

	if not require(builder.SetSelectedRoadType(2), "Parallel Edge upgrade could not select Arterial"):
		return
	if not require(
		builder.BeginUpgrade(second_position, false) and
		builder.GetUpgradeSelectionCount() == 1 and
		renderer.GetUpgradePreviewEdgeCount() == 1,
		"Parallel Edge upgrade did not select exactly one arc"):
		return
	if not require(builder.ConfirmUpgrade(second_position), "Parallel Edge upgrade did not commit"):
		return
	if not await wait_for_presentation(renderer, "Parallel Edge upgrade"):
		return
	if not require(builder.UndoLastEdit(), "Parallel Edge upgrade undo did not start"):
		return
	if not await wait_for_presentation(renderer, "Parallel Edge upgrade undo"):
		return
	if not require(
		renderer.GetRenderedEdgeCount() == 2,
		"Parallel Edge upgrade undo did not restore both arcs"):
		return

	var restored_first_hit: Dictionary = renderer.FindRoadSurfaceHit(first_position, 0.0)
	var restored_second_hit: Dictionary = renderer.FindRoadSurfaceHit(second_position, 0.0)
	var restored_first_location: Dictionary = restored_first_hit.get("location", {})
	var restored_second_location: Dictionary = restored_second_hit.get("location", {})
	if not require(
		int(restored_first_location.get("edgeID", -1)) !=
		int(restored_second_location.get("edgeID", -1)),
		"Parallel Edge undo did not preserve independent surface owners"):
		return

	if not require(
		await V3_SAVE_FIXTURE.delete_slot(save_manager, slot_id),
		"Parallel Edge fixture slot cleanup failed"):
		return
	slot_id = ""
	test_map.queue_free()
	await process_frame
	await process_frame
	if not require(
		save_manager.get("RegisteredSaveableCount") == 0,
		"Parallel Edge cleanup retained saveables"):
		return
	print("PASS road parallel edge runtime contract")
	quit(0)

func wait_for_presentation(renderer: Node, source: String) -> bool:
	for _frame in range(600):
		var state: Dictionary = renderer.GetPresentationState()
		if bool(state.get("isReady", false)) and state.get("desired", {}) == state.get("presented", {}):
			return true
		await process_frame
	return require(false, "%s did not publish a matching presentation" % source)

func build_fixture() -> Dictionary:
	return {
		"formatFamily": "simple-cities-v3",
		"payloadType": "road-network",
		"schemaVersion": 1,
		"nextID": 5,
		"nodes": [
			{"id": 1, "x": 0.0, "y": 0.0},
			{"id": 2, "x": 100.0, "y": 0.0},
		],
		"edges": [
			{
				"id": 3,
				"nodeAID": 1,
				"nodeBID": 2,
				"roadType": "street",
				"geometry": [
					{
						"version": 1,
						"kind": "line",
						"start": {"x": 0.0, "y": 0.0},
						"end": {"x": 50.0, "y": -20.0},
					},
					{
						"version": 1,
						"kind": "line",
						"start": {"x": 50.0, "y": -20.0},
						"end": {"x": 100.0, "y": 0.0},
					},
				],
			},
			{
				"id": 4,
				"nodeAID": 1,
				"nodeBID": 2,
				"roadType": "highway",
				"geometry": [
					{
						"version": 1,
						"kind": "line",
						"start": {"x": 0.0, "y": 0.0},
						"end": {"x": 50.0, "y": 20.0},
					},
					{
						"version": 1,
						"kind": "line",
						"start": {"x": 50.0, "y": 20.0},
						"end": {"x": 100.0, "y": 0.0},
					},
				],
			},
		],
	}

func require(condition: bool, message: String) -> bool:
	if condition:
		return true
	push_error(message)
	if not failure_cleanup_started:
		failure_cleanup_started = true
		cleanup_after_failure.call_deferred()
	return false

func cleanup_after_failure() -> void:
	if save_manager != null and not slot_id.is_empty():
		await V3_SAVE_FIXTURE.delete_slot(save_manager, slot_id)
		slot_id = ""
	if test_map != null:
		test_map.queue_free()
	quit(1)
