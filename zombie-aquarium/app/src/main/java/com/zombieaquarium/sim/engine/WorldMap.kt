package com.zombieaquarium.sim.engine

import kotlin.math.abs
import kotlin.math.max
import kotlin.math.min
import kotlin.math.sin
import kotlin.math.sqrt

data class LootContainer(
    val id: Int,
    val x: Int,
    val y: Int,
    val buildingId: Int,
    var food: Int = 0,
    var water: Int = 0,
    var bandages: Int = 0,
    var antibiotics: Int = 0,
    var wood: Int = 0,
    var scrap: Int = 0,
    var ammo: Int = 0,
    var fuel: Int = 0,
    var weapon: WeaponType? = null,
    var hasFlashlight: Boolean = false,
    var searched: Boolean = false
) {
    fun isEmpty(): Boolean {
        return food <= 0 && water <= 0 && bandages <= 0 && antibiotics <= 0 &&
            wood <= 0 && scrap <= 0 && ammo <= 0 && fuel <= 0 &&
            weapon == null && !hasFlashlight
    }
}

data class BuildingInfo(
    val id: Int,
    val type: BuildingType,
    val x1: Int,
    val y1: Int,
    val x2: Int,
    val y2: Int,
    val stories: Int = 1,
    val roofStyle: Int = 0, // 0=gabled pitched, 1=hipped pitched, 2=flat parapet, 3=commercial/awning
    val roofColorIdx: Int = 0,
    val facadeColorIdx: Int = 0,
    val doorTiles: MutableList<Int> = ArrayList(),
    val windowTiles: MutableList<Int> = ArrayList(),
    var claimedByCampId: Int = -1
) {
    val centerX: Float get() = (x1 + x2) * 0.5f
    val centerY: Float get() = (y1 + y2) * 0.5f
    val area: Int get() = (x2 - x1 + 1) * (y2 - y1 + 1)
}

data class CampStructure(
    val id: Int,
    val type: StructureType,
    val x: Int,
    val y: Int,
    val campId: Int,
    var hp: Float = 100f,
    var fuelTicks: Int = 180,
    var storedWater: Int = 0
)

class WorldMap(val size: Int, seed: Long, lootRarity: Float) {
    val totalTiles = size * size
    val terrain = ByteArray(totalTiles)
    val wall = ByteArray(totalTiles)
    val wallHp = FloatArray(totalTiles)
    val buildingId = IntArray(totalTiles) { -1 }

    // Physical fields
    val noiseField = FloatArray(totalTiles)
    val bloodField = FloatArray(totalTiles)
    val lightField = FloatArray(totalTiles)
    val dangerField = FloatArray(totalTiles)

    // Zombie Flow Field (distance & steering direction toward noise/blood/light/humans)
    val flowDist = IntArray(totalTiles) { 9999 }
    val flowDx = FloatArray(totalTiles)
    val flowDy = FloatArray(totalTiles)
    private val flowQueue = IntArray(totalTiles)

    val buildings = ArrayList<BuildingInfo>()
    val lootContainers = ArrayList<LootContainer>()
    val lootByTile = HashMap<Int, LootContainer>()

    // Reusable A* primitive buffers (zero per-call allocations!)
    private val aStarG = FloatArray(totalTiles)
    private val aStarParent = IntArray(totalTiles)
    private val aStarStamp = IntArray(totalTiles)
    private val aStarClosedStamp = IntArray(totalTiles)
    private var aStarRunId = 1
    private val heapNodes = IntArray(totalTiles)
    private val heapF = FloatArray(totalTiles)
    private val tempPath = IntArray(512)

    // Chunked hierarchical connectivity (16x16 chunks)
    val chunkSize = 16
    val chunkCols = (size + chunkSize - 1) / chunkSize

    init {
        generate(SimRng(seed), seed, lootRarity)
    }

    fun idx(x: Int, y: Int): Int = y * size + x
    fun inBounds(x: Int, y: Int): Boolean = x in 0 until size && y in 0 until size

    fun isWalkableForHuman(x: Int, y: Int, allowClosedDoor: Boolean = true): Boolean {
        if (!inBounds(x, y)) return false
        val i = idx(x, y)
        if (terrain[i] == TerrainType.WATER) return false
        return when (wall[i]) {
            WallType.NONE, WallType.DOOR_OPEN, WallType.WINDOW_BROKEN -> true
            WallType.DOOR_CLOSED -> allowClosedDoor
            else -> false
        }
    }

