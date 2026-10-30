using System;

namespace FirstPerson.Core
{
	public static class ZombieColors
	{
		public struct Living
		{
			public uint Shirt;

			public uint Pants;

			public uint Shoes;

			public uint Hair;

			public uint Accent;

			public bool LongHair;

			public bool Beard;

			public bool Barefoot;

			public bool Known;
		}

		private static readonly uint Grime = TerrainCache.Rgb(86, 78, 58);

		private static readonly uint[] Shirts = new uint[8]
		{
			TerrainCache.Rgb(158, 66, 52),
			TerrainCache.Rgb(72, 92, 140),
			TerrainCache.Rgb(150, 128, 78),
			TerrainCache.Rgb(94, 118, 72),
			TerrainCache.Rgb(128, 96, 72),
			TerrainCache.Rgb(170, 160, 136),
			TerrainCache.Rgb(104, 72, 112),
			TerrainCache.Rgb(64, 110, 104)
		};

		private static readonly uint[] Pants = new uint[5]
		{
			TerrainCache.Rgb(70, 72, 110),
			TerrainCache.Rgb(92, 80, 60),
			TerrainCache.Rgb(64, 62, 60),
			TerrainCache.Rgb(110, 94, 70),
			TerrainCache.Rgb(76, 96, 70)
		};

		private static readonly uint[] Hairs = new uint[5]
		{
			TerrainCache.Rgb(92, 56, 32),
			TerrainCache.Rgb(40, 32, 28),
			TerrainCache.Rgb(188, 160, 92),
			TerrainCache.Rgb(140, 72, 40),
			TerrainCache.Rgb(128, 124, 120)
		};

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

		public static uint Rot(uint c)
		{
			int num = (R(c) * 30 + G(c) * 59 + B(c) * 11) / 100;
			uint b = TerrainCache.Rgb(num, num, num);
			return TerrainCache.Scale(TerrainCache.Lerp(TerrainCache.Lerp(c, b, 0.3f), Grime, 0.15f), 0.92f);
		}

		private static uint Dull(uint c)
		{
			int num = (R(c) * 30 + G(c) * 59 + B(c) * 11) / 100;
			return TerrainCache.Scale(TerrainCache.Lerp(c, TerrainCache.Rgb(num, num, num), 0.45f), 0.8f);
		}

		public static string Write(Humanoid.Palette p)
		{
			int num = (int)((p.LongHair ? 1u : 0u) | (uint)(p.Beard ? 2 : 0)) | (Near(p.Shoes, p.Skin) ? 4 : 0);
			return $"{p.Shirt & 0xFFFFFF:x6},{p.Pants & 0xFFFFFF:x6},{p.Shoes & 0xFFFFFF:x6},{p.Hair & 0xFFFFFF:x6},{p.Accent & 0xFFFFFF:x6},{num}";
		}

		public static Living Read(string s)
		{
			Living result = default(Living);
			if (string.IsNullOrEmpty(s))
			{
				return result;
			}
			string[] array = s.Split(',');
			if (array.Length < 6)
			{
				return result;
			}
			try
			{
				result.Shirt = H(array[0]);
				result.Pants = H(array[1]);
				result.Shoes = H(array[2]);
				result.Hair = H(array[3]);
				result.Accent = H(array[4]);
				int num = int.Parse(array[5]);
				result.LongHair = (num & 1) != 0;
				result.Beard = (num & 2) != 0;
				result.Barefoot = (num & 4) != 0;
				result.Known = true;
			}
			catch
			{
				result = default(Living);
			}
			return result;
			uint H(string x)
			{
				return Convert.ToUInt32(x, 16);
			}
		}

		public static Humanoid.Palette Of(Humanoid.Palette sprite, string species, Living living, int seed)
		{
			Humanoid.Palette result = sprite;
			uint num = (uint)(seed * -1640531535);
			if (living.Known)
			{
				result.Shirt = Rot(living.Shirt);
				result.Pants = Rot(living.Pants);
				result.Shoes = (living.Barefoot ? result.Skin : Rot(living.Shoes));
				result.Hair = Dull(living.Hair);
				result.Accent = ((living.Accent != 0) ? Rot(living.Accent) : 0u);
				result.LongHair = living.LongHair;
				result.Beard = living.Beard;
			}
			else
			{
				result.Shirt = Rot(Shirts[(num >> 3) % (uint)Shirts.Length]);
				result.Pants = Rot(Pants[(num >> 9) % (uint)Pants.Length]);
				result.Shoes = TerrainCache.Rgb(58, 44, 34);
				result.Hair = Dull(Hairs[(num >> 15) % (uint)Hairs.Length]);
				result.Accent = 0u;
				result.LongHair = (num >> 21) % 5 < 2;
				result.Beard = (num >> 24) % 2 == 0;
			}
			if (Near(result.Shirt, result.Skin))
			{
				result.Shirt = TerrainCache.Scale(result.Shirt, 0.7f);
			}
			if (Near(result.Pants, result.Skin))
			{
				result.Pants = TerrainCache.Scale(result.Pants, 0.7f);
			}
			return result;
		}

		private static bool Near(uint a, uint b)
		{
			return Math.Abs(R(a) - R(b)) + Math.Abs(G(a) - G(b)) + Math.Abs(B(a) - B(b)) < 40;
		}
	}
}
