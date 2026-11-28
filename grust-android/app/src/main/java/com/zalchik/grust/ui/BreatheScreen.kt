package com.zalchik.grust.ui

import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.zalchik.grust.data.Practice
import com.zalchik.grust.data.Practices
import com.zalchik.grust.ui.theme.Palette

private const val TICK_MS = 50

/**
 * The breathing screen: pick a rhythm, then follow the circle.
 *
 * The circle's size is driven by a single [Animatable] that each phase animates towards its target
 * scale, so the movement is continuous instead of stepping at phase boundaries. The timer that
 * advances the phases is a coroutine that sleeps in small ticks - it stops the moment the screen
 * leaves composition, and that is all the cleanup a breathing timer needs.
 */
@Composable
fun BreatheScreen() {
    var running by remember { mutableStateOf(false) }
    var practiceIndex by remember { mutableIntStateOf(0) }
    val practice = Practices.all[practiceIndex]

    val scale = remember { Animatable(1f) }
    var phaseIndex by remember { mutableIntStateOf(0) }
    var cycle by remember { mutableIntStateOf(0) }
    var phaseMillisLeft by remember { mutableIntStateOf(0) }
    var finished by remember { mutableStateOf(false) }

    fun reset() {
        phaseIndex = 0
        cycle = 0
        phaseMillisLeft = practice.phases.first().seconds * 1000
        finished = false
    }

    // The circle snaps to where the practice begins. Without this the first phase of "Квадрат"
    // animates towards the scale the circle is already at, so the first inhale is invisible.
    LaunchedEffect(practiceIndex, running) {
        if (running) scale.snapTo(practice.phases.first().from)
    }

    // A different practice starts from its own beginning.
    LaunchedEffect(practiceIndex) {
        running = false
        reset()
    }

    LaunchedEffect(running, practiceIndex) {
        if (!running) return@LaunchedEffect
        if (finished) return@LaunchedEffect
        while (true) {
            val phase = practice.phases[phaseIndex]
            scale.snapTo(phase.from)
            val target = phase.to
            scale.animateTo(target, tween(phase.seconds * 1000, easing = LinearEasing))
            // The phase ran out: step on, or finish after the last cycle.
            if (phaseIndex == practice.phases.lastIndex) {
                if (cycle + 1 >= practice.cycles) {
                    running = false
                    finished = true
                    scale.animateTo(1f, tween(600))
                    break
                }
                cycle++
            }
            phaseIndex = (phaseIndex + 1) % practice.phases.size
        }
    }

    // The countdown is only a display: it is recomputed from the phase, not from the animation, so
    // it stays honest even if a frame is dropped.
    LaunchedEffect(running, practiceIndex, phaseIndex) {
        if (!running) return@LaunchedEffect
        val phase = practice.phases[phaseIndex]
        phaseMillisLeft = phase.seconds * 1000
        var left = phase.seconds * 1000
        while (left > 0) {
            kotlinx.coroutines.delay(TICK_MS.toLong())
            left -= TICK_MS
            phaseMillisLeft = left
        }
    }

    Column(
        modifier = Modifier.fillMaxWidth().verticalScroll(rememberScrollState()).padding(20.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Spacer(Modifier.height(24.dp))
        Text(
            text = "Дыхание",
            fontSize = 28.sp,
            fontFamily = FontFamily.Serif,
            color = Palette.Lavender,
        )
        Text(
            text = "медленнее, чем хочется",
            fontSize = 12.sp,
            color = Palette.Mist,
        )
        Spacer(Modifier.height(20.dp))

        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            Practices.all.forEachIndexed { index, item ->
                PracticeTab(
                    practice = item,
                    selected = index == practiceIndex,
                    modifier = Modifier.weight(1f),
                    onClick = { practiceIndex = index },
                )
            }
        }

        Spacer(Modifier.height(24.dp))

        Box(
            modifier = Modifier.size(240.dp),
            contentAlignment = Alignment.Center,
        ) {
            Canvas(Modifier.size(240.dp)) {
                val radius = size.minDimension * 0.46f
                drawCircle(
                    color = Palette.Wall,
                    radius = radius,
                    style = Stroke(width = 1.5f),
                )
                drawCircle(
                    color = Palette.Lavender.copy(alpha = 0.10f),
                    radius = radius * scale.value,
                )
                drawCircle(
                    color = Palette.Rain.copy(alpha = 0.55f),
                    radius = radius * scale.value,
                    style = Stroke(width = 3f),
                )
            }
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                val label = when {
                    finished -> "хватит"
                    !running -> "готов?"
                    else -> practice.phases[phaseIndex].name
                }
                Text(
                    text = label,
                    fontSize = 22.sp,
                    fontFamily = FontFamily.Serif,
                    color = Palette.onNight,
                )
                if (running && !finished) {
                    Text(
                        text = "${(phaseMillisLeft + 999) / 1000}",
                        fontSize = 40.sp,
                        color = Palette.Lamp,
                    )
                    Text(
                        text = "круг ${cycle + 1} из ${practice.cycles}",
                        fontSize = 11.sp,
                        color = Palette.Mist,
                    )
                }
            }
        }

        Spacer(Modifier.height(18.dp))
        Text(
            text = if (finished) "Круги закончились. Можно остаться так." else practice.subtitle,
            fontSize = 13.sp,
            color = Palette.Mist,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(16.dp))

        Button(
            onClick = {
                if (finished) reset()
                running = !running
            },
            modifier = Modifier.fillMaxWidth().height(52.dp),
            shape = RoundedCornerShape(16.dp),
            colors = ButtonDefaults.buttonColors(
                containerColor = Palette.Lavender,
                contentColor = Palette.Night,
            ),
        ) {
            Text(if (running) "Пауза" else if (finished) "Ещё раз" else "Начать", fontSize = 15.sp)
        }
        if (running) {
            TextButton(onClick = {
                running = false
                reset()
            }) {
                Text("сначала", fontSize = 12.sp, color = Palette.Mist)
            }
        }
        Spacer(Modifier.height(20.dp))
        Text(
            text = "Если становится непривычно или кружится голова - просто дыши как обычно. Упражнение не обязано быть сделано.",
            fontSize = 11.sp,
            lineHeight = 17.sp,
            color = Palette.Mist.copy(alpha = 0.8f),
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(24.dp))
    }
}

@Composable
private fun PracticeTab(
    practice: Practice,
    selected: Boolean,
    modifier: Modifier = Modifier,
    onClick: () -> Unit,
) {
    Column(
        modifier = modifier
            .background(
                if (selected) Palette.Deep else Palette.Deep.copy(alpha = 0.45f),
                RoundedCornerShape(14.dp),
            )
            .border(
                if (selected) 1.5.dp else 1.dp,
                if (selected) Palette.Lavender else Palette.Wall,
                RoundedCornerShape(14.dp),
            )
            .clickable(onClick = onClick)
            .padding(horizontal = 10.dp, vertical = 10.dp),
    ) {
        Text(
            text = practice.title,
            fontSize = 13.sp,
            color = if (selected) Palette.onNight else Palette.Mist,
        )
        Spacer(Modifier.height(3.dp))
        Text(
            text = practice.phases.joinToString("-") { it.seconds.toString() },
            fontSize = 10.sp,
            color = Palette.Mist.copy(alpha = 0.8f),
        )
    }
}
