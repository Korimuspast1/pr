using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.UI;

namespace FirstPerson
{
	internal static class Hitch
	{
		private struct MemoryCounters
		{
			public uint cb;

			public uint PageFaultCount;

			public UIntPtr PeakWorkingSetSize;

			public UIntPtr WorkingSetSize;

			public UIntPtr QuotaPeakPagedPoolUsage;

			public UIntPtr QuotaPagedPoolUsage;

			public UIntPtr QuotaPeakNonPagedPoolUsage;

			public UIntPtr QuotaNonPagedPoolUsage;

			public UIntPtr PagefileUsage;

			public UIntPtr PeakPagefileUsage;
		}

		[StructLayout(LayoutKind.Sequential, Size = 1)]
		private struct MarkerTag
		{
		}

		private const float LogMs = 120f;

		private const int MostPerMinute = 20;

		private const int MaxLaps = 64;

		private static readonly string[] _lapName = new string[64];

		private static readonly long[] _lapTicks = new long[64];

		private static readonly long[] _lapCycles = new long[64];

		private static int _laps;

		private static long _lapMark;

		private static long _lapCycleMark;

		private static bool _lapping;

		private static long _start;

		private static long _lateStart;

		private static long _lateEnd;

		private static long _modUpdate;

		private static long _guiTicks;

		private static long _cStart;

		private static long _cLate;

		private static long _cLateEnd;

		private static int _gcStart;

		private static int _gcLate;

		private static int _gcLateEnd;

		private static int _lines;

		private static float _minuteFrom;

		private static readonly double MsPerTick = 1000.0 / (double)Stopwatch.Frequency;

		private static long _heapAt;

		private static double _allocBytes;

		private static int _allocFrames;

		private static readonly Dictionary<string, int> _gIndex = new Dictionary<string, int>();

		private static readonly string[] _gName = new string[128];

		private static readonly double[] _gBytes = new double[128];

		private static int _gCount;

		private static long _lapHeap;

		private static long _updHeap;

		private static long _lateEndHeap;

		private static long _modUpdBytes;

		private static int _lapGc;

		private static int _updGc;

		private static int _lateEndGc;

		private static double _gGame;

		private static double _gAfter;

		private static int _gFrames;

		private static bool _modUpdGcFree;

		private static readonly uint CountersSize = (uint)Marshal.SizeOf(typeof(MemoryCounters));

		private static bool _procOff = Application.platform != RuntimePlatform.WindowsPlayer;

		private static long _procCpu;

		private static uint _faults;

		private static bool _cyclesOff = Application.platform != RuntimePlatform.WindowsPlayer;

		private static double _cyclesPerMs;

		internal const double SpikeMs = 33.333333333333336;

		private const int WorstKept = 3;

		internal static int Spikes;

		private static double _spTotal;

		private static double _spGame;

		private static double _spModUpd;

		private static double _spLate;

		private static double _spAfter;

		private static double _spHud;

		private static double _spGameCpu;

		private static double _spLateCpu;

		private static double _spAfterCpu;

		private static int _spGc;

		private static int _spWaiting;

		private static readonly Dictionary<string, double[]> _spLaps = new Dictionary<string, double[]>();

		private static readonly Dictionary<string, double[]> _spSteps = new Dictionary<string, double[]>();

		private static readonly Dictionary<string, double[]> _spBench = new Dictionary<string, double[]>();

		private static readonly double[] _spWorstMs = new double[3];

		private static readonly string[] _spWorstWhat = new string[3];

		private static int _allFrames;

		private static double _allGame;

		private static double _allModUpd;

		private static double _allLate;

		private static double _allAfter;

		private static readonly double[] _sceneMs = new double[26];

		private static readonly long[] _sceneCounts = new long[13];

		private static readonly double[] _spScene = new double[26];

		private static readonly double[] _allScene = new double[26];

		private static readonly double[] _spN = new double[13];

		private static readonly double[] _allN = new double[13];

		private static int _spSampled;

		private static int _allSampled;

		private static long _offscreenAt;

		private static long _glidedAt;

		private static double _spOffscreen;

		private static double _spGlided;

		private static double _allOffscreen;

		private static double _allGlided;

		private static bool _gameBench;

		private static Dictionary<string, double> _benchNow;

		internal static double SumUpdates;

		internal static double SumLate;

		internal static double SumAfter;

		internal static double SumUpdatesCpu;

		internal static double SumLateCpu;

		internal static double SumAfterCpu;

		internal static double SumModUpdate;

		internal static int SumFrames;

		internal static bool LapSumsOn;

		private static readonly Dictionary<string, double[]> _lapSums = new Dictionary<string, double[]>();

		private static bool _cvHooked;

		private static long _cvStart;

		private static long _cvRebuild;

		private static int _cvLayouts;

		private static int _cvGraphics;

		private static string _cvWho;

		private static float _cvLogged = -999f;

		private static object _cvRegistry;

		private static PropertyInfo _cvCount;

		private static FieldInfo _cvLayoutQ;

		private static FieldInfo _cvGraphicQ;

		private static string[] _stepName = new string[0];

		private static long[] _stepTicks = new long[0];

		private static long[] _stepCycles = new long[0];

		private static int _step = -1;

		private static long _stepMark;

		private static long _stepCycleMark;

		private static bool _loopIn;

		internal static int Logged { get; private set; }

		internal static long CyclesNow => Cycles();

		internal static double CyclesPerMs
		{
			get
			{
				if (_cyclesPerMs <= 0.0 && !_cyclesOff)
				{
					Calibrate();
				}
				return _cyclesPerMs;
			}
		}

		internal static bool GameBench
		{
			get
			{
				return _gameBench;
			}
			set
			{
				_gameBench = value;
				try
				{
					Bench.bench_enabled = value;
				}
				catch
				{
					_gameBench = false;
				}
				if (!value)
				{
					_benchNow = null;
				}
			}
		}

