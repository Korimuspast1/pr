using System;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Net;
using FirstPerson.Towns;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson.OneWorld
{
	internal static class InsideSpots
	{
		private sealed class Home
		{
			public Building B;

			public long Id;

			public HouseWorld.Placement Pose;

			public RoomLayout Layout;

			public bool Making;

			public volatile bool Done;

			public RoomLayout Built;

			public string Failed;

			public float Used;

			public int ResidentsHash;

			public readonly List<long> BedOrder = new List<long>();

			public readonly Dictionary<int, int> Taken = new Dictionary<int, int>();

			public int X0;

			public int Y0;

			public int X1;

			public int Y1;
		}

		private sealed class Who
		{
			public Actor A;

			public Home Home;

			public int Place;

			public Vector2 Put;
		}

		private sealed class Woke
		{
			public Home Home;

			public int Place;

			public float At;
		}

		internal static bool MoveBodies = true;

		internal static bool On = true;

		private const int Keep = 2048;

		private const int Building_ = 2;

		private static readonly Dictionary<long, Home> _homes = new Dictionary<long, Home>();

		private static readonly Dictionary<long, Who> _who = new Dictionary<long, Who>();

		private static readonly List<long> _out = new List<long>();

		private static readonly List<Home> _queue = new List<Home>();

		private static int _making;

		private static int _pass = -1;

		private static object _world;

		internal static int PlacedForTest;

		internal static int OutForTest;

		internal static int LayoutsForTest;

		private static float _passStart;

		private static int _newThisPass;

		private const int NewPerPass = 200;

		private static readonly Dictionary<long, Woke> _woke = new Dictionary<long, Woke>();

		private static bool Moving
		{
			get
			{
				if (MoveBodies)
				{
					return !WorldSync.Mirroring;
				}
				return false;
			}
		}

		public static void Tick()
		{
			if (World.world == null || !Models.Ready || !On)
			{
				return;
			}
			if (!Wanted(WorldBoxMod.Instance?.Settings))
			{
				if (_who.Count > 0 || _homes.Count > 0)
				{
					Forget();
				}
				return;
			}
			object map_stats = World.world.map_stats;
			if (map_stats != _world)
			{
				_world = map_stats;
				Forget();
			}
			Outs();
			Finished();
			if (HouseOccupancy.Passes != _pass)
			{
				_pass = HouseOccupancy.Passes;
				Ins();
			}
			Start();
		}

		public static bool Where(Actor a, out RoomLayout layout, out RoomLayout.Place place)
		{
			layout = null;
			place = null;
			if (a == null || a.data == null || !_who.TryGetValue(a.data.id, out var value) || value.Home.Layout == null)
			{
				return false;
			}
			layout = value.Home.Layout;
			place = layout.Places[value.Place];
			return true;
		}

		public static RoomLayout LayoutOf(Building b, out HouseWorld.Placement pose)
		{
			pose = default(HouseWorld.Placement);
			if (b == null || b.data == null || !_homes.TryGetValue(b.data.id, out var value))
			{
				return null;
			}
			pose = value.Pose;
			value.Used = Time.unscaledTime;
			return value.Layout;
		}

		public static void Want(Building b)
		{
			Home home = HomeOf(b);
			if (home != null)
			{
				home.Used = Time.unscaledTime;
			}
		}

		private static void Ins()
		{
			_passStart = Time.unscaledTime;
			_newThisPass = 0;
			foreach (KeyValuePair<Building, HouseOccupancy.Occupants> item in HouseOccupancy.All)
			{
				Building key = item.Key;
				if (key == null || key.asset == null || key.asset.type != "type_house")
				{
					continue;
				}
				Home home = HomeOf(key);
				if (home == null)
				{
					continue;
				}
				home.Used = Time.unscaledTime;
				if (home.Layout == null)
				{
					continue;
				}
				foreach (Actor item2 in item.Value.Inside)
				{
					if (item2 == null || item2.data == null)
					{
						continue;
					}
					if (_who.TryGetValue(item2.data.id, out var value))
					{
						if (value.Home == home)
						{
							continue;
						}
						Leave(value);
					}
					Place(item2, home);
				}
			}
		}

		private static void Outs()
		{
			_out.Clear();
			foreach (KeyValuePair<long, Who> item in _who)
			{
				Who value = item.Value;
				Actor a = value.A;
				bool flag = a?.isAlive() ?? false;
				if (flag && a.is_inside_building && a.inside_building == value.Home.B)
				{
					continue;
				}
				_out.Add(item.Key);
				if (!flag || a.is_inside_building)
				{
					continue;
				}
				if (Possessed(a))
				{
					_woke[a.data.id] = new Woke
					{
						Home = value.Home,
						Place = value.Place,
						At = Time.unscaledTime
					};
				}
				else if (Moving && (a.current_position - value.Put).sqrMagnitude < 0.01f)
				{
					WorldTile worldTile = SafeDoor(value.Home.B);
					if (worldTile != null)
					{
						a.setCurrentTilePosition(worldTile);
						OutForTest++;
					}
				}
			}
			foreach (long item2 in _out)
			{
				if (_who.TryGetValue(item2, out var value2))
				{
					Free(value2);
				}
				_who.Remove(item2);
			}
		}

		internal static bool Possessed(Actor a)
		{
			try
			{
				return ControllableUnit.isControllingUnit(a) || ((BaseSimObject)a).hasStatus("possessed");
			}
			catch
			{
				return false;
			}
		}

		internal static WorldTile SafeDoor(Building b)
		{
			try
			{
				if (b == null || !b.isAlive())
				{
					return null;
				}
				WorldTile current_tile = b.current_tile;
				WorldTile worldTile = ((current_tile != null && current_tile.has_tile_down) ? current_tile.tile_down : null);
				if (worldTile == null)
				{
					return current_tile;
				}
				TileTypeBase type = worldTile.Type;
				return (type != null && type.ground && !type.block && !type.liquid && !type.lava && !worldTile.isOnFire()) ? worldTile : current_tile;
			}
			catch
			{
				return null;
			}
		}

		internal static Vector2 Clamp(Building b, Vector2 at, out WorldTile tile)
		{
			tile = null;
			try
			{
				WorldTile current_tile = b.current_tile;
				BuildingFundament fundament = b.asset.fundament;
				int num = current_tile.x - (fundament?.left ?? 1);
				int num2 = current_tile.x + (fundament?.right ?? 1);
				int num3 = current_tile.y - (fundament?.bottom ?? 0);
				int num4 = current_tile.y + (fundament?.top ?? 2);
				at = new Vector2(Mathf.Clamp(at.x, (float)num + 0.05f, (float)num2 + 0.95f), Mathf.Clamp(at.y, (float)num3 + 0.05f, (float)num4 + 0.95f));
				tile = World.world.GetTile((int)Mathf.Floor(at.x), (int)Mathf.Floor(at.y));
			}
			catch
			{
			}
			return at;
		}

		public static bool WokeAt(Actor a, Building b, out RoomLayout layout, out RoomLayout.Place place)
		{
			layout = null;
			place = null;
			if (a == null || a.data == null)
			{
				return false;
			}
			if (_who.TryGetValue(a.data.id, out var value) && value.Home.B == b && value.Home.Layout != null && value.Place < value.Home.Layout.Places.Count)
			{
				layout = value.Home.Layout;
				place = layout.Places[value.Place];
				return true;
			}
			if (!_woke.TryGetValue(a.data.id, out var value2))
			{
				return false;
			}
			_woke.Remove(a.data.id);
			if (Time.unscaledTime - value2.At > 60f || value2.Home.B != b || value2.Home.Layout == null || value2.Place >= value2.Home.Layout.Places.Count)
			{
				return false;
			}
			layout = value2.Home.Layout;
			place = layout.Places[value2.Place];
			return true;
		}

		private static void Leave(Who w)
		{
			Free(w);
			_who.Remove(w.A.data.id);
		}

		private static void Free(Who w)
		{
			if (w.Home.Taken.TryGetValue(w.Place, out var value))
			{
				if (value <= 1)
				{
					w.Home.Taken.Remove(w.Place);
				}
				else
				{
					w.Home.Taken[w.Place] = value - 1;
				}
			}
		}

		private static void Place(Actor a, Home h)
		{
			if (Possessed(a))
			{
				return;
			}
			int num = Choose(a, h);
			if (num < 0)
			{
				return;
			}
			h.Taken[num] = ((!h.Taken.TryGetValue(num, out var value)) ? 1 : (value + 1));
			Who who = new Who
			{
				A = a,
				Home = h,
				Place = num
			};
			RoomLayout.Place place = h.Layout.Places[num];
			Vector2 vector = ToWorld(h, place.MidX, place.MidY);
			if (Moving)
			{
				float num2 = Mathf.Clamp(vector.x, (float)h.X0 + 0.05f, (float)h.X1 + 0.95f);
				float num3 = Mathf.Clamp(vector.y, (float)h.Y0 + 0.05f, (float)h.Y1 + 0.95f);
				WorldTile tile = World.world.GetTile((int)Mathf.Floor(num2), (int)Mathf.Floor(num3));
				if (tile != null)
				{
					a.setCurrentTilePosition(tile);
					a.current_position = new Vector2(num2, num3);
					PlacedForTest++;
				}
			}
			who.Put = a.current_position;
			_who[a.data.id] = who;
		}

		internal static Vector2 ToWorld(Building b, float rx, float ry)
		{
			Home value;
			Home home = ((b != null && b.data != null && _homes.TryGetValue(b.data.id, out value)) ? value : null);
			if (home == null || home.Layout == null)
			{
				return b?.current_position ?? Vector2.zero;
			}
			return ToWorld(home, rx, ry);
		}

		private static Vector2 ToWorld(Home h, float rx, float ry)
		{
			float footW = h.X1 - h.X0 + 1;
			float footD = h.Y1 - h.Y0 + 1;
			h.Layout.ToHouse(rx, ry, footW, footD, out var hx, out var hy);
			Vector2 v = new Vector2(hx, hy);
			if (!h.Layout.Fitted)
			{
				v = Facing.Turn(v, h.Pose.Face);
			}
			return new Vector2(h.Pose.X + v.x, h.Pose.Y + v.y);
		}

		private static int Choose(Actor a, Home h)
		{
			RoomLayout layout = h.Layout;
			HouseOccupancy.Doing doing = HouseOccupancy.DoingOf(a);
			string text = null;
			try
			{
				text = ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.id;
			}
			catch
			{
			}
			if (doing == HouseOccupancy.Doing.Sleep)
			{
				int num = OwnBed(a, h);
				if (num >= 0)
				{
					return num;
				}
				int num2 = First(h, RoomLayout.Use.Bed);
				if (num2 >= 0)
				{
					return num2;
				}
			}
			if (doing == HouseOccupancy.Doing.Work)
			{
				int num3 = First(h, RoomLayout.Use.Bench);
				if (num3 >= 0)
				{
					return num3;
				}
			}
			if (text == "generate_loot")
			{
				int num4 = First(h, RoomLayout.Use.Chest);
				if (num4 >= 0)
				{
					return num4;
				}
			}
			int num5;
			switch (text)
			{
			default:
				num5 = ((doing == HouseOccupancy.Doing.Together) ? 1 : 0);
				break;
			case "stay_in_own_home":
			case "fp_homebody":
			case "fp_home_visit":
			case "fp_family_in":
				num5 = 1;
				break;
			}
			bool flag = (byte)num5 != 0;
			for (int num6 = 1; num6 >= 0; num6--)
			{
				RoomLayout.Use[] array = ((!flag) ? new RoomLayout.Use[7]
				{
					RoomLayout.Use.Free,
					RoomLayout.Use.Hearth,
					RoomLayout.Use.Table,
					RoomLayout.Use.TableSeat,
					RoomLayout.Use.HearthSeat,
					RoomLayout.Use.Bench,
					RoomLayout.Use.Chest
				} : new RoomLayout.Use[5]
				{
					RoomLayout.Use.HearthSeat,
					RoomLayout.Use.TableSeat,
					RoomLayout.Use.Hearth,
					RoomLayout.Use.Table,
					RoomLayout.Use.Free
				});
				foreach (RoomLayout.Use use in array)
				{
					int num7 = First(h, use, (num6 != 1) ? (-1) : 0);
					if (num7 >= 0)
					{
						return num7;
					}
				}
			}
			for (int j = 0; j < layout.Places.Count; j++)
			{
				if (layout.Places[j].Use == RoomLayout.Use.Door)
				{
					return j;
				}
			}
			if (layout.Places.Count <= 0)
			{
				return -1;
			}
			return 0;
		}

		private static int First(Home h, RoomLayout.Use use, int storey = -1)
		{
			List<RoomLayout.Place> places = h.Layout.Places;
			for (int i = 0; i < places.Count; i++)
			{
				if (places[i].Use == use && (storey < 0 || places[i].Storey == storey) && (!h.Taken.TryGetValue(i, out var value) || value < places[i].Sleeps))
				{
					return i;
				}
			}
			return -1;
		}

		private static int OwnBed(Actor a, Home h)
		{
			Building b = h.B;
			int num = 17;
			try
			{
				foreach (long resident in b.residents)
				{
					num = num * 31 + resident.GetHashCode();
				}
			}
			catch
			{
			}
			if (num != h.ResidentsHash || h.BedOrder.Count == 0)
			{
				h.ResidentsHash = num;
				h.BedOrder.Clear();
				List<Actor> list = new List<Actor>();
				try
				{
					foreach (long resident2 in b.residents)
					{
						Actor actor = World.world.units.get(resident2);
						if (actor != null && actor.isAlive())
						{
							list.Add(actor);
						}
					}
				}
				catch
				{
				}
				list.Sort(delegate(Actor x, Actor y)
				{
					bool flag = x.isAdult();
					bool flag2 = y.isAdult();
					if (flag != flag2)
					{
						if (!flag)
						{
							return 1;
						}
						return -1;
					}
					int num5 = y.getAge().CompareTo(x.getAge());
					return (num5 == 0) ? x.data.id.CompareTo(y.data.id) : num5;
				});
				HashSet<long> hashSet = new HashSet<long>();
				foreach (Actor item in list)
				{
					if (!hashSet.Contains(item.data.id))
					{
						h.BedOrder.Add(item.data.id);
						hashSet.Add(item.data.id);
						Actor actor2 = null;
						try
						{
							actor2 = item.lover;
						}
						catch
						{
						}
						if (actor2 != null && actor2.data != null && !hashSet.Contains(actor2.data.id) && list.Contains(actor2))
						{
							h.BedOrder.Add(actor2.data.id);
							hashSet.Add(actor2.data.id);
						}
					}
				}
			}
			int num2 = h.BedOrder.IndexOf(a.data.id);
			if (num2 < 0)
			{
				return -1;
			}
			List<RoomLayout.Place> places = h.Layout.Places;
			int num3 = 0;
			for (int num4 = 0; num4 < places.Count; num4++)
			{
				if (places[num4].Use != RoomLayout.Use.Bed)
				{
					continue;
				}
				if (num2 < num3 + places[num4].Sleeps)
				{
					if (h.Taken.TryGetValue(num4, out var value) && value >= places[num4].Sleeps)
					{
						return -1;
					}
					return num4;
				}
				num3 += places[num4].Sleeps;
			}
			return -1;
		}

		private static Home HomeOf(Building b)
		{
			if (b == null || b.data == null || !b.isAlive())
			{
				return null;
			}
			long id = b.data.id;
			if (_homes.TryGetValue(id, out var value) && value.B == b && value.Pose.Asset == b.asset && value.Pose.Tile == b.current_tile && (!HouseWorld.Of(b, out var p) || Same(value.Pose, p)))
			{
				return value;
			}
			if (!Pose(b, out var p2))
			{
				return null;
			}
			if (_homes.TryGetValue(id, out var value2))
			{
				if (value2.B == b && Same(value2.Pose, p2))
				{
					return value2;
				}
				Drop(value2);
			}
			if (_newThisPass >= 200)
			{
				return null;
			}
			if (_homes.Count >= 2048 && !Oldest())
			{
				return null;
			}
			_newThisPass++;
			value2 = new Home
			{
				B = b,
				Id = id,
				Pose = p2,
				Used = Time.unscaledTime
			};
			Footprint(b, value2);
			_homes[id] = value2;
			_queue.Add(value2);
			return value2;
		}

		private static bool Same(HouseWorld.Placement a, HouseWorld.Placement b)
		{
			if (a.Model == b.Model && a.Mirror == b.Mirror && a.Face == b.Face && Mathf.Abs(a.Scale - b.Scale) < 0.002f && a.Tile == b.Tile)
			{
				return a.Asset == b.Asset;
			}
			return false;
		}

		private static void Footprint(Building b, Home h)
		{
			WorldTile current_tile = b.current_tile;
			BuildingFundament fundament = b.asset.fundament;
			int num = fundament?.left ?? 1;
			int num2 = fundament?.right ?? 1;
			int num3 = fundament?.top ?? 2;
			int num4 = fundament?.bottom ?? 0;
			h.X0 = current_tile.x - num;
			h.X1 = current_tile.x + num2;
			h.Y0 = current_tile.y - num4;
			h.Y1 = current_tile.y + num3;
		}

		internal static bool Pose(Building b, out HouseWorld.Placement p)
		{
			if (HouseWorld.Of(b, out p))
			{
				return true;
			}
			p = default(HouseWorld.Placement);
			try
			{
				string text = Models.BuildingFamily(b);
				MeshModel meshModel = ((text != null) ? Models.Get(text) : null);
				Sprite sprite = ((b.animData?.main != null && b.animData.main.Length != 0) ? b.animData.main[0] : null);
				WorldTile current_tile = b.current_tile;
				if (meshModel == null || sprite == null || current_tile == null)
				{
					return false;
				}
				float scale = sprite.rect.width / Mathf.Max(0.01f, sprite.pixelsPerUnit) * Mathf.Abs(b.asset.scale_base.x) / Mathf.Max(0.5f, meshModel.InfoFloat("width", meshModel.Max.X - meshModel.Min.X));
				BuildingFundament fundament = b.asset.fundament;
				float num = ((fundament != null) ? ((float)(fundament.top - fundament.bottom) * 0.5f) : 0f);
				p = new HouseWorld.Placement
				{
					Model = meshModel,
					X = current_tile.posV3.x,
					Y = current_tile.posV3.y + num,
					Scale = scale,
					Mirror = (meshModel.InfoFloat("footing", 0f) > 0f && Models.Mirrored(b)),
					Face = (Facing.Of(b) & 3),
					Tile = current_tile,
					Asset = b.asset
				};
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static void Start()
		{
			if (_queue.Count != 0 && _making < 2)
			{
				Vector3 eye = Vector3.zero;
				try
				{
					eye = World.world.camera.transform.position;
				}
				catch
				{
				}
				Actor actor = WorldBoxMod.Instance?.Host;
				if (actor != null)
				{
					eye = actor.current_position;
				}
				_queue.RemoveAll((Home q) => q.Making || q.Layout != null || !_homes.ContainsKey(q.Id) || _homes[q.Id] != q);
				_queue.Sort((Home x, Home y) => Dist(x, eye).CompareTo(Dist(y, eye)));
				while (_making < 2 && _queue.Count > 0)
				{
					Home h = _queue[0];
					_queue.RemoveAt(0);
					Begin(h);
				}
			}
		}

		private static float Dist(Home h, Vector3 eye)
		{
			return (h.Pose.X - eye.x) * (h.Pose.X - eye.x) + (h.Pose.Y - eye.y) * (h.Pose.Y - eye.y);
		}

		private static bool Wanted(Settings s)
		{
			if (s != null && s.MapHouses <= 0)
			{
				return s.LookInside;
			}
			return true;
		}

		private static void Begin(Home h)
		{
			Building b = h.B;
			HouseInterior.RaceAndTier(b.asset, out var race, out var tier);
			int seed = (int)(b.getID() & 0x7FFFFFFF);
			string style = ((h.Pose.Model != null) ? h.Pose.Model.Info("style") : null);
			HouseWorld.Placement place = h.Pose;
			bool fitted = HouseInterior.WalksIn(b.asset);
			uint wall = TerrainCache.Rgb(215, 195, 155);
			uint roof = TerrainCache.Rgb(160, 60, 50);
			int roomW = 5 + Mathf.RoundToInt((float)tier * 0.6f) + ((seed % 3 == 0) ? 1 : 0);
			int roomD = 4 + (tier + 1) / 3;
			_making++;
			h.Making = true;
			Vector3 eye = Vector3.zero;
			try
			{
				eye = ((WorldBoxMod.Instance?.Host != null) ? ((Vector3)WorldBoxMod.Instance.Host.current_position) : World.world.camera.transform.position);
			}
			catch
			{
			}
			((Dist(h, eye) < 1600f) ? new Action<Action>(SlowLane.RunSoon) : new Action<Action>(SlowLane.Run))(delegate
			{
				try
				{
					Interior interior = null;
					if (fitted)
					{
						HousePlan housePlan = HouseWorld.PlanFor(place);
						if (housePlan != null)
						{
							interior = InteriorBuilder.BuildFitted(housePlan, place.Scale, place.Mirror, wall, roof, seed, race, tier, Models.Get, style);
						}
					}
					if (interior == null)
					{
						interior = InteriorBuilder.Build(roomW, roomD, wall, roof, seed, race, tier, Models.Get, style);
					}
					h.Built = RoomLayout.Of(interior);
				}
				catch (Exception ex)
				{
					h.Failed = ex.Message;
				}
				finally
				{
					h.Done = true;
				}
			});
		}

		private static void Finished()
		{
			if (_making == 0)
			{
				return;
			}
			foreach (Home value in _homes.Values)
			{
				if (value.Making && value.Done)
				{
					_making--;
					if (value.Failed != null)
					{
						Log.Error("inside: a layout of " + value.B?.asset?.id + " failed: " + value.Failed);
					}
					else
					{
						value.Layout = value.Built;
						LayoutsForTest++;
					}
					value.Making = false;
				}
			}
		}

		private static void Drop(Home h)
		{
			_homes.Remove(h.Id);
			if (h.Making)
			{
				_making--;
			}
			_out.Clear();
			foreach (KeyValuePair<long, Who> item in _who)
			{
				if (item.Value.Home == h)
				{
					_out.Add(item.Key);
				}
			}
			foreach (long item2 in _out)
			{
				_who.Remove(item2);
			}
		}

		private static bool Oldest()
		{
			Home home = null;
			foreach (Home value in _homes.Values)
			{
				if (!value.Making && value.Used < _passStart && (home == null || value.Used < home.Used))
				{
					home = value;
				}
			}
			if (home == null)
			{
				return false;
			}
			Drop(home);
			return true;
		}

		private static void Forget()
		{
			_homes.Clear();
			_who.Clear();
			_queue.Clear();
			_making = 0;
			_pass = -1;
		}
	}
}
