package com.zombieaquarium.sim.engine

data class SimConfig(
    val mapSize: Int = 160,
    val initialSurvivors: Int = 28,
    val zombieDensity: Float = 1.0f,   // 0.5f .. 2.0f
    val lootRarity: Float = 1.0f,      // 0.5f (редкий) .. 1.6f (богатый)
    val runnerRatio: Float = 0.16f,    // 0.05f .. 0.40f
    val hordeFrequency: Float = 1.0f,  // 0.5f .. 2.0f
    val seed: Long = System.currentTimeMillis()
)

object TerrainType {
    const val GRASS: Byte = 0
    const val FOREST: Byte = 1
    const val WATER: Byte = 2
    const val BRIDGE: Byte = 3
    const val ROAD: Byte = 4
    const val CONCRETE: Byte = 5
    const val WOOD_FLOOR: Byte = 6
    const val TILE_FLOOR: Byte = 7
}

object WallType {
    const val NONE: Byte = 0
    const val WALL_BRICK: Byte = 1
    const val WALL_WOOD: Byte = 2
    const val WINDOW_INTACT: Byte = 3
    const val WINDOW_BROKEN: Byte = 4
    const val DOOR_CLOSED: Byte = 5
    const val DOOR_OPEN: Byte = 6
    const val DOOR_BARRICADED: Byte = 7
    const val TREE: Byte = 8
    const val CAR_WRECK: Byte = 9
    const val FENCE: Byte = 10
}

enum class BuildingType(val titleRu: String, val defensibility: Float) {
    SUBURB_HOUSE("Частный дом", 1.25f),
    APARTMENT("Многоэтажка", 0.9f),
    SUPERMARKET("Супермаркет", 0.75f),
    PHARMACY("Аптека", 1.05f),
    HOSPITAL("Больница", 0.7f),
    POLICE("Полицейский участок", 1.15f),
    GAS_STATION("Заправка", 0.8f),
    WAREHOUSE("Склад", 1.0f)
}

enum class WeatherType(val titleRu: String) {
    CLEAR("Ясно"),
    RAIN("Дождь"),
    FOG("Туман"),
    STORM("Гроза")
}

enum class OverlayMode(val titleRu: String) {
    NORMAL("Обычный вид"),
    INTERIORS("Без крыш"),
    NOISE_LIGHT("Шум и свет"),
    FACTIONS("Зоны лагерей"),
    AGENT_MEMORY("Память агента")
}

enum class WeaponType(
    val titleRu: String,
    val isFirearm: Boolean,
    val damage: Float,
    val rangeTiles: Float,
    val noiseRadius: Float,
    val cooldownTicks: Int
) {
    FISTS("Кулаки", false, 8f, 1.35f, 3f, 7),
    KNIFE("Нож", false, 18f, 1.45f, 2f, 5),
    BAT("Бита / Лом", false, 28f, 1.6f, 5f, 8),
    AXE("Топор", false, 38f, 1.65f, 6f, 10),
    PISTOL("Пистолет", true, 46f, 9.5f, 34f, 7),
    SHOTGUN("Дробовик", true, 78f, 6.5f, 45f, 13),
    RIFLE("Винтовка", true, 64f, 13.5f, 44f, 9)
}

enum class StructureType(val titleRu: String, val woodCost: Int, val scrapCost: Int) {
    CAMPFIRE("Костёр", 3, 0),
    RAIN_COLLECTOR("Дождесборник", 3, 2),
    SPIKE_TRAP("Колья", 2, 1),
    STORAGE_BOX("Склад лагеря", 2, 1),
    BARRICADE("Баррикада", 2, 0)
}

enum class ZombieType(
    val titleRu: String,
    val maxHp: Float,
    val speedTilesPerSec: Float,
    val attackDamage: Float,
    val structureDamage: Float
) {
    WALKER("Бродяга", 75f, 1.45f, 14f, 6f),
    RUNNER("Бегун", 60f, 3.65f, 16f, 5f),
    SCREAMER("Крикун", 65f, 1.9f, 10f, 4f),
    BRUTE("Громила", 240f, 1.25f, 30f, 22f)
}