		private static void GarbageTo(string name, long bytes)
		{
			if (bytes <= 0)
			{
				return;
			}
			if (!_gIndex.TryGetValue(name, out var value))
			{
				if (_gCount >= _gName.Length)
				{
					return;
				}
				value = _gCount++;
				_gIndex[name] = value;
				_gName[value] = name;
			}
			_gBytes[value] += bytes;
		}

		public static string TakeGarbageSplit(float seconds, int most = 10)
		{
			if (seconds <= 0f)
			{
				return null;
			}
			double num = 9.5367431640625E-07 / (double)seconds;
			StringBuilder stringBuilder = new StringBuilder(256);
			stringBuilder.Append("game ").Append((_gGame * num).ToString("F1")).Append(", after ")
				.Append((_gAfter * num).ToString("F1"));
			bool[] array = new bool[_gCount];
			for (int i = 0; i < most; i++)
			{
				int num2 = -1;
				for (int j = 0; j < _gCount; j++)
				{
					if (!array[j] && _gBytes[j] * num >= 0.05 && (num2 < 0 || _gBytes[j] > _gBytes[num2]))
					{
						num2 = j;
					}
				}
				if (num2 < 0)
				{
					break;
				}
				array[num2] = true;
				stringBuilder.Append(", ").Append(_gName[num2]).Append(' ')
					.Append((_gBytes[num2] * num).ToString("F1"));
			}
			for (int k = 0; k < _gCount; k++)
			{
				_gBytes[k] = 0.0;
			}
			stringBuilder.Append(" (").Append(_gFrames).Append(" frames in ")
				.Append(seconds.ToString("F0"))
				.Append(" s)");
			_gGame = (_gAfter = 0.0);
			_gFrames = 0;
			return stringBuilder.ToString();
		}

		public static void RaisePoolFloor()
		{
			try
			{
				ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
				int num = Math.Max(workerThreads, Math.Min(64, Environment.ProcessorCount * 2));
				if (num > workerThreads && ThreadPool.SetMinThreads(num, completionPortThreads))
				{
					Log.Info($"thread pool: at least {num} threads at once (was {workerThreads}), for the game's jobs each frame");
				}
			}
			catch (Exception ex)
			{
				Log.Error("thread pool floor: " + ex.Message);
			}
		}

		[DllImport("kernel32.dll")]
		private static extern IntPtr GetCurrentThread();

		[DllImport("kernel32.dll")]
		private static extern bool QueryThreadCycleTime(IntPtr thread, out ulong cycles);

		[DllImport("kernel32.dll")]
		private static extern IntPtr GetCurrentProcess();

		[DllImport("kernel32.dll")]
		private static extern bool GetProcessTimes(IntPtr process, out long created, out long exited, out long kernel, out long user);

		[DllImport("psapi.dll")]
		private static extern bool GetProcessMemoryInfo(IntPtr process, ref MemoryCounters counters, uint size);

		private static void ProcessNow(out long cpu, out uint faults)
		{
			cpu = 0L;
			faults = 0u;
			if (_procOff)
			{
				return;
			}
			try
			{
				IntPtr currentProcess = GetCurrentProcess();
				if (GetProcessTimes(currentProcess, out var _, out var _, out var kernel, out var user))
				{
					cpu = kernel + user;
				}
				MemoryCounters counters = new MemoryCounters
				{
					cb = CountersSize
				};
				if (GetProcessMemoryInfo(currentProcess, ref counters, counters.cb))
				{
					faults = counters.PageFaultCount;
				}
			}
			catch
			{
				_procOff = true;
			}
		}

		private static long Cycles()
		{
			if (_cyclesOff)
			{
				return 0L;
			}
			try
			{
				ulong cycles;
				return (long)(QueryThreadCycleTime(GetCurrentThread(), out cycles) ? cycles : 0);
			}
			catch
			{
				_cyclesOff = true;
				return 0L;
			}
		}

		private static void Calibrate()
		{
			long num = Cycles();
			if (_cyclesOff || num == 0L)
			{
				_cyclesOff = true;
				return;
			}
			long timestamp = Stopwatch.GetTimestamp();
			long num2 = timestamp + Stopwatch.Frequency / 200;
			while (Stopwatch.GetTimestamp() < num2)
			{
			}
			double num3 = (double)(Stopwatch.GetTimestamp() - timestamp) * MsPerTick;
			_cyclesPerMs = (double)(Cycles() - num) / num3;
			if (_cyclesPerMs <= 0.0)
			{
				_cyclesOff = true;
			}
		}

		private static string Cpu(long cycles)
		{
			return ((double)cycles / _cyclesPerMs).ToString("F0");
		}

		public static float TakeAllocRate(float seconds)
		{
			float result = ((_allocFrames > 0 && seconds > 0f) ? ((float)(_allocBytes / 1048576.0 / (double)seconds)) : 0f);
			_allocBytes = 0.0;
			_allocFrames = 0;
			return result;
		}

		public static void FrameStart()
		{
			if (_start == 0L)
			{
				Calibrate();
			}
			long timestamp = Stopwatch.GetTimestamp();
			long num = Cycles();
			int num2 = GC.CollectionCount(0);
			ProcessNow(out var cpu, out var faults);
			StepsCut(timestamp, num);
			if (!_cvHooked && Time.frameCount > 600)
			{
				HookCanvases();
			}
			if (_start != 0L && _lateEnd > _lateStart && _lateStart > _start)
			{
				AddSums(timestamp, num);
				double num3 = (double)(timestamp - _start) * MsPerTick;
				SpikeTake(num3, timestamp, num, num2);
				if (num3 > 120.0)
				{
					Report(num3, timestamp, num, num2, cpu, faults);
				}
			}
			_procCpu = cpu;
			_faults = faults;
			StepsReset();
			long totalMemory = GC.GetTotalMemory(forceFullCollection: false);
			if (num2 == _gcStart && _heapAt > 0 && totalMemory >= _heapAt)
			{
				_allocBytes += totalMemory - _heapAt;
				_allocFrames++;
			}
			_gFrames++;
			if (_lateEndGc == num2 && _lateEndHeap > 0 && totalMemory >= _lateEndHeap)
			{
				_gAfter += totalMemory - _lateEndHeap;
			}
			_lateEndHeap = 0L;
			_heapAt = totalMemory;
			_updHeap = totalMemory;
			_updGc = num2;
			_modUpdBytes = 0L;
			_modUpdGcFree = true;
			_start = timestamp;
			_cStart = num;
			_gcStart = num2;
			_lateStart = (_lateEnd = 0L);
			_modUpdate = (_guiTicks = 0L);
			_laps = 0;
			_lapping = false;
		}

