using System;

namespace FirstPerson.Core
{
	internal static class Fm
	{
		public const float PI = MathF.PI;

		public const float Deg2Rad = MathF.PI / 180f;

		public const float Rad2Deg = 180f / MathF.PI;

		public static float Clamp01(float v)
		{
			if (!(v < 0f))
			{
				if (!(v > 1f))
				{
					return v;
				}
				return 1f;
			}
			return 0f;
		}

		public static float Clamp(float v, float lo, float hi)
		{
			if (!(v < lo))
			{
				if (!(v > hi))
				{
					return v;
				}
				return hi;
			}
			return lo;
		}

		public static float Lerp(float a, float b, float t)
		{
			return a + (b - a) * Clamp01(t);
		}

		public static float MoveTowards(float a, float b, float d)
		{
			if (!(Math.Abs(b - a) <= d))
			{
				return a + (float)Math.Sign(b - a) * d;
			}
			return b;
		}

		public static float Sin(float v)
		{
			return (float)Math.Sin(v);
		}

		public static float Cos(float v)
		{
			return (float)Math.Cos(v);
		}

		public static float Atan(float v)
		{
			return (float)Math.Atan(v);
		}

		public static float Exp(float v)
		{
			return (float)Math.Exp(v);
		}

		public static float Max(float a, float b)
		{
			if (!(a > b))
			{
				return b;
			}
			return a;
		}

		public static float DeltaAngle(float a, float b)
		{
			float num = (b - a) % 360f;
			if (num > 180f)
			{
				num -= 360f;
			}
			if (num < -180f)
			{
				num += 360f;
			}
			return num;
		}
	}
}
