using System.Collections.Generic;

namespace FirstPerson
{
	internal sealed class Record
	{
		public long Id;

		public readonly List<Kin> Kin = new List<Kin>();

		public readonly List<Memory> Memories = new List<Memory>();

		public readonly List<Regard> Regards = new List<Regard>();

		public long LastStamp;

		public bool HasLast;

		public int LastIndex;

		public int LastBonus;

		public double LastTime;

		public bool KinDirty;

		public bool MemDirty;

		public bool YouDirty;

		public int Swept;

		public Kin Get(string role)
		{
			for (int i = 0; i < Kin.Count; i++)
			{
				if (Kin[i].Role == role)
				{
					return Kin[i];
				}
			}
			return null;
		}

		public IEnumerable<Kin> All(string role)
		{
			for (int i = 0; i < Kin.Count; i++)
			{
				if (Kin[i].Role == role)
				{
					yield return Kin[i];
				}
			}
		}

		public Kin Child(long id)
		{
			for (int i = 0; i < Kin.Count; i++)
			{
				if (Kin[i].Role == "ch" && Kin[i].Id == id)
				{
					return Kin[i];
				}
			}
			return null;
		}
	}
}
