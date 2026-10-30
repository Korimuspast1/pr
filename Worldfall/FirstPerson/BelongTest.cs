using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BelongTest : CivilTest
	{
		private Actor _kingA;

		private Actor _kingB;

		private Actor _host;

		private City _a;

		private City _b;

		private readonly List<Actor> _peopleA = new List<Actor>();

		private readonly List<Actor> _peopleB = new List<Actor>();

		protected override string Prefix => "BELONG";

		protected override IEnumerator Body()
		{
			_kingA = Village("human", 10, 0);
			if (!Check("a first village", _kingA != null))
			{
				yield break;
			}
			_a = _kingA.city;
			foreach (Actor item in Founded)
			{
				if (item != null && item != _kingA && item.isAdult())
				{
					_peopleA.Add(item);
				}
			}
			_kingB = Village("human", 10, 0, _kingA.current_tile, 25f);
			if (!Check("a second village", _kingB != null))
			{
				yield break;
			}
			_b = _kingB.city;
			foreach (Actor item2 in Founded)
			{
				if (item2 != null && item2 != _kingB && item2.isAdult())
				{
					_peopleB.Add(item2);
				}
			}
			Check("two kingdoms", _a.kingdom != null && _b.kingdom != null && _a.kingdom != _b.kingdom);
			_host = _peopleA[0];
			try
			{
				((BaseSimObject)_host).addStatusEffect("invincible", 300f, pColorEffect: false);
			}
			catch
			{
			}
			yield return Possess(_host);
			if (Check("first person on a villager", InControl(_host)))
			{
				yield return Part("joining", Joining());
				yield return Part("leaving", Leaving());
				yield return Part("refused", Refused());
				yield return Part("a king moves", KingMoves());
				yield return Part("a king moves within his realm", KingMovesHome());
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
			return c.Line + " / " + string.Join(" | ", c.Choices.ConvertAll((Choice x) => x.Label).ToArray());
		}

		private IEnumerator TalkTo(Actor npc)
		{
			Mod.EndConversation();
			_host.current_position = npc.current_position - new Vector2(1.3f, 0f);
			try
			{
				npc.makeWait(6f);
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.3f);
			Mod.StartConversationForTest(npc);
		}

		private bool Say(Conversation c, string start)
		{
			Choice choice = Find(c, start);
			if (choice == null)
			{
				return false;
			}
			c.LineStart -= 999f;
			c.Pick(c.Choices.IndexOf(choice));
			return true;
		}

		private IEnumerator Joining()
		{
			Conversation c = Mod.Converse;
			yield return TalkTo(_kingB);
			Check("the other king offers to take you in", Find(c, "May I live in") != null, Labels(c));
			Check("you can't leave a town you're not in", Find(c, "I'm leaving") == null);
			Say(c, "May I live in");
			Check("he says yes", Say(c, "Thank you."), c.Line + " / " + Labels(c));
			Mod.EndConversation();
			yield return CivilTest.Wait(0.5f);
			Check("you live in his town", _host.city == _b, _host.city?.name);
			Check("and are of his kingdom", _host.kingdom == _b.kingdom, _host.kingdom?.name);
			Check("your old town lost you", !Civil.PeopleOf(_a).Contains(_host));
		}

		private IEnumerator Leaving()
		{
			Conversation c = Mod.Converse;
			yield return TalkTo(_peopleB[0]);
			Check("a neighbour hears you're leaving", Find(c, "I'm leaving") != null, Labels(c));
			Say(c, "I'm leaving");
			Check("you go", Say(c, "Yes. I'm going."), Labels(c));
			Mod.EndConversation();
			yield return CivilTest.Wait(0.5f);
			Check("you have no town", _host.city == null, _host.city?.name);
			Kingdom kingdom = _host.kingdom;
			Check("you're a nomad", kingdom != null && kingdom != _b.kingdom && kingdom != _a.kingdom && !kingdom.isCiv(), kingdom?.name + " / " + kingdom?.data?.id);
			Check("the game takes you for its own nomad, not an enemy", kingdom == null || !kingdom.isEnemy(_b.kingdom));
			yield return CivilTest.Wait(4f);
			Check("you stay a nomad while you're in control", _host.city == null, _host.city?.name);
		}

		private IEnumerator Refused()
		{
			Conversation c = Mod.Converse;
			Law.SetBounty(_a.kingdom, _host, 50);
			yield return TalkTo(_kingA);
			Say(c, "May I live in");
			Check("a wanted man is turned away", Find(c, "Thank you.") == null && c.Line != null && c.Line.Contains("price"), c.Line);
			Law.SetBounty(_a.kingdom, _host, 0);
			Mod.EndConversation();
			yield return TalkTo(_kingA);
			Say(c, "May I live in");
			Check("with the fine gone he takes you", Say(c, "Thank you."), c.Line);
			Mod.EndConversation();
			yield return CivilTest.Wait(0.5f);
			Check("home again", _host.city == _a && _host.kingdom == _a.kingdom);
		}

		private IEnumerator KingMoves()
		{
			Kingdom old = _kingB.kingdom;
			yield return Possess(_kingB);
			if (Check("first person on the other king", InControl(_kingB)))
			{
				_host = _kingB;
				Conversation c = Mod.Converse;
				yield return TalkTo(_kingA);
				Say(c, "May I live in");
				Check("he warns you of the crown", c.Line != null && c.Line.Contains("crown"), c.Line);
				Check("he says yes", Say(c, "Thank you."), Labels(c));
				Mod.EndConversation();
				yield return CivilTest.Wait(0.5f);
				Check("the old king lives in the other town", _kingB.city == _a && _kingB.kingdom == _a.kingdom);
				Check("his old kingdom has lost its king", old == null || old.king != _kingB);
				Check("he's no king", !_kingB.isKing());
			}
		}

		private IEnumerator KingMovesHome()
		{
			Kingdom realm = _a.kingdom;
			try
			{
				_b.joinAnotherKingdom(realm);
			}
			catch (Exception ex)
			{
				Note("joinAnotherKingdom: " + ex.Message);
			}
			yield return CivilTest.Wait(0.5f);
			if (!Check("both towns in one kingdom", _b.kingdom == realm, _b.kingdom?.name) || !Check("the first king still rules", _kingA.isAlive() && _kingA.isKing() && realm.king == _kingA))
			{
				yield break;
			}
			yield return Possess(_kingA);
			if (!Check("first person on the first king", InControl(_kingA)))
			{
				yield break;
			}
			_host = _kingA;
			Actor actor = null;
			foreach (Actor item in Civil.PeopleOf(_b))
			{
				if (item != null && item.isAdult() && item != _kingA && (item.isCityLeader() || !_b.hasLeader()))
				{
					actor = item;
					break;
				}
			}
			if (Check("someone in the other town can take him in", actor != null))
			{
				Conversation c = Mod.Converse;
				yield return TalkTo(actor);
				Check("it's a move, not a new home", Find(c, "May I move to") != null, Labels(c));
				Say(c, "May I move to");
				Check("no warning about the crown", c.Line != null && !c.Line.Contains("crown"), c.Line);
				Check("they say yes", Say(c, "Thank you."), Labels(c));
				Mod.EndConversation();
				yield return CivilTest.Wait(0.5f);
				Check("the king lives in the other town", _kingA.city == _b, _kingA.city?.name);
				Check("and keeps his crown", _kingA.isKing() && realm.king == _kingA && _kingA.kingdom == realm);
			}
		}
	}
}
