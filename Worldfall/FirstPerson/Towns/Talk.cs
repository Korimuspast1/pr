using System;
using System.Collections.Generic;

namespace FirstPerson.Towns
{
	internal static class Talk
	{
		internal static int Calls;

		public static void Choices(Conversation c, List<Choice> list)
		{
			if (c == null || list == null)
			{
				return;
			}
			Calls++;
			try
			{
				Planning.Choices(c, list);
			}
			catch (Exception e)
			{
				Hub.Fail("planning talk", e);
			}
			try
			{
				CourtTalk.Choices(c, list);
			}
			catch (Exception e2)
			{
				Hub.Fail("court talk", e2);
			}
			try
			{
				Freedom.Choices(c, list);
			}
			catch (Exception e3)
			{
				Hub.Fail("freedom talk", e3);
			}
			try
			{
				LandPlot.Choices(c, list);
			}
			catch (Exception e4)
			{
				Hub.Fail("land talk", e4);
			}
		}

		public static string Greeting(Conversation c)
		{
			Actor actor = c?.With;
			Actor actor2 = c?.You;
			if (actor == null || actor2 == null)
			{
				return null;
			}
			try
			{
				if (actor.lover == actor2 || (actor.family != null && actor.family == actor2.family))
				{
					return null;
				}
				Kingdom kingdom = (Rep.Civ(actor.kingdom) ? actor.kingdom : null);
				RepLevel repLevel = ((kingdom != null) ? Rep.Standing(actor2, kingdom) : RepLevel.Stranger);
				if (kingdom != null && actor.isKing())
				{
					switch (repLevel)
					{
					case RepLevel.Outlaw:
						return Dialogue.Pick("Every town in my realm knows your name, and none kindly. Speak.", "An outlaw before my throne. Speak, and be brief.");
					case RepLevel.Distrusted:
						return Dialogue.Pick("My towns speak ill of you. Choose your words.", "I've heard of you. Little of it good.");
					default:
						return null;
					}
				}
				City city = TownOf(c);
				if (city == null)
				{
					if (repLevel == RepLevel.Outlaw)
					{
						return Dialogue.Pick("I know who you are. Keep your distance.", "Outlaw. Say your piece.");
					}
					return null;
				}
				if (Rep.Rules(actor2, city))
				{
					return null;
				}
				RepLevel repLevel2 = Rep.Level(actor2, city);
				if (repLevel2 >= RepLevel.Honored && Rep.RealmOf(city) != null && Rep.Standing(actor2, Rep.RealmOf(city)) == RepLevel.Outlaw)
				{
					return Dialogue.Pick("The crown wants your head. Not here, though.", "They call you an outlaw in the capital. Here you're one of us.");
				}
				switch (repLevel2)
				{
				case RepLevel.Outlaw:
					return Dialogue.Pick("You've got nerve, showing your face here.", "Speak, then go. The whole town knows you.", "Talk fast. People are watching.");
				case RepLevel.Distrusted:
					return Dialogue.Pick("We know what you did here. Keep it short.", "Say what you need and move on.", "I'll talk. Don't expect a smile.");
				}
			}
			catch (Exception e)
			{
				Hub.Fail("greeting", e);
			}
			return null;
		}

		public static City TownOf(Conversation c)
		{
			try
			{
				if (c?.With != null && c.With.city != null && c.With.city.isAlive())
				{
					return c.With.city;
				}
			}
			catch
			{
			}
			return Hub.Here;
		}
	}
}
