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
import android.view.VelocityTracker
import com.zombieaquarium.sim.ai.MemoryKind
import com.zombieaquarium.sim.ai.SurvivorAgent
import com.zombieaquarium.sim.ai.ZombieAgent
import com.zombieaquarium.sim.audio.SoundEngine
import com.zombieaquarium.sim.audio.SoundEvent
import com.zombieaquarium.sim.engine.BuildingInfo
import com.zombieaquarium.sim.engine.BuildingType
import com.zombieaquarium.sim.engine.CampStructure
import com.zombieaquarium.sim.engine.LootContainer
import com.zombieaquarium.sim.engine.OverlayMode
import com.zombieaquarium.sim.engine.StructureType
import com.zombieaquarium.sim.engine.WallType
import com.zombieaquarium.sim.engine.WeatherType
import com.zombieaquarium.sim.engine.WorldSimulation
import kotlin.math.abs
import kotlin.math.ceil
import kotlin.math.floor
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
    var kind: Int = 0 // 0=wall/prop, 1=loot, 2=structure, 3=survivor, 4=zombie, 5=building roof/upper
    var tx: Int = 0
    var ty: Int = 0
    var wallType: Byte = 0
    var loot: LootContainer? = null
    var structure: CampStructure? = null
    var survivor: SurvivorAgent? = null
    var zombie: ZombieAgent? = null
    var building: BuildingInfo? = null
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
                panVelPxX = 0f
                panVelPxY = 0f
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

    @Volatile var camX: Float = 80f
    @Volatile var camY: Float = 80f
    @Volatile var zoom: Float = 0.85f

    private var lastTouchX = 0f
    private var lastTouchY = 0f
    private var isPanning = false
    private var totalMoveDist = 0f
    private var velocityTracker: VelocityTracker? = null
    @Volatile private var panVelPxX = 0f
    @Volatile private var panVelPxY = 0f

    private val scaleDetector = ScaleGestureDetector(context, object : ScaleGestureDetector.SimpleOnScaleGestureListener() {
        override fun onScale(detector: ScaleGestureDetector): Boolean {
            val oldZoom = zoom
            val newZoom = (oldZoom * detector.scaleFactor).coerceIn(0.28f, 2.35f)
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

    // Preallocated Paint, Path, RectF & Sort Pool (zero GC allocations per frame)
    private val bmpPaint = Paint(Paint.FILTER_BITMAP_FLAG)
    private val roofPaint = Paint(Paint.ANTI_ALIAS_FLAG)
    private val roofStrokePaint = Paint(Paint.ANTI_ALIAS_FLAG).apply { style = Paint.Style.STROKE }
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
    private val roofPath = Path()

    private val drawPool = Array(6500) { IsoDrawItem() }
    private val drawActive = arrayOfNulls<IsoDrawItem>(6500)
    private val buildingOccupied = BooleanArray(512)
    private val frameSounds = ArrayList<SoundEvent>(32)

    init {
        holder.addCallback(this)
        isFocusable = true
    }

    fun centerOn(wx: Float, wy: Float, targetZoom: Float = 1.15f) {
        panVelPxX = 0f
        panVelPxY = 0f
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
                velocityTracker?.recycle()
                velocityTracker = VelocityTracker.obtain()
                velocityTracker?.addMovement(event)
                panVelPxX = 0f
                panVelPxY = 0f
                lastTouchX = event.x
                lastTouchY = event.y
                totalMoveDist = 0f
                isPanning = true
            }
            MotionEvent.ACTION_MOVE -> {
                velocityTracker?.addMovement(event)
                if (!scaleDetector.isInProgress && isPanning && event.pointerCount == 1) {
                    val dx = event.x - lastTouchX
                    val dy = event.y - lastTouchY
                    totalMoveDist += abs(dx) + abs(dy)
                    applyPanDeltaPx(dx, dy)
                    lastTouchX = event.x
                    lastTouchY = event.y
                }
            }
            MotionEvent.ACTION_UP -> {
                isPanning = false
                velocityTracker?.addMovement(event)
                velocityTracker?.computeCurrentVelocity(1000, 4500f)
                val vx = velocityTracker?.xVelocity ?: 0f
                val vy = velocityTracker?.yVelocity ?: 0f
                velocityTracker?.recycle()
                velocityTracker = null

                if (totalMoveDist < 18f && !scaleDetector.isInProgress) {
                    handleTap(event.x, event.y)
                } else if (!scaleDetector.isInProgress) {
                    panVelPxX = vx
                    panVelPxY = vy
                }
            }
            MotionEvent.ACTION_CANCEL -> {
                isPanning = false
                velocityTracker?.recycle()
                velocityTracker = null
            }
        }
        return true
    }

    private fun applyPanDeltaPx(dx: Float, dy: Float) {
        val halfW = 32f * zoom
        val halfH = 16f * zoom
        val da = -dx / halfW
        val db = -dy / halfH
        camX += (da + db) * 0.5f
        camY += (db - da) * 0.5f
        clampCamera()
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

        val hw = 32f * zoom
        val hh = 16f * zoom
        val hsw = width * 0.5f
        val hsh = height * 0.5f
        val tapRadiusPx = 40f
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
                val frameDt = ((nowNs - lastNs) / 1_000_000_000f).coerceIn(0.001f, 0.05f)
                lastNs = nowNs

                // Smooth camera fling inertia
                if (!isPanning && (abs(panVelPxX) > 8f || abs(panVelPxY) > 8f)) {
                    applyPanDeltaPx(panVelPxX * frameDt, panVelPxY * frameDt)
                    val damp = kotlin.math.exp(-7.5f * frameDt)
                    panVelPxX *= damp
                    panVelPxY *= damp
                }

                val sim = simulation
                var renderAlpha = 1f
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
                        renderAlpha = (accumulator / WorldSimulation.TICK_DT).coerceIn(0f, 1f)
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
                        drawIsometricWorld(canvas, sim, renderAlpha)
                    } finally {
                        holder.unlockCanvasAndPost(canvas)
                    }
                }

                val elapsedMs = (System.nanoTime() - nowNs) / 1_000_000L
                val sleepMs = 15L - elapsedMs
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

    private fun drawIsometricWorld(canvas: Canvas, sim: WorldSimulation?, renderAlpha: Float) {
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
        val propW = IsoTextureAtlas.PROP_W * 0.5f * zoom
        val propH = IsoTextureAtlas.PROP_H * 0.5f * zoom
        val propBaseY = IsoTextureAtlas.PROP_BASE_Y * 0.5f * zoom

        synchronized(sim.simLock) {
            val map = sim.map
            val mapSize = map.size

            // Tight isometric (u = tx - ty, v = tx + ty) screen frustum bounds (~2.5x fewer iterations)
            val uCenter = camX - camY
            val vCenter = camX + camY
            val uHalf = (hsw + tileW) / hw
            val vTop = (hsh + tileH * 2f) / hh
            val vBot = (hsh + tileH * 4.5f) / hh
            val uMin = floor(uCenter - uHalf).toInt()
            val uMax = ceil(uCenter + uHalf).toInt()
            val vMin = floor(vCenter - vTop).toInt()
            val vMax = ceil(vCenter + vBot).toInt()

            var minTx = mapSize
            var maxTx = -1
            var minTy = mapSize
            var maxTy = -1

            var drawCount = 0
            fun addDrawItem(
                depth: Float,
                kind: Int,
                tx: Int = 0,
                ty: Int = 0,
                wt: Byte = 0,
                loot: LootContainer? = null,
                st: CampStructure? = null,
                s: SurvivorAgent? = null,
                z: ZombieAgent? = null,
                b: BuildingInfo? = null
            ) {
                if (drawCount >= drawPool.size) return
                val item = drawPool[drawCount]
                item.depth = depth
                item.kind = kind
                item.tx = tx
                item.ty = ty
                item.wallType = wt
                item.loot = loot
                item.structure = st
                item.survivor = s
                item.zombie = z
                item.building = b
                drawActive[drawCount++] = item
            }

            // 1. Ground Terrain Pass (Strict screen-culled isometric diamond scan)
            val baseSxDiff = hsw - uCenter * hw
            val baseSyDiff = hsh - vCenter * hh

            for (v in vMin..vMax) {
                val sy = baseSyDiff + v * hh
                var u = uMin
                if (((u + v) and 1) != 0) u++
                while (u <= uMax) {
                    val tx = (u + v) shr 1
                    val ty = (v - u) shr 1
                    if (tx in 0 until mapSize && ty in 0 until mapSize) {
                        if (tx < minTx) minTx = tx
                        if (tx > maxTx) maxTx = tx
                        if (ty < minTy) minTy = ty
                        if (ty > maxTy) maxTy = ty

                        val sx = baseSxDiff + u * hw
                        val idx = ty * mapSize + tx
                        val bmp = atlas.getTerrain(map.terrain[idx], tx, ty, sim.tickCount)
                        dstRect.set(sx - hw, sy, sx + hw, sy + tileH)
                        canvas.drawBitmap(bmp, null, dstRect, bmpPaint)

                        val bld = map.bloodField[idx]
                        if (bld > 4f) {
                            fxPaint.color = Color.argb(min(185, (bld * 2.4f).toInt()), 153, 27, 27)
                            canvas.drawCircle(sx, sy + hh, 6f * zoom, fxPaint)
                        }

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

                        val wt = map.wall[idx]
                        if (wt != WallType.NONE) {
                            addDrawItem(depth = tx + ty + 1.0f, kind = 0, tx = tx, ty = ty, wt = wt)
                        }
                    }
                    u += 2
                }
            }

            if (minTx > maxTx || minTy > maxTy) return

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

            // Track which buildings currently hold living survivors for smart cutaway roofs
            val occLimit = min(map.buildings.size + 1, buildingOccupied.size)
            for (k in 0 until occLimit) buildingOccupied[k] = false

            val sel = selectedTarget
            if (sel is SelectionTarget.Building && sel.id in 1 until occLimit) {
                buildingOccupied[sel.id] = true
            }

            // 2. Collect Loot Crates, Structures, Zombies, Survivors & Building Roofs
            if (zoom >= 0.45f) {
                for (i in 0 until map.lootContainers.size) {
                    val c = map.lootContainers[i]
                    if (c.isEmpty()) continue
                    if (c.x < minTx || c.x > maxTx || c.y < minTy || c.y > maxTy) continue
                    addDrawItem(depth = c.x + c.y + 1.02f, kind = 1, loot = c)
                }
            }

            for (i in 0 until sim.structures.size) {
                val st = sim.structures[i]
                if (st.x < minTx - 2 || st.x > maxTx + 2 || st.y < minTy - 2 || st.y > maxTy + 2) continue
                addDrawItem(depth = st.x + st.y + 1.04f, kind = 2, st = st)
            }

            for (i in 0 until sim.zombies.size) {
                val z = sim.zombies[i]
                if (!z.alive) continue
                val rx = z.prevX + (z.x - z.prevX) * renderAlpha
                val ry = z.prevY + (z.y - z.prevY) * renderAlpha
                if (rx < minTx - 2 || rx > maxTx + 2 || ry < minTy - 2 || ry > maxTy + 2) continue
                addDrawItem(depth = rx + ry, kind = 4, z = z)
            }

            for (i in 0 until sim.survivors.size) {
                val s = sim.survivors[i]
                if (!s.alive) continue
                val stx = s.x.toInt()
                val sty = s.y.toInt()
                if (stx in 0 until mapSize && sty in 0 until mapSize) {
                    val bId = map.buildingId[sty * mapSize + stx]
                    if (bId in 1 until occLimit) {
                        buildingOccupied[bId] = true
                    }
                }
                val rx = s.prevX + (s.x - s.prevX) * renderAlpha
                val ry = s.prevY + (s.y - s.prevY) * renderAlpha
                if (rx < minTx - 2 || rx > maxTx + 2 || ry < minTy - 2 || ry > maxTy + 2) continue
                addDrawItem(depth = rx + ry, kind = 3, s = s)
            }

            // Queue 3D roofs + 2nd-story facades for buildings not opened by cutaway
            val showRoofs = overlayMode != OverlayMode.INTERIORS
            if (showRoofs) {
                for (i in 0 until map.buildings.size) {
                    val b = map.buildings[i]
                    if (b.x2 < minTx - 2 || b.x1 > maxTx + 2 || b.y2 < minTy - 2 || b.y1 > maxTy + 2) continue
                    if (b.id < occLimit && buildingOccupied[b.id]) continue
                    val roofDepth = max(b.x2 + b.y1, b.x1 + b.y2) + 0.05f
                    addDrawItem(depth = roofDepth, kind = 5, building = b)
                }
            }

            // Zero-allocation in-place quicksort
            if (drawCount > 1) {
                quickSortDrawItems(drawActive, 0, drawCount - 1)
            }

            // 3. Sorted 2.5D Render Pass
            for (i in 0 until drawCount) {
                val item = drawActive[i] ?: continue
                when (item.kind) {
                    0 -> {
                        val bmp = atlas.getWall(item.wallType) ?: continue
                        val sx = worldToScreenX(item.tx.toFloat(), item.ty.toFloat(), hsw, hw)
                        val sy = worldToScreenY(item.tx.toFloat(), item.ty.toFloat(), hsh, hh) + hh
                        dstRect.set(sx - propW * 0.5f, sy - propBaseY, sx + propW * 0.5f, sy + (propH - propBaseY))
                        if (showRoofs && zoom >= 0.7f && (item.wallType == WallType.WALL_BRICK || item.wallType == WallType.WALL_WOOD)) {
                            val bId = map.buildingId[item.ty * mapSize + item.tx]
                            if (bId in 1 until occLimit && buildingOccupied[bId]) {
                                bmpPaint.alpha = 185
                                canvas.drawBitmap(bmp, null, dstRect, bmpPaint)
                                bmpPaint.alpha = 255
                            } else {
                                canvas.drawBitmap(bmp, null, dstRect, bmpPaint)
                            }
                        } else if (!showRoofs && (item.wallType == WallType.WALL_BRICK || item.wallType == WallType.WALL_WOOD)) {
                            bmpPaint.alpha = 165
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
                        val rx = s.prevX + (s.x - s.prevX) * renderAlpha
                        val ry = s.prevY + (s.y - s.prevY) * renderAlpha
                        val sx = worldToScreenX(rx, ry, hsw, hw)
                        val sy = worldToScreenY(rx, ry, hsh, hh)
                        val isSel = sel is SelectionTarget.Survivor && sel.id == s.id
                        charRenderer.drawSurvivor(canvas, s, sim, sx, sy, zoom, renderAlpha, isSel, textPaint, bubbleBgPaint)
                    }
                    4 -> {
                        val z = item.zombie ?: continue
                        val rx = z.prevX + (z.x - z.prevX) * renderAlpha
                        val ry = z.prevY + (z.y - z.prevY) * renderAlpha
                        val sx = worldToScreenX(rx, ry, hsw, hw)
                        val sy = worldToScreenY(rx, ry, hsh, hh)
                        val isSel = sel is SelectionTarget.Zombie && sel.id == z.id
                        charRenderer.drawZombie(canvas, z, sx, sy, zoom, renderAlpha, isSel)
                    }
                    5 -> {
                        val b = item.building ?: continue
                        drawBuildingUpperAndRoof(canvas, b, sim, hsw, hsh, hw, hh)
                    }
                }
            }

            // 4. Sound rings, bullet tracers & particles
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

            // 5. Selected Agent Path & Memory Overlay
            if (sel is SelectionTarget.Survivor) {
                val s = sim.survivorsById[sel.id]
                if (s != null && s.alive) {
                    drawSelectedAgentOverlay(canvas, s, sim, hsw, hsh, hw, hh, renderAlpha)
                }
            }

            // 6. Weather & Day/Night Atmosphere
            drawAtmosphere(canvas, sim, w, h)
        }
    }

    /**
     * Renders realistic 2nd-story facades (windows, ledges) + real 3D pitched/hipped/parapet/commercial roofs
     * with chimneys, HVAC units, helipads, and storefront awnings.
     */
    private fun drawBuildingUpperAndRoof(
        canvas: Canvas,
        b: BuildingInfo,
        sim: WorldSimulation,
        hsw: Float,
        hsh: Float,
        hw: Float,
        hh: Float
    ) {
        val baseH = if (b.type == BuildingType.SUBURB_HOUSE) 29f * zoom else 32f * zoom
        val storyH = if (b.stories == 2) 25f * zoom else 0f
        val eaveH = baseH + storyH

        val x1 = b.x1.toFloat()
        val y1 = b.y1.toFloat()
        val x2 = (b.x2 + 1).toFloat()
        val y2 = (b.y2 + 1).toFloat()

        // 2nd Story Facade (for Apartments, Hospitals, Police)
        if (b.stories == 2) {
            val fLx = worldToScreenX(x1, y2, hsw, hw)
            val fLy = worldToScreenY(x1, y2, hsh, hh)
            val fBx = worldToScreenX(x2, y2, hsw, hw)
            val fBy = worldToScreenY(x2, y2, hsh, hh)
            val fRx = worldToScreenX(x2, y1, hsw, hw)
            val fRy = worldToScreenY(x2, y1, hsh, hh)

            val wallLeftCol = when (b.type) {
                BuildingType.HOSPITAL -> 0xFFD6DEE8.toInt()
                BuildingType.POLICE -> 0xFF475569.toInt()
                else -> 0xFF8C5341.toInt()
            }
            val wallRightCol = when (b.type) {
                BuildingType.HOSPITAL -> 0xFFAEBAC9.toInt()
                BuildingType.POLICE -> 0xFF334155.toInt()
                else -> 0xFF693D30.toInt()
            }

            // Front-left 2nd story wall
            roofPaint.color = wallLeftCol
            roofPath.reset()
            roofPath.moveTo(fLx, fLy - baseH)
            roofPath.lineTo(fBx, fBy - baseH)
            roofPath.lineTo(fBx, fBy - eaveH)
            roofPath.lineTo(fLx, fLy - eaveH)
            roofPath.close()
            canvas.drawPath(roofPath, roofPaint)

            // Front-right 2nd story wall
            roofPaint.color = wallRightCol
            roofPath.reset()
            roofPath.moveTo(fBx, fBy - baseH)
            roofPath.lineTo(fRx, fRy - baseH)
            roofPath.lineTo(fRx, fRy - eaveH)
            roofPath.lineTo(fBx, fBy - eaveH)
            roofPath.close()
            canvas.drawPath(roofPath, roofPaint)

            // 2nd story windows (glow warm amber at night)
            if (zoom >= 0.4f) {
                val winCol = if (sim.isNight && ((b.id + sim.day) % 3 != 0)) 0xFFFDE047.toInt() else 0xFF38BDF8.toInt()
                val winColR = if (sim.isNight && ((b.id + sim.day) % 2 == 0)) 0xFFFACC15.toInt() else 0xFF0284C7.toInt()
                val stepsL = (b.x2 - b.x1 + 1).coerceAtLeast(3)
                for (k in 1 until stepsL) {
                    val frac = k.toFloat() / stepsL
                    val wx = fLx + (fBx - fLx) * frac
                    val wy = fLy + (fBy - fLy) * frac - baseH - storyH * 0.5f
                    roofPaint.color = 0xFFE2E8F0.toInt()
                    canvas.drawRect(wx - 4.5f * zoom, wy - 6.5f * zoom, wx + 4.5f * zoom, wy + 5.5f * zoom, roofPaint)
                    roofPaint.color = winCol
                    canvas.drawRect(wx - 3.2f * zoom, wy - 5.2f * zoom, wx + 3.2f * zoom, wy + 4.2f * zoom, roofPaint)
                }
                val stepsR = (b.y2 - b.y1 + 1).coerceAtLeast(3)
                for (k in 1 until stepsR) {
                    val frac = k.toFloat() / stepsR
                    val wx = fBx + (fRx - fBx) * frac
                    val wy = fBy + (fRy - fBy) * frac - baseH - storyH * 0.5f
                    roofPaint.color = 0xFFCBD5E1.toInt()
                    canvas.drawRect(wx - 4.5f * zoom, wy - 6.5f * zoom, wx + 4.5f * zoom, wy + 5.5f * zoom, roofPaint)
                    roofPaint.color = winColR
                    canvas.drawRect(wx - 3.2f * zoom, wy - 5.2f * zoom, wx + 3.2f * zoom, wy + 4.2f * zoom, roofPaint)
                }
            }
        }

        // Overhanging roof eaves footprint
        val ov = 0.16f
        val ox1 = x1 - ov
        val oy1 = y1 - ov
        val ox2 = x2 + ov
        val oy2 = y2 + ov

        val pTx = worldToScreenX(ox1, oy1, hsw, hw)
        val pTy = worldToScreenY(ox1, oy1, hsh, hh) - eaveH
        val pRx = worldToScreenX(ox2, oy1, hsw, hw)
        val pRy = worldToScreenY(ox2, oy1, hsh, hh) - eaveH
        val pBx = worldToScreenX(ox2, oy2, hsw, hw)
        val pBy = worldToScreenY(ox2, oy2, hsh, hh) - eaveH
        val pLx = worldToScreenX(ox1, oy2, hsw, hw)
        val pLy = worldToScreenY(ox1, oy2, hsh, hh) - eaveH

        // Crisp under-eave drop shadow on top of walls
        roofStrokePaint.color = 0x66000000
        roofStrokePaint.strokeWidth = (4.5f * zoom).coerceAtLeast(1.5f)
        canvas.drawLine(pLx, pLy + 2f * zoom, pBx, pBy + 2f * zoom, roofStrokePaint)
        canvas.drawLine(pBx, pBy + 2f * zoom, pRx, pRy + 2f * zoom, roofStrokePaint)

        when (b.roofStyle) {
            0 -> {
                // Gabled pitched shingle roof (classic suburban house)
                val (litCol, darkCol) = when (b.roofColorIdx) {
                    0 -> 0xFFB44C3B.toInt() to 0xFF7E3125.toInt() // Terracotta red
                    1 -> 0xFF4B5D73.toInt() to 0xFF324052.toInt() // Slate blue-gray
                    2 -> 0xFF2F5E48.toInt() to 0xFF1E3F30.toInt() // Pine green
                    else -> 0xFF8B5A36.toInt() to 0xFF5D3A21.toInt() // Warm cedar
                }
                val ridgeH = eaveH + 22f * zoom
                val oyMid = (oy1 + oy2) * 0.5f
                val r1x = worldToScreenX(ox1, oyMid, hsw, hw)
                val r1y = worldToScreenY(ox1, oyMid, hsh, hh) - ridgeH
                val r2x = worldToScreenX(ox2, oyMid, hsw, hw)
                val r2y = worldToScreenY(ox2, oyMid, hsh, hh) - ridgeH

                // Back slope
                roofPaint.color = darkCol
                roofPath.reset()
                roofPath.moveTo(pTx, pTy); roofPath.lineTo(pRx, pRy); roofPath.lineTo(r2x, r2y); roofPath.lineTo(r1x, r1y); roofPath.close()
                canvas.drawPath(roofPath, roofPaint)

                // Gable wall triangle on right end
                roofPaint.color = 0xFFB0A089.toInt()
                roofPath.reset()
                roofPath.moveTo(pRx, pRy); roofPath.lineTo(pBx, pBy); roofPath.lineTo(r2x, r2y); roofPath.close()
                canvas.drawPath(roofPath, roofPaint)

                // Front lit slope
                roofPaint.color = litCol
                roofPath.reset()
                roofPath.moveTo(r1x, r1y); roofPath.lineTo(r2x, r2y); roofPath.lineTo(pBx, pBy); roofPath.lineTo(pLx, pLy); roofPath.close()
                canvas.drawPath(roofPath, roofPaint)

                // Shingle course lines
                if (zoom >= 0.5f) {
                    roofStrokePaint.color = 0x33000000
                    roofStrokePaint.strokeWidth = 1.4f * zoom
                    for (k in 1..3) {
                        val t = k * 0.25f
                        canvas.drawLine(
                            r1x + (pLx - r1x) * t, r1y + (pLy - r1y) * t,
                            r2x + (pBx - r2x) * t, r2y + (pBy - r2y) * t,
                            roofStrokePaint
                        )
                    }
                }

                // Ridge cap highlight
                roofStrokePaint.color = 0x55FFFFFF
                roofStrokePaint.strokeWidth = 2.4f * zoom
                canvas.drawLine(r1x, r1y, r2x, r2y, roofStrokePaint)

                // Brick chimney
                val chx = r1x + (r2x - r1x) * 0.28f
                val chy = r1y + (r2y - r1y) * 0.28f + 4f * zoom
                roofPaint.color = 0xFF7C2D12.toInt()
                canvas.drawRect(chx - 4.5f * zoom, chy - 14f * zoom, chx + 4.5f * zoom, chy, roofPaint)
                roofPaint.color = 0xFF94A3B8.toInt()
                canvas.drawRect(chx - 5.5f * zoom, chy - 16f * zoom, chx + 5.5f * zoom, chy - 13.5f * zoom, roofPaint)
            }
            1 -> {
                // Hipped / Pyramid Roof (House or Pharmacy)
                val (litCol, midCol, darkCol) = when (b.type) {
                    BuildingType.PHARMACY -> Triple(0xFF15803D.toInt(), 0xFF166534.toInt(), 0xFF14532D.toInt())
                    else -> Triple(0xFFA84632.toInt(), 0xFF8C3727.toInt(), 0xFF6B281B.toInt())
                }
                val peakH = eaveH + 20f * zoom
                val cx1 = ox1 + (ox2 - ox1) * 0.3f
                val cx2 = ox2 - (ox2 - ox1) * 0.3f
                val cyMid = (oy1 + oy2) * 0.5f
                val r1x = worldToScreenX(cx1, cyMid, hsw, hw)
                val r1y = worldToScreenY(cx1, cyMid, hsh, hh) - peakH
                val r2x = worldToScreenX(cx2, cyMid, hsw, hw)
                val r2y = worldToScreenY(cx2, cyMid, hsh, hh) - peakH

                roofPaint.color = darkCol
                roofPath.reset()
                roofPath.moveTo(pTx, pTy); roofPath.lineTo(pRx, pRy); roofPath.lineTo(r2x, r2y); roofPath.lineTo(r1x, r1y); roofPath.close()
                canvas.drawPath(roofPath, roofPaint)

                roofPaint.color = midCol
                roofPath.reset()
                roofPath.moveTo(pRx, pRy); roofPath.lineTo(pBx, pBy); roofPath.lineTo(r2x, r2y); roofPath.close()
                canvas.drawPath(roofPath, roofPaint)

                roofPaint.color = litCol
                roofPath.reset()
                roofPath.moveTo(r1x, r1y); roofPath.lineTo(r2x, r2y); roofPath.lineTo(pBx, pBy); roofPath.lineTo(pLx, pLy); roofPath.close()
                canvas.drawPath(roofPath, roofPaint)

                roofPaint.color = midCol
                roofPath.reset()
                roofPath.moveTo(pTx, pTy); roofPath.lineTo(r1x, r1y); roofPath.lineTo(pLx, pLy); roofPath.close()
                canvas.drawPath(roofPath, roofPaint)
            }
            2 -> {
                // Flat parapet rooftop (Apartment, Hospital, Police)
                roofPaint.color = 0xFF64748B.toInt()
                roofPath.reset()
                roofPath.moveTo(pTx, pTy); roofPath.lineTo(pRx, pRy); roofPath.lineTo(pBx, pBy); roofPath.lineTo(pLx, pLy); roofPath.close()
                canvas.drawPath(roofPath, roofPaint)

                // Recessed inner gravel/tar roof deck
                val inset = 0.45f
                val ix1 = worldToScreenX(x1 + inset, y1 + inset, hsw, hw)
                val iy1 = worldToScreenY(x1 + inset, y1 + inset, hsh, hh) - eaveH
                val ix2 = worldToScreenX(x2 - inset, y1 + inset, hsw, hw)
                val iy2 = worldToScreenY(x2 - inset, y1 + inset, hsh, hh) - eaveH
                val ix3 = worldToScreenX(x2 - inset, y2 - inset, hsw, hw)
                val iy3 = worldToScreenY(x2 - inset, y2 - inset, hsh, hh) - eaveH
                val ix4 = worldToScreenX(x1 + inset, y2 - inset, hsw, hw)
                val iy4 = worldToScreenY(x1 + inset, y2 - inset, hsh, hh) - eaveH

                roofPaint.color = if (b.type == BuildingType.HOSPITAL) 0xFF475569.toInt() else 0xFF334155.toInt()
                roofPath.reset()
                roofPath.moveTo(ix1, iy1); roofPath.lineTo(ix2, iy2); roofPath.lineTo(ix3, iy3); roofPath.lineTo(ix4, iy4); roofPath.close()
                canvas.drawPath(roofPath, roofPaint)

                val rcx = (ix1 + ix3) * 0.5f
                val rcy = (iy1 + iy3) * 0.5f
                if (b.type == BuildingType.HOSPITAL && zoom >= 0.45f) {
                    // Medical Cross / Helipad emblem
                    roofPaint.color = Color.WHITE
                    canvas.drawCircle(rcx, rcy, 12f * zoom, roofPaint)
                    roofPaint.color = 0xFFEF4444.toInt()
                    canvas.drawRect(rcx - 7f * zoom, rcy - 2.3f * zoom, rcx + 7f * zoom, rcy + 2.3f * zoom, roofPaint)
                    canvas.drawRect(rcx - 2.3f * zoom, rcy - 7f * zoom, rcx + 2.3f * zoom, rcy + 7f * zoom, roofPaint)
                } else if (zoom >= 0.45f) {
                    // Rooftop HVAC box & stairwell penthouse
                    roofPaint.color = 0xFF94A3B8.toInt()
                    canvas.drawRect(rcx - 14f * zoom, rcy - 8f * zoom, rcx - 3f * zoom, rcy + 2f * zoom, roofPaint)
                    roofPaint.color = 0xFF475569.toInt()
                    canvas.drawRect(rcx + 5f * zoom, rcy - 11f * zoom, rcx + 16f * zoom, rcy + 1f * zoom, roofPaint)
                    if (b.type == BuildingType.POLICE) {
                        roofPaint.color = 0xFF3B82F6.toInt()
                        canvas.drawRect(rcx + 5f * zoom, rcy - 13f * zoom, rcx + 16f * zoom, rcy - 10f * zoom, roofPaint)
                    }
                }
            }
            else -> {
                // Commercial / Warehouse / Supermarket / Gas Station ribbed roof + colored front awning
                roofPaint.color = when (b.type) {
                    BuildingType.SUPERMARKET -> 0xFF475569.toInt()
                    BuildingType.GAS_STATION -> 0xFF334155.toInt()
                    else -> 0xFF52525B.toInt()
                }
                roofPath.reset()
                roofPath.moveTo(pTx, pTy); roofPath.lineTo(pRx, pRy); roofPath.lineTo(pBx, pBy); roofPath.lineTo(pLx, pLy); roofPath.close()
                canvas.drawPath(roofPath, roofPaint)

                // Skylights / metal ribs
                if (zoom >= 0.45f) {
                    roofStrokePaint.color = 0x44CBD5E1
                    roofStrokePaint.strokeWidth = 2f * zoom
                    for (k in 1..3) {
                        val f = k * 0.25f
                        canvas.drawLine(
                            pTx + (pLx - pTx) * f, pTy + (pLy - pTy) * f,
                            pRx + (pBx - pRx) * f, pRy + (pBy - pRy) * f,
                            roofStrokePaint
                        )
                    }
                    // Storefront colored awning above front-left facade
                    val awningCol = when (b.type) {
                        BuildingType.SUPERMARKET -> 0xFFDC2626.toInt()
                        BuildingType.GAS_STATION -> 0xFFF59E0B.toInt()
                        else -> 0xFF0284C7.toInt()
                    }
                    val a1x = worldToScreenX(x1 + 0.6f, y2 + 0.05f, hsw, hw)
                    val a1y = worldToScreenY(x1 + 0.6f, y2 + 0.05f, hsh, hh) - baseH * 0.72f
                    val a2x = worldToScreenX(x2 - 0.6f, y2 + 0.05f, hsw, hw)
                    val a2y = worldToScreenY(x2 - 0.6f, y2 + 0.05f, hsh, hh) - baseH * 0.72f
                    roofPaint.color = awningCol
                    roofPath.reset()
                    roofPath.moveTo(a1x, a1y); roofPath.lineTo(a2x, a2y)
                    roofPath.lineTo(a2x - 6f * zoom, a2y + 7f * zoom)
                    roofPath.lineTo(a1x - 6f * zoom, a1y + 7f * zoom)
                    roofPath.close()
                    canvas.drawPath(roofPath, roofPaint)
                }
            }
        }
    }

    private fun quickSortDrawItems(arr: Array<IsoDrawItem?>, lo: Int, hi: Int) {
        if (lo >= hi) return
        var i = lo
        var j = hi
        val pivot = arr[(lo + hi) ushr 1]!!.depth
        while (i <= j) {
            while (arr[i]!!.depth < pivot) i++
            while (arr[j]!!.depth > pivot) j--
            if (i <= j) {
                val tmp = arr[i]
                arr[i] = arr[j]
                arr[j] = tmp
                i++
                j--
            }
        }
        if (lo < j) quickSortDrawItems(arr, lo, j)
        if (i < hi) quickSortDrawItems(arr, i, hi)
    }

    private fun drawSelectedAgentOverlay(
        canvas: Canvas,
        s: SurvivorAgent,
        sim: WorldSimulation,
        hsw: Float,
        hsh: Float,
        hw: Float,
        hh: Float,
        renderAlpha: Float
    ) {
        if (s.currentPath.isNotEmpty() && s.pathCursor < s.currentPath.size) {
            dashPaint.color = 0xFF38BDF8.toInt()
            val rx = s.prevX + (s.x - s.prevX) * renderAlpha
            val ry = s.prevY + (s.y - s.prevY) * renderAlpha
            var prevX = worldToScreenX(rx, ry, hsw, hw)
            var prevY = worldToScreenY(rx, ry, hsh, hh)
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
            for (i in 0..24) {
                val rx = ((i * 97 + off * 3) % w.toInt().coerceAtLeast(100)).toFloat()
                val ry = ((i * 131 + off * 7) % h.toInt().coerceAtLeast(100)).toFloat()
                canvas.drawLine(rx, ry, rx - 10f, ry + 24f, linePaint)
            }
        }
    }
}
