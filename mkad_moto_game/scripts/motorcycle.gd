extends CharacterBody3D
## Motorcycle — управляемый игроком мотоцикл.
## Аркадная модель движения (не полноценная физика колёс, но с ускорением,
## торможением, инерцией в поворотах и наклоном корпуса).
## 3D-модель байка — готовый (не процедурный) GLB-меш "vehicle-motorcycle"
## из официального Kenney "Starter Kit: Racing" (CC0), см. CREDITS.md.
## Наездник поверх модели по-прежнему собран из капсул/сфер вручную —
## в комплекте Kenney фигуры райдера нет.

signal crashed_into_traffic

const MAX_SPEED_KMH: float = 190.0
const REVERSE_MAX_KMH: float = 18.0
const ACCEL: float = 9.5
const BRAKE_DECEL: float = 18.0
const ENGINE_BRAKE: float = 3.2
const HANDBRAKE_DECEL: float = 26.0
const OFFROAD_DRAG: float = 6.0

const MOTO_SCENE_PATH: String = "res://assets/kenney/moto/vehicle-motorcycle.glb"
const ENGINE_SOUND_PATH: String = "res://assets/audio/engine-motorcycle.ogg"
const SKID_SOUND_PATH: String = "res://assets/audio/skid.ogg"
const IMPACT_SOUND_PATH: String = "res://assets/audio/impact.ogg"
const WHEEL_RADIUS: float = 0.3
const WHEELBASE: float = 1.514

var speed: float = 0.0            # м/с, со знаком (вперёд +)
var steer_input: float = 0.0
var lean_visual: float = 0.0
var total_theta: float = 0.0
var _last_theta: float = 0.0
var _theta_initialised := false

@onready var model: Node3D = Node3D.new()
var rear_wheel_pivot: Node3D
var front_fork_pivot: Node3D
var front_wheel_roll: Node3D
var headlight: SpotLight3D
var taillight: OmniLight3D
var engine_sound: AudioStreamPlayer3D
var skid_sound: AudioStreamPlayer3D
var impact_sound: AudioStreamPlayer3D
var _impact_cooldown: float = 0.0

func _ready() -> void:
	add_to_group("player")
	_build_collision()
	add_child(model)
	model.name = "Model"
	_build_model()
	Game.player_node = self
	Game.reset_player_stats()
	_last_theta = Game.angle_of(global_position)

func _build_collision() -> void:
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(0.85, 1.05, 2.3)
	shape.shape = box
	shape.position = Vector3(0, 0.5, 0.05)
	add_child(shape)

# ---------------------------------------------------------------------------
# 3D-МОДЕЛЬ МОТОЦИКЛА (готовый GLB-ассет Kenney, CC0) + процедурный наездник
# ---------------------------------------------------------------------------
func _build_model() -> void:
	var mat_seat := StandardMaterial3D.new()
	mat_seat.albedo_color = Color(0.04, 0.04, 0.045)
	mat_seat.roughness = 0.8

	var mat_suit := StandardMaterial3D.new()
	mat_suit.albedo_color = Color(0.08, 0.08, 0.09)
	mat_suit.metallic = 0.15
	mat_suit.roughness = 0.5

	# --- Готовая модель байка Kenney "Starter Kit: Racing" (CC0) ---
	var moto_scene: PackedScene = load(MOTO_SCENE_PATH)
	var moto_instance: Node3D = moto_scene.instantiate()
	model.add_child(moto_instance)
	# Исходная модель "смотрит" в сторону +Z, а вперёд для CharacterBody3D — это
	# -Z, поэтому разворачиваем модель на 180°, чтобы фара/вилка были спереди.
	moto_instance.rotation_degrees.y = 180.0

	rear_wheel_pivot = moto_instance.get_node("wheel-back")
	front_wheel_roll = moto_instance.get_node("wheel-front")
	var body_node: Node3D = moto_instance.get_node("body")
	front_fork_pivot = body_node.get_node("fork")

	# --- Функциональные источники света (фара / стоп-сигнал) ---
	headlight = SpotLight3D.new()
	headlight.position = Vector3(0, 0.85, 1.25)
	headlight.rotation_degrees = Vector3(-8, 180, 0)
	headlight.spot_range = 45.0
	headlight.spot_angle = 32.0
	headlight.light_energy = 3.0
	model.add_child(headlight)

	taillight = OmniLight3D.new()
	taillight.position = Vector3(0, 0.68, -1.05)
	taillight.light_color = Color(1, 0.2, 0.2)
	taillight.omni_range = 2.0
	taillight.light_energy = 0.6
	model.add_child(taillight)

	# --- Звук двигателя и юза (готовые CC0-сэмплы Kenney) ---
	engine_sound = AudioStreamPlayer3D.new()
	engine_sound.stream = load(ENGINE_SOUND_PATH)
	engine_sound.unit_size = 8.0
	engine_sound.autoplay = true
	engine_sound.volume_db = -15.0
	model.add_child(engine_sound)

	skid_sound = AudioStreamPlayer3D.new()
	skid_sound.stream = load(SKID_SOUND_PATH)
	skid_sound.unit_size = 6.0
	skid_sound.volume_db = -80.0
	model.add_child(skid_sound)

	impact_sound = AudioStreamPlayer3D.new()
	impact_sound.stream = load(IMPACT_SOUND_PATH)
	impact_sound.unit_size = 8.0
	model.add_child(impact_sound)

	# В комплекте Kenney нет фигуры райдера — добавляем процедурного
	# наездника поверх готовой модели байка.
	_build_rider(mat_suit, mat_seat)

