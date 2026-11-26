package com.zombieaquarium.sim.ai

import com.zombieaquarium.sim.engine.WeaponType
import com.zombieaquarium.sim.engine.ZombieType

enum class SurvivorTrait(val titleRu: String, val descRu: String) {
    LEADER("Лидер", "Снижает панику союзников, создаёт лагерь и распределяет роли"),
    BRAVE("Смельчак", "Прикрывает раненых и не бежит от одиночных зомби"),
    COWARD("Паникёр", "Бежит при опасности и чаще бросает напарников"),
    SELFISH("Эгоист", "Прячет еду, может обворовать склад при голоде"),
    MEDIC("Медик", "Лечит быстрее и экономнее, ищет аптечки"),
    ENGINEER("Инженер", "Быстрее строит баррикады, ловушки и костры"),
    STEALTH("Разведчик", "Меньше шумит при ходьбе и обыске"),
    PARANOID("Параноик", "Не доверяет чужакам, требует изоляции укушенных")
}

enum class CampRole(val titleRu: String) {
    NONE("Одиночка"),
    LEADER("Командир"),
    SENTRY("Часовой"),
    SCAVENGER("Добытчик"),
    BUILDER("Строитель"),
    MEDIC("Медик")
}

enum class FsmState(val titleRu: String) {
    IDLE("Осматривается"),
    MOVING("Идёт по маршруту"),
    SNEAKING("Крадётся скрытно"),
    LOOTING("Обыскивает"),
    FIGHTING("В бою"),
    FLEEING("Спасается бегством"),
    BUILDING("Строит / укрепляет"),
    HEALING("Лечит"),
    SLEEPING("Спит"),
    TALKING("Общается")
}

enum class UtilityGoal(val titleRu: String) {
    SURVIVE_FLEE("Спастись / бежать"),
    FIGHT_THREAT("Дать отпор угрозе"),
    HEAL_SELF("Подлечить раны"),
    HEAL_ALLY("Спасти союзника"),
    EAT("Найти еду и поесть"),
    DRINK("Найти воду и попить"),
    SLEEP("Отдохнуть / поспать"),
    WARM_UP("Согреться у костра"),
    SCAVENGE("Добыть припасы"),
    FORTIFY_CAMP("Укрепить и обустроить лагерь"),
    SOCIALIZE("Пообщаться и обменяться картой"),
    PATROL_OR_GUARD("Патрулировать периметр"),
    EXPLORE("Разведать город")
}

data class UtilityScore(
    val goal: UtilityGoal,
    val score: Float,
    val reasonRu: String
)

enum class MemoryKind(val titleRu: String) {
    FOOD("Еда"),
    WATER("Вода"),
    MEDS("Аптечка / бинты"),
    WEAPON("Оружие / патроны"),
    MATERIALS("Доски / лом"),
    HORDE("Опасная орда"),
    CAMP("Безопасный лагерь")
}

data class MemoryEntry(
    val kind: MemoryKind,
    val x: Int,
    val y: Int,
    val buildingId: Int,
    var seenTick: Int,
    var confidence: Float = 1.0f,
    var note: String = ""
)

enum class GoapActionType(val titleRu: String) {
    MOVE_TO("Идти к цели"),
    SNEAK_TO("Прокрасться к цели"),
    LOOT_CONTAINER("Обыскать полку/ящик"),
    EAT_INV("Съесть припасы"),
    DRINK_INV("Выпить воды"),
    TREAT_SELF("Перевязать себя"),
    TREAT_ALLY("Вылечить раненого"),
    SLEEP_TICK("Отдохнуть в укрытии"),
    BUILD_STRUCTURE("Построить объект"),
    BARRICADE_DOOR("Забаррикадировать проход"),
    CLOSE_DOOR("Закрыть дверь"),
    TALK_ALLY("Поговорить и обменяться знаниями"),
    FLEE_TO_SAFE("Отступить в безопасное место"),
    ATTACK_ZOMBIE("Атаковать зомби"),
    HANDLE_INFECTED("Принять меры к заражённому"),
    PATROL_STEP("Патрулировать сектор")
}

data class GoapStep(
    val action: GoapActionType,
    val targetX: Int = -1,
    val targetY: Int = -1,
    val targetEntityId: Int = -1,
    val detail: String = ""
)

