using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using FirstPerson.Net;
using UnityEngine;
using UnityEngine.UI;

namespace FirstPerson
{
	internal static class WorldPace
	{
		internal sealed class Pace
		{
			public string Id;

			public float Years;

			public int Of;

			public string Name;
		}

		internal static class Ui
		{
			private static bool _assets;

			private static bool _buttons;

			private static float _nextLook;

			private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();

			private const string IconPath = "ui/Icons/iconClockFpYears";

			private static int _looks;

			private static Transform _row;

			private static readonly Color32 Fill = new Color32(150, 226, 120, byte.MaxValue);

			private static readonly Color32 Shadow = new Color32(0, 0, 0, byte.MaxValue);

			private static readonly string[] Hourglass = new string[5] { "#####", ".#.#.", "..#..", ".#.#.", "#####" };

			private static readonly string[] One = new string[4] { ".#", "##", ".#", ".#" };

			private static readonly string[] Slash = new string[5] { "...#", "..#.", "..#.", ".#..", "#..." };

			private static readonly string[] Two = new string[4] { "##", ".#", "#.", "##" };

			private static readonly string[] Five = new string[4] { "##", "#.", ".#", "##" };

			private static readonly string[] Ten = new string[4] { "#.###", "#.#.#", "#.#.#", "#.###" };

			public static void Ensure()
			{
				if (!_assets)
				{
					Assets();
				}
				if (_assets && !_buttons && Time.unscaledTime >= _nextLook)
				{
					_nextLook = Time.unscaledTime + 1f;
					Buttons();
				}
			}

			private static void Assets()
			{
				WorldTimeScaleLibrary time_scales = AssetManager.time_scales;
				if (time_scales == null || time_scales.list == null || time_scales.dict == null || !time_scales.dict.ContainsKey("x1"))
				{
					return;
				}
				for (int i = 0; i < Paces.Length; i++)
				{
					Pace pace = Paces[i];
					if (!time_scales.dict.ContainsKey(pace.Id))
					{
						time_scales.add(new WorldTimeScaleAsset
						{
							id = pace.Id,
							multiplier = 1f,
							ticks = 1,
							conway_ticks = 1,
							path_icon = "ui/Icons/iconClockFpYears" + pace.Of,
							locale_key = "speed_" + pace.Id
						});
					}
					Lang.AddGameText("speed_" + pace.Id, Sentence(pace));
					if (!Icons.ContainsKey(pace.Id))
					{
						Icons[pace.Id] = MakeIcon(pace.Of, "ui/Icons/iconClockFpYears" + pace.Of);
					}
				}
				for (int j = 0; j < Paces.Length; j++)
				{
					if (time_scales.dict.TryGetValue(Paces[j].Id, out var value) && value != null)
					{
						time_scales.list.Remove(value);
						time_scales.list.Insert(0, value);
					}
				}
				_assets = true;
			}

			private static string Sentence(Pace p)
			{
				if (p.Of != 2)
				{
					if (p.Of != 5)
					{
						return "Years at a tenth";
					}
					return "Years at a fifth";
				}
				return "Years at half speed";
			}

