using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class AmbientFx
	{
		private struct Bird
		{
			public float Ox;

			public float Oy;

			public float Oz;

			public float Phase;

			public float Rate;

			public float Wander;
		}

		private sealed class Flock
		{
			public float X;

			public float Y;

			public float Z;

			public float DirX;

			public float DirY;

			public float Speed;

			public float Age;

			public float Life;

			public bool Gulls;

			public bool Vee;

			public uint Seed;

			public readonly List<Bird> Birds = new List<Bird>();
		}

		private struct Mote
		{
			public Vec3 Pos;

			public Vec3 Vel;

			public float Phase;

			public float Rate;
		}

		private readonly List<Flock> _flocks = new List<Flock>();

		private readonly List<Mote> _motes = new List<Mote>(64);

		private readonly Random _rnd;

		private float _nextFlock;

		public int Flocks => _flocks.Count;

		public int Birds
		{
			get
			{
				int num = 0;
				foreach (Flock flock in _flocks)
				{
					num += flock.Birds.Count;
				}
				return num;
			}
		}

		public int Motes => _motes.Count;

		public float NextFlock => _nextFlock;

		public AmbientFx(int seed)
		{
			_rnd = new Random(seed);
			_nextFlock = 20f + 40f * R();
		}

		private float R()
		{
			return (float)_rnd.NextDouble();
		}

		public void Clear()
		{
			_flocks.Clear();
			_motes.Clear();
		}

		public void Step(float dt, float time, FxSink sink, Gusts g, float fov, float sunUp, float golden, float wet, bool cold, bool sea, float green)
		{
			if (sink.Ground != null)
			{
				StepFlocks(dt, time, sink, fov, sunUp, wet, sea);
				StepMotes(dt, time, sink, g, sunUp, golden, wet, cold, green);
			}
		}

		public void FlockNow(FxSink sink, float fov, bool gulls)
		{
			StartFlock(sink, fov, gulls);
		}

		private void StartFlock(FxSink sink, float fov, bool gulls)
		{
			float num = sink.Yaw + (R() - 0.5f) * fov * 0.6f;
			float num2 = 28f + 30f * R();
			float num3 = sink.EyeX + (float)Math.Cos(num) * num2;
			float num4 = sink.EyeY + (float)Math.Sin(num) * num2;
			float num5 = num + ((R() < 0.5f) ? 1f : (-1f)) * (1.1f + 0.9f * R());
			Flock flock = new Flock
			{
				DirX = (float)Math.Cos(num5),
				DirY = (float)Math.Sin(num5),
				Speed = (gulls ? (6f + 2f * R()) : (8f + 3f * R())),
				Gulls = gulls,
				Vee = (gulls ? (R() < 0.7f) : (R() < 0.2f)),
				Seed = (uint)_rnd.Next()
			};
			float num6 = 75f;
			flock.X = num3 - flock.DirX * num6;
			flock.Y = num4 - flock.DirY * num6;
			flock.Z = sink.Ground(num3, num4) + 14f + 12f * R();
			flock.Life = num6 * 2f / flock.Speed;
			int num7 = (gulls ? (4 + _rnd.Next(5)) : (6 + _rnd.Next(8)));
			for (int i = 0; i < num7; i++)
			{
				Bird item = new Bird
				{
					Phase = R() * 6.283f,
					Rate = (gulls ? 5f : 9f) * (0.85f + 0.3f * R()),
					Wander = R() * 6.283f
				};
				if (flock.Vee)
				{
					int num8 = (i + 1) / 2;
					int num9 = ((i % 2 == 0) ? 1 : (-1));
					item.Ox = (float)(-num8) * 1.3f;
					item.Oy = ((i == 0) ? 0f : ((float)(num9 * num8) * 1f));
					item.Oz = (R() - 0.5f) * 0.3f;
				}
				else
				{
					item.Ox = (R() - 0.5f) * 7f;
					item.Oy = (R() - 0.5f) * 6f;
					item.Oz = (R() - 0.5f) * 2.5f;
				}
				flock.Birds.Add(item);
			}
			_flocks.Add(flock);
		}

		private void StepFlocks(float dt, float time, FxSink sink, float fov, float sunUp, float wet, bool sea)
		{
			if (dt > 0f && sunUp > 0.4f && wet < 0.2f)
			{
				_nextFlock -= dt;
				if (_nextFlock <= 0f)
				{
					_nextFlock = 60f + 90f * R();
					if (_flocks.Count < 2)
					{
						StartFlock(sink, fov, sea && R() < 0.75f);
					}
				}
			}
			for (int num = _flocks.Count - 1; num >= 0; num--)
			{
				Flock flock = _flocks[num];
				flock.Age += dt;
				flock.X += flock.DirX * flock.Speed * dt;
				flock.Y += flock.DirY * flock.Speed * dt;
				float num2 = flock.X - sink.EyeX;
				float num3 = flock.Y - sink.EyeY;
				if (flock.Age > flock.Life || num2 * num2 + num3 * num3 > 25600f)
				{
					_flocks.RemoveAt(num);
				}
				else if (!(Math.Min(1f, flock.Age / 2f) * Math.Min(1f, (flock.Life - flock.Age) / 2f) < 0.15f))
				{
					float num4 = 0f - flock.DirY;
					float dirX = flock.DirX;
					for (int i = 0; i < flock.Birds.Count; i++)
					{
						Bird bird = flock.Birds[i];
						float num5 = (flock.Vee ? 0f : (1.2f * (float)Math.Sin(time * 0.5f + bird.Wander)));
						float num6 = (flock.Vee ? 0f : (1.2f * (float)Math.Cos(time * 0.43f + bird.Wander * 1.3f)));
						float num7 = 0.25f * (float)Math.Sin(time * 1.1f + bird.Wander);
						float x = flock.X + flock.DirX * (bird.Ox + num5) + num4 * (bird.Oy + num6);
						float y = flock.Y + flock.DirY * (bird.Ox + num5) + dirX * (bird.Oy + num6);
						float num8 = flock.Z + bird.Oz + num7;
						int num9 = (((float)Math.Sin(time * 0.7f + bird.Wander * 2f) > (flock.Gulls ? 0.1f : 0.6f)) ? 1 : ((int)((time * bird.Rate + bird.Phase) / 6.283f * 4f) & 3));
						if (num9 == 3)
						{
							num9 = 1;
						}
						float num10 = (flock.Gulls ? 1.25f : 0.85f);
						sink.Billboards.Add(new Billboard
						{
							X = x,
							Y = y,
							Z = num8,
							GroundZ = num8,
							Width = num10,
							Height = num10 * 0.5f,
							Image = AmbientArt.Bird(num9, flock.Gulls),
							Kind = BillboardKind.Facing,
							Tag = -1,
							Far = true,
							Flip = (((int)bird.Wander & 1) != 0)
						});
					}
				}
			}
		}

		private void StepMotes(float dt, float time, FxSink sink, Gusts g, float sunUp, float golden, float wet, bool cold, float green)
		{
			float val = (cold ? 0f : (sunUp * (1f - wet) * green * (1f - 0.5f * g.Strength)));
			int num = (int)(Math.Min(1f, val) * 16f);
			while (_motes.Count > num)
			{
				_motes.RemoveAt(_motes.Count - 1);
			}
			while (_motes.Count < num)
			{
				_motes.Add(NewMote(sink, anywhere: true));
			}
			if (_motes.Count == 0)
			{
				return;
			}
			uint multiply = TerrainCache.Lerp(TerrainCache.Rgb(250, 250, 244), TerrainCache.Rgb(255, 226, 176), golden * 0.6f);
			float num2 = g.DirX * g.Speed * g.Strength;
			float num3 = g.DirY * g.Speed * g.Strength;
			for (int i = 0; i < _motes.Count; i++)
			{
				Mote value = _motes[i];
				if (dt > 0f)
				{
					float num4 = 1f - (float)Math.Exp(-0.8f * dt);
					float num5 = 0.25f * (float)Math.Sin(time * 0.31f + value.Phase * 3f) + num2;
					float num6 = 0.25f * (float)Math.Cos(time * 0.27f + value.Phase * 2f) + num3;
					float num7 = 0.1f * (float)Math.Sin(time * 0.5f + value.Phase) - 0.03f;
					value.Vel = new Vec3(value.Vel.X + (num5 - value.Vel.X) * num4, value.Vel.Y + (num6 - value.Vel.Y) * num4, value.Vel.Z + (num7 - value.Vel.Z) * num4);
					value.Pos += value.Vel * dt;
					float num8 = value.Pos.X - sink.EyeX;
					float num9 = value.Pos.Y - sink.EyeY;
					float num10 = sink.Ground(value.Pos.X, value.Pos.Y);
					if (num8 * num8 + num9 * num9 > 81f || value.Pos.Z < num10 + 0.15f || value.Pos.Z > num10 + 3.5f)
					{
						value = NewMote(sink, anywhere: false);
					}
					_motes[i] = value;
				}
				float num11 = value.Pos.X - sink.EyeX;
				float num12 = value.Pos.Y - sink.EyeY;
				float num13 = (float)Math.Sqrt(num11 * num11 + num12 * num12);
				float num14 = Math.Min(1f, (8.5f - num13) / 2f) * Math.Min(1f, (num13 - 0.8f) / 0.6f);
				if (!(num14 < 0.1f))
				{
					bool turned = Math.Sin(time * value.Rate * 0.4f + value.Phase) > 0.0;
					sink.Billboards.Add(new Billboard
					{
						X = value.Pos.X,
						Y = value.Pos.Y,
						Z = value.Pos.Z,
						GroundZ = value.Pos.Z,
						Width = 0.075f,
						Height = 0.075f,
						Image = AmbientArt.Fluff(turned),
						Kind = BillboardKind.Facing,
						Tag = -1,
						Multiply = multiply,
						Alpha = (byte)Math.Max(2, (int)(225f * num14))
					});
				}
			}
		}

		private Mote NewMote(FxSink sink, bool anywhere)
		{
			float num = (anywhere ? (sink.Yaw + (R() - 0.5f) * 3.6f) : (sink.Yaw + (R() - 0.5f) * 2.4f));
			float num2 = (anywhere ? (1.5f + 6.5f * (float)Math.Sqrt(R())) : (3f + 5f * R()));
			float num3 = sink.EyeX + (float)Math.Cos(num) * num2;
			float num4 = sink.EyeY + (float)Math.Sin(num) * num2;
			float z = sink.Ground(num3, num4) + 0.4f + 2.4f * R();
			return new Mote
			{
				Pos = new Vec3(num3, num4, z),
				Phase = R() * 6.283f,
				Rate = 1.5f + 2.5f * R()
			};
		}
	}
}
