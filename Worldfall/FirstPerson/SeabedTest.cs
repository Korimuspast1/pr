using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class SeabedTest : MonoBehaviour
	{
		private struct Spot
		{
			public string Name;

			public Vector2 Eye;

			public float Height;

			public float Yaw;

			public float Pitch;
		}

		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

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
			Log.Info((ok ? "SEABED PASS " : "SEABED FAIL ") + what + ((detail != null) ? (" (" + detail + ")") : ""));
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
				Log.Error("SEABED could not found a kingdom");
				Finish(quit);
				yield break;
			}
			ControllableUnit.setControllableCreature(actor);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("SEABED first person did not start");
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
					Log.Error("SEABED failed: " + ex);
				}
				break;
				IL_02aa:
				yield return current;
			}
			_mod.HeldTime = -1f;
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
			Log.Info($"SEABED {_pass} passed, {_fail} failed");
			Log.Info("SEABED DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator Run()
		{
			TerrainCache land = _mod.TerrainForTest;
			WorldSampler sampler = _mod.SamplerForTest;
			int w = land.Width;
			int height = land.Height;
			int num = w * height;
			Check(land.HasSeabed, "the sea's floor is laid as the world is read");
			if (!land.HasSeabed)
			{
				yield break;
			}
			byte[] flags = land.Flags;
			float[] array = new float[4];
			float[] array2 = new float[4];
			float[] array3 = new float[4];
			int[] array4 = new int[4];
			for (int i = 0; i < 4; i++)
			{
				array[i] = float.MaxValue;
			}
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			for (int j = 0; j < num; j++)
			{
				if ((flags[j] & 1) == 0)
				{
					continue;
				}
				num2++;
				byte b = sampler.SeaBandOf(j);
				if ((b & 0x10) != 0)
				{
					num4++;
					continue;
				}
				float num5 = land.DepthOf(j);
				if (num5 > 0f)
				{
					num3++;
				}
				int num6 = b & 3;
				if (num6 != 0)
				{
					array[num6] = Mathf.Min(array[num6], num5);
					array2[num6] = Mathf.Max(array2[num6], num5);
					array3[num6] += num5;
					array4[num6]++;
				}
			}
			Log.Info($"SEABED map {w}x{height}: {num2} water tiles, {num3} with a floor under them, {num4} lakes' own");
			Check(num3 >= (num2 - num4) * 99 / 100, "a floor under every tile of the sea", $"{num3} of {num2 - num4}");
			string[] array5 = new string[4] { "", "shallow", "close", "deep" };
			float[] array6 = new float[4] { 0f, 0.05f, 1f, 7f };
			float[] array7 = new float[4] { 0f, 1.4f, 5.5f, 25.5f };
			for (int k = 1; k <= 3; k++)
			{
				if (array4[k] == 0)
				{
					Log.Info("SEABED no " + array5[k] + " water on this map");
					continue;
				}
				Log.Info($"SEABED {array5[k]}: {array4[k]} tiles, {array[k]:F1}..{array2[k]:F1} deep, {array3[k] / (float)array4[k]:F2} on average");
				Check(array[k] >= array6[k] - 0.01f && array2[k] <= array7[k] + 0.01f, array5[k] + " water as deep as its band", $"{array[k]:F2}..{array2[k]:F2}, band {array6[k]}..{array7[k]}");
			}
			int num7 = 0;
			int num8 = 0;
			for (int l = 0; l < height; l++)
			{
				for (int m = 0; m + 1 < w; m++)
				{
					int num9 = l * w + m;
					int num10 = num9 + 1;
					if ((flags[num9] & flags[num10] & 1) != 0 && (sampler.SeaBandOf(num9) & 3) == (sampler.SeaBandOf(num10) & 3))
					{
						num8++;
						if (Mathf.Abs(land.DepthOf(num9) - land.DepthOf(num10)) > 1.2f)
						{
							num7++;
						}
					}
				}
			}
			Check(num8 > 0 && num7 <= num8 / 200, "within a band the floor runs smoothly from tile to tile", $"{num7} of {num8} neighbours more than 1.2 apart");
			int num11 = 0;
			int num12 = 0;
			for (int n = 0; n < num; n += 7)
			{
				if (num11 >= 400)
				{
					break;
				}
				if ((flags[n] & 1) == 0 || (flags[n] & 0x10) == 0)
				{
					continue;
				}
				int num13 = n % w;
				int num14 = n / w;
				for (int num15 = 0; num15 < 4; num15++)
				{
					int num16;
					switch (num15)
					{
					default:
						num16 = 0;
						break;
					case 1:
						num16 = -1;
						break;
					case 0:
						num16 = 1;
						break;
					}
					int num17 = num13 + num16;
					int num18;
					switch (num15)
					{
					default:
						num18 = 0;
						break;
					case 3:
						num18 = -1;
						break;
					case 2:
						num18 = 1;
						break;
					}
					int num19 = num14 + num18;
					if (num17 >= 0 && num19 >= 0 && num17 < w && num19 < height && (flags[num19 * w + num17] & 3) == 0)
					{
						num11++;
						if (land.FloorHeight((float)num13 + 0.5f + (float)(num17 - num13) * 0.5f, (float)num14 + 0.5f + (float)(num19 - num14) * 0.5f) < -0.6f)
						{
							num12++;
						}
						break;
					}
				}
			}
			Check(num11 > 0 && num12 <= num11 / 20, "the floor meets the beach at the shoreline", $"{num12} of {num11} shore edges more than 0.6 under");
			TileType deep = AssetManager.tiles.get("deep_ocean");
			TileType pitShallow = AssetManager.tiles.get("pit_shallow_waters");
			TileType shallow = AssetManager.tiles.get("shallow_waters");
			int hole = LoneShallow(land, sampler, 3);
			if (hole >= 0 && deep != null && shallow != null)
			{
				WorldTile t = World.world.GetTile(hole % w, hole / w);
				float before = land.DepthOf(hole);
				Aim(new Spot
				{
					Eye = new Vector2((float)(hole % w) + 0.5f, (float)(hole / w) - 6f),
					Height = 3f,
					Yaw = MathF.PI / 2f,
					Pitch = -25f
				});
				t.setTileType(deep);
				float num20 = before;
				for (int num21 = 0; num21 < 40; num21++)
				{
					if (!(num20 < 9f))
					{
						break;
					}
					yield return new WaitForSecondsRealtime(0.25f);
					num20 = land.DepthOf(hole);
				}
				Check(num20 >= 9.5f, "a deep tile painted into the shallows is a true hole as deep as its band", $"{before:F1} -> {num20:F1} tiles");
				float num22 = land.DepthOf(hole + 2);
				Check(num22 < 2f, "the shallows beside it stay shallow", $"two tiles off {num22:F1}");
				for (int num21 = 0; num21 < 2; num21++)
				{
					_mod.Settings.GpuRender = num21 == 1;
					for (int num23 = 0; num23 < 8; num23++)
					{
						yield return null;
					}
					_mod.SaveFrame("seabed_hole_" + ((num21 == 1) ? "gpu" : "cpu") + ".png");
				}
				TileType tileType = AssetManager.tiles.get("pit_deep_ocean");
				if (tileType != null)
				{
					float floorBefore = land.Floor[hole];
					Config.paused = true;
					t.setTileType(tileType);
					for (int num21 = 0; num21 < 20; num21++)
					{
						if ((land.Flags[hole] & 1) == 0)
						{
							break;
						}
						yield return new WaitForSecondsRealtime(0.25f);
					}
					for (int num21 = 0; num21 < 8; num21++)
					{
						yield return new WaitForSecondsRealtime(0.25f);
					}
					bool flag = (land.Flags[hole] & 1) == 0;
					Check(flag && land.Ground[hole] < -8f, "a deep hole drained stays as deep", $"dry {flag}, floor {floorBefore:F1}, bed {land.Ground[hole]:F1}");
					for (int num21 = 0; num21 < 2; num21++)
					{
						_mod.Settings.GpuRender = num21 == 1;
						for (int num23 = 0; num23 < 8; num23++)
						{
							yield return null;
						}
						_mod.SaveFrame("seabed_dryhole_" + ((num21 == 1) ? "gpu" : "cpu") + ".png");
					}
					float num24 = _mod.GpuRendererForTest?.TerrainForTest?.DeepSkirtForTest(hole % w, hole / w) ?? (-1f);
					Check(num24 >= 0f - land.Ground[hole] - 0.5f, "the graphics card hangs its land's skirt as deep round it", $"skirt {num24:F1}, bed {land.Ground[hole]:F1}");
					t.setTileType(deep);
					for (int num21 = 0; num21 < 20; num21++)
					{
						if ((land.Flags[hole] & 1) != 0)
						{
							break;
						}
						yield return new WaitForSecondsRealtime(0.25f);
					}
					Config.paused = false;
				}
				t.setTileType(shallow);
				for (int num21 = 0; num21 < 40; num21++)
				{
					if (!(land.DepthOf(hole) > 2f))
					{
						break;
					}
					yield return new WaitForSecondsRealtime(0.25f);
				}
				Check(land.DepthOf(hole) < 2f, "and shallow again once it is shallow water again", $"{land.DepthOf(hole):F1}");
			}
			else
			{
				Log.Info("SEABED no lone shallow tile for the hole: skipped");
			}
			int drain = LoneShallow(land, sampler, 2);
			if (drain >= 0 && pitShallow != null && shallow != null)
			{
				WorldTile t = World.world.GetTile(drain % w, drain / w);
				float before = land.Floor[drain];
				Aim(new Spot
				{
					Eye = new Vector2((float)(drain % w) + 0.5f, (float)(drain / w) - 5f),
					Height = 2.5f,
					Yaw = MathF.PI / 2f,
					Pitch = -25f
				});
				Config.paused = true;
				t.setTileType(pitShallow);
				for (int num21 = 0; num21 < 20; num21++)
				{
					if ((land.Flags[drain] & 1) == 0)
					{
						break;
					}
					yield return new WaitForSecondsRealtime(0.25f);
				}
				for (int num21 = 0; num21 < 8; num21++)
				{
					yield return new WaitForSecondsRealtime(0.25f);
				}
				bool flag2 = (land.Flags[drain] & 1) == 0;
				float num25 = land.Ground[drain];
				Check(flag2 && Mathf.Abs(num25 - before) < 0.11f, "water drained leaves the bed it showed", $"dry {flag2}, floor {before:F2}, bed {num25:F2}");
				t.setTileType(shallow);
				for (int num21 = 0; num21 < 20; num21++)
				{
					if ((land.Flags[drain] & 1) != 0)
					{
						break;
					}
					yield return new WaitForSecondsRealtime(0.25f);
				}
				for (int num21 = 0; num21 < 8; num21++)
				{
					yield return new WaitForSecondsRealtime(0.25f);
				}
				Config.paused = false;
				Check((land.Flags[drain] & 1) != 0 && Mathf.Abs(land.Floor[drain] - before) < 0.11f, "and filled again, the floor where it was", $"floor {land.Floor[drain]:F2}");
			}
			else
			{
				Log.Info("SEABED no shallow tile to drain: skipped");
			}
			List<Spot> spots = new List<Spot>();
			if (Shallows(land, out var eye, out var yaw))
			{
				spots.Add(new Spot
				{
					Name = "down",
					Eye = eye,
					Height = 1.1f,
					Yaw = yaw,
					Pitch = -40f
				});
				spots.Add(new Spot
				{
					Name = "out",
					Eye = eye,
					Height = 1.6f,
					Yaw = yaw,
					Pitch = -8f
				});
				spots.Add(new Spot
				{
					Name = "above",
					Eye = eye,
					Height = 12f,
					Yaw = yaw,
					Pitch = -45f
				});
			}
			int num26 = Deepest(land);
			if (num26 >= 0)
			{
				spots.Add(new Spot
				{
					Name = "deep",
					Eye = new Vector2((float)(num26 % w) + 0.5f, (float)(num26 / w) + 0.5f),
					Height = 1.3f,
					Yaw = 0.7f,
					Pitch = -12f
				});
			}
			Check(spots.Count == 4, "shallows by a beach and deep water found to look at", $"{spots.Count} views");
			Settings s = _mod.Settings;
			Config.paused = true;
			_mod.HeldTime = Mathf.Floor(Time.unscaledTime) + 0.25f;
			Dictionary<string, Color> means = new Dictionary<string, Color>();
			foreach (Spot sp in spots)
			{
				Aim(sp);
				for (int num21 = 0; num21 < 2; num21++)
				{
					s.GpuRender = num21 == 1;
					for (int num23 = 0; num23 < 10; num23++)
					{
						yield return null;
					}
					int width;
					int height2;
					Color32[] array8 = Grab(out width, out height2);
					_mod.SaveFrame("seabed_" + sp.Name + "_" + ((num21 == 1) ? "gpu" : "cpu") + ".png");
					if (array8 == null)
					{
						Check(ok: false, sp.Name + ": the " + ((num21 == 1) ? "GPU" : "CPU") + "'s picture read");
					}
					else
					{
						means[sp.Name + num21] = Mean(array8, width, height2);
					}
				}
				if (means.TryGetValue(sp.Name + 0, out var value) && means.TryGetValue(sp.Name + 1, out var value2))
				{
					float num27 = Mathf.Max(Mathf.Abs(value.r - value2.r), Mathf.Max(Mathf.Abs(value.g - value2.g), Mathf.Abs(value.b - value2.b))) * 255f;
					Check(num27 < 22f, sp.Name + ": the CPU and the GPU see into the water alike", $"lower half's mean CPU {Hex(value)}, GPU {Hex(value2)}, {num27:F0} apart");
				}
			}
			if (means.TryGetValue("down1", out var value3) && means.TryGetValue("deep1", out var value4))
			{
				Check(value3.g - value4.g > 0.06f && value3.r - value4.r > 0.04f, "the shallows show their sand, the deep water none", "shallows " + Hex(value3) + ", deep " + Hex(value4));
			}
			_mod.HeldTime = -1f;
			Config.paused = false;
			if (spots.Count <= 0)
			{
				yield break;
			}
			Aim(spots[0]);
			for (int num21 = 0; num21 < 2; num21++)
			{
				s.GpuRender = num21 == 1;
				float[] frame = new float[2];
				float[] terrain = new float[2];
				float[] dt = new float[2];
				for (int num23 = 0; num23 < 3; num23++)
				{
					for (int on = 0; on < 2; on++)
					{
						land.HasSeabed = on == 1;
						for (int num28 = 0; num28 < 10; num28++)
						{
							yield return null;
						}
						for (int num28 = 0; num28 < 30; num28++)
						{
							yield return null;
							frame[on] += _mod.FrameMs;
							terrain[on] += _mod.Renderer.TerrainMs;
							dt[on] += Time.unscaledDeltaTime * 1000f;
						}
					}
				}
				land.HasSeabed = true;
				for (int num29 = 0; num29 < 2; num29++)
				{
					frame[num29] /= 90f;
					terrain[num29] /= 90f;
					dt[num29] /= 90f;
				}
				Log.Info((num21 == 1) ? $"SEABED PERF GPU in the shallows: frame {dt[1]:F2} ms seeing in, {dt[0]:F2} ms not; the mod's own work {frame[1]:F2} / {frame[0]:F2} ms" : $"SEABED PERF CPU in the shallows: terrain {terrain[1]:F2} ms seeing in, {terrain[0]:F2} ms not ({(terrain[1] / Mathf.Max(0.01f, terrain[0]) - 1f) * 100f:+0;-0}%); the mod's frame {frame[1]:F2} / {frame[0]:F2} ms");
			}
		}

		private static string Hex(Color c)
		{
			return $"#{(int)(c.r * 255f):X2}{(int)(c.g * 255f):X2}{(int)(c.b * 255f):X2}";
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

		private static int LoneShallow(TerrainCache land, WorldSampler sampler, int reach)
		{
			int width = land.Width;
			int height = land.Height;
			for (int i = reach + 2; i < height - reach - 2; i += 3)
			{
				for (int j = reach + 2; j < width - reach - 2; j += 3)
				{
					int num = i * width + j;
					if ((sampler.SeaBandOf(num) & 0x17) != 1 || land.DepthOf(num) <= 0f)
					{
						continue;
					}
					bool flag = true;
					for (int k = i - reach; k <= i + reach && flag; k++)
					{
						for (int l = j - reach; l <= j + reach && flag; l++)
						{
							flag = (sampler.SeaBandOf(k * width + l) & 0x17) == 1;
						}
					}
					if (flag)
					{
						return num;
					}
				}
			}
			return -1;
		}

		private static bool Shallows(TerrainCache land, out Vector2 eye, out float yaw)
		{
			eye = Vector2.zero;
			yaw = 0f;
			int width = land.Width;
			int height = land.Height;
			float num = -1f;
			for (int i = 12; i < height - 12; i++)
			{
				for (int j = 12; j < width - 12; j++)
				{
					int num2 = i * width + j;
					if ((land.Flags[num2] & 3) != 0)
					{
						continue;
					}
					for (int k = 0; k < 8; k++)
					{
						float num3 = (float)k * MathF.PI / 4f;
						float num4 = Mathf.Cos(num3);
						float num5 = Mathf.Sin(num3);
						bool flag = true;
						float num6 = 0f;
						for (int l = 1; l <= 12 && flag; l++)
						{
							int num7 = Mathf.FloorToInt((float)j + 0.5f + num4 * (float)l);
							int num8 = Mathf.FloorToInt((float)i + 0.5f + num5 * (float)l) * width + num7;
							flag = (land.Flags[num8] & 1) != 0 && (l > 4 || land.DepthOf(num8) < 1f);
							num6 = land.DepthOf(num8);
						}
						if (flag && !(num6 < 2f) && num6 > num)
						{
							num = num6;
							eye = new Vector2((float)j + 0.5f + num4 * 2f, (float)i + 0.5f + num5 * 2f);
							yaw = num3;
						}
					}
				}
			}
			return num >= 0f;
		}

		private static int Deepest(TerrainCache land)
		{
			int width = land.Width;
			int height = land.Height;
			int result = -1;
			int num = -1;
			for (int i = 0; i < width * height; i++)
			{
				if (!(land.DepthOf(i) < 15f))
				{
					int num2 = ((land.SeaReach.Length == width * height) ? land.SeaReach[i] : 0);
					if (num2 > num)
					{
						num = num2;
						result = i;
					}
				}
			}
			return result;
		}

		private void Aim(Spot sp)
		{
			_mod.DebugEye = sp.Eye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = sp.Height;
			_mod.SetYaw(sp.Yaw);
			_mod.SetPitchDegrees(sp.Pitch);
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
