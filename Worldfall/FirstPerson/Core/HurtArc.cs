using System;

namespace FirstPerson.Core
{
	public static class HurtArc
	{
		public const float Left = -0.47f;

		public const float Right = 0.47f;

		public const float Inner = 0.78f;

		public const float Outer = 1.3f;

		public const float Radius = 0.085f;

		public const float Hold = 0.9f;

		public const float Life = 2.2f;

		private const float Sweep = 0.454f;

		private const float Band = 0.955f;

		private const float HalfWidth = 0.065f;

		private const float Ink = 0.03f;

		private static readonly float[] TipX = new float[3] { -0.1f, 0.1f, 0f };

		private static readonly float[] TipY = new float[3] { 0.99f, 0.99f, 1.21f };

		public static float Alpha(float age)
		{
			if (!(age < 0.9f))
			{
				return Clamp01(1f - (age - 0.9f) / 1.3000001f);
			}
			return 1f;
		}

		public static float Pop(float age)
		{
			float num = 1f - Clamp01(age / 0.16f);
			return 1f + 0.22f * num * num;
		}

		public static uint[] Paint(int width, out int height)
		{
			height = Math.Max(1, (int)Math.Round((float)width * 0.52f / 0.94f));
			float num = 0.94f / (float)width;
			uint[] array = new uint[width * height];
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < width; j++)
				{
					float num2 = -0.47f + ((float)j + 0.5f) * num;
					float num3 = 0.78f + ((float)i + 0.5f) * num;
					float num4 = Distance(num2, num3);
					float num5 = Clamp01(0.5f - (num4 - 0.03f) / num);
					if (!(num5 <= 0f))
					{
						float num6 = Clamp01(0.5f - num4 / num);
						float num7 = (float)Math.Sqrt(num2 * num2 + num3 * num3);
						float num8 = 0.8f + 0.2f * Clamp01((num7 - 0.89f) / 0.25f);
						float num9 = 0.12f + (num8 - 0.12f) * num6;
						float num10 = num5 * (0.82f + 0.18f * num6);
						uint num11 = (uint)(num9 * 255f + 0.5f);
						array[i * width + j] = num11 | (num11 << 8) | (num11 << 16) | ((uint)(num10 * 255f + 0.5f) << 24);
					}
				}
			}
			return array;
		}

		private static float Distance(float x, float y)
		{
			float num = (float)Math.Sqrt(x * x + y * y);
			float num2 = Math.Abs((float)Math.Atan2(x, y));
			float num3 = Clamp01(num2 / 0.454f);
			float num4 = 0.065f * (1f - 0.75f * num3 * num3);
			return Math.Min(Math.Max(Math.Abs(num - 0.955f) - num4, (num2 - 0.454f) * num), Triangle(x, y));
		}

		private static float Triangle(float x, float y)
		{
			float num = float.MaxValue;
			bool flag = true;
			int i = 0;
			int num2 = 2;
			for (; i < 3; i++)
			{
				float num3 = TipX[i] - TipX[num2];
				float num4 = TipY[i] - TipY[num2];
				float num5 = x - TipX[num2];
				float num6 = y - TipY[num2];
				float num7 = Clamp01((num5 * num3 + num6 * num4) / (num3 * num3 + num4 * num4));
				float num8 = num5 - num3 * num7;
				float num9 = num6 - num4 * num7;
				num = Math.Min(num, num8 * num8 + num9 * num9);
				if (num3 * num6 - num4 * num5 < 0f)
				{
					flag = false;
				}
				num2 = i;
			}
			float num10 = (float)Math.Sqrt(num);
			if (!flag)
			{
				return num10;
			}
			return 0f - num10;
		}

		private static float Clamp01(float v)
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
	}
}
