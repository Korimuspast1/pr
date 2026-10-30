using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal sealed class TaxesTest : CivilTest
	{
		private Actor _you;

		private City _city;

		private Kingdom _realm;

		protected override string Prefix => "TAXES";

		protected override IEnumerator Body()
		{
			_you = Village("human", 9, 0);
			if (Check("a village", _you != null && _you.city != null))
			{
				_city = _you.city;
				_realm = _you.kingdom;
				try
				{
					((BaseSimObject)_you).addStatusEffect("invincible", 1200f, pColorEffect: false);
				}
				catch
				{
				}
				Stockpile();
				yield return Possess(_you);
				if (Check("in first person", InControl(_you)))
				{
					Check("you rule the realm", _realm.king == _you, "leader of the town " + WorldBoxMod.Describe(_city.leader));
					yield return Part("king's taxes", KingsTaxes());
					yield return Part("rates", Rates());
					yield return Part("leader under a king", LeaderUnderKing());
					yield return Part("untaxed", Untaxed());
					yield return Part("season payday", SeasonPayday());
				}
			}
		}

		private Actor Citizen()
		{
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != _you && item.city == _city && item.isAdult())
				{
					return item;
				}
			}
			return null;
		}

		private static void GiveTax(Actor a)
		{
			new BehActorGiveTax().execute(a);
		}

		private IEnumerator KingsTaxes()
		{
			Taxes.SetStep(_realm, tribute: false, 2, null);
			Taxes.SetStep(_realm, tribute: true, 2, null);
			Taxes.Payday(_you, Season.Spring, null);
			Actor actor = _city.leader;
			if (actor == null || actor == _you)
			{
				actor = Citizen();
				try
				{
					_city.setLeader(actor, pNew: false);
				}
				catch (Exception ex)
				{
					Note("leader: " + ex.Message);
				}
			}
			if (!Check("the town has a leader under you", actor != null && _city.leader == actor && actor != _you))
			{
				yield break;
			}
			Actor actor2 = Citizen();
			if (actor2 == actor)
			{
				foreach (Actor item in Founded)
				{
					if (item != null && item.isAlive() && item != _you && item != actor && item.city == _city && item.isAdult())
					{
						actor2 = item;
						break;
					}
				}
			}
			int num = CivilTest.Money(actor2);
			int loot = actor.loot;
			actor2.addLoot(100);
			GiveTax(actor2);
			Check("a citizen paid half into the leader's loot", actor.loot - loot == 50, $"+{actor.loot - loot}");
			Check("...and kept half as money", CivilTest.Money(actor2) - num == 50, $"+{CivilTest.Money(actor2) - num}");
			int num2 = Gold();
			GiveTax(actor);
			Check("the leader's tribute: half to your loot", Taxes.Due(_you) == 25, $"due {Taxes.Due(_you)}");
			Check("...the town keeps its share", Gold() - num2 >= 12, $"+{Gold() - num2}");
			int num3 = CivilTest.Money(_you);
			int num4 = Taxes.Payday(_you, Season.Summer, Hub.Toast);
			Check("payday: the king keeps it all", num4 == 25 && CivilTest.Money(_you) - num3 == 25, $"got {num4}, purse +{CivilTest.Money(_you) - num3}");
			Check("...and the loot is settled", Taxes.Due(_you) == 0, $"due {Taxes.Due(_you)}");
			yield return null;
		}

		private IEnumerator Rates()
		{
			float[] array = new float[5] { 0f, 0.2f, 0.5f, 0.7f, 0.9f };
			for (int i = 0; i < 5; i++)
			{
				Taxes.SetStep(_realm, tribute: false, i, null);
				Check($"{Taxes.StepNames[i]} sets the rate {array[i]}", Mathf.Abs(_realm.getTaxRateLocal() - array[i]) < 0.001f, $"{_realm.getTaxRateLocal()}");
			}
			try
			{
				((MetaObjectWithTraits<KingdomData, KingdomTrait>)_realm).recalcBaseStats();
			}
			catch (Exception ex)
			{
				Note("recalc: " + ex.Message);
			}
			yield return CivilTest.Wait(1.2f);
			Check("Harsh stays after the game recalculates", Mathf.Abs(_realm.getTaxRateLocal() - 0.9f) < 0.001f, $"{_realm.getTaxRateLocal()}");
			Check("the payment is ours (mood by step)", PaidTaxReplaced());
			Actor c = Citizen();
			if (c != null)
			{
				int num = Mood(1);
				int num2 = Mood(4);
				Check("Harsh costs more mood than Low", num2 < num, $"low {num}, harsh {num2}");
				Taxes.SetStep(_realm, tribute: false, 2, null);
				Taxes.Payday(_you, Season.Summer, null);
			}
			int Mood(int step)
			{
				Taxes.SetStep(_realm, tribute: false, step, null);
				int happiness = c.getHappiness();
				c.addLoot(10);
				GiveTax(c);
				return c.getHappiness() - happiness;
			}
		}

		private static bool PaidTaxReplaced()
		{
			try
			{
				return (bool)typeof(Taxes).GetField("_installed", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
			}
			catch
			{
				return false;
			}
		}

		private IEnumerator LeaderUnderKing()
		{
			Actor actor = null;
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != _you && item.isAdult() && item.city == _city && item != Citizen())
				{
					actor = item;
					break;
				}
			}
			if (!Check("someone to crown", actor != null))
			{
				yield break;
			}
			try
			{
				_realm.setKing(actor);
			}
			catch (Exception ex)
			{
				Note("crown: " + ex.Message);
			}
			try
			{
				if (_city.leader != _you)
				{
					_city.setLeader(_you, pNew: false);
				}
			}
			catch
			{
			}
			Check("you lead under another king", _realm.king == actor && _city.leader == _you && !_you.isKing());
			Taxes.SetStep(_realm, tribute: true, 2, null);
			_you.addLoot(100);
			int loot = actor.loot;
			int num = CivilTest.Money(_you);
			int num2 = Gold();
			int num3 = Taxes.Payday(_you, Season.Autumn, Hub.Toast);
			Check("the king's share went up (half)", actor.loot - loot == 50, $"+{actor.loot - loot}");
			Check("half the rest to the stockpile", Gold() - num2 == 25, $"+{Gold() - num2}");
			Check("half the rest to your purse", num3 == 25 && CivilTest.Money(_you) - num == 25, $"got {num3}");
			try
			{
				_realm.setKing(_you);
			}
			catch
			{
			}
			yield return null;
		}

		private IEnumerator Untaxed()
		{
			Actor actor = null;
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != _you && item.isAdult() && item.city == _city)
				{
					actor = item;
				}
			}
			if (actor != null)
			{
				try
				{
					_city.setLeader(actor, pNew: false);
					_realm.setKing(actor);
				}
				catch (Exception ex)
				{
					Note("step down: " + ex.Message);
				}
				Check("you rule nothing", !_you.isKing() && !_you.isCityLeader());
				_you.addLoot(40);
				int num = CivilTest.Money(_you);
				int num2 = Gold();
				int num3 = Taxes.Payday(_you, Season.Winter, Hub.Toast);
				Check("your own earnings come whole", num3 == 40 && CivilTest.Money(_you) - num == 40, $"got {num3}");
				Check("...none to the town", Gold() == num2);
				try
				{
					_realm.setKing(_you);
					_city.setLeader(_you, pNew: false);
				}
				catch
				{
				}
				yield return null;
			}
		}

		private IEnumerator SeasonPayday()
		{
			_you.addLoot(30);
			int purse = CivilTest.Money(_you);
			bool flag = false;
			for (int i = 0; i < 12; i++)
			{
				if (flag)
				{
					break;
				}
				int day = Calendar.SkipToDawn();
				yield return null;
				Calendar.SeasonOf(day, out var dayIn, out var _);
				flag = CivilTest.Money(_you) - purse >= 30;
				if (dayIn == 0)
				{
					Check("the season's first dawn is payday", flag, $"day {day}, purse +{CivilTest.Money(_you) - purse}");
					break;
				}
				if (flag)
				{
					Check("no payday mid-season", ok: false, $"paid on day {day} ({dayIn} into it)");
				}
			}
			if (!flag)
			{
				Check("a payday within a year", ok: false);
			}
		}

		private int Gold()
		{
			try
			{
				return _city.getResourcesAmount("gold");
			}
			catch
			{
				return -1;
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
						break;
					}
				}
			}
			catch (Exception ex)
			{
				Note("stockpile: " + ex.Message);
			}
		}
	}
}
