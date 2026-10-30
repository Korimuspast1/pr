using System;

namespace FirstPerson.Core
{
	public sealed class VoxelShape
	{
		public int Width;

		public int Height;

		public int Depth;

		public byte[] Thickness;

		public uint[] Colors;

		public sbyte[] Offset;

		public byte[] Mirror;

		public uint[] Grid;

		public byte[] Speckle;

		public byte[] Glow;

		public byte[] Leaves;

		private byte[] _empty;

		public byte[] EmptyReach => _empty ?? (_empty = BuildEmptyReach());

		private byte[] BuildEmptyReach()
		{
			int width = Width;
			int height = Height;
			byte[] array = new byte[width * height];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = (byte)((Thickness[i] <= 0) ? byte.MaxValue : 0);
			}
			for (int j = 0; j < height; j++)
			{
				for (int k = 0; k < width; k++)
				{
					int num = j * width + k;
					int num2 = array[num];
					if (num2 == 0)
					{
						continue;
					}
					if (k > 0)
					{
						num2 = Math.Min(num2, array[num - 1] + 1);
					}
					if (j > 0)
					{
						num2 = Math.Min(num2, array[num - width] + 1);
						if (k > 0)
						{
							num2 = Math.Min(num2, array[num - width - 1] + 1);
						}
						if (k < width - 1)
						{
							num2 = Math.Min(num2, array[num - width + 1] + 1);
						}
					}
					array[num] = (byte)num2;
				}
			}
			for (int num3 = height - 1; num3 >= 0; num3--)
			{
				for (int num4 = width - 1; num4 >= 0; num4--)
				{
					int num5 = num3 * width + num4;
					int num6 = array[num5];
					if (num6 != 0)
					{
						if (num4 < width - 1)
						{
							num6 = Math.Min(num6, array[num5 + 1] + 1);
						}
						if (num3 < height - 1)
						{
							num6 = Math.Min(num6, array[num5 + width] + 1);
							if (num4 < width - 1)
							{
								num6 = Math.Min(num6, array[num5 + width + 1] + 1);
							}
							if (num4 > 0)
							{
								num6 = Math.Min(num6, array[num5 + width - 1] + 1);
							}
						}
						array[num5] = (byte)num6;
					}
				}
			}
			return array;
		}

		public static VoxelShape Leafy(SpriteImage img)
		{
			int width = img.Width;
			int height = img.Height;
			uint[] array = (uint[])img.Pixels.Clone();
			SpriteImage spriteImage = new SpriteImage
			{
				Width = width,
				Height = height,
				Pixels = array,
				PivotX = img.PivotX,
				UnitWidth = img.UnitWidth,
				UnitHeight = img.UnitHeight
			};
			spriteImage.MeasureOpaqueRows();
			int opaqueBottom = spriteImage.OpaqueBottom;
			int opaqueTop = spriteImage.OpaqueTop;
			int num = opaqueBottom + (opaqueTop - opaqueBottom) * 3 / 10;
			for (int i = opaqueBottom; i < num; i++)
			{
				for (int j = 0; j < width; j++)
				{
					if (array[i * width + j] >> 24 >= 128)
					{
						int num2 = j;
						for (; j + 1 < width && array[i * width + j + 1] >> 24 >= 128; j++)
						{
						}
						if (j - num2 + 1 >= 4)
						{
							array[i * width + num2] = 0u;
							array[i * width + j] = 0u;
						}
					}
				}
			}
			VoxelShape voxelShape = Inflate(spriteImage, 0.85f, 10);
			voxelShape.Speckle = new byte[width * height];
			voxelShape.Leaves = new byte[width * height];
			for (int k = 0; k < height; k++)
			{
				for (int l = 0; l < width; l++)
				{
					int num3 = k * width + l;
					if (array[num3] >> 24 >= 128)
					{
						voxelShape.Leaves[num3] = (byte)((k >= num) ? 1u : 2u);
					}
					uint num4 = TerrainModel.Hash(l >> 1, (k >> 1) + 1013);
					uint num5 = TerrainModel.Hash(l + 71, k * 3 + 5);
					float num6 = 1f + ((float)(num4 & 0xFF) / 255f - 0.5f) * 0.22f + ((float)(num5 & 0xFF) / 255f - 0.5f) * 0.12f;
					if (k >= num)
					{
						num6 *= 0.86f + 0.24f * (float)(k - num) / Math.Max(1f, opaqueTop - num);
					}
					voxelShape.Speckle[num3] = (byte)Math.Max(60, Math.Min(250, (int)(num6 * 128f)));
				}
			}
			return voxelShape;
		}

