using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BiomeTest : MonoBehaviour
	{
		private sealed class Biome
		{
			public string Name;

			public string Tile;

			public string[] Trees;

			public string[] Plants;

			public string[] Lumps;

			public string[] Units;
		}

		private WorldBoxMod _mod;

		private static readonly Biome[] Biomes = new Biome[22]
		{
			B("savanna", "savanna_low", "savanna_tree_1,savanna_tree_2,savanna_tree_big_1,savanna_tree_big_2", "savanna_plant", "fruit_bush,mineral_stone", "rhino,ostrich"),
			B("jungle", "jungle_low", "jungle_tree", "jungle_plant,jungle_flower", "fruit_bush,mineral_gems", "monkey,frog"),
			B("desert", "desert_low", "desert_tree,cacti_tree", "desert_plant", "mineral_gems", "scorpion"),
			B("lemon", "lemon_low", "lemon_tree", "green_herb,flower,mushroom_yellow", "fruit_bush", "lemon_snail,civ_lemon_man"),
			B("permafrost", "permafrost_low", "pine_tree", "snow_plant", "mineral_stone", "snowman,cold_one"),
			B("swamp", "swamp_low", "swamp_tree", "swamp_plant,swamp_plant_big", "mineral_stone", "crocodile"),
			B("crystal", "crystal_low", "crystal_tree", "crystal_plant", "mineral_gems", "crystal_sword,civ_crystal_golem"),
			B("enchanted", "enchanted_low", "enchanted_tree", "flower", "fruit_bush", "fairy,white_mage"),
			B("corrupted", "corrupted_low", "corrupted_tree,corrupted_tree_big", "corrupted_plant", "mineral_bones", "jumpy_skull,necromancer"),
			B("infernal", "infernal_low", "infernal_tree,infernal_tree_big,infernal_tree_small", "flame_flower", "mineral_adamantine", "fire_skull,demon"),
			B("candy", "candy_low", "candy_tree", "candy_plant", "mineral_stone", "smore,civ_candy_man"),
			B("mushroom", "mushroom_low", "mushroom_tree", "mushroom_red,mushroom_green,mushroom_teal,mushroom_white,mushroom_yellow", "fruit_bush", "frog"),
			B("wasteland", "wasteland_low", "wasteland_tree", "wasteland_flower", "mineral_stone", "acid_blob,civ_acid_gentleman"),
			B("birch", "birch_low", "birch_tree", "birch_plant,green_herb,mushroom_white", "fruit_bush", "rabbit"),
			B("maple", "maple_low", "maple_tree", "maple_plant,mushroom_red", "fruit_bush", "dog"),
			B("rocklands", "rocklands_low", "rocklands_tree", "rocklands_plant", "mineral_stone,mineral_gems", "goat"),
			B("garlic", "garlic_low", "garlic_tree", "garlic_plant", "fruit_bush", "garl,civ_garlic_man"),
			B("flower", "flower_low", "flower_tree_1,flower_tree_2,flower_tree_3", "flower_plant,flower", "fruit_bush", "flower_bud,civ_liliar"),
			B("celestial", "celestial_low", "celestial_tree,celestial_tree_small", "celestial_plant", "mineral_mythril", "unicorn"),
			B("clover", "clover_low", "clover_tree", "clover_plant", "fruit_bush", "cow"),
			B("singularity", "singularity_low", "singularity_tree", "singularity_plant", "mineral_stone", "angle"),
			B("paradox", "paradox_low", "paradox_tree", "paradox_plant", "mineral_mythril", "alien")
		};

		private const int W = 26;

		private const int D = 18;

		private static readonly string[] WallTypes = new string[7] { "wall_order", "wall_ancient", "wall_evil", "wall_iron", "wall_green", "wall_light", "wall_wild" };

		private static Biome B(string name, string tile, string trees, string plants, string lumps, string units)
		{
			return new Biome
			{
				Name = name,
				Tile = tile,
				Trees = trees.Split(','),
				Plants = plants.Split(','),
				Lumps = lumps.Split(','),
				Units = ((units.Length > 0) ? units.Split(',') : new string[0])
			};
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
			int num = 0;
			int num2 = 0;
			Biome[] biomes = Biomes;
			for (int i = 0; i < biomes.Length; i++)
			{
				string[] trees = biomes[i].Trees;
				for (int j = 0; j < trees.Length; j++)
				{
					string text = Flora.TreeFamily(trees[j]);
					if (text != null)
					{
						num2++;
						if (Models.Get(text) != null)
						{
							num++;
						}
					}
				}
			}
			Log.Info(string.Format("BIOMES world {0}x{1}, models {2}, {3} of {4} biome tree models", MapBox.width, MapBox.height, Models.Ready ? "ready" : "missing", num, num2));
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
				Log.Error("BIOMES could not found a kingdom");
				Application.Quit();
				yield break;
			}
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("BIOMES first person did not start");
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
			Vector2? site = FlatSite(king.current_position);
			if (!site.HasValue)
			{
				Log.Error("BIOMES no flat open land");
				Application.Quit();
				yield break;
			}
			Settings s = _mod.Settings;
			int savedMode = s.TimeMode;
			s.TimeMode = 1;
			_mod.ForceRelight();
			int summer = -1;
			if (options.Contains("summer"))
			{
				for (int k = 0; k < 8; k++)
				{
					if (summer >= 0)
					{
						break;
					}
					if (Calendar.SeasonOf(k, out var dayIn, out var _) == Season.Summer && dayIn == 0)
					{
						summer = k + 16;
					}
				}
			}
			Biome[] biomes2 = Biomes;
			foreach (Biome b in biomes2)
			{
				if (options.Contains("only") && !options.Contains("only" + b.Name))
				{
					continue;
				}
				if (summer >= 0 && Calendar.Day != summer)
				{
					Calendar.SetDayForTest(summer);
					yield return new WaitForSecondsRealtime(2f);
					try
					{
						_mod.SamplerForTest.RelightAll();
					}
					catch
					{
					}
				}
				yield return Run(b.Name, Show(b, king, site.Value, !options.Contains("nounits")));
			}
			if (options.Contains("walls"))
			{
				yield return Run("walls", Walls(king, site.Value));
			}
			if (options.Contains("giant"))
			{
				yield return Run("giant", Giant(king, site.Value));
			}
			s.TimeMode = savedMode;
			_mod.ForceRelight();
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("BIOMES DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("BIOMES --- " + name);
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
					Log.Error("BIOMES " + name + " failed: " + ex);
				}
				break;
				IL_007b:
				yield return current;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			_mod.DebugEyeSet = false;
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
			for (int i = 12; i < terrainForTest.Height - 18 - 12; i += 2)
			{
				for (int j = 12; j < terrainForTest.Width - 26 - 12; j += 2)
				{
					float num2 = float.MaxValue;
					float num3 = float.MinValue;
					int num4 = 0;
					for (int k = -8; k < 26; k += 2)
					{
						for (int l = -2; l < 28; l += 2)
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
						float num6 = (float)num4 * 4f + (num3 - num2) + Vector2.Distance(near, new Vector2(j, i)) * 0.01f;
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
				Log.Info($"BIOMES site {result.Value}: score {num:F1}");
			}
			return result;
		}

		private IEnumerator Walls(Actor king, Vector2 site)
		{
			int sx = (int)site.x + 6;
			int sy = (int)site.y + 4;
			List<Vector2Int> fort = new List<Vector2Int>();
			for (int i = 0; i < 10; i++)
			{
				if (i < 4 || i > 5)
				{
					fort.Add(new Vector2Int(i, 0));
				}
				fort.Add(new Vector2Int(i, 7));
			}
			for (int j = 1; j < 7; j++)
			{
				fort.Add(new Vector2Int(0, j));
				fort.Add(new Vector2Int(9, j));
			}
			for (int k = 4; k < 7; k++)
			{
				fort.Add(new Vector2Int(5, k));
			}
			fort.Add(new Vector2Int(13, 3));
			fort.Add(new Vector2Int(12, 0));
			fort.Add(new Vector2Int(13, 0));
			TopTileType grass = AssetManager.top_tiles.get("grass_low");
			Settings st = _mod.Settings;
			ClearUnits(new Vector2((float)sx + 6f, (float)sy + 3f), 40f, king);
			string[] wallTypes = WallTypes;
			foreach (string type in wallTypes)
			{
				TopTileType topTileType = AssetManager.top_tiles.get(type);
				if (topTileType == null)
				{
					Log.Error("BIOMES no wall type " + type);
					continue;
				}
				foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
				{
					if (item == null || !item.isAlive() || item.current_tile == null)
					{
						continue;
					}
					int num = item.current_tile.x - sx;
					int num2 = item.current_tile.y - sy;
					if (num < -3 || num > 16 || num2 < -12 || num2 > 12)
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
				for (int m = sy - 12; m <= sy + 12; m++)
				{
					for (int n = sx - 3; n <= sx + 16; n++)
					{
						WorldTile tile = World.world.GetTile(n, m);
						if (tile != null && tile.main_type != null && !tile.main_type.liquid && grass != null)
						{
							try
							{
								tile.setTopTileType(grass);
							}
							catch
							{
							}
						}
					}
				}
				int num3 = 0;
				foreach (Vector2Int item2 in fort)
				{
					WorldTile tile2 = World.world.GetTile(sx + item2.x, sy + item2.y);
					if (tile2 != null && tile2.main_type != null && !tile2.main_type.liquid)
					{
						try
						{
							tile2.setTopTileType(topTileType);
							num3++;
						}
						catch
						{
						}
					}
				}
				Log.Info($"BIOMES walls {type}: {num3} tiles at {sx},{sy}");
				if (!king.isAlive() || !_mod.IsFirstPerson)
				{
					Log.Error("BIOMES the king is gone; stopping the walls");
					break;
				}
				try
				{
					king.setCurrentTilePosition(World.world.GetTile(sx + 5, sy - 14));
				}
				catch
				{
				}
				st.TimeMode = 1;
				_mod.ForceRelight();
				yield return new WaitForSecondsRealtime(2f);
				_mod.DebugEyeSet = true;
				string name = type.Substring(5);
				_mod.DebugEye = new Vector2((float)sx + 5f, (float)sy + 17f);
				_mod.SetYaw(-MathF.PI / 2f);
				_mod.SetPitchDegrees(4f);
				yield return new WaitForSecondsRealtime(1.2f);
				_mod.SaveFrame("walls_" + name + "_lit.png");
				_mod.DebugEye = new Vector2((float)sx + 5f, (float)sy - 6f);
				_mod.SetYaw(MathF.PI / 2f);
				_mod.SetPitchDegrees(8f);
				yield return new WaitForSecondsRealtime(1.2f);
				_mod.SaveFrame("walls_" + name + "_gate.png");
				ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "walls_" + name + "_screen.png"));
				yield return new WaitForSecondsRealtime(0.5f);
				yield return TimeFrames(name, "pieces");
				SceneCollector.WallModelsOffForTest = true;
				yield return TimeFrames(name, "blocks");
				SceneCollector.WallModelsOffForTest = false;
				_mod.DebugEye = new Vector2((float)sx + 2.5f, (float)sy + 2.5f);
				_mod.SetYaw(MathF.PI / 4f);
				_mod.SetPitchDegrees(12f);
				yield return new WaitForSecondsRealtime(1.2f);
				_mod.SaveFrame("walls_" + name + "_yard.png");
				_mod.DebugEye = new Vector2((float)sx + 12.2f, (float)sy + 10.2f);
				_mod.SetYaw(3.926991f);
				_mod.SetPitchDegrees(10f);
				yield return new WaitForSecondsRealtime(1.2f);
				_mod.SaveFrame("walls_" + name + "_corner.png");
				st.TimeMode = 2;
				_mod.ForceRelight();
				_mod.DebugEye = new Vector2((float)sx - 4f, (float)sy - 5f);
				_mod.SetYaw(MathF.PI / 4f);
				_mod.SetPitchDegrees(6f);
				yield return new WaitForSecondsRealtime(1.5f);
				_mod.SaveFrame("walls_" + name + "_golden.png");
				st.TimeMode = 1;
				_mod.ForceRelight();
				foreach (Vector2Int item3 in fort)
				{
					WorldTile tile3 = World.world.GetTile(sx + item3.x, sy + item3.y);
					if (tile3 != null && grass != null)
					{
						try
						{
							tile3.setTopTileType(grass);
						}
						catch
						{
						}
					}
				}
				yield return new WaitForSecondsRealtime(0.5f);
			}
		}

		private IEnumerator Giant(Actor king, Vector2 site)
		{
			Vector2 c = new Vector2((float)((int)site.x + 13) + 0.5f, (float)((int)site.y + 9) + 0.5f);
			int cx = (int)c.x;
			int cy = (int)c.y;
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item == null || !item.isAlive() || item.current_tile == null || (new Vector2(item.current_tile.x, item.current_tile.y) - c).sqrMagnitude > 1600f)
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
			TopTileType pTopTile = AssetManager.top_tiles.get("celestial_low");
			for (int i = cy - 26; i <= cy + 26; i++)
			{
				for (int j = cx - 26; j <= cx + 26; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile != null && tile.main_type != null && !tile.main_type.liquid && !tile.main_type.lava && (j - cx) * (j - cx) + (i - cy) * (i - cy) <= 676)
					{
						try
						{
							tile.setTopTileType(pTopTile);
						}
						catch
						{
						}
					}
				}
			}
			yield return null;
			List<Building> placed = new List<Building>();
			Building tree = Put("celestial_tree", cx, cy);
			if (tree == null)
			{
				Log.Error("BIOMES giant: no celestial_tree");
				yield break;
			}
			placed.Add(tree);
			for (int k = 0; k < 4; k++)
			{
				float f = (float)k * MathF.PI * 0.5f + 0.6f;
				Building building = Put("celestial_tree_small", cx + Mathf.RoundToInt(Mathf.Cos(f) * 17f), cy + Mathf.RoundToInt(Mathf.Sin(f) * 17f));
				if (building != null)
				{
					placed.Add(building);
				}
			}
			for (int l = 0; l < 10; l++)
			{
				Building building2 = Put("celestial_plant", cx - 6 + l, cy - 9 - l % 3);
				if (building2 != null)
				{
					placed.Add(building2);
				}
			}
			ClearUnits(c, 40f, king);
			Vector2 dir = Vector2.down;
			float reach = 0f;
			for (int m = 0; m < 8; m++)
			{
				Vector2 vector = new Vector2(Mathf.Cos((float)m * MathF.PI * 0.25f), Mathf.Sin((float)m * MathF.PI * 0.25f));
				float num;
				for (num = 4f; num <= 60f; num += 1f)
				{
					WorldTile tile2 = World.world.GetTile((int)(c.x + vector.x * num), (int)(c.y + vector.y * num));
					if (tile2 == null || tile2.main_type == null || tile2.main_type.liquid || tile2.main_type.lava)
					{
						break;
					}
				}
				if (num > reach)
				{
					reach = num;
					dir = vector;
				}
			}
			float yaw = Mathf.Atan2(0f - dir.y, 0f - dir.x);
			Vector2 side = new Vector2(0f - dir.y, dir.x);
			try
			{
				king.setCurrentTilePosition(World.world.GetTile((int)(c.x + dir.x * 30f), (int)(c.y + dir.y * 30f)));
			}
			catch
			{
			}
			yield return new WaitForSecondsRealtime(3f);
			Log.Info($"BIOMES giant: tree at {c}, scale {tree.current_scale}, base {tree.asset.scale_base}, {placed.Count} placed; looking from {dir} (dry for {reach:F0} tiles)");
			_mod.SceneForTest.DumpNear(c, 2f);
			_mod.DebugEyeSet = true;
			float[] array = new float[4] { 55f, 35f, 20f, 10f };
			for (int n = 0; n < array.Length; n++)
			{
				float far = array[n];
				if (!(far > reach - 1f))
				{
					Vector2 debugEye = c + dir * far;
					try
					{
						king.setCurrentTilePosition(World.world.GetTile((int)debugEye.x, (int)debugEye.y));
					}
					catch
					{
					}
					_mod.DebugEye = debugEye;
					_mod.SetYaw(yaw);
					_mod.SetPitchDegrees((far > 30f) ? 10f : ((far > 15f) ? 24f : 38f));
					yield return new WaitForSecondsRealtime(1.5f);
					_mod.SaveFrame($"giant_{far:F0}.png");
					Log.Info($"BIOMES giant: frame at {far:F0} tiles");
					_mod.SceneForTest.DumpNear(c, 2f);
				}
			}
			Vector2 debugEye2 = c + dir * 7f + side * 4f;
			try
			{
				king.setCurrentTilePosition(World.world.GetTile((int)debugEye2.x, (int)debugEye2.y));
			}
			catch
			{
			}
			_mod.DebugEye = debugEye2;
			_mod.SetYaw(Mathf.Atan2(c.y - debugEye2.y, c.x - debugEye2.x));
			_mod.SetPitchDegrees(55f);
			yield return new WaitForSecondsRealtime(1.5f);
			_mod.SaveFrame("giant_under.png");
			_mod.SetPitchDegrees(8f);
			yield return new WaitForSecondsRealtime(1.2f);
			_mod.SaveFrame("giant_trunk.png");
			_mod.DebugEyeSet = false;
			try
			{
				king.setCurrentTilePosition(World.world.GetTile((int)(c.x + dir.x * 9f), (int)(c.y + dir.y * 9f)));
			}
			catch
			{
			}
			yield return new WaitForSecondsRealtime(1f);
			_mod.SetYaw(yaw);
			_mod.SetPitchDegrees(5f);
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			for (float far = 0f; far < 5f; far += 0.5f)
			{
				yield return new WaitForSecondsRealtime(0.5f);
				_mod.SetYaw(yaw);
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			Log.Info($"BIOMES giant: walked in to {(king.current_position - c).magnitude:F2} tiles from the trunk's middle (at {king.current_position})");
			yield return new WaitForSecondsRealtime(0.8f);
			_mod.SaveFrame("giant_walked.png");
			foreach (Building item2 in placed)
			{
				try
				{
					if (item2 != null && item2.isAlive())
					{
						item2.removeBuildingFinal();
					}
				}
				catch
				{
				}
			}
		}

		private static void ClearUnits(Vector2 at, float radius, Actor king)
		{
			int num = 0;
			foreach (Actor item in new List<Actor>(World.world.units.getSimpleList()))
			{
				if (item != null && item != king && item.isAlive() && !((item.current_position - at).sqrMagnitude > radius * radius))
				{
					try
					{
						item.dieSimpleNone();
						num++;
					}
					catch
					{
					}
				}
			}
			if (num > 0)
			{
				Log.Info($"BIOMES cleared {num} creatures");
			}
		}

		private IEnumerator TimeFrames(string wall, string what)
		{
			yield return new WaitForSecondsRealtime(0.6f);
			double mod = 0.0;
			double models = 0.0;
			int frames = 0;
			float start = Time.unscaledTime;
			while (Time.unscaledTime - start < 2f)
			{
				yield return null;
				mod += (double)_mod.FrameMs;
				models += (double)_mod.Renderer.MeshMs;
				frames++;
			}
			Log.Info($"BIOMES walls {wall} {what}: mod {mod / (double)Math.Max(1, frames):F2} ms, models {models / (double)Math.Max(1, frames):F2} ms, {_mod.SceneForTest.Meshes.Count} meshes, {frames} frames");
		}

		private static Building Put(string id, int x, int y)
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
				return World.world.buildings.addBuilding(id, tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
			}
			catch (Exception ex)
			{
				Log.Error("BIOMES building " + id + ": " + ex.Message);
				return null;
			}
		}

		private IEnumerator Show(Biome bio, Actor king, Vector2 site, bool units)
		{
			int sx = (int)site.x;
			int sy = (int)site.y;
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item == null || !item.isAlive() || item.current_tile == null)
				{
					continue;
				}
				int num = item.current_tile.x - sx;
				int num2 = item.current_tile.y - sy;
				if (num < -2 || num > 28 || num2 < -6 || num2 > 20)
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
			TopTileType topTileType = AssetManager.top_tiles.get(bio.Tile);
			if (topTileType == null)
			{
				Log.Error("BIOMES no tile " + bio.Tile);
				yield break;
			}
			for (int i = sy - 6; i < sy + 18 + 2; i++)
			{
				for (int j = sx - 2; j < sx + 26 + 2; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile != null && tile.main_type != null && !tile.main_type.liquid && !tile.main_type.lava)
					{
						try
						{
							tile.setTopTileType(topTileType);
						}
						catch
						{
						}
					}
				}
			}
			try
			{
				_mod.TerrainForTest?.Snow?.Clear();
			}
			catch
			{
			}
			yield return null;
			List<Building> placed = new List<Building>();
			int num3 = 0;
			for (int k = 0; k < 3; k++)
			{
				string[] trees = bio.Trees;
				foreach (string id in trees)
				{
					for (int m = 0; m < ((bio.Trees.Length == 1) ? 3 : ((bio.Trees.Length > 2) ? 1 : 2)); m++)
					{
						int x = sx + 2 + (num3 * 5 + k * 2) % 23;
						int y = sy + 8 + k * 3 + num3 % 2;
						Building building = Put(id, x, y);
						if (building != null)
						{
							placed.Add(building);
						}
						num3++;
					}
				}
			}
			for (int n = 0; n < bio.Plants.Length; n++)
			{
				for (int num4 = 0; num4 < 4; num4++)
				{
					Building building2 = Put(bio.Plants[n], sx + 6 + n * 3 + num4 % 2, sy + 3 + num4);
					if (building2 != null)
					{
						placed.Add(building2);
					}
				}
			}
			for (int num5 = 0; num5 < bio.Lumps.Length; num5++)
			{
				Building building3 = Put(bio.Lumps[num5], (num5 == 0) ? sx : (sx + 26 - 1), sy + 1);
				if (building3 != null)
				{
					placed.Add(building3);
				}
			}
			string tower = ((bio.Name == "singularity") ? "angle_tower" : ((bio.Name == "infernal") ? "flame_tower" : ((bio.Name == "permafrost") ? "ice_tower" : null)));
			if (tower != null)
			{
				Building building4 = Put(tower, sx + 13, sy + 18 + 9);
				if (building4 != null)
				{
					placed.Add(building4);
				}
			}
			List<Actor> spawned = new List<Actor>();
			if (units)
			{
				for (int num6 = 0; num6 < bio.Units.Length; num6++)
				{
					for (int num7 = 0; num7 < 2; num7++)
					{
						WorldTile tile2 = World.world.GetTile(sx + 9 + num6 * 4 + num7, sy + 2 + num7);
						try
						{
							Actor actor = World.world.units.spawnNewUnit(bio.Units[num6], tile2, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
							if (actor != null)
							{
								spawned.Add(actor);
								try
								{
									actor.makeWait(40f);
								}
								catch
								{
								}
							}
						}
						catch (Exception ex)
						{
							Log.Error("BIOMES unit " + bio.Units[num6] + ": " + ex.Message);
						}
					}
				}
			}
			Log.Info(string.Format("BIOMES {0}: placed {1} ({2}), {3} creatures", bio.Name, placed.Count, string.Join(", ", placed.ConvertAll((Building b) => b.asset.id)), spawned.Count));
			try
			{
				king.setCurrentTilePosition(World.world.GetTile(sx + 13, sy - 9));
			}
			catch
			{
			}
			yield return new WaitForSecondsRealtime(3f);
			_mod.DebugEye = new Vector2((float)sx + 13f, (float)sy - 4.5f);
			_mod.DebugEyeSet = true;
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(4f);
			yield return new WaitForSecondsRealtime(1.5f);
			_mod.SaveFrame("biome_" + bio.Name + "_south.png");
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "biome_" + bio.Name + "_screen.png"));
			yield return new WaitForSecondsRealtime(0.5f);
			_mod.DebugEye = new Vector2((float)sx + 13f, (float)(sy + 18) + 4f);
			_mod.SetYaw(-MathF.PI / 2f);
			_mod.SetPitchDegrees(2f);
			yield return new WaitForSecondsRealtime(1.2f);
			_mod.SaveFrame("biome_" + bio.Name + "_north.png");
			if (tower != null)
			{
				_mod.DebugEye = new Vector2((float)sx + 13f + 1.5f, (float)(sy + 18) + 1f);
				_mod.SetYaw(MathF.PI / 2f);
				_mod.SetPitchDegrees(10f);
				yield return new WaitForSecondsRealtime(1.2f);
				_mod.SaveFrame("biome_" + bio.Name + "_" + tower + ".png");
			}
			_mod.DebugEye = new Vector2((float)sx + 6.5f + (float)(bio.Plants.Length - 1) * 1.5f, (float)sy + 1.6f);
			_mod.DebugEyeHeight = 0.6f;
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(-16f);
			yield return new WaitForSecondsRealtime(1.2f);
			_mod.SaveFrame("biome_" + bio.Name + "_plants.png");
			_mod.DebugEyeHeight = 0f;
			Building building5 = null;
			foreach (Building item2 in placed)
			{
				if (item2 != null && item2.isAlive() && item2.asset != null && item2.asset.type == "type_tree" && (building5 == null || Mathf.Abs(item2.current_tile.x - (sx + 13)) < Mathf.Abs(building5.current_tile.x - (sx + 13))))
				{
					building5 = item2;
				}
			}
			if (building5 != null)
			{
				Vector2 vector = new Vector2((float)building5.current_tile.x + 0.5f, (float)building5.current_tile.y + 0.5f);
				Vector2 debugEye = vector + new Vector2(0.8f, -4.2f);
				_mod.DebugEye = debugEye;
				_mod.SetYaw(Mathf.Atan2(vector.y - debugEye.y, vector.x - debugEye.x));
				_mod.SetPitchDegrees(10f);
				yield return new WaitForSecondsRealtime(1.2f);
				_mod.SaveFrame("biome_" + bio.Name + "_tree.png");
			}
			string[] units2 = bio.Units;
			foreach (string kind in units2)
			{
				Actor a = spawned.Find((Actor actor2) => actor2 != null && actor2.isAlive() && actor2.asset != null && actor2.asset.id == kind);
				if (a != null)
				{
					try
					{
						a.makeWait();
						a.stopMovement();
					}
					catch
					{
					}
					Vector2 at = a.current_position;
					Vector2 debugEye2 = at + new Vector2(0.35f, -1.35f);
					_mod.DebugEye = debugEye2;
					_mod.DebugEyeHeight = 0.7f;
					_mod.SetYaw(Mathf.Atan2(at.y - debugEye2.y, at.x - debugEye2.x));
					_mod.SetPitchDegrees(-2f);
					yield return new WaitForSecondsRealtime(0.25f);
					_mod.SaveFrame("biome_" + bio.Name + "_" + kind + ".png");
					Log.Info($"BIOMES {bio.Name}: {kind} at {at}, moved {(a.current_position - at).magnitude:F2} while framing");
					Log.Info("BIOMES " + bio.Name + ": " + kind + " drawn as " + (Models.CreatureFamily(a) ?? Models.PersonFamily(a)));
				}
			}
			_mod.DebugEyeHeight = 0f;
			_mod.SceneForTest.DumpNear(new Vector2((float)sx + 13f, (float)sy + 9f), 16f);
			foreach (Building item3 in placed)
			{
				try
				{
					if (item3 != null && item3.isAlive())
					{
						item3.removeBuildingFinal();
					}
				}
				catch
				{
				}
			}
			foreach (Actor item4 in spawned)
			{
				try
				{
					if (item4 != null && item4.isAlive())
					{
						item4.dieSimpleNone();
					}
				}
				catch
				{
				}
			}
			ClearUnits(new Vector2((float)sx + 13f, (float)sy + 9f), 34f, king);
			yield return new WaitForSecondsRealtime(0.5f);
		}
	}
}
