using System;

namespace FirstPerson.Core
{
	public static class ShotgunPattern
	{
		public const float Spread = 0.058f;

		public static void Offset(int i, int n, int shot, out float right, out float up)
		{
			right = (up = 0f);
			if (n > 1)
			{
				float num = Rand(shot, 991) * 6.2832f;
				float num2;
				float num3;
				if (i == 0)
				{
					num2 = 0.08f;
					num3 = Rand(shot, 7) * 6.2832f;
				}
				else if (i <= 5 && n > 6)
				{
					num2 = 0.47f;
					num3 = (float)(i - 1) / 5f * 6.2832f + num;
				}
				else
				{
					int val = ((n > 6) ? (n - 6) : (n - 1));
					int num4 = ((n > 6) ? (i - 6) : (i - 1));
					num2 = 0.9f;
					num3 = ((float)num4 + 0.5f) / (float)Math.Max(1, val) * 6.2832f - num * 0.7f;
				}
				num2 *= 0.82f + 0.36f * Rand(shot, i * 13 + 1);
				num3 += (Rand(shot, i * 13 + 2) - 0.5f) * 0.55f;
				right = (float)Math.Cos(num3) * num2 * 0.058f;
				up = (float)Math.Sin(num3) * num2 * 0.058f * 0.9f;
			}
		}

		private static float Rand(int shot, int k)
		{
			return (float)(TerrainModel.Hash(shot * 7919 + 13, k * 104729 + 7) & 0xFFFF) / 65535f;
		}
	}
}
