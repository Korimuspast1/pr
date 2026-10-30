using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BiomeFxTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private static readonly (string name, BiomeKind kind, string tile, string tree)[] Biomes = new(string, BiomeKind, string, string)[26]
		{
			("savanna", BiomeKind.Savanna, "savanna_low", "savanna_tree_1"),
			("desert", BiomeKind.Desert, "desert_low", "cacti_tree"),
			("jungle", BiomeKind.Jungle, "jungle_low", "jungle_tree"),
			("swamp", BiomeKind.Swamp, "swamp_low", "swamp_tree"),
			("permafrost", BiomeKind.Permafrost, "permafrost_low", "pine_tree"),
			("birch", BiomeKind.Birch, "birch_low", "birch_tree"),
			("maple", BiomeKind.Maple, "maple_low", "maple_tree"),
			("rocklands", BiomeKind.Rocklands, "rocklands_low", "rocklands_tree"),
			("clover", BiomeKind.Clover, "clover_low", "clover_tree"),
			("flower", BiomeKind.Flower, "flower_low", "flower_tree_1"),
			("lemon", BiomeKind.Lemon, "lemon_low", "lemon_tree"),
			("garlic", BiomeKind.Garlic, "garlic_low", "garlic_tree"),
			("candy", BiomeKind.Candy, "candy_low", "candy_tree"),
			("mushroom", BiomeKind.Mushroom, "mushroom_low", "mushroom_tree"),
			("crystal", BiomeKind.Crystal, "crystal_low", "crystal_tree"),
			("enchanted", BiomeKind.Enchanted, "enchanted_low", "enchanted_tree"),
			("corrupted", BiomeKind.Corrupted, "corrupted_low", "corrupted_tree"),
			("infernal", BiomeKind.Infernal, "infernal_low", "infernal_tree"),
			("wasteland", BiomeKind.Wasteland, "wasteland_low", "wasteland_tree"),
			("celestial", BiomeKind.Celestial, "celestial_low", "celestial_tree_small"),
			("singularity", BiomeKind.Singularity, "singularity_low", "singularity_tree"),
			("paradox", BiomeKind.Paradox, "paradox_low", "paradox_tree"),
			("tumor", BiomeKind.Tumor, "tumor_low", null),
			("biomass", BiomeKind.Biomass, "biomass_low", null),
			("pumpkin", BiomeKind.Pumpkin, "pumpkin_low", null),
			("cybertile", BiomeKind.Cybertile, "cybertile_low", null)
		};

		private const int Half = 22;

		private float _measured;

		private void Check(bool ok, string what)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info("BFX " + (ok ? "PASS" : "FAIL") + " " + what);
		}

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
			Actor king = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item, "human", 2, 0);
				if (king != null)
				{
					break;
				}
			}
			if (king == null)
			{
				Log.Error("BFX could not found a kingdom");
				Application.Quit();
				yield break;
			}
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("BFX first person did not start");
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
			Vector2? vector = FlatSite(king.current_position);
			if (!vector.HasValue)
			{
				Log.Error("BFX no flat open land");
				Application.Quit();
				yield break;
			}
			Vector2 mid = vector.Value;
			Settings s = _mod.Settings;
			int savedMode = s.TimeMode;
			int savedFx = s.BiomeFx;
			bool savedGpu = s.GpuRender;
			s.BiomeFx = 2;
			s.TimeMode = 1;
			_mod.ForceRelight();
			WorldTile worldTile = DryTile((int)mid.x, (int)mid.y - 22 - 6);
			if (worldTile != null)
			{
				try
				{
					king.setCurrentTilePosition(worldTile);
					Log.Info($"BFX king parked at {worldTile.x},{worldTile.y}");
				}
				catch
				{
				}
			}
			yield return Paint(mid, "grass_low", null);
			_mod.DebugEye = mid;
			_mod.DebugEyeSet = true;
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(4f);
			yield return new WaitForSecondsRealtime(4f);
			uint grassHorizon = _mod.Renderer.Air.Horizon;
			yield return Measure(60);
			float measured = _measured;
			_mod.SaveFrame("bfx_grass_gpu.png");
			Log.Info($"BFX grass: horizon {Hex(grassHorizon)}, frame {measured:F1} ms");
			(string name, BiomeKind kind, string tile, string tree)[] biomes = Biomes;
			for (int i = 0; i < biomes.Length; i++)
			{
				(string name, BiomeKind kind, string tile, string tree) b = biomes[i];
				if (options.Contains("only") && !options.Contains("only" + b.name))
				{
					continue;
				}
				if (!_mod.IsFirstPerson)
				{
					Log.Error("BFX first person ended (the body died?): " + b.name + " and the rest not measured");
					_fail++;
					break;
				}
				IEnumerator part = Show(b.name, b.kind, b.tile, b.tree, mid, grassHorizon);
				while (true)
				{
					object current2;
					try
					{
						if (!part.MoveNext())
						{
							break;
						}
						current2 = part.Current;
						goto IL_0547;
					}
					catch (Exception ex)
					{
						Log.Error("BFX " + b.name + " failed: " + ex);
						_fail++;
					}
					break;
					IL_0547:
					yield return current2;
				}
			}
			if (_mod.IsFirstPerson && (!options.Contains("only") || options.Contains("onlyamount")))
			{
				IEnumerator part = Amounts(mid);
				while (true)
				{
					object current3;
					try
					{
						if (!part.MoveNext())
						{
							break;
						}
						current3 = part.Current;
						goto IL_0633;
					}
					catch (Exception ex2)
					{
						Log.Error("BFX amount failed: " + ex2);
						_fail++;
					}
					break;
					IL_0633:
					yield return current3;
				}
			}
			if (_mod.IsFirstPerson && !options.Contains("nowalk"))
			{
				IEnumerator part = Walk(mid);
				while (true)
				{
					object current4;
					try
					{
						if (!part.MoveNext())
						{
							break;
						}
						current4 = part.Current;
						goto IL_06d9;
					}
					catch (Exception ex3)
					{
						Log.Error("BFX walk failed: " + ex3);
						_fail++;
					}
					break;
					IL_06d9:
					yield return current4;
				}
			}
			s.TimeMode = savedMode;
			s.BiomeFx = savedFx;
			s.GpuRender = savedGpu;
			_mod.ForceRelight();
			_mod.DebugEyeSet = false;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info($"BFX {_pass} passed, {_fail} failed");
			Log.Info("BFX DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator Show(string name, BiomeKind kind, string tile, string tree, Vector2 mid, uint grassHorizon)
		{
			Settings s = _mod.Settings;
			Log.Info("BFX --- " + name);
			yield return Paint(mid, tile, tree);
			_mod.DebugEye = mid;
			_mod.DebugEyeSet = true;
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(4f);
			s.TimeMode = 1;
			_mod.ForceRelight();
			yield return new WaitForSecondsRealtime(7f);
			SceneCollector sc = _mod.SceneForTest;
			float num = sc.BiomeMix.Near[(uint)kind];
			int num2 = sc.BiomeForTest.AliveOf(kind);
			uint horizon = _mod.Renderer.Air.Horizon;
			int num3 = ColourDiff(horizon, grassHorizon);
			Log.Info($"BFX {name}: near {num:F2}, alive {num2}, horizon {Hex(horizon)} ({num3} from grass), shimmer {BiomePicture.Shimmer:F2} mirage {BiomePicture.Mirage:F2} warp {BiomePicture.Warp:F2}");
			Check(num > 0.6f, $"{name} biome read round you (near {num:F2})");
			Check(num2 > 0, $"{name} particles alive ({num2})");
			BiomeAir biomeAir = BiomeLooks.Air(kind);
			int num4 = ColourDiff(TerrainCache.Lerp(grassHorizon, biomeAir.Sky, biomeAir.Amount * num), grassHorizon);
			if (num4 >= 20)
			{
				Check((float)num3 >= (float)num4 * 0.35f, $"{name} air tinted ({num3}, about {num4} expected)");
			}
			if (kind == BiomeKind.Desert)
			{
				Check(BiomePicture.Shimmer > 0.3f && BiomePicture.Mirage > 0.3f, $"desert shimmer {BiomePicture.Shimmer:F2} mirage {BiomePicture.Mirage:F2}");
			}
			if (kind == BiomeKind.Singularity)
			{
				Check(BiomePicture.Warp > 0.5f, $"singularity warp {BiomePicture.Warp:F2}");
			}
			s.GpuRender = true;
			yield return new WaitForSecondsRealtime(0.6f);
			yield return Measure(60);
			float fullMs = _measured;
			_mod.SaveFrame("bfx_" + name + "_gpu.png");
			yield return new WaitForSecondsRealtime(0.4f);
			_mod.SaveFrame("bfx_" + name + "_gpu2.png");
			s.BiomeFx = 0;
			yield return new WaitForSecondsRealtime(0.8f);
			yield return Measure(60);
			float measured = _measured;
			_mod.SaveFrame("bfx_" + name + "_off.png");
			s.BiomeFx = 2;
			Log.Info($"BFX {name}: frame {fullMs:F1} ms with the effects, {measured:F1} ms without ({fullMs - measured:+0.0;-0.0} ms)");
			Check(fullMs - measured < 2.5f, $"{name} cost {fullMs - measured:F1} ms");
			yield return new WaitForSecondsRealtime(2.5f);
			s.GpuRender = false;
			yield return new WaitForSecondsRealtime(1f);
			_mod.SaveFrame("bfx_" + name + "_cpu.png");
			s.GpuRender = true;
			s.TimeMode = 3;
			_mod.ForceRelight();
			yield return new WaitForSecondsRealtime(4f);
			_mod.SaveFrame("bfx_" + name + "_night.png");
			Log.Info($"BFX {name}: night alive {sc.BiomeForTest.AliveOf(kind)}, scene lights {sc.Lights.Count}");
			s.TimeMode = 1;
			_mod.ForceRelight();
		}

		private IEnumerator Amounts(Vector2 mid)
		{
			Log.Info("BFX --- amount");
			Settings s = _mod.Settings;
			float saved = s.BiomeParticles;
			yield return Paint(mid, "flower_low", "flower_tree_1");
			_mod.DebugEye = mid;
			_mod.DebugEyeSet = true;
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(4f);
			s.TimeMode = 1;
			_mod.ForceRelight();
			SceneCollector sc = _mod.SceneForTest;
			Dictionary<float, int> alive = new Dictionary<float, int>();
			float[] array = new float[3] { 0.5f, 1f, 2f };
			for (int i = 0; i < array.Length; i++)
			{
				float amount = (s.BiomeParticles = array[i]);
				yield return new WaitForSecondsRealtime(9f);
				alive[amount] = sc.BiomeForTest.AliveOf(BiomeKind.Flower);
				_mod.SaveFrame($"bfx_amount_{amount * 100f:0}.png");
				Log.Info($"BFX amount {amount * 100f:0}%: alive {alive[amount]}");
			}
			s.BiomeParticles = 0f;
			yield return new WaitForSecondsRealtime(3f);
			int num = sc.BiomeForTest.AliveOf(BiomeKind.Flower);
			float num2 = sc.BiomeMix.Near[11];
			_mod.SaveFrame("bfx_amount_0.png");
			Log.Info($"BFX amount off: alive {num}, near {num2:F2}");
			Check((float)alive[1f] > (float)alive[0.5f] * 1.4f, $"amount: as made more than half ({alive[0.5f]} -> {alive[1f]})");
			Check((float)alive[2f] > (float)alive[1f] * 1.4f, $"amount: twice as many ({alive[1f]} -> {alive[2f]})");
			Check(num == 0, $"amount off: no particles ({num})");
			Check(num2 > 0.6f, $"amount off: the air kept (near {num2:F2})");
			s.BiomeParticles = saved;
		}

		private IEnumerator Walk(Vector2 mid)
		{
			Log.Info("BFX --- walk");
			TopTileType topTileType = AssetManager.top_tiles.get("crystal_low");
			TopTileType topTileType2 = AssetManager.top_tiles.get("grass_low");
			int num = (int)mid.x;
			int num2 = (int)mid.y;
			for (int i = num2 - 22; i <= num2 + 22; i++)
			{
				for (int j = num - 22; j <= num + 22; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile != null && tile.main_type != null && !tile.main_type.liquid && !tile.main_type.lava)
					{
						try
						{
							tile.setTopTileType((i < num2) ? topTileType : topTileType2);
						}
						catch
						{
						}
					}
				}
			}
			_mod.DebugEye = new Vector2(mid.x, mid.y + 22f - 2f);
			_mod.SetYaw(-MathF.PI / 2f);
			_mod.SetPitchDegrees(3f);
			yield return new WaitForSecondsRealtime(6f);
			SceneCollector sc = _mod.SceneForTest;
			float last = sc.BiomeMix.Near[16];
			float worstJump = 0f;
			float start = last;
			uint lastSky = _mod.Renderer.Air.Horizon;
			int worstSky = 0;
			int shot = 0;
			for (float t = 0f; t < 14f; t += Time.unscaledDeltaTime)
			{
				float b = mid.y + 22f - 2f - t * 2.6f;
				_mod.DebugEye = new Vector2(mid.x, Mathf.Max(mid.y - 22f + 2f, b));
				yield return null;
				float num3 = sc.BiomeMix.Near[16];
				worstJump = Mathf.Max(worstJump, Mathf.Abs(num3 - last));
				last = num3;
				uint horizon = _mod.Renderer.Air.Horizon;
				worstSky = Mathf.Max(worstSky, ColourDiff(horizon, lastSky));
				lastSky = horizon;
				if (t >= (float)shot * 2.8f)
				{
					_mod.SaveFrame($"bfx_walk_{shot}.png");
					Log.Info($"BFX walk t={t:F1}: near {num3:F2}, horizon {Hex(horizon)}, alive {sc.BiomeForTest.AliveOf(BiomeKind.Crystal)}");
					shot++;
				}
			}
			Log.Info($"BFX walk: near {start:F2} -> {last:F2}, worst step {worstJump:F3}, worst sky step {worstSky}");
			Check(last > start + 0.4f, $"walk: the crystal's share grew ({start:F2} -> {last:F2})");
			Check(worstJump < 0.05f, $"walk: no jump in the air (worst {worstJump:F3} a frame)");
			Check(worstSky <= 8, $"walk: the sky eased (worst {worstSky} a frame)");
		}

		private IEnumerator Paint(Vector2 mid, string tile, string tree)
		{
			int cx = (int)mid.x;
			int cy = (int)mid.y;
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item == null || !item.isAlive() || item.current_tile == null)
				{
					continue;
				}
				int value = item.current_tile.x - cx;
				int value2 = item.current_tile.y - cy;
				if (Mathf.Abs(value) > 24 || Mathf.Abs(value2) > 24)
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
			TopTileType topTileType = AssetManager.top_tiles.get(tile);
			if (topTileType == null)
			{
				Log.Error("BFX no tile " + tile);
				yield break;
			}
			for (int i = cy - 22; i <= cy + 22; i++)
			{
				for (int j = cx - 22; j <= cx + 22; j++)
				{
					WorldTile tile2 = World.world.GetTile(j, i);
					if (tile2 != null && tile2.main_type != null && !tile2.main_type.liquid && !tile2.main_type.lava)
					{
						try
						{
							tile2.setTopTileType(topTileType);
						}
						catch
						{
						}
					}
				}
			}
			yield return null;
			if (tree == null)
			{
				yield break;
			}
			System.Random random = new System.Random(tile.Length * 31);
			for (int k = 0; k < 16; k++)
			{
				int pX = cx - 14 + random.Next(29);
				int pY = cy + 5 + random.Next(14);
				if (k >= 11)
				{
					pX = cx + ((k % 2 != 0) ? 1 : (-1)) * (6 + random.Next(10));
					pY = cy - 4 + random.Next(8);
				}
				WorldTile tile3 = World.world.GetTile(pX, pY);
				if (tile3 == null || tile3.Type == null || tile3.Type.liquid || tile3.Type.block)
				{
					continue;
				}
				try
				{
					if (tile3.building != null)
					{
						tile3.building.startDestroyBuilding();
					}
					World.world.buildings.addBuilding(tree, tile3, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				}
				catch (Exception ex)
				{
					Log.Error("BFX tree " + tree + ": " + ex.Message);
					break;
				}
			}
		}

		private IEnumerator Measure(int frames)
		{
			List<float> all = new List<float>(frames);
			for (int i = 0; i < frames; i++)
			{
				yield return null;
				all.Add(_mod.FrameMs);
			}
			all.Sort();
			_measured = ((all.Count > 0) ? all[all.Count / 2] : 0f);
		}

		private Vector2? FlatSite(Vector2 near)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			if (terrainForTest == null || terrainForTest.Width == 0)
			{
				return null;
			}
			Vector2? result = null;
			float num = float.MaxValue;
			for (int i = 30; i < terrainForTest.Height - 22 - 8; i += 3)
			{
				for (int j = 30; j < terrainForTest.Width - 22 - 8; j += 3)
				{
					float num2 = float.MaxValue;
					float num3 = float.MinValue;
					int num4 = 0;
					for (int k = -22; k <= 22; k += 3)
					{
						for (int l = -22; l <= 22; l += 3)
						{
							int num5 = (i + k) * terrainForTest.Width + j + l;
							if ((terrainForTest.Flags[num5] & 3) != 0)
							{
								num4++;
								continue;
							}
							num2 = Mathf.Min(num2, terrainForTest.Ground[num5]);
							num3 = Mathf.Max(num3, terrainForTest.Ground[num5]);
						}
					}
					if (!(num2 > num3))
					{
						float num6 = (float)num4 * 4f + (num3 - num2) * 2f + Vector2.Distance(near, new Vector2(j, i)) * 0.01f;
						if (num6 < num)
						{
							num = num6;
							result = new Vector2((float)j + 0.5f, (float)i + 0.5f);
						}
					}
				}
			}
			if (result.HasValue)
			{
				Log.Info($"BFX site {result.Value}: score {num:F1}");
			}
			return result;
		}

		private static WorldTile DryTile(int x0, int y0)
		{
			for (int i = 0; i <= 14; i++)
			{
				for (int j = -i; j <= Mathf.Min(i, 2); j++)
				{
					for (int k = -i; k <= i; k++)
					{
						if (Mathf.Max(Mathf.Abs(k), Mathf.Abs(j)) == i)
						{
							WorldTile tile = World.world.GetTile(x0 + k, y0 + j);
							if (tile != null && tile.main_type != null && !tile.main_type.liquid && !tile.main_type.lava && tile.Type != null && !tile.Type.liquid && !tile.Type.lava && !tile.Type.block)
							{
								return tile;
							}
						}
					}
				}
			}
			return null;
		}

		private static int ColourDiff(uint a, uint b)
		{
			return Mathf.Abs((int)((a & 0xFF) - (b & 0xFF))) + Mathf.Abs((int)(((a >> 8) & 0xFF) - ((b >> 8) & 0xFF))) + Mathf.Abs((int)(((a >> 16) & 0xFF) - ((b >> 16) & 0xFF)));
		}

		private static string Hex(uint c)
		{
			return $"#{c & 0xFF:X2}{(c >> 8) & 0xFF:X2}{(c >> 16) & 0xFF:X2}";
		}
	}
}
