package com.zombieaquarium.sim.ai

import com.zombieaquarium.sim.engine.BuildingInfo
import com.zombieaquarium.sim.engine.CampStructure
import com.zombieaquarium.sim.engine.LootContainer
import com.zombieaquarium.sim.engine.SimRng
import com.zombieaquarium.sim.engine.StructureType
import com.zombieaquarium.sim.engine.TerrainType
import com.zombieaquarium.sim.engine.WallType
import com.zombieaquarium.sim.engine.WeaponType
import com.zombieaquarium.sim.engine.WeatherType
import com.zombieaquarium.sim.engine.WorldMap
import com.zombieaquarium.sim.engine.WorldSimulation
import kotlin.math.abs
import kotlin.math.max
import kotlin.math.min
import kotlin.math.sqrt

object HumanBrain {
    private val queryBuf = IntArray(64)

    fun tickSurvivor(
        agent: SurvivorAgent,
        sim: WorldSimulation,
        dt: Float,
        rng: SimRng
    ) {
        if (!agent.alive) return

        // 1. Physiology & Status Updates
        updatePhysiology(agent, sim, dt, rng)
        if (!agent.alive) return

        if (agent.thoughtTimer > 0) agent.thoughtTimer--
        if (agent.actionCooldown > 0) agent.actionCooldown--
        if (agent.combatCooldown > 0) agent.combatCooldown--
        if (agent.thinkCooldown > 0) agent.thinkCooldown--

        // 2. Sensory scan (Strict Fog of Knowledge: only what agent sees!)
        val visRadius = computeVisionRadius(agent, sim)
        val nearbyZombies = sim.zombieGrid.queryRadius(agent.x, agent.y, visRadius, queryBuf)
        var visibleZombies = 0
        var closestZombie: ZombieAgent? = null
        var closestZDist2 = Float.MAX_VALUE

        for (i in 0 until nearbyZombies) {
            val z = sim.zombiesById[queryBuf[i]] ?: continue
            if (!z.alive) continue
            if (sim.map.hasLineOfSight(agent.x.toInt(), agent.y.toInt(), z.x.toInt(), z.y.toInt(), visRadius.toInt())) {
                visibleZombies++
                val d2 = dist2(agent.x, agent.y, z.x, z.y)
                if (d2 < closestZDist2) {
                    closestZDist2 = d2
                    closestZombie = z
                }
            }
        }
        if (visibleZombies >= 4) {
            agent.remember(MemoryKind.HORDE, agent.x.toInt(), agent.y.toInt(), -1, sim.tickCount, "Орда ($visibleZombies)")
        }

        // Scan nearby loot containers in sight
        scanVisibleWorld(agent, sim, visRadius)

        // Nearby survivors (for healing, camp formation, social sync, moral dilemmas)
        val nearbyHumansCount = sim.survivorGrid.queryRadius(agent.x, agent.y, visRadius, queryBuf)
        val visibleAllies = ArrayList<SurvivorAgent>(8)
        for (i in 0 until nearbyHumansCount) {
            val other = sim.survivorsById[queryBuf[i]] ?: continue
            if (!other.alive || other.id == agent.id) continue
            if (sim.map.hasLineOfSight(agent.x.toInt(), agent.y.toInt(), other.x.toInt(), other.y.toInt(), visRadius.toInt())) {
                visibleAllies.add(other)
            }
        }

        // Dynamic camp formation / joining
        tryFormOrJoinCamp(agent, visibleAllies, sim, rng)

        // Check moral dilemmas in camp (infected member or food theft)
        handleCampDilemmas(agent, visibleAllies, sim, rng)
        if (!agent.alive) return

        // Stress update from visible danger & traits
        updateStress(agent, visibleZombies, visibleAllies, sim)

        // 3. Reactive Interrupt Layer (immediate threats override slow plans!)
        if (checkReactiveInterrupt(agent, visibleZombies, closestZombie, closestZDist2, sim, rng)) {
            executeCurrentStep(agent, visibleZombies, closestZombie, sim, dt, rng)
            return
        }

        // 4. Utility AI + GOAP Re-planning when idle or cooldown ready
        if (agent.goapPlan.isEmpty() || agent.thinkCooldown <= 0) {
            evaluateUtilitiesAndPlan(agent, visibleZombies, closestZombie, visibleAllies, sim, rng)
            agent.thinkCooldown = rng.nextRange(12, 22)
        }

        // 5. Execute FSM / GOAP step
        executeCurrentStep(agent, visibleZombies, closestZombie, sim, dt, rng)
    }

    private fun updatePhysiology(agent: SurvivorAgent, sim: WorldSimulation, dt: Float, rng: SimRng) {
        val isSleeping = agent.fsmState == FsmState.SLEEPING
        val speedFactor = if (agent.fsmState == FsmState.FLEEING) 1.6f else if (isSleeping) 0.45f else 1.0f

        agent.hunger = max(0f, agent.hunger - 0.048f * dt * speedFactor)
        agent.thirst = max(0f, agent.thirst - 0.068f * dt * speedFactor)
        agent.energy = if (isSleeping) {
            min(100f, agent.energy + 1.45f * dt)
        } else {
            max(0f, agent.energy - 0.042f * dt * speedFactor)
        }

        // Temperature / Warmth
        val nearFire = sim.isNearActiveCampfire(agent.x, agent.y, 4.5f)
        val indoors = sim.map.buildingId[sim.map.idx(agent.x.toInt().coerceIn(0, sim.map.size - 1), agent.y.toInt().coerceIn(0, sim.map.size - 1))] != -1
        val ambientWarmthTarget = when {
            nearFire -> 95f
            indoors && !sim.isNight -> 82f
            indoors && sim.isNight -> 68f
            sim.weather == WeatherType.RAIN || sim.weather == WeatherType.STORM -> 36f
            sim.isNight -> 45f
            else -> 76f
        }
        agent.warmth += (ambientWarmthTarget - agent.warmth) * 0.05f * dt

        // Stamina recovery
        if (agent.fsmState != FsmState.FLEEING && agent.fsmState != FsmState.FIGHTING) {
            agent.stamina = min(100f, agent.stamina + 3.2f * dt)
        }

        // Bleeding leaves blood trail attracting zombies!
        if (agent.bleeding > 0f) {
            agent.hp -= agent.bleeding * 0.16f * dt
            if (rng.chance(0.32f)) {
                val tx = agent.x.toInt().coerceIn(0, sim.map.size - 1)
                val ty = agent.y.toInt().coerceIn(0, sim.map.size - 1)
                sim.map.addBlood(tx, ty, 22f)
                sim.particles.spawn(agent.x, agent.y, 0f, 0f, 0.9f, 0xFFDC2626.toInt(), 0.12f)
            }
            agent.bleeding = max(0f, agent.bleeding - 0.03f * dt)
        }

        // Starvation / dehydration / hypothermia
        if (agent.hunger <= 0f) agent.hp -= 0.35f * dt
        if (agent.thirst <= 0f) agent.hp -= 0.50f * dt
        if (agent.warmth < 15f) agent.hp -= 0.25f * dt

        // Natural slow healing if well fed & resting
        if (agent.bleeding <= 0.1f && agent.hunger > 45f && agent.thirst > 45f && !agent.infected) {
            agent.hp = min(100f, agent.hp + (if (isSleeping) 0.45f else 0.12f) * dt)
        }

        // Bite infection incubation!
        if (agent.infected) {
            agent.infectionTimerTicks--
            if (agent.infectionTimerTicks < 180) {
                agent.sick = true
                agent.hp -= 0.28f * dt
            }
            if (agent.infectionTimerTicks <= 0 || agent.hp <= 0f) {
                sim.turnSurvivorIntoZombie(agent, "Превратился в зомби после укуса")
                return
            }
        }

        if (agent.hp <= 0f) {
            val cause = when {
                agent.bleeding > 1f -> "Потеря крови"
                agent.thirst <= 0f -> "Обезвоживание"
                agent.hunger <= 0f -> "Голод"
                agent.warmth < 15f -> "Переохлаждение"
                else -> "Погиб в схватке"
            }
            sim.killSurvivor(agent, cause)
        }
    }

