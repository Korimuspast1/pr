using System;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class BeastView
	{
		public static Actor Inside;

		private static Actor _for;

		public static bool Active { get; private set; }

		public static float BodyYaw { get; private set; }

		public static float Widen { get; private set; }

		public static Mat3 HeadTurn { get; private set; } = Mat3.Identity;

		public static bool IsBeast(Actor a)
		{
			try
			{
				return a != null && a.asset != null && !a.asset.is_boat && !People.IsHumanoid(a);
			}
			catch
			{
				return false;
			}
		}

		public static bool ShowsBody(Actor host)
		{
			if (!Models.Ready || !IsBeast(host))
			{
				return false;
			}
			string text = Models.CreatureFamily(host);
			if (text != null)
			{
				return Models.Detailed(text, 0f) != null;
			}
			return false;
		}

		public static ulong HiddenParts(MeshModel m)
		{
			return BeastEye.HiddenParts(m);
		}

		public static bool Update(Actor host, float viewYaw, float pitchDegrees, float body, bool swimming, bool moving, float sinceAttack, bool melee, float dt, out Vector2 offset, out float up)
		{
			offset = Vector2.zero;
			up = body * 0.9f;
			Widen = 0f;
			Active = IsBeast(host);
			if (!Active)
			{
				_for = null;
				return false;
			}
			if (host != _for)
			{
				_for = host;
				BodyYaw = viewYaw;
			}
			float num = Mathf.DeltaAngle(BodyYaw * 57.29578f, viewYaw * 57.29578f);
			if (num > 60f)
			{
				BodyYaw = viewYaw - MathF.PI / 3f;
			}
			else if (num < -60f)
			{
				BodyYaw = viewYaw + MathF.PI / 3f;
			}
			BodyYaw = Mathf.LerpAngle(BodyYaw * 57.29578f, viewYaw * 57.29578f, 1f - Mathf.Exp((0f - (moving ? 7f : 2.5f)) * dt)) * (MathF.PI / 180f);
			float x = Mathf.Cos(viewYaw);
			float y = Mathf.Sin(viewYaw);
			HeadTurn = BeastEye.HeadTurn(BodyYaw, viewYaw, pitchDegrees);
			float num2;
			if (Creatures.DrawnAs(host, out var model, out var scale, out var lift, out var tall))
			{
				Vec3 vec = BeastEye.Offset(model, scale, BodyYaw, viewYaw, pitchDegrees);
				offset = new Vector2(vec.X, vec.Y);
				up = vec.Z + lift;
				if (swimming)
				{
					up -= tall * ((Models.CreatureFamily(host) == "animal_fish") ? 0.75f : 0.5f);
				}
				else
				{
					Widen = BeastEye.Widen(pitchDegrees);
				}
				num2 = tall;
			}
			else
			{
				offset = new Vector2(x, y) * (0.3f * body);
				up = body * (swimming ? 0.28f : 0.75f);
				num2 = body;
			}
			if (swimming)
			{
				up = Mathf.Max(up, 0.06f);
			}
			if (melee && sinceAttack >= 0f && sinceAttack < 0.35f)
			{
				float num3 = Mathf.Sin(sinceAttack / 0.35f * MathF.PI);
				offset += new Vector2(x, y) * (0.28f * num2 * num3);
				up -= 0.08f * num2 * num3;
			}
			return true;
		}
	}
}
