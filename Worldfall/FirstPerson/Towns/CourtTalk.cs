using System.Collections.Generic;

namespace FirstPerson.Towns
{
	internal static class CourtTalk
	{
		public static void Choices(Conversation c, List<Choice> list)
		{
			Actor you = c.You;
			Actor with = c.With;
			if (you == null || with == null)
			{
				return;
			}
			if (CourtThrone.Holding)
			{
				list.Add(new Choice("That is all for today.", delegate
				{
					c.LastTopic = "court_over";
					CourtThrone.Stop();
					c.EndAfter = true;
					c.Say(Dialogue.Pick("As you wish, Your Majesty.", "*bows* Your Majesty."));
				}, ChoiceTone.Leave));
			}
			if (!Royal.IsKing(you) || with == you)
			{
				return;
			}
			Kingdom kingdom = Court.KingdomOf(you);
			bool flag = false;
			try
			{
				flag = kingdom != null && with.isAlive() && with.kingdom == kingdom && with.isAdult() && !with.isKing();
			}
			catch
			{
			}
			if (!flag)
			{
				return;
			}
			if (Court.SeatOf(with, out var seat))
			{
				list.Add(new Choice("Your counsel", delegate
				{
					Counsel(c, seat);
				}, ChoiceTone.Quest));
			}
			list.Add(new Choice("A seat on my council", delegate
			{
				SeatMenu(c);
			}));
		}

		private static void Counsel(Conversation c, Seat seat)
		{
			c.LastTopic = "counsel";
			Kingdom home = Court.KingdomOf(c.You);
			c.Say(Advice(home, c.You, seat));
			Choice item = new Choice("Show me", delegate
			{
				c.LastTopic = "counsel_open";
				c.End();
				Court.OpenCouncil(seat);
			}, ChoiceTone.Quest);
			c.Offer(new List<Choice>
			{
				item,
				Dialogue.Back(c),
				Dialogue.Goodbye(c)
			});
		}

		internal static string Advice(Kingdom home, Actor you, Seat seat)
		{
			if (home == null)
			{
				return "...";
			}
			long num = Royal.KillsToSpend(home);
			List<Kingdom> list = Royal.Rivals(home);
			switch (seat)
			{
			case Seat.Marshal:
			{
				int num3 = Royal.Warriors(home);
				Kingdom kingdom3 = null;
				foreach (Kingdom item in list)
				{
					if (Royal.AtWar(home, item) && (kingdom3 == null || Royal.Warriors(item) > Royal.Warriors(kingdom3)))
					{
						kingdom3 = item;
					}
				}
				if (kingdom3 != null)
				{
					int num4 = Royal.Warriors(kingdom3);
					return $"We have {num3} warriors. {Royal.NameOf(kingdom3)} has {num4}. " + ((num3 > num4) ? "We can press them." : "We should hold our ground.") + $" The war chest holds {num} kills.";
				}
				Kingdom kingdom4 = null;
				foreach (Kingdom item2 in list)
				{
					if (kingdom4 == null || Royal.Warriors(item2) > Royal.Warriors(kingdom4))
					{
						kingdom4 = item2;
					}
				}
				if (kingdom4 == null)
				{
					return $"We have {num3} warriors, and no realm to fear. The war chest holds {num} kills.";
				}
				return $"We have {num3} warriors. The strongest realm, {Royal.NameOf(kingdom4)}, has {Royal.Warriors(kingdom4)}. The war chest holds {num} kills.";
			}
			case Seat.Steward:
			{
				int num5 = 0;
				string arg = "the capital";
				try
				{
					if (home.capital != null)
					{
						num5 = home.capital.getResourcesAmount("gold");
						arg = Renames.Apply(home.capital.name);
					}
				}
				catch
				{
				}
				Kingdom kingdom5 = null;
				foreach (Kingdom item3 in list)
				{
					if (!Royal.AtWar(home, item3) && (float)item3.power < (float)home.power * Court.TributeReach(home) && (kingdom5 == null || item3.power < kingdom5.power))
					{
						kingdom5 = item3;
					}
				}
				return $"The treasury in {arg} holds {num5} gold. " + ((kingdom5 != null) ? (Royal.NameOf(kingdom5) + " is weak enough to pay us tribute.") : "No realm is weak enough to pay us tribute.");
			}
			case Seat.Chancellor:
			{
				List<string> list3 = new List<string>();
				Kingdom kingdom2 = null;
				int num2 = int.MinValue;
				foreach (Kingdom item4 in list)
				{
					if (Royal.AtWar(home, item4))
					{
						list3.Add(Royal.NameOf(item4));
					}
					else if (!Royal.Allied(home, item4))
					{
						int total;
						try
						{
							total = World.world.diplomacy.getOpinion(item4, home).total;
						}
						catch
						{
							continue;
						}
						if (total > num2)
						{
							num2 = total;
							kingdom2 = item4;
						}
					}
				}
				string text2 = ((list3.Count > 0) ? ("We are at war with " + string.Join(" and ", list3.ToArray()) + ". ") : "We are at peace. ");
				if (kingdom2 != null && num2 >= Court.AllianceShift(home))
				{
					return text2 + Royal.NameOf(kingdom2) + " thinks well of us. They may take an alliance.";
				}
				return text2 + "No realm trusts us enough for an alliance. Gifts would help.";
			}
			case Seat.Spymaster:
			{
				Kingdom kingdom = null;
				foreach (Kingdom item5 in list)
				{
					if (Royal.AtWar(home, item5))
					{
						kingdom = item5;
						break;
					}
				}
				if (kingdom == null && list.Count > 0)
				{
					kingdom = list[0];
				}
				string text = null;
				try
				{
					if (kingdom != null)
					{
						List<City> list2 = Royal.CitiesOf(kingdom, you.current_position);
						if (list2.Count > 0)
						{
							text = Renames.Apply(list2[0].name) + " of " + Royal.NameOf(kingdom) + " is the closest.";
						}
					}
				}
				catch
				{
				}
				return $"Our agents are ready. The war chest holds {num} kills." + ((text != null) ? (" " + text) : "");
			}
			default:
				return $"The skies will answer when you call. The war chest holds {num} kills.";
			}
		}

