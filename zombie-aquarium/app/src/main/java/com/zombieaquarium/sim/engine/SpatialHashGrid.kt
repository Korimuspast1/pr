package com.zombieaquarium.sim.engine

import kotlin.math.max
import kotlin.math.min

/**
 * Zero-allocation-per-query O(1) spatial hash grid backed by flat IntArray buckets.
 */
class SpatialHashGrid(
    private val mapSize: Int,
    private val cellSize: Int = 8,
    maxEntities: Int = 4096
) {
    private val cols: Int = (mapSize + cellSize - 1) / cellSize
    private val cellCount: Int = cols * cols
    private val head = IntArray(cellCount) { -1 }
    private val next = IntArray(maxEntities) { -1 }
    private val entityX = FloatArray(maxEntities)
    private val entityY = FloatArray(maxEntities)
    private val entityId = IntArray(maxEntities)
    private var count = 0

    fun clear() {
        head.fill(-1)
        count = 0
    }

    fun insert(id: Int, x: Float, y: Float) {
        if (count >= next.size) return
        val cx = min(cols - 1, max(0, (x / cellSize).toInt()))
        val cy = min(cols - 1, max(0, (y / cellSize).toInt()))
        val cell = cy * cols + cx
        val idx = count++
        entityId[idx] = id
        entityX[idx] = x
        entityY[idx] = y
        next[idx] = head[cell]
        head[cell] = idx
    }

    fun queryRadius(
        qx: Float,
        qy: Float,
        radius: Float,
        outIds: IntArray,
        maxOut: Int = outIds.size
    ): Int {
        val r2 = radius * radius
        val minCx = max(0, ((qx - radius) / cellSize).toInt())
        val maxCx = min(cols - 1, ((qx + radius) / cellSize).toInt())
        val minCy = max(0, ((qy - radius) / cellSize).toInt())
        val maxCy = min(cols - 1, ((qy + radius) / cellSize).toInt())

        var found = 0
        for (cy in minCy..maxCy) {
            val rowOff = cy * cols
            for (cx in minCx..maxCx) {
                var idx = head[rowOff + cx]
                while (idx != -1) {
                    val dx = entityX[idx] - qx
                    val dy = entityY[idx] - qy
                    if (dx * dx + dy * dy <= r2) {
                        outIds[found++] = entityId[idx]
                        if (found >= maxOut) return found
                    }
                    idx = next[idx]
                }
            }
        }
        return found
    }
}