			private static void Buttons()
			{
				Transform transform = Row();
				if (transform == null)
				{
					if (++_looks > 60)
					{
						_nextLook = Time.unscaledTime + 10f;
					}
					return;
				}
				Transform transform2 = transform.Find("slow_mo") ?? transform.Find("x1");
				if (transform2 == null)
				{
					return;
				}
				Transform transform3 = transform.Find("slow_mo") ?? transform2;
				for (int i = 0; i < Paces.Length; i++)
				{
					Pace pace = Paces[i];
					Transform transform4 = transform.Find(pace.Id);
					if (transform4 == null)
					{
						GameObject gameObject = UnityEngine.Object.Instantiate(transform2.gameObject, transform, worldPositionStays: false);
						gameObject.name = pace.Id;
						transform4 = gameObject.transform;
						PowerButton component = gameObject.GetComponent<PowerButton>();
						if (component != null && component.icon != null && Icons.TryGetValue(pace.Id, out var value) && value != null)
						{
							component.icon.sprite = value;
						}
						else
						{
							Image componentInChildren = gameObject.GetComponentInChildren<Image>();
							if (componentInChildren != null && Icons.TryGetValue(pace.Id, out var value2) && value2 != null)
							{
								componentInChildren.sprite = value2;
							}
						}
					}
					transform4.SetSiblingIndex(transform3.GetSiblingIndex());
					transform3 = transform4;
				}
				_buttons = true;
				_row = transform;
				Log.Info("pace: the hourglass has the slower years (" + transform.childCount + " speeds)");
			}

			private static Transform Row()
			{
				PowerButton[] array = Resources.FindObjectsOfTypeAll<PowerButton>();
				foreach (PowerButton powerButton in array)
				{
					if (!(powerButton == null) && !(powerButton.sizeButtons == null) && powerButton.gameObject.scene.IsValid())
					{
						Transform transform = Named(powerButton.sizeButtons.transform, "x1", 3);
						if (transform != null)
						{
							return transform.parent;
						}
					}
				}
				return null;
			}

			private static Transform Named(Transform t, string name, int depth)
			{
				for (int i = 0; i < t.childCount; i++)
				{
					Transform child = t.GetChild(i);
					if (child.name == name)
					{
						return child;
					}
					if (depth > 1)
					{
						Transform transform = Named(child, name, depth - 1);
						if (transform != null)
						{
							return transform;
						}
					}
				}
				return null;
			}

			internal static Transform ButtonForTest(string id)
			{
				return ((_row != null) ? _row : Row())?.Find(id);
			}

			internal static string DescribeForTest()
			{
				StringBuilder stringBuilder = new StringBuilder();
				PowerButton[] array = Resources.FindObjectsOfTypeAll<PowerButton>();
				foreach (PowerButton powerButton in array)
				{
					if (!(powerButton == null) && !(powerButton.sizeButtons == null) && powerButton.gameObject.scene.IsValid())
					{
						stringBuilder.Append(powerButton.name).Append(" > ").Append(powerButton.sizeButtons.name)
							.Append(" [");
						Transform transform = powerButton.sizeButtons.transform;
						for (int j = 0; j < transform.childCount && j < 16; j++)
						{
							stringBuilder.Append((j > 0) ? " " : "").Append(transform.GetChild(j).name).Append("(")
								.Append(transform.GetChild(j).childCount)
								.Append(")");
						}
						stringBuilder.Append("]; ");
					}
				}
				PowerClockButton[] array2 = Resources.FindObjectsOfTypeAll<PowerClockButton>();
				foreach (PowerClockButton powerClockButton in array2)
				{
					if (powerClockButton != null && powerClockButton.gameObject.scene.IsValid())
					{
						stringBuilder.Append("clock: ").Append(powerClockButton.name).Append(" under ")
							.Append((powerClockButton.transform.parent != null) ? powerClockButton.transform.parent.name : "-")
							.Append("; ");
					}
				}
				return stringBuilder.ToString();
			}

