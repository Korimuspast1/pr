using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class Billows
	{
		public struct B
		{
			public Vec3 Pos;

			public Vec3 Vel;

			public float Age;

			public float Life;

			public float Size;

			public float EndSize;

			public float Heat;

			public float Rise;

			public int Variant;
		}

		public readonly List<B> List = new List<B>(128);

		public int Max = 260;

		public void Add(Vec3 pos, Vec3 vel, float size, float endSize, float life, float heat, int variant, float rise = 0.6f)
		{
			if (List.Count < Max)
			{
				List.Add(new B
				{
					Pos = pos,
					Vel = vel,
					Size = size,
					EndSize = endSize,
					Life = life,
					Heat = heat,
					Variant = variant,
					Rise = rise
				});
			}
		}

		public void Step(float dt)
		{
			if (dt <= 0f)
			{
				return;
			}
			for (int num = List.Count - 1; num >= 0; num--)
			{
				B value = List[num];
				value.Age += dt;
				if (value.Age >= value.Life)
				{
					List.RemoveAt(num);
				}
				else
				{
					float num2 = (float)Math.Exp(-2.2f * dt);
					value.Vel = new Vec3(value.Vel.X * num2, value.Vel.Y * num2, value.Vel.Z * num2 + value.Rise * dt);
					value.Pos += value.Vel * dt;
					List[num] = value;
				}
			}
		}

		public void Draw(FxSink sink, float fade = 1f)
		{
			for (int i = 0; i < List.Count; i++)
			{
				B b = List[i];
				float num = b.Age / b.Life;
				float num2 = b.Size + (b.EndSize - b.Size) * (1f - (1f - num) * (1f - num));
				float num3 = (1f - BlastFx.Smooth((num - 0.55f) / 0.45f)) * fade;
				float num4 = Math.Min(1f, b.Age / 0.12f);
				num3 *= num4;
				if (!(num3 < 0.02f))
				{
					bool flip = (b.Variant & 1) != 0;
					byte alpha = (byte)((num3 > 0.97f) ? 250 : ((byte)Math.Max(2, (int)(num3 * 250f))));
					sink.Sprite(PowerFx.DarkPuff(b.Variant), b.Pos.X, b.Pos.Y, b.Pos.Z, num2, num2, 0u, alpha, emissive: false, additive: false, flip);
					float num5 = b.Heat * (1f - BlastFx.Smooth(num / 0.85f));
					if (num5 > 0.03f)
					{
						sink.Sprite(PowerFx.FireRim(b.Variant), b.Pos.X, b.Pos.Y, b.Pos.Z, num2, num2, BlastFx.Heat(0.5f + 0.32f * num5), (byte)Math.Max(2, (int)(Math.Min(1f, num5 * 1.4f) * num3 * 230f)), emissive: true, additive: true, flip);
					}
				}
			}
		}
	}
}
