package com.zombieaquarium.sim.ui

import android.graphics.Bitmap
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Path
import com.zombieaquarium.sim.engine.TerrainType
import com.zombieaquarium.sim.engine.WallType
import kotlin.random.Random

/**
 * Generates and caches crisp 2.5D isometric Bitmap textures for every terrain,
 * building wall, prop, loot crate, and structure at startup (zero per-frame allocs).
 */
class IsoTextureAtlas {
    companion object {
        const val TILE_W = 64
        const val TILE_H = 32
        const val PROP_W = 64
        const val PROP_H = 76
        const val PROP_BASE_Y = 60f
    }

    private val terrainBitmaps = HashMap<Int, Bitmap>() // key = terrainType * 4 + variant
    private val wallBitmaps = HashMap<Byte, Bitmap>()
    val crateNormal: Bitmap
    val crateHigh: Bitmap
    val campfireFrames: Array<Bitmap>
    val campfireOut: Bitmap
    val rainCollector: Bitmap
    val spikeTrap: Bitmap

    private val p = Paint(Paint.ANTI_ALIAS_FLAG)
    private val pSharp = Paint()

    init {
        for (v in 0..2) {
            terrainBitmaps[TerrainType.GRASS * 4 + v] = makeGrassTile(v)
            terrainBitmaps[TerrainType.FOREST * 4 + v] = makeForestTile(v)
            terrainBitmaps[TerrainType.WATER * 4 + v] = makeWaterTile(v)
            terrainBitmaps[TerrainType.ROAD * 4 + v] = makeRoadTile(v)
        }
        terrainBitmaps[TerrainType.BRIDGE * 4] = makeBridgeTile()
        terrainBitmaps[TerrainType.CONCRETE * 4] = makeConcreteTile()
        terrainBitmaps[TerrainType.WOOD_FLOOR * 4] = makeWoodFloorTile()
        terrainBitmaps[TerrainType.TILE_FLOOR * 4] = makeCheckTileFloor()

        wallBitmaps[WallType.WALL_BRICK] = makeIsoBlock(0xFF64748B.toInt(), 0xFF475569.toInt(), 0xFF334155.toInt(), bricks = true, woodPlanks = false, height = 34f)
        wallBitmaps[WallType.WALL_WOOD] = makeIsoBlock(0xFF92400E.toInt(), 0xFF78350F.toInt(), 0xFF451A03.toInt(), bricks = false, woodPlanks = true, height = 30f)
        wallBitmaps[WallType.DOOR_CLOSED] = makeDoorProp(open = false, barricaded = false)
        wallBitmaps[WallType.DOOR_OPEN] = makeDoorProp(open = true, barricaded = false)
        wallBitmaps[WallType.DOOR_BARRICADED] = makeDoorProp(open = false, barricaded = true)
        wallBitmaps[WallType.WINDOW_INTACT] = makeWindowProp(broken = false)
        wallBitmaps[WallType.WINDOW_BROKEN] = makeWindowProp(broken = true)
        wallBitmaps[WallType.TREE] = makeTreeProp()
        wallBitmaps[WallType.CAR_WRECK] = makeCarWreckProp()
        wallBitmaps[WallType.FENCE] = makeIsoBlock(0xFF78350F.toInt(), 0xFF572408.toInt(), 0xFF3B1805.toInt(), bricks = false, woodPlanks = true, height = 16f)

        crateNormal = makeCrateProp(0xFF0284C7.toInt(), 0xFF0369A1.toInt())
        crateHigh = makeCrateProp(0xFFF59E0B.toInt(), 0xFFB45309.toInt())
        campfireFrames = Array(3) { makeCampfireProp(lit = true, frame = it) }
        campfireOut = makeCampfireProp(lit = false, frame = 0)
        rainCollector = makeRainCollectorProp()
        spikeTrap = makeSpikeTrapProp()
    }

    fun getTerrain(type: Byte, tx: Int, ty: Int, animTick: Int): Bitmap {
        val v = when (type) {
            TerrainType.WATER -> (animTick / 6 + tx + ty) % 3
            TerrainType.GRASS, TerrainType.FOREST, TerrainType.ROAD -> ((tx * 73856093 xor ty * 19349663) and 0x7FFFFFFF) % 3
            else -> 0
        }
        return terrainBitmaps[type * 4 + v] ?: terrainBitmaps[TerrainType.GRASS * 4]!!
    }

