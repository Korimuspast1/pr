using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class RoomFloors
	{
		private static readonly ConditionalWeakTable<Interior, object> Opened = new ConditionalWeakTable<Interior, object>();

		private static Interior _room;

		private static RoomFloor _floor;

		private static volatile RoomFloor _built;

		private static int _models;

		private static float _feet;

		private static float _shown;

		private static float _vel;

		private static float _body = 1f;

		private static readonly RoomFloor.EyeCut Cut = new RoomFloor.EyeCut();

		private const float UnderCeiling = 0.2f;

		public static float Feet => _feet;

		public static float Shown => _shown;

		public static bool Low => _feet < 0.4f;

		internal static bool Ready => _floor?.Field != null;

		internal static RoomFloor ForTest => _floor;

		internal static float BodyForTest => _body;

		internal static void GroundForTest()
		{
			_feet = 0f;
			_shown = 0f;
			_vel = 0f;
		}

		internal static void FeetForTest(float z)
		{
			_feet = z;
			_shown = z;
			_vel = 0f;
		}

		public static void Begin(Interior room)
		{
			if (room == null)
			{
				_room = null;
				_floor = null;
				return;
			}
			if (room != _room)
			{
				_room = room;
				_floor = null;
				_built = null;
				_feet = (_shown = 0f);
				_vel = 0f;
				Cut.Reset();
				if (!Opened.TryGetValue(room, out var _))
				{
					Opened.Add(room, new object());
					try
					{
						if (RoomFloor.Hatches(room))
						{
							Log.Info("room: opened the ceiling over its stair");
						}
					}
					catch (Exception ex)
					{
						Log.Info("room hatch: " + ex.Message);
					}
				}
				Rebuild(room);
			}
			else if (room.Meshes.Count != _models)
			{
				Rebuild(room);
			}
			RoomFloor built = _built;
			if (built != null && built != _floor)
			{
				_floor = built;
			}
		}

		private static void Rebuild(Interior room)
		{
			_models = room.Meshes.Count;
			ThreadPool.QueueUserWorkItem(delegate
			{
				try
				{
					Stopwatch stopwatch = Stopwatch.StartNew();
					RoomFloor roomFloor = RoomFloor.Build(room);
					if (room == _room)
					{
						_built = roomFloor;
					}
					Log.Info($"room floor: {((roomFloor.Field != null) ? roomFloor.Field.Faces : 0)} faces, {roomFloor.WalkOn.Count} climbed, {stopwatch.Elapsed.TotalMilliseconds:F0} ms");
				}
				catch (Exception ex)
				{
					Log.Info("room floor: " + ex.Message);
				}
			});
		}

		public static void SetBody(float body)
		{
			_body = RoomFloor.Stooped(_room, Mathf.Clamp(body, 0.3f, 2f));
		}

		public static bool Climbs(float[] solid)
		{
			if (_floor != null)
			{
				return _floor.WalkOn.Contains(solid);
			}
			return false;
		}

		public static bool CanMove(Vector2 to)
		{
			if (_floor != null)
			{
				return _floor.CanMove(to.x, to.y, _feet, _body);
			}
			return true;
		}

		public static float Pace(Vector2 at, Vector2 move, float speed)
		{
			if (_floor != null)
			{
				return _floor.Pace(at.x, at.y, move.x, move.y, _feet, _body, speed);
			}
			return 1f;
		}

		public static void Stepped(Vector2 at)
		{
			if (_floor?.Field != null)
			{
				float num = _floor.StandAt(at.x, at.y, _feet, _body);
				if (num >= _feet || _floor.Clear(at.x, at.y, num, _body))
				{
					_feet = num;
				}
			}
		}

		public static Vector2 Settle(Vector2 eye, float dt)
		{
			if (_floor?.Field == null)
			{
				_feet = (_shown = 0f);
				return eye;
			}
			float x = eye.x;
			float y = eye.y;
			_feet = _floor.Settle(ref x, ref y, _feet, _body);
			if (Mathf.Abs(_shown - _feet) > 2.5f)
			{
				_shown = _feet;
				_vel = 0f;
			}
			else
			{
				_shown = Mathf.SmoothDamp(_shown, _feet, ref _vel, (_feet < _shown - 0.3f) ? 0.11f : 0.06f, 40f, Mathf.Max(0.0001f, dt));
			}
			_shown = Mathf.Max(_shown, _feet - 0.12f * _body);
			return new Vector2(x, y);
		}

		public static float EyeLimit(Vector2 at, float eye = float.NaN)
		{
			float num = ((_floor == null) ? float.PositiveInfinity : (_floor.CeilingOver(at.x, at.y, _feet, _body) - 0.06f));
			FitInfo fitInfo = _room?.Fit;
			if (fitInfo != null)
			{
				try
				{
					num = Mathf.Min(num, RoomFloor.FittedEyeCeiling(fitInfo, at.x, at.y, _feet, eye, _shown) - 0.2f);
				}
				catch
				{
				}
			}
			return num;
		}

		public static float EyeUnder(float eye, Vector2 at, float dt)
		{
			return Cut.Apply(eye, EyeLimit(at, eye), dt);
		}

		public static void Leave()
		{
			_room = null;
			_floor = null;
			_built = null;
			_feet = (_shown = 0f);
		}
	}
}
