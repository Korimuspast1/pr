using System;

namespace FirstPerson.Core
{
	public static class LightningFx
	{
		public const float Life = 1.5f;

		public const float Leader = 0.09f;

		private static readonly float[] Strokes = new float[3] { 0f, 0.15f, 0.32f };

		private static readonly float[] StrokePower = new float[3] { 1f, 0.75f, 0.55f };

		private static readonly uint Core = TerrainCache.Rgb(255, 255, 255);

		private static readonly uint Hot = TerrainCache.Rgb(214, 232, 255);

		private static readonly uint Blue = TerrainCache.Rgb(96, 168, 255);

		private static readonly uint Deep = TerrainCache.Rgb(70, 120, 255);

		private static readonly uint Violet = TerrainCache.Rgb(150, 120, 255);

		private static readonly uint Scorch = TerrainCache.Rgb(255, 150, 70);

		private const int TrunkNodes = 33;

		private const int BranchNodes = 9;

		private const int MaxBranches = 9;

		private static readonly Vec3[] Trunk = new Vec3[33];

		private static readonly Vec3[] Branch = new Vec3[9];

		private static readonly Vec3[] Tendril = new Vec3[7];

		public static float Stroke(float age)
		{
			float num = age - 0.09f;
			if (num < 0f)
			{
				return 0f;
			}
			float num2 = 0f;
			for (int i = 0; i < Strokes.Length; i++)
			{
				float num3 = num - Strokes[i];
				if (!(num3 < 0f))
				{
					float num4 = StrokePower[i] * (float)Math.Exp((0f - num3) * 16f) + StrokePower[i] * 0.35f * (float)Math.Exp((0f - Math.Abs(num3 - 0.05f)) * 60f);
					if (num4 > num2)
					{
						num2 = num4;
					}
				}
			}
			return Math.Min(1f, num2);
		}

		private static float Afterglow(float age)
		{
			if (!(age < 0.09f))
			{
				return (float)Math.Exp((0f - (age - 0.09f)) * 4.5f);
			}
			return 0f;
		}

		public static float Draw(FxSink sink, FxParticles parts, float x, float y, float ground, float top, float size, uint seed, float age, bool strike)
		{
			if (age < 0f || age > 1.5f)
			{
				return 0f;
			}
			float num = sink.Distance(x, y);
			float num2 = num * 0.013f;
			float num3 = Stroke(age);
			float num4 = Afterglow(age);
			BuildTrunk(x, y, ground, top, size, seed);
			if (age < 0.09f)
			{
				float num5 = age / 0.09f;
				int num6 = Math.Max(1, (int)(num5 * 32f));
				float num7 = 0.35f + 0.25f * BlastFx.Rand(seed, (int)(age * 300f));
				for (int i = 0; i < num6; i++)
				{
					sink.Line(Trunk[i], Trunk[i + 1], Hot, num7, num7, 0.05f);
				}
				Vec3 vec = Trunk[num6];
				sink.Glow(vec.X, vec.Y, vec.Z, Math.Max(1.2f * size, num2 * 2f), Blue, 0.6f);
				DrawBranches(sink, seed, size, num2, num7 * 0.7f, 0f, num6);
				CloudGlow(sink, seed, size, num2, 0.25f, age);
				return 0.1f;
			}
			float num8 = Math.Max(num3, num4 * 0.35f);
			if (num8 > 0.01f)
			{
				float num9 = Math.Min(1f, num3 * 1.2f + num4 * 0.4f);
				for (int j = 0; j < 32; j++)
				{
					Streak(sink, Trunk[j], Trunk[j + 1], num9, 0.14f * size);
					GlowAlong(sink, Trunk[j], Trunk[j + 1], size, num2, num8, num4);
				}
				DrawBranches(sink, seed, size, num2, num9 * 0.8f, num8 * 0.75f, 32);
			}
			CloudGlow(sink, seed, size, num2, Math.Max(num3, num4 * 0.3f), age);
			Impact(sink, x, y, ground, size, num2, seed, age, num3, num4);
			if (strike && parts != null)
			{
				Burst(sink, parts, x, y, ground, size, seed, num);
			}
			return Math.Max(num3, num4 * 0.25f);
		}

