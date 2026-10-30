using System;
using System.Collections.Generic;
using System.Globalization;

namespace FirstPerson.Core
{
	public sealed class TorchFire
	{
		private sealed class Shape
		{
			public MeshModel Model;

			public Vec3 Flame;

			public Vec3 Head;

			public float Rim;

			public int Ember = -1;

			public int RimSlot = -1;

			public int Coal = -1;

			public int Fires = 1;

			public float Spread;

			public int Glow = -1;

			public float Reach = 6.5f;
		}

		public const string ModelName = "held_torch";

		private static Shape _shape;

		private static readonly Dictionary<MeshModel, Shape> Shapes = new Dictionary<MeshModel, Shape>();

		public bool Lit;

		public float Sputter;

		public Vec3 Flame;

		public Vec3 Head;

		public Vec3 Up = new Vec3(0f, 0f, 1f);

		public float Scale = 1f;

		public float Speed;

		public float Fan;

		public bool Shown;

		public float PosedAt = -1f;

		public float Grown;

		private bool _moving;

		private Vec3 _vel;

		private float _lastTime = -1f;

		private uint[] _palette;

		private readonly Random _rng = new Random(9);

		private float _emberClock;

		private float _smokeClock;

		private bool _wasLit;

		private int _douse;

		private Shape _my;

		private const uint MainSeed = 7u;

		private const uint ScrapSeed = 66u;

		private const float ScrapOffset = 3.5f;

		private const float MostLean = 0.7f;

		private static readonly uint EmberHot = TerrainCache.Rgb(255, 230, 140);

		private static readonly uint EmberCold = TerrainCache.Rgb(200, 58, 30);

		private static readonly uint SmokeDark = TerrainCache.Rgb(58, 44, 62);

		private static readonly uint SmokeFar = TerrainCache.Rgb(104, 94, 112);

		private static readonly uint SteamHot = TerrainCache.Rgb(214, 210, 206);

		private static readonly uint SteamFar = TerrainCache.Rgb(150, 146, 148);

		public static bool IsModel(MeshModel m)
		{
			if (m != null)
			{
				return m.Name == "held_torch";
			}
			return false;
		}

		public static bool Burns(MeshModel m)
		{
			if (!IsModel(m))
			{
				if (m != null && m.Info("kind") == "light")
				{
					return !string.IsNullOrEmpty(m.Info("flame"));
				}
				return false;
			}
			return true;
		}

		private static Shape ShapeOf(MeshModel m)
		{
			Shape value = _shape;
			if (value != null && value.Model == m)
			{
				return value;
			}
			if (Shapes.TryGetValue(m, out value))
			{
				_shape = value;
				return value;
			}
			value = new Shape
			{
				Model = m,
				Flame = InfoVec(m, "flame", new Vec3(0f, 0f, 0.244f)),
				Head = InfoVec(m, "head", new Vec3(0f, 0f, 0.176f)),
				Rim = m.InfoFloat("rim", 0.036f),
				Ember = m.SlotIndex("ember"),
				RimSlot = m.SlotIndex("rim"),
				Coal = m.SlotIndex("coal"),
				Fires = (int)m.InfoFloat("fires", 1f),
				Spread = m.InfoFloat("spread", 0f),
				Glow = (string.IsNullOrEmpty(m.Info("glow")) ? (-1) : m.SlotIndex(m.Info("glow"))),
				Reach = m.InfoFloat("reach", 6.5f)
			};
			if (Shapes.Count > 32)
			{
				Shapes.Clear();
			}
			Shapes[m] = value;
			_shape = value;
			return value;
		}

		private static Vec3 InfoVec(MeshModel m, string key, Vec3 fallback)
		{
			string text = m.Info(key);
			if (string.IsNullOrEmpty(text))
			{
				return fallback;
			}
			string[] array = text.Split(',');
			CultureInfo invariantCulture = CultureInfo.InvariantCulture;
			if (array.Length == 3 && float.TryParse(array[0], NumberStyles.Float, invariantCulture, out var result) && float.TryParse(array[1], NumberStyles.Float, invariantCulture, out var result2) && float.TryParse(array[2], NumberStyles.Float, invariantCulture, out var result3))
			{
				return new Vec3(result, result2, result3);
			}
			return fallback;
		}

