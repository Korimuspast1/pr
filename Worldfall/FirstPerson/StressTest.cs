using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using FirstPerson.Core;
using FirstPerson.Gpu;
using UnityEngine;
using UnityEngine.Profiling;

namespace FirstPerson
{
	[DefaultExecutionOrder(-30000)]
	internal sealed class StressTest : MonoBehaviour
	{
		private struct Spot
		{
			public string Name;

			public Vector2 Eye;

			public float Height;

			public float Yaw;

			public float Pitch;
		}

		private sealed class Block
		{
			public string Label;

			public int Frames;

			public float P50;

			public float P95;

			public float Updates;

			public float UpdatesCpu;

			public float ModUpd;

			public float Late;

			public float LateCpu;

			public float After;

			public float AfterCpu;

			public float Scene;

			public float Submit;

			public float HeapMb;

			public int Gc;

			public SceneClock.Split Split;
		}

		private const float BlockSeconds = 4f;

		private const int DropFrames = 10;

		private WorldBoxMod _mod;

		private bool _quit;

		private bool _timing;

		private bool _cameraFailed;

		private Vector2 _at;

		private float _zoom = 71f;

		private int _pass;

		private int _fail;

		private readonly List<string> _summary = new List<string>();

		private readonly List<Spot> _spots = new List<Spot>();

		private Block _lastOff;

		private IEnumerator Start()
		{
			Stack<IEnumerator> stack = new Stack<IEnumerator>();
			stack.Push(Run());
			while (stack.Count > 0)
			{
				IEnumerator enumerator = stack.Peek();
				object current;
				try
				{
					if (!enumerator.MoveNext())
					{
						stack.Pop();
						continue;
					}
					current = enumerator.Current;
				}
				catch (Exception ex)
				{
					Log.Error("STRESS stopped by an error: " + ex);
					_fail++;
					break;
				}
				if (current is IEnumerator item && !(current is CustomYieldInstruction))
				{
					stack.Push(item);
				}
				else
				{
					yield return current;
				}
			}
			Finish();
		}

		private void LateUpdate()
		{
			if (_timing)
			{
				if (QualitySettings.vSyncCount != 0)
				{
					QualitySettings.vSyncCount = 0;
				}
				if (Application.targetFrameRate != -1)
				{
					Application.targetFrameRate = -1;
				}
			}
		}

