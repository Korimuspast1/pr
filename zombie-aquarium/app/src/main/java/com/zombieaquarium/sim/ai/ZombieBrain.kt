package com.zombieaquarium.sim.ai

import com.zombieaquarium.sim.audio.SoundEventType
import com.zombieaquarium.sim.engine.SimRng
import com.zombieaquarium.sim.engine.TerrainType
import com.zombieaquarium.sim.engine.WallType
import com.zombieaquarium.sim.engine.WeatherType
import com.zombieaquarium.sim.engine.WorldSimulation
import com.zombieaquarium.sim.engine.ZombieType
import kotlin.math.max
import kotlin.math.min
import kotlin.math.sqrt

object ZombieBrain {
    private val humanBuf = IntArray(32)
    private val zomBuf = IntArray(24)

    fun tickZombie(
        z: ZombieAgent,
        sim: WorldSimulation,
        dt: Float,
        rng: SimRng
    ) {
        if (!z.alive) return
        if (z.attackCooldown > 0) z.attackCooldown--
        if (z.screamCooldown > 0) z.screamCooldown--
        if (z.alertTimer > 0) z.alertTimer--
        if (z.attackAnimTicks > 0) z.attackAnimTicks--
        if (z.hurtAnimTicks > 0) z.hurtAnimTicks--

        // 1. Check nearby visible / audible survivors
        val sightRange = when {
            sim.weather == WeatherType.FOG -> 5.5f
            sim.isNight -> 9.0f
            else -> 10.5f
        }
        val hCount = sim.survivorGrid.queryRadius(z.x, z.y, sightRange, humanBuf)
        var targetHuman: SurvivorAgent? = null
        var bestD2 = Float.MAX_VALUE

        for (i in 0 until hCount) {
            val h = sim.survivorsById[humanBuf[i]] ?: continue
            if (!h.alive) continue
            val d2 = dist2(z.x, z.y, h.x, h.y)
            if (d2 < bestD2 && sim.map.hasLineOfSight(z.x.toInt(), z.y.toInt(), h.x.toInt(), h.y.toInt(), sightRange.toInt())) {
                bestD2 = d2
                targetHuman = h
            }
        }

        // Screamer alarm wave (50 tiles!)
        if (targetHuman != null && z.type == ZombieType.SCREAMER && z.screamCooldown <= 0) {
            z.screamCooldown = 90
            sim.map.addNoise(z.x.toInt().coerceIn(0, sim.map.size - 1), z.y.toInt().coerceIn(0, sim.map.size - 1), 48f, 100f)
            sim.soundRings.spawn(z.x, z.y, 24f, 0xFFA855F7.toInt(), 1.2f)
            sim.emitSound(SoundEventType.SCREAMER, z.x, z.y)
        } else if (targetHuman != null && rng.chance(0.015f)) {
            sim.emitSound(SoundEventType.ZOMBIE_GROAN, z.x, z.y)
        }

        var moveDx = 0f
        var moveDy = 0f
        var speed = z.type.speedTilesPerSec
        if (sim.isNight) speed *= 1.15f

        if (targetHuman != null) {
            z.targetSurvivorId = targetHuman.id
            z.lastAlertX = targetHuman.x
            z.lastAlertY = targetHuman.y
            z.alertTimer = 45
            val d = sqrt(bestD2)
            if (d <= 1.25f) {
                attackSurvivor(z, targetHuman, sim, rng)
                return
            }
            moveDx = (targetHuman.x - z.x) / max(0.01f, d)
            moveDy = (targetHuman.y - z.y) / max(0.01f, d)
        } else {
            // Follow Flow Field (noise, blood, lights!) or flocking/wander
            val tx = z.x.toInt().coerceIn(1, sim.map.size - 2)
            val ty = z.y.toInt().coerceIn(1, sim.map.size - 2)
            val idx = sim.map.idx(tx, ty)
            val fdx = sim.map.flowDx[idx]
            val fdy = sim.map.flowDy[idx]
            if (fdx != 0f || fdy != 0f) {
                moveDx = fdx
                moveDy = fdy
            } else if (z.alertTimer > 0 && z.lastAlertX >= 0f) {
                val dx = z.lastAlertX - z.x
                val dy = z.lastAlertY - z.y
                val d = sqrt(dx * dx + dy * dy)
                if (d > 0.5f) {
                    moveDx = dx / d
                    moveDy = dy / d
                }
            } else {
                // Slow idle horde drift
                speed *= 0.42f
                if (z.wanderTimer <= 0) {
                    val angle = rng.nextFloatRange(0f, 6.283f)
                    z.wanderVx = kotlin.math.cos(angle)
                    z.wanderVy = kotlin.math.sin(angle)
                    z.wanderTimer = rng.nextRange(25, 70)
                }
                z.wanderTimer--
                moveDx = z.wanderVx
                moveDy = z.wanderVy
            }
        }

        // Boids local separation from adjacent zombies so hordes fan out naturally
        val nearZ = sim.zombieGrid.queryRadius(z.x, z.y, 1.2f, zomBuf)
        var sepX = 0f
        var sepY = 0f
        for (i in 0 until nearZ) {
            val other = sim.zombiesById[zomBuf[i]] ?: continue
            if (!other.alive || other.id == z.id) continue
            val dx = z.x - other.x
            val dy = z.y - other.y
            val d2 = dx * dx + dy * dy
            if (d2 > 0.0001f && d2 < 1.0f) {
                sepX += dx / d2
                sepY += dy / d2
            }
        }
        moveDx += sepX * 0.28f
        moveDy += sepY * 0.28f

        val len = sqrt(moveDx * moveDx + moveDy * moveDy)
        if (len > 0.01f) {
            moveDx /= len
            moveDy /= len
            z.facingX = moveDx
            z.facingY = moveDy
            z.animPhase += speed * dt * 2.4f
            val nx = (z.x + moveDx * speed * dt).coerceIn(1f, sim.map.size - 2f)
            val ny = (z.y + moveDy * speed * dt).coerceIn(1f, sim.map.size - 2f)
            val ntx = nx.toInt()
            val nty = ny.toInt()
            val nIdx = sim.map.idx(ntx, nty)
            val w = sim.map.wall[nIdx]

            if (sim.map.isWalkableForZombie(ntx, nty)) {
                z.x = nx
                z.y = ny
                sim.map.addDanger(ntx, nty, 1, 2.5f)
            } else if (w == WallType.DOOR_CLOSED || w == WallType.DOOR_BARRICADED || w == WallType.WINDOW_INTACT) {
                // Siege doors, barricades, and windows!
                if (z.attackCooldown <= 0) {
                    z.attackCooldown = 9
                    z.attackAnimTicks = 5
                    sim.map.wallHp[nIdx] -= z.type.structureDamage
                    sim.map.addNoise(ntx, nty, 16f, 42f)
                    sim.emitSound(SoundEventType.DOOR_BREAK, ntx + 0.5f, nty + 0.5f)
                    sim.particles.spawn(ntx + 0.5f, nty + 0.5f, rng.nextFloatRange(-1f, 1f), rng.nextFloatRange(-1f, 1f), 0.35f, 0xFFB45309.toInt(), 0.14f)
                    if (sim.map.wallHp[nIdx] <= 0f) {
                        sim.map.wall[nIdx] = if (w == WallType.WINDOW_INTACT) WallType.WINDOW_BROKEN else WallType.DOOR_OPEN
                        sim.emitSound(if (w == WallType.WINDOW_INTACT) SoundEventType.GLASS_BREAK else SoundEventType.DOOR_BREAK, ntx + 0.5f, nty + 0.5f)
                        sim.soundRings.spawn(ntx + 0.5f, nty + 0.5f, 14f, 0xFFF59E0B.toInt(), 0.9f)
                        sim.logChronicle("Прорыв", "Зомби (${z.type.titleRu}) проломили преграду!", ntx.toFloat(), nty.toFloat())
                    }
                }
            } else {
                // Slide along axis if wall blocks diagonal movement
                if (sim.map.isWalkableForZombie(nx.toInt(), z.y.toInt())) z.x = nx
                else if (sim.map.isWalkableForZombie(z.x.toInt(), ny.toInt())) z.y = ny
                else if (sim.map.terrain[nIdx] != TerrainType.WATER) {
                    z.wanderTimer = 0
                }
            }
        }
    }

