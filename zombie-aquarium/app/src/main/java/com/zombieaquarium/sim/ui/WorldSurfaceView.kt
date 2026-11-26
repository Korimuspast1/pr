package com.zombieaquarium.sim.ui

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.DashPathEffect
import android.graphics.Paint
import android.graphics.RectF
import android.os.Build
import android.util.AttributeSet
import android.view.MotionEvent
import android.view.ScaleGestureDetector
import android.view.SurfaceHolder
import android.view.SurfaceView
import com.zombieaquarium.sim.ai.MemoryKind
import com.zombieaquarium.sim.ai.SurvivorAgent
import com.zombieaquarium.sim.ai.ZombieAgent
import com.zombieaquarium.sim.engine.BuildingInfo
import com.zombieaquarium.sim.engine.OverlayMode
import com.zombieaquarium.sim.engine.StructureType
import com.zombieaquarium.sim.engine.TerrainType
import com.zombieaquarium.sim.engine.WallType
import com.zombieaquarium.sim.engine.WeatherType
import com.zombieaquarium.sim.engine.WorldSimulation
import com.zombieaquarium.sim.engine.ZombieType
import kotlin.math.abs
import kotlin.math.max
import kotlin.math.min

sealed class SelectionTarget {
    data class Survivor(val id: Int) : SelectionTarget()
    data class Zombie(val id: Int) : SelectionTarget()
    data class Building(val id: Int) : SelectionTarget()
}

enum class GodTool {
    NONE, SUPPLY_CRATE, HORDE, NEW_SURVIVOR
}

