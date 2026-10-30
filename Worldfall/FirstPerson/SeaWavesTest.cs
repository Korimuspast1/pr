using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class SeaWavesTest : MonoBehaviour
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
			Log.Info((ok ? "SEA PASS " : "SEA FAIL ") + what + ((detail != null) ? (" (" + detail + ")") : ""));
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
				Log.Error("SEA could not found a kingdom");
				Finish(quit);
				yield break;
			}
			ControllableUnit.setControllableCreature(actor);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("SEA first person did not start");
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
					Log.Error("SEA failed: " + ex);
				}
				break;
				IL_02aa:
				yield return current;
			}
			_mod.Renderer.SeaWaves = true;
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
			Log.Info($"SEA {_pass} passed, {_fail} failed");
			Log.Info("SEA DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator Run()
		{
			TerrainCache land = _mod.TerrainForTest;
			int w = land.Width;
			int h = land.Height;
			int n = w * h;
			int i = 0;
			int width;
			while (i < 20 && land.SeaReach.Length != n)
			{
				yield return new WaitForSecondsRealtime(0.5f);
				width = i++;
			}
			Check(land.SeaReach.Length == n, "every tile's distance from land is worked out", $"{land.SeaReach.Length} of {n}");
			if (land.SeaReach.Length != n)
			{
				yield break;
			}
			byte[] seaReach = land.SeaReach;
			byte[] flags = land.Flags;
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			int num6 = -1;
			for (int j = 0; j < n; j++)
			{
				if ((flags[j] & 1) != 0)
				{
					num++;
					if (seaReach[j] < 8)
					{
						num3++;
					}
					if (seaReach[j] >= 104)
					{
						num2++;
					}
					if (seaReach[j] > num5)
					{
						num5 = seaReach[j];
						num6 = j;
					}
				}
				else if (seaReach[j] != 0 && (flags[j] & 2) == 0)
				{
					num4++;
				}
			}
			Log.Info($"SEA map {w}x{h}: {num} water tiles, {num2} of them 13 or more tiles from land, the farthest {(float)num5 / 8f:F1} tiles out");
			Check(num3 <= num / 200 && num4 <= (n - num) / 200, "land at 0, water a tile or more out", $"{num3} water and {num4} land tiles otherwise");
			int num7 = 0;
			int num8 = 0;
			float num9 = 0f;
			for (int k = 0; k < n; k += 89)
			{
				if (num7 >= 400)
				{
					break;
				}
				if ((flags[k] & 1) == 0)
				{
					continue;
				}
				float num10 = NearestLand(land, k % w, k / w, 32);
				if (!(num10 > 30f))
				{
					num7++;
					float num11 = Mathf.Abs((float)(int)seaReach[k] / 8f - num10);
					if (num11 > 0.1f * num10 + 0.25f)
					{
						num8++;
					}
					num9 = Mathf.Max(num9, num11 / num10);
				}
			}
			Check(num7 > 0 && num8 <= num7 / 50, "the distances match the straight line to the nearest land, within a tenth", $"{num8} of {num7} off, worst {num9 * 100f:F0}%");
			Check(num2 > 0, "the test world has open sea (13 or more tiles from land)", $"{num2} tiles");
			if (num2 == 0)
			{
				yield break;
			}
			List<Spot> spots = new List<Spot>();
			int num12 = num6;
			float num13 = float.MaxValue;
			for (int l = 0; l < n; l++)
			{
				if (seaReach[l] >= 160)
				{
					float num14 = (float)(l % w) - (float)w * 0.5f;
					float num15 = (float)(l / w) - (float)h * 0.5f;
					if (num14 * num14 + num15 * num15 < num13)
					{
						num13 = num14 * num14 + num15 * num15;
						num12 = l;
					}
				}
			}
			int num16 = num12 % w;
			int num17 = num12 / w;
			spots.Add(new Spot
			{
				Name = "deck",
				Eye = new Vector2((float)num16 + 0.5f, (float)num17 + 0.5f),
				Height = 1.3f,
				Yaw = OpenestWay(land, num16, num17),
				Pitch = -4f
			});
			if (OpenBeach(land, out var eye, out var yaw))
			{
				spots.Add(new Spot
				{
					Name = "beach",
					Eye = eye,
					Height = 1.2f,
					Yaw = yaw,
					Pitch = -7f
				});
			}
			Check(spots.Count == 2, "a beach facing open sea found");
			foreach (Spot item in spots)
			{
				Log.Info($"SEA view {item.Name}: eye {item.Eye.x:F1},{item.Eye.y:F1} +{item.Height:F1} yaw {item.Yaw * 57.29578f:F0}");
			}
			float now = Mathf.Floor(Time.unscaledTime) + 0.25f;
			List<float> list = new List<float>();
			List<float> list2 = new List<float>();
			for (float num18 = -10f; num18 <= 10f; num18 += 0.25f)
			{
				for (float num19 = -10f; num19 <= 10f; num19 += 0.25f)
				{
					list.Add((float)num16 + 0.5f + num19);
					list2.Add((float)num17 + 0.5f + num18);
				}
			}
			int num20 = Hits(land, list, list2, now, 3);
			Check(num20 > list.Count * 3 / 500, "whitecaps out at sea", $"{num20} of {list.Count * 3} points foamed over three moments");
			if (spots.Count == 2)
			{
				Vector2 eye2 = spots[1].Eye;
				Vector2 vector = new Vector2(0f - Mathf.Sin(spots[1].Yaw), Mathf.Cos(spots[1].Yaw));
				Vector2 vector2 = new Vector2(Mathf.Cos(spots[1].Yaw), Mathf.Sin(spots[1].Yaw));
				List<float> list3 = new List<float>();
				List<float> list4 = new List<float>();
				for (float num21 = -8f; num21 <= 8f; num21 += 0.25f)
				{
					for (float num22 = 0.5f; num22 <= 4.5f; num22 += 0.25f)
					{
						Vector2 vector3 = eye2 + vector * num21 + vector2 * num22;
						if (!(vector3.x < 0f) && !(vector3.y < 0f) && !(vector3.x >= (float)w) && !(vector3.y >= (float)h) && (flags[(int)vector3.y * w + (int)vector3.x] & 1) != 0)
						{
							list3.Add(vector3.x);
							list4.Add(vector3.y);
						}
					}
				}
				int num23 = ((list3.Count > 0) ? Hits(land, list3, list4, now, 6) : 0);
				Check(num23 > 0, "breakers by the open beach", $"{num23} of {list3.Count * 6} points foamed over six moments");
			}
			if (Pond(land, out var tiles))
			{
				List<float> list5 = new List<float>();
				List<float> list6 = new List<float>();
				foreach (int item2 in tiles)
				{
					for (int m = 0; m < 4; m++)
					{
						list5.Add((float)(item2 % w) + 0.25f + 0.5f * (float)(m & 1));
						list6.Add((float)(item2 / w) + 0.25f + 0.5f * (float)(m >> 1));
					}
				}
				int num24 = Hits(land, list5, list6, now, 8);
				Check(num24 == 0, "a pond stays calm", $"{tiles.Count} tiles, {num24} points foamed over eight moments");
			}
			else
			{
				Log.Info("SEA no pond on this map: its check skipped");
			}
			Settings s = _mod.Settings;
			Config.paused = true;
			_mod.HeldTime = now;
			foreach (Spot sp in spots)
			{
				Aim(sp);
				bool[][] masks = new bool[2][];
				i = 0;
				int fh = 0;
				int gpu = 0;
				while (gpu < 2)
				{
					s.GpuRender = gpu == 1;
					string tag = "sea_" + sp.Name + "_" + ((gpu == 1) ? "gpu" : "cpu");
					_mod.Renderer.SeaWaves = false;
					int i2 = 0;
					while (i2 < 8)
					{
						yield return null;
						width = i2++;
					}
					Color32[] calm = Grab(out i, out fh);
					_mod.SaveFrame(tag + "_calm.png");
					_mod.Renderer.SeaWaves = true;
					i2 = 0;
					while (i2 < 8)
					{
						yield return null;
						width = i2++;
					}
					int fw2;
					int fh2;
					Color32[] waves = Grab(out fw2, out fh2);
					_mod.SaveFrame(tag + ".png");
					_mod.Renderer.SeaWaves = false;
					i2 = 0;
					while (i2 < 8)
					{
						yield return null;
						width = i2++;
					}
					int height;
					Color32[] array = Grab(out width, out height);
					_mod.Renderer.SeaWaves = true;
					if (calm == null || waves == null || array == null || calm.Length != waves.Length || array.Length != calm.Length)
					{
						Check(ok: false, sp.Name + ": the " + ((gpu == 1) ? "GPU" : "CPU") + "'s pictures read", "none or different sizes");
					}
					else
					{
						bool[] array2 = new bool[calm.Length];
						int num25 = 0;
						int num26 = 0;
						for (int num27 = 0; num27 < calm.Length; num27++)
						{
							if (Differ(calm[num27], array[num27]))
							{
								num26++;
							}
							else if (Differ(calm[num27], waves[num27]))
							{
								array2[num27] = true;
								num25++;
							}
						}
						if (num26 > 0)
						{
							Log.Info(string.Format("SEA {0} {1}: {2} pixels moving between the calm pictures, left out", sp.Name, (gpu == 1) ? "GPU" : "CPU", num26));
						}
						masks[gpu] = array2;
						Check(num25 > 20, sp.Name + ": the " + ((gpu == 1) ? "GPU" : "CPU") + " draws the foam", $"{num25} of {calm.Length} pixels {fw2}x{fh2}");
					}
					height = gpu++;
				}
				if (masks[0] != null && masks[1] != null && masks[0].Length == masks[1].Length)
				{
					float num28 = Near(masks[1], masks[0], i, fh, 2);
					float num29 = Near(masks[0], masks[1], i, fh, 2);
					Check(num28 > 0.5f && num29 > 0.5f, sp.Name + ": the CPU's foam and the GPU's in the same places", $"{num28 * 100f:F0}% of the GPU's by the CPU's, {num29 * 100f:F0}% of the CPU's by the GPU's");
				}
			}
			s.GpuRender = true;
			Aim(spots[0]);
			for (int fh = 0; fh < 6; fh++)
			{
				_mod.HeldTime = now + (float)fh * 0.5f;
				for (i = 0; i < 4; i++)
				{
					yield return null;
				}
				_mod.SaveFrame($"sea_deck_gpu_s{fh}.png");
			}
			_mod.HeldTime = -1f;
			Aim(spots[0]);
			for (int fh = 0; fh < 2; fh++)
			{
				s.GpuRender = fh == 1;
				float[] frame = new float[2];
				float[] terrain = new float[2];
				float[] dt = new float[2];
				for (i = 0; i < 3; i++)
				{
					for (int gpu = 0; gpu < 2; gpu++)
					{
						_mod.Renderer.SeaWaves = gpu == 1;
						for (int fh2 = 0; fh2 < 10; fh2++)
						{
							yield return null;
						}
						for (int fh2 = 0; fh2 < 30; fh2++)
						{
							yield return null;
							frame[gpu] += _mod.FrameMs;
							terrain[gpu] += _mod.Renderer.TerrainMs;
							dt[gpu] += Time.unscaledDeltaTime * 1000f;
						}
					}
				}
				for (int num30 = 0; num30 < 2; num30++)
				{
					frame[num30] /= 90f;
					terrain[num30] /= 90f;
					dt[num30] /= 90f;
				}
				Log.Info((fh == 1) ? $"SEA PERF GPU at sea: frame {dt[1]:F2} ms with the waves, {dt[0]:F2} ms without; the mod's own work {frame[1]:F2} / {frame[0]:F2} ms" : $"SEA PERF CPU at sea: terrain {terrain[1]:F2} ms with the waves, {terrain[0]:F2} ms without ({(terrain[1] / Mathf.Max(0.01f, terrain[0]) - 1f) * 100f:+0;-0}%); the mod's frame {frame[1]:F2} / {frame[0]:F2} ms");
			}
			_mod.Renderer.SeaWaves = true;
		}

		private static bool Differ(Color32 a, Color32 b)
		{
			return Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b))) > 24;
		}

		private int Hits(TerrainCache land, List<float> xs, List<float> ys, float t0, int moments)
		{
			float[] array = xs.ToArray();
			float[] ys2 = ys.ToArray();
			float[] array2 = new float[array.Length];
			int num = 0;
			for (int i = 0; i < moments; i++)
			{
				_mod.Renderer.SeaFoamForTest(land, t0 + (float)i * 0.7f, array, ys2, array2);
				float[] array3 = array2;
				for (int j = 0; j < array3.Length; j++)
				{
					if (array3[j] > 0f)
					{
						num++;
					}
				}
			}
			return num;
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

		private static float Near(bool[] a, bool[] b, int w, int h, int r)
		{
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < h; i++)
			{
				for (int j = 0; j < w; j++)
				{
					if (!a[i * w + j])
					{
						continue;
					}
					num++;
					bool flag = false;
					for (int k = Math.Max(0, i - r); k <= Math.Min(h - 1, i + r); k++)
					{
						if (flag)
						{
							break;
						}
						for (int l = Math.Max(0, j - r); l <= Math.Min(w - 1, j + r); l++)
						{
							if (b[k * w + l])
							{
								flag = true;
								break;
							}
						}
					}
					if (flag)
					{
						num2++;
					}
				}
			}
			if (num <= 0)
			{
				return 0f;
			}
			return (float)num2 / (float)num;
		}

		private static float NearestLand(TerrainCache land, int x, int y, int reach)
		{
			float num = 99f;
			for (int i = Math.Max(0, y - reach); i <= Math.Min(land.Height - 1, y + reach); i++)
			{
				for (int j = Math.Max(0, x - reach); j <= Math.Min(land.Width - 1, x + reach); j++)
				{
					if ((land.Flags[i * land.Width + j] & 1) == 0)
					{
						float num2 = Mathf.Sqrt((j - x) * (j - x) + (i - y) * (i - y));
						if (num2 < num)
						{
							num = num2;
						}
					}
				}
			}
			return num;
		}

		private static float OpenestWay(TerrainCache land, int x, int y)
		{
			float num = -1f;
			float result = 0f;
			for (int i = 0; i < 16; i++)
			{
				float num2 = (float)i * MathF.PI / 8f;
				float num3 = 0f;
				for (int j = 4; j <= 40; j += 2)
				{
					int num4 = Mathf.FloorToInt((float)x + 0.5f + Mathf.Cos(num2) * (float)j);
					int num5 = Mathf.FloorToInt((float)y + 0.5f + Mathf.Sin(num2) * (float)j);
					num3 += (float)(int)((num4 < 0 || num5 < 0 || num4 >= land.Width || num5 >= land.Height) ? byte.MaxValue : land.SeaReach[num5 * land.Width + num4]);
				}
				if (num3 > num)
				{
					num = num3;
					result = num2;
				}
			}
			return result;
		}

		private static bool OpenBeach(TerrainCache land, out Vector2 eye, out float yaw)
		{
			eye = Vector2.zero;
			yaw = 0f;
			int width = land.Width;
			int height = land.Height;
			int num = -1;
			for (int i = 4; i < height - 4; i++)
			{
				for (int j = 4; j < width - 4; j++)
				{
					int num2 = i * width + j;
					if ((land.Flags[num2] & 0xB) != 0)
					{
						continue;
					}
					for (int k = 0; k < 8; k++)
					{
						float num3 = (float)k * MathF.PI / 4f;
						float num4 = Mathf.Cos(num3);
						float num5 = Mathf.Sin(num3);
						bool flag = true;
						for (int l = 1; l <= 8 && flag; l++)
						{
							int num6 = Mathf.FloorToInt((float)j + 0.5f + num4 * (float)l);
							int num7 = Mathf.FloorToInt((float)i + 0.5f + num5 * (float)l);
							flag = num6 >= 0 && num7 >= 0 && num6 < width && num7 < height && (land.Flags[num7 * width + num6] & 1) != 0;
						}
						if (flag)
						{
							int num8 = Mathf.FloorToInt((float)j + 0.5f + num4 * 12f);
							int num9 = Mathf.FloorToInt((float)i + 0.5f + num5 * 12f);
							int num10 = ((num8 < 0 || num9 < 0 || num8 >= width || num9 >= height) ? byte.MaxValue : land.SeaReach[num9 * width + num8]);
							if (num10 >= 64 && num10 > num)
							{
								num = num10;
								eye = new Vector2((float)j + 0.5f, (float)i + 0.5f);
								yaw = num3;
							}
						}
					}
				}
			}
			return num >= 0;
		}

		private static bool Pond(TerrainCache land, out List<int> tiles)
		{
			tiles = new List<int>();
			int width = land.Width;
			int height = land.Height;
			bool[] array = new bool[width * height];
			Stack<int> stack = new Stack<int>();
			for (int i = 0; i < width * height; i++)
			{
				if (array[i] || (land.Flags[i] & 1) == 0)
				{
					continue;
				}
				tiles.Clear();
				int num = 0;
				bool flag = false;
				array[i] = true;
				stack.Push(i);
				while (stack.Count > 0)
				{
					int num2 = stack.Pop();
					int num3 = num2 % width;
					int num4 = num2 / width;
					tiles.Add(num2);
					num = Math.Max(num, land.SeaReach[num2]);
					if (num3 == 0 || num4 == 0 || num3 == width - 1 || num4 == height - 1)
					{
						flag = true;
					}
					for (int j = 0; j < 4; j++)
					{
						int num5;
						switch (j)
						{
						default:
							num5 = 0;
							break;
						case 1:
							num5 = -1;
							break;
						case 0:
							num5 = 1;
							break;
						}
						int num6 = num3 + num5;
						int num7;
						switch (j)
						{
						default:
							num7 = 0;
							break;
						case 3:
							num7 = -1;
							break;
						case 2:
							num7 = 1;
							break;
						}
						int num8 = num4 + num7;
						if (num6 >= 0 && num8 >= 0 && num6 < width && num8 < height)
						{
							int num9 = num8 * width + num6;
							if (!array[num9] && (land.Flags[num9] & 1) != 0)
							{
								array[num9] = true;
								stack.Push(num9);
							}
						}
					}
				}
				if (!flag && tiles.Count >= 4 && tiles.Count <= 400 && num <= 24)
				{
					return true;
				}
			}
			tiles.Clear();
			return false;
		}
	}
}
