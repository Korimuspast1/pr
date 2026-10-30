using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class FxParticles
	{
		public enum Kind : byte
		{
			Spark,
			Ember,
			Glow,
			Smoke,
			Fire,
			Steam,
			Drop,
			Dust,
			Trail
		}

		public struct P
		{
			public Vec3 Pos;

			public Vec3 Vel;

			public float Age;

			public float Life;

			public float Size;

			public float EndSize;

			public float Gravity;

			public float Drag;

			public uint Color;

			public uint EndColor;

			public Kind Kind;

			public byte Alpha;

			public int Variant;

			public bool Bounce;

			public bool Trail;

			public bool Matte;

			public float TrailClock;
		}

		public readonly List<P> List = new List<P>(512);

		public int Max = 2400;

		public float WindX = 0.6f;

		public float WindY = 0.25f;

		public void Add(in P p)
		{
			if (List.Count < Max)
			{
				List.Add(p);
			}
		}

		public void Spark(Vec3 at, Vec3 vel, float life, float size, uint color, uint endColor, float gravity = 9f, float drag = 0.4f)
		{
			P p = new P
			{
				Pos = at,
				Vel = vel,
				Life = life,
				Size = size,
				EndSize = size * 0.6f,
				Color = color,
				EndColor = endColor,
				Gravity = gravity,
				Drag = drag,
				Kind = Kind.Spark,
				Bounce = true
			};
			Add(in p);
		}

		public void Smoke(Vec3 at, Vec3 vel, float life, float size, float endSize, uint color, uint endColor, byte alpha, int variant, float drag = 0.6f)
		{
			P p = new P
			{
				Pos = at,
				Vel = vel,
				Life = life,
				Size = size,
				EndSize = endSize,
				Color = color,
				EndColor = endColor,
				Alpha = alpha,
				Variant = variant,
				Drag = drag,
				Gravity = -0.15f,
				Kind = Kind.Smoke
			};
			Add(in p);
		}

		public void Update(float dt, Func<float, float, float> ground)
		{
			if (dt <= 0f)
			{
				return;
			}
			for (int num = List.Count - 1; num >= 0; num--)
			{
				P value = List[num];
				value.Age += dt;
				if (value.Age >= value.Life)
				{
					List.RemoveAt(num);
					continue;
				}
				float num2 = (float)Math.Exp((0f - value.Drag) * dt);
				value.Vel = new Vec3(value.Vel.X * num2 + WindX * (1f - num2) * ((value.Kind == Kind.Smoke || value.Kind == Kind.Steam) ? 1f : 0.3f), value.Vel.Y * num2 + WindY * (1f - num2) * ((value.Kind == Kind.Smoke || value.Kind == Kind.Steam) ? 1f : 0.3f), value.Vel.Z * num2 - value.Gravity * dt);
				value.Pos += value.Vel * dt;
				if (value.Trail && List.Count < Max)
				{
					value.TrailClock += dt;
					if (value.TrailClock > 0.03f)
					{
						value.TrailClock = 0f;
						float num3 = Math.Max(0.3f, value.Size * 2.2f);
						List.Add(new P
						{
							Pos = value.Pos,
							Vel = new Vec3(0f, 0f, 0.5f),
							Life = 1.3f,
							Size = num3,
							EndSize = num3 * 3.5f,
							Color = TerrainCache.Rgb(96, 88, 86),
							EndColor = TerrainCache.Rgb(150, 144, 140),
							Alpha = 120,
							Variant = (num & 7),
							Drag = 1.2f,
							Gravity = -0.2f,
							Kind = Kind.Smoke
						});
					}
				}
				if (ground != null && (value.Kind == Kind.Spark || value.Kind == Kind.Ember || value.Kind == Kind.Drop || value.Kind == Kind.Dust))
				{
					float num4 = ground(value.Pos.X, value.Pos.Y);
					if (value.Pos.Z < num4)
					{
						if (value.Bounce && value.Vel.Z < -1.5f)
						{
							value.Pos = new Vec3(value.Pos.X, value.Pos.Y, num4 + 0.01f);
							value.Vel = new Vec3(value.Vel.X * 0.4f, value.Vel.Y * 0.4f, (0f - value.Vel.Z) * 0.3f);
							value.Bounce = false;
						}
						else
						{
							if (value.Kind != Kind.Dust)
							{
								List.RemoveAt(num);
								continue;
							}
							value.Pos = new Vec3(value.Pos.X, value.Pos.Y, num4);
						}
					}
				}
				List[num] = value;
			}
		}

		public void Draw(FxSink sink)
		{
			for (int i = 0; i < List.Count; i++)
			{
				P p = List[i];
				float num = p.Age / p.Life;
				float num2 = p.Size + (p.EndSize - p.Size) * num;
				uint num3 = TerrainCache.Lerp(p.Color, p.EndColor, num);
				switch (p.Kind)
				{
				case Kind.Spark:
				case Kind.Drop:
				{
					float num8 = p.Pos.X - sink.EyeX;
					float num9 = p.Pos.Y - sink.EyeY;
					float num10 = p.Pos.Z - sink.EyeZ;
					float num11 = num8 * num8 + num9 * num9 + num10 * num10;
					if (!(num11 < 0.25f))
					{
						num2 = Math.Min(num2, (float)Math.Sqrt(num11) * 0.016f);
						int size = ((num2 > 0.16f) ? 3 : ((!(num2 > 0.08f)) ? 1 : 2));
						float num12 = 1f - BlastFx.Smooth((num - 0.7f) / 0.3f);
						if (p.Kind == Kind.Drop)
						{
							sink.Sprite(BlastFx.Spark(size), p.Pos.X, p.Pos.Y, p.Pos.Z, num2, num2, num3, 0, !p.Matte);
						}
						else
						{
							sink.Sprite(BlastFx.Spark(size), p.Pos.X, p.Pos.Y, p.Pos.Z, num2, num2, num3, (byte)Math.Max(2, (int)(255f * num12)), emissive: true, additive: true);
						}
					}
					break;
				}
				case Kind.Ember:
				{
					float num7 = 1f - BlastFx.Smooth((num - 0.6f) / 0.4f);
					sink.Sprite(BlastFx.Streak, p.Pos.X, p.Pos.Y, p.Pos.Z, num2 * 0.3f, num2 * 1.5f, num3, (byte)Math.Max(2, (int)(255f * num7)), emissive: true, additive: true);
					break;
				}
				case Kind.Glow:
					sink.Glow(p.Pos.X, p.Pos.Y, p.Pos.Z, num2, num3, (1f - num) * ((p.Alpha == 0) ? 1f : ((float)(int)p.Alpha / 255f)));
					break;
				case Kind.Trail:
				{
					float num13 = ((p.Alpha == 0) ? 0.8f : ((float)(int)p.Alpha / 255f)) * (float)Math.Pow(1f - num, 1.149999976158142);
					uint num14 = BlastFx.StreakColor(0.1f + 0.9f * (float)Math.Pow(num, 0.550000011920929));
					if (num13 > 0.01f)
					{
						sink.Sprite(BlastFx.GlowSprite, p.Pos.X, p.Pos.Y, p.Pos.Z, num2, num2, num14, (byte)Math.Max(2, (int)(num13 * 255f)), emissive: true);
					}
					if (num < 0.3f)
					{
						sink.Glow(p.Pos.X, p.Pos.Y, p.Pos.Z, num2 * 0.5f, TerrainCache.Lerp(TerrainCache.Rgb(255, 250, 220), num14, num / 0.3f), (1f - num / 0.3f) * 0.7f);
					}
					break;
				}
				case Kind.Fire:
				{
					byte alpha = (byte)((!(num < 0.5f)) ? ((byte)Math.Max(2, (int)(255f * (1f - (num - 0.5f) / 0.5f)))) : 0);
					sink.Sprite(BlastFx.Puff(p.Variant), p.Pos.X, p.Pos.Y, p.Pos.Z, num2, num2, num3, alpha, emissive: true, additive: false, (p.Variant & 1) != 0);
					break;
				}
				case Kind.Smoke:
				case Kind.Steam:
				case Kind.Dust:
				{
					float num4 = ((p.Alpha == 0) ? 200f : ((float)(int)p.Alpha));
					float num5 = Math.Min(1f, p.Age / Math.Max(0.05f, p.Life * 0.08f));
					int num6 = (int)(num4 * num5 * (1f - BlastFx.Smooth((num - 0.45f) / 0.55f)));
					if (num6 >= 2)
					{
						sink.Sprite(BlastFx.Soft(p.Variant), p.Pos.X, p.Pos.Y, p.Pos.Z, num2, num2, num3, (byte)Math.Min(255, num6), emissive: false, additive: false, (p.Variant & 1) != 0);
					}
					break;
				}
				}
			}
		}
	}
}
