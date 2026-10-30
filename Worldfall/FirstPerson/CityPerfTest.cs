using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	[DefaultExecutionOrder(-30000)]
	internal sealed class CityPerfTest : MonoBehaviour
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
			public string Spot;

			public string Label;

			public int Frames;

			public float FrameP50;

			public float FrameP95;

			public float SceneP50;

			public float SceneP95;

			public float SubmitP50;

			public float UpdatesP50;

			public float ModP50;

			public float RestP50;

			public SceneClock.Split Split;

			public float Scene
			{
				get
				{
					if (!(SceneP50 > 0f))
					{
						return Split.Total;
					}
					return SceneP50;
				}
			}
		}

		private enum Section
		{
			Mesh,
			Voxel,
			Light,
			Tagged,
			Box,
			Far,
			Counts
		}

		private sealed class Rec
		{
			public string Label;

			public MeshModel Model;

			public int Rot = -1;

			public int Off = -1;

			public int Scl = -1;

			public int Pal = -1;

			public uint[] W;
		}

		private sealed class Snap
		{
			public readonly List<Rec>[] Parts = new List<Rec>[7]
			{
				new List<Rec>(),
				new List<Rec>(),
				new List<Rec>(),
				new List<Rec>(),
				new List<Rec>(),
				new List<Rec>(),
				new List<Rec>()
			};

			public int Meshes;

			public int UnitMeshes;

			public int Voxels;

			public int Billboards;

			public int Lights;

			public int Tagged;

			public int UnitTags;

			public int Heads;

			public int Words;
		}

		private const float BlockSeconds = 3f;

		private const int DropFrames = 10;

		private const int CheckInputs = 2000000;

		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private bool _pin = true;

		private bool _quick;

		private bool _timing;

		private bool _quit;

		private readonly List<string> _summary = new List<string>();

		private readonly List<Spot> _spots = new List<Spot>();

		private NpcTune.Switch _canary;

		private bool _canaryOn;

		private float _poseAt;

		private const float CanaryNudge = 0.25f;

		private long _updateStart;

		private bool _saved;

		private bool _savedGpu;

		private int _savedRes;

		private int _savedTime;

		private int _savedVsync;

		private int _savedFps;

		private float _savedDetail;

		private float _savedCreatures;

		private float _savedDraw;

		private bool[] _savedSwitches;

		private const float LiveSeconds = 6f;

		private const float FlipSeconds = 4f;

		private object _ships;

		private object _feet;

		private const int TimedRuns = 25;

		private static readonly string[] MeshFields = new string[25]
		{
			"Position.X", "Position.Y", "Position.Z", "Basis.C0.X", "Basis.C0.Y", "Basis.C0.Z", "Basis.C1.X", "Basis.C1.Y", "Basis.C1.Z", "Basis.C2.X",
			"Basis.C2.Y", "Basis.C2.Z", "Hidden (parts 0-31)", "Hidden (parts 32-63)", "Flash", "Multiply", "Tag", "flags (emissive, no shadow, static, shadow only, keep small, fade keeps shadow, cloud)", "Glow", "Snow",
			"Sway", "SwayInvTop", "Fade", "DissolveScale", "SortDepth"
		};

		private static readonly string[] VoxelFields = new string[21]
		{
			"Tag", "Origin.X", "Origin.Y", "Origin.Z", "U.X", "U.Y", "U.Z", "V.X", "V.Y", "V.Z",
			"N.X", "N.Y", "N.Z", "SizeU", "SizeV", "SizeN", "Flash", "Multiply", "flags (overlay, no shadow, emissive)", "Glow",
			"SortDepth"
		};

		private static readonly string[] LightFields = new string[8] { "X", "Y", "Z", "Radius", "R", "G", "B", "Glow" };

		private static readonly string[] BoxFields = new string[21]
		{
			"body Centre.X", "body Centre.Y", "body Centre.Z", "body AX", "body AY", "body HalfA", "body HalfB", "body HalfUp", "has a head", "head Centre.X",
			"head Centre.Y", "head Centre.Z", "head X.X", "head X.Y", "head X.Z", "head Y.X", "head Y.Y", "head Y.Z", "head Z.X", "head Z.Y",
			"head Z.Z"
		};

		private static readonly string[] FarFields = new string[6] { "X", "Y", "Z", "Width", "Height", "Color" };

		private static readonly string[] CountFields = new string[7] { "meshes", "voxels", "billboards", "lights", "tagged", "creature tags (UnitTagEnd)", "far creatures" };

		private const int Whole = 16777215;

		private void Update()
		{
			_updateStart = Stopwatch.GetTimestamp();
		}

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
					Log.Error("CITYPERF stopped by an error: " + ex);
					_fail++;
					foreach (IEnumerator item2 in stack)
					{
						(item2 as IDisposable)?.Dispose();
					}
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

		private static int SoakSeconds(string options)
		{
			int num = options.IndexOf("soak=", StringComparison.Ordinal);
			if (num < 0)
			{
				return 0;
			}
			int i;
			for (i = num + 5; i < options.Length && char.IsDigit(options[i]); i++)
			{
			}
			if (!int.TryParse(options.Substring(num + 5, i - num - 5), out var result))
			{
				return 0;
			}
			return result;
		}

		private IEnumerator Run()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(path);
			_quit = options.Contains("quit");
			_pin = !options.Contains("adaptive");
			_quick = options.Contains("quick");
			if (options.Contains("canary"))
			{
				_canary = new NpcTune.Switch("canary", () => _canaryOn, delegate(bool v)
				{
					_canaryOn = v;
					if (PoseClock.Frozen)
					{
						PoseClock.Freeze(_poseAt + (v ? 0.25f : 0f));
					}
				});
				NpcTune.Switches.Add(_canary);
			}
			int slot = SaveSlot(options);
			if (slot <= 0)
			{
				slot = 32;
			}
			Log.Info(string.Format("CITYPERF start: options \"{0}\", save slot {1}, detail {2} while timing; switches: {3}; checks: {4}", options.Trim(), slot, _pin ? "held at full" : "left to AdaptDetail", NpcTune.Names, NpcTune.Checks.Count));
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
					Log.Error("CITYPERF could not load " + text + ": " + ex.Message);
				}
			}
			else
			{
				Log.Info("CITYPERF no save in slot " + slot + ": a village is founded instead");
			}
			if (loaded)
			{
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
			Config.paused = false;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			int num = 0;
			try
			{
				foreach (City item in World.world.cities.list)
				{
					if (item != null && item.isAlive())
					{
						num++;
					}
				}
			}
			catch
			{
			}
			Log.Info(string.Format("CITYPERF world: {0}, {1}x{2} tiles, {3} creatures, {4} towns; settling 10 s", loaded ? ("save " + slot) : "the starting map", MapBox.width, MapBox.height, World.world.units.getSimpleList().Count, num));
			yield return new WaitForSecondsRealtime(10f);
			WorldTile centre;
			int crowd;
			City town;
			Actor host = BusiestTown(out centre, out crowd, out town);
			if (host == null)
			{
				foreach (WorldTile item2 in KingTest.GoodSites())
				{
					host = KingTest.FoundKingdom(item2, "human", 14, 4);
					if (host != null)
					{
						centre = item2;
						crowd = 18;
						break;
					}
				}
				if (host != null)
				{
					yield return new WaitForSecondsRealtime(5f);
				}
			}
			if (host == null)
			{
				Report(ok: false, "a town to stand in");
				yield break;
			}
			Log.Info(string.Format("CITYPERF town {0} at {1},{2}: {3} creatures within 15 tiles of its middle; possessing {4}", (town != null) ? town.name : "(founded)", centre.x, centre.y, crowd, WorldBoxMod.Describe(host)));
			Settings s = _mod.Settings;
			_savedGpu = s.GpuRender;
			_savedRes = s.Resolution;
			_savedTime = s.TimeMode;
			_savedDetail = s.DetailDistance;
			_savedCreatures = s.CreatureDistance;
			_savedDraw = s.DrawDistance;
			_savedVsync = QualitySettings.vSyncCount;
			_savedFps = Application.targetFrameRate;
			_savedSwitches = NpcTune.Save();
			_saved = true;
			if (!options.Contains("keep"))
			{
				s.GpuRender = true;
				s.Resolution = Mathf.Min(3, WorldBoxMod.ResolutionWidths.Length - 1);
				s.DetailDistance = 240f;
				s.CreatureDistance = 1200f;
				s.DrawDistance = 1200f;
				s.TimeMode = 1;
			}
			_mod.ForceRelight();
			ControllableUnit.setControllableCreature(host);
			yield return new WaitForSecondsRealtime(4f);
			if (!_mod.IsFirstPerson)
			{
				Report(ok: false, "first person starts");
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
			Vector2 town2 = new Vector2((float)centre.x + 0.5f, (float)centre.y + 0.5f);
			PlanSpots(town2);
			for (int i = 0; i < 10; i++)
			{
				yield return null;
			}
			bool flag3 = _mod.GpuRendererForTest != null && _mod.ShownFrame == _mod.GpuRendererForTest.Shown;
			bool flag4 = false;
			try
			{
				flag4 = Config.fps_lock_30;
			}
			catch
			{
			}
			Log.Info(string.Format("CITYPERF settings: {0}, {1} wide (index {2}), ", flag3 ? "GPU drawing" : ("the CPU drawing (" + (_mod.GpuRendererForTest?.Error ?? "GPU off") + ")"), WorldBoxMod.ResolutionWidths[s.Resolution], s.Resolution) + string.Format("detail {0:F0}, creatures {1:F0}, draw {2}, always day, finish {3}, ", s.DetailDistance, s.CreatureDistance, (s.DrawDistance >= 1200f) ? "whole" : s.DrawDistance.ToString("F0"), (s.GpuLook == 0) ? "classic" : "painterly") + $"pixel distance {s.PixelDistance}, shadows {s.Shadows}, biome fx {s.BiomeFx}, folk pace {s.FolkPace:F2}; game fps lock 30 {flag4}; screen {Screen.width}x{Screen.height}; " + $"{SystemInfo.processorType} ({SystemInfo.processorCount} threads), {SystemInfo.graphicsDeviceName}");
			int soak = SoakSeconds(options);
			if (soak > 0 && _spots.Count > 0)
			{
				Place(_spots[0]);
				int i = Hitch.Logged;
				if (options.Contains("bench"))
				{
					Hitch.GameBench = true;
					SceneClock.SampleEveryFrame = true;
				}
				float t = _spots[0].Yaw;
				for (float t2 = 0f; t2 < (float)soak; t2 += Time.unscaledDeltaTime)
				{
					if (!StillFirstPerson(host))
					{
						break;
					}
					_mod.SetYaw(t + t2 * 6f);
					if (Time.frameCount % 60 == 0)
					{
						try
						{
							if (host.isAlive())
							{
								host.setNutrition(host.getMaxNutrition());
							}
							if (host.is_inside_building)
							{
								host.exitBuilding();
							}
							host.makeWait(30f);
							if (_mod.Interior.Inside)
							{
								_mod.LeaveRoomForTest();
							}
						}
						catch
						{
						}
					}
					yield return null;
				}
				Log.Info($"CITYPERF soak: {soak} s in the street, {Hitch.Logged - i} frames over 120 ms");
				Hitch.GameBench = false;
				SceneClock.SampleEveryFrame = false;
				yield break;
			}
			SceneClock.SampleEveryFrame = true;
			_timing = true;
			foreach (Spot spot in _spots)
			{
				if (_quick)
				{
					break;
				}
				if (StillFirstPerson(host))
				{
					yield return MeasureSpot(spot);
					continue;
				}
				yield break;
			}
			if (!_quick && _spots.Count > 0 && StillFirstPerson(host))
			{
				yield return Variants(_spots[0]);
			}
			_timing = false;
			SceneClock.SampleEveryFrame = false;
			QualitySettings.vSyncCount = _savedVsync;
			Application.targetFrameRate = _savedFps;
			if (StillFirstPerson(host))
			{
				Config.paused = true;
				foreach (Spot spot2 in _spots)
				{
					yield return Frozen(spot2);
				}
				Config.paused = false;
			}
			if (NpcTune.LiveChecks.Count > 0)
			{
				foreach (Spot spot3 in _spots)
				{
					if (!StillFirstPerson(host))
					{
						break;
					}
					yield return Live(spot3);
				}
			}
			int checksFailed = _fail;
			foreach (NpcTune.Check check in NpcTune.Checks)
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				string text2;
				try
				{
					text2 = check.Run(2000000);
				}
				catch (Exception ex2)
				{
					text2 = "threw " + ex2.GetType().Name + ": " + ex2.Message;
				}
				Report(text2 == null, $"check {check.Name}: {2000000} inputs the same to the bit ({stopwatch.Elapsed.TotalMilliseconds:F0} ms)", text2);
				yield return null;
			}
			_summary.Add($"checks: {NpcTune.Checks.Count - (_fail - checksFailed)} of {NpcTune.Checks.Count} the same to the bit on {2000000} inputs each");
		}

		private IEnumerator Live(Spot spot)
		{
			Place(spot);
			Config.paused = false;
			yield return Settle(1.5f);
			foreach (NpcTune.LiveCheck liveCheck in NpcTune.LiveChecks)
			{
				try
				{
					liveCheck.Enable(obj: true);
				}
				catch (Exception ex)
				{
					Report(ok: false, "live " + liveCheck.Name + " at " + spot.Name + ": turned on", ex.GetType().Name + ": " + ex.Message);
				}
			}
			try
			{
				for (float t = 0f; t < 6f; t += Time.unscaledDeltaTime)
				{
					yield return null;
				}
			}
			finally
			{
				foreach (NpcTune.LiveCheck liveCheck2 in NpcTune.LiveChecks)
				{
					try
					{
						liveCheck2.Enable(obj: false);
					}
					catch
					{
					}
				}
			}
			foreach (NpcTune.LiveCheck liveCheck3 in NpcTune.LiveChecks)
			{
				string text;
				try
				{
					text = liveCheck3.Result();
				}
				catch (Exception ex2)
				{
					text = "threw " + ex2.GetType().Name + ": " + ex2.Message;
				}
				Report(text == null, $"live {liveCheck3.Name} at {spot.Name}: old and new the same to the bit over {6f:F0} s of the world running", text);
			}
		}

		private bool StillFirstPerson(Actor host)
		{
			if (_mod.IsFirstPerson)
			{
				return true;
			}
			Report(ok: false, "first person lasts the test", (host != null && host.isAlive()) ? "let go" : "the body died");
			return false;
		}

		private void Hold()
		{
			if (!_timing)
			{
				return;
			}
			if (_pin)
			{
				_mod.PinDetailForTest(1f, 1f);
			}
			Actor host = _mod.Host;
			if (host != null && Time.frameCount % 120 == 0)
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
			if (QualitySettings.vSyncCount != 0)
			{
				QualitySettings.vSyncCount = 0;
			}
			if (Application.targetFrameRate != -1)
			{
				Application.targetFrameRate = -1;
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

		private IEnumerator Settle(float seconds, int frames = 0)
		{
			int n = 0;
			for (float t = 0f; t < seconds || n < frames; n++)
			{
				Hold();
				yield return null;
				t += Time.unscaledDeltaTime;
			}
		}

		private IEnumerator MeasureSpot(Spot spot)
		{
			Place(spot);
			Log.Info($"CITYPERF spot {spot.Name}: eye {spot.Eye.x:F1},{spot.Eye.y:F1} +{spot.Height:F1}, yaw {spot.Yaw * 57.29578f:F0}, pitch {spot.Pitch:F1}");
			yield return Settle(2f);
			List<Block> a = new List<Block>();
			yield return Measure(spot.Name, "A1", a);
			yield return Measure(spot.Name, "A2", a);
			if (a.Count < 2)
			{
				yield break;
			}
			float noiseFrame = Mathf.Abs(a[0].FrameP50 - a[1].FrameP50);
			float noiseScene = Mathf.Abs(a[0].Scene - a[1].Scene);
			Log.Info($"CITYPERF noise {spot.Name}: frame p50 {a[0].FrameP50:F2} vs {a[1].FrameP50:F2} ms ({Pct(noiseFrame, a[0].FrameP50)}), scene p50 {a[0].Scene:F2} vs {a[1].Scene:F2} ms ({Pct(noiseScene, a[0].Scene)}), " + $"collect {a[0].Split.Total:F2} vs {a[1].Split.Total:F2} ms");
			_summary.Add($"{spot.Name}: frame p50 {(a[0].FrameP50 + a[1].FrameP50) * 0.5f:F2} ms, scene p50 {(a[0].Scene + a[1].Scene) * 0.5f:F2} ms (noise {noiseFrame:F2} / {noiseScene:F2}), people {a[0].Split.People:F0}, units visited {a[0].Split[SceneClock.N.Visited]:F0}; " + $"parts p50 updates {(a[0].UpdatesP50 + a[1].UpdatesP50) * 0.5f:F2}, first-person drawing {(a[0].ModP50 + a[1].ModP50) * 0.5f:F2}, rest {(a[0].RestP50 + a[1].RestP50) * 0.5f:F2} ms");
			yield return Interleave(spot.Name, null);
			foreach (NpcTune.Switch sw in NpcTune.Switches)
			{
				bool[] saved = NpcTune.Save();
				List<Block> b = new List<Block>();
				for (int k = 0; k < 4; k++)
				{
					bool flag = k == 1 || k == 2;
					sw.Set(flag);
					yield return Measure(spot.Name, sw.Name + (flag ? " on" : " off"), b);
				}
				NpcTune.Restore(saved);
				if (b.Count >= 4)
				{
					float num = (b[0].FrameP50 + b[3].FrameP50) * 0.5f;
					float num2 = (b[1].FrameP50 + b[2].FrameP50) * 0.5f;
					float num3 = (b[0].Scene + b[3].Scene) * 0.5f;
					float num4 = (b[1].Scene + b[2].Scene) * 0.5f;
					float num5 = (b[0].Split.Total + b[3].Split.Total) * 0.5f;
					float num6 = (b[1].Split.Total + b[2].Split.Total) * 0.5f;
					string text = $"switch {sw.Name} at {spot.Name}: frame p50 {num:F2} -> {num2:F2} ms ({num2 - num:+0.00;-0.00} ms, {Pct(num2 - num, num)}), " + $"scene p50 {num3:F2} -> {num4:F2} ms ({num4 - num3:+0.00;-0.00} ms, {Pct(num4 - num3, num3)}), collect {num5:F2} -> {num6:F2} ms; " + string.Format("noise {0:F2} / {1:F2} ms: {2}", noiseFrame, noiseScene, (num3 - num4 > 2f * noiseScene) ? "faster" : ((num4 - num3 > 2f * noiseScene) ? "SLOWER" : "within the noise"));
					Log.Info("CITYPERF " + text);
					_summary.Add(text);
					yield return Interleave(spot.Name, sw);
					NpcTune.Restore(saved);
				}
			}
		}

		private IEnumerator Interleave(string spot, NpcTune.Switch sw)
		{
			string name = ((sw != null) ? sw.Name : "nothing");
			bool on = false;
			sw?.Set(obj: false);
			_mod.RestartPerfWindowsForTest();
			for (int i = 0; i < 10; i++)
			{
				Hold();
				yield return null;
			}
			List<float> offMs = new List<float>();
			List<float> onMs = new List<float>();
			List<float> diffs = new List<float>();
			float[] offSplit = new float[26];
			float[] onSplit = new float[26];
			int offN = 0;
			int onN = 0;
			float pending = -1f;
			bool pendingOn = false;
			_mod.GpuSumsForTest(out var lastScene, out var submit, out var lastFrames);
			SceneClock.ResetWindow();
			float start = Time.unscaledTime;
			while (Time.unscaledTime - start < 4f)
			{
				bool ran = on;
				Hold();
				yield return null;
				if (!_mod.IsFirstPerson)
				{
					yield break;
				}
				_mod.GpuSumsForTest(out var scene, out submit, out var frames);
				SceneClock.Split split = SceneClock.Snapshot();
				SceneClock.ResetWindow();
				bool num = frames == lastFrames + 1 && split.Passes == 1;
				float num2 = (float)(scene - lastScene);
				lastScene = scene;
				lastFrames = frames;
				on = !on;
				sw?.Set(on);
				if (!num)
				{
					pending = -1f;
					continue;
				}
				(ran ? onMs : offMs).Add(num2);
				float[] array = (ran ? onSplit : offSplit);
				for (int j = 0; j < array.Length; j++)
				{
					array[j] += split.Ms[j];
				}
				if (ran)
				{
					onN++;
				}
				else
				{
					offN++;
				}
				if (pending < 0f || pendingOn == ran)
				{
					pending = num2;
					pendingOn = ran;
				}
				else
				{
					diffs.Add(ran ? (num2 - pending) : (pending - num2));
					pending = -1f;
				}
			}
			sw?.Set(obj: false);
			if (diffs.Count < 8 || offN == 0 || onN == 0)
			{
				Log.Info($"CITYPERF flip {spot} {name}: too few frames ({diffs.Count} pairs)");
				yield break;
			}
			diffs.Sort();
			int count = diffs.Count;
			float num3 = Rank(diffs, 0.5f);
			int num4 = Mathf.FloorToInt(0.98f * Mathf.Sqrt(count));
			float num5 = diffs[Mathf.Clamp(count / 2 - num4 - 1, 0, count - 1)];
			float num6 = diffs[Mathf.Clamp((count + 1) / 2 + num4, 0, count - 1)];
			for (int k = 0; k < offSplit.Length; k++)
			{
				offSplit[k] /= offN;
				onSplit[k] /= onN;
			}
			float num7 = Rank(offMs, 0.5f);
			float num8 = Rank(onMs, 0.5f);
			string arg = ((num6 < 0f) ? "faster" : ((num5 > 0f) ? "SLOWER" : "within the noise"));
			if (sw == null)
			{
				arg = ((num5 <= 0f && num6 >= 0f) ? "holds 0, as it must" : "DOESN'T HOLD 0: the judge is off");
			}
			string text = $"flip {spot} {name}: scene p50 off {num7:F2} -> on {num8:F2} ms; neighbouring frames, on minus off: median {num3:+0.00;-0.00} ms ({Pct(num3, num7)}), 95% {num5:+0.00;-0.00} to {num6:+0.00;-0.00} ms, " + $"{count} pairs: {arg}; parts that moved: {Moved(offSplit, onSplit)}";
			Log.Info("CITYPERF " + text);
			_summary.Add(text);
		}

		private IEnumerator Variants(Spot spot)
		{
			Settings s = _mod.Settings;
			float detail = s.DetailDistance;
			float creatures = s.CreatureDistance;
			Place(spot);
			yield return Settle(1.5f);
			List<Block> all = new List<Block>();
			yield return Measure(spot.Name, $"detail {detail:F0} creatures {creatures:F0}", all);
			(float, float)[] array = new(float, float)[4]
			{
				(120f, creatures),
				(80f, creatures),
				(120f, 120f),
				(80f, 80f)
			};
			for (int i = 0; i < array.Length; i++)
			{
				(float, float) tuple = array[i];
				float d = tuple.Item1;
				float c = tuple.Item2;
				s.DetailDistance = d;
				s.CreatureDistance = c;
				yield return Settle(1.5f);
				yield return Measure(spot.Name, $"detail {d:F0} creatures {c:F0}", all);
			}
			s.DetailDistance = detail;
			s.CreatureDistance = creatures;
			if (all.Count == 0)
			{
				yield break;
			}
			List<string> list = new List<string>();
			foreach (Block item in all)
			{
				list.Add($"{item.Label}: frame {item.FrameP50:F2}, scene {item.Scene:F2} ms, people {item.Split.People:F0}");
			}
			string text = "variants at " + spot.Name + ": " + string.Join("; ", list);
			Log.Info("CITYPERF " + text);
			_summary.Add(text);
		}

		private IEnumerator Measure(string spot, string label, List<Block> into)
		{
			List<float> frameMs = new List<float>(1024);
			List<float> sceneMs = new List<float>(1024);
			List<float> submitMs = new List<float>(1024);
			List<float> updatesMs = new List<float>(1024);
			List<float> modMs = new List<float>(1024);
			List<float> restMs = new List<float>(1024);
			double lastScene = 0.0;
			double lastSubmit = 0.0;
			int lastFrames = 0;
			int n = 0;
			float lastUpdates = -1f;
			float start = Time.unscaledTime;
			_mod.RestartPerfWindowsForTest();
			while (true)
			{
				Hold();
				yield return null;
				if (!_mod.IsFirstPerson)
				{
					yield break;
				}
				n++;
				float num = lastUpdates;
				lastUpdates = ((_updateStart != 0L) ? ((float)((double)(Stopwatch.GetTimestamp() - _updateStart) * 1000.0 / (double)Stopwatch.Frequency)) : (-1f));
				if (n <= 10)
				{
					if (n == 10)
					{
						_mod.RestartPerfWindowsForTest();
						double num2;
						lastSubmit = (num2 = 0.0);
						lastScene = num2;
						lastFrames = 0;
						start = Time.unscaledTime;
					}
					continue;
				}
				frameMs.Add(Time.unscaledDeltaTime * 1000f);
				if (num >= 0f)
				{
					updatesMs.Add(num);
					modMs.Add(_mod.FrameMs);
					restMs.Add(Time.unscaledDeltaTime * 1000f - num - _mod.FrameMs);
				}
				_mod.GpuSumsForTest(out var scene, out var submit, out var frames);
				if (frames == lastFrames + 1)
				{
					sceneMs.Add((float)(scene - lastScene));
					submitMs.Add((float)(submit - lastSubmit));
				}
				lastScene = scene;
				lastSubmit = submit;
				lastFrames = frames;
				if (Time.unscaledTime - start >= 3f)
				{
					break;
				}
			}
			Block block = new Block
			{
				Spot = spot,
				Label = label,
				Frames = frameMs.Count,
				FrameP50 = Rank(frameMs, 0.5f),
				FrameP95 = Rank(frameMs, 0.95f),
				SceneP50 = Rank(sceneMs, 0.5f),
				SceneP95 = Rank(sceneMs, 0.95f),
				SubmitP50 = Rank(submitMs, 0.5f),
				UpdatesP50 = Rank(updatesMs, 0.5f),
				ModP50 = Rank(modMs, 0.5f),
				RestP50 = Rank(restMs, 0.5f),
				Split = SceneClock.Snapshot()
			};
			SceneClock.Split split = block.Split;
			Log.Info(string.Format("CITYPERF block {0} {1}: frame p50 {2:F2} p95 {3:F2} ms, scene p50 {4} p95 {5} ms, ", spot, label, block.FrameP50, block.FrameP95, (sceneMs.Count > 0) ? block.SceneP50.ToString("F2") : "-", (sceneMs.Count > 0) ? block.SceneP95.ToString("F2") : "-") + string.Format("submit p50 {0} ms, collect {1:F2} ms, {2} frames ({3} on the GPU); ", (submitMs.Count > 0) ? block.SubmitP50.ToString("F2") : "-", split.Total, block.Frames, sceneMs.Count) + $"detail {_mod.SceneDetailForTest:F2}, creatures {_mod.CreatureDetailForTest:F2} ({_mod.CreatureReachForTest:F0} tiles); vsync {QualitySettings.vSyncCount}, target fps {Application.targetFrameRate}");
			Log.Info($"CITYPERF parts {spot} {label}: p50 of {updatesMs.Count} frames: updates {block.UpdatesP50:F2} (the game's world, every script's Update), first-person drawing {block.ModP50:F2} (land, features, scene, submit), " + $"rest {block.RestP50:F2} ms (LateUpdates' other work, the HUD, Unity's drawing, waiting on the card)");
			Log.Info("CITYPERF split " + spot + " " + label + ": " + SplitText(split));
			Log.Info($"CITYPERF spread {spot} {label}: p50 {split.P50:F1}, p95 {split.P95:F1}, p99 {split.P99:F1}, worst {split.Worst:F1} ms, {split.Over16} of {split.Frames} over 16.7 ms, {split.Over33} over 33 ms; gc0 +{split.Gc0}, heap {split.HeapMb:F0} MB");
			into?.Add(block);
		}

		internal static string SplitText(SceneClock.Split s)
		{
			return $"crowd {s[SceneClock.B.Crowd]:F2}, ticks {s[SceneClock.B.Ticks]:F2}, begin {s[SceneClock.B.Begin]:F2}, units {s[SceneClock.B.Units]:F2} (checks {s[SceneClock.B.Checks]:F2}, steer {s[SceneClock.B.Steer]:F2}, track {s[SceneClock.B.Track]:F2} [route {s[SceneClock.B.Route]:F2}, step {s[SceneClock.B.Step]:F2}, face {s[SceneClock.B.Face]:F2}], sprites {s[SceneClock.B.Sprites]:F2}, ground {s[SceneClock.B.Ground]:F2}, " + $"people {s[SceneClock.B.People]:F2} [figure {s[SceneClock.B.Figure]:F2}, pose {s[SceneClock.B.Pose]:F2}, extras {s[SceneClock.B.Extras]:F2}, place {s[SceneClock.B.Place]:F2}, head {s[SceneClock.B.Head]:F2}, gear {s[SceneClock.B.Gear]:F2}], " + $"creatures {s[SceneClock.B.Creatures]:F2}, other {s[SceneClock.B.Other]:F2}, far {s[SceneClock.B.Far]:F2}), buildings {s[SceneClock.B.Buildings]:F2}, far props {s[SceneClock.B.FarProps]:F2}, fx {s[SceneClock.B.Fx]:F2}, rest {s[SceneClock.B.Rest]:F2} ms; " + $"units {s[SceneClock.N.Visited]:F0} visited, {s[SceneClock.N.Tracked]:F0} tracked, {s[SceneClock.N.Steered]:F0} steered, {s[SceneClock.N.Behind]:F0} behind, people {s.People:F0} (<30 {s[SceneClock.N.Near]:F0}, 30-60 {s[SceneClock.N.Mid]:F0}, >60 {s[SceneClock.N.Distant]:F0}), " + $"creatures {s[SceneClock.N.Creatures]:F0}, far {s[SceneClock.N.Far]:F0}; steering {s[SceneClock.N.Walkers]:F0} walkers {s.SteerMs:F2} ms ({s.Passes} passes, {s.Sampled} timed)";
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
			return (100f * change / of).ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + "%";
		}

		private IEnumerator Frozen(Spot spot)
		{
			Place(spot);
			yield return Settle(3f, 200);
			if (!_mod.IsFirstPerson)
			{
				Report(ok: false, "frozen " + spot.Name + ": first person");
				yield break;
			}
			SceneCollector sceneForTest = _mod.SceneForTest;
			UnityEngine.Random.State state = UnityEngine.Random.state;
			bool[] array = NpcTune.Save();
			PoseClock.Freeze(PoseClock.Now);
			_poseAt = PoseClock.Now;
			try
			{
				for (int i = 0; i < 4; i++)
				{
					if (!Gather(state, -1))
					{
						Report(ok: false, "frozen " + spot.Name + ": the outdoor scene gathered by the test", "not a plain outdoor frame");
						yield break;
					}
				}
				_ships = Creatures.HoldShipsForTest();
				_feet = People.HoldFeetForTest();
				Stopwatch stopwatch = Stopwatch.StartNew();
				Gather(state, -1);
				double totalMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
				Snap snap = Take(sceneForTest);
				Gather(state, -1);
				Snap snap2 = Take(sceneForTest);
				Gather(state, -1);
				Snap snap3 = Take(sceneForTest);
				Log.Info($"CITYPERF frozen {spot.Name}: {snap.Meshes} meshes ({snap.UnitMeshes} of creatures), {snap.Voxels} voxels, {snap.Billboards} billboards, {snap.Lights} lights, {snap.Tagged} tagged ({snap.UnitTags} creatures), {snap.Parts[5].Count} far specks, " + $"{snap.Heads} head boxes; {snap.Words} numbers compared; a run {totalMilliseconds:F1} ms; switches {NpcTune.Names}");
				HashSet<long> hashSet = new HashSet<long>();
				List<string> list = new List<string>();
				int num = Compare(snap, snap2, null, hashSet, list, 10);
				int num2 = Compare(snap2, snap3, null, hashSet, list, 10);
				Report(num == 0 && num2 == 0, "frozen " + spot.Name + ": the same frame gathered three times comes out the same to the bit", (num + num2 == 0) ? null : $"{num} + {num2} numbers differ, {hashSet.Count} places; {Groups(snap, hashSet)}");
				foreach (string item in list)
				{
					Log.Info("CITYPERF frozen " + spot.Name + " noise: " + item);
				}
				_summary.Add(string.Format("frozen {0}: {1}; {2} meshes, {3} creatures tagged, {4} heads, {5} numbers; a run {6:F1} ms", spot.Name, (num + num2 == 0) ? "the same to the bit three times" : $"NOT the same three times ({hashSet.Count} places: {Groups(snap, hashSet)})", snap.Meshes, snap.UnitTags, snap.Heads, snap.Words, totalMilliseconds));
				for (int j = 0; j < NpcTune.Switches.Count; j++)
				{
					NpcTune.Switch obj = NpcTune.Switches[j];
					Gather(state, j);
					Snap run = Take(sceneForTest);
					Gather(state, -1);
					Snap run2 = Take(sceneForTest);
					if (obj == _canary)
					{
						Caught(spot.Name, snap3, run, hashSet);
					}
					else
					{
						Judge(spot.Name, obj.Name + " on", snap3, run, hashSet);
					}
					Judge(spot.Name, obj.Name + " off again", snap3, run2, hashSet);
				}
				if (NpcTune.Switches.Count > 1)
				{
					Gather(state, -2);
					Snap run3 = Take(sceneForTest);
					Gather(state, -1);
					Snap run4 = Take(sceneForTest);
					Judge(spot.Name, "all switches on", snap3, run3, hashSet);
					Judge(spot.Name, "all off again", snap3, run4, hashSet);
				}
				TimeRuns(spot.Name, state);
			}
			finally
			{
				_ships = (_feet = null);
				NpcTune.Restore(array);
				PoseClock.Thaw();
				UnityEngine.Random.state = state;
			}
			yield return null;
		}

		private bool Gather(UnityEngine.Random.State random, int only)
		{
			for (int i = 0; i < NpcTune.Switches.Count; i++)
			{
				NpcTune.Switches[i].Set((only == -2 && NpcTune.Switches[i] != _canary) || i == only);
			}
			UnityEngine.Random.state = random;
			if (_ships != null)
			{
				Creatures.PutBackShipsForTest(_ships);
			}
			if (_feet != null)
			{
				People.PutBackFeetForTest(_feet);
			}
			return _mod.CollectSceneForTest(0f);
		}

		private void Caught(string spot, Snap off, Snap run, HashSet<long> noise)
		{
			List<string> list = new List<string>();
			int num = Compare(off, run, noise, null, list, 4);
			Report(num > 0, $"frozen {spot}: canary on (the pose clock {0.25f} s on) is caught", num + " numbers differ");
			_summary.Add("frozen " + spot + " canary on: " + ((num > 0) ? ("caught, " + num + " numbers differ") : "NOT caught"));
			foreach (string item in list)
			{
				Log.Info("CITYPERF frozen " + spot + " canary on: " + item);
			}
		}

		private void TimeRuns(string spot, UnityEngine.Random.State random)
		{
			long timestamp = Stopwatch.GetTimestamp();
			for (int i = 0; i < 1000000; i++)
			{
				Stopwatch.GetTimestamp();
			}
			double num = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000000000.0 / (double)Stopwatch.Frequency / 1000000.0;
			bool sampleEveryFrame = SceneClock.SampleEveryFrame;
			List<float> list = new List<float>();
			List<float> list2 = new List<float>();
			float[] array = NewSplit();
			try
			{
				for (int j = 0; j < 25; j++)
				{
					SceneClock.NoSampling = true;
					list.Add(TimedGather(random, -1));
					SceneClock.NoSampling = false;
					list2.Add(SplitGather(random, -1, array));
				}
				float num2 = Least(list2) - Least(list);
				float num3 = Rank(list2, 0.5f) - Rank(list, 0.5f);
				string text = $"clock {spot}: a clock read {num:F1} ns; the frozen frame {Least(list):F2} ms (median {Rank(list, 0.5f):F2}) with no creature timed, " + $"{Least(list2):F2} ({Rank(list2, 0.5f):F2}) with every one: the laps cost {num2:F2} ({num3:F2}) ms in a timed frame, {num2 / 8f:F3} ms a frame in play (one in eight timed)";
				Log.Info("CITYPERF " + text);
				_summary.Add(text);
				Log.Info("CITYPERF frozen split " + spot + " all off: " + BucketText(array));
				for (int k = 0; k < NpcTune.Switches.Count; k++)
				{
					List<float> list3 = new List<float>();
					List<float> list4 = new List<float>();
					float[] array2 = NewSplit();
					float[] array3 = NewSplit();
					for (int l = 0; l < 25; l++)
					{
						list3.Add(SplitGather(random, -1, array2));
						list4.Add(SplitGather(random, k, array3));
					}
					Gather(random, -1);
					float num4 = Least(list3);
					float num5 = Least(list4);
					string text2 = $"frozen time {spot} {NpcTune.Switches[k].Name}: off {num4:F2} (median {Rank(list3, 0.5f):F2}) -> on {num5:F2} ({Rank(list4, 0.5f):F2}) ms ({num5 - num4:+0.00;-0.00} ms, {Pct(num5 - num4, num4)}); " + "parts that moved: " + Moved(array2, array3);
					Log.Info("CITYPERF " + text2);
					Log.Info("CITYPERF frozen split " + spot + " " + NpcTune.Switches[k].Name + " on: " + BucketText(array3));
					_summary.Add(text2);
				}
			}
			finally
			{
				SceneClock.NoSampling = false;
				SceneClock.SampleEveryFrame = sampleEveryFrame;
				SceneClock.ResetWindow();
			}
		}

		private static float[] NewSplit()
		{
			float[] array = new float[26];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = float.MaxValue;
			}
			return array;
		}

		private float SplitGather(UnityEngine.Random.State random, int only, float[] least)
		{
			bool sampleEveryFrame = SceneClock.SampleEveryFrame;
			SceneClock.SampleEveryFrame = true;
			SceneClock.ResetWindow();
			float result = TimedGather(random, only);
			SceneClock.Split split = SceneClock.Snapshot();
			SceneClock.SampleEveryFrame = sampleEveryFrame;
			for (int i = 0; i < least.Length; i++)
			{
				least[i] = Mathf.Min(least[i], split.Ms[i]);
			}
			return result;
		}

		private static string BucketText(float[] m)
		{
			return $"crowd {P(SceneClock.B.Crowd):F2}, ticks {P(SceneClock.B.Ticks):F2}, begin {P(SceneClock.B.Begin):F2}, units {P(SceneClock.B.Units):F2} (checks {P(SceneClock.B.Checks):F2}, steer {P(SceneClock.B.Steer):F2}, track {P(SceneClock.B.Track):F2} [route {P(SceneClock.B.Route):F2}, step {P(SceneClock.B.Step):F2}, face {P(SceneClock.B.Face):F2}], " + $"sprites {P(SceneClock.B.Sprites):F2}, ground {P(SceneClock.B.Ground):F2}, people {P(SceneClock.B.People):F2} [figure {P(SceneClock.B.Figure):F2}, pose {P(SceneClock.B.Pose):F2}, extras {P(SceneClock.B.Extras):F2}, " + $"place {P(SceneClock.B.Place):F2}, head {P(SceneClock.B.Head):F2}, gear {P(SceneClock.B.Gear):F2}], creatures {P(SceneClock.B.Creatures):F2}, other {P(SceneClock.B.Other):F2}, far {P(SceneClock.B.Far):F2}), " + $"buildings {P(SceneClock.B.Buildings):F2}, far props {P(SceneClock.B.FarProps):F2}, fx {P(SceneClock.B.Fx):F2}, rest {P(SceneClock.B.Rest):F2} ms (each part's fastest of {25})";
			float P(SceneClock.B b)
			{
				return m[(int)b];
			}
		}

		private static string Moved(float[] off, float[] on)
		{
			List<string> list = new List<string>();
			for (int i = 0; i < off.Length; i++)
			{
				if (Mathf.Abs(on[i] - off[i]) >= 0.05f)
				{
					SceneClock.B b = (SceneClock.B)i;
					list.Add($"{b.ToString().ToLowerInvariant()} {off[i]:F2} -> {on[i]:F2}");
				}
			}
			if (list.Count <= 0)
			{
				return "none by 0.05 ms";
			}
			return string.Join(", ", list);
		}

		private float TimedGather(UnityEngine.Random.State random, int only)
		{
			long timestamp = Stopwatch.GetTimestamp();
			Gather(random, only);
			return (float)((double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency);
		}

		private static float Least(List<float> values)
		{
			float num = float.MaxValue;
			foreach (float value in values)
			{
				num = Mathf.Min(num, value);
			}
			if (values.Count <= 0)
			{
				return 0f;
			}
			return num;
		}

		private void Judge(string spot, string what, Snap off, Snap run, HashSet<long> noise)
		{
			List<string> list = new List<string>();
			int num = Compare(off, run, noise, null, list, 10);
			int num2 = Compare(off, run, null, null, null, 0) - num;
			Report(num == 0, "frozen " + spot + ": " + what + ", the same to the bit as all off", (num != 0) ? $"{num} numbers differ ({num2} more in the noise)" : ((num2 > 0) ? (num2 + " numbers differ, all where the frame itself is noisy") : null));
			_summary.Add("frozen " + spot + " " + what + ": " + ((num == 0) ? "exact" : ("DIFFERS in " + num + " numbers")) + ((num2 > 0) ? (" (" + num2 + " in the noise)") : ""));
			foreach (string item in list)
			{
				Log.Info("CITYPERF frozen " + spot + " " + what + ": " + item);
			}
		}

		private static uint B(float f)
		{
			return NpcTune.Bits(f);
		}

		private static uint B(bool b)
		{
			return b ? 1u : 0u;
		}

		private static Snap Take(SceneCollector scene)
		{
			Snap snap = new Snap();
			List<BaseSimObject> tagged = scene.Tagged;
			List<uint> list = new List<uint>(64);
			foreach (MeshInstance mesh in scene.Meshes)
			{
				list.Clear();
				if (mesh == null)
				{
					snap.Parts[0].Add(new Rec
					{
						Label = "(null)",
						W = new uint[0]
					});
					continue;
				}
				list.Add(B(mesh.Position.X));
				list.Add(B(mesh.Position.Y));
				list.Add(B(mesh.Position.Z));
				Add(list, mesh.Basis.C0);
				Add(list, mesh.Basis.C1);
				Add(list, mesh.Basis.C2);
				list.Add((uint)mesh.Hidden);
				list.Add((uint)(mesh.Hidden >> 32));
				list.Add(mesh.Flash);
				list.Add(mesh.Multiply);
				list.Add((uint)mesh.Tag);
				list.Add(B(mesh.Emissive) | (B(mesh.NoShadow) << 1) | (B(mesh.Static) << 2) | (B(mesh.ShadowOnly) << 3) | (B(mesh.KeepSmall) << 4) | (B(mesh.FadeKeepsShadow) << 5) | (B(mesh.Cloud != null) << 6));
				list.Add(B(mesh.Glow));
				list.Add(B(mesh.Snow));
				list.Add(B(mesh.Sway));
				list.Add(B(mesh.SwayInvTop));
				list.Add(B(mesh.Fade));
				list.Add(B(mesh.DissolveScale));
				list.Add(B(mesh.SortDepth));
				Rec rec = new Rec
				{
					Label = ((mesh.Model != null) ? mesh.Model.Name : "(no model)") + " " + Who(scene, mesh.Tag),
					Model = mesh.Model
				};
				if (mesh.PartRotation != null)
				{
					rec.Rot = mesh.PartRotation.Length;
					Mat3[] partRotation = mesh.PartRotation;
					for (int i = 0; i < partRotation.Length; i++)
					{
						Mat3 mat = partRotation[i];
						Add(list, mat.C0);
						Add(list, mat.C1);
						Add(list, mat.C2);
					}
				}
				if (mesh.PartOffset != null)
				{
					rec.Off = mesh.PartOffset.Length;
					Vec3[] partOffset = mesh.PartOffset;
					foreach (Vec3 v in partOffset)
					{
						Add(list, v);
					}
				}
				if (mesh.PartScale != null)
				{
					rec.Scl = mesh.PartScale.Length;
					float[] partScale = mesh.PartScale;
					foreach (float f in partScale)
					{
						list.Add(B(f));
					}
				}
				if (mesh.Palette != null)
				{
					rec.Pal = mesh.Palette.Length;
					uint[] palette = mesh.Palette;
					foreach (uint item in palette)
					{
						list.Add(item);
					}
				}
				rec.W = list.ToArray();
				snap.Parts[0].Add(rec);
				if ((mesh.Tag >= 0 && mesh.Tag < scene.UnitTagEnd) || mesh.Tag == 2147483646)
				{
					snap.UnitMeshes++;
				}
			}
			foreach (VoxelObject voxel in scene.Voxels)
			{
				list.Clear();
				list.Add((uint)voxel.Tag);
				Add(list, voxel.Origin);
				Add(list, voxel.U);
				Add(list, voxel.V);
				Add(list, voxel.N);
				list.Add(B(voxel.SizeU));
				list.Add(B(voxel.SizeV));
				list.Add(B(voxel.SizeN));
				list.Add(voxel.Flash);
				list.Add(voxel.Multiply);
				list.Add(B(voxel.Overlay) | (B(voxel.NoShadow) << 1) | (B(voxel.Emissive) << 2));
				list.Add(B(voxel.Glow));
				list.Add(B(voxel.SortDepth));
				snap.Parts[1].Add(new Rec
				{
					Label = "voxels " + Who(scene, voxel.Tag),
					W = list.ToArray()
				});
			}
			foreach (PointLight light in scene.Lights)
			{
				snap.Parts[2].Add(new Rec
				{
					Label = "light",
					W = new uint[8]
					{
						B(light.X),
						B(light.Y),
						B(light.Z),
						B(light.Radius),
						B(light.R),
						B(light.G),
						B(light.B),
						B(light.Glow)
					}
				});
			}
			for (int j = 0; j < tagged.Count; j++)
			{
				BaseSimObject baseSimObject = tagged[j];
				snap.Parts[3].Add(new Rec
				{
					Label = Name(baseSimObject),
					W = new uint[1] { (baseSimObject != null) ? ((uint)RuntimeHelpers.GetHashCode(baseSimObject)) : 0u }
				});
				if (!(baseSimObject is Actor a))
				{
					continue;
				}
				snap.UnitTags++;
				list.Clear();
				if (Hitboxes.Get(a, out var box))
				{
					Add(list, box.Centre);
					list.Add(B(box.AX));
					list.Add(B(box.AY));
					list.Add(B(box.HalfA));
					list.Add(B(box.HalfB));
					list.Add(B(box.HalfUp));
				}
				else
				{
					for (int k = 0; k < 8; k++)
					{
						list.Add(uint.MaxValue);
					}
				}
				HeadBox head2;
				bool head = Hitboxes.GetHead(a, out head2);
				list.Add(B(head));
				if (head)
				{
					snap.Heads++;
					Add(list, head2.Centre);
					Add(list, head2.X);
					Add(list, head2.Y);
					Add(list, head2.Z);
				}
				else
				{
					for (int l = 0; l < 12; l++)
					{
						list.Add(0u);
					}
				}
				snap.Parts[4].Add(new Rec
				{
					Label = Name(baseSimObject),
					W = list.ToArray()
				});
			}
			foreach (FarActor farActor in scene.FarActors)
			{
				snap.Parts[5].Add(new Rec
				{
					Label = "far creature",
					W = new uint[6]
					{
						B(farActor.X),
						B(farActor.Y),
						B(farActor.Z),
						B(farActor.Width),
						B(farActor.Height),
						farActor.Color
					}
				});
			}
			snap.Meshes = scene.Meshes.Count;
			snap.Voxels = scene.Voxels.Count;
			snap.Billboards = scene.Billboards.Count;
			snap.Lights = scene.Lights.Count;
			snap.Tagged = tagged.Count;
			snap.Parts[6].Add(new Rec
			{
				Label = "counts",
				W = new uint[7]
				{
					(uint)scene.Meshes.Count,
					(uint)scene.Voxels.Count,
					(uint)scene.Billboards.Count,
					(uint)scene.Lights.Count,
					(uint)tagged.Count,
					(uint)scene.UnitTagEnd,
					(uint)scene.FarActors.Count
				}
			});
			List<Rec>[] parts = snap.Parts;
			for (int i = 0; i < parts.Length; i++)
			{
				foreach (Rec item2 in parts[i])
				{
					snap.Words += item2.W.Length;
				}
			}
			return snap;
		}

		private static void Add(List<uint> words, Vec3 v)
		{
			words.Add(B(v.X));
			words.Add(B(v.Y));
			words.Add(B(v.Z));
		}

		private static string Who(SceneCollector scene, int tag)
		{
			if (tag == 2147483646)
			{
				return "(you)";
			}
			if (tag < 0 || tag >= scene.Tagged.Count)
			{
				return "(tag " + tag + ")";
			}
			return Name(scene.Tagged[tag]);
		}

		private static string Name(BaseSimObject o)
		{
			try
			{
				if (o is Actor actor)
				{
					return "unit " + ((actor.data != null) ? actor.data.id.ToString() : "?") + " " + ((actor.asset != null) ? actor.asset.id : "?");
				}
				if (o is Building building)
				{
					return "building " + ((building.asset != null) ? building.asset.id : "?") + ((building.current_tile != null) ? (" at " + building.current_tile.x + "," + building.current_tile.y) : "");
				}
			}
			catch
			{
			}
			if (o != null)
			{
				return o.GetType().Name;
			}
			return "(nothing)";
		}

		private static long Key(int section, int index, int word)
		{
			return ((long)section << 56) | ((long)index << 24) | (word & 0xFFFFFF);
		}

		private static int Compare(Snap a, Snap b, HashSet<long> skip, HashSet<long> places, List<string> lines, int most)
		{
			int num = 0;
			for (int i = 0; i < a.Parts.Length; i++)
			{
				List<Rec> list = a.Parts[i];
				List<Rec> list2 = b.Parts[i];
				if (list.Count != list2.Count)
				{
					long item = Key(i, 16777215, 16777215);
					if (skip == null || !skip.Contains(item))
					{
						num++;
						places?.Add(item);
						if (lines != null && lines.Count < most)
						{
							lines.Add($"{(Section)i}: {list.Count} vs {list2.Count} of them");
						}
					}
				}
				int num2 = Math.Min(list.Count, list2.Count);
				for (int j = 0; j < num2; j++)
				{
					Rec rec = list[j];
					Rec rec2 = list2[j];
					if (rec.Label != rec2.Label || rec.Rot != rec2.Rot || rec.Off != rec2.Off || rec.Scl != rec2.Scl || rec.Pal != rec2.Pal || rec.W.Length != rec2.W.Length)
					{
						long item2 = Key(i, j, 16777215);
						if (skip == null || !skip.Contains(item2))
						{
							num++;
							places?.Add(item2);
							if (lines != null && lines.Count < most)
							{
								lines.Add($"{(Section)i} {j}: {rec.Label} [{rec.Rot},{rec.Off},{rec.Scl},{rec.Pal}] vs {rec2.Label} [{rec2.Rot},{rec2.Off},{rec2.Scl},{rec2.Pal}]");
							}
						}
						continue;
					}
					for (int k = 0; k < rec.W.Length; k++)
					{
						if (rec.W[k] == rec2.W[k])
						{
							continue;
						}
						long item3 = Key(i, j, k);
						if (skip == null || !skip.Contains(item3))
						{
							num++;
							places?.Add(item3);
							if (lines != null && lines.Count < most)
							{
								lines.Add($"{(Section)i} {j} ({rec.Label}): {Field((Section)i, rec, k)} {Value((Section)i, rec, k, rec.W[k])} vs {Value((Section)i, rec, k, rec2.W[k])}");
							}
						}
					}
				}
			}
			return num;
		}

		private static string Groups(Snap snap, HashSet<long> places)
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			foreach (long place in places)
			{
				int num = (int)(place >> 56);
				int num2 = (int)((place >> 24) & 0xFFFFFFFFu);
				List<Rec> list = snap.Parts[num];
				Section section = (Section)num;
				string key = section.ToString() + " " + ((num2 < list.Count) ? First(list[num2].Label) : "count");
				dictionary[key] = ((!dictionary.TryGetValue(key, out var value)) ? 1 : (value + 1));
			}
			List<KeyValuePair<string, int>> list2 = new List<KeyValuePair<string, int>>(dictionary);
			list2.Sort((KeyValuePair<string, int> p, KeyValuePair<string, int> q) => q.Value.CompareTo(p.Value));
			List<string> list3 = new List<string>();
			for (int num3 = 0; num3 < list2.Count && num3 < 5; num3++)
			{
				list3.Add(list2[num3].Key + " x" + list2[num3].Value);
			}
			return string.Join(", ", list3);
		}

		private static string First(string label)
		{
			int num = label.IndexOf(' ');
			if (num <= 0)
			{
				return label;
			}
			return label.Substring(0, num);
		}

		private static string Field(Section s, Rec r, int j)
		{
			switch (s)
			{
			case Section.Voxel:
				if (j >= VoxelFields.Length)
				{
					return "#" + j;
				}
				return VoxelFields[j];
			case Section.Light:
				if (j >= LightFields.Length)
				{
					return "#" + j;
				}
				return LightFields[j];
			case Section.Box:
				if (j >= BoxFields.Length)
				{
					return "#" + j;
				}
				return BoxFields[j];
			case Section.Far:
				if (j >= FarFields.Length)
				{
					return "#" + j;
				}
				return FarFields[j];
			case Section.Counts:
				if (j >= CountFields.Length)
				{
					return "#" + j;
				}
				return CountFields[j];
			case Section.Tagged:
				return "who";
			default:
			{
				if (j < MeshFields.Length)
				{
					return MeshFields[j];
				}
				j -= MeshFields.Length;
				int num = Math.Max(0, r.Rot) * 9;
				int num2 = Math.Max(0, r.Off) * 3;
				int num3 = Math.Max(0, r.Scl);
				if (j < num)
				{
					return string.Format("PartRotation[{0}].C{1}.{2}", Part(r, j / 9), j % 9 / 3, "XYZ"[j % 3]);
				}
				j -= num;
				if (j < num2)
				{
					return string.Format("PartOffset[{0}].{1}", Part(r, j / 3), "XYZ"[j % 3]);
				}
				j -= num2;
				if (j < num3)
				{
					return "PartScale[" + Part(r, j) + "]";
				}
				j -= num3;
				return $"Palette[{j}]";
			}
			}
		}

		private static string Part(Rec r, int i)
		{
			if (r.Model == null || r.Model.Parts == null || i >= r.Model.Parts.Length)
			{
				return i.ToString();
			}
			return i + " " + r.Model.Parts[i].Name;
		}

		private static string Value(Section s, Rec r, int j, uint w)
		{
			if (s != Section.Counts && s != Section.Tagged && (s != Section.Voxel || (j != 0 && j != 16 && j != 17 && j != 18)) && (s != Section.Box || j != 8) && (s != Section.Far || j != 5) && (s != Section.Mesh || ((j < 12 || j > 17) && !Field(s, r, j).StartsWith("Palette", StringComparison.Ordinal))))
			{
				return NpcTune.Show(NpcTune.FromBits(w));
			}
			string[] obj = new string[5]
			{
				"0x",
				w.ToString("X8"),
				" (",
				null,
				null
			};
			int num = (int)w;
			obj[3] = num.ToString();
			obj[4] = ")";
			return string.Concat(obj);
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
			float num2 = Mathf.Atan2(vector3.y, vector3.x);
			_spots.Add(new Spot
			{
				Name = "street",
				Eye = vector,
				Height = 0f,
				Yaw = num2,
				Pitch = 1.2f
			});
			_spots.Add(new Spot
			{
				Name = "away",
				Eye = vector,
				Height = 0f,
				Yaw = num2 + MathF.PI,
				Pitch = 1.2f
			});
			Vector2 vector4 = town + new Vector2(28f, 0f);
			float num3 = -99f;
			for (int j = 0; j < 16; j++)
			{
				float f2 = (float)j * MathF.PI / 8f;
				Vector2 vector5 = town + new Vector2(Mathf.Cos(f2), Mathf.Sin(f2)) * 28f;
				if (OnLand(vector5))
				{
					float num4 = terrainForTest.SampleHeight(vector5.x, vector5.y);
					if (num4 > num3)
					{
						num3 = num4;
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

		internal static int SaveSlot(string options)
		{
			int num = options.IndexOf("map", StringComparison.Ordinal);
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

		internal static Actor BusiestTown(out WorldTile centre, out int crowd, out City town)
		{
			centre = null;
			town = null;
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
				float num2 = float.MaxValue;
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
						float num3 = float.MaxValue;
						try
						{
							flag = item2.isAdult();
							num3 = item2.getAgeRatio();
						}
						catch
						{
						}
						if (flag && num3 < num2)
						{
							actor = item2;
							num2 = num3;
						}
					}
				}
				if (actor != null && num > crowd)
				{
					crowd = num;
					result = actor;
					centre = worldTile;
					town = item;
				}
			}
			return result;
		}

		private void Report(bool ok, string what, string detail = null)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info("CITYPERF " + (ok ? "PASS " : "FAIL ") + what + ((detail != null) ? (" (" + detail + ")") : ""));
		}

		private void Finish()
		{
			try
			{
				SceneClock.SampleEveryFrame = false;
				SceneClock.NoSampling = false;
				PoseClock.Thaw();
				foreach (NpcTune.LiveCheck liveCheck in NpcTune.LiveChecks)
				{
					try
					{
						liveCheck.Enable(obj: false);
					}
					catch
					{
					}
				}
				if (_canary != null)
				{
					NpcTune.Switches.Remove(_canary);
				}
				Config.paused = false;
				if (_saved)
				{
					Settings settings = _mod.Settings;
					settings.GpuRender = _savedGpu;
					settings.Resolution = _savedRes;
					settings.TimeMode = _savedTime;
					settings.DetailDistance = _savedDetail;
					settings.CreatureDistance = _savedCreatures;
					settings.DrawDistance = _savedDraw;
					QualitySettings.vSyncCount = _savedVsync;
					Application.targetFrameRate = _savedFps;
					NpcTune.Restore(_savedSwitches);
					_mod.ForceRelight();
				}
				if (_mod != null)
				{
					_mod.DebugEyeSet = false;
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
				Log.Error("CITYPERF putting things back: " + ex.Message);
			}
			foreach (string item in _summary)
			{
				Log.Info("CITYPERF summary " + item);
			}
			Log.Info($"CITYPERF {_pass} passed, {_fail} failed");
			Log.Info("CITYPERF DONE");
			if (_quit)
			{
				Application.Quit();
			}
		}
	}
}
