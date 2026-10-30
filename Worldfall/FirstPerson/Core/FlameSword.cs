using System;
using System.Collections.Generic;
using System.Globalization;

namespace FirstPerson.Core
{
	public sealed class FlameSword
	{
		private sealed class Shape
		{
			public MeshModel Model;

			public Vec3 BladeBase;

			public Vec3 BladeTip;

			public Vec3 Ember;

			public Vec3 Tip;

			public Vec3 Across;

			public Vec3 Across2;

			public bool Round;

			public float HalfWidth;

			public int EmberSlot = -1;

			public int Red = -1;

			public int Orange = -1;

			public int Amber = -1;
		}

		private struct Lick
		{
			public float Along;

			public float Across;

			public float Size;

			public float Offset;

			public float Near;

			public uint Seed;
		}

		public const string ModelName = "held_flame_sword";

		public const string ItemId = "flame_sword";

		public const string HammerModelName = "held_flame_hammer";

		private static readonly Dictionary<MeshModel, Shape> Shapes = new Dictionary<MeshModel, Shape>();

		private Shape _shape;

		public Vec3 Base;

		public Vec3 Point;

		public Vec3 Tip;

		public Vec3 Ember;

		public float Scale = 1f;

		public Vec3 Across = new Vec3(0f, 1f, 0f);

		public Vec3 Across2 = new Vec3(0f, 1f, 0f);

		public float Speed;

		public float Heat;

		public bool Cutting;

		public bool Shown;

		public float PosedAt = -1f;

		private bool _moving;

		private Vec3 _vBase;

		private Vec3 _vTip;

		private float _lastTime = -1f;

		private uint[] _palette;

		private readonly Random _rng = new Random(5);

		private float _emberClock;

		private static readonly Lick[] Licks = MakeLicks();

		private const int ArcSamples = 12;

		private const int ArcColumns = 28;

		public const float ArcLife = 0.11f;

		private readonly Vec3[] _arcBase = new Vec3[12];

		private readonly Vec3[] _arcTip = new Vec3[12];

		private readonly float[] _arcTime = new float[12];

		private int _arcCount;

		private MeshModel _arcModel;

		private MeshInstance _arc;

		private float _arcTimeNow;

		private static readonly uint[] ArcColours = new uint[6]
		{
			TerrainCache.Rgb(255, 252, 230),
			TerrainCache.Rgb(255, 232, 104),
			TerrainCache.Rgb(255, 178, 50),
			TerrainCache.Rgb(244, 112, 32),
			TerrainCache.Rgb(214, 54, 30),
			TerrainCache.Rgb(150, 30, 28)
		};

		private static readonly uint EmberHot = TerrainCache.Rgb(255, 236, 150);

		private static readonly uint EmberCold = TerrainCache.Rgb(206, 60, 30);

		public int ArcFrames => _arcCount;

		public static bool IsItem(string id)
		{
			return id == "flame_sword";
		}

		public static bool IsModel(MeshModel m)
		{
			if (m != null)
			{
				if (!(m.Name == "held_flame_sword"))
				{
					if (m.Name != null)
					{
						return m.Name.StartsWith("held_flame_hammer", StringComparison.Ordinal);
					}
					return false;
				}
				return true;
			}
			return false;
		}

