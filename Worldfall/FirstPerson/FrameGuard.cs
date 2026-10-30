using System;
using System.Collections.Generic;
using System.Text;
using FirstPerson.Net;
using UnityEngine;
using db;

namespace FirstPerson
{
	internal static class FrameGuard
	{
		private sealed class Step
		{
			public string Name;

			public Action<MapBox> Run;
		}

		internal static bool Enabled = true;

		private static bool _listening;

		private static int _streak;

		private static int _logged;

		private static float _streakSince;

		private static readonly HashSet<string> Reported = new HashSet<string>();

		private static string _message;

		private static string _stack;

		private static int _frame = -1;

		private static bool _fromGame;

		private static int _cutFrame = -1;

		private static string _cutAt;

		private static readonly Step[] Steps = new Step[33]
		{
			S("ScrollingHelper.update", delegate
			{
				ScrollingHelper.update();
			}),
			S("MoveCamera.update", delegate(MapBox w)
			{
				w.move_camera.update();
			}),
			S("SignalManager.update", delegate(MapBox w)
			{
				w.stack_effects.light_blobs.Clear();
				w._signal_manager.update();
			}),
			S("MapBox.clearFrameCaches", delegate(MapBox w)
			{
				w.clearFrameCaches();
			}),
			S("Config.updateCrashMetadata", delegate
			{
				Config.updateCrashMetadata();
			}),
			S("PlayerConfig.update", delegate
			{
				PlayerConfig.instance.update();
			}),
			S("Tooltip.checkClearAll", delegate
			{
				Tooltip.checkClearAll();
			}),
			S("CursorTooltipHelper.update", delegate
			{
				CursorTooltipHelper.update();
			}),
			S("GameStats.updateStats", delegate(MapBox w)
			{
				w.delta_time = Time.fixedDeltaTime;
				w.fixed_delta_time = Time.fixedDeltaTime;
				w.game_stats.updateStats(Time.deltaTime);
			}),
			S("Zones.getMapMetaAsset", delegate(MapBox w)
			{
				w._is_paused = Config.paused || ScrollWindow.isWindowActive() || RewardedAds.isShowing();
				w._cached_map_meta_asset = Zones.getMapMetaAsset();
			}),
			S("BattleKeeperManager.update", delegate(MapBox w)
			{
				BattleKeeperManager.update(w.delta_time);
			}),
			S("MapBox.calculateCurElapsed", delegate(MapBox w)
			{
				w.elapsed = w.calculateCurElapsed();
				if (Config.fps_lock_30)
				{
					w.elapsed *= 2f;
				}
			}),
			S("CursorSpeed.update", delegate
			{
				MapBox.cursor_speed.update();
			}),
			S("MusicBox.update", delegate(MapBox w)
			{
				MusicBox.inst.update(w.delta_time);
			}),
			S("AutoTesterBot.update", delegate(MapBox w)
			{
				w.auto_tester.update(w.elapsed);
			}),
			S("AutoSaveManager.update", delegate
			{
				AutoSaveManager.update();
			}),
			S("DBInserter.executeCommandsAsync", delegate
			{
				DBInserter.executeCommandsAsync();
			}),
			S("MapBox.checkMainSimulationUpdate", delegate(MapBox w)
			{
				w.checkMainSimulationUpdate();
			}),
			S("DelayedActionsManager.update", delegate(MapBox w)
			{
				w.delayed_actions_manager.update(w.elapsed, w.delta_time);
			}),
			S("WorldTilemap.update", delegate(MapBox w)
			{
				w.tilemap.update(w.elapsed);
			}),
			S("MapBox.updateShake", delegate(MapBox w)
			{
				w.updateShake(w.elapsed);
			}),
			S("MapStats.updateStatsForPanel", delegate(MapBox w)
			{
				w.map_stats.updateStatsForPanel(w.delta_time);
			}),
			S("QualityChanger.update", delegate(MapBox w)
			{
				w.quality_changer.update();
			}),
			S("MapBox.updateTransitionEffect", delegate(MapBox w)
			{
				w.updateTransitionEffect();
			}),
			S("PlayerControl.updateControls", delegate(MapBox w)
			{
				w.player_control.updateControls();
			}),
			S("ZoneCamera.update", delegate(MapBox w)
			{
				w.zone_camera.update();
			}),
			S("UnitSelectionEffect.update", delegate(MapBox w)
			{
				if (w._unit_select_effect != null)
				{
					w._unit_select_effect.update(w.elapsed);
				}
			}),
			S("ZoneCalculator.updateAnimationsAndSelections", delegate(MapBox w)
			{
				w.zone_calculator.updateAnimationsAndSelections();
			}),
			S("NameplateManager.update", delegate(MapBox w)
			{
				w.nameplate_manager.update();
			}),
			S("MapBox.renderStuff", delegate(MapBox w)
			{
				if (Config.time_scale_asset != null && Config.time_scale_asset.render_skip && w._render_skip < 2)
				{
					w._render_skip++;
				}
				else
				{
					w._render_skip = 0;
					w.calculateVisibleObjects();
					w.renderStuff();
				}
			}),
			S("MapBox.updateDynamicSprites", delegate(MapBox w)
			{
				w.updateDynamicSprites();
			}),
			S("LightRenderer.update", delegate(MapBox w)
			{
				LightRenderer.instance.update(w.delta_time);
			}),
			S("MapBox.updateFinish", delegate(MapBox w)
			{
				w.updateFinish();
			})
		};

