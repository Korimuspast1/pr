using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BlockEvent
	{
		public int Serial;

		public Stop Kind;

		public Clash Material;

		public bool YourBlow;

		public bool OnYou;

		public bool Shot;

		public Actor Defender;

		public BaseSimObject Attacker;

		public Vec3 At;

		public Vector2 Toward;

		public float Weight;

		public float Time;

		public int Count = 1;

		public float Age => UnityEngine.Time.unscaledTime - Time;
	}
}
