extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const TEST_SLOT_NAME := "Road parallel edge runtime contract"
const SCREENSHOT_PATH := "res://.godot/qa-road-parallel-edge-visual.png"
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
	var camera: Camera2D = test_map.get_node("Camera2D")
	camera.process_mode = Node.PROCESS_MODE_DISABLED
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

	if not await verify_parallel_edge_rectangle_tools(
		renderer,
		builder,
		first_edge_id,
		second_edge_id,
		first_position,
		second_position,
		slot_id):
		return

	if not await verify_parallel_edge_visual_matrix(
		renderer,
		camera,
		first_edge_id,
		second_edge_id,
		first_position,
		second_position,
		slot_id):
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

func verify_parallel_edge_rectangle_tools(
	renderer: Node,
	builder: Node,
	first_edge_id: int,
	second_edge_id: int,
	first_position: Vector2,
	second_position: Vector2,
	fixture_slot_id: String) -> bool:
	var upper_start := Vector2(35.0, -28.0)
	var upper_end := Vector2(65.0, -5.0)
	if not require(
		builder.BeginRemove(upper_start, true),
		"Parallel Edge rectangle removal did not begin"):
		return false
	builder.UpdateRemove(upper_end)
	if not require(
		builder.GetRemovalSelectionCount() == 1 and
		renderer.GetRemovalPreviewEdgeCount() == 1,
		"Parallel Edge rectangle removal did not isolate one Edge"):
		return false
	if not require(
		builder.ConfirmRemove(upper_end),
		"Parallel Edge rectangle removal did not commit"):
		return false
	if not await wait_for_presentation(renderer, "Parallel Edge rectangle removal"):
		return false
	var removed_upper: Dictionary = renderer.FindRoadSurfaceHit(first_position, 0.0)
	var retained_lower: Dictionary = renderer.FindRoadSurfaceHit(second_position, 0.0)
	if not require(
		renderer.GetRenderedEdgeCount() == 1 and
		removed_upper.is_empty() and
		int(retained_lower.get("edgeID", -1)) == second_edge_id,
		"Parallel Edge rectangle removal changed the wrong owner"):
		return false

	if not require(builder.UndoLastEdit(), "Parallel Edge rectangle removal undo did not start"):
		return false
	if not await wait_for_presentation(renderer, "Parallel Edge rectangle removal undo"):
		return false
	if not require(
		renderer.GetRenderedEdgeCount() == 2 and
		int(renderer.FindRoadSurfaceHit(first_position, 0.0).get("edgeID", -1)) == first_edge_id and
		int(renderer.FindRoadSurfaceHit(second_position, 0.0).get("edgeID", -1)) == second_edge_id,
		"Parallel Edge rectangle removal undo did not restore both owners"):
		return false

	if not require(builder.RedoLastEdit(), "Parallel Edge rectangle removal redo did not start"):
		return false
	if not await wait_for_presentation(renderer, "Parallel Edge rectangle removal redo"):
		return false
	if not require(
		renderer.GetRenderedEdgeCount() == 1 and
		renderer.FindRoadSurfaceHit(first_position, 0.0).is_empty() and
		int(renderer.FindRoadSurfaceHit(second_position, 0.0).get("edgeID", -1)) == second_edge_id,
		"Parallel Edge rectangle removal redo changed the wrong owner"):
		return false

	if not require(
		await V3_SAVE_FIXTURE.load_slot(save_manager, fixture_slot_id),
		"Parallel Edge rectangle tool fixture did not restore after removal"):
		return false
	if not await wait_for_presentation(renderer, "Parallel Edge rectangle tool restore"):
		return false
	if not require(renderer.GetRenderedEdgeCount() == 2, "Parallel Edge rectangle tool restore lost an Edge"):
		return false
	var lower_start := Vector2(35.0, 5.0)
	var lower_end := Vector2(65.0, 28.0)
	if not require(builder.SetSelectedRoadType(2), "Parallel Edge rectangle upgrade could not select Arterial"):
		return false
	if not require(
		builder.BeginUpgrade(lower_start, true),
		"Parallel Edge rectangle upgrade did not begin"):
		return false
	builder.UpdateUpgrade(lower_end)
	if not require(
		builder.GetUpgradeSelectionCount() == 1 and
		renderer.GetUpgradePreviewEdgeCount() == 1,
		"Parallel Edge rectangle upgrade did not isolate one Edge"):
		return false
	if not require(
		builder.ConfirmUpgrade(lower_end),
		"Parallel Edge rectangle upgrade did not commit"):
		return false
	if not await wait_for_presentation(renderer, "Parallel Edge rectangle upgrade"):
		return false
	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, fixture_slot_id),
		"Parallel Edge rectangle upgrade save failed"):
		return false
	if not require(
		read_edge_road_type(fixture_slot_id, first_edge_id) == "street" and
		read_edge_road_type(fixture_slot_id, second_edge_id) == "arterial",
		"Parallel Edge rectangle upgrade changed the wrong RoadType owner"):
		return false

	if not require(builder.UndoLastEdit(), "Parallel Edge rectangle upgrade undo did not start"):
		return false
	if not await wait_for_presentation(renderer, "Parallel Edge rectangle upgrade undo"):
		return false
	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, fixture_slot_id),
		"Parallel Edge rectangle upgrade undo save failed"):
		return false
	if not require(
		read_edge_road_type(fixture_slot_id, first_edge_id) == "street" and
		read_edge_road_type(fixture_slot_id, second_edge_id) == "highway",
		"Parallel Edge rectangle upgrade undo did not restore the lower owner"):
		return false

	if not require(builder.RedoLastEdit(), "Parallel Edge rectangle upgrade redo did not start"):
		return false
	if not await wait_for_presentation(renderer, "Parallel Edge rectangle upgrade redo"):
		return false
	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, fixture_slot_id),
		"Parallel Edge rectangle upgrade redo save failed"):
		return false
	if not require(
		read_edge_road_type(fixture_slot_id, first_edge_id) == "street" and
		read_edge_road_type(fixture_slot_id, second_edge_id) == "arterial",
		"Parallel Edge rectangle upgrade redo changed the wrong RoadType owner"):
		return false

	return await restore_parallel_edge_fixture(
		renderer,
		fixture_slot_id,
		"Parallel Edge rectangle tool final restore")

