namespace FirstPerson
{
	internal sealed class Hireling
	{
		public Actor Guard;

		public double Until;

		public long Id;

		public bool Still
		{
			get
			{
				try
				{
					return Guard != null && Guard.isAlive() && Guard.data != null && (Id == 0L || Guard.data.id == Id);
				}
				catch
				{
					return false;
				}
			}
		}
	}
}
