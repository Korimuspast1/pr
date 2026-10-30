using System;
using System.Collections.Generic;
using FirstPerson.Core;

namespace FirstPerson.Towns
{
	internal static class Rooms
	{
		internal static int Asked;

		public static bool CanEnter(Building b)
		{
			if (b == null)
			{
				return false;
			}
			try
			{
				if (HallRooms.CanEnter(b))
				{
					return true;
				}
			}
			catch (Exception e)
			{
				Hub.Fail("hall rooms", e);
			}
			return false;
		}

		public static void Seated(Building b, Interior room, Actor host, List<RoomSeat> into)
		{
			if (b == null || room == null || into == null)
			{
				return;
			}
			try
			{
				Court.Seated(b, room, host, into);
			}
			catch (Exception e)
			{
				Hub.Fail("court seats", e);
			}
		}

		public static Interior Build(RoomAsk ask)
		{
			if (ask == null || ask.B == null)
			{
				return null;
			}
			Asked++;
			Interior interior = null;
			try
			{
				interior = HallRooms.Build(ask);
			}
			catch (Exception e)
			{
				Hub.Fail("hall rooms", e);
			}
			if (interior != null)
			{
				return interior;
			}
			return interior;
		}
	}
}
