extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const TEST_SLOT_NAME := "Road input strategy runtime contract"

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var packed_map: PackedScene = load(MAP_SCENE)
	if not assert_true(packed_map != null, "MapTest scene did not load"):
		return
	var map: Node = packed_map.instantiate()
	var autosave_controller: Node = map.get_node("AutosaveController")
	autosave_controller.set("AutosaveEnabled", false)
	root.add_child(map)
	current_scene = map
	await process_frame
	await process_frame

	autosave_controller.SetAutosaveEnabled(false)
	var road_system: Node = map.get_node("RoadSystem")
	var road_builder: Node = road_system.get_node("RoadBuilder")
	var road_renderer: Node = road_system.get_node("RoadRenderer")
	var hud: CanvasLayer = map.get_node("GameHUD")
	var save_manager: Node = root.get_node("SaveManager")

	if not assert_true(road_builder.BeginPlace(Vector2(5, 5)), "RoadBuilder did not begin a placement"):
		return
	road_builder.UpdatePlace(Vector2(130, 10))
	if not assert_true(road_builder.CommitPlace(Vector2(130, 10)), "RoadBuilder did not commit the strategy path"):
		return
	if not assert_true(save_manager.SaveAs(TEST_SLOT_NAME), "Strategy path save failed"):
		return
	var slot_id: String = save_manager.get("CurrentSlotID")
	var roads_path := "res://saves/%s/road_network.json" % slot_id
	if not assert_true(slot_id.begins_with("manual-"), "Strategy path save did not create an isolated slot"):
		return
	if not assert_true(FileAccess.file_exists(roads_path), "Strategy path payload is missing"):
		return
	if not assert_saved_line_path(roads_path):
		return

	if not assert_true(road_builder.BeginPlace(Vector2(300, 300)), "Continuous placement did not begin"):
		return
	if not assert_true(road_builder.AddPlacePoint(Vector2(400, 300)), "Continuous placement did not add its first segment"):
		return
	if not assert_true(road_builder.AddPlacePoint(Vector2(400, 400)), "Continuous placement did not add its second segment"):
		return
	road_builder.UpdatePlace(Vector2(300, 400))
	if not assert_preview_points(
		road_renderer,
		[Vector2(300, 300), Vector2(400, 300), Vector2(400, 400), Vector2(300, 400)]):
		return
	if not assert_true(
		road_builder.RemoveLastPlacePoint(Vector2(400, 400)) and road_builder.GetFixedCornerCount() == 1,
		"Continuous placement did not roll back its last fixed corner"):
		return
	if not assert_true(road_builder.AddPlacePoint(Vector2(400, 400)), "Continuous placement did not restore its second segment"):
		return
	road_builder.UpdatePlace(Vector2(300, 400))
	if not assert_true(road_builder.ConfirmPlace(Vector2(300, 400)), "Continuous placement did not commit"):
		return
	if not assert_true(
		not road_builder.HasActivePlaceSession() and road_renderer.GetPreviewPointCount() == 0,
		"Continuous placement retained its session or preview after commit"):
		return
	if not assert_true(save_manager.Save(slot_id), "Continuous placement save failed"):
		return
	if not assert_saved_continuous_path(roads_path):
		return
	var roads_before_cancel := FileAccess.get_file_as_string(roads_path)

	if not assert_true(road_builder.BeginPlace(Vector2(256, 256)), "RoadBuilder did not begin the cancel scenario"):
		return
	road_builder.UpdatePlace(Vector2(400, 256))
	road_builder.CancelPlaceDrag()
	if not assert_true(save_manager.Save(slot_id), "Save after cancel failed"):
		return
	if not assert_true(FileAccess.get_file_as_string(roads_path) == roads_before_cancel, "Cancel changed the saved RoadGraph"):
		return

	if not assert_true(road_builder.BeginPlace(Vector2(600, 600)), "Rejected placement scenario did not begin"):
		return
	if not assert_true(road_builder.AddPlacePoint(Vector2(700, 600)), "Rejected placement did not add its first segment"):
		return
	if not assert_true(road_builder.AddPlacePoint(Vector2(700, 700)), "Rejected placement did not add its second segment"):
		return
	if not assert_true(
		not road_builder.ConfirmPlace(Vector2(600, 600)) and road_builder.HasActivePlaceSession(),
		"Repeated-point placement was accepted or discarded its editable session"):
		return
	road_builder.CancelPlaceSession()
	if not assert_true(save_manager.Save(slot_id), "Save after rejected placement failed"):
		return
	if not assert_true(FileAccess.get_file_as_string(roads_path) == roads_before_cancel, "Rejected placement changed the saved RoadGraph"):
		return

	move_pointer(road_builder, Vector2(800, 300))
	click_left(road_builder, Vector2(800, 300))
	if not assert_true(road_builder.HasActivePlaceSession(), "Click placement did not retain its initial point"):
		return
	move_pointer(road_builder, Vector2(900, 300))
	click_left(road_builder, Vector2(900, 300))
	if not assert_true(
		road_builder.GetFixedCornerCount() == 1,
		"Click placement did not fix its first corner: active=%s, fixed=%d" % [
			road_builder.HasActivePlaceSession(),
			road_builder.GetFixedCornerCount()]):
		return
	move_pointer(road_builder, Vector2(900, 400))
	click_left(road_builder, Vector2(900, 400))
	if not assert_true(road_builder.GetFixedCornerCount() == 2, "Click placement did not fix its second corner"):
		return
	move_pointer(road_builder, Vector2(800, 400))
	if not assert_preview_points(
		road_renderer,
		[Vector2(800, 300), Vector2(900, 300), Vector2(900, 400), Vector2(800, 400)]):
		return
	road_builder.HandlePlaceInput(key_event(KEY_ENTER))
	await process_frame
	if not assert_true(not road_builder.HasActivePlaceSession(), "Enter did not confirm the click placement"):
		return
	if not assert_true(save_manager.Save(slot_id), "Click placement save failed"):
		return
	if not assert_saved_input_path(roads_path):
		return
	var roads_before_right_cancel := FileAccess.get_file_as_string(roads_path)

	move_pointer(road_builder, Vector2(1000, 300))
	click_left(road_builder, Vector2(1000, 300))
	road_builder.HandlePlaceInput(mouse_button_event(
		MOUSE_BUTTON_RIGHT,
		true,
		road_builder.get_canvas_transform() * Vector2(1000, 300)))
	await process_frame
	if not assert_true(not road_builder.HasActivePlaceSession(), "Right click did not cancel a zero-segment placement"):
		return
	if not assert_true(save_manager.Save(slot_id), "Save after right-click cancel failed"):
		return
	if not assert_true(FileAccess.get_file_as_string(roads_path) == roads_before_right_cancel, "Right-click cancel changed the saved RoadGraph"):
		return

	road_builder.HandleRemoveInput(mouse_button_event(
		MOUSE_BUTTON_LEFT,
		true,
		road_builder.get_canvas_transform() * Vector2(850, 300)))
	move_remove_pointer(road_builder, Vector2(900, 350))
	if not assert_true(
		road_builder.HasActiveRemoveSession() and road_builder.GetRemovalSelectionCount() >= 1,
		"Removal cancel scenario did not retain a stable selection"):
		return
	road_builder.HandleRemoveInput(mouse_button_event(
		MOUSE_BUTTON_RIGHT,
		true,
		road_builder.get_canvas_transform() * Vector2(900, 350)))
	if not assert_true(
		not road_builder.HasActiveRemoveSession() and road_renderer.GetRemovalPreviewEdgeCount() == 0,
		"Right click did not cancel the removal selection and preview"):
		return
	if not assert_true(save_manager.Save(slot_id), "Save after removal cancel failed"):
		return
	if not assert_true(FileAccess.get_file_as_string(roads_path) == roads_before_right_cancel, "Removal cancel changed the saved RoadGraph"):
		return

	road_builder.HandleRemoveInput(mouse_button_event(
		MOUSE_BUTTON_LEFT,
		true,
		road_builder.get_canvas_transform() * Vector2(280, 350)))
	move_remove_pointer(road_builder, Vector2(420, 350))
	if not assert_true(
		road_builder.GetRemovalSelectionCount() == 3 and road_renderer.GetRemovalPreviewEdgeCount() == 3,
		"Continuous removal did not select the three crossed edges exactly once"):
		return
	var mesh_vertices_before_continuous_remove: int = road_renderer.GetRoadMeshVertexCount()
	road_builder.HandleRemoveInput(mouse_button_event(
		MOUSE_BUTTON_LEFT,
		false,
		road_builder.get_canvas_transform() * Vector2(420, 350)))
	if not assert_true(
		not road_builder.HasActiveRemoveSession() and road_renderer.GetRemovalPreviewEdgeCount() == 0,
		"Continuous removal did not commit and clear its preview"):
		return
	if not assert_true(
		road_renderer.GetRenderedEdgeCount() == 4 and
		road_renderer.GetRoadMeshVertexCount() == mesh_vertices_before_continuous_remove,
		"Continuous removal did not defer its merged static batch rebuild"):
		return
	await process_frame
	if not assert_true(
		road_renderer.GetRoadMeshVertexCount() == road_renderer.GetRenderedEdgeCount() * 4,
		"Continuous removal did not publish the merged static batch on the next frame"):
		return
	if not assert_true(save_manager.Save(slot_id), "Continuous removal save failed"):
		return
	if not assert_saved_counts(roads_path, 6, 4, 2, "Continuous removal"):
		return

	road_builder.HandleRemoveInput(mouse_button_event(
		MOUSE_BUTTON_LEFT,
		true,
		road_builder.get_canvas_transform() * Vector2(-50, -50),
		true))
	move_remove_pointer(road_builder, Vector2(850, 350))
	if not assert_true(
		road_builder.GetRemovalSelectionCount() == 2 and road_renderer.GetRemovalPreviewEdgeCount() == 2,
		"Rectangle removal did not select two edges across groups"):
		return
	road_builder.HandleRemoveInput(mouse_button_event(
		MOUSE_BUTTON_LEFT,
		false,
		road_builder.get_canvas_transform() * Vector2(850, 350),
		true))
	if not assert_true(save_manager.Save(slot_id), "Rectangle removal save failed"):
		return
	if not assert_saved_counts(roads_path, 3, 2, 1, "Rectangle removal"):
		return
	if not assert_true(
		road_builder.GetUndoEditCount() == 5 and road_builder.GetRedoEditCount() == 0,
		"Successful and rejected road edits entered the wrong history stacks"):
		return

	hud._Input(action_event("edit_undo"))
	await process_frame
	if not assert_true(
		road_renderer.GetRenderedEdgeCount() == 4 and road_builder.GetUndoEditCount() == 4 and road_builder.GetRedoEditCount() == 1,
		"Undo did not restore the rectangle removal boundary and rebuild rendering"):
		return
	if not assert_true(save_manager.Save(slot_id), "First undo save failed"):
		return
	if not assert_saved_counts(roads_path, 6, 4, 2, "First undo"):
		return

	hud._Input(action_event("edit_undo"))
	await process_frame
	if not assert_true(
		road_renderer.GetRenderedEdgeCount() == 7 and road_builder.GetUndoEditCount() == 3 and road_builder.GetRedoEditCount() == 2,
		"Second undo did not restore the continuous removal boundary"):
		return
	if not assert_true(save_manager.Save(slot_id), "Second undo save failed"):
		return
	if not assert_saved_counts(roads_path, 10, 7, 3, "Second undo"):
		return

	hud._Input(action_event("edit_redo"))
	await process_frame
	if not assert_true(
		road_renderer.GetRenderedEdgeCount() == 4 and road_builder.GetUndoEditCount() == 4 and road_builder.GetRedoEditCount() == 1,
		"First redo did not reproduce the continuous removal"):
		return
	hud._Input(action_event("edit_redo"))
	await process_frame
	if not assert_true(
		road_renderer.GetRenderedEdgeCount() == 2 and road_builder.GetUndoEditCount() == 5 and road_builder.GetRedoEditCount() == 0,
		"Second redo did not reproduce the rectangle removal"):
		return
	if not assert_true(save_manager.Save(slot_id), "Redo save failed"):
		return
	if not assert_saved_counts(roads_path, 3, 2, 1, "Redo"):
		return
	if not assert_true(save_manager.DeleteSlot(slot_id), "Strategy path test slot cleanup failed"):
		return

	map.queue_free()
	await process_frame
	if not assert_true(save_manager.get("RegisteredSaveableCount") == 0, "Runtime cleanup retained saveables"):
		return

	print("PASS road input strategy runtime contract")
	quit(0)