		public void Catch()
		{
			if (!Lit || !(Grown > 0.5f))
			{
				Lit = true;
				Grown = 0.05f;
			}
		}

		public void Douse()
		{
			if (Lit)
			{
				Lit = false;
				Grown = 0f;
				_douse = 8;
			}
		}

		public void Pose(MeshInstance inst, float time)
		{
			MeshModel model = inst.Model;
			if (!Burns(model))
			{
				Shown = false;
				return;
			}
			Shape shape = (_my = ShapeOf(model));
			float num = ((_lastTime < 0f) ? 0f : (time - _lastTime));
			_lastTime = time;
			PosedAt = time;
			Mat3 basis = inst.Basis;
			Vec3 position = inst.Position;
			Vec3 vec = position + basis.Mul(shape.Flame);
			Scale = Math.Max(0.001f, Len(basis.C2));
			if (num <= 0f || num > 0.25f || Len(vec - Flame) > 40f * Scale)
			{
				_moving = false;
			}
			if (_moving)
			{
				float num2 = 1f - (float)Math.Exp((0f - num) / 0.06f);
				_vel += ((vec - Flame) * (1f / num) - _vel) * num2;
			}
			else
			{
				_vel = new Vec3(0f, 0f, 0f);
			}
			Flame = vec;
			Head = position + basis.Mul(shape.Head);
			Up = basis.C2 * (1f / Scale);
			Speed = Len(_vel);
			Fan = Math.Min(1f, Speed / (Scale * 3f));
			_moving = true;
			Shown = true;
			if (Lit)
			{
				Grown = ((num > 0f) ? Math.Min(1f, Grown + num / 0.45f) : Math.Max(Grown, 0.05f));
			}
			else
			{
				Grown = 0f;
			}
			if (_palette == null || _palette.Length != model.SlotNames.Length)
			{
				_palette = new uint[model.SlotNames.Length];
			}
			Array.Clear(_palette, 0, _palette.Length);
			if (Lit)
			{
				float num3 = 0.5f + 0.3f * Fm.Sin(time * 3.7f) + 0.2f * Fm.Sin(time * 9.1f + 1.1f);
				if (shape.Ember >= 0)
				{
					_palette[shape.Ember] = TerrainCache.Lerp(TerrainCache.Rgb(214, 74, 26), TerrainCache.Rgb(255, 214, 110), num3 * (1f - 0.5f * Sputter));
				}
				if (shape.RimSlot >= 0)
				{
					_palette[shape.RimSlot] = TerrainCache.Lerp(model.SlotColor[shape.RimSlot], TerrainCache.Rgb(232, 142, 76), 0.35f * Grown * (1f - 0.5f * Sputter));
				}
				if (shape.Glow >= 0)
				{
					_palette[shape.Glow] = TerrainCache.Lerp(TerrainCache.Rgb(196, 98, 36), model.SlotColor[shape.Glow], 0.5f + 0.5f * num3);
				}
			}
			else if (shape.Glow >= 0)
			{
				_palette[shape.Glow] = TerrainCache.Rgb(70, 62, 52);
			}
			else if (shape.Ember >= 0)
			{
				_palette[shape.Ember] = ((_douse > 0 || _wasLit) ? TerrainCache.Rgb(70, 40, 32) : TerrainCache.Rgb(46, 30, 26));
			}
			_wasLit = Lit;
			inst.Palette = _palette;
			inst.PartRotation = null;
			inst.PartScale = null;
			inst.PartOffset = null;
			inst.Hidden = 0uL;
		}

		private static float Len(Vec3 v)
		{
			return (float)Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
		}

