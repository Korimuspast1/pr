using System;

namespace FirstPerson.Core
{
	public static class FarOutline
	{
		public static FarLook Of(SpriteImage img, out float grow, out float lift)
		{
			grow = 1f;
			lift = 0f;
			if (img == null || img.Pixels == null || img.Width <= 0 || img.Height <= 0)
			{
				return null;
			}
			int width = img.Width;
			int height = img.Height;
			uint[] pixels = img.Pixels;
			int num = width;
			int num2 = -1;
			int num3 = height;
			int num4 = -1;
			long num5 = 0L;
			long num6 = 0L;
			long num7 = 0L;
			long num8 = 0L;
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < width; j++)
				{
					uint num9 = pixels[i * width + j];
					if (num9 >> 24 >= 128)
					{
						if (j < num)
						{
							num = j;
						}
						if (j > num2)
						{
							num2 = j;
						}
						if (i < num3)
						{
							num3 = i;
						}
						if (i > num4)
						{
							num4 = i;
						}
						num5 += num9 & 0xFF;
						num6 += (num9 >> 8) & 0xFF;
						num7 += (num9 >> 16) & 0xFF;
						num8++;
					}
				}
			}
			if (num8 == 0L)
			{
				return null;
			}
			uint num10 = (uint)(-16777216 | (int)Math.Min(255L, num5 / num8 * 6 / 5 + 10) | ((int)Math.Min(255L, num6 / num8 * 6 / 5 + 10) << 8) | ((int)Math.Min(255L, num7 / num8 * 6 / 5 + 10) << 16));
			uint num11 = (uint)(-16777216 | (int)(num5 / num8 / 8 + 6) | ((int)(num6 / num8 / 8 + 4) << 8) | ((int)(num7 / num8 / 6 + 14) << 16));
			int num12 = num2 - num + 3;
			int num13 = num4 - num3 + 3;
			uint[] array = new uint[num12 * num13];
			for (int k = 0; k < num13; k++)
			{
				for (int l = 0; l < num12; l++)
				{
					if (Shown(pixels, width, height, l + num - 1, k + num3 - 1))
					{
						array[k * num12 + l] = num10;
					}
					else if (Shown(pixels, width, height, l + num - 2, k + num3 - 1) || Shown(pixels, width, height, l + num, k + num3 - 1) || Shown(pixels, width, height, l + num - 1, k + num3 - 2) || Shown(pixels, width, height, l + num - 1, k + num3))
					{
						array[k * num12 + l] = num11;
					}
				}
			}
			int[] array2 = new int[5];
			int[] array3 = new int[5];
			uint[][] array4 = new uint[5][];
			array2[0] = num12;
			array3[0] = num13;
			array4[0] = array;
			int m;
			for (m = 1; m < 5 && Math.Max(array2[m - 1], array3[m - 1]) > 2; m++)
			{
				array4[m] = Half(array4[m - 1], array2[m - 1], array3[m - 1], out array2[m], out array3[m]);
			}
			FarLook farLook = new FarLook
			{
				W = new int[m],
				H = new int[m],
				Pixels = new uint[m][],
				Creature = true
			};
			Array.Copy(array2, farLook.W, m);
			Array.Copy(array3, farLook.H, m);
			Array.Copy(array4, farLook.Pixels, m);
			farLook.Left = (float)(num - 1) / (float)width - img.PivotX;
			farLook.Right = (float)(num2 + 2) / (float)width - img.PivotX;
			int num14 = num4 - num3 + 1;
			grow = (float)num13 / (float)num14;
			lift = -1f / (float)num14;
			return farLook;
		}

		private static bool Shown(uint[] px, int w, int h, int x, int y)
		{
			if (x >= 0 && y >= 0 && x < w && y < h)
			{
				return px[y * w + x] >> 24 >= 128;
			}
			return false;
		}

		private static uint[] Half(uint[] src, int w, int h, out int hw, out int hh)
		{
			hw = (w + 1) / 2;
			hh = (h + 1) / 2;
			uint[] array = new uint[hw * hh];
			for (int i = 0; i < hh; i++)
			{
				for (int j = 0; j < hw; j++)
				{
					int num = 0;
					int num2 = 0;
					int num3 = 0;
					int num4 = 0;
					int num5 = 0;
					for (int k = 0; k < 2; k++)
					{
						for (int l = 0; l < 2; l++)
						{
							int num6 = j * 2 + l;
							int num7 = i * 2 + k;
							if (num6 < w && num7 < h)
							{
								num5++;
								uint num8 = src[num7 * w + num6];
								if (num8 >> 24 != 0)
								{
									num += (int)(num8 & 0xFF);
									num2 += (int)((num8 >> 8) & 0xFF);
									num3 += (int)((num8 >> 16) & 0xFF);
									num4++;
								}
							}
						}
					}
					if (num4 != 0 && num4 * 2 >= num5)
					{
						array[i * hw + j] = (uint)(-16777216 | (num / num4) | (num2 / num4 << 8) | (num3 / num4 << 16));
					}
				}
			}
			return array;
		}
	}
}
