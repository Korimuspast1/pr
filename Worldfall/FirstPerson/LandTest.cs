using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class LandTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private int _k;

		private readonly List<RectInt> _dug = new List<RectInt>();

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
			Settings s = _mod.Settings;
			int savedTime = s.TimeMode;
			s.TimeMode = 1;
			_mod.ForceRelight();
			Actor king = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item, "human", 6, 0);
				if (king != null)
				{
					break;
				}
			}
			if (king == null)
			{
				Log.Error("LAND could not found a village");
				Finish(quit, s, savedTime);
				yield break;
			}
			yield return new WaitForSecondsRealtime(2f);
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(3f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("LAND first person did not start");
				Finish(quit, s, savedTime);
				yield break;
			}
			WorldSampler sampler = _mod.SamplerForTest;
			Vector2 home = king.current_position;
			int width = MapBox.width;
			int height = MapBox.height;
			int sweep = Mathf.CeilToInt((float)height / Mathf.Max(2f, Mathf.Min(height, Mathf.Floor(6000f / (float)width))));
			for (int round = 0; round < 6; round++)
			{
				WorldSampler.Quick = round < 3;
				Config.paused = false;
				yield return null;
				sampler.StaleForTest();
				int changed = Change(home);
				Config.paused = true;
				sampler.ScanLandformsForTest();
				for (int i = 0; i < sweep + 6; i++)
				{
					yield return null;
				}
				string[] why = new string[0];
				bool[] array = sampler.StaleForTest(out why);
				int width2 = _mod.TerrainForTest.Width;
				int num = 0;
				List<string> list = new List<string>();
				int num2 = 0;
				for (int j = 0; j < array.Length; j++)
				{
					if (!array[j])
					{
						continue;
					}
					WorldTile tile = World.world.GetTile(j % width2, j / width2);
					if (tile != null && tile.main_type != null && tile.main_type.id != null && tile.main_type.id.Contains("lava"))
					{
						num2++;
						continue;
					}
					num++;
					if (list.Count < 6)
					{
						list.Add($"{j % width2},{j / width2} {why[j]}");
					}
				}
				if (num2 > 0)
				{
					Log.Info($"LAND round {round + 1}: {num2} tiles lava reached after the sweep (out of date whichever way the land is read)");
				}
				Check(string.Format("round {0} ({1}): after a sweep not a tile is out of date", round + 1, WorldSampler.Quick ? "only changed tiles read" : "every tile read"), num == 0, string.Format("{0} tiles out of date after {1} changes and {2} frames{3}", num, changed, sweep + 6, (list.Count > 0) ? (" (e.g. " + string.Join(" ", list) + ")") : ""));
				CheckHollows(round);
			}
			Config.paused = false;
			bool[] array2 = new bool[4] { true, false, true, false };
			foreach (bool quick in array2)
			{
				WorldSampler.Quick = quick;
				yield return new WaitForSecondsRealtime(0.5f);
				double read0 = sampler.TotalReadMs;
				double light0 = sampler.TotalLightMs;
				long tiles0 = sampler.TotalTiles;
				int changed = 0;
				float t = Time.unscaledTime;
				double frame = 0.0;
				while (Time.unscaledTime - t < 4f)
				{
					yield return null;
					changed++;
					frame += (double)Time.unscaledDeltaTime * 1000.0;
				}
				Log.Info(string.Format("LAND cost {0}: reading {1:F2} ms, ", quick ? "only changed tiles read in full" : "every tile read in full", (sampler.TotalReadMs - read0) / (double)Math.Max(1, changed)) + $"relighting {(sampler.TotalLightMs - light0) / (double)Math.Max(1, changed):F2} ms, {(sampler.TotalTiles - tiles0) / Math.Max(1, changed)} tiles a frame, {1000.0 * (double)changed / Math.Max(1.0, frame):F1} fps");
			}
			WorldSampler.Quick = true;
			Log.Info($"LAND {_pass} passed, {_fail} failed");
			Finish(quit, s, savedTime);
		}

		private int Change(Vector2 home)
		{
			int count = 0;
			TileType mountains = AssetManager.tiles.get("mountains");
			TileType sand = AssetManager.tiles.get("sand");
			TileType shallow = AssetManager.tiles.get("shallow_waters");
			foreach (RectInt item in _dug)
			{
				if (sand != null)
				{
					Each(item, delegate(WorldTile t)
					{
						t.setTileType(sand);
					});
				}
			}
			_dug.Clear();
			TileType pit = AssetManager.tiles.get("pit_close_ocean");
			TileType deepPit = AssetManager.tiles.get("pit_deep_ocean");
			RectInt rectInt;
			RectInt rectInt2;
			if (pit != null && deepPit != null)
			{
				rectInt = (rectInt2 = Patch(home, 5));
				if (rectInt.width > 0)
				{
					RectInt hole = rectInt2;
					Each(hole, delegate(WorldTile t)
					{
						t.setTileType((Mathf.Abs(t.x - hole.x - 2) + Mathf.Abs(t.y - hole.y - 2) <= 1) ? deepPit : pit);
					});
					_dug.Add(hole);
				}
			}
			if (deepPit != null)
			{
				rectInt = (rectInt2 = Patch(home, 1));
				if (rectInt.width > 0)
				{
					Each(rectInt2, delegate(WorldTile t)
					{
						t.setTileType(deepPit);
					});
					_dug.Add(rectInt2);
				}
			}
			if (mountains != null)
			{
				rectInt = (rectInt2 = Patch(home, 5));
				if (rectInt.width > 0)
				{
					Each(rectInt2, delegate(WorldTile t)
					{
						t.setTileType(mountains);
					});
				}
			}
			if (shallow != null)
			{
				rectInt = (rectInt2 = Patch(home, 4));
				if (rectInt.width > 0)
				{
					Each(rectInt2, delegate(WorldTile t)
					{
						t.setTileType(shallow);
					});
				}
			}
			if (sand != null)
			{
				rectInt = (rectInt2 = Patch(home, 4));
				if (rectInt.width > 0)
				{
					Each(rectInt2, delegate(WorldTile t)
					{
						t.setTileType(sand);
					});
				}
			}
			rectInt = (rectInt2 = Patch(home, 4));
			if (rectInt.width > 0)
			{
				Each(rectInt2, delegate(WorldTile t)
				{
					t.freeze(1000);
				});
			}
			rectInt = (rectInt2 = Patch(home, 3));
			if (rectInt.width > 0)
			{
				Each(rectInt2, delegate(WorldTile t)
				{
					t.startFire(pForce: true);
				});
			}
			rectInt = (rectInt2 = Patch(home, 4));
			if (rectInt.width > 0)
			{
				Each(rectInt2, delegate(WorldTile t)
				{
					t.setBurned(12);
				});
			}
			rectInt = (rectInt2 = Patch(home, 5));
			if (rectInt.width > 0)
			{
				try
				{
					Building building = World.world.buildings.addBuilding("house_human_1", World.world.GetTile(rectInt2.x + 2, rectInt2.y + 2), pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					if (building != null)
					{
						building.completeConstruction();
						count++;
					}
				}
				catch (Exception ex)
				{
					Log.Error("LAND house: " + ex.Message);
				}
			}
			rectInt = (rectInt2 = Patch(home, 3));
			if (rectInt.width > 0)
			{
				try
				{
					if (World.world.buildings.addBuilding("wheat", World.world.GetTile(rectInt2.x + 1, rectInt2.y + 1), pCheckForBuild: false, pSfx: false, BuildPlacingType.New) != null)
					{
						count++;
					}
				}
				catch (Exception ex2)
				{
					Log.Error("LAND wheat: " + ex2.Message);
				}
			}
			return count;
			void Each(RectInt r, Action<WorldTile> act)
			{
				for (int i = 0; i < r.height; i++)
				{
					for (int j = 0; j < r.width; j++)
					{
						WorldTile tile = World.world.GetTile(r.x + j, r.y + i);
						if (tile != null)
						{
							try
							{
								act(tile);
								count++;
							}
							catch (Exception ex3)
							{
								Log.Error("LAND change: " + ex3.Message);
							}
						}
					}
				}
			}
		}

		private RectInt Patch(Vector2 home, int size)
		{
			int width = MapBox.width;
			int height = MapBox.height;
			int num = 0;
			while (num < 60)
			{
				float f = (float)_k * 2.399f;
				float num2 = 30f + (float)(_k * 13 % 60);
				int num3 = Mathf.Clamp((int)(home.x + Mathf.Cos(f) * num2), 2, width - size - 2);
				int num4 = Mathf.Clamp((int)(home.y + Mathf.Sin(f) * num2), 2, height - size - 2);
				bool flag = true;
				for (int i = 0; i < size && flag; i++)
				{
					for (int j = 0; j < size && flag; j++)
					{
						WorldTile tile = World.world.GetTile(num3 + j, num4 + i);
						flag = tile != null && tile.main_type != null && !tile.main_type.liquid && tile.building == null;
					}
				}
				int num5 = 3;
				foreach (RectInt item in _dug)
				{
					if (num3 < item.xMax + num5 && num3 + size + num5 > item.x && num4 < item.yMax + num5 && num4 + size + num5 > item.y)
					{
						flag = false;
					}
				}
				if (flag)
				{
					_k++;
					return new RectInt(num3, num4, size, size);
				}
				num++;
				_k++;
			}
			return new RectInt(-1, -1, 0, 0);
		}

		private void CheckHollows(int round)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			int num = 2;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			List<string> list = new List<string>();
			foreach (RectInt item in _dug)
			{
				for (int i = item.y; i < item.yMax; i++)
				{
					for (int j = item.x; j < item.xMax; j++)
					{
						num6++;
						if (Hole(j, i))
						{
							num5++;
						}
					}
				}
				for (int k = item.y - num; k < item.yMax + num; k++)
				{
					for (int l = item.x - num; l < item.xMax + num; l++)
					{
						if (l < 0 || k < 0 || l >= terrainForTest.Width || k >= terrainForTest.Height)
						{
							continue;
						}
						byte b = terrainForTest.Flags[k * terrainForTest.Width + l];
						if ((b & 3) != 0)
						{
							continue;
						}
						bool flag = false;
						for (int m = -num; m <= num; m++)
						{
							if (flag)
							{
								break;
							}
							for (int n = -num; n <= num; n++)
							{
								if (flag)
								{
									break;
								}
								flag = Hole(l + n, k + m) || (Math.Abs(n) <= 1 && Math.Abs(m) <= 1 && _mod.SamplerForTest.OutOfPlaceForTest(l + n, k + m));
							}
						}
						num2++;
						bool flag2 = (b & 0x40) != 0;
						if (flag2)
						{
							num3++;
						}
						if (flag2 != flag)
						{
							num4++;
							if (list.Count < 6)
							{
								list.Add(string.Format("{0},{1} {2} ({3})", l, k, flag2 ? "flagged" : "not flagged", World.world.GetTile(l, k)?.main_type?.id));
							}
						}
					}
				}
			}
			Check($"round {round + 1}: the ground round {_dug.Count} dug holes is drawn rounded", _dug.Count > 0 && num5 > 0 && num4 == 0, string.Format("{0} of {1} dug tiles still holes, {2} of {3} tiles round them flagged, {4} wrong{5}", num5, num6, num3, num2, num4, (list.Count > 0) ? (" (" + string.Join("; ", list) + ")") : ""));
			bool Hole(int x, int y)
			{
				WorldTile tile = World.world.GetTile(x, y);
				if (tile == null || tile.main_type == null || tile.main_type.id == null || !tile.main_type.id.StartsWith("pit_"))
				{
					return false;
				}
				if (!tile.main_type.lava)
				{
					if (tile.cur_tile_type != null)
					{
						return !tile.cur_tile_type.lava;
					}
					return true;
				}
				return false;
			}
		}

		private void Check(string what, bool ok, string detail)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info("LAND " + (ok ? "PASS" : "FAIL") + " " + what + ": " + detail);
		}

		private void Finish(bool quit, Settings s, int time)
		{
			Config.paused = false;
			WorldSampler.Quick = true;
			s.TimeMode = time;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("LAND DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
