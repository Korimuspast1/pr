package com.zombieaquarium.sim

import com.zombieaquarium.sim.engine.SimConfig
import com.zombieaquarium.sim.engine.WeatherType
import com.zombieaquarium.sim.engine.WorldSimulation
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class SimulationStressTest {
    @Test
    fun test650TicksNoCrashOrNaN() {
        val cfg = SimConfig(
            mapSize = 128,
            initialSurvivors = 24,
            zombieDensity = 1.0f,
            lootRarity = 1.0f,
            runnerRatio = 0.18f,
            hordeFrequency = 1.2f,
            seed = 424242L
        )
        val sim = WorldSimulation(cfg)
        assertTrue("Expected dense town buildings generated", sim.map.buildings.size >= 35)
        assertTrue("Expected loot containers generated", sim.map.lootContainers.isNotEmpty())
        assertTrue("Expected survivors spawned", sim.survivors.size == 24)
        assertTrue("Expected zombies spawned", sim.zombies.size >= 45)

        var sawSound = false
        for (tick in 1..650) {
            sim.stepFixedTick()
            if (sim.pendingSounds.isNotEmpty()) {
                sawSound = true
                sim.pendingSounds.clear()
            }
            if (tick == 120) {
                sim.godDropSupplyCrate(64f, 64f)
                sim.godSetWeather(WeatherType.STORM)
            }
            if (tick == 250) {
                sim.godSpawnHorde(50f, 50f)
                sim.godSpawnSurvivor(70f, 70f)
            }
        }
        assertTrue("Expected real sound events emitted during simulation", sawSound)
        assertTrue("Expected character animations advancing", sim.survivors.any { it.animPhase > 0f })

        for (s in sim.survivors) {
            assertFalse("Survivor X NaN", s.x.isNaN())
            assertFalse("Survivor Y NaN", s.y.isNaN())
            assertFalse("Survivor HP NaN", s.hp.isNaN())
            assertTrue("Survivor in bounds X", s.x >= 0f && s.x < cfg.mapSize)
            assertTrue("Survivor in bounds Y", s.y >= 0f && s.y < cfg.mapSize)
        }
        for (z in sim.zombies) {
            assertFalse("Zombie X NaN", z.x.isNaN())
            assertFalse("Zombie Y NaN", z.y.isNaN())
            assertTrue("Zombie in bounds X", z.x >= 0f && z.x < cfg.mapSize)
            assertTrue("Zombie in bounds Y", z.y >= 0f && z.y < cfg.mapSize)
        }
        assertTrue("Chronicle should record events", sim.chronicle.size >= 2)
    }

    @Test
    fun testPathfinderFindsRoute() {
        val sim = WorldSimulation(SimConfig(mapSize = 128, initialSurvivors = 10, seed = 999L))
        val s0 = sim.survivors[0]
        val s1 = sim.survivors[1]
        val path = ArrayList<Int>()
        val ok = sim.map.findPath(
            s0.x.toInt(),
            s0.y.toInt(),
            s1.x.toInt(),
            s1.y.toInt(),
            avoidDanger = false,
            maxExpansions = 12000,
            outPath = path
        )
        assertTrue("A* should find path or partial route", ok)
        assertTrue("Path should not be empty", path.isNotEmpty() || (s0.x.toInt() == s1.x.toInt() && s0.y.toInt() == s1.y.toInt()))
    }
}
