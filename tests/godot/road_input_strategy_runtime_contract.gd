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
	var roads_before_cancel := FileAccess.get_file_as_string(roads_path)

	if not assert_true(road_builder.BeginPlace(Vector2(256, 256)), "RoadBuilder did not begin the cancel scenario"):
		return
	road_builder.UpdatePlace(Vector2(400, 256))
	road_builder.CancelPlaceDrag()
	if not assert_true(save_manager.Save(slot_id), "Save after cancel failed"):
		return
	if not assert_true(FileAccess.get_file_as_string(roads_path) == roads_before_cancel, "Cancel changed the saved RoadGraph"):
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

func assert_true(condition: bool, message: String) -> bool:
	if condition:
		return true
	push_error(message)
	quit(1)
	return false
