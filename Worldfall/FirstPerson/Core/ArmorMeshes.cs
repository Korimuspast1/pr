using System;
using System.Collections.Generic;
using System.Threading;

namespace FirstPerson.Core
{
	public static class ArmorMeshes
	{
		private sealed class LegFit
		{
			public MeshPart Part;

			public float Sole;

			public float Knee;

			public float H;

			public float ToeY;

			public float HeelY;

			public float FootTop;

			public Ring Ankle;

			public Ring Shin(float f)
			{
				return Fit(Part, Sole + H * f, H * 0.05f);
			}

			public static LegFit Of(MeshPart p)
			{
				Bounds(p, out var lo, out var _);
				LegFit legFit = new LegFit
				{
					Part = p,
					Sole = lo.Z,
					Knee = p.Pivot.Z
				};
				legFit.H = legFit.Knee - legFit.Sole;
				if (legFit.H <= 0.05f)
				{
					return null;
				}
				legFit.FootTop = legFit.Sole + legFit.H * 0.2f;
				float num = float.MaxValue;
				float num2 = float.MinValue;
				for (int i = 0; i < p.VertexCount; i++)
				{
					if (!(p.Pos[i * 3 + 2] > legFit.FootTop))
					{
						float val = p.Pos[i * 3 + 1];
						num = Math.Min(num, val);
						num2 = Math.Max(num2, val);
					}
				}
				if (num > num2)
				{
					return null;
				}
				legFit.ToeY = num;
				legFit.HeelY = num2;
				legFit.Ankle = legFit.Shin(0.28f);
				return legFit;
			}

			public void Station(float t, out float y, out float cx, out float half, out float top)
			{
				y = HeelY + (ToeY - HeelY) * t;
				List<Vec3> list = new List<Vec3>();
				float num = Math.Abs(HeelY - ToeY);
				float[] array = new float[3]
				{
					0f,
					(0f - num) * 0.06f,
					num * 0.06f
				};
				foreach (float num2 in array)
				{
					Slice(Part, 1, y + num2, list);
				}
				float num3 = float.MaxValue;
				float num4 = float.MinValue;
				float num5 = FootTop + H * 0.06f;
				top = Sole + H * 0.04f;
				foreach (Vec3 item in list)
				{
					if (!(item.Z > num5))
					{
						num3 = Math.Min(num3, item.X);
						num4 = Math.Max(num4, item.X);
						top = Math.Max(top, item.Z);
					}
				}
				if (num3 > num4)
				{
					cx = Ankle.X;
					half = Ankle.Rx;
				}
				else
				{
					cx = (num3 + num4) * 0.5f;
					half = Math.Max(0.01f, (num4 - num3) * 0.5f);
				}
				top = Math.Min(top, num5);
			}
		}

		public enum Metal : byte
		{
			Leather,
			Copper,
			Bronze,
			Silver,
			Iron,
			Steel,
			Mythril,
			Adamantine
		}

		internal sealed class Shades
		{
			public uint Main;

			public uint Shade;

			public uint Dark;

			public uint Light;

			public uint Trim;

			public uint TrimDark;

			public uint TrimLight;

			public uint Rivet;

			public uint Inside;

			public uint Accent;

			public uint AccentHot;

			public byte Kind = 10;

			public byte TrimKind = 10;

			public byte AccentKind = 10;

			public float Bulk;
		}

		public struct DressMemo
		{
			internal MeshModel Body;

			internal MeshModel Fit;

			internal string Helmet;

			internal string Chest;

			internal string Boots;

			internal MeshInstance[] Into;

			internal int LetGo;

			internal int Count;

			internal MeshModel Model0;

			internal MeshModel Model1;

			internal MeshModel Model2;

			internal ulong Hidden0;

			internal ulong Hidden1;

			internal ulong Hidden2;

			internal MeshModel Model(int i)
			{
				switch (i)
				{
				default:
					return Model2;
				case 1:
					return Model1;
				case 0:
					return Model0;
				}
			}

			internal ulong Hidden(int i)
			{
				switch (i)
				{
				default:
					return Hidden2;
				case 1:
					return Hidden1;
				case 0:
					return Hidden0;
				}
			}

			internal void Set(int i, MeshModel model, ulong hidden)
			{
				switch (i)
				{
				case 0:
					Model0 = model;
					Hidden0 = hidden;
					break;
				case 1:
					Model1 = model;
					Hidden1 = hidden;
					break;
				default:
					Model2 = model;
					Hidden2 = hidden;
					break;
				}
			}
		}

		internal struct Ring
		{
			public float X;

			public float Y;

			public float Z;

			public float Rx;

			public float Ry;

			public Vec3 C => new Vec3(X, Y, Z);

			public Ring(float x, float y, float z, float rx, float ry)
			{
				X = x;
				Y = y;
				Z = z;
				Rx = Math.Max(0.0001f, rx);
				Ry = Math.Max(0.0001f, ry);
			}

			public Vec3 At(float th)
			{
				return new Vec3(X + Rx * (float)Math.Cos(th), Y + Ry * (float)Math.Sin(th), Z);
			}

			public Vec3 At(float th, float out_)
			{
				return new Vec3(X + (Rx + out_) * (float)Math.Cos(th), Y + (Ry + out_) * (float)Math.Sin(th), Z);
			}

			public Vec3 N(float th)
			{
				return new Vec3((float)Math.Cos(th) / Rx, (float)Math.Sin(th) / Ry, 0f).Normalized();
			}

			public Vec3 T(float th)
			{
				return new Vec3((0f - Rx) * (float)Math.Sin(th), Ry * (float)Math.Cos(th), 0f).Normalized();
			}

			public Ring Grow(float k, float pad)
			{
				return new Ring(X, Y, Z, Rx * k + pad, Ry * k + pad);
			}

			public Ring Up(float z)
			{
				return new Ring(X, Y, z, Rx, Ry);
			}

			public Ring Shift(float dx, float dy, float dz)
			{
				return new Ring(X + dx, Y + dy, Z + dz, Rx, Ry);
			}

			public Ring Size(float rx, float ry)
			{
				return new Ring(X, Y, Z, rx, ry);
			}

			public static Ring Lerp(Ring a, Ring b, float t)
			{
				return new Ring(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, a.Rx + (b.Rx - a.Rx) * t, a.Ry + (b.Ry - a.Ry) * t);
			}
		}

		internal sealed class HeadFit
		{
			public MeshPart Part;

			public float Lo;

			public float Top;

			public float Eye;

			public float Brow;

			public float Chin;

			public float H;

			public bool Snout;

			public Ring At(float z)
			{
				return Fit(Part, z, Math.Max(0.012f, H * 0.12f), 1.15f);
			}

			public static HeadFit Of(MeshModel body, MeshPart p)
			{
				Bounds(p, out var lo, out var hi);
				HeadFit headFit = new HeadFit
				{
					Part = p,
					Lo = lo.Z
				};
				float num = SlotHeight(body, p, "eyes");
				if (float.IsNaN(num))
				{
					num = SlotHeight(body, p, "glow");
				}
				if (float.IsNaN(num) || num < lo.Z + (hi.Z - lo.Z) * 0.35f || num > hi.Z - (hi.Z - lo.Z) * 0.12f)
				{
					num = lo.Z + (hi.Z - lo.Z) * 0.66f;
				}
				headFit.Eye = num;
				Ring ring = Fit(p, num + (hi.Z - num) * 0.3f, (hi.Z - lo.Z) * 0.06f, 1.15f);
				float num2 = num + 0.04f;
				for (int i = 0; i < p.VertexCount; i++)
				{
					float num3 = p.Pos[i * 3];
					float num4 = p.Pos[i * 3 + 1];
					float num5 = p.Pos[i * 3 + 2];
					if (Math.Abs(num3 - ring.X) < ring.Rx * 0.5f && Math.Abs(num4 - ring.Y) < ring.Ry * 1.05f && num5 > num2)
					{
						num2 = num5;
					}
				}
				headFit.Top = Math.Min(hi.Z, num2);
				headFit.H = Math.Max(0.04f, headFit.Top - num);
				headFit.Brow = num + headFit.H * 0.24f;
				headFit.Chin = Math.Max(lo.Z + 0.015f, num - headFit.H * 1.25f);
				Ring ring2 = headFit.At(headFit.Brow);
				float num6 = num - headFit.H * 0.5f;
				float num7 = float.MaxValue;
				for (int j = 0; j < p.VertexCount; j++)
				{
					if (Math.Abs(p.Pos[j * 3 + 2] - num6) < headFit.H * 0.35f)
					{
						num7 = Math.Min(num7, p.Pos[j * 3 + 1]);
					}
				}
				headFit.Snout = num7 < ring2.Y - ring2.Ry * 1.45f;
				return headFit;
			}
		}

		internal sealed class Kit
		{
			public readonly MeshBuilder B;

			public readonly Metal M;

			public readonly Shades S;

			public readonly float Detail;

			public readonly float U;

			public readonly int Main;

			public readonly int Shade;

			public readonly int Dark;

			public readonly int Light;

			public readonly int Trim;

			public readonly int TrimDark;

			public readonly int TrimLight;

			public readonly int Rivet;

			public readonly int Inside;

			public readonly int Accent;

			public readonly int AccentHot;

			public readonly bool Worn;

			public readonly int Gore = -1;

			public readonly int Grime = -1;

			public readonly int Decay = -1;

			public readonly int Glow = -1;

			public Vec3 Origin;

			public Mat3 Rot = Mat3.Identity;

			public bool Dress;

			public float Bulk => S.Bulk;

			public int PlateSlot
			{
				get
				{
					if (M != Metal.Bronze)
					{
						return Main;
					}
					return Trim;
				}
			}

			public int LegSlot
			{
				get
				{
					if (M != Metal.Iron)
					{
						return Main;
					}
					return Trim;
				}
			}

			public bool Fine => Detail > 0.9f;

			public bool Mid => Detail > 0.6f;

			public Kit(MeshBuilder b, Metal metal, float detail, float unit, bool worn = false)
			{
				bool worn2 = worn;
				base..ctor();
				B = b;
				M = metal;
				S = (worn2 ? WornShades(metal) : ShadesOf(metal));
				Detail = detail;
				U = Math.Max(0.4f, unit);
				Worn = worn2;
				Main = b.Slot("main", S.Kind, S.Main, Shine(S.Kind));
				Shade = b.Slot("shade", S.Kind, S.Shade, Shine(S.Kind));
				Dark = b.Slot("dark", S.Kind, S.Dark, Shine(S.Kind));
				Light = b.Slot("light", S.Kind, S.Light, Shine(S.Kind));
				Trim = b.Slot("trim", S.TrimKind, S.Trim, Shine(S.TrimKind));
				TrimDark = b.Slot("trim_dark", S.TrimKind, S.TrimDark, Shine(S.TrimKind));
				TrimLight = b.Slot("trim_light", S.TrimKind, S.TrimLight, Shine(S.TrimKind));
				Rivet = b.Slot("rivet", 0, S.Rivet, (byte)((!worn2) ? 16 : 0));
				Inside = b.Slot("inside", 0, S.Inside, 0);
				Accent = b.Slot("accent", S.AccentKind, S.Accent, Shine(S.AccentKind));
				AccentHot = b.Slot("accent_hot", S.AccentKind, S.AccentHot, Shine(S.AccentKind));
				if (worn2)
				{
					Gore = b.Slot("gore", 0, WornBlood, 0);
					Grime = b.Slot("grime", 7, WornMud, 0);
					Decay = b.Slot("rot", 0, RotOf(metal), 0);
					Glow = b.Slot("glow", 0, DeadGlow, 1);
				}
				byte Shine(byte kind)
				{
					if (kind != 10 || worn2)
					{
						return 0;
					}
					return 16;
				}
			}

			public int Sides(int n)
			{
				return Math.Max(4, (int)Math.Round((float)n * (Fine ? 1.34f : (Mid ? 0.85f : 0.55f))));
			}

			public int Facets(int n)
			{
				if (!Mid)
				{
					return Math.Max(4, n - 2);
				}
				return n;
			}

			private Vec3 P(Vec3 v)
			{
				return Origin + Rot.Mul(v);
			}

			private Vec3 D(Vec3 v)
			{
				return Rot.Mul(v);
			}

			public void Loft(int slot, Ring[] rings, int sides, bool smooth = true, float turn = 0f, bool caps = true)
			{
				Vec3[] array = new Vec3[rings.Length];
				float[] array2 = new float[rings.Length];
				float[] array3 = new float[rings.Length];
				for (int i = 0; i < rings.Length; i++)
				{
					array[i] = P(rings[i].C);
					array2[i] = rings[i].Rx;
					array3[i] = rings[i].Ry;
				}
				B.Loft(slot, array, array2, array3, D(new Vec3(1f, 0f, 0f)), D(new Vec3(0f, 1f, 0f)), Math.Max(3, sides), smooth, turn, caps);
			}

			public void LoftAlong(int slot, Vec3[] centres, float[] rx, float[] ry, Vec3 ax, Vec3 ay, int sides, bool smooth = true)
			{
				Vec3[] array = new Vec3[centres.Length];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = P(centres[i]);
				}
				B.Loft(slot, array, rx, ry, D(ax), D(ay), Math.Max(3, sides), smooth);
			}

			public void Band(int slot, Ring at, float half, float pad, int sides, bool smooth = true)
			{
				if (Mid)
				{
					Loft(slot, new Ring[3]
					{
						at.Grow(1f, pad * 0.6f).Up(at.Z - half),
						at.Grow(1f, pad).Up(at.Z),
						at.Grow(1f, pad * 0.6f).Up(at.Z + half)
					}, sides, smooth, 0f, caps: false);
				}
			}

			public void Plate(int slot, int back, Ring[] rings, float th0, float th1, float thick, int segs = 0)
			{
				int num = rings.Length;
				if (num < 2)
				{
					return;
				}
				bool flag = thick <= 0.0045f * U;
				if (flag && !Mid)
				{
					return;
				}
				bool flag2 = Fine || (!flag && Mid);
				if (segs <= 0)
				{
					segs = Math.Max(2, (int)Math.Ceiling(Math.Abs(th1 - th0) / (MathF.PI * 2f) * (float)Sides(16)));
				}
				int num2 = segs + 1;
				bool flag3 = Math.Abs(Math.Abs(th1 - th0) - MathF.PI * 2f) < 0.001f;
				Vec3[,] array = new Vec3[num, num2];
				Vec3[,] array2 = new Vec3[num, num2];
				for (int i = 0; i < num; i++)
				{
					for (int j = 0; j < num2; j++)
					{
						float th2 = th0 + (th1 - th0) * (float)j / (float)segs;
						Ring ring = rings[i];
						array[i, j] = P(ring.At(th2));
						array2[i, j] = P(ring.Grow(1f, 0f - thick).At(th2));
					}
				}
				Ring ring2 = rings[num / 2];
				float th3 = (th0 + th1) * 0.5f;
				Vec3 vec = D(ring2.N(th3));
				SheetOut(slot, array, vec);
				if (!flag2)
				{
					return;
				}
				SheetOut(back, array2, vec * -1f);
				if (thick <= 0f)
				{
					return;
				}
				bool flag4 = rings[num - 1].Z >= rings[0].Z;
				Vec3 vec2 = D(new Vec3(0f, 0f, flag4 ? 1f : (-1f)));
				for (int k = 0; k + 1 < num2; k++)
				{
					FaceOut(back, vec2, array[num - 1, k], array[num - 1, k + 1], array2[num - 1, k + 1], array2[num - 1, k]);
					FaceOut(back, vec2 * -1f, array[0, k], array[0, k + 1], array2[0, k + 1], array2[0, k]);
				}
				if (!flag3)
				{
					float num3 = ((th1 > th0) ? 1f : (-1f));
					for (int l = 0; l + 1 < num; l++)
					{
						FaceOut(back, D(rings[l].T(th0)) * (0f - num3), array[l, 0], array[l + 1, 0], array2[l + 1, 0], array2[l, 0]);
						FaceOut(back, D(rings[l].T(th1)) * num3, array[l, num2 - 1], array[l + 1, num2 - 1], array2[l + 1, num2 - 1], array2[l, num2 - 1]);
					}
				}
			}

			public void SheetOut(int slot, Vec3[,] p, Vec3 outward)
			{
				int length = p.GetLength(0);
				int length2 = p.GetLength(1);
				int num = Math.Min(length - 2, length / 2);
				int num2 = Math.Min(length2 - 2, length2 / 2);
				Vec3 vec = Vec3.Cross(p[num, num2 + 1] - p[num, num2], p[num + 1, num2] - p[num, num2]);
				if (Vec3.Dot(vec, vec) < 1E-14f)
				{
					vec = Vec3.Cross(p[num + 1, num2 + 1] - p[num + 1, num2], p[num + 1, num2] - p[num, num2]);
				}
				if (Vec3.Dot(vec, outward) < 0f)
				{
					Vec3[,] array = new Vec3[length, length2];
					for (int i = 0; i < length; i++)
					{
						for (int j = 0; j < length2; j++)
						{
							array[i, j] = p[i, length2 - 1 - j];
						}
					}
					p = array;
				}
				B.Sheet(slot, p);
			}

			public void FaceOut(int slot, Vec3 outward, params Vec3[] pts)
			{
				Vec3 vec = Vec3.Cross(pts[1] - pts[0], pts[2] - pts[0]);
				if (Vec3.Dot(vec, vec) < 1E-16f && pts.Length > 3)
				{
					vec = Vec3.Cross(pts[2] - pts[0], pts[3] - pts[0]);
				}
				if (Vec3.Dot(vec, outward) < 0f)
				{
					Array.Reverse(pts);
				}
				B.Face(slot, pts);
			}

			public void Box(int slot, Vec3 c, Vec3 hx, Vec3 hy, Vec3 hz)
			{
				B.Box(slot, P(c), D(hx), D(hy), D(hz));
			}

			public void OnRing(int slot, Ring r, float th, float z, float w, float h, float d, float lift = 0f)
			{
				Vec3 vec = r.N(th);
				Vec3 vec2 = Vec3.Cross(new Vec3(0f, 0f, 1f), vec);
				Vec3 c = r.Up(z).At(th) + vec * (d * 0.5f + lift);
				Box(slot, c, vec2 * w, new Vec3(0f, 0f, h), vec * d);
			}

			public void Stud(int slot, Ring r, float th, float z, float size)
			{
				if (Fine)
				{
					OnRing(slot, r, th, z, size, size, size * 0.8f);
				}
			}

			public void Spike(int slot, Vec3 at, Vec3 tip, float r, int sides)
			{
				B.Tube(slot, P(at), P(tip), r, 0f, Math.Max(3, sides), smooth: false);
			}

			public void Tube(int slot, Vec3 a, Vec3 b, float ra, float rb, int sides, bool smooth = true)
			{
				B.Tube(slot, P(a), P(b), ra, rb, Math.Max(3, sides), smooth);
			}

			public void Blob(int slot, Vec3 c, float hx, float hy, float hz, int sides)
			{
				Vec3[] array = new Vec3[5];
				float[] array2 = new float[5];
				float[] array3 = new float[5];
				float[] array4 = new float[5] { -0.92f, -0.6f, 0f, 0.6f, 0.92f };
				for (int i = 0; i < 5; i++)
				{
					float num = (float)Math.Sqrt(1f - array4[i] * array4[i]);
					array[i] = P(c + new Vec3(0f, hy * array4[i], 0f));
					array2[i] = hz * num;
					array3[i] = hx * num;
				}
				B.Loft(slot, array, array2, array3, D(new Vec3(0f, 0f, 1f)), D(new Vec3(1f, 0f, 0f)), Math.Max(4, sides), smooth: true);
			}

