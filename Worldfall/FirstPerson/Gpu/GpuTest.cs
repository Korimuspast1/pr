using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using FirstPerson.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPerson.Gpu
{
	internal sealed class GpuTest : MonoBehaviour
	{
		private struct View
		{
			public string Name;

			public Vector2 Eye;

			public float Height;

			public float Yaw;

			public float Pitch;
		}

		private int _pass;

		private int _fail;

		private WorldBoxMod _mod;

		private readonly List<View> _views = new List<View>();

		internal static int TuftsResolution = 3;

		private IEnumerator Bench(string arg)
		{
			int frames = 60;
			string tag = "";
			string[] array = arg.Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string text in array)
			{
				if (int.TryParse(text, out var result))
				{
					frames = Math.Max(1, result);
				}
				else if (text.StartsWith("cull="))
				{
					GpuRenderer.ModelCull = ((text == "cull=front") ? CullMode.Front : ((text == "cull=back") ? CullMode.Back : CullMode.Off));
				}
				else if (text.StartsWith("sky="))
				{
					GpuRenderer.SkyLast = text == "sky=last";
				}
				else
				{
					tag = text;
				}
			}
			Log.Info(string.Format("GPU bench {0}: models culled {1}, sky {2}", tag, GpuRenderer.ModelCull, GpuRenderer.SkyLast ? "last" : "first"));
			Settings s = _mod.Settings;
			int savedRes = s.Resolution;
			s.GpuRender = true;
			s.GpuLook = 1;
			s.Resolution = 3;
			float sum = 0f;
			foreach (View v in _views)
			{
				Aim(v);
				for (int j = 0; j < 12; j++)
				{
					yield return null;
				}
				List<float> runs = new List<float>();
				for (int j = 0; j < 5; j++)
				{
					GpuRenderer.BenchFrames = frames;
					for (int k = 0; k < 3; k++)
					{
						yield return null;
					}
					if (GpuRenderer.BenchFrames == 0 && GpuRenderer.BenchMs >= 0f)
					{
						runs.Add(GpuRenderer.BenchMs);
					}
				}
				GpuRenderer.BenchFrames = 0;
				runs.Sort();
				if (runs.Count == 0)
				{
					Log.Error("GPU bench " + v.Name + ": no time taken");
					continue;
				}
				sum += runs[0];
				Log.Info($"GPU bench {v.Name} {tag}: best {runs[0]:F2} ms, median {runs[runs.Count / 2]:F2} ms a frame on the graphics card ({frames} frames x {runs.Count})");
				_mod.SaveFrame("bench_" + v.Name + ((tag.Length > 0) ? ("_" + tag) : "") + ".png");
			}
			Log.Info($"GPU bench all {tag}: {sum:F2} ms (best per view, summed)");
			s.Resolution = savedRes;
		}

		private IEnumerator Copies(Vector2 town)
		{
			float a = float.Parse("16777216", CultureInfo.InvariantCulture);
			float b = float.Parse("1", CultureInfo.InvariantCulture);
			Log.Info("COPIES float math: " + ((FloatSum(a, b) == 0f) ? "float32" : "kept as double"));
			Settings s = _mod.Settings;
			float savedDraw = s.DrawDistance;
			float savedDetail = s.DetailDistance;
			float savedCreatures = s.CreatureDistance;
			float savedZoom = s.GodViewZoom;
			bool savedGod = s.GodView;
			s.DrawDistance = 1200f;
			s.DetailDistance = 240f;
			s.CreatureDistance = 1200f;
			s.GpuRender = true;
			s.Resolution = 2;
			Config.paused = true;
			foreach (View v in _views)
			{
				_mod.DebugEye = v.Eye;
				_mod.DebugEyeSet = true;
				_mod.DebugEyeHeight = ((v.Height > 0f) ? v.Height : 0f);
				_mod.SetYaw(v.Yaw);
				_mod.SetPitchDegrees(v.Pitch);
				for (int i = 0; i < 40; i++)
				{
					yield return null;
				}
				CopiesHere("fp " + v.Name);
				yield return null;
			}
			_mod.DebugEyeSet = false;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch (Exception ex)
			{
				Log.Error("COPIES letting go: " + ex.Message);
			}
			s.GodViewZoom = 150f;
			s.GodView = true;
			float[] array = new float[3] { 34f, 70f, 111f };
			for (int i = 0; i < array.Length; i++)
			{
				float zoom = array[i];
				for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
				{
					HoldCamera(town, zoom);
					yield return null;
				}
				CopiesHere("god " + zoom.ToString("0"));
				yield return null;
			}
			s.GodView = savedGod;
			s.GodViewZoom = savedZoom;
			s.DrawDistance = savedDraw;
			s.DetailDistance = savedDetail;
			s.CreatureDistance = savedCreatures;
		}

		private void CopiesHere(string where)
		{
			GpuRenderer gpuRendererForTest = _mod.GpuRendererForTest;
			if (gpuRendererForTest == null || !gpuRendererForTest.CopiesNoted)
			{
				Check(where + ": a frame drawn on the card", ok: false);
				return;
			}
			FrameRenderer renderer = _mod.Renderer;
			List<VoxelObject> voxels = _mod.SceneForTest.Voxels;
			Log.Info("COPIES " + where + ": " + gpuRendererForTest.Stats);
			Log.Info("COPIES " + where + ": " + gpuRendererForTest.CopyStats(renderer));
			string text = gpuRendererForTest.CompareCopies(renderer, voxels);
			Check(where + ": the same draws as before, to the bit", text == null, text);
			Log.Info("COPIES " + where + " ms: " + gpuRendererForTest.ProfileCopies(renderer, voxels, 15));
			List<Billboard> billboards = _mod.SceneForTest.Billboards;
			bool classic = _mod.Settings.GpuLook == 0;
			gpuRendererForTest.RedrawForTest(renderer, billboards, voxels, classic, skipUnseen: false);
			byte[] a = gpuRendererForTest.RedrawForTest(renderer, billboards, voxels, classic, skipUnseen: false);
			byte[] b = gpuRendererForTest.RedrawForTest(renderer, billboards, voxels, classic, skipUnseen: true);
			byte[] b2 = gpuRendererForTest.RedrawForTest(renderer, billboards, voxels, classic, skipUnseen: false);
			int num = Differ(a, b2);
			int num2 = Differ(a, b);
			Check(where + ": drawn twice alike (so the next check can judge)", num == 0, num + " bytes differ");
			Check(where + ": the same picture with the unseen copies left out", num2 == 0, num2 + " bytes differ, " + gpuRendererForTest.Stats);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static float FloatSum(float a, float b)
		{
			return a + b - a;
		}

		private static int Differ(byte[] a, byte[] b)
		{
			if (a == null || b == null)
			{
				return -1;
			}
			if (a.Length != b.Length)
			{
				return int.MaxValue;
			}
			int num = 0;
			for (int i = 0; i < a.Length; i++)
			{
				if (a[i] != b[i])
				{
					num++;
				}
			}
			return num;
		}

		private static void HoldCamera(Vector2 at, float zoom)
		{
			try
			{
				Camera camera = World.world.camera;
				camera.transform.position = new Vector3(at.x, at.y, camera.transform.position.z);
				camera.orthographicSize = zoom;
				if (World.world.move_camera != null)
				{
					World.world.move_camera.setTargetZoom(zoom);
				}
				World.world.setZoomOrthographic(zoom);
			}
			catch (Exception ex)
			{
				Log.Error("COPIES camera: " + ex.Message);
			}
		}

		private IEnumerator CpuLook(string tag)
		{
			Settings s = _mod.Settings;
			int savedRes = s.Resolution;
			int savedLook = s.GpuLook;
			bool savedGpu = s.GpuRender;
			if (_mod.Interior.Inside)
			{
				_mod.LeaveRoomForTest();
				yield return new WaitForSecondsRealtime(1f);
			}
			string suffix = ((tag.Length > 0) ? ("_" + tag) : "");
			if (_views.Count > 2 && !_views.Exists((View x) => x.Name == "high"))
			{
				View view = _views[2];
				_views.Add(new View
				{
					Name = "high",
					Eye = view.Eye,
					Height = 24f,
					Yaw = view.Yaw,
					Pitch = -6f
				});
				_views.Add(new View
				{
					Name = "level",
					Eye = view.Eye,
					Height = view.Height,
					Yaw = view.Yaw + MathF.PI / 2f,
					Pitch = 0f
				});
			}
			Log.Info($"CPULOOK draw distance {s.DrawDistance:F0}, detail {s.DetailDistance:F0}, pixel distance {s.PixelDistance}, palette {s.Palette}");
			int ch;
			int savedPx;
			foreach (View v in _views)
			{
				_mod.DebugEye = v.Eye;
				_mod.DebugEyeSet = true;
				_mod.DebugEyeHeight = ((v.Height > 0f) ? v.Height : 0f);
				_mod.SetYaw(v.Yaw);
				_mod.SetPitchDegrees(v.Pitch);
				for (int i = 0; i < 3; i++)
				{
					yield return null;
				}
				if (_mod.Interior.Inside)
				{
					_mod.LeaveRoomForTest();
					yield return new WaitForSecondsRealtime(0.5f);
				}
				s.Resolution = 1;
				s.GpuRender = false;
				for (int i = 0; i < 8; i++)
				{
					yield return null;
				}
				_mod.SaveFrame("cl_" + v.Name + "_cpu" + suffix + ".png");
				int cw;
				Color32[] cpu = ReadShown(out cw, out ch);
				s.GpuRender = true;
				s.GpuLook = 0;
				int[] array = new int[3] { 1, 2, 3 };
				for (int i = 0; i < array.Length; i++)
				{
					int res = (s.Resolution = array[i]);
					for (int i2 = 0; i2 < 8; i2++)
					{
						yield return null;
					}
					int num = WorldBoxMod.ResolutionWidths[res];
					_mod.SaveFrame($"cl_{v.Name}_classic_{num}{suffix}.png");
					if (res != 1 || cpu == null)
					{
						continue;
					}
					Texture2D texture2D = _mod.GpuRendererForTest?.ReadOutput();
					if (!(texture2D == null))
					{
						if (texture2D.width == cw && texture2D.height == ch)
						{
							Log.Info($"CPULOOK {v.Name}: classic vs cpu mean difference {MeanDiff(cpu, texture2D.GetPixels32()):F4}");
						}
						UnityEngine.Object.Destroy(texture2D);
					}
				}
				s.GpuLook = 1;
				savedPx = s.PixelDistance;
				array = new int[2] { 2, 3 };
				foreach (int res in array)
				{
					for (int i2 = 0; i2 < 2; i2++)
					{
						s.Resolution = res;
						s.PixelDistance = i2;
						for (int i3 = 0; i3 < 8; i3++)
						{
							yield return null;
						}
						_mod.SaveFrame(string.Format("cl_{0}_painterly_{1}{2}{3}.png", v.Name, (i2 == 1) ? "retro_" : "", WorldBoxMod.ResolutionWidths[res], suffix));
					}
				}
				s.PixelDistance = savedPx;
			}
			_mod.DebugEyeSet = false;
			Building building = null;
			Vector2 vector = ((_mod.Host != null) ? _mod.Host.current_position : Vector2.zero);
			float num2 = float.MaxValue;
			foreach (Building building2 in World.world.buildings)
			{
				if (building2 != null && building2.current_tile != null && HouseInterior.IsEnterable(building2))
				{
					float sqrMagnitude = (new Vector2(building2.current_tile.x, building2.current_tile.y) - vector).sqrMagnitude;
					if (sqrMagnitude < num2)
					{
						num2 = sqrMagnitude;
						building = building2;
					}
				}
			}
			if (building != null)
			{
				_mod.EnterHouseForTest(building);
				yield return new WaitForSecondsRealtime(1f);
				s.Resolution = 1;
				s.GpuRender = false;
				for (int cw = 0; cw < 8; cw++)
				{
					yield return null;
				}
				_mod.SaveFrame("cl_room_cpu" + suffix + ".png");
				Color32[] cpu = ReadShown(out savedPx, out ch);
				s.GpuRender = true;
				s.GpuLook = 0;
				for (int cw = 0; cw < 8; cw++)
				{
					yield return null;
				}
				_mod.SaveFrame("cl_room_classic_640" + suffix + ".png");
				Texture2D texture2D2 = _mod.GpuRendererForTest?.ReadOutput();
				if (texture2D2 != null && cpu != null && texture2D2.width == savedPx && texture2D2.height == ch)
				{
					Log.Info($"CPULOOK room: classic vs cpu mean difference {MeanDiff(cpu, texture2D2.GetPixels32()):F4}");
				}
				if (texture2D2 != null)
				{
					UnityEngine.Object.Destroy(texture2D2);
				}
				s.GpuLook = 1;
				for (int cw = 0; cw < 8; cw++)
				{
					yield return null;
				}
				_mod.SaveFrame("cl_room_painterly_640" + suffix + ".png");
				_mod.LeaveRoomForTest();
				yield return new WaitForSecondsRealtime(1f);
			}
			s.Resolution = savedRes;
			s.GpuLook = savedLook;
			s.GpuRender = savedGpu;
		}

		private Color32[] ReadShown(out int w, out int h)
		{
			w = (h = 0);
			if (!(_mod.ShownFrame is Texture2D texture2D))
			{
				return null;
			}
			w = texture2D.width;
			h = texture2D.height;
			try
			{
				return texture2D.GetPixels32();
			}
			catch
			{
				return null;
			}
		}

		private static float MeanDiff(Color32[] a, Color32[] b)
		{
			if (a.Length != b.Length)
			{
				return -1f;
			}
			double num = 0.0;
			for (int i = 0; i < a.Length; i++)
			{
				num += (double)(Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b));
			}
			return (float)(num / ((double)a.Length * 765.0));
		}

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(path);
			bool quit = options.Contains("quit");
			bool stay = options.Contains("stay");
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
			Check("shaders load", GpuShaders.Ready, GpuShaders.Error);
			ProbeCheck();
			GpuSupport.WatchEverywhere = true;
			Check("graphics meet the GPU renderer's needs", GpuSupport.Missing == null, GpuSupport.Missing);
			Settings s = _mod.Settings;
			bool savedGpu = s.GpuRender;
			int savedTime = s.TimeMode;
			int savedRes = s.Resolution;
			s.TimeMode = 1;
			s.Resolution = 1;
			s.GpuRender = false;
			_mod.ForceRelight();
			Actor king = null;
			WorldTile village = null;
			int slot = SaveSlot(options);
			if (slot > 0)
			{
				string text = Path.Combine(Path.Combine(Path.Combine(Application.persistentDataPath, "saves"), "save" + slot), "map.wbox");
				bool flag2 = false;
				try
				{
					SaveManager.loadMapFromBytes(File.ReadAllBytes(text));
					flag2 = true;
				}
				catch (Exception ex)
				{
					Log.Error("GPU could not load " + text + ": " + ex.Message);
				}
				if (flag2)
				{
					for (float t = 0f; t < 90f; t += 0.5f)
					{
						yield return new WaitForSecondsRealtime(0.5f);
						bool flag3 = true;
						try
						{
							flag3 = SmoothLoader.isLoading();
						}
						catch
						{
						}
						if (!flag3 && t > 3f)
						{
							break;
						}
					}
					Config.autosaves = false;
					yield return new WaitForSecondsRealtime(2f);
					Log.Info($"GPU loaded save {slot}: {MapBox.width}x{MapBox.height}");
					king = BusiestTown(out village);
				}
			}
			if (king == null)
			{
				foreach (WorldTile item in KingTest.GoodSites())
				{
					king = KingTest.FoundKingdom(item, "human", 10, 3);
					if (king != null)
					{
						village = item;
						break;
					}
				}
			}
			if (king == null)
			{
				Log.Error("GPU could not found a village");
				Done(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(5f);
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(4f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("GPU first person did not start");
				Done(quit);
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
			PlanViews(new Vector2((float)village.x + 0.5f, (float)village.y + 0.5f));
			Config.paused = true;
			for (int i = 0; i < 10; i++)
			{
				yield return null;
			}
			bool cpuLook = options.Contains("cpulook");
			if (cpuLook)
			{
				yield return CpuLook("");
			}
			if (options.Contains("tufts"))
			{
				yield return Tufts(new Vector2((float)village.x + 0.5f, (float)village.y + 0.5f), options.Contains("tuftbiomes"));
				Done(quit);
				yield break;
			}
			if (options.Contains("copies"))
			{
				yield return Copies(new Vector2((float)village.x + 0.5f, (float)village.y + 0.5f));
				Done(quit);
				yield break;
			}
			if (!cpuLook)
			{
				yield return Shots("", cpuToo: true);
			}
			s.GpuRender = true;
			for (int i = 0; i < 4; i++)
			{
				yield return null;
			}
			Check("gpu drew the frame", _mod.ShownFrame == _mod.GpuRendererForTest?.Shown, _mod.GpuRendererForTest?.Error);
			if (!cpuLook)
			{
				yield return Picking();
				yield return Perf();
				yield return Integration();
			}
			if (options.Contains("pixel"))
			{
				yield return Pixels("");
			}
			if (options.Contains("sharp"))
			{
				yield return Sharp("");
			}
			if (options.Contains("dark"))
			{
				yield return Dark("");
			}
			if (options.Contains("compare"))
			{
				yield return Compare(new Vector2((float)village.x + 0.5f, (float)village.y + 0.5f));
			}
			if (stay)
			{
				Log.Info("GPU staying: commands in " + Path.Combine(Log.Folder, "gpu.cmd"));
				string cmdFile = Path.Combine(Log.Folder, "gpu.cmd");
				while (true)
				{
					yield return new WaitForSecondsRealtime(0.5f);
					if (!File.Exists(cmdFile))
					{
						continue;
					}
					string cmd;
					try
					{
						cmd = File.ReadAllText(cmdFile).Trim();
						File.Delete(cmdFile);
					}
					catch
					{
						continue;
					}
					Log.Info("GPU command: " + cmd);
					if (cmd.StartsWith("quit"))
					{
						break;
					}
					if (cmd.StartsWith("shots") || cmd.StartsWith("cpu"))
					{
						string tag = (cmd.Contains(" ") ? cmd.Substring(cmd.IndexOf(' ') + 1).Trim() : "");
						for (int i = 0; i < 3; i++)
						{
							yield return null;
						}
						yield return Shots(tag, cmd.StartsWith("cpu"));
						Log.Info("GPU shots done " + tag);
					}
					if (cmd.StartsWith("bench"))
					{
						yield return Bench(cmd.Substring(5));
						Log.Info("GPU bench done");
					}
					if (cmd.StartsWith("cpulook"))
					{
						string tag = (cmd.Contains(" ") ? cmd.Substring(cmd.IndexOf(' ') + 1).Trim() : "");
						for (int i = 0; i < 3; i++)
						{
							yield return null;
						}
						yield return CpuLook(tag);
						Log.Info("GPU cpulook done " + tag);
					}
					if (cmd.StartsWith("pixel"))
					{
						string tag = (cmd.Contains(" ") ? cmd.Substring(cmd.IndexOf(' ') + 1).Trim() : "");
						for (int i = 0; i < 3; i++)
						{
							yield return null;
						}
						yield return Pixels(tag);
						Log.Info("GPU pixel done " + tag);
					}
					if (cmd.StartsWith("sharp"))
					{
						string tag = (cmd.Contains(" ") ? cmd.Substring(cmd.IndexOf(' ') + 1).Trim() : "");
						for (int i = 0; i < 3; i++)
						{
							yield return null;
						}
						yield return Sharp(tag);
						Log.Info("GPU sharp done " + tag);
					}
					if (cmd.StartsWith("dark"))
					{
						string tag = (cmd.Contains(" ") ? cmd.Substring(cmd.IndexOf(' ') + 1).Trim() : "");
						for (int i = 0; i < 3; i++)
						{
							yield return null;
						}
						yield return Dark(tag);
						Log.Info("GPU dark done " + tag);
					}
					if (cmd.StartsWith("nightsky"))
					{
						yield return NightSky();
						Log.Info("GPU nightsky done");
					}
					if (cmd.StartsWith("time "))
					{
						if (int.TryParse(cmd.Substring(5), out var result))
						{
							s.TimeMode = result;
							_mod.ForceRelight();
						}
						for (int i = 0; i < 30; i++)
						{
							yield return null;
						}
						Log.Info("GPU time set");
					}
					if (cmd.StartsWith("sun "))
					{
						string[] array = cmd.Substring(4).Split(new char[1] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
						CultureInfo invariantCulture = CultureInfo.InvariantCulture;
						NumberStyles style = NumberStyles.Float;
						if (array.Length != 0 && float.TryParse(array[0], style, invariantCulture, out var result2))
						{
							s.TimeMode = 0;
							Config.paused = true;
							_mod.Day.TimeOfDay = Mathf.Repeat(result2, 1f);
							_mod.Day.TestDeclination = ((array.Length > 1 && float.TryParse(array[1], style, invariantCulture, out var result3)) ? new float?(result3 * (MathF.PI / 180f)) : ((float?)null));
							_mod.Day.TestMoonCycle = ((array.Length > 2 && float.TryParse(array[2], style, invariantCulture, out var result4)) ? new float?(Mathf.Repeat(result4, 1f)) : ((float?)null));
							_mod.ForceRelight();
						}
						for (int i = 0; i < 30; i++)
						{
							yield return null;
						}
						Log.Info($"GPU sun set: hour {_mod.Day.Hour:F3}, natural {_mod.Day.NaturalCycle}, declination {_mod.Day.Declination * 57.29578f:F1}, moon {_mod.Day.MoonCycle:F2}");
					}
				}
			}
			Config.paused = false;
			_mod.Day.TestDeclination = (_mod.Day.TestMoonCycle = null);
			s.GpuRender = savedGpu;
			s.TimeMode = savedTime;
			s.Resolution = savedRes;
			Done(quit);
		}

		private static int SaveSlot(string options)
		{
			int num = options.IndexOf("map");
			if (num < 0)
			{
				return 0;
			}
			int num2 = 0;
			int num3 = num + 3;
			while (num3 < options.Length && char.IsDigit(options[num3]))
			{
				num2 = num2 * 10 + (options[num3++] - 48);
			}
			return num2;
		}

		private static Actor BusiestTown(out WorldTile centre)
		{
			centre = null;
			Actor actor = null;
			int num = 0;
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
				int num2 = 0;
				Actor actor2 = null;
				foreach (Actor item2 in simpleList)
				{
					if (item2 != null && item2.isAlive() && !((item2.current_position - new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f)).sqrMagnitude > 225f))
					{
						num2++;
						if (actor2 == null && item2.city == item)
						{
							actor2 = item2;
						}
					}
				}
				if (actor2 != null && num2 > num)
				{
					num = num2;
					actor = actor2;
					centre = worldTile;
				}
			}
			if (actor != null)
			{
				Log.Info($"GPU town at {centre.x},{centre.y}: {num} people round it");
			}
			return actor;
		}

		private void PlanViews(Vector2 village)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			_views.Clear();
			Vector2 eye = village + new Vector2(28f, 0f);
			float num = -99f;
			for (int i = 0; i < 16; i++)
			{
				float f = (float)i * MathF.PI / 8f;
				Vector2 vector = village + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * 28f;
				if (OnLand(vector))
				{
					float num2 = terrainForTest.SampleHeight(vector.x, vector.y);
					if (num2 > num)
					{
						num = num2;
						eye = vector;
					}
				}
			}
			_views.Add(Look("village", eye, 7f, village, -9f));
			Vector2 vector2 = village + new Vector2(-6f, -5f);
			_views.Add(Look("street", vector2, 0f, village + new Vector2(8f, 6f), 1.2f));
			Vector2 eye2 = new Vector2((float)terrainForTest.Width * 0.5f, (float)terrainForTest.Height * 0.5f);
			float num3 = -99f;
			for (int j = 8; j < terrainForTest.Height - 8; j += 4)
			{
				for (int k = 8; k < terrainForTest.Width - 8; k += 4)
				{
					float num4 = terrainForTest.SampleHeight((float)k + 0.5f, (float)j + 0.5f) - 0.02f * (Mathf.Abs((float)k - (float)terrainForTest.Width * 0.5f) + Mathf.Abs((float)j - (float)terrainForTest.Height * 0.5f));
					if (num4 > num3)
					{
						num3 = num4;
						eye2 = new Vector2((float)k + 0.5f, (float)j + 0.5f);
					}
				}
			}
			_views.Add(LandView("vista", eye2, 3f, 40f, 170f, -2f));
			_views.Add(LandView("hills", vector2 + new Vector2(0f, -4f), 1.5f, 50f, 160f, 1.5f));
			Vector2 vector3 = Vector2.zero;
			float num5 = float.MaxValue;
			Vector2 vector4 = Vector2.right;
			for (int l = 4; l < terrainForTest.Height - 4; l += 2)
			{
				for (int m = 4; m < terrainForTest.Width - 4; m += 2)
				{
					if ((terrainForTest.Flags[l * terrainForTest.Width + m] & 1) == 0)
					{
						continue;
					}
					for (int n = 0; n < 8; n++)
					{
						float f2 = (float)n * MathF.PI / 4f;
						Vector2 vector5 = new Vector2(Mathf.Cos(f2), Mathf.Sin(f2));
						Vector2 vector6 = new Vector2((float)m + 0.5f, (float)l + 0.5f) - vector5 * 3f;
						if (OnLand(vector6))
						{
							float sqrMagnitude = (vector6 - village).sqrMagnitude;
							if (sqrMagnitude < num5)
							{
								num5 = sqrMagnitude;
								vector3 = vector6;
								vector4 = vector5;
							}
						}
					}
				}
			}
			if (num5 < float.MaxValue)
			{
				_views.Add(Look("coast", vector3, 1.5f, vector3 + vector4 * 40f, 0.2f));
			}
			foreach (View view in _views)
			{
				Log.Info($"GPU view {view.Name}: eye {view.Eye.x:F0},{view.Eye.y:F0} +{view.Height:F1} yaw {view.Yaw * 57.29578f:F0} pitch {view.Pitch:F1}");
			}
		}

		private View LandView(string name, Vector2 eye, float height, float near, float far, float pitchDegrees)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			float num = -1f;
			float yaw = 0f;
			for (int i = 0; i < 32; i++)
			{
				float num2 = (float)i * MathF.PI / 16f;
				Vector2 vector = new Vector2(Mathf.Cos(num2), Mathf.Sin(num2));
				float num3 = 0f;
				for (float num4 = near; num4 <= far; num4 += 4f)
				{
					Vector2 vector2 = eye + vector * num4;
					if (vector2.x < 0f || vector2.y < 0f || vector2.x >= (float)terrainForTest.Width || vector2.y >= (float)terrainForTest.Height)
					{
						break;
					}
					if ((terrainForTest.Flags[(int)vector2.y * terrainForTest.Width + (int)vector2.x] & 1) == 0)
					{
						num3 += 1f + terrainForTest.SampleHeight(vector2.x, vector2.y) * 0.3f;
					}
				}
				if (num3 > num)
				{
					num = num3;
					yaw = num2;
				}
			}
			return new View
			{
				Name = name,
				Eye = eye,
				Height = height,
				Yaw = yaw,
				Pitch = pitchDegrees
			};
		}

		private View Look(string name, Vector2 eye, float height, Vector2 at, float pitchDegrees)
		{
			Vector2 vector = at - eye;
			return new View
			{
				Name = name,
				Eye = eye,
				Height = height,
				Yaw = Mathf.Atan2(vector.y, vector.x),
				Pitch = pitchDegrees
			};
		}

		private static bool OnLand(Vector2 p)
		{
			if (p.x < 2f || p.y < 2f || p.x > (float)(MapBox.width - 3) || p.y > (float)(MapBox.height - 3))
			{
				return false;
			}
			WorldTile tile = World.world.GetTile((int)p.x, (int)p.y);
			if (tile != null && tile.main_type != null)
			{
				return !tile.main_type.liquid;
			}
			return false;
		}

		private IEnumerator Shots(string tag, bool cpuToo)
		{
			Settings s = _mod.Settings;
			foreach (View v in _views)
			{
				_mod.DebugEye = v.Eye;
				_mod.DebugEyeSet = true;
				_mod.DebugEyeHeight = ((v.Height > 0f) ? v.Height : 0f);
				_mod.SetYaw(v.Yaw);
				_mod.SetPitchDegrees(v.Pitch);
				if (cpuToo)
				{
					s.GpuRender = false;
					for (int i = 0; i < 6; i++)
					{
						yield return null;
					}
					_mod.SaveFrame("gpu_" + v.Name + "_cpu.png");
				}
				s.GpuRender = true;
				if (cpuToo)
				{
					s.GpuLook = 0;
					for (int i = 0; i < 6; i++)
					{
						yield return null;
					}
					_mod.SaveFrame("gpu_" + v.Name + "_classic.png");
				}
				s.GpuLook = 1;
				for (int i = 0; i < 6; i++)
				{
					yield return null;
				}
				_mod.SaveFrame("gpu_" + v.Name + ((tag.Length > 0) ? ("_" + tag) : "") + "_gpu.png");
				if (_mod.GpuRendererForTest != null && _mod.GpuRendererForTest.Error != null)
				{
					Log.Error("GPU " + v.Name + ": " + _mod.GpuRendererForTest.Error);
				}
			}
		}

		private IEnumerator Integration()
		{
			Settings s = _mod.Settings;
			s.GpuRender = true;
			_mod.DebugEyeSet = false;
			Building building = null;
			Vector2 vector = ((_mod.Host != null) ? _mod.Host.current_position : Vector2.zero);
			float num = float.MaxValue;
			foreach (Building building2 in World.world.buildings)
			{
				if (building2 != null && building2.current_tile != null && HouseInterior.IsEnterable(building2))
				{
					float sqrMagnitude = (new Vector2(building2.current_tile.x, building2.current_tile.y) - vector).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						building = building2;
					}
				}
			}
			if (building != null)
			{
				_mod.EnterHouseForTest(building);
				yield return new WaitForSecondsRealtime(1f);
				Check("a room is drawn on the card too, its picture shown", _mod.Interior.Inside && _mod.ShownFrame == _mod.GpuRendererForTest?.Shown, string.Format("inside {0}, shown {1}", _mod.Interior.Inside, (_mod.ShownFrame == _mod.FrameTexture) ? "CPU" : "GPU"));
				_mod.SaveFrame("gpu_room.png");
				_mod.LeaveRoomForTest();
				yield return new WaitForSecondsRealtime(1f);
				Check("outdoors again on the GPU", !_mod.Interior.Inside && _mod.ShownFrame == _mod.GpuRendererForTest?.Shown, string.Format("inside {0}, shown {1}", _mod.Interior.Inside, (_mod.ShownFrame == _mod.FrameTexture) ? "CPU" : "GPU"));
			}
			else
			{
				Log.Info("GPU (no enterable house: the room check skipped)");
			}
			int res = s.Resolution;
			for (int k = 0; k < WorldBoxMod.ResolutionWidths.Length; k++)
			{
				s.Resolution = k;
				for (int i = 0; i < 4; i++)
				{
					yield return null;
				}
				GpuRenderer gpuRendererForTest = _mod.GpuRendererForTest;
				Check("render width " + WorldBoxMod.ResolutionWidths[k], gpuRendererForTest != null && gpuRendererForTest.Output != null && gpuRendererForTest.Output.width == WorldBoxMod.ResolutionWidths[k] && _mod.ShownFrame == gpuRendererForTest.Shown, (gpuRendererForTest?.Output != null) ? (gpuRendererForTest.Output.width + "x" + gpuRendererForTest.Output.height) : "no output");
			}
			s.Resolution = res;
			s.GpuRender = false;
			for (int k = 0; k < 4; k++)
			{
				yield return null;
			}
			bool cpu = _mod.ShownFrame == _mod.FrameTexture;
			s.GpuRender = true;
			for (int k = 0; k < 4; k++)
			{
				yield return null;
			}
			Check("the setting switches the picture over and back", cpu && _mod.ShownFrame == _mod.GpuRendererForTest?.Shown);
		}

		private IEnumerator Pixels(string tag)
		{
			Settings s = _mod.Settings;
			int savedRes = s.Resolution;
			int savedPx = s.PixelDistance;
			int savedLook = s.GpuLook;
			int savedPal = s.Palette;
			string suffix = ((tag.Length > 0) ? ("_" + tag) : "");
			s.GpuRender = true;
			foreach (View v in _views)
			{
				_mod.DebugEye = v.Eye;
				_mod.DebugEyeSet = true;
				_mod.DebugEyeHeight = ((v.Height > 0f) ? v.Height : 0f);
				_mod.SetYaw(v.Yaw);
				_mod.SetPitchDegrees(v.Pitch);
				int[] array = new int[2]
				{
					1,
					WorldBoxMod.ResolutionWidths.Length - 1
				};
				foreach (int res in array)
				{
					for (int look = 0; look < 2; look++)
					{
						s.Resolution = res;
						s.GpuLook = look;
						int width = WorldBoxMod.ResolutionWidths[res];
						string finish = ((look == 0) ? "classic" : "painterly");
						Color32[] off = null;
						Color32[] on = null;
						int w = 0;
						int h = 0;
						int levels = 0;
						for (int px = 0; px < 2; px++)
						{
							s.PixelDistance = px;
							for (int j = 0; j < 6; j++)
							{
								yield return null;
							}
							_mod.SaveFrame(string.Format("px_{0}_{1}_{2}_{3}{4}.png", v.Name, finish, width, (px == 1) ? "on" : "off", suffix));
							GpuRenderer gpuRendererForTest = _mod.GpuRendererForTest;
							Texture2D texture2D = gpuRendererForTest?.ReadOutput();
							if (!(texture2D == null))
							{
								w = texture2D.width;
								h = texture2D.height;
								if (px == 0)
								{
									off = texture2D.GetPixels32();
								}
								else
								{
									on = texture2D.GetPixels32();
									levels = gpuRendererForTest.PixelLevels;
								}
								UnityEngine.Object.Destroy(texture2D);
							}
						}
						if (off == null || on == null)
						{
							Check($"pixel {v.Name} {finish} {width}: pictures read back", ok: false);
							continue;
						}
						float num = Flatness(off, w, h);
						float num2 = Flatness(on, w, h);
						float num3 = BottomDiff(off, on, w, h);
						Log.Info($"PIXEL {v.Name} {finish} {width}: flat {num:F3} -> {num2:F3}, near ground changed {num3:F4}, levels {levels}");
						if (v.Name == "vista")
						{
							Check($"pixel distance flattens the far land ({finish}, {width})", num2 > num + 0.04f, $"{num:F3} -> {num2:F3}");
						}
						if (v.Name == "street")
						{
							Check($"pixel distance leaves the near ground ({finish}, {width})", num3 < 0.02f, num3.ToString("F4"));
						}
						if (v.Name == "vista")
						{
							Check($"block levels for {width} wide", levels == ((width >= 960) ? 4 : 3), levels.ToString());
						}
					}
				}
			}
			if (_views.Count > 2)
			{
				View v = _views[2];
				_mod.DebugEye = v.Eye;
				_mod.DebugEyeHeight = ((v.Height > 0f) ? v.Height : 0f);
				_mod.SetPitchDegrees(v.Pitch);
				s.Resolution = 1;
				s.GpuLook = 0;
				float[] moved = new float[2];
				for (int i = 0; i < 2; i++)
				{
					s.PixelDistance = i;
					Color32[] on = null;
					Color32[] off = null;
					int res = 0;
					int look = 0;
					for (int levels = 0; levels < 2; levels++)
					{
						float num4 = _mod.GpuRendererForTest?.Projection.Focal ?? 500f;
						_mod.SetYaw(v.Yaw + (float)levels * 5f / num4);
						for (int h = 0; h < 6; h++)
						{
							yield return null;
						}
						Texture2D texture2D2 = _mod.GpuRendererForTest?.ReadOutput();
						if (!(texture2D2 == null))
						{
							res = texture2D2.width;
							look = texture2D2.height;
							if (levels == 0)
							{
								on = texture2D2.GetPixels32();
							}
							else
							{
								off = texture2D2.GetPixels32();
							}
							UnityEngine.Object.Destroy(texture2D2);
						}
					}
					if (on != null && off != null)
					{
						moved[i] = Mathf.Min(ShiftDiff(on, off, res, look, 5), ShiftDiff(on, off, res, look, -5));
					}
				}
				Log.Info($"PIXEL turning 5 pixels: the picture moved back differs {moved[0]:F4} without blocks, {moved[1]:F4} with");
				Check("the far blocks slide with the view as it turns (no crawling)", moved[1] < moved[0] + 0.015f, $"{moved[0]:F4} / {moved[1]:F4}");
			}
			if (_views.Count > 0)
			{
				View view = _views[Mathf.Min(2, _views.Count - 1)];
				_mod.DebugEye = view.Eye;
				_mod.DebugEyeHeight = ((view.Height > 0f) ? view.Height : 0f);
				_mod.SetYaw(view.Yaw);
				_mod.SetPitchDegrees(view.Pitch);
				s.Resolution = 1;
				s.GpuLook = 1;
				s.PixelDistance = 1;
				int[] array = new int[2];
				for (int i = 0; i < 2; i++)
				{
					s.Palette = i;
					for (int look = 0; look < 6; look++)
					{
						yield return null;
					}
					_mod.SaveFrame("px_palette_" + ((i == 1) ? "on" : "off") + suffix + ".png");
					Texture2D texture2D3 = _mod.GpuRendererForTest?.ReadOutput();
					if (!(texture2D3 == null))
					{
						HashSet<int> hashSet = new HashSet<int>();
						Color32[] pixels = texture2D3.GetPixels32();
						for (int k = 0; k < pixels.Length; k++)
						{
							Color32 color = pixels[k];
							hashSet.Add(color.r | (color.g << 8) | (color.b << 16));
						}
						array[i] = hashSet.Count;
						UnityEngine.Object.Destroy(texture2D3);
					}
				}
				Check("the palette filter works on the GPU (Painterly: more colours with it off)", (float)array[0] > (float)array[1] * 1.3f, $"off {array[0]}, on {array[1]}");
			}
			s.Resolution = savedRes;
			s.PixelDistance = savedPx;
			s.GpuLook = savedLook;
			s.Palette = savedPal;
		}

		private static float Flatness(Color32[] p, int w, int h)
		{
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < h; i++)
			{
				for (int j = 0; j + 1 < w; j++)
				{
					Color32 color = p[i * w + j];
					Color32 color2 = p[i * w + j + 1];
					if (color.r == color2.r && color.g == color2.g && color.b == color2.b)
					{
						num++;
					}
					num2++;
				}
			}
			if (num2 <= 0)
			{
				return 0f;
			}
			return (float)num / (float)num2;
		}

		private static float ShiftDiff(Color32[] a, Color32[] b, int w, int h, int dx)
		{
			double num = 0.0;
			int num2 = 0;
			for (int i = h * 3 / 10; i < h * 7 / 10; i++)
			{
				for (int j = w / 4; j < w * 3 / 4; j++)
				{
					Color32 color = a[i * w + j];
					Color32 color2 = b[i * w + j + dx];
					num += (double)(Math.Abs(color.r - color2.r) + Math.Abs(color.g - color2.g) + Math.Abs(color.b - color2.b));
					num2 += 3;
				}
			}
			if (num2 <= 0)
			{
				return 0f;
			}
			return (float)(num / (double)num2 / 255.0);
		}

		private static float BottomDiff(Color32[] a, Color32[] b, int w, int h)
		{
			double num = 0.0;
			int num2 = 0;
			for (int i = 0; i < h / 4; i++)
			{
				for (int j = 0; j < w; j++)
				{
					Color32 color = a[i * w + j];
					Color32 color2 = b[i * w + j];
					num += (double)(Math.Abs(color.r - color2.r) + Math.Abs(color.g - color2.g) + Math.Abs(color.b - color2.b));
					num2 += 3;
				}
			}
			if (num2 <= 0)
			{
				return 0f;
			}
			return (float)(num / (double)num2 / 255.0);
		}

		private IEnumerator Compare(Vector2 village)
		{
			Settings settings = _mod.Settings;
			_ = _mod.TerrainForTest;
			_mod.DebugEye = village;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = 4f;
			settings.GpuRender = false;
			for (int i = 0; i < 8; i++)
			{
				yield return null;
			}
			SceneCollector scene = _mod.SceneForTest;
			List<MeshInstance> list = new List<MeshInstance>();
			HashSet<string> hashSet = new HashSet<string>();
			List<MeshInstance> list2 = new List<MeshInstance>(scene.Meshes);
			list2.Sort((MeshInstance a, MeshInstance meshInstance) => Dist(a.Position, village).CompareTo(Dist(meshInstance.Position, village)));
			foreach (MeshInstance item in list2)
			{
				if (item?.Model != null && list.Count < 7)
				{
					string text = ((item.Model.Name.EndsWith("_m") || item.Model.Name.EndsWith("_f")) ? item.Model.Name.Substring(0, item.Model.Name.Length - 2) : item.Model.Name);
					if ((text.StartsWith("tree") || text.StartsWith("bush") || text.Contains("_tree")) && Array.IndexOf(item.Model.SlotKind, (byte)1) >= 0 && !(item.Basis.C2.Z * item.Model.Max.Z < 0.8f) && hashSet.Add(text))
					{
						list.Add(item);
					}
				}
			}
			foreach (MeshInstance item2 in list)
			{
				float num = item2.Basis.C2.Z * item2.Model.Max.Z;
				float b = Mathf.Max(Mathf.Abs(item2.Model.Min.X), Mathf.Abs(item2.Model.Max.X), Mathf.Abs(item2.Model.Min.Y), Mathf.Abs(item2.Model.Max.Y)) * Mathf.Abs(item2.Basis.C0.X + item2.Basis.C0.Y);
				yield return CompareAt("tree_" + item2.Model.Name, new Vector3(item2.Position.X, item2.Position.Y, item2.Position.Z + num * 0.6f), Mathf.Max(1f, b) + 2.5f, 0f);
			}
			VoxelObject? voxelObject = null;
			float num2 = float.MaxValue;
			foreach (VoxelObject voxel in scene.Voxels)
			{
				if (voxel.Shape != null && voxel.Shape.Leaves != null && !voxel.Overlay)
				{
					float num3 = Dist(voxel.Origin, village);
					if (num3 < num2)
					{
						num2 = num3;
						voxelObject = voxel;
					}
				}
			}
			if (voxelObject.HasValue)
			{
				VoxelObject value = voxelObject.Value;
				Vec3 c = value.Origin + value.U * ((float)value.Shape.Width * value.SizeU * 0.5f) + value.V * ((float)value.Shape.Height * value.SizeV * 0.6f);
				yield return CompareAt("vtree_near", new Vector3(c.X, c.Y, c.Z), (float)value.Shape.Width * value.SizeU * 0.5f + 1.5f, 0f);
				yield return CompareAt("vtree_mid", new Vector3(c.X, c.Y, c.Z), 8f, 0f);
			}
			else
			{
				Log.Info($"GPU compare: no tree made from its sprite near the village ({scene.Voxels.Count} voxel objects)");
			}
			yield return CompareDragon(village);
			_mod.DebugEyeHeight = 0f;
		}

		private static float Dist(Vec3 p, Vector2 at)
		{
			return Mathf.Sqrt((p.X - at.x) * (p.X - at.x) + (p.Y - at.y) * (p.Y - at.y));
		}

		private IEnumerator CompareAt(string name, Vector3 at, float range, float above)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			Vector2 vector = new Vector2(at.x + range, at.y);
			float num = float.MinValue;
			for (int i = 0; i < 12; i++)
			{
				float f = (float)i * MathF.PI / 6f;
				Vector2 vector2 = new Vector2(at.x + Mathf.Cos(f) * range, at.y + Mathf.Sin(f) * range);
				if (OnLand(vector2))
				{
					float num2 = 0f;
					for (int j = 1; j < 6; j++)
					{
						float t = (float)j / 6f;
						num2 -= Mathf.Max(0f, terrainForTest.SampleHeight(Mathf.Lerp(vector2.x, at.x, t), Mathf.Lerp(vector2.y, at.y, t)) - at.z);
					}
					if (num2 > num)
					{
						num = num2;
						vector = vector2;
					}
				}
			}
			float num3 = terrainForTest.SampleHeight(vector.x, vector.y);
			float num4 = Mathf.Max(num3 + 1.4f, at.z + above);
			_mod.DebugEye = vector;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = num4 - num3;
			Vector2 vector3 = new Vector2(at.x, at.y) - vector;
			_mod.SetYaw(Mathf.Atan2(vector3.y, vector3.x));
			_mod.SetPitchDegrees(Mathf.Clamp(Mathf.Atan2(at.z - num4, vector3.magnitude) * 57.29578f, -40f, 40f));
			Log.Info($"GPU compare {name}: eye {vector.x:F1},{vector.y:F1} +{num4 - num3:F1}, at {at.x:F1},{at.y:F1},{at.z:F1}, {range:F1} off");
			yield return ThreeShots("cmp_" + name);
		}

		private IEnumerator ThreeShots(string name)
		{
			Settings s = _mod.Settings;
			s.GpuRender = false;
			for (int i = 0; i < 24; i++)
			{
				yield return null;
			}
			_mod.SaveFrame(name + "_cpu.png");
			s.GpuRender = true;
			s.GpuLook = 0;
			for (int i = 0; i < 6; i++)
			{
				yield return null;
			}
			_mod.SaveFrame(name + "_classic.png");
			s.GpuLook = 1;
			for (int i = 0; i < 6; i++)
			{
				yield return null;
			}
			_mod.SaveFrame(name + "_gpu.png");
			if (_mod.GpuRendererForTest != null && _mod.GpuRendererForTest.Error != null)
			{
				Log.Error("GPU " + name + ": " + _mod.GpuRendererForTest.Error);
			}
		}

		private IEnumerator CompareDragon(Vector2 village)
		{
			WorldTile worldTile = null;
			for (int i = 0; i < 24; i++)
			{
				if (worldTile != null)
				{
					break;
				}
				float f = (float)i * MathF.PI / 12f;
				Vector2 p = village + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * 70f;
				if (OnLand(p))
				{
					worldTile = World.world.GetTile((int)p.x, (int)p.y);
				}
			}
			if (worldTile == null)
			{
				worldTile = World.world.GetTile(Mathf.Clamp((int)village.x + 20, 2, MapBox.width - 3), Mathf.Clamp((int)village.y, 2, MapBox.height - 3));
			}
			Actor dragon = null;
			try
			{
				dragon = World.world.units.spawnNewUnit("dragon", worldTile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("GPU compare: dragon spawn: " + ex.Message);
			}
			if (dragon == null)
			{
				yield break;
			}
			Config.paused = false;
			Vec3 pos = default(Vec3);
			string act = "";
			float alt = 0f;
			float waited = 0f;
			float fire;
			int hold;
			float held;
			while (waited < 25f && dragon != null && dragon.isAlive())
			{
				Vector2 current_position = dragon.current_position;
				_mod.DebugEye = current_position + new Vector2(-26f, -6f);
				_mod.DebugEyeSet = true;
				_mod.DebugEyeHeight = 6f;
				_mod.SetYaw(Mathf.Atan2(6f, 26f));
				_mod.SetPitchDegrees(8f);
				if (Dragons.Probe(dragon, out pos, out act, out alt, out fire, out hold, out held) && alt > 6f && waited > 6f)
				{
					switch (act)
					{
					case "Cruise":
					case "Glide":
					case "Swoop":
						goto end_IL_027c;
					}
				}
				waited += Time.unscaledDeltaTime;
				yield return null;
				continue;
				end_IL_027c:
				break;
			}
			Config.paused = true;
			int i2 = 0;
			while (i2 < 4)
			{
				yield return null;
				hold = i2++;
			}
			if (!Dragons.Probe(dragon, out pos, out act, out alt, out held, out hold, out fire))
			{
				Log.Error("GPU compare: the dragon is not drawn");
				yield break;
			}
			Log.Info($"GPU compare dragon: {act} at {pos.X:F1},{pos.Y:F1},{pos.Z:F1}, {alt:F1} up");
			Vector3 at = new Vector3(pos.X, pos.Y, pos.Z);
			yield return CompareAt("dragon_below", at, 16f, 0f - Mathf.Min(alt - 1.5f, 9f));
			yield return CompareAt("dragon_above", at, 16f, 9f);
			yield return CompareAt("dragon_level", at, 12f, 0f);
		}

		private IEnumerator NightSky()
		{
			Settings s = _mod.Settings;
			int savedTime = s.TimeMode;
			s.TimeMode = 3;
			_mod.ForceRelight();
			FrameRenderer r = _mod.Renderer;
			r.ForceComet = 1f;
			r.ForceAurora = 1f;
			r.ForceShower = 1f;
			View v = ((_views.Count > 2) ? _views[2] : _views[0]);
			_mod.DebugEye = v.Eye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = v.Height;
			for (int k = 0; k < 4; k++)
			{
				_mod.SetYaw(v.Yaw + (float)k * MathF.PI * 0.5f);
				_mod.SetPitchDegrees(28f);
				s.GpuRender = false;
				for (int i = 0; i < 40; i++)
				{
					yield return null;
				}
				_mod.SaveFrame("gpu_night" + k + "_cpu.png");
				s.GpuRender = true;
				for (int i = 0; i < 6; i++)
				{
					yield return null;
				}
				_mod.SaveFrame("gpu_night" + k + "_gpu.png");
			}
			r.ForceComet = (r.ForceAurora = (r.ForceShower = -1f));
			s.TimeMode = savedTime;
			_mod.ForceRelight();
		}

		private IEnumerator Picking()
		{
			Settings s = _mod.Settings;
			s.GpuRender = true;
			View view = ((_views.Count > 1) ? _views[1] : _views[0]);
			Actor target = null;
			float num = float.MaxValue;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple != _mod.Host)
				{
					float magnitude = (simple.current_position - view.Eye).magnitude;
					if (magnitude > 3f && magnitude < 9f && magnitude < num)
					{
						num = magnitude;
						target = simple;
					}
				}
			}
			if (target == null)
			{
				Log.Info("GPU no creature near the street eye to pick");
				yield break;
			}
			TerrainCache terrainForTest = _mod.TerrainForTest;
			Vector2 vector = target.current_position - view.Eye;
			float num2 = terrainForTest.SampleHeight(view.Eye.x, view.Eye.y) + 1.4f;
			float num3 = terrainForTest.SampleHeight(target.current_position.x, target.current_position.y) + 0.5f;
			_mod.DebugEye = view.Eye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = 1.4f;
			_mod.SetYaw(Mathf.Atan2(vector.y, vector.x));
			_mod.SetPitchDegrees(Mathf.Atan2(num3 - num2, vector.magnitude) * 57.29578f);
			s.GpuRender = false;
			for (int i = 0; i < 6; i++)
			{
				yield return null;
			}
			BaseSimObject cpu = _mod.Picked;
			s.GpuRender = true;
			for (int i = 0; i < 12; i++)
			{
				yield return null;
			}
			BaseSimObject picked = _mod.Picked;
			Check("the crosshair picks on the GPU path as on the CPU", picked == cpu || (picked != null && cpu != null), "GPU " + ((picked == null) ? "nothing" : ((picked == target) ? "the creature looked at" : picked.ToString())) + ", CPU " + ((cpu == null) ? "nothing" : ((cpu == target) ? "the creature looked at" : cpu.ToString())));
			_mod.SaveFrame("gpu_pick_gpu.png");
		}

		private IEnumerator Perf()
		{
			Settings s = _mod.Settings;
			View view = _views[0];
			_mod.DebugEye = view.Eye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = view.Height;
			_mod.SetYaw(view.Yaw);
			_mod.SetPitchDegrees(view.Pitch);
			float[] ms = new float[2];
			for (int pass = 0; pass < 2; pass++)
			{
				s.GpuRender = pass == 1;
				for (int i = 0; i < 20; i++)
				{
					yield return null;
				}
				float sum = 0f;
				for (int i = 0; i < 60; i++)
				{
					yield return null;
					sum += _mod.FrameMs;
				}
				ms[pass] = sum / 60f;
			}
			Log.Info($"PERF GPU village: the mod's frame {ms[0]:F1} ms on the CPU (in step), {ms[1]:F1} ms on the GPU ({ms[0] / Mathf.Max(0.01f, ms[1]):F1}x)");
			Check("the GPU frame costs the game's thread less", ms[1] < ms[0], $"{ms[1]:F1} vs {ms[0]:F1} ms");
		}

		private void ProbeCheck()
		{
			Shader shader = GpuShaders.Get("FirstPerson/Probe");
			if (shader == null)
			{
				Check("probe shader found", ok: false);
				return;
			}
			Material material = new Material(shader);
			material.SetColor("_Color", new Color(1f, 0.5f, 0.25f, 1f));
			RenderTexture renderTexture = new RenderTexture(64, 32, 0, RenderTextureFormat.ARGB32);
			Graphics.Blit(null, renderTexture, material);
			Texture2D texture2D = new Texture2D(64, 32, TextureFormat.RGBA32, mipChain: false);
			RenderTexture active = RenderTexture.active;
			RenderTexture.active = renderTexture;
			texture2D.ReadPixels(new Rect(0f, 0f, 64f, 32f), 0, 0);
			texture2D.Apply();
			RenderTexture.active = active;
			Color pixel = texture2D.GetPixel(1, 16);
			Color pixel2 = texture2D.GetPixel(62, 16);
			Check("probe draws its colour", pixel.r > 0.4f && pixel.r < 0.6f && pixel2.r > 0.9f && Mathf.Abs(pixel2.g - 0.5f) < 0.06f, $"{pixel} {pixel2}");
			UnityEngine.Object.Destroy(texture2D);
			UnityEngine.Object.Destroy(material);
			renderTexture.Release();
		}

		private void Done(bool quit)
		{
			if (_mod != null)
			{
				_mod.DebugEyeSet = false;
			}
			Check("the picture watch passed the GPU's pictures", GpuSupport.Broken == null, GpuSupport.Broken);
			Log.Info($"GPU {_pass} passed, {_fail} failed");
			Log.Info("GPU DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private void Check(string what, bool ok, string detail = null)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info((ok ? "PASS " : "FAIL ") + "GPU " + what + ((detail != null) ? (" (" + detail + ")") : ""));
		}

		private IEnumerator Dark(string tag)
		{
			Settings s = _mod.Settings;
			List<string> list = new List<string>();
			try
			{
				foreach (HotkeyAsset item in AssetManager.hotkey_library.list)
				{
					if (item != null && (item.default_key_1 == KeyCode.F10 || item.default_key_2 == KeyCode.F10 || item.default_key_3 == KeyCode.F10))
					{
						list.Add(item.id);
					}
				}
			}
			catch (Exception ex)
			{
				list.Add("(not read: " + ex.Message + ")");
			}
			Check("F10 is free of the game's own hotkeys", list.Count == 0, string.Join(", ", list.ToArray()));
			int savedTime = s.TimeMode;
			int savedLook = s.GpuLook;
			int savedDark = s.DarkFantasy;
			int savedPal = s.Palette;
			int savedRes = s.Resolution;
			string suffix = ((tag.Length > 0) ? ("_" + tag) : "");
			s.GpuRender = true;
			s.Resolution = 1;
			s.GpuLook = 1;
			Dictionary<string, Color32[]> stats = new Dictionary<string, Color32[]>();
			int w = 0;
			int h = 0;
			int[] array = new int[2] { 1, 3 };
			for (int i = 0; i < array.Length; i++)
			{
				int time = (s.TimeMode = array[i]);
				_mod.ForceRelight();
				for (int j = 0; j < 30; j++)
				{
					yield return null;
				}
				string when = ((time == 3) ? "night" : "day");
				foreach (View v in _views)
				{
					Aim(v);
					string[] array2 = ((time != 1) ? new string[2] { "off", "on" } : new string[3] { "off", "on", "gloom" });
					foreach (string mode in array2)
					{
						s.DarkFantasy = ((!(mode == "off")) ? 1 : 0);
						GpuDark.GloomForTest = ((mode == "gloom") ? 1f : ((mode == "on") ? 0f : (-1f)));
						for (int k = 0; k < 40; k++)
						{
							if (!(Mathf.Abs(GpuDark.Shown - (float)s.DarkFantasy) > 0.001f))
							{
								break;
							}
							yield return null;
						}
						for (int k = 0; k < 4; k++)
						{
							yield return null;
						}
						string text = "dark_" + v.Name + "_" + when + "_" + mode + suffix;
						_mod.SaveFrame(text + ".png");
						Texture2D texture2D = _mod.GpuRendererForTest?.ReadOutput();
						if (!(texture2D == null))
						{
							w = texture2D.width;
							h = texture2D.height;
							stats[v.Name + "_" + when + "_" + mode] = texture2D.GetPixels32();
							UnityEngine.Object.Destroy(texture2D);
						}
					}
				}
			}
			GpuDark.GloomForTest = -1f;
			foreach (KeyValuePair<string, Color32[]> item2 in stats)
			{
				Log.Info($"DARK {item2.Key}: luma {MeanLuma(item2.Value):F3}, far band blueness {Blueness(item2.Value, w, h, 0.35f, 0.6f):F3} hue {BandHue(item2.Value, w, h, 0.35f, 0.6f):F0}, bottom blue {MeanBlue(item2.Value, w, h, 0.55f, 1f):F3}, colours {Colours(item2.Value)}");
			}
			if (stats.TryGetValue("village_day_off", out var value) && stats.TryGetValue("village_day_on", out var value2))
			{
				Check("dark fantasy changes the picture", Diff(value, value2) > 0.02f, Diff(value, value2).ToString("F3"));
			}
			string text2 = (stats.ContainsKey("vista_day_on") ? "vista" : "village");
			if (stats.TryGetValue(text2 + "_day_off", out var value3) && stats.TryGetValue(text2 + "_day_on", out var value4))
			{
				float num = BandHue(value3, w, h, 0.35f, 0.6f);
				float num2 = BandHue(value4, w, h, 0.35f, 0.6f);
				float num3 = Mathf.Abs(Mathf.DeltaAngle(num, 272f));
				float num4 = Mathf.Abs(Mathf.DeltaAngle(num2, 272f));
				Check("the far land turns towards blue-violet", num4 < num3 - 3f || num4 < 15f, $"{text2}: hue {num:F0} -> {num2:F0} degrees");
			}
			if (stats.TryGetValue(text2 + "_day_on", out var value5) && stats.TryGetValue(text2 + "_day_gloom", out var value6))
			{
				Check("a dark day is darker than a bright one", MeanLuma(value6) < MeanLuma(value5) * 0.92f, $"{MeanLuma(value5):F3} -> {MeanLuma(value6):F3}");
			}
			if (stats.TryGetValue("coast_night_off", out var value7) && stats.TryGetValue("coast_night_on", out var value8))
			{
				Check("the water glows at night", MeanBlue(value8, w, h, 0.55f, 1f) > MeanBlue(value7, w, h, 0.55f, 1f) + 0.03f, $"{MeanBlue(value7, w, h, 0.55f, 1f):F3} -> {MeanBlue(value8, w, h, 0.55f, 1f):F3}");
			}
			else
			{
				Log.Info("DARK no coast view: the water's glow not checked");
			}
			yield return DarkRoom(suffix);
			if (_views.Count > 0)
			{
				Aim(_views[0]);
				s.TimeMode = 1;
				_mod.ForceRelight();
				s.Palette = 1;
				array = new int[2];
				for (int time = 0; time < 2; time++)
				{
					s.DarkFantasy = time;
					for (int j = 0; j < 40; j++)
					{
						yield return null;
					}
					Texture2D texture2D2 = _mod.GpuRendererForTest?.ReadOutput();
					if (!(texture2D2 == null))
					{
						array[time] = Colours(texture2D2.GetPixels32());
						UnityEngine.Object.Destroy(texture2D2);
					}
				}
				Check("no palette steps under dark fantasy", (float)array[1] > (float)array[0] * 1.3f, $"palette on: {array[0]} colours, with dark fantasy {array[1]}");
				s.GpuLook = 0;
				s.DarkFantasy = 1;
				for (int time = 0; time < 6; time++)
				{
					yield return null;
				}
				int i = _mod.GpuRendererForTest?.Supersample ?? 0;
				_mod.SaveFrame("dark_" + _views[0].Name + "_day_classic" + suffix + ".png");
				s.DarkFantasy = 0;
				for (int time = 0; time < 6; time++)
				{
					yield return null;
				}
				int num5 = _mod.GpuRendererForTest?.Supersample ?? 0;
				Check("the classic finish is drawn finer under dark fantasy", i >= 2 && num5 == 1, $"{num5} -> {i}");
				s.DarkFantasy = 1;
				float start = Time.unscaledTime;
				while (GpuDark.Shown < 0.999f && Time.unscaledTime - start < 2f)
				{
					yield return null;
				}
				Check("the look eases in within half a second", Time.unscaledTime - start < 0.6f, $"{Time.unscaledTime - start:F2} s");
			}
			s.TimeMode = savedTime;
			s.GpuLook = savedLook;
			s.DarkFantasy = savedDark;
			s.Palette = savedPal;
			s.Resolution = savedRes;
			_mod.ForceRelight();
		}

		private IEnumerator DarkRoom(string suffix)
		{
			Settings s = _mod.Settings;
			_mod.DebugEyeSet = false;
			Building building = null;
			Vector2 vector = ((_mod.Host != null) ? _mod.Host.current_position : Vector2.zero);
			float num = float.MaxValue;
			foreach (Building building2 in World.world.buildings)
			{
				if (building2 != null && building2.current_tile != null && HouseInterior.IsEnterable(building2))
				{
					float sqrMagnitude = (new Vector2(building2.current_tile.x, building2.current_tile.y) - vector).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						building = building2;
					}
				}
			}
			if (building == null)
			{
				Log.Info("DARK no enterable house: the room not checked");
				yield break;
			}
			_mod.EnterHouseForTest(building);
			yield return new WaitForSecondsRealtime(1f);
			if (!_mod.Interior.Inside)
			{
				Check("into a room for the dark fantasy look", ok: false);
				yield break;
			}
			int[] array = new int[2] { 1, 3 };
			for (int i = 0; i < array.Length; i++)
			{
				int time = (s.TimeMode = array[i]);
				_mod.ForceRelight();
				for (int j = 0; j < 20; j++)
				{
					yield return null;
				}
				string when = ((time == 3) ? "night" : "day");
				Color32[] off = null;
				Color32[] on = null;
				for (int j = 0; j < 2; j++)
				{
					s.DarkFantasy = j;
					for (int k = 0; k < 40; k++)
					{
						if (!(Mathf.Abs(GpuDark.Shown - (float)j) > 0.001f))
						{
							break;
						}
						yield return null;
					}
					for (int k = 0; k < 4; k++)
					{
						yield return null;
					}
					_mod.SaveFrame("dark_room_" + when + "_" + ((j == 1) ? "on" : "off") + suffix + ".png");
					Texture2D texture2D = _mod.GpuRendererForTest?.ReadOutput();
					if (!(texture2D == null))
					{
						if (j == 0)
						{
							off = texture2D.GetPixels32();
						}
						else
						{
							on = texture2D.GetPixels32();
						}
						UnityEngine.Object.Destroy(texture2D);
					}
				}
				if (off != null && on != null)
				{
					Log.Info($"DARK room {when}: luma {MeanLuma(off):F3} -> {MeanLuma(on):F3}, changed {Diff(off, on):F3}");
					Check("a room keeps its light under dark fantasy (" + when + ")", MeanLuma(on) > MeanLuma(off) * 0.7f, $"{MeanLuma(off):F3} -> {MeanLuma(on):F3}");
				}
			}
			s.DarkFantasy = 0;
			_mod.LeaveRoomForTest();
			yield return new WaitForSecondsRealtime(1f);
		}

		private void Aim(View v)
		{
			_mod.DebugEye = v.Eye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = ((v.Height > 0f) ? v.Height : 0f);
			_mod.SetYaw(v.Yaw);
			_mod.SetPitchDegrees(v.Pitch);
		}

		private static float MeanLuma(Color32[] p)
		{
			double num = 0.0;
			for (int i = 0; i < p.Length; i++)
			{
				Color32 color = p[i];
				num += (double)(int)color.r * 0.3 + (double)(int)color.g * 0.59 + (double)(int)color.b * 0.11;
			}
			if (p.Length == 0)
			{
				return 0f;
			}
			return (float)(num / (double)p.Length / 255.0);
		}

		private static float Blueness(Color32[] p, int w, int h, float top0, float top1)
		{
			double num = 0.0;
			int num2 = 0;
			for (int i = Mathf.FloorToInt((float)h * (1f - top1)); i < Mathf.CeilToInt((float)h * (1f - top0)); i++)
			{
				for (int j = 0; j < w; j++)
				{
					Color32 color = p[i * w + j];
					num += (double)(int)color.b - (double)(color.r + color.g) * 0.5;
					num2++;
				}
			}
			if (num2 <= 0)
			{
				return 0f;
			}
			return (float)(num / (double)num2 / 255.0);
		}

		private static float BandHue(Color32[] p, int w, int h, float top0, float top1)
		{
			double num = 0.0;
			double num2 = 0.0;
			double num3 = 0.0;
			int num4 = 0;
			for (int i = Mathf.FloorToInt((float)h * (1f - top1)); i < Mathf.CeilToInt((float)h * (1f - top0)); i++)
			{
				for (int j = 0; j < w; j++)
				{
					Color32 color = p[i * w + j];
					num += (double)(int)color.r;
					num2 += (double)(int)color.g;
					num3 += (double)(int)color.b;
					num4++;
				}
			}
			if (num4 == 0)
			{
				return 0f;
			}
			Vector3 vector = GpuDark.Lab(new Color((float)(num / (double)num4 / 255.0), (float)(num2 / (double)num4 / 255.0), (float)(num3 / (double)num4 / 255.0)));
			return Mathf.Atan2(vector.z, vector.y) * 57.29578f;
		}

		private static float MeanBlue(Color32[] p, int w, int h, float top0, float top1)
		{
			double num = 0.0;
			int num2 = 0;
			for (int i = Mathf.FloorToInt((float)h * (1f - top1)); i < Mathf.CeilToInt((float)h * (1f - top0)); i++)
			{
				for (int j = 0; j < w; j++)
				{
					num += (double)(int)p[i * w + j].b;
					num2++;
				}
			}
			if (num2 <= 0)
			{
				return 0f;
			}
			return (float)(num / (double)num2 / 255.0);
		}

		private static int Colours(Color32[] p)
		{
			HashSet<int> hashSet = new HashSet<int>();
			for (int i = 0; i < p.Length; i++)
			{
				Color32 color = p[i];
				hashSet.Add(color.r | (color.g << 8) | (color.b << 16));
			}
			return hashSet.Count;
		}

		private static float Diff(Color32[] a, Color32[] b)
		{
			if (a.Length != b.Length)
			{
				return 1f;
			}
			double num = 0.0;
			for (int i = 0; i < a.Length; i++)
			{
				num += (double)(Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b));
			}
			if (a.Length == 0)
			{
				return 0f;
			}
			return (float)(num / (double)a.Length / 3.0 / 255.0);
		}

		private IEnumerator Sharp(string tag)
		{
			Settings s = _mod.Settings;
			int savedRes = s.Resolution;
			int savedPx = s.PixelDistance;
			int savedLook = s.GpuLook;
			int savedPal = s.Palette;
			bool savedTrial = s.ResolutionTrial;
			string suffix = ((tag.Length > 0) ? ("_" + tag) : "");
			int[] resolutionWidths = WorldBoxMod.ResolutionWidths;
			Log.Info($"SHARP screen {Screen.width}x{Screen.height}, card '{SystemInfo.graphicsDeviceName}' {SystemInfo.graphicsMemorySize} MB");
			Check("sharp: four resolutions, half the screen third and 1920 last", resolutionWidths.Length == 4 && resolutionWidths[2] == Screen.width / 2 && resolutionWidths[3] == 1920 && WorldBoxMod.ResolutionName(resolutionWidths[2]) == Screen.width / 2 + "x" + Screen.height / 2, string.Join(", ", Array.ConvertAll(resolutionWidths, WorldBoxMod.ResolutionName)));
			(string, int, bool)[] array = new(string, int, bool)[19]
			{
				("AMD Radeon RX 6700 XT", 12272, true),
				("NVIDIA GeForce RTX 3060", 12288, true),
				("NVIDIA GeForce GTX 1650", 4096, true),
				("Intel(R) Arc(TM) A770 Graphics", 16288, true),
				("Intel(R) Arc(TM) B580 Graphics", 12216, true),
				("AMD Radeon RX 7600M XT", 8176, true),
				("NVIDIA GeForce GTX 1050", 2048, false),
				("NVIDIA GeForce MX450", 2048, false),
				("Intel(R) UHD Graphics 620", 4096, false),
				("Intel(R) Iris(R) Xe Graphics", 8192, false),
				("Intel(R) Arc(TM) Graphics", 8192, false),
				("Intel(R) Arc(TM) 140V GPU", 8192, false),
				("AMD Radeon(TM) Graphics", 4096, false),
				("AMD Radeon Graphics", 8192, false),
				("AMD Radeon(TM) Vega 8 Graphics", 4096, false),
				("AMD Radeon 780M Graphics", 8192, false),
				("AMD Custom GPU 0405", 4096, false),
				("Apple M1", 8192, false),
				("Microsoft Basic Render Driver", 4096, false)
			};
			int num = 0;
			string text = "";
			(string, int, bool)[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				(string, int, bool) tuple = array2[i];
				if (WorldBoxMod.StrongCard(tuple.Item1, tuple.Item2, compute: true) == tuple.Item3)
				{
					num++;
				}
				else
				{
					text = text + tuple.Item1 + "; ";
				}
			}
			Check("sharp: strong cards told from shared chips", num == array.Length, (text.Length > 0) ? text : $"{num} cards");
			Check("sharp: no compute shaders, not strong", !WorldBoxMod.StrongCard("AMD Radeon RX 6700 XT", 12272, compute: false));
			string file = Path.Combine(Log.Folder, "settings.txt");
			string text2 = (File.Exists(file) ? File.ReadAllText(file) : null);
			try
			{
				Settings settings = Read("version=3\nresolution=2\n");
				Settings settings2 = Read("version=3\nresolution=1\n");
				Settings settings3 = Read("version=4\nresolution=2\nresolution_trial=1\n");
				Settings settings4 = Read(null);
				Check("sharp: a version 3 file's 1920 is index 3 now", settings.Resolution == 3 && settings.Upgraded && !settings.Fresh, $"{settings.Resolution}, upgraded {settings.Upgraded}");
				Check("sharp: a version 3 file's 640 stays", settings2.Resolution == 1, settings2.Resolution.ToString());
				Check("sharp: a version 4 file keeps half the screen and its trial", settings3.Resolution == 2 && settings3.ResolutionTrial && !settings3.Upgraded, $"{settings3.Resolution}, trial {settings3.ResolutionTrial}");
				Check("sharp: no file is a new player", settings4.Fresh && settings4.Resolution == 0, $"fresh {settings4.Fresh}");
			}
			finally
			{
				if (text2 != null)
				{
					File.WriteAllText(file, text2);
				}
				else if (File.Exists(file))
				{
					File.Delete(file);
				}
			}
			bool flag = WorldBoxMod.StrongCard(SystemInfo.graphicsDeviceName, SystemInfo.graphicsMemorySize, SystemInfo.supportsComputeShaders) || WorldBoxMod.AppleSilicon(SystemInfo.graphicsDeviceName);
			_mod.ChooseFirstResolutionForTest();
			Check("sharp: a new player's resolution follows the card", (!flag) ? (s.Resolution == 0 && !s.ResolutionTrial) : (s.Resolution == 2 && s.ResolutionTrial), $"strong {flag}, resolution {s.Resolution}, trial {s.ResolutionTrial}");
			s.GpuRender = true;
			s.ResolutionTrial = false;
			s.GpuLook = 1;
			s.Palette = 1;
			s.PixelDistance = 1;
			_ = _mod.GpuRendererForTest;
			int sharpW = Screen.width / 2;
			int sharpH = Screen.height / 2;
			foreach (View v in _views)
			{
				if (v.Name == "street" || v.Name == "hills")
				{
					continue;
				}
				_mod.DebugEye = v.Eye;
				_mod.DebugEyeSet = true;
				_mod.DebugEyeHeight = ((v.Height > 0f) ? v.Height : 0f);
				_mod.SetYaw(v.Yaw);
				_mod.SetPitchDegrees(v.Pitch);
				int[] array3 = new int[3] { 1, 2, 3 };
				for (int j = 0; j < array3.Length; j++)
				{
					int res = (s.Resolution = array3[j]);
					for (int k = 0; k < 8; k++)
					{
						yield return null;
					}
					GpuRenderer gpuRendererForTest = _mod.GpuRendererForTest;
					if (gpuRendererForTest == null || gpuRendererForTest.Output == null)
					{
						Check("sharp " + v.Name + ": the card drew", ok: false, gpuRendererForTest?.Error);
						continue;
					}
					int width = gpuRendererForTest.Output.width;
					int height = gpuRendererForTest.Output.height;
					_mod.SaveFrame($"sharp_{v.Name}_{width}{suffix}.png");
					bool flag2 = Screen.width % width == 0 && Screen.height % height == 0;
					if (res == 2)
					{
						Check("sharp " + v.Name + ": half the screen, point sampled, the 3D drawn at the screen's size under it", width == sharpW && height == sharpH && !gpuRendererForTest.Presented && _mod.ShownFrame == gpuRendererForTest.Output && gpuRendererForTest.Output.filterMode == FilterMode.Point && gpuRendererForTest.Supersample == Mathf.Min(2, 2560 / width), $"{width}x{height} x{gpuRendererForTest.Supersample}, presented {gpuRendererForTest.Presented}, filter {gpuRendererForTest.Output.filterMode}");
					}
					else if (flag2)
					{
						Check($"sharp {v.Name} {width}: a whole multiple of the screen, shown as it is", !gpuRendererForTest.Presented && _mod.ShownFrame == gpuRendererForTest.Output && gpuRendererForTest.Output.filterMode == FilterMode.Point, $"presented {gpuRendererForTest.Presented}, filter {gpuRendererForTest.Output.filterMode}");
					}
					else if (width < Screen.width)
					{
						bool flag3 = gpuRendererForTest.Presented && _mod.ShownFrame == gpuRendererForTest.Shown && gpuRendererForTest.Shown.width == Screen.width && gpuRendererForTest.Shown.height == Screen.height;
						Check($"sharp {v.Name} {width}: put on the screen sharply", flag3, $"presented {gpuRendererForTest.Presented}, shown {gpuRendererForTest.Shown.width}x{gpuRendererForTest.Shown.height}");
						if (flag3)
						{
							PresentCheck(gpuRendererForTest, $"sharp_{v.Name}_{width}_shown{suffix}.png", v.Name, width);
						}
					}
					if (gpuRendererForTest.Error != null)
					{
						Log.Error("SHARP " + v.Name + ": " + gpuRendererForTest.Error);
					}
				}
			}
			if (_views.Count > 0)
			{
				View view = _views[0];
				_mod.DebugEye = view.Eye;
				_mod.DebugEyeHeight = ((view.Height > 0f) ? view.Height : 0f);
				_mod.SetYaw(view.Yaw);
				_mod.SetPitchDegrees(view.Pitch);
				int[] array3 = new int[3] { 1, 2, 3 };
				for (int j = 0; j < array3.Length; j++)
				{
					int res = (s.Resolution = array3[j]);
					for (int l = 0; l < 20; l++)
					{
						yield return null;
					}
					float start = Time.realtimeSinceStartup;
					int k = 0;
					while (Time.realtimeSinceStartup - start < 3f)
					{
						k++;
						yield return null;
					}
					float num2 = Time.realtimeSinceStartup - start;
					Log.Info($"SHARP PERF {WorldBoxMod.ResolutionName(WorldBoxMod.ResolutionWidths[res])}: {(float)k / num2:F1} fps ({_mod.GpuRendererForTest?.Stats})");
				}
			}
			_mod.TrialSecondsForTest = 1.2f;
			s.Resolution = 2;
			s.ResolutionTrial = true;
			_mod.TrialFrameMsForTest = 40f;
			_mod.TrialProbeMsForTest = 12f;
			int probed = 0;
			for (float start = 0f; start < 15f; start += Time.unscaledDeltaTime)
			{
				if (!s.ResolutionTrial)
				{
					break;
				}
				yield return null;
				GpuRenderer gpuRendererForTest2 = _mod.GpuRendererForTest;
				if (s.ResolutionTrial && gpuRendererForTest2 != null && gpuRendererForTest2.Output != null && gpuRendererForTest2.Output.width == WorldBoxMod.ResolutionWidths[0])
				{
					probed++;
				}
			}
			Check("sharp: slow at 2x, quicker at 480x270: 480x270 kept", s.Resolution == 0 && !s.ResolutionTrial && probed > 0, $"resolution {s.Resolution}, trial {s.ResolutionTrial}, frames tried at 480 {probed}");
			s.Resolution = 2;
			s.ResolutionTrial = true;
			_mod.TrialProbeMsForTest = 38f;
			for (float start = 0f; start < 15f; start += Time.unscaledDeltaTime)
			{
				if (!s.ResolutionTrial)
				{
					break;
				}
				yield return null;
			}
			Check("sharp: slow at 2x and as slow at 480x270: the game is slow, 2x kept", s.Resolution == 2 && !s.ResolutionTrial, $"resolution {s.Resolution}, trial {s.ResolutionTrial}");
			for (int j = 0; j < 4; j++)
			{
				yield return null;
			}
			GpuRenderer gpuRendererForTest3 = _mod.GpuRendererForTest;
			Check("sharp: drawn at 2x again after the try", gpuRendererForTest3 != null && gpuRendererForTest3.Output != null && gpuRendererForTest3.Output.width == WorldBoxMod.SharpWidth, (gpuRendererForTest3 != null && gpuRendererForTest3.Output != null) ? gpuRendererForTest3.Output.width.ToString() : "none");
			s.ResolutionTrial = true;
			_mod.TrialFrameMsForTest = 10f;
			for (float start = 0f; start < 15f; start += Time.unscaledDeltaTime)
			{
				if (!s.ResolutionTrial)
				{
					break;
				}
				yield return null;
			}
			Check("sharp: quick frames in the trial keep it", s.Resolution == 2 && !s.ResolutionTrial, $"resolution {s.Resolution}, trial {s.ResolutionTrial}");
			s.ResolutionTrial = true;
			s.Resolution = 3;
			for (int j = 0; j < 6; j++)
			{
				yield return null;
			}
			Check("sharp: a resolution chosen in the trial ends it", s.Resolution == 3 && !s.ResolutionTrial, $"resolution {s.Resolution}, trial {s.ResolutionTrial}");
			_mod.TrialSecondsForTest = (_mod.TrialFrameMsForTest = (_mod.TrialProbeMsForTest = -1f));
			s.Resolution = savedRes;
			s.PixelDistance = savedPx;
			s.GpuLook = savedLook;
			s.Palette = savedPal;
			s.ResolutionTrial = savedTrial;
			Settings Read(string text3)
			{
				if (text3 == null)
				{
					if (File.Exists(file))
					{
						File.Delete(file);
					}
				}
				else
				{
					File.WriteAllText(file, text3);
				}
				return Settings.Load();
			}
		}

		private void PresentCheck(GpuRenderer g, string png, string view, int w)
		{
			Texture2D texture2D = Read(g.Output);
			Texture2D texture2D2 = Read(g.Shown);
			if (texture2D == null || texture2D2 == null)
			{
				Check($"sharp {view} {w}: read back", ok: false);
				return;
			}
			File.WriteAllBytes(Path.Combine(Log.Folder, png), texture2D2.EncodeToPNG());
			Color32[] pixels = texture2D.GetPixels32();
			Color32[] pixels2 = texture2D2.GetPixels32();
			int width = texture2D.width;
			int height = texture2D.height;
			int width2 = texture2D2.width;
			int height2 = texture2D2.height;
			float num = (float)width2 / (float)width;
			float num2 = (float)height2 / (float)height;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			for (int i = 0; i < height2; i += 3)
			{
				for (int j = 0; j < width2; j += 3)
				{
					float num7 = ((float)j + 0.5f) / num;
					float num8 = ((float)i + 0.5f) / num2;
					int num9 = Mathf.Min(width - 1, (int)num7);
					int num10 = Mathf.Min(height - 1, (int)num8);
					float f = num7 - (float)num9 - 0.5f;
					float f2 = num8 - (float)num10 - 0.5f;
					Color32 p = pixels2[i * width2 + j];
					if (Mathf.Abs(f) <= 0.5f - 0.5f / num + 0.001f && Mathf.Abs(f2) <= 0.5f - 0.5f / num2 + 0.001f)
					{
						num3++;
						if (Near(p, pixels[num10 * width + num9], 1))
						{
							num4++;
						}
					}
					int num11 = 255;
					int num12 = 255;
					int num13 = 255;
					int num14 = 0;
					int num15 = 0;
					int num16 = 0;
					for (int k = -1; k <= 1; k++)
					{
						for (int l = -1; l <= 1; l++)
						{
							Color32 color = pixels[Mathf.Clamp(num10 + k, 0, height - 1) * width + Mathf.Clamp(num9 + l, 0, width - 1)];
							num11 = Mathf.Min(num11, color.r);
							num12 = Mathf.Min(num12, color.g);
							num13 = Mathf.Min(num13, color.b);
							num14 = Mathf.Max(num14, color.r);
							num15 = Mathf.Max(num15, color.g);
							num16 = Mathf.Max(num16, color.b);
						}
					}
					if (p.r >= num11 - 1 && p.r <= num14 + 1 && p.g >= num12 - 1 && p.g <= num15 + 1 && p.b >= num13 - 1 && p.b <= num16 + 1)
					{
						num5++;
					}
					num6++;
				}
			}
			Check($"sharp {view} {w}: screen pixels well inside a picture pixel are its colour", num3 > 0 && (float)num4 >= (float)num3 * 0.99f, $"{num4} of {num3}");
			Check($"sharp {view} {w}: the rest blend only neighbours", (float)num5 >= (float)num6 * 0.99f, $"{num5} of {num6}");
			UnityEngine.Object.Destroy(texture2D);
			UnityEngine.Object.Destroy(texture2D2);
		}

		private static bool Near(Color32 p, Color32 q, int tolerance)
		{
			if (Mathf.Abs(p.r - q.r) <= tolerance && Mathf.Abs(p.g - q.g) <= tolerance)
			{
				return Mathf.Abs(p.b - q.b) <= tolerance;
			}
			return false;
		}

		private static Texture2D Read(RenderTexture rt)
		{
			if (rt == null)
			{
				return null;
			}
			Texture2D texture2D = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, mipChain: false);
			RenderTexture active = RenderTexture.active;
			RenderTexture.active = rt;
			texture2D.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
			texture2D.Apply();
			RenderTexture.active = active;
			return texture2D;
		}

		private IEnumerator Tufts(Vector2 near, bool biomes)
		{
			Settings s = _mod.Settings;
			TerrainCache terrainForTest = _mod.TerrainForTest;
			Vector2 spot = Vector2.zero;
			float num = 1E+09f;
			for (int i = 0; i < 60; i++)
			{
				if (terrainForTest == null)
				{
					break;
				}
				for (int j = (int)near.y - i; j <= (int)near.y + i; j++)
				{
					for (int k = (int)near.x - i; k <= (int)near.x + i; k++)
					{
						if (k < 4 || j < 4 || k >= terrainForTest.Width - 4 || j >= terrainForTest.Height - 4)
						{
							continue;
						}
						bool flag = true;
						for (int l = -3; l <= 3 && flag; l++)
						{
							for (int m = -3; m <= 3 && flag; m++)
							{
								int num2 = (j + l) * terrainForTest.Width + k + m;
								flag = terrainForTest.Materials[num2] == 1 && (terrainForTest.Flags[num2] & 0xD) == 0 && (terrainForTest.PathSlot.Length == 0 || terrainForTest.PathSlot[num2] == 0);
							}
						}
						float num3 = ((float)k - near.x) * ((float)k - near.x) + ((float)j - near.y) * ((float)j - near.y);
						if (flag && num3 < num)
						{
							num = num3;
							spot = new Vector2((float)k + 0.5f, (float)j - 2.5f);
						}
					}
				}
			}
			if (num > 100000000f)
			{
				Log.Error("TUFTS no open grass found");
				yield break;
			}
			int savedTime = s.TimeMode;
			s.GpuRender = true;
			s.GpuLook = 1;
			s.Resolution = 2;
			s.TimeMode = 0;
			_mod.Day.TimeOfDay = 0.02f;
			_mod.DebugEye = spot;
			_mod.DebugEyeSet = true;
			_mod.SetYaw(90f);
			_mod.SetPitchDegrees(-30f);
			for (int n = 0; n < 60; n++)
			{
				yield return null;
			}
			_mod.SaveFrame("tufts_before_switch.png");
			if (TuftsResolution >= 0)
			{
				s.Resolution = TuftsResolution;
			}
			for (int n = 0; n < 30; n++)
			{
				yield return null;
			}
			_mod.DebugEye = spot;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = 0f;
			_mod.SetYaw(90f);
			_mod.SetPitchDegrees(-30f);
			int[] array = new int[5] { 10, 1, 3, 11, 12 };
			foreach (int time in array)
			{
				s.TimeMode = ((time < 10) ? time : 0);
				if (time >= 10)
				{
					DayCycle day = _mod.Day;
					float timeOfDay;
					switch (time)
					{
					default:
						timeOfDay = 0.17f;
						break;
					case 11:
						timeOfDay = 0.88f;
						break;
					case 10:
						timeOfDay = 0.02f;
						break;
					}
					day.TimeOfDay = timeOfDay;
				}
				_mod.ForceRelight();
				for (int num4 = 0; num4 < 30; num4++)
				{
					yield return null;
				}
				object obj;
				switch (time)
				{
				default:
					obj = "day";
					break;
				case 12:
					obj = "predawn";
					break;
				case 11:
					obj = "evening";
					break;
				case 10:
					obj = "midnight";
					break;
				case 3:
					obj = "night";
					break;
				}
				string when = (string)obj;
				Color32[] on = null;
				Color32[] off = null;
				bool[] array2 = new bool[2] { true, false };
				foreach (bool tufts in array2)
				{
					_mod.SceneForTest.Tufts = tufts;
					for (int num5 = 0; num5 < 6; num5++)
					{
						yield return null;
					}
					_mod.SaveFrame("tufts_" + when + "_" + (tufts ? "on" : "off") + ".png");
					Texture2D texture2D = _mod.GpuRendererForTest?.ReadOutput();
					if (!(texture2D == null))
					{
						if (tufts)
						{
							on = texture2D.GetPixels32();
						}
						else
						{
							off = texture2D.GetPixels32();
						}
						UnityEngine.Object.Destroy(texture2D);
					}
				}
				_mod.SceneForTest.Tufts = true;
				if (on == null || off == null || on.Length != off.Length)
				{
					continue;
				}
				double num6 = 0.0;
				double num7 = 0.0;
				int num8 = 0;
				for (int num9 = 0; num9 < on.Length; num9++)
				{
					Color32 color = on[num9];
					Color32 color2 = off[num9];
					if (Mathf.Abs(color.r - color2.r) + Mathf.Abs(color.g - color2.g) + Mathf.Abs(color.b - color2.b) >= 24)
					{
						num6 += (double)(int)color.r * 0.3 + (double)(int)color.g * 0.59 + (double)(int)color.b * 0.11;
						num7 += (double)(int)color2.r * 0.3 + (double)(int)color2.g * 0.59 + (double)(int)color2.b * 0.11;
						num8++;
					}
				}
				float num10 = ((num8 > 0) ? ((float)(num6 / (double)num8 / 255.0)) : 0f);
				float num11 = ((num8 > 0) ? ((float)(num7 / (double)num8 / 255.0)) : 0f);
				Log.Info($"TUFTS {when} at {spot}: {num8} pixels, tufts luma {num10:F3}, ground under them {num11:F3}, ratio {((num11 > 0f) ? (num10 / num11) : 0f):F2}, frame {MeanLuma(off):F3}");
				if (time >= 3)
				{
					Check(when + " tufts no brighter than their ground", num8 > 0 && num10 <= num11 * 1.15f, $"{num10:F3} vs {num11:F3}");
				}
			}
			if (biomes)
			{
				yield return TuftBiomes(spot);
			}
			s.TimeMode = savedTime;
			_mod.ForceRelight();
		}

		private IEnumerator TuftBiomes(Vector2 spot)
		{
			_mod.Settings.TimeMode = 3;
			List<string> list = new List<string>();
			foreach (TopTileType item in AssetManager.top_tiles.list)
			{
				if (item != null && item.id.EndsWith("_low"))
				{
					list.Add(item.id);
				}
			}
			foreach (string id in list)
			{
				TopTileType pTopTile = AssetManager.top_tiles.get(id);
				for (int i = (int)spot.y - 4; i < (int)spot.y + 14; i++)
				{
					for (int j = (int)spot.x - 9; j < (int)spot.x + 10; j++)
					{
						WorldTile tile = World.world.GetTile(j, i);
						if (tile != null && tile.main_type != null && !tile.main_type.liquid && !tile.main_type.lava)
						{
							try
							{
								tile.setTopTileType(pTopTile);
							}
							catch
							{
							}
						}
					}
				}
				_mod.ForceRelight();
				for (int k = 0; k < 20; k++)
				{
					yield return null;
				}
				TerrainCache land = _mod.TerrainForTest;
				int c = ((int)spot.y + 4) * land.Width + (int)spot.x;
				Color32[] on = null;
				Color32[] off = null;
				bool[] array = new bool[2] { true, false };
				foreach (bool tufts in array)
				{
					_mod.SceneForTest.Tufts = tufts;
					for (int l = 0; l < 6; l++)
					{
						yield return null;
					}
					if (tufts)
					{
						_mod.SaveFrame("tufts_biome_" + id + ".png");
					}
					Texture2D texture2D = _mod.GpuRendererForTest?.ReadOutput();
					if (!(texture2D == null))
					{
						if (tufts)
						{
							on = texture2D.GetPixels32();
						}
						else
						{
							off = texture2D.GetPixels32();
						}
						UnityEngine.Object.Destroy(texture2D);
					}
				}
				_mod.SceneForTest.Tufts = true;
				if (on == null || off == null)
				{
					continue;
				}
				double num = 0.0;
				double num2 = 0.0;
				double num3 = 0.0;
				double num4 = 0.0;
				double num5 = 0.0;
				double num6 = 0.0;
				int num7 = 0;
				for (int m = 0; m < on.Length; m++)
				{
					Color32 color = on[m];
					Color32 color2 = off[m];
					if (Mathf.Abs(color.r - color2.r) + Mathf.Abs(color.g - color2.g) + Mathf.Abs(color.b - color2.b) >= 24)
					{
						num += (double)(int)color.r;
						num2 += (double)(int)color.g;
						num3 += (double)(int)color.b;
						num4 += (double)(int)color2.r;
						num5 += (double)(int)color2.g;
						num6 += (double)(int)color2.b;
						num7++;
					}
				}
				if (num7 == 0)
				{
					Log.Info($"TUFTS biome {id}: mat {land.Materials[c]}, no tufts");
					continue;
				}
				uint num8 = land.Colors[c];
				Log.Info($"TUFTS biome {id}: mat {land.Materials[c]}, colour {num8 & 0xFF},{(num8 >> 8) & 0xFF},{(num8 >> 16) & 0xFF}, {num7} px, tufts {num / (double)num7:F0},{num2 / (double)num7:F0},{num3 / (double)num7:F0} ground {num4 / (double)num7:F0},{num5 / (double)num7:F0},{num6 / (double)num7:F0}");
			}
		}
	}
}
