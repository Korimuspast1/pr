using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class WindFx
	{
		private struct Line
		{
			public Vec3 O;

			public float Age;

			public float Life;

			public float Len;

			public float Bend;

			public float Lift;

			public float CurlR;

			public float Speed;

			public float Phase;

			public bool Curl;
		}

		public enum BitKind : byte
		{
			Leaf,
			Blade,
			Petal,
			Grain,
			Flake
		}

		private struct Bit
		{
			public Vec3 Pos;

			public Vec3 Vel;

			public float Age;

			public float Life;

			public float Size;

			public float Spin;

			public float SpinRate;

			public float Flutter;

			public float Fall;

			public uint Color;

			public BitKind Kind;

			public bool Landed;

			public bool Drifting;

			public bool Glow;
		}

		private struct Drift
		{
			public Vec3 Pos;

			public float Age;

			public float Life;

			public float Size;

			public float EndSize;

			public float Speed;

			public uint Color;

			public int Variant;

			public byte Alpha;
		}

		public struct Crown
		{
			public float X;

			public float Y;

			public float Z;

			public float Radius;

			public uint Leaf;

			public bool Deciduous;

			public bool Glow;

			public bool Snowy;
		}

		private struct Arc
		{
			public float X;

			public float Y;

			public float R;

			public float A0;

			public float Span;

			public float Z;

			public float Rise;

			public float Age;

			public float Life;

			public float Turn;
		}

		public float LeafAmount = 1f;

		private float _night;

		private const int MostBits = 160;

		private readonly List<Line> _lines = new List<Line>(32);

		private readonly List<Arc> _arcs = new List<Arc>(24);

		private readonly List<Bit> _bits = new List<Bit>(128);

		private readonly List<Drift> _drifts = new List<Drift>(24);

		public readonly List<Crown> Crowns = new List<Crown>(64);

		public readonly List<Vortex> Vortices = new List<Vortex>(4);

		private readonly Random _rnd = new Random(7);

		private float _lineClock;

		private float _bitClock;

		private float _driftClock;

		private float _leafClock;

		private float _arcClock;

		public int Lines => _lines.Count;

		public int Arcs => _arcs.Count;

		public int Bits => _bits.Count;

		public int Drifts => _drifts.Count;

		public int Falling
		{
			get
			{
				int num = 0;
				foreach (Bit bit in _bits)
				{
					if (bit.Drifting)
					{
						num++;
					}
				}
				return num;
			}
		}

		private float R()
		{
			return (float)_rnd.NextDouble();
		}

		public static void Bend(MeshInstance inst, float x, float y, Gusts g, float windTime, float clock)
		{
			Bend(inst, x, y, g.DirX, g.DirY, g.Strength, windTime, clock);
		}

		public static void Bend(MeshInstance inst, float x, float y, float dirX, float dirY, float strength, float windTime, float clock)
		{
			if (!(strength < 0.04f))
			{
				float num = 0.5f + 0.5f * (float)Math.Sin((x * dirX + y * dirY) * 0.85f - windTime * 3.1f + 0.7f);
				float num2 = 0.012f * (float)Math.Sin(clock * 5.3f + x * 1.7f + y);
				float num3 = Math.Min(1f, strength) * (0.022f + 0.04f * num * num + num2);
				if (strength > 1f)
				{
					num3 += Math.Min(1.5f, strength - 1f) * (0.07f + 0.03f * (float)Math.Sin(clock * 11f + x * 1.3f + y * 0.7f) + 0.04f * num);
				}
				Vec3 c = inst.Basis.C2;
				float z = c.Z;
				inst.Basis = new Mat3(inst.Basis.C0, inst.Basis.C1, new Vec3(c.X + dirX * num3 * z, c.Y + dirY * num3 * z, c.Z));
				inst.Static = false;
			}
		}

		public bool TwisterWind(float x, float y, out float dirX, out float dirY, out float strength)
		{
			dirX = 1f;
			dirY = 0f;
			strength = 0f;
			if (Vortices.Count == 0)
			{
				return false;
			}
			float num = 0f;
			float num2 = 0f;
			float num3 = 1f;
			foreach (Vortex vortice in Vortices)
			{
				vortice.Flow(x, y, out var vx, out var vy, out var _);
				num += vx;
				num2 += vy;
				num3 = Math.Max(num3, vortice.Speed);
			}
			float num4 = (float)Math.Sqrt(num * num + num2 * num2);
			if (num4 < 0.3f)
			{
				return false;
			}
			dirX = num / num4;
			dirY = num2 / num4;
			strength = num4 / (num3 * 0.3f);
			return true;
		}

		private void Air(float x, float y, Gusts g, out float vx, out float vy, out float vz)
		{
			vx = (vy = (vz = 0f));
			foreach (Vortex vortice in Vortices)
			{
				vortice.Flow(x, y, out var vx2, out var vy2, out var vz2);
				vx += vx2;
				vy += vy2;
				vz += vz2;
			}
			float num = ((Vortices.Count == 0) ? 0f : Math.Min(1f, (float)Math.Sqrt(vx * vx + vy * vy) / Math.Max(1f, g.Speed)));
			vx += g.DirX * g.Speed * (1f - num);
			vy += g.DirY * g.Speed * (1f - num);
		}

		public void Clear()
		{
			_lines.Clear();
			_arcs.Clear();
			_bits.Clear();
			_drifts.Clear();
		}

		public void Step(float dt, float time, Gusts g, FxSink sink, TerrainCache t, float fov, float night)
		{
			if (t == null || t.Width == 0 || sink.Ground == null)
			{
				Crowns.Clear();
				return;
			}
			_night = night;
			_ = g.Strength;
			if (dt > 0f)
			{
				SpawnLines(dt, g, sink, fov);
				SpawnArcs(dt, sink);
				SpawnBits(dt, g, sink, t, fov);
				SpawnDrifts(dt, g, sink, t, fov);
				DropLeaves(dt, g, sink);
			}
			Crowns.Clear();
			StepLines(dt, g, sink, night);
			StepArcs(dt, sink, night);
			StepBits(dt, time, g, sink);
			StepDrifts(dt, g, sink);
		}

		private bool NearTwister(FxSink sink, TerrainCache t, float inner, float outer, float reach, out float x, out float y, out Vortex v)
		{
			v = Vortices[_rnd.Next(Vortices.Count)];
			float num = R() * 6.2832f;
			float num2 = v.Core * (inner + (outer - inner) * R());
			x = v.X + (float)Math.Cos(num) * num2;
			y = v.Y + (float)Math.Sin(num) * num2;
			float num3 = x - sink.EyeX;
			float num4 = y - sink.EyeY;
			if (num3 * num3 + num4 * num4 > reach * reach)
			{
				return false;
			}
			int num5 = (int)x;
			int num6 = (int)y;
			if (t != null && (num5 < 0 || num6 < 0 || num5 >= t.Width || num6 >= t.Height))
			{
				return false;
			}
			return true;
		}

		private void SpawnArcs(float dt, FxSink sink)
		{
			if (Vortices.Count == 0)
			{
				return;
			}
			_arcClock += dt * 16f;
			while (_arcClock > 1f && _arcs.Count < 22)
			{
				_arcClock -= 1f;
				if (NearTwister(sink, null, 1.05f, 3.2f, 38f, out var x, out var y, out var v))
				{
					float num = x - v.X;
					float num2 = y - v.Y;
					float num3 = (float)Math.Sqrt(num * num + num2 * num2);
					float num4 = v.Speed * (float)Math.Pow(v.Core / Math.Max(num3, v.Core), 0.7);
					_arcs.Add(new Arc
					{
						X = v.X,
						Y = v.Y,
						R = num3,
						A0 = (float)Math.Atan2(num2, num),
						Span = 0.7f + 0.9f * R(),
						Z = sink.Ground(x, y) + 0.3f + 4.5f * R() * R(),
						Rise = 0.4f + 1.2f * R(),
						Life = 0.7f + 0.5f * R(),
						Turn = num4 / Math.Max(0.5f, num3)
					});
				}
			}
			if (_arcClock > 1f)
			{
				_arcClock = 1f;
			}
		}

		private void StepArcs(float dt, FxSink sink, float night)
		{
			float num = 1f - 0.6f * night;
			uint color = TerrainCache.Lerp(TerrainCache.Rgb(236, 240, 246), TerrainCache.Rgb(150, 166, 196), night);
			for (int num2 = _arcs.Count - 1; num2 >= 0; num2--)
			{
				Arc value = _arcs[num2];
				value.Age += dt;
				if (value.Age >= value.Life)
				{
					_arcs.RemoveAt(num2);
				}
				else
				{
					value.A0 += value.Turn * dt;
					_arcs[num2] = value;
					if (sink.Streaks != null)
					{
						float num3 = BlastFx.Smooth(value.Age / (value.Life * 0.5f));
						float num4 = BlastFx.Smooth((value.Age - value.Life * 0.3f) / (value.Life * 0.7f));
						if (!(num3 - num4 < 0.02f))
						{
							Vec3 a = default(Vec3);
							float alphaA = 0f;
							for (int i = 0; i <= 16; i++)
							{
								float num5 = num4 + (num3 - num4) * (float)i / 16f;
								float num6 = value.A0 + num5 * value.Span;
								float num7 = value.R * (1f - 0.12f * num5);
								Vec3 vec = new Vec3(value.X + (float)Math.Cos(num6) * num7, value.Y + (float)Math.Sin(num6) * num7, value.Z + value.Rise * num5);
								float num8 = vec.X - sink.EyeX;
								float num9 = vec.Y - sink.EyeY;
								float num10 = vec.Z - sink.EyeZ;
								float num11 = BlastFx.Smooth(((float)Math.Sqrt(num8 * num8 + num9 * num9 + num10 * num10) - 2.5f) / 4f);
								float num12 = 0.5f * num * num11 * (float)Math.Pow(Math.Sin(Math.PI * (double)i / 16.0), 0.6);
								if (i > 0)
								{
									sink.Line(a, vec, color, alphaA, num12, 0.03f, 0.55f);
								}
								a = vec;
								alphaA = num12;
							}
						}
					}
				}
			}
		}

		private void InView(FxSink sink, float fov, float near, float far, out float x, out float y)
		{
			float num = near + (far - near) * R() * R();
			float num2 = sink.Yaw + (R() - 0.5f) * fov * 1.15f;
			x = sink.EyeX + (float)Math.Cos(num2) * num;
			y = sink.EyeY + (float)Math.Sin(num2) * num;
		}

		private void SpawnLines(float dt, Gusts g, FxSink sink, float fov)
		{
			float strength = g.Strength;
			if (!(strength < 0.14f))
			{
				_lineClock += dt * 16f * (float)Math.Pow(strength - 0.1f, 1.2000000476837158);
				while (_lineClock > 1f && _lines.Count < 26)
				{
					_lineClock -= 1f;
					InView(sink, fov, 2.5f, 18f, out var x, out var y);
					float num = 0.9f + 0.6f * R();
					float num2 = 3.5f + 4.5f * R();
					float num3 = g.Speed * (0.8f + 0.4f * R());
					float num4 = num2 * 0.5f + num3 * num * 0.3f;
					float x2 = x - g.DirX * num4;
					float y2 = y - g.DirY * num4;
					bool flag = R() < 0.12f;
					float z = sink.Ground(x, y) + (flag ? (4f + 4f * R()) : (0.3f + 2.6f * (float)Math.Pow(R(), 1.5)));
					_lines.Add(new Line
					{
						O = new Vec3(x2, y2, z),
						Life = num,
						Len = num2,
						Speed = num3,
						Bend = (R() - 0.5f) * 1.2f,
						Lift = (R() - 0.3f) * 0.6f,
						Phase = R() * 6.283f,
						Curl = (R() < 0.3f),
						CurlR = 0.3f + 0.4f * R()
					});
				}
				if (_lineClock > 1f)
				{
					_lineClock = 1f;
				}
			}
		}

		private static Vec3 LinePoint(in Line l, float u, float dx, float dy)
		{
			float num = (l.Curl ? (Math.Min(u, 0.72f) / 0.72f) : u);
			float num2 = 0f - dy;
			float num3 = (float)Math.Sin((double)num * Math.PI);
			Vec3 result = new Vec3(l.O.X + dx * num * l.Len + num2 * l.Bend * num3, l.O.Y + dy * num * l.Len + dx * l.Bend * num3, l.O.Z + l.Lift * num3 + 0.12f * (float)Math.Sin(num * 9.4f + l.Phase));
			if (!l.Curl || u <= 0.72f)
			{
				return result;
			}
			float num4 = (u - 0.72f) / 0.27999997f * 6.283f * 0.88f;
			float curlR = l.CurlR;
			return new Vec3(result.X + dx * curlR * (float)Math.Sin(num4), result.Y + dy * curlR * (float)Math.Sin(num4), result.Z + curlR * (1f - (float)Math.Cos(num4)));
		}

		private void StepLines(float dt, Gusts g, FxSink sink, float night)
		{
			float num = 1f - 0.6f * night;
			uint color = TerrainCache.Lerp(TerrainCache.Rgb(244, 248, 255), TerrainCache.Rgb(150, 166, 196), night);
			for (int num2 = _lines.Count - 1; num2 >= 0; num2--)
			{
				Line l = _lines[num2];
				l.Age += dt;
				if (l.Age >= l.Life)
				{
					_lines.RemoveAt(num2);
				}
				else
				{
					l.O = new Vec3(l.O.X + g.DirX * l.Speed * dt * 0.35f, l.O.Y + g.DirY * l.Speed * dt * 0.35f, l.O.Z);
					_lines[num2] = l;
					if (sink.Streaks != null)
					{
						float num3 = BlastFx.Smooth(l.Age / (l.Life * 0.55f));
						float num4 = BlastFx.Smooth((l.Age - l.Life * 0.3f) / (l.Life * 0.7f));
						if (!(num3 - num4 < 0.02f))
						{
							float num5 = 0.6f * Math.Min(1f, 0.35f + g.Strength) * num;
							Vec3 a = default(Vec3);
							float alphaA = 0f;
							for (int i = 0; i <= 14; i++)
							{
								float u = num4 + (num3 - num4) * (float)i / 14f;
								Vec3 vec = LinePoint(in l, u, g.DirX, g.DirY);
								float num6 = sink.Ground(vec.X, vec.Y) + 0.12f;
								if (vec.Z < num6)
								{
									vec = new Vec3(vec.X, vec.Y, num6);
								}
								float num7 = num5 * (float)Math.Pow(Math.Sin(Math.PI * (double)i / 14.0), 0.6);
								if (i > 0)
								{
									sink.Line(a, vec, color, alphaA, num7, 0.028f, 0.55f);
								}
								a = vec;
								alphaA = num7;
							}
						}
					}
				}
			}
		}

		private void SpawnBits(float dt, Gusts g, FxSink sink, TerrainCache t, float fov)
		{
			float strength = g.Strength;
			if (strength < 0.2f && Vortices.Count == 0)
			{
				return;
			}
			int num = 110 + (int)(80f * g.Fury);
			_bitClock += dt * (46f * (float)Math.Pow(Math.Max(0f, strength - 0.15f), 1.2999999523162842) + ((Vortices.Count > 0) ? 40f : 0f));
			while (_bitClock > 1f && _bits.Count < num)
			{
				_bitClock -= 1f;
				float x;
				float y;
				if (Vortices.Count > 0 && R() < 0.65f)
				{
					if (!NearTwister(sink, t, 0.5f, 4f, 19f, out x, out y, out var _))
					{
						continue;
					}
				}
				else
				{
					if (strength < 0.2f)
					{
						continue;
					}
					InView(sink, fov, 1.2f, 13f, out var x2, out var y2);
					float num2 = g.Speed * (0.4f + 0.8f * R());
					x = x2 - g.DirX * num2;
					y = y2 - g.DirY * num2;
				}
				int num3 = (int)x;
				int num4 = (int)y;
				if (num3 < 0 || num4 < 0 || num3 >= t.Width || num4 >= t.Height)
				{
					continue;
				}
				int num5 = num4 * t.Width + num3;
				if ((t.Flags[num5] & 3) != 0)
				{
					continue;
				}
				byte b = (byte)((t.Snow.On && t.Snow.Depth[num5] > 150) ? 5 : t.Materials[num5]);
				uint num6 = t.Colors[num5];
				float num7 = R();
				BitKind bitKind;
				uint color;
				switch (b)
				{
				case 5:
					bitKind = BitKind.Flake;
					color = TerrainCache.Rgb(240, 246, 255);
					break;
				case 2:
					bitKind = ((!(num7 < 0.92f)) ? BitKind.Blade : BitKind.Grain);
					color = ((num7 < 0.92f) ? TerrainCache.Lerp(num6, TerrainCache.Rgb(236, 214, 160), 0.4f) : TerrainCache.Rgb(170, 150, 90));
					break;
				case 1:
				case 7:
					if (num7 < 0.55f)
					{
						bitKind = BitKind.Leaf;
						color = LeafColour(num6, R());
					}
					else if (num7 < 0.85f)
					{
						bitKind = BitKind.Blade;
						color = TerrainCache.Scale(TerrainCache.Lerp(num6, TerrainCache.Rgb(150, 190, 80), 0.3f), 1.1f);
					}
					else
					{
						bitKind = BitKind.Petal;
						color = Petal(R());
					}
					break;
				default:
					if (num7 < 0.7f)
					{
						bitKind = BitKind.Grain;
						color = TerrainCache.Lerp(num6, TerrainCache.Rgb(150, 132, 106), 0.4f);
					}
					else
					{
						bitKind = BitKind.Leaf;
						color = LeafColour(TerrainCache.Rgb(110, 140, 60), R());
					}
					break;
				}
				bool flag = bitKind == BitKind.Grain || bitKind == BitKind.Flake;
				float z = sink.Ground(x, y) + (flag ? (0.05f + 0.6f * R() * R()) : (0.15f + 2.4f * R() * R()));
				Air(x, y, g, out var vx, out var vy, out var _);
				List<Bit> bits = _bits;
				Bit item = new Bit
				{
					Pos = new Vec3(x, y, z),
					Vel = new Vec3(vx * 0.5f, vy * 0.5f, 0.5f + R()),
					Life = 2.2f + 2f * R()
				};
				float size;
				switch (bitKind)
				{
				default:
					size = (flag ? (0.05f + 0.03f * R()) : 0.11f);
					break;
				case BitKind.Petal:
					size = 0.07f;
					break;
				case BitKind.Leaf:
					size = 0.11f + 0.06f * R();
					break;
				}
				item.Size = size;
				item.Spin = R() * 6.283f;
				item.SpinRate = (6f + 8f * R()) * ((R() < 0.5f) ? (-1f) : 1f);
				item.Flutter = R() * 6.283f;
				item.Color = color;
				item.Kind = bitKind;
				item.Fall = (flag ? 4f : ((bitKind == BitKind.Leaf) ? 0.7f : 1.1f));
				bits.Add(item);
			}
			if (_bitClock > 1f)
			{
				_bitClock = 1f;
			}
		}

		private static uint LeafColour(uint ground, float r)
		{
			if (r < 0.08f)
			{
				return TerrainCache.Rgb(196, 160, 64);
			}
			if (r < 0.13f)
			{
				return TerrainCache.Rgb(150, 98, 52);
			}
			return TerrainCache.Scale(TerrainCache.Lerp(ground, TerrainCache.Rgb(90, 150, 60), 0.35f), 0.8f + 0.5f * r);
		}

		private static uint Petal(float r)
		{
			if (!(r < 0.4f))
			{
				if (!(r < 0.7f))
				{
					return TerrainCache.Rgb(250, 222, 90);
				}
				return TerrainCache.Rgb(250, 176, 206);
			}
			return TerrainCache.Rgb(250, 246, 236);
		}

		private void DropLeaves(float dt, Gusts g, FxSink sink)
		{
			if (Crowns.Count == 0 || LeafAmount <= 0f)
			{
				return;
			}
			float num = 1f + 2.5f * SeasonLook.Autumn - 0.5f * SeasonLook.Winter;
			_leafClock += dt * (0.25f + 0.035f * (float)Math.Min(40, Crowns.Count)) * (1f + 6f * g.Strength) * num * LeafAmount;
			while (_leafClock > 1f && _bits.Count < 160)
			{
				_leafClock -= 1f;
				Crown c = Crowns[_rnd.Next(Crowns.Count)];
				if (c.Snowy && R() < 0.85f)
				{
					SnowFrom(in c);
				}
				else if (c.Deciduous || !(R() < 0.7f * SeasonLook.Autumn))
				{
					bool flag = c.Deciduous && R() < 0.4f * SeasonLook.Spring;
					float num2 = R() * 6.283f;
					float num3 = c.Radius * (0.3f + 0.6f * R());
					_bits.Add(new Bit
					{
						Pos = new Vec3(c.X + (float)Math.Cos(num2) * num3, c.Y + (float)Math.Sin(num2) * num3, c.Z - 0.3f * R()),
						Vel = new Vec3(0f, 0f, -0.3f),
						Life = 7f + 4f * R(),
						Size = 0.12f + 0.06f * R(),
						Spin = R() * 6.283f,
						SpinRate = (3f + 4f * R()) * ((R() < 0.5f) ? (-1f) : 1f),
						Flutter = R() * 6.283f,
						Color = (flag ? Petal(R() * 0.7f) : TerrainCache.Scale(c.Leaf, 0.85f + 0.35f * R())),
						Kind = BitKind.Leaf,
						Fall = (c.Glow ? 0.3f : 0.45f),
						Drifting = true,
						Glow = (c.Glow && !flag)
					});
				}
			}
			if (_leafClock > 1f)
			{
				_leafClock = 1f;
			}
		}

		private void SnowFrom(in Crown c)
		{
			float num = R() * 6.283f;
			float num2 = c.Radius * (0.5f + 0.45f * R());
			float num3 = c.X + (float)Math.Cos(num) * num2;
			float num4 = c.Y + (float)Math.Sin(num) * num2;
			float num5 = c.Z - c.Radius * 0.2f * R();
			int num6 = 4 + _rnd.Next(4);
			for (int i = 0; i < num6; i++)
			{
				if (_bits.Count >= 160)
				{
					break;
				}
				_bits.Add(new Bit
				{
					Pos = new Vec3(num3 + (R() - 0.5f) * 0.25f, num4 + (R() - 0.5f) * 0.25f, num5 + (R() - 0.5f) * 0.2f),
					Vel = new Vec3((R() - 0.5f) * 0.3f, (R() - 0.5f) * 0.3f, -0.6f - 0.6f * R()),
					Life = 3f + 1.5f * R(),
					Size = 0.05f + 0.05f * R(),
					Spin = 0f,
					SpinRate = 0f,
					Flutter = R() * 6.283f,
					Color = TerrainCache.Rgb(236, 242, 250),
					Kind = BitKind.Flake,
					Fall = 1.1f + 0.5f * R(),
					Drifting = true
				});
			}
		}

		private void StepBits(float dt, float time, Gusts g, FxSink sink)
		{
			float num = ((Vortices.Count > 0) ? 26f : 22f);
			for (int num2 = _bits.Count - 1; num2 >= 0; num2--)
			{
				Bit b = _bits[num2];
				if (dt > 0f)
				{
					b.Age += dt;
					float num3 = b.Pos.X - sink.EyeX;
					float num4 = b.Pos.Y - sink.EyeY;
					if (b.Age >= b.Life || num3 * num3 + num4 * num4 > num * num)
					{
						_bits.RemoveAt(num2);
						continue;
					}
					float num5 = sink.Ground(b.Pos.X, b.Pos.Y);
					if (!b.Landed)
					{
						b.Spin += b.SpinRate * dt;
						Air(b.Pos.X, b.Pos.Y, g, out var vx, out var vy, out var vz);
						float num6 = (float)Math.Sqrt(vx * vx + vy * vy);
						float num7 = ((num6 > 0.001f) ? (vx / num6) : g.DirX);
						float num8 = ((num6 > 0.001f) ? (vy / num6) : g.DirY);
						float num9 = (b.Drifting ? (0.25f + 0.75f * g.Strength) : (0.75f + 0.4f * (float)Math.Sin(b.Flutter)));
						float num10 = (float)Math.Sin(time * (b.Drifting ? 2.2f : 4.3f) + b.Flutter);
						float num11 = vx * num9 - num8 * num10 * (b.Drifting ? 0.7f : 1.3f);
						float num12 = vy * num9 + num7 * num10 * (b.Drifting ? 0.7f : 1.3f);
						float num13 = Math.Min(1f, num6 / 11f);
						float num14 = ((b.Kind != BitKind.Grain && b.Kind != BitKind.Flake) ? (0.9f * num13 * (float)Math.Sin(time * 1.3f + b.Flutter * 2f)) : ((b.Pos.Z - num5 < 0.08f) ? (1.2f + 2f * num13 * Math.Abs((float)Math.Sin(b.Flutter * 3f + time))) : 0f));
						if (b.Pos.Z - num5 < 7f)
						{
							num14 += vz * ((b.Kind == BitKind.Grain || b.Kind == BitKind.Flake) ? 0.5f : 0.85f);
						}
						float num15 = 0f - b.Fall + num14 + ((b.Kind == BitKind.Leaf) ? (0.35f * (float)Math.Sin(time * 4.4f + b.Flutter)) : 0f);
						float num16 = 1f - (float)Math.Exp((0f - ((b.Kind == BitKind.Grain) ? 6f : 2.6f)) * dt);
						b.Vel = new Vec3(b.Vel.X + (num11 - b.Vel.X) * num16, b.Vel.Y + (num12 - b.Vel.Y) * num16, b.Vel.Z + (num15 - b.Vel.Z) * num16);
						b.Pos += b.Vel * dt;
						if (b.Pos.Z <= num5 + 0.02f)
						{
							b.Pos = new Vec3(b.Pos.X, b.Pos.Y, num5 + 0.02f);
							if (b.Kind == BitKind.Grain || b.Kind == BitKind.Flake)
							{
								b.Vel = new Vec3(b.Vel.X, b.Vel.Y, Math.Abs(b.Vel.Z) * 0.4f);
							}
							else if (g.Strength < 0.35f)
							{
								b.Landed = true;
								b.Life = Math.Min(b.Life, b.Age + 2.5f + 2f * R());
							}
							else
							{
								b.Vel = new Vec3(b.Vel.X, b.Vel.Y, 0.8f + 1.2f * g.Strength);
							}
						}
					}
					else if (g.Strength > 0.45f)
					{
						b.Landed = false;
						Air(b.Pos.X, b.Pos.Y, g, out var vx2, out var vy2, out var _);
						b.Vel = new Vec3(vx2 * 0.4f, vy2 * 0.4f, 1.5f);
					}
					_bits[num2] = b;
				}
				Draw(sink, in b);
			}
		}

		private void Draw(FxSink sink, in Bit b)
		{
			float num = b.Pos.X - sink.EyeX;
			float num2 = b.Pos.Y - sink.EyeY;
			float num3 = b.Pos.Z - sink.EyeZ;
			if (num * num + num2 * num2 + num3 * num3 < 0.80999994f)
			{
				return;
			}
			float num4 = b.Age / b.Life;
			float num5 = Math.Min(1f, b.Age * 4f) * (1f - BlastFx.Smooth((num4 - 0.85f) / 0.15f));
			if (num5 < 0.3f)
			{
				return;
			}
			float size = b.Size;
			switch (b.Kind)
			{
			case BitKind.Leaf:
			case BitKind.Petal:
			{
				float num6 = (float)Math.Cos(b.Spin);
				int num7 = ((!b.Landed) ? ((Math.Abs(num6) < 0.35f) ? 2 : ((!(num6 > 0f)) ? 1 : 0)) : 0);
				uint multiply = ((num7 == 1) ? TerrainCache.Scale(b.Color, 0.78f) : b.Color);
				sink.Billboards.Add(new Billboard
				{
					X = b.Pos.X,
					Y = b.Pos.Y,
					Z = b.Pos.Z,
					GroundZ = b.Pos.Z,
					Width = size,
					Height = size * ((num7 == 2) ? 0.28f : ((b.Kind == BitKind.Petal) ? 1f : 0.62f)),
					Image = WindArt.Leaf((b.Kind == BitKind.Petal) ? 3 : num7),
					Kind = BillboardKind.Facing,
					Tag = -1,
					Multiply = multiply,
					Flip = (Math.Sin(b.Spin * 0.5f) > 0.0),
					Emissive = b.Glow
				});
				if (b.Glow)
				{
					float num8 = (0.25f + 0.75f * _night) * (0.7f + 0.3f * Math.Abs(num6)) * num5;
					sink.Billboards.Add(new Billboard
					{
						X = b.Pos.X,
						Y = b.Pos.Y,
						Z = b.Pos.Z - size * 0.9f,
						GroundZ = b.Pos.Z,
						Width = size * 2.8f,
						Height = size * 2.8f,
						Image = BlastFx.GlowSprite,
						Kind = BillboardKind.Facing,
						Tag = -1,
						Multiply = b.Color,
						Emissive = true,
						Alpha = (byte)Math.Max(1, Math.Min(255, (int)(150f * num8))),
						Additive = true
					});
				}
				break;
			}
			case BitKind.Blade:
				sink.Billboards.Add(new Billboard
				{
					X = b.Pos.X,
					Y = b.Pos.Y,
					Z = b.Pos.Z,
					GroundZ = b.Pos.Z,
					Width = size * 0.6f,
					Height = size,
					Image = WindArt.Blade((int)(Math.Abs(b.Spin) * 2f) & 3),
					Kind = BillboardKind.Facing,
					Tag = -1,
					Multiply = b.Color
				});
				break;
			default:
				sink.Billboards.Add(new Billboard
				{
					X = b.Pos.X,
					Y = b.Pos.Y,
					Z = b.Pos.Z,
					GroundZ = b.Pos.Z,
					Width = size,
					Height = size,
					Image = BlastFx.Spark((b.Kind != BitKind.Flake) ? 1 : 2),
					Kind = BillboardKind.Facing,
					Tag = -1,
					Multiply = b.Color
				});
				break;
			}
		}

		private void SpawnDrifts(float dt, Gusts g, FxSink sink, TerrainCache t, float fov)
		{
			float strength = g.Strength;
			bool flag = Vortices.Count > 0;
			if (strength < 0.45f && !flag)
			{
				return;
			}
			_driftClock += dt * (4f * Math.Max(0f, strength - 0.4f) + (flag ? 7f : 0f));
			while (_driftClock > 1f && _drifts.Count < (flag ? 24 : 14))
			{
				_driftClock -= 1f;
				bool flag2 = flag && R() < 0.7f;
				float x;
				float y;
				if (flag2)
				{
					if (!NearTwister(sink, t, 0.9f, 3.5f, 24f, out x, out y, out var _))
					{
						continue;
					}
				}
				else
				{
					if (strength < 0.45f)
					{
						continue;
					}
					InView(sink, fov, 4f, 22f, out var x2, out var y2);
					x = x2 - g.DirX * g.Speed;
					y = y2 - g.DirY * g.Speed;
				}
				int num = (int)x;
				int num2 = (int)y;
				if (num >= 0 && num2 >= 0 && num < t.Width && num2 < t.Height)
				{
					int num3 = num2 * t.Width + num;
					byte b = (byte)((t.Snow.On && t.Snow.Depth[num3] > 150) ? 5 : t.Materials[num3]);
					bool flag3 = b == 5;
					if ((flag2 || flag3 || b == 2 || b == 3 || b == 6 || b == 4) && (t.Flags[num3] & 3) == 0)
					{
						uint color = (flag3 ? TerrainCache.Rgb(236, 242, 250) : ((flag2 && (b == 1 || b == 7)) ? TerrainCache.Lerp(t.Colors[num3], TerrainCache.Rgb(140, 110, 80), 0.7f) : TerrainCache.Lerp(t.Colors[num3], TerrainCache.Rgb(214, 200, 172), 0.45f)));
						float num4 = 1f + 0.8f * R();
						_drifts.Add(new Drift
						{
							Pos = new Vec3(x, y, sink.Ground(x, y) + 0.25f + 0.3f * R()),
							Life = 2.4f + 1.6f * R(),
							Size = num4,
							EndSize = num4 * (2.4f + R()),
							Speed = 0.75f + 0.3f * R(),
							Color = color,
							Variant = _rnd.Next(6),
							Alpha = (byte)(flag3 ? 90u : (flag2 ? 95u : 70u))
						});
					}
				}
			}
			if (_driftClock > 1f)
			{
				_driftClock = 1f;
			}
		}

		private void StepDrifts(float dt, Gusts g, FxSink sink)
		{
			for (int num = _drifts.Count - 1; num >= 0; num--)
			{
				Drift value = _drifts[num];
				value.Age += dt;
				if (value.Age >= value.Life)
				{
					_drifts.RemoveAt(num);
				}
				else
				{
					Air(value.Pos.X, value.Pos.Y, g, out var vx, out var vy, out var _);
					float num2 = value.Pos.X + vx * value.Speed * dt;
					float num3 = value.Pos.Y + vy * value.Speed * dt;
					float num4 = sink.Ground(num2, num3);
					value.Pos = new Vec3(num2, num3, Math.Max(value.Pos.Z + 0.25f * dt, num4 + 0.2f));
					_drifts[num] = value;
					float num5 = value.Age / value.Life;
					float num6 = (float)(int)value.Alpha * Math.Min(1f, value.Age / 0.5f) * (1f - BlastFx.Smooth((num5 - 0.4f) / 0.6f));
					if (!(num6 < 2f))
					{
						float num7 = value.Size + (value.EndSize - value.Size) * num5;
						sink.Billboards.Add(new Billboard
						{
							X = value.Pos.X,
							Y = value.Pos.Y,
							Z = value.Pos.Z - num7 * 0.3f,
							GroundZ = num4,
							Width = num7 * 1.6f,
							Height = num7 * 0.7f,
							Image = BlastFx.Soft(value.Variant),
							Kind = BillboardKind.Facing,
							Tag = -1,
							Multiply = value.Color,
							Alpha = (byte)num6,
							Flip = ((value.Variant & 1) != 0)
						});
					}
				}
			}
		}
	}
}
