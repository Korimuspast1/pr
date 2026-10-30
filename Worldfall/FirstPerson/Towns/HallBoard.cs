using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class HallBoard
	{
		private sealed class Placed
		{
			public Building Hall;

			public string Family;

			public Vector2 At;

			public Vector2 HallAt;

			public float Yaw;
		}

		private static readonly Dictionary<long, Placed> ByTown = new Dictionary<long, Placed>();

		private static readonly Dictionary<long, string> Why = new Dictionary<long, string>();

		private static readonly object Lock = new object();

		public static void Building(TownBuilding tb)
		{
			if (tb == null || tb.B == null || tb.Full == null || tb.Inst == null)
			{
				return;
			}
			string family = tb.Family;
			if (family == null || (!family.StartsWith("tw_hall_", StringComparison.Ordinal) && !family.StartsWith("tw_keep_", StringComparison.Ordinal)))
			{
				return;
			}
			City city;
			try
			{
				city = tb.B.city;
			}
			catch
			{
				return;
			}
			if (city == null)
			{
				return;
			}
			long id = city.data.id;
			lock (Lock)
			{
				if (tb.Ruin)
				{
					ByTown.Remove(id);
					return;
				}
				if (ByTown.TryGetValue(id, out var value) && value.Hall == tb.B && value.Family == family)
				{
					return;
				}
			}
			Anchors.Spot[] array = Anchors.Spots(tb.Full, "a_sign");
			if (array.Length == 0)
			{
				return;
			}
			float yaw = Anchors.WorldHeading(tb.Heading, array[0].HasYaw ? array[0].Yaw : 0f, tb.Mirrored);
			string text = null;
			Vector2 at = default(Vector2);
			bool flag = false;
			float[] array2 = new float[2] { 1f, -1f };
			foreach (float num in array2)
			{
				Vec3 vec = Anchors.World(local: new Vec3(array[0].At.X * num, array[0].At.Y, array[0].At.Z), inst: tb.Inst);
				at = new Vector2(vec.X, vec.Y);
				text = Blocked(at, yaw, tb.B);
				if (text == null)
				{
					flag = true;
					break;
				}
			}
			lock (Lock)
			{
				if (flag)
				{
					ByTown[id] = new Placed
					{
						Hall = tb.B,
						Family = family,
						At = at,
						HallAt = new Vector2(tb.X, tb.Y),
						Yaw = yaw
					};
					Why.Remove(id);
				}
				else
				{
					ByTown.Remove(id);
					Why[id] = text;
				}
			}
		}

		public static bool Spot(City c, out Vector2 at, out float yaw)
		{
			at = default(Vector2);
			yaw = 0f;
			if (c == null)
			{
				return false;
			}
			Placed value;
			lock (Lock)
			{
				if (!ByTown.TryGetValue(c.data.id, out value))
				{
					return false;
				}
			}
			try
			{
				if (value.Hall == null || !value.Hall.isAlive() || value.Hall.city != c || value.Hall.isRuin())
				{
					lock (Lock)
					{
						ByTown.Remove(c.data.id);
					}
					return false;
				}
			}
			catch
			{
				return false;
			}
			at = value.At;
			yaw = value.Yaw;
			return true;
		}

		internal static bool ForTest(City c, out Vector2 hall, out Vector2 board, out string why)
		{
			hall = (board = default(Vector2));
			why = null;
			lock (Lock)
			{
				if (c != null)
				{
					Why.TryGetValue(c.data.id, out why);
				}
				if (c == null || !ByTown.TryGetValue(c.data.id, out var value))
				{
					return false;
				}
				hall = value.HallAt;
				board = value.At;
				return true;
			}
		}

		private static string Blocked(Vector2 at, float yaw, Building hall)
		{
			Vector2 vector = new Vector2(0f - Mathf.Sin(yaw), Mathf.Cos(yaw));
			float[] array = new float[3] { -0.8f, 0f, 0.8f };
			foreach (float num in array)
			{
				Vector2 vector2 = at + vector * num;
				WorldTile tile = World.world.GetTile(Mathf.FloorToInt(vector2.x), Mathf.FloorToInt(vector2.y));
				if (tile == null || tile.main_type == null)
				{
					return $"off the map at {vector2.x:F1},{vector2.y:F1}";
				}
				if (tile.main_type.liquid)
				{
					return $"water at {vector2.x:F1},{vector2.y:F1}";
				}
				if (tile.main_type.block)
				{
					return $"{tile.main_type.id} at {vector2.x:F1},{vector2.y:F1}";
				}
				if (tile.building != null && tile.building != hall && AssetStyle.IsSolid(tile.building))
				{
					return $"{tile.building.asset?.id} at {vector2.x:F1},{vector2.y:F1}";
				}
			}
			return null;
		}
	}
}
