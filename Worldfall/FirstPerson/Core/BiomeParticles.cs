using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class BiomeParticles
	{
		private struct P
		{
			public float X;

			public float Y;

			public float Z;

			public float Vx;

			public float Vy;

			public float Vz;

			public float Age;

			public float Life;

			public float Seed;

			public float Phase;

			public float Size;

			public float Presence;

			public float Retry;

			public float Ax;

			public float Ay;

			public float Az;

			public float Bx;

			public float T1x;

			public float T1y;

			public float T1z;

			public float T2x;

			public float T2y;

			public float T2z;

			public float TrailClock;

			public uint Color;

			public int Well;

			public bool Live;
		}

		private sealed class Pool
		{
			public MoteRecipe R;

			public P[] Ps;

			public float Fade;

			public float Absent;
		}

		private sealed class Features
		{
			private sealed class Devil
			{
				public float X;

				public float Y;

				public float Heading;

				public float Age;

				public float Life;

				public float Height;

				public float Radius;

				public float Seed;

				public BiomeKind Kind;
			}

			private sealed class Pillar
			{
				public float X;

				public float Y;

				public float Age;

				public float Life;

				public float Width;

				public float Height;

				public float Seed;

				public uint Color;
			}

			private sealed class Hole
			{
				public float X;

				public float Y;

				public float Z;

				public float Az;

				public float Age;

				public float Life;

				public float Seed;

				public float Swallowed;

				public float Vx;

				public float Vy;
			}

			private readonly BiomeParticles _o;

			private readonly List<Devil> _devils = new List<Devil>();

			private readonly List<Pillar> _pillars = new List<Pillar>();

			private readonly List<Hole> _wells = new List<Hole>();

			private float _devilClock = 6f;

			private float _pillarClock = 3f;

			private float _wellClock;

			private float _hawk;

			private float _hawkAngle;

			private float _hawkX = float.NaN;

			private float _hawkY;

			private float _hawkR;

			public int Count => _devils.Count + _pillars.Count + _wells.Count + ((_hawk > 0.01f) ? 1 : 0);

			public Features(BiomeParticles o)
			{
				_o = o;
			}

			public int CountOf(BiomeKind k)
			{
				int num = 0;
				foreach (Devil devil in _devils)
				{
					if (devil.Kind == k)
					{
						num++;
					}
				}
				if (k == BiomeKind.Celestial)
				{
					num += _pillars.Count;
				}
				if (k == BiomeKind.Singularity)
				{
					num += _wells.Count;
				}
				if (k == BiomeKind.Rocklands && _hawk > 0.01f)
				{
					num++;
				}
				return num;
			}

			public void Clear()
			{
				_devils.Clear();
				_pillars.Clear();
				_wells.Clear();
				_hawk = 0f;
				_hawkX = float.NaN;
			}

			private float R()
			{
				return _o.R();
			}

			private bool Place(in BiomeFrame f, BiomeMap map, BiomeKind kind, float rMin, float rMax, out float x, out float y)
			{
				FxSink sink = f.Sink;
				for (int i = 0; i < 6; i++)
				{
					float num = ((R() < 0.75f) ? (sink.Yaw + (R() - 0.5f) * f.Fov * 1.1f) : (R() * 6.2832f));
					float num2 = rMin + (rMax - rMin) * R();
					x = sink.EyeX + (float)Math.Cos(num) * num2;
					y = sink.EyeY + (float)Math.Sin(num) * num2;
					if (map.At(x, y) == kind)
					{
						return true;
					}
				}
				x = (y = 0f);
				return false;
			}

			public void Step(in BiomeFrame f, BiomeMap map, BiomeMix mix)
			{
				float dt = f.Dt;
				FxSink sink = f.Sink;
				bool flag = f.Level > 0f && f.Amount > 0f;
				float num = mix.Area[2] + mix.Area[3];
				if (flag && num > 0.04f && f.SunUp > 0.3f)
				{
					_devilClock -= dt;
					if (_devilClock <= 0f)
					{
						_devilClock = (18f + 26f * R()) / Math.Max(0.4f, f.Level);
						BiomeKind kind = ((mix.Area[3] > mix.Area[2]) ? BiomeKind.Desert : BiomeKind.Savanna);
						if (_devils.Count < ((!(f.Level >= 1f)) ? 1 : 2) && Place(in f, map, kind, 16f, 42f, out var x, out var y))
						{
							_devils.Add(new Devil
							{
								X = x,
								Y = y,
								Heading = R() * 6.2832f,
								Life = 20f + 14f * R(),
								Height = 4f + 3.5f * R(),
								Radius = 0.7f + 0.6f * R(),
								Seed = R(),
								Kind = kind
							});
						}
					}
				}
				for (int num2 = _devils.Count - 1; num2 >= 0; num2--)
				{
					Devil devil = _devils[num2];
					devil.Age += dt;
					devil.Heading += (float)Math.Sin(f.Time * 0.3f + devil.Seed * 9f) * 0.5f * dt;
					float x2 = devil.X + (float)Math.Cos(devil.Heading) * 1.4f;
					float y2 = devil.Y + (float)Math.Sin(devil.Heading) * 1.4f;
					if (map.At(x2, y2) != devil.Kind)
					{
						devil.Heading += 2.2f * dt;
					}
					devil.X += ((float)Math.Cos(devil.Heading) * 0.9f + f.WindX * 0.3f) * dt;
					devil.Y += ((float)Math.Sin(devil.Heading) * 0.9f + f.WindY * 0.3f) * dt;
					float num3 = devil.X - sink.EyeX;
					float num4 = devil.Y - sink.EyeY;
					if (devil.Age > devil.Life || num3 * num3 + num4 * num4 > 3600f)
					{
						_devils.RemoveAt(num2);
					}
				}
				bool flag2 = flag && mix.Area[9] > 0.12f && f.SunUp > 0.3f;
				_hawk = Approach(_hawk, flag2 ? 1f : 0f, dt / 4f);
				if (_hawk > 0f)
				{
					if (float.IsNaN(_hawkX))
					{
						_hawkX = sink.EyeX + (R() - 0.5f) * 16f;
						_hawkY = sink.EyeY + (R() - 0.5f) * 16f;
						_hawkR = 9f + 6f * R();
						_hawkAngle = R() * 6.2832f;
					}
					_hawkAngle += 0.85f / _hawkR * dt * 6f;
					float num5 = 1f - (float)Math.Exp(-0.05f * dt);
					_hawkX += (sink.EyeX - _hawkX) * num5;
					_hawkY += (sink.EyeY - _hawkY) * num5;
				}
				else
				{
					_hawkX = float.NaN;
				}
				if (flag && mix.Area[21] > 0.03f)
				{
					_pillarClock -= dt;
					if (_pillarClock <= 0f)
					{
						_pillarClock = (8f + 14f * R()) / Math.Max(0.4f, f.Level);
						if (_pillars.Count < ((!(f.Level >= 1f)) ? 1 : 3) && Place(in f, map, BiomeKind.Celestial, 20f, 58f, out var x3, out var y3))
						{
							_pillars.Add(new Pillar
							{
								X = x3,
								Y = y3,
								Life = 26f + 18f * R(),
								Width = 1.3f + 0.9f * R(),
								Height = 26f + 14f * R(),
								Seed = R(),
								Color = ((R() < 0.6f) ? TerrainCache.Rgb(226, 196, 255) : TerrainCache.Rgb(255, 232, 176))
							});
						}
					}
				}
				for (int num6 = _pillars.Count - 1; num6 >= 0; num6--)
				{
					_pillars[num6].Age += dt;
					float num7 = _pillars[num6].X - sink.EyeX;
					float num8 = _pillars[num6].Y - sink.EyeY;
					if (_pillars[num6].Age > _pillars[num6].Life || num7 * num7 + num8 * num8 > 6400f)
					{
						_pillars.RemoveAt(num6);
					}
				}
				if (flag && mix.Area[22] > 0.03f)
				{
					_wellClock -= dt;
					if (_wellClock <= 0f)
					{
						_wellClock = 3f + 5f * R();
						if (_wells.Count < ((!(f.Level >= 1f)) ? 1 : 2) && Place(in f, map, BiomeKind.Singularity, 4f, 15f, out var x4, out var y4))
						{
							float num9 = 1.5f + 1.6f * R();
							_wells.Add(new Hole
							{
								X = x4,
								Y = y4,
								Az = num9,
								Z = sink.Ground(x4, y4) + num9,
								Life = 35f + 35f * R(),
								Seed = R()
							});
						}
					}
				}
				for (int num10 = _wells.Count - 1; num10 >= 0; num10--)
				{
					Hole hole = _wells[num10];
					hole.Age += dt;
					hole.Swallowed = Math.Max(0f, hole.Swallowed - dt * 4f);
					float num11 = 1f - (float)Math.Exp(-0.4f * dt);
					hole.Vx += (0.12f * (float)Math.Sin(f.Time * 0.13f + hole.Seed * 9f) - hole.Vx) * num11;
					hole.Vy += (0.12f * (float)Math.Cos(f.Time * 0.11f + hole.Seed * 7f) - hole.Vy) * num11;
					hole.X += hole.Vx * dt;
					hole.Y += hole.Vy * dt;
					float num12 = sink.Ground(hole.X, hole.Y) + hole.Az;
					hole.Z += (num12 - hole.Z) * (1f - (float)Math.Exp(0f - dt));
					float num13 = hole.X - sink.EyeX;
					float num14 = hole.Y - sink.EyeY;
					if (hole.Age > hole.Life + 4f || num13 * num13 + num14 * num14 > 900f || (map.At(hole.X, hole.Y) != BiomeKind.Singularity && hole.Age < hole.Life))
					{
						if (hole.Age < hole.Life)
						{
							hole.Life = hole.Age;
						}
						if (hole.Age > hole.Life + 4f || num13 * num13 + num14 * num14 > 900f)
						{
							_wells.RemoveAt(num10);
						}
					}
				}
			}

			public int PickWell(float u)
			{
				if (_wells.Count == 0)
				{
					return -1;
				}
				int num = Math.Min(_wells.Count - 1, (int)(u * (float)_wells.Count));
				if (!(_wells[num].Age < _wells[num].Life))
				{
					return -1;
				}
				return num;
			}

			public bool Well(int i, out float x, out float y, out float z, out float strength)
			{
				x = (y = (z = (strength = 0f)));
				if (i < 0 || i >= _wells.Count)
				{
					return false;
				}
				Hole hole = _wells[i];
				x = hole.X;
				y = hole.Y;
				z = hole.Z;
				strength = Smooth(hole.Age / 4f) * Smooth((hole.Life + 4f - hole.Age) / 4f);
				return true;
			}

			public void Swallow(int i)
			{
				if (i >= 0 && i < _wells.Count)
				{
					_wells[i].Swallowed = Math.Min(1f, _wells[i].Swallowed + 0.35f);
				}
			}

			public void Draw(in BiomeFrame f)
			{
				FxSink sink = f.Sink;
				float time = f.Time;
				foreach (Devil devil in _devils)
				{
					float num = Smooth(devil.Age / 3f) * Smooth((devil.Life - devil.Age) / 4f) * f.Level;
					if (!(num < 0.01f))
					{
						float num2 = sink.Ground(devil.X, devil.Y);
						uint col = ((devil.Kind == BiomeKind.Desert) ? TerrainCache.Rgb(232, 206, 150) : TerrainCache.Rgb(222, 184, 116));
						for (int i = 0; i < 12; i++)
						{
							float num3 = (float)i / 11f;
							float num4 = num3 * devil.Height;
							float num5 = devil.Radius * (0.35f + 1.4f * num3 * num3);
							float num6 = time * (4.2f - 1.8f * num3) + (float)i * 0.9f + devil.Seed * 6f;
							float num7 = (float)Math.Sin(num4 * 0.7f - time * 1.3f + devil.Seed * 5f) * 0.45f * num3;
							float x = devil.X + (float)Math.Cos(num6) * num5 * 0.55f + num7 + f.WindX * 0.08f * num4;
							float y = devil.Y + (float)Math.Sin(num6) * num5 * 0.55f + f.WindY * 0.08f * num4;
							float num8 = 0.6f + num5 * 1.1f;
							Put(sink, BlastFx.Soft(i + (int)(devil.Seed * 20f)), x, y, num2 + num4 + num8 * 0.25f, num8, num8 * 0.7f, col, num * (0.42f - 0.22f * num3), add: false, emissive: false, (i & 1) == 0);
						}
						for (int j = 0; j < 3; j++)
						{
							float num9 = time * 2.4f + (float)j * 2.1f;
							Put(sink, BlastFx.Soft(j + 7), devil.X + (float)Math.Cos(num9) * 0.9f, devil.Y + (float)Math.Sin(num9) * 0.9f, num2 + 0.3f, 1.6f, 0.6f, col, num * 0.3f, add: false, emissive: false);
						}
					}
				}
				if (_hawk > 0.01f && !float.IsNaN(_hawkX))
				{
					float x2 = _hawkX + (float)Math.Cos(_hawkAngle) * _hawkR;
					float y2 = _hawkY + (float)Math.Sin(_hawkAngle) * _hawkR;
					float z = sink.Ground(_hawkX, _hawkY) + 22f + 1.5f * (float)Math.Sin(time * 0.2f);
					int frame = ((!(Math.Sin(time * 0.45f) > 0.8)) ? 1 : ((int)(time * 6f) % 3));
					Put(sink, AmbientArt.Bird(frame, gull: false), x2, y2, z, 1.7f, 0.85f, TerrainCache.Rgb(150, 120, 96), _hawk * f.Level, add: false, emissive: false, Math.Sin(_hawkAngle) > 0.0, far: true);
				}
				foreach (Pillar pillar in _pillars)
				{
					float num10 = Smooth(pillar.Age / 7f) * Smooth((pillar.Life - pillar.Age) / 7f) * f.Level;
					if (!(num10 < 0.01f))
					{
						float num11 = sink.Ground(pillar.X, pillar.Y);
						float num12 = 0.8f + 0.2f * (float)Math.Sin(time * 0.7f + pillar.Seed * 6f);
						float num13 = num10 * num12 * (0.32f + 0.25f * f.Night);
						Put(sink, BiomeArt.Pillar, pillar.X, pillar.Y, num11 + pillar.Height * 0.5f, pillar.Width, pillar.Height, pillar.Color, num13, add: true, emissive: true, flip: false, far: true);
						Put(sink, BlastFx.GlowSprite, pillar.X, pillar.Y, num11 + 0.4f, pillar.Width * 3f, 1.4f, pillar.Color, num13 * 0.8f, add: true, emissive: true, flip: false, far: true);
						if (f.Night > 0.15f)
						{
							_o.WantLight(sink, pillar.X, pillar.Y, num11 + 1.5f, 6f, pillar.Color, num10 * 0.5f * f.Night);
						}
					}
				}
				foreach (Hole well in _wells)
				{
					float num14 = Smooth(well.Age / 4f) * Smooth((well.Life + 4f - well.Age) / 4f) * f.Level;
					if (!(num14 < 0.01f))
					{
						float num15 = 0.85f + 0.15f * (float)Math.Sin(time * 2.2f + well.Seed * 6f) + 0.4f * well.Swallowed;
						Put(sink, BlastFx.GlowSprite, well.X, well.Y, well.Z, 2.6f, 2.6f, TerrainCache.Rgb(120, 60, 210), num14 * 0.32f * num15, add: true, emissive: true);
						Put(sink, BlastFx.Ring, well.X, well.Y, well.Z, 0.95f * num15, 0.95f * num15, TerrainCache.Rgb(200, 150, 255), num14 * 0.55f, add: true, emissive: true);
						Put(sink, BlastFx.GlowSprite, well.X, well.Y, well.Z, 0.5f, 0.5f, TerrainCache.Rgb(14, 6, 26), num14 * 0.95f, add: false, emissive: true);
						if (f.Night > 0.15f)
						{
							_o.WantLight(sink, well.X, well.Y, well.Z, 4f, TerrainCache.Rgb(150, 90, 255), num14 * 0.35f * f.Night);
						}
					}
				}
			}
		}

		private readonly List<Pool> _pools = new List<Pool>();

		private readonly Random _rnd;

		private readonly Features _features;

		private readonly List<(float d2, PointLight l)> _lightsWanted = new List<(float, PointLight)>(32);

		public const int MostLights = 6;

		public const int MostAmount = 2;

		private const float PopTime = 0.14f;

		public int Alive
		{
			get
			{
				int num = 0;
				foreach (Pool pool in _pools)
				{
					P[] ps = pool.Ps;
					for (int i = 0; i < ps.Length; i++)
					{
						if (ps[i].Live)
						{
							num++;
						}
					}
				}
				return num + _features.Count;
			}
		}

		public int Pools => _pools.Count;

		public BiomeParticles(int seed)
		{
			_rnd = new Random(seed);
			_features = new Features(this);
		}

		private float R()
		{
			return (float)_rnd.NextDouble();
		}

		public int AliveOf(BiomeKind k)
		{
			int num = 0;
			foreach (Pool pool in _pools)
			{
				if (pool.R.Biome != k)
				{
					continue;
				}
				P[] ps = pool.Ps;
				for (int i = 0; i < ps.Length; i++)
				{
					if (ps[i].Live)
					{
						num++;
					}
				}
			}
			return num + _features.CountOf(k);
		}

		public void Clear()
		{
			_pools.Clear();
			_features.Clear();
		}

		public void Step(in BiomeFrame f, BiomeMap map, BiomeMix mix)
		{
			FxSink sink = f.Sink;
			if (sink == null || sink.Ground == null || map == null)
			{
				return;
			}
			_lightsWanted.Clear();
			OpenPools(mix, f.Dt);
			_features.Step(in f, map, mix);
			for (int num = _pools.Count - 1; num >= 0; num--)
			{
				Pool pool = _pools[num];
				MoteRecipe r = pool.R;
				bool flag = mix.Area[(uint)r.Biome] > 0.0015f && f.Level > 0f && f.Amount > 0f;
				pool.Fade = Approach(pool.Fade, flag ? 1f : 0f, f.Dt / 1.6f);
				pool.Absent = (flag ? 0f : (pool.Absent + f.Dt));
				bool flag2 = false;
				for (int i = 0; i < pool.Ps.Length; i++)
				{
					ref P reference = ref pool.Ps[i];
					if (reference.Live && !flag && pool.Fade <= 0f)
					{
						reference.Live = false;
					}
					if (!reference.Live)
					{
						if (!flag || i >= Wanted(r, in f))
						{
							continue;
						}
						reference.Retry -= f.Dt;
						if (reference.Retry > 0f)
						{
							continue;
						}
						if (!Spawn(ref reference, r, in f, map))
						{
							reference.Retry = 0.1f + 0.3f * R();
							continue;
						}
					}
					flag2 = true;
					Move(ref reference, r, in f, map);
					if (reference.Live)
					{
						Draw(ref reference, r, in f, pool.Fade);
					}
				}
				if (!flag2 && pool.Absent > 4f)
				{
					_pools.RemoveAt(num);
				}
			}
			_features.Draw(in f);
			FlushLights(sink, in f);
		}

		private void OpenPools(BiomeMix mix, float dt)
		{
			for (int i = 1; i < 28; i++)
			{
				if (mix.Area[i] <= 0.0015f)
				{
					continue;
				}
				List<MoteRecipe> list = MoteRecipes.Of((BiomeKind)i);
				if (list == null)
				{
					continue;
				}
				foreach (MoteRecipe item in list)
				{
					bool flag = false;
					foreach (Pool pool in _pools)
					{
						if (pool.R == item)
						{
							flag = true;
							break;
						}
					}
					if (!flag)
					{
						_pools.Add(new Pool
						{
							R = item,
							Ps = new P[item.Slots * 2]
						});
					}
				}
			}
		}

		private static int Wanted(MoteRecipe r, in BiomeFrame f)
		{
			float val = Math.Max(f.Night, f.Golden * 0.7f);
			float num = r.DayW + (r.NightW - r.DayW) * Math.Min(1f, val);
			if (r.Warm)
			{
				num *= 1f - Math.Min(1f, f.Chill * 1.4f);
			}
			float num2 = Math.Max(0f, Math.Min(2f, f.Amount));
			return (int)((float)r.Slots * Math.Max(0f, num) * f.Level * num2 + 0.5f);
		}

		private bool Spawn(ref P p, MoteRecipe r, in BiomeFrame f, BiomeMap map)
		{
			FxSink sink = f.Sink;
			for (int i = 0; i < 2; i++)
			{
				float num = ((r.Ahead && R() < 0.72f) ? (sink.Yaw + (R() - 0.5f) * f.Fov * 1.25f) : (R() * 6.2832f));
				float num2 = (float)Math.Sqrt(r.RMin * r.RMin + (r.RMax * r.RMax - r.RMin * r.RMin) * R());
				float num3 = sink.EyeX + (float)Math.Cos(num) * num2;
				float num4 = sink.EyeY + (float)Math.Sin(num) * num2;
				if (map.At(num3, num4) != r.Biome)
				{
					continue;
				}
				int num5 = -1;
				if (r.Style == MoteStyle.Spiral || r.Style == MoteStyle.Orbit)
				{
					num5 = _features.PickWell(R());
					if (num5 < 0)
					{
						return false;
					}
				}
				float num6 = sink.Ground(num3, num4);
				p = new P
				{
					Live = true,
					X = num3,
					Y = num4,
					Z = num6 + r.ZMin + (r.ZMax - r.ZMin) * R(),
					Seed = R(),
					Phase = R(),
					Life = r.LifeMin + (r.LifeMax - r.LifeMin) * R(),
					Size = r.SizeMin + (r.SizeMax - r.SizeMin) * R(),
					Presence = 1f,
					Well = num5,
					Color = ((r.Colors != null && r.Colors.Length != 0) ? r.Colors[_rnd.Next(r.Colors.Length)] : uint.MaxValue)
				};
				p.Ax = num3;
				p.Ay = num4;
				p.Az = p.Z - num6;
				p.T1x = (p.T2x = num3);
				p.T1y = (p.T2y = num4);
				p.T1z = (p.T2z = p.Z);
				switch (r.Style)
				{
				case MoteStyle.Flutter:
				case MoteStyle.Echo:
					p.Bx = R() * 6.2832f;
					break;
				case MoteStyle.Fall:
				{
					float num7 = R() * 6.2832f;
					float num8 = 0.8f + 1.6f * R();
					p.Ax = num3 - (float)Math.Cos(num7) * num8;
					p.Ay = num4 - (float)Math.Sin(num7) * num8;
					break;
				}
				case MoteStyle.Spiral:
					p.Ax = 2.4f + 2.8f * R();
					p.Ay = R() * 6.2832f;
					p.Az = (R() - 0.5f) * 3f;
					p.Life = 60f;
					break;
				case MoteStyle.Orbit:
					p.Ax = 1.8f + 2.4f * R();
					p.Ay = R() * 6.2832f;
					p.Az = (R() - 0.5f) * 1.6f;
					break;
				case MoteStyle.Glitch:
				case MoteStyle.Column:
					p.X = (float)Math.Round(num3 * 4f) / 4f;
					p.Y = (float)Math.Round(num4 * 4f) / 4f;
					p.Z = (float)Math.Round(p.Z * 4f) / 4f;
					p.Ax = p.X;
					p.Ay = p.Y;
					p.Az = num6;
					p.Bx = 0.1f + 0.4f * R();
					if (r.Style == MoteStyle.Column)
					{
						p.Z = num6;
					}
					break;
				case MoteStyle.UpRain:
					p.Z = num6;
					p.Life = 30f;
					break;
				}
				return true;
			}
			return false;
		}

		private void Move(ref P p, MoteRecipe r, in BiomeFrame f, BiomeMap map)
		{
			float dt = f.Dt;
			float time = f.Time;
			float num = p.Phase * 6.2832f;
			FxSink sink = f.Sink;
			p.Age += dt;
			float num2 = 1f - (float)Math.Exp(-0.9f * dt);
			float num3 = f.WindX * r.Wind;
			float num4 = f.WindY * r.Wind;
			switch (r.Style)
			{
			case MoteStyle.Drift:
			case MoteStyle.Rise:
			{
				float num39 = num3 + r.Drift * (float)Math.Sin(time * 0.31f + num * 3.1f + p.Z * 0.6f);
				float num40 = num4 + r.Drift * (float)Math.Cos(time * 0.27f + num * 2.3f + p.Z * 0.5f);
				float num41 = ((r.Style == MoteStyle.Rise) ? (r.Speed * (0.75f + 0.25f * (float)Math.Sin(time * 1.7f + num * 6f))) : (r.Speed + r.Drift * 0.4f * (float)Math.Sin(time * 0.5f + num * 1.7f)));
				p.Vx += (num39 - p.Vx) * num2;
				p.Vy += (num40 - p.Vy) * num2;
				p.Vz += (num41 - p.Vz) * num2;
				p.X += p.Vx * dt;
				p.Y += p.Vy * dt;
				p.Z += p.Vz * dt;
				if (r.Jitter > 0f && dt > 0f)
				{
					float num42 = time * r.Jitter + p.Seed * 7f;
					if (num42 - (float)Math.Floor(num42) < dt * r.Jitter)
					{
						p.X += (R() - 0.5f) * 0.3f;
						p.Y += (R() - 0.5f) * 0.3f;
						p.Z += R() * 0.12f;
					}
				}
				float num12 = sink.Ground(p.X, p.Y);
				if (p.Z < num12 + 0.04f)
				{
					p.Z = num12 + 0.04f;
					p.Vz = Math.Max(0f, p.Vz);
					if (r.Speed < 0f)
					{
						p.Life = Math.Min(p.Life, p.Age + 0.8f);
					}
				}
				if (p.Z > num12 + r.Top)
				{
					p.Life = Math.Min(p.Life, p.Age + r.FadeOut);
				}
				break;
			}
			case MoteStyle.Puff:
			{
				float num43 = num3 * 0.4f + r.Drift * (float)Math.Sin(time * 0.07f + num * 3f);
				float num44 = num4 * 0.4f + r.Drift * (float)Math.Cos(time * 0.06f + num * 2f);
				float num45 = 1f - (float)Math.Exp(-0.3f * dt);
				p.Vx += (num43 - p.Vx) * num45;
				p.Vy += (num44 - p.Vy) * num45;
				p.X += p.Vx * dt;
				p.Y += p.Vy * dt;
				float num12 = sink.Ground(p.X, p.Y);
				float num46 = num12 + p.Az + r.Speed * p.Age;
				p.Z += (num46 - p.Z) * (1f - (float)Math.Exp(-1.5f * dt));
				break;
			}
			case MoteStyle.Flutter:
			{
				p.Bx += ((float)Math.Sin(time * 1.3f + num * 5f) * 1.7f + (float)Math.Sin(time * 3.1f + num * 9f) * 0.9f) * dt;
				float num8 = p.X - p.Ax;
				float num9 = p.Y - p.Ay;
				if (num8 * num8 + num9 * num9 > 16f)
				{
					float num10 = Wrap((float)Math.Atan2(0f - num9, 0f - num8) - p.Bx);
					p.Bx += num10 * Math.Min(1f, dt * 1.5f);
				}
				float num11 = r.Speed * (0.6f + 0.4f * (float)Math.Sin(time * 2.1f + num * 4f));
				p.X += ((float)Math.Cos(p.Bx) * num11 + num3 * 0.3f) * dt;
				p.Y += ((float)Math.Sin(p.Bx) * num11 + num4 * 0.3f) * dt;
				float num12 = sink.Ground(p.X, p.Y);
				float num13 = num12 + p.Az + 0.32f * (float)Math.Sin(time * 1.9f + num * 7f) + 0.12f * (float)Math.Sin(time * 7.3f + num * 3f);
				p.Z += (num13 - p.Z) * (1f - (float)Math.Exp(-4f * dt));
				break;
			}
			case MoteStyle.Fall:
				if (!(p.Bx > 0.5f))
				{
					float num14 = r.Drift * (float)Math.Sin(time * 1.6f + num * 7f);
					float num15 = num3 + num14 * (float)Math.Cos(num);
					float num16 = num4 + num14 * (float)Math.Sin(num);
					if (r.Swirl > 0f)
					{
						float num17 = p.X - p.Ax;
						float num18 = p.Y - p.Ay;
						float num19 = (float)Math.Sqrt(num17 * num17 + num18 * num18) + 0.05f;
						float num20 = r.Swirl / (0.7f + num19);
						num15 += (0f - num18) / num19 * num20 - num17 / num19 * 0.12f;
						num16 += num17 / num19 * num20 - num18 / num19 * 0.12f;
					}
					float num21 = (0f - r.Speed) * (0.7f + 0.3f * (float)Math.Sin(time * 2.3f + num * 5f));
					p.Vx += (num15 - p.Vx) * num2;
					p.Vy += (num16 - p.Vy) * num2;
					p.Vz += (num21 - p.Vz) * num2;
					p.X += p.Vx * dt;
					p.Y += p.Vy * dt;
					p.Z += p.Vz * dt;
					float num12 = sink.Ground(p.X, p.Y);
					if (p.Z <= num12 + 0.03f)
					{
						p.Z = num12 + 0.03f;
						p.Bx = 1f;
						p.Life = Math.Min(p.Life, p.Age + 1.4f);
					}
				}
				break;
			case MoteStyle.Bubble:
				if (!(p.Age > p.Life - 0.14f))
				{
					float num5 = num3 * 0.5f + r.Drift * (float)Math.Sin(time * 1.1f + num * 4f);
					float num6 = num4 * 0.5f + r.Drift * (float)Math.Cos(time * 0.9f + num * 3f);
					float num7 = r.Speed * (0.8f + 0.2f * (float)Math.Sin(time * 1.4f + num * 2f));
					p.Vx += (num5 - p.Vx) * num2;
					p.Vy += (num6 - p.Vy) * num2;
					p.Vz += (num7 - p.Vz) * num2;
					p.X += p.Vx * dt;
					p.Y += p.Vy * dt;
					p.Z += p.Vz * dt;
				}
				break;
			case MoteStyle.Glow:
			case MoteStyle.Echo:
			{
				float num23 = num3 * 0.3f + r.Drift * (float)Math.Sin(time * 0.21f + num * 5f);
				float num24 = num4 * 0.3f + r.Drift * (float)Math.Cos(time * 0.19f + num * 4f);
				if (r.Style == MoteStyle.Echo)
				{
					p.Bx += (float)Math.Sin(time * 0.5f + num * 3f) * 0.7f * dt;
					num23 += (float)Math.Cos(p.Bx) * r.Speed;
					num24 += (float)Math.Sin(p.Bx) * r.Speed;
				}
				if (r.Flee)
				{
					float num25 = p.Ax - sink.EyeX;
					float num26 = p.Ay - sink.EyeY;
					float num27 = (float)Math.Sqrt(num25 * num25 + num26 * num26) + 0.01f;
					if (num27 < 8f)
					{
						float num28 = (8f - num27) / 8f * 2.2f;
						num23 += num25 / num27 * num28;
						num24 += num26 / num27 * num28;
					}
				}
				p.Vx += (num23 - p.Vx) * num2;
				p.Vy += (num24 - p.Vy) * num2;
				p.Ax += p.Vx * dt;
				p.Ay += p.Vy * dt;
				float num29 = sink.Ground(p.Ax, p.Ay);
				if (r.Style == MoteStyle.Echo)
				{
					p.X = p.Ax;
					p.Y = p.Ay;
				}
				else
				{
					float num30 = ((r.Speed > 0f) ? (0.25f + 0.35f * p.Seed) : 0.12f);
					float num31 = time * (0.9f + p.Seed) + num;
					p.X = p.Ax + (float)Math.Cos(num31) * num30;
					p.Y = p.Ay + (float)Math.Sin(num31) * num30;
				}
				float num32 = num29 + p.Az + r.Speed * p.Age + 0.25f * (float)Math.Sin(time * 0.9f + num * 5f);
				p.Z += (num32 - p.Z) * (1f - (float)Math.Exp(-3f * dt));
				if (p.Z > num29 + r.Top)
				{
					p.Life = Math.Min(p.Life, p.Age + r.FadeOut);
				}
				if (r.Trail)
				{
					p.TrailClock += dt;
					if (p.TrailClock >= 0.09f)
					{
						p.TrailClock = 0f;
						p.T2x = p.T1x;
						p.T2y = p.T1y;
						p.T2z = p.T1z;
						p.T1x = p.X;
						p.T1y = p.Y;
						p.T1z = p.Z;
					}
				}
				break;
			}
			case MoteStyle.Ribbon:
			{
				float num33 = f.WindX;
				float num34 = f.WindY;
				float num35 = (float)Math.Sqrt(num33 * num33 + num34 * num34);
				if (num35 < 0.05f)
				{
					num33 = (float)Math.Cos(time * 0.01f);
					num34 = (float)Math.Sin(time * 0.01f);
					num35 = 1f;
				}
				num33 /= num35;
				num34 /= num35;
				float num36 = r.Speed * (0.8f + 0.4f * p.Seed) + num35 * r.Wind;
				float num37 = (float)Math.Cos(p.Age * 2.1f + num) * 0.6f;
				p.X += (num33 * num36 - num34 * num37) * dt;
				p.Y += (num34 * num36 + num33 * num37) * dt;
				float num12 = sink.Ground(p.X, p.Y);
				p.Z = num12 + p.Az + 0.04f * (float)Math.Sin(p.Age * 5f + num);
				break;
			}
			case MoteStyle.Spiral:
			case MoteStyle.Orbit:
			{
				if (!_features.Well(p.Well, out var x, out var y, out var z, out var strength))
				{
					p.Life = Math.Min(p.Life, p.Age + 0.5f);
					break;
				}
				if (r.Style == MoteStyle.Spiral)
				{
					float num38 = 1.4f * r.Speed / (p.Ax + 0.25f);
					p.Ay += num38 * dt;
					p.Ax -= (0.16f + 0.5f / (p.Ax + 0.35f)) * dt * r.Speed;
					p.Az *= (float)Math.Exp((0f - dt) * 0.55f);
					if (p.Ax < 0.08f)
					{
						_features.Swallow(p.Well);
						p.Live = false;
						p.Retry = 0.2f;
						return;
					}
				}
				else
				{
					p.Ay += 0.3f * r.Speed / (0.6f + p.Ax * 0.4f) * dt;
					p.Az += (float)Math.Sin(time * 0.7f + num * 5f) * 0.1f * dt;
				}
				p.X = x + (float)Math.Cos(p.Ay) * p.Ax;
				p.Y = y + (float)Math.Sin(p.Ay) * p.Ax;
				p.Z = z + p.Az;
				p.Bx = strength;
				break;
			}
			case MoteStyle.UpRain:
				p.Z += r.Speed * dt;
				p.X += num3 * 0.2f * dt;
				p.Y += num4 * 0.2f * dt;
				if (p.Z > sink.Ground(p.X, p.Y) + r.Top)
				{
					p.Live = false;
					p.Retry = 0.05f + 0.3f * R();
					return;
				}
				break;
			case MoteStyle.Glitch:
				p.Bx -= dt;
				if (p.Bx <= 0f)
				{
					float num22 = R();
					if (num22 < 0.55f)
					{
						p.Z += 0.25f;
					}
					else if (num22 < 0.7f)
					{
						p.X += 0.25f;
					}
					else if (num22 < 0.85f)
					{
						p.X -= 0.25f;
					}
					else if (num22 < 0.93f)
					{
						p.Y += 0.25f;
					}
					else
					{
						p.Y -= 0.25f;
					}
					p.Bx = 0.15f + 0.6f * R();
				}
				if (p.Z > p.Az + r.Top)
				{
					p.Life = Math.Min(p.Life, p.Age + 0.3f);
				}
				break;
			case MoteStyle.Column:
				p.Bx -= dt;
				if (p.Bx <= 0f)
				{
					p.Z += 0.18f;
					p.Bx = 0.08f;
				}
				if (p.Z > p.Az + r.Top)
				{
					p.Live = false;
					p.Retry = 0.5f + R();
					return;
				}
				break;
			}
			bool flag = map.At((r.Style == MoteStyle.Glow || r.Style == MoteStyle.Echo) ? p.Ax : p.X, (r.Style == MoteStyle.Glow || r.Style == MoteStyle.Echo) ? p.Ay : p.Y) == r.Biome || r.Style == MoteStyle.Spiral || r.Style == MoteStyle.Orbit;
			p.Presence = Approach(p.Presence, flag ? 1f : 0f, dt / 1.2f);
			float num47 = p.X - sink.EyeX;
			float num48 = p.Y - sink.EyeY;
			float num49 = r.RMax + 3f;
			if (p.Age >= p.Life || p.Presence <= 0.01f || num47 * num47 + num48 * num48 > num49 * num49)
			{
				p.Live = false;
				p.Retry = 0.05f + 0.25f * R();
			}
		}

		private void Draw(ref P p, MoteRecipe r, in BiomeFrame f, float poolFade)
		{
			FxSink sink = f.Sink;
			float time = f.Time;
			float num = p.Phase * 6.2832f;
			float num2 = Smooth(p.Age / Math.Max(0.01f, r.FadeIn)) * Smooth((p.Life - p.Age) / Math.Max(0.01f, r.FadeOut)) * p.Presence * poolFade;
			if (r.Style == MoteStyle.Bubble || r.Style == MoteStyle.UpRain || r.Style == MoteStyle.Spiral)
			{
				num2 = Smooth(p.Age / Math.Max(0.01f, r.FadeIn)) * p.Presence * poolFade;
			}
			float num3 = p.X - sink.EyeX;
			float num4 = p.Y - sink.EyeY;
			float num5 = (float)Math.Sqrt(num3 * num3 + num4 * num4);
			num2 *= Smooth((r.RMax + 2.5f - num5) / 2.5f);
			float num6 = ((r.Style == MoteStyle.Puff) ? Math.Max(1.5f, r.SizeMax * 0.6f) : ((r.Style == MoteStyle.Bubble) ? (0.6f + r.SizeMax * 2f) : ((r.Style == MoteStyle.UpRain) ? 1.3f : (0.55f + r.SizeMax * 4f))));
			num2 *= Smooth((num5 - num6) / ((r.Style == MoteStyle.Puff) ? 3f : 0.8f));
			float num7 = r.DayBright + (r.NightBright - r.DayBright) * f.Night;
			if (r.Pulse)
			{
				num7 *= 0.5f + 0.7f * Math.Max(0f, (float)Math.Sin(time * 1.5f - num5 * 0.55f));
			}
			float num8 = num2 * num7;
			uint num9 = p.Color;
			if (r.Hue > 0f)
			{
				num9 = BiomeLooks.HueTurn(num9, time * r.Hue + p.Seed * 0.35f);
			}
			float num10 = p.Size;
			if (r.Beat)
			{
				num10 *= 1f + 0.3f * BiomeMix.Heartbeat(time + p.Seed * 0.15f);
			}
			if (r.Light > 0f && f.Night > 0.15f && num2 > 0.05f)
			{
				WantLight(sink, p.X, p.Y, p.Z, r.Light, r.LightColor, num2 * Math.Min(1f, (f.Night - 0.15f) * 2f) * 0.55f * (0.85f + 0.15f * (float)Math.Sin(time * 5f + num)));
			}
			if (r.Alpha == 0 || num8 < 0.01f)
			{
				return;
			}
			float num11 = num8 * (float)(int)r.Alpha / 255f;
			int num12 = ((r.Frames > 1) ? ((int)(time * r.FrameRate + p.Seed * 13f) % r.Frames) : 0);
			switch (r.Style)
			{
			case MoteStyle.Drift:
			case MoteStyle.Fall:
			case MoteStyle.Rise:
			{
				if (r.Twinkle > 0f)
				{
					float num19 = (float)Math.Sin(time * r.Twinkle + num * 3f);
					float num20 = 0.25f + 0.75f * num19 * num19;
					num11 *= num20;
					if (r.Starry)
					{
						num12 = ((!(num19 * num19 > 0.55f)) ? 1 : 0);
					}
				}
				if (r.Style == MoteStyle.Fall && p.Bx > 0.5f)
				{
					num12 = 0;
				}
				SpriteImage spriteImage = ((r.Sprite != null) ? r.Sprite(num12) : (r.Starry ? BiomeArt.Star(num12) : BlastFx.GlowSprite));
				if (r.Additive && spriteImage != BlastFx.GlowSprite)
				{
					float num21 = Smooth((num5 - 1.4f) / 2.2f);
					if (num21 < 1f)
					{
						Put(sink, BlastFx.GlowSprite, p.X, p.Y, p.Z, num10 * 2.4f, num10 * 2.4f, num9, num11 * (1f - num21) * 0.85f, add: true, emissive: true);
					}
					num11 *= num21;
				}
				Put(sink, spriteImage, p.X, p.Y, p.Z, num10, num10 * r.Aspect, num9, num11, r.Additive, r.Emissive, ((int)(p.Seed * 97f) & 1) != 0);
				if (r.Glint && num12 == 1)
				{
					Put(sink, BiomeArt.Star(0), p.X, p.Y, p.Z, num10 * 1.4f, num10 * 1.4f, TerrainCache.Lerp(num9, uint.MaxValue, 0.6f), num11 * 0.9f, add: true, emissive: true);
				}
				break;
			}
			case MoteStyle.Puff:
			{
				float num29 = 1f + r.Grow * Math.Min(1f, p.Age / Math.Max(0.1f, p.Life));
				float num30 = num10 * num29;
				float num31 = num30 * r.Aspect;
				Put(sink, BlastFx.Soft((int)(p.Seed * 50f)), p.X, p.Y, p.Z + num31 * 0.42f, num30, num31, num9, num11, add: false, r.Emissive, p.Seed > 0.5f);
				break;
			}
			case MoteStyle.Flutter:
			{
				int num17 = (new int[4] { 0, 2, 1, 2 })[(int)(time * r.FrameRate + p.Seed * 9f) & 3];
				if (Math.Sin(time * 0.6f + num * 8f) > 0.72)
				{
					num17 = 0;
				}
				SpriteImage img3 = ((r.Sprite != null) ? r.Sprite(num17) : BiomeArt.Butterfly(num17));
				float num18 = (r.Emissive ? (0.75f + 0.25f * (float)Math.Sin(time * 6f + num)) : 1f);
				Put(sink, img3, p.X, p.Y, p.Z, num10, num10 * 0.8f, num9, num11 * num18, add: false, r.Emissive, Math.Cos(p.Bx - sink.Yaw) < 0.0);
				break;
			}
			case MoteStyle.Bubble:
			{
				float num22 = p.Life - 0.14f;
				if (p.Age >= num22)
				{
					float num23 = (p.Age - num22) / 0.14f;
					float num24 = num10 * (1f + 0.6f * num23);
					Put(sink, BlastFx.Ring, p.X, p.Y, p.Z, num24, num24, TerrainCache.Lerp(num9, uint.MaxValue, 0.4f), num11 * (1f - num23) * (1f - num23) * 0.5f, add: true, emissive: true);
				}
				else
				{
					uint col = ((r.Hue > 0f) ? BiomeLooks.Hsv(time * 0.12f + p.Seed + p.Z * 0.15f, 0.42f, 1f) : num9);
					Put(sink, BiomeArt.Bubble, p.X, p.Y, p.Z, num10, num10, col, num11, add: false, r.Emissive);
				}
				break;
			}
			case MoteStyle.Glow:
			{
				SpriteImage glowSprite2 = BlastFx.GlowSprite;
				float num35 = 0.85f + 0.15f * (float)Math.Sin(time * 3.3f + num * 2f);
				if (r.Blink > 0f)
				{
					float num36 = time * r.Blink * (0.8f + 0.4f * p.Seed) + p.Seed * 7.31f;
					num36 -= (float)Math.Floor(num36);
					num35 = ((num36 < 0.4f) ? ((float)Math.Sin((double)(num36 / 0.4f) * Math.PI)) : 0f);
					if (num35 < 0.02f)
					{
						break;
					}
				}
				if (r.Trail)
				{
					Put(sink, glowSprite2, p.T2x, p.T2y, p.T2z, num10 * 0.55f, num10 * 0.55f, num9, num11 * 0.22f, add: true, emissive: true);
					Put(sink, glowSprite2, p.T1x, p.T1y, p.T1z, num10 * 0.75f, num10 * 0.75f, num9, num11 * 0.42f, add: true, emissive: true);
				}
				Put(sink, glowSprite2, p.X, p.Y, p.Z, num10, num10, num9, num11 * num35, add: true, emissive: true);
				Put(sink, glowSprite2, p.X, p.Y, p.Z, num10 * 0.38f, num10 * 0.38f, TerrainCache.Lerp(num9, uint.MaxValue, 0.6f), Math.Min(1f, num11 * 1.4f) * ((r.Blink > 0f) ? num35 : 1f), add: true, emissive: true);
				break;
			}
			case MoteStyle.Echo:
			{
				SpriteImage glowSprite = BlastFx.GlowSprite;
				for (int num25 = 2; num25 >= 0; num25--)
				{
					float num26 = (float)num25 * 0.45f;
					float x = p.X + p.Vx * num26;
					float y = p.Y + p.Vy * num26;
					uint col2 = BiomeLooks.HueTurn(num9, (float)num25 * 0.12f);
					float z = p.Z;
					float w = num10 * (1f - (float)num25 * 0.18f);
					float h = num10 * (1f - (float)num25 * 0.18f);
					float num27 = num11;
					float num28;
					switch (num25)
					{
					default:
						num28 = 0.18f;
						break;
					case 1:
						num28 = 0.42f;
						break;
					case 0:
						num28 = 1f;
						break;
					}
					Put(sink, glowSprite, x, y, z, w, h, col2, num27 * num28, add: true, emissive: true);
				}
				break;
			}
			case MoteStyle.Ribbon:
				Put(sink, BlastFx.Soft((int)(p.Seed * 40f)), p.X, p.Y, p.Z + num10 * r.Aspect * 0.5f, num10, num10 * r.Aspect, num9, num11, add: false, emissive: false, p.Seed > 0.5f);
				break;
			case MoteStyle.Spiral:
			{
				float num15 = 1f - Math.Min(1f, p.Ax / 5f);
				float num16 = (0.45f + 0.55f * num15) * p.Bx;
				SpriteImage img2 = ((r.Sprite != null) ? r.Sprite(0) : BlastFx.GlowSprite);
				Put(sink, img2, p.X, p.Y, p.Z, num10 * (0.8f + 0.5f * num15), num10 * (0.8f + 0.5f * num15), num9, num11 * num16, r.Additive, r.Emissive);
				break;
			}
			case MoteStyle.Orbit:
				Put(sink, SkyFx.Rock(), p.X, p.Y, p.Z, num10, num10, num9, num11 * p.Bx, add: false, emissive: false, p.Seed > 0.5f);
				break;
			case MoteStyle.UpRain:
			{
				float num32 = num10 * r.Aspect;
				Put(sink, BiomeArt.UpDrop, p.X, p.Y, p.Z + num32 * 0.5f, num10, num32, num9, num11, add: false, r.Emissive);
				if (p.Age < 0.25f)
				{
					float num33 = p.Age / 0.25f;
					float num34 = sink.Ground(p.X, p.Y);
					Put(sink, BlastFx.Ring, p.X, p.Y, num34 + 0.04f, 0.32f * (1f - num33) + 0.04f, (0.32f * (1f - num33) + 0.04f) * 0.4f, num9, num11 * 0.5f * num33, add: false, emissive: false);
				}
				break;
			}
			case MoteStyle.Glitch:
				if (!(Math.Sin(time * 13f + num * 40f) < -0.75))
				{
					num11 *= Smooth((num5 - 2f) / 1.5f);
					SpriteImage img4 = ((r.Sprite != null) ? r.Sprite((int)(p.Seed * 9f)) : BlastFx.Spark(2));
					Put(sink, img4, p.X, p.Y, p.Z, num10, num10, num9, num11, r.Additive, r.Emissive);
				}
				break;
			case MoteStyle.Column:
			{
				SpriteImage img = ((r.Sprite != null) ? r.Sprite(1) : BlastFx.Spark(2));
				for (int i = 0; i < 6; i++)
				{
					float num13 = p.Z - (float)i * 0.18f;
					if (!(num13 < p.Az))
					{
						float num14 = (1f - (float)i / 6f) * (1f - (float)i / 6f);
						Put(sink, img, p.X, p.Y, num13, num10, num10, (i == 0) ? TerrainCache.Lerp(num9, uint.MaxValue, 0.7f) : num9, num11 * num14, add: true, emissive: true);
						continue;
					}
					break;
				}
				break;
			}
			}
		}

		internal static void Put(FxSink s, SpriteImage img, float x, float y, float z, float w, float h, uint col, float alpha, bool add, bool emissive, bool flip = false, bool far = false)
		{
			int num = (int)(alpha * 255f);
			if (num >= 3 && img != null)
			{
				if (num > 255)
				{
					num = 255;
				}
				s.Billboards.Add(new Billboard
				{
					X = x,
					Y = y,
					Z = z - h * 0.5f,
					GroundZ = z - h * 0.5f,
					Width = w,
					Height = h,
					Image = img,
					Kind = BillboardKind.Facing,
					Tag = -1,
					Multiply = (col | 0xFF000000u),
					Alpha = (byte)num,
					Additive = add,
					Emissive = emissive,
					Flip = flip,
					Far = far
				});
			}
		}

		internal void WantLight(FxSink s, float x, float y, float z, float radius, uint color, float strength)
		{
			if (!(strength <= 0.02f))
			{
				float num = x - s.EyeX;
				float num2 = y - s.EyeY;
				_lightsWanted.Add((num * num + num2 * num2, new PointLight
				{
					X = x,
					Y = y,
					Z = z,
					Radius = radius,
					Glow = 0f,
					R = (float)(color & 0xFF) / 255f * strength,
					G = (float)((color >> 8) & 0xFF) / 255f * strength,
					B = (float)((color >> 16) & 0xFF) / 255f * strength
				}));
			}
		}

		private void FlushLights(FxSink s, in BiomeFrame f)
		{
			if (_lightsWanted.Count != 0 && s.Lights != null)
			{
				_lightsWanted.Sort(((float d2, PointLight l) a, (float d2, PointLight l) b) => a.d2.CompareTo(b.d2));
				int num = Math.Min(6, _lightsWanted.Count);
				for (int num2 = 0; num2 < num; num2++)
				{
					s.Lights.Add(_lightsWanted[num2].l);
				}
			}
		}

		internal static float Smooth(float x)
		{
			if (x <= 0f)
			{
				return 0f;
			}
			if (x >= 1f)
			{
				return 1f;
			}
			return x * x * (3f - 2f * x);
		}

		internal static float Approach(float v, float target, float step)
		{
			if (!(v < target))
			{
				return Math.Max(target, v - step);
			}
			return Math.Min(target, v + step);
		}

		private static float Wrap(float a)
		{
			while ((double)a > Math.PI)
			{
				a -= 6.2832f;
			}
			while ((double)a < -Math.PI)
			{
				a += 6.2832f;
			}
			return a;
		}
	}
}
