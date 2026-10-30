using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace FirstPerson
{
	internal sealed class RenamesTest : CivilTest
	{
		private static readonly Regex Tags = new Regex("<[^>]+>");

		private Actor _king;

		private Actor _villager;

		private Kingdom _k;

		private Kingdom _foe;

		private City _town;

		private string _oldK;

		private string _oldTown;

		private string _oldV;

		private string _newK;

		private string _newTown;

		private string _newV;

		private string _warWas;

		protected override string Prefix => "RENAME";

		private Hud H => Mod.HudForTest;

		protected override IEnumerator Body()
		{
			_king = Village("human", 10, 0);
			if (!Check("a village", _king != null))
			{
				yield break;
			}
			_k = _king.kingdom;
			_town = _king.city;
			foreach (Actor item in Founded)
			{
				if (item != null && item != _king && item.isAdult())
				{
					_villager = item;
					break;
				}
			}
			Actor actor = Village("orc", 8, 0, _king.current_tile, 30f);
			if (!Check("a second village", actor != null))
			{
				yield break;
			}
			_foe = actor.kingdom;
			foreach (Actor item2 in Founded)
			{
				try
				{
					((BaseSimObject)item2).addStatusEffect("invincible", 600f, pColorEffect: false);
				}
				catch
				{
				}
			}
			yield return Possess(_king);
			if (!Check("the king in first person", InControl(_king) && Royal.IsKing(_king)))
			{
				yield break;
			}
			try
			{
				World.world.diplomacy.startWar(_k, _foe, WarTypeLibrary.normal, pLog: true);
			}
			catch (Exception ex)
			{
				Note("war: " + ex.Message);
			}
			yield return CivilTest.Wait(1f);
			War war = null;
			try
			{
				using (IEnumerator<War> enumerator2 = _k.getWars().GetEnumerator())
				{
					if (enumerator2.MoveNext())
					{
						War current3 = enumerator2.Current;
						war = current3;
					}
				}
			}
			catch
			{
			}
			_warWas = war?.name;
			_oldK = _k.name;
			_oldTown = _town.name;
			_oldV = ((_villager != null) ? _villager.getName() : null);
			Note("before: realm " + _oldK + ", town " + _oldTown + ", villager " + _oldV + ", war \"" + _warWas + "\"");
			NewsItem warLine = WorldNews.Items.FindLast((NewsItem n) => n.Kind == "diplomacy_war_started" || n.Kind == "total_war_started");
			Check("the war's news line names the realm", warLine != null && Plain(warLine.Text).Contains(_oldK), (warLine != null) ? Plain(warLine.Text) : "no line");
			yield return Part("rename", Rename());
			yield return Part("the swap", Swaps());
			yield return Part("war map", WarMapNames(war));
			yield return Part("news", News(warLine));
			yield return Part("words", Words());
			yield return Part("talk", Talk());
		}

		private static string Plain(string s)
		{
			if (s != null)
			{
				return Tags.Replace(s, "");
			}
			return "";
		}

		private static void RenameAsGame(NanoObject o, BaseSystemData data, string name)
		{
			data.custom_name = true;
			o.setName(name);
		}

		private IEnumerator Rename()
		{
			_newK = _oldK + " Reborn";
			_newTown = "Quillhaven";
			_newV = "Tessaly Marrow";
			RenameAsGame(_k, _k.data, _newK);
			RenameAsGame(_town, _town.data, _newTown);
			if (_villager != null)
			{
				_villager.data.custom_name = true;
				_villager.setName(_newV);
			}
			Check("the realm renamed", _k.name == _newK, _k.name);
			Check("the game kept the old name", _k.data.past_names != null && _k.data.past_names.Exists((NameEntry e) => e.name == _oldK), (_k.data.past_names != null) ? string.Join(", ", _k.data.past_names.ConvertAll((NameEntry e) => e.name).ToArray()) : "none");
			yield return CivilTest.Wait(2.5f);
			Note("swaps known: " + Renames.Count);
		}

		private IEnumerator Swaps()
		{
			Check("the realm's old name swapped", Renames.Apply("Long live " + _oldK + "!") == "Long live " + _newK + "!", Renames.Apply("Long live " + _oldK + "!"));
			Check("the town's old name swapped", Renames.Apply("Bread for " + _oldTown + ".") == "Bread for " + _newTown + ".", Renames.Apply("Bread for " + _oldTown + "."));
			Check("with the game's colour tags round it", Renames.Apply("<color=#FF0000>" + _oldK + "</color> wins") == "<color=#FF0000>" + _newK + "</color> wins");
			Check("the new name left as it is", Renames.Apply("Hail " + _newK) == "Hail " + _newK, Renames.Apply("Hail " + _newK));
			Check("twice is the same as once", Renames.Apply(Renames.Apply(_oldK)) == _newK);
			Check("not inside another word", Renames.Apply(_oldTown + "ish x" + _oldTown) == _oldTown + "ish x" + _oldTown, Renames.Apply(_oldTown + "ish x" + _oldTown));
			if (_oldV == null)
			{
				yield break;
			}
			string text = Renames.Apply("I met " + _oldV + " today");
			bool flag = false;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple != _villager && simple.getName() == _oldV)
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				Note("villager's old name " + _oldV + " is still someone else's: left alone (" + text + ")");
			}
			else
			{
				Check("a villager's old name swapped", text == "I met " + _newV + " today", text);
			}
		}

		private IEnumerator WarMapNames(War war)
		{
			WarMap map = Mod.WarMap;
			map.Overlay = WarMap.Layer.Kingdoms;
			map.Tab = 0;
			map.Toggle(_king, Mod.Toast);
			yield return CivilTest.Wait(2f);
			if (!Check("the war map opens", map.Open))
			{
				yield break;
			}
			WarMap.WarEntry warEntry = map.AllWars.Find((WarMap.WarEntry e) => e.Ours);
			Check("our war listed", warEntry != null);
			WarMap.Realm realm = map.RealmOf(_k);
			Check("our realm by its new name", realm != null && realm.Name == _newK, realm?.Name);
			if (warEntry != null)
			{
				Note("war was \"" + _warWas + "\", now listed as \"" + warEntry.Name + "\" (the game's own: \"" + war?.name + "\")");
				if (_warWas != null && _warWas.Contains(_oldK))
				{
					Check("the war's name says the new name", warEntry.Name.Contains(_newK) && !Renames.Apply(warEntry.Name).Contains(_newK + " Reborn"), warEntry.Name);
				}
				else
				{
					Note("(the war's name didn't carry the realm's)");
				}
			}
			bool ok = map.Towns.Exists((WarMap.Town t) => t.Name == _newTown);
			Check("our town by its new name", ok);
			yield return Shot("renames_warmap.png");
			map.Open = false;
		}

		private IEnumerator News(NewsItem warLine)
		{
			yield return CivilTest.Wait(0.5f);
			if (warLine != null)
			{
				Check("the war's news line says the new name", Plain(warLine.Text).Contains(_newK) && !Plain(warLine.Text).Contains(_newK + " Reborn"), Plain(warLine.Text));
			}
			WorldNews.Toggle();
			yield return Shot("renames_news.png");
			WorldNews.Toggle();
		}

		private IEnumerator Words()
		{
			Quest q = new Quest
			{
				Kind = QuestKind.Pilgrimage,
				Place = _oldTown
			};
			Check("a quest's town says the new name", Quests.Title(q) == "Pilgrimage to " + _newTown, Quests.Title(q));
			Check("a quest's step says the new name", Quests.Status(q) == "Travel to " + _newTown, Quests.Status(q));
			Mod.Toast("You joined " + _oldTown + " of " + _oldK + ".");
			Check("a toast says the new names", Mod.ToastText == "You joined " + _newTown + " of " + _newK + ".", Mod.ToastText);
			yield break;
		}

		private IEnumerator Talk()
		{
			if (_villager == null || !_villager.isAlive())
			{
				Note("(no villager to talk to)");
				yield break;
			}
			CivilTest.StandBefore(_villager, _king, 1.6f);
			yield return CivilTest.Wait(0.5f);
			if (Check("a talk opens", Mod.StartConversationForTest(_villager)))
			{
				Conversation converse = Mod.Converse;
				converse.Say("We of " + _oldK + " keep " + _oldTown + " fed.");
				string text = converse.Line ?? "";
				Check("they say the new names", text.Contains(_newK) && text.Contains(_newTown) && !text.Contains(_newK + " Reborn"), text);
				Check("their name in the talk is the new one", converse.Name == _newV, converse.Name);
				converse.Next();
				yield return Shot("renames_talk.png");
				Mod.EndConversation();
			}
		}

		private IEnumerator Shot(string name)
		{
			yield return CivilTest.Wait(0.4f);
			H.CaptureHudForTest(Path.Combine(Log.Folder, name));
			for (int i = 0; i < 20; i++)
			{
				if (!H.CapturePending)
				{
					break;
				}
				yield return null;
			}
			yield return null;
		}
	}
}