		private IEnumerator Run()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(path);
			_quit = options.Contains("quit");
			bool quick = options.Contains("quick");
			int slot = CityPerfTest.SaveSlot(options);
			if (slot <= 0)
			{
				slot = 35;
			}
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
			string path2 = Path.Combine(Path.Combine(Path.Combine(Application.persistentDataPath, "saves"), "save" + slot), "map.wbox");
			if (File.Exists(path2))
			{
				try
				{
					SaveManager.loadMapFromBytes(File.ReadAllBytes(path2));
				}
				catch (Exception ex)
				{
					Report(ok: false, "load save " + slot, ex.Message);
					yield break;
				}
				for (float t = 0f; t < 90f; t += 0.5f)
				{
					yield return new WaitForSecondsRealtime(0.5f);
					bool flag2 = true;
					try
					{
						flag2 = SmoothLoader.isLoading();
					}
					catch
					{
					}
					if (!flag2 && t > 3f)
					{
						break;
					}
				}
				Config.autosaves = false;
			}
			else
			{
				Log.Info("STRESS no save in slot " + slot + ": the starting map");
			}
			Config.paused = false;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			yield return new WaitForSecondsRealtime(8f);
			WorldTile centre;
			int crowd;
			City town;
			Actor host = CityPerfTest.BusiestTown(out centre, out crowd, out town);
			if (centre == null)
			{
				Report(ok: false, "a town to stand in");
				yield break;
			}
			_at = new Vector2((float)centre.x + 0.5f, (float)centre.y + 0.5f);
			Settings s = _mod.Settings;
			int count = World.world.units.getSimpleList().Count;
			int num = 0;
			try
			{
				num = World.world.buildings.Count;
			}
			catch
			{
			}
			Log.Info($"STRESS world: save {slot}, {MapBox.width}x{MapBox.height}, {count} creatures, {num} buildings; busiest town {town?.name} ({crowd} within 15) at {_at.x:F0},{_at.y:F0}; " + string.Format("settings: gpu {0}, resolution {1}, draw {2}, detail {3:F0}, creatures {4:F0}, far models {5}, ", s.GpuRender, s.Resolution, (s.DrawDistance >= 1200f) ? "whole" : s.DrawDistance.ToString("F0"), s.DetailDistance, s.CreatureDistance, s.FarModels) + string.Format("shadows {0} ({1:F0} tiles), finish {2}, pixel distance {3}, biome fx {4} x{5:F1}, offscreen {6}, far poses {7}, crowd {8}, ", s.Shadows, s.ShadowDistance, (s.GpuLook == 0) ? "classic" : "painterly", s.PixelDistance, s.BiomeFx, s.BiomeParticles, s.OffscreenMotion, s.FarPoses, s.CrowdDetail) + $"god view {s.GodView} zoom {s.GodViewZoom:F0}, world steps {s.WorldSteps}; {SystemInfo.processorType} ({SystemInfo.processorCount} threads), {SystemInfo.graphicsDeviceName}, screen {Screen.width}x{Screen.height}");
			Probe("at the start");
			SceneClock.SampleEveryFrame = true;
			WorldBoxMod.LandBesideForTest = true;
			_timing = true;
			bool godWas = s.GodView;
			if (!options.Contains("nomap"))
			{
				if (s.GodView)
				{
					_mod.SetGodView(on: false);
				}
				yield return Settle(3f);
				yield return Measure("map flat", null);
				yield return Measure("map flat", null);
				_mod.SetGodView(on: true);
				float[] array = new float[2] { 71f, 40f };
				for (int i = 0; i < array.Length; i++)
				{
					float t = (_zoom = array[i]);
					yield return Settle(4f);
					yield return Measure($"map 3D god view zoom {t:F0}", null);
				}
				_mod.SetGodView(godWas);
			}
			if (host == null)
			{
				Report(ok: false, "a grown-up in the town to take");
				yield break;
			}
			ControllableUnit.setControllableCreature(host);
			for (float t = 0f; t < 8f; t += Time.unscaledDeltaTime)
			{
				if (_mod.IsFirstPerson)
				{
					break;
				}
				yield return null;
			}
			if (!Report(_mod.IsFirstPerson, "first person in " + (town?.name ?? "the town")))
			{
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
			try
			{
				if (host.is_inside_building)
				{
					host.exitBuilding();
				}
				host.makeWait(30f);
			}
			catch
			{
			}
			PlanSpots(_at);
			yield return Settle(3f);
			foreach (Spot spot in _spots)
			{
				if (!StillFirstPerson(host))
				{
					yield break;
				}
				Place(spot);
				Log.Info($"STRESS spot {spot.Name}: eye {spot.Eye.x:F1},{spot.Eye.y:F1} +{spot.Height:F1}, yaw {spot.Yaw * 57.29578f:F0}, pitch {spot.Pitch:F1}");
				yield return Settle(3f, spot);
				Probe("before " + spot.Name);
				List<Block> a = new List<Block>();
				yield return Measure(spot.Name + " as is", a, spot);
				yield return Measure(spot.Name + " as is", a, spot);
				if (a.Count == 2)
				{
					_lastOff = a[1];
					_summary.Add($"{spot.Name} as is: frame p50 {(a[0].P50 + a[1].P50) * 0.5f:F1} ms (noise {Mathf.Abs(a[0].P50 - a[1].P50):F1}); updates {(a[0].UpdatesCpu + a[1].UpdatesCpu) * 0.5f:F1}, mod late {(a[0].LateCpu + a[1].LateCpu) * 0.5f:F1}, after {(a[0].AfterCpu + a[1].AfterCpu) * 0.5f:F1} ms cpu; " + $"scene {(a[0].Scene + a[1].Scene) * 0.5f:F1}, submit {(a[0].Submit + a[1].Submit) * 0.5f:F1} ms; people {a[0].Split.People:F0}, tracked {a[0].Split[SceneClock.N.Tracked]:F0}, visited {a[0].Split[SceneClock.N.Visited]:F0}");
				}
				if (options.Contains("pictures"))
				{
					yield return Pictures(spot);
				}
				if (!quick)
				{
					yield return Variant(spot, "no creatures gathered", delegate(bool v)
					{
						SceneCollector.SkipUnitsForTest = v;
					});
					yield return Variant(spot, "no buildings gathered", delegate(bool v)
					{
						SceneCollector.SkipBuildingsForTest = v;
					});
					yield return Variant(spot, "world paused", delegate(bool v)
					{
						Config.paused = v;
					});
					yield return Variant(spot, "far creatures full", delegate(bool v)
					{
						SimLod.Off = v;
					});
					int chunk0 = s.ChunkWalk;
					yield return Variant(spot, "chunk walk on", delegate(bool v)
					{
						s.ChunkWalk = (v ? 1 : chunk0);
					});
					yield return Variant(spot, "glide off", delegate(bool v)
					{
						People.GlideOff = v;
					});
					yield return Variant(spot, "building cells off", delegate(bool v)
					{
						SceneCollector.BuildingCellsOff = v;
					});
					yield return Variant(spot, "map hold off", delegate(bool v)
					{
						WorldBoxMod.MapHoldFirstPersonOff = v;
					});
					yield return Variant(spot, "land beside off", delegate(bool v)
					{
						WorldBoxMod.LandBesideOff = v;
					});
					yield return Variant(spot, "kept trees off", delegate(bool v)
					{
						SceneCollector.KeptTreesOff = v;
					});
					yield return Variant(spot, "off-screen creatures full", delegate(bool v)
					{
						s.OffscreenMotion = ((!v) ? 1 : 0);
					});
					yield return Variant(spot, "far poses", delegate(bool v)
					{
						s.FarPoses = (v ? 1 : 0);
					});
					int crowd2 = s.CrowdDetail;
					yield return Variant(spot, "crowd detail Low", delegate(bool v)
					{
						s.CrowdDetail = (v ? 3 : crowd2);
					});
					yield return Variant(spot, "crowd detail Medium", delegate(bool v)
					{
						s.CrowdDetail = (v ? 2 : crowd2);
					});
					float creatures0 = s.CreatureDistance;
					float detail0 = s.DetailDistance;
					yield return Variant(spot, "creatures 200", delegate(bool v)
					{
						s.CreatureDistance = (v ? 200f : creatures0);
					});
					yield return Variant(spot, "creatures 400", delegate(bool v)
					{
						s.CreatureDistance = (v ? 400f : creatures0);
					});
					yield return Variant(spot, "detail 80", delegate(bool v)
					{
						s.DetailDistance = (v ? 80f : detail0);
					});
					int farModels0 = s.FarModels;
					int shadows0 = s.Shadows;
					int res0 = s.Resolution;
					int look0 = s.GpuLook;
					int biome0 = s.BiomeFx;
					yield return Variant(spot, "far models off", delegate(bool v)
					{
						s.FarModels = ((!v) ? farModels0 : 0);
					});
					yield return Variant(spot, "shadows off", delegate(bool v)
					{
						s.Shadows = ((!v) ? shadows0 : 0);
					});
					yield return Variant(spot, "biome fx off", delegate(bool v)
					{
						s.BiomeFx = ((!v) ? biome0 : 0);
					});
					yield return Variant(spot, "picture 640 wide", delegate(bool v)
					{
						s.Resolution = (v ? 1 : res0);
					});
					yield return Variant(spot, "classic finish", delegate(bool v)
					{
						s.GpuLook = ((!v) ? look0 : 0);
					});
					Probe("after " + spot.Name);
				}
			}
		}

