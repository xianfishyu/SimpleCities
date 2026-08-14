extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const TEST_SLOT_NAME := "Road input strategy runtime contract"
const V3_SAVE_FIXTURE := preload("res://tests/godot/v3_save_fixture.gd")

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
	var tool_manager: Node = map.get_node("ToolManager")
	var pause_menu: Control = hud.get_node("PauseMenu")
	var save_manager: Node = root.get_node("SaveManager")

	if not assert_true(road_builder.BeginPlace(Vector2(5, 5)), "RoadBuilder did not begin a placement"):
		return
	road_builder.UpdatePlace(Vector2(130, 10))
	if not assert_true(road_builder.CommitPlace(Vector2(130, 10)), "RoadBuilder did not commit the strategy path"):
		return
	if not assert_true(await V3_SAVE_FIXTURE.save_as(save_manager, TEST_SLOT_NAME), "Strategy path save failed"):
		return
	var slot_id: String = save_manager.get("CurrentSlotID")
	var roads_path := "user://saves-v3/%s/road_network.json" % slot_id
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
	if not assert_true(road_builder.AddPlacePoint(Vector2(300, 400)), "Closed placement did not add its third segment"):
		return
	road_builder.UpdatePlace(Vector2(300, 300))
	if not assert_preview_points(
		road_renderer,
		[Vector2(300, 300), Vector2(400, 300), Vector2(400, 400), Vector2(300, 400), Vector2(300, 300)]):
		return
	if not assert_true(road_builder.ConfirmPlace(Vector2(300, 300)), "Closed placement did not commit"):
		return
	if not assert_true(
		not road_builder.HasActivePlaceSession() and road_renderer.GetPreviewPointCount() == 0,
		"Closed placement retained its session or preview after commit"):
		return
	await process_frame
	if not assert_true(
		road_renderer.GetRenderedEdgeCount() == 2 and
		road_renderer.GetRoadMeshVertexCount() == 12 and
		road_renderer.GetNodeMarkerCount() == 2,
		"Closed placement did not publish a seamless ribbon without a seam marker"):
		return
	if not assert_true(await V3_SAVE_FIXTURE.save(save_manager, slot_id), "Closed placement save failed"):
		return
	if not assert_saved_closed_path(roads_path):
		return
	var roads_before_cancel := FileAccess.get_file_as_string(roads_path)

	if not assert_true(road_builder.BeginPlace(Vector2(256, 256)), "RoadBuilder did not begin the cancel scenario"):
		return
	road_builder.UpdatePlace(Vector2(400, 256))
	road_builder.CancelPlaceDrag()
	if not assert_true(await V3_SAVE_FIXTURE.save(save_manager, slot_id), "Save after cancel failed"):
		return
	if not assert_true(FileAccess.get_file_as_string(roads_path) == roads_before_cancel, "Cancel changed the saved RoadGraph"):
		return

	if not assert_true(road_builder.BeginPlace(Vector2(600, 600)), "Rejected placement scenario did not begin"):
		return
	if not assert_true(road_builder.AddPlacePoint(Vector2(700, 600)), "Rejected placement did not add its first segment"):
		return
	if not assert_true(
		not road_builder.ConfirmPlace(Vector2(600, 600)) and road_builder.HasActivePlaceSession(),
		"Backtracking placement was accepted or discarded its editable session"):
		return
	road_builder.CancelPlaceSession()
	if not assert_true(await V3_SAVE_FIXTURE.save(save_manager, slot_id), "Save after rejected placement failed"):
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
	if not assert_true(await V3_SAVE_FIXTURE.save(save_manager, slot_id), "Click placement save failed"):
		return
	if not assert_saved_input_path(roads_path):
		return
	var roads_before_right_cancel := FileAccess.get_file_as_string(roads_path)

	move_pointer(road_builder, Vector2(1000, 300))
	click_left(road_builder, Vector2(1000, 300))
	move_pointer(road_builder, Vector2(1100, 300))
	click_left(road_builder, Vector2(1100, 300))
	if not assert_true(
		road_builder.GetFixedCornerCount() == 1 and road_renderer.GetPreviewPointCount() >= 2,
		"Right-click cancel scenario did not retain its fixed segment and preview"):
		return
	road_builder.HandlePlaceInput(mouse_button_event(
		MOUSE_BUTTON_RIGHT,
		true,
		road_builder.get_canvas_transform() * Vector2(1100, 300)))
	await process_frame
	if not assert_true(
		not road_builder.HasActivePlaceSession() and road_renderer.GetPreviewPointCount() == 0,
		"Right click did not cancel the fixed placement and preview"):
		return

	tool_manager.set("CurrentTool", 1)
	if not assert_true(road_builder.BeginPlace(Vector2(1200, 300)), "Tool-switch cancel scenario did not begin"):
		return
	if not assert_true(road_builder.AddPlacePoint(Vector2(1300, 300)), "Tool-switch cancel scenario did not add a fixed segment"):
		return
	tool_manager.set("CurrentTool", 0)
	await process_frame
	if not assert_true(
		not road_builder.HasActivePlaceSession() and road_renderer.GetPreviewPointCount() == 0,
		"Switching away from Road did not clear the placement and preview"):
		return

	tool_manager.set("CurrentTool", 1)
	if not assert_true(road_builder.BeginPlace(Vector2(1200, 500)), "Pause cancel scenario did not begin"):
		return
	if not assert_true(road_builder.AddPlacePoint(Vector2(1300, 500)), "Pause cancel scenario did not add a fixed segment"):
		return
	hud._Input(action_event("pause_menu"))
	if not assert_true(
		pause_menu.visible and paused and not road_builder.HasActivePlaceSession() and
		road_renderer.GetPreviewPointCount() == 0,
		"Pausing did not clear the placement session and preview"):
		return
	pause_menu._Input(key_event(KEY_ESCAPE))
	await process_frame
	if not assert_true(not pause_menu.visible and not paused, "Pause cancel scenario did not resume"):
		return
	if not assert_true(
		road_builder.GetUndoEditCount() == 3 and road_builder.GetRedoEditCount() == 0,
		"Rejected or cancelled placements changed edit history"):
		return
	if not assert_true(await V3_SAVE_FIXTURE.save(save_manager, slot_id), "Save after right-click cancel failed"):
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
	if not assert_true(await V3_SAVE_FIXTURE.save(save_manager, slot_id), "Save after removal cancel failed"):
		return
	if not assert_true(FileAccess.get_file_as_string(roads_path) == roads_before_right_cancel, "Removal cancel changed the saved RoadGraph"):
		return

	road_builder.HandleRemoveInput(mouse_button_event(
		MOUSE_BUTTON_LEFT,
		true,
		road_builder.get_canvas_transform() * Vector2(280, 350)))
	move_remove_pointer(road_builder, Vector2(420, 350))
	if not assert_true(
		road_builder.GetRemovalSelectionCount() == 1 and road_renderer.GetRemovalPreviewEdgeCount() == 1,
		"Continuous removal did not select the crossed maximal Edge exactly once"):
		return
	var presentation_before_continuous_remove: Dictionary = road_renderer.GetPresentationState()
	var rendered_edges_before_continuous_remove: int = road_renderer.GetRenderedEdgeCount()
	var mesh_vertices_before_continuous_remove: int = road_renderer.GetRoadMeshVertexCount()
	var surface_primitives_before_continuous_remove := int(
		presentation_before_continuous_remove.get("surfacePrimitiveCount", 0))
	road_builder.HandleRemoveInput(mouse_button_event(
		MOUSE_BUTTON_LEFT,
		false,
		road_builder.get_canvas_transform() * Vector2(420, 350)))
	if not assert_true(
		not road_builder.HasActiveRemoveSession() and road_renderer.GetRemovalPreviewEdgeCount() == 0,
		"Continuous removal did not commit and clear its preview"):
		return
	var pending_continuous_remove: Dictionary = road_renderer.GetPresentationState()
	if not assert_true(
		pending_continuous_remove.get("phase", "") == "pending" and
		not bool(pending_continuous_remove.get("isReady", true)) and
		int(pending_continuous_remove.get("surfacePrimitiveCount", -1)) == 0 and
		int(pending_continuous_remove.get("retainedSurfacePrimitiveCount", -1)) ==
			surface_primitives_before_continuous_remove and
		road_renderer.GetRenderedEdgeCount() == rendered_edges_before_continuous_remove and
		road_renderer.GetRoadMeshVertexCount() == mesh_vertices_before_continuous_remove,
		"Continuous removal did not retain its previous complete presentation " +
		"(edges=%d, vertices=%d, before=%d, undo=%d)" % [
			road_renderer.GetRenderedEdgeCount(),
			road_renderer.GetRoadMeshVertexCount(),
			mesh_vertices_before_continuous_remove,
			road_builder.GetUndoEditCount(),
		]):
		return
	await process_frame
	if not assert_true(
		road_renderer.GetPresentationState().get("phase", "") == "ready" and
		road_renderer.GetRenderedEdgeCount() == 2 and
		road_renderer.GetRoadMeshVertexCount() == 12,
		"Continuous removal did not atomically publish the merged presentation on the next frame"):
		return
	if not assert_true(await V3_SAVE_FIXTURE.save(save_manager, slot_id), "Continuous removal save failed"):
		return
	if not assert_saved_counts(roads_path, 4, 2, "Continuous removal"):
		return

	road_builder.HandleRemoveInput(mouse_button_event(
		MOUSE_BUTTON_LEFT,
		true,
		road_builder.get_canvas_transform() * Vector2(-50, -50),
		true))
	move_remove_pointer(road_builder, Vector2(850, 350))
	if not assert_true(
		road_builder.GetRemovalSelectionCount() == 2 and road_renderer.GetRemovalPreviewEdgeCount() == 2,
		"Rectangle removal did not select two edges"):
		return
	road_builder.HandleRemoveInput(mouse_button_event(
		MOUSE_BUTTON_LEFT,
		false,
		road_builder.get_canvas_transform() * Vector2(850, 350),
		true))
	if not assert_true(await V3_SAVE_FIXTURE.save(save_manager, slot_id), "Rectangle removal save failed"):
		return
	if not assert_saved_counts(roads_path, 0, 0, "Rectangle removal"):
		return
	if not assert_true(
		road_builder.GetUndoEditCount() == 5 and road_builder.GetRedoEditCount() == 0,
		"Successful and rejected road edits entered the wrong history stacks"):
		return

	hud._Input(action_event("edit_undo"))
	await process_frame
	if not assert_true(
		road_renderer.GetRenderedEdgeCount() == 2 and road_builder.GetUndoEditCount() == 4 and road_builder.GetRedoEditCount() == 1,
		"Undo did not restore the rectangle removal boundary and rebuild rendering"):
		return
	if not assert_true(await V3_SAVE_FIXTURE.save(save_manager, slot_id), "First undo save failed"):
		return
	if not assert_saved_counts(roads_path, 4, 2, "First undo"):
		return

	hud._Input(action_event("edit_undo"))
	await process_frame
	if not assert_true(
		road_renderer.GetRenderedEdgeCount() == 3 and road_builder.GetUndoEditCount() == 3 and road_builder.GetRedoEditCount() == 2,
		"Second undo did not restore the continuous removal boundary"):
		return
	if not assert_true(await V3_SAVE_FIXTURE.save(save_manager, slot_id), "Second undo save failed"):
		return
	if not assert_saved_counts(roads_path, 5, 3, "Second undo"):
		return

	hud._Input(action_event("edit_redo"))
	await process_frame
	if not assert_true(
		road_renderer.GetRenderedEdgeCount() == 2 and road_builder.GetUndoEditCount() == 4 and road_builder.GetRedoEditCount() == 1,
		"First redo did not reproduce the continuous removal"):
		return
	hud._Input(action_event("edit_redo"))
	await process_frame
	if not assert_true(
		road_renderer.GetRenderedEdgeCount() == 0 and road_builder.GetUndoEditCount() == 5 and road_builder.GetRedoEditCount() == 0,
		"Second redo did not reproduce the rectangle removal"):
		return
	if not assert_true(await V3_SAVE_FIXTURE.save(save_manager, slot_id), "Redo save failed"):
		return
	if not assert_saved_counts(roads_path, 0, 0, "Redo"):
		return
	if not assert_true(await V3_SAVE_FIXTURE.delete_slot(save_manager, slot_id), "Strategy path test slot cleanup failed"):
		return

	map.queue_free()
	await process_frame
	await process_frame
	if not assert_true(save_manager.get("RegisteredSaveableCount") == 0, "Runtime cleanup retained saveables"):
		return
	if not await verify_invalid_config_falls_back_to_renderable_values(packed_map, save_manager):
		return

	print("PASS road input strategy runtime contract")
	quit(0)

