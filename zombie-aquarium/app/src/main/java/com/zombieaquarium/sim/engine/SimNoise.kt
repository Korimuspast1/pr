package com.zombieaquarium.sim.engine

import kotlin.math.floor

class SimRng(seed: Long) {
    private var state: Long = if (seed == 0L) -7046029254386353131L else seed

    fun nextLong(): Long {
        state += -7046029254386353131L // 0x9E3779B97F4A7C15
        var z = state
        z = (z xor (z ushr 30)) * -4658895280553007687L
        z = (z xor (z ushr 27)) * -7723592293110705685L
        return z xor (z ushr 31)
    }

    fun nextFloat(): Float {
        return ((nextLong() ushr 40).toInt() and 0xFFFFFF) / 16777216f
    }

    fun nextInt(bound: Int): Int {
        if (bound <= 1) return 0
        val raw = (nextLong() ushr 1) % bound.toLong()
        return raw.toInt()
    }

    fun nextRange(min: Int, maxInclusive: Int): Int {
        if (maxInclusive <= min) return min
        return min + nextInt(maxInclusive - min + 1)
    }

    fun nextFloatRange(min: Float, max: Float): Float {
        return min + (max - min) * nextFloat()
    }

    fun chance(p: Float): Boolean = nextFloat() < p
}

object SimNoise {
    private fun hash2(x: Int, y: Int, seed: Long): Float {
        var h = seed xor (x.toLong() * 374761393L) xor (y.toLong() * 668265263L)
        h = (h xor (h ushr 13)) * 1274126177L
        h = h xor (h ushr 16)
        return (h and 0xFFFFL).toFloat() / 65535f
    }

    private fun smooth(t: Float): Float = t * t * (3f - 2f * t)

    fun value2D(x: Float, y: Float, seed: Long): Float {
        val xi = floor(x).toInt()
        val yi = floor(y).toInt()
        val xf = smooth(x - xi)
        val yf = smooth(y - yi)
        val v00 = hash2(xi, yi, seed)
        val v10 = hash2(xi + 1, yi, seed)
        val v01 = hash2(xi, yi + 1, seed)
        val v11 = hash2(xi + 1, yi + 1, seed)
        val a = v00 + (v10 - v00) * xf
        val b = v01 + (v11 - v01) * xf
        return a + (b - a) * yf
    }

    fun fbm(x: Float, y: Float, seed: Long, octaves: Int = 3): Float {
        var total = 0f
        var amp = 1f
        var freq = 1f
        var norm = 0f
        for (i in 0 until octaves) {
            total += value2D(x * freq, y * freq, seed + i * 1013L) * amp
            norm += amp
            amp *= 0.5f
            freq *= 2.02f
        }
        return if (norm > 0f) total / norm else 0f
    }
}
