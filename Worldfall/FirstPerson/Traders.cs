using System;
using System.Collections.Generic;
using System.Linq;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class Traders
	{
		internal sealed class Ware
		{
			public EquipmentAsset Asset;

			public string Name;
		}

		private const string SoldKey = "fp_forge";

		private static List<EquipmentAsset> _assets;

		internal static int Worth(EquipmentAsset a)
		{
			if (a == null)
			{
				return 10;
			}
			int[] array = new int[6] { 14, 22, 38, 64, 120, 220 };
			int num = Mathf.Clamp(Workbench.Rank(a), 0, 5);
			float num2;
			switch (a.equipment_type)
			{
			case EquipmentType.Weapon:
				num2 = 1f;
				break;
			case EquipmentType.Armor:
				num2 = 1.1f;
				break;
			case EquipmentType.Helmet:
				num2 = 0.7f;
				break;
			case EquipmentType.Boots:
				num2 = 0.6f;
				break;
			default:
				num2 = 0.8f;
				break;
			}
			return Mathf.RoundToInt((float)array[num] * num2);
		}

		internal static int ModWorth(EquipmentAsset a, IEnumerable<string> mods)
		{
			int num = 0;
			if (mods == null)
			{
				return 0;
			}
			foreach (string mod in mods)
			{
				if (Array.IndexOf(WeaponTraits.All, mod) < 0)
				{
					continue;
				}
				try
				{
					if (a != null && a.item_modifier_ids != null && Array.IndexOf(a.item_modifier_ids, mod) >= 0)
					{
						continue;
					}
				}
				catch
				{
				}
				num += ((mod == "divine_rune" || mod == "eternal") ? 500 : 150);
			}
			return num;
		}

		internal static int Grade(City c)
		{
			int num = 0;
			bool flag = false;
			bool flag2 = false;
			try
			{
				num = c.getPopulationPeople();
				flag = c.isCapitalCity();
				flag2 = Has(c, "mine");
			}
			catch
			{
			}
			int num2 = ((flag && num >= 40) ? 4 : ((num >= 25) ? 3 : 2));
			if (flag2)
			{
				num2++;
			}
			return Mathf.Clamp(num2, 2, 4);
		}

		private static bool Has(City c, string id)
		{
			try
			{
				foreach (Building building in c.buildings)
				{
					if (building != null && building.isAlive() && building.asset != null && (building.asset.id == id || building.asset.id.StartsWith(id + "_", StringComparison.Ordinal)))
					{
						return true;
					}
				}
			}
			catch
			{
			}
			return false;
		}

		internal static List<Ware> Wares(City c)
		{
			List<Ware> list = new List<Ware>();
			if (c == null)
			{
				return list;
			}
			Assets();
			int num = 0;
			try
			{
				num = c.getPopulationPeople();
			}
			catch
			{
			}
			int grade = Grade(c);
			bool armour = Has(c, "barracks") || num >= 25;
			bool flag = Has(c, "market");
			int num2 = 2 + ((num >= 15) ? 1 : 0) + ((num >= 40) ? 1 : 0) + (flag ? 2 : 0);
			List<EquipmentAsset> list2 = _assets.Where((EquipmentAsset a) => Workbench.Rank(a) <= grade && (a.equipment_type == EquipmentType.Weapon || armour)).ToList();
			if (list2.Count == 0)
			{
				return list;
			}
			int day = Calendar.Day;
			long num3 = 0L;
			try
			{
				num3 = c.data.id;
			}
			catch
			{
			}
			System.Random random = new System.Random((int)((num3 * 7919 + day * 104729) & 0x7FFFFFFF));
			list2.Sort((EquipmentAsset x, EquipmentAsset y) => Workbench.Rank(y).CompareTo(Workbench.Rank(x)));
			HashSet<string> hashSet = new HashSet<string>();
			for (int num4 = 0; num4 < 40; num4++)
			{
				if (list.Count >= num2)
				{
					break;
				}
				int index = Mathf.Min(list2.Count - 1, (int)(Math.Pow(random.NextDouble(), 1.6) * (double)list2.Count));
				EquipmentAsset equipmentAsset = list2[index];
				if (hashSet.Add(equipmentAsset.id))
				{
					list.Add(new Ware
					{
						Asset = equipmentAsset,
						Name = AssetName(equipmentAsset)
					});
				}
			}
			List<string> sold = SoldToday(c, day);
			list.RemoveAll((Ware w) => sold.Contains(w.Asset.id));
			return list;
		}

		internal static int Price(EquipmentAsset a, float markup)
		{
			return Mathf.Clamp(Mathf.RoundToInt((float)Worth(a) * 1.2f * markup), 6, 2400);
		}

		private static List<string> SoldToday(City c, int day)
		{
			List<string> list = new List<string>();
			try
			{
				c.data.get("fp_forge", out var pResult, null);
				if (string.IsNullOrEmpty(pResult))
				{
					return list;
				}
				int num = pResult.IndexOf('|');
				if (num < 0 || !int.TryParse(pResult.Substring(0, num), out var result) || result != day)
				{
					return list;
				}
				list.AddRange(pResult.Substring(num + 1).Split(new char[1] { ',' }, StringSplitOptions.RemoveEmptyEntries));
			}
			catch
			{
			}
			return list;
		}

		internal static bool Buy(Actor you, City c, Ware w, int price, Actor smith, out Item made, out string note)
		{
			made = null;
			note = null;
			if (Economy.Money(you) < price)
			{
				note = "You can't afford that.";
				return false;
			}
			try
			{
				if (you.equipment == null || !you.understandsHowToUseItems())
				{
					note = "You can't use that.";
					return false;
				}
				ActorEquipmentSlot slot = you.equipment.getSlot(w.Asset.equipment_type);
				Item item = slot.getItem();
				if (item != null && item.isCursed())
				{
					note = "Your cursed " + Economy.ItemName(item) + " won't come off.";
					return false;
				}
				made = World.world.items.generateItem(w.Asset, c.kingdom, (smith != null) ? smith.getName() : c.name, 1, you, 0, pByPlayer: true);
				if (made == null)
				{
					note = "The forge went cold. Try again.";
					return false;
				}
				Crafting.Ordinary(made);
				if (item != null)
				{
					Gear.Piece piece = Gear.FromItem(item);
					bool value = Bag.Free(you) > 0;
					slot.takeAwayItem();
					Gear.LetGo(item);
					bool flag = piece != null && Bag.PutPiece(you, piece, null, value);
					note = ((piece == null) ? "" : (flag ? (" Your old " + Gear.NameOf(piece).ToLowerInvariant() + " is in your bag.") : (" Your old " + Gear.NameOf(piece).ToLowerInvariant() + " is at your feet.")));
				}
				slot.setItem(made, you);
				you.setStatsDirty();
			}
			catch (Exception ex)
			{
				Log.Error("traders: buy " + w.Asset.id + ": " + ex.Message);
				note = "Something went wrong at the forge.";
				return false;
			}
			Economy.Pay(you, price, c, smith);
			int day = Calendar.Day;
			List<string> list = SoldToday(c, day);
			list.Add(w.Asset.id);
			try
			{
				c.data.set("fp_forge", day + "|" + string.Join(",", list.ToArray()));
			}
			catch
			{
			}
			return true;
		}

		private static void Assets()
		{
			if (_assets != null)
			{
				return;
			}
			_assets = new List<EquipmentAsset>();
			try
			{
				foreach (EquipmentAsset item in AssetManager.items.list)
				{
					if (item != null && !string.IsNullOrEmpty(item.id))
					{
						EquipmentType equipment_type = item.equipment_type;
						if ((equipment_type == EquipmentType.Weapon || equipment_type == EquipmentType.Helmet || equipment_type == EquipmentType.Armor || equipment_type == EquipmentType.Boots) && !string.IsNullOrEmpty(item.cost_resource_id_1) && !(item.cost_resource_id_1 == "none") && item.cost_resource_1 > 0 && (item.item_modifier_ids == null || !item.item_modifier_ids.Any((string m) => Array.IndexOf(WeaponTraits.All, m) >= 0)) && Workbench.Rank(item) < 5)
						{
							_assets.Add(item);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("traders: wares: " + ex.Message);
			}
		}

		internal static string AssetName(EquipmentAsset a)
		{
			try
			{
				string translatedName = a.getTranslatedName();
				if (!string.IsNullOrEmpty(translatedName) && !translatedName.StartsWith("$"))
				{
					return translatedName.ToLowerInvariant();
				}
			}
			catch
			{
			}
			return a.id.Replace('_', ' ');
		}
	}
}
