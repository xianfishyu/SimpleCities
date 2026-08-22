extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const PROBE_PATH := "res://tests/godot/RoadLoadObserverFailureProbe.cs"
const SOURCE_SLOT_NAME := "Road observer cleanup source"
const ACTIVE_SLOT_NAME := "Road observer cleanup active"
const V3_SAVE_FIXTURE := preload("res://tests/godot/v3_save_fixture.gd")
const RESULT_SUCCEEDED := 0
const RESULT_SUCCEEDED_WITH_WARNINGS := 1
const TOOL_ROAD := 1
const TOOL_ROAD_REMOVE := 2
const TOOL_ROAD_UPGRADE := 3

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
	if not require(renderer.GetRenderedEdgeCount() == 1, "Active road did not render"):
		return
	if not require(
		await V3_SAVE_FIXTURE.save_as(save_manager, ACTIVE_SLOT_NAME),
		"Could not create the active slot"):
		return
	active_slot_id = str(save_manager.get("CurrentSlotID"))

	tool_manager.set("CurrentTool", TOOL_ROAD)
	if not require(builder.BeginPlace(Vector2(700.0, 400.0)), "Transient placement did not begin"):
		return
	if not require(builder.AddPlacePoint(Vector2(800.0, 400.0)), "Transient placement point was not added"):
		return

	var probe_script: Script = load(PROBE_PATH)
	if not require(probe_script != null, "Debug observer failure probe did not load"):
		return
	probe = probe_script.new()
	if not require(probe != null, "Debug observer failure probe did not instantiate"):
		return
	probe.Arm(renderer)
	probe.ArmToolCleanupFailure(tool_manager)

	var warned_result := await run_load(source_slot_id)
	if not require(
		int(warned_result.get("resultKind", -1)) == RESULT_SUCCEEDED_WITH_WARNINGS and
		bool(warned_result.get("committed", false)),
		"Observer failure did not produce a committed warning result: %s" %
		JSON.stringify(warned_result)):
		return
	if not require(
		str(warned_result.get("warnings", "")).contains(
			"Road presentation observer failed: Injected RoadRenderer presentation observer failure."),
		"Observer warning did not identify the real renderer participant"):
		return
	if not require(
		str(warned_result.get("warnings", "")).contains(
			"Load participant 'road-tools' cleanup failed: " +
			"Injected ToolManager load cleanup failure."),
		"Cleanup warning did not identify the real tool participant"):
		return
	if not require(
		probe.GetTriggerCount() == 1 and
		probe.GetToolCleanupFailureCount() == 1 and
		not probe.IsToolCleanupFailureArmed(),
		"Observer and tool cleanup failure probes did not each trigger exactly once"):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == source_slot_id and
		renderer.GetRenderedEdgeCount() == 0 and
		not builder.HasActivePlaceSession() and
		builder.GetUndoEditCount() == 0 and
		builder.GetRedoEditCount() == 0 and
		matching_presentation_is_ready(renderer),
		"Warned Load did not leave graph, tools, presentation, and slot jointly committed"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD_UPGRADE)
	if not require(
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD_UPGRADE,
		"Tool cleanup did not release the admission after the warned Load"):
		return

	var clean_result := await run_load(active_slot_id)
	if not require(
		int(clean_result.get("resultKind", -1)) == RESULT_SUCCEEDED and
		bool(clean_result.get("committed", false)) and
		str(clean_result.get("warnings", "")).is_empty(),
		"Second Load did not re-admit every real participant: %s" % JSON.stringify(clean_result)):
		return
	if not require(
		probe.GetTriggerCount() == 1 and
		probe.GetToolCleanupFailureCount() == 1 and
		probe.GetRendererCleanupFailureCount() == 0 and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		renderer.GetRenderedEdgeCount() == 1 and
		matching_presentation_is_ready(renderer),
		"Second Load did not restore one matching presentation without re-triggering the probe"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD)
	if not require(builder.BeginPlace(Vector2(700.0, 500.0)), "Second transient road did not begin"):
		return
	if not require(
		builder.AddPlacePoint(Vector2(800.0, 500.0)),
		"Second transient placement point was not added"):
		return
	probe.Arm(renderer)
	probe.ArmRendererCleanupFailure(renderer)

	var renderer_warned_result := await run_load(source_slot_id)
	if not require(
		int(renderer_warned_result.get("resultKind", -1)) == RESULT_SUCCEEDED_WITH_WARNINGS and
		bool(renderer_warned_result.get("committed", false)),
		"Renderer cleanup failure did not produce a committed warning result: %s" %
		JSON.stringify(renderer_warned_result)):
		return
	if not require(
		str(renderer_warned_result.get("warnings", "")).contains(
			"Road presentation observer failed: Injected RoadRenderer presentation observer failure."),
		"Second observer warning did not identify the real renderer participant"):
		return
	if not require(
		str(renderer_warned_result.get("warnings", "")).contains(
			"Load participant 'road-presentation' cleanup failed: " +
			"Injected RoadRenderer load cleanup failure."),
		"Cleanup warning did not identify the real renderer participant"):
		return
	if not require(
		probe.GetTriggerCount() == 2 and
		probe.GetToolCleanupFailureCount() == 1 and
		probe.GetRendererCleanupFailureCount() == 1 and
		not probe.IsToolCleanupFailureArmed() and
		not probe.IsRendererCleanupFailureArmed(),
		"Observer and renderer cleanup failure probes did not reach their exact counts"):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == source_slot_id and
		renderer.GetRenderedEdgeCount() == 0 and
		not builder.HasActivePlaceSession() and
		builder.GetUndoEditCount() == 0 and
		builder.GetRedoEditCount() == 0 and
		matching_presentation_is_ready(renderer),
		"Renderer-cleanup warned Load did not leave all real participants committed"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD_UPGRADE)
	if not require(
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD_UPGRADE,
		"Renderer cleanup failure prevented later tool use"):
		return

	var renderer_clean_result := await run_load(active_slot_id)
	if not require(
		int(renderer_clean_result.get("resultKind", -1)) == RESULT_SUCCEEDED and
		bool(renderer_clean_result.get("committed", false)) and
		str(renderer_clean_result.get("warnings", "")).is_empty(),
		"Load after renderer cleanup failure did not re-admit every participant: %s" %
		JSON.stringify(renderer_clean_result)):
		return
	if not require(
		probe.GetTriggerCount() == 2 and
		probe.GetToolCleanupFailureCount() == 1 and
		probe.GetRendererCleanupFailureCount() == 1 and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		renderer.GetRenderedEdgeCount() == 1 and
		matching_presentation_is_ready(renderer),
		"Final Load did not restore one matching presentation without re-triggering probes"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD_REMOVE)
	if not require(
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD_REMOVE,
		"Second Load left the tool admission active"):
		return

	print("ROAD_LOAD_OBSERVER_CLEANUP_RESULT %s" % JSON.stringify({
		"warning_result_kind": int(warned_result.get("resultKind", -1)),
		"clean_result_kind": int(clean_result.get("resultKind", -1)),
		"renderer_warning_result_kind": int(renderer_warned_result.get("resultKind", -1)),
		"renderer_clean_result_kind": int(renderer_clean_result.get("resultKind", -1)),
		"observer_trigger_count": probe.GetTriggerCount(),
		"tool_cleanup_trigger_count": probe.GetToolCleanupFailureCount(),
		"renderer_cleanup_trigger_count": probe.GetRendererCleanupFailureCount(),
		"rendered_edges": renderer.GetRenderedEdgeCount(),
		"current_tool": int(tool_manager.get("CurrentTool")),
	}))
	await cleanup()
	print("PASS road load observer cleanup runtime contract")
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
	if probe != null:
		probe.Disarm()
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
	push_error("FAIL road load observer cleanup runtime contract: %s" % message)
	cleanup_after_failure.call_deferred()

func cleanup_after_failure() -> void:
	await cleanup()
	quit(1)