		public static void UpdateStart()
		{
			_lapMark = Stopwatch.GetTimestamp();
			_lapCycleMark = Cycles();
			_lapping = true;
			_lapHeap = GC.GetTotalMemory(forceFullCollection: false);
			_lapGc = GC.CollectionCount(0);
		}

		public static void UpdateEnd(string name, long ticks)
		{
			long lapHeap = _lapHeap;
			int lapGc = _lapGc;
			Lap(name);
			if (_lapGc == lapGc)
			{
				_modUpdBytes += Math.Max(0L, _lapHeap - lapHeap);
			}
			else
			{
				_modUpdGcFree = false;
			}
			_lapping = false;
			_modUpdate += ticks;
		}

		public static void LateStart()
		{
			BenchSnapshot();
			_lateStart = (_lapMark = Stopwatch.GetTimestamp());
			_cLate = (_lapCycleMark = Cycles());
			_lapping = true;
			_gcLate = GC.CollectionCount(0);
			_lapHeap = GC.GetTotalMemory(forceFullCollection: false);
			_lapGc = _gcLate;
			if (_updHeap > 0 && _updGc == _gcLate && _modUpdGcFree)
			{
				_gGame += Math.Max(0L, _lapHeap - _updHeap - _modUpdBytes);
			}
			_updHeap = 0L;
		}

		public static void Lap(string name)
		{
			if (_lapping)
			{
				long timestamp = Stopwatch.GetTimestamp();
				long num = Cycles();
				if (_laps < 64)
				{
					_lapName[_laps] = name;
					_lapTicks[_laps] = timestamp - _lapMark;
					_lapCycles[_laps] = num - _lapCycleMark;
					_laps++;
				}
				if (LapSumsOn)
				{
					SumLap(name, timestamp - _lapMark, num - _lapCycleMark);
				}
				_lapMark = timestamp;
				_lapCycleMark = num;
				long totalMemory = GC.GetTotalMemory(forceFullCollection: false);
				int num2 = GC.CollectionCount(0);
				if (num2 == _lapGc)
				{
					GarbageTo(name, totalMemory - _lapHeap);
				}
				_lapHeap = totalMemory;
				_lapGc = num2;
			}
		}

		public static void LateEnd()
		{
			if (_lateStart != 0L)
			{
				Lap("rest");
				_lapping = false;
				_lateEnd = Stopwatch.GetTimestamp();
				_cLateEnd = Cycles();
				_gcLateEnd = GC.CollectionCount(0);
				_lateEndHeap = GC.GetTotalMemory(forceFullCollection: false);
				_lateEndGc = _gcLateEnd;
			}
		}

		public static void Gui(long ticks)
		{
			_guiTicks += ticks;
		}

		private static void Report(double total, long now, long cycles, int gcNow, long procCpu, uint faults)
		{
			float unscaledTime = Time.unscaledTime;
			if (unscaledTime - _minuteFrom > 60f)
			{
				_minuteFrom = unscaledTime;
				_lines = 0;
			}
			if (_lines >= 20)
			{
				return;
			}
			_lines++;
			Logged++;
			bool flag = !_cyclesOff;
			StringBuilder stringBuilder = new StringBuilder(320);
			stringBuilder.Append("hitch: ").Append(total.ToString("F0")).Append(" ms");
			if (flag)
			{
				stringBuilder.Append(" (cpu ").Append(Cpu(cycles - _cStart)).Append(')');
			}
			stringBuilder.Append(" = updates ").Append(((double)(_lateStart - _start) * MsPerTick).ToString("F0"));
			if (flag)
			{
				stringBuilder.Append(" (cpu ").Append(Cpu(_cLate - _cStart)).Append(')');
			}
			stringBuilder.Append(" of which the mod's ").Append(((double)_modUpdate * MsPerTick).ToString("F0"));
			stringBuilder.Append(", late ").Append(((double)(_lateEnd - _lateStart) * MsPerTick).ToString("F0"));
			if (flag)
			{
				stringBuilder.Append(" (cpu ").Append(Cpu(_cLateEnd - _cLate)).Append(')');
			}
			stringBuilder.Append(", after ").Append(((double)(now - _lateEnd) * MsPerTick).ToString("F0"));
			if (flag)
			{
				stringBuilder.Append(" (cpu ").Append(Cpu(cycles - _cLateEnd)).Append(')');
			}
			stringBuilder.Append(" of which the hud ").Append(((double)_guiTicks * MsPerTick).ToString("F0"));
			int num = 0;
			bool[] array = new bool[_laps];
			for (int i = 0; i < 6; i++)
			{
				int num2 = -1;
				for (int j = 0; j < _laps; j++)
				{
					if (!array[j] && (double)_lapTicks[j] * MsPerTick >= 1.0 && (num2 < 0 || _lapTicks[j] > _lapTicks[num2]))
					{
						num2 = j;
					}
				}
				if (num2 < 0)
				{
					break;
				}
				array[num2] = true;
				stringBuilder.Append((num++ == 0) ? "; laps " : ", ").Append(_lapName[num2]).Append(' ')
					.Append(((double)_lapTicks[num2] * MsPerTick).ToString("F0"));
				if (flag)
				{
					stringBuilder.Append('/').Append(Cpu(_lapCycles[num2]));
				}
			}
			AppendSteps(stringBuilder, flag);
			int num3 = _gcLate - _gcStart;
			int num4 = _gcLateEnd - _gcLate;
			int num5 = gcNow - _gcLateEnd;
			if (num3 + num4 + num5 > 0)
			{
				stringBuilder.Append("; gc in ").Append((num3 > 0) ? "updates " : "").Append((num4 > 0) ? "late " : "")
					.Append((num5 > 0) ? "after " : "")
					.Append('x')
					.Append(num3 + num4 + num5);
			}
			if (!_procOff && procCpu > 0 && _procCpu > 0)
			{
				stringBuilder.Append("; process cpu ").Append(((double)(procCpu - _procCpu) / 10000.0).ToString("F0")).Append(" ms on all threads, page faults ")
					.Append(faults - _faults);
			}
			stringBuilder.Append("; heap ").Append(((float)GC.GetTotalMemory(forceFullCollection: false) / 1048576f).ToString("F0")).Append(" MB");
			try
			{
				if (Config.game_loaded && World.world != null)
				{
					stringBuilder.Append(", units ").Append(World.world.units.Count);
				}
			}
			catch
			{
			}
			Log.Info(stringBuilder.ToString());
			for (int k = 0; k < _stepTicks.Length; k++)
			{
				if (_stepName[k].EndsWith(".PlayerUpdateCanvases", StringComparison.Ordinal) && (double)_stepTicks[k] * MsPerTick > 100.0)
				{
					CanvasReport((double)_stepTicks[k] * MsPerTick);
				}
			}
		}

