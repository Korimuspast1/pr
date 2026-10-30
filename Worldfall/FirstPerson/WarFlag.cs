using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class WarFlag
	{
		public int Number;

		public WorldTile Tile;

		public readonly List<Actor> Band = new List<Actor>();

		public readonly Dictionary<Actor, long> Ids = new Dictionary<Actor, long>();

		public int Holding;

		public int Coming;

		public int Fighting;

		public int Eating;

		public int Fallen;

		public int AtSea;

		public Vector2 ComingFrom;

		public float Born;

		public bool HadBand;

		public bool ToldFallen;

		public WarOrder Order;

		public float Radius = 8f;

		public bool Quick;

		public BaseSimObject Target;

		public long TargetId;

		public Vector2 TargetLast;

		public int Burned;

		public Vector2 P => new Vector2((float)Tile.x + 0.5f, (float)Tile.y + 0.5f);

		public Color Color
		{
			get
			{
				if (!Quick)
				{
					return WarMap.FlagColor(Number);
				}
				return WarMap.QuickColor;
			}
		}
	}
}
