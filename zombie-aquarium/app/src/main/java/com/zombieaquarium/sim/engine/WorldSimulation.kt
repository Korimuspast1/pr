package com.zombieaquarium.sim.engine

import com.zombieaquarium.sim.ai.CampFaction
import com.zombieaquarium.sim.ai.CampRole
import com.zombieaquarium.sim.ai.ChronicleEvent
import com.zombieaquarium.sim.ai.HumanBrain
import com.zombieaquarium.sim.ai.SurvivorAgent
import com.zombieaquarium.sim.ai.SurvivorTrait
import com.zombieaquarium.sim.ai.ZombieAgent
import com.zombieaquarium.sim.ai.ZombieBrain
import kotlin.math.max
import kotlin.math.min

class WorldSimulation(val config: SimConfig) {
    val simLock = Any()
    val rng = SimRng(config.seed)
    val map = WorldMap(config.mapSize, config.seed, config.lootRarity)

    val survivorGrid = SpatialHashGrid(config.mapSize, cellSize = 8, maxEntities = 1024)
    val zombieGrid = SpatialHashGrid(config.mapSize, cellSize = 8, maxEntities = 4096)

    val particles = ParticlePool(512)
    val soundRings = SoundRingPool(64)
    val tracers = TracerPool(64)

    val survivors = ArrayList<SurvivorAgent>()
    val survivorsById = HashMap<Int, SurvivorAgent>()
    val zombies = ArrayList<ZombieAgent>()
    val zombiesById = HashMap<Int, ZombieAgent>()
    val camps = ArrayList<CampFaction>()
    val campsById = HashMap<Int, CampFaction>()
    val structures = ArrayList<CampStructure>()
    val chronicle = ArrayDeque<ChronicleEvent>()

    var tickCount: Int = 0
    var day: Int = 1
    var timeOfDay: Float = 0.34f // 08:10 утра
    var weather: WeatherType = WeatherType.CLEAR
    var weatherTimerTicks: Int = 450
    var lightningFlashTicks: Int = 0

    var speedMultiplier: Int = 1 // 0 = пауза, 1, 2, 4, 8
    var deadSurvivorsCount: Int = 0
    var killedZombiesCount: Int = 0

    var nextEntityId = 1
    var nextCampId = 1
    var nextStructureId = 1

    private val beaconX = IntArray(128)
    private val beaconY = IntArray(128)

    val isNight: Boolean
        get() = timeOfDay < 0.24f || timeOfDay > 0.80f

    val livingSurvivorsCount: Int
        get() = survivors.count { it.alive }

    val livingZombiesCount: Int
        get() = zombies.count { it.alive }

    init {
        populateWorld()
        logChronicle("Старт", "Новый мир создан (Seed ${config.seed}). Выживших: ${survivors.size}, зомби: ${zombies.size}.")
    }

    private fun populateWorld() {
        val allTraits = SurvivorTrait.entries.toTypedArray()

        // Spawn survivors in suburbs/buildings/roads away from river
        repeat(config.initialSurvivors) { idx ->
            val pos = findWalkableSpawn(preferBuilding = idx % 2 == 0)
            val name = RUSSIAN_NAMES[idx % RUSSIAN_NAMES.size]
            val age = rng.nextRange(19, 54)
            val t1 = allTraits[rng.nextInt(allTraits.size)]
            var t2 = allTraits[rng.nextInt(allTraits.size)]
            if (t2 == t1) t2 = allTraits[(t1.ordinal + 1) % allTraits.size]
            val traits = if (rng.chance(0.45f)) {
                val t3 = allTraits[(t2.ordinal + 2) % allTraits.size]
                listOf(t1, t2, t3).distinct()
            } else listOf(t1, t2)

            val agent = SurvivorAgent(
                id = nextEntityId++,
                name = name,
                age = age,
                avatarColor = SURVIVOR_COLORS[idx % SURVIVOR_COLORS.size],
                x = pos.first + 0.5f,
                y = pos.second + 0.5f,
                traits = traits
            ).apply {
                meleeWeapon = if (rng.chance(0.45f)) WeaponType.KNIFE else if (rng.chance(0.3f)) WeaponType.BAT else WeaponType.FISTS
                hasFlashlight = rng.chance(0.4f)
                addBio(1, formatTime(), "Очнулся(лась) в начале эпидемии")
            }
            survivors.add(agent)
            survivorsById[agent.id] = agent
        }

        // Spawn zombies scaled by map area & zombieDensity
        val baseZombies = ((config.mapSize * config.mapSize) / 145f * config.zombieDensity).toInt().coerceIn(45, 520)
        repeat(baseZombies) {
            val pos = findWalkableSpawn(preferBuilding = rng.chance(0.25f))
            val zType = pickZombieType()
            spawnZombieAt(pos.first + 0.5f, pos.second + 0.5f, zType)
        }
        rebuildGrids()
        rebuildFlowField()
    }

