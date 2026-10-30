using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class ImpactFx
	{
		private struct Chunk
		{
			public MeshInstance Inst;

			public Vec3 Pos;

			public Vec3 Vel;

			public Vec3 Axis;

			public float Angle;

			public float Spin;

			public float Size;

			public float Trail;

			public bool Landed;
		}

		public readonly float X;

		public readonly float Y;

		public readonly float Ground;

		public readonly float Scale;

		public readonly uint Seed;

		public float Age;

		private readonly uint _land;

		private bool _burst;

		private float _smokeClock;

		private CraterFx _crater;

		private readonly List<Chunk> _chunks = new List<Chunk>();

		public bool Finished
		{
			get
			{
				if (Age > 50f)
				{
					if (_crater != null)
					{
						return _crater.Finished;
					}
					return true;
				}
				return false;
			}
		}

		public ImpactFx(float x, float y, float ground, float scale, uint seed, uint land)
		{
			X = x;
			Y = y;
			Ground = ground;
			Scale = scale;
			Seed = seed;
			_land = land;
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
				Burst(sink, parts);
				sink.Shake = Math.Max(sink.Shake, 1.25f * BlastFx.Clamp01(1f - num / (50f * scale)) + 0.12f * BlastFx.Clamp01(1f - num / (120f * scale)));
			}
			if (age < 0.4f)
			{
				float num2 = 1f - age / 0.4f;
				sink.AddFlash(0.6f * num2 * num2 * BlastFx.Clamp01(1.1f - num / (70f * scale)) * (0.5f + 0.5f * Math.Max(0f, sink.Facing(X, Y))), TerrainCache.Rgb(255, 238, 200));
				float num3 = scale * (10f + 16f * age);
				sink.Sprite(BlastFx.StarSprite, X, Y, Ground + 1.5f * scale, num3, num3, TerrainCache.Rgb(255, 236, 190), (byte)Math.Max(2, (int)(255f * num2)), emissive: true, additive: true);
			}
			if (age < 0.9f)
			{
				sink.Glow(X, Y, Ground + 1.4f * scale, scale * (12f + 10f * age), TerrainCache.Rgb(255, 200, 110), (1f - age / 0.9f) * 0.85f);
			}
			sink.Light(X, Y, Ground + 2f * scale, 28f * scale, BlastFx.Heat((age < 0.5f) ? 1.1f : 0.8f), 3.2f * (float)Math.Exp((0f - age) / 0.45f) + 0.7f * (float)Math.Exp((0f - age) / 3.5f), (age < 0.6f) ? (4f * scale) : 0f);
			if (age < 1.4f)
			{
				DustRing(sink, age);
			}
			if (parts != null && age > 0.25f && age < 7f && num < 160f)
			{
				_smokeClock += dt * 6f * (1f - age / 7f);
				while (_smokeClock > 1f)
				{
					_smokeClock -= 1f;
					int num4 = parts.List.Count + (int)(age * 733f);
					Vec3 at = new Vec3(X + (BlastFx.Rand(Seed, num4) - 0.5f) * 1.5f * scale, Y + (BlastFx.Rand(Seed, num4 + 1) - 0.5f) * 1.5f * scale, Ground + 0.8f * scale);
					parts.Smoke(at, new Vec3(0f, 0f, 2.6f + BlastFx.Rand(Seed, num4 + 2) * 1.5f), 6f + 3f * BlastFx.Rand(Seed, num4 + 3), 1.6f * scale, 5f * scale, TerrainCache.Rgb(64, 54, 52), BlastFx.Smoke(0.65f), 190, (int)(BlastFx.Rand(Seed, num4 + 4) * 6f), 0.35f);
				}
			}
			float num5 = (float)Math.Exp((0f - age) / 9f);
			for (int i = 0; i < _chunks.Count; i++)
			{
				Chunk value = _chunks[i];
				if (!value.Landed)
				{
					value.Vel = new Vec3(value.Vel.X, value.Vel.Y, value.Vel.Z - 9.8f * dt);
					value.Pos += value.Vel * dt;
					value.Angle += value.Spin * dt;
					float num6 = ((sink.Ground != null) ? sink.Ground(value.Pos.X, value.Pos.Y) : Ground);
					if (value.Pos.Z < num6 + value.Size * 0.25f && value.Vel.Z < 0f)
					{
						value.Pos = new Vec3(value.Pos.X, value.Pos.Y, num6 + value.Size * 0.22f);
						value.Landed = true;
						if (parts != null)
						{
							for (int j = 0; j < 6; j++)
							{
								parts.Spark(value.Pos, new Vec3((BlastFx.Rand(Seed, i * 9 + j) - 0.5f) * 4f, (BlastFx.Rand(Seed, i * 9 + j + 50) - 0.5f) * 4f, 2f + 3f * BlastFx.Rand(Seed, i * 9 + j + 90)), 0.8f, 0.1f, BlastFx.Heat(1f), BlastFx.Heat(0.5f), 9f, 0.5f);
							}
						}
					}
					else if (parts != null)
					{
						value.Trail += (float)Math.Sqrt(Vec3.Dot(value.Vel, value.Vel)) * dt;
						while (value.Trail > 0.35f)
						{
							value.Trail -= 0.35f;
							parts.Smoke(value.Pos, new Vec3(0f, 0f, 0.4f), 0.9f, value.Size * 0.5f, value.Size * 1.6f, TerrainCache.Rgb(104, 94, 92), TerrainCache.Rgb(160, 154, 150), 110, i & 7, 1.2f);
						}
					}
				}
				MeshInstance inst = value.Inst;
				MeshModel model = inst.Model;
				for (int k = 0; k < model.SlotNames.Length; k++)
				{
					string text = model.SlotNames[k];
					uint[] palette = inst.Palette;
					int num7 = k;
					int num8;
					switch (text)
					{
					default:
						num8 = 0;
						break;
					case "crust":
						num8 = (int)BlastFx.Heat(0.04f + 0.5f * num5);
						break;
					case "crack":
						num8 = (int)BlastFx.Heat(0.08f + 0.8f * num5);
						break;
					case "crack_core":
						num8 = (int)BlastFx.Heat(0.15f + 1f * num5);
						break;
					}
					palette[num7] = (uint)num8;
				}
				inst.Position = value.Pos;
				inst.Basis = Mat3.Rotation(value.Axis, value.Angle) * value.Size;
				inst.Fade = 1f - BlastFx.Clamp01((age - 40f) / 10f);
				inst.SortDepth = sink.Distance(value.Pos.X, value.Pos.Y);
				if (inst.Fade > 0.01f)
				{
					sink.AddMesh(inst);
				}
				_chunks[i] = value;
			}
			if (_crater == null && age > 0.5f)
			{
				_crater = new CraterFx(X, Y, 3.4f * scale, 45f, Seed ^ 0x9E37, big: false);
			}
			_crater?.Step(dt, sink, parts);
		}

		private void Burst(FxSink sink, FxParticles parts)
		{
			float scale = Scale;
			if (parts != null)
			{
				for (int i = 0; i < 26; i++)
				{
					float num = BlastFx.Rand(Seed, i) * 6.283f;
					float num2 = BlastFx.Rand(Seed, i + 1);
					float num3 = scale * (3.5f + 7f * BlastFx.Rand(Seed, i + 2));
					Vec3 vec = new Vec3((float)Math.Cos(num) * num3 * (1f - num2 * 0.5f), (float)Math.Sin(num) * num3 * (1f - num2 * 0.5f), scale * (2f + 7f * num2));
					float num4 = scale * (1.8f + 1.8f * BlastFx.Rand(Seed, i + 3));
					parts.Add(new FxParticles.P
					{
						Pos = new Vec3(X, Y, Ground + 0.8f * scale),
						Vel = vec,
						Life = 0.9f + 0.9f * BlastFx.Rand(Seed, i + 4),
						Size = num4,
						EndSize = num4 * 1.9f,
						Color = BlastFx.Heat(1.15f),
						EndColor = BlastFx.Heat(0.25f),
						Kind = FxParticles.Kind.Fire,
						Variant = i,
						Drag = 2.6f,
						Gravity = -1.5f
					});
					parts.Smoke(new Vec3(X, Y, Ground + 1f * scale) + vec * 0.12f, vec * 0.25f + new Vec3(0f, 0f, 1.2f), 4.5f + 2f * BlastFx.Rand(Seed, i + 5), num4 * 1.2f, num4 * 3f, TerrainCache.Rgb(76, 60, 56), BlastFx.Smoke(0.65f), 200, i + 3, 0.9f);
				}
				for (int j = 0; j < 70; j++)
				{
					float num5 = BlastFx.Rand(Seed, j + 100) * 6.283f;
					float num6 = scale * (5f + 13f * BlastFx.Rand(Seed, j + 101));
					parts.Spark(new Vec3(X, Y, Ground + 0.6f * scale), new Vec3((float)Math.Cos(num5) * num6, (float)Math.Sin(num5) * num6, scale * (4f + 11f * BlastFx.Rand(Seed, j + 102))), 1f + 1.4f * BlastFx.Rand(Seed, j + 103), 0.13f + 0.12f * BlastFx.Rand(Seed, j + 104), BlastFx.Heat(1.15f), BlastFx.Heat(0.4f), 9.8f);
				}
				uint num7 = TerrainCache.Lerp(TerrainCache.Rgb(96, 74, 54), _land, 0.4f);
				for (int k = 0; k < 30; k++)
				{
					float num8 = BlastFx.Rand(Seed, k + 200) * 6.283f;
					float num9 = scale * (3f + 7f * BlastFx.Rand(Seed, k + 201));
					parts.Add(new FxParticles.P
					{
						Pos = new Vec3(X, Y, Ground + 0.4f),
						Vel = new Vec3((float)Math.Cos(num8) * num9, (float)Math.Sin(num8) * num9, scale * (4f + 6f * BlastFx.Rand(Seed, k + 202))),
						Life = 3f,
						Size = 0.22f * scale,
						EndSize = 0.2f * scale,
						Gravity = 9.8f,
						Drag = 0.2f,
						Color = num7,
						EndColor = num7,
						Kind = FxParticles.Kind.Drop,
						Matte = true
					});
				}
			}
			if (sink.Model == null)
			{
				return;
			}
			string[] array = new string[3] { "meteor_chunk_a", "meteor_chunk_b", "meteor_chunk_c" };
			for (int l = 0; l < 13; l++)
			{
				MeshModel meshModel = sink.Model(array[l % 3]);
				if (meshModel != null)
				{
					float num10 = ((float)l + BlastFx.Rand(Seed, l + 300) * 0.7f) / 13f * 6.283f;
					float num11 = scale * (3.5f + 6f * BlastFx.Rand(Seed, l + 301));
					_chunks.Add(new Chunk
					{
						Inst = new MeshInstance
						{
							Model = meshModel,
							Palette = new uint[meshModel.SlotNames.Length],
							DissolveScale = 0.25f
						},
						Pos = new Vec3(X, Y, Ground + 0.6f * scale),
						Vel = new Vec3((float)Math.Cos(num10) * num11, (float)Math.Sin(num10) * num11, scale * (5f + 7f * BlastFx.Rand(Seed, l + 302))),
						Axis = new Vec3(BlastFx.Rand(Seed, l + 303) - 0.5f, BlastFx.Rand(Seed, l + 304) - 0.5f, BlastFx.Rand(Seed, l + 305) - 0.5f).Normalized(),
						Spin = 4f + 8f * BlastFx.Rand(Seed, l + 306),
						Size = scale * (0.35f + 0.55f * BlastFx.Rand(Seed, l + 307))
					});
					continue;
				}
				break;
			}
		}

		private void DustRing(FxSink sink, float t)
		{
			float scale = Scale;
			float num = scale * 11f * (1f - (float)Math.Exp((0f - t) / 0.35f));
			int num2 = (int)(190f * (1f - t / 1.4f));
			if (num2 >= 3)
			{
				uint multiply = TerrainCache.Lerp(TerrainCache.Rgb(160, 140, 112), _land, 0.35f);
				for (int i = 0; i < 26; i++)
				{
					float num3 = ((float)i + BlastFx.Rand(Seed, i + 400) * 0.5f) / 26f * 6.283f;
					float num4 = X + (float)Math.Cos(num3) * num;
					float num5 = Y + (float)Math.Sin(num3) * num;
					float num6 = ((sink.Ground != null) ? sink.Ground(num4, num5) : Ground);
					float num7 = scale * (1.1f + num * 0.12f) * (0.8f + 0.4f * BlastFx.Rand(Seed, i + 401));
					sink.Sprite(BlastFx.Soft(i), num4, num5, num6 + num7 * 0.34f, num7 * 1.5f, num7, multiply, (byte)num2, emissive: false, additive: false, (i & 1) != 0);
				}
			}
		}
	}
}
