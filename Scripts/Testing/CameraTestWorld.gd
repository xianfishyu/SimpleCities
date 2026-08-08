extends Node2D

const WORLD_HALF_EXTENT := 3200.0
const MINOR_GRID_SPACING := 100.0
const MAJOR_GRID_SPACING := 500.0
const LABEL_FONT_SIZE := 18

const BUILDINGS := [
	{"rect": Rect2(-1060, -680, 300, 220), "fill": Color("#5D8296"), "accent": Color("#B8DEE8"), "name": "NORTH WORKS"},
	{"rect": Rect2(-630, -940, 240, 340), "fill": Color("#8B6A55"), "accent": Color("#F1C99D"), "name": "ARCHIVE"},
	{"rect": Rect2(320, -760, 340, 260), "fill": Color("#647E62"), "accent": Color("#C2E0B2"), "name": "GREENHOUSE"},
	{"rect": Rect2(830, -480, 280, 350), "fill": Color("#7D6F9B"), "accent": Color("#D4C4EE"), "name": "OBSERVATORY"},
	{"rect": Rect2(-1160, 420, 380, 250), "fill": Color("#9C6A62"), "accent": Color("#F4B7A8"), "name": "MARKET"},
	{"rect": Rect2(-450, 610, 260, 320), "fill": Color("#566F91"), "accent": Color("#B9D0F7"), "name": "SOUTH DEPOT"},
	{"rect": Rect2(420, 500, 360, 260), "fill": Color("#85764E"), "accent": Color("#E9D889"), "name": "SOLAR YARD"},
	{"rect": Rect2(1030, 690, 280, 340), "fill": Color("#686C76"), "accent": Color("#D7DCE6"), "name": "TERMINAL"},
]


func _ready() -> void:
	queue_redraw()


func _draw() -> void:
	draw_rect(
		Rect2(-WORLD_HALF_EXTENT, -WORLD_HALF_EXTENT, WORLD_HALF_EXTENT * 2.0, WORLD_HALF_EXTENT * 2.0),
		Color("#E9EEF0"))
	draw_grid()
	draw_waterway()
	draw_roads()
	draw_districts()
	draw_buildings()
	draw_landmarks()
	draw_scale_markers()


func draw_grid() -> void:
	for coordinate in range(-int(WORLD_HALF_EXTENT), int(WORLD_HALF_EXTENT) + 1, int(MINOR_GRID_SPACING)):
		var color := Color("#D8E0E3") if coordinate % int(MAJOR_GRID_SPACING) != 0 else Color("#AEBEC5")
		var width := 1.0 if coordinate % int(MAJOR_GRID_SPACING) != 0 else 2.0
		draw_line(Vector2(coordinate, -WORLD_HALF_EXTENT), Vector2(coordinate, WORLD_HALF_EXTENT), color, width, true)
		draw_line(Vector2(-WORLD_HALF_EXTENT, coordinate), Vector2(WORLD_HALF_EXTENT, coordinate), color, width, true)


func draw_waterway() -> void:
	var river := PackedVector2Array([
		Vector2(-WORLD_HALF_EXTENT, -1260),
		Vector2(-1960, -1120),
		Vector2(-820, -1190),
		Vector2(240, -1040),
		Vector2(1280, -1130),
		Vector2(WORLD_HALF_EXTENT, -920),
		Vector2(WORLD_HALF_EXTENT, -480),
		Vector2(1300, -690),
		Vector2(240, -620),
		Vector2(-840, -770),
		Vector2(-1970, -700),
		Vector2(-WORLD_HALF_EXTENT, -840),
	])
	draw_colored_polygon(river, Color("#9FC7D7"))
	draw_polyline(river, Color("#628FA1"), 8.0, true)