		private static void SeatMenu(Conversation c)
		{
			c.LastTopic = "council_seat";
			Actor with = c.With;
			Kingdom k = Court.KingdomOf(c.You);
			Seat seat;
			bool flag = Court.SeatOf(with, out seat);
			c.Say(flag ? ("I serve as your " + Court.Title(seat) + ". Where would you have me?") : Dialogue.Pick("A seat at court? Which one, Your Majesty?", "You would have me on your council?"));
			List<Choice> list = new List<Choice>();
			Seat[] seats = Court.Seats;
			foreach (Seat seat2 in seats)
			{
				Seat seat3 = seat2;
				Actor actor = Court.Holder(k, seat2);
				Choice choice = new Choice(Court.Title(seat2) + ": " + Court.Duty(seat2), delegate
				{
					Appoint(c, seat3);
				});
				if (actor == with)
				{
					choice.Off("Their seat now");
				}
				else
				{
					choice.Note = ((actor == null) ? "Seat empty" : ("Now " + NameOf(actor)));
				}
				list.Add(choice);
			}
			list.Add(Dialogue.Back(c));
			c.Offer(list);
		}

		private static void Appoint(Conversation c, Seat s)
		{
			c.LastTopic = "council_appoint";
			Actor with = c.With;
			if (!Court.Appoint(Court.KingdomOf(c.You), s, with))
			{
				c.Say("I cannot serve, Your Majesty.");
				c.Offer(Dialogue.Topics(c));
				return;
			}
			c.Voice = Dialogue.VoiceOf(with, c.You);
			c.Say(Dialogue.Pick("I will serve as your " + Court.Title(s) + ", Your Majesty.", "*bows* Your " + Court.Title(s) + ", then. I won't fail you."));
			c.Toast?.Invoke(NameOf(with) + " is your " + Court.Title(s));
			c.Offer(Dialogue.Topics(c));
		}

		private static string NameOf(Actor a)
		{
			try
			{
				return Renames.Apply(a.getName());
			}
			catch
			{
				return "someone";
			}
		}
	}
}
