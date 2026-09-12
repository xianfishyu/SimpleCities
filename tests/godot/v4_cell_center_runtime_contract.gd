extends SceneTree

const SAVE := preload("res://tests/godot/v3_save_fixture.gd")
const CONTROLS := "HUD/Panel/Margin/Controls/"
const CELLS := [25, 50, 100, 200]

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var map = load("res://Scenes/V4MapTest.tscn").instantiate()
	root.add_child(map)
	current_scene = map
	await process_frame
	var manager = root.get_node("SaveManager")
	var rows: Array = []
	var passed: bool = true
	var screenshot: bool = false
	for cell in CELLS:
		var c: float = float(cell)
		var center := Vector2(c / 2.0, c / 2.0)
		var created: bool = map.CreateMap(cell)
		focus(map, center, 200.0 / c)
		await process_frame
		# Crossing the two complete diagonals creates a structural node at the exact half-cell.
		var first: bool = await build(map, Vector2.ZERO, Vector2(c, c))
		var second: bool = await build(map, Vector2(c, 0), Vector2(0, c))
		var automatic: bool = created and first and second and junction_matches(map, center, 5, 4)
		var before: Dictionary = map.GetRoadState()
		var junction_before: Dictionary = junction_at(before, center)
		var junction_id: int = junction_before.get("nodeId", 0)
		var saved: bool = await SAVE.save_as(manager, "V4 cell-center %d m QA" % cell)
		var slot: String = manager.CurrentSlotID if saved else ""
		var round_trip: bool = false
		var deleted: bool = false
		if saved:
			var different: int = 25 if cell == 200 else 200
			var replacement_created: bool = map.CreateMap(different)
			var empty_token: String = map.StateToken
			var loaded: bool = await SAVE.operation_succeeded(manager, map.LoadSlot(slot))
			await process_frame
			await RenderingServer.frame_post_draw
			var after: Dictionary = map.GetRoadState()
			var junction_after: Dictionary = map.GetJunctionState(junction_id)
			round_trip = replacement_created and loaded and map.CellSizeMetres == cell and map.PresentedCellSizeMetres == cell and after.get("edges") == before.get("edges") and junction_after.get("nodeId") == junction_id and junction_after.get("position") == center and junction_after.get("incidences") == junction_before.get("incidences") and junction_after.get("turns") == junction_before.get("turns") and lineage(map.StateToken) != lineage(empty_token) and lineage(map.StateToken) != lineage(before.get("sourceToken", "")) and junction_matches(map, center, 5, 4)
			deleted = await SAVE.delete_slot(manager, slot)
			deleted = deleted and not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path("user://saves-v4/" + slot))
		# A full X has no unused diagonal. Start at a real three-arm center to test a valid new arm.
		var partial_created: bool = map.CreateMap(cell)
		var diagonal: bool = await build(map, Vector2.ZERO, Vector2(c, c))
		var third_arm: bool = await build(map, Vector2(c, 0), center)
		var partial: bool = partial_created and diagonal and third_arm and junction_matches(map, center, 4, 3)
		var partial_id: int = junction_at(map.GetRoadState(), center).get("nodeId", 0)
		var token: String = map.StateToken
		press(map, center)
		motion(map, Vector2(0, c))
		var ready: bool = await wait_preview(map)
		var preview: Dictionary = map.GetBuildPreview()
		var positive_preview: bool = ready and preview.get("phase") == "Ready" and preview.get("sourceToken") == token and preview.get("segments", []).size() > 0 and preview.get("segments", []).all(func(segment): return not segment.get("conflict", false))
		release(map, Vector2(0, c))
		var idle: bool = await wait_idle(map)
		var fourth_arm: bool = positive_preview and idle and map.StateToken != token and junction_matches(map, center, 5, 4) and junction_at(map.GetRoadState(), center).get("nodeId", 0) == partial_id
		var horizontal: bool = await rejected_drag(map, center, center + Vector2(c, 0), "对角", false)
		var vertical: bool = await rejected_drag(map, center, center + Vector2(0, c), "对角", false)
		var duplicate: bool = await rejected_drag(map, center, Vector2(0, c), "重叠", true)
		if cell == 100:
			await RenderingServer.frame_post_draw
			var capture_dir: String = ProjectSettings.globalize_path("res://.scratch/v4-09-qa")
			var directory_error: int = DirAccess.make_dir_recursive_absolute(capture_dir)
			screenshot = (directory_error == OK or directory_error == ERR_ALREADY_EXISTS) and root.get_texture().get_image().save_png(capture_dir.path_join("cell-center.png")) == OK
		var row_passed: bool = automatic and saved and round_trip and deleted and partial and fourth_arm and horizontal and vertical and duplicate
		rows.append({"cell":cell,"center":center,"automatic_cross":automatic,"saved":saved,"round_trip":round_trip,"slot_deleted":deleted,"partial_t":partial,"fourth_arm":fourth_arm,"horizontal_rejected":horizontal,"vertical_rejected":vertical,"duplicate_conflict":duplicate,"passed":row_passed})
		passed = row_passed and passed
	# A diagonal beginning at the last cell center stops at the map corner; its cap can extend outside.
	var boundary_created: bool = map.CreateMap(25)
	var boundary_start := Vector2(4000.0 - 25.0 / 2.0, 4000.0 - 25.0 / 2.0)
	focus(map, boundary_start, 0.6)
	await process_frame
	var boundary_built: bool = await build(map, boundary_start, Vector2(4500, 4500))
	var boundary_state: Dictionary = map.GetRoadState()
	var boundary: bool = boundary_created and boundary_built and boundary_state.get("start") == boundary_start and boundary_state.get("end") == Vector2(4000, 4000) and boundary_state.get("nodeCount", 0) == 2 and boundary_state.get("edges", []).size() == 1
	var cap_hit: Dictionary = map.PickRoad(Vector2(4001, 4001))
	var outside_cap: bool = not cap_hit.is_empty() and cap_hit.get("surfaceCenter") == Vector2(4000, 4000) and cap_hit.get("sourceToken") == map.StateToken
	passed = passed and boundary and outside_cap and screenshot
	print("V4_CELL_CENTER_RESULT ", JSON.stringify({"rows":rows,"boundary":boundary,"outside_cap":outside_cap,"screenshot":screenshot,"passed":passed}))
	map.queue_free()
	await process_frame
	if passed:
		print("PASS V4 cell-center runtime contract")
		quit(0)
	else:
		push_error("FAIL V4 cell-center runtime contract")
		quit(1)