		private static void SpikeAdd(Dictionary<string, double[]> to, string name, double ms, double cpu)
		{
			if (!(ms <= 0.0))
			{
				if (!to.TryGetValue(name, out var value))
				{
					value = (to[name] = new double[2]);
				}
				value[0] += ms;
				value[1] += cpu;
			}
		}

		private static void SpikeTake(double total, long now, long cycles, int gcNow)
		{
			double num = (double)(_lateStart - _start) * MsPerTick;
			double num2 = (double)_modUpdate * MsPerTick;
			double num3 = (double)(_lateEnd - _lateStart) * MsPerTick;
			double num4 = (double)(now - _lateEnd) * MsPerTick;
			_allFrames++;
			_allGame += num - num2;
			_allModUpd += num2;
			_allLate += num3;
			_allAfter += num4;
			SceneClock.FrameDelta(_sceneMs, _sceneCounts, out var sampled);
			long num5 = SceneCollector.OffscreenSkipped - _offscreenAt;
			long num6 = People.Glided - _glidedAt;
			_offscreenAt = SceneCollector.OffscreenSkipped;
			_glidedAt = People.Glided;
			if (sampled)
			{
				_allSampled++;
				_allOffscreen += num5;
				_allGlided += num6;
				for (int i = 0; i < _sceneMs.Length; i++)
				{
					_allScene[i] += _sceneMs[i];
				}
				for (int j = 0; j < _sceneCounts.Length; j++)
				{
					_allN[j] += _sceneCounts[j];
				}
			}
			if (total <= 33.333333333333336)
			{
				return;
			}
			if (sampled)
			{
				_spSampled++;
				_spOffscreen += num5;
				_spGlided += num6;
				for (int k = 0; k < _sceneMs.Length; k++)
				{
					_spScene[k] += _sceneMs[k];
				}
				for (int l = 0; l < _sceneCounts.Length; l++)
				{
					_spN[l] += _sceneCounts[l];
				}
			}
			bool flag = !_cyclesOff && _cyclesPerMs > 0.0;
			Spikes++;
			_spTotal += total;
			_spGame += num - num2;
			_spModUpd += num2;
			_spLate += num3;
			_spAfter += num4;
			_spHud += (double)_guiTicks * MsPerTick;
			double num7 = 0.0;
			double num8 = 0.0;
			double num9 = 0.0;
			if (flag)
			{
				num7 = (double)(_cLate - _cStart) / _cyclesPerMs;
				num8 = (double)(_cLateEnd - _cLate) / _cyclesPerMs;
				num9 = (double)(cycles - _cLateEnd) / _cyclesPerMs;
				_spGameCpu += num7;
				_spLateCpu += num8;
				_spAfterCpu += num9;
				if ((double)(cycles - _cStart) / _cyclesPerMs < total * 0.5)
				{
					_spWaiting++;
				}
			}
			if (gcNow > _gcStart)
			{
				_spGc++;
			}
			for (int m = 0; m < _laps; m++)
			{
				SpikeAdd(_spLaps, _lapName[m], (double)_lapTicks[m] * MsPerTick, flag ? ((double)_lapCycles[m] / _cyclesPerMs) : 0.0);
			}
			for (int n = 0; n < _stepTicks.Length; n++)
			{
				string text = _stepName[n];
				if (!text.EndsWith(".ScriptRunBehaviourUpdate", StringComparison.Ordinal) && !text.EndsWith(".ScriptRunBehaviourLateUpdate", StringComparison.Ordinal))
				{
					SpikeAdd(_spSteps, text, (double)_stepTicks[n] * MsPerTick, flag ? ((double)_stepCycles[n] / _cyclesPerMs) : 0.0);
				}
			}
			double num10 = 0.0;
			if (_benchNow != null && _benchNow.Count > 0)
			{
				foreach (KeyValuePair<string, double> item in _benchNow)
				{
					SpikeAdd(_spBench, item.Key, item.Value, 0.0);
					num10 += item.Value;
				}
				SpikeAdd(_spBench, "creatures, buildings (untimed)", num - num2 - num10, 0.0);
			}
			int num11 = -1;
			for (int num12 = 0; num12 < 3; num12++)
			{
				if (_spWorstWhat[num12] == null)
				{
					num11 = num12;
					break;
				}
				if (_spWorstMs[num12] < total && (num11 < 0 || _spWorstMs[num12] < _spWorstMs[num11]))
				{
					num11 = num12;
				}
			}
			if (num11 < 0)
			{
				return;
			}
			StringBuilder stringBuilder = new StringBuilder(160);
			stringBuilder.Append(total.ToString("F0")).Append(" ms (frame ").Append(Time.frameCount)
				.Append(", game ")
				.Append((num - num2).ToString("F0"));
			if (flag)
			{
				stringBuilder.Append('/').Append(num7.ToString("F0"));
			}
			stringBuilder.Append(", mod ").Append(num2.ToString("F0")).Append(", late ")
				.Append(num3.ToString("F0"));
			if (flag)
			{
				stringBuilder.Append('/').Append(num8.ToString("F0"));
			}
			stringBuilder.Append(", after ").Append(num4.ToString("F0"));
			if (flag)
			{
				stringBuilder.Append('/').Append(num9.ToString("F0"));
			}
			if (gcNow > _gcStart)
			{
				stringBuilder.Append(", gc");
			}
			Top(stringBuilder, "; ", 2, _laps, (int num14) => _lapName[num14], (int num14) => (double)_lapTicks[num14] * MsPerTick, 1.0);
			Top(stringBuilder, "; unity ", 2, _stepTicks.Length, (int num14) => _stepName[num14], (int num14) => (!_stepName[num14].EndsWith("Update", StringComparison.Ordinal) || !_stepName[num14].Contains("ScriptRunBehaviour")) ? ((double)_stepTicks[num14] * MsPerTick) : 0.0, 2.0);
			if (_benchNow != null && _benchNow.Count > 0)
			{
				List<KeyValuePair<string, double>> list = new List<KeyValuePair<string, double>>(_benchNow);
				list.Sort((KeyValuePair<string, double> x, KeyValuePair<string, double> y) => y.Value.CompareTo(x.Value));
				stringBuilder.Append("; step: rest ").Append((num - num2 - num10).ToString("F0"));
				for (int num13 = 0; num13 < list.Count && num13 < 3 && list[num13].Value >= 1.0; num13++)
				{
					stringBuilder.Append(", ").Append(list[num13].Key).Append(' ')
						.Append(list[num13].Value.ToString("F0"));
				}
			}
			stringBuilder.Append(')');
			_spWorstMs[num11] = total;
			_spWorstWhat[num11] = stringBuilder.ToString();
		}