			private static Sprite MakeIcon(int of, string path)
			{
				bool[,] array = new bool[28, 28];
				Stamp(array, Hourglass, 4, 12);
				Stamp(array, One, 12, 10);
				Stamp(array, Slash, 14, 12);
				string[] glyph;
				switch (of)
				{
				default:
					glyph = Ten;
					break;
				case 5:
					glyph = Five;
					break;
				case 2:
					glyph = Two;
					break;
				}
				Stamp(array, glyph, 17, 15);
				Color32[] array2 = new Color32[784];
				for (int i = 0; i < 28; i++)
				{
					for (int j = 0; j < 28; j++)
					{
						Color32 color = new Color32(0, 0, 0, 0);
						if (array[j, i])
						{
							color = Fill;
						}
						else if (j > 0 && i > 0 && array[j - 1, i - 1])
						{
							color = Shadow;
						}
						array2[(27 - i) * 28 + j] = color;
					}
				}
				Texture2D texture2D = new Texture2D(28, 28, TextureFormat.RGBA32, mipChain: false);
				texture2D.filterMode = FilterMode.Point;
				texture2D.SetPixels32(array2);
				texture2D.Apply();
				byte[] pBytes = texture2D.EncodeToPNG();
				UnityEngine.Object.Destroy(texture2D);
				SpriteTextureLoader.addSprite(path, pBytes);
				return SpriteTextureLoader.getSprite(path);
			}

			private static void Stamp(bool[,] fill, string[] glyph, int x0, int y0)
			{
				for (int i = 0; i < glyph.Length; i++)
				{
					for (int j = 0; j < glyph[i].Length; j++)
					{
						if (glyph[i][j] == '#')
						{
							fill[x0 + j, y0 + i] = true;
						}
					}
				}
			}
		}

		public static readonly Pace[] Paces = new Pace[3]
		{
			new Pace
			{
				Id = "fp_years_2",
				Years = 0.5f,
				Of = 2,
				Name = "2× slower"
			},
			new Pace
			{
				Id = "fp_years_5",
				Years = 0.2f,
				Of = 5,
				Name = "5× slower"
			},
			new Pace
			{
				Id = "fp_years_10",
				Years = 0.1f,
				Of = 10,
				Name = "10× slower"
			}
		};

		private static double _worldNow;

		private static double _offset;

		private static double _lastWorld = double.NaN;

		private static object _world;

		private static Actor _possessed;

		private static string _lastSpeed;

		private static bool _settingApplied;

		private static bool _reported;

		internal const string OffsetKey = "fp_pace_clock";

		private static float _nextKeep;

		private static readonly Dictionary<Status, double> _statusEnds = new Dictionary<Status, double>();

		private static int _statusPrune;

		private static bool _kept;

		private static float _fireTime;

		private static float _fireStop;

		private static float _fireSpread;

		private static readonly Dictionary<TileTypeBase, int> _lava = new Dictionary<TileTypeBase, int>();

		private static bool _installed;

		private static bool _failed;

		private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		internal static Actor WatchedForTest;

		internal static int WatchedFired;

		public static float Factor { get; private set; } = 1f;

		public static double ActionNow
		{
			get
			{
				double num = _worldNow;
				try
				{
					if (World.world != null)
					{
						num = World.world.getCurWorldTime();
					}
				}
				catch
				{
				}
				if (Factor < 1f && num >= _lastWorld)
				{
					return num + _offset + (num - _lastWorld) * (1.0 / (double)Factor - 1.0);
				}
				return num + _offset;
			}
		}

		internal static bool Installed => _installed;

		public static Pace Find(string id)
		{
			if (id == null)
			{
				return null;
			}
			Pace[] paces = Paces;
			foreach (Pace pace in paces)
			{
				if (pace.Id == id)
				{
					return pace;
				}
			}
			return null;
		}

		public static float Since(double stamp)
		{
			double num = 0.0;
			try
			{
				num = World.world.getCurWorldTime();
			}
			catch
			{
			}
			return (float)((num - stamp) / (double)Factor);
		}