			public void Fin(int slot, Vec3[] path, Vec3[] outward, float[] height, float half)
			{
				int num = path.Length;
				if (num < 2)
				{
					return;
				}
				if (!Mid)
				{
					float num2 = 0f;
					foreach (float val in height)
					{
						num2 = Math.Max(num2, val);
					}
					if (num2 < 0.015f * U)
					{
						return;
					}
				}
				Vec3[,] array = new Vec3[2, num];
				Vec3[,] array2 = new Vec3[2, num];
				Vec3[] array3 = new Vec3[num];
				Vec3[] array4 = new Vec3[num];
				for (int j = 0; j < num; j++)
				{
					Vec3 a = (path[Math.Min(num - 1, j + 1)] - path[Math.Max(0, j - 1)]).Normalized();
					Vec3 vec = outward[j].Normalized();
					Vec3 vec2 = Vec3.Cross(a, vec);
					vec2 = (array4[j] = ((Vec3.Dot(vec2, vec2) > 1E-08f) ? (vec2.Normalized() * half) : new Vec3(half, 0f, 0f)));
					array3[j] = path[j] + vec * height[j];
					array[0, j] = P(path[j] - vec2);
					array[1, j] = P(array3[j] - vec2);
					array2[0, j] = P(path[j] + vec2);
					array2[1, j] = P(array3[j] + vec2);
				}
				int num3 = num / 2;
				SheetOut(slot, array, D(array4[num3] * -1f));
				SheetOut(slot, array2, D(array4[num3]));
				for (int k = 0; k + 1 < num; k++)
				{
					FaceOut(slot, D(outward[k]), P(array3[k] - array4[k]), P(array3[k + 1] - array4[k + 1]), P(array3[k + 1] + array4[k + 1]), P(array3[k] + array4[k]));
				}
				FaceOut(slot, D(path[0] - path[1]), P(path[0] - array4[0]), P(array3[0] - array4[0]), P(array3[0] + array4[0]), P(path[0] + array4[0]));
				FaceOut(slot, D(path[num - 1] - path[num - 2]), P(path[num - 1] - array4[num - 1]), P(array3[num - 1] - array4[num - 1]), P(array3[num - 1] + array4[num - 1]), P(path[num - 1] + array4[num - 1]));
			}
		}

		public const int Metals = 8;

		public const string SlotMain = "main";

		public const string SlotShade = "shade";

		public const string SlotDark = "dark";

		public const string SlotLight = "light";

		public const string SlotTrim = "trim";

		public const string SlotTrimDark = "trim_dark";

		public const string SlotTrimLight = "trim_light";

		public const string SlotRivet = "rivet";

		public const string SlotInside = "inside";

		public const string SlotAccent = "accent";

		public const string SlotAccentHot = "accent_hot";

		private static readonly Shades[] Palette = MakePalette();

		private static readonly Dictionary<(MeshModel, Metal), MeshModel> Cache = new Dictionary<(MeshModel, Metal), MeshModel>();

		public static bool Remember = true;

		private static int _letGo;

		private static bool _verifyDress;

		private static int _dressCompared;

		private static int _dressDiffer;

		private static string _dressFirst;

		private static readonly MeshInstance[] VerifyInto = new MeshInstance[3];

		internal const float Front = -MathF.PI / 2f;

		internal const float Back = MathF.PI / 2f;

		internal const float Pi = MathF.PI;

		[ThreadStatic]
		private static List<Vec3> _slice;

		public const string SlotGore = "gore";

		public const string SlotGrime = "grime";

		public const string SlotRot = "rot";

		public const string SlotGlow = "glow";

		private static readonly uint WornSoil = Hex(4865582);

		private static readonly uint WornBlood = Hex(6035990);

		private static readonly uint WornMud = Hex(3879204);

		private static readonly uint DeadGlow = Hex(16731184);

		public static int DressesCompared => _dressCompared;

		private static Ring[] Shaft(LegFit b, float topF, float pad, float flare = 1f, float grow = 1.07f)
		{
			List<Ring> list = new List<Ring>();
			Ring ring = b.Ankle.Grow(grow, pad);
			list.Add(ring.Up(b.Sole + b.H * 0.1f));
			list.Add(ring);
			Ring ring2 = ring;
			float[] array = new float[4] { 0.45f, 0.62f, 0.78f, 0.9f };
			foreach (float num in array)
			{
				if (num >= topF - 0.04f)
				{
					break;
				}
				Ring ring3 = b.Shin(num).Grow(grow, pad);
				ring3.Rx = Math.Max(ring3.Rx, ring2.Rx * 0.97f);
				ring3.Ry = Math.Max(ring3.Ry, ring2.Ry * 0.97f);
				list.Add(ring3);
				ring2 = ring3;
			}
			Ring ring4 = b.Shin(Math.Min(0.97f, topF)).Grow(grow, pad);
			ring4.Rx = Math.Max(ring4.Rx, ring2.Rx * 0.97f) * flare;
			ring4.Ry = Math.Max(ring4.Ry, ring2.Ry * 0.97f) * flare;
			list.Add(ring4.Up(b.Sole + b.H * topF));
			return list.ToArray();
		}

		private static void Foot(Kit k, LegFit b, int slot, float pad, float point = 0f, float from = 0f, float to = 1f, float lift = 0f)
		{
			float[] array = ((!k.Fine) ? ((!k.Mid) ? new float[4] { 0f, 0.2f, 0.7f, 1f } : new float[6] { 0f, 0.15f, 0.5f, 0.8f, 0.93f, 1f }) : new float[7] { 0f, 0.12f, 0.35f, 0.6f, 0.8f, 0.93f, 1f });
			if (!k.Mid && (from > 0f || to < 1f) && (!(from >= 0.6f) || !(to >= 1f)))
			{
				if (from > 0f)
				{
					return;
				}
				to = 1f;
			}
			List<Vec3> list = new List<Vec3>();
			List<float> list2 = new List<float>();
			List<float> list3 = new List<float>();
			float num = Math.Abs(b.HeelY - b.ToeY);
			foreach (float num2 in array)
			{
				if (!(num2 < from - 0.0001f) && !(num2 > to + 0.0001f))
				{
					b.Station(Math.Min(0.96f, num2), out var y, out var cx, out var half, out var top);
					float num3 = (top - b.Sole) * 0.5f + pad + lift;
					float num4 = half * 1.06f + pad + lift;
					if (num2 == 0f)
					{
						y += pad * 0.8f;
						num4 *= 0.78f;
						num3 *= 0.9f;
					}
					if (num2 >= 0.93f)
					{
						float num5 = ((num2 == 1f) ? 1f : 0.5f);
						y -= pad * 0.8f + num * point * 0.4f * num5;
						num4 *= ((num2 == 1f) ? (0.62f - 0.4f * Math.Min(1f, point)) : (0.86f - 0.2f * Math.Min(1f, point)));
						num3 *= ((num2 == 1f) ? (0.62f - 0.3f * Math.Min(1f, point)) : (0.9f - 0.15f * Math.Min(1f, point)));
					}
					list.Add(new Vec3(cx, y, b.Sole + num3 - pad * 0.6f + ((num2 >= 0.93f) ? ((0f - num3) * 0.15f * point) : 0f)));
					list2.Add(num4);
					list3.Add(num3);
				}
			}
			if (list.Count >= 2)
			{
				k.LoftAlong(slot, list.ToArray(), list2.ToArray(), list3.ToArray(), new Vec3(1f, 0f, 0f), new Vec3(0f, 0f, 1f), k.Sides(8));
			}
		}

		private static void Sole(Kit k, LegFit b, int slot, float pad, float point = 0f)
		{
			if (!k.Mid)
			{
				return;
			}
			float[] array = new float[4] { 0f, 0.35f, 0.75f, 1f };
			Vec3[] array2 = new Vec3[array.Length];
			float[] array3 = new float[array.Length];
			float[] array4 = new float[array.Length];
			float num = Math.Abs(b.HeelY - b.ToeY);
			for (int i = 0; i < array.Length; i++)
			{
				b.Station(Math.Min(0.96f, array[i]), out var y, out var cx, out var half, out var _);
				if (i == 0)
				{
					y += pad;
				}
				if (i == array.Length - 1)
				{
					y -= pad + num * point * 0.4f;
				}
				array2[i] = new Vec3(cx, y, b.Sole + 0.006f * k.U);
				array3[i] = (half * 1.08f + pad * 1.15f) * ((i == 0) ? 0.8f : ((i == array.Length - 1) ? (0.55f - 0.35f * Math.Min(1f, point)) : 1f));
				array4[i] = 0.008f * k.U;
			}
			k.LoftAlong(slot, array2, array3, array4, new Vec3(1f, 0f, 0f), new Vec3(0f, 0f, 1f), k.Sides(6));
		}

		private static Vec3 ToeTip(LegFit b, float pad, float point)
		{
			b.Station(0.96f, out var y, out var cx, out var _, out var top);
			float num = Math.Abs(b.HeelY - b.ToeY);
			return new Vec3(cx, y - pad * 0.8f - num * point * 0.4f, b.Sole + (top - b.Sole) * 0.3f);
		}

		private static void Boot(Kit k, MeshPart p)
		{
			LegFit b = LegFit.Of(p);
			if (b == null)
			{
				return;
			}
			float num = 0.011f * k.U;
			float H = b.H;
			int num2 = k.Sides(10);
			if (!k.Mid)
			{
				k.Loft(k.LegSlot, Shaft(b, 0.97f, num, 1.02f), (k.M == Metal.Adamantine) ? k.Facets(6) : num2, k.M != Metal.Adamantine);
				Foot(k, b, k.LegSlot, num, (k.M == Metal.Copper) ? 0.35f : ((k.M == Metal.Mythril) ? 0.45f : ((k.M == Metal.Bronze) ? 0.25f : 0.1f)));
				return;
			}
			switch (k.M)
			{
			case Metal.Leather:
			{
				Ring[] array4 = Shaft(b, 0.5f, num * 1.4f, 1.1f, 1.1f);
				k.Loft(k.Shade, new Ring[3]
				{
					b.Shin(0.45f).Grow(1.05f, num * 0.6f).Up(b.Sole + H * 0.45f),
					b.Shin(0.75f).Grow(1.05f, num * 0.6f),
					b.Shin(0.98f).Grow(1.05f, num * 0.6f).Up(b.Sole + H * 1f)
				}, num2);
				k.Loft(k.Main, array4, num2);
				Foot(k, b, k.Main, num * 1.6f, 0.06f);
				Sole(k, b, k.Dark, num * 1.6f, 0.06f);
				Ring ring4 = array4[array4.Length - 1];
				k.Plate(k.Light, k.Inside, new Ring[3]
				{
					ring4.Grow(1.16f, 0.006f * k.U).Up(ring4.Z - H * 0.14f),
					ring4.Grow(1.1f, 0.005f * k.U).Up(ring4.Z - H * 0.04f),
					ring4.Grow(1.03f, 0.003f * k.U).Up(ring4.Z + H * 0.01f)
				}, 0f, MathF.PI * 2f, 0.006f * k.U, num2);
				k.Band(k.TrimDark, S(array4, 0.12f), H * 0.025f, 0.006f * k.U, num2);
				float[] array3 = new float[4]
				{
					-2.0707965f,
					MathF.PI * -121f / 355f,
					0f,
					MathF.PI
				};
				foreach (float th2 in array3)
				{
					k.Stud(k.Rivet, S(array4, 0.12f).Grow(1f, 0.006f * k.U), th2, b.Sole + H * 0.12f, 0.009f * k.U);
				}
				k.Band(k.TrimDark, S(array4, 0.24f), H * 0.025f, 0.003f * k.U, num2);
				k.Band(k.TrimDark, b.Shin(0.82f).Grow(1.05f, num * 0.6f), H * 0.025f, 0.003f * k.U, num2);
				array3 = new float[2]
				{
					0f,
					MathF.PI
				};
				foreach (float th3 in array3)
				{
					k.Stud(k.Rivet, S(array4, 0.24f).Grow(1f, 0.003f * k.U), th3, b.Sole + H * 0.24f, 0.009f * k.U);
				}
				Poleyn(k, b, k.Main, k.Shade, 0.75f);
				break;
			}
			case Metal.Copper:
			{
				Ring[] array11 = Shaft(b, 0.82f, num, 1.05f);
				k.Loft(k.Trim, new Ring[2]
				{
					b.Shin(0.76f).Grow(1.05f, num * 0.7f),
					b.Shin(0.98f).Grow(1.05f, num * 0.7f).Up(b.Sole + H * 1f)
				}, num2);
				k.Loft(k.Main, array11, num2);
				Foot(k, b, k.Main, num, 0.42f);
				Sole(k, b, k.Dark, num, 0.42f);
				if (k.Mid)
				{
					float[] array3 = new float[4] { -0.9f, -0.3f, 0.3f, 0.9f };
					foreach (float num5 in array3)
					{
						Vec3[] path = new Vec3[3]
						{
							S(array11, 0.32f).At(-MathF.PI / 2f + num5),
							S(array11, 0.5f).At(-MathF.PI / 2f + num5),
							S(array11, 0.68f).At(-MathF.PI / 2f + num5)
						};
						Vec3[] outward = new Vec3[3]
						{
							S(array11, 0.32f).N(-MathF.PI / 2f + num5),
							S(array11, 0.5f).N(-MathF.PI / 2f + num5),
							S(array11, 0.68f).N(-MathF.PI / 2f + num5)
						};
						k.Fin(k.Shade, path, outward, Heights(3, 0.004f * k.U), 0.004f * k.U);
					}
				}
				Ring ring5 = array11[array11.Length - 1];
				k.Plate(k.Trim, k.TrimDark, new Ring[2]
				{
					ring5.Grow(1.1f, 0.006f * k.U).Up(ring5.Z - H * 0.14f),
					ring5.Grow(1.05f, 0.004f * k.U).Up(ring5.Z + H * 0.01f)
				}, 0f, MathF.PI * 2f, 0.004f * k.U, num2);
				Poleyn(k, b, k.Main, k.Shade, 0.9f);
				break;
			}
			case Metal.Bronze:
			{
				Ring[] array10 = Shaft(b, 0.96f, num, 1.03f);
				k.Loft(k.Main, array10, num2);
				Foot(k, b, k.Main, num, 0.35f);
				Sole(k, b, k.Dark, num, 0.35f);
				Ring[] rings2 = new Ring[3]
				{
					S(array10, 0.3f).Grow(1f, 0.004f * k.U),
					S(array10, 0.55f).Grow(1f, 0.005f * k.U),
					S(array10, 0.84f).Grow(1f, 0.004f * k.U)
				};
				k.Plate(k.Trim, k.TrimDark, rings2, -2.6207962f, -0.5207964f, 0.004f * k.U);
				k.Band(k.TrimLight, S(array10, 0.3f).Grow(1f, 0.003f * k.U), H * 0.012f, 0.002f * k.U, num2);
				Foot(k, b, k.Trim, num, 0.35f, 0.75f, 1f, 0.004f * k.U);
				CurlToe(k, b, k.Trim, num, 0.35f, 0.012f * k.U, 2.1f);
				Spur(k, b, k.Trim, num);
				k.Band(k.Trim, S(array10, 0.16f), H * 0.035f, 0.005f * k.U, num2);
				Poleyn(k, b, k.Trim, k.Main, 1f);
				break;
			}
			case Metal.Silver:
			{
				Ring[] array8 = Shaft(b, 0.97f, num, 1.08f);
				k.Loft(k.Main, array8, num2);
				Sole(k, b, k.Dark, num, 0.55f);
				float[] array9 = new float[5] { 0f, 0.42f, 0.62f, 0.8f, 1f };
				for (int l = 0; l + 1 < array9.Length; l++)
				{
					Foot(k, b, (l % 2 == 0) ? k.Main : k.Light, num, 0.55f, array9[l], array9[l + 1], (float)l * 0.0015f * k.U);
				}
				Ridge(k, array8, b.Sole + H * 0.3f, b.Sole + H * 0.8f, k.Light, 0.004f * k.U);
				k.Band(k.Light, S(array8, 0.3f), H * 0.02f, 0.003f * k.U, num2);
				k.Band(k.Shade, S(array8, 0.82f), H * 0.03f, 0.004f * k.U, num2);
				Poleyn(k, b, k.Light, k.Main, 1f);
				break;
			}
			case Metal.Iron:
			{
				Ring[] array2 = Shaft(b, 0.97f, num, 1.03f);
				k.Loft(k.Trim, array2, num2);
				Foot(k, b, k.Trim, num, 0.1f);
				Sole(k, b, k.Dark, num, 0.1f);
				Ring[] rings = new Ring[3]
				{
					S(array2, 0.22f).Grow(1f, 0.005f * k.U),
					S(array2, 0.54f).Grow(1f, 0.006f * k.U),
					S(array2, 0.86f).Grow(1f, 0.005f * k.U)
				};
				float[] array3 = new float[2] { -0.75f, 0.75f };
				foreach (float num3 in array3)
				{
					k.Plate(k.Main, k.Shade, rings, -MathF.PI / 2f + num3 - 0.2f, -MathF.PI / 2f + num3 + 0.2f, 0.006f * k.U, 2);
				}
				k.Plate(k.Main, k.Shade, new Ring[2]
				{
					S(array2, 0.52f).Grow(1f, 0.006f * k.U),
					S(array2, 0.58f).Grow(1f, 0.006f * k.U)
				}, -2.5207963f, -0.6207964f, 0.006f * k.U);
				k.Plate(k.Light, k.Shade, new Ring[2]
				{
					S(array2, 0.84f).Grow(1f, 0.006f * k.U),
					S(array2, 0.88f).Grow(1f, 0.006f * k.U)
				}, -2.5207963f, -0.6207964f, 0.006f * k.U);
				k.Band(k.Main, S(array2, 0.2f), H * 0.03f, 0.007f * k.U, num2);
				Foot(k, b, k.Main, num, 0.04f, 0.55f, 1f, 0.009f * k.U);
				Foot(k, b, k.Main, num, 0f, 0f, 0.2f, 0.007f * k.U);
				array3 = new float[2]
				{
					0f,
					MathF.PI
				};
				foreach (float th in array3)
				{
					k.Stud(k.Rivet, S(array2, 0.33f).Grow(1f, 0.006f * k.U), th, b.Sole + H * 0.33f, 0.009f * k.U);
					k.Stud(k.Rivet, S(array2, 0.7f).Grow(1f, 0.006f * k.U), th, b.Sole + H * 0.7f, 0.009f * k.U);
				}
				Poleyn(k, b, k.Main, k.Light, 1f);
				break;
			}
			case Metal.Steel:
			{
				Ring[] array5 = Shaft(b, 0.98f, num, 1.02f);
				k.Loft(k.Main, array5, k.Sides(12));
				Ridge(k, array5, b.Sole + H * 0.3f, b.Sole + H * 0.85f, k.Light, 0.005f * k.U);
				Sole(k, b, k.Dark, num, 0.35f);
				float[] array6 = new float[6] { 0f, 0.38f, 0.56f, 0.74f, 0.88f, 1f };
				for (int j = 0; j + 1 < array6.Length; j++)
				{
					Foot(k, b, (j % 2 == 0) ? k.Main : k.Shade, num, 0.35f, array6[j], array6[j + 1], (float)j * 0.0015f * k.U);
				}
				k.Band(k.Shade, S(array5, 0.24f), H * 0.025f, 0.003f * k.U, k.Sides(12));
				if (k.Mid)
				{
					float[] array3 = new float[2]
					{
						0f,
						MathF.PI
					};
					foreach (float num4 in array3)
					{
						k.OnRing(k.Trim, S(array5, 0.55f), num4 + ((num4 == 0f) ? 0.4f : (-0.4f)), b.Sole + H * 0.55f, 0.012f * k.U, H * 0.02f, 0.004f * k.U);
					}
				}
				Poleyn(k, b, k.Light, k.Shade, 1.3f);
				break;
			}
			case Metal.Mythril:
			{
				Ring[] array7 = Shaft(b, 0.97f, num, 1.04f);
				k.Loft(k.Main, array7, num2);
				Foot(k, b, k.Main, num, 0.6f);
				Sole(k, b, k.Accent, num, 0.6f);
				CurlToe(k, b, k.Main, num, 0.6f, 0.012f * k.U, 2.4f);
				CurlToe(k, b, k.Accent, num + 0.002f * k.U, 0.6f, 0.0055f * k.U, 2.4f);
				Ridge(k, array7, b.Sole + H * 0.12f, b.Sole + H * 0.82f, k.Accent, 0.003f * k.U, 0.003f * k.U);
				if (k.Mid)
				{
					Scales(k, array7, b.Sole + H * 0.32f, b.Sole + H * 0.8f, k.Light, 4, k.Fine ? 10 : 6);
				}
				k.Band(k.Dark, S(array7, 0.84f), H * 0.03f, 0.004f * k.U, num2);
				k.Band(k.Accent, S(array7, 0.8f), H * 0.008f, 0.005f * k.U, num2);
				Poleyn(k, b, k.Main, k.Accent, 1.05f);
				break;
			}
			default:
			{
				Ring[] array = Shaft(b, 0.98f, num, 1.04f);
				k.Loft(k.Main, array, k.Facets(6), smooth: false);
				Foot(k, b, k.Main, num, 0.15f);
				Sole(k, b, k.Dark, num, 0.15f);
				Foot(k, b, k.Light, num, 0.08f, 0.55f, 1f, 0.01f * k.U);
				Ring ring = S(array, 0.8f);
				k.Loft(k.Accent, new Ring[2]
				{
					ring.Grow(1.03f, 0.004f * k.U).Up(ring.Z - H * 0.045f),
					ring.Grow(1.04f, 0.005f * k.U).Up(ring.Z + H * 0.045f)
				}, k.Facets(6), smooth: false);
				if (k.Mid)
				{
					k.Loft(k.AccentHot, new Ring[2]
					{
						ring.Grow(1.05f, 0.006f * k.U).Up(ring.Z - H * 0.015f),
						ring.Grow(1.05f, 0.006f * k.U).Up(ring.Z + H * 0.015f)
					}, k.Facets(6), smooth: false);
				}
				k.Loft(k.Dark, new Ring[2]
				{
					ring.Grow(1.06f, 0.006f * k.U).Up(ring.Z + H * 0.045f),
					ring.Grow(1.06f, 0.006f * k.U).Up(ring.Z + H * 0.1f)
				}, k.Facets(6), smooth: false);
				Ring ring2 = S(array, 0.45f);
				Lava(k, S(array, 0.72f).At(-1.3207964f, 0.002f * k.U), S(array, 0.5f).At(-1.6707964f, 0.002f * k.U), ring2.N(-MathF.PI / 2f), 0.0035f * k.U);
				Lava(k, S(array, 0.5f).At(-1.6707964f, 0.002f * k.U), S(array, 0.3f).At(-1.3907964f, 0.002f * k.U), ring2.N(-MathF.PI / 2f), 0.003f * k.U);
				Lava(k, S(array, 0.62f).At(-0.8207964f, 0.002f * k.U), S(array, 0.42f).At(-0.6207964f, 0.002f * k.U), S(array, 0.5f).N(-0.72079635f), 0.0028f * k.U);
				Poleyn(k, b, k.Dark, k.Main, 1.15f);
				if (k.Mid)
				{
					Ring ring3 = b.Shin(0.96f).Grow(1.1f, num);
					Vec3 vec = ring3.Up(b.Sole + H * 0.97f).At(-MathF.PI / 2f) + ring3.N(-MathF.PI / 2f) * (0.012f * k.U);
					k.Spike(k.Dark, vec, vec + (ring3.N(-MathF.PI / 2f) * 1f + new Vec3(0f, 0f, 0.35f)).Normalized() * (H * 0.12f), 0.01f * k.U, 4);
				}
				break;
			}
			}
			Ring S(Ring[] shaft, float f)
			{
				return ShellAt(shaft, b.Sole + H * f);
			}
		}