    fun isWalkableForZombie(x: Int, y: Int): Boolean {
        if (!inBounds(x, y)) return false
        val i = idx(x, y)
        if (terrain[i] == TerrainType.WATER) return false
        return when (wall[i]) {
            WallType.NONE, WallType.DOOR_OPEN, WallType.WINDOW_BROKEN -> true
            else -> false
        }
    }

    fun blocksSight(x: Int, y: Int): Boolean {
        if (!inBounds(x, y)) return true
        val w = wall[idx(x, y)]
        return w == WallType.WALL_BRICK || w == WallType.WALL_WOOD ||
            w == WallType.DOOR_CLOSED || w == WallType.DOOR_BARRICADED ||
            w == WallType.TREE
    }

    fun hasLineOfSight(x0: Int, y0: Int, x1: Int, y1: Int, maxDist: Int): Boolean {
        val dx = abs(x1 - x0)
        val dy = abs(y1 - y0)
        if (dx * dx + dy * dy > maxDist * maxDist) return false
        var cx = x0
        var cy = y0
        val sx = if (x0 < x1) 1 else -1
        val sy = if (y0 < y1) 1 else -1
        var err = dx - dy
        while (cx != x1 || cy != y1) {
            if (!(cx == x0 && cy == y0) && blocksSight(cx, cy)) return false
            val e2 = 2 * err
            if (e2 > -dy) {
                err -= dy
                cx += sx
            }
            if (e2 < dx) {
                err += dx
                cy += sy
            }
        }
        return true
    }

    fun addNoise(cx: Int, cy: Int, radius: Float, strength: Float) {
        val r = radius.toInt().coerceIn(1, 52)
        val r2 = radius * radius
        val minX = max(0, cx - r)
        val maxX = min(size - 1, cx + r)
        val minY = max(0, cy - r)
        val maxY = min(size - 1, cy + r)
        for (y in minY..maxY) {
            for (x in minX..maxX) {
                val dx = (x - cx).toFloat()
                val dy = (y - cy).toFloat()
                val d2 = dx * dx + dy * dy
                if (d2 <= r2) {
                    val i = idx(x, y)
                    val falloff = 1f - sqrt(d2) / radius
                    val wallDamp = if (wall[i] == WallType.WALL_BRICK || wall[i] == WallType.WALL_WOOD) 0.45f else 1f
                    val v = strength * falloff * wallDamp
                    if (v > noiseField[i]) noiseField[i] = min(100f, v)
                }
            }
        }
    }

    fun addBlood(cx: Int, cy: Int, amount: Float) {
        if (!inBounds(cx, cy)) return
        val i = idx(cx, cy)
        bloodField[i] = min(100f, bloodField[i] + amount)
    }

    fun addDanger(cx: Int, cy: Int, radius: Int, amount: Float) {
        val minX = max(0, cx - radius)
        val maxX = min(size - 1, cx + radius)
        val minY = max(0, cy - radius)
        val maxY = min(size - 1, cy + radius)
        for (y in minY..maxY) {
            for (x in minX..maxX) {
                val i = idx(x, y)
                dangerField[i] = min(100f, dangerField[i] + amount)
            }
        }
    }

    fun decayFields() {
        for (i in 0 until totalTiles) {
            noiseField[i] *= 0.84f
            if (noiseField[i] < 0.2f) noiseField[i] = 0f
            bloodField[i] *= 0.994f
            if (bloodField[i] < 0.1f) bloodField[i] = 0f
            lightField[i] *= 0.65f
            if (lightField[i] < 0.1f) lightField[i] = 0f
            dangerField[i] *= 0.985f
            if (dangerField[i] < 0.1f) dangerField[i] = 0f
        }
    }

