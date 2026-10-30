using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class HouseWorld
	{
		internal struct Placement
		{
			public MeshModel Model;

			public float X;

			public float Y;

			public float Ground;

			public float Scale;

			public bool Mirror;

			public int Face;

			public float Seen;

			public WorldTile Tile;

			public BuildingAsset Asset;
		}

		private static readonly Dictionary<Building, Placement> Placed = new Dictionary<Building, Placement>();

		public static void Note(Building b, MeshModel model, float x, float y, float ground, float scale, bool mirror, int face = 0)
		{
			if (b != null && model != null)
			{
				if (Placed.Count > 600)
				{
					Placed.Clear();
				}
				Placed[b] = new Placement
				{
					Model = model,
					X = x,
					Y = y,
					Ground = ground,
					Scale = scale,
					Mirror = mirror,
					Face = (face & 3),
					Seen = Time.unscaledTime,
					Tile = b.current_tile,
					Asset = b.asset
				};
			}
		}

		public static HousePlan PlanFor(Placement p)
		{
			return HousePlanTurn.Of(p.Model, Facing.PlanTurns(p.Face, p.Mirror));
		}

		public static bool Of(Building b, out Placement p)
		{
			if (b != null && Placed.TryGetValue(b, out p) && p.Tile == b.current_tile && p.Asset == b.asset)
			{
				return true;
			}
			p = default(Placement);
			return false;
		}

		public static IEnumerable<KeyValuePair<Building, Placement>> Near()
		{
			float now = Time.unscaledTime;
			foreach (KeyValuePair<Building, Placement> item in Placed)
			{
				if (now - item.Value.Seen < 1f)
				{
					yield return item;
				}
			}
		}

		public static HousePlan PlanOf(Building b)
		{
			if (!Of(b, out var p))
			{
				return null;
			}
			return PlanFor(p);
		}

		public static void Clear()
		{
			Placed.Clear();
		}
	}
}
