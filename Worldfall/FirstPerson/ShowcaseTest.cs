using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class ShowcaseTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private Settings _s;

		private string _dir;

		private string _savedSettings;

		private Actor _king;

		private Actor _dragon;

		private Vector2 _centre;

		private Vector2? _shore;

		private int _shots;

		private readonly List<Canvas> _hidden = new List<Canvas>();

		private static readonly string[] MeadowTrees = new string[4] { "tree_green_1", "tree_green_2", "tree_green_3", "tree_green_2" };

		private static readonly string[] OtherBiomes = new string[25]
		{
			"jungle", "clover", "savanna", "swamp", "desert", "cacti", "lemon", "candy", "crystal", "corrupted",
			"infernal", "flame", "mushroom", "wasteland", "birch", "maple", "rocklands", "garlic", "celestial", "singularity",
			"paradox", "enchanted", "snow", "pine", "palm"
		};

		private readonly List<Vector2> _spots = new List<Vector2>();

		private static readonly string[] SitePeoples = new string[12]
		{
			"cat", "necromancer", "ghost", "demon", "unicorn", "crystal_golem", "candy_man", "fairy", "white_mage", "angle",
			"liliar", "wolf"
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
			_s = _mod.Settings;
			string path2 = Path.Combine(Log.Folder, "settings.txt");
			try
			{
				_savedSettings = (File.Exists(path2) ? File.ReadAllText(path2) : null);
			}
			catch
			{
			}
			int savedMode = _s.TimeMode;
			int savedRes = _s.Resolution;
			bool savedGod = _s.GodView;
			float savedDraw = _s.DrawDistance;
			float savedGodZoom = _s.GodViewZoom;
			_s.Resolution = WorldBoxMod.ResolutionWidths.Length - 1;
			if (options.Contains("lowq"))
			{
				_s.Resolution = 1;
				SettingsMenu.SetQuality(_s, 0);
			}
			_s.TimeMode = 1;
			_s.GodView = false;
			_s.GodViewZoom = 40f;
			_s.DrawDistance = 600f;
			_dir = Path.Combine(Log.Folder, "showcase");
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
			foreach (WorldTile item in ShowreelTest.ScenicSites())
			{
				_king = KingTest.FoundKingdom(item, "human", 22, 0);
				if (_king != null)
				{
					worldTile = item;
					break;
				}
			}
			if (_king == null)
			{
				Log.Info("SHOW PROBLEM no village");
				Finish(quit, savedMode, savedRes, savedGod, savedDraw, savedGodZoom);
				yield break;
			}
			_centre = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f);
			int num = ShowreelTest.BuildTown(_king.city, worldTile, _mod.TerrainForTest);
			_shore = ShowreelTest.Shore(_centre);
			Log.Info(string.Format("SHOW village {0} at {1},{2}: {3} buildings, shore {4}", _king.city.name, worldTile.x, worldTile.y, num, _shore.HasValue ? _shore.Value.ToString() : "none"));
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			ControllableUnit.setControllableCreature(_king);
			for (float t = 0f; t < 10f; t += 0.5f)
			{
				if (_mod.IsFirstPerson)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.5f);
			}
			if (!_mod.IsFirstPerson)
			{
				Log.Info("SHOW PROBLEM first person didn't start");
				Finish(quit, savedMode, savedRes, savedGod, savedDraw, savedGodZoom);
				yield break;
			}
			_mod.HintUntil = 0f;
			HideCanvases();
			yield return new WaitForSecondsRealtime(8f);
			if (options.Contains("farshore"))
			{
				yield return Run("far shore", FarShore());
			}
			else if (options.Contains("refresh"))
			{
				yield return Refresh();
			}
			else if (options.Contains("styles"))
			{
				yield return Run("styles", Styles());
			}
			else if (options.Contains("nightlights"))
			{
				yield return Run("nightlights", NightLights());
			}
			else if (options.Contains("harbor"))
			{
				yield return Run("harbor", Harbor());
			}
			else if (options.Contains("more"))
			{
				yield return More();
			}
			else if (options.Contains("site"))
			{
				yield return Site();
			}
			else
			{
				yield return Run("vantage", Vantage());
				yield return Run("ship", Ship());
				yield return Run("dragon", Dragon());
				yield return Run("god view", GodShots());
				yield return Run("nuke", Nuke());
				yield return Run("crabzilla", Crab());
			}
			Log.Info($"SHOW {_shots} pictures");
			Finish(quit, savedMode, savedRes, savedGod, savedDraw, savedGodZoom);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("SHOW --- " + name);
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
					goto IL_007d;
				}
				catch (Exception ex)
				{
					Log.Info("SHOW PROBLEM " + name + " failed: " + ex);
				}
				break;
				IL_007d:
				yield return current;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			bool[] scriptedFire = Crabzillas.ScriptedFire;
			bool[] scriptedFire2 = Crabzillas.ScriptedFire;
			bool flag = false;
			scriptedFire2[1] = false;
			scriptedFire[0] = flag;
			FrameRenderer renderer = _mod.Renderer;
			if (renderer != null)
			{
				renderer.ForceComet = (renderer.ForceShower = (renderer.ForceAurora = -1f));
			}
			SceneCollector.AllLit = false;
		}

		private void Hour(int mode)
		{
			_s.TimeMode = mode;
			SceneCollector.AllLit = mode == 3;
			_mod.ForceRelight();
		}

		private float GroundAt(Vector2 p)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			if (terrainForTest == null || terrainForTest.Width <= 0)
			{
				return 0f;
			}
			return terrainForTest.SampleHeight(p.x, p.y);
		}

		private void Eye(Vector2 at, float height, Vector2 lookAt, float lookZ)
		{
			_mod.DebugEyeSet = true;
			_mod.DebugEye = at;
			_mod.DebugEyeHeight = height;
			float num = GroundAt(at) + Mathf.Max(height, 1.4f);
			float x = Mathf.Max(1f, Vector2.Distance(at, lookAt));
			_mod.SetYaw(Mathf.Atan2(lookAt.y - at.y, lookAt.x - at.x));
			_mod.SetPitchDegrees(Mathf.Clamp(Mathf.Atan2(lookZ - num, x) * 57.29578f, -24f, 30f));
		}

		private IEnumerator Vantage()
		{
			Vector2 vector = (_shore.HasValue ? (_shore.Value - _centre).normalized : Vector2.up);
			Vector2 vector2 = _centre - vector * 17f;
			for (int i = 0; i < 8; i++)
			{
				if (Dry(vector2))
				{
					break;
				}
				vector2 = Vector2.Lerp(vector2, _centre, 0.25f);
			}
			Vector2 lookAt = _centre + vector * 8f;
			Eye(vector2, 9f, lookAt, GroundAt(_centre));
			int[] array = new int[3] { 1, 2, 3 };
			foreach (int mode in array)
			{
				Hour(mode);
				yield return new WaitForSecondsRealtime(2.2f);
				ShowcaseTest showcaseTest = this;
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
				yield return showcaseTest.Shot("town_" + (string)obj);
			}
			FrameRenderer r = _mod.Renderer;
			r.ForceAurora = 1f;
			_mod.SetYaw(0f);
			_mod.SetPitchDegrees(9f);
			yield return new WaitForSecondsRealtime(2.5f);
			yield return Shot("sky_aurora");
			r.ForceAurora = 0f;
			r.ForceComet = 1f;
			yield return null;
			yield return null;
			Vec3 cometDirection = r.CometDirection;
			_mod.SetYaw(Mathf.Atan2(cometDirection.Y, cometDirection.X));
			_mod.SetPitchDegrees(Mathf.Min(28f, Mathf.Asin(Mathf.Clamp(cometDirection.Z, -1f, 1f)) * 57.29578f * 0.8f));
			yield return new WaitForSecondsRealtime(1.5f);
			yield return Shot("sky_comet");
			r.ForceComet = 0f;
			r.ForceShower = 1f;
			r.ForceAurora = 1f;
			_mod.SetYaw(0.4f);
			_mod.SetPitchDegrees(14f);
			for (int j = 0; j < 3; j++)
			{
				yield return new WaitForSecondsRealtime(1.2f);
				yield return Shot($"sky_shower_{j}");
			}
			Hour(2);
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			yield return new WaitForSecondsRealtime(1f);
		}

		private static bool Dry(Vector2 p)
		{
			WorldTile tile = World.world.GetTile((int)p.x, (int)p.y);
			if (tile != null && tile.Type != null && !tile.Type.liquid)
			{
				return !tile.Type.lava;
			}
			return false;
		}

		private static bool Sea(int x, int y)
		{
			WorldTile tile = World.world.GetTile(x, y);
			if (tile != null && tile.Type != null && tile.Type.liquid)
			{
				return tile.Type.ocean;
			}
			return false;
		}

		private IEnumerator Ship()
		{
			if (!_shore.HasValue)
			{
				Log.Info("SHOW PROBLEM no shore for the ship");
				yield break;
			}
			Vector2 normalized = (_shore.Value - _centre).normalized;
			Vector2? vector = null;
			for (float num = 8f; num < 26f; num += 1f)
			{
				if (vector.HasValue)
				{
					break;
				}
				for (int i = -4; i <= 4; i++)
				{
					if (vector.HasValue)
					{
						break;
					}
					Vector2 value = _shore.Value + (Vector2)(Quaternion.Euler(0f, 0f, (float)i * 12f) * normalized) * num;
					bool flag = true;
					for (int j = -2; j <= 2 && flag; j++)
					{
						for (int k = -2; k <= 2 && flag; k++)
						{
							if (!Sea((int)value.x + k, (int)value.y + j))
							{
								flag = false;
							}
						}
					}
					if (flag)
					{
						vector = value;
					}
				}
			}
			if (!vector.HasValue)
			{
				Log.Info("SHOW PROBLEM no open water for the ship");
				yield break;
			}
			Actor boat = null;
			try
			{
				boat = World.world.units.createNewUnit("boat_transport_human", World.world.GetTile((int)vector.Value.x, (int)vector.Value.y));
				if (boat != null && _king.kingdom != null)
				{
					boat.setKingdom(_king.kingdom);
				}
			}
			catch (Exception ex)
			{
				Log.Info("SHOW PROBLEM ship: " + ex.Message);
			}
			if (boat == null)
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(0.5f);
			Hour(2);
			if (!Possess.Swap(boat))
			{
				Log.Info("SHOW PROBLEM couldn't take the helm");
				yield break;
			}
			yield return new WaitForSecondsRealtime(2.5f);
			_mod.HintUntil = 0f;
			Vector2 p = boat.current_position;
			_mod.SetYaw(Mathf.Atan2(_centre.y - p.y, _centre.x - p.x));
			_mod.SetPitchDegrees(2f);
			yield return new WaitForSecondsRealtime(1.2f);
			yield return Shot("ship_town");
			_mod.SetYaw(Mathf.Atan2(p.y - _centre.y, p.x - _centre.x) + 0.5f);
			yield return new WaitForSecondsRealtime(1.2f);
			yield return Shot("ship_sea");
			Possess.Swap(_king);
			yield return new WaitForSecondsRealtime(2.5f);
			try
			{
				if (boat.isAlive())
				{
					boat.dieSimpleNone();
				}
			}
			catch
			{
			}
		}

		private IEnumerator Dragon()
		{
			Hour(2);
			Vector2 vector = _centre + new Vector2(32f, 14f);
			WorldTile tile = World.world.GetTile(Mathf.Clamp((int)vector.x, 2, MapBox.width - 3), Mathf.Clamp((int)vector.y, 2, MapBox.height - 3));
			try
			{
				_dragon = World.world.units.spawnNewUnit("dragon", tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Info("SHOW PROBLEM dragon: " + ex.Message);
			}
			if (_dragon == null)
			{
				yield break;
			}
			Log.Info($"SHOW dragon at {_dragon.current_position}, jointed model {Dragons.Ready}");
			Vector2 eye = _centre + (vector - _centre).normalized * 9f;
			if (!Dry(eye))
			{
				eye = _centre;
			}
			yield return Follow(eye, 9f, 1.3f, "dragon_fly");
			Dragon dragon = null;
			try
			{
				dragon = _dragon.getActorComponent<Dragon>();
			}
			catch
			{
			}
			if (dragon != null)
			{
				int num = 0;
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple.isAlive() && simple != _king && simple.asset != null && simple.asset.id == "human" && Vector2.Distance(simple.current_position, _centre) < 12f && num < 4)
					{
						dragon.aggroTargets.Add(simple);
						num++;
					}
				}
				try
				{
					_dragon.setFlip(_dragon.current_position.x > _centre.x);
					_dragon.setTask("dragon_slide");
				}
				catch (Exception ex2)
				{
					Log.Info("SHOW PROBLEM dragon task: " + ex2.Message);
				}
				Log.Info($"SHOW dragon set on {num} villagers");
			}
			yield return Follow(eye, 16f, 1.1f, "dragon_fire");
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
		}

		private IEnumerator Follow(Vector2 eye, float seconds, float every, string name)
		{
			float next = 0.8f;
			int k = 0;
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				if (_dragon == null)
				{
					break;
				}
				if (!_dragon.isAlive())
				{
					break;
				}
				Vector3 vector = _dragon.current_position;
				float lookZ = GroundAt(vector) + 4f;
				if (Dragons.Probe(_dragon, out var pos, out var _, out var _, out var _, out var _, out var _))
				{
					vector = new Vector2(pos.X, pos.Y);
					lookZ = pos.Z;
				}
				Vector2 vector2 = vector;
				float num = Vector2.Distance(vector2, eye);
				if (num > 60f || num < 14f)
				{
					eye = vector2 + (eye - vector2).normalized * 30f;
				}
				if (!Dry(eye))
				{
					eye = Vector2.Lerp(eye, _centre, 0.3f);
				}
				Eye(eye, 1.6f, vector2, lookZ);
				if (t >= next)
				{
					next += every;
					yield return Shot($"{name}_{k++:00}");
				}
				yield return null;
			}
		}

		private IEnumerator GodShots()
		{
			_mod.NoDive = true;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			yield return new WaitForSecondsRealtime(1f);
			_s.GodView = true;
			Hour(2);
			for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime)
			{
				ShowreelTest.Hold(_centre, 30f);
				yield return null;
			}
			yield return Shot("god_town_golden");
			if (_dragon != null && _dragon.isAlive())
			{
				for (int k = 0; k < 6; k++)
				{
					if (!_dragon.isAlive())
					{
						break;
					}
					for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
					{
						Vector2 at = _dragon.current_position;
						if (Dragons.Probe(_dragon, out var pos, out var _, out var _, out var _, out var _, out var _))
						{
							at = new Vector2(pos.X, pos.Y - 3f);
						}
						ShowreelTest.Hold(at, (k < 3) ? 20f : 14f);
						yield return null;
					}
					yield return Shot($"god_dragon_{k}");
				}
			}
			Hour(3);
			for (float t = 0f; t < 2.5f; t += Time.unscaledDeltaTime)
			{
				ShowreelTest.Hold(_centre, 24f);
				yield return null;
			}
			yield return Shot("god_town_night");
			Hour(2);
			_s.GodView = false;
			ControllableUnit.setControllableCreature(_king);
			for (float t = 0f; t < 6f; t += 0.25f)
			{
				if (_mod.IsFirstPerson)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.25f);
			}
			_mod.HintUntil = 0f;
		}

		private IEnumerator Nuke()
		{
			if (!_mod.IsFirstPerson)
			{
				Log.Info("SHOW PROBLEM not in first person for the nuke");
				yield break;
			}
			Hour(2);
			Vector2 site = Vector2.zero;
			float num = float.MaxValue;
			for (int i = 0; i < 48; i++)
			{
				for (float num2 = 75f; num2 <= 100f; num2 += 5f)
				{
					float f = (float)i * MathF.PI * 2f / 48f;
					Vector2 vector = _centre + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num2;
					if (vector.x < 10f || vector.y < 10f || vector.x > (float)(MapBox.width - 10) || vector.y > (float)(MapBox.height - 10))
					{
						continue;
					}
					int num3 = 0;
					for (int j = -6; j <= 6; j += 3)
					{
						for (int k = -6; k <= 6; k += 3)
						{
							if (!Dry(vector + new Vector2(k, j)))
							{
								num3++;
							}
						}
					}
					float num4 = (float)num3 * 10f + Mathf.Abs(num2 - 85f) * 0.2f;
					if (num4 < num)
					{
						num = num4;
						site = vector;
					}
				}
			}
			if (num > 30f)
			{
				Log.Info("SHOW PROBLEM no open land for the nuke");
				yield break;
			}
			Vector2 vector2 = _centre + (site - _centre).normalized * 12f;
			if (!Dry(vector2))
			{
				vector2 = _centre;
			}
			Eye(vector2, 4f, site, GroundAt(site) + 18f);
			yield return new WaitForSecondsRealtime(1f);
			int before = SceneCollector.NukeCount;
			try
			{
				World.world.drop_manager.spawn(World.world.GetTile((int)site.x, (int)site.y), "atomic_bomb", 10f, -1f, -1L);
			}
			catch (Exception ex)
			{
				Log.Info("SHOW PROBLEM nuke: " + ex.Message);
				yield break;
			}
			float waited;
			for (waited = 0f; waited < 15f; waited += Time.unscaledDeltaTime)
			{
				if (SceneCollector.NukeCount > before)
				{
					break;
				}
				yield return null;
			}
			Log.Info($"SHOW nuke went off after {waited:F1} s at {site}, {Vector2.Distance(site, _centre):F0} tiles from town");
			float t0 = Time.unscaledTime;
			float[] array = new float[7] { 0.4f, 1.5f, 3f, 5f, 8f, 12f, 17f };
			foreach (float at in array)
			{
				while (Time.unscaledTime - t0 < at)
				{
					yield return null;
				}
				yield return Shot($"nuke_{at:00.0}");
			}
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
		}

		private IEnumerator Crab()
		{
			Hour(2);
			Vector2 vector = _centre + new Vector2(-20f, -16f);
			for (int i = 0; i < 8; i++)
			{
				if (Dry(vector))
				{
					break;
				}
				vector = Vector2.Lerp(vector, _centre, 0.2f);
			}
			Actor crab = null;
			try
			{
				crab = World.world.units.spawnNewUnit("crabzilla", World.world.GetTile((int)vector.x, (int)vector.y), pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Info("SHOW PROBLEM crabzilla: " + ex.Message);
			}
			if (crab == null)
			{
				yield break;
			}
			for (float t = 0f; t < 8f; t += 0.25f)
			{
				if (_mod.IsFirstPerson && Crabzillas.You == crab)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.25f);
			}
			if (!_mod.IsFirstPerson || Crabzillas.You != crab)
			{
				Log.Info("SHOW PROBLEM not in Crabzilla's eyes");
				yield break;
			}
			_mod.HintUntil = 0f;
			yield return new WaitForSecondsRealtime(1.5f);
			ViewState view = _mod.View;
			Vector2 vector2 = _centre - new Vector2(view.X, view.Y);
			_mod.SetYaw(Mathf.Atan2(vector2.y, vector2.x));
			_mod.SetPitchDegrees(Mathf.Atan2(GroundAt(_centre) - view.Z, vector2.magnitude) * 57.29578f * 0.7f);
			yield return new WaitForSecondsRealtime(1f);
			yield return Shot("crab_town");
			Crabzillas.ScriptedFire[0] = true;
			yield return new WaitForSecondsRealtime(0.7f);
			Crabzillas.ScriptedFire[1] = true;
			for (int k = 0; k < 4; k++)
			{
				yield return new WaitForSecondsRealtime(0.6f);
				yield return Shot($"crab_fire_{k}");
			}
			bool[] scriptedFire = Crabzillas.ScriptedFire;
			bool[] scriptedFire2 = Crabzillas.ScriptedFire;
			bool flag = false;
			scriptedFire2[1] = false;
			scriptedFire[0] = flag;
			yield return new WaitForSecondsRealtime(1.5f);
			yield return Shot("crab_after");
		}

		private IEnumerator Shot(string name)
		{
			yield return new WaitForEndOfFrame();
			ScreenCapture.CaptureScreenshot(Path.Combine(_dir, name + ".png"));
			_mod.SaveFrame(Path.Combine("showcase", name + "_f.png"));
			_shots++;
			yield return null;
		}

		private void HideCanvases()
		{
			Canvas[] array = UnityEngine.Object.FindObjectsOfType<Canvas>();
			foreach (Canvas canvas in array)
			{
				if (!(canvas == null) && canvas.isRootCanvas && canvas.enabled && canvas.renderMode != RenderMode.WorldSpace)
				{
					canvas.enabled = false;
					_hidden.Add(canvas);
				}
			}
		}

		private void Finish(bool quit, int mode, int res, bool god, float draw, float godZoom)
		{
			foreach (Canvas item in _hidden)
			{
				if (item != null)
				{
					item.enabled = true;
				}
			}
			_hidden.Clear();
			Crabzillas.ScriptedFire[0] = (Crabzillas.ScriptedFire[1] = false);
			PossessionHooks.ScriptedInput = Vector2.zero;
			SceneCollector.AllLit = false;
			if (_mod != null)
			{
				_s.TimeMode = mode;
				_s.Resolution = res;
				_s.GodView = god;
				_s.DrawDistance = draw;
				_s.GodViewZoom = godZoom;
				_mod.NoDive = true;
				_mod.DebugEyeSet = false;
				_mod.DebugEyeHeight = 0f;
			}
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
			Log.Info("SHOW DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator Harbor()
		{
			if (!_shore.HasValue)
			{
				Log.Info("SHOW PROBLEM no shore for the harbor");
				yield break;
			}
			yield return Run("meadow", Meadow(48));
			Vector2 away = (_shore.Value - _centre).normalized;
			Vector2 vector = new Vector2(0f - away.y, away.x);
			Vector2? at = null;
			for (float num = 5f; num < 20f; num += 1f)
			{
				if (at.HasValue)
				{
					break;
				}
				for (int i = -3; i <= 3; i++)
				{
					if (at.HasValue)
					{
						break;
					}
					Vector2 value = _shore.Value + (Vector2)(Quaternion.Euler(0f, 0f, (float)i * 10f) * away) * num;
					bool flag = true;
					for (int j = -3; j <= 3 && flag; j++)
					{
						for (int k = -5; k <= 5 && flag; k++)
						{
							if (!Sea((int)(value.x + vector.x * (float)k + away.x * (float)j), (int)(value.y + vector.y * (float)k + away.y * (float)j)))
							{
								flag = false;
							}
						}
					}
					if (flag)
					{
						at = value;
					}
				}
			}
			if (!at.HasValue)
			{
				Log.Info("SHOW PROBLEM no open water for the harbor");
				yield break;
			}
			List<Actor> boats = new List<Actor>();
			string[] array = new string[3] { "boat_fishing", "boat_transport_human", "boat_trading_human" };
			for (int l = 0; l < array.Length; l++)
			{
				Vector2 vector2 = at.Value + vector * ((float)(l - 1) * 3.4f);
				try
				{
					Actor actor = World.world.units.createNewUnit(array[l], World.world.GetTile((int)vector2.x, (int)vector2.y));
					if (actor != null && _king.kingdom != null)
					{
						actor.setKingdom(_king.kingdom);
					}
					if (actor != null)
					{
						boats.Add(actor);
					}
				}
				catch (Exception ex)
				{
					Log.Info("SHOW PROBLEM boat " + array[l] + ": " + ex.Message);
				}
			}
			Log.Info($"SHOW harbor: {boats.Count} boats at {at.Value}");
			if (boats.Count == 0)
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(1.5f);
			bool paused = Config.paused;
			HoldSummer();
			Config.paused = true;
			Vector2 beach = _shore.Value;
			for (int m = 0; m < 8; m++)
			{
				if (Dry(beach))
				{
					break;
				}
				beach -= away * 0.75f;
			}
			beach -= away * 1.5f;
			int[] array2 = new int[2] { 1, 2 };
			foreach (int num2 in array2)
			{
				Hour(num2);
				string hour = ((num2 == 1) ? "day" : "golden");
				Eye(beach, 0f, at.Value, GroundAt(beach) + 0.6f);
				yield return new WaitForSecondsRealtime(2.5f);
				yield return Shot("harbor_beach_" + hour);
				Eye(beach - away * 2f, 4f, at.Value, GroundAt(beach));
				yield return new WaitForSecondsRealtime(2f);
				yield return Shot("harbor_high_" + hour);
			}
			Config.paused = paused;
			foreach (Actor item in boats)
			{
				try
				{
					if (item.isAlive())
					{
						item.dieSimpleNone();
					}
				}
				catch
				{
				}
			}
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
		}

		private IEnumerator NightLights()
		{
			yield return Run("meadow", Meadow(48));
			yield return Run("summer", SiteSummer());
			FindSpots();
			int num = 0;
			for (float num2 = 3f; num2 <= 12f; num2 += 2.5f)
			{
				if (num >= 22)
				{
					break;
				}
				for (int i = 0; i < 12; i++)
				{
					if (num >= 22)
					{
						break;
					}
					float num3 = ((float)i + num2 % 5f) * MathF.PI * 2f / 12f;
					Vector2 vector = _centre + new Vector2(Mathf.Cos(num3), Mathf.Sin(num3)) * num2;
					WorldTile tile = World.world.GetTile((int)vector.x, (int)vector.y);
					if (!Dry(vector) || tile == null || tile.building != null)
					{
						continue;
					}
					PlacedLights.Kind k = ((num % 4 == 3) ? PlacedLights.Kind.Brazier : PlacedLights.Kind.Lamp);
					if (PlacedLights.WhyNotAt(vector, k) != null)
					{
						continue;
					}
					try
					{
						if (PlacedLights.Add(_king, k, vector, num3, null, _king.city) != null)
						{
							num++;
						}
					}
					catch
					{
					}
				}
			}
			Log.Info($"SHOW nightlights: {num} lights set down");
			Hour(3);
			bool paused = Config.paused;
			HoldSummer();
			Config.paused = true;
			yield return new WaitForSecondsRealtime(4f);
			for (int j = 0; j < 4; j++)
			{
				float f = (float)j * MathF.PI * 0.5f + 0.4f;
				Vector2 vector2 = _centre + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * 6f;
				if (Dry(vector2))
				{
					Eye(vector2, 0f, _centre - (vector2 - _centre) * 0.5f, GroundAt(_centre) + 1.4f);
					yield return new WaitForSecondsRealtime(2.5f);
					yield return Shot($"lights_street_{j}");
				}
			}
			for (int j = 0; j < Mathf.Min(2, _spots.Count); j++)
			{
				Vector2 vector3 = _spots[j];
				Eye(vector3, 1.8f, _centre + (_centre - vector3).normalized * 6f, GroundAt(_centre) + 1f);
				yield return new WaitForSecondsRealtime(2.5f);
				yield return Shot($"lights_over_{j}");
			}
			Hour(2);
			int dusk = 0;
			foreach (PlacedLights.Light item in new List<PlacedLights.Light>(PlacedLights.All))
			{
				if (dusk < 3 && item.Kind == PlacedLights.Kind.Lamp)
				{
					Vector2 vector4 = new Vector2(item.X, item.Y);
					Vector2 normalized = (vector4 - _centre).normalized;
					Vector2 vector5 = vector4 + normalized * 2.2f + new Vector2(0f - normalized.y, normalized.x) * 0.9f;
					if (Dry(vector5) && Clear(vector5, _centre, 4f))
					{
						Eye(vector5, 0f, _centre, GroundAt(_centre) + 1.2f);
						yield return new WaitForSecondsRealtime(2.5f);
						yield return Shot($"lights_dusk_{dusk++}");
					}
				}
			}
			Config.paused = paused;
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
		}

		private IEnumerator More()
		{
			yield return Run("shore and peaks", Vistas());
			yield return Run("wolf", Animal());
			yield return Run("talk", Talk());
			yield return Run("home", Home());
			yield return Run("fireball", Fireball());
			yield return Run("raid", Raid());
			yield return Run("nuke", Nuke());
		}

		private IEnumerator Back()
		{
			if (_mod.Host != _king && _king != null && _king.isAlive())
			{
				Possess.Swap(_king);
			}
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			for (float t = 0f; t < 2f; t += 0.25f)
			{
				yield return new WaitForSecondsRealtime(0.25f);
			}
			_mod.HintUntil = 0f;
		}

		private Actor SpawnAt(string id, Vector2 at)
		{
			WorldTile tile = World.world.GetTile(Mathf.Clamp((int)at.x, 1, MapBox.width - 2), Mathf.Clamp((int)at.y, 1, MapBox.height - 2));
			try
			{
				return World.world.units.spawnNewUnit(id, tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Info("SHOW PROBLEM spawn " + id + ": " + ex.Message);
				return null;
			}
		}

		private static float SeaFrom(Vector2 p)
		{
			Vector2 zero = Vector2.zero;
			for (int i = -16; i <= 16; i++)
			{
				for (int j = -16; j <= 16; j++)
				{
					if (j * j + i * i <= 256 && Sea((int)p.x + j, (int)p.y + i))
					{
						zero += new Vector2(j, i);
					}
				}
			}
			return Mathf.Atan2(zero.y, zero.x);
		}

		private IEnumerator Vistas()
		{
			Hour(2);
			if (_shore.HasValue)
			{
				Vector2 s = _shore.Value;
				float sea = SeaFrom(s);
				float[] array = new float[2] { -0.35f, 0.35f };
				foreach (float off in array)
				{
					_mod.DebugEyeSet = true;
					_mod.DebugEye = s;
					_mod.DebugEyeHeight = 1.6f;
					_mod.SetYaw(sea + off);
					_mod.SetPitchDegrees(1f);
					yield return new WaitForSecondsRealtime(2f);
					yield return Shot((off < 0f) ? "shore_a" : "shore_b");
				}
			}
			_ = _mod.TerrainForTest;
			Vector2 vector = _centre;
			float num = float.MinValue;
			for (int j = 6; j < MapBox.height - 6; j += 2)
			{
				for (int k = 6; k < MapBox.width - 6; k += 2)
				{
					float num2 = Vector2.Distance(new Vector2(k, j), _centre);
					if (!(num2 < 35f) && !(num2 > 110f))
					{
						float num3 = GroundAt(new Vector2(k, j));
						if (num3 > num)
						{
							num = num3;
							vector = new Vector2(k, j);
						}
					}
				}
			}
			Vector2 vector2 = vector + (_centre - vector).normalized * 28f;
			for (int l = 0; l < 12; l++)
			{
				if (Dry(vector2) && GroundAt(vector2) < num - 6f)
				{
					break;
				}
				vector2 = Vector2.Lerp(vector2, _centre, 0.12f);
			}
			Log.Info($"SHOW peak {vector} ({num:F1} high), eye {vector2} ({GroundAt(vector2):F1})");
			Eye(vector2, 2f, vector, num * 0.85f);
			yield return new WaitForSecondsRealtime(2.5f);
			yield return Shot("peaks_golden");
			Hour(1);
			yield return new WaitForSecondsRealtime(2f);
			yield return Shot("peaks_day");
			yield return Back();
		}

		private IEnumerator Animal()
		{
			Hour(2);
			Vector2 at = (_shore.HasValue ? Vector2.Lerp(_shore.Value, _centre, 0.25f) : (_centre + new Vector2(10f, 0f)));
			Actor wolf = SpawnAt("wolf", at);
			if (wolf == null)
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(0.5f);
			if (!Possess.Swap(wolf))
			{
				Log.Info("SHOW PROBLEM couldn't take the wolf");
				yield break;
			}
			yield return new WaitForSecondsRealtime(2.5f);
			_mod.HintUntil = 0f;
			Vector2 p = wolf.current_position;
			float num = (_shore.HasValue ? SeaFrom(p) : 0f);
			_mod.SetYaw(num - 0.3f);
			_mod.SetPitchDegrees(2f);
			yield return new WaitForSecondsRealtime(1.2f);
			yield return Shot("wolf_sea");
			_mod.SetYaw(Mathf.Atan2(_centre.y - p.y, _centre.x - p.x));
			yield return new WaitForSecondsRealtime(1.2f);
			yield return Shot("wolf_town");
			yield return Back();
			try
			{
				if (wolf.isAlive())
				{
					wolf.dieSimpleNone();
				}
			}
			catch
			{
			}
		}

		private IEnumerator Talk()
		{
			Hour(2);
			Vector2 vector = _shore ?? _centre;
			Actor npc = SpawnAt("human", vector);
			if (npc != null)
			{
				Vector2 vector2 = (_shore.HasValue ? (_centre - vector).normalized : Vector2.down);
				Vector2 vector3 = vector + vector2 * 2.4f;
				try
				{
					_king.setCurrentTilePosition(World.world.GetTile((int)vector3.x, (int)vector3.y));
				}
				catch
				{
				}
				yield return new WaitForSecondsRealtime(1f);
				Vector2 vector4 = npc.current_position - _king.current_position;
				_mod.SetYaw(Mathf.Atan2(vector4.y, vector4.x));
				_mod.SetPitchDegrees(-2f);
				bool flag = _mod.StartConversationForTest(npc);
				Log.Info($"SHOW talking to {WorldBoxMod.Describe(npc)}: {flag}");
				yield return new WaitForSecondsRealtime(2.2f);
				yield return Shot("talk_beach");
				yield return new WaitForSecondsRealtime(1.5f);
				yield return Shot("talk_beach_b");
				try
				{
					_mod.EndConversation();
				}
				catch
				{
				}
				yield return new WaitForSecondsRealtime(0.5f);
			}
		}

		private IEnumerator Home()
		{
			Building building = null;
			foreach (Building building2 in World.world.buildings)
			{
				if (building2 != null && building2.isAlive() && HouseInterior.IsEnterable(building2) && building2.asset.id.StartsWith("house_", StringComparison.Ordinal) && Vector2.Distance(new Vector2(building2.current_tile.x, building2.current_tile.y), _centre) < 25f)
				{
					building = building2;
					if (building2.asset.id.EndsWith("3", StringComparison.Ordinal) || building2.asset.id.EndsWith("4", StringComparison.Ordinal))
					{
						break;
					}
				}
			}
			if (building == null)
			{
				Log.Info("SHOW PROBLEM no house to walk into");
				yield break;
			}
			Hour(1);
			_mod.EnterHouseForTest(building);
			yield return new WaitForSecondsRealtime(1.8f);
			_mod.SetPitchDegrees(-6f);
			yield return new WaitForSecondsRealtime(0.5f);
			yield return Shot("home_day");
			_mod.SetYaw(_mod.View.Yaw + 2.2f);
			yield return new WaitForSecondsRealtime(0.8f);
			yield return Shot("home_day_b");
			Hour(3);
			yield return new WaitForSecondsRealtime(1.5f);
			yield return Shot("home_night");
			_mod.SetYaw(_mod.View.Yaw - 2.2f);
			yield return new WaitForSecondsRealtime(0.8f);
			yield return Shot("home_night_b");
			_mod.LeaveRoomForTest();
			Hour(2);
			yield return new WaitForSecondsRealtime(1f);
		}

		private IEnumerator Fireball()
		{
			Hour(2);
			Vector2 vector = _centre + new Vector2(-14f, 6f);
			for (int i = 0; i < 8; i++)
			{
				if (Dry(vector))
				{
					break;
				}
				vector = Vector2.Lerp(vector, _centre, 0.2f);
			}
			Actor mage = SpawnAt("evil_mage", vector);
			if (mage == null)
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(0.5f);
			if (!Possess.Swap(mage))
			{
				Log.Info("SHOW PROBLEM couldn't take the mage");
				yield break;
			}
			yield return new WaitForSecondsRealtime(2.5f);
			_mod.HintUntil = 0f;
			Vector2 current_position = mage.current_position;
			Vector2 dir = (_centre - current_position).normalized;
			List<Actor> prey = new List<Actor>();
			for (int j = 0; j < 3; j++)
			{
				Actor actor = SpawnAt("bandit", current_position + dir * (6f + (float)j) + new Vector2(0f - dir.y, dir.x) * (j - 1) * 1.5f);
				if (actor != null)
				{
					prey.Add(actor);
					try
					{
						mage.addAggro(actor);
						actor.addAggro(mage);
					}
					catch
					{
					}
				}
			}
			yield return new WaitForSecondsRealtime(0.3f);
			_mod.SetYaw(Mathf.Atan2(dir.y, dir.x));
			_mod.SetPitchDegrees(-5f);
			yield return new WaitForSecondsRealtime(0.6f);
			_mod.PressAbilityForTest(down: true);
			yield return new WaitForSecondsRealtime(0.5f);
			yield return Shot("fireball_aim");
			_mod.PressAbilityForTest(down: false);
			float[] array = new float[5] { 0.15f, 0.35f, 0.6f, 0.9f, 1.4f };
			foreach (float t in array)
			{
				yield return new WaitForSecondsRealtime(t);
				yield return Shot($"fireball_{t:0.00}");
			}
			yield return Back();
			foreach (Actor item in prey)
			{
				try
				{
					if (item.isAlive())
					{
						item.dieSimpleNone();
					}
				}
				catch
				{
				}
			}
			try
			{
				if (mage.isAlive())
				{
					mage.dieSimpleNone();
				}
			}
			catch
			{
			}
		}

		private IEnumerator Raid()
		{
			Hour(2);
			Vector2 vector = _centre + new Vector2(16f, -12f);
			for (int i = 0; i < 8; i++)
			{
				if (Dry(vector))
				{
					break;
				}
				vector = Vector2.Lerp(vector, _centre, 0.2f);
			}
			List<Actor> raiders = new List<Actor>();
			for (int j = 0; j < 9; j++)
			{
				Actor actor = SpawnAt("orc", vector + new Vector2((float)(j % 3) * 1.5f, (float)(j / 3) * 1.5f));
				if (actor != null)
				{
					raiders.Add(actor);
				}
			}
			List<Actor> list = new List<Actor>();
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple.asset != null && simple.asset.id == "human" && simple != _king && Vector2.Distance(simple.current_position, _centre) < 18f)
				{
					list.Add(simple);
				}
			}
			for (int k = 0; k < raiders.Count; k++)
			{
				if (list.Count > 0)
				{
					try
					{
						raiders[k].addAggro(list[k % list.Count]);
						list[k % list.Count].addAggro(raiders[k]);
					}
					catch
					{
					}
				}
			}
			Log.Info($"SHOW raid: {raiders.Count} orcs on {list.Count} villagers");
			Vector2 vector2 = Vector2.Lerp(_centre, vector, 0.55f);
			try
			{
				_king.setCurrentTilePosition(World.world.GetTile((int)vector2.x, (int)vector2.y));
			}
			catch
			{
			}
			for (int l = 0; l < 10; l++)
			{
				Vector2 zero = Vector2.zero;
				int num = 0;
				foreach (Actor item in raiders)
				{
					if (item != null && item.isAlive())
					{
						zero += item.current_position;
						num++;
					}
				}
				if (num == 0)
				{
					break;
				}
				zero /= (float)num;
				Vector2 vector3 = zero - _king.current_position;
				_mod.SetYaw(Mathf.Atan2(vector3.y, vector3.x));
				_mod.SetPitchDegrees(-3f);
				if (vector3.magnitude < 2.5f)
				{
					PossessionCombat.AttackForTest(_king);
				}
				yield return new WaitForSecondsRealtime(0.9f);
				yield return Shot($"raid_{l:00}");
			}
			foreach (Actor item2 in raiders)
			{
				try
				{
					if (item2.isAlive())
					{
						item2.dieSimpleNone();
					}
				}
				catch
				{
				}
			}
			yield return new WaitForSecondsRealtime(0.5f);
		}

		private IEnumerator Refresh()
		{
			yield return Run("summer", SiteSummer());
			yield return Run("far shore", FarShore());
			yield return Run("hours", Hours());
			yield return Run("skies", Skies());
		}

		private IEnumerator FarShore()
		{
			yield return Run("summer", SiteSummer());
			HoldSummer();
			Hour(2);
			yield return new WaitForSecondsRealtime(2.5f);
			Vec3 sunDisc = _mod.Renderer.Air.SunDisc;
			float sunYaw = Mathf.Atan2(sunDisc.Y, sunDisc.X);
			Vector2 dir = new Vector2(Mathf.Cos(sunYaw), Mathf.Sin(sunYaw));
			List<KeyValuePair<float, Vector2>> list = new List<KeyValuePair<float, Vector2>>();
			for (int i = 4; i < MapBox.height - 4; i++)
			{
				for (int j = 4; j < MapBox.width - 4; j++)
				{
					Vector2 vector = new Vector2((float)j + 0.5f, (float)i + 0.5f);
					if (Vector2.Distance(vector, _centre) > 80f || !Dry(vector) || !Sea((int)(vector.x + dir.x * 2f), (int)(vector.y + dir.y * 2f)))
					{
						continue;
					}
					float num = 0f;
					float num2 = 0f;
					for (float num3 = 2f; num3 < 300f; num3 += 1f)
					{
						Vector2 vector2 = vector + dir * num3;
						if (vector2.x < 1f || vector2.y < 1f || vector2.x >= (float)(MapBox.width - 1) || vector2.y >= (float)(MapBox.height - 1))
						{
							break;
						}
						if (Sea((int)vector2.x, (int)vector2.y))
						{
							num = num3;
							continue;
						}
						num2 = num3;
						break;
					}
					if (!(num < 30f) && !HouseNear(vector, 4f) && Clear(vector, vector + dir * 12f, 8f))
					{
						float key = Mathf.Min(num, 160f) + ((num2 > 40f) ? 100f : 0f) - Vector2.Distance(vector, _centre) * 0.5f;
						list.Add(new KeyValuePair<float, Vector2>(key, vector));
					}
				}
			}
			list.Sort((KeyValuePair<float, Vector2> a, KeyValuePair<float, Vector2> b) => b.Key.CompareTo(a.Key));
			List<Vector2> picked = new List<Vector2>();
			foreach (KeyValuePair<float, Vector2> item in list)
			{
				bool flag = false;
				foreach (Vector2 item2 in picked)
				{
					if (Vector2.Distance(item2, item.Value) < 15f)
					{
						flag = true;
					}
				}
				if (!flag)
				{
					picked.Add(item.Value);
				}
				if (picked.Count >= 3)
				{
					break;
				}
			}
			Log.Info(string.Format("SHOW far shore: sun at {0:F0} deg, {1:F0} up; {2} shore tiles face it over open sea, picked {3}", sunYaw * 57.29578f, Mathf.Asin(Mathf.Clamp(sunDisc.Z, -1f, 1f)) * 57.29578f, list.Count, string.Join(" ", picked.ConvertAll((Vector2 p) => $"{p.x:F0},{p.y:F0}"))));
			for (int k = 0; k < picked.Count; k++)
			{
				Vector2 eye = picked[k] - dir * 2.5f;
				if (!Dry(eye) || HouseNear(eye, 3f))
				{
					eye = picked[k];
				}
				float[] array = new float[3] { 0.22f, 0f, -0.22f };
				foreach (float off in array)
				{
					Outdoors();
					_mod.DebugEyeSet = true;
					_mod.DebugEye = eye;
					_mod.DebugEyeHeight = 0f;
					_mod.SetYaw(sunYaw + off);
					_mod.SetPitchDegrees(2f);
					yield return new WaitForSecondsRealtime(2.5f);
					yield return Shot(string.Format("farshore_{0}_{1}", k, (off > 0f) ? "a" : ((off < 0f) ? "b" : "sun")));
				}
			}
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
		}

		private IEnumerator Skies()
		{
			if (_spots.Count == 0)
			{
				yield break;
			}
			Vector2 eye = _spots[0];
			foreach (Vector2 spot in _spots)
			{
				if (Clear(spot, spot + Vector2.right * 24f, 14f))
				{
					eye = spot;
					break;
				}
			}
			HoldSummer();
			Hour(3);
			FrameRenderer r = _mod.Renderer;
			Outdoors();
			Eye(eye, 1.8f, _centre, GroundAt(_centre) + 1f);
			float num = Mathf.Atan2(_centre.y - eye.y, _centre.x - eye.x);
			r.ForceAurora = 1f;
			float[] array = new float[2] { 0f, num };
			foreach (float yaw in array)
			{
				_mod.SetYaw(yaw);
				_mod.SetPitchDegrees(10f);
				yield return new WaitForSecondsRealtime(2.5f);
				yield return Shot("night_aurora_" + ((yaw == 0f) ? "pole" : "town"));
			}
			r.ForceAurora = 0f;
			r.ForceComet = 1f;
			yield return null;
			yield return null;
			Vec3 d = r.CometDirection;
			_mod.SetYaw(Mathf.Atan2(d.Y, d.X));
			_mod.SetPitchDegrees(Mathf.Min(28f, Mathf.Asin(Mathf.Clamp(d.Z, -1f, 1f)) * 57.29578f * 0.8f));
			yield return new WaitForSecondsRealtime(2f);
			yield return Shot("night_comet_0");
			_mod.SetPitchDegrees(Mathf.Min(20f, Mathf.Asin(Mathf.Clamp(d.Z, -1f, 1f)) * 57.29578f * 0.55f));
			yield return new WaitForSecondsRealtime(1.5f);
			yield return Shot("night_comet_1");
			r.ForceComet = 0f;
			r.ForceShower = 1f;
			Vector2 vector = (_shore.HasValue ? (_shore.Value - eye) : (_centre - eye));
			_mod.SetYaw(Mathf.Atan2(vector.y, vector.x));
			_mod.SetPitchDegrees(12f);
			for (int i = 0; i < 3; i++)
			{
				yield return new WaitForSecondsRealtime(1.2f);
				yield return Shot($"night_shower_{i}");
			}
			r.ForceComet = (r.ForceShower = (r.ForceAurora = -1f));
			Hour(1);
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
		}

		private void Outdoors()
		{
			if (_mod.Interior.Inside)
			{
				Log.Info("SHOW the eye was in a room: out again");
				_mod.LeaveRoomForTest();
			}
		}

		private static bool HouseNear(Vector2 p, float r)
		{
			for (int i = -(int)r; i <= (int)r; i++)
			{
				for (int j = -(int)r; j <= (int)r; j++)
				{
					if (!((float)(j * j + i * i) > r * r))
					{
						Building building = World.world.GetTile((int)p.x + j, (int)p.y + i)?.building;
						if (building != null && building.asset != null && building.asset.type != "type_tree")
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		private IEnumerator Hours()
		{
			yield return Run("meadow", Meadow(48));
			FindSpots();
			_spots.RemoveAll((Vector2 q) => HouseNear(q, 3f));
			if (_spots.Count == 0)
			{
				Log.Info("SHOW PROBLEM no clear spot for the hours");
				yield break;
			}
			string[] hours = new string[4] { "", "day", "golden", "night" };
			bool paused = Config.paused;
			HoldSummer();
			Config.paused = true;
			yield return new WaitForSecondsRealtime(3f);
			for (int i = 0; i < Mathf.Min(3, _spots.Count); i++)
			{
				Vector2 vector = _spots[i];
				Outdoors();
				Eye(vector, 1.8f, _centre + (_centre - vector).normalized * 6f, GroundAt(_centre) + 1f);
				int[] array = new int[3] { 1, 2, 3 };
				foreach (int mode in array)
				{
					Outdoors();
					Hour(mode);
					yield return new WaitForSecondsRealtime(2.5f);
					yield return Shot($"hours_{i}_{hours[mode]}");
				}
			}
			Config.paused = paused;
			Hour(1);
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
		}

		private IEnumerator Site()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			bool squareOnly = false;
			try
			{
				squareOnly = File.ReadAllText(path).Contains("squareonly");
			}
			catch
			{
			}
			yield return Run("meadow", Meadow(48));
			if (squareOnly)
			{
				yield return Run("peoples", Peoples());
				yield break;
			}
			yield return Run("summer", SiteSummer());
			yield return Run("hero", Hero());
			yield return Run("dragon", DragonSite());
			yield return Run("god view", GodShots());
			yield return Run("peoples", Peoples());
		}

		private IEnumerator Meadow(int r)
		{
			TopTileType topTileType = AssetManager.top_tiles.get("grass_low");
			TopTileType topTileType2 = AssetManager.top_tiles.get("grass_high") ?? topTileType;
			if (topTileType == null)
			{
				Log.Info("SHOW PROBLEM no grass tiles");
				yield break;
			}
			int num = (int)_centre.x;
			int num2 = (int)_centre.y;
			int tiles = 0;
			int trees = 0;
			int plants = 0;
			for (int i = num2 - r; i <= num2 + r; i++)
			{
				for (int j = num - r; j <= num + r; j++)
				{
					if ((j - num) * (j - num) + (i - num2) * (i - num2) > r * r)
					{
						continue;
					}
					WorldTile tile = World.world.GetTile(j, i);
					if (tile == null || tile.main_type == null || tile.main_type.liquid || tile.main_type.lava || tile.top_type == null)
					{
						continue;
					}
					string text = tile.top_type.id ?? "";
					if (text.StartsWith("grass", StringComparison.Ordinal))
					{
						continue;
					}
					bool flag = text.EndsWith("_high", StringComparison.Ordinal);
					if (flag || text.EndsWith("_low", StringComparison.Ordinal))
					{
						try
						{
							tile.setTopTileType(flag ? topTileType2 : topTileType);
							tiles++;
						}
						catch
						{
						}
					}
				}
			}
			List<WorldTile> spots = new List<WorldTile>();
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item == null || !item.isAlive() || item.asset == null || item.current_tile == null)
				{
					continue;
				}
				WorldTile current_tile = item.current_tile;
				if ((current_tile.x - num) * (current_tile.x - num) + (current_tile.y - num2) * (current_tile.y - num2) > r * r)
				{
					continue;
				}
				string text2 = item.asset.id ?? "";
				bool flag2 = false;
				string[] otherBiomes = OtherBiomes;
				foreach (string value in otherBiomes)
				{
					if (text2.Contains(value))
					{
						flag2 = true;
						break;
					}
				}
				if (!flag2)
				{
					continue;
				}
				try
				{
					if (item.asset.type == "type_tree")
					{
						item.removeBuildingFinal();
						spots.Add(current_tile);
						trees++;
					}
					else if (!AssetStyle.IsSolid(item))
					{
						item.removeBuildingFinal();
						plants++;
					}
				}
				catch
				{
				}
			}
			yield return null;
			System.Random random = new System.Random(5);
			int num3 = 0;
			foreach (WorldTile item2 in spots)
			{
				if (item2.Type == null || item2.Type.liquid || item2.Type.block || item2.building != null)
				{
					continue;
				}
				try
				{
					if (World.world.buildings.addBuilding(MeadowTrees[random.Next(MeadowTrees.Length)], item2, pCheckForBuild: false, pSfx: false, BuildPlacingType.New) != null)
					{
						num3++;
					}
				}
				catch
				{
				}
			}
			Log.Info($"SHOW meadow: {tiles} tiles to grass, {trees} trees swapped ({num3} meadow trees planted), {plants} plants cleared");
			yield return new WaitForSecondsRealtime(4f);
		}

		private IEnumerator SiteSummer()
		{
			int num = -1;
			for (int i = 0; i < 8; i++)
			{
				if (num >= 0)
				{
					break;
				}
				if (Calendar.SeasonOf(i, out var dayIn, out var _) == Season.Summer && dayIn == 0)
				{
					num = i;
				}
			}
			if (num < 0)
			{
				Log.Info("SHOW PROBLEM this age has no summer");
				yield break;
			}
			bool paused = Config.paused;
			Config.paused = false;
			Calendar.SetDayForTest(num + 16);
			yield return new WaitForSecondsRealtime(5f);
			try
			{
				_mod.SamplerForTest.RelightAll();
			}
			catch
			{
			}
			Config.paused = paused;
			for (int j = 0; j < 10; j++)
			{
				yield return null;
			}
		}

		private static bool Clear(Vector2 from, Vector2 to, float upTo)
		{
			Vector2 vector = to - from;
			float magnitude = vector.magnitude;
			if (magnitude < 0.01f)
			{
				return true;
			}
			vector /= magnitude;
			Vector2 vector2 = new Vector2(0f - vector.y, vector.x) * 0.9f;
			for (float num = 0f; num < Mathf.Min(magnitude, upTo); num += 0.5f)
			{
				Vector2[] array = new Vector2[3]
				{
					from + vector * num,
					from + vector * num + vector2,
					from + vector * num - vector2
				};
				for (int i = 0; i < array.Length; i++)
				{
					Vector2 vector3 = array[i];
					WorldTile tile = World.world.GetTile((int)vector3.x, (int)vector3.y);
					if (tile == null || tile.Type == null)
					{
						return false;
					}
					if (tile.Type.block)
					{
						return false;
					}
					Building building = tile.building;
					if (building != null && AssetStyle.IsSolid(building) && building.asset != null && building.asset.type != "type_tree")
					{
						return false;
					}
				}
			}
			return true;
		}

		private void FindSpots()
		{
			_spots.Clear();
			Vector2 rhs = (_shore.HasValue ? (_shore.Value - _centre).normalized : Vector2.zero);
			float num = GroundAt(_centre);
			List<KeyValuePair<float, Vector2>> list = new List<KeyValuePair<float, Vector2>>();
			for (float num2 = 12f; num2 <= 26f; num2 += 2f)
			{
				for (int i = 0; i < 36; i++)
				{
					float f = (float)i * MathF.PI * 2f / 36f;
					Vector2 vector = _centre + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num2;
					if (Dry(vector) && Clear(vector, _centre, 6f))
					{
						float key = Mathf.Clamp(GroundAt(vector) - num, -2f, 6f) * 2f + Vector2.Dot((_centre - vector).normalized, rhs) * 4f - Mathf.Abs(num2 - 17f) * 0.15f;
						list.Add(new KeyValuePair<float, Vector2>(key, vector));
					}
				}
			}
			list.Sort((KeyValuePair<float, Vector2> x, KeyValuePair<float, Vector2> y) => y.Key.CompareTo(x.Key));
			foreach (KeyValuePair<float, Vector2> item in list)
			{
				bool flag = false;
				foreach (Vector2 spot in _spots)
				{
					if (Vector2.Distance(spot, item.Value) < 7f)
					{
						flag = true;
					}
				}
				if (!flag)
				{
					_spots.Add(item.Value);
				}
				if (_spots.Count >= 5)
				{
					break;
				}
			}
			int num3 = 0;
			foreach (Vector2 spot2 in _spots)
			{
				num3 += Glade(spot2, 9f);
			}
			Log.Info(string.Format("SHOW {0} clear spots, kept {1}: {2}, {3} trees felled round them", list.Count, _spots.Count, string.Join(" ", _spots.ConvertAll((Vector2 p) => $"{p.x:F0},{p.y:F0}")), num3));
		}

		private static int Glade(Vector2 at, float r)
		{
			int num = 0;
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item != null && item.isAlive() && item.asset != null && item.current_tile != null && !(item.asset.type != "type_tree") && !(Vector2.Distance(new Vector2((float)item.current_tile.x + 0.5f, (float)item.current_tile.y + 0.5f), at) > r))
				{
					try
					{
						item.removeBuildingFinal();
						num++;
					}
					catch
					{
					}
				}
			}
			return num;
		}

		private IEnumerator Hero()
		{
			FindSpots();
			for (int i = 0; i < _spots.Count; i++)
			{
				Vector2 vector = _spots[i];
				Vector2 lookAt = _centre + (_centre - vector).normalized * 6f;
				Eye(vector, 1.8f, lookAt, GroundAt(_centre) + 1f);
				int[] array = new int[3] { 2, 1, 3 };
				foreach (int mode in array)
				{
					Hour(mode);
					yield return new WaitForSecondsRealtime((i == 0 && mode == 2) ? 4f : 1.8f);
					ShowcaseTest showcaseTest = this;
					object arg = i;
					object arg2;
					switch (mode)
					{
					default:
						arg2 = "night";
						break;
					case 2:
						arg2 = "golden";
						break;
					case 1:
						arg2 = "day";
						break;
					}
					yield return showcaseTest.Shot($"hero_{arg}_{arg2}");
				}
			}
			if (_spots.Count > 0)
			{
				Hour(2);
				Vector2 vector2 = _spots[0];
				Vector2 vector3 = vector2 + (vector2 - _centre).normalized * 20f;
				if (Clear(vector2, vector3, 8f))
				{
					Eye(vector2, 1.8f, vector3, GroundAt(vector3) + 2f);
					yield return new WaitForSecondsRealtime(1.8f);
					yield return Shot("hero_meadow_golden");
				}
			}
			Hour(2);
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			yield return new WaitForSecondsRealtime(0.5f);
		}

		private IEnumerator Peoples()
		{
			if (!_mod.IsFirstPerson)
			{
				Log.Info("SHOW PROBLEM not in first person for the peoples");
				yield break;
			}
			Vector2? vector = null;
			float num = float.MaxValue;
			for (float num2 = 30f; num2 <= 50f; num2 += 4f)
			{
				for (int i = 0; i < 32; i++)
				{
					float f = (float)i * MathF.PI * 2f / 32f;
					Vector2 vector2 = _centre + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num2;
					if (vector2.x < 16f || vector2.y < 16f || vector2.x > (float)(MapBox.width - 16) || vector2.y > (float)(MapBox.height - 16))
					{
						continue;
					}
					float num3 = float.MaxValue;
					float num4 = float.MinValue;
					bool flag = true;
					for (int j = -12; j <= 10 && flag; j += 2)
					{
						for (int k = -11; k <= 11 && flag; k += 2)
						{
							Vector2 p = vector2 + new Vector2(k, j);
							WorldTile tile = World.world.GetTile((int)p.x, (int)p.y);
							if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block || tile.Type.lava)
							{
								flag = false;
								break;
							}
							if (tile.building != null && tile.building.isKingdomCiv())
							{
								flag = false;
								break;
							}
							float b = GroundAt(p);
							num3 = Mathf.Min(num3, b);
							num4 = Mathf.Max(num4, b);
						}
					}
					if (flag)
					{
						float num5 = num4 - num3 + Mathf.Abs(num2 - 36f) * 0.05f;
						if (num5 < num)
						{
							num = num5;
							vector = vector2;
						}
					}
				}
			}
			if (!vector.HasValue)
			{
				Log.Info("SHOW PROBLEM no flat meadow for the peoples");
				yield break;
			}
			int sx = (int)vector.Value.x;
			int sy = (int)vector.Value.y;
			Log.Info($"SHOW peoples' square at {sx},{sy}, land {num:F1} uneven");
			Hour(1);
			City city = _king.city;
			Outside();
			List<Building> placed = new List<Building>();
			string[] sitePeoples = SitePeoples;
			foreach (string sp in sitePeoples)
			{
				try
				{
					Models.LoadSpeciesNow(sp, out var _);
				}
				catch (Exception ex)
				{
					Log.Info("SHOW PROBLEM " + sp + " pack: " + ex.Message);
				}
				foreach (Building item in placed)
				{
					try
					{
						if (item != null && item.isAlive())
						{
							item.removeBuildingFinal();
						}
					}
					catch
					{
					}
				}
				placed.Clear();
				foreach (Building item2 in new List<Building>(World.world.buildings.getSimpleList()))
				{
					if (item2 == null || !item2.isAlive() || item2.current_tile == null)
					{
						continue;
					}
					int num6 = item2.current_tile.x - sx;
					int num7 = item2.current_tile.y - sy;
					if (num6 < -16 || num6 > 16 || num7 < -16 || num7 > 14)
					{
						continue;
					}
					try
					{
						if (!item2.isKingdomCiv())
						{
							item2.removeBuildingFinal();
						}
					}
					catch
					{
					}
				}
				yield return null;
				(string id, int x, int y)[] want = new(string, int, int)[5]
				{
					("hall_civ_" + sp + "_0", sx, sy - 5),
					("house_civ_" + sp + "_0", sx - 8, sy - 2),
					("house_civ_" + sp + "_0", sx + 8, sy - 2),
					("temple_civ_" + sp, sx - 9, sy - 10),
					("windmill_civ_" + sp + "_0", sx + 9, sy - 10)
				};
				(string, int, int)[] array = want;
				for (int m = 0; m < array.Length; m++)
				{
					(string, int, int) tuple = array[m];
					WorldTile tile2 = World.world.GetTile(tuple.Item2, tuple.Item3);
					if (tile2 == null || AssetManager.buildings.get(tuple.Item1) == null)
					{
						continue;
					}
					try
					{
						if (tile2.building != null)
						{
							tile2.building.removeBuildingFinal();
						}
						Building building = World.world.buildings.addBuilding(tuple.Item1, tile2, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
						if (building != null)
						{
							building.completeConstruction();
							if (city != null)
							{
								building.setKingdom(city.kingdom);
								city.listBuilding(building);
							}
							placed.Add(building);
						}
					}
					catch (Exception ex2)
					{
						Log.Info("SHOW PROBLEM " + tuple.Item1 + ": " + ex2.Message);
					}
				}
				Vector2 at = new Vector2((float)sx + 0.5f, (float)sy + 10.5f);
				Outside();
				Eye(at, 2.6f, new Vector2((float)sx + 0.5f, (float)sy - 4f), GroundAt(new Vector2(sx, sy - 4)) + 1.5f);
				yield return new WaitForSecondsRealtime(3f);
				if (_mod.Interior.Inside)
				{
					Outside();
					yield return new WaitForSecondsRealtime(1f);
				}
				Log.Info(string.Format("SHOW peoples {0}: {1} of {2} placed ({3})", sp, placed.Count, want.Length, string.Join(", ", placed.ConvertAll((Building b2) => Models.BuildingFamily(b2)))));
				yield return Shot("people_" + sp);
			}
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
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			void Outside()
			{
				try
				{
					_king.setCurrentTilePosition(World.world.GetTile(sx, sy + 14));
					_king.makeWait(30f);
				}
				catch
				{
				}
				if (_mod.Interior.Inside)
				{
					_mod.LeaveRoomForTest();
				}
			}
		}

		private IEnumerator DragonSite()
		{
			Hour(2);
			Vector2 vector = ((_spots.Count > 0) ? _spots[0] : _centre);
			Vector2 vector2 = _centre + (_centre - vector).normalized * 34f + new Vector2(6f, 0f);
			WorldTile tile = World.world.GetTile(Mathf.Clamp((int)vector2.x, 2, MapBox.width - 3), Mathf.Clamp((int)vector2.y, 2, MapBox.height - 3));
			try
			{
				_dragon = World.world.units.spawnNewUnit("dragon", tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Info("SHOW PROBLEM dragon: " + ex.Message);
			}
			if (_dragon == null)
			{
				yield break;
			}
			Log.Info($"SHOW dragon at {_dragon.current_position}, jointed model {Dragons.Ready}, watched from {vector}");
			yield return Watch(14f, 1f, "site_dragon_fly");
			Dragon dragon = null;
			try
			{
				dragon = _dragon.getActorComponent<Dragon>();
			}
			catch
			{
			}
			if (dragon != null)
			{
				int num = 0;
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple.isAlive() && simple != _king && simple.asset != null && simple.asset.id == "human" && Vector2.Distance(simple.current_position, _centre) < 12f && num < 4)
					{
						dragon.aggroTargets.Add(simple);
						num++;
					}
				}
				try
				{
					_dragon.setFlip(_dragon.current_position.x > _centre.x);
					_dragon.setTask("dragon_slide");
				}
				catch (Exception ex2)
				{
					Log.Info("SHOW PROBLEM dragon task: " + ex2.Message);
				}
				Log.Info($"SHOW dragon set on {num} villagers");
			}
			yield return Watch(20f, 0.8f, "site_dragon_fire");
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
		}

		private IEnumerator Watch(float seconds, float every, string name)
		{
			float next = 0.8f;
			int k = 0;
			Vector2 eye = ((_spots.Count > 0) ? _spots[0] : _centre);
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				if (_dragon == null)
				{
					break;
				}
				if (!_dragon.isAlive())
				{
					break;
				}
				Vector2 vector = _dragon.current_position;
				float lookZ = GroundAt(vector) + 4f;
				if (Dragons.Probe(_dragon, out var pos, out var _, out var _, out var _, out var _, out var _))
				{
					vector = new Vector2(pos.X, pos.Y);
					lookZ = pos.Z;
				}
				float num = Vector2.Distance(vector, eye);
				if (num < 16f || num > 70f || !Clear(eye, vector, 6f))
				{
					Vector2 vector2 = eye;
					float num2 = float.MinValue;
					foreach (Vector2 spot in _spots)
					{
						float num3 = Vector2.Distance(spot, vector);
						if (Clear(spot, vector, 6f))
						{
							float num4 = 0f - Mathf.Abs(num3 - 32f);
							if (num4 > num2)
							{
								num2 = num4;
								vector2 = spot;
							}
						}
					}
					eye = vector2;
				}
				Eye(eye, 2.2f, vector, lookZ);
				if (t >= next)
				{
					next += every;
					yield return Shot($"{name}_{k++:00}");
				}
				yield return null;
			}
		}

		private void HoldSummer()
		{
			for (int i = 0; i < 8; i++)
			{
				if (Calendar.SeasonOf(i, out var dayIn, out var _) == Season.Summer && dayIn == 0)
				{
					Calendar.SetDayForTest(i + 16);
					break;
				}
			}
			try
			{
				_mod.TerrainForTest?.Snow?.Clear();
			}
			catch
			{
			}
			try
			{
				_mod.SamplerForTest.RelightAll();
			}
			catch
			{
			}
		}

		private IEnumerator Styles()
		{
			yield return Run("meadow", Meadow(48));
			yield return Run("summer", SiteSummer());
			FindSpots();
			if (_spots.Count == 0)
			{
				Log.Info("SHOW PROBLEM no clear spot for the styles");
				yield break;
			}
			int look = _s.GpuLook;
			int dark = _s.DarkFantasy;
			int palette = _s.Palette;
			int res = _s.Resolution;
			float draw = _s.DrawDistance;
			Hour(1);
			bool paused = Config.paused;
			HoldSummer();
			Config.paused = true;
			yield return new WaitForSecondsRealtime(3f);
			for (int i = 0; i < Mathf.Min(2, _spots.Count); i++)
			{
				Vector2 vector = _spots[i];
				Eye(vector, 1.8f, _centre + (_centre - vector).normalized * 6f, GroundAt(_centre) + 1f);
				string[] array = new string[5] { "painterly", "classic", "dark", "clean", "low" };
				foreach (string name in array)
				{
					_s.GpuLook = ((!(name == "classic")) ? 1 : 0);
					_s.DarkFantasy = ((name == "dark") ? 1 : 0);
					_s.Palette = ((!(name == "clean")) ? palette : 0);
					if (name == "low")
					{
						_s.Resolution = 1;
						SettingsMenu.SetQuality(_s, 0);
					}
					else
					{
						_s.Resolution = res;
						SettingsMenu.SetQuality(_s, 3);
					}
					_mod.ForceRelight();
					yield return new WaitForSecondsRealtime(2.5f);
					yield return Shot($"style_{i}_{name}");
				}
			}
			Config.paused = paused;
			_s.GpuLook = look;
			_s.DarkFantasy = dark;
			_s.Palette = palette;
			_s.Resolution = res;
			_s.DrawDistance = draw;
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
		}
	}
}
