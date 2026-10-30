using System;

namespace FirstPerson.Core
{
	public sealed class MeteorFx
	{
		private readonly MeshInstance _rock = new MeshInstance
		{
			NoShadow = true
		};

		private readonly uint _seed;

		private float _spin;

		private float _sparkClock;

		private float _trailLeft;

		private Vec3 _axis;

		private bool _haveHead;

		public Vec3 LastHead;

		private static readonly uint[] TrailColors = new uint[7]
		{
			TerrainCache.Rgb(255, 255, 236),
			TerrainCache.Rgb(255, 232, 120),
			TerrainCache.Rgb(255, 160, 60),
			TerrainCache.Rgb(244, 80, 52),
			TerrainCache.Rgb(232, 64, 108),
			TerrainCache.Rgb(164, 56, 150),
			TerrainCache.Rgb(90, 40, 120)
		};

		private static readonly uint[] SparkColors = new uint[5]
		{
			TerrainCache.Rgb(255, 255, 230),
			TerrainCache.Rgb(255, 226, 110),
			TerrainCache.Rgb(255, 150, 60),
			TerrainCache.Rgb(250, 84, 60),
			TerrainCache.Rgb(236, 80, 140)
		};

		public MeteorFx(uint seed)
		{
			_seed = seed;
			_axis = new Vec3(BlastFx.Rand(seed, 1) - 0.5f, BlastFx.Rand(seed, 2) - 0.5f, BlastFx.Rand(seed, 3) - 0.5f).Normalized();
		}

		private static uint TrailColor(float k)
		{
			float num = BlastFx.Clamp01(k) * (float)(TrailColors.Length - 1);
			int num2 = Math.Min(TrailColors.Length - 2, (int)num);
			return TerrainCache.Lerp(TrailColors[num2], TrailColors[num2 + 1], num - (float)num2);
		}