func _build_rider(mat_suit: Material, mat_helmet: Material) -> void:
	var mat_skin := StandardMaterial3D.new()
	mat_skin.albedo_color = Color(0.82, 0.66, 0.55)

	var mat_helmet_glossy := StandardMaterial3D.new()
	mat_helmet_glossy.albedo_color = Color(0.08, 0.08, 0.1)
	mat_helmet_glossy.metallic = 0.4
	mat_helmet_glossy.roughness = 0.25

	var rider := Node3D.new()
	rider.position = Vector3(0, 0, 0)
	model.add_child(rider)

	var torso := MeshInstance3D.new()
	var torso_mesh := CapsuleMesh.new()
	torso_mesh.radius = 0.15
	torso_mesh.height = 0.62
	torso.mesh = torso_mesh
	torso.material_override = mat_suit
	torso.position = Vector3(0, 0.98, -0.34)
	torso.rotation_degrees = Vector3(28, 0, 0)
	rider.add_child(torso)

	var head := MeshInstance3D.new()
	var head_mesh := SphereMesh.new()
	head_mesh.radius = 0.135
	head_mesh.height = 0.27
	head.mesh = head_mesh
	head.material_override = mat_helmet_glossy
	head.position = Vector3(0, 1.33, -0.14)
	rider.add_child(head)

	var visor := MeshInstance3D.new()
	var visor_mesh := BoxMesh.new()
	visor_mesh.size = Vector3(0.19, 0.08, 0.03)
	visor.mesh = visor_mesh
	var mat_visor := StandardMaterial3D.new()
	mat_visor.albedo_color = Color(0.1, 0.15, 0.2)
	mat_visor.metallic = 0.8
	mat_visor.roughness = 0.1
	visor.material_override = mat_visor
	visor.position = Vector3(0, 1.31, -0.01)
	rider.add_child(visor)

	for side in [-1.0, 1.0]:
		var arm := MeshInstance3D.new()
		var arm_mesh := CapsuleMesh.new()
		arm_mesh.radius = 0.045
		arm_mesh.height = 0.42
		arm.mesh = arm_mesh
		arm.material_override = mat_suit
		arm.position = Vector3(side * 0.22, 0.98, 0.28)
		arm.rotation_degrees = Vector3(58, 0, side * 12.0)
		rider.add_child(arm)

		var leg := MeshInstance3D.new()
		var leg_mesh := CapsuleMesh.new()
		leg_mesh.radius = 0.06
		leg_mesh.height = 0.5
		leg.mesh = leg_mesh
		leg.material_override = mat_suit
		leg.position = Vector3(side * 0.18, 0.56, -0.18)
		leg.rotation_degrees = Vector3(0, 0, side * 18.0)
		rider.add_child(leg)

