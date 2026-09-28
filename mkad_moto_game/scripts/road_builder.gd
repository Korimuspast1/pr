extends Node3D
## RoadBuilder — процедурно строит кольцевую трассу (МКАД), окружение,
## отбойники, освещение, рекламные щиты, здания и деревья.
## Ничего не хранится в .tscn — вся геометрия создаётся в коде при старте.

const SEGMENTS: int = 360          # сегментов на полный круг (сглаженность кольца)
const TILE_SIZE: float = 6.0       # размер одного тайла асфальта в метрах

const ASPHALT_TEX := preload("res://assets/textures/asphalt.jpg")
const GRASS_TEX := preload("res://assets/textures/grass.jpg")
const FACADE_TEX := preload("res://assets/textures/building_facade.jpg")
const BARK_TEX := preload("res://assets/textures/birch_bark.jpg")

const RAIL_SCENE := "res://assets/kenney/racing/rail.glb"
const LIGHT_SCENE := "res://assets/kenney/racing/lightPostModern.glb"
const BILLBOARD_SCENE := "res://assets/kenney/racing/billboard.glb"

var _rng := RandomNumberGenerator.new()

func _ready() -> void:
	_rng.seed = 20260928
	_build_ground()
	_build_road_surface()
	_build_lane_markings()
	_build_median()
	_build_guardrails()
	_build_streetlights()
	_build_billboards()
	_build_gantries()
	_build_distance_signs()
	_build_buildings()
	_build_trees()

# ---------------------------------------------------------------------------
# Земля / трава
# ---------------------------------------------------------------------------
func _build_ground() -> void:
	var size := (Game.OUTER_EDGE + 900.0) * 2.0
	var plane := PlaneMesh.new()
	plane.size = Vector2(size, size)
	plane.subdivide_width = 1
	plane.subdivide_depth = 1
	var uv_repeat := size / TILE_SIZE

	var mat := StandardMaterial3D.new()
	mat.albedo_texture = GRASS_TEX
	mat.roughness = 1.0
	mat.uv1_scale = Vector3(uv_repeat, uv_repeat, 1.0)

	var mi := MeshInstance3D.new()
	mi.mesh = plane
	mi.material_override = mat
	mi.position = Vector3(0, -0.03, 0)
	add_child(mi)

	var body := StaticBody3D.new()
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(size, 0.2, size)
	shape.shape = box
	shape.position = Vector3(0, -0.15, 0)  # ниже полотна дороги, чтобы не "перебивать" её коллайдер
	body.add_child(shape)
	mi.add_child(body)

# ---------------------------------------------------------------------------
# Универсальная плоская "лента" кольца (annulus) — используется для дорожного
# полотна и верхней плиты разделительного барьера.
# ---------------------------------------------------------------------------
func _build_ring_band(r_inner: float, r_outer: float, y: float, material: Material, tile: float) -> MeshInstance3D:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var width := r_outer - r_inner
	var v1 := width / tile
	for i in range(SEGMENTS):
		var t0 := TAU * float(i) / float(SEGMENTS)
		var t1 := TAU * float(i + 1) / float(SEGMENTS)
		var u0 := (t0 * r_outer) / tile
		var u1 := (t1 * r_outer) / tile

		var dir0 := Vector2(cos(t0), sin(t0))
		var dir1 := Vector2(cos(t1), sin(t1))

		var p_out0 := Vector3(dir0.x * r_outer, y, dir0.y * r_outer)
		var p_in0 := Vector3(dir0.x * r_inner, y, dir0.y * r_inner)
		var p_out1 := Vector3(dir1.x * r_outer, y, dir1.y * r_outer)
		var p_in1 := Vector3(dir1.x * r_inner, y, dir1.y * r_inner)

		# Треугольник 1: out0, in0, out1
		st.set_uv(Vector2(u0, 0.0)); st.set_normal(Vector3.UP); st.add_vertex(p_out0)
		st.set_uv(Vector2(u0, v1)); st.set_normal(Vector3.UP); st.add_vertex(p_in0)
		st.set_uv(Vector2(u1, 0.0)); st.set_normal(Vector3.UP); st.add_vertex(p_out1)
		# Треугольник 2: in0, in1, out1
		st.set_uv(Vector2(u0, v1)); st.set_normal(Vector3.UP); st.add_vertex(p_in0)
		st.set_uv(Vector2(u1, v1)); st.set_normal(Vector3.UP); st.add_vertex(p_in1)
		st.set_uv(Vector2(u1, 0.0)); st.set_normal(Vector3.UP); st.add_vertex(p_out1)

	var mesh := st.commit()
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.material_override = material
	add_child(mi)
	return mi

