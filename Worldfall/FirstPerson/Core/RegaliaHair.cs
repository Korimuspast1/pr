namespace FirstPerson.Core
{
	public static class RegaliaHair
	{
		private static readonly uint[] Colors = new uint[64];

		private static readonly int[] Counts = new int[64];

		private static readonly uint[] Natural = new uint[8]
		{
			TerrainCache.Rgb(62, 40, 24),
			TerrainCache.Rgb(92, 58, 32),
			TerrainCache.Rgb(36, 28, 22),
			TerrainCache.Rgb(140, 92, 48),
			TerrainCache.Rgb(196, 160, 96),
			TerrainCache.Rgb(120, 52, 30),
			TerrainCache.Rgb(24, 20, 18),
			TerrainCache.Rgb(160, 120, 70)
		};

		public static uint FromHead(SpriteImage head, uint skin)
		{
			if (head?.Pixels == null)
			{
				return 0u;
			}
			int num = 0;
			uint[] pixels = head.Pixels;
			foreach (uint num2 in pixels)
			{
				if (num2 >> 24 < 128)
				{
					continue;
				}
				int num3 = (int)(num2 & 0xFF);
				int num4 = (int)((num2 >> 8) & 0xFF);
				int num5 = (int)((num2 >> 16) & 0xFF);
				if ((num4 > num3 + 40 && num4 > num5 + 40) || (num3 > 200 && num4 < 70 && num5 > 200) || (num3 < 70 && num4 > 200 && num5 > 200) || (num3 == 255 && num4 == 216 && num5 == 0) || (skin != 0 && Near(num2, skin, 18)))
				{
					continue;
				}
				uint num6 = num2 | 0xFF000000u;
				int j;
				for (j = 0; j < num && Colors[j] != num6; j++)
				{
				}
				if (j == num)
				{
					if (num == Colors.Length)
					{
						continue;
					}
					Colors[num] = num6;
					Counts[num] = 0;
					num++;
				}
				Counts[j]++;
			}
			int num7 = -1;
			for (int k = 0; k < num; k++)
			{
				if (num7 < 0 || Counts[k] > Counts[num7])
				{
					num7 = k;
				}
			}
			if (num7 >= 0)
			{
				return Colors[num7];
			}
			return 0u;
		}

		private static bool Near(uint a, uint b, int d)
		{
			uint num = (a & 0xFF) - (b & 0xFF);
			int num2 = (int)(((a >> 8) & 0xFF) - ((b >> 8) & 0xFF));
			int num3 = (int)(((a >> 16) & 0xFF) - ((b >> 16) & 0xFF));
			return (int)(num * num) + num2 * num2 + num3 * num3 < d * d;
		}

		public static uint Seeded(long id)
		{
			return Natural[(uint)((ulong)(id * 2654435761u) % (ulong)Natural.Length)];
		}
	}
}