		private static void BuildTrunk(float x, float y, float ground, float top, float size, uint seed)
		{
			float num = Math.Max(4f, top - ground);
			float num2 = (BlastFx.Rand(seed, 1) - 0.5f) * num * 0.35f;
			float num3 = (BlastFx.Rand(seed, 2) - 0.5f) * num * 0.35f;
			Trunk[0] = new Vec3(x + num2, y + num3, top);
			Trunk[32] = new Vec3(x, y, ground);
			Displace(Trunk, 0, 32, num * 0.16f * (0.8f + 0.4f * size), seed, 11);
		}

		private static void Displace(Vec3[] p, int a, int b, float amount, uint seed, int salt)
		{
			if (b - a >= 2)
			{
				int num = (a + b) / 2;
				Vec3 vec = (p[a] + p[b]) * 0.5f;
				float num2 = BlastFx.Rand(seed, salt * 97 + num * 3) - 0.5f;
				float num3 = BlastFx.Rand(seed, salt * 97 + num * 3 + 1) - 0.5f;
				float num4 = BlastFx.Rand(seed, salt * 97 + num * 3 + 2) - 0.5f;
				p[num] = new Vec3(vec.X + num2 * 2f * amount, vec.Y + num3 * 2f * amount, vec.Z + num4 * amount * 0.6f);
				Displace(p, a, num, amount * 0.55f, seed, salt);
				Displace(p, num, b, amount * 0.55f, seed, salt);
			}
		}

		private static void DrawBranches(FxSink sink, uint seed, float size, float least, float coreAlpha, float glow, int upTo)
		{
			int num = 4 + (int)(BlastFx.Rand(seed, 40) * 5f);
			Vec3 vec = Trunk[32] - Trunk[0];
			float num2 = (float)Math.Sqrt(Vec3.Dot(vec, vec));
			for (int i = 0; i < num; i++)
			{
				int num3 = 2 + (int)(BlastFx.Rand(seed, 50 + i) * 23.1f);
				if (num3 >= upTo)
				{
					continue;
				}
				float num4 = num2 * (0.12f + 0.28f * BlastFx.Rand(seed, 60 + i));
				float num5 = BlastFx.Rand(seed, 70 + i) * MathF.PI * 2f;
				Vec3 vec2 = Trunk[num3];
				Vec3 vec3 = new Vec3((float)Math.Cos(num5) * 0.75f, (float)Math.Sin(num5) * 0.75f, -0.65f).Normalized();
				FillBranch(vec2, vec2 + vec3 * num4, num4, seed, 200 + i * 13);
				for (int j = 0; j < 8; j++)
				{
					float num6 = 1f - (float)j / 8f;
					float num7 = 1f - (float)(j + 1) / 8f;
					sink.Line(Branch[j], Branch[j + 1], Hot, coreAlpha * num6, coreAlpha * num7, 0.07f * size);
					if (glow > 0.01f)
					{
						sink.Glow(Branch[j].X, Branch[j].Y, Branch[j].Z, Math.Max(1.4f * size, least * 2f) * (0.5f + 0.5f * num6), Blue, glow * 0.8f * num6);
					}
				}
				if (num4 > num2 * 0.25f)
				{
					Vec3 vec4 = Branch[4];
					float num8 = num5 + ((BlastFx.Rand(seed, 80 + i) > 0.5f) ? 0.9f : (-0.9f));
					Vec3 vec5 = new Vec3((float)Math.Cos(num8) * 0.8f, (float)Math.Sin(num8) * 0.8f, -0.55f).Normalized();
					FillBranch(vec4, vec4 + vec5 * num4 * 0.45f, num4 * 0.45f, seed, 400 + i * 13);
					for (int k = 0; k < 8; k++)
					{
						float num9 = 1f - (float)k / 8f;
						float num10 = 1f - (float)(k + 1) / 8f;
						sink.Line(Branch[k], Branch[k + 1], Hot, coreAlpha * 0.6f * num9, coreAlpha * 0.6f * num10, 0.05f * size);
					}
				}
			}
		}