		private static void Poleyn(Kit k, LegFit b, int slot, int wing, float size)
		{
			float h = b.H;
			float pad = 0.011f * k.U;
			Ring ring = b.Shin(0.95f).Grow(1.08f, pad);
			float num = b.Sole + h * 0.98f;
			Vec3 vec = ring.N(-MathF.PI / 2f);
			Vec3 c = ring.Up(num).At(-MathF.PI / 2f) + vec * (0.004f * k.U);
			k.Plate(wing, k.Dark, new Ring[3]
			{
				ring.Grow(1f, 0.003f * k.U).Up(num - h * 0.07f * size),
				ring.Grow(1f, 0.005f * k.U).Up(num),
				ring.Grow(1f, 0.003f * k.U).Up(num + h * 0.05f * size)
			}, -2.9207964f, -0.22079635f, 0.004f * k.U, k.Sides(6));
			k.Blob(slot, c, ring.Rx * 0.7f * size, 0.012f * k.U * size, h * 0.075f * size, k.Sides(8));
		}

		private static void Cuisse(Kit k, MeshPart p)
		{
			Bounds(p, out var lo, out var _);
			float len = p.Pivot.Z - lo.Z;
			if (len <= 0.05f)
			{
				return;
			}
			float pad = (0.009f + 0.006f * k.Bulk) * k.U;
			float[] array = new float[4] { 0.04f, 0.3f, 0.6f, 0.88f };
			Ring[] r = new Ring[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				r[i] = Fit(p, lo.Z + len * array[i], len * 0.08f).Grow(1.06f, pad);
			}
			for (int j = 1; j < r.Length; j++)
			{
				r[j].Rx = Math.Max(r[j].Rx, r[j - 1].Rx * 0.98f);
				r[j].Ry = Math.Max(r[j].Ry, r[j - 1].Ry * 0.98f);
			}
			Ring[] array2 = new Ring[r.Length + 1];
			array2[0] = r[0].Up(lo.Z - len * 0.06f);
			Array.Copy(r, 0, array2, 1, r.Length);
			r = array2;
			Ring[] rings = new Ring[3]
			{
				At(0.1f).Grow(1f, 0.004f * k.U),
				At(0.45f).Grow(1f, 0.005f * k.U),
				At(0.85f).Grow(1f, 0.004f * k.U)
			};
			int sides = k.Sides(10);
			if (!k.Mid)
			{
				k.Loft((k.M == Metal.Leather) ? k.Shade : k.LegSlot, r, sides, smooth: true, 0f, caps: false);
				return;
			}
			switch (k.M)
			{
			case Metal.Leather:
				k.Loft(k.Shade, r, sides);
				k.Plate(k.Light, k.Shade, rings, -2.3707964f, -0.77079636f, 0.003f * k.U);
				k.Band(k.TrimDark, At(0.3f), len * 0.025f, 0.004f * k.U, sides);
				k.Band(k.TrimDark, At(0.7f), len * 0.025f, 0.004f * k.U, sides);
				k.Stud(k.Rivet, At(0.3f).Grow(1f, 0.004f * k.U), -MathF.PI / 2f, At(0.3f).Z, 0.008f * k.U);
				break;
			case Metal.Copper:
			{
				k.Loft(k.Trim, r, sides);
				k.Plate(k.Main, k.Shade, rings, -2.9207964f, -0.22079635f, 0.004f * k.U);
				k.Plate(k.Shade, k.Shade, new Ring[2]
				{
					At(0.42f).Grow(1f, 0.006f * k.U),
					At(0.48f).Grow(1f, 0.006f * k.U)
				}, -2.9207964f, -0.22079635f, 0.002f * k.U);
				float[] array3 = new float[2] { -1f, 1f };
				foreach (float num in array3)
				{
					k.Stud(k.Rivet, At(0.7f).Grow(1f, 0.006f * k.U), -MathF.PI / 2f + num * 1.1f, At(0.7f).Z, 0.008f * k.U);
				}
				break;
			}
			case Metal.Bronze:
				k.Loft(k.Main, r, sides);
				k.Plate(k.Trim, k.TrimDark, rings, -2.7707963f, -0.37079632f, 0.004f * k.U);
				k.Band(k.TrimLight, At(0.12f), len * 0.02f, 0.006f * k.U, sides);
				break;
			case Metal.Silver:
				k.Loft(k.Main, r, sides);
				Ridge(k, r, lo.Z + len * 0.1f, lo.Z + len * 0.85f, k.Light, 0.005f * k.U);
				k.Band(k.Light, At(0.35f), len * 0.02f, 0.003f * k.U, sides);
				k.Band(k.Light, At(0.65f), len * 0.02f, 0.003f * k.U, sides);
				break;
			case Metal.Iron:
			{
				k.Loft(k.Trim, r, sides);
				k.Plate(k.Main, k.Shade, rings, -2.6707964f, -0.47079635f, 0.004f * k.U);
				k.Plate(k.Light, k.Shade, new Ring[2]
				{
					At(0.08f).Grow(1f, 0.006f * k.U),
					At(0.14f).Grow(1f, 0.006f * k.U)
				}, -2.6707964f, -0.47079635f, 0.002f * k.U);
				float[] array3 = new float[2] { -1f, 1f };
				foreach (float num2 in array3)
				{
					k.Stud(k.Rivet, At(0.5f).Grow(1f, 0.007f * k.U), -MathF.PI / 2f + num2 * 0.9f, At(0.5f).Z, 0.008f * k.U);
				}
				break;
			}
			case Metal.Steel:
			{
				k.Loft(k.Main, r, k.Sides(12));
				Ridge(k, r, lo.Z + len * 0.1f, lo.Z + len * 0.7f, k.Light, 0.006f * k.U);
				for (int m = 0; m < 2; m++)
				{
					k.Plate((m == 0) ? k.Shade : k.Main, k.Dark, new Ring[2]
					{
						At(0.7f + (float)m * 0.1f).Grow(1f, (0.006f + (float)m * 0.002f) * k.U),
						At(0.8f + (float)m * 0.1f).Grow(1f, (0.004f + (float)m * 0.002f) * k.U)
					}, -2.9707963f, -0.1707964f, 0.003f * k.U);
				}
				k.Band(k.Light, At(0.08f), len * 0.02f, 0.005f * k.U, k.Sides(12));
				break;
			}
			case Metal.Mythril:
				k.Loft(k.Main, r, sides);
				if (k.Mid)
				{
					Scales(k, r, lo.Z + len * 0.1f, lo.Z + len * 0.85f, k.Light, 4, k.Fine ? 12 : 8);
				}
				Ridge(k, r, lo.Z + len * 0.05f, lo.Z + len * 0.9f, k.Accent, 0.003f * k.U, 0.003f * k.U);
				break;
			default:
				k.Loft(k.Main, r, k.Facets(6), smooth: false);
				k.Plate(k.Dark, k.Dark, rings, -2.6707964f, -0.47079635f, 0.004f * k.U, 3);
				Lava(k, At(0.75f).At(-1.8707964f, 0.006f * k.U), At(0.45f).At(-1.4707963f, 0.006f * k.U), At(0.6f).N(-MathF.PI / 2f), 0.0032f * k.U);
				Lava(k, At(0.45f).At(-1.4707963f, 0.006f * k.U), At(0.25f).At(-1.7207963f, 0.006f * k.U), At(0.35f).N(-MathF.PI / 2f), 0.0028f * k.U);
				break;
			}
			Ring At(float t)
			{
				return ShellAt(r, lo.Z + len * t);
			}
		}

		private static void CurlToe(Kit k, LegFit b, int slot, float pad, float point, float r, float high = 1f)
		{
			if (k.Mid)
			{
				Vec3 vec = ToeTip(b, pad, point);
				float num = Math.Abs(b.HeelY - b.ToeY);
				Vec3 a = vec + new Vec3(0f, num * 0.04f, 0f);
				Vec3 vec2 = vec + new Vec3(0f, (0f - num) * 0.12f, num * 0.08f * high);
				Vec3 vec3 = vec + new Vec3(0f, (0f - num) * 0.1f, num * 0.17f * high);
				Vec3 b2 = vec + new Vec3(0f, (0f - num) * 0.04f, num * 0.21f * high);
				k.Tube(slot, a, vec2, r, r * 0.75f, k.Sides(6));
				k.Tube(slot, vec2, vec3, r * 0.75f, r * 0.5f, k.Sides(6));
				k.Tube(slot, vec3, b2, r * 0.5f, r * 0.2f, k.Sides(6));
			}
		}

		private static void Spur(Kit k, LegFit b, int slot, float pad)
		{
			if (k.Mid)
			{
				b.Station(0f, out var y, out var cx, out var _, out var top);
				Vec3 vec = new Vec3(cx, y + pad, b.Sole + (top - b.Sole) * 0.7f);
				float num = Math.Abs(b.HeelY - b.ToeY);
				k.Spike(slot, vec - new Vec3(0f, pad, 0f), vec + new Vec3(0f, num * 0.32f, num * 0.04f), 0.008f * k.U, 4);
				k.Blob(slot, vec + new Vec3(0f, num * 0.3f, num * 0.04f), 0.012f * k.U, 0.003f * k.U, 0.012f * k.U, k.Sides(6));
			}
		}

		private static Ring[] ChestRings(MeshPart p, float pad, float bulk, out float lo, out float hi)
		{
			Bounds(p, out var lo2, out var hi2);
			lo = lo2.Z;
			hi = hi2.Z;
			float num = hi - lo;
			float[] array = new float[6] { 0f, 0.22f, 0.45f, 0.66f, 0.84f, 0.95f };
			float[] array2 = new float[6]
			{
				1.05f,
				1.06f + 0.05f * bulk,
				1.06f + 0.09f * bulk,
				1.05f + 0.11f * bulk,
				1.03f + 0.09f * bulk,
				0.9f + 0.04f * bulk
			};
			Ring[] array3 = new Ring[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array3[i] = Fit(p, lo + num * array[i], num * 0.07f).Grow(array2[i], pad * ((i == array.Length - 1) ? 0.6f : 1f));
			}
			for (int j = 0; j < array3.Length - 1; j++)
			{
				float num2 = ((j == 0) ? array3[1].Rx : Math.Min(array3[j - 1].Rx, array3[j + 1].Rx));
				float num3 = ((j == 0) ? array3[1].Ry : Math.Min(array3[j - 1].Ry, array3[j + 1].Ry));
				array3[j].Rx = Math.Max(array3[j].Rx, num2 * 0.97f);
				array3[j].Ry = Math.Max(array3[j].Ry, num3 * 0.97f);
			}
			float[] array4 = new float[6] { 0f, 0.35f, 0.8f, 1f, 0.7f, 0.2f };
			for (int k = 0; k < array3.Length; k++)
			{
				float num4 = bulk * num * 0.055f * array4[k];
				array3[k] = new Ring(array3[k].X, array3[k].Y - num4 * 0.5f, array3[k].Z, array3[k].Rx * (1f + ((k == 4) ? 0.07f : 0.02f) * bulk), array3[k].Ry + num4 * 0.5f);
			}
			return array3;
		}

