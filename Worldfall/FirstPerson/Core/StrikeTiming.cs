using System;

namespace FirstPerson.Core
{
	public struct StrikeTiming
	{
		public float Windup;

		public float Hold;

		public float Strike;

		public float Total;

		public float Weight;

		public const float Contact = 0.6f;

		public float StrikeStart => Windup + Hold;

		public float Hit => Windup + Hold + Strike * 0.6f;

		public float StrikeEnd => Windup + Hold + Strike;

		private static StrikeTiming Of(float windup, float hold, float strike, float total, float weight)
		{
			return new StrikeTiming
			{
				Windup = windup,
				Hold = hold,
				Strike = strike,
				Total = total,
				Weight = weight
			};
		}

		public StrikeTiming WithWindup(float scale)
		{
			if (scale <= 0f || Math.Abs(scale - 1f) < 0.001f)
			{
				return this;
			}
			StrikeTiming result = this;
			float num = Windup * (1f - scale);
			result.Windup = Windup * scale;
			result.Total = Math.Max(result.StrikeEnd, Total - num);
			return result;
		}

		public static StrikeTiming For(WeaponKind kind, bool melee, bool hands)
		{
			if (!hands)
			{
				return Of(0f, 0f, 0.1f, 0.75f, 0.3f);
			}
			switch (kind)
			{
			case WeaponKind.Gun:
				return Of(0f, 0f, 0f, 0.55f, 0f);
			case WeaponKind.Shotgun:
				return Of(0f, 0f, 0f, 0.82f, 0f);
			case WeaponKind.Staff:
				return Of(0.3f, 0.05f, 0.16f, 1.2f, 0.35f);
			case WeaponKind.Dagger:
				return Of(0.13f, 0f, 0.12f, 0.6f, 0.2f);
			case WeaponKind.Stick:
				return Of(0.19f, 0.02f, 0.15f, 0.78f, 0.4f);
			case WeaponKind.Sword:
				return Of(0.23f, 0.03f, 0.16f, 0.92f, 0.55f);
			case WeaponKind.Spear:
				return Of(0.25f, 0.04f, 0.15f, 0.98f, 0.5f);
			case WeaponKind.Axe:
				return Of(0.3f, 0.05f, 0.17f, 1.12f, 0.8f);
			case WeaponKind.Hammer:
				return Of(0.36f, 0.07f, 0.19f, 1.38f, 1f);
			case WeaponKind.Bow:
				return Of(0f, 0f, 0.05f, 1f, 0f);
			default:
				if (!melee)
				{
					return Of(0.17f, 0.02f, 0.12f, 0.88f, 0.2f);
				}
				return Of(0.12f, 0f, 0.12f, 0.56f, 0.25f);
			}
		}
	}
}