		public static void Tick(WorldBoxMod mod)
		{
			try
			{
				Ui.Ensure();
			}
			catch (Exception e)
			{
				Fail("the speed buttons", e);
			}
			if (World.world == null)
			{
				return;
			}
			WorldTimeScaleAsset worldTimeScaleAsset = null;
			try
			{
				worldTimeScaleAsset = Config.time_scale_asset;
			}
			catch
			{
			}
			float num = Find(worldTimeScaleAsset?.id)?.Years ?? 1f;
			if (num < 1f && !Install())
			{
				try
				{
					Config.setWorldSpeed("x1", pUpdateDebug: false);
				}
				catch
				{
				}
				num = 1f;
				Pace pace = null;
			}
			Clock(num);
			if (num != Factor)
			{
				RescaleStatuses(Factor, num);
				Compensate(num);
				Factor = num;
				KeepOffset();
			}
			if (num < 1f)
			{
				KeepStatuses();
			}
			Choice(mod, worldTimeScaleAsset);
		}

		private static void Clock(float factor)
		{
			object obj = null;
			double num = 0.0;
			try
			{
				obj = World.world.map_stats;
				num = World.world.getCurWorldTime();
			}
			catch
			{
			}
			if (obj != _world)
			{
				_settingApplied = false;
				_world = obj;
				_offset = SavedOffset();
				_statusEnds.Clear();
			}
			else if (double.IsNaN(_lastWorld) || num < _lastWorld)
			{
				_statusEnds.Clear();
			}
			else if (Factor < 1f)
			{
				_offset += (num - _lastWorld) * (1.0 / (double)Factor - 1.0);
				if (Time.unscaledTime >= _nextKeep)
				{
					KeepOffset();
				}
			}
			_lastWorld = num;
			_worldNow = num;
		}