		private static void Cuirass(Kit k, MeshModel body, MeshPart p)
		{
			float pad = (0.013f + 0.012f * k.Bulk) * k.U;
			float lo;
			float hi;
			Ring[] c = ChestRings(p, pad, k.Bulk, out lo, out hi);
			float h = hi - lo;
			int sides = k.Sides(12);
			switch (k.M)
			{
			case Metal.Leather:
			{
				k.Loft(k.Main, c, sides);
				for (int l = 0; l < 4; l++)
				{
					k.Band((l % 2 == 0) ? k.Light : k.Shade, At(0.16f + (float)l * 0.18f), h * 0.03f, 0.003f * k.U, sides);
				}
				Collar(k, At(0.95f).Grow(0.9f, 0f), h * 0.12f, 1.08f, 0f, k.Inside, -1);
				k.Band(k.Inside, At(0.03f), h * 0.04f, 0.005f * k.U, sides);
				for (int m = 0; m < 2; m++)
				{
					for (int n = -1; n <= 1; n++)
					{
						Ring r2 = At(0.34f + (float)m * 0.36f).Grow(1f, 0.004f * k.U);
						k.Stud(k.Rivet, r2, -MathF.PI / 2f + (float)n * 0.6f, r2.Z, 0.008f * k.U);
					}
				}
				if (k.Fine)
				{
					for (int num3 = 0; num3 < 5; num3++)
					{
						Ring r3 = At(0.28f + (float)num3 * 0.13f).Grow(1f, 0.003f * k.U);
						k.OnRing(k.TrimDark, r3, -MathF.PI / 2f, r3.Z, 0.012f * k.U, 0.0025f * k.U, 0.002f * k.U);
					}
				}
				Belt(k, At(0.03f), h, k.TrimDark, k.Rivet);
				return;
			}
			case Metal.Copper:
			{
				k.Loft(k.Main, c, sides);
				Ridge(k, c, lo + h * 0.05f, lo + h * 0.88f, k.Light, 0.013f * k.U, 0.009f * k.U);
				k.Plate(k.Shade, k.Shade, new Ring[2]
				{
					At(0.02f).Grow(1f, 0.004f * k.U),
					At(0.3f).Grow(1f, 0.004f * k.U)
				}, -2.9207964f, -0.22079635f, 0.004f * k.U);
				k.Band(k.Light, At(0.31f).Grow(1f, 0.004f * k.U), h * 0.012f, 0.002f * k.U, sides);
				k.Band(k.Trim, At(0.93f).Grow(0.93f, 0f), h * 0.045f, 0.005f * k.U, sides);
				float[] array = new float[2] { -1f, 1f };
				foreach (float num4 in array)
				{
					for (int num5 = 0; num5 < 3; num5++)
					{
						Ring r4 = At(0.42f + (float)num5 * 0.14f).Grow(1f, 0.002f * k.U);
						k.Stud(k.Rivet, r4, -MathF.PI / 2f + num4 * 1.2f, r4.Z, 0.009f * k.U);
					}
				}
				SideStraps(k, c, lo, h, k.Trim, k.TrimLight);
				Belt(k, At(0.02f), h, k.Trim, k.Rivet);
				return;
			}
			case Metal.Bronze:
			{
				k.Loft(k.Main, c, sides);
				Ring[] array3 = new Ring[c.Length - 1];
				for (int num7 = 0; num7 < array3.Length; num7++)
				{
					array3[num7] = c[num7].Grow(1f, 0.004f * k.U);
				}
				k.Plate(k.Trim, k.TrimDark, array3, -2.7707963f, -0.37079632f, 0.004f * k.U);
				if (k.Mid)
				{
					float[] array = new float[2] { -1f, 1f };
					foreach (float num8 in array)
					{
						Ring ring = At(0.6f).Grow(1f, 0.006f * k.U);
						Vec3 a = ring.At(-MathF.PI / 2f + num8 * 0.08f);
						Vec3 b = At(0.66f).Grow(1f, 0.006f * k.U).At(-MathF.PI / 2f + num8 * 0.75f);
						Crack(k, a, b, ring.N(-MathF.PI / 2f + num8 * 0.4f), k.TrimDark, 0.003f * k.U);
					}
					Ridge(k, c, lo + h * 0.12f, lo + h * 0.58f, k.TrimDark, 0.002f * k.U, 0.003f * k.U);
					Ridge(k, c, lo + h * 0.6f, lo + h * 0.86f, k.TrimLight, 0.004f * k.U, 0.004f * k.U);
					for (int num9 = 0; num9 < 2; num9++)
					{
						Ring r5 = At(0.24f + (float)num9 * 0.14f).Grow(1f, 0.006f * k.U);
						k.OnRing(k.TrimDark, r5, -MathF.PI / 2f, r5.Z, r5.Rx * 0.42f, 0.0025f * k.U, 0.003f * k.U);
					}
				}
				k.Band(k.TrimLight, At(0.93f).Grow(0.93f, 0f), h * 0.035f, 0.005f * k.U, sides);
				k.Band(k.TrimLight, At(0.02f), h * 0.025f, 0.006f * k.U, sides);
				SideStraps(k, c, lo, h, k.Dark, k.TrimLight);
				return;
			}
			case Metal.Silver:
				k.Loft(k.Main, c, sides);
				Ridge(k, c, lo + h * 0.04f, lo + h * 0.88f, k.Light, 0.007f * k.U);
				k.Plate(k.Main, k.Shade, new Ring[3]
				{
					At(0.03f).Grow(1f, 0.006f * k.U),
					At(0.18f).Grow(1f, 0.006f * k.U),
					At(0.32f).Grow(1f, 0.003f * k.U)
				}, -2.8707962f, -0.27079642f, 0.004f * k.U);
				k.Band(k.Light, At(0.32f).Grow(1f, 0.004f * k.U), h * 0.01f, 0.002f * k.U, sides);
				k.Band(k.Shade, At(0.92f).Grow(0.94f, 0f), h * 0.05f, 0.006f * k.U, sides);
				k.Band(k.Light, At(0.85f).Grow(0.98f, 0f), h * 0.02f, 0.005f * k.U, sides);
				k.Band(k.Light, At(0.03f), h * 0.02f, 0.006f * k.U, sides);
				SideStraps(k, c, lo, h, k.Dark, k.Light);
				return;
			case Metal.Iron:
			{
				k.Loft(k.Main, c, sides);
				k.Plate(k.Light, k.Shade, new Ring[2]
				{
					At(0.5f).Grow(1f, 0.004f * k.U),
					At(0.86f).Grow(1f, 0.004f * k.U)
				}, -2.5207963f, -0.6207964f, 0.004f * k.U);
				k.Plate(k.Main, k.Shade, new Ring[2]
				{
					At(0.52f).Grow(1f, 0.007f * k.U),
					At(0.84f).Grow(1f, 0.007f * k.U)
				}, -2.4207964f, -0.72079635f, 0.003f * k.U);
				float[] array = new float[2] { -1f, 1f };
				foreach (float num in array)
				{
					for (int j = 0; j < 2; j++)
					{
						Ring r = At(0.6f + (float)j * 0.16f).Grow(1f, 0.008f * k.U);
						k.Stud(k.Rivet, r, -MathF.PI / 2f + num * 0.72f, r.Z, 0.008f * k.U);
					}
				}
				Ring neck = At(0.95f).Grow(0.92f, 0.004f * k.U);
				Collar(k, neck, h * 0.16f, 1.16f, 0f, k.Dark, k.Light);
				if (k.Mid)
				{
					array = new float[2] { -1f, 1f };
					foreach (float num2 in array)
					{
						Vec3 vec = neck.Grow(1.16f, 0f).Up(neck.Z + h * 0.15f).At(-MathF.PI / 2f + num2 * 0.55f);
						k.Spike(k.Dark, vec - new Vec3(0f, 0f, h * 0.03f), vec + (neck.N(-MathF.PI / 2f + num2 * 0.55f) * 0.4f + new Vec3(0f, 0f, 1f)).Normalized() * (h * 0.09f), 0.009f * k.U, 4);
					}
				}
				SideStraps(k, c, lo, h, k.Trim, k.TrimLight);
				Belt(k, At(0.04f), h, k.TrimDark, k.TrimLight);
				return;
			}
			case Metal.Steel:
			{
				k.Loft(k.Main, c, k.Sides(14));
				Ridge(k, c, lo + h * 0.3f, lo + h * 0.88f, k.Light, 0.007f * k.U);
				Ring[] array2 = new Ring[3]
				{
					At(0.02f).Grow(1f, 0.007f * k.U),
					At(0.2f).Grow(1f, 0.007f * k.U),
					At(0.4f).Grow(1f, 0.003f * k.U)
				};
				k.Plate(k.Main, k.Shade, array2, -2.9707963f, -0.1707964f, 0.004f * k.U);
				k.Band(k.Light, At(0.4f).Grow(1f, 0.004f * k.U), h * 0.01f, 0.002f * k.U, k.Sides(14));
				for (int num6 = -2; num6 <= 2; num6++)
				{
					k.Stud(k.Rivet, array2[1].Grow(1f, 0.001f * k.U), -MathF.PI / 2f + (float)num6 * 0.5f, array2[1].Z + h * 0.08f, 0.007f * k.U);
				}
				k.Band(k.Shade, At(0.92f).Grow(0.92f, 0f), h * 0.04f, 0.007f * k.U, k.Sides(14));
				k.Band(k.Light, At(0.97f).Grow(0.86f, 0f), h * 0.03f, 0.006f * k.U, k.Sides(14));
				k.Band(k.Light, At(0.02f), h * 0.02f, 0.008f * k.U, k.Sides(14));
				SideStraps(k, c, lo, h, k.Trim, k.TrimLight);
				return;
			}
			case Metal.Mythril:
				k.Loft(k.Main, c, sides);
				if (k.Mid)
				{
					Scales(k, c, lo + h * 0.08f, lo + h * 0.85f, k.Light, k.Fine ? 7 : 5, k.Fine ? 18 : 12);
				}
				Ridge(k, c, lo, lo + h * 0.9f, k.Accent, 0.005f * k.U, 0.006f * k.U);
				Collar(k, At(0.95f).Grow(0.9f, 0.006f * k.U), h * 0.26f, 1.42f, 0.5f, k.Dark, k.Accent);
				Belt(k, At(0.04f), h, k.Dark, k.Accent);
				return;
			}
			k.Loft(k.Main, c, k.Facets(8), smooth: false);
			k.Plate(k.Dark, k.Dark, new Ring[2]
			{
				At(0f).Grow(1f, 0.007f * k.U),
				At(0.3f).Grow(1f, 0.007f * k.U)
			}, -3.0707965f, -0.07079637f, 0.004f * k.U, 4);
			k.Band(k.Dark, At(0.93f).Grow(0.92f, 0f), h * 0.05f, 0.008f * k.U, k.Facets(8), smooth: false);
			Ring ring2 = At(0.95f).Grow(0.9f, 0.008f * k.U);
			k.Plate(k.Dark, k.Dark, new Ring[2]
			{
				ring2.Up(ring2.Z - h * 0.03f),
				ring2.Grow(1.06f, 0f).Up(ring2.Z + h * 0.12f)
			}, 0.1707964f, 2.9707963f, 0.006f * k.U, 4);
			if (k.Mid)
			{
				for (int num10 = -1; num10 <= 1; num10++)
				{
					Vec3 vec2 = ring2.Up(ring2.Z + h * 0.08f).At(MathF.PI / 2f + (float)num10 * 0.7f);
					k.Spike(k.Dark, vec2, vec2 + (ring2.N(MathF.PI / 2f + (float)num10 * 0.7f) * 0.5f + new Vec3(0f, 0f, 1f)).Normalized() * (h * 0.14f), 0.008f * k.U, 4);
				}
			}
			for (int num11 = 0; num11 < 2; num11++)
			{
				float num12 = (float)num11 * 0.17f;
				List<Vec3> list = new List<Vec3>();
				List<Vec3> list2 = new List<Vec3>();
				(float, float)[] array4 = new(float, float)[5]
				{
					(-1f, 0.88f),
					(-0.5f, 0.72f),
					(0f, 0.56f),
					(0.5f, 0.72f),
					(1f, 0.88f)
				};
				for (int i = 0; i < array4.Length; i++)
				{
					(float, float) tuple = array4[i];
					float item = tuple.Item1;
					Ring ring3 = At(tuple.Item2 - num12);
					list.Add(ring3.At(-MathF.PI / 2f + item));
					list2.Add(ring3.N(-MathF.PI / 2f + item));
				}
				k.Fin((num11 == 0) ? k.Light : k.Shade, list.ToArray(), list2.ToArray(), Heights(list.Count, 0.009f * k.U), 0.007f * k.U);
			}
			Ridge(k, c, lo + h * 0.32f, lo + h * 0.56f, k.Light, 0.006f * k.U);
			(float, float)[][] array5 = new(float, float)[3][]
			{
				new(float, float)[5]
				{
					(-0.55f, 0.86f),
					(-0.3f, 0.72f),
					(-0.38f, 0.58f),
					(-0.12f, 0.46f),
					(0.04f, 0.32f)
				},
				new(float, float)[3]
				{
					(-0.3f, 0.72f),
					(0.04f, 0.67f),
					(0.3f, 0.8f)
				},
				new(float, float)[3]
				{
					(-0.12f, 0.46f),
					(0.32f, 0.52f),
					(0.5f, 0.42f)
				}
			};
			for (int num13 = 0; num13 < array5.Length; num13++)
			{
				for (int num14 = 0; num14 + 1 < array5[num13].Length; num14++)
				{
					(float, float) tuple2 = array5[num13][num14];
					float item2 = tuple2.Item1;
					float item3 = tuple2.Item2;
					(float, float) tuple3 = array5[num13][num14 + 1];
					float item4 = tuple3.Item1;
					float item5 = tuple3.Item2;
					Ring ring4 = At(item3);
					Ring ring5 = At(item5);
					Lava(k, ring4.At(-MathF.PI / 2f + item2, 0.002f * k.U), ring5.At(-MathF.PI / 2f + item4, 0.002f * k.U), ring4.N(-MathF.PI / 2f + (item2 + item4) * 0.5f), ((num13 == 0) ? 0.0042f : 0.003f) * k.U);
				}
			}
			Belt(k, At(0.03f), h, k.Dark, k.Light);
			Ring At(float f)
			{
				return ShellAt(c, lo + h * f);
			}
		}

		private static void Collar(Kit k, Ring neck, float tall, float flare, float open, int slot, int rim)
		{
			Ring ring = neck.Up(neck.Z - tall * 0.25f);
			Ring ring2 = neck.Grow(1f + (flare - 1f) * 0.45f, 0f).Up(neck.Z + tall * 0.45f);
			Ring ring3 = neck.Grow(flare, 0f).Up(neck.Z + tall);
			if (open <= 0f)
			{
				k.Plate(slot, k.Shade, new Ring[3] { ring, ring2, ring3 }, 0f, MathF.PI * 2f, 0.006f * k.U, k.Sides(14));
			}
			else
			{
				k.Plate(slot, k.Shade, new Ring[3] { ring, ring2, ring3 }, -MathF.PI / 2f + open, 4.712389f - open, 0.006f * k.U);
			}
			if (rim >= 0)
			{
				Ring ring4 = ring3.Grow(1f, 0.002f * k.U).Up(ring3.Z - tall * 0.12f);
				Ring ring5 = ring3.Grow(1f, 0.002f * k.U).Up(ring3.Z + tall * 0.02f);
				if (open <= 0f)
				{
					k.Plate(rim, k.Shade, new Ring[2] { ring4, ring5 }, 0f, MathF.PI * 2f, 0.003f * k.U, k.Sides(14));
				}
				else
				{
					k.Plate(rim, k.Shade, new Ring[2] { ring4, ring5 }, -MathF.PI / 2f + open, 4.712389f - open, 0.003f * k.U);
				}
			}
		}

		private static void Ridge(Kit k, Ring[] shell, float z0, float z1, int slot, float height, float half = 0f)
		{
			List<Vec3> list = new List<Vec3>();
			List<Vec3> list2 = new List<Vec3>();
			for (int i = 0; i <= 5; i++)
			{
				float z2 = z0 + (z1 - z0) * (float)i / 5f;
				Ring ring = ShellAt(shell, z2);
				list.Add(ring.At(-MathF.PI / 2f));
				list2.Add(ring.N(-MathF.PI / 2f));
			}
			k.Fin(slot, list.ToArray(), list2.ToArray(), Heights(list.Count, height), (half > 0f) ? half : (height * 0.8f));
		}

		private static void Belt(Kit k, Ring waist, float h, int strap, int buckle)
		{
			k.Band(strap, waist, h * 0.035f, 0.006f * k.U, k.Sides(12));
			if (k.Mid)
			{
				Ring r = waist.Grow(1f, 0.008f * k.U);
				k.OnRing(buckle, r, -MathF.PI / 2f, waist.Z, 0.014f * k.U, h * 0.04f, 0.004f * k.U);
				k.OnRing(strap, r, -MathF.PI / 2f, waist.Z, 0.007f * k.U, h * 0.022f, 0.0055f * k.U);
			}
		}

		private static void SideStraps(Kit k, Ring[] c, float lo, float h, int strap, int buckle)
		{
			if (!k.Mid)
			{
				return;
			}
			float[] array = new float[2]
			{
				0f,
				MathF.PI
			};
			foreach (float num in array)
			{
				float[] array2 = new float[2] { 0.36f, 0.62f };
				foreach (float num2 in array2)
				{
					Ring r = ShellAt(c, lo + h * num2).Grow(1f, 0.002f * k.U);
					k.OnRing(strap, r, num, r.Z, 0.018f * k.U, h * 0.022f, 0.003f * k.U);
					k.OnRing(buckle, r, num + ((num == 0f) ? (-0.12f) : 0.12f), r.Z, 0.008f * k.U, h * 0.03f, 0.005f * k.U);
				}
			}
		}

		private static void Lava(Kit k, Vec3 a, Vec3 b, Vec3 n, float w)
		{
			Crack(k, a, b, n, k.Accent, w * 2.1f);
			if (k.Mid)
			{
				Crack(k, a + n * (w * 0.9f), b + n * (w * 0.9f), n, k.AccentHot, w * 0.85f);
			}
		}

		private static void Crack(Kit k, Vec3 a, Vec3 b, Vec3 n, int slot, float w)
		{
			if (k.Mid)
			{
				Vec3 vec = b - a;
				float num = (float)Math.Sqrt(Vec3.Dot(vec, vec));
				if (!(num < 0.0001f))
				{
					Vec3 vec2 = vec * (1f / num);
					Vec3 vec3 = Vec3.Cross(n, vec2).Normalized();
					Vec3 vec4 = Vec3.Cross(vec2, vec3);
					k.Box(slot, (a + b) * 0.5f, vec2 * (num * 0.5f), vec3 * w, vec4 * (w * 0.8f));
				}
			}
		}

		private static void Scales(Kit k, Ring[] shell, float z0, float z1, int slot, int rows, int per)
		{
			if (!k.Mid)
			{
				return;
			}
			if (!k.Fine)
			{
				rows = Math.Max(2, (rows + 1) / 2);
			}
			float num = (z1 - z0) / (float)rows;
			for (int i = 0; i < rows; i++)
			{
				float num2 = z0 + num * (float)i;
				float z2 = num2 + num * 0.95f;
				Ring ring = ShellAt(shell, num2);
				Ring ring2 = ShellAt(shell, z2);
				k.Loft((i % 2 == 0) ? slot : k.Shade, new Ring[3]
				{
					ring.Grow(1f, 0.0015f * k.U).Up(num2 - num * 0.02f),
					ring.Grow(1f, 0.0048f * k.U).Up(num2 + num * 0.06f),
					ring2.Grow(1f, 0.0015f * k.U).Up(num2 + num * 0.3f)
				}, k.Sides(12), smooth: true, 0f, caps: false);
				if (k.Fine)
				{
					for (int j = 0; j < per; j += 2)
					{
						float th = -MathF.PI / 2f + ((float)j + (float)(i % 2) * 0.5f) * 2f * MathF.PI / (float)per;
						k.OnRing(k.Dark, ring.Grow(1f, 0.0045f * k.U), th, num2 + num * 0.2f, 0.0015f * k.U, num * 0.22f, 0.0015f * k.U);
					}
				}
			}
		}

		private static void Skirt(Kit k, MeshModel body, MeshPart p)
		{
			Bounds(p, out var lo, out var hi);
			float pad = 0.013f * k.U;
			float top = hi.Z;
			float num = 1.8f * k.U;
			Ring waist = Fit(p, top - 0.025f * k.U, 0.03f * k.U).Grow(1.06f, pad);
			float num2 = Math.Min(top - lo.Z + 0.01f * k.U, num * 0.14f);
			int slot = ((k.M == Metal.Copper || k.M == Metal.Iron) ? k.Trim : ((k.M == Metal.Bronze) ? k.Main : ((k.M == Metal.Leather || k.M == Metal.Steel) ? k.Shade : k.Dark)));
			k.Loft(slot, new Ring[3]
			{
				Fit(p, top - num2, 0.03f * k.U).Grow(1.04f, pad * 0.6f).Up(top - num2),
				Ring.Lerp(Down(num2 * 0.5f, 1.02f), Fit(p, top - num2 * 0.5f, 0.03f * k.U).Grow(1.04f, pad * 0.6f), 0.5f),
				Down(-0.005f * k.U, 1f)
			}, k.Sides(12));
			switch (k.M)
			{
			case Metal.Leather:
			{
				float num5 = num * 0.07f;
				k.Loft(k.Shade, new Ring[3]
				{
					Down(num5, 1.12f),
					Down(num5 * 0.5f, 1.06f),
					Down(-0.01f * k.U, 1f)
				}, k.Sides(12));
				k.Band(k.Light, Down(num5 * 0.25f, 1.04f), num5 * 0.12f, 0.003f * k.U, k.Sides(12));
				for (int j = -2; j <= 2; j++)
				{
					k.Stud(k.Rivet, Down(num5 * 0.6f, 1.09f), -MathF.PI / 2f + (float)j * 0.55f, top - num5 * 0.6f, 0.01f * k.U);
				}
				break;
			}
			case Metal.Copper:
			{
				k.Loft(k.Trim, new Ring[2]
				{
					Down(num * 0.04f, 1.02f),
					Down(-0.01f * k.U, 1f)
				}, k.Sides(12));
				for (int num15 = 0; num15 < 3; num15++)
				{
					float num16 = num * (0.02f + (float)num15 * 0.04f);
					float d = num16 + num * 0.05f;
					k.Plate((num15 % 2 == 0) ? k.Main : k.Shade, k.Shade, new Ring[2]
					{
						Down(d, 1.12f + (float)num15 * 0.06f),
						Down(num16, 1.04f + (float)num15 * 0.06f)
					}, 0f, MathF.PI * 2f, 0.004f * k.U, k.Sides(12));
				}
				for (int num17 = 0; num17 < 5; num17++)
				{
					float num18 = num * (0.04f + (float)num17 * 0.035f);
					float d2 = num18 + num * 0.04f;
					k.Plate((num17 % 2 == 0) ? k.Main : k.Shade, k.Shade, new Ring[2]
					{
						Down(d2, 1.2f + (float)num17 * 0.05f),
						Down(num18, 1.16f + (float)num17 * 0.05f)
					}, -1.9907963f, -1.1507964f, 0.006f * k.U, 3);
				}
				break;
			}
			case Metal.Bronze:
			{
				k.Band(k.Trim, Down(num * 0.01f, 1.02f), num * 0.012f, 0.004f * k.U, k.Sides(12));
				int num10 = (k.Fine ? 16 : (k.Mid ? 12 : 8));
				float num11 = MathF.PI * 2f / (float)num10;
				for (int num12 = 1; num12 < num10; num12++)
				{
					float num13 = -MathF.PI / 2f + (float)num12 * num11;
					k.Plate((num12 % 2 == 0) ? k.Trim : k.Main, k.Shade, new Ring[2]
					{
						Down(num * 0.13f, 1.22f),
						Down(num * 0.02f, 1.06f)
					}, num13 - num11 * 0.45f, num13 + num11 * 0.45f, 0.004f * k.U, 1);
				}
				Ring[] rings = new Ring[3]
				{
					Down(num * 0.2f, 1.3f),
					Down(num * 0.1f, 1.18f),
					Down(num * 0.01f, 1.08f)
				};
				k.Plate(k.Trim, k.TrimDark, rings, -1.9707963f, -1.1707964f, 0.006f * k.U, 3);
				k.Plate(k.TrimLight, k.TrimDark, new Ring[2]
				{
					Down(num * 0.2f, 1.31f),
					Down(num * 0.18f, 1.29f)
				}, -1.9707963f, -1.1707964f, 0.007f * k.U, 3);
				break;
			}
			case Metal.Silver:
			{
				k.Loft(k.Mid ? k.Dark : k.Main, new Ring[2]
				{
					Down(num * 0.1f, 1.14f),
					Down(-0.01f * k.U, 1f)
				}, k.Sides(12));
				int num8 = (k.Mid ? 3 : 0);
				int num9 = (k.Fine ? 14 : 10);
				for (int m = 0; m < num8; m++)
				{
					Ring r = Down(num * (0.015f + (float)m * 0.03f) + num * 0.015f, 1.04f + (float)m * 0.04f);
					for (int n = 0; n < num9; n++)
					{
						float th2 = -MathF.PI / 2f + ((float)n + (float)(m % 2) * 0.5f) * 2f * MathF.PI / (float)num9;
						k.OnRing(((n + m) % 2 == 0) ? k.Light : k.Main, r, th2, r.Z, (float)Math.Sqrt(r.Rx * r.Ry) * MathF.PI / (float)num9 * 0.95f, num * 0.017f, 0.005f * k.U);
					}
				}
				break;
			}
			case Metal.Iron:
			{
				k.Loft(k.Trim, new Ring[3]
				{
					Down(num * 0.12f, 1.2f),
					Down(num * 0.06f, 1.1f),
					Down(-0.01f * k.U, 1f)
				}, k.Sides(12));
				k.Band(k.TrimDark, Down(num * 0.07f, 1.12f), num * 0.008f, 0.003f * k.U, k.Sides(12));
				for (int num14 = -1; num14 <= 1; num14++)
				{
					k.Stud(k.TrimLight, Down(num * 0.07f, 1.12f).Grow(1f, 0.003f * k.U), -MathF.PI / 2f + (float)num14 * 0.7f, top - num * 0.07f, 0.012f * k.U);
				}
				break;
			}
			case Metal.Steel:
			{
				for (int num19 = 0; num19 < 3; num19++)
				{
					float num20 = num * ((float)num19 * 0.025f) - 0.01f * k.U;
					float d3 = num20 + num * 0.035f;
					k.Loft((num19 % 2 == 0) ? k.Main : k.Shade, new Ring[2]
					{
						Down(d3, 1.06f + (float)num19 * 0.05f),
						Down(num20, 1.02f + (float)num19 * 0.05f)
					}, k.Sides(14));
				}
				float[] array2 = new float[2] { -1f, 1f };
				foreach (float num21 in array2)
				{
					k.Plate(k.Main, k.Shade, new Ring[2]
					{
						Down(num * 0.15f, 1.3f),
						Down(num * 0.07f, 1.2f)
					}, -MathF.PI / 2f + num21 * 0.04f, -MathF.PI / 2f + num21 * 1.15f, 0.004f * k.U, 3);
				}
				break;
			}
			case Metal.Mythril:
			{
				float num6 = num * 0.2f;
				Ring[] array = new Ring[3]
				{
					Down(num6, 1.3f),
					Down(num6 * 0.5f, 1.15f),
					Down(-0.01f * k.U, 1.02f)
				};
				k.Plate(k.Main, k.Shade, array, -1.2907964f, 4.432389f, 0.004f * k.U);
				if (k.Mid)
				{
					Scales(k, array, top - num6 * 0.9f, top, k.Light, 3, k.Fine ? 14 : 10);
				}
				float[] array2 = new float[2] { -1f, 1f };
				foreach (float num7 in array2)
				{
					float th = -MathF.PI / 2f + num7 * 0.28f;
					Vec3[] path = new Vec3[3]
					{
						array[0].At(th, 0.001f * k.U),
						array[1].At(th, 0.001f * k.U),
						array[2].At(th, 0.001f * k.U)
					};
					Vec3[] outward = new Vec3[3]
					{
						array[0].N(th),
						array[1].N(th),
						array[2].N(th)
					};
					k.Fin(k.Accent, path, outward, Heights(3, 0.003f * k.U), 0.004f * k.U);
				}
				break;
			}
			default:
			{
				for (int i = 0; i < 3; i++)
				{
					float num3 = num * ((float)i * 0.035f) - 0.01f * k.U;
					float num4 = num3 + num * 0.03f;
					k.Loft(k.Main, new Ring[2]
					{
						Down(num4, 1.08f + (float)i * 0.07f),
						Down(num3, 1.04f + (float)i * 0.07f)
					}, k.Facets(8), smooth: false);
					k.Loft(k.Accent, new Ring[2]
					{
						Down(num4 + num * 0.006f, 1.03f + (float)i * 0.07f),
						Down(num4 - num * 0.003f, 1.03f + (float)i * 0.07f)
					}, k.Facets(8), smooth: false);
				}
				break;
			}
			}
			Ring Down(float num22, float flare)
			{
				return new Ring(waist.X, waist.Y, top - num22, waist.Rx * flare + pad * 0.3f, waist.Ry * flare + pad * 0.3f);
			}
		}