    private fun pickZombieType(): ZombieType {
        val r = rng.nextFloat()
        return when {
            r < config.runnerRatio -> ZombieType.RUNNER
            r < config.runnerRatio + 0.08f -> ZombieType.SCREAMER
            r < config.runnerRatio + 0.14f -> ZombieType.BRUTE
            else -> ZombieType.WALKER
        }
    }

    private fun findWalkableSpawn(preferBuilding: Boolean): Pair<Int, Int> {
        if (preferBuilding && map.buildings.isNotEmpty()) {
            repeat(15) {
                val b = map.buildings[rng.nextInt(map.buildings.size)]
                val x = rng.nextRange(b.x1 + 1, max(b.x1 + 1, b.x2 - 1))
                val y = rng.nextRange(b.y1 + 1, max(b.y1 + 1, b.y2 - 1))
                if (map.isWalkableForHuman(x, y, allowClosedDoor = false)) return x to y
            }
        }
        repeat(100) {
            val x = rng.nextRange(4, map.size - 5)
            val y = rng.nextRange(4, map.size - 5)
            if (map.isWalkableForHuman(x, y, allowClosedDoor = false)) return x to y
        }
        return map.size / 2 to map.size / 2
    }

    fun stepFixedTick() {
        synchronized(simLock) {
            tickCount++
            val dt = TICK_DT

            // 1. Day/Night & Weather
            timeOfDay += 0.00055f // ~1800 ticks per full day (~3 min at x1)
            if (timeOfDay >= 1f) {
                timeOfDay -= 1f
                day++
                logChronicle("Рассвет", "Наступил день $day. Выживших: $livingSurvivorsCount, лагерей: ${camps.size}.")
            }

            if (lightningFlashTicks > 0) lightningFlashTicks--
            weatherTimerTicks--
            if (weatherTimerTicks <= 0) {
                val nextW = WeatherType.entries[rng.nextInt(WeatherType.entries.size)]
                weather = nextW
                weatherTimerTicks = rng.nextRange(320, 720)
                logChronicle("Погода", "Погода изменилась: ${weather.titleRu}.")
            }
            if (weather == WeatherType.STORM && rng.chance(0.014f)) {
                lightningFlashTicks = 3
            }

            // 2. Camp structures (campfires, rain collectors)
            updateStructures()

            // 3. Spatial grids
            rebuildGrids()

            // 4. Human agents
            for (i in 0 until survivors.size) {
                val s = survivors[i]
                if (s.alive) HumanBrain.tickSurvivor(s, this, dt, rng)
            }

            // 5. Zombie agents
            for (i in 0 until zombies.size) {
                val z = zombies[i]
                if (z.alive) ZombieBrain.tickZombie(z, this, dt, rng)
            }

            // 6. Fields, Flow Field & Pools
            map.decayFields()
            if (tickCount % 8 == 0) {
                rebuildFlowField()
            }
            particles.update(dt)
            soundRings.update(dt)
            tracers.update(dt)

            // 7. Periodic migrating horde
            val hordePeriod = max(240, (680 / config.hordeFrequency).toInt())
            if (tickCount % hordePeriod == 0 && livingZombiesCount < 580) {
                spawnMigratingHorde()
            }
        }
    }

