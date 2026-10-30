using UnityEngine;

namespace FirstPerson
{
	internal struct RestSeat
	{
		public RestSeatKind Kind;

		public Vector2 At;

		public float Heading;

		public float Floor;

		public float Top;

		public bool Hang;

		public bool Knee;

		public int Spot;

		public float Height => Mathf.Max(0f, Top - Floor);
	}
}
