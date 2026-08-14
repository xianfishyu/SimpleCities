extends SceneTree

const CONFIG_PATH := "res://Scenes/road_config.tres"
const MAP_SCENE := "res://Scenes/MapTest.tscn"
const ROUND_TRIP_PATH := "user://road-type-style-runtime-contract.tres"
const EXPECTED_STYLES := {
	0: {"display_name": "土路", "color": Color("#8A6652"), "width": 14.0},
	1: {"display_name": "街道", "color": Color("#60727C"), "width": 20.0},
	2: {"display_name": "主干道", "color": Color("#D7A928"), "width": 26.0},
	3: {"display_name": "高速道路", "color": Color("#C84B3A"), "width": 32.0},
}

var failed := false
var map: Node

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	remove_round_trip_file()
	var config: Resource = load(CONFIG_PATH)
	require(config != null, "Production RoadConfig did not load")
	if config == null:
		await finish()
		return

	verify_config(config, "Production RoadConfig")
	verify_invalid_variants(config)
	verify_round_trip(config)
	await verify_map_scene(config)
	await finish()

func verify_config(config: Resource, source: String) -> void:
	var validation: Dictionary = config.GetRoadTypeStylesValidationResult()
	require(bool(validation.get("valid", false)), "%s is invalid: %s" % [
		source,
		validation.get("error", "missing error"),
	])
	var styles: Array = config.get("RoadTypeStyles")
	require(styles.size() == EXPECTED_STYLES.size(), "%s does not contain exactly four styles" % source)
	for road_type: int in EXPECTED_STYLES:
		var expected: Dictionary = EXPECTED_STYLES[road_type]
		var style: Resource = config.GetRoadTypeStyle(road_type)
		require(style != null, "%s lookup returned null for RoadType %d" % [source, road_type])
		if style == null:
			continue
		require(int(style.get("RoadType")) == road_type, "%s lookup returned the wrong RoadType" % source)
		require(style.get("DisplayName") == expected.display_name, "%s has the wrong display name for RoadType %d" % [source, road_type])
		require((style.get("Color") as Color).is_equal_approx(expected.color), "%s has the wrong color for RoadType %d" % [source, road_type])
		require(is_equal_approx(float(style.get("Width")), expected.width), "%s has the wrong width for RoadType %d" % [source, road_type])

func verify_invalid_variants(config: Resource) -> void:
	var missing := config.duplicate(true)
	missing.RoadTypeStyles.remove_at(0)
	require_invalid(missing, "missing RoadType")

	var missing_reference := config.duplicate(true)
	missing_reference.RoadTypeStyles[0] = null
	require_invalid(missing_reference, "empty style reference")

	var duplicate := config.duplicate(true)
	duplicate.RoadTypeStyles[3].RoadType = duplicate.RoadTypeStyles[0].RoadType
	require_invalid(duplicate, "duplicate RoadType")

	var empty_name := config.duplicate(true)
	empty_name.RoadTypeStyles[1].DisplayName = "   "
	require_invalid(empty_name, "empty display name")

	var non_finite_color := config.duplicate(true)
	non_finite_color.RoadTypeStyles[2].Color = Color(INF, 0.5, 0.5, 1.0)
	require_invalid(non_finite_color, "non-finite color")

	var transparent := config.duplicate(true)
	transparent.RoadTypeStyles[2].Color = Color(0.5, 0.5, 0.5, 0.0)
	require_invalid(transparent, "transparent color")

	var zero_width := config.duplicate(true)
	zero_width.RoadTypeStyles[3].Width = 0.0
	require_invalid(zero_width, "zero width")

	var undefined_type := config.duplicate(true)
	undefined_type.RoadTypeStyles[3].RoadType = 99
	require_invalid(undefined_type, "undefined RoadType")

func require_invalid(config: Resource, source: String) -> void:
	var validation: Dictionary = config.GetRoadTypeStylesValidationResult()
	require(not bool(validation.get("valid", true)), "Validation accepted %s" % source)
	require(not str(validation.get("error", "")).is_empty(), "Validation did not explain %s" % source)

func verify_round_trip(config: Resource) -> void:
	var copy: Resource = config.duplicate(true)
	var save_error := ResourceSaver.save(copy, ROUND_TRIP_PATH)
	require(save_error == OK, "RoadConfig round-trip save failed with error %d" % save_error)
	if save_error != OK:
		return
	var reloaded: Resource = ResourceLoader.load(ROUND_TRIP_PATH, "", ResourceLoader.CACHE_MODE_REPLACE)
	require(reloaded != null, "RoadConfig round-trip reload failed")
	if reloaded != null:
		verify_config(reloaded, "Round-tripped RoadConfig")

func verify_map_scene(production_config: Resource) -> void:
	var packed_map: PackedScene = load(MAP_SCENE)
	require(packed_map != null, "MapTest scene did not load")
	if packed_map == null:
		return
	map = packed_map.instantiate()
	map.get_node("AutosaveController").set("AutosaveEnabled", false)
	root.add_child(map)
	current_scene = map
	await process_frame
	await process_frame

	var builder_config: Resource = map.get_node("RoadSystem/RoadBuilder").get("Config")
	var renderer_config: Resource = map.get_node("RoadSystem/RoadRenderer").get("Config")
	require(builder_config == production_config, "RoadBuilder does not use the production RoadConfig")
	require(renderer_config == production_config, "RoadRenderer does not use the production RoadConfig")
	verify_config(renderer_config, "MapTest RoadRenderer config")

func remove_round_trip_file() -> void:
	if FileAccess.file_exists(ROUND_TRIP_PATH):
		var error := DirAccess.remove_absolute(ProjectSettings.globalize_path(ROUND_TRIP_PATH))
		require(error == OK, "Could not remove temporary RoadConfig round-trip resource")

func require(condition: bool, message: String) -> void:
	if condition:
		return
	failed = true
	push_error("FAIL road type style runtime contract: %s" % message)

func finish() -> void:
	if map != null and is_instance_valid(map):
		map.queue_free()
		await process_frame
		await process_frame
	remove_round_trip_file()
	var save_manager := root.get_node_or_null("SaveManager")
	if save_manager != null:
		require(save_manager.get("RegisteredSaveableCount") == 0, "Cleanup retained saveables")
	if failed:
		quit(1)
		return
	print("PASS road type style runtime contract")
	quit(0)
