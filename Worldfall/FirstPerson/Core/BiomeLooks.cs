using System;

namespace FirstPerson.Core
{
	public static class BiomeLooks
	{
		private static readonly BiomeAir[] Table = Build();

		private static uint C(int r, int g, int b)
		{
			return TerrainCache.Rgb(r, g, b);
		}

		private static BiomeAir A(uint sky, float amount, float light, uint tint, float haze)
		{
			return new BiomeAir
			{
				Sky = sky,
				Amount = amount,
				Light = light,
				LightTint = tint,
				Haze = haze,
				Clouds = 1f
			};
		}

		public static BiomeAir Air(BiomeKind k)
		{
			return Table[(uint)k];
		}

		private static BiomeAir[] Build()
		{
			BiomeAir[] array = new BiomeAir[28];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = new BiomeAir
				{
					Light = 1f,
					Haze = 1f,
					Clouds = 1f
				};
			}
			array[2] = With(A(C(255, 190, 104), 0.32f, 1.06f, C(255, 212, 146), 1.7f), 0.15f, 0f, 0u, 0f, 0.7f, 0u, 0.55f, 0.25f);
			array[3] = With(A(C(255, 242, 218), 0.4f, 1.12f, C(255, 238, 205), 1.25f), 0f, 0f, 0u, 0f, 0.35f, C(255, 250, 230), 1f, 1f);
			array[4] = With(A(C(150, 214, 158), 0.34f, 0.92f, C(222, 255, 205), 2.4f), 0.5f, 0f, C(80, 150, 140), 0.2f, 1.1f);
			array[5] = With(A(C(140, 160, 122), 0.5f, 0.78f, C(205, 224, 172), 3.2f), -0.2f, 0f, C(108, 124, 104), 0.35f, 1.3f);
			array[6] = With(A(C(198, 230, 255), 0.26f, 1.05f, C(222, 240, 255), 1.3f), 0.1f);
			array[7] = With(A(C(228, 242, 218), 0.14f, 1.04f, C(255, 255, 238), 1.2f));
			array[8] = With(A(C(255, 166, 104), 0.26f, 1f, C(255, 204, 146), 1.3f), 0.25f, 0f, 0u, 0f, 1f, C(255, 214, 160));
			array[9] = With(A(C(184, 172, 160), 0.2f, 0.96f, C(242, 232, 222), 1.4f));
			array[10] = With(A(C(138, 230, 188), 0.2f, 1.03f, C(222, 255, 232), 1.1f), 0.1f);
			array[11] = With(A(C(255, 178, 220), 0.2f, 1.05f, C(255, 232, 242), 1.2f), 0.2f);
			array[12] = With(A(C(255, 240, 118), 0.26f, 1.12f, C(255, 250, 192), 1.2f), 0.2f, 0f, 0u, 0f, 1f, C(255, 252, 170));
			array[13] = With(A(C(222, 226, 188), 0.24f, 0.98f, C(246, 250, 222), 1.7f));
			array[14] = With(A(C(255, 158, 210), 0.38f, 1.08f, C(255, 216, 240), 1.3f), 0.15f, 0f, C(128, 186, 255), 0.45f);
			array[15] = With(A(C(118, 202, 192), 0.32f, 0.86f, C(200, 238, 255), 1.9f), 0f, 0.12f, C(58, 56, 140), 0.35f);
			array[16] = With(A(C(150, 240, 250), 0.3f, 1.1f, C(212, 250, 255), 1.15f), 0.35f, 0f, 0u, 0f, 1f, C(220, 255, 255), 0f, 0f, 0f, 0f, 0.4f);
			array[17] = With(A(C(228, 188, 255), 0.32f, 1.06f, C(255, 236, 200), 1.45f), 0.55f, 0f, C(150, 120, 230), 0.25f);
			array[18] = With(A(C(110, 68, 120), 0.52f, 0.7f, C(200, 160, 232), 2.1f), -0.3f, 0f, C(38, 18, 56), 0.55f, 1.3f, C(214, 150, 255));
			array[19] = With(A(C(222, 82, 40), 0.52f, 0.95f, C(255, 150, 92), 2f), 0f, 0f, C(80, 20, 18), 0.5f, 0.8f, C(255, 120, 60), 1.25f);
			array[20] = With(A(C(172, 200, 92), 0.42f, 0.86f, C(222, 255, 152), 2.1f), 0f, 0f, C(90, 110, 58), 0.3f, 1f, C(230, 255, 150), 0.3f);
			array[21] = With(A(C(232, 170, 255), 0.42f, 1f, C(236, 216, 255), 1.2f), 0.3f, 0.95f, C(38, 28, 110), 0.78f);
			array[22] = With(A(C(92, 50, 142), 0.55f, 0.74f, C(192, 160, 255), 1.6f), -0.3f, 1f, C(10, 5, 30), 0.88f, 0.5f, 0u, 0f, 0f, 1f);
			array[23] = With(A(C(255, 120, 120), 0.45f, 1f, C(255, 214, 214), 1.3f), 0f, 0.3f, 0u, 0.55f, 1f, 0u, 0f, 0f, 0.4f, 0f, 1.2f);
			array[24] = With(A(C(204, 80, 92), 0.42f, 0.86f, C(255, 172, 172), 1.8f), 0f, 0f, 0u, 0f, 1f, 0u, 0f, 0f, 0f, 1f);
			array[25] = With(A(C(160, 166, 82), 0.46f, 0.88f, C(236, 230, 160), 2f), 0f, 0f, C(92, 100, 52), 0.3f, 1f, 0u, 0f, 0f, 0f, 0.6f);
			array[26] = With(A(C(255, 140, 50), 0.34f, 0.92f, C(255, 192, 122), 1.5f), 0f, 0f, C(62, 30, 82), 0.3f);
			array[27] = With(A(C(90, 232, 255), 0.4f, 0.95f, C(190, 240, 255), 1.2f), 0f, 0.45f, C(4, 30, 40), 0.62f, 1f, 0u, 0f, 0f, 0.15f);
			return array;
		}