# ---------------------------------------------------------------------------
# ФИЗИКА / УПРАВЛЕНИЕ
# ---------------------------------------------------------------------------
func _physics_process(delta: float) -> void:
	_handle_input(delta)
	_integrate_motion(delta)
	_apply_ring_constraints()
	_snap_to_ground()
	_update_visuals(delta)
	_update_telemetry(delta)

func _snap_to_ground() -> void:
	var space_state := get_world_3d().direct_space_state
	var from := global_position + Vector3.UP * 3.0
	var to := global_position - Vector3.UP * 5.0
	var params := PhysicsRayQueryParameters3D.create(from, to)
	params.exclude = [self.get_rid()]
	var result := space_state.intersect_ray(params)
	if result.has("position"):
		var gp := global_position
		gp.y = result.position.y
		global_position = gp

func _handle_input(delta: float) -> void:
	var throttle := Input.get_action_strength("throttle") - Input.get_action_strength("brake")
	steer_input = Input.get_action_strength("steer_right") - Input.get_action_strength("steer_left")
	var handbrake := Input.is_action_pressed("handbrake")

	if Input.is_action_just_pressed("reset_bike"):
		_reset_to_road()
		return

	if throttle > 0.01:
		speed += throttle * ACCEL * delta
	elif throttle < -0.01:
		if speed > 0.3:
			speed += throttle * BRAKE_DECEL * delta
		else:
			speed += throttle * ACCEL * 0.55 * delta
	else:
		speed = move_toward(speed, 0.0, ENGINE_BRAKE * delta)

	if handbrake:
		speed = move_toward(speed, 0.0, HANDBRAKE_DECEL * delta)

	# Сопротивление вне асфальта (обочина/поле) — мягкое ограничение трассы.
	var r := Game.radius_of(global_position)
	if r > Game.OUTER_EDGE or r < Game.INNER_EDGE:
		speed = move_toward(speed, 0.0, OFFROAD_DRAG * delta)

	speed = clamp(speed, -REVERSE_MAX_KMH / 3.6, MAX_SPEED_KMH / 3.6)

func _integrate_motion(delta: float) -> void:
	var speed_ratio: float = clamp(abs(speed) / 6.0, 0.12, 1.0)
	var high_speed_damp: float = clamp(1.0 - (abs(speed) / 55.0) * 0.35, 0.55, 1.0)
	var turn_rate := deg_to_rad(95.0) * speed_ratio * high_speed_damp
	var dir_sign := 1.0 if speed >= 0.0 else -1.0
	rotation.y -= steer_input * turn_rate * delta * dir_sign

	var forward := -global_transform.basis.z
	velocity = forward * speed
	move_and_slide()
	_check_traffic_collision()

func _check_traffic_collision() -> void:
	_impact_cooldown = max(_impact_cooldown - get_physics_process_delta_time(), 0.0)
	if _impact_cooldown > 0.0:
		return
	for i in get_slide_collision_count():
		var collision := get_slide_collision(i)
		var other := collision.get_collider()
		if other is Node and (other as Node).is_in_group("traffic"):
			var impact_speed: float = abs(speed)
			impact_sound.volume_db = clamp(remap(impact_speed, 0.0, 20.0, -20.0, 0.0), -20.0, 0.0)
			impact_sound.play()
			speed *= 0.4
			_impact_cooldown = 0.6
			crashed_into_traffic.emit()
			break

func _apply_ring_constraints() -> void:
	var p := global_position
	var r := Vector2(p.x, p.z).length()
	if r < 0.001:
		return
	var dir2 := Vector2(p.x, p.z) / r

	var median_inner := Game.RING_RADIUS - Game.MEDIAN_HALF_WIDTH - 0.4
	var median_outer := Game.RING_RADIUS + Game.MEDIAN_HALF_WIDTH + 0.4
	if r > median_inner and r < median_outer:
		var push_r: float = median_inner if (r - median_inner) < (median_outer - r) else median_outer
		p.x = dir2.x * push_r
		p.z = dir2.y * push_r
		global_position = p
		speed *= 0.85

	var hard_outer := Game.OUTER_EDGE + Game.HARD_WALL_MARGIN
	var hard_inner: float = max(Game.INNER_EDGE - Game.HARD_WALL_MARGIN, 5.0)
	if r > hard_outer:
		p.x = dir2.x * hard_outer
		p.z = dir2.y * hard_outer
		global_position = p
		speed *= 0.6
	elif r < hard_inner:
		p.x = dir2.x * hard_inner
		p.z = dir2.y * hard_inner
		global_position = p
		speed *= 0.6

