extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const TEST_SLOT_NAME := "Road type mesh style runtime contract"
const SCREENSHOT_PATH := "res://.godot/qa-road-type-mesh-style.png"
const V3_SAVE_FIXTURE := preload("res://tests/godot/v3_save_fixture.gd")
const EXPECTED_STYLES := [
	{"token": "dirt", "color": Color("#8A6652"), "width": 14.0, "y": -180.0},
	{"token": "street", "color": Color("#60727C"), "width": 20.0, "y": -60.0},
	{"token": "arterial", "color": Color("#D7A928"), "width": 26.0, "y": 60.0},
	{"token": "highway", "color": Color("#C84B3A"), "width": 32.0, "y": 180.0},
]

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
	var autosave_controller: Node = test_map.get_node("AutosaveController")
	autosave_controller.set("AutosaveEnabled", false)
	root.add_child(test_map)
	current_scene = test_map
	await process_frame
	await process_frame
	autosave_controller.SetAutosaveEnabled(false)

	save_manager = root.get_node("SaveManager")
	if not require(
		await V3_SAVE_FIXTURE.save_as(save_manager, TEST_SLOT_NAME),
		"Mesh style fixture slot was not created"):
		return
	slot_id = str(save_manager.get("CurrentSlotID"))
	if not require(
		V3_SAVE_FIXTURE.publish_payload(slot_id, build_fixture()),
		"Mesh style fixture payload and manifest could not be published"):
		return
	if not require(
		await V3_SAVE_FIXTURE.load_slot(save_manager, slot_id),
		"Mesh style fixture did not load"):
		return
	await process_frame
	await RenderingServer.frame_post_draw

	var renderer: Node = test_map.get_node("RoadSystem/RoadRenderer")
	if not verify_mesh(renderer, EXPECTED_STYLES, "Aggregate Load"):
		return
	var camera: Camera2D = test_map.get_node("Camera2D")
	camera.position = Vector2.ZERO
	camera.zoom = Vector2.ONE
	await process_frame
	await RenderingServer.frame_post_draw
	var screenshot := root.get_texture().get_image()
	if not require(
		screenshot != null and screenshot.save_png(SCREENSHOT_PATH) == OK,
		"Mesh style Vulkan screenshot was not written"):
		return

	var builder: Node = test_map.get_node("RoadSystem/RoadBuilder")
	if not require(builder.BeginPlace(Vector2(0.0, 400.0)), "Normal mutation did not begin"):
		return
	builder.UpdatePlace(Vector2(100.0, 400.0))
	if not require(builder.CommitPlace(Vector2(100.0, 400.0)), "Normal mutation did not commit"):
		return
	await process_frame
	await RenderingServer.frame_post_draw
	var expected_after_mutation: Array = EXPECTED_STYLES.duplicate(true)
	expected_after_mutation.append({
		"token": "street",
		"color": Color("#60727C"),
		"width": 20.0,
		"y": 400.0,
	})
	if not verify_mesh(renderer, expected_after_mutation, "Normal mutation"):
		return

	if not require(
		await V3_SAVE_FIXTURE.delete_slot(save_manager, slot_id),
		"Mesh style fixture slot cleanup failed"):
		return
	slot_id = ""
	test_map.queue_free()
	await process_frame
	await process_frame
	if not require(
		save_manager.get("RegisteredSaveableCount") == 0,
		"Mesh style cleanup retained saveables"):
		return

	print("PASS road type mesh style runtime contract")
	quit(0)

func build_fixture() -> Dictionary:
	var nodes := []
	var edges := []
	for index in range(EXPECTED_STYLES.size()):
		var style: Dictionary = EXPECTED_STYLES[index]
		var node_a_id := index * 2
		var node_b_id := node_a_id + 1
		var edge_id := EXPECTED_STYLES.size() * 2 + index
		var y := float(style.y)
		nodes.append({"id": node_a_id, "x": -200.0, "y": y})
		nodes.append({"id": node_b_id, "x": 200.0, "y": y})
		edges.append({
			"id": edge_id,
			"nodeAID": node_a_id,
			"nodeBID": node_b_id,
			"roadType": style.token,
			"geometry": [{
				"version": 1,
				"kind": "line",
				"start": {"x": -200.0, "y": y},
				"end": {"x": 200.0, "y": y},
			}],
		})
	return {
		"formatFamily": "simple-cities-v3",
		"payloadType": "road-network",
		"schemaVersion": 1,
		"nextID": EXPECTED_STYLES.size() * 3,
		"nodes": nodes,
		"edges": edges,
	}

func verify_mesh(renderer: Node, expected_styles: Array, source: String) -> bool:
	if not require(
		renderer.GetRenderedEdgeCount() == expected_styles.size(),
		"%s published the wrong Edge count" % source):
		return false
	var road_layer: MeshInstance2D
	for child: Node in renderer.get_children():
		if child is MeshInstance2D:
			road_layer = child
			break
	if not require(road_layer != null and road_layer.mesh != null, "%s has no road mesh" % source):
		return false
	if not require(
		road_layer.modulate.is_equal_approx(Color.WHITE),
		"%s still multiplies vertex colors by a global road color" % source):
		return false
	var arrays: Array = road_layer.mesh.surface_get_arrays(0)
	var vertices: PackedVector2Array = arrays[Mesh.ARRAY_VERTEX]
	var colors: PackedColorArray = arrays[Mesh.ARRAY_COLOR]
	var expected_vertex_count := expected_styles.size() * 4
	if not require(
		vertices.size() == expected_vertex_count and colors.size() == expected_vertex_count,
		"%s did not publish one colored ribbon pair per endpoint" % source):
		return false
	for index in range(expected_styles.size()):
		var style: Dictionary = expected_styles[index]
		var vertex_offset := index * 4
		var rendered_width := vertices[vertex_offset].distance_to(vertices[vertex_offset + 1])
		if not require(
			is_equal_approx(rendered_width, float(style.width)),
			"%s published the wrong width for %s" % [source, style.token]):
			return false
		var center_y := (vertices[vertex_offset].y + vertices[vertex_offset + 1].y) * 0.5
		if not require(
			is_equal_approx(center_y, float(style.y)),
			"%s reordered the %s ribbon" % [source, style.token]):
			return false
		for color_index in range(vertex_offset, vertex_offset + 4):
			if not require(
				vertex_color_matches(colors[color_index], style.color),
				"%s published the wrong vertex color for %s: actual=%s expected=%s" % [
					source,
					style.token,
					colors[color_index],
					style.color,
				]):
				return false
	return true

func vertex_color_matches(actual: Color, expected: Color) -> bool:
	var tolerance := 1.0 / 255.0 + 0.00001
	return (
		abs(actual.r - expected.r) <= tolerance and
		abs(actual.g - expected.g) <= tolerance and
		abs(actual.b - expected.b) <= tolerance and
		abs(actual.a - expected.a) <= tolerance)

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