		private IEnumerator Variant(Spot spot, string name, Action<bool> set)
		{
			if (!StillFirstPerson(_mod.Host))
			{
				yield break;
			}
			Block before = _lastOff;
			set(obj: true);
			yield return Settle(1.5f, spot);
			List<Block> on = new List<Block>();
			yield return Measure(spot.Name + " " + name + " on", on, spot);
			set(obj: false);
			yield return Settle(1.5f, spot);
			List<Block> off = new List<Block>();
			yield return Measure(spot.Name + " " + name + " off", off, spot);
			if (on.Count != 0 && off.Count != 0)
			{
				Block block = on[0];
				Block block2 = (_lastOff = off[0]);
				if (before != null)
				{
					block2 = new Block
					{
						P50 = (before.P50 + block2.P50) * 0.5f,
						UpdatesCpu = (before.UpdatesCpu + block2.UpdatesCpu) * 0.5f,
						LateCpu = (before.LateCpu + block2.LateCpu) * 0.5f,
						AfterCpu = (before.AfterCpu + block2.AfterCpu) * 0.5f,
						Scene = (before.Scene + block2.Scene) * 0.5f,
						Submit = (before.Submit + block2.Submit) * 0.5f,
						Split = block2.Split
					};
				}
				string text = $"{spot.Name} {name}: frame p50 {block2.P50:F1} -> {block.P50:F1} ms ({Pct(block.P50 - block2.P50, block2.P50)}); updates cpu {block2.UpdatesCpu:F1} -> {block.UpdatesCpu:F1}, mod late cpu {block2.LateCpu:F1} -> {block.LateCpu:F1}, after cpu {block2.AfterCpu:F1} -> {block.AfterCpu:F1}; " + $"scene {block2.Scene:F1} -> {block.Scene:F1}, submit {block2.Submit:F1} -> {block.Submit:F1} ms; people {block2.Split.People:F0} -> {block.Split.People:F0}, tracked {block2.Split[SceneClock.N.Tracked]:F0} -> {block.Split[SceneClock.N.Tracked]:F0}";
				Log.Info("STRESS " + text);
				_summary.Add(text);
			}
		}

