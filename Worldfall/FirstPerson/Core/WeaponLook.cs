namespace FirstPerson.Core
{
	public struct WeaponLook
	{
		public WeaponKind Kind;

		public uint Head;

		public uint Handle;

		public uint Accent;

		public bool Flame;

		public bool Ice;

		public bool Glow;

		public string Model;

		public uint String => ViewMeshes.HeldOf(Model).String;

		public bool StringGlow => ViewMeshes.HeldOf(Model).StringGlow;

		public uint GlowLight => ViewMeshes.HeldOf(Model).Glow;
	}
}