    private fun updateStructures() {
        for (st in structures) {
            when (st.type) {
                StructureType.CAMPFIRE -> {
                    if (weather == WeatherType.RAIN || weather == WeatherType.STORM) {
                        st.fuelTicks = max(0, st.fuelTicks - 3)
                    } else if (st.fuelTicks > 0) {
                        st.fuelTicks--
                        val ti = map.idx(st.x.coerceIn(0, map.size - 1), st.y.coerceIn(0, map.size - 1))
                        map.lightField[ti] = 85f
                    }
                }
                StructureType.RAIN_COLLECTOR -> {
                    if ((weather == WeatherType.RAIN || weather == WeatherType.STORM) && tickCount % 40 == 0) {
                        val camp = campsById[st.campId]
                        if (camp != null && camp.storageWater < 12) {
                            camp.storageWater++
                        }
                    }
                }
                else -> {}
            }
        }
    }

    private fun rebuildGrids() {
        survivorGrid.clear()
        for (i in 0 until survivors.size) {
            val s = survivors[i]
            if (s.alive) survivorGrid.insert(s.id, s.x, s.y)
        }
        zombieGrid.clear()
        for (i in 0 until zombies.size) {
            val z = zombies[i]
            if (z.alive) zombieGrid.insert(z.id, z.x, z.y)
        }
    }

    private fun rebuildFlowField() {
        var bCount = 0
        for (s in survivors) {
            if (!s.alive) continue
            if (bCount < beaconX.size && (s.hasFlashlight && isNight || s.bleeding > 0.5f || s.fsmState == com.zombieaquarium.sim.ai.FsmState.FIGHTING)) {
                beaconX[bCount] = s.x.toInt().coerceIn(0, map.size - 1)
                beaconY[bCount] = s.y.toInt().coerceIn(0, map.size - 1)
                bCount++
            }
        }
        map.rebuildZombieFlowField(beaconX, beaconY, bCount)
    }

    fun isNearActiveCampfire(x: Float, y: Float, radius: Float): Boolean {
        val r2 = radius * radius
        for (st in structures) {
            if (st.type == StructureType.CAMPFIRE && st.fuelTicks > 0) {
                val dx = st.x + 0.5f - x
                val dy = st.y + 0.5f - y
                if (dx * dx + dy * dy <= r2) return true
            }
        }
        return false
    }

    fun hasCampStructure(campId: Int, type: StructureType): Boolean {
        for (st in structures) {
            if (st.campId == campId && st.type == type) return true
        }
        return false
    }

    fun needsDoorBarricade(camp: CampFaction): Boolean {
        val b = map.buildings.getOrNull(camp.homeBuildingId - 1) ?: return false
        for (ti in b.doorTiles) {
            if (map.wall[ti] != WallType.DOOR_BARRICADED) return true
        }
        return false
    }

    fun addCampStructure(st: CampStructure) {
        structures.add(st)
    }

    fun createCamp(founder: SurvivorAgent, building: BuildingInfo): CampFaction {
        val id = nextCampId++
        val campName = CAMP_NAMES[(id - 1) % CAMP_NAMES.size]
        val color = CAMP_COLORS[(id - 1) % CAMP_COLORS.size]
        val camp = CampFaction(
            id = id,
            name = campName,
            color = color,
            homeBuildingId = building.id,
            centerX = building.centerX.toInt(),
            centerY = building.centerY.toInt(),
            leaderId = founder.id
        )
        building.claimedByCampId = id
        camps.add(camp)
        campsById[id] = camp
        joinCamp(founder, camp, forceRole = CampRole.LEADER)
        logChronicle("Лагерь", "${founder.name} основал(а) лагерь «$campName» (${building.type.titleRu}).", building.centerX, building.centerY)
        return camp
    }

    fun joinCamp(agent: SurvivorAgent, camp: CampFaction, forceRole: CampRole? = null) {
        if (agent.campId == camp.id) return
        agent.campId = camp.id
        if (!camp.memberIds.contains(agent.id)) camp.memberIds.add(agent.id)
        agent.role = forceRole ?: when {
            agent.hasTrait(SurvivorTrait.MEDIC) -> CampRole.MEDIC
            agent.hasTrait(SurvivorTrait.ENGINEER) -> CampRole.BUILDER
            agent.hasTrait(SurvivorTrait.BRAVE) -> CampRole.SENTRY
            else -> CampRole.SCAVENGER
        }
        agent.addBio(day, formatTime(), "Присоединился(лась) к лагерю «${camp.name}» (${agent.role.titleRu})")
    }

