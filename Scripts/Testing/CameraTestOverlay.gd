extends CanvasLayer

const REFRESH_INTERVAL := 0.25

@onready var _camera: Camera2D = get_parent().get_node("Camera2D")
@onready var _zoom_label: Label = get_node("Panel/Margin/Rows/Zoom")
@onready var _position_label: Label = get_node("Panel/Margin/Rows/Position")
@onready var _performance_label: Label = get_node("Panel/Margin/Rows/Performance")

var _elapsed := 0.0
var _frame_total := 0.0
var _frame_count := 0


func _ready() -> void:
	update_readout(0.0)


func _process(delta: float) -> void:
	_elapsed += delta
	_frame_total += delta
	_frame_count += 1
	if _elapsed < REFRESH_INTERVAL:
		return

	update_readout(_frame_total / float(_frame_count))
	_elapsed = 0.0
	_frame_total = 0.0
	_frame_count = 0


func update_readout(average_delta: float) -> void:
	var target_zoom := float(_camera.get("defaultScale"))
	_zoom_label.text = "ZOOM  %.3f    TARGET  %.3f" % [_camera.zoom.x, target_zoom]
	_position_label.text = "POSITION  %+.0f, %+.0f" % [_camera.position.x, _camera.position.y]
	var frame_ms := average_delta * 1000.0
	_performance_label.text = "FPS  %3d    AVG FRAME  %5.2f ms" % [Engine.get_frames_per_second(), frame_ms]