    private fun computeVisionRadius(agent: SurvivorAgent, sim: WorldSimulation): Float {
        var r = if (sim.isNight) 5.5f else 11.5f
        if (sim.isNight && (agent.hasFlashlight || sim.isNearActiveCampfire(agent.x, agent.y, 6f))) {
            r += 4.5f
        }
        if (sim.weather == WeatherType.FOG) r *= 0.62f
        if (sim.weather == WeatherType.STORM && sim.lightningFlashTicks > 0) r = max(r, 12f)
        if (agent.hasTrait(SurvivorTrait.STEALTH)) r += 1.5f
        return r.coerceIn(4f, 14f)
    }

    private fun scanVisibleWorld(agent: SurvivorAgent, sim: WorldSimulation, visRadius: Float) {
        val ax = agent.x.toInt()
        val ay = agent.y.toInt()
        val r = visRadius.toInt()
        val minX = max(0, ax - r)
        val maxX = min(sim.map.size - 1, ax + r)
        val minY = max(0, ay - r)
        val maxY = min(sim.map.size - 1, ay + r)

        // Check loot containers in visible bounding box
        for (y in minY..maxY step 2) {
            for (x in minX..maxX step 2) {
                val tileIdx = sim.map.idx(x, y)
                val c = sim.map.lootByTile[tileIdx]
                if (c != null && !c.isEmpty() && sim.map.hasLineOfSight(ax, ay, x, y, r)) {
                    if (c.food > 0) agent.remember(MemoryKind.FOOD, x, y, c.buildingId, sim.tickCount, "Еда")
                    if (c.water > 0) agent.remember(MemoryKind.WATER, x, y, c.buildingId, sim.tickCount, "Вода")
                    if (c.bandages > 0 || c.antibiotics > 0) agent.remember(MemoryKind.MEDS, x, y, c.buildingId, sim.tickCount, "Медицина")
                    if (c.weapon != null || c.ammo > 0) agent.remember(MemoryKind.WEAPON, x, y, c.buildingId, sim.tickCount, "Оружие")
                    if (c.wood > 0 || c.scrap > 0) agent.remember(MemoryKind.MATERIALS, x, y, c.buildingId, sim.tickCount, "Материалы")
                }
            }
        }
    }

    private fun updateStress(
        agent: SurvivorAgent,
        visibleZombies: Int,
        visibleAllies: List<SurvivorAgent>,
        sim: WorldSimulation
    ) {
        var delta = -0.25f
        delta += visibleZombies * 0.55f
        if (agent.bleeding > 0f) delta += 0.6f
        if (agent.infected) delta += 0.7f
        if (sim.isNight && !agent.hasFlashlight) delta += 0.2f
        if (agent.hasTrait(SurvivorTrait.COWARD)) delta *= 1.5f
        if (agent.hasTrait(SurvivorTrait.BRAVE)) delta *= 0.55f
        if (visibleAllies.any { it.hasTrait(SurvivorTrait.LEADER) }) delta -= 0.45f
        agent.stress = (agent.stress + delta).coerceIn(0f, 100f)
    }

    private fun tryFormOrJoinCamp(
        agent: SurvivorAgent,
        visibleAllies: List<SurvivorAgent>,
        sim: WorldSimulation,
        rng: SimRng
    ) {
        if (agent.quarantined) return
        if (agent.campId != -1) return

        // Try joining an existing nearby friendly camp
        for (ally in visibleAllies) {
            if (ally.campId != -1) {
                val camp = sim.campsById[ally.campId] ?: continue
                val rel = agent.relations[ally.id] ?: 0
                val paranoiaPenalty = if (agent.hasTrait(SurvivorTrait.PARANOID) || ally.hasTrait(SurvivorTrait.PARANOID)) -12 else 0
                if (rel + paranoiaPenalty >= -5 && camp.memberIds.size < 8) {
                    sim.joinCamp(agent, camp)
                    return
                }
            }
        }

        // Form a new camp if inside a good building with an ally or if LEADER
        val tx = agent.x.toInt().coerceIn(0, sim.map.size - 1)
        val ty = agent.y.toInt().coerceIn(0, sim.map.size - 1)
        val bId = sim.map.buildingId[sim.map.idx(tx, ty)]
        if (bId != -1) {
            val bInfo = sim.map.buildings.getOrNull(bId - 1)
            if (bInfo != null && bInfo.claimedByCampId == -1) {
                val friendlyAlly = visibleAllies.firstOrNull { it.campId == -1 && (agent.relations[it.id] ?: 0) >= 0 }
                if (friendlyAlly != null && (agent.hasTrait(SurvivorTrait.LEADER) || rng.chance(0.08f))) {
                    val camp = sim.createCamp(agent, bInfo)
                    sim.joinCamp(friendlyAlly, camp)
                }
            }
        }
    }

