extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const PROBE_PATH := "res://tests/godot/RoadLoadPreflightResourceFailureProbe.cs"
const SOURCE_SLOT_NAME := "Road preflight resource source"
const ACTIVE_SLOT_NAME := "Road preflight resource active"
const V3_SAVE_FIXTURE := preload("res://tests/godot/v3_save_fixture.gd")
const RESULT_SUCCEEDED := 0
const TOOL_ROAD := 1

var failed := false
var test_map: Node
var save_manager: Node
var probe: RefCounted
var source_slot_id := ""
var active_slot_id := ""

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

	save_manager = root.get_node("SaveManager")
	var builder: Node = test_map.get_node("RoadSystem/RoadBuilder")
	var renderer: Node = test_map.get_node("RoadSystem/RoadRenderer")
	var tool_manager: Node = test_map.get_node("ToolManager")

	if not require(
		await V3_SAVE_FIXTURE.save_as(save_manager, SOURCE_SLOT_NAME),
		"Could not create the empty source slot"):
		return
	source_slot_id = str(save_manager.get("CurrentSlotID"))

	if not require(builder.BeginPlace(Vector2(200.0, 300.0)), "Active road did not begin"):
		return
	builder.UpdatePlace(Vector2(600.0, 300.0))
	if not require(builder.CommitPlace(Vector2(600.0, 300.0)), "Active road did not commit"):
		return
	await process_frame
	await process_frame
	if not require(
		renderer.GetRenderedEdgeCount() == 1 and
		renderer.GetRoadMeshVertexCount() > 0 and
		renderer.GetNodeMarkerCount() > 0,
		"Active road did not publish all retained presentation resources"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save_as(save_manager, ACTIVE_SLOT_NAME),
		"Could not create the active slot"):
		return
	active_slot_id = str(save_manager.get("CurrentSlotID"))
	var active_payload_before := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(not active_payload_before.is_empty(), "Active payload was not readable"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD)
	if not require(builder.BeginPlace(Vector2(700.0, 400.0)), "Transient placement did not begin"):
		return
	if not require(
		builder.AddPlacePoint(Vector2(800.0, 400.0)),
		"Transient placement point was not added"):
		return

	var presentation_before: Dictionary = renderer.GetPresentationState().duplicate(true)
	var edge_count_before: int = renderer.GetRenderedEdgeCount()
	var vertex_count_before: int = renderer.GetRoadMeshVertexCount()
	var marker_count_before: int = renderer.GetNodeMarkerCount()
	var hit_before: Dictionary = renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0)
	var undo_count_before: int = builder.GetUndoEditCount()
	var redo_count_before: int = builder.GetRedoEditCount()

	var probe_script: Script = load(PROBE_PATH)
	if not require(probe_script != null, "Debug resource failure probe did not load"):
		return
	probe = probe_script.new()
	if not require(probe != null, "Debug resource failure probe did not instantiate"):
		return
	var probe_result: Dictionary = probe.Run(renderer)

	if not require(
		bool(probe_result.get("failedAtSnapshot", false)) and
		str(probe_result.get("exceptionType", "")) == "ArgumentNullException" and
		int(probe_result.get("roadVertexCount", 0)) > 0 and
		int(probe_result.get("nodeMarkerCount", 0)) > 0,
		"RoadSurfaceSnapshot failure was not injected after both resource factories: %s" %
		JSON.stringify(probe_result)):
		return
	if not require(
		int(probe_result.get("resourceCountAfter", -1)) ==
		int(probe_result.get("resourceCountBefore", -2)) and
		bool(probe_result.get("roadMeshPreserved", false)) and
		bool(probe_result.get("nodeBatchPreserved", false)) and
		bool(probe_result.get("surfacePreserved", false)),
		"Failed preflight leaked resources or replaced retained presentation references: %s" %
		JSON.stringify(probe_result)):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		builder.HasActivePlaceSession() and
		builder.GetFixedCornerCount() == 1 and
		builder.GetUndoEditCount() == undo_count_before and
		builder.GetRedoEditCount() == redo_count_before and
		renderer.GetRenderedEdgeCount() == edge_count_before and
		renderer.GetRoadMeshVertexCount() == vertex_count_before and
		renderer.GetNodeMarkerCount() == marker_count_before and
		renderer.GetPresentationState() == presentation_before and
		renderer.FindRoadSurfaceHit(Vector2(400.0, 300.0), 0.0) == hit_before,
		"Failed preflight changed graph, tool, presentation, surface, token, or slot state"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save(save_manager, active_slot_id),
		"Could not recapture the active graph after failed preflight"):
		return
	var active_payload_after := FileAccess.get_file_as_string(
		V3_SAVE_FIXTURE.slot_path(active_slot_id, V3_SAVE_FIXTURE.PAYLOAD_FILE_NAME))
	if not require(active_payload_after == active_payload_before, "Failed preflight changed RoadGraph"):
		return

	var load_result := await run_load(source_slot_id)
	if not require(
		int(load_result.get("resultKind", -1)) == RESULT_SUCCEEDED and
		bool(load_result.get("committed", false)) and
		str(load_result.get("warnings", "")).is_empty(),
		"Load after failed preflight did not reacquire every admission: %s" %
		JSON.stringify(load_result)):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == source_slot_id and
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD and
		not builder.HasActivePlaceSession() and
		builder.GetUndoEditCount() == 0 and
		builder.GetRedoEditCount() == 0 and
		renderer.GetRenderedEdgeCount() == 0 and
		matching_presentation_is_ready(renderer),
		"Load after failed preflight did not jointly commit empty graph/tool/presentation/slot state: %s" %
		JSON.stringify({
			"slot": str(save_manager.get("CurrentSlotID")),
			"tool": int(tool_manager.get("CurrentTool")),
			"hasPlacement": builder.HasActivePlaceSession(),
			"undoCount": builder.GetUndoEditCount(),
			"redoCount": builder.GetRedoEditCount(),
			"renderedEdges": renderer.GetRenderedEdgeCount(),
			"presentation": renderer.GetPresentationState(),
		})):
		return

	print("ROAD_LOAD_PREFLIGHT_RESOURCE_FAILURE_RESULT %s" % JSON.stringify({
		"exception_type": str(probe_result.get("exceptionType", "")),
		"resource_count_before": int(probe_result.get("resourceCountBefore", -1)),
		"resource_count_after": int(probe_result.get("resourceCountAfter", -1)),
		"road_vertices": int(probe_result.get("roadVertexCount", -1)),
		"node_markers": int(probe_result.get("nodeMarkerCount", -1)),
		"load_result_kind": int(load_result.get("resultKind", -1)),
	}))
	await cleanup()
	print("PASS road load preflight resource failure runtime contract")
	quit(0)

