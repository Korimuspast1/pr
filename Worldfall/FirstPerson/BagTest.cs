using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BagTest : CivilTest
	{
		private Actor _me;

		private Bundles.Bundle _sack;

		protected override string Prefix => "BAG";

		protected override IEnumerator Body()
		{
			Check("stacks: wood 50, ore 25, gems 25, bread 20", Bag.Stack("wood") == 50 && Bag.Stack("common_metals") == 25 && Bag.Stack("gems") == 25 && Bag.Stack("wheat") == 50 && Bag.Stack("bread") == 20, string.Format("{0} {1} {2} {3} {4}", Bag.Stack("wood"), Bag.Stack("common_metals"), Bag.Stack("gems"), Bag.Stack("wheat"), Bag.Stack("bread")));
			string text = Path.Combine(Log.Folder, "runs_bagtest.txt");
			try
			{
				if (File.Exists(text))
				{
					File.Delete(text);
				}
			}
			catch
			{
			}
			Run.HallFileForTest = text;
			Run.Forget();
			Actor king = Village("human", 12, 0);
			if (!Check("a village", king != null))
			{
				yield break;
			}
			yield return CivilTest.Wait(2f);
			List<Actor> adults = Founded.Where((Actor u) => u != null && u.isAlive() && u != king && u.isAdult()).ToList();
			if (!Check("villagers", adults.Count >= 4))
			{
				yield break;
			}
			_me = adults[0];
			foreach (Actor item in Founded)
			{
				try
				{
					((BaseSimObject)item).addStatusEffect("invincible", 600f, pColorEffect: false);
				}
				catch
				{
				}
			}
			yield return Possess(_me);
			if (Check("first person", InControl(_me)))
			{
				Empty(_me);
				Check("in the sandbox, the Frame pack", Bag.Size(_me) == 24 && Bag.Name(_me) == "Frame pack");
				Run.Picked.Clear();
				Check("locked in", Run.Lock(_me, Run.Picked, base.Note) && Run.Locked);
				yield return Part("slots", Slots());
				yield return Part("a bigger bag", Bigger(king));
				yield return Part("selling gear", SellGear(king));
				yield return Part("looting a bandit", LootBandit());
				yield return Part("your kill keeps its coins and bag", YourKill());
				yield return Part("robbing a townsman", RobTownsman(adults[1]));
				yield return Part("founding alone", FoundAlone(king));
				Run.Asking = Run.Ask.Abandon;
				Mod.RunAnswer(abandon: true);
				Run.LegacyOpen = false;
				Run.HallFileForTest = null;
				Check("given up: the Frame pack again", Bag.Size(_me) == 24);
			}
		}

		private IEnumerator Slots()
		{
			Empty(_me);
			Check("a locked-in life starts with a Pouch of 6 slots", Bag.Tier(_me) == 0 && Bag.Size(_me) == 6, Bag.Name(_me) + " " + Bag.Size(_me));
			int num = Bag.Put(_me, "wood", 125, spill: false);
			Check("125 wood: three slots", num == 125 && Bag.Used(_me) == 3, $"{num} in, {Bag.Used(_me)} used");
			int num2 = Bag.Put(_me, "gems", 70, spill: false);
			Check("70 gems: three more, the bag full", num2 == 70 && Bag.Used(_me) == 6 && Bag.Free(_me) == 0, $"{num2} in, {Bag.Used(_me)} used");
			Check("the wood's last stack still takes 25", Bag.RoomFor(_me, "wood") == 25 && Bag.RoomFor(_me, "gems") == 5 && Bag.RoomFor(_me, "stone") == 0, string.Format("wood {0}, gems {1}, stone {2}", Bag.RoomFor(_me, "wood"), Bag.RoomFor(_me, "gems"), Bag.RoomFor(_me, "stone")));
			int count = Bundles.List.Count;
			int num3 = Bag.Put(_me, "stone", 5, spill: true, base.Note);
			Bundles.Bundle sack = ((Bundles.List.Count > count) ? Bundles.List[Bundles.List.Count - 1] : null);
			Check("no room: the stone set down as a sack", num3 == 0 && sack != null && sack.Resource == "stone" && sack.Count == 5);
			Check("a spare piece won't go in either", !Gear.HasRoom(_me));
			yield return null;
			_sack = sack;
		}

		private IEnumerator Bigger(Actor trader)
		{
			CivilTest.Money(_me);
			try
			{
				_me.addMoney(500);
			}
			catch
			{
			}
			int purse = CivilTest.Money(_me);
			yield return Talk(trader, "Let's trade.", "A bigger bag");
			Check("a satchel bought: 10 slots", Bag.Tier(_me) == 1 && Bag.Size(_me) == 10 && CivilTest.Money(_me) < purse, $"{Bag.Name(_me)}, purse {purse} -> {CivilTest.Money(_me)}");
			if (_sack != null && Bundles.List.Contains(_sack))
			{
				bool flag = Bundles.PickUp(_me, _sack, base.Note);
				Check("the sack picked up now there's room", flag && Civil.BagCount(_me, "stone") == 5);
			}
		}

		private IEnumerator SellGear(Actor smith)
		{
			Gear.Piece p = new Gear.Piece
			{
				Id = "sword_iron"
			};
			Check("a spare sword in the bag", Gear.Add(_me, p));
			int purse = CivilTest.Money(_me);
			City town = smith.city;
			int armory = 0;
			try
			{
				armory = town.getEquipmentList(EquipmentType.Weapon).Count;
			}
			catch
			{
			}
			yield return Talk(smith, "Let's trade.", "I have gear to sell.", "Sell");
			int num = 0;
			try
			{
				num = town.getEquipmentList(EquipmentType.Weapon).Count;
			}
			catch
			{
			}
			Check("sold to the smith: gold for it, into the armory", Gear.Count(_me) == 0 && CivilTest.Money(_me) > purse && num == armory + 1, $"purse {purse} -> {CivilTest.Money(_me)}, armory {armory} -> {num}");
		}

		private IEnumerator LootBandit()
		{
			Empty(_me);
			Vector2 ahead = _me.current_position + new Vector2(3f, 0f);
			Actor bandit = CivilTest.SpawnNear("bandit", ahead);
			if (!Check("a bandit", bandit != null))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)bandit).addStatusEffect("invincible", 30f, pColorEffect: false);
				bandit.makeWait(30f);
			}
			catch
			{
			}
			Equip(bandit, "sword_iron");
			Item item = WeaponTraits.Weapon(bandit);
			try
			{
				item.addMod("flame");
			}
			catch
			{
			}
			try
			{
				int money = bandit.money;
				if (money < 30)
				{
					bandit.addMoney(30 - money);
				}
				else if (money > 30)
				{
					bandit.spendMoney(money - 30);
				}
				bandit.addToInventory("wheat", 3);
			}
			catch
			{
			}
			for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
			{
				Face(bandit);
				bandit.current_position = ahead;
				yield return null;
			}
			try
			{
				bandit.attackedBy = null;
			}
			catch
			{
			}
			Loot.WatchForTest(bandit);
			try
			{
				bandit.finishStatusEffect("invincible");
			}
			catch
			{
			}
			CivilTest.Kill(bandit);
			Loot.Fallen fallen = null;
			for (int i = 0; i < 30; i++)
			{
				if (fallen != null)
				{
					break;
				}
				Face(bandit);
				yield return null;
				fallen = Loot.Bodies.Find((Loot.Fallen f) => f.Name == Dialogue.NameOf(bandit));
			}
			if (Check("his body keeps what he had", fallen != null && fallen.Pieces.Exists((Gear.Piece x) => x.Id == "sword_iron" && x.Mods.Contains("flame")) && fallen.Money == 30, (fallen == null) ? "no body" : $"{fallen.Pieces.Count} pieces, {fallen.Money} gold, {fallen.Bag.Count} kinds"))
			{
				Check("...and lies a while", fallen.Lie >= 15f);
				Check("the prompt to search it", Loot.Near(_me.current_position, Mod.ViewYaw, 4.2f) == fallen);
				int num = CivilTest.Money(_me);
				int num2 = Law.BountyOn(_me.kingdom, _me);
				bool flag = Loot.Take(_me, fallen, base.Note);
				int num3 = Gear.Of(_me).FindIndex((Gear.Piece x) => x.Id == "sword_iron");
				Check("taken: his sword, his coins, his wheat", flag && num3 >= 0 && CivilTest.Money(_me) == num + 30 && Civil.BagCount(_me, "wheat") == 3);
				Check("no crime to rob a bandit", Law.BountyOn(_me.kingdom, _me) == num2);
				bool flag2 = num3 >= 0 && Gear.PutOn(_me, num3, base.Note);
				Item item2 = WeaponTraits.Weapon(_me);
				Check("put on, it still burns", flag2 && item2 != null && item2.getAsset().id == "sword_iron" && item2.hasMod("flame"));
			}
		}

		private IEnumerator YourKill()
		{
			Empty(_me);
			Check("the kill hand-out taken over", Loot.KillInstalled);
			Vector2 ahead = _me.current_position + new Vector2(2.5f, 0f);
			Actor bandit = CivilTest.SpawnNear("bandit", ahead);
			Actor poor = CivilTest.SpawnNear("bandit", _me.current_position + new Vector2(2.5f, 0.8f));
			if (!Check("two bandits", bandit != null && poor != null))
			{
				yield break;
			}
			Actor[] array = new Actor[2] { bandit, poor };
			foreach (Actor actor in array)
			{
				try
				{
					((BaseSimObject)actor).addStatusEffect("invincible", 30f, pColorEffect: false);
					actor.makeWait(30f);
					actor.data.loot = 0;
					if (actor.money > 0)
					{
						actor.spendMoney(actor.money);
					}
					foreach (KeyValuePair<string, int> item in Bag.Contents(actor))
					{
						if (!Supplies.Is(item.Key))
						{
							actor.takeFromInventory(item.Key, item.Value);
						}
					}
					string[] array2 = new string[2] { "tw_item_bandage", "tw_item_salve" };
					foreach (string id in array2)
					{
						Supplies.Take(actor, id, Supplies.Count(actor, id));
					}
				}
				catch (Exception ex)
				{
					Note("(setting up a bandit: " + ex.Message + ")");
				}
			}
			try
			{
				bandit.addMoney(30);
				bandit.addToInventory("wheat", 3);
			}
			catch
			{
			}
			try
			{
				foreach (ActorEquipmentSlot item2 in poor.equipment)
				{
					if (!item2.isEmpty())
					{
						item2.takeAwayItem();
					}
				}
				poor.setStatsDirty();
			}
			catch
			{
			}
			Check("each carries his torch", Supplies.Count(bandit, "tw_item_torch") == 1 && Supplies.Count(poor, "tw_item_torch") == 1);
			for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
			{
				Face(bandit);
				bandit.current_position = ahead;
				poor.current_position = _me.current_position + new Vector2(2.5f, 0.8f);
				yield return null;
			}
			array = new Actor[2] { bandit, poor };
			foreach (Actor actor2 in array)
			{
				try
				{
					actor2.finishStatusEffect("invincible");
					((BaseSimObject)actor2).updateStats();
				}
				catch
				{
				}
			}
			for (int k = 0; k < 30; k++)
			{
				if (!bandit.is_invincible && !poor.is_invincible)
				{
					break;
				}
				Face(bandit);
				yield return null;
			}
			Loot.WatchForTest(bandit);
			Loot.WatchForTest(poor);
			int loot = _me.loot;
			int wheat = Civil.BagCount(_me, "wheat");
			int kept = Loot.KeptOnBody;
			long id2 = bandit.data.id;
			long id3 = poor.data.id;
			KillBy(bandit, _me);
			KillBy(poor, _me);
			Loot.Fallen body = null;
			for (int k = 0; k < 30; k++)
			{
				if (body != null)
				{
					break;
				}
				Face(bandit);
				yield return null;
				body = Loot.Bodies.Find((Loot.Fallen f) => f.Id == id2);
			}
			for (int k = 0; k < 5; k++)
			{
				yield return null;
			}
			Check("both fell to your blows", !bandit.isAlive() && !poor.isAlive(), $"invincible {bandit.is_invincible} {poor.is_invincible}, alive {bandit.isAlive()} {poor.isAlive()}");
			Note(string.Format("your loot {0} -> {1}, wheat {2} -> {3}, kept on bodies {4} -> {5}", loot, _me.loot, wheat, Civil.BagCount(_me, "wheat"), kept, Loot.KeptOnBody));
			if (Check("his body has his coins and wheat", body != null && body.Money == 30 && body.Bag.Exists((KeyValuePair<string, int> kv) => kv.Key == "wheat" && kv.Value == 3), (body == null) ? "no body" : string.Format("{0} gold, {1}", body.Money, string.Join(", ", body.Bag.ConvertAll((KeyValuePair<string, int> kv) => kv.Value + " " + kv.Key).ToArray()))))
			{
				Check("...not your loot and bag", _me.loot == loot && Civil.BagCount(_me, "wheat") == wheat);
				Check("...nor his torch", !body.Bag.Exists((KeyValuePair<string, int> kv) => kv.Key == "tw_item_torch"));
				Check("...a kill of yours: it lies 30 s", body.ByYou && body.Lie >= 30f, $"by you {body.ByYou}, lies {body.Lie:F0} s");
				Loot.Fallen fallen = Loot.Bodies.Find((Loot.Fallen f) => f.Id == id3);
				Check("the one with only his torch leaves nothing to search", fallen == null, (fallen == null) ? null : string.Format("{0} pieces ({1}), {2} gold, {3}", fallen.Pieces.Count, string.Join(", ", fallen.Pieces.ConvertAll((Gear.Piece p) => p.Id).ToArray()), fallen.Money, string.Join(", ", fallen.Bag.ConvertAll((KeyValuePair<string, int> kv) => kv.Value + " " + kv.Key).ToArray())));
				int num = CivilTest.Money(_me);
				Loot.Take(_me, body, base.Note);
				Check("taken: 30 gold into your purse, 3 wheat", CivilTest.Money(_me) == num + 30 && Civil.BagCount(_me, "wheat") == wheat + 3, string.Format("purse {0} -> {1}, wheat {2}", num, CivilTest.Money(_me), Civil.BagCount(_me, "wheat")));
			}
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
				Log.Error("BAG kill: " + ex.Message);
			}
		}

		private IEnumerator RobTownsman(Actor townsman)
		{
			Empty(_me);
			try
			{
				townsman.finishStatusEffect("invincible");
				townsman.makeWait(30f);
			}
			catch
			{
			}
			Equip(townsman, "sword_bronze");
			long id = WeaponTraits.Weapon(townsman)?.data.id ?? (-1);
			try
			{
				if (townsman.is_inside_building)
				{
					townsman.exitBuilding();
				}
			}
			catch
			{
			}
			Vector2 at = _me.current_position + new Vector2(2.5f, 0f);
			for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
			{
				townsman.current_position = at;
				Face(townsman);
				yield return null;
			}
			Note($"the townsman: inside {townsman.is_inside_building}, {(townsman.current_position - _me.current_position).magnitude:F1} tiles off");
			Loot.WatchForTest(townsman);
			CivilTest.Kill(townsman);
			Loot.Fallen fallen = null;
			for (int i = 0; i < 30; i++)
			{
				if (fallen != null)
				{
					break;
				}
				yield return null;
				fallen = Loot.Bodies.Find((Loot.Fallen f) => f.Name == Dialogue.NameOf(townsman));
			}
			if (Check("the townsman's body", fallen != null))
			{
				City city = _me.city;
				bool flag = false;
				try
				{
					flag = city.getEquipmentList(EquipmentType.Weapon).Contains(id);
				}
				catch
				{
				}
				Note("his sword " + (flag ? "went to" : "is not in") + " the armory");
				Kingdom kingdom = _me.kingdom;
				int num = Law.BountyOn(kingdom, _me);
				Loot.Take(_me, fallen, base.Note);
				Check("robbing him where his people see is a theft", Law.LastCrime == "theft" && Law.LastWitnesses > 0 && Law.BountyOn(kingdom, _me) > num, $"{Law.LastWitnesses} saw, bounty {num} -> {Law.BountyOn(kingdom, _me)}");
				bool flag2 = false;
				try
				{
					flag2 = city.getEquipmentList(EquipmentType.Weapon).Contains(id);
				}
				catch
				{
				}
				Check("his sword is yours, not the armory's too", Gear.Of(_me).Exists((Gear.Piece x) => x.Id == "sword_bronze") && !flag2);
				Law.SetBounty(kingdom, _me, 0);
			}
		}

		private IEnumerator FoundAlone(Actor king)
		{
			Empty(_me);
			Check("left the town: a nomad", Belong.Leave(_me) && _me.city == null);
			WorldTile worldTile = king.city?.getTile();
			WorldTile worldTile2 = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				if (worldTile == null || !(new Vector2(item.x - worldTile.x, item.y - worldTile.y).magnitude < 55f))
				{
					worldTile2 = item;
					break;
				}
			}
			if (!Check("free land far off", worldTile2 != null))
			{
				yield break;
			}
			_me.current_position = new Vector2((float)worldTile2.x + 0.5f, (float)worldTile2.y + 0.5f);
			_me.dirty_current_tile = true;
			yield return CivilTest.Wait(1f);
			int count = Founding.With(_me).Count;
			Note(string.Format("{0} with you; the cost: {1}", count, Founding.Cost(_me) ?? "nothing"));
			if (count >= 3)
			{
				Note("(enough followers: the supplies are waived)");
			}
			else
			{
				Check("alone, no supplies: no town", FamilyFollow.SettleHere(_me, base.Note) == null && _me.city == null);
			}
			Bag.Put(_me, "wood", 10, spill: false);
			Bag.Put(_me, "berries", 10, spill: false);
			int num = Run.Current?.Founded ?? (-1);
			City c = FamilyFollow.SettleHere(_me, base.Note);
			Check("with 10 wood and 10 food: a town, and you its king", c != null && _me.city == c && _me.isKing(), c?.name);
			if (c == null)
			{
				yield break;
			}
			Check("...the supplies taken from your bag", count >= 3 || (Civil.BagCount(_me, "wood") == 0 && Civil.BagCount(_me, "berries") == 0));
			Check("...a founding for the run", (Run.Current?.Founded ?? (-1)) == num + 1, $"{num} -> {Run.Current?.Founded}");
			for (float t = 0f; t < 60f; t += 1f)
			{
				if (Economy.HasStores(c))
				{
					break;
				}
				yield return CivilTest.Wait(1f);
			}
			if (!Economy.HasStores(c))
			{
				Note("(no stockpile yet after a minute: the supplies wait for it)");
				yield break;
			}
			yield return CivilTest.Wait(4f);
			Check("its first stockpile has your supplies", c.getResourcesAmount("wood") >= 10, string.Format("wood {0}, berries {1}", c.getResourcesAmount("wood"), c.getResourcesAmount("berries")));
		}

		private static void Empty(Actor a)
		{
			foreach (KeyValuePair<string, int> item in Bag.Contents(a))
			{
				try
				{
					a.takeFromInventory(item.Key, item.Value);
				}
				catch
				{
				}
			}
			Supplies.Clear(a);
			while (Gear.Count(a) > 0)
			{
				Gear.Remove(a, 0);
			}
			Gear.Strip(a, EquipmentType.Weapon, null);
		}

		private void Face(Actor npc)
		{
			Vector2 vector = npc.current_position - _me.current_position;
			if (vector.sqrMagnitude > 0.0001f)
			{
				Mod.SetYaw(Mathf.Atan2(vector.y, vector.x));
			}
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
				Item item = World.world.items.generateItem(AssetManager.items.get(id), unit.kingdom, unit.getName(), 1, unit, 0, pByPlayer: true);
				Crafting.Ordinary(item);
				slot.setItem(item, unit);
				unit.setStatsDirty();
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("BAG equip " + id + ": " + ex.Message);
				return false;
			}
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