    private fun handleCampDilemmas(
        agent: SurvivorAgent,
        visibleAllies: List<SurvivorAgent>,
        sim: WorldSimulation,
        rng: SimRng
    ) {
        val camp = sim.campsById[agent.campId] ?: return

        // 1. Infected ally dilemma!
        for (ally in visibleAllies) {
            if (ally.campId == camp.id && ally.infected && !ally.quarantined) {
                if (agent.hasTrait(SurvivorTrait.MEDIC) && agent.antibiotics > 0) {
                    agent.antibiotics--
                    ally.infectionTimerTicks += 280
                    ally.quarantined = true
                    agent.setThought("Лечу ${ally.name}, нужен карантин!", 55)
                    sim.logChronicle("Медицина", "${agent.name} дал(а) антибиотик укушенному ${ally.name} и изолировал(а) его.")
                    return
                }
                if ((agent.hasTrait(SurvivorTrait.PARANOID) || agent.hasTrait(SurvivorTrait.SELFISH)) && rng.chance(0.18f)) {
                    // Expel or liquidate depending on desperation
                    if (ally.infectionTimerTicks < 140 && agent.activeWeapon(1, false).isFirearm) {
                        sim.killSurvivor(ally, "Ликвидирован группой из-за укуса")
                        agent.setThought("Прости, ${ally.name}... иначе погибнем все.", 65)
                        sim.logChronicle("Драма", "${agent.name} застрелил(а) заражённого ${ally.name} в лагере «${camp.name}».", ally.x, ally.y)
                    } else {
                        sim.expelFromCamp(ally, camp)
                        ally.quarantined = true
                        agent.adjustRelation(ally.id, -40)
                        ally.adjustRelation(agent.id, -65)
                        agent.setThought("Уходи, ${ally.name}! Ты укушен!", 60)
                        sim.logChronicle("Изгнание", "Группа «${camp.name}» изгнала укушенного ${ally.name}.", ally.x, ally.y)
                    }
                    return
                }
            }
        }

        // 2. Scarcity food theft by SELFISH survivors
        if (agent.hasTrait(SurvivorTrait.SELFISH) && agent.hunger < 45f && camp.storageFood > 0 && rng.chance(0.06f)) {
            camp.storageFood--
            agent.hiddenPersonalFood++
            agent.setThought("Спрячу банку тушёнки про запас...", 40)
            for (ally in visibleAllies) {
                if (ally.campId == camp.id && rng.chance(0.45f)) {
                    ally.adjustRelation(agent.id, -28)
                    ally.setThought("${agent.name} ворует еду со склада!", 50)
                    sim.logChronicle("Конфликт", "${agent.name} пойман(а) на краже еды в лагере «${camp.name}».", agent.x, agent.y)
                }
            }
        }
    }

    private fun checkReactiveInterrupt(
        agent: SurvivorAgent,
        visibleZombies: Int,
        closestZombie: ZombieAgent?,
        closestZDist2: Float,
        sim: WorldSimulation,
        rng: SimRng
    ): Boolean {
        if (visibleZombies == 0 || closestZombie == null) return false

        val courage = computeCourage(agent, visibleZombies)
        val dist = sqrt(closestZDist2)

        // Very close threat -> immediate fight or flee!
        if (dist < 4.5f || (visibleZombies >= 3 && dist < 8.5f)) {
            val weapon = agent.activeWeapon(visibleZombies, false)
            val canFight = courage > 0.45f && agent.hp > 28f && (weapon != WeaponType.FISTS || visibleZombies == 1)
            if (canFight && (!agent.hasTrait(SurvivorTrait.COWARD) || dist < 2.2f)) {
                agent.goapPlan.clear()
                agent.goapPlan.addLast(GoapStep(GoapActionType.ATTACK_ZOMBIE, closestZombie.x.toInt(), closestZombie.y.toInt(), closestZombie.id))
                agent.fsmState = FsmState.FIGHTING
                agent.activeGoal = UtilityGoal.FIGHT_THREAT
                agent.currentActionIcon = "Бой"
                return true
            } else {
                // Flee!
                agent.goapPlan.clear()
                val safePt = pickEscapeTarget(agent, closestZombie, sim, rng)
                agent.goapPlan.addLast(GoapStep(GoapActionType.FLEE_TO_SAFE, safePt.first, safePt.second))
                agent.fsmState = FsmState.FLEEING
                agent.activeGoal = UtilityGoal.SURVIVE_FLEE
                agent.currentActionIcon = "Побег"
                if (rng.chance(0.22f)) agent.setThought("Слишком много тварей, бегу!", 38)
                return true
            }
        }
        return false
    }

    private fun evaluateUtilitiesAndPlan(
        agent: SurvivorAgent,
        visibleZombies: Int,
        closestZombie: ZombieAgent?,
        visibleAllies: List<SurvivorAgent>,
        sim: WorldSimulation,
        rng: SimRng
    ) {
        val scores = ArrayList<UtilityScore>(6)

        // 1. Heal self
        val healSelfScore = when {
            agent.bleeding > 1.2f -> 94f
            agent.hp < 55f -> (100f - agent.hp) * 1.15f
            else -> 0f
        } + if (agent.hasTrait(SurvivorTrait.MEDIC)) 8f else 0f
        scores.add(UtilityScore(UtilityGoal.HEAL_SELF, healSelfScore.coerceIn(0f, 100f),
            if (agent.bleeding > 0.5f) "Кровотечение (${agent.hp.toInt()} HP)" else "Здоровье ${agent.hp.toInt()}%"))

        // 2. Heal injured ally
        val hurtAlly = visibleAllies.firstOrNull { it.hp < 65f || it.bleeding > 0.4f }
        var healAllyScore = if (hurtAlly != null) (85f - hurtAlly.hp * 0.6f) else 0f
        if (hurtAlly != null) {
            if (agent.hasTrait(SurvivorTrait.MEDIC)) healAllyScore += 22f
            if (agent.hasTrait(SurvivorTrait.BRAVE)) healAllyScore += 10f
            if (agent.hasTrait(SurvivorTrait.SELFISH)) healAllyScore -= 25f
        }
        scores.add(UtilityScore(UtilityGoal.HEAL_ALLY, healAllyScore.coerceIn(0f, 100f),
            if (hurtAlly != null) "Ранен союзник ${hurtAlly.name}" else "Все целы"))

        // 3. Eat & Drink
        val eatScore = ((100f - agent.hunger) * 1.15f + if (agent.hunger < 25f) 20f else 0f).coerceIn(0f, 100f)
        val drinkScore = ((100f - agent.thirst) * 1.22f + if (agent.thirst < 25f) 24f else 0f).coerceIn(0f, 100f)
        scores.add(UtilityScore(UtilityGoal.EAT, eatScore, "Сытость ${agent.hunger.toInt()}%"))
        scores.add(UtilityScore(UtilityGoal.DRINK, drinkScore, "Жажда ${100 - agent.thirst.toInt()}%"))

        // 4. Sleep & Warmth
        var sleepScore = (100f - agent.energy) * (if (sim.isNight) 1.25f else 0.78f)
        if (visibleZombies > 0) sleepScore *= 0.25f
        scores.add(UtilityScore(UtilityGoal.SLEEP, sleepScore.coerceIn(0f, 100f), "Бодрость ${agent.energy.toInt()}%"))

        val warmScore = if (agent.warmth < 55f) ((65f - agent.warmth) * 1.35f).coerceIn(0f, 100f) else 0f
        scores.add(UtilityScore(UtilityGoal.WARM_UP, warmScore, "Тепло ${agent.warmth.toInt()}%"))

        // 5. Fight distant zombie if armed & brave
        val fightScore = if (closestZombie != null && computeCourage(agent, visibleZombies) > 0.55f) {
            58f + (if (agent.hasTrait(SurvivorTrait.BRAVE)) 18f else 0f)
        } else 0f
        scores.add(UtilityScore(UtilityGoal.FIGHT_THREAT, fightScore.coerceIn(0f, 100f), "Угроза рядом: $visibleZombies"))

        // 6. Fortify / build camp
        val camp = sim.campsById[agent.campId]
        var fortifyScore = if (camp != null) {
            var base = 36f
            if (agent.hasTrait(SurvivorTrait.ENGINEER) || agent.role == CampRole.BUILDER) base += 28f
            if (camp.memberIds.isNotEmpty() && !sim.hasCampStructure(camp.id, StructureType.CAMPFIRE)) base += 18f
            if (sim.needsDoorBarricade(camp)) base += 16f
            base
        } else 0f
        scores.add(UtilityScore(UtilityGoal.FORTIFY_CAMP, fortifyScore.coerceIn(0f, 100f),
            if (camp != null) "Укрепление «${camp.name}»" else "Нет лагеря"))

        // 7. Scavenge supplies
        var scavengeScore = 44f
        if (agent.food == 0 || agent.water == 0) scavengeScore += 22f
        if (agent.meleeWeapon == WeaponType.FISTS && agent.firearm == null) scavengeScore += 16f
        if (agent.role == CampRole.SCAVENGER) scavengeScore += 16f
        scores.add(UtilityScore(UtilityGoal.SCAVENGE, scavengeScore.coerceIn(0f, 100f), "Поиск еды/медицины/оружия"))

        // 8. Socialize & share memory
        val talkAlly = visibleAllies.firstOrNull { dist2(agent.x, agent.y, it.x, it.y) < 100f }
        var socialScore = if (talkAlly != null && agent.stress > 25f) 42f else if (talkAlly != null) 28f else 0f
        if (agent.hasTrait(SurvivorTrait.PARANOID)) socialScore *= 0.45f
        scores.add(UtilityScore(UtilityGoal.SOCIALIZE, socialScore.coerceIn(0f, 100f),
            if (talkAlly != null) "Рядом ${talkAlly.name}" else "Никого рядом"))

        // 9. Patrol / Explore
        val patrolScore = if (camp != null && agent.role == CampRole.SENTRY) 52f else if (camp != null) 24f else 0f
        scores.add(UtilityScore(UtilityGoal.PATROL_OR_GUARD, patrolScore, "Охрана периметра"))
        scores.add(UtilityScore(UtilityGoal.EXPLORE, 30f, "Разведка новых кварталов"))

        scores.sortByDescending { it.score }
        agent.topUtilities.clear()
        for (i in 0 until min(3, scores.size)) agent.topUtilities.add(scores[i])

        val winner = scores[0]
        agent.activeGoal = winner.goal
        buildGoapPlanForGoal(agent, winner.goal, hurtAlly, closestZombie, talkAlly, camp, sim, rng)
    }

