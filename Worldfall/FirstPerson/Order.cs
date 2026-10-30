using UnityEngine;

namespace FirstPerson
{
	internal sealed class Order
	{
		public OrderKind Kind;

		public string Title;

		public string Detail;

		public Vector2 Where;

		public bool HasWhere;

		public City Target;

		public City Home;

		public Kingdom Realm;

		public Kingdom Enemy;

		public Actor King;

		public Actor Captain;

		public string From;

		public int Soldiers;

		public int Wanted;

		public int Foes;

		public int Flag;

		public bool YouLead;

		public string Key;

		public void MakeKey()
		{
			Key = Kind.ToString() + ":" + ((Kind == OrderKind.Patrol) ? Id(Home) : ((Kind != OrderKind.Muster) ? Id(Target) : 0)) + ":" + Id(Enemy) + ":" + Flag;
			long Id(NanoObject n)
			{
				try
				{
					return n?.getID() ?? 0;
				}
				catch
				{
					return 0L;
				}
			}
		}

		public static string NameOf(NanoObject n)
		{
			try
			{
				return n?.name;
			}
			catch
			{
				return null;
			}
		}
	}
}
