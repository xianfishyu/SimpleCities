extends "res://tests/godot/v4_history_runtime_contract.gd"

var _capacity_results: Array = []
var _capacity_slot: String = ""

func run() -> void:
	var manager: Node = root.get_node("SaveManager")
	for cell in [25, 50, 100, 200]:
		if not await capacity_dataset(manager, cell, false): break
	if _passed:
		await capacity_dataset(manager, 200, true)
	if not _capacity_slot.is_empty():
		var cleaned: bool = await SAVE.delete_slot(manager, _capacity_slot)
		check("remaining_capacity_slot_deleted", cleaned)
		if cleaned: _capacity_slot = ""
	var output: String = OS.get_environment("V4_QA_OUTPUT")
	if output.is_empty(): output = "res://.scratch/v4-24-qa"
	var directory: String = ProjectSettings.globalize_path(output)
	DirAccess.make_dir_recursive_absolute(directory)
	var file := FileAccess.open(directory.path_join("godot-result.json"), FileAccess.WRITE)
	check("capacity_result_file_opened", file != null)
	var result: Dictionary = {"passed": _passed, "datasets": _capacity_results, "rows": _rows, "renderingMethod": RenderingServer.get_current_rendering_method(), "renderingDriver": RenderingServer.get_current_rendering_driver_name(), "viewport": {"width": root.get_visible_rect().size.x, "height": root.get_visible_rect().size.y}, "timingBoundary": "Individual operation examples only; not a performance acceptance sample", "remainingSlot": _capacity_slot}
	if file != null:
		file.store_string(JSON.stringify(result, "\t"))
		file.close()
	print("V4_CAPACITY_RESULT ", JSON.stringify({"passed": _passed, "datasetCount": _capacity_results.size(), "checks": _rows.size(), "remainingSlot": _capacity_slot}))
	if not _passed: push_error("V4 capacity runtime contract failed; see godot-result.json")
	quit(0 if _passed else 1)

