using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class Shops
	{
		public const string Key = "fp_shop";

		public static ShopKind Of(Building b)
		{
			string text = Raw(b);
			if (string.IsNullOrEmpty(text))
			{
				return ShopKind.None;
			}
			if (text[text.Length - 1] == '*')
			{
				text = text.Substring(0, text.Length - 1);
			}
			try
			{
				return (ShopKind)Enum.Parse(typeof(ShopKind), text);
			}
			catch
			{
				return ShopKind.None;
			}
		}

		public static void Set(Building b, ShopKind k)
		{
			if (b == null)
			{
				return;
			}
			try
			{
				if (k == ShopKind.None)
				{
					b.data.removeString("fp_shop");
				}
				else
				{
					b.data.set("fp_shop", k.ToString() + "*");
				}
			}
			catch (Exception ex)
			{
				Log.Error("shops: set: " + ex.Message);
			}
		}

		public static bool Chosen(Building b)
		{
			string text = Raw(b);
			if (!string.IsNullOrEmpty(text))
			{
				return text[text.Length - 1] == '*';
			}
			return false;
		}

		public static Actor Keeper(Building b)
		{
			if (b == null)
			{
				return null;
			}
			Actor actor = null;
			try
			{
				if (b.residents == null)
				{
					return null;
				}
				foreach (long resident in b.residents)
				{
					Actor actor2 = World.world.units.get(resident);
					if (actor2 == null || !actor2.isAlive())
					{
						continue;
					}
					try
					{
						if (!actor2.isAdult())
						{
							continue;
						}
					}
					catch
					{
					}
					if (actor == null || actor2.data.id < actor.data.id)
					{
						actor = actor2;
					}
				}
			}
			catch
			{
			}
			return actor;
		}

		public static IEnumerable<Building> In(City c)
		{
			if (c == null)
			{
				yield break;
			}
			List<Building> list;
			try
			{
				list = new List<Building>(c.buildings);
			}
			catch
			{
				yield break;
			}
			foreach (Building item in list)
			{
				if (item != null && Of(item) != ShopKind.None)
				{
					yield return item;
				}
			}
		}

		public static List<ShopKind> KindsFor(Tier tier, int houses)
		{
			return new List<ShopKind> { ShopKind.Smithy };
		}

		public static string Word(ShopKind k)
		{
			return k.ToString().ToLowerInvariant();
		}

		public static int Assign(City c)
		{
			if (c == null)
			{
				return 0;
			}
			List<Building> list = new List<Building>();
			Vector2 centre;
			try
			{
				if (!c.isAlive())
				{
					return 0;
				}
				foreach (Building building in c.buildings)
				{
					if (IsHouse(building))
					{
						list.Add(building);
					}
				}
				centre = Centre(c);
			}
			catch
			{
				return 0;
			}
			int num = 0;
			List<ShopKind> list2 = KindsFor(TownTier.Of(c), list.Count);
			HashSet<ShopKind> hashSet = new HashSet<ShopKind>();
			foreach (Building item in list)
			{
				if (Chosen(item))
				{
					hashSet.Add(Of(item));
				}
			}
			foreach (Building item2 in list)
			{
				ShopKind shopKind = Of(item2);
				if (shopKind == ShopKind.None || Chosen(item2))
				{
					continue;
				}
				if (!Fit(item2) || hashSet.Contains(shopKind) || !list2.Contains(shopKind))
				{
					try
					{
						item2.data.removeString("fp_shop");
					}
					catch
					{
					}
					num++;
				}
				else
				{
					hashSet.Add(shopKind);
				}
			}
			list.Sort(delegate(Building a, Building b)
			{
				int num2 = TierOf(b).CompareTo(TierOf(a));
				if (num2 != 0)
				{
					return num2;
				}
				float sqrMagnitude = (Pos(a) - centre).sqrMagnitude;
				float sqrMagnitude2 = (Pos(b) - centre).sqrMagnitude;
				return (Mathf.Abs(sqrMagnitude - sqrMagnitude2) > 0.01f) ? sqrMagnitude.CompareTo(sqrMagnitude2) : IdOf(a).CompareTo(IdOf(b));
			});
			foreach (ShopKind item3 in list2)
			{
				if (hashSet.Contains(item3))
				{
					continue;
				}
				foreach (Building item4 in list)
				{
					if (Of(item4) == ShopKind.None && Fit(item4))
					{
						try
						{
							item4.data.set("fp_shop", item3.ToString());
						}
						catch
						{
							break;
						}
						hashSet.Add(item3);
						num++;
						Log.Info($"shops: {Rep.NameOf(c)}'s {Word(item3)} is {item4.asset.id} at {item4.current_tile?.x},{item4.current_tile?.y}");
						break;
					}
				}
			}
			return num;
		}

		private static bool IsHouse(Building b)
		{
			try
			{
				return b != null && b.isAlive() && b.asset != null && b.asset.type == "type_house";
			}
			catch
			{
				return false;
			}
		}

		private static bool Fit(Building b)
		{
			try
			{
				if (!IsHouse(b) || b.isUnderConstruction() || b.isRuin() || !b.hasResidents())
				{
					return false;
				}
				if ((b.asset.id ?? "").StartsWith("tent", StringComparison.Ordinal))
				{
					return false;
				}
				return Homes.OwnerId(b) <= 0;
			}
			catch
			{
				return false;
			}
		}

		private static int TierOf(Building b)
		{
			string text = ((b.asset != null) ? (b.asset.id ?? "") : "");
			char c = ((text.Length > 0) ? text[text.Length - 1] : '0');
			if (c < '0' || c > '9')
			{
				return 0;
			}
			return c - 48;
		}

		private static Vector2 Pos(Building b)
		{
			try
			{
				WorldTile current_tile = b.current_tile;
				return (current_tile != null) ? new Vector2(current_tile.x, current_tile.y) : Vector2.zero;
			}
			catch
			{
				return Vector2.zero;
			}
		}

		private static long IdOf(Building b)
		{
			try
			{
				return b.data.id;
			}
			catch
			{
				return 0L;
			}
		}

		internal static Vector2 Centre(City c)
		{
			try
			{
				if (c.buildings_dict_type != null && c.buildings_dict_type.TryGetValue("type_hall", out var value) && value != null)
				{
					foreach (Building item in value)
					{
						if (item != null && item.isAlive() && item.current_tile != null)
						{
							return Pos(item);
						}
					}
				}
				WorldTile tile = c.getTile();
				if (tile != null)
				{
					return new Vector2(tile.x, tile.y);
				}
			}
			catch
			{
			}
			return Vector2.zero;
		}

		private static string Raw(Building b)
		{
			if (b == null)
			{
				return null;
			}
			string pResult = null;
			try
			{
				b.data.get("fp_shop", out pResult, null);
			}
			catch
			{
			}
			return pResult;
		}
	}
}
