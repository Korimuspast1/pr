using System;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class SceneView
	{
		public const float ZoomNear = 5f;

		public const float ZoomStart = 16f;

		private const float PanSpeed = 1.7f;

		private const float TurnSpeed = 1.75f;

		private const float TipSpeed = 1.1f;

		private const float Ramp = 0.14f;

		public bool On;

		public MapTabs.Page Page;

		public Vector2 At;

		public float Ground = float.NaN;

		public float Zoom = 16f;

		public float ZoomGoal = 16f;

		private Vector2 _pivot;

		private Vector2 _pivotMouse;

		private bool _pivotSet;

		private bool _held;

		private float _heldTail;

		public float Turn;

		public float Tip;

		private float _turnRate;

		private float _tipRate;

		private Actor _follow;

		private long _followId = -1L;

		public bool FollowMarked;

		private Vector2 _followVel;

		internal Vector2? TestMove;

		internal Vector2? TestTurn;

		public Actor Follow
		{
			get
			{
				return _follow;
			}
			set
			{
				_follow = value;
				_followId = -1L;
				try
				{
					if (value != null)
					{
						_followId = value.data.id;
					}
				}
				catch
				{
				}
			}
		}

		public bool Following
		{
			get
			{
				if (Follow == null)
				{
					return FollowMarked;
				}
				return true;
			}
		}

		public Vector2 Ahead => new Vector2(0f - Mathf.Sin(Turn), Mathf.Cos(Turn));

		public Vector2 Right => new Vector2(Mathf.Cos(Turn), Mathf.Sin(Turn));

		public bool Pivot(out Vector2 ground, out Vector2 mouse)
		{
			ground = _pivot;
			mouse = _pivotMouse;
			if (_pivotSet || _held || _heldTail > 0f)
			{
				return !Following;
			}
			return false;
		}

		public void Hold(Vector2 ground, Vector2 mouse)
		{
			StopFollow();
			_pivot = ground;
			_pivotMouse = mouse;
			_held = true;
		}

		public void LetGo()
		{
			if (_held)
			{
				_held = false;
				_heldTail = 0.8f;
			}
		}

		public void Open(MapTabs.Page page, Vector2 at)
		{
			On = true;
			Page = page;
			At = at;
			Ground = float.NaN;
			ZoomGoal = Zoom;
			_pivotSet = (_held = false);
			_heldTail = 0f;
			_turnRate = (_tipRate = 0f);
			StopFollow();
		}

		public void StopFollow()
		{
			Follow = null;
			FollowMarked = false;
			_followVel = Vector2.zero;
		}

		public ViewState Camera(int width, int height)
		{
			return Camera(At, Ground, Zoom, Turn, Tip, width, height);
		}

		public static ViewState Camera(Vector2 at, float ground, float zoom, float turn, float tip, int width, int height)
		{
			float num = MathF.PI / 3f;
			float num2 = MathF.PI / 2f + turn;
			float num3 = (float)width * 0.5f / Mathf.Tan(num * 0.5f);
			float num4 = 2f * Mathf.Sqrt(2f) * num3 * zoom / (float)Mathf.Max(1, height);
			float num5 = num4 * Mathf.Cos(MathF.PI / 4f);
			return GodTurn.Tip(new ViewState
			{
				X = at.x - Mathf.Cos(num2) * num5,
				Y = at.y - Mathf.Sin(num2) * num5,
				Z = ground + num4 * Mathf.Sin(MathF.PI / 4f),
				Yaw = num2,
				Pitch = 0f - Mathf.Tan(MathF.PI / 4f),
				Fov = num,
				Shear = 1f
			}, new Vec3(at.x, at.y, ground), tip);
		}

		public void Update(float dt, Vector2 move, float spin, float tip, bool fast, Vector2? band, float zoomFar)
		{
			if (TestMove.HasValue)
			{
				move = TestMove.Value;
			}
			if (TestTurn.HasValue)
			{
				spin = TestTurn.Value.x;
				tip = TestTurn.Value.y;
			}
			if (move != Vector2.zero || spin != 0f || tip != 0f)
			{
				_held = false;
				_heldTail = 0f;
				_pivotSet = false;
			}
			_heldTail = Mathf.Max(0f, _heldTail - dt);
			if (move != Vector2.zero)
			{
				StopFollow();
				At += (Ahead * move.y + Right * move.x).normalized * Zoom * 1.7f * (fast ? 2f : 1f) * dt;
			}
			float t = 1f - Mathf.Exp((0f - dt) / 0.14f);
			_turnRate = Mathf.Lerp(_turnRate, spin * 1.75f * (fast ? 2f : 1f), t);
			float num = tip * 1.1f;
			if (num > 0f)
			{
				num *= Mathf.SmoothStep(0.15f, 1f, (1f - Tip) / 0.25f);
			}
			else if (num < 0f)
			{
				num *= Mathf.SmoothStep(0.15f, 1f, Tip / 0.25f);
			}
			_tipRate = Mathf.Lerp(_tipRate, num, t);
			if (spin == 0f && Mathf.Abs(_turnRate) < 0.001f)
			{
				_turnRate = 0f;
			}
			if (tip == 0f && Mathf.Abs(_tipRate) < 0.001f)
			{
				_tipRate = 0f;
			}
			Turn = DivePath.Wrap(Turn + _turnRate * dt);
			Tip = Mathf.Clamp01(Tip + _tipRate * dt);
			if ((Tip <= 0f && _tipRate < 0f) || (Tip >= 1f && _tipRate > 0f))
			{
				_tipRate = 0f;
			}
			ZoomGoal = Mathf.Clamp(ZoomGoal, 5f, zoomFar);
			float zoom = Zoom;
			Zoom = ((Mathf.Abs(ZoomGoal - Zoom) < 0.01f) ? ZoomGoal : Mathf.Lerp(Zoom, ZoomGoal, 1f - Mathf.Exp(-12f * dt)));
			if (_pivotSet && !Following && zoom > 0f)
			{
				At = _pivot + (At - _pivot) * (Zoom / zoom);
			}
			if (_pivotSet && Zoom == ZoomGoal && zoom == Zoom)
			{
				_pivotSet = false;
				_heldTail = Mathf.Max(_heldTail, 0.8f);
			}
			Vector2? vector = null;
			if (Follow != null)
			{
				try
				{
					if (Follow.isAlive() && Follow.data.id == _followId)
					{
						vector = Follow.current_position;
					}
				}
				catch
				{
				}
				if (!vector.HasValue)
				{
					StopFollow();
				}
			}
			else if (FollowMarked)
			{
				if (band.HasValue)
				{
					vector = band;
				}
				else
				{
					StopFollow();
				}
			}
			if (vector.HasValue)
			{
				_pivotSet = false;
				_heldTail = 0f;
				At = Vector2.SmoothDamp(At, vector.Value, ref _followVel, 0.25f, 400f, Mathf.Max(0.0001f, dt));
			}
			At = new Vector2(Mathf.Clamp(At.x, 0f, WarMap.WorldWidth), Mathf.Clamp(At.y, 0f, WarMap.WorldHeight));
		}

		public void Wheel(float notches, Vector2? ground, Vector2 mouse, float zoomFar)
		{
			ZoomGoal = Mathf.Clamp(ZoomGoal * Mathf.Pow(0.85f, notches), 5f, zoomFar);
			_pivotSet = ground.HasValue && !Following;
			if (_pivotSet)
			{
				_pivot = ground.Value;
				_pivotMouse = mouse;
			}
		}

		public void JumpTo(Vector2 at)
		{
			StopFollow();
			_pivotSet = (_held = false);
			_heldTail = 0f;
			At = new Vector2(Mathf.Clamp(at.x, 0f, WarMap.WorldWidth), Mathf.Clamp(at.y, 0f, WarMap.WorldHeight));
		}

		public void Unhold()
		{
			_held = (_pivotSet = false);
			_heldTail = 0f;
		}

		public void Dragged(Vector2 at)
		{
			StopFollow();
			At = new Vector2(Mathf.Clamp(at.x, 0f, WarMap.WorldWidth), Mathf.Clamp(at.y, 0f, WarMap.WorldHeight));
		}
	}
}
