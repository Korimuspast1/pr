namespace FirstPerson.Core
{
	public struct GearLook
	{
		public HeadGear Head;

		public NeckGear Neck;

		public SashGear Sash;

		public BackGear Back;

		public HandGear Hand;

		public BodyGear Body;

		public KingdomStyle Style;

		public bool Any;

		public bool ReplacesHelmet => (Head & (HeadGear.CrownHelm | HeadGear.Hat | HeadGear.Hood)) != 0;

		public override string ToString()
		{
			if (!Any)
			{
				return "nothing";
			}
			return $"head {Head}, neck {Neck}, sash {Sash}, back {Back}, hand {Hand}, body {Body}, style {Style}";
		}
	}
}
