using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal static class Bag
	{
		public const string TierKey = "fp_bagtier";

		internal static readonly string[] Names = new string[4] { "Pouch", "Satchel", "Pack", "Frame pack" };

		internal static readonly int[] Slots = new int[4] { 6, 10, 16, 24 };

		internal static readonly int[] Prices = new int[4] { 0, 60, 200, 600 };

		public const int Best = 3;

		private static readonly List<KeyValuePair<string, int>> _usedScratch = new List<KeyValuePair<string, int>>();

		public static int Tier(Actor a)
		{
			if (a == null || !Run.LockedOn(a))
			{
				return 3;
			}
			int pResult = 0;
			try
			{
				a.data.get("fp_bagtier", out pResult, 0);
			}
			catch
			{
			}
			return Mathf.Clamp(pResult, 0, 3);
		}

		public static int Size(Actor a)
		{
			return Slots[Tier(a)];
		}

		public static string Name(Actor a)
		{
			return Names[Tier(a)];
		}

		public static int Stack(string res)
		{
			if (string.IsNullOrEmpty(res))
			{
				return 50;
			}
			Supplies.Kind kind = Supplies.Of(res);
			if (kind != null)
			{
				return kind.Stack;
			}
			switch (res)
			{
			case "gems":
			case "tear":
			case "adamantine":
			case "golden_egg":
			case "dragon_scales":
			case "mythril":
			case "evil_eye":
				return 25;
			case "gold":
			case "iron":
			case "common_metals":
			case "bronze":
			case "silver":
			case "copper":
			case "steel":
				return 25;
			case "burger":
			case "bread":
			case "sushi":
			case "cider":
			case "pie":
			case "tea":
			case "ale":
			case "jam":
				return 20;
			default:
				try
				{
					ResourceAsset resourceAsset = AssetManager.resources.get(res);
					if (resourceAsset != null && resourceAsset.mineral && res != "stone")
					{
						return 25;
					}
				}
				catch
				{
				}
				return 50;
			}
		}

		public static int SlotsFor(string res, int amount)
		{
			if (amount > 0)
			{
				return (amount + Stack(res) - 1) / Stack(res);
			}
			return 0;
		}

		public static List<KeyValuePair<string, int>> Contents(Actor a)
		{
			List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>();
			Fill(a, list);
			return list;
		}

		private static void Fill(Actor a, List<KeyValuePair<string, int>> list)
		{
			try
			{
				Dictionary<string, ResourceContainer> dictionary = a?.inventory?.getResources();
				if (dictionary != null)
				{
					foreach (KeyValuePair<string, ResourceContainer> item in dictionary)
					{
						if (item.Value.amount > 0)
						{
							list.Add(new KeyValuePair<string, int>(item.Key, item.Value.amount));
						}
					}
				}
			}
			catch
			{
			}
			Supplies.AddTo(a, list);
		}

		private static void Add(Actor a, string res, int amount)
		{
			if (Supplies.Is(res))
			{
				Supplies.Give(a, res, amount);
			}
			else
			{
				a.addToInventory(res, amount);
			}
		}

		public static int Used(Actor a)
		{
			int num = Gear.Count(a) + (Hotbar.HandCounts(a) ? 1 : 0);
			_usedScratch.Clear();
			Fill(a, _usedScratch);
			for (int i = 0; i < _usedScratch.Count; i++)
			{
				num += SlotsFor(_usedScratch[i].Key, _usedScratch[i].Value);
			}
			_usedScratch.Clear();
			return num;
		}

		public static int Free(Actor a)
		{
			return Mathf.Max(0, Size(a) - Used(a));
		}

		public static int RoomFor(Actor a, string res)
		{
			if (a == null)
			{
				return 0;
			}
			int num = Civil.BagCount(a, res);
			int num2 = Stack(res);
			return ((num % num2 != 0) ? (num2 - num % num2) : 0) + Free(a) * num2;
		}

		public static int Put(Actor a, string res, int amount, bool spill, Action<string> toast = null)
		{
			if (a == null || amount <= 0)
			{
				return 0;
			}
			int num = Mathf.Min(amount, RoomFor(a, res));
			if (num > 0)
			{
				try
				{
					Add(a, res, num);
				}
				catch (Exception ex)
				{
					Log.Error("bag: put " + res + ": " + ex.Message);
					num = 0;
				}
			}
			int num2 = amount - num;
			if (num2 > 0 && spill)
			{
				Drop(a, res, num2, null);
				toast?.Invoke("No room in your " + Name(a).ToLowerInvariant() + ": " + Work.Amount(res, num2) + " set down at your feet (E to pick up).");
			}
			return num;
		}

		public static bool PutPiece(Actor a, Gear.Piece p, Action<string> toast = null, bool? room = null)
		{
			if (a == null || p == null)
			{
				return false;
			}
			if ((room ?? (Free(a) > 0)) && Gear.Add(a, p))
			{
				return true;
			}
			Drop(a, null, 0, p);
			toast?.Invoke("No room in your " + Name(a).ToLowerInvariant() + ": the " + Gear.NameOf(p).ToLowerInvariant() + " is set down at your feet (E to pick up).");
			return false;
		}

		private static void Drop(Actor a, string res, int count, Gear.Piece piece)
		{
			try
			{
				WorldBoxMod mod = WorldBoxMod.Instance;
				float f = ((mod != null) ? mod.ViewYaw : 0f);
				Vector2 at = a.current_position + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * 0.7f;
				if ((!(mod != null) || !mod.IndoorsForDrops) && DryNear(ref at, a.current_position))
				{
					Bundles.Drop(at, (Vector2 p) => (!(mod != null)) ? 0f : mod.GroundAt(p), res, count, piece);
					return;
				}
				if (piece != null)
				{
					Gear.AddOver(a, piece);
				}
				else
				{
					Add(a, res, count);
				}
				Log.Info("bag: nowhere to set " + ((piece != null) ? piece.Id : (count + " " + res)) + " down: kept in the bag over its size");
			}
			catch (Exception ex)
			{
				Log.Error("bag: drop: " + ex.Message);
			}
		}

		private static bool DryNear(ref Vector2 at, Vector2 from)
		{
			if (Bundles.Dry(at))
			{
				return true;
			}
			for (float num = 0.8f; num <= 6f; num += 0.8f)
			{
				for (int i = 0; i < 12; i++)
				{
					float f = (float)i * MathF.PI / 6f;
					Vector2 vector = from + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num;
					if (Bundles.Dry(vector))
					{
						at = vector;
						return true;
					}
				}
			}
			return false;
		}

		public static bool Next(Actor a, out int tier, out int price)
		{
			tier = Tier(a) + 1;
			price = ((tier <= 3) ? Prices[tier] : 0);
			if (tier <= 3)
			{
				return Run.LockedOn(a);
			}
			return false;
		}

		public static void SetTier(Actor a, int tier)
		{
			try
			{
				a.data.set("fp_bagtier", Mathf.Clamp(tier, 0, 3));
			}
			catch
			{
			}
		}
	}
}
