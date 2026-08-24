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
	var road_system: Node = test_map.get_node("RoadSystem")
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
		probe.GetGraphCleanupFailureCount() == 0 and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		renderer.GetRenderedEdgeCount() == 1 and
		matching_presentation_is_ready(renderer),
		"Final Load did not restore one matching presentation without re-triggering probes"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD)
	if not require(builder.BeginPlace(Vector2(700.0, 600.0)), "Third transient road did not begin"):
		return
	if not require(
		builder.AddPlacePoint(Vector2(800.0, 600.0)),
		"Third transient placement point was not added"):
		return
	probe.Arm(renderer)
	probe.ArmGraphCleanupFailure(road_system)

	var graph_warned_result := await run_load(source_slot_id)
	if not require(
		int(graph_warned_result.get("resultKind", -1)) == RESULT_SUCCEEDED_WITH_WARNINGS and
		bool(graph_warned_result.get("committed", false)),
		"Graph cleanup failure did not produce a committed warning result: %s" %
		JSON.stringify(graph_warned_result)):
		return
	if not require(
		str(graph_warned_result.get("warnings", "")).contains(
			"Road presentation observer failed: Injected RoadRenderer presentation observer failure."),
		"Third observer warning did not identify the real renderer participant"):
		return
	if not require(
		str(graph_warned_result.get("warnings", "")).contains(
			"Load participant 'road-graph' cleanup failed: " +
			"Injected RoadGraph load cleanup failure."),
		"Cleanup warning did not identify the real graph participant"):
		return
	if not require(
		probe.GetTriggerCount() == 3 and
		probe.GetToolCleanupFailureCount() == 1 and
		probe.GetRendererCleanupFailureCount() == 1 and
		probe.GetGraphCleanupFailureCount() == 1 and
		not probe.IsToolCleanupFailureArmed() and
		not probe.IsRendererCleanupFailureArmed() and
		not probe.IsGraphCleanupFailureArmed(),
		"Observer and graph cleanup failure probes did not reach their exact counts"):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == source_slot_id and
		renderer.GetRenderedEdgeCount() == 0 and
		not builder.HasActivePlaceSession() and
		builder.GetUndoEditCount() == 0 and
		builder.GetRedoEditCount() == 0 and
		matching_presentation_is_ready(renderer),
		"Graph-cleanup warned Load did not leave all real participants committed"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD_UPGRADE)
	if not require(
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD_UPGRADE,
		"Graph cleanup failure prevented later tool use"):
		return

	var graph_clean_result := await run_load(active_slot_id)
	if not require(
		int(graph_clean_result.get("resultKind", -1)) == RESULT_SUCCEEDED and
		bool(graph_clean_result.get("committed", false)) and
		str(graph_clean_result.get("warnings", "")).is_empty(),
		"Load after graph cleanup failure did not re-admit every participant: %s" %
		JSON.stringify(graph_clean_result)):
		return
	if not require(
		probe.GetTriggerCount() == 3 and
		probe.GetToolCleanupFailureCount() == 1 and
		probe.GetRendererCleanupFailureCount() == 1 and
		probe.GetGraphCleanupFailureCount() == 1 and
		probe.GetSlotCleanupFailureCount() == 0 and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		renderer.GetRenderedEdgeCount() == 1 and
		matching_presentation_is_ready(renderer),
		"Graph-cleanup recovery Load did not restore one matching presentation"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD)
	if not require(builder.BeginPlace(Vector2(700.0, 700.0)), "Fourth transient road did not begin"):
		return
	if not require(
		builder.AddPlacePoint(Vector2(800.0, 700.0)),
		"Fourth transient placement point was not added"):
		return
	probe.Arm(renderer)
	probe.ArmSlotCleanupFailure(save_manager)

	var slot_warned_result := await run_load(source_slot_id)
	if not require(
		int(slot_warned_result.get("resultKind", -1)) == RESULT_SUCCEEDED_WITH_WARNINGS and
		bool(slot_warned_result.get("committed", false)),
		"Slot cleanup failure did not produce a committed warning result: %s" %
		JSON.stringify(slot_warned_result)):
		return
	if not require(
		str(slot_warned_result.get("warnings", "")).contains(
			"Road presentation observer failed: Injected RoadRenderer presentation observer failure."),
		"Fourth observer warning did not identify the real renderer participant"):
		return
	if not require(
		str(slot_warned_result.get("warnings", "")).contains(
			"Load participant 'slot-target' cleanup failed: " +
			"Injected slot target load cleanup failure."),
		"Cleanup warning did not identify the real slot participant"):
		return
	if not require(
		probe.GetTriggerCount() == 4 and
		probe.GetToolCleanupFailureCount() == 1 and
		probe.GetRendererCleanupFailureCount() == 1 and
		probe.GetGraphCleanupFailureCount() == 1 and
		probe.GetSlotCleanupFailureCount() == 1 and
		not probe.IsToolCleanupFailureArmed() and
		not probe.IsRendererCleanupFailureArmed() and
		not probe.IsGraphCleanupFailureArmed() and
		not probe.IsSlotCleanupFailureArmed(),
		"Observer and slot cleanup failure probes did not reach their exact counts"):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == source_slot_id and
		renderer.GetRenderedEdgeCount() == 0 and
		not builder.HasActivePlaceSession() and
		builder.GetUndoEditCount() == 0 and
		builder.GetRedoEditCount() == 0 and
		matching_presentation_is_ready(renderer),
		"Slot-cleanup warned Load did not leave all real participants committed"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD_UPGRADE)
	if not require(
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD_UPGRADE,
		"Slot cleanup failure prevented later tool use"):
		return

	var slot_clean_result := await run_load(active_slot_id)
	if not require(
		int(slot_clean_result.get("resultKind", -1)) == RESULT_SUCCEEDED and
		bool(slot_clean_result.get("committed", false)) and
		str(slot_clean_result.get("warnings", "")).is_empty(),
		"Load after slot cleanup failure did not re-admit every participant: %s" %
		JSON.stringify(slot_clean_result)):
		return
	if not require(
		probe.GetTriggerCount() == 4 and
		probe.GetToolCleanupFailureCount() == 1 and
		probe.GetRendererCleanupFailureCount() == 1 and
		probe.GetGraphCleanupFailureCount() == 1 and
		probe.GetSlotCleanupFailureCount() == 1 and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		renderer.GetRenderedEdgeCount() == 1 and
		matching_presentation_is_ready(renderer),
		"Slot-cleanup recovery Load did not restore one matching presentation"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD)
	if not require(builder.BeginPlace(Vector2(700.0, 800.0)), "Fifth transient road did not begin"):
		return
	if not require(
		builder.AddPlacePoint(Vector2(800.0, 800.0)),
		"Fifth transient placement point was not added"):
		return
	probe.Arm(renderer)
	probe.ArmGraphCleanupFailure(road_system)
	probe.ArmToolCleanupFailure(tool_manager)
	probe.ArmRendererCleanupFailure(renderer)
	probe.ArmSlotCleanupFailure(save_manager)

	var combined_warned_result := await run_load(source_slot_id)
	if not require(
		int(combined_warned_result.get("resultKind", -1)) == RESULT_SUCCEEDED_WITH_WARNINGS and
		bool(combined_warned_result.get("committed", false)),
		"Combined cleanup failures did not produce a committed warning result: %s" %
		JSON.stringify(combined_warned_result)):
		return
	var combined_warnings := str(combined_warned_result.get("warnings", ""))
	if not require(
		combined_warnings.split("\n", false).size() == 5,
		"Combined failure did not preserve exactly five warnings: %s" % combined_warnings):
		return
	if not require(
		combined_warnings.contains(
			"Road presentation observer failed: " +
			"Injected RoadRenderer presentation observer failure."),
		"Combined observer warning did not identify the real renderer participant"):
		return
	if not require(
		combined_warnings.contains(
			"Load participant 'road-graph' cleanup failed: " +
			"Injected RoadGraph load cleanup failure."),
		"Combined cleanup warning did not identify the real graph participant"):
		return
	if not require(
		combined_warnings.contains(
			"Load participant 'road-tools' cleanup failed: " +
			"Injected ToolManager load cleanup failure."),
		"Combined cleanup warning did not identify the real tool participant"):
		return
	if not require(
		combined_warnings.contains(
			"Load participant 'road-presentation' cleanup failed: " +
			"Injected RoadRenderer load cleanup failure."),
		"Combined cleanup warning did not identify the real renderer participant"):
		return
	if not require(
		combined_warnings.contains(
			"Load participant 'slot-target' cleanup failed: " +
			"Injected slot target load cleanup failure."),
		"Combined cleanup warning did not identify the real slot participant"):
		return
	if not require(
		probe.GetTriggerCount() == 5 and
		probe.GetToolCleanupFailureCount() == 2 and
		probe.GetRendererCleanupFailureCount() == 2 and
		probe.GetGraphCleanupFailureCount() == 2 and
		probe.GetSlotCleanupFailureCount() == 2 and
		not probe.IsToolCleanupFailureArmed() and
		not probe.IsRendererCleanupFailureArmed() and
		not probe.IsGraphCleanupFailureArmed() and
		not probe.IsSlotCleanupFailureArmed(),
		"Combined observer and cleanup probes did not reach their exact counts"):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == source_slot_id and
		renderer.GetRenderedEdgeCount() == 0 and
		not builder.HasActivePlaceSession() and
		builder.GetUndoEditCount() == 0 and
		builder.GetRedoEditCount() == 0 and
		matching_presentation_is_ready(renderer),
		"Combined-failure Load did not leave all real participants committed"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD_UPGRADE)
	if not require(
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD_UPGRADE,
		"Combined cleanup failures prevented later tool use"):
		return

	var combined_clean_result := await run_load(active_slot_id)
	if not require(
		int(combined_clean_result.get("resultKind", -1)) == RESULT_SUCCEEDED and
		bool(combined_clean_result.get("committed", false)) and
		str(combined_clean_result.get("warnings", "")).is_empty(),
		"Load after combined cleanup failures did not re-admit every participant: %s" %
		JSON.stringify(combined_clean_result)):
		return
	if not require(
		probe.GetTriggerCount() == 5 and
		probe.GetToolCleanupFailureCount() == 2 and
		probe.GetRendererCleanupFailureCount() == 2 and
		probe.GetGraphCleanupFailureCount() == 2 and
		probe.GetSlotCleanupFailureCount() == 2 and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		renderer.GetRenderedEdgeCount() == 1 and
		matching_presentation_is_ready(renderer),
		"Combined-failure recovery Load did not restore one matching presentation"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD)
	if not require(builder.BeginPlace(Vector2(700.0, 900.0)), "Sixth transient road did not begin"):
		return
	if not require(
		builder.AddPlacePoint(Vector2(800.0, 900.0)),
		"Sixth transient placement point was not added"):
		return
	probe.ArmGraphObserverFailure(road_system)
	probe.ArmGraphCleanupFailure(road_system)
	probe.ArmToolCleanupFailure(tool_manager)
	probe.ArmRendererCleanupFailure(renderer)
	probe.ArmSlotCleanupFailure(save_manager)

	var graph_observer_warned_result := await run_load(source_slot_id)
	if not require(
		int(graph_observer_warned_result.get("resultKind", -1)) ==
		RESULT_SUCCEEDED_WITH_WARNINGS and
		bool(graph_observer_warned_result.get("committed", false)),
		"Graph observer and cleanup failures did not produce a committed warning result: %s" %
		JSON.stringify(graph_observer_warned_result)):
		return
	var graph_observer_warnings := str(graph_observer_warned_result.get("warnings", ""))
	if not require(
		graph_observer_warnings.split("\n", false).size() == 5,
		"Graph observer combination did not preserve exactly five warnings: %s" %
		graph_observer_warnings):
		return
	if not require(
		graph_observer_warnings.contains(
			"RoadGraph observer failed: Injected RoadGraph observer failure."),
		"Graph observer warning did not identify the real RoadGraph participant"):
		return
	if not require(
		graph_observer_warnings.contains(
			"Load participant 'road-graph' cleanup failed: " +
			"Injected RoadGraph load cleanup failure."),
		"Graph-observer combination did not identify graph cleanup"):
		return
	if not require(
		graph_observer_warnings.contains(
			"Load participant 'road-tools' cleanup failed: " +
			"Injected ToolManager load cleanup failure."),
		"Graph-observer combination did not identify tool cleanup"):
		return
	if not require(
		graph_observer_warnings.contains(
			"Load participant 'road-presentation' cleanup failed: " +
			"Injected RoadRenderer load cleanup failure."),
		"Graph-observer combination did not identify renderer cleanup"):
		return
	if not require(
		graph_observer_warnings.contains(
			"Load participant 'slot-target' cleanup failed: " +
			"Injected slot target load cleanup failure."),
		"Graph-observer combination did not identify slot cleanup"):
		return
	if not require(
		probe.GetTriggerCount() == 5 and
		probe.GetGraphObserverTriggerCount() == 1 and
		probe.GetToolCleanupFailureCount() == 3 and
		probe.GetRendererCleanupFailureCount() == 3 and
		probe.GetGraphCleanupFailureCount() == 3 and
		probe.GetSlotCleanupFailureCount() == 3 and
		not probe.IsGraphObserverFailureArmed() and
		not probe.IsToolCleanupFailureArmed() and
		not probe.IsRendererCleanupFailureArmed() and
		not probe.IsGraphCleanupFailureArmed() and
		not probe.IsSlotCleanupFailureArmed(),
		"Graph observer and cleanup probes did not reach their exact counts"):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == source_slot_id and
		renderer.GetRenderedEdgeCount() == 0 and
		not builder.HasActivePlaceSession() and
		builder.GetUndoEditCount() == 0 and
		builder.GetRedoEditCount() == 0 and
		matching_presentation_is_ready(renderer),
		"Graph-observer warned Load did not leave all real participants committed"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD_UPGRADE)
	if not require(
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD_UPGRADE,
		"Graph observer and cleanup failures prevented later tool use"):
		return

	var graph_observer_clean_result := await run_load(active_slot_id)
	if not require(
		int(graph_observer_clean_result.get("resultKind", -1)) == RESULT_SUCCEEDED and
		bool(graph_observer_clean_result.get("committed", false)) and
		str(graph_observer_clean_result.get("warnings", "")).is_empty(),
		"Load after graph observer and cleanup failures did not re-admit every participant: %s" %
		JSON.stringify(graph_observer_clean_result)):
		return
	if not require(
		probe.GetTriggerCount() == 5 and
		probe.GetGraphObserverTriggerCount() == 1 and
		probe.GetToolCleanupFailureCount() == 3 and
		probe.GetRendererCleanupFailureCount() == 3 and
		probe.GetGraphCleanupFailureCount() == 3 and
		probe.GetSlotCleanupFailureCount() == 3 and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		renderer.GetRenderedEdgeCount() == 1 and
		matching_presentation_is_ready(renderer),
		"Graph-observer recovery Load did not restore one matching presentation"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD)
	if not require(builder.BeginPlace(Vector2(700.0, 1000.0)), "Seventh transient road did not begin"):
		return
	if not require(
		builder.AddPlacePoint(Vector2(800.0, 1000.0)),
		"Seventh transient placement point was not added"):
		return
	probe.Arm(renderer)
	probe.ArmGraphObserverFailure(road_system)
	probe.ArmGraphCleanupFailure(road_system)
	probe.ArmToolCleanupFailure(tool_manager)
	probe.ArmRendererCleanupFailure(renderer)
	probe.ArmSlotCleanupFailure(save_manager)

	var dual_observer_warned_result := await run_load(source_slot_id)
	if not require(
		int(dual_observer_warned_result.get("resultKind", -1)) ==
		RESULT_SUCCEEDED_WITH_WARNINGS and
		bool(dual_observer_warned_result.get("committed", false)),
		"Dual observer and cleanup failures did not produce a committed warning result: %s" %
		JSON.stringify(dual_observer_warned_result)):
		return
	var dual_observer_warnings := str(dual_observer_warned_result.get("warnings", ""))
	if not require(
		dual_observer_warnings.split("\n", false).size() == 6,
		"Dual-observer combination did not preserve exactly six warnings: %s" %
		dual_observer_warnings):
		return
	if not require(
		dual_observer_warnings.contains(
			"Road presentation observer failed: " +
			"Injected RoadRenderer presentation observer failure."),
		"Dual-observer warning did not identify the real renderer participant"):
		return
	if not require(
		dual_observer_warnings.contains(
			"RoadGraph observer failed: Injected RoadGraph observer failure."),
		"Dual-observer warning did not identify the real RoadGraph participant"):
		return
	if not require(
		dual_observer_warnings.contains(
			"Load participant 'road-graph' cleanup failed: " +
			"Injected RoadGraph load cleanup failure."),
		"Dual-observer combination did not identify graph cleanup"):
		return
	if not require(
		dual_observer_warnings.contains(
			"Load participant 'road-tools' cleanup failed: " +
			"Injected ToolManager load cleanup failure."),
		"Dual-observer combination did not identify tool cleanup"):
		return
	if not require(
		dual_observer_warnings.contains(
			"Load participant 'road-presentation' cleanup failed: " +
			"Injected RoadRenderer load cleanup failure."),
		"Dual-observer combination did not identify renderer cleanup"):
		return
	if not require(
		dual_observer_warnings.contains(
			"Load participant 'slot-target' cleanup failed: " +
			"Injected slot target load cleanup failure."),
		"Dual-observer combination did not identify slot cleanup"):
		return
	if not require(
		probe.GetTriggerCount() == 6 and
		probe.GetGraphObserverTriggerCount() == 2 and
		probe.GetToolCleanupFailureCount() == 4 and
		probe.GetRendererCleanupFailureCount() == 4 and
		probe.GetGraphCleanupFailureCount() == 4 and
		probe.GetSlotCleanupFailureCount() == 4 and
		not probe.IsGraphObserverFailureArmed() and
		not probe.IsToolCleanupFailureArmed() and
		not probe.IsRendererCleanupFailureArmed() and
		not probe.IsGraphCleanupFailureArmed() and
		not probe.IsSlotCleanupFailureArmed(),
		"Dual observer and cleanup probes did not reach their exact counts"):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == source_slot_id and
		renderer.GetRenderedEdgeCount() == 0 and
		not builder.HasActivePlaceSession() and
		builder.GetUndoEditCount() == 0 and
		builder.GetRedoEditCount() == 0 and
		matching_presentation_is_ready(renderer),
		"Dual-observer warned Load did not leave all real participants committed"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD_UPGRADE)
	if not require(
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD_UPGRADE,
		"Dual observer and cleanup failures prevented later tool use"):
		return

	var dual_observer_clean_result := await run_load(active_slot_id)
	if not require(
		int(dual_observer_clean_result.get("resultKind", -1)) == RESULT_SUCCEEDED and
		bool(dual_observer_clean_result.get("committed", false)) and
		str(dual_observer_clean_result.get("warnings", "")).is_empty(),
		"Load after dual observer and cleanup failures did not re-admit every participant: %s" %
		JSON.stringify(dual_observer_clean_result)):
		return
	if not require(
		probe.GetTriggerCount() == 6 and
		probe.GetGraphObserverTriggerCount() == 2 and
		probe.GetToolCleanupFailureCount() == 4 and
		probe.GetRendererCleanupFailureCount() == 4 and
		probe.GetGraphCleanupFailureCount() == 4 and
		probe.GetSlotCleanupFailureCount() == 4 and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		renderer.GetRenderedEdgeCount() == 1 and
		matching_presentation_is_ready(renderer),
		"Dual-observer recovery Load did not restore one matching presentation"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD)
	if not require(builder.BeginPlace(Vector2(700.0, 1100.0)), "Eighth transient road did not begin"):
		return
	if not require(
		builder.AddPlacePoint(Vector2(800.0, 1100.0)),
		"Eighth transient placement point was not added"):
		return
	probe.ArmPostCommitFailure(save_manager)

	var post_commit_warned_result := await run_load(source_slot_id)
	if not require(
		int(post_commit_warned_result.get("resultKind", -1)) ==
		RESULT_SUCCEEDED_WITH_WARNINGS and
		bool(post_commit_warned_result.get("committed", false)),
		"Aggregate post-commit work failure did not produce a committed warning result: %s" %
		JSON.stringify(post_commit_warned_result)):
		return
	if not require(
		str(post_commit_warned_result.get("warnings", "")).contains(
			"Load post-commit work failed: " +
			"Injected aggregate Load post-commit work failure."),
		"Aggregate post-commit warning did not preserve the failure cause"):
		return
	if not require(
		probe.GetPostCommitFailureCount() == 1 and
		not probe.IsPostCommitFailureArmed(),
		"Aggregate post-commit failure probe did not trigger exactly once"):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == source_slot_id and
		renderer.GetRenderedEdgeCount() == 0 and
		not builder.HasActivePlaceSession() and
		builder.GetUndoEditCount() == 0 and
		builder.GetRedoEditCount() == 0 and
		matching_presentation_is_ready(renderer),
		"Aggregate post-commit warning did not preserve the committed Load state"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD_UPGRADE)
	if not require(
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD_UPGRADE,
		"Aggregate post-commit warning left a participant admission active"):
		return

	var post_commit_clean_result := await run_load(active_slot_id)
	if not require(
		int(post_commit_clean_result.get("resultKind", -1)) == RESULT_SUCCEEDED and
		bool(post_commit_clean_result.get("committed", false)) and
		str(post_commit_clean_result.get("warnings", "")).is_empty(),
		"Load after aggregate post-commit warning did not re-admit every participant: %s" %
		JSON.stringify(post_commit_clean_result)):
		return
	if not require(
		probe.GetPostCommitFailureCount() == 1 and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		renderer.GetRenderedEdgeCount() == 1 and
		matching_presentation_is_ready(renderer),
		"Aggregate post-commit recovery Load did not restore one matching presentation"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD)
	if not require(builder.BeginPlace(Vector2(700.0, 1200.0)), "Ninth transient road did not begin"):
		return
	if not require(
		builder.AddPlacePoint(Vector2(800.0, 1200.0)),
		"Ninth transient placement point was not added"):
		return
	probe.Arm(renderer)
	probe.ArmGraphObserverFailure(road_system)
	probe.ArmGraphCleanupFailure(road_system)
	probe.ArmToolCleanupFailure(tool_manager)
	probe.ArmRendererCleanupFailure(renderer)
	probe.ArmSlotCleanupFailure(save_manager)
	probe.ArmPostCommitFailure(save_manager)

	var post_commit_combined_warned_result := await run_load(source_slot_id)
	if not require(
		int(post_commit_combined_warned_result.get("resultKind", -1)) ==
		RESULT_SUCCEEDED_WITH_WARNINGS and
		bool(post_commit_combined_warned_result.get("committed", false)),
		"Combined participant and post-commit failures did not produce a committed warning result: %s" %
		JSON.stringify(post_commit_combined_warned_result)):
		return
	var post_commit_combined_warnings := str(
		post_commit_combined_warned_result.get("warnings", ""))
	var post_commit_combined_expected_warnings := PackedStringArray([
		"RoadGraph observer failed: Injected RoadGraph observer failure.",
		"Road presentation observer failed: " +
			"Injected RoadRenderer presentation observer failure.",
		"Load participant 'road-graph' cleanup failed: " +
			"Injected RoadGraph load cleanup failure.",
		"Load participant 'road-tools' cleanup failed: " +
			"Injected ToolManager load cleanup failure.",
		"Load participant 'road-presentation' cleanup failed: " +
			"Injected RoadRenderer load cleanup failure.",
		"Load participant 'slot-target' cleanup failed: " +
			"Injected slot target load cleanup failure.",
		"Load post-commit work failed: " +
			"Injected aggregate Load post-commit work failure.",
	])
	if not require(
		post_commit_combined_warnings.split("\n", false) ==
		post_commit_combined_expected_warnings,
		"Combined participant/post-commit warnings were missing or out of order: %s" %
		post_commit_combined_warnings):
		return
	if not require(
		probe.GetTriggerCount() == 7 and
		probe.GetGraphObserverTriggerCount() == 3 and
		probe.GetToolCleanupFailureCount() == 5 and
		probe.GetRendererCleanupFailureCount() == 5 and
		probe.GetGraphCleanupFailureCount() == 5 and
		probe.GetSlotCleanupFailureCount() == 5 and
		probe.GetPostCommitFailureCount() == 2 and
		not probe.IsGraphObserverFailureArmed() and
		not probe.IsToolCleanupFailureArmed() and
		not probe.IsRendererCleanupFailureArmed() and
		not probe.IsGraphCleanupFailureArmed() and
		not probe.IsSlotCleanupFailureArmed() and
		not probe.IsPostCommitFailureArmed(),
		"Combined participant/post-commit probes did not reach their exact counts"):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == source_slot_id and
		renderer.GetRenderedEdgeCount() == 0 and
		not builder.HasActivePlaceSession() and
		builder.GetUndoEditCount() == 0 and
		builder.GetRedoEditCount() == 0 and
		matching_presentation_is_ready(renderer),
		"Combined participant/post-commit warning did not preserve the committed Load state"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD_UPGRADE)
	if not require(
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD_UPGRADE,
		"Combined participant/post-commit warning left an admission active"):
		return

	var post_commit_combined_clean_result := await run_load(active_slot_id)
	if not require(
		int(post_commit_combined_clean_result.get("resultKind", -1)) == RESULT_SUCCEEDED and
		bool(post_commit_combined_clean_result.get("committed", false)) and
		str(post_commit_combined_clean_result.get("warnings", "")).is_empty(),
		"Load after combined participant/post-commit warnings did not re-admit every participant: %s" %
		JSON.stringify(post_commit_combined_clean_result)):
		return
	if not require(
		probe.GetTriggerCount() == 7 and
		probe.GetGraphObserverTriggerCount() == 3 and
		probe.GetToolCleanupFailureCount() == 5 and
		probe.GetRendererCleanupFailureCount() == 5 and
		probe.GetGraphCleanupFailureCount() == 5 and
		probe.GetSlotCleanupFailureCount() == 5 and
		probe.GetPostCommitFailureCount() == 2 and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		renderer.GetRenderedEdgeCount() == 1 and
		matching_presentation_is_ready(renderer),
		"Combined participant/post-commit recovery Load did not restore one matching presentation"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD)
	if not require(builder.BeginPlace(Vector2(700.0, 1300.0)), "Tenth transient road did not begin"):
		return
	if not require(
		builder.AddPlacePoint(Vector2(800.0, 1300.0)),
		"Tenth transient placement point was not added"):
		return
	probe.ArmPostCommitCancellation(renderer, save_manager)
	var post_commit_cancel_operation_token: String = save_manager.StartLoad(source_slot_id)
	var post_commit_cancel_result: Dictionary = await V3_SAVE_FIXTURE.wait_for_operation(
		save_manager,
		post_commit_cancel_operation_token)
	if not await V3_SAVE_FIXTURE.wait_for_idle(save_manager):
		fail("Post-commit cancellation Load did not become idle")
		return
	if not require(
		int(post_commit_cancel_result.get("resultKind", -1)) == RESULT_SUCCEEDED and
		bool(post_commit_cancel_result.get("committed", false)) and
		str(post_commit_cancel_result.get("warnings", "")).is_empty() and
		str(post_commit_cancel_result.get("error", "")).is_empty(),
		"Cancellation requested after aggregate commit changed the successful result: %s" %
		JSON.stringify(post_commit_cancel_result)):
		return
	if not require(
		probe.GetPostCommitCancellationCount() == 1 and
		not probe.WasPostCommitCancellationAccepted() and
		probe.GetPostCommitCancellationOperationToken() ==
		post_commit_cancel_operation_token and
		not probe.IsPostCommitCancellationArmed(),
		"Post-commit cancellation probe did not record one rejected matching request"):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == source_slot_id and
		renderer.GetRenderedEdgeCount() == 0 and
		not builder.HasActivePlaceSession() and
		builder.GetUndoEditCount() == 0 and
		builder.GetRedoEditCount() == 0 and
		matching_presentation_is_ready(renderer),
		"Post-commit cancellation request did not preserve the committed Load state"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD_UPGRADE)
	if not require(
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD_UPGRADE,
		"Post-commit cancellation request left an admission active"):
		return

	var post_commit_cancel_clean_result := await run_load(active_slot_id)
	if not require(
		int(post_commit_cancel_clean_result.get("resultKind", -1)) == RESULT_SUCCEEDED and
		bool(post_commit_cancel_clean_result.get("committed", false)) and
		str(post_commit_cancel_clean_result.get("warnings", "")).is_empty(),
		"Load after rejected post-commit cancellation did not re-admit every participant: %s" %
		JSON.stringify(post_commit_cancel_clean_result)):
		return
	if not require(
		probe.GetPostCommitCancellationCount() == 1 and
		probe.GetPostCommitCancellationOperationToken() ==
		post_commit_cancel_operation_token and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		renderer.GetRenderedEdgeCount() == 1 and
		matching_presentation_is_ready(renderer),
		"Post-commit cancellation recovery Load did not restore one matching presentation"):
		return
	var post_commit_cancel_request_accepted: bool = probe.WasPostCommitCancellationAccepted()
	var post_commit_cancel_trigger_count: int = probe.GetPostCommitCancellationCount()

	tool_manager.set("CurrentTool", TOOL_ROAD)
	if not require(
		builder.BeginPlace(Vector2(700.0, 1400.0)),
		"Eleventh transient road did not begin"):
		return
	if not require(
		builder.AddPlacePoint(Vector2(800.0, 1400.0)),
		"Eleventh transient placement point was not added"):
		return
	probe.Arm(renderer)
	probe.ArmGraphObserverFailure(road_system)
	probe.ArmGraphCleanupFailure(road_system)
	probe.ArmToolCleanupFailure(tool_manager)
	probe.ArmRendererCleanupFailure(renderer)
	probe.ArmSlotCleanupFailure(save_manager)
	probe.ArmPostCommitFailure(save_manager)
	probe.ArmPostCommitCancellation(renderer, save_manager)

	var post_commit_cancel_combined_operation_token: String = save_manager.StartLoad(source_slot_id)
	var post_commit_cancel_combined_warned_result: Dictionary = (
		await V3_SAVE_FIXTURE.wait_for_operation(
			save_manager,
			post_commit_cancel_combined_operation_token)
	)
	if not await V3_SAVE_FIXTURE.wait_for_idle(save_manager):
		fail("Combined post-commit cancellation Load did not become idle")
		return
	if not require(
		int(post_commit_cancel_combined_warned_result.get("resultKind", -1)) ==
		RESULT_SUCCEEDED_WITH_WARNINGS and
		bool(post_commit_cancel_combined_warned_result.get("committed", false)) and
		str(post_commit_cancel_combined_warned_result.get("error", "")).is_empty(),
		"Combined cancellation and participant failures changed the committed warning result: %s" %
		JSON.stringify(post_commit_cancel_combined_warned_result)):
		return
	var post_commit_cancel_combined_warnings := str(
		post_commit_cancel_combined_warned_result.get("warnings", ""))
	if not require(
		post_commit_cancel_combined_warnings.split("\n", false) ==
		post_commit_combined_expected_warnings,
		"Rejected cancellation changed the combined warning list or order: %s" %
		post_commit_cancel_combined_warnings):
		return
	if not require(
		probe.GetTriggerCount() == 8 and
		probe.GetGraphObserverTriggerCount() == 4 and
		probe.GetToolCleanupFailureCount() == 6 and
		probe.GetRendererCleanupFailureCount() == 6 and
		probe.GetGraphCleanupFailureCount() == 6 and
		probe.GetSlotCleanupFailureCount() == 6 and
		probe.GetPostCommitFailureCount() == 3 and
		probe.GetPostCommitCancellationCount() == 2 and
		not probe.WasPostCommitCancellationAccepted() and
		probe.GetPostCommitCancellationOperationToken() ==
		post_commit_cancel_combined_operation_token and
		not probe.IsGraphObserverFailureArmed() and
		not probe.IsToolCleanupFailureArmed() and
		not probe.IsRendererCleanupFailureArmed() and
		not probe.IsGraphCleanupFailureArmed() and
		not probe.IsSlotCleanupFailureArmed() and
		not probe.IsPostCommitFailureArmed() and
		not probe.IsPostCommitCancellationArmed(),
		"Combined cancellation/warning probes did not reach their exact counts"):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == source_slot_id and
		renderer.GetRenderedEdgeCount() == 0 and
		not builder.HasActivePlaceSession() and
		builder.GetUndoEditCount() == 0 and
		builder.GetRedoEditCount() == 0 and
		matching_presentation_is_ready(renderer),
		"Rejected cancellation changed the combined warning Load state"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD_UPGRADE)
	if not require(
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD_UPGRADE,
		"Combined cancellation/warning Load left an admission active"):
		return

	var post_commit_cancel_combined_clean_result := await run_load(active_slot_id)
	if not require(
		int(post_commit_cancel_combined_clean_result.get("resultKind", -1)) ==
		RESULT_SUCCEEDED and
		bool(post_commit_cancel_combined_clean_result.get("committed", false)) and
		str(post_commit_cancel_combined_clean_result.get("warnings", "")).is_empty(),
		"Load after combined cancellation/warnings did not re-admit every participant: %s" %
		JSON.stringify(post_commit_cancel_combined_clean_result)):
		return
	if not require(
		probe.GetTriggerCount() == 8 and
		probe.GetGraphObserverTriggerCount() == 4 and
		probe.GetToolCleanupFailureCount() == 6 and
		probe.GetRendererCleanupFailureCount() == 6 and
		probe.GetGraphCleanupFailureCount() == 6 and
		probe.GetSlotCleanupFailureCount() == 6 and
		probe.GetPostCommitFailureCount() == 3 and
		probe.GetPostCommitCancellationCount() == 2 and
		probe.GetPostCommitCancellationOperationToken() ==
		post_commit_cancel_combined_operation_token and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		renderer.GetRenderedEdgeCount() == 1 and
		matching_presentation_is_ready(renderer),
		"Combined cancellation/warning recovery Load did not restore one matching presentation"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD)
	if not require(
		builder.BeginPlace(Vector2(700.0, 1500.0)),
		"Twelfth transient road did not begin"):
		return
	if not require(
		builder.AddPlacePoint(Vector2(800.0, 1500.0)),
		"Twelfth transient placement point was not added"):
		return
	probe.ArmGraphPostCommitCancellation(road_system, save_manager)
	var post_commit_graph_cancel_operation_token: String = save_manager.StartLoad(source_slot_id)
	var post_commit_graph_cancel_result: Dictionary = await V3_SAVE_FIXTURE.wait_for_operation(
		save_manager,
		post_commit_graph_cancel_operation_token)
	if not await V3_SAVE_FIXTURE.wait_for_idle(save_manager):
		fail("Graph post-commit cancellation Load did not become idle")
		return
	if not require(
		int(post_commit_graph_cancel_result.get("resultKind", -1)) == RESULT_SUCCEEDED and
		bool(post_commit_graph_cancel_result.get("committed", false)) and
		str(post_commit_graph_cancel_result.get("warnings", "")).is_empty() and
		str(post_commit_graph_cancel_result.get("error", "")).is_empty(),
		"Cancellation from the first aggregate observer changed the successful result: %s" %
		JSON.stringify(post_commit_graph_cancel_result)):
		return
	if not require(
		probe.GetPostCommitGraphCancellationCount() == 1 and
		not probe.WasPostCommitGraphCancellationAccepted() and
		probe.GetPostCommitGraphCancellationOperationToken() ==
		post_commit_graph_cancel_operation_token and
		not probe.IsPostCommitGraphCancellationArmed(),
		"Graph post-commit cancellation probe did not record one rejected matching request"):
		return
	if not require(
		str(save_manager.get("CurrentSlotID")) == source_slot_id and
		renderer.GetRenderedEdgeCount() == 0 and
		not builder.HasActivePlaceSession() and
		builder.GetUndoEditCount() == 0 and
		builder.GetRedoEditCount() == 0 and
		matching_presentation_is_ready(renderer),
		"Graph observer cancellation did not preserve the completed aggregate notifications"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD_UPGRADE)
	if not require(
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD_UPGRADE,
		"Graph post-commit cancellation left an admission active"):
		return

	var post_commit_graph_cancel_clean_result := await run_load(active_slot_id)
	if not require(
		int(post_commit_graph_cancel_clean_result.get("resultKind", -1)) == RESULT_SUCCEEDED and
		bool(post_commit_graph_cancel_clean_result.get("committed", false)) and
		str(post_commit_graph_cancel_clean_result.get("warnings", "")).is_empty(),
		"Load after graph observer cancellation did not re-admit every participant: %s" %
		JSON.stringify(post_commit_graph_cancel_clean_result)):
		return
	if not require(
		probe.GetPostCommitGraphCancellationCount() == 1 and
		probe.GetPostCommitGraphCancellationOperationToken() ==
		post_commit_graph_cancel_operation_token and
		str(save_manager.get("CurrentSlotID")) == active_slot_id and
		renderer.GetRenderedEdgeCount() == 1 and
		matching_presentation_is_ready(renderer),
		"Graph observer cancellation recovery Load did not restore one matching presentation"):
		return

	tool_manager.set("CurrentTool", TOOL_ROAD_REMOVE)
	if not require(
		int(tool_manager.get("CurrentTool")) == TOOL_ROAD_REMOVE,
		"Combined participant/post-commit recovery Load left the tool admission active"):
		return

	print("ROAD_LOAD_OBSERVER_CLEANUP_RESULT %s" % JSON.stringify({
		"warning_result_kind": int(warned_result.get("resultKind", -1)),
		"clean_result_kind": int(clean_result.get("resultKind", -1)),
		"renderer_warning_result_kind": int(renderer_warned_result.get("resultKind", -1)),
		"renderer_clean_result_kind": int(renderer_clean_result.get("resultKind", -1)),
		"graph_warning_result_kind": int(graph_warned_result.get("resultKind", -1)),
		"graph_clean_result_kind": int(graph_clean_result.get("resultKind", -1)),
		"slot_warning_result_kind": int(slot_warned_result.get("resultKind", -1)),
		"slot_clean_result_kind": int(slot_clean_result.get("resultKind", -1)),
		"combined_warning_result_kind": int(combined_warned_result.get("resultKind", -1)),
		"combined_clean_result_kind": int(combined_clean_result.get("resultKind", -1)),
		"graph_observer_warning_result_kind": int(
			graph_observer_warned_result.get("resultKind", -1)),
		"graph_observer_clean_result_kind": int(
			graph_observer_clean_result.get("resultKind", -1)),
		"dual_observer_warning_result_kind": int(
			dual_observer_warned_result.get("resultKind", -1)),
		"dual_observer_clean_result_kind": int(
			dual_observer_clean_result.get("resultKind", -1)),
		"post_commit_warning_result_kind": int(
			post_commit_warned_result.get("resultKind", -1)),
		"post_commit_clean_result_kind": int(
			post_commit_clean_result.get("resultKind", -1)),
		"post_commit_combined_warning_result_kind": int(
			post_commit_combined_warned_result.get("resultKind", -1)),
		"post_commit_combined_clean_result_kind": int(
			post_commit_combined_clean_result.get("resultKind", -1)),
		"post_commit_cancel_result_kind": int(
			post_commit_cancel_result.get("resultKind", -1)),
		"post_commit_cancel_clean_result_kind": int(
			post_commit_cancel_clean_result.get("resultKind", -1)),
		"post_commit_cancel_request_accepted": post_commit_cancel_request_accepted,
		"post_commit_cancel_trigger_count": post_commit_cancel_trigger_count,
		"post_commit_cancel_combined_warning_result_kind": int(
			post_commit_cancel_combined_warned_result.get("resultKind", -1)),
		"post_commit_cancel_combined_clean_result_kind": int(
			post_commit_cancel_combined_clean_result.get("resultKind", -1)),
		"post_commit_cancel_combined_request_accepted":
			probe.WasPostCommitCancellationAccepted(),
		"post_commit_cancel_combined_trigger_count":
			probe.GetPostCommitCancellationCount(),
		"post_commit_graph_cancel_result_kind": int(
			post_commit_graph_cancel_result.get("resultKind", -1)),
		"post_commit_graph_cancel_clean_result_kind": int(
			post_commit_graph_cancel_clean_result.get("resultKind", -1)),
		"post_commit_graph_cancel_request_accepted":
			probe.WasPostCommitGraphCancellationAccepted(),
		"post_commit_graph_cancel_trigger_count":
			probe.GetPostCommitGraphCancellationCount(),
		"observer_trigger_count": probe.GetTriggerCount(),
		"graph_observer_trigger_count": probe.GetGraphObserverTriggerCount(),
		"tool_cleanup_trigger_count": probe.GetToolCleanupFailureCount(),
		"renderer_cleanup_trigger_count": probe.GetRendererCleanupFailureCount(),
		"graph_cleanup_trigger_count": probe.GetGraphCleanupFailureCount(),
		"slot_cleanup_trigger_count": probe.GetSlotCleanupFailureCount(),
		"post_commit_trigger_count": probe.GetPostCommitFailureCount(),
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
	if save_manager != null and not source_slot_id.is_empty():
		await V3_SAVE_FIXTURE.delete_slot(save_manager, source_slot_id)
		source_slot_id = ""
	if save_manager != null and not active_slot_id.is_empty():
		await V3_SAVE_FIXTURE.delete_slot(save_manager, active_slot_id)
		active_slot_id = ""
	if test_map != null and is_instance_valid(test_map):
		test_map.queue_free()
		await process_frame
	if probe != null:
		probe.FlushPendingManagedFinalizers()
		probe = null

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
