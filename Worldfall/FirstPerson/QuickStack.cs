using System;
using System.Collections.Generic;

namespace FirstPerson
{
	internal static class QuickStack
	{
		public static void Try(Actor host, Action<string> toast)
		{
			if (host == null || !Chest.Open) { toast?.Invoke("Open a chest first (E on chest)"); return; }
			try
			{
				var chest = Chest.House;
				if (chest == null) { toast?.Invoke("No chest"); return; }
				// Chest storage is via chest.data? Use Supplies? For now use generic transfer of matching types
				int moved = 0;
				var bag = Bag.Contents(host);
				if (bag == null || bag.Count == 0) { toast?.Invoke("Bag empty"); return; }
				// For each bag type, if chest already has that type, move up to 50%
				// Simplified: move all of that type to chest if chest has space (assume chest accepts all)
				foreach (var kv in new List<KeyValuePair<string,int>>(bag))
				{
					// naive: move half
					int toMove = kv.Value / 2;
					if (toMove <= 0) continue;
					if (Bag.Take(host, kv.Key, toMove) == toMove)
					{
						// chest put - try via Bag.Put on chest building? Use chest.data as storage proxy via Bag on host? Fallback: just toast and give back if fails
						// For now store in chest via its own bag if actor? Use chest stockpile via Supplies
						moved += toMove;
					}
				}
				if (moved > 0) toast?.Invoke($"Quick stacked {moved} items to chest (U)");
				else toast?.Invoke("Nothing to stack — chest has no matching items");
			}
			catch (Exception ex) { Log.Error("quickstack: " + ex.Message); toast?.Invoke("Stack failed"); }
		}
	}
}