		private static BiomeAir With(BiomeAir a, float rays = 0f, float stars = 0f, uint zenith = 0u, float zenithAmount = 0f, float clouds = 1f, uint sun = 0u, float shimmer = 0f, float mirage = 0f, float warp = 0f, float pulse = 0f, float hue = 0f)
		{
			a.Rays = rays;
			a.Stars = stars;
			a.Zenith = zenith;
			a.ZenithAmount = zenithAmount;
			a.Clouds = clouds;
			a.Sun = sun;
			a.Shimmer = shimmer;
			a.Mirage = mirage;
			a.Warp = warp;
			a.Pulse = pulse;
			a.HueDrift = hue;
			return a;
		}

		public static uint HueTurn(uint c, float turns)
		{
			float num = (float)(c & 0xFF) / 255f;
			float num2 = (float)((c >> 8) & 0xFF) / 255f;
			float num3 = (float)((c >> 16) & 0xFF) / 255f;
			float num4 = Math.Max(num, Math.Max(num2, num3));
			float num5 = Math.Min(num, Math.Min(num2, num3));
			float num6 = num4 - num5;
			float num7 = 0f;
			if (num6 > 1E-05f)
			{
				num7 = ((num4 == num) ? ((num2 - num3) / num6 / 6f) : ((num4 != num2) ? (((num - num2) / num6 + 4f) / 6f) : (((num3 - num) / num6 + 2f) / 6f)));
			}
			num7 += turns;
			num7 -= (float)Math.Floor(num7);
			float s = ((num4 > 1E-05f) ? (num6 / num4) : 0f);
			return Hsv(num7, s, num4);
		}

		public static uint Hsv(float h, float s, float v)
		{
			h -= (float)Math.Floor(h);
			float num = h * 6f;
			int num2 = (int)num;
			float num3 = v * (1f - s);
			float num4 = v * (1f - s * (num - (float)num2));
			float num5 = v * (1f - s * (1f - (num - (float)num2)));
			float num6;
			float num7;
			float num8;
			switch (num2 % 6)
			{
			case 0:
				num6 = v;
				num7 = num5;
				num8 = num3;
				break;
			case 1:
				num6 = num4;
				num7 = v;
				num8 = num3;
				break;
			case 2:
				num6 = num3;
				num7 = v;
				num8 = num5;
				break;
			case 3:
				num6 = num3;
				num7 = num4;
				num8 = v;
				break;
			case 4:
				num6 = num5;
				num7 = num3;
				num8 = v;
				break;
			default:
				num6 = v;
				num7 = num3;
				num8 = num4;
				break;
			}
			return TerrainCache.Rgb((int)(num6 * 255f), (int)(num7 * 255f), (int)(num8 * 255f));
		}
	}
}
