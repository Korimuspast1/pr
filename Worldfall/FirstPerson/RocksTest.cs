using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class RocksTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private const int Half = 16;

		private static readonly string[] Minerals = new string[8] { "mineral_stone", "mineral_metals", "mineral_gold", "mineral_silver", "mineral_gems", "mineral_mythril", "mineral_adamantine", "mineral_bones" };

		private static readonly (string tile, char style)[] Lands = new(string, char)[4]
		{
			("grass_low", 'w'),
			("swamp_low", 's'),
			("desert_low", 'b'),
			("infernal_low", 'j')
		};

		private readonly List<Building> _rocks = new List<Building>();

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
			Log.Info("STONES " + (ok ? "PASS" : "FAIL") + " " + what);
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
				Log.Error("STONES could not found a kingdom");
				Application.Quit();
				yield break;
			}
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("STONES first person did not start");
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
				Log.Error("STONES no flat open land");
				Application.Quit();
				yield break;
			}
			Vector2 mid = vector.Value;
			Settings s = _mod.Settings;
			int savedMode = s.TimeMode;
			int savedRes = s.Resolution;
			bool savedGpu = s.GpuRender;
			s.GpuRender = true;
			s.TimeMode = 1;
			s.Resolution = WorldBoxMod.ResolutionWidths.Length - 1;
			_mod.ForceRelight();
			WorldTile tile = World.world.GetTile((int)mid.x, (int)mid.y - 16 - 6);
			if (tile != null && tile.Type != null && !tile.Type.liquid)
			{
				try
				{
					king.setCurrentTilePosition(tile);
				}
				catch
				{
				}
			}
			(string tile, char style)[] lands = Lands;
			for (int i = 0; i < lands.Length; i++)
			{
				(string tile, char style) land = lands[i];
				if (options.Contains("onlygrass") && land.tile != "grass_low")
				{
					continue;
				}
				IEnumerator part = Land(mid, land.tile, land.style);
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
						goto IL_03eb;
					}
					catch (Exception ex)
					{
						Log.Error("STONES " + land.tile + " failed: " + ex);
						_fail++;
					}
					break;
					IL_03eb:
					yield return current2;
				}
			}
			if (!options.Contains("nomine"))
			{
				WorldTile mt = World.world.GetTile((int)mid.x, (int)mid.y - 9);
				Building building = null;
				try
				{
					if (mt != null)
					{
						building = World.world.buildings.addBuilding("mine", mt, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					}
				}
				catch (Exception ex2)
				{
					Log.Error("STONES mine: " + ex2.Message);
				}
				if (building != null)
				{
					_mod.DebugEye = new Vector2((float)mt.x + 0.5f, (float)mt.y - 6f);
					_mod.SetYaw(MathF.PI / 2f);
					_mod.SetPitchDegrees(-4f);
					yield return new WaitForSecondsRealtime(2f);
					_mod.SaveFrame("stones_town_mine.png");
					_mod.DebugEye = new Vector2((float)mt.x - 2.2f, (float)mt.y - 3.8f);
					_mod.SetYaw(1.1309735f);
					yield return new WaitForSecondsRealtime(1.5f);
					_mod.SaveFrame("stones_town_mine_near.png");
				}
				IEnumerator part = Mine(mid);
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
						goto IL_0625;
					}
					catch (Exception ex3)
					{
						Log.Error("STONES mining failed: " + ex3);
						_fail++;
					}
					break;
					IL_0625:
					yield return current3;
				}
			}
			s.TimeMode = savedMode;
			s.Resolution = savedRes;
			s.GpuRender = savedGpu;
			_mod.ForceRelight();
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info($"STONES {_pass} passed, {_fail} failed");
			Log.Info("STONES DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator Land(Vector2 mid, string tile, char style)
		{
			int cx = (int)mid.x;
			int cy = (int)mid.y;
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item == null || !item.isAlive() || item.current_tile == null || Mathf.Abs(item.current_tile.x - cx) > 18 || Mathf.Abs(item.current_tile.y - cy) > 18)
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
				Log.Error("STONES no tile " + tile);
				yield break;
			}
			for (int i = cy - 16; i <= cy + 16; i++)
			{
				for (int j = cx - 16; j <= cx + 16; j++)
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
			_rocks.Clear();
			for (int k = 0; k < 3; k++)
			{
				for (int l = 0; l < Minerals.Length; l++)
				{
					int pX = cx - 11 + l * 3 + k;
					int pY = cy + 3 + k * 3;
					WorldTile tile3 = World.world.GetTile(pX, pY);
					if (tile3 == null || tile3.Type == null || tile3.Type.liquid || tile3.Type.block)
					{
						continue;
					}
					try
					{
						Building building = World.world.buildings.addBuilding(Minerals[l], tile3, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
						if (building != null)
						{
							_rocks.Add(building);
						}
					}
					catch (Exception ex)
					{
						Log.Error("STONES plant " + Minerals[l] + ": " + ex.Message);
					}
				}
			}
			_mod.DebugEye = new Vector2(mid.x, mid.y - 3f);
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = 0f;
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(-8f);
			for (float wait = 0f; wait < 10f; wait += 0.5f)
			{
				yield return new WaitForSecondsRealtime(0.5f);
				int num = 0;
				foreach (Building rock in _rocks)
				{
					if (MeshAt(new Vector2((float)rock.current_tile.x + 0.5f, (float)rock.current_tile.y + 0.5f), "rock_") != null)
					{
						num++;
					}
				}
				if (wait >= 2.5f && num >= _rocks.Count * 3 / 4)
				{
					break;
				}
			}
			int num2 = 0;
			int num3 = 0;
			HashSet<int> hashSet = new HashSet<int>();
			float num4 = float.MaxValue;
			float num5 = 0f;
			List<string> list = new List<string>();
			foreach (Building rock2 in _rocks)
			{
				MeshInstance meshInstance = MeshAt(new Vector2((float)rock2.current_tile.x + 0.5f, (float)rock2.current_tile.y + 0.5f), "rock_");
				if (meshInstance != null)
				{
					num2++;
					string text = Strip(meshInstance.Model.Name);
					if (StyleOf(text) == style)
					{
						num3++;
					}
					hashSet.Add(RockForms.FormOf(text));
					float b = (Models.Get(text) ?? meshInstance.Model).Max.Z * meshInstance.Basis.C2.Z;
					num4 = Mathf.Min(num4, b);
					num5 = Mathf.Max(num5, b);
					if (list.Count < 8)
					{
						list.Add(text);
					}
				}
			}
			Log.Info(string.Format("STONES {0}: {1} planted, {2} drawn as rocks, {3} in style '{4}', forms {5}, heights {6:F2}..{7:F2} tiles; e.g. {8}", tile, _rocks.Count, num2, num3, style, string.Join(",", hashSet), num4, num5, string.Join(" ", list)));
			Check(num2 >= _rocks.Count * 3 / 4 && num2 > 0, $"{tile}: the planted minerals are drawn as rock models ({num2}/{_rocks.Count})");
			Check(num3 == num2 && num2 > 0, $"{tile}: every rock in the land's style '{style}' ({num3}/{num2})");
			Check(hashSet.Count >= 3, $"{tile}: at least three forms among them ({hashSet.Count})");
			Check(num5 > num4 * 1.6f, $"{tile}: sizes vary (heights {num4:F2}..{num5:F2})");
			string name = tile.Replace("_low", "");
			_mod.SaveFrame("stones_" + name + ".png");
			_mod.DebugEye = new Vector2(mid.x + 1f, mid.y + 13f);
			_mod.SetYaw(-MathF.PI / 2f);
			yield return new WaitForSecondsRealtime(1.5f);
			_mod.SaveFrame("stones_" + name + "_lit.png");
			_mod.DebugEye = new Vector2(mid.x - 5f, mid.y + 0.6f);
			_mod.SetYaw(1.3194689f);
			_mod.SetPitchDegrees(-14f);
			yield return new WaitForSecondsRealtime(1.5f);
			_mod.SaveFrame("stones_" + name + "_close.png");
		}

		private IEnumerator Mine(Vector2 mid)
		{
			Building rock = null;
			foreach (Building rock2 in _rocks)
			{
				if (rock2 != null && rock2.isAlive() && rock2.asset != null && rock2.asset.id == "mineral_gold")
				{
					rock = rock2;
					break;
				}
			}
			if (rock == null)
			{
				Log.Error("STONES no gold rock to mine");
				_fail++;
				yield break;
			}
			Vector2 at = new Vector2((float)rock.current_tile.x + 0.5f, (float)rock.current_tile.y + 0.5f);
			_mod.DebugEye = at + new Vector2(0.3f, -2.4f);
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(-16f);
			yield return new WaitForSecondsRealtime(1.5f);
			MeshInstance meshInstance = MeshAt(at, "rock_");
			if (meshInstance == null)
			{
				Log.Error("STONES the gold rock is not drawn");
				_fail++;
				yield break;
			}
			MeshModel model = meshInstance.Model;
			int c1 = model.PartIndex("cracks_1");
			int c2 = model.PartIndex("cracks_2");
			Log.Info($"STONES mining {model.Name}: {model.Parts.Length} parts, cracks at {c1},{c2}");
			bool none = !Shown(c1) && !Shown(c2);
			_mod.SaveFrame("stones_mine_0.png");
			Work work = _mod.Work;
			work.SetProgressForTest(rock, 0.4f);
			yield return new WaitForSecondsRealtime(0.4f);
			bool one = Shown(c1) && !Shown(c2);
			_mod.SaveFrame("stones_mine_1.png");
			work.SetProgressForTest(rock, 0.72f);
			yield return new WaitForSecondsRealtime(0.4f);
			bool flag = Shown(c1) && Shown(c2);
			_mod.SaveFrame("stones_mine_2.png");
			Check(none && one && flag, $"cracks spread: none whole, the first at a third, all at two thirds ({none}, {one}, {flag})");
			Actor body = _mod.Body;
			WorldTile tile = World.world.GetTile(rock.current_tile.x, rock.current_tile.y - 2);
			if (body != null && tile != null)
			{
				try
				{
					body.setCurrentTilePosition(tile);
				}
				catch
				{
				}
			}
			yield return new WaitForSecondsRealtime(1.5f);
			float num = ((_mod.TerrainForTest != null) ? _mod.TerrainForTest.SampleHeight(at.x, at.y) : 0f);
			bool flag2 = false;
			float z = 0f;
			for (int i = 0; i < 9; i++)
			{
				if (flag2 && z > num + 0.15f)
				{
					break;
				}
				Vector2 vector = at + ((i == 0) ? Vector2.zero : (new Vector2(Mathf.Cos((float)i * 0.785f), Mathf.Sin((float)i * 0.785f)) * 0.3f));
				flag2 = BuildingFloors.StandOn(vector.x, vector.y, 0.12f, num + 3f, out z, out var _);
			}
			Check(flag2 && z > num + 0.15f, "you stand on a rock's top (" + (flag2 ? (z - num).ToString("F2") : "nothing") + " over the ground)");
			_mod.SceneForTest.MinedOut(rock, _mod.DebugEye);
			try
			{
				rock.startDestroyBuilding();
			}
			catch
			{
			}
			yield return new WaitForSecondsRealtime(0.12f);
			_mod.SaveFrame("stones_mine_3a.png");
			int flying = 0;
			foreach (MeshInstance mesh in _mod.SceneForTest.Meshes)
			{
				if (mesh?.Model == model && mesh.Hidden != 0L && mesh.Hidden != ulong.MaxValue && BitCount(~mesh.Hidden) == 1)
				{
					int j;
					for (j = 0; ((~mesh.Hidden >> j) & 1) == 0L; j++)
					{
					}
					if (model.Parts[j].Name.StartsWith("frag_", StringComparison.Ordinal))
					{
						flying++;
					}
				}
			}
			yield return new WaitForSecondsRealtime(0.5f);
			_mod.SaveFrame("stones_mine_3b.png");
			yield return new WaitForSecondsRealtime(1.5f);
			_mod.SaveFrame("stones_mine_3c.png");
			bool ok = false;
			foreach (MeshInstance mesh2 in _mod.SceneForTest.Meshes)
			{
				if (mesh2?.Model != null && mesh2.Model.Name.StartsWith("rock_rubble", StringComparison.Ordinal))
				{
					ok = true;
				}
			}
			Check(flying >= 2, $"mined out, it bursts into fragments ({flying} flying)");
			Check(ok, "rubble lies where it stood");
			work.SetProgressForTest(rock, 0f);
			bool Shown(int part)
			{
				MeshInstance meshInstance2 = MeshAt(at, "rock_");
				if (meshInstance2 != null && part >= 0)
				{
					return (meshInstance2.Hidden & (ulong)(1L << part)) == 0;
				}
				return false;
			}
		}

		private static int BitCount(ulong v)
		{
			int num = 0;
			while (v != 0L)
			{
				v &= v - 1;
				num++;
			}
			return num;
		}

		private MeshInstance MeshAt(Vector2 p, string prefix)
		{
			MeshInstance result = null;
			float num = 1f;
			foreach (MeshInstance mesh in _mod.SceneForTest.Meshes)
			{
				if (mesh != null && mesh.Model?.Name != null && mesh.Model.Name.StartsWith(prefix, StringComparison.Ordinal) && !mesh.Model.Name.StartsWith("rock_rubble", StringComparison.Ordinal))
				{
					float num2 = (mesh.Position.X - p.x) * (mesh.Position.X - p.x) + (mesh.Position.Y - p.y) * (mesh.Position.Y - p.y);
					if (num2 < num)
					{
						num = num2;
						result = mesh;
					}
				}
			}
			return result;
		}

		private static string Strip(string name)
		{
			if (!name.EndsWith("_m", StringComparison.Ordinal) && !name.EndsWith("_f", StringComparison.Ordinal))
			{
				return name;
			}
			return name.Substring(0, name.Length - 2);
		}

		private static char StyleOf(string look)
		{
			int length = look.Length;
			if (length <= 3 || look[length - 3] != '_' || "wsbj".IndexOf(look[length - 2]) < 0 || look[length - 1] < '1' || look[length - 1] > '4')
			{
				return 'w';
			}
			return look[length - 2];
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
			for (int i = 24; i < terrainForTest.Height - 16 - 8; i += 3)
			{
				for (int j = 24; j < terrainForTest.Width - 16 - 8; j += 3)
				{
					float num2 = float.MaxValue;
					float num3 = float.MinValue;
					int num4 = 0;
					for (int k = -20; k <= 20; k += 3)
					{
						for (int l = -20; l <= 20; l += 3)
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
						float num6 = (float)num4 * 6f + (num3 - num2) * 2f + Vector2.Distance(near, new Vector2(j, i)) * 0.01f;
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
				Log.Info($"STONES site {result.Value}: score {num:F1}");
			}
			return result;
		}
	}
}
