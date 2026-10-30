namespace FirstPerson
{
	internal static class AssetStyle
	{
		public static BuildingLook Classify(BuildingAsset asset)
		{
			if (asset == null)
			{
				return BuildingLook.House;
			}
			if (asset.id != null && asset.id.Contains("volcano"))
			{
				if (Models.Get("nature_volcano") == null)
				{
					return BuildingLook.Landform;
				}
				return BuildingLook.Lump;
			}
			switch (asset.building_type)
			{
			case BuildingType.Building_Tree:
				return BuildingLook.Tree;
			case BuildingType.Building_Wheat:
			case BuildingType.Building_Plant:
				return BuildingLook.Plant;
			case BuildingType.Building_Fruits:
			case BuildingType.Building_Hives:
			case BuildingType.Building_Poops:
			case BuildingType.Building_Mineral:
			case BuildingType.Building_Nature:
			case BuildingType.Building_Mob:
			case BuildingType.Building_Creep:
				return BuildingLook.Lump;
			default:
				return BuildingLook.House;
			}
		}

		public static bool IsSolid(Building b)
		{
			if (b == null || !b.isAlive() || b.asset == null)
			{
				return false;
			}
			switch (b.asset.building_type)
			{
			case BuildingType.Building_Fruits:
			case BuildingType.Building_Poops:
			case BuildingType.Building_Wheat:
			case BuildingType.Building_Plant:
			case BuildingType.Building_Creep:
				return false;
			default:
				return true;
			}
		}
	}
}
