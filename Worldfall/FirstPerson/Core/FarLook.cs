using System;

namespace FirstPerson.Core
{
	public sealed class FarLook
	{
		public int[] W;

		public int[] H;

		public uint[][] Pixels;

		public float Left;

		public float Right;

		public int Slot = -1;

		public bool Creature;

		private const int MostLevels = 5;

		public int Levels => Pixels.Length;

		public static FarLook From(SpriteImage img)
		{
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
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < width; j++)
				{
					if (pixels[i * width + j] >> 24 >= 128)
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
					}
				}
			}
			if (num2 < num || num4 < num3)
			{
				return null;
			}
			int num5 = num2 - num + 1;
			int num6 = num4 - num3 + 1;
			uint[] array = new uint[num5 * num6];
			for (int k = 0; k < num6; k++)
			{
				for (int l = 0; l < num5; l++)
				{
					uint num7 = pixels[(k + num3) * width + l + num];
					array[k * num5 + l] = ((num7 >> 24 >= 128) ? (num7 | 0xFF000000u) : 0u);
				}
			}
			int[] array2 = new int[5];
			int[] array3 = new int[5];
			uint[][] array4 = new uint[5][];
			array2[0] = num5;
			array3[0] = num6;
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
				Pixels = new uint[m][]
			};
			Array.Copy(array2, farLook.W, m);
			Array.Copy(array3, farLook.H, m);
			Array.Copy(array4, farLook.Pixels, m);
			farLook.Left = (float)num / (float)width - img.PivotX;
			farLook.Right = (float)(num2 + 1) / (float)width - img.PivotX;
			return farLook;
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

		public FarLook Recoloured(Func<uint, bool, uint> colour)
		{
			int num = W[0];
			int num2 = H[0];
			uint[] array = Pixels[0];
			uint[] array2 = new uint[array.Length];
			for (int i = 0; i < num2; i++)
			{
				for (int j = 0; j < num; j++)
				{
					uint num3 = array[i * num + j];
					if (num3 != 0)
					{
						bool arg = i + 1 >= num2 || array[(i + 1) * num + j] == 0;
						array2[i * num + j] = colour(num3, arg) | 0xFF000000u;
					}
				}
			}
			int num4 = Pixels.Length;
			FarLook farLook = new FarLook
			{
				W = (int[])W.Clone(),
				H = (int[])H.Clone(),
				Pixels = new uint[num4][],
				Left = Left,
				Right = Right
			};
			farLook.Pixels[0] = array2;
			for (int k = 1; k < num4; k++)
			{
				farLook.Pixels[k] = Half(farLook.Pixels[k - 1], farLook.W[k - 1], farLook.H[k - 1], out var _, out var _);
			}
			return farLook;
		}

		public int LevelFor(float rows)
		{
			int i;
			for (i = 0; i + 1 < Pixels.Length && (float)H[i + 1] >= rows; i++)
			{
			}
			return i;
		}
	}
}
