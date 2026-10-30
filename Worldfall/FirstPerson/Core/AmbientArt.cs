using System;

namespace FirstPerson.Core
{
	public static class AmbientArt
	{
		private static SpriteImage[] _birds;

		private static SpriteImage[] _fluff;

		public static SpriteImage Fluff(bool turned)
		{
			if (_fluff == null)
			{
				uint num = TerrainCache.Rgb(255, 255, 255);
				uint num2 = TerrainCache.Rgb(226, 226, 222);
				uint num3 = TerrainCache.Rgb(150, 130, 100);
				uint num4 = 0u;
				_fluff = new SpriteImage[2]
				{
					Image(5, 5, new uint[25]
					{
						num4, num4, num3, num4, num4, num2, num4, num, num4, num2,
						num4, num, num, num, num4, num2, num4, num, num4, num2,
						num4, num4, num2, num4, num4
					}),
					Image(5, 5, new uint[25]
					{
						num4, num4, num4, num4, num4, num4, num2, num, num2, num4,
						num4, num, num, num, num4, num4, num2, num, num2, num4,
						num4, num4, num2, num4, num4
					})
				};
			}
			return _fluff[turned ? 1u : 0u];
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

		public static SpriteImage Bird(int frame, bool gull)
		{
			if (_birds == null)
			{
				_birds = new SpriteImage[6];
				string[][] array = new string[3][]
				{
					new string[4] { "...B...", "..BBB..", ".W...W.", "W.....W" },
					new string[4] { "...B...", "WWWBWWW", ".......", "......." },
					new string[4] { "W.....W", ".W.B.W.", "..BBB..", "......." }
				};
				for (int i = 0; i < 2; i++)
				{
					for (int j = 0; j < 3; j++)
					{
						string[] array2 = array[j];
						uint[] array3 = new uint[28];
						for (int k = 0; k < 4; k++)
						{
							for (int l = 0; l < 7; l++)
							{
								char c = array2[k][l];
								if (c != '.')
								{
									bool flag = c == 'W' && (l == 0 || l == 6);
									array3[k * 7 + l] = ((i == 0) ? TerrainCache.Rgb(38, 36, 44) : ((c == 'B') ? TerrainCache.Rgb(244, 244, 240) : (flag ? TerrainCache.Rgb(70, 74, 84) : TerrainCache.Rgb(214, 218, 222))));
								}
							}
						}
						_birds[i * 3 + j] = Image(7, 4, array3);
					}
				}
			}
			return _birds[(gull ? 3 : 0) + Math.Max(0, Math.Min(2, frame))];
		}
	}
}