func verify_invalid_config_falls_back_to_renderable_values(
	packed_map: PackedScene,
	save_manager: Node) -> bool:
	var invalid_map: Node = packed_map.instantiate()
	invalid_map.get_node("AutosaveController").set("AutosaveEnabled", false)
	var builder: Node = invalid_map.get_node("RoadSystem/RoadBuilder")
	var renderer: Node = invalid_map.get_node("RoadSystem/RoadRenderer")
	var config: Resource = builder.get("Config")
	config.set("CellSize", 0.0)
	config.set("RoadWidth", 0.0)
	root.add_child(invalid_map)
	current_scene = invalid_map
	await process_frame
	await process_frame

	if not assert_true(float(config.get("CellSize")) > 0.0, "CellSize=0 was not restored to a valid runtime value"):
		return false
	if not assert_true(float(config.get("RoadWidth")) > 0.0, "RoadWidth=0 was not restored to a visible runtime value"):
		return false
	if not assert_true(builder.BeginPlace(Vector2.ZERO), "Fallback road placement did not begin"):
		return false
	builder.UpdatePlace(Vector2(128.0, 0.0))
	if not assert_true(builder.CommitPlace(Vector2(128.0, 0.0)), "Fallback road placement did not commit"):
		return false
	await process_frame
	if not assert_true(
		renderer.GetRenderedEdgeCount() > 0 and renderer.GetRoadMeshVertexCount() > 0,
		"Fallback road committed without a visible ribbon"):
		return false

	invalid_map.queue_free()
	await process_frame
	await process_frame
	return assert_true(save_manager.get("RegisteredSaveableCount") == 0, "Invalid-config cleanup retained saveables")

