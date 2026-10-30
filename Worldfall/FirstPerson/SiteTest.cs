using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class SiteTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private static readonly (string id, int stage)[] Civic = new(string, int)[16]
		{
			("windmill_human_0", 1),
			("temple_human", 2),
			("library_human", 3),
			("barracks_human", 2),
			("watch_tower_human", 2),
			("market_human", 1),
			("well", 1),
			("statue", 2),
			("training_dummy", 0),
			("stockpile", 0),
			("tent_human", 1),
			("tent_orc", 0),
			("bonfire", 1),
			("hall_elf_0", 3),
			("hall_orc_0", 1),
			("hall_dwarf_0", 2)
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
			Log.Info("SITES models " + (Models.Ready ? "ready" : "missing") + ", site_hall_human " + (Models.Has("site_hall_human") ? "found" : "MISSING"));
			Actor king = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item, "human", 4, 0);
				if (king != null)
				{
					break;
				}
			}
			if (king == null)
			{
				Log.Error("SITES could not found a kingdom");
				Application.Quit();
				yield break;
			}
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("SITES first person did not start");
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
			IEnumerator run = Street(king, options);
			while (true)
			{
				object current2;
				try
				{
					if (!run.MoveNext())
					{
						break;
					}
					current2 = run.Current;
					goto IL_0289;
				}
				catch (Exception ex)
				{
					Log.Error("SITES failed: " + ex);
				}
				break;
				IL_0289:
				yield return current2;
			}
			_mod.DebugEyeSet = false;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("SITES DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private Vector2? Block(Vector2 near)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			if (terrainForTest == null || terrainForTest.Width == 0)
			{
				return null;
			}
			Vector2? result = null;
			float num = float.MaxValue;
			for (int i = 22; i < terrainForTest.Height - 26; i += 2)
			{
				for (int j = 6; j < terrainForTest.Width - 70; j += 2)
				{
					float num2 = float.MaxValue;
					float num3 = float.MinValue;
					int num4 = 0;
					for (int k = -20; k <= 24; k += 2)
					{
						for (int l = -4; l <= 64; l += 2)
						{
							int num5 = (i + k) * terrainForTest.Width + j + l;
							if ((terrainForTest.Flags[num5] & 0xB) != 0)
							{
								num4++;
								continue;
							}
							float b = terrainForTest.Ground[num5];
							num2 = Mathf.Min(num2, b);
							num3 = Mathf.Max(num3, b);
						}
					}
					if (!(num2 > num3))
					{
						float num6 = (float)num4 * 10f + Mathf.Max(0f, num3 - num2 - 1.5f) * 6f + (new Vector2(j, i) - near).magnitude * 0.02f;
						if (num6 < num)
						{
							num = num6;
							result = new Vector2(j, i);
						}
					}
				}
			}
			if (result.HasValue)
			{
				Log.Info($"SITES block {result.Value}: score {num:F1}");
			}
			return result;
		}

		private static Building Raise(City city, string id, int x, int y, int stage)
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
				building.setKingdom(city.kingdom);
				city.listBuilding(building);
				if (!building.isUnderConstruction())
				{
					building.setUnderConstruction();
				}
				SetStage(building, stage);
				return building;
			}
			catch (Exception ex)
			{
				Log.Error("SITES building " + id + ": " + ex.Message);
				return null;
			}
		}

		private static void SetStage(Building b, int stage)
		{
			int num = Mathf.Max(1, b.asset.construction_progress_needed);
			b.data.set("construction_progress", Mathf.Min(num - 1, (int)(((float)stage + 0.5f) * (float)num / 4f)));
		}

		private IEnumerator Street(Actor king, string options)
		{
			City city = king.city;
			Vector2? vector = Block(new Vector2(king.current_tile.x, king.current_tile.y));
			if (!vector.HasValue)
			{
				Log.Error("SITES no land for the street");
				yield break;
			}
			int sx = (int)vector.Value.x;
			int street = (int)vector.Value.y;
			foreach (Building item3 in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item3 == null || !item3.isAlive() || item3.current_tile == null)
				{
					continue;
				}
				float num = item3.current_tile.x - sx;
				float num2 = item3.current_tile.y - street;
				if (num < -4f || num > 66f || num2 < -20f || num2 > 24f)
				{
					continue;
				}
				try
				{
					if (!item3.isKingdomCiv())
					{
						item3.removeBuildingFinal();
					}
				}
				catch
				{
				}
			}
			int num3 = 0;
			for (int i = street - 22; i <= street + 26; i += 4)
			{
				for (int j = sx - 6; j <= sx + 68; j += 4)
				{
					TileZone tileZone = World.world.GetTile(j, i)?.zone;
					if (tileZone != null && tileZone.city == null)
					{
						try
						{
							city.addZone(tileZone);
							num3++;
						}
						catch (Exception ex)
						{
							Log.Error("SITES zone: " + ex.Message);
						}
					}
				}
			}
			Log.Info($"SITES claimed {num3} zones for {city.name}");
			List<(Building b, int stage)> placed = new List<(Building, int)>();
			for (int k = 0; k < 4; k++)
			{
				Building building = Raise(city, "hall_human_0", sx + 6 + k * 11, street + 7, k);
				if (building != null)
				{
					placed.Add((building, k));
				}
			}
			List<(string, int)> list = new List<(string, int)>(Civic);
			if (options.Contains("civ"))
			{
				string[] array = new string[2] { "cat", "ghost" };
				foreach (string text in array)
				{
					if (Models.Has("site_hall_" + text))
					{
						list.Add(("hall_civ_" + text + "_0", 2));
					}
				}
			}
			int num4 = sx + 4;
			int num5 = sx + 3;
			int num6 = sx + 6;
			foreach (var (text2, num7) in list)
			{
				if (text2.StartsWith("tent", StringComparison.Ordinal))
				{
					goto IL_0395;
				}
				switch (text2)
				{
				case "well":
				case "training_dummy":
				case "statue":
					goto IL_0395;
				}
				int num8 = ((text2 == "bonfire") ? 1 : 0);
				goto IL_0396;
				IL_0396:
				bool flag = (byte)num8 != 0;
				bool flag2 = text2.StartsWith("hall_", StringComparison.Ordinal);
				int num9 = (flag2 ? num6 : (flag ? num5 : num4));
				int y = street + (flag2 ? 18 : (flag ? (-16) : (-8)));
				if (num9 <= sx + 62)
				{
					Building building2 = Raise(city, text2, num9, y, num7);
					if (building2 != null)
					{
						placed.Add((building2, num7));
					}
					if (flag2)
					{
						num6 += 11;
					}
					else if (flag)
					{
						num5 += 5;
					}
					else
					{
						num4 += 8;
					}
				}
				continue;
				IL_0395:
				num8 = 1;
				goto IL_0396;
			}
			Log.Info($"SITES placed {placed.Count}");
			yield return new WaitForSecondsRealtime(1.5f);
			int num10 = placed.RemoveAll(((Building b, int stage) q) => q.b == null || !q.b.isAlive() || q.b.asset == null || q.b.data == null);
			if (num10 > 0)
			{
				Log.Error($"SITES {num10} sites were pulled down");
			}
			int num11 = 0;
			foreach (var item4 in placed)
			{
				Building item = item4.b;
				int item2 = item4.stage;
				string text3 = Models.BuildingFamily(item);
				bool flag3 = text3 != null && Models.Has("site_" + text3);
				if (flag3)
				{
					num11++;
				}
				Log.Info(string.Format("SITES {0}: family {1}, site {2}, building {3}, progress {4}/{5} stage {6} (wanted {7})", item.asset.id, text3, flag3 ? "yes" : "NO", item.isUnderConstruction(), item.getConstructionProgress(), item.asset.construction_progress_needed, BuildSite.StageOf(item.getConstructionProgress(), item.asset.construction_progress_needed), item2));
			}
			Log.Info($"SITES {placed.Count} placed, {num11} with a site model");
			Settings s = _mod.Settings;
			int savedMode = s.TimeMode;
			s.TimeMode = 1;
			_mod.ForceRelight();
			try
			{
				king.setCurrentTilePosition(World.world.GetTile(sx - 2, street));
			}
			catch
			{
			}
			_mod.DebugEyeSet = true;
			_mod.DebugEye = new Vector2((float)sx - 3.5f, (float)street + 0.5f);
			_mod.SetYaw(0.12f);
			_mod.SetPitchDegrees(-2f);
			yield return new WaitForSecondsRealtime(3f);
			_mod.SaveFrame("site_street_north.png");
			_mod.SetYaw(-0.16f);
			yield return new WaitForSecondsRealtime(1f);
			_mod.SaveFrame("site_street_south.png");
			_mod.SceneForTest.DumpNear(new Vector2((float)sx + 30f, street), 40f);
			Log.Info($"SITES street render {_mod.RenderMs:F1} ms");
			foreach (var (b, stage) in placed)
			{
				if (b != null && b.isAlive() && b.current_tile != null)
				{
					Vector2 vector2 = new Vector2((float)b.current_tile.x + 0.5f, (float)b.current_tile.y + 0.5f);
					bool flag4 = b.asset.id.StartsWith("tent", StringComparison.Ordinal) || b.asset.id == "well" || b.asset.id == "training_dummy" || b.asset.id == "bonfire";
					float num12 = (flag4 ? 5f : 9f);
					Vector2 debugEye = ((vector2.y > (float)(street + 14)) ? (vector2 + new Vector2(num12 * 0.45f, num12 + 2f)) : ((vector2.y > (float)street) ? (vector2 + new Vector2(num12 * 0.45f, 0f - num12)) : (vector2 + new Vector2(num12 * 0.45f, flag4 ? (num12 * 0.8f) : (num12 * 0.95f)))));
					_mod.DebugEye = debugEye;
					_mod.SetYaw(Mathf.Atan2(vector2.y + 1f - debugEye.y, vector2.x - debugEye.x));
					_mod.SetPitchDegrees(flag4 ? (-8f) : 3f);
					yield return new WaitForSecondsRealtime(0.8f);
					_mod.SaveFrame($"site_{b.asset.id}_{stage}.png");
				}
			}
			Building riser = ((placed.Count > 0) ? placed[0].b : null);
			if (riser != null && riser.isAlive())
			{
				Vector2 at = new Vector2((float)riser.current_tile.x + 0.5f, (float)riser.current_tile.y + 0.5f);
				_mod.DebugEye = at + new Vector2(5f, -10f);
				_mod.SetYaw(Mathf.Atan2(at.y + 1f - _mod.DebugEye.y, at.x - _mod.DebugEye.x));
				_mod.SetPitchDegrees(2f);
				for (int stage = 0; stage < 4; stage++)
				{
					SetStage(riser, stage);
					yield return new WaitForSecondsRealtime(0.6f);
					_mod.SaveFrame($"site_rise_{stage}.png");
					_mod.SceneForTest.DumpNear(at, 1.5f);
				}
				riser.completeConstruction();
				yield return new WaitForSecondsRealtime(1f);
				_mod.SaveFrame("site_rise_done.png");
				_mod.SceneForTest.DumpNear(at, 1.5f);
			}
			s.TimeMode = 3;
			_mod.ForceRelight();
			_mod.DebugEye = new Vector2((float)sx - 3.5f, (float)street + 0.5f);
			_mod.SetYaw(0.12f);
			_mod.SetPitchDegrees(-2f);
			yield return new WaitForSecondsRealtime(1.5f);
			_mod.SaveFrame("site_street_night.png");
			s.TimeMode = savedMode;
			_mod.ForceRelight();
			_mod.DebugEyeSet = false;
		}
	}
}
