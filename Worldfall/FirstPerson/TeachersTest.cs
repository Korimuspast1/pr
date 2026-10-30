using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class TeachersTest : CivilTest
	{
		private Actor _me;

		private List<Actor> _pool = new List<Actor>();

		protected override string Prefix => "TEACH";

		protected override IEnumerator Body()
		{
			List<string> list = new List<string>();
			string[] array = new string[26]
			{
				"strong", "tough", "hard_skin", "boosted_vitality", "block", "dodge", "backstep", "battle_reflexes", "eagle_eyed", "fast",
				"agile", "dash", "miner", "heart_of_wizard", "arcane_reflexes", "fire_proof", "freeze_proof", "regeneration", "blessed", "sunblessed",
				"moonchild", "nightchild", "lucky", "wise", "long_liver", "strong_minded"
			};
			foreach (string text in array)
			{
				if (AssetManager.traits.get(text) == null)
				{
					list.Add(text);
				}
			}
			Check("every trait a teacher teaches is one of the game's", list.Count == 0, (list.Count > 0) ? ("missing: " + string.Join(", ", list.ToArray())) : null);
			Actor king = Village("human", 14, 0);
			if (!Check("a village", king != null))
			{
				yield break;
			}
			yield return CivilTest.Wait(2f);
			List<Actor> list2 = Founded.Where((Actor u) => u != null && u.isAlive() && u != king && u.isAdult()).ToList();
			if (!Check("villagers", list2.Count >= 9, list2.Count + " adults"))
			{
				yield break;
			}
			_me = list2[0];
			_pool = list2.Skip(1).ToList();
			foreach (Actor item in _pool)
			{
				try
				{
					((BaseSimObject)item).addStatusEffect("invincible", 900f, pColorEffect: false);
				}
				catch
				{
				}
			}
			try
			{
				((BaseSimObject)king).addStatusEffect("invincible", 900f, pColorEffect: false);
			}
			catch
			{
			}
			foreach (Actor item2 in Founded)
			{
				try
				{
					item2.removeTrait("greedy");
					item2.removeTrait("honest");
					item2.removeTrait("pacifist");
				}
				catch
				{
				}
			}
			yield return Possess(_me);
			if (!Check("first person", InControl(_me)))
			{
				yield break;
			}
			try
			{
				_me.addMoney(3000);
			}
			catch
			{
			}
			array = new string[7] { "strong", "eagle_eyed", "miner", "heart_of_wizard", "lucky", "wise", "strong_minded" };
			foreach (string pTraitID in array)
			{
				try
				{
					_me.removeTrait(pTraitID);
				}
				catch
				{
				}
			}
			Actor soldier = Next();
			yield return Part("who teaches", Who(king, soldier));
			try
			{
				soldier.finishStatusEffect("invincible");
			}
			catch
			{
			}
			yield return Part("the soldier's spar", Spar(soldier));
			yield return Part("the hunter's shots", Shots(Next()));
			GiveUpLeftovers();
			yield return Part("the miner's ore", Ore(Next()));
			GiveUpLeftovers();
			if (!StillYou())
			{
				yield break;
			}
			yield return Part("the mage's fire", Fire(Next()));
			GiveUpLeftovers();
			if (!StillYou())
			{
				yield break;
			}
			yield return Part("the priest's vigil", Vigil(Next()));
			GiveUpLeftovers();
			if (StillYou())
			{
				yield return Part("the elder's story", Story(Next(), null));
				if (StillYou())
				{
					yield return Part("weapon traits", Weapons(king));
					yield return Part("the smith's forge", Forge(king));
				}
			}
		}

		private void GiveUpLeftovers()
		{
			Quest[] array = Quests.Log.ToArray();
			foreach (Quest quest in array)
			{
				if (Quests.IsTrial(quest.Kind))
				{
					Note("(gave up a trial left over: " + quest.Kind.ToString() + ")");
					Quests.Abandon(quest, _me);
				}
			}
		}

		private bool StillYou()
		{
			if (_me != null && _me.isAlive() && InControl(_me))
			{
				return true;
			}
			return Check("you're still alive and in your body (no one took against you after the spar)", ok: false);
		}

		private Actor Next()
		{
			while (_pool.Count > 0)
			{
				Actor actor = _pool[0];
				_pool.RemoveAt(0);
				if (actor != null && actor.isAlive())
				{
					return actor;
				}
				Note("(a villager died before their part)");
			}
			return null;
		}

		private IEnumerator Forge(Actor smith)
		{
			City town = smith.city;
			List<Traders.Ware> list = Traders.Wares(town);
			int grade = Traders.Grade(town);
			Note($"the forge of {town?.name} (grade {grade}): " + string.Join(", ", list.Select((Traders.Ware w) => w.Asset.id + "/" + Workbench.Rank(w.Asset)).ToArray()));
			Check("a smith forges a few pieces a day", list.Count >= 2, list.Count.ToString());
			Check("...no finer than the town works, nothing enchanted", list.All((Traders.Ware w) => Workbench.Rank(w.Asset) <= grade && (w.Asset.item_modifier_ids == null || !w.Asset.item_modifier_ids.Any((string m) => WeaponTraits.All.Contains(m)))));
			Check("...the same all day", string.Join(",", (from w in Traders.Wares(town)
				select w.Asset.id).ToArray()) == string.Join(",", list.Select((Traders.Ware w) => w.Asset.id).ToArray()));
			if (list.Count != 0)
			{
				Traders.Ware ware = list[0];
				string text = (("aeiou".IndexOf(ware.Name[0]) >= 0) ? "an " : "a ") + ware.Name;
				int purse = CivilTest.Money(_me);
				int price = Traders.Price(ware.Asset, 1f);
				yield return Talk(smith, "Let's trade.", "What can you forge for me?", text);
				Item item = null;
				try
				{
					item = _me.equipment.getSlot(ware.Asset.equipment_type).getItem();
				}
				catch
				{
				}
				Check("bought, made and put on", item != null && item.getAsset() == ware.Asset && CivilTest.Money(_me) < purse, $"wearing {item?.getAsset()?.id}, purse {purse} -> {CivilTest.Money(_me)} (about {price})");
				Check("...and not for sale again today", !Traders.Wares(town).Any((Traders.Ware w) => w.Asset == ware.Asset));
				Calendar.SetDayForTest(Calendar.Day + 1);
				Note("the next day: " + string.Join(", ", (from w in Traders.Wares(town)
					select w.Asset.id).ToArray()));
			}
		}

		private IEnumerator Weapons(Actor king)
		{
			if (!Check("a sword in hand", Equip(_me, "sword_iron")))
			{
				yield break;
			}
			Item sword = WeaponTraits.Weapon(_me);
			Crafting.Ordinary(sword);
			if (!Check("the king still alive and king", king != null && king.isAlive() && king.isKing()))
			{
				yield break;
			}
			try
			{
				_me.data.renown = 0;
				if (_me.isWarrior())
				{
					_me.stopBeingWarrior();
				}
			}
			catch
			{
			}
			Note($"you: soldier {Service.IsSoldier(_me)}, captain {Service.IsCaptain(_me)}, leader {_me.isCityLeader()}, renown {_me.data.renown}");
			Check("no boon before you've served", WeaponTraits.BoonRefusal(king, _me) == "serve them first", WeaponTraits.BoonRefusal(king, _me));
			try
			{
				_me.data.renown = 150;
			}
			catch
			{
			}
			Check("a boon once you have", WeaponTraits.BoonRefusal(king, _me) == null, WeaponTraits.BoonRefusal(king, _me));
			List<string> offer = WeaponTraits.BoonOffer(king, _me);
			Check("the king offers two of the five", offer.Count == 2 && offer.All((string m) => WeaponTraits.Common.Contains(m)), string.Join(", ", offer.ToArray()));
			yield return Talk(king, "I've served you well.", WeaponTraits.Name(offer[0]));
			Check("the boon is on your sword", sword.hasMod(offer[0]) && WeaponTraits.On(_me).Contains(offer[0]), string.Join(", ", WeaponTraits.On(_me).ToArray()));
			Check("...once", WeaponTraits.BoonRefusal(king, _me) == "already");
			Crafting.Ordinary(sword);
			Check("what you craft never keeps them (Crafting.Ordinary)", WeaponTraits.On(_me).Count == 0);
		}

		private static bool Equip(Actor unit, string id)
		{
			try
			{
				ActorEquipmentSlot slot = unit.equipment.getSlot(EquipmentType.Weapon);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				if (equipmentAsset == null)
				{
					return false;
				}
				Item pItem = World.world.items.generateItem(equipmentAsset, unit.kingdom, unit.getName(), 1, unit, 0, pByPlayer: true);
				slot.setItem(pItem, unit);
				unit.setStatsDirty();
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("TEACH equip " + id + ": " + ex.Message);
				return false;
			}
		}

		private IEnumerator Who(Actor king, Actor soldier)
		{
			try
			{
				soldier.addTrait("veteran", pRemoveOpposites: true);
				soldier.addTrait("strong", pRemoveOpposites: true);
				king.addTrait("veteran", pRemoveOpposites: true);
				king.addTrait("strong", pRemoveOpposites: true);
			}
			catch
			{
			}
			yield return null;
			List<KeyValuePair<string, Teachers.School>> list = Teachers.CanTeach(soldier);
			Check("a veteran with strength can teach it", list.Exists((KeyValuePair<string, Teachers.School> kv) => kv.Key == "strong" && kv.Value == Teachers.School.Soldier), string.Join(", ", list.Select((KeyValuePair<string, Teachers.School> kv) => kv.Key + "/" + kv.Value).ToArray()));
			Check("a king doesn't teach", Teachers.CanTeach(king).Count == 0);
			bool flag = false;
			try
			{
				flag = soldier.family != null && soldier.family == _me.family;
			}
			catch
			{
			}
			int num = Teachers.Fee(soldier, _me, "strong");
			Check("the fee: 40 gold a point (half for kin)", num == (flag ? 60 : 120), $"{num} gold, kin {flag}");
			try
			{
				_me.data.level = 1;
			}
			catch
			{
			}
			string text = Teachers.Refusal(soldier, _me, "strong");
			Check("not taught before you're ready (your level)", text?.Contains("level") ?? false, text);
			Check("deeds aren't taught", Teachers.Refusal(soldier, _me, "veteran") != null);
			try
			{
				_me.data.level = 10;
			}
			catch
			{
			}
			Check("taught once you are", Teachers.Refusal(soldier, _me, "strong") == null, Teachers.Refusal(soldier, _me, "strong"));
		}

		private IEnumerator Spar(Actor soldier)
		{
			int purse = CivilTest.Money(_me);
			int theirs = CivilTest.Money(soldier);
			int fee = Teachers.Fee(soldier, _me, "strong");
			yield return Talk(soldier, "Could you teach me something?", Teachers.Name("strong"), "I'll do it.", "Yes, that's right.");
			Quest quest = Mine(soldier);
			if (!Check("a lesson asked for in talk: the trial is on", quest != null && quest.Kind == QuestKind.TrialSpar, (quest != null) ? quest.Kind.ToString() : "none"))
			{
				yield break;
			}
			Check("the fee paid, to the teacher", CivilTest.Money(_me) == purse - fee && CivilTest.Money(soldier) == theirs + fee, $"you {purse} -> {CivilTest.Money(_me)}, them {theirs} -> {CivilTest.Money(soldier)}, fee {fee}");
			HealBoth(soldier);
			Near(soldier, 2f);
			for (float t = 0f; t < 4f; t += Time.unscaledDeltaTime)
			{
				if (Quests.BoutForTest)
				{
					break;
				}
				Near(soldier, 2f, hold: false);
				yield return null;
			}
			if (!Check("the bout begins as you step up", Quests.BoutForTest))
			{
				yield break;
			}
			float lowest = 1f;
			float swing = 0f;
			float waited = 0f;
			while (waited < 40f && (quest = Mine(soldier)) != null && quest.Stage == 0)
			{
				Face(soldier);
				if ((soldier.current_position - _me.current_position).magnitude > 1.6f)
				{
					_me.current_position = Vector2.MoveTowards(_me.current_position, soldier.current_position, 3f * Time.unscaledDeltaTime);
				}
				swing -= Time.unscaledDeltaTime;
				if (swing <= 0f)
				{
					swing = 0.9f;
					PossessionCombat.AttackForTest(_me);
				}
				lowest = Mathf.Min(lowest, _me.getHealthRatio());
				waited += Time.unscaledDeltaTime;
				yield return null;
			}
			quest = Mine(soldier);
			bool flag = quest != null && quest.Stage == 1;
			Note(string.Format("the spar: {0} after {1:F0} s, your health down to {2:F0}%", flag ? "won" : ((quest == null) ? "lost" : "undecided"), waited, lowest * 100f));
			Check("the spar is decided", flag || quest == null, (quest != null) ? Quests.Status(quest) : null);
			Check("nobody dies in a spar", _me.isAlive() && soldier.isAlive());
			Check("no crime, no bounty", Law.BountyOn(soldier.kingdom, _me) == 0);
			Check("the fight's over after", !Quests.BoutForTest && (!soldier.has_attack_target || soldier.attack_target != _me));
			if (flag)
			{
				yield return Talk(soldier, "About my trial...");
				Check("passed: you're strong", _me.hasTrait("strong") && Mine(soldier) == null);
			}
			else
			{
				Check("lost: no trying again before the next dawn", !Quests.MayTryAgain(soldier, "strong"));
				Calendar.SetDayForTest(Calendar.Day + 1);
				Check("...and after it, again", Quests.MayTryAgain(soldier, "strong"));
			}
		}

		private IEnumerator Shots(Actor hunter)
		{
			try
			{
				hunter.addTrait("eagle_eyed", pRemoveOpposites: true);
			}
			catch
			{
			}
			string refused;
			Quest q = Quests.TrialForTest(hunter, _me, "eagle_eyed", out refused);
			if (!Check("a hunter's trial", q != null && q.Kind == QuestKind.TrialRange, refused))
			{
				yield break;
			}
			Actor near = CivilTest.SpawnNear("sheep", _me.current_position, 3f);
			Actor far = CivilTest.SpawnNear("sheep", _me.current_position, 11f);
			if (!Check("sheep near and far", near != null && far != null))
			{
				yield break;
			}
			yield return null;
			Quests.TrialBlow(_me, near, hit: true, projectile: true);
			Check("a hit from three paces doesn't count", q.Have == 0, q.Have.ToString());
			Quests.TrialBlow(_me, far, hit: true, projectile: false);
			Check("...nor a blow", q.Have == 0);
			Quests.TrialBlow(_me, far, hit: true, projectile: true);
			Quests.TrialBlow(_me, far, hit: true, projectile: true);
			Check("a shot from afar counts, once", q.Have == 1, q.Have.ToString());
			Vector2 farAt = far.current_position;
			Vector2 meAt = _me.current_position;
			try
			{
				far.makeWait(30f);
			}
			catch
			{
			}
			for (int i = 0; i < 4; i++)
			{
				for (int f = 0; f < 22; f++)
				{
					far.current_position = farAt;
					_me.current_position = meAt;
					yield return null;
				}
				Quests.TrialBlow(_me, far, hit: true, projectile: true);
			}
			yield return CivilTest.Wait(0.8f);
			Check("five: done, go back", q.Stage == 1 || (Mine(hunter)?.Stage ?? (-1)) == 1, Quests.Status(q));
			yield return Talk(hunter, "About my trial...");
			Check("eagle-eyed", _me.hasTrait("eagle_eyed"));
			CivilTest.Kill(near);
			CivilTest.Kill(far);
		}

		private IEnumerator Ore(Actor miner)
		{
			try
			{
				miner.addTrait("miner", pRemoveOpposites: true);
			}
			catch
			{
			}
			Quest q = Quests.TrialForTest(miner, _me, "miner", out var refused);
			if (Check("a miner's trial", q != null && q.Kind == QuestKind.TrialOre, refused))
			{
				Quests.OnGathered("wood", 10);
				Check("wood isn't ore", q.Have == 0);
				for (int i = 0; i < 4; i++)
				{
					Quests.OnGathered((i % 2 == 0) ? "stone" : "common_metals", 5);
				}
				yield return CivilTest.Wait(0.8f);
				q = Mine(miner) ?? q;
				Check("twenty dug: done", q.Stage == 1, Quests.Status(q));
				yield return Talk(miner, "About my trial...");
				Check("a miner now", _me.hasTrait("miner"));
			}
		}

		private IEnumerator Fire(Actor mage)
		{
			try
			{
				mage.addTrait("heart_of_wizard", pRemoveOpposites: true);
			}
			catch
			{
			}
			Quest q = Quests.TrialForTest(mage, _me, "heart_of_wizard", out var refused);
			if (!Check("a mage's trial", q != null && q.Kind == QuestKind.TrialFire, refused))
			{
				yield break;
			}
			HealBoth(mage);
			Near(mage, 3f);
			for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
			{
				if (Quests.FireForTest.x >= 0)
				{
					break;
				}
				Near(mage, 3f, hold: false);
				yield return null;
			}
			Vector2Int c = Quests.FireForTest;
			if (!Check("the fire lit as you come close", c.x >= 0 && Burning(c, 1) > 0, $"{Burning(c, 1)} tiles burning"))
			{
				yield break;
			}
			int outside = 0;
			for (float t = 0f; t < 10f; t += Time.unscaledDeltaTime)
			{
				if ((Mine(mage)?.Stage ?? (-1)) != 0)
				{
					break;
				}
				_me.current_position = new Vector2((float)c.x + 0.5f, (float)c.y + 0.5f);
				try
				{
					mage.makeWait(1f);
				}
				catch
				{
				}
				outside = Mathf.Max(outside, Burning(c, 4) - Burning(c, 1));
				yield return null;
			}
			q = Mine(mage) ?? q;
			Check("stood in it: done", q.Stage == 1, Quests.Status(q));
			Check("the fire kept in its ring", outside <= 2, outside + " tiles burning outside it at most");
			yield return CivilTest.Wait(0.3f);
			Check("then put out", Burning(c, 4) == 0, Burning(c, 4) + " tiles");
			Check("you came through", _me.isAlive() && !((BaseSimObject)_me).hasStatus("burning"));
			yield return Talk(mage, "About my trial...");
			Check("touched by magic", _me.hasTrait("heart_of_wizard"));
		}

		private IEnumerator Vigil(Actor priest)
		{
			try
			{
				priest.addTrait("blessed", pRemoveOpposites: true);
			}
			catch
			{
			}
			Quest q = Quests.MakeTrial(priest, _me, "lucky", Teachers.School.Priest);
			if (!Check("a priest's vigil at a place", q.Kind == QuestKind.TrialVigil && q.X >= 0, q.Place) || !Check("taken on", Quests.Accept(q, _me, null)))
			{
				yield break;
			}
			Check("no deadline on a vigil", q.Deadline == 0.0);
			Vector2 spot = new Vector2((float)q.X + 0.5f, (float)q.Y + 0.5f);
			Mod.Settings.TimeMode = 0;
			Mod.Day.TimeOfDay = 0.6f;
			_me.current_position = spot + new Vector2(1.5f, 0f);
			yield return CivilTest.Wait(0.5f);
			Check("not begun by day", (Mine(priest) ?? q).Taken == 0);
			Mod.Day.TimeOfDay = 0.76f;
			yield return CivilTest.Wait(0.5f);
			Check("begun at dusk", (Mine(priest) ?? q).Taken == 1);
			_me.current_position = spot + new Vector2(16f, 0f);
			for (float t = 0f; t < 10f; t += Time.unscaledDeltaTime)
			{
				if ((Mine(priest) ?? q).Taken != 1)
				{
					break;
				}
				_me.current_position = spot + new Vector2(16f, 0f);
				yield return null;
			}
			Check("broken by walking off", (Mine(priest) ?? q).Taken == 0);
			Mod.Day.TimeOfDay = 0.77f;
			_me.current_position = spot + new Vector2(1.5f, 0f);
			yield return CivilTest.Wait(0.5f);
			Check("begun again", (Mine(priest) ?? q).Taken == 1);
			Mod.Day.TimeOfDay = 0.2492f;
			for (float t = 0f; t < 6f; t += Time.unscaledDeltaTime)
			{
				if ((Mine(priest) ?? q).Stage != 0)
				{
					break;
				}
				_me.current_position = spot + new Vector2(1.5f, 0f);
				yield return null;
			}
			Check("kept to dawn: done", (Mine(priest) ?? q).Stage == 1, Quests.Status(Mine(priest) ?? q));
			yield return Talk(priest, "About my trial...");
			Check("lucky", _me.hasTrait("lucky"));
			Mod.Day.TimeOfDay = 0.4f;
		}

		private IEnumerator Story(Actor elder, Actor other)
		{
			try
			{
				elder.addTrait("wise", pRemoveOpposites: true);
				elder.addTrait("strong_minded", pRemoveOpposites: true);
			}
			catch
			{
			}
			List<string> told;
			List<KeyValuePair<string, string[]>> asks = Quests.StoryForTest(elder, out told);
			Note("the elder's story: " + string.Join(" ", told.ToArray()));
			List<string> list = new List<string>
			{
				"Could you teach me something?",
				Teachers.Name("wise"),
				"I'll do it.",
				"Yes, that's right."
			};
			foreach (KeyValuePair<string, string[]> item in asks)
			{
				list.Add(item.Value[0]);
			}
			int purse = CivilTest.Money(_me);
			yield return Talk(elder, list.ToArray());
			Check("listened and answered right: wise", _me.hasTrait("wise") && Mine(elder) == null, $"purse {purse} -> {CivilTest.Money(_me)}");
			list = new List<string>
			{
				"Could you teach me something?",
				Teachers.Name("strong_minded"),
				"I'll do it.",
				"Yes, that's right."
			};
			foreach (KeyValuePair<string, string[]> item2 in asks)
			{
				list.Add(item2.Value[1]);
			}
			purse = CivilTest.Money(_me);
			yield return Talk(elder, list.ToArray());
			Check("answered wrong: no trait, the fee gone", !_me.hasTrait("strong_minded") && CivilTest.Money(_me) < purse && Mine(elder) == null, $"purse {purse} -> {CivilTest.Money(_me)}");
			Check("...and not before the next dawn", !Quests.MayTryAgain(elder, "strong_minded"));
			_ = other;
		}

		private static Quest Mine(Actor teacher)
		{
			return Quests.Log.Find((Quest q) => q.Giver == teacher.data.id && Quests.IsTrial(q.Kind));
		}

		private static void HealBoth(Actor a)
		{
			try
			{
				a.restoreHealth(a.getMaxHealth());
			}
			catch
			{
			}
			try
			{
				WorldBoxMod.Instance.Host.restoreHealth(WorldBoxMod.Instance.Host.getMaxHealth());
			}
			catch
			{
			}
		}

		private void Near(Actor npc, float d, bool hold = true)
		{
			_me.current_position = npc.current_position - new Vector2(d, 0f);
			Mod.SetYaw(0f);
			if (hold)
			{
				try
				{
					npc.makeWait(3f);
				}
				catch
				{
				}
			}
		}

		private void Face(Actor npc)
		{
			Vector2 vector = npc.current_position - _me.current_position;
			if (vector.sqrMagnitude > 0.0001f)
			{
				Mod.SetYaw(Mathf.Atan2(vector.y, vector.x));
			}
		}

		private static int Burning(Vector2Int c, int r)
		{
			int num = 0;
			for (int i = -r; i <= r; i++)
			{
				for (int j = -r; j <= r; j++)
				{
					WorldTile tile = World.world.GetTile(c.x + i, c.y + j);
					try
					{
						if (tile != null && tile.isOnFire())
						{
							num++;
						}
					}
					catch
					{
					}
				}
			}
			return num;
		}

		private static Choice Find(Conversation c, string start)
		{
			return c.Choices.Find((Choice ch) => ch.Label.StartsWith(start, StringComparison.Ordinal));
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
			Mod.StartConversationForTest(npc);
			foreach (string say in says)
			{
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
				Choice choice = Find(c, say);
				if (choice == null)
				{
					Note("(no \"" + say + "\" with " + Dialogue.NameOf(npc) + ": " + string.Join(" | ", c.Choices.ConvertAll((Choice x) => x.Label).ToArray()) + ")");
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
	}
}
