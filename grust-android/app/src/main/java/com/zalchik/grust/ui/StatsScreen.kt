package com.zalchik.grust.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.zalchik.grust.data.Mood
import com.zalchik.grust.data.MoodEntry
import com.zalchik.grust.data.Stats
import com.zalchik.grust.ui.theme.Palette
import java.time.LocalDate

private const val WINDOW_DAYS = 14

/**
 * The statistics screen: fourteen days of dots, a streak, and an honest distribution.
 *
 * Everything is computed from the diary on each composition - the list is at most a few thousand
 * small objects, so the arithmetic is cheaper than keeping a cache in sync with deletes.
 */
@Composable
fun StatsScreen(entries: List<MoodEntry>, today: LocalDate) {
    val window = Stats.window(entries, today, WINDOW_DAYS)
    val average = Stats.average(window)
    val streak = Stats.streak(entries, today)
    val filled = window.count { it != null }
    val distribution = Stats.distribution(entries)
    val total = distribution.sumOf { it.second }

    Column(
        modifier = Modifier.fillMaxWidth().verticalScroll(rememberScrollState()).padding(20.dp),
    ) {
        Spacer(Modifier.height(24.dp))
        Text(
            text = "Статистика",
            fontSize = 28.sp,
            fontFamily = FontFamily.Serif,
            color = Palette.Lavender,
        )
        Text(
            text = "не для оценки, а для карты",
            fontSize = 12.sp,
            color = Palette.Mist,
        )
        Spacer(Modifier.height(18.dp))

        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            MetricCard(
                modifier = Modifier.weight(1f),
                value = if (average == null) "—" else Format.oneDecimal(average),
                label = "среднее за $WINDOW_DAYS ${Format.pluralDays(WINDOW_DAYS)}",
                accent = Stats.moodFor(average)?.tint ?: Palette.Mist,
            )
            MetricCard(
                modifier = Modifier.weight(1f),
                value = "$streak",
                label = "${Format.pluralDays(streak)} подряд",
                accent = Palette.Lamp,
            )
        }
        Spacer(Modifier.height(10.dp))
        Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
            MetricCard(
                modifier = Modifier.weight(1f),
                value = "$filled / $WINDOW_DAYS",
                label = "дней заполнено",
                accent = Palette.Rain,
            )
            MetricCard(
                modifier = Modifier.weight(1f),
                value = "${entries.size}",
                label = "всего записей",
                accent = Palette.Lavender,
            )
        }

        Spacer(Modifier.height(24.dp))
        Text("Последние $WINDOW_DAYS ${Format.pluralDays(WINDOW_DAYS)}", fontSize = 13.sp, color = Palette.Mist)
        Spacer(Modifier.height(10.dp))
        Strip(window, today)

        Spacer(Modifier.height(24.dp))
        Text("Как часто какое состояние", fontSize = 13.sp, color = Palette.Mist)
        Spacer(Modifier.height(10.dp))
        if (total == 0) {
            Text(
                "Здесь появятся полосы, когда в дневнике будет хотя бы одна запись.",
                fontSize = 12.sp,
                color = Palette.Mist.copy(alpha = 0.8f),
            )
        } else {
            distribution.reversed().forEach { (mood, count) ->
                BarRow(mood = mood, count = count, max = total)
            }
        }

        Spacer(Modifier.height(22.dp))
        val dominant = Stats.dominant(entries)
        Note(
            when {
                total == 0 -> "Пока нечего считать. Дневник начнётся с одного дня."
                dominant == Mood.LIGHT || dominant == Mood.OK ->
                    "Чаще всего было легче, чем тяжелее. Это не отменяет тяжёлых дней - но они видны на карте, а не только в памяти."
                dominant == Mood.FLAT ->
                    "Чаще всего было «никак». Ровные дни - тоже часть пути, в них ничего не сломано."
                else ->
                    "Чаще всего было тяжело. Это не приговор и не характер - это описание периода. Если тяжесть не отпускает неделями, стоит сказать о ней живому человеку: близкому или специалисту."
            },
        )

        Spacer(Modifier.height(14.dp))
        Note("Данные лежат только на этом телефоне, никуда не отправляются и исчезают вместе с приложением.")
        Spacer(Modifier.height(28.dp))
    }
}

@Composable
private fun MetricCard(
    modifier: Modifier = Modifier,
    value: String,
    label: String,
    accent: androidx.compose.ui.graphics.Color,
) {
    Column(
        modifier = modifier
            .background(Palette.Deep.copy(alpha = 0.8f), RoundedCornerShape(16.dp))
            .border(1.dp, Palette.Wall, RoundedCornerShape(16.dp))
            .padding(14.dp),
    ) {
        Text(value, fontSize = 24.sp, color = accent, fontFamily = FontFamily.Serif)
        Spacer(Modifier.height(4.dp))
        Text(label, fontSize = 11.sp, lineHeight = 15.sp, color = Palette.Mist)
    }
}

/** Fourteen columns: height is the mood, a hollow column is a day that was not written. */
@Composable
private fun Strip(window: List<MoodEntry?>, today: LocalDate) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .height(120.dp)
            .background(Palette.Deep.copy(alpha = 0.6f), RoundedCornerShape(16.dp))
            .padding(horizontal = 10.dp, vertical = 12.dp),
        verticalAlignment = Alignment.Bottom,
        horizontalArrangement = Arrangement.spacedBy(4.dp),
    ) {
        window.forEachIndexed { index, entry ->
            val day = today.minusDays((window.size - 1 - index).toLong())
            Column(
                modifier = Modifier.weight(1f),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height((8 + (entry?.level ?: 0) * 13).dp)
                        .background(
                            color = entry?.mood?.tint ?: Palette.Wall.copy(alpha = 0.55f),
                            shape = RoundedCornerShape(4.dp),
                        ),
                )
                Spacer(Modifier.height(5.dp))
                Text(
                    text = if (index == window.lastIndex) "сегодня" else Format.weekdayShort(day),
                    fontSize = 8.sp,
                    maxLines = 1,
                    color = Palette.Mist.copy(alpha = if (index == window.lastIndex) 1f else 0.7f),
                )
            }
        }
    }
}

@Composable
private fun BarRow(mood: Mood, count: Int, max: Int) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(vertical = 3.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(mood.emoji, fontSize = 14.sp)
        Spacer(Modifier.width(8.dp))
        Text(
            text = mood.title,
            fontSize = 11.sp,
            color = Palette.Mist,
            modifier = Modifier.width(58.dp),
        )
        Box(
            modifier = Modifier
                .weight(1f)
                .height(10.dp)
                .background(Palette.Wall.copy(alpha = 0.5f), RoundedCornerShape(50)),
        ) {
            val fraction = if (max == 0) 0f else count.toFloat() / max
            if (count > 0) {
                Box(
                    modifier = Modifier
                        .fillMaxWidth(fraction.coerceAtLeast(0.04f))
                        .height(10.dp)
                        .background(mood.tint, RoundedCornerShape(50)),
                )
            }
        }
        Spacer(Modifier.width(8.dp))
        Text("$count", fontSize = 11.sp, color = Palette.onNight, modifier = Modifier.width(22.dp))
    }
}

@Composable
private fun Note(text: String) {
    Text(
        text = text,
        fontSize = 12.sp,
        lineHeight = 18.sp,
        color = Palette.Lavender.copy(alpha = 0.8f),
        textAlign = TextAlign.Start,
        modifier = Modifier
            .fillMaxWidth()
            .background(Palette.Deep.copy(alpha = 0.5f), RoundedCornerShape(14.dp))
            .padding(14.dp),
    )
}
