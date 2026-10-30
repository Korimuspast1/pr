using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class SwellTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private Actor _ship;

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
			Log.Info((ok ? "SWELL PASS " : "SWELL FAIL ") + what + ((detail != null) ? (" (" + detail + ")") : ""));
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
			Actor actor = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				actor = KingTest.FoundKingdom(item, "human", 3, 0);
				if (actor != null)
				{
					break;
				}
			}
			if (actor == null)
			{
				Log.Error("SWELL could not found a kingdom");
				Finish(quit);
				yield break;
			}
			ControllableUnit.setControllableCreature(actor);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("SWELL first person did not start");
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
			int savedRes = s.Resolution;
			int savedLook = s.GpuLook;
			bool savedGpu = s.GpuRender;
			s.TimeMode = 1;
			s.Resolution = 1;
			s.GpuLook = 0;
			_mod.ForceRelight();
			IEnumerator run = Run();
			while (true)
			{
				object current;
				try
				{
					if (!run.MoveNext())
					{
						break;
					}
					current = run.Current;
					goto IL_02aa;
				}
				catch (Exception ex)
				{
					Log.Error("SWELL failed: " + ex);
				}
				break;
				IL_02aa:
				yield return current;
			}
			_mod.SeaStormForTest = -1f;
			_mod.HeldTime = -1f;
			_mod.ForceSwim = false;
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			Config.paused = false;
			s.TimeMode = savedMode;
			s.Resolution = savedRes;
			s.GpuLook = savedLook;
			s.GpuRender = savedGpu;
			Finish(quit);
		}

		private void Finish(bool quit)
		{
			Log.Info($"SWELL {_pass} passed, {_fail} failed");
			Log.Info("SWELL DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private void Update()
		{
			if (_ship == null)
			{
				return;
			}
			try
			{
				if (_ship.isAlive())
				{
					_ship.stopMovement();
					_ship.cancelAllBeh();
					_ship.data.set("existence_check", (int)World.world.getCurWorldTime());
				}
			}
			catch
			{
			}
		}

		private IEnumerator Run()
		{
			TerrainCache land = _mod.TerrainForTest;
			int w = land.Width;
			_ = land.Height;
			int deep = OpenSea(land);
			Check(deep >= 0, "open deep sea found", (deep >= 0) ? $"tile {deep % w},{deep / w}, {land.DepthOf(deep):F1} deep" : null);
			if (deep < 0)
			{
				yield break;
			}
			Vector2 sea = new Vector2((float)(deep % w) + 0.5f, (float)(deep / w) + 0.5f);
			Config.paused = true;
			float c0 = Sea.Now;
			yield return new WaitForSecondsRealtime(1f);
			float now = Sea.Now;
			Check(now - c0 > 0.7f && now - c0 < 1.6f, "the sea's clock runs while the game is paused", $"{now - c0:F2} s in a second");
			Check(Mathf.Abs(_mod.Renderer.Time - Sea.Now) < 0.2f, "the renderer draws by the sea's clock", $"renderer {_mod.Renderer.Time:F2}, sea {Sea.Now:F2}");
			Config.paused = false;
			Aim(sea, 2f, 0f, -10f);
			_mod.SeaStormForTest = 1f;
			for (int i = 0; i < 5; i++)
			{
				yield return null;
			}
			Check(Mathf.Abs(Sea.Storm - 1f) < 0.01f && Sea.A1 > 1f, "a storm held", $"storm {Sea.Storm:F2}, seas {Sea.A1:F2} + {Sea.A2:F2}");
			int num = BandTile(land, 1.5f, 4.5f, sea);
			int num2 = BandTile(land, 0.3f, 1f, sea);
			float num3 = Swing(land, deep);
			float num4 = ((num >= 0) ? Swing(land, num) : (-1f));
			float num5 = ((num2 >= 0) ? Swing(land, num2) : (-1f));
			Log.Info($"SWELL storm seas trough to crest: deep {num3:F2}, close {num4:F2}, shallow {num5:F2} tiles");
			Check(num3 > 2f && num3 < 3.6f, "a storm's seas stand 2 to 3 tiles over the deep ocean", $"{num3:F2}");
			if (num >= 0)
			{
				Check(num4 > 0.2f && num4 < num3 * 0.75f, "smaller over the close water", $"{num4:F2}");
			}
			if (num2 >= 0)
			{
				Check(num5 < 0.6f, "the shallows only a fraction of a tile", $"{num5:F2}");
			}
			_mod.SeaStormForTest = 0f;
			for (int i = 0; i < 3; i++)
			{
				yield return null;
			}
			float num6 = Swing(land, deep);
			Check(num6 > 0.1f && num6 < 0.3f, "on a fair day a tenth of a tile or so over the deep ocean", $"trough to crest {num6:F2}");
			try
			{
				_ship = World.world.units.createNewUnit("boat_transport_human", World.world.GetTile(deep % w, deep / w));
			}
			catch (Exception ex)
			{
				Log.Error("SWELL spawn: " + ex.Message);
			}
			Check(_ship != null, "a ship put out on the open sea");
			if (_ship != null)
			{
				_mod.SeaStormForTest = 1f;
				Aim(sea + new Vector2(-14f, -3f), 3f, Mathf.Atan2(3f, 14f), -6f);
				for (int j = 0; j < 30; j++)
				{
					yield return null;
				}
				List<float> hull = new List<float>();
				List<float> water = new List<float>();
				int i = 0;
				int tipCount = 0;
				for (int j = 0; j < 180; j++)
				{
					yield return null;
					MeshInstance meshInstance = Creatures.BoatPlaced(_ship);
					if (meshInstance == null)
					{
						continue;
					}
					Vector2 vector = new Vector2(meshInstance.Position.X, meshInstance.Position.Y);
					Vec3 c1 = meshInstance.Basis.C1;
					float num7 = Mathf.Sqrt(c1.X * c1.X + c1.Y * c1.Y);
					if (num7 < 0.0001f)
					{
						continue;
					}
					float num8 = (0f - c1.X) / num7;
					float num9 = (0f - c1.Y) / num7;
					float num10 = Mathf.Sqrt(Vec3.Dot(meshInstance.Basis.C0, meshInstance.Basis.C0));
					MeshModel model = meshInstance.Model;
					float num11 = Mathf.Max(0.5f, model.InfoFloat("length", model.Max.Y - model.Min.Y) * 0.5f * num10);
					float num12 = Mathf.Max(0.3f, model.InfoFloat("width", model.Max.X - model.Min.X) * 0.5f * num10);
					float item = (land.WaveAt(vector.x + num8 * num11, vector.y + num9 * num11) + land.WaveAt(vector.x - num8 * num11, vector.y - num9 * num11) + land.WaveAt(vector.x - num9 * num12, vector.y + num8 * num12) + land.WaveAt(vector.x + num9 * num12, vector.y - num8 * num12)) * 0.25f;
					hull.Add(meshInstance.Position.Z - land.LiquidSurface(vector.x, vector.y));
					water.Add(item);
					float num13 = land.WaveAt(vector.x + num8 * 3f, vector.y + num9 * 3f);
					float num14 = land.WaveAt(vector.x - num8 * 3f, vector.y - num9 * 3f);
					if (Mathf.Abs(num13 - num14) > 0.4f)
					{
						tipCount++;
						if (Mathf.Sign(0f - c1.Z) == Mathf.Sign(num13 - num14))
						{
							i++;
						}
					}
				}
				float num15 = Correlation(hull, water);
				float num16 = Spread(hull);
				Log.Info($"SWELL ship: {hull.Count} frames, heave {num16:F2} tiles across, correlation with the water under her (bow, stern, sides) {num15:F2}, tipped the water's way {i} of {tipCount}");
				Check(hull.Count > 100 && num16 > 0.8f, "in a storm the ship rises and falls with the seas", $"{num16:F2} tiles");
				Check(num15 > 0.7f, "her heave follows the water drawn under her", $"correlation {num15:F2}");
				Check(tipCount < 10 || i >= tipCount * 3 / 4, "her bow rises where the water under it stands higher than under her stern", $"{i} of {tipCount}");
				Check(Creatures.BoatTilt(_ship, out var bowUp, out var portUp) && Mathf.Abs(bowUp) < 0.6f && Mathf.Abs(portUp) < 0.6f, "the cabin reads her tilt from her hull", $"bow {bowUp * 57.29578f:F1}, port {portUp * 57.29578f:F1} degrees");
				SaveBoth("ship");
			}
			_mod.ForceSwim = true;
			Aim(sea + new Vector2(3f, 2f), 0f, 0.5f, 0f);
			for (int tipCount = 0; tipCount < 20; tipCount++)
			{
				yield return null;
			}
			List<float> eye = new List<float>();
			List<float> under = new List<float>();
			for (int tipCount = 0; tipCount < 120; tipCount++)
			{
				yield return null;
				Vector3 eyeForTest = _mod.EyeForTest;
				eye.Add(eyeForTest.z - land.LiquidSurface(eyeForTest.x, eyeForTest.y));
				under.Add(land.WaveAt(eyeForTest.x, eyeForTest.y));
			}
			float num17 = Correlation(eye, under);
			Check(num17 > 0.8f && Spread(eye) > 0.8f, "swimming, your head rides the storm's seas", $"correlation {num17:F2}, {Spread(eye):F2} tiles up and down");
			_mod.ForceSwim = false;
			float num18 = land.TopAt(sea.x, sea.y);
			float num19 = land.LiquidSurface(sea.x, sea.y) + land.WaveAt(sea.x, sea.y);
			Check(Mathf.Abs(num18 - num19) < 0.01f, "a shot meets the sea at its drawn surface", $"{num18:F2} against {num19:F2}");
			TileType tileType = AssetManager.tiles.get("pit_deep_ocean");
			int dry = DryLand(land);
			if (tileType != null && dry >= 0)
			{
				Aim(new Vector2((float)(dry % w) + 0.5f, (float)(dry / w) - 4f), 3f, MathF.PI / 2f, -20f);
				World.world.GetTile(dry % w, dry / w).setTileType(tileType);
				float before = land.Ground[dry];
				for (int tipCount = 0; tipCount < 20; tipCount++)
				{
					if (!(land.Ground[dry] > before - 0.5f))
					{
						break;
					}
					yield return new WaitForSecondsRealtime(0.25f);
				}
				float x = (float)(dry % w) + 0.5f;
				float y = (float)(dry / w) + 0.5f;
				float num20 = land.TopAt(x, y);
				float num21 = land.SampleHeight(x, y);
				Check(!land.IsWaterAt(x, y) && Mathf.Abs(num20 - num21) < 0.01f && num21 < before - 0.5f, "a shot into a dry hole comes down at its floor, never at the sea's level", $"hole floor {num21:F2} (was {before:F2}), shot meets {num20:F2}");
			}
			Settings s = _mod.Settings;
			_mod.SeaStormForTest = 1f;
			Config.paused = true;
			_mod.HeldTime = Mathf.Floor(Time.unscaledTime) + 0.4f;
			Aim(sea, 2.5f, 0.3f, -6f);
			Color[] means = new Color[2];
			for (int tipCount = 0; tipCount < 2; tipCount++)
			{
				s.GpuRender = tipCount == 1;
				for (int i = 0; i < 10; i++)
				{
					yield return null;
				}
				int width;
				int height;
				Color32[] array = Grab(out width, out height);
				_mod.SaveFrame("swell_storm_" + ((tipCount == 1) ? "gpu" : "cpu") + ".png");
				means[tipCount] = ((array != null) ? Mean(array, width, height) : Color.black);
			}
			float num22 = Mathf.Max(Mathf.Abs(means[0].r - means[1].r), Mathf.Max(Mathf.Abs(means[0].g - means[1].g), Mathf.Abs(means[0].b - means[1].b))) * 255f;
			Check(num22 < 22f, "the CPU and the GPU draw the same storm", $"{num22:F0} apart");
			_mod.HeldTime = -1f;
			Config.paused = false;
			for (int tipCount = 0; tipCount < 2; tipCount++)
			{
				s.GpuRender = tipCount == 1;
				float[] frame = new float[2];
				float[] terrain = new float[2];
				float[] dt = new float[2];
				for (int i = 0; i < 3; i++)
				{
					for (int j = 0; j < 2; j++)
					{
						_mod.SeaStormForTest = j;
						for (int k = 0; k < 10; k++)
						{
							yield return null;
						}
						for (int k = 0; k < 30; k++)
						{
							yield return null;
							frame[j] += _mod.FrameMs;
							terrain[j] += _mod.Renderer.TerrainMs;
							dt[j] += Time.unscaledDeltaTime * 1000f;
						}
					}
				}
				for (int l = 0; l < 2; l++)
				{
					frame[l] /= 90f;
					terrain[l] /= 90f;
					dt[l] /= 90f;
				}
				Log.Info((tipCount == 1) ? $"SWELL PERF GPU at sea: frame {dt[1]:F2} ms in a storm, {dt[0]:F2} ms calm; the mod's own work {frame[1]:F2} / {frame[0]:F2} ms" : $"SWELL PERF CPU at sea: terrain {terrain[1]:F2} ms in a storm, {terrain[0]:F2} ms calm ({(terrain[1] / Mathf.Max(0.01f, terrain[0]) - 1f) * 100f:+0;-0}%); the mod's frame {frame[1]:F2} / {frame[0]:F2} ms");
			}
			_mod.SeaStormForTest = -1f;
		}

		private static float Swing(TerrainCache land, int i)
		{
			float x = (float)(i % land.Width) + 0.5f;
			float y = (float)(i / land.Width) + 0.5f;
			float open = land.OpenAt(x, y);
			float num = float.MaxValue;
			float num2 = float.MinValue;
			for (float num3 = 0f; num3 < 60f; num3 += 0.1f)
			{
				float b = Sea.Height(x, y, Sea.Now + num3, open);
				num = Mathf.Min(num, b);
				num2 = Mathf.Max(num2, b);
			}
			return num2 - num;
		}

		private static float Spread(List<float> v)
		{
			if (v.Count == 0)
			{
				return 0f;
			}
			float num = float.MaxValue;
			float num2 = float.MinValue;
			foreach (float item in v)
			{
				num = Mathf.Min(num, item);
				num2 = Mathf.Max(num2, item);
			}
			return num2 - num;
		}

		private static float Correlation(List<float> a, List<float> b)
		{
			int num = Mathf.Min(a.Count, b.Count);
			if (num < 3)
			{
				return 0f;
			}
			double num2 = 0.0;
			double num3 = 0.0;
			for (int i = 0; i < num; i++)
			{
				num2 += (double)a[i];
				num3 += (double)b[i];
			}
			num2 /= (double)num;
			num3 /= (double)num;
			double num4 = 0.0;
			double num5 = 0.0;
			double num6 = 0.0;
			for (int j = 0; j < num; j++)
			{
				double num7 = (double)a[j] - num2;
				double num8 = (double)b[j] - num3;
				num4 += num7 * num8;
				num5 += num7 * num7;
				num6 += num8 * num8;
			}
			if (!(num5 > 1E-09) || !(num6 > 1E-09))
			{
				return 0f;
			}
			return (float)(num4 / Math.Sqrt(num5 * num6));
		}

		private static int OpenSea(TerrainCache land)
		{
			int num = land.Width * land.Height;
			int result = -1;
			int num2 = -1;
			for (int i = 0; i < num; i++)
			{
				if (!(land.DepthOf(i) < 15f) && land.SeaReach.Length == num)
				{
					int num3 = i % land.Width;
					int num4 = i / land.Width;
					if (num3 >= 20 && num4 >= 20 && num3 < land.Width - 20 && num4 < land.Height - 20 && land.SeaReach[i] > num2)
					{
						num2 = land.SeaReach[i];
						result = i;
					}
				}
			}
			return result;
		}

		private static int BandTile(TerrainCache land, float lo, float hi, Vector2 near)
		{
			int num = land.Width * land.Height;
			int result = -1;
			float num2 = float.MaxValue;
			for (int i = 0; i < num; i++)
			{
				float num3 = land.DepthOf(i);
				if (!(num3 < lo) && !(num3 > hi))
				{
					float num4 = (float)(i % land.Width) - near.x;
					float num5 = (float)(i / land.Width) - near.y;
					float num6 = num4 * num4 + num5 * num5;
					if (num6 < num2)
					{
						num2 = num6;
						result = i;
					}
				}
			}
			return result;
		}

		private static int DryLand(TerrainCache land)
		{
			int width = land.Width;
			int height = land.Height;
			for (int i = 10; i < height - 10; i += 7)
			{
				for (int j = 10; j < width - 10; j += 7)
				{
					bool flag = true;
					for (int k = -3; k <= 3 && flag; k++)
					{
						for (int l = -3; l <= 3 && flag; l++)
						{
							int num = (i + k) * width + j + l;
							flag = (land.Flags[num] & 0xB) == 0 && land.Ground[num] > 0.2f && land.Ground[num] < 3f;
						}
					}
					if (flag)
					{
						return i * width + j;
					}
				}
			}
			return -1;
		}

		private void SaveBoth(string name)
		{
			_mod.SaveFrame("swell_" + name + ".png");
		}

		private void Aim(Vector2 eye, float height, float yaw, float pitch)
		{
			_mod.DebugEye = eye;
			_mod.DebugEyeSet = true;
			_ = _mod.TerrainForTest;
			_mod.DebugEyeHeight = height;
			_mod.SetYaw(yaw);
			_mod.SetPitchDegrees(pitch);
		}

		private static Color Mean(Color32[] px, int w, int h)
		{
			double num = 0.0;
			double num2 = 0.0;
			double num3 = 0.0;
			int num4 = 0;
			for (int i = 0; i < h / 2; i++)
			{
				for (int j = 0; j < w; j++)
				{
					Color32 color = px[i * w + j];
					num += (double)(int)color.r;
					num2 += (double)(int)color.g;
					num3 += (double)(int)color.b;
					num4++;
				}
			}
			if (num4 <= 0)
			{
				return Color.black;
			}
			return new Color((float)(num / (double)num4 / 255.0), (float)(num2 / (double)num4 / 255.0), (float)(num3 / (double)num4 / 255.0));
		}

		private Color32[] Grab(out int width, out int height)
		{
			width = (height = 0);
			Texture2D texture2D = ((!_mod.Settings.GpuRender) ? null : _mod.GpuRendererForTest?.ReadOutput());
			if (texture2D != null)
			{
				width = texture2D.width;
				height = texture2D.height;
				Color32[] pixels = texture2D.GetPixels32();
				UnityEngine.Object.Destroy(texture2D);
				return pixels;
			}
			Texture2D frameTexture = _mod.FrameTexture;
			if (frameTexture == null)
			{
				return null;
			}
			width = frameTexture.width;
			height = frameTexture.height;
			return frameTexture.GetPixels32();
		}
	}
}
