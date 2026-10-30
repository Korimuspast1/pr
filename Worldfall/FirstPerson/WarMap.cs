using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;
using life.taxi;

namespace FirstPerson
{
	internal sealed class WarMap
	{
		public struct Unit
		{
			public Actor Actor;

			public Vector2 P;

			public Color Color;

			public bool Ours;

			public bool Fighting;

			public bool King;

			public bool Warrior;

			public bool Ship;

			public Side Side;
		}

		public struct Town
		{
			public Vector2 P;

			public string Name;

			public Color Color;

			public bool Ours;

			public bool Capital;

			public bool Objective;

			public Kingdom Owner;

			public Side Side;

			public int People;

			public City City;
		}

		public enum Side
		{
			Own,
			Ally,
			Enemy,
			Neutral
		}

		public enum Layer
		{
			None,
			Kingdoms,
			Alliances,
			Sides
		}

		public sealed class Realm
		{
			public Kingdom K;

			public string Name;

			public Color Color;

			public Color Text;

			public Side Side;

			public Alliance Alliance;

			public Vector2 Capital;

			public bool HasCapital;

			public int Towns;

			public int People;

			public int Warriors;

			public int Opinion;

			public bool HasOpinion;

			public readonly List<Kingdom> Foes = new List<Kingdom>();

			public readonly List<Kingdom> Friends = new List<Kingdom>();
		}

		public sealed class WarEntry
		{
			public War War;

			public string Name;

			public readonly List<Kingdom> Attackers = new List<Kingdom>();

			public readonly List<Kingdom> Defenders = new List<Kingdom>();

			public Kingdom MainAttacker;

			public Kingdom MainDefender;

			public bool Ours;

			public bool WeAttack;

			public bool Total;

			public int DeadAttackers;

			public int DeadDefenders;

			public int Years;

			public Kingdom Foe;
		}

		public sealed class Pact
		{
			public Alliance A;

			public string Name;

			public Color Color;

			public readonly List<Kingdom> Members = new List<Kingdom>();

			public bool Ours;

			public int Warriors;
		}

		public struct Border
		{
			public Vector2 A;

			public Vector2 B;

			public bool Vertical;

			public int Low;

			public int High;
		}

		public bool Open;

		public int WarIndex;

		public readonly HashSet<Actor> Marked = new HashSet<Actor>();

		public readonly List<Kingdom> Wars = new List<Kingdom>();

		public Kingdom Home;

		public Kingdom Enemy;

		public readonly List<Unit> Units = new List<Unit>(512);

		public readonly List<Town> Towns = new List<Town>();

		public readonly List<Vector2> Clashes = new List<Vector2>();

		public readonly List<Vector2> OtherClashes = new List<Vector2>();

		public int OurWarriors;

		public int TheirWarriors;

		public int OurFighting;

		public int TheirFighting;

		public int OurShips;

		public int TheirShips;

		public float RegionX;

		public float RegionY;

		public float RegionSize = 64f;

		public float Span = 64f;

		public Rect Bounds;

		public float Zoom = 1f;

		public Vector2 Centre;

		public Texture2D Terrain;

		private float _texX = -1f;

		private float _texY;

		private float _texSize;

		private float _texAge;

		private float _refresh;

		private bool _fit;

		private Kingdom _fitFor;

		public Rect WantView;

		public float WantPixels;

		private float _rebuildWait;

		private readonly List<Actor> _deadMarks = new List<Actor>();

		private readonly Dictionary<long, Vector2> _cellSum = new Dictionary<long, Vector2>();

		private readonly Dictionary<long, int> _cellCount = new Dictionary<long, int>();

		private readonly HashSet<long> _cellOurs = new HashSet<long>();

		private City _objective;

		public const int Seats = 50;

		private const float BoardWait = 25f;

		private const float EmbarkReach = 4.5f;

		private const float QuickBoard = 9f;

		public readonly List<Lift> Lifts = new List<Lift>();

		private readonly Dictionary<Actor, Lift> _liftOf = new Dictionary<Actor, Lift>();

		private readonly Dictionary<Actor, float> _boardSentAt = new Dictionary<Actor, float>();

		private readonly HashSet<string> _liftNews = new HashSet<string>();

		public const int MaxFlags = 6;

		public readonly List<WarFlag> Flags = new List<WarFlag>();

		private readonly Dictionary<Actor, WarFlag> _bandOf = new Dictionary<Actor, WarFlag>();

		private readonly Dictionary<Actor, float> _orderedAt = new Dictionary<Actor, float>();

		private readonly Dictionary<Actor, float> _freeUntil = new Dictionary<Actor, float>();

		private readonly List<WarFlag> _emptied = new List<WarFlag>();

		private readonly List<WarFlag> _left = new List<WarFlag>();

		private readonly List<WarFlag> _downed = new List<WarFlag>();

		private Kingdom _flagsOf;

		private long _flagsKing = -1L;

		private float _upkeep;

		private static readonly Color[] FlagColors = new Color[6]
		{
			new Color(0.35f, 0.72f, 1f),
			new Color(0.55f, 0.95f, 0.3f),
			new Color(0.88f, 0.5f, 1f),
			new Color(1f, 0.58f, 0.18f),
			new Color(0.25f, 0.95f, 0.85f),
			new Color(1f, 0.45f, 0.72f)
		};

		public readonly List<ShipOrder> ShipOrders = new List<ShipOrder>();

		private readonly Dictionary<Actor, ShipOrder> _orderOf = new Dictionary<Actor, ShipOrder>();

		public const float SafeDistance = 4.5f;

		private readonly List<WorldTile> _berthsTaken = new List<WorldTile>();

		private readonly HashSet<string> _fleetNews = new HashSet<string>();

		private Kingdom _fleetOf;

		private float _fleetUpkeep;

		private static WarMap _current;

		private long _fleetKing = -1L;

		public const float DefaultRadius = 8f;

		public const float QuickRadius = 5f;

		public const float MinRadius = 3f;

		public const float MaxRadius = 30f;

		private const float EngageReach = 7f;

		private const float TorchReach = 4f;

		public static readonly Color QuickColor = new Color(0.95f, 0.92f, 0.85f);

		public static readonly string[] OrderNames = new string[5] { "Attack", "Wait", "Destroy", "Kill", "Pillage" };

		public static readonly KeyCode[] OrderKeys = new KeyCode[5]
		{
			KeyCode.Q,
			KeyCode.E,
			KeyCode.R,
			KeyCode.T,
			KeyCode.Y
		};

		public static readonly string[] OrderTips = new string[5] { "Fight every foe in the area and hold it", "Hold the ground and fight only what comes close", "Break the foe's buildings in the area", "Hunt down the foe's fighters in the area", "Kill everyone in the area and burn the houses" };

		public readonly List<WarFlag> Posts = new List<WarFlag>();

		public WarOrder NextOrder;

		public WarFlag SelectedFlag;

		private readonly Dictionary<Actor, int> _groupOf = new Dictionary<Actor, int>();

		private readonly Dictionary<Actor, long> _groupIds = new Dictionary<Actor, long>();

		private readonly List<Actor> _fighters = new List<Actor>();

		private readonly List<Actor> _others = new List<Actor>();

		private readonly List<Building> _houses = new List<Building>();

		private readonly HashSet<MapChunk> _chunks = new HashSet<MapChunk>();

		public readonly List<Realm> Realms = new List<Realm>();

		public readonly List<WarEntry> AllWars = new List<WarEntry>();

		public readonly List<Pact> Pacts = new List<Pact>();

		private readonly Dictionary<Kingdom, Realm> _realmOf = new Dictionary<Kingdom, Realm>();

		private readonly Dictionary<Kingdom, int> _realmIndex = new Dictionary<Kingdom, int>();

		public Layer Overlay = Layer.Kingdoms;

		public int Tab;

		public Vector2 RealmScroll;

		public Kingdom Picked;

		public float PickedAt = -10f;

		private float _realmsWait;

		public Texture2D Territory;

		public int ZonesX;

		public int ZonesY;

		public float ZoneSize = 8f;

		private int[] _owner = new int[0];

		private Layer _territoryLayer = (Layer)(-1);

		private bool _territoryDirty = true;

		public readonly List<Border> Borders = new List<Border>();

		public static readonly Color OwnSide = new Color(0.33f, 0.62f, 1f);

		public static readonly Color AllySide = new Color(0.42f, 0.88f, 0.36f);

		public static readonly Color EnemySide = new Color(0.96f, 0.28f, 0.22f);

		public static readonly Color NeutralSide = new Color(0.62f, 0.62f, 0.6f);

		public float ZoomMin => Span / Mathf.Max(Span, Mathf.Max(WorldWidth, WorldHeight) * 1.05f);

		public float ZoomMax => Mathf.Max(1f, Span / 12f);

		public static float WorldWidth
		{
			get
			{
				try
				{
					return MapBox.width;
				}
				catch
				{
					return 256f;
				}
			}
		}

		public static float WorldHeight
		{
			get
			{
				try
				{
					return MapBox.height;
				}
				catch
				{
					return 256f;
				}
			}
		}

		public float TexX => _texX;

		public float TexY => _texY;

		public float TexSize => _texSize;

		public Kingdom FlagsKingdom => _flagsOf;

		public List<WarFlag> LastEmptied => _emptied;

		public Kingdom FleetKingdom => _fleetOf;

		public WarOrder CurrentOrder
		{
			get
			{
				if (SelectedFlag == null || !Flags.Contains(SelectedFlag))
				{
					return NextOrder;
				}
				return SelectedFlag.Order;
			}
		}

		public void Toggle(Actor host, Action<string> toast)
		{
			if (Open)
			{
				Open = false;
				return;
			}
			if (!Royal.IsKing(host))
			{
				toast("Only a king keeps a war map.");
				return;
			}
			Open = true;
			_refresh = 0f;
			_realmsWait = 0f;
			_fit = true;
		}

		public void Update(Actor host, TerrainCache cache, float dt)
		{
			_deadMarks.Clear();
			foreach (Actor item in Marked)
			{
				bool flag;
				try
				{
					flag = item?.isAlive() ?? false;
				}
				catch
				{
					flag = false;
				}
				if (!flag)
				{
					_deadMarks.Add(item);
				}
			}
			foreach (Actor deadMark in _deadMarks)
			{
				Marked.Remove(deadMark);
			}
			if (Flags.Count > 0 && host.kingdom != FlagsKingdom && Royal.IsKing(host))
			{
				ClearFlags();
			}
			if (ShipOrders.Count > 0 && host.kingdom != FleetKingdom && Royal.IsKing(host))
			{
				ClearFleet();
			}
			if (!Open)
			{
				return;
			}
			if (!Royal.IsKing(host))
			{
				Open = false;
				return;
			}
			_refresh -= dt;
			_texAge += dt;
			_rebuildWait -= dt;
			if (cache != null && WantView.width > 0f && _rebuildWait <= 0f && (_texX < 0f || _texAge > 12f || !Covers(WantView) || TooCoarse()))
			{
				_rebuildWait = 0.25f;
				BuildTerrain(cache, WantView);
			}
			if (_refresh > 0f)
			{
				return;
			}
			_refresh = 0.35f;
			try
			{
				Collect(host);
			}
			catch (Exception ex)
			{
				Log.Error("war map: " + ex.Message);
			}
		}

		private bool Covers(Rect v)
		{
			if (v.xMin >= _texX && v.yMin >= _texY && v.xMax <= _texX + _texSize)
			{
				return v.yMax <= _texY + _texSize;
			}
			return false;
		}

		private bool TooCoarse()
		{
			if (Terrain == null || WantPixels <= 0f)
			{
				return false;
			}
			float num = _texSize / (float)Terrain.width;
			float num2 = WantView.width / WantPixels;
			if (num > num2 * 2.2f)
			{
				return num > 1.01f;
			}
			return false;
		}

		private void Collect(Actor host)
		{
			Home = host.kingdom;
			Wars.Clear();
			foreach (Kingdom item in Royal.Rivals(Home))
			{
				if (Royal.AtWar(Home, item))
				{
					Wars.Add(item);
				}
			}
			Enemy = ((Wars.Count > 0) ? Wars[(WarIndex % Wars.Count + Wars.Count) % Wars.Count] : null);
			CollectRealms(_fit);
			Units.Clear();
			Towns.Clear();
			Clashes.Clear();
			OtherClashes.Clear();
			OurWarriors = (TheirWarriors = (OurFighting = (TheirFighting = (OurShips = (TheirShips = 0)))));
			Color color = Royal.ColorOf(Home);
			Color color2 = ((Enemy != null) ? Royal.ColorOf(Enemy) : Color.red);
			float x0 = host.current_position.x;
			float y0 = host.current_position.y;
			float x1 = x0;
			float y1 = y0;
			City objective = _objective;
			foreach (Realm realm2 in Realms)
			{
				Kingdom k = realm2.K;
				foreach (City city in k.getCities())
				{
					if (city == null || !city.isAlive())
					{
						continue;
					}
					WorldTile tile = city.getTile();
					if (tile != null)
					{
						Vector2 p = new Vector2(tile.posV3.x, tile.posV3.y);
						if (k == Home || k == Enemy)
						{
							Grow(p);
						}
						int people = 0;
						try
						{
							people = city.countUnits();
						}
						catch
						{
						}
						Towns.Add(new Town
						{
							P = p,
							Name = city.name,
							Color = ((k == Home) ? color : realm2.Color),
							Ours = (k == Home),
							Capital = (k.capital == city),
							Objective = (city == objective),
							Owner = k,
							Side = realm2.Side,
							People = people,
							City = city
						});
					}
				}
			}
			List<Actor> simpleList = World.world.units.getSimpleList();
			for (int i = 0; i < simpleList.Count; i++)
			{
				Actor actor = simpleList[i];
				if (actor == null || !actor.isAlive() || actor.kingdom == null || actor.is_inside_boat)
				{
					continue;
				}
				Realm realm = RealmOf(actor.kingdom);
				if (realm == null && !Marked.Contains(actor))
				{
					continue;
				}
				Side side = realm?.Side ?? SideOf(actor.kingdom);
				bool flag = side == Side.Own;
				bool flag2 = Enemy != null && actor.kingdom == Enemy;
				bool flag3 = false;
				bool flag4 = false;
				bool flag5 = false;
				bool flag6 = IsShip(actor);
				try
				{
					flag3 = actor.isWarrior();
					flag4 = actor.isKing();
					flag5 = actor.isFighting();
				}
				catch
				{
				}
				if ((!flag3 && !flag4 && !flag5 && !flag6 && !Marked.Contains(actor)) || (side == Side.Neutral && !flag3 && !flag4 && !Marked.Contains(actor)))
				{
					continue;
				}
				Vector2 current_position = actor.current_position;
				if ((flag || flag2) && (flag3 || flag4 || flag6))
				{
					Grow(current_position);
				}
				Units.Add(new Unit
				{
					Actor = actor,
					P = current_position,
					Color = (flag ? color : (flag2 ? color2 : (realm?.Color ?? Royal.ColorOf(actor.kingdom)))),
					Ours = flag,
					Fighting = flag5,
					King = flag4,
					Warrior = (flag3 && !flag6),
					Ship = flag6,
					Side = side
				});
				if (flag && flag3 && !flag6)
				{
					OurWarriors++;
					if (flag5)
					{
						OurFighting++;
					}
				}
				if (flag2 && flag3 && !flag6)
				{
					TheirWarriors++;
					if (flag5)
					{
						TheirFighting++;
					}
				}
				if (flag6)
				{
					if (flag)
					{
						OurShips++;
					}
					else if (flag2)
					{
						TheirShips++;
					}
				}
				if (flag5)
				{
					Cluster(current_position, side != Side.Neutral);
				}
			}
			foreach (KeyValuePair<long, int> item2 in _cellCount)
			{
				if (item2.Value >= 3)
				{
					(_cellOurs.Contains(item2.Key) ? Clashes : OtherClashes).Add(_cellSum[item2.Key] / item2.Value);
				}
			}
			_cellSum.Clear();
			_cellCount.Clear();
			_cellOurs.Clear();
			float num = Mathf.Max(56f, Mathf.Max(x1 - x0, y1 - y0) + 24f);
			RegionX = (x0 + x1) * 0.5f - num * 0.5f;
			RegionY = (y0 + y1) * 0.5f - num * 0.5f;
			RegionSize = num;
			Rect bounds = new Rect(RegionX, RegionY, num, num);
			if (_fit || Enemy != _fitFor)
			{
				_fit = false;
				_fitFor = Enemy;
				Span = num;
				Bounds = bounds;
				Zoom = 1f;
				Centre = bounds.center;
			}
			else
			{
				Bounds = Rect.MinMaxRect(Mathf.Min(Bounds.xMin, bounds.xMin), Mathf.Min(Bounds.yMin, bounds.yMin), Mathf.Max(Bounds.xMax, bounds.xMax), Mathf.Max(Bounds.yMax, bounds.yMax));
			}
			void Grow(Vector2 vector)
			{
				x0 = Mathf.Min(x0, vector.x);
				y0 = Mathf.Min(y0, vector.y);
				x1 = Mathf.Max(x1, vector.x);
				y1 = Mathf.Max(y1, vector.y);
			}
		}

