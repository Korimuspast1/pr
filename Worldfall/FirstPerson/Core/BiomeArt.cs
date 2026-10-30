using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class BiomeArt
	{
		private static readonly Dictionary<string, SpriteImage> Cache = new Dictionary<string, SpriteImage>();

		public static SpriteImage Bubble
		{
			get
			{
				if (Cache.TryGetValue("bubble", out var value))
				{
					return value;
				}
				uint[] array = new uint[121];
				for (int i = 0; i < 11; i++)
				{
					for (int j = 0; j < 11; j++)
					{
						float num = ((float)j + 0.5f) / 11f * 2f - 1f;
						float num2 = ((float)i + 0.5f) / 11f * 2f - 1f;
						float num3 = (float)Math.Sqrt(num * num + num2 * num2);
						if (!(num3 > 1f))
						{
							float num4 = Math.Max(0f, 1f - Math.Abs(num3 - 0.86f) / 0.2f);
							float val = 0.14f + 0.86f * num4;
							float num5 = num + 0.38f;
							float num6 = num2 - 0.38f;
							if (num5 * num5 + num6 * num6 < 0.05f)
							{
								val = 1f;
							}
							array[i * 11 + j] = 0xFFFFFF | ((uint)(Math.Min(1f, val) * 255f) << 24);
						}
					}
				}
				value = Image(11, 11, array);
				Cache["bubble"] = value;
				return value;
			}
		}

		public static SpriteImage UpDrop
		{
			get
			{
				if (Cache.TryGetValue("updrop", out var value))
				{
					return value;
				}
				uint[] array = new uint[7];
				for (int i = 0; i < 7; i++)
				{
					array[i] = (uint)(0xFFFFFF | (40 + i * 35 << 24));
				}
				value = Image(1, 7, array);
				Cache["updrop"] = value;
				return value;
			}
		}

		public static SpriteImage Pillar
		{
			get
			{
				if (Cache.TryGetValue("pillar", out var value))
				{
					return value;
				}
				uint[] array = new uint[768];
				for (int i = 0; i < 64; i++)
				{
					for (int j = 0; j < 12; j++)
					{
						float num = (((float)j + 0.5f) / 12f - 0.5f) * 2f;
						float num2 = (float)Math.Exp((0f - num) * num * 5f);
						float num3 = ((float)i + 0.5f) / 64f;
						float num4 = (float)Math.Pow(1f - num3, 0.800000011920929) * Math.Min(1f, num3 * 14f);
						float num5 = num2 * num4;
						if (!(num5 < 0.02f))
						{
							array[i * 12 + j] = 0xFFFFFF | ((uint)(Math.Min(1f, num5) * 255f) << 24);
						}
					}
				}
				value = Image(12, 64, array);
				Cache["pillar"] = value;
				return value;
			}
		}

		private static SpriteImage Rows(string key, params string[] rows)
		{
			if (Cache.TryGetValue(key, out var value))
			{
				return value;
			}
			int num = rows.Length;
			int length = rows[0].Length;
			uint[] array = new uint[length * num];
			for (int i = 0; i < num; i++)
			{
				for (int j = 0; j < length; j++)
				{
					uint num2;
					switch (rows[i][j])
					{
					case 'X':
						num2 = uint.MaxValue;
						break;
					case 'x':
						num2 = TerrainCache.Rgb(214, 214, 214);
						break;
					case 'o':
						num2 = TerrainCache.Rgb(160, 160, 160);
						break;
					case 'B':
						num2 = TerrainCache.Rgb(46, 40, 52);
						break;
					case 'h':
						num2 = 2164260863u;
						break;
					default:
						num2 = 0u;
						break;
					}
					array[(num - 1 - i) * length + j] = num2;
				}
			}
			value = Image(length, num, array);
			Cache[key] = value;
			return value;
		}

		private static SpriteImage Image(int w, int h, uint[] px)
		{
			SpriteImage spriteImage = new SpriteImage();
			spriteImage.Width = w;
			spriteImage.Height = h;
			spriteImage.Pixels = px;
			spriteImage.PivotX = 0.5f;
			spriteImage.UnitWidth = 1f;
			spriteImage.UnitHeight = 1f;
			spriteImage.MeasureOpaqueRows();
			return spriteImage;
		}

		public static SpriteImage Butterfly(int frame)
		{
			switch ((frame % 3 + 3) % 3)
			{
			case 0:
				return Rows("bf0", "XX.XX", "XXBXX", ".xBx.", "xx.xx");
			case 1:
				return Rows("bf1", ".X.X.", ".XBX.", "..B..", ".....");
			default:
				return Rows("bf2", ".....", "xXBXx", ".xBx.", ".....");
			}
		}

		public static SpriteImage Moth(int frame)
		{
			if ((frame & 1) != 0)
			{
				return Rows("mo1", "..x..", ".xBx.", "..B..", ".....");
			}
			return Rows("mo0", ".....", "xXBXx", "xxBxx", ".....");
		}

		public static SpriteImage Petal(int frame)
		{
			switch (frame & 3)
			{
			case 0:
				return Rows("pe0", ".XX..", "XXXX.", ".XXXx", "..xx.");
			case 1:
				return Rows("pe1", ".....", ".XXX.", "XXXxx", ".....");
			case 2:
				return Rows("pe2", ".....", ".....", "XXXXx", ".....");
			default:
				return Rows("pe3", "..xx.", ".xXXX", "xXXX.", ".XX..");
			}
		}

		public static SpriteImage Samara(int frame)
		{
			switch (frame & 3)
			{
			case 0:
				return Rows("sa0", ".....", "...xX", ".oXXx", ".....", ".....");
			case 1:
				return Rows("sa1", ".....", "..o..", "..Xx.", "..XX.", "..xX.");
			case 2:
				return Rows("sa2", ".....", ".....", "xXXo.", "Xx...", ".....");
			default:
				return Rows("sa3", ".Xx..", ".XX..", ".xX..", "..o..", ".....");
			}
		}

		public static SpriteImage Clover(int frame)
		{
			if ((frame & 1) != 0)
			{
				return Rows("cl1", ".....", ".....", "xXXXx", "..x..", "..x..");
			}
			return Rows("cl0", ".X.X.", "XXXXX", ".XXX.", "..x..", "..x..");
		}

		public static SpriteImage Star(int frame)
		{
			if ((frame & 1) != 0)
			{
				return Rows("st1", ".....", "..h..", ".hXh.", "..h..", ".....");
			}
			return Rows("st0", "..h..", "..X..", "hXXXh", "..X..", "..h..");
		}

		public static SpriteImage Shard(int frame)
		{
			switch (frame & 3)
			{
			case 0:
				return Rows("sh0", ".X.", "XXo", "Xxo", "Xxo", ".o.");
			case 1:
				return Rows("sh1", ".x.", "xXx", "oXo", "oXx", ".x.");
			case 2:
				return Rows("sh2", ".x.", "oxX", "oxX", "oXX", ".X.");
			default:
				return Rows("sh3", ".X.", ".X.", ".x.", ".x.", ".o.");
			}
		}

		public static SpriteImage Whiff(int frame)
		{
			if ((frame & 1) != 0)
			{
				return Rows("wh1", "X..", ".X.", "..X", "..X", ".X.", "X..", "X..", ".h.");
			}
			return Rows("wh0", ".X.", "..X", "..X", ".X.", "X..", "X..", ".X.", "..h");
		}

		public static SpriteImage Sprinkle(int frame)
		{
			switch ((frame % 3 + 3) % 3)
			{
			case 0:
				return Rows("sp0", ".X.", ".X.", ".x.");
			case 1:
				return Rows("sp1", "..X", ".X.", "x..");
			default:
				return Rows("sp2", "...", "XXx", "...");
			}
		}

		public static SpriteImage Dot(int frame)
		{
			return Rows("dot", "hXh", "XXX", "hXh");
		}

		public static SpriteImage Bit(int frame)
		{
			if ((frame & 1) != 0)
			{
				return Rows("bi1", "XX", "XX");
			}
			return Rows("bi0", "XXX", "XoX", "XXX");
		}
	}
}
