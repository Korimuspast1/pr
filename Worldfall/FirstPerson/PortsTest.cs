using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class PortsTest : CivilTest
	{
		private const int Harbours = 5;

		private readonly List<Building> _docks = new List<Building>();

		private readonly List<City> _towns = new List<City>();

		private City _planTown;

		private Actor _planKing;

		private WorldTile _planAt;

		protected override string Prefix => "PORTS";

		protected override IEnumerator Body()
		{
			Mod.DebugEyeSet = true;
			yield return Part("towns by the sea, harbours where the game puts them", GameHarbours());
			yield return Part("a harbour planned with Y", Planned());
			Planning.AimForTest = null;
			if (Planning.Aiming)
			{
				Planning.Escape();
			}
		}

		private IEnumerator GameHarbours()
		{
			int num = 0;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				if ((_docks.Count >= 5 && _planTown != null) || num >= 60)
				{
					break;
				}
				if (!NearSea(item, 14))
				{
					continue;
				}
				bool flag = true;
				foreach (City town in _towns)
				{
					WorldTile tile = town.getTile();
					if (tile != null && Mathf.Abs(tile.x - item.x) + Mathf.Abs(tile.y - item.y) < 40)
					{
						flag = false;
					}
				}
				if (!flag)
				{
					continue;
				}
				num++;
				Actor actor = KingTest.FoundKingdom(item, "human", 4, 0);
				if (actor == null || actor.city == null)
				{
					continue;
				}
				City city = actor.city;
				GrowToCoast(city, item);
				WorldTile worldTile = null;
				try
				{
					worldTile = CityBehBuild.getDockTile(city);
				}
				catch (Exception ex)
				{
					Note("getDockTile: " + ex.Message);
				}
				if (worldTile == null)
				{
					Note($"{city.name} at {item.x},{item.y}: the game finds no harbour spot");
					Disband(actor);
					continue;
				}
				_towns.Add(city);
				if (_planTown == null)
				{
					_planTown = city;
					_planKing = actor;
					_planAt = worldTile;
					Note($"{city.name}: the game's spot {worldTile.x},{worldTile.y} kept for planning");
					continue;
				}
				Building building = null;
				try
				{
					BuildingAsset buildingDockAsset = city.getActorAsset().getBuildingDockAsset();
					building = World.world.buildings.addBuilding(buildingDockAsset, worldTile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					if (building != null)
					{
						building.setKingdom(city.kingdom);
						if (building.city != city)
						{
							city.listBuilding(building);
						}
						building.completeConstruction();
					}
				}
				catch (Exception ex2)
				{
					Note("harbour: " + ex2.Message);
				}
				if (building == null)
				{
					Note($"{city.name}: the game's spot {worldTile.x},{worldTile.y} took no harbour");
					continue;
				}
				_docks.Add(building);
				Note($"{city.name}: harbour {building.asset.id} at {worldTile.x},{worldTile.y} (the game's spot)");
			}
			Check("harbours put where the game's towns put them", _docks.Count >= 3, $"{_docks.Count} harbours, {num} towns tried");
			Actor you = _planKing;
			if (you == null)
			{
				foreach (Building dock in _docks)
				{
					if (dock.city?.kingdom?.king != null)
					{
						you = dock.city.kingdom.king;
						break;
					}
				}
			}
			if (Check("someone to be", you != null))
			{
				try
				{
					((BaseSimObject)you).addStatusEffect("invincible", 900f, pColorEffect: false);
				}
				catch
				{
				}
				yield return Possess(you);
				Check("in first person", InControl(you));
				for (int i = 0; i < _docks.Count; i++)
				{
					yield return Survey(_docks[i], "game" + i, judge: true);
				}
			}
		}

		private static void GrowToCoast(City town, WorldTile mid)
		{
			try
			{
				foreach (TileZone zone in World.world.zone_calculator.zones)
				{
					if (zone == null || zone.city != null || zone.centerTile == null || zone.tiles_with_liquid == 0 || Mathf.Abs(zone.centerTile.x - mid.x) > 14 || Mathf.Abs(zone.centerTile.y - mid.y) > 14)
					{
						continue;
					}
					bool flag = false;
					TileZone[] neighbours = zone.neighbours;
					foreach (TileZone tileZone in neighbours)
					{
						if (tileZone != null && tileZone.city == town)
						{
							flag = true;
						}
					}
					if (flag)
					{
						town.addZone(zone);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("PORTS zones: " + ex);
			}
		}

		private static void Disband(Actor king)
		{
			try
			{
				foreach (Actor item in new List<Actor>(king.city.units))
				{
					item.dieSimpleNone();
				}
			}
			catch
			{
			}
		}

		private static bool NearSea(WorldTile t, int r)
		{
			for (int i = -r; i <= r; i += 2)
			{
				for (int j = -r; j <= r; j += 2)
				{
					WorldTile tile = World.world.GetTile(t.x + j, t.y + i);
					try
					{
						if (tile != null && tile.Type != null && tile.Type.ocean)
						{
							return true;
						}
					}
					catch
					{
					}
				}
			}
			return false;
		}

		private IEnumerator Survey(Building d, string label, bool judge)
		{
			Vector2 p = Middle(d);
			bool flag = false;
			try
			{
				flag = d.isRuin();
			}
			catch
			{
			}
			if (flag)
			{
				Note(label + ": the game made it a ruin (its zone " + ((d.current_tile?.zone?.city == null) ? "has no town" : ("is " + d.current_tile.zone.city.name + "'s")) + "): not measured");
				yield break;
			}
			Look(p);
			int i = -1;
			for (float t = 0f; t < 15f; t += 0.25f)
			{
				int num;
				i = (num = DeckOf(d));
				if (num >= 0)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			if (i < 0)
			{
				string text;
				try
				{
					text = string.Format("alive {0}, ruin {1}, building {2}, town {3}, tile {4},{5}, eye {6}", d.isAlive(), d.isRuin(), d.isUnderConstruction(), (d.city != null) ? d.city.name : "none", d.current_tile?.x, d.current_tile?.y, Mod.DebugEye);
				}
				catch (Exception ex)
				{
					text = ex.Message;
				}
				Note(label + ": not drawn: " + text);
			}
			if (!Check(label + ": the harbour is drawn", i >= 0, $"{DockDecks.Count} docks drawn"))
			{
				yield break;
			}
			yield return CivilTest.Wait(1f);
			i = DeckOf(d);
			if (i < 0)
			{
				yield break;
			}
			TerrainCache terrainForTest = Mod.TerrainForTest;
			DockDecks.PointForTest(i, 0f, 0f, out var at, out var heading);
			Vector2 vector = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
			Vector2 zero = Vector2.zero;
			int feet = 0;
			int dryFeet = 0;
			int num2 = 0;
			HashSet<WorldTile> hashSet = new HashSet<WorldTile>(Blueprints.Footprint(d.current_tile, d.asset));
			foreach (WorldTile item in hashSet)
			{
				feet++;
				if (!Liquid(item))
				{
					dryFeet++;
				}
				WorldTile[] neighbours = item.neighbours;
				foreach (WorldTile worldTile in neighbours)
				{
					if (worldTile != null && !hashSet.Contains(worldTile) && !Liquid(worldTile))
					{
						num2++;
						zero += new Vector2((float)worldTile.x + 0.5f - at.x, (float)worldTile.y + 0.5f - at.y);
					}
				}
			}
			float off = ((num2 > 0) ? Vector2.Angle(vector, -zero) : (-1f));
			string text2 = "";
			int endOnLand = 0;
			int sunk = 0;
			float lowDeck = 99f;
			float highDeck = -99f;
			float landingGap = 0f;
			float[] array = new float[7] { 3f, 1.5f, 0f, -2f, -4f, -6f, -8f };
			float heading2;
			foreach (float num3 in array)
			{
				DockDecks.PointForTest(i, 0f, num3, out var at2, out heading2);
				WorldTile tile = World.world.GetTile((int)at2.x, (int)at2.y);
				bool flag2 = tile != null && Liquid(tile);
				float num4 = terrainForTest.SampleHeight(at2.x, at2.y);
				float z;
				bool flag3 = DockDecks.HeightAt(at2.x, at2.y, out z);
				text2 += string.Format(" y{0:0.#}:{1} g{2:0.00} d{3}", num3, flag2 ? "w" : "L", num4, flag3 ? z.ToString("0.00") : "-");
				if (num3 <= -4f && !flag2)
				{
					endOnLand++;
				}
				if (flag3 && z < num4 - 0.05f)
				{
					sunk++;
				}
				if (flag3 && num3 <= -2f)
				{
					lowDeck = Mathf.Min(lowDeck, z - 0f);
					highDeck = Mathf.Max(highDeck, z - 0f);
				}
				if (num3 == 3f && flag3)
				{
					landingGap = z - num4;
				}
			}
			int berths = 0;
			int under = 0;
			string text3 = "";
			try
			{
				d.component_docks.recalculateOceanTiles();
				foreach (ref WorldTile item2 in d.component_docks.tiles_ocean)
				{
					WorldTile current2 = item2;
					if (current2 != null)
					{
						berths++;
						bool flag4 = DockDecks.On(new Vector2((float)current2.x + 0.5f, (float)current2.y + 0.5f));
						if (flag4)
						{
							under++;
						}
						text3 += string.Format(" {0},{1}{2}", current2.x, current2.y, flag4 ? "*" : "");
					}
				}
			}
			catch (Exception ex2)
			{
				Note("berths: " + ex2.Message);
			}
			WorldTile current_tile = d.current_tile;
			Note($"{label}: {d.asset.id} tile {current_tile.x},{current_tile.y} footprint {feet} ({dryFeet} dry), land beside {num2}, pier heading {heading * 57.29578f:0} deg, " + string.Format("{0} deg off straight out from the shore; deck {1:0.00}..{2:0.00} over the water, landing {3:0.00} over its ground;{4}; berths {5} ({6} under the boards):{7}", (off >= 0f) ? off.ToString("0") : "?", lowDeck, highDeck, landingGap, text2, berths, under, text3));
			Frame("ports_" + label + "_above");
			yield return CivilTest.Wait(0.6f);
			DockDecks.PointForTest(i, 0f, 4.5f, out var at3, out heading2);
			Mod.DebugEye = at3;
			Mod.DebugEyeHeight = 1.7f;
			Mod.DebugEyeSet = true;
			Mod.SetYaw(heading);
			Mod.SetPitchDegrees(-8f);
			yield return CivilTest.Wait(1.2f);
			Frame("ports_" + label + "_landing");
			yield return CivilTest.Wait(0.6f);
			if (judge)
			{
				Check(label + ": its footprint on water, as the game's rule has it", dryFeet == 0, $"{dryFeet} of {feet} tiles dry");
				Check(label + ": the pier runs out from the shore", off >= 0f && off <= 50f, $"{off:0} deg off");
				Check(label + ": its far end over water", endOnLand == 0, $"{endOnLand} points on land");
				Check(label + ": its boards never inside the ground", sunk == 0, $"{sunk} points");
				Check(label + ": its boards at the waterline out on the water", lowDeck > 0.15f && highDeck < 1.2f, $"{lowDeck:0.00}..{highDeck:0.00}");
				Check(label + ": its landing on the shore, not hanging over it", landingGap < 0.9f, $"{landingGap:0.00} over the ground");
				Check(label + ": its boats' berths not under its boards", berths > 0 && under * 2 <= berths, $"{under} of {berths}");
			}
		}

		private IEnumerator Planned()
		{
			if (!Check("a town by the sea to plan in, where the game would put a harbour", _planTown != null && _planAt != null))
			{
				yield break;
			}
			City town = _planTown;
			Actor king = _planKing;
			if (town.countBuildingsType("type_bonfire", pCountOnlyFinished: false) == 0)
			{
				AddHouse("bonfire", town);
			}
			GiveStockpile(town);
			try
			{
				town.addResourcesToRandomStockpile("wood", 200);
				town.addResourcesToRandomStockpile("stone", 200);
				town.addResourcesToRandomStockpile("common_metals", 50);
				town.addResourcesToRandomStockpile("gold", 200);
			}
			catch
			{
			}
			if (!InControl(king))
			{
				yield return Possess(king);
			}
			if (!Check("its king in first person", InControl(king)))
			{
				yield break;
			}
			try
			{
				Bag.Put(king, "wood", 40, spill: false);
				Bag.Put(king, "stone", 20, spill: false);
			}
			catch (Exception ex)
			{
				Note("bag: " + ex.Message);
			}
			Planning.Begin(king, town);
			if (!Check("Y: planning in the town", Planning.Aiming && Planning.Town == town) || !Check("the fishing docks on the bar", Planning.PickForTest("order_docks_0") && Planning.Asset != null && Planning.Asset.docks, Planning.Asset?.id))
			{
				yield break;
			}
			BuildingAsset a = Planning.Asset;
			int up = ((a.fundament != null) ? (a.fundament.top / 2) : 0);
			WorldTile aim = World.world.GetTile(_planAt.x, _planAt.y + up) ?? _planAt;
			Vector2 mid = new Vector2((float)_planAt.x + 0.5f, (float)_planAt.y + 0.5f + ((a.fundament != null) ? ((float)(a.fundament.top - a.fundament.bottom) * 0.5f) : 0f));
			Look(mid);
			Planning.AimForTest = aim;
			yield return CivilTest.Wait(1f);
			Check("where the game would put a harbour, its ghost is green", Planning.Valid && Planning.SiteTile == _planAt, $"{Planning.Why} at {Planning.SiteTile?.x},{Planning.SiteTile?.y}");
			float auto = GhostHeading();
			Vector2 sea = SeaWay(_planAt, a);
			float num = Vector2.Angle(new Vector2(Mathf.Cos(auto), Mathf.Sin(auto)), sea);
			Note($"ghost heading {auto * 57.29578f:0} deg, the sea's way {Mathf.Atan2(sea.y, sea.x) * 57.29578f:0} deg");
			Check("...its pier facing the sea", num <= 50f, $"{num:0} deg off");
			WorldTile beach = null;
			foreach (WorldTile item in Blueprints.Footprint(_planAt, a))
			{
				WorldTile[] neighbours = item.neighbours;
				foreach (WorldTile worldTile in neighbours)
				{
					if (beach == null && worldTile != null && worldTile.Type != null && worldTile.Type.ground && worldTile.zone != null && worldTile.zone.city == town)
					{
						beach = worldTile;
					}
				}
			}
			if (beach != null)
			{
				Planning.AimForTest = beach;
				yield return CivilTest.Wait(0.6f);
				WorldTile siteTile = Planning.SiteTile;
				bool flag = siteTile != null;
				if (flag)
				{
					foreach (WorldTile item2 in Blueprints.Footprint(siteTile, a))
					{
						if (item2 == null || !Liquid(item2))
						{
							flag = false;
						}
					}
				}
				Check("aimed at the beach by the shore, it moves out onto the water: green", Planning.Valid && flag && siteTile != beach, $"{Planning.Why}, aimed {beach.x},{beach.y}, stands {siteTile?.x},{siteTile?.y}");
				Planning.AimForTest = aim;
				yield return CivilTest.Wait(0.6f);
			}
			else
			{
				Note("no beach of the town's beside the harbour spot: snapping not tried");
			}
			yield return Shot("ports_plan_ghost.png");
			List<float> hs = new List<float>();
			int chosen = -1;
			string ways = "";
			for (int k = 0; k < 4; k++)
			{
				TurnR();
				yield return CivilTest.Wait(0.4f);
				float num2 = GhostHeading();
				hs.Add(num2);
				bool valid = Planning.Valid;
				float num3 = Vector2.Angle(new Vector2(Mathf.Cos(num2), Mathf.Sin(num2)), sea);
				ways += string.Format(" {0:0}:{1}", num2 * 57.29578f, valid ? "green" : Planning.Why);
				if (k < 3 && valid && chosen < 0 && num3 > 30f && num3 < 135f)
				{
					chosen = k;
				}
				if (k < 3 && LandAlong(mid, num2, (a.fundament != null) ? ((float)a.fundament.width * 1.15f) : 5f))
				{
					Check("...turned so its pier would cross the land, red", !valid, $"{Planning.Why}, {num3:0} deg off the sea's way");
				}
			}
			Note("R round:" + ways);
			int num4 = 0;
			for (int j = 0; j < 3; j++)
			{
				float num5 = Mathf.Repeat(hs[j] * 57.29578f + 90f, 90f);
				if (num5 < 3f || num5 > 87f)
				{
					num4++;
				}
			}
			bool flag2 = Mathf.Abs(Mathf.DeltaAngle(hs[0] * 57.29578f, hs[1] * 57.29578f) - 90f) < 3f && Mathf.Abs(Mathf.DeltaAngle(hs[1] * 57.29578f, hs[2] * 57.29578f) - 90f) < 3f;
			Check("R turns the harbour a quarter at a time", num4 == 3 && flag2, string.Format("{0} of 3 on a quarter, steps {1}", num4, flag2 ? "90" : "not 90"));
			Check("...back to out from the shore after four", Mathf.Abs(Mathf.DeltaAngle(hs[3] * 57.29578f, auto * 57.29578f)) < 3f);
			float want = auto;
			if (chosen >= 0)
			{
				for (int k = 0; k <= chosen; k++)
				{
					TurnR();
					yield return CivilTest.Wait(0.3f);
				}
				want = GhostHeading();
			}
			Note(string.Format("placing it at {0:0} deg ({1})", want * 57.29578f, (chosen >= 0) ? "turned" : "as it first faced"));
			yield return Shot("ports_plan_turned.png");
			int before = Blueprints.SitesOf(town).Count;
			Planning.ClickForTest = true;
			yield return CivilTest.Wait(0.6f);
			Building site = null;
			foreach (Building item3 in Blueprints.SitesOf(town))
			{
				if (item3.asset != null && item3.asset.docks && item3.current_tile == _planAt)
				{
					site = item3;
				}
			}
			if (!Check("a click places the harbour's plan", site != null, $"{before} -> {Blueprints.SitesOf(town).Count} plans, {Planning.Why}"))
			{
				yield break;
			}
			Planning.Escape();
			Planning.AimForTest = null;
			try
			{
				site.completeConstruction();
			}
			catch (Exception ex2)
			{
				Note("built: " + ex2.Message);
			}
			yield return CivilTest.Wait(0.5f);
			Look(mid);
			int i2 = -1;
			for (float t = 0f; t < 15f; t += 0.25f)
			{
				int i;
				i2 = (i = DeckOf(site));
				if (i >= 0)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			if (Check("the planned harbour built and drawn", i2 >= 0))
			{
				DockDecks.PointForTest(i2, 0f, 0f, out var _, out var heading);
				Check("...its pier facing as it was planned", Mathf.Abs(Mathf.DeltaAngle(heading * 57.29578f, want * 57.29578f)) < 5f, $"built {heading * 57.29578f:0}, planned {want * 57.29578f:0}");
				yield return Survey(site, "planned", judge: false);
				Planning.Begin(king, town);
				Planning.PickForTest("order_docks_0");
				WorldTile dry = Dry(_planAt, a);
				if (dry != null)
				{
					Planning.AimForTest = World.world.GetTile(dry.x, dry.y + up);
					yield return CivilTest.Wait(0.4f);
					Check("well onto the land, red", !Planning.Valid || Planning.SiteTile != World.world.GetTile(dry.x, dry.y), $"{Planning.Why} at {Planning.SiteTile?.x},{Planning.SiteTile?.y}");
				}
				WorldTile open = OpenSea(_planAt, a, town);
				if (open != null)
				{
					Planning.AimForTest = World.world.GetTile(open.x, open.y + up);
					yield return CivilTest.Wait(0.4f);
					Check("out at sea off the shore, red (or moved in to it)", !Planning.Valid || Planning.SiteTile != open, $"{Planning.Why} at {Planning.SiteTile?.x},{Planning.SiteTile?.y}");
				}
				WorldTile lake = Lake(_planAt);
				if (lake != null)
				{
					Planning.AimForTest = World.world.GetTile(lake.x, lake.y + up);
					yield return CivilTest.Wait(0.4f);
					Check("on a lake, red", !Planning.Valid, $"{Planning.Why} at {lake.x},{lake.y}");
				}
				else
				{
					Note("no lake near: not tried");
				}
				Planning.AimForTest = null;
				Planning.Escape();
			}
		}

		private static bool LandAlong(Vector2 mid, float h, float reach)
		{
			for (float num = 1f; num <= reach; num += 0.5f)
			{
				if (!Liquid(World.world.GetTile(Mathf.FloorToInt(mid.x + Mathf.Cos(h) * num), Mathf.FloorToInt(mid.y + Mathf.Sin(h) * num))))
				{
					return true;
				}
			}
			return false;
		}

		private static void TurnR()
		{
			try
			{
				typeof(Planning).GetMethod("Turn", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, null);
			}
			catch (Exception ex)
			{
				Log.Error("PORTS R: " + ex);
			}
		}

		private static float GhostHeading()
		{
			try
			{
				Vec3 c = ((MeshInstance)typeof(PlanGhosts).GetField("Ghost", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)).Basis.C0;
				return Mathf.Atan2(c.Y, c.X) - MathF.PI / 2f;
			}
			catch
			{
				return float.NaN;
			}
		}

		private static Vector2 SeaWay(WorldTile at, BuildingAsset a)
		{
			HashSet<WorldTile> hashSet = new HashSet<WorldTile>(Blueprints.Footprint(at, a));
			Vector2 zero = Vector2.zero;
			Vector2 zero2 = Vector2.zero;
			foreach (WorldTile item in hashSet)
			{
				zero += new Vector2((float)item.x + 0.5f, (float)item.y + 0.5f);
			}
			zero /= (float)Mathf.Max(1, hashSet.Count);
			foreach (WorldTile item2 in hashSet)
			{
				WorldTile[] neighbours = item2.neighbours;
				foreach (WorldTile worldTile in neighbours)
				{
					if (worldTile != null && !hashSet.Contains(worldTile) && !Liquid(worldTile))
					{
						zero2 += new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f) - zero;
					}
				}
			}
			if (!(zero2.sqrMagnitude > 0f))
			{
				return Vector2.down;
			}
			return -zero2.normalized;
		}

		private static WorldTile Dry(WorldTile near, BuildingAsset a)
		{
			for (int i = 3; i < 20; i++)
			{
				for (int j = 0; j < 16; j++)
				{
					WorldTile tile = World.world.GetTile(near.x + Mathf.RoundToInt(Mathf.Cos((float)j * MathF.PI / 8f) * (float)i), near.y + Mathf.RoundToInt(Mathf.Sin((float)j * MathF.PI / 8f) * (float)i));
					if (tile == null || tile.zone == null || tile.zone.city != Planning.Town)
					{
						continue;
					}
					bool flag = true;
					foreach (WorldTile item in Blueprints.Footprint(tile, a))
					{
						if (item == null || Liquid(item))
						{
							flag = false;
						}
					}
					if (flag)
					{
						return tile;
					}
				}
			}
			return null;
		}

		private static WorldTile OpenSea(WorldTile near, BuildingAsset a, City town)
		{
			for (int i = 4; i < 30; i++)
			{
				for (int j = 0; j < 24; j++)
				{
					WorldTile tile = World.world.GetTile(near.x + Mathf.RoundToInt(Mathf.Cos((float)j * MathF.PI / 12f) * (float)i), near.y + Mathf.RoundToInt(Mathf.Sin((float)j * MathF.PI / 12f) * (float)i));
					if (tile == null || tile.zone == null || (tile.zone.city != town && tile.zone.city != null))
					{
						continue;
					}
					bool flag = true;
					foreach (WorldTile item in Blueprints.Footprint(tile, a))
					{
						if (item == null || !Liquid(item))
						{
							flag = false;
							break;
						}
						WorldTile[] neighbours = item.neighbours;
						foreach (WorldTile worldTile in neighbours)
						{
							if (worldTile == null || !Liquid(worldTile))
							{
								flag = false;
							}
						}
					}
					if (flag)
					{
						return tile;
					}
				}
			}
			return null;
		}

		private static WorldTile Lake(WorldTile near)
		{
			for (int i = 4; i < 60; i += 2)
			{
				for (int j = 0; j < 32; j++)
				{
					WorldTile tile = World.world.GetTile(near.x + Mathf.RoundToInt(Mathf.Cos((float)j * MathF.PI / 16f) * (float)i), near.y + Mathf.RoundToInt(Mathf.Sin((float)j * MathF.PI / 16f) * (float)i));
					try
					{
						if (tile == null || tile.Type == null || !tile.Type.ocean || tile.region?.island == null || tile.region.island.goodForDocks())
						{
							continue;
						}
						WorldTile[] neighbours = tile.neighbours;
						foreach (WorldTile worldTile in neighbours)
						{
							if (worldTile != null && !Liquid(worldTile))
							{
								return tile;
							}
						}
					}
					catch
					{
					}
				}
			}
			return null;
		}

		private static bool Liquid(WorldTile t)
		{
			try
			{
				return t != null && t.Type != null && t.Type.liquid && !t.Type.lava;
			}
			catch
			{
				return false;
			}
		}

		private static int DeckOf(Building b)
		{
			for (int i = 0; i < DockDecks.Count; i++)
			{
				if (DockDecks.BuildingForTest(i) == b)
				{
					return i;
				}
			}
			return -1;
		}

		private static Vector2 Middle(Building d)
		{
			WorldTile current_tile = d.current_tile;
			BuildingFundament fundament = d.asset.fundament;
			return new Vector2((float)current_tile.x + 0.5f, (float)current_tile.y + 0.5f + ((fundament != null) ? ((float)(fundament.top - fundament.bottom) * 0.5f) : 0f));
		}

		private void Look(Vector2 p)
		{
			Mod.DebugEye = p + new Vector2(-4f, -6f);
			Mod.DebugEyeHeight = 12f;
			Mod.DebugEyeSet = true;
			Mod.SetYaw(Mathf.Atan2(6f, 4f));
			Mod.SetPitchDegrees(-55f);
		}

		private void Frame(string name)
		{
			Mod.SaveFrame(name + ".png");
			try
			{
				if (!Mod.HudForTest.CapturePending)
				{
					Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, name + "_hd.png"));
				}
			}
			catch
			{
			}
		}

		private IEnumerator Shot(string name)
		{
			yield return CivilTest.Wait(0.4f);
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, name));
			for (int i = 0; i < 20; i++)
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