    private fun buildGoapPlanForGoal(
        agent: SurvivorAgent,
        goal: UtilityGoal,
        hurtAlly: SurvivorAgent?,
        closestZombie: ZombieAgent?,
        talkAlly: SurvivorAgent?,
        camp: CampFaction?,
        sim: WorldSimulation,
        rng: SimRng
    ) {
        agent.goapPlan.clear()
        val isSneak = agent.hasTrait(SurvivorTrait.STEALTH) || sim.isNight
        val moveType = if (isSneak) GoapActionType.SNEAK_TO else GoapActionType.MOVE_TO

        when (goal) {
            UtilityGoal.EAT -> {
                if (agent.food > 0 || agent.hiddenPersonalFood > 0) {
                    agent.goapPlan.addLast(GoapStep(GoapActionType.EAT_INV, detail = "Из рюкзака"))
                    agent.setThought("Перекушу из запасов.", 35)
                } else if (camp != null && camp.storageFood > 0) {
                    agent.goapPlan.addLast(GoapStep(moveType, camp.centerX, camp.centerY, detail = "К складу лагеря"))
                    agent.goapPlan.addLast(GoapStep(GoapActionType.EAT_INV, detail = "Взять еду со склада"))
                    agent.setThought("Иду на склад за едой.", 40)
                } else {
                    val mem = findBestMemory(agent, MemoryKind.FOOD, sim.tickCount)
                    if (mem != null) {
                        agent.goapPlan.addLast(GoapStep(moveType, mem.x, mem.y, detail = "По памяти: ${mem.note}"))
                        agent.goapPlan.addLast(GoapStep(GoapActionType.LOOT_CONTAINER, mem.x, mem.y, detail = "Взять еду"))
                        agent.setThought("Вспомнил, где видел еду!", 45)
                    } else {
                        val pt = pickExploreTarget(agent, sim, rng)
                        agent.goapPlan.addLast(GoapStep(moveType, pt.first, pt.second, detail = "Искать магазин/дом"))
                        agent.setThought("Нужно срочно найти еду...", 45)
                    }
                }
            }

            UtilityGoal.DRINK -> {
                if (agent.water > 0) {
                    agent.goapPlan.addLast(GoapStep(GoapActionType.DRINK_INV, detail = "Фляга в рюкзаке"))
                } else if (camp != null && camp.storageWater > 0) {
                    agent.goapPlan.addLast(GoapStep(moveType, camp.centerX, camp.centerY, detail = "Вода в лагере"))
                    agent.goapPlan.addLast(GoapStep(GoapActionType.DRINK_INV, detail = "Попить на базе"))
                } else {
                    val mem = findBestMemory(agent, MemoryKind.WATER, sim.tickCount)
                    if (mem != null) {
                        agent.goapPlan.addLast(GoapStep(moveType, mem.x, mem.y, detail = "За водой по памяти"))
                        agent.goapPlan.addLast(GoapStep(GoapActionType.LOOT_CONTAINER, mem.x, mem.y, detail = "Взять воду"))
                    } else {
                        val pt = pickExploreTarget(agent, sim, rng)
                        agent.goapPlan.addLast(GoapStep(moveType, pt.first, pt.second, detail = "Искать воду"))
                    }
                }
            }

            UtilityGoal.HEAL_SELF -> {
                if (agent.bandages > 0) {
                    agent.goapPlan.addLast(GoapStep(GoapActionType.TREAT_SELF, detail = "Перевязать раны"))
                } else {
                    val mem = findBestMemory(agent, MemoryKind.MEDS, sim.tickCount)
                    if (mem != null) {
                        agent.goapPlan.addLast(GoapStep(moveType, mem.x, mem.y, detail = "В аптеку за бинтами"))
                        agent.goapPlan.addLast(GoapStep(GoapActionType.LOOT_CONTAINER, mem.x, mem.y, detail = "Взять бинты"))
                        agent.goapPlan.addLast(GoapStep(GoapActionType.TREAT_SELF, detail = "Перевязать себя"))
                        agent.setThought("Нужны бинты! Бегу в аптеку по памяти.", 50)
                    } else {
                        val pt = pickExploreTarget(agent, sim, rng)
                        agent.goapPlan.addLast(GoapStep(moveType, pt.first, pt.second, detail = "Искать аптечку"))
                    }
                }
            }

            UtilityGoal.HEAL_ALLY -> {
                if (hurtAlly != null) {
                    if (agent.bandages > 0) {
                        agent.goapPlan.addLast(GoapStep(moveType, hurtAlly.x.toInt(), hurtAlly.y.toInt(), hurtAlly.id, "К ${hurtAlly.name}"))
                        agent.goapPlan.addLast(GoapStep(GoapActionType.TREAT_ALLY, hurtAlly.x.toInt(), hurtAlly.y.toInt(), hurtAlly.id, "Перевязать ${hurtAlly.name}"))
                        agent.setThought("Держись, ${hurtAlly.name}, сейчас помогу!", 50)
                    } else {
                        val mem = findBestMemory(agent, MemoryKind.MEDS, sim.tickCount)
                        if (mem != null) {
                            agent.goapPlan.addLast(GoapStep(moveType, mem.x, mem.y, detail = "За бинтами для ${hurtAlly.name}"))
                            agent.goapPlan.addLast(GoapStep(GoapActionType.LOOT_CONTAINER, mem.x, mem.y, detail = "Взять аптечку"))
                            agent.goapPlan.addLast(GoapStep(GoapActionType.TREAT_ALLY, hurtAlly.x.toInt(), hurtAlly.y.toInt(), hurtAlly.id, "Вернуться к ${hurtAlly.name}"))
                        }
                    }
                }
            }

            UtilityGoal.SLEEP, UtilityGoal.WARM_UP -> {
                if (camp != null) {
                    agent.goapPlan.addLast(GoapStep(moveType, camp.centerX, camp.centerY, detail = "Вернуться в лагерь"))
                } else {
                    val nearestB = findNearestBuilding(agent, sim)
                    if (nearestB != null) {
                        agent.goapPlan.addLast(GoapStep(moveType, nearestB.centerX.toInt(), nearestB.centerY.toInt(), detail = "Укрыться в ${nearestB.type.titleRu}"))
                    }
                }
                agent.goapPlan.addLast(GoapStep(GoapActionType.SLEEP_TICK, detail = "Восстановить силы"))
            }

            UtilityGoal.FIGHT_THREAT -> {
                if (closestZombie != null) {
                    agent.goapPlan.addLast(GoapStep(GoapActionType.ATTACK_ZOMBIE, closestZombie.x.toInt(), closestZombie.y.toInt(), closestZombie.id, "Ликвидировать зомби"))
                }
            }

            UtilityGoal.FORTIFY_CAMP -> {
                if (camp != null) {
                    val doorToBarricade = findUnbarricadedDoor(camp, sim)
                    if (agent.wood + camp.storageWood >= 2 && doorToBarricade != -1) {
                        val dx = doorToBarricade % sim.map.size
                        val dy = doorToBarricade / sim.map.size
                        agent.goapPlan.addLast(GoapStep(GoapActionType.MOVE_TO, dx, dy, detail = "К двери базы"))
                        agent.goapPlan.addLast(GoapStep(GoapActionType.BARRICADE_DOOR, dx, dy, detail = "Заколотить дверь"))
                        agent.setThought("Укреплю вход досками!", 45)
                    } else if (agent.wood + camp.storageWood >= 3 && !sim.hasCampStructure(camp.id, StructureType.CAMPFIRE)) {
                        agent.goapPlan.addLast(GoapStep(GoapActionType.MOVE_TO, camp.centerX, camp.centerY, detail = "В центр базы"))
                        agent.goapPlan.addLast(GoapStep(GoapActionType.BUILD_STRUCTURE, camp.centerX, camp.centerY, 0, "Развести костёр"))
                    } else if (agent.wood + camp.storageWood >= 3 && !sim.hasCampStructure(camp.id, StructureType.RAIN_COLLECTOR)) {
                        agent.goapPlan.addLast(GoapStep(GoapActionType.MOVE_TO, camp.centerX + 1, camp.centerY, detail = "Поставить дождесборник"))
                        agent.goapPlan.addLast(GoapStep(GoapActionType.BUILD_STRUCTURE, camp.centerX + 1, camp.centerY, 1, "Собрать дождесборник"))
                    } else {
                        val mem = findBestMemory(agent, MemoryKind.MATERIALS, sim.tickCount)
                        if (mem != null) {
                            agent.goapPlan.addLast(GoapStep(moveType, mem.x, mem.y, detail = "За стройматериалами"))
                            agent.goapPlan.addLast(GoapStep(GoapActionType.LOOT_CONTAINER, mem.x, mem.y, detail = "Взять доски/лом"))
                        } else {
                            val pt = pickExploreTarget(agent, sim, rng)
                            agent.goapPlan.addLast(GoapStep(moveType, pt.first, pt.second, detail = "Искать доски и лом"))
                        }
                    }
                }
            }

            UtilityGoal.SOCIALIZE -> {
                if (talkAlly != null) {
                    agent.goapPlan.addLast(GoapStep(GoapActionType.MOVE_TO, talkAlly.x.toInt(), talkAlly.y.toInt(), talkAlly.id, "Подойти к ${talkAlly.name}"))
                    agent.goapPlan.addLast(GoapStep(GoapActionType.TALK_ALLY, talkAlly.x.toInt(), talkAlly.y.toInt(), talkAlly.id, "Обмен новостями"))
                }
            }

            UtilityGoal.PATROL_OR_GUARD -> {
                if (camp != null) {
                    val px = (camp.centerX + rng.nextRange(-5, 5)).coerceIn(2, sim.map.size - 3)
                    val py = (camp.centerY + rng.nextRange(-5, 5)).coerceIn(2, sim.map.size - 3)
                    agent.goapPlan.addLast(GoapStep(GoapActionType.PATROL_STEP, px, py, detail = "Дозор вокруг базы"))
                }
            }

            UtilityGoal.SCAVENGE, UtilityGoal.EXPLORE -> {
                val desiredKind = when {
                    agent.meleeWeapon == WeaponType.FISTS && agent.firearm == null -> MemoryKind.WEAPON
                    agent.food <= 1 -> MemoryKind.FOOD
                    agent.water <= 1 -> MemoryKind.WATER
                    else -> MemoryKind.MATERIALS
                }
                val mem = findBestMemory(agent, desiredKind, sim.tickCount)
                if (mem != null && goal == UtilityGoal.SCAVENGE) {
                    agent.goapPlan.addLast(GoapStep(moveType, mem.x, mem.y, detail = "Рейд: ${mem.note}"))
                    agent.goapPlan.addLast(GoapStep(GoapActionType.LOOT_CONTAINER, mem.x, mem.y, detail = "Обыскать"))
                    agent.setThought("Иду в рейд за припасами.", 40)
                } else {
                    val pt = pickExploreTarget(agent, sim, rng)
                    agent.goapPlan.addLast(GoapStep(moveType, pt.first, pt.second, detail = "Разведка сектора"))
                }
            }

            UtilityGoal.SURVIVE_FLEE -> {
                val pt = pickExploreTarget(agent, sim, rng)
                agent.goapPlan.addLast(GoapStep(GoapActionType.FLEE_TO_SAFE, pt.first, pt.second, detail = "Отход в укрытие"))
            }
        }
    }

