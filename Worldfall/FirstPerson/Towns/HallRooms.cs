using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class HallRooms
	{
		private static readonly Anchors.Spot[] None = new Anchors.Spot[0];

		internal static string RoomFor(Building b)
		{
			if (b == null || b.asset == null || b.asset.type != "type_hall")
			{
				return null;
			}
			if (!HallModels.Parse(b.asset.id, out var race, out var level))
			{
				return null;
			}
			bool flag = false;
			if (level == 2)
			{
				try
				{
					flag = TownTier.IsCapital(b.city);
				}
				catch
				{
				}
			}
			if (!flag)
			{
				return $"tw_room_hall_{race}_{level}";
			}
			return "tw_room_keep_" + race;
		}

		public static bool CanEnter(Building b)
		{
			string text = RoomFor(b);
			if (text != null && Models.Ready)
			{
				return Models.Has(text);
			}
			return false;
		}

		public static Interior Build(RoomAsk ask)
		{
			string text = RoomFor(ask.B);
			if (text == null || !Models.Ready)
			{
				return null;
			}
			MeshModel meshModel = (Models.PackReady(text) ? Models.Get(text) : null);
			if (meshModel == null)
			{
				Log.Info("towns: " + text + " isn't loaded yet; the plain room this once");
				return null;
			}
			uint kingdom = 0u;
			try
			{
				Kingdom kingdom2 = ask.B.kingdom;
				if (kingdom2 != null && kingdom2.isCiv())
				{
					Color color = Royal.ColorOf(kingdom2);
					kingdom = TerrainCache.Rgb((int)(color.r * 255f), (int)(color.g * 255f), (int)(color.b * 255f));
				}
			}
			catch
			{
			}
			Interior interior = HallRoomBuilder.Build(meshModel, kingdom);
			float fill = 0.5f;
			try
			{
				City city = ask.B.city;
				if (city != null)
				{
					fill = Mathf.Clamp01((float)Economy.Treasury(city) / 400f);
				}
			}
			catch
			{
			}
			HallRoomBuilder.Treasury(interior, fill);
			ask.Name = ((HallRoomBuilder.KindOf(interior) == "keep") ? "keep" : "hall");
			return interior;
		}

		public static Anchors.Spot[] Spots(Interior room, string name)
		{
			HallRoomBuilder.Spot[] array = HallRoomBuilder.Spots(room, name);
			if (array.Length == 0)
			{
				return None;
			}
			Anchors.Spot[] array2 = new Anchors.Spot[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array2[i] = new Anchors.Spot
				{
					At = array[i].At,
					Yaw = array[i].Yaw,
					HasYaw = array[i].HasYaw
				};
			}
			return array2;
		}

		public static string KindOf(Interior room)
		{
			return HallRoomBuilder.KindOf(room);
		}

		public static bool IsHallRoom(Interior room)
		{
			return KindOf(room) != null;
		}
	}
}