func _build_road_surface() -> void:
	var mat := StandardMaterial3D.new()
	mat.albedo_texture = ASPHALT_TEX
	mat.roughness = 0.95
	mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	var mi := _build_ring_band(Game.INNER_EDGE, Game.OUTER_EDGE, 0.05, mat, TILE_SIZE)

	# Коллизия дороги — просто широкий плоский StaticBody чуть выше уровня травы,
	# чтобы мотоцикл/машины ехали по её поверхности.
	var body := StaticBody3D.new()
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3((Game.OUTER_EDGE + 2.0) * 2.0, 0.2, (Game.OUTER_EDGE + 2.0) * 2.0)
	shape.shape = box
	shape.position = Vector3(0, -0.05, 0)
	body.add_child(shape)
	mi.add_child(body)

func _build_lane_markings() -> void:
	var white := StandardMaterial3D.new()
	white.albedo_color = Color(0.95, 0.95, 0.92)
	white.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED

	var yellow := StandardMaterial3D.new()
	yellow.albedo_color = Color(0.95, 0.78, 0.1)
	yellow.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED

	var dash_len := 4.0
	var gap_len := 5.0
	var dash_width := 0.18

	# Радиусы границ между полосами (кроме крайних — там сплошная линия).
	var radii: Array = []
	# Внешняя сторона (от центрального барьера наружу): границы между полосами
	for lane in range(1, Game.LANES_PER_SIDE):
		radii.append({"r": Game.RING_RADIUS + Game.MEDIAN_HALF_WIDTH + lane * Game.LANE_WIDTH, "dashed": true})
	for lane in range(1, Game.LANES_PER_SIDE):
		radii.append({"r": Game.RING_RADIUS - Game.MEDIAN_HALF_WIDTH - lane * Game.LANE_WIDTH, "dashed": true})
	# Сплошные линии обочины (край проезжей части)
	radii.append({"r": Game.OUTER_SHOULDER_START, "dashed": false})
	radii.append({"r": Game.INNER_SHOULDER_START, "dashed": false})

	var dash_mesh := BoxMesh.new()
	dash_mesh.size = Vector3(dash_width, 0.02, dash_len)

	for entry in radii:
		var r: float = entry.r
		var dashed: bool = entry.dashed
		var circumference := TAU * r
		var mm := MultiMesh.new()
		mm.mesh = dash_mesh
		mm.transform_format = MultiMesh.TRANSFORM_3D
		var step := dash_len + gap_len if dashed else dash_len * 0.999
		var count := int(circumference / step)
		mm.instance_count = max(count, 1)
		for i in range(mm.instance_count):
			var theta := TAU * float(i) / float(count)
			var pos := Vector3(cos(theta) * r, 0.08, sin(theta) * r)
			var tangent := Vector3(-sin(theta), 0.0, cos(theta))
			var basis := Basis.looking_at(tangent, Vector3.UP)
			mm.set_instance_transform(i, Transform3D(basis, pos))
		var mmi := MultiMeshInstance3D.new()
		mmi.multimesh = mm
		mmi.material_override = white if dashed else white
		add_child(mmi)

