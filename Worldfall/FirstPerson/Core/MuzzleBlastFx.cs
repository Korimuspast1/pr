using System;

namespace FirstPerson.Core
{
	public sealed class MuzzleBlastFx
	{
		public Vec3 Muzzle;

		public Vec3 Dir;

		public readonly float Scale;

		public readonly uint Seed;

		public readonly float Water;

		public readonly bool Fiery;

		public float Detail = 1f;

		public float Age;

		private readonly SpeedLines _lines = new SpeedLines();

		private Vec3 _ringFrom;

		private Vec3 _ringDir;

		private Vec3 _splash;

		private bool _fired;

		public bool Finished
		{
			get
			{
				if (Age > 2.6f)
				{
					return _lines.Count == 0;
				}
				return false;
			}
		}

		public MuzzleBlastFx(Vec3 muzzle, Vec3 dir, float scale, uint seed, float water, bool fiery = false)
		{
			Muzzle = muzzle;
			Dir = dir.Normalized();
			Scale = scale;
			Seed = seed;
			Water = water;
			Fiery = fiery;
		}

		private float R(int i)
		{
			return CannonFx.R(Seed, i);
		}

		private int Count(int full)
		{
			return Math.Max(1, (int)((float)full * Detail + 0.5f));
		}

		public void Step(float dt, FxSink sink, FxParticles parts)
		{
			Age += dt;
			float age = Age;
			float scale = Scale;
			float num = sink.Distance(Muzzle.X, Muzzle.Y);
			if (!_fired)
			{
				_fired = true;
				Fire(sink, parts, num);
			}
			Vec3 dir = Dir;
			Vec3 vec = new Vec3(0f, 0f, 1f);
			if (age < 0.11f)
			{
				float num2 = 1f - age / 0.11f;
				Vec3 vec2 = Muzzle + dir * (0.45f * scale);
				sink.Glow(vec2.X, vec2.Y, vec2.Z, scale * (2.4f + 10f * age), TerrainCache.Rgb(255, 252, 238), num2);
				float num3 = scale * 3.1f * (0.75f + 0.45f * num2);
				sink.Sprite(BlastFx.StarSprite, vec2.X, vec2.Y, vec2.Z, num3, num3, TerrainCache.Rgb(255, 234, 186), (byte)Math.Max(2, (int)(240f * num2)), emissive: true, additive: true);
				float num4 = 0.55f + 0.45f * BlastFx.Smooth(age / 0.04f);
				for (int i = 0; i < 7; i++)
				{
					float num5 = (float)i / 6f;
					Vec3 vec3 = Muzzle + dir * (scale * (0.2f + (Fiery ? 3.4f : 2.7f) * num5) * num4);
					float size = scale * (1.3f - 0.8f * num5) * (0.7f + 0.3f * num2);
					sink.Glow(vec3.X, vec3.Y, vec3.Z, size, BlastFx.Heat(1.2f - 0.6f * num5), num2 * (1f - 0.45f * num5));
				}
				float num6 = Math.Max(BlastFx.Clamp01(0.25f + sink.Facing(vec2.X, vec2.Y)), BlastFx.Clamp01(1f - num / 5f));
				sink.AddFlash(0.17f * num2 * BlastFx.Clamp01(1f - num / 30f) * num6, TerrainCache.Rgb(255, 236, 204));
			}
			if (age < 0.2f)
			{
				float num7 = age / 0.2f;
				Vec3 vec4 = Muzzle + dir * (scale * (0.5f + 1.6f * num7));
				float num8 = scale * (0.6f + 5.4f * (1f - (1f - num7) * (1f - num7)));
				sink.Sprite(BlastFx.Ring, vec4.X, vec4.Y, vec4.Z, num8, num8, TerrainCache.Rgb(255, 246, 226), (byte)Math.Max(2, (int)(170f * (1f - num7) * (1f - num7))), emissive: true, additive: true);
			}
			if (!Fiery)
			{
				SmokeRing(sink, age);
			}
			if (!float.IsNaN(Water) && age < 1.7f)
			{
				float num9 = age / 1.7f;
				float radius = scale * (0.35f + 2.6f * (1f - (float)Math.Exp(-2.6f * age)));
				CannonFx.FoamRing(sink, _splash, radius, scale * (0.7f + 0.4f * num9), 0.75f * (1f - num9) * BlastFx.Smooth(age / 0.06f), Seed, (Detail > 0.6f) ? 14 : 8, CannonFx.Foam);
			}
			float strength = 2.6f * (float)Math.Exp((0f - age) / 0.05f) + 0.55f * (float)Math.Exp((0f - age) / 0.35f);
			Vec3 vec5 = Muzzle + dir * (0.9f * scale) + vec * (0.3f * scale);
			sink.Light(vec5.X, vec5.Y, vec5.Z, 11f * scale, BlastFx.Heat(1f), strength, (age < 0.08f) ? (1.4f * scale) : 0f);
			_lines.Step(dt, 0f);
			_lines.Draw(sink);
		}

