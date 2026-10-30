namespace FirstPerson
{
	internal sealed class TreeNode
	{
		public long Id;

		public string Name;

		public string Role;

		public Actor Actor;

		public bool Female;

		public bool Dead;

		public int Born = int.MinValue;

		public int Died = int.MinValue;
	}
}
