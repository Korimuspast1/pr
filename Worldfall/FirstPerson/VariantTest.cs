using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class VariantTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private Actor _king;

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
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			string[] array = new string[4] { "human", "elf", "orc", "dwarf" };
			foreach (string text in array)
			{
				string[] array2;
				for (int j = 0; j < 6; j++)
				{
					array2 = new string[2] { "_b", "_c" };
					foreach (string arg in array2)
					{
						string text2 = $"house_{text}_{j}{arg}";
						if (Models.Get(text2) != null)
						{
							num++;
						}
						if (Models.Get(text2 + "_m") != null && Models.Get(text2 + "_f") != null)
						{
							num2++;
						}
						if (Models.Get("ruin_" + text2) != null)
						{
							num3++;
						}
					}
				}
				array2 = new string[2] { "", "_b" };
				foreach (string text3 in array2)
				{
					if (Models.Get("tent_" + text + text3) != null)
					{
						num4++;
					}
					if (Models.Get("ruin_tent_" + text + text3) != null)
					{
						num3++;
					}
				}
			}
			array = new string[9] { "int_v_inglenook", "int_v_loft", "int_v_shrine", "int_v_fountain", "int_v_bigforge", "int_v_skullpole", "int_v_hoodhearth", "int_v_spiral", "int_v_bedroll" };
			for (int i = 0; i < array.Length; i++)
			{
				if (Models.Get(array[i]) != null)
				{
					num5++;
				}
			}
			Log.Info(string.Format("VARIANTS world {0}x{1}, models {2}: {3} of 48 house variants ({4} with distance versions), {5} of 8 tents, {6} of 56 ruins, {7} of 9 room key pieces", MapBox.width, MapBox.height, Models.Ready ? "ready" : "missing", num, num2, num4, num3, num5));
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
				Log.Error("VARIANTS could not found a kingdom");
				Application.Quit();
				yield break;
			}
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("VARIANTS first person did not start");
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
			if (options.Contains("site2"))
			{
				Vector2? vector = HillSite(taken);
				if (vector.HasValue)
				{
					taken.Add(vector.Value);
				}
			}
			string[] array3 = new string[4] { "human", "elf", "orc", "dwarf" };
			foreach (string text4 in array3)
			{
				if (!options.Contains("only") || options.Contains("only" + text4))
				{
					Vector2? vector2 = HillSite(taken);
					if (!vector2.HasValue)
					{
						Log.Error("VARIANTS no land left for " + text4);
						continue;
					}
					taken.Add(vector2.Value);
					yield return Run(text4, Village(text4, _king, vector2.Value, !options.Contains("noruin"), !options.Contains("norooms"), options.Contains("doors")));
				}
			}
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("VARIANTS DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("VARIANTS --- " + name);
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
					Log.Error("VARIANTS " + name + " failed: " + ex);
				}
				break;
				IL_007b:
				yield return current;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			if (_mod.Interior.Inside)
			{
				_mod.LeaveRoomForTest();
			}
		}

		private Vector2? HillSite(List<Vector2> taken)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			if (terrainForTest == null || terrainForTest.Width == 0)
			{
				Log.Error("VARIANTS no terrain yet");
				return null;
			}
			Vector2? result = null;
			float num = float.MaxValue;
			float num2 = 0f;
			int num3 = 0;
			for (int i = 14; i < terrainForTest.Height - 36; i += 2)
			{
				for (int j = 10; j < terrainForTest.Width - 56; j += 2)
				{
					bool flag = false;
					foreach (Vector2 item in taken)
					{
						if (Mathf.Abs(item.x - (float)j) < 64f && Mathf.Abs(item.y - (float)i) < 48f)
						{
							flag = true;
						}
					}
					if (flag)
					{
						continue;
					}
					float num4 = float.MaxValue;
					float num5 = float.MinValue;
					int num6 = 0;
					int num7 = 0;
					for (int k = -12; k <= 32; k += 2)
					{
						for (int l = -8; l <= 52; l += 2)
						{
							int num8 = (i + k) * terrainForTest.Width + j + l;
							if ((terrainForTest.Flags[num8] & 0xB) != 0)
							{
								num6++;
								continue;
							}
							float num9 = terrainForTest.Ground[num8];
							if (Mathf.Abs(num9 - terrainForTest.Ground[num8 + 2]) > 0.7f || Mathf.Abs(num9 - terrainForTest.Ground[num8 + 2 * terrainForTest.Width]) > 0.7f)
							{
								num7++;
							}
							num4 = Mathf.Min(num4, num9);
							num5 = Mathf.Max(num5, num9);
						}
					}
					if (!(num4 > num5))
					{
						float num10 = num5 - num4;
						float num11 = (float)num6 * 10f + (float)num7 * 3f + Mathf.Max(0f, num10 - 3f) * 4f + Mathf.Max(0f, 0.8f - num10) * 3f;
						if (num11 < num)
						{
							num = num11;
							result = new Vector2(j, i);
							num2 = num10;
							num3 = num6;
						}
					}
				}
			}
			if (result.HasValue)
			{
				Log.Info($"VARIANTS site {result.Value}: score {num:F1}, rise {num2:F2} tiles, {num3} wet or mountain tiles");
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
				if (num2 < -8f || num2 > 54f || num3 < -12f || num3 > 34f)
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
			Log.Info($"VARIANTS cleared {num} plants and rocks");
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
				if (tile.zone != null && tile.zone.city == null)
				{
					try
					{
						city.addZone(tile.zone);
					}
					catch
					{
					}
				}
				building.completeConstruction();
				building.setKingdom(city.kingdom);
				city.listBuilding(building);
				return building;
			}
			catch (Exception ex)
			{
				Log.Error("VARIANTS building " + id + ": " + ex.Message);
				return null;
			}
		}

		private static Vector2Int Near(int x, int y, string race, int want, int count)
		{
			for (int i = 0; i <= 6; i++)
			{
				for (int j = -i; j <= i; j++)
				{
					int[] array = new int[3] { 0, -1, 1 };
					foreach (int num in array)
					{
						if (Models.ShapeAt(x + j, y + num, race, count) == want)
						{
							return new Vector2Int(x + j, y + num);
						}
					}
				}
			}
			return new Vector2Int(x, y);
		}

		private static string Mix(int x0, int y0, string race)
		{
			int[] array = new int[3];
			int num = 48;
			for (int i = y0; i < y0 + num; i++)
			{
				for (int j = x0; j < x0 + num; j++)
				{
					array[Models.ShapeAt(j, i, race, 3)]++;
				}
			}
			return $"original {array[0] * 100 / (num * num)}%, b {array[1] * 100 / (num * num)}%, c {array[2] * 100 / (num * num)}%";
		}

		private IEnumerator Village(string race, Actor king, Vector2 site, bool ruin, bool rooms, bool doors)
		{
			City city = king.city;
			Clear(site);
			int sx = (int)site.x;
			int street = (int)site.y;
			List<(Building b, string family)> placed = new List<(Building, string)>();
			for (int i = 0; i < 6; i++)
			{
				for (int j = 0; j < 3; j++)
				{
					Vector2Int vector2Int = Near(sx + 3 + (int)((float)i * 8.5f), street + 5 + j * 11, race, j, 3);
					Building building = Put(city, $"house_{race}_{i}", vector2Int.x, vector2Int.y);
					if (building != null)
					{
						placed.Add((building, Models.BuildingFamily(building)));
					}
				}
			}
			for (int k = 0; k < 2; k++)
			{
				for (int l = 0; l < 2; l++)
				{
					Vector2Int vector2Int2 = Near(sx + 6 + k * 12 + l * 5, street - 5, race, k, 2);
					Building building2 = Put(city, "tent_" + race, vector2Int2.x, vector2Int2.y);
					if (building2 != null)
					{
						placed.Add((building2, Models.BuildingFamily(building2)));
					}
				}
			}
			HashSet<string> hashSet = new HashSet<string>();
			foreach (var item2 in placed)
			{
				hashSet.Add(item2.family);
			}
			Log.Info(string.Format("VARIANTS {0} at {1}: placed {2}: {3}", race, site, placed.Count, string.Join(", ", placed.ConvertAll(((Building b, string family) p) => p.b.asset.id + "=" + p.family + (Models.Mirrored(p.b) ? " (mirrored)" : "")))));
			Log.Info(string.Format("VARIANTS {0}: {1} different models: {2}", race, hashSet.Count, string.Join(" ", hashSet)));
			for (int num = 0; num < 4; num++)
			{
				int num2 = 48;
				int num3 = (sx + num % 2 * 40) / num2 * num2;
				int num4 = (street + num / 2 * 30) / num2 * num2;
				Log.Info($"VARIANTS {race} district at {num3},{num4}: {Mix(num3, num4, race)}");
			}
			Settings s = _mod.Settings;
			int savedMode = s.TimeMode;
			s.TimeMode = 1;
			_mod.ForceRelight();
			try
			{
				king.setCurrentTilePosition(World.world.GetTile(sx - 3, street));
			}
			catch
			{
			}
			_mod.DebugEyeSet = true;
			_mod.DebugEye = new Vector2((float)sx - 4.5f, (float)street + 0.5f);
			_mod.SetYaw(0f);
			_mod.SetPitchDegrees(-2f);
			yield return new WaitForSecondsRealtime(3f);
			_mod.SaveFrame("var_" + race + "_street.png");
			for (int row = 0; row < 3; row++)
			{
				_mod.DebugEye = new Vector2((float)sx - 6f, (float)street + 1.5f + (float)row * 11f);
				_mod.SetYaw(0.32f);
				_mod.SetPitchDegrees(-3f);
				yield return new WaitForSecondsRealtime(1.2f);
				_mod.SaveFrame($"var_{race}_row{row}.png");
			}
			_mod.SceneForTest.DumpNear(new Vector2((float)sx + 30f, (float)street + 12f), 45f);
			foreach (var (b, family) in placed)
			{
				if (b != null && b.isAlive())
				{
					Vector2 at = new Vector2((float)b.current_tile.x + 0.5f, (float)b.current_tile.y + 0.5f);
					bool tent = family.StartsWith("tent", StringComparison.Ordinal);
					float back = (tent ? 4.5f : 7.5f);
					Vector2 debugEye = at + new Vector2(back * 0.55f, 0f - back);
					_mod.DebugEye = debugEye;
					_mod.SetYaw(Mathf.Atan2(at.y + 1f - debugEye.y, at.x - debugEye.x));
					_mod.SetPitchDegrees(-7f);
					yield return new WaitForSecondsRealtime(0.7f);
					_mod.SaveFrame("var_" + race + "_" + family + ".png");
					if (!tent && b.asset.id.EndsWith("3", StringComparison.Ordinal))
					{
						debugEye = at + new Vector2((0f - back) * 0.8f, (0f - back) * 0.55f);
						_mod.DebugEye = debugEye;
						_mod.SetYaw(Mathf.Atan2(at.y + 1f - debugEye.y, at.x - debugEye.x));
						yield return new WaitForSecondsRealtime(0.6f);
						_mod.SaveFrame("var_" + race + "_" + family + "_side.png");
					}
				}
			}
			if (doors)
			{
				foreach (var (b, family) in placed)
				{
					if (b != null && b.isAlive() && !family.StartsWith("tent", StringComparison.Ordinal))
					{
						_mod.DebugEye = new Vector2((float)b.current_tile.x + 0.5f, (float)b.current_tile.y - 3.5f);
						yield return new WaitForSecondsRealtime(0.4f);
						if (!Steering.DoorForTest(b, out var door))
						{
							Log.Info("VARIANTS " + family + ": no door found");
							continue;
						}
						Vector2 debugEye2 = door + new Vector2(0.3f, -2.1f);
						_mod.DebugEye = debugEye2;
						_mod.SetYaw(Mathf.Atan2(door.y + 0.3f - debugEye2.y, door.x - debugEye2.x));
						_mod.SetPitchDegrees(0f);
						yield return new WaitForSecondsRealtime(0.6f);
						_mod.SaveFrame("var_" + race + "_" + family + "_door.png");
					}
				}
			}
			SceneCollector.AllLit = true;
			s.TimeMode = 3;
			_mod.ForceRelight();
			_mod.DebugEye = new Vector2((float)sx - 4.5f, (float)street + 0.5f);
			_mod.SetYaw(0.15f);
			_mod.SetPitchDegrees(-2f);
			yield return new WaitForSecondsRealtime(1.5f);
			_mod.SaveFrame("var_" + race + "_night.png");
			SceneCollector.AllLit = false;
			s.TimeMode = 1;
			_mod.ForceRelight();
			if (rooms)
			{
				HashSet<string> seen = new HashSet<string>();
				foreach (var (building3, family) in placed)
				{
					if (building3 == null || !building3.isAlive())
					{
						continue;
					}
					string style = Models.Get(family)?.Info("style");
					bool flag = family.StartsWith("tent", StringComparison.Ordinal);
					if (style == null || seen.Contains(style) || (!flag && !building3.asset.id.EndsWith("3", StringComparison.Ordinal)))
					{
						continue;
					}
					seen.Add(style);
					if (flag && !building3.asset.id.StartsWith("tent", StringComparison.Ordinal))
					{
						Log.Info("VARIANTS " + family + ": the game made it a " + building3.asset.id + " meanwhile, not entered");
						seen.Remove(style);
						continue;
					}
					if (!HouseInterior.IsEnterable(building3))
					{
						Log.Info($"VARIANTS {family}: not one to go into now ({building3.asset.id}, building {building3.isUnderConstruction()}, ruin {building3.isRuin()})");
						seen.Remove(style);
						continue;
					}
					_mod.DebugEyeSet = false;
					_mod.EnterHouseForTest(building3);
					yield return new WaitForSecondsRealtime(1.2f);
					if (!_mod.Interior.Inside)
					{
						Log.Error("VARIANTS could not enter " + family);
						continue;
					}
					Interior room = _mod.Interior.Room;
					Log.Info($"VARIANTS room {style}: {room.Meshes.Count} models, {room.Lights.Count} lights, {room.Spots.Count} spots, {room.Windows.Count} windows, {room.MinX:F1}..{room.MaxX:F1} x {room.MinY:F1}..{room.MaxY:F1}, top {room.Top:F2}, render {_mod.RenderMs:F1} ms");
					_mod.SetPitchDegrees(-4f);
					yield return new WaitForSecondsRealtime(0.5f);
					_mod.SaveFrame("var_room_" + style + ".png");
					_mod.SetYaw(2.4707963f);
					yield return new WaitForSecondsRealtime(0.5f);
					_mod.SaveFrame("var_room_" + style + "_left.png");
					_mod.SetYaw(0.6707964f);
					_mod.SetPitchDegrees(8f);
					yield return new WaitForSecondsRealtime(0.5f);
					_mod.SaveFrame("var_room_" + style + "_right.png");
					if (style == "human_stone")
					{
						ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "var_screen_room.png"));
					}
					yield return new WaitForSecondsRealtime(0.4f);
					_mod.LeaveRoomForTest();
					yield return new WaitForSecondsRealtime(0.6f);
					_mod.DebugEyeSet = true;
				}
			}
			if (ruin)
			{
				int num5 = 0;
				foreach (var item3 in placed)
				{
					Building item = item3.b;
					try
					{
						if (item != null && item.isAlive())
						{
							item.makeRuins();
							num5++;
						}
					}
					catch (Exception ex)
					{
						Log.Error("VARIANTS could not ruin " + item.asset.id + ": " + ex.Message);
					}
				}
				Log.Info($"VARIANTS {race}: {num5} ruined");
				_mod.DebugEye = new Vector2((float)sx - 4.5f, (float)street + 0.5f);
				_mod.SetYaw(0.2f);
				_mod.SetPitchDegrees(-2f);
				yield return new WaitForSecondsRealtime(2.5f);
				_mod.SaveFrame("var_" + race + "_ruins.png");
				_mod.SceneForTest.DumpNear(new Vector2((float)sx + 30f, (float)street + 12f), 45f);
				foreach (var (building4, family) in placed)
				{
					if (building4 != null && building4.isAlive() && building4.current_tile != null && (family.StartsWith("tent", StringComparison.Ordinal) || building4.asset.id.EndsWith("2", StringComparison.Ordinal) || building4.asset.id.EndsWith("5", StringComparison.Ordinal)))
					{
						Vector2 vector = new Vector2((float)building4.current_tile.x + 0.5f, (float)building4.current_tile.y + 0.5f);
						float num6 = (family.StartsWith("tent", StringComparison.Ordinal) ? 4.5f : 7f);
						Vector2 debugEye3 = vector + new Vector2(num6 * 0.55f, 0f - num6);
						_mod.DebugEye = debugEye3;
						_mod.SetYaw(Mathf.Atan2(vector.y + 1f - debugEye3.y, vector.x - debugEye3.x));
						_mod.SetPitchDegrees(-8f);
						yield return new WaitForSecondsRealtime(0.7f);
						_mod.SaveFrame("var_" + race + "_ruin_" + family + ".png");
					}
				}
			}
			_mod.DebugEyeSet = false;
			s.TimeMode = savedMode;
			_mod.ForceRelight();
			yield return new WaitForSecondsRealtime(1f);
		}
	}
}