func assert_saved_line_path(roads_path: String) -> bool:
	var payload: Variant = JSON.parse_string(FileAccess.get_file_as_string(roads_path))
	if not assert_true(payload is Dictionary, "RoadGraph payload is not an object"):
		return false
	var graph_data: Dictionary = payload
	var nodes: Array = graph_data.get("nodes", [])
	var edges: Array = graph_data.get("edges", [])
	var groups: Array = graph_data.get("groups", [])
	if not assert_true(nodes.size() == 2, "Saved strategy path node count is wrong"):
		return false
	if not assert_true(edges.size() == 1, "Saved strategy path edge count is wrong"):
		return false
	if not assert_true(groups.size() == 1, "Saved strategy path group count is wrong"):
		return false
	for edge: Variant in edges:
		if not assert_true(edge is Dictionary, "Saved edge is not an object"):
			return false
		var edge_data: Dictionary = edge
		var geometry: Array = edge_data.get("geometry", [])
		if not assert_true(geometry.size() == 1, "Saved edge does not contain one geometry segment"):
			return false
		if not assert_true(str(geometry[0].get("kind", "")) == "line", "Saved geometry is not a native line"):
			return false
		var line: Dictionary = geometry[0]
		var start: Dictionary = line.get("start", {})
		var end: Dictionary = line.get("end", {})
		if not assert_true(float(start.get("x", -1)) == 0.0 and float(start.get("y", -1)) == 0.0, "Saved line start is not snapped to the scene grid"):
			return false
		if not assert_true(float(end.get("x", -1)) == 100.0 and float(end.get("y", -1)) == 0.0, "Saved line end does not match the scene cell size"):
			return false
	return true