func restore_parallel_edge_fixture(renderer: Node, fixture_slot_id: String, source: String) -> bool:
	if not require(
		await V3_SAVE_FIXTURE.load_slot(save_manager, fixture_slot_id),
		"%s did not load" % source):
		return false
	if not await wait_for_presentation(renderer, source):
		return false
	return require(renderer.GetRenderedEdgeCount() == 2, "%s lost an Edge" % source)

func read_edge_road_type(fixture_slot_id: String, edge_id: int) -> String:
	var payload: Variant = JSON.parse_string(
		FileAccess.get_file_as_string(V3_SAVE_FIXTURE.slot_path(fixture_slot_id, "road_network.json")))
	if not payload is Dictionary:
		return ""
	for edge: Dictionary in payload.get("edges", []):
		if int(edge.get("id", -1)) == edge_id:
			return str(edge.get("roadType", ""))
	return ""

func verify_parallel_edge_visual_matrix(
	renderer: Node,
	camera: Camera2D,
	first_edge_id: int,
	second_edge_id: int,
	first_position: Vector2,
	second_position: Vector2,
	fixture_slot_id: String) -> bool:
	camera.position = Vector2(50.0, 0.0)
	camera.zoom = Vector2.ONE
	await RenderingServer.frame_post_draw
	var original_points := snapshot_edge_points(renderer, [first_edge_id, second_edge_id])
	var original_vertices: int = renderer.GetRoadMeshVertexCount()
	var original_first_hit: Dictionary = renderer.FindRoadSurfaceHit(first_position, 0.0)
	var original_second_hit: Dictionary = renderer.FindRoadSurfaceHit(second_position, 0.0)
	if not require(
		original_vertices > 0 and
		int(original_first_hit.get("edgeID", -1)) == first_edge_id and
		int(original_second_hit.get("edgeID", -1)) == second_edge_id,
		"Parallel Edge visual matrix did not start from two stable owners"):
		return false

	for zoom_value in [0.25, 4.0, 1.0]:
		camera.zoom = Vector2(zoom_value, zoom_value)
		await RenderingServer.frame_post_draw
		if not require(
			rendered_points_match(renderer, original_points) and
			renderer.GetRoadMeshVertexCount() == original_vertices,
			"Camera zoom %s changed the parallel Edge world geometry or mesh size" % zoom_value):
			return false
		var zoom_first_hit: Dictionary = renderer.FindRoadSurfaceHit(first_position, 0.0)
		var zoom_second_hit: Dictionary = renderer.FindRoadSurfaceHit(second_position, 0.0)
		if not require(
			int(zoom_first_hit.get("edgeID", -1)) == first_edge_id and
			int(zoom_second_hit.get("edgeID", -1)) == second_edge_id,
			"Camera zoom %s changed parallel Edge surface ownership" % zoom_value):
			return false

	var base_image := await capture_visual_frame(renderer, null)
	var first_highlight_image := await capture_visual_frame(renderer, first_edge_id)
	var second_highlight_image := await capture_visual_frame(renderer, second_edge_id)
	base_image.save_png("res://.godot/qa-road-parallel-edge-base.png")
	first_highlight_image.save_png("res://.godot/qa-road-parallel-edge-first.png")
	second_highlight_image.save_png("res://.godot/qa-road-parallel-edge-second.png")
	var first_activation := image_region_difference(
		base_image,
		first_highlight_image,
		first_position,
		camera,
		8.0)
	var first_cross_activation := image_region_difference(
		base_image,
		first_highlight_image,
		second_position,
		camera,
		8.0)
	var second_activation := image_region_difference(
		base_image,
		second_highlight_image,
		second_position,
		camera,
		8.0)
	var second_cross_activation := image_region_difference(
		base_image,
		second_highlight_image,
		first_position,
		camera,
		8.0)
	print("HIGHLIGHT_METRICS first=%f firstCross=%f second=%f secondCross=%f" % [
		first_activation,
		first_cross_activation,
		second_activation,
		second_cross_activation])
	if not require(
		first_activation > 10.0 and
		second_activation > 10.0 and
		first_cross_activation < first_activation * 0.35 and
		second_cross_activation < second_activation * 0.35,
		"Parallel Edge highlight was not isolated to the selected owner"):
		return false

	var screenshot: Image = second_highlight_image
	if not require(
		screenshot != null and screenshot.save_png(SCREENSHOT_PATH) == OK,
		"Parallel Edge visual matrix screenshot was not written"):
		return false

	if not require(
		await V3_SAVE_FIXTURE.load_slot(save_manager, fixture_slot_id),
		"Parallel Edge visual rebuild fixture did not reload"):
		return false
	if not await wait_for_presentation(renderer, "Parallel Edge visual rebuild"):
		return false
	if not require(
		rendered_points_match(renderer, original_points) and
		renderer.GetRoadMeshVertexCount() == original_vertices,
		"Parallel Edge rebuild changed world geometry or mesh size"):
		return false
	var rebuilt_first_hit: Dictionary = renderer.FindRoadSurfaceHit(first_position, 0.0)
	var rebuilt_second_hit: Dictionary = renderer.FindRoadSurfaceHit(second_position, 0.0)
	return require(
		int(rebuilt_first_hit.get("edgeID", -1)) == first_edge_id and
		int(rebuilt_second_hit.get("edgeID", -1)) == second_edge_id,
		"Parallel Edge rebuild changed surface ownership")

