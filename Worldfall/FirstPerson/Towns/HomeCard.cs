using System.Collections.Generic;

namespace FirstPerson.Towns
{
	internal static class HomeCard
	{
		public static bool Open;

		public static Building House;

		public static readonly List<Actor> Family = new List<Actor>();

		public static void Show(Building house, Actor you)
		{
			House = house;
			Open = house != null;
			Family.Clear();
			Actor owner = HomeLock.OwnerOf(house) ?? you;
			Family.AddRange(HomeLock.FamilyOf(owner));
		}

		public static void Close()
		{
			Open = false;
			House = null;
			Family.Clear();
		}

		public static bool Yours(Building b, Actor you)
		{
			if (b != null && you != null)
			{
				return Homes.OwnedBy(b, you);
			}
			return false;
		}
	}
}
