using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class EconomyTest : CivilTest
	{
		private Actor _king;

		private Actor _host;

		private City _city;

		private readonly List<Actor> _people = new List<Actor>();

		protected override string Prefix => "ECON";

		protected override IEnumerator Body()
		{
			_king = Village("human", 18, 2);
			if (!Check("a village", _king != null))
			{
				yield break;
			}
			_city = _king.city;
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != _king && item.isAdult())
				{
					_people.Add(item);
				}
			}
			foreach (Actor person in _people)
			{
				if (!person.isWarrior())
				{
					_host = person;
					break;
				}
			}
			if (_host == null)
			{
				_host = _people[0];
			}
			try
			{
				_host.addMoney(900);
				((BaseSimObject)_host).addStatusEffect("invincible", 400f, pColorEffect: false);
			}
			catch (Exception ex)
			{
				Note("setup: " + ex.Message);
			}
			Stockpile();
			yield return Possess(_host);
			if (Check("first person on a villager", InControl(_host)))
			{
				try
				{
					_city.addResourcesToRandomStockpile("wheat", 40);
					_city.addResourcesToRandomStockpile("bread", 30);
					_city.addResourcesToRandomStockpile("wood", 40);
					_city.addResourcesToRandomStockpile("gold", 60);
				}
				catch (Exception ex2)
				{
					Note("stores: " + ex2.Message);
				}
				Note(string.Format("stores: bread {0}, wood {1}, treasury {2}", _city.getResourcesAmount("bread"), _city.getResourcesAmount("wood"), Economy.Treasury(_city)));
				yield return Part("trade", Trade());
				yield return Part("arms and a bed", ArmsAndBed());
				yield return Part("a sellsword", Sellsword());
				yield return Part("the kingdom's protection", Kingdom());
				yield return Part("a bribe", Bribe());
				yield return Part("a bought war", War());
			}
		}

		private IEnumerator War()
		{
			WorldTile tile = _city.getTile();
			Actor king = Village("human", 14, 0, tile, 25f);
			if (!Check("a second realm", king != null && king.kingdom != _city.kingdom))
			{
				yield break;
			}
			new List<Actor>(Founded);
			Actor actor = Village("human", 8, 0, king.city?.getTile() ?? king.current_tile, 25f, tile) ?? Village("human", 8, 0, tile, 25f, king.city?.getTile() ?? king.current_tile);
			if (!Check("a third realm", actor != null && actor.kingdom != king.kingdom && actor.kingdom != _city.kingdom))
			{
				yield break;
			}
			Kingdom k = king.kingdom;
			Kingdom target = actor.kingdom;
			try
			{
				king.removeTrait("pacifist");
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.6f);
			Conversation c = Mod.Converse;
			bool[] ok = new bool[1];
			int had = CivilTest.Money(_host);
			try
			{
				_host.spendMoney(had);
				_host.addMoney(100);
			}
			catch
			{
			}
			yield return Talk(king);
			Check("a foreign king hears an offer of war", Find(c, "I'd pay you to make war") != null, Labels(c));
			yield return Say(c, "I'd pay you to make war", ok);
			Note("wars for sale: " + Labels(c));
			Choice choice = Find(c, "March on " + Royal.NameOf(target));
			string why;
			int price = Dialogue.WarPrice(king, target, c.Voice, out why);
			Check("it costs a fortune", price >= 800, $"{price} gold ({why})");
			Check("out of reach with 100 gold", choice != null && !choice.Enabled, choice?.Note);
			try
			{
				_host.addMoney(price + 500);
			}
			catch
			{
			}
			Mod.EndConversation();
			yield return Talk(king);
			yield return Say(c, "I'd pay you to make war", ok);
			int money = CivilTest.Money(_host);
			int treasury = Economy.Treasury(k.capital);
			yield return Say(c, "March on " + Royal.NameOf(target), ok);
			Check("the war is bought", ok[0] && Royal.AtWar(k, target), Royal.NameOf(k) + " vs " + Royal.NameOf(target));
			Check("for the asking price", money - CivilTest.Money(_host) == price, $"{money} -> {CivilTest.Money(_host)}");
			Check("half to the capital's treasury", k.capital == null || Economy.Treasury(k.capital) > treasury, $"{treasury} -> {Economy.Treasury(k.capital)}");
			Mod.EndConversation();
			yield return Talk(king);
			yield return Say(c, "I'd pay you to make war", ok);
			Check("not twice in a row", Find(c, "March on") == null, c.Line);
			Mod.EndConversation();
			try
			{
				_host.addMoney(had);
			}
			catch
			{
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
						Note(string.Format("stockpile at {0},{1} (city {2})", tile2.x, tile2.y, (building.city != null) ? building.city.name : "none"));
						break;
					}
				}
			}
			catch (Exception ex)
			{
				Note("stockpile: " + ex.Message);
			}
		}

		private static Choice Find(Conversation c, string start)
		{
			return c.Choices.Find((Choice ch) => ch.Label.StartsWith(start, StringComparison.Ordinal));
		}

		private static string Labels(Conversation c)
		{
			return string.Join(" | ", c.Choices.ConvertAll((Choice x) => x.Label + (x.Enabled ? "" : (" [off: " + x.Note + "]"))).ToArray());
		}

		private IEnumerator Say(Conversation c, string start, bool[] ok)
		{
			for (int i = 0; i < 12; i++)
			{
				if (!c.MorePages)
				{
					break;
				}
				c.Next();
				c.Next();
			}
			Choice choice = Find(c, start);
			ok[0] = choice?.Enabled ?? false;
			if (!ok[0])
			{
				Note("(no \"" + start + "\" now: " + Labels(c) + ")");
				yield break;
			}
			c.LineStart -= 999f;
			c.Pick(c.Choices.IndexOf(choice));
			yield return CivilTest.Wait(0.15f);
			Note("  [" + start + "] \"" + c.Line + "\"");
			for (int j = 0; j < 12; j++)
			{
				if (!c.MorePages)
				{
					break;
				}
				c.Next();
				c.Next();
			}
		}

		private IEnumerator Talk(Actor npc)
		{
			if (Mod.Converse.Open)
			{
				Mod.EndConversation();
			}
			CivilTest.StandBefore(npc, _host, 1.4f);
			yield return CivilTest.Wait(0.3f);
			Mod.StartConversationForTest(npc);
			yield return CivilTest.Wait(0.2f);
			Conversation converse = Mod.Converse;
			for (int i = 0; i < 12; i++)
			{
				if (!converse.MorePages)
				{
					break;
				}
				converse.Next();
				converse.Next();
			}
		}

		private Actor Someone(Func<Actor, bool> fits)
		{
			foreach (Actor person in _people)
			{
				if (person != null && person != _host && person.isAlive() && fits(person))
				{
					return person;
				}
			}
			return null;
		}

		private IEnumerator Trade()
		{
			Actor npc = Someone((Actor u) => !u.isWarrior());
			Conversation c = Mod.Converse;
			bool[] ok = new bool[1];
			yield return Talk(npc);
			Check("talking to a villager", c.Open);
			Check("\"Let's trade.\" is offered", Find(c, "Let's trade.") != null, Labels(c));
			Check("\"I need protection.\" is offered", Find(c, "I need protection.") != null);
			yield return Say(c, "Let's trade.", ok);
			yield return Say(c, "What can I buy?", ok);
			Note("for sale: " + Labels(c));
			int money = CivilTest.Money(_host);
			int treasury = Economy.Treasury(_city);
			int bread = Civil.BagCount(_host, "bread");
			int stores = _city.getResourcesAmount("bread");
			yield return Say(c, "Buy 5 Bread", ok);
			Check("bought 5 bread", ok[0] && Civil.BagCount(_host, "bread") == bread + 5 && _city.getResourcesAmount("bread") == stores - 5, string.Format("bag {0} -> {1}, stores {2} -> {3}", bread, Civil.BagCount(_host, "bread"), stores, _city.getResourcesAmount("bread")));
			int num = money - CivilTest.Money(_host);
			Check("paid from your purse into the town's treasury", num > 0 && Economy.Treasury(_city) == treasury + num, $"paid {num}, treasury {treasury} -> {Economy.Treasury(_city)}");
			yield return Shot("econ_trade.png");
			try
			{
				_host.addToInventory("wood", 10);
			}
			catch
			{
			}
			yield return Say(c, "Something else.", ok);
			yield return Say(c, "I have goods to sell.", ok);
			Note("to sell: " + Labels(c));
			money = CivilTest.Money(_host);
			treasury = Economy.Treasury(_city);
			yield return Say(c, "Sell 5 Wood", ok);
			Check("sold 5 wood", ok[0] && Civil.BagCount(_host, "wood") == 5 && CivilTest.Money(_host) > money, string.Format("bag wood {0}, +{1} gold", Civil.BagCount(_host, "wood"), CivilTest.Money(_host) - money));
			Check("paid out of the treasury", Economy.Treasury(_city) == treasury - (CivilTest.Money(_host) - money), $"{treasury} -> {Economy.Treasury(_city)}");
			money = CivilTest.Money(_host);
			yield return Say(c, "Sell everything", ok);
			Check("sold the whole bag", ok[0] && Civil.BagCount(_host, "wood") == 0 && Civil.BagCount(_host, "bread") == 0 && CivilTest.Money(_host) > money, $"+{CivilTest.Money(_host) - money}");
			try
			{
				_host.addToInventory("gold", 10);
			}
			catch
			{
			}
			yield return Say(c, "I have goods to sell.", ok);
			money = CivilTest.Money(_host);
			treasury = Economy.Treasury(_city);
			yield return Say(c, "Sell 5 Gold", ok);
			Check("gold sells one to one, the treasury even", ok[0] && CivilTest.Money(_host) == money + 5 && Economy.Treasury(_city) == treasury, $"+{CivilTest.Money(_host) - money} coins, treasury {treasury} -> {Economy.Treasury(_city)}");
			Mod.EndConversation();
		}

		private IEnumerator ArmsAndBed()
		{
			Item sword = null;
			try
			{
				EquipmentAsset pItemAsset = AssetManager.items.get("sword_iron");
				sword = World.world.items.generateItem(pItemAsset, _city.kingdom, "smith", 1, null, 0, pByPlayer: true);
				_city.tryToPutItem(sword);
			}
			catch (Exception ex)
			{
				Note("armory setup: " + ex.Message);
			}
			Note("armory: " + string.Join(", ", Economy.Armory(_city, 12).ConvertAll((Item i) => Economy.ItemName(i) + " " + Economy.ItemPrice(i, 1f)).ToArray()));
			Actor warrior = Someone((Actor u) => u.isWarrior());
			if (Check("a warrior to show the armory", warrior != null))
			{
				Conversation c = Mod.Converse;
				bool[] ok = new bool[1];
				yield return Talk(warrior);
				yield return Say(c, "Let's trade.", ok);
				yield return Say(c, "Show me your arms and armor.", ok);
				Check("the armory is shown", ok[0]);
				Note("arms: " + Labels(c));
				int money = CivilTest.Money(_host);
				Choice choice = c.Choices.Find((Choice ch) => ch.Label.Contains("sword"));
				if (choice != null)
				{
					yield return Say(c, choice.Label, ok);
				}
				Item item = null;
				try
				{
					item = _host.equipment.getSlot(EquipmentType.Weapon).getItem();
				}
				catch
				{
				}
				Check("a sword bought and worn", item != null && item.getAsset().id.Contains("sword") && CivilTest.Money(_host) < money, string.Format("{0}, {1} -> {2}", (item != null) ? item.getAsset().id : "nothing", money, CivilTest.Money(_host)));
				Check("it's gone from the armory", sword == null || !_city.getEquipmentList(EquipmentType.Weapon).Contains(sword.data.id));
				Mod.EndConversation();
				try
				{
					_host.data.health = Mathf.Max(1, _host.getMaxHealth() / 4);
				}
				catch
				{
				}
				yield return CivilTest.Wait(1.2f);
				yield return Talk(warrior);
				yield return Say(c, "Let's trade.", ok);
				Choice choice2 = Find(c, "I need a bed for the night");
				Check("no bed just after you're hurt", choice2 != null && !choice2.Enabled, choice2?.Note);
				Mod.EndConversation();
				yield return CivilTest.Wait(8.5f);
				yield return Talk(warrior);
				yield return Say(c, "Let's trade.", ok);
				yield return Say(c, "I need a bed for the night", ok);
				Check("a night's sleep heals you", ok[0] && _host.getHealth() >= _host.getMaxHealth(), $"{_host.getHealth()}/{_host.getMaxHealth()}");
				choice2 = Find(c, "I need a bed for the night");
				Check("only once a year", choice2 != null && !choice2.Enabled, choice2?.Note);
				Mod.EndConversation();
			}
		}

		private IEnumerator Sellsword()
		{
			Actor guard = Someone((Actor u) => u.isWarrior());
			Conversation c = Mod.Converse;
			bool[] ok = new bool[1];
			yield return Talk(guard);
			yield return Say(c, "I need protection.", ok);
			Note("protection: " + Labels(c));
			int money = CivilTest.Money(_host);
			yield return Say(c, "Hire " + Dialogue.NameOf(guard) + " as your guard for 5 years", ok);
			Check("hired a guard", ok[0] && Economy.HirelingOf(guard) != null && CivilTest.Money(_host) < money, $"{money} -> {CivilTest.Money(_host)}");
			guard.data.get("fp_guard", out var pResult, 0L);
			Check("the contract is in their saved data", pResult == _host.data.id);
			Mod.EndConversation();
			Vector2 current_position = _host.current_position;
			Vector2 current_position2 = current_position + new Vector2(10f, 0f);
			WorldTile tile = World.world.GetTile((int)current_position2.x, (int)current_position2.y);
			if (tile != null && tile.Type != null && tile.Type.ground && !tile.Type.block)
			{
				_host.current_position = current_position2;
			}
			else
			{
				_host.current_position = current_position - new Vector2(10f, 0f);
			}
			float before = (guard.current_position - _host.current_position).magnitude;
			yield return CivilTest.Wait(8f);
			float magnitude = (guard.current_position - _host.current_position).magnitude;
			Check("they come when you stray", magnitude < before - 2f || magnitude < 5.5f, $"{before:F1} -> {magnitude:F1} tiles");
			Actor bandit = CivilTest.SpawnNear("bandit", _host.current_position, 3f);
			if (!Check("a bandit", bandit != null))
			{
				yield break;
			}
			try
			{
				bandit.addAggro(_host);
				bandit.startFightingWith(_host);
			}
			catch
			{
			}
			bool flag = false;
			for (int i = 0; i < 20; i++)
			{
				if (flag)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
				flag = guard.has_attack_target && guard.attack_target == bandit;
			}
			Check("your guard fights whoever fights you", flag || !bandit.isAlive(), "guard's target " + ((guard.attack_target != null) ? WorldBoxMod.Describe(guard.attack_target as Actor) : "none"));
			CivilTest.Kill(bandit);
			yield return CivilTest.Wait(0.5f);
			yield return Shot("econ_purse.png");
			yield return Talk(guard);
			Check("your guard talks about the contract", Find(c, "About your contract...") != null, Labels(c));
			yield return Say(c, "About your contract...", ok);
			yield return Say(c, "You're dismissed.", ok);
			guard.data.get("fp_guard", out pResult, 0L);
			Check("dismissed", ok[0] && Economy.Hired.Count == 0 && pResult == 0);
			Mod.EndConversation();
		}

		private IEnumerator Kingdom()
		{
			Conversation c = Mod.Converse;
			bool[] ok = new bool[1];
			Kingdom k = _city.kingdom;
			yield return Talk(_king);
			yield return Say(c, "I need protection.", ok);
			Note("the king offers: " + Labels(c));
			int money = CivilTest.Money(_host);
			yield return Say(c, "Buy " + k.name + "'s protection for 10 years", ok);
			Check("the kingdom's protection bought", ok[0] && Economy.IsProtected(k, _host) && CivilTest.Money(_host) < money, $"{money} -> {CivilTest.Money(_host)}");
			Mod.EndConversation();
			yield return CivilTest.Wait(3.2f);
			Check("the purse knows you're protected", Economy.ProtectorsOf(_host).Contains(k));
			foreach (Actor person in _people)
			{
				if (person != null && person.isAlive() && person.isWarrior())
				{
					person.current_position = _host.current_position + UnityEngine.Random.insideUnitCircle * 6f;
					try
					{
						person.cancelAllBeh();
					}
					catch
					{
					}
				}
			}
			Actor wolf = CivilTest.SpawnNear("wolf", _host.current_position, 4f);
			if (!Check("a wolf", wolf != null))
			{
				yield break;
			}
			try
			{
				wolf.addAggro(_host);
				wolf.startFightingWith(_host);
			}
			catch
			{
			}
			int coming = 0;
			for (int i = 0; i < 20; i++)
			{
				if (coming != 0)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
				foreach (Actor person2 in _people)
				{
					if (person2 != null && person2.isAlive() && person2.has_attack_target && person2.attack_target == wolf)
					{
						coming++;
					}
				}
			}
			Check("the kingdom's warriors come to your aid", coming > 0 || !wolf.isAlive(), coming + " warriors on it");
			CivilTest.Kill(wolf);
		}

		private IEnumerator Bribe()
		{
			Conversation c = Mod.Converse;
			bool[] ok = new bool[1];
			Kingdom k = _city.kingdom;
			Law.SetBounty(k, _host, 100);
			yield return CivilTest.Wait(0.6f);
			Check("a crime against the kingdom ends its protection", !Economy.IsProtected(k, _host));
			Actor npc = Someone((Actor u) => u.isWarrior());
			yield return Talk(npc);
			Check("a guard can be bribed", Find(c, "Slip them some coin") != null, Labels(c));
			int money = CivilTest.Money(_host);
			yield return Say(c, "Slip them some coin", ok);
			int num = Law.BountyOn(k, _host);
			Check("the bribe is taken or refused", (num == 0 && CivilTest.Money(_host) < money) || (num == 125 && CivilTest.Money(_host) == money), $"bounty {num}, gold {money} -> {CivilTest.Money(_host)}");
			Law.SetBounty(k, _host, 0);
			Law.StandDown(k, _host);
			Mod.EndConversation();
		}

		private IEnumerator Shot(string name)
		{
			try
			{
				ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, name));
			}
			catch
			{
			}
			yield return null;
			yield return null;
		}
	}
}
