using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BushesTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private Vector2 _mid;

		private const int Half = 9;

		private static readonly string[] Looks = new string[23]
		{
			"grass", "lemon", "flower", "enchanted", "birch", "maple", "clover", "garlic", "savanna", "jungle",
			"mushroom", "permafrost", "swamp", "celestial", "paradox", "desert", "wasteland", "corrupted", "infernal", "crystal",
			"singularity", "candy", "rocklands"
		};

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
			Log.Info("BUSHES " + (ok ? "PASS" : "FAIL") + " " + what);
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
			foreach (WorldTile item2 in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item2, "human", 2, 0);
				if (king != null)
				{
					break;
				}
			}
			if (king == null)
			{
				Log.Error("BUSHES could not found a kingdom");
				Application.Quit();
				yield break;
			}
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("BUSHES first person did not start");
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
				Log.Error("BUSHES no flat open land");
				Application.Quit();
				yield break;
			}
			_mid = vector.Value;
			Settings s = _mod.Settings;
			int savedMode = s.TimeMode;
			int savedRes = s.Resolution;
			bool savedGpu = s.GpuRender;
			s.GpuRender = true;
			s.TimeMode = 1;
			s.Resolution = WorldBoxMod.ResolutionWidths.Length - 1;
			_mod.ForceRelight();
			int cx = (int)_mid.x;
			int cy = (int)_mid.y;
			try
			{
				king.setCurrentTilePosition(World.world.GetTile(cx + 3, cy + 9 - 1));
			}
			catch
			{
			}
			yield return Winter(on: false);
			HashSet<string> only = new HashSet<string>();
			int num = options.IndexOf("only:", StringComparison.Ordinal);
			if (num >= 0)
			{
				string[] array = options.Substring(num + 5).Split(new char[3] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)[0].Split(',');
				foreach (string item in array)
				{
					only.Add(item);
				}
			}
			Actor person = null;
			Clear(cx, cy);
			try
			{
				person = World.world.units.spawnNewUnit("human", World.world.GetTile(cx + 2, cy), pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("BUSHES person: " + ex.Message);
			}
			Check(person != null, "a person stands beside the bushes for size");
			yield return Shot(new Vector2((float)cx + 0.1f, (float)cy + 3.2f), -MathF.PI / 2f, -16f, null);
			string[] looks = Looks;
			foreach (string text in looks)
			{
				if (only.Count <= 0 || only.Contains(text))
				{
					yield return OneLook(text, cx, cy, person, options.Contains("night"));
				}
			}
			if (only.Count == 0 || only.Contains("turn"))
			{
				yield return LandTurns(cx, cy);
			}
			Config.paused = false;
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
			Log.Info($"BUSHES {_pass} passed, {_fail} failed");
			Log.Info("BUSHES DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator OneLook(string look, int cx, int cy, Actor person, bool night)
		{
			Config.paused = false;
			TopTileType topTileType = AssetManager.top_tiles.get(look + "_low");
			if (topTileType == null)
			{
				Log.Info("BUSHES no ground " + look + "_low");
				yield break;
			}
			Clear(cx, cy);
			Ground(cx, cy, topTileType);
			yield return null;
			Building hero = Plant("fruit_bush", cx, cy);
			Plant("fruit_bush", cx - 8, cy - 4);
			Plant("fruit_bush", cx + 8, cy - 5);
			if (hero == null)
			{
				Log.Info("BUSHES " + look + ": could not plant the bush");
			}
			if (person != null)
			{
				try
				{
					person.setCurrentTilePosition(World.world.GetTile(cx + 2, cy));
				}
				catch
				{
				}
			}
			yield return new WaitForSecondsRealtime(1.2f);
			Config.paused = true;
			yield return new WaitForSecondsRealtime(0.6f);
			Vector2 p = new Vector2((float)cx + 0.5f, (float)cy + 0.5f);
			yield return Shot(new Vector2(p.x - 0.4f, p.y + 2.7f), -MathF.PI / 2f, -16f, "bushes_" + look + ".png");
			MeshInstance meshInstance = DrawnBush(p);
			string text = meshInstance?.Model?.Name ?? "none";
			Check(text.StartsWith("bush_" + look, StringComparison.Ordinal), look + ": the bush is drawn in its land's look (" + text + ")");
			if (meshInstance != null)
			{
				MeshModel obj2 = Models.Get("bush_" + look) ?? meshInstance.Model;
				float num = obj2.Max.Z * meshInstance.Basis.C2.Z;
				float num2 = obj2.InfoFloat("tiles", 0.5f);
				Check(num > num2 * 0.86f && num < num2 * 1.14f, $"{look}: {num:F2} tiles tall (its model says {num2:F2}; people are 0.9)");
				bool flag = true;
				try
				{
					flag = hero?.hasResourcesToCollect() ?? true;
				}
				catch
				{
				}
				int num3 = meshInstance.Model.PartIndex("berries");
				bool flag2 = num3 >= 0 && (meshInstance.Hidden & (ulong)(1L << num3)) == 0;
				Check(num3 >= 0 && flag2 == flag, $"{look}: berries shown while it has some (ripe {flag}, shown {flag2})");
			}
			yield return Shot(new Vector2(p.x - 0.5f, p.y + 7.5f), -MathF.PI / 2f, -8f, "bushes_" + look + "_far.png");
			if (night)
			{
				_mod.Settings.TimeMode = 3;
				_mod.ForceRelight();
				for (int i = 0; i < 20; i++)
				{
					yield return null;
				}
				yield return Shot(new Vector2(p.x - 0.4f, p.y + 2.7f), -MathF.PI / 2f, -16f, "bushes_" + look + "_night.png");
				_mod.Settings.TimeMode = 1;
				_mod.ForceRelight();
				for (int i = 0; i < 20; i++)
				{
					yield return null;
				}
			}
		}

		private IEnumerator LandTurns(int cx, int cy)
		{
			Config.paused = false;
			TopTileType top = AssetManager.top_tiles.get("grass_low");
			Clear(cx, cy);
			Ground(cx, cy, top);
			yield return null;
			Plant("fruit_bush", cx, cy);
			yield return new WaitForSecondsRealtime(1f);
			Vector2 p = new Vector2((float)cx + 0.5f, (float)cy + 0.5f);
			yield return Shot(new Vector2(p.x - 0.4f, p.y + 2.7f), -MathF.PI / 2f, -16f, "bushes_turn_before.png");
			string before = DrawnBush(p)?.Model?.Name ?? "none";
			TopTileType top2 = AssetManager.top_tiles.get("desert_low");
			Ground(cx, cy, top2);
			yield return new WaitForSecondsRealtime(1f);
			yield return Shot(new Vector2(p.x - 0.4f, p.y + 2.7f), -MathF.PI / 2f, -16f, "bushes_turn_after.png");
			string text = DrawnBush(p)?.Model?.Name ?? "none";
			Check(before.StartsWith("bush_grass", StringComparison.Ordinal) && text.StartsWith("bush_desert", StringComparison.Ordinal), "a meadow bush on land turned to desert takes the desert's look (" + before + " -> " + text + ")");
		}

		private MeshInstance DrawnBush(Vector2 p)
		{
			MeshInstance result = null;
			float num = 1f;
			foreach (MeshInstance mesh in _mod.SceneForTest.Meshes)
			{
				if (mesh?.Model != null && mesh.Model.Name.StartsWith("bush", StringComparison.Ordinal))
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

		private IEnumerator Shot(Vector2 eye, float yaw, float pitch, string file)
		{
			_mod.DebugEye = eye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = 0f;
			_mod.SetYaw(yaw);
			_mod.SetPitchDegrees(pitch);
			yield return new WaitForSecondsRealtime(0.6f);
			if (file != null)
			{
				_mod.SaveFrame(file);
			}
			yield return null;
		}

		private void Clear(int cx, int cy)
		{
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item == null || !item.isAlive() || item.current_tile == null)
				{
					continue;
				}
				int value = item.current_tile.x - cx;
				int value2 = item.current_tile.y - cy;
				if (Mathf.Abs(value) > 11 || Mathf.Abs(value2) > 11)
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
		}

		private void Ground(int cx, int cy, TopTileType top)
		{
			for (int i = cy - 9; i <= cy + 9; i++)
			{
				for (int j = cx - 9; j <= cx + 9; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile != null && tile.main_type != null && !tile.main_type.liquid && !tile.main_type.lava)
					{
						try
						{
							tile.setTopTileType(top);
						}
						catch
						{
						}
					}
				}
			}
		}

		private Building Plant(string id, int x, int y)
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
				Log.Error("BUSHES plant " + id + ": " + ex.Message);
				return null;
			}
		}

		private IEnumerator Winter(bool on)
		{
			int num = Find((!on) ? Season.Summer : Season.Winter);
			if (num >= 0)
			{
				bool paused = Config.paused;
				Config.paused = false;
				Calendar.SetDayForTest(num + 16);
				yield return new WaitForSecondsRealtime(6f);
				try
				{
					_mod.SamplerForTest.RelightAll();
				}
				catch
				{
				}
				Config.paused = paused;
				for (int i = 0; i < 10; i++)
				{
					yield return null;
				}
			}
			int Find(Season want)
			{
				for (int j = 0; j < 8; j++)
				{
					if (Calendar.SeasonOf(j, out var dayIn, out var _) == want && dayIn == 0)
					{
						return j;
					}
				}
				return -1;
			}
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
			for (int i = 17; i < terrainForTest.Height - 9 - 8; i += 3)
			{
				for (int j = 17; j < terrainForTest.Width - 9 - 8; j += 3)
				{
					float num2 = float.MaxValue;
					float num3 = float.MinValue;
					int num4 = 0;
					for (int k = -9; k <= 9; k += 3)
					{
						for (int l = -9; l <= 9; l += 3)
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
			return result;
		}
	}
}
