using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal static class Hotbar
	{
		public const string Key = "fp_hotbar";

		public const string Hand = "h";

		public const int Max = 8;

		public const float Settle = 0.15f;

		public const float ShowFor = 3f;

		public static float ChangedAt = -10f;

		private static float _settleAt;

		private static bool _refused;

		private static long _refusedBody;

		private static int _refusedSlot;

		private static string _refusedKey;

		private static string _refusedId;

		private static readonly List<KeyValuePair<string, int>> _bag = new List<KeyValuePair<string, int>>();

		private static readonly string TorchKey = BagOrder.ResourceKey("tw_item_torch");

		public static int Size(Actor a)
		{
			return Mathf.Min(8, Bag.Size(a));
		}

		public static int Chosen(Actor a)
		{
			int pResult = 0;
			try
			{
				a.data.get("fp_hotbar", out pResult, 0);
			}
			catch
			{
			}
			return Mathf.Clamp(pResult, 0, Mathf.Max(0, Size(a) - 1));
		}

		public static void Choose(Actor a, int i, bool wheel)
		{
			if (a != null)
			{
				i = Mathf.Clamp(i, 0, Mathf.Max(0, Size(a) - 1));
				try
				{
					a.data.set("fp_hotbar", i);
				}
				catch
				{
				}
				_settleAt = (wheel ? (Time.unscaledTime + 0.15f) : 0f);
				ChangedAt = Time.unscaledTime;
			}
		}

		public static void Step(Actor a, int by)
		{
			int num = Size(a);
			if (num > 0)
			{
				Choose(a, ((Chosen(a) + by) % num + num) % num, wheel: true);
			}
		}

		public static Item Weapon(Actor a)
		{
			try
			{
				return a.equipment?.getSlot(EquipmentType.Weapon)?.getItem();
			}
			catch
			{
				return null;
			}
		}

		public static bool UsesItems(Actor a)
		{
			try
			{
				return a.equipment != null && a.understandsHowToUseItems();
			}
			catch
			{
				return false;
			}
		}

		public static bool HandCounts(Actor a)
		{
			if (a != null && BagOrder.Of(a).Contains("h"))
			{
				return Weapon(a) != null;
			}
			return false;
		}

		public static string InHand(Actor a)
		{
			List<string> list = BagOrder.Of(a);
			int num = Chosen(a);
			if (num >= list.Count)
			{
				return "";
			}
			return list[num];
		}

		public static string Food(Actor a)
		{
			string text = BagOrder.ResourceOf(InHand(a));
			if (text == null || !Eating.Edible(a, text))
			{
				return null;
			}
			return text;
		}

		public static Supplies.Kind Supply(Actor a)
		{
			return Supplies.Of(BagOrder.ResourceOf(InHand(a)));
		}

		public static bool IsWeapon(Gear.Piece p)
		{
			EquipmentAsset equipmentAsset = p?.Asset;
			if (equipmentAsset != null)
			{
				return equipmentAsset.equipment_type == EquipmentType.Weapon;
			}
			return false;
		}

		public static void Sync(Actor a, Action<string> toast)
		{
			if (a == null)
			{
				return;
			}
			FillBag(a);
			int pieces = Gear.Count(a);
			int num = Size(a);
			bool num2 = UsesItems(a);
			BagOrder.Tidy(a, _bag, pieces);
			List<string> list = BagOrder.Of(a);
			if (num2 && Time.unscaledTime >= _settleAt && Weapon(a) != null && !list.Contains("h"))
			{
				int num3 = Chosen(a);
				if (!Free(list, num3))
				{
					int num4 = FirstFree(list, 0, num, -1);
					if (num4 >= 0)
					{
						num3 = num4;
						Choose(a, num4, wheel: false);
					}
					else
					{
						BagOrder.Set(a, FirstFree(list, num, int.MaxValue, -1), list[num3]);
					}
				}
				BagOrder.Set(a, num3, "h");
			}
			string torchKey = TorchKey;
			if (num2 && !list.Contains(torchKey) && Supplies.Count(a, "tw_item_torch") > 0)
			{
				int num5 = FirstFree(list, 0, num, Chosen(a));
				if (num5 >= 0)
				{
					BagOrder.Set(a, num5, torchKey);
				}
			}
			list = BagOrder.Fit(a, _bag, pieces, Bag.Size(a));
			if (!num2 || Time.unscaledTime < _settleAt)
			{
				return;
			}
			int num6 = Chosen(a);
			int num7 = list.IndexOf("h");
			int pResult = 0;
			try
			{
				a.data.get("fp_hotbar", out pResult, 0);
			}
			catch
			{
			}
			if (num7 >= 0 && num7 != num6 && pResult > num6)
			{
				BagOrder.Swap(a, num7, num6);
				Choose(a, num6, wheel: false);
				list = BagOrder.Of(a);
				num7 = list.IndexOf("h");
			}
			if (num7 >= 0 && num7 != num6)
			{
				if (!Gear.TakeOff(a, EquipmentType.Weapon, toast, quiet: true))
				{
					if (num7 < num)
					{
						Choose(a, num7, wheel: false);
					}
					else
					{
						BagOrder.Swap(a, num7, num6);
					}
					return;
				}
				BagOrder.Set(a, num7, BagOrder.PieceKey(Gear.Count(a) - 1));
			}
			if (Weapon(a) != null || num6 >= list.Count)
			{
				return;
			}
			int num8 = BagOrder.PieceOf(list[num6]);
			List<Gear.Piece> list2 = Gear.Of(a);
			if (num8 < 0 || num8 >= list2.Count || !IsWeapon(list2[num8]))
			{
				return;
			}
			long id = a.data.id;
			if (!_refused || _refusedBody != id || _refusedSlot != num6 || !(_refusedKey == list[num6]) || !(_refusedId == list2[num8].Id))
			{
				if (!Gear.PutOn(a, num8, toast, quiet: true))
				{
					_refused = true;
					_refusedBody = id;
					_refusedSlot = num6;
					_refusedKey = list[num6];
					_refusedId = list2[num8].Id;
				}
				else
				{
					_refused = false;
					BagOrder.PieceGone(a, num8);
					BagOrder.Set(a, num6, "h");
				}
			}
		}

		public static void Hold(Actor a, int cell, Action<string> toast)
		{
			if (a != null && cell >= 0)
			{
				if (cell < Size(a))
				{
					Choose(a, cell, wheel: false);
				}
				else
				{
					BagOrder.Swap(a, cell, Chosen(a));
					_settleAt = 0f;
					ChangedAt = Time.unscaledTime;
				}
				Sync(a, toast);
			}
		}

		public static bool Stow(Actor a, Action<string> toast)
		{
			List<string> list = BagOrder.Of(a);
			int num = list.IndexOf("h");
			int not = Chosen(a);
			int num2 = Size(a);
			if (num < 0)
			{
				return false;
			}
			int num3 = FirstFree(list, 0, num2, not);
			if (num3 < 0)
			{
				num3 = FirstFree(list, num2, Bag.Size(a), not);
			}
			if (num3 < 0)
			{
				toast?.Invoke("No room in your bag");
				return false;
			}
			BagOrder.Swap(a, num, num3);
			_settleAt = 0f;
			ChangedAt = Time.unscaledTime;
			Sync(a, toast);
			return true;
		}

		public static void Moved()
		{
			_settleAt = 0f;
			ChangedAt = Time.unscaledTime;
		}

		private static bool Free(List<string> s, int i)
		{
			if (i < s.Count)
			{
				return s[i].Length == 0;
			}
			return true;
		}

		private static int FirstFree(List<string> s, int from, int to, int not)
		{
			for (int i = from; i < to; i++)
			{
				if (i != not && Free(s, i))
				{
					return i;
				}
			}
			return -1;
		}

		private static void FillBag(Actor a)
		{
			_bag.Clear();
			try
			{
				Dictionary<string, ResourceContainer> dictionary = a.inventory?.getResources();
				if (dictionary != null)
				{
					foreach (KeyValuePair<string, ResourceContainer> item in dictionary)
					{
						if (item.Value.amount > 0)
						{
							_bag.Add(new KeyValuePair<string, int>(item.Key, item.Value.amount));
						}
					}
				}
			}
			catch
			{
			}
			Supplies.AddTo(a, _bag);
			_bag.Sort((KeyValuePair<string, int> x, KeyValuePair<string, int> y) => string.CompareOrdinal(x.Key, y.Key));
		}
	}
}