func capture_visual_frame(renderer: Node, hovered_edge_id: Variant) -> Image:
	if hovered_edge_id == null:
		renderer.ClearHoveredEdgeID()
	else:
		renderer.SetHoveredEdgeID(int(hovered_edge_id))
	await process_frame
	await RenderingServer.frame_post_draw
	return root.get_texture().get_image()

func snapshot_edge_points(renderer: Node, edge_ids: Array) -> Dictionary:
	var snapshot := {}
	for edge_id: int in edge_ids:
		var points: Array[Vector2] = []
		for point_index in range(renderer.GetRenderedPointCount(edge_id)):
			points.append(renderer.GetRenderedPoint(edge_id, point_index))
		snapshot[edge_id] = points
	return snapshot

func rendered_points_match(renderer: Node, expected: Dictionary) -> bool:
	for edge_id: int in expected:
		var points: Array = expected[edge_id]
		if renderer.GetRenderedPointCount(edge_id) != points.size():
			return false
		for point_index in range(points.size()):
			if renderer.GetRenderedPoint(edge_id, point_index).distance_to(points[point_index]) > 0.001:
				return false
	return true

func image_region_difference(
	before: Image,
	after: Image,
	world_position: Vector2,
	camera: Camera2D,
	radius_world: float) -> float:
	var center := Vector2(
		float(before.get_width()) * 0.5 + (world_position.x - camera.position.x) * camera.zoom.x,
		float(before.get_height()) * 0.5 + (world_position.y - camera.position.y) * camera.zoom.y)
	var radius: float = max(4.0, radius_world * max(abs(camera.zoom.x), abs(camera.zoom.y)))
	var left := maxi(0, floori(center.x - radius))
	var right := mini(before.get_width() - 1, ceili(center.x + radius))
	var top := maxi(0, floori(center.y - radius))
	var bottom := mini(before.get_height() - 1, ceili(center.y + radius))
	var difference := 0.0
	for y in range(top, bottom + 1, 2):
		for x in range(left, right + 1, 2):
			var before_color := before.get_pixel(x, y)
			var after_color := after.get_pixel(x, y)
			difference += abs(before_color.r - after_color.r)
			difference += abs(before_color.g - after_color.g)
			difference += abs(before_color.b - after_color.b)
	return difference

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