    /**
     * Recomputes multi-source BFS Flow Field for zombie hordes toward active noise, blood, lights, and visible beacons.
     */
    fun rebuildZombieFlowField(beaconsX: IntArray, beaconsY: IntArray, beaconCount: Int) {
        flowDist.fill(9999)
        var head = 0
        var tail = 0

        for (b in 0 until beaconCount) {
            val bx = beaconsX[b]
            val by = beaconsY[b]
            if (inBounds(bx, by)) {
                val idx = idx(bx, by)
                if (flowDist[idx] != 0) {
                    flowDist[idx] = 0
                    flowQueue[tail++] = idx
                }
            }
        }

        // Also seed strong noise & blood tiles
        var step = 0
        while (step < totalTiles) {
            if (noiseField[step] > 14f || bloodField[step] > 28f || lightField[step] > 20f) {
                if (flowDist[step] != 0) {
                    flowDist[step] = 0
                    flowQueue[tail++] = step
                }
            }
            step += 2
        }

        while (head < tail) {
            val cur = flowQueue[head++]
            val cd = flowDist[cur]
            if (cd >= 65) continue
            val cx = cur % size
            val cy = cur / size

            // 4 neighbors
            for (dir in 0 until 4) {
                val nx = when (dir) { 0 -> cx + 1; 1 -> cx - 1; else -> cx }
                val ny = when (dir) { 2 -> cy + 1; 3 -> cy - 1; else -> cy }
                if (!inBounds(nx, ny)) continue
                val nIdx = ny * size + nx
                if (terrain[nIdx] == TerrainType.WATER) continue
                val w = wall[nIdx]
                if (w == WallType.WALL_BRICK || w == WallType.WALL_WOOD || w == WallType.TREE || w == WallType.CAR_WRECK) continue
                val cost = if (w == WallType.DOOR_CLOSED || w == WallType.DOOR_BARRICADED || w == WallType.WINDOW_INTACT) 3 else 1
                val nd = cd + cost
                if (nd < flowDist[nIdx]) {
                    flowDist[nIdx] = nd
                    flowQueue[tail++] = nIdx
                }
            }
        }

        // Compute normalized gradient vectors
        for (y in 1 until size - 1) {
            val row = y * size
            for (x in 1 until size - 1) {
                val i = row + x
                val d = flowDist[i]
                if (d >= 9999) {
                    flowDx[i] = 0f
                    flowDy[i] = 0f
                    continue
                }
                val dl = flowDist[i - 1]
                val dr = flowDist[i + 1]
                val du = flowDist[i - size]
                val dd = flowDist[i + size]
                var vx = 0f
                var vy = 0f
                if (dl < d) vx -= 1f
                if (dr < d) vx += 1f
                if (du < d) vy -= 1f
                if (dd < d) vy += 1f
                val len = sqrt(vx * vx + vy * vy)
                if (len > 0.01f) {
                    flowDx[i] = vx / len
                    flowDy[i] = vy / len
                } else {
                    flowDx[i] = 0f
                    flowDy[i] = 0f
                }
            }
        }
    }

