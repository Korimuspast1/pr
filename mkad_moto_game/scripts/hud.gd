extends CanvasLayer
## HUD — спидометр, одометр, счётчик кругов и подсказки по управлению.

var speed_label: Label
var sub_label: Label
var lap_label: Label
var hint_label: Label

func _ready() -> void:
	layer = 10

	var margin := MarginContainer.new()
	margin.set_anchors_preset(Control.PRESET_FULL_RECT)
	margin.add_theme_constant_override("margin_left", 24)
	margin.add_theme_constant_override("margin_right", 24)
	margin.add_theme_constant_override("margin_top", 18)
	margin.add_theme_constant_override("margin_bottom", 18)
	margin.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(margin)

	var root_box := VBoxContainer.new()
	root_box.set_anchors_preset(Control.PRESET_FULL_RECT)
	root_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	margin.add_child(root_box)

	# --- Верхняя подсказка по управлению ---
	hint_label = Label.new()
	hint_label.text = "W/↑ — газ    S/↓ — тормоз    A/D или ←/→ — руль    Пробел — ручник    ЛКМ+мышь — камера    C — вид    R — сброс на дорогу"
	hint_label.add_theme_font_size_override("font_size", 16)
	hint_label.add_theme_color_override("font_color", Color(1, 1, 1, 0.85))
	hint_label.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.8))
	hint_label.add_theme_constant_override("shadow_offset_x", 1)
	hint_label.add_theme_constant_override("shadow_offset_y", 1)
	hint_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	root_box.add_child(hint_label)

	var spacer := Control.new()
	spacer.size_flags_vertical = Control.SIZE_EXPAND_FILL
	root_box.add_child(spacer)

	# --- Нижняя панель: спидометр + одометр + круги ---
	var bottom_box := HBoxContainer.new()
	bottom_box.alignment = BoxContainer.ALIGNMENT_END
	bottom_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	root_box.add_child(bottom_box)

	var info_col := VBoxContainer.new()
	bottom_box.add_child(info_col)

	sub_label = Label.new()
	sub_label.text = "Пройдено: 0.0 км"
	sub_label.add_theme_font_size_override("font_size", 20)
	sub_label.add_theme_color_override("font_color", Color(1, 1, 1, 0.9))
	sub_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	info_col.add_child(sub_label)

	lap_label = Label.new()
	lap_label.text = "Круг: 0"
	lap_label.add_theme_font_size_override("font_size", 20)
	lap_label.add_theme_color_override("font_color", Color(1, 1, 1, 0.9))
	lap_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	info_col.add_child(lap_label)

	speed_label = Label.new()
	speed_label.text = "0 км/ч"
	speed_label.add_theme_font_size_override("font_size", 52)
	speed_label.add_theme_color_override("font_color", Color(1, 1, 1))
	speed_label.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.85))
	speed_label.add_theme_constant_override("shadow_offset_x", 2)
	speed_label.add_theme_constant_override("shadow_offset_y", 2)
	speed_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	bottom_box.add_child(speed_label)

func _process(_delta: float) -> void:
	speed_label.text = "%d км/ч" % int(round(Game.player_speed_kmh))
	sub_label.text = "Пройдено: %.1f км" % (Game.player_distance_m / 1000.0)
	lap_label.text = "Круг: %d" % Game.player_laps
