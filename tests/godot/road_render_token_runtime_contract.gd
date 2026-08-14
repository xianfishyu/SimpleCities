extends SceneTree

const MAP_SCENE := "res://Scenes/MapTest.tscn"
const TEST_SLOT_NAME := "Road render token runtime contract"
const V3_SAVE_FIXTURE := preload("res://tests/godot/v3_save_fixture.gd")
const TOKEN_KEYS := [
	"sceneGeneration",
	"graphFacadeID",
	"graphFacadeGeneration",
	"changeSequence",
	"roadStyleRevision",
	"renderRequestID",
]

var test_map: Node
var save_manager: Node
var slot_id := ""
var failure_cleanup_started := false

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var packed_map: PackedScene = load(MAP_SCENE)
	if not require(packed_map != null, "MapTest scene did not load"):
		return
	test_map = packed_map.instantiate()
	var autosave_controller: Node = test_map.get_node("AutosaveController")
	autosave_controller.set("AutosaveEnabled", false)
	root.add_child(test_map)
	current_scene = test_map
	await process_frame
	await process_frame
	autosave_controller.SetAutosaveEnabled(false)

	save_manager = root.get_node("SaveManager")
	var renderer: Node = test_map.get_node("RoadSystem/RoadRenderer")
	var initial := presentation_token(renderer, "Initial presentation")
	if initial.is_empty():
		return
	if not require(
		int(initial.sceneGeneration) == int(save_manager.get("SceneGeneration")),
		"Renderer did not consume SaveManager scene generation"):
		return

	var builder: Node = test_map.get_node("RoadSystem/RoadBuilder")
	if not require(builder.BeginPlace(Vector2(0.0, 0.0)), "Normal mutation did not begin"):
		return
	builder.UpdatePlace(Vector2(100.0, 0.0))
	if not require(builder.CommitPlace(Vector2(100.0, 0.0)), "Normal mutation did not commit"):
		return
	await process_frame
	await process_frame
	var mutated := presentation_token(renderer, "Normal mutation")
	if mutated.is_empty() or not require_ordinary_change(initial, mutated):
		return
	if not require_surface_hit(renderer, Vector2(50.0, 0.0), mutated, "Normal mutation"):
		return
	if not require_terminal_cap_hit(renderer, Vector2(-5.0, 0.0), mutated, "Normal mutation"):
		return

	if not require(renderer.RefreshRoadStyles(), "Explicit road style refresh did not present"):
		return
	var styled := presentation_token(renderer, "Style refresh")
	if styled.is_empty() or not require_style_change(mutated, styled):
		return
	if not require_surface_hit(renderer, Vector2(50.0, 0.0), styled, "Style refresh"):
		return
	if not require_terminal_cap_hit(renderer, Vector2(-5.0, 0.0), styled, "Style refresh"):
		return

	if not require(
		await V3_SAVE_FIXTURE.save_as(save_manager, TEST_SLOT_NAME),
		"Render token fixture slot was not created"):
		return
	slot_id = str(save_manager.get("CurrentSlotID"))
	if not require(
		V3_SAVE_FIXTURE.publish_payload(slot_id, build_fixture()),
		"Render token fixture payload and manifest could not be published"):
		return
	if not require(
		await V3_SAVE_FIXTURE.load_slot(save_manager, slot_id),
		"Render token fixture did not load"):
		return
	await process_frame
	var loaded := presentation_token(renderer, "Aggregate Load")
	if loaded.is_empty() or not require_load_change(styled, loaded):
		return
	if not require_surface_hit(renderer, Vector2(0.0, 100.0), loaded, "Aggregate Load"):
		return
	if not require_terminal_cap_hit(renderer, Vector2(-108.0, 100.0), loaded, "Aggregate Load"):
		return

	if not require(
		await V3_SAVE_FIXTURE.delete_slot(save_manager, slot_id),
		"Render token fixture slot cleanup failed"):
		return
	slot_id = ""
	test_map.queue_free()
	await process_frame
	await process_frame
	if not require(
		save_manager.get("RegisteredSaveableCount") == 0,
		"Render token cleanup retained saveables"):
		return

	print("PASS road render token runtime contract")
	quit(0)

func presentation_token(renderer: Node, source: String) -> Dictionary:
	var state: Dictionary = renderer.GetPresentationState()
	if not require(bool(state.get("isReady", false)), "%s is not presentation-ready" % source):
		return {}
	var desired: Dictionary = state.get("desired", {})
	var presented: Dictionary = state.get("presented", {})
	if not require(desired == presented, "%s published mismatched desired/presented tokens" % source):
		return {}
	for key: String in TOKEN_KEYS:
		if not require(desired.has(key), "%s token is missing %s" % [source, key]):
			return {}
	if not require(
		int(desired.sceneGeneration) > 0 and
		int(desired.graphFacadeID) > 0 and
		int(desired.graphFacadeGeneration) > 0 and
		int(desired.changeSequence) >= 0 and
		int(desired.roadStyleRevision) > 0 and
		int(desired.renderRequestID) > 0,
		"%s token contains an invalid identity component" % source):
		return {}
	return desired

func require_ordinary_change(before: Dictionary, after: Dictionary) -> bool:
	return (
		require_same(before, after, [
			"sceneGeneration",
			"graphFacadeID",
			"graphFacadeGeneration",
			"roadStyleRevision",
		], "Normal mutation") and
		require(
			int(after.changeSequence) == int(before.changeSequence) + 1,
			"Normal mutation did not advance ChangeSequence exactly once") and
		require(
			int(after.renderRequestID) == int(before.renderRequestID) + 1,
			"Normal mutation did not schedule exactly one render request"))

