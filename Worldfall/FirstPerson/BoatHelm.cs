using System;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class BoatHelm
	{
		public static Actor Boat;

		public static float Heading;

		public static float Sails;

		public static float Wheel;

		public static float Way;

		public static float Bump;

		public const float WheelTurn = 2.4f;

		private const float TurnRate = 0.75f;

		private static Vector2 _lastPos;

		private static float _stuck;

		public static bool Steers(Actor a)
		{
			if (a != null)
			{
				return a == Boat;
			}
			return false;
		}

		public static bool IsBoat(Actor a)
		{
			try
			{
				return a != null && a.asset != null && a.asset.is_boat;
			}
			catch
			{
				return false;
			}
		}

		public static void Begin(Actor boat)
		{
			Boat = boat;
			Heading = (Creatures.BoatHeading(boat, out var heading) ? heading : (boat.flip ? 0f : MathF.PI));
			Sails = 0f;
			Wheel = 0f;
			Way = 0f;
			Bump = 0f;
			_stuck = 0f;
			_lastPos = boat.current_position;
			Log.Info("at the helm of " + WorldBoxMod.Describe(boat));
		}

		public static void End()
		{
			if (Boat != null)
			{
				Log.Info("left the helm");
			}
			Boat = null;
			Sails = (Wheel = (Way = 0f));
		}

		public static void Update(float dt, bool keysFree)
		{
			if (Boat != null && !(dt <= 0f))
			{
				dt = Mathf.Min(dt, 0.1f);
				Vector2 vector = (keysFree ? (PossessionHooks.MoveKeys() + PossessionHooks.ScriptedInput) : Vector2.zero);
				bool flag = keysFree && (PlayInput.GetKey(KeyCode.LeftShift) || PlayInput.GetKey(KeyCode.RightShift) || PossessionHooks.ScriptedRun);
				if (vector.y > 0.1f)
				{
					Sails = Mathf.Min(1f, Sails + 0.55f * dt);
				}
				else if (vector.y < -0.1f)
				{
					Sails = Mathf.Max(-0.35f, Sails - 0.7f * dt);
				}
				float num = Mathf.Clamp(vector.x, -1f, 1f);
				Wheel = Mathf.MoveTowards(Wheel, num, ((num == 0f) ? 0.9f : 1.6f) * dt);
				float num2 = Sails * ((flag && Sails > 0.5f) ? 1.35f : 1f);
				Way = Mathf.MoveTowards(Way, num2, ((Mathf.Abs(num2) > Mathf.Abs(Way)) ? 0.32f : 0.45f) * dt);
				if (Mathf.Abs(Way) < 0.004f && num2 == 0f)
				{
					Way = 0f;
				}
				float num3 = 0.22f + 0.78f * Mathf.Clamp01(Mathf.Abs(Way));
				Heading -= Wheel * 0.75f * num3 * Mathf.Sign((Way == 0f) ? 1f : Way) * dt;
				if (Heading > MathF.PI)
				{
					Heading -= MathF.PI * 2f;
				}
				else if (Heading < -MathF.PI)
				{
					Heading += MathF.PI * 2f;
				}
				Vector2 current_position = Boat.current_position;
				float magnitude = (current_position - _lastPos).magnitude;
				_lastPos = current_position;
				if (Mathf.Abs(Way) > 0.2f && magnitude < 0.002f)
				{
					_stuck += dt;
				}
				else
				{
					_stuck = 0f;
				}
				if (_stuck > 0.5f)
				{
					Way *= 0.5f;
					_stuck = 0f;
				}
				Bump = Mathf.Max(0f, Bump - dt * 3f);
			}
		}

		public static Vector2 Course(Vector2 from)
		{
			if (Boat == null || Way == 0f)
			{
				return Vector2.zero;
			}
			Vector2 vector = new Vector2(Mathf.Cos(Heading), Mathf.Sin(Heading)) * Mathf.Sign(Way);
			float num = HalfLength() * 0.92f;
			if (!Clear(from + vector * num) || !Clear(from + vector * (num * 0.5f)))
			{
				if (Mathf.Abs(Way) > 0.25f)
				{
					Bump = Mathf.Max(Bump, Mathf.Abs(Way));
				}
				Way = 0f;
				Sails = Mathf.Min(Sails, 0.25f);
				return Vector2.zero;
			}
			return vector * Mathf.Abs(Way);
		}

		public static float HalfLength()
		{
			MeshModel meshModel = Models.Get(Models.BoatFamily(Boat));
			Creatures.BoatPose(Boat, 0f, 0f, 0f, out var _, out var _, out var scale);
			if (meshModel == null)
			{
				return 1.5f;
			}
			return Mathf.Max(0.4f, (meshModel.Max.Y - meshModel.Min.Y) * 0.5f * scale);
		}

		private static bool Clear(Vector2 p)
		{
			try
			{
				WorldTile tileAt = Toolbox.getTileAt(p.x, p.y);
				if (tileAt == null || tileAt.Type == null || !tileAt.Type.liquid)
				{
					return false;
				}
			}
			catch
			{
				return false;
			}
			if (DockDecks.On(p))
			{
				return false;
			}
			for (int i = 0; i < Colliders.Near.Count; i++)
			{
				Collider c = Colliders.Near[i];
				if (!c.Mobile && Colliders.Distance(c, p, out var _) < 0.12f)
				{
					return false;
				}
			}
			return true;
		}
	}
}
