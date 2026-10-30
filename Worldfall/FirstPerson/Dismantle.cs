using UnityEngine;

namespace FirstPerson
{
	internal static class Dismantle
	{
		public const float ShowFor = 6f;

		public static Building Last;

		public static float At = -100f;

		public static void Struck(BaseSimObject target)
		{
			try
			{
				if (target == null || !target.isBuilding())
				{
					return;
				}
				Building b = target.b;
				if (b != null && b.asset != null)
				{
					BuildingType building_type = b.asset.building_type;
					if (building_type != BuildingType.Building_Tree && building_type != BuildingType.Building_Mineral && building_type != BuildingType.Building_Plant)
					{
						Last = b;
						At = Time.unscaledTime;
					}
				}
			}
			catch
			{
			}
		}

		public static Building Current()
		{
			Building last = Last;
			if (last == null || Time.unscaledTime - At > 6f)
			{
				return null;
			}
			try
			{
				if (!last.isAlive())
				{
					return null;
				}
				return last;
			}
			catch
			{
				return null;
			}
		}
	}
}