		private float Strength(float time)
		{
			if (!Lit)
			{
				return 0f;
			}
			float num = ((Grown < 1f) ? (0.35f + 0.65f * Grown) : 1f);
			if (Sputter > 0f)
			{
				float num2 = 0.5f + 0.5f * Fm.Sin(time * (6f + 9f * Sputter)) * Fm.Sin(time * 3.3f + 0.7f);
				num *= 1f - Sputter * (0.35f + 0.45f * num2);
			}
			return num;
		}

		public void AddFlames(List<VoxelObject> overlay, Vec3 eye, float time, bool all = true)
		{
			if (!Shown || overlay == null || _my == null || _my.Fires <= 0)
			{
				return;
			}
			float num = Strength(time);
			if (num <= 0.02f)
			{
				return;
			}
			float num2 = _my.Rim * Scale;
			Vec3 vec = eye - Flame;
			float num3 = Len(vec);
			if (!(num3 < 0.0001f))
			{
				vec *= 1f / num3;
				Axes(vec, out var u, out var v, out var rise, out var lean);
				float num4 = 0.9f + 0.1f * Fm.Sin(time * 7.3f);
				float num5 = 2.6f * num2 * num4 * num;
				SpriteImage spriteImage = TorchFlames.At(time, 7u);
				float num6 = num5 * (float)spriteImage.Height / (float)spriteImage.Width * (1f + 0.35f * Fan + 0.15f * Math.Min(1f, lean)) * (1f - 0.25f * Sputter);
				float num7 = num5 / (float)spriteImage.Width;
				float num8 = num6 / (float)spriteImage.Height;
				overlay.Add(new VoxelObject
				{
					Shape = spriteImage.SlabShape(1),
					Overlay = true,
					Emissive = true,
					NoShadow = true,
					Tag = -1,
					U = u,
					V = v,
					N = vec,
					SizeU = num7,
					SizeV = num8,
					SizeN = Math.Min(num7, num8) * 0.5f,
					Origin = Flame - u * (num5 * 0.5f) - v * (num6 * TorchFlames.BelowShare(small: false))
				});
				if (all && Scrap(time, num, out var size, out var lift))
				{
					SpriteImage spriteImage2 = Flames.At(time + 3.5f, 66u, small: true);
					float num9 = size * num2;
					float num10 = num9 * (float)spriteImage2.Height / (float)spriteImage2.Width;
					float num11 = num9 / (float)spriteImage2.Width;
					float num12 = num10 / (float)spriteImage2.Height;
					Vec3 vec2 = Flame + rise * (num6 * (1f - TorchFlames.BelowShare(small: false)) * (0.82f + 0.3f * lift));
					overlay.Add(new VoxelObject
					{
						Shape = spriteImage2.SlabShape(1),
						Overlay = true,
						Emissive = true,
						NoShadow = true,
						Tag = -1,
						U = u,
						V = v,
						N = vec,
						SizeU = num11,
						SizeV = num12,
						SizeN = Math.Min(num11, num12) * 0.5f,
						Origin = vec2 - u * (num9 * 0.5f)
					});
				}
			}
		}

		public void AddFlames(List<Billboard> world, float time, bool near)
		{
			if (!Shown || world == null || _my == null || _my.Fires <= 0)
			{
				return;
			}
			float num = Strength(time);
			if (num <= 0.02f)
			{
				return;
			}
			float num2 = _my.Rim * Scale;
			uint num3 = (uint)(Math.Abs(Flame.X * 7.1f + Flame.Y * 3.3f) % 5f);
			for (int i = 0; i < _my.Fires; i++)
			{
				float num4 = 0.9f + 0.1f * Fm.Sin(time * 7.3f + 1.1f + (float)i * 2.3f);
				float num5 = ((i == 0) ? 1f : (0.72f + 0.08f * Fm.Sin(time * 1.7f + (float)i)));
				float num6 = 2.6f * num2 * num4 * num * num5;
				SpriteImage spriteImage = TorchFlames.At(time + (float)i * 0.41f, 7 + num3 + (uint)(i * 3), !near);
				float num7 = num6 * (float)spriteImage.Height / (float)spriteImage.Width * (1f + 0.3f * Fan) * (1f - 0.25f * Sputter);
				float num8 = Flame.X;
				float num9 = Flame.Y;
				float num10 = Flame.Z - num7 * TorchFlames.BelowShare(!near);
				if (i > 0)
				{
					float v = (float)(i - 1) * 6.2832f / (float)Math.Max(1, _my.Fires - 1) + (float)num3 * 0.9f;
					float num11 = _my.Spread * Scale;
					num8 += Fm.Cos(v) * num11;
					num9 += Fm.Sin(v) * num11;
					num10 -= num2 * 0.25f;
				}
				world.Add(new Billboard
				{
					X = num8,
					Y = num9,
					Z = num10,
					GroundZ = num10,
					Width = num6,
					Height = num7,
					Image = spriteImage,
					Kind = BillboardKind.Facing,
					Tag = -1,
					Emissive = true
				});
			}
		}

