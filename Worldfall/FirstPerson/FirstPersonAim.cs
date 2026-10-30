using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class FirstPersonAim
	{
		private static int _frame = -100;

		private static Actor _host;

		private static float _eyeX;

		private static float _eyeY;

		private static float _eyeZ;

		private static float _yaw;

		private static float _slope;

		private static float _body;

		private static float _feet;

		private static float _range;

		private static BaseSimObject _picked;

		private static float _pickDepth;

		private static TerrainCache _terrain;

		private static readonly List<Actor> Candidates = new List<Actor>();

		private static readonly List<Actor> InFight = new List<Actor>();

		private static readonly List<float> FoeDepths = new List<float>();

		private static int _handFrame = -100;

		private static Vec3 _handFrom;

		private static Vec3 _freeFrom;

		private static float _handYaw;

		private static float _handSlope;

		private static float _freeYaw;

		private static float _freeSlope;

		private static float _heldUntil = -1f;

		private const float FoeCone = 35f;

		private const float BodyDepth = 0.35f;

		private static bool _foeAtHand;

		private const float FoeLine = 14f;

		private const float FoeNearLine = 0.6f;

		private const float FoeBehind = 2f;

		private const float FoeAhead = 0.8f;

		private static float _foeBehind = 14f;

		private static float _foeReach = 14f;

		internal static List<string> Trace;

		public static TerrainCache Terrain => _terrain;

		public static BaseSimObject Picked => _picked;

		public static float PickDepth => _pickDepth;

		public static Vec3 Eye => new Vec3(_eyeX, _eyeY, _eyeZ);

		public static float Yaw => _yaw;

		public static float Slope => _slope;

		public static float Body => _body;

		public static Vector2 Flat => new Vector2(Mathf.Cos(_yaw), Mathf.Sin(_yaw));

		public static Vec3 Ray => new Vec3(Mathf.Cos(_yaw), Mathf.Sin(_yaw), _slope).Normalized();

		public static bool HandAim => Time.frameCount - _handFrame <= 3;

		public static Vec3 HandFrom => _handFrom;

		public static Vec3 FreeFrom => _freeFrom;

		public static float ShotYaw
		{
			get
			{
				if (!HandAim)
				{
					return _yaw;
				}
				return _handYaw;
			}
		}

		public static float ShotSlope
		{
			get
			{
				if (!HandAim)
				{
					return _slope;
				}
				return _handSlope;
			}
		}

		public static Vector2 ShotFlat => new Vector2(Mathf.Cos(ShotYaw), Mathf.Sin(ShotYaw));

		public static Vec3 ShotRay => new Vec3(Mathf.Cos(ShotYaw), Mathf.Sin(ShotYaw), ShotSlope).Normalized();

		public static float ThrowYaw
		{
			get
			{
				if (!HandAim)
				{
					return _yaw;
				}
				return _freeYaw;
			}
		}

		public static float ThrowSlope
		{
			get
			{
				if (!HandAim)
				{
					return _slope;
				}
				return _freeSlope;
			}
		}

		public static void Set(Actor host, ViewState view, float body, float feet, BaseSimObject picked, float pickDepth, TerrainCache terrain, float range)
		{
			_frame = Time.frameCount;
			_host = host;
			_eyeX = view.X;
			_eyeY = view.Y;
			_eyeZ = view.Z;
			_yaw = view.Yaw;
			_slope = view.Pitch;
			_body = body;
			_feet = feet;
			_picked = picked;
			_pickDepth = pickDepth;
			_terrain = terrain;
			_range = range;
		}

		public static bool Ready(Actor host)
		{
			if (host != null && host == _host && Time.frameCount - _frame <= 3)
			{
				return _terrain != null;
			}
			return false;
		}

		public static void Clear()
		{
			_host = null;
			_picked = null;
		}

		private static Vec3 At(float depth)
		{
			return new Vec3(_eyeX + Mathf.Cos(_yaw) * depth, _eyeY + Mathf.Sin(_yaw) * depth, _eyeZ + _slope * depth);
		}

		public static bool Point(out Vec3 point, out Actor creature, bool pastFriends = false)
		{
			return Point(out point, out creature, pastFriends, 14f, 14f);
		}

		private static bool Point(out Vec3 point, out Actor creature, bool pastFriends, float foeBehind, float foeReach)
		{
			_foeBehind = foeBehind;
			_foeReach = foeReach;
			creature = null;
			point = default(Vec3);
			if (_terrain == null)
			{
				return false;
			}
			FoeDepths.Clear();
			if (pastFriends)
			{
				FoesOnRay(Mathf.Min(_range, 14f));
			}
			bool flag = pastFriends && _picked is Actor a && LookPast(a);
			if (_picked != null && !flag && _pickDepth * Mathf.Sqrt(1f + _slope * _slope) > 0.05f && _pickDepth < _range && (!(_picked is Building b) || AssetStyle.IsSolid(b)))
			{
				point = At(_pickDepth);
				creature = _picked as Actor;
				if (creature != null && !Alive(creature))
				{
					creature = null;
				}
				return true;
			}
			float num = Ground(_range);
			float depth;
			Actor actor = OnRay((num > 0f) ? num : _range, _host, out depth, pastFriends);
			if (actor != null)
			{
				point = At(depth);
				creature = actor;
				return true;
			}
			if (num <= 0f)
			{
				return false;
			}
			point = At(num);
			return true;
		}

		public static void SetHand(Vec3 from, Vec3 dir, Vec3 freeFrom, Vec3 freeDir)
		{
			_handFrame = Time.frameCount;
			_freeFrom = freeFrom;
			Heading(freeDir, out _freeYaw, out _freeSlope);
			if (!(Time.unscaledTime < _heldUntil))
			{
				_handFrom = from;
				Heading(dir, out _handYaw, out _handSlope);
			}
		}

		public static void ClearHand()
		{
			_handFrame = -100;
			_heldUntil = -1f;
		}

		public static void HoldShot(Vec3 from, Vec3 dir)
		{
			_handFrame = Time.frameCount;
			_handFrom = from;
			Heading(dir, out _handYaw, out _handSlope);
			_heldUntil = Time.unscaledTime + 1.5f;
		}

		public static void ReleaseShot()
		{
			_heldUntil = -1f;
		}

		private static void Heading(Vec3 d, out float yaw, out float slope)
		{
			float num = Mathf.Sqrt(d.X * d.X + d.Y * d.Y);
			yaw = ((num > 0.0001f) ? Mathf.Atan2(d.Y, d.X) : _yaw);
			slope = Mathf.Clamp(d.Z / Mathf.Max(num, 0.08f), -12f, 12f);
		}

		public static bool ShotPoint(out Vec3 point, out Actor creature, bool pastFriends = false)
		{
			if (!HandAim)
			{
				return Point(out point, out creature, pastFriends);
			}
			return Along(_handFrom, _handYaw, _handSlope, out point, out creature, pastFriends);
		}

		public static bool ThrowPoint(out Vec3 point, out Actor creature, bool pastFriends = false)
		{
			if (!HandAim)
			{
				return Point(out point, out creature, pastFriends);
			}
			return Along(_freeFrom, _freeYaw, _freeSlope, out point, out creature, pastFriends);
		}

		private static bool Along(Vec3 from, float yaw, float slope, out Vec3 point, out Actor creature, bool pastFriends)
		{
			float eyeX = _eyeX;
			float eyeY = _eyeY;
			float eyeZ = _eyeZ;
			float yaw2 = _yaw;
			float slope2 = _slope;
			float pickDepth = _pickDepth;
			BaseSimObject picked = _picked;
			_eyeX = from.X;
			_eyeY = from.Y;
			_eyeZ = from.Z;
			_yaw = yaw;
			_slope = slope;
			_picked = null;
			try
			{
				return Point(out point, out creature, pastFriends);
			}
			finally
			{
				_eyeX = eyeX;
				_eyeY = eyeY;
				_eyeZ = eyeZ;
				_yaw = yaw2;
				_slope = slope2;
				_picked = picked;
				_pickDepth = pickDepth;
			}
		}

		private static float Ground(float limit)
		{
			float num = 0f;
			for (float num2 = 0.2f; num2 <= limit; num2 += 0.12f + num2 * 0.02f)
			{
				if (Below(num2))
				{
					float num3 = num;
					float num4 = num2;
					for (int i = 0; i < 8; i++)
					{
						float num5 = (num3 + num4) * 0.5f;
						if (Below(num5))
						{
							num4 = num5;
						}
						else
						{
							num3 = num5;
						}
					}
					return num4;
				}
				num = num2;
			}
			return -1f;
		}

		private static bool Below(float t)
		{
			Vec3 vec = At(t);
			return vec.Z <= _terrain.TopAt(vec.X, vec.Y);
		}

		private static Actor OnRay(float limit, Actor skip, out float depth, bool pastFriends)
		{
			depth = 0f;
			Vec3 eye = Eye;
			Vec3 vec = At(limit);
			Hitboxes.Near(eye, vec, 3f, skip, Candidates);
			Actor actor = null;
			float num = 1f;
			Mathf.Sqrt(Vec3.Dot(vec - eye, vec - eye));
			for (int i = 0; i < Candidates.Count; i++)
			{
				if (Hitboxes.Get(Candidates[i], out var box))
				{
					float num2 = box.Centre.X - eye.X;
					float num3 = box.Centre.Y - eye.Y;
					float pad = 0.02f + 0.006f * Mathf.Sqrt(num2 * num2 + num3 * num3);
					if (box.Segment(eye, vec, pad, out var t) && t < num && (!pastFriends || !LookPast(Candidates[i])))
					{
						num = t;
						actor = Candidates[i];
					}
				}
			}
			if (actor != null)
			{
				depth = limit * num;
			}
			return actor;
		}

		public static Actor Melee(Actor self, float reach)
		{
			_foeAtHand = FoeAtHand(self, reach);
			try
			{
				return MeleeTarget(self, reach);
			}
			finally
			{
				_foeAtHand = false;
			}
		}

		private static Actor MeleeTarget(Actor self, float reach)
		{
			if (Point(out var _, out var creature, pastFriends: true, 2f, reach + 0.35f) && creature != null && creature != self && InReach(creature, reach, out var _, out var _))
			{
				return creature;
			}
			Vec3 eye = Eye;
			Hitboxes.Near(eye, eye, reach + 3f, self, Candidates);
			Actor result = null;
			float num = float.MaxValue;
			for (int i = 0; i < Candidates.Count; i++)
			{
				Actor actor = Candidates[i];
				if (!InReach(actor, reach, out var flat2, out var nearest2) || LookPast(actor))
				{
					continue;
				}
				Vector2 vector = nearest2 - new Vector2(eye.X, eye.Y);
				float num2 = ((vector.sqrMagnitude < 0.0025f) ? 0f : Mathf.Abs(Mathf.DeltaAngle(_yaw * 57.29578f, Mathf.Atan2(vector.y, vector.x) * 57.29578f)));
				bool flag = Foe(self, actor);
				if (!(num2 > (flag ? 35f : 18f)))
				{
					float num3 = num2 + flat2 * 20f + (flag ? 0f : 25f);
					if (num3 < num)
					{
						num = num3;
						result = actor;
					}
				}
			}
			return result;
		}

		private static bool FoeAtHand(Actor self, float reach)
		{
			try
			{
				Vec3 eye = Eye;
				Hitboxes.Near(eye, eye, reach + 3f, self, InFight);
				for (int i = 0; i < InFight.Count; i++)
				{
					Actor actor = InFight[i];
					if (!FriendlyFire.InFight(self, actor))
					{
						if (Trace != null && Trace.Count < 32 && !FriendlyFire.Bystanding(self, actor) && actor.kingdom != null && !actor.kingdom.isCiv())
						{
							Trace.Add(WorldBoxMod.Describe(actor) + " not in a fight (going for " + ((actor.has_attack_target && actor.attack_target is Actor a) ? WorldBoxMod.Describe(a) : "nobody") + ")");
						}
						continue;
					}
					float flat;
					Vector2 nearest;
					bool flag = InReach(actor, reach, out flat, out nearest);
					Vector2 vector = nearest - new Vector2(eye.X, eye.Y);
					float num = ((vector.sqrMagnitude < 0.0025f) ? 0f : Mathf.Abs(Mathf.DeltaAngle(_yaw * 57.29578f, Mathf.Atan2(vector.y, vector.x) * 57.29578f)));
					if (Trace != null && Trace.Count < 32)
					{
						Trace.Add(string.Format("{0} in a fight, {1} ({2:F2} of {3:F2}), {4:F0} deg", WorldBoxMod.Describe(actor), flag ? "in reach" : "out of reach", flat, reach, num));
					}
					if (flag && num <= 35f)
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

		private static bool LookPast(Actor a)
		{
			if (FriendlyFire.Holds(_host, a))
			{
				return true;
			}
			if (_foeAtHand && FriendlyFire.Bystanding(_host, a))
			{
				if (Trace != null && Trace.Count < 32)
				{
					Trace.Add(WorldBoxMod.Describe(a) + " looked past (a foe in a fight at hand)");
				}
				return true;
			}
			if (FoeDepths.Count == 0 || !FriendlyFire.Bystanding(_host, a) || !Hitboxes.Get(a, out var box))
			{
				return false;
			}
			float num = AlongView(box);
			bool flag = false;
			for (int i = 0; i < FoeDepths.Count; i++)
			{
				if (flag)
				{
					break;
				}
				flag = FoeDepths[i] >= num - 0.8f && FoeDepths[i] <= Mathf.Min(num + _foeBehind, _foeReach);
			}
			if (Trace != null && Trace.Count < 32)
			{
				Trace.Add(string.Format("{0} at {1:F2} {2} (foes at {3}, reach {4:F2})", WorldBoxMod.Describe(a), num, flag ? "looked past" : "kept", string.Join(" ", FoeDepths.ConvertAll((float d) => d.ToString("F2"))), _foeReach));
			}
			return flag;
		}

		private static void FoesOnRay(float limit)
		{
			try
			{
				Vec3 eye = Eye;
				Vec3 b = At(limit);
				Hitboxes.Near(eye, b, 3f, _host, InFight);
				for (int i = 0; i < InFight.Count; i++)
				{
					Actor a = InFight[i];
					if (FriendlyFire.InFight(_host, a) && Hitboxes.Get(a, out var box) && box.Segment(eye, b, 0.6f, out var _))
					{
						FoeDepths.Add(AlongView(box));
					}
				}
			}
			catch
			{
			}
		}

		private static float AlongView(Hitbox box)
		{
			return (box.Centre.X - _eyeX) * Mathf.Cos(_yaw) + (box.Centre.Y - _eyeY) * Mathf.Sin(_yaw);
		}

		private static bool InReach(Actor a, float reach, out float flat, out Vector2 nearest)
		{
			flat = 0f;
			nearest = default(Vector2);
			if (!Hitboxes.Get(a, out var box))
			{
				return false;
			}
			flat = box.FlatDistance(_eyeX, _eyeY, out nearest);
			if (flat > reach)
			{
				return false;
			}
			if (box.Bottom <= _eyeZ + Mathf.Max(0.35f, _body * 0.45f))
			{
				return box.Top >= _feet - 0.1f - _body * 0.5f;
			}
			return false;
		}

		private static bool Foe(Actor self, Actor a)
		{
			try
			{
				if (CombatFeedback.FightingLately(a, 10f))
				{
					return true;
				}
				if (a.has_attack_target && a.attack_target == self)
				{
					return true;
				}
				return self.areFoes(a);
			}
			catch
			{
				return false;
			}
		}

		private static bool Alive(Actor a)
		{
			try
			{
				return a.isAlive();
			}
			catch
			{
				return false;
			}
		}
	}
}