    fun getWall(type: Byte): Bitmap? = wallBitmaps[type]

    private fun diamondPath(cx: Float = TILE_W * 0.5f, cy: Float = TILE_H * 0.5f, hw: Float = TILE_W * 0.5f, hh: Float = TILE_H * 0.5f): Path {
        return Path().apply {
            moveTo(cx, cy - hh)
            lineTo(cx + hw, cy)
            lineTo(cx, cy + hh)
            lineTo(cx - hw, cy)
            close()
        }
    }

    private fun makeGrassTile(variant: Int): Bitmap {
        val bmp = Bitmap.createBitmap(TILE_W, TILE_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val path = diamondPath()
        p.color = when (variant) {
            0 -> 0xFF223A24.toInt()
            1 -> 0xFF264028.toInt()
            else -> 0xFF1F3521.toInt()
        }
        c.drawPath(path, p)
        c.clipPath(path)
        val rng = Random(100 + variant)
        repeat(18) {
            val gx = rng.nextInt(8, TILE_W - 8).toFloat()
            val gy = rng.nextInt(5, TILE_H - 5).toFloat()
            pSharp.color = if (it % 3 == 0) 0xFF345B37.toInt() else 0xFF192B1B.toInt()
            c.drawRect(gx, gy, gx + 2f, gy + 2f, pSharp)
        }
        p.style = Paint.Style.STROKE
        p.strokeWidth = 1f
        p.color = 0x22000000
        c.drawPath(path, p)
        p.style = Paint.Style.FILL
        return bmp
    }

    private fun makeForestTile(variant: Int): Bitmap {
        val bmp = Bitmap.createBitmap(TILE_W, TILE_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val path = diamondPath()
        p.color = if (variant == 1) 0xFF182D1B.toInt() else 0xFF142717.toInt()
        c.drawPath(path, p)
        c.clipPath(path)
        val rng = Random(200 + variant)
        repeat(20) {
            val gx = rng.nextInt(6, TILE_W - 6).toFloat()
            val gy = rng.nextInt(4, TILE_H - 4).toFloat()
            pSharp.color = if (it % 2 == 0) 0xFF28482C.toInt() else 0xFF2E2114.toInt()
            c.drawRect(gx, gy, gx + 2.5f, gy + 1.5f, pSharp)
        }
        return bmp
    }

    private fun makeWaterTile(frame: Int): Bitmap {
        val bmp = Bitmap.createBitmap(TILE_W, TILE_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val path = diamondPath()
        p.color = 0xFF1D4ED8.toInt()
        c.drawPath(path, p)
        c.clipPath(path)
        p.color = 0xFF2563EB.toInt()
        val off = (frame * 6) % 18
        c.drawLine(14f + off, 12f, 26f + off, 12f, p.apply { strokeWidth = 2f })
        c.drawLine(10f + (18 - off), 20f, 22f + (18 - off), 20f, p)
        p.color = 0x6693C5FD
        c.drawLine(24f + off * 0.5f, 16f, 34f + off * 0.5f, 16f, p.apply { strokeWidth = 1.4f })
        p.strokeWidth = 1f
        return bmp
    }

    private fun makeBridgeTile(): Bitmap {
        val bmp = Bitmap.createBitmap(TILE_W, TILE_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val path = diamondPath()
        p.color = 0xFF78350F.toInt()
        c.drawPath(path, p)
        c.clipPath(path)
        p.color = 0xFF451A03.toInt()
        p.strokeWidth = 1.5f
        for (i in 1..6) {
            val t = i / 7f
            c.drawLine(TILE_W * t, 0f, TILE_W * t, TILE_H.toFloat(), p)
        }
        p.color = 0xFFB45309.toInt()
        c.drawLine(0f, TILE_H * 0.5f, TILE_W * 0.5f, 0f, p)
        c.drawLine(TILE_W * 0.5f, TILE_H.toFloat(), TILE_W.toFloat(), TILE_H * 0.5f, p)
        return bmp
    }

    private fun makeRoadTile(variant: Int): Bitmap {
        val bmp = Bitmap.createBitmap(TILE_W, TILE_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val path = diamondPath()
        p.color = if (variant == 2) 0xFF27303F.toInt() else 0xFF1F2733.toInt()
        c.drawPath(path, p)
        c.clipPath(path)
        val rng = Random(300 + variant)
        repeat(14) {
            pSharp.color = if (it % 2 == 0) 0xFF334155.toInt() else 0xFF141A23.toInt()
            c.drawRect(rng.nextInt(10, 54).toFloat(), rng.nextInt(6, 26).toFloat(), rng.nextInt(12, 56).toFloat(), rng.nextInt(8, 28).toFloat(), pSharp)
        }
        if (variant == 0) {
            p.color = 0x99CBD5E1.toInt()
            p.strokeWidth = 2f
            c.drawLine(24f, 12f, 40f, 20f, p)
        }
        return bmp
    }

    private fun makeConcreteTile(): Bitmap {
        val bmp = Bitmap.createBitmap(TILE_W, TILE_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val path = diamondPath()
        p.color = 0xFF334155.toInt()
        c.drawPath(path, p)
        p.style = Paint.Style.STROKE
        p.color = 0xFF475569.toInt()
        c.drawPath(path, p)
        p.style = Paint.Style.FILL
        return bmp
    }

    private fun makeWoodFloorTile(): Bitmap {
        val bmp = Bitmap.createBitmap(TILE_W, TILE_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val path = diamondPath()
        p.color = 0xFF5C2D0C.toInt()
        c.drawPath(path, p)
        c.clipPath(path)
        p.color = 0xFF3E1C05.toInt()
        p.strokeWidth = 1.2f
        c.drawLine(12f, 6f, 52f, 26f, p)
        c.drawLine(6f, 13f, 42f, 31f, p)
        c.drawLine(22f, 1f, 58f, 19f, p)
        return bmp
    }

    private fun makeCheckTileFloor(): Bitmap {
        val bmp = Bitmap.createBitmap(TILE_W, TILE_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val path = diamondPath()
        p.color = 0xFF334155.toInt()
        c.drawPath(path, p)
        c.clipPath(path)
        p.color = 0xFF1E293B.toInt()
        c.drawPath(diamondPath(TILE_W * 0.5f, TILE_H * 0.25f, TILE_W * 0.25f, TILE_H * 0.25f), p)
        c.drawPath(diamondPath(TILE_W * 0.5f, TILE_H * 0.75f, TILE_W * 0.25f, TILE_H * 0.25f), p)
        return bmp
    }

    private fun makeIsoBlock(
        topColor: Int,
        leftColor: Int,
        rightColor: Int,
        bricks: Boolean,
        woodPlanks: Boolean,
        height: Float
    ): Bitmap {
        val bmp = Bitmap.createBitmap(PROP_W, PROP_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val cx = PROP_W * 0.5f
        val by = PROP_BASE_Y
        val hw = TILE_W * 0.5f
        val hh = TILE_H * 0.5f

        // Left face
        val leftFace = Path().apply {
            moveTo(cx - hw, by)
            lineTo(cx, by + hh)
            lineTo(cx, by + hh - height)
            lineTo(cx - hw, by - height)
            close()
        }
        p.color = leftColor
        c.drawPath(leftFace, p)

        // Right face
        val rightFace = Path().apply {
            moveTo(cx, by + hh)
            lineTo(cx + hw, by)
            lineTo(cx + hw, by - height)
            lineTo(cx, by + hh - height)
            close()
        }
        p.color = rightColor
        c.drawPath(rightFace, p)

        // Top face
        val topFace = diamondPath(cx, by - height, hw, hh)
        p.color = topColor
        c.drawPath(topFace, p)

        if (bricks) {
            p.color = 0x440F172A
            p.strokeWidth = 1.2f
            for (row in 1..4) {
                val dy = row * (height / 5f)
                c.drawLine(cx - hw + 2f, by - dy, cx - 1f, by + hh - dy, p)
                c.drawLine(cx + 1f, by + hh - dy, cx + hw - 2f, by - dy, p)
            }
        }
        if (woodPlanks) {
            p.color = 0x55290F02
            p.strokeWidth = 1.4f
            for (row in 1..3) {
                val dy = row * (height / 4f)
                c.drawLine(cx - hw + 1f, by - dy, cx, by + hh - dy, p)
                c.drawLine(cx, by + hh - dy, cx + hw - 1f, by - dy, p)
            }
        }
        return bmp
    }

    private fun makeDoorProp(open: Boolean, barricaded: Boolean): Bitmap {
        val bmp = Bitmap.createBitmap(PROP_W, PROP_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val cx = PROP_W * 0.5f
        val by = PROP_BASE_Y
        val hw = TILE_W * 0.5f
        val hh = TILE_H * 0.5f

        if (open) {
            // Side hinge leaf
            p.color = 0xFFB45309.toInt()
            val leaf = Path().apply {
                moveTo(cx - hw * 0.4f, by - 4f)
                lineTo(cx - hw * 0.1f, by + 2f)
                lineTo(cx - hw * 0.1f, by - 26f)
                lineTo(cx - hw * 0.4f, by - 32f)
                close()
            }
            c.drawPath(leaf, p)
            return bmp
        }

        // Closed wooden door slab
        val doorBody = makeIsoBlock(0xFFD97706.toInt(), 0xFFB45309.toInt(), 0xFF92400E.toInt(), bricks = false, woodPlanks = true, height = 28f)
        c.drawBitmap(doorBody, 0f, 0f, null)

        // Brass doorknob
        p.color = 0xFFFDE047.toInt()
        c.drawCircle(cx - 8f, by - 6f, 2.2f, p)

        if (barricaded) {
            // Diagonal & horizontal wooden planks nailed across
            p.color = 0xFFFBBF24.toInt()
            p.strokeWidth = 3.5f
            c.drawLine(cx - hw * 0.85f, by - 18f, cx + 2f, by + hh - 10f, p)
            c.drawLine(cx - hw * 0.85f, by - 6f, cx + 2f, by + hh - 22f, p)
            c.drawLine(cx + 2f, by + hh - 14f, cx + hw * 0.85f, by - 12f, p)
        }
        return bmp
    }

    private fun makeWindowProp(broken: Boolean): Bitmap {
        val bmp = Bitmap.createBitmap(PROP_W, PROP_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val base = makeIsoBlock(0xFF64748B.toInt(), 0xFF475569.toInt(), 0xFF334155.toInt(), bricks = true, woodPlanks = false, height = 14f)
        c.drawBitmap(base, 0f, 0f, null)

        val cx = PROP_W * 0.5f
        val by = PROP_BASE_Y
        val hw = TILE_W * 0.48f
        val hh = TILE_H * 0.48f
        p.color = if (broken) 0x5538BDF8 else 0xAA7DD3FC.toInt()
        val pane = Path().apply {
            moveTo(cx - hw, by - 14f)
            lineTo(cx, by + hh - 14f)
            lineTo(cx, by + hh - 30f)
            lineTo(cx - hw, by - 30f)
            close()
        }
        c.drawPath(pane, p)
        if (broken) {
            p.color = 0xFFE0F2FE.toInt()
            p.strokeWidth = 1.5f
            c.drawLine(cx - 22f, by - 18f, cx - 10f, by - 12f, p)
            c.drawLine(cx - 12f, by - 25f, cx - 4f, by - 10f, p)
        }
        return bmp
    }

    private fun makeTreeProp(): Bitmap {
        val bmp = Bitmap.createBitmap(PROP_W, PROP_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val cx = PROP_W * 0.5f
        val by = PROP_BASE_Y + 4f

        // Ground shadow
        p.color = 0x55000000
        c.drawOval(cx - 20f, by - 6f, cx + 20f, by + 10f, p)

        // Trunk
        p.color = 0xFF5C3317.toInt()
        c.drawRect(cx - 4f, by - 26f, cx + 4f, by + 2f, p)

        // Layered canopy
        p.color = 0xFF14532D.toInt()
        c.drawCircle(cx, by - 30f, 18f, p)
        p.color = 0xFF166534.toInt()
        c.drawCircle(cx - 6f, by - 36f, 14f, p)
        c.drawCircle(cx + 7f, by - 34f, 13f, p)
        p.color = 0xFF22C55E.toInt()
        c.drawCircle(cx - 2f, by - 44f, 10f, p)
        return bmp
    }

    private fun makeCarWreckProp(): Bitmap {
        val bmp = Bitmap.createBitmap(PROP_W, PROP_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val cx = PROP_W * 0.5f
        val by = PROP_BASE_Y + 2f

        // Shadow
        p.color = 0x66000000
        c.drawOval(cx - 24f, by - 6f, cx + 24f, by + 11f, p)

        // Body
        val chassis = makeIsoBlock(0xFF94A3B8.toInt(), 0xFF64748B.toInt(), 0xFF475569.toInt(), bricks = false, woodPlanks = false, height = 11f)
        c.drawBitmap(chassis, 0f, 0f, null)

        // Cabin
        p.color = 0xFF38BDF8.toInt()
        c.drawRect(cx - 11f, by - 20f, cx + 11f, by - 11f, p)
        p.color = 0xFFCBD5E1.toInt()
        c.drawRect(cx - 9f, by - 23f, cx + 9f, by - 19f, p)

        // Wheels
        p.color = 0xFF0F172A.toInt()
        c.drawCircle(cx - 14f, by + 4f, 4.5f, p)
        c.drawCircle(cx + 12f, by + 6f, 4.5f, p)
        return bmp
    }

    private fun makeCrateProp(topC: Int, sideC: Int): Bitmap {
        val bmp = Bitmap.createBitmap(PROP_W, PROP_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val cx = PROP_W * 0.5f
        val by = PROP_BASE_Y + 2f
        val hw = 14f
        val hh = 7f
        val h = 13f

        val left = Path().apply {
            moveTo(cx - hw, by); lineTo(cx, by + hh); lineTo(cx, by + hh - h); lineTo(cx - hw, by - h); close()
        }
        val right = Path().apply {
            moveTo(cx, by + hh); lineTo(cx + hw, by); lineTo(cx + hw, by - h); lineTo(cx, by + hh - h); close()
        }
        p.color = sideC; c.drawPath(left, p)
        p.color = (sideC and 0x00FEFEFE) ushr 1 or (0xFF shl 24); c.drawPath(right, p)
        p.color = topC; c.drawPath(diamondPath(cx, by - h, hw, hh), p)
        return bmp
    }

    private fun makeCampfireProp(lit: Boolean, frame: Int): Bitmap {
        val bmp = Bitmap.createBitmap(PROP_W, PROP_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val cx = PROP_W * 0.5f
        val by = PROP_BASE_Y + 2f

        p.color = 0xFF78716C.toInt()
        for (i in 0 until 6) {
            val ang = i * 1.047f
            c.drawCircle(cx + kotlin.math.cos(ang) * 9f, by + kotlin.math.sin(ang) * 4.5f, 3f, p)
        }
        p.color = 0xFF78350F.toInt()
        p.strokeWidth = 3f
        c.drawLine(cx - 8f, by - 3f, cx + 8f, by + 3f, p)
        c.drawLine(cx - 8f, by + 3f, cx + 8f, by - 3f, p)

        if (lit) {
            val flicker = (frame - 1) * 2.2f
            p.color = 0x44FB923C
            c.drawCircle(cx, by - 6f, 22f + frame * 2f, p)
            p.color = 0xFFF97316.toInt()
            c.drawOval(cx - 7f, by - 18f - frame * 2f, cx + 7f, by + 1f, p)
            p.color = 0xFFFDE047.toInt()
            c.drawOval(cx - 3.5f + flicker * 0.3f, by - 13f - frame, cx + 3.5f + flicker * 0.3f, by, p)
        }
        return bmp
    }

    private fun makeRainCollectorProp(): Bitmap {
        val bmp = Bitmap.createBitmap(PROP_W, PROP_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val cx = PROP_W * 0.5f
        val by = PROP_BASE_Y + 2f
        p.color = 0xFF78350F.toInt()
        c.drawRect(cx - 10f, by - 18f, cx + 10f, by + 2f, p)
        p.color = 0xFF94A3B8.toInt()
        c.drawRect(cx - 10f, by - 14f, cx + 10f, by - 12f, p)
        c.drawRect(cx - 10f, by - 4f, cx + 10f, by - 2f, p)
        p.color = 0xFF0EA5E9.toInt()
        c.drawOval(cx - 10f, by - 22f, cx + 10f, by - 14f, p)
        return bmp
    }

    private fun makeSpikeTrapProp(): Bitmap {
        val bmp = Bitmap.createBitmap(PROP_W, PROP_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val cx = PROP_W * 0.5f
        val by = PROP_BASE_Y + 2f
        p.color = 0xFFA16207.toInt()
        p.strokeWidth = 2.5f
        c.drawLine(cx - 10f, by + 3f, cx - 3f, by - 11f, p)
        c.drawLine(cx, by + 4f, cx + 5f, by - 12f, p)
        c.drawLine(cx + 8f, by + 2f, cx + 13f, by - 10f, p)
        return bmp
    }
}
