using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal static class Stature
	{
		private sealed class Track
		{
			public float Value;

			public float Peak;

			public float Previous;

			public float Window;

			public int Frame;
		}

		private const float WindowSeconds = 0.8f;

		private static readonly Dictionary<Actor, Track> Tracks = new Dictionary<Actor, Track>();

		private static readonly List<Actor> Stale = new List<Actor>();

		private static int _cleaned;

		public static float Of(Actor a, float raw, float dt)
		{
			if (a == null || !(raw > 0f))
			{
				return raw;
			}
			int frameCount = Time.frameCount;
			if (!Tracks.TryGetValue(a, out var value) || frameCount - value.Frame > 90)
			{
				value = new Track
				{
					Value = raw,
					Peak = raw,
					Previous = raw,
					Frame = frameCount
				};
				Tracks[a] = value;
				Clean(frameCount);
				return raw;
			}
			if (value.Frame == frameCount)
			{
				return value.Value;
			}
			value.Frame = frameCount;
			value.Window += dt;
			if (value.Window >= 0.8f)
			{
				value.Previous = value.Peak;
				value.Peak = raw;
				value.Window = 0f;
			}
			else if (raw > value.Peak)
			{
				value.Peak = raw;
			}
			float num = Mathf.Max(value.Peak, value.Previous);
			float num2 = ((Mathf.Abs(num - value.Value) > 0.3f * value.Value) ? 6f : 1.2f);
			value.Value = Mathf.Lerp(value.Value, num, 1f - Mathf.Exp((0f - num2) * Mathf.Min(dt, 0.1f)));
			return value.Value;
		}

		public static float Peek(Actor a, float raw)
		{
			if (a == null || !Tracks.TryGetValue(a, out var value) || Time.frameCount - value.Frame > 90)
			{
				return raw;
			}
			return value.Value;
		}

		private static void Clean(int frame)
		{
			if (frame - _cleaned < 600)
			{
				return;
			}
			_cleaned = frame;
			Stale.Clear();
			foreach (KeyValuePair<Actor, Track> track in Tracks)
			{
				if (frame - track.Value.Frame > 600)
				{
					Stale.Add(track.Key);
				}
			}
			foreach (Actor item in Stale)
			{
				Tracks.Remove(item);
			}
		}
	}
}
