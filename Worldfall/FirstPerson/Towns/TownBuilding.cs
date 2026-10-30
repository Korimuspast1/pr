using FirstPerson.Core;

namespace FirstPerson.Towns
{
	internal sealed class TownBuilding
	{
		public TownScene Scene;

		public Building B;

		public MeshInstance Inst;

		public MeshModel Full;

		public MeshModel Model;

		public string Family;

		public float X;

		public float Y;

		public float Ground;

		public float Heading;

		public float Scale;

		public bool Mirrored;

		public bool Lit;

		public bool Ruin;

		public bool Seen;

		public float Dist;

		public Vec3 World(Vec3 local)
		{
			return Anchors.World(Inst, local);
		}
	}
}
