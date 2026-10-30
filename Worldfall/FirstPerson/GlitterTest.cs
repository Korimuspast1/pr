using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class GlitterTest : MonoBehaviour
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

		private const int Steps = 13;

		private const float Step = 0.04f;

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
			Log.Info((ok ? "GLT PASS " : "GLT FAIL ") + what + ((detail != null) ? (" (" + detail + ")") : ""));
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
				Log.Error("GLT could not found a kingdom");
				Finish(quit);
				yield break;
			}
			ControllableUnit.setControllableCreature(actor);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("GLT first person did not start");
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
					Log.Error("GLT failed: " + ex);
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
			Log.Info($"GLT {_pass} passed, {_fail} failed");
			Log.Info("GLT DONE");
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
			for (int i = 0; i < 20; i++)
			{
				if (land.SeaReach.Length == n)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.5f);
			}
			if (land.SeaReach.Length != n)
			{
				Check(ok: false, "the sea's distances from land are worked out");
				yield break;
			}
			int num = -1;
			int num2 = -1;
			float num3 = float.MaxValue;
			for (int j = 0; j < n; j++)
			{
				if ((land.Flags[j] & 1) != 0)
				{
					int num4 = Math.Min((int)land.SeaReach[j], 160);
					float num5 = (float)(j % w) - (float)w * 0.5f;
					float num6 = (float)(j / w) - (float)h * 0.5f;
					float num7 = num5 * num5 + num6 * num6;
					if (num4 > num2 || (num4 == num2 && num7 < num3))
					{
						num2 = num4;
						num3 = num7;
						num = j;
					}
				}
			}
			Check(num >= 0 && num2 >= 64, "the test world has open sea", $"{(float)num2 / 8f:F1} tiles from land");
			if (num < 0)
			{
				yield break;
			}
			Vector2 eye = new Vector2((float)(num % w) + 0.5f, (float)(num / w) + 0.5f);
			Vec3 lightDir = land.LightDir;
			float num8 = Mathf.Atan2(lightDir.Y, lightDir.X);
			List<Spot> list = new List<Spot>
			{
				new Spot
				{
					Name = "deck",
					Eye = eye,
					Height = 1.3f,
					Yaw = num8,
					Pitch = -6f
				},
				new Spot
				{
					Name = "above",
					Eye = eye,
					Height = 16f,
					Yaw = num8,
					Pitch = -45f
				}
			};
			Log.Info($"GLT eye {eye.x:F1},{eye.y:F1}, toward the sun {num8 * 57.29578f:F0} deg (light {lightDir.X:F2},{lightDir.Y:F2},{lightDir.Z:F2})");
			Settings s = _mod.Settings;
			Config.paused = true;
			_mod.Renderer.SeaWaves = false;
			float now = Mathf.Floor(Time.unscaledTime) + 0.25f;
			foreach (Spot sp in list)
			{
				Aim(sp);
				bool[][][] masks = new bool[2][][];
				int width = 0;
				int height = 0;
				for (int i = 0; i < 2; i++)
				{
					s.GpuRender = i == 1;
					string tag = "glt_" + sp.Name + "_" + ((i == 1) ? "gpu" : "cpu");
					masks[i] = new bool[13][];
					for (int k = 0; k < 13; k++)
					{
						_mod.HeldTime = now + (float)k * 0.04f;
						for (int l = 0; l < ((k == 0) ? 12 : 4); l++)
						{
							yield return null;
						}
						Color32[] array = Grab(out width, out height);
						if (array == null)
						{
							Check(ok: false, sp.Name + ": the " + Which(i) + "'s picture reads");
							break;
						}
						masks[i][k] = Glitter(array, width, height, sp.Name == "deck");
						if (k == 0 || k == 6 || k == 12)
						{
							_mod.SaveFrame($"{tag}_{k}.png");
						}
					}
					bool[][] array2 = masks[i];
					if (array2[12] == null)
					{
						continue;
					}
					int num9 = 0;
					for (int m = 0; m < array2[0].Length; m++)
					{
						int num10 = 0;
						for (int num11 = 0; num11 < 13; num11++)
						{
							if (array2[num11][m])
							{
								num10++;
							}
						}
						if (num10 >= 12)
						{
							num9++;
							for (int num12 = 0; num12 < 13; num12++)
							{
								array2[num12][m] = false;
							}
						}
					}
					Log.Info($"GLT {sp.Name} {Which(i)}: {num9} pixels white throughout, left out");
					float num13 = 0f;
					for (int num14 = 0; num14 < 13; num14++)
					{
						num13 += (float)Count(array2[num14]) / (float)array2[num14].Length;
					}
					num13 /= 13f;
					Check(num13 > 0.0005f, sp.Name + ": the " + Which(i) + " draws glitter", $"{num13 * 100f:F2}% of the picture");
					float num15 = 1f;
					float num16 = 0f;
					for (int num17 = 0; num17 + 1 < 13; num17++)
					{
						float num18 = Near(array2[num17], array2[num17 + 1], width, height, 1);
						num15 = Mathf.Min(num15, num18);
						num16 += num18;
					}
					float num19 = num16 / 12f;
					Check(num15 >= 0.75f * num19, sp.Name + ": on the " + Which(i) + " no moment swaps most of the glitter at once", $"kept 0.04 s on: {num19 * 100f:F0}% on average, {num15 * 100f:F0}% at the least");
					if (sp.Name == "above")
					{
						Shifted(array2[0], array2[6], width, height, 14, out var still, out var moved, out var sx, out var sy);
						Check(moved >= still * 1.3f && (sx != 0 || sy != 0), "above: on the " + Which(i) + " the glitter rides on the waves", $"after {0.24f:F2} s {still * 100f:F0}% in place, {moved * 100f:F0}% moved {sx},{sy} px");
					}
				}
				bool[][] obj = masks[0];
				if (((obj != null) ? obj[0] : null) == null)
				{
					continue;
				}
				bool[][] obj2 = masks[1];
				if (((obj2 != null) ? obj2[0] : null) != null && masks[0][0].Length == masks[1][0].Length)
				{
					float num20 = 0f;
					float num21 = 0f;
					for (int num22 = 0; num22 < 13; num22 += 6)
					{
						num20 += Near(masks[1][num22], masks[0][num22], width, height, 2);
						num21 += Near(masks[0][num22], masks[1][num22], width, height, 2);
					}
					num20 /= 3f;
					num21 /= 3f;
					Check(num20 > 0.5f && num21 > 0.5f, sp.Name + ": the CPU's glitter and the GPU's in the same places", $"{num20 * 100f:F0}% of the GPU's by the CPU's, {num21 * 100f:F0}% of the CPU's by the GPU's");
				}
			}
			_mod.HeldTime = -1f;
			Config.paused = false;
		}

		private static string Which(int gpu)
		{
			if (gpu != 1)
			{
				return "CPU";
			}
			return "GPU";
		}

		private void Aim(Spot sp)
		{
			_mod.DebugEye = sp.Eye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = sp.Height;
			_mod.SetYaw(sp.Yaw);
			_mod.SetPitchDegrees(sp.Pitch);
		}

		private static bool[] Glitter(Color32[] px, int w, int h, bool lower)
		{
			bool[] array = new bool[px.Length];
			int num = (lower ? (h * 55 / 100) : h);
			for (int i = 0; i < num; i++)
			{
				for (int j = 0; j < w; j++)
				{
					Color32 color = px[i * w + j];
					array[i * w + j] = Math.Min(color.r, Math.Min(color.g, color.b)) > 175;
				}
			}
			return array;
		}

		private static int Count(bool[] m)
		{
			int num = 0;
			for (int i = 0; i < m.Length; i++)
			{
				if (m[i])
				{
					num++;
				}
			}
			return num;
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

		private static void Shifted(bool[] a, bool[] b, int w, int h, int r, out float still, out float moved, out int sx, out int sy)
		{
			int num = Count(a);
			still = (moved = 0f);
			sx = (sy = 0);
			if (num == 0)
			{
				return;
			}
			for (int i = -r; i <= r; i++)
			{
				for (int j = -r; j <= r; j++)
				{
					int num2 = 0;
					for (int k = Math.Max(0, -i); k < Math.Min(h, h - i); k++)
					{
						for (int l = Math.Max(0, -j); l < Math.Min(w, w - j); l++)
						{
							if (a[k * w + l] && b[(k + i) * w + l + j])
							{
								num2++;
							}
						}
					}
					float num3 = (float)num2 / (float)num;
					if (j == 0 && i == 0)
					{
						still = num3;
					}
					if (num3 > moved)
					{
						moved = num3;
						sx = j;
						sy = i;
					}
				}
			}
		}
	}
}
