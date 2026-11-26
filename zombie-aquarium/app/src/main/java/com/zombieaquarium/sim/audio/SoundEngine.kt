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
    SHELL_CASING,
    RELOAD,
    MELEE,
    AXE_BUILD,
    SPLAT,
    ZOMBIE_BITE,
    ZOMBIE_DIE,
    ZOMBIE_GROAN,
    ZOMBIE_MOB,
    SCREAMER,
    DOOR_OPEN,
    DOOR_BREAK,
    GLASS_BREAK,
    HUMAN_DIE,
    FEMALE_SCREAM,
    LOOT,
    BANDAGE,
    FOOTSTEP,
    THUNDER
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
    private var cityPlayer: MediaPlayer? = null
    private var windPlayer: MediaPlayer? = null
    private var songPlayer: MediaPlayer? = null

    @Volatile
    var muted: Boolean = false
        set(value) {
            field = value
            if (value) {
                rainPlayer?.setVolume(0f, 0f)
                nightPlayer?.setVolume(0f, 0f)
                cityPlayer?.setVolume(0f, 0f)
                windPlayer?.setVolume(0f, 0f)
                songPlayer?.setVolume(0f, 0f)
            }
        }

    @Volatile
    var ambientEnabled: Boolean = true
        set(value) {
            field = value
            if (!value) {
                windPlayer?.setVolume(0f, 0f)
                songPlayer?.setVolume(0f, 0f)
            }
        }

    init {
        val attrs = AudioAttributes.Builder()
            .setUsage(AudioAttributes.USAGE_GAME)
            .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
            .build()
        soundPool = SoundPool.Builder()
            .setMaxStreams(12)
            .setAudioAttributes(attrs)
            .build()

        sfxMap[SoundEventType.PISTOL] = soundPool.load(appContext, R.raw.sfx_pistol, 1)
        sfxMap[SoundEventType.SHOTGUN] = soundPool.load(appContext, R.raw.sfx_shotgun, 1)
        sfxMap[SoundEventType.RIFLE] = soundPool.load(appContext, R.raw.sfx_rifle, 1)
        sfxMap[SoundEventType.SHELL_CASING] = soundPool.load(appContext, R.raw.sfx_shell_casing, 1)
        sfxMap[SoundEventType.RELOAD] = soundPool.load(appContext, R.raw.sfx_reload, 1)
        sfxMap[SoundEventType.MELEE] = soundPool.load(appContext, R.raw.sfx_melee, 1)
        sfxMap[SoundEventType.AXE_BUILD] = soundPool.load(appContext, R.raw.sfx_axe, 1)
        sfxMap[SoundEventType.SPLAT] = soundPool.load(appContext, R.raw.sfx_splat, 1)
        sfxMap[SoundEventType.ZOMBIE_BITE] = soundPool.load(appContext, R.raw.sfx_zombie_bite, 1)
        sfxMap[SoundEventType.ZOMBIE_DIE] = soundPool.load(appContext, R.raw.sfx_zombie_die, 1)
        sfxMap[SoundEventType.ZOMBIE_GROAN] = soundPool.load(appContext, R.raw.sfx_zombie_groan, 1)
        sfxMap[SoundEventType.ZOMBIE_MOB] = soundPool.load(appContext, R.raw.sfx_zombie_mob, 1)
        sfxMap[SoundEventType.SCREAMER] = soundPool.load(appContext, R.raw.sfx_screamer, 1)
        sfxMap[SoundEventType.DOOR_OPEN] = soundPool.load(appContext, R.raw.sfx_door_open, 1)
        sfxMap[SoundEventType.DOOR_BREAK] = soundPool.load(appContext, R.raw.sfx_door_break, 1)
        sfxMap[SoundEventType.GLASS_BREAK] = soundPool.load(appContext, R.raw.sfx_glass_break, 1)
        sfxMap[SoundEventType.HUMAN_DIE] = soundPool.load(appContext, R.raw.sfx_human_die, 1)
        sfxMap[SoundEventType.FEMALE_SCREAM] = soundPool.load(appContext, R.raw.sfx_female_scream, 1)
        sfxMap[SoundEventType.LOOT] = soundPool.load(appContext, R.raw.sfx_loot, 1)
        sfxMap[SoundEventType.BANDAGE] = soundPool.load(appContext, R.raw.sfx_bandage, 1)
        sfxMap[SoundEventType.FOOTSTEP] = soundPool.load(appContext, R.raw.sfx_footstep, 1)
        sfxMap[SoundEventType.THUNDER] = soundPool.load(appContext, R.raw.sfx_thunder, 1)

        try {
            rainPlayer = MediaPlayer.create(appContext, R.raw.sfx_rain_loop)?.apply {
                isLooping = true
                setVolume(0f, 0f)
            }
            nightPlayer = MediaPlayer.create(appContext, R.raw.sfx_night_loop)?.apply {
                isLooping = true
                setVolume(0f, 0f)
            }
            cityPlayer = MediaPlayer.create(appContext, R.raw.sfx_city_loop)?.apply {
                isLooping = true
                setVolume(0f, 0f)
            }
            windPlayer = MediaPlayer.create(appContext, R.raw.sfx_ambient_wind)?.apply {
                isLooping = true
                setVolume(0f, 0f)
            }
            songPlayer = MediaPlayer.create(appContext, R.raw.sfx_campfire_song)?.apply {
                isLooping = true
                setVolume(0f, 0f)
            }
        } catch (_: Throwable) {
        }
    }

    fun playSpatial(events: List<SoundEvent>, camX: Float, camY: Float, audibleRadius: Float) {
        if (muted || events.isEmpty()) return
        val now = System.currentTimeMillis()
        val rMax = audibleRadius.coerceAtLeast(22f)

        for (i in 0 until events.size) {
            val ev = events[i]
            val prev = lastPlayedMs[ev.type] ?: 0L
            val minGap = when (ev.type) {
                SoundEventType.PISTOL, SoundEventType.SHOTGUN, SoundEventType.RIFLE -> 75L
                SoundEventType.MELEE, SoundEventType.SPLAT, SoundEventType.ZOMBIE_BITE -> 105L
                SoundEventType.FOOTSTEP -> 260L
                SoundEventType.ZOMBIE_MOB -> 2800L
                else -> 160L
            }
            if (now - prev < minGap) continue

            val isGlobal = ev.type == SoundEventType.THUNDER || ev.type == SoundEventType.ZOMBIE_MOB
            val dx = ev.wx - camX
            val dy = ev.wy - camY
            val dist = sqrt(dx * dx + dy * dy)
            if (!isGlobal && dist > rMax) continue

            val sfxId = sfxMap[ev.type] ?: continue
            val att = if (isGlobal) 0.72f else (1f - dist / rMax).coerceIn(0.08f, 1f)
            val baseVol = when (ev.type) {
                SoundEventType.PISTOL, SoundEventType.SHOTGUN, SoundEventType.RIFLE, SoundEventType.SCREAMER, SoundEventType.THUNDER -> 0.86f
                SoundEventType.DOOR_BREAK, SoundEventType.GLASS_BREAK, SoundEventType.SPLAT -> 0.70f
                SoundEventType.FOOTSTEP, SoundEventType.SHELL_CASING -> 0.34f
                else -> 0.54f
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
            cityPlayer?.setVolume(0f, 0f)
            windPlayer?.setVolume(0f, 0f)
            songPlayer?.setVolume(0f, 0f)
            return
        }
        val rainVol = when (weather) {
            WeatherType.STORM -> 0.34f
            WeatherType.RAIN -> 0.22f
            else -> 0f
        }
        val nightVol = if (isNight && rainVol == 0f) 0.16f else if (isNight) 0.06f else 0f
        val cityVol = if (!isNight && rainVol == 0f) 0.13f else 0.04f
        val windVol = if (ambientEnabled) 0.24f else 0f
        val songVol = if (ambientEnabled) 0.13f else 0f

        rainPlayer?.let { p ->
            if (rainVol > 0f && !p.isPlaying) p.start()
            p.setVolume(rainVol, rainVol)
        }
        nightPlayer?.let { p ->
            if (nightVol > 0f && !p.isPlaying) p.start()
            p.setVolume(nightVol, nightVol)
        }
        cityPlayer?.let { p ->
            if (cityVol > 0f && !p.isPlaying) p.start()
            p.setVolume(cityVol, cityVol)
        }
        windPlayer?.let { p ->
            if (windVol > 0f && !p.isPlaying) p.start()
            p.setVolume(windVol, windVol)
        }
        songPlayer?.let { p ->
            if (songVol > 0f && !p.isPlaying) p.start()
            p.setVolume(songVol, songVol)
        }
    }

    fun pause() {
        try {
            if (rainPlayer?.isPlaying == true) rainPlayer?.pause()
            if (nightPlayer?.isPlaying == true) nightPlayer?.pause()
            if (cityPlayer?.isPlaying == true) cityPlayer?.pause()
            if (windPlayer?.isPlaying == true) windPlayer?.pause()
            if (songPlayer?.isPlaying == true) songPlayer?.pause()
        } catch (_: Throwable) {
        }
    }

    fun release() {
        try {
            soundPool.release()
            rainPlayer?.release()
            nightPlayer?.release()
            cityPlayer?.release()
            windPlayer?.release()
            songPlayer?.release()
        } catch (_: Throwable) {
        }
    }
}
