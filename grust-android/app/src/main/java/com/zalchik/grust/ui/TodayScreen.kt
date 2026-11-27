package com.zalchik.grust.ui

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
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.zalchik.grust.data.Mood
import com.zalchik.grust.data.Russian
import com.zalchik.grust.data.MoodEntry
import com.zalchik.grust.data.Stats
import com.zalchik.grust.data.Words
import com.zalchik.grust.ui.theme.Palette
import java.time.LocalDate

/**
 * The main screen: the phrase of the day, one honest question about the mood, and a place to write.
 * Nothing here demands an answer - the button only wakes up when a step is picked.
 */
@Composable
fun TodayScreen(
    entries: List<MoodEntry>,
    today: LocalDate,
    phrase: Int,
    onSave: (MoodEntry) -> Unit,
    onAnotherPhrase: () -> Unit,
    onOpenDiary: () -> Unit,
) {
    val saved = entries.firstOrNull { it.day == today }
    var level by rememberSaveable(today) { mutableIntStateOf(saved?.level ?: 0) }
    var note by rememberSaveable(today) { mutableStateOf(saved?.note ?: "") }
    var confirmation by rememberSaveable(today) { mutableStateOf(false) }

    val streak = Stats.streak(entries, today)

    LazyColumn(
        modifier = Modifier.fillMaxWidth(),
        contentPadding = androidx.compose.foundation.layout.PaddingValues(20.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp),
    ) {
        item {
            Spacer(Modifier.height(24.dp))
            Text(
                text = Russian.longDate(today),
                fontSize = 13.sp,
                color = Palette.Mist,
                letterSpacing = 0.6.sp,
            )
            Spacer(Modifier.height(6.dp))
            Text(
                text = "Как оно сегодня?",
                fontSize = 28.sp,
                fontFamily = FontFamily.Serif,
                color = Palette.Lavender,
            )
            if (streak > 0) {
                Spacer(Modifier.height(6.dp))
                Text(
                    text = "$streak ${Russian.pluralDays(streak)} подряд ты остаёшься на связи с собой",
                    fontSize = 12.sp,
                    color = Palette.Mist,
                )
            }
        }

        item {
            PhraseCard(
                text = Words.supportOfDay(today.plusDays(phrase.toLong())),
                onAnother = onAnotherPhrase,
            )
        }

        item {
            Text(
                text = "Отметь, что ближе. Это не оценка, просто ориентир.",
                fontSize = 13.sp,
                color = Palette.Mist,
            )
            Spacer(Modifier.height(10.dp))
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                Mood.entries.forEach { mood ->
                    MoodChip(
                        mood = mood,
                        selected = level == mood.level,
                        modifier = Modifier.weight(1f),
                        onClick = {
                            level = mood.level
                            confirmation = false
                        },
                    )
                }
            }
            Spacer(Modifier.height(8.dp))
            Text(
                text = if (level == 0) " " else Mood.of(level).hint,
                fontSize = 12.sp,
                color = Palette.Lavender.copy(alpha = 0.75f),
                textAlign = TextAlign.Center,
                modifier = Modifier.fillMaxWidth(),
            )
        }

        item {
            Text(
                text = "Что хочется записать",
                fontSize = 13.sp,
                color = Palette.Mist,
            )
            Spacer(Modifier.height(6.dp))
            Text(
                text = Words.promptOfDay(today),
                fontSize = 14.sp,
                color = Palette.Lavender,
                fontFamily = FontFamily.Serif,
            )
            Spacer(Modifier.height(10.dp))
            NoteField(value = note, onValueChange = { note = it })
        }

        item {
            Button(
                onClick = {
                    if (level > 0) {
                        onSave(MoodEntry(today, level, note.trim()))
                        confirmation = true
                    }
                },
                enabled = level > 0,
                modifier = Modifier.fillMaxWidth().height(52.dp),
                shape = RoundedCornerShape(16.dp),
                colors = ButtonDefaults.buttonColors(
                    containerColor = Palette.Lavender,
                    contentColor = Palette.Night,
                    disabledContainerColor = Palette.Wall,
                    disabledContentColor = Palette.Mist,
                ),
            ) {
                Text(
                    if (level == 0) "Сначала выбери, как оно" else "Сохранить день",
                    fontSize = 15.sp,
                )
            }
            if (confirmation && saved != null) {
                Spacer(Modifier.height(10.dp))
                Text(
                    text = "Записано. День не пропал.",
                    fontSize = 13.sp,
                    color = Palette.Lamp,
                    modifier = Modifier.fillMaxWidth(),
                    textAlign = TextAlign.Center,
                )
            }
        }

        item {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.Center,
            ) {
                TextButton(onClick = onOpenDiary) {
                    Text(
                        text = "Посмотреть весь дневник",
                        fontSize = 13.sp,
                        color = Palette.Rain,
                        textDecoration = TextDecoration.Underline,
                    )
                }
            }
            Spacer(Modifier.height(16.dp))
        }
    }
}

