using System;

namespace FirstPerson.Core
{
	public sealed class BiomeMap
	{
		public int Width;

		public int Height;

		public byte[] Kinds = new byte[0];

		public const int Reach = 44;

		private int _row;

		private int _cx = int.MinValue;

		private int _cy = int.MinValue;

		public const int KindCount = 28;

		public void Allocate(int w, int h)
		{
			if (w != Width || h != Height)
			{
				Width = w;
				Height = h;
				Kinds = new byte[w * h];
				_cx = (_cy = int.MinValue);
			}
		}

		public BiomeKind At(float x, float y)
		{
			int num = (int)x;
			int num2 = (int)y;
			if (num < 0 || num2 < 0 || num >= Width || num2 >= Height)
			{
				return BiomeKind.None;
			}
			return (BiomeKind)Kinds[num2 * Width + num];
		}

		public void Fill(BiomeKind k)
		{
			for (int i = 0; i < Kinds.Length; i++)
			{
				Kinds[i] = (byte)k;
			}
		}

		public void Sweep(float eyeX, float eyeY, Func<int, int, BiomeKind> read, int rows)
		{
			if (Width == 0 || read == null || float.IsNaN(eyeX) || float.IsNaN(eyeY))
			{
				return;
			}
			int num = (int)eyeX;
			int num2 = (int)eyeY;
			if (_cx == int.MinValue || Math.Abs((long)num - (long)_cx) > 22 || Math.Abs((long)num2 - (long)_cy) > 22)
			{
				_cx = num;
				_cy = num2;
				rows = 89;
				_row = 0;
			}
			else if (Math.Abs(num - _cx) > 6 || Math.Abs(num2 - _cy) > 6)
			{
				_cx = num;
				_cy = num2;
			}
			int num3 = Math.Max(0, _cx - 44);
			int num4 = Math.Min(Width - 1, _cx + 44);
			for (int i = 0; i < rows; i++)
			{
				int num5 = _cy - 44 + _row;
				_row = (_row + 1) % 89;
				if (num5 >= 0 && num5 < Height)
				{
					int num6 = num5 * Width;
					for (int j = num3; j <= num4; j++)
					{
						Kinds[num6 + j] = (byte)read(j, num5);
					}
				}
			}
		}

		public void Shares(float eyeX, float eyeY, float radius, float[] near, float[] area)
		{
			Array.Clear(near, 0, near.Length);
			Array.Clear(area, 0, area.Length);
			if (Width == 0)
			{
				return;
			}
			int num = (int)eyeX;
			int num2 = (int)eyeY;
			int num3 = (int)radius;
			float num4 = 0f;
			float num5 = 0f;
			for (int i = -num3; i <= num3; i += 2)
			{
				for (int j = -num3; j <= num3; j += 2)
				{
					float num6 = j * j + i * i;
					if (num6 > radius * radius)
					{
						continue;
					}
					float num7 = 1f - num6 / (radius * radius);
					float num8 = num7 * num7 * (0.35f + 0.65f * num7);
					num4 += num8;
					num5 += 1f;
					int num9 = num + j;
					int num10 = num2 + i;
					if (num9 >= 0 && num10 >= 0 && num9 < Width && num10 < Height)
					{
						int num11 = Kinds[num10 * Width + num9];
						if (num11 != 0)
						{
							near[num11] += num8;
							area[num11] += 1f;
						}
					}
				}
			}
			if (num4 > 0f)
			{
				for (int k = 0; k < near.Length; k++)
				{
					near[k] /= num4;
				}
			}
			if (num5 > 0f)
			{
				for (int l = 0; l < area.Length; l++)
				{
					area[l] /= num5;
				}
			}
		}

		public static BiomeKind FromTileId(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return BiomeKind.None;
			}
			string text = id;
			if (text.EndsWith("_low", StringComparison.Ordinal))
			{
				text = text.Substring(0, text.Length - 4);
			}
			else if (text.EndsWith("_high", StringComparison.Ordinal))
			{
				text = text.Substring(0, text.Length - 5);
			}
			switch (text)
			{
			case "grass":
				return BiomeKind.Grass;
			case "savanna":
				return BiomeKind.Savanna;
			case "desert":
				return BiomeKind.Desert;
			case "jungle":
				return BiomeKind.Jungle;
			case "swamp":
				return BiomeKind.Swamp;
			case "permafrost":
				return BiomeKind.Permafrost;
			case "birch":
				return BiomeKind.Birch;
			case "maple":
			case "mapple":
				return BiomeKind.Maple;
			case "rocklands":
				return BiomeKind.Rocklands;
			case "clover":
				return BiomeKind.Clover;
			case "flower":
				return BiomeKind.Flower;
			case "lemon":
				return BiomeKind.Lemon;
			case "garlic":
				return BiomeKind.Garlic;
			case "candy":
				return BiomeKind.Candy;
			case "mushroom":
				return BiomeKind.Mushroom;
			case "crystal":
				return BiomeKind.Crystal;
			case "enchanted":
				return BiomeKind.Enchanted;
			case "corrupted":
				return BiomeKind.Corrupted;
			case "infernal":
				return BiomeKind.Infernal;
			case "wasteland":
				return BiomeKind.Wasteland;
			case "celestial":
				return BiomeKind.Celestial;
			case "singularity":
				return BiomeKind.Singularity;
			case "paradox":
				return BiomeKind.Paradox;
			case "tumor":
				return BiomeKind.Tumor;
			case "biomass":
				return BiomeKind.Biomass;
			case "pumpkin":
				return BiomeKind.Pumpkin;
			case "cybertile":
				return BiomeKind.Cybertile;
			default:
				return BiomeKind.None;
			}
		}
	}
}
