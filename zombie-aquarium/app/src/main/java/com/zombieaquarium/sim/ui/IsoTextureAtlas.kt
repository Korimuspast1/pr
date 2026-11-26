package com.zombieaquarium.sim.ui

import android.graphics.Bitmap
import android.graphics.Canvas
import android.graphics.Paint
import android.graphics.Path
import com.zombieaquarium.sim.engine.TerrainType
import com.zombieaquarium.sim.engine.WallType
import kotlin.math.cos
import kotlin.math.sin
import kotlin.random.Random

/**
 * High-resolution 128x64 isometric tile & 128x152 3D prop Bitmap atlas.
 * Every prop includes an anchored ground contact shadow on its tile footprint so
 * crates, barrels, cars, and trees sit firmly on the floor with zero floating illusion.
 */
class IsoTextureAtlas {
    companion object {
        const val TILE_W = 128
        const val TILE_H = 64
        const val PROP_W = 128
        const val PROP_H = 152
        const val PROP_BASE_Y = 120f
    }

    private val terrainBitmaps = HashMap<Int, Bitmap>()
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

        wallBitmaps[WallType.WALL_BRICK] = makeBrickWall()
        wallBitmaps[WallType.WALL_WOOD] = makeWoodWall()
        wallBitmaps[WallType.DOOR_CLOSED] = makeDoorProp(open = false, barricaded = false)
        wallBitmaps[WallType.DOOR_OPEN] = makeDoorProp(open = true, barricaded = false)
        wallBitmaps[WallType.DOOR_BARRICADED] = makeDoorProp(open = false, barricaded = true)
        wallBitmaps[WallType.WINDOW_INTACT] = makeWindowProp(broken = false)
        wallBitmaps[WallType.WINDOW_BROKEN] = makeWindowProp(broken = true)
        wallBitmaps[WallType.TREE] = makeTreeProp()
        wallBitmaps[WallType.CAR_WRECK] = makeCarWreckProp()
        wallBitmaps[WallType.FENCE] = makeIsoBlock(0xFF92400E.toInt(), 0xFF6B3410.toInt(), 0xFF451A03.toInt(), 28f, wood = true)