		private void SmokeRing(FxSink sink, float t)
		{
			if (t < 0.03f || t > 2.4f || Detail < 0.45f)
			{
				return;
			}
			float scale = Scale;
			Vec3 ringDir = _ringDir;
			Vec3 vec = Vec3.Cross(ringDir, new Vec3(0f, 0f, 1f));
			vec = ((Vec3.Dot(vec, vec) > 1E-06f) ? vec.Normalized() : new Vec3(1f, 0f, 0f));
			Vec3 vec2 = Vec3.Cross(vec, ringDir).Normalized();
			float num = scale * (0.8f + 4.6f * (1f - (float)Math.Exp(-2.4f * t)));
			Vec3 vec3 = _ringFrom + ringDir * num + new Vec3(0f, 0f, scale * 0.2f * t);
			float num2 = scale * (0.3f + 0.85f * (1f - (float)Math.Exp(-1.7f * t)));
			float num3 = (1f - BlastFx.Smooth((t - 0.8f) / 1.6f)) * BlastFx.Smooth(t / 0.1f);
			Vec3 a = new Vec3(vec3.X - sink.EyeX, vec3.Y - sink.EyeY, vec3.Z - sink.EyeZ).Normalized();
			num3 *= BlastFx.Smooth((Math.Abs(Vec3.Dot(a, ringDir)) - 0.3f) / 0.35f);
			if (num3 < 0.02f)
			{
				return;
			}
			float num4 = scale * (0.34f + 0.34f * (1f - (float)Math.Exp(-1.4f * t)));
			float num5 = 1f - BlastFx.Smooth(t / 0.22f);
			for (int i = 0; i < 12; i++)
			{
				float num6 = (float)i / 12f * 6.2832f + t * 0.7f;
				float num7 = 1f + 0.09f * (float)Math.Sin(t * 5f + (float)i * 1.7f);
				float num8 = (float)Math.Cos(num6);
				float num9 = (float)Math.Sin(num6);
				Vec3 vec4 = vec3 + (vec * num8 + vec2 * num9) * (num2 * num7);
				uint num10 = TerrainCache.Lerp(TerrainCache.Rgb(196, 194, 192), CannonFx.GunSmoke, 0.5f + 0.5f * num9);
				if (num5 > 0.01f)
				{
					num10 = TerrainCache.Lerp(num10, BlastFx.Heat(0.95f), num5 * 0.8f);
				}
				sink.Sprite(BlastFx.Soft(i + (int)(Seed & 3)), vec4.X, vec4.Y, vec4.Z, num4, num4, num10, (byte)Math.Max(2, (int)(205f * num3)), emissive: false, additive: false, (i & 1) != 0);
			}
		}