		private IEnumerator Measure(string label, List<Block> into, Spot? spot = null)
		{
			for (int i = 0; i < 10; i++)
			{
				Keep(spot);
				yield return null;
			}
			Hitch.ResetSums();
			Hitch.LapSumsOn = true;
			SceneClock.ResetWindow();
			_mod.RestartPerfWindowsForTest();
			long glided0 = People.Glided;
			long kept0 = SceneCollector.TreesKept;
			long offscreen0 = SceneCollector.OffscreenSkipped;
			long cells0 = SceneCollector.CellsSkipped;
			long chunks0 = SceneCollector.ChunksSkipped;
			SimLod.ResetCounts();
			GpuRenderer gpu = _mod.GpuRendererForTest;
			gpu?.TakeTimes();
			_mod.GpuSumsForTest(out var lastScene, out var lastSubmit, out var lastFrames);
			List<float> frames = new List<float>(1024);
			List<float> sceneMs = new List<float>(1024);
			List<float> submitMs = new List<float>(1024);
			int gc0 = GC.CollectionCount(0);
			for (float t = 0f; t < 4f; t += Time.unscaledDeltaTime)
			{
				Keep(spot);
				yield return null;
				frames.Add(Time.unscaledDeltaTime * 1000f);
				_mod.GpuSumsForTest(out var scene, out var submit, out var frames2);
				if (frames2 == lastFrames + 1)
				{
					sceneMs.Add((float)(scene - lastScene));
					submitMs.Add((float)(submit - lastSubmit));
				}
				lastScene = scene;
				lastSubmit = submit;
				lastFrames = frames2;
			}
			int num = Mathf.Max(1, Hitch.SumFrames);
			Block block = new Block
			{
				Label = label,
				Frames = frames.Count,
				P50 = Rank(frames, 0.5f),
				P95 = Rank(frames, 0.95f),
				Updates = (float)(Hitch.SumUpdates / (double)num),
				UpdatesCpu = (float)(Hitch.SumUpdatesCpu / (double)num),
				ModUpd = (float)(Hitch.SumModUpdate / (double)num),
				Late = (float)(Hitch.SumLate / (double)num),
				LateCpu = (float)(Hitch.SumLateCpu / (double)num),
				After = (float)(Hitch.SumAfter / (double)num),
				AfterCpu = (float)(Hitch.SumAfterCpu / (double)num),
				Scene = Rank(sceneMs, 0.5f),
				Submit = Rank(submitMs, 0.5f),
				Split = SceneClock.Snapshot(),
				HeapMb = (float)GC.GetTotalMemory(forceFullCollection: false) / 1048576f,
				Gc = GC.CollectionCount(0) - gc0
			};
			string text = ((gpu != null) ? gpu.TakeTimes() : "-");
			string text2 = ((gpu != null) ? gpu.Stats : "-");
			string text3 = ((_mod.Settings.GodView && !_mod.IsFirstPerson) ? ("; " + _mod.GodPerfReadForTest()) : "");
			_mod.GodPerfResetForTest();
			Log.Info($"STRESS block {label}: {block.Frames} frames, p50 {block.P50:F1}, p95 {block.P95:F1} ms; per frame (clock/cpu): updates {block.Updates:F1}/{block.UpdatesCpu:F1} of which the mod's Update {block.ModUpd:F1}, mod late {block.Late:F1}/{block.LateCpu:F1}, after {block.After:F1}/{block.AfterCpu:F1}; " + string.Format("scene p50 {0}, submit p50 {1} ms ({2} drawn on the card); heap {3:F0} MB, gc0 +{4}{5}", (sceneMs.Count > 0) ? block.Scene.ToString("F1") : "-", (submitMs.Count > 0) ? block.Submit.ToString("F1") : "-", sceneMs.Count, block.HeapMb, block.Gc, text3));
			Log.Info("STRESS card " + label + ": " + text + "; " + text2);
			Log.Info(string.Format("STRESS laps {0} (clock/cpu ms a frame): {1}; a frame: {2:F0} glided, {3:F0} trees kept, {4:F0} off-screen skipped, {5:F0} building cells skipped, {6:F0} chunks skipped; far creatures {7}: {8}; {9}", label, Hitch.TopLaps(14), (float)(People.Glided - glided0) / (float)num, (float)(SceneCollector.TreesKept - kept0) / (float)num, (float)(SceneCollector.OffscreenSkipped - offscreen0) / (float)num, (float)(SceneCollector.CellsSkipped - cells0) / (float)num, (float)(SceneCollector.ChunksSkipped - chunks0) / (float)num, SimLod.Active ? "budgeted" : "full", SimLod.Report(), SimLod.JobTimes()));
			Hitch.LapSumsOn = false;
			if (block.Split.Passes > 0)
			{
				Log.Info("STRESS split " + label + ": " + CityPerfTest.SplitText(block.Split));
			}
			into?.Add(block);
		}