    private fun attackSurvivor(z: ZombieAgent, h: SurvivorAgent, sim: WorldSimulation, rng: SimRng) {
        if (z.attackCooldown > 0) return
        z.attackCooldown = 10
        z.attackAnimTicks = 5
        val dx = h.x - z.x
        val dy = h.y - z.y
        val dl = sqrt(dx * dx + dy * dy)
        if (dl > 0.01f) {
            z.facingX = dx / dl
            z.facingY = dy / dl
        }
        h.hp -= z.type.attackDamage
        h.hurtAnimTicks = 4
        sim.emitSound(SoundEventType.ZOMBIE_BITE, h.x, h.y)
        sim.map.addBlood(h.x.toInt().coerceIn(0, sim.map.size - 1), h.y.toInt().coerceIn(0, sim.map.size - 1), 35f)
        repeat(4) {
            sim.particles.spawn(h.x, h.y, rng.nextFloatRange(-2f, 2f), rng.nextFloatRange(-2f, 2f), 0.5f, 0xFFEF4444.toInt(), 0.14f)
        }
        if (rng.chance(0.45f)) h.bleeding = min(10f, h.bleeding + 2.4f)
        if (rng.chance(0.18f)) h.limping = true
        if (!h.infected && rng.chance(0.24f)) {
            h.infected = true
            h.infectionTimerTicks = rng.nextRange(240, 420)
            h.setThought("Чёрт... меня укусили!", 65)
            h.addBio(sim.day, sim.formatTime(), "Получен укус зомби!")
            sim.logChronicle("Заражение", "${h.name} получил(а) укус от ${z.type.titleRu}!", h.x, h.y)
        }
    }

    private fun dist2(x1: Float, y1: Float, x2: Float, y2: Float): Float {
        val dx = x1 - x2
        val dy = y1 - y2
        return dx * dx + dy * dy
    }
}