# ---------------------------------------------------------------------------
# Центральный разделитель (отбойник)
# ---------------------------------------------------------------------------
func _build_median() -> void:
	var barrier_h := 0.9
	var concrete := StandardMaterial3D.new()
	concrete.albedo_color = Color(0.82, 0.82, 0.8)
	concrete.roughness = 0.85

	var inner_r := Game.RING_RADIUS - Game.MEDIAN_HALF_WIDTH
	var outer_r := Game.RING_RADIUS + Game.MEDIAN_HALF_WIDTH

	for r in [inner_r, outer_r]:
		var cyl := CylinderMesh.new()
		cyl.top_radius = r
		cyl.bottom_radius = r
		cyl.height = barrier_h
		cyl.cap_top = false
		cyl.cap_bottom = false
		cyl.radial_segments = SEGMENTS
		var mi := MeshInstance3D.new()
		mi.mesh = cyl
		mi.material_override = concrete
		mi.position = Vector3(0, barrier_h * 0.5, 0)
		add_child(mi)

	_build_ring_band(inner_r, outer_r, barrier_h, concrete, 3.0)
	# Физическое ограничение на среднюю разделительную полосу и края дороги
	# реализовано программно в scripts/motorcycle.gd и scripts/traffic_car.gd
	# (мягкое радиальное ограничение), чтобы не городить вогнутый коллайдер кольца.

# ---------------------------------------------------------------------------
# Отбойники по краям проезжей части (используем модель Kenney "rail")
# ---------------------------------------------------------------------------
func _find_meshinstance(node: Node) -> MeshInstance3D:
	if node is MeshInstance3D:
		return node
	for c in node.get_children():
		var r := _find_meshinstance(c)
		if r:
			return r
	return null

func _load_asset_mesh(path: String) -> Dictionary:
	var packed: PackedScene = load(path)
	if packed == null:
		return {}
	var inst: Node = packed.instantiate()
	var mi := _find_meshinstance(inst)
	var result := {}
	if mi and mi.mesh:
		result["mesh"] = mi.mesh
		result["xform"] = mi.global_transform
	inst.free()
	return result

func _build_guardrails() -> void:
	var data := _load_asset_mesh(RAIL_SCENE)
	if data.is_empty():
		return
	var mesh: Mesh = data["mesh"]
	var local_xform: Transform3D = data["xform"]
	var scale_factor := 6.0
	var spacing := 1.0 * scale_factor

	for r in [Game.OUTER_EDGE - 0.15, Game.INNER_EDGE + 0.15]:
		var circumference := TAU * r
		var count := int(circumference / spacing)
		var mm := MultiMesh.new()
		mm.mesh = mesh
		mm.transform_format = MultiMesh.TRANSFORM_3D
		mm.instance_count = count
		for i in range(count):
			var theta := TAU * float(i) / float(count)
			var pos := Vector3(cos(theta) * r, 0.0, sin(theta) * r)
			var tangent := Vector3(-sin(theta), 0.0, cos(theta))
			var place_basis := Basis.looking_at(tangent, Vector3.UP).scaled(Vector3.ONE * scale_factor)
			var place := Transform3D(place_basis, pos)
			mm.set_instance_transform(i, place * local_xform)
		var mmi := MultiMeshInstance3D.new()
		mmi.multimesh = mm
		add_child(mmi)