func assert_preview_points(road_renderer: Node, expected: Array[Vector2]) -> bool:
	var actual_count: int = road_renderer.GetPreviewPointCount()
	if not assert_true(
		actual_count == expected.size(),
		"Full preview point count is wrong: expected %d, got %d" % [expected.size(), actual_count]):
		return false
	for index: int in expected.size():
		var actual: Vector2 = road_renderer.GetPreviewPoint(index)
		if not assert_true(
			actual.is_equal_approx(expected[index]),
			"Full preview point %d is wrong: expected %s, got %s" % [index, expected[index], actual]):
			return false
	return true

func assert_saved_continuous_path(roads_path: String) -> bool:
	var payload: Variant = JSON.parse_string(FileAccess.get_file_as_string(roads_path))
	if not assert_true(payload is Dictionary, "Continuous RoadGraph payload is not an object"):
		return false
	var graph_data: Dictionary = payload
	var nodes: Array = graph_data.get("nodes", [])
	var edges: Array = graph_data.get("edges", [])
	var groups: Array = graph_data.get("groups", [])
	if not assert_true(nodes.size() == 6, "Continuous path node count is wrong"):
		return false
	if not assert_true(edges.size() == 4, "Continuous path edge count is wrong"):
		return false
	if not assert_true(groups.size() == 2, "Continuous path group count is wrong"):
		return false
	return true

