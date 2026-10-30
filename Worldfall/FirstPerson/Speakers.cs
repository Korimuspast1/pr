using FirstPerson.Core;

namespace FirstPerson
{
	internal static class Speakers
	{
		public static Mumble.Speaker Of(Actor a, out bool mute)
		{
			Mumble.Speaker result = default(Mumble.Speaker);
			mute = false;
			if (a == null)
			{
				return result;
			}
			try
			{
				long id = a.data.id;
				result.Seed = (int)(id ^ (id >> 32));
			}
			catch
			{
			}
			result.Female = Models.IsFemale(a);
			Voices.AgeBand ageBand = Personas.AgeOf(a);
			result.Child = ageBand == Voices.AgeBand.Child;
			result.Young = ageBand == Voices.AgeBand.Young;
			result.Elder = ageBand == Voices.AgeBand.Elder;
			try
			{
				bool flag = a.hasSubspeciesTrait("wernicke_area");
				result.Broken = !flag;
				result.Flat = !a.hasSubspeciesTrait("amygdala") && !a.hasEmotions();
				mute = a.hasTrait("mute") || (!flag && !a.hasSubspeciesTrait("advanced_hippocampus") && !a.hasSubspeciesTrait("prefrontal_cortex"));
				bool flag2 = a.hasTrait("hotheaded") || a.hasTrait("bloodlust");
				bool flag3 = a.hasTrait("psychopath") || a.hasTrait("evil");
				int num = Dialogue.SafeHappiness(a);
				result.Tone = ((num >= 50) ? Mumble.Tone.Happy : ((num <= -60) ? Mumble.Tone.Dark : ((num <= -20) ? ((flag2 || flag3) ? Mumble.Tone.Angry : Mumble.Tone.Sad) : Mumble.Tone.Normal)));
				result.Pace = (flag2 ? 1.12f : (a.hasTrait("wise") ? 0.9f : (a.hasTrait("stupid") ? 0.88f : 1f)));
			}
			catch
			{
			}
			return result;
		}

		public static Mumble.Speaker Of(Conversation c)
		{
			bool mute;
			Mumble.Speaker result = Of(c.With, out mute);
			if (c.Slots != null)
			{
				Voices.Persona persona = c.Persona;
				result.Female = persona.Female;
				result.Child = persona.Child;
				result.Young = persona.Age == Voices.AgeBand.Young;
				result.Elder = persona.Age == Voices.AgeBand.Elder;
				result.Tone = ((persona.Mood == Voices.Mood.Happy) ? Mumble.Tone.Happy : ((persona.Mood == Voices.Mood.Sad) ? Mumble.Tone.Sad : ((persona.Mood == Voices.Mood.Angry) ? Mumble.Tone.Angry : ((persona.Mood == Voices.Mood.Dark) ? Mumble.Tone.Dark : Mumble.Tone.Normal))));
				result.Pace = 0.7f * PaceOf(persona.First) + 0.3f * PaceOf(persona.Second);
			}
			result.Broken = c.Voice.Broken;
			result.Flat = c.Voice.Flat;
			return result;
		}

		private static float PaceOf(Voices.Temper t)
		{
			switch (t)
			{
			case Voices.Temper.Hothead:
				return 1.12f;
			case Voices.Temper.Clever:
				return 1.06f;
			case Voices.Temper.Sly:
				return 1.05f;
			case Voices.Temper.Cruel:
			case Voices.Temper.Ambitious:
				return 1.04f;
			case Voices.Temper.Greedy:
				return 1.03f;
			case Voices.Temper.Wise:
				return 0.9f;
			case Voices.Temper.Dull:
				return 0.88f;
			case Voices.Temper.Pious:
			case Voices.Temper.Gentle:
				return 0.95f;
			case Voices.Temper.Hardened:
				return 0.96f;
			case Voices.Temper.Easy:
				return 0.97f;
			default:
				return 1f;
			}
		}
	}
}
