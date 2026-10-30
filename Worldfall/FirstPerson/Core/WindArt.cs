using System;

namespace FirstPerson.Core
{
	public static class WindArt
	{
		private static SpriteImage[] _leaves;

		private static SpriteImage[] _blades;

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

		public static SpriteImage Leaf(int frame)
		{
			if (_leaves == null)
			{
				_leaves = new SpriteImage[4]
				{
					MakeLeaf(back: false),
					MakeLeaf(back: true),
					MakeEdge(),
					MakePetal()
				};
			}
			return _leaves[Math.Max(0, Math.Min(3, frame))];
		}

		private static SpriteImage MakeLeaf(bool back)
		{
			uint[] array = new uint[40];
			for (int i = 0; i < 5; i++)
			{
				for (int j = 1; j < 8; j++)
				{
					float value = ((float)(j - 1) + 0.5f) / 7f * 2f - 1f;
					float num = 2.1f * (1f - (float)Math.Pow(Math.Abs(value), 1.7000000476837158));
					float num2 = (float)i + 0.5f - 2.5f;
					if (!(Math.Abs(num2) > num))
					{
						int num3 = ((num2 > 0.4f) ? 255 : ((num2 > -0.6f) ? 222 : 186));
						if (i == 2 && j > 1 && j < 7)
						{
							num3 = 168;
						}
						if (back)
						{
							num3 = num3 * 4 / 5;
						}
						array[i * 8 + j] = TerrainCache.Rgb(num3, num3, num3);
					}
				}
			}
			array[16] = TerrainCache.Rgb(150, 150, 150);
			return Image(8, 5, array);
		}

		private static SpriteImage MakeEdge()
		{
			uint[] array = new uint[14];
			for (int i = 0; i < 7; i++)
			{
				int num = ((i == 0 || i == 6) ? 1 : 0);
				array[num * 7 + i] = TerrainCache.Rgb(205, 205, 205);
			}
			return Image(7, 2, array);
		}

		private static SpriteImage MakePetal()
		{
			uint num = TerrainCache.Rgb(255, 255, 255);
			uint num2 = TerrainCache.Rgb(226, 226, 226);
			uint num3 = 0u;
			return Image(4, 3, new uint[12]
			{
				num3, num2, num2, num3, num2, num, num, num2, num3, num,
				num, num3
			});
		}

		public static SpriteImage Blade(int frame)
		{
			if (_blades == null)
			{
				uint num = TerrainCache.Rgb(255, 255, 255);
				uint num2 = TerrainCache.Rgb(200, 200, 200);
				uint num3 = 0u;
				_blades = new SpriteImage[4]
				{
					Image(2, 4, new uint[8] { num2, num3, num2, num3, num3, num, num3, num }),
					Image(3, 3, new uint[9] { num2, num3, num3, num3, num, num3, num3, num3, num }),
					Image(4, 2, new uint[8] { num2, num2, num3, num3, num3, num3, num, num }),
					Image(3, 3, new uint[9] { num3, num3, num, num3, num, num3, num2, num3, num3 })
				};
			}
			return _blades[frame & 3];
		}
	}
}
