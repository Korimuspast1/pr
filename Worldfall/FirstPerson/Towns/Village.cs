using System;
using System.Collections.Generic;
using FirstPerson.Net;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson.Towns
{
	internal static class Village
	{
		private sealed class Hold
		{
			public long City;

			public long Ruler;

			public float Seen;

			public bool RulerGone;
		}

		private struct Searched
		{
			public int Zones;

			public int Buildings;

			public float At;
		}

		public const string Key = "fp_village";

		private const float AwayGrace = 300f;

		private const float DeadGrace = 60f;

		private const float HoldTimer = 30f;

		internal static float GraceForTest = -1f;

		private static readonly Dictionary<long, Hold> Holds = new Dictionary<long, Hold>();

		private static readonly List<City> HeldNow = new List<City>();

		private static readonly List<long> Gone = new List<long>();

		private static float _scan;

		private static float _upgrade;

		private static bool _reseed;

		private static object _world;

		private static readonly List<Actor> Players = new List<Actor>();

		internal const string TownHomeKey = "fp_town_home";

		private static readonly Dictionary<long, Searched> NoGround = new Dictionary<long, Searched>();

		private static float _bordersAt = -10f;

		internal static List<City> Held => HeldNow;

		internal static Actor Player
		{
			get
			{
				try
				{
					WorldBoxMod instance = WorldBoxMod.Instance;
					Actor actor = ((instance != null && instance.IsFirstPerson) ? instance.Host : null);
					return (actor != null && actor.isAlive()) ? actor : null;
				}
				catch
				{
					return null;
				}
			}
		}

		public static bool PlansAlone(City c)
		{
			if (c != null)
			{
				if (!HeldNow.Contains(c))
				{
					if (WorldSync.Mirroring && IsVillage(c))
					{
						return Seat(WorldSync.MyBodyActor, c);
					}
					return false;
				}
				return true;
			}
			return false;
		}

		public static bool IsVillage(City c)
		{
			if (c == null)
			{
				return false;
			}
			try
			{
				c.data.get("fp_village", out var pResult, 0L);
				return pResult != 0;
			}
			catch
			{
				return false;
			}
		}

		private static void Mark(City c, Actor founder)
		{
			try
			{
				c.data.set("fp_village", founder.data.id);
			}
			catch (Exception e)
			{
				Hub.Fail("village mark", e);
			}
		}

		public static bool Seat(Actor body, City c)
		{
			try
			{
				if (body == null || c == null || !c.isAlive() || !body.isAlive() || !Rep.Rules(body, c))
				{
					return false;
				}
				if (c.leader == body)
				{
					return true;
				}
				return c.kingdom != null && c.kingdom.king == body && (IsVillage(c) || c.isCapitalCity());
			}
			catch
			{
				return false;
			}
		}

		public static void Tick(float dt)
		{
			object obj = World.world?.map_stats;
			if (obj != _world)
			{
				_world = obj;
				Holds.Clear();
				HeldNow.Clear();
				Blueprints.Reset();
				NoGround.Clear();
				_reseed = true;
			}
			if ((_scan -= dt) <= 0f)
			{
				_scan = 0.5f;
				Scan();
			}
			for (int num = HeldNow.Count - 1; num >= 0; num--)
			{
				City city = HeldNow[num];
				try
				{
					if (city == null || !city.isAlive())
					{
						HeldNow.RemoveAt(num);
					}
					else if (city.timer_build < 30f)
					{
						city.timer_build = 30f;
					}
				}
				catch (Exception e)
				{
					Hub.Fail("village hold", e);
				}
			}
			try
			{
				Blueprints.Tick(dt);
			}
			catch (Exception e2)
			{
				Hub.Fail("blueprints", e2);
			}
			try
			{
				Growth.Tick(dt);
			}
			catch (Exception e3)
			{
				Hub.Fail("growth", e3);
			}
			try
			{
				Haul.Tick(dt);
			}
			catch (Exception e4)
			{
				Hub.Fail("haul", e4);
			}
			try
			{
				Taxes.Tick(dt);
			}
			catch (Exception e5)
			{
				Hub.Fail("taxes", e5);
			}
			if (!((_upgrade -= dt) <= 0f))
			{
				return;
			}
			_upgrade = 5f;
			foreach (City item in HeldNow)
			{
				try
				{
					Paths(item);
				}
				catch (Exception e6)
				{
					Hub.Fail("village paths", e6);
				}
				try
				{
					GrowAHouse(item);
				}
				catch (Exception e7)
				{
					Hub.Fail("village houses", e7);
				}
				try
				{
					Growth.RaiseHall(item);
				}
				catch (Exception e8)
				{
					Hub.Fail("village hall", e8);
				}
				try
				{
					HomesForNewcomers(item);
				}
				catch (Exception e9)
				{
					Hub.Fail("village homes", e9);
				}
			}
		}

		private static void Scan()
		{
			Actor player = Player;
			float unscaledTime = Time.unscaledTime;
			if (_reseed)
			{
				Reseed(unscaledTime);
			}
			Players.Clear();
			if (player != null)
			{
				Players.Add(player);
			}
			WorldSync.RemoteBodies(Players);
			foreach (Actor player2 in Players)
			{
				try
				{
					foreach (City item in World.world.cities.list)
					{
						if (Seat(player2, item) && IsVillage(item))
						{
							long id = item.data.id;
							if (!Holds.TryGetValue(id, out var value))
							{
								Dictionary<long, Hold> holds = Holds;
								Hold obj = new Hold
								{
									City = id
								};
								value = obj;
								holds[id] = obj;
								Log.Info("village: " + Rep.NameOf(item) + " is yours to plan (its own building held)");
							}
							value.Ruler = player2.data.id;
							value.Seen = unscaledTime;
							value.RulerGone = false;
						}
					}
				}
				catch (Exception e)
				{
					Hub.Fail("village scan", e);
				}
			}
			HeldNow.Clear();
			Gone.Clear();
			foreach (KeyValuePair<long, Hold> hold in Holds)
			{
				Hold value2 = hold.Value;
				City city = CityById(value2.City);
				if (city == null)
				{
					Gone.Add(hold.Key);
					continue;
				}
				Actor actor = ActorById(value2.Ruler);
				bool flag = actor != null && Seat(actor, city) && IsVillage(city);
				if (!flag && !value2.RulerGone)
				{
					value2.RulerGone = true;
					value2.Seen = unscaledTime;
				}
				if (flag && Players.Contains(actor))
				{
					value2.Seen = unscaledTime;
				}
				float num = ((GraceForTest >= 0f) ? GraceForTest : (flag ? 300f : 60f));
				if (unscaledTime - value2.Seen <= num)
				{
					HeldNow.Add(city);
					continue;
				}
				Gone.Add(hold.Key);
				try
				{
					city.timer_build = 0f;
				}
				catch
				{
				}
				Log.Info("village: " + Rep.NameOf(city) + " is an ordinary town again (" + (flag ? "you're away from its ruler" : "its ruler is gone") + ")");
			}
			foreach (long item2 in Gone)
			{
				Holds.Remove(item2);
			}
		}

		private static void Reseed(float now)
		{
			try
			{
				if (World.world?.cities == null || World.world.cities.list.Count == 0)
				{
					return;
				}
				_reseed = false;
				foreach (City item in World.world.cities.list)
				{
					if (item != null && item.isAlive() && IsVillage(item) && !Holds.ContainsKey(item.data.id))
					{
						Actor actor = null;
						try
						{
							item.data.get("fp_village", out var pResult, 0L);
							actor = ActorById(pResult);
						}
						catch
						{
						}
						if (actor == null || !Seat(actor, item))
						{
							actor = ((item.leader != null && Seat(item.leader, item)) ? item.leader : ((item.kingdom?.king != null && Seat(item.kingdom.king, item)) ? item.kingdom.king : null));
						}
						if (actor != null)
						{
							Holds[item.data.id] = new Hold
							{
								City = item.data.id,
								Ruler = actor.data.id,
								Seen = now
							};
							Log.Info("village: " + Rep.NameOf(item) + " held again after the load (ruler " + WorldBoxMod.Describe(actor) + ")");
						}
					}
				}
			}
			catch (Exception e)
			{
				Hub.Fail("village reseed", e);
			}
		}

		internal static City CityById(long id)
		{
			try
			{
				City city = World.world.cities.get(id);
				return (city != null && city.isAlive()) ? city : null;
			}
			catch
			{
				return null;
			}
		}

		internal static Actor ActorById(long id)
		{
			try
			{
				Actor actor = World.world.units.get(id);
				return (actor != null && actor.isAlive() && actor.data.id == id) ? actor : null;
			}
			catch
			{
				return null;
			}
		}

		private static void Paths(City c)
		{
			if (c.buildings.Count > 2 && c.hasCulture() && c.culture.canUseRoads())
			{
				Building building = c.buildings[UnityEngine.Random.Range(0, c.buildings.Count)];
				if (building != null && building.isAlive() && !building.isUnderConstruction())
				{
					CityBehBuild.makeRoadsBuildings(c, building);
				}
			}
		}

		private static void HomesForNewcomers(City c)
		{
			if (c.status == null || c.status.housing_free > 1)
			{
				return;
			}
			int num = c.status.homeless;
			Actor player = Player;
			try
			{
				if (player != null && player.city == c && !player.hasHouse())
				{
					num--;
				}
			}
			catch
			{
			}
			if (num <= 0)
			{
				return;
			}
			foreach (Building item in Blueprints.SitesOf(c))
			{
				if (!(item.asset.type != "type_house"))
				{
					item.data.get("fp_town_home", out var pResult, pDefault: false);
					if (pResult)
					{
						return;
					}
				}
			}
			PlanPieces.Piece piece = PlanPieces.OfOrder("order_tent");
			if (piece == null || PlanPieces.WhyNot(piece, c) != null || Blueprints.CountOf(c) >= 12)
			{
				return;
			}
			BuildingAsset a = PlanPieces.AssetOf(piece, c);
			long id = c.data.id;
			int count = c.zones.Count;
			int count2 = c.buildings.Count;
			float unscaledTime = Time.unscaledTime;
			if (NoGround.TryGetValue(id, out var value) && value.Zones == count && value.Buildings == count2 && unscaledTime - value.At < 60f)
			{
				return;
			}
			WorldTile worldTile = Blueprints.NearestFree(c, a);
			if (worldTile == null)
			{
				NoGround[id] = new Searched
				{
					Zones = count,
					Buildings = count2,
					At = unscaledTime
				};
				return;
			}
			NoGround.Remove(id);
			Building building = Blueprints.PlaceSite(c, a, worldTile, null, home: false);
			if (building != null)
			{
				try
				{
					building.data.set("fp_town_home", pData: true);
				}
				catch
				{
				}
				Log.Info($"village: {Rep.NameOf(c)} plans a home for its {num} without a bed");
				if (Hub.Here == c)
				{
					Hub.Toast?.Invoke(Rep.NameOf(c) + " plans a home for newcomers");
				}
			}
		}

		private static void GrowAHouse(City c)
		{
			if (c.hasBuildingToBuild())
			{
				return;
			}
			int num = HallLevel(c);
			bool flag = c.countBuildingsType("type_bonfire") > 0;
			List<Building> buildings = c.buildings;
			int count = buildings.Count;
			if (count == 0)
			{
				return;
			}
			int num2 = UnityEngine.Random.Range(0, count);
			for (int i = 0; i < count; i++)
			{
				Building building = buildings[(num2 + i) % count];
				if (building == null || building.asset == null || !building.isAlive() || building.isRuin() || building.isUnderConstruction() || building.asset.type != "type_house" || string.IsNullOrEmpty(building.asset.upgrade_to))
				{
					continue;
				}
				int num3 = LandPlot.TierOf(building);
				int num4 = ((num3 < 0) ? (-1) : ((num3 != 0) ? ((num3 <= 2) ? 1 : 2) : 0));
				if (((num3 < 0) ? (!flag) : (num < num4)) || num3 + 1 > Growth.HouseCap(c))
				{
					continue;
				}
				BuildingAsset buildingAsset = AssetManager.buildings.get(building.asset.upgrade_to);
				if (buildingAsset != null && c.hasEnoughResourcesFor(buildingAsset.cost))
				{
					string id = building.asset.id;
					if (CityBehBuild.upgradeBuilding(building, c))
					{
						Log.Info("village: " + Rep.NameOf(c) + ": " + id + " grew to " + building.asset.id);
						break;
					}
				}
			}
		}

		internal static int HallLevel(City c)
		{
			int num = -1;
			try
			{
				foreach (Building building in c.buildings)
				{
					if (building != null && building.asset != null && !(building.asset.type != "type_hall") && !building.isUnderConstruction() && !building.isRuin())
					{
						string id = building.asset.id;
						char c2 = id[id.Length - 1];
						num = Mathf.Max(num, (c2 >= '0' && c2 <= '9') ? (c2 - 48) : 0);
					}
				}
			}
			catch
			{
			}
			return num;
		}

		public static bool CanFound(Actor you)
		{
			try
			{
				return you != null && you.isAlive() && you.city == null && you.asset.civ && you.isAdult() && !you.isBaby();
			}
			catch
			{
				return false;
			}
		}

		internal static BuildingAsset BonfireOf(Actor you)
		{
			BuildingAsset buildingAsset = null;
			try
			{
				buildingAsset = you.asset.architecture_asset?.getBuilding("order_bonfire");
			}
			catch
			{
			}
			return buildingAsset ?? AssetManager.buildings.get("bonfire");
		}

		public static string WhyNotFoundAt(Actor you, WorldTile at)
		{
			if (!CanFound(you))
			{
				return "Leave your town first";
			}
			if (at == null || at.zone == null)
			{
				return "Look at open land";
			}
			if (WorldSync.Mirroring)
			{
				MirrorBorders();
			}
			try
			{
				KingdomAsset kingdomAsset = AssetManager.kingdoms.get(you.asset.kingdom_id_civilization);
				if (kingdomAsset == null || !kingdomAsset.civ)
				{
					return "Your people don't build towns";
				}
				if (you.kingdom != null && you.kingdom.asset != null && you.kingdom.asset.is_forced_by_trait)
				{
					return "Your people don't build towns";
				}
				TileZone zone = at.zone;
				if (zone.city != null)
				{
					return "That's " + Rep.NameOf(zone.city) + "'s land";
				}
				TileZone[] neighbours = zone.neighbours;
				foreach (TileZone tileZone in neighbours)
				{
					if (tileZone != null && tileZone.city != null)
					{
						WorldTile tile = tileZone.city.getTile();
						if (tile != null && tile.isSameIsland(at))
						{
							return "Too close to " + Rep.NameOf(tileZone.city);
						}
					}
				}
				if (!zone.isGoodForNewCity(you))
				{
					return "This land won't take a town";
				}
				BuildingAsset buildingAsset = BonfireOf(you);
				if (buildingAsset == null)
				{
					return "Your people don't build towns";
				}
				if (!at.canBuildOn(buildingAsset) || !World.world.buildings.canBuildFrom(at, buildingAsset, (City)null, BuildPlacingType.New, pFloraGrowth: false))
				{
					return "The ground isn't clear";
				}
				foreach (WorldTile item in Blueprints.Footprint(at, buildingAsset))
				{
					TileZone zone2 = item.zone;
					if (zone2 == null)
					{
						return "The ground isn't clear";
					}
					if (zone2 != zone && (zone2.city != null || Array.IndexOf(zone.neighbours_all, zone2) < 0))
					{
						return "Too close to the edge of the land";
					}
				}
			}
			catch (Exception e)
			{
				Hub.Fail("founding check", e);
				return "Not here";
			}
			return null;
		}

		private static void MirrorBorders()
		{
			float unscaledTime = Time.unscaledTime;
			if (unscaledTime - _bordersAt < 2f)
			{
				return;
			}
			_bordersAt = unscaledTime;
			try
			{
				foreach (City item in World.world.cities.list)
				{
					if (item != null && item.isAlive())
					{
						item.recalculateNeighbourZones();
					}
				}
				World.world.city_zone_helper.city_place_finder.setDirty();
			}
			catch (Exception e)
			{
				Hub.Fail("village borders", e);
			}
		}

		public static City Found(Actor you, WorldTile at, out string why)
		{
			why = WhyNotFoundAt(you, at);
			if (why != null)
			{
				return null;
			}
			City city;
			try
			{
				Kingdom kingdom = World.world.kingdoms.makeNewCivKingdom(you);
				city = World.world.cities.buildNewCity(you, at.zone);
				you.joinCity(city);
				city.setUnitMetas(you);
				you.createDefaultCultureAndLanguageAndClan(city.name);
				if (kingdom == null)
				{
					kingdom = you.kingdom;
				}
				kingdom?.setUnitMetas(you);
				city.setUnitMetas(you);
			}
			catch (Exception ex)
			{
				Log.Error("village: founding: " + ex);
				why = "The land won't take a town here";
				return null;
			}
			Mark(city, you);
			Holds[city.data.id] = new Hold
			{
				City = city.data.id,
				Ruler = you.data.id,
				Seen = Time.unscaledTime
			};
			if (!HeldNow.Contains(city))
			{
				HeldNow.Add(city);
			}
			try
			{
				city.timer_build = 30f;
			}
			catch
			{
			}
			Building building = Blueprints.PlaceSite(city, BonfireOf(you), at, you, home: false);
			Log.Info(string.Format("village: {0} founded {1} of {2} at {3},{4}; bonfire site {5}", WorldBoxMod.Describe(you), city.name, city.kingdom?.name, at.x, at.y, (building != null) ? "placed" : "NOT placed"));
			return city;
		}
	}
}
