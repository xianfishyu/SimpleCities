extends SceneTree

const SAVE := preload("res://tests/godot/v3_save_fixture.gd")

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var map = load("res://Scenes/V4MapTest.tscn").instantiate()
	root.add_child(map)
	current_scene = map
	await process_frame
	var manager = root.get_node("SaveManager")
	var rows: Array = []
	var created: bool = map.CreateMap(100)
	focus(map, Vector2(300, 200), 0.6)
	await process_frame
	var built: bool = await square(map)
	var pure_state: Dictionary = map.GetRoadState()
	var pure_edges: Array = pure_state.get("edges", [])
	var root_id: int = pure_edges[0].get("startNodeId", 0) if pure_edges.size() == 1 else 0
	var pure_read: Dictionary = map.GetJunctionState(root_id)
	var pure: bool = created and built and pure_state.get("nodeCount", 0) == 1 and pure_edges.size() == 1 and closed_edge(pure_edges[0]) and pure_read.get("position") == Vector2.ZERO and roles_match(map, pure_read, 2) and self_loop_roles(pure_read, pure_edges[0].get("edgeId", 0))
	var pure_saved: bool = await round_trip(map, manager, "pure")
	rows.append({"shape":"pure","topology":pure,"round_trip_and_cleanup":pure_saved})
	# Attaching away from the old seam must move both loop ends to the real junction.
	focus(map, Vector2(300, 200), 0.6)
	await process_frame
	var branch_built: bool = await build(map, Vector2(400, 400), Vector2(600, 400))
	var branch_state: Dictionary = map.GetRoadState()
	var branch_loops: Array = branch_state.get("edges", []).filter(func(edge): return edge.get("startNodeId", 0) == edge.get("endNodeId", -1))
	var branch_id: int = branch_loops[0].get("startNodeId", 0) if branch_loops.size() == 1 else 0
	var branch_read: Dictionary = map.GetJunctionState(branch_id)
	var branch: bool = branch_built and branch_state.get("nodeCount", 0) == 2 and branch_state.get("edges", []).size() == 2 and branch_id != root_id and map.GetJunctionState(root_id).is_empty() and branch_read.get("position") == Vector2(400, 400) and roles_match(map, branch_read, 3) and self_loop_roles(branch_read, branch_loops[0].get("edgeId", 0)) and junction_surface_matches(map, branch_read)
	var branch_saved: bool = await round_trip(map, manager, "branch")
	rows.append({"shape":"branch","topology":branch,"round_trip_and_cleanup":branch_saved})
	# The two routes around the rectangle plus a diagonal are three distinct edges.
	created = map.CreateMap(100)
	focus(map, Vector2(200, 200), 0.8)
	await process_frame
	built = await square(map)
	var diagonal_built: bool = await build(map, Vector2.ZERO, Vector2(400, 400))
	var parallel_state: Dictionary = map.GetRoadState()
	var parallel_edges: Array = parallel_state.get("edges", [])
	var parallel: bool = created and built and diagonal_built and parallel_state.get("nodeCount", 0) == 2 and parallel_edges.size() == 3
	var edge_ids: Dictionary = {}
	var endpoints: Dictionary = {}
	for edge in parallel_edges:
		edge_ids[edge.get("edgeId", 0)] = true
		endpoints[edge.get("startNodeId", 0)] = true
		endpoints[edge.get("endNodeId", 0)] = true
	parallel = parallel and edge_ids.size() == 3 and endpoints.size() == 2 and not edge_ids.has(0) and not endpoints.has(0)
	for node_id in endpoints:
		parallel = roles_match(map, map.GetJunctionState(node_id), 3) and parallel
	var picked_ids: Dictionary = {}
	for point in [Vector2(0, 200), Vector2(200, 0), Vector2(200, 200)]:
		var hit: Dictionary = map.PickRoad(point)
		picked_ids[hit.get("edgeId", 0)] = true
		parallel = parallel and hit.get("sourceToken") == map.StateToken and hit.get("surfaceCenter") == point
	parallel = parallel and picked_ids.size() == 3 and not picked_ids.has(0)
	await RenderingServer.frame_post_draw
	var capture_path: String = OS.get_environment("V4_QA_OUTPUT")
	var capture_dir: String = ProjectSettings.globalize_path(capture_path if not capture_path.is_empty() else "res://.scratch/v4-10-11-qa")
	var directory_error: int = DirAccess.make_dir_recursive_absolute(capture_dir)
	var screenshot: bool = (directory_error == OK or directory_error == ERR_ALREADY_EXISTS) and root.get_texture().get_image().save_png(capture_dir.path_join("loop.png")) == OK
	var parallel_saved: bool = await round_trip(map, manager, "parallel")
	rows.append({"shape":"parallel","topology_and_picking":parallel,"round_trip_and_cleanup":parallel_saved})
	# Closing onto the middle of a diagonal produces a cell-center-rooted loop with a branch.
	created = map.CreateMap(100)
	focus(map, Vector2(50, 50), 2.0)
	await process_frame
	built = await build(map, Vector2.ZERO, Vector2(100, 100))
	built = await build(map, Vector2.ZERO, Vector2(100, 0)) and built
	built = await build(map, Vector2(100, 0), Vector2(50, 50)) and built
	var center_state: Dictionary = map.GetRoadState()
	var center_loops: Array = center_state.get("edges", []).filter(func(edge): return edge.get("startNodeId", 0) == edge.get("endNodeId", -1))
	var center_id: int = center_loops[0].get("startNodeId", 0) if center_loops.size() == 1 else 0
	var center_read: Dictionary = map.GetJunctionState(center_id)
	var center: bool = created and built and center_state.get("nodeCount", 0) == 2 and center_state.get("edges", []).size() == 2 and center_read.get("position") == Vector2(50, 50) and roles_match(map, center_read, 3) and self_loop_roles(center_read, center_loops[0].get("edgeId", 0)) and junction_surface_matches(map, center_read)
	for incidence in center_read.get("incidences", []):
		var outward: Vector2 = incidence.get("outward", Vector2.ZERO)
		center = center and is_equal_approx(absf(outward.x), sqrt(0.5)) and is_equal_approx(absf(outward.y), sqrt(0.5))
	var center_saved: bool = await round_trip(map, manager, "cell-center")
	rows.append({"shape":"cell-center","topology":center,"round_trip_and_cleanup":center_saved})
	var passed: bool = pure and pure_saved and branch and branch_saved and parallel and parallel_saved and center and center_saved and screenshot
	print("V4_LOOP_RESULT ", JSON.stringify({"rows":rows,"screenshot":screenshot,"passed":passed}))
	map.queue_free()
	await process_frame
	if passed:
		print("PASS V4 loop runtime contract")
		quit(0)
	else:
		push_error("FAIL V4 loop runtime contract")
		quit(1)

