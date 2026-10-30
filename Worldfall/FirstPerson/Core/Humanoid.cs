using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class Humanoid
	{
		public struct Palette
		{
			public uint Skin;

			public uint Hair;

			public uint Eyes;

			public uint Shirt;

			public uint Pants;

			public uint Shoes;

			public uint Accent;

			public bool LongHair;

			public bool Beard;

			public bool SkinSeen;
		}

		public const int GridWidth = 8;

		public const int GridHeight = 14;

		public const int ChildGridHeight = 11;

		public const int GridDepth = 2;

		public static Palette Extract(SpriteImage img)
		{
			return Extract(img, null);
		}

		public static Palette Extract(SpriteImage img, string species, uint knownSkin = 0u)
		{
			int opaqueBottom = img.OpaqueBottom;
			int num = Math.Max(img.OpaqueBottom + 1, img.OpaqueTop);
			int num2 = num - opaqueBottom;
			int num3 = opaqueBottom + num2 * 55 / 100;
			int num4 = opaqueBottom + num2 * 28 / 100;
			Palette result = default(Palette);
			uint skin;
			if (knownSkin != 0)
			{
				result.SkinSeen = true;
				if (!MostCommon(img, opaqueBottom, num, (uint c) => IsShadeOf(c, knownSkin), out skin))
				{
					skin = knownSkin;
				}
			}
			else
			{
				uint plain;
				bool allOver;
				Func<uint, bool> accept = SkinTest(species, out plain, out allOver);
				result.SkinSeen = MostCommon(img, allOver ? opaqueBottom : num3, num, accept, out skin, species?.StartsWith("zombie", StringComparison.Ordinal) ?? false);
				if (!result.SkinSeen && plain != 0)
				{
					skin = plain;
				}
				else if (!result.SkinSeen && !MostCommon(img, num3, num, (uint c) => Luma(c) > 70, out skin))
				{
					skin = TerrainCache.Rgb(232, 184, 140);
				}
			}
			result.Skin = ((knownSkin != 0) ? knownSkin : skin);
			result.Eyes = Darkest(img, num3, num, TerrainCache.Rgb(35, 28, 28));
			if (Luma(result.Eyes) > 110)
			{
				result.Eyes = TerrainCache.Rgb(35, 28, 28);
			}
			int y = num3 + (num - num3) * 45 / 100;
			uint eyes = result.Eyes;
			if (!MostCommon(img, y, num, (uint c) => Dist(c, skin) > 40 && Dist(c, eyes) > 20, out result.Hair))
			{
				result.Hair = TerrainCache.Scale(skin, 0.55f);
			}
			if (!MostCommon(img, num4, num3, (uint c) => Dist(c, skin) > 40 && Luma(c) > 35, out result.Shirt))
			{
				result.Shirt = TerrainCache.Rgb(150, 90, 60);
			}
			result.Shoes = Darkest(img, opaqueBottom, opaqueBottom + 1, TerrainCache.Scale(result.Shirt, 0.4f));
			uint shoes = result.Shoes;
			if (!MostCommon(img, opaqueBottom + 1, Math.Max(opaqueBottom + 2, num4 + 1), (uint c) => Dist(c, skin) > 40 && Dist(c, shoes) > 30 && Luma(c) > 25, out result.Pants))
			{
				result.Pants = TerrainCache.Scale(result.Shirt, 0.7f);
			}
			if (Dist(result.Shoes, result.Pants) < 20)
			{
				result.Shoes = TerrainCache.Scale(result.Pants, 0.6f);
			}
			result.Accent = StandOut(img, skin, result.Shirt);
			if (Dist(result.Hair, skin) > 40)
			{
				uint hair = result.Hair;
				result.LongHair = Count(img, num4 + (num3 - num4) / 2, num3, 0, img.Width, (uint c) => Dist(c, hair) < 24) >= 2;
				result.Beard = Count(img, num3, num3 + (num - num3) / 3, 0, img.Width / 2, (uint c) => Dist(c, hair) < 24) >= 2;
			}
			return result;
		}

		public static VoxelShape Build(Palette p, int frame, bool child = false)
		{
			int num = (child ? 2 : 4);
			int num2 = (child ? 5 : 8);
			int num3 = num2;
			int num4 = ((!child) ? 1 : 2);
			int num5 = (child ? 6 : 7);
			VoxelShape voxelShape = VoxelShape.NewGrid(8, num3 + 6, 2);
			int num6;
			switch (frame)
			{
			default:
				num6 = 0;
				break;
			case 2:
				num6 = -1;
				break;
			case 1:
				num6 = 1;
				break;
			}
			int num7 = num6;
			uint dark = TerrainCache.Scale(p.Pants, 0.85f);
			Leg(voxelShape, (!child) ? 1 : 2, num, -num7, p, dark);
			Leg(voxelShape, child ? 4 : 5, num, num7, p, dark);
			voxelShape.Box(num4, num, -1, num5, num2, 1, p.Shirt);
			voxelShape.Box(num4, num, -1, num5, num + 1, 1, TerrainCache.Scale(p.Shirt, 0.72f));
			Arm(voxelShape, num4 - 1, num, num2, num7, p);
			Arm(voxelShape, num5, num, num2, -num7, p);
			voxelShape.Box(1, num3, -2, 7, num3 + 6, 2, p.Skin);
			voxelShape.Box(1, num3 + 5, -2, 7, num3 + 6, 2, p.Hair);
			voxelShape.Box(1, num3 + 1, 1, 7, num3 + 5, 2, p.Hair);
			voxelShape.Box(1, num3 + 2, -1, 2, num3 + 5, 1, p.Hair);
			voxelShape.Box(6, num3 + 2, -1, 7, num3 + 5, 1, p.Hair);
			voxelShape.Box(1, num3 + 4, -2, 7, num3 + 5, -1, p.Hair);
			voxelShape.SetVoxel(2, num3 + 2, -2, p.Eyes);
			voxelShape.SetVoxel(5, num3 + 2, -2, p.Eyes);
			voxelShape.SetVoxel(2, num3 + 3, -2, TerrainCache.Scale(p.Hair, 0.8f));
			voxelShape.SetVoxel(5, num3 + 3, -2, TerrainCache.Scale(p.Hair, 0.8f));
			if (p.Beard && !child)
			{
				voxelShape.Box(1, num3, -2, 7, num3 + 1, 0, p.Hair);
				voxelShape.Box(2, num3 - 1, -2, 6, num3, -1, p.Hair);
			}
			else
			{
				voxelShape.SetVoxel(3, num3, -2, TerrainCache.Scale(p.Skin, 0.78f));
				voxelShape.SetVoxel(4, num3, -2, TerrainCache.Scale(p.Skin, 0.78f));
			}
			if (child)
			{
				uint color = TerrainCache.Lerp(p.Skin, TerrainCache.Rgb(236, 120, 120), 0.35f);
				voxelShape.SetVoxel(1, num3 + 1, -2, color);
				voxelShape.SetVoxel(6, num3 + 1, -2, color);
			}
			if (p.LongHair)
			{
				voxelShape.Box(1, Math.Max(num, num3 - 2), 1, 7, num3 + 1, 2, p.Hair);
			}
			return voxelShape;
		}

		private static void Leg(VoxelShape s, int x, int top, int step, Palette p, uint dark)
		{
			int num = -1 + step;
			s.Box(x, 1, num, x + 2, top, num + 2, p.Pants);
			s.Box(x, top - 1, num, x + 2, top, num + 2, dark);
			s.Box(x, 0, num, x + 2, 1, num + 2, p.Shoes);
			s.SetVoxel(x, 0, num - 1, p.Shoes);
			s.SetVoxel(x + 1, 0, num - 1, p.Shoes);
		}

		private static void Arm(VoxelShape s, int x, int bottom, int top, int step, Palette p)
		{
			int num = -1 + step;
			s.Box(x, bottom + 1, num, x + 1, top, num + 2, p.Shirt);
			s.Box(x, bottom, num, x + 1, bottom + 1, num + 2, p.Skin);
		}

		private static int Luma(uint c)
		{
			return (int)((c & 0xFF) * 3 + ((c >> 8) & 0xFF) * 6 + ((c >> 16) & 0xFF)) / 10;
		}

		private static int Dist(uint a, uint b)
		{
			return Math.Abs((int)((a & 0xFF) - (b & 0xFF))) + Math.Abs((int)(((a >> 8) & 0xFF) - ((b >> 8) & 0xFF))) + Math.Abs((int)(((a >> 16) & 0xFF) - ((b >> 16) & 0xFF)));
		}

		private static bool IsShadeOf(uint c, uint skin)
		{
			for (int num = 10; num >= 7; num--)
			{
				if (Dist(c, TerrainCache.Scale(skin, (float)num * 0.1f)) <= 18)
				{
					return true;
				}
			}
			return false;
		}

		private static bool SkinLike(uint c)
		{
			int num = (int)(c & 0xFF);
			int num2 = (int)((c >> 8) & 0xFF);
			int num3 = (int)((c >> 16) & 0xFF);
			if (num > 110 && num >= num2 && num2 >= num3 - 10 && num - num3 > 25 && num2 * 100 >= num * 45)
			{
				return num3 * 100 >= num2 * 35;
			}
			return false;
		}

		private static Func<uint, bool> SkinTest(string species, out uint plain, out bool allOver)
		{
			plain = 0u;
			allOver = false;
			species = species ?? "";
			if (species.StartsWith("zombie", StringComparison.Ordinal))
			{
				allOver = true;
				plain = TerrainCache.Rgb(150, 196, 96);
				return delegate(uint c)
				{
					int num = Math.Max(R(c), Math.Max(G(c), B(c)));
					int num2 = Math.Min(R(c), Math.Min(G(c), B(c)));
					bool flag = R(c) > G(c) + 40 && R(c) > B(c) + 25;
					return num - num2 >= 24 && !flag && Luma(c) > 50;
				};
			}
			switch (species)
			{
			case "orc":
			case "alien":
				allOver = true;
				plain = ((species == "orc") ? TerrainCache.Rgb(104, 132, 76) : TerrainCache.Rgb(150, 196, 96));
				return (uint c) => G(c) > R(c) + 8 && G(c) > B(c) + 8 && Luma(c) > 60;
			case "cold_one":
				allOver = true;
				plain = TerrainCache.Rgb(120, 180, 236);
				return (uint c) => B(c) > R(c) + 40 && B(c) >= G(c) && Luma(c) > 70;
			case "demon":
				allOver = true;
				plain = TerrainCache.Rgb(150, 44, 44);
				return (uint c) => R(c) > G(c) + 40 && R(c) > B(c) + 25;
			case "dwarf":
				plain = TerrainCache.Rgb(226, 172, 130);
				return (uint c) => SkinLike(c) && Luma(c) > 150;
			case "bandit":
				plain = TerrainCache.Rgb(214, 164, 122);
				break;
			}
			switch (species)
			{
			case "white_mage":
			case "evil_mage":
			case "druid":
			case "necromancer":
			case "plague_doctor":
				plain = TerrainCache.Rgb(222, 180, 144);
				break;
			}
			return SkinLike;
		}

		private static int R(uint c)
		{
			return (int)(c & 0xFF);
		}

		private static int G(uint c)
		{
			return (int)((c >> 8) & 0xFF);
		}

		private static int B(uint c)
		{
			return (int)((c >> 16) & 0xFF);
		}

		private static uint StandOut(SpriteImage img, uint skin, uint shirt)
		{
			MostCommon(img, img.OpaqueBottom, img.OpaqueTop, delegate(uint c)
			{
				int num = Math.Max(R(c), Math.Max(G(c), B(c)));
				int num2 = Math.Min(R(c), Math.Min(G(c), B(c)));
				return num > 90 && (float)(num - num2) > (float)num * 0.45f && Dist(c, skin) > 90 && Dist(c, shirt) > 90;
			}, out var result);
			return result;
		}

		private static bool MostCommon(SpriteImage img, int y0, int y1, Func<uint, bool> accept, out uint result, bool lighterOnTie)
		{
			if (!lighterOnTie)
			{
				return MostCommon(img, y0, y1, accept, out result);
			}
			Dictionary<uint, int> dictionary = new Dictionary<uint, int>();
			for (int i = Math.Max(0, y0); i < Math.Min(img.Height, y1); i++)
			{
				for (int j = 0; j < img.Width; j++)
				{
					uint num = img.Pixels[i * img.Width + j];
					if (num >> 24 >= 128)
					{
						num |= 0xFF000000u;
						if (accept(num))
						{
							dictionary.TryGetValue(num, out var value);
							dictionary[num] = value + 1;
						}
					}
				}
			}
			result = 0u;
			int num2 = 0;
			foreach (KeyValuePair<uint, int> item in dictionary)
			{
				if (item.Value > num2 || (item.Value == num2 && Luma(item.Key) > Luma(result)))
				{
					num2 = item.Value;
					result = item.Key;
				}
			}
			return num2 > 0;
		}

		private static bool MostCommon(SpriteImage img, int y0, int y1, Func<uint, bool> accept, out uint result)
		{
			Dictionary<uint, int> dictionary = new Dictionary<uint, int>();
			for (int i = Math.Max(0, y0); i < Math.Min(img.Height, y1); i++)
			{
				for (int j = 0; j < img.Width; j++)
				{
					uint num = img.Pixels[i * img.Width + j];
					if (num >> 24 >= 128)
					{
						num |= 0xFF000000u;
						if (accept(num))
						{
							dictionary.TryGetValue(num, out var value);
							dictionary[num] = value + 1;
						}
					}
				}
			}
			result = 0u;
			int num2 = 0;
			foreach (KeyValuePair<uint, int> item in dictionary)
			{
				if (item.Value > num2)
				{
					num2 = item.Value;
					result = item.Key;
				}
			}
			return num2 > 0;
		}

		private static int Count(SpriteImage img, int y0, int y1, int x0, int x1, Func<uint, bool> accept)
		{
			int num = 0;
			for (int i = Math.Max(0, y0); i < Math.Min(img.Height, y1); i++)
			{
				for (int j = Math.Max(0, x0); j < Math.Min(img.Width, x1); j++)
				{
					uint num2 = img.Pixels[i * img.Width + j];
					if (num2 >> 24 >= 128 && accept(num2 | 0xFF000000u))
					{
						num++;
					}
				}
			}
			return num;
		}

		private static uint Darkest(SpriteImage img, int y0, int y1, uint fallback)
		{
			uint result = fallback;
			int num = int.MaxValue;
			for (int i = Math.Max(0, y0); i < Math.Min(img.Height, y1); i++)
			{
				for (int j = 0; j < img.Width; j++)
				{
					uint num2 = img.Pixels[i * img.Width + j];
					if (num2 >> 24 >= 128)
					{
						int num3 = Luma(num2);
						if (num3 < num)
						{
							num = num3;
							result = num2 | 0xFF000000u;
						}
					}
				}
			}
			return result;
		}
	}
}
