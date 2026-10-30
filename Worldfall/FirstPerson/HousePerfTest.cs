using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class HousePerfTest : MonoBehaviour
	{
		private const float Stretch = 8f;

		private WorldBoxMod _mod;

		private Actor _you;

		private readonly List<string> _summary = new List<string>();

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(path);
			bool quit = options.Contains("quit");
			int slot = 23;
			int num = options.IndexOf("map", StringComparison.Ordinal);
			if (num >= 0)
			{
				int i;
				for (i = num + 3; i < options.Length && char.IsDigit(options[i]); i++)
				{
				}
				if (i > num + 3)
				{
					int.TryParse(options.Substring(num + 3, i - num - 3), out slot);
				}
			}
			while (!Config.game_loaded || World.world == null || World.world.units == null)
			{
				yield return null;
			}
			for (float t = 0f; t < 60f; t += 1f)
			{
				bool flag = false;
				try
				{
					flag = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag && t >= 4f)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(1f);
			}
			Config.autosaves = false;
			_mod = WorldBoxMod.Instance;
			string text = Path.Combine(Path.Combine(Path.Combine(Application.persistentDataPath, "saves"), "save" + slot), "map.wbox");
			bool loaded = false;
			if (File.Exists(text))
			{
				try
				{
					SaveManager.loadMapFromBytes(File.ReadAllBytes(text));
					loaded = true;
				}
				catch (Exception ex)
				{
					Log.Error("HOUSEPERF could not load " + text + ": " + ex.Message);
				}
			}
			if (loaded)
			{
				for (float t = 0f; t < 90f; t += 0.5f)
				{
					yield return new WaitForSecondsRealtime(0.5f);
					bool flag2 = true;
					try
					{
						flag2 = SmoothLoader.isLoading();
					}
					catch
					{
					}
					if (!flag2 && t > 3f)
					{
						break;
					}
				}
				Config.autosaves = false;
			}
			Config.paused = false;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Log.Info(string.Format("HOUSEPERF world: {0}, {1}x{2} tiles, {3} creatures; settling 8 s", loaded ? ("save " + slot) : ("the starting map (no save " + slot + ")"), MapBox.width, MapBox.height, World.world.units.getSimpleList().Count));
			yield return new WaitForSecondsRealtime(8f);
			City town;
			Building house = PickHouse(out _you, out town);
			if (house == null || _you == null)
			{
				Log.Error("HOUSEPERF no two-storey human house with a grown-up of its town to take it");
				Finish(quit);
				yield break;
			}
			Homes.SetOwner(house, _you);
			Log.Info($"HOUSEPERF house {house.asset.id} at {house.current_position} in {town?.name}, you {WorldBoxMod.Describe(_you)}");
			ControllableUnit.setControllableCreature(_you);
			yield return new WaitForSecondsRealtime(4f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("HOUSEPERF first person did not start");
				Finish(quit);
				yield break;
			}
			_mod.HintUntil = 0f;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Settings s = _mod.Settings;
			int savedRate = Application.targetFrameRate;
			int savedSync = QualitySettings.vSyncCount;
			int savedWindows = s.WindowView;
			if (!options.Contains("capped"))
			{
				Application.targetFrameRate = 1000;
				QualitySettings.vSyncCount = 0;
			}
			Log.Info(string.Format("HOUSEPERF settings: {0}, {1}x{2}, fov {3}, draw {4:F0}, detail {5:F0}, creatures {6:F0}, ", s.GpuRender ? "GPU" : "CPU", _mod.Renderer.Width, _mod.Renderer.Height, s.FieldOfView, s.DrawDistance, s.DetailDistance, s.CreatureDistance) + string.Format("shadows {0}, window view {1}, finish {2}, time mode {3}; {4}, {5}", s.Shadows, s.WindowView, (s.GpuLook == 0) ? "classic" : "painterly", s.TimeMode, SystemInfo.processorType.Trim(), SystemInfo.graphicsDeviceName));
			if (!HouseWorld.Of(house, out var p) || HousePlan.Of(p.Model) == null)
			{
				Put(_you, house.current_position + new Vector2(0f, -4.5f));
				_mod.SetYaw(MathF.PI / 2f);
				for (float t = 0f; t < 4f; t += 0.2f)
				{
					if (HouseWorld.Of(house, out p))
					{
						break;
					}
					yield return new WaitForSecondsRealtime(0.2f);
				}
			}
			HousePlan housePlan = (HouseWorld.Of(house, out p) ? HousePlan.Of(p.Model) : null);
			if (housePlan == null)
			{
				Log.Error("HOUSEPERF the house isn't drawn / measured");
				Finish(quit);
				yield break;
			}
			float num2 = (p.Mirror ? (0f - p.Scale) : p.Scale);
			Vector2 vector = new Vector2(p.X + housePlan.Door.X * num2, p.Y + housePlan.Door.Y * p.Scale);
			Vector2 normalized = new Vector2(housePlan.Door.OutX * (p.Mirror ? (-1f) : 1f), housePlan.Door.OutY).normalized;
			Log.Info($"HOUSEPERF {p.Model.Name}: {housePlan.Storeys.Count} storeys, {housePlan.Windows.Count} windows");
			Put(_you, vector + normalized * 1.6f);
			float outYaw = Mathf.Atan2(normalized.y, normalized.x);
			_mod.SetYaw(outYaw);
			_mod.SetPitchDegrees(0f);
			yield return new WaitForSecondsRealtime(2f);
			yield return Outdoors("street, turning", outYaw, turning: true);
			yield return Outdoors("street, facing out", outYaw, turning: false);
			_mod.SetYaw(outYaw + MathF.PI);
			float t2 = Time.unscaledTime;
			while (Time.unscaledTime - t2 < 8f && (!_mod.DoorRoom.Inside || !_mod.DoorRoom.Fitted || _mod.DoorRoom.House != house))
			{
				yield return new WaitForSecondsRealtime(0.1f);
			}
			_mod.EnterHouseForTest(house);
			yield return new WaitForSecondsRealtime(2f);
			if (!_mod.Interior.Inside)
			{
				Log.Error("HOUSEPERF could not get in");
				Finish(quit);
				yield break;
			}
			Interior room = _mod.Interior.Room;
			Log.Info($"HOUSEPERF in: {room.Meshes.Count} models, {room.Windows.Count} windows, {room.Lights.Count} lights, {((room.Fit == null) ? 1 : room.Fit.StoreyZ.Length)} storeys, fitted {_mod.Interior.Fitted}");
			float spawnYaw = _mod.EyeYawForTest;
			float windowYaw = float.NaN;
			float wallYaw = float.NaN;
			for (int j = 0; j < 24; j++)
			{
				float t = spawnYaw + (float)j * MathF.PI * 2f / 24f;
				_mod.SetYaw(t);
				yield return null;
				yield return null;
				bool windowInViewForTest = _mod.WindowInViewForTest;
				if (windowInViewForTest && float.IsNaN(windowYaw))
				{
					windowYaw = t;
				}
				if (!windowInViewForTest && float.IsNaN(wallYaw))
				{
					wallYaw = t;
				}
			}
			Log.Info("HOUSEPERF window in view from " + (float.IsNaN(windowYaw) ? "nowhere" : (windowYaw * 57.29578f).ToString("F0")) + " deg, none from " + (float.IsNaN(wallYaw) ? "nowhere" : (wallYaw * 57.29578f).ToString("F0")) + " deg");
			float reach = WorldBoxMod.WindowReachTiles;
			int every = WorldBoxMod.WindowEvery;
			WorldBoxMod.WindowReachTiles = 0f;
			WorldBoxMod.WindowEvery = 1;
			yield return Indoors("room, turning, window view whole (as 0.9.6)", spawnYaw, turning: true);
			if (!float.IsNaN(windowYaw))
			{
				yield return Indoors("room, facing a window, window view whole (as 0.9.6)", windowYaw, turning: false, "houseperf_window_whole.png");
			}
			WorldBoxMod.WindowEvery = every;
			foreach (float item in Reaches(options, reach))
			{
				float t = (WorldBoxMod.WindowReachTiles = item);
				yield return Indoors($"room, turning, window view {t:F0} tiles, gathered every {every}", spawnYaw, turning: true);
				if (!float.IsNaN(windowYaw))
				{
					yield return Indoors($"room, facing a window, window view {t:F0} tiles, gathered every {every}", windowYaw, turning: false, $"houseperf_window_{t:F0}.png");
				}
			}
			WorldBoxMod.WindowReachTiles = reach;
			WorldBoxMod.WindowEvery = ((every != 1) ? 1 : 2);
			yield return Indoors($"room, turning, window view {reach:F0} tiles, gathered every {WorldBoxMod.WindowEvery}", spawnYaw, turning: true);
			if (!float.IsNaN(windowYaw))
			{
				yield return Indoors($"room, facing a window, window view {reach:F0} tiles, gathered every {WorldBoxMod.WindowEvery}", windowYaw, turning: false);
			}
			WorldBoxMod.WindowEvery = every;
			if (!float.IsNaN(wallYaw))
			{
				yield return Indoors("room, facing a wall", wallYaw, turning: false, "houseperf_wall.png");
			}
			s.WindowView = 0;
			yield return Indoors("room, windows off, turning", spawnYaw, turning: true);
			if (!float.IsNaN(windowYaw))
			{
				yield return Indoors("room, windows off, facing a window", windowYaw, turning: false);
			}
			s.WindowView = savedWindows;
			_mod.LeaveRoomForTest();
			yield return new WaitForSecondsRealtime(1f);
			Application.targetFrameRate = savedRate;
			QualitySettings.vSyncCount = savedSync;
			foreach (string item2 in _summary)
			{
				Log.Info("HOUSEPERF summary " + item2);
			}
			Finish(quit);
		}

		private IEnumerator Outdoors(string what, float yaw, bool turning)
		{
			yield return new WaitForSecondsRealtime(1f);
			List<float> dts = new List<float>();
			float t0 = Time.unscaledTime;
			while (Time.unscaledTime - t0 < 8f)
			{
				if (turning)
				{
					_mod.SetYaw(yaw + (Time.unscaledTime - t0) * 0.8f);
				}
				Feed();
				yield return null;
				dts.Add(Time.unscaledDeltaTime * 1000f);
			}
			if (turning)
			{
				_mod.SetYaw(yaw);
			}
			_summary.Add(what + ": " + Stats(dts));
			Log.Info("HOUSEPERF " + what + ": " + Stats(dts));
		}

		private IEnumerator Indoors(string what, float yaw, bool turning, string png = null)
		{
			_mod.SetYaw(yaw);
			_mod.SetPitchDegrees(0f);
			yield return new WaitForSecondsRealtime(1f);
			if (png != null)
			{
				_mod.SaveFrame(png);
			}
			List<float> dts = new List<float>();
			RoomClock.TestStart();
			SceneClock.ResetWindow();
			SceneClock.SampleEveryFrame = true;
			float t0 = Time.unscaledTime;
			while (Time.unscaledTime - t0 < 8f && _mod.Interior.Inside)
			{
				if (turning)
				{
					_mod.SetYaw(yaw + (Time.unscaledTime - t0) * 0.8f);
				}
				Feed();
				yield return null;
				dts.Add(Time.unscaledDeltaTime * 1000f);
			}
			SceneClock.SampleEveryFrame = false;
			string text = what + ": " + Stats(dts) + "; " + RoomClock.TestLine() + "; " + Split();
			_summary.Add(text);
			Log.Info("HOUSEPERF " + text);
		}

		private static List<float> Reaches(string options, float standard)
		{
			List<float> list = new List<float>();
			int num = options.IndexOf("reach=", StringComparison.Ordinal);
			if (num >= 0)
			{
				int i;
				for (i = num + 6; i < options.Length && (char.IsDigit(options[i]) || options[i] == ','); i++)
				{
				}
				string[] array = options.Substring(num + 6, i - num - 6).Split(',');
				for (int j = 0; j < array.Length; j++)
				{
					if (float.TryParse(array[j], out var result))
					{
						list.Add(result);
					}
				}
			}
			if (list.Count == 0)
			{
				list.Add(standard);
				if (standard != 48f)
				{
					list.Add(48f);
				}
			}
			return list;
		}

		private static string Split()
		{
			SceneClock.Split split = SceneClock.Snapshot();
			if (split.Passes == 0)
			{
				return "no scene gathered";
			}
			return $"scene: units {split[SceneClock.B.Units]:F2} (track {split[SceneClock.B.Track]:F2}, people {split[SceneClock.B.People]:F2}), buildings {split[SceneClock.B.Buildings]:F2}, far props {split[SceneClock.B.FarProps]:F2}, fx {split[SceneClock.B.Fx]:F2}, rest {split[SceneClock.B.Rest]:F2}, crowd {split[SceneClock.B.Crowd]:F2}, begin {split[SceneClock.B.Begin]:F2} ms; " + $"{split[SceneClock.N.Visited]:F0} visited, {split[SceneClock.N.Tracked]:F0} tracked, {split[SceneClock.N.Behind]:F0} behind, people {split.People:F0}";
		}

		private void Feed()
		{
			if (Time.frameCount % 120 != 0)
			{
				return;
			}
			try
			{
				if (_you != null && _you.isAlive())
				{
					_you.setNutrition(_you.getMaxNutrition());
				}
			}
			catch
			{
			}
		}

		private static string Stats(List<float> dts)
		{
			if (dts.Count == 0)
			{
				return "no frames";
			}
			List<float> list = new List<float>(dts);
			list.Sort();
			float num = 0f;
			foreach (float dt in dts)
			{
				num += dt;
			}
			float num2 = num / (float)dts.Count;
			return $"{1000f / num2:F1} fps, {num2:F1} ms mean, p50 {list[list.Count / 2]:F1}, p95 {list[Mathf.Min(list.Count - 1, list.Count * 95 / 100)]:F1} ms ({dts.Count} frames)";
		}

		private static Building PickHouse(out Actor you, out City town)
		{
			you = null;
			town = null;
			Building result = null;
			int num = -1;
			List<Actor> simpleList = World.world.units.getSimpleList();
			foreach (City item in World.world.cities.list)
			{
				if (item == null || !item.isAlive())
				{
					continue;
				}
				WorldTile worldTile = null;
				try
				{
					worldTile = item.getTile();
				}
				catch
				{
				}
				if (worldTile == null)
				{
					continue;
				}
				Vector2 vector = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f);
				int num2 = 0;
				Actor actor = null;
				foreach (Actor item2 in simpleList)
				{
					if (item2 == null || !item2.isAlive() || (item2.current_position - vector).sqrMagnitude > 225f)
					{
						continue;
					}
					num2++;
					if (actor == null && item2.city == item && People.IsHumanoid(item2) && !item2.is_inside_building && !item2.is_inside_boat && HouseInterior.HouseAt(item2) == null)
					{
						bool flag = false;
						try
						{
							flag = item2.isAdult() && item2.asset.id.Contains("human");
						}
						catch
						{
						}
						if (flag)
						{
							actor = item2;
						}
					}
				}
				if (actor == null || num2 <= num)
				{
					continue;
				}
				Building building = null;
				float num3 = float.MaxValue;
				foreach (Building simple in World.world.buildings.getSimpleList())
				{
					if (simple == null || !simple.isAlive() || simple.city != item || simple.asset == null)
					{
						continue;
					}
					string id = simple.asset.id;
					if ((id.StartsWith("house_human_3") || id.StartsWith("house_human_4") || id.StartsWith("house_human_5")) && HouseInterior.WalksIn(simple.asset) && HouseInterior.IsEnterable(simple))
					{
						float sqrMagnitude = (simple.current_position - vector).sqrMagnitude;
						if (sqrMagnitude < num3)
						{
							num3 = sqrMagnitude;
							building = simple;
						}
					}
				}
				if (building != null)
				{
					num = num2;
					result = building;
					you = actor;
					town = item;
				}
			}
			return result;
		}

		private static void Put(Actor a, Vector2 at)
		{
			try
			{
				a.current_position = at;
				a.dirty_current_tile = true;
			}
			catch
			{
			}
		}

		private static void Finish(bool quit)
		{
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("HOUSEPERF DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
