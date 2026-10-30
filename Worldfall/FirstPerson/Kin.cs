namespace FirstPerson
{
	internal sealed class Kin
	{
		public string Role;

		public long Id;

		public string Name;

		public bool Female;

		public int Born = int.MinValue;

		public int Died = int.MinValue;

		public bool Dead => Died != int.MinValue;

		public Kin Copy(string role)
		{
			return new Kin
			{
				Role = role,
				Id = Id,
				Name = Name,
				Female = Female,
				Born = Born,
				Died = Died
			};
		}
	}
}
