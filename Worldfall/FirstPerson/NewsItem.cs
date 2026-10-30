using UnityEngine;

namespace FirstPerson
{
	internal sealed class NewsItem
	{
		public int Seq;

		public string Kind;

		public string Group;

		public string Text;

		public string Icon;

		public double Time;

		public bool HasPlace;

		public Vector2 At;

		public Actor Unit;

		public bool Mine;

		public NewsTone Tone;

		public string Key;

		public bool Read;

		public string About;

		public bool Late;

		public float Heard;

		public bool War
		{
			get
			{
				if (!(Group == "wars") && !(Kind == "alliance_new") && !(Kind == "alliance_dissolved") && !(Kind == "fp_siege") && !(Kind == "fp_taken"))
				{
					return Kind == "fp_won";
				}
				return true;
			}
		}

		public bool Where(out Vector2 at)
		{
			at = At;
			try
			{
				if (Unit != null && Unit.isAlive())
				{
					at = Unit.current_position;
					return true;
				}
			}
			catch
			{
			}
			return HasPlace;
		}
	}
}