		private IEnumerator Pictures(Spot spot)
		{
			Config.paused = true;
			PoseClock.Freeze(PoseClock.Now);
			_mod.HeldTime = Time.unscaledTime;
			_mod.NoFirefliesForTest = true;
			int chunkWas = _mod.Settings.ChunkWalk;
			Color32[][] pics = new Color32[3][];
			string[] drawn = new string[3];
			int w = 0;
			int h = 0;
			string[] names = new string[3] { "before", "new", "before2" };
			for (int i = 0; i < 3; i++)
			{
				bool flag = (SceneCollector.BuildingCellsOff = (SceneCollector.KeptTreesOff = (People.GlideOff = i != 1)));
				_mod.Settings.ChunkWalk = ((!flag) ? 1 : 0);
				for (int f = 0; f < 14; f++)
				{
					Keep(spot);
					yield return null;
				}
				pics[i] = Shown(out w, out h);
				if (pics[i] != null)
				{
					Save("st_" + spot.Name + "_" + names[i] + ".png", pics[i], w, h);
				}
				Match match = Regex.Match(_mod.GpuRendererForTest?.Stats ?? "", "models \\d+ \\(\\d+ copies, \\d+ draws\\)");
				drawn[i] = (match.Success ? match.Value : "?");
			}
			int num = Copies(drawn[0]);
			int num2 = Copies(drawn[1]);
			int num3 = Copies(drawn[2]);
			int num4 = Mathf.Abs(num - num3);
			Report(drawn[1] == drawn[0] || drawn[1] == drawn[2] || (num2 >= 0 && Mathf.Abs(num2 - num3) <= num4 + 8 && Mathf.Abs(num2 - num) <= num4 + 8), spot.Name + ": the card draws the same models, copies and draws with the savings on and off", "off " + drawn[0] + ", on " + drawn[1] + ", off " + drawn[2]);
			People.GlideOff = false;
			SceneCollector.KeptTreesOff = false;
			SceneCollector.BuildingCellsOff = false;
			_mod.Settings.ChunkWalk = chunkWas;
			_mod.HeldTime = -1f;
			_mod.NoFirefliesForTest = false;
			PoseClock.Thaw();
			Config.paused = false;
			if (pics[0] == null || pics[1] == null || pics[2] == null)
			{
				Report(ok: false, "pictures at " + spot.Name, "no picture read back from the card");
				yield break;
			}
			int num5 = Differ(pics[0], pics[1]);
			int num6 = Differ(pics[0], pics[2]);
			SaveDiff("st_" + spot.Name + "_diff.png", pics[0], pics[1], w, h);
			Report(num5 <= num6 * 2 + 400, spot.Name + ": the picture with people gliding and trees kept is the picture without", $"{num5} pixels differ on/off, {num6} off/off, of {w}x{h}");
		}

