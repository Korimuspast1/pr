extends CharacterBody3D
## Motorcycle — управляемый игроком мотоцикл.
## Аркадная модель движения (не полноценная физика колёс, но с ускорением,
## торможением, инерцией в поворотах и наклоном корпуса) + процедурная,
## полностью "не кубическая" 3D-модель байка, собранная из цилиндров,
## сфер и капсул прямо в коде.

signal crashed_into_traffic

const MAX_SPEED_KMH: float = 190.0
const REVERSE_MAX_KMH: float = 18.0
const ACCEL: float = 9.5
const BRAKE_DECEL: float = 18.0
const ENGINE_BRAKE: float = 3.2
const HANDBRAKE_DECEL: float = 26.0
const OFFROAD_DRAG: float = 6.0

const WHEEL_RADIUS: float = 0.33
const WHEELBASE: float = 1.42

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
	box.size = Vector3(0.75, 1.1, 2.05)
	shape.shape = box
	shape.position = Vector3(0, 0.55, 0)
	add_child(shape)

# ---------------------------------------------------------------------------
# ПРОЦЕДУРНАЯ 3D-МОДЕЛЬ МОТОЦИКЛА
# ---------------------------------------------------------------------------
func _add_tube(parent: Node3D, from: Vector3, to: Vector3, radius: float, mat: Material) -> void:
	var length := from.distance_to(to)
	if length < 0.001:
		return
	var cyl := CylinderMesh.new()
	cyl.top_radius = radius
	cyl.bottom_radius = radius
	cyl.height = length
	cyl.radial_segments = 10
	var mi := MeshInstance3D.new()
	mi.mesh = cyl
	mi.material_override = mat

	var y_axis := (to - from).normalized()
	var up_ref := Vector3.UP
	if abs(y_axis.dot(Vector3.UP)) > 0.98:
		up_ref = Vector3.RIGHT
	var x_axis := y_axis.cross(up_ref).normalized()
	var z_axis := x_axis.cross(y_axis).normalized()
	mi.transform = Transform3D(Basis(x_axis, y_axis, z_axis), (from + to) * 0.5)
	parent.add_child(mi)

func _wheel(radius: float, width: float, mat_tire: Material, mat_rim: Material) -> Node3D:
	var root := Node3D.new()
	var tire := CylinderMesh.new()
	tire.top_radius = radius
	tire.bottom_radius = radius
	tire.height = width
	tire.radial_segments = 20
	var tire_mi := MeshInstance3D.new()
	tire_mi.mesh = tire
	tire_mi.material_override = mat_tire
	tire_mi.rotation_degrees = Vector3(0, 0, 90)
	root.add_child(tire_mi)

	var rim := CylinderMesh.new()
	rim.top_radius = radius * 0.55
	rim.bottom_radius = radius * 0.55
	rim.height = width + 0.02
	rim.radial_segments = 12
	var rim_mi := MeshInstance3D.new()
	rim_mi.mesh = rim
	rim_mi.material_override = mat_rim
	rim_mi.rotation_degrees = Vector3(0, 0, 90)
	root.add_child(rim_mi)
	return root

