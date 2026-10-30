namespace FirstPerson.Core
{
	public sealed class MeshPart
	{
		public string Name;

		public int Parent;

		public Vec3 Pivot;

		public float[] Pos;

		public float[] Nrm;

		public int[] Tri;

		public byte[] Slot;

		public byte[] Axis;

		public float[] Uv;

		public int Version;

		public int VertexCount => Pos.Length / 3;

		public int TriangleCount => Tri.Length / 3;
	}
}
