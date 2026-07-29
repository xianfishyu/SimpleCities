extends SceneTree

const CATALOG_PATH := "res://Scenes/UI/RoadsConstructionCategory.tres"
const EXPECTED_TOOLS := {
	"select": {"display_name": "选择", "shortcut_hint": "Esc", "tool_type": 0, "sort_order": 0},
	"road": {"display_name": "铺路", "shortcut_hint": "R", "tool_type": 1, "sort_order": 10},
	"road-remove": {"display_name": "拆路", "shortcut_hint": "E", "tool_type": 2, "sort_order": 20},
}

func _initialize() -> void:
	var category := load(CATALOG_PATH)
	if category == null:
		fail("Could not load %s" % CATALOG_PATH)
		return

	var validation: Dictionary = category.GetValidationResult()
	if not validation.get("valid", false):
		fail("Production validation failed: %s" % validation.get("error", "missing error"))
		return

	if category.Id != "roads" or category.DisplayName != "道路":
		fail("Unexpected category identity: %s/%s" % [category.Id, category.DisplayName])
		return

	if category.Tools.size() != EXPECTED_TOOLS.size():
		fail("Expected exactly three tools, got %d" % category.Tools.size())
		return

	for tool in category.Tools:
		if not EXPECTED_TOOLS.has(tool.Id):
			fail("Unexpected tool ID: %s" % tool.Id)
			return
		var expected: Dictionary = EXPECTED_TOOLS[tool.Id]
		if tool.DisplayName != expected.display_name or tool.ShortcutHint != expected.shortcut_hint:
			fail("Unexpected display data for tool %s" % tool.Id)
			return
		if tool.ToolType != expected.tool_type:
			fail("Tool %s maps to ToolType %d" % [tool.Id, tool.ToolType])
			return
		if tool.SortOrder != expected.sort_order or tool.Description.is_empty():
			fail("Unexpected ordering or description for tool %s" % tool.Id)
			return

	var empty_id_category := category.duplicate(true)
	empty_id_category.Tools[0].Id = ""
	if empty_id_category.GetValidationResult().get("valid", true):
		fail("Production validation accepted an empty tool ID")
		return

	var duplicate_id_category := category.duplicate(true)
	duplicate_id_category.Tools[2].Id = "road"
	if duplicate_id_category.GetValidationResult().get("valid", true):
		fail("Production validation accepted duplicate tool IDs")
		return

	var empty_ref_category := category.duplicate(true)
	empty_ref_category.Tools[1] = null
	if empty_ref_category.GetValidationResult().get("valid", true):
		fail("Production validation accepted an empty tool reference")
		return

	print("PASS roads construction category contract")
	quit(0)

func fail(message: String) -> void:
	push_error(message)
	quit(1)