func _build_streetlights() -> void:
	var data := _load_asset_mesh(LIGHT_SCENE)
	if data.is_empty():
		return
	var mesh: Mesh = data["mesh"]
	var local_xform: Transform3D = data["xform"]
	var scale_factor := 5.5
	var r := Game.OUTER_EDGE + 1.2
	var spacing := 45.0
	var circumference := TAU * r
	var count := int(circumference / spacing)
	var mm := MultiMesh.new()
	mm.mesh = mesh
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.instance_count = count
	for i in range(count):
		var theta := TAU * float(i) / float(count)
		var pos := Vector3(cos(theta) * r, 0.0, sin(theta) * r)
		var inward := Vector3(-cos(theta), 0.0, -sin(theta))
		var place_basis := Basis.looking_at(inward, Vector3.UP).scaled(Vector3.ONE * scale_factor)
		var place := Transform3D(place_basis, pos)
		mm.set_instance_transform(i, place * local_xform)
	var mmi := MultiMeshInstance3D.new()
	mmi.multimesh = mm
	add_child(mmi)

	var light_mat := StandardMaterial3D.new()
	# Точечные фонари для ночной атмосферы (недорого — общий OmniLight с широким радиусом мог бы
	# быть тяжёлым при полусотне штук, поэтому используем только каждый 3-й фонарь).
	for i in range(0, count, 3):
		var theta := TAU * float(i) / float(count)
		var pos := Vector3(cos(theta) * r, 4.6, sin(theta) * r)
		var light := OmniLight3D.new()
		light.position = pos
		light.light_energy = 1.4
		light.omni_range = 14.0
		light.light_color = Color(1.0, 0.85, 0.55)
		add_child(light)

func _build_billboards() -> void:
	var packed: PackedScene = load(BILLBOARD_SCENE)
	if packed == null:
		return
	var count := 12
	var scale_factor := 5.0
	for i in range(count):
		var theta := TAU * float(i) / float(count) + _rng.randf_range(-0.05, 0.05)
		var r := Game.OUTER_EDGE + 12.0 + _rng.randf_range(0, 20.0)
		var inst = packed.instantiate()
		add_child(inst)
		var pos := Vector3(cos(theta) * r, 0.0, sin(theta) * r)
		var inward := (Vector3.ZERO - pos)
		inward.y = 0
		inst.global_transform = Transform3D(Basis.looking_at(inward.normalized(), Vector3.UP), pos).scaled_local(Vector3.ONE * scale_factor)

# ---------------------------------------------------------------------------
# Портальные рамки-указатели над дорогой (процедурные, без внешних моделей)
# ---------------------------------------------------------------------------
func _build_gantries() -> void:
	var pole_mat := StandardMaterial3D.new()
	pole_mat.albedo_color = Color(0.55, 0.57, 0.6)
	pole_mat.metallic = 0.6
	pole_mat.roughness = 0.4

	var sign_mat := StandardMaterial3D.new()
	sign_mat.albedo_color = Color(0.05, 0.35, 0.12)
	sign_mat.roughness = 0.7

	var gantry_count := 6
	var span := (Game.OUTER_EDGE - Game.INNER_EDGE) + 4.0
	var clearance := 6.2

	for g in range(gantry_count):
		var theta := TAU * float(g) / float(gantry_count) + 0.35
		var pos := Game.ring_point(theta, Game.RING_RADIUS)
		var tangent := Game.ring_tangent(theta, 1)
		var radial := Vector3(cos(theta), 0, sin(theta))

		var root := Node3D.new()
		root.position = pos
		root.transform.basis = Basis.looking_at(tangent, Vector3.UP)
		add_child(root)

		for side in [-1.0, 1.0]:
			var pole := MeshInstance3D.new()
			var cyl := CylinderMesh.new()
			cyl.top_radius = 0.18
			cyl.bottom_radius = 0.22
			cyl.height = clearance
			pole.mesh = cyl
			pole.material_override = pole_mat
			pole.position = Vector3(side * span * 0.5, clearance * 0.5, 0)
			root.add_child(pole)

		var beam := MeshInstance3D.new()
		var box := BoxMesh.new()
		box.size = Vector3(span, 0.35, 0.35)
		beam.mesh = box
		beam.material_override = pole_mat
		beam.position = Vector3(0, clearance, 0)
		root.add_child(beam)

		var sign := MeshInstance3D.new()
		var sign_box := BoxMesh.new()
		sign_box.size = Vector3(span * 0.55, 1.7, 0.08)
		sign.mesh = sign_box
		sign.material_override = sign_mat
		sign.position = Vector3(0, clearance - 1.0, 0.25)
		root.add_child(sign)

		var label := Label3D.new()
		label.text = "МКАД"
		label.font_size = 90
		label.outline_size = 0
		label.modulate = Color(1, 1, 1)
		label.position = Vector3(0, clearance - 1.0, 0.32)
		label.pixel_size = 0.01
		label.no_depth_test = false
		label.billboard = BaseMaterial3D.BILLBOARD_DISABLED
		root.add_child(label)

