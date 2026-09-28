extends CharacterBody3D
## TrafficCar — попутная/встречная машина, едущая по кольцу МКАД.
## Движение задаётся по окружности (угол + радиус полосы), визуал — одна из
## моделей набора Kenney Car Kit (реальная детализированная геометрия и
## текстуры, а не примитивный кубик).

var theta: float = 0.0
var lane_radius: float = 300.0
var direction: int = 1
var base_speed_kmh: float = 90.0
var _phase: float = 0.0

func _ready() -> void:
	add_to_group("traffic")
	_phase = randf() * TAU
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(1.9, 1.5, 4.3)
	shape.shape = box
	shape.position = Vector3(0, 0.75, 0)
	add_child(shape)

func setup(car_scene_path: String, start_theta: float, radius: float, dir: int, speed_kmh: float, model_scale: float) -> void:
	theta = start_theta
	lane_radius = radius
	direction = dir
	base_speed_kmh = speed_kmh
	_place(0.0)
	_load_visual(car_scene_path, model_scale)

func _load_visual(path: String, model_scale: float) -> void:
	var packed: PackedScene = load(path)
	if packed == null:
		return
	var inst: Node3D = packed.instantiate()
	inst.scale = Vector3.ONE * model_scale
	inst.rotation_degrees = Vector3(0, 180, 0)   # модель смотрит по +Z, разворачиваем на -Z (вперёд для CharacterBody3D)
	add_child(inst)

func _physics_process(delta: float) -> void:
	var speed_kmh: float = base_speed_kmh + sin(Time.get_ticks_msec() * 0.0004 + _phase) * 6.0
	var speed_ms := speed_kmh / 3.6
	var angular_speed := speed_ms / lane_radius
	theta += direction * angular_speed * delta
	_place(delta)

func _place(_delta: float) -> void:
	var pos := Game.ring_point(theta, lane_radius)
	pos.y = 0.0
	global_position = pos
	var tangent := Game.ring_tangent(theta, direction)
	look_at(global_position + tangent, Vector3.UP)