		private void Cluster(Vector2 p, bool ours)
		{
			long num = ((long)Mathf.FloorToInt(p.x / 7f) << 32) ^ (uint)Mathf.FloorToInt(p.y / 7f);
			_cellSum.TryGetValue(num, out var value);
			_cellCount.TryGetValue(num, out var value2);
			_cellSum[num] = value + p;
			_cellCount[num] = value2 + 1;
			if (ours)
			{
				_cellOurs.Add(num);
			}
		}

		public Vector2 TownPoint(City c)
		{
			try
			{
				WorldTile tile = c.getTile();
				if (tile != null)
				{
					return new Vector2(tile.posV3.x, tile.posV3.y);
				}
			}
			catch
			{
			}
			return default(Vector2);
		}

		public void SetObjective(City c)
		{
			_objective = c;
		}

		private void BuildTerrain(TerrainCache t, Rect view)
		{
			float num = Mathf.Max(48f, Mathf.Max(view.width, view.height) * 1.6f);
			Vector2 center = view.center;
			_texX = center.x - num * 0.5f;
			_texY = center.y - num * 0.5f;
			_texSize = num;
			_texAge = 0f;
			float num2 = ((WantPixels > 0f) ? (WantPixels * num / Mathf.Max(1f, view.width)) : num);
			int num3 = Mathf.Clamp(Mathf.CeilToInt(Mathf.Min(num * 4f, num2 / 1.6f)), 96, 512);
			if (Terrain == null || Terrain.width != num3)
			{
				if (Terrain != null)
				{
					UnityEngine.Object.Destroy(Terrain);
				}
				Terrain = new Texture2D(num3, num3, TextureFormat.RGBA32, mipChain: false)
				{
					filterMode = FilterMode.Point,
					wrapMode = TextureWrapMode.Clamp,
					name = "FirstPersonWarMap"
				};
			}
			Color32[] array = new Color32[num3 * num3];
			float num4 = _texSize / (float)num3;
			Color b = new Color(0.16f, 0.35f, 0.62f);
			Color color = new Color(0.06f, 0.08f, 0.13f);
			int w = t.Width;
			int h = t.Height;
			for (int i = 0; i < num3; i++)
			{
				for (int j = 0; j < num3; j++)
				{
					float f = _texX + ((float)j + 0.5f) * num4;
					float f2 = _texY + ((float)i + 0.5f) * num4;
					int num5 = Mathf.FloorToInt(f);
					int num6 = Mathf.FloorToInt(f2);
					Color a;
					if (num5 < 0 || num6 < 0 || num5 >= w || num6 >= h)
					{
						a = color;
					}
					else
					{
						int num7 = num6 * w + num5;
						uint num8 = t.BaseColors[num7];
						Color color2 = new Color((float)(num8 & 0xFF) / 255f, (float)((num8 >> 8) & 0xFF) / 255f, (float)((num8 >> 16) & 0xFF) / 255f);
						if ((t.Flags[num7] & 1) != 0)
						{
							a = Color.Lerp(color2, b, 0.55f) * 0.92f;
						}
						else
						{
							a = color2;
							int num9 = Mathf.Max(1, Mathf.RoundToInt(num4));
							float num10 = Height(num5 + num9, num6) - Height(num5 - num9, num6);
							float num11 = Height(num5, num6 + num9) - Height(num5, num6 - num9);
							float num12 = Mathf.Clamp(1f + (0f - num10 + num11) * 0.16f / (float)num9, 0.62f, 1.3f);
							a *= num12;
							if (t.PathSlot.Length > num7 && t.PathSlot[num7] != 0)
							{
								a = Color.Lerp(a, new Color(0.62f, 0.5f, 0.36f), 0.35f);
							}
							if (t.Materials[num7] == 6)
							{
								a = Color.Lerp(a, new Color(0.45f, 0.38f, 0.3f), 0.5f);
							}
						}
						if ((t.Flags[num7] & 2) != 0)
						{
							a = new Color(0.95f, 0.4f, 0.1f);
						}
					}
					array[i * num3 + j] = new Color32((byte)(Mathf.Clamp01(a.r) * 255f), (byte)(Mathf.Clamp01(a.g) * 255f), (byte)(Mathf.Clamp01(a.b) * 255f), byte.MaxValue);
				}
			}
			Terrain.SetPixels32(array);
			Terrain.Apply(updateMipmaps: false);
			float Height(int x, int y)
			{
				return t.Heights[Mathf.Clamp(y, 0, h - 1) * w + Mathf.Clamp(x, 0, w - 1)];
			}
		}

		public bool ToggleMark(Actor a)
		{
			if (a == null)
			{
				return false;
			}
			if (!Marked.Remove(a))
			{
				Marked.Add(a);
				return true;
			}
			return false;
		}

		public void MarkAll()
		{
			foreach (Unit unit in Units)
			{
				if (unit.Ours && unit.Warrior)
				{
					Marked.Add(unit.Actor);
				}
			}
		}

		public int MarkIn(Rect area)
		{
			int num = 0;
			foreach (Unit unit in Units)
			{
				if (unit.Ours && (unit.Warrior || unit.Ship) && area.Contains(unit.P) && Marked.Add(unit.Actor))
				{
					num++;
				}
			}
			return num;
		}

		public void Fit()
		{
			Zoom = 1f;
			Centre = new Vector2(RegionX + RegionSize * 0.5f, RegionY + RegionSize * 0.5f);
			Span = Mathf.Max(Span, 1f);
		}

		public List<Actor> MarkedOwn()
		{
			List<Actor> list = new List<Actor>();
			foreach (Actor item in Marked)
			{
				try
				{
					if (item != null && item.isAlive() && item.kingdom == Home && !IsShip(item) && !item.is_inside_boat)
					{
						list.Add(item);
					}
				}
				catch
				{
				}
			}
			return list;
		}

		public void Close()
		{
			Open = false;
		}

		public void TakeOffFlag(Actor a)
		{
			WarFlag warFlag = CommandOf(a);
			if (warFlag != null)
			{
				Leave(a, warFlag);
			}
		}

		public Actor TransportFor(Actor host, List<Actor> troops)
		{
			if (host == null)
			{
				return null;
			}
			List<Actor> list = Able(host, troops);
			if (list.Count == 0)
			{
				return null;
			}
			List<Actor> list2 = ByLand(list)[0];
			Vector2 mid = Middle(list2);
			WorldTile land = LandOf(list2[0]) ?? TileOf(list2[0]);
			List<Actor> list3 = new List<Actor>();
			foreach (Actor item in MarkedShips())
			{
				if (Free(item, host, busy: true))
				{
					list3.Add(item);
				}
			}
			if (list3.Count == 0)
			{
				try
				{
					foreach (Actor simple in World.world.units.getSimpleList())
					{
						if (simple != null && IsShip(simple) && simple.kingdom == host.kingdom && Free(simple, host, busy: false))
						{
							list3.Add(simple);
						}
					}
				}
				catch
				{
				}
			}
			list3.Sort((Actor a, Actor b) => (a.current_position - mid).sqrMagnitude.CompareTo((b.current_position - mid).sqrMagnitude));
			int num = 0;
			foreach (Actor item2 in list3)
			{
				if (ShoreWater(item2, mid, land, 48, null, out var _) != null)
				{
					return item2;
				}
				if (++num >= 8)
				{
					break;
				}
			}
			return null;
		}

		public static bool Transport(Actor ship)
		{
			try
			{
				return ship != null && ship.asset != null && ship.asset.is_boat && ship.asset.is_boat_transport && ship.getSimpleComponent<Boat>() != null;
			}
			catch
			{
				return false;
			}
		}

		private static Boat BoatOf(Actor ship)
		{
			try
			{
				return ship?.getSimpleComponent<Boat>();
			}
			catch
			{
				return null;
			}
		}

		public static int AboardCount(Actor ship)
		{
			Boat boat = BoatOf(ship);
			try
			{
				return boat?.countPassengers() ?? 0;
			}
			catch
			{
				return 0;
			}
		}

		public Lift LiftOf(Actor ship)
		{
			if (ship == null || !_liftOf.TryGetValue(ship, out var value))
			{
				return null;
			}
			return value;
		}

		public int Carrying(Actor ship)
		{
			Lift lift = LiftOf(ship);
			return AboardCount(ship) + (lift?.Boarding.Count ?? 0);
		}

		public bool Loaded(Actor ship)
		{
			if (LiftOf(ship) != null)
			{
				return Carrying(ship) > 0;
			}
			return false;
		}

		public int SeatsLeft(Actor ship)
		{
			return Mathf.Max(0, 50 - Carrying(ship));
		}

		public static bool CanWalkTo(Actor a, WorldTile t)
		{
			WorldTile worldTile = LandOf(a);
			if (worldTile == null)
			{
				return Reaches(a, t);
			}
			return SameLand(worldTile, t);
		}

		private static WorldTile LandOf(Actor a)
		{
			WorldTile worldTile = TileOf(a);
			if (worldTile == null || Royal.Standable(worldTile))
			{
				return worldTile;
			}
			WorldTile result = null;
			int num = int.MaxValue;
			for (int i = -2; i <= 2; i++)
			{
				for (int j = -2; j <= 2; j++)
				{
					int num2 = j * j + i * i;
					if (num2 < num)
					{
						WorldTile worldTile2 = null;
						try
						{
							worldTile2 = World.world.GetTile(worldTile.x + j, worldTile.y + i);
						}
						catch
						{
						}
						if (Royal.Standable(worldTile2))
						{
							result = worldTile2;
							num = num2;
						}
					}
				}
			}
			return result;
		}

		public int BySea(WarFlag f)
		{
			int num = 0;
			foreach (Lift lift in Lifts)
			{
				if (lift.Flag == f)
				{
					num += lift.Aboard + lift.Boarding.Count;
				}
			}
			return num;
		}

		private static string LiftDoing(Lift l, ShipOrder o)
		{
			if (l == null)
			{
				return null;
			}
			if (l.Now == Lift.Stage.Boarding && l.Boarding.Count > 0)
			{
				return "taking soldiers aboard (" + l.Boarding.Count + " to come)";
			}
			if (l.Now == Lift.Stage.Landing && l.Flag != null)
			{
				return "sailing to land her soldiers at flag " + l.Flag.Number;
			}
			if (l.Aboard > 0 && (o == null || o.What == ShipOrder.Kind.Sail))
			{
				return "at sea with " + Soldiers(l.Aboard) + " aboard";
			}
			return null;
		}

		public int Board(Royal royal, Actor host, List<Actor> troops, Actor ship, WarFlag flag, out int cantReach, out int noRoom, out string why)
		{
			cantReach = (noRoom = 0);
			why = null;
			_emptied.Clear();
			_left.Clear();
			if (host == null || !Free(ship, host, busy: true))
			{
				why = ((host != null && Transport(ship) && SeatsLeft(ship) <= 0) ? "She is full" : "She can't take anyone aboard");
				return 0;
			}
			BeginFleet(host.kingdom);
			List<Actor> list = Able(host, troops);
			if (list.Count == 0)
			{
				why = "None of your marked warriors can go aboard";
				return 0;
			}
			List<List<Actor>> list2 = ByLand(list);
			List<Actor> list3 = list2[0];
			Lift lift = LiftOf(ship);
			if (lift != null && lift.Now == Lift.Stage.Boarding && lift.Boarding.Count > 0 && lift.Shore != null)
			{
				foreach (List<Actor> item in list2)
				{
					if (SameLand(LandOf(item[0]) ?? TileOf(item[0]), lift.Shore))
					{
						list3 = item;
						break;
					}
				}
			}
			foreach (List<Actor> item2 in list2)
			{
				if (item2 != list3)
				{
					cantReach += item2.Count;
				}
			}
			int num = Embark(royal, ship, list3, flag, out why);
			noRoom = ((num > 0) ? list3.Count : 0);
			return num;
		}

		public int Ferry(Royal royal, Actor host, List<Actor> troops, WarFlag flag, List<Actor> prefer, out int noShip, out int ships)
		{
			noShip = (ships = 0);
			if (host == null || flag == null)
			{
				return 0;
			}
			List<Actor> list = Able(host, troops);
			noShip = troops.Count - list.Count;
			if (list.Count == 0)
			{
				return 0;
			}
			BeginFleet(host.kingdom);
			int num = 0;
			HashSet<Actor> hashSet = new HashSet<Actor>();
			foreach (List<Actor> item in ByLand(list))
			{
				List<Actor> list2 = new List<Actor>(item);
				while (list2.Count > 0)
				{
					Actor actor = FerryFor(host, list2, flag, prefer, hashSet);
					if (actor == null)
					{
						break;
					}
					hashSet.Add(actor);
					bool flag2 = LiftOf(actor) == null && OrderOf(actor) == null && (prefer == null || !prefer.Contains(actor));
					string why;
					int num2 = Embark(royal, actor, list2, flag, out why);
					if (num2 != 0)
					{
						if (flag2 && LiftOf(actor) != null)
						{
							LiftOf(actor).Auto = true;
						}
						num += num2;
						ships++;
					}
				}
				noShip += list2.Count;
				foreach (Actor item2 in list2)
				{
					WorldTile worldTile = TileOf(item2);
					WorldTile worldTile2 = LandOf(item2);
					string text = ((worldTile != null && worldTile.Type != null) ? worldTile.Type.id : "?");
					Log.Info("ferry: no ship for " + WorldBoxMod.Describe(item2) + " at " + ((worldTile != null) ? (worldTile.x + "," + worldTile.y) : "?") + " (" + text + ", land " + ((worldTile2 != null) ? (worldTile2.x + "," + worldTile2.y) : "none") + ", " + $"{item.Count} on that land)");
				}
			}
			return num;
		}

