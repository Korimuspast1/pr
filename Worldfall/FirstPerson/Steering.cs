using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using FirstPerson.Core;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal static class Steering
	{
		private struct Body
		{
			public Actor A;

			public Vector2 P;

			public float R;

			public bool Moving;

			public Vector2 V;
		}

		private struct Obstacle
		{
			public Collider Shape;

			public Building Owner;
		}

		private sealed class State
		{
			public float Radius = 0.15f;

			public float Blocked;

			public float GhostUntil;

			public Vector2 Waypoint;

			public Vector2 Anchor;

			public float Best = float.MaxValue;

			public float SinceCloser;

			public bool PushedSince;

			public float Side;

			public bool Flipped;

			public int Frame;

			public int HiddenFrame = -1;

			public int OutsideFrame = -100;

			public float ExitedAt = -100f;

			public Vector2 Doorway;

			public float Outward;

			public bool ExitPending;

			public Vector2 Vel;
		}

		private static List<Body> _crowd = new List<Body>(256);

		private static List<Body> _crowdNext = new List<Body>(256);

		private static readonly int[] CrowdHead = new int[7056];

		private static int[] _crowdLink = new int[512];

		private const float CrowdLook = 0.7f;

		private const float CrowdAhead = 0.8f;

		public const float Range = 36f;

		private const int Half = 42;

		private const int Size = 84;

		private const float Margin = 0.7f;

		private const float StuckSeconds = 2.2f;

		private const float GhostSeconds = 3f;

		private static readonly Dictionary<Building, Vector2> Doors = new Dictionary<Building, Vector2>();

		private static readonly Dictionary<MeshModel, Vector2?> ModelDoors = new Dictionary<MeshModel, Vector2?>();

		private static readonly Dictionary<Building, int> OwnerIndex = new Dictionary<Building, int>();

		internal static int DoorsIn;

		private static readonly Dictionary<Building, float> Crossings = new Dictionary<Building, float>();

		private static List<Obstacle> _pending = new List<Obstacle>(1024);

		private static List<Obstacle> _live = new List<Obstacle>(1024);

		private static readonly int[] Head = new int[7056];

		private static int[] _next = new int[8192];

		private static int[] _item = new int[8192];

		private static int _entries;

		private static int _x0;

		private static int _y0;

		private static bool _ready;

		private static Vector2 _you;

		private static float _youRadius;

		private static bool _haveYou;

		private static readonly Dictionary<Actor, State> States = new Dictionary<Actor, State>();

		private static readonly List<Actor> Stale = new List<Actor>();

		private static int _cleaned;

		public static int Ghosted;

		public static int DoorWays;

		public static int DoorExits;

		public static float CostMs;

		public static bool Verbose;

		private static string _pressedBy;

		public static int Walkers;

		private static long _ticks;

		private static int _walkers;

		private static Building _target;

		private static Building _foe;

		private static Vector2 _goal;

		private static Vector2 _goal2;

		private static bool _haveGoal;

		private static bool _haveGoal2;

		private const float InsideDepth = 0.15f;

		private const float ArriveFree = 1.6f;

		internal static bool Fast = true;

		internal static bool Verify;

		internal static long Verified;

		internal static long Mismatched;

		internal static long CrowdVerifiedCount;

		internal static long CrowdMismatchedCount;

		internal static string FirstMismatch;

		internal static string CrowdFirstMismatch;

		private static Collider[] _shapes = new Collider[1024];

		private static Building[] _owners = new Building[1024];

		private static int _shapeCount;

		internal static bool CrowdQuick = true;

		private static Actor[] _cA = new Actor[256];

		private static float[] _cPx = new float[256];

		private static float[] _cPy = new float[256];

		private static float[] _cR = new float[256];

		private static float[] _cVx = new float[256];

		private static float[] _cVy = new float[256];

		private static bool[] _cMoving = new bool[256];

		private static int _cCount;

		public static bool TakeExit(Actor a, out Vector2 doorway, out float outward)
		{
			doorway = default(Vector2);
			outward = 0f;
			if (a == null || !States.TryGetValue(a, out var value) || !value.ExitPending)
			{
				return false;
			}
			value.ExitPending = false;
			if (Time.unscaledTime - value.ExitedAt > 0.5f)
			{
				return false;
			}
			doorway = value.Doorway;
			outward = value.Outward;
			return true;
		}

		public static bool InsideAt(Vector2 p)
		{
			if (_ready)
			{
				return InsideOf(p, 0.15f) >= 0;
			}
			return false;
		}

		private static void BeginCrowd()
		{
			List<Body> crowd = _crowd;
			_crowd = _crowdNext;
			_crowdNext = crowd;
			_crowdNext.Clear();
			for (int i = 0; i < CrowdHead.Length; i++)
			{
				CrowdHead[i] = -1;
			}
			if (_crowdLink.Length < _crowd.Count)
			{
				_crowdLink = new int[_crowd.Count * 2];
			}
			for (int j = 0; j < _crowd.Count; j++)
			{
				int num = CellOf(_crowd[j].P);
				if (num < 0)
				{
					_crowdLink[j] = -2;
					continue;
				}
				_crowdLink[j] = CrowdHead[num];
				CrowdHead[num] = j;
			}
			KeepCrowd();
		}

		private static void RememberInCrowd(Actor a, Vector2 p, float radius, bool moving)
		{
			Vector2 v = Vector2.zero;
			if (moving && States.TryGetValue(a, out var value))
			{
				v = value.Vel;
			}
			if (_crowdNext.Count < 2048)
			{
				_crowdNext.Add(new Body
				{
					A = a,
					P = p,
					R = radius,
					Moving = moving,
					V = v
				});
			}
		}

		private static Vector2 Crowd(Actor a, Vector2 p, Vector2 waypoint, float radius, float speed, float dt, out bool moved)
		{
			if (!CrowdQuick)
			{
				return CrowdOld(a, p, waypoint, radius, speed, dt, out moved);
			}
			if (Verify)
			{
				return CrowdVerified(a, p, waypoint, radius, speed, dt, out moved);
			}
			return CrowdFast(a, p, waypoint, radius, speed, dt, out moved);
		}

		private static Vector2 CrowdOld(Actor a, Vector2 p, Vector2 waypoint, float radius, float speed, float dt, out bool moved)
		{
			moved = false;
			Vector2 vector = waypoint - p;
			Vector2 vector2 = ((vector.sqrMagnitude > 1E-06f) ? vector.normalized : Vector2.zero);
			Vector2 vector3 = new Vector2(vector2.y, 0f - vector2.x);
			int num = Mathf.FloorToInt(p.x) - _x0;
			int num2 = Mathf.FloorToInt(p.y) - _y0;
			Vector2 zero = Vector2.zero;
			float num3 = Mathf.Max(0.7f, speed * 0.8f);
			float num4 = Mathf.Max(1.2f, speed * 1.5f);
			Vector2 vector4 = vector2 * speed;
			if (States.TryGetValue(a, out var value))
			{
				value.Vel = vector4;
			}
			bool flag = num >= -1 && num <= 84 && num2 >= -1 && num2 <= 84;
			int num5 = num2 - 1;
			while (flag && num5 <= num2 + 1)
			{
				for (int i = num - 1; i <= num + 1; i++)
				{
					if (i < 0 || num5 < 0 || i >= 84 || num5 >= 84)
					{
						continue;
					}
					for (int num6 = CrowdHead[num5 * 84 + i]; num6 >= 0; num6 = _crowdLink[num6])
					{
						Body body = _crowd[num6];
						if (body.A != a)
						{
							Vector2 vector5 = body.P - p;
							float magnitude = vector5.magnitude;
							float num7 = radius + body.R + 0.05f;
							if (magnitude < 0.0001f)
							{
								vector5 = ((vector3.sqrMagnitude > 0f) ? vector3 : Vector2.right);
							}
							else
							{
								vector5 /= magnitude;
							}
							if (magnitude < num7)
							{
								zero -= vector5 * Mathf.Min(num7 - magnitude, num4 * dt) * (body.Moving ? 0.5f : 1f);
							}
							else if (vector2 != Vector2.zero && magnitude < num7 + num3 + 2f * speed * 0.8f)
							{
								Vector2 vector6 = body.P - p;
								Vector2 vector7 = body.V - vector4;
								float sqrMagnitude = vector7.sqrMagnitude;
								float num8 = ((sqrMagnitude > 0.0001f) ? Mathf.Clamp((0f - Vector2.Dot(vector6, vector7)) / sqrMagnitude, 0f, 0.8f) : 0f);
								Vector2 rhs = vector6 + vector7 * num8;
								float magnitude2 = rhs.magnitude;
								if (!(magnitude2 >= num7 + 0.08f) && (!(num8 <= 0f) || !(Vector2.Dot(vector2, vector5) <= 0.5f)))
								{
									float num9 = Vector2.Dot(vector3, rhs);
									float num10 = ((Mathf.Abs(num9) < 0.05f) ? 1f : ((num9 > 0f) ? (-1f) : 1f));
									float num11 = 1f - num8 / 0.8f;
									float num12 = 1f - magnitude2 / (num7 + 0.08f);
									zero += vector3 * num10 * Mathf.Min(0.08f, speed * dt * 1.2f) * Mathf.Clamp01(0.35f + num11) * Mathf.Clamp01(0.4f + num12);
								}
							}
						}
					}
				}
				num5++;
			}
			if (zero.sqrMagnitude < 1E-10f)
			{
				return p;
			}
			moved = true;
			bool moved2;
			return Clear(p + zero, radius, out moved2);
		}

		public static void SetDoor(Building b, Vector2 outside)
		{
			if (Doors.Count > 1500)
			{
				Doors.Clear();
			}
			Doors[b] = outside;
		}

		public static bool DoorOf(MeshModel m, out Vector2 at)
		{
			if (!ModelDoors.TryGetValue(m, out var value))
			{
				value = null;
				int num = m.SlotIndex("door");
				if (num >= 0)
				{
					float num2 = float.MaxValue;
					MeshPart[] parts = m.Parts;
					foreach (MeshPart meshPart in parts)
					{
						for (int j = 0; j < meshPart.TriangleCount; j++)
						{
							if (meshPart.Slot[j] == num)
							{
								for (int k = 0; k < 3; k++)
								{
									num2 = Mathf.Min(num2, meshPart.Pos[meshPart.Tri[j * 3 + k] * 3 + 2]);
								}
							}
						}
					}
					float num3 = 0f;
					float num4 = float.MaxValue;
					int num5 = 0;
					parts = m.Parts;
					foreach (MeshPart meshPart2 in parts)
					{
						for (int l = 0; l < meshPart2.TriangleCount; l++)
						{
							if (meshPart2.Slot[l] != num)
							{
								continue;
							}
							float num6 = float.MaxValue;
							for (int n = 0; n < 3; n++)
							{
								num6 = Mathf.Min(num6, meshPart2.Pos[meshPart2.Tri[l * 3 + n] * 3 + 2]);
							}
							if (!(num6 > num2 + 0.35f))
							{
								for (int num7 = 0; num7 < 3; num7++)
								{
									int num8 = meshPart2.Tri[l * 3 + num7] * 3;
									num3 += meshPart2.Pos[num8];
									num4 = Mathf.Min(num4, meshPart2.Pos[num8 + 1]);
									num5++;
								}
							}
						}
					}
					if (num5 > 0)
					{
						value = new Vector2(num3 / (float)num5, num4);
					}
				}
				ModelDoors[m] = value;
			}
			at = value ?? Vector2.zero;
			return value.HasValue;
		}

		public static bool Hidden(Actor a)
		{
			if (a != null && States.TryGetValue(a, out var value))
			{
				return value.HiddenFrame == Time.frameCount;
			}
			return false;
		}

		public static bool Solid(Vector2 p)
		{
			if (_ready)
			{
				return InsideOf(p, -0.05f) >= 0;
			}
			return false;
		}

		internal static bool HiddenLastFrame(Actor a)
		{
			if (a != null && States.TryGetValue(a, out var value))
			{
				return value.HiddenFrame >= Time.frameCount - 1;
			}
			return false;
		}

		internal static bool DoorForTest(Building b, out Vector2 door)
		{
			return Doors.TryGetValue(b, out door);
		}

		public static bool DoorOf(Building b, out Vector2 door)
		{
			if (b != null && Doors.TryGetValue(b, out door))
			{
				return true;
			}
			door = default(Vector2);
			return false;
		}

		public static void NoteCrossing(Building b)
		{
			if (b != null)
			{
				if (Crossings.Count > 400)
				{
					Crossings.Clear();
				}
				Crossings[b] = Time.unscaledTime;
			}
		}

		public static float SinceCrossing(Building b)
		{
			if (b == null || !Crossings.TryGetValue(b, out var value))
			{
				return 1000000f;
			}
			return Time.unscaledTime - value;
		}

		internal static string DoorReportForTest()
		{
			StringBuilder stringBuilder = new StringBuilder();
			int num = 0;
			int num2 = 0;
			foreach (KeyValuePair<Building, Vector2> door in Doors)
			{
				if (CellOf(door.Value) < 0)
				{
					continue;
				}
				num2++;
				int num3 = InsideOf(door.Value);
				if (num3 < 0)
				{
					num++;
				}
				else if (stringBuilder.Length < 900)
				{
					Building owner = _live[num3].Owner;
					Collider shape = _live[num3].Shape;
					Vector2 normal;
					float num4 = Colliders.Distance(shape, door.Value, out normal);
					stringBuilder.Append(string.Format(" {0} door at {1:F2},{2:F2} in {3} ({4:F2}), centre {5:F2},{6:F2} scale {7:F3}, south of the door:", door.Key.asset?.id, door.Value.x, door.Value.y, (owner == door.Key) ? "itself" : (owner?.asset?.id ?? "a wall"), num4, shape.X, shape.Y, shape.Scale));
					for (int i = 1; i <= 6; i++)
					{
						stringBuilder.Append($" {Colliders.Distance(shape, door.Value - new Vector2(0f, 0.25f * (float)i), out normal):F2}");
					}
					stringBuilder.Append(", east:");
					for (int j = 1; j <= 4; j++)
					{
						stringBuilder.Append($" {Colliders.Distance(shape, door.Value + new Vector2(0.4f * (float)j, 0f), out normal):F2}");
					}
					stringBuilder.Append(';');
				}
			}
			return $"{num} of {num2} doors clear;" + stringBuilder;
		}

		internal static string InsideWhatForTest(Vector2 p)
		{
			int num = (_ready ? InsideOf(p, 0.15f) : (-1));
			if (num < 0)
			{
				return null;
			}
			Building owner = _live[num].Owner;
			if (owner == null || owner.asset == null)
			{
				return "a wall";
			}
			return owner.asset.id;
		}

		public static void Add(Collider shape, Building owner)
		{
			if (!shape.Mobile)
			{
				_pending.Add(new Obstacle
				{
					Shape = shape,
					Owner = owner
				});
			}
		}

		public static void Begin(float eyeX, float eyeY, Actor you, float youRadius)
		{
			CostMs = Mathf.Lerp(CostMs, (float)_ticks * 1000f / (float)Stopwatch.Frequency, 0.1f);
			Walkers = _walkers;
			_walkers = 0;
			long timestamp = Stopwatch.GetTimestamp();
			List<Obstacle> live = _live;
			_live = _pending;
			_pending = live;
			_pending.Clear();
			_x0 = Mathf.FloorToInt(eyeX) - 42;
			_y0 = Mathf.FloorToInt(eyeY) - 42;
			for (int i = 0; i < Head.Length; i++)
			{
				Head[i] = -1;
			}
			_entries = 0;
			OwnerIndex.Clear();
			KeepShapes();
			for (int j = 0; j < _live.Count; j++)
			{
				if (_live[j].Owner != null)
				{
					OwnerIndex[_live[j].Owner] = j;
				}
				Collider shape = _live[j].Shape;
				float num = Reach(shape) + 0.7f;
				int num2 = Mathf.Max(0, Mathf.FloorToInt(shape.X - num) - _x0);
				int num3 = Mathf.Min(83, Mathf.FloorToInt(shape.X + num) - _x0);
				int num4 = Mathf.Max(0, Mathf.FloorToInt(shape.Y - num) - _y0);
				int num5 = Mathf.Min(83, Mathf.FloorToInt(shape.Y + num) - _y0);
				for (int k = num4; k <= num5; k++)
				{
					for (int l = num2; l <= num3; l++)
					{
						Insert(k * 84 + l, j);
					}
				}
			}
			BeginCrowd();
			_haveYou = false;
			try
			{
				if (you != null && you.isAlive() && you.position_height < 0.3f && !you.is_inside_building)
				{
					_you = you.current_position;
					_youRadius = youRadius;
					_haveYou = true;
				}
			}
			catch
			{
			}
			_ready = true;
			_ticks = Stopwatch.GetTimestamp() - timestamp;
		}

		private static void Insert(int cell, int item)
		{
			if (_entries >= _item.Length)
			{
				Array.Resize(ref _item, _item.Length * 2);
				Array.Resize(ref _next, _next.Length * 2);
			}
			_item[_entries] = item;
			_next[_entries] = Head[cell];
			Head[cell] = _entries++;
		}

		private static float Reach(Collider c)
		{
			if (c.Shape != null)
			{
				return c.Shape.Reach * c.Scale;
			}
			if (c.Box)
			{
				return Mathf.Sqrt(c.HalfW * c.HalfW + c.HalfD * c.HalfD);
			}
			return c.Radius;
		}

		private static int CellOf(Vector2 p)
		{
			int num = Mathf.FloorToInt(p.x) - _x0;
			int num2 = Mathf.FloorToInt(p.y) - _y0;
			if (num >= 0 && num2 >= 0 && num < 84 && num2 < 84)
			{
				return num2 * 84 + num;
			}
			return -1;
		}

		public static void SetRadius(Actor a, float radius)
		{
			if (States.TryGetValue(a, out var value))
			{
				value.Radius = Mathf.Clamp(radius, 0.06f, 0.6f);
			}
		}

		private static bool ExemptOld(int i, float radius)
		{
			Obstacle obstacle = _live[i];
			if (obstacle.Owner != null && (obstacle.Owner == _target || obstacle.Owner == _foe))
			{
				return true;
			}
			float num = radius + 0.55f;
			if (_haveGoal && Colliders.Distance(obstacle.Shape, _goal, out var normal) < num)
			{
				return true;
			}
			if (_haveGoal2 && Colliders.Distance(obstacle.Shape, _goal2, out normal) < num)
			{
				return true;
			}
			return false;
		}

		private static float Deepest(Vector2 p, float radius, out Vector2 normal)
		{
			if (!Fast)
			{
				return DeepestOld(p, radius, out normal);
			}
			if (Verify)
			{
				return DeepestVerified(p, radius, out normal);
			}
			return DeepestFast(p, radius, out normal);
		}

		private static float DeepestOld(Vector2 p, float radius, out Vector2 normal)
		{
			normal = Vector2.zero;
			float num = 0f;
			int num2 = CellOf(p);
			if (num2 < 0)
			{
				return 0f;
			}
			for (int num3 = Head[num2]; num3 >= 0; num3 = _next[num3])
			{
				int num4 = _item[num3];
				Vector2 normal2;
				float num5 = Colliders.Distance(_live[num4].Shape, p, out normal2);
				float num6 = radius - num5;
				if (!(num6 <= num) && !ExemptOld(num4, radius))
				{
					num = num6;
					normal = normal2;
					if (Verbose)
					{
						_pressedBy = ((_live[num4].Owner != null && _live[num4].Owner.asset != null) ? _live[num4].Owner.asset.id : "a wall");
					}
				}
			}
			if (_haveYou)
			{
				Vector2 vector = p - _you;
				float magnitude = vector.magnitude;
				float num7 = radius + _youRadius - magnitude;
				if (num7 > num)
				{
					num = num7;
					normal = ((magnitude > 0.0001f) ? (vector / magnitude) : Vector2.right);
					if (Verbose)
					{
						_pressedBy = "you";
					}
				}
			}
			return num;
		}

		private static Vector2 Clear(Vector2 p, float radius, out bool moved)
		{
			moved = false;
			for (int i = 0; i < 4; i++)
			{
				Vector2 normal;
				float num = Deepest(p, radius, out normal);
				if (num <= 0f)
				{
					break;
				}
				p += normal * (num + 0.003f);
				moved = true;
			}
			return p;
		}

		public static bool Steer(Actor a, float dt)
		{
			if (!_ready || dt <= 0f)
			{
				return false;
			}
			long timestamp = Stopwatch.GetTimestamp();
			_walkers++;
			bool result = SteerOne(a, dt);
			try
			{
				if (States.TryGetValue(a, out var value) && value.HiddenFrame != Time.frameCount)
				{
					RememberInCrowd(a, a.current_position, value.Radius, a.is_moving);
				}
			}
			catch
			{
			}
			_ticks += Stopwatch.GetTimestamp() - timestamp;
			return result;
		}

		private static bool SteerOne(Actor a, float dt)
		{
			State state = StateOf(a);
			state.Vel = Vector2.zero;
			float unscaledTime = Time.unscaledTime;
			Vector2 vector = a.current_position;
			if (CellOf(vector) < 0)
			{
				return false;
			}
			float radius = state.Radius;
			bool is_moving;
			try
			{
				is_moving = a.is_moving;
				_target = a.beh_building_target;
				_foe = (a.has_attack_target ? (a.attack_target as Building) : null);
				WorldTile tile_target = a.tile_target;
				WorldTile beh_tile_target = a.beh_tile_target;
				_haveGoal = tile_target != null;
				if (_haveGoal)
				{
					_goal = new Vector2(tile_target.posV3.x, tile_target.posV3.y);
				}
				_haveGoal2 = beh_tile_target != null;
				if (_haveGoal2)
				{
					_goal2 = new Vector2(beh_tile_target.posV3.x, beh_tile_target.posV3.y);
				}
			}
			catch
			{
				return false;
			}
			int num = InsideOf(vector, 0.15f);
			Vector2 normal;
			if (num >= 0)
			{
				Building owner = _live[num].Owner;
				bool num2 = state.OutsideFrame >= Time.frameCount - 3;
				bool flag = is_moving && _haveGoal && Colliders.Distance(_live[num].Shape, _goal, out normal) >= 0f;
				if (!num2 && flag && unscaledTime >= state.GhostUntil && unscaledTime - state.ExitedAt > 4f && owner != null && Doors.TryGetValue(owner, out var value) && CellOf(value) >= 0 && InsideOf(value) < 0)
				{
					state.ExitedAt = unscaledTime;
					Vector2 vector2 = value - new Vector2(_live[num].Shape.X, _live[num].Shape.Y);
					state.Outward = Mathf.Atan2(vector2.y, vector2.x);
					state.Doorway = value - ((vector2.sqrMagnitude > 1E-06f) ? vector2.normalized : Vector2.zero) * 0.45f;
					state.ExitPending = true;
					try
					{
						a.current_position = value;
						a.dirty_current_tile = true;
					}
					catch
					{
					}
					state.OutsideFrame = Time.frameCount;
					DoorExits++;
					NoteCrossing(owner);
					return true;
				}
				state.HiddenFrame = Time.frameCount;
			}
			else
			{
				state.OutsideFrame = Time.frameCount;
			}
			if (unscaledTime < state.GhostUntil)
			{
				return false;
			}
			bool flag2 = false;
			Vector2 value2 = default(Vector2);
			if (is_moving && _target != null && Doors.TryGetValue(_target, out value2) && OwnerIndex.TryGetValue(_target, out var value3) && _haveGoal && Colliders.Distance(_live[value3].Shape, _goal, out normal) < 0.3f && Colliders.Distance(_live[value3].Shape, vector, out normal) > -0.02f && (vector - value2).sqrMagnitude > 0.09f && (vector - value2).sqrMagnitude < 144f)
			{
				flag2 = true;
				DoorWays++;
				_target = null;
				_haveGoal = (_haveGoal2 = false);
			}
			if (!flag2 && is_moving && _target != null && _haveGoal && Doors.TryGetValue(_target, out var value4) && OwnerIndex.TryGetValue(_target, out var value5))
			{
				normal = vector - value4;
				if (normal.sqrMagnitude <= 0.09f && Colliders.Distance(_live[value5].Shape, _goal, out normal) < 0.3f)
				{
					try
					{
						a.next_step_position = _goal;
					}
					catch
					{
					}
					state.GhostUntil = unscaledTime + 3f;
					NoteCrossing(_target);
					DoorsIn++;
					return false;
				}
			}
			Vector2 vector3 = vector;
			if (flag2)
			{
				vector3 = value2;
				try
				{
					a.next_step_position = value2;
				}
				catch
				{
				}
			}
			else if (is_moving)
			{
				vector3 = a.next_step_position;
				if (!_haveGoal || !((vector3 - _goal).sqrMagnitude < 0.01f))
				{
					bool moved;
					Vector2 vector4 = Clear(vector3, radius + 0.04f, out moved);
					if (moved && (vector4 - vector3).sqrMagnitude < 12.25f)
					{
						a.next_step_position = vector4;
						vector3 = vector4;
					}
				}
			}
			Vector2 vector5 = vector;
			Vector2 normal2;
			bool flag3 = Deepest(vector, radius, out normal2) > 0f;
			bool moved2;
			if (flag3)
			{
				Vector2 vector6 = Clear(vector, radius, out moved2) - vector;
				float num3 = (is_moving ? 1f : (2.5f * dt));
				if (vector6.magnitude > num3)
				{
					vector6 = vector6.normalized * num3;
				}
				vector += vector6;
				if (is_moving)
				{
					Vector2 vector7 = vector3 - vector;
					if (vector7.sqrMagnitude > 1E-06f)
					{
						Vector2 normalized = vector7.normalized;
						if ((normalized - normal2 * Vector2.Dot(normalized, normal2)).sqrMagnitude < 0.12f)
						{
							if (state.Side == 0f)
							{
								state.Side = (((a.GetHashCode() & 1) == 0) ? 1f : (-1f));
							}
							Vector2 vector8 = new Vector2(0f - normal2.y, normal2.x) * state.Side;
							float num4 = 1f;
							try
							{
								num4 = Mathf.Max(0.3f, a._current_combined_movement_speed * WalkPace.Of(a));
							}
							catch
							{
							}
							vector += vector8 * Mathf.Min(0.12f, num4 * dt * 0.9f);
							vector = Clear(vector, radius, out moved2);
						}
					}
				}
			}
			bool flag4 = (_haveGoal && (_goal - vector).sqrMagnitude < 2.5600002f) || (_haveGoal2 && (_goal2 - vector).sqrMagnitude < 2.5600002f);
			if (is_moving && !flag4)
			{
				float speed = 1f;
				try
				{
					speed = Mathf.Max(0.3f, a._current_combined_movement_speed * WalkPace.Of(a));
				}
				catch
				{
				}
				vector = Crowd(a, vector, vector3, radius, speed, dt, out moved2);
			}
			if (is_moving)
			{
				float magnitude = (vector3 - vector).magnitude;
				if ((vector3 - state.Waypoint).sqrMagnitude > 0.0001f)
				{
					state.Waypoint = vector3;
					state.Flipped = false;
					state.Best = magnitude;
					state.SinceCloser = 0f;
					state.PushedSince = false;
				}
				else if (magnitude < state.Best - 0.3f)
				{
					state.Best = magnitude;
					state.SinceCloser = 0f;
					state.PushedSince = false;
				}
				else
				{
					state.SinceCloser += dt;
				}
				if (flag3)
				{
					state.PushedSince = true;
				}
				if ((vector - state.Anchor).sqrMagnitude > 0.122499995f)
				{
					state.Anchor = vector;
					state.Blocked = 0f;
				}
				else if (flag3)
				{
					state.Blocked += dt;
				}
				if (state.Blocked > 1.1f && !state.Flipped && state.Side != 0f)
				{
					state.Side = 0f - state.Side;
					state.Flipped = true;
				}
				if (state.Blocked > 2.2f || (state.PushedSince && state.SinceCloser > 4f))
				{
					if (Verbose)
					{
						string text = null;
						try
						{
							text = ((a.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task != null) ? ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id : "none");
						}
						catch
						{
						}
						Log.Info(string.Format("steering: {0} let through ({1}) at {2:F1},{3:F1} ", WorldBoxMod.Describe(a), (state.Blocked > 2.2f) ? "pressed in place" : "no closer for 4 s", vector.x, vector.y) + $"heading for {vector3.x:F1},{vector3.y:F1}, pressed by {_pressedBy}, task {text}, speed {a._current_combined_movement_speed:F2}");
					}
					state.Blocked = 0f;
					state.SinceCloser = 0f;
					state.Best = float.MaxValue;
					state.GhostUntil = unscaledTime + 3f;
					Ghosted++;
				}
			}
			else
			{
				state.Blocked = 0f;
				state.Anchor = vector;
				state.Waypoint = vector;
				state.SinceCloser = 0f;
			}
			if ((vector - vector5).sqrMagnitude < 1E-10f)
			{
				return false;
			}
			try
			{
				a.current_position = vector;
				if (!is_moving && (Mathf.FloorToInt(vector.x) != Mathf.FloorToInt(vector5.x) || Mathf.FloorToInt(vector.y) != Mathf.FloorToInt(vector5.y)))
				{
					a.dirty_current_tile = true;
				}
			}
			catch
			{
				return false;
			}
			return true;
		}

		private static int InsideOf(Vector2 p, float depth = 0.02f)
		{
			if (!Fast)
			{
				return InsideOfOld(p, depth);
			}
			if (Verify)
			{
				return InsideOfVerified(p, depth);
			}
			return InsideOfFast(p, depth);
		}

		private static int InsideOfOld(Vector2 p, float depth)
		{
			int num = CellOf(p);
			if (num < 0)
			{
				return -1;
			}
			for (int num2 = Head[num]; num2 >= 0; num2 = _next[num2])
			{
				int num3 = _item[num2];
				if (Colliders.Distance(_live[num3].Shape, p, out var _) < 0f - depth)
				{
					return num3;
				}
			}
			return -1;
		}

		private static State StateOf(Actor a)
		{
			int frameCount = Time.frameCount;
			if (!States.TryGetValue(a, out var value))
			{
				value = new State();
				States[a] = value;
				if (frameCount - _cleaned > 600)
				{
					_cleaned = frameCount;
					Stale.Clear();
					foreach (KeyValuePair<Actor, State> state in States)
					{
						if (frameCount - state.Value.Frame > 600)
						{
							Stale.Add(state.Key);
						}
					}
					foreach (Actor item in Stale)
					{
						States.Remove(item);
					}
				}
			}
			value.Frame = frameCount;
			return value;
		}

		internal static float OverlapForTest(Vector2 p, float radius)
		{
			float num = 0f;
			int num2 = (_ready ? CellOf(p) : (-1));
			if (num2 < 0)
			{
				return 0f;
			}
			for (int num3 = Head[num2]; num3 >= 0; num3 = _next[num3])
			{
				num = Mathf.Max(num, radius - Colliders.Distance(_live[_item[num3]].Shape, p, out var _));
			}
			return num;
		}

		public static void Reset()
		{
			_pending.Clear();
			_live.Clear();
			Crossings.Clear();
			Array.Clear(_shapes, 0, _shapeCount);
			Array.Clear(_owners, 0, _shapeCount);
			_shapeCount = 0;
			_crowd.Clear();
			_crowdNext.Clear();
			Array.Clear(_cA, 0, _cCount);
			_cCount = 0;
			States.Clear();
			Doors.Clear();
			OwnerIndex.Clear();
			_ready = false;
		}

		private static void KeepShapes()
		{
			int count = _live.Count;
			int num = _shapeCount;
			if (_shapes.Length < count)
			{
				_shapes = new Collider[count * 2];
				_owners = new Building[count * 2];
				num = 0;
			}
			for (int i = 0; i < count; i++)
			{
				Obstacle obstacle = _live[i];
				_shapes[i] = obstacle.Shape;
				_owners[i] = obstacle.Owner;
			}
			for (int j = count; j < num; j++)
			{
				_shapes[j] = default(Collider);
				_owners[j] = null;
			}
			_shapeCount = count;
		}

		private static int InsideOfFast(Vector2 p, float depth)
		{
			int num = CellOf(p);
			if (num < 0)
			{
				return -1;
			}
			Collider[] shapes = _shapes;
			for (int num2 = Head[num]; num2 >= 0; num2 = _next[num2])
			{
				int num3 = _item[num2];
				if (num3 >= _shapeCount)
				{
					return InsideOfOld(p, depth);
				}
				if (Colliders.DistanceTo(in shapes[num3], p) < 0f - depth)
				{
					return num3;
				}
			}
			return -1;
		}

		private static float DeepestFast(Vector2 p, float radius, out Vector2 normal)
		{
			normal = Vector2.zero;
			float num = 0f;
			int num2 = CellOf(p);
			if (num2 < 0)
			{
				return 0f;
			}
			Collider[] shapes = _shapes;
			for (int num3 = Head[num2]; num3 >= 0; num3 = _next[num3])
			{
				int num4 = _item[num3];
				if (num4 >= _shapeCount)
				{
					return DeepestOld(p, radius, out normal);
				}
				Vector2 normal2;
				float num5 = Colliders.DistanceIn(in shapes[num4], p, out normal2);
				float num6 = radius - num5;
				if (!(num6 <= num) && !ExemptFast(num4, radius))
				{
					num = num6;
					normal = normal2;
					if (Verbose)
					{
						_pressedBy = ((_live[num4].Owner != null && _live[num4].Owner.asset != null) ? _live[num4].Owner.asset.id : "a wall");
					}
				}
			}
			if (_haveYou)
			{
				Vector2 vector = p - _you;
				float magnitude = vector.magnitude;
				float num7 = radius + _youRadius - magnitude;
				if (num7 > num)
				{
					num = num7;
					normal = ((magnitude > 0.0001f) ? (vector / magnitude) : Vector2.right);
					if (Verbose)
					{
						_pressedBy = "you";
					}
				}
			}
			return num;
		}

		private static bool ExemptFast(int i, float radius)
		{
			Building building = _owners[i];
			if (building != null && (building == _target || building == _foe))
			{
				return true;
			}
			float num = radius + 0.55f;
			if (_haveGoal && Colliders.DistanceTo(in _shapes[i], _goal) < num)
			{
				return true;
			}
			if (_haveGoal2 && Colliders.DistanceTo(in _shapes[i], _goal2) < num)
			{
				return true;
			}
			return false;
		}

		private static void KeepCrowd()
		{
			int count = _crowd.Count;
			int num = _cCount;
			if (_cA.Length < count)
			{
				int num2 = Math.Max(count, _cA.Length * 2);
				_cA = new Actor[num2];
				_cPx = new float[num2];
				_cPy = new float[num2];
				_cR = new float[num2];
				_cVx = new float[num2];
				_cVy = new float[num2];
				_cMoving = new bool[num2];
				num = 0;
			}
			for (int i = 0; i < count; i++)
			{
				Body body = _crowd[i];
				_cA[i] = body.A;
				_cPx[i] = body.P.x;
				_cPy[i] = body.P.y;
				_cR[i] = body.R;
				_cMoving[i] = body.Moving;
				_cVx[i] = body.V.x;
				_cVy[i] = body.V.y;
			}
			for (int j = count; j < num; j++)
			{
				_cA[j] = null;
			}
			_cCount = count;
		}

		private static Vector2 CrowdFast(Actor a, Vector2 p, Vector2 waypoint, float radius, float speed, float dt, out bool moved)
		{
			moved = false;
			Vector2 vector = waypoint - p;
			Vector2 vector2 = ((vector.sqrMagnitude > 1E-06f) ? vector.normalized : Vector2.zero);
			Vector2 vector3 = new Vector2(vector2.y, 0f - vector2.x);
			int num = Mathf.FloorToInt(p.x) - _x0;
			int num2 = Mathf.FloorToInt(p.y) - _y0;
			float num3 = 0f;
			float num4 = 0f;
			float num5 = Mathf.Max(0.7f, speed * 0.8f);
			float num6 = Mathf.Max(1.2f, speed * 1.5f);
			Vector2 vel = vector2 * speed;
			if (States.TryGetValue(a, out var value))
			{
				value.Vel = vel;
			}
			bool flag = vector2 != Vector2.zero;
			float x = p.x;
			float y = p.y;
			float x2 = vel.x;
			float y2 = vel.y;
			float x3 = vector2.x;
			float y3 = vector2.y;
			float x4 = vector3.x;
			float y4 = vector3.y;
			bool flag2 = num >= -1 && num <= 84 && num2 >= -1 && num2 <= 84;
			int num7 = num2 - 1;
			while (flag2 && num7 <= num2 + 1)
			{
				for (int i = num - 1; i <= num + 1; i++)
				{
					if (i < 0 || num7 < 0 || i >= 84 || num7 >= 84)
					{
						continue;
					}
					for (int num8 = CrowdHead[num7 * 84 + i]; num8 >= 0; num8 = _crowdLink[num8])
					{
						if (num8 >= _cCount)
						{
							return CrowdOld(a, p, waypoint, radius, speed, dt, out moved);
						}
						if (_cA[num8] != a)
						{
							float num9 = _cPx[num8];
							float num10 = _cPy[num8];
							float num11 = num9 - x;
							float num12 = num10 - y;
							float num13 = num11;
							float num14 = num12;
							float num15 = (float)Math.Sqrt(num13 * num13 + num14 * num14);
							float num16 = radius + _cR[num8] + 0.05f;
							if (num15 < 0.0001f)
							{
								if (vector3.sqrMagnitude > 0f)
								{
									num13 = vector3.x;
									num14 = vector3.y;
								}
								else
								{
									num13 = 1f;
									num14 = 0f;
								}
							}
							else
							{
								num13 /= num15;
								num14 /= num15;
							}
							if (num15 < num16)
							{
								float num17 = Mathf.Min(num16 - num15, num6 * dt);
								float num18 = num13 * num17;
								float num19 = num14 * num17;
								float num20 = (_cMoving[num8] ? 0.5f : 1f);
								num18 *= num20;
								num19 *= num20;
								num3 -= num18;
								num4 -= num19;
							}
							else if (flag && num15 < num16 + num5 + 2f * speed * 0.8f)
							{
								float num21 = _cVx[num8] - x2;
								float num22 = _cVy[num8] - y2;
								float num23 = num21 * num21 + num22 * num22;
								float num24 = ((num23 > 0.0001f) ? Mathf.Clamp((0f - (num11 * num21 + num12 * num22)) / num23, 0f, 0.8f) : 0f);
								float num25 = num11 + num21 * num24;
								float num26 = num12 + num22 * num24;
								float num27 = (float)Math.Sqrt(num25 * num25 + num26 * num26);
								if (!(num27 >= num16 + 0.08f) && (!(num24 <= 0f) || !(x3 * num13 + y3 * num14 <= 0.5f)))
								{
									float num28 = x4 * num25 + y4 * num26;
									float num29 = ((Mathf.Abs(num28) < 0.05f) ? 1f : ((num28 > 0f) ? (-1f) : 1f));
									float num30 = 1f - num24 / 0.8f;
									float num31 = 1f - num27 / (num16 + 0.08f);
									float num32 = x4 * num29;
									float num33 = y4 * num29;
									float num34 = Mathf.Min(0.08f, speed * dt * 1.2f);
									num32 *= num34;
									num33 *= num34;
									float num35 = Mathf.Clamp01(0.35f + num30);
									num32 *= num35;
									num33 *= num35;
									float num36 = Mathf.Clamp01(0.4f + num31);
									num32 *= num36;
									num33 *= num36;
									num3 += num32;
									num4 += num33;
								}
							}
						}
					}
				}
				num7++;
			}
			Vector2 vector4 = new Vector2(num3, num4);
			if (vector4.sqrMagnitude < 1E-10f)
			{
				return p;
			}
			moved = true;
			bool moved2;
			return Clear(p + vector4, radius, out moved2);
		}

		private static Vector2 CrowdVerified(Actor a, Vector2 p, Vector2 waypoint, float radius, float speed, float dt, out bool moved)
		{
			bool moved2;
			Vector2 vector = CrowdFast(a, p, waypoint, radius, speed, dt, out moved2);
			Vector2 result = CrowdOld(a, p, waypoint, radius, speed, dt, out moved);
			Verified++;
			CrowdVerifiedCount++;
			if (moved2 != moved || NpcTune.Bits(vector.x) != NpcTune.Bits(result.x) || NpcTune.Bits(vector.y) != NpcTune.Bits(result.y))
			{
				CrowdMismatchedCount++;
				if (CrowdFirstMismatch == null)
				{
					CrowdFirstMismatch = $"round the crowd from {p.x:R},{p.y:R} (radius {radius:R}, pace {speed:R}): {NpcTune.Show(vector.x)},{NpcTune.Show(vector.y)} moved {moved2}, the old way {NpcTune.Show(result.x)},{NpcTune.Show(result.y)} moved {moved}";
				}
				Mismatch($"round the crowd from {p.x:R},{p.y:R} (radius {radius:R}, pace {speed:R}): {NpcTune.Show(vector.x)},{NpcTune.Show(vector.y)} moved {moved2}, the old way {NpcTune.Show(result.x)},{NpcTune.Show(result.y)} moved {moved}");
			}
			return result;
		}

		private static int InsideOfVerified(Vector2 p, float depth)
		{
			int num = InsideOfFast(p, depth);
			int num2 = InsideOfOld(p, depth);
			Verified++;
			if (num != num2)
			{
				Mismatch($"inside of at {p.x:R},{p.y:R} (depth {depth:R}): obstacle {num}, the old way {num2}");
			}
			return num2;
		}

		private static float DeepestVerified(Vector2 p, float radius, out Vector2 normal)
		{
			Vector2 normal2;
			float f = DeepestFast(p, radius, out normal2);
			float num = DeepestOld(p, radius, out normal);
			Verified++;
			if (NpcTune.Bits(f) != NpcTune.Bits(num) || NpcTune.Bits(normal2.x) != NpcTune.Bits(normal.x) || NpcTune.Bits(normal2.y) != NpcTune.Bits(normal.y))
			{
				Mismatch($"deepest at {p.x:R},{p.y:R} (radius {radius:R}): {NpcTune.Show(f)} out {NpcTune.Show(normal2.x)},{NpcTune.Show(normal2.y)}, the old way {NpcTune.Show(num)} out {NpcTune.Show(normal.x)},{NpcTune.Show(normal.y)}");
			}
			return num;
		}

		private static void Mismatch(string what)
		{
			Mismatched++;
			if (FirstMismatch == null)
			{
				FirstMismatch = what;
			}
		}
	}
}
