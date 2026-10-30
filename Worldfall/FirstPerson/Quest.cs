namespace FirstPerson
{
	internal sealed class Quest
	{
		public QuestKind Kind;

		public long Giver;

		public string GiverName;

		public long Holder;

		public long Target;

		public string TargetName;

		public long Target2;

		public string Target2Name;

		public string Thing;

		public int Need;

		public int Have;

		public int X = -1;

		public int Y = -1;

		public string Place;

		public int Money;

		public int Renown;

		public int Xp;

		public int Opinion;

		public double Deadline;

		public int Stage;

		public bool FromBoard;

		public double Wait;

		public bool Bench;

		public int Taken;

		public int Posted = -1;

		public string Extra;

		public const int UnderWay = 0;

		public const int ReadyToReport = 1;

		public const int Finished = 2;

		public const int FailedStage = 3;
	}
}
