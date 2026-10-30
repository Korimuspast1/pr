using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class TownPaths
	{
		private sealed class Town
		{
			public long Id;

			public int Signature;

			public float HubX;

			public float HubY;

			public int Strokes;

			public Dictionary<int, byte[]> Wear;

			public bool Seen;
		}

		private sealed class Track
		{
			public long A;

			public long B;

			public Dictionary<int, byte[]> Wear;
		}

		private sealed class Job
		{
			public Town Town;

			public Track Track;

			public TownPlan Plan;

			public PathPoint From;

			public PathPoint To;

			public CostGrid Grid;

			public int[] Slots;

			public volatile bool Done;

			public string Error;
		}

		private const float Range = 160f;

		private const float TrackReach = 46f;

		private const float Recheck = 2.5f;

		private readonly Dictionary<long, Town> _towns = new Dictionary<long, Town>();

		private readonly List<Track> _tracks = new List<Track>();

		private readonly HashSet<int> _blocked = new HashSet<int>();

		private readonly HashSet<int> _rough = new HashSet<int>();

		private readonly HashSet<int> _fields = new HashSet<int>();

		private readonly List<City> _near = new List<City>();

		private readonly List<long> _gone = new List<long>();

		private readonly List<Dictionary<int, byte[]>> _parts = new List<Dictionary<int, byte[]>>();

		private int[] _slots;

		private float _timer;

		private bool _dirty;

		private Job _job;

		public int StrokeCount { get; private set; }

		public int TownCount => _towns.Count;

		public bool HubOf(long cityId, out Vector2 hub)
		{
			hub = Vector2.zero;
			if (!_towns.TryGetValue(cityId, out var value))
			{
				return false;
			}
			hub = new Vector2(value.HubX, value.HubY);
			return true;
		}

		public void Update(TerrainCache cache, Vector2 eye, float dt)
		{
			if (cache == null || cache.Width == 0)
			{
				return;
			}
			if (_slots != cache.PathSlot)
			{
				_slots = cache.PathSlot;
				_towns.Clear();
				_tracks.Clear();
				_timer = 0f;
			}
			if (_job != null && _job.Done)
			{
				Job j = _job;
				_job = null;
				if (j.Error != null)
				{
					Log.Error("town paths: " + j.Error);
				}
				else if (j.Slots == cache.PathSlot)
				{
					if (j.Town != null)
					{
						_towns[j.Town.Id] = j.Town;
						_tracks.RemoveAll((Track k) => k.A == j.Town.Id || k.B == j.Town.Id);
					}
					_dirty = true;
				}
			}
			if (_dirty)
			{
				_parts.Clear();
				int num = 0;
				foreach (Town value in _towns.Values)
				{
					if (value.Wear != null)
					{
						_parts.Add(value.Wear);
					}
					num += value.Strokes;
				}
				foreach (Track track in _tracks)
				{
					if (track.Wear != null)
					{
						_parts.Add(track.Wear);
						num++;
					}
				}
				PathNetwork.ApplyMasks(cache, _parts);
				StrokeCount = num;
				_dirty = false;
			}
			_timer -= dt;
			if (_timer > 0f || _job != null)
			{
				return;
			}
			_timer = 2.5f;
			try
			{
				Step(cache, eye);
			}
			catch (Exception ex)
			{
				Log.Error("town paths: " + ex.Message);
				_timer = 30f;
			}
		}

		private void Step(TerrainCache cache, Vector2 eye)
		{
			_near.Clear();
			foreach (City city2 in World.world.cities)
			{
				if (city2 != null && city2.isAlive() && city2.buildings != null && city2.buildings.Count != 0)
				{
					WorldTile tile = city2.getTile();
					if (tile != null && !((new Vector2(tile.posV3.x, tile.posV3.y) - eye).sqrMagnitude > 25600f))
					{
						_near.Add(city2);
					}
				}
			}
			foreach (Town value2 in _towns.Values)
			{
				value2.Seen = false;
			}
			City city = null;
			int sig = 0;
			foreach (City item in _near)
			{
				long iD = item.getID();
				_towns.TryGetValue(iD, out var value);
				if (value != null)
				{
					value.Seen = true;
				}
				if (city == null)
				{
					int num = Signature(item);
					if (value == null || value.Signature != num)
					{
						city = item;
						sig = num;
					}
				}
			}
			_gone.Clear();
			foreach (KeyValuePair<long, Town> town in _towns)
			{
				if (!town.Value.Seen)
				{
					_gone.Add(town.Key);
				}
			}
			foreach (long id in _gone)
			{
				_towns.Remove(id);
				_tracks.RemoveAll((Track k) => k.A == id || k.B == id);
				_dirty = true;
			}
			if (city != null)
			{
				StartTown(cache, city, sig);
			}
			else
			{
				StartTrack(cache);
			}
		}

		private static int Signature(City c)
		{
			int num = 17;
			WorldTile tile = c.getTile();
			if (tile != null)
			{
				num = num * 31 + tile.x * 7919 + tile.y;
			}
			foreach (Building building in c.buildings)
			{
				if (building != null && building.current_tile != null)
				{
					num = num * 31 + building.current_tile.x * 92821 + building.current_tile.y * 7 + ((building.asset != null) ? building.asset.id.Length : 0);
				}
			}
			return num;
		}

		private void CollectObstacles(TerrainCache cache, int x0, int y0, int x1, int y1)
		{
			_blocked.Clear();
			_rough.Clear();
			_fields.Clear();
			int width = cache.Width;
			List<Building> simpleList = World.world.buildings.getSimpleList();
			for (int i = 0; i < simpleList.Count; i++)
			{
				Building building = simpleList[i];
				if (building == null || building.asset == null)
				{
					continue;
				}
				WorldTile current_tile = building.current_tile;
				if (current_tile == null || current_tile.x < x0 - 3 || current_tile.y < y0 - 3 || current_tile.x > x1 + 3 || current_tile.y > y1 + 3 || !building.isAlive())
				{
					continue;
				}
				BuildingType building_type = building.asset.building_type;
				if (building_type == BuildingType.Building_Plant || building_type == BuildingType.Building_Poops || building_type == BuildingType.Building_Creep)
				{
					continue;
				}
				int num;
				switch (building_type)
				{
				case BuildingType.Building_Wheat:
					_fields.Add(current_tile.y * width + current_tile.x);
					continue;
				default:
					num = ((building_type == BuildingType.Building_Mineral) ? 1 : 0);
					break;
				case BuildingType.Building_Tree:
				case BuildingType.Building_Fruits:
					num = 1;
					break;
				}
				bool flag = (byte)num != 0;
				HashSet<int> hashSet = (flag ? _rough : _blocked);
				if (building.tiles != null && building.tiles.Count > 0 && !flag)
				{
					foreach (WorldTile tile in building.tiles)
					{
						if (tile != null)
						{
							hashSet.Add(tile.y * width + tile.x);
						}
					}
				}
				else
				{
					hashSet.Add(current_tile.y * width + current_tile.x);
				}
			}
		}

		private float Cost(TerrainCache cache, int i)
		{
			if ((cache.Flags[i] & 0xB) != 0)
			{
				return -1f;
			}
			if (_blocked.Contains(i))
			{
				return -1f;
			}
			float num;
			switch (cache.Materials[i])
			{
			case 6:
				num = 0.35f;
				break;
			case 7:
				num = 7f;
				break;
			case 4:
				num = 4f;
				break;
			case 5:
				num = 1.6f;
				break;
			case 2:
				num = 1.2f;
				break;
			default:
				num = 1f;
				break;
			}
			if (_fields.Contains(i))
			{
				num = Math.Max(num, 7f);
			}
			if (_rough.Contains(i))
			{
				num += 3f;
			}
			return num;
		}

		private void StartTown(TerrainCache cache, City c, int sig)
		{
			WorldTile tile = c.getTile();
			if (tile == null)
			{
				return;
			}
			TownPlan townPlan = new TownPlan
			{
				Id = (int)c.getID(),
				HubX = (float)tile.x + 0.5f,
				HubY = (float)tile.y + 0.5f
			};
			Building building = null;
			int width = cache.Width;
			foreach (Building building2 in c.buildings)
			{
				if (building2 != null && building2.asset != null && building2.current_tile != null && building2.isAlive() && (building2.asset.building_type == BuildingType.Building_Civ || Models.BuildingFamily(building2.asset) != null))
				{
					if (building2.asset.type == "type_hall")
					{
						building = building2;
					}
					townPlan.Doors.Add(DoorOf(building2));
				}
			}
			if (building != null)
			{
				PathPoint pathPoint = DoorOf(building);
				townPlan.HubX = pathPoint.X;
				townPlan.HubY = pathPoint.Y - 1.2f;
			}
			PathNetwork.TownRegion(cache, townPlan, out var x, out var y, out var x2, out var y2);
			CollectObstacles(cache, x, y, x2, y2);
			int num = (int)townPlan.HubX;
			int num2 = (int)townPlan.HubY;
			if (num < 0 || num2 < 0 || num >= width || num2 >= cache.Height || Cost(cache, num2 * width + num) < 0f)
			{
				for (int i = 1; i < 6; i++)
				{
					bool flag = false;
					for (int j = -i; j <= i; j++)
					{
						if (flag)
						{
							break;
						}
						for (int k = -i; k <= i; k++)
						{
							if (flag)
							{
								break;
							}
							int num3 = num + k;
							int num4 = num2 + j;
							if (num3 >= 0 && num4 >= 0 && num3 < width && num4 < cache.Height && !(Cost(cache, num4 * width + num3) < 0f))
							{
								townPlan.HubX = (float)num3 + 0.5f;
								townPlan.HubY = (float)num4 + 0.5f;
								flag = true;
							}
						}
					}
					if (flag)
					{
						break;
					}
				}
			}
			townPlan.PlazaRadius = Mathf.Clamp(1.2f + (float)townPlan.Doors.Count * 0.06f, 1.4f, 2.6f);
			Job job = new Job
			{
				Town = new Town
				{
					Id = c.getID(),
					Signature = sig,
					HubX = townPlan.HubX,
					HubY = townPlan.HubY,
					Seen = true
				},
				Plan = townPlan,
				Grid = CostGrid.Take(cache, x, y, x2, y2, (int i2) => Cost(cache, i2)),
				Slots = cache.PathSlot
			};
			Run(job, cache);
		}

		private void StartTrack(TerrainCache cache)
		{
			foreach (Town a in _towns.Values)
			{
				foreach (Town b in _towns.Values)
				{
					if (a.Id >= b.Id || _tracks.Exists((Track k) => k.A == a.Id && k.B == b.Id))
					{
						continue;
					}
					Track track = new Track
					{
						A = a.Id,
						B = b.Id
					};
					_tracks.Add(track);
					float num = a.HubX - b.HubX;
					float num2 = a.HubY - b.HubY;
					if (!(num * num + num2 * num2 > 2116f))
					{
						PathPoint pathPoint = new PathPoint(a.HubX, a.HubY);
						PathPoint pathPoint2 = new PathPoint(b.HubX, b.HubY);
						PathNetwork.TrackRegion(pathPoint, pathPoint2, out var x, out var y, out var x2, out var y2);
						CollectObstacles(cache, x, y, x2, y2);
						Run(new Job
						{
							Track = track,
							From = pathPoint,
							To = pathPoint2,
							Grid = CostGrid.Take(cache, x, y, x2, y2, (int i) => Cost(cache, i)),
							Slots = cache.PathSlot
						}, cache);
						return;
					}
				}
			}
		}

		private void Run(Job job, TerrainCache cache)
		{
			_job = job;
			SlowLane.Run(delegate
			{
				try
				{
					CostGrid grid = job.Grid;
					if (job.Town != null)
					{
						List<PathStroke> list = PathNetwork.PlanTown(cache, job.Plan, grid.At);
						job.Town.Strokes = list.Count;
						job.Town.Wear = PathNetwork.Paint(cache, list);
					}
					else
					{
						PathStroke pathStroke = PathNetwork.PlanTrack(cache, job.From, job.To, grid.At);
						if (pathStroke != null)
						{
							job.Track.Wear = PathNetwork.Paint(cache, new List<PathStroke> { pathStroke });
						}
					}
				}
				catch (Exception ex)
				{
					job.Error = ex.GetType().Name + ": " + ex.Message;
				}
				job.Done = true;
			});
		}

		private static PathPoint DoorOf(Building b)
		{
			WorldTile current_tile = b.current_tile;
			int num = ((b.asset != null) ? b.asset.fundament : null)?.bottom ?? 0;
			return new PathPoint((float)current_tile.x + 0.5f, (float)(current_tile.y - num) - 0.35f);
		}
	}
}
