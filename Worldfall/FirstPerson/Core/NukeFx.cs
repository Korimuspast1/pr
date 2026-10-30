using System;

namespace FirstPerson.Core
{
	public sealed class NukeFx
	{
		public readonly float X;

		public readonly float Y;

		public readonly float Ground;

		public readonly float Scale;

		public readonly uint Seed;

		public float Age;

		public const float Life = 80f;

		private readonly uint _land;

		private readonly MeshInstance _ball = new MeshInstance
		{
			NoShadow = true,
			DissolveScale = 0.28f
		};

		private readonly MeshInstance _cap = new MeshInstance
		{
			NoShadow = true,
			DissolveScale = 0.2f
		};

		private readonly MeshInstance _stem = new MeshInstance
		{
			NoShadow = true,
			DissolveScale = 0.07f
		};

		private readonly MeshInstance _skirt = new MeshInstance
		{
			NoShadow = true,
			DissolveScale = 0.18f
		};

		private BlastFx.CloudSlots _ballS;

		private BlastFx.CloudSlots _capS;

		private BlastFx.CloudSlots _capFarS;

		private BlastFx.CloudSlots _stemS;

		private BlastFx.CloudSlots _skirtS;

		private CraterFx _crater;

		private float _ring;

		private float _emberClock;

		private float _smokeClock;

		private float _stemSmokeClock;

		private bool _threw;

		private static readonly float[] HeatT = new float[10] { 0f, 1f, 3f, 6f, 10f, 15f, 22f, 30f, 40f, 60f };

		private static readonly float[] HeatV = new float[10] { 1.3f, 1.12f, 0.98f, 0.84f, 0.68f, 0.52f, 0.37f, 0.24f, 0.12f, 0f };

		private const float CapStart = 0.7f;

		private const float WindX = 0.83f;

		private const float WindY = 0.55f;

