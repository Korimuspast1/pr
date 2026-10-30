using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class StatueLookTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			bool quit = File.ReadAllText(path).Contains("quit");
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
			List<WorldTile> list = KingTest.GoodSites();
			List<(string race, City city, WorldTile at)> towns = new List<(string, City, WorldTile)>();
			Actor actor = null;
			int i = 0;
			string[] array = new string[4] { "human", "elf", "orc", "dwarf" };
			foreach (string text in array)
			{
				for (; i < list.Count; i++)
				{
					WorldTile worldTile = list[i];
					bool flag2 = true;
					foreach (var item5 in towns)
					{
						if (Mathf.Abs(item5.at.x - worldTile.x) < 40 && Mathf.Abs(item5.at.y - worldTile.y) < 40)
						{
							flag2 = false;
						}
					}
					if (!flag2)
					{
						continue;
					}
					Actor actor2 = KingTest.FoundKingdom(worldTile, text, 3, 0);
					if (actor2 != null)
					{
						if (actor == null)
						{
							actor = actor2;
						}
						towns.Add((text, actor2.city, worldTile));
						i++;
						break;
					}
				}
			}
			Log.Info(string.Format("STATUES {0} towns: {1}", towns.Count, string.Join(", ", towns.ConvertAll(((string race, City city, WorldTile at) x) => x.race))));
			if (actor == null)
			{
				Finish(quit);
				yield break;
			}
			ControllableUnit.setControllableCreature(actor);
			try
			{
				((BaseSimObject)actor).addStatusEffect("invincible", 9999f, pColorEffect: true);
			}
			catch
			{
			}
			yield return new WaitForSecondsRealtime(2.5f);
			_mod.HintUntil = 0f;
			_mod.Day.TimeOfDay = 0.32f;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			foreach (var item6 in towns)
			{
				string race = item6.race;
				City item = item6.city;
				WorldTile item2 = item6.at;
				Building standing = Place("statue", item2, 9, 0, item);
				Building fallen = Place("statue", item2, 9, 9, item);
				try
				{
					fallen?.makeRuins();
				}
				catch (Exception ex)
				{
					Log.Error("STATUES could not ruin: " + ex.Message);
				}
				yield return new WaitForSecondsRealtime(1.5f);
				(Building, string)[] array2 = new(Building, string)[2]
				{
					(standing, "standing"),
					(fallen, "ruin")
				};
				for (int num = 0; num < array2.Length; num++)
				{
					var (building, what) = array2[num];
					if (building != null)
					{
						Vector2 c = new Vector2((float)building.current_tile.x + 0.5f, (float)building.current_tile.y + 0.5f);
						string text2 = Models.BuildingFamily(building);
						Log.Info($"STATUES {race} {what}: {text2} at {building.current_tile.x},{building.current_tile.y}");
						(Vector2, float, string)[] array3 = new(Vector2, float, string)[2]
						{
							(new Vector2(5f, -9f), 14f, "far"),
							(new Vector2(-2.6f, -3.4f), 26f, "near")
						};
						for (int num2 = 0; num2 < array3.Length; num2++)
						{
							(Vector2, float, string) tuple2 = array3[num2];
							Vector2 item3 = tuple2.Item1;
							float item4 = tuple2.Item2;
							string view = tuple2.Item3;
							Vector2 debugEye = c + ((!(what == "ruin")) ? item3 : ((view == "far") ? new Vector2(4f, -7f) : new Vector2(2.5f, -4.5f)));
							_mod.DebugEye = debugEye;
							_mod.DebugEyeSet = true;
							_mod.SetYaw(Mathf.Atan2(c.y - debugEye.y, c.x - debugEye.x));
							_mod.SetPitchDegrees((what == "ruin") ? (-14f) : item4);
							yield return new WaitForSecondsRealtime(0.8f);
							_mod.SaveFrame("statue_" + race + "_" + what + "_" + view + ".png");
							ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "statue_" + race + "_" + what + "_" + view + "_screen.png"));
							yield return null;
						}
					}
				}
				_mod.DebugEyeSet = false;
			}
			Finish(quit);
		}

		private static Building Place(string id, WorldTile at, int dx, int dy, City city)
		{
			WorldTile tile = World.world.GetTile(at.x + dx, at.y + dy);
			if (tile == null)
			{
				return null;
			}
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item == null || !item.isAlive() || item.current_tile == null || Mathf.Abs(item.current_tile.x - tile.x) > 3 || Mathf.Abs(item.current_tile.y - tile.y) > 3)
				{
					continue;
				}
				try
				{
					if (!item.isKingdomCiv())
					{
						item.removeBuildingFinal();
					}
				}
				catch
				{
				}
			}
			try
			{
				Building building = World.world.buildings.addBuilding(id, tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				if (building == null)
				{
					return null;
				}
				building.completeConstruction();
				building.setKingdom(city.kingdom);
				city.listBuilding(building);
				return building;
			}
			catch (Exception ex)
			{
				Log.Error("STATUES " + id + ": " + ex.Message);
				return null;
			}
		}

		private static void Finish(bool quit)
		{
			PossessionHooks.ScriptedInput = Vector2.zero;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("STATUES DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
