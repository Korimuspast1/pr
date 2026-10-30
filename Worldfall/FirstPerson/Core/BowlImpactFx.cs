using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class BowlImpactFx
	{
		private struct Shard
		{
			public MeshInstance Inst;

			public Vec3 Pos;

			public Vec3 Vel;

			public Vec3 Axis;

			public float Angle;

			public float Spin;

			public float Size;

			public float Rest;

			public bool Landed;
		}

		public readonly float X;

		public readonly float Y;

		public readonly float Ground;

		public readonly float Scale;

		public readonly uint Seed;

		public readonly bool Water;

		public readonly bool Final;

		public float Age;

		private readonly uint _land;

		private readonly Billows _billows = new Billows
		{
			Max = 120
		};

		private readonly SpeedLines _lines = new SpeedLines();

		private CraterFx _crater;

		private bool _burst;

		private readonly List<Shard> _shards = new List<Shard>();

		public bool Finished
		{
			get
			{
				if (Age > 9f && _billows.List.Count == 0 && (_crater == null || _crater.Finished))
				{
					return _shards.Count == 0;
				}
				return false;
			}
		}

		public BowlImpactFx(float x, float y, float ground, float scale, uint seed, uint land, bool water, bool final)
		{
			X = x;
			Y = y;
			Ground = ground;
			Scale = scale;
			Seed = seed;
			_land = land;
			Water = water;
			Final = final;
		}

		public void Step(float dt, FxSink sink, FxParticles parts)
		{
			Age += dt;
			float age = Age;
			float scale = Scale;
			float num = sink.Distance(X, Y);
			if (!_burst)
			{
				_burst = true;
				if (Water)
				{
					Splash(sink, parts);
				}
				else
				{
					Burst(sink, parts);
				}
				if (Final)
				{
					Shatter(sink, parts);
				}
				float num2 = (Final ? 1.3f : 0.8f);
				sink.Shake = Math.Max(sink.Shake, num2 * BlastFx.Clamp01(1f - num / (Final ? 70f : 45f)));
			}
			if (!Water)
			{
				if (age < 0.3f)
				{
					float num3 = 1f - age / 0.3f;
					sink.Glow(X, Y, Ground + 1f * scale, scale * (6f + 6f * age), TerrainCache.Rgb(255, 250, 200), 0.9f * num3);
					sink.Glow(X, Y, Ground + 0.6f * scale, scale * 10f, TerrainCache.Rgb(255, 170, 60), 0.6f * num3);
				}
				sink.Light(X, Y, Ground + 1.5f * scale, 16f * scale, BlastFx.Heat(0.95f), (Final ? 2.6f : 1.6f) * (float)Math.Exp((0f - age) / 0.3f), (age < 0.25f) ? (3f * scale) : 0f);
			}
			if (Final && age < 0.6f)
			{
				float num4 = 1f - age / 0.6f;
				sink.AddFlash(0.35f * num4 * num4 * BlastFx.Clamp01(1.2f - num / 80f) * (0.5f + 0.5f * Math.Max(0f, sink.Facing(X, Y))), TerrainCache.Rgb(255, 236, 200));
				sink.Sprite(BlastFx.StarSprite, X, Y, Ground + 2f * scale, scale * (14f + 18f * age), scale * (14f + 18f * age), TerrainCache.Rgb(255, 236, 190), (byte)Math.Max(2, (int)(255f * num4)), emissive: true, additive: true);
			}
			if (age < 1.3f)
			{
				Ring(sink, age);
			}
			StepShards(dt, sink, parts, age);
			if (_crater == null && Final && age > 0.3f)
			{
				_crater = new CraterFx(X, Y, 4f * scale, 40f, Seed ^ 0x5BD1, big: false);
			}
			_crater?.Step(dt, sink, parts);
			_billows.Step(dt);
			_billows.Draw(sink);
			_lines.Step(dt, 5f);
			_lines.Draw(sink);
		}

		private void Burst(FxSink sink, FxParticles parts)
		{
			float scale = Scale;
			int num = (Final ? 46 : 30);
			float num2 = BowlingBallFx.Radius * 0.75f * scale;
			for (int i = 0; i < num; i++)
			{
				float num3 = ((float)i + PowerFx.Rand(Seed, i) * 0.8f) / (float)num * 6.2832f;
				float num4 = num2 * (0.6f + 0.5f * PowerFx.Rand(Seed, i + 1));
				float num5 = scale * (3f + 4f * PowerFx.Rand(Seed, i + 2));
				float z = scale * (6f + 7f * PowerFx.Rand(Seed, i + 3));
				float num6 = scale * (1.3f + 1.5f * PowerFx.Rand(Seed, i + 4));
				_billows.Add(new Vec3(X + (float)Math.Cos(num3) * num4, Y + (float)Math.Sin(num3) * num4, Ground + num6 * 0.4f), new Vec3((float)Math.Cos(num3) * num5, (float)Math.Sin(num3) * num5, z), num6, num6 * (1.8f + 0.6f * PowerFx.Rand(Seed, i + 5)), 1.5f + 1.4f * PowerFx.Rand(Seed, i + 6), 1f, i, -2.4f);
			}
			if (parts != null)
			{
				for (int j = 0; j < (Final ? 30 : 16); j++)
				{
					float num7 = PowerFx.Rand(Seed, j + 50) * 6.2832f;
					float num8 = num2 * PowerFx.Rand(Seed, j + 51);
					float num9 = scale * (3f + 5f * PowerFx.Rand(Seed, j + 52));
					float num10 = scale * (1.2f + 1.2f * PowerFx.Rand(Seed, j + 53)) * (Final ? 1.4f : 1f);
					parts.Add(new FxParticles.P
					{
						Pos = new Vec3(X + (float)Math.Cos(num7) * num8, Y + (float)Math.Sin(num7) * num8, Ground + 0.5f * scale),
						Vel = new Vec3((float)Math.Cos(num7) * num9, (float)Math.Sin(num7) * num9, scale * (1f + (Final ? 7f : 3f) * PowerFx.Rand(Seed, j + 54))),
						Life = 0.45f + 0.35f * PowerFx.Rand(Seed, j + 55),
						Size = num10,
						EndSize = num10 * 1.6f,
						Drag = 3f,
						Gravity = -1f,
						Color = BlastFx.Heat(1.15f),
						EndColor = BlastFx.Heat(0.3f),
						Kind = FxParticles.Kind.Fire,
						Variant = j
					});
				}
				uint num11 = TerrainCache.Lerp(TerrainCache.Rgb(96, 74, 54), _land, 0.45f);
				for (int k = 0; k < (Final ? 60 : 36); k++)
				{
					float num12 = PowerFx.Rand(Seed, k + 200) * 6.2832f;
					float num13 = scale * (3f + 8f * PowerFx.Rand(Seed, k + 201));
					parts.Add(new FxParticles.P
					{
						Pos = new Vec3(X + (float)Math.Cos(num12) * num2 * 0.7f, Y + (float)Math.Sin(num12) * num2 * 0.7f, Ground + 0.4f),
						Vel = new Vec3((float)Math.Cos(num12) * num13, (float)Math.Sin(num12) * num13, scale * (5f + 9f * PowerFx.Rand(Seed, k + 202))),
						Life = 3f,
						Size = (0.2f + 0.2f * PowerFx.Rand(Seed, k + 203)) * scale,
						EndSize = 0.2f * scale,
						Gravity = 9.8f,
						Drag = 0.15f,
						Color = ((k % 4 == 0) ? TerrainCache.Rgb(120, 116, 110) : num11),
						EndColor = ((k % 4 == 0) ? TerrainCache.Rgb(120, 116, 110) : num11),
						Kind = FxParticles.Kind.Drop,
						Matte = true
					});
				}
				for (int l = 0; l < 24; l++)
				{
					float num14 = PowerFx.Rand(Seed, l + 260) * 6.2832f;
					float num15 = scale * (6f + 10f * PowerFx.Rand(Seed, l + 261));
					parts.Spark(new Vec3(X, Y, Ground + 0.6f * scale), new Vec3((float)Math.Cos(num14) * num15, (float)Math.Sin(num14) * num15, scale * (4f + 9f * PowerFx.Rand(Seed, l + 262))), 0.6f + 0.7f * PowerFx.Rand(Seed, l + 263), 0.11f * scale, BlastFx.Heat(1.15f), BlastFx.Heat(0.45f), 9.8f);
				}
			}
			int num16 = (Final ? 22 : 14);
			for (int m = 0; m < num16; m++)
			{
				float num17 = PowerFx.Rand(Seed, m + 300) * 6.2832f;
				float z2 = 0.6f + 1.6f * PowerFx.Rand(Seed, m + 301);
				Vec3 vec = new Vec3((float)Math.Cos(num17), (float)Math.Sin(num17), z2).Normalized();
				_lines.Add(new Vec3(X, Y, Ground + 0.5f * scale) + vec * (num2 * 0.5f), vec * (scale * (22f + 14f * PowerFx.Rand(Seed, m + 302))), scale * (4f + 3f * PowerFx.Rand(Seed, m + 303)), 0.45f * scale, 0.34f + 0.2f * PowerFx.Rand(Seed, m + 304), (PowerFx.Rand(Seed, m + 305) < 0.5f) ? TerrainCache.Rgb(255, 255, 240) : TerrainCache.Rgb(255, 236, 90), additive: false);
			}
		}

		private void Splash(FxSink sink, FxParticles parts)
		{
			float scale = Scale;
			float num = BowlingBallFx.Radius * 0.75f * scale;
			if (parts != null)
			{
				for (int i = 0; i < 90; i++)
				{
					float num2 = PowerFx.Rand(Seed, i) * 6.2832f;
					float num3 = num * (0.6f + 0.4f * PowerFx.Rand(Seed, i + 1));
					float num4 = scale * (2f + 5f * PowerFx.Rand(Seed, i + 2));
					float z = scale * (7f + 11f * PowerFx.Rand(Seed, i + 3));
					uint num5 = ((PowerFx.Rand(Seed, i + 4) < 0.45f) ? TerrainCache.Rgb(236, 246, 255) : TerrainCache.Rgb(110, 176, 236));
					parts.Add(new FxParticles.P
					{
						Pos = new Vec3(X + (float)Math.Cos(num2) * num3, Y + (float)Math.Sin(num2) * num3, Ground + 0.3f),
						Vel = new Vec3((float)Math.Cos(num2) * num4, (float)Math.Sin(num2) * num4, z),
						Life = 3f,
						Size = (0.25f + 0.25f * PowerFx.Rand(Seed, i + 5)) * scale,
						EndSize = 0.18f * scale,
						Gravity = 11f,
						Drag = 0.2f,
						Color = num5,
						EndColor = num5,
						Kind = FxParticles.Kind.Drop,
						Matte = true
					});
				}
				for (int j = 0; j < 26; j++)
				{
					float num6 = ((float)j + PowerFx.Rand(Seed, j + 100)) / 26f * 6.2832f;
					float num7 = num * 0.9f;
					float num8 = scale * (1.5f + 2f * PowerFx.Rand(Seed, j + 101));
					parts.Smoke(new Vec3(X + (float)Math.Cos(num6) * num7, Y + (float)Math.Sin(num6) * num7, Ground + scale * (0.5f + 3f * PowerFx.Rand(Seed, j + 102))), new Vec3((float)Math.Cos(num6) * num8, (float)Math.Sin(num6) * num8, scale * (2f + 3f * PowerFx.Rand(Seed, j + 103))), 1.8f, scale * 1.4f, scale * 3.4f, TerrainCache.Rgb(240, 248, 255), TerrainCache.Rgb(214, 230, 240), 200, j, 1.3f);
				}
			}
		}

		private void Ring(FxSink sink, float t)
		{
			float scale = Scale;
			float num = scale * (Final ? 15f : 10f) * (1f - (float)Math.Exp((0f - t) / 0.3f)) + BowlingBallFx.Radius * 0.6f;
			int num2 = (int)(200f * (1f - t / 1.3f));
			if (num2 >= 3)
			{
				uint multiply = (Water ? TerrainCache.Rgb(232, 244, 255) : TerrainCache.Lerp(TerrainCache.Rgb(160, 140, 112), _land, 0.35f));
				int num3 = 30;
				for (int i = 0; i < num3; i++)
				{
					float num4 = ((float)i + PowerFx.Rand(Seed, i + 400) * 0.5f) / (float)num3 * 6.2832f;
					float num5 = X + (float)Math.Cos(num4) * num;
					float num6 = Y + (float)Math.Sin(num4) * num;
					float num7 = ((sink.Ground != null) ? sink.Ground(num5, num6) : Ground);
					float num8 = scale * (1.3f + num * 0.08f) * (0.8f + 0.4f * PowerFx.Rand(Seed, i + 401));
					sink.Sprite(BlastFx.Soft(i), num5, num6, num7 + num8 * 0.32f, num8 * 1.6f, num8, multiply, (byte)num2, emissive: false, additive: false, (i & 1) != 0);
				}
			}
		}

		private void Shatter(FxSink sink, FxParticles parts)
		{
			if (sink.Model == null)
			{
				return;
			}
			string[] array = new string[3] { "bowling_shard_a", "bowling_shard_b", "bowling_shard_c" };
			float scale = Scale;
			float radius = BowlingBallFx.Radius;
			for (int i = 0; i < 12; i++)
			{
				MeshModel meshModel = sink.Model(array[i % 3]);
				if (meshModel != null)
				{
					float num = ((float)i + PowerFx.Rand(Seed, i + 500) * 0.7f) / 12f * 6.2832f;
					float num2 = PowerFx.Rand(Seed, i + 501);
					float num3 = scale * (4f + 6f * PowerFx.Rand(Seed, i + 502));
					Vec3 vec = new Vec3((float)Math.Cos(num), (float)Math.Sin(num), 0.3f + num2).Normalized();
					_shards.Add(new Shard
					{
						Inst = new MeshInstance
						{
							Model = meshModel,
							DissolveScale = 0.2f
						},
						Pos = new Vec3(X, Y, Ground + radius * 0.6f) + vec * (radius * 0.5f),
						Vel = vec * num3 + new Vec3(0f, 0f, scale * 6f),
						Axis = new Vec3(PowerFx.Rand(Seed, i + 503) - 0.5f, PowerFx.Rand(Seed, i + 504) - 0.5f, PowerFx.Rand(Seed, i + 505) - 0.5f).Normalized(),
						Spin = 3f + 6f * PowerFx.Rand(Seed, i + 506),
						Size = radius * (0.5f + 0.35f * PowerFx.Rand(Seed, i + 507)),
						Rest = 0.2f
					});
					continue;
				}
				break;
			}
		}

		private void StepShards(float dt, FxSink sink, FxParticles parts, float t)
		{
			for (int num = _shards.Count - 1; num >= 0; num--)
			{
				Shard value = _shards[num];
				if (!value.Landed)
				{
					value.Vel = new Vec3(value.Vel.X, value.Vel.Y, value.Vel.Z - 9.8f * dt);
					value.Pos += value.Vel * dt;
					value.Angle += value.Spin * dt;
					float num2 = ((sink.Ground != null) ? sink.Ground(value.Pos.X, value.Pos.Y) : Ground);
					if (value.Pos.Z < num2 + value.Size * value.Rest && value.Vel.Z < 0f)
					{
						value.Pos = new Vec3(value.Pos.X, value.Pos.Y, num2 + value.Size * value.Rest);
						value.Landed = true;
						if (parts != null)
						{
							for (int i = 0; i < 5; i++)
							{
								parts.Smoke(value.Pos, new Vec3((PowerFx.Rand(Seed, num * 7 + i) - 0.5f) * 3f, (PowerFx.Rand(Seed, num * 7 + i + 40) - 0.5f) * 3f, 0.6f), 1.4f, 0.8f, 2f, TerrainCache.Rgb(150, 136, 116), TerrainCache.Rgb(184, 176, 160), 140, i, 1.4f);
							}
						}
					}
				}
				float num3 = 1f - BlastFx.Clamp01((t - 20f) / 6f);
				if (num3 <= 0.01f)
				{
					_shards.RemoveAt(num);
				}
				else
				{
					value.Inst.Position = value.Pos;
					value.Inst.Basis = Mat3.Rotation(value.Axis, value.Angle) * value.Size;
					value.Inst.Fade = num3;
					value.Inst.SortDepth = sink.Distance(value.Pos.X, value.Pos.Y);
					sink.AddMesh(value.Inst);
					_shards[num] = value;
				}
			}
		}
	}
}
