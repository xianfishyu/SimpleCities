extends SceneTree

const SAVE := preload("res://tests/godot/v3_save_fixture.gd")
const CONTROLS := "HUD/Panel/Margin/Controls/"

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var map = load("res://Scenes/V4MapTest.tscn").instantiate()
	root.add_child(map)
	current_scene = map
	await process_frame
	var manager = root.get_node("SaveManager")
	var camera: Camera2D = map.get_node("Camera2D")
	camera.position = Vector2(-400, 0)
	camera.zoom = Vector2(0.75, 0.75)
	await process_frame
	# Two diagonals meet at a primary grid point, not at a cell center.
	var diagonal_built := await build(map, Vector2(-200, -200), Vector2(200, 200))
	diagonal_built = await build(map, Vector2(-200, 200), Vector2(200, -200)) and diagonal_built
	var diagonal: bool = diagonal_built and junction_matches(map, 5, 4,
		[Vector2(1, 1).normalized(), Vector2(-1, 1).normalized(), Vector2(-1, -1).normalized(), Vector2(1, -1).normalized()])
	var created: bool = map.CreateMap(100)
	await process_frame
	var t_built := await build(map, Vector2(-300, 0), Vector2(300, 0))
	t_built = await build(map, Vector2(0, -300), Vector2.ZERO) and t_built
	var t_shape: bool = created and t_built and junction_matches(map, 4, 3, [Vector2.RIGHT, Vector2.LEFT, Vector2.UP])
	var t_junction: Dictionary = center_junction(map.GetRoadState())
	var junction_id: int = t_junction.get("nodeId", 0)
	# Adding a fourth arm reuses the T's node and publishes exactly one new state.
	var cross_built := await build(map, Vector2.ZERO, Vector2(0, 300))
	var cross: bool = cross_built and junction_matches(map, 5, 4, [Vector2.RIGHT, Vector2.DOWN, Vector2.LEFT, Vector2.UP])
	var cross_junction: Dictionary = map.GetJunctionState(junction_id)
	var cross_turns: bool = turn_matches(cross_junction, Vector2.LEFT, Vector2.RIGHT, 0.0) and turn_matches(cross_junction, Vector2.LEFT, Vector2.DOWN, PI / 2.0) and turn_matches(cross_junction, Vector2.LEFT, Vector2.UP, -PI / 2.0) and turn_matches(cross_junction, Vector2.LEFT, Vector2.LEFT, PI)
	map.get_node(CONTROLS + "Profile").select(3)
	var branch_built := await build(map, Vector2.ZERO, Vector2(200, -200))
	var five_way: bool = branch_built and junction_matches(map, 6, 5,
		[Vector2.RIGHT, Vector2.DOWN, Vector2.LEFT, Vector2.UP, Vector2(1, -1).normalized()])
	var before: Dictionary = map.GetRoadState()
	var junction_before: Dictionary = center_junction(before)
	var reused: bool = junction_id > 0 and junction_before.get("nodeId", 0) == junction_id and map.GetJunctionState(junction_id) == junction_before
	var profiles: Array = junction_before.get("incidences", []).filter(func(item): return item.get("profile") == "highway")
	reused = reused and profiles.size() == 1
	await RenderingServer.frame_post_draw
	var screenshot: bool = root.get_texture().get_image().save_png(ProjectSettings.globalize_path("res://.scratch/v4-08-qa/primary-junction.png")) == OK
	var saved := await SAVE.save_as(manager, "V4 primary junction QA")
	var slot: String = manager.CurrentSlotID if saved else ""
	var round_trip := false
	var overlap_rejected := false
	var deleted := false
	if saved:
		var payload_path := "user://saves-v4/" + slot + "/road_network_v4.json"
		var bytes := FileAccess.get_file_as_bytes(payload_path)
		var new_map: bool = map.CreateMap(25)
		var empty_token: String = map.StateToken
		var loaded := await SAVE.operation_succeeded(manager, map.LoadSlot(slot))
		await process_frame
		await RenderingServer.frame_post_draw
		var after: Dictionary = map.GetRoadState()
		var junction_after: Dictionary = map.GetJunctionState(junction_id)
		round_trip = new_map and loaded and map.CellSizeMetres == 100 and after.get("edges") == before.get("edges") and after.get("nodeCount") == before.get("nodeCount") and after.get("junctions", []).size() == 1 and junction_after.get("incidences") == junction_before.get("incidences") and junction_after.get("turns") == junction_before.get("turns") and junction_after.get("nodeId") == junction_id and lineage(after.get("sourceToken", "")) != lineage(empty_token) and lineage(after.get("sourceToken", "")) != lineage(before.get("sourceToken", "")) and junction_surface_matches(map, junction_after)
		var token: String = map.StateToken
		# Release immediately: final validation must reject even without waiting for preview.
		gesture(map, Vector2(-400, 0), Vector2(400, 0))
		var idle := await wait_idle(map)
		var unchanged: bool = idle and map.StateToken == token and map.GetRoadState().get("edges") == after.get("edges") and map.IsPresentationCurrent and map.get_node(CONTROLS + "Status").text.contains("重叠")
		var saved_again := await SAVE.save(manager, slot)
		overlap_rejected = unchanged and saved_again and FileAccess.get_file_as_bytes(payload_path) == bytes
		deleted = await SAVE.delete_slot(manager, slot)
		deleted = deleted and not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path("user://saves-v4/" + slot))
	var passed: bool = diagonal and t_shape and cross and cross_turns and five_way and reused and screenshot and saved and round_trip and overlap_rejected and deleted
	print("V4_PRIMARY_JUNCTION_RESULT ", JSON.stringify({"diagonal_cross":diagonal,"t_shape":t_shape,"cross":cross,"cross_turns":cross_turns,"five_way":five_way,"reused_node":reused,"screenshot":screenshot,"round_trip":round_trip,"overlap_rejected":overlap_rejected,"slot_deleted":deleted,"passed":passed}))
	map.queue_free()
	await process_frame
	if passed:
		print("PASS V4 primary junction runtime contract")
		quit(0)
	else:
		push_error("FAIL V4 primary junction runtime contract")
		quit(1)

