extends "res://tests/godot/v4_single_span_edit_runtime_contract.gd"

func run() -> void:
	var map = load("res://Scenes/V4MapTest.tscn").instantiate()
	root.add_child(map)
	current_scene = map
	await process_frame
	check("query_map", map.CreateMap(100))
	focus(map, Vector2(500, 0), 0.65)
	await process_frame
	check("query_road", await build(map, Vector2.ZERO, Vector2(1000, 0)))
	var baseline: Dictionary = query(map, Vector2(450, 0))
	var baseline_trace: Dictionary = trace(map, Vector2(350, 0), Vector2(650, 0))
	# Surface picking uses the same binary32 geometry as the visible mesh.
	check("baseline_local_pick", baseline.get("status") == "Ready" and absf(baseline.get("hit", {}).get("parameter", -1.0) - 0.45) < 0.0000001 and baseline.get("fullEdgeVisits") == 0 and baseline.get("exactGeometryTests", 0) > 0, baseline)
	check("baseline_trace_four_spans", baseline_trace.get("status") == "Ready" and baseline_trace.get("spanCount") == 4, baseline_trace)
	for i in 20:
		var y: float = 1500 + i * 100
		focus(map, Vector2(2050, y), 1.0)
		await process_frame
		check("distant_road_" + str(i), await build(map, Vector2(2000, y), Vector2(2100, y)))
	focus(map, Vector2(500, 0), 0.65)
	await process_frame
	var grown: Dictionary = query(map, Vector2(450, 0))
	var grown_trace: Dictionary = trace(map, Vector2(350, 0), Vector2(650, 0))
	check("far_growth_keeps_local_exact_work", same_work(baseline, grown) and grown.get("hit", {}).get("sourceToken") == map.StateToken and grown.get("hit", {}).get("edgeId") == baseline.get("hit", {}).get("edgeId"), [baseline, grown])
	check("far_growth_keeps_trace_work", same_work(baseline_trace, grown_trace) and grown_trace.get("spanCount") == 4, [baseline_trace, grown_trace])
	check("invalid_point_is_explicit", query(map, Vector2(NAN, 0)).get("status") == "InvalidParameters")
	check("invalid_budget_is_explicit", map.QueryRoad(Vector2(450, 0), 0, 10, 10).get("status") == "InvalidParameters")
	var limited: Dictionary = map.TraceRoadSpans(Vector2(50, 0), Vector2(950, 0), 4096, 1, 16384)
	check("candidate_exhaustion_has_no_partial_result", limited.get("status") == "BudgetExceeded" and limited.get("spanCount") == 0 and not limited.get("reason", "").is_empty(), limited)
	limited = map.TraceRoadSpans(Vector2(50, 0), Vector2(950, 0), 1, 4096, 16384)
	check("bucket_exhaustion_is_explicit", limited.get("status") == "BudgetExceeded" and limited.get("spanCount") == 0, limited)
	check("selection_uses_current_index", map.SetToolMode(1))
	press(map, Vector2(350, 0))
	await process_frame
	motion(map, Vector2(650, 0))
	await process_frame
	check("real_drag_same_four_spans", map.GetSelectionState().get("selectedCount") == 4 and map.GetQueryState().get("status") == "Ready" and map.GetQueryState().get("fullEdgeVisits") == 0, map.GetQueryState())
	release(map, Vector2(650, 0))
	await process_frame
	escape()
	await process_frame
	check("failure_remove_mode", map.SetToolMode(2))
	var before_failure: Dictionary = map.GetRoadState()
	press(map, Vector2(350, 0))
	await process_frame
	check("valid_candidate_before_query_failure", map.GetSelectionState().get("selectedCount") == 1)
	# A finite but unindexable raw pointer coordinate exercises the real input rejection path.
	motion(map, Vector2(1.0e12, 0))
	await process_frame
	check("query_failure_discards_whole_selection", map.GetQueryState().get("status") == "InvalidParameters" and map.GetSelectionState().get("selectedCount") == 0 and not map.GetSelectionState().get("selecting", true), map.GetQueryState())
	release(map, Vector2(350, 0))
	await settle(map)
	check("release_after_query_failure_never_commits_subset", map.GetRoadState() == before_failure and not map.IsBuildBusy)
	check("long_map", map.SetToolMode(0) and map.CreateMap(25))
	focus(map, Vector2.ZERO, 0.075)
	await process_frame
	check("eight_kilometre_road", await build(map, Vector2(-4000, 0), Vector2(4000, 0)))
	var long_trace: Dictionary = trace(map, Vector2(-3987.5, 0), Vector2(3987.5, 0))
	check("long_trace_320_spans_with_local_work", long_trace.get("status") == "Ready" and long_trace.get("spanCount") == 320 and long_trace.get("fullEdgeVisits") == 0, long_trace)
	check("long_selection_mode", map.SetToolMode(1))
	press(map, Vector2(-3987.5, 0))
	await process_frame
	motion(map, Vector2(3987.5, 0))
	await process_frame
	check("single_motion_long_road_has_no_gaps", map.GetSelectionState().get("selectedCount") == 320, map.GetQueryState())
	release(map, Vector2(3987.5, 0))
	await process_frame
	print("V4_LOCAL_QUERY_RESULT ", JSON.stringify({"rows": _rows, "passed": _passed}))
	map.queue_free()
	await process_frame
	if _passed:
		print("PASS V4 local query runtime contract")
		quit(0)
	else:
		push_error("FAIL V4 local query runtime contract")
		quit(1)

func query(map: Node, point: Vector2) -> Dictionary:
	return map.QueryRoad(point, 4096, 4096, 16384)

func trace(map: Node, from: Vector2, to: Vector2) -> Dictionary:
	return map.TraceRoadSpans(from, to, 4096, 4096, 16384)

func same_work(a: Dictionary, b: Dictionary) -> bool:
	return ["bucketsVisited", "fragmentCandidates", "exactGeometryTests", "fullEdgeVisits", "hits"].all(func(key): return a.get(key) == b.get(key))