		public bool LandAt(Actor ship, WarFlag flag)
		{
			Lift lift = LiftOf(ship);
			if (lift == null || flag == null)
			{
				return false;
			}
			WorldTile ground;
			WorldTile worldTile = ShoreWater(ship, flag.P, flag.Tile, 40, lift, out ground);
			if (worldTile == null)
			{
				return false;
			}
			lift.Flag = flag;
			lift.Drop = worldTile;
			lift.Beach = ground;
			lift.DropFor = flag.Tile;
			lift.Auto = false;
			if (lift.Now != Lift.Stage.Boarding || lift.Boarding.Count == 0)
			{
				StartLanding(lift, WorldClock());
			}
			return true;
		}

		public void ClearLifts()
		{
			for (int num = Lifts.Count - 1; num >= 0; num--)
			{
				EndLift(Lifts[num]);
			}
			_boardSentAt.Clear();
		}

		private bool KeepAboard(Actor ship)
		{
			Lift lift = LiftOf(ship);
			if (lift == null)
			{
				return false;
			}
			if (AboardCount(ship) == 0)
			{
				EndLift(lift);
				return false;
			}
			foreach (Actor item in lift.Boarding)
			{
				Royal.StopBoarding(item);
				_boardSentAt.Remove(item);
			}
			lift.Boarding.Clear();
			lift.Now = Lift.Stage.Aboard;
			lift.Flag = null;
			lift.Auto = false;
			Steer(ship, SeaTile(ship));
			return true;
		}

		private int Embark(Royal royal, Actor ship, List<Actor> group, WarFlag flag, out string why)
		{
			why = null;
			Lift lift = LiftOf(ship);
			if (lift != null)
			{
				lift.Auto = false;
			}
			if (group.Count == 0)
			{
				return 0;
			}
			int num = SeatsLeft(ship);
			if (num <= 0)
			{
				why = "She is full";
				return 0;
			}
			Lift lift2 = LiftOf(ship);
			WorldTile worldTile = LandOf(group[0]) ?? TileOf(group[0]);
			WorldTile worldTile2;
			WorldTile ground;
			if (lift2 != null && lift2.Now == Lift.Stage.Boarding && lift2.Boarding.Count > 0 && lift2.Shore != null && SameLand(lift2.Shore, worldTile))
			{
				worldTile2 = lift2.Pickup;
				ground = lift2.Shore;
			}
			else
			{
				if (lift2 != null && lift2.Boarding.Count > 0)
				{
					why = "She is taking others aboard on another shore";
					return 0;
				}
				worldTile2 = ShoreWater(ship, Middle(group), worldTile, 48, lift2, out ground);
				if (worldTile2 == null)
				{
					why = "She can't reach their shore";
					return 0;
				}
			}
			Vector2 at = Mid(ground);
			group.Sort((Actor a, Actor b) => (a.current_position - at).sqrMagnitude.CompareTo((b.current_position - at).sqrMagnitude));
			if (lift2 == null)
			{
				lift2 = new Lift
				{
					Ship = ship
				};
				Lifts.Add(lift2);
				_liftOf[ship] = lift2;
				FreeOfTheGame(ship);
			}
			if (lift2.Now != Lift.Stage.Boarding || lift2.Pickup != worldTile2)
			{
				lift2.ArrivedAt = -1f;
				lift2.StageAt = WorldClock();
				lift2.MovedAt = -1f;
			}
			lift2.Now = Lift.Stage.Boarding;
			lift2.Pickup = worldTile2;
			lift2.Shore = ground;
			if (flag != null && lift2.Flag != flag)
			{
				lift2.Flag = flag;
				lift2.Drop = (lift2.Beach = (lift2.DropFor = null));
			}
			float spread = BoardSpread(lift2.Boarding.Count + Mathf.Min(num, group.Count));
			int num2 = 0;
			float unscaledTime = Time.unscaledTime;
			for (int num3 = 0; num3 < group.Count; num3++)
			{
				if (num2 >= num)
				{
					break;
				}
				Actor actor = group[num3];
				WarFlag warFlag = FlagOf(actor);
				if (warFlag != null)
				{
					Leave(actor, warFlag);
				}
				royal?.LeaveCampaign(actor);
				foreach (Lift lift3 in Lifts)
				{
					if (lift3 != lift2)
					{
						lift3.Boarding.Remove(actor);
					}
				}
				if (!lift2.Boarding.Contains(actor))
				{
					lift2.Boarding.Add(actor);
				}
				if (Royal.Board(actor, ground, spread))
				{
					_boardSentAt[actor] = unscaledTime;
				}
				num2++;
			}
			group.RemoveRange(0, num2);
			TakeDownEmptied(flag);
			Steer(ship, worldTile2);
			return num2;
		}

		private static float BoardSpread(int count)
		{
			return Mathf.Clamp(1f + Mathf.Sqrt(Mathf.Max(1, count)) * 0.35f, 1.5f, 3f);
		}

		private static List<Actor> Able(Actor host, List<Actor> troops)
		{
			List<Actor> list = new List<Actor>();
			foreach (Actor troop in troops)
			{
				try
				{
					if (troop != null && troop.isAlive() && troop != host && troop.ai != null && troop.kingdom == host.kingdom && !IsShip(troop) && !troop.is_inside_boat && !BoatHelm.Steers(troop))
					{
						list.Add(troop);
					}
				}
				catch
				{
				}
			}
			return list;
		}

		private bool Free(Actor ship, Actor host, bool busy)
		{
			try
			{
				if (ship == null || !ship.isAlive() || ship == host || ship.ai == null || ship.kingdom != host.kingdom || !Transport(ship) || BoatHelm.Steers(ship))
				{
					return false;
				}
				if (SeatsLeft(ship) <= 0)
				{
					return false;
				}
				if (busy)
				{
					return true;
				}
				if (LiftOf(ship) != null)
				{
					return false;
				}
				ShipOrder shipOrder = OrderOf(ship);
				if (shipOrder != null && shipOrder.What == ShipOrder.Kind.Bombard)
				{
					return false;
				}
				Boat boat = BoatOf(ship);
				return boat != null && !boat.hasPassengers() && boat.taxi_request == null;
			}
			catch
			{
				return false;
			}
		}