func capacity_dataset(manager: Node, cell: int, dense: bool) -> bool:
	var name: String = "cell-%d-%s" % [cell, "dense-polylines" if dense else "target"]
	var expected: int = 320 if dense else (9680 if cell == 200 else 10000)
	var path: String = "res://.scratch/v4-24-qa/datasets/%s.road.json" % name
	var map = load("res://Scenes/V4MapTest.tscn").instantiate()
	root.add_child(map)
	current_scene = map
	await process_frame
	var sample: Dictionary = {"name": name, "cell": cell, "expectedEdges": expected, "operations": []}
	_capacity_results.append(sample)
	check(name + "_file_exists", FileAccess.file_exists(path))
	if not _passed: return false
	map.LoadPerformanceFixture(path)
	var deadline: int = Time.get_ticks_msec() + 30000
	while map.PerformanceFixtureBusy and Time.get_ticks_msec() < deadline:
		await process_frame
	check(name + "_fixture_loaded", not map.PerformanceFixtureBusy and map.PerformanceFixtureError.is_empty(), {"error": map.PerformanceFixtureError})
	if not _passed: return false
	await RenderingServer.frame_post_draw
	check(name + "_fixture_current", await capacity_current(map) and map.RoadCount == expected and map.CellSizeMetres == cell, {"edges": map.RoadCount, "cell": map.CellSizeMetres})
	if not _passed: return false
	sample.fixture = map.GetPerformanceFixture()
	# The quarter-cell point lies inside the eight-way junction envelope at 25 m.
	# The horizontal midpoint is between primary nodes, outside both junction mouths;
	# a cell-center junction belongs to the diagonals, not this horizontal span.
	var point := Vector2(-19.5 * cell, -20 * cell) if dense else Vector2(0.5 * cell, 0)
	focus(map, point, minf(2.0, 150.0 / cell))
	await process_frame
	var profile: OptionButton = map.get_node("HUD/Panel/Margin/Controls/Profile")
	profile.select(3)
	for mode in [3, 2]:
		var operation: String = "profile" if mode == 3 else "delete"
		check(name + "_" + operation + "_mode", map.SetToolMode(mode))
		await process_frame
		var token: String = map.StateToken
		press(map, point)
		await process_frame
		var selection: Dictionary = map.GetSelectionState()
		check(name + "_" + operation + "_one_span_selected", selection.get("selectedCount", 0) == 1, {"selection": selection, "query": map.GetQueryState(), "pick": map.PickRoad(point), "world": point, "screen": map.get_canvas_transform() * point})
		if not _passed: return false
		release(map, point)
		check(name + "_" + operation + "_drawn", await capacity_drawn(map, token))
		if not _passed: return false
		sample.operations.append({"operation": operation, "timings": map.GetOperationTimings(), "edgesAfter": map.RoadCount})
		if mode == 3:
			check(name + "_profile_changed", map.PickRoad(point).get("profile", "") == "highway")
		else:
			# Deleting one leg of an L can preserve the canonical-edge count.
			# Verify the selected world interval disappeared instead of inferring it from count.
			check(name + "_deleted_span_absent", map.PickRoad(point).is_empty(), {"pick": map.PickRoad(point), "edgesAfter": map.RoadCount})
		token = map.StateToken
		check(name + "_" + operation + "_undo_accepted", map.UndoRoadEdit())
		check(name + "_" + operation + "_undo_drawn", await capacity_drawn(map, token) and map.RoadCount == expected and map.PickRoad(point).get("profile", "") == "street")
		if not _passed: return false
		sample.operations.append({"operation": operation + "-undo-api", "timings": map.GetOperationTimings()})
	check(name + "_save_as", await SAVE.save_as(manager, "V4 capacity QA " + name))
	if not _passed: return false
	_capacity_slot = manager.CurrentSlotID
	sample.slot = _capacity_slot
	check(name + "_saved_slot_exists", not _capacity_slot.is_empty())
	check(name + "_empty_other_cell", map.CreateMap(50 if cell != 50 else 100) and map.RoadCount == 0)
	if not _passed: return false
	await RenderingServer.frame_post_draw
	var load_token: String = map.LoadSlot(_capacity_slot)
	var loaded: Dictionary = await SAVE.wait_for_operation(manager, load_token, 15.0)
	var idle: bool = await SAVE.wait_for_idle(manager, 15.0)
	check(name + "_normal_save_manager_load", idle and int(loaded.get("resultKind", -1)) in [0, 1], loaded)
	check(name + "_loaded_current_and_empty_history", await capacity_current(map) and map.RoadCount == expected and map.CellSizeMetres == cell and history_counts(map, 0, 0), {"edges": map.RoadCount, "cell": map.CellSizeMetres, "history": map.GetHistoryState()})
	var deleted: bool = await SAVE.delete_slot(manager, _capacity_slot)
	check(name + "_slot_deleted", deleted and not DirAccess.dir_exists_absolute(ProjectSettings.globalize_path("user://saves-v4/" + _capacity_slot)))
	if deleted: _capacity_slot = ""
	sample.passed = _passed
	map.queue_free()
	await process_frame
	return _passed

func capacity_current(map: Node) -> bool:
	var deadline: int = Time.get_ticks_msec() + 15000
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if not map.IsBuildBusy and map.IsPresentationCurrent: return true
	return false

func capacity_drawn(map: Node, previous: String) -> bool:
	var deadline: int = Time.get_ticks_msec() + 15000
	while Time.get_ticks_msec() < deadline:
		await process_frame
		if not map.IsBuildBusy and map.IsPresentationCurrent and map.StateToken != previous and map.BuildPhase == "Drawn": return true
		if not map.IsBuildBusy and map.BuildPhase in ["Failed", "Rejected", "Cancelled"]: return false
	return false
