using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal sealed class VillageTest : CivilTest
	{
		private Actor _you;

		private City _town;

		private readonly List<Actor> _folk = new List<Actor>();

		protected override string Prefix => "VILLAGE";

		protected override IEnumerator Body()
		{
			WorldTile stage = CivilTest.Stage();
			if (!Check("open land", stage != null))
			{
				yield break;
			}
			CivilTest.ClearBox(stage.x - 26, stage.y - 26, stage.x + 26, stage.y + 26);
			_you = CivilTest.SpawnNear("human", new Vector2((float)stage.x + 0.5f, (float)stage.y - 6.5f));
			if (!Check("a nomad", _you != null && _you.city == null))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)_you).addStatusEffect("invincible", 1200f, pColorEffect: false);
			}
			catch
			{
			}
			yield return Possess(_you);
			if (!Check("in first person", InControl(_you)))
			{
				yield break;
			}
			_town = FirstPerson.Towns.Village.Found(_you, stage, out var why);
			if (!Check("a village founded", _town != null, why))
			{
				yield break;
			}
			yield return CivilTest.Wait(1f);
			Check("held: planned by you alone", FirstPerson.Towns.Village.PlansAlone(_town));
			foreach (Building item in Blueprints.SitesOf(_town))
			{
				Finish(item);
			}
			yield return Part("growth", GrowthPart());
			yield return Part("carried", Carried());
			yield return Part("give and order", GiveAndOrder());
			yield return Part("taken town", TakenTown());
			yield return Part("other peoples", OtherPeoples());
			yield return Part("after a load", Reload());
		}

		private IEnumerator Reload()
		{
			long town = _town.data.id;
			Check("held before the save", FirstPerson.Towns.Village.PlansAlone(_town));
			byte[] pMapData;
			try
			{
				pMapData = SaveManager.currentWorldToSavedMap().toZip();
			}
			catch (Exception ex)
			{
				Check("the world saved to bytes", ok: false, ex.Message);
				yield break;
			}
			try
			{
				SaveManager.loadMapFromBytes(pMapData);
			}
			catch (Exception ex2)
			{
				Check("the world loaded from bytes", ok: false, ex2.Message);
				yield break;
			}
			for (float t = 0f; t < 60f; t += 0.5f)
			{
				yield return CivilTest.Wait(0.5f);
				bool flag = true;
				try
				{
					flag = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag && Config.game_loaded && t > 2f)
				{
					break;
				}
			}
			yield return CivilTest.Wait(1.5f);
			City city = FirstPerson.Towns.Village.CityById(town);
			if (Check("the village is back after the load", city != null && FirstPerson.Towns.Village.IsVillage(city)))
			{
				Check("...held again at once, its founder not taken yet", FirstPerson.Towns.Village.PlansAlone(city), $"timer {city.timer_build:F1}");
			}
		}

		private IEnumerator OtherPeoples()
		{
			WorldTile mid = _town.getTile();
			int n = 0;
			int may = 0;
			int founded = 0;
			string[] array = new string[10] { "civ_cat", "civ_dog", "civ_fox", "civ_wolf", "civ_bear", "civ_rat", "civ_crab", "civ_frog", "civ_crystal_golem", "civ_candy_man" };
			foreach (string id in array)
			{
				if (AssetManager.actor_library.get(id) == null)
				{
					continue;
				}
				WorldTile site = null;
				foreach (WorldTile item in KingTest.GoodSites())
				{
					if (mid == null || Mathf.Abs(item.x - mid.x) + Mathf.Abs(item.y - mid.y) > 50)
					{
						site = item;
						break;
					}
				}
				if (site == null)
				{
					Note("no site away from towns");
					yield break;
				}
				Actor a = CivilTest.SpawnNear(id, new Vector2((float)site.x + 0.5f, (float)site.y + 0.5f));
				if (a == null)
				{
					Note(id + ": couldn't be spawned");
					continue;
				}
				yield return null;
				n++;
				string text = (FirstPerson.Towns.Village.CanFound(a) ? FirstPerson.Towns.Village.WhyNotFoundAt(a, site) : "can't found");
				Note(string.Format("{0}: civ {1}, kingdom {2}, adult {3}, town {4}: {5}", id, a.asset.civ, a.asset.kingdom_id_civilization, a.isAdult(), a.city?.name ?? "none", text ?? "may found here"));
				if (text == null)
				{
					may++;
				}
				if (text == null && founded == 0)
				{
					string why;
					City city = FirstPerson.Towns.Village.Found(a, site, out why);
					if (city != null)
					{
						founded++;
						Check(id + " founds a village and is its king", a.city == city && a.kingdom != null && a.kingdom.king == a && Royal.IsKing(a), why);
					}
				}
				CivilTest.Kill(a);
			}
			Check("the other peoples may found villages", n > 0 && may == n, $"{may} of {n}");
		}

		private void Finish(Building b)
		{
			Blueprints.Pay(_town, b, _you, out var _);
			for (int i = 0; i < 400; i++)
			{
				if (!b.isAlive())
				{
					break;
				}
				if (!b.isUnderConstruction())
				{
					break;
				}
				Blueprints.Blow(_you, b, 1.5f);
			}
		}

		private Building Plan(string order)
		{
			BuildingAsset buildingAsset = PlanPieces.AssetOf(PlanPieces.OfOrder(order), _town);
			WorldTile worldTile = ((buildingAsset != null) ? Blueprints.NearestFree(_town, buildingAsset) : null);
			if (worldTile == null)
			{
				return null;
			}
			return Blueprints.PlaceSite(_town, buildingAsset, worldTile, _you, home: false);
		}

		private IEnumerator GrowthPart()
		{
			Building building = Plan("order_stockpile");
			if (building != null)
			{
				Finish(building);
			}
			if (Check("a stockpile", _town.hasStorages()))
			{
				yield return CivilTest.Wait(1.5f);
				int p0 = Growth.PointsOf(_town);
				_town.addResourcesToRandomStockpile("wood", 30);
				yield return CivilTest.Wait(2.5f);
				Check("30 wood brought in: 30 points", Growth.PointsOf(_town) - p0 == 30, $"{Growth.PointsOf(_town) - p0}");
				Check("...level 2", PlanPieces.LevelOf(_town) == 2, $"level {PlanPieces.LevelOf(_town)}");
				Check("the windmill opens at level 2", PlanPieces.WhyNot(PlanPieces.OfOrder("order_windmill_0"), _town) == null, PlanPieces.WhyNot(PlanPieces.OfOrder("order_windmill_0"), _town));
				int p1 = Growth.PointsOf(_town);
				Growth.Ignore(_town, "stone", 10);
				_town.addResourcesToRandomStockpile("stone", 10);
				yield return CivilTest.Wait(2.5f);
				Check("a refund doesn't count", Growth.PointsOf(_town) == p1, $"{Growth.PointsOf(_town) - p1}");
				Check("spending doesn't lower it", ok: true);
				_town.takeResource("wood", 10);
				yield return CivilTest.Wait(1.5f);
				Check("...points kept after spending", Growth.PointsOf(_town) == p1, $"{Growth.PointsOf(_town)}");
			}
		}

		private IEnumerator Carried()
		{
			for (int i = 0; i < 4; i++)
			{
				Actor actor = CivilTest.SpawnNear("human", (Vector2)_town.getTile().posV + new Vector2(i - 2, 3f));
				if (actor != null)
				{
					try
					{
						actor.joinCity(_town);
					}
					catch
					{
					}
					_folk.Add(actor);
				}
			}
			Check("newcomers", _folk.Count >= 3, _folk.Count.ToString());
			_town.addResourcesToRandomStockpile("wood", 20);
			_town.addResourcesToRandomStockpile("stone", 20);
			int wood = _town.getResourcesAmount("wood");
			int stone = _town.getResourcesAmount("stone");
			Building hall = Plan("order_hall_0");
			if (!Check("a hall planned", hall?.isUnderConstruction() ?? false))
			{
				yield break;
			}
			Check("...not paid at once", !Blueprints.IsPaid(hall));
			int d0 = Haul.Delivered;
			for (int j = 0; j < 180; j++)
			{
				if (Blueprints.IsPaid(hall))
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			Check("carriers brought loads", Haul.Delivered > d0, $"{Haul.Delivered - d0} loads");
			Check("...and the hall is paid when it's all there", Blueprints.IsPaid(hall), Blueprints.StateOf(hall));
			Check("...out of the stockpile (its stone exactly; wood also goes to the town's own tents)", _town.getResourcesAmount("wood") <= wood - hall.asset.cost.wood && _town.getResourcesAmount("stone") == stone - hall.asset.cost.stone, string.Format("wood {0} -> {1}, stone {2} -> {3}", wood, _town.getResourcesAmount("wood"), stone, _town.getResourcesAmount("stone")));
			Finish(hall);
		}

		private IEnumerator GiveAndOrder()
		{
			Building building = Plan("order_tent");
			if (!Check("a home planned", building?.isUnderConstruction() ?? false))
			{
				yield break;
			}
			int num = (_town.hasStorages() ? _town.getResourcesAmount("wood") : 0);
			if (num > 0)
			{
				_town.takeResource("wood", num);
			}
			Bag.Put(_you, "wood", building.asset.cost.wood, spill: true);
			_you.addMoney(building.asset.cost.gold + 5);
			int num2 = Haul.Give(_you, building);
			Check("E: give stocks it from your bag", num2 >= building.asset.cost.wood && Blueprints.IsPaid(building), $"gave {num2}, {Blueprints.StateOf(building)}");
			Building building2 = Plan("order_tent");
			if (Check("a home planned to pay half and half", building2 != null && building2.isUnderConstruction() && !Blueprints.IsPaid(building2)))
			{
				int wood = building2.asset.cost.wood;
				int num3 = wood / 2;
				int num4 = wood - num3;
				int num5 = (_town.hasStorages() ? _town.getResourcesAmount("wood") : 0);
				if (num5 > 0)
				{
					_town.takeResource("wood", num5);
				}
				int num6 = Civil.BagCount(_you, "wood");
				if (num6 > 0)
				{
					Civil.TakeFromBag(_you, "wood", num6);
				}
				Bag.Put(_you, "wood", num3, spill: true);
				Growth.Ignore(_town, "wood", num4);
				_town.addResourcesToRandomStockpile("wood", num4);
				_you.addMoney(building2.asset.cost.gold + 5);
				string text = Blueprints.CostAgainst(building2, _you);
				Check("...its prompt counts your bag and the stores", text?.Contains("wood " + wood + "/" + wood) ?? false, text);
				string missing;
				bool flag = Blueprints.Pay(_town, building2, _you, out missing);
				Check("...a blow pays it from your bag, the stores the rest", flag && Civil.BagCount(_you, "wood") == 0 && _town.getResourcesAmount("wood") == 0, string.Format("paid {0} ({1}), bag {2}, stores {3}", flag, missing, Civil.BagCount(_you, "wood"), _town.getResourcesAmount("wood")));
				_town.addResourcesToRandomStockpile("wood", 40);
				Building a = Plan("order_tent");
				Building b = Plan("order_tent");
				if (Check("two more plans", a != null && b != null))
				{
					Check("T: build next puts the second on top", Blueprints.BuildNext(b) && Blueprints.SitesOf(_town)[0] == b);
					yield return CivilTest.Wait(1f);
					Check("...the builders' site is it", _town.under_construction_building == b || !Blueprints.IsPaid(b), _town.under_construction_building?.asset?.id);
					int resourcesAmount = _town.getResourcesAmount("wood");
					Haul.Add(a, 0, 1);
					bool flag2 = Blueprints.IsPaid(a);
					Blueprints.Remove(a, _you);
					int num7 = _town.getResourcesAmount("wood") - resourcesAmount;
					Check("a plan taken back returns what had arrived", flag2 ? (num7 == a.asset.cost.wood) : (num7 == 1), $"back {num7}, paid {flag2}");
				}
			}
		}

		private IEnumerator TakenTown()
		{
			Actor actor = Village("human", 6, 0, _town.getTile(), 60f, _town.getTile());
			if (!Check("another town", actor != null && actor.city != null))
			{
				yield break;
			}
			City c = actor.city;
			Actor body = null;
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != actor && item.isAdult())
				{
					body = item;
					break;
				}
			}
			if (!Check("someone of it to play", body != null))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)body).addStatusEffect("invincible", 1200f, pColorEffect: false);
				c.setLeader(body, pNew: false);
			}
			catch (Exception ex)
			{
				Note("leader: " + ex.Message);
			}
			yield return Possess(body);
			yield return CivilTest.Wait(1.5f);
			Check("you lead it", c.leader == body && InControl(body), WorldBoxMod.Describe(c.leader));
			Check("a town you took over keeps building by itself", !FirstPerson.Towns.Village.PlansAlone(c) && c.timer_build < 20f, $"timer {c.timer_build:F1}");
			GiveStockpile(c);
			Check("its stockpile", c.hasStorages(), c.buildings.Count + " buildings, " + c.zones.Count + " zones");
			c.addResourcesToRandomStockpile("wood", 30);
			c.addResourcesToRandomStockpile("stone", 30);
			BuildingAsset a = PlanPieces.AssetOf(PlanPieces.OfOrder("order_tent"), c);
			WorldTile worldTile = Blueprints.NearestFree(c, a);
			Building building = ((worldTile != null) ? Blueprints.PlaceSite(c, a, worldTile, body, home: false) : null);
			if (Check("a plan placed there", building != null))
			{
				if (building.isUnderConstruction())
				{
					Check("...it's the town's site at once (your plans first)", c.under_construction_building == building, c.under_construction_building?.asset?.id);
				}
				else
				{
					Check("...it stands at once (no building phase)", ok: true);
				}
				yield return null;
			}
		}
	}
}