    /**
     * Danger-aware A* pathfinder with zero per-call heap allocations.
     */
    fun findPath(
        sx: Int,
        sy: Int,
        gx: Int,
        gy: Int,
        avoidDanger: Boolean,
        maxExpansions: Int = 1800,
        outPath: MutableList<Int>
    ): Boolean {
        outPath.clear()
        if (!inBounds(sx, sy) || !inBounds(gx, gy)) return false
        val start = idx(sx, sy)
        val goal = idx(gx, gy)
        if (start == goal) return true

        aStarRunId++
        if (aStarRunId == Int.MAX_VALUE) {
            aStarStamp.fill(0)
            aStarClosedStamp.fill(0)
            aStarRunId = 1
        }
        val run = aStarRunId

        var heapSize = 0
        fun push(node: Int, f: Float) {
            if (heapSize >= heapNodes.size) return
            var c = heapSize++
            while (c > 0) {
                val p = (c - 1) ushr 1
                if (heapF[p] <= f) break
                heapNodes[c] = heapNodes[p]
                heapF[c] = heapF[p]
                c = p
            }
            heapNodes[c] = node
            heapF[c] = f
        }

        fun pop(): Int {
            val top = heapNodes[0]
            heapSize--
            if (heapSize > 0) {
                val lastNode = heapNodes[heapSize]
                val lastF = heapF[heapSize]
                var c = 0
                while (true) {
                    val l = (c shl 1) + 1
                    if (l >= heapSize) break
                    val r = l + 1
                    val best = if (r < heapSize && heapF[r] < heapF[l]) r else l
                    if (heapF[best] >= lastF) break
                    heapNodes[c] = heapNodes[best]
                    heapF[c] = heapF[best]
                    c = best
                }
                heapNodes[c] = lastNode
                heapF[c] = lastF
            }
            return top
        }

        aStarStamp[start] = run
        aStarG[start] = 0f
        aStarParent[start] = -1
        push(start, abs(gx - sx).toFloat() + abs(gy - sy).toFloat())

        var expansions = 0
        var bestReached = start
        var bestDist = abs(gx - sx) + abs(gy - sy)

        while (heapSize > 0 && expansions < maxExpansions) {
            val cur = pop()
            if (aStarClosedStamp[cur] == run) continue
            aStarClosedStamp[cur] = run
            expansions++

            if (cur == goal) {
                bestReached = goal
                break
            }

            val cx = cur % size
            val cy = cur / size
            val md = abs(gx - cx) + abs(gy - cy)
            if (md < bestDist) {
                bestDist = md
                bestReached = cur
            }

            val cg = aStarG[cur]
            for (dir in 0 until 8) {
                val nx = cx + DX8[dir]
                val ny = cy + DY8[dir]
                if (!inBounds(nx, ny)) continue
                val nIdx = ny * size + nx
                if (aStarClosedStamp[nIdx] == run) continue
                if (terrain[nIdx] == TerrainType.WATER) continue
                val w = wall[nIdx]
                if (w == WallType.WALL_BRICK || w == WallType.WALL_WOOD || w == WallType.TREE || w == WallType.CAR_WRECK || w == WallType.FENCE) continue
                if (w == WallType.DOOR_BARRICADED && nIdx != goal) continue
                if (w == WallType.WINDOW_INTACT && nIdx != goal) continue

                val baseStep = if (dir < 4) 1f else 1.414f
                val roadBonus = if (terrain[nIdx] == TerrainType.ROAD || terrain[nIdx] == TerrainType.BRIDGE) 0.82f else 1f
                val doorCost = if (w == WallType.DOOR_CLOSED) 0.4f else 0f
                val dangerCost = if (avoidDanger) dangerField[nIdx] * 0.22f else 0f
                val ng = cg + baseStep * roadBonus + doorCost + dangerCost

                if (aStarStamp[nIdx] != run || ng < aStarG[nIdx]) {
                    aStarStamp[nIdx] = run
                    aStarG[nIdx] = ng
                    aStarParent[nIdx] = cur
                    val h = (abs(gx - nx) + abs(gy - ny)).toFloat()
                    push(nIdx, ng + h * 1.15f)
                }
            }
        }

        var len = 0
        var trace = if (aStarClosedStamp[goal] == run) goal else bestReached
        while (trace != -1 && trace != start && len < tempPath.size) {
            tempPath[len++] = trace
            trace = aStarParent[trace]
        }
        for (i in len - 1 downTo 0) {
            outPath.add(tempPath[i])
        }
        return outPath.isNotEmpty()
    }