func focus(map: Node, world: Vector2, zoom: float) -> void:
	var camera: Camera2D = map.get_node("Camera2D")
	camera.zoom = Vector2(zoom, zoom)
	# Keep all raw mouse gestures clear of the left-hand control panel.
	camera.position = world - (Vector2(1000, 450) - root.get_visible_rect().size / 2.0) / zoom

func junction_at(state: Dictionary, position: Vector2) -> Dictionary:
	var found: Array = state.get("junctions", []).filter(func(item): return item.get("position") == position)
	return found[0] if found.size() == 1 else {}

func junction_matches(map: Node, center: Vector2, nodes: int, branches: int) -> bool:
	var state: Dictionary = map.GetRoadState()
	var junction: Dictionary = junction_at(state, center)
	var incidences: Array = junction.get("incidences", [])
	var turns: Array = junction.get("turns", [])
	if state.get("nodeCount", 0) != nodes or state.get("edges", []).size() != branches or state.get("junctions", []).size() != 1 or incidences.size() != branches or turns.size() != branches * branches:
		return false
	var keys: Dictionary = {}
	var directions: Array = []
	var previous_bearing: float = -1.0
	for incidence in incidences:
		var edge_id: int = incidence.get("edgeId", 0)
		var role: String = incidence.get("role", "")
		var outward: Vector2 = incidence.get("outward", Vector2.ZERO)
		var bearing: float = incidence.get("bearingRadians", -1.0)
		var key: String = "%d:%s" % [edge_id, role]
		if edge_id <= 0 or role not in ["Start", "End"] or keys.has(key) or directions.has(outward) or bearing < previous_bearing or not is_equal_approx(absf(outward.x), sqrt(0.5)) or not is_equal_approx(absf(outward.y), sqrt(0.5)):
			return false
		keys[key] = true
		directions.append(outward)
		previous_bearing = bearing
	var turn_keys: Dictionary = {}
	for turn in turns:
		var from_key: String = "%d:%s" % [turn.get("fromEdgeId", 0), turn.get("fromRole", "")]
		var to_key: String = "%d:%s" % [turn.get("toEdgeId", 0), turn.get("toRole", "")]
		var pair: String = from_key + ">" + to_key
		if not keys.has(from_key) or not keys.has(to_key) or turn_keys.has(pair): return false
		turn_keys[pair] = true
	var hit: Dictionary = map.PickRoad(center)
	var owners: Array = incidences.filter(func(item): return item.get("edgeId") == hit.get("edgeId"))
	var node_id: int = junction.get("nodeId", 0)
	return map.IsPresentationCurrent and state.get("sourceToken") == map.StateToken and junction.get("sourceToken") == map.StateToken and node_id > 0 and map.GetJunctionState(node_id) == junction and hit.get("sourceToken") == map.StateToken and hit.get("junctionNodeId", 0) == node_id and hit.get("surfaceCenter") == center and owners.size() == 1

