namespace FirstPerson.Core
{
	public sealed class Carried
	{
		public string Name;

		public CarryStyle Style;

		public bool Palm;

		public bool Edible;

		public bool Drink;

		public Vec3 HandR;

		public Vec3 HandL;

		public bool LeftHand;

		public float Lean;

		public Vec3 Mouth;

		public string Hold;

		public WeaponKind Swing;

		public MeshModel Model(int detail)
		{
			return ViewMeshes.OwnModel(Name, detail);
		}
	}
}