# ---------------------------------------------------------------------------
# Километровые указатели по периметру
# ---------------------------------------------------------------------------
func _build_distance_signs() -> void:
	var sign_count := 16
	var circumference := TAU * Game.RING_RADIUS
	for i in range(sign_count):
		var theta := TAU * float(i) / float(sign_count)
		var r := Game.OUTER_SHOULDER_START + 0.6
		var pos := Vector3(cos(theta) * r, 0.0, sin(theta) * r)
		var km := int(round(circumference * float(i) / float(sign_count) / 1000.0 * 10.0)) / 10.0

		var root := Node3D.new()
		root.position = pos
		var inward := (Vector3.ZERO - pos); inward.y = 0
		root.transform.basis = Basis.looking_at(inward.normalized(), Vector3.UP)
		add_child(root)

		var pole_mat := StandardMaterial3D.new()
		pole_mat.albedo_color = Color(0.7, 0.7, 0.72)
		pole_mat.metallic = 0.5

		var pole := MeshInstance3D.new()
		var cyl := CylinderMesh.new()
		cyl.top_radius = 0.05
		cyl.bottom_radius = 0.06
		cyl.height = 2.2
		pole.mesh = cyl
		pole.material_override = pole_mat
		pole.position = Vector3(0, 1.1, 0)
		root.add_child(pole)

		var panel_mat := StandardMaterial3D.new()
		panel_mat.albedo_color = Color(0.07, 0.4, 0.16)
		var panel := MeshInstance3D.new()
		var pb := BoxMesh.new()
		pb.size = Vector3(0.9, 0.6, 0.05)
		panel.mesh = pb
		panel.material_override = panel_mat
		panel.position = Vector3(0, 2.0, 0)
		root.add_child(panel)

		var label := Label3D.new()
		label.text = "МКАД %s км" % str(km)
		label.font_size = 48
		label.pixel_size = 0.006
		label.position = Vector3(0, 2.0, 0.04)
		root.add_child(label)

# ---------------------------------------------------------------------------
# Здания — силуэт "московского" кольца многоэтажек
# ---------------------------------------------------------------------------
func _build_buildings() -> void:
	var mat := StandardMaterial3D.new()
	mat.albedo_texture = FACADE_TEX
	mat.roughness = 0.9

	var box := BoxMesh.new()
	box.size = Vector3(1, 1, 1)

	var mm := MultiMesh.new()
	mm.mesh = box
	mm.transform_format = MultiMesh.TRANSFORM_3D

	var count := 70
	mm.instance_count = count
	for i in range(count):
		var theta := TAU * float(i) / float(count) + _rng.randf_range(-0.08, 0.08)
		var r := Game.OUTER_EDGE + 90.0 + _rng.randf_range(0.0, 260.0)
		var w := _rng.randf_range(16.0, 34.0)
		var d := _rng.randf_range(16.0, 34.0)
		var h := _rng.randf_range(18.0, 90.0)
		var pos := Vector3(cos(theta) * r, h * 0.5, sin(theta) * r)
		var basis := Basis(Vector3.UP, _rng.randf_range(0, TAU)).scaled(Vector3(w, h, d))
		mm.set_instance_transform(i, Transform3D(basis, pos))

	var mmi := MultiMeshInstance3D.new()
	mmi.multimesh = mm
	mmi.material_override = mat
	add_child(mmi)

	# Небольшой кластер зданий видно и внутри кольца — как будто МКАД огибает город.
	var mm2 := MultiMesh.new()
	mm2.mesh = box
	mm2.transform_format = MultiMesh.TRANSFORM_3D
	var count2 := 24
	mm2.instance_count = count2
	for i in range(count2):
		var theta := _rng.randf_range(0, TAU)
		var r := _rng.randf_range(40.0, Game.INNER_EDGE - 40.0)
		var w := _rng.randf_range(14.0, 26.0)
		var d := _rng.randf_range(14.0, 26.0)
		var h := _rng.randf_range(14.0, 60.0)
		var pos := Vector3(cos(theta) * r, h * 0.5, sin(theta) * r)
		var basis := Basis(Vector3.UP, _rng.randf_range(0, TAU)).scaled(Vector3(w, h, d))
		mm2.set_instance_transform(i, Transform3D(basis, pos))
	var mmi2 := MultiMeshInstance3D.new()
	mmi2.multimesh = mm2
	mmi2.material_override = mat
	add_child(mmi2)

