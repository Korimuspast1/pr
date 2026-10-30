using System;
using UnityEngine;

namespace FirstPerson
{
	internal struct AbilityUse
	{
		public Actor Self;

		public Vector2 At;

		public Actor Target;

		public Vector2 Dir;

		public Action<string> Toast;
	}
}
