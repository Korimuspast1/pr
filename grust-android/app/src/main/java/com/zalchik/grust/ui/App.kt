package com.zalchik.grust.ui

import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.systemBarsPadding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.NavigationBarItemDefaults
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.zalchik.grust.data.MoodEntry
import com.zalchik.grust.ui.theme.Palette
import java.time.LocalDate

/**
 * The shell: the sky, the bottom bar, and the choice the app opens with.
 *
 * The opening screen asks one question with one button. "Побыть с ней" and "Отложить" lead to the
 * same place - the point is only that the first thing the app says is that the feeling is allowed.
 */
@Composable
fun App(
    entries: List<MoodEntry>,
    today: LocalDate,
    screen: Screen,
    phrase: Int,
    onScreen: (Screen) -> Unit,
    onSaveEntry: (MoodEntry) -> Unit,
    onErase: (LocalDate) -> Unit,
    onAnotherPhrase: () -> Unit,
) {
    var chosen by rememberSaveable { mutableStateOf(false) }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(Palette.sky),
    ) {
        if (!chosen) {
            EntryScreen(onContinue = { chosen = true })
        } else {
            Scaffold(
                containerColor = androidx.compose.ui.graphics.Color.Transparent,
                bottomBar = {
                    NavigationBar(containerColor = Palette.Deep.copy(alpha = 0.92f)) {
                        Screen.entries.forEach { item ->
                            NavigationBarItem(
                                selected = item == screen,
                                onClick = { onScreen(item) },
                                icon = { NavGlyph(item, selected = item == screen) },
                                label = {
                                    Text(item.label, fontSize = 11.sp, maxLines = 1)
                                },
                                colors = NavigationBarItemDefaults.colors(
                                    selectedIconColor = Palette.Lamp,
                                    selectedTextColor = Palette.Lamp,
                                    indicatorColor = Palette.Wall,
                                    unselectedIconColor = Palette.Mist,
                                    unselectedTextColor = Palette.Mist,
                                ),
                            )
                        }
                    }
                },
            ) { padding ->
                Box(Modifier.padding(padding).fillMaxSize()) {
                    when (screen) {
                        Screen.TODAY -> TodayScreen(
                            entries = entries,
                            today = today,
                            phrase = phrase,
                            onSave = onSaveEntry,
                            onAnotherPhrase = onAnotherPhrase,
                            onOpenDiary = { onScreen(Screen.DIARY) },
                        )
                        Screen.DIARY -> DiaryScreen(entries, today, onErase)
                        Screen.BREATHE -> BreatheScreen()
                        Screen.STATS -> StatsScreen(entries, today)
                    }
                }
            }
        }
    }
}

@Composable
private fun EntryScreen(onContinue: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .systemBarsPadding()
            .padding(horizontal = 28.dp, vertical = 24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Rain(modifier = Modifier.fillMaxWidth().height(120.dp))
        Spacer(Modifier.height(28.dp))
        Text(
            text = "грусть",
            fontSize = 46.sp,
            fontWeight = FontWeight.Light,
            fontFamily = FontFamily.Serif,
            color = Palette.Lavender,
        )
        Spacer(Modifier.height(10.dp))
        Text(
            text = "тихое место, где не надо быть в порядке",
            fontSize = 14.sp,
            color = Palette.Mist,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(44.dp))
        Text(
            text = "Сегодня внутри тяжело?",
            fontSize = 20.sp,
            color = Palette.onNight,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(8.dp))
        Text(
            text = "Это не вопрос, на который нужен правильный ответ.",
            fontSize = 13.sp,
            color = Palette.Mist,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(28.dp))
        Button(
            onClick = onContinue,
            modifier = Modifier.fillMaxWidth().height(54.dp),
            shape = RoundedCornerShape(18.dp),
            colors = ButtonDefaults.buttonColors(
                containerColor = Palette.Lavender,
                contentColor = Palette.Night,
            ),
        ) {
            Text("Побыть с ней", fontSize = 16.sp)
        }
        Spacer(Modifier.height(12.dp))
        OutlinedButton(
            onClick = onContinue,
            modifier = Modifier.fillMaxWidth().height(52.dp),
            shape = RoundedCornerShape(18.dp),
        ) {
            Text("Отложить на потом", fontSize = 15.sp, color = Palette.Mist)
        }
        Spacer(Modifier.height(20.dp))
        TextButton(onClick = onContinue) {
            Text(
                "оба варианта ведут в одно и то же место - и это нормально",
                fontSize = 11.sp,
                color = Palette.Mist.copy(alpha = 0.7f),
                textAlign = TextAlign.Center,
            )
        }
    }
}

/** A slow breathing circle of falling drops: the app's one piece of movement outside the practice. */
@Composable
fun Rain(modifier: Modifier = Modifier) {
    val transition = rememberInfiniteTransition(label = "rain")
    val drift by transition.animateFloat(
        initialValue = 0f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(tween(9000), RepeatMode.Restart),
        label = "drift",
    )
    Canvas(modifier) {
        val drops = 22
        repeat(drops) { index ->
            val x = size.width * ((index * 37 % 100) / 100f)
            val speed = 0.6f + (index % 5) * 0.12f
            val y = ((drift * speed + index * 0.077f) % 1f) * size.height
            val length = size.height * (0.06f + (index % 3) * 0.02f)
            drawLine(
                color = Palette.Rain.copy(alpha = 0.18f + (index % 4) * 0.06f),
                start = Offset(x, y),
                end = Offset(x - length * 0.12f, y + length),
                strokeWidth = 2f,
                cap = StrokeCap.Round,
            )
        }
        drawCircle(
            color = Palette.Lavender.copy(alpha = 0.05f),
            radius = size.minDimension * 0.30f,
            center = Offset(size.width * 0.5f, size.height * 0.55f),
        )
    }
}

/**
 * Navigation icons drawn by hand: four tiny glyphs are less code than an icon dependency, and they
 * keep the bar in one weight.
 */
@Composable
private fun NavGlyph(screen: Screen, selected: Boolean) {
    val color = if (selected) Palette.Lamp else Palette.Mist
    Canvas(Modifier.size(22.dp)) {
        val w = size.width
        val h = size.height
        val stroke = Stroke(width = w * 0.09f, cap = StrokeCap.Round)
        when (screen) {
            Screen.TODAY -> {
                drawCircle(
                    color = color,
                    radius = w * 0.24f,
                    center = Offset(w * 0.5f, h * 0.45f),
                    style = stroke,
                )
                drawLine(
                    color,
                    Offset(w * 0.5f, h * 0.72f),
                    Offset(w * 0.5f, h * 0.95f),
                    w * 0.09f,
                    StrokeCap.Round,
                )
            }
            Screen.DIARY -> {
                repeat(3) { line ->
                    val y = h * (0.25f + line * 0.25f)
                    drawLine(color, Offset(w * 0.18f, y), Offset(w * 0.82f, y), w * 0.09f, StrokeCap.Round)
                }
            }
            Screen.BREATHE -> {
                drawCircle(color, w * 0.20f, Offset(w * 0.5f, h * 0.5f), style = stroke)
                drawCircle(color.copy(alpha = 0.55f), w * 0.33f, Offset(w * 0.5f, h * 0.5f), style = Stroke(w * 0.06f))
            }
            Screen.STATS -> {
                val heights = listOf(0.40f, 0.70f, 0.55f, 0.85f)
                heights.forEachIndexed { index, part ->
                    val x = w * (0.20f + index * 0.20f)
                    drawLine(color, Offset(x, h), Offset(x, h * (1f - part)), w * 0.09f, StrokeCap.Round)
                }
            }
        }
    }
}
