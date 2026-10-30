using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class ButcherTest : CivilTest
	{
		private Actor _me;

		private City _city;

		private readonly List<Actor> _pool = new List<Actor>();

		private bool _talked;

		private CitizenJobAsset _hunterJob;

		private readonly HashSet<Actor> _hunters = new HashSet<Actor>();

		protected override string Prefix => "BUTCHER";

		protected override IEnumerator Body()
		{
			Actor king = Village("human", 16, 0);
			if (!Check("a village", king != null))
			{
				yield break;
			}
			_city = king.city;
			yield return CivilTest.Wait(2f);
			List<Actor> list = Founded.Where((Actor u) => u != null && u.isAlive() && u != king && u.isAdult()).ToList();
			if (!Check("villagers", list.Count >= 8, list.Count + " adults"))
			{
				yield break;
			}
			_me = list.FirstOrDefault((Actor u) => !u.isWarrior()) ?? list[0];
			_pool.AddRange(list.Where((Actor u) => u != _me));
			foreach (Actor item in Founded)
			{
				try
				{
					((BaseSimObject)item).addStatusEffect("invincible", 900f, pColorEffect: false);
				}
				catch
				{
				}
			}
			yield return Possess(_me);
			if (Check("first person", InControl(_me)))
			{
				Check("the game's kill hand-out is taken over", Butchery.Installed);
				try
				{
					_me.addMoney(200);
				}
				catch
				{
				}
				Stockpile();
				yield return Part("a sheep you kill", YourKill());
				yield return Part("a townsman's kill", TheirKill());
				yield return Part("a wolf's kill", WolfKill());
				yield return Part("a hunter's hunt", Hunt());
				yield return Part("a great hunt", GreatHunt());
				yield return Part("the board", Board());
				Quests.BigHuntForTest = -1;
			}
		}

		private int MeatIn(Actor a)
		{
			return Civil.BagCount(a, "meat");
		}

		private static void KillBy(Actor victim, Actor by)
		{
			try
			{
				victim.finishStatusEffect("invincible");
			}
			catch
			{
			}
			try
			{
				((BaseSimObject)victim).getHit(99999f, pFlash: true, AttackType.Weapon, (BaseSimObject)by, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: false);
			}
			catch (Exception ex)
			{
				Log.Error("BUTCHER kill: " + ex.Message);
			}
		}

		private void Face(Vector2 at, float d = 1.1f)
		{
			_me.current_position = at - new Vector2(d, 0f);
			Mod.SetYaw(0f);
		}

		private IEnumerator YourKill()
		{
			Equip(null);
			Mod.SetYaw(0f);
			Actor sheep = CivilTest.SpawnNear("sheep", _me.current_position + new Vector2(2f, 0f));
			if (!Check("a sheep", sheep != null))
			{
				yield break;
			}
			try
			{
				sheep.makeWait();
			}
			catch
			{
			}
			Face(sheep.current_position, 2f);
			yield return CivilTest.Wait(0.6f);
			long id = sheep.data.id;
			int before = MeatIn(_me);
			KillBy(sheep, _me);
			yield return CivilTest.Wait(0.6f);
			Butchery.Carcass c = Butchery.Of(id);
			Check("the sheep is dead", !sheep.isAlive() || sheep.data.id != id);
			Check("no meat jumped into your bag", MeatIn(_me) == before, $"{before} -> {MeatIn(_me)}");
			if (!Check("its carcass holds meat", c != null && c.MeatLeft > 0, (c != null) ? Butchery.Describe(c) : "no carcass"))
			{
				yield break;
			}
			Check("killed by you, its body lies longer", c.ByYou && c.Lie >= 59f, $"lie {c.Lie:F0} s, linked {c.Linked}");
			Check("its body fell in view", c.Linked);
			Mod.ViewForTest(on: false);
			for (int i = 0; i < 60; i++)
			{
				if (!Mod.IsFirstPerson)
				{
					break;
				}
				yield return null;
			}
			bool wasOut = !Mod.IsFirstPerson;
			Mod.ViewForTest(on: true);
			for (int i = 0; i < 120; i++)
			{
				if (Mod.IsFirstPerson)
				{
					break;
				}
				yield return null;
			}
			yield return CivilTest.Wait(0.3f);
			Check("V twice: the carcass lies on, its meat on it", wasOut && Mod.IsFirstPerson && Butchery.Of(id) == c && c.MeatLeft > 0, $"out {wasOut}, back {Mod.IsFirstPerson}, " + ((Butchery.Of(id) != null) ? Butchery.Describe(Butchery.Of(id)) : "no carcass"));
			Mod.Holstered = true;
			Face(c.At, 1.4f);
			Mod.SetPitchDegrees(-32f);
			yield return CivilTest.Wait(0.9f);
			Picture("butcher_carcass.png");
			yield return CivilTest.Wait(0.1f);
			Face(c.At);
			yield return CivilTest.Wait(0.1f);
			int total = c.Left;
			int got0 = MeatIn(_me);
			PossessionCombat.AttackForTest(_me);
			yield return CivilTest.Wait(0.3f);
			Check("a blow barehanded cuts one piece", c.Left == total - 1 && MeatIn(_me) == got0 + 1, $"left {c.Left} of {total}, meat {got0} -> {MeatIn(_me)}");
			Note("prompt: " + (Mod.Prompt ?? "none"));
			Mod.Holstered = false;
			yield return CivilTest.Wait(0.2f);
			bool inHand = Hotbar.Food(_me) != null;
			int meat1 = MeatIn(_me);
			WorldBoxMod.HotbarClickForTest = Time.frameCount + 1;
			yield return CivilTest.Wait(0.3f);
			Check("food in hand, a click aimed at the carcass doesn't eat it", MeatIn(_me) >= meat1, $"in hand {inHand}, meat {meat1} -> {MeatIn(_me)}");
			Check("...and doesn't hold the blow back", !PossessionHooks.BlockAttack, $"in hand {inHand}");
			bool sword = Equip("sword_iron");
			int left = c.Left;
			Face(c.At);
			yield return CivilTest.Wait(0.1f);
			PossessionCombat.AttackForTest(_me);
			yield return CivilTest.Wait(0.3f);
			Check("a blow with a sword cuts two", sword && c.Left == Mathf.Max(0, left - 2), $"left {left} -> {c.Left}");
			Face(c.At, 1.4f);
			yield return CivilTest.Wait(0.5f);
			Picture("butcher_cut.png");
			yield return CivilTest.Wait(0.1f);
			for (int i = 0; i < 20; i++)
			{
				if (c.Left <= 0)
				{
					break;
				}
				Face(c.At);
				yield return CivilTest.Wait(0.05f);
				PossessionCombat.AttackForTest(_me);
				yield return CivilTest.Wait(0.15f);
			}
			Check("cut bare", c.Left == 0, Butchery.Describe(c));
			Check("all its meat in your bag", MeatIn(_me) - before >= 1, $"+{MeatIn(_me) - before} meat");
			yield return CivilTest.Wait(2.6f);
			Check("the bare carcass is gone", Butchery.Of(id) == null);
			Mod.SetPitchDegrees(0f);
			Equip(null);
		}

		private IEnumerator TheirKill()
		{
			Actor man = _pool.FirstOrDefault((Actor u) => u.isAlive() && !u.isWarrior());
			if (Check("a townsman", man != null))
			{
				Actor sheep = CivilTest.SpawnNear("sheep", man.current_position, 1.5f);
				if (Check("a sheep by him", sheep != null))
				{
					yield return CivilTest.Wait(0.4f);
					long id = sheep.data.id;
					int before = MeatIn(man);
					KillBy(sheep, man);
					yield return CivilTest.Wait(0.5f);
					Check("his kill fills his own bag, the game's way", MeatIn(man) > before, $"{before} -> {MeatIn(man)}");
					Check("and leaves no carcass", Butchery.Of(id) == null);
				}
			}
		}

		private IEnumerator WolfKill()
		{
			Actor wolf = CivilTest.SpawnNear("wolf", _me.current_position + new Vector2(4f, 0f));
			Actor sheep = CivilTest.SpawnNear("sheep", _me.current_position + new Vector2(3f, 1f));
			if (Check("a wolf and a sheep", wolf != null && sheep != null))
			{
				try
				{
					wolf.makeWait();
					sheep.makeWait();
				}
				catch
				{
				}
				Mod.SetYaw(0f);
				yield return CivilTest.Wait(0.5f);
				long id = sheep.data.id;
				KillBy(sheep, wolf);
				yield return CivilTest.Wait(0.5f);
				Butchery.Carcass carcass = Butchery.Of(id);
				Check("a wolf's kill near you leaves a carcass to cut", carcass != null && !carcass.ByYou && carcass.MeatLeft > 0, (carcass != null) ? (Butchery.Describe(carcass) + $", lie {carcass.Lie:F0}") : "none");
				CivilTest.Kill(wolf);
			}
		}

		private void KeepHunter(Actor a)
		{
			try
			{
				if (_hunterJob != null && _hunters.Contains(a) && a.citizen_job != _hunterJob)
				{
					a.setCitizenJob(_hunterJob);
				}
			}
			catch
			{
			}
		}

		private Actor MakeHunter()
		{
			CitizenJobAsset citizenJobAsset = null;
			try
			{
				citizenJobAsset = AssetManager.citizen_job_library.get("hunter");
			}
			catch
			{
			}
			if (citizenJobAsset == null)
			{
				Note("(no hunter job in this game)");
				return null;
			}
			_hunterJob = citizenJobAsset;
			foreach (Actor item in _pool)
			{
				if (item == null || !item.isAlive() || item.isWarrior() || item.isKing() || item.isCityLeader())
				{
					continue;
				}
				Quest quest = Quests.Read(item.data);
				if (quest == null || quest.Stage >= 2)
				{
					try
					{
						item.setCitizenJob(citizenJobAsset);
					}
					catch (Exception ex)
					{
						Note("job: " + ex.Message);
						continue;
					}
					if (Quests.IsHunter(item))
					{
						_hunters.Add(item);
						return item;
					}
				}
			}
			return null;
		}

		private IEnumerator Talk(Actor npc, params string[] says)
		{
			Conversation c = Mod.Converse;
			_me.current_position = npc.current_position - new Vector2(1.4f, 0f);
			Mod.SetYaw(0f);
			try
			{
				npc.makeWait(8f);
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.3f);
			KeepHunter(npc);
			Mod.StartConversationForTest(npc);
			_talked = c.Open;
			foreach (string say in says)
			{
				KeepHunter(npc);
				for (int j = 0; j < 8; j++)
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
					_me.current_position = npc.current_position - new Vector2(1.4f, 0f);
					try
					{
						npc.makeWait(8f);
					}
					catch
					{
					}
				}
				Choice choice = c.Choices.Find((Choice x) => x.Label.StartsWith(say, StringComparison.Ordinal));
				if (choice == null)
				{
					Note("(no \"" + say + "\" with " + Dialogue.NameOf(npc) + ": " + string.Join(" | ", c.Choices.ConvertAll((Choice x) => x.Label).ToArray()) + ")");
					_talked = false;
					break;
				}
				c.LineStart -= 999f;
				c.Pick(c.Choices.IndexOf(choice));
				yield return CivilTest.Wait(0.2f);
				Note("  " + say + " -> \"" + c.Line + "\"");
			}
			Mod.EndConversation();
			yield return CivilTest.Wait(0.2f);
		}

		private static Quest Live(Quest q)
		{
			return Quests.Log.Find((Quest x) => x.Kind == q.Kind && x.Giver == q.Giver && x.FromBoard == q.FromBoard) ?? q;
		}

		private bool GroundsFine(Quest q, out string detail)
		{
			Quests.HuntPlan huntPlan = Quests.PlanOf(q);
			WorldTile tile = _city.getTile();
			List<string> list = new List<string>();
			bool flag = huntPlan != null && huntPlan.Grounds.Count > 0;
			if (huntPlan != null)
			{
				foreach (Quests.Ground ground in huntPlan.Grounds)
				{
					WorldTile tile2 = World.world.GetTile(ground.X, ground.Y);
					float num = Mathf.Sqrt((ground.X - tile.x) * (ground.X - tile.x) + (ground.Y - tile.y) * (ground.Y - tile.y));
					bool flag2 = tile2 != null && tile2.zone_city == null && tile2.isSameIsland(tile) && num >= 24f && num <= 82f;
					flag = flag && flag2;
					list.Add(string.Format("{0} at {1},{2} ({3:F0} tiles, {4}{5}){6}", ground.Land, ground.X, ground.Y, num, ground.Prey, string.IsNullOrEmpty(ground.Pred) ? "" : (" + " + ground.PredCount + " " + ground.Pred), flag2 ? "" : " BAD"));
				}
			}
			detail = string.Join("; ", list.ToArray());
			return flag;
		}

		private IEnumerator Reach(Quest q, Quests.Ground g)
		{
			WorldTile tile = _city.getTile();
			Vector2 vector = new Vector2((float)g.X + 0.5f, (float)g.Y + 0.5f);
			Vector2 vector2 = new Vector2(tile.x, tile.y) - vector;
			_me.current_position = vector + vector2.normalized * Mathf.Min(30f, vector2.magnitude * 0.5f);
			for (int i = 0; i < 12; i++)
			{
				if (g.Spawned)
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
				g = Quests.PlanOf(q)?.Grounds.Find((Quests.Ground x) => x.X == g.X && x.Y == g.Y) ?? g;
			}
		}

		private IEnumerator Harvest(Quest q, Quests.Ground g, Func<bool> enough)
		{
			int blows = 0;
			long[] array = g.Ids.ToArray();
			foreach (long id in array)
			{
				if (enough())
				{
					break;
				}
				Actor a = World.world.units.get(id);
				if (a == null || !a.isAlive())
				{
					continue;
				}
				_me.current_position = a.current_position - new Vector2(2f, 0f);
				Mod.SetYaw(0f);
				try
				{
					a.makeWait(5f);
				}
				catch
				{
				}
				yield return CivilTest.Wait(0.25f);
				KillBy(a, _me);
				yield return CivilTest.Wait(0.35f);
				Butchery.Carcass c = Butchery.Of(id);
				if (c == null)
				{
					Note($"(no carcass from {a.asset?.id} {id})");
					continue;
				}
				for (int j = 0; j < 40; j++)
				{
					if (c.Left <= 0)
					{
						break;
					}
					if (blows < 3)
					{
						Face(c.At);
						yield return CivilTest.Wait(0.1f);
						PossessionCombat.AttackForTest(_me);
						blows++;
						yield return CivilTest.Wait(0.15f);
					}
					else if (Butchery.Cut(_me, c, null) == 0)
					{
						break;
					}
				}
			}
		}

		private IEnumerator Hunt()
		{
			Quests.BigHuntForTest = 0;
			Actor hunter = MakeHunter();
			if (!Check("a hunter in town", hunter != null))
			{
				yield break;
			}
			Quest quest = Quests.Offer(hunter, _me);
			Check("a hunter's work is a meat hunt", quest != null && quest.Kind == QuestKind.Meat, (quest != null) ? Quests.Title(quest) : "nothing");
			yield return Talk(hunter, "Any work for me?", "I have a question.", "Where are the hunting grounds?", "How do I get the meat?", "No more questions.", "I'll do it.");
			Quest q = Quests.Log.Find((Quest x) => x.Kind == QuestKind.Meat && x.Giver == hunter.data.id);
			if (!Check("took the hunt in talk", _talked && q != null, (q != null) ? (Quests.Title(q) + " / " + Quests.Status(q)) : "none"))
			{
				yield break;
			}
			Check("its grounds lie out of town on the island", GroundsFine(q, out var detail), detail);
			Quests.HuntPlan h = Quests.PlanOf(q);
			Check("a normal hunt: one or two grounds", h != null && !h.Big && h.Grounds.Count >= 1 && h.Grounds.Count <= 2);
			List<Vector2> list = new List<Vector2>();
			Quests.HuntMarks(list);
			Check("the grounds are marked for the maps", list.Count >= 1, list.Count + " marks");
			Picture("butcher_minimap.png");
			yield return CivilTest.Wait(0.2f);
			Check("the compass leads to a ground", Quests.Heading(q, out var at) && (at - new Vector2((float)h.Grounds[0].X + 0.5f, (float)h.Grounds[0].Y + 0.5f)).magnitude < 90f);
			Quests.Ground g = h.Grounds[0];
			yield return Reach(q, g);
			h = Quests.PlanOf(q);
			g = h.Grounds[0];
			int num = g.Ids.Count((long id) => World.world.units.get(id)?.isAlive() ?? false);
			string pResult = null;
			try
			{
				World.world.units.get(g.Ids[0])?.data.get("fp_herd", out pResult, null);
			}
			catch
			{
			}
			Check("the herd came as you neared its ground", g.Spawned && g.Count >= 3 && num >= 3, $"{g.Count} {g.Prey}, {num} alive" + ((g.PredCount > 0) ? $", {g.PredCount} {g.Pred}" : ""));
			Check("its animals wear the hunt's mark", pResult == Quests.HerdTag(q).ToString(), pResult ?? "none");
			Vector2 herd;
			bool ok = Quests.Heading(q, out herd) && h.Grounds.Exists((Quests.Ground x) => x.Ids.Exists(delegate(long id)
			{
				Actor actor = World.world.units.get(id);
				return actor != null && actor.isAlive() && (actor.current_position - herd).sqrMagnitude < 0.01f;
			}));
			Check("the compass leads to the herd", ok, $"{herd.x:F0},{herd.y:F0}");
			yield return Harvest(q, g, () => MeatIn(_me) >= q.Need);
			Note($"meat after the herd: {MeatIn(_me)} of {q.Need}");
			if (MeatIn(_me) < q.Need)
			{
				try
				{
					_me.addToInventory("meat", q.Need - MeatIn(_me));
				}
				catch
				{
				}
			}
			yield return CivilTest.Wait(1.2f);
			q = Live(q);
			Check("enough meat: back to the hunter", q.Stage == 1, Quests.Status(q));
			int money = CivilTest.Money(_me);
			int stores = Stores();
			int carried = MeatIn(hunter);
			int meat = MeatIn(_me);
			yield return Talk(hunter, "About what you asked of me...", "Here: ");
			Check("handed in: paid", CivilTest.Money(_me) >= money + q.Money && !Quests.Log.Contains(q), $"+{CivilTest.Money(_me) - money} of {q.Money}");
			Check("the meat left your bag for the town", MeatIn(_me) == meat - q.Need && (Stores() >= stores + q.Need || MeatIn(hunter) >= carried + q.Need), $"bag {meat} -> {MeatIn(_me)}, stores {stores} -> {Stores()}, hunter {carried} -> {MeatIn(hunter)}");
		}

		private IEnumerator GreatHunt()
		{
			Quests.BigHuntForTest = 1;
			Actor hunter = MakeHunter();
			if (!Check("another hunter", hunter != null))
			{
				yield break;
			}
			Quests.ForceKindForTest = 22;
			yield return Talk(hunter, "Any work for me?", "I'll do it.");
			Quests.ForceKindForTest = -1;
			Quest q = Quests.Log.Find((Quest x) => x.Kind == QuestKind.Meat && x.Giver == hunter.data.id);
			if (!Check("took a great hunt", _talked && q != null && Quests.IsBigHunt(q), (q != null) ? Quests.Title(q) : "none"))
			{
				yield break;
			}
			Quests.HuntPlan huntPlan = Quests.PlanOf(q);
			Check("three grounds, a predator on one", huntPlan.Grounds.Count == 3 && huntPlan.Grounds.Exists((Quests.Ground x) => !string.IsNullOrEmpty(x.Pred)), GroundsFine(q, out var detail) ? detail : ("BAD: " + detail));
			Check("a great hunt has more time", Quests.MonthsFor(q) == 96);
			try
			{
				_me.addToInventory("meat", 30);
			}
			catch
			{
			}
			yield return CivilTest.Wait(1.2f);
			Check("bought meat doesn't count", q.Have == 0 && q.Stage == 0, Quests.Status(q));
			for (int i = 0; i < 3; i++)
			{
				if (Live(q).Have >= q.Need)
				{
					break;
				}
				q = Live(q);
				Quests.Ground g = Quests.PlanOf(q).Grounds[i];
				yield return Reach(q, g);
				q = Live(q);
				g = Quests.PlanOf(q).Grounds[i];
				Note($"ground {i + 1}: {g.Count} {g.Prey}" + ((g.PredCount > 0) ? $", {g.PredCount} {g.Pred}" : "") + $"; herd meat so far {q.Have}");
				yield return Harvest(q, g, () => Live(q).Have >= q.Need);
			}
			yield return CivilTest.Wait(1.2f);
			q = Live(q);
			Check("meat cut from the herds counts", q.Have >= q.Need && q.Stage == 1, Quests.Status(q));
			int money = CivilTest.Money(_me);
			yield return Talk(hunter, "About what you asked of me...", "Here: ");
			Check("the great hunt paid", CivilTest.Money(_me) >= money + q.Money, $"+{CivilTest.Money(_me) - money} of {q.Money}");
		}

		private IEnumerator Board()
		{
			Quests.BigHuntForTest = 0;
			Actor actor = MakeHunter();
			ResetBoard();
			KeepHunter(actor);
			List<Quest> list = Quests.Today(_city, _me);
			Note("notices: " + string.Join(" | ", list.Select((Quest n) => n.Kind.ToString() + " from " + n.GiverName).ToArray()));
			Check("the board posts a hunter's hunt", actor != null && list.Exists((Quest n) => n.Kind == QuestKind.Meat && !n.FromBoard));
			Check("and a contract for several kinds of work", Enumerable.Count(list.Select((Quest n) => n.Kind).Distinct()) >= 3, list.Count + " notices");
			CitizenJobAsset citizenJob = null;
			try
			{
				citizenJob = AssetManager.citizen_job_library.get("gatherer_bushes") ?? AssetManager.citizen_job_library.get("farmer");
			}
			catch
			{
			}
			_hunters.Clear();
			foreach (Actor item in Civil.PeopleOf(_city))
			{
				if (Quests.IsHunter(item))
				{
					try
					{
						item.setCitizenJob(citizenJob);
					}
					catch
					{
					}
				}
			}
			ResetBoard();
			list = Quests.Today(_city, _me);
			Quest quest = list.Find((Quest n) => n.Kind == QuestKind.Meat && n.FromBoard);
			if (!Check("a town with no hunter posts its own hunt", quest != null, string.Join(" | ", list.Select((Quest n) => n.Kind.ToString() + " from " + n.GiverName).ToArray())))
			{
				yield break;
			}
			Check("taken off the board", Quests.TakeNotice(_city, quest, _me, Mod.Toast));
			Quest q = Quests.Log.Find((Quest x) => x.Kind == QuestKind.Meat && x.FromBoard && x.Giver == _city.data.id);
			string pResult = null;
			try
			{
				_city.data.get("fp_hq", out pResult, null);
			}
			catch
			{
			}
			Check("it's yours, kept by the town", q != null && !string.IsNullOrEmpty(pResult));
			Check("listed on the board as yours", Quests.TakenToday(_city).Contains(q));
			Quest quest2 = Quests.Parse(Quests.Write(q));
			Check("its text form keeps the hunt", quest2 != null && quest2.Extra == q.Extra && quest2.Kind == QuestKind.Meat && quest2.FromBoard && quest2.Need == q.Need, q.Extra);
			int num = q.Need - MeatIn(_me);
			if (num > 0)
			{
				try
				{
					_me.addToInventory("meat", num);
				}
				catch
				{
				}
			}
			yield return CivilTest.Wait(1.2f);
			q = Live(q);
			Check("enough meat: hand it in at the board", q.Stage == 1, Quests.Status(q));
			int num2 = CivilTest.Money(_me);
			Check("handed in at the board", Quests.HandInAtBoard(_city, q, _me, Mod.Toast) && CivilTest.Money(_me) >= num2 + q.Money, $"+{CivilTest.Money(_me) - num2} of {q.Money}");
			pResult = null;
			try
			{
				_city.data.get("fp_hq", out pResult, null);
			}
			catch
			{
			}
			Check("the town's hunt is done with", string.IsNullOrEmpty(pResult) && !Quests.Log.Contains(q));
		}

		private void Picture(string name)
		{
			try
			{
				Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, name));
			}
			catch (Exception ex)
			{
				Note("picture: " + ex.Message);
			}
		}

		private void ResetBoard()
		{
			try
			{
				_city.data.removeString("fp_dq");
			}
			catch
			{
			}
		}

		private int Stores()
		{
			try
			{
				return _city.getResourcesAmount("meat");
			}
			catch
			{
				return 0;
			}
		}

		private void Stockpile()
		{
			try
			{
				WorldTile tile = _city.getTile();
				for (int i = 0; i < 30; i++)
				{
					WorldTile tile2 = World.world.GetTile(tile.x + UnityEngine.Random.Range(-5, 6), tile.y + UnityEngine.Random.Range(-5, 6));
					if (tile2 == null || tile2.Type == null || !tile2.Type.ground || tile2.Type.block || (tile2.building != null && AssetStyle.IsSolid(tile2.building)))
					{
						continue;
					}
					Building building = World.world.buildings.addBuilding("stockpile", tile2, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					if (building != null)
					{
						if (building.city != _city)
						{
							_city.listBuilding(building);
						}
						Note($"stockpile at {tile2.x},{tile2.y}");
						break;
					}
				}
			}
			catch (Exception ex)
			{
				Note("stockpile: " + ex.Message);
			}
		}

		private bool Equip(string id)
		{
			try
			{
				ActorEquipmentSlot slot = _me.equipment.getSlot(EquipmentType.Weapon);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				if (id == null)
				{
					_me.setStatsDirty();
					return true;
				}
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				if (equipmentAsset == null)
				{
					return false;
				}
				Item pItem = World.world.items.generateItem(equipmentAsset, _me.kingdom, _me.getName(), 1, _me, 0, pByPlayer: true);
				slot.setItem(pItem, _me);
				_me.setStatsDirty();
				return true;
			}
			catch (Exception ex)
			{
				Note("equip " + id + ": " + ex.Message);
				return false;
			}
		}
	}
}