func draw_roads() -> void:
	var routes := [
		PackedVector2Array([Vector2(-2800, 0), Vector2(-1200, 0), Vector2(-300, 240), Vector2(800, 120), Vector2(2800, 120)]),
		PackedVector2Array([Vector2(-200, -2500), Vector2(-200, -600), Vector2(0, 260), Vector2(0, 2350)]),
		PackedVector2Array([Vector2(-2250, 1620), Vector2(-900, 1120), Vector2(160, 1380), Vector2(2140, 1800)]),
	]
	for route in routes:
		draw_polyline(route, Color("#F9FBFC"), 42.0, true)
		draw_polyline(route, Color("#AEBCC2"), 4.0, true)
		for point in route:
			draw_circle(point, 10.0, Color("#456475"))
			draw_circle(point, 4.0, Color("#F5CD72"))


func draw_districts() -> void:
	draw_circle(Vector2(-1180, 1100), 430.0, Color("#BFD6B2", 0.62))
	draw_circle(Vector2(1200, 1060), 520.0, Color("#E8CFB8", 0.62))
	draw_circle(Vector2(950, -1320), 370.0, Color("#C9C4E0", 0.62))
	draw_string(ThemeDB.fallback_font, Vector2(-1450, 1120), "PARK DISTRICT", HORIZONTAL_ALIGNMENT_LEFT, -1, LABEL_FONT_SIZE, Color("#41624D"))
	draw_string(ThemeDB.fallback_font, Vector2(920, 1080), "INDUSTRIAL QUARTER", HORIZONTAL_ALIGNMENT_LEFT, -1, LABEL_FONT_SIZE, Color("#765B3C"))
	draw_string(ThemeDB.fallback_font, Vector2(730, -1300), "RESEARCH RIDGE", HORIZONTAL_ALIGNMENT_LEFT, -1, LABEL_FONT_SIZE, Color("#51486F"))


func draw_buildings() -> void:
	for building in BUILDINGS:
		var building_rect: Rect2 = building.rect
		draw_rect(building_rect.grow(18.0), Color("#829299", 0.24))
		draw_rect(building_rect, building.fill)
		draw_rect(building_rect, Color("#30444C"), false, 4.0, true)
		var window_size := Vector2(26.0, 18.0)
		for x in range(int(building_rect.position.x + 32.0), int(building_rect.end.x - 16.0), 52):
			for y in range(int(building_rect.position.y + 34.0), int(building_rect.end.y - 14.0), 42):
				draw_rect(Rect2(Vector2(x, y), window_size), building.accent)
		draw_string(ThemeDB.fallback_font, building_rect.position + Vector2(12.0, -14.0), building.name, HORIZONTAL_ALIGNMENT_LEFT, -1, 15, Color("#25353C"))


func draw_landmarks() -> void:
	var center := Vector2(0, 0)
	draw_circle(center, 150.0, Color("#F7E3A2"))
	draw_circle(center, 112.0, Color("#DEA952"))
	draw_circle(center, 48.0, Color("#304F5D"))
	draw_line(center + Vector2(-190, 0), center + Vector2(190, 0), Color("#304F5D"), 8.0, true)
	draw_line(center + Vector2(0, -190), center + Vector2(0, 190), Color("#304F5D"), 8.0, true)
	draw_string(ThemeDB.fallback_font, center + Vector2(-102.0, 230.0), "CENTRAL PLAZA", HORIZONTAL_ALIGNMENT_LEFT, -1, 20, Color("#4B4032"))

	var beacon := Vector2(1750, -80)
	draw_circle(beacon, 96.0, Color("#E38572"))
	draw_circle(beacon, 58.0, Color("#F7D7A8"))
	draw_line(beacon + Vector2(0, -250), beacon + Vector2(0, 250), Color("#713F45"), 18.0, true)
	draw_string(ThemeDB.fallback_font, beacon + Vector2(-100.0, 300.0), "SIGNAL TOWER", HORIZONTAL_ALIGNMENT_LEFT, -1, 18, Color("#713F45"))


func draw_scale_markers() -> void:
	for coordinate in range(-3000, 3001, 500):
		draw_string(ThemeDB.fallback_font, Vector2(coordinate + 12.0, 28.0), str(coordinate), HORIZONTAL_ALIGNMENT_LEFT, -1, 14, Color("#5D7079"))
		draw_string(ThemeDB.fallback_font, Vector2(12.0, coordinate - 8.0), str(coordinate), HORIZONTAL_ALIGNMENT_LEFT, -1, 14, Color("#5D7079"))