func assert_saved_input_path(roads_path: String) -> bool:
	var payload: Variant = JSON.parse_string(FileAccess.get_file_as_string(roads_path))
	if not assert_true(payload is Dictionary, "Input RoadGraph payload is not an object"):
		return false
	var graph_data: Dictionary = payload
	if not assert_true(graph_data.get("nodes", []).size() == 10, "Click path node count is wrong"):
		return false
	if not assert_true(graph_data.get("edges", []).size() == 7, "Click path edge count is wrong"):
		return false
	if not assert_true(graph_data.get("groups", []).size() == 3, "Click path group count is wrong"):
		return false
	return true

func assert_saved_counts(
	roads_path: String,
	expected_nodes: int,
	expected_edges: int,
	expected_groups: int,
	label: String) -> bool:
	var payload: Variant = JSON.parse_string(FileAccess.get_file_as_string(roads_path))
	if not assert_true(payload is Dictionary, "%s RoadGraph payload is not an object" % label):
		return false
	var graph_data: Dictionary = payload
	if not assert_true(graph_data.get("nodes", []).size() == expected_nodes, "%s node count is wrong" % label):
		return false
	if not assert_true(graph_data.get("edges", []).size() == expected_edges, "%s edge count is wrong" % label):
		return false
	if not assert_true(graph_data.get("groups", []).size() == expected_groups, "%s group count is wrong" % label):
		return false
	return true

func move_pointer(road_builder: Node, position: Vector2) -> void:
	var event := InputEventMouseMotion.new()
	var viewport_position: Vector2 = road_builder.get_canvas_transform() * position
	event.position = viewport_position
	event.global_position = viewport_position
	road_builder.HandlePlaceInput(event)

func move_remove_pointer(road_builder: Node, position: Vector2) -> void:
	var event := InputEventMouseMotion.new()
	var viewport_position: Vector2 = road_builder.get_canvas_transform() * position
	event.position = viewport_position
	event.global_position = viewport_position
	road_builder.HandleRemoveInput(event)

func click_left(road_builder: Node, position: Vector2) -> void:
	var viewport_position: Vector2 = road_builder.get_canvas_transform() * position
	road_builder.HandlePlaceInput(mouse_button_event(MOUSE_BUTTON_LEFT, true, viewport_position))
	road_builder.HandlePlaceInput(mouse_button_event(MOUSE_BUTTON_LEFT, false, viewport_position))

func mouse_button_event(
	button: MouseButton,
	pressed: bool,
	position: Vector2,
	shift_pressed: bool = false) -> InputEventMouseButton:
	var event := InputEventMouseButton.new()
	event.button_index = button
	event.pressed = pressed
	event.position = position
	event.global_position = position
	event.shift_pressed = shift_pressed
	return event

func key_event(keycode: int) -> InputEventKey:
	var event := InputEventKey.new()
	event.keycode = keycode
	event.physical_keycode = keycode
	event.pressed = true
	return event

func action_event(action_name: StringName) -> InputEventAction:
	var event := InputEventAction.new()
	event.action = action_name
	event.pressed = true
	return event

func assert_true(condition: bool, message: String) -> bool:
	if condition:
		return true
	push_error(message)
	quit(1)
	return false