func closed_edge(edge: Dictionary) -> bool:
	var points: Array = edge.get("points", [])
	return edge.get("startNodeId", 0) > 0 and edge.get("startNodeId") == edge.get("endNodeId") and points.size() >= 4 and points[0] == points[-1]

func self_loop_roles(read: Dictionary, edge_id: int) -> bool:
	var incidences: Array = read.get("incidences", []).filter(func(item): return item.get("edgeId", 0) == edge_id)
	return edge_id > 0 and incidences.size() == 2 and incidences[0].get("role") != incidences[1].get("role") and incidences[0].get("outward") != incidences[1].get("outward")

func roles_match(map: Node, read: Dictionary, count: int) -> bool:
	var incidences: Array = read.get("incidences", [])
	var turns: Array = read.get("turns", [])
	if read.get("nodeId", 0) <= 0 or read.get("sourceToken", "") != map.StateToken or not map.IsPresentationCurrent or incidences.size() != count or turns.size() != count * count:
		return false
	var keys: Dictionary = {}
	var directions: Array = []
	for incidence in incidences:
		var role: String = incidence.get("role", "")
		var key: String = "%d:%s" % [incidence.get("edgeId", 0), role]
		var outward: Vector2 = incidence.get("outward", Vector2.ZERO)
		if keys.has(key) or role not in ["Start", "End"] or directions.has(outward) or not is_equal_approx(outward.length(), 1.0): return false
		keys[key] = true
		directions.append(outward)
	var pairs: Dictionary = {}
	for turn in turns:
		var from_key: String = "%d:%s" % [turn.get("fromEdgeId", 0), turn.get("fromRole", "")]
		var to_key: String = "%d:%s" % [turn.get("toEdgeId", 0), turn.get("toRole", "")]
		var pair: String = from_key + ">" + to_key
		if not keys.has(from_key) or not keys.has(to_key) or pairs.has(pair): return false
		pairs[pair] = true
	return true