		private static void Top(StringBuilder b, string lead, int most, int count, Func<int, string> name, Func<int, double> ms, double least)
		{
			bool[] array = new bool[count];
			int num = 0;
			for (int i = 0; i < most; i++)
			{
				int num2 = -1;
				for (int j = 0; j < count; j++)
				{
					if (!array[j] && ms(j) >= least && (num2 < 0 || ms(j) > ms(num2)))
					{
						num2 = j;
					}
				}
				if (num2 >= 0)
				{
					array[num2] = true;
					b.Append((num++ == 0) ? lead : ", ").Append(name(num2)).Append(' ')
						.Append(ms(num2).ToString("F0"));
					continue;
				}
				break;
			}
		}

		private static void AppendAverages(StringBuilder b, string lead, Dictionary<string, double[]> sums, int over, int most, double least, bool cpu)
		{
			List<KeyValuePair<string, double[]>> list = new List<KeyValuePair<string, double[]>>(sums);
			list.Sort((KeyValuePair<string, double[]> x, KeyValuePair<string, double[]> y) => y.Value[0].CompareTo(x.Value[0]));
			int num = 0;
			for (int num2 = 0; num2 < list.Count && num2 < most; num2++)
			{
				double num3 = list[num2].Value[0] / (double)over;
				if (!(num3 < least))
				{
					b.Append((num++ == 0) ? lead : ", ").Append(list[num2].Key).Append(' ')
						.Append(num3.ToString("F1"));
					if (cpu && list[num2].Value[1] > 0.0)
					{
						b.Append('/').Append((list[num2].Value[1] / (double)over).ToString("F1"));
					}
					continue;
				}
				break;
			}
		}

