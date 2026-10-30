using System;

namespace FirstPerson
{
	internal sealed class Choice
	{
		public string Label;

		public Action Act;

		public bool Enabled = true;

		public string Note;

		public ChoiceTone Tone;

		public Choice(string label, Action act, ChoiceTone tone = ChoiceTone.Plain)
		{
			Label = label;
			Act = act;
			Tone = tone;
		}

		public Choice Off(string why)
		{
			Enabled = false;
			Note = why;
			return this;
		}
	}
}
