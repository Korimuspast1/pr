using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal sealed class PlanningTest : CivilTest
	{
		private Actor _you;

		private City _town;

		private WorldTile _stage;

		private Building _fire;

		private Building _stockpile;

		private Building _hall;

		private readonly List<Actor> _folk = new List<Actor>();

		private readonly List<TileZone> _claims = new List<TileZone>();

		protected override string Prefix => "PLANNING";

		private Hud H => Mod.HudForTest;

		protected override IEnumerator Body()
		{
			_stage = CivilTest.Stage();
			if (!Check("open land to settle", _stage != null))
			{
				yield break;
			}
			CivilTest.ClearBox(_stage.x - 26, _stage.y - 26, _stage.x + 26, _stage.y + 26);
			_you = CivilTest.SpawnNear("human", new Vector2((float)_stage.x + 0.5f, (float)_stage.y - 6.5f));
			if (!Check("a nomad to be", _you != null && _you.city == null, (_you != null) ? WorldBoxMod.Describe(_you) : "none"))
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
			yield return Part("founding", Founding());
			if (_town != null)
			{
				yield return Part("the hold", TheHold());
				yield return Part("the hammer", TheHammer());
				yield return Part("plans", Plans());
				yield return Part("builders", Builders());
				yield return Part("refunds", Refunds());
				yield return Part("land", Land());
				yield return Part("your home", YourHome());
				yield return Part("hand-off", HandOff());
				Planning.AimForTest = null;
				FirstPerson.Towns.Village.GraceForTest = -1f;
				if (Planning.Aiming)
				{
					Planning.Escape();
				}
			}
		}

		private IEnumerator Founding()
		{
			Check("a nomad could found a village", FirstPerson.Towns.Village.CanFound(_you));
			Planning.Key(_you);
			Check("Y on open land is founding", Planning.Aiming && Planning.Founding);
			WorldTile aim = (Planning.AimForTest = Above(_stage, FirstPerson.Towns.Village.BonfireOf(_you)));
			yield return Frames(4);
			Check("the bonfire's ghost is green on open land", Planning.Valid && Planning.SiteTile == _stage, Planning.Why);
			WorldTile worldTile = WaterNear(_stage, 40);
			if (worldTile != null)
			{
				Planning.AimForTest = worldTile;
				yield return Frames(4);
				Check("...and red on water", !Planning.Valid, Planning.Why);
				Planning.AimForTest = aim;
				yield return Frames(4);
			}
			Look(new Vector2((float)_stage.x + 0.5f, (float)_stage.y + 2f), 8f);
			yield return CivilTest.Wait(0.6f);
			yield return Shot("planning_found.png");
			Mod.DebugEyeSet = false;
			Planning.ClickForTest = true;
			yield return Frames(6);
			_town = _you.city;
			if (Check("a click founds the village", _town != null, Planning.Why))
			{
				Check("...a realm of your own, you its king", _you.isKing() && _town.kingdom != null && _town.kingdom.king == _you, _town.kingdom?.name);
				Check("...marked your village", FirstPerson.Towns.Village.IsVillage(_town));
				Check("...planned by you alone", FirstPerson.Towns.Village.PlansAlone(_town) && _town.timer_build > 5f, $"timer {_town.timer_build:F1}");
				List<Building> list = Blueprints.SitesOf(_town);
				_fire = ((list.Count > 0) ? list[0] : null);
				Check("...the bonfire its first site, where it was set", _fire != null && _fire.asset.type == "type_bonfire" && _fire.isUnderConstruction() && _fire.current_tile == _stage, (_fire != null) ? $"{_fire.asset.id} at {_fire.current_tile?.x},{_fire.current_tile?.y}" : "none");
				Check("...the founding mode is over", !Planning.Aiming);
				Planning.AimForTest = null;
			}
		}

		private IEnumerator TheHold()
		{
			int before = _town.buildings.Count;
			for (int i = 0; i < 16; i++)
			{
				yield return CivilTest.Wait(0.5f);
			}
			Check("nothing goes up that you didn't plan", _town.buildings.Count == before && Blueprints.SitesOf(_town).Count == before, $"{_town.buildings.Count} buildings, {Blueprints.SitesOf(_town).Count} plans");
			Check("the town's build timer is held", _town.timer_build > 5f, $"{_town.timer_build:F1}");
		}

		private IEnumerator TheHammer()
		{
			if (!Check("a bonfire site to build", _fire != null && _fire.isAlive()))
			{
				yield break;
			}
			Vector2 fire = new Vector2((float)_stage.x + 0.5f, (float)_stage.y + 1.5f);
			Vector2 stand = fire + new Vector2(0f, -3.2f);
			Equip(_you, "hammer_wood");
			yield return AimAt(_fire, stand, fire);
			Check("with a hammer it's a work target", Mod.Work.Target == _fire && Mod.Prompt != null && Mod.Prompt.StartsWith("LMB:Build"), "prompt '" + Mod.Prompt + "', picked " + Picked());
			Equip(_you, "sword_iron");
			yield return AimAt(_fire, stand, fire, () => Mod.Work.Needs != null);
			Check("with a sword: build with a hammer", Mod.Work.Target == null && Mod.Work.Needs != null && Mod.Work.Needs.Contains("hammer"), "needs '" + Mod.Work.Needs + "', picked " + Picked());
			Equip(_you, "hammer_wood");
			yield return AimAt(_fire, stand, fire);
			float w0 = Blueprints.WorkOn(_fire);
			Mod.Work.ForceSwing = true;
			for (float t = 0f; t < 3.4f; t += Time.unscaledDeltaTime)
			{
				Stay(stand, fire);
				yield return null;
			}
			Mod.Work.ForceSwing = false;
			float w1 = Blueprints.WorkOn(_fire);
			int constructionProgress = _fire.getConstructionProgress();
			Check("held, the hammer works it (8 a blow: four builders')", w1 - w0 >= 16f && Mathf.Abs((w1 - w0) % 8f) < 0.01f, $"work {w0} -> {w1}");
			Check("...the game's progress at half the work", constructionProgress == Mathf.FloorToInt(w1 / 2f), $"progress {constructionProgress}, work {w1}");
			yield return Shot("planning_hammer.png");
			Check("bare hands build at a third", Mathf.Abs(Blueprints.ToolFactor("hands") - 1f / 3f) < 0.01f && Blueprints.ToolFactor("sword_iron") == 0f && Blueprints.ToolFactor("hammer_iron") > Blueprints.ToolFactor("hammer_wood"));
			int num = Mathf.RoundToInt(w1 / 8f);
			for (int num2 = 0; num2 < 200; num2++)
			{
				if (!_fire.isUnderConstruction())
				{
					break;
				}
				Blueprints.Blow(_you, _fire, 1f);
				num++;
			}
			int construction_progress_needed = _fire.asset.construction_progress_needed;
			int num3 = Mathf.CeilToInt(2f * (float)construction_progress_needed / 8f);
			Check("the bonfire stands after twice the game's work, a quarter the blows", !_fire.isUnderConstruction() && _town.countBuildingsType("type_bonfire") == 1 && Mathf.Abs(num - num3) <= 1, $"{num} blows for a need of {construction_progress_needed} (want {num3})");
			Mod.DebugEyeSet = false;
		}

		private IEnumerator Plans()
		{
			Planning.Key(_you);
			Check("Y in your village is planning", Planning.Aiming && !Planning.Founding && Planning.Town == _town);
			Vector2 fire = Mid(_fire);
			Check("a second bonfire: one per town", Planning.PickForTest("order_bonfire") && PlanPieces.WhyNot(Planning.Piece, _town) == "One per town", PlanPieces.WhyNot(Planning.Piece, _town));
			Check("the windmill opens at level 2", Planning.PickForTest("order_windmill_0") && PlanPieces.WhyNot(Planning.Piece, _town) == "Opens at level 2", PlanPieces.WhyNot(Planning.Piece, _town));
			Planning.PickForTest("order_bonfire");
			Planning.AimForTest = Above(FreeSpot(Planning.Asset, fire, 6f, 14f) ?? _stage, Planning.Asset);
			yield return Frames(4);
			Check("...a second bonfire's ghost only a faint hint", !Planning.Valid && Planning.Nowhere, Planning.Why);
			Planning.PickForTest("order_stockpile");
			WorldTile spot = FreeSpot(Planning.Asset, fire, 6f, 12f);
			if (!Check("land for the stockpile", spot != null))
			{
				yield break;
			}
			Planning.AimForTest = Above(spot, Planning.Asset);
			yield return Frames(4);
			Check("its ghost is green", Planning.Valid && Planning.SiteTile == spot, Planning.Why);
			Planning.ClickForTest = true;
			yield return Frames(4);
			_stockpile = spot.building;
			Check("a click plans it: free, so paid", _stockpile != null && _stockpile.isUnderConstruction() && Blueprints.IsPaid(_stockpile) && _stockpile.asset.type == "type_stockpile", _stockpile?.asset?.id);
			Planning.PickForTest("order_tent");
			Planning.AimForTest = Above(spot, Planning.Asset);
			yield return Frames(4);
			Check("a home on top of it: the ghost is red", !Planning.Valid && Planning.Why == "Too close to another building", Planning.Why);
			Planning.PickForTest("order_hall_0");
			spot = FreeSpot(Planning.Asset, fire, 6f, 14f);
			if (Check("land for the hall", spot != null))
			{
				Planning.AimForTest = Above(spot, Planning.Asset);
				yield return Frames(4);
				Planning.ClickForTest = true;
				yield return Frames(4);
				_hall = spot.building;
				Check("the hall is planned, unpaid with bare stores", _hall != null && _hall.isUnderConstruction() && !Blueprints.IsPaid(_hall), _hall?.asset?.id);
				for (int i = 0; i < 4; i++)
				{
					yield return CivilTest.Wait(0.4f);
				}
				Check("the builders' site is the paid stockpile, not the hall", _town.under_construction_building == _stockpile, _town.under_construction_building?.asset?.id ?? "none");
				Look(fire + new Vector2(0f, 3f), 11f);
				Planning.AimForTest = Above(FreeSpot(Planning.Asset, fire, 8f, 16f) ?? spot, Planning.Asset);
				yield return CivilTest.Wait(0.6f);
				yield return Shot("planning_bar.png");
				Mod.DebugEyeSet = false;
				Planning.AimForTest = null;
				Planning.Escape();
			}
		}

		private IEnumerator Builders()
		{
			if (!Check("plans to build", _stockpile != null && _hall != null))
			{
				yield break;
			}
			Vector2 vector = Mid(_fire);
			for (int i = 0; i < 3; i++)
			{
				Actor actor = CivilTest.SpawnNear("human", vector + new Vector2(2f + (float)i, -2f));
				if (actor != null)
				{
					try
					{
						actor.joinCity(_town);
						((BaseSimObject)actor).addStatusEffect("invincible", 1200f, pColorEffect: false);
					}
					catch
					{
					}
					_folk.Add(actor);
				}
			}
			Check("three newcomers", _folk.Count == 3 && _folk.TrueForAll((Actor a) => a.city == _town));
			Building townHome = null;
			for (int i2 = 0; i2 < 30; i2++)
			{
				if (townHome != null)
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
				foreach (Building item in Blueprints.SitesOf(_town))
				{
					item.data.get("fp_town_home", out var pResult, pDefault: false);
					if (pResult)
					{
						townHome = item;
					}
				}
			}
			Check("newcomers without a bed: the town plans them a home", townHome != null && townHome.asset.type == "type_house", $"homeless {_town.status.homeless}, free beds {_town.status.housing_free}");
			float t0 = Time.unscaledTime;
			for (int i2 = 0; i2 < 720; i2++)
			{
				if (!_stockpile.isUnderConstruction())
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			float num = Time.unscaledTime - t0;
			Check("they build the stockpile", !_stockpile.isUnderConstruction() && _stockpile.isAlive(), $"{num:F0} s");
			Check("...at twice the work", Blueprints.WorkOn(_stockpile) >= (float)(2 * _stockpile.asset.construction_progress_needed - 2), $"work {Blueprints.WorkOn(_stockpile)} for a need of {_stockpile.asset.construction_progress_needed}");
			Check("...and leave the unpaid hall alone", _hall.isAlive() && _hall.getConstructionProgress() == 0 && !Blueprints.IsPaid(_hall), $"hall progress {_hall.getConstructionProgress()}");
			_town.addResourcesToRandomStockpile("wood", 30);
			_town.addResourcesToRandomStockpile("stone", 30);
			_town.addResourcesToRandomStockpile("gold", 20);
			_town.addResourcesToRandomStockpile("common_metals", 4);
			int wood = _town.getResourcesAmount("wood");
			int stone = _town.getResourcesAmount("stone");
			for (int i2 = 0; i2 < 160; i2++)
			{
				if (Blueprints.IsPaid(_hall))
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			for (int i2 = 0; i2 < 6; i2++)
			{
				if (_town.under_construction_building == _hall)
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			Check("stores in: carried to the hall, it's paid and the builders' turn", Blueprints.IsPaid(_hall) && _town.under_construction_building == _hall, $"paid {Blueprints.IsPaid(_hall)}, site {_town.under_construction_building?.asset?.id}");
			ConstructionCost constructionCost = ((townHome != null && townHome.isAlive()) ? townHome.asset.cost : null);
			int resourcesAmount = _town.getResourcesAmount("wood");
			int resourcesAmount2 = _town.getResourcesAmount("stone");
			int num2 = wood - _hall.asset.cost.wood;
			int num3 = stone - _hall.asset.cost.stone;
			Check("...from the stockpile", resourcesAmount <= num2 && resourcesAmount >= num2 - (constructionCost?.wood ?? 0) && resourcesAmount2 <= num3 && resourcesAmount2 >= num3 - (constructionCost?.stone ?? 0), $"wood {wood} -> {resourcesAmount}, stone {stone} -> {resourcesAmount2}, hall {_hall.asset.cost.wood} wood {_hall.asset.cost.stone} stone, home {constructionCost?.wood ?? 0} wood");
			for (int num4 = 0; num4 < 300; num4++)
			{
				if (!_hall.isUnderConstruction())
				{
					break;
				}
				Blueprints.Blow(_you, _hall, 1.5f);
			}
			Check("the village hall stands", !_hall.isUnderConstruction() && FirstPerson.Towns.Village.HallLevel(_town) == 0, $"hall level {FirstPerson.Towns.Village.HallLevel(_town)}");
			if (townHome != null && townHome.isAlive())
			{
				Blueprints.Pay(_town, townHome, null, out var _);
				for (int num5 = 0; num5 < 300; num5++)
				{
					if (!townHome.isUnderConstruction())
					{
						break;
					}
					Blueprints.Blow(_you, townHome, 1.5f);
				}
				Check("...and the newcomers' home stands, nobody's own", !townHome.isUnderConstruction() && !Homes.OwnedBy(townHome, _you), townHome.asset?.id);
			}
			yield return null;
		}

		private IEnumerator Refunds()
		{
			try
			{
				_town.data.set("fp_level", 3);
			}
			catch
			{
			}
			Planning.Begin(_you, _town);
			Vector2 fire = Mid(_fire);
			Planning.PickForTest("order_well");
			WorldTile spot = FreeSpot(Planning.Asset, fire, 6f, 18f);
			if (!Check("land for a well", spot != null))
			{
				yield break;
			}
			int stone0 = _town.getResourcesAmount("stone");
			Planning.AimForTest = Above(spot, Planning.Asset);
			yield return Frames(4);
			Planning.ClickForTest = true;
			yield return Frames(4);
			Building well = spot.building;
			if (!Check("a well planned", well?.isUnderConstruction() ?? false, Planning.Why))
			{
				yield break;
			}
			for (int i = 0; i < 240; i++)
			{
				if (Blueprints.IsPaid(well))
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			for (int i = 0; i < 12; i++)
			{
				if (_town.under_construction_building == well)
				{
					break;
				}
				yield return CivilTest.Wait(0.1f);
			}
			Check("its materials carried from the stockpile, the builders take it", Blueprints.IsPaid(well) && _town.under_construction_building == well && _town.getResourcesAmount("stone") == stone0 - well.asset.cost.stone, string.Format("stone {0} -> {1}", stone0, _town.getResourcesAmount("stone")));
			int stone1 = _town.getResourcesAmount("stone");
			int cost = well.asset.cost.stone;
			Planning.AimForTest = Above(spot, Planning.Asset);
			yield return Frames(3);
			Planning.RightClickForTest = true;
			yield return Frames(4);
			Check("a paid plan taken back: gone, its cost back", !well.isAlive() || well.isRuin() || !Blueprints.SitesOf(_town).Contains(well), $"alive {well.isAlive()}");
			Check("...the stone back in the stockpile", _town.getResourcesAmount("stone") == stone1 + cost, string.Format("stone {0} -> {1}", stone1, _town.getResourcesAmount("stone")));
			yield return Frames(10);
			spot = FreeSpot(Planning.Asset, fire, 6f, 18f);
			Planning.AimForTest = Above(spot, Planning.Asset);
			yield return Frames(4);
			Planning.ClickForTest = true;
			yield return Frames(4);
			well = spot?.building;
			if (!Check("another well", well?.isUnderConstruction() ?? false, Planning.Why))
			{
				yield break;
			}
			Blueprints.Pay(_town, well, _you, out var _);
			for (int j = 0; j < 400; j++)
			{
				if (!well.isUnderConstruction())
				{
					break;
				}
				Blueprints.Blow(_you, well, 1.5f);
			}
			if (Check("...built", !well.isUnderConstruction()))
			{
				int stone2 = _town.getResourcesAmount("stone");
				int gold2 = _town.getResourcesAmount("gold");
				int stoneCost = well.asset.cost.stone;
				int goldCost = well.asset.cost.gold;
				Planning.AimForTest = Above(spot, Planning.Asset);
				yield return Frames(3);
				Planning.RightClickForTest = true;
				yield return Frames(3);
				Check("one right-click asks first", well.isAlive() && !well.isRuin());
				Planning.RightClickForTest = true;
				yield return Frames(4);
				Check("a second knocks it down, half its cost back", _town.getResourcesAmount("stone") == stone2 + stoneCost / 2 && _town.getResourcesAmount("gold") == gold2 + goldCost / 2, string.Format("stone {0} -> {1}, gold {2} -> {3}", stone2, _town.getResourcesAmount("stone"), gold2, _town.getResourcesAmount("gold")));
				Check("the hall can't be knocked down", Blueprints.WhyNotKnockDown(_hall) != null);
				Planning.AimForTest = null;
				Planning.Escape();
			}
		}

		private IEnumerator Land()
		{
			Planning.Begin(_you, _town);
			Planning.PickForTest("order_tent");
			BuildingAsset tent = Planning.Asset;
			WorldTile edge = null;
			WorldTile far = null;
			foreach (TileZone zone in _town.zones)
			{
				TileZone[] neighbours = zone.neighbours;
				foreach (TileZone tileZone in neighbours)
				{
					if (tileZone == null || tileZone.city != null || tileZone.centerTile == null)
					{
						continue;
					}
					if (edge == null && Blueprints.WhyNotHere(_town, tent, tileZone.centerTile, _claims) == null && _claims.Count > 0)
					{
						edge = tileZone.centerTile;
					}
					TileZone[] neighbours2 = tileZone.neighbours;
					foreach (TileZone tileZone2 in neighbours2)
					{
						if (tileZone2 == null || tileZone2.city != null || tileZone2.centerTile == null || far != null || !Dry(tileZone2.centerTile, tent))
						{
							continue;
						}
						bool flag = false;
						TileZone[] neighbours3 = tileZone2.neighbours;
						foreach (TileZone tileZone3 in neighbours3)
						{
							if (tileZone3 != null && tileZone3.city == _town)
							{
								flag = true;
							}
						}
						if (!flag)
						{
							far = tileZone2.centerTile;
						}
					}
				}
			}
			if (Check("free land beside the town's", edge != null))
			{
				int zones = _town.zones.Count;
				Planning.AimForTest = Above(edge, tent);
				yield return Frames(4);
				Check("a plan there would claim the zone", Planning.Valid && Planning.ClaimsNow.Count > 0 && Planning.Prompt != null && Planning.Prompt.Contains("claims land"), Planning.Why ?? Planning.Prompt);
				Planning.ClickForTest = true;
				yield return Frames(4);
				Building building = edge.building;
				Check("placed, the town's land grows", building != null && _town.zones.Count > zones && edge.zone.city == _town, $"{zones} -> {_town.zones.Count} zones");
				if (building != null)
				{
					Blueprints.Remove(building, _you);
				}
				yield return Frames(4);
				Check("taken back, the land too", _town.zones.Count == zones && edge.zone.city == null, $"{_town.zones.Count} zones");
				if (far != null)
				{
					Planning.AimForTest = Above(far, tent);
					yield return Frames(4);
					Check("land not touching the town's can't be claimed", !Planning.Valid && Planning.Why != null && Planning.Why.Contains("Not next to"), Planning.Why);
				}
				Planning.AimForTest = null;
				Planning.Escape();
			}
		}

		private IEnumerator YourHome()
		{
			Planning.Begin(_you, _town);
			Planning.PickForTest("order_tent");
			Check("your first home would be yours", Planning.FirstHome(_you, _town));
			WorldTile spot = FreeSpot(Planning.Asset, Mid(_fire), 5f, 14f);
			if (!Check("land for a home", spot != null))
			{
				yield break;
			}
			Planning.AimForTest = Above(spot, Planning.Asset);
			yield return Frames(4);
			Planning.ClickForTest = true;
			yield return Frames(4);
			Building home = spot.building;
			Check("a home planned, yours", home != null && Blueprints.HomePlanned(_town, _you) && !Planning.FirstHome(_you, _town), home?.asset?.id);
			Planning.AimForTest = null;
			Planning.Escape();
			if (home == null)
			{
				yield break;
			}
			Blueprints.Pay(_town, home, _you, out var _);
			for (int i = 0; i < 300; i++)
			{
				if (!home.isUnderConstruction())
				{
					break;
				}
				Blueprints.Blow(_you, home, 1.5f);
			}
			yield return CivilTest.Wait(1f);
			Check("it stands: your house", !home.isUnderConstruction() && Homes.OwnedBy(home, _you) && HomeLife.OwnHouse(_you) == home, home.asset.id);
			_town.addResourcesToRandomStockpile("wood", 20);
			for (int j = 0; j < 30; j++)
			{
				if (home.asset.id.StartsWith("house_"))
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			Check("...and grows from a tent to a house", home.asset.id.StartsWith("house_"), home.asset.id);
			Look(Mid(home) + new Vector2(0f, -1f), 9f, -7f);
			yield return CivilTest.Wait(0.6f);
			yield return Shot("planning_plans.png");
			Mod.DebugEyeSet = false;
		}

		private IEnumerator HandOff()
		{
			Actor actor = null;
			foreach (Actor item in _folk)
			{
				if (item != null && item.isAlive() && _town.leader != item && !item.isKing())
				{
					actor = item;
					break;
				}
			}
			if (!Check("someone else to play", actor != null))
			{
				yield break;
			}
			FirstPerson.Towns.Village.GraceForTest = 1f;
			yield return Possess(actor);
			for (int i = 0; i < 10; i++)
			{
				if (!FirstPerson.Towns.Village.PlansAlone(_town))
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			Check("playing someone else, the hold lifts after the grace", !FirstPerson.Towns.Village.PlansAlone(_town) && _town.timer_build <= 5f, $"timer {_town.timer_build:F1}");
			yield return Possess(_you);
			for (int i = 0; i < 10; i++)
			{
				if (FirstPerson.Towns.Village.PlansAlone(_town))
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			Check("back as its king, it holds again", FirstPerson.Towns.Village.PlansAlone(_town) && _town.timer_build > 5f, $"timer {_town.timer_build:F1}");
			FirstPerson.Towns.Village.GraceForTest = -1f;
		}

		private static IEnumerator Frames(int n)
		{
			for (int i = 0; i < n; i++)
			{
				yield return null;
			}
		}

		private static Vector2 Mid(Building b)
		{
			WorldTile current_tile = b.current_tile;
			float num = ((b.asset.fundament != null) ? ((float)b.asset.fundament.top * 0.5f) : 0f);
			return new Vector2((float)current_tile.x + 0.5f, (float)current_tile.y + 0.5f + num);
		}

		private static WorldTile Above(WorldTile t, BuildingAsset a)
		{
			if (t == null)
			{
				return null;
			}
			int num = ((a?.fundament != null) ? (a.fundament.top / 2) : 0);
			return World.world.GetTile(t.x, t.y + num) ?? t;
		}

		private WorldTile FreeSpot(BuildingAsset a, Vector2 near, float min, float max)
		{
			for (float num = min; num <= max; num += 1f)
			{
				for (int i = 0; i < 24; i++)
				{
					float f = (float)i * MathF.PI / 12f + num * 0.37f;
					WorldTile tile = World.world.GetTile(Mathf.FloorToInt(near.x + Mathf.Cos(f) * num), Mathf.FloorToInt(near.y + Mathf.Sin(f) * num));
					if (tile != null && tile.zone != null && tile.zone.city == _town && Blueprints.WhyNotHere(_town, a, tile, _claims) == null && _claims.Count == 0)
					{
						return tile;
					}
				}
			}
			return null;
		}

		private static bool Dry(WorldTile t, BuildingAsset a)
		{
			foreach (WorldTile item in Blueprints.Footprint(t, a))
			{
				if (item == null || item.Type == null || item.Type.liquid || !item.Type.ground || item.Type.block)
				{
					return false;
				}
			}
			return true;
		}

		private static WorldTile WaterNear(WorldTile at, int reach)
		{
			for (int i = 4; i <= reach; i += 2)
			{
				for (int j = 0; j < 16; j++)
				{
					WorldTile tile = World.world.GetTile(at.x + Mathf.RoundToInt(Mathf.Cos((float)j * 0.3927f) * (float)i), at.y + Mathf.RoundToInt(Mathf.Sin((float)j * 0.3927f) * (float)i));
					if (tile != null && tile.Type != null && tile.Type.liquid)
					{
						return tile;
					}
				}
			}
			return null;
		}

		private void Look(Vector2 at, float from, float pitch = -16f)
		{
			Vector2 debugEye = at + new Vector2(-0.6f, -0.8f) * from;
			Mod.DebugEye = debugEye;
			Mod.DebugEyeSet = true;
			Mod.SetYaw(Mathf.Atan2(at.y - debugEye.y, at.x - debugEye.x));
			Mod.SetPitchDegrees(pitch);
		}

		private void Stay(Vector2 stand, Vector2 at)
		{
			if (_you != null && _you.isAlive())
			{
				_you.current_position = stand;
				_you.dirty_current_tile = true;
				Mod.SetYaw(Mathf.Atan2(at.y - stand.y, at.x - stand.x));
			}
		}

		private IEnumerator AimAt(Building b, Vector2 stand, Vector2 at, Func<bool> done = null)
		{
			for (float pitch = 0f; pitch > -50f; pitch -= 3f)
			{
				Mod.SetPitchDegrees(pitch);
				for (float t = 0f; t < 0.12f; t += Time.unscaledDeltaTime)
				{
					Stay(stand, at);
					yield return null;
				}
				if (done?.Invoke() ?? (Mod.Picked == b || Mod.Work.Target == b))
				{
					for (int i = 0; i < 4; i++)
					{
						Stay(stand, at);
						yield return null;
					}
					break;
				}
			}
		}

		private string Picked()
		{
			object obj;
			if (!(Mod.Picked is Building building))
			{
				obj = Mod.Picked?.ToString();
				if (obj == null)
				{
					return "nothing";
				}
			}
			else
			{
				obj = building.asset.id;
			}
			return (string)obj;
		}

		private bool Equip(Actor unit, string id)
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
				Log.Error(Prefix + " equip " + id + ": " + ex.Message);
				return false;
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