func run_load(slot_id: String) -> Dictionary:
	var operation_token: String = save_manager.StartLoad(slot_id)
	var result: Dictionary = await V3_SAVE_FIXTURE.wait_for_operation(
		save_manager,
		operation_token)
	if not await V3_SAVE_FIXTURE.wait_for_idle(save_manager):
		fail("Load did not become idle for slot %s" % slot_id)
		return {}
	return result

func matching_presentation_is_ready(renderer: Node) -> bool:
	var state: Dictionary = renderer.GetPresentationState()
	return (
		bool(state.get("isReady", false)) and
		not bool(state.get("isStalled", true)) and
		state.get("desired", {}) == state.get("presented", {}))

func cleanup() -> void:
	probe = null
	if save_manager != null and not source_slot_id.is_empty():
		await V3_SAVE_FIXTURE.delete_slot(save_manager, source_slot_id)
		source_slot_id = ""
	if save_manager != null and not active_slot_id.is_empty():
		await V3_SAVE_FIXTURE.delete_slot(save_manager, active_slot_id)
		active_slot_id = ""
	if test_map != null and is_instance_valid(test_map):
		test_map.queue_free()
		await process_frame

func require(condition: bool, message: String) -> bool:
	if condition:
		return true
	fail(message)
	return false

func fail(message: String) -> void:
	if failed:
		return
	failed = true
	push_error("FAIL road load preflight resource failure runtime contract: %s" % message)
	cleanup_after_failure.call_deferred()

func cleanup_after_failure() -> void:
	await cleanup()
	quit(1)