    fun expelFromCamp(agent: SurvivorAgent, camp: CampFaction) {
        camp.memberIds.remove(agent.id)
        agent.campId = -1
        agent.role = CampRole.NONE
        agent.addBio(day, formatTime(), "Изгнан(а) из лагеря «${camp.name}»")
    }

    fun spawnZombieAt(x: Float, y: Float, type: ZombieType): ZombieAgent {
        val z = ZombieAgent(
            id = nextEntityId++,
            type = type,
            x = x.coerceIn(1f, map.size - 2f),
            y = y.coerceIn(1f, map.size - 2f),
            hp = type.maxHp
        )
        zombies.add(z)
        zombiesById[z.id] = z
        return z
    }

    fun killZombie(z: ZombieAgent, killer: SurvivorAgent?) {
        if (!z.alive) return
        z.alive = false
        killedZombiesCount++
        if (killer != null) {
            killer.addBio(day, formatTime(), "Уничтожил(а) зомби (${z.type.titleRu})")
        }
    }

    fun killSurvivor(agent: SurvivorAgent, cause: String) {
        if (!agent.alive) return
        agent.alive = false
        agent.causeOfDeath = cause
        deadSurvivorsCount++
        campsById[agent.campId]?.memberIds?.remove(agent.id)
        agent.addBio(day, formatTime(), "Погиб(ла): $cause")
        logChronicle("Гибель", "${agent.name} погиб(ла): $cause.", agent.x, agent.y)
    }

    fun turnSurvivorIntoZombie(agent: SurvivorAgent, reason: String) {
        killSurvivor(agent, reason)
        spawnZombieAt(agent.x, agent.y, ZombieType.RUNNER)
        logChronicle("Обращение", "${agent.name} обратился(лась) в зомби!", agent.x, agent.y)
    }

    private fun spawnMigratingHorde() {
        val edge = rng.nextInt(4)
        val cx = when (edge) { 0 -> 4f; 1 -> (map.size - 5).toFloat(); else -> rng.nextFloatRange(10f, map.size - 10f) }
        val cy = when (edge) { 2 -> 4f; 3 -> (map.size - 5).toFloat(); else -> rng.nextFloatRange(10f, map.size - 10f) }
        val count = rng.nextRange(8, 16)
        repeat(count) {
            val zx = (cx + rng.nextFloatRange(-3f, 3f)).coerceIn(2f, map.size - 3f)
            val zy = (cy + rng.nextFloatRange(-3f, 3f)).coerceIn(2f, map.size - 3f)
            if (map.isWalkableForZombie(zx.toInt(), zy.toInt())) {
                spawnZombieAt(zx, zy, pickZombieType())
            }
        }
        logChronicle("Орда", "К городу движется мигрирующая орда ($count зомби)!", cx, cy)
    }

    // === God Mode Interventions ===
    fun godDropSupplyCrate(wx: Float, wy: Float) {
        synchronized(simLock) {
            val tx = wx.toInt().coerceIn(2, map.size - 3)
            val ty = wy.toInt().coerceIn(2, map.size - 3)
            val c = LootContainer(
                id = map.lootContainers.size + 1,
                x = tx, y = ty, buildingId = -1,
                food = 5, water = 5, bandages = 4, antibiotics = 2,
                wood = 6, ammo = 18, weapon = WeaponType.RIFLE, hasFlashlight = true
            )
            map.lootContainers.add(c)
            map.lootByTile[map.idx(tx, ty)] = c
            map.addNoise(tx, ty, 24f, 65f)
            soundRings.spawn(tx + 0.5f, ty + 0.5f, 18f, 0xFF10B981.toInt(), 1.2f)
            logChronicle("Режим Бога", "Сброшен гуманитарный ящик в сектор ($tx, $ty)!", tx.toFloat(), ty.toFloat())
        }
    }

