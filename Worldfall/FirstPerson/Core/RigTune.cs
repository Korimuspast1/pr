using System.Globalization;
using System.Runtime.InteropServices;

namespace FirstPerson.Core
{
	public static class RigTune
	{
		public sealed class Tally
		{
			public bool On;

			public int Calls;

			public int Busy;

			public int Busier;

			public int Differed;

			public string First;

			public void Start(bool on)
			{
				On = on;
				if (on)
				{
					Calls = (Busy = (Busier = (Differed = 0)));
					First = null;
				}
			}

			public void Count(string differs)
			{
				Calls++;
				if (differs != null)
				{
					Differed++;
					if (First == null)
					{
						First = differs;
					}
				}
			}
		}

		[StructLayout(LayoutKind.Explicit)]
		private struct FloatBits
		{
			[FieldOffset(0)]
			public float F;

			[FieldOffset(0)]
			public uint U;
		}

		public static bool Transform = true;

		public static bool Plant = true;

		public static bool Creature = true;

		public static bool Lean = true;

		public static readonly Tally TransformCheck = new Tally();

		public static readonly Tally PlantCheck = new Tally();

		public static readonly Tally LeanCheck = new Tally();

		public static uint Bits(float f)
		{
			FloatBits floatBits = new FloatBits
			{
				F = f
			};
			return floatBits.U;
		}

		public static bool Same(Vec3 a, Vec3 b)
		{
			if (Bits(a.X) == Bits(b.X) && Bits(a.Y) == Bits(b.Y))
			{
				return Bits(a.Z) == Bits(b.Z);
			}
			return false;
		}

		public static bool Same(Mat3 a, Mat3 b)
		{
			if (Same(a.C0, b.C0) && Same(a.C1, b.C1))
			{
				return Same(a.C2, b.C2);
			}
			return false;
		}

		public static string PoseDiffers(MeshInstance a, MeshInstance b)
		{
			if (a.PartRotation == null != (b.PartRotation == null) || (a.PartRotation != null && a.PartRotation.Length != b.PartRotation.Length))
			{
				return "turns' arrays";
			}
			if (a.PartOffset == null != (b.PartOffset == null) || (a.PartOffset != null && a.PartOffset.Length != b.PartOffset.Length))
			{
				return "shifts' arrays";
			}
			if (a.PartScale == null != (b.PartScale == null) || (a.PartScale != null && a.PartScale.Length != b.PartScale.Length))
			{
				return "sizes' arrays";
			}
			if (a.PartRotation != null)
			{
				for (int i = 0; i < a.PartRotation.Length; i++)
				{
					if (!Same(a.PartRotation[i], b.PartRotation[i]))
					{
						return "part " + i + " (" + PartName(a, i) + ") turned " + Show(a.PartRotation[i]) + " vs " + Show(b.PartRotation[i]);
					}
				}
			}
			if (a.PartOffset != null)
			{
				for (int j = 0; j < a.PartOffset.Length; j++)
				{
					if (!Same(a.PartOffset[j], b.PartOffset[j]))
					{
						return "part " + j + " (" + PartName(a, j) + ") shifted " + Show(a.PartOffset[j]) + " vs " + Show(b.PartOffset[j]);
					}
				}
			}
			if (a.PartScale != null)
			{
				for (int k = 0; k < a.PartScale.Length; k++)
				{
					if (Bits(a.PartScale[k]) != Bits(b.PartScale[k]))
					{
						return "part " + k + " (" + PartName(a, k) + ") sized " + Show(a.PartScale[k]) + " vs " + Show(b.PartScale[k]);
					}
				}
			}
			return null;
		}

		private static string PartName(MeshInstance m, int i)
		{
			if (m.Model == null || i >= m.Model.Parts.Length)
			{
				return "?";
			}
			return m.Model.Parts[i].Name;
		}

		public static string Show(float f)
		{
			return f.ToString("R", CultureInfo.InvariantCulture) + " (0x" + Bits(f).ToString("X8") + ")";
		}

		public static string Show(Vec3 v)
		{
			return "(" + Show(v.X) + ", " + Show(v.Y) + ", " + Show(v.Z) + ")";
		}

		public static string Show(Mat3 m)
		{
			return "[" + Show(m.C0) + ", " + Show(m.C1) + ", " + Show(m.C2) + "]";
		}

		public static void CopyPose(MeshInstance from, MeshInstance into)
		{
			into.Model = from.Model;
			into.Position = from.Position;
			into.Basis = from.Basis;
			into.PartRotation = ((from.PartRotation != null) ? ((Mat3[])from.PartRotation.Clone()) : null);
			into.PartOffset = ((from.PartOffset != null) ? ((Vec3[])from.PartOffset.Clone()) : null);
			into.PartScale = ((from.PartScale != null) ? ((float[])from.PartScale.Clone()) : null);
		}
	}
}
