namespace FirstPerson.Core
{
	public static class HouseShapes
	{
		public const int District = 48;

		public static uint Roll(int x, int y)
		{
			return TerrainModel.Hash(x * 11 + 3, y * 17 + 5) >> 5;
		}

		public static int At(int x, int y, string race, int count)
		{
			int num = x / 48;
			uint num2 = TerrainModel.Hash(y: y / 48 * 13 + race[race.Length - 1] * 29, x: num * 7 + race.Length * 131 + race[0] * 17) >> 3;
			int num3 = (int)(num2 % (uint)count);
			int num4 = (num3 + 1 + (int)((num2 >> 5) % (uint)(count - 1))) % count;
			uint num5 = Roll(x, y) % 100;
			if (count == 2)
			{
				if (num5 >= 72)
				{
					return num4;
				}
				return num3;
			}
			switch (num5)
			{
			case 0u:
			case 1u:
			case 2u:
			case 3u:
			case 4u:
			case 5u:
			case 6u:
			case 7u:
			case 8u:
			case 9u:
			case 10u:
			case 11u:
			case 12u:
			case 13u:
			case 14u:
			case 15u:
			case 16u:
			case 17u:
			case 18u:
			case 19u:
			case 20u:
			case 21u:
			case 22u:
			case 23u:
			case 24u:
			case 25u:
			case 26u:
			case 27u:
			case 28u:
			case 29u:
			case 30u:
			case 31u:
			case 32u:
			case 33u:
			case 34u:
			case 35u:
			case 36u:
			case 37u:
			case 38u:
			case 39u:
			case 40u:
			case 41u:
			case 42u:
			case 43u:
			case 44u:
			case 45u:
			case 46u:
			case 47u:
			case 48u:
			case 49u:
			case 50u:
			case 51u:
			case 52u:
			case 53u:
			case 54u:
			case 55u:
			case 56u:
			case 57u:
			case 58u:
			case 59u:
			case 60u:
			case 61u:
			case 62u:
			case 63u:
				return num3;
			case 64u:
			case 65u:
			case 66u:
			case 67u:
			case 68u:
			case 69u:
			case 70u:
			case 71u:
			case 72u:
			case 73u:
			case 74u:
			case 75u:
			case 76u:
			case 77u:
			case 78u:
			case 79u:
			case 80u:
			case 81u:
			case 82u:
			case 83u:
			case 84u:
			case 85u:
			case 86u:
			case 87u:
			case 88u:
			case 89u:
			case 90u:
				return num4;
			default:
				return 3 - num3 - num4;
			}
		}

		public static bool Mirrored(int x, int y)
		{
			return ((TerrainModel.Hash(x * 13 + 7, y * 3 + 1) >> 11) & 1) != 0;
		}

		public static string House(string race, int tier, int shape)
		{
			string text = $"house_{race}_{tier}";
			object obj;
			switch (shape)
			{
			default:
				obj = "";
				break;
			case 2:
				obj = "_c";
				break;
			case 1:
				obj = "_b";
				break;
			}
			return text + (string)obj;
		}

		public static string Tent(string race, int shape)
		{
			return "tent_" + race + ((shape == 1) ? "_b" : "");
		}
	}
}