        crateNormal = makeCrateProp(highValue = false)
        crateHigh = makeCrateProp(highValue = true)
        campfireFrames = Array(3) { makeCampfireProp(lit = true, frame = it) }
        campfireOut = makeCampfireProp(lit = false, frame = 0)
        rainCollector = makeRainCollectorProp()
        spikeTrap = makeSpikeTrapProp()
    }

    fun getTerrain(type: Byte, tx: Int, ty: Int, animTick: Int): Bitmap {
        val v = when (type) {
            TerrainType.WATER -> (animTick / 5 + tx + ty) % 3
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
            0 -> 0xFF233D25.toInt()
            1 -> 0xFF27442A.toInt()
            else -> 0xFF1E3620.toInt()
        }
        c.drawPath(path, p)
        c.clipPath(path)

        val rng = Random(101 + variant * 17)
        // Subtle soil/moss patches
        repeat(8) {
            p.color = if (it % 2 == 0) 0xFF1A2F1C.toInt() else 0xFF2E4E31.toInt()
            c.drawOval(
                rng.nextInt(14, 100).toFloat(),
                rng.nextInt(10, 48).toFloat(),
                rng.nextInt(24, 118).toFloat(),
                rng.nextInt(16, 58).toFloat(),
                p
            )
        }
        // Crisp grass tufts
        p.strokeWidth = 1.6f
        repeat(24) {
            val gx = rng.nextInt(14, TILE_W - 14).toFloat()
            val gy = rng.nextInt(10, TILE_H - 10).toFloat()
            p.color = if (it % 3 == 0) 0xFF426B46.toInt() else 0xFF335937.toInt()
            c.drawLine(gx, gy, gx - 2.5f, gy - 4.5f, p)
            c.drawLine(gx, gy, gx + 2.5f, gy - 4f, p)
        }
        p.style = Paint.Style.STROKE
        p.strokeWidth = 1f
        p.color = 0x1A000000
        c.drawPath(path, p)
        p.style = Paint.Style.FILL
        return bmp
    }

    private fun makeForestTile(variant: Int): Bitmap {
        val bmp = Bitmap.createBitmap(TILE_W, TILE_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val path = diamondPath()
        p.color = if (variant == 1) 0xFF192E1C.toInt() else 0xFF142717.toInt()
        c.drawPath(path, p)
        c.clipPath(path)

        val rng = Random(205 + variant * 19)
        repeat(16) {
            p.color = if (it % 2 == 0) 0xFF2D2115.toInt() else 0xFF244328.toInt()
            val x = rng.nextInt(12, TILE_W - 16).toFloat()
            val y = rng.nextInt(8, TILE_H - 10).toFloat()
            c.drawOval(x, y, x + 9f, y + 4.5f, p)
        }
        repeat(26) {
            pSharp.color = if (it % 3 == 0) 0xFF3B663F.toInt() else 0xFF101F12.toInt()
            val x = rng.nextInt(12, TILE_W - 12).toFloat()
            val y = rng.nextInt(8, TILE_H - 8).toFloat()
            c.drawRect(x, y, x + 3f, y + 2f, pSharp)
        }
        return bmp
    }

    private fun makeWaterTile(frame: Int): Bitmap {
        val bmp = Bitmap.createBitmap(TILE_W, TILE_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val path = diamondPath()
        p.color = 0xFF1E40AF.toInt()
        c.drawPath(path, p)
        c.clipPath(path)

        p.color = 0xFF2563EB.toInt()
        p.strokeWidth = 2.6f
        val off = (frame * 10f)
        c.drawLine(24f + off, 20f, 52f + off, 20f, p)
        c.drawLine(16f + (22f - off), 38f, 44f + (22f - off), 38f, p)
        p.color = 0x8893C5FD.toInt()
        p.strokeWidth = 1.8f
        c.drawLine(48f + off * 0.6f, 29f, 70f + off * 0.6f, 29f, p)
        c.drawLine(36f - off * 0.5f, 46f, 56f - off * 0.5f, 46f, p)
        return bmp
    }

    private fun makeBridgeTile(): Bitmap {
        val bmp = Bitmap.createBitmap(TILE_W, TILE_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val path = diamondPath()
        p.color = 0xFF7C3A0E.toInt()
        c.drawPath(path, p)
        c.clipPath(path)
        p.color = 0xFF451A03.toInt()
        p.strokeWidth = 2.2f
        for (i in 1..8) {
            val t = i / 9f
            c.drawLine(TILE_W * t, 0f, TILE_W * t, TILE_H.toFloat(), p)
        }
        p.color = 0xFFD97706.toInt()
        p.strokeWidth = 3f
        c.drawLine(4f, TILE_H * 0.5f, TILE_W * 0.5f, 4f, p)
        c.drawLine(TILE_W * 0.5f, TILE_H - 4f, TILE_W - 4f, TILE_H * 0.5f, p)
        return bmp
    }

    private fun makeRoadTile(variant: Int): Bitmap {
        val bmp = Bitmap.createBitmap(TILE_W, TILE_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val path = diamondPath()
        p.color = if (variant == 2) 0xFF26303E.toInt() else 0xFF1E2632.toInt()
        c.drawPath(path, p)
        c.clipPath(path)

        val rng = Random(311 + variant * 13)
        repeat(28) {
            pSharp.color = if (it % 2 == 0) 0xFF334155.toInt() else 0xFF141B24.toInt()
            val x = rng.nextInt(14, TILE_W - 14).toFloat()
            val y = rng.nextInt(8, TILE_H - 8).toFloat()
            c.drawRect(x, y, x + 3f, y + 2f, pSharp)
        }
        // Asphalt crack
        p.color = 0xFF0F172A.toInt()
        p.strokeWidth = 1.4f
        c.drawLine(42f, 24f, 64f, 32f, p)
        c.drawLine(64f, 32f, 76f, 28f, p)

        if (variant == 0) {
            p.color = 0x99E2E8F0.toInt()
            p.strokeWidth = 3.2f
            c.drawLine(46f, 23f, 82f, 41f, p)
        }
        return bmp
    }

    private fun makeConcreteTile(): Bitmap {
        val bmp = Bitmap.createBitmap(TILE_W, TILE_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val path = diamondPath()
        p.color = 0xFF364354.toInt()
        c.drawPath(path, p)
        c.clipPath(path)
        p.color = 0xFF283342.toInt()
        p.strokeWidth = 1.8f
        c.drawLine(32f, 16f, 96f, 48f, p)
        c.drawLine(96f, 16f, 32f, 48f, p)
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
        p.color = 0xFF62310E.toInt()
        c.drawPath(path, p)
        c.clipPath(path)
        p.color = 0xFF3F1C05.toInt()
        p.strokeWidth = 1.8f
        for (off in -36..36 step 12) {
            c.drawLine(18f, 32f + off, 110f, 32f + off + 46f, p)
            c.drawLine(18f, 32f + off - 46f, 110f, 32f + off, p)
        }
        p.style = Paint.Style.STROKE
        p.color = 0x22000000
        c.drawPath(path, p)
        p.style = Paint.Style.FILL
        return bmp
    }

    private fun makeCheckTileFloor(): Bitmap {
        val bmp = Bitmap.createBitmap(TILE_W, TILE_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val path = diamondPath()
        p.color = 0xFF3B495E.toInt()
        c.drawPath(path, p)
        c.clipPath(path)
        p.color = 0xFF232F40.toInt()
        c.drawPath(diamondPath(TILE_W * 0.5f, TILE_H * 0.25f, TILE_W * 0.25f, TILE_H * 0.25f), p)
        c.drawPath(diamondPath(TILE_W * 0.5f, TILE_H * 0.75f, TILE_W * 0.25f, TILE_H * 0.25f), p)
        p.style = Paint.Style.STROKE
        p.strokeWidth = 1f
        p.color = 0x33000000
        c.drawPath(path, p)
        p.style = Paint.Style.FILL
        return bmp
    }

    private fun makeBrickWall(): Bitmap {
        val bmp = makeIsoBlock(0xFF94A3B8.toInt(), 0xFF7C4A3A.toInt(), 0xFF5C3428.toInt(), 64f, wood = false)
        val c = Canvas(bmp)
        val cx = PROP_W * 0.5f
        val by = PROP_BASE_Y
        val hw = TILE_W * 0.5f
        val hh = TILE_H * 0.5f
        val h = 64f

        // Stone plinth at base
        p.color = 0xFF475569.toInt()
        val plinthL = Path().apply {
            moveTo(cx - hw, by); lineTo(cx, by + hh); lineTo(cx, by + hh - 10f); lineTo(cx - hw, by - 10f); close()
        }
        c.drawPath(plinthL, p)
        p.color = 0xFF334155.toInt()
        val plinthR = Path().apply {
            moveTo(cx, by + hh); lineTo(cx + hw, by); lineTo(cx + hw, by - 10f); lineTo(cx, by + hh - 10f); close()
        }
        c.drawPath(plinthR, p)

        // Staggered brick courses on both visible faces
        p.color = 0x5529150E
        p.strokeWidth = 1.5f
        for (row in 2..6) {
            val dy = row * (h / 7f)
            c.drawLine(cx - hw + 2f, by - dy, cx - 1f, by + hh - dy, p)
            c.drawLine(cx + 1f, by + hh - dy, cx + hw - 2f, by - dy, p)
            val shift = if (row % 2 == 0) 0.25f else 0.5f
            for (k in 0..2) {
                val frac = (shift + k * 0.35f).coerceIn(0.1f, 0.9f)
                val lx = (cx - hw) + hw * frac
                val ly = by - dy + hh * frac
                c.drawLine(lx, ly, lx, ly + h / 7f, p)
                val rx = cx + hw * frac
                val ry = by + hh - dy - hh * frac
                c.drawLine(rx, ry, rx, ry + h / 7f, p)
            }
        }
        // Architectural cornice trim
        p.color = 0xFFCBD5E1.toInt()
        p.strokeWidth = 3f
        c.drawLine(cx - hw + 1f, by - h + 2f, cx, by + hh - h + 2f, p)
        c.drawLine(cx, by + hh - h + 2f, cx + hw - 1f, by - h + 2f, p)
        return bmp
    }

    private fun makeWoodWall(): Bitmap {
        // Warm suburban clapboard siding instead of bright orange
        val bmp = makeIsoBlock(0xFFD6CBB9.toInt(), 0xFFC2B299.toInt(), 0xFF9C8B72.toInt(), 58f, wood = true)
        val c = Canvas(bmp)
        val cx = PROP_W * 0.5f
        val by = PROP_BASE_Y
        val hw = TILE_W * 0.5f
        val hh = TILE_H * 0.5f
        // White corner trim & base foundation
        p.color = 0xFF64748B.toInt()
        c.drawLine(cx - hw, by - 4f, cx, by + hh - 4f, p)
        c.drawLine(cx, by + hh - 4f, cx + hw, by - 4f, p)
        p.color = 0xFFF1F5F9.toInt()
        p.strokeWidth = 3.5f
        c.drawLine(cx, by + hh - 2f, cx, by + hh - 58f, p)
        return bmp
    }

    private fun makeIsoBlock(topColor: Int, leftColor: Int, rightColor: Int, height: Float, wood: Boolean): Bitmap {
        val bmp = Bitmap.createBitmap(PROP_W, PROP_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val cx = PROP_W * 0.5f
        val by = PROP_BASE_Y
        val hw = TILE_W * 0.5f
        val hh = TILE_H * 0.5f

        // Floor contact AO shadow around base
        p.color = 0x55000000
        c.drawPath(diamondPath(cx, by + 2f, hw + 3f, hh + 2f), p)

        val leftFace = Path().apply {
            moveTo(cx - hw, by); lineTo(cx, by + hh); lineTo(cx, by + hh - height); lineTo(cx - hw, by - height); close()
        }
        p.color = leftColor; c.drawPath(leftFace, p)

        val rightFace = Path().apply {
            moveTo(cx, by + hh); lineTo(cx + hw, by); lineTo(cx + hw, by - height); lineTo(cx, by + hh - height); close()
        }
        p.color = rightColor; c.drawPath(rightFace, p)

        p.color = topColor
        c.drawPath(diamondPath(cx, by - height, hw, hh), p)

        if (wood) {
            p.color = 0x33291A0C
            p.strokeWidth = 1.6f
            for (row in 1..6) {
                val dy = row * (height / 7f)
                c.drawLine(cx - hw + 2f, by - dy, cx, by + hh - dy, p)
                c.drawLine(cx, by + hh - dy, cx + hw - 2f, by - dy, p)
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
            p.color = 0xFF78350F.toInt()
            val leaf = Path().apply {
                moveTo(cx - hw * 0.42f, by - 4f)
                lineTo(cx - hw * 0.1f, by + 4f)
                lineTo(cx - hw * 0.1f, by - 50f)
                lineTo(cx - hw * 0.42f, by - 58f)
                close()
            }
            c.drawPath(leaf, p)
            return bmp
        }

        // Wall surround with recessed front door + transom light
        val wallBase = makeIsoBlock(0xFF94A3B8.toInt(), 0xFF7A6A58.toInt(), 0xFF5A4C3E.toInt(), 58f, wood = false)
        c.drawBitmap(wallBase, 0f, 0f, null)

        // Left-face door panel
        p.color = 0xFF5C2D0C.toInt()
        val doorL = Path().apply {
            moveTo(cx - hw * 0.72f, by - 2f + hh * 0.28f)
            lineTo(cx - hw * 0.28f, by - 2f + hh * 0.72f)
            lineTo(cx - hw * 0.28f, by - 46f + hh * 0.72f)
            lineTo(cx - hw * 0.72f, by - 46f + hh * 0.28f)
            close()
        }
        c.drawPath(doorL, p)

        // Right-face door panel
        p.color = 0xFF431F07.toInt()
        val doorR = Path().apply {
            moveTo(cx + hw * 0.28f, by - 2f + hh * 0.72f)
            lineTo(cx + hw * 0.72f, by - 2f + hh * 0.28f)
            lineTo(cx + hw * 0.72f, by - 46f + hh * 0.28f)
            lineTo(cx + hw * 0.28f, by - 46f + hh * 0.72f)
            close()
        }
        c.drawPath(doorR, p)

        // Brass doorknobs
        p.color = 0xFFFDE047.toInt()
        c.drawCircle(cx - 24f, by - 6f, 3.2f, p)
        c.drawCircle(cx + 24f, by - 6f, 3.2f, p)

        if (barricaded) {
            p.color = 0xFFD97706.toInt()
            p.strokeWidth = 5.5f
            c.drawLine(cx - hw * 0.85f, by - 38f, cx + 4f, by + hh - 16f, p)
            c.drawLine(cx - hw * 0.85f, by - 14f, cx + 4f, by + hh - 42f, p)
            c.drawLine(cx + 4f, by + hh - 28f, cx + hw * 0.85f, by - 26f, p)
            p.color = 0xFF1E293B.toInt()
            c.drawCircle(cx - hw * 0.72f, by - 34f, 2.5f, p)
            c.drawCircle(cx - hw * 0.72f, by - 18f, 2.5f, p)
        }
        return bmp
    }

    private fun makeWindowProp(broken: Boolean): Bitmap {
        val bmp = Bitmap.createBitmap(PROP_W, PROP_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val base = makeIsoBlock(0xFF94A3B8.toInt(), 0xFF7D6E5D.toInt(), 0xFF5D5144.toInt(), 58f, wood = false)
        c.drawBitmap(base, 0f, 0f, null)

        val cx = PROP_W * 0.5f
        val by = PROP_BASE_Y
        val hw = TILE_W * 0.5f
        val hh = TILE_H * 0.5f

        // Framed window on left face
        p.color = 0xFFE2E8F0.toInt()
        val frameL = Path().apply {
            moveTo(cx - hw * 0.78f, by - 18f + hh * 0.22f)
            lineTo(cx - hw * 0.22f, by - 18f + hh * 0.78f)
            lineTo(cx - hw * 0.22f, by - 48f + hh * 0.78f)
            lineTo(cx - hw * 0.78f, by - 48f + hh * 0.22f)
            close()
        }
        c.drawPath(frameL, p)

        // Framed window on right face
        p.color = 0xFFCBD5E1.toInt()
        val frameR = Path().apply {
            moveTo(cx + hw * 0.22f, by - 18f + hh * 0.78f)
            lineTo(cx + hw * 0.78f, by - 18f + hh * 0.22f)
            lineTo(cx + hw * 0.78f, by - 48f + hh * 0.22f)
            lineTo(cx + hw * 0.22f, by - 48f + hh * 0.78f)
            close()
        }
        c.drawPath(frameR, p)

        // Glass panes
        p.color = if (broken) 0xFF1E293B.toInt() else 0xFF38BDF8.toInt()
        val paneL = Path().apply {
            moveTo(cx - hw * 0.72f, by - 21f + hh * 0.28f)
            lineTo(cx - hw * 0.28f, by - 21f + hh * 0.72f)
            lineTo(cx - hw * 0.28f, by - 45f + hh * 0.72f)
            lineTo(cx - hw * 0.72f, by - 45f + hh * 0.28f)
            close()
        }
        c.drawPath(paneL, p)

        p.color = if (broken) 0xFF0F172A.toInt() else 0xFF0284C7.toInt()
        val paneR = Path().apply {
            moveTo(cx + hw * 0.28f, by - 21f + hh * 0.72f)
            lineTo(cx + hw * 0.72f, by - 21f + hh * 0.28f)
            lineTo(cx + hw * 0.72f, by - 45f + hh * 0.28f)
            lineTo(cx + hw * 0.28f, by - 45f + hh * 0.72f)
            close()
        }
        c.drawPath(paneR, p)

        if (broken) {
            p.color = 0xFFBAE6FD.toInt()
            p.strokeWidth = 2.2f
            c.drawLine(cx - 42f, by - 22f, cx - 22f, by - 12f, p)
            c.drawLine(cx + 22f, by - 12f, cx + 42f, by - 24f, p)
        } else {
            // Window mullions
            p.color = 0xFFE2E8F0.toInt()
            p.strokeWidth = 2f
            c.drawLine(cx - hw * 0.5f, by - 21f + hh * 0.5f, cx - hw * 0.5f, by - 45f + hh * 0.5f, p)
            c.drawLine(cx + hw * 0.5f, by - 21f + hh * 0.5f, cx + hw * 0.5f, by - 45f + hh * 0.5f, p)
        }
        return bmp
    }

    private fun makeTreeProp(): Bitmap {
        val bmp = Bitmap.createBitmap(PROP_W, PROP_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val cx = PROP_W * 0.5f
        val by = PROP_BASE_Y + 4f

        // Ground shadow on tile footprint
        p.color = 0x77000000
        c.drawOval(cx - 38f, by - 14f, cx + 38f, by + 18f, p)

        // Root flare & textured trunk
        p.color = 0xFF451A03.toInt()
        c.drawRect(cx - 8f, by - 52f, cx + 8f, by + 5f, p)
        p.color = 0xFF78350F.toInt()
        c.drawRect(cx - 5f, by - 50f, cx + 2f, by + 4f, p)

        // Multi-tiered shaded foliage
        p.color = 0xFF0F3D21.toInt()
        c.drawCircle(cx, by - 56f, 34f, p)
        p.color = 0xFF14532D.toInt()
        c.drawCircle(cx - 14f, by - 66f, 26f, p)
        c.drawCircle(cx + 14f, by - 64f, 25f, p)
        p.color = 0xFF166534.toInt()
        c.drawCircle(cx, by - 78f, 24f, p)
        p.color = 0xFF22C55E.toInt()
        c.drawCircle(cx - 6f, by - 88f, 16f, p)
        return bmp
    }

    private fun makeCarWreckProp(): Bitmap {
        val bmp = Bitmap.createBitmap(PROP_W, PROP_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val cx = PROP_W * 0.5f
        val by = PROP_BASE_Y + 2f

        // Undercarriage shadow grounded on asphalt
        p.color = 0x88000000.toInt()
        c.drawPath(diamondPath(cx, by + 5f, 46f, 23f), p)

        // Rusted chassis
        val chassis = makeIsoBlock(0xFF94A3B8.toInt(), 0xFF64748B.toInt(), 0xFF475569.toInt(), 20f, wood = false)
        c.drawBitmap(chassis, 0f, 0f, null)

        // Cabin + windshield
        p.color = 0xFF38BDF8.toInt()
        c.drawRect(cx - 22f, by - 38f, cx + 22f, by - 20f, p)
        p.color = 0xFFCBD5E1.toInt()
        c.drawRect(cx - 18f, by - 44f, cx + 18f, by - 36f, p)

        // Rubber tires anchored on ground
        p.color = 0xFF090D12.toInt()
        c.drawCircle(cx - 28f, by + 8f, 8.5f, p)
        c.drawCircle(cx + 24f, by + 11f, 8.5f, p)
        p.color = 0xFF64748B.toInt()
        c.drawCircle(cx - 28f, by + 8f, 3.5f, p)
        c.drawCircle(cx + 24f, by + 11f, 3.5f, p)
        return bmp
    }

    /**
     * Grounded supply/loot crate sitting firmly on a dark wooden pallet + cast isometric contact shadow.
     */
    private fun makeCrateProp(highValue: Boolean): Bitmap {
        val bmp = Bitmap.createBitmap(PROP_W, PROP_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val cx = PROP_W * 0.5f
        val by = PROP_BASE_Y + 6f
        val hw = 26f
        val hh = 13f
        val h = 24f

        // 1. Strong grounded contact shadow directly on floor diamond
        p.color = 0x99000000.toInt()
        c.drawPath(diamondPath(cx + 3f, by + 5f, hw + 6f, hh + 3f), p)

        // 2. Dark wooden pallet base sitting flush on floor
        p.color = 0xFF3E1C05.toInt()
        c.drawPath(diamondPath(cx, by + 2f, hw + 2f, hh + 1f), p)

        // 3. Crate body
        val topC = if (highValue) 0xFFF59E0B.toInt() else 0xFF0EA5E9.toInt()
        val leftC = if (highValue) 0xFFD97706.toInt() else 0xFF0284C7.toInt()
        val rightC = if (highValue) 0xFF92400E.toInt() else 0xFF0369A1.toInt()

        val left = Path().apply {
            moveTo(cx - hw, by); lineTo(cx, by + hh); lineTo(cx, by + hh - h); lineTo(cx - hw, by - h); close()
        }
        val right = Path().apply {
            moveTo(cx, by + hh); lineTo(cx + hw, by); lineTo(cx + hw, by - h); lineTo(cx, by + hh - h); close()
        }
        p.color = leftC; c.drawPath(left, p)
        p.color = rightC; c.drawPath(right, p)
        p.color = topC; c.drawPath(diamondPath(cx, by - h, hw, hh), p)

        // 4. Metal edge straps & rivets
        p.color = 0xFF1E293B.toInt()
        p.strokeWidth = 2.6f
        c.drawLine(cx - hw, by - h * 0.5f, cx, by + hh - h * 0.5f, p)
        c.drawLine(cx, by + hh - h * 0.5f, cx + hw, by - h * 0.5f, p)
        p.style = Paint.Style.STROKE
        p.strokeWidth = 1.6f
        p.color = 0xAA0F172A.toInt()
        c.drawPath(diamondPath(cx, by - h, hw, hh), p)
        p.style = Paint.Style.FILL
        return bmp
    }

    private fun makeCampfireProp(lit: Boolean, frame: Int): Bitmap {
        val bmp = Bitmap.createBitmap(PROP_W, PROP_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val cx = PROP_W * 0.5f
        val by = PROP_BASE_Y + 4f

        // Ground ash patch & shadow
        p.color = 0x88000000.toInt()
        c.drawOval(cx - 24f, by - 8f, cx + 24f, by + 12f, p)

        // Stone ring
        p.color = 0xFF78716C.toInt()
        for (i in 0 until 8) {
            val ang = i * 0.785f
            c.drawCircle(cx + cos(ang) * 18f, by + sin(ang) * 9f, 5.2f, p)
        }
        // Crossed firewood
        p.color = 0xFF78350F.toInt()
        p.strokeWidth = 5f
        c.drawLine(cx - 15f, by - 5f, cx + 15f, by + 5f, p)
        c.drawLine(cx - 15f, by + 5f, cx + 15f, by - 5f, p)

        if (lit) {
            val fShift = (frame - 1) * 3f
            p.color = 0x44FB923C
            c.drawCircle(cx, by - 12f, 38f + frame * 4f, p)
            p.color = 0xFFF97316.toInt()
            c.drawOval(cx - 13f, by - 36f - frame * 4f, cx + 13f, by + 2f, p)
            p.color = 0xFFFDE047.toInt()
            c.drawOval(cx - 6.5f + fShift * 0.4f, by - 26f - frame * 2f, cx + 6.5f + fShift * 0.4f, by, p)
        }
        return bmp
    }

    private fun makeRainCollectorProp(): Bitmap {
        val bmp = Bitmap.createBitmap(PROP_W, PROP_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val cx = PROP_W * 0.5f
        val by = PROP_BASE_Y + 4f

        p.color = 0x88000000.toInt()
        c.drawOval(cx - 22f, by - 6f, cx + 22f, by + 10f, p)

        p.color = 0xFF78350F.toInt()
        c.drawRect(cx - 18f, by - 36f, cx + 18f, by + 4f, p)
        p.color = 0xFF94A3B8.toInt()
        c.drawRect(cx - 18f, by - 28f, cx + 18f, by - 24f, p)
        c.drawRect(cx - 18f, by - 8f, cx + 18f, by - 4f, p)
        p.color = 0xFF0EA5E9.toInt()
        c.drawOval(cx - 18f, by - 42f, cx + 18f, by - 28f, p)
        return bmp
    }

    private fun makeSpikeTrapProp(): Bitmap {
        val bmp = Bitmap.createBitmap(PROP_W, PROP_H, Bitmap.Config.ARGB_8888)
        val c = Canvas(bmp)
        val cx = PROP_W * 0.5f
        val by = PROP_BASE_Y + 4f
        p.color = 0x77000000
        c.drawPath(diamondPath(cx, by + 2f, 24f, 12f), p)
        p.color = 0xFFA16207.toInt()
        p.strokeWidth = 4.5f
        c.drawLine(cx - 18f, by + 4f, cx - 6f, by - 22f, p)
        c.drawLine(cx, by + 6f, cx + 8f, by - 24f, p)
        c.drawLine(cx + 14f, by + 2f, cx + 22f, by - 20f, p)
        return bmp
    }
}