func require_style_change(before: Dictionary, after: Dictionary) -> bool:
	return (
		require_same(before, after, [
			"sceneGeneration",
			"graphFacadeID",
			"graphFacadeGeneration",
			"changeSequence",
		], "Style refresh") and
		require(
			int(after.roadStyleRevision) == int(before.roadStyleRevision) + 1,
			"Style refresh did not advance RoadStyleRevision exactly once") and
		require(
			int(after.renderRequestID) == int(before.renderRequestID) + 1,
			"Style refresh did not schedule exactly one render request"))

func require_load_change(before: Dictionary, after: Dictionary) -> bool:
	return (
		require_same(before, after, [
			"sceneGeneration",
			"graphFacadeID",
			"roadStyleRevision",
		], "Aggregate Load") and
		require(
			int(after.graphFacadeGeneration) == int(before.graphFacadeGeneration) + 1,
			"Aggregate Load did not advance GraphFacadeGeneration exactly once") and
		require(
			int(after.changeSequence) == int(before.changeSequence) + 1,
			"Aggregate Load did not advance ChangeSequence exactly once") and
		require(
			int(after.renderRequestID) == int(before.renderRequestID) + 1,
			"Aggregate Load did not reserve exactly one render request"))

func require_surface_hit(
	renderer: Node,
	position: Vector2,
	expected_token: Dictionary,
	source: String) -> bool:
	var state: Dictionary = renderer.GetPresentationState()
	if not require(
		int(state.get("surfacePrimitiveCount", 0)) > 0,
		"%s did not publish road surface primitives" % source):
		return false
	var hit: Dictionary = renderer.FindRoadSurfaceHit(position, 0.0)
	var location: Dictionary = hit.get("location", {})
	return (
		require(not hit.is_empty(), "%s did not return a visible surface hit" % source) and
		require(
			hit.get("ownerKind", "") == "EdgeRibbon",
			"%s returned a non-ribbon owner for the basic surface" % source) and
		require(
			float(hit.get("surfaceDistance", -1.0)) == 0.0,
			"%s returned a non-zero distance inside the visible surface" % source) and
		require(
			hit.get("renderToken", {}) == expected_token,
			"%s surface hit token did not match the presented token" % source) and
		require(not location.is_empty(), "%s surface hit did not return a canonical location" % source) and
		require(
			int(location.get("edgeID", -1)) == int(hit.get("edgeID", -2)),
			"%s surface location did not preserve its owner Edge" % source) and
		require(
			int(location.get("geometryIndex", -1)) >= 0,
			"%s surface location returned an invalid geometry index" % source) and
		require(
			float(location.get("parameter", -1.0)) >= 0.0 and
			float(location.get("parameter", 2.0)) <= 1.0,
			"%s surface location returned an invalid parameter" % source))

func require_terminal_cap_hit(
	renderer: Node,
	position: Vector2,
	expected_token: Dictionary,
	source: String) -> bool:
	var hit: Dictionary = renderer.FindRoadSurfaceHit(position, 0.0)
	var location: Dictionary = hit.get("location", {})
	return (
		require(not hit.is_empty(), "%s did not return a terminal cap hit" % source) and
		require(
			hit.get("ownerKind", "") == "TerminalCap",
			"%s returned the wrong terminal cap owner kind" % source) and
		require(
			hit.get("endpoint", "") == "A" and int(hit.get("nodeID", -1)) >= 0,
			"%s terminal cap did not preserve its Node incidence" % source) and
		require(
			float(hit.get("surfaceDistance", -1.0)) == 0.0,
			"%s returned a non-zero distance inside the terminal cap" % source) and
		require(
			hit.get("renderToken", {}) == expected_token,
			"%s terminal cap token did not match the presented token" % source) and
		require(not location.is_empty(), "%s terminal cap omitted its canonical location" % source) and
		require(
			int(location.get("edgeID", -1)) == int(hit.get("edgeID", -2)) and
			int(location.get("geometryIndex", -1)) == 0 and
			is_zero_approx(float(location.get("parameter", -1.0))),
			"%s terminal cap did not return the canonical Edge start" % source))

func require_same(
	before: Dictionary,
	after: Dictionary,
	keys: Array,
	source: String) -> bool:
	for key: String in keys:
		if not require(
			int(after[key]) == int(before[key]),
			"%s unexpectedly changed %s" % [source, key]):
			return false
	return true

func build_fixture() -> Dictionary:
	return {
		"formatFamily": "simple-cities-v3",
		"payloadType": "road-network",
		"schemaVersion": 1,
		"nextID": 3,
		"nodes": [
			{"id": 0, "x": -100.0, "y": 100.0},
			{"id": 1, "x": 100.0, "y": 100.0},
		],
		"edges": [{
			"id": 2,
			"nodeAID": 0,
			"nodeBID": 1,
			"roadType": "highway",
			"geometry": [{
				"version": 1,
				"kind": "line",
				"start": {"x": -100.0, "y": 100.0},
				"end": {"x": 100.0, "y": 100.0},
			}],
		}],
	}

func require(condition: bool, message: String) -> bool:
	if condition:
		return true
	push_error(message)
	if not failure_cleanup_started:
		failure_cleanup_started = true
		cleanup_after_failure.call_deferred()
	return false

func cleanup_after_failure() -> void:
	if save_manager != null and not slot_id.is_empty():
		await V3_SAVE_FIXTURE.delete_slot(save_manager, slot_id)
		slot_id = ""
	if test_map != null:
		test_map.queue_free()
	quit(1)