func rejected_drag(map: Node, start: Vector2, end: Vector2, reason: String, conflict: bool) -> bool:
	var before: Dictionary = map.GetRoadState()
	var token: String = map.StateToken
	press(map, start)
	motion(map, end)
	var ready: bool = await wait_preview(map)
	var preview: Dictionary = map.GetBuildPreview()
	var preview_reason: String = preview.get("reason", "")
	var segments: Array = preview.get("segments", [])
	var red: Array = segments.filter(func(segment): return segment.get("conflict", false))
	var rejected: bool = ready and preview.get("phase") == "Rejected" and preview.get("sourceToken") == token and preview_reason.contains(reason) and (not conflict or red.size() > 0) and map.StateToken == token
	release(map, end)
	var idle: bool = await wait_idle(map)
	var status: String = map.get_node(CONTROLS + "Status").text
	return rejected and idle and status.contains(reason) and map.StateToken == token and map.GetRoadState() == before and map.IsPresentationCurrent

func lineage(token: String) -> String:
	return token.get_slice("Lineage = ", 1).get_slice(",", 0).strip_edges()

func build(map: Node, start: Vector2, end: Vector2) -> bool:
	var token: String = map.StateToken
	press(map, start)
	motion(map, end)
	release(map, end)
	var idle: bool = await wait_idle(map)
	return idle and map.StateToken != token and map.IsPresentationCurrent

func press(map: Node, point: Vector2) -> void:
	button(map.get_canvas_transform() * point, true)

func release(map: Node, point: Vector2) -> void:
	button(map.get_canvas_transform() * point, false)

func button(position: Vector2, pressed: bool) -> void:
	var event := InputEventMouseButton.new()
	event.position = position
	event.button_index = MOUSE_BUTTON_LEFT
	event.pressed = pressed
	Input.parse_input_event(event)

func motion(map: Node, point: Vector2) -> void:
	var event := InputEventMouseMotion.new()
	event.position = map.get_canvas_transform() * point
	event.button_mask = MOUSE_BUTTON_MASK_LEFT
	Input.parse_input_event(event)

func wait_idle(map: Node) -> bool:
	var deadline: int = Time.get_ticks_msec() + 5000
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if not map.IsBuildBusy: return true
	return false

func wait_preview(map: Node) -> bool:
	var deadline: int = Time.get_ticks_msec() + 5000
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if map.GetBuildPreview().get("phase") != "Pending": return true
	return false