		private static void Pauldron(Kit k, MeshPart p, MeshModel body)
		{
			Bounds(p, out var lo, out var _);
			float len = Math.Max(0.05f, p.Pivot.Z - lo.Z);
			Ring ring = Fit(p, p.Pivot.Z - len * 0.2f, len * 0.1f);
			float side = ((Math.Abs(ring.X) > 0.001f) ? ((float)Math.Sign(ring.X)) : (p.Name.EndsWith("L", StringComparison.Ordinal) ? (-1f) : 1f));
			float r = Math.Max(ring.Rx, ring.Ry);
			float pad = 0.008f * k.U;
			float cx = ring.X + side * r * 0.18f;
			float cy = ring.Y;
			float num;
			switch (k.M)
			{
			case Metal.Leather:
				num = 1.2f;
				break;
			case Metal.Iron:
				num = 1.5f;
				break;
			case Metal.Steel:
				num = 1.42f;
				break;
			case Metal.Adamantine:
				num = 1.45f;
				break;
			case Metal.Copper:
				num = 1.42f;
				break;
			case Metal.Bronze:
				num = 1.42f;
				break;
			case Metal.Silver:
				num = 1.3f;
				break;
			default:
				num = 1.3f;
				break;
			}
			float big = num + 0.2f * k.Bulk;
			float bottom = p.Pivot.Z - len * 0.32f;
			float top = p.Pivot.Z + r * 0.95f;
			int sides = k.Sides(10);
			if (!k.Mid && k.M != Metal.Adamantine)
			{
				k.Loft(k.PlateSlot, Cap(r * 0.1f), sides);
				return;
			}
			switch (k.M)
			{
			case Metal.Leather:
				k.Loft(k.Main, Cap(0f), sides);
				k.Band(k.Shade, Lame(bottom + len * 0.03f, big), len * 0.04f, 0.002f * k.U, sides);
				k.Stud(k.Rivet, Lame(p.Pivot.Z, big), (side > 0f) ? 0f : MathF.PI, p.Pivot.Z, 0.009f * k.U);
				return;
			case Metal.Copper:
				k.Loft(k.Main, Cap(r * 0.1f), sides);
				Lames(3, k.Shade, k.Main);
				k.Stud(k.Rivet, Lame(p.Pivot.Z, big * 0.97f), (side > 0f) ? 0f : MathF.PI, p.Pivot.Z, 0.008f * k.U);
				return;
			case Metal.Bronze:
				k.Loft(k.Trim, Cap(r * 0.1f), sides);
				k.Band(k.Main, Lame(bottom + len * 0.02f, big), len * 0.04f, 0.004f * k.U, sides);
				Lames(1, k.Main, k.Trim);
				k.Band(k.TrimLight, Lame(p.Pivot.Z + r * 0.2f, big * 0.96f), r * 0.06f, 0.003f * k.U, sides);
				return;
			case Metal.Silver:
				Rerebrace(k.Shade);
				k.Loft(k.Main, Cap(r * 0.12f), sides);
				Wings(3, k.Light, k.Main);
				return;
			case Metal.Iron:
			{
				k.Loft(k.Main, Cap(r * 0.2f), sides);
				k.Band(k.Light, Lame(bottom + len * 0.02f, big), len * 0.035f, 0.004f * k.U, sides);
				Lames(1, k.Shade, k.Main);
				for (int i = -1; i <= 1; i++)
				{
					k.Stud(k.Rivet, Lame(bottom + len * 0.02f, big).Grow(1f, 0.004f * k.U), ((side > 0f) ? 0f : MathF.PI) + (float)i * 0.6f, bottom + len * 0.02f, 0.009f * k.U);
				}
				return;
			}
			case Metal.Steel:
				Rerebrace(k.Shade);
				k.Loft(k.Main, Cap(r * 0.18f), sides);
				Lames(3, k.Shade, k.Main);
				Haute(k.Light);
				k.Band(k.Light, Lame(p.Pivot.Z + r * 0.15f, big * 0.95f), r * 0.07f, 0.003f * k.U, sides);
				return;
			case Metal.Mythril:
				Rerebrace(k.Main);
				k.Loft(k.Main, Cap(r * 0.14f), sides);
				Wings(2, k.Main, k.Dark);
				k.Band(k.Accent, Lame(bottom + len * 0.01f, big), len * 0.015f, 0.003f * k.U, sides);
				k.Band(k.Light, Lame(bottom + len * 0.12f, big * 1.01f), len * 0.025f, 0.002f * k.U, sides);
				return;
			}
			Rerebrace(k.Dark);
			k.Loft(k.Main, Cap(r * 0.2f), k.Facets(6), smooth: false);
			k.Loft(k.Accent, new Ring[2]
			{
				Lame(bottom - len * 0.01f, big - 0.01f),
				Lame(bottom + len * 0.03f, big - 0.01f)
			}, k.Facets(6), smooth: false);
			for (int j = 0; j < 3; j++)
			{
				Vec3 vec = new Vec3(cx + side * r * 0.75f, cy + (float)(j - 1) * r * 0.5f, p.Pivot.Z + r * 0.45f);
				Vec3 tip = vec + new Vec3(side * r * 1f, (float)(j - 1) * r * 0.35f, r * ((j == 1) ? 2.4f : 1.6f));
				k.Spike(k.Dark, vec, tip, r * ((j == 1) ? 0.34f : 0.26f), 4);
			}
			if (k.Mid)
			{
				Ring r2 = Lame(p.Pivot.Z + r * 0.05f, big).Grow(1f, 0.002f * k.U);
				float num2 = ((side > 0f) ? 0f : MathF.PI);
				float[] array = new float[2] { -0.5f, 0.5f };
				foreach (float num3 in array)
				{
					k.OnRing(k.Accent, r2, num2 + num3, r2.Z, r * 0.36f, 0.006f * k.U, 0.004f * k.U);
					k.OnRing(k.AccentHot, r2, num2 + num3, r2.Z, r * 0.24f, 0.003f * k.U, 0.006f * k.U);
				}
			}
			Ring[] Cap(float lift)
			{
				float num4 = top + lift;
				float num5 = num4 - bottom;
				return new Ring[6]
				{
					Lame(bottom, big),
					Lame(bottom + num5 * 0.38f, big),
					Lame(bottom + num5 * 0.62f, big * 0.92f),
					Lame(bottom + num5 * 0.8f, big * 0.74f),
					Lame(bottom + num5 * 0.93f, big * 0.44f),
					Lame(num4, 0f)
				};
			}
			void Haute(int slot)
			{
				float num4 = ((side > 0f) ? MathF.PI : 0f);
				Ring ring2 = Lame(top - r * 0.35f, big * 0.82f);
				Ring ring3 = Lame(top + r * 0.45f, big * 0.86f);
				k.Plate(slot, k.Shade, new Ring[2] { ring2, ring3 }, num4 - 0.75f, num4 + 0.75f, 0.005f * k.U, k.Sides(5));
			}
			Ring Lame(float z, float s)
			{
				return new Ring(cx, cy, z, r * s + pad, r * s * 1.06f + pad);
			}
			void Lames(int n, int slotA, int slotB)
			{
				for (int m = 0; m < n; m++)
				{
					float num4 = bottom - len * (0.02f + (float)m * 0.1f);
					float z = num4 - len * 0.12f;
					k.Loft((m % 2 == 0) ? slotA : slotB, new Ring[2]
					{
						Lame(z, big + 0.06f + (float)m * 0.04f),
						Lame(num4 + len * 0.03f, big + 0.02f + (float)m * 0.04f)
					}, k.Sides(10));
				}
			}
			void Rerebrace(int slot)
			{
				Ring a = Fit(p, lo.Z + len * 0.1f, len * 0.08f).Grow(1.06f, 0.007f * k.U);
				Ring b = Fit(p, bottom, len * 0.08f).Grow(1.05f, 0.007f * k.U);
				k.Loft(slot, new Ring[3]
				{
					a.Up(lo.Z - len * 0.02f),
					Ring.Lerp(a, b, 0.5f),
					b.Up(bottom + len * 0.02f)
				}, k.Sides(8));
				if (k.Mid)
				{
					Ring ring2 = a.Up(lo.Z + len * 0.02f);
					k.Blob((slot == k.Shade) ? k.Main : k.Light, ring2.At(MathF.PI / 2f) + ring2.N(MathF.PI / 2f) * (0.004f * k.U), ring2.Rx * 0.75f, 0.012f * k.U, len * 0.11f, k.Sides(8));
				}
			}
			void Wings(int n, int slotA, int slotB)
			{
				float num4 = ((side > 0f) ? 0f : MathF.PI);
				for (int m = 0; m < n; m++)
				{
					float num5 = bottom + len * (0.06f - (float)m * 0.11f);
					Ring ring2 = Lame(num5 - len * 0.17f, big + 0.28f + (float)m * 0.12f).Shift(side * r * (0.25f + (float)m * 0.08f), 0f, 0f);
					Ring ring3 = Lame(num5, big + 0.02f + (float)m * 0.06f);
					k.Plate((m % 2 == 0) ? slotA : slotB, k.Shade, new Ring[2] { ring2, ring3 }, num4 - 1.35f, num4 + 1.35f, 0.006f * k.U, k.Sides(6));
				}
			}
		}

		private static void Vambrace(Kit k, MeshPart p)
		{
			Bounds(p, out var lo, out var _);
			float num = p.Pivot.Z - lo.Z;
			if (num <= 0.05f)
			{
				return;
			}
			float num2 = lo.Z + num * 0.3f;
			float num3 = float.MaxValue;
			for (int i = 0; i <= 10; i++)
			{
				float num4 = lo.Z + num * (0.12f + 0.4f * (float)i / 10f);
				Ring ring = Fit(p, num4, num * 0.04f);
				float num5 = ring.Rx + ring.Ry;
				if (num5 < num3 - 0.0001f)
				{
					num3 = num5;
					num2 = num4;
				}
			}
			float pad = 0.008f * k.U;
			Ring wrist = Fit(p, num2 + num * 0.04f, num * 0.05f).Grow(1.05f, pad);
			bool flag = k.M == Metal.Steel || k.M == Metal.Silver || k.M == Metal.Mythril || k.M == Metal.Adamantine;
			Ring elbow = Fit(p, p.Pivot.Z - num * (flag ? 0.02f : 0.12f), num * 0.06f).Grow(1.05f, pad);
			elbow.Rx = Math.Max(elbow.Rx, wrist.Rx);
			elbow.Ry = Math.Max(elbow.Ry, wrist.Ry);
			Bracer(k, wrist, elbow);
		}

		internal static void Bracer(Kit k, Ring wrist, Ring elbow)
		{
			float num = elbow.Z - wrist.Z;
			int sides = k.Sides(10);
			Ring[] array = new Ring[3]
			{
				At(0f),
				At(0.5f).Grow(1.03f, 0f),
				At(1f)
			};
			if (!k.Mid)
			{
				k.Loft(k.PlateSlot, array, sides, smooth: true, 0f, caps: false);
				return;
			}
			switch (k.M)
			{
			case Metal.Leather:
				k.Loft(k.Main, array, sides);
				k.Band(k.TrimDark, At(0.25f), num * 0.06f, 0.002f * k.U, sides);
				k.Band(k.TrimDark, At(0.72f), num * 0.06f, 0.002f * k.U, sides);
				k.Stud(k.Rivet, At(0.5f).Grow(1.03f, 0.001f * k.U), -MathF.PI / 2f, At(0.5f).Z, 0.008f * k.U);
				break;
			case Metal.Copper:
			{
				for (int i = 0; i < 3; i++)
				{
					k.Loft((i % 2 == 0) ? k.Main : k.Shade, new Ring[2]
					{
						At((float)i / 3f - 0.02f).Grow(1f + (float)i * 0.02f, 0f),
						At((float)(i + 1) / 3f + 0.02f).Grow(1.04f + (float)i * 0.02f, 0f)
					}, sides);
				}
				k.Stud(k.Rivet, At(0.15f).Grow(1.02f, 0.001f * k.U), -MathF.PI / 2f, At(0.15f).Z, 0.007f * k.U);
				break;
			}
			case Metal.Bronze:
				k.Loft(k.Trim, array, sides);
				k.Band(k.Main, At(0.03f), num * 0.05f, 0.002f * k.U, sides);
				k.Band(k.Main, At(0.97f), num * 0.05f, 0.002f * k.U, sides);
				k.Band(k.TrimLight, At(0.5f), num * 0.025f, 0.003f * k.U, sides);
				break;
			case Metal.Silver:
				k.Loft(k.Main, array, sides);
				Ridge(k, array, wrist.Z + num * 0.05f, elbow.Z - num * 0.05f, k.Light, 0.004f * k.U);
				k.Band(k.Shade, At(0.04f), num * 0.05f, 0.002f * k.U, sides);
				break;
			case Metal.Iron:
				k.Loft(k.Main, array, sides);
				k.Band(k.Light, At(0.04f), num * 0.04f, 0.002f * k.U, sides);
				k.Band(k.Light, At(0.96f), num * 0.04f, 0.002f * k.U, sides);
				k.Band(k.Trim, At(0.5f), num * 0.07f, 0.002f * k.U, sides);
				break;
			case Metal.Steel:
				k.Loft(k.Main, array, sides);
				Ridge(k, array, wrist.Z + num * 0.05f, elbow.Z - num * 0.05f, k.Light, 0.004f * k.U);
				k.Band(k.Shade, At(0.05f), num * 0.05f, 0.003f * k.U, sides);
				k.Band(k.Shade, At(0.92f), num * 0.06f, 0.003f * k.U, sides);
				break;
			case Metal.Mythril:
				k.Loft(k.Main, array, sides);
				if (k.Mid)
				{
					Scales(k, array, wrist.Z + num * 0.12f, elbow.Z - num * 0.06f, k.Light, 3, k.Fine ? 9 : 6);
				}
				Ridge(k, array, wrist.Z, elbow.Z, k.Accent, 0.0025f * k.U, 0.003f * k.U);
				k.Band(k.Dark, At(0.03f), num * 0.05f, 0.002f * k.U, sides);
				break;
			default:
			{
				k.Loft(k.Main, array, k.Facets(6), smooth: false);
				k.Loft(k.Dark, new Ring[2]
				{
					At(0f).Grow(1.06f, 0.001f * k.U),
					At(0.18f).Grow(1.06f, 0.001f * k.U)
				}, k.Facets(6), smooth: false);
				k.Loft(k.Dark, new Ring[2]
				{
					At(0.78f).Grow(1.06f, 0.001f * k.U),
					At(1f).Grow(1.06f, 0.001f * k.U)
				}, k.Facets(6), smooth: false);
				Ridge(k, array, wrist.Z + num * 0.2f, elbow.Z - num * 0.24f, k.Accent, 0.0025f * k.U, 0.003f * k.U);
				Ring ring = At(0.82f);
				Vec3 vec = ring.At(MathF.PI / 2f);
				k.Spike(k.Dark, vec - ring.N(MathF.PI / 2f) * (ring.Ry * 0.3f), vec + (ring.N(MathF.PI / 2f) * 1f + new Vec3(0f, 0f, 0.6f)).Normalized() * (num * 0.28f), ring.Ry * 0.35f, 4);
				break;
			}
			}
			Ring At(float t)
			{
				return Ring.Lerp(wrist, elbow, t);
			}
		}

		public static Metal MetalOf(string id)
		{
			id = id ?? "";
			if (id.Contains("adamantine"))
			{
				return Metal.Adamantine;
			}
			if (id.Contains("mythril"))
			{
				return Metal.Mythril;
			}
			if (id.Contains("steel"))
			{
				return Metal.Steel;
			}
			if (id.Contains("silver"))
			{
				return Metal.Silver;
			}
			if (id.Contains("bronze"))
			{
				return Metal.Bronze;
			}
			if (id.Contains("copper"))
			{
				return Metal.Copper;
			}
			if (id.Contains("leather"))
			{
				return Metal.Leather;
			}
			return Metal.Iron;
		}

		private static Shades[] MakePalette()
		{
			return new Shades[8]
			{
				new Shades
				{
					Main = C(6699576),
					Shade = C(4860456),
					Dark = C(3022880),
					Light = C(8473652),
					Trim = C(4860456),
					TrimDark = C(3022880),
					TrimLight = C(8473652),
					Rivet = C(11053224),
					Inside = C(1775897),
					Accent = C(8473652),
					AccentHot = C(8473652),
					Kind = 7,
					TrimKind = 7,
					AccentKind = 7,
					Bulk = 0f
				},
				new Shades
				{
					Main = C(13672036),
					Shade = C(9728063),
					Dark = C(7229486),
					Light = C(16164984),
					Trim = C(6699576),
					TrimDark = C(4860456),
					TrimLight = C(16230004),
					Rivet = C(16231036),
					Inside = C(2631720),
					Accent = C(16164984),
					AccentHot = C(16164984),
					TrimKind = 7,
					Bulk = 0.2f
				},
				new Shades
				{
					Main = C(7768642),
					Shade = C(5661231),
					Dark = C(3949602),
					Light = C(9347154),
					Trim = C(12620834),
					TrimDark = C(9728026),
					TrimLight = C(14463040),
					Rivet = C(15251004),
					Inside = C(1974802),
					Accent = C(14463040),
					AccentHot = C(15251004),
					Bulk = 0.3f
				},
				new Shades
				{
					Main = C(6860960),
					Shade = C(5148802),
					Dark = C(3828832),
					Light = C(9621698),
					Trim = C(10020566),
					TrimDark = C(5148802),
					TrimLight = C(10809054),
					Rivet = C(10809054),
					Inside = C(2046516),
					Accent = C(10020566),
					AccentHot = C(10020566),
					Bulk = 0.45f
				},
				new Shades
				{
					Main = C(5658458),
					Shade = C(4342597),
					Dark = C(2894894),
					Light = C(8421508),
					Trim = C(6699576),
					TrimDark = C(4860456),
					TrimLight = C(8473652),
					Rivet = C(11053228),
					Inside = C(1775897),
					Accent = C(8421508),
					AccentHot = C(8421508),
					TrimKind = 7,
					Bulk = 0.5f
				},
				new Shades
				{
					Main = C(12237518),
					Shade = C(9342380),
					Dark = C(7236744),
					Light = C(15263984),
					Trim = C(10122320),
					TrimDark = C(7624246),
					TrimLight = C(12096098),
					Rivet = C(15263984),
					Inside = C(4934237),
					Accent = C(15263984),
					AccentHot = C(15263984),
					Bulk = 0.75f
				},
				new Shades
				{
					Main = C(3957924),
					Shade = C(2904200),
					Dark = C(1980510),
					Light = C(2265284),
					Trim = C(2265284),
					TrimDark = C(1980510),
					TrimLight = C(3188956),
					Rivet = C(4248820),
					Inside = C(924206),
					Accent = C(3198450),
					AccentHot = C(9234170),
					Bulk = 0.85f
				},
				new Shades
				{
					Main = C(4999754),
					Shade = C(3683894),
					Dark = C(2302241),
					Light = C(7236716),
					Trim = C(2302241),
					TrimDark = C(1446932),
					TrimLight = C(9210506),
					Rivet = C(9210506),
					Inside = C(1315346),
					Accent = C(8791574),
					AccentHot = C(14178846),
					AccentKind = 14,
					Bulk = 1f
				}
			};
			uint C(int hex)
			{
				return TerrainCache.Rgb((hex >> 16) & 0xFF, (hex >> 8) & 0xFF, hex & 0xFF);
			}
		}