		private static double _seenTime = -1.0;

		private static bool _running;

		private static readonly HashSet<string> StepFailed = new HashSet<string>();

		private static float _stillSince = -1f;

		private static bool _stillTold;

		internal static int Broken { get; private set; }

		internal static int Resumed { get; private set; }

		public static void Install()
		{
			if (!_listening)
			{
				Application.logMessageReceived += OnLog;
				_listening = true;
			}
		}

		public static void Uninstall()
		{
			if (_listening)
			{
				Application.logMessageReceived -= OnLog;
				_listening = false;
			}
		}

		private static void OnLog(string message, string stack, LogType type)
		{
			if (type != LogType.Exception || _running)
			{
				return;
			}
			bool flag = stack?.Contains("MapBox") ?? false;
			if (_frame != Time.frameCount || !_fromGame || flag)
			{
				_frame = Time.frameCount;
				_fromGame = flag;
				_message = message;
				_stack = stack;
				string text = StepOf(stack);
				if (text != null)
				{
					_cutFrame = Time.frameCount;
					_cutAt = text;
				}
			}
		}

		internal static string StepOf(string stack)
		{
			if (stack == null)
			{
				return null;
			}
			string[] array = stack.Split('\n');
			for (int i = 0; i < array.Length; i++)
			{
				if (!array[i].TrimStart().StartsWith("MapBox.Update ()", StringComparison.Ordinal))
				{
					continue;
				}
				for (int j = i + 1; j < array.Length; j++)
				{
					if (array[j].Trim().Length > 0)
					{
						return null;
					}
				}
				if (i == 0)
				{
					return "";
				}
				string text = array[i - 1].Trim();
				int num = text.IndexOf(" (", StringComparison.Ordinal);
				if (num > 0)
				{
					text = text.Substring(0, num);
				}
				return Detour.ReplacedName(text) ?? text;
			}
			return null;
		}

		private static Step S(string name, Action<MapBox> run)
		{
			return new Step
			{
				Name = name,
				Run = run
			};
		}

		private static int IndexOf(string at, MapBox world)
		{
			if (!string.IsNullOrEmpty(at))
			{
				for (int i = 0; i < Steps.Length; i++)
				{
					if (Steps[i].Name == at)
					{
						return i;
					}
				}
				if (at == "MapBox.updateSimulation")
				{
					return Array.FindIndex(Steps, (Step s) => s.Name == "MapBox.checkMainSimulationUpdate");
				}
				if (at == "MapBox.calculateVisibleObjects")
				{
					return Array.FindIndex(Steps, (Step s) => s.Name == "MapBox.renderStuff");
				}
			}
			int num = Array.FindIndex(Steps, (Step s) => s.Name == "MapBox.checkMainSimulationUpdate");
			if (!Moved(world))
			{
				return num - 1;
			}
			return num;
		}

		private static bool Moved(MapBox world)
		{
			return ((world.map_stats != null) ? world.map_stats.world_time : 0.0) != _seenTime;
		}

		private static void RunRest(MapBox world, int from)
		{
			_running = true;
			try
			{
				for (int i = from + 1; i < Steps.Length; i++)
				{
					Step step = Steps[i];
					try
					{
						step.Run(world);
					}
					catch (Exception ex)
					{
						if (StepFailed.Add(step.Name + ex.GetType().Name) && StepFailed.Count < 24)
						{
							Log.Error("frame guard: the game's " + step.Name + ", run after its frame broke off, threw too: " + ex);
						}
					}
				}
			}
			finally
			{
				_running = false;
			}
			Resumed++;
		}