    private fun executeCurrentStep(
        agent: SurvivorAgent,
        visibleZombies: Int,
        closestZombie: ZombieAgent?,
        sim: WorldSimulation,
        dt: Float,
        rng: SimRng
    ) {
        val step = agent.goapPlan.firstOrNull()
        if (step == null) {
            agent.fsmState = FsmState.IDLE
            agent.currentActionIcon = "Ждёт"
            return
        }

        when (step.action) {
            GoapActionType.MOVE_TO, GoapActionType.SNEAK_TO, GoapActionType.FLEE_TO_SAFE, GoapActionType.PATROL_STEP -> {
                val sneaking = step.action == GoapActionType.SNEAK_TO
                val fleeing = step.action == GoapActionType.FLEE_TO_SAFE
                agent.fsmState = when {
                    fleeing -> FsmState.FLEEING
                    sneaking -> FsmState.SNEAKING
                    else -> FsmState.MOVING
                }
                agent.currentActionIcon = when {
                    fleeing -> "Бежит"
                    sneaking -> "Крадётся"
                    step.action == GoapActionType.PATROL_STEP -> "Дозор"
                    else -> "Идёт"
                }

                // Dynamic target update if chasing moving ally
                val gx = if (step.targetEntityId > 0) {
                    sim.survivorsById[step.targetEntityId]?.x?.toInt() ?: step.targetX
                } else step.targetX
                val gy = if (step.targetEntityId > 0) {
                    sim.survivorsById[step.targetEntityId]?.y?.toInt() ?: step.targetY
                } else step.targetY

                val speed = computeMoveSpeed(agent, sneaking, fleeing, sim)
                val reached = moveAlongPath(agent, gx, gy, speed, dt, fleeing, sneaking, sim)
                if (reached) {
                    agent.goapPlan.removeFirstOrNull()
                }
            }

            GoapActionType.LOOT_CONTAINER -> {
                agent.fsmState = FsmState.LOOTING
                agent.currentActionIcon = "Лутает"
                val c = sim.map.lootByTile[sim.map.idx(step.targetX.coerceIn(0, sim.map.size - 1), step.targetY.coerceIn(0, sim.map.size - 1))]
                if (c != null && !c.isEmpty()) {
                    takeLootFromContainer(agent, c, sim)
                }
                agent.forgetNear(MemoryKind.FOOD, step.targetX, step.targetY)
                agent.forgetNear(MemoryKind.WATER, step.targetX, step.targetY)
                agent.forgetNear(MemoryKind.MEDS, step.targetX, step.targetY)
                agent.forgetNear(MemoryKind.WEAPON, step.targetX, step.targetY)
                agent.forgetNear(MemoryKind.MATERIALS, step.targetX, step.targetY)
                agent.goapPlan.removeFirstOrNull()
            }

            GoapActionType.EAT_INV -> {
                if (agent.hiddenPersonalFood > 0) {
                    agent.hiddenPersonalFood--
                    agent.hunger = min(100f, agent.hunger + 48f)
                } else if (agent.food > 0) {
                    agent.food--
                    agent.hunger = min(100f, agent.hunger + 48f)
                } else {
                    val camp = sim.campsById[agent.campId]
                    if (camp != null && camp.storageFood > 0) {
                        camp.storageFood--
                        agent.hunger = min(100f, agent.hunger + 48f)
                    }
                }
                agent.setThought("Поел, стало легче.", 35)
                agent.goapPlan.removeFirstOrNull()
            }

            GoapActionType.DRINK_INV -> {
                if (agent.water > 0) {
                    agent.water--
                    agent.thirst = min(100f, agent.thirst + 55f)
                } else {
                    val camp = sim.campsById[agent.campId]
                    if (camp != null && camp.storageWater > 0) {
                        camp.storageWater--
                        agent.thirst = min(100f, agent.thirst + 55f)
                    }
                }
                agent.goapPlan.removeFirstOrNull()
            }

            GoapActionType.TREAT_SELF -> {
                agent.fsmState = FsmState.HEALING
                agent.currentActionIcon = "Лечит"
                if (agent.bandages > 0) {
                    agent.bandages--
                    agent.bleeding = 0f
                    agent.hp = min(100f, agent.hp + (if (agent.hasTrait(SurvivorTrait.MEDIC)) 38f else 26f))
                    agent.limping = false
                    agent.setThought("Перевязал рану.", 40)
                }
                agent.goapPlan.removeFirstOrNull()
            }

            GoapActionType.TREAT_ALLY -> {
                agent.fsmState = FsmState.HEALING
                agent.currentActionIcon = "Лечит"
                val ally = sim.survivorsById[step.targetEntityId]
                if (ally != null && ally.alive && dist2(agent.x, agent.y, ally.x, ally.y) <= 6.25f && agent.bandages > 0) {
                    agent.bandages--
                    ally.bleeding = 0f
                    ally.hp = min(100f, ally.hp + (if (agent.hasTrait(SurvivorTrait.MEDIC)) 44f else 28f))
                    ally.limping = false
                    ally.adjustRelation(agent.id, 24)
                    agent.adjustRelation(ally.id, 14)
                    ally.addBio(sim.day, sim.formatTime(), "${agent.name} перевязал(а) мне раны")
                    agent.addBio(sim.day, sim.formatTime(), "Спас(ла) раненого ${ally.name}")
                    agent.setThought("Держись, ${ally.name}!", 45)
                }
                agent.goapPlan.removeFirstOrNull()
            }

            GoapActionType.SLEEP_TICK -> {
                agent.fsmState = FsmState.SLEEPING
                agent.currentActionIcon = "Спит"
                if (agent.energy >= 88f && agent.warmth >= 60f) {
                    agent.goapPlan.removeFirstOrNull()
                    agent.fsmState = FsmState.IDLE
                }
            }

            GoapActionType.BARRICADE_DOOR -> {
                agent.fsmState = FsmState.BUILDING
                agent.currentActionIcon = "Строит"
                val camp = sim.campsById[agent.campId]
                val ti = sim.map.idx(step.targetX.coerceIn(0, sim.map.size - 1), step.targetY.coerceIn(0, sim.map.size - 1))
                val hasWood = agent.wood >= 2 || (camp != null && camp.storageWood >= 2)
                if (hasWood && (sim.map.wall[ti] == WallType.DOOR_CLOSED || sim.map.wall[ti] == WallType.DOOR_OPEN || sim.map.wall[ti] == WallType.WINDOW_BROKEN)) {
                    if (agent.wood >= 2) agent.wood -= 2 else if (camp != null) camp.storageWood -= 2
                    sim.map.wall[ti] = WallType.DOOR_BARRICADED
                    sim.map.wallHp[ti] = 220f
                    sim.map.addNoise(step.targetX, step.targetY, 10f, 35f)
                    sim.soundRings.spawn(step.targetX + 0.5f, step.targetY + 0.5f, 9f, 0xFFF59E0B.toInt())
                    agent.setThought("Проход заколочен!", 40)
                    agent.addBio(sim.day, sim.formatTime(), "Забаррикадировал(а) вход в убежище")
                }
                agent.goapPlan.removeFirstOrNull()
            }

            GoapActionType.BUILD_STRUCTURE -> {
                agent.fsmState = FsmState.BUILDING
                agent.currentActionIcon = "Строит"
                val camp = sim.campsById[agent.campId]
                if (camp != null) {
                    val sType = if (step.targetEntityId == 1) StructureType.RAIN_COLLECTOR else StructureType.CAMPFIRE
                    if (agent.wood + camp.storageWood >= sType.woodCost) {
                        var rem = sType.woodCost
                        val fromInv = min(rem, agent.wood)
                        agent.wood -= fromInv
                        rem -= fromInv
                        camp.storageWood = max(0, camp.storageWood - rem)
                        sim.addCampStructure(CampStructure(sim.nextStructureId++, sType, step.targetX, step.targetY, camp.id))
                        agent.setThought("Построен ${sType.titleRu}!", 45)
                        sim.logChronicle("База", "В лагере «${camp.name}» ${agent.name} построил(а) ${sType.titleRu}.", step.targetX.toFloat(), step.targetY.toFloat())
                    }
                }
                agent.goapPlan.removeFirstOrNull()
            }

            GoapActionType.TALK_ALLY -> {
                agent.fsmState = FsmState.TALKING
                agent.currentActionIcon = "Общается"
                val ally = sim.survivorsById[step.targetEntityId]
                if (ally != null && ally.alive && dist2(agent.x, agent.y, ally.x, ally.y) <= 9f) {
                    shareMemories(agent, ally, sim.tickCount)
                    agent.adjustRelation(ally.id, 8)
                    ally.adjustRelation(agent.id, 8)
                    agent.stress = max(0f, agent.stress - 8f)
                    ally.stress = max(0f, ally.stress - 8f)
                    agent.setThought("Обменялись картой с ${ally.name}.", 42)
                }
                agent.goapPlan.removeFirstOrNull()
            }

            GoapActionType.ATTACK_ZOMBIE -> {
                agent.fsmState = FsmState.FIGHTING
                agent.currentActionIcon = "Бой"
                val target = sim.zombiesById[step.targetEntityId] ?: closestZombie
                if (target == null || !target.alive) {
                    agent.goapPlan.removeFirstOrNull()
                    return
                }
                val weapon = agent.activeWeapon(visibleZombies, false)
                val d = sqrt(dist2(agent.x, agent.y, target.x, target.y))
                if (d > weapon.rangeTiles) {
                    val speed = computeMoveSpeed(agent, false, false, sim)
                    moveAlongPath(agent, target.x.toInt(), target.y.toInt(), speed, dt, false, false, sim)
                } else if (agent.combatCooldown <= 0) {
                    performAttack(agent, target, weapon, sim, rng)
                }
            }

            else -> agent.goapPlan.removeFirstOrNull()
        }
    }