		private static void FillBranch(Vec3 a, Vec3 b, float len, uint seed, int salt)
		{
			Branch[0] = a;
			Branch[8] = b;
			Displace(Branch, 0, 8, len * 0.22f, seed, salt);
		}

		private static void GlowAlong(FxSink sink, Vec3 a, Vec3 b, float size, float least, float shine, float after)
		{
			Vec3 vec = b - a;
			float num = (float)Math.Sqrt(Vec3.Dot(vec, vec));
			float num2 = Math.Max(2.4f * size, least * 3.2f);
			int num3 = Math.Max(1, Math.Min(8, (int)(num / (num2 * 0.3f))));
			for (int i = 0; i < num3; i++)
			{
				Vec3 vec2 = a + vec * (((float)i + 0.5f) / (float)num3);
				sink.Glow(vec2.X, vec2.Y, vec2.Z, num2, TerrainCache.Lerp(Deep, Violet, after * 0.5f), shine * 0.75f);
				sink.Glow(vec2.X, vec2.Y, vec2.Z, num2 * 0.4f, Blue, shine);
				sink.Glow(vec2.X, vec2.Y, vec2.Z, num2 * 0.16f, Core, shine);
			}
		}

		private static void Streak(FxSink sink, Vec3 a, Vec3 b, float alpha, float width)
		{
			if (sink.Streaks != null && !(alpha <= 0.004f))
			{
				sink.Streaks.Add(new Streak
				{
					A = a,
					B = b,
					Color = Core,
					AlphaA = Math.Min(1f, alpha),
					AlphaB = Math.Min(1f, alpha),
					Width = width,
					Glow = 1f,
					Edge = -1f
				});
			}
		}

		private static void CloudGlow(FxSink sink, uint seed, float size, float least, float k, float age)
		{
			if (!(k <= 0.01f))
			{
				Vec3 a = Trunk[0];
				sink.Glow(a.X, a.Y, a.Z + 0.6f, Math.Max(9f * size, least * 6f), Blue, k * 0.55f);
				sink.Glow(a.X, a.Y, a.Z, Math.Max(3.5f * size, least * 2.5f), Hot, k * 0.6f);
				int tick = (int)(age * 30f);
				for (int i = 0; i < 4; i++)
				{
					float num = BlastFx.Rand(seed, 90 + i) * MathF.PI * 2f;
					float num2 = (4f + 6f * BlastFx.Rand(seed, 95 + i)) * size;
					Jagged(b: new Vec3(a.X + (float)Math.Cos(num) * num2, a.Y + (float)Math.Sin(num) * num2, a.Z + (BlastFx.Rand(seed, 99 + i) - 0.3f) * 1.5f), sink: sink, a: a, kinks: 6, jitter: num2 * 0.18f, seed: seed + (uint)(i * 31), tick: tick, alpha: k * 0.7f, width: 0.06f * size, color: Hot);
				}
			}
		}

		private static void Jagged(FxSink sink, Vec3 a, Vec3 b, int kinks, float jitter, uint seed, int tick, float alpha, float width, uint color)
		{
			Vec3 a2 = a;
			for (int i = 1; i <= kinks; i++)
			{
				float num = (float)i / (float)kinks;
				Vec3 vec = a + (b - a) * num;
				if (i < kinks)
				{
					float num2 = jitter * (float)Math.Sin((double)num * Math.PI);
					vec = new Vec3(vec.X + (BlastFx.Rand(seed, tick * 131 + i) - 0.5f) * 2f * num2, vec.Y + (BlastFx.Rand(seed, tick * 131 + i + 61) - 0.5f) * 2f * num2, vec.Z + (BlastFx.Rand(seed, tick * 131 + i + 17) - 0.5f) * num2 * 0.6f);
				}
				sink.Line(a2, vec, color, alpha * (1f - num * 0.6f), alpha * (1f - num * 0.6f), width);
				a2 = vec;
			}
		}