    fun godSpawnHorde(wx: Float, wy: Float) {
        synchronized(simLock) {
            val count = 14
            repeat(count) {
                val zx = (wx + rng.nextFloatRange(-3.5f, 3.5f)).coerceIn(2f, map.size - 3f)
                val zy = (wy + rng.nextFloatRange(-3.5f, 3.5f)).coerceIn(2f, map.size - 3f)
                val zt = if (it == 0) ZombieType.BRUTE else if (it % 4 == 0) ZombieType.SCREAMER else pickZombieType()
                spawnZombieAt(zx, zy, zt)
            }
            soundRings.spawn(wx, wy, 16f, 0xFFEF4444.toInt(), 1.1f)
            logChronicle("Режим Бога", "Призвана орда зомби ($count) в точку (${wx.toInt()}, ${wy.toInt()}).", wx, wy)
        }
    }

    fun godSpawnSurvivor(wx: Float, wy: Float) {
        synchronized(simLock) {
            val allTraits = SurvivorTrait.entries.toTypedArray()
            val name = RUSSIAN_NAMES[nextEntityId % RUSSIAN_NAMES.size]
            val t1 = allTraits[rng.nextInt(allTraits.size)]
            val t2 = allTraits[(t1.ordinal + 2) % allTraits.size]
            val s = SurvivorAgent(
                id = nextEntityId++,
                name = name,
                age = rng.nextRange(20, 48),
                avatarColor = SURVIVOR_COLORS[nextEntityId % SURVIVOR_COLORS.size],
                x = wx.coerceIn(2f, map.size - 3f),
                y = wy.coerceIn(2f, map.size - 3f),
                traits = listOf(t1, t2)
            ).apply {
                meleeWeapon = WeaponType.BAT
                hasFlashlight = true
                addBio(day, formatTime(), "Прибыл(а) в город извне")
            }
            survivors.add(s)
            survivorsById[s.id] = s
            logChronicle("Режим Бога", "Новый выживший ${s.name} появился в городе.", s.x, s.y)
        }
    }

    fun godSetWeather(w: WeatherType) {
        synchronized(simLock) {
            weather = w
            weatherTimerTicks = 500
            if (w == WeatherType.STORM) lightningFlashTicks = 4
            logChronicle("Режим Бога", "Установлена погода: ${w.titleRu}.")
        }
    }

    fun logChronicle(category: String, msg: String, fx: Float = -1f, fy: Float = -1f) {
        chronicle.addFirst(ChronicleEvent(day, formatTime(), category, msg, fx, fy))
        while (chronicle.size > 90) chronicle.removeLast()
    }

    fun formatTime(): String {
        val totalMin = (timeOfDay * 1440f).toInt().coerceIn(0, 1439)
        val hh = totalMin / 60
        val mm = totalMin % 60
        return String.format("%02d:%02d", hh, mm)
    }

    companion object {
        const val TICK_DT = 0.1f

        val RUSSIAN_NAMES = listOf(
            "Артём", "Анна", "Олег", "Марина", "Дмитрий", "Елена", "Игорь", "Виктория",
            "Кирилл", "Наталья", "Максим", "Дарья", "Роман", "София", "Павел", "Алина",
            "Евгений", "Ольга", "Никита", "Татьяна", "Сергей", "Ксения", "Алексей", "Ирина",
            "Денис", "Полина", "Андрей", "Мария", "Тимур", "Вероника"
        )

        val CAMP_NAMES = listOf(
            "Северный пост", "Тихая гавань", "Искра", "Цитадель", "Последний оплот",
            "Красный мост", "Заря", "Старая аптека"
        )

        val SURVIVOR_COLORS = intArrayOf(
            0xFF38BDF8.toInt(), 0xFF34D399.toInt(), 0xFFFBBF24.toInt(), 0xFFA78BFA.toInt(),
            0xFFF472B6.toInt(), 0xFFFB923C.toInt(), 0xFF2DD4BF.toInt(), 0xFF60A5FA.toInt()
        )

        val CAMP_COLORS = intArrayOf(
            0xFF10B981.toInt(), 0xFF3B82F6.toInt(), 0xFFF59E0B.toInt(), 0xFFEC4899.toInt(),
            0xFF8B5CF6.toInt(), 0xFF14B8A6.toInt()
        )
    }
}
