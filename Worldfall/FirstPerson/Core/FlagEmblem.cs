using System;

namespace FirstPerson.Core
{
	public sealed class FlagEmblem
	{
		public const byte Clear = 0;

		public const byte Field = 1;

		public const byte Dark = 2;

		public const byte Emblem = 3;

		public int W;

		public int H;

		public byte[] Tone;

		public byte GreyField = byte.MaxValue;

		public byte GreyDark = 200;

		public byte GreyEmblem = byte.MaxValue;

		public byte At(int x, int y)
		{
			if (x >= 0 && y >= 0 && x < W && y < H)
			{
				return Tone[y * W + x];
			}
			return 0;
		}

		public bool Box(out int x0, out int y0, out int x1, out int y1)
		{
			x0 = (y0 = int.MaxValue);
			x1 = (y1 = -1);
			for (int i = 0; i < H; i++)
			{
				for (int j = 0; j < W; j++)
				{
					if (Tone[i * W + j] != 0)
					{
						if (j < x0)
						{
							x0 = j;
						}
						if (j > x1)
						{
							x1 = j;
						}
						if (i < y0)
						{
							y0 = i;
						}
						if (i > y1)
						{
							y1 = i;
						}
					}
				}
			}
			return x1 >= 0;
		}

		public static FlagEmblem FromBanner(SpriteImage background, SpriteImage icon)
		{
			SpriteImage spriteImage = background ?? icon;
			if (spriteImage == null)
			{
				return null;
			}
			FlagEmblem flagEmblem = new FlagEmblem
			{
				W = spriteImage.Width,
				H = spriteImage.Height,
				Tone = new byte[spriteImage.Width * spriteImage.Height]
			};
			if (background != null)
			{
				int[] array = new int[256];
				uint[] pixels = background.Pixels;
				foreach (uint num in pixels)
				{
					if (num >> 24 >= 128)
					{
						array[GreyOf(num)]++;
					}
				}
				int num2 = -1;
				int num3 = -1;
				for (int j = 0; j < 256; j++)
				{
					if (array[j] != 0)
					{
						if (num2 < 0 || array[j] > array[num2])
						{
							num3 = num2;
							num2 = j;
						}
						else if (num3 < 0 || array[j] > array[num3])
						{
							num3 = j;
						}
					}
				}
				if (num2 >= 0)
				{
					int num4 = ((num3 < 0) ? num2 : Math.Max(num2, num3));
					int num5 = ((num3 < 0) ? (num2 * 200 / 255) : Math.Min(num2, num3));
					flagEmblem.GreyField = (byte)num4;
					flagEmblem.GreyDark = (byte)num5;
					for (int k = 0; k < flagEmblem.H && k < background.Height; k++)
					{
						for (int l = 0; l < flagEmblem.W && l < background.Width; l++)
						{
							uint num6 = background.Pixels[k * background.Width + l];
							if (num6 >> 24 >= 128)
							{
								int num7 = GreyOf(num6);
								flagEmblem.Tone[k * flagEmblem.W + l] = (byte)((num3 < 0 || Math.Abs(num7 - num5) >= Math.Abs(num7 - num4)) ? 1 : 2);
							}
						}
					}
				}
			}
			if (icon != null)
			{
				flagEmblem.GreyEmblem = Over(flagEmblem, icon);
			}
			return flagEmblem;
		}

		public static FlagEmblem FromIcon(SpriteImage icon)
		{
			if (icon == null)
			{
				return null;
			}
			FlagEmblem obj = new FlagEmblem
			{
				W = icon.Width,
				H = icon.Height,
				Tone = new byte[icon.Width * icon.Height]
			};
			obj.GreyEmblem = Over(obj, icon);
			return obj;
		}

		private static byte Over(FlagEmblem e, SpriteImage icon)
		{
			int[] array = new int[256];
			for (int i = 0; i < e.H && i < icon.Height; i++)
			{
				for (int j = 0; j < e.W && j < icon.Width; j++)
				{
					uint num = icon.Pixels[i * icon.Width + j];
					if (num >> 24 >= 128)
					{
						e.Tone[i * e.W + j] = 3;
						array[GreyOf(num)]++;
					}
				}
			}
			int num2 = 255;
			for (int k = 0; k < 256; k++)
			{
				if (array[k] > array[num2])
				{
					num2 = k;
				}
			}
			return (byte)num2;
		}

		private static int GreyOf(uint p)
		{
			return (int)(((p & 0xFF) + ((p >> 8) & 0xFF) + ((p >> 16) & 0xFF) + 1) / 3);
		}
	}
}