    private fun performAttack(
        agent: SurvivorAgent,
        target: ZombieAgent,
        weapon: WeaponType,
        sim: WorldSimulation,
        rng: SimRng
    ) {
        agent.combatCooldown = weapon.cooldownTicks
        val accuracyPenalty = if (agent.stress > 65f) 0.72f else 1.0f
        val hitChance = (if (weapon.isFirearm) 0.88f else 0.94f) * accuracyPenalty

        if (weapon.isFirearm && agent.ammo > 0) {
            agent.ammo--
            sim.tracers.spawn(agent.x, agent.y, target.x, target.y)
            sim.map.addNoise(agent.x.toInt(), agent.y.toInt(), weapon.noiseRadius, 95f)
            sim.soundRings.spawn(agent.x, agent.y, weapon.noiseRadius * 0.45f, 0xFFEF4444.toInt(), 1.0f)
            agent.setThought("Выстрел из ${weapon.titleRu}!", 30)
        } else {
            agent.stamina = max(0f, agent.stamina - 8f)
            sim.map.addNoise(agent.x.toInt(), agent.y.toInt(), weapon.noiseRadius, 24f)
        }

        if (rng.chance(hitChance)) {
            target.hp -= weapon.damage
            sim.map.addBlood(target.x.toInt().coerceIn(0, sim.map.size - 1), target.y.toInt().coerceIn(0, sim.map.size - 1), 18f)
            repeat(3) {
                sim.particles.spawn(
                    target.x, target.y,
                    rng.nextFloatRange(-2f, 2f), rng.nextFloatRange(-2f, 2f),
                    0.45f, 0xFF991B1B.toInt(), 0.14f
                )
            }
            if (target.hp <= 0f) {
                sim.killZombie(target, agent)
                agent.goapPlan.removeFirstOrNull()
            }
        }
    }

