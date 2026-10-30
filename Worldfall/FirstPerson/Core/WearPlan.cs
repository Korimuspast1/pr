namespace FirstPerson.Core
{
	public struct WearPlan
	{
		public ulong Soldier;

		public ulong Captain;

		public ulong Royal;

		public ulong Court;

		public ulong Honour;

		public ulong Robe;

		public bool Halberd;

		public bool ShowHair;

		public bool HideHair;

		public bool HideBeard;

		public bool HideHats;

		public bool HideUnder;

		public bool HideTallHair;

		public bool HideArmourHead;

		public SashGear Sash;

		public KingdomStyle Style;

		public ulong this[RegaliaKit.Set s]
		{
			get
			{
				switch (s)
				{
				default:
					return Robe;
				case RegaliaKit.Set.Honour:
					return Honour;
				case RegaliaKit.Set.Court:
					return Court;
				case RegaliaKit.Set.Royal:
					return Royal;
				case RegaliaKit.Set.Captain:
					return Captain;
				case RegaliaKit.Set.Soldier:
					return Soldier;
				}
			}
			set
			{
				switch (s)
				{
				case RegaliaKit.Set.Soldier:
					Soldier = value;
					break;
				case RegaliaKit.Set.Captain:
					Captain = value;
					break;
				case RegaliaKit.Set.Royal:
					Royal = value;
					break;
				case RegaliaKit.Set.Court:
					Court = value;
					break;
				case RegaliaKit.Set.Honour:
					Honour = value;
					break;
				default:
					Robe = value;
					break;
				}
			}
		}

		public bool Any
		{
			get
			{
				if ((Soldier | Captain | Royal | Court | Honour | Robe) == 0L)
				{
					return Halberd;
				}
				return true;
			}
		}
	}
}
