using System;
using System.Collections.Generic;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson.Towns
{
	internal static class Haul
	{
		private sealed class Trip
		{
			public long Who;

			public long Site;

			public long City;

			public int Res;

			public int N;

			public bool Loaded;

			public WorldTile From;

			public WorldTile To;

			public float Since;
		}

		private sealed class BehCarry : BehaviourActionActor
		{
			public override BehResult execute(Actor a)
			{
				if (!Trips.TryGetValue(a.data.id, out var value))
				{
					return BehResult.Stop;
				}
				City city = Village.CityById(value.City);
				Building building = null;
				try
				{
					building = World.world.buildings.get(value.Site);
				}
				catch
				{
				}
				if (city == null || building == null || !building.isAlive() || !building.isUnderConstruction() || Blueprints.IsPaid(building))
				{
					Trips.Remove(a.data.id);
					if (value.Loaded)
					{
						Back(city, value);
					}
					LetGo(a.data.id);
					return BehResult.Stop;
				}
				WorldTile worldTile = (value.Loaded ? value.To : value.From);
				float magnitude = (a.current_position - new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f)).magnitude;
				if (!value.Loaded && magnitude <= 2.2f)
				{
					int num = 0;
					try
					{
						num = Mathf.Min(value.N, city.getResourcesAmount(Res[value.Res]));
						if (num > 0)
						{
							city.takeResource(Res[value.Res], num);
						}
					}
					catch
					{
						num = 0;
					}
					if (num <= 0)
					{
						Trips.Remove(a.data.id);
						return BehResult.Stop;
					}
					value.N = num;
					value.Loaded = true;
					Holding(a, value);
					a.beh_tile_target = value.To;
					worldTile = value.To;
				}
				else if (value.Loaded && Blueprints.DistanceTo(building, a.current_position) <= 2.2f)
				{
					int num2 = Mathf.Clamp(Needed(building, value.Res) - Have(building)[value.Res], 0, value.N);
					if (num2 > 0)
					{
						Add(building, value.Res, num2);
					}
					if (num2 < value.N)
					{
						int n = value.N;
						value.N -= num2;
						Back(city, value);
						value.N = n;
					}
					Delivered++;
					Trips.Remove(a.data.id);
					LetGo(a.data.id);
					Blueprints.CheckStocked(building);
					return BehResult.Stop;
				}
				try
				{
					if (!a.is_moving || a.beh_tile_target != worldTile)
					{
						a.beh_tile_target = worldTile;
						a.goTo(worldTile);
					}
				}
				catch
				{
				}
				a.timer_action = 0.6f;
				return BehResult.RepeatStep;
			}

			private static void Back(City c, Trip t)
			{
				if (Economy.HasStores(c))
				{
					try
					{
						Growth.Ignore(c, Res[t.Res], t.N);
						c.addResourcesToRandomStockpile(Res[t.Res], t.N);
					}
					catch
					{
					}
				}
			}
		}

		public const string HaveKey = "fp_have";

		public const string Task = "fp_haul";

		public const int Load = 5;

		private const int Carriers = 3;

		private const int PlansAtOnce = 2;

		private const float Every = 1f;

		private const float Reach = 2.2f;

		private const float Near = 40f;

		internal static readonly string[] Res = new string[4] { "wood", "stone", "common_metals", "gold" };

		private static readonly Dictionary<long, Trip> Trips = new Dictionary<long, Trip>();

		private static readonly List<long> Drop = new List<long>();

		private static readonly List<Actor> Scratch = new List<Actor>();

		private static float _next;

		private static bool _registered;

		private static object _world;

		internal static int Delivered;

		private const string LoadKey = "fp_haul_load";

		private static bool _sweep;

		public static int[] Have(Building b)
		{
			int[] array = new int[4];
			if (b == null)
			{
				return array;
			}
			try
			{
				b.data.get("fp_have", out var pResult, null);
				if (string.IsNullOrEmpty(pResult))
				{
					return array;
				}
				string[] array2 = pResult.Split(',');
				for (int i = 0; i < 4 && i < array2.Length; i++)
				{
					int.TryParse(array2[i], out array[i]);
				}
			}
			catch
			{
			}
			return array;
		}

		private static void SetHave(Building b, int[] have)
		{
			try
			{
				if ((have[0] | have[1] | have[2] | have[3]) == 0)
				{
					b.data.removeString("fp_have");
					return;
				}
				b.data.set("fp_have", string.Join(",", Array.ConvertAll(have, (int x) => x.ToString())));
			}
			catch
			{
			}
		}

		internal static void Add(Building b, int res, int n)
		{
			int[] array = Have(b);
			array[res] += n;
			SetHave(b, array);
		}

		internal static void Clear(Building b)
		{
			SetHave(b, new int[4]);
		}

		internal static void Return(City c, Building b, int[] except = null)
		{
			int[] array = Have(b);
			for (int i = 0; i < 4; i++)
			{
				int num = array[i] - ((except != null) ? Mathf.Min(except[i], array[i]) : 0);
				if (num > 0 && Economy.HasStores(c))
				{
					try
					{
						Growth.Ignore(c, Res[i], num);
						c.addResourcesToRandomStockpile(Res[i], num);
					}
					catch
					{
					}
				}
			}
			Clear(b);
		}

		private static int Needed(Building b, int res)
		{
			ConstructionCost cost = b.asset.cost;
			if (cost == null)
			{
				return 0;
			}
			switch (res)
			{
			default:
				return cost.gold;
			case 2:
				return cost.common_metals;
			case 1:
				return cost.stone;
			case 0:
				return cost.wood;
			}
		}

		private static int Inbound(Building b, int res)
		{
			int num = 0;
			foreach (Trip value in Trips.Values)
			{
				if (value.Site == b.data.id && value.Res == res)
				{
					num += value.N;
				}
			}
			return num;
		}

		public static bool Stocked(Building b)
		{
			int[] array = Have(b);
			for (int i = 0; i < 4; i++)
			{
				if (array[i] < Needed(b, i))
				{
					return false;
				}
			}
			return true;
		}

		public static string Brought(Building b)
		{
			int[] array = Have(b);
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < 4; i++)
			{
				int num3 = Needed(b, i);
				num2 += num3;
				num += Mathf.Min(num3, array[i]);
			}
			return num + "/" + num2 + " brought";
		}

		public static string Missing(Building b)
		{
			int[] array = Have(b);
			List<string> list = new List<string>(4);
			for (int i = 0; i < 4; i++)
			{
				int num = Needed(b, i) - array[i];
				if (num > 0)
				{
					list.Add(num + " " + ((i == 2) ? "metal" : Res[i]));
				}
			}
			if (list.Count != 0)
			{
				return string.Join(", ", list.ToArray());
			}
			return null;
		}

		public static int Give(Actor you, Building b)
		{
			if (you == null || b == null || !b.isUnderConstruction() || Blueprints.IsPaid(b))
			{
				return 0;
			}
			int[] array = Have(b);
			int num = 0;
			for (int i = 0; i < 4; i++)
			{
				int num2 = Needed(b, i) - array[i];
				if (num2 <= 0)
				{
					continue;
				}
				int b2 = ((Res[i] == "gold") ? Economy.Money(you) : Civil.BagCount(you, Res[i]));
				int num3 = Mathf.Min(num2, b2);
				if (num3 <= 0)
				{
					continue;
				}
				try
				{
					if (Res[i] == "gold")
					{
						you.spendMoney(num3);
					}
					else
					{
						Civil.TakeFromBag(you, Res[i], num3);
					}
				}
				catch (Exception e)
				{
					Hub.Fail("haul give", e);
					continue;
				}
				array[i] += num3;
				Blueprints.AddOwn(b, i, num3, you);
				num += num3;
			}
			if (num > 0)
			{
				SetHave(b, array);
				Blueprints.CheckStocked(b);
				Log.Info($"haul: you gave {num} to the {b.asset.id}");
			}
			return num;
		}

		public static void Tick(float dt)
		{
			object obj = World.world?.map_stats;
			if (obj != _world)
			{
				_world = obj;
				Trips.Clear();
				_sweep = true;
			}
			if ((_next -= dt) > 0f)
			{
				return;
			}
			_next = 1f;
			if (_sweep && !WorldBoxMod.WorldLoading)
			{
				_sweep = false;
				try
				{
					LoadsBack();
				}
				catch (Exception e)
				{
					Hub.Fail("haul loads back", e);
				}
			}
			try
			{
				Tidy();
			}
			catch (Exception e2)
			{
				Hub.Fail("haul tidy", e2);
			}
			foreach (City item in Village.Held)
			{
				try
				{
					Send(item);
				}
				catch (Exception e3)
				{
					Hub.Fail("haul", e3);
				}
			}
		}

		private static void Holding(Actor a, Trip t)
		{
			try
			{
				if (t.Loaded)
				{
					a.data.set("fp_haul_load", t.City + ":" + t.Res + ":" + t.N);
				}
				else
				{
					a.data.removeString("fp_haul_load");
				}
			}
			catch
			{
			}
		}

		private static void LetGo(long who)
		{
			try
			{
				Village.ActorById(who)?.data.removeString("fp_haul_load");
			}
			catch
			{
			}
		}

		private static void LoadsBack()
		{
			int num = 0;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				string pResult = null;
				try
				{
					if (simple == null || !simple.isAlive())
					{
						continue;
					}
					simple.data.get("fp_haul_load", out pResult, null);
					goto IL_0051;
				}
				catch
				{
				}
				continue;
				IL_0051:
				if (string.IsNullOrEmpty(pResult))
				{
					continue;
				}
				try
				{
					simple.data.removeString("fp_haul_load");
				}
				catch
				{
				}
				string[] array = pResult.Split(':');
				if (array.Length < 3 || !long.TryParse(array[0], out var result) || !int.TryParse(array[1], out var result2) || !int.TryParse(array[2], out var result3) || result2 < 0 || result2 >= Res.Length || result3 <= 0)
				{
					continue;
				}
				City city = Village.CityById(result);
				if (Economy.HasStores(city))
				{
					try
					{
						Growth.Ignore(city, Res[result2], result3);
						city.addResourcesToRandomStockpile(Res[result2], result3);
						num++;
					}
					catch
					{
					}
				}
			}
			if (num > 0)
			{
				Log.Info($"haul: {num} load(s) carried at the save put back in the stores");
			}
		}

		private static void Tidy()
		{
			Drop.Clear();
			foreach (KeyValuePair<long, Trip> trip in Trips)
			{
				Trip value = trip.Value;
				Actor actor = Village.ActorById(value.Who);
				bool flag = actor != null && actor.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)actor.ai).task != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)actor.ai).task.id == "fp_haul";
				if (flag && Time.unscaledTime - value.Since < 120f)
				{
					continue;
				}
				if (value.Loaded)
				{
					City city = Village.CityById(value.City);
					if (Economy.HasStores(city))
					{
						try
						{
							Growth.Ignore(city, Res[value.Res], value.N);
							city.addResourcesToRandomStockpile(Res[value.Res], value.N);
						}
						catch
						{
						}
					}
				}
				try
				{
					if (flag)
					{
						actor.cancelAllBeh();
					}
				}
				catch
				{
				}
				LetGo(value.Who);
				Drop.Add(trip.Key);
			}
			foreach (long item in Drop)
			{
				Trips.Remove(item);
			}
		}

		private static int CarriersOf(City c)
		{
			int num = 0;
			foreach (Trip value in Trips.Values)
			{
				if (value.City == c.data.id)
				{
					num++;
				}
			}
			return num;
		}

		private static void Send(City c)
		{
			if (!c.hasStorages() || CarriersOf(c) >= 3)
			{
				return;
			}
			int num = 0;
			foreach (Building item in Blueprints.SitesOf(c))
			{
				if (num >= 2)
				{
					break;
				}
				if (Blueprints.IsPaid(item) || !item.isAlive())
				{
					continue;
				}
				num++;
				for (int i = 0; i < 4; i++)
				{
					int num2 = Needed(item, i) - Have(item)[i] - Inbound(item, i);
					while (num2 > 0 && CarriersOf(c) < 3)
					{
						int num3 = 0;
						try
						{
							num3 = c.getResourcesAmount(Res[i]) - Reserved(c, i);
						}
						catch
						{
						}
						if (num3 <= 0)
						{
							break;
						}
						int num4 = Mathf.Min(5, Mathf.Min(num2, num3));
						Actor actor = Carrier(c, item);
						if (actor == null || !Start(actor, c, item, i, num4))
						{
							return;
						}
						num2 -= num4;
					}
				}
			}
		}

		private static int Reserved(City c, int res)
		{
			int num = 0;
			foreach (Trip value in Trips.Values)
			{
				if (value.City == c.data.id && value.Res == res && !value.Loaded)
				{
					num += value.N;
				}
			}
			return num;
		}

		public static bool NoHands(City c)
		{
			if (c == null)
			{
				return true;
			}
			Actor actor = WorldBoxMod.Instance?.Host;
			try
			{
				foreach (Actor unit in c.units)
				{
					if (unit != null && unit != actor && unit.isAlive() && unit.isAdult() && !unit.isKing() && !unit.isCityLeader() && !unit.isWarrior() && unit.army == null)
					{
						return false;
					}
				}
			}
			catch
			{
			}
			return true;
		}

		private static Actor Carrier(City c, Building b)
		{
			Actor actor = WorldBoxMod.Instance?.Host;
			Vector2 current_position = b.current_position;
			Actor actor2 = null;
			Actor actor3 = null;
			float num = 1600f;
			float num2 = 1600f;
			foreach (Actor unit in c.units)
			{
				try
				{
					if (unit == null || !unit.isAlive() || unit == actor || Trips.ContainsKey(unit.data.id) || !unit.isAdult() || unit.isKing() || unit.isCityLeader() || unit.isWarrior() || unit.army != null || unit.has_attack_target || unit.is_inside_boat || unit.isInsideSomething() || (unit.citizen_job != null && unit.citizen_job.id == "builder"))
					{
						continue;
					}
					float sqrMagnitude = (unit.current_position - current_position).sqrMagnitude;
					if (unit.citizen_job == null)
					{
						if (sqrMagnitude < num)
						{
							num = sqrMagnitude;
							actor2 = unit;
						}
					}
					else if (sqrMagnitude < num2)
					{
						num2 = sqrMagnitude;
						actor3 = unit;
					}
				}
				catch
				{
				}
			}
			return actor2 ?? actor3;
		}

		private static bool Start(Actor a, City c, Building b, int res, int n)
		{
			Register();
			Building building = null;
			try
			{
				building = c.getStorageNear(b.current_tile, pOnlyFood: false);
			}
			catch
			{
			}
			if (building == null || building.current_tile == null)
			{
				return false;
			}
			try
			{
				if (a.is_inside_building)
				{
					a.exitBuilding();
				}
				a.setTask("fp_haul");
				if (((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task == null || ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id != "fp_haul")
				{
					return false;
				}
				a.beh_tile_target = building.current_tile;
			}
			catch (Exception e)
			{
				Hub.Fail("haul task", e);
				return false;
			}
			Trips[a.data.id] = new Trip
			{
				Who = a.data.id,
				Site = b.data.id,
				City = c.data.id,
				Res = res,
				N = n,
				From = building.current_tile,
				To = b.current_tile,
				Since = Time.unscaledTime
			};
			Log.Info($"haul: {WorldBoxMod.Describe(a)} carries {n} {Res[res]} to the {b.asset.id} in {Rep.NameOf(c)}");
			return true;
		}

		private static void Register()
		{
			if (_registered)
			{
				return;
			}
			try
			{
				if (AssetManager.tasks_actor.get("fp_haul") == null)
				{
					BehaviourTaskActor behaviourTaskActor = new BehaviourTaskActor
					{
						id = "fp_haul",
						locale_key = "task_unit_move"
					};
					behaviourTaskActor.setIcon("ui/Icons/iconResources");
					BehCarry behCarry = new BehCarry();
					((BehaviourActionBase<Actor>)behCarry).setupErrorChecks();
					behaviourTaskActor.addBeh(behCarry);
					AssetManager.tasks_actor.add(behaviourTaskActor);
				}
				_registered = true;
			}
			catch (Exception ex)
			{
				Log.Error("could not register the haul task: " + ex);
			}
		}
	}
}