func junction_surface_matches(map: Node, read: Dictionary) -> bool:
	var hit: Dictionary = map.PickRoad(read.get("position", Vector2.INF))
	var parameter: float = hit.get("parameter", -1.0)
	var owners: Array = read.get("incidences", []).filter(func(item): return item.get("edgeId") == hit.get("edgeId") and ((parameter == 0.0 and item.get("role") == "Start") or (parameter == 1.0 and item.get("role") == "End")))
	return hit.get("sourceToken") == map.StateToken and hit.get("junctionNodeId", 0) == read.get("nodeId") and hit.get("surfaceCenter") == read.get("position") and owners.size() == 1

func round_trip(map: Node, manager: Node, shape: String) -> bool:
	var before: Dictionary = map.GetRoadState()
	var reads: Dictionary = {}
	for edge in before.get("edges", []):
		for node_id in [edge.get("startNodeId", 0), edge.get("endNodeId", 0)]:
			reads[node_id] = map.GetJunctionState(node_id)
	var saved: bool = await SAVE.save_as(manager, "V4 loop %s QA" % shape)
	if not saved: return false
	var slot: String = manager.CurrentSlotID
	var payload_path: String = "user://saves-v4/" + slot + "/road_network_v4.json"
	var bytes: PackedByteArray = FileAccess.get_file_as_bytes(payload_path)
	var created: bool = map.CreateMap(25)
	var empty_token: String = map.StateToken
	var loaded: bool = await SAVE.operation_succeeded(manager, map.LoadSlot(slot))
	await process_frame
	await RenderingServer.frame_post_draw
	var after: Dictionary = map.GetRoadState()
	var matched: bool = created and loaded and map.CellSizeMetres == 100 and map.IsPresentationCurrent and after.get("edges") == before.get("edges") and after.get("nodeCount") == before.get("nodeCount") and lineage(map.StateToken) != lineage(empty_token) and lineage(map.StateToken) != lineage(before.get("sourceToken", ""))
	for node_id in reads:
		var previous: Dictionary = reads[node_id]
		var current: Dictionary = map.GetJunctionState(node_id)
		matched = matched and current.get("position") == previous.get("position") and current.get("incidences") == previous.get("incidences") and current.get("turns") == previous.get("turns") and current.get("sourceToken") == map.StateToken
	var saved_again: bool = await SAVE.save(manager, slot)
	matched = matched and saved_again and FileAccess.get_file_as_bytes(payload_path) == bytes
	var deleted: bool = await SAVE.delete_slot(manager, slot)
	deleted = deleted and not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path("user://saves-v4/" + slot))
	return matched and deleted

func focus(map: Node, world: Vector2, zoom: float) -> void:
	var camera: Camera2D = map.get_node("Camera2D")
	camera.zoom = Vector2(zoom, zoom)
	camera.position = world - (Vector2(1000, 450) - root.get_visible_rect().size / 2.0) / zoom

func lineage(token: String) -> String:
	return token.get_slice("Lineage = ", 1).get_slice(",", 0).strip_edges()

func square(map: Node) -> bool:
	var passed: bool = await build(map, Vector2.ZERO, Vector2(400, 0))
	passed = await build(map, Vector2(400, 0), Vector2(400, 400)) and passed
	passed = await build(map, Vector2(400, 400), Vector2(0, 400)) and passed
	passed = await build(map, Vector2(0, 400), Vector2.ZERO) and passed
	return passed

func build(map: Node, start: Vector2, end: Vector2) -> bool:
	var before: String = map.StateToken
	var transform: Transform2D = map.get_canvas_transform()
	button(transform * start, true)
	var motion := InputEventMouseMotion.new()
	motion.position = transform * end
	motion.button_mask = MOUSE_BUTTON_MASK_LEFT
	Input.parse_input_event(motion)
	button(transform * end, false)
	return await wait_idle(map) and map.StateToken != before and map.IsPresentationCurrent

func wait_idle(map: Node) -> bool:
	var deadline: int = Time.get_ticks_msec() + 5000
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