		internal static Shades ShadesOf(Metal m)
		{
			return Palette[(uint)m];
		}

		public static uint MaterialColor(string id)
		{
			return ShadesOf(MetalOf(id)).Main;
		}

		public static uint ShoeColor(string id)
		{
			Metal metal = MetalOf(id);
			if (metal != Metal.Iron)
			{
				return ShadesOf(metal).Main;
			}
			return ShadesOf(metal).Trim;
		}

		public static bool HidesHair(string helmetId)
		{
			if (helmetId != null)
			{
				return MetalOf(helmetId) != Metal.Mythril;
			}
			return false;
		}

		public static MeshModel For(MeshModel body, Metal metal)
		{
			if (body == null)
			{
				return null;
			}
			(MeshModel, Metal) key = (body, metal);
			lock (Cache)
			{
				if (Cache.TryGetValue(key, out var value))
				{
					return value;
				}
				if (Cache.Count > 160)
				{
					Cache.Clear();
					_letGo++;
				}
				value = Build(body, metal);
				Cache[key] = value;
				return value;
			}
		}

		public static ulong HiddenFor(MeshModel armor, bool helmet, bool chest, bool boots)
		{
			ulong num = 0uL;
			for (int i = 0; i < armor.Parts.Length && i < 64; i++)
			{
				bool num2;
				switch (armor.Parts[i].Name)
				{
				case "lower_leg_L":
				case "lower_leg_R":
				case "upper_leg_L":
				case "upper_leg_R":
					num2 = boots;
					goto IL_00b4;
				case "torso":
				case "hips":
				case "upper_arm_L":
				case "upper_arm_R":
				case "lower_arm_L":
				case "lower_arm_R":
					num2 = chest;
					goto IL_00b4;
				case "head":
					{
						num2 = helmet;
						goto IL_00b4;
					}
					IL_00b4:
					if (num2 && armor.Parts[i].VertexCount != 0)
					{
						continue;
					}
					break;
				}
				num |= (ulong)(1L << i);
			}
			return num;
		}

		public static int Dress(MeshInstance body, MeshModel fitTo, string helmet, string chest, string boots, MeshInstance[] into)
		{
			int num = 0;
			Metal metal = MetalOf(helmet);
			Metal metal2 = MetalOf(chest);
			Metal metal3 = MetalOf(boots);
			for (int i = 0; i < 3; i++)
			{
				if (num >= into.Length)
				{
					break;
				}
				Metal num2;
				switch (i)
				{
				default:
					num2 = metal3;
					break;
				case 1:
					num2 = metal2;
					break;
				case 0:
					num2 = metal;
					break;
				}
				Metal metal4 = num2;
				string text;
				switch (i)
				{
				default:
					text = boots;
					break;
				case 1:
					text = chest;
					break;
				case 0:
					text = helmet;
					break;
				}
				if (text != null && (i < 1 || helmet == null || metal != metal4) && (i != 2 || chest == null || metal2 != metal4))
				{
					MeshModel meshModel = For(fitTo ?? body.Model, metal4);
					if (meshModel != null && meshModel.Parts.Length == body.Model.Parts.Length)
					{
						MeshInstance obj = into[num] ?? (into[num] = new MeshInstance());
						obj.Model = meshModel;
						obj.Position = body.Position;
						obj.Basis = body.Basis;
						obj.PartRotation = body.PartRotation;
						obj.PartOffset = body.PartOffset;
						obj.PartScale = body.PartScale;
						obj.Palette = null;
						obj.Hidden = HiddenFor(meshModel, helmet != null && metal == metal4, chest != null && metal2 == metal4, boots != null && metal3 == metal4);
						num++;
					}
				}
			}
			return num;
		}

		private static bool Same(string a, string b)
		{
			if ((object)a != b)
			{
				if (a != null && b != null)
				{
					return a == b;
				}
				return false;
			}
			return true;
		}

		public static int Dress(MeshInstance body, MeshModel fitTo, string helmet, string chest, string boots, MeshInstance[] into, ref DressMemo memo)
		{
			int num = Volatile.Read(ref _letGo);
			if (memo.LetGo != num || memo.Into != into || memo.Body != body.Model || memo.Fit != fitTo || !Same(memo.Helmet, helmet) || !Same(memo.Chest, chest) || !Same(memo.Boots, boots))
			{
				int num2 = Dress(body, fitTo, helmet, chest, boots, into);
				memo.LetGo = ((Volatile.Read(ref _letGo) == num) ? num : (-1));
				memo.Into = into;
				memo.Body = body.Model;
				memo.Fit = fitTo;
				memo.Helmet = helmet;
				memo.Chest = chest;
				memo.Boots = boots;
				memo.Count = num2;
				for (int i = 0; i < num2; i++)
				{
					memo.Set(i, into[i].Model, into[i].Hidden);
				}
				return num2;
			}
			for (int j = 0; j < memo.Count; j++)
			{
				MeshInstance obj = into[j] ?? (into[j] = new MeshInstance());
				obj.Model = memo.Model(j);
				obj.Position = body.Position;
				obj.Basis = body.Basis;
				obj.PartRotation = body.PartRotation;
				obj.PartOffset = body.PartOffset;
				obj.PartScale = body.PartScale;
				obj.Palette = null;
				obj.Hidden = memo.Hidden(j);
			}
			if (_verifyDress)
			{
				VerifyDress(body, fitTo, helmet, chest, boots, ref memo);
			}
			return memo.Count;
		}

		public static void VerifyDresses(bool on)
		{
			_verifyDress = on;
			if (on)
			{
				_dressCompared = (_dressDiffer = 0);
				_dressFirst = null;
			}
		}

		public static string DressVerdict()
		{
			if (_dressDiffer > 0)
			{
				return $"{_dressDiffer} of {_dressCompared} dressings differ; first: {_dressFirst}";
			}
			if (_dressCompared != 0)
			{
				return null;
			}
			return "no dressing was compared";
		}

		private static void VerifyDress(MeshInstance body, MeshModel fitTo, string helmet, string chest, string boots, ref DressMemo memo)
		{
			_dressCompared++;
			int num = Dress(body, fitTo, helmet, chest, boots, VerifyInto);
			string text = ((num != memo.Count) ? $"{num} models the old way, {memo.Count} remembered" : null);
			int num2 = 0;
			while (text == null && num2 < num)
			{
				if (VerifyInto[num2].Model != memo.Model(num2))
				{
					text = $"model {num2}: {VerifyInto[num2].Model?.Name} the old way, {memo.Model(num2)?.Name} remembered";
				}
				else if (VerifyInto[num2].Hidden != memo.Hidden(num2))
				{
					text = $"model {num2} ({memo.Model(num2)?.Name}): hidden {VerifyInto[num2].Hidden:X} the old way, {memo.Hidden(num2):X} remembered";
				}
				num2++;
			}
			if (text != null)
			{
				_dressDiffer++;
				if (_dressFirst == null)
				{
					_dressFirst = body.Model?.Name + " in " + (helmet ?? "-") + ", " + (chest ?? "-") + ", " + (boots ?? "-") + ": " + text;
				}
			}
		}

		private static MeshModel Build(MeshModel body, Metal metal)
		{
			string text = body.Name ?? "";
			float detail = (text.EndsWith("_f", StringComparison.Ordinal) ? 0.5f : (text.EndsWith("_m", StringComparison.Ordinal) ? 0.75f : 1f));
			float num = Math.Max(0.5f, PersonRig.BodyHeight(body));
			bool worn = body.Info("zombie") == "1";
			Kit kit = new Kit(new MeshBuilder(text + "_armor_" + metal.ToString().ToLowerInvariant()), metal, detail, num / 1.8f, worn);
			uint num2 = (uint)(text.Length * 7919 + (int)metal * 104729);
			int num3 = body.PartIndex("hips");
			int num4 = body.PartIndex("lower_leg_L");
			if (num3 >= 0 && num4 >= 0 && body.Parts[num3].VertexCount > 0)
			{
				Bounds(body.Parts[num3], out var lo, out var _);
				kit.Dress = lo.Z < body.Parts[num4].Pivot.Z + 0.02f;
			}
			MeshPart meshPart = null;
			if (body.Info("robe") == "1")
			{
				float num5 = 0f;
				MeshPart[] parts = body.Parts;
				foreach (MeshPart meshPart2 in parts)
				{
					if (meshPart2.Name != null && meshPart2.Name.StartsWith("skirt_", StringComparison.Ordinal) && meshPart2.VertexCount >= 8)
					{
						Bounds(meshPart2, out var lo2, out var hi2);
						if (hi2.X - lo2.X > num5)
						{
							num5 = hi2.X - lo2.X;
							meshPart = meshPart2;
						}
					}
				}
				if (meshPart != null)
				{
					kit.Dress = true;
				}
			}
			HeadFit headFit = null;
			int num6 = body.PartIndex("head");
			if (num6 >= 0 && body.Parts[num6].VertexCount >= 8)
			{
				headFit = HeadFit.Of(body, body.Parts[num6]);
			}
			for (int j = 0; j < body.Parts.Length; j++)
			{
				MeshPart meshPart3 = body.Parts[j];
				kit.B.Part(meshPart3.Name, meshPart3.Parent, meshPart3.Pivot);
				if (meshPart3.VertexCount < 4)
				{
					continue;
				}
				switch (meshPart3.Name)
				{
				case "head":
					if (headFit != null)
					{
						Helmet(kit, headFit);
						WornHelmet(kit, headFit, num2);
					}
					break;
				case "torso":
					Cuirass(kit, body, meshPart3);
					WornChest(kit, meshPart3, num2 + 11);
					break;
				case "hips":
					Skirt(kit, body, meshPart ?? meshPart3);
					break;
				case "upper_arm_L":
				case "upper_arm_R":
					Pauldron(kit, meshPart3, body);
					WornPauldron(kit, meshPart3, num2 + 23);
					break;
				case "lower_arm_L":
				case "lower_arm_R":
					Vambrace(kit, meshPart3);
					break;
				case "lower_leg_L":
				case "lower_leg_R":
					Boot(kit, meshPart3);
					WornBoot(kit, meshPart3, num2 + 31);
					break;
				case "upper_leg_L":
				case "upper_leg_R":
					if (!kit.Dress)
					{
						Cuisse(kit, meshPart3);
					}
					break;
				}
			}
			return kit.B.Build();
		}

		internal static void Bounds(MeshPart p, out Vec3 lo, out Vec3 hi)
		{
			lo = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
			hi = new Vec3(float.MinValue, float.MinValue, float.MinValue);
			for (int i = 0; i < p.VertexCount; i++)
			{
				float val = p.Pos[i * 3];
				float val2 = p.Pos[i * 3 + 1];
				float val3 = p.Pos[i * 3 + 2];
				lo = new Vec3(Math.Min(lo.X, val), Math.Min(lo.Y, val2), Math.Min(lo.Z, val3));
				hi = new Vec3(Math.Max(hi.X, val), Math.Max(hi.Y, val2), Math.Max(hi.Z, val3));
			}
		}

		internal static void Slice(MeshPart p, int axis, float at, List<Vec3> into)
		{
			for (int i = 0; i < p.TriangleCount; i++)
			{
				for (int j = 0; j < 3; j++)
				{
					int num = p.Tri[i * 3 + j];
					int num2 = p.Tri[i * 3 + (j + 1) % 3];
					if (num > num2)
					{
						int num3 = num;
						num = num2;
						num2 = num3;
					}
					float num4 = p.Pos[num * 3 + axis];
					float num5 = p.Pos[num2 * 3 + axis];
					if (!((num4 - at) * (num5 - at) > 0f) && num4 != num5)
					{
						float num6 = (at - num4) / (num5 - num4);
						into.Add(new Vec3(p.Pos[num * 3] + (p.Pos[num2 * 3] - p.Pos[num * 3]) * num6, p.Pos[num * 3 + 1] + (p.Pos[num2 * 3 + 1] - p.Pos[num * 3 + 1]) * num6, p.Pos[num * 3 + 2] + (p.Pos[num2 * 3 + 2] - p.Pos[num * 3 + 2]) * num6));
					}
				}
			}
		}

		internal static Ring Fit(MeshPart p, float z, float band, float round = 0f)
		{
			List<Vec3> list = _slice ?? (_slice = new List<Vec3>());
			list.Clear();
			Slice(p, 2, z, list);
			Slice(p, 2, z - band * 0.5f, list);
			Slice(p, 2, z + band * 0.5f, list);
			if (list.Count < 3)
			{
				for (int i = 0; i < p.VertexCount; i++)
				{
					if (Math.Abs(p.Pos[i * 3 + 2] - z) <= band * 2f)
					{
						list.Add(new Vec3(p.Pos[i * 3], p.Pos[i * 3 + 1], z));
					}
				}
			}
			if (list.Count == 0)
			{
				return new Ring(p.Pivot.X, p.Pivot.Y, z, 0.05f, 0.05f);
			}
			float num = float.MaxValue;
			float num2 = float.MinValue;
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			foreach (Vec3 item in list)
			{
				if (item.X < num)
				{
					num = item.X;
				}
				if (item.X > num2)
				{
					num2 = item.X;
				}
				if (item.Y < num3)
				{
					num3 = item.Y;
				}
				if (item.Y > num4)
				{
					num4 = item.Y;
				}
			}
			float num5 = (num + num2) * 0.5f;
			float num6 = (num3 + num4) * 0.5f;
			float num7 = Math.Max(0.008f, (num2 - num) * 0.5f);
			float num8 = Math.Max(0.008f, (num4 - num3) * 0.5f);
			if (round > 0f)
			{
				if (num7 > num8 * round)
				{
					num7 = num8 * round;
				}
				if (num8 > num7 * round * 1.05f)
				{
					float num9 = num4;
					num8 = num7 * round * 1.05f;
					num6 = num9 - num8;
				}
			}
			float[] array = new float[list.Count];
			for (int j = 0; j < list.Count; j++)
			{
				float num10 = (list[j].X - num5) / num7;
				float num11 = (list[j].Y - num6) / num8;
				array[j] = (float)Math.Sqrt(num10 * num10 + num11 * num11);
			}
			Array.Sort(array);
			float num12 = Math.Max(1f, Math.Min(1.3f, array[Math.Min(array.Length - 1, (int)((float)array.Length * 0.9f))]));
			return new Ring(num5, num6, z, num7 * num12, num8 * num12);
		}

		internal static float SlotHeight(MeshModel body, MeshPart p, string slot)
		{
			int num = body.SlotIndex(slot);
			if (num < 0 || p.Slot == null)
			{
				return float.NaN;
			}
			double num2 = 0.0;
			int num3 = 0;
			for (int i = 0; i < p.TriangleCount; i++)
			{
				if (p.Slot[i] == num)
				{
					for (int j = 0; j < 3; j++)
					{
						num2 += (double)p.Pos[p.Tri[i * 3 + j] * 3 + 2];
						num3++;
					}
				}
			}
			if (num3 <= 0)
			{
				return float.NaN;
			}
			return (float)(num2 / (double)num3);
		}

		private static void Helmet(Kit k, HeadFit f)
		{
			switch (k.M)
			{
			case Metal.Leather:
				LeatherCap(k, f);
				break;
			case Metal.Copper:
				Spangenhelm(k, f);
				break;
			case Metal.Bronze:
				Corinthian(k, f);
				break;
			case Metal.Silver:
				ElvenHelm(k, f);
				break;
			case Metal.Iron:
				Kettle(k, f);
				break;
			case Metal.Steel:
				VisoredSallet(k, f);
				break;
			case Metal.Mythril:
				CrystalCrown(k, f);
				break;
			default:
				DarkHelm(k, f);
				break;
			}
		}

		private static Ring[] Dome(HeadFit f, float z0, float pad, float lift, float[] cap = null, float grow = 1.03f)
		{
			cap = cap ?? new float[5] { 0.94f, 0.79f, 0.58f, 0.34f, 0f };
			float num = Math.Max(z0 + f.H * 0.12f, Math.Min(f.Brow + f.H * 0.25f, f.Top - f.H * 0.32f));
			List<Ring> list = new List<Ring>();
			Ring ring = default(Ring);
			for (int i = 0; i < 3; i++)
			{
				float z1 = z0 + (num - z0) * (float)i / 2f;
				Ring ring2 = f.At(z1).Grow(grow, pad);
				list.Add(ring2);
				ring = ring2;
			}
			float num2 = f.Top + pad + lift;
			for (int j = 0; j < cap.Length; j++)
			{
				float num3 = ((float)j + 1f) / (float)cap.Length;
				list.Add(ring.Up(num + (num2 - num) * num3).Size(ring.Rx * cap[j], ring.Ry * cap[j]));
			}
			return list.ToArray();
		}

		private static Ring Hang(HeadFit f, Ring from, float z, float pad, float scale = 1f)
		{
			Ring ring = f.At(z).Grow(1.02f, pad);
			Ring ring2 = from.Up(z).Size(from.Rx * scale, from.Ry * scale);
			return new Ring(ring2.X, ring2.Y, z, Math.Max(ring2.Rx, ring.Rx), Math.Max(ring2.Ry, ring.Ry));
		}

		private static void Meridian(Ring[] shell, float pad, out Vec3[] path, out Vec3[] outward, int skipFront = 0, int skipBack = 0)
		{
			List<Vec3> list = new List<Vec3>();
			List<Vec3> list2 = new List<Vec3>();
			int num = shell.Length - 1;
			float midZ = (shell[0].Z + shell[num].Z) * 0.5f;
			for (int i = skipFront; i < num; i++)
			{
				Vec3 vec = shell[i].At(-MathF.PI / 2f, pad);
				list.Add(vec);
				list2.Add(Out(vec, shell[i]));
			}
			list.Add(shell[num].C + new Vec3(0f, 0f, pad));
			list2.Add(new Vec3(0f, 0f, 1f));
			for (int num2 = num - 1; num2 >= skipBack; num2--)
			{
				Vec3 vec2 = shell[num2].At(MathF.PI / 2f, pad);
				list.Add(vec2);
				list2.Add(Out(vec2, shell[num2]));
			}
			path = list.ToArray();
			outward = list2.ToArray();
			Vec3 Out(Vec3 at, Ring r)
			{
				return new Vec3(at.X - r.X, at.Y - r.Y, (at.Z - midZ) * 0.8f).Normalized();
			}
		}

		private static void Coronal(Ring[] shell, float pad, out Vec3[] path, out Vec3[] outward, int skip = 0)
		{
			List<Vec3> list = new List<Vec3>();
			List<Vec3> list2 = new List<Vec3>();
			int num = shell.Length - 1;
			float midZ = (shell[0].Z + shell[num].Z) * 0.5f;
			for (int i = skip; i < num; i++)
			{
				Vec3 vec = shell[i].At(0f, pad);
				list.Add(vec);
				list2.Add(Out(vec, shell[i]));
			}
			list.Add(shell[num].C + new Vec3(0f, 0f, pad));
			list2.Add(new Vec3(0f, 0f, 1f));
			for (int num2 = num - 1; num2 >= skip; num2--)
			{
				Vec3 vec2 = shell[num2].At(MathF.PI, pad);
				list.Add(vec2);
				list2.Add(Out(vec2, shell[num2]));
			}
			path = list.ToArray();
			outward = list2.ToArray();
			Vec3 Out(Vec3 at, Ring r)
			{
				return new Vec3(at.X - r.X, at.Y - r.Y, (at.Z - midZ) * 0.8f).Normalized();
			}
		}

