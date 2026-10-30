using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class ShowreelTest : MonoBehaviour
	{
		private const int Fps = 30;

		private WorldBoxMod _mod;

		private string _dir;

		private int _frame;

		private readonly StringBuilder _meta = new StringBuilder();

		private readonly List<Canvas> _hidden = new List<Canvas>();

		private Vector2 _centre;

		private string _phase = "";

		private float _savedGodZoom = 40f;

		private string _savedSettings;

		private bool _flightOk;

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
			string path2 = Path.Combine(Log.Folder, "settings.txt");
			try
			{
				_savedSettings = (File.Exists(path2) ? File.ReadAllText(path2) : null);
			}
			catch
			{
			}
			int savedMode = s.TimeMode;
			int savedRes = s.Resolution;
			bool savedGod = s.GodView;
			_savedGodZoom = s.GodViewZoom;
			s.TimeMode = 1;
			s.Resolution = WorldBoxMod.ResolutionWidths.Length - 1;
			s.GodView = true;
			s.GodViewZoom = 40f;
			_mod.HintUntil = 0f;
			_dir = Path.Combine(Log.Folder, "showreel");
			Directory.CreateDirectory(_dir);
			string[] files = Directory.GetFiles(_dir);
			foreach (string path3 in files)
			{
				try
				{
					File.Delete(path3);
				}
				catch
				{
				}
			}
			WorldTile worldTile = null;
			Actor leader = null;
			foreach (WorldTile item in ScenicSites())
			{
				leader = KingTest.FoundKingdom(item, "human", 22, 0);
				if (leader != null)
				{
					worldTile = item;
					break;
				}
			}
			if (leader == null)
			{
				Log.Info("REEL PROBLEM no village could be founded");
				Finish(quit, s, savedMode, savedRes, savedGod);
				yield break;
			}
			_centre = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f);
			int num = BuildTown(leader.city, worldTile, _mod.TerrainForTest);
			Log.Info($"REEL village {leader.city.name} at {worldTile.x},{worldTile.y}: {num} buildings, screen {Screen.width}x{Screen.height}");
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			for (float t = 0f; t < 10f; t += Time.unscaledDeltaTime)
			{
				Hold(_centre, 22f);
				yield return null;
			}
			s.GodView = false;
			HideCanvases(all: false);
			yield return HoldFor(_centre, 26f, 1.5f);
			yield return Still("still_map_ui.png");
			HideCanvases(all: true);
			yield return HoldFor(_centre, 26f, 1f);
			yield return Still("still_map_26.png");
			s.GodView = true;
			int[] array = new int[3] { 1, 2, 3 };
			for (int j = 0; j < array.Length; j++)
			{
				int mode = (s.TimeMode = array[j]);
				SceneCollector.AllLit = mode == 3;
				_mod.ForceRelight();
				yield return HoldFor(_centre, 26f, 2.5f);
				ShowreelTest showreelTest = this;
				object obj5;
				switch (mode)
				{
				default:
					obj5 = "night";
					break;
				case 2:
					obj5 = "golden";
					break;
				case 1:
					obj5 = "day";
					break;
				}
				yield return showreelTest.Still("still_god_" + (string)obj5 + ".png");
			}
			yield return HoldFor(_centre, 70f, 1.5f);
			yield return Still("still_god_70.png");
			SceneCollector.AllLit = false;
			s.TimeMode = 1;
			_mod.ForceRelight();
			yield return HoldFor(_centre, 26f, 2f);
			GodView.TestMouse = new Vector2((float)Screen.width * 0.5f, (float)Screen.height * 0.5f);
			Vector2? beach = Shore(_centre);
			Log.Info("REEL beach " + (beach.HasValue ? beach.Value.ToString() : "none: flying to the middle of town"));
			for (int j = 1; j <= 3; j++)
			{
				if (_flightOk)
				{
					break;
				}
				Actor target = BeachVillager(leader.city, beach);
				yield return new WaitForSecondsRealtime(0.5f);
				yield return Flight(s, target, beach, j == 3);
				if (_flightOk)
				{
					break;
				}
				Log.Info($"REEL PROBLEM try {j} didn't reach the eyes by the magnet, flying again");
				Time.captureFramerate = 0;
				PossessionHooks.ScriptedInput = Vector2.zero;
				try
				{
					ControllableUnit.clear(pCallKill: false);
				}
				catch
				{
				}
				_mod.NoDive = true;
				yield return HoldFor(new Vector2((float)MapBox.width * 0.5f, (float)MapBox.height * 0.5f), 60f, 2f);
			}
			Time.captureFramerate = 0;
			File.WriteAllText(Path.Combine(_dir, "seq.txt"), _meta.ToString());
			if (_mod.IsFirstPerson)
			{
				array = new int[3] { 1, 2, 3 };
				for (int j = 0; j < array.Length; j++)
				{
					int mode = (s.TimeMode = array[j]);
					SceneCollector.AllLit = mode == 3;
					_mod.ForceRelight();
					yield return new WaitForSecondsRealtime((mode == 1) ? 0.3f : 2f);
					ShowreelTest showreelTest2 = this;
					object obj7;
					switch (mode)
					{
					default:
						obj7 = "night";
						break;
					case 2:
						obj7 = "golden";
						break;
					case 1:
						obj7 = "day";
						break;
					}
					yield return showreelTest2.Still("still_fp_" + (string)obj7 + ".png");
				}
				SceneCollector.AllLit = false;
				s.TimeMode = 1;
				_mod.ForceRelight();
				yield return VillageStills(s);
			}
			else
			{
				Log.Info("REEL PROBLEM not in first person for the stills");
			}
			Finish(quit, s, savedMode, savedRes, savedGod);
		}

		private IEnumerator Flight(Settings s, Actor target, Vector2? beach, bool last)
		{
			string[] files = Directory.GetFiles(_dir, "seq_*.jpg");
			foreach (string path in files)
			{
				try
				{
					File.Delete(path);
				}
				catch
				{
				}
			}
			_frame = 0;
			_meta.Length = 0;
			_flightOk = false;
			Time.captureFramerate = 30;
			float mapWhole = Mathf.Max((float)MapBox.height * 0.56f, 60f);
			Vector2 mapMid = new Vector2((float)MapBox.width * 0.5f, (float)MapBox.height * 0.5f);
			for (int k = 0; k < 8; k++)
			{
				Hold(mapMid, mapWhole);
				yield return null;
			}
			_phase = "map";
			for (int k = 0; k < 20; k++)
			{
				Hold(mapMid, mapWhole);
				yield return Grab();
			}
			object obj2;
			if (target == null)
			{
				obj2 = "the town's middle";
			}
			else
			{
				string text = WorldBoxMod.Describe(target);
				Vector2 current_position = target.current_position;
				obj2 = text + " at " + current_position.ToString();
			}
			Log.Info("REEL flying to " + (string)obj2);
			float z0 = mapWhole;
			float z1 = 16.6f;
			for (int k = 1; k <= 150; k++)
			{
				float num = (float)k / 150f;
				float t = num * num * (3f - 2f * num);
				float num2 = Mathf.Exp(Mathf.Lerp(Mathf.Log(z0), Mathf.Log(z1), t));
				float num3 = Mathf.Pow(Mathf.InverseLerp(z1, z0, num2), 1.4f);
				Vector2 vector = ((target != null && target.isAlive()) ? target.current_position : _centre);
				Vector2 at = vector + (mapMid - vector) * num3;
				_phase = ((num2 > s.GodViewZoom * 1.5f) ? "map" : ((num2 > s.GodViewZoom) ? "fade" : "god"));
				Hold(at, num2);
				yield return Grab();
			}
			_phase = "magnet";
			Camera cam = World.world.camera;
			for (int k = 1; k <= 40; k++)
			{
				ZoomOnly(Mathf.Lerp(z1, 10f, Mathf.SmoothStep(0f, 1f, (float)k / 40f)));
				GodView.TestWheel = 0.01f;
				yield return Grab();
			}
			Actor held = _mod.GodLockForTest;
			Log.Info(string.Format("REEL magnet held {0}, pull {1:F2}, map zoom {2:F1}", (held != null) ? WorldBoxMod.Describe(held) : "nothing", _mod.GodPullForTest, cam.orthographicSize));
			if (held != null && !Possess.CanJumpTo(held))
			{
				Log.Info("REEL PROBLEM the magnet holds someone who can't be taken over");
				held = null;
			}
			if (held == null && !last)
			{
				yield break;
			}
			_mod.NoDive = false;
			Actor host = held ?? target ?? NearestVillager(_centre);
			if (held != null)
			{
				_phase = "close";
				for (int k = 0; k < 24; k++)
				{
					if (!(_mod.GodCloseTargetForTest > 0.9009f))
					{
						break;
					}
					GodView.TestWheel = 1f;
					for (int j = 0; j < 5; j++)
					{
						yield return Grab();
					}
				}
				for (int k = 0; k < 24; k++)
				{
					yield return Grab();
				}
				Log.Info(string.Format("REEL close-up {0:F2}, following {1}", _mod.GodCloseForTest, (_mod.GodLockForTest != null) ? WorldBoxMod.Describe(_mod.GodLockForTest) : "nobody"));
				if (_mod.GodLockForTest == null && !last)
				{
					yield break;
				}
				_phase = "dive";
				GodView.TestWheel = 1f;
				yield return Grab();
			}
			else
			{
				Log.Info("REEL PROBLEM no magnet hold: taking " + ((host != null) ? WorldBoxMod.Describe(host) : "nobody") + " directly");
				_phase = "dive";
				if (host != null)
				{
					ControllableUnit.setControllableCreature(host);
				}
			}
			for (int k = 0; k < 180 && (!_mod.IsFirstPerson || _mod.Diving || k <= 10); k++)
			{
				if (k == 40 && !_mod.IsFirstPerson && !_mod.Diving)
				{
					if (!last)
					{
						yield break;
					}
					if (host != null)
					{
						Log.Info("REEL PROBLEM the snap didn't take: taking " + WorldBoxMod.Describe(host) + " directly");
						ControllableUnit.setControllableCreature(host);
					}
				}
				yield return Grab();
			}
			Log.Info($"REEL in the eyes: first person {_mod.IsFirstPerson}, diving {_mod.Diving}, frame {_frame}");
			if (_mod.IsFirstPerson)
			{
				_flightOk = held != null;
				_phase = "eyes";
				_mod.HintUntil = 0f;
				for (int k = 0; k < 24; k++)
				{
					yield return Grab();
				}
				Vector2 vector2 = ((_mod.Host != null) ? _mod.Host.current_position : (beach ?? _centre));
				float num4 = SeaYaw(vector2) ?? OpenYaw(vector2);
				float yaw0 = _mod.View.Yaw;
				float num5 = num4 - 0.55f;
				float turn = Mathf.DeltaAngle(yaw0 * 57.29578f, num5 * 57.29578f) * (MathF.PI / 180f);
				Log.Info($"REEL looking out to sea at {num4 * 57.29578f:F0} degrees from {vector2}");
				for (int k = 1; k <= 54; k++)
				{
					float num6 = (float)k / 54f;
					_mod.SetYaw(yaw0 + turn * num6 * num6 * (3f - 2f * num6));
					_mod.SetPitchDegrees(-3f * num6);
					yield return Grab();
				}
				_phase = "walk";
				float start = yaw0 + turn;
				for (int k = 1; k <= 96; k++)
				{
					float num7 = (float)k / 96f;
					_mod.SetYaw(start + 1f * num7 * num7 * (3f - 2f * num7));
					yield return Grab();
				}
				Log.Info($"REEL flight: {_frame} frames");
			}
		}

		private static Actor BeachVillager(City city, Vector2? beach)
		{
			if (!beach.HasValue)
			{
				return PickVillager(new Vector2(city.getTile().x, city.getTile().y));
			}
			WorldTile tile = World.world.GetTile((int)beach.Value.x, (int)beach.Value.y);
			Actor actor = null;
			try
			{
				actor = World.world.units.spawnNewUnit("human", tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
				if (actor != null && city != null && actor.city != city)
				{
					actor.joinCity(city);
				}
			}
			catch (Exception ex)
			{
				Log.Info("REEL PROBLEM beach villager: " + ex.Message);
			}
			if (actor == null || !Possess.CanJumpTo(actor))
			{
				return PickVillager(beach.Value);
			}
			return actor;
		}

		private static float? SeaYaw(Vector2 from)
		{
			Vector2 zero = Vector2.zero;
			int num = 0;
			for (int i = -16; i <= 16; i++)
			{
				for (int j = -16; j <= 16; j++)
				{
					if (j * j + i * i <= 256)
					{
						WorldTile tile = World.world.GetTile((int)from.x + j, (int)from.y + i);
						if (tile != null && tile.Type != null && tile.Type.liquid && tile.Type.ocean)
						{
							zero += new Vector2(j, i);
							num++;
						}
					}
				}
			}
			if (num < 12)
			{
				return null;
			}
			return Mathf.Atan2(zero.y, zero.x);
		}

		internal static List<WorldTile> ScenicSites()
		{
			List<WorldTile> list = KingTest.GoodSites();
			Dictionary<WorldTile, float> score = new Dictionary<WorldTile, float>();
			foreach (WorldTile item in list)
			{
				int num = 0;
				int num2 = 0;
				int num3 = 0;
				int num4 = 0;
				for (int i = -36; i <= 36; i += 3)
				{
					for (int j = -36; j <= 36; j += 3)
					{
						WorldTile tile = World.world.GetTile(item.x + j, item.y + i);
						if (tile == null || tile.Type == null)
						{
							continue;
						}
						float num5 = Mathf.Sqrt(j * j + i * i);
						if (tile.Type.liquid && tile.Type.ocean && num5 > 10f && num5 < 30f)
						{
							num++;
						}
						int num6;
						if (tile.Type.height_min < 199)
						{
							num6 = (tile.Type.block ? 1 : 0);
							if (num6 == 0)
							{
								goto IL_00fd;
							}
						}
						else
						{
							num6 = 1;
						}
						if (i > 12 && Mathf.Abs(j) < 30)
						{
							num2++;
						}
						goto IL_00fd;
						IL_00fd:
						if (num6 != 0 && i < 4 && num5 < 26f)
						{
							num3++;
						}
						if (num5 < 12f && !tile.Type.liquid && !tile.Type.block)
						{
							num4++;
						}
					}
				}
				score[item] = (float)Mathf.Min(num, 40) * 1.5f + (float)Mathf.Min(num2, 30) - (float)num3 * 4f + (float)num4 * 2f;
			}
			list.Sort((WorldTile a, WorldTile b) => score[b].CompareTo(score[a]));
			if (list.Count > 0)
			{
				Log.Info($"REEL {list.Count} sites, best {list[0].x},{list[0].y} scores {score[list[0]]:F0}");
			}
			return list;
		}

		internal static int BuildTown(City city, WorldTile c, TerrainCache cache)
		{
			System.Random random = new System.Random(11);
			float g0 = Ground(cache, c.x, c.y);
			List<Vector2> taken = new List<Vector2>();
			int placed = 0;
			Try("hall_human_0", c.x, c.y);
			Queue<string> queue = new Queue<string>(new string[6] { "well", "temple_human", "market_human", "library_human", "stockpile", "barracks_human" });
			Queue<string> queue2 = new Queue<string>(new string[3] { "windmill_human_0", "watch_tower_human", "windmill_human_0" });
			string[] array = new string[10] { "house_human_1", "house_human_2", "house_human_0", "house_human_3", "house_human_1", "house_human_2", "house_human_4", "house_human_1", "house_human_3", "house_human_2" };
			int num = 0;
			float[] array2 = new float[4] { 6.5f, 11f, 15.5f, 20f };
			foreach (float num2 in array2)
			{
				int num3 = Mathf.FloorToInt(MathF.PI * 2f * num2 / 5.2f);
				float num4 = (float)random.NextDouble() * MathF.PI * 2f;
				for (int j = 0; j < num3; j++)
				{
					float f = num4 + (float)j * MathF.PI * 2f / (float)num3 + (float)(random.NextDouble() - 0.5) * 0.35f;
					float num5 = num2 + (float)(random.NextDouble() - 0.5) * 2f;
					float x = (float)c.x + Mathf.Cos(f) * num5;
					float y = (float)c.y + Mathf.Sin(f) * num5;
					Queue<string> queue3 = ((num2 < 12f && j % 3 == 1) ? queue : ((num2 > 18f && j % 4 == 0) ? queue2 : null));
					if (queue3 != null && queue3.Count > 0 && Try(queue3.Peek(), x, y))
					{
						queue3.Dequeue();
					}
					else if ((!(num2 > 18f) || !(random.NextDouble() < 0.35)) && Try(array[num % array.Length], x, y))
					{
						num++;
					}
				}
			}
			return placed;
			bool Try(string id, float f2, float f3)
			{
				int num6 = Mathf.RoundToInt(f2);
				int num7 = Mathf.RoundToInt(f3);
				foreach (Vector2 item in taken)
				{
					if ((item - new Vector2(num6, num7)).sqrMagnitude < 21.16f)
					{
						return false;
					}
				}
				for (int k = -1; k <= 1; k++)
				{
					for (int l = -1; l <= 1; l++)
					{
						WorldTile tile = World.world.GetTile(num6 + l, num7 + k);
						if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block || tile.Type.lava)
						{
							return false;
						}
						if (Mathf.Abs(Ground(cache, num6 + l, num7 + k) - g0) > 3f)
						{
							return false;
						}
					}
				}
				if (AssetManager.buildings.get(id) == null)
				{
					Log.Info("REEL PROBLEM no building " + id);
					return false;
				}
				if (Put(city, id, num6, num7) == null)
				{
					return false;
				}
				taken.Add(new Vector2(num6, num7));
				placed++;
				return true;
			}
		}

		internal static float Ground(TerrainCache cache, int x, int y)
		{
			if (cache == null || cache.Width == 0)
			{
				return 0f;
			}
			x = Mathf.Clamp(x, 0, cache.Width - 1);
			y = Mathf.Clamp(y, 0, cache.Height - 1);
			return cache.Ground[y * cache.Width + x];
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
				Log.Info("REEL PROBLEM building " + id + ": " + ex.Message);
				return null;
			}
		}

		internal static Actor PickVillager(Vector2 at)
		{
			Actor actor = null;
			float num = float.MaxValue;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple == null || !simple.isAlive() || simple.asset == null || simple.asset.id != "human" || !simple.isAdult() || !Possess.CanJumpTo(simple))
				{
					continue;
				}
				float num2 = Vector2.Distance(simple.current_position, at);
				if (num2 > 14f)
				{
					continue;
				}
				float num3 = 0f;
				foreach (Actor simple2 in World.world.units.getSimpleList())
				{
					if (simple2 != null && simple2 != simple && simple2.isAlive() && Vector2.Distance(simple2.current_position, simple.current_position) < 2.5f)
					{
						num3 += 3f;
					}
				}
				WorldTile current_tile = simple.current_tile;
				float num4 = Mathf.Abs(num2 - 4f) + num3 + ((current_tile != null && current_tile.building != null && !current_tile.building.asset.flora) ? 6f : 0f);
				if (num4 < num)
				{
					num = num4;
					actor = simple;
				}
			}
			return actor ?? NearestVillager(at);
		}

		private static Actor NearestVillager(Vector2 at)
		{
			Actor result = null;
			float num = float.MaxValue;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple.asset != null && !(simple.asset.id != "human") && !simple.is_inside_building)
				{
					float num2 = Vector2.Distance(simple.current_position, at);
					if (num2 < num)
					{
						num = num2;
						result = simple;
					}
				}
			}
			return result;
		}

		private float OpenYaw(Vector2 from)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			float[] array = new float[36];
			float num = Ground(terrainForTest, (int)from.x, (int)from.y);
			for (int i = 0; i < 36; i++)
			{
				float f = (float)i * MathF.PI * 2f / 36f;
				Vector2 vector = new Vector2(Mathf.Cos(f), Mathf.Sin(f));
				float num2 = 1.5f;
				float num3 = 0f;
				for (; num2 < 40f; num2 += 0.5f)
				{
					Vector2 vector2 = from + vector * num2;
					WorldTile tile = World.world.GetTile((int)vector2.x, (int)vector2.y);
					if (tile == null)
					{
						break;
					}
					if (tile.Type != null && tile.Type.liquid && tile.Type.ocean)
					{
						num3 += 0.5f;
					}
					if ((tile.building != null && tile.building.asset != null && !tile.building.asset.flora && tile.building.isAlive()) || Ground(terrainForTest, (int)vector2.x, (int)vector2.y) > num + 1.5f + num2 * 0.15f)
					{
						break;
					}
					int num4 = (int)vector2.y * terrainForTest.Width + (int)vector2.x;
					if (terrainForTest.Width > 0 && num4 >= 0 && num4 < terrainForTest.Flags.Length && (terrainForTest.Flags[num4] & 8) != 0)
					{
						break;
					}
				}
				array[i] = num2 + Mathf.Min(num3, 15f);
			}
			int num5 = 0;
			float num6 = float.MinValue;
			for (int j = 0; j < 36; j++)
			{
				float num7 = array[j] * 2f + array[(j + 1) % 36] + array[(j + 36 - 1) % 36];
				if (num7 > num6)
				{
					num6 = num7;
					num5 = j;
				}
			}
			return (float)num5 * MathF.PI * 2f / 36f;
		}

		private IEnumerator VillageStills(Settings s)
		{
			TerrainCache cache = _mod.TerrainForTest;
			Vector2 eye = _centre + new Vector2(-18f, -12f);
			float num = float.MinValue;
			for (float num2 = 17f; num2 <= 26f; num2 += 3f)
			{
				for (int i = 0; i < 24; i++)
				{
					float f = (float)i * MathF.PI * 2f / 24f;
					int num3 = Mathf.RoundToInt(_centre.x + Mathf.Cos(f) * num2);
					int num4 = Mathf.RoundToInt(_centre.y + Mathf.Sin(f) * num2);
					WorldTile tile = World.world.GetTile(num3, num4);
					if (tile != null && tile.Type != null && !tile.Type.liquid && !tile.Type.block && (tile.building == null || !tile.building.isKingdomCiv()))
					{
						float num5 = Ground(cache, num3, num4) - num2 * 0.05f;
						if (num5 > num)
						{
							num = num5;
							eye = new Vector2((float)num3 + 0.5f, (float)num4 + 0.5f);
						}
					}
				}
			}
			float num6 = Mathf.Atan2(_centre.y - eye.y, _centre.x - eye.x);
			float num7 = Mathf.Clamp((0f - Mathf.Atan2(Ground(cache, (int)eye.x, (int)eye.y) + 3f - Ground(cache, (int)_centre.x, (int)_centre.y), Vector2.Distance(eye, _centre))) * 57.29578f * 0.8f, -18f, 2f);
			Log.Info($"REEL village eye {eye} ({num:F1} high), yaw {num6 * 57.29578f:F0}, pitch {num7:F1}");
			_mod.DebugEyeSet = true;
			_mod.DebugEye = eye;
			_mod.DebugEyeHeight = 3f;
			_mod.SetYaw(num6);
			_mod.SetPitchDegrees(num7);
			int[] array = new int[3] { 1, 2, 3 };
			for (int j = 0; j < array.Length; j++)
			{
				int mode = (s.TimeMode = array[j]);
				SceneCollector.AllLit = mode == 3;
				_mod.ForceRelight();
				yield return new WaitForSecondsRealtime(2f);
				ShowreelTest showreelTest = this;
				object obj;
				switch (mode)
				{
				default:
					obj = "night";
					break;
				case 2:
					obj = "golden";
					break;
				case 1:
					obj = "day";
					break;
				}
				yield return showreelTest.Still("still_village_" + (string)obj + ".png");
			}
			SceneCollector.AllLit = false;
			Vector2 vector = eye;
			float num8 = float.MinValue;
			for (int k = 4; k < MapBox.height - 4; k += 3)
			{
				for (int l = 4; l < MapBox.width - 4; l += 3)
				{
					float num9 = Vector2.Distance(new Vector2(l, k), eye);
					if (!(num9 < 30f) && !(num9 > 110f))
					{
						float num10 = Ground(cache, l, k);
						if (num10 > num8)
						{
							num8 = num10;
							vector = new Vector2(l, k);
						}
					}
				}
			}
			s.TimeMode = 2;
			_mod.ForceRelight();
			_mod.SetYaw(Mathf.Atan2(vector.y - eye.y, vector.x - eye.x));
			_mod.SetPitchDegrees(3f);
			yield return new WaitForSecondsRealtime(2f);
			yield return Still("still_vista_golden.png");
			Vector2? vector2 = Shore(_centre);
			if (vector2.HasValue)
			{
				Vector2 vector3 = vector2.Value;
				for (float num11 = 3f; num11 < 30f; num11 += 1f)
				{
					bool flag = false;
					for (int m = 0; m < 16; m++)
					{
						if (flag)
						{
							break;
						}
						float f2 = (float)m * MathF.PI / 8f;
						Vector2 vector4 = vector2.Value + new Vector2(Mathf.Cos(f2), Mathf.Sin(f2)) * num11;
						WorldTile tile2 = World.world.GetTile((int)vector4.x, (int)vector4.y);
						if (tile2 != null && tile2.Type != null && tile2.Type.liquid && tile2.Type.ocean)
						{
							vector3 = vector4;
							flag = true;
						}
					}
					if (flag)
					{
						break;
					}
				}
				_mod.DebugEye = vector2.Value;
				_mod.DebugEyeHeight = 0f;
				_mod.SetYaw(Mathf.Atan2(vector3.y - vector2.Value.y, vector3.x - vector2.Value.x));
				_mod.SetPitchDegrees(1f);
				yield return new WaitForSecondsRealtime(2f);
				yield return Still("still_sea_golden.png");
			}
			s.TimeMode = 1;
			_mod.ForceRelight();
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			yield return new WaitForSecondsRealtime(1f);
		}

		internal static Vector2? Shore(Vector2 from)
		{
			Vector2? result = null;
			float num = float.MaxValue;
			for (int i = (int)from.y - 40; i <= (int)from.y + 40; i++)
			{
				for (int j = (int)from.x - 40; j <= (int)from.x + 40; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block || tile.building != null)
					{
						continue;
					}
					float num2 = ((float)j - from.x) * ((float)j - from.x) + ((float)i - from.y) * ((float)i - from.y);
					if (num2 >= num)
					{
						continue;
					}
					int num3 = 0;
					Vector2Int[] array = new Vector2Int[4]
					{
						new Vector2Int(1, 0),
						new Vector2Int(-1, 0),
						new Vector2Int(0, 1),
						new Vector2Int(0, -1)
					};
					for (int k = 0; k < array.Length; k++)
					{
						Vector2Int vector2Int = array[k];
						for (int l = 1; l <= 4; l++)
						{
							WorldTile tile2 = World.world.GetTile(j + vector2Int.x * l, i + vector2Int.y * l);
							if (tile2 != null && tile2.Type != null && tile2.Type.liquid && tile2.Type.ocean)
							{
								num3++;
							}
						}
					}
					if (num3 >= 4)
					{
						result = new Vector2((float)j + 0.5f, (float)i + 0.5f);
						num = num2;
					}
				}
			}
			return result;
		}

		internal static void Hold(Vector2 at, float zoom)
		{
			try
			{
				Camera camera = World.world.camera;
				camera.transform.position = new Vector3(at.x, at.y, camera.transform.position.z);
				ZoomOnly(zoom);
			}
			catch (Exception ex)
			{
				Log.Info("REEL PROBLEM camera: " + ex.Message);
			}
		}

		internal static void ZoomOnly(float zoom)
		{
			try
			{
				World.world.camera.orthographicSize = zoom;
				if (World.world.move_camera != null)
				{
					World.world.move_camera.setTargetZoom(zoom);
				}
				World.world.setZoomOrthographic(zoom);
			}
			catch (Exception ex)
			{
				Log.Info("REEL PROBLEM zoom: " + ex.Message);
			}
		}

		private IEnumerator HoldFor(Vector2 at, float zoom, float seconds)
		{
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				Hold(at, zoom);
				yield return null;
			}
		}

		private void HideCanvases(bool all)
		{
			Canvas[] array = UnityEngine.Object.FindObjectsOfType<Canvas>();
			foreach (Canvas canvas in array)
			{
				if (!(canvas == null) && canvas.isRootCanvas && canvas.enabled && canvas.renderMode != RenderMode.WorldSpace && (all || canvas.name.IndexOf("Debug", StringComparison.Ordinal) >= 0 || canvas.name.IndexOf("Tooltip", StringComparison.Ordinal) >= 0))
				{
					canvas.enabled = false;
					_hidden.Add(canvas);
				}
			}
			Log.Info(string.Format("REEL hid {0} canvases: {1}", _hidden.Count, string.Join(", ", _hidden.ConvertAll((Canvas c) => c.name))));
		}

		private void ShowCanvases()
		{
			foreach (Canvas item in _hidden)
			{
				if (item != null)
				{
					item.enabled = true;
				}
			}
			_hidden.Clear();
		}

		private IEnumerator Grab()
		{
			yield return new WaitForEndOfFrame();
			Texture2D texture2D = null;
			Texture2D texture2D2 = null;
			try
			{
				texture2D = ScreenCapture.CaptureScreenshotAsTexture();
				int num = ((texture2D.width < 2400) ? 1 : 2);
				int num2 = texture2D.width / num;
				int num3 = texture2D.height / num;
				Color32[] pixels = texture2D.GetPixels32();
				Color32[] array = new Color32[num2 * num3];
				int width = texture2D.width;
				for (int i = 0; i < num3; i++)
				{
					for (int j = 0; j < num2; j++)
					{
						if (num == 1)
						{
							array[i * num2 + j] = pixels[i * width + j];
							continue;
						}
						Color32 color = pixels[2 * i * width + 2 * j];
						Color32 color2 = pixels[2 * i * width + 2 * j + 1];
						Color32 color3 = pixels[(2 * i + 1) * width + 2 * j];
						Color32 color4 = pixels[(2 * i + 1) * width + 2 * j + 1];
						array[i * num2 + j] = new Color32((byte)(color.r + color2.r + color3.r + color4.r + 2 >> 2), (byte)(color.g + color2.g + color3.g + color4.g + 2 >> 2), (byte)(color.b + color2.b + color3.b + color4.b + 2 >> 2), byte.MaxValue);
					}
				}
				texture2D2 = new Texture2D(num2, num3, TextureFormat.RGB24, mipChain: false);
				texture2D2.SetPixels32(array);
				texture2D2.Apply();
				string text = $"seq_{_frame:D4}.jpg";
				File.WriteAllBytes(Path.Combine(_dir, text), texture2D2.EncodeToJPG(92));
				Camera camera = World.world.camera;
				_meta.AppendLine($"{text}\t{_phase}\tzoom {camera.orthographicSize:F2}\tgod {_mod.GodAlpha:F2}\tclose {_mod.GodCloseForTest:F2}\tfp {_mod.IsFirstPerson}\tdiving {_mod.Diving}\tdt {Time.unscaledDeltaTime:F4}");
				if (_frame % 30 == 0)
				{
					Log.Info($"REEL frame {_frame} {_phase}: zoom {camera.orthographicSize:F1}, god {_mod.GodAlpha:F2}, close {_mod.GodCloseForTest:F2}, dt {Time.unscaledDeltaTime:F4}, game dt {Time.deltaTime:F4}");
				}
				_frame++;
			}
			catch (Exception ex)
			{
				Log.Info("REEL PROBLEM grab: " + ex.Message);
			}
			finally
			{
				if (texture2D != null)
				{
					UnityEngine.Object.Destroy(texture2D);
				}
				if (texture2D2 != null)
				{
					UnityEngine.Object.Destroy(texture2D2);
				}
			}
		}

		private IEnumerator Still(string name)
		{
			yield return new WaitForEndOfFrame();
			ScreenCapture.CaptureScreenshot(Path.Combine(_dir, name));
			yield return null;
			yield return null;
			Log.Info("REEL still " + name);
		}

		private void Finish(bool quit, Settings s, int mode, int res, bool god)
		{
			Time.captureFramerate = 0;
			ShowCanvases();
			GodView.TestMouse = null;
			GodView.TestWheel = null;
			SceneCollector.AllLit = false;
			s.TimeMode = mode;
			s.Resolution = res;
			s.GodView = god;
			s.GodViewZoom = _savedGodZoom;
			_mod.NoDive = true;
			_mod.DebugEyeSet = false;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			if (_savedSettings != null)
			{
				try
				{
					File.WriteAllText(Path.Combine(Log.Folder, "settings.txt"), _savedSettings);
				}
				catch
				{
				}
			}
			Log.Info("REEL DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
