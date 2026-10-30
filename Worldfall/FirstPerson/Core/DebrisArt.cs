using System;

namespace FirstPerson.Core
{
	public static class DebrisArt
	{
		private static SpriteImage[] _clods;

		private static SpriteImage[] _planks;

		private static SpriteImage[] _boards;

		private static SpriteImage[] _branches;

		private static SpriteImage _stone;

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

		public static SpriteImage Clod(int variant)
		{
			if (_clods == null)
			{
				_clods = new SpriteImage[4];
				for (int i = 0; i < 4; i++)
				{
					uint[] array = new uint[25];
					for (int j = 0; j < 5; j++)
					{
						for (int k = 0; k < 5; k++)
						{
							uint num = TerrainModel.Hash(k * 7 + i * 31, j * 13 + i);
							float num2 = (float)k - 2f;
							float num3 = (float)j - 2f;
							if (!(num2 * num2 + num3 * num3 > 4.2f + (float)(num & 3) * 0.6f))
							{
								uint num4 = ((j >= 3 && i < 2 && (num & 1) == 0) ? TerrainCache.Rgb(76, 116, 52) : TerrainCache.Rgb((int)(92 + (num & 0xF)), (int)(66 + ((num >> 4) & 0xF)), 44));
								array[j * 5 + k] = num4;
							}
						}
					}
					_clods[i] = Image(5, 5, array);
				}
			}
			return _clods[variant & 3];
		}

		public static SpriteImage Plank(int frame)
		{
			if (_planks == null)
			{
				_planks = Tumbling(9, 2, TerrainCache.Rgb(226, 226, 226), TerrainCache.Rgb(176, 176, 176));
			}
			return _planks[frame & 3];
		}

		public static SpriteImage Board(int frame)
		{
			if (_boards == null)
			{
				_boards = Tumbling(5, 3, TerrainCache.Rgb(232, 232, 232), TerrainCache.Rgb(180, 180, 180));
			}
			return _boards[frame & 3];
		}

		private static SpriteImage[] Tumbling(int len, int thick, uint light, uint dark)
		{
			SpriteImage[] array = new SpriteImage[4];
			for (int i = 0; i < 4; i++)
			{
				int num = len + 1;
				uint[] array2 = new uint[num * num];
				float num2 = (float)i * MathF.PI / 4f;
				float num3 = (float)Math.Cos(num2);
				float num4 = (float)Math.Sin(num2);
				for (int j = 0; j < num; j++)
				{
					for (int k = 0; k < num; k++)
					{
						float num5 = (float)k + 0.5f - (float)num * 0.5f;
						float num6 = (float)j + 0.5f - (float)num * 0.5f;
						float value = num5 * num3 + num6 * num4;
						float num7 = (0f - num5) * num4 + num6 * num3;
						if (!(Math.Abs(value) > (float)len * 0.5f) && !(Math.Abs(num7) > (float)thick * 0.5f))
						{
							array2[j * num + k] = ((num7 > 0f) ? light : dark);
						}
					}
				}
				array[i] = Image(num, num, array2);
			}
			return array;
		}

		public static SpriteImage Branch(int frame)
		{
			if (_branches == null)
			{
				_branches = new SpriteImage[4];
				uint num = TerrainCache.Rgb(96, 70, 46);
				uint num2 = TerrainCache.Rgb(70, 120, 50);
				uint num3 = TerrainCache.Rgb(98, 148, 62);
				for (int i = 0; i < 4; i++)
				{
					uint[] array = new uint[81];
					float num4 = (float)i * MathF.PI / 4f + 0.3f;
					float num5 = (float)Math.Cos(num4);
					float num6 = (float)Math.Sin(num4);
					for (int j = 0; j < 9; j++)
					{
						for (int k = 0; k < 9; k++)
						{
							float num7 = (float)k + 0.5f - 4.5f;
							float num8 = (float)j + 0.5f - 4.5f;
							float num9 = num7 * num5 + num8 * num6;
							float value = (0f - num7) * num6 + num8 * num5;
							uint num10 = TerrainModel.Hash(k * 5 + i, j * 3 + 11);
							if (Math.Abs(value) < 0.6f && Math.Abs(num9) < 4.2f)
							{
								array[j * 9 + k] = num;
							}
							else if (num9 > -1f && Math.Abs(value) < 2.6f - Math.Abs(num9 - 1.5f) * 0.45f && (num10 & 3) != 0)
							{
								array[j * 9 + k] = (((num10 & 4) != 0) ? num2 : num3);
							}
						}
					}
					_branches[i] = Image(9, 9, array);
				}
			}
			return _branches[frame & 3];
		}

		public static SpriteImage Stone()
		{
			if (_stone != null)
			{
				return _stone;
			}
			uint num = TerrainCache.Rgb(150, 148, 144);
			uint num2 = TerrainCache.Rgb(116, 114, 112);
			uint num3 = TerrainCache.Rgb(84, 82, 82);
			uint num4 = 0u;
			return _stone = Image(4, 4, new uint[16]
			{
				num4, num3, num3, num4, num3, num2, num2, num3, num2, num,
				num2, num3, num4, num2, num, num4
			});
		}
	}
}