		public bool Finished
		{
			get
			{
				if (Age > 80f)
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

		public CraterFx Crater => _crater;

		public NukeFx(float x, float y, float ground, float scale, uint seed, uint land)
		{
			X = x;
			Y = y;
			Ground = ground;
			Scale = scale;
			Seed = seed;
			_land = land;
		}

		public static float HeatAt(float t)
		{
			if (t <= HeatT[0])
			{
				return HeatV[0];
			}
			for (int i = 1; i < HeatT.Length; i++)
			{
				if (t <= HeatT[i])
				{
					return HeatV[i - 1] + (HeatV[i] - HeatV[i - 1]) * (t - HeatT[i - 1]) / (HeatT[i] - HeatT[i - 1]);
				}
			}
			return 0f;
		}

		private void Cap(float t, out float belly, out float radius)
		{
			float num = Math.Max(0f, t - 0.7f);
			belly = Scale * (9f + 35f * (1f - (float)Math.Exp((0f - num) / 4.5f)) + 0.22f * Math.Max(0f, t - 20f));
			radius = Scale * (6f + 9.5f * (1f - (float)Math.Exp((0f - num) / 3f)) + 0.11f * Math.Max(0f, t - 12f));
		}

		private float Drift(float t, float height)
		{
			return 0.5f * Math.Max(0f, t - 12f) * (0.3f + height / (50f * Scale));
		}

		public void Step(float dt, FxSink sink, FxParticles parts)
		{
			Age += dt;
			float age = Age;
			float num = sink.Distance(X, Y);
			float scale = Scale;
			float num2 = HeatAt(age);
			uint seed = Seed;
			Func<string, MeshModel> model = sink.Model;
			if (age < 0.9f)
			{
				float num3 = 1f - age / 0.9f;
				float num4 = BlastFx.Clamp01(1.25f - num / (260f * scale));
				sink.AddFlash(num3 * num3 * num4 * (0.62f + 0.38f * Math.Max(0f, sink.Facing(X, Y))) * 0.97f, TerrainCache.Rgb(255, 250, 236));
			}
			if (age < 0.8f)
			{
				float num5 = 1f - age / 0.8f;
				float z = Ground + 3.5f * scale + age * 6f * scale;
				float num6 = scale * (30f + 46f * age);
				sink.Sprite(BlastFx.StarSprite, X, Y, z, num6, num6, TerrainCache.Rgb(255, 252, 240), (byte)Math.Max(2, (int)(255.0 * Math.Pow(num5, 1.399999976158142))), emissive: true, additive: true);
				sink.Glow(X, Y, z, scale * (60f + 30f * age), TerrainCache.Rgb(255, 236, 190), num5 * 0.9f);
			}
			float ring = _ring;
			_ring = scale * 50f * (1f - (float)Math.Exp((0f - age) / 1.25f));
			if (age > 0.12f && age < 5f)
			{
				ShockRing(sink, age, _ring);
			}
			if (num < scale * 52f && ring < num && _ring >= num)
			{
				sink.Shake = Math.Max(sink.Shake, 0.35f + 1.3f * (1f - num / (scale * 52f)));
			}
			if (age < 6f)
			{
				sink.Shake = Math.Max(sink.Shake, 0.55f * (1f - age / 6f) * BlastFx.Clamp01(1f - num / (170f * scale)));
			}
			float num7 = scale * 9f * (1f - (float)Math.Exp((0f - age) / 0.22f)) * (1f + 0.08f * age);
			float num8 = Ground + num7 * 0.45f + age * 3.2f * scale;
			if (age < 2.7f && model != null)
			{
				MeshModel meshModel = model("fx_fireball");
				if (meshModel != null)
				{
					if (_ballS == null)
					{
						_ballS = BlastFx.CloudSlots.Of(meshModel);
					}
					_ballS.Fire(num2 + 0.32f, 0f, 0f);
					_ball.Model = meshModel;
					_ball.Palette = _ballS.Palette;
					_ball.Emissive = true;
					_ball.Fade = ((age < 1.5f) ? 1f : Math.Max(0f, 1f - (age - 1.5f) / 1.2f));
					_ball.Position = new Vec3(X, Y, num8);
					_ball.Basis = BlastFx.AlongAxis(new Vec3(0f, 0f, 1f), age * 0.6f + (float)seed) * num7;
					_ball.SortDepth = num;
					sink.AddMesh(_ball);
				}
			}
			if (age < 3f)
			{
				sink.Glow(X, Y, num8, num7 * 3.2f, BlastFx.Heat(num2 + 0.2f), 0.75f * (1f - age / 3f));
			}
			Cap(age, out var belly, out var radius);
			float num9 = 1f - 0.3f * BlastFx.Clamp01((age - 15f) / 30f);
			float num10 = radius * 0.92f * num9;
			float num11 = Drift(age, belly);
			float num12 = X + 0.83f * num11;
			float num13 = Y + 0.55f * num11;
			float num14 = Ground + belly;
			bool flag = num2 > 0.33f;
			float num15 = BlastFx.Clamp01((age - 3f) / 22f);
			float num16 = 0.7f * BlastFx.Smooth((age - 6f) / 6f) * (1f - BlastFx.Smooth((age - 24f) / 22f));
			if (age > 0.7f && model != null)
			{
				MeshModel meshModel2 = ((num < 110f * scale) ? model("fx_mush_cap") : (model("fx_mush_cap_f") ?? model("fx_mush_cap")));
				if (meshModel2 != null)
				{
					BlastFx.CloudSlots cloudSlots = (meshModel2.Name.EndsWith("_f", StringComparison.Ordinal) ? (_capFarS ?? (_capFarS = BlastFx.CloudSlots.Of(meshModel2))) : (_capS ?? (_capS = BlastFx.CloudSlots.Of(meshModel2))));
					if (flag)
					{
						cloudSlots.Fire(num2, num15, num16);
					}
					else
					{
						cloudSlots.Smoked(BlastFx.Clamp01(num2 / 0.33f) * 0.7f + num16 * 0.4f);
					}
					_cap.Model = meshModel2;
					_cap.Palette = cloudSlots.Palette;
					_cap.Emissive = flag;
					float num17 = BlastFx.Clamp01((age - 0.7f) / 0.5f);
					float num18 = 1f - BlastFx.Clamp01((age - 32f) / 22f);
					_cap.Fade = num17 * num18;
					if (_cap.Fade > 0.01f)
					{
						_cap.Position = new Vec3(num12, num13, num14);
						Mat3 mat = Mat3.RotZ(age * 0.035f + (float)seed * 0.001f);
						_cap.Basis = new Mat3(mat.C0 * radius, mat.C1 * radius, new Vec3(0f, 0f, num10 / 0.92f));
						_cap.SortDepth = num;
						sink.AddMesh(_cap);
					}
				}
			}
			if (age > 0.3f && model != null)
			{
				MeshModel meshModel3 = model("fx_mush_stem");
				if (meshModel3 != null)
				{
					if (_stemS == null)
					{
						_stemS = BlastFx.CloudSlots.Of(meshModel3);
					}
					float num19 = num2 + 0.06f;
					if (num19 > 0.33f)
					{
						_stemS.Fire(num19, num15 * 0.8f, 0f);
					}
					else
					{
						_stemS.Smoked(BlastFx.Clamp01(num19 / 0.33f) * 0.6f);
					}
					_stem.Model = meshModel3;
					_stem.Palette = _stemS.Palette;
					_stem.Emissive = num19 > 0.33f;
					float num20 = ((age < 1.1f) ? Math.Max(num8 - Ground, belly * BlastFx.Smooth((age - 0.3f) / 1.1f)) : (belly + num10 * 0.25f));
					float num21 = scale * (1.5f + 2.6f * BlastFx.Clamp01(age / 3f)) * (1f - 0.45f * BlastFx.Clamp01((age - 14f) / 20f));
					_stem.Fade = BlastFx.Clamp01((age - 0.3f) / 0.4f) * (1f - BlastFx.Clamp01((age - 17f) / 16f));
					if (_stem.Fade > 0.01f && num20 > 1f)
					{
						float num22 = num21 / 0.12f;
						_stem.Position = new Vec3(X, Y, Ground - 0.6f);
						_stem.Basis = new Mat3(new Vec3(num22, 0f, 0f), new Vec3(0f, num22, 0f), new Vec3(num12 - X, num13 - Y, num20 + 0.6f));
						_stem.SortDepth = num;
						sink.AddMesh(_stem);
					}
				}
			}
			if (age > 0.25f && model != null)
			{
				MeshModel meshModel4 = model("fx_mush_skirt");
				if (meshModel4 != null)
				{
					if (_skirtS == null)
					{
						_skirtS = BlastFx.CloudSlots.Of(meshModel4);
					}
					float num23 = BlastFx.Clamp01(1f - age / 4.5f);
					_skirtS.Dust(_land, num23);
					_skirt.Model = meshModel4;
					_skirt.Palette = _skirtS.Palette;
					_skirt.Emissive = num23 > 0.45f;
					float num24 = scale * (5f + 19f * (1f - (float)Math.Exp((0f - (age - 0.25f)) / 2.2f)) + 0.15f * Math.Max(0f, age - 8f));
					_skirt.Fade = BlastFx.Clamp01((age - 0.25f) / 0.5f) * (1f - BlastFx.Clamp01((age - 2.2f) / 1.6f));
					if (_skirt.Fade > 0.01f)
					{
						float z2 = Math.Min(scale * 22f, num24 * 0.85f);
						Mat3 mat2 = Mat3.RotZ((0f - age) * 0.05f);
						_skirt.Position = new Vec3(X, Y, Ground - 0.4f);
						_skirt.Basis = new Mat3(mat2.C0 * num24, mat2.C1 * num24, new Vec3(0f, 0f, z2));
						_skirt.SortDepth = num;
						sink.AddMesh(_skirt);
					}
				}
			}
			if (age > 0.9f && age < 30f)
			{
				RollingPuffs(sink, age, num12, num13, num14, radius, num10, num2, num15, num);
			}
			if (age > 0.8f && age < 30f)
			{
				BaseSurge(sink, age, num);
			}
			if (age > 1.5f && age < 26f)
			{
				float strength = (1f - BlastFx.Smooth((age - 10f) / 16f)) * 0.5f;
				sink.Glow(num12, num13, num14 + num10 * 0.05f, radius * 1.6f, BlastFx.Heat(num2 + 0.1f), strength);
			}
			if (num16 > 0.02f)
			{
				EdgeGlow(sink, age, num12, num13, num14, radius, num10, num16, num);
			}
			float strength2 = ((age < 0.25f) ? 4.5f : (4.5f * (float)Math.Exp((0f - (age - 0.25f)) / 1.4f) + 1.3f * (float)Math.Exp((0f - age) / 11f)));
			float z3 = ((age < 1.7f) ? num8 : (num14 + num10 * 0.3f));
			sink.Light(num12, num13, z3, scale * 85f, BlastFx.Heat(num2 + 0.25f), strength2, (age < 3f) ? (scale * 10f * (1f - age / 3f)) : 0f);
			if (parts != null)
			{
				if (!_threw)
				{
					_threw = true;
					Debris(parts);
				}
				if (num < 220f * scale)
				{
					if (age > 2f && age < 30f)
					{
						_emberClock += dt * (24f * scale) * (1f - age / 30f);
						while (_emberClock > 1f)
						{
							_emberClock -= 1f;
							int num25 = (int)(age * 1000f) + parts.List.Count;
							float num26 = BlastFx.Rand(seed, num25) * 6.283f;
							float num27 = (float)Math.Sqrt(BlastFx.Rand(seed, num25 + 1)) * radius * 0.85f;
							Vec3 pos = new Vec3(num12 + (float)Math.Cos(num26) * num27, num13 + (float)Math.Sin(num26) * num27, num14 + num10 * 0.1f);
							parts.Add(new FxParticles.P
							{
								Pos = pos,
								Vel = new Vec3((BlastFx.Rand(seed, num25 + 2) - 0.5f) * 3f, (BlastFx.Rand(seed, num25 + 3) - 0.5f) * 3f, -2f - BlastFx.Rand(seed, num25 + 4) * 3f),
								Life = 5f + BlastFx.Rand(seed, num25 + 5) * 5f,
								Size = 0.9f * scale,
								EndSize = 0.4f * scale,
								Gravity = 1.2f,
								Drag = 0.25f,
								Color = BlastFx.Heat(0.8f),
								EndColor = BlastFx.Heat(0.32f),
								Kind = FxParticles.Kind.Ember
							});
						}
					}
					if (age > 26f && age < 54f)
					{
						_smokeClock += dt * 3.2f * scale;
						while (_smokeClock > 1f)
						{
							_smokeClock -= 1f;
							int num28 = (int)(age * 977f) + parts.List.Count * 3;
							float num29 = BlastFx.Rand(seed, num28) * 6.283f;
							float num30 = (0.3f + 0.7f * BlastFx.Rand(seed, num28 + 1)) * radius * 0.9f;
							Vec3 at = new Vec3(num12 + (float)Math.Cos(num29) * num30, num13 + (float)Math.Sin(num29) * num30, num14 + num10 * (0.2f + 0.6f * BlastFx.Rand(seed, num28 + 2)));
							float num31 = BlastFx.Clamp01(1f - (age - 26f) / 14f);
							uint color = TerrainCache.Lerp(BlastFx.Smoke(0.4f + 0.3f * BlastFx.Rand(seed, num28 + 3)), TerrainCache.Rgb(120, 72, 58), num31 * 0.6f);
							parts.Smoke(at, new Vec3(0.996f, 0.66f, 0.25f), 13f + BlastFx.Rand(seed, num28 + 4) * 7f, radius * 0.55f, radius * 0.95f, color, BlastFx.Smoke(0.7f), (byte)(150 + (int)(60f * BlastFx.Rand(seed, num28 + 5))), (int)(BlastFx.Rand(seed, num28 + 6) * 6f), 0.15f);
						}
					}
					if (age > 16f && age < 34f)
					{
						_stemSmokeClock += dt * 2.2f * scale;
						while (_stemSmokeClock > 1f)
						{
							_stemSmokeClock -= 1f;
							int num32 = (int)(age * 613f) + parts.List.Count * 5;
							float num33 = BlastFx.Rand(seed, num32);
							Vec3 at2 = new Vec3(X + (num12 - X) * num33, Y + (num13 - Y) * num33, Ground + belly * num33);
							parts.Smoke(at2, new Vec3(0.83f, 0.55f, 0.4f), 9f + BlastFx.Rand(seed, num32 + 1) * 4f, scale * 4f, scale * 8f, BlastFx.Smoke(0.5f), BlastFx.Smoke(0.75f), 160, (int)(BlastFx.Rand(seed, num32 + 2) * 6f), 0.2f);
						}
					}
				}
			}
			if (_crater == null && age > 1.2f)
			{
				_crater = new CraterFx(X, Y, 12f * scale, 140f, seed ^ 0x5BD1, big: true);
			}
			_crater?.Step(dt, sink, parts);
		}

		private void ShockRing(FxSink sink, float t, float r)
		{
			float scale = Scale;
			float num = BlastFx.Clamp01((t - 0.12f) / 4.88f);
			int num2 = ((sink.Distance(X, Y) < 120f * scale) ? 64 : 36);
			uint c = TerrainCache.Lerp(TerrainCache.Rgb(196, 176, 148), _land, 0.18f);
			int num3 = (int)(215.0 * Math.Pow(1f - num, 1.2000000476837158));
			float num4 = scale * (3.2f + r * 0.11f);
			for (int i = 0; i < num2; i++)
			{
				float num5 = ((float)i + BlastFx.Rand(Seed, i) * 0.6f) / (float)num2 * 6.283f;
				float num6 = r * (0.94f + 0.1f * BlastFx.Rand(Seed, i + 50));
				float num7 = X + (float)Math.Cos(num5) * num6;
				float num8 = Y + (float)Math.Sin(num5) * num6;
				float num9 = ((sink.Ground != null) ? sink.Ground(num7, num8) : Ground);
				float num10 = num4 * (0.75f + 0.5f * BlastFx.Rand(Seed, i + 100));
				if (num3 > 2)
				{
					sink.Sprite(BlastFx.Soft(i), num7, num8, num9 + num10 * 0.36f, num10 * 1.6f, num10, TerrainCache.Scale(c, 0.85f + 0.3f * BlastFx.Rand(Seed, i + 200)), (byte)num3, emissive: false, additive: false, (i & 1) != 0);
				}
				if (t < 0.9f && (i & 1) == 0)
				{
					sink.Glow(num7, num8, num9 + num10 * 0.3f, num10 * 1.6f, TerrainCache.Rgb(255, 220, 160), (1f - t / 0.9f) * 0.55f);
				}
			}
		}

		private void RollingPuffs(FxSink sink, float t, float cx, float cy, float capZ, float capR, float capH, float heat, float soot, float dist)
		{
			int num = ((dist < 90f * Scale) ? 30 : 16);
			float num2 = capR * 0.7f;
			float num3 = capR * 0.36f;
			float num4 = capZ + capH * 0.26f;
			float num5 = BlastFx.Clamp01((t - 0.7f - 0.2f) / 0.6f) * (1f - BlastFx.Clamp01((t - 22f) / 8f));
			bool flag = heat > 0.33f;
			float num6 = t * 0.035f;
			for (int i = 0; i < num; i++)
			{
				float num7 = ((float)i + BlastFx.Rand(Seed, i + 300) * 0.5f) / (float)num * 6.283f + num6;
				float num8 = BlastFx.Rand(Seed, i + 400) * 6.283f - t * 0.32f;
				float num9 = (float)Math.Cos(num8);
				float num10 = (float)Math.Sin(num8);
				if (!(num9 < -0.25f))
				{
					float num11 = num2 + num3 * num9 * 0.82f;
					float x = cx + (float)Math.Cos(num7) * num11;
					float y = cy + (float)Math.Sin(num7) * num11;
					float z = num4 + num3 * num10 * 0.85f;
					float num12 = capR * (0.3f + 0.14f * BlastFx.Rand(Seed, i + 500));
					float num13 = 0.5f + 0.5f * num10;
					uint multiply = (flag ? BlastFx.Heat(heat - 0.1f + 0.22f * num13 - 0.12f * soot) : TerrainCache.Lerp(BlastFx.Smoke(0.3f + 0.6f * num13), TerrainCache.Rgb(120, 70, 56), BlastFx.Clamp01(heat / 0.33f) * 0.6f));
					byte alpha = (byte)((!(num5 >= 0.98f && flag)) ? ((byte)Math.Max(2, (int)(230f * num5))) : 0);
					sink.Sprite(BlastFx.Puff(i + (int)(t * 0.5f)), x, y, z, num12, num12 * 0.85f, multiply, alpha, flag, additive: false, (i & 1) != 0);
				}
			}
		}

		private void BaseSurge(FxSink sink, float t, float dist)
		{
			float scale = Scale;
			int num = ((dist < 110f * scale) ? 40 : 22);
			float num2 = scale * (6f + 19f * (1f - (float)Math.Exp((0f - (t - 0.8f)) / 2.4f)) + 0.12f * Math.Max(0f, t - 8f));
			float num3 = BlastFx.Clamp01((t - 0.8f) / 1.2f) * (1f - BlastFx.Smooth((t - 12f) / 18f));
			uint c = TerrainCache.Lerp(TerrainCache.Rgb(178, 160, 134), _land, 0.2f);
			float num4 = BlastFx.Clamp01(1f - (t - 0.8f) / 7f);
			for (int i = 0; i < num; i++)
			{
				float num5 = ((float)i + BlastFx.Rand(Seed, i + 1200) * 0.8f) / (float)num * 6.283f - t * 0.04f;
				float num6 = BlastFx.Rand(Seed, i + 1201);
				float num7 = num2 * (0.55f + 0.5f * num6);
				float num8 = X + (float)Math.Cos(num5) * num7;
				float num9 = Y + (float)Math.Sin(num5) * num7;
				float num10 = ((sink.Ground != null) ? sink.Ground(num8, num9) : Ground);
				float num11 = scale * (4.5f + 4f * BlastFx.Rand(Seed, i + 1202)) * (0.7f + 0.3f * BlastFx.Clamp01(t / 6f));
				float num12 = (float)Math.Sin(t * 0.7f + (float)i) * 0.3f * scale;
				uint multiply = TerrainCache.Lerp(TerrainCache.Scale(c, 0.8f + 0.3f * num6), BlastFx.Heat(0.6f), num4 * (1f - num6) * 0.8f);
				int num13 = (int)(200f * num3);
				if (num13 > 2)
				{
					sink.Sprite(BlastFx.Soft(i + 3), num8, num9, num10 + num11 * 0.38f + num12, num11 * 1.5f, num11, multiply, (byte)num13, emissive: false, additive: false, (i & 1) != 0);
				}
			}
		}

		private void EdgeGlow(FxSink sink, float t, float cx, float cy, float capZ, float capR, float capH, float rimHeat, float dist)
		{
			int num = ((dist < 90f * Scale) ? 22 : 12);
			for (int i = 0; i < num; i++)
			{
				float num2 = ((float)i + 0.5f) / (float)num * 6.283f + t * 0.035f;
				float num3 = 0.75f + 0.25f * (float)Math.Sin(t * 2.1f + (float)i * 1.7f);
				float num4 = capR * (0.92f + 0.1f * BlastFx.Rand(Seed, i + 700));
				float x = cx + (float)Math.Cos(num2) * num4;
				float y = cy + (float)Math.Sin(num2) * num4;
				float z = capZ + capH * (0.12f + 0.2f * BlastFx.Rand(Seed, i + 800));
				sink.Glow(x, y, z, capR * 0.7f, BlastFx.Heat(0.52f + rimHeat * 0.3f), rimHeat * num3 * 1.1f);
			}
			sink.Glow(cx, cy, capZ, capR * 1.3f, BlastFx.Heat(0.45f + rimHeat * 0.3f), rimHeat * 0.5f);
		}

		private void Debris(FxParticles parts)
		{
			float scale = Scale;
			for (int i = 0; i < 56; i++)
			{
				float num = BlastFx.Rand(Seed, i + 900) * 6.283f;
				float num2 = scale * (10f + 22f * BlastFx.Rand(Seed, i + 901));
				float z = scale * (6f + 16f * BlastFx.Rand(Seed, i + 902));
				bool flag = i % 3 != 0;
				bool trail = i % 2 == 0;
				parts.Add(new FxParticles.P
				{
					Pos = new Vec3(X, Y, Ground + 1f),
					Vel = new Vec3((float)Math.Cos(num) * num2, (float)Math.Sin(num) * num2, z),
					Life = 2.5f + 2.5f * BlastFx.Rand(Seed, i + 903),
					Size = 0.35f * scale,
					EndSize = 0.25f * scale,
					Gravity = 9.8f,
					Drag = 0.15f,
					Color = (flag ? BlastFx.Heat(1f) : TerrainCache.Rgb(48, 40, 36)),
					EndColor = (flag ? BlastFx.Heat(0.35f) : TerrainCache.Rgb(40, 34, 30)),
					Kind = FxParticles.Kind.Drop,
					Trail = trail,
					Matte = !flag
				});
			}
			for (int j = 0; j < 70; j++)
			{
				float num3 = BlastFx.Rand(Seed, j + 1000) * 6.283f;
				float num4 = scale * (6f + 18f * BlastFx.Rand(Seed, j + 1001));
				parts.Spark(new Vec3(X, Y, Ground + 2f * scale), new Vec3((float)Math.Cos(num3) * num4, (float)Math.Sin(num3) * num4, scale * (4f + 14f * BlastFx.Rand(Seed, j + 1002))), 1.5f + 1.5f * BlastFx.Rand(Seed, j + 1003), 0.3f * scale, BlastFx.Heat(1.1f), BlastFx.Heat(0.45f), 9f, 0.5f);
			}
		}
	}
}
