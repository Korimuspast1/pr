using System;
using System.Collections;
using System.Collections.Generic;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class QuestLevelsTest : CivilTest
	{
		private Actor _host;

		private Actor _king;

		private Actor _villager;

		private City _city;

		private readonly List<string> _toasts = new List<string>();

		private string _missing;

		protected override string Prefix => "QLV";

		protected override IEnumerator Body()
		{
			_king = Village("human", 16, 2);
			if (!Check("a village", _king != null))
			{
				yield break;
			}
			_city = _king.city;
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != _king && item.isAdult() && !item.isWarrior() && !item.isCityLeader())
				{
					if (_host == null)
					{
						_host = item;
					}
					else if (_villager == null)
					{
						_villager = item;
					}
				}
			}
			if (Check("a body and a villager", _host != null && _villager != null))
			{
				try
				{
					((BaseSimObject)_host).addStatusEffect("invincible", 600f, pColorEffect: false);
					((BaseSimObject)_king).addStatusEffect("invincible", 600f, pColorEffect: false);
				}
				catch (Exception ex)
				{
					Note("setup: " + ex.Message);
				}
				yield return Possess(_host);
				if (Check("first person on a villager", InControl(_host)))
				{
					Quests.RaidWarningForTest = 5f;
					Quests.RaidBetweenForTest = 7f;
					yield return Part("the counts", Counts());
					yield return Part("who asks what", Askers());
					yield return Part("a site of the dead", Site());
					yield return Part("picks", Picks());
					yield return Part("a raid", Raid());
					yield return Part("leaving a raid", LeaveRaid());
					yield return Part("the heir", Heir());
					Quests.RaidWarningForTest = (Quests.RaidBetweenForTest = 0f);
					Quests.GoodPickForTest = false;
				}
			}
		}

		private void Toast(string s)
		{
			_toasts.Add(s);
			Note("toast: " + s);
		}

		private static Choice Find(Conversation c, string start)
		{
			return c.Choices.Find((Choice ch) => ch.Label.StartsWith(start, StringComparison.Ordinal));
		}

		private IEnumerator Talk(Actor npc, bool keepOpen, params string[] says)
		{
			Conversation c = Mod.Converse;
			_missing = null;
			CivilTest.StandBefore(npc, _host, 1.4f);
			yield return CivilTest.Wait(0.3f);
			Mod.StartConversationForTest(npc);
			foreach (string say in says)
			{
				for (int j = 0; j < 6; j++)
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
				if (c.Open)
				{
					try
					{
						npc.makeWait(8f);
					}
					catch
					{
					}
				}
				Choice choice = Find(c, say);
				if (choice == null)
				{
					_missing = say;
					Note("(no \"" + say + "\" with " + Dialogue.NameOf(npc) + ": " + string.Join(" | ", c.Choices.ConvertAll((Choice x) => x.Label).ToArray()) + ")");
					break;
				}
				c.LineStart -= 999f;
				c.Pick(c.Choices.IndexOf(choice));
				yield return CivilTest.Wait(0.2f);
				Note("  " + say + " -> \"" + c.Line + "\"");
			}
			for (int num = 0; num < 6; num++)
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
			if (!keepOpen)
			{
				Mod.EndConversation();
				yield return CivilTest.Wait(0.2f);
			}
		}

		private List<string> Labels()
		{
			return Mod.Converse.Choices.ConvertAll((Choice x) => x.Label);
		}

		private IEnumerator Counts()
		{
			Quests.LevelState levelState = Quests.LevelOf(_host);
			Check("a new body starts at level 1", levelState.Level == 1 && levelState.Done1 == 0 && levelState.Done2 == 0 && levelState.Done3 == 0, Quests.LevelLine(_host));
			Quests.SetLevelForTest(_host, 1, 9, 0, 0);
			Quests.LevelDoneForTest(_host, QuestKind.CourtGift, Toast);
			Check("a courting task doesn't count", Quests.LevelOf(_host).Done1 == 9);
			Quests.LevelDoneForTest(_host, QuestKind.TrialSpar, Toast);
			Check("a teacher's trial doesn't count", Quests.LevelOf(_host).Done1 == 9);
			Quests.ForceKindForTest = -1;
			Quest quest = Quests.MakeForTest(QuestKind.Supply, _king, _host) ?? Quests.MakeForTest(QuestKind.Craft, _king, _host);
			if (quest != null && Quests.Accept(quest, _host, Toast))
			{
				quest.Stage = 1;
				Quests.Save(quest);
				Quests.Reward(quest, _host, Toast);
				Check("the tenth task opens level 2", Quests.LevelOf(_host).Level == 2 && Quests.LevelOf(_host).Done1 == 10, Quests.LevelLine(_host));
				Check("...with a word of it", _toasts.Exists((string t) => t.StartsWith("Quest level 2", StringComparison.Ordinal)));
			}
			else
			{
				Note("no supply or craft task from the king: counted directly");
				Quests.LevelDoneForTest(_host, QuestKind.Supply, Toast);
				Check("the tenth task opens level 2", Quests.LevelOf(_host).Level == 2, Quests.LevelLine(_host));
			}
			Quests.SetLevelForTest(_host, 2, 10, 14, 0);
			Quests.LevelDoneForTest(_host, QuestKind.Supply, Toast);
			Check("everyday work at level 2 still counts for itself, not toward level 3", Quests.LevelOf(_host).Level == 2 && Quests.LevelOf(_host).Done1 == 11);
			Quests.LevelDoneForTest(_host, QuestKind.Undead, Toast);
			Check("the fifteenth site opens level 3", Quests.LevelOf(_host).Level == 3, Quests.LevelLine(_host));
			Check("the tasks window says so", Quests.LevelLine(_host).StartsWith("Quest level 3", StringComparison.Ordinal), Quests.LevelLine(_host));
			Quests.SetLevelForTest(_host, 1, 0, 0, 0);
			Check("level 1: the line counts toward level 2", Quests.LevelLine(_host) == "Quest level 1: 0/10 tasks toward level 2", Quests.LevelLine(_host));
			yield break;
		}

		private IEnumerator Askers()
		{
			Quests.SetLevelForTest(_host, 1, 0, 0, 0);
			yield return Talk(_king, true, "Any work for me?");
			List<string> list = Labels();
			Check("level 1: no choice of levels", !list.Exists((string l) => l.StartsWith("I'll hunt the dead", StringComparison.Ordinal)), string.Join(" | ", list.ToArray()));
			Mod.EndConversation();
			yield return CivilTest.Wait(0.3f);
			Quests.SetLevelForTest(_host, 2, 10, 0, 0);
			foreach (Actor item in Founded)
			{
				if (item != null && item != _host && item.isAlive() && item.isAdult() && !Quests.GivesHigherWork(item))
				{
					_villager = item;
					break;
				}
			}
			yield return Talk(_villager, true, "Any work for me?");
			list = Labels();
			Check("a villager offers everyday work only", !list.Exists((string l) => l.StartsWith("I'll hunt the dead", StringComparison.Ordinal)), string.Join(" | ", list.ToArray()));
			Mod.EndConversation();
			yield return CivilTest.Wait(0.3f);
			yield return Talk(_king, true, "Any work for me?");
			list = Labels();
			Check("level 2: the king offers the dead and everyday work", list.Exists((string l) => l.StartsWith("I'll hunt the dead", StringComparison.Ordinal)) && list.Exists((string l) => l.StartsWith("Everyday work", StringComparison.Ordinal)), string.Join(" | ", list.ToArray()));
			Check("...but no raid before level 3", !list.Exists((string l) => l.StartsWith("Defend the town", StringComparison.Ordinal)));
			Mod.EndConversation();
			yield return CivilTest.Wait(0.3f);
			Quests.SetLevelForTest(_host, 3, 10, 15, 0);
			yield return Talk(_king, true, "Any work for me?");
			list = Labels();
			Check("level 3: a raid first", list.Count > 0 && list[0].StartsWith("Defend the town", StringComparison.Ordinal), string.Join(" | ", list.ToArray()));
			Mod.EndConversation();
			yield return CivilTest.Wait(0.3f);
			List<Quest> list2 = Quests.Today(_city, _host);
			Note("board: " + string.Join(", ", list2.ConvertAll((Quest n) => n.Kind.ToString()).ToArray()));
			Quests.SetLevelForTest(_host, 1, 0, 0, 0);
			List<Quest> list3 = Quests.Today(_city, _host);
			Check("the board shows a level-1 body no site and no raid", !list3.Exists((Quest n) => n.Kind == QuestKind.Undead || n.Kind == QuestKind.Raid));
			Check("...and a level-3 body a raid and a site", list2.Exists((Quest n) => n.Kind == QuestKind.Raid) && list2.Exists((Quest n) => n.Kind == QuestKind.Undead));
		}

		private IEnumerator Site()
		{
			Quests.SetLevelForTest(_host, 2, 10, 0, 0);
			ClearDead();
			yield return Talk(_king, false, "Any work for me?", "I'll hunt the dead.", "I'll do it.");
			Quest q = Quests.Log.Find((Quest x) => x.Kind == QuestKind.Undead);
			if (!Check("a site of the dead taken from the king", q != null, _missing))
			{
				yield break;
			}
			Vector2 vector = Raiders.Centre(_city);
			Vector2 vector2 = new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f);
			float magnitude = (vector2 - vector).magnitude;
			Check("the town holds it (the king may fall)", q.FromBoard && Quests.TownLevelTaskForTest(_city) != null, Quests.Status(q));
			Check("the site is 25-40 tiles out", magnitude >= 22f && magnitude <= 43f, $"{magnitude:F1} tiles, {q.Place}");
			Check("nothing rises before you come near", Quests.SiteIdsForTest(q).Count == 0, Quests.Status(q));
			Check("the king has no second site to give", Quests.MakeForTest(QuestKind.Undead, _king, _host) == null);
			_host.current_position = vector2 + new Vector2(0f, -12f);
			Mod.SetYaw(MathF.PI / 2f);
			Mod.SetPitchDegrees(-4f);
			yield return CivilTest.Wait(1.5f);
			List<long> ids = Quests.SiteIdsForTest(q);
			Check("the dead rise as you come near", ids.Count >= 4 && ids.Count == q.Need, $"{ids.Count} rose, need {q.Need}: {Quests.Status(q)}");
			int num = 0;
			int num2 = 0;
			foreach (long item in ids)
			{
				Actor actor = World.world.units.get(item);
				string obj = actor?.asset?.id ?? "";
				if (obj.StartsWith("zombie", StringComparison.Ordinal))
				{
					num++;
				}
				if (obj == "skeleton")
				{
					num2++;
				}
				try
				{
					actor?.makeWait(20f);
				}
				catch
				{
				}
			}
			Note($"risen: {num2} skeletons, {num} zombies");
			Check("skeletons among them", num2 > 0);
			yield return CivilTest.Wait(0.5f);
			Mod.SaveFrame("qlv_site.png");
			int killed = 0;
			foreach (long item2 in ids)
			{
				Actor actor2 = World.world.units.get(item2);
				if (actor2 != null && actor2.isAlive())
				{
					CivilTest.Kill(actor2);
					killed++;
				}
				if (killed == ids.Count / 2)
				{
					yield return CivilTest.Wait(0.8f);
				}
			}
			ClearDead();
			yield return CivilTest.Wait(1.5f);
			Check("the site is clear: back to the town", q.Stage == 1, Quests.Status(q));
			int money = CivilTest.Money(_host);
			_host.current_position = Raiders.Centre(_city);
			yield return CivilTest.Wait(0.5f);
			yield return Talk(_king, false, "The dead at");
			Check("reported to the king: paid", CivilTest.Money(_host) > money && !Quests.Log.Contains(q), $"{money} -> {CivilTest.Money(_host)}");
			Check("...one hunt of the dead counted", Quests.LevelOf(_host).Done2 == 1, Quests.LevelLine(_host));
			Check("...no pick for the first site", Quests.PicksOwed(_host) == 0);
			Check("...and the town's slot is free again", Quests.TownLevelTaskForTest(_city) == null);
			Quests.SetLevelForTest(_host, 2, 10, 3, 0);
			Quest quest = Quests.MakeForTest(QuestKind.Undead, _king, _host);
			Check("the fourth site has a necromancer", quest != null && quest.Thing == "necro" && quest.Need == 6, (quest != null) ? (Quests.Title(quest) + " " + quest.Need) : null);
		}

		private static void ClearDead()
		{
			foreach (Actor item in new List<Actor>(World.world.units.getSimpleList()))
			{
				string text = item?.asset?.id ?? "";
				if (item != null && item.isAlive() && (text == "skeleton" || text == "necromancer" || text.StartsWith("zombie", StringComparison.Ordinal)))
				{
					CivilTest.Kill(item);
				}
			}
		}

		private IEnumerator Picks()
		{
			Quests.SetLevelForTest(_host, 2, 10, 2, 0);
			Quest quest = Quests.MakeForTest(QuestKind.Undead, _king, _host);
			if (!Check("a third site", quest != null && Quests.Accept(quest, _host, Toast)))
			{
				yield break;
			}
			quest.Stage = 1;
			Quests.Save(quest);
			yield return Talk(_king, true, "The dead at");
			List<string> list = Labels();
			List<string> list2 = list.FindAll((string l) => l.Contains(": you'll be "));
			Check("the third site ends with a pick of three traits", list2.Count == 3, string.Join(" | ", list.ToArray()));
			Check("...one pick owed until it's made", Quests.PicksOwed(_host) == 1);
			string chosen = null;
			if (list2.Count > 0)
			{
				chosen = list2[0];
				Conversation converse = Mod.Converse;
				converse.LineStart -= 999f;
				converse.Pick(converse.Choices.FindIndex((Choice ch) => ch.Label == chosen));
				yield return CivilTest.Wait(0.3f);
			}
			Mod.EndConversation();
			yield return CivilTest.Wait(0.3f);
			List<string> list3 = TraitSwitch.EarnedOf(_host);
			Check("the trait is yours, and earned (it can be switched)", list3.Count == 1 && _host.hasTrait(list3[0]), string.Join(",", list3.ToArray()) + " from " + chosen);
			Check("...nothing owed now", Quests.PicksOwed(_host) == 0);
		}

		private IEnumerator Raid()
		{
			Quests.SetLevelForTest(_host, 3, 10, 15, 0);
			Quests.GoodPickForTest = true;
			_host.current_position = Raiders.Centre(_city);
			yield return CivilTest.Wait(0.3f);
			yield return Talk(_king, false, "Any work for me?", "Defend the town.", "I'll do it.");
			Quest q = Quests.Log.Find((Quest x) => x.Kind == QuestKind.Raid);
			Raiders.Raid r = Raiders.Current;
			if (!Check("a raid taken from the king: the warning", q != null && r != null && r.State == Raiders.Phase.Warning && r.TownId == _city.data.id, (q != null) ? Quests.Status(q) : _missing))
			{
				yield break;
			}
			Check("bandits first (no raids held, a village)", r.Kind == Raiders.Band.Bandits && r.Waves.Count == 3, r.What);
			Check("the card counts down", Quests.Status(q).Contains("here in"), Quests.Status(q));
			Check("one raid at a time", Quests.MakeForTest(QuestKind.Raid, _king, _host) == null);
			_host.current_position = Raiders.Centre(_city);
			int waveSeen = 0;
			int chiefSeen = 0;
			bool fled = false;
			for (float t = 0f; t < 90f; t += 0.5f)
			{
				if (r.Over)
				{
					break;
				}
				_host.current_position = Raiders.Centre(_city);
				yield return CivilTest.Wait(0.5f);
				if (r.WavesSent > waveSeen)
				{
					waveSeen = r.WavesSent;
					Note($"wave {waveSeen}: {r.Units.Count} raiders about, {Quests.Status(q)}");
					if (waveSeen == 1)
					{
						Mod.SaveFrame("qlv_raid.png");
						Check("the horn: wave 1 marches", r.State == Raiders.Phase.Fighting && r.Units.Count >= 3);
					}
					if (waveSeen == 3)
					{
						Actor actor = World.world.units.get(r.ChiefId);
						if (actor != null && actor.isAlive() && actor.hasTrait("giant"))
						{
							chiefSeen++;
						}
						Check("the last wave brings the chief", chiefSeen > 0, r.ChiefName);
					}
					int num = ((waveSeen >= 3) ? 2 : 0);
					List<Actor> list = new List<Actor>(r.Units);
					for (int num2 = num; num2 < list.Count; num2++)
					{
						CivilTest.Kill(list[num2]);
					}
				}
				if (r.State == Raiders.Phase.Fleeing && !fled)
				{
					fled = true;
					Note($"they flee: {r.Units.Count} left, {r.Dead} of {r.Sent} dead");
				}
			}
			Check("after the last wave, half dead: the rest flee", fled);
			Check("the raid is held", r.State == Raiders.Phase.Held, r.Status(World.world.getCurWorldTime()));
			yield return CivilTest.Wait(1f);
			q = Quests.Log.Find((Quest x) => x.Kind == QuestKind.Raid) ?? q;
			Check("the town held: back to the king", q.Stage == 1, Quests.Status(q));
			int pieces = Gear.Count(_host);
			yield return Talk(_king, true, _city.name + " held.");
			List<string> list2 = Labels();
			Check("held: a weapon with a power to pick", list2.Exists((string l) => l.StartsWith(WeaponTraits.Name("flame"), StringComparison.Ordinal)), string.Join(" | ", list2.ToArray()));
			Conversation c = Mod.Converse;
			int num3 = c.Choices.FindIndex((Choice ch) => ch.Label.StartsWith(WeaponTraits.Name("flame"), StringComparison.Ordinal));
			if (num3 >= 0)
			{
				c.LineStart -= 999f;
				c.Pick(num3);
				yield return CivilTest.Wait(0.3f);
			}
			List<Gear.Piece> list3 = Gear.Of(_host);
			Gear.Piece piece = ((list3.Count > pieces) ? list3[list3.Count - 1] : null);
			Item item = WeaponTraits.Weapon(_host);
			bool flag = piece != null && piece.Id.EndsWith("_iron", StringComparison.Ordinal) && Array.IndexOf(piece.Mods, "flame") >= 0;
			bool flag2 = item != null && item.asset != null && item.asset.id.EndsWith("_iron", StringComparison.Ordinal) && item.hasMod("flame");
			Check("an iron weapon of flame, yours", flag || flag2, ((piece != null) ? ("bag " + piece.Id + " " + string.Join(",", piece.Mods)) : "bag none") + ", hand " + (item?.asset?.id ?? "none"));
			list2 = Labels();
			List<string> good = list2.FindAll((string l) => l.Contains(": you'll be "));
			Check("...and (this time) a pick of good traits", good.Count > 0, string.Join(" | ", list2.ToArray()));
			if (good.Count > 0)
			{
				c.LineStart -= 999f;
				c.Pick(c.Choices.FindIndex((Choice ch) => ch.Label == good[0]));
				yield return CivilTest.Wait(0.3f);
			}
			Mod.EndConversation();
			yield return CivilTest.Wait(0.3f);
			Check("a raid held counted", Quests.LevelOf(_host).Done3 == 1, Quests.LevelLine(_host));
			List<string> learned = Abilities.LearnedOf(_host);
			Check("...and taught a new spell", learned.Count == 1 && Abilities.Known(_host).Exists((Ability ab) => ab.SpellId == learned[0]), string.Join(",", learned.ToArray()));
			Check("...two traits earned now", TraitSwitch.EarnedOf(_host).Count == 2, string.Join(",", TraitSwitch.EarnedOf(_host).ToArray()));
			Quests.GoodPickForTest = false;
		}

		private IEnumerator LeaveRaid()
		{
			_host.current_position = Raiders.Centre(_city);
			yield return CivilTest.Wait(0.3f);
			yield return Talk(_king, false, "Any work for me?", "Defend the town.", "I'll do it.");
			Quest quest = Quests.Log.Find((Quest x) => x.Kind == QuestKind.Raid);
			Raiders.Raid r = Raiders.Current;
			if (!Check("a second raid", quest != null && r != null && !r.Over, _missing))
			{
				yield break;
			}
			Check("its raiders are stronger now (one raid held)", r.Waves[0].Units.Length >= 3, r.What + " " + r.Waves[0].Units.Length);
			for (float t = 0f; t < 20f; t += 0.5f)
			{
				if (r.State != Raiders.Phase.Warning)
				{
					break;
				}
				_host.current_position = Raiders.Centre(_city);
				yield return CivilTest.Wait(0.5f);
			}
			Check("the horn sounds", r.State == Raiders.Phase.Fighting);
			Vector2 far = Raiders.Centre(_city) + new Vector2(0f, 45f);
			int failed = Quests.Failed;
			for (float t = 0f; t < 25f; t += 0.5f)
			{
				if (!Quests.Log.Exists((Quest x) => x.Kind == QuestKind.Raid))
				{
					break;
				}
				_host.current_position = far;
				yield return CivilTest.Wait(0.5f);
			}
			Check("leaving the town fails it", !Quests.Log.Exists((Quest x) => x.Kind == QuestKind.Raid) && Quests.Failed == failed + 1, $"failed {failed} -> {Quests.Failed}");
			Check("...the raid goes on without you", !r.Over || r.State == Raiders.Phase.Lost);
			for (float t = 0f; t < 40f; t += 0.5f)
			{
				if (r.Over)
				{
					break;
				}
				foreach (Actor item in new List<Actor>(r.Units))
				{
					CivilTest.Kill(item);
				}
				yield return CivilTest.Wait(0.5f);
			}
			Raiders.Release(r);
			_host.current_position = Raiders.Centre(_city);
		}

		private IEnumerator Heir()
		{
			Quests.SetLevelForTest(_host, 3, 12, 15, 2);
			Actor villager = _villager;
			Quests.SetLevelForTest(villager, 1, 4, 0, 0);
			Rep.Remember(_host);
			Rep.Inherit(_host, villager);
			Quests.LevelState levelState = Quests.LevelOf(villager);
			Check("an heir keeps the level and the higher counts", levelState.Level == 3 && levelState.Done1 == 12 && levelState.Done2 == 15 && levelState.Done3 == 2, Quests.LevelLine(villager));
			Check("...earned traits are their own (not passed on)", TraitSwitch.EarnedOf(villager).Count == 0);
			yield break;
		}
	}
}
