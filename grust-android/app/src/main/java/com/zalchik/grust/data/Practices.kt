package com.zalchik.grust.data

/** One step of a breathing cycle: how long it lasts and where the circle travels. */
data class BreathPhase(
    val name: String,
    val seconds: Int,
    val from: Float,
    val to: Float,
)

/**
 * A breathing exercise. All three are slow on purpose and none of them asks for effort: the point
 * is to give the body something steady to hold on to while the head is loud.
 */
data class Practice(
    val title: String,
    val subtitle: String,
    val phases: List<BreathPhase>,
    val cycles: Int,
)

object Practices {

    private fun hold(seconds: Int, scale: Float) = BreathPhase("задержка", seconds, scale, scale)

    val all = listOf(
        Practice(
            title = "Квадрат",
            subtitle = "4-4-4-4 · 4 круга · когда мысли крутятся",
            phases = listOf(
                BreathPhase("вдох", 4, 0.62f, 1f),
                hold(4, 1f),
                BreathPhase("выдох", 4, 1f, 0.62f),
                hold(4, 0.62f),
            ),
            cycles = 4,
        ),
        Practice(
            title = "4-7-8",
            subtitle = "4-7-8 · 4 круга · чтобы уснуть",
            phases = listOf(
                BreathPhase("вдох", 4, 0.62f, 1f),
                hold(7, 1f),
                BreathPhase("выдох", 8, 1f, 0.62f),
            ),
            cycles = 4,
        ),
        Practice(
            title = "Вздох",
            subtitle = "3-1-8 · 5 кругов · когда накрывает",
            phases = listOf(
                BreathPhase("вдох", 3, 0.62f, 0.92f),
                BreathPhase("добрать", 1, 0.92f, 1f),
                BreathPhase("выдох", 8, 1f, 0.58f),
            ),
            cycles = 5,
        ),
    )
}
