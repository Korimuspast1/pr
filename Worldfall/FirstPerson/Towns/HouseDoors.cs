namespace FirstPerson.Towns
{
	internal static class HouseDoors
	{
		public static bool MayWalkIn(Building b, Actor you)
		{
			if (b == null || you == null)
			{
				return false;
			}
			try
			{
				if (Homes.OwnedBy(b, you) || Workbench.HomeOf(you) == b)
				{
					return true;
				}
				if (Rep.Reigns(you, b.kingdom))
				{
					return true;
				}
				if (!HomeLock.Locked(b))
				{
					return true;
				}
				Actor actor = HomeLock.OwnerOf(b);
				return actor == null || HomeLock.IsFamily(you, actor);
			}
			catch
			{
			}
			return true;
		}

		public static bool MaySleepIn(Building b, Actor you)
		{
			return MayWalkIn(b, you);
		}
	}
}
