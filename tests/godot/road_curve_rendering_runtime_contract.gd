extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const TEST_SLOT_NAME := "Road curve rendering runtime contract"
const SCREENSHOT_PATH := "res://.godot/qa-road-curve-rendering.png"

var test_map: Node
var save_manager: Node
var slot_id := ""

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var packed_map: PackedScene = load(MAP_SCENE)
	if not require(packed_map != null, "MapTest scene did not load"):
		return
	test_map = packed_map.instantiate()
	var autosave_controller: Node = test_map.get_node("AutosaveController")
	autosave_controller.set("AutosaveEnabled", false)
	root.add_child(test_map)
	current_scene = test_map
	await process_frame
	await process_frame
	autosave_controller.SetAutosaveEnabled(false)

	save_manager = root.get_node("SaveManager")
	if not require(save_manager.SaveAs(TEST_SLOT_NAME), "Curve rendering fixture slot was not created"):
		return
	slot_id = save_manager.get("CurrentSlotID")
	var road_path := "res://saves/%s/road_network.json" % slot_id
	var fixture := build_fixture()
	var road_file := FileAccess.open(road_path, FileAccess.WRITE)
	if not require(road_file != null, "Curve rendering fixture payload could not be opened"):
		return
	road_file.store_string(JSON.stringify(fixture, "\t"))
	road_file.close()

	if not require(save_manager.Load(slot_id), "Curve rendering fixture did not load"):
		return
	await process_frame
	await process_frame

	var renderer: Node = test_map.get_node("RoadSystem/RoadRenderer")
	if not require(renderer.GetRenderedEdgeCount() == 6, "Renderer did not rebuild all six native geometry edges"):
		return
	var original_points := snapshot_rendered_points(renderer)
	if not require(original_points.size() == 6, "Rendered point snapshot is incomplete"):
		return
	for edge_index in range(6):
		var edge_id := 13 + edge_index
		var points: Array = original_points[edge_id]
		var start_node: Dictionary = fixture.nodes[edge_index * 2]
		var end_node: Dictionary = fixture.nodes[edge_index * 2 + 1]
		var expected_start := Vector2(start_node.x, start_node.y)
		var expected_end := Vector2(end_node.x, end_node.y)
		if not require(points.front().distance_to(expected_start) <= 0.01, "Rendered edge %d start drifted" % edge_id):
			return
		if not require(points.back().distance_to(expected_end) <= 0.01, "Rendered edge %d end drifted" % edge_id):
			return
		if edge_index == 0:
			if not require(points.size() == 2, "Native line should render with exactly two points"):
				return
		else:
			if not require(points.size() > 2, "Native curve edge %d collapsed to its endpoint chord" % edge_id):
				return
			if not require(maximum_chord_deviation(points) > 5.0, "Native curve edge %d has no visible curvature" % edge_id):
				return

	var camera: Camera2D = test_map.get_node("Camera2D")
	camera.zoom = Vector2(0.125, 0.125)
	await process_frame
	camera.zoom = Vector2(4.0, 4.0)
	await process_frame
	if not require(rendered_points_match(renderer, original_points), "Camera zoom changed stable world-space curve samples"):
		return

	if not require(save_manager.Save(slot_id), "Curve rendering fixture could not be saved after display sampling"):
		return
	var saved_payload: Variant = JSON.parse_string(FileAccess.get_file_as_string(road_path))
	if not require(saved_payload is Dictionary, "Saved curve payload is not an object"):
		return
	if not require(geometry_parameters_match(fixture, saved_payload), "Display sampling changed native control parameters"):
		return
	if not require(save_manager.Load(slot_id), "Saved curve fixture could not be reloaded"):
		return
	await process_frame
	await process_frame
	if not require(rendered_points_match(renderer, original_points), "GraphCleared rebuild changed curve display samples"):
		return

	camera.position = Vector2.ZERO
	camera.zoom = Vector2.ONE
	await process_frame
	await process_frame
	var screenshot := root.get_texture().get_image()
	if not require(screenshot != null and screenshot.save_png(SCREENSHOT_PATH) == OK, "Curve rendering QA screenshot was not written"):
		return

	if not require(save_manager.DeleteSlot(slot_id), "Curve rendering fixture slot cleanup failed"):
		return
	slot_id = ""
	test_map.queue_free()
	await process_frame
	if not require(save_manager.get("RegisteredSaveableCount") == 0, "Curve rendering cleanup retained saveables"):
		return

	print("PASS road curve rendering runtime contract")
	quit(0)

