using System;
using System.Collections.Generic;

namespace FirstPerson
{
	internal static class Civil
	{
		public static List<Actor> PeopleOf(City c)
		{
			List<Actor> list = new List<Actor>();
			if (c == null)
			{
				return list;
			}
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple.isAlive() && simple.city == c)
					{
						list.Add(simple);
					}
				}
			}
			catch
			{
			}
			return list;
		}

		public static List<Actor> SubjectsOf(Kingdom k)
		{
			List<Actor> list = new List<Actor>();
			if (k == null)
			{
				return list;
			}
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple.isAlive() && simple.kingdom == k)
					{
						list.Add(simple);
					}
				}
			}
			catch
			{
			}
			return list;
		}

		public static int BagCount(Actor a, string res)
		{
			if (a == null || string.IsNullOrEmpty(res))
			{
				return 0;
			}
			if (Supplies.Is(res))
			{
				return Supplies.Count(a, res);
			}
			try
			{
				foreach (KeyValuePair<string, ResourceContainer> resource in a.inventory.getResources())
				{
					if (resource.Key == res)
					{
						return Math.Max(0, resource.Value.amount);
					}
				}
			}
			catch
			{
			}
			return 0;
		}

		public static bool TakeFromBag(Actor a, string res, int amount)
		{
			if (amount <= 0)
			{
				return true;
			}
			if (BagCount(a, res) < amount)
			{
				return false;
			}
			if (Supplies.Is(res))
			{
				return Supplies.Take(a, res, amount);
			}
			try
			{
				a.takeFromInventory(res, amount);
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("bag: " + ex.Message);
				return false;
			}
		}
	}
}
