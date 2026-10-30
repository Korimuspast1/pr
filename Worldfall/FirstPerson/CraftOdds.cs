using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal static class CraftOdds
	{
		public const int Grades = 4;

		private const int Samples = 6000;

		private static readonly Dictionary<long, float[]> Cache = new Dictionary<long, float[]>();

		public static float[] For(EquipmentAsset a, int tries)
		{
			if (a == null)
			{
				return null;
			}
			int num = Floor(a);
			long key = ((long)a.equipment_type << 32) | ((long)num << 16) | Mathf.Clamp(tries, 0, 99);
			if (Cache.TryGetValue(key, out var value))
			{
				return value;
			}
			value = new float[4];
			List<ItemModAsset> list = Pool(a.equipment_type);
			if (list == null || list.Count == 0)
			{
				value[num] = 1f;
			}
			else
			{
				System.Random random = new System.Random(1234 + tries);
				List<string> list2 = new List<string>(8);
				for (int i = 0; i < 6000; i++)
				{
					list2.Clear();
					int num2 = num;
					for (int j = 0; j < tries; j++)
					{
						if (random.Next(2) == 0)
						{
							continue;
						}
						ItemModAsset itemModAsset = list[random.Next(list.Count)];
						if (itemModAsset != null && itemModAsset.mod_can_be_given && !list2.Contains(itemModAsset.mod_type))
						{
							list2.Add(itemModAsset.mod_type);
							if (!Crafting.IsEarned(itemModAsset.id))
							{
								num2 = Math.Max(num2, Grade(itemModAsset.quality));
							}
						}
					}
					value[num2] += 1f;
				}
				for (int k = 0; k < 4; k++)
				{
					value[k] /= 6000f;
				}
			}
			Cache[key] = value;
			return value;
		}

		public static Rarity QualityOf(EquipmentAsset a, string[] mods)
		{
			int num = 0;
			try
			{
				num = Grade(a.quality);
			}
			catch
			{
			}
			if (mods != null)
			{
				foreach (string pID in mods)
				{
					ItemModAsset itemModAsset = null;
					try
					{
						itemModAsset = AssetManager.items_modifiers.get(pID);
					}
					catch
					{
					}
					if (itemModAsset != null)
					{
						num = Math.Max(num, Grade(itemModAsset.quality));
					}
				}
			}
			return (Rarity)num;
		}

		private static int Floor(EquipmentAsset a)
		{
			int num = 0;
			try
			{
				num = Grade(a.quality);
				if (a.item_modifiers != null)
				{
					ItemModAsset[] item_modifiers = a.item_modifiers;
					foreach (ItemModAsset itemModAsset in item_modifiers)
					{
						if (itemModAsset != null && !Crafting.IsEarned(itemModAsset.id))
						{
							num = Math.Max(num, Grade(itemModAsset.quality));
						}
					}
				}
			}
			catch
			{
			}
			return num;
		}

		private static int Grade(Rarity r)
		{
			return Mathf.Clamp((int)r, 0, 3);
		}

		private static List<ItemModAsset> Pool(EquipmentType t)
		{
			object obj;
			switch (t)
			{
			default:
				obj = "armor";
				break;
			case EquipmentType.Ring:
			case EquipmentType.Amulet:
				obj = "accessory";
				break;
			case EquipmentType.Weapon:
				obj = "weapon";
				break;
			}
			string key = (string)obj;
			try
			{
				List<ItemModAsset> value;
				return AssetManager.items_modifiers.pools.TryGetValue(key, out value) ? value : null;
			}
			catch
			{
				return null;
			}
		}

		public static string Word(Rarity r)
		{
			switch (r)
			{
			case Rarity.R1_Rare:
				return "Rare";
			case Rarity.R2_Epic:
				return "Epic";
			case Rarity.R3_Legendary:
				return "Legendary";
			default:
				return "Normal";
			}
		}

		public static Color ColorOf(Rarity r)
		{
			Color color;
			try
			{
				color = r.getRarityColor();
			}
			catch
			{
				Color color2;
				switch (r)
				{
				default:
					color2 = Color.white;
					break;
				case Rarity.R3_Legendary:
					color2 = new Color(1f, 0.72f, 0.2f);
					break;
				case Rarity.R2_Epic:
					color2 = new Color(0.75f, 0.4f, 1f);
					break;
				case Rarity.R1_Rare:
					color2 = new Color(0.35f, 0.65f, 1f);
					break;
				}
				color = color2;
			}
			if (r == Rarity.R0_Normal)
			{
				return new Color(0.92f, 0.92f, 0.94f);
			}
			if (!(0.3f * color.r + 0.59f * color.g + 0.11f * color.b < 0.45f))
			{
				return color;
			}
			return Color.Lerp(color, Color.white, 0.35f);
		}

		public static string ModName(string id)
		{
			try
			{
				string text = AssetManager.items_modifiers.get(id)?.getTranslatedName();
				if (!string.IsNullOrEmpty(text) && !text.StartsWith("$"))
				{
					return text;
				}
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(id))
			{
				return "";
			}
			string text2 = id.Replace('_', ' ');
			return char.ToUpperInvariant(text2[0]) + text2.Substring(1);
		}
	}
}
