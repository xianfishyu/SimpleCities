extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const TEST_SLOT_NAME := "Road closed ribbon runtime contract"
const V3_SAVE_FIXTURE := preload("res://tests/godot/v3_save_fixture.gd")

var failed := false
var map: Node
var save_manager: Node
var slot_id := ""

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var packed_map: PackedScene = load(MAP_SCENE)
	require(packed_map != null, "MapTest scene did not load")
	if packed_map == null:
		await finish()
		return

	map = packed_map.instantiate()
	map.get_node("AutosaveController").set("AutosaveEnabled", false)
	root.add_child(map)
	current_scene = map
	await process_frame
	await process_frame

	map.get_node("AutosaveController").SetAutosaveEnabled(false)
	save_manager = root.get_node("SaveManager")
	var builder: Node = map.get_node("RoadSystem/RoadBuilder")
	var renderer: Node = map.get_node("RoadSystem/RoadRenderer")

	require(create_square_loop(builder), "Could not create the square self-loop")
	await process_frame
	require_closed_ribbon(renderer, "Normal graph mutation")

	var saved := await V3_SAVE_FIXTURE.save_as(save_manager, TEST_SLOT_NAME)
	require(saved, "Could not save the square self-loop")
	if saved:
		slot_id = str(save_manager.get("CurrentSlotID"))
		require(slot_id.begins_with("manual-"), "Save As did not create an isolated manual slot")

	require(builder.BeginPlace(Vector2(300.0, 0.0)), "Could not begin the open-road mutation")
	builder.UpdatePlace(Vector2(400.0, 0.0))
	require(builder.CommitPlace(Vector2(400.0, 0.0)), "Could not commit the open-road mutation")
	await process_frame
	require(renderer.GetRenderedEdgeCount() == 2, "Open-road mutation did not create a second Edge")
	require(renderer.GetRoadMeshVertexCount() == 12, "Open-road mutation published the wrong mesh size")
	require(renderer.GetNodeMarkerCount() == 2, "Open-road mutation published the wrong endpoint markers")

	if not slot_id.is_empty():
		require(await V3_SAVE_FIXTURE.load_slot(save_manager, slot_id), "Could not load the saved self-loop")
		await process_frame
		await process_frame
		require(str(save_manager.get("CurrentSlotID")) == slot_id, "Load did not select the saved slot")
		require_closed_ribbon(renderer, "Aggregate Load")
		require(
			builder.GetUndoEditCount() == 0 and builder.GetRedoEditCount() == 0,
			"Aggregate Load did not reset edit history")

		require(
			create_branch(builder, Vector2.ZERO, Vector2(-100.0, 0.0)),
			"Could not create the branch at the original loop seam")
		require(
			create_branch(builder, Vector2(100.0, 100.0), Vector2(200.0, 100.0)),
			"Could not create the branch at the opposite loop junction")
		await process_frame
		require(renderer.GetRenderedEdgeCount() == 4, "Two-junction loop did not publish four Edges")
		require_junction_patch_presentation(renderer, 38, 2, 20, "Normal junction mutation")
		require(
			await V3_SAVE_FIXTURE.save(save_manager, slot_id),
			"Could not overwrite the slot with the two-junction graph")

		var removed_branch_end := Vector2(-100.0, 0.0)
		require(builder.BeginRemove(removed_branch_end, false), "Could not begin seam-side branch removal")
		require(builder.GetRemovalSelectionCount() == 1, "Seam-side removal did not select exactly one Edge")
		require(builder.ConfirmRemove(removed_branch_end), "Could not remove the seam-side branch")
		await process_frame
		var relocated_edge_count: int = renderer.GetRenderedEdgeCount()
		var relocated_vertex_count: int = renderer.GetRoadMeshVertexCount()
		var relocated_marker_count: int = renderer.GetNodeMarkerCount()
		require(
			relocated_edge_count == 2,
			"Seam relocation left %d Edges instead of one loop and one branch" % relocated_edge_count)
		require(
			relocated_vertex_count == 21,
			"Seam relocation published %d mesh vertices instead of 21" % relocated_vertex_count)
		require(
			relocated_marker_count == 1,
			"Seam relocation published %d markers instead of 1" % relocated_marker_count)
		require_junction_patch_presentation(renderer, 21, 1, 14, "Seam relocation")

		require(
			await V3_SAVE_FIXTURE.load_slot(save_manager, slot_id),
			"Could not reload the saved two-junction graph")
		await process_frame
		await process_frame
		require(renderer.GetRenderedEdgeCount() == 4, "Junction Load did not restore four Edges")
		require_junction_patch_presentation(renderer, 38, 2, 20, "Aggregate junction Load")
		require(
			builder.GetUndoEditCount() == 0 and builder.GetRedoEditCount() == 0,
			"Aggregate junction Load did not reset edit history")

	await finish()

func create_square_loop(builder: Node) -> bool:
	return (
		builder.BeginPlace(Vector2.ZERO) and
		builder.AddPlacePoint(Vector2(100.0, 0.0)) and
		builder.AddPlacePoint(Vector2(100.0, 100.0)) and
		builder.AddPlacePoint(Vector2(0.0, 100.0)) and
		builder.ConfirmPlace(Vector2.ZERO))

func create_branch(builder: Node, start: Vector2, end: Vector2) -> bool:
	if not builder.BeginPlace(start):
		return false
	builder.UpdatePlace(end)
	return builder.CommitPlace(end)

func require_closed_ribbon(renderer: Node, source: String) -> void:
	require(renderer.GetRenderedEdgeCount() == 1, "%s did not publish one self-loop Edge" % source)
	require(renderer.GetRoadMeshVertexCount() == 8, "%s retained a duplicate seam vertex pair" % source)
	require(renderer.GetNodeMarkerCount() == 0, "%s published a false seam marker" % source)

func require_junction_patch_presentation(
	renderer: Node,
	expected_vertices: int,
	expected_markers: int,
	expected_primitives: int,
	source: String) -> void:
	var state: Dictionary = renderer.GetPresentationState()
	require(
		renderer.GetRoadMeshVertexCount() == expected_vertices,
		"%s published %d mesh vertices instead of %d" % [
			source,
			renderer.GetRoadMeshVertexCount(),
			expected_vertices,
		])
	require(
		renderer.GetNodeMarkerCount() == expected_markers,
		"%s published %d endpoint markers instead of %d" % [
			source,
			renderer.GetNodeMarkerCount(),
			expected_markers,
		])
	require(bool(state.get("isReady", false)), "%s is not presentation-ready" % source)
	require(
		state.get("desired", {}) == state.get("presented", {}),
		"%s published mismatched desired/presented tokens" % source)
	require(
		int(state.get("surfacePrimitiveCount", -1)) == expected_primitives,
		"%s published %d surface primitives instead of %d" % [
			source,
			int(state.get("surfacePrimitiveCount", -1)),
			expected_primitives,
		])

func cleanup() -> void:
	if save_manager != null and not slot_id.is_empty():
		await V3_SAVE_FIXTURE.delete_slot(save_manager, slot_id)
		slot_id = ""
	if map != null and is_instance_valid(map):
		map.queue_free()
		await process_frame
		await process_frame

func require(condition: bool, message: String) -> void:
	if condition:
		return
	failed = true
	push_error("FAIL road closed ribbon runtime contract: %s" % message)

func finish() -> void:
	await cleanup()
	if save_manager != null:
		require(save_manager.get("RegisteredSaveableCount") == 0, "Cleanup retained saveables")
	if failed:
		quit(1)
		return
	print("PASS road closed ribbon runtime contract")
	quit(0)
