using UnityEngine;

namespace FirstPerson
{
	internal sealed class BagGain
	{
		public string Id;

		public int Amount;

		public Vector3 From;

		public float Born;

		public int Order;

		public int Of;

		internal Vector2 LastSeen;

		internal bool Seen;

		internal bool Landed;

		public const float Stagger = 0.09f;

		public const float Pop = 0.24f;

		public const float Launch = 0.52f;

		public const float Flight = 0.46f;

		public const float Linger = 0.8f;

		public float Start => Born + (float)Order * 0.09f;

		public float LandAt => Start + 0.52f + 0.46f;

		public float Gone => LandAt + 0.8f;
	}
}