    private fun takeLootFromContainer(agent: SurvivorAgent, c: LootContainer, sim: WorldSimulation) {
        agent.food += c.food; c.food = 0
        agent.water += c.water; c.water = 0
        agent.bandages += c.bandages; c.bandages = 0
        agent.antibiotics += c.antibiotics; c.antibiotics = 0
        agent.wood += c.wood; c.wood = 0
        agent.scrap += c.scrap; c.scrap = 0
        agent.ammo += c.ammo; c.ammo = 0
        if (c.hasFlashlight) {
            agent.hasFlashlight = true
            c.hasFlashlight = false
        }
        val w = c.weapon
        if (w != null) {
            if (w.isFirearm) {
                if (agent.firearm == null || w.damage > agent.firearm!!.damage) {
                    agent.firearm = w
                    c.weapon = null
                }
            } else if (w.damage > agent.meleeWeapon.damage) {
                agent.meleeWeapon = w
                c.weapon = null
            }
        }
        c.searched = true

        // Deposit surplus to camp storage if nearby
        val camp = sim.campsById[agent.campId]
        if (camp != null && dist2(agent.x, agent.y, camp.centerX.toFloat(), camp.centerY.toFloat()) < 144f) {
            if (agent.food > 2) { camp.storageFood += agent.food - 2; agent.food = 2 }
            if (agent.water > 2) { camp.storageWater += agent.water - 2; agent.water = 2 }
            if (agent.wood > 0) { camp.storageWood += agent.wood; agent.wood = 0 }
        }
        val noiseR = if (agent.hasTrait(SurvivorTrait.STEALTH)) 3f else 7f
        sim.map.addNoise(c.x, c.y, noiseR, 20f)
    }

    private fun shareMemories(a: SurvivorAgent, b: SurvivorAgent, tick: Int) {
        for (i in 0 until min(6, a.memories.size)) {
            val m = a.memories[a.memories.size - 1 - i]
            b.remember(m.kind, m.x, m.y, m.buildingId, tick, m.note)
        }
        for (i in 0 until min(6, b.memories.size)) {
            val m = b.memories[b.memories.size - 1 - i]
            a.remember(m.kind, m.x, m.y, m.buildingId, tick, m.note)
        }
    }

    private fun computeCourage(agent: SurvivorAgent, visibleZombies: Int): Float {
        var c = 0.60f
        if (agent.hasTrait(SurvivorTrait.BRAVE)) c += 0.30f
        if (agent.hasTrait(SurvivorTrait.COWARD)) c -= 0.42f
        if (agent.firearm != null && agent.ammo > 0) c += 0.25f
        else if (agent.meleeWeapon != WeaponType.FISTS) c += 0.12f
        c -= (visibleZombies - 1) * 0.14f
        c -= agent.stress * 0.004f
        return c.coerceIn(0f, 1f)
    }