func center_junction(state: Dictionary) -> Dictionary:
	var found: Array = state.get("junctions", []).filter(func(item): return item.get("position") == Vector2.ZERO)
	return found[0] if found.size() == 1 else {}

func junction_matches(map: Node, nodes: int, edges: int, directions: Array) -> bool:
	var state: Dictionary = map.GetRoadState()
	var junction: Dictionary = center_junction(state)
	var incidences: Array = junction.get("incidences", [])
	var turns: Array = junction.get("turns", [])
	if state.get("nodeCount", 0) != nodes or state.get("edges", []).size() != edges or state.get("junctions", []).size() != 1 or incidences.size() != directions.size() or turns.size() != directions.size() * directions.size():
		return false
	var previous_bearing := -1.0
	for index in range(incidences.size()):
		var incidence: Dictionary = incidences[index]
		var outward: Vector2 = incidence.get("outward", Vector2.ZERO)
		var bearing: float = incidence.get("bearingRadians", -1.0)
		if not outward.is_equal_approx(directions[index]) or bearing < previous_bearing or not incidence.get("role", "") in ["Start", "End"]:
			return false
		previous_bearing = bearing
	return junction_surface_matches(map, junction)

func junction_surface_matches(map: Node, junction: Dictionary) -> bool:
	var hit: Dictionary = map.PickRoad(Vector2.ZERO)
	var owners: Array = junction.get("incidences", []).filter(func(item): return item.get("edgeId") == hit.get("edgeId"))
	return map.IsPresentationCurrent and junction.get("sourceToken", "") == map.StateToken and hit.get("sourceToken", "") == map.StateToken and junction.get("nodeId", 0) > 0 and hit.get("junctionNodeId", 0) == junction.get("nodeId") and hit.get("surfaceCenter") == Vector2.ZERO and owners.size() == 1

func turn_matches(junction: Dictionary, from_direction: Vector2, to_direction: Vector2, angle: float) -> bool:
	var incidences: Array = junction.get("incidences", [])
	var from_items: Array = incidences.filter(func(item): return item.get("outward") == from_direction)
	var to_items: Array = incidences.filter(func(item): return item.get("outward") == to_direction)
	if from_items.size() != 1 or to_items.size() != 1: return false
	var turns: Array = junction.get("turns", []).filter(func(item): return item.get("fromEdgeId") == from_items[0]["edgeId"] and item.get("fromRole") == from_items[0]["role"] and item.get("toEdgeId") == to_items[0]["edgeId"] and item.get("toRole") == to_items[0]["role"])
	return turns.size() == 1 and absf(turns[0].get("signedAngleRadians", INF) - angle) < 0.00001

func lineage(token: String) -> String:
	return token.get_slice("Lineage = ", 1).get_slice(",", 0).strip_edges()

func build(map: Node, start: Vector2, end: Vector2) -> bool:
	var before: String = map.StateToken
	gesture(map, start, end)
	return await wait_idle(map) and map.StateToken != before and map.IsPresentationCurrent

func gesture(map: Node, start: Vector2, end: Vector2) -> void:
	var transform: Transform2D = map.get_canvas_transform()
	button(transform * start, true)
	var motion := InputEventMouseMotion.new()
	motion.position = transform * end
	motion.button_mask = MOUSE_BUTTON_MASK_LEFT
	Input.parse_input_event(motion)
	button(transform * end, false)

func wait_idle(map: Node) -> bool:
	var deadline := Time.get_ticks_msec() + 5000
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if not map.IsBuildBusy: return true
	return false

func button(position: Vector2, pressed: bool) -> void:
	var event := InputEventMouseButton.new()
	event.position = position
	event.button_index = MOUSE_BUTTON_LEFT
	event.pressed = pressed
	Input.parse_input_event(event)
