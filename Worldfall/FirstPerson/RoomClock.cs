using System.Diagnostics;
using UnityEngine;

namespace FirstPerson
{
	internal static class RoomClock
	{
		internal enum P
		{
			Move,
			Scene,
			Backdrop,
			Collect,
			Draw,
			Rest,
			Count
		}

		private static readonly double[] _sum = new double[6];

		private static readonly double[] _test = new double[6];

		private static long _mark;

		private static int _frames;

		private static int _backdrops;

		private static int _testFrames;

		private static int _testBackdrops;

		private static double _testWall;

		private static float _since;

		internal static int TestFrames => _testFrames;

		internal static int TestBackdrops => _testBackdrops;

		internal static void Begin()
		{
			_mark = Stopwatch.GetTimestamp();
		}

		internal static void Lap(P part)
		{
			long timestamp = Stopwatch.GetTimestamp();
			double num = (double)(timestamp - _mark) * 1000.0 / (double)Stopwatch.Frequency;
			_mark = timestamp;
			_sum[(int)part] += num;
			_test[(int)part] += num;
		}

		internal static bool End(bool backdrop)
		{
			_frames++;
			_testFrames++;
			if (backdrop)
			{
				_backdrops++;
				_testBackdrops++;
			}
			_testWall += (double)Time.unscaledDeltaTime * 1000.0;
			float unscaledTime = Time.unscaledTime;
			if (_since <= 0f || unscaledTime - _since > 20f)
			{
				Reset(unscaledTime);
				return false;
			}
			return unscaledTime - _since >= 10f;
		}

		internal static string Line(string gpu)
		{
			float unscaledTime = Time.unscaledTime;
			float num = Mathf.Max(0.001f, unscaledTime - _since);
			int num2 = Mathf.Max(1, _frames);
			string result = $"room perf: {(float)_frames / num:F1} fps, town for the windows in {_backdrops} of {_frames} frames: " + $"move {_sum[0] / (double)num2:F2}, town scene {_sum[1] / (double)num2:F2}, town drawn {_sum[2] / (double)num2:F2}, room gathered {_sum[3] / (double)num2:F2}, room drawn {_sum[4] / (double)num2:F2}, rest {_sum[5] / (double)num2:F2} ms a frame; {gpu}";
			Reset(unscaledTime);
			return result;
		}

		private static void Reset(float now)
		{
			_since = now;
			_frames = (_backdrops = 0);
			for (int i = 0; i < _sum.Length; i++)
			{
				_sum[i] = 0.0;
			}
		}

		internal static void TestStart()
		{
			_testFrames = (_testBackdrops = 0);
			_testWall = 0.0;
			for (int i = 0; i < _test.Length; i++)
			{
				_test[i] = 0.0;
			}
		}

		internal static string TestLine()
		{
			int num = Mathf.Max(1, _testFrames);
			return $"{_testFrames} room frames ({_testBackdrops} with the town drawn for the windows), {_testWall / (double)num:F2} ms apart; move {_test[0] / (double)num:F2}, town scene {_test[1] / (double)num:F2}, town drawn {_test[2] / (double)num:F2}, room gathered {_test[3] / (double)num:F2}, room drawn {_test[4] / (double)num:F2}, rest {_test[5] / (double)num:F2} ms";
		}

		internal static double TestMs(P part)
		{
			return _test[(int)part] / (double)Mathf.Max(1, _testFrames);
		}
	}
}
