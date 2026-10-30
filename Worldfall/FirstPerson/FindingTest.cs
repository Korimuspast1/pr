using System;
using System.Collections;
using System.IO;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class FindingTest : CivilTest
	{
		private Actor _king;

		private Actor _host;

		private Actor _npc;

		protected override string Prefix => "FIND";

		protected override IEnumerator Body()
		{
			_king = Village("human", 12, 2);
			if (!Check("a village", _king != null))
			{
				yield break;
			}
			foreach (Actor item in Founded)
			{
				if (item != null && item != _king && item.isAdult())
				{
					if (_host == null)
					{
						_host = item;
					}
					else if (_npc == null)
					{
						_npc = item;
						break;
					}
				}
			}
			if (Check("two villagers", _host != null && _npc != null))
			{
				try
				{
					((BaseSimObject)_host).addStatusEffect("invincible", 300f, pColorEffect: false);
					((BaseSimObject)_npc).addStatusEffect("invincible", 300f, pColorEffect: false);
				}
				catch
				{
				}
				yield return Possess(_host);
				if (Check("first person on a villager", InControl(_host)))
				{
					yield return Part("ask after the leader", AskRole());
					yield return Part("ask after a name", AskName());
					yield return Part("refused by someone who hates you", Refused());
					yield return Part("the hall's ledger", LedgerPart());
				}
			}
		}

		private IEnumerator Talk()
		{
			Mod.EndConversation();
			yield return CivilTest.Wait(0.3f);
			_host.current_position = _npc.current_position - new Vector2(1.3f, 0f);
			try
			{
				_npc.makeWait(8f);
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.3f);
			Check("a talk opens", Mod.StartConversationForTest(_npc) && Mod.Converse.Open);
		}

		private IEnumerator AskRole()
		{
			try
			{
				_host.data.money = 40;
				_npc.data.money = 0;
			}
			catch
			{
			}
			Mod.Pings.Clear();
			Finding.Reset();
			yield return Talk();
			Conversation c = Mod.Converse;
			if (!Check("\"I'm looking for someone.\" is a topic", Say(c, "I'm looking for someone."), Labels(c)))
			{
				yield break;
			}
			Note("roles: " + Labels(c));
			Choice choice = Find(c, "The town's leader") ?? Find(c, "The king or queen");
			if (Check("the town's ruler is offered", choice != null, Labels(c)))
			{
				int fee = 0;
				int num = choice.Label.IndexOf('(');
				if (num > 0)
				{
					int.TryParse(choice.Label.Substring(num + 1).Split(' ')[0], out fee);
				}
				Check("a stranger wants 1 to 3 gold", fee >= 1 && fee <= 3, choice.Label);
				Actor who = Finding.Holder(_npc.city, (!choice.Label.StartsWith("The town's", StringComparison.Ordinal)) ? FindRole.Monarch : FindRole.Leader, _npc.current_position);
				Check("...and someone holds it", who != null && who != _host, WorldBoxMod.Describe(who));
				Say(c, choice.Label);
				yield return CivilTest.Wait(0.3f);
				Note("answer: " + string.Join(" / ", c.Pages.ToArray()));
				Ping ping = ((who != null) ? Mod.Pings.On(who) : null);
				Check("the ruler is pinged", ping != null && ping.Kind == PingKind.Look && ping.Actor == who);
				Check("...for ten minutes", ping != null && Mathf.Abs(ping.Until - ping.Born - 600f) < 1f);
				Check("the tip is paid", CivilTest.Money(_host) == 40 - fee && CivilTest.Money(_npc) == fee, $"you {CivilTest.Money(_host)}, they {CivilTest.Money(_npc)}");
				Check("you can ask after someone else", Find(c, "I'm looking for someone else.") != null, Labels(c));
				yield return CivilTest.Wait(0.4f);
				Capture("answer");
			}
		}

		private IEnumerator AskName()
		{
			Mod.Pings.Clear();
			Conversation c = Mod.Converse;
			if (!Check("again", Say(c, "I'm looking for someone else."), Labels(c)) || !Check("by name", Say(c, "Someone by name..."), Labels(c)))
			{
				yield break;
			}
			Note("groups: " + Labels(c));
			Choice choice = c.Choices.Find((Choice ch) => ch.Label.Contains(" to "));
			if (!Check("a letter group", choice != null, Labels(c)))
			{
				yield break;
			}
			Say(c, choice.Label);
			Note("names: " + Labels(c));
			Actor target = null;
			foreach (Choice choice2 in c.Choices)
			{
				foreach (Actor item in Founded)
				{
					if (item != null && item != _host && item != _npc && item != _king && item.isAlive() && choice2.Label.StartsWith(Dialogue.NameOf(item) + ",", StringComparison.Ordinal))
					{
						target = item;
						break;
					}
				}
				if (target != null)
				{
					Say(c, choice2.Label);
					break;
				}
			}
			if (Check("a villager listed by name and work", target != null, Labels(c)))
			{
				bool knew = PeopleMet.Knows(_host, target);
				yield return CivilTest.Wait(0.3f);
				Note("answer: " + string.Join(" / ", c.Pages.ToArray()));
				Check("they are pinged", Mod.Pings.On(target) != null);
				Check("their name is heard: the ping calls them by it", Finding.Heard(_host, target) && PeopleMet.Label(_host, target) == Dialogue.NameOf(target), PeopleMet.Label(_host, target));
				Check("...but they are not a contact before a hello", knew || !PeopleMet.Knows(_host, target));
				Mod.EndConversation();
			}
		}

		private IEnumerator Refused()
		{
			Chronicle.ChangeOpinion(_npc, _host, -500);
			yield return Talk();
			Conversation converse = Mod.Converse;
			Check("hated: still a topic", Say(converse, "I'm looking for someone."), Labels(converse));
			Note("answer: " + string.Join(" / ", converse.Pages.ToArray()));
			Check("...but they won't help", Find(converse, "The ") == null && Find(converse, "Someone by name") == null, Labels(converse));
			Mod.EndConversation();
			Chronicle.ChangeOpinion(_npc, _host, 500);
		}

		private IEnumerator LedgerPart()
		{
			City town = _king.city;
			Building hall = null;
			try
			{
				foreach (Building building in town.buildings)
				{
					if (building != null && building.asset != null && building.asset.type == "type_hall")
					{
						hall = building;
						break;
					}
				}
			}
			catch
			{
			}
			if (hall == null)
			{
				hall = AddHouse("hall_human_0", town);
			}
			if (!Check("a hall", hall != null && HallRooms.CanEnter(hall), (hall != null) ? hall.asset.id : "none"))
			{
				yield break;
			}
			for (float t = 0f; t < 12f; t += 0.5f)
			{
				if (Mod.Interior.Inside)
				{
					break;
				}
				Mod.EnterHouseForTest(hall);
				yield return CivilTest.Wait(0.5f);
			}
			if (Check("inside the hall", Mod.Interior.Inside && Mod.Interior.House == hall, string.Format("inside {0}, house {1}, room {2}, model {3}", Mod.Interior.Inside, (Mod.Interior.House == hall) ? "the hall" : (Mod.Interior.House?.asset?.id ?? "none"), (Mod.Interior.Room != null) ? (HallRooms.KindOf(Mod.Interior.Room) ?? "not a hall room") : "none", HallRooms.RoomFor(hall))))
			{
				Anchors.Spot[] array = HallRooms.Spots(Mod.Interior.Room, "a_steward");
				Note($"{HallRooms.KindOf(Mod.Interior.Room)}: {array.Length} steward's desk");
				if (array.Length != 0)
				{
					Mod.Interior.Eye = new Vector2(array[0].At.X, array[0].At.Y);
				}
				PromptAsk promptAsk = new PromptAsk
				{
					Host = _host,
					Room = Mod.Interior,
					House = hall,
					Eye = Mod.Interior.Eye,
					Yaw = Mod.ViewYaw
				};
				Check("the desk offers the ledger", Ledger.RoomPrompt(promptAsk) && promptAsk.Prompt != null && promptAsk.Prompt.Contains("ledger"), promptAsk.Prompt);
				Ledger.Show(hall);
				yield return CivilTest.Wait(0.6f);
				Check("it opens", Ledger.Open);
				Check("the offices list the ruler", Ledger.Offices.Exists((Ledger.Row r) => r.A == _king || r.A == town.leader), Ledger.Offices.Count + " offices");
				Check("everyone of the town is listed", Ledger.People.Count >= Founded.Count - 4 && Ledger.People.Exists((Ledger.Row r) => r.A == _npc), Ledger.People.Count + " listed");
				Capture("ledger");
				Mod.Pings.Clear();
				Ledger.Row row = Ledger.People.Find((Ledger.Row r) => r.A == _npc);
				if (row != null)
				{
					Ledger.Find(row);
				}
				Check("Find pings them", Mod.Pings.On(_npc) != null);
				Ledger.Search = Dialogue.NameOf(_npc).Substring(0, 3);
				yield return CivilTest.Wait(0.4f);
				Capture("ledger_search");
				Check("Esc closes it", Ledger.Escape() && !Ledger.Open);
				Mod.LeaveRoomForTest();
				yield return CivilTest.Wait(0.5f);
			}
		}

		private static Choice Find(Conversation c, string start)
		{
			for (int i = 0; i < 8; i++)
			{
				if (c.Choices.Count <= 0)
				{
					break;
				}
				if (!(c.Choices[0].Label == "Go on..."))
				{
					break;
				}
				c.Next();
				c.Next();
			}
			return c.Choices.Find((Choice ch) => ch.Label.StartsWith(start, StringComparison.Ordinal));
		}

		private static string Labels(Conversation c)
		{
			return string.Join(" | ", c.Choices.ConvertAll((Choice x) => x.Label).ToArray());
		}

		private bool Say(Conversation c, string start)
		{
			Choice choice = Find(c, start);
			if (choice == null)
			{
				Note("(no \"" + start + "\": " + Labels(c) + ")");
				return false;
			}
			c.LineStart -= 999f;
			c.Pick(c.Choices.IndexOf(choice));
			return true;
		}

		private void Capture(string name)
		{
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "find_" + name + "_hud.png"));
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "find_" + name + ".png"));
		}
	}
}
