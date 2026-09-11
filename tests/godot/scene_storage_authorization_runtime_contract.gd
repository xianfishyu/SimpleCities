extends SceneTree

const SAVE_FIXTURE := preload("res://tests/godot/v3_save_fixture.gd")

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var map: Node = load("res://Scenes/MapTest.tscn").instantiate()
	map.get_node("AutosaveController").set("AutosaveEnabled", false)
	root.add_child(map)
	current_scene = map
	await process_frame
	await process_frame
	var manager: Node = root.get_node("SaveManager")
	var roads: Node = map.get_node("RoadSystem")
	var tools: Node = map.get_node("ToolManager")
	var renderer: Node = roads.get_node("RoadRenderer")
	var probe: RefCounted = load("res://tests/godot/SceneStoragePolicyProbe.cs").new()
	probe.CreateMatchingSlots(roads)
	var bound_a: bool = probe.BindRoot(manager, roads, tools, renderer, probe.RootA)
	var authorization: String = probe.CaptureAndAuthorize(manager)
	var bound_b: bool = probe.BindRoot(manager, roads, tools, renderer, probe.RootB)
	var stale_summary: String = probe.RearmCapturedSummary(manager)
	var started: String = manager.StartDeleteSlot(probe.SlotID, authorization)
	var deletion_result: Dictionary = {}
	if not started.is_empty():
		deletion_result = await SAVE_FIXTURE.wait_for_operation(manager, started)
	var idle: bool = await SAVE_FIXTURE.wait_for_idle(manager)
	var retained: bool = probe.BothSlotsExist()
	var passed := bound_a and bound_b and not authorization.is_empty() and stale_summary.is_empty() and started.is_empty() and idle and retained
	print("SCENE_STORAGE_AUTHORIZATION_RESULT ", JSON.stringify({
		"bound_a": bound_a, "bound_b": bound_b,
		"old_summary_rejected": stale_summary.is_empty(),
		"old_authorization_rejected": started.is_empty(),
		"both_slots_retained": retained, "deletion_result": deletion_result,
		"passed": passed,
	}))
	probe.Cleanup(manager, roads, tools, renderer)
	map.queue_free()
	await process_frame
	if passed:
		print("PASS scene storage authorization runtime contract")
		quit(0)
	else:
		push_error("FAIL scene storage authorization runtime contract")
		quit(1)
