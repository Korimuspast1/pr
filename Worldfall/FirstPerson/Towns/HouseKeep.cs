using System;
using System.Reflection;
using ai.behaviours;

namespace FirstPerson.Towns
{
	internal static class HouseKeep
	{
		private const int GameIlBytes = 106;

		private const int FindHouseIlBytes = 131;

		private static bool _installed;

		internal static int SparedForTest;

		public static bool Installed => _installed;

		public static void Install()
		{
			if (!_installed)
			{
				MethodInfo method = typeof(CityBehBuild).GetMethod("getOnHouseTile", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 106)
				{
					throw new InvalidOperationException("the game's house replacing has changed (" + num + " bytes of IL, not " + 106 + ")");
				}
				MethodInfo method2 = typeof(BehFindHouse).GetMethod("execute", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(Actor) }, null);
				int num2 = method2?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num2 != 131)
				{
					throw new InvalidOperationException("the game's house finding has changed (" + num2 + " bytes of IL, not " + 131 + ")");
				}
				Detour.Apply(method, typeof(HouseKeep).GetMethod("OnHouseTile", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				Detour.Apply(method2, typeof(HouseKeep).GetMethod("FindHouse", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
			}
		}

		private static BehResult FindHouse(BehFindHouse self, Actor pActor)
		{
			if (pActor.hasHouse())
			{
				return BehResult.Stop;
			}
			Building building = null;
			foreach (Building building2 in pActor.city.buildings)
			{
				if (!building2.isUnderConstruction() && building2.hasResidentSlots())
				{
					if (MayLive(building2, pActor))
					{
						building = building2;
						break;
					}
					SparedForTest++;
				}
			}
			if (building == null)
			{
				building = BehFindHouse.tryToFindFamilyHouse(pActor);
				if (building != null && !MayLive(building, pActor))
				{
					building = null;
				}
			}
			if (building == null)
			{
				return BehResult.Stop;
			}
			pActor.setHomeBuilding(building);
			pActor.changeHappiness("just_found_house", building.asset.housing_happiness);
			return BehResult.Continue;
		}

		internal static bool MayLive(Building b, Actor a)
		{
			long num = Held(b);
			if (num == 0L)
			{
				return true;
			}
			try
			{
				if (a.data.id == num || a.data.parent_id_1 == num || a.data.parent_id_2 == num)
				{
					return true;
				}
				return a.lover != null && a.lover.data.id == num;
			}
			catch
			{
				return false;
			}
		}

		internal static long Held(Building b)
		{
			long num = Homes.OwnerId(b);
			if (num == 0L)
			{
				return 0L;
			}
			try
			{
				Actor actor = World.world.units.get(num);
				return (actor != null && actor.isAlive()) ? num : 0;
			}
			catch
			{
				return 0L;
			}
		}

		private static WorldTile OnHouseTile(City pCity, BuildingAsset pAsset)
		{
			foreach (Building item in pCity.buildings.LoopRandom())
			{
				if (item.asset.priority <= pAsset.priority && item.asset.hasHousingSlots())
				{
					if (Held(item) != 0L)
					{
						SparedForTest++;
					}
					else if (CityBehBuild.isGoodTileForBuilding(item.current_tile, pAsset, pCity))
					{
						return item.current_tile;
					}
				}
			}
			return null;
		}
	}
}