		public static string TakeSpikes(float seconds)
		{
			int spikes = Spikes;
			string result = null;
			if (spikes > 0)
			{
				bool flag = !_cyclesOff && _cyclesPerMs > 0.0;
				int num = Math.Max(1, _allFrames);
				StringBuilder stringBuilder = new StringBuilder(640);
				stringBuilder.Append(spikes).Append(" frames over ").Append(33.333333333333336.ToString("F0"))
					.Append(" ms in ")
					.Append(seconds.ToString("F0"))
					.Append(" s");
				if (_spWaiting > 0)
				{
					stringBuilder.Append(", ").Append(_spWaiting).Append(" mostly waiting");
				}
				if (_spGc > 0)
				{
					stringBuilder.Append(", a collection in ").Append(_spGc);
				}
				stringBuilder.Append("; a spike ").Append((_spTotal / (double)spikes).ToString("F0")).Append(" ms = game ")
					.Append((_spGame / (double)spikes).ToString("F1"));
				if (flag)
				{
					stringBuilder.Append(" (cpu ").Append((_spGameCpu / (double)spikes).ToString("F0")).Append(')');
				}
				stringBuilder.Append(" + mod update ").Append((_spModUpd / (double)spikes).ToString("F1")).Append(" + late ")
					.Append((_spLate / (double)spikes).ToString("F1"));
				if (flag)
				{
					stringBuilder.Append(" (cpu ").Append((_spLateCpu / (double)spikes).ToString("F0")).Append(')');
				}
				stringBuilder.Append(" + after ").Append((_spAfter / (double)spikes).ToString("F1"));
				if (flag)
				{
					stringBuilder.Append(" (cpu ").Append((_spAfterCpu / (double)spikes).ToString("F0")).Append(", hud ")
						.Append((_spHud / (double)spikes).ToString("F0"))
						.Append(')');
				}
				stringBuilder.Append("; a frame ").Append(((_allGame + _allModUpd + _allLate + _allAfter) / (double)num).ToString("F0")).Append(" ms = game ")
					.Append((_allGame / (double)num).ToString("F1"))
					.Append(" + mod update ")
					.Append((_allModUpd / (double)num).ToString("F1"))
					.Append(" + late ")
					.Append((_allLate / (double)num).ToString("F1"))
					.Append(" + after ")
					.Append((_allAfter / (double)num).ToString("F1"));
				AppendAverages(stringBuilder, "; laps in a spike ", _spLaps, spikes, 8, 0.5, flag);
				AppendAverages(stringBuilder, "; unity ", _spSteps, spikes, 4, 1.0, flag);
				AppendAverages(stringBuilder, "; game step ", _spBench, spikes, 8, 0.5, cpu: false);
				if (_spSampled > 0 && _allSampled > 0)
				{
					stringBuilder.Append("; scene in a spike / a frame (").Append(_spSampled).Append('/')
						.Append(_allSampled)
						.Append(" timed):");
					int num2 = 0;
					for (int i = 0; i < 26; i++)
					{
						double num3 = _spScene[i] / (double)_spSampled;
						double num4 = _allScene[i] / (double)_allSampled;
						if (!(num3 < 0.5) || !(num4 < 0.5))
						{
							StringBuilder stringBuilder2 = stringBuilder.Append((num2++ == 0) ? " " : ", ");
							SceneClock.B b = (SceneClock.B)i;
							stringBuilder2.Append(b.ToString().ToLowerInvariant()).Append(' ').Append(num3.ToString("F1"))
								.Append('/')
								.Append(num4.ToString("F1"));
						}
					}
					stringBuilder.Append("; counts:");
					num2 = 0;
					for (int j = 2; j < 13; j++)
					{
						if (j != 6)
						{
							double num5 = _spN[j] / (double)_spSampled;
							double num6 = _allN[j] / (double)_allSampled;
							if (!(num5 < 1.0) || !(num6 < 1.0))
							{
								StringBuilder stringBuilder3 = stringBuilder.Append((num2++ == 0) ? " " : ", ");
								SceneClock.N n = (SceneClock.N)j;
								stringBuilder3.Append(n.ToString().ToLowerInvariant()).Append(' ').Append(num5.ToString("F0"))
									.Append('/')
									.Append(num6.ToString("F0"));
							}
						}
					}
					stringBuilder.Append(", off-screen skipped ").Append((_spOffscreen / (double)_spSampled).ToString("F0")).Append('/')
						.Append((_allOffscreen / (double)_allSampled).ToString("F0"));
					stringBuilder.Append(", glided ").Append((_spGlided / (double)_spSampled).ToString("F0")).Append('/')
						.Append((_allGlided / (double)_allSampled).ToString("F0"));
				}
				stringBuilder.Append("; worst");
				List<int> list = new List<int>();
				for (int k = 0; k < 3; k++)
				{
					if (_spWorstWhat[k] != null)
					{
						list.Add(k);
					}
				}
				list.Sort((int x, int y) => _spWorstMs[y].CompareTo(_spWorstMs[x]));
				foreach (int item in list)
				{
					stringBuilder.Append(' ').Append(_spWorstWhat[item]);
				}
				result = stringBuilder.ToString();
			}
			Spikes = 0;
			_spTotal = (_spGame = (_spModUpd = (_spLate = (_spAfter = (_spHud = (_spGameCpu = (_spLateCpu = (_spAfterCpu = 0.0))))))));
			_spGc = (_spWaiting = 0);
			_spLaps.Clear();
			_spSteps.Clear();
			_spBench.Clear();
			for (int num7 = 0; num7 < 3; num7++)
			{
				_spWorstMs[num7] = 0.0;
				_spWorstWhat[num7] = null;
			}
			_allFrames = 0;
			_allGame = (_allModUpd = (_allLate = (_allAfter = 0.0)));
			Array.Clear(_spScene, 0, _spScene.Length);
			Array.Clear(_allScene, 0, _allScene.Length);
			Array.Clear(_spN, 0, _spN.Length);
			Array.Clear(_allN, 0, _allN.Length);
			_spSampled = (_allSampled = 0);
			_spOffscreen = (_spGlided = (_allOffscreen = (_allGlided = 0.0)));
			return result;
		}