		public static void AfterGameFrame()
		{
			try
			{
				if (!Config.game_loaded || World.world == null || SmoothLoader.isLoading())
				{
					_cutFrame = -1;
					return;
				}
				MapBox world = World.world;
				bool flag = _cutFrame == Time.frameCount;
				_cutFrame = -1;
				bool flag2 = world.timer_nutrition_decay < 0f && SimGlobals.m.interval_nutrition_decay > 0f;
				if (!flag && !flag2)
				{
					if (_streak > 0)
					{
						Log.Info($"the game's frame runs to its end again, after {_streak} cut short over {Time.unscaledTime - _streakSince:F1} s");
					}
					_streak = 0;
					WatchClock(world);
					return;
				}
				Broken++;
				if (_streak++ == 0)
				{
					_streakSince = Time.unscaledTime;
				}
				if (!Enabled)
				{
					WatchClock(world);
					return;
				}
				int num = (flag ? IndexOf(_cutAt, world) : (-1));
				if (flag)
				{
					RunRest(world, num);
				}
				else
				{
					try
					{
						world.updateFinish();
					}
					catch
					{
						world.timer_nutrition_decay = SimGlobals.m.interval_nutrition_decay;
					}
				}
				Report(flag ? _cutAt : null, num < Array.FindIndex(Steps, (Step x) => x.Name == "MapBox.checkMainSimulationUpdate"));
				WatchClock(world);
			}
			catch (Exception ex)
			{
				_running = false;
				if (_logged++ < 3)
				{
					Log.Error("frame guard: " + ex.Message);
				}
			}
		}

		private static void Report(string at, bool stepRun)
		{
			bool flag = _frame == Time.frameCount || _frame == Time.frameCount - 1;
			string text = ((flag && _message != null) ? _message : "(no exception seen)");
			string item = ((text.Length > 160) ? text.Substring(0, 160) : text);
			if (Reported.Count < 12 && Reported.Add(item))
			{
				string text2 = ((at == null) ? "its hunger clock left run out: the whole world would starve a step a frame; wound up again" : ("in " + ((at.Length > 0) ? at : "MapBox.Update itself") + ": the rest of its frame run here" + (stepRun ? ", the world's step among it" : "")));
				Log.Error("the game's frame was cut short before its end (" + text2 + "). It broke off on: " + text + ((flag && _stack != null) ? ("\n" + _stack.TrimEnd()) : ""));
			}
		}

		private static void WatchClock(MapBox world)
		{
			double num = ((world.map_stats != null) ? world.map_stats.world_time : 0.0);
			bool flag = num != _seenTime;
			_seenTime = num;
			bool flag2 = false;
			try
			{
				flag2 = Config.paused || ScrollWindow.isWindowActive() || RewardedAds.isShowing() || NetSession.Joined;
			}
			catch
			{
			}
			if (flag || flag2)
			{
				if (_stillTold && flag)
				{
					Log.Info($"world clock: moving again after {Time.unscaledTime - _stillSince:F1} s");
				}
				_stillSince = -1f;
				_stillTold = false;
			}
			else if (_stillSince < 0f)
			{
				_stillSince = Time.unscaledTime;
			}
			else if (!_stillTold && !(Time.unscaledTime - _stillSince < 3f))
			{
				_stillTold = true;
				Log.Error("world clock: stood still " + (Time.unscaledTime - _stillSince).ToString("F1") + " s with the game not paused (" + Holds(world) + ")");
			}
		}

		internal static string Holds(MapBox world)
		{
			StringBuilder b = new StringBuilder();
			Add("paused", () => Config.paused);
			Add("is_paused", () => world._is_paused);
			Add("window", () => ScrollWindow.isWindowActive());
			Add("speed", () => (Config.time_scale_asset != null) ? (Config.time_scale_asset.id + " x" + Config.time_scale_asset.multiplier + " ticks " + Config.time_scale_asset.ticks) : "none");
			Add("step", () => Time.fixedDeltaTime.ToString("F4"));
			Add("elapsed", () => world.elapsed.ToString("F4"));
			Add("real-time clock", () => GameClock.Active);
			Add("years", () => WorldPace.Factor);
			Add("frames cut", () => Broken + " (" + Resumed + " run on)");
			Add("last exception", () => (_message != null) ? (Time.frameCount - _frame + " frames ago: " + ((_message.Length > 120) ? _message.Substring(0, 120) : _message)) : "none");
			return b.ToString();
			void Add(string what, Func<object> read)
			{
				object value;
				try
				{
					value = read();
				}
				catch (Exception ex)
				{
					value = "? " + ex.GetType().Name;
				}
				if (b.Length > 0)
				{
					b.Append(", ");
				}
				b.Append(what).Append(' ').Append(value);
			}
		}
	}
}
