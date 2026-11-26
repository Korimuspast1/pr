package com.zombieaquarium.sim.audio

import android.content.Context
import android.media.AudioAttributes
import android.media.MediaPlayer
import android.media.SoundPool
import com.zombieaquarium.sim.R
import com.zombieaquarium.sim.engine.WeatherType
import kotlin.math.sqrt

enum class SoundEventType {
    PISTOL,
    SHOTGUN,
    RIFLE,
    MELEE,
    AXE_BUILD,
    ZOMBIE_BITE,
    ZOMBIE_DIE,
    ZOMBIE_GROAN,
    SCREAMER,
    DOOR_OPEN,
    DOOR_BREAK,
    GLASS_BREAK,
    HUMAN_DIE,
    LOOT
}

data class SoundEvent(
    val type: SoundEventType,
    val wx: Float,
    val wy: Float
)

class SoundEngine(context: Context) {
    private val appContext = context.applicationContext
    private val soundPool: SoundPool
    private val sfxMap = HashMap<SoundEventType, Int>()
    private val lastPlayedMs = HashMap<SoundEventType, Long>()

    private var rainPlayer: MediaPlayer? = null
    private var nightPlayer: MediaPlayer? = null

    @Volatile
    var muted: Boolean = false
        set(value) {
            field = value
            if (value) {
                rainPlayer?.setVolume(0f, 0f)
                nightPlayer?.setVolume(0f, 0f)
            }
        }

    init {
        val attrs = AudioAttributes.Builder()
            .setUsage(AudioAttributes.USAGE_GAME)
            .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
            .build()
        soundPool = SoundPool.Builder()
            .setMaxStreams(10)
            .setAudioAttributes(attrs)
            .build()

        // Load real downloaded CC0/BSD-3-Clause audio clips from res/raw/
        sfxMap[SoundEventType.PISTOL] = soundPool.load(appContext, R.raw.sfx_pistol, 1)
        sfxMap[SoundEventType.SHOTGUN] = soundPool.load(appContext, R.raw.sfx_shotgun, 1)
        sfxMap[SoundEventType.RIFLE] = soundPool.load(appContext, R.raw.sfx_rifle, 1)
        sfxMap[SoundEventType.MELEE] = soundPool.load(appContext, R.raw.sfx_melee, 1)
        sfxMap[SoundEventType.AXE_BUILD] = soundPool.load(appContext, R.raw.sfx_axe, 1)
        sfxMap[SoundEventType.ZOMBIE_BITE] = soundPool.load(appContext, R.raw.sfx_zombie_bite, 1)
        sfxMap[SoundEventType.ZOMBIE_DIE] = soundPool.load(appContext, R.raw.sfx_zombie_die, 1)
        sfxMap[SoundEventType.ZOMBIE_GROAN] = soundPool.load(appContext, R.raw.sfx_zombie_groan, 1)
        sfxMap[SoundEventType.SCREAMER] = soundPool.load(appContext, R.raw.sfx_screamer, 1)
        sfxMap[SoundEventType.DOOR_OPEN] = soundPool.load(appContext, R.raw.sfx_door_open, 1)
        sfxMap[SoundEventType.DOOR_BREAK] = soundPool.load(appContext, R.raw.sfx_door_break, 1)
        sfxMap[SoundEventType.GLASS_BREAK] = soundPool.load(appContext, R.raw.sfx_glass_break, 1)
        sfxMap[SoundEventType.HUMAN_DIE] = soundPool.load(appContext, R.raw.sfx_human_die, 1)
        sfxMap[SoundEventType.LOOT] = soundPool.load(appContext, R.raw.sfx_loot, 1)

        try {
            rainPlayer = MediaPlayer.create(appContext, R.raw.sfx_rain_loop)?.apply {
                isLooping = true
                setVolume(0f, 0f)
            }
            nightPlayer = MediaPlayer.create(appContext, R.raw.sfx_night_loop)?.apply {
                isLooping = true
                setVolume(0f, 0f)
            }
        } catch (_: Throwable) {
        }
    }

    fun playSpatial(events: List<SoundEvent>, camX: Float, camY: Float, audibleRadius: Float) {
        if (muted || events.isEmpty()) return
        val now = System.currentTimeMillis()
        val rMax = audibleRadius.coerceAtLeast(18f)

        for (i in 0 until events.size) {
            val ev = events[i]
            val prev = lastPlayedMs[ev.type] ?: 0L
            val minGap = when (ev.type) {
                SoundEventType.PISTOL, SoundEventType.SHOTGUN, SoundEventType.RIFLE -> 70L
                SoundEventType.MELEE, SoundEventType.ZOMBIE_BITE -> 95L
                else -> 160L
            }
            if (now - prev < minGap) continue

            val dx = ev.wx - camX
            val dy = ev.wy - camY
            val dist = sqrt(dx * dx + dy * dy)
            if (dist > rMax) continue

            val sfxId = sfxMap[ev.type] ?: continue
            val att = (1f - dist / rMax).coerceIn(0.08f, 1f)
            val baseVol = when (ev.type) {
                SoundEventType.PISTOL, SoundEventType.SHOTGUN, SoundEventType.RIFLE, SoundEventType.SCREAMER -> 0.85f
                SoundEventType.DOOR_BREAK, SoundEventType.GLASS_BREAK -> 0.72f
                else -> 0.52f
            }
            val vol = (baseVol * att).coerceIn(0.05f, 1f)
            soundPool.play(sfxId, vol, vol, 1, 0, 1.0f)
            lastPlayedMs[ev.type] = now
        }
    }

    fun updateAmbient(weather: WeatherType, isNight: Boolean, paused: Boolean) {
        if (muted || paused) {
            rainPlayer?.setVolume(0f, 0f)
            nightPlayer?.setVolume(0f, 0f)
            return
        }
        val rainVol = when (weather) {
            WeatherType.STORM -> 0.34f
            WeatherType.RAIN -> 0.22f
            else -> 0f
        }
        val nightVol = if (isNight && rainVol == 0f) 0.16f else if (isNight) 0.06f else 0f

        rainPlayer?.let { p ->
            if (rainVol > 0f && !p.isPlaying) p.start()
            p.setVolume(rainVol, rainVol)
        }
        nightPlayer?.let { p ->
            if (nightVol > 0f && !p.isPlaying) p.start()
            p.setVolume(nightVol, nightVol)
        }
    }

    fun pause() {
        try {
            if (rainPlayer?.isPlaying == true) rainPlayer?.pause()
            if (nightPlayer?.isPlaying == true) nightPlayer?.pause()
        } catch (_: Throwable) {
        }
    }

    fun release() {
        try {
            soundPool.release()
            rainPlayer?.release()
            nightPlayer?.release()
        } catch (_: Throwable) {
        }
    }
}