		private static int Copies(string drawn)
		{
			Match match = Regex.Match(drawn ?? "", "\\((\\d+) copies");
			if (!match.Success || !int.TryParse(match.Groups[1].Value, out var result))
			{
				return -1;
			}
			return result;
		}

		private Color32[] Shown(out int w, out int h)
		{
			w = (h = 0);
			Texture2D texture2D = _mod.GpuRendererForTest?.ReadOutput();
			if (texture2D == null)
			{
				return null;
			}
			Color32[] pixels = texture2D.GetPixels32();
			w = texture2D.width;
			h = texture2D.height;
			UnityEngine.Object.Destroy(texture2D);
			return pixels;
		}

		private static int Differ(Color32[] a, Color32[] b)
		{
			int num = 0;
			for (int i = 0; i < a.Length && i < b.Length; i++)
			{
				Color32 color = a[i];
				Color32 color2 = b[i];
				if (color.r != color2.r || color.g != color2.g || color.b != color2.b)
				{
					num++;
				}
			}
			return num;
		}

		private static void SaveDiff(string name, Color32[] a, Color32[] b, int w, int h)
		{
			Color32[] array = new Color32[a.Length];
			for (int i = 0; i < a.Length && i < b.Length; i++)
			{
				Color32 color = a[i];
				Color32 color2 = b[i];
				bool flag = color.r != color2.r || color.g != color2.g || color.b != color2.b;
				array[i] = (flag ? new Color32(byte.MaxValue, 0, 0, byte.MaxValue) : new Color32((byte)(color.r / 3), (byte)(color.g / 3), (byte)(color.b / 3), byte.MaxValue));
			}
			Save(name, array, w, h);
		}

		private static void Save(string name, Color32[] px, int w, int h)
		{
			try
			{
				Texture2D texture2D = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
				texture2D.SetPixels32(px);
				texture2D.Apply(updateMipmaps: false);
				File.WriteAllBytes(Path.Combine(Log.Folder, name), texture2D.EncodeToPNG());
				UnityEngine.Object.Destroy(texture2D);
			}
			catch (Exception ex)
			{
				Log.Error("STRESS picture " + name + ": " + ex.Message);
			}
		}

		private void Keep(Spot? spot)
		{
			if (!_mod.IsFirstPerson)
			{
				try
				{
					Camera camera = World.world.camera;
					camera.transform.position = new Vector3(_at.x, _at.y, camera.transform.position.z);
					camera.orthographicSize = _zoom;
					if (World.world.move_camera != null)
					{
						World.world.move_camera.setTargetZoom(_zoom);
					}
					World.world.setZoomOrthographic(_zoom);
					return;
				}
				catch (Exception ex)
				{
					if (!_cameraFailed)
					{
						_cameraFailed = true;
						Log.Error("STRESS camera: " + ex.Message);
					}
					return;
				}
			}
			if (spot.HasValue)
			{
				Place(spot.Value);
			}
			Actor host = _mod.Host;
			if (host == null || Time.frameCount % 60 != 0)
			{
				return;
			}
			try
			{
				if (host.isAlive())
				{
					host.setNutrition(host.getMaxNutrition());
					if (host.getHealth() < host.getMaxHealth())
					{
						host.restoreHealth(host.getMaxHealth());
					}
					if (host.is_inside_building)
					{
						host.exitBuilding();
					}
					host.makeWait(30f);
				}
			}
			catch
			{
			}
		}

		private IEnumerator Settle(float seconds, Spot? spot = null)
		{
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				Keep(spot);
				yield return null;
			}
		}

		private void Place(Spot spot)
		{
			_mod.DebugEye = spot.Eye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = spot.Height;
			_mod.SetYaw(spot.Yaw);
			_mod.SetPitchDegrees(spot.Pitch);
		}

