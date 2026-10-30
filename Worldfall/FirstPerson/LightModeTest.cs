using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using FirstPerson.Gpu;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class LightModeTest : CivilTest
	{
		private const float BlockSeconds = 4f;

		private const int DropFrames = 15;

		protected override string Prefix => "LIGHT";

		protected override IEnumerator Body()
		{
			Settings s = Mod.Settings;
			int lightWas = s.Light;
			Note($"device {Device.Is} ({Device.Summary})");
			Check("acting as a phone (FIRSTPERSON_DEVICE=phone)", Device.OnPhone);
			int slot = 32;
			int num = Options.IndexOf("map", StringComparison.Ordinal);
			if (num >= 0)
			{
				int num2 = 0;
				int num3 = num + 3;
				while (num3 < Options.Length && char.IsDigit(Options[num3]))
				{
					num2 = num2 * 10 + (Options[num3++] - 48);
				}
				if (num2 > 0)
				{
					slot = num2;
				}
			}
			string text = Path.Combine(Path.Combine(Path.Combine(Application.persistentDataPath, "saves"), "save" + slot), "map.wbox");
			bool loaded = false;
			if (File.Exists(text))
			{
				try
				{
					SaveManager.loadMapFromBytes(File.ReadAllBytes(text));
					loaded = true;
				}
				catch (Exception ex)
				{
					Log.Error("LIGHT could not load " + text + ": " + ex.Message);
				}
				float t = 0f;
				while (loaded && t < 90f)
				{
					yield return CivilTest.Wait(0.5f);
					bool flag = true;
					try
					{
						flag = SmoothLoader.isLoading();
					}
					catch
					{
					}
					if (!flag && t > 3f)
					{
						break;
					}
					t += 0.5f;
				}
				Config.autosaves = false;
			}
			Config.paused = false;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Note(string.Format("world: {0}, {1}x{2} tiles, {3} creatures", loaded ? ("save " + slot) : "the starting map", MapBox.width, MapBox.height, World.world.units.getSimpleList().Count));
			yield return CivilTest.Wait(6f);
			WorldTile centre;
			int crowd;
			Actor host = BusiestTown(out centre, out crowd);
			if (host == null)
			{
				host = Village("human", 20, 6);
				centre = host?.current_tile;
				crowd = 26;
				yield return CivilTest.Wait(4f);
			}
			if (Check("a town to stand in", host != null && centre != null))
			{
				Note($"town at {centre.x},{centre.y}: {crowd} creatures within 15 tiles; possessing {WorldBoxMod.Describe(host)}");
				yield return Possess(host, 3f);
				if (Check("first person on them", InControl(host)))
				{
					yield return Part("on by itself on a phone", Auto(s));
					yield return Part("the notice", Notice(s));
					yield return Part("the player's own values kept", Kept(s));
					yield return Part("frame times off and on", Timing(s, host, centre));
					s.Light = lightWas;
					yield return null;
				}
			}
		}

		private IEnumerator Auto(Settings s)
		{
			s.Light = 0;
			yield return null;
			yield return null;
			Check("Light mode on (Auto, a phone)", LightMode.On);
			Check("light values in force", s.Resolution == 0 && s.GpuLook == 0 && s.Shadows == 0 && s.PixelDistance == 0 && s.WindowView == 0 && !s.SpatialSound && s.BiomeFx == 0 && s.DetailDistance <= 40f && s.CreatureDistance <= 80f && s.DrawDistance <= 140f && s.FarModels == 0, $"res {s.Resolution}, finish {s.GpuLook}, shadows {s.Shadows}, pixels {s.PixelDistance}, windows {s.WindowView}, 3d sound {s.SpatialSound}, biome {s.BiomeFx}, detail {s.DetailDistance}, creatures {s.CreatureDistance}, draw {s.DrawDistance}");
			Check("no lamp shadow cubes, sea waves or lying snow", GpuRenderer.RoomLights.Get("room_shadows") <= 0f && !Mod.Renderer.SeaWaves && SnowCover.Hidden);
		}

		private IEnumerator Notice(Settings s)
		{
			for (float t = 0f; t < 12f; t += 0.25f)
			{
				if (s.LightTold)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Check("the phone notice shown once", s.LightTold, Mod.ToastText);
			Check("its text names Light mode and F2", Mod.ToastText != null && Mod.ToastText.Contains("Light mode") && Mod.ToastText.Contains("F2"), Mod.ToastText);
		}

		private IEnumerator Kept(Settings s)
		{
			s.Save();
			string text = "";
			try
			{
				text = File.ReadAllText(Path.Combine(Log.Folder, "settings.txt"));
			}
			catch
			{
			}
			int num = OwnInt(text, "shadows");
			int num2 = OwnInt(text, "gpu_look");
			Note($"settings.txt: shadows={num}, gpu_look={num2}; live shadows {s.Shadows}, finish {s.GpuLook}");
			Check("settings.txt keeps the player's shadows and finish", num == 2 && num2 == 1, $"shadows={num}, gpu_look={num2}");
			Check("still light after saving", s.Shadows == 0 && s.GpuLook == 0);
			s.Light = 2;
			yield return null;
			yield return null;
			Check("Off gives the player's settings back", !LightMode.On && s.Shadows == 2 && s.GpuLook == 1, $"shadows {s.Shadows}, finish {s.GpuLook}");
			Check("lamp shadow cubes and sea waves back", GpuRenderer.RoomLights.Get("room_shadows") > 0f && Mod.Renderer.SeaWaves && !SnowCover.Hidden);
			s.Light = 1;
			yield return null;
			yield return null;
			Check("On again", LightMode.On && s.Shadows == 0);
			s.Shadows = 3;
			yield return null;
			s.Light = 2;
			yield return null;
			yield return null;
			Check("a value changed while on stays the player's", s.Shadows == 3 && s.GpuLook == 1, $"shadows {s.Shadows}");
			s.Shadows = 2;
			yield return null;
		}

		private static int OwnInt(string text, string key)
		{
			string[] array = text.Split('\n');
			for (int i = 0; i < array.Length; i++)
			{
				string text2 = array[i].Trim();
				if (text2.StartsWith(key + "=", StringComparison.Ordinal) && int.TryParse(text2.Substring(key.Length + 1), out var result))
				{
					return result;
				}
			}
			return -1;
		}

		private IEnumerator Timing(Settings s, Actor host, WorldTile centre)
		{
			bool vsync = s.VSync;
			int cap = s.FrameCap;
			s.VSync = false;
			s.FrameCap = 0;
			List<float> off = new List<float>();
			List<float> on = new List<float>();
			Vector2 vector = new Vector2((float)centre.x + 0.5f, (float)centre.y + 0.5f);
			float yaw0 = Mathf.Atan2(vector.y - host.current_position.y, vector.x - host.current_position.x);
			for (int block = 0; block < 8; block++)
			{
				bool light = block % 2 == 1;
				s.Light = (light ? 1 : 2);
				List<float> times = new List<float>();
				int frame = 0;
				float t = 0f;
				while (t < 4f)
				{
					if (QualitySettings.vSyncCount != 0)
					{
						QualitySettings.vSyncCount = 0;
					}
					if (Application.targetFrameRate != -1)
					{
						Application.targetFrameRate = -1;
					}
					Mod.PinDetailForTest(1f, 1f);
					Mod.SetYaw(yaw0 + Mathf.Sin(t * 0.8f) * 0.6f);
					if (Time.frameCount % 60 == 0)
					{
						try
						{
							if (host.isAlive())
							{
								host.setNutrition(host.getMaxNutrition());
								if (host.getHealth() < host.getMaxHealth())
								{
									host.restoreHealth(host.getMaxHealth());
								}
							}
						}
						catch
						{
						}
					}
					yield return null;
					float unscaledDeltaTime = Time.unscaledDeltaTime;
					t += unscaledDeltaTime;
					int num = frame + 1;
					frame = num;
					if (num > 15)
					{
						times.Add(unscaledDeltaTime * 1000f);
					}
				}
				if (!InControl(host))
				{
					Check("still in first person while timing", ok: false);
					break;
				}
				times.Sort();
				float num2 = 0f;
				foreach (float item in times)
				{
					num2 += item;
				}
				num2 /= (float)Mathf.Max(1, times.Count);
				float num3 = ((times.Count > 0) ? times[times.Count / 2] : 0f);
				float num4 = ((times.Count > 0) ? times[Mathf.Min(times.Count - 1, times.Count * 95 / 100)] : 0f);
				(light ? on : off).AddRange(times);
				Note(string.Format("block {0} {1}: {2} frames, mean {3:F1} ms, p50 {4:F1}, p95 {5:F1}; res {6}, finish {7}, shadows {8}, detail {9:F0}, creatures {10:F0}, draw {11:F0}", block, light ? "ON " : "OFF", times.Count, num2, num3, num4, WorldBoxMod.ResolutionWidths[s.Resolution], s.GpuLook, s.Shadows, s.DetailDistance, s.CreatureDistance, s.DrawDistance));
			}
			s.Light = 2;
			yield return null;
			s.VSync = vsync;
			s.FrameCap = cap;
			off.Sort();
			on.Sort();
			float num5 = ((off.Count > 0) ? off[off.Count / 2] : 0f);
			float num6 = ((on.Count > 0) ? on[on.Count / 2] : 0f);
			float num7 = 0f;
			float num8 = 0f;
			foreach (float item2 in off)
			{
				num7 += item2;
			}
			foreach (float item3 in on)
			{
				num8 += item3;
			}
			num7 /= (float)Mathf.Max(1, off.Count);
			num8 /= (float)Mathf.Max(1, on.Count);
			Note($"summary: Off p50 {num5:F1} ms mean {num7:F1} ({off.Count} frames, {1000f / Mathf.Max(0.1f, num7):F0} fps); On p50 {num6:F1} ms mean {num8:F1} ({on.Count} frames, {1000f / Mathf.Max(0.1f, num8):F0} fps)");
			Check("frames faster with Light mode on", on.Count > 0 && off.Count > 0 && num6 < num5, $"{num6:F1} vs {num5:F1} ms");
		}

		private static Actor BusiestTown(out WorldTile centre, out int crowd)
		{
			centre = null;
			crowd = 0;
			Actor result = null;
			List<Actor> simpleList = World.world.units.getSimpleList();
			foreach (City item in World.world.cities.list)
			{
				if (item == null || !item.isAlive())
				{
					continue;
				}
				WorldTile worldTile = null;
				try
				{
					worldTile = item.getTile();
				}
				catch
				{
				}
				if (worldTile == null)
				{
					continue;
				}
				int num = 0;
				Actor actor = null;
				Vector2 vector = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f);
				foreach (Actor item2 in simpleList)
				{
					if (item2 == null || !item2.isAlive() || (item2.current_position - vector).sqrMagnitude > 225f)
					{
						continue;
					}
					num++;
					if (item2.city == item && People.IsHumanoid(item2) && !item2.is_inside_building && !item2.is_inside_boat && HouseInterior.HouseAt(item2) == null)
					{
						bool flag = false;
						try
						{
							flag = item2.isAdult();
						}
						catch
						{
						}
						if (flag && actor == null)
						{
							actor = item2;
						}
					}
				}
				if (actor != null && num > crowd)
				{
					crowd = num;
					result = actor;
					centre = worldTile;
				}
			}
			return result;
		}
	}
}
