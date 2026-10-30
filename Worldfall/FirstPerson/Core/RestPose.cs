namespace FirstPerson.Core
{
	public struct RestPose
	{
		public RestStyle Style;

		public float Amount;

		public float Seat;

		public float Heading;

		public float X;

		public float Y;

		public bool Hang;

		public bool On
		{
			get
			{
				if (Style != RestStyle.None)
				{
					return Amount > 0f;
				}
				return false;
			}
		}

		public static RestPose Of(RestStyle style, float amount, float heading = float.NaN, float seat = 0f, bool hang = false)
		{
			return new RestPose
			{
				Style = style,
				Amount = amount,
				Seat = seat,
				Hang = hang,
				Heading = heading,
				X = float.NaN,
				Y = float.NaN
			};
		}

		public static bool Sitting(RestStyle s)
		{
			if (s != RestStyle.Seat && s != RestStyle.Ground && s != RestStyle.SitBack)
			{
				return s == RestStyle.CrossLegged;
			}
			return true;
		}
	}
}
