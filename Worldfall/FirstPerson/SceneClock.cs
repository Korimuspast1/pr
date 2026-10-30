using System;
using System.Diagnostics;

namespace FirstPerson
{
	internal static class SceneClock
	{
		public enum B
		{
			Crowd,
			Ticks,
			Begin,
			Units,
			Buildings,
			FarProps,
			Fx,
			Rest,
			Checks,
			Steer,
			Track,
			Sprites,
			Ground,
			People,
			Creatures,
			Other,
			Far,
			Figure,
			Pose,
			Extras,
			Place,
			Head,
			Gear,
			Route,
			Step,
			Face,
			Count
		}

		public enum N
		{
			Passes,
			Sampled,
			Visited,
			Tracked,
			Steered,
			Fetched,
			Behind,
			Near,
			Mid,
			Distant,
			Creatures,
			Far,
			Walkers,
			Count
		}

		public sealed class Split
		{
			public int Passes;

			public int Sampled;

			public float Seconds;

			public readonly float[] Ms = new float[26];

			public readonly float[] Counts = new float[13];

			public float SteerMs;

			public int Frames;

			public int Over16;

			public int Over33;

			public int Gc0;

			public float P50;

			public float P95;

			public float P99;

			public float Worst;

			public float HeapMb;

			public float this[B b] => Ms[(int)b];

			public float this[N n] => Counts[(int)n];

			public float People => Counts[7] + Counts[8] + Counts[9];

			public float Total
			{
				get
				{
					float num = 0f;
					for (int i = 0; i < 8; i++)
					{
						num += Ms[i];
					}
					return num;
				}
			}
		}

		public static bool SampleEveryFrame;

		public static bool NoSampling;

		private const int FirstSampled = 8;

		private static readonly long[] _ticks = new long[26];

		private static readonly long[] _counts = new long[13];

		private static int _stage = -1;

		private static int _unit = -1;

		private static int _sub = -1;

		private static long _stageMark;

		private static long _unitMark;

		private static long _subMark;

		private static bool _in;

		private static bool _on;

		private static int _pass;

		private const int FrameRing = 8192;

		private static readonly float[] _frameMs = new float[8192];

		private static readonly float[] _sorted = new float[8192];

		private static int _frames;

		private static int _over16;

		private static int _over33;

		private static float _worst;

		private static int _gcBase = -1;

		private static long _windowStart = Stopwatch.GetTimestamp();

		private static readonly string[] StageLaps = new string[8] { "s:crowd", "s:ticks", "s:begin", "s:units", "s:buildings", "s:far props", "s:fx", "s:rest" };

		private static readonly long[] _lastTicks = new long[26];

		private static readonly long[] _lastCounts = new long[13];

		public static void Begin()
		{
			_in = (_on = false);
			_unit = (_sub = -1);
			_pass++;
			_counts[0]++;
			_stageMark = Stopwatch.GetTimestamp();
			_stage = 7;
		}

		public static void Stage(B b)
		{
			long timestamp = Stopwatch.GetTimestamp();
			if (_stage >= 0)
			{
				_ticks[_stage] += timestamp - _stageMark;
			}
			if (_stage >= 0 && _stage < StageLaps.Length)
			{
				Hitch.Lap(StageLaps[_stage]);
			}
			_stageMark = timestamp;
			_stage = (int)b;
			if (b == B.Units)
			{
				_in = true;
				_on = !NoSampling && (SampleEveryFrame || (_pass & 0x3F) == 0);
				if (_on)
				{
					_counts[1]++;
					_unitMark = timestamp;
					_unit = 8;
					_sub = -1;
				}
			}
		}

		public static void UnitsDone(int farActors)
		{
			To(B.Checks);
			_unit = -1;
			if (_in)
			{
				_counts[11] += farActors;
			}
			_in = (_on = false);
			Stage(B.Rest);
		}

		public static void FrameDelta(double[] ms, long[] counts, out bool sampled)
		{
			double num = 1000.0 / (double)Stopwatch.Frequency;
			for (int i = 0; i < 26; i++)
			{
				ms[i] = (double)(_ticks[i] - _lastTicks[i]) * num;
				_lastTicks[i] = _ticks[i];
			}
			for (int j = 0; j < 13; j++)
			{
				counts[j] = _counts[j] - _lastCounts[j];
				_lastCounts[j] = _counts[j];
			}
			sampled = counts[1] > 0;
		}

