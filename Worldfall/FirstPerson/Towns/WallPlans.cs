using System;
using System.Collections.Generic;
using System.Text;
using FirstPerson.Core;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson.Towns
{
	internal static class WallPlans
	{
		internal struct Plan
		{
			public int X;

			public int Y;

			public int Kind;
		}

		private sealed class Parsed
		{
			public string Raw;

			public readonly List<Plan> List = new List<Plan>();
		}

		private sealed class Job
		{
			public City City;

			public int X;

			public int Y;

			public int Kind;

			public float Since;
		}

		public const string Key = "fp_walls";

		public const string TaskId = "fp_raise_wall";

		public const int Kinds = 3;

		public static readonly string[] Names = new string[3] { "Palisade", "Stone wall", "Iron wall" };

		private static readonly string[] Res = new string[3] { "wood", "stone", "common_metals" };

		private static readonly string[] ResName = new string[3] { "wood", "stone", "metal" };

		private const float WorkNeed = 6f;

		private const int MostPlans = 240;

		private const int Workers = 2;

		private const float Reach = 2.6f;

		private static readonly Dictionary<City, Parsed> Cache = new Dictionary<City, Parsed>();

		private static readonly List<Plan> Empty = new List<Plan>();

		internal static int Raised;

		private static readonly Dictionary<long, float> _work = new Dictionary<long, float>();

		private static float _blowAt;

		private static float _needsAt = -9f;

		private static readonly Dictionary<long, Job> _jobs = new Dictionary<long, Job>();

		private static float _tick;

		private static readonly List<long> Drop = new List<long>();

		private static bool _registered;

		internal static List<Plan> Of(City c)
		{
			if (c == null)
			{
				return Empty;
			}
			string pResult = null;
			try
			{
				c.data.get("fp_walls", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return Empty;
			}
			if (!Cache.TryGetValue(c, out var value))
			{
				if (Cache.Count > 64)
				{
					Cache.Clear();
				}
				value = (Cache[c] = new Parsed());
			}
			if (value.Raw == pResult)
			{
				return value.List;
			}
			value.Raw = pResult;
			value.List.Clear();
			string[] array = pResult.Split(';');
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(':');
				if (array2.Length == 3 && int.TryParse(array2[0], out var result) && int.TryParse(array2[1], out var result2) && int.TryParse(array2[2], out var result3))
				{
					value.List.Add(new Plan
					{
						X = result,
						Y = result2,
						Kind = Mathf.Clamp(result3, 0, 2)
					});
				}
			}
			return value.List;
		}

		private static void Write(City c, List<Plan> list)
		{
			try
			{
				if (list.Count == 0)
				{
					c.data.removeString("fp_walls");
					return;
				}
				StringBuilder stringBuilder = new StringBuilder(list.Count * 10);
				for (int i = 0; i < list.Count; i++)
				{
					if (i > 0)
					{
						stringBuilder.Append(';');
					}
					stringBuilder.Append(list[i].X).Append(':').Append(list[i].Y)
						.Append(':')
						.Append(list[i].Kind);
				}
				c.data.set("fp_walls", stringBuilder.ToString());
			}
			catch (Exception e)
			{
				Hub.Fail("walls write", e);
			}
		}

		internal static bool Pending(City c, WorldTile t, out int kind)
		{
			kind = 0;
			if (c == null || t == null)
			{
				return false;
			}
			foreach (Plan item in Of(c))
			{
				if (item.X == t.x && item.Y == t.y)
				{
					kind = item.Kind;
					return true;
				}
			}
			return false;
		}

		internal static string WhyNotKind(City c, int kind)
		{
			if (c == null)
			{
				return "No town";
			}
			int num = -1;
			try
			{
				num = Village.HallLevel(c);
			}
			catch
			{
			}
			if (kind == 1 && num < 1)
			{
				return "Needs a town hall";
			}
			if (kind == 2 && num < 2)
			{
				return "Needs a city hall";
			}
			return null;
		}

		internal static string WhyNotAt(City c, WorldTile t, bool planned = false)
		{
			try
			{
				if (t == null || t.Type == null)
				{
					return "Look at the ground";
				}
				if (t.Type.liquid)
				{
					return "Not on water";
				}
				if (t.Type.road)
				{
					return "A road: left open as a gate";
				}
				if (t.top_type != null && t.top_type.wall)
				{
					return "A wall stands here";
				}
				if (!t.Type.ground || t.Type.block)
				{
					return "Can't build here";
				}
				if (t.zone == null || t.zone.city != c)
				{
					return "Not your town's land";
				}
				if (t.building != null && AssetStyle.IsSolid(t.building))
				{
					return "Something stands here";
				}
				if (!planned && Pending(c, t, out var _))
				{
					return "Laid out already";
				}
			}
			catch
			{
				return "Can't build here";
			}
			return null;
		}

		internal static int Add(City c, List<WorldTile> tiles, int kind)
		{
			List<Plan> list = new List<Plan>(Of(c));
			int num = 0;
			foreach (WorldTile tile in tiles)
			{
				if (list.Count >= 240)
				{
					break;
				}
				if (WhyNotAt(c, tile) == null)
				{
					list.Add(new Plan
					{
						X = tile.x,
						Y = tile.y,
						Kind = kind
					});
					num++;
				}
			}
			if (num > 0)
			{
				Write(c, list);
			}
			Log.Info($"walls: {num} {Names[kind].ToLowerInvariant()} plans laid out in {Rep.NameOf(c)} ({list.Count} waiting)");
			return num;
		}

		internal static bool Remove(City c, WorldTile t)
		{
			List<Plan> list = new List<Plan>(Of(c));
			int num = list.FindIndex((Plan p) => p.X == t.x && p.Y == t.y);
			if (num < 0)
			{
				return false;
			}
			list.RemoveAt(num);
			Write(c, list);
			return true;
		}

		internal static string Cost(int kind, int n)
		{
			return n + " " + ResName[Mathf.Clamp(kind, 0, 2)];
		}

		private static bool Raise(City c, WorldTile t, int kind, Actor payer, out string missing)
		{
			missing = null;
			string text = WhyNotAt(c, t, planned: true);
			if (text != null)
			{
				Remove(c, t);
				_work.Remove(Index(t));
				Log.Info($"walls: the plan at {t.x},{t.y} dropped ({text})");
				missing = text;
				return false;
			}
			string text2 = Res[kind];
			int num = 0;
			try
			{
				num = (c.hasStorages() ? c.getResourcesAmount(text2) : 0);
			}
			catch
			{
			}
			bool flag = num < 1;
			if (flag && (payer == null || Civil.BagCount(payer, text2) < 1))
			{
				missing = "1 " + ResName[kind];
				return false;
			}
			TopTileType topTileType;
			switch (kind)
			{
			default:
				topTileType = TopTileLibrary.wall_iron;
				break;
			case 1:
				topTileType = TopTileLibrary.wall_order;
				break;
			case 0:
				topTileType = TopTileLibrary.wall_wild;
				break;
			}
			TopTileType pTopType = topTileType;
			try
			{
				MapAction.terraformTop(t, pTopType, TerraformLibrary.draw);
			}
			catch (Exception e)
			{
				Hub.Fail("raise wall", e);
				return false;
			}
			try
			{
				if (flag)
				{
					Civil.TakeFromBag(payer, text2, 1);
				}
				else
				{
					c.takeResource(text2, 1);
				}
			}
			catch (Exception e2)
			{
				Hub.Fail("wall pay", e2);
			}
			Remove(c, t);
			_work.Remove(Index(t));
			Raised++;
			Sounds.PlayAt((kind == 0) ? "event:/SFX/HIT/HitWood" : "event:/SFX/HIT/HitStone", new Vector2(t.posV3.x + 0.5f, t.posV3.y + 0.5f));
			return true;
		}

		private static long Index(WorldTile t)
		{
			return ((long)t.x << 20) | (uint)t.y;
		}

		public static bool HandWork(Work w, Actor host, Vector2 you, bool mouseFree, bool holstered, CombatFeedback feedback, Action<string> toast)
		{
			WorldTile tile;
			City city;
			int kind;
			try
			{
				if (!FirstPersonAim.Ready(host) || !FirstPersonAim.Point(out var point, out var _))
				{
					return false;
				}
				if ((new Vector2(point.X, point.Y) - you).magnitude > 2.6f)
				{
					return false;
				}
				tile = World.world.GetTile(Mathf.FloorToInt(point.X), Mathf.FloorToInt(point.Y));
				city = tile?.zone?.city;
				if (city == null || !Pending(city, tile, out kind) || !Rep.Rules(host, city))
				{
					return false;
				}
			}
			catch
			{
				return false;
			}
			float num = Blueprints.ToolFactor(Work.ToolInHand(host, holstered));
			if (num <= 0f)
			{
				w.Needs = "Raise walls with a hammer or bare hands|Hold Q:Put it away";
				return true;
			}
			long key = Index(tile);
			_work.TryGetValue(key, out var value);
			w.Needs = "Hold LMB:Raise the " + Names[kind].ToLowerInvariant() + " (" + Cost(kind, 1) + ")";
			w.Progress = value / 6f;
			if (!mouseFree || !PlayInput.GetMouseButton(0))
			{
				return true;
			}
			float unscaledTime = Time.unscaledTime;
			if (unscaledTime - _blowAt < 0.55f)
			{
				return true;
			}
			_blowAt = unscaledTime;
			feedback.WorkSwing(Swing.Mine);
			value += 8f * num;
			if (value < 6f)
			{
				_work[key] = value;
				Sounds.PlayAt((kind == 0) ? "event:/SFX/HIT/HitWood" : "event:/SFX/HIT/HitStone", new Vector2(tile.posV3.x + 0.5f, tile.posV3.y + 0.5f));
				return true;
			}
			if (!Raise(city, tile, kind, host, out var missing))
			{
				if (!Pending(city, tile, out var _))
				{
					toast?.Invoke("No wall here now: " + missing);
				}
				else
				{
					_work[key] = 6f;
					if (unscaledTime - _needsAt > 2.5f)
					{
						_needsAt = unscaledTime;
						toast?.Invoke("Needs " + missing + ", in the stockpile or your bag");
					}
				}
			}
			return true;
		}

		public static void Tick(float dt)
		{
			if ((_tick -= dt) > 0f)
			{
				return;
			}
			_tick = 1f;
			try
			{
				Drop.Clear();
				foreach (KeyValuePair<long, Job> job in _jobs)
				{
					Actor actor = Village.ActorById(job.Key);
					if (actor == null || !actor.isAlive() || actor.ai == null || ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)actor.ai).task == null || !(((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)actor.ai).task.id == "fp_raise_wall") || !(Time.unscaledTime - job.Value.Since < 45f))
					{
						Drop.Add(job.Key);
					}
				}
				foreach (long item in Drop)
				{
					_jobs.Remove(item);
				}
				foreach (City item2 in World.world.cities.list)
				{
					if (item2 != null && item2.isAlive())
					{
						List<Plan> list = Of(item2);
						if (list.Count != 0 && !item2.hasBuildingToBuild() && BuildHelp.YourSite?.city != item2)
						{
							Send(item2, list);
						}
					}
				}
			}
			catch (Exception e)
			{
				Hub.Fail("walls tick", e);
			}
		}

		private static void Send(City c, List<Plan> plans)
		{
			int num = 0;
			foreach (Job value in _jobs.Values)
			{
				if (value.City == c)
				{
					num++;
				}
			}
			if (num >= 2 || c.units == null)
			{
				return;
			}
			Register();
			foreach (Actor unit in c.units)
			{
				if (num >= 2)
				{
					break;
				}
				try
				{
					if (unit == null || !unit.isAlive() || _jobs.ContainsKey(unit.data.id) || !unit.isAdult() || unit.isKing() || unit.isWarrior() || unit.army != null || unit.has_attack_target || unit.is_inside_boat || unit.isInsideSomething() || ControllableUnit.isControllingUnit(unit))
					{
						continue;
					}
					Plan plan = default(Plan);
					float num2 = float.MaxValue;
					foreach (Plan plan2 in plans)
					{
						bool flag = false;
						foreach (Job value2 in _jobs.Values)
						{
							if (value2.City == c && value2.X == plan2.X && value2.Y == plan2.Y)
							{
								flag = true;
								break;
							}
						}
						if (!flag)
						{
							float sqrMagnitude = (new Vector2(plan2.X, plan2.Y) - unit.current_position).sqrMagnitude;
							if (sqrMagnitude < num2)
							{
								num2 = sqrMagnitude;
								plan = plan2;
							}
						}
					}
					if (num2 == float.MaxValue || num2 > 3600f)
					{
						break;
					}
					_jobs[unit.data.id] = new Job
					{
						City = c,
						X = plan.X,
						Y = plan.Y,
						Kind = plan.Kind,
						Since = Time.unscaledTime
					};
					unit.setTask("fp_raise_wall");
					if (unit.ai == null || ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)unit.ai).task == null || ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)unit.ai).task.id != "fp_raise_wall")
					{
						_jobs.Remove(unit.data.id);
						continue;
					}
					num++;
					Log.Info($"walls: {WorldBoxMod.Describe(unit)} goes to raise the {Names[plan.Kind].ToLowerInvariant()} at {plan.X},{plan.Y}");
				}
				catch (Exception e)
				{
					Hub.Fail("walls send", e);
					break;
				}
			}
		}

		internal static bool JobOf(Actor a, out City c, out WorldTile t, out int kind)
		{
			c = null;
			t = null;
			kind = 0;
			if (a == null || !_jobs.TryGetValue(a.data.id, out var value))
			{
				return false;
			}
			c = value.City;
			t = World.world.GetTile(value.X, value.Y);
			kind = value.Kind;
			int kind2;
			if (t != null)
			{
				return Pending(c, t, out kind2);
			}
			return false;
		}

		internal static WorldTile StandBy(City c, WorldTile t)
		{
			WorldTile result = null;
			float num = float.MaxValue;
			Vector2 vector = t.posV3;
			try
			{
				WorldTile tile = c.getTile();
				if (tile != null)
				{
					vector = tile.posV3;
				}
			}
			catch
			{
			}
			for (int i = 0; i < 4; i++)
			{
				MapBox world = World.world;
				int x = t.x;
				int num2;
				switch (i)
				{
				default:
					num2 = 0;
					break;
				case 1:
					num2 = -1;
					break;
				case 0:
					num2 = 1;
					break;
				}
				int pX = x + num2;
				int y = t.y;
				int num3;
				switch (i)
				{
				default:
					num3 = 0;
					break;
				case 3:
					num3 = -1;
					break;
				case 2:
					num3 = 1;
					break;
				}
				WorldTile tile2 = world.GetTile(pX, y + num3);
				if (tile2 != null && tile2.Type != null && !tile2.Type.liquid && !tile2.Type.block && (tile2.top_type == null || !tile2.top_type.wall) && !Pending(c, tile2, out var _))
				{
					float sqrMagnitude = ((Vector2)tile2.posV3 - vector).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						result = tile2;
					}
				}
			}
			return result;
		}

		internal static void Done(Actor a)
		{
			if (a == null || !_jobs.TryGetValue(a.data.id, out var value))
			{
				return;
			}
			_jobs.Remove(a.data.id);
			WorldTile tile = World.world.GetTile(value.X, value.Y);
			if (tile != null && Pending(value.City, tile, out var kind) && !((a.current_position - new Vector2(tile.posV3.x + 0.5f, tile.posV3.y + 0.5f)).magnitude > 2.5f))
			{
				if (Raise(value.City, tile, kind, null, out var missing))
				{
					Log.Info($"walls: {WorldBoxMod.Describe(a)} raised the {Names[kind].ToLowerInvariant()} at {tile.x},{tile.y}");
				}
				else
				{
					Log.Info($"walls: the {Names[kind].ToLowerInvariant()} at {tile.x},{tile.y} waits for {missing}");
				}
			}
		}

		private static void Register()
		{
			if (_registered)
			{
				return;
			}
			_registered = true;
			try
			{
				if (AssetManager.tasks_actor.get("fp_raise_wall") == null)
				{
					BehaviourTaskActor behaviourTaskActor = new BehaviourTaskActor
					{
						id = "fp_raise_wall",
						locale_key = "task_unit_build",
						cancellable_by_reproduction = false,
						cancellable_by_socialize = false
					};
					behaviourTaskActor.setIcon("ui/Icons/iconCitizenJobBuilder");
					BehaviourActionActor[] array = new BehaviourActionActor[3]
					{
						new BehWallStand(),
						new BehGoToTileTarget(),
						new BehRaiseWall()
					};
					foreach (BehaviourActionActor behaviourActionActor in array)
					{
						((BehaviourActionBase<Actor>)behaviourActionActor).setupErrorChecks();
						behaviourTaskActor.addBeh(behaviourActionActor);
					}
					AssetManager.tasks_actor.add(behaviourTaskActor);
				}
			}
			catch (Exception e)
			{
				Hub.Fail("walls task register", e);
			}
		}
	}
}
