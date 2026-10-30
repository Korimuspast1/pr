using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal static class InventoryQoL
	{
		public static void TryQuickHeal(Actor a, Action<string> toast)
		{
			if (a == null) return;
			try
			{
				// find best heal item in bag
				var best = Bandages.Best(a, a);
				if (best == null)
				{
					toast?.Invoke("No bandage/salve in bag (craft Tab)");
					return;
				}
				// if already holding heal item, use it via left click logic else swap hotbar
				var held = Hotbar.Supply(a);
				if (held == null || !Supplies.Heals(held))
				{
					// try to equip best to hotbar
					Hotbar.Pick(a, best.Id);
					toast?.Invoke("Equipped " + best.Name + " — click to use");
				}
				else
				{
					toast?.Invoke("Use " + held.Name + " (LMB)");
				}
			}
			catch (Exception ex) { toast?.Invoke("Heal: " + ex.Message); }
		}

		public static void SortBag(Actor a, Action<string> toast)
		{
			if (a == null) return;
			try
			{
				var contents = Bag.Contents(a);
				if (contents == null || contents.Count == 0) { toast?.Invoke("Bag empty"); return; }
				// sort by id alphabetical, then count desc
				contents.Sort((p,q) => {
					int c = string.Compare(p.Key, q.Key, StringComparison.Ordinal);
					if (c != 0) return c;
					return q.Value.CompareTo(p.Value);
				});
				// Rebuild bag by removing and re-adding in sorted order (preserves stack logic)
				// Use Bag API: Take then Put to reorder via temporary list
				var copy = new List<KeyValuePair<string,int>>(contents);
				// clear via taking all
				foreach (var kv in copy) Bag.Take(a, kv.Key, kv.Value);
				foreach (var kv in copy) Bag.Put(a, kv.Key, kv.Value, spill:false);
				toast?.Invoke("Bag sorted");
				Log.Info("inv: sorted " + copy.Count + " kinds for " + WorldBoxMod.Describe(a));
			}
			catch (Exception ex) { Log.Error("inv sort: " + ex.Message); toast?.Invoke("Sort failed"); }
		}
	}
}
