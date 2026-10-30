using System;
using UnityEngine;

namespace FirstPerson
{
	internal static class FallHurt
	{
		public const float Safe = 2f;

		public const float PerTile = 0.15f;

		internal const float Steep = 4f;

		private static float _top = float.NaN;

		private static float _last = float.NaN;

		private static Vector2 _at;

		private static Actor _body;

		private static float _quiet;

		internal static int Falls;

		internal static float LastDrop;

		internal static float LastHurt;

		public static void Reset()
		{
			_top = (_last = float.NaN);
			_quiet = 0.3f;
		}

		public static void Track(Actor host, Vector2 pos, float ground, float dt)
		{
			if (host != _body)
			{
				_body = host;
				Reset();
			}
			if (host == null)
			{
				return;
			}
			if (_quiet > 0f)
			{
				_quiet -= dt;
				_last = ground;
				_at = pos;
				_top = float.NaN;
				return;
			}
			if (float.IsNaN(_last))
			{
				_last = ground;
				_at = pos;
				return;
			}
			float magnitude = (pos - _at).magnitude;
			float num = _last - ground;
			if (magnitude > 3f)
			{
				Reset();
				return;
			}
			if (num > 0.02f && num > magnitude * 4f)
			{
				if (float.IsNaN(_top))
				{
					_top = _last;
				}
			}
			else if (!float.IsNaN(_top))
			{
				float num2 = _top - _last;
				_top = float.NaN;
				if (num2 > 2f)
				{
					Hurt(host, num2);
				}
			}
			_last = ground;
			_at = pos;
		}

		private static void Hurt(Actor host, float fell)
		{
			float num = Mathf.Min(1f, (fell - 2f) * 0.15f);
			float num2 = 0f;
			try
			{
				num2 = Mathf.Max(1f, (float)host.getMaxHealth() * num);
			}
			catch
			{
			}
			if (num2 <= 0f)
			{
				return;
			}
			Falls++;
			LastDrop = fell;
			LastHurt = num2;
			Log.Info($"fall: {fell:0.0} tiles down, {num2:0} health");
			try
			{
				((BaseSimObject)host).getHit(num2, pFlash: true, AttackType.Gravity, (BaseSimObject)null, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: false);
			}
			catch (Exception ex)
			{
				Log.Error("fall: " + ex.Message);
			}
			try
			{
				Sounds.PlayAt("event:/SFX/HIT/HitStone", host.current_position);
			}
			catch
			{
			}
		}
	}
}
