using System;

namespace FirstPerson.Core
{
	public static class BlockFx
	{
		private static readonly uint Hot = TerrainCache.Rgb(255, 252, 232);

		private static readonly uint Cool = TerrainCache.Rgb(255, 150, 60);

		private static readonly uint Core = TerrainCache.Rgb(236, 246, 255);

		private static readonly uint CoreEnd = TerrainCache.Rgb(255, 196, 120);

		private static readonly uint Chip = TerrainCache.Rgb(152, 112, 72);

		private static readonly uint ChipDark = TerrainCache.Rgb(104, 76, 50);

		private static readonly uint Puff = TerrainCache.Rgb(196, 188, 176);

		private static readonly uint PuffEnd = TerrainCache.Rgb(214, 208, 200);

		public static void Stopped(FxParticles parts, Vec3 at, Vec3 toward, Clash material, float weight, bool shot, uint seed, float glow = 1f)
		{
			if (parts == null)
			{
				return;
			}
			weight = Math.Max(0f, Math.Min(1f, weight));
			toward = new Vec3(toward.X, toward.Y, 0f).Normalized();
			Vec3 vec = new Vec3(0f - toward.Y, toward.X, 0f);
			Vec3 vec2 = new Vec3(0f, 0f, 1f);
			float num = (shot ? 0.6f : 1f);
			switch (material)
			{
			case Clash.Metal:
			{
				FxParticles.P p = new FxParticles.P
				{
					Pos = at,
					Life = 0.1f + 0.04f * weight,
					Size = (0.1f + 0.07f * weight) * num * glow,
					EndSize = 0.03f * glow,
					Color = Core,
					EndColor = CoreEnd,
					Alpha = 235,
					Kind = FxParticles.Kind.Glow
				};
				parts.Add(in p);
				int num5 = (shot ? 5 : (9 + (int)(6f * weight)));
				for (int j = 0; j < num5; j++)
				{
					float num6 = BlastFx.Rand(seed, j) * 2f - 1f;
					float num7 = BlastFx.Rand(seed, j + 17);
					float num8 = BlastFx.Rand(seed, j + 31);
					Vec3 vec4 = (vec * (num6 * 1.3f) + vec2 * (0.25f + 0.9f * num7) + toward * (0.7f * num8 - 0.15f)).Normalized();
					float num9 = (2.4f + 3.6f * BlastFx.Rand(seed, j + 43) + 1.5f * weight) * num;
					parts.Spark(at, vec4 * num9, 0.16f + 0.24f * BlastFx.Rand(seed, j + 59), 0.027f, Hot, Cool, 9f, 1.1f);
				}
				break;
			}
			case Clash.Wood:
			{
				int num2 = (shot ? 3 : (4 + (int)(3f * weight)));
				for (int i = 0; i < num2; i++)
				{
					float num3 = BlastFx.Rand(seed, i) * 2f - 1f;
					float num4 = BlastFx.Rand(seed, i + 17);
					Vec3 vec3 = (vec * num3 + vec2 * (0.5f + num4) + toward * 0.4f).Normalized();
					parts.Add(new FxParticles.P
					{
						Pos = at,
						Vel = vec3 * (1.4f + 1.8f * BlastFx.Rand(seed, i + 29)) * num,
						Life = 0.45f + 0.25f * BlastFx.Rand(seed, i + 37),
						Size = 0.04f,
						EndSize = 0.035f,
						Color = Chip,
						EndColor = ChipDark,
						Gravity = 9.8f,
						Drag = 0.6f,
						Kind = FxParticles.Kind.Drop,
						Matte = true
					});
				}
				Dust(parts, at, toward, 1, 0.05f * num, seed);
				break;
			}
			default:
				Dust(parts, at, toward, 2, 0.06f * num, seed);
				break;
			}
		}

		public static void Dodged(FxParticles parts, Vec3 feet, Vec3 toward, uint seed)
		{
			if (parts != null)
			{
				for (int i = 0; i < 3; i++)
				{
					float num = ((float)i / 3f + BlastFx.Rand(seed, i) * 0.2f) * 6.2832f;
					Vec3 vec = new Vec3((float)Math.Cos(num), (float)Math.Sin(num), 0.35f);
					parts.Add(new FxParticles.P
					{
						Pos = feet + new Vec3(0f, 0f, 0.03f),
						Vel = vec * (0.5f + 0.3f * BlastFx.Rand(seed, i + 5)),
						Life = 0.5f + 0.25f * BlastFx.Rand(seed, i + 9),
						Size = 0.06f,
						EndSize = 0.2f,
						Color = Puff,
						EndColor = PuffEnd,
						Alpha = 120,
						Variant = (int)(BlastFx.Rand(seed, i + 13) * 6f),
						Drag = 2.4f,
						Gravity = 0.2f,
						Kind = FxParticles.Kind.Dust
					});
				}
			}
		}

		private static void Dust(FxParticles parts, Vec3 at, Vec3 toward, int n, float size, uint seed)
		{
			for (int i = 0; i < n; i++)
			{
				parts.Add(new FxParticles.P
				{
					Pos = at + toward * 0.03f,
					Vel = toward * (0.4f + 0.3f * BlastFx.Rand(seed, i + 70)) + new Vec3(0f, 0f, 0.3f),
					Life = 0.4f + 0.2f * BlastFx.Rand(seed, i + 74),
					Size = size,
					EndSize = size * 3f,
					Color = Puff,
					EndColor = PuffEnd,
					Alpha = 150,
					Variant = (int)(BlastFx.Rand(seed, i + 78) * 6f),
					Drag = 2.6f,
					Gravity = 0.3f,
					Kind = FxParticles.Kind.Dust
				});
			}
		}
	}
}
