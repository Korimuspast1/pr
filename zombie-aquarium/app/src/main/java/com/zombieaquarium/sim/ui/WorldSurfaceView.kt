package com.zombieaquarium.sim.ui

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.DashPathEffect
import android.graphics.Paint
import android.graphics.Path
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
import com.zombieaquarium.sim.audio.SoundEngine
import com.zombieaquarium.sim.audio.SoundEvent
import com.zombieaquarium.sim.engine.CampStructure
import com.zombieaquarium.sim.engine.LootContainer
import com.zombieaquarium.sim.engine.OverlayMode
import com.zombieaquarium.sim.engine.StructureType
import com.zombieaquarium.sim.engine.WallType
import com.zombieaquarium.sim.engine.WeatherType
import com.zombieaquarium.sim.engine.WorldSimulation
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

private class IsoDrawItem {
    var depth: Float = 0f
    var kind: Int = 0 // 0=wall/prop, 1=loot, 2=structure, 3=survivor, 4=zombie
    var tx: Int = 0
    var ty: Int = 0
    var wallType: Byte = 0
    var loot: LootContainer? = null
    var structure: CampStructure? = null
    var survivor: SurvivorAgent? = null
    var zombie: ZombieAgent? = null
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
                zoom = 0.85f
            }
        }

    @Volatile
    var soundEngine: SoundEngine? = null

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
    private val atlas = IsoTextureAtlas()
    private val charRenderer = IsoCharacterRenderer()

    // Camera in world coordinates & isometric zoom
    @Volatile var camX: Float = 80f
    @Volatile var camY: Float = 80f
    @Volatile var zoom: Float = 0.85f

    private var lastTouchX = 0f
    private var lastTouchY = 0f
    private var isPanning = false
    private var totalMoveDist = 0f

    private val scaleDetector = ScaleGestureDetector(context, object : ScaleGestureDetector.SimpleOnScaleGestureListener() {
        override fun onScale(detector: ScaleGestureDetector): Boolean {
            val oldZoom = zoom
            val newZoom = (oldZoom * detector.scaleFactor).coerceIn(0.24f, 2.35f)
            if (width > 0 && height > 0) {
                val fx = detector.focusX - width * 0.5f
                val fy = detector.focusY - height * 0.5f
                val oldHw = 32f * oldZoom
                val oldHh = 16f * oldZoom
                val newHw = 32f * newZoom
                val newHh = 16f * newZoom
                val oldWx = (fx / oldHw + fy / oldHh) * 0.5f
                val oldWy = (fy / oldHh - fx / oldHw) * 0.5f
                val newWx = (fx / newHw + fy / newHh) * 0.5f
                val newWy = (fy / newHh - fx / newHw) * 0.5f
                camX += oldWx - newWx
                camY += oldWy - newWy
            }
            zoom = newZoom
            clampCamera()
            return true
        }
    })

    // Preallocated Paint, Rect, & Depth-Sort Pool (zero GC per frame)
    private val bmpPaint = Paint(Paint.FILTER_BITMAP_FLAG)
    private val linePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { strokeWidth = 2f; style = Paint.Style.STROKE }
    private val dashPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        strokeWidth = 2.6f
        style = Paint.Style.STROKE
        pathEffect = DashPathEffect(floatArrayOf(10f, 8f), 0f)
    }
    private val fxPaint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val ringPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { style = Paint.Style.STROKE; strokeWidth = 2.5f }
    private val textPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        color = Color.WHITE
        textAlign = Paint.Align.CENTER
    }
    private val bubbleBgPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { color = 0xCC0F172A.toInt() }
    private val dstRect = RectF()
    private val diamondPath = Path()

    private val drawPool = Array(5500) { IsoDrawItem() }
    private val activeDrawList = ArrayList<IsoDrawItem>(2400)
    private val frameSounds = ArrayList<SoundEvent>(32)

    init {
        holder.addCallback(this)
        isFocusable = true
    }

    fun centerOn(wx: Float, wy: Float, targetZoom: Float = 1.15f) {
        camX = wx
        camY = wy
        zoom = max(zoom, targetZoom)
        clampCamera()
    }

    private fun clampCamera() {
        val sz = (simulation?.map?.size ?: 160).toFloat()
        camX = camX.coerceIn(0f, sz)
        camY = camY.coerceIn(0f, sz)
    }

    private fun worldToScreenX(wx: Float, wy: Float, halfScreenW: Float, halfW: Float): Float {
        return halfScreenW + ((wx - camX) - (wy - camY)) * halfW
    }

    private fun worldToScreenY(wx: Float, wy: Float, halfScreenH: Float, halfH: Float): Float {
        return halfScreenH + ((wx - camX) + (wy - camY)) * halfH
    }

    private fun screenToWorld(sx: Float, sy: Float): Pair<Float, Float> {
        val halfW = 32f * zoom
        val halfH = 16f * zoom
        val a = (sx - width * 0.5f) / halfW
        val b = (sy - height * 0.5f) / halfH
        return (camX + (a + b) * 0.5f) to (camY + (b - a) * 0.5f)
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
                    val halfW = 32f * zoom
                    val halfH = 16f * zoom
                    val da = -dx / halfW
                    val db = -dy / halfH
                    camX += (da + db) * 0.5f
                    camY += (db - da) * 0.5f
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
        val (wx, wy) = screenToWorld(sx, sy)

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

        // Hit-test in screen space so tapping body/head above feet works intuitively in isometric view!
        val hw = 32f * zoom
        val hh = 16f * zoom
        val hsw = width * 0.5f
        val hsh = height * 0.5f
        val tapRadiusPx = 38f
        val tapR2 = tapRadiusPx * tapRadiusPx
        var picked: SelectionTarget? = null

        synchronized(sim.simLock) {
            var bestD2 = tapR2
            for (s in sim.survivors) {
                if (!s.alive) continue
                val csx = worldToScreenX(s.x, s.y, hsw, hw)
                val csy = worldToScreenY(s.x, s.y, hsh, hh) - 10f * zoom
                val dx = csx - sx
                val dy = csy - sy
                val d2 = dx * dx + dy * dy
                if (d2 < bestD2) {
                    bestD2 = d2
                    picked = SelectionTarget.Survivor(s.id)
                }
            }
            if (picked == null) {
                for (z in sim.zombies) {
                    if (!z.alive) continue
                    val csx = worldToScreenX(z.x, z.y, hsw, hw)
                    val csy = worldToScreenY(z.x, z.y, hsh, hh) - 10f * zoom
                    val dx = csx - sx
                    val dy = csy - sy
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

    private inner class RenderLoopThread : Thread("ZombieIsoLoop") {
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

                    frameSounds.clear()
                    sim.drainSounds(frameSounds)
                    if (speed > 0) {
                        soundEngine?.playSpatial(frameSounds, camX, camY, (22f / zoom).coerceIn(18f, 45f))
                    }

                    hudAccum += frameDt
                    if (hudAccum >= 0.25f) {
                        hudAccum = 0f
                        soundEngine?.updateAmbient(sim.weather, sim.isNight, speed == 0)
                        post { onHudTick?.invoke() }
                    }
                }

                val canvas = lockBestCanvas()
                if (canvas != null) {
                    try {
                        drawIsometricWorld(canvas, sim)
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

    private fun drawIsometricWorld(canvas: Canvas, sim: WorldSimulation?) {
        canvas.drawColor(0xFF070B10.toInt())
        if (sim == null) return

        val w = width.toFloat()
        val h = height.toFloat()
        if (w <= 0f || h <= 0f) return

        val hsw = w * 0.5f
        val hsh = h * 0.5f
        val hw = 32f * zoom
        val hh = 16f * zoom
        val tileW = hw * 2f
        val tileH = hh * 2f
        val propW = IsoTextureAtlas.PROP_W * zoom
        val propH = IsoTextureAtlas.PROP_H * zoom
        val propBaseY = IsoTextureAtlas.PROP_BASE_Y * zoom

        synchronized(sim.simLock) {
            val map = sim.map
            // Compute conservative tile bounds covering isometric screen corners
            val spanTiles = (max(w / tileW, h / tileH) + 6).toInt()
            val minTx = max(0, (camX - spanTiles).toInt())
            val maxTx = min(map.size - 1, (camX + spanTiles).toInt())
            val minTy = max(0, (camY - spanTiles).toInt())
            val maxTy = min(map.size - 1, (camY + spanTiles).toInt())

            var poolIdx = 0
            activeDrawList.clear()

            fun addDrawItem(depth: Float, kind: Int, tx: Int = 0, ty: Int = 0, wt: Byte = 0, loot: LootContainer? = null, st: CampStructure? = null, s: SurvivorAgent? = null, z: ZombieAgent? = null) {
                if (poolIdx >= drawPool.size) return
                val item = drawPool[poolIdx++]
                item.depth = depth
                item.kind = kind
                item.tx = tx
                item.ty = ty
                item.wallType = wt
                item.loot = loot
                item.structure = st
                item.survivor = s
                item.zombie = z
                activeDrawList.add(item)
            }

            // 1. Ground Terrain Pass (Isometric Diamonds) + Collect Walls
            for (ty in minTy..maxTy) {
                val rowOff = ty * map.size
                for (tx in minTx..maxTx) {
                    val sx = worldToScreenX(tx.toFloat(), ty.toFloat(), hsw, hw)
                    val sy = worldToScreenY(tx.toFloat(), ty.toFloat(), hsh, hh)
                    if (sx < -tileW || sx > w + tileW || sy < -tileH * 2f || sy > h + tileH * 2f) continue

                    val idx = rowOff + tx
                    val bmp = atlas.getTerrain(map.terrain[idx], tx, ty, sim.tickCount)
                    dstRect.set(sx - hw, sy, sx + hw, sy + tileH)
                    canvas.drawBitmap(bmp, null, dstRect, bmpPaint)

                    // Blood splatters on ground
                    val bld = map.bloodField[idx]
                    if (bld > 4f) {
                        fxPaint.color = Color.argb(min(185, (bld * 2.4f).toInt()), 153, 27, 27)
                        canvas.drawCircle(sx, sy + hh, 6f * zoom, fxPaint)
                    }

                    // Noise & Light Overlay
                    if (overlayMode == OverlayMode.NOISE_LIGHT) {
                        val n = map.noiseField[idx]
                        val l = map.lightField[idx]
                        if (n > 3f || l > 3f) {
                            diamondPath.reset()
                            diamondPath.moveTo(sx, sy)
                            diamondPath.lineTo(sx + hw, sy + hh)
                            diamondPath.lineTo(sx, sy + tileH)
                            diamondPath.lineTo(sx - hw, sy + hh)
                            diamondPath.close()
                            if (n > 3f) {
                                fxPaint.color = Color.argb(min(160, (n * 2f).toInt()), 245, 158, 11)
                                canvas.drawPath(diamondPath, fxPaint)
                            }
                            if (l > 3f) {
                                fxPaint.color = Color.argb(min(130, (l * 1.6f).toInt()), 56, 189, 248)
                                canvas.drawPath(diamondPath, fxPaint)
                            }
                        }
                    }

                    // Queue 3D Wall / Tree / Car / Door
                    val wt = map.wall[idx]
                    if (wt != WallType.NONE) {
                        addDrawItem(depth = tx + ty + 0.5f, kind = 0, tx = tx, ty = ty, wt = wt)
                    }
                }
            }

            // Faction territories overlay
            if (overlayMode == OverlayMode.FACTIONS) {
                for (camp in sim.camps) {
                    val b = map.buildings.getOrNull(camp.homeBuildingId - 1) ?: continue
                    val p0x = worldToScreenX(b.x1.toFloat(), b.y1.toFloat(), hsw, hw)
                    val p0y = worldToScreenY(b.x1.toFloat(), b.y1.toFloat(), hsh, hh)
                    val p1x = worldToScreenX((b.x2 + 1).toFloat(), b.y1.toFloat(), hsw, hw)
                    val p1y = worldToScreenY((b.x2 + 1).toFloat(), b.y1.toFloat(), hsh, hh)
                    val p2x = worldToScreenX((b.x2 + 1).toFloat(), (b.y2 + 1).toFloat(), hsw, hw)
                    val p2y = worldToScreenY((b.x2 + 1).toFloat(), (b.y2 + 1).toFloat(), hsh, hh)
                    val p3x = worldToScreenX(b.x1.toFloat(), (b.y2 + 1).toFloat(), hsw, hw)
                    val p3y = worldToScreenY(b.x1.toFloat(), (b.y2 + 1).toFloat(), hsh, hh)
                    diamondPath.reset()
                    diamondPath.moveTo(p0x, p0y)
                    diamondPath.lineTo(p1x, p1y)
                    diamondPath.lineTo(p2x, p2y)
                    diamondPath.lineTo(p3x, p3y)
                    diamondPath.close()
                    fxPaint.color = (camp.color and 0x00FFFFFF) or 0x3A000000
                    canvas.drawPath(diamondPath, fxPaint)
                    linePaint.color = camp.color
                    canvas.drawPath(diamondPath, linePaint)
                }
            }

            // 2. Collect Loot Crates, Camp Structures, Zombies & Survivors for 3D depth sorting
            if (zoom >= 0.45f) {
                for (c in map.lootContainers) {
                    if (c.isEmpty()) continue
                    if (c.x < minTx || c.x > maxTx || c.y < minTy || c.y > maxTy) continue
                    addDrawItem(depth = c.x + c.y + 0.52f, kind = 1, loot = c)
                }
            }

            for (st in sim.structures) {
                if (st.x < minTx - 2 || st.x > maxTx + 2 || st.y < minTy - 2 || st.y > maxTy + 2) continue
                addDrawItem(depth = st.x + st.y + 0.55f, kind = 2, structure = st)
            }

            for (z in sim.zombies) {
                if (!z.alive) continue
                if (z.x < minTx - 2 || z.x > maxTx + 2 || z.y < minTy - 2 || z.y > maxTy + 2) continue
                addDrawItem(depth = z.x + z.y, kind = 4, zombie = z)
            }

            for (s in sim.survivors) {
                if (!s.alive) continue
                if (s.x < minTx - 2 || s.x > maxTx + 2 || s.y < minTy - 2 || s.y > maxTy + 2) continue
                addDrawItem(depth = s.x + s.y, kind = 3, survivor = s)
            }

            // Sort back-to-front in isometric depth!
            activeDrawList.sortBy { it.depth }

            val sel = selectedTarget
            for (i in 0 until activeDrawList.size) {
                val item = activeDrawList[i]
                when (item.kind) {
                    0 -> {
                        val bmp = atlas.getWall(item.wallType) ?: continue
                        val sx = worldToScreenX(item.tx.toFloat(), item.ty.toFloat(), hsw, hw)
                        val sy = worldToScreenY(item.tx.toFloat(), item.ty.toFloat(), hsh, hh) + hh
                        dstRect.set(sx - propW * 0.5f, sy - propBaseY, sx + propW * 0.5f, sy + (propH - propBaseY))
                        // Slightly translucent front building walls when zoomed in so interiors stay readable!
                        if (zoom >= 0.7f && (item.wallType == WallType.WALL_BRICK || item.wallType == WallType.WALL_WOOD)) {
                            bmpPaint.alpha = 215
                            canvas.drawBitmap(bmp, null, dstRect, bmpPaint)
                            bmpPaint.alpha = 255
                        } else {
                            canvas.drawBitmap(bmp, null, dstRect, bmpPaint)
                        }
                    }
                    1 -> {
                        val c = item.loot ?: continue
                        val sx = worldToScreenX(c.x + 0.5f, c.y + 0.5f, hsw, hw)
                        val sy = worldToScreenY(c.x + 0.5f, c.y + 0.5f, hsh, hh)
                        val bmp = if (c.weapon != null || c.antibiotics > 0) atlas.crateHigh else atlas.crateNormal
                        dstRect.set(sx - propW * 0.5f, sy - propBaseY, sx + propW * 0.5f, sy + (propH - propBaseY))
                        canvas.drawBitmap(bmp, null, dstRect, bmpPaint)
                    }
                    2 -> {
                        val st = item.structure ?: continue
                        val sx = worldToScreenX(st.x + 0.5f, st.y + 0.5f, hsw, hw)
                        val sy = worldToScreenY(st.x + 0.5f, st.y + 0.5f, hsh, hh)
                        val bmp = when (st.type) {
                            StructureType.CAMPFIRE -> if (st.fuelTicks > 0) atlas.campfireFrames[(sim.tickCount / 2) % 3] else atlas.campfireOut
                            StructureType.RAIN_COLLECTOR -> atlas.rainCollector
                            StructureType.SPIKE_TRAP -> atlas.spikeTrap
                            else -> atlas.crateNormal
                        }
                        dstRect.set(sx - propW * 0.5f, sy - propBaseY, sx + propW * 0.5f, sy + (propH - propBaseY))
                        canvas.drawBitmap(bmp, null, dstRect, bmpPaint)
                    }
                    3 -> {
                        val s = item.survivor ?: continue
                        val sx = worldToScreenX(s.x, s.y, hsw, hw)
                        val sy = worldToScreenY(s.x, s.y, hsh, hh)
                        val isSel = sel is SelectionTarget.Survivor && sel.id == s.id
                        charRenderer.drawSurvivor(canvas, s, sim, sx, sy, zoom, isSel, textPaint, bubbleBgPaint)
                    }
                    4 -> {
                        val z = item.zombie ?: continue
                        val sx = worldToScreenX(z.x, z.y, hsw, hw)
                        val sy = worldToScreenY(z.x, z.y, hsh, hh)
                        val isSel = sel is SelectionTarget.Zombie && sel.id == z.id
                        charRenderer.drawZombie(canvas, z, sx, sy, zoom, isSel)
                    }
                }
            }

            // 3. Sound rings, bullet tracers & particles in isometric space
            for (i in 0 until sim.soundRings.capacity) {
                if (!sim.soundRings.active[i]) continue
                val sx = worldToScreenX(sim.soundRings.x[i], sim.soundRings.y[i], hsw, hw)
                val sy = worldToScreenY(sim.soundRings.x[i], sim.soundRings.y[i], hsh, hh)
                val alpha = ((sim.soundRings.life[i] / sim.soundRings.maxLife[i]) * 190).toInt().coerceIn(0, 255)
                ringPaint.color = (sim.soundRings.color[i] and 0x00FFFFFF) or (alpha shl 24)
                val rx = sim.soundRings.radius[i] * hw
                val ry = sim.soundRings.radius[i] * hh
                dstRect.set(sx - rx, sy - ry, sx + rx, sy + ry)
                canvas.drawOval(dstRect, ringPaint)
            }

            for (i in 0 until sim.tracers.capacity) {
                if (!sim.tracers.active[i]) continue
                linePaint.color = sim.tracers.color[i]
                canvas.drawLine(
                    worldToScreenX(sim.tracers.x1[i], sim.tracers.y1[i], hsw, hw),
                    worldToScreenY(sim.tracers.x1[i], sim.tracers.y1[i], hsh, hh) - 12f * zoom,
                    worldToScreenX(sim.tracers.x2[i], sim.tracers.y2[i], hsw, hw),
                    worldToScreenY(sim.tracers.x2[i], sim.tracers.y2[i], hsh, hh) - 12f * zoom,
                    linePaint
                )
            }

            for (i in 0 until sim.particles.capacity) {
                if (!sim.particles.active[i]) continue
                val sx = worldToScreenX(sim.particles.x[i], sim.particles.y[i], hsw, hw)
                val sy = worldToScreenY(sim.particles.x[i], sim.particles.y[i], hsh, hh) - 8f * zoom
                fxPaint.color = sim.particles.color[i]
                canvas.drawCircle(sx, sy, sim.particles.size[i] * 22f * zoom, fxPaint)
            }

            // 4. Selected Agent Path & Fog-of-Knowledge Memory Overlay
            if (sel is SelectionTarget.Survivor) {
                val s = sim.survivorsById[sel.id]
                if (s != null && s.alive) {
                    drawSelectedAgentOverlay(canvas, s, sim, hsw, hsh, hw, hh)
                }
            }

            // 5. Weather Streaks & Day/Night Atmosphere
            drawAtmosphere(canvas, sim, w, h)
        }
    }

    private fun drawSelectedAgentOverlay(
        canvas: Canvas,
        s: SurvivorAgent,
        sim: WorldSimulation,
        hsw: Float,
        hsh: Float,
        hw: Float,
        hh: Float
    ) {
        if (s.currentPath.isNotEmpty() && s.pathCursor < s.currentPath.size) {
            dashPaint.color = 0xFF38BDF8.toInt()
            var prevX = worldToScreenX(s.x, s.y, hsw, hw)
            var prevY = worldToScreenY(s.x, s.y, hsh, hh)
            for (i in s.pathCursor until s.currentPath.size) {
                val node = s.currentPath[i]
                val nx = (node % sim.map.size) + 0.5f
                val ny = (node / sim.map.size) + 0.5f
                val sx = worldToScreenX(nx, ny, hsw, hw)
                val sy = worldToScreenY(nx, ny, hsh, hh)
                canvas.drawLine(prevX, prevY, sx, sy, dashPaint)
                prevX = sx
                prevY = sy
            }
        }

        if (overlayMode == OverlayMode.AGENT_MEMORY) {
            for (m in s.memories) {
                val mx = worldToScreenX(m.x + 0.5f, m.y + 0.5f, hsw, hw)
                val my = worldToScreenY(m.x + 0.5f, m.y + 0.5f, hsh, hh) - 10f * zoom
                fxPaint.color = when (m.kind) {
                    MemoryKind.HORDE -> 0xFFEF4444.toInt()
                    MemoryKind.MEDS -> 0xFFEC4899.toInt()
                    MemoryKind.WEAPON -> 0xFFF59E0B.toInt()
                    else -> 0xFF10B981.toInt()
                }
                canvas.drawCircle(mx, my, 7f, fxPaint)
                if (zoom >= 0.65f) {
                    textPaint.textSize = 19f
                    textPaint.color = Color.WHITE
                    canvas.drawText(m.kind.titleRu, mx, my - 11f, textPaint)
                }
            }
        }
    }

    private fun drawAtmosphere(canvas: Canvas, sim: WorldSimulation, w: Float, h: Float) {
        val darknessAlpha = when {
            sim.weather == WeatherType.STORM && sim.lightningFlashTicks > 0 -> 14
            sim.timeOfDay < 0.22f || sim.timeOfDay > 0.84f -> 112
            sim.timeOfDay < 0.28f || sim.timeOfDay > 0.76f -> 56
            else -> 0
        }
        if (darknessAlpha > 0) {
            canvas.drawColor(Color.argb(darknessAlpha, 6, 10, 22))
        }
        if (sim.weather == WeatherType.FOG) {
            canvas.drawColor(0x38CBD5E1)
        } else if (sim.weather == WeatherType.RAIN || sim.weather == WeatherType.STORM) {
            canvas.drawColor(0x220F172A)
            linePaint.color = 0x5593C5FD
            val off = (sim.tickCount * 29) % 120
            for (i in 0..28) {
                val rx = ((i * 97 + off * 3) % w.toInt().coerceAtLeast(100)).toFloat()
                val ry = ((i * 131 + off * 7) % h.toInt().coerceAtLeast(100)).toFloat()
                canvas.drawLine(rx, ry, rx - 10f, ry + 24f, linePaint)
            }
        }
    }
}
