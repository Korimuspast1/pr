using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FirstPerson.OneWorld
{
	internal sealed class OneWorldPerfTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private IEnumerator Start()
		{
			string text = File.ReadAllText(Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest"));
			bool quit = text.Contains("quit");
			int slot = 24;
			int num = text.IndexOf("map", StringComparison.Ordinal);
			if (num >= 0)
			{
				int i;
				for (i = num + 3; i < text.Length && char.IsDigit(text[i]); i++)
				{
				}
				if (i > num + 3)
				{
					int.TryParse(text.Substring(num + 3, i - num - 3), out slot);
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
			string text2 = Path.Combine(Path.Combine(Path.Combine(Application.persistentDataPath, "saves"), "save" + slot), "map.wbox");
			bool loaded = false;
			if (File.Exists(text2))
			{
				try
				{
					SaveManager.loadMapFromBytes(File.ReadAllBytes(text2));
					loaded = true;
				}
				catch (Exception ex)
				{
					Log.Error("OWPERF could not load " + text2 + ": " + ex.Message);
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
			}
			Config.autosaves = false;
			Config.paused = false;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Log.Info(string.Format("OWPERF world: {0}, {1}x{2} tiles, {3} creatures", loaded ? ("save " + slot) : ("the starting map (no save " + slot + ")"), MapBox.width, MapBox.height, World.world.units.getSimpleList().Count));
			City city = Biggest();
			Building house = null;
			if (city != null)
			{
				foreach (Building building in city.buildings)
				{
					if (building != null && building.asset != null && building.asset.type == "type_house" && building.isUsable())
					{
						house = building;
						break;
					}
				}
				Look(city.getTile()?.posV3 ?? Vector3.zero, 20f);
			}
			Log.Info(string.Format("OWPERF over {0} ({1} buildings), a house to look into: {2}", city?.name ?? "no town", city?.buildings.Count ?? 0, house?.asset?.id ?? "none"));
			yield return new WaitForSecondsRealtime(6f);
			Settings s = _mod.Settings;
			int houses = s.MapHouses;
			int night = s.MapNight;
			bool look = s.LookInside;
			string[] array = new string[2] { "x1", "x5" };
			foreach (string speed in array)
			{
				Config.setWorldSpeed(speed, pUpdateDebug: false);
				bool[] array2 = new bool[2] { false, true };
				foreach (bool on in array2)
				{
					float[] array3 = new float[2] { 0.5f, 0f };
					for (int l = 0; l < array3.Length; l++)
					{
						float t = array3[l];
						s.MapHouses = (on ? 2 : 0);
						s.MapNight = (on ? 1 : 0);
						s.LookInside = on;
						InsideSpots.On = on;
						MapPeek.ForceForTest = ((on && t < 0.25f) ? house : null);
						_mod.Day.TimeOfDay = t;
						yield return new WaitForSecondsRealtime(3f);
						yield return Measure(speed + " " + (on ? "on " : "off") + " " + ((t < 0.25f) ? "night" : "day  "), 10f);
					}
				}
			}
			s.MapHouses = houses;
			s.MapNight = night;
			s.LookInside = look;
			InsideSpots.On = true;
			MapPeek.ForceForTest = null;
			Config.setWorldSpeed("x1", pUpdateDebug: false);
			Log.Info("OWPERF DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator Measure(string tag, float seconds)
		{
			float t0 = Time.unscaledTime;
			float worst = 0f;
			float cost = 0f;
			float occ = 0f;
			float inside = 0f;
			float housesMs = 0f;
			float draw = 0f;
			int frames = 0;
			while (Time.unscaledTime - t0 < seconds)
			{
				yield return null;
				frames++;
				worst = Mathf.Max(worst, Time.unscaledDeltaTime);
				cost += MapClock.CostMs;
				occ += MapClock.OccupancyMs;
				inside += MapClock.InsideMs;
				housesMs += MapClock.HousesMs;
				draw += MapLights.DrawMs;
			}
			float num = Time.unscaledTime - t0;
			float num2 = Mathf.Max(1, frames);
			Log.Info($"OWPERF {tag}: {(float)frames / num:F1} fps (worst frame {worst * 1000f:F0} ms); one world {cost / num2:F3} ms a frame (occupancy {occ / num2:F3}, places {inside / num2:F3}, houses {housesMs / num2:F3}, lights' draw {draw / num2:F3}); " + $"indoors {HouseOccupancy.IndoorsCount}, slice {HouseOccupancy.SliceForTest}, layouts {InsideSpots.LayoutsForTest}, placed {InsideSpots.PlacedForTest}, peek {MapPeek.PeopleForTest} drawn, smoking {MapSmoke.SmokingForTest}");
		}

		private static City Biggest()
		{
			City city = null;
			try
			{
				foreach (City item in World.world.cities.list)
				{
					if (item != null && item.isAlive() && (city == null || item.buildings.Count > city.buildings.Count))
					{
						city = item;
					}
				}
			}
			catch
			{
			}
			return city;
		}

		private static void Look(Vector3 at, float zoom)
		{
			try
			{
				Camera camera = World.world.camera;
				camera.transform.position = new Vector3(at.x, at.y, camera.transform.position.z);
				camera.orthographicSize = zoom;
				World.world.move_camera?.setTargetZoom(zoom);
				World.world.setZoomOrthographic(zoom);
			}
			catch
			{
			}
		}
	}
}