		public static void End()
		{
			long timestamp = Stopwatch.GetTimestamp();
			if (_stage >= 0)
			{
				_ticks[_stage] += timestamp - _stageMark;
			}
			if (_stage >= 0 && _stage < StageLaps.Length)
			{
				Hitch.Lap(StageLaps[_stage]);
			}
			_stage = -1;
			_in = (_on = false);
			_counts[12] += Steering.Walkers;
		}

		public static void Unit()
		{
			if (_in)
			{
				_counts[2]++;
				if (_unit != 16)
				{
					To(B.Checks);
				}
			}
		}

		public static void To(B b)
		{
			if (_on && (b != B.Far || _unit != 16))
			{
				long timestamp = Stopwatch.GetTimestamp();
				if (_sub >= 0)
				{
					_ticks[_sub] += timestamp - _subMark;
					_sub = -1;
				}
				if (_unit >= 0)
				{
					_ticks[_unit] += timestamp - _unitMark;
				}
				_unitMark = timestamp;
				_unit = (int)b;
			}
		}

		public static void Steer(bool steered)
		{
			if (steered && _in)
			{
				_counts[4]++;
			}
			To(B.Steer);
		}

		public static void Track()
		{
			if (_in)
			{
				_counts[3]++;
			}
			To(B.Track);
		}

		public static void Sprite()
		{
			if (_in)
			{
				_counts[5]++;
			}
			To(B.Sprites);
		}

		public static void Creature()
		{
			if (_in)
			{
				_counts[10]++;
			}
		}

		public static void Person(float distance)
		{
			if (_in)
			{
				_counts[(distance < 30f) ? 7 : ((distance < 60f) ? 8 : 9)]++;
				Sub(B.Figure);
			}
		}

		public static void Sub(B b)
		{
			if (_on)
			{
				long timestamp = Stopwatch.GetTimestamp();
				if (_sub >= 0)
				{
					_ticks[_sub] += timestamp - _subMark;
				}
				_subMark = timestamp;
				_sub = (int)b;
			}
		}

		public static void Frame(float ms)
		{
			_frameMs[_frames % 8192] = ms;
			_frames++;
			if (ms > 16.666666f)
			{
				_over16++;
			}
			if (ms > 33.333332f)
			{
				_over33++;
			}
			if (ms > _worst)
			{
				_worst = ms;
			}
			if (_gcBase < 0)
			{
				_gcBase = GC.CollectionCount(0);
			}
		}

		public static Split Snapshot()
		{
			Split split = new Split();
			long num = _counts[0];
			long num2 = _counts[1];
			split.Passes = (int)num;
			split.Sampled = (int)num2;
			split.Seconds = (float)((double)(Stopwatch.GetTimestamp() - _windowStart) / (double)Stopwatch.Frequency);
			double num3 = 1000.0 / (double)Stopwatch.Frequency;
			for (int i = 0; i < 26; i++)
			{
				long num4 = ((i < 8) ? num : num2);
				split.Ms[i] = ((num4 > 0) ? ((float)((double)_ticks[i] * num3 / (double)num4)) : 0f);
			}
			for (int j = 0; j < 13; j++)
			{
				split.Counts[j] = ((num > 0) ? ((float)_counts[j] / (float)num) : 0f);
			}
			split.Counts[0] = num;
			split.Counts[1] = num2;
			split.Counts[6] = ((num > 0) ? ((float)(_counts[3] - _counts[5]) / (float)num) : 0f);
			split.SteerMs = Steering.CostMs;
			split.Frames = _frames;
			split.Over16 = _over16;
			split.Over33 = _over33;
			split.Worst = _worst;
			int num5 = Math.Min(_frames, 8192);
			if (num5 > 0)
			{
				Array.Copy(_frameMs, _sorted, num5);
				Array.Sort(_sorted, 0, num5);
				split.P50 = Rank(num5, 0.5f);
				split.P95 = Rank(num5, 0.95f);
				split.P99 = Rank(num5, 0.99f);
			}
			split.Gc0 = ((_gcBase >= 0) ? (GC.CollectionCount(0) - _gcBase) : 0);
			split.HeapMb = (float)GC.GetTotalMemory(forceFullCollection: false) / 1048576f;
			return split;
		}

		private static float Rank(int n, float q)
		{
			return _sorted[Math.Min(n - 1, Math.Max(0, (int)Math.Ceiling(q * (float)n) - 1))];
		}

		public static void ResetWindow()
		{
			Array.Clear(_ticks, 0, _ticks.Length);
			Array.Clear(_counts, 0, _counts.Length);
			_frames = (_over16 = (_over33 = 0));
			_worst = 0f;
			_gcBase = GC.CollectionCount(0);
			_windowStart = Stopwatch.GetTimestamp();
		}
	}
}
