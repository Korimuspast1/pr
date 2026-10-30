namespace FirstPerson
{
	internal sealed class Suitor
	{
		public long Body;

		public int Affection;

		public int Done;

		public int Need;

		public int Kinds;

		public int Last = -1;

		public int Same;

		public double Stamp;

		public bool Asked;

		public int Extra;

		public long Rival;

		public int Day = -1;

		public int Gained;

		public int Exchanges;

		public bool Gift;

		public int Best;

		public int Known;

		public int LastTalk = -1;

		public int CoolUntil = -1;

		public bool Knows
		{
			get
			{
				return (Known & 1) != 0;
			}
			set
			{
				if (value)
				{
					Known |= 1;
				}
				else
				{
					Known &= -2;
				}
			}
		}

		public int Wanted => Need + Extra;
	}
}