class WorldSurfaceView @JvmOverloads constructor(
    context: Context,
    attrs: AttributeSet? = null
) : SurfaceView(context, attrs), SurfaceHolder.Callback {

    @Volatile
    var simulation: WorldSimulation? = null
        set(value) {
            field = value
            if (value != null) {
                camX = value.map.size * 0.5f
                camY = value.map.size * 0.5f
                tilePx = 14f
            }
        }

    @Volatile
    var overlayMode: OverlayMode = OverlayMode.NORMAL

    @Volatile
    var selectedTarget: SelectionTarget? = null

    @Volatile
    var activeGodTool: GodTool = GodTool.NONE

    var onSelectionChanged: ((SelectionTarget?) -> Unit)? = null
    var onHudTick: (() -> Unit)? = null
    var onGodActionDone: (() -> Unit)? = null

    private var renderThread: RenderLoopThread? = null

    // Camera in world tile coordinates
    @Volatile var camX: Float = 80f
    @Volatile var camY: Float = 80f
    @Volatile var tilePx: Float = 14f

    private var downX = 0f
    private var downY = 0f
    private var lastTouchX = 0f
    private var lastTouchY = 0f
    private var isPanning = false
    private var totalMoveDist = 0f

    private val scaleDetector = ScaleGestureDetector(context, object : ScaleGestureDetector.SimpleOnScaleGestureListener() {
        override fun onScale(detector: ScaleGestureDetector): Boolean {
            val factor = detector.scaleFactor
            val oldPx = tilePx
            val newPx = (oldPx * factor).coerceIn(3.5f, 44f)
            if (width > 0 && height > 0) {
                val fx = detector.focusX - width * 0.5f
                val fy = detector.focusY - height * 0.5f
                camX += fx / oldPx - fx / newPx
                camY += fy / oldPx - fy / newPx
            }
            tilePx = newPx
            clampCamera()
            return true
        }
    })

    // Preallocated Paint objects (zero allocation during draw!)
    private val tilePaint = Paint()
    private val linePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { strokeWidth = 2f; style = Paint.Style.STROKE }
    private val dashPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        strokeWidth = 2.5f
        style = Paint.Style.STROKE
        pathEffect = DashPathEffect(floatArrayOf(10f, 8f), 0f)
    }
    private val agentPaint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val ringPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { style = Paint.Style.STROKE; strokeWidth = 2.5f }
    private val glowPaint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val textPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.WHITE
        textAlign = Paint.Align.CENTER
    }
    private val bubbleBgPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = 0xCC0F172A.toInt() }
    private val bubbleRect = RectF()

    init {
        holder.addCallback(this)
        isFocusable = true
    }

    fun centerOn(wx: Float, wy: Float, zoomPx: Float = 20f) {
        camX = wx
        camY = wy
        tilePx = max(tilePx, zoomPx)
        clampCamera()
    }

    private fun clampCamera() {
        val sz = (simulation?.map?.size ?: 160).toFloat()
        camX = camX.coerceIn(0f, sz)
        camY = camY.coerceIn(0f, sz)
    }

    override fun surfaceCreated(holder: SurfaceHolder) {
        val t = RenderLoopThread()
        renderThread = t
        t.running = true
        t.start()
    }

    override fun surfaceChanged(holder: SurfaceHolder, format: Int, width: Int, height: Int) {}

    override fun surfaceDestroyed(holder: SurfaceHolder) {
        val t = renderThread
        t?.running = false
        try {
            t?.join(600)
        } catch (_: InterruptedException) {
        }
        renderThread = null
    }

    override fun onTouchEvent(event: MotionEvent): Boolean {
        scaleDetector.onTouchEvent(event)
        when (event.actionMasked) {
            MotionEvent.ACTION_DOWN -> {
                downX = event.x
                downY = event.y
                lastTouchX = event.x
                lastTouchY = event.y
                totalMoveDist = 0f
                isPanning = true
            }
            MotionEvent.ACTION_MOVE -> {
                if (!scaleDetector.isInProgress && isPanning && event.pointerCount == 1) {
                    val dx = event.x - lastTouchX
                    val dy = event.y - lastTouchY
                    totalMoveDist += abs(dx) + abs(dy)
                    camX -= dx / tilePx
                    camY -= dy / tilePx
                    clampCamera()
                    lastTouchX = event.x
                    lastTouchY = event.y
                }
            }
            MotionEvent.ACTION_UP -> {
                isPanning = false
                if (totalMoveDist < 18f && !scaleDetector.isInProgress) {
                    handleTap(event.x, event.y)
                }
            }
            MotionEvent.ACTION_CANCEL -> isPanning = false
        }
        return true
    }

    private fun handleTap(sx: Float, sy: Float) {
        val sim = simulation ?: return
        val wx = camX + (sx - width * 0.5f) / tilePx
        val wy = camY + (sy - height * 0.5f) / tilePx

        when (activeGodTool) {
            GodTool.SUPPLY_CRATE -> {
                sim.godDropSupplyCrate(wx, wy)
                activeGodTool = GodTool.NONE
                post { onGodActionDone?.invoke() }
                return
            }
            GodTool.HORDE -> {
                sim.godSpawnHorde(wx, wy)
                activeGodTool = GodTool.NONE
                post { onGodActionDone?.invoke() }
                return
            }
            GodTool.NEW_SURVIVOR -> {
                sim.godSpawnSurvivor(wx, wy)
                activeGodTool = GodTool.NONE
                post { onGodActionDone?.invoke() }
                return
            }
            GodTool.NONE -> {}
        }

        val hitRadius = max(1.4f, 22f / tilePx)
        val hitR2 = hitRadius * hitRadius
        var picked: SelectionTarget? = null

        synchronized(sim.simLock) {
            var bestD2 = hitR2
            for (s in sim.survivors) {
                if (!s.alive) continue
                val dx = s.x - wx
                val dy = s.y - wy
                val d2 = dx * dx + dy * dy
                if (d2 < bestD2) {
                    bestD2 = d2
                    picked = SelectionTarget.Survivor(s.id)
                }
            }
            if (picked == null) {
                for (z in sim.zombies) {
                    if (!z.alive) continue
                    val dx = z.x - wx
                    val dy = z.y - wy
                    val d2 = dx * dx + dy * dy
                    if (d2 < bestD2) {
                        bestD2 = d2
                        picked = SelectionTarget.Zombie(z.id)
                    }
                }
            }
            if (picked == null) {
                val tx = wx.toInt()
                val ty = wy.toInt()
                if (sim.map.inBounds(tx, ty)) {
                    val bId = sim.map.buildingId[sim.map.idx(tx, ty)]
                    if (bId != -1) picked = SelectionTarget.Building(bId)
                }
            }
        }

        selectedTarget = picked
        post { onSelectionChanged?.invoke(picked) }
    }

    private inner class RenderLoopThread : Thread("ZombieAquariumLoop") {
        @Volatile var running = true

        override fun run() {
            var lastNs = System.nanoTime()
            var accumulator = 0f
            var hudAccum = 0f

            while (running) {
                val nowNs = System.nanoTime()
                val frameDt = ((nowNs - lastNs) / 1_000_000_000f).coerceIn(0f, 0.08f)
                lastNs = nowNs

                val sim = simulation
                if (sim != null) {
                    val speed = sim.speedMultiplier
                    if (speed > 0) {
                        accumulator += frameDt * speed
                        var steps = 0
                        while (accumulator >= WorldSimulation.TICK_DT && steps < 8) {
                            sim.stepFixedTick()
                            accumulator -= WorldSimulation.TICK_DT
                            steps++
                        }
                        if (steps >= 8) accumulator = 0f
                    }
                    hudAccum += frameDt
                    if (hudAccum >= 0.25f) {
                        hudAccum = 0f
                        post { onHudTick?.invoke() }
                    }
                }

                val canvas = lockBestCanvas()
                if (canvas != null) {
                    try {
                        drawWorld(canvas, sim)
                    } finally {
                        holder.unlockCanvasAndPost(canvas)
                    }
                }

                val elapsedMs = (System.nanoTime() - nowNs) / 1_000_000L
                val sleepMs = 16L - elapsedMs
                if (sleepMs > 1L) {
                    try {
                        sleep(sleepMs)
                    } catch (_: InterruptedException) {
                    }
                }
            }
        }

        private fun lockBestCanvas(): Canvas? {
            return try {
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                    holder.lockHardwareCanvas()
                } else {
                    holder.lockCanvas()
                }
            } catch (_: Throwable) {
                try {
                    holder.lockCanvas()
                } catch (_: Throwable) {
                    null
                }
            }
        }
    }

    private fun drawWorld(canvas: Canvas, sim: WorldSimulation?) {
        canvas.drawColor(0xFF090D12.toInt())
        if (sim == null) return

        val w = width.toFloat()
        val h = height.toFloat()
        if (w <= 0f || h <= 0f) return

        val px = tilePx
        val halfW = (w * 0.5f) / px
        val halfH = (h * 0.5f) / px

        synchronized(sim.simLock) {
            val map = sim.map
            val minTx = max(0, (camX - halfW - 1f).toInt())
            val maxTx = min(map.size - 1, (camX + halfW + 1f).toInt())
            val minTy = max(0, (camY - halfH - 1f).toInt())
            val maxTy = min(map.size - 1, (camY + halfH + 1f).toInt())

            // 1. Visible Tiles & Walls
            for (ty in minTy..maxTy) {
                val sy = h * 0.5f + (ty - camY) * px
                val rowOff = ty * map.size
                for (tx in minTx..maxTx) {
                    val sx = w * 0.5f + (tx - camX) * px
                    val idx = rowOff + tx

                    tilePaint.color = terrainColor(map.terrain[idx])
                    canvas.drawRect(sx, sy, sx + px + 0.7f, sy + px + 0.7f, tilePaint)

                    // Road center dashed stripe when zoomed
                    if (px >= 10f && map.terrain[idx] == TerrainType.ROAD && tx % 2 == 0 && ty % 2 == 0) {
                        tilePaint.color = 0x55E2E8F0.toInt()
                        canvas.drawRect(sx + px * 0.42f, sy + px * 0.42f, sx + px * 0.58f, sy + px * 0.58f, tilePaint)
                    }

                    // Walls / Doors / Windows / Trees / Cars
                    val wt = map.wall[idx]
                    if (wt != WallType.NONE) {
                        drawWallTile(canvas, sx, sy, px, wt)
                    }

                    // Noise & Light overlay
                    if (overlayMode == OverlayMode.NOISE_LIGHT) {
                        val n = map.noiseField[idx]
                        if (n > 3f) {
                            tilePaint.color = Color.argb(min(180, (n * 2.2f).toInt()), 245, 158, 11)
                            canvas.drawRect(sx, sy, sx + px, sy + px, tilePaint)
                        }
                        val l = map.lightField[idx]
                        if (l > 3f) {
                            tilePaint.color = Color.argb(min(140, (l * 1.8f).toInt()), 56, 189, 248)
                            canvas.drawRect(sx, sy, sx + px, sy + px, tilePaint)
                        }
                    }
                }
            }

            // 2. Loot containers (when zoomed in)
            if (px >= 8f) {
                for (c in map.lootContainers) {
                    if (c.isEmpty()) continue
                    if (c.x < minTx || c.x > maxTx || c.y < minTy || c.y > maxTy) continue
                    val sx = w * 0.5f + (c.x + 0.5f - camX) * px
                    val sy = h * 0.5f + (c.y + 0.5f - camY) * px
                    tilePaint.color = if (c.weapon != null || c.antibiotics > 0) 0xFFF59E0B.toInt() else 0xFF38BDF8.toInt()
                    val r = px * 0.22f
                    canvas.drawRect(sx - r, sy - r, sx + r, sy + r, tilePaint)
                }
            }

            // 3. Camp structures & Factions overlay
            for (st in sim.structures) {
                if (st.x < minTx - 2 || st.x > maxTx + 2 || st.y < minTy - 2 || st.y > maxTy + 2) continue
                val sx = w * 0.5f + (st.x + 0.5f - camX) * px
                val sy = h * 0.5f + (st.y + 0.5f - camY) * px
                when (st.type) {
                    StructureType.CAMPFIRE -> {
                        if (st.fuelTicks > 0) {
                            glowPaint.color = 0x33FB923C
                            canvas.drawCircle(sx, sy, px * 3.8f, glowPaint)
                            agentPaint.color = 0xFFF97316.toInt()
                            canvas.drawCircle(sx, sy, px * 0.34f, agentPaint)
                        } else {
                            agentPaint.color = 0xFF78716C.toInt()
                            canvas.drawCircle(sx, sy, px * 0.28f, agentPaint)
                        }
                    }
                    StructureType.RAIN_COLLECTOR -> {
                        agentPaint.color = 0xFF0EA5E9.toInt()
                        canvas.drawCircle(sx, sy, px * 0.30f, agentPaint)
                    }
                    else -> {
                        agentPaint.color = 0xFFA16207.toInt()
                        canvas.drawCircle(sx, sy, px * 0.26f, agentPaint)
                    }
                }
            }

            if (overlayMode == OverlayMode.FACTIONS) {
                for (camp in sim.camps) {
                    val b = map.buildings.getOrNull(camp.homeBuildingId - 1) ?: continue
                    val rx1 = w * 0.5f + (b.x1 - camX) * px
                    val ry1 = h * 0.5f + (b.y1 - camY) * px
                    val rx2 = w * 0.5f + (b.x2 + 1 - camX) * px
                    val ry2 = h * 0.5f + (b.y2 + 1 - camY) * px
                    tilePaint.color = (camp.color and 0x00FFFFFF) or 0x44000000
                    canvas.drawRect(rx1, ry1, rx2, ry2, tilePaint)
                    linePaint.color = camp.color
                    canvas.drawRect(rx1, ry1, rx2, ry2, linePaint)
                }
            }

            // 4. Sound rings & Tracers
            for (i in 0 until sim.soundRings.capacity) {
                if (!sim.soundRings.active[i]) continue
                val sx = w * 0.5f + (sim.soundRings.x[i] - camX) * px
                val sy = h * 0.5f + (sim.soundRings.y[i] - camY) * px
                val alpha = ((sim.soundRings.life[i] / sim.soundRings.maxLife[i]) * 190).toInt().coerceIn(0, 255)
                ringPaint.color = (sim.soundRings.color[i] and 0x00FFFFFF) or (alpha shl 24)
                canvas.drawCircle(sx, sy, sim.soundRings.radius[i] * px, ringPaint)
            }

            for (i in 0 until sim.tracers.capacity) {
                if (!sim.tracers.active[i]) continue
                linePaint.color = sim.tracers.color[i]
                canvas.drawLine(
                    w * 0.5f + (sim.tracers.x1[i] - camX) * px,
                    h * 0.5f + (sim.tracers.y1[i] - camY) * px,
                    w * 0.5f + (sim.tracers.x2[i] - camX) * px,
                    h * 0.5f + (sim.tracers.y2[i] - camY) * px,
                    linePaint
                )
            }

            // 5. Agent Memory Overlay (for selected survivor)
            val sel = selectedTarget
            if (sel is SelectionTarget.Survivor) {
                val s = sim.survivorsById[sel.id]
                if (s != null && s.alive) {
                    drawSelectedAgentPathAndMemory(canvas, s, sim, w, h, px)
                }
            }

            // 6. Zombies
            for (z in sim.zombies) {
                if (!z.alive) continue
                if (z.x < minTx - 1 || z.x > maxTx + 1 || z.y < minTy - 1 || z.y > maxTy + 1) continue
                drawZombie(canvas, z, w, h, px, sel)
            }

            // 7. Survivors
            for (s in sim.survivors) {
                if (!s.alive) continue
                if (s.x < minTx - 2 || s.x > maxTx + 2 || s.y < minTy - 2 || s.y > maxTy + 2) continue
                drawSurvivor(canvas, s, sim, w, h, px, sel)
            }

            // 8. Particles
            for (i in 0 until sim.particles.capacity) {
                if (!sim.particles.active[i]) continue
                val sx = w * 0.5f + (sim.particles.x[i] - camX) * px
                val sy = h * 0.5f + (sim.particles.y[i] - camY) * px
                agentPaint.color = sim.particles.color[i]
                canvas.drawCircle(sx, sy, sim.particles.size[i] * px, agentPaint)
            }

            // 9. Day / Night & Weather atmosphere overlay
            drawAtmosphere(canvas, sim, w, h)
        }
    }

    private fun drawWallTile(canvas: Canvas, sx: Float, sy: Float, px: Float, wt: Byte) {
        when (wt) {
            WallType.WALL_BRICK -> {
                tilePaint.color = 0xFF475569.toInt()
                canvas.drawRect(sx, sy, sx + px + 0.5f, sy + px + 0.5f, tilePaint)
            }
            WallType.WALL_WOOD -> {
                tilePaint.color = 0xFF78350F.toInt()
                canvas.drawRect(sx, sy, sx + px + 0.5f, sy + px + 0.5f, tilePaint)
            }
            WallType.DOOR_CLOSED -> {
                tilePaint.color = 0xFFD97706.toInt()
                canvas.drawRect(sx + px * 0.1f, sy + px * 0.1f, sx + px * 0.9f, sy + px * 0.9f, tilePaint)
            }
            WallType.DOOR_OPEN -> {
                tilePaint.color = 0x55D97706
                canvas.drawRect(sx + px * 0.1f, sy + px * 0.1f, sx + px * 0.35f, sy + px * 0.9f, tilePaint)
            }
            WallType.DOOR_BARRICADED -> {
                tilePaint.color = 0xFF92400E.toInt()
                canvas.drawRect(sx, sy, sx + px, sy + px, tilePaint)
                tilePaint.color = 0xFFFBBF24.toInt()
                canvas.drawRect(sx + px * 0.1f, sy + px * 0.2f, sx + px * 0.9f, sy + px * 0.35f, tilePaint)
                canvas.drawRect(sx + px * 0.1f, sy + px * 0.65f, sx + px * 0.9f, sy + px * 0.8f, tilePaint)
            }
            WallType.WINDOW_INTACT -> {
                tilePaint.color = 0xFF38BDF8.toInt()
                canvas.drawRect(sx + px * 0.15f, sy + px * 0.15f, sx + px * 0.85f, sy + px * 0.85f, tilePaint)
            }
            WallType.WINDOW_BROKEN -> {
                tilePaint.color = 0x5538BDF8
                canvas.drawRect(sx + px * 0.15f, sy + px * 0.15f, sx + px * 0.85f, sy + px * 0.85f, tilePaint)
            }
            WallType.TREE -> {
                agentPaint.color = 0xFF14532D.toInt()
                canvas.drawCircle(sx + px * 0.5f, sy + px * 0.5f, px * 0.48f, agentPaint)
                agentPaint.color = 0xFF166534.toInt()
                canvas.drawCircle(sx + px * 0.45f, sy + px * 0.42f, px * 0.32f, agentPaint)
            }
            WallType.CAR_WRECK -> {
                tilePaint.color = 0xFF64748B.toInt()
                canvas.drawRect(sx + px * 0.08f, sy + px * 0.16f, sx + px * 0.92f, sy + px * 0.84f, tilePaint)
            }
        }
    }

    private fun drawZombie(
        canvas: Canvas,
        z: ZombieAgent,
        w: Float,
        h: Float,
        px: Float,
        sel: SelectionTarget?
    ) {
        val sx = w * 0.5f + (z.x - camX) * px
        val sy = h * 0.5f + (z.y - camY) * px
        val baseR = when (z.type) {
            ZombieType.BRUTE -> px * 0.46f
            ZombieType.RUNNER -> px * 0.34f
            else -> px * 0.36f
        }.coerceAtLeast(2.4f)

        agentPaint.color = when (z.type) {
            ZombieType.WALKER -> 0xFF65A30D.toInt()
            ZombieType.RUNNER -> 0xFFEF4444.toInt()
            ZombieType.SCREAMER -> 0xFFA855F7.toInt()
            ZombieType.BRUTE -> 0xFF991B1B.toInt()
        }
        canvas.drawCircle(sx, sy, baseR, agentPaint)

        if (sel is SelectionTarget.Zombie && sel.id == z.id) {
            ringPaint.color = Color.WHITE
            canvas.drawCircle(sx, sy, baseR + 4f, ringPaint)
        }
    }

    private fun drawSurvivor(
        canvas: Canvas,
        s: SurvivorAgent,
        sim: WorldSimulation,
        w: Float,
        h: Float,
        px: Float,
        sel: SelectionTarget?
    ) {
        val sx = w * 0.5f + (s.x - camX) * px
        val sy = h * 0.5f + (s.y - camY) * px
        val r = (px * 0.40f).coerceAtLeast(3.2f)

        // Flashlight beam glow at night
        if (sim.isNight && s.hasFlashlight) {
            glowPaint.color = 0x22FEF08A
            canvas.drawCircle(sx, sy, px * 3.4f, glowPaint)
        }

        // Camp ring or infection ring
        if (s.infected) {
            ringPaint.color = 0xFFA3E635.toInt()
            canvas.drawCircle(sx, sy, r + 3.5f, ringPaint)
        } else if (s.bleeding > 0.3f) {
            ringPaint.color = 0xFFEF4444.toInt()
            canvas.drawCircle(sx, sy, r + 3f, ringPaint)
        }

        val campColor = sim.campsById[s.campId]?.color
        if (campColor != null) {
            ringPaint.color = campColor
            canvas.drawCircle(sx, sy, r + 1.5f, ringPaint)
        }

        // Body
        agentPaint.color = s.avatarColor
        canvas.drawCircle(sx, sy, r, agentPaint)

        // Selection ring
        if (sel is SelectionTarget.Survivor && sel.id == s.id) {
            ringPaint.color = Color.WHITE
            canvas.drawCircle(sx, sy, r + 6f, ringPaint)
        }

        // Overhead name, action tag & thought bubble when zoomed in
        if (px >= 13f) {
            textPaint.textSize = (px * 0.52f).coerceIn(18f, 28f)
            textPaint.color = Color.WHITE
            canvas.drawText("${s.name} [${s.currentActionIcon}]", sx, sy - r - 6f, textPaint)

            if (px >= 18f && s.thoughtTimer > 0 && s.thoughtText.isNotEmpty()) {
                textPaint.textSize = 21f
                val tw = textPaint.measureText(s.thoughtText)
                bubbleRect.set(sx - tw * 0.5f - 10f, sy - r - 58f, sx + tw * 0.5f + 10f, sy - r - 28f)
                canvas.drawRoundRect(bubbleRect, 8f, 8f, bubbleBgPaint)
                textPaint.color = 0xFFE2E8F0.toInt()
                canvas.drawText(s.thoughtText, sx, sy - r - 36f, textPaint)
            }
        }
    }

    private fun drawSelectedAgentPathAndMemory(
        canvas: Canvas,
        s: SurvivorAgent,
        sim: WorldSimulation,
        w: Float,
        h: Float,
        px: Float
    ) {
        // Draw current A* path
        if (s.currentPath.isNotEmpty() && s.pathCursor < s.currentPath.size) {
            dashPaint.color = 0xFF38BDF8.toInt()
            var prevX = w * 0.5f + (s.x - camX) * px
            var prevY = h * 0.5f + (s.y - camY) * px
            for (i in s.pathCursor until s.currentPath.size) {
                val node = s.currentPath[i]
                val nx = w * 0.5f + (node % sim.map.size + 0.5f - camX) * px
                val ny = h * 0.5f + (node / sim.map.size + 0.5f - camY) * px
                canvas.drawLine(prevX, prevY, nx, ny, dashPaint)
                prevX = nx
                prevY = ny
            }
        }

        // Draw known memory markers when Agent Memory overlay is active (or always subtle pins)
        if (overlayMode == OverlayMode.AGENT_MEMORY) {
            for (m in s.memories) {
                val mx = w * 0.5f + (m.x + 0.5f - camX) * px
                val my = h * 0.5f + (m.y + 0.5f - camY) * px
                agentPaint.color = when (m.kind) {
                    MemoryKind.HORDE -> 0xFFEF4444.toInt()
                    MemoryKind.MEDS -> 0xFFEC4899.toInt()
                    MemoryKind.WEAPON -> 0xFFF59E0B.toInt()
                    else -> 0xFF10B981.toInt()
                }
                canvas.drawCircle(mx, my, 8f, agentPaint)
                if (px >= 10f) {
                    textPaint.textSize = 20f
                    textPaint.color = Color.WHITE
                    canvas.drawText(m.kind.titleRu, mx, my - 12f, textPaint)
                }
            }
        }
    }

    private fun drawAtmosphere(canvas: Canvas, sim: WorldSimulation, w: Float, h: Float) {
        val darknessAlpha = when {
            sim.weather == WeatherType.STORM && sim.lightningFlashTicks > 0 -> 15
            sim.timeOfDay < 0.22f || sim.timeOfDay > 0.84f -> 115
            sim.timeOfDay < 0.28f || sim.timeOfDay > 0.76f -> 58
            else -> 0
        }
        if (darknessAlpha > 0) {
            canvas.drawColor(Color.argb(darknessAlpha, 6, 10, 22))
        }
        when (sim.weather) {
            WeatherType.FOG -> canvas.drawColor(0x36CBD5E1)
            WeatherType.RAIN, WeatherType.STORM -> canvas.drawColor(0x221E293B)
            WeatherType.CLEAR -> {}
        }
    }

    private fun terrainColor(t: Byte): Int {
        return when (t) {
            TerrainType.GRASS -> 0xFF1E2E20.toInt()
            TerrainType.FOREST -> 0xFF162519.toInt()
            TerrainType.WATER -> 0xFF1D4ED8.toInt()
            TerrainType.BRIDGE -> 0xFF78350F.toInt()
            TerrainType.ROAD -> 0xFF1F2937.toInt()
            TerrainType.CONCRETE -> 0xFF334155.toInt()
            TerrainType.WOOD_FLOOR -> 0xFF451A03.toInt()
            TerrainType.TILE_FLOOR -> 0xFF273549.toInt()
            else -> 0xFF111827.toInt()
        }
    }
}
