using System;
using System.Collections.Generic;
using System.Reflection;

namespace FirstPerson
{
	internal static class DockGround
	{
		private const int GameIlBytes = 97;

		private static bool _installed;

		public static bool Installed => _installed;

		public static void Install()
		{
			if (!_installed)
			{
				MethodInfo method = typeof(Docks).GetMethod("update", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(float) }, null);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 97)
				{
					throw new InvalidOperationException("the game's harbour update has changed (" + num + " bytes of IL, not " + 97 + ")");
				}
				Detour.Apply(method, typeof(DockGround).GetMethod("Update", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
			}
		}

		private static void Update(Docks self, float pElapsed)
		{
			Building building = ((BaseBuildingComponent)self).building;
			if (building != null)
			{
				if (OnGround(building))
				{
					((BaseSimObject)building).getHit(1000f, pFlash: true, AttackType.Other, (BaseSimObject)null, pSkipIfShake: true, pMetallicWeapon: false, pCheckDamageReduction: true);
				}
				if (building.hasCity() && building.city.buildings.Count == 1)
				{
					((BaseSimObject)building).getHit(1000f, pFlash: true, AttackType.Other, (BaseSimObject)null, pSkipIfShake: true, pMetallicWeapon: false, pCheckDamageReduction: true);
				}
			}
		}

		private static bool OnGround(Building b)
		{
			List<WorldTile> tiles = b.tiles;
			for (int num = tiles.Count - 1; num >= 0; num--)
			{
				WorldTile worldTile = tiles[num];
				if (worldTile != null && worldTile.Type.ground)
				{
					if (worldTile == b.current_tile)
					{
						return true;
					}
					tiles.RemoveAt(num);
					if (worldTile.building == b)
					{
						worldTile.building = null;
					}
				}
			}
			return false;
		}
	}
}