		private static float[] Heights(int n, float h)
		{
			float[] array = new float[n];
			for (int i = 0; i < n; i++)
			{
				array[i] = h;
			}
			return array;
		}

		private static Ring ShellAt(Ring[] shell, float z)
		{
			if (z <= shell[0].Z)
			{
				return shell[0].Up(z);
			}
			for (int i = 0; i + 1 < shell.Length; i++)
			{
				if (z <= shell[i + 1].Z)
				{
					return Ring.Lerp(shell[i], shell[i + 1], (z - shell[i].Z) / Math.Max(1E-05f, shell[i + 1].Z - shell[i].Z));
				}
			}
			return shell[shell.Length - 1].Up(z);
		}

		private static void LeatherCap(Kit k, HeadFit f)
		{
			float h = f.H;
			float pad = 0.011f * k.U;
			Ring[] array = Dome(f, f.Brow - h * 0.1f, pad, h * 0.04f);
			k.Loft(k.Main, array, k.Sides(12));
			Ring[] array2 = new Ring[array.Length - 1];
			for (int i = 0; i < array2.Length; i++)
			{
				array2[i] = array[i + 1].Grow(1f, 0.0025f * k.U);
			}
			k.Plate(k.Light, k.Shade, array2, -2.0707965f, MathF.PI * -121f / 355f, 0.002f * k.U);
			k.Plate(k.Light, k.Shade, array2, MathF.PI * 121f / 355f, 2.0707965f, 0.002f * k.U);
			Meridian(array, 0.002f * k.U, out var path, out var outward, 1, 1);
			k.Fin(k.Shade, path, outward, Heights(path.Length, 0.004f * k.U), 0.004f * k.U);
			if (k.Mid)
			{
				k.Blob(k.Shade, array[array.Length - 1].C + new Vec3(0f, 0f, 0.006f * k.U), 0.011f * k.U, 0.011f * k.U, 0.008f * k.U, k.Sides(6));
			}
			Ring at = ShellAt(array, f.Brow - h * 0.02f);
			k.Band(k.Shade, at, h * 0.09f, 0.006f * k.U, k.Sides(12));
			for (int j = -2; j <= 2; j++)
			{
				k.Stud(k.Rivet, at.Grow(1f, 0.006f * k.U), -MathF.PI / 2f + (float)j * 0.5f, at.Z, 0.009f * k.U);
			}
			Ring ring = ShellAt(array, f.Brow + h * 0.02f).Grow(1f, 0.003f * k.U);
			float num = f.Eye - h * 0.95f;
			Ring ring2 = Hang(f, ring, f.Eye - h * 0.45f, pad, 1.12f);
			Ring ring3 = Hang(f, ring, num, pad, 1.24f);
			Ring ring4 = Hang(f, ring, num - h * 0.12f, pad, 1.2f);
			float[] array3 = new float[2]
			{
				0f,
				MathF.PI
			};
			foreach (float num2 in array3)
			{
				float num3 = ((num2 == 0f) ? (-1f) : 1f);
				k.Plate(k.Light, k.Inside, new Ring[3] { ring3, ring2, ring }, num2 - 0.3f * num3, num2 + 1.05f * num3, 0.008f * k.U, k.Sides(6));
				k.Plate(k.Light, k.Inside, new Ring[2] { ring4, ring3 }, num2 + 0f * num3, num2 + 0.75f * num3, 0.008f * k.U, k.Sides(4));
				k.Plate(k.Shade, k.Inside, new Ring[2]
				{
					ring3.Grow(1f, 0.002f * k.U).Up(num + h * 0.04f),
					ring3.Grow(1f, 0.002f * k.U).Up(num + h * 0.1f)
				}, num2 - 0.3f * num3, num2 + 1.05f * num3, 0.003f * k.U, k.Sides(6));
				Vec3[] path2 = new Vec3[4]
				{
					ring4.At(num2 + 0.75f * num3, 0.002f * k.U),
					ring3.At(num2 + 1.05f * num3, 0.002f * k.U),
					ring2.At(num2 + 1.05f * num3, 0.002f * k.U),
					ring.At(num2 + 1.05f * num3, 0.002f * k.U)
				};
				Vec3[] outward2 = new Vec3[4]
				{
					ring4.N(num2 + 0.75f * num3),
					ring3.N(num2 + 1.05f * num3),
					ring2.N(num2 + 1.05f * num3),
					ring.N(num2 + 1.05f * num3)
				};
				k.Fin(k.Dark, path2, outward2, Heights(4, 0.004f * k.U), 0.005f * k.U);
				k.Stud(k.Rivet, Ring.Lerp(ring3, ring2, 0.4f).Grow(1f, 0.003f * k.U), num2 + 0.25f * num3, num + (ring2.Z - num) * 0.4f, 0.01f * k.U);
				k.Stud(k.Rivet, Ring.Lerp(ring3, ring2, 0.4f).Grow(1f, 0.003f * k.U), num2 + 0.75f * num3, num + (ring2.Z - num) * 0.4f, 0.01f * k.U);
			}
			Ring ring5 = Hang(f, ring, f.Eye - h * 0.7f, pad, 1.08f);
			k.Plate(k.Main, k.Inside, new Ring[2] { ring5, ring }, 0.72079635f, 2.4207964f, 0.008f * k.U, k.Sides(6));
		}

		private static void Spangenhelm(Kit k, HeadFit f)
		{
			float h = f.H;
			float pad = 0.012f * k.U;
			Ring[] array = Dome(f, f.Brow - h * 0.1f, pad, h * 0.5f, new float[7] { 1.05f, 1.02f, 0.9f, 0.7f, 0.45f, 0.22f, 0f });
			k.Loft(k.Main, array, k.Sides(14));
			Ring[] array2 = new Ring[array.Length - 1];
			for (int i = 0; i < array2.Length; i++)
			{
				array2[i] = array[i].Grow(1f, 0.003f * k.U);
			}
			for (int j = 0; j < 6; j++)
			{
				float num = -MathF.PI / 2f + (float)j * MathF.PI / 3f;
				k.Plate(k.Shade, k.Dark, array2, num - 0.08f, num + 0.08f, 0.002f * k.U, 2);
				for (int l = 1; l < array2.Length - 2; l++)
				{
					k.Stud(k.Rivet, array2[l], num, array2[l].Z, 0.008f * k.U);
				}
			}
			Vec3 c = array[array.Length - 1].C;
			k.Blob(k.Light, c + new Vec3(0f, 0f, 0.004f * k.U), 0.012f * k.U, 0.012f * k.U, 0.01f * k.U, k.Sides(6));
			k.Spike(k.Shade, c + new Vec3(0f, 0f, 0.008f * k.U), c + new Vec3(0f, 0f, h * 0.32f), 0.006f * k.U, 4);
			Ring at = ShellAt(array, array[0].Z + h * 0.06f);
			k.Band(k.Shade, at, h * 0.08f, 0.006f * k.U, k.Sides(14));
			for (int m = 0; m < 10; m++)
			{
				k.Stud(k.Rivet, at.Grow(1f, 0.006f * k.U), -MathF.PI / 2f + (float)m * MathF.PI / 5f, at.Z, 0.008f * k.U);
			}
			k.Band(k.Trim, ShellAt(array, array[0].Z - h * 0.03f).Grow(0.98f, 0f), h * 0.04f, 0.003f * k.U, k.Sides(14));
			Ring r = f.At(f.Eye).Grow(1.03f, pad);
			float num2 = f.Eye - h * 0.5f;
			float z = at.Z;
			k.OnRing(k.Shade, r, -MathF.PI / 2f, (num2 + z) * 0.5f, 0.012f * k.U, (z - num2) * 0.5f, 0.006f * k.U);
			k.OnRing(k.Shade, r, -MathF.PI / 2f, num2 + h * 0.04f, 0.018f * k.U, h * 0.04f, 0.007f * k.U);
		}

		private static void Corinthian(Kit k, HeadFit f)
		{
			float h = f.H;
			float pad = 0.013f * k.U;
			float num = (f.Snout ? (f.Eye - h * 0.28f) : (f.Chin - h * 0.05f));
			List<Ring> list = new List<Ring>();
			Ring ring = f.At(f.Eye).Grow(1.04f, pad);
			list.Add(Hang(f, ring, num, pad, 1.04f));
			list.Add(Hang(f, ring, (num + f.Eye) * 0.5f, pad));
			Ring[] collection = Dome(f, f.Eye, pad, h * 0.06f);
			list.AddRange(collection);
			Ring[] array = list.ToArray();
			k.Loft(k.Main, array, k.Sides(14));
			Ring ring2 = ShellAt(array, f.Eye - h * 0.25f);
			Ring ring3 = Hang(f, ring2, num - h * 0.08f, pad, 1.12f).Shift(0f, ring2.Ry * 0.06f, 0f);
			k.Plate(k.Main, k.Shade, new Ring[2] { ring3, ring2 }, 0.37079632f, 2.7707963f, 0.005f * k.U, k.Sides(6));
			k.Plate(k.Trim, k.TrimDark, new Ring[2]
			{
				ring3.Grow(1f, 0.002f * k.U),
				ring3.Grow(1f, 0.002f * k.U).Up(ring3.Z + h * 0.07f)
			}, 0.37079632f, 2.7707963f, 0.003f * k.U, k.Sides(6));
			Ring ring4 = ShellAt(array, f.Eye + h * 0.08f).Grow(1f, 0.004f * k.U);
			Ring ring5 = ShellAt(array, f.Eye + h * 0.16f).Grow(1f, 0.011f * k.U);
			Ring ring6 = ShellAt(array, f.Eye + h * 0.28f).Grow(1f, 0.004f * k.U);
			k.Plate(k.Trim, k.TrimDark, new Ring[3] { ring4, ring5, ring6 }, -2.7707963f, -0.37079632f, 0.006f * k.U);
			k.Band(k.Trim, array[0].Up(num + h * 0.04f), h * 0.045f, 0.005f * k.U, k.Sides(14));
			Ring r = ShellAt(array, f.Eye);
			float[] array2 = new float[2] { -1f, 1f };
			foreach (float num2 in array2)
			{
				k.OnRing(k.Inside, r, -MathF.PI / 2f + num2 * 0.42f, f.Eye, 0.034f * k.U, 0.017f * k.U, 0.004f * k.U);
			}
			DeadEyes(k, r, f.Eye, 0.42f, 0.013f * k.U, 0.008f * k.U, 0.004f * k.U);
			if (!f.Snout)
			{
				float num3 = num + h * 0.06f;
				float num4 = f.Eye - h * 0.32f;
				Ring r2 = ShellAt(array, (num3 + num4) * 0.5f);
				k.OnRing(k.Inside, r2, -MathF.PI / 2f, (num3 + num4) * 0.5f, 0.012f * k.U, (num4 - num3) * 0.5f, 0.004f * k.U);
				array2 = new float[2] { -1f, 1f };
				foreach (float num5 in array2)
				{
					k.OnRing(k.TrimLight, r2, -MathF.PI / 2f + num5 * 0.2f, (num3 + num4) * 0.5f, 0.005f * k.U, (num4 - num3) * 0.5f, 0.006f * k.U);
				}
			}
			k.OnRing(k.TrimLight, r, -MathF.PI / 2f, f.Eye - h * 0.08f, 0.011f * k.U, h * 0.24f, 0.007f * k.U);
			Meridian(array, 0f, out var path, out var outward, 3, 2);
			float[] array3 = new float[path.Length];
			for (int j = 0; j < array3.Length; j++)
			{
				float num6 = (float)j / ((float)array3.Length - 1f);
				array3[j] = (0.025f + 0.07f * (float)Math.Sin(Math.Min(1f, num6 * 1.15f) * MathF.PI)) * k.U;
			}
			k.Fin(k.Trim, path, outward, array3, 0.011f * k.U);
			k.Fin(k.TrimDark, path, outward, Heights(path.Length, 0.008f * k.U), 0.016f * k.U);
		}

		private static void ElvenHelm(Kit k, HeadFit f)
		{
			float h = f.H;
			float pad = 0.013f * k.U;
			Ring[] array = Dome(f, f.Brow - h * 0.12f, pad, h * 0.08f);
			k.Loft(k.Main, array, k.Sides(14));
			Meridian(array, 0f, out var path, out var outward, 1, 1);
			float[] array2 = new float[path.Length];
			for (int i = 0; i < array2.Length; i++)
			{
				float num = (float)i / ((float)array2.Length - 1f);
				array2[i] = (0.008f + 0.035f * (float)Math.Sin(num * MathF.PI) * (0.6f + 0.4f * num)) * k.U;
			}
			k.Fin(k.Light, path, outward, array2, 0.006f * k.U);
			Ring r = array[0];
			k.Band(k.Shade, r.Up(r.Z + h * 0.03f), h * 0.05f, 0.005f * k.U, k.Sides(14));
			k.OnRing(k.Light, r, -MathF.PI / 2f, r.Z - h * 0.05f, 0.012f * k.U, h * 0.08f, 0.005f * k.U);
			Ring ring = ShellAt(array, r.Z + h * 0.04f);
			Ring ring2 = Hang(f, ring, f.Eye - h * 0.55f, pad, 1.02f);
			Ring ring3 = Hang(f, ring, f.Eye - h * 1.15f, pad, 0.98f).Shift(0f, (0f - ring.Ry) * 0.18f, 0f);
			Ring ring4 = Hang(f, ring, f.Eye - h * 1.5f, pad, 0.95f).Shift(0f, (0f - ring.Ry) * 0.32f, 0f);
			float[] array3 = new float[2] { -1f, 1f };
			foreach (float num2 in array3)
			{
				k.Plate(k.Main, k.Dark, new Ring[2] { ring2, ring }, -MathF.PI / 2f + num2 * 0.5f, -MathF.PI / 2f + num2 * 1.6f, 0.006f * k.U, k.Sides(5));
				k.Plate(k.Main, k.Dark, new Ring[2] { ring3, ring2 }, -MathF.PI / 2f + num2 * 0.62f, -MathF.PI / 2f + num2 * 1.4f, 0.006f * k.U, k.Sides(4));
				k.Plate(k.Main, k.Dark, new Ring[2] { ring4, ring3 }, -MathF.PI / 2f + num2 * 0.82f, -MathF.PI / 2f + num2 * 1.08f, 0.006f * k.U, 2);
				Vec3[] path2 = new Vec3[4]
				{
					ring4.At(-MathF.PI / 2f + num2 * 0.85f, 0.001f * k.U),
					ring3.At(-MathF.PI / 2f + num2 * 0.64f, 0.001f * k.U),
					ring2.At(-MathF.PI / 2f + num2 * 0.52f, 0.001f * k.U),
					ring.At(-MathF.PI / 2f + num2 * 0.5f, 0.001f * k.U)
				};
				Vec3[] outward2 = new Vec3[4]
				{
					ring4.N(-MathF.PI / 2f + num2 * 0.85f),
					ring3.N(-MathF.PI / 2f + num2 * 0.64f),
					ring2.N(-MathF.PI / 2f + num2 * 0.52f),
					ring.N(-MathF.PI / 2f + num2 * 0.5f)
				};
				k.Fin(k.Light, path2, outward2, Heights(4, 0.003f * k.U), 0.004f * k.U);
			}
			Ring ring5 = ShellAt(array, r.Z + h * 0.1f);
			Ring ring6 = Hang(f, ring5, f.Eye - h * 0.75f, pad, 1.14f).Shift(0f, ring5.Ry * 0.1f, 0f);
			k.Plate(k.Main, k.Dark, new Ring[2] { ring6, ring5 }, 0.4207964f, 2.7207963f, 0.006f * k.U);
			k.Plate(k.Light, k.Dark, new Ring[2]
			{
				ring6.Grow(1f, 0.002f * k.U),
				ring6.Grow(1f, 0.002f * k.U).Up(ring6.Z + h * 0.07f)
			}, 0.4207964f, 2.7207963f, 0.003f * k.U);
		}

		private static void Kettle(Kit k, HeadFit f)
		{
			float h = f.H;
			float pad = 0.018f * k.U;
			Ring[] array = Dome(f, f.Eye + h * 0.02f, pad, h * 0.1f, new float[5] { 0.98f, 0.9f, 0.74f, 0.48f, 0f });
			k.Loft(k.Main, array, k.Sides(14));
			Ring ring = array[0];
			k.Loft(k.Dark, new Ring[4]
			{
				ring.Grow(1f, 0.002f * k.U).Up(ring.Z - h * 0.03f),
				ring.Grow(1f, 0.01f * k.U).Up(ring.Z - h * 0.02f),
				ring.Grow(1f, 0.01f * k.U).Up(ring.Z + h * 0.02f),
				ring.Grow(1f, 0.002f * k.U).Up(ring.Z + h * 0.06f)
			}, k.Sides(14));
			Meridian(array, 0f, out var path, out var outward, 1, 1);
			k.Fin(k.Light, path, outward, Heights(path.Length, 0.006f * k.U), 0.012f * k.U);
			Coronal(array, 0f, out var path2, out var outward2, 1);
			k.Fin(k.Light, path2, outward2, Heights(path2.Length, 0.006f * k.U), 0.012f * k.U);
			if (k.Fine)
			{
				(Vec3[], Vec3[])[] array2 = new(Vec3[], Vec3[])[2]
				{
					(path, outward),
					(path2, outward2)
				};
				for (int i = 0; i < array2.Length; i++)
				{
					(Vec3[], Vec3[]) tuple = array2[i];
					Vec3[] item = tuple.Item1;
					Vec3[] item2 = tuple.Item2;
					for (int j = 1; j < item.Length - 1; j += 2)
					{
						k.Box(k.Rivet, item[j] + item2[j] * (0.008f * k.U), new Vec3(0.005f * k.U, 0f, 0f), new Vec3(0f, 0.005f * k.U, 0f), new Vec3(0f, 0f, 0.005f * k.U));
					}
				}
			}
			k.Blob(k.Light, array[array.Length - 1].C + new Vec3(0f, 0f, 0.006f * k.U), 0.014f * k.U, 0.014f * k.U, 0.009f * k.U, k.Sides(6));
			if (f.Snout || !k.Mid)
			{
				return;
			}
			float num = 0.0065f * k.U;
			float[] array3 = new float[2]
			{
				0f,
				MathF.PI
			};
			foreach (float num2 in array3)
			{
				float num3 = num2 + ((num2 == 0f) ? (-0.22f) : 0.22f);
				Vec3 a = ring.At(num3, 0.004f * k.U);
				Vec3 vec = Hang(f, ring, f.Eye - h * 0.85f, 0.008f * k.U).At(num3 + ((num2 == 0f) ? (-0.1f) : 0.1f));
				k.Tube(k.TrimLight, a, vec, num, num, k.Sides(5));
				if (num2 == 0f)
				{
					k.Box(k.Rivet, vec + new Vec3(0f, 0f, 0.004f * k.U), new Vec3(0.008f * k.U, 0f, 0f), new Vec3(0f, 0.004f * k.U, 0f), new Vec3(0f, 0f, 0.007f * k.U));
				}
			}
		}