func build_fixture() -> Dictionary:
	var curvature := 0.005
	var arc_length := 250.0
	var heading_delta := curvature * arc_length
	var clothoid_start := Vector2(50.0, 50.0)
	var clothoid_end := clothoid_start + Vector2(
		sin(heading_delta) / curvature,
		(1.0 - cos(heading_delta)) / curvature)
	var definitions := [
		{
			"start": Vector2(-400.0, -250.0),
			"end": Vector2(-100.0, -250.0),
			"geometry": {"version": 1, "kind": "line", "start": point(-400.0, -250.0), "end": point(-100.0, -250.0)},
		},
		{
			"start": Vector2(-400.0, -150.0),
			"end": Vector2(-100.0, -150.0),
			"geometry": {"version": 1, "kind": "cubicBezier", "start": point(-400.0, -150.0), "control1": point(-400.0, -250.0), "control2": point(-100.0, -50.0), "end": point(-100.0, -150.0)},
		},
		{
			"start": Vector2(50.0, -250.0),
			"end": Vector2(350.0, -250.0),
			"geometry": {"version": 1, "kind": "cubicHermite", "start": point(50.0, -250.0), "startTangent": point(300.0, 200.0), "end": point(350.0, -250.0), "endTangent": point(300.0, -200.0)},
		},
		{
			"start": Vector2(-400.0, 50.0),
			"end": Vector2(-100.0, 50.0),
			"geometry": {"version": 1, "kind": "circularArc", "center": point(-250.0, 50.0), "radius": 150.0, "startAngle": PI, "sweepAngle": PI},
		},
		{
			"start": clothoid_start,
			"end": clothoid_end,
			"geometry": {"version": 1, "kind": "clothoid", "start": point(clothoid_start.x, clothoid_start.y), "startHeading": 0.0, "startCurvature": curvature, "endCurvature": curvature, "arcLength": arc_length},
		},
		{
			"start": Vector2(-50.0, 250.0),
			"end": Vector2(350.0, 250.0),
			"geometry": {"version": 1, "kind": "rationalQuadratic", "start": point(-50.0, 250.0), "startWeight": 1.0, "control1": point(150.0, 80.0), "controlWeight": 0.6, "end": point(350.0, 250.0), "endWeight": 1.0},
		},
	]
	var nodes := []
	var edges := []
	var groups := []
	for index in range(definitions.size()):
		var definition: Dictionary = definitions[index]
		var node_a_id := index * 2 + 1
		var node_b_id := node_a_id + 1
		var edge_id := 13 + index
		var group_id := 19 + index
		var start: Vector2 = definition.start
		var end: Vector2 = definition.end
		nodes.append({"id": node_a_id, "x": start.x, "y": start.y})
		nodes.append({"id": node_b_id, "x": end.x, "y": end.y})
		edges.append({"id": edge_id, "nodeAID": node_a_id, "nodeBID": node_b_id, "groupID": group_id, "geometry": [definition.geometry]})
		groups.append({"id": group_id, "edgeIDs": [edge_id]})
	return {"schemaVersion": 1, "nextID": 25, "nodes": nodes, "edges": edges, "groups": groups}

func point(x: float, y: float) -> Dictionary:
	return {"x": x, "y": y}

func snapshot_rendered_points(renderer: Node) -> Dictionary:
	var snapshot := {}
	for edge_id in range(13, 19):
		var points := []
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

func maximum_chord_deviation(points: Array) -> float:
	var start: Vector2 = points.front()
	var end: Vector2 = points.back()
	var maximum := 0.0
	for position: Vector2 in points:
		maximum = max(maximum, sqrt(distance_squared_to_segment(position, start, end)))
	return maximum

func distance_squared_to_segment(position: Vector2, start: Vector2, end: Vector2) -> float:
	var delta := end - start
	var length_squared := delta.length_squared()
	if length_squared == 0.0:
		return position.distance_squared_to(start)
	var parameter: float = clampf((position - start).dot(delta) / length_squared, 0.0, 1.0)
	return position.distance_squared_to(start + parameter * delta)

func geometry_parameters_match(expected: Dictionary, actual: Dictionary) -> bool:
	var expected_edges: Array = expected.edges
	var actual_edges: Array = actual.get("edges", [])
	if expected_edges.size() != actual_edges.size():
		return false
	for index in range(expected_edges.size()):
		var expected_geometry: Dictionary = expected_edges[index].geometry[0]
		var actual_geometry: Dictionary = actual_edges[index].geometry[0]
		for key: String in expected_geometry:
			if not actual_geometry.has(key) or not values_match(actual_geometry[key], expected_geometry[key]):
				return false
	return true

func values_match(actual: Variant, expected: Variant) -> bool:
	if actual is Dictionary and expected is Dictionary:
		for key: String in expected:
			if not actual.has(key) or not values_match(actual[key], expected[key]):
				return false
		return true
	if (actual is float or actual is int) and (expected is float or expected is int):
		return abs(float(actual) - float(expected)) <= 0.0001
	return actual == expected

func require(condition: bool, message: String) -> bool:
	if condition:
		return true
	push_error(message)
	cleanup_after_failure()
	quit(1)
	return false

func cleanup_after_failure() -> void:
	if save_manager != null and not slot_id.is_empty():
		save_manager.DeleteSlot(slot_id)
		slot_id = ""
	if test_map != null:
		test_map.queue_free()