		private static void BenchSnapshot()
		{
			if (!_gameBench)
			{
				return;
			}
			try
			{
				if (!Bench.dict.TryGetValue("game_total", out var value))
				{
					return;
				}
				if (_benchNow == null)
				{
					_benchNow = new Dictionary<string, double>();
				}
				_benchNow.Clear();
				foreach (KeyValuePair<string, ToolBenchmarkData> dict_datum in value.dict_data)
				{
					ToolBenchmarkData value2 = dict_datum.Value;
					if (!(value2.latest_result <= 0.0))
					{
						_benchNow[dict_datum.Key] = value2.latest_result * 1000.0;
						value2.latest_result = 0.0;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("hitch: the game's benchmark can't be read: " + ex.Message);
				GameBench = false;
			}
		}

		internal static void ResetSums()
		{
			SumUpdates = (SumLate = (SumAfter = (SumUpdatesCpu = (SumLateCpu = (SumAfterCpu = (SumModUpdate = 0.0))))));
			SumFrames = 0;
			foreach (KeyValuePair<string, double[]> lapSum in _lapSums)
			{
				lapSum.Value[0] = 0.0;
				lapSum.Value[1] = 0.0;
			}
		}

		private static void SumLap(string name, long ticks, long cycles)
		{
			if (!_lapSums.TryGetValue(name, out var value))
			{
				value = (_lapSums[name] = new double[2]);
			}
			value[0] += (double)ticks * MsPerTick;
			if (!_cyclesOff && _cyclesPerMs > 0.0)
			{
				value[1] += (double)cycles / _cyclesPerMs;
			}
		}

		internal static string TopLaps(int most)
		{
			int num = Math.Max(1, SumFrames);
			List<KeyValuePair<string, double[]>> list = new List<KeyValuePair<string, double[]>>(_lapSums);
			list.Sort((KeyValuePair<string, double[]> a, KeyValuePair<string, double[]> b) => b.Value[0].CompareTo(a.Value[0]));
			StringBuilder stringBuilder = new StringBuilder(512);
			for (int num2 = 0; num2 < list.Count && num2 < most && !(list[num2].Value[0] / (double)num < 0.05); num2++)
			{
				if (num2 > 0)
				{
					stringBuilder.Append(", ");
				}
				stringBuilder.Append(list[num2].Key).Append(' ').Append((list[num2].Value[0] / (double)num).ToString("F1"))
					.Append('/')
					.Append((list[num2].Value[1] / (double)num).ToString("F1"));
			}
			return stringBuilder.ToString();
		}

		private static void AddSums(long now, long cycles)
		{
			SumUpdates += (double)(_lateStart - _start) * MsPerTick;
			SumLate += (double)(_lateEnd - _lateStart) * MsPerTick;
			SumAfter += (double)(now - _lateEnd) * MsPerTick;
			SumModUpdate += (double)_modUpdate * MsPerTick;
			if (!_cyclesOff && _cyclesPerMs > 0.0)
			{
				SumUpdatesCpu += (double)(_cLate - _cStart) / _cyclesPerMs;
				SumLateCpu += (double)(_cLateEnd - _cLate) / _cyclesPerMs;
				SumAfterCpu += (double)(cycles - _cLateEnd) / _cyclesPerMs;
			}
			SumFrames++;
		}

		private static void HookCanvases()
		{
			if (_cvHooked)
			{
				return;
			}
			_cvHooked = true;
			try
			{
				_cvRegistry = CanvasUpdateRegistry.instance;
				Type typeFromHandle = typeof(CanvasUpdateRegistry);
				_cvLayoutQ = typeFromHandle.GetField("m_LayoutRebuildQueue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_cvGraphicQ = typeFromHandle.GetField("m_GraphicRebuildQueue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_cvCount = (_cvLayoutQ?.GetValue(_cvRegistry))?.GetType().GetProperty("Count");
				Canvas.preWillRenderCanvases += CanvasesBegin;
				Canvas.willRenderCanvases += CanvasesRebuilt;
			}
			catch (Exception ex)
			{
				Log.Error("hitch: canvases not timed: " + ex.Message);
			}
		}

		private static int QueueCount(FieldInfo f)
		{
			try
			{
				object obj = f?.GetValue(_cvRegistry);
				return (obj != null && _cvCount != null) ? ((int)_cvCount.GetValue(obj, null)) : (-1);
			}
			catch
			{
				return -1;
			}
		}

		private static void CanvasesBegin()
		{
			_cvStart = Stopwatch.GetTimestamp();
			_cvRebuild = 0L;
			_cvLayouts = QueueCount(_cvLayoutQ);
			_cvGraphics = QueueCount(_cvGraphicQ);
			_cvWho = ((_cvLayouts + _cvGraphics >= 200 && Time.unscaledTime - _cvLogged >= 20f) ? Who() : null);
		}

		private static void CanvasesRebuilt()
		{
			if (_cvStart != 0L)
			{
				_cvRebuild = Stopwatch.GetTimestamp() - _cvStart;
			}
		}

		private static string Who()
		{
			try
			{
				Dictionary<string, int> dictionary = new Dictionary<string, int>();
				FieldInfo[] array = new FieldInfo[2] { _cvLayoutQ, _cvGraphicQ };
				for (int i = 0; i < array.Length; i++)
				{
					object obj = array[i]?.GetValue(_cvRegistry);
					PropertyInfo propertyInfo = obj?.GetType().GetProperty("Item");
					if (propertyInfo == null)
					{
						continue;
					}
					int num = Math.Min(4000, (int)_cvCount.GetValue(obj, null));
					for (int j = 0; j < num; j++)
					{
						object value = propertyInfo.GetValue(obj, new object[1] { j });
						if (value is ICanvasElement canvasElement)
						{
							Transform transform = canvasElement.transform;
							if (!(transform == null))
							{
								Canvas componentInParent = transform.GetComponentInParent<Canvas>();
								string text = ((componentInParent != null) ? componentInParent.rootCanvas.name : "-");
								string text2 = ((transform.parent != null) ? transform.parent.name : "-");
								string key = text + " / " + text2 + " / " + value.GetType().Name;
								dictionary.TryGetValue(key, out var value2);
								dictionary[key] = value2 + 1;
							}
						}
					}
				}
				StringBuilder stringBuilder = new StringBuilder();
				for (int k = 0; k < 6; k++)
				{
					if (dictionary.Count <= 0)
					{
						break;
					}
					string text3 = null;
					int num2 = 0;
					foreach (KeyValuePair<string, int> item in dictionary)
					{
						if (item.Value > num2)
						{
							num2 = item.Value;
							text3 = item.Key;
						}
					}
					dictionary.Remove(text3);
					stringBuilder.Append((k == 0) ? "" : "; ").Append(text3).Append(" x")
						.Append(num2);
				}
				return stringBuilder.ToString();
			}
			catch (Exception ex)
			{
				return "? " + ex.Message;
			}
		}

		private static void CanvasReport(double stepMs)
		{
			float unscaledTime = Time.unscaledTime;
			if (unscaledTime - _cvLogged < 20f)
			{
				return;
			}
			_cvLogged = unscaledTime;
			StringBuilder stringBuilder = new StringBuilder(512);
			stringBuilder.Append("canvases: step ").Append(stepMs.ToString("F0")).Append(" ms, the UI's rebuild ")
				.Append(((double)_cvRebuild * MsPerTick).ToString("F0"))
				.Append(" ms (")
				.Append(_cvLayouts)
				.Append(" layouts, ")
				.Append(_cvGraphics)
				.Append(" graphics waiting)");
			if (_cvWho != null)
			{
				stringBuilder.Append(" of ").Append(_cvWho);
			}
			try
			{
				stringBuilder.Append("; roots:");
				Canvas[] array = UnityEngine.Object.FindObjectsOfType<Canvas>();
				foreach (Canvas canvas in array)
				{
					if (!canvas.isRootCanvas || !canvas.isActiveAndEnabled)
					{
						continue;
					}
					int num = 0;
					int num2 = 0;
					int num3 = 0;
					Graphic[] componentsInChildren = canvas.GetComponentsInChildren<Graphic>(includeInactive: false);
					foreach (Graphic graphic in componentsInChildren)
					{
						if (graphic.enabled)
						{
							num++;
							if (graphic is Text text)
							{
								num2++;
								num3 += ((text.text != null) ? text.text.Length : 0);
							}
						}
					}
					stringBuilder.Append(' ').Append(canvas.name).Append(" [")
						.Append(num)
						.Append(" graphics, ")
						.Append(num2)
						.Append(" texts, ")
						.Append(num3)
						.Append(" chars, ")
						.Append(canvas.renderMode)
						.Append(']');
				}
			}
			catch (Exception ex)
			{
				stringBuilder.Append(" ? ").Append(ex.Message);
			}
			Log.Info(stringBuilder.ToString());
		}

		public static void InstallLoopMarkers()
		{
			if (_loopIn)
			{
				return;
			}
			_loopIn = true;
			try
			{
				PlayerLoopSystem currentPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
				if (currentPlayerLoop.subSystemList == null)
				{
					return;
				}
				List<string> list = new List<string>();
				PlayerLoopSystem[] array = new PlayerLoopSystem[currentPlayerLoop.subSystemList.Length];
				for (int i = 0; i < array.Length; i++)
				{
					PlayerLoopSystem playerLoopSystem = currentPlayerLoop.subSystemList[i];
					string text = ((playerLoopSystem.type != null) ? playerLoopSystem.type.Name : "?");
					PlayerLoopSystem[] subSystemList = playerLoopSystem.subSystemList;
					if (subSystemList == null || subSystemList.Length == 0)
					{
						array[i] = playerLoopSystem;
						continue;
					}
					List<PlayerLoopSystem> list2 = new List<PlayerLoopSystem>(subSystemList.Length * 2 + 1);
					PlayerLoopSystem[] array2 = subSystemList;
					for (int j = 0; j < array2.Length; j++)
					{
						PlayerLoopSystem item = array2[j];
						string name = text + "." + ((item.type != null) ? item.type.Name : "?");
						list2.Add(Marker(list, name));
						list2.Add(item);
					}
					list2.Add(Marker(list, text + " end"));
					playerLoopSystem.subSystemList = list2.ToArray();
					array[i] = playerLoopSystem;
				}
				currentPlayerLoop.subSystemList = array;
				_stepName = list.ToArray();
				_stepTicks = new long[_stepName.Length];
				_stepCycles = new long[_stepName.Length];
				PlayerLoop.SetPlayerLoop(currentPlayerLoop);
				Log.Info($"hitch: {_stepName.Length} markers in Unity's frame");
			}
			catch (Exception ex)
			{
				Log.Error("hitch: Unity's frame steps not timed: " + ex.Message);
			}
		}

		private static PlayerLoopSystem Marker(List<string> names, string name)
		{
			int index = names.Count;
			names.Add(name);
			return new PlayerLoopSystem
			{
				type = typeof(MarkerTag),
				updateDelegate = delegate
				{
					Step(index);
				}
			};
		}

		private static void Step(int index)
		{
			long timestamp = Stopwatch.GetTimestamp();
			long num = Cycles();
			if (_step >= 0 && _step < _stepTicks.Length)
			{
				_stepTicks[_step] += timestamp - _stepMark;
				_stepCycles[_step] += num - _stepCycleMark;
			}
			_step = index;
			_stepMark = timestamp;
			_stepCycleMark = num;
		}

		private static void StepsCut(long now, long cycles)
		{
			if (_step >= 0 && _step < _stepTicks.Length)
			{
				_stepTicks[_step] += now - _stepMark;
				_stepCycles[_step] += cycles - _stepCycleMark;
			}
			_stepMark = now;
			_stepCycleMark = cycles;
		}

		private static void StepsReset()
		{
			Array.Clear(_stepTicks, 0, _stepTicks.Length);
			Array.Clear(_stepCycles, 0, _stepCycles.Length);
		}

		private static void AppendSteps(StringBuilder b, bool cpu)
		{
			int num = 0;
			bool[] array = new bool[_stepTicks.Length];
			for (int i = 0; i < 5; i++)
			{
				int num2 = -1;
				for (int j = 0; j < _stepTicks.Length; j++)
				{
					if (!array[j] && !((double)_stepTicks[j] * MsPerTick < 2.0))
					{
						string text = _stepName[j];
						if (!text.EndsWith(".ScriptRunBehaviourUpdate", StringComparison.Ordinal) && !text.EndsWith(".ScriptRunBehaviourLateUpdate", StringComparison.Ordinal) && (num2 < 0 || _stepTicks[j] > _stepTicks[num2]))
						{
							num2 = j;
						}
					}
				}
				if (num2 >= 0)
				{
					array[num2] = true;
					b.Append((num++ == 0) ? "; unity " : ", ").Append(_stepName[num2]).Append(' ')
						.Append(((double)_stepTicks[num2] * MsPerTick).ToString("F0"));
					if (cpu)
					{
						b.Append('/').Append(Cpu(_stepCycles[num2]));
					}
					continue;
				}
				break;
			}
		}
	}
}
