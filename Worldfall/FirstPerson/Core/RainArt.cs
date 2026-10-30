using System;

namespace FirstPerson.Core
{
	public static class RainArt
	{
		private static SpriteImage[] _curtains;

		private static SpriteImage[] _snowVeils;

		public static SpriteImage SnowVeil(int variant)
		{
			if (_snowVeils == null)
			{
				SpriteImage[] array = new SpriteImage[4];
				for (int i = 0; i < 4; i++)
				{
					uint[] array2 = new uint[1280];
					for (int j = 0; j < 32; j++)
					{
						uint num = TerrainModel.Hash(j * 23 + i * 131, i * 5 + 11);
						float num2 = ((float)j + 0.5f) / 32f * 2f - 1f;
						float num3 = 1f - num2 * num2;
						num3 *= num3;
						float num4 = 0.02f + 0.3f * (1f - num3) + 0.18f * (float)((num >> 4) & 0xFF) / 255f;
						for (int k = 0; k < 40; k++)
						{
							float num5 = ((float)k + 0.5f) / 40f;
							if (!(num5 < num4))
							{
								float num6 = num3 * BlastFx.Smooth((num5 - num4) / 0.35f) * (0.35f + 0.65f * num5);
								uint num7 = TerrainModel.Hash(j * 7 + i * 3, k * 13 + i);
								float num8 = num6 * 0.8f + (((num7 & 0xFF) < 46) ? (num6 * 0.5f) : 0f);
								int num9 = Math.Min(255, (int)(num8 * 255f));
								if (num9 >= 8)
								{
									int num10 = (int)(236 + ((num7 >> 8) & 0xF));
									array2[k * 32 + j] = (uint)(num10 | (num10 << 8) | (Math.Min(255, num10 + 6) << 16) | (num9 << 24));
								}
							}
						}
					}
					SpriteImage spriteImage = new SpriteImage
					{
						Width = 32,
						Height = 40,
						Pixels = array2,
						PivotX = 0.5f,
						UnitWidth = 1f,
						UnitHeight = 1f
					};
					spriteImage.MeasureOpaqueRows();
					array[i] = spriteImage;
				}
				_snowVeils = array;
			}
			return _snowVeils[variant & 3];
		}

		public static SpriteImage Curtain(int variant)
		{
			if (_curtains == null)
			{
				SpriteImage[] array = new SpriteImage[4];
				for (int i = 0; i < 4; i++)
				{
					uint[] array2 = new uint[960];
					for (int j = 0; j < 24; j++)
					{
						uint num = TerrainModel.Hash(j * 17 + i * 101, i * 7 + 3);
						float num2 = ((float)j + 0.5f) / 24f * 2f - 1f;
						float num3 = 1f - num2 * num2;
						float num4 = 0.05f + 0.35f * (1f - num3) + 0.25f * (float)((num >> 4) & 0xFF) / 255f;
						float num5 = 0.55f + 0.45f * (float)((num >> 12) & 0xFF) / 255f;
						for (int k = 0; k < 40; k++)
						{
							float num6 = ((float)k + 0.5f) / 40f;
							if (!(num6 < num4))
							{
								float num7 = num3 * num5 * BlastFx.Smooth((num6 - num4) / 0.3f) * (0.55f + 0.45f * num6);
								uint num8 = TerrainModel.Hash(j * 3 + i, k / 3 + (int)(num & 7));
								num7 *= 0.7f + 0.3f * ((float)(num8 & 0xFF) / 255f);
								int num9 = Math.Min(255, (int)(num7 * 255f));
								if (num9 >= 8)
								{
									int num10 = (int)(214 + ((num8 >> 8) & 0x1F));
									array2[k * 24 + j] = (uint)(num10 | (num10 << 8) | (Math.Min(255, num10 + 10) << 16) | (num9 << 24));
								}
							}
						}
					}
					SpriteImage spriteImage = new SpriteImage
					{
						Width = 24,
						Height = 40,
						Pixels = array2,
						PivotX = 0.5f,
						UnitWidth = 1f,
						UnitHeight = 1f
					};
					spriteImage.MeasureOpaqueRows();
					array[i] = spriteImage;
				}
				_curtains = array;
			}
			return _curtains[variant & 3];
		}
	}
}