func assert_saved_line_path(roads_path: String) -> bool:
	var payload: Variant = JSON.parse_string(FileAccess.get_file_as_string(roads_path))
	if not assert_true(payload is Dictionary, "RoadGraph payload is not an object"):
		return false
	var graph_data: Dictionary = payload
	if not assert_street_road_payload(graph_data, "Saved strategy path"):
		return false
	var nodes: Array = graph_data.get("nodes", [])
	var edges: Array = graph_data.get("edges", [])
	if not assert_true(nodes.size() == 2, "Saved strategy path node count is wrong"):
		return false
	if not assert_true(edges.size() == 1, "Saved strategy path edge count is wrong"):
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

func assert_saved_closed_path(roads_path: String) -> bool:
	var payload: Variant = JSON.parse_string(FileAccess.get_file_as_string(roads_path))
	if not assert_true(payload is Dictionary, "Closed RoadGraph payload is not an object"):
		return false
	var graph_data: Dictionary = payload
	if not assert_street_road_payload(graph_data, "Closed path"):
		return false
	var nodes: Array = graph_data.get("nodes", [])
	var edges: Array = graph_data.get("edges", [])
	if not assert_true(nodes.size() == 3, "Closed path node count is wrong"):
		return false
	if not assert_true(edges.size() == 2, "Closed path edge count is wrong"):
		return false
	var loops: Array = edges.filter(func(edge: Dictionary) -> bool:
		return edge.get("nodeAID", -1) == edge.get("nodeBID", -2))
	if not assert_true(loops.size() == 1, "Closed path was not stored as one self-loop"):
		return false
	var closed_geometry: Array = loops[0].get("geometry", [])
	if not assert_true(
		closed_geometry.size() == 4,
		"Closed path was not stored as one self-loop with four geometry primitives"):
		return false
	return true

