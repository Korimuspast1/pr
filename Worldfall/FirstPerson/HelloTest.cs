using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class HelloTest : CivilTest
	{
		private Actor _king;

		private Actor _host;

		private Actor _stranger;

		private Actor _hothead;

		private Actor _child;

		protected override string Prefix => "HELLO";

		protected override IEnumerator Body()
		{
			_king = Village("human", 10, 2);
			if (!Check("a village", _king != null))
			{
				yield break;
			}
			foreach (Actor item in Founded)
			{
				if (item == null || item == _king || !item.isAlive())
				{
					continue;
				}
				bool flag = false;
				try
				{
					flag = item.isCityLeader();
				}
				catch
				{
				}
				if (!item.isAdult())
				{
					if (_child == null)
					{
						_child = item;
					}
				}
				else if (!flag)
				{
					if (_host == null)
					{
						_host = item;
					}
					else if (_stranger == null)
					{
						_stranger = item;
					}
					else if (_hothead == null)
					{
						_hothead = item;
					}
				}
			}
			if (!Check("villagers to meet", _host != null && _stranger != null && _hothead != null))
			{
				yield break;
			}
			Actor[] array = new Actor[4] { _host, _stranger, _hothead, _king };
			foreach (Actor actor in array)
			{
				try
				{
					((BaseSimObject)actor).addStatusEffect("invincible", 300f, pColorEffect: false);
				}
				catch
				{
				}
			}
			array = new Actor[3] { _host, _stranger, _hothead };
			foreach (Actor actor2 in array)
			{
				try
				{
					if (actor2.hasTrait("mute"))
					{
						actor2.removeTrait("mute");
						Note(WorldBoxMod.Describe(actor2) + " was mute: the trait taken off");
					}
				}
				catch
				{
				}
			}
			array = new Actor[2] { _stranger, _hothead };
			foreach (Actor actor3 in array)
			{
				try
				{
					if (actor3.lover != null)
					{
						actor3.setLover(null);
					}
				}
				catch
				{
				}
			}
			try
			{
				if (_host.lover != null)
				{
					_host.setLover(null);
				}
			}
			catch
			{
			}
			yield return Possess(_host);
			if (Check("first person on a villager", InControl(_host)))
			{
				yield return Part("hello", Hello());
				yield return Part("known without a hello", Known());
				yield return Part("insults", Insults());
				yield return Part("people panel", Panel());
				yield return Part("a mute body", Mute());
			}
		}

		private IEnumerator Hello()
		{
			Conversation c = Mod.Converse;
			Mod.EndConversation();
			Check("a stranger's name isn't known", !PeopleMet.Knows(_host, _stranger));
			string looks = PeopleMet.Looks(_stranger);
			string name = Dialogue.NameOf(_stranger);
			Check("...they're called by their look", PeopleMet.Label(_host, _stranger) == looks && looks != name, looks);
			CivilTest.StandBefore(_stranger, _host, 1.3f);
			try
			{
				_stranger.makeWait();
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.3f);
			Check("E on them opens a talk", Mod.HelloForTest(_stranger) && c.Open);
			Check("it starts with your own words", c.LineIsYours && c.Line != null && c.Line.Contains(Dialogue.NameOf(_host)), c.Line);
			Check("...the card shows their look, not their name", c.Shown == looks, c.Shown);
			Check("...and only \"Go on...\" to pick", c.Choices.Count == 1 && c.Choices[0].Label == "Go on...");
			yield return CivilTest.Wait(0.5f);
			yield return Capture("yours");
			float until = Time.unscaledTime + 6f;
			while (c.Open && c.LineIsYours && Time.unscaledTime < until)
			{
				yield return CivilTest.Wait(0.1f);
			}
			Check("your line gives way to theirs by itself", c.Open && !c.LineIsYours && c.Page == 1, "page " + c.Page);
			Check("they say their name", c.Line != null && c.Line.Contains(name), c.Line);
			Check("...and the card shows it", c.Shown == name, c.Shown);
			yield return CivilTest.Wait(1.2f);
			yield return Capture("theirs");
			c.Next();
			yield return null;
			Check("then the topics", c.Choices.Exists((Choice ch) => ch.Label.StartsWith("Tell me about your family", StringComparison.Ordinal)), string.Join(" | ", c.Choices.ConvertAll((Choice ch) => ch.Label).ToArray()));
			Mod.EndConversation();
			yield return CivilTest.Wait(0.3f);
			Check("they're on your contacts", PeopleMet.Of(_host).Exists((Met m) => m.Id == _stranger.data.id && m.Talks == 1));
			Check("...and known by name now", PeopleMet.Knows(_host, _stranger) && PeopleMet.Label(_host, _stranger) == name);
			Check("E again is a plain talk", Mod.HelloForTest(_stranger) && !c.LineIsYours && c.Shown == name, c.Line);
			Mod.EndConversation();
			yield return CivilTest.Wait(0.3f);
		}

		private IEnumerator Known()
		{
			Check("a king is known by name", PeopleMet.Knows(_host, _king));
			Actor actor = null;
			try
			{
				actor = _host.city?.leader;
			}
			catch
			{
			}
			if (actor != null && actor != _host)
			{
				Check("...and your town's leader", PeopleMet.Knows(_host, actor), Dialogue.NameOf(actor));
			}
			else
			{
				Note("no leader in the town to check");
			}
			_child = null;
			foreach (Actor item in Founded)
			{
				try
				{
					if (item != null && item.isAlive() && !item.isAdult() && !PeopleMet.Knows(_host, item))
					{
						_child = item;
						break;
					}
				}
				catch
				{
				}
			}
			if (_child == null)
			{
				Note("no child in the village that isn't yours already");
				yield break;
			}
			Check("someone else's child is a stranger", !PeopleMet.Knows(_host, _child));
			try
			{
				_child.setParent1(_host);
			}
			catch (Exception ex)
			{
				Note("setParent1: " + ex.Message);
			}
			Check("your own child is known", PeopleMet.Knows(_host, _child));
			PeopleMet.KeepFamily(_host, 999f);
			Check("...and put on your contacts, not talked to", PeopleMet.Of(_host).Exists((Met m) => m.Id == _child.data.id && m.Talks == 0));
		}

		private IEnumerator Insults()
		{
			Interactions talk = Mod.Talk;
			int before = Chronicle.Opinion(_stranger, _host);
			int thrown = talk.Insults;
			CivilTest.StandBefore(_stranger, _host, 1.3f);
			yield return CivilTest.Wait(0.3f);
			float z0 = _stranger.position_height;
			talk.Insult(_host, _stranger);
			Check("F insults them", talk.Insults == thrown + 1);
			Check("...they answer back", talk.Speaker == _stranger && !string.IsNullOrEmpty(talk.Line), talk.Line);
			Check("...and think less of you", Chronicle.Opinion(_stranger, _host) < before, before + " -> " + Chronicle.Opinion(_stranger, _host));
			float hop = 0f;
			for (int i = 0; i < 10; i++)
			{
				yield return CivilTest.Wait(0.05f);
				hop = Mathf.Max(hop, _stranger.position_height - z0);
			}
			Check("...without the hop through the air", hop < 0.05f && !((BaseSimObject)_stranger).hasStatus("surprised"), $"rose {hop:F2}");
			Check("...nor a fight", _stranger.attack_target != _host, (_stranger.attack_target != null) ? ("targets " + Describe(_stranger.attack_target)) : "none");
			yield return Capture("insult");
			try
			{
				_hothead.addTrait("hotheaded", pRemoveOpposites: true);
			}
			catch
			{
			}
			CivilTest.StandBefore(_hothead, _host, 1.3f);
			yield return CivilTest.Wait(1.4f);
			talk.Insult(_host, _hothead);
			yield return CivilTest.Wait(0.6f);
			bool ok = false;
			try
			{
				ok = _hothead.attack_target == _host || ((BaseSimObject)_hothead).hasStatus("angry");
			}
			catch
			{
			}
			Check("a hothead comes at you", ok, talk.Line);
		}

		private IEnumerator Mute()
		{
			try
			{
				_host.addTrait("mute", pRemoveOpposites: true);
			}
			catch
			{
			}
			if (!Check("you are mute", _host.hasTrait("mute")))
			{
				yield break;
			}
			Interactions talk = Mod.Talk;
			CivilTest.StandBefore(_hothead, _host, 1.3f);
			yield return CivilTest.Wait(1.6f);
			int insults = talk.Insults;
			Mod.ToastText = null;
			talk.Insult(_host, _hothead);
			Check("a mute body's insult is refused", talk.Insults == insults);
			Check("...and says why", Mod.ToastText == "You are mute", Mod.ToastText);
			try
			{
				_host.removeTrait("mute");
			}
			catch
			{
			}
		}

		private IEnumerator Panel()
		{
			Mod.Craft.Open = true;
			Favorites.Tab = 1;
			yield return CivilTest.Wait(0.5f);
			yield return Capture("contacts");
			bool flag = Mod.StarCreature(_stranger);
			Mod.Favs.Update(0f, force: true);
			Check("a contact's star makes them a favourite", flag && Mod.Favs.List.Contains(_stranger));
			Favorites.Tab = 0;
			yield return CivilTest.Wait(0.5f);
			yield return Capture("starred");
			Mod.Craft.Open = false;
			try
			{
				if (_stranger.isFavorite())
				{
					_stranger.switchFavorite();
				}
			}
			catch
			{
			}
		}

		private static string Describe(BaseSimObject o)
		{
			if (!(o is Actor a))
			{
				return o.GetType().Name;
			}
			return WorldBoxMod.Describe(a);
		}

		private IEnumerator Capture(string name)
		{
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "hello_" + name + "_hud.png"));
			for (float t = 0f; t < 4f; t += Time.unscaledDeltaTime)
			{
				if (!Mod.HudForTest.CapturePending)
				{
					break;
				}
				yield return null;
			}
			yield return null;
		}
	}
}