    private fun computeMoveSpeed(agent: SurvivorAgent, sneaking: Boolean, fleeing: Boolean, sim: WorldSimulation): Float {
        var s = 2.6f
        if (sneaking) s = 1.65f
        if (fleeing && agent.stamina > 8f) {
            s = 3.85f
            agent.stamina = max(0f, agent.stamina - 0.5f)
        }
        if (agent.limping) s *= 0.62f
        if (agent.energy < 20f) s *= 0.78f
        val tx = agent.x.toInt().coerceIn(0, sim.map.size - 1)
        val ty = agent.y.toInt().coerceIn(0, sim.map.size - 1)
        val t = sim.map.terrain[sim.map.idx(tx, ty)]
        if (t == TerrainType.ROAD || t == TerrainType.BRIDGE) s *= 1.14f
        if (t == TerrainType.FOREST) s *= 0.85f
        return s
    }

    private fun moveAlongPath(
        agent: SurvivorAgent,
        gx: Int,
        gy: Int,
        speed: Float,
        dt: Float,
        fleeing: Boolean,
        sneaking: Boolean,
        sim: WorldSimulation
    ): Boolean {
        val cx = agent.x.toInt().coerceIn(0, sim.map.size - 1)
        val cy = agent.y.toInt().coerceIn(0, sim.map.size - 1)
        if (abs(cx - gx) <= 1 && abs(cy - gy) <= 1) return true

        val needRecompute = agent.pathCursor >= agent.currentPath.size ||
            (agent.currentPath.isNotEmpty() && run {
                val last = agent.currentPath.last()
                abs(last % sim.map.size - gx) > 2 || abs(last / sim.map.size - gy) > 2
            })

        if (needRecompute) {
            agent.pathCursor = 0
            val ok = sim.map.findPath(cx, cy, gx, gy, avoidDanger = !fleeing, maxExpansions = 1600, outPath = agent.currentPath)
            if (!ok) {
                agent.goapPlan.clear()
                return true
            }
        }

        if (agent.pathCursor >= agent.currentPath.size) return true
        val nextNode = agent.currentPath[agent.pathCursor]
        val nx = nextNode % sim.map.size
        val ny = nextNode / sim.map.size

        // Open / close door tactically
        val ni = sim.map.idx(nx, ny)
        if (sim.map.wall[ni] == WallType.DOOR_CLOSED) {
            sim.map.wall[ni] = WallType.DOOR_OPEN
        }

        val tx = nx + 0.5f
        val ty = ny + 0.5f
        val dx = tx - agent.x
        val dy = ty - agent.y
        val d = sqrt(dx * dx + dy * dy)
        val step = speed * dt
        if (d <= step + 0.08f) {
            agent.x = tx
            agent.y = ty
            agent.pathCursor++
            // Close door behind when fleeing or sneaking!
            if (agent.pathCursor >= 2) {
                val prev = agent.currentPath[agent.pathCursor - 2]
                if (sim.map.wall[prev] == WallType.DOOR_OPEN && (fleeing || sneaking)) {
                    sim.map.wall[prev] = WallType.DOOR_CLOSED
                }
            }
        } else if (d > 0.001f) {
            agent.x += (dx / d) * step
            agent.y += (dy / d) * step
        }

        // Emit movement noise
        val noiseR = when {
            sneaking -> if (agent.hasTrait(SurvivorTrait.STEALTH)) 2f else 3f
            fleeing -> 10f
            else -> 5f
        }
        sim.map.addNoise(agent.x.toInt().coerceIn(0, sim.map.size - 1), agent.y.toInt().coerceIn(0, sim.map.size - 1), noiseR, if (fleeing) 32f else 12f)
        return false
    }

    private fun findBestMemory(agent: SurvivorAgent, kind: MemoryKind, currentTick: Int): MemoryEntry? {
        var best: MemoryEntry? = null
        var bestScore = Float.NEGATIVE_INFINITY
        for (i in 0 until agent.memories.size) {
            val m = agent.memories[i]
            if (m.kind != kind) continue
            val age = max(1, currentTick - m.seenTick)
            val d = sqrt(dist2(agent.x, agent.y, m.x.toFloat(), m.y.toFloat()))
            val score = 100f - d - age * 0.04f
            if (score > bestScore) {
                bestScore = score
                best = m
            }
        }
        return best
    }

    private fun pickEscapeTarget(agent: SurvivorAgent, threat: ZombieAgent, sim: WorldSimulation, rng: SimRng): Pair<Int, Int> {
        val camp = sim.campsById[agent.campId]
        if (camp != null && dist2(camp.centerX.toFloat(), camp.centerY.toFloat(), threat.x, threat.y) > 36f) {
            return camp.centerX to camp.centerY
        }
        val dx = agent.x - threat.x
        val dy = agent.y - threat.y
        val len = max(0.1f, sqrt(dx * dx + dy * dy))
        val ex = (agent.x + (dx / len) * 16f + rng.nextRange(-4, 4)).toInt().coerceIn(2, sim.map.size - 3)
        val ey = (agent.y + (dy / len) * 16f + rng.nextRange(-4, 4)).toInt().coerceIn(2, sim.map.size - 3)
        return ex to ey
    }

    private fun pickExploreTarget(agent: SurvivorAgent, sim: WorldSimulation, rng: SimRng): Pair<Int, Int> {
        // Bias toward unvisited buildings
        if (sim.map.buildings.isNotEmpty() && rng.chance(0.7f)) {
            val b = sim.map.buildings[rng.nextInt(sim.map.buildings.size)]
            return b.centerX.toInt() to b.centerY.toInt()
        }
        val tx = (agent.x.toInt() + rng.nextRange(-26, 26)).coerceIn(3, sim.map.size - 4)
        val ty = (agent.y.toInt() + rng.nextRange(-26, 26)).coerceIn(3, sim.map.size - 4)
        return tx to ty
    }

    private fun findNearestBuilding(agent: SurvivorAgent, sim: WorldSimulation): BuildingInfo? {
        var best: BuildingInfo? = null
        var bestD = Float.MAX_VALUE
        for (b in sim.map.buildings) {
            val d = dist2(agent.x, agent.y, b.centerX, b.centerY)
            if (d < bestD) {
                bestD = d
                best = b
            }
        }
        return best
    }

    private fun findUnbarricadedDoor(camp: CampFaction, sim: WorldSimulation): Int {
        val b = sim.map.buildings.getOrNull(camp.homeBuildingId - 1) ?: return -1
        for (ti in b.doorTiles) {
            val w = sim.map.wall[ti]
            if (w == WallType.DOOR_CLOSED || w == WallType.DOOR_OPEN) return ti
        }
        return -1
    }

    private fun dist2(x1: Float, y1: Float, x2: Float, y2: Float): Float {
        val dx = x1 - x2
        val dy = y1 - y2
        return dx * dx + dy * dy
    }
}