		public static SpriteImage Scale2x(SpriteImage img)
		{
			int width = img.Width;
			int height = img.Height;
			int num = width * 2;
			uint[] pixels = img.Pixels;
			uint[] array = new uint[width * height * 4];
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < width; j++)
				{
					uint num2 = pixels[i * width + j];
					uint c = ((i + 1 < height) ? pixels[(i + 1) * width + j] : 0u);
					uint c2 = ((i > 0) ? pixels[(i - 1) * width + j] : 0u);
					uint c3 = ((j > 0) ? pixels[i * width + j - 1] : 0u);
					uint c4 = ((j + 1 < width) ? pixels[i * width + j + 1] : 0u);
					c = Clear(c);
					c2 = Clear(c2);
					c3 = Clear(c3);
					c4 = Clear(c4);
					uint num3 = num2;
					uint num4 = num2;
					uint num5 = num2;
					uint num6 = num2;
					if (c3 == c && c3 != c2 && c != c4)
					{
						num3 = c;
					}
					if (c == c4 && c != c3 && c4 != c2)
					{
						num4 = c4;
					}
					if (c2 == c3 && c2 != c4 && c3 != c)
					{
						num5 = c3;
					}
					if (c4 == c2 && c4 != c && c2 != c3)
					{
						num6 = c2;
					}
					int num7 = i * 2 * num + j * 2;
					array[num7] = num5;
					array[num7 + 1] = num6;
					array[num7 + num] = num3;
					array[num7 + num + 1] = num4;
				}
			}
			return new SpriteImage
			{
				Width = num,
				Height = height * 2,
				Pixels = array,
				PivotX = img.PivotX,
				PivotY = img.PivotY,
				UnitWidth = img.UnitWidth,
				UnitHeight = img.UnitHeight
			};
		}

		private static uint Clear(uint c)
		{
			if (c >> 24 >= 128)
			{
				return c;
			}
			return 0u;
		}

		public static VoxelShape NewGrid(int width, int height, int depth)
		{
			return new VoxelShape
			{
				Width = width,
				Height = height,
				Depth = depth,
				Grid = new uint[width * height * depth * 2],
				Thickness = new byte[0],
				Colors = new uint[0]
			};
		}

		public void SetVoxel(int x, int y, int z, uint color)
		{
			if (x >= 0 && y >= 0 && x < Width && y < Height && z >= -Depth && z < Depth)
			{
				Grid[(y * Width + x) * Depth * 2 + z + Depth] = color | 0xFF000000u;
			}
		}

		public void Box(int x0, int y0, int z0, int x1, int y1, int z1, uint color)
		{
			for (int i = y0; i < y1; i++)
			{
				for (int j = x0; j < x1; j++)
				{
					for (int k = z0; k < z1; k++)
					{
						SetVoxel(j, i, k, color);
					}
				}
			}
		}

		public static VoxelShape Creature(SpriteImage img, float factor, int maxDepth)
		{
			VoxelShape voxelShape = Inflate(img, factor, maxDepth);
			int width = img.Width;
			int opaqueBottom = img.OpaqueBottom;
			int opaqueTop = img.OpaqueTop;
			int num = opaqueTop - opaqueBottom;
			if (num < 4)
			{
				return voxelShape;
			}
			int[] array = new int[img.Height];
			int num2 = 0;
			int num3 = width;
			int num4 = 0;
			for (int i = opaqueBottom; i < opaqueTop; i++)
			{
				for (int j = 0; j < width; j++)
				{
					if (voxelShape.Thickness[i * width + j] != 0)
					{
						array[i]++;
						if (j < num3)
						{
							num3 = j;
						}
						if (j > num4)
						{
							num4 = j;
						}
					}
				}
				if (array[i] > num2)
				{
					num2 = array[i];
				}
			}
			int k;
			for (k = opaqueBottom; k < opaqueTop && (float)array[k] < (float)num2 * 0.5f; k++)
			{
			}
			int num5 = k - opaqueBottom;
			if (num5 < 1 || (float)num5 > (float)num * 0.45f)
			{
				return voxelShape;
			}
			int num6 = 1;
			for (int l = k; l < opaqueTop; l++)
			{
				for (int m = 0; m < width; m++)
				{
					num6 = Math.Max(num6, voxelShape.Thickness[l * width + m]);
				}
			}
			bool flag = (float)(num4 - num3 + 1) > (float)num * 1.1f;
			int num7 = Math.Max(1, (int)Math.Round((float)num6 * 0.55f));
			int num8 = Math.Max(1, Math.Min(2, (int)Math.Round((float)num6 * 0.35f)));
			voxelShape.Offset = new sbyte[width * img.Height];
			voxelShape.Mirror = new byte[width * img.Height];
			for (int n = opaqueBottom; n < k; n++)
			{
				int num9 = 0;
				for (int num10 = 0; num10 < width; num10++)
				{
					int num11 = n * width + num10;
					if (voxelShape.Thickness[num11] == 0)
					{
						continue;
					}
					int num12 = num10;
					for (; num10 + 1 < width && voxelShape.Thickness[n * width + num10 + 1] > 0; num10++)
					{
					}
					int num13 = num10;
					for (int num14 = num12; num14 <= num13; num14++)
					{
						int num15 = n * width + num14;
						voxelShape.Thickness[num15] = (byte)num8;
						if (flag)
						{
							voxelShape.Offset[num15] = (sbyte)num7;
							voxelShape.Mirror[num15] = 1;
						}
						else
						{
							int num16 = ((num13 <= num12 || num9 != 0 || !IsOnlyRun(voxelShape.Thickness, width, n, num13)) ? ((num9 % 2 == 0) ? 1 : (-1)) : ((num14 <= (num12 + num13) / 2) ? 1 : (-1)));
							voxelShape.Offset[num15] = (sbyte)(num16 * num7);
						}
					}
					num9++;
				}
			}
			voxelShape.Depth = Math.Max(voxelShape.Depth, num7 + num8);
			return voxelShape;
		}

		private static bool IsOnlyRun(byte[] thickness, int w, int y, int end)
		{
			for (int i = end + 1; i < w; i++)
			{
				if (thickness[y * w + i] > 0)
				{
					return false;
				}
			}
			return true;
		}

		public static VoxelShape Inflate(SpriteImage img, float factor, int maxDepth)
		{
			int width = img.Width;
			int height = img.Height;
			int[] array = new int[width * height];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = ((img.Pixels[i] >> 24 >= 128) ? 1048576 : 0);
			}
			for (int j = 0; j < height; j++)
			{
				for (int k = 0; k < width; k++)
				{
					int num = j * width + k;
					if (array[num] != 0)
					{
						int val = array[num];
						val = Math.Min(val, (k > 0) ? (array[num - 1] + 3) : 3);
						val = Math.Min(val, (j > 0) ? (array[num - width] + 3) : 3);
						val = Math.Min(val, (k > 0 && j > 0) ? (array[num - width - 1] + 4) : 4);
						val = Math.Min(val, (k < width - 1 && j > 0) ? (array[num - width + 1] + 4) : 4);
						array[num] = val;
					}
				}
			}
			for (int num2 = height - 1; num2 >= 0; num2--)
			{
				for (int num3 = width - 1; num3 >= 0; num3--)
				{
					int num4 = num2 * width + num3;
					if (array[num4] != 0)
					{
						int val2 = array[num4];
						val2 = Math.Min(val2, (num3 < width - 1) ? (array[num4 + 1] + 3) : 3);
						val2 = Math.Min(val2, (num2 < height - 1) ? (array[num4 + width] + 3) : 3);
						val2 = Math.Min(val2, (num3 < width - 1 && num2 < height - 1) ? (array[num4 + width + 1] + 4) : 4);
						val2 = Math.Min(val2, (num3 > 0 && num2 < height - 1) ? (array[num4 + width - 1] + 4) : 4);
						array[num4] = val2;
					}
				}
			}
			VoxelShape voxelShape = new VoxelShape
			{
				Width = width,
				Height = height,
				Thickness = new byte[width * height],
				Colors = img.Pixels
			};
			for (int l = 0; l < array.Length; l++)
			{
				if (array[l] != 0)
				{
					int val3 = (int)Math.Round((float)array[l] / 3f * factor);
					val3 = Math.Max(1, Math.Min(maxDepth, val3));
					voxelShape.Thickness[l] = (byte)val3;
					if (val3 > voxelShape.Depth)
					{
						voxelShape.Depth = val3;
					}
				}
			}
			return voxelShape;
		}

		public static VoxelShape House(SpriteImage img, int halfDepth, float roofShare)
		{
			halfDepth = Math.Max(1, Math.Min(255, halfDepth));
			VoxelShape voxelShape = new VoxelShape
			{
				Width = img.Width,
				Height = img.Height,
				Depth = halfDepth,
				Thickness = new byte[img.Width * img.Height],
				Colors = img.Pixels
			};
			int opaqueBottom = img.OpaqueBottom;
			int num = Math.Max(opaqueBottom + 1, img.OpaqueTop);
			int num2 = num - Math.Max(1, (int)Math.Round((float)(num - opaqueBottom) * roofShare));
			for (int i = 0; i < img.Height; i++)
			{
				int num3 = halfDepth;
				if (i >= num2)
				{
					float num4 = ((float)(i - num2) + 1f) / (float)Math.Max(1, num - num2);
					num3 = Math.Max(1, (int)Math.Round((float)halfDepth * (1f - num4)));
				}
				for (int j = 0; j < img.Width; j++)
				{
					int num5 = i * img.Width + j;
					if (img.Pixels[num5] >> 24 >= 128)
					{
						voxelShape.Thickness[num5] = (byte)num3;
					}
				}
			}
			return voxelShape;
		}

		public static VoxelShape Furniture(SpriteImage img, int halfDepth, int legRows)
		{
			VoxelShape voxelShape = Slab(img, halfDepth);
			if (legRows <= 0)
			{
				return voxelShape;
			}
			voxelShape.Offset = new sbyte[img.Width * img.Height];
			voxelShape.Mirror = new byte[img.Width * img.Height];
			for (int i = 0; i < Math.Min(legRows, img.Height); i++)
			{
				for (int j = 0; j < img.Width; j++)
				{
					int num = i * img.Width + j;
					if (voxelShape.Thickness[num] != 0)
					{
						voxelShape.Thickness[num] = 1;
						voxelShape.Offset[num] = (sbyte)Math.Max(1, halfDepth - 1);
						voxelShape.Mirror[num] = 1;
					}
				}
			}
			return voxelShape;
		}

		public static VoxelShape Slab(SpriteImage img, int halfThickness)
		{
			halfThickness = Math.Max(1, Math.Min(255, halfThickness));
			VoxelShape voxelShape = new VoxelShape
			{
				Width = img.Width,
				Height = img.Height,
				Depth = halfThickness,
				Thickness = new byte[img.Width * img.Height],
				Colors = img.Pixels
			};
			for (int i = 0; i < voxelShape.Thickness.Length; i++)
			{
				if (img.Pixels[i] >> 24 >= 128)
				{
					voxelShape.Thickness[i] = (byte)halfThickness;
				}
			}
			return voxelShape;
		}
	}
}
