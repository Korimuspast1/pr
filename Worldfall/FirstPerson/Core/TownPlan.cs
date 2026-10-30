using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class TownPlan
	{
		public int Id;

		public float HubX;

		public float HubY;

		public float PlazaRadius = 1.7f;

		public readonly List<PathPoint> Doors = new List<PathPoint>();

		public readonly List<PathPoint> Neighbours = new List<PathPoint>();
	}
}