		private void PlanSpots(Vector2 town)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			Vector2 vector = town + new Vector2(-6f, -5f);
			if (!Open(vector))
			{
				bool flag = false;
				for (float num = 5f; num <= 10f; num += 1f)
				{
					if (flag)
					{
						break;
					}
					for (int i = 0; i < 32; i++)
					{
						if (flag)
						{
							break;
						}
						float f = (float)i * MathF.PI / 16f;
						Vector2 vector2 = town + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num;
						if (Open(vector2))
						{
							vector = vector2;
							flag = true;
						}
					}
				}
			}
			Vector2 vector3 = town - vector;
			_spots.Add(new Spot
			{
				Name = "street",
				Eye = vector,
				Height = 0f,
				Yaw = Mathf.Atan2(vector3.y, vector3.x),
				Pitch = 1.2f
			});
			Vector2 vector4 = town + new Vector2(28f, 0f);
			float num2 = -99f;
			for (int j = 0; j < 16; j++)
			{
				float f2 = (float)j * MathF.PI / 8f;
				Vector2 vector5 = town + new Vector2(Mathf.Cos(f2), Mathf.Sin(f2)) * 28f;
				if (OnLand(vector5))
				{
					float num3 = terrainForTest.SampleHeight(vector5.x, vector5.y);
					if (num3 > num2)
					{
						num2 = num3;
						vector4 = vector5;
					}
				}
			}
			Vector2 vector6 = town - vector4;
			_spots.Add(new Spot
			{
				Name = "rise",
				Eye = vector4,
				Height = 7f,
				Yaw = Mathf.Atan2(vector6.y, vector6.x),
				Pitch = -9f
			});
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

		private static bool Open(Vector2 p)
		{
			if (!OnLand(p))
			{
				return false;
			}
			WorldTile tile = World.world.GetTile((int)p.x, (int)p.y);
			if (tile.building == null && tile.Type != null)
			{
				return !tile.Type.block;
			}
			return false;
		}

		private bool StillFirstPerson(Actor host)
		{
			if (_mod.IsFirstPerson)
			{
				try
				{
					if (host != null && host.is_inside_building)
					{
						Log.Info("STRESS note: the host is inside a building");
					}
				}
				catch
				{
				}
				return true;
			}
			Report(ok: false, "first person lasts the test", (host != null && host.isAlive()) ? "let go" : "the body died");
			return false;
		}

		private void Probe(string when)
		{
			StringBuilder stringBuilder = new StringBuilder(512);
			stringBuilder.Append("STRESS memory ").Append(when).Append(": heap ")
				.Append(((float)GC.GetTotalMemory(forceFullCollection: false) / 1048576f).ToString("F0"))
				.Append(" MB");
			try
			{
				stringBuilder.Append(", mono used ").Append(((float)Profiler.GetMonoUsedSizeLong() / 1048576f).ToString("F0")).Append(" of ")
					.Append(((float)Profiler.GetMonoHeapSizeLong() / 1048576f).ToString("F0"))
					.Append(" MB, unity ")
					.Append(((float)Profiler.GetTotalAllocatedMemoryLong() / 1048576f).ToString("F0"))
					.Append(" MB, graphics driver ")
					.Append(((float)Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576f).ToString("F0"))
					.Append(" MB");
			}
			catch
			{
			}
			try
			{
				Texture2D[] array = Resources.FindObjectsOfTypeAll<Texture2D>();
				long num = 0L;
				Texture2D[] array2 = array;
				foreach (Texture2D o in array2)
				{
					try
					{
						num += Profiler.GetRuntimeMemorySizeLong(o);
					}
					catch
					{
					}
				}
				stringBuilder.Append("; textures ").Append(array.Length).Append(" (")
					.Append(((float)num / 1048576f).ToString("F0"))
					.Append(" MB)");
				Mesh[] array3 = Resources.FindObjectsOfTypeAll<Mesh>();
				num = 0L;
				Mesh[] array4 = array3;
				foreach (Mesh o2 in array4)
				{
					try
					{
						num += Profiler.GetRuntimeMemorySizeLong(o2);
					}
					catch
					{
					}
				}
				stringBuilder.Append(", meshes ").Append(array3.Length).Append(" (")
					.Append(((float)num / 1048576f).ToString("F0"))
					.Append(" MB)");
				stringBuilder.Append(", render textures ").Append(Resources.FindObjectsOfTypeAll<RenderTexture>().Length);
			}
			catch (Exception ex)
			{
				stringBuilder.Append("; unity objects unreadable: ").Append(ex.Message);
			}
			stringBuilder.Append("; caches: ");
			SceneCollector sceneForTest = _mod.SceneForTest;
			GpuRenderer gpuRendererForTest = _mod.GpuRendererForTest;
			object item = ((gpuRendererForTest != null) ? Field(gpuRendererForTest, "_models") : null);
			(string, object, Type, string)[] array5 = new(string, object, Type, string)[14]
			{
				("figures", null, typeof(People), "Figures"),
				("headings", sceneForTest, null, "_headings"),
				("hitboxes", null, typeof(Hitboxes), "Boxes"),
				("steering", null, typeof(Steering), "States"),
				("stature", null, typeof(Stature), "Tracks"),
				("creatures", null, typeof(Creatures), "States"),
				("gazes", null, typeof(Attention), "Gazes"),
				("loot", null, typeof(Loot), "Snaps"),
				("loungers", null, typeof(Lounging), "All"),
				("buildings near", sceneForTest, null, "_buildings"),
				("far models", sceneForTest, null, "_farModels"),
				("card images", sceneForTest, null, "_cardImages"),
				("gpu lit", item, null, "_lit"),
				("gpu built", item, null, "_built")
			};
			for (int i = 0; i < array5.Length; i++)
			{
				(string, object, Type, string) tuple = array5[i];
				int num2 = Count(tuple.Item2, tuple.Item3, tuple.Item4);
				if (num2 >= 0)
				{
					stringBuilder.Append(tuple.Item1).Append(' ').Append(num2)
						.Append(", ");
				}
			}
			Log.Info(stringBuilder.ToString());
		}

