using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;

namespace FirstPerson.Core
{
	public static class Mat3Verify
	{
		[StructLayout(LayoutKind.Explicit)]
		private struct FloatBits
		{
			[FieldOffset(0)]
			public float F;

			[FieldOffset(0)]
			public uint U;
		}

		public static bool On;

		private static long _compared;

		private static long _differed;

		private static long _turns;

		private static long _muls;

		private static long _products;

		private static long _scaled;

		private static string _first;

		public static long Compared => Interlocked.Read(ref _compared);

		public static long Differed => Interlocked.Read(ref _differed);

		public static string First => _first;

		public static string Counts => Interlocked.Read(ref _turns) + " turns, " + Interlocked.Read(ref _products) + " products, " + Interlocked.Read(ref _muls) + " Mul, " + Interlocked.Read(ref _scaled) + " scaled";

		public static void Enable(bool on)
		{
			if (on)
			{
				Interlocked.Exchange(ref _compared, 0L);
				Interlocked.Exchange(ref _differed, 0L);
				Interlocked.Exchange(ref _turns, 0L);
				Interlocked.Exchange(ref _muls, 0L);
				Interlocked.Exchange(ref _products, 0L);
				Interlocked.Exchange(ref _scaled, 0L);
				_first = null;
			}
			On = on;
		}

		public static string Result()
		{
			long compared = Compared;
			long differed = Differed;
			if (compared == 0L)
			{
				return "nothing compared (no Mat3 product or turn ran with the verify mode on)";
			}
			if (differed == 0L)
			{
				return null;
			}
			return differed + " of " + compared + " differed; first: " + (_first ?? "?");
		}

		internal static void Turn(int axis, float a, Mat3 now)
		{
			Mat3 a2;
			switch (axis)
			{
			default:
				a2 = Mat3.RotZOld(a);
				break;
			case 1:
				a2 = Mat3.RotYOld(a);
				break;
			case 0:
				a2 = Mat3.RotXOld(a);
				break;
			}
			string text = Differs(a2, now);
			if (Count(ref _turns, text))
			{
				string[] obj = new string[6] { "Rot", null, null, null, null, null };
				object obj2;
				switch (axis)
				{
				default:
					obj2 = "Z";
					break;
				case 1:
					obj2 = "Y";
					break;
				case 0:
					obj2 = "X";
					break;
				}
				obj[1] = (string)obj2;
				obj[2] = "(";
				obj[3] = Show(a);
				obj[4] = "): old vs new ";
				obj[5] = text;
				_first = string.Concat(obj);
			}
		}

		internal static void Mul(Mat3 m, Vec3 v, Vec3 now)
		{
			string text = Differs(m.MulOld(v), now, "");
			if (Count(ref _muls, text))
			{
				_first = "Mul(" + Show(m) + ", " + Show(v) + "): old vs new " + text;
			}
		}

		internal static void Product(Mat3 a, Mat3 b, Mat3 now)
		{
			string text = Differs(Mat3.ProductOld(a, b), now);
			if (Count(ref _products, text))
			{
				_first = "product " + Show(a) + " * " + Show(b) + ": old vs new " + text;
			}
		}

		internal static void Scaled(Mat3 a, float s, Mat3 now)
		{
			string text = Differs(Mat3.ScaledOld(a, s), now);
			if (Count(ref _scaled, text))
			{
				_first = "scaled " + Show(a) + " * " + Show(s) + ": old vs new " + text;
			}
		}

		private static bool Count(ref long kind, string differs)
		{
			Interlocked.Increment(ref kind);
			Interlocked.Increment(ref _compared);
			if (differs != null)
			{
				return Interlocked.Increment(ref _differed) == 1;
			}
			return false;
		}

		private static uint Bits(float f)
		{
			FloatBits floatBits = new FloatBits
			{
				F = f
			};
			return floatBits.U;
		}

		private static string Show(float f)
		{
			return f.ToString("R", CultureInfo.InvariantCulture) + " (0x" + Bits(f).ToString("X8") + ")";
		}

		private static string Show(Vec3 v)
		{
			return "(" + Show(v.X) + ", " + Show(v.Y) + ", " + Show(v.Z) + ")";
		}

		private static string Show(Mat3 m)
		{
			return "[" + Show(m.C0) + ", " + Show(m.C1) + ", " + Show(m.C2) + "]";
		}

		private static string Differs(Vec3 a, Vec3 b, string prefix)
		{
			if (Bits(a.X) != Bits(b.X))
			{
				return prefix + "X " + Show(a.X) + " vs " + Show(b.X);
			}
			if (Bits(a.Y) != Bits(b.Y))
			{
				return prefix + "Y " + Show(a.Y) + " vs " + Show(b.Y);
			}
			if (Bits(a.Z) != Bits(b.Z))
			{
				return prefix + "Z " + Show(a.Z) + " vs " + Show(b.Z);
			}
			return null;
		}

		private static string Differs(Mat3 a, Mat3 b)
		{
			return Differs(a.C0, b.C0, "C0.") ?? Differs(a.C1, b.C1, "C1.") ?? Differs(a.C2, b.C2, "C2.");
		}
	}
}