		public void Draw(FxSink sink, FxParticles parts, Vec3 head, Vec3 back, float size, float dt, float time)
		{
			Vec3 vec = (_haveHead ? LastHead : head);
			LastHead = head;
			_haveHead = true;
			back = back.Normalized();
			float num = (float)Math.Sqrt((head.X - sink.EyeX) * (head.X - sink.EyeX) + (head.Y - sink.EyeY) * (head.Y - sink.EyeY) + (head.Z - sink.EyeZ) * (head.Z - sink.EyeZ));
			Vec3 vec2 = new Vec3(sink.EyeX - head.X, sink.EyeY - head.Y, sink.EyeZ - head.Z);
			float num2 = (float)Math.Sqrt(Vec3.Dot(vec2, vec2));
			Vec3 vec3 = ((num2 > 0.001f) ? (vec2 * (-1f / num2)) : new Vec3(0f, 0f, 0f));
			float num3 = 0.88f + 0.12f * (float)Math.Sin(time * 31f + (float)_seed);
			MeshModel meshModel = ((sink.Model == null) ? null : ((num < 40f) ? sink.Model("meteor_rock") : (sink.Model("meteor_rock_f") ?? sink.Model("meteor_rock"))));
			if (meshModel != null)
			{
				_spin += dt * 2.4f;
				float num4 = size * 0.62f;
				Mat3 b = Mat3.Rotation(_axis, _spin);
				Mat3 a = BlastFx.AlongAxis(back, 0f) * b;
				_rock.Model = meshModel;
				_rock.Basis = a * num4;
				_rock.Position = head;
				_rock.Palette = null;
				_rock.SortDepth = num;
				_rock.Emissive = false;
				sink.AddMesh(_rock);
			}
			else
			{
				sink.Sprite(BlastFx.Puff((int)_seed), head.X, head.Y, head.Z, size * 0.6f, size * 0.6f, TerrainCache.Rgb(90, 60, 50), 0, emissive: true);
			}
			Vec3 vec4 = head + vec3 * (size * 0.6f);
			sink.Glow(vec4.X, vec4.Y, vec4.Z, size * 1.5f, TerrainCache.Rgb(255, 255, 240), 0.95f * num3);
			sink.Glow(vec4.X, vec4.Y, vec4.Z, size * 2.8f, TerrainCache.Rgb(255, 214, 110), 0.7f * num3);
			sink.Glow(vec4.X, vec4.Y, vec4.Z, size * 5f, TerrainCache.Rgb(255, 120, 60), 0.4f);
			for (int i = 0; i < 5; i++)
			{
				float num5 = time * 13f + (float)i * 1.256f;
				Vec3 vec5 = Vec3.Cross(back, new Vec3(0f, 0f, 1f));
				if (Vec3.Dot(vec5, vec5) < 0.0001f)
				{
					vec5 = new Vec3(1f, 0f, 0f);
				}
				vec5 = vec5.Normalized();
				Vec3 vec6 = Vec3.Cross(vec5, back);
				Vec3 vec7 = head + (vec5 * (float)Math.Cos(num5) + vec6 * (float)Math.Sin(num5)) * (size * 0.3f) + back * (size * 0.25f);
				float num6 = size * (0.5f + 0.15f * (float)Math.Sin(num5 * 1.7f));
				sink.Sprite(BlastFx.Puff(i + (int)(time * 6f)), vec7.X, vec7.Y, vec7.Z, num6, num6, TrailColor(0.1f + 0.12f * (float)i / 5f), 0, emissive: true, additive: false, (i & 1) != 0);
			}
			float num7 = size * 9f;
			for (int j = 1; j <= 16; j++)
			{
				float num8 = (float)j / 16f;
				float num9 = num7 * (float)Math.Pow(num8, 1.25);
				float num10 = size * 0.25f * num8 * (float)Math.Sin(time * 5f + (float)j * 0.9f);
				Vec3 vec8 = head + back * num9 + new Vec3(num10, (0f - num10) * 0.5f, 0f);
				float size2 = size * (2.2f - 1.4f * num8) * (1f + 0.15f * (float)Math.Sin(time * 9f + (float)j));
				float strength = (1f - num8) * (1f - num8) * 0.85f + 0.06f;
				sink.Glow(vec8.X, vec8.Y, vec8.Z, size2, TrailColor(num8), strength);
			}
			for (int k = 1; k <= 10; k++)
			{
				float num11 = (float)k / 10f;
				Vec3 vec9 = head + back * (size * 7f * num11);
				sink.Glow(vec9.X, vec9.Y, vec9.Z, size * (1.1f - 0.7f * num11), TerrainCache.Rgb(255, 250, 220), 0.8f * (1f - num11));
			}
			if (parts != null && !(dt <= 0f))
			{
				_sparkClock += dt * ((num < 120f) ? 90f : 40f);
				while (_sparkClock > 1f)
				{
					_sparkClock -= 1f;
					int num12 = parts.List.Count + (int)(time * 997f);
					float num13 = BlastFx.Rand(_seed, num12);
					float num14 = BlastFx.Rand(_seed, num12 + 1);
					float num15 = BlastFx.Rand(_seed, num12 + 2);
					Vec3 pos = head + back * (size * (0.5f + 6f * num13 * num13)) + new Vec3((num14 - 0.5f) * size * 1.6f, (num15 - 0.5f) * size * 1.6f, (BlastFx.Rand(_seed, num12 + 3) - 0.5f) * size * 1.6f);
					uint color = SparkColors[(int)(BlastFx.Rand(_seed, num12 + 4) * (float)SparkColors.Length) % SparkColors.Length];
					float num16 = size * (0.12f + 0.18f * BlastFx.Rand(_seed, num12 + 5));
					parts.Add(new FxParticles.P
					{
						Pos = pos,
						Vel = back * (1.5f + 2f * num14) + new Vec3((num15 - 0.5f) * 1.5f, (num13 - 0.5f) * 1.5f, -0.5f),
						Life = 0.6f + 1.4f * BlastFx.Rand(_seed, num12 + 6),
						Size = num16,
						EndSize = num16 * 0.5f,
						Drag = 1.6f,
						Gravity = 0.6f,
						Color = color,
						EndColor = TrailColor(0.5f + 0.5f * num13),
						Kind = FxParticles.Kind.Spark
					});
				}
				Vec3 vec10 = head - vec;
				float num17 = (float)Math.Sqrt(Vec3.Dot(vec10, vec10));
				if (num17 > 40f)
				{
					vec = head + back * Math.Min(num17, size * 6f);
					vec10 = head - vec;
					num17 = (float)Math.Sqrt(Vec3.Dot(vec10, vec10));
				}
				float num18 = size * 0.45f;
				_trailLeft += num17;
				int num19 = 0;
				while (_trailLeft > num18 && num19 < 400)
				{
					_trailLeft -= num18;
					float num20 = ((num17 > 0.0001f) ? (_trailLeft / num17) : 0f);
					Vec3 vec11 = head - vec10 * num20;
					int num21 = parts.List.Count + num19 * 13 + (int)(time * 89f);
					float num22 = BlastFx.Rand(_seed, num21);
					parts.Add(new FxParticles.P
					{
						Pos = vec11 + new Vec3((num22 - 0.5f) * size * 0.3f, (BlastFx.Rand(_seed, num21 + 1) - 0.5f) * size * 0.3f, 0f),
						Vel = back * 0.4f,
						Life = 1.6f + 0.6f * num22,
						Size = size * 1.5f,
						EndSize = size * 2.8f,
						Drag = 0.5f,
						Gravity = 0f,
						Color = TrailColor(0.12f),
						EndColor = TrailColor(1f),
						Alpha = 190,
						Kind = FxParticles.Kind.Trail
					});
					num19++;
				}
				sink.Light(head.X, head.Y, head.Z, 14f + size * 4f, TerrainCache.Rgb(255, 196, 120), 1.5f * num3, size * 0.8f);
			}
		}
	}
}
