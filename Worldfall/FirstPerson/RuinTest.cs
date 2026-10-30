using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class RuinTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private Actor _king;

		private static readonly string[] Town = new string[14]
		{
			"hall_{0}_0", "windmill_{0}_0", "watch_tower_{0}", "temple_{0}", "library_{0}", "barracks_{0}", "market_{0}", "well", "stockpile", "statue",
			"tent_{0}", "training_dummy", "beehive", "mine"
		};

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(path);
			bool quit = options.Contains("quit");
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
			int num = 0;
			string[] array = new string[4] { "human", "elf", "orc", "dwarf" };
			foreach (string arg in array)
			{
				for (int j = 0; j < 6; j++)
				{
					if (Models.Get($"ruin_house_{arg}_{j}") != null)
					{
						num++;
					}
				}
			}
			Log.Info(string.Format("RUINS world {0}x{1}, models {2}, {3} of 24 ruined houses", MapBox.width, MapBox.height, Models.Ready ? "ready" : "missing", num));
			foreach (WorldTile item in KingTest.GoodSites())
			{
				_king = KingTest.FoundKingdom(item, "human", 4, 0);
				if (_king != null)
				{
					break;
				}
			}
			if (_king == null)
			{
				Log.Error("RUINS could not found a kingdom");
				Application.Quit();
				yield break;
			}
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("RUINS first person did not start");
				Application.Quit();
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
			List<Vector2> taken = new List<Vector2>();
			string[] array2 = new string[4] { "human", "elf", "orc", "dwarf" };
			foreach (string text in array2)
			{
				if (!options.Contains("only") || options.Contains("only" + text))
				{
					Vector2? vector = FlatSite(taken);
					if (!vector.HasValue)
					{
						Log.Error("RUINS no flat open land left for " + text);
						continue;
					}
					taken.Add(vector.Value);
					yield return Run(text, Village(text, _king, vector.Value, text == "human"));
				}
			}
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("RUINS DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("RUINS --- " + name);
			while (true)
			{
				object current;
				try
				{
					if (!part.MoveNext())
					{
						break;
					}
					current = part.Current;
					goto IL_007b;
				}
				catch (Exception ex)
				{
					Log.Error("RUINS " + name + " failed: " + ex);
				}
				break;
				IL_007b:
				yield return current;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
		}

		private Vector2? FlatSite(List<Vector2> taken)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			if (terrainForTest == null || terrainForTest.Width == 0)
			{
				Log.Error("RUINS no terrain yet");
				return null;
			}
			Vector2? result = null;
			float num = float.MaxValue;
			for (int i = 26; i < terrainForTest.Height - 12; i += 3)
			{
				for (int j = 12; j < terrainForTest.Width - 56; j += 3)
				{
					bool flag = false;
					foreach (Vector2 item in taken)
					{
						if (Mathf.Abs(item.x - (float)j) < 64f && Mathf.Abs(item.y - (float)i) < 34f)
						{
							flag = true;
						}
					}
					if (flag)
					{
						continue;
					}
					float num2 = float.MaxValue;
					float num3 = float.MinValue;
					int num4 = 0;
					int num5 = 0;
					for (int k = -22; k <= 10; k += 2)
					{
						for (int l = -9; l <= 54; l += 2)
						{
							int num6 = (i + k) * terrainForTest.Width + j + l;
							if ((terrainForTest.Flags[num6] & 3) != 0)
							{
								if (l >= -2 && k >= -20 && k <= 9)
								{
									num4++;
								}
								else
								{
									num5++;
								}
							}
							else
							{
								num2 = Mathf.Min(num2, terrainForTest.Ground[num6]);
								num3 = Mathf.Max(num3, terrainForTest.Ground[num6]);
							}
						}
					}
					if (!(num2 > num3))
					{
						float num7 = (float)num4 * 6f + (float)num5 * 0.5f + (num3 - num2);
						if (num7 < num)
						{
							num = num7;
							result = new Vector2(j, i);
						}
					}
				}
			}
			if (result.HasValue)
			{
				Log.Info($"RUINS site {result.Value}: score {num:F1} (wet tiles x6 + rise)");
			}
			return result;
		}

		private static void Clear(Vector2 site)
		{
			int num = 0;
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item == null || !item.isAlive() || item.current_tile == null)
				{
					continue;
				}
				float num2 = (float)item.current_tile.x - site.x;
				float num3 = (float)item.current_tile.y - site.y;
				if (num2 < -14f || num2 > 64f || num3 < -28f || num3 > 14f)
				{
					continue;
				}
				try
				{
					if (!item.isKingdomCiv())
					{
						item.removeBuildingFinal();
						num++;
					}
				}
				catch
				{
				}
			}
			Log.Info($"RUINS cleared {num} plants and rocks");
		}

		private static Building Put(City city, string id, int x, int y)
		{
			WorldTile tile = World.world.GetTile(x, y);
			if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block)
			{
				return null;
			}
			try
			{
				if (tile.building != null)
				{
					tile.building.startDestroyBuilding();
				}
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
				Log.Error("RUINS building " + id + ": " + ex.Message);
				return null;
			}
		}

		private IEnumerator Village(string race, Actor king, Vector2 site, bool town)
		{
			City city = king.city;
			Clear(site);
			List<Building> placed = new List<Building>();
			int sx = (int)site.x;
			int street = (int)site.y;
			for (int i = 0; i < 6; i++)
			{
				Building building = Put(city, $"house_{race}_{i}", sx + i * 8, street + 5);
				if (building != null)
				{
					placed.Add(building);
				}
			}
			List<string> list = new List<string> { "hall_" + race + "_0" };
			if (town)
			{
				list.AddRange(Town);
			}
			for (int j = 0; j < list.Count; j++)
			{
				if (j <= 0 || !(string.Format(list[j], race) == string.Format(list[0], race)))
				{
					int num = ((j >= 7) ? 1 : 0);
					int num2 = ((num == 0) ? j : (j - 7));
					Building building2 = Put(city, string.Format(list[j], race), sx + num2 * 8 + ((num != 0 || j != 0) ? 2 : 0), street - 5 - num * 10);
					if (building2 != null)
					{
						placed.Add(building2);
					}
				}
			}
			Log.Info(string.Format("RUINS {0} at {1}: placed {2} buildings: {3}", race, site, placed.Count, string.Join(", ", placed.ConvertAll((Building building4) => (building4?.asset == null) ? "(gone)" : building4.asset.id))));
			List<Building> empty = new List<Building>();
			if (race == "elf")
			{
				(int, int, int)[] array = new(int, int, int)[3]
				{
					(1, 50, 5),
					(3, 58, 5),
					(4, 54, -5)
				};
				for (int num3 = 0; num3 < array.Length; num3++)
				{
					(int, int, int) tuple = array[num3];
					int item = tuple.Item1;
					int item2 = tuple.Item2;
					int item3 = tuple.Item3;
					Building building3 = Put(city, $"house_elf_{item}", sx + item2, street + item3);
					if (building3 != null)
					{
						empty.Add(building3);
					}
				}
			}
			Settings s = _mod.Settings;
			int savedMode = s.TimeMode;
			s.TimeMode = 1;
			_mod.ForceRelight();
			try
			{
				king.setCurrentTilePosition(World.world.GetTile(sx - 6, street));
			}
			catch
			{
			}
			Vector2 streetEye = new Vector2((float)sx - 7.5f, (float)street + 0.5f);
			_mod.DebugEye = streetEye;
			_mod.DebugEyeSet = true;
			_mod.SetYaw(0f);
			_mod.SetPitchDegrees(-2f);
			yield return new WaitForSecondsRealtime(2.5f);
			_mod.SaveFrame("ruin_" + race + "_before.png");
			int num4 = 0;
			foreach (Building item4 in placed)
			{
				try
				{
					item4.makeRuins();
					num4++;
				}
				catch (Exception ex)
				{
					Log.Error("RUINS could not ruin " + item4.asset.id + ": " + ex.Message);
				}
			}
			Log.Info($"RUINS {race}: {num4} buildings ruined");
			yield return new WaitForSecondsRealtime(3f);
			_mod.SaveFrame("ruin_" + race + "_street.png");
			_mod.SceneForTest.DumpNear(new Vector2((float)sx + 24f, street), 40f);
			_mod.DebugEye = new Vector2((float)sx + 22f, (float)street + 0.5f);
			_mod.SetYaw(MathF.PI / 2f);
			yield return new WaitForSecondsRealtime(1f);
			_mod.SaveFrame("ruin_" + race + "_houses.png");
			_mod.SetYaw(-MathF.PI / 2f);
			yield return new WaitForSecondsRealtime(1f);
			_mod.SaveFrame("ruin_" + race + "_hall.png");
			if (town)
			{
				_mod.DebugEye = new Vector2((float)sx + 22f, (float)street - 10f);
				_mod.SetYaw(-MathF.PI / 2f);
				_mod.SetPitchDegrees(-6f);
				yield return new WaitForSecondsRealtime(1f);
				_mod.SaveFrame("ruin_" + race + "_town.png");
			}
			foreach (Building b in placed)
			{
				if (b != null && b.isAlive())
				{
					Vector2 vector = new Vector2((float)b.current_tile.x + 0.5f, (float)b.current_tile.y + 0.5f);
					bool flag = vector.y > (float)street;
					float num5 = ((b.asset.id.StartsWith("hall", StringComparison.Ordinal) || b.asset.id.StartsWith("temple", StringComparison.Ordinal)) ? 11f : 7.5f);
					Vector2 debugEye = vector + new Vector2(num5 * 0.55f, flag ? (0f - num5) : ((0f - num5) * 0.9f));
					if (!flag)
					{
						debugEye = vector + new Vector2(num5 * 0.55f, (0f - num5) * 0.9f);
					}
					_mod.DebugEye = debugEye;
					_mod.SetYaw(Mathf.Atan2(vector.y + 1f - debugEye.y, vector.x - debugEye.x));
					_mod.SetPitchDegrees(-8f);
					yield return new WaitForSecondsRealtime(0.6f);
					_mod.SaveFrame("ruin_" + race + "_" + b.asset.id + ".png");
				}
			}
			_mod.DebugEye = streetEye;
			_mod.SetYaw(0f);
			_mod.SetPitchDegrees(-2f);
			s.TimeMode = 2;
			_mod.ForceRelight();
			yield return new WaitForSecondsRealtime(1.5f);
			_mod.SaveFrame("ruin_" + race + "_golden.png");
			s.TimeMode = 3;
			_mod.ForceRelight();
			yield return new WaitForSecondsRealtime(1.5f);
			_mod.SaveFrame("ruin_" + race + "_night.png");
			s.TimeMode = 1;
			_mod.ForceRelight();
			Building house = null;
			foreach (Building item5 in placed)
			{
				if (item5 != null && item5.isAlive() && item5.asset.id == "house_" + race + "_2")
				{
					house = item5;
				}
			}
			if (house != null)
			{
				_mod.DebugEyeSet = false;
				Vector2 vector2 = new Vector2((float)house.current_tile.x + 0.5f, (float)house.current_tile.y - 2.5f);
				try
				{
					king.setCurrentTilePosition(World.world.GetTile((int)vector2.x, (int)vector2.y));
				}
				catch
				{
				}
				yield return new WaitForSecondsRealtime(1f);
				_mod.SetYaw(MathF.PI / 2f);
				_mod.SetPitchDegrees(-4f);
				Vector2 from = king.current_position;
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				yield return new WaitForSecondsRealtime(2.2f);
				PossessionHooks.ScriptedInput = Vector2.zero;
				Vector2 current_position = king.current_position;
				int num6 = 0;
				foreach (Collider item6 in Colliders.Near)
				{
					if (item6.Shape != null)
					{
						num6++;
					}
				}
				Log.Info($"RUINS {race}: walked from {from} to {current_position} into {house.asset.id} at {house.current_tile.x},{house.current_tile.y} ({current_position.y - (float)house.current_tile.y:F1} tiles past its anchor), {num6} model colliders near");
				yield return new WaitForSecondsRealtime(0.5f);
				_mod.SaveFrame("ruin_" + race + "_inside.png");
				_mod.SetYaw(2.7707963f);
				yield return new WaitForSecondsRealtime(0.5f);
				_mod.SaveFrame("ruin_" + race + "_inside_left.png");
				ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "ruin_" + race + "_screen.png"));
				yield return new WaitForSecondsRealtime(0.5f);
			}
			if (empty.Count > 0)
			{
				_mod.DebugEyeSet = true;
				_mod.DebugEye = new Vector2((float)sx + 54.5f, (float)street - 13f);
				_mod.SetYaw(MathF.PI / 2f);
				_mod.SetPitchDegrees(-4f);
				yield return new WaitForSecondsRealtime(1f);
				_mod.SaveFrame("ruin_elf_lived.png");
				int n = 0;
				foreach (Building item7 in empty)
				{
					try
					{
						item7.makeAbandoned();
						n++;
					}
					catch (Exception ex2)
					{
						Log.Error("RUINS abandon: " + ex2.Message);
					}
				}
				yield return new WaitForSecondsRealtime(1.5f);
				_mod.SaveFrame("ruin_elf_abandoned.png");
				Log.Info(string.Format("RUINS abandoned {0} houses: {1}", n, string.Join(", ", empty.ConvertAll((Building building4) => ((building4?.asset != null) ? building4.asset.id : "(gone)") + (building4.isAbandoned() ? " abandoned" : "") + (building4.isRuin() ? " ruin" : "")))));
				_mod.SceneForTest.DumpNear(new Vector2((float)sx + 54f, street), 8f);
				_mod.DebugEyeSet = false;
			}
			s.TimeMode = savedMode;
			_mod.ForceRelight();
			yield return new WaitForSecondsRealtime(1f);
		}
	}
}