		private static Shape ShapeOf(MeshModel m)
		{
			if (Shapes.TryGetValue(m, out var value))
			{
				return value;
			}
			value = new Shape
			{
				Model = m,
				BladeBase = InfoVec(m, "blade_base", new Vec3(0f, 0f, 0.08f)),
				BladeTip = InfoVec(m, "blade_tip", new Vec3(0f, 0f, 0.47f)),
				Ember = InfoVec(m, "ember", new Vec3(0f, 0f, 0.061f)),
				Tip = InfoVec(m, "tip", new Vec3(0f, 0f, 0.47f)),
				Across = InfoVec(m, "across", new Vec3(0f, 1f, 0f)),
				Across2 = InfoVec(m, "across2", new Vec3(0f, 0f, 0f)),
				HalfWidth = m.InfoFloat("half_width", 0.027f),
				EmberSlot = m.SlotIndex("ember"),
				Red = m.SlotIndex("core_red"),
				Orange = m.SlotIndex("core_orange"),
				Amber = m.SlotIndex("core_amber")
			};
			value.Round = Len(value.Across2) > 0.5f;
			if (Shapes.Count > 16)
			{
				Shapes.Clear();
			}
			Shapes[m] = value;
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

		private Vec3 AcrossOf(int lick)
		{
			if (((lick >> 1) & 1) == 0)
			{
				return Across;
			}
			return Across2;
		}

		public void Pose(MeshInstance inst, float time)
		{
			MeshModel model = inst.Model;
			if (!IsModel(model))
			{
				Shown = false;
				return;
			}
			Shape shape = (_shape = ShapeOf(model));
			float num = ((_lastTime < 0f) ? 0f : (time - _lastTime));
			_lastTime = time;
			PosedAt = time;
			Mat3 basis = inst.Basis;
			Vec3 position = inst.Position;
			Vec3 vec = position + basis.Mul(shape.BladeBase);
			Vec3 vec2 = position + basis.Mul(shape.BladeTip);
			Scale = Math.Max(0.001f, Len(basis.C2));
			if (num <= 0f || num > 0.25f || Len(vec2 - Point) > 40f * Scale)
			{
				_moving = false;
			}
			if (_moving)
			{
				float num2 = 1f - (float)Math.Exp((0f - num) / 0.045f);
				_vBase += ((vec - Base) * (1f / num) - _vBase) * num2;
				_vTip += ((vec2 - Point) * (1f / num) - _vTip) * num2;
			}
			else
			{
				_vBase = (_vTip = new Vec3(0f, 0f, 0f));
				_arcCount = 0;
			}
			Base = vec;
			Point = vec2;
			Tip = position + basis.Mul(shape.Tip);
			Ember = position + basis.Mul(shape.Ember);
			Across = basis.Mul(shape.Across).Normalized();
			Across2 = (shape.Round ? basis.Mul(shape.Across2).Normalized() : Across);
			Speed = Len(_vTip);
			Heat = Math.Min(1f, Speed / (Scale * 4f));
			_moving = true;
			Shown = true;
			LayArc(time);
			if (_palette == null || _palette.Length != model.SlotNames.Length)
			{
				_palette = new uint[model.SlotNames.Length];
			}
			Array.Clear(_palette, 0, _palette.Length);
			if (shape.EmberSlot >= 0)
			{
				_palette[shape.EmberSlot] = TerrainCache.Lerp(model.SlotColor[shape.EmberSlot], TerrainCache.Rgb(255, 206, 120), 0.5f + 0.5f * Fm.Sin(time * 4.1f));
			}
			if (Heat > 0.05f)
			{
				if (shape.Red >= 0)
				{
					_palette[shape.Red] = TerrainCache.Lerp(model.SlotColor[shape.Red], TerrainCache.Rgb(255, 110, 40), 0.55f * Heat);
				}
				if (shape.Orange >= 0)
				{
					_palette[shape.Orange] = TerrainCache.Lerp(model.SlotColor[shape.Orange], TerrainCache.Rgb(255, 170, 56), 0.45f * Heat);
				}
				if (shape.Amber >= 0)
				{
					_palette[shape.Amber] = TerrainCache.Lerp(model.SlotColor[shape.Amber], TerrainCache.Rgb(255, 226, 96), 0.4f * Heat);
				}
			}
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

		private Vec3 VelocityAt(float along)
		{
			return _vBase + (_vTip - _vBase) * along;
		}

		private static Lick[] MakeLicks()
		{
			List<Lick> list = new List<Lick>();
			Random random = new Random(23);
			for (int i = 0; i < 11; i++)
			{
				float num = 0.1f + (float)i * 0.082f;
				float num2 = (((i & 1) == 0) ? 1f : (-1f));
				list.Add(new Lick
				{
					Along = num + (float)(random.NextDouble() - 0.5) * 0.03f,
					Across = num2 * (0.5f + (float)random.NextDouble() * 0.35f),
					Size = 0.046f + 0.03f * num + (float)random.NextDouble() * 0.01f,
					Offset = (float)random.NextDouble() * 5f,
					Near = (float)(i % 3) - 1f,
					Seed = (uint)(i * 7 + 3)
				});
			}
			list.Add(new Lick
			{
				Along = 0.97f,
				Across = 0.1f,
				Size = 0.085f,
				Offset = 1.7f,
				Near = 1f,
				Seed = 101u
			});
			list.Add(new Lick
			{
				Along = 1.04f,
				Across = -0.25f,
				Size = 0.07f,
				Offset = 3.1f,
				Near = -1f,
				Seed = 202u
			});
			return list.ToArray();
		}

		public void AddFlames(List<VoxelObject> overlay, Vec3 eye, float time, bool all = true)
		{
			if (!Shown || overlay == null || _shape == null)
			{
				return;
			}
			Vec3 vec = Point - Base;
			float num = _shape.HalfWidth * Scale;
			for (int i = 0; i < Licks.Length; i++)
			{
				if (!all && i % 3 == 1 && i < Licks.Length - 2)
				{
					continue;
				}
				Lick l = Licks[i];
				Vec3 vec2 = Base + vec * l.Along + AcrossOf(i) * (l.Across * num);
				Vec3 vec3 = eye - vec2;
				float num2 = Len(vec3);
				if (!(num2 < 0.0001f))
				{
					vec3 *= 1f / num2;
					Place(in l, vec3, time, out var u, out var v, out var w, out var h, out var img);
					if (!all)
					{
						w *= 1.15f;
						h *= 1.15f;
					}
					vec2 += vec3 * (l.Near * 0.006f * Scale);
					float num3 = w / (float)img.Width;
					float num4 = h / (float)img.Height;
					overlay.Add(new VoxelObject
					{
						Shape = img.SlabShape(1),
						Overlay = true,
						Emissive = true,
						NoShadow = true,
						Tag = -1,
						U = u,
						V = v,
						N = vec3,
						SizeU = num3,
						SizeV = num4,
						SizeN = Math.Min(num3, num4) * 0.5f,
						Origin = vec2 - u * (w * 0.5f) - v * (h * 0.14f)
					});
				}
			}
		}

		public void AddFlames(List<Billboard> world, float time, bool near)
		{
			if (!Shown || world == null || _shape == null)
			{
				return;
			}
			Vec3 vec = Point - Base;
			float num = _shape.HalfWidth * Scale;
			for (int i = 0; i < Licks.Length; i++)
			{
				if (near || (i & 1) == 0 || i >= Licks.Length - 2)
				{
					Lick lick = Licks[i];
					Vec3 vec2 = Base + vec * lick.Along + AcrossOf(i) * (lick.Across * num);
					float num2 = 0.88f + 0.12f * Fm.Sin(time * 7.3f + lick.Offset * 3f);
					float num3 = lick.Size * Scale * num2 * (near ? 1f : 1.4f);
					SpriteImage spriteImage = Flames.At(time + lick.Offset, lick.Seed, !near);
					float num4 = num3 * (float)spriteImage.Height / (float)spriteImage.Width * (1f + 0.35f * Heat);
					world.Add(new Billboard
					{
						X = vec2.X,
						Y = vec2.Y,
						Z = vec2.Z - num4 * 0.14f,
						GroundZ = vec2.Z - num4 * 0.14f,
						Width = num3,
						Height = num4,
						Image = spriteImage,
						Kind = BillboardKind.Facing,
						Tag = -1,
						Emissive = true
					});
				}
			}
		}

		private void Place(in Lick l, Vec3 toEye, float time, out Vec3 u, out Vec3 v, out float w, out float h, out SpriteImage img)
		{
			Vec3 vec = VelocityAt(Math.Min(1f, l.Along)) * (-0.11f / Scale);
			float num = Len(vec);
			if (num > 1.6f)
			{
				vec *= 1.6f / num;
				num = 1.6f;
			}
			Vec3 a = new Vec3(0f, 0f, 1f) + vec;
			u = Vec3.Cross(a, toEye);
			float num2 = Len(u);
			if (num2 < 0.001f)
			{
				u = Across;
				num2 = 1f;
			}
			u *= 1f / num2;
			v = Vec3.Cross(toEye, u);
			float num3 = 0.88f + 0.12f * Fm.Sin(time * 7.3f + l.Offset * 3f);
			w = l.Size * Scale * num3;
			img = Flames.At(time + l.Offset, l.Seed);
			h = w * (float)img.Height / (float)img.Width * (1f + 0.45f * Heat + 0.25f * Math.Min(1f, num));
		}

		private void LayArc(float time)
		{
			_arcTimeNow = time;
			while (_arcCount > 0 && time - _arcTime[_arcCount - 1] > 0.11f)
			{
				_arcCount--;
			}
			if (Cutting && !(Heat < 0.35f))
			{
				for (int num = Math.Min(_arcCount, 11); num > 0; num--)
				{
					_arcBase[num] = _arcBase[num - 1];
					_arcTip[num] = _arcTip[num - 1];
					_arcTime[num] = _arcTime[num - 1];
				}
				_arcBase[0] = Base;
				_arcTip[0] = Point + (Point - Base) * 0.05f;
				_arcTime[0] = time;
				_arcCount = Math.Min(12, _arcCount + 1);
			}
		}

		public MeshInstance Arc()
		{
			if (_arcCount < 2)
			{
				return null;
			}
			if (_arcModel == null)
			{
				MakeArcModel();
			}
			MeshPart meshPart = _arcModel.Parts[0];
			float[] pos = meshPart.Pos;
			Vec3 vec = _arcBase[0];
			int num = _arcCount - 1;
			for (int i = 0; i < 28; i++)
			{
				float num2 = (float)i / 27f * (float)num;
				int num3 = Math.Min(num - 1, (int)num2);
				float num4 = num2 - (float)num3;
				Vec3 vec2 = Spline(_arcBase, num3, num4, num);
				Vec3 vec3 = Spline(_arcTip, num3, num4, num);
				float num5 = _arcTime[num3] + (_arcTime[num3 + 1] - _arcTime[num3]) * num4;
				float num6 = Math.Min(1f, Math.Max(0f, (_arcTimeNow - num5) / 0.11f));
				float num7 = 0.76f + 0.22f * (float)Math.Pow(num6, 0.7);
				float num8 = num7 + (1f - num7) * 0.45f;
				for (int j = 0; j < 3; j++)
				{
					float num9;
					switch (j)
					{
					default:
						num9 = 1f;
						break;
					case 1:
						num9 = num8;
						break;
					case 0:
						num9 = num7;
						break;
					}
					float num10 = num9;
					Vec3 vec4 = vec2 + (vec3 - vec2) * num10 - vec;
					int num11 = (i * 3 + j) * 3;
					pos[num11] = vec4.X;
					pos[num11 + 1] = vec4.Y;
					pos[num11 + 2] = vec4.Z;
				}
			}
			meshPart.Version++;
			_arc.Position = vec;
			_arc.Basis = Mat3.Identity;
			return _arc;
		}

		private static Vec3 Spline(Vec3[] pts, int i, float t, int last)
		{
			Vec3 vec = pts[Math.Max(0, i - 1)];
			Vec3 vec2 = pts[i];
			Vec3 vec3 = pts[Math.Min(last, i + 1)];
			Vec3 vec4 = pts[Math.Min(last, i + 2)];
			float num = t * t;
			float num2 = num * t;
			return (vec2 * 2f + (vec3 - vec) * t + (vec * 2f - vec2 * 5f + vec3 * 4f - vec4) * num + (vec2 * 3f - vec - vec3 * 3f + vec4) * num2) * 0.5f;
		}

		private void MakeArcModel()
		{
			int num = 84;
			int[] array = new int[54 * 4 * 3];
			byte[] array2 = new byte[54 * 4];
			int num2 = 0;
			int num3 = 0;
			for (int i = 0; i < 27; i++)
			{
				float num4 = (float)i / 26f;
				for (int j = 0; j < 2; j++)
				{
					int num5 = i * 3 + j;
					int num6 = num5 + 1;
					int num7 = num5 + 3;
					int num8 = num5 + 4;
					byte b = (byte)Math.Min(ArcColours.Length - 1, (int)(num4 * 4.2f) + j);
					int[] array3 = new int[12]
					{
						num5, num7, num6, num6, num7, num8, num5, num6, num7, num6,
						num8, num7
					};
					for (int k = 0; k < 12; k++)
					{
						array[num2++] = array3[k];
					}
					for (int l = 0; l < 4; l++)
					{
						array2[num3++] = b;
					}
				}
			}
			MeshPart meshPart = new MeshPart
			{
				Name = "arc",
				Parent = -1,
				Pivot = new Vec3(0f, 0f, 0f),
				Pos = new float[num * 3],
				Nrm = new float[num * 3],
				Tri = array,
				Slot = array2,
				Axis = new byte[array.Length / 3],
				Version = 1
			};
			for (int m = 0; m < num; m++)
			{
				meshPart.Nrm[m * 3 + 2] = 1f;
			}
			int num9 = ArcColours.Length;
			string[] array4 = new string[num9];
			byte[] array5 = new byte[num9];
			byte[] array6 = new byte[num9];
			for (int n = 0; n < num9; n++)
			{
				array4[n] = "arc" + n;
				array5[n] = 11;
				array6[n] = 1;
			}
			_arcModel = new MeshModel
			{
				Name = "flame_arc",
				SlotNames = array4,
				SlotKind = array5,
				SlotFlags = array6,
				SlotColor = (uint[])ArcColours.Clone(),
				Parts = new MeshPart[1] { meshPart }
			};
			_arc = new MeshInstance
			{
				Model = _arcModel,
				NoShadow = true,
				Emissive = true
			};
		}

		public void Emit(FxParticles parts, float dt, float amount = 1f)
		{
			if (Shown && parts != null && !(dt <= 0f) && !(amount <= 0f))
			{
				float scale = Scale;
				Vec3 vec = Point - Base;
				_emberClock += dt * (5f + (Cutting ? 22f : 6f) * Heat) * amount;
				int num = 0;
				while (_emberClock >= 1f && num < 6)
				{
					_emberClock -= 1f;
					float num2 = 0.15f + (float)_rng.NextDouble() * 0.9f;
					Vec3 pos = Base + vec * num2 + Across * (((float)_rng.NextDouble() * 2f - 1f) * 0.022f * scale);
					Vec3 vel = new Vec3(0f, 0f, (0.3f + (float)_rng.NextDouble() * 0.4f) * scale) + VelocityAt(Math.Min(1f, num2)) * 0.15f + new Vec3((float)_rng.NextDouble() - 0.5f, (float)_rng.NextDouble() - 0.5f, 0f) * (0.25f * scale);
					float num3 = (0.006f + (float)_rng.NextDouble() * 0.006f) * scale;
					parts.Add(new FxParticles.P
					{
						Pos = pos,
						Vel = vel,
						Life = 0.4f + (float)_rng.NextDouble() * 0.5f,
						Size = num3,
						EndSize = num3 * 0.5f,
						Color = EmberHot,
						EndColor = EmberCold,
						Gravity = -0.7f * scale,
						Drag = 1.8f,
						Kind = FxParticles.Kind.Ember
					});
					num++;
				}
				if (_emberClock > 3f)
				{
					_emberClock = 0f;
				}
			}
		}

		public void Light(List<PointLight> lights, float time, float strength = 1f)
		{
			if (Shown && lights != null && !(strength <= 0f))
			{
				Vec3 vec = Base + (Point - Base) * 0.55f;
				float num = 0.82f + 0.1f * Fm.Sin(time * 11.3f) + 0.08f * Fm.Sin(time * 17.9f + 1.3f);
				float num2 = (0.55f + 0.35f * Heat) * num * strength;
				lights.Add(new PointLight
				{
					X = vec.X,
					Y = vec.Y,
					Z = vec.Z,
					Radius = 4f + 1.2f * Heat,
					R = 1.05f * num2,
					G = 0.56f * num2,
					B = 0.2f * num2,
					Glow = 0.05f * num2
				});
			}
		}
	}
}
