extends Node3D
## Main — корневой скрипт уровня. Строит окружение (небо, свет, туман),
## трассу, спавнит игрока-мотоциклиста, трафик и HUD. Вся сцена собирается
## кодом, чтобы не зависеть от хрупких ручных .tscn-структур.

const MOTORCYCLE_SCENE := "res://scenes/Motorcycle.tscn"
const TRAFFIC_CAR_SCENE := "res://scenes/TrafficCar.tscn"

const CAR_SCENES := [
	"res://assets/kenney/car/sedan.glb",
	"res://assets/kenney/car/sedan-sports.glb",
	"res://assets/kenney/car/suv.glb",
	"res://assets/kenney/car/suv-luxury.glb",
	"res://assets/kenney/car/hatchback-sports.glb",
	"res://assets/kenney/car/taxi.glb",
	"res://assets/kenney/car/van.glb",
	"res://assets/kenney/car/delivery.glb",
]
const CAR_MODEL_SCALE := 1.7

var _rng := RandomNumberGenerator.new()
var hud: CanvasLayer

func _ready() -> void:
	_rng.randomize()
	_build_environment()
	_build_road()
	var player := _spawn_player()
	_spawn_camera(player)
	_spawn_traffic()
	_build_hud()

# ---------------------------------------------------------------------------
func _build_environment() -> void:
	var env := Environment.new()
	var sky_mat := ProceduralSkyMaterial.new()
	sky_mat.sky_top_color = Color(0.30, 0.48, 0.75)
	sky_mat.sky_horizon_color = Color(0.75, 0.80, 0.82)
	sky_mat.ground_bottom_color = Color(0.35, 0.35, 0.32)
	sky_mat.ground_horizon_color = Color(0.75, 0.80, 0.82)
	sky_mat.sun_angle_max = 30.0
	var sky := Sky.new()
	sky.sky_material = sky_mat
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.reflected_light_source = Environment.REFLECTION_SOURCE_SKY

	env.fog_enabled = true
	env.fog_light_color = Color(0.72, 0.77, 0.8)
	env.fog_density = 0.006
	env.fog_sky_affect = 0.4

	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.glow_enabled = true
	env.glow_intensity = 0.5
	env.glow_bloom = 0.05

	var world_env := WorldEnvironment.new()
	world_env.environment = env
	add_child(world_env)

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-48, -35, 0)
	sun.light_energy = 1.15
	sun.light_color = Color(1.0, 0.97, 0.9)
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 260.0
	add_child(sun)

func _build_road() -> void:
	var road := Node3D.new()
	road.name = "RoadBuilder"
	road.set_script(load("res://scripts/road_builder.gd"))
	add_child(road)

func _spawn_player() -> Node3D:
	var packed: PackedScene = load(MOTORCYCLE_SCENE)
	var bike = packed.instantiate()
	add_child(bike)
	var start_theta := -PI * 0.5
	var start_radius := Game.RING_RADIUS + Game.MEDIAN_HALF_WIDTH + 0.5 * Game.LANE_WIDTH
	var pos := Game.ring_point(start_theta, start_radius)
	pos.y = 0.06
	bike.global_position = pos
	var tangent := Game.ring_tangent(start_theta, 1)
	bike.look_at(bike.global_position + tangent, Vector3.UP)
	return bike

func _spawn_camera(_player: Node3D) -> void:
	var rig := Node3D.new()
	rig.name = "ChaseCamera"
	rig.set_script(load("res://scripts/chase_camera.gd"))
	add_child(rig)

func _spawn_traffic() -> void:
	# Внешняя сторона кольца (направление +1), полосы 1 и 2 (0 — полоса игрока).
	for lane in range(1, Game.LANES_PER_SIDE):
		var r := Game.RING_RADIUS + Game.MEDIAN_HALF_WIDTH + (lane + 0.5) * Game.LANE_WIDTH
		var base_speed := 100.0 - lane * 12.0
		_spawn_lane(r, 1, base_speed)

	# Внутренняя сторона кольца (встречное движение, направление -1), все полосы.
	for lane in range(0, Game.LANES_PER_SIDE):
		var r := Game.RING_RADIUS - Game.MEDIAN_HALF_WIDTH - (lane + 0.5) * Game.LANE_WIDTH
		var base_speed := 95.0 - lane * 10.0
		_spawn_lane(r, -1, base_speed)

func _spawn_lane(radius: float, direction: int, base_speed_kmh: float) -> void:
	var circumference := TAU * radius
	var spacing := 58.0
	var count: int = max(5, int(circumference / spacing))
	var packed: PackedScene = load(TRAFFIC_CAR_SCENE)
	for i in range(count):
		var theta := TAU * float(i) / float(count) + _rng.randf_range(-0.02, 0.02)
		var car = packed.instantiate()
		add_child(car)
		var car_scene: String = CAR_SCENES[_rng.randi_range(0, CAR_SCENES.size() - 1)]
		var speed_variation := _rng.randf_range(-6.0, 6.0)
		car.setup(car_scene, theta, radius, direction, base_speed_kmh + speed_variation, CAR_MODEL_SCALE)

func _build_hud() -> void:
	hud = CanvasLayer.new()
	hud.set_script(load("res://scripts/hud.gd"))
	add_child(hud)