		private static void Impact(FxSink sink, float x, float y, float g, float size, float least, uint seed, float age, float stroke, float after)
		{
			float num = age - 0.09f;
			float num2 = Math.Max(stroke, after * 0.45f);
			if (stroke > 0.02f)
			{
				float num3 = Math.Max(5.5f * size, least * 5f) * (0.75f + 0.25f * stroke);
				sink.Sprite(BlastFx.StarSprite, x, y, g + 0.5f * size, num3, num3, Core, (byte)Math.Max(2, Math.Min(255, (int)(stroke * 255f))), emissive: true, additive: true);
				sink.Glow(x, y, g + 0.4f, Math.Max(2.6f * size, least * 2.4f), Core, Math.Min(1f, stroke * 1.3f));
			}
			sink.Glow(x, y, g + 0.3f, Math.Max(10f * size, least * 7f), Blue, num2 * 0.7f);
			sink.Glow(x, y, g + 0.2f, Math.Max(5f * size, least * 4f), Hot, num2 * 0.5f);
			if (num2 > 0.05f)
			{
				int num4 = (int)(age * 24f);
				int num5 = 7;
				for (int i = 0; i < num5; i++)
				{
					float num6 = ((float)i + BlastFx.Rand(seed, 300 + i) * 0.7f) / (float)num5 * MathF.PI * 2f;
					float num7 = (2.2f + 3.2f * BlastFx.Rand(seed, 310 + i + num4 * 7)) * size * (0.6f + 0.4f * Math.Min(1f, num / 0.12f));
					Tendril[0] = new Vec3(x, y, g + 0.08f);
					for (int j = 1; j < Tendril.Length; j++)
					{
						float num8 = (float)j / (float)(Tendril.Length - 1);
						float num9 = (BlastFx.Rand(seed, 330 + i * 17 + j + num4 * 13) - 0.5f) * 0.9f * num8;
						float num10 = x + (float)Math.Cos(num6 + num9) * num7 * num8;
						float num11 = y + (float)Math.Sin(num6 + num9) * num7 * num8;
						Tendril[j] = new Vec3(num10, num11, sink.Ground(num10, num11) + 0.08f);
					}
					for (int k = 0; k < Tendril.Length - 1; k++)
					{
						float num12 = (float)k / (float)(Tendril.Length - 1);
						sink.Line(Tendril[k], Tendril[k + 1], Hot, num2 * (1f - num12), num2 * (1f - num12 - 1f / (float)(Tendril.Length - 1)), 0.05f * size);
					}
					Vec3 vec = Tendril[Tendril.Length - 1];
					sink.Glow(vec.X, vec.Y, vec.Z + 0.1f, Math.Max(0.9f * size, least), Blue, num2 * 0.5f);
				}
			}
			if (num > 0f && num < 0.45f)
			{
				float num13 = num / 0.45f;
				float num14 = (1f + 7f * (float)Math.Sqrt(num13)) * size;
				float strength = (1f - num13) * 0.55f;
				int num15 = 28;
				for (int l = 0; l < num15; l++)
				{
					float num16 = (float)l / (float)num15 * MathF.PI * 2f;
					float num17 = x + (float)Math.Cos(num16) * num14;
					float num18 = y + (float)Math.Sin(num16) * num14;
					sink.Glow(num17, num18, sink.Ground(num17, num18) + 0.2f, Math.Max(1.4f * size, least * 1.2f), TerrainCache.Lerp(Hot, Blue, num13), strength);
				}
			}
			sink.Light(x, y, g + 3f, 22f * size, Hot, 0.9f * stroke + 0.2f * after);
			float num19 = ((num > 0f) ? ((float)Math.Exp((0f - num) * 2.2f) * BlastFx.Clamp01(num / 0.2f)) : 0f);
			if (num19 > 0.02f)
			{
				sink.Glow(x, y, g + 0.15f, 1.6f * size, Scorch, num19 * 0.8f);
				sink.Light(x, y, g + 0.6f, 4f * size, Scorch, num19 * 0.8f);
			}
		}