		private static double SavedOffset()
		{
			string pResult = null;
			try
			{
				World.world?.map_stats?.custom_data?.get("fp_pace_clock", out pResult, null);
			}
			catch
			{
			}
			if (pResult == null || !double.TryParse(pResult, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || !(result > 0.0) || double.IsInfinity(result))
			{
				return 0.0;
			}
			return result;
		}

		private static void KeepOffset()
		{
			_nextKeep = Time.unscaledTime + 1f;
			if (_offset <= 0.0 || NetSession.Joined || WorldBoxMod.WorldLoading)
			{
				return;
			}
			try
			{
				MapStats mapStats = World.world?.map_stats;
				if (mapStats != null)
				{
					if (mapStats.custom_data == null)
					{
						mapStats.custom_data = new SaveCustomData();
					}
					mapStats.custom_data.set("fp_pace_clock", _offset.ToString("R", CultureInfo.InvariantCulture));
				}
			}
			catch
			{
			}
		}

		private static void Choice(WorldBoxMod mod, WorldTimeScaleAsset speed)
		{
			Settings settings = mod?.Settings;
			if (settings == null || speed == null || NetSession.Joined)
			{
				return;
			}
			Actor actor = null;
			try
			{
				actor = ControllableUnit._unit_main;
			}
			catch
			{
			}
			bool flag = actor != null && actor != _possessed;
			_possessed = actor;
			if (!_settingApplied && Config.game_loaded && !WorldBoxMod.WorldLoading)
			{
				_settingApplied = true;
				if (settings.WorldPace > 0)
				{
					Apply(settings.WorldPace);
				}
				_lastSpeed = Config.time_scale_asset?.id;
			}
			else if (flag && settings.WorldPace > 0 && speed.id == "x1")
			{
				Apply(settings.WorldPace);
				_lastSpeed = Config.time_scale_asset?.id;
			}
			else
			{
				if (speed.id == _lastSpeed)
				{
					return;
				}
				_lastSpeed = speed.id;
				int num = 0;
				for (int i = 0; i < Paces.Length; i++)
				{
					if (Paces[i].Id == speed.id)
					{
						num = i + 1;
					}
				}
				if (!flag && num != settings.WorldPace)
				{
					settings.WorldPace = num;
					try
					{
						settings.Save();
					}
					catch
					{
					}
				}
			}
		}

		public static void Apply(int index)
		{
			string text = ((index > 0 && index <= Paces.Length) ? Paces[index - 1].Id : "x1");
			try
			{
				Config.setWorldSpeed(text, pUpdateDebug: false);
			}
			catch (Exception e)
			{
				Fail("setting the speed", e);
			}
			_lastSpeed = text;
		}

		private static void KeepStatuses()
		{
			List<Status> list = World.world.statuses?.list;
			if (list == null)
			{
				return;
			}
			double worldNow = _worldNow;
			for (int i = 0; i < list.Count; i++)
			{
				Status status = list[i];
				if (status == null || status.is_finished)
				{
					continue;
				}
				double num = status._end_time;
				if (!_statusEnds.TryGetValue(status, out var value) || value != num)
				{
					double num2 = num - worldNow;
					if (num2 > 0.0)
					{
						num = (status._end_time = worldNow + num2 * (double)Factor);
					}
					_statusEnds[status] = num;
				}
			}
			if (++_statusPrune >= 600 || _statusEnds.Count > list.Count * 2 + 256)
			{
				Prune(list);
			}
		}

		private static void RescaleStatuses(float from, float to)
		{
			List<Status> list = null;
			try
			{
				list = World.world.statuses?.list;
			}
			catch
			{
			}
			if (list == null || from == to)
			{
				return;
			}
			double worldNow = _worldNow;
			for (int i = 0; i < list.Count; i++)
			{
				Status status = list[i];
				if (status != null && !status.is_finished)
				{
					double num = status._end_time - worldNow;
					if (num > 0.0)
					{
						status._end_time = worldNow + num * (double)(to / from);
					}
					if (to < 1f)
					{
						_statusEnds[status] = status._end_time;
					}
				}
			}
			if (to >= 1f)
			{
				_statusEnds.Clear();
			}
		}

		private static void Prune(List<Status> list)
		{
			_statusPrune = 0;
			HashSet<Status> hashSet = new HashSet<Status>(list);
			List<Status> list2 = new List<Status>();
			foreach (Status key in _statusEnds.Keys)
			{
				if (!hashSet.Contains(key) || key.is_finished)
				{
					list2.Add(key);
				}
			}
			foreach (Status item in list2)
			{
				_statusEnds.Remove(item);
			}
		}

		private static void Compensate(float factor)
		{
			try
			{
				SimGlobalAsset m = SimGlobals.m;
				if (m != null)
				{
					if (!_kept)
					{
						_fireTime = m.fire_time;
						_fireStop = m.fire_stop_time;
						_fireSpread = m.fire_spread_time;
					}
					m.fire_time = _fireTime * factor;
					m.fire_stop_time = _fireStop * factor;
					m.fire_spread_time = _fireSpread * factor;
				}
				if (!_kept)
				{
					foreach (TileType item in AssetManager.tiles.list)
					{
						if (item != null && item.lava_change_state_after > 0)
						{
							_lava[item] = item.lava_change_state_after;
						}
					}
					foreach (TopTileType item2 in AssetManager.top_tiles.list)
					{
						if (item2 != null && item2.lava_change_state_after > 0)
						{
							_lava[item2] = item2.lava_change_state_after;
						}
					}
				}
				foreach (KeyValuePair<TileTypeBase, int> item3 in _lava)
				{
					item3.Key.lava_change_state_after = ((factor >= 1f) ? item3.Value : Mathf.Max(1, Mathf.RoundToInt((float)item3.Value * factor)));
				}
				_kept = true;
			}
			catch (Exception e)
			{
				Fail("fire and lava timing", e);
			}
		}

		private static bool Install()
		{
			if (_installed)
			{
				return true;
			}
			if (_failed)
			{
				return false;
			}
			try
			{
				Hook(typeof(MapStats), "updateWorldTime", new Type[1] { typeof(float) }, 77, "UpdateWorldTime");
			}
			catch (Exception ex)
			{
				_failed = true;
				Log.Error("pace: the world's clock can't be slowed on this game version: " + ex.Message);
				WorldBoxMod.Instance?.Toast("Slower years don't work with this version of WorldBox.");
				return false;
			}
			_installed = true;
			try
			{
				Hook(typeof(Actor), "u7_checkAugmentationEffects", Type.EmptyTypes, 295, "CheckAugmentationEffects");
			}
			catch (Exception ex2)
			{
				Log.Error("pace: trait effects keep the slow clock: " + ex2.Message);
			}
			try
			{
				Hook(typeof(Actor), "isJustAttacked", Type.EmptyTypes, 24, "IsJustAttacked");
			}
			catch (Exception ex3)
			{
				Log.Error("pace: the deflect window keeps the slow clock: " + ex3.Message);
			}
			Log.Info("pace: slower years on (the world clock, trait effects, the deflect window)");
			return true;
		}

		private static void Hook(Type type, string name, Type[] args, int ilBytes, string replacement)
		{
			MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, args, null);
			int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
			if (num != ilBytes)
			{
				throw new InvalidOperationException(type.Name + "." + name + " has changed (" + num + " bytes of IL, not " + ilBytes + ")");
			}
			Detour.Apply(method, typeof(WorldPace).GetMethod(replacement, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
		}

		private static void UpdateWorldTime(MapStats self, float pElapsed)
		{
			if (Factor >= 1f)
			{
				self.world_time += pElapsed;
			}
			else
			{
				self.world_time += (double)pElapsed * (double)Factor;
			}
			int currentYear = Date.getCurrentYear();
			int currentMonth = Date.getCurrentMonth();
			if (self._last_year != currentYear)
			{
				World.world.updateObjectAge();
			}
			if (self._last_year != currentYear)
			{
				self._last_month = -1;
			}
			self._last_year = currentYear;
			self._last_month = currentMonth;
		}

		private static void CheckAugmentationEffects(Actor self)
		{
			if (self._update_done)
			{
				return;
			}
			float factor = Factor;
			if (World.world.getWorldTimeElapsedSince(self._timestamp_augmentation_effects) < 1f * factor)
			{
				return;
			}
			List<BaseAugmentationAsset> tempAugmentationList = Actor._tempAugmentationList;
			Dictionary<BaseAugmentationAsset, double> s_special_effect_augmentations_timers = self._s_special_effect_augmentations_timers;
			double value = (self._timestamp_augmentation_effects = World.world.getCurWorldTime());
			List<BaseAugmentationAsset> s_special_effect_augmentations = self._s_special_effect_augmentations;
			int count = s_special_effect_augmentations.Count;
			for (int i = 0; i < count; i++)
			{
				BaseAugmentationAsset baseAugmentationAsset = s_special_effect_augmentations[i];
				if (s_special_effect_augmentations_timers.TryGetValue(baseAugmentationAsset, out var value2))
				{
					if (World.world.getWorldTimeElapsedSince(value2) < baseAugmentationAsset.special_effect_interval * factor)
					{
						continue;
					}
					tempAugmentationList.Add(baseAugmentationAsset);
				}
				s_special_effect_augmentations_timers[baseAugmentationAsset] = value;
			}
			if (tempAugmentationList.Count != 0)
			{
				if (self == WatchedForTest)
				{
					WatchedFired += tempAugmentationList.Count;
				}
				int j = 0;
				for (int count2 = tempAugmentationList.Count; j < count2; j++)
				{
					tempAugmentationList[j].action_special_effect(self, self.current_tile);
				}
				Actor._tempAugmentationList.Clear();
			}
		}

		private static bool IsJustAttacked(Actor self)
		{
			return World.world.getWorldTimeElapsedSince(self.last_attack_timestamp) < 0.25f * Factor;
		}

		private static void Fail(string what, Exception e)
		{
			if (!_reported)
			{
				_reported = true;
				Log.Error("pace: " + what + ": " + e.Message);
			}
		}

		internal static void ForgetWorldForTest()
		{
			_world = null;
		}
	}
}