func _build_model() -> void:
	var mat_body := StandardMaterial3D.new()
	mat_body.albedo_color = Color(0.78, 0.08, 0.1)
	mat_body.metallic = 0.55
	mat_body.roughness = 0.28

	var mat_frame := StandardMaterial3D.new()
	mat_frame.albedo_color = Color(0.08, 0.08, 0.09)
	mat_frame.metallic = 0.7
	mat_frame.roughness = 0.35

	var mat_chrome := StandardMaterial3D.new()
	mat_chrome.albedo_color = Color(0.85, 0.86, 0.88)
	mat_chrome.metallic = 1.0
	mat_chrome.roughness = 0.12

	var mat_tire := StandardMaterial3D.new()
	mat_tire.albedo_color = Color(0.03, 0.03, 0.03)
	mat_tire.roughness = 0.9

	var mat_seat := StandardMaterial3D.new()
	mat_seat.albedo_color = Color(0.04, 0.04, 0.045)
	mat_seat.roughness = 0.8

	var mat_glass := StandardMaterial3D.new()
	mat_glass.albedo_color = Color(0.6, 0.75, 0.8, 0.35)
	mat_glass.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat_glass.metallic = 0.2
	mat_glass.roughness = 0.05

	var rear_axle := Vector3(0, WHEEL_RADIUS, -WHEELBASE * 0.5)
	var front_axle := Vector3(0, WHEEL_RADIUS, WHEELBASE * 0.5)
	var head_top := Vector3(0, 1.02, WHEELBASE * 0.42)
	var seat_pivot := Vector3(0, 0.86, -0.30)
	var swingarm_pivot := Vector3(0, 0.42, -0.28)
	var engine_center := Vector3(0, 0.5, 0.05)

	# --- Колёса ---
	rear_wheel_pivot = _wheel(WHEEL_RADIUS, 0.20, mat_tire, mat_chrome)
	rear_wheel_pivot.position = rear_axle
	model.add_child(rear_wheel_pivot)

	front_fork_pivot = Node3D.new()
	front_fork_pivot.position = front_axle
	model.add_child(front_fork_pivot)

	front_wheel_roll = _wheel(WHEEL_RADIUS, 0.16, mat_tire, mat_chrome)
	front_fork_pivot.add_child(front_wheel_roll)

	# --- Рама (трубчатая) ---
	_add_tube(model, rear_axle, swingarm_pivot, 0.035, mat_frame)
	_add_tube(model, swingarm_pivot, engine_center, 0.045, mat_frame)
	_add_tube(model, engine_center, head_top, 0.045, mat_frame)
	_add_tube(model, seat_pivot, head_top, 0.04, mat_frame)
	_add_tube(model, seat_pivot, swingarm_pivot, 0.04, mat_frame)
	_add_tube(model, head_top, front_axle, 0.05, mat_chrome)   # передняя вилка
	_add_tube(model, head_top + Vector3(0.09, 0, 0), front_axle + Vector3(0.09, 0, 0), 0.035, mat_chrome)
	_add_tube(model, head_top - Vector3(0.09, 0, 0), front_axle - Vector3(0.09, 0, 0), 0.035, mat_chrome)

	# --- Двигатель ---
	var engine := MeshInstance3D.new()
	var eng_box := BoxMesh.new()
	eng_box.size = Vector3(0.34, 0.32, 0.42)
	engine.mesh = eng_box
	engine.material_override = mat_frame
	engine.position = engine_center
	model.add_child(engine)

	# --- Бензобак ---
	var tank := MeshInstance3D.new()
	var tank_mesh := CapsuleMesh.new()
	tank_mesh.radius = 0.19
	tank_mesh.height = 0.62
	tank.mesh = tank_mesh
	tank.material_override = mat_body
	tank.rotation_degrees = Vector3(90, 0, 0)
	tank.position = Vector3(0, 0.86, 0.18)
	tank.scale = Vector3(1.0, 1.0, 1.25)
	model.add_child(tank)

	# --- Сиденье ---
	var seat := MeshInstance3D.new()
	var seat_mesh := CapsuleMesh.new()
	seat_mesh.radius = 0.16
	seat_mesh.height = 0.6
	seat.mesh = seat_mesh
	seat.material_override = mat_seat
	seat.rotation_degrees = Vector3(90, 0, 0)
	seat.position = Vector3(0, 0.80, -0.42)
	seat.scale = Vector3(0.95, 1.0, 1.35)
	model.add_child(seat)

	# --- Хвост/крыло заднее ---
	var tail := MeshInstance3D.new()
	var tail_mesh := BoxMesh.new()
	tail_mesh.size = Vector3(0.22, 0.12, 0.4)
	tail.mesh = tail_mesh
	tail.material_override = mat_body
	tail.position = Vector3(0, 0.86, -0.72)
	tail.rotation_degrees = Vector3(-8, 0, 0)
	model.add_child(tail)

	# --- Фара ---
	var lamp := MeshInstance3D.new()
	var lamp_mesh := SphereMesh.new()
	lamp_mesh.radius = 0.11
	lamp_mesh.height = 0.2
	lamp.mesh = lamp_mesh
	lamp.material_override = mat_chrome
	lamp.position = Vector3(0, 0.95, WHEELBASE * 0.52)
	model.add_child(lamp)

	headlight = SpotLight3D.new()
	headlight.position = Vector3(0, 0.95, WHEELBASE * 0.54)
	headlight.rotation_degrees = Vector3(-8, 180, 0)
	headlight.spot_range = 45.0
	headlight.spot_angle = 32.0
	headlight.light_energy = 3.0
	model.add_child(headlight)

	# --- Ветровое стекло ---
	var screen := MeshInstance3D.new()
	var screen_mesh := BoxMesh.new()
	screen_mesh.size = Vector3(0.36, 0.28, 0.02)
	screen.mesh = screen_mesh
	screen.material_override = mat_glass
	screen.position = Vector3(0, 1.12, WHEELBASE * 0.46)
	screen.rotation_degrees = Vector3(-24, 0, 0)
	model.add_child(screen)

	# --- Руль ---
	var bar_y := 1.02
	_add_tube(model, Vector3(-0.24, bar_y, front_axle.z - 0.02), Vector3(0.24, bar_y, front_axle.z - 0.02), 0.02, mat_chrome)
	_add_tube(model, Vector3(0, 0.92, front_axle.z - 0.02), Vector3(0, bar_y, front_axle.z - 0.02), 0.025, mat_chrome)
	for side in [-1.0, 1.0]:
		var grip := MeshInstance3D.new()
		var grip_mesh := CylinderMesh.new()
		grip_mesh.top_radius = 0.022
		grip_mesh.bottom_radius = 0.022
		grip_mesh.height = 0.11
		grip.mesh = grip_mesh
		grip.material_override = mat_seat
		grip.rotation_degrees = Vector3(0, 0, 90)
		grip.position = Vector3(side * 0.29, bar_y, front_axle.z - 0.02)
		model.add_child(grip)

	# --- Глушитель ---
	_add_tube(model, Vector3(0.16, 0.42, -0.05), Vector3(0.22, 0.30, -0.95), 0.065, mat_chrome)

	# --- Задний фонарь ---
	var mat_tail_light := StandardMaterial3D.new()
	mat_tail_light.albedo_color = Color(0.9, 0.05, 0.05)
	mat_tail_light.emission_enabled = true
	mat_tail_light.emission = Color(1.0, 0.1, 0.1)
	mat_tail_light.emission_energy_multiplier = 2.0
	var tail_lamp := MeshInstance3D.new()
	var tail_lamp_mesh := BoxMesh.new()
	tail_lamp_mesh.size = Vector3(0.12, 0.06, 0.04)
	tail_lamp.mesh = tail_lamp_mesh
	tail_lamp.material_override = mat_tail_light
	tail_lamp.position = Vector3(0, 0.83, -0.92)
	model.add_child(tail_lamp)

	taillight = OmniLight3D.new()
	taillight.position = Vector3(0, 0.83, -0.95)
	taillight.light_color = Color(1, 0.2, 0.2)
	taillight.omni_range = 2.0
	taillight.light_energy = 0.6
	model.add_child(taillight)

	_build_rider(mat_frame, mat_seat)

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
	torso.position = Vector3(0, 1.08, -0.36)
	torso.rotation_degrees = Vector3(28, 0, 0)
	rider.add_child(torso)

	var head := MeshInstance3D.new()
	var head_mesh := SphereMesh.new()
	head_mesh.radius = 0.135
	head_mesh.height = 0.27
	head.mesh = head_mesh
	head.material_override = mat_helmet_glossy
	head.position = Vector3(0, 1.42, -0.18)
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
	visor.position = Vector3(0, 1.40, -0.045)
	rider.add_child(visor)

	for side in [-1.0, 1.0]:
		var arm := MeshInstance3D.new()
		var arm_mesh := CapsuleMesh.new()
		arm_mesh.radius = 0.045
		arm_mesh.height = 0.42
		arm.mesh = arm_mesh
		arm.material_override = mat_suit
		arm.position = Vector3(side * 0.22, 1.02, 0.05)
		arm.rotation_degrees = Vector3(60, 0, side * 12.0)
		rider.add_child(arm)

		var leg := MeshInstance3D.new()
		var leg_mesh := CapsuleMesh.new()
		leg_mesh.radius = 0.06
		leg_mesh.height = 0.5
		leg.mesh = leg_mesh
		leg.material_override = mat_suit
		leg.position = Vector3(side * 0.16, 0.62, -0.32)
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

	var roll_delta := (speed * delta) / WHEEL_RADIUS
	rear_wheel_pivot.rotate_x(roll_delta)
	front_wheel_roll.rotate_x(roll_delta)
	var target_steer_angle: float = clamp(-steer_input, -1.0, 1.0) * deg_to_rad(28.0)
	front_fork_pivot.rotation.y = lerp(front_fork_pivot.rotation.y, target_steer_angle, clamp(delta * 8.0, 0.0, 1.0))

	taillight.light_energy = 1.6 if Input.get_action_strength("brake") > 0.1 or Input.is_action_pressed("handbrake") else 0.4

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