		private void Fire(FxSink sink, FxParticles parts, float dist)
		{
			float scale = Scale;
			Vec3 dir = Dir;
			Vec3 vec = new Vec3(0f, 0f, 1f);
			sink.Shake = Math.Max(sink.Shake, 0.42f * BlastFx.Clamp01(1f - dist / 24f));
			_ringFrom = Muzzle;
			_ringDir = dir;
			Vec3 vec2 = new Vec3(dir.X, dir.Y, 0f);
			vec2 = ((Vec3.Dot(vec2, vec2) > 1E-06f) ? vec2.Normalized() : new Vec3(1f, 0f, 0f));
			if (!float.IsNaN(Water))
			{
				_splash = new Vec3(Muzzle.X + vec2.X * 1.1f * scale, Muzzle.Y + vec2.Y * 1.1f * scale, Water);
			}
			for (int i = 0; i < 6; i++)
			{
				_lines.Add(Muzzle + dir * (0.3f * scale), CannonFx.Cone(dir, 0.16f, R(i + 300), R(i + 301)) * (scale * (20f + 12f * R(i + 302))), scale * (1.6f + 0.8f * R(i + 303)), 0.11f * scale, 0.2f + 0.1f * R(i + 304), TerrainCache.Rgb(255, 244, 214));
			}
			if (parts == null)
			{
				return;
			}
			int j = 0;
			for (int num = Count(Fiery ? 18 : 12); j < num; j++)
			{
				parts.Add(new FxParticles.P
				{
					Pos = Muzzle + dir * (0.15f * scale),
					Vel = CannonFx.Cone(dir, 0.26f, R(j), R(j + 1)) * (scale * (6f + 8f * R(j + 2))) + vec * (0.6f * scale),
					Life = 0.16f + 0.14f * R(j + 3) + (Fiery ? 0.15f : 0f),
					Size = scale * (0.5f + 0.25f * R(j + 4)),
					EndSize = scale * (1.2f + 0.5f * R(j + 5)),
					Drag = 7f,
					Gravity = -1.5f,
					Color = BlastFx.Heat(1.15f),
					EndColor = BlastFx.Heat(0.62f),
					Kind = FxParticles.Kind.Fire,
					Variant = j
				});
			}
			int k = 0;
			for (int num2 = Count(Fiery ? 6 : 14); k < num2; k++)
			{
				float num3 = R(k + 40);
				Vec3 at = Muzzle + dir * (scale * (0.2f + 1.8f * num3)) + vec * (scale * 0.15f * (R(k + 41) - 0.5f));
				Vec3 vel = CannonFx.Cone(dir, 0.5f, R(k + 42), R(k + 43)) * (scale * (2.2f + 7f * (1f - num3))) + vec * (scale * 0.35f);
				float num4 = scale * (0.8f + 0.45f * R(k + 44));
				parts.Smoke(at, vel, 3f + 2f * R(k + 45), num4, num4 * (3.7f + 1.3f * R(k + 46)), CannonFx.GunSmoke, CannonFx.GunSmokeOld, 236, k, 2.4f);
			}
			int l = 0;
			for (int num5 = Count(5); l < num5; l++)
			{
				parts.Smoke(Muzzle + dir * (scale * (0.4f + 0.8f * R(l + 60))), CannonFx.Cone(dir, 0.3f, R(l + 61), R(l + 62)) * (scale * (3f + 3f * R(l + 63))), 0.5f + 0.25f * R(l + 64), scale * (0.7f + 0.3f * R(l + 65)), scale * (1.6f + 0.5f * R(l + 66)), TerrainCache.Rgb(255, 204, 136), CannonFx.GunSmoke, 225, l + 3, 4f);
			}
			int m = 0;
			for (int num6 = Count(20); m < num6; m++)
			{
				parts.Spark(Muzzle + dir * (0.2f * scale), CannonFx.Cone(dir, 0.36f, R(m + 80), R(m + 81)) * (scale * (8f + 10f * R(m + 82))) + vec * (scale * (1f + 2f * R(m + 83))), 0.35f + 0.5f * R(m + 84), 0.07f + 0.06f * R(m + 85), BlastFx.Heat(1.15f), BlastFx.Heat(0.45f), 9.8f, 0.7f);
			}
			int n = 0;
			for (int num7 = Count(4); n < num7; n++)
			{
				parts.Add(new FxParticles.P
				{
					Pos = Muzzle + dir * (0.3f * scale),
					Vel = CannonFx.Cone(dir, 0.3f, R(n + 100), R(n + 101)) * (scale * (5f + 4f * R(n + 102))) + vec * (scale * (2f + 2f * R(n + 103))),
					Life = 1.3f + 0.6f * R(n + 104),
					Size = 0.13f * scale,
					EndSize = 0.08f * scale,
					Gravity = 6f,
					Drag = 0.6f,
					Color = BlastFx.Heat(1.05f),
					EndColor = BlastFx.Heat(0.35f),
					Kind = FxParticles.Kind.Ember
				});
			}
			if (!float.IsNaN(Water) && Muzzle.Z - Water < 2.4f * scale)
			{
				Vec3 vec3 = Vec3.Cross(vec2, vec);
				int num8 = 0;
				for (int num9 = Count(18); num8 < num9; num8++)
				{
					float num10 = scale * (2.5f + 3.5f * R(num8 + 120));
					Vec3 vel2 = vec2 * (scale * (1.5f + 5f * R(num8 + 121))) + vec3 * (scale * (R(num8 + 122) - 0.5f) * 4f) + vec * num10;
					float num11 = scale * (0.08f + 0.05f * R(num8 + 123));
					parts.Add(new FxParticles.P
					{
						Pos = _splash + vec3 * (scale * (R(num8 + 124) - 0.5f) * 1.2f) + vec * 0.05f,
						Vel = vel2,
						Life = Math.Min(1.8f, 2f * num10 / 9.8f + 0.04f),
						Size = num11,
						EndSize = num11 * 0.8f,
						Gravity = 9.8f,
						Drag = 0.3f,
						Color = CannonFx.Foam,
						EndColor = CannonFx.FoamShade,
						Kind = FxParticles.Kind.Drop,
						Matte = true
					});
				}
				int num12 = 0;
				for (int num13 = Count(4); num12 < num13; num12++)
				{
					parts.Smoke(_splash + vec * (0.25f * scale), vec2 * (scale * (1f + 1.5f * R(num12 + 140))) + vec * (0.3f * scale), 1.6f + 0.6f * R(num12 + 141), 0.6f * scale, scale * (2f + 0.6f * R(num12 + 142)), TerrainCache.Rgb(244, 247, 250), TerrainCache.Rgb(226, 232, 238), 150, num12 + 20, 1.6f);
				}
			}
		}
	}
}
