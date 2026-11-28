package com.zalchik.grust.ui

import android.graphics.Color
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.SystemBarStyle
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import com.zalchik.grust.data.MoodEntry
import com.zalchik.grust.data.MoodStore
import com.zalchik.grust.ui.theme.GrustTheme
import java.time.LocalDate

/** The four places the app has. An enum rather than a navigation library: there are four. */
enum class Screen(val label: String) {
    TODAY("Сегодня"),
    DIARY("Дневник"),
    BREATHE("Дыхание"),
    STATS("Статистика"),
}

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        // Light icons on the dark sky, on both bars.
        enableEdgeToEdge(
            statusBarStyle = SystemBarStyle.dark(Color.TRANSPARENT),
            navigationBarStyle = SystemBarStyle.dark(Color.TRANSPARENT),
        )
        setContent {
            GrustTheme {
                val store = remember { MoodStore(applicationContext) }
                var entries by remember { mutableStateOf(store.load()) }
                var screen by rememberSaveable { mutableStateOf(Screen.TODAY) }
                // Bumped by "ещё", so the phrase on the start screen has something to change.
                var phrase by rememberSaveable { mutableIntStateOf(0) }

                App(
                    entries = entries,
                    today = LocalDate.now(),
                    screen = screen,
                    phrase = phrase,
                    onScreen = { screen = it },
                    onSaveEntry = { entry: MoodEntry ->
                        entries = (entries.filterNot { it.day == entry.day } + entry)
                            .sortedBy { it.day }
                        store.save(entries)
                    },
                    onErase = { day: LocalDate ->
                        entries = entries.filterNot { it.day == day }
                        store.save(entries)
                    },
                    onAnotherPhrase = { phrase++ },
                )
            }
        }
    }
}