    private fun generate(rng: SimRng, seed: Long, lootRarity: Float) {
        // 1. Base terrain + river + forests
        val riverCenterBase = size * rng.nextFloatRange(0.36f, 0.64f)
        val riverHorizontal = rng.chance(0.5f)
        val riverAmp = size * 0.09f

        for (y in 0 until size) {
            for (x in 0 until size) {
                val i = idx(x, y)
                val n = SimNoise.fbm(x * 0.045f, y * 0.045f, seed, 3)
                terrain[i] = if (n > 0.59f) TerrainType.FOREST else TerrainType.GRASS

                val riverPos = if (riverHorizontal) {
                    riverCenterBase + sin(x * 0.055f) * riverAmp + (SimNoise.value2D(x * 0.08f, 1f, seed) - 0.5f) * 6f
                } else {
                    riverCenterBase + sin(y * 0.055f) * riverAmp + (SimNoise.value2D(1f, y * 0.08f, seed) - 0.5f) * 6f
                }
                val coord = if (riverHorizontal) y.toFloat() else x.toFloat()
                if (abs(coord - riverPos) < 2.6f) {
                    terrain[i] = TerrainType.WATER
                }
            }
        }

        // 2. Urban Road Grid (main 3-tile avenues + cross streets) & bridges over river
        val roadCols = if (size >= 150) {
            listOf((size * 0.16f).toInt(), (size * 0.34f).toInt(), (size * 0.50f).toInt(), (size * 0.66f).toInt(), (size * 0.84f).toInt())
        } else {
            listOf((size * 0.19f).toInt(), (size * 0.40f).toInt(), (size * 0.60f).toInt(), (size * 0.81f).toInt())
        }
        val roadRows = roadCols

        for (rx in roadCols) {
            for (y in 0 until size) {
                for (dx in -1..1) {
                    val x = rx + dx
                    if (inBounds(x, y)) {
                        val i = idx(x, y)
                        terrain[i] = if (terrain[i] == TerrainType.WATER) TerrainType.BRIDGE else TerrainType.ROAD
                    }
                }
            }
        }
        for (ry in roadRows) {
            for (x in 0 until size) {
                for (dy in -1..1) {
                    val y = ry + dy
                    if (inBounds(x, y)) {
                        val i = idx(x, y)
                        terrain[i] = if (terrain[i] == TerrainType.WATER) TerrainType.BRIDGE else TerrainType.ROAD
                    }
                }
            }
        }

        // 3. Trees on forest tiles & car wrecks on roads
        for (y in 2 until size - 2) {
            for (x in 2 until size - 2) {
                val i = idx(x, y)
                if (terrain[i] == TerrainType.FOREST && rng.chance(0.24f)) {
                    wall[i] = WallType.TREE
                    wallHp[i] = 60f
                } else if (terrain[i] == TerrainType.ROAD && rng.chance(0.015f)) {
                    if (terrain[i] == TerrainType.ROAD && wall[i] == WallType.NONE) {
                        wall[i] = WallType.CAR_WRECK
                        wallHp[i] = 140f
                        if (rng.chance(0.55f)) {
                            val c = LootContainer(
                                id = lootContainers.size + 1,
                                x = x,
                                y = y,
                                buildingId = -1,
                                scrap = rng.nextRange(1, 3),
                                fuel = if (rng.chance(0.5f)) rng.nextRange(1, 2) else 0,
                                food = if (rng.chance(0.2f)) 1 else 0
                            )
                            lootContainers.add(c)
                            lootByTile[i] = c
                        }
                    }
                }
            }
        }

        // 4. Generate dense neighborhoods of real houses & city buildings aligned to road blocks
        val colBounds = ArrayList<Int>().apply { add(3); for (c in roadCols) { add(c - 2); add(c + 2) }; add(size - 4) }
        val rowBounds = ArrayList<Int>().apply { add(3); for (r in roadRows) { add(r - 2); add(r + 2) }; add(size - 4) }

        for (ri in 0 until rowBounds.size step 2) {
            val by1 = rowBounds[ri]
            val by2 = rowBounds[ri + 1]
            if (by2 - by1 < 6) continue
            for (ci in 0 until colBounds.size step 2) {
                val bx1 = colBounds[ci]
                val bx2 = colBounds[ci + 1]
                if (bx2 - bx1 < 6) continue

                var curY = by1
                while (curY + 5 <= by2) {
                    var curX = bx1
                    var rowMaxH = 6
                    while (curX + 5 <= bx2) {
                        val distCenter = sqrt(
                            ((curX - size * 0.5f) * (curX - size * 0.5f) + (curY - size * 0.5f) * (curY - size * 0.5f))
                        ) / (size * 0.5f)

                        val bType = pickBuildingType(distCenter, rng)
                        val w = when (bType) {
                            BuildingType.SUBURB_HOUSE, BuildingType.PHARMACY, BuildingType.GAS_STATION -> rng.nextRange(5, 6)
                            else -> rng.nextRange(6, 7)
                        }
                        val h = (w + rng.nextRange(-1, 1)).coerceIn(5, 7)
                        val x2 = curX + w
                        val y2 = curY + h
                        if (x2 <= bx2 && y2 <= by2 && canPlaceBuilding(curX, curY, x2, y2)) {
                            placeBuilding(buildings.size + 1, bType, curX, curY, x2, y2, rng, lootRarity)
                            curX = x2 + 3 // Leave 2-tile yard/alley between neighboring houses
                            if (h + 2 > rowMaxH) rowMaxH = h + 2
                        } else {
                            curX += 3
                        }
                    }
                    curY += rowMaxH + 1
                }
            }
        }
    }

