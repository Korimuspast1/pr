package com.zalchik.grust.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.zalchik.grust.data.MoodEntry
import com.zalchik.grust.data.Russian
import com.zalchik.grust.ui.theme.Palette
import java.time.LocalDate

/**
 * The whole diary, newest first. Deleting is deliberate: it asks once and then leaves the day
 * alone, because an accidental delete would cost the user the one thing the app keeps.
 */
@Composable
fun DiaryScreen(
    entries: List<MoodEntry>,
    today: LocalDate,
    onErase: (LocalDate) -> Unit,
) {
    var pendingDelete by remember { mutableStateOf<MoodEntry?>(null) }
    val ordered = entries.sortedByDescending { it.day }

    if (ordered.isEmpty()) {
        EmptyDiary()
        return
    }

    LazyColumn(
        modifier = Modifier.fillMaxWidth(),
        contentPadding = PaddingValues(20.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        item {
            Spacer(Modifier.height(24.dp))
            Text(
                text = "Дневник",
                fontSize = 28.sp,
                fontFamily = FontFamily.Serif,
                color = Palette.Lavender,
            )
            Text(
                text = "записей: ${ordered.size} · с текстом: ${ordered.count { it.note.isNotBlank() }}",
                fontSize = 12.sp,
                color = Palette.Mist,
            )
            Spacer(Modifier.height(8.dp))
        }
        items(ordered, key = { it.day.toEpochDay() }) { entry ->
            DiaryRow(
                entry = entry,
                isToday = entry.day == today,
                onDelete = { pendingDelete = entry },
            )
        }
        item { Spacer(Modifier.height(20.dp)) }
    }

    pendingDelete?.let { entry ->
        AlertDialog(
            onDismissRequest = { pendingDelete = null },
            containerColor = Palette.Deep,
            title = { Text("Убрать ${Russian.shortDate(entry.day)}?", color = Palette.onNight) },
            text = {
                Text(
                    "Запись и текст исчезнут. Это нельзя отменить.",
                    color = Palette.Mist,
                    fontSize = 13.sp,
                )
            },
            confirmButton = {
                TextButton(onClick = {
                    onErase(entry.day)
                    pendingDelete = null
                }) {
                    Text("Убрать", color = Palette.Error)
                }
            },
            dismissButton = {
                TextButton(onClick = { pendingDelete = null }) {
                    Text("Оставить", color = Palette.Lavender)
                }
            },
        )
    }
}

@Composable
private fun DiaryRow(entry: MoodEntry, isToday: Boolean, onDelete: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(Palette.Deep.copy(alpha = 0.8f), RoundedCornerShape(16.dp))
            .border(1.dp, Palette.Wall, RoundedCornerShape(16.dp))
            .padding(start = 14.dp, end = 4.dp, top = 12.dp, bottom = 12.dp),
        verticalAlignment = Alignment.Top,
    ) {
        Column(
            modifier = Modifier
                .size(38.dp)
                .background(entry.mood.tint.copy(alpha = 0.22f), RoundedCornerShape(12.dp)),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
        ) {
            Text(entry.mood.emoji, fontSize = 18.sp)
        }
        Column(modifier = Modifier.weight(1f).padding(start = 12.dp, end = 8.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    text = Russian.shortDate(entry.day),
                    fontSize = 14.sp,
                    color = Palette.onNight,
                )
                Spacer(Modifier.size(8.dp))
                Text(
                    text = entry.mood.title,
                    fontSize = 11.sp,
                    color = entry.mood.tint,
                )
                if (isToday) {
                    Spacer(Modifier.size(8.dp))
                    Text("сегодня", fontSize = 10.sp, color = Palette.Mist)
                }
            }
            if (entry.note.isNotBlank()) {
                Spacer(Modifier.height(5.dp))
                Text(
                    text = entry.note,
                    fontSize = 13.sp,
                    lineHeight = 19.sp,
                    color = Palette.Lavender.copy(alpha = 0.85f),
                    fontFamily = FontFamily.Serif,
                )
            }
        }
        TextButton(onClick = onDelete) {
            Text("×", fontSize = 20.sp, color = Palette.Mist)
        }
    }
}

@Composable
private fun EmptyDiary() {
    Column(
        modifier = Modifier.fillMaxWidth().padding(28.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Spacer(Modifier.height(80.dp))
        EmptyDots(4)
        Spacer(Modifier.height(18.dp))
        Text(
            text = "Пока пусто",
            fontSize = 20.sp,
            fontFamily = FontFamily.Serif,
            color = Palette.Lavender,
        )
        Spacer(Modifier.height(8.dp))
        Text(
            text = "Первая запись появится здесь, когда ты отметишь сегодняшний день на вкладке «Сегодня». Ничего страшного, если это будет не сейчас.",
            fontSize = 13.sp,
            lineHeight = 20.sp,
            color = Palette.Mist,
            textAlign = TextAlign.Center,
        )
    }
}
