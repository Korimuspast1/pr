using System;

namespace FirstPerson.Core
{
	public struct RegaliaColors
	{
		public uint Main;

		public uint Second;

		public uint Emblem;

		public const float MinLuma = 0.07f;

		public const float MinApart = 60f;

		public const float StripeApart = 25f;

		private static readonly uint Cream = TerrainCache.Rgb(240, 236, 226);

		public uint StripeColor
		{
			get
			{
				if (!(DeltaE(Second, Main) < 25f))
				{
					return Second;
				}
				return RegaliaKit.StripeLight;
			}
		}

		public static RegaliaColors From(uint main, uint second, uint banner)
		{
			main |= 0xFF000000u;
			second |= 0xFF000000u;
			banner |= 0xFF000000u;
			float num = Luma(main);
			if (num < 0.07f)
			{
				int num2 = (int)Math.Ceiling((0.07f - num) * 255f);
				main = TerrainCache.Add(main, num2, num2, num2);
			}
			if (Apart(second, main) < 60f)
			{
				second = banner;
				if (Apart(second, main) < 60f)
				{
					second = ((Luma(main) >= 0.5f) ? TerrainCache.Scale(main, 0.4f) : TerrainCache.Lerp(main, Cream, 0.7f));
				}
			}
			return new RegaliaColors
			{
				Main = main,
				Second = second,
				Emblem = banner
			};
		}

		public uint Slot(string slot, SashGear sash, uint helm)
		{
			switch (slot)
			{
			case "banner":
			case "gem":
				return Main;
			case "banner_dark":
				return TerrainCache.Scale(Main, 0.62f);
			case "banner_2":
				return Second;
			case "emblem":
				return Emblem;
			case "sash":
				if (sash != SashGear.Gold)
				{
					return Main;
				}
				return RegaliaKit.SashGold;
			case "sash_dark":
				return TerrainCache.Scale((sash == SashGear.Gold) ? RegaliaKit.SashGold : Main, 0.62f);
			case "stripe":
				switch (sash)
				{
				default:
					return Main;
				case SashGear.Striped:
					return StripeColor;
				case SashGear.Gold:
					return RegaliaKit.SashGold;
				}
			case "edge":
				switch (sash)
				{
				default:
					return TerrainCache.Scale(Main, 0.62f);
				case SashGear.GoldEdged:
					return RegaliaKit.Gold;
				case SashGear.Gold:
					return RegaliaKit.GoldDark;
				}
			case "helm":
				if (helm == 0)
				{
					return TerrainCache.Rgb(86, 87, 90);
				}
				return helm | 0xFF000000u;
			default:
				return 0u;
			}
		}

		public static float Luma(uint c)
		{
			return (0.2126f * (float)(c & 0xFF) + 0.7152f * (float)((c >> 8) & 0xFF) + 0.0722f * (float)((c >> 16) & 0xFF)) / 255f;
		}

		public static float Apart(uint a, uint b)
		{
			float num = (int)((a & 0xFF) - (b & 0xFF));
			float num2 = (int)(((a >> 8) & 0xFF) - ((b >> 8) & 0xFF));
			float num3 = (int)(((a >> 16) & 0xFF) - ((b >> 16) & 0xFF));
			return (float)Math.Sqrt(num * num + num2 * num2 + num3 * num3);
		}

		public static float DeltaE(uint a, uint b)
		{
			Lab(a, out var l, out var a2, out var b2);
			Lab(b, out var l2, out var a3, out var b3);
			return (float)Math.Sqrt((l - l2) * (l - l2) + (a2 - a3) * (a2 - a3) + (b2 - b3) * (b2 - b3));
		}

		private static void Lab(uint c, out float l, out float a, out float b)
		{
			float num = Lin((float)(c & 0xFF) / 255f);
			float num2 = Lin((float)((c >> 8) & 0xFF) / 255f);
			float num3 = Lin((float)((c >> 16) & 0xFF) / 255f);
			float t = (0.4124f * num + 0.3576f * num2 + 0.1805f * num3) / 0.95047f;
			float t2 = 0.2126f * num + 0.7152f * num2 + 0.0722f * num3;
			float t3 = (0.0193f * num + 0.1192f * num2 + 0.9505f * num3) / 1.08883f;
			float num4 = F(t);
			float num5 = F(t2);
			float num6 = F(t3);
			l = 116f * num5 - 16f;
			a = 500f * (num4 - num5);
			b = 200f * (num5 - num6);
		}

		private static float Lin(float v)
		{
			if (!(v <= 0.04045f))
			{
				return (float)Math.Pow((v + 0.055f) / 1.055f, 2.4000000953674316);
			}
			return v / 12.92f;
		}

		private static float F(float t)
		{
			if (!(t > 0.008856f))
			{
				return 7.787f * t + 0.13793103f;
			}
			return (float)Math.Pow(t, 0.3333333432674408);
		}
	}
}
