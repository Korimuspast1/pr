using System;

namespace FirstPerson.Core
{
	public static class Species
	{
		public static readonly string[] Civ = new string[49]
		{
			"necromancer", "demon", "ghost", "cat", "dog", "chicken", "rabbit", "monkey", "fox", "sheep",
			"cow", "armadillo", "wolf", "bear", "rhino", "buffalo", "hyena", "rat", "alpaca", "capybara",
			"goat", "scorpion", "crab", "penguin", "turtle", "crocodile", "snake", "frog", "piranha", "beetle",
			"seal", "unicorn", "bee", "druid", "bandit", "white_mage", "evil_mage", "cold_one", "angle", "fairy",
			"snowman", "alien", "greg", "garlic_man", "lemon_man", "acid_gentleman", "crystal_golem", "candy_man", "liliar"
		};

		private static readonly string[] Classic = new string[4] { "human", "elf", "orc", "dwarf" };

		private static readonly string[] ByLength = Sorted();

		private static readonly string[] Kinds = new string[12]
		{
			"boat_transport_", "boat_trading_", "watch_tower_", "windmill_", "barracks_", "library_", "temple_", "house_", "hall_", "docks_",
			"tent_", "int_"
		};

		private static string[] Sorted()
		{
			string[] array = new string[Civ.Length + Classic.Length];
			Civ.CopyTo(array, 0);
			Classic.CopyTo(array, Civ.Length);
			Array.Sort(array, (string a, string b) => b.Length.CompareTo(a.Length));
			return array;
		}

		public static string Alias(string key)
		{
			if (!(key == "greg"))
			{
				return key;
			}
			return "hyena";
		}

		public static bool IsClassic(string key)
		{
			switch (key)
			{
			default:
				return key == "dwarf";
			case "human":
			case "elf":
			case "orc":
				return true;
			}
		}

		public static string Of(string buildingId)
		{
			string text = buildingId ?? "";
			int num = text.IndexOf("_civ_", StringComparison.Ordinal);
			if (num >= 0)
			{
				string text2 = text.Substring(num + 5);
				int num2 = text2.Length;
				while (num2 > 0 && char.IsDigit(text2[num2 - 1]))
				{
					num2--;
				}
				if (num2 < text2.Length && num2 > 0 && text2[num2 - 1] == '_')
				{
					text2 = text2.Substring(0, num2 - 1);
				}
				return Alias(text2);
			}
			if (!text.Contains("elf"))
			{
				if (!text.Contains("orc"))
				{
					if (!text.Contains("dwarf"))
					{
						return "human";
					}
					return "dwarf";
				}
				return "orc";
			}
			return "elf";
		}

		public static string OfBoat(string boatId)
		{
			string text = boatId ?? "";
			if (text.StartsWith("boat_transport_", StringComparison.Ordinal))
			{
				return text.Substring(15);
			}
			if (text.StartsWith("boat_trading_", StringComparison.Ordinal))
			{
				return text.Substring(13);
			}
			return null;
		}

		public static string Family(string type, string key, bool fishing)
		{
			switch (type)
			{
			case "type_house":
				return "house_" + key + "_0";
			case "type_hall":
				return "hall_" + key;
			case "type_windmill":
				return "windmill_" + key;
			case "type_watch_tower":
				return "watch_tower_" + key;
			case "type_temple":
				return "temple_" + key;
			case "type_library":
				return "library_" + key;
			case "type_barracks":
				return "barracks_" + key;
			case "type_docks":
				if (!fishing)
				{
					return "docks_" + key;
				}
				return null;
			default:
				return null;
			}
		}

		public static string FromFamily(string family)
		{
			string text = family ?? "";
			if (text.StartsWith("ruin_", StringComparison.Ordinal) || text.StartsWith("site_", StringComparison.Ordinal))
			{
				text = text.Substring(5);
			}
			string[] kinds = Kinds;
			foreach (string text2 in kinds)
			{
				if (!text.StartsWith(text2, StringComparison.Ordinal))
				{
					continue;
				}
				string text3 = text.Substring(text2.Length);
				string[] byLength = ByLength;
				foreach (string text4 in byLength)
				{
					if (text3.StartsWith(text4, StringComparison.Ordinal) && (text3.Length == text4.Length || text3[text4.Length] == '_'))
					{
						return text4;
					}
				}
				return null;
			}
			return null;
		}

		public static bool IsCivFamily(string family)
		{
			string text = FromFamily(family);
			if (text != null)
			{
				return !IsClassic(text);
			}
			return false;
		}
	}
}