		private static object Field(object owner, string name)
		{
			try
			{
				return owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(owner);
			}
			catch
			{
				return null;
			}
		}

		private static int Count(object owner, Type type, string field)
		{
			try
			{
				object obj = ((owner != null) ? Field(owner, field) : type.GetField(field, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null));
				if (obj is ICollection collection)
				{
					return collection.Count;
				}
				if (obj == null)
				{
					return -1;
				}
				PropertyInfo property = obj.GetType().GetProperty("Count");
				return (property != null) ? ((int)property.GetValue(obj)) : (-1);
			}
			catch
			{
				return -1;
			}
		}

		private static float Rank(List<float> values, float q)
		{
			if (values.Count == 0)
			{
				return 0f;
			}
			List<float> list = new List<float>(values);
			list.Sort();
			return list[Mathf.Clamp(Mathf.CeilToInt(q * (float)list.Count) - 1, 0, list.Count - 1)];
		}

		private static string Pct(float change, float of)
		{
			if (!(of > 0f))
			{
				return "-";
			}
			return (100f * change / of).ToString("+0;-0;0") + "%";
		}

		private bool Report(bool ok, string what, string detail = null)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info("STRESS " + (ok ? "PASS " : "FAIL ") + what + ((detail != null) ? (" (" + detail + ")") : ""));
			return ok;
		}

		private void Finish()
		{
			_timing = false;
			try
			{
				SceneClock.SampleEveryFrame = false;
				SceneCollector.SkipUnitsForTest = false;
				SceneCollector.SkipBuildingsForTest = false;
				SceneCollector.KeptTreesOff = false;
				SceneCollector.BuildingCellsOff = false;
				WorldBoxMod.MapHoldFirstPersonOff = false;
				WorldBoxMod.LandBesideOff = false;
				WorldBoxMod.LandBesideForTest = false;
				People.GlideOff = false;
				Hitch.LapSumsOn = false;
				Config.paused = false;
				if (_mod != null)
				{
					_mod.DebugEyeSet = false;
					_mod.HeldTime = -1f;
					_mod.NoFirefliesForTest = false;
				}
				try
				{
					ControllableUnit.clear(pCallKill: false);
				}
				catch
				{
				}
			}
			catch (Exception ex)
			{
				Log.Error("STRESS putting things back: " + ex.Message);
			}
			foreach (string item in _summary)
			{
				Log.Info("STRESS summary " + item);
			}
			Log.Info($"STRESS {_pass} passed, {_fail} failed");
			Log.Info("STRESS DONE");
			if (_quit)
			{
				Application.Quit();
			}
		}
	}
}
