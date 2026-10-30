using System;

namespace FirstPerson.Core
{
	public sealed class PixelCanvas
	{
		public readonly int Width;

		public readonly int Height;

		private readonly uint[] _px;

		public PixelCanvas(int width, int height)
		{
			Width = width;
			Height = height;
			_px = new uint[width * height];
		}

		public void Set(int x, int y, uint c)
		{
			if (x >= 0 && y >= 0 && x < Width && y < Height)
			{
				_px[y * Width + x] = c | 0xFF000000u;
			}
		}

		public void Rect(int x, int y, int w, int h, uint c)
		{
			for (int i = y; i < y + h; i++)
			{
				for (int j = x; j < x + w; j++)
				{
					Set(j, i, c);
				}
			}
		}

		public void Noisy(int x, int y, int w, int h, uint c, float amount, int seed)
		{
			for (int i = y; i < y + h; i++)
			{
				for (int j = x; j < x + w; j++)
				{
					uint num = TerrainModel.Hash(j + seed * 131, i - seed * 71);
					Set(j, i, TerrainCache.Scale(c, 1f + ((float)(num & 0xFF) / 255f - 0.5f) * amount));
				}
			}
		}

		public SpriteImage ToImage(float unitWidth, float unitHeight)
		{
			uint[] array = new uint[Width * Height];
			for (int i = 0; i < Height; i++)
			{
				Array.Copy(_px, i * Width, array, (Height - 1 - i) * Width, Width);
			}
			SpriteImage spriteImage = new SpriteImage();
			spriteImage.Width = Width;
			spriteImage.Height = Height;
			spriteImage.Pixels = array;
			spriteImage.UnitWidth = unitWidth;
			spriteImage.UnitHeight = unitHeight;
			spriteImage.MeasureOpaqueRows();
			return spriteImage;
		}
	}
}
