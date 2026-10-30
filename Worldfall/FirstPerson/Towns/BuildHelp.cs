using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class BuildHelp
	{
		private struct Handed
		{
			public Building Was;

			public Building Mine;

			public long WasId;
		}

		private const float RecruitEvery = 4f;

		private const float YoursFor = 15f;

		private const float Near = 28f;

		private const int Builders = 3;

		private const string BuildTask = "try_build_building";

		private static Building _site;

		private static long _siteId;

		private static object _world;

		private static float _workedAt = -99f;

		private static float _recruitAt;

		private static readonly List<Actor> _helpers = new List<Actor>();

		private static readonly Dictionary<City, Handed> _before = new Dictionary<City, Handed>();

		private static readonly List<City> Done = new List<City>();

		private static readonly List<Actor> Scratch = new List<Actor>();

		internal static int Sent;

		public static Building YourSite
		{
			get
			{
				if (_site == null || !(Time.unscaledTime - _workedAt <= 15f) || !Same(_site, _siteId))
				{
					return null;
				}
				return _site;
			}
		}

		internal static IReadOnlyList<Actor> Helpers => _helpers;

		private static bool Same(Building b, long id)
		{
			try
			{
				return b != null && b.isAlive() && b.data != null && b.data.id == id;
			}
			catch
			{
				return false;
			}
		}

		public static void Worked(Actor you, Building b)
		{
			if (b == null || you == null)
			{
				return;
			}
			try
			{
				City city = b.city;
				if (city == null || !city.isAlive() || !b.isUnderConstruction())
				{
					return;
				}
				float unscaledTime = Time.unscaledTime;
				if (_site != b)
				{
					_helpers.Clear();
					_recruitAt = 0f;
				}
				_site = b;
				_siteId = b.data.id;
				_world = World.world.map_stats;
				_workedAt = unscaledTime;
				Building under_construction_building = city.under_construction_building;
				if (under_construction_building != b)
				{
					if (_before.TryGetValue(city, out var value))
					{
						value.Mine = b;
						_before[city] = value;
					}
					else if (under_construction_building != null && under_construction_building.isAlive() && under_construction_building.isUnderConstruction())
					{
						_before[city] = new Handed
						{
							Was = under_construction_building,
							Mine = b,
							WasId = under_construction_building.data.id
						};
					}
					city.under_construction_building = b;
					Log.Info("build help: " + Rep.NameOf(city) + "'s builders come to the " + b.asset.id);
				}
				if (unscaledTime >= _recruitAt)
				{
					_recruitAt = unscaledTime + 4f;
					Recruit(you, b, city);
				}
			}
			catch (Exception e)
			{
				Hub.Fail("build help", e);
			}
		}

		public static void Tick()
		{
			if (_before.Count == 0 && _site == null)
			{
				return;
			}
			object obj = ((World.world != null) ? World.world.map_stats : null);
			if (obj != _world)
			{
				_before.Clear();
				_site = null;
				_helpers.Clear();
				_world = obj;
				return;
			}
			float unscaledTime = Time.unscaledTime;
			if (_site != null && (!Alive(_site) || !_site.isUnderConstruction() || unscaledTime - _workedAt > 15f))
			{
				_site = null;
				_helpers.Clear();
			}
			if (_before.Count == 0)
			{
				return;
			}
			Scratch.Clear();
			Done.Clear();
			foreach (KeyValuePair<City, Handed> item in _before)
			{
				City key = item.Key;
				try
				{
					if (key == null || !key.isAlive())
					{
						Done.Add(key);
						continue;
					}
					Building under_construction_building = key.under_construction_building;
					if (under_construction_building != null && under_construction_building == _site)
					{
						continue;
					}
					Building was = item.Value.Was;
					if ((under_construction_building == null || !Alive(under_construction_building) || !under_construction_building.isUnderConstruction() || under_construction_building == item.Value.Mine) && Same(was, item.Value.WasId) && was.city == key && was.isUnderConstruction())
					{
						key.under_construction_building = was;
					}
					goto IL_0181;
				}
				catch
				{
					goto IL_0181;
				}
				IL_0181:
				Done.Add(key);
			}
			foreach (City item2 in Done)
			{
				_before.Remove(item2);
			}
		}

		private static bool Alive(Building b)
		{
			try
			{
				return b.isAlive();
			}
			catch
			{
				return false;
			}
		}

		private static void Recruit(Actor you, Building b, City c)
		{
			_helpers.RemoveAll((Actor a) => a == null || !a.isAlive());
			if (c.units == null)
			{
				return;
			}
			int num = 0;
			foreach (Actor unit in c.units)
			{
				try
				{
					if (unit != null && unit.isAlive() && unit.citizen_job != null && unit.citizen_job.id == "builder")
					{
						num++;
					}
				}
				catch
				{
				}
			}
			int num2 = 3 - num - _helpers.Count;
			if (num2 <= 0)
			{
				return;
			}
			Vector2 at = b.current_position;
			Scratch.Clear();
			foreach (Actor unit2 in c.units)
			{
				if (unit2 == null || unit2 == you || !unit2.isAlive() || _helpers.Contains(unit2))
				{
					continue;
				}
				try
				{
					if (unit2.isAdult() && !unit2.isKing() && !unit2.isCityLeader() && !unit2.isWarrior() && unit2.army == null && !unit2.has_attack_target && !unit2.is_inside_boat && !unit2.isInsideSomething() && (unit2.citizen_job == null || !(unit2.citizen_job.id == "builder")) && !((unit2.current_position - at).sqrMagnitude > 784f) && (unit2.data == null || unit2.data.id != you.data.id))
					{
						Scratch.Add(unit2);
					}
				}
				catch
				{
				}
			}
			Scratch.Sort((Actor x, Actor y) => (x.current_position - at).sqrMagnitude.CompareTo((y.current_position - at).sqrMagnitude));
			for (int num3 = 0; num3 < Scratch.Count; num3++)
			{
				if (num2 <= 0)
				{
					break;
				}
				Actor actor = Scratch[num3];
				try
				{
					actor.setTask("try_build_building");
					_helpers.Add(actor);
					num2--;
					Sent++;
				}
				catch (Exception e)
				{
					Hub.Fail("build help task", e);
					return;
				}
			}
			if (Scratch.Count > 0)
			{
				Log.Info($"build help: {_helpers.Count} of {Rep.NameOf(c)} help with the {b.asset.id}");
			}
		}
	}
}