    private fun canPlaceBuilding(x1: Int, y1: Int, x2: Int, y2: Int): Boolean {
        for (y in y1 - 1..y2 + 1) {
            for (x in x1 - 1..x2 + 1) {
                if (!inBounds(x, y)) return false
                val t = terrain[idx(x, y)]
                if (t == TerrainType.WATER || t == TerrainType.BRIDGE || t == TerrainType.ROAD) return false
                if (buildingId[idx(x, y)] != -1) return false
            }
        }
        return true
    }

    private fun pickBuildingType(distNorm: Float, rng: SimRng): BuildingType {
        val roll = rng.nextFloat()
        return if (distNorm < 0.45f) {
            when {
                roll < 0.28f -> BuildingType.APARTMENT
                roll < 0.46f -> BuildingType.SUPERMARKET
                roll < 0.62f -> BuildingType.PHARMACY
                roll < 0.74f -> BuildingType.HOSPITAL
                roll < 0.87f -> BuildingType.POLICE
                else -> BuildingType.GAS_STATION
            }
        } else {
            when {
                roll < 0.62f -> BuildingType.SUBURB_HOUSE
                roll < 0.76f -> BuildingType.WAREHOUSE
                roll < 0.86f -> BuildingType.GAS_STATION
                roll < 0.94f -> BuildingType.SUPERMARKET
                else -> BuildingType.PHARMACY
            }
        }
    }

    private fun placeBuilding(
        id: Int,
        type: BuildingType,
        x1: Int,
        y1: Int,
        x2: Int,
        y2: Int,
        rng: SimRng,
        lootRarity: Float
    ) {
        val stories = when (type) {
            BuildingType.APARTMENT, BuildingType.HOSPITAL, BuildingType.POLICE -> 2
            else -> 1
        }
        val roofStyle = when (type) {
            BuildingType.SUBURB_HOUSE -> if (rng.chance(0.7f)) 0 else 1
            BuildingType.PHARMACY -> 1
            BuildingType.APARTMENT, BuildingType.HOSPITAL, BuildingType.POLICE -> 2
            BuildingType.SUPERMARKET, BuildingType.WAREHOUSE, BuildingType.GAS_STATION -> 3
        }
        val roofColorIdx = when (type) {
            BuildingType.SUBURB_HOUSE -> rng.nextInt(4) // terracotta, slate, pine, cedar
            BuildingType.HOSPITAL, BuildingType.PHARMACY -> 1
            BuildingType.POLICE -> 2
            else -> rng.nextInt(3)
        }
        val facadeColorIdx = rng.nextInt(4)
        val info = BuildingInfo(id, type, x1, y1, x2, y2, stories, roofStyle, roofColorIdx, facadeColorIdx)
        val wallMat = if (type == BuildingType.SUBURB_HOUSE) WallType.WALL_WOOD else WallType.WALL_BRICK
        val floorMat = when (type) {
            BuildingType.SUBURB_HOUSE, BuildingType.APARTMENT -> TerrainType.WOOD_FLOOR
            BuildingType.WAREHOUSE, BuildingType.GAS_STATION -> TerrainType.CONCRETE
            else -> TerrainType.TILE_FLOOR
        }

        // Lay clean paved sidewalk / porch apron around building perimeter
        for (y in max(1, y1 - 1)..min(size - 2, y2 + 1)) {
            for (x in max(1, x1 - 1)..min(size - 2, x2 + 1)) {
                val i = idx(x, y)
                if (x in x1..x2 && y in y1..y2) continue
                if (terrain[i] == TerrainType.GRASS || terrain[i] == TerrainType.FOREST) {
                    if (wall[i] == WallType.TREE) wall[i] = WallType.NONE
                    if (type != BuildingType.SUBURB_HOUSE || y == y2 + 1 || x == x2 + 1) {
                        terrain[i] = TerrainType.CONCRETE
                    }
                }
            }
        }

        for (y in y1..y2) {
            for (x in x1..x2) {
                val i = idx(x, y)
                terrain[i] = floorMat
                buildingId[i] = id
                val isBorder = (x == x1 || x == x2 || y == y1 || y == y2)
                if (isBorder) {
                    wall[i] = wallMat
                    wallHp[i] = if (wallMat == WallType.WALL_BRICK) 240f else 140f
                } else {
                    wall[i] = WallType.NONE
                    wallHp[i] = 0f
                }
            }
        }

        // Front & back doors (placed on front-facing walls for clear street entry)
        val midX = (x1 + x2) / 2
        val midY = (y1 + y2) / 2
        val d1 = idx(midX, y2)
        val d2 = idx(x2, midY)
        wall[d1] = WallType.DOOR_CLOSED; wallHp[d1] = 85f; info.doorTiles.add(d1)
        wall[d2] = WallType.DOOR_CLOSED; wallHp[d2] = 85f; info.doorTiles.add(d2)

        // Windows along front & side facades
        val winOffsets = listOf(x1 + 1, x2 - 1)
        for (wx in winOffsets) {
            if (wx != midX && wx in (x1 + 1) until x2) {
                val wi = idx(wx, y2)
                wall[wi] = WallType.WINDOW_INTACT; wallHp[wi] = 40f; info.windowTiles.add(wi)
            }
        }
        val wLeft = idx(x1, midY)
        val wTop = idx(midX, y1)
        wall[wLeft] = WallType.WINDOW_INTACT; wallHp[wLeft] = 40f; info.windowTiles.add(wLeft)
        wall[wTop] = WallType.WINDOW_INTACT; wallHp[wTop] = 40f; info.windowTiles.add(wTop)

        // Populate loot containers inside rooms
        val containerCount = when (type) {
            BuildingType.SUPERMARKET, BuildingType.HOSPITAL, BuildingType.WAREHOUSE -> 4
            BuildingType.POLICE, BuildingType.APARTMENT -> 3
            else -> 2
        }
        repeat(containerCount) {
            val lx = rng.nextRange(x1 + 1, x2 - 1)
            val ly = rng.nextRange(y1 + 1, y2 - 1)
            val li = idx(lx, ly)
            if (wall[li] == WallType.NONE && !lootByTile.containsKey(li)) {
                val c = createBuildingLoot(lootContainers.size + 1, lx, ly, id, type, rng, lootRarity)
                lootContainers.add(c)
                lootByTile[li] = c
            }
        }
        buildings.add(info)
    }

