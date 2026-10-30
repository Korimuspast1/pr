using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using FirstPerson.Gpu;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class RoomLightTest : MonoBehaviour
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
				king = KingTest.FoundKingdom(item, "human", 8, 2);
				if (king != null)
				{
					break;
				}
			}
			if (king == null)
			{
				Log.Error("ROOMLIGHT could not found a village");
				Finish(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(2f);
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(3f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("ROOMLIGHT first person did not start");
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
			List<Actor> people = new List<Actor>();
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple != king && simple.isAlive() && People.IsHumanoid(simple) && simple.kingdom == king.kingdom && people.Count < 3)
				{
					people.Add(simple);
				}
			}
			Settings s = _mod.Settings;
			Log.Info($"ROOMLIGHT {SystemInfo.graphicsDeviceName}, gpu {s.GpuRender}, look {s.GpuLook}, palette {s.Palette}, resolution {s.Resolution}, night_dark {s.NightDark}");
			List<(string, int, string)> list = new List<(string, int, string)>
			{
				("human", 3, "human_stone"),
				("human", 3, "human_timber"),
				("human", 1, null),
				("elf", 3, null),
				("orc", 3, null),
				("dwarf", 3, null),
				("human", 0, "tent_human")
			};
			if (options.Contains("quick"))
			{
				list.RemoveRange(2, list.Count - 2);
			}
			bool cpu = options.Contains("cpu");
			bool ab = options.Contains("ab");
			int savedMode = s.TimeMode;
			foreach (var r in list)
			{
				string text = r.Item3;
				if (text == null)
				{
					(text, _, _) = r;
				}
				string tag = $"{text}_{r.Item2}";
				int[] array = new int[2] { 1, 3 };
				for (int i = 0; i < array.Length; i++)
				{
					int mode = (s.TimeMode = array[i]);
					string when = ((mode == 1) ? "day" : "night");
					try
					{
						_mod.EnterRoomForTest(people, r.Item1, r.Item2, r.Item3);
					}
					catch (Exception arg)
					{
						Log.Error($"ROOMLIGHT {tag}: could not go in: {arg}");
						continue;
					}
					List<string> first = new List<string>();
					float t = 0f;
					for (int f = 0; f < 8; f++)
					{
						yield return null;
						float b = Time.unscaledDeltaTime * 1000f;
						t = Mathf.Max(t, b);
						first.Add(b.ToString("F0"));
					}
					yield return new WaitForSecondsRealtime(0.6f);
					if (!_mod.Interior.Inside || _mod.Interior.Room == null)
					{
						Log.Error("ROOMLIGHT " + tag + ": no room");
						continue;
					}
					Interior room = _mod.Interior.Room;
					float yaw0 = _mod.ViewYaw;
					if (mode == 1)
					{
						List<string> list2 = new List<string>();
						foreach (RoomLight light in room.Lights)
						{
							list2.Add(string.Format("({0:F2},{1:F2},{2:F2} r{3:F1}{4})", light.Light.X, light.Light.Y, light.Light.Z, light.Light.Radius, light.Fire ? " fire" : ""));
						}
						Log.Info(string.Format("ROOMLIGHT {0}: eye {1:F2},{2:F2} yaw {3:F0}, hearth {4:F2},{5:F2}, lamps {6}", tag, _mod.Interior.Eye.x, _mod.Interior.Eye.y, yaw0 * 57.29578f, room.HearthX, room.HearthY, string.Join(" ", list2)));
						Log.Info("ROOMLIGHT " + tag + ": " + _mod.GpuRoomShadowsForTest());
					}
					List<string> looks = new List<string>();
					(string, float, float)[] array2 = new(string, float, float)[5]
					{
						("door", 0f, -10f),
						("left", 1.2f, -10f),
						("right", -1.2f, -10f),
						("back", MathF.PI, -10f),
						("up", 0f, 60f)
					};
					for (int f = 0; f < array2.Length; f++)
					{
						var (view, num, pitchDegrees) = array2[f];
						_mod.SetYaw(yaw0 + num);
						_mod.SetPitchDegrees(pitchDegrees);
						yield return new WaitForSecondsRealtime(0.4f);
						string name = "roomlight_" + tag + "_" + when + "_" + view;
						Texture2D texture2D = Shot();
						if (texture2D != null)
						{
							Measure(texture2D, out var mean, out var dark, out var bright);
							looks.Add($"{view} L {mean:F0} dark {dark * 100f:F0}% bright {bright * 100f:F1}%");
							File.WriteAllBytes(Path.Combine(Log.Folder, name + ".png"), texture2D.EncodeToPNG());
							UnityEngine.Object.Destroy(texture2D);
						}
						if (ab)
						{
							GpuRenderer.OldRoomLightForTest = true;
							yield return new WaitForSecondsRealtime(0.3f);
							Texture2D texture2D2 = Shot();
							GpuRenderer.OldRoomLightForTest = false;
							if (texture2D2 != null)
							{
								Measure(texture2D2, out var mean2, out var dark2, out var bright2);
								looks.Add($"(old L {mean2:F0} dark {dark2 * 100f:F0}% bright {bright2 * 100f:F1}%)");
								File.WriteAllBytes(Path.Combine(Log.Folder, name + "_old.png"), texture2D2.EncodeToPNG());
								UnityEngine.Object.Destroy(texture2D2);
							}
						}
						if (cpu && view == "door")
						{
							s.GpuRender = false;
							yield return new WaitForSecondsRealtime(0.4f);
							_mod.SaveFrame(name + "_cpu.png");
							s.GpuRender = true;
						}
					}
					_mod.SetYaw(yaw0);
					_mod.SetPitchDegrees(-10f);
					yield return new WaitForSecondsRealtime(0.3f);
					List<float> means = new List<float>();
					int frames = 0;
					float sumMs = 0f;
					float start = Time.unscaledTime;
					while (Time.unscaledTime - start < 2.4f)
					{
						yield return null;
						frames++;
						sumMs += Time.unscaledDeltaTime * 1000f;
						if (frames % 4 == 0)
						{
							Texture2D texture2D3 = Shot();
							if (!(texture2D3 == null))
							{
								Measure(texture2D3, out var mean3, out var _, out var _);
								means.Add(mean3);
								UnityEngine.Object.Destroy(texture2D3);
							}
						}
					}
					float num2 = float.MaxValue;
					float num3 = 0f;
					float num4 = 0f;
					foreach (float item2 in means)
					{
						num2 = Mathf.Min(num2, item2);
						num3 = Mathf.Max(num3, item2);
						num4 += item2;
					}
					num4 /= (float)Mathf.Max(1, means.Count);
					float num5 = ((num4 > 0f) ? ((num3 - num2) / num4 * 100f) : 0f);
					Log.Info($"ROOMLIGHT {tag} {when}: {room.Meshes.Count} models, {room.Lights.Count} lights, {room.Windows.Count} windows; " + string.Format("first frames {0} ms (worst {1:F0}); {2:F0} fps; ", string.Join(" ", first), t, 1000f * (float)frames / Mathf.Max(1f, sumMs)) + string.Format("light swing {0:F1}% ({1} samples); {2}", num5, means.Count, string.Join("; ", looks)) + ((_mod.GpuRoomLightForTest != null) ? ("; " + _mod.GpuRoomLightForTest) : ""));
					_mod.LeaveRoomForTest();
					yield return new WaitForSecondsRealtime(0.4f);
				}
			}
			s.TimeMode = savedMode;
			if (options.Contains("perf"))
			{
				yield return Perf(people);
			}
			Finish(quit);
		}

		private IEnumerator Perf(List<Actor> people)
		{
			Settings s = _mod.Settings;
			int savedRate = Application.targetFrameRate;
			int savedSync = QualitySettings.vSyncCount;
			int savedMode = s.TimeMode;
			Application.targetFrameRate = 1000;
			QualitySettings.vSyncCount = 0;
			s.TimeMode = 3;
			_mod.EnterRoomForTest(people, "human", 3, "human_stone");
			yield return new WaitForSecondsRealtime(1f);
			_mod.SetPitchDegrees(-10f);
			Dictionary<bool, List<float>> times = new Dictionary<bool, List<float>>
			{
				{
					false,
					new List<float>()
				},
				{
					true,
					new List<float>()
				}
			};
			for (int round = 0; round < 6; round++)
			{
				bool old = (GpuRenderer.OldRoomLightForTest = (round & 1) == 1);
				yield return new WaitForSecondsRealtime(0.5f);
				float sum = 0f;
				int frames = 0;
				for (float t = 0f; t < 2.5f; t += Time.unscaledDeltaTime)
				{
					yield return null;
					sum += Time.unscaledDeltaTime * 1000f;
					frames++;
				}
				times[old].Add(sum / (float)Mathf.Max(1, frames));
			}
			GpuRenderer.OldRoomLightForTest = false;
			Log.Info("ROOMLIGHT perf stone house by night, uncapped: new " + Line(times[false]) + ", old " + Line(times[true]) + "; " + _mod.GpuRoomLightForTest);
			_mod.LeaveRoomForTest();
			Application.targetFrameRate = savedRate;
			QualitySettings.vSyncCount = savedSync;
			s.TimeMode = savedMode;
			string Line(List<float> l)
			{
				l.Sort();
				return string.Format("{0:F2} ms median ({1})", l[l.Count / 2], string.Join(" ", l.ConvertAll((float v) => v.ToString("F2"))));
			}
		}

		private Texture2D Shot()
		{
			byte[] array = _mod.ShownPngForTest();
			if (array == null)
			{
				return null;
			}
			Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
			texture2D.LoadImage(array);
			return texture2D;
		}

		private static void Measure(Texture2D t, out float mean, out float dark, out float bright)
		{
			Color32[] pixels = t.GetPixels32();
			double num = 0.0;
			int num2 = 0;
			int num3 = 0;
			Color32[] array = pixels;
			for (int i = 0; i < array.Length; i++)
			{
				Color32 color = array[i];
				float num4 = 0.3f * (float)(int)color.r + 0.59f * (float)(int)color.g + 0.11f * (float)(int)color.b;
				num += (double)num4;
				if (num4 < 40f)
				{
					num2++;
				}
				if (num4 > 215f)
				{
					num3++;
				}
			}
			int num5 = Math.Max(1, pixels.Length);
			mean = (float)(num / (double)num5);
			dark = (float)num2 / (float)num5;
			bright = (float)num3 / (float)num5;
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
			Log.Info("ROOMLIGHT DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