func assert_saved_input_path(roads_path: String) -> bool:
	var payload: Variant = JSON.parse_string(FileAccess.get_file_as_string(roads_path))
	if not assert_true(payload is Dictionary, "Input RoadGraph payload is not an object"):
		return false
	var graph_data: Dictionary = payload
	if not assert_street_road_payload(graph_data, "Input path"):
		return false
	var nodes: Array = graph_data.get("nodes", [])
	var edges: Array = graph_data.get("edges", [])
	if not assert_true(nodes.size() == 5, "Click path node count is wrong"):
		return false
	if not assert_true(edges.size() == 3, "Click path edge count is wrong"):
		return false
	var primitive_counts: Array[int] = []
	for edge: Dictionary in edges:
		primitive_counts.append(edge.get("geometry", []).size())
	primitive_counts.sort()
	if not assert_true(
		primitive_counts == [1, 3, 4],
		"Click paths did not preserve their geometry chains inside maximal Edges"):
		return false
	return true

func assert_saved_counts(
	roads_path: String,
	expected_nodes: int,
	expected_edges: int,
	label: String) -> bool:
	var payload: Variant = JSON.parse_string(FileAccess.get_file_as_string(roads_path))
	if not assert_true(payload is Dictionary, "%s RoadGraph payload is not an object" % label):
		return false
	var graph_data: Dictionary = payload
	if not assert_street_road_payload(graph_data, label):
		return false
	if not assert_true(graph_data.get("nodes", []).size() == expected_nodes, "%s node count is wrong" % label):
		return false
	if not assert_true(graph_data.get("edges", []).size() == expected_edges, "%s edge count is wrong" % label):
		return false
	return true

func assert_street_road_payload(graph_data: Dictionary, label: String) -> bool:
	if not assert_true(
		graph_data.get("formatFamily", "") == "simple-cities-v3" and
		graph_data.get("payloadType", "") == "road-network" and
		graph_data.get("schemaVersion", -1) == 1,
		"%s V3 admission fields are wrong" % label):
		return false
	for edge: Variant in graph_data.get("edges", []):
		if not assert_true(edge is Dictionary, "%s edge is not an object" % label):
			return false
		if not assert_true(edge.get("roadType", "") == "street", "%s edge roadType is not street" % label):
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
	event.keycode = keycode as Key
	event.physical_keycode = keycode as Key
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