    private fun createBuildingLoot(
        id: Int,
        x: Int,
        y: Int,
        bId: Int,
        type: BuildingType,
        rng: SimRng,
        rarity: Float
    ): LootContainer {
        fun scaled(base: Int): Int = max(0, (base * rarity + if (rng.chance(0.35f)) 1 else 0).toInt())
        return when (type) {
            BuildingType.SUPERMARKET -> LootContainer(
                id, x, y, bId,
                food = scaled(3), water = scaled(3),
                weapon = if (rng.chance(0.25f)) WeaponType.KNIFE else null
            )
            BuildingType.PHARMACY, BuildingType.HOSPITAL -> LootContainer(
                id, x, y, bId,
                bandages = scaled(3), antibiotics = scaled(2),
                water = scaled(1), hasFlashlight = rng.chance(0.2f)
            )
            BuildingType.POLICE -> LootContainer(
                id, x, y, bId,
                ammo = scaled(10),
                weapon = when {
                    rng.chance(0.35f) -> WeaponType.SHOTGUN
                    rng.chance(0.65f) -> WeaponType.PISTOL
                    else -> WeaponType.RIFLE
                },
                bandages = scaled(1),
                hasFlashlight = rng.chance(0.45f)
            )
            BuildingType.GAS_STATION, BuildingType.WAREHOUSE -> LootContainer(
                id, x, y, bId,
                wood = scaled(4), scrap = scaled(3), fuel = scaled(2),
                food = scaled(1),
                weapon = when {
                    rng.chance(0.30f) -> WeaponType.AXE
                    rng.chance(0.55f) -> WeaponType.BAT
                    else -> null
                },
                hasFlashlight = rng.chance(0.3f)
            )
            BuildingType.SUBURB_HOUSE, BuildingType.APARTMENT -> LootContainer(
                id, x, y, bId,
                food = scaled(2), water = scaled(2),
                bandages = if (rng.chance(0.45f)) 1 else 0,
                wood = scaled(2),
                weapon = if (rng.chance(0.35f)) WeaponType.BAT else if (rng.chance(0.25f)) WeaponType.KNIFE else null,
                ammo = if (rng.chance(0.18f)) rng.nextRange(4, 8) else 0,
                hasFlashlight = rng.chance(0.25f)
            )
        }
    }

    companion object {
        val DX8 = intArrayOf(1, -1, 0, 0, 1, 1, -1, -1)
        val DY8 = intArrayOf(0, 0, 1, -1, 1, -1, 1, -1)
    }
}
