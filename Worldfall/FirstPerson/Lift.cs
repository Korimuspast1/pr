using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class Lift
	{
		public enum Stage
		{
			Boarding,
			Aboard,
			Landing
		}

		public Stage Now;

		public Actor Ship;

		public readonly List<Actor> Boarding = new List<Actor>();

		public WorldTile Pickup;

		public WorldTile Shore;

		public WarFlag Flag;

		public WorldTile Drop;

		public WorldTile Beach;

		public WorldTile DropFor;

		public float ArrivedAt = -1f;

		public float StageAt;

		public int Aboard;

		public bool Auto;

		public readonly List<Actor> Riders = new List<Actor>();

		public WorldTile LastTile;

		public Vector2 MovedFrom;

		public float MovedAt = -1f;
	}
}
