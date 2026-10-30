using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class CraterFx
	{
		public readonly float X;

		public readonly float Y;

		public readonly float Radius;

		public readonly float Life;

		public readonly uint Seed;

		public float Age;

		private readonly bool _big;

		private readonly MeshInstance _mesh = new MeshInstance
		{
			NoShadow = true,
			DissolveScale = 1.4f
		};

		private readonly List<MeshInstance> _rubble = new List<MeshInstance>();

		private float _built = float.NaN;

		private float _check;

		private float _smokeClock;

		private float _flameClock;

		private int _core;

		private int _crack;

		private int _edge;

		private int _pool;

		private int _crust;

		private readonly uint[] _pal = new uint[5];

		public bool Finished => Age > Life;

		public float Heat => (float)Math.Exp((0f - Age) / (Life * 0.3f));

		public CraterFx(float x, float y, float radius, float life, uint seed, bool big)
		{
			X = x;
			Y = y;
			Radius = radius;
			Life = life;
			Seed = seed;
			_big = big;
		}

		private void Build(FxSink sink)
		{
			Func<float, float, float> ground = sink.Ground ?? ((Func<float, float, float>)((float x, float y) => 0f));
			float g0 = ground(X, Y);
			_built = g0;
			MeshBuilder meshBuilder = new MeshBuilder("crater");
			_core = meshBuilder.Slot("core", 0, BlastFx.Heat(1f), 1);
			_crack = meshBuilder.Slot("crack", 0, BlastFx.Heat(0.8f), 1);
			_edge = meshBuilder.Slot("edge", 0, BlastFx.Heat(0.55f), 1);
			_pool = meshBuilder.Slot("pool", 11, BlastFx.Heat(0.9f), 1);
			_crust = meshBuilder.Slot("crust", 14, TerrainCache.Rgb(40, 34, 32), 0);
			meshBuilder.Part("crater", -1, new Vec3(0f, 0f, 0f));
			float num = Radius * (_big ? 0.24f : 0.34f);
			float[] array = new float[5] { 0f, 0.45f, 0.8f, 1f, 1.18f };
			int[] array2 = new int[5] { _pool, _pool, _pool, _edge, _crust };
			for (int num2 = 0; num2 < array.Length - 1; num2++)
			{
				for (int num3 = 0; num3 < 18; num3++)
				{
					float num4 = (float)num3 * 6.283f / 18f;
					float num5 = (float)(num3 + 1) * 6.283f / 18f;
					float num6 = 1f + 0.12f * (float)Math.Sin(num4 * 3f + (float)Seed);
					float num7 = 1f + 0.12f * (float)Math.Sin(num5 * 3f + (float)Seed);
					float num8 = array[num2] * num;
					float num9 = array[num2 + 1] * num;
					Vec3 vec = At((float)Math.Cos(num4) * num8 * num6, (float)Math.Sin(num4) * num8 * num6, 0.08f);
					Vec3 vec2 = At((float)Math.Cos(num5) * num8 * num7, (float)Math.Sin(num5) * num8 * num7, 0.08f);
					Vec3 vec3 = At((float)Math.Cos(num4) * num9 * num6, (float)Math.Sin(num4) * num9 * num6, (num2 + 1 == array.Length - 1) ? 0.18f : 0.08f);
					Vec3 vec4 = At((float)Math.Cos(num5) * num9 * num7, (float)Math.Sin(num5) * num9 * num7, (num2 + 1 == array.Length - 1) ? 0.18f : 0.08f);
					if (num2 == 0)
					{
						meshBuilder.Face(array2[num2], vec, vec3, vec4);
					}
					else
					{
						meshBuilder.Face(array2[num2], vec, vec3, vec4, vec2);
					}
				}
			}
			Random random = new Random((int)Seed);
			int num10 = (_big ? 12 : 7);
			float num11 = (_big ? 0.5f : 0.24f);
			List<(float, float, float, float, float)> list = new List<(float, float, float, float, float)>();
			for (int num12 = 0; num12 < num10; num12++)
			{
				float num13 = ((float)num12 + (float)random.NextDouble() * 0.7f) / (float)num10 * 6.283f;
				list.Add(((float)Math.Cos(num13) * num * 0.9f, (float)Math.Sin(num13) * num * 0.9f, num13, Radius * (0.55f + 0.5f * (float)random.NextDouble()), num11 * (0.7f + 0.5f * (float)random.NextDouble())));
			}
			int num14 = 0;
			while (list.Count > 0 && num14 < 900)
			{
				(float, float, float, float, float) tuple = list[list.Count - 1];
				list.RemoveAt(list.Count - 1);
				float num15 = tuple.Item1;
				float num16 = tuple.Item2;
				float num17 = tuple.Item3;
				float num18 = 0f;
				for (float num19 = (_big ? 0.9f : 0.45f); num18 < tuple.Item4; num18 += num19)
				{
					if (num14 >= 900)
					{
						break;
					}
					float num20 = tuple.Item5 * (float)Math.Pow(1f - num18 / tuple.Item4, 0.699999988079071);
					num17 += ((float)random.NextDouble() - 0.5f) * 0.9f;
					float num21 = num15 + (float)Math.Cos(num17) * num19;
					float num22 = num16 + (float)Math.Sin(num17) * num19;
					float w = tuple.Item5 * (float)Math.Pow(Math.Max(0f, 1f - (num18 + num19) / tuple.Item4), 0.699999988079071);
					Ribbon(meshBuilder, At, num15, num16, num21, num22, num20, w);
					num14++;
					if (random.NextDouble() < 0.16 && tuple.Item5 > 0.08f)
					{
						list.Add((num21, num22, num17 + ((random.NextDouble() < 0.5) ? (-1f) : 1f) * (0.5f + (float)random.NextDouble() * 0.6f), (tuple.Item4 - num18) * 0.6f, num20 * 0.65f));
					}
					num15 = num21;
					num16 = num22;
				}
			}
			_mesh.Model = meshBuilder.Build();
			_mesh.Position = new Vec3(X, Y, g0);
			_mesh.Basis = Mat3.Identity;
			_mesh.Static = false;
			_rubble.Clear();
			if (!_big || sink.Model == null)
			{
				return;
			}
			string[] array3 = new string[3] { "meteor_chunk_a", "meteor_chunk_b", "meteor_chunk_c" };
			for (int num23 = 0; num23 < 16; num23++)
			{
				MeshModel meshModel = sink.Model(array3[num23 % 3]);
				if (meshModel != null)
				{
					float num24 = ((float)num23 + (float)random.NextDouble() * 0.6f) / 16f * 6.283f;
					float num25 = num * (1.3f + 1.4f * (float)random.NextDouble());
					float num26 = X + (float)Math.Cos(num24) * num25;
					float num27 = Y + (float)Math.Sin(num24) * num25;
					float num28 = 0.8f + 1.6f * (float)random.NextDouble();
					MeshInstance meshInstance = new MeshInstance
					{
						Model = meshModel,
						NoShadow = false,
						Palette = new uint[meshModel.SlotNames.Length],
						DissolveScale = 0.25f
					};
					meshInstance.Place(num26, num27, ground(num26, num27) + num28 * 0.12f, num24 * 1.7f, num28, num28, num28 * 0.8f);
					_rubble.Add(meshInstance);
					continue;
				}
				break;
			}
			Vec3 At(float dx, float dy, float lift)
			{
				return new Vec3(dx, dy, ground(X + dx, Y + dy) - g0 + lift);
			}
		}

		private static void Ribbon(MeshBuilder b, Func<float, float, float, Vec3> at, float x0, float y0, float x1, float y1, float w0, float w1)
		{
			float num = x1 - x0;
			float num2 = y1 - y0;
			float num3 = (float)Math.Sqrt(num * num + num2 * num2);
			if (!(num3 < 0.0001f))
			{
				float num4 = (0f - num2) / num3;
				float num5 = num / num3;
				b.Face(2, at(x0 - num4 * w0, y0 - num5 * w0, 0.05f), at(x1 - num4 * w1, y1 - num5 * w1, 0.05f), at(x1 + num4 * w1, y1 + num5 * w1, 0.05f), at(x0 + num4 * w0, y0 + num5 * w0, 0.05f));
				float num6 = w0 * 0.4f;
				float num7 = w1 * 0.4f;
				b.Face(1, at(x0 - num4 * num6, y0 - num5 * num6, 0.07f), at(x1 - num4 * num7, y1 - num5 * num7, 0.07f), at(x1 + num4 * num7, y1 + num5 * num7, 0.07f), at(x0 + num4 * num6, y0 + num5 * num6, 0.07f));
				float num8 = w0 * 0.14f;
				float num9 = w1 * 0.14f;
				if (num8 > 0.02f)
				{
					b.Face(0, at(x0 - num4 * num8, y0 - num5 * num8, 0.085f), at(x1 - num4 * num9, y1 - num5 * num9, 0.085f), at(x1 + num4 * num9, y1 + num5 * num9, 0.085f), at(x0 + num4 * num8, y0 + num5 * num8, 0.085f));
				}
			}
		}

		public void Step(float dt, FxSink sink, FxParticles parts)
		{
			Age += dt;
			if (Finished)
			{
				return;
			}
			_check -= dt;
			if (float.IsNaN(_built) || (_check <= 0f && sink.Ground != null && Math.Abs(sink.Ground(X, Y) - _built) > 0.3f))
			{
				Build(sink);
			}
			if (_check <= 0f)
			{
				_check = 2f;
			}
			float heat = Heat;
			float num = 0.9f + 0.1f * (float)Math.Sin(Age * 7.3f + (float)Seed) * (float)Math.Sin(Age * 3.1f);
			_pal[_core] = BlastFx.Heat(0.15f + 1f * heat * num);
			_pal[_crack] = BlastFx.Heat(0.08f + 0.82f * heat);
			_pal[_edge] = BlastFx.Heat(0.04f + 0.6f * heat);
			_pal[_pool] = BlastFx.Heat(0.2f + 0.85f * heat * num);
			_pal[_crust] = TerrainCache.Rgb(40, 34, 32);
			_mesh.Palette = _pal;
			float num2 = 1f - BlastFx.Clamp01((Age - Life * 0.8f) / (Life * 0.2f));
			_mesh.Fade = num2;
			float num3 = sink.Distance(X, Y);
			_mesh.SortDepth = num3;
			sink.AddMesh(_mesh);
			for (int i = 0; i < _rubble.Count; i++)
			{
				MeshInstance meshInstance = _rubble[i];
				MeshModel model = meshInstance.Model;
				for (int j = 0; j < model.SlotNames.Length; j++)
				{
					string text = model.SlotNames[j];
					uint[] palette = meshInstance.Palette;
					int num4 = j;
					uint num5;
					switch (text)
					{
					default:
						num5 = TerrainCache.Rgb(34, 30, 30);
						break;
					case "crust":
						num5 = BlastFx.Heat(0.02f + 0.4f * heat);
						break;
					case "crack":
						num5 = BlastFx.Heat(0.05f + 0.7f * heat);
						break;
					case "crack_core":
						num5 = BlastFx.Heat(0.1f + 0.9f * heat);
						break;
					}
					palette[num4] = num5;
				}
				meshInstance.Fade = num2;
				meshInstance.SortDepth = num3;
				sink.AddMesh(meshInstance);
			}
			float num6 = Radius * (_big ? 0.24f : 0.34f);
			sink.Light(X, Y, _built + 1.2f + num6 * 0.3f, 6f + Radius * 1.3f, BlastFx.Heat(0.75f), 1.7f * heat * num * num2);
			if (heat > 0.5f)
			{
				sink.Glow(X, Y, _built + 0.5f, num6 * 3f, BlastFx.Heat(0.8f), (heat - 0.5f) * 0.6f);
			}
			if (parts == null || num3 > 140f)
			{
				return;
			}
			_smokeClock += dt * (_big ? 9f : 3f) * heat;
			while (_smokeClock > 1f)
			{
				_smokeClock -= 1f;
				int num7 = parts.List.Count + (int)(Age * 331f);
				float num8 = BlastFx.Rand(Seed, num7) * 6.283f;
				float num9 = num6 + (Radius - num6) * BlastFx.Rand(Seed, num7 + 1) * 0.8f;
				float num10 = X + (float)Math.Cos(num8) * num9;
				float num11 = Y + (float)Math.Sin(num8) * num9;
				float num12 = ((sink.Ground != null) ? sink.Ground(num10, num11) : _built);
				parts.Smoke(new Vec3(num10, num11, num12 + 0.3f), new Vec3(0f, 0f, 0.9f + BlastFx.Rand(Seed, num7 + 2)), 4f + 3f * BlastFx.Rand(Seed, num7 + 3), 0.6f, 2.4f, TerrainCache.Rgb(70, 62, 60), BlastFx.Smoke(0.6f), 140, (int)(BlastFx.Rand(Seed, num7 + 4) * 6f), 0.4f);
			}
			_flameClock += dt * (_big ? 10f : 4f) * heat;
			while (_flameClock > 1f)
			{
				_flameClock -= 1f;
				int num13 = parts.List.Count * 7 + (int)(Age * 211f);
				float num14 = BlastFx.Rand(Seed, num13) * 6.283f;
				float num15 = (float)Math.Sqrt(BlastFx.Rand(Seed, num13 + 1)) * num6;
				float x = X + (float)Math.Cos(num14) * num15;
				float y = Y + (float)Math.Sin(num14) * num15;
				float num16 = (0.5f + 0.6f * BlastFx.Rand(Seed, num13 + 2)) * (_big ? 1.6f : 0.8f);
				parts.Add(new FxParticles.P
				{
					Pos = new Vec3(x, y, _built + 0.2f + num16 * 0.3f),
					Vel = new Vec3(0f, 0f, 1.5f + BlastFx.Rand(Seed, num13 + 3)),
					Life = 0.7f + 0.5f * BlastFx.Rand(Seed, num13 + 4),
					Size = num16,
					EndSize = num16 * 0.4f,
					Color = BlastFx.Heat(0.95f),
					EndColor = BlastFx.Heat(0.35f),
					Kind = FxParticles.Kind.Fire,
					Variant = (num13 & 7),
					Drag = 1f,
					Gravity = -1f
				});
				if ((num13 & 3) == 0)
				{
					parts.Spark(new Vec3(x, y, _built + 0.4f), new Vec3((BlastFx.Rand(Seed, num13 + 5) - 0.5f) * 2f, (BlastFx.Rand(Seed, num13 + 6) - 0.5f) * 2f, 3f + 3f * BlastFx.Rand(Seed, num13 + 7)), 1.2f, 0.1f, BlastFx.Heat(1f), BlastFx.Heat(0.5f), 3f, 0.6f);
				}
			}
		}
	}
}