data class SurvivorAgent(
    val id: Int,
    val name: String,
    val age: Int,
    val avatarColor: Int,
    var x: Float,
    var y: Float,
    val traits: List<SurvivorTrait>
) {
    var alive: Boolean = true
    var causeOfDeath: String = ""

    // Physiology (0..100)
    var hp: Float = 100f
    var hunger: Float = 82f      // 100 = сыт, 0 = голодает
    var thirst: Float = 84f      // 100 = не хочет пить, 0 = обезвожен
    var energy: Float = 88f      // 100 = бодрый, 0 = валится с ног
    var warmth: Float = 86f      // 100 = тепло, 0 = переохлаждение
    var stamina: Float = 100f    // для спринта и ударов
    var stress: Float = 10f      // 0 = спокоен, 100 = паника

    // Injuries & infection
    var bleeding: Float = 0f
    var limping: Boolean = false
    var sick: Boolean = false
    var infected: Boolean = false
    var infectionTimerTicks: Int = 0
    var quarantined: Boolean = false

    // Inventory & equipment
    var food: Int = 1
    var water: Int = 1
    var bandages: Int = 1
    var antibiotics: Int = 0
    var wood: Int = 0
    var scrap: Int = 0
    var ammo: Int = 0
    var meleeWeapon: WeaponType = WeaponType.FISTS
    var firearm: WeaponType? = null
    var hasFlashlight: Boolean = false
    var hiddenPersonalFood: Int = 0

    // Social & faction
    var campId: Int = -1
    var role: CampRole = CampRole.NONE
    val relations = HashMap<Int, Int>() // otherId -> -100..+100

    // Personal Fog-of-Knowledge Memory
    val memories = ArrayList<MemoryEntry>()
    val visitedBuildingIds = HashSet<Int>()

    // Brain state (Utility + GOAP + FSM)
    var fsmState: FsmState = FsmState.IDLE
    var activeGoal: UtilityGoal = UtilityGoal.EXPLORE
    var lastGoalScore: Float = 0f
    val topUtilities = ArrayList<UtilityScore>(3)
    val goapPlan = ArrayDeque<GoapStep>()
    val currentPath = ArrayList<Int>()
    var pathCursor: Int = 0
    var targetX: Float = x
    var targetY: Float = y
    var lastPosX: Float = x
    var lastPosY: Float = y
    var stuckTicks: Int = 0

    // Timers & overhead bubble
    var thinkCooldown: Int = 0
    var actionCooldown: Int = 0
    var combatCooldown: Int = 0
    var thoughtText: String = "Осматриваюсь..."
    var thoughtTimer: Int = 40
    var currentActionIcon: String = "Ищет"
    val bioLog = ArrayList<String>()

    // Visual animation state
    var facingX: Float = 1f
    var facingY: Float = 0f
    var prevX: Float = x
    var prevY: Float = y
    var animPhase: Float = 0f
    var attackAnimTicks: Int = 0
    var hurtAnimTicks: Int = 0
    var isMovingNow: Boolean = false

    fun hasTrait(trait: SurvivorTrait): Boolean = traits.contains(trait)

    fun activeWeapon(nearbyZombieCount: Int, isSneaking: Boolean): WeaponType {
        val gun = firearm
        if (gun != null && ammo > 0) {
            if (gun == WeaponType.CROSSBOW) return gun
            if (!isSneaking && (nearbyZombieCount >= 2 || stress > 50f || hp < 55f)) {
                return gun
            }
        }
        return meleeWeapon
    }

    fun addBio(day: Int, timeStr: String, text: String) {
        bioLog.add(0, "Д$day $timeStr — $text")
        if (bioLog.size > 35) bioLog.removeAt(bioLog.size - 1)
    }

    fun setThought(text: String, ticks: Int = 45) {
        thoughtText = text
        thoughtTimer = ticks
    }

    fun adjustRelation(otherId: Int, delta: Int) {
        if (otherId == id) return
        val cur = relations[otherId] ?: 0
        relations[otherId] = (cur + delta).coerceIn(-100, 100)
    }

    fun remember(kind: MemoryKind, px: Int, py: Int, bId: Int, tick: Int, note: String = "") {
        for (i in 0 until memories.size) {
            val m = memories[i]
            if (m.kind == kind && kotlin.math.abs(m.x - px) <= 2 && kotlin.math.abs(m.y - py) <= 2) {
                m.seenTick = tick
                m.confidence = 1f
                if (note.isNotEmpty()) m.note = note
                return
            }
        }
        memories.add(MemoryEntry(kind, px, py, bId, tick, 1f, note))
        if (memories.size > 48) {
            // Drop oldest least relevant memory
            var oldestIdx = 0
            var oldestTick = Int.MAX_VALUE
            for (i in 0 until memories.size) {
                if (memories[i].seenTick < oldestTick) {
                    oldestTick = memories[i].seenTick
                    oldestIdx = i
                }
            }
            memories.removeAt(oldestIdx)
        }
    }

    fun forgetNear(kind: MemoryKind, px: Int, py: Int) {
        memories.removeAll { it.kind == kind && kotlin.math.abs(it.x - px) <= 2 && kotlin.math.abs(it.y - py) <= 2 }
    }
}

data class CampFaction(
    val id: Int,
    var name: String,
    val color: Int,
    val homeBuildingId: Int,
    val centerX: Int,
    val centerY: Int,
    var leaderId: Int,
    val memberIds: MutableList<Int> = ArrayList(),
    var storageFood: Int = 0,
    var storageWater: Int = 0,
    var storageBandages: Int = 0,
    var storageAntibiotics: Int = 0,
    var storageWood: Int = 2,
    var storageScrap: Int = 0,
    var storageAmmo: Int = 0
)

data class ZombieAgent(
    val id: Int,
    val type: ZombieType,
    var x: Float,
    var y: Float,
    var hp: Float,
    var alive: Boolean = true,
    var targetSurvivorId: Int = -1,
    var lastAlertX: Float = -1f,
    var lastAlertY: Float = -1f,
    var alertTimer: Int = 0,
    var attackCooldown: Int = 0,
    var screamCooldown: Int = 0,
    var wanderVx: Float = 0f,
    var wanderVy: Float = 0f,
    var wanderTimer: Int = 0,
    var facingX: Float = 1f,
    var facingY: Float = 0f,
    var prevX: Float = x,
    var prevY: Float = y,
    var animPhase: Float = 0f,
    var attackAnimTicks: Int = 0,
    var hurtAnimTicks: Int = 0
)

data class ChronicleEvent(
    val day: Int,
    val timeStr: String,
    val category: String,
    val message: String,
    val focusX: Float = -1f,
    val focusY: Float = -1f
)
