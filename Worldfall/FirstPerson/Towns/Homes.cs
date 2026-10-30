using System;
using System.Collections.Generic;

namespace FirstPerson.Towns
{
	internal static class Homes
	{
		public const string Key = "fp_owner";

		public static long OwnerId(Building b)
		{
			if (b == null)
			{
				return 0L;
			}
			long result = 0L;
			try
			{
				b.data.get("fp_owner", out var pResult, null);
				if (!string.IsNullOrEmpty(pResult))
				{
					long.TryParse(pResult, out result);
				}
			}
			catch
			{
			}
			return result;
		}

		public static bool OwnedBy(Building b, Actor body)
		{
			if (b == null || body == null)
			{
				return false;
			}
			long id;
			try
			{
				id = body.data.id;
			}
			catch
			{
				return false;
			}
			if (id > 0)
			{
				return OwnerId(b) == id;
			}
			return false;
		}

		public static void SetOwner(Building b, Actor body)
		{
			if (b == null)
			{
				return;
			}
			try
			{
				if (body == null)
				{
					b.data.removeString("fp_owner");
				}
				else
				{
					b.data.set("fp_owner", body.data.id.ToString());
				}
			}
			catch (Exception ex)
			{
				Log.Error("homes: set owner: " + ex.Message);
			}
		}

		public static int CountOwned(Actor body)
		{
			if (body == null)
			{
				return 0;
			}
			long id;
			try
			{
				id = body.data.id;
			}
			catch
			{
				return 0;
			}
			List<Building> simpleList;
			try
			{
				simpleList = World.world.buildings.getSimpleList();
			}
			catch
			{
				return 0;
			}
			int num = 0;
			for (int i = 0; i < simpleList.Count; i++)
			{
				Building building = simpleList[i];
				if (building != null)
				{
					bool flag = false;
					try
					{
						flag = building.isAlive();
					}
					catch
					{
					}
					if (flag && OwnerId(building) == id)
					{
						num++;
					}
				}
			}
			return num;
		}

		public static IEnumerable<Building> Owned(Actor body)
		{
			if (body == null)
			{
				yield break;
			}
			long id;
			try
			{
				id = body.data.id;
			}
			catch
			{
				yield break;
			}
			List<Building> list;
			try
			{
				list = new List<Building>(World.world.buildings.getSimpleList());
			}
			catch
			{
				yield break;
			}
			foreach (Building item in list)
			{
				if (item != null)
				{
					bool flag = false;
					try
					{
						flag = item.isAlive();
					}
					catch
					{
					}
					if (flag && OwnerId(item) == id)
					{
						yield return item;
					}
				}
			}
		}

		public static void Update(Actor host, float dt)
		{
			Chest.Update(host);
		}

		public static void OnBodyDying(Actor body)
		{
		}

		public static void OnBodyDied(Actor body, Actor heir)
		{
		}

		public static bool RoomPrompt(PromptAsk ask)
		{
			return Chest.RoomPrompt(ask);
		}
	}
}
