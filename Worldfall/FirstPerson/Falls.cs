using System;
using UnityEngine;

namespace FirstPerson
{
	internal static class Falls
	{
		public static float FreeDrop = 2f;

		public static float PerTile = 0.12f;

		public static float Sheer = 3f;

		private static Actor _who;

		private static Vector2 _lastPos;

		private static float _lastGround;

		private static float _fell;

		internal static float LastDrop;

		internal static float LastHurt;

		public static void Update(Actor host, Vector2 pos, float ground, bool spared)
		{
			if (host != _who || spared)
			{
				_who = host;
				_fell = 0f;
				_lastPos = pos;
				_lastGround = ground;
				return;
			}
			float magnitude = (pos - _lastPos).magnitude;
			float num = _lastGround - ground;
			_lastPos = pos;
			_lastGround = ground;
			if (magnitude > 1.5f || Mathf.Abs(num) > 12f)
			{
				_fell = 0f;
			}
			else if (num > 0.02f && num > Sheer * Mathf.Max(magnitude, 0.004f))
			{
				_fell += num;
			}
			else if (!(_fell <= 0f))
			{
				float fell = _fell;
				_fell = 0f;
				if (!(fell <= FreeDrop))
				{
					Hurt(host, fell);
				}
			}
		}

		private static void Hurt(Actor a, float fell)
		{
			float num = Mathf.Min(1f, PerTile * (fell - FreeDrop));
			float num2 = 0f;
			try
			{
				num2 = Mathf.Max(1f, (float)a.getMaxHealth() * num);
				((BaseSimObject)a).getHit(num2, pFlash: true, AttackType.Gravity, (BaseSimObject)null, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: false);
			}
			catch (Exception ex)
			{
				Log.Error("falls: " + ex.Message);
			}
			LastDrop = fell;
			LastHurt = num2;
			Sounds.PlayAt("event:/SFX/HIT/HitStone", a.current_position);
		}
	}
}