		private Actor FerryFor(Actor host, List<Actor> group, WarFlag flag, List<Actor> prefer, HashSet<Actor> used)
		{
			Vector2 mid = Middle(group);
			WorldTile land = LandOf(group[0]) ?? TileOf(group[0]);
			List<Actor> list = new List<Actor>();
			if (prefer != null)
			{
				foreach (Actor item in prefer)
				{
					if (!used.Contains(item) && Free(item, host, busy: true) && LiftOf(item) == null)
					{
						list.Add(item);
					}
				}
			}
			Actor actor = Nearest(list, mid, land, flag);
			if (actor != null)
			{
				return actor;
			}
			List<Actor> list2 = new List<Actor>();
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && IsShip(simple) && simple.kingdom == host.kingdom && !used.Contains(simple) && Free(simple, host, busy: false))
					{
						list2.Add(simple);
					}
				}
			}
			catch
			{
			}
			return Nearest(list2, mid, land, flag);
		}

		private Actor Nearest(List<Actor> ships, Vector2 mid, WorldTile land, WarFlag flag)
		{
			ships.Sort((Actor a, Actor b) => (a.current_position - mid).sqrMagnitude.CompareTo((b.current_position - mid).sqrMagnitude));
			List<KeyValuePair<WorldTile, bool>> list = new List<KeyValuePair<WorldTile, bool>>();
			foreach (Actor ship in ships)
			{
				WorldTile worldTile = SeaTile(ship);
				bool flag2 = false;
				bool flag3 = false;
				foreach (KeyValuePair<WorldTile, bool> item in list)
				{
					if (SameSea(item.Key, worldTile) && Water(item.Key) && Water(worldTile))
					{
						flag2 = true;
						flag3 = item.Value;
						break;
					}
				}
				if (!flag2)
				{
					flag3 = ShoreWater(ship, mid, land, 48, null, out var ground) != null && ShoreWater(ship, flag.P, flag.Tile, 40, null, out ground) != null;
					if (worldTile != null)
					{
						list.Add(new KeyValuePair<WorldTile, bool>(worldTile, flag3));
					}
				}
				if (flag3)
				{
					return ship;
				}
			}
			return null;
		}

		private void Steer(Actor ship, WorldTile to)
		{
			if (to != null)
			{
				ShipOrder shipOrder = Order(ship);
				shipOrder.What = ShipOrder.Kind.Sail;
				shipOrder.To = to;
				shipOrder.Target = null;
				shipOrder.Town = null;
				shipOrder.TargetName = null;
				shipOrder.Holding = null;
				if (Royal.Sail(ship, to))
				{
					shipOrder.OrderedAt = Time.unscaledTime;
				}
				_fleetUpkeep = 0f;
			}
		}

		private static void FreeOfTheGame(Actor ship)
		{
			Boat boat = BoatOf(ship);
			if (boat == null)
			{
				return;
			}
			try
			{
				if (boat.taxi_request != null)
				{
					TaxiManager.finish(boat.taxi_request);
					boat.taxi_request = null;
				}
				boat.taxi_target = null;
			}
			catch (Exception ex)
			{
				Log.Error("ferry: freeing a ship: " + ex.Message);
			}
		}

		private void EndLift(Lift l)
		{
			foreach (Actor item in l.Boarding)
			{
				Royal.StopBoarding(item);
				_boardSentAt.Remove(item);
			}
			l.Boarding.Clear();
			Lifts.Remove(l);
			if (l.Ship != null)
			{
				_liftOf.Remove(l.Ship);
			}
			if (l.Flag != null)
			{
				l.Flag.AtSea = BySea(l.Flag);
			}
		}

		private static Vector2 Mid(WorldTile t)
		{
			return new Vector2((float)t.x + 0.5f, (float)t.y + 0.5f);
		}

		private static WorldTile TileOf(Actor a)
		{
			try
			{
				return World.world.GetTile(Mathf.FloorToInt(a.current_position.x), Mathf.FloorToInt(a.current_position.y)) ?? a.current_tile;
			}
			catch
			{
				return null;
			}
		}

		private static WorldTile SeaTile(Actor ship)
		{
			try
			{
				return ship.current_tile;
			}
			catch
			{
				return null;
			}
		}

		private static bool SameLand(WorldTile a, WorldTile b)
		{
			try
			{
				return a == b || (a != null && b != null && a.isSameIsland(b));
			}
			catch
			{
				return false;
			}
		}

		private static Vector2 Middle(List<Actor> group)
		{
			Vector2 zero = Vector2.zero;
			foreach (Actor item in group)
			{
				zero += item.current_position;
			}
			if (group.Count <= 0)
			{
				return zero;
			}
			return zero / group.Count;
		}

		private static List<List<Actor>> ByLand(List<Actor> troops)
		{
			List<List<Actor>> list = new List<List<Actor>>();
			List<WorldTile> list2 = new List<WorldTile>();
			foreach (Actor troop in troops)
			{
				WorldTile worldTile = LandOf(troop) ?? TileOf(troop);
				int num = -1;
				for (int i = 0; i < list2.Count; i++)
				{
					if (num >= 0)
					{
						break;
					}
					if (SameLand(list2[i], worldTile))
					{
						num = i;
					}
				}
				if (num < 0)
				{
					list.Add(new List<Actor>());
					list2.Add(worldTile);
					num = list.Count - 1;
				}
				list[num].Add(troop);
			}
			list.Sort((List<Actor> x, List<Actor> y) => y.Count.CompareTo(x.Count));
			return list;
		}

		private static WorldTile GroundBeside(WorldTile water, WorldTile land)
		{
			try
			{
				WorldTile[] neighboursAll = water.neighboursAll;
				foreach (WorldTile worldTile in neighboursAll)
				{
					if (worldTile != null && Royal.Standable(worldTile) && SameLand(worldTile, land))
					{
						return worldTile;
					}
				}
			}
			catch
			{
			}
			return null;
		}

		private static bool GoodForBoat(WorldTile t)
		{
			try
			{
				return Water(t) && t.isGoodForBoat();
			}
			catch
			{
				return false;
			}
		}

		private WorldTile ShoreWater(Actor ship, Vector2 p, WorldTile land, int radius, Lift self, out WorldTile ground)
		{
			ground = null;
			if (land == null)
			{
				return null;
			}
			WorldTile worldTile = SeaTile(ship);
			WorldTile worldTile2 = null;
			int num = Mathf.FloorToInt(p.x);
			int num2 = Mathf.FloorToInt(p.y);
			float num3 = float.MaxValue;
			for (int i = 0; i <= radius && (worldTile2 == null || !((float)(i * i) > num3)); i++)
			{
				for (int j = -i; j <= i; j++)
				{
					int num4 = ((j == -i || j == i) ? 1 : Mathf.Max(1, 2 * i));
					for (int k = -i; k <= i; k += num4)
					{
						WorldTile worldTile3 = null;
						try
						{
							worldTile3 = World.world.GetTile(num + k, num2 + j);
						}
						catch
						{
						}
						if (worldTile3 == null || !GoodForBoat(worldTile3) || !SameSea(worldTile, worldTile3))
						{
							continue;
						}
						float num5 = (float)(k * k + j * j) + Crowded(worldTile3, self);
						if (!(num5 >= num3))
						{
							WorldTile worldTile4 = GroundBeside(worldTile3, land);
							if (worldTile4 != null)
							{
								worldTile2 = worldTile3;
								ground = worldTile4;
								num3 = num5;
							}
						}
					}
				}
			}
			return worldTile2;
		}

		private float Crowded(WorldTile t, Lift self)
		{
			foreach (Lift lift in Lifts)
			{
				if (lift != self)
				{
					WorldTile worldTile = ((lift.Now == Lift.Stage.Boarding) ? lift.Pickup : lift.Drop);
					if (worldTile != null && Mathf.Abs(worldTile.x - t.x) + Mathf.Abs(worldTile.y - t.y) < 4)
					{
						return 60f;
					}
				}
			}
			return 0f;
		}

		private void UpkeepLifts(Actor you, float now, Action<string> toast)
		{
			if (Lifts.Count == 0)
			{
				return;
			}
			_liftNews.Clear();
			float clock = WorldClock();
			for (int num = Lifts.Count - 1; num >= 0; num--)
			{
				Lift lift = Lifts[num];
				Actor ship = lift.Ship;
				bool flag;
				bool flag2;
				try
				{
					flag = ship?.isAlive() ?? false;
					flag2 = flag && ship.kingdom == _fleetOf;
				}
				catch
				{
					flag = (flag2 = false);
				}
				if (!flag2)
				{
					if (!flag)
					{
						FreeRiders(lift);
					}
					if (lift.Aboard > 0)
					{
						_liftNews.Add(flag ? ("A ship with " + Soldiers(lift.Aboard) + " aboard is lost to you") : ("A ship goes down with " + Soldiers(lift.Aboard) + " aboard"));
					}
					EndLift(lift);
				}
				else
				{
					Remember(lift, ship);
					if (ship != you)
					{
						try
						{
							ShipOrder shipOrder = OrderOf(ship);
							lift.Aboard = AboardCount(ship);
							if (shipOrder == null)
							{
								EndLift(lift);
							}
							else
							{
								if (lift.Flag != null && !Flags.Contains(lift.Flag))
								{
									int number = lift.Flag.Number;
									lift.Flag = null;
									lift.Drop = (lift.Beach = (lift.DropFor = null));
									if (lift.Now == Lift.Stage.Landing)
									{
										lift.Now = Lift.Stage.Aboard;
										Steer(ship, ship.current_tile);
										if (lift.Aboard > 0)
										{
											_liftNews.Add("Flag " + number + " is down. " + Soldiers(lift.Aboard) + " wait aboard");
										}
									}
								}
								if (lift.Now == Lift.Stage.Boarding)
								{
									KeepBoarding(lift, ship, shipOrder, you, now, clock);
								}
								else if (lift.Now == Lift.Stage.Landing)
								{
									KeepLanding(lift, ship, shipOrder, now, clock);
								}
								else if (lift.Aboard == 0)
								{
									EndLift(lift);
								}
							}
						}
						catch (Exception ex)
						{
							Log.Error("ferry: " + ex.Message);
							EndLift(lift);
						}
					}
				}
			}
			foreach (WarFlag flag3 in Flags)
			{
				flag3.AtSea = BySea(flag3);
			}
			foreach (string item in _liftNews)
			{
				Log.Info("ferry: " + item);
				toast?.Invoke(item);
			}
		}

		private static string Soldiers(int n)
		{
			if (n != 1)
			{
				return n + " soldiers";
			}
			return "1 soldier";
		}

		private static float WorldClock()
		{
			return (float)WorldPace.ActionNow;
		}

		private static void Remember(Lift l, Actor ship)
		{
			l.Riders.Clear();
			Boat boat = BoatOf(ship);
			try
			{
				if (boat != null)
				{
					l.Riders.AddRange(boat.getPassengers());
				}
			}
			catch
			{
			}
			l.LastTile = SeaTile(ship);
		}

		private static int FreeRiders(Lift l)
		{
			int num = 0;
			WorldTile lastTile = l.LastTile;
			foreach (Actor rider in l.Riders)
			{
				try
				{
					if (rider != null && rider.isAlive() && rider.is_inside_boat && lastTile != null)
					{
						rider.spawnOn(lastTile, 0f);
						rider.data.transportID = -1L;
						rider.exitBoat();
						rider.setTask("short_move");
						num++;
					}
				}
				catch (Exception ex)
				{
					Log.Error("ferry: freeing a soldier: " + ex.Message);
				}
			}
			l.Riders.Clear();
			if (num > 0)
			{
				Log.Info("ferry: " + num + " put out of a ship that was gone without sinking");
			}
			return num;
		}

		private void KeepBoarding(Lift l, Actor s, ShipOrder o, Actor you, float now, float clock)
		{
			if (o.What != ShipOrder.Kind.Sail || o.To != l.Pickup)
			{
				int count = l.Boarding.Count;
				foreach (Actor item in l.Boarding)
				{
					Royal.StopBoarding(item);
					_boardSentAt.Remove(item);
				}
				l.Boarding.Clear();
				if (count > 0)
				{
					_liftNews.Add(Soldiers(count) + " stay ashore");
				}
				if (l.Aboard == 0)
				{
					EndLift(l);
					return;
				}
				l.Now = Lift.Stage.Aboard;
				l.Flag = null;
				l.Auto = false;
				return;
			}
			Boat boat = BoatOf(s);
			float magnitude = (s.current_position - Mid(l.Pickup)).magnitude;
			bool flag = magnitude <= 4.5f || (magnitude <= 8f && !s._is_moving && clock - l.StageAt > 3f);
			float a = ((magnitude <= 3f) ? 4.5f : 8f);
			if (flag && l.ArrivedAt < 0f)
			{
				l.ArrivedAt = clock;
			}
			if (!flag)
			{
				Unstick(l, s, l.Pickup, clock);
			}
			if (!flag && l.Aboard == 0 && clock - l.StageAt > 150f)
			{
				_liftNews.Add("Your ship can't reach their shore");
				EndLift(l);
				return;
			}
			float spread = BoardSpread(l.Boarding.Count);
			for (int num = l.Boarding.Count - 1; num >= 0; num--)
			{
				Actor actor = l.Boarding[num];
				bool flag2;
				try
				{
					flag2 = actor != null && actor.isAlive() && actor.kingdom == _fleetOf;
				}
				catch
				{
					flag2 = false;
				}
				WorldTile to;
				if (!flag2 || actor.is_inside_boat)
				{
					l.Boarding.RemoveAt(num);
					if (actor != null)
					{
						_boardSentAt.Remove(actor);
						if (!actor.is_inside_boat)
						{
							Royal.StopBoarding(actor);
						}
					}
				}
				else if (actor != you && (Royal.IsHolding(actor) || Royal.IsMarchingTo(actor, out to)))
				{
					l.Boarding.RemoveAt(num);
					_boardSentAt.Remove(actor);
				}
				else if (actor != you)
				{
					bool flag3 = false;
					try
					{
						flag3 = actor.isFighting();
					}
					catch
					{
					}
					if (!flag3)
					{
						float value;
						if (flag && boat != null && !actor.is_inside_building && ((actor.current_position - s.current_position).magnitude <= Mathf.Max(a, 9f) || (actor.current_position - Mid(l.Shore)).magnitude <= 9f))
						{
							actor.embarkInto(boat);
							l.Boarding.RemoveAt(num);
							_boardSentAt.Remove(actor);
						}
						else if (!Royal.IsBoarding(actor) && (!_boardSentAt.TryGetValue(actor, out value) || !(now - value < 1.5f)) && Royal.Board(actor, l.Shore, spread))
						{
							_boardSentAt[actor] = now;
						}
					}
				}
			}
			l.Aboard = AboardCount(s);
			if (flag && l.Boarding.Count > 0 && clock - l.ArrivedAt > 25f)
			{
				int count2 = l.Boarding.Count;
				foreach (Actor item2 in l.Boarding)
				{
					Royal.StopBoarding(item2);
					_boardSentAt.Remove(item2);
				}
				l.Boarding.Clear();
				_liftNews.Add(Soldiers(count2) + " didn't reach the ship in time");
			}
			if (l.Boarding.Count <= 0)
			{
				if (l.Aboard == 0)
				{
					EndLift(l);
					return;
				}
				if (l.Flag != null)
				{
					StartLanding(l, clock);
					return;
				}
				l.Now = Lift.Stage.Aboard;
				l.StageAt = clock;
				_liftNews.Add(Soldiers(l.Aboard) + " aboard. Mark the ship and right-click land to put them ashore");
			}
		}

		private static void Unstick(Lift l, Actor s, WorldTile to, float clock)
		{
			Vector2 current_position = s.current_position;
			if (l.MovedAt < 0f || (current_position - l.MovedFrom).sqrMagnitude > 1f)
			{
				l.MovedFrom = current_position;
				l.MovedAt = clock;
			}
			else if (!(clock - l.MovedAt < 8f) && to != null)
			{
				l.MovedAt = clock;
				Log.Info($"ferry: {WorldBoxMod.Describe(s)} has made no way at {current_position.x:F0},{current_position.y:F0} on her way to {to.x},{to.y}: sent again");
				Royal.Sail(s, to);
			}
		}

		private void StartLanding(Lift l, float clock)
		{
			if (l.Flag == null)
			{
				l.Now = Lift.Stage.Aboard;
				return;
			}
			if (l.Drop == null || l.DropFor != l.Flag.Tile)
			{
				l.Drop = ShoreWater(l.Ship, l.Flag.P, l.Flag.Tile, 40, l, out l.Beach);
				l.DropFor = l.Flag.Tile;
			}
			if (l.Drop == null)
			{
				_liftNews.Add("Your ship can't reach the shore at flag " + l.Flag.Number + ". " + Soldiers(l.Aboard) + " wait aboard");
				l.Flag = null;
				l.Now = Lift.Stage.Aboard;
			}
			else
			{
				l.Now = Lift.Stage.Landing;
				l.StageAt = clock;
				l.ArrivedAt = -1f;
				l.MovedAt = -1f;
				Steer(l.Ship, l.Drop);
			}
		}

		private void KeepLanding(Lift l, Actor s, ShipOrder o, float now, float clock)
		{
			if (l.Aboard == 0)
			{
				EndLift(l);
				return;
			}
			if (l.DropFor != l.Flag.Tile)
			{
				l.Drop = null;
				StartLanding(l, clock);
				if (l.Now != Lift.Stage.Landing)
				{
					return;
				}
			}
			if (o.What != ShipOrder.Kind.Sail || o.To != l.Drop)
			{
				l.Now = Lift.Stage.Aboard;
				l.Flag = null;
				l.Auto = false;
				return;
			}
			float magnitude = (s.current_position - Mid(l.Drop)).magnitude;
			if (magnitude > 2.5f && (!(magnitude <= 7f) || s._is_moving || !(clock - l.StageAt > 6f)))
			{
				Unstick(l, s, l.Drop, clock);
				if (clock - l.StageAt > 150f)
				{
					_liftNews.Add("Your ship can't reach the shore at flag " + l.Flag.Number + ". " + Soldiers(l.Aboard) + " wait aboard");
					l.Now = Lift.Stage.Aboard;
					l.Flag = null;
				}
				return;
			}
			if (l.ArrivedAt < 0f)
			{
				l.ArrivedAt = clock;
			}
			WorldTile worldTile = GroundBeside(s.current_tile, l.Flag.Tile);
			if (worldTile == null && l.Beach != null && (s.current_position - Mid(l.Beach)).magnitude <= 8f)
			{
				worldTile = l.Beach;
			}
			if (worldTile == null)
			{
				if (clock - l.ArrivedAt > 10f)
				{
					_liftNews.Add("No beach for your soldiers at flag " + l.Flag.Number + ". They wait aboard");
					l.Now = Lift.Stage.Aboard;
					l.Flag = null;
				}
				return;
			}
			Boat boat = BoatOf(s);
			if (boat == null)
			{
				return;
			}
			List<Actor> list = new List<Actor>(boat.getPassengers());
			boat.unloadPassengers(worldTile, pRandomForce: false);
			WarFlag flag = l.Flag;
			int num = 0;
			foreach (Actor item in list)
			{
				bool flag2;
				try
				{
					flag2 = item != null && item.isAlive() && item.kingdom == _fleetOf && !item.is_inside_boat;
				}
				catch
				{
					flag2 = false;
				}
				if (flag2)
				{
					JoinFlag(item, flag, now);
					num++;
				}
			}
			if (num > 0)
			{
				flag.HadBand = true;
				flag.ToldFallen = false;
			}
			_upkeep = 0f;
			_liftNews.Add(Soldiers(num) + " land at flag " + flag.Number);
			bool auto = l.Auto;
			EndLift(l);
			if (auto)
			{
				ShipOrder shipOrder = OrderOf(s);
				if (shipOrder != null)
				{
					Drop(shipOrder);
					Royal.ReleaseShip(s);
				}
			}
		}

		private void JoinFlag(Actor a, WarFlag f, float now)
		{
			WarFlag warFlag = FlagOf(a);
			if (warFlag != f)
			{
				if (warFlag != null)
				{
					Leave(a, warFlag);
				}
				f.Band.Add(a);
				_bandOf[a] = f;
			}
			_freeUntil.Remove(a);
			if (Royal.Hold(a, f.Tile, Spread(f.Band.Count)))
			{
				_orderedAt[a] = now;
			}
		}

		public static Color FlagColor(int number)
		{
			return FlagColors[((number - 1) % FlagColors.Length + FlagColors.Length) % FlagColors.Length];
		}

		public WarFlag FlagOf(Actor a)
		{
			if (a == null || !_bandOf.TryGetValue(a, out var value) || value.Quick)
			{
				return null;
			}
			return value;
		}

		public WarFlag CommandOf(Actor a)
		{
			if (a == null || !_bandOf.TryGetValue(a, out var value))
			{
				return null;
			}
			return value;
		}

		public WarFlag FlagNumbered(int n)
		{
			foreach (WarFlag flag in Flags)
			{
				if (flag.Number == n)
				{
					return flag;
				}
			}
			return null;
		}

		public static WorldTile GroundFor(WorldTile t)
		{
			if (t == null)
			{
				return null;
			}
			if (Royal.Standable(t))
			{
				return t;
			}
			WorldTile result = null;
			int num = int.MaxValue;
			for (int i = -4; i <= 4; i++)
			{
				for (int j = -4; j <= 4; j++)
				{
					int num2 = j * j + i * i;
					if (num2 < num && num2 <= 16)
					{
						WorldTile worldTile = null;
						try
						{
							worldTile = World.world.GetTile(t.x + j, t.y + i);
						}
						catch
						{
						}
						if (Royal.Standable(worldTile))
						{
							result = worldTile;
							num = num2;
						}
					}
				}
			}
			return result;
		}

		public WarFlag Plant(WorldTile t, Kingdom k, int number = 0)
		{
			if (t == null)
			{
				return null;
			}
			OrdersFor(k);
			if (Flags.Count >= 6)
			{
				return null;
			}
			int i;
			for (i = ((number < 1 || number > 6 || FlagNumbered(number) != null) ? 1 : number); FlagNumbered(i) != null; i++)
			{
			}
			WarFlag warFlag = new WarFlag
			{
				Number = i,
				Tile = t,
				Born = Time.unscaledTime
			};
			Flags.Add(warFlag);
			Flags.Sort((WarFlag a, WarFlag b) => a.Number.CompareTo(b.Number));
			return warFlag;
		}

		public void PullUp(WarFlag f)
		{
			if (f == null)
			{
				return;
			}
			foreach (Actor item in f.Band)
			{
				Forget(item);
				Royal.StandDown(item);
			}
			f.Band.Clear();
			Flags.Remove(f);
			Posts.Remove(f);
			if (SelectedFlag == f)
			{
				SelectedFlag = null;
			}
		}

		public void ClearFlags()
		{
			for (int num = Flags.Count - 1; num >= 0; num--)
			{
				PullUp(Flags[num]);
			}
			for (int num2 = Posts.Count - 1; num2 >= 0; num2--)
			{
				PullUp(Posts[num2]);
			}
			_bandOf.Clear();
			_orderedAt.Clear();
			_freeUntil.Clear();
			ClearGroups();
			SelectedFlag = null;
		}

		public static float Spread(int count)
		{
			return Mathf.Clamp(1.5f + Mathf.Sqrt(Mathf.Max(1, count)) * 0.6f, 2f, 7f);
		}

		public int Assign(Royal royal, Actor host, List<Actor> warriors, WarFlag f, out int cantReach)
		{
			cantReach = 0;
			_emptied.Clear();
			_left.Clear();
			if (f == null || host == null)
			{
				return 0;
			}
			float spread = Spread(f.Band.Count + warriors.Count);
			int num = 0;
			foreach (Actor warrior in warriors)
			{
				bool flag;
				try
				{
					flag = warrior != null && warrior.isAlive() && warrior != host && warrior.ai != null && warrior.kingdom == host.kingdom;
				}
				catch
				{
					flag = false;
				}
				if (!flag)
				{
					continue;
				}
				if (!Reaches(warrior, f.Tile))
				{
					cantReach++;
					continue;
				}
				WarFlag warFlag = CommandOf(warrior);
				if (warFlag != f)
				{
					if (warFlag != null)
					{
						Leave(warrior, warFlag);
					}
					f.Band.Add(warrior);
					_bandOf[warrior] = f;
				}
				if (!f.Quick)
				{
					JoinGroup(warrior, f.Number);
				}
				royal?.LeaveCampaign(warrior);
				_freeUntil.Remove(warrior);
				try
				{
					if (warrior.has_attack_target)
					{
						warrior.clearAttackTarget();
					}
				}
				catch
				{
				}
				if (Royal.Hold(warrior, f.Tile, spread))
				{
					_orderedAt[warrior] = Time.unscaledTime - 2f;
				}
				num++;
			}
			if (num > 0)
			{
				f.HadBand = true;
				f.ToldFallen = false;
			}
			_upkeep = 0f;
			TakeDownEmptied(f);
			return num;
		}

		public void Move(WarFlag f, WorldTile t)
		{
			if (f == null || t == null)
			{
				return;
			}
			f.Tile = t;
			f.Born = Time.unscaledTime;
			float spread = Spread(f.Band.Count);
			foreach (Actor item in f.Band)
			{
				if (Royal.Hold(item, t, spread))
				{
					_orderedAt[item] = Time.unscaledTime;
				}
			}
			_upkeep = 0f;
		}

		public int StandDown(List<Actor> warriors)
		{
			_emptied.Clear();
			_left.Clear();
			int num = 0;
			foreach (Actor warrior in warriors)
			{
				WarFlag warFlag = CommandOf(warrior);
				if (warFlag != null)
				{
					Leave(warrior, warFlag);
					Royal.StandDown(warrior);
					num++;
				}
			}
			TakeDownEmptied(null);
			return num;
		}

		public WarFlag WholeBand(List<Actor> own)
		{
			if (own.Count == 0)
			{
				return null;
			}
			WarFlag warFlag = FlagOf(own[0]);
			if (warFlag == null || warFlag.Band.Count != own.Count)
			{
				return null;
			}
			foreach (Actor item in own)
			{
				if (FlagOf(item) != warFlag)
				{
					return null;
				}
			}
			return warFlag;
		}

		public int MarkBand(WarFlag f, bool add)
		{
			if (!add)
			{
				Marked.Clear();
			}
			if (f == null)
			{
				return 0;
			}
			foreach (Actor item in f.Band)
			{
				Marked.Add(item);
			}
			return f.Band.Count;
		}

		private void Leave(Actor a, WarFlag f)
		{
			f.Band.Remove(a);
			Forget(a);
			if (!_left.Contains(f))
			{
				_left.Add(f);
			}
		}

		private void Forget(Actor a)
		{
			_bandOf.Remove(a);
			_orderedAt.Remove(a);
			_freeUntil.Remove(a);
		}

		private bool FreeToEat(Actor a, float now)
		{
			bool flag;
			try
			{
				flag = a.isHungry();
			}
			catch
			{
				flag = false;
			}
			if (_freeUntil.TryGetValue(a, out var value))
			{
				if (now < value)
				{
					return true;
				}
				if (now < value + 90f)
				{
					return false;
				}
				_freeUntil.Remove(a);
			}
			if (!flag)
			{
				return false;
			}
			_freeUntil[a] = now + 40f;
			return true;
		}

		private void TakeDownEmptied(WarFlag except)
		{
			foreach (WarFlag item in _left)
			{
				if (item == except || item.Band.Count > 0)
				{
					continue;
				}
				if (item.Quick)
				{
					Posts.Remove(item);
					continue;
				}
				if (Flags.Remove(item))
				{
					_emptied.Add(item);
				}
				if (SelectedFlag == item)
				{
					SelectedFlag = null;
				}
			}
			_left.Clear();
		}

		private static bool Reaches(Actor a, WorldTile t)
		{
			try
			{
				WorldTile current_tile = a.current_tile;
				return current_tile == null || current_tile == t || current_tile.isSameIsland(t) || !Royal.Standable(current_tile);
			}
			catch
			{
				return true;
			}
		}

		public void UpkeepFlags(Actor you, float dt, Action<string> toast)
		{
			if (Flags.Count == 0 && Posts.Count == 0)
			{
				return;
			}
			_upkeep -= dt;
			if (_upkeep > 0f)
			{
				return;
			}
			_upkeep = 0.5f;
			WarFlag warFlag = ((Flags.Count > 0) ? Flags[0] : Posts[0]);
			bool flag;
			try
			{
				flag = _flagsOf == null || !_flagsOf.isAlive() || !World.world.kingdoms.list.Contains(_flagsOf) || World.world.GetTile(warFlag.Tile.x, warFlag.Tile.y) != warFlag.Tile || (_flagsKing >= 0 && KingId(_flagsOf) != _flagsKing);
			}
			catch
			{
				flag = true;
			}
			if (flag)
			{
				Flags.Clear();
				Posts.Clear();
				_bandOf.Clear();
				_orderedAt.Clear();
				_freeUntil.Clear();
				ClearGroups();
				SelectedFlag = null;
				return;
			}
			float unscaledTime = Time.unscaledTime;
			_downed.Clear();
			foreach (WarFlag flag2 in Flags)
			{
				KeepBand(flag2, you, unscaledTime, toast);
			}
			foreach (WarFlag post in Posts)
			{
				KeepBand(post, you, unscaledTime, toast);
			}
			foreach (WarFlag item in _downed)
			{
				if (item.Quick)
				{
					Posts.Remove(item);
					continue;
				}
				Flags.Remove(item);
				if (SelectedFlag == item)
				{
					SelectedFlag = null;
				}
				toast?.Invoke("Flag " + item.Number + " comes down: its band has other orders.");
			}
			_downed.Clear();
		}

		private void KeepBand(WarFlag f, Actor you, float now, Action<string> toast)
		{
			f.Holding = (f.Coming = (f.Fighting = (f.Eating = 0)));
			int num = 0;
			ChaseOn(f);
			Gather(f);
			Vector2 zero = Vector2.zero;
			Vector2 p = f.P;
			float num2 = Mathf.Min(Spread(f.Band.Count), Mathf.Max(2f, f.Radius));
			float num3 = Mathf.Max(num2, (f.Order == WarOrder.Wait) ? 0f : f.Radius) + 2.5f;
			for (int num4 = f.Band.Count - 1; num4 >= 0; num4--)
			{
				Actor actor = f.Band[num4];
				bool flag = false;
				bool flag2;
				bool flag3;
				try
				{
					flag2 = actor?.isAlive() ?? false;
					flag3 = flag2 && actor.kingdom == _flagsOf;
				}
				catch
				{
					flag2 = (flag3 = false);
				}
				if (flag3)
				{
					if (!f.Ids.TryGetValue(actor, out var value))
					{
						f.Ids[actor] = actor.data.id;
					}
					else if (value != actor.data.id)
					{
						flag2 = (flag3 = false);
					}
				}
				if (!flag3)
				{
					if (actor != null)
					{
						f.Ids.Remove(actor);
					}
					f.Band.RemoveAt(num4);
					if (actor != null)
					{
						Forget(actor);
					}
					if (!flag2)
					{
						f.Fallen++;
					}
				}
				else
				{
					Vector2 current_position = actor.current_position;
					float magnitude = (current_position - p).magnitude;
					bool flag4 = false;
					try
					{
						flag = actor.isFighting();
						flag4 = actor.isKing();
					}
					catch
					{
					}
					if (flag4 || (actor != you && !flag && Royal.IsMarchingTo(actor, out var to) && to != null && (new Vector2((float)to.x + 0.5f, (float)to.y + 0.5f) - p).magnitude > num3 + 2f))
					{
						f.Band.RemoveAt(num4);
						Forget(actor);
						num++;
					}
					else
					{
						if (f.Order == WarOrder.Pillage && actor != you)
						{
							Torch(f, actor, now);
						}
						if (flag && (actor == you || !Strays(f, actor)))
						{
							f.Fighting++;
						}
						else if (actor == you)
						{
							if (magnitude <= num3)
							{
								f.Holding++;
							}
							else
							{
								f.Coming++;
								zero += current_position;
							}
						}
						else
						{
							int num5 = Engage(f, actor, now);
							if (num5 != 0)
							{
								if (num5 > 0)
								{
									f.Fighting++;
								}
								else
								{
									f.Coming++;
									zero += current_position;
								}
							}
							else
							{
								bool flag5 = Royal.IsHolding(actor) && actor.beh_tile_target != null && (new Vector2((float)actor.beh_tile_target.x + 0.5f, (float)actor.beh_tile_target.y + 0.5f) - p).magnitude <= num2 + 1.5f;
								if (!flag5 && FreeToEat(actor, now))
								{
									f.Eating++;
								}
								else
								{
									if (magnitude <= num3)
									{
										f.Holding++;
									}
									else
									{
										f.Coming++;
										zero += current_position;
									}
									if (!flag5 && (!_orderedAt.TryGetValue(actor, out var value2) || !(now - value2 < 1.5f)) && Royal.Hold(actor, f.Tile, num2))
									{
										_orderedAt[actor] = now;
									}
								}
							}
						}
					}
				}
			}
			f.ComingFrom = ((f.Coming > 0) ? (zero / f.Coming) : p);
			if (f.Band.Count == 0 && (num > 0 || (f.Quick && f.HadBand)))
			{
				_downed.Add(f);
			}
			else if (!f.Quick && f.HadBand && f.Band.Count == 0 && f.Fallen > 0 && !f.ToldFallen)
			{
				f.ToldFallen = true;
				toast?.Invoke("Flag " + f.Number + "'s band has fallen: all " + f.Fallen + " who held it are dead.");
			}
		}

		public WarMap()
		{
			_current = this;
		}

		public static bool IsShip(Actor a)
		{
			return BoatHelm.IsBoat(a);
		}

		public static bool Armed(Actor ship)
		{
			try
			{
				return ship?.hasRangeAttack() ?? false;
			}
			catch
			{
				return false;
			}
		}

		public ShipOrder OrderOf(Actor ship)
		{
			if (ship == null || !_orderOf.TryGetValue(ship, out var value))
			{
				return null;
			}
			return value;
		}

		internal static bool Ordered(Actor a, out bool firing)
		{
			firing = false;
			WarMap current = _current;
			if (current == null || current._orderOf.Count == 0 || a == null || !current._orderOf.TryGetValue(a, out var value))
			{
				return false;
			}
			firing = value.What == ShipOrder.Kind.Bombard;
			return true;
		}

		public static string ShipDoing(Actor ship)
		{
			ShipOrder shipOrder = _current?.OrderOf(ship);
			if (shipOrder == null)
			{
				return null;
			}
			string text = LiftDoing(_current.LiftOf(ship), shipOrder);
			if (text != null)
			{
				return text;
			}
			if (shipOrder.What == ShipOrder.Kind.Sail)
			{
				return "sailing where the king sent her";
			}
			if (shipOrder.Holding != null)
			{
				return "closing on " + shipOrder.TargetName + " (" + shipOrder.Holding + ")";
			}
			return "firing on " + shipOrder.TargetName + ((shipOrder.Shots > 0) ? (" (" + shipOrder.Shots + " shots)") : "");
		}

		public List<Actor> MarkedShips()
		{
			List<Actor> list = new List<Actor>();
			foreach (Actor item in Marked)
			{
				try
				{
					if (item != null && item.isAlive() && item.kingdom == Home && IsShip(item))
					{
						list.Add(item);
					}
				}
				catch
				{
				}
			}
			return list;
		}

		public int MarkShips(bool add)
		{
			if (!add)
			{
				Marked.Clear();
			}
			int num = 0;
			foreach (Unit unit in Units)
			{
				if (unit.Ours && unit.Ship && Marked.Add(unit.Actor))
				{
					num++;
				}
			}
			return num;
		}

		public int SailTo(Actor host, List<Actor> ships, WorldTile water, out int cantReach)
		{
			cantReach = 0;
			if (water == null || host == null)
			{
				return 0;
			}
			BeginFleet(host.kingdom);
			_berthsTaken.Clear();
			int num = 0;
			for (int i = 0; i < ships.Count; i++)
			{
				Actor actor = ships[i];
				if (!Commandable(actor, host))
				{
					continue;
				}
				float num2 = ((i == 0) ? 0f : (2.6f * Mathf.Sqrt(i)));
				float f = (float)i * 2.39996f;
				WorldTile worldTile = WaterNear(new Vector2((float)water.x + 0.5f + Mathf.Cos(f) * num2, (float)water.y + 0.5f + Mathf.Sin(f) * num2), actor, 3);
				if (worldTile == null)
				{
					cantReach++;
					continue;
				}
				_berthsTaken.Add(worldTile);
				ShipOrder shipOrder = Order(actor);
				shipOrder.What = ShipOrder.Kind.Sail;
				shipOrder.To = worldTile;
				shipOrder.Target = null;
				shipOrder.Town = null;
				shipOrder.TargetName = null;
				shipOrder.Holding = null;
				if (Royal.Sail(actor, worldTile))
				{
					shipOrder.OrderedAt = Time.unscaledTime;
				}
				num++;
			}
			_fleetUpkeep = 0f;
			return num;
		}

		public int Bombard(Actor host, List<Actor> ships, Vector2 aim, Actor target, City town, string name, out int unarmed)
		{
			unarmed = 0;
			if (host == null)
			{
				return 0;
			}
			BeginFleet(host.kingdom);
			int num = 0;
			foreach (Actor ship in ships)
			{
				if (!Commandable(ship, host))
				{
					continue;
				}
				if (!Armed(ship))
				{
					unarmed++;
					continue;
				}
				ShipOrder shipOrder = Order(ship);
				bool num2 = shipOrder.What == ShipOrder.Kind.Bombard && shipOrder.Target == target && shipOrder.Town == town && (shipOrder.Aim - aim).sqrMagnitude < 1f;
				shipOrder.What = ShipOrder.Kind.Bombard;
				shipOrder.Aim = aim;
				shipOrder.LastAim = aim;
				shipOrder.Target = target;
				shipOrder.TargetId = ActorId(target);
				shipOrder.Town = town;
				shipOrder.TargetName = name;
				if (!num2)
				{
					shipOrder.Shots = 0;
					shipOrder.To = null;
					shipOrder.BerthAt = -10f;
				}
				shipOrder.Holding = null;
				num++;
			}
			_fleetUpkeep = 0f;
			return num;
		}

		public int CeaseFire(List<Actor> ships, out int kept)
		{
			int num = 0;
			kept = 0;
			foreach (Actor ship in ships)
			{
				ShipOrder shipOrder = OrderOf(ship);
				if (shipOrder != null)
				{
					num++;
					if (KeepAboard(ship))
					{
						kept++;
						continue;
					}
					Drop(shipOrder);
					Royal.ReleaseShip(ship);
				}
			}
			return num;
		}

		public int CeaseFire(List<Actor> ships)
		{
			int kept;
			return CeaseFire(ships, out kept);
		}

		public int CeaseFireOn(ShipOrder like)
		{
			List<Actor> list = new List<Actor>();
			foreach (ShipOrder shipOrder in ShipOrders)
			{
				if (shipOrder.SameTarget(like))
				{
					list.Add(shipOrder.Ship);
				}
			}
			return CeaseFire(list);
		}

		public void ClearFleet()
		{
			ClearLifts();
			foreach (ShipOrder shipOrder in ShipOrders)
			{
				Royal.ReleaseShip(shipOrder.Ship);
			}
			ShipOrders.Clear();
			_orderOf.Clear();
		}

		private void BeginFleet(Kingdom k)
		{
			if (k != _fleetOf)
			{
				ClearFleet();
				_fleetOf = k;
			}
			_fleetKing = KingId(k);
		}

		private bool Commandable(Actor s, Actor host)
		{
			try
			{
				return s != null && s.isAlive() && IsShip(s) && s.kingdom == host.kingdom && s.ai != null && s != host;
			}
			catch
			{
				return false;
			}
		}

		private static long ActorId(Actor a)
		{
			try
			{
				return a?.data.id ?? (-1);
			}
			catch
			{
				return -1L;
			}
		}

		internal static long KingId(Kingdom k)
		{
			try
			{
				return (k != null && k.king != null && k.king.isAlive()) ? k.king.data.id : (-1);
			}
			catch
			{
				return -1L;
			}
		}

		private bool OnYourSide(Kingdom k)
		{
			try
			{
				return k != null && _fleetOf != null && (k == _fleetOf || Royal.Allied(_fleetOf, k));
			}
			catch
			{
				return false;
			}
		}

		private ShipOrder Order(Actor s)
		{
			ShipOrder shipOrder = OrderOf(s);
			if (shipOrder != null)
			{
				return shipOrder;
			}
			shipOrder = new ShipOrder
			{
				Ship = s,
				ShipId = ActorId(s)
			};
			ShipOrders.Add(shipOrder);
			_orderOf[s] = shipOrder;
			return shipOrder;
		}

		private void Drop(ShipOrder o)
		{
			ShipOrders.Remove(o);
			if (o.Ship != null)
			{
				_orderOf.Remove(o.Ship);
			}
		}

		private static bool Water(WorldTile t)
		{
			try
			{
				return t != null && t.Type != null && t.is_liquid && !t.Type.lava;
			}
			catch
			{
				return false;
			}
		}

		private static WorldTile WaterNear(Vector2 p, Actor ship, int radius)
		{
			WorldTile worldTile = null;
			try
			{
				worldTile = ship.current_tile;
			}
			catch
			{
			}
			WorldTile result = null;
			float num = float.MaxValue;
			int num2 = Mathf.FloorToInt(p.x);
			int num3 = Mathf.FloorToInt(p.y);
			for (int i = -radius; i <= radius; i++)
			{
				for (int j = -radius; j <= radius; j++)
				{
					float num4 = j * j + i * i;
					if (!(num4 >= num) && !(num4 > (float)(radius * radius) + 0.5f))
					{
						WorldTile worldTile2 = null;
						try
						{
							worldTile2 = World.world.GetTile(num2 + j, num3 + i);
						}
						catch
						{
						}
						if (Water(worldTile2) && SameSea(worldTile, worldTile2))
						{
							result = worldTile2;
							num = num4;
						}
					}
				}
			}
			return result;
		}

		private static bool SameSea(WorldTile from, WorldTile t)
		{
			if (from == null || from == t)
			{
				return true;
			}
			try
			{
				return t.isSameIsland(from) || !Water(from);
			}
			catch
			{
				return true;
			}
		}

		private WorldTile Berth(Actor ship, Vector2 aim, float reach)
		{
			WorldTile worldTile = null;
			try
			{
				worldTile = ship.current_tile;
			}
			catch
			{
			}
			Vector2 current_position = ship.current_position;
			WorldTile result = null;
			float num = float.MaxValue;
			for (float num2 = Mathf.Max(2f, reach); num2 >= 2f; num2 -= 1.5f)
			{
				for (int i = 0; i < 28; i++)
				{
					float f = (float)i * MathF.PI * 2f / 28f;
					WorldTile worldTile2 = null;
					try
					{
						worldTile2 = World.world.GetTile(Mathf.FloorToInt(aim.x + Mathf.Cos(f) * num2), Mathf.FloorToInt(aim.y + Mathf.Sin(f) * num2));
					}
					catch
					{
					}
					if (!Water(worldTile2) || !SameSea(worldTile, worldTile2))
					{
						continue;
					}
					bool flag = false;
					foreach (WorldTile item in _berthsTaken)
					{
						if (Mathf.Abs(item.x - worldTile2.x) + Mathf.Abs(item.y - worldTile2.y) < 3)
						{
							flag = true;
							break;
						}
					}
					float num3 = (new Vector2((float)worldTile2.x + 0.5f, (float)worldTile2.y + 0.5f) - current_position).sqrMagnitude + (flag ? 400f : 0f);
					if (num3 < num)
					{
						num = num3;
						result = worldTile2;
					}
				}
			}
			return result;
		}

		private bool OwnNear(Vector2 p, float radius)
		{
			try
			{
				if (_fleetOf == null)
				{
					return false;
				}
				foreach (Actor unit in _fleetOf.getUnits())
				{
					if (unit != null && unit.kingdom == _fleetOf && !IsShip(unit) && unit.isAlive() && (unit.current_position - p).sqrMagnitude < radius * radius)
					{
						return true;
					}
				}
			}
			catch
			{
			}
			return false;
		}

		private bool TownAim(City town, out Vector2 aim)
		{
			aim = Royal.Position(town);
			try
			{
				if (town.buildings != null && town.buildings.Count > 0)
				{
					for (int i = 0; i < 6; i++)
					{
						Building building = town.buildings[UnityEngine.Random.Range(0, town.buildings.Count)];
						if (building != null && building.isAlive() && building.current_tile != null)
						{
							Vector2 vector = new Vector2((float)building.current_tile.x + 0.5f, (float)building.current_tile.y + 0.5f);
							if (!OwnNear(vector, 4.5f))
							{
								aim = vector;
								return true;
							}
						}
					}
				}
			}
			catch
			{
			}
			return !OwnNear(aim, 4.5f);
		}

		public void UpkeepFleet(Actor you, float dt, Action<string> toast)
		{
			if (ShipOrders.Count == 0 && Lifts.Count == 0)
			{
				return;
			}
			_fleetUpkeep -= dt;
			if (_fleetUpkeep > 0f)
			{
				return;
			}
			_fleetUpkeep = 0.25f;
			bool flag;
			try
			{
				flag = _fleetOf == null || !_fleetOf.isAlive() || !World.world.kingdoms.list.Contains(_fleetOf) || (_fleetKing >= 0 && KingId(_fleetOf) != _fleetKing);
			}
			catch
			{
				flag = true;
			}
			if (flag)
			{
				ClearLifts();
				ShipOrders.Clear();
				_orderOf.Clear();
				return;
			}
			bool flag2 = false;
			try
			{
				flag2 = Config.paused || World.world.isPaused();
			}
			catch
			{
			}
			float unscaledTime = Time.unscaledTime;
			UpkeepLifts(you, unscaledTime, toast);
			_berthsTaken.Clear();
			_fleetNews.Clear();
			foreach (ShipOrder shipOrder2 in ShipOrders)
			{
				if (shipOrder2.To != null)
				{
					_berthsTaken.Add(shipOrder2.To);
				}
			}
			for (int num = ShipOrders.Count - 1; num >= 0; num--)
			{
				ShipOrder shipOrder = ShipOrders[num];
				Actor ship = shipOrder.Ship;
				bool flag3;
				try
				{
					flag3 = ship != null && ship.isAlive() && ship.kingdom == _fleetOf && (shipOrder.ShipId < 0 || ship.data.id == shipOrder.ShipId);
				}
				catch
				{
					flag3 = false;
				}
				if (!flag3)
				{
					Drop(shipOrder);
				}
				else if (ship != you)
				{
					try
					{
						Vector2 vector;
						if (shipOrder.What == ShipOrder.Kind.Sail)
						{
							Keep(shipOrder, shipOrder.To, unscaledTime);
						}
						else if (shipOrder.Target != null)
						{
							if (!shipOrder.Target.isAlive() || (shipOrder.TargetId >= 0 && shipOrder.Target.data.id != shipOrder.TargetId))
							{
								Done(shipOrder, shipOrder.TargetName + (IsShip(shipOrder.Target) ? " is sunk." : " is dead.") + " Your ships hold their fire.");
							}
							else
							{
								if (!OnYourSide(shipOrder.Target.kingdom))
								{
									vector = shipOrder.Target.current_position;
									goto IL_038f;
								}
								Done(shipOrder, shipOrder.TargetName + " is no foe now. Your ships hold their fire.");
							}
						}
						else
						{
							if (shipOrder.Town == null)
							{
								vector = shipOrder.Aim;
								goto IL_038f;
							}
							if (!shipOrder.Town.isAlive() || shipOrder.Town.buildings == null || shipOrder.Town.buildings.Count <= 0)
							{
								Done(shipOrder, shipOrder.TargetName + " lies in ruins. Your ships hold their fire.");
							}
							else
							{
								if (!OnYourSide(shipOrder.Town.kingdom))
								{
									vector = shipOrder.LastAim;
									if ((vector - Royal.Position(shipOrder.Town)).sqrMagnitude > 900f)
									{
										vector = Royal.Position(shipOrder.Town);
									}
									goto IL_038f;
								}
								Done(shipOrder, shipOrder.TargetName + " is yours now. Your ships hold their fire.");
							}
						}
						goto end_IL_01df;
						IL_0488:
						shipOrder.Holding = "out of reach";
						Keep(shipOrder, shipOrder.To, unscaledTime);
						goto end_IL_01df;
						IL_038f:
						float num2 = Mathf.Max(3f, ship.getAttackRange() - 1f);
						Vector2 aim;
						if ((ship.current_position - vector).magnitude > num2)
						{
							if (shipOrder.To != null && !((new Vector2((float)shipOrder.To.x + 0.5f, (float)shipOrder.To.y + 0.5f) - vector).magnitude > num2) && (shipOrder.Target == null || !(unscaledTime - shipOrder.BerthAt > 4f)))
							{
								goto IL_0488;
							}
							WorldTile worldTile = Berth(ship, vector, num2 - 1f);
							if (worldTile != null)
							{
								shipOrder.To = worldTile;
								shipOrder.BerthAt = unscaledTime;
								_berthsTaken.Add(worldTile);
								goto IL_0488;
							}
							Done(shipOrder, "Your ships can't get within reach of " + shipOrder.TargetName + ": no water near enough.");
						}
						else
						{
							if (shipOrder.To == null || (new Vector2((float)shipOrder.To.x + 0.5f, (float)shipOrder.To.y + 0.5f) - ship.current_position).magnitude > 2.5f)
							{
								shipOrder.To = ship.current_tile;
								shipOrder.BerthAt = unscaledTime;
							}
							Keep(shipOrder, shipOrder.To, unscaledTime);
							if (!flag2 && ship.isAttackReady())
							{
								aim = vector;
								if (shipOrder.Town != null)
								{
									if (TownAim(shipOrder.Town, out aim))
									{
										goto IL_057f;
									}
									shipOrder.Holding = "your own are there";
								}
								else
								{
									if (!OwnNear(vector, 4.5f))
									{
										goto IL_057f;
									}
									shipOrder.Holding = "your own are there";
								}
							}
						}
						goto end_IL_01df;
						IL_057f:
						shipOrder.Holding = null;
						shipOrder.LastAim = aim;
						WorldTile tile = World.world.GetTile(Mathf.FloorToInt(aim.x), Mathf.FloorToInt(aim.y));
						if (ship.tryToAttack((BaseSimObject)shipOrder.Target, pDoChecks: false, (Action)null, new Vector3(aim.x, aim.y, 0f), (Kingdom)null, tile, 0f))
						{
							bool flag4 = true;
							foreach (ShipOrder shipOrder3 in ShipOrders)
							{
								if (shipOrder3 != shipOrder && shipOrder3.SameTarget(shipOrder) && shipOrder3.Shots > 0)
								{
									flag4 = false;
									break;
								}
							}
							if (flag4 && shipOrder.Shots == 0)
							{
								_fleetNews.Add("Your ships open fire on " + shipOrder.TargetName + "!");
							}
							shipOrder.Shots++;
						}
						end_IL_01df:;
					}
					catch (Exception ex)
					{
						Log.Error("fleet: " + ex.Message);
						Drop(shipOrder);
					}
				}
			}
			foreach (string item in _fleetNews)
			{
				toast?.Invoke(item);
			}
		}

		private void Keep(ShipOrder o, WorldTile to, float now)
		{
			if (to != null)
			{
				Actor ship = o.Ship;
				if ((!Royal.IsSailing(ship) || ship.beh_tile_target != to) && !(now - o.OrderedAt < 1.2f) && Royal.Sail(ship, to))
				{
					o.OrderedAt = now;
				}
			}
		}

		private void Done(ShipOrder o, string news)
		{
			_fleetNews.Add(news);
			if (!KeepAboard(o.Ship))
			{
				Drop(o);
				Royal.ReleaseShip(o.Ship);
			}
		}

		public void ChooseOrder(WarOrder order)
		{
			NextOrder = order;
			if (SelectedFlag != null && Flags.Contains(SelectedFlag) && SelectedFlag.Order != order)
			{
				SelectedFlag.Order = order;
				SelectedFlag.Radius = Mathf.Max(SelectedFlag.Radius, 3f);
				SelectedFlag.Born = Time.unscaledTime;
				_upkeep = 0f;
			}
		}

		private void OrdersFor(Kingdom k)
		{
			if (k != _flagsOf)
			{
				ClearFlags();
				_flagsOf = k;
			}
			_flagsKing = KingId(k);
		}

		public WarFlag Post(Royal royal, Actor host, List<Actor> warriors, WorldTile t, WarOrder order, float radius, BaseSimObject target, out int cantReach)
		{
			cantReach = 0;
			if (t == null || host == null)
			{
				return null;
			}
			OrdersFor(host.kingdom);
			WarFlag warFlag = new WarFlag
			{
				Quick = true,
				Tile = t,
				Order = order,
				Radius = radius,
				Born = Time.unscaledTime
			};
			if (target != null)
			{
				warFlag.Target = target;
				try
				{
					warFlag.TargetId = target.getID();
					warFlag.TargetLast = target.current_position;
				}
				catch
				{
				}
			}
			Posts.Add(warFlag);
			if (Assign(royal, host, warriors, warFlag, out cantReach) == 0)
			{
				Posts.Remove(warFlag);
				return null;
			}
			return warFlag;
		}

		private void ClearGroups()
		{
			_groupOf.Clear();
			_groupIds.Clear();
		}

		private void JoinGroup(Actor a, int n)
		{
			if (a == null || n < 1 || n > 6)
			{
				return;
			}
			_groupOf[a] = n;
			try
			{
				_groupIds[a] = a.data.id;
			}
			catch
			{
			}
		}

		public int GroupOf(Actor a)
		{
			if (a == null || !_groupOf.TryGetValue(a, out var value))
			{
				return 0;
			}
			try
			{
				if (!a.isAlive() || a.kingdom != _flagsOf || !_groupIds.TryGetValue(a, out var value2) || value2 != a.data.id)
				{
					_groupOf.Remove(a);
					_groupIds.Remove(a);
					return 0;
				}
				return value;
			}
			catch
			{
				return 0;
			}
		}

		public List<Actor> GroupMembers(int n)
		{
			List<Actor> list = new List<Actor>();
			foreach (KeyValuePair<Actor, int> item in _groupOf)
			{
				if (item.Value == n)
				{
					list.Add(item.Key);
				}
			}
			for (int num = list.Count - 1; num >= 0; num--)
			{
				if (GroupOf(list[num]) != n)
				{
					list.RemoveAt(num);
				}
			}
			return list;
		}

		public int SaveGroup(int n, List<Actor> own, Kingdom k)
		{
			if (n < 1 || n > 6)
			{
				return 0;
			}
			OrdersFor(k);
			List<Actor> list = new List<Actor>();
			foreach (KeyValuePair<Actor, int> item in _groupOf)
			{
				if (item.Value == n)
				{
					list.Add(item.Key);
				}
			}
			foreach (Actor item2 in list)
			{
				_groupOf.Remove(item2);
				_groupIds.Remove(item2);
			}
			int num = 0;
			foreach (Actor item3 in own)
			{
				try
				{
					if (item3 == null || !item3.isAlive() || item3.kingdom != k)
					{
						continue;
					}
					goto IL_00d8;
				}
				catch
				{
				}
				continue;
				IL_00d8:
				JoinGroup(item3, n);
				num++;
			}
			return num;
		}

		public int SelectGroup(int n, bool add, out Vector2 middle)
		{
			List<Actor> list = GroupMembers(n);
			WarFlag warFlag = FlagNumbered(n);
			if (list.Count == 0 && warFlag != null)
			{
				list.AddRange(warFlag.Band);
			}
			if (!add)
			{
				Marked.Clear();
			}
			Vector2 zero = Vector2.zero;
			int num = 0;
			foreach (Actor item in list)
			{
				Marked.Add(item);
				try
				{
					zero += item.current_position;
					num++;
				}
				catch
				{
				}
			}
			SelectedFlag = warFlag;
			middle = ((num > 0) ? (zero / num) : (warFlag?.P ?? default(Vector2)));
			return list.Count;
		}

		public int GroupOfAll(List<Actor> own)
		{
			if (own.Count == 0)
			{
				return 0;
			}
			int num = GroupOf(own[0]);
			if (num == 0)
			{
				return 0;
			}
			foreach (Actor item in own)
			{
				if (GroupOf(item) != num)
				{
					return 0;
				}
			}
			if (GroupMembers(num).Count != own.Count)
			{
				return 0;
			}
			return num;
		}

		public int FreeNumber()
		{
			for (int i = 1; i <= 6; i++)
			{
				if (FlagNumbered(i) == null && GroupMembers(i).Count == 0)
				{
					return i;
				}
			}
			for (int j = 1; j <= 6; j++)
			{
				if (FlagNumbered(j) == null)
				{
					return j;
				}
			}
			return 0;
		}

		public void GroupCounts(int[] counts)
		{
			for (int i = 0; i < counts.Length; i++)
			{
				counts[i] = 0;
			}
			foreach (Actor item in new List<Actor>(_groupOf.Keys))
			{
				int num = GroupOf(item);
				if (num > 0 && num < counts.Length)
				{
					counts[num]++;
				}
			}
			foreach (WarFlag flag in Flags)
			{
				if (flag.Number < counts.Length && counts[flag.Number] == 0)
				{
					counts[flag.Number] = flag.Band.Count;
				}
			}
		}

		private void ChaseOn(WarFlag f)
		{
			if (f.Target == null)
			{
				return;
			}
			bool flag;
			try
			{
				flag = f.Target.isAlive() && f.Target.getID() == f.TargetId && (!f.Target.isBuilding() || !f.Target.b.isRuin());
			}
			catch
			{
				flag = false;
			}
			if (flag)
			{
				try
				{
					f.TargetLast = f.Target.current_position;
					WorldTile current_tile = f.Target.current_tile;
					if (current_tile != null)
					{
						f.Tile = current_tile;
					}
					return;
				}
				catch
				{
					return;
				}
			}
			f.Target = null;
			WorldTile worldTile = null;
			try
			{
				worldTile = GroundFor(World.world.GetTile((int)f.TargetLast.x, (int)f.TargetLast.y));
			}
			catch
			{
			}
			if (worldTile != null)
			{
				f.Tile = worldTile;
			}
			f.Order = WarOrder.Attack;
			f.Radius = 5f;
		}

		private static bool IsFoe(Kingdom home, Actor u)
		{
			try
			{
				return u != null && u.isAlive() && u.kingdom != null && u.kingdom != home && !u.is_inside_boat && !u.isInsideSomething() && home.isEnemy(u.kingdom);
			}
			catch
			{
				return false;
			}
		}

		private static bool IsFighter(Actor u)
		{
			try
			{
				return u.isWarrior() || u.isKing() || (u.kingdom != null && !u.kingdom.isCiv());
			}
			catch
			{
				return false;
			}
		}

		private void Gather(WarFlag f)
		{
			_fighters.Clear();
			_others.Clear();
			_houses.Clear();
			if (f.Target != null || f.Order == WarOrder.Wait || f.Band.Count == 0 || _flagsOf == null)
			{
				return;
			}
			Kingdom flagsOf = _flagsOf;
			bool units = f.Order != WarOrder.Destroy;
			bool houses = f.Order == WarOrder.Destroy || f.Order == WarOrder.Pillage;
			bool villagers = f.Order != WarOrder.Kill;
			float radius = f.Radius;
			float r = radius * radius;
			Vector2 p = f.P;
			_chunks.Clear();
			for (float num = p.y - radius; num <= p.y + radius + 7.9f; num += 8f)
			{
				for (float num2 = p.x - radius; num2 <= p.x + radius + 7.9f; num2 += 8f)
				{
					WorldTile worldTile = null;
					try
					{
						worldTile = World.world.GetTile(Mathf.Clamp((int)Mathf.Min(num2, p.x + radius), 0, (int)WorldWidth - 1), Mathf.Clamp((int)Mathf.Min(num, p.y + radius), 0, (int)WorldHeight - 1));
					}
					catch
					{
					}
					if (worldTile != null && worldTile.chunk != null)
					{
						_chunks.Add(worldTile.chunk);
					}
				}
			}
			foreach (MapChunk chunk in _chunks)
			{
				try
				{
					GatherChunk(f, chunk, flagsOf, p, r, units, houses, villagers);
				}
				catch (Exception ex)
				{
					Log.Error("war orders: reading a chunk: " + ex.Message);
				}
			}
		}

		private void GatherChunk(WarFlag f, MapChunk chunk, Kingdom home, Vector2 c, float r2, bool units, bool houses, bool villagers)
		{
			ChunkObjectContainer objects = chunk.objects;
			if (objects == null)
			{
				return;
			}
			if (units)
			{
				foreach (Actor item in objects.units_all)
				{
					if (item == null)
					{
						continue;
					}
					Vector2 current_position;
					try
					{
						current_position = item.current_position;
					}
					catch
					{
						continue;
					}
					if (!((current_position - c).sqrMagnitude > r2) && IsFoe(home, item))
					{
						if (IsFighter(item))
						{
							_fighters.Add(item);
						}
						else if (villagers)
						{
							_others.Add(item);
						}
					}
				}
			}
			if (!houses)
			{
				return;
			}
			foreach (Building item2 in objects.buildings_all)
			{
				try
				{
					if (item2 != null && item2.isAlive() && !item2.isRuin() && item2.kingdom != null && item2.kingdom != home && home.isEnemy(item2.kingdom) && !((item2.current_position - c).sqrMagnitude > r2) && (f.Order != WarOrder.Pillage || (item2.isBurnable() && !((BaseSimObject)item2).hasStatus("burning"))))
					{
						_houses.Add(item2);
					}
				}
				catch
				{
				}
			}
		}

		private BaseSimObject FoeFor(WarFlag f, Actor a)
		{
			if (f.Target != null)
			{
				return f.Target;
			}
			switch (f.Order)
			{
			case WarOrder.Attack:
				return Nearest(a, _fighters) ?? Nearest(a, _others);
			case WarOrder.Kill:
				return Nearest(a, _fighters);
			case WarOrder.Destroy:
				return Nearest(a, _houses);
			case WarOrder.Pillage:
			{
				BaseSimObject baseSimObject = Nearest(a, _fighters) ?? Nearest(a, _others);
				Building building = Nearest(a, _houses);
				if (baseSimObject == null || building == null)
				{
					return baseSimObject ?? building;
				}
				try
				{
					return ((building.current_position - a.current_position).magnitude * 0.75f < (baseSimObject.current_position - a.current_position).magnitude) ? building : baseSimObject;
				}
				catch
				{
					return baseSimObject;
				}
			}
			default:
				return null;
			}
		}

		private static T Nearest<T>(Actor a, List<T> list) where T : BaseSimObject
		{
			T result = null;
			float num = float.MaxValue;
			Vector2 current_position = a.current_position;
			foreach (T item in list)
			{
				float sqrMagnitude;
				try
				{
					sqrMagnitude = (item.current_position - current_position).sqrMagnitude;
				}
				catch
				{
					continue;
				}
				if (!(sqrMagnitude >= num) && Reaches(a, item.current_tile))
				{
					result = item;
					num = sqrMagnitude;
				}
			}
			return result;
		}

		private int Engage(WarFlag f, Actor a, float now)
		{
			BaseSimObject baseSimObject = FoeFor(f, a);
			if (baseSimObject == null)
			{
				return 0;
			}
			Vector2 current_position;
			WorldTile current_tile;
			try
			{
				current_position = baseSimObject.current_position;
				current_tile = baseSimObject.current_tile;
			}
			catch
			{
				return 0;
			}
			if (current_tile == null)
			{
				return 0;
			}
			float magnitude = (current_position - a.current_position).magnitude;
			bool flag = f.Order == WarOrder.Pillage && f.Target == null && baseSimObject.isBuilding();
			if (flag && magnitude <= 4f)
			{
				return 1;
			}
			if (!flag && magnitude <= 7f)
			{
				try
				{
					if (!a.has_attack_target || a.attack_target != baseSimObject)
					{
						a.startFightingWith(baseSimObject);
					}
				}
				catch
				{
					return 0;
				}
				_orderedAt[a] = now;
				return 1;
			}
			if (Royal.IsHolding(a) && a.beh_tile_target != null && (new Vector2((float)a.beh_tile_target.x + 0.5f, (float)a.beh_tile_target.y + 0.5f) - current_position).magnitude <= 3f + magnitude * 0.25f)
			{
				return -1;
			}
			if (_orderedAt.TryGetValue(a, out var value) && now - value < 1.5f)
			{
				return -1;
			}
			if (Royal.Hold(a, current_tile, flag ? 1f : 1.5f))
			{
				_orderedAt[a] = now;
			}
			return -1;
		}

		private bool Strays(WarFlag f, Actor a)
		{
			BaseSimObject baseSimObject;
			try
			{
				baseSimObject = (a.has_attack_target ? a.attack_target : null);
			}
			catch
			{
				return false;
			}
			if (baseSimObject == null || baseSimObject == f.Target)
			{
				return false;
			}
			bool flag;
			try
			{
				float num = f.Radius + ((f.Order == WarOrder.Wait) ? 3f : 10f);
				flag = (baseSimObject.current_position - f.P).sqrMagnitude > num * num;
				if (!flag && f.Order == WarOrder.Kill && baseSimObject.isActor() && !IsFighter(baseSimObject.a) && (!baseSimObject.a.has_attack_target || baseSimObject.a.attack_target != a))
				{
					flag = true;
				}
			}
			catch
			{
				return false;
			}
			if (flag)
			{
				try
				{
					a.clearAttackTarget();
				}
				catch
				{
				}
			}
			return flag;
		}

		internal static bool Spares(Actor a)
		{
			WarMap current = _current;
			if (current == null || a == null || current._bandOf.Count == 0 || !current._bandOf.TryGetValue(a, out var value) || value.Order != WarOrder.Kill || value.Target != null)
			{
				return false;
			}
			try
			{
				return a.has_attack_target && a.attack_target is Actor actor && !IsFighter(actor) && (!actor.has_attack_target || actor.attack_target != a);
			}
			catch
			{
				return false;
			}
		}

		private void Torch(WarFlag f, Actor a, float now)
		{
			if (_houses.Count == 0 || UnityEngine.Random.value > 0.45f)
			{
				return;
			}
			Vector2 current_position;
			try
			{
				current_position = a.current_position;
			}
			catch
			{
				return;
			}
			foreach (Building house in _houses)
			{
				try
				{
					if ((house.current_position - current_position).sqrMagnitude > 16f || ((BaseSimObject)house).hasStatus("burning") || !house.isBurnable())
					{
						continue;
					}
					((BaseSimObject)house).addStatusEffect("burning", 0f, pColorEffect: true);
					f.Burned++;
					break;
				}
				catch
				{
					break;
				}
			}
		}

		public static Color Readable(Color c)
		{
			float num = 0.3f * c.r + 0.59f * c.g + 0.11f * c.b;
			if (!(num >= 0.5f))
			{
				return Color.Lerp(c, Color.white, Mathf.Clamp01((0.5f - num) / 0.5f) * 0.7f);
			}
			return c;
		}

		public static Color SideColor(Side s)
		{
			switch (s)
			{
			default:
				return NeutralSide;
			case Side.Enemy:
				return EnemySide;
			case Side.Ally:
				return AllySide;
			case Side.Own:
				return OwnSide;
			}
		}

		public static string SideName(Side s)
		{
			switch (s)
			{
			default:
				return "At peace";
			case Side.Enemy:
				return "At war";
			case Side.Ally:
				return "Allied";
			case Side.Own:
				return "Your realm";
			}
		}

		public Realm RealmOf(Kingdom k)
		{
			if (k == null || !_realmOf.TryGetValue(k, out var value))
			{
				return null;
			}
			return value;
		}

		public Side SideOf(Kingdom k)
		{
			if (k == null)
			{
				return Side.Neutral;
			}
			if (k == Home)
			{
				return Side.Own;
			}
			Realm realm = RealmOf(k);
			if (realm != null)
			{
				return realm.Side;
			}
			if (Royal.AtWar(Home, k))
			{
				return Side.Enemy;
			}
			if (Royal.Allied(Home, k))
			{
				return Side.Ally;
			}
			return Side.Neutral;
		}

		private void CollectRealms(bool force)
		{
			_realmsWait -= 0.35f;
			if (!force && _realmsWait > 0f && Realms.Count > 0)
			{
				return;
			}
			_realmsWait = 1.5f;
			Realms.Clear();
			_realmOf.Clear();
			_realmIndex.Clear();
			AllWars.Clear();
			Pacts.Clear();
			Kingdom home = Home;
			List<Kingdom> list = new List<Kingdom>();
			try
			{
				foreach (Kingdom item in World.world.kingdoms.list)
				{
					if (item != null && item.isAlive() && item.isCiv() && (item == home || item.hasCities()))
					{
						list.Add(item);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("war map: kingdoms: " + ex.Message);
			}
			foreach (Kingdom item2 in list)
			{
				Realm realm = new Realm
				{
					K = item2,
					Name = Royal.NameOf(item2),
					Color = Royal.ColorOf(item2),
					Text = Color.white
				};
				try
				{
					realm.Text = Readable(item2.getColor().getColorText());
				}
				catch
				{
				}
				realm.Side = ((item2 != home) ? (Royal.AtWar(home, item2) ? Side.Enemy : (Royal.Allied(home, item2) ? Side.Ally : Side.Neutral)) : Side.Own);
				try
				{
					realm.Alliance = item2.getAlliance();
				}
				catch
				{
				}
				realm.Towns = Royal.CityCount(item2);
				realm.People = Royal.Population(item2);
				realm.Warriors = Royal.Warriors(item2);
				try
				{
					City capital = item2.capital;
					WorldTile worldTile = ((capital != null && capital.isAlive()) ? capital.getTile() : null);
					if (worldTile == null)
					{
						foreach (City city in item2.getCities())
						{
							if (city != null && city.isAlive())
							{
								worldTile = city.getTile();
								if (worldTile != null)
								{
									break;
								}
							}
						}
					}
					if (worldTile != null)
					{
						realm.Capital = new Vector2(worldTile.posV3.x, worldTile.posV3.y);
						realm.HasCapital = true;
					}
				}
				catch
				{
				}
				if (item2 != home && home != null)
				{
					try
					{
						KingdomOpinion opinion = World.world.diplomacy.getOpinion(item2, home);
						if (opinion != null)
						{
							realm.Opinion = opinion.total;
							realm.HasOpinion = true;
						}
					}
					catch
					{
					}
				}
				_realmIndex[item2] = Realms.Count;
				_realmOf[item2] = realm;
				Realms.Add(realm);
			}
			Vector2 at = RealmOf(home)?.Capital ?? Vector2.zero;
			Realms.Sort(delegate(Realm a, Realm b)
			{
				int num2 = a.Side.CompareTo(b.Side);
				return (num2 != 0) ? num2 : (a.Capital - at).sqrMagnitude.CompareTo((b.Capital - at).sqrMagnitude);
			});
			for (int num = 0; num < Realms.Count; num++)
			{
				_realmIndex[Realms[num].K] = num;
			}
			try
			{
				foreach (War item3 in World.world.wars.list)
				{
					if (item3 == null || !item3.isAlive() || item3.hasEnded())
					{
						continue;
					}
					WarEntry warEntry = new WarEntry
					{
						War = item3,
						Name = Renames.Apply(item3.name)
					};
					try
					{
						foreach (Kingdom attacker in item3.getAttackers())
						{
							if (attacker != null && attacker.isAlive())
							{
								warEntry.Attackers.Add(attacker);
							}
						}
					}
					catch
					{
					}
					try
					{
						foreach (Kingdom defender in item3.getDefenders())
						{
							if (defender != null && defender.isAlive())
							{
								warEntry.Defenders.Add(defender);
							}
						}
					}
					catch
					{
					}
					if (warEntry.Attackers.Count == 0 && warEntry.Defenders.Count == 0)
					{
						continue;
					}
					try
					{
						warEntry.MainAttacker = item3.main_attacker;
						warEntry.MainDefender = item3.main_defender;
					}
					catch
					{
					}
					try
					{
						warEntry.DeadAttackers = item3.getDeadAttackers();
						warEntry.DeadDefenders = item3.getDeadDefenders();
					}
					catch
					{
					}
					try
					{
						warEntry.Years = item3.getDuration();
					}
					catch
					{
					}
					try
					{
						warEntry.Total = item3.isTotalWar();
					}
					catch
					{
					}
					warEntry.Ours = home != null && (warEntry.Attackers.Contains(home) || warEntry.Defenders.Contains(home));
					warEntry.WeAttack = warEntry.Ours && warEntry.Attackers.Contains(home);
					if (warEntry.Ours)
					{
						List<Kingdom> list2 = (warEntry.WeAttack ? warEntry.Defenders : warEntry.Attackers);
						Kingdom kingdom = (warEntry.WeAttack ? warEntry.MainDefender : warEntry.MainAttacker);
						warEntry.Foe = ((kingdom != null && list2.Contains(kingdom)) ? kingdom : ((list2.Count > 0) ? list2[0] : null));
					}
					if (string.IsNullOrEmpty(warEntry.Name))
					{
						warEntry.Name = Royal.NameOf(warEntry.MainAttacker ?? ((warEntry.Attackers.Count > 0) ? warEntry.Attackers[0] : null)) + " war";
					}
					AllWars.Add(warEntry);
					foreach (Kingdom attacker2 in warEntry.Attackers)
					{
						foreach (Kingdom defender2 in warEntry.Defenders)
						{
							Realm realm2 = RealmOf(attacker2);
							Realm realm3 = RealmOf(defender2);
							if (realm2 != null && !realm2.Foes.Contains(defender2))
							{
								realm2.Foes.Add(defender2);
							}
							if (realm3 != null && !realm3.Foes.Contains(attacker2))
							{
								realm3.Foes.Add(attacker2);
							}
						}
					}
				}
			}
			catch (Exception ex2)
			{
				Log.Error("war map: wars: " + ex2.Message);
			}
			AllWars.Sort(delegate(WarEntry a, WarEntry b)
			{
				if (a.Ours == b.Ours)
				{
					return (b.DeadAttackers + b.DeadDefenders).CompareTo(a.DeadAttackers + a.DeadDefenders);
				}
				return (!a.Ours) ? 1 : (-1);
			});
			try
			{
				foreach (Alliance item4 in World.world.alliances.list)
				{
					if (item4 == null || !item4.isAlive())
					{
						continue;
					}
					Pact pact = new Pact
					{
						A = item4,
						Name = Renames.Apply(item4.name),
						Color = Color.white
					};
					try
					{
						pact.Color = item4.getColor().getColorMain();
					}
					catch
					{
					}
					foreach (Kingdom item5 in item4.kingdoms_list)
					{
						if (item5 != null && item5.isAlive() && _realmOf.ContainsKey(item5))
						{
							pact.Members.Add(item5);
							pact.Warriors += _realmOf[item5].Warriors;
						}
					}
					if (pact.Members.Count == 0)
					{
						continue;
					}
					pact.Ours = home != null && pact.Members.Contains(home);
					foreach (Kingdom member in pact.Members)
					{
						foreach (Kingdom member2 in pact.Members)
						{
							if (member != member2 && _realmOf.TryGetValue(member, out var value) && !value.Friends.Contains(member2))
							{
								value.Friends.Add(member2);
							}
						}
					}
					Pacts.Add(pact);
				}
			}
			catch (Exception ex3)
			{
				Log.Error("war map: alliances: " + ex3.Message);
			}
			Pacts.Sort(delegate(Pact a, Pact b)
			{
				if (a.Ours == b.Ours)
				{
					return b.Members.Count.CompareTo(a.Members.Count);
				}
				return (!a.Ours) ? 1 : (-1);
			});
			if (Picked != null && !_realmOf.ContainsKey(Picked))
			{
				Picked = null;
			}
			CollectTerritory();
		}

		private void CollectTerritory()
		{
			ZoneCalculator zoneCalculator = null;
			try
			{
				zoneCalculator = World.world.zone_calculator;
			}
			catch
			{
			}
			if (zoneCalculator == null || zoneCalculator.zones == null)
			{
				return;
			}
			int zones_total_x = zoneCalculator.zones_total_x;
			int zones_total_y = zoneCalculator.zones_total_y;
			if (zones_total_x <= 0 || zones_total_y <= 0)
			{
				return;
			}
			ZonesX = zones_total_x;
			ZonesY = zones_total_y;
			ZoneSize = WorldWidth / (float)zones_total_x;
			if (_owner.Length != zones_total_x * zones_total_y)
			{
				_owner = new int[zones_total_x * zones_total_y];
			}
			for (int i = 0; i < _owner.Length; i++)
			{
				_owner[i] = -1;
			}
			foreach (TileZone zone in zoneCalculator.zones)
			{
				if (zone == null || zone.city == null || zone.x < 0 || zone.y < 0 || zone.x >= zones_total_x || zone.y >= zones_total_y)
				{
					continue;
				}
				Kingdom kingdom = null;
				try
				{
					if (zone.city.isAlive())
					{
						kingdom = zone.city.kingdom;
					}
				}
				catch
				{
				}
				if (kingdom != null && _realmIndex.TryGetValue(kingdom, out var value))
				{
					_owner[zone.y * zones_total_x + zone.x] = value;
				}
			}
			Borders.Clear();
			float zoneSize = ZoneSize;
			for (int j = 0; j < zones_total_x - 1; j++)
			{
				int num = -1;
				int num2 = -2;
				int num3 = -2;
				for (int k = 0; k <= zones_total_y; k++)
				{
					int num4 = ((k < zones_total_y) ? _owner[k * zones_total_x + j] : (-2));
					int num5 = ((k < zones_total_y) ? _owner[k * zones_total_x + j + 1] : (-2));
					bool flag = k < zones_total_y && num4 != num5;
					if (num >= 0 && (!flag || num4 != num2 || num5 != num3))
					{
						Borders.Add(new Border
						{
							A = new Vector2((float)(j + 1) * zoneSize, (float)num * zoneSize),
							B = new Vector2((float)(j + 1) * zoneSize, (float)k * zoneSize),
							Vertical = true,
							Low = num2,
							High = num3
						});
						num = -1;
					}
					if (flag && num < 0)
					{
						num = k;
						num2 = num4;
						num3 = num5;
					}
				}
			}
			for (int l = 0; l < zones_total_y - 1; l++)
			{
				int num6 = -1;
				int num7 = -2;
				int num8 = -2;
				for (int m = 0; m <= zones_total_x; m++)
				{
					int num9 = ((m < zones_total_x) ? _owner[l * zones_total_x + m] : (-2));
					int num10 = ((m < zones_total_x) ? _owner[(l + 1) * zones_total_x + m] : (-2));
					bool flag2 = m < zones_total_x && num9 != num10;
					if (num6 >= 0 && (!flag2 || num9 != num7 || num10 != num8))
					{
						Borders.Add(new Border
						{
							A = new Vector2((float)num6 * zoneSize, (float)(l + 1) * zoneSize),
							B = new Vector2((float)m * zoneSize, (float)(l + 1) * zoneSize),
							Vertical = false,
							Low = num7,
							High = num8
						});
						num6 = -1;
					}
					if (flag2 && num6 < 0)
					{
						num6 = m;
						num7 = num9;
						num8 = num10;
					}
				}
			}
			_territoryDirty = true;
		}

		public Color LandColor(int realm, Layer layer)
		{
			if (realm < 0 || realm >= Realms.Count)
			{
				return Color.clear;
			}
			Realm realm2 = Realms[realm];
			switch (layer)
			{
			case Layer.Kingdoms:
				return new Color(realm2.Color.r, realm2.Color.g, realm2.Color.b, 0.34f);
			case Layer.Alliances:
				if (realm2.Alliance != null)
				{
					foreach (Pact pact in Pacts)
					{
						if (pact.A == realm2.Alliance)
						{
							return new Color(pact.Color.r, pact.Color.g, pact.Color.b, 0.4f);
						}
					}
				}
				return new Color(realm2.Color.r, realm2.Color.g, realm2.Color.b, 0.1f);
			case Layer.Sides:
			{
				Color color = SideColor(realm2.Side);
				return new Color(color.r, color.g, color.b, (realm2.Side == Side.Neutral) ? 0.16f : 0.36f);
			}
			default:
				return Color.clear;
			}
		}

		public bool SameGroup(int a, int b, Layer layer)
		{
			if (a < 0 || b < 0 || a >= Realms.Count || b >= Realms.Count)
			{
				return false;
			}
			Realm realm = Realms[a];
			Realm realm2 = Realms[b];
			switch (layer)
			{
			case Layer.Alliances:
				if (realm.Alliance != null)
				{
					return realm.Alliance == realm2.Alliance;
				}
				return false;
			case Layer.Sides:
				if (realm.Side == realm2.Side)
				{
					return realm.Side != Side.Neutral;
				}
				return false;
			default:
				return false;
			}
		}

		public Texture2D TerritoryFor(Layer layer)
		{
			if (layer == Layer.None || ZonesX <= 0 || _owner.Length != ZonesX * ZonesY)
			{
				return null;
			}
			if (Territory != null && !_territoryDirty && _territoryLayer == layer)
			{
				return Territory;
			}
			if (Territory == null || Territory.width != ZonesX || Territory.height != ZonesY)
			{
				if (Territory != null)
				{
					UnityEngine.Object.Destroy(Territory);
				}
				Territory = new Texture2D(ZonesX, ZonesY, TextureFormat.RGBA32, mipChain: false)
				{
					filterMode = FilterMode.Point,
					wrapMode = TextureWrapMode.Clamp,
					name = "FirstPersonWarMapLand"
				};
			}
			Color[] array = new Color[Realms.Count];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = LandColor(i, layer);
			}
			Color32[] array2 = new Color32[ZonesX * ZonesY];
			for (int j = 0; j < array2.Length; j++)
			{
				int num = _owner[j];
				array2[j] = ((num >= 0 && num < array.Length) ? ((Color32)array[num]) : new Color32(0, 0, 0, 0));
			}
			Territory.SetPixels32(array2);
			Territory.Apply(updateMipmaps: false);
			_territoryDirty = false;
			_territoryLayer = layer;
			return Territory;
		}

		public int OwnerAt(Vector2 p)
		{
			if (ZonesX <= 0 || ZoneSize <= 0f)
			{
				return -1;
			}
			int num = Mathf.FloorToInt(p.x / ZoneSize);
			int num2 = Mathf.FloorToInt(p.y / ZoneSize);
			if (num < 0 || num2 < 0 || num >= ZonesX || num2 >= ZonesY || _owner.Length != ZonesX * ZonesY)
			{
				return -1;
			}
			return _owner[num2 * ZonesX + num];
		}

		public int IndexOf(Kingdom k)
		{
			if (k == null || !_realmIndex.TryGetValue(k, out var value))
			{
				return -1;
			}
			return value;
		}

		public void Pick(Kingdom k, bool goThere)
		{
			if (k != null && k == Picked && !goThere)
			{
				Picked = null;
				return;
			}
			Picked = k;
			PickedAt = Time.unscaledTime;
			Realm realm = RealmOf(k);
			if (goThere && realm != null && realm.HasCapital)
			{
				Centre = realm.Capital;
			}
		}

		internal void RefreshRealmsForTest()
		{
			CollectRealms(force: true);
		}

		public void FocusWar(Kingdom foe)
		{
			int num = Wars.IndexOf(foe);
			if (num >= 0)
			{
				WarIndex = num;
				_fit = true;
				_refresh = 0f;
			}
		}
	}
}
