using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BurntTest : MonoBehaviour
	{
		private static readonly string[] Kinds = new string[35]
		{
			"tree_green_1", "tree_green_2", "tree_green_3", "birch_tree", "maple_tree", "pine_tree", "jungle_tree", "swamp_tree", "savanna_tree_1", "savanna_tree_2",
			"savanna_tree_big_1", "savanna_tree_big_2", "palm_tree", "desert_tree", "cacti_tree", "lemon_tree", "mushroom_tree", "wasteland_tree", "rocklands_tree", "garlic_tree",
			"clover_tree", "crystal_tree", "enchanted_tree", "corrupted_tree", "corrupted_tree_big", "infernal_tree", "infernal_tree_big", "infernal_tree_small", "candy_tree", "flower_tree_1",
			"flower_tree_2", "flower_tree_3", "singularity_tree", "paradox_tree", "celestial_tree_small"
		};

		private static readonly string[] Burning = new string[4] { "tree_green_1", "pine_tree", "palm_tree", "birch_tree" };

		private const int Cols = 7;

		private const int Rows = 5;

		private const int StepX = 4;

		private const int StepY = 5;

		private const int W = 32;

		private const int D = 39;

		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

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
			Log.Info("BURNT " + (ok ? "PASS" : "FAIL") + " " + what);
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
			Log.Info(string.Format("BURNT world {0}x{1}, models {2}, gpu {3}", MapBox.width, MapBox.height, Models.Ready ? "ready" : "missing", _mod.Settings.GpuRender));
			int num = 0;
			string[] kinds = Kinds;
			foreach (string text in kinds)
			{
				string text2 = Models.TreeFamily(text, 0);
				string text3 = BurntTrees.FamilyOf(text2);
				if (Models.Get(text3) != null && Models.Get(text3 + "_m") != null && Models.Get(text3 + "_f") != null)
				{
					num++;
					continue;
				}
				Log.Info("BURNT no burnt model for " + text + " (" + text2 + " -> " + text3 + ")");
			}
			Check(num == Kinds.Length, $"burnt models (near, middle, far) for {num} of {Kinds.Length} kinds");
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
				Log.Error("BURNT could not found a kingdom");
				Finish(quit);
				yield break;
			}
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("BURNT first person did not start");
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
			Vector2? vector = FlatSite(king.current_position);
			if (!vector.HasValue)
			{
				Log.Error("BURNT no flat open land");
				Finish(quit);
				yield break;
			}
			Settings s = _mod.Settings;
			int savedMode = s.TimeMode;
			bool savedGpu = s.GpuRender;
			s.TimeMode = 1;
			_mod.ForceRelight();
			IEnumerator run = Run(king, vector.Value, options);
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
					goto IL_0419;
				}
				catch (Exception ex)
				{
					Log.Error("BURNT failed: " + ex);
				}
				break;
				IL_0419:
				yield return current2;
			}
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			s.TimeMode = savedMode;
			s.GpuRender = savedGpu;
			_mod.ForceRelight();
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Finish(quit);
		}

		private void Finish(bool quit)
		{
			Log.Info($"BURNT DONE pass {_pass} fail {_fail}");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator Run(Actor king, Vector2 site, string options)
		{
			int sx = (int)site.x;
			int sy = (int)site.y;
			SceneCollector scene = _mod.SceneForTest;
			TopTileType topTileType = AssetManager.top_tiles.get("grass_low");
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item == null || !item.isAlive() || item.current_tile == null)
				{
					continue;
				}
				int num = item.current_tile.x - sx;
				int num2 = item.current_tile.y - sy;
				if (num < -4 || num > 36 || num2 < -4 || num2 > 43)
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
			for (int i = sy - 2; i <= sy + 39 + 2; i++)
			{
				for (int j = sx - 2; j <= sx + 32 + 2; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile != null && tile.main_type != null && !tile.main_type.liquid && !tile.main_type.lava && topTileType != null)
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
			yield return null;
			int ox = sx + 2;
			int oy = sy + 12;
			List<Building> stand = new List<Building>();
			List<string> kinds = new List<string>();
			for (int k = 0; k < Kinds.Length; k++)
			{
				Building building = Put(Kinds[k], ox + k % 7 * 4, oy + k / 7 * 5);
				if (building != null)
				{
					stand.Add(building);
					kinds.Add(Kinds[k]);
				}
			}
			Check(stand.Count >= Kinds.Length - 2, $"planted {stand.Count} of {Kinds.Length} kinds");
			ClearUnits(new Vector2((float)sx + 16f, (float)sy + 19.5f), 50f, king);
			Park(king, sx + 16, sy + 39 + 16);
			yield return new WaitForSecondsRealtime(2.5f);
			Vector2 over = new Vector2((float)ox + 14f - 2f, (float)(oy + 25) + 16f);
			yield return Shots("living", ox, oy, over, options.Contains("cpu"));
			yield return TimeFrames("living", over);
			int ruined = 0;
			int alive = 0;
			for (int l = 0; l < stand.Count; l++)
			{
				Building building2 = stand[l];
				bool flag;
				try
				{
					flag = building2 != null && building2.isAlive() && building2.asset != null;
				}
				catch
				{
					flag = false;
				}
				if (!flag)
				{
					Log.Info("BURNT " + kinds[l] + " was gone before it could be burnt (the game took it)");
					continue;
				}
				alive++;
				try
				{
					building2.startMakingRuins();
				}
				catch (Exception ex)
				{
					Log.Error("BURNT ruin " + kinds[l] + ": " + ex.Message);
				}
			}
			yield return new WaitForSecondsRealtime(2.5f);
			foreach (Building item2 in stand)
			{
				if (item2 != null && item2.isAlive() && item2.isRuin())
				{
					ruined++;
				}
			}
			Check(ruined >= alive - 1 && alive > 0, $"{ruined} of {alive} trees still standing are the game's ruins (burnt sprites)");
			yield return Shots("burnt", ox, oy, over, options.Contains("cpu"));
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = 6f;
			_mod.DebugEye = new Vector2((float)ox + 14f, (float)oy - 3f);
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(-14f);
			yield return new WaitForSecondsRealtime(1.5f);
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			for (int m = 0; m < stand.Count; m++)
			{
				Building building3 = stand[m];
				if (building3 != null && building3.isAlive() && building3.isRuin())
				{
					num5++;
					string text = scene.ModelNameForTest(building3);
					if (text?.StartsWith("burnt_", StringComparison.Ordinal) ?? false)
					{
						num3++;
					}
					if (scene.SwayForTest(building3) == 0f)
					{
						num4++;
					}
					Log.Info(string.Format("BURNT {0}: drawn as {1}, sway {2:F3}", kinds[m], text ?? "-", scene.SwayForTest(building3)));
				}
			}
			Check(num3 == num5 && num5 > 0, $"{num3} of {num5} burnt trees drawn with burnt models");
			Check(num4 == num5 && num5 > 0, $"{num4} of {num5} burnt trees stand still (no sway)");
			yield return TimeFrames("burnt", over);
			Building oak = stand.Find((Building b) => b != null && b.isAlive() && b.isRuin() && b.asset.id == "tree_green_1");
			string verb;
			string sound;
			if (oak != null)
			{
				Check(Work.Workable(oak, out verb, out sound, out var hits) && hits < 6, "a burnt oak can be chopped, quicker than a living one");
			}
			Building fresh = Put("tree_green_3", sx + 16, sy + 2);
			yield return null;
			if (fresh != null)
			{
				Check(Work.Workable(fresh, out sound, out verb, out var _), "a living oak can still be chopped");
			}
			Building gone = stand.Find((Building b) => b != null && b.isAlive() && b.isRuin() && b.asset.id == "maple_tree") ?? oak;
			if (gone != null)
			{
				Vector2 vector = new Vector2((float)gone.current_tile.x + 0.5f, (float)gone.current_tile.y + 0.5f);
				_mod.DebugEyeHeight = 0f;
				_mod.DebugEye = vector + new Vector2(0f, 7f);
				_mod.SetYaw(-MathF.PI / 2f);
				_mod.SetPitchDegrees(6f);
				yield return new WaitForSecondsRealtime(1.5f);
				_mod.SaveFrame("burnt_crumble_0.png");
				try
				{
					gone.startDestroyBuilding();
				}
				catch (Exception ex2)
				{
					Log.Error("BURNT destroy: " + ex2.Message);
				}
				yield return null;
				yield return null;
				int crumbling = scene.CrumblesForTest;
				yield return new WaitForSecondsRealtime(0.3f);
				_mod.SaveFrame("burnt_crumble_1.png");
				yield return new WaitForSecondsRealtime(0.4f);
				_mod.SaveFrame("burnt_crumble_2.png");
				yield return new WaitForSecondsRealtime(1.2f);
				_mod.SaveFrame("burnt_crumble_3.png");
				Check(crumbling > 0, $"a burnt tree taken away crumbles ({crumbling} crumbling)");
			}
			if (!_mod.IsFirstPerson || !king.isAlive())
			{
				Log.Error("BURNT first person ended before the fire (the body died?): fire checks skipped");
				_fail++;
				yield break;
			}
			List<Building> lit = new List<Building>();
			for (int num6 = 0; num6 < Burning.Length; num6++)
			{
				Building building4 = Put(Burning[num6], ox + 3 + num6 * 6, sy + 3);
				if (building4 != null)
				{
					lit.Add(building4);
				}
			}
			yield return new WaitForSecondsRealtime(1.5f);
			Vector2 fireEye = new Vector2((float)(ox + 3) + (float)(Burning.Length - 1) * 3f, (float)sy - 6f);
			_mod.DebugEyeHeight = 0f;
			_mod.DebugEye = fireEye;
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(10f);
			yield return new WaitForSecondsRealtime(1f);
			foreach (Building item3 in lit)
			{
				try
				{
					((BaseSimObject)item3).addStatusEffect("burning", 25f, pColorEffect: true);
				}
				catch (Exception ex3)
				{
					Log.Error("BURNT alight: " + ex3.Message);
				}
			}
			yield return new WaitForSecondsRealtime(2f);
			_mod.SaveFrame("burnt_fire_burning.png");
			float waited = 0f;
			int down = 0;
			for (; waited < 60f; waited += 1f)
			{
				down = 0;
				foreach (Building item4 in lit)
				{
					if (item4 != null && item4.isAlive() && item4.isRuin())
					{
						down++;
					}
				}
				if (down == lit.Count)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(1f);
			}
			Log.Info($"BURNT fire: {down} of {lit.Count} burnt down after {waited:F0} s");
			Check(down >= lit.Count - 1 && lit.Count > 0, $"{down} of {lit.Count} trees set alight burnt down");
			yield return new WaitForSecondsRealtime(1.5f);
			int num7 = 0;
			int num8 = 0;
			foreach (Building item5 in lit)
			{
				if (item5 != null && item5.isAlive() && item5.isRuin() && scene.SmoulderingForTest(item5))
				{
					num7++;
				}
			}
			foreach (MeshInstance mesh in scene.Meshes)
			{
				if (mesh.Model != null && mesh.Model.Name != null && mesh.Model.Name.Contains("_embers"))
				{
					num8++;
				}
			}
			Check(num7 >= down - 1 && down > 0, $"{num7} of {down} burnt down by the fire smoulder");
			Check(num8 > 0, $"embers drawn over {num8} smouldering trees");
			_mod.SaveFrame("burnt_fire_day.png");
			_mod.Settings.TimeMode = 3;
			_mod.ForceRelight();
			_mod.DebugEye = fireEye + new Vector2(-3f, 3.5f);
			yield return new WaitForSecondsRealtime(1.5f);
			_mod.SaveFrame("burnt_fire_night.png");
			_mod.DebugEye = new Vector2((float)ox + 3.5f, (float)sy - 1.5f);
			_mod.SetPitchDegrees(14f);
			yield return new WaitForSecondsRealtime(1f);
			_mod.SaveFrame("burnt_fire_night_close.png");
			_mod.Settings.TimeMode = 1;
			_mod.ForceRelight();
		}

		private IEnumerator Shots(string tag, int ox, int oy, Vector2 over, bool cpu)
		{
			_mod.DebugEyeSet = true;
			for (int pass = 0; pass < ((!cpu) ? 1 : 2); pass++)
			{
				string r = ((pass == 1) ? "_cpu" : "");
				if (pass == 1)
				{
					_mod.Settings.GpuRender = false;
					yield return new WaitForSecondsRealtime(1f);
				}
				_mod.DebugEyeHeight = 16f;
				_mod.DebugEye = over;
				_mod.SetYaw(-MathF.PI / 2f);
				_mod.SetPitchDegrees(-30f);
				yield return new WaitForSecondsRealtime(1.5f);
				_mod.SaveFrame("burnt_" + tag + "_stand" + r + ".png");
				_mod.DebugEyeHeight = 0f;
				for (int row = 0; row < 5; row += 2)
				{
					_mod.DebugEye = new Vector2((float)ox + 14f - 2f, (float)(oy + row * 5) + 3.5f);
					_mod.SetYaw(-MathF.PI / 2f);
					_mod.SetPitchDegrees(8f);
					yield return new WaitForSecondsRealtime(1.2f);
					_mod.SaveFrame($"burnt_{tag}_row{row}{r}.png");
				}
				if (pass == 1)
				{
					_mod.Settings.GpuRender = true;
					yield return new WaitForSecondsRealtime(1f);
				}
			}
		}

		private IEnumerator TimeFrames(string what, Vector2 over)
		{
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = 16f;
			_mod.DebugEye = over;
			_mod.SetYaw(-MathF.PI / 2f);
			_mod.SetPitchDegrees(-30f);
			yield return new WaitForSecondsRealtime(1f);
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
			Log.Info($"BURNT perf {what}: mod {mod / (double)Math.Max(1, frames):F2} ms, models {models / (double)Math.Max(1, frames):F2} ms, {_mod.SceneForTest.Meshes.Count} meshes, {frames} frames");
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
			for (int i = 12; i < terrainForTest.Height - 39 - 12; i += 2)
			{
				for (int j = 12; j < terrainForTest.Width - 32 - 12; j += 2)
				{
					float num2 = float.MaxValue;
					float num3 = float.MinValue;
					int num4 = 0;
					for (int k = -8; k < 47; k += 2)
					{
						for (int l = -2; l < 34; l += 2)
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
				Log.Info($"BURNT site {result.Value}: score {num:F1}");
			}
			return result;
		}

		private static void Park(Actor king, int x, int y)
		{
			for (int i = 0; i <= 24; i++)
			{
				for (int j = -i; j <= i; j++)
				{
					for (int k = -i; k <= i; k++)
					{
						if (Math.Max(Math.Abs(k), Math.Abs(j)) != i)
						{
							continue;
						}
						WorldTile tile = World.world.GetTile(x + k, y + j);
						if (tile != null && tile.Type != null && !tile.Type.liquid && !tile.Type.lava && !tile.Type.block && tile.building == null)
						{
							try
							{
								king.setCurrentTilePosition(tile);
							}
							catch
							{
								continue;
							}
							Log.Info($"BURNT body parked at {tile.x},{tile.y}");
							return;
						}
					}
				}
			}
			Log.Error("BURNT no dry land to park the body on");
		}

		private static void ClearUnits(Vector2 at, float radius, Actor king)
		{
			foreach (Actor item in new List<Actor>(World.world.units.getSimpleList()))
			{
				if (item != null && item != king && item.isAlive() && !((item.current_position - at).sqrMagnitude > radius * radius))
				{
					try
					{
						item.dieSimpleNone();
					}
					catch
					{
					}
				}
			}
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
				Log.Error("BURNT building " + id + ": " + ex.Message);
				return null;
			}
		}
	}
}
