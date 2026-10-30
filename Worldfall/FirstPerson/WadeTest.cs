using System;
using System.Collections;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class WadeTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private Actor _king;

		private void Check(bool ok, string what, string detail = null)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info((ok ? "WADE PASS " : "WADE FAIL ") + what + ((detail != null) ? (" (" + detail + ")") : ""));
		}

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
			foreach (WorldTile item in KingTest.GoodSites())
			{
				_king = KingTest.FoundKingdom(item, "human", 3, 0);
				if (_king != null)
				{
					break;
				}
			}
			if (_king == null)
			{
				Log.Error("WADE could not found a kingdom");
				Finish(quit);
				yield break;
			}
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("WADE first person did not start");
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
			Settings s = _mod.Settings;
			int savedMode = s.TimeMode;
			s.TimeMode = 1;
			IEnumerator run = Run();
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
					goto IL_0267;
				}
				catch (Exception ex)
				{
					Log.Error("WADE failed: " + ex);
				}
				break;
				IL_0267:
				yield return current2;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			s.TimeMode = savedMode;
			Finish(quit);
		}

		private void Finish(bool quit)
		{
			Log.Info($"WADE {_pass} passed, {_fail} failed");
			Log.Info("WADE DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator Run()
		{
			TerrainCache land = _mod.TerrainForTest;
			int w = land.Width;
			Check(land.HasSeabed, "the sea's floor is laid");
			if (!land.HasSeabed)
			{
				yield break;
			}
			if (!Shelf(land, out var shallow, out var deep, out var yaw))
			{
				Check(ok: false, "a beach with shallows and deeper water found");
				yield break;
			}
			Log.Info($"WADE shallows at {shallow % w},{shallow / w} ({land.DepthOf(shallow):F2} deep), deep water at {deep % w},{deep / w} ({land.DepthOf(deep):F2} deep)");
			WorldTile beach = World.world.GetTile(shallow % w - Mathf.RoundToInt(Mathf.Cos(yaw) * 3f), shallow / w - Mathf.RoundToInt(Mathf.Sin(yaw) * 3f));
			Put(beach);
			yield return new WaitForSecondsRealtime(1.5f);
			Vector3 eyeForTest = _mod.EyeForTest;
			float eyeOnLand = eyeForTest.z - land.SampleHeight(eyeForTest.x, eyeForTest.y);
			float paceOnLand = PossessionHooks.SpeedForTest(_king);
			Put(World.world.GetTile(shallow % w, shallow / w));
			_mod.SetYaw(yaw);
			_mod.SetPitchDegrees(-30f);
			yield return new WaitForSecondsRealtime(2f);
			Vector3 eyeForTest2 = _mod.EyeForTest;
			float num = land.FloorHeight(eyeForTest2.x, eyeForTest2.y);
			float num2 = land.LiquidSurface(eyeForTest2.x, eyeForTest2.y);
			Check(!_mod.Swimming && Wading.YouWade, "in the shallows you wade, not swim", $"swimming {_mod.Swimming}, wading {Wading.YouWade}, {num2 - num:F2} deep");
			Check(Mathf.Abs(eyeForTest2.z - num - eyeOnLand) < 0.12f, "your eye stands as high over the floor as over dry land", $"{eyeForTest2.z - num:F2} over the floor, {eyeOnLand:F2} on land");
			float num3 = PossessionHooks.SpeedForTest(_king);
			Check(num3 < paceOnLand * 0.9f && num3 > paceOnLand * 0.5f, "wading slows you", $"{num3:F2} against {paceOnLand:F2} on land");
			float swim;
			float crawl;
			MeshInstance mesh;
			if (_mod.SceneForTest.OwnBody > 0)
			{
				float wade;
				bool flag = People.WaterForTest(_king, out swim, out crawl, out wade, out mesh);
				Check(flag && wade > 0.1f, "your own body wades, knees high", flag ? $"wading {wade:F2}" : "your body not drawn in 3D");
			}
			int stamina0 = _king.getStamina();
			float health0 = _king.getHealth();
			yield return new WaitForSecondsRealtime(5f);
			Check(_king.getStamina() >= stamina0 && (float)_king.getHealth() >= health0 && !((BaseSimObject)_king).hasStatus("drowning"), "standing in the shallows costs no stamina and nobody drowns", $"stamina {stamina0} -> {_king.getStamina()}, health {health0:F0} -> {_king.getHealth():F0}");
			Check(!SceneCollector.IsSwimming(_king), "you can fight there (not swimming to the mod)");
			_mod.SaveFrame("wade_you.png");
			Put(World.world.GetTile(deep % w, deep / w));
			yield return new WaitForSecondsRealtime(2f);
			Check(_mod.Swimming && !Wading.YouWade, "out of your depth you swim", $"swimming {_mod.Swimming}, {land.DepthOf(deep):F2} deep");
			Put(beach);
			Actor wader = null;
			Actor swimmer = null;
			try
			{
				wader = World.world.units.spawnNewUnit("human", World.world.GetTile(shallow % w, shallow / w), pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
			}
			catch
			{
			}
			try
			{
				swimmer = World.world.units.spawnNewUnit("human", World.world.GetTile(deep % w, deep / w), pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
			}
			catch
			{
			}
			Check(wader != null && swimmer != null, "a wader and a swimmer put in the water");
			WorldTile far;
			bool swimmerWet;
			bool swam;
			if (wader != null && swimmer != null)
			{
				_mod.SetYaw(yaw);
				_mod.SetPitchDegrees(-12f);
				float wh = wader.getHealth();
				swam = false;
				swimmerWet = false;
				float swimDepth = float.NaN;
				Vector2 sp = default(Vector2);
				int i;
				for (i = 0; i < 40; i++)
				{
					Hold(wader);
					Hold(swimmer);
					if (i == 10)
					{
						swam = SceneCollector.IsSwimming(swimmer);
						swimmerWet = swimmer.isAffectedByLiquid();
						sp = swimmer.current_position;
						swimDepth = Wading.DepthAt(land, sp.x, sp.y);
					}
					yield return new WaitForSecondsRealtime(0.2f);
				}
				Check(!SceneCollector.IsSwimming(wader), "someone in the shallows stands there", $"{land.DepthOf(shallow):F2} deep");
				Check(!wader.isAffectedByLiquid() && !wader.isInWaterAndCantAttack(), "to the game too they are out of the water (drawn walking, in fights)", $"in the water {wader.isAffectedByLiquid()}, cleared {WadeChecks.ClearedForTest} times");
				Check(swimmerWet, "the swimmer is in the water to the game", $"{swimDepth:F2} deep");
				Check(swam, "someone in deep water still swims", $"at {sp.x:F1},{sp.y:F1}, {swimDepth:F2} deep, in the water to the game {swimmerWet}");
				Check(wader.isAlive() && (float)wader.getHealth() >= wh && !((BaseSimObject)wader).hasStatus("drowning"), "the wader doesn't drown", $"health {wh:F0} -> {wader.getHealth():F0}");
				for (int j = 0; j < 20; j++)
				{
					Hold(swimmer);
					Hold(wader);
					yield return null;
				}
				float swim2;
				float crawl2;
				MeshInstance mesh2;
				bool flag2 = People.WaterForTest(swimmer, out swim2, out crawl2, out crawl, out mesh2);
				Vector2 spos = swimmer.current_position;
				float num4 = land.WaterTop(spos.x, spos.y);
				float num5 = (flag2 ? Upright(mesh2) : 0f);
				float num6 = (flag2 ? (mesh2.Position.Z + mesh2.Basis.C2.Z * mesh2.Model.Max.Z) : float.NaN);
				Check(flag2 && swim2 > 0.9f && crawl2 < 0.2f && num5 > 0.9f && num6 > num4 && mesh2.Position.Z < num4 - 0.25f, "a swimmer still treads water: upright, the head out", flag2 ? $"swim {swim2:F2}, crawl {crawl2:F2}, upright {num5:F2}, feet {mesh2.Position.Z - num4:F2} and head {num6 - num4:F2} from the surface" : "not drawn in 3D");
				i = _mod.Settings.Resolution;
				_mod.Settings.Resolution = 3;
				CloseUp(swimmer);
				for (int j = 0; j < 12; j++)
				{
					Hold(swimmer);
					Hold(wader);
					Look(swimmer);
					yield return null;
				}
				_mod.SaveFrame("wade_swim_tread.png");
				_mod.DebugEyeSet = false;
				_mod.DebugEyeHeight = 0f;
				int num7 = (int)spos.x;
				int num8 = (int)spos.y;
				far = null;
				for (int k = 6; k <= 12; k += 2)
				{
					if (far != null)
					{
						break;
					}
					Vector2Int[] array = new Vector2Int[4]
					{
						new Vector2Int(k, 0),
						new Vector2Int(-k, 0),
						new Vector2Int(0, k),
						new Vector2Int(0, -k)
					};
					for (int l = 0; l < array.Length; l++)
					{
						Vector2Int vector2Int = array[l];
						WorldTile tile = World.world.GetTile(num7 + vector2Int.x, num8 + vector2Int.y);
						if (tile != null && tile.Type != null && tile.Type.liquid && !tile.Type.lava && Wading.DepthAt(land, (float)tile.x + 0.5f, (float)tile.y + 0.5f) > 1.5f)
						{
							far = tile;
							break;
						}
					}
				}
				float most = 0f;
				float flat = 1f;
				if (far != null)
				{
					for (int j = 0; j < 50; j++)
					{
						Hold(wader);
						if (j % 12 == 0)
						{
							try
							{
								swimmer.goTo(far, pPathOnWater: true);
							}
							catch
							{
							}
						}
						yield return new WaitForSecondsRealtime(0.05f);
						if (People.WaterForTest(swimmer, out crawl, out var crawl3, out swim, out var mesh3))
						{
							most = Mathf.Max(most, crawl3);
							flat = Mathf.Min(flat, Upright(mesh3));
							if (j == 43)
							{
								Log.Info($"WADE crawl: the body reaches {Highest(mesh3) - land.WaterTop(swimmer.current_position.x, swimmer.current_position.y):F2} over the surface, its feet {mesh3.Position.Z - land.WaterTop(swimmer.current_position.x, swimmer.current_position.y):F2}");
							}
						}
						if (j >= 36 && j < 44)
						{
							CloseUp(swimmer);
							Look(swimmer);
						}
						if (j == 43)
						{
							_mod.SaveFrame("wade_swim_crawl.png");
						}
					}
				}
				_mod.DebugEyeSet = false;
				_mod.DebugEyeHeight = 0f;
				_mod.Settings.Resolution = i;
				Check(far != null && most > 0.7f && flat < 0.5f, "swimming off, a swimmer tips forward into a crawl", (far != null) ? $"crawl {most:F2}, upright at the least {flat:F2}" : "no deep water to swim to");
				_mod.SetYaw(yaw);
				_mod.SetPitchDegrees(-12f);
				_mod.SaveFrame("wade_other.png");
				if (EdgePoints(land, shallow, yaw, out var inside, out var past))
				{
					WorldTile tile2 = World.world.GetTile((int)inside.x, (int)inside.y);
					WorldTile tile3 = World.world.GetTile((int)past.x, (int)past.y);
					bool flag3 = Wading.Stands(wader, inside, tile2);
					bool flag4 = Wading.Stands(wader, past, tile3);
					bool flag5 = Wading.Stands(swimmer, past, tile3);
					Check(flag3 && flag4 && !flag5, "at the edge of their depth people don't flicker between wading and swimming", $"{Wading.DepthAt(land, inside.x, inside.y):F2} deep stands {flag3}, then {Wading.DepthAt(land, past.x, past.y):F2} deep {flag4}; someone fresh there {flag5}");
				}
				else
				{
					Log.Info("WADE no edge of the depth found along the shelf: skipped");
				}
				int num9 = KneeDeep(land, shallow, yaw);
				Actor knee = null;
				try
				{
					knee = World.world.units.spawnNewUnit("human", World.world.GetTile(num9 % w, num9 / w), pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: false, pAdultAge: true);
				}
				catch
				{
				}
				Check(knee != null, "someone put up to the knee in the water", $"{land.DepthOf(num9):F2} deep");
				if (knee != null)
				{
					for (int j = 0; j < 30; j++)
					{
						Hold(knee);
						Hold(wader);
						Hold(swimmer);
						yield return null;
					}
					float wade2;
					bool flag6 = People.WaterForTest(knee, out swim, out crawl, out wade2, out mesh);
					Check(flag6 && wade2 > 0.2f, "up to the knee, people wade: knees high and arms out", flag6 ? $"wading {wade2:F2}" : "not drawn in 3D");
					i = _mod.Settings.Resolution;
					_mod.Settings.Resolution = 3;
					CloseUp(knee);
					for (int j = 0; j < 12; j++)
					{
						Hold(knee);
						Hold(wader);
						Hold(swimmer);
						Look(knee);
						yield return null;
					}
					_mod.SaveFrame("wade_knee_close.png");
					_mod.DebugEyeSet = false;
					_mod.DebugEyeHeight = 0f;
					_mod.Settings.Resolution = i;
					float num10 = 0f;
					for (int m = 0; m < _mod.Renderer.WakeCount; m++)
					{
						num10 = Mathf.Max(num10, _mod.Renderer.Wakes[m * 4 + 3]);
					}
					Check(num10 > 0.05f, "a collar of foam where the water meets them", $"radius {num10:F2} of {_mod.Renderer.WakeCount} in the water near");
				}
				int rings = 0;
				int noted = 0;
				int frames = 0;
				for (i = 0; i < 30; i++)
				{
					if (rings != 0)
					{
						break;
					}
					Hold(wader);
					yield return null;
					frames++;
					rings = Mathf.Max(rings, _mod.Renderer.WakeCount);
					noted = Mathf.Max(noted, Wading.NotedForTest);
				}
				Check(rings > 0, "rings spread round a wader's legs", $"{rings} drawn, {noted} noted in {frames} frames, at {wader.current_position.x:F1},{wader.current_position.y:F1}");
				try
				{
					wader.goTo(beach);
				}
				catch (Exception ex)
				{
					Log.Info("WADE goTo: " + ex.Message);
				}
				bool flag7 = false;
				for (i = 0; i < 60; i++)
				{
					if (flag7)
					{
						break;
					}
					yield return new WaitForSecondsRealtime(0.25f);
					WorldTile current_tile = wader.current_tile;
					flag7 = current_tile != null && current_tile.Type != null && !current_tile.Type.liquid;
					if (i % 12 == 11 && !flag7)
					{
						try
						{
							wader.goTo(beach);
						}
						catch
						{
						}
					}
				}
				Vector2 current_position = wader.current_position;
				Check(flag7, "a wader sent ashore walks out of the shallows", $"at {current_position.x:F1},{current_position.y:F1}, {Wading.DepthAt(land, current_position.x, current_position.y):F2} deep");
				try
				{
					wader.dieSimpleNone();
				}
				catch
				{
				}
				try
				{
					swimmer.dieSimpleNone();
				}
				catch
				{
				}
			}
			WorldAgeAsset age = null;
			bool thawing = false;
			try
			{
				age = World.world_era;
				thawing = age.global_unfreeze_world;
				age.global_unfreeze_world = false;
			}
			catch
			{
			}
			bool ok = false;
			try
			{
				ok = !AssetManager.top_tiles.get("ice").damaged_when_walked;
			}
			catch
			{
			}
			Check(ok, "the game's ice is set to hold under feet (WadeChecks)");
			far = World.world.GetTile(shallow % w, shallow / w);
			for (int n = -2; n <= 2; n++)
			{
				for (int num11 = -2; num11 <= 2; num11++)
				{
					try
					{
						World.world.GetTile(shallow % w + num11, shallow / w + n)?.freeze();
					}
					catch (Exception ex2)
					{
						Log.Info("WADE ice: " + ex2.Message);
					}
				}
			}
			for (int frames = 0; frames < 10; frames++)
			{
				if (Wading.Frozen(far))
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.1f);
			}
			Check(Wading.Frozen(far), "the shallows freeze to ice", "tile type " + far.Type?.id);
			Put(far);
			for (int frames = 0; frames < 12; frames++)
			{
				yield return new WaitForSecondsRealtime(0.1f);
				try
				{
					if (!Wading.Frozen(far))
					{
						far.freeze();
					}
				}
				catch
				{
				}
			}
			swimmerWet = !Wading.Frozen(far);
			yield return new WaitForSecondsRealtime(0.5f);
			Vector3 eyeForTest3 = _mod.EyeForTest;
			bool flag8 = Wading.Frozen(far);
			Check(flag8 && !_mod.Swimming, "ice holds you up and doesn't break under you", $"still ice {flag8}, thawed meanwhile {swimmerWet}, swimming {_mod.Swimming}");
			Check(Mathf.Abs(eyeForTest3.z - land.LiquidSurface(eyeForTest3.x, eyeForTest3.y) - eyeOnLand) < 0.15f, "on the ice you stand at the water's level", $"{eyeForTest3.z - land.LiquidSurface(eyeForTest3.x, eyeForTest3.y):F2} over it");
			int num12 = shallow / w * w + shallow % w;
			Check((land.Flags[num12] & 0x80) != 0 && land.WaveAt((float)(shallow % w) + 0.5f, (float)(shallow / w) + 0.5f) == 0f, "the ice is drawn as ice, still", $"colour {land.Colors[num12]:X8}, the game's {land.BaseColors[num12]:X8}, flags {land.Flags[num12]}");
			Put(beach);
			_mod.SetYaw(yaw);
			_mod.SetPitchDegrees(-20f);
			swam = _mod.Settings.GpuRender;
			for (int frames = 0; frames < 2; frames++)
			{
				_mod.Settings.GpuRender = frames == 1;
				for (int num13 = -2; num13 <= 2; num13++)
				{
					for (int num14 = -2; num14 <= 2; num14++)
					{
						try
						{
							WorldTile tile4 = World.world.GetTile(shallow % w + num14, shallow / w + num13);
							if (tile4 != null && !Wading.Frozen(tile4))
							{
								tile4.freeze();
							}
						}
						catch
						{
						}
					}
				}
				yield return new WaitForSecondsRealtime(1f);
				_mod.SaveFrame("wade_ice_" + ((frames == 1) ? "gpu" : "cpu") + ".png");
				int width;
				int height;
				Color32[] array2 = Grab(frames == 1, out width, out height);
				if (array2 != null && width > 0)
				{
					Color32 color = array2[height / 2 * width + width / 2];
					Check(color.r + color.g + color.b > 420, "the ice is drawn bright (" + ((frames == 1) ? "GPU" : "CPU") + ")", $"middle {color.r},{color.g},{color.b}");
				}
			}
			_mod.Settings.GpuRender = swam;
			Put(beach);
			if (age != null)
			{
				age.global_unfreeze_world = thawing;
			}
		}

		private void Put(WorldTile t)
		{
			try
			{
				_king.cancelAllBeh();
				_king.setCurrentTilePosition(t);
			}
			catch (Exception ex)
			{
				Log.Info("WADE put: " + ex.Message);
			}
		}

		private static float Highest(MeshInstance m)
		{
			Vec3 min = m.Model.Min;
			Vec3 max = m.Model.Max;
			Mat3 basis = m.Basis;
			return m.Position.Z + High(basis.C0.Z, min.X, max.X) + High(basis.C1.Z, min.Y, max.Y) + High(basis.C2.Z, min.Z, max.Z);
			float High(float k, float l, float h)
			{
				if (!(k >= 0f))
				{
					return k * l;
				}
				return k * h;
			}
		}

		private static float Upright(MeshInstance m)
		{
			Vec3 c = m.Basis.C2;
			float num = Mathf.Sqrt(c.X * c.X + c.Y * c.Y + c.Z * c.Z);
			if (!(num > 1E-06f))
			{
				return 0f;
			}
			return c.Z / num;
		}

		private void CloseUp(Actor who)
		{
			Vector3 eyeForTest = _mod.EyeForTest;
			Vector2 current_position = who.current_position;
			Vector2 vector = new Vector2(eyeForTest.x - current_position.x, eyeForTest.y - current_position.y);
			if (vector.sqrMagnitude < 0.0001f)
			{
				vector = Vector2.right;
			}
			_mod.DebugEye = current_position + vector.normalized * 2.5f;
			_mod.DebugEyeHeight = 1f;
			_mod.DebugEyeSet = true;
		}

		private void Look(Actor who)
		{
			Vector3 eyeForTest = _mod.EyeForTest;
			Vector2 current_position = who.current_position;
			float num = current_position.x - eyeForTest.x;
			float num2 = current_position.y - eyeForTest.y;
			float b = Mathf.Sqrt(num * num + num2 * num2);
			_mod.SetYaw(Mathf.Atan2(num2, num));
			_mod.SetPitchDegrees((0f - Mathf.Atan2(eyeForTest.z - 0f, Mathf.Max(0.5f, b))) * 57.29578f);
		}

		private Color32[] Grab(bool gpu, out int width, out int height)
		{
			width = (height = 0);
			if (!gpu)
			{
				if (_mod.Frame == null)
				{
					return null;
				}
				width = _mod.Frame.width;
				height = _mod.Frame.height;
				return _mod.Frame.GetPixels32();
			}
			Texture2D texture2D = _mod.GpuRendererForTest?.ReadOutput();
			if (texture2D == null)
			{
				return null;
			}
			width = texture2D.width;
			height = texture2D.height;
			Color32[] pixels = texture2D.GetPixels32();
			UnityEngine.Object.Destroy(texture2D);
			return pixels;
		}

		private static int Differ(Color32[] a, Color32[] b, int w, int h)
		{
			if (a == null || b == null || a.Length != b.Length || a.Length != w * h)
			{
				return -1;
			}
			int num = 0;
			for (int i = h * 3 / 8; i < h * 5 / 8; i++)
			{
				for (int j = w * 2 / 5; j < w * 3 / 5; j++)
				{
					int num2 = i * w + j;
					if (Math.Abs(a[num2].r - b[num2].r) > 40 || Math.Abs(a[num2].g - b[num2].g) > 40 || Math.Abs(a[num2].b - b[num2].b) > 40)
					{
						num++;
					}
				}
			}
			return num;
		}

		private static int Kept(Color32[] on1, Color32[] on2, Color32[] off, int w, int h)
		{
			if (on1 == null || on2 == null || off == null || on1.Length != w * h || on2.Length != on1.Length || off.Length != on1.Length)
			{
				return -1;
			}
			int num = 0;
			for (int i = h * 3 / 8; i < h * 5 / 8; i++)
			{
				for (int j = w * 2 / 5; j < w * 3 / 5; j++)
				{
					int num2 = i * w + j;
					if (Plain(on1[num2], off[num2]) && !Plain(on1[num2], on2[num2]))
					{
						num++;
					}
				}
			}
			return num;
		}

		private static bool Plain(Color32 a, Color32 b)
		{
			if (Math.Abs(a.r - b.r) <= 40 && Math.Abs(a.g - b.g) <= 40)
			{
				return Math.Abs(a.b - b.b) > 40;
			}
			return true;
		}

		private static bool EdgePoints(TerrainCache land, int shallow, float yaw, out Vector2 inside, out Vector2 past)
		{
			inside = (past = default(Vector2));
			bool flag = false;
			int width = land.Width;
			Vector2 vector = new Vector2((float)(shallow % width) + 0.5f, (float)(shallow / width) + 0.5f);
			Vector2 vector2 = new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw));
			for (float num = 0f; num < 10f; num += 0.05f)
			{
				Vector2 vector3 = vector + vector2 * num;
				float num2 = Wading.DepthAt(land, vector3.x, vector3.y);
				if (!float.IsNaN(num2))
				{
					if (!flag && num2 > 0.54f && num2 < 0.58f)
					{
						inside = vector3;
						flag = true;
					}
					else if (flag && num2 > 0.62f && num2 < 0.66f)
					{
						past = vector3;
						return true;
					}
				}
			}
			return false;
		}

		private static int KneeDeep(TerrainCache land, int shallow, float yaw)
		{
			int width = land.Width;
			float num = (float)(shallow % width) + 0.5f;
			float num2 = (float)(shallow / width) + 0.5f;
			for (int i = 1; i <= 8; i++)
			{
				int num3 = (int)(num + Mathf.Cos(yaw) * (float)i);
				int num4 = (int)(num2 + Mathf.Sin(yaw) * (float)i);
				if (num3 < 0 || num4 < 0 || num3 >= width || num4 >= land.Height)
				{
					break;
				}
				int num5 = num4 * width + num3;
				float num6 = land.DepthOf(num5);
				if (num6 >= 0.3f && num6 <= 0.48f && (land.Flags[num5] & 0x81) == 1)
				{
					return num5;
				}
			}
			return shallow;
		}

		private static void Hold(Actor a)
		{
			try
			{
				if (a != null && a.isAlive())
				{
					a.stopMovement();
					a.cancelAllBeh();
				}
			}
			catch
			{
			}
		}

		private static bool Shelf(TerrainCache land, out int shallow, out int deep, out float yaw)
		{
			shallow = (deep = -1);
			yaw = 0f;
			int width = land.Width;
			int height = land.Height;
			for (int i = 16; i < height - 16; i += 2)
			{
				for (int j = 16; j < width - 16; j += 2)
				{
					int num = i * width + j;
					if ((land.Flags[num] & 0xB) != 0)
					{
						continue;
					}
					for (int k = 0; k < 8; k++)
					{
						float num2 = (float)k * MathF.PI / 4f;
						int num3 = Mathf.RoundToInt((float)j + Mathf.Cos(num2) * 2f);
						int num4 = Mathf.RoundToInt((float)i + Mathf.Sin(num2) * 2f) * width + num3;
						float num5 = land.DepthOf(num4);
						if ((land.Flags[num4] & 1) == 0 || num5 < 0.2f || num5 > 0.45f)
						{
							continue;
						}
						for (int l = 4; l <= 12; l++)
						{
							int num6 = Mathf.RoundToInt((float)j + Mathf.Cos(num2) * (float)l);
							int num7 = Mathf.RoundToInt((float)i + Mathf.Sin(num2) * (float)l) * width + num6;
							if ((land.Flags[num7] & 1) == 0)
							{
								break;
							}
							if (land.DepthOf(num7) > 2f)
							{
								shallow = num4;
								deep = num7;
								yaw = num2;
								return true;
							}
						}
					}
				}
			}
			return false;
		}
	}
}