# ---------------------------------------------------------------------------
# Деревья (берёзы) вдоль дороги — собственная процедурная модель.
# ---------------------------------------------------------------------------
func _build_tree_mesh() -> ArrayMesh:
	var trunk := CylinderMesh.new()
	trunk.top_radius = 0.12
	trunk.bottom_radius = 0.2
	trunk.height = 3.2
	trunk.radial_segments = 7

	var bark_mat := StandardMaterial3D.new()
	bark_mat.albedo_texture = BARK_TEX
	bark_mat.roughness = 1.0

	var leaves := SphereMesh.new()
	leaves.radius = 1.8
	leaves.height = 3.2
	leaves.radial_segments = 8
	leaves.rings = 6

	var leaf_mat := StandardMaterial3D.new()
	leaf_mat.albedo_color = Color(0.22, 0.42, 0.16)
	leaf_mat.roughness = 1.0

	var st := SurfaceTool.new()
	var combined := ArrayMesh.new()

	# Ствол
	st.clear()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	st.append_from(trunk, 0, Transform3D(Basis.IDENTITY, Vector3(0, 1.6, 0)))
	st.commit(combined)
	combined.surface_set_material(0, bark_mat)

	# Крона
	st.clear()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	st.append_from(leaves, 0, Transform3D(Basis.IDENTITY, Vector3(0, 3.8, 0)))
	st.commit(combined)
	combined.surface_set_material(1, leaf_mat)

	return combined

func _build_trees() -> void:
	var mesh := _build_tree_mesh()
	var mm := MultiMesh.new()
	mm.mesh = mesh
	mm.transform_format = MultiMesh.TRANSFORM_3D

	var placements: Array = []
	var attempts := 0
	var target := 220
	while placements.size() < target and attempts < target * 6:
		attempts += 1
		var band := _rng.randi_range(0, 1)
		var r: float
		if band == 0:
			r = _rng.randf_range(Game.OUTER_EDGE + 6.0, Game.OUTER_EDGE + 85.0)
		else:
			r = _rng.randf_range(30.0, Game.INNER_EDGE - 6.0)
		var theta := _rng.randf_range(0, TAU)
		placements.append(Vector2(theta, r))

	mm.instance_count = placements.size()
	for i in range(placements.size()):
		var theta: float = placements[i].x
		var r: float = placements[i].y
		var pos := Vector3(cos(theta) * r, 0.0, sin(theta) * r)
		var s := _rng.randf_range(0.8, 1.4)
		var basis := Basis(Vector3.UP, _rng.randf_range(0, TAU)).scaled(Vector3.ONE * s)
		mm.set_instance_transform(i, Transform3D(basis, pos))

	var mmi := MultiMeshInstance3D.new()
	mmi.multimesh = mm
	add_child(mmi)
