using System;
using System.Runtime.CompilerServices;

namespace FirstPerson.Core
{
	public static class Sea
	{
		public const float W1x = 0.8f;

		public const float W1y = 0.6f;

		public const float W1k = 1.257f;

		public const float W1w = 1.1f;

		public const float W1a = 0.016f;

		public const float W2x = -0.35f;

		public const float W2y = 0.94f;

		public const float W2k = 2.03f;

		public const float W2w = 1.5f;

		public const float W2a = 0.01f;

		public const float W3x = 0.93f;

		public const float W3y = -0.37f;

		public const float W3k = 3.3f;

		public const float W3w = 2.1f;

		public const float W3a = 0.005f;

		public const float OpenSwell = 2.2f;

		public const float S1k = 0.24f;

		public const float S1w = 1.16f;

		public const float S2k = 0.37f;

		public const float S2w = 1.44f;

		public const float S1Most = 1.1f;

		public const float S2Most = 0.45f;

		public const float ShallowStorm = 0.12f;

		public const float RangeFair = 24f;

		public const float RangeStorm = 100f;

		public static float Now;

		public static float EyeX;

		public static float EyeY;

		private const int SinSize = 1024;

		private const float SinScale = 512f / MathF.PI;

		private static readonly float[] SinTable = BuildSinTable();

		public static float Storm { get; private set; }

		public static float A1 { get; private set; }

		public static float A2 { get; private set; }

		public static float DirX { get; private set; } = 1f;

		public static float DirY { get; private set; }

		public static float Dir2X { get; private set; } = 0.866f;

		public static float Dir2Y { get; private set; } = 0.5f;

		public static float Range { get; private set; } = 24f;

		public static float Lift { get; private set; } = 0.0992f;

		public static float FadeAt(float x, float y)
		{
			float num = x - EyeX;
			float num2 = y - EyeY;
			float num3 = (float)Math.Sqrt(num * num + num2 * num2);
			if (!(num3 >= Range))
			{
				return 1f - num3 / Range;
			}
			return 0f;
		}

		public static void Set(float storm, float dirX, float dirY)
		{
			storm = ((storm < 0f) ? 0f : ((storm > 1f) ? 1f : storm));
			Storm = storm;
			A1 = 1.1f * storm;
			A2 = 0.45f * storm;
			float num = (float)Math.Sqrt(dirX * dirX + dirY * dirY);
			if (num > 0.0001f)
			{
				DirX = dirX / num;
				DirY = dirY / num;
			}
			Dir2X = DirX * 0.866f - DirY * 0.5f;
			Dir2Y = DirX * 0.5f + DirY * 0.866f;
			Range = 24f + 76f * storm;
			Lift = 0.0992f + 1.25f * (A1 + A2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float OpenShare(float depth, float reachTiles)
		{
			float num = Smooth((depth - 1f) * (1f / 7f)) * Smooth((reachTiles - 2f) * 0.1f);
			float num2 = 0.12f * Smooth(depth * 1.25f) * Smooth((reachTiles - 0.5f) * 0.4f);
			if (!(num > num2))
			{
				return num2;
			}
			return num;
		}

		public static float Height(float x, float y, float t, float open, float shore = 1f)
		{
			float num = (0.016f * FastSin(1.257f * (0.8f * x + 0.6f * y) - 1.1f * t) + 0.01f * FastSin(2.03f * (-0.35f * x + 0.94f * y) - 1.5f * t + 1.7f) + 0.005f * FastSin(3.3f * (0.93f * x + -0.37f * y) - 2.1f * t + 4.1f)) * (1f + 2.2f * open);
			if (A1 > 0f)
			{
				num += StormRise(x, y, t, open) * shore;
			}
			return num;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float ShoreFade(float side)
		{
			if (!(side >= 0.4f))
			{
				if (!(side <= 0f))
				{
					return Smooth(side * 2.5f);
				}
				return 0f;
			}
			return 1f;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float StormRise(float x, float y, float t, float share)
		{
			float num = 1f + FastSin(0.24f * (DirX * x + DirY * y) - 1.16f * t);
			float num2 = 1f + FastSin(0.37f * (Dir2X * x + Dir2Y * y) - 1.44f * t + 2.3f);
			return (A1 * (num * num * 0.5f - 0.75f) + A2 * (num2 * num2 * 0.5f - 0.75f)) * share;
		}

		public static void StormSlope(float x, float y, float t, float share, out float gx, out float gy)
		{
			gx = (gy = 0f);
			if (!(A1 <= 0f))
			{
				float x2 = 0.24f * (DirX * x + DirY * y) - 1.16f * t;
				float x3 = 0.37f * (Dir2X * x + Dir2Y * y) - 1.44f * t + 2.3f;
				float num = A1 * (1f + FastSin(x2)) * FastCos(x2) * 0.24f * share;
				float num2 = A2 * (1f + FastSin(x3)) * FastCos(x3) * 0.37f * share;
				gx = num * DirX + num2 * Dir2X;
				gy = num * DirY + num2 * Dir2Y;
			}
		}

		private static float[] BuildSinTable()
		{
			float[] array = new float[1024];
			for (int i = 0; i < 1024; i++)
			{
				array[i] = (float)Math.Sin((float)i / (512f / MathF.PI));
			}
			return array;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float FastSin(float x)
		{
			return SinTable[(int)(x * (512f / MathF.PI)) & 0x3FF];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float FastCos(float x)
		{
			return SinTable[((int)(x * (512f / MathF.PI)) + 256) & 0x3FF];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static float Smooth(float x)
		{
			x = ((x < 0f) ? 0f : ((x > 1f) ? 1f : x));
			return x * x * (3f - 2f * x);
		}
	}
}
