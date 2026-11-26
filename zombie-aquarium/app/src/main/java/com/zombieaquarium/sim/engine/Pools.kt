package com.zombieaquarium.sim.engine

class ParticlePool(val capacity: Int = 512) {
    val active = BooleanArray(capacity)
    val x = FloatArray(capacity)
    val y = FloatArray(capacity)
    val vx = FloatArray(capacity)
    val vy = FloatArray(capacity)
    val life = FloatArray(capacity)
    val maxLife = FloatArray(capacity)
    val color = IntArray(capacity)
    val size = FloatArray(capacity)
    private var cursor = 0

    fun spawn(
        px: Float,
        py: Float,
        pvx: Float,
        pvy: Float,
        pLife: Float,
        pColor: Int,
        pSize: Float = 0.14f
    ) {
        val idx = cursor
        cursor = (cursor + 1) % capacity
        active[idx] = true
        x[idx] = px
        y[idx] = py
        vx[idx] = pvx
        vy[idx] = pvy
        life[idx] = pLife
        maxLife[idx] = pLife
        color[idx] = pColor
        size[idx] = pSize
    }

    fun update(dt: Float) {
        for (i in 0 until capacity) {
            if (!active[i]) continue
            life[i] -= dt
            if (life[i] <= 0f) {
                active[i] = false
            } else {
                x[i] += vx[i] * dt
                y[i] += vy[i] * dt
                vx[i] *= 0.92f
                vy[i] *= 0.92f
            }
        }
    }
}

class SoundRingPool(val capacity: Int = 64) {
    val active = BooleanArray(capacity)
    val x = FloatArray(capacity)
    val y = FloatArray(capacity)
    val radius = FloatArray(capacity)
    val maxRadius = FloatArray(capacity)
    val life = FloatArray(capacity)
    val maxLife = FloatArray(capacity)
    val color = IntArray(capacity)
    private var cursor = 0

    fun spawn(px: Float, py: Float, maxR: Float, pColor: Int, duration: Float = 0.85f) {
        val idx = cursor
        cursor = (cursor + 1) % capacity
        active[idx] = true
        x[idx] = px
        y[idx] = py
        radius[idx] = 0.4f
        maxRadius[idx] = maxR
        life[idx] = duration
        maxLife[idx] = duration
        color[idx] = pColor
    }

    fun update(dt: Float) {
        for (i in 0 until capacity) {
            if (!active[i]) continue
            life[i] -= dt
            if (life[i] <= 0f) {
                active[i] = false
            } else {
                val p = 1f - (life[i] / maxLife[i])
                radius[i] = 0.4f + (maxRadius[i] - 0.4f) * p
            }
        }
    }
}

class TracerPool(val capacity: Int = 64) {
    val active = BooleanArray(capacity)
    val x1 = FloatArray(capacity)
    val y1 = FloatArray(capacity)
    val x2 = FloatArray(capacity)
    val y2 = FloatArray(capacity)
    val life = FloatArray(capacity)
    val color = IntArray(capacity)
    private var cursor = 0

    fun spawn(sx: Float, sy: Float, ex: Float, ey: Float, pColor: Int = 0xFFFDE047.toInt()) {
        val idx = cursor
        cursor = (cursor + 1) % capacity
        active[idx] = true
        x1[idx] = sx
        y1[idx] = sy
        x2[idx] = ex
        y2[idx] = ey
        life[idx] = 0.18f
        color[idx] = pColor
    }

    fun update(dt: Float) {
        for (i in 0 until capacity) {
            if (!active[i]) continue
            life[i] -= dt
            if (life[i] <= 0f) active[i] = false
        }
    }
}
