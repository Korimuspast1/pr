using UnityEngine;

namespace FirstPerson
{
	internal sealed class ShipOrder
	{
		public enum Kind
		{
			Sail,
			Bombard
		}

		public Kind What;

		public Actor Ship;

		public long ShipId = -1L;

		public long TargetId = -1L;

		public WorldTile To;

		public Vector2 Aim;

		public Actor Target;

		public City Town;

		public string TargetName;

		public int Shots;

		public float OrderedAt = -10f;

		public float BerthAt = -10f;

		public Vector2 LastAim;

		public string Holding;

		public Vector2 Mark
		{
			get
			{
				try
				{
					if (Target != null && Target.isAlive())
					{
						return Target.current_position;
					}
					if (Town != null && Town.isAlive())
					{
						return Royal.Position(Town);
					}
				}
				catch
				{
				}
				return Aim;
			}
		}

		public bool SameTarget(ShipOrder o)
		{
			if (o != null && o.What == Kind.Bombard && What == Kind.Bombard && o.Target == Target && o.Town == Town)
			{
				if (Target == null && Town == null)
				{
					return (o.Aim - Aim).sqrMagnitude < 1f;
				}
				return true;
			}
			return false;
		}
	}
}
