extends Node
## Game — глобальный автозагружаемый синглтон.
## Хранит константы кольцевой трассы (МКАД) и общие игровые данные,
## доступные из любого скрипта через "Game.".

# ---------------------------------------------------------------------------
# Геометрия кольца
# ---------------------------------------------------------------------------
const RING_RADIUS: float = 300.0          # радиус осевой линии кольца (м)
const LANE_WIDTH: float = 3.5             # ширина одной полосы (м)
const LANES_PER_SIDE: int = 3             # число полос в одну сторону
const MEDIAN_HALF_WIDTH: float = 2.0      # половина ширины отбойника/разделителя
const SHOULDER_WIDTH: float = 2.75        # ширина обочины
const ROAD_HALF_WIDTH: float = MEDIAN_HALF_WIDTH + LANES_PER_SIDE * LANE_WIDTH + SHOULDER_WIDTH

const OUTER_EDGE: float = RING_RADIUS + ROAD_HALF_WIDTH
const INNER_EDGE: float = RING_RADIUS - ROAD_HALF_WIDTH
const OUTER_SHOULDER_START: float = RING_RADIUS + ROAD_HALF_WIDTH - SHOULDER_WIDTH
const INNER_SHOULDER_START: float = RING_RADIUS - ROAD_HALF_WIDTH + SHOULDER_WIDTH

# Полностью съезжаем "в поле" за этим радиусом — трава/бездорожье.
const OFFROAD_MARGIN: float = 40.0
const HARD_WALL_MARGIN: float = 90.0

# ---------------------------------------------------------------------------
# Игровое состояние (обновляется мотоциклом, читается HUD и камерой)
# ---------------------------------------------------------------------------
var player_speed_kmh: float = 0.0
var player_distance_m: float = 0.0        # суммарный одометр
var player_laps: int = 0
var player_theta: float = -PI * 0.5       # текущий угол мотоцикла на кольце
var player_node: Node3D = null

signal speed_changed(speed_kmh: float)
signal lap_completed(lap_number: int)

# ---------------------------------------------------------------------------
# Математика кольца
# ---------------------------------------------------------------------------

## Точка на окружности заданного радиуса под углом theta (XZ-плоскость).
static func ring_point(theta: float, radius: float) -> Vector3:
	return Vector3(cos(theta) * radius, 0.0, sin(theta) * radius)

## Касательная (направление движения) в точке кольца.
## direction: 1 = против часовой (увеличение theta), -1 = по часовой.
static func ring_tangent(theta: float, direction: int) -> Vector3:
	var t := Vector3(-sin(theta), 0.0, cos(theta))
	return t.normalized() * sign(direction)

## Радиальный угол точки в мировых координатах XZ.
static func angle_of(pos: Vector3) -> float:
	return atan2(pos.z, pos.x)

## Расстояние от центра кольца (по плоскости XZ).
static func radius_of(pos: Vector3) -> float:
	return Vector2(pos.x, pos.z).length()

func reset_player_stats() -> void:
	player_speed_kmh = 0.0
	player_distance_m = 0.0
	player_laps = 0

func report_speed(kmh: float) -> void:
	player_speed_kmh = kmh
	speed_changed.emit(kmh)

func report_lap() -> void:
	player_laps += 1
	lap_completed.emit(player_laps)
