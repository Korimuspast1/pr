using System;

namespace FirstPerson.Core
{
	public sealed class WeaponAura
	{
		private string _weapon;

		private Vec3 _head;

		private Vec3 _eye;

		private float _clock;

		private int _serial;

		public void Reset()
		{
			_weapon = null;
			_clock = 0f;
		}

		public int Emit(FxParticles pool, string weapon, Vec3 head, Vec3 eye, uint colour, bool heat, float dt)
		{
			if (pool == null || weapon == null || colour == 0 || dt <= 0f || dt > 0.15f)
			{
				Reset();
				return 0;
			}
			Vec3 vec = head - _head;
			float num = vec.X * vec.X + vec.Y * vec.Y + vec.Z * vec.Z;
			if (_weapon != weapon || num > 4f)
			{
				_weapon = weapon;
				_head = head;
				_eye = eye;
				_clock = 0f;
				return 0;
			}
			Vec3 vec2 = vec - (eye - _eye);
			float num2 = (((float)Math.Sqrt(vec2.X * vec2.X + vec2.Y * vec2.Y + vec2.Z * vec2.Z) / dt > 0.8f) ? 18f : 5f);
			_clock += dt * num2;
			int num3 = Math.Min(2, (int)_clock);
			_clock -= num3;
			_clock = Math.Min(_clock, 1f);
			int count = pool.List.Count;
			for (int i = 0; i < num3; i++)
			{
				int num4 = _serial++;
				float num5 = (BlastFx.Rand(73u, num4 * 3) - 0.5f) * 0.035f;
				float num6 = (BlastFx.Rand(73u, num4 * 3 + 1) - 0.5f) * 0.035f;
				float z = BlastFx.Rand(73u, num4 * 3 + 2) * 0.035f;
				Vec3 pos = _head + vec * (((float)i + 1f) / ((float)num3 + 1f)) + new Vec3(num5, num6, z);
				pool.Add(new FxParticles.P
				{
					Pos = pos,
					Vel = new Vec3(num5 * 1.5f, num6 * 1.5f, heat ? 0.13f : 0.065f),
					Life = (heat ? 0.34f : 0.24f),
					Size = (heat ? 0.009f : 0.007f),
					EndSize = 0.001f,
					Color = colour,
					EndColor = colour,
					Alpha = 180,
					Kind = (heat ? FxParticles.Kind.Ember : FxParticles.Kind.Glow),
					Gravity = (heat ? (-0.08f) : 0f),
					Drag = 1.5f
				});
			}
			_head = head;
			_eye = eye;
			return pool.List.Count - count;
		}
	}
}
