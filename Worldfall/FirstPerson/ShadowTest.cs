using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Gpu;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class ShadowTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

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
				king = KingTest.FoundKingdom(item, "human", 10, 2);
				if (king != null)
				{
					Log.Info($"SHADOWTUNE village at {item.x},{item.y}");
					break;
				}
			}
			if (king == null)
			{
				Log.Error("SHADOWTUNE could not found a village");
				Finish(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(3f);
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(3f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("SHADOWTUNE first person did not start");
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
			(int Shadows, float ShadowDistance, int ShadowSoftness, float ShadowDarkness, int TimeMode, bool GpuRender) saved = (Shadows: s.Shadows, ShadowDistance: s.ShadowDistance, ShadowSoftness: s.ShadowSoftness, ShadowDarkness: s.ShadowDarkness, TimeMode: s.TimeMode, GpuRender: s.GpuRender);
			int savedRate = Application.targetFrameRate;
			int savedSync = QualitySettings.vSyncCount;
			Application.targetFrameRate = 1000;
			QualitySettings.vSyncCount = 0;
			s.TimeMode = 0;
			s.GpuRender = true;
			float savedHour = _mod.Day.TimeOfDay;
			Config.paused = true;
			_mod.Day.TimeOfDay = 0.64f;
			yield return new WaitForSecondsRealtime(0.5f);
			Vector2 current_position = king.current_position;
			Vector3 vector = new Vector3(_mod.Day.Air.Light.X, _mod.Day.Air.Light.Y, _mod.Day.Air.Light.Z);
			Vector2 sun = new Vector2(vector.x, vector.y).normalized;
			Vector2 vector2 = new Vector2(0f - sun.y, sun.x);
			WorldTile worldTile = Dry(current_position - sun * 12f, 6, 60);
			List<Building> list = new List<Building>();
			if (worldTile != null)
			{
				Clear(worldTile, 14);
			}
			if (worldTile != null)
			{
				int[] array = new int[3] { -1, 0, 1 };
				foreach (int num in array)
				{
					WorldTile tile = World.world.GetTile(worldTile.x + Mathf.RoundToInt(vector2.x * (float)num * 5f), worldTile.y + Mathf.RoundToInt(vector2.y * (float)num * 5f));
					Building building = Build((num == 0) ? "house_human_3" : "house_human_1", tile);
					if (building != null)
					{
						list.Add(building);
					}
				}
			}
			Vector2 house = ((list.Count > 1) ? list[1].current_position : ((list.Count > 0) ? list[0].current_position : current_position));
			Log.Info($"SHADOWTUNE {list.Count} houses at {house}, sun {vector}, {SystemInfo.graphicsDeviceName}, {_mod.Renderer.Width}x{_mod.Renderer.Height}");
			yield return new WaitForSecondsRealtime(1f);
			Vector2 vector3 = -sun;
			Vector2 vector4 = Turn(vector3, 35f);
			Vector2 vector5 = Turn(vector3, 90f);
			Vector2 vector6 = house + vector3 * 1.8f;
			(string, Vector2, Vector2, float)[] array2 = new(string, Vector2, Vector2, float)[3]
			{
				("street", house - vector4 * 10f, vector4, -9f),
				("across", house + vector3 * 2.5f - vector5 * 6f, vector5, -18f),
				("foot", vector6 - vector5 * 2.6f, vector5, -38f)
			};
			bool acne = options.Contains("acne");
			if (acne)
			{
				Vector2 vector7 = Turn(sun, 55f);
				Vector2 vector8 = Turn(sun, -65f);
				array2 = new(string, Vector2, Vector2, float)[3]
				{
					("wall", house + vector7 * 4.5f, -vector7, 8f),
					("roofs", house + vector8 * 9f, -vector8, 4f),
					("street", house - vector4 * 10f, vector4, -9f)
				};
			}
			List<(string Tag, int Quality, float Distance, int Soft, float Dark, bool Gpu)> configs = new List<(string, int, float, int, float, bool)>
			{
				("medium_hard", 2, 64f, 0, 1f, true),
				("off", 0, 64f, 0, 1f, true),
				("low", 1, 64f, 0, 1f, true),
				("high", 3, 64f, 0, 1f, true),
				("ultra", 4, 64f, 0, 1f, true),
				("medium_soft", 2, 64f, 1, 1f, true),
				("medium_natural", 2, 64f, 2, 1f, true),
				("high_natural", 3, 96f, 2, 1f, true),
				("ultra_natural", 4, 128f, 2, 1f, true),
				("high_soft", 3, 96f, 1, 1f, true),
				("dark50", 2, 64f, 0, 0.5f, true),
				("far128", 2, 128f, 0, 1f, true),
				("high_far160", 3, 160f, 0, 1f, true),
				("medium_hard_again", 2, 64f, 0, 1f, true)
			};
			if (acne)
			{
				configs = new List<(string, int, float, int, float, bool)>
				{
					("medium_hard", 2, 64f, 0, 1f, true),
					("medium_hard_192", 2, 192f, 0, 1f, true),
					("medium_soft_192", 2, 192f, 1, 1f, true),
					("medium_natural_192", 2, 192f, 2, 1f, true),
					("high_hard_192", 3, 192f, 0, 1f, true),
					("low_hard_128", 1, 128f, 0, 1f, true),
					("medium_hard_again", 2, 64f, 0, 1f, true)
				};
			}
			if (options.Contains("cpu"))
			{
				configs.AddRange(new(string, int, float, int, float, bool)[4]
				{
					("cpu_medium", 2, 64f, 0, 1f, false),
					("cpu_dark50", 2, 64f, 0, 0.5f, false),
					("cpu_ultra", 4, 128f, 0, 1f, false),
					("cpu_off", 0, 64f, 0, 1f, false)
				});
			}
			Dictionary<string, Color32[]> pictures = new Dictionary<string, Color32[]>();
			Dictionary<string, float> fps = new Dictionary<string, float>();
			(string Tag, Vector2 Eye, Vector2 Dir, float Pitch)[] array3 = array2;
			for (int j = 0; j < array3.Length; j++)
			{
				(string Tag, Vector2 Eye, Vector2 Dir, float Pitch) v = array3[j];
				_mod.DebugEyeSet = true;
				_mod.DebugEye = v.Eye;
				_mod.DebugEyeHeight = 0f;
				_mod.SetYaw(Mathf.Atan2(v.Dir.y, v.Dir.x));
				_mod.SetPitchDegrees(v.Pitch);
				for (int round = 0; round < 2; round++)
				{
					foreach (var c in configs)
					{
						s.Shadows = c.Quality;
						s.ShadowDistance = c.Distance;
						s.ShadowSoftness = c.Soft;
						s.ShadowDarkness = c.Dark;
						s.GpuRender = c.Gpu;
						Config.paused = true;
						_mod.Day.TimeOfDay = 0.64f;
						yield return new WaitForSecondsRealtime(0.6f);
						string key = v.Tag + " " + c.Tag;
						if (round == 0)
						{
							_mod.SaveFrame("shadowtune_" + v.Tag + "_" + c.Tag + ".png");
							pictures[key] = Grab();
						}
						float t = Time.unscaledTime;
						int frames = 0;
						while (Time.unscaledTime - t < 1.5f)
						{
							yield return null;
							frames++;
						}
						float num2 = (float)frames / Mathf.Max(0.01f, Time.unscaledTime - t);
						fps[key] = ((round == 0) ? num2 : Mathf.Max(fps[key], num2));
						if (round == 1)
						{
							Log.Info(string.Format("SHADOWTUNE {0} {1}: {2:F0} fps, {3}", v.Tag, c.Tag, fps[key], c.Gpu ? Map() : ("CPU map " + _mod.Renderer.ShadowMs.ToString("F1") + " ms")));
						}
					}
				}
				if (acne)
				{
					s.Shadows = 2;
					s.ShadowDistance = 192f;
					s.ShadowSoftness = 0;
					s.ShadowDarkness = 1f;
					s.GpuRender = true;
					Vector2 side = new Vector2(0f - v.Dir.y, v.Dir.x);
					for (int round = 0; round < 4; round++)
					{
						_mod.DebugEye = v.Eye + side * (0.15f * (float)round);
						_mod.DebugEyeHeight = 0.07f * (float)round;
						Config.paused = true;
						_mod.Day.TimeOfDay = 0.64f;
						yield return new WaitForSecondsRealtime(0.6f);
						_mod.SaveFrame($"shadowtune_{v.Tag}_step{round}.png");
					}
					_mod.DebugEye = v.Eye;
					_mod.DebugEyeHeight = 0f;
					continue;
				}
				Color32[] array4 = pictures[v.Tag + " medium_hard"];
				Color32[] b = pictures[v.Tag + " medium_hard_again"];
				int num3 = Diff(array4, b);
				Log.Info($"SHADOWTUNE {v.Tag}: the defaults twice differ in {num3} pixels");
				if (num3 > array4.Length / 100)
				{
					Log.Error($"SHADOWTUNE {v.Tag}: the defaults drew differently twice ({num3} pixels)");
				}
				foreach (var item2 in configs)
				{
					if (!item2.Tag.StartsWith("medium_hard") && !item2.Tag.StartsWith("cpu"))
					{
						int num4 = Diff(array4, pictures[v.Tag + " " + item2.Tag]);
						Log.Info($"SHADOWTUNE {v.Tag} {item2.Tag}: {num4} pixels differ from the defaults ({(float)num4 * 100f / (float)array4.Length:F2}%)");
						if (num4 <= num3 * 2 + 50 && item2.Tag != "high_far160" && item2.Tag != "far128")
						{
							Log.Error("SHADOWTUNE " + v.Tag + " " + item2.Tag + ": no change from the defaults");
						}
					}
				}
			}
			_mod.DebugEyeSet = false;
			Config.paused = false;
			(s.Shadows, s.ShadowDistance, s.ShadowSoftness, s.ShadowDarkness, s.TimeMode, s.GpuRender) = saved;
			_mod.Day.TimeOfDay = savedHour;
			Application.targetFrameRate = savedRate;
			QualitySettings.vSyncCount = savedSync;
			Finish(quit);
			Vector2 Turn(Vector2 vector9, float deg)
			{
				float f = deg * (MathF.PI / 180f);
				float num5 = Mathf.Cos(f);
				float num6 = Mathf.Sin(f);
				return new Vector2(vector9.x * num5 - vector9.y * num6, vector9.x * num6 + vector9.y * num5);
			}
		}

		private string Map()
		{
			GpuRenderer gpuRendererForTest = _mod.GpuRendererForTest;
			if (gpuRendererForTest != null)
			{
				return $"{gpuRendererForTest.ShadowSize} texels, quality {gpuRendererForTest.ShadowQuality}, reach {gpuRendererForTest.ShadowReach:F0}";
			}
			return "none";
		}

		private Color32[] Grab()
		{
			try
			{
				Texture2D texture2D = new Texture2D(2, 2);
				texture2D.LoadImage(_mod.ShownPngForTest());
				Color32[] pixels = texture2D.GetPixels32();
				UnityEngine.Object.Destroy(texture2D);
				return pixels;
			}
			catch (Exception ex)
			{
				Log.Error("SHADOWTUNE grab: " + ex.Message);
				return new Color32[0];
			}
		}

		private static int Diff(Color32[] a, Color32[] b)
		{
			if (a.Length != b.Length)
			{
				return Math.Max(a.Length, b.Length);
			}
			int num = 0;
			for (int i = 0; i < a.Length; i++)
			{
				if (Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b) > 12)
				{
					num++;
				}
			}
			return num;
		}

		private static void Clear(WorldTile at, int r)
		{
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item == null || !item.isAlive() || item.current_tile == null)
				{
					continue;
				}
				int num = item.current_tile.x - at.x;
				int num2 = item.current_tile.y - at.y;
				try
				{
					if (num * num + num2 * num2 <= r * r && !item.isKingdomCiv())
					{
						item.removeBuildingFinal();
					}
				}
				catch
				{
				}
			}
		}

		private static Building Build(string id, WorldTile at)
		{
			if (at == null)
			{
				return null;
			}
			try
			{
				foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
				{
					if (item != null && item.isAlive() && item.current_tile != null && !item.isKingdomCiv())
					{
						int num = item.current_tile.x - at.x;
						int num2 = item.current_tile.y - at.y;
						if (num * num + num2 * num2 <= 9)
						{
							item.removeBuildingFinal();
						}
					}
				}
				Building building = World.world.buildings.addBuilding(id, at, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				building?.completeConstruction();
				return building;
			}
			catch (Exception ex)
			{
				Log.Error("SHADOWTUNE could not build " + id + ": " + ex.Message);
				return null;
			}
		}

		private static WorldTile Dry(Vector2 c, int r, int reach)
		{
			for (int i = 0; i < reach; i += 2)
			{
				for (int j = 0; j < 32; j++)
				{
					float f = (float)j / 32f * MathF.PI * 2f;
					int num = (int)(c.x + Mathf.Cos(f) * (float)i);
					int num2 = (int)(c.y + Mathf.Sin(f) * (float)i);
					bool flag = true;
					for (int k = -r; k <= r + 1 && flag; k++)
					{
						for (int l = -r; l <= r && flag; l++)
						{
							WorldTile tile = World.world.GetTile(num + l, num2 + k);
							if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block || tile.Type.lava || (tile.building != null && tile.building.isKingdomCiv()))
							{
								flag = false;
							}
						}
					}
					if (flag)
					{
						return World.world.GetTile(num, num2);
					}
					if (i == 0)
					{
						break;
					}
				}
			}
			return null;
		}

		private static void Finish(bool quit)
		{
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("SHADOWTUNE DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
