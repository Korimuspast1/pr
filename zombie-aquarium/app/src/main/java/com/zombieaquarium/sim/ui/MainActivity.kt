package com.zombieaquarium.sim.ui

import android.os.Bundle
import android.view.View
import android.widget.Button
import android.widget.EditText
import android.widget.SeekBar
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity
import com.zombieaquarium.sim.R
import com.zombieaquarium.sim.engine.OverlayMode
import com.zombieaquarium.sim.engine.SimConfig
import com.zombieaquarium.sim.engine.WeatherType
import com.zombieaquarium.sim.engine.WorldSimulation
import kotlin.random.Random

class MainActivity : AppCompatActivity() {

    private lateinit var worldSurface: WorldSurfaceView
    private var sim: WorldSimulation? = null

    private lateinit var tvClockWeather: TextView
    private lateinit var tvCounters: TextView
    private lateinit var tvGodHint: TextView
    private lateinit var godPanel: View
    private lateinit var inspectorSheet: View
    private lateinit var tvInspectTitle: TextView
    private lateinit var tvInspectSub: TextView
    private lateinit var tvInspectBody: TextView
    private lateinit var inspectorTabRow: View
    private lateinit var tabBrain: Button
    private lateinit var tabStats: Button
    private lateinit var tabBio: Button
    private lateinit var chronicleDrawer: View
    private lateinit var tvChronicleBody: TextView
    private lateinit var setupOverlay: View
    private lateinit var btnCancelSetup: Button

    private lateinit var btnSpeedPause: Button
    private lateinit var btnSpeed1: Button
    private lateinit var btnSpeed2: Button
    private lateinit var btnSpeed4: Button
    private lateinit var btnSpeed8: Button
    private lateinit var btnOverlay: Button

    private var inspectorTab = 0 // 0=Разум, 1=Состояние, 2=Биография

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        worldSurface = findViewById(R.id.worldSurface)
        tvClockWeather = findViewById(R.id.tvClockWeather)
        tvCounters = findViewById(R.id.tvCounters)
        tvGodHint = findViewById(R.id.tvGodHint)
        godPanel = findViewById(R.id.godPanel)
        inspectorSheet = findViewById(R.id.inspectorSheet)
        tvInspectTitle = findViewById(R.id.tvInspectTitle)
        tvInspectSub = findViewById(R.id.tvInspectSub)
        tvInspectBody = findViewById(R.id.tvInspectBody)
        inspectorTabRow = findViewById(R.id.inspectorTabRow)
        tabBrain = findViewById(R.id.tabBrain)
        tabStats = findViewById(R.id.tabStats)
        tabBio = findViewById(R.id.tabBio)
        chronicleDrawer = findViewById(R.id.chronicleDrawer)
        tvChronicleBody = findViewById(R.id.tvChronicleBody)
        setupOverlay = findViewById(R.id.setupOverlay)
        btnCancelSetup = findViewById(R.id.btnCancelSetup)

        btnSpeedPause = findViewById(R.id.btnSpeedPause)
        btnSpeed1 = findViewById(R.id.btnSpeed1)
        btnSpeed2 = findViewById(R.id.btnSpeed2)
        btnSpeed4 = findViewById(R.id.btnSpeed4)
        btnSpeed8 = findViewById(R.id.btnSpeed8)
        btnOverlay = findViewById(R.id.btnOverlay)

        setupSpeedButtons()
        setupOverlayAndGodButtons()
        setupInspector()
        setupWorldGeneratorScreen()

