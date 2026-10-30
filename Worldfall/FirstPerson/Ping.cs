using UnityEngine;

namespace FirstPerson
{
	internal sealed class Ping
	{
		public PingKind Kind;

		public Vector3 At;

		public Actor Actor;

		public BaseSimObject On;

		public float Born;

		public float Until;

		public bool Far;
	}
}
