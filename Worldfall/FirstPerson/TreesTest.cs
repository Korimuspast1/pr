using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class TreesTest : MonoBehaviour
	{
		private struct View
		{
			public string Name;

			public Vector2 Eye;

			public float Yaw;

			public float Pitch;

			public float Height;
		}

		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private Vector2 _mid;

		private string[] _ids = new string[4] { "tree_green_1", "tree_green_2", "tree_green_3", "tree_green_2" };

		private readonly List<View> _views = new List<View>();

		private const int Half = 30;

		private Actor _person;

		private Actor _king;

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
			Log.Info("TREES " + (ok ? "PASS" : "FAIL") + " " + what);
		}

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(path);
			bool quit = options.Contains("quit");
			bool stay = options.Contains("stay");
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
				Log.Error("TREES could not found a kingdom");
				Application.Quit();
				yield break;
			}
			ControllableUnit.setControllableCreature(king);
			_king = king;
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("TREES first person did not start");
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
				Log.Error("TREES no flat open land");
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
			if (options.Contains("res2"))
			{
				s.Resolution = 2;
			}
			_mod.ForceRelight();
			try
			{
				king.setCurrentTilePosition(World.world.GetTile((int)_mid.x + 30 - 3, (int)_mid.y - 30 + 3));
			}
			catch
			{
			}
			bool great = options.Contains("great");
			int num = options.IndexOf("ids:", StringComparison.Ordinal);
			if (num >= 0)
			{
				string text = options.Substring(num + 4).Split(new char[3] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)[0];
				_ids = text.Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			}
			if (great)
			{
				yield return Celestial();
			}
			else
			{
				yield return Meadow();
			}
			if (!options.Contains("asis"))
			{
				yield return Summer();
			}
			if (great)
			{
				PlanGreatViews();
			}
			else
			{
				PlanViews();
			}
			yield return new WaitForSecondsRealtime(3f);
			yield return Sway();
			Config.paused = true;
			yield return Shots("");
			yield return Perf();
			if (options.Contains("winter"))
			{
				yield return Winter(on: true);
				yield return Shots("_winter");
				yield return Winter(on: false);
			}
			if (options.Contains("rocks"))
			{
				yield return Rocks();
			}
			if (stay)
			{
				string cmdFile = Path.Combine(Log.Folder, "trees.cmd");
				Log.Info("TREES staying: commands in " + cmdFile);
				while (true)
				{
					yield return new WaitForSecondsRealtime(0.5f);
					if (!File.Exists(cmdFile))
					{
						continue;
					}
					string cmd;
					try
					{
						cmd = File.ReadAllText(cmdFile).Trim();
						File.Delete(cmdFile);
					}
					catch
					{
						continue;
					}
					Log.Info("TREES command: " + cmd);
					string[] w = cmd.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
					if (w.Length == 0)
					{
						continue;
					}
					if (w[0] == "quit")
					{
						break;
					}
					if (w[0] == "shots")
					{
						for (int i = 0; i < 3; i++)
						{
							yield return null;
						}
						yield return Shots((w.Length > 1) ? ("_" + w[1]) : "");
					}
					if (w[0] == "view" && w.Length > 1)
					{
						View v = _views.Find((View x) => x.Name == w[1]);
						if (v.Name != null)
						{
							yield return Shot(v, TimeTag(s.TimeMode) + ((w.Length > 2) ? ("_" + w[2]) : ""));
						}
					}
					if (w[0] == "time" && w.Length > 1 && int.TryParse(w[1], out var result))
					{
						s.TimeMode = result;
						_mod.ForceRelight();
						for (int i = 0; i < 30; i++)
						{
							yield return null;
						}
					}
					if (w[0] == "winter")
					{
						yield return Winter(on: true);
					}
					if (w[0] == "summer")
					{
						yield return Winter(on: false);
					}
					if (w[0] == "run")
					{
						Config.paused = false;
					}
					if (w[0] == "pause")
					{
						Config.paused = true;
					}
					Log.Info("TREES done " + cmd);
				}
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
			Log.Info($"TREES {_pass} passed, {_fail} failed");
			Log.Info("TREES DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator Meadow()
		{
			int cx = (int)_mid.x;
			int cy = (int)_mid.y;
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item == null || !item.isAlive() || item.current_tile == null)
				{
					continue;
				}
				int value = item.current_tile.x - cx;
				int value2 = item.current_tile.y - cy;
				if (Mathf.Abs(value) > 32 || Mathf.Abs(value2) > 32)
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
			TopTileType pTopTile = AssetManager.top_tiles.get("grass_low");
			for (int i = cy - 30; i <= cy + 30; i++)
			{
				for (int j = cx - 30; j <= cx + 30; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile != null && tile.main_type != null && !tile.main_type.liquid && !tile.main_type.lava)
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
			Plant(_ids[0], cx - 7, cy + 10);
			Plant(_ids[Math.Min(1, _ids.Length - 1)], cx, cy + 11);
			Plant(_ids[Math.Min(2, _ids.Length - 1)], cx + 7, cy + 10);
			System.Random random = new System.Random(17);
			string[] ids = _ids;
			int n = 0;
			for (int k = cy + 21; k <= cy + 30 - 2; k += 3)
			{
				for (int l = cx - 30 + 3; l <= cx + 30 - 3; l += 3)
				{
					if (Plant(ids[random.Next(ids.Length)], l + random.Next(2), k + random.Next(2)))
					{
						n++;
					}
				}
			}
			string[] lumps = new string[7] { "fruit_bush", "mineral_stone", "fruit_bush", "mineral_gold", "mineral_gems", "fruit_bush", "mineral_metals" };
			yield return new WaitForSecondsRealtime(0.5f);
			int num = 0;
			for (int m = 0; m < lumps.Length; m++)
			{
				if (Plant(lumps[m], cx - 12 + m * 4, cy - 4))
				{
					num++;
				}
				else
				{
					Log.Info($"TREES could not plant {lumps[m]} at {cx - 12 + m * 4},{cy - 4}");
				}
			}
			Log.Info($"TREES meadow at {_mid}: {n} trees in the wood, {num} of {lumps.Length} bushes and rocks, season {SeasonLook.Spring:F2}/{SeasonLook.Summer:F2}/{SeasonLook.Autumn:F2}/{SeasonLook.Winter:F2}");
			yield return new WaitForSecondsRealtime(1f);
		}

		private IEnumerator Celestial()
		{
			int cx = (int)_mid.x;
			int cy = (int)_mid.y;
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item == null || !item.isAlive() || item.current_tile == null)
				{
					continue;
				}
				int value = item.current_tile.x - cx;
				int value2 = item.current_tile.y - cy;
				if (Mathf.Abs(value) > 32 || Mathf.Abs(value2) > 32)
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
			TopTileType pTopTile = AssetManager.top_tiles.get("celestial_low") ?? AssetManager.top_tiles.get("grass_low");
			for (int i = cy - 30; i <= cy + 30; i++)
			{
				for (int j = cx - 30; j <= cx + 30; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile != null && tile.main_type != null && !tile.main_type.liquid && !tile.main_type.lava)
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
			bool flag = Plant("celestial_tree", cx, cy + 14);
			Plant("celestial_tree_small", cx - 18, cy + 8);
			Plant("celestial_tree_small", cx + 17, cy + 10);
			Actor actor = null;
			try
			{
				actor = World.world.units.spawnNewUnit("human", World.world.GetTile(cx + 3, cy + 9), pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("TREES person: " + ex.Message);
			}
			_person = actor;
			Log.Info(string.Format("TREES celestial patch at {0}: great tree {1}, a person {2}", _mid, flag ? "planted" : "NOT planted", (actor != null) ? "beside it" : "missing"));
			yield return new WaitForSecondsRealtime(1.5f);
			foreach (MeshInstance mesh in _mod.SceneForTest.Meshes)
			{
				if (mesh?.Model != null && mesh.Model.Name.StartsWith("tree_celestial", StringComparison.Ordinal) && !mesh.Model.Name.StartsWith("tree_celestial_small", StringComparison.Ordinal))
				{
					Log.Info($"TREES great tree drawn {mesh.Model.Max.Z * mesh.Basis.C2.Z:F1} tiles tall ({mesh.Model.Name})");
				}
			}
		}

		private void PlanGreatViews()
		{
			float x = _mid.x;
			float y = _mid.y;
			_views.Clear();
			_views.Add(new View
			{
				Name = "great_far",
				Eye = new Vector2(x + 2f, y - 30f + 2f),
				Yaw = MathF.PI / 2f,
				Pitch = 18f
			});
			_views.Add(new View
			{
				Name = "great_mid",
				Eye = new Vector2(x - 6f, y - 8f),
				Yaw = MathF.PI * 43f / 100f,
				Pitch = 28f
			});
			_views.Add(new View
			{
				Name = "great_foot",
				Eye = new Vector2(x + 7f, y + 4f),
				Yaw = MathF.PI * 31f / 50f,
				Pitch = 16f
			});
			_views.Add(new View
			{
				Name = "great_under",
				Eye = new Vector2(x + 4f, y + 9f),
				Yaw = 1.8849558f,
				Pitch = 60f
			});
			_views.Add(new View
			{
				Name = "great_lit",
				Eye = new Vector2(x - 1f, y + 30f - 1f),
				Yaw = -MathF.PI / 2f,
				Pitch = 22f
			});
		}

		private bool Plant(string id, int x, int y)
		{
			WorldTile tile = World.world.GetTile(x, y);
			if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block)
			{
				return false;
			}
			try
			{
				if (tile.building != null)
				{
					tile.building.startDestroyBuilding();
				}
				return World.world.buildings.addBuilding(id, tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New) != null;
			}
			catch (Exception ex)
			{
				Log.Error("TREES plant " + id + ": " + ex.Message);
				return false;
			}
		}

		private IEnumerator Summer()
		{
			yield return Winter(on: false);
		}

		private void PlanViews()
		{
			float x = _mid.x;
			float y = _mid.y;
			_views.Clear();
			_views.Add(new View
			{
				Name = "back",
				Eye = new Vector2(x + 0.3f, y + 2.5f),
				Yaw = MathF.PI / 2f,
				Pitch = 12f
			});
			_views.Add(new View
			{
				Name = "lit",
				Eye = new Vector2(x - 0.5f, y + 18.5f),
				Yaw = -MathF.PI / 2f,
				Pitch = 12f
			});
			_views.Add(new View
			{
				Name = "under",
				Eye = new Vector2(x + 0.8f, y + 10.2f),
				Yaw = MathF.PI * 11f / 20f,
				Pitch = 55f
			});
			_views.Add(new View
			{
				Name = "bushes",
				Eye = new Vector2(x - 2f, y - 7.2f),
				Yaw = MathF.PI / 2f,
				Pitch = -12f
			});
			_views.Add(new View
			{
				Name = "wood",
				Eye = new Vector2(x - 2f, y - 1f),
				Yaw = MathF.PI / 2f,
				Pitch = 6f
			});
			_views.Add(new View
			{
				Name = "far",
				Eye = new Vector2(x, y - 30f + 1f),
				Yaw = MathF.PI / 2f,
				Pitch = 2f,
				Height = 3f
			});
		}

		private static string TimeTag(int mode)
		{
			switch (mode)
			{
			default:
				return "day";
			case 3:
				return "night";
			case 2:
				return "golden";
			}
		}

		private IEnumerator Shots(string tag)
		{
			Settings s = _mod.Settings;
			int was = s.TimeMode;
			int[] array = new int[3] { 1, 2, 3 };
			for (int i = 0; i < array.Length; i++)
			{
				int mode = (s.TimeMode = array[i]);
				_mod.ForceRelight();
				for (int j = 0; j < 20; j++)
				{
					yield return null;
				}
				foreach (View view in _views)
				{
					yield return Shot(view, TimeTag(mode) + tag);
				}
			}
			s.TimeMode = was;
			_mod.ForceRelight();
			Log.Info("TREES shots" + tag + " saved");
		}

		private IEnumerator Shot(View v, string tag)
		{
			_mod.DebugEye = v.Eye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = v.Height;
			_mod.SetYaw(v.Yaw);
			_mod.SetPitchDegrees(v.Pitch);
			yield return new WaitForSecondsRealtime(0.5f);
			_mod.SaveFrame("trees_" + v.Name + "_" + tag + ".png");
			yield return null;
		}

		private IEnumerator Rocks()
		{
			Config.paused = false;
			Building rock = null;
			foreach (Building simple in World.world.buildings.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple.asset != null && simple.current_tile != null && simple.asset.id == "mineral_gold" && Mathf.Abs((float)simple.current_tile.y - (_mid.y - 4f)) < 2f && Mathf.Abs((float)simple.current_tile.x - _mid.x) < 14f)
				{
					rock = simple;
					break;
				}
			}
			if (rock == null)
			{
				Log.Error("TREES no gold rock in the row");
				yield break;
			}
			Vector2 at = new Vector2((float)rock.current_tile.x + 0.5f, (float)rock.current_tile.y + 0.5f);
			View eye = new View
			{
				Name = "mine",
				Eye = at + new Vector2(0.4f, -3.2f),
				Yaw = MathF.PI / 2f,
				Pitch = -14f
			};
			View v = new View
			{
				Name = "row",
				Eye = new Vector2(_mid.x - 4f, _mid.y - 8.5f),
				Yaw = 1.3194689f,
				Pitch = -10f
			};
			_mod.Settings.TimeMode = 1;
			_mod.ForceRelight();
			yield return Shot(v, "day");
			yield return Shot(eye, "0");
			Work work = _mod.Work;
			int whole = Parts();
			work.SetProgressForTest(rock, 0.4f);
			yield return new WaitForSecondsRealtime(0.25f);
			_mod.SaveFrame("trees_mine_1a.png");
			yield return new WaitForSecondsRealtime(1.2f);
			_mod.SaveFrame("trees_mine_1b.png");
			int one = Parts();
			work.SetProgressForTest(rock, 0.72f);
			yield return new WaitForSecondsRealtime(0.25f);
			_mod.SaveFrame("trees_mine_2a.png");
			yield return new WaitForSecondsRealtime(1.2f);
			_mod.SaveFrame("trees_mine_2b.png");
			int num = Parts();
			Log.Info($"TREES mining: parts drawn {whole} -> {one} -> {num}");
			Check(whole >= 3 && ((one == whole + 1 && num == whole + 2) || (one == whole - 1 && num == whole - 2)), $"cracks spread (or a chunk breaks off) at a third and at two thirds ({whole} -> {one} -> {num})");
			_mod.SceneForTest.MinedOut(rock, eye.Eye);
			try
			{
				rock.startDestroyBuilding();
			}
			catch
			{
			}
			yield return new WaitForSecondsRealtime(0.3f);
			_mod.SaveFrame("trees_mine_3a.png");
			yield return new WaitForSecondsRealtime(2f);
			_mod.SaveFrame("trees_mine_3b.png");
			bool ok = false;
			foreach (MeshInstance mesh in _mod.SceneForTest.Meshes)
			{
				if (mesh?.Model != null && mesh.Model.Name.StartsWith("rock_rubble", StringComparison.Ordinal))
				{
					ok = true;
				}
			}
			Check(ok, "mined out, rubble lies there");
			work.SetProgressForTest(rock, 0f);
			Building bush = null;
			foreach (Building simple2 in World.world.buildings.getSimpleList())
			{
				if (simple2 != null && simple2.isAlive() && simple2.asset != null && simple2.asset.id == "fruit_bush" && simple2.current_tile != null && Mathf.Abs((float)simple2.current_tile.y - (_mid.y - 4f)) < 2f && Mathf.Abs((float)simple2.current_tile.x - _mid.x) < 14f)
				{
					bush = simple2;
					break;
				}
			}
			if (bush != null)
			{
				bool ripe = true;
				try
				{
					ripe = bush.hasResourcesToCollect();
				}
				catch
				{
				}
				yield return null;
				bool flag = Berries();
				Log.Info($"TREES fruit bush: ripe {ripe}, berries shown {flag}");
				Check(flag == ripe, $"a fruit bush shows berries only when it can be picked (ripe {ripe}, shown {flag})");
				if (ripe && _king != null)
				{
					try
					{
						bush.extractResources(_king);
					}
					catch (Exception ex)
					{
						Log.Error("TREES pick: " + ex.Message);
					}
					yield return new WaitForSecondsRealtime(0.5f);
					bool flag2 = true;
					try
					{
						flag2 = bush.hasResourcesToCollect();
					}
					catch
					{
					}
					bool flag3 = Berries();
					Log.Info($"TREES fruit bush picked: ripe {flag2}, berries shown {flag3}");
					Check(!flag2 && !flag3, $"picked, the bush has no berries ({flag3})");
				}
			}
			Config.paused = true;
			bool Berries()
			{
				Vector2 vector = new Vector2((float)bush.current_tile.x + 0.5f, (float)bush.current_tile.y + 0.5f);
				MeshInstance meshInstance = null;
				float num2 = 4f;
				foreach (MeshInstance mesh2 in _mod.SceneForTest.Meshes)
				{
					if (mesh2?.Model != null && mesh2.Model.Name.StartsWith("bush_", StringComparison.Ordinal))
					{
						float num3 = (mesh2.Position.X - vector.x) * (mesh2.Position.X - vector.x) + (mesh2.Position.Y - vector.y) * (mesh2.Position.Y - vector.y);
						if (num3 < num2)
						{
							num2 = num3;
							meshInstance = mesh2;
						}
					}
				}
				if (meshInstance == null)
				{
					Log.Info($"TREES no fruit bush drawn near {vector}");
					return false;
				}
				int num4 = meshInstance.Model.PartIndex("berries");
				Log.Info($"TREES fruit bush drawn: {meshInstance.Model.Name} at {meshInstance.Position.X:F1},{meshInstance.Position.Y:F1}, berries part {num4}, hidden {meshInstance.Hidden}");
				if (num4 >= 0)
				{
					return (meshInstance.Hidden & (ulong)(1L << num4)) == 0;
				}
				return false;
			}
			int Parts()
			{
				foreach (MeshInstance mesh3 in _mod.SceneForTest.Meshes)
				{
					if (mesh3?.Model != null && mesh3.Model.Name.StartsWith("rock_ore_gold", StringComparison.Ordinal) && (mesh3.Position.X - at.x) * (mesh3.Position.X - at.x) + (mesh3.Position.Y - at.y) * (mesh3.Position.Y - at.y) < 1f && mesh3.Hidden != ulong.MaxValue && mesh3.Fade >= 1f)
					{
						return mesh3.Model.Parts.Length - BitCount(mesh3.Hidden);
					}
				}
				return -1;
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

		private IEnumerator Sway()
		{
			Config.paused = false;
			View v = _views[0];
			yield return Shot(v, "sway0");
			float clock = TreeSway.Clock;
			yield return new WaitForSecondsRealtime(0.6f);
			_mod.SaveFrame("trees_back_sway1.png");
			int num = 0;
			int num2 = 0;
			foreach (MeshInstance mesh in _mod.SceneForTest.Meshes)
			{
				if (mesh?.Model != null && mesh.Model.Name.StartsWith("tree_", StringComparison.Ordinal))
				{
					num2++;
					if (mesh.Sway > 0f && mesh.SwayInvTop > 0f)
					{
						num++;
					}
				}
			}
			List<string> list = new List<string>();
			foreach (MeshInstance mesh2 in _mod.SceneForTest.Meshes)
			{
				if (mesh2?.Model != null && Mathf.Abs(mesh2.Position.X - _mid.x) < 14f && Mathf.Abs(mesh2.Position.Y - (_mid.y - 4f)) < 1.5f)
				{
					list.Add($"{mesh2.Model.Name}@{mesh2.Position.X:F1},{mesh2.Position.Y:F1}");
				}
			}
			Log.Info("TREES drawn at the bush row: " + string.Join(" ", list.ToArray()));
			Log.Info($"TREES sway: {num} of {num2} trees drawn swaying, clock {clock:F2} -> {TreeSway.Clock:F2}, push ({TreeSway.PushX:F2}, {TreeSway.PushY:F2}), gust {TreeSway.Gust:F2}");
			Check(num2 > 0 && num == num2, $"every tree sways ({num}/{num2})");
			Check(TreeSway.Clock > clock + 0.2f, "the sway clock runs with the world");
			Config.paused = true;
			float held = TreeSway.Clock;
			yield return new WaitForSecondsRealtime(0.5f);
			Check(Mathf.Abs(TreeSway.Clock - held) < 0.0001f, "paused, the trees stand still");
		}

		private IEnumerator Perf()
		{
			View v = _views.Find((View x) => x.Name == "wood");
			if (v.Name == null)
			{
				v = _views[Math.Min(1, _views.Count - 1)];
			}
			yield return Shot(v, "perf");
			List<float> all = new List<float>(90);
			for (int i = 0; i < 90; i++)
			{
				yield return null;
				all.Add(_mod.FrameMs);
			}
			all.Sort();
			Log.Info($"TREES perf wood: frame median {all[all.Count / 2]:F2} ms, p90 {all[all.Count * 9 / 10]:F2} ms, {_mod.SceneForTest.Meshes.Count} models");
		}

		private IEnumerator Winter(bool on)
		{
			int num = Find((!on) ? Season.Summer : Season.Winter);
			if (num < 0)
			{
				Log.Info("TREES this age has no " + (on ? "winter" : "summer"));
				yield break;
			}
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
			Log.Info(string.Format("TREES {0}: SeasonLook winter {1:F2}", on ? "winter" : "summer", SeasonLook.Winter));
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
			for (int i = 38; i < terrainForTest.Height - 30 - 8; i += 3)
			{
				for (int j = 38; j < terrainForTest.Width - 30 - 8; j += 3)
				{
					float num2 = float.MaxValue;
					float num3 = float.MinValue;
					int num4 = 0;
					for (int k = -30; k <= 30; k += 3)
					{
						for (int l = -30; l <= 30; l += 3)
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
