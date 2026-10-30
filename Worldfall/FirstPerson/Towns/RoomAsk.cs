using System;
using FirstPerson.Core;

namespace FirstPerson.Towns
{
	internal sealed class RoomAsk
	{
		public Building B;

		public Actor Host;

		public int Width;

		public int Depth;

		public int Seed;

		public int Tier;

		public uint Wall;

		public uint Roof;

		public string Race;

		public string Style;

		public Func<string, MeshModel> Models;

		public string Name;
	}
}