		private static void Burst(FxSink sink, FxParticles parts, float x, float y, float g, float size, uint seed, float dist)
		{
			Vec3 vec = new Vec3(x, y, g + 0.2f);
			int num = (int)(34f * size);
			for (int i = 0; i < num; i++)
			{
				float num2 = BlastFx.Rand(seed, 500 + i) * MathF.PI * 2f;
				float num3 = 0.25f + 0.75f * BlastFx.Rand(seed, 600 + i);
				float num4 = (3f + 7f * BlastFx.Rand(seed, 700 + i)) * size;
				float num5 = (float)Math.Sqrt(1f - num3 * num3);
				parts.Spark(vec, new Vec3((float)Math.Cos(num2) * num5 * num4, (float)Math.Sin(num2) * num5 * num4, num3 * num4), 0.35f + 0.55f * BlastFx.Rand(seed, 800 + i), 0.12f, (i % 3 == 0) ? Core : Hot, Deep, 9f, 0.8f);
			}
			for (int j = 0; j < 12; j++)
			{
				float num6 = BlastFx.Rand(seed, 900 + j) * MathF.PI * 2f;
				float num7 = 1.5f * BlastFx.Rand(seed, 920 + j) * size;
				parts.Add(new FxParticles.P
				{
					Pos = new Vec3(x + (float)Math.Cos(num6) * num7, y + (float)Math.Sin(num6) * num7, g + 0.3f + 1.5f * BlastFx.Rand(seed, 940 + j)),
					Vel = new Vec3((BlastFx.Rand(seed, 960 + j) - 0.5f) * 1.2f, (BlastFx.Rand(seed, 980 + j) - 0.5f) * 1.2f, 0.6f + 1.2f * BlastFx.Rand(seed, 990 + j)),
					Life = 0.8f + 0.7f * BlastFx.Rand(seed, 1000 + j),
					Size = 0.35f * size,
					EndSize = 0.15f * size,
					Color = Blue,
					EndColor = Violet,
					Drag = 1.5f,
					Kind = FxParticles.Kind.Glow
				});
			}
			for (int k = 0; k < 6; k++)
			{
				float num8 = BlastFx.Rand(seed, 1100 + k) * MathF.PI * 2f;
				float num9 = 2f + 3f * BlastFx.Rand(seed, 1110 + k);
				parts.Add(new FxParticles.P
				{
					Pos = vec,
					Vel = new Vec3((float)Math.Cos(num8) * num9, (float)Math.Sin(num8) * num9, 3f + 3f * BlastFx.Rand(seed, 1120 + k)),
					Life = 0.9f + 0.6f * BlastFx.Rand(seed, 1130 + k),
					Size = 0.25f,
					EndSize = 0.15f,
					Color = Scorch,
					EndColor = TerrainCache.Rgb(160, 40, 20),
					Gravity = 7f,
					Drag = 0.6f,
					Kind = FxParticles.Kind.Ember
				});
			}
			for (int l = 0; l < 8; l++)
			{
				float num10 = (float)l / 8f * MathF.PI * 2f + BlastFx.Rand(seed, 1200 + l);
				float num11 = (1.2f + 1.5f * BlastFx.Rand(seed, 1210 + l)) * size;
				parts.Smoke(new Vec3(x, y, g + 0.3f), new Vec3((float)Math.Cos(num10) * num11, (float)Math.Sin(num10) * num11, 0.5f), 1.4f + 0.8f * BlastFx.Rand(seed, 1220 + l), 1.2f * size, 3.6f * size, TerrainCache.Rgb(178, 192, 230), TerrainCache.Rgb(150, 150, 156), 95, l, 1.4f);
			}
			sink.Shake = Math.Max(sink.Shake, 0.45f * BlastFx.Clamp01(1f - dist / 28f));
			sink.AddFlash(0.18f * BlastFx.Clamp01(1f - dist / 50f), TerrainCache.Rgb(220, 232, 255));
		}
	}
}