		private static void VisoredSallet(Kit k, HeadFit f)
		{
			float h = f.H;
			float pad = 0.017f * k.U;
			float num = (f.Snout ? (f.Eye - h * 0.32f) : (f.Chin - h * 0.12f));
			Ring[] array = Dome(f, f.Eye + h * 0.06f, pad, h * 0.08f, new float[5] { 0.99f, 0.93f, 0.78f, 0.5f, 0f });
			Ring ring = array[0];
			array[0] = new Ring(ring.X, ring.Y - ring.Ry * 0.12f, ring.Z, ring.Rx * 1.08f, ring.Ry * 1.14f);
			k.Loft(k.Main, array, k.Sides(14));
			k.Band(k.Light, array[0].Up(array[0].Z + h * 0.015f), h * 0.03f, 0.004f * k.U, k.Sides(14));
			Meridian(array, 0f, out var path, out var outward, 1, 1);
			k.Fin(k.Light, path, outward, Heights(path.Length, 0.012f * k.U), 0.004f * k.U);
			Ring ring2 = f.At(f.Eye).Grow(1.04f, pad);
			k.Loft(k.Inside, new Ring[2]
			{
				ring2.Up(f.Eye - h * 0.06f),
				ring2.Up(array[0].Z + h * 0.01f)
			}, k.Sides(14), smooth: true, 0f, caps: false);
			DeadEyes(k, ring2, f.Eye + h * 0.01f, 0.3f, 0.014f * k.U, 0.007f * k.U, 0.0025f * k.U);
			Ring[] array2 = new List<Ring>
			{
				Hang(f, ring2, num, pad, 0.92f),
				Hang(f, ring2, (num + f.Eye) * 0.5f, pad),
				ring2.Grow(1.03f, 0.002f * k.U).Up(f.Eye - h * 0.04f)
			}.ToArray();
			k.Loft(k.Main, array2, k.Sides(14));
			List<Vec3> list = new List<Vec3>();
			List<Vec3> list2 = new List<Vec3>();
			float[] array3 = new float[4] { 0.05f, 0.35f, 0.7f, 0.98f };
			foreach (float num2 in array3)
			{
				Ring ring3 = ShellAt(array2, num + (f.Eye - h * 0.04f - num) * num2);
				list.Add(ring3.At(-MathF.PI / 2f));
				list2.Add(ring3.N(-MathF.PI / 2f));
			}
			k.Fin(k.Light, list.ToArray(), list2.ToArray(), Heights(list.Count, 0.008f * k.U), 0.005f * k.U);
			if (k.Fine && !f.Snout)
			{
				for (int j = 0; j < 3; j++)
				{
					float z = f.Eye - h * (0.35f + (float)j * 0.17f);
					Ring r = ShellAt(array2, z);
					for (int l = 0; l < 2; l++)
					{
						k.OnRing(k.Inside, r, -1.1507964f + (float)l * 0.16f, z, 0.006f * k.U, 0.006f * k.U, 0.002f * k.U);
					}
				}
			}
			Ring ring4 = ShellAt(array, array[0].Z + h * 0.05f);
			Ring ring5 = Hang(f, ring4, f.Eye - h * 0.7f, pad, 1.16f).Shift(0f, ring4.Ry * 0.14f, 0f);
			k.Plate(k.Main, k.Shade, new Ring[2] { ring5, ring4 }, 0.32079637f, 2.8207965f, 0.006f * k.U);
			array3 = new float[2]
			{
				0f,
				MathF.PI
			};
			foreach (float num3 in array3)
			{
				for (int m = 0; m < 3; m++)
				{
					float num4 = f.Eye - h * (0.2f + (float)m * 0.25f);
					if (num4 < num + h * 0.05f)
					{
						break;
					}
					k.OnRing(k.Trim, ShellAt(array2, num4), num3 + ((num3 == 0f) ? 0.35f : (-0.35f)), num4, 0.014f * k.U, 0.008f * k.U, 0.004f * k.U);
				}
			}
		}

		private static void CrystalCrown(Kit k, HeadFit f)
		{
			float h = f.H;
			float pad = 0.012f * k.U;
			Ring ring = f.At(f.Brow + h * 0.05f).Grow(1.04f, pad);
			k.Loft(k.Main, new Ring[3]
			{
				ring.Up(ring.Z - h * 0.1f),
				ring.Grow(1f, 0.004f * k.U).Up(ring.Z),
				ring.Up(ring.Z + h * 0.1f)
			}, k.Sides(14), smooth: true, 0f, caps: false);
			k.Band(k.Accent, ring.Up(ring.Z + h * 0.1f), h * 0.016f, 0.003f * k.U, k.Sides(14));
			k.Band(k.Dark, ring.Up(ring.Z - h * 0.1f), h * 0.016f, 0.003f * k.U, k.Sides(14));
			float[] array = ((!k.Mid) ? new float[5] { 0f, -0.8f, 0.8f, -1.8f, 1.8f } : new float[7] { 0f, -0.72f, 0.72f, -1.5f, 1.5f, -2.35f, 2.35f });
			float[] array2 = ((!k.Mid) ? new float[5] { 1.55f, 1.1f, 1.1f, 0.7f, 0.7f } : new float[7] { 1.55f, 1.2f, 1.2f, 0.85f, 0.85f, 0.55f, 0.55f });
			Ring ring2 = ring.Grow(1f, 0.002f * k.U);
			for (int i = 0; i < array.Length; i++)
			{
				float th = -MathF.PI / 2f + array[i];
				Vec3 vec = ring2.N(th);
				Vec3 vec2 = ring2.T(th);
				Vec3 vec3 = ring2.Up(ring.Z + h * 0.02f).At(th);
				Vec3 vec4 = (vec * 0.34f + new Vec3(0f, 0f, 1f)).Normalized();
				float num = h * array2[i];
				float num2 = (0.022f + 0.008f * array2[i]) * k.U;
				float num3 = 0.008f * k.U;
				Vec3 ax = vec2;
				Vec3 vec5 = Vec3.Cross(vec4, vec2).Normalized();
				Vec3[] centres = new Vec3[4]
				{
					vec3,
					vec3 + vec4 * (num * 0.3f),
					vec3 + vec4 * (num * 0.72f),
					vec3 + vec4 * num
				};
				float[] rx = new float[4]
				{
					num2 * 0.75f,
					num2,
					num2 * 0.62f,
					0f
				};
				float[] ry = new float[4]
				{
					num3,
					num3,
					num3 * 0.7f,
					0f
				};
				k.LoftAlong(k.Main, centres, rx, ry, ax, vec5, 4, smooth: false);
				if (k.Mid)
				{
					Vec3 vec6 = vec5 * ((0f - num3) * 0.75f);
					Vec3[] centres2 = new Vec3[4]
					{
						vec3 + vec6,
						vec3 + vec4 * (num * 0.3f) + vec6,
						vec3 + vec4 * (num * 0.72f) + vec6 * 0.8f,
						vec3 + vec4 * (num * 0.98f)
					};
					k.LoftAlong(k.Accent, centres2, new float[4]
					{
						num2 * 0.2f,
						num2 * 0.26f,
						num2 * 0.16f,
						0f
					}, new float[4]
					{
						num3 * 0.45f,
						num3 * 0.45f,
						num3 * 0.35f,
						0f
					}, ax, vec5, 4, smooth: false);
				}
			}
			Vec3 vec7 = ring2.N(-MathF.PI / 2f);
			k.Blob(k.AccentHot, ring2.At(-MathF.PI / 2f) + vec7 * (0.006f * k.U), 0.011f * k.U, 0.007f * k.U, 0.013f * k.U, k.Sides(6));
		}

		private static void DarkHelm(Kit k, HeadFit f)
		{
			float h = f.H;
			float pad = 0.016f * k.U;
			float num = (f.Snout ? (f.Eye - h * 0.3f) : (f.Chin - h * 0.12f));
			Ring ring = f.At(f.Eye).Grow(1.05f, pad);
			Ring ring2 = Hang(f, ring, num, pad, 0.9f);
			ring2 = new Ring(ring2.X, ring2.Y - ring2.Ry * 0.16f, ring2.Z, ring2.Rx * 0.8f, ring2.Ry);
			List<Ring> list = new List<Ring>
			{
				ring2,
				Hang(f, ring, (num + f.Eye) * 0.5f, pad, 0.98f),
				ring
			};
			list.AddRange(Dome(f, f.Brow, pad, h * 0.14f, new float[5] { 0.95f, 0.8f, 0.56f, 0.27f, 0f }));
			Ring[] array = list.ToArray();
			k.Loft(k.Main, array, k.Facets(8), smooth: false);
			float[] array2 = new float[2] { -1f, 1f };
			foreach (float num2 in array2)
			{
				Ring ring3 = ShellAt(array, num + h * 0.1f).Grow(1f, 0.004f * k.U);
				Ring ring4 = ShellAt(array, f.Eye - h * 0.1f).Grow(1f, 0.004f * k.U);
				k.Plate(k.Light, k.Dark, new Ring[2] { ring3, ring4 }, -MathF.PI / 2f + num2 * 0.55f, -MathF.PI / 2f + num2 * 1.25f, 0.004f * k.U, 2);
			}
			Meridian(array, 0f, out var path, out var outward, 3, 2);
			List<Vec3> list2 = new List<Vec3>(path);
			List<Vec3> list3 = new List<Vec3>(outward);
			Vec3 vec = path[path.Length - 1];
			Vec3 vec2 = new Vec3(0f, 1f, -0.25f).Normalized();
			list2.Add(vec + vec2 * (h * 0.35f) + new Vec3(0f, 0f, h * 0.08f));
			list3.Add(new Vec3(0f, 0.4f, 1f).Normalized());
			float[] array3 = new float[list2.Count];
			for (int j = 0; j < array3.Length; j++)
			{
				float num3 = (float)j / ((float)array3.Length - 1f);
				array3[j] = (0.012f + 0.085f * (float)Math.Pow(Math.Sin(Math.Min(1f, num3 * 1.2f) * MathF.PI * 0.5f), 2.0) * (1f - Math.Max(0f, num3 - 0.85f) * 5f)) * k.U;
			}
			k.Fin(k.Dark, list2.ToArray(), list3.ToArray(), array3, 0.009f * k.U);
			Ring ring5 = ShellAt(array, f.Eye + h * 0.3f);
			array2 = new float[2]
			{
				0f,
				MathF.PI
			};
			foreach (float num4 in array2)
			{
				Vec3 vec3 = ring5.N(num4);
				Vec3 vec4 = ring5.At(num4 + ((num4 == 0f) ? (-0.35f) : 0.35f));
				Vec3 tip = vec4 + (vec3 * 0.4f + new Vec3(0f, 1f, 0.6f)).Normalized() * (h * 1.15f);
				k.Spike(k.Dark, vec4 - vec3 * (0.006f * k.U), tip, 0.022f * k.U, 4);
			}
			Ring ring6 = ShellAt(array, f.Eye + h * 0.02f);
			array2 = new float[2] { -1f, 1f };
			foreach (float num5 in array2)
			{
				float th = -MathF.PI / 2f + num5 * 0.36f;
				Vec3 vec5 = ring6.N(th);
				Vec3 vec6 = Vec3.Cross(new Vec3(0f, 0f, 1f), vec5);
				Vec3 vec7 = new Vec3(0f, 0f, 1f);
				float num6 = num5 * -0.32f;
				Vec3 vec8 = vec6 * (float)Math.Cos(num6) + vec7 * (float)Math.Sin(num6);
				Vec3 vec9 = Vec3.Cross(vec5, vec8);
				k.Box(k.Accent, ring6.At(th) + vec5 * (0.002f * k.U), vec8 * (0.024f * k.U), vec9 * (0.0075f * k.U), vec5 * (0.003f * k.U));
				k.Box(k.AccentHot, ring6.At(th) + vec5 * (0.0045f * k.U), vec8 * (0.018f * k.U), vec9 * (0.0035f * k.U), vec5 * (0.002f * k.U));
			}
			if (f.Snout)
			{
				return;
			}
			for (int l = 0; l < 3; l++)
			{
				float num7 = f.Eye - h * (0.42f + (float)l * 0.17f);
				if (!(num7 < num + h * 0.06f))
				{
					k.OnRing((l == 0) ? k.AccentHot : k.Accent, ShellAt(array, num7), -MathF.PI / 2f, num7, (0.018f - (float)l * 0.004f) * k.U, 0.0035f * k.U, 0.004f * k.U);
					continue;
				}
				break;
			}
		}

		public static void FirstPersonBracer(MeshBuilder b, string armorId, bool woman)
		{
			Kit k = new Kit(b, MetalOf(armorId), 1f, 0.7f)
			{
				Origin = new Vec3(0f, 0.17f, 0f),
				Rot = new Mat3(new Vec3(-1f, 0f, 0f), new Vec3(0f, 0f, -1f), new Vec3(0f, -1f, 0f))
			};
			Ring wrist = (woman ? new Ring(0f, 0f, -0.012f, 0.0272f, 0.029f) : new Ring(0f, 0f, -0.012f, 0.0302f, 0.0322f));
			Ring elbow = (woman ? new Ring(0f, 0f, 0.24f, 0.0318f, 0.0342f) : new Ring(0f, 0f, 0.24f, 0.0342f, 0.0372f));
			Bracer(k, wrist, elbow);
		}

		private static uint Hex(int hex)
		{
			return TerrainCache.Rgb((hex >> 16) & 0xFF, (hex >> 8) & 0xFF, hex & 0xFF);
		}

		private static Shades WornShades(Metal m)
		{
			Shades shades = ShadesOf(m);
			return new Shades
			{
				Main = Soil(shades.Main, 0.22f, 0.9f),
				Shade = Soil(shades.Shade, 0.22f, 0.9f),
				Dark = Soil(shades.Dark, 0.15f, 0.92f),
				Light = Soil(TerrainCache.Lerp(shades.Light, shades.Main, 0.35f), 0.22f, 0.9f),
				Trim = Soil(shades.Trim, 0.2f, 0.9f),
				TrimDark = Soil(shades.TrimDark, 0.15f, 0.92f),
				TrimLight = Soil(TerrainCache.Lerp(shades.TrimLight, shades.Trim, 0.35f), 0.2f, 0.9f),
				Rivet = Soil(shades.Rivet, 0.3f, 0.85f),
				Inside = shades.Inside,
				Accent = Soil(shades.Accent, 0.2f, 0.9f),
				AccentHot = Soil(shades.AccentHot, 0.12f, 0.94f),
				Kind = shades.Kind,
				TrimKind = shades.TrimKind,
				AccentKind = shades.AccentKind,
				Bulk = shades.Bulk
			};
			uint Soil(uint c, float k, float dark)
			{
				return TerrainCache.Scale(TerrainCache.Lerp(c, WornSoil, k), dark);
			}
		}

		private static uint RotOf(Metal m)
		{
			switch (m)
			{
			case Metal.Iron:
			case Metal.Steel:
				return Hex(8142366);
			case Metal.Copper:
			case Metal.Bronze:
				return Hex(5147244);
			case Metal.Silver:
				return Hex(2900024);
			case Metal.Leather:
				return Hex(4081200);
			case Metal.Mythril:
				return Hex(2503754);
			default:
				return Hex(2760736);
			}
		}

		private static float Noise(uint seed, int i)
		{
			int num = ((int)seed * -1640531535) ^ ((i + 1) * -2048144777);
			int num2 = (int)((uint)num ^ ((uint)num >> 15)) * -1640531535;
			return (float)(((uint)num2 ^ ((uint)num2 >> 13)) & 0xFFFF) / 65535f;
		}

		private static void Blot(Kit k, int slot, Vec3 from, Vec3 dir, float r, float drip, uint seed)
		{
			if (!k.Mid || slot < 0)
			{
				return;
			}
			dir = dir.Normalized();
			if (k.B.CastOut(from, dir, out var hit, out var normal))
			{
				float num = 0.0016f * k.U;
				Vec3 vec = Vec3.Cross(new Vec3(0f, 0f, 1f), normal);
				vec = ((Vec3.Dot(vec, vec) > 1E-06f) ? vec.Normalized() : new Vec3(1f, 0f, 0f));
				Vec3 vec2 = Vec3.Cross(vec, normal).Normalized();
				if (vec2.Z > 0f)
				{
					vec2 *= -1f;
				}
				int num2 = (k.Fine ? 9 : 6);
				int num3 = (int)(Noise(seed, 99) * 3f);
				Vec3[] array = new Vec3[num2];
				for (int i = 0; i < num2; i++)
				{
					float num4 = MathF.PI * 2f * ((float)i + 0.3f * Noise(seed, i + 40)) / (float)num2;
					float num5 = r * (0.62f + 0.55f * Noise(seed, i));
					float num6 = Math.Max(0f, (float)Math.Sin(num4));
					float num7 = ((num6 > 0.5f && (i - num3) % 3 == 0) ? (drip * (0.6f + 0.6f * Noise(seed, i + 70))) : (drip * 0.25f * num6));
					Vec3 vec3 = hit + vec * ((float)Math.Cos(num4) * num5) + vec2 * ((float)Math.Sin(num4) * num5 + num7);
					Vec3 vec4 = default(Vec3);
					Vec3 hit2;
					Vec3 normal2;
					bool flag = k.B.CastOut(from, (vec3 - from).Normalized(), out hit2, out normal2) && Vec3.Dot(vec4 = hit2 - vec3, vec4) < r * r * 4f;
					array[i] = (flag ? (hit2 + normal2 * num) : (vec3 + normal * num));
				}
				Vec3 vec5 = hit + normal * num;
				for (int j = 0; j < num2; j++)
				{
					k.FaceOut(slot, normal, vec5, array[j], array[(j + 1) % num2]);
				}
			}
		}

		private static Vec3 Core(MeshPart p, float z)
		{
			return Fit(p, z, 0.04f).C;
		}

		private static void WornChest(Kit k, MeshPart p, uint seed)
		{
			Vec3 lo;
			float h;
			if (k.Worn && k.Mid)
			{
				Bounds(p, out lo, out var hi);
				h = hi.Z - lo.Z;
				Blot(k, k.Gore, At(0.74f), Way(-1.2507963f), 0.036f * k.U, 0.07f * k.U, seed);
				Blot(k, k.Gore, At(0.42f), Way(-2.1907964f), 0.022f * k.U, 0.035f * k.U, seed + 1);
				if (k.Fine)
				{
					Blot(k, k.Gore, At(0.6f), Way(1.9707963f), 0.026f * k.U, 0.03f * k.U, seed + 2);
				}
				Blot(k, k.Grime, At(0.1f), Way(-1.7207963f), 0.04f * k.U, 0f, seed + 3);
				Blot(k, k.Grime, At(0.12f), Way(MathF.PI * 121f / 355f), 0.036f * k.U, 0f, seed + 4);
				Blot(k, k.Decay, At(0.86f), Way(-2.6207962f), 0.012f * k.U, 0.08f * k.U, seed + 5);
				if (k.Fine)
				{
					Blot(k, k.Decay, At(0.82f), Way(-0.4207964f), 0.01f * k.U, 0.06f * k.U, seed + 6);
				}
			}
			Vec3 At(float f)
			{
				return Core(p, lo.Z + h * f);
			}
			Vec3 Way(float th, float tilt = 0f)
			{
				return new Vec3((float)Math.Cos(th), (float)Math.Sin(th), tilt);
			}
		}

		private static void WornHelmet(Kit k, HeadFit f, uint seed)
		{
			if (k.Worn && k.Mid)
			{
				Vec3 c = f.At(f.Brow).C;
				Blot(k, k.Gore, c, Way(-0.5707964f, 0.15f), 0.022f * k.U, 0.03f * k.U, seed);
				Blot(k, k.Grime, c, Way(0.97079635f, 0.3f), 0.026f * k.U, 0f, seed + 1);
				Blot(k, k.Decay, c, Way(-2.4707963f, 0.55f), 0.01f * k.U, 0.04f * k.U, seed + 2);
			}
			Vec3 Way(float th, float up)
			{
				return new Vec3((float)Math.Cos(th), (float)Math.Sin(th), up);
			}
		}

		private static void WornPauldron(Kit k, MeshPart p, uint seed)
		{
			if (k.Worn && k.Fine)
			{
				Vec3 vec = p.Pivot + new Vec3(0f, 0f, -0.03f * k.U);
				float num = (p.Name.EndsWith("_L", StringComparison.Ordinal) ? (-1f) : 1f);
				Blot(k, (num < 0f) ? k.Gore : k.Grime, vec, new Vec3(num, -0.35f, 0.75f), 0.02f * k.U, 0.025f * k.U, seed);
			}
		}

		private static void WornBoot(Kit k, MeshPart p, uint seed)
		{
			if (k.Worn && k.Mid)
			{
				for (int i = 0; i < (k.Fine ? 3 : 2); i++)
				{
					float num = -MathF.PI / 2f + (float)i * 2.1f + Noise(seed, i) * 0.6f;
					Blot(k, k.Grime, Core(p, 0.09f + 0.03f * (float)i), new Vec3((float)Math.Cos(num), (float)Math.Sin(num), -0.1f), 0.032f * k.U, 0f, seed + (uint)i);
				}
				if (p.Name.EndsWith("_R", StringComparison.Ordinal))
				{
					Blot(k, k.Gore, Core(p, 0.32f), new Vec3(0.3f, -1f, 0f), 0.016f * k.U, 0.03f * k.U, seed + 9);
				}
			}
		}

		private static void DeadEyes(Kit k, Ring r, float z, float spread, float w, float h, float lift)
		{
			if (k.Worn && k.Glow >= 0)
			{
				float[] array = new float[2] { -1f, 1f };
				foreach (float num in array)
				{
					k.OnRing(k.Glow, r, -MathF.PI / 2f + num * spread, z, w, h, 0.002f * k.U, lift);
				}
			}
		}
	}
}
