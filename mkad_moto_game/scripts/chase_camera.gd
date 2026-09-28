extends Node3D
## ChaseCamera — плавная камера от третьего лица, следующая за мотоциклом.
## ЛКМ + движение мыши — довернуть камеру вокруг байка.
## C — переключение между дальним и ближним планом.

@onready var camera: Camera3D = Camera3D.new()

const DISTANCE_FAR: float = 6.4
const DISTANCE_NEAR: float = 3.6
const HEIGHT_FAR: float = 2.5
const HEIGHT_NEAR: float = 1.5
const LOOK_HEIGHT: float = 1.0
const FOLLOW_SPEED: float = 5.0

var yaw_offset: float = 0.0
var near_mode: bool = false
var _current_pos: Vector3
var _initialised: bool = false

func _ready() -> void:
	add_child(camera)
	camera.current = true
	camera.fov = 72
	camera.near = 0.05
	camera.far = 3000.0

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion and Input.is_action_pressed("camera_look"):
		yaw_offset -= event.relative.x * 0.006
		yaw_offset = clamp(yaw_offset, -2.2, 2.2)
	if Input.is_action_just_pressed("toggle_camera"):
		near_mode = not near_mode

func _process(delta: float) -> void:
	var target: Node3D = Game.player_node
	if target == null:
		return

	var dist := DISTANCE_NEAR if near_mode else DISTANCE_FAR
	var hgt := HEIGHT_NEAR if near_mode else HEIGHT_FAR

	var back: Vector3 = target.global_transform.basis.z.normalized()
	if yaw_offset != 0.0:
		back = Basis(Vector3.UP, yaw_offset) * back

	# Плавно возвращаем камеру за спину байка, когда мышь отпущена.
	if not Input.is_action_pressed("camera_look"):
		yaw_offset = lerp(yaw_offset, 0.0, clamp(delta * 2.0, 0.0, 1.0))

	var desired_pos: Vector3 = target.global_position + back * dist + Vector3.UP * hgt

	if not _initialised:
		_current_pos = desired_pos
		_initialised = true
	else:
		_current_pos = _current_pos.lerp(desired_pos, clamp(delta * FOLLOW_SPEED, 0.0, 1.0))

	if _current_pos.y < target.global_position.y + 0.4:
		_current_pos.y = target.global_position.y + 0.4

	global_position = _current_pos
	var look_target: Vector3 = target.global_position + Vector3.UP * LOOK_HEIGHT
	look_at(look_target, Vector3.UP)
