using System;
using System.Collections.Generic;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal static class HouseOccupancy
	{
		internal sealed class Occupants
		{
			public readonly List<Actor> Inside = new List<Actor>();

			public int Awake;

			public int Frame;
		}

		public enum Doing
		{
			Rest,
			Sleep,
			Work,
			Together
		}

		private const float Every = 1f;

		private static Dictionary<Building, Occupants> _byBuilding = new Dictionary<Building, Occupants>();

		private static Dictionary<Building, Occupants> _next = new Dictionary<Building, Occupants>();

		private static readonly HashSet<Actor> _seen = new HashSet<Actor>();

		private static readonly Dictionary<Building, float> _crossed = new Dictionary<Building, float>();

		private static readonly List<Building> _stale = new List<Building>();

		private static readonly Stack<Occupants> _spare = new Stack<Occupants>();

		private static readonly Occupants None = new Occupants();

		private static int _frame;

		private static int _at;

		private static int _indoors;

		private static object _world;

		private static bool _first = true;

		private static float _scannedAt = -99f;

		private static readonly List<Actor> _units = new List<Actor>();

		internal static int IndoorsCount { get; private set; }

		internal static int SliceForTest { get; private set; }

		public static int Passes => _frame;

		public static Dictionary<Building, Occupants> All => _byBuilding;

		public static void Update(float dt)
		{
			if (World.world == null || World.world.units == null)
			{
				return;
			}
			object map_stats = World.world.map_stats;
			if (map_stats != _world)
			{
				_world = map_stats;
				Forget();
			}
			if (_at != 0 || Begin())
			{
				int count = _units.Count;
				int num = Math.Max(64, (int)Math.Ceiling((float)count * Math.Max(dt, 0.001f) / 1f));
				int num2 = Math.Min(count, _at + num);
				SliceForTest = num2 - _at;
				Look(_units, _at, num2);
				_at = num2;
				if (_at >= count)
				{
					Finish();
				}
			}
		}

		public static void Scan(bool latest = true)
		{
			if (World.world == null || World.world.units == null)
			{
				return;
			}
			float unscaledTime = Time.unscaledTime;
			if (latest || !(unscaledTime - _scannedAt < 0.25f) || World.world.map_stats != _world)
			{
				_scannedAt = unscaledTime;
				if (Begin())
				{
					Look(_units, 0, _units.Count);
					Finish();
				}
			}
		}

		private static bool Begin()
		{
			_units.Clear();
			try
			{
				_units.AddRange(World.world.units.getSimpleList());
			}
			catch
			{
				return false;
			}
			_at = 0;
			_indoors = 0;
			_seen.Clear();
			foreach (Occupants value in _next.Values)
			{
				_spare.Push(value);
			}
			_next.Clear();
			return true;
		}

		private static void Look(List<Actor> units, int from, int to)
		{
			try
			{
				for (int i = from; i < to && i < units.Count; i++)
				{
					Actor actor = units[i];
					if (actor == null || !actor.is_inside_building || !actor.isAlive())
					{
						continue;
					}
					Building inside_building = actor.inside_building;
					if (inside_building != null && _seen.Add(actor))
					{
						if (!_next.TryGetValue(inside_building, out var value))
						{
							value = ((_spare.Count > 0) ? _spare.Pop() : new Occupants());
							value.Inside.Clear();
							value.Awake = 0;
							_next[inside_building] = value;
						}
						value.Inside.Add(actor);
						if (!Asleep(actor))
						{
							value.Awake++;
						}
						_indoors++;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("occupancy: " + ex.Message);
			}
		}

		private static void Finish()
		{
			_frame++;
			float unscaledTime = Time.unscaledTime;
			foreach (KeyValuePair<Building, Occupants> item in _next)
			{
				item.Value.Frame = _frame;
				if (!_first && (!_byBuilding.TryGetValue(item.Key, out var value) || !Same(value.Inside, item.Value.Inside)))
				{
					_crossed[item.Key] = unscaledTime;
				}
			}
			if (!_first)
			{
				foreach (KeyValuePair<Building, Occupants> item2 in _byBuilding)
				{
					if (!_next.ContainsKey(item2.Key))
					{
						_crossed[item2.Key] = unscaledTime;
					}
				}
			}
			_first = false;
			Dictionary<Building, Occupants> byBuilding = _byBuilding;
			_byBuilding = _next;
			_next = byBuilding;
			IndoorsCount = _indoors;
			_at = 0;
			if (_crossed.Count <= 256)
			{
				return;
			}
			_stale.Clear();
			foreach (KeyValuePair<Building, float> item3 in _crossed)
			{
				if (unscaledTime - item3.Value > 10f || item3.Key == null || !item3.Key.isAlive())
				{
					_stale.Add(item3.Key);
				}
			}
			foreach (Building item4 in _stale)
			{
				_crossed.Remove(item4);
			}
		}

		private static bool Same(List<Actor> a, List<Actor> b)
		{
			if (a.Count != b.Count)
			{
				return false;
			}
			for (int i = 0; i < a.Count; i++)
			{
				if (!b.Contains(a[i]))
				{
					return false;
				}
			}
			return true;
		}

		private static void Forget()
		{
			_byBuilding.Clear();
			_next.Clear();
			_crossed.Clear();
			_at = 0;
			_first = true;
		}

		public static Occupants Of(Building b)
		{
			if (b == null || !_byBuilding.TryGetValue(b, out var value))
			{
				return None;
			}
			return value;
		}

		public static float CrossedAt(Building b)
		{
			if (b == null || !_crossed.TryGetValue(b, out var value))
			{
				return -999f;
			}
			return value;
		}

		public static float SinceCrossing(Building b)
		{
			if (b == null || !_crossed.TryGetValue(b, out var value))
			{
				return 999f;
			}
			return Time.unscaledTime - value;
		}

		public static int Count(Building b)
		{
			return Of(b).Inside.Count;
		}

		public static bool Lively(Building b)
		{
			return Of(b).Awake > 0;
		}

		public static bool Asleep(Actor a)
		{
			try
			{
				return ((BaseSimObject)a).hasStatus("sleeping");
			}
			catch
			{
				return false;
			}
		}

		public static float StayLeft(Actor a)
		{
			try
			{
				if (a == null || !a.is_inside_building)
				{
					return -1f;
				}
				string text = ((a.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task != null) ? ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id : null);
				if (Asleep(a) || text == "sleep_inside")
				{
					return 999f;
				}
				return Mathf.Max(0f, a.timer_action);
			}
			catch
			{
				return 999f;
			}
		}

		public static Doing DoingOf(Actor a)
		{
			try
			{
				if (Asleep(a))
				{
					return Doing.Sleep;
				}
				string text = ((a.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task != null) ? ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id : null);
				if (text == null)
				{
					return Doing.Rest;
				}
				if (text == "sleep_inside" && a.timer_action > 0f)
				{
					return Doing.Sleep;
				}
				if (text == "make_items")
				{
					return Doing.Work;
				}
				if (text.StartsWith("sexual_reproduction", StringComparison.Ordinal))
				{
					return Doing.Together;
				}
				return Doing.Rest;
			}
			catch
			{
				return Doing.Rest;
			}
		}
	}
}