        worldSurface.onSelectionChanged = { sel ->
            if (sel == null) {
                inspectorSheet.visibility = View.GONE
            } else {
                inspectorSheet.visibility = View.VISIBLE
                refreshInspector()
            }
        }
        worldSurface.onHudTick = {
            refreshHud()
            if (inspectorSheet.visibility == View.VISIBLE) refreshInspector()
        }
        worldSurface.onGodActionDone = {
            tvGodHint.visibility = View.GONE
            refreshHud()
        }
    }

    private fun setupSpeedButtons() {
        val map = listOf(
            btnSpeedPause to 0,
            btnSpeed1 to 1,
            btnSpeed2 to 2,
            btnSpeed4 to 4,
            btnSpeed8 to 8
        )
        for ((btn, spd) in map) {
            btn.setOnClickListener {
                sim?.speedMultiplier = spd
                updateSpeedHighlight(spd)
            }
        }
    }

    private fun updateSpeedHighlight(active: Int) {
        val pairs = listOf(btnSpeedPause to 0, btnSpeed1 to 1, btnSpeed2 to 2, btnSpeed4 to 4, btnSpeed8 to 8)
        for ((btn, spd) in pairs) {
            if (spd == active) {
                btn.setBackgroundColor(0xFF10B981.toInt())
                btn.setTextColor(0xFF04120C.toInt())
            } else {
                btn.setBackgroundColor(0xFF1E293B.toInt())
                btn.setTextColor(0xFFF8FAFC.toInt())
            }
        }
    }

    private fun setupOverlayAndGodButtons() {
        btnOverlay.setOnClickListener {
            val modes = OverlayMode.entries
            val next = modes[(worldSurface.overlayMode.ordinal + 1) % modes.size]
            worldSurface.overlayMode = next
            btnOverlay.text = "Слой: ${next.titleRu.substringBefore(' ')}"
        }

        findViewById<Button>(R.id.btnChronicle).setOnClickListener {
            refreshChronicle()
            chronicleDrawer.visibility = View.VISIBLE
        }
        findViewById<Button>(R.id.btnCloseChronicle).setOnClickListener {
            chronicleDrawer.visibility = View.GONE
        }

        findViewById<Button>(R.id.btnGodToggle).setOnClickListener {
            godPanel.visibility = if (godPanel.visibility == View.VISIBLE) View.GONE else View.VISIBLE
            if (godPanel.visibility == View.GONE) {
                worldSurface.activeGodTool = GodTool.NONE
                tvGodHint.visibility = View.GONE
            }
        }
        findViewById<Button>(R.id.btnGodCrate).setOnClickListener {
            worldSurface.activeGodTool = GodTool.SUPPLY_CRATE
            tvGodHint.text = "Тапните по карте, чтобы сбросить гуманитарный ящик"
            tvGodHint.visibility = View.VISIBLE
        }
        findViewById<Button>(R.id.btnGodHorde).setOnClickListener {
            worldSurface.activeGodTool = GodTool.HORDE
            tvGodHint.text = "Тапните по карте, чтобы призвать орду зомби"
            tvGodHint.visibility = View.VISIBLE
        }
        findViewById<Button>(R.id.btnGodSurvivor).setOnClickListener {
            worldSurface.activeGodTool = GodTool.NEW_SURVIVOR
            tvGodHint.text = "Тапните по карте, чтобы подселить выжившего"
            tvGodHint.visibility = View.VISIBLE
        }
        findViewById<Button>(R.id.btnGodWeather).setOnClickListener {
            val s = sim ?: return@setOnClickListener
            val all = WeatherType.entries
            s.godSetWeather(all[(s.weather.ordinal + 1) % all.size])
            refreshHud()
        }

        findViewById<Button>(R.id.btnNewWorld).setOnClickListener {
            btnCancelSetup.visibility = if (sim != null) View.VISIBLE else View.GONE
            setupOverlay.visibility = View.VISIBLE
        }
    }

    private fun setupInspector() {
        findViewById<Button>(R.id.btnInspectClose).setOnClickListener {
            worldSurface.selectedTarget = null
            inspectorSheet.visibility = View.GONE
        }
        findViewById<Button>(R.id.btnInspectFocus).setOnClickListener {
            val s = sim ?: return@setOnClickListener
            val sel = worldSurface.selectedTarget ?: return@setOnClickListener
            synchronized(s.simLock) {
                when (sel) {
                    is SelectionTarget.Survivor -> s.survivorsById[sel.id]?.let { worldSurface.centerOn(it.x, it.y, 24f) }
                    is SelectionTarget.Zombie -> s.zombiesById[sel.id]?.let { worldSurface.centerOn(it.x, it.y, 24f) }
                    is SelectionTarget.Building -> s.map.buildings.getOrNull(sel.id - 1)?.let { worldSurface.centerOn(it.centerX, it.centerY, 18f) }
                }
            }
        }
        val tabBtns = listOf(tabBrain to 0, tabStats to 1, tabBio to 2)
        for ((btn, idx) in tabBtns) {
            btn.setOnClickListener {
                inspectorTab = idx
                updateInspectorTabStyle()
                refreshInspector()
            }
        }
    }

    private fun updateInspectorTabStyle() {
        val tabBtns = listOf(tabBrain to 0, tabStats to 1, tabBio to 2)
        for ((btn, idx) in tabBtns) {
            if (idx == inspectorTab) {
                btn.setBackgroundColor(0xFF10B981.toInt())
                btn.setTextColor(0xFF04120C.toInt())
            } else {
                btn.setBackgroundColor(0xFF1E293B.toInt())
                btn.setTextColor(0xFFE2E8F0.toInt())
            }
        }
    }

    private fun setupWorldGeneratorScreen() {
        val seekMap = findViewById<SeekBar>(R.id.seekMapSize)
        val tvMap = findViewById<TextView>(R.id.tvMapSizeLabel)
        val seekSurv = findViewById<SeekBar>(R.id.seekSurvivors)
        val tvSurv = findViewById<TextView>(R.id.tvSurvivorsLabel)
        val seekZom = findViewById<SeekBar>(R.id.seekZombieDensity)
        val tvZom = findViewById<TextView>(R.id.tvZombieDensityLabel)
        val seekLoot = findViewById<SeekBar>(R.id.seekLootRarity)
        val tvLoot = findViewById<TextView>(R.id.tvLootRarityLabel)
        val seekRun = findViewById<SeekBar>(R.id.seekRunners)
        val tvRun = findViewById<TextView>(R.id.tvRunnerRatioLabel)
        val etSeed = findViewById<EditText>(R.id.etSeed)

        etSeed.setText((100000 + Random.nextInt(899999)).toString())
        findViewById<Button>(R.id.btnRandomSeed).setOnClickListener {
            etSeed.setText((100000 + Random.nextInt(899999)).toString())
        }

        val mapSizes = intArrayOf(128, 160, 200)
        seekMap.setOnSeekBarChangeListener(simpleSeek { p ->
            tvMap.text = "Размер карты: ${mapSizes[p]}x${mapSizes[p]}"
        })
        seekSurv.setOnSeekBarChangeListener(simpleSeek { p ->
            tvSurv.text = "Стартовых выживших: ${12 + p}"
        })
        seekZom.setOnSeekBarChangeListener(simpleSeek { p ->
            val d = 0.5f + p * 0.1f
            tvZom.text = String.format("Плотность зомби: %.1fx", d)
        })
        seekLoot.setOnSeekBarChangeListener(simpleSeek { p ->
            val l = 0.5f + p * 0.1f
            tvLoot.text = String.format("Ресурсы и лут: %.1fx", l)
        })
        seekRun.setOnSeekBarChangeListener(simpleSeek { p ->
            val rPct = 5 + p
            val hFreq = 0.6f + p * 0.04f
            tvRun.text = String.format("Доля бегунов / частота орд: %d%% / %.1fx", rPct, hFreq)
        })

        btnCancelSetup.setOnClickListener {
            setupOverlay.visibility = View.GONE
        }

        findViewById<Button>(R.id.btnStartWorld).setOnClickListener {
            val mapSize = mapSizes[seekMap.progress]
            val survCount = 12 + seekSurv.progress
            val zDens = 0.5f + seekZom.progress * 0.1f
            val lRarity = 0.5f + seekLoot.progress * 0.1f
            val rRatio = (5 + seekRun.progress) / 100f
            val hFreq = 0.6f + seekRun.progress * 0.04f
            val seed = etSeed.text.toString().toLongOrNull() ?: System.currentTimeMillis()

            val newSim = WorldSimulation(
                SimConfig(
                    mapSize = mapSize,
                    initialSurvivors = survCount,
                    zombieDensity = zDens,
                    lootRarity = lRarity,
                    runnerRatio = rRatio,
                    hordeFrequency = hFreq,
                    seed = seed
                )
            )
            sim = newSim
            worldSurface.simulation = newSim
            worldSurface.selectedTarget = null
            inspectorSheet.visibility = View.GONE
            setupOverlay.visibility = View.GONE
            updateSpeedHighlight(1)
            refreshHud()
        }
    }

    private fun refreshHud() {
        val s = sim ?: return
        synchronized(s.simLock) {
            val phase = if (s.isNight) "Ночь" else "День"
            tvClockWeather.text = "День ${s.day} • ${s.formatTime()} ($phase) • ${s.weather.titleRu}"
            tvCounters.text = "Люди: ${s.livingSurvivorsCount} • Лагеря: ${s.camps.size} • Зомби: ${s.livingZombiesCount} • Погибло: ${s.deadSurvivorsCount}"
        }
    }

    private fun refreshChronicle() {
        val s = sim ?: return
        synchronized(s.simLock) {
            if (s.chronicle.isEmpty()) {
                tvChronicleBody.text = "Пока всё спокойно..."
                return
            }
            val sb = StringBuilder()
            for (ev in s.chronicle) {
                sb.append("День ").append(ev.day).append(", ").append(ev.timeStr)
                    .append(" [").append(ev.category).append("] — ")
                    .append(ev.message).append("\n\n")
            }
            tvChronicleBody.text = sb.toString()
        }
    }

    private fun refreshInspector() {
        val s = sim ?: return
        val sel = worldSurface.selectedTarget ?: return

        synchronized(s.simLock) {
            when (sel) {
                is SelectionTarget.Survivor -> {
                    val agent = s.survivorsById[sel.id]
                    if (agent == null || !agent.alive) {
                        tvInspectTitle.text = "${agent?.name ?: "Выживший"} (Погиб)"
                        tvInspectSub.text = agent?.causeOfDeath ?: ""
                        inspectorTabRow.visibility = View.GONE
                        tvInspectBody.text = agent?.bioLog?.joinToString("\n") ?: ""
                        return
                    }
                    inspectorTabRow.visibility = View.VISIBLE
                    val campName = s.campsById[agent.campId]?.name ?: "Одиночка"
                    tvInspectTitle.text = "${agent.name}, ${agent.age} лет • HP ${agent.hp.toInt()}%"
                    tvInspectSub.text = "$campName (${agent.role.titleRu}) • ${agent.traits.joinToString { it.titleRu }}"

                    val sb = StringBuilder()
                    when (inspectorTab) {
                        0 -> {
                            sb.append("Состояние FSM: ").append(agent.fsmState.titleRu).append("\n")
                            sb.append("Активная цель: ").append(agent.activeGoal.titleRu).append("\n")
                            sb.append("Мысль: «").append(agent.thoughtText).append("»\n\n")
                            sb.append("Топ-3 оценки Utility AI:\n")
                            for (u in agent.topUtilities) {
                                sb.append(" • ").append(u.goal.titleRu)
                                    .append(" [").append(u.score.toInt()).append("] — ")
                                    .append(u.reasonRu).append("\n")
                            }
                            sb.append("\nПлан GOAP:\n")
                            if (agent.goapPlan.isEmpty()) {
                                sb.append(" • Оценка новой цели...")
                            } else {
                                for (step in agent.goapPlan) {
                                    sb.append(" -> ").append(step.action.titleRu)
                                    if (step.detail.isNotEmpty()) sb.append(" (").append(step.detail).append(")")
                                    sb.append("\n")
                                }
                            }
                            sb.append("\nПамять (точек: ").append(agent.memories.size).append("):")
                            for (m in agent.memories.takeLast(5)) {
                                sb.append("\n • ").append(m.kind.titleRu).append(" (${m.x},${m.y}) ").append(m.note)
                            }
                        }
                        1 -> {
                            sb.append("Сытость: ").append(agent.hunger.toInt()).append("% | Жажда: ").append(100 - agent.thirst.toInt()).append("%\n")
                            sb.append("Бодрость: ").append(agent.energy.toInt()).append("% | Тепло: ").append(agent.warmth.toInt()).append("%\n")
                            sb.append("Выносливость: ").append(agent.stamina.toInt()).append("% | Стресс: ").append(agent.stress.toInt()).append("%\n")
                            if (agent.bleeding > 0f) sb.append("ТРАВМА: Кровотечение!\n")
                            if (agent.limping) sb.append("ТРАВМА: Хромота (замедлен)\n")
                            if (agent.infected) sb.append("ЗАРАЖЕНИЕ ОТ УКУСА! (осталось тиков: ").append(agent.infectionTimerTicks).append(")\n")
                            sb.append("\nОружие: ").append(agent.meleeWeapon.titleRu)
                            if (agent.firearm != null) sb.append(" + ").append(agent.firearm!!.titleRu).append(" (патр: ").append(agent.ammo).append(")")
                            sb.append("\nРюкзак: Еда=${agent.food}, Вода=${agent.water}, Бинты=${agent.bandages}, Антиб=${agent.antibiotics}, Доски=${agent.wood}, Лом=${agent.scrap}")
                            if (agent.hiddenPersonalFood > 0) sb.append(", Заначка=${agent.hiddenPersonalFood}")
                            sb.append("\nФонарик: ").append(if (agent.hasFlashlight) "Да" else "Нет")
                            if (agent.relations.isNotEmpty()) {
                                sb.append("\n\nОтношения:")
                                for ((oid, rel) in agent.relations.entries.take(6)) {
                                    val oname = s.survivorsById[oid]?.name ?: "#$oid"
                                    sb.append("\n • ").append(oname).append(": ").append(if (rel > 0) "+$rel" else "$rel")
                                }
                            }
                        }
                        else -> {
                            sb.append(if (agent.bioLog.isEmpty()) "Событий пока нет." else agent.bioLog.joinToString("\n"))
                        }
                    }
                    tvInspectBody.text = sb.toString()
                }

                is SelectionTarget.Zombie -> {
                    val z = s.zombiesById[sel.id]
                    inspectorTabRow.visibility = View.GONE
                    tvInspectTitle.text = "${z?.type?.titleRu ?: "Зомби"} #${sel.id}"
                    tvInspectSub.text = if (z?.alive == true) "HP ${z.hp.toInt()} • Урон ${z.type.attackDamage.toInt()}" else "Уничтожен"
                    tvInspectBody.text = if (z != null && z.alive) {
                        val tgt = s.survivorsById[z.targetSurvivorId]?.name ?: "Идёт по шуму / запаху"
                        "Цель: $tgt\nСкорость: ${z.type.speedTilesPerSec} тайл/с\nУрон баррикадам: ${z.type.structureDamage}"
                    } else ""
                }

                is SelectionTarget.Building -> {
                    val b = s.map.buildings.getOrNull(sel.id - 1)
                    inspectorTabRow.visibility = View.GONE
                    tvInspectTitle.text = b?.type?.titleRu ?: "Здание"
                    val camp = b?.let { s.campsById[it.claimedByCampId] }
                    tvInspectSub.text = if (camp != null) "База группы «${camp.name}» (${camp.memberIds.size} чел.)" else "Не занято"
                    val lootLeft = s.map.lootContainers.count { it.buildingId == sel.id && !it.isEmpty() }
                    tvInspectBody.text = buildString {
                        append("Обыскано точек / осталось с лутом: $lootLeft\n")
                        if (camp != null) {
                            append("Склад лагеря: Еда=${camp.storageFood}, Вода=${camp.storageWater}, Доски=${camp.storageWood}\n")
                        }
                    }
                }
            }
        }
    }

    private fun simpleSeek(onChange: (Int) -> Unit) = object : SeekBar.OnSeekBarChangeListener {
        override fun onProgressChanged(seekBar: SeekBar?, progress: Int, fromUser: Boolean) = onChange(progress)
        override fun onStartTrackingTouch(seekBar: SeekBar?) {}
        override fun onStopTrackingTouch(seekBar: SeekBar?) {}
    }
}