func _update_visuals(delta: float) -> void:
	var target_lean: float = clamp(-steer_input * clamp(abs(speed) / 14.0, 0.0, 1.0), -1.0, 1.0)
	lean_visual = lerp(lean_visual, target_lean, clamp(delta * 6.0, 0.0, 1.0))
	model.rotation.z = lean_visual * deg_to_rad(28.0)

	# Качение колёс — независимо крутим Euler X, не трогая basis напрямую,
	# чтобы не конфликтовать с рулением по Y (см. wheel-front ниже).
	var roll_delta := (speed * delta) / WHEEL_RADIUS
	rear_wheel_pivot.rotation.x += roll_delta
	front_wheel_roll.rotation.x += roll_delta

	# Руление: синхронно поворачиваем вилку (fork) и переднее колесо по Y.
	var target_steer_angle: float = clamp(-steer_input, -1.0, 1.0) * deg_to_rad(28.0)
	front_fork_pivot.rotation.y = lerp_angle(front_fork_pivot.rotation.y, target_steer_angle, clamp(delta * 8.0, 0.0, 1.0))
	front_wheel_roll.rotation.y = lerp_angle(front_wheel_roll.rotation.y, target_steer_angle, clamp(delta * 8.0, 0.0, 1.0))

	taillight.light_energy = 1.6 if Input.get_action_strength("brake") > 0.1 or Input.is_action_pressed("handbrake") else 0.4

	_update_audio(delta)

func _update_audio(delta: float) -> void:
	var throttle := Input.get_action_strength("throttle")
	var speed_ratio: float = clamp(abs(speed) / (MAX_SPEED_KMH / 3.6), 0.0, 1.0)

	if not engine_sound.playing:
		engine_sound.play()
	var target_pitch: float = 0.7 + speed_ratio * 1.6 + throttle * 0.25
	engine_sound.pitch_scale = lerp(engine_sound.pitch_scale, target_pitch, clamp(delta * 3.0, 0.0, 1.0))
	engine_sound.volume_db = lerp(-24.0, -6.0, clamp(speed_ratio + throttle * 0.4, 0.0, 1.0))

	var skidding: bool = Input.is_action_pressed("handbrake") or (abs(steer_input) > 0.5 and abs(speed) > 8.0)
	if skidding:
		if not skid_sound.playing:
			skid_sound.play()
		skid_sound.volume_db = lerp(skid_sound.volume_db, -6.0, clamp(delta * 6.0, 0.0, 1.0))
	else:
		skid_sound.volume_db = lerp(skid_sound.volume_db, -80.0, clamp(delta * 4.0, 0.0, 1.0))

func _update_telemetry(delta: float) -> void:
	Game.report_speed(abs(speed) * 3.6)
	Game.player_distance_m += abs(speed) * delta

	var theta := Game.angle_of(global_position)
	if _theta_initialised:
		var d := wrapf(theta - _last_theta, -PI, PI)
		total_theta += d
		var laps := int(abs(total_theta) / TAU)
		if laps > Game.player_laps:
			Game.report_lap()
	else:
		_theta_initialised = true
	_last_theta = theta
	Game.player_theta = theta

func _reset_to_road() -> void:
	var theta := Game.angle_of(global_position)
	var pos := Game.ring_point(theta, Game.RING_RADIUS + Game.LANE_WIDTH)
	pos.y = 0.06
	global_position = pos
	var tangent := Game.ring_tangent(theta, 1)
	rotation = Vector3.ZERO
	look_at(global_position + tangent, Vector3.UP)
	speed = 0.0
	velocity = Vector3.ZERO
