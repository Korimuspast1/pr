using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FirstPerson.Gpu;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class GodPerfTest : MonoBehaviour
	{
		private delegate void Toggle(bool on);

		private float _burnMs;

		private bool _burnMemory;

		private int[] _burnArray;

		private int _burnAt;

		private long _burnSink;

		private const float BlockSeconds = 6f;

		private const int DropFrames = 12;

		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private bool _quit;

		private bool _timing;

		private bool _pictures;

		private int _only;

		private Vector2 _at;

		private float _zoom = 91f;

		private bool _savedFrameUnits = true;

		private bool _savedFrameBuildings = true;

		private bool _savedFarPoses;

		private int _savedCrowd = -1;

		private readonly List<string> _summary = new List<string>();

		private static Toggle BothToggle => delegate(bool v)
		{
			SceneCollector.GodFrameUnits = v;
			SceneCollector.GodFrameBuildings = v;
		};

		private void Burn()
		{
			if (_burnMs <= 0f)
			{
				return;
			}
			long num = Stopwatch.GetTimestamp() + (long)((double)(_burnMs * (float)Stopwatch.Frequency) / 1000.0);
			if (!_burnMemory)
			{
				while (Stopwatch.GetTimestamp() < num)
				{
					_burnSink++;
				}
				return;
			}
			if (_burnArray == null)
			{
				_burnArray = new int[67108864];
			}
			int[] burnArray = _burnArray;
			while (Stopwatch.GetTimestamp() < num)
			{
				for (int i = 0; i < 4096; i++)
				{
					_burnAt += 16;
					if (_burnAt >= burnArray.Length)
					{
						_burnAt -= burnArray.Length;
					}
					burnArray[_burnAt]++;
				}
			}
		}

		private IEnumerator BenchProbe()
		{
			_zoom = 108f;
			bool wasBench = Bench.bench_enabled;
			Bench.bench_enabled = true;
			try
			{
				(string label, bool god, bool noUnits, float burn, bool memory)[] plan = new(string, bool, bool, float, bool)[5]
				{
					("3D god view", true, false, 0f, false),
					("3D god view, no creatures gathered", true, true, 0f, false),
					("flat map", false, false, 0f, false),
					("flat map + 50 ms over memory", false, false, 50f, true),
					("flat map + 50 ms spinning", false, false, 50f, false)
				};
				for (int round = 0; round < 2; round++)
				{
					for (int k = 0; k < plan.Length; k++)
					{
						(string label, bool god, bool noUnits, float burn, bool memory) p = plan[(round == 0) ? k : (plan.Length - 1 - k)];
						if (_mod.Settings.GodView != p.god)
						{
							_mod.SetGodView(p.god);
						}
						SceneCollector.SkipUnitsForTest = p.noUnits;
						_burnMs = p.burn;
						_burnMemory = p.memory;
						yield return Settle(4f);
						yield return BenchBlock("probe zoom 108, " + p.label);
					}
				}
			}
			finally
			{
				GodPerfTest godPerfTest = this;
				Bench.bench_enabled = wasBench;
				SceneCollector.SkipUnitsForTest = false;
				godPerfTest._burnMs = 0f;
				godPerfTest._burnArray = null;
			}
			if (!_mod.Settings.GodView)
			{
				_mod.SetGodView(on: true);
			}
		}

		private IEnumerator BenchHold()
		{
			_zoom = 108f;
			bool wasBench = Bench.bench_enabled;
			Bench.bench_enabled = true;
			try
			{
				if (!_mod.Settings.GodView)
				{
					_mod.SetGodView(on: true);
				}
				bool[] array = new bool[4] { false, true, true, false };
				foreach (bool hold in array)
				{
					WorldBoxMod.MapHoldOff = !hold;
					yield return Settle(4f);
					yield return BenchBlock("hold zoom 108, 3D god view, flat map's drawing " + (hold ? "held" : "as ever"));
				}
			}
			finally
			{
				Bench.bench_enabled = wasBench;
				WorldBoxMod.MapHoldOff = false;
			}
		}

		private IEnumerator BenchLean()
		{
			bool wasBench = Bench.bench_enabled;
			Bench.bench_enabled = true;
			try
			{
				if (!_mod.Settings.GodView)
				{
					_mod.SetGodView(on: true);
				}
				(string label, bool line, bool lean, bool coast, bool phased)[] plan = new(string, bool, bool, bool, bool)[3]
				{
					("as before", false, false, false, true),
					("god line, lean + coast, copies in one pass", true, true, true, false),
					("god line, lean + coast, copies in steps", true, true, true, true)
				};
				float[] array = new float[2] { 108f, 60f };
				for (int i = 0; i < array.Length; i++)
				{
					float z = (_zoom = array[i]);
					for (int round = 0; round < 2; round++)
					{
						for (int k = 0; k < plan.Length; k++)
						{
							(string label, bool line, bool lean, bool coast, bool phased) p = plan[(round == 0) ? k : (plan.Length - 1 - k)];
							WorldBoxMod.GodLineOff = !p.line;
							People.LeanOff = !p.lean;
							People.CoastOff = !p.coast;
							GpuModels.CrowdsPhased = p.phased;
							yield return Settle(4f);
							long lean0 = People.LeanPosed;
							long coast0 = People.Coasted;
							int f0 = Time.frameCount;
							yield return BenchBlock($"lean zoom {z:F0}, {p.label}");
							Log.Info($"GODPERF bench lean zoom {z:F0}, {p.label}: {(People.LeanPosed - lean0) / Mathf.Max(1, Time.frameCount - f0)} lean, {(People.Coasted - coast0) / Mathf.Max(1, Time.frameCount - f0)} coasting a frame");
						}
					}
				}
			}
			finally
			{
				Bench.bench_enabled = wasBench;
				WorldBoxMod.GodLineOff = false;
				People.LeanOff = false;
				People.CoastOff = false;
				GpuModels.CrowdsPhased = true;
			}
		}

		private IEnumerator BenchPics()
		{
			if (!_mod.Settings.GodView)
			{
				_mod.SetGodView(on: true);
			}
			float[] array = new float[3] { 108f, 60f, 30f };
			for (int i = 0; i < array.Length; i++)
			{
				float z = (_zoom = array[i]);
				WorldBoxMod.GodLineOff = true;
				Config.paused = false;
				PoseClock.Thaw();
				WorldBoxMod.GodTimeForTest = -1f;
				yield return Settle(4f);
				Config.paused = true;
				PoseClock.Freeze(PoseClock.Now);
				WorldBoxMod.GodTimeForTest = Time.unscaledTime;
				Color32[][] pics = new Color32[4][];
				int width = 0;
				int height = 0;
				string[] names = new string[4] { "before", "new", "before2", "new_onepass" };
				for (int j = 0; j < 4; j++)
				{
					WorldBoxMod.GodLineOff = j == 0 || j == 2;
					GpuModels.CrowdsPhased = j != 3;
					for (int f = 0; f < 45; f++)
					{
						Hold();
						yield return null;
					}
					pics[j] = _mod.GodPixelsForTest(out width, out height);
					Log.Info($"GODPERF pics zoom {z:F0} {names[j]}: {_mod.GodPerfReadForTest()}");
					_mod.GodPerfResetForTest();
					if (pics[j] != null)
					{
						Save($"gp_pic_z{z:F0}_{names[j]}.png", pics[j], width, height);
					}
				}
				if (pics[0] != null && pics[1] != null && pics[2] != null)
				{
					SaveDiff($"gp_pic_z{z:F0}_diff.png", pics[0], pics[1], width, height);
					Log.Info($"GODPERF pics zoom {z:F0}: {Differ(pics[0], pics[1])} pixels differ before/new, {Differ(pics[0], pics[2])} before/before, of {width}x{height}");
				}
				if (pics[1] != null && pics[3] != null)
				{
					Log.Info($"GODPERF pics zoom {z:F0}: {Differ(pics[1], pics[3])} pixels differ between the crowd's copies in steps and in one pass");
				}
				GpuModels.CrowdsPhased = true;
			}
			WorldBoxMod.GodLineOff = false;
			WorldBoxMod.GodTimeForTest = -1f;
			PoseClock.Thaw();
			Config.paused = false;
		}

		private IEnumerator BenchCells()
		{
			if (!_mod.Settings.GodView)
			{
				_mod.SetGodView(on: true);
			}
			float[] array = new float[3] { 108f, 60f, 30f };
			for (int i = 0; i < array.Length; i++)
			{
				float z = (_zoom = array[i]);
				SceneCollector.GodCellsOff = false;
				Config.paused = false;
				PoseClock.Thaw();
				WorldBoxMod.GodTimeForTest = -1f;
				yield return Settle(3f);
				Config.paused = true;
				PoseClock.Freeze(PoseClock.Now);
				WorldBoxMod.GodTimeForTest = Time.unscaledTime;
				string[] drawn = new string[4];
				for (int j = 0; j < 4; j++)
				{
					SceneCollector.GodCellsOff = j == 0 || j == 3;
					for (int f = 0; f < ((j == 0) ? 40 : 8); f++)
					{
						Hold();
						yield return null;
					}
					_mod.GodPerfResetForTest();
					for (int f = 0; f < 4; f++)
					{
						Hold();
						yield return null;
					}
					Match match = Regex.Match(_mod.GodPerfReadForTest(), "models \\d+ \\(\\d+ copies, \\d+ draws\\), crowd \\d+");
					drawn[j] = (match.Success ? match.Value : "?");
				}
				bool ok = ((drawn[0] == drawn[3]) ? (drawn[0] == drawn[1] && drawn[1] == drawn[2]) : (drawn[1] == drawn[0] || drawn[1] == drawn[3]));
				Report(ok, $"zoom {z:F0}: the off-frame cells draw what the full checks draw", drawn[0] + " / " + drawn[1]);
			}
			SceneCollector.GodCellsOff = false;
			WorldBoxMod.GodTimeForTest = -1f;
			PoseClock.Thaw();
			Config.paused = false;
			bool wasBench = Bench.bench_enabled;
			Bench.bench_enabled = true;
			try
			{
				_zoom = 108f;
				bool[] array2 = new bool[4] { false, true, true, false };
				foreach (bool on in array2)
				{
					SceneCollector.GodCellsOff = !on;
					yield return Settle(4f);
					yield return BenchBlock("cells zoom 108, " + (on ? "cells" : "full checks"));
				}
			}
			finally
			{
				Bench.bench_enabled = wasBench;
				SceneCollector.GodCellsOff = false;
			}
		}

		private IEnumerator BenchTrees()
		{
			if (!_mod.Settings.GodView)
			{
				_mod.SetGodView(on: true);
			}
			float[] array = new float[3] { 108f, 60f, 30f };
			for (int i = 0; i < array.Length; i++)
			{
				float z = (_zoom = array[i]);
				SceneCollector.KeptTreesOff = false;
				Config.paused = false;
				PoseClock.Thaw();
				WorldBoxMod.GodTimeForTest = -1f;
				yield return Settle(3f);
				Config.paused = true;
				PoseClock.Freeze(PoseClock.Now);
				WorldBoxMod.GodTimeForTest = Time.unscaledTime;
				string[] drawn = new string[4];
				for (int j = 0; j < 4; j++)
				{
					SceneCollector.KeptTreesOff = j == 0 || j == 3;
					for (int f = 0; f < ((j == 0) ? 40 : 8); f++)
					{
						Hold();
						yield return null;
					}
					_mod.GodPerfResetForTest();
					for (int f = 0; f < 4; f++)
					{
						Hold();
						yield return null;
					}
					Match match = Regex.Match(_mod.GodPerfReadForTest(), "models \\d+ \\(\\d+ copies, \\d+ draws\\), crowd \\d+");
					drawn[j] = (match.Success ? match.Value : "?");
				}
				bool ok = ((drawn[0] == drawn[3]) ? (drawn[0] == drawn[1] && drawn[1] == drawn[2]) : (drawn[1] == drawn[0] || drawn[1] == drawn[3]));
				Report(ok, $"zoom {z:F0}: the kept trees draw what full gatherings draw", drawn[0] + " / " + drawn[1]);
			}
			SceneCollector.KeptTreesOff = false;
			WorldBoxMod.GodTimeForTest = -1f;
			PoseClock.Thaw();
			Config.paused = false;
			bool wasBench = Bench.bench_enabled;
			Bench.bench_enabled = true;
			try
			{
				_zoom = 108f;
				bool[] array2 = new bool[4] { false, true, true, false };
				foreach (bool on in array2)
				{
					SceneCollector.KeptTreesOff = !on;
					yield return Settle(4f);
					yield return BenchBlock("trees zoom 108, " + (on ? "kept" : "gathered every frame"));
				}
			}
			finally
			{
				Bench.bench_enabled = wasBench;
				SceneCollector.KeptTreesOff = false;
			}
		}

		private IEnumerator BenchSteps(string options)
		{
			if (!_mod.Settings.GodView)
			{
				_mod.SetGodView(on: true);
			}
			int saved = _mod.Settings.WorldSteps;
			bool wasBench = Bench.bench_enabled;
			Bench.bench_enabled = true;
			try
			{
				float[] array = ((!options.Contains("zooms=60")) ? new float[1] { 108f } : new float[1] { 60f });
				for (int i = 0; i < array.Length; i++)
				{
					float z = (_zoom = array[i]);
					bool[] array2 = new bool[4] { false, true, true, false };
					foreach (bool on in array2)
					{
						_mod.Settings.WorldSteps = (on ? 1 : 0);
						yield return Settle(4f);
						long skipped0 = WorldSteps.Skipped;
						long doubled0 = WorldSteps.Doubled;
						double world0 = 0.0;
						try
						{
							world0 = World.world.getCurWorldTime();
						}
						catch
						{
						}
						float real0 = Time.realtimeSinceStartup;
						List<float> frames = new List<float>();
						for (float t = 0f; t < 6f; t += Time.unscaledDeltaTime)
						{
							Hold();
							yield return null;
							frames.Add(Time.unscaledDeltaTime * 1000f);
						}
						double num = world0;
						try
						{
							num = World.world.getCurWorldTime();
						}
						catch
						{
						}
						float b = Time.realtimeSinceStartup - real0;
						List<float> list = new List<float>();
						for (int k = 1; k < frames.Count; k++)
						{
							list.Add(Mathf.Abs(frames[k] - frames[k - 1]));
						}
						List<float> list2 = new List<float>(frames);
						list2.Sort();
						list.Sort();
						float num2 = list2[list2.Count / 2];
						float num3 = list2[Mathf.Min(list2.Count - 1, (int)((float)list2.Count * 0.95f))];
						float num4 = ((list.Count > 0) ? list[list.Count / 2] : 0f);
						Log.Info(string.Format("GODPERF bench steps zoom {0:F0}, {1}: {2} frames, p50 {3:F1} ms, p95 {4:F1} ms, ", z, on ? "30 a second" : "every frame", frames.Count, num2, num3) + $"frame-to-frame change p50 {num4:F1} ms; world clock {(num - world0) / (double)Mathf.Max(0.01f, b):F2} world s a real s; " + $"{WorldSteps.Skipped - skipped0} frames not stepped, {WorldSteps.Doubled - doubled0} double steps; {_mod.GodPerfReadForTest()}");
					}
				}
			}
			finally
			{
				GodPerfTest godPerfTest = this;
				Bench.bench_enabled = wasBench;
				godPerfTest._mod.Settings.WorldSteps = saved;
			}
		}

		private IEnumerator BenchAll()
		{
			if (!_mod.Settings.GodView)
			{
				_mod.SetGodView(on: true);
			}
			bool wasBench = Bench.bench_enabled;
			Bench.bench_enabled = true;
			try
			{
				float[] array = new float[2] { 108f, 60f };
				for (int i = 0; i < array.Length; i++)
				{
					float z = (_zoom = array[i]);
					bool[] array2 = new bool[4] { false, true, true, false };
					foreach (bool on in array2)
					{
						AllSavings(on);
						yield return Settle(4f);
						yield return BenchBlock(string.Format("all zoom {0:F0}, {1}", z, on ? "all on" : "all off (as before)"));
					}
				}
			}
			finally
			{
				Bench.bench_enabled = wasBench;
				AllSavings(on: true);
			}
		}

		private static void AllSavings(bool on)
		{
			WorldBoxMod.MapHoldOff = !on;
			WorldBoxMod.GodLineOff = !on;
			People.LeanOff = !on;
			People.CoastOff = !on;
			GpuModels.CrowdsPhased = on;
			SceneCollector.GodCellsOff = !on;
			SceneCollector.KeptTreesOff = !on;
		}

		private IEnumerator WorldBench(string options)
		{
			if (options.Contains("benchall"))
			{
				yield return BenchAll();
				yield break;
			}
			if (options.Contains("benchsteps"))
			{
				yield return BenchSteps(options);
				yield break;
			}
			if (options.Contains("benchcells"))
			{
				yield return BenchCells();
				yield break;
			}
			if (options.Contains("benchkept"))
			{
				yield return BenchTrees();
				yield break;
			}
			if (options.Contains("benchpics"))
			{
				yield return BenchPics();
				yield break;
			}
			if (options.Contains("benchprobe"))
			{
				yield return BenchProbe();
				yield break;
			}
			if (options.Contains("benchhold"))
			{
				yield return BenchHold();
				yield break;
			}
			if (options.Contains("benchlean"))
			{
				yield return BenchLean();
				yield break;
			}
			List<float> list = new List<float> { 108f, 60f, 30f };
			int num = options.IndexOf("zooms=", StringComparison.Ordinal);
			if (num >= 0)
			{
				list.Clear();
				string[] array = options.Substring(num + 6).Split(' ', '\r', '\n')[0].Split(',');
				for (int i = 0; i < array.Length; i++)
				{
					if (float.TryParse(array[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
					{
						list.Add(result);
					}
				}
			}
			List<int> levels = new List<int>();
			num = options.IndexOf("crowds=", StringComparison.Ordinal);
			if (num >= 0)
			{
				string[] array = options.Substring(num + 7).Split(' ', '\r', '\n')[0].Split(',');
				for (int i = 0; i < array.Length; i++)
				{
					if (int.TryParse(array[i], out var result2))
					{
						levels.Add(Mathf.Clamp(result2, 0, 3));
					}
				}
			}
			int savedCrowd = _mod.Settings.CrowdDetail;
			bool wasBench = Bench.bench_enabled;
			Bench.bench_enabled = true;
			try
			{
				foreach (float item in list)
				{
					float z = (_zoom = item);
					if (levels.Count > 0)
					{
						foreach (int level in levels)
						{
							if (!_mod.Settings.GodView)
							{
								_mod.SetGodView(on: true);
							}
							_mod.Settings.CrowdDetail = level;
							yield return Settle(4f);
							yield return BenchBlock($"zoom {z:F0}, 3D god view, crowd detail {level}");
						}
						_mod.Settings.CrowdDetail = savedCrowd;
						_mod.SetGodView(on: false);
						yield return Settle(4f);
						yield return BenchBlock($"zoom {z:F0}, flat map");
						continue;
					}
					bool[] array2 = new bool[4] { true, false, false, true };
					foreach (bool god in array2)
					{
						if (_mod.Settings.GodView != god)
						{
							_mod.SetGodView(god);
						}
						yield return Settle(4f);
						yield return BenchBlock(string.Format("zoom {0:F0}, {1}", z, god ? "3D god view" : "flat map"));
					}
				}
			}
			finally
			{
				GodPerfTest godPerfTest = this;
				Bench.bench_enabled = wasBench;
				godPerfTest._mod.Settings.CrowdDetail = savedCrowd;
			}
			if (!_mod.Settings.GodView)
			{
				_mod.SetGodView(on: true);
			}
		}

		private IEnumerator BenchBlock(string label)
		{
			for (int i = 0; i < 12; i++)
			{
				Hold();
				yield return null;
			}
			try
			{
				Bench.dict.Clear();
			}
			catch (Exception ex)
			{
				Log.Error("GODPERF bench clear: " + ex.Message);
			}
			Hitch.ResetSums();
			SceneClock.ResetWindow();
			SceneCollector.BuildClockOn = true;
			SceneCollector.TakeBuildClock();
			_mod.GodPerfResetForTest();
			List<float> frames = new List<float>();
			for (float t = 0f; t < 6f; t += Time.unscaledDeltaTime)
			{
				Hold();
				yield return null;
				frames.Add(Time.unscaledDeltaTime * 1000f);
			}
			frames.Sort();
			float num = frames[frames.Count / 2];
			int num2 = Mathf.Max(1, Hitch.SumFrames);
			int num3 = 0;
			try
			{
				num3 = World.world.units.Count;
			}
			catch
			{
			}
			Log.Info($"GODPERF bench {label}: {frames.Count} frames, p50 {num:F1} ms, {num3} creatures; per frame on the clock (cpu): " + $"updates {Hitch.SumUpdates / (double)num2:F1} ({Hitch.SumUpdatesCpu / (double)num2:F1}) of which the mod's Update {Hitch.SumModUpdate / (double)num2:F1}, " + $"mod late {Hitch.SumLate / (double)num2:F1} ({Hitch.SumLateCpu / (double)num2:F1}), after {Hitch.SumAfter / (double)num2:F1} ({Hitch.SumAfterCpu / (double)num2:F1})");
			if (_mod.Settings.GodView)
			{
				Log.Info("GODPERF bench " + label + " view: " + _mod.GodPerfReadForTest());
				Log.Info("GODPERF bench " + label + " split: " + CityPerfTest.SplitText(SceneClock.Snapshot()));
				Log.Info("GODPERF bench " + label + " " + SceneCollector.TakeBuildClock());
			}
			Log.Info("GODPERF bench " + label + " game: " + BenchTop(30));
		}

		private static string BenchTop(int most)
		{
			List<KeyValuePair<string, double>> list = new List<KeyValuePair<string, double>>();
			try
			{
				foreach (KeyValuePair<string, BenchmarkGroup> item in Bench.dict)
				{
					if (item.Value?.dict_data == null)
					{
						continue;
					}
					foreach (KeyValuePair<string, ToolBenchmarkData> dict_datum in item.Value.dict_data)
					{
						if (dict_datum.Value != null)
						{
							list.Add(new KeyValuePair<string, double>(item.Key + "/" + dict_datum.Key, dict_datum.Value.getAverage() * 1000.0));
						}
					}
				}
			}
			catch (Exception ex)
			{
				return "unreadable: " + ex.Message;
			}
			list.Sort((KeyValuePair<string, double> a, KeyValuePair<string, double> b) => b.Value.CompareTo(a.Value));
			StringBuilder stringBuilder = new StringBuilder(1024);
			for (int num = 0; num < list.Count && num < most && !(list[num].Value < 0.05); num++)
			{
				if (num > 0)
				{
					stringBuilder.Append(", ");
				}
				stringBuilder.Append(list[num].Key).Append(' ').Append(list[num].Value.ToString("F2"));
			}
			return stringBuilder.ToString();
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
					Log.Error("GODPERF stopped by an error: " + ex);
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
				Burn();
			}
		}

		private IEnumerator Run()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(path);
			_quit = options.Contains("quit");
			_pictures = options.Contains("pictures");
			_only = (options.Contains("unitsonly") ? 1 : (options.Contains("buildingsonly") ? 2 : 0));
			int slot = CityPerfTest.SaveSlot(options);
			if (slot <= 0)
			{
				slot = 22;
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
			_savedFrameUnits = SceneCollector.GodFrameUnits;
			_savedFrameBuildings = SceneCollector.GodFrameBuildings;
			_savedFarPoses = People.FarPoses;
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
				Log.Info("GODPERF no save in slot " + slot + ": the starting map");
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
			_at = ((centre != null) ? new Vector2((float)centre.x + 0.5f, (float)centre.y + 0.5f) : new Vector2((float)MapBox.width * 0.5f, (float)MapBox.height * 0.5f));
			int num = options.IndexOf("at=", StringComparison.Ordinal);
			if (num >= 0)
			{
				string[] array = options.Substring(num + 3).Split(' ', '\r', '\n')[0].Split(',');
				if (array.Length == 2 && float.TryParse(array[0], out var result) && float.TryParse(array[1], out var result2))
				{
					_at = new Vector2(result + 0.5f, result2 + 0.5f);
				}
			}
			int num2 = 0;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && (simple.current_position - _at).sqrMagnitude < 1600f)
				{
					num2++;
				}
			}
			Settings s = _mod.Settings;
			Log.Info($"GODPERF world: save {slot}, {MapBox.width}x{MapBox.height}, {World.world.units.getSimpleList().Count} creatures; busiest town {town?.name} ({crowd} within 15); " + $"looking at {_at.x:F0},{_at.y:F0} ({num2} creatures within 40); settings: gpu {s.GpuRender}, resolution {s.Resolution}, god view zoom {s.GodViewZoom}, " + $"shadows {s.Shadows}, far poses {s.FarPoses}; {SystemInfo.processorType} ({SystemInfo.processorCount} threads), {SystemInfo.graphicsDeviceName}, screen {Screen.width}x{Screen.height}");
			if (!s.GodView)
			{
				_mod.SetGodView(on: true);
			}
			_timing = true;
			SceneClock.SampleEveryFrame = true;
			yield return Settle(6f);
			if (!Report(_mod.GodShown && _mod.GodAlpha > 0.99f, "the 3D god view covers the map", $"alpha {_mod.GodAlpha:F2}"))
			{
				yield break;
			}
			if (options.Contains("bench"))
			{
				yield return WorldBench(options);
				yield break;
			}
			if (options.Contains("crowdfp"))
			{
				_savedCrowd = s.CrowdDetail;
				if (host == null)
				{
					Report(ok: false, "a grown-up in a town to take");
					yield break;
				}
				ControllableUnit.setControllableCreature(host);
				for (float t = 0f; t < 6f; t += Time.unscaledDeltaTime)
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
				yield return new WaitForSecondsRealtime(3f);
				Config.paused = true;
				PoseClock.Freeze(PoseClock.Now);
				int[] array2 = new int[3] { 0, 2, 3 };
				foreach (int level in array2)
				{
					s.CrowdDetail = level;
					for (int f = 0; f < 10; f++)
					{
						yield return null;
					}
					byte[] array3 = _mod.ShownPngForTest();
					Log.Info($"GODPERF crowd fp level {level}: {People.CrowdPosed} posed on the card so far");
					if (array3 != null)
					{
						File.WriteAllBytes(Path.Combine(Log.Folder, $"gp_fp_l{level}.png"), array3);
					}
				}
				s.CrowdDetail = 0;
				PoseClock.Thaw();
				Config.paused = false;
				array2 = new int[2] { 0, 1 };
				for (int i = 0; i < array2.Length; i++)
				{
					_ = array2[i];
					List<float> off = new List<float>();
					List<float> on = new List<float>();
					bool[] array4 = new bool[4] { false, true, true, false };
					foreach (bool v in array4)
					{
						s.CrowdDetail = (v ? 2 : 0);
						for (int f = 0; f < 12; f++)
						{
							yield return null;
						}
						long posed0 = People.CrowdPosed;
						List<float> frames = new List<float>();
						for (float t = 0f; t < 6f; t += Time.unscaledDeltaTime)
						{
							yield return null;
							frames.Add(Time.unscaledDeltaTime * 1000f);
						}
						frames.Sort();
						float num3 = frames[frames.Count / 2];
						(v ? on : off).Add(num3);
						Log.Info(string.Format("GODPERF crowd fp {0}: {1} frames, p50 {2:F1} ms, {3} a frame on the card; {4}", v ? "Medium" : "off", frames.Count, num3, (People.CrowdPosed - posed0) / Mathf.Max(1, frames.Count), _mod.GpuRendererForTest?.Stats));
					}
					_summary.Add($"first person, crowd detail Medium: frame p50 {(off[0] + off[1]) * 0.5f:F1} -> {(on[0] + on[1]) * 0.5f:F1} ms");
				}
				s.CrowdDetail = 0;
				yield break;
			}
			if (options.Contains("crowdgear"))
			{
				_savedCrowd = s.CrowdDetail;
				float[] array5 = new float[2] { 91f, 150f };
				for (int i = 0; i < array5.Length; i++)
				{
					float t = (_zoom = array5[i]);
					s.CrowdDetail = 3;
					yield return Settle(4f);
					if (t > 100f)
					{
						yield return Abba("crowd Low against off", delegate(bool flag3)
						{
							s.CrowdDetail = (flag3 ? 3 : 0);
						}, BothSet(v: true));
					}
					yield return Abba("crowd Medium (gear) against Low", delegate(bool flag3)
					{
						s.CrowdDetail = (flag3 ? 2 : 3);
					}, BothSet(v: true));
				}
				yield break;
			}
			if (options.Contains("crowdfar"))
			{
				_savedCrowd = s.CrowdDetail;
				Config.paused = true;
				PoseClock.Freeze(PoseClock.Now);
				WorldBoxMod.GodTimeForTest = Time.unscaledTime;
				_zoom = 150f;
				Config.paused = false;
				PoseClock.Thaw();
				yield return Settle(6f);
				Config.paused = true;
				PoseClock.Freeze(PoseClock.Now);
				int[] array2 = new int[4] { -1, 0, 2, 3 };
				for (int i = 0; i < array2.Length; i++)
				{
					int level = array2[i];
					SceneCollector.FarOutlines = true;
					SceneCollector.FarOutlinesOffForTest = level < 0;
					s.CrowdDetail = Mathf.Max(0, level);
					for (int f = 0; f < 40; f++)
					{
						Hold();
						yield return null;
					}
					int width;
					int height;
					Color32[] array6 = _mod.GodPixelsForTest(out width, out height);
					string arg = _mod.GodPerfReadForTest();
					_mod.GodPerfResetForTest();
					Log.Info($"GODPERF crowd far zoom 150 level {level}: {arg}");
					if (array6 != null)
					{
						Save("gp_far_z150_" + ((level < 0) ? "none" : ("l" + level)) + ".png", array6, width, height);
					}
				}
				s.CrowdDetail = 0;
				SceneCollector.FarOutlinesOffForTest = false;
				SceneCollector.FarOutlines = false;
				WorldBoxMod.GodTimeForTest = -1f;
				PoseClock.Thaw();
				Config.paused = false;
				_zoom = 150f;
				yield return Settle(3f);
				yield return Abba("crowd detail Medium", delegate(bool flag3)
				{
					s.CrowdDetail = (flag3 ? 2 : 0);
				}, BothSet(v: true));
				yield break;
			}
			if (options.Contains("crowdclose"))
			{
				Config.paused = true;
				PoseClock.Freeze(PoseClock.Now);
				WorldBoxMod.GodTimeForTest = Time.unscaledTime;
				float[] array5 = new float[2] { 12f, 20f };
				for (int i = 0; i < array5.Length; i++)
				{
					float t = (_zoom = array5[i]);
					float[] array7 = new float[2] { 0f, 5000f };
					for (int level = 0; level < array7.Length; level++)
					{
						float line = (WorldBoxMod.CrowdLineForTest = array7[level]);
						for (int f = 0; f < 8; f++)
						{
							Hold();
							yield return null;
						}
						int width2;
						int height2;
						Color32[] array8 = _mod.GodPixelsForTest(out width2, out height2);
						Log.Info($"GODPERF crowd close zoom {t:F0} line {line:F0}: {_mod.GodPerfReadForTest()}");
						_mod.GodPerfResetForTest();
						if (array8 != null)
						{
							Save(string.Format("gp_close_z{0:F0}_{1}.png", t, (line > 0f) ? "card" : "cpu"), array8, width2, height2);
						}
					}
				}
				WorldBoxMod.CrowdLineForTest = -1f;
				WorldBoxMod.GodTimeForTest = -1f;
				PoseClock.Thaw();
				Config.paused = false;
				yield break;
			}
			if (options.Contains("crowd"))
			{
				_savedCrowd = s.CrowdDetail;
				Config.paused = true;
				PoseClock.Freeze(PoseClock.Now);
				WorldBoxMod.GodTimeForTest = Time.unscaledTime;
				float[] array5 = new float[2] { 40f, 91f };
				for (int i = 0; i < array5.Length; i++)
				{
					float t = (_zoom = array5[i]);
					int[] array2 = new int[3] { 0, 2, 3 };
					foreach (int f in array2)
					{
						s.CrowdDetail = f;
						for (int f2 = 0; f2 < 8; f2++)
						{
							Hold();
							yield return null;
						}
						int width3;
						int height3;
						Color32[] array9 = _mod.GodPixelsForTest(out width3, out height3);
						string arg2 = _mod.GodPerfReadForTest();
						_mod.GodPerfResetForTest();
						Log.Info($"GODPERF crowd picture zoom {t:F0} level {f}: {arg2}");
						if (array9 != null)
						{
							Save($"gp_crowd_z{t:F0}_l{f}.png", array9, width3, height3);
						}
					}
				}
				s.CrowdDetail = 0;
				WorldBoxMod.GodTimeForTest = -1f;
				PoseClock.Thaw();
				Config.paused = false;
				array5 = new float[2] { 91f, 40f };
				foreach (float zoom in array5)
				{
					_zoom = zoom;
					yield return Settle(3f);
					yield return Abba("crowd detail Medium", delegate(bool flag3)
					{
						s.CrowdDetail = (flag3 ? 2 : 0);
					}, BothSet(v: true));
				}
				yield break;
			}
			if (!options.Contains("quick"))
			{
				Config.paused = true;
				PoseClock.Freeze(PoseClock.Now);
				WorldBoxMod.GodTimeForTest = Time.unscaledTime;
				float[] array5 = new float[2] { 91f, 40f };
				for (int i = 0; i < array5.Length; i++)
				{
					float num4 = (_zoom = array5[i]);
					yield return Same("zoom " + num4.ToString("F0"));
				}
				WorldBoxMod.GodTimeForTest = -1f;
				PoseClock.Thaw();
				Config.paused = false;
			}
			if (!options.Contains("exactonly"))
			{
				_zoom = 91f;
				yield return Settle(3f);
				yield return Measure("zoom 91, everything gathered", null, BothSet(v: false));
				yield return Abba("frame-only creatures", delegate(bool godFrameUnits)
				{
					SceneCollector.GodFrameUnits = godFrameUnits;
				}, BothSet(v: false));
				yield return Abba("frame-only buildings", delegate(bool godFrameBuildings)
				{
					SceneCollector.GodFrameBuildings = godFrameBuildings;
				}, BothSet(v: false));
				yield return Abba("frame-only, both", BothToggle, BothSet(v: false));
				yield return Abba("far poses, frame-only on", delegate(bool farPoses)
				{
					People.FarPoses = farPoses;
				}, BothSet(v: true));
				float[] array5 = new float[2] { 40f, 150f };
				foreach (float zoom2 in array5)
				{
					_zoom = zoom2;
					yield return Settle(3f);
					yield return Abba("frame-only, both", BothToggle, BothSet(v: false));
				}
			}
		}

		private static Action BothSet(bool v)
		{
			return delegate
			{
				SceneCollector.GodFrameUnits = v;
				SceneCollector.GodFrameBuildings = v;
			};
		}

		private IEnumerator Same(string where)
		{
			Color32[][] pics = new Color32[4][];
			string[] drawn = new string[4];
			int width = 0;
			int height = 0;
			long skipped = 0L;
			for (int i = 0; i < 4; i++)
			{
				BothSet(i >= 2)();
				if (_only == 1)
				{
					SceneCollector.GodFrameBuildings = false;
				}
				if (_only == 2)
				{
					SceneCollector.GodFrameUnits = false;
				}
				_mod.GodPerfResetForTest();
				SceneCollector.GodFrameSkipped = 0L;
				for (int f = 0; f < 8; f++)
				{
					Hold();
					yield return null;
				}
				if (i >= 2)
				{
					skipped += SceneCollector.GodFrameSkipped / 8;
				}
				pics[i] = _mod.GodPixelsForTest(out width, out height);
				Match match = Regex.Match(_mod.GodPerfReadForTest(), "models \\d+ \\(\\d+ copies, \\d+ draws\\)");
				drawn[i] = (match.Success ? match.Value : null);
				if (pics[i] == null)
				{
					Report(ok: false, where + ": the picture read back from the card");
					BothSet(v: true)();
					yield break;
				}
				if (i == 0 || i == 2)
				{
					Save("gp_" + where.Replace(' ', '_') + "_" + ((i == 0) ? "all" : "frame") + ".png", pics[i], width, height);
				}
			}
			BothSet(v: true)();
			int num = Differ(pics[0], pics[1]);
			int num2 = Differ(pics[0], pics[2]);
			int num3 = Differ(pics[2], pics[3]);
			if (_pictures)
			{
				SaveDiff("gp_" + where.Replace(' ', '_') + "_diff_offoff.png", pics[0], pics[1], width, height);
				SaveDiff("gp_" + where.Replace(' ', '_') + "_diff_offon.png", pics[0], pics[2], width, height);
				SaveDiff("gp_" + where.Replace(' ', '_') + "_diff_onon.png", pics[2], pics[3], width, height);
			}
			bool ok = drawn[0] != null && drawn[0] == drawn[1] && drawn[0] == drawn[2] && drawn[0] == drawn[3];
			Report(skipped > 0, where + ": things outside the frame passed over", $"{skipped / 2} a frame");
			Report(ok, where + ": the card draws the same models with the frame-only gathering", drawn[0] + " / " + drawn[2]);
			Log.Info($"GODPERF {where}: {num2} pixels differ off/on, {num} off/off, {num3} on/on, of {width}x{height} (snow, fire)");
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
				Log.Error("GODPERF picture " + name + ": " + ex.Message);
			}
		}

		private IEnumerator Abba(string name, Toggle set, Action setup)
		{
			List<float[]> off = new List<float[]>();
			List<float[]> on = new List<float[]>();
			bool[] array = new bool[4] { false, true, true, false };
			foreach (bool v in array)
			{
				float[] r = new float[4];
				yield return Measure(string.Format("zoom {0:F0}, {1} {2}", _zoom, name, v ? "on" : "off"), r, delegate
				{
					setup();
					set(v);
				});
				(v ? on : off).Add(r);
			}
			set(on: false);
			float num = Avg(off, 1);
			float num2 = Avg(on, 1);
			_summary.Add($"zoom {_zoom:F0}, {name}: game-thread CPU, scene {num:F1} -> {num2:F1} ms ({(num2 - num) / Mathf.Max(0.01f, num) * 100f:+0;-0}%), " + $"card {Avg(off, 2):F1} -> {Avg(on, 2):F1} ms; frame p50 {Avg(off, 0):F1} -> {Avg(on, 0):F1} ms; {Avg(on, 3):F0} passed over a frame");
			float Avg(List<float[]> l, int num3)
			{
				return (l[0][num3] + l[1][num3]) * 0.5f;
			}
		}

		private IEnumerator Settle(float seconds)
		{
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				Hold();
				yield return null;
			}
		}

		private IEnumerator Measure(string label, float[] result, Action setup)
		{
			setup?.Invoke();
			for (int i = 0; i < 12; i++)
			{
				Hold();
				yield return null;
			}
			SceneClock.ResetWindow();
			_mod.GodPerfResetForTest();
			List<float> frames = new List<float>();
			for (float t = 0f; t < 6f; t += Time.unscaledDeltaTime)
			{
				Hold();
				yield return null;
				frames.Add(Time.unscaledDeltaTime * 1000f);
			}
			frames.Sort();
			float num = frames[frames.Count / 2];
			float num2 = frames[Mathf.Min(frames.Count - 1, (int)((float)frames.Count * 0.95f))];
			double cyclesPerMs = Hitch.CyclesPerMs;
			float num3 = ((cyclesPerMs > 0.0) ? ((float)((double)WorldBoxMod.GodSceneCycles / cyclesPerMs / (double)frames.Count)) : 0f);
			float num4 = ((cyclesPerMs > 0.0) ? ((float)((double)WorldBoxMod.GodCardCycles / cyclesPerMs / (double)frames.Count)) : 0f);
			float num5 = (float)SceneCollector.GodFrameSkipped / (float)frames.Count;
			Log.Info($"GODPERF {label}: game-thread CPU scene {num3:F1} ms, card {num4:F1} ms; {frames.Count} frames, p50 {num:F1} ms, p95 {num2:F1} ms; {_mod.GodPerfReadForTest()}");
			Log.Info("GODPERF " + label + " split: " + CityPerfTest.SplitText(SceneClock.Snapshot()));
			if (result != null)
			{
				result[0] = num;
				result[1] = num3;
				result[2] = num4;
				result[3] = num5;
			}
		}

		private void Hold()
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
			}
			catch (Exception ex)
			{
				Log.Error("GODPERF camera: " + ex.Message);
			}
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
			Log.Info("GODPERF " + (ok ? "PASS " : "FAIL ") + what + ((detail != null) ? (" (" + detail + ")") : ""));
			return ok;
		}

		private void Finish()
		{
			_timing = false;
			try
			{
				SceneClock.SampleEveryFrame = false;
				WorldBoxMod.GodTimeForTest = -1f;
				WorldBoxMod.CrowdLineForTest = -1f;
				PoseClock.Thaw();
				Config.paused = false;
				SceneCollector.GodFrameUnits = _savedFrameUnits;
				SceneCollector.GodFrameBuildings = _savedFrameBuildings;
				People.FarPoses = _savedFarPoses;
				if (_savedCrowd >= 0 && _mod != null)
				{
					_mod.Settings.CrowdDetail = _savedCrowd;
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
				Log.Error("GODPERF putting things back: " + ex.Message);
			}
			foreach (string item in _summary)
			{
				Log.Info("GODPERF summary " + item);
			}
			Log.Info($"GODPERF {_pass} passed, {_fail} failed");
			Log.Info("GODPERF DONE");
			if (_quit)
			{
				Application.Quit();
			}
		}
	}
}
