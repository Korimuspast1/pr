namespace FirstPerson.Core
{
	public struct VoxelObject
	{
		public VoxelShape Shape;

		public Vec3 Origin;

		public Vec3 U;

		public Vec3 V;

		public Vec3 N;

		public float SizeU;

		public float SizeV;

		public float SizeN;

		public uint Flash;

		public uint Multiply;

		public int Tag;

		public bool Overlay;

		public bool NoShadow;

		public bool Emissive;

		public float Glow;

		public float SortDepth;

		public static VoxelObject Standing(VoxelShape shape, float x, float y, float z, float width, float height, float pivotX, float rightX, float rightY)
		{
			VoxelObject result = new VoxelObject
			{
				Shape = shape,
				Tag = -1
			};
			result.U = new Vec3(rightX, rightY, 0f).Normalized();
			result.V = new Vec3(0f, 0f, 1f);
			result.N = Vec3.Cross(result.U, result.V);
			result.SizeU = width / (float)shape.Width;
			result.SizeV = height / (float)shape.Height;
			result.SizeN = result.SizeU;
			result.Origin = new Vec3(x, y, z) - result.U * (pivotX * width);
			return result;
		}
	}
}
