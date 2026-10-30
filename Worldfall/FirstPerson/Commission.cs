using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class Commission
	{
		public int X;

		public int Y;

		public long HomeId;

		public long RealmId;

		public string Giver;

		public string HomeName;

		public string RealmName;

		public readonly List<long> Settlers = new List<long>();

		public Vector2 Site => new Vector2((float)X + 0.5f, (float)Y + 0.5f);
	}
}