@Composable
private fun PhraseCard(text: String, onAnother: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(Palette.Deep.copy(alpha = 0.85f), RoundedCornerShape(20.dp))
            .border(1.dp, Palette.Wall, RoundedCornerShape(20.dp))
            .padding(18.dp),
    ) {
        Text(
            text = text,
            fontSize = 15.sp,
            lineHeight = 22.sp,
            fontFamily = FontFamily.Serif,
            color = Palette.onNight,
        )
        Spacer(Modifier.height(6.dp))
        TextButton(
            onClick = onAnother,
            contentPadding = androidx.compose.foundation.layout.PaddingValues(0.dp),
        ) {
            Text("ещё одну", fontSize = 12.sp, color = Palette.Mist)
        }
    }
}

@Composable
private fun MoodChip(
    mood: Mood,
    selected: Boolean,
    modifier: Modifier = Modifier,
    onClick: () -> Unit,
) {
    val background = if (selected) mood.tint.copy(alpha = 0.28f) else Palette.Deep.copy(alpha = 0.7f)
    val edge = if (selected) mood.tint else Palette.Wall
    Column(
        modifier = modifier
            .height(84.dp)
            .background(background, RoundedCornerShape(16.dp))
            .border(if (selected) 1.5.dp else 1.dp, edge, RoundedCornerShape(16.dp))
            .clickable(onClick = onClick)
            .padding(vertical = 10.dp, horizontal = 4.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        // The emoji is drawn over a slightly darker disc: on a near-black background the stock
        // yellow face loses its outline, and a smiley with no outline is a coloured smear.
        Box(
            modifier = Modifier
                .size(30.dp)
                .background(Palette.Night.copy(alpha = 0.55f), RoundedCornerShape(50)),
            contentAlignment = Alignment.Center,
        ) {
            Text(mood.emoji, fontSize = 18.sp)
        }
        Spacer(Modifier.height(6.dp))
        Text(
            text = mood.title,
            fontSize = 11.sp,
            maxLines = 1,
            color = if (selected) Palette.onNight else Palette.Mist,
            textAlign = TextAlign.Center,
        )
    }
}

@Composable
private fun NoteField(value: String, onValueChange: (String) -> Unit) {
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .background(Palette.Deep.copy(alpha = 0.7f), RoundedCornerShape(16.dp))
            .border(1.dp, Palette.Wall, RoundedCornerShape(16.dp))
            .padding(14.dp),
    ) {
        if (value.isEmpty()) {
            Text(
                text = "Можно ничего не писать. Можно писать криво.",
                fontSize = 14.sp,
                color = Palette.Mist.copy(alpha = 0.7f),
            )
        }
        BasicTextField(
            value = value,
            onValueChange = onValueChange,
            textStyle = TextStyle(fontSize = 14.sp, color = Palette.onNight, lineHeight = 20.sp),
            cursorBrush = SolidColor(Palette.Lamp),
            modifier = Modifier.fillMaxWidth().height(88.dp),
            maxLines = 6,
            decorationBox = { inner -> Box { inner() } },
        )
    }
}

/** Kept for the empty-state illustration shared with the diary screen. */
@Composable
fun EmptyDots(count: Int = 3) {
    Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
        repeat(count) {
            Box(
                Modifier
                    .size(6.dp)
                    .background(Color(0xFF6E7BA6), RoundedCornerShape(50)),
            )
        }
    }
}