		private static bool Scrap(float time, float strength, out float size, out float lift)
		{
			float num = time / 0.9f + 0.37f;
			float num2 = num - (float)Math.Floor(num);
			lift = num2 * 2f;
			size = 0.7f * (1f - 0.65f * lift) * strength;
			if (num2 < 0.5f)
			{
				return strength > 0.55f;
			}
			return false;
		}

		private void Axes(Vec3 toEye, out Vec3 u, out Vec3 v, out Vec3 rise, out float lean)
		{
			Vec3 vec = _vel * (-0.16f / Scale);
			vec = new Vec3(vec.X, vec.Y, Math.Max(-0.3f, vec.Z));
			lean = Len(vec);
			if (lean > 0.7f)
			{
				vec *= 0.7f / lean;
				lean = 0.7f;
			}
			rise = new Vec3(0f, 0f, 1f) + vec;
			float num = Len(rise);
			rise = ((num > 0.001f) ? (rise * (1f / num)) : new Vec3(0f, 0f, 1f));
			u = Vec3.Cross(rise, toEye);
			float num2 = Len(u);
			if (num2 < 0.001f)
			{
				u = Vec3.Cross(Up, toEye);
				num2 = Math.Max(0.001f, Len(u));
			}
			u *= 1f / num2;
			v = Vec3.Cross(toEye, u);
		}

		public void Emit(FxParticles parts, float dt, float time, float amount = 1f)
		{
			if (!Shown || parts == null || dt <= 0f || amount <= 0f || _my == null)
			{
				return;
			}
			float scale = Scale;
			float num = _my.Rim * scale;
			if (_douse > 0)
			{
				int num2 = 0;
				while (num2 < 2 && _douse > 0)
				{
					Vec3 vel = new Vec3(((float)_rng.NextDouble() - 0.5f) * 0.3f * scale, ((float)_rng.NextDouble() - 0.5f) * 0.3f * scale, (0.5f + (float)_rng.NextDouble() * 0.4f) * scale);
					parts.Add(new FxParticles.P
					{
						Pos = Flame,
						Vel = vel,
						Life = 0.9f + (float)_rng.NextDouble() * 0.5f,
						Size = num * 1.1f,
						EndSize = num * 3.2f,
						Color = (((_douse & 1) == 0) ? SteamHot : SmokeDark),
						EndColor = SteamFar,
						Alpha = 150,
						Variant = _rng.Next(6),
						Drag = 1.6f,
						Gravity = -0.3f * scale,
						Kind = FxParticles.Kind.Steam
					});
					num2++;
					_douse--;
				}
			}
			float num3 = Strength(time);
			if (num3 <= 0.02f || _my.Fires <= 0)
			{
				return;
			}
			if (_my.Fires > 1)
			{
				amount *= 1f + 0.5f * (float)(_my.Fires - 1);
			}
			if (Grown < 0.4f && _rng.NextDouble() < 0.6)
			{
				for (int i = 0; i < 2; i++)
				{
					parts.Spark(Flame, new Vec3(((float)_rng.NextDouble() - 0.5f) * 1.2f * scale, ((float)_rng.NextDouble() - 0.5f) * 1.2f * scale, (0.6f + (float)_rng.NextDouble()) * scale), 0.3f + (float)_rng.NextDouble() * 0.3f, 0.006f * scale, EmberHot, EmberCold, 2f * scale, 1.2f);
				}
			}
			_emberClock += dt * (4.5f + 10f * Fan) * amount * num3;
			int num4 = 0;
			while (_emberClock >= 1f && num4 < 5)
			{
				_emberClock -= 1f;
				Vec3 pos = Flame + new Vec3(((float)_rng.NextDouble() * 2f - 1f) * num * 0.6f, ((float)_rng.NextDouble() * 2f - 1f) * num * 0.6f, num * (0.3f + (float)_rng.NextDouble()));
				Vec3 vel2 = new Vec3(0f, 0f, (0.35f + (float)_rng.NextDouble() * 0.45f) * scale) + _vel * 0.12f + new Vec3((float)_rng.NextDouble() - 0.5f, (float)_rng.NextDouble() - 0.5f, 0f) * (0.3f * scale);
				float num5 = (0.005f + (float)_rng.NextDouble() * 0.006f) * scale;
				parts.Add(new FxParticles.P
				{
					Pos = pos,
					Vel = vel2,
					Life = 0.5f + (float)_rng.NextDouble() * 0.7f,
					Size = num5,
					EndSize = num5 * 0.5f,
					Color = EmberHot,
					EndColor = EmberCold,
					Gravity = -0.6f * scale,
					Drag = 1.6f,
					Kind = FxParticles.Kind.Ember
				});
				num4++;
			}
			if (_emberClock > 3f)
			{
				_emberClock = 0f;
			}
			_smokeClock += dt * (6f + 6f * Sputter) * amount;
			int num6 = 0;
			while (_smokeClock >= 1f && num6 < 4)
			{
				_smokeClock -= 1f;
				Vec3 pos2 = Flame + new Vec3(0f, 0f, num * (4.2f + 1.2f * (float)_rng.NextDouble()) * num3);
				Vec3 vel3 = new Vec3(((float)_rng.NextDouble() - 0.5f) * 0.1f * scale, ((float)_rng.NextDouble() - 0.5f) * 0.1f * scale, (0.2f + (float)_rng.NextDouble() * 0.12f) * scale) + _vel * 0.05f;
				parts.Add(new FxParticles.P
				{
					Pos = pos2,
					Vel = vel3,
					Life = 0.8f + (float)_rng.NextDouble() * 0.6f,
					Size = num * 0.45f,
					EndSize = num * (1.8f + 1.2f * Sputter),
					Color = SmokeDark,
					EndColor = SmokeFar,
					Alpha = (byte)(46f + 50f * Sputter),
					Variant = _rng.Next(6),
					Drag = 1.8f,
					Gravity = -0.2f * scale,
					Kind = FxParticles.Kind.Smoke
				});
				num6++;
			}
			if (_smokeClock > 3f)
			{
				_smokeClock = 0f;
			}
		}

		public void Light(List<PointLight> lights, float time, float strength = 1f)
		{
			if (!Shown || lights == null || strength <= 0f)
			{
				return;
			}
			float num = Strength(time);
			if (!(num <= 0.02f))
			{
				bool flag = _my == null || _my.Fires > 0;
				Vec3 vec = Flame + new Vec3(0f, 0f, flag ? (((_my != null) ? _my.Rim : 0.036f) * Scale * 2f) : 0f);
				float num2 = 0.84f + 0.09f * Fm.Sin(time * 10.7f) + 0.07f * Fm.Sin(time * 17.3f + 1.3f);
				if (!flag)
				{
					num2 = 0.94f + 0.06f * num2;
				}
				float num3 = num * num2 * strength;
				lights.Add(new PointLight
				{
					X = vec.X,
					Y = vec.Y,
					Z = vec.Z,
					Radius = ((_my != null) ? _my.Reach : 6.5f) * (0.6f + 0.4f * num),
					R = 1.3f * num3,
					G = 0.78f * num3,
					B = 0.32f * num3,
					Glow = 0.04f * num3
				});
			}
		}
	}
}
