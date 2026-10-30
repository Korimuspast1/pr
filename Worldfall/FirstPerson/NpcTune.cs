using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using FirstPerson.Core;
using FirstPerson.Gpu;
using UnityEngine;

namespace FirstPerson
{
	internal static class NpcTune
	{
		internal sealed class Switch
		{
			public readonly string Name;

			public readonly Func<bool> Get;

			public readonly Action<bool> Set;

			public Switch(string name, Func<bool> get, Action<bool> set)
			{
				Name = name;
				Get = get;
				Set = set;
			}
		}

		internal sealed class Check
		{
			public readonly string Name;

			public readonly Func<int, string> Run;

			public Check(string name, Func<int, string> run)
			{
				Name = name;
				Run = run;
			}
		}

		internal sealed class LiveCheck
		{
			public readonly string Name;

			public readonly Action<bool> Enable;

			public readonly Func<string> Result;

			public LiveCheck(string name, Action<bool> enable, Func<string> result)
			{
				Name = name;
				Enable = enable;
				Result = result;
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

		internal struct Rng
		{
			private ulong _s;

			public Rng(ulong seed)
			{
				_s = ((seed != 0L) ? seed : 11400714819323198485uL);
			}

			public ulong Next()
			{
				_s ^= _s >> 12;
				_s ^= _s << 25;
				_s ^= _s >> 27;
				return _s * 2685821657736338717L;
			}

			public float Unit()
			{
				return (float)(Next() >> 40) / 16777216f;
			}

			public float Float(int i)
			{
				if (i < 0 || i >= Awkward.Length)
				{
					return Random();
				}
				return Awkward[i];
			}

			public float Random()
			{
				ulong num = Next();
				switch ((int)(num % 20))
				{
				case 0:
				case 1:
					return FromBits((uint)(num >> 32));
				case 2:
				case 3:
				case 4:
					return (Unit() * 2f - 1f) * 0.001f;
				case 5:
				case 6:
				case 7:
					return (Unit() * 2f - 1f) * 10000f;
				case 8:
				case 9:
				case 10:
				case 11:
					return Unit() * 2f - 1f;
				default:
					return (Unit() * 2f - 1f) * 4f * MathF.PI;
				}
			}

			public Mat3 Matrix(int i)
			{
				if ((Next() & 3) == 0L)
				{
					return new Mat3(new Vec3(Unit() * 4f - 2f, Unit() * 4f - 2f, Unit() * 4f - 2f), new Vec3(Unit() * 4f - 2f, Unit() * 4f - 2f, Unit() * 4f - 2f), new Vec3(Unit() * 4f - 2f, Unit() * 4f - 2f, Unit() * 4f - 2f));
				}
				return Mat3.RotZ(Float(i)) * Mat3.RotY(Float(i + 7)) * Mat3.RotX(Float(i + 13));
			}

			public Vec3 Vector(int i)
			{
				return new Vec3(Float(i), Float(i + 3), Float(i + 11));
			}
		}

		internal delegate T Make<T>(ref Rng rng, int i);

		private sealed class HeadPose
		{
			public MeshInstance Body;

			public Mat3[] Rot;

			public Vec3[] Off;

			public float[] Scl;

			public int[] Chain;
		}

		private struct HeadOut
		{
			public bool Has;

			public bool Got;

			public bool Again;

			public HeadBox Box;

			public HeadBox Second;
		}

		private sealed class Arrays
		{
			public Mat3[] Rot;

			public Vec3[] Off;

			public float[] Scale;
		}

		internal static readonly List<Switch> Switches;

		internal static readonly List<LiveCheck> LiveChecks;

		internal static readonly List<Check> Checks;

		private static readonly float[] Awkward;

		private static bool _every;

		private static List<string> _creatureIds;

		private static HeadPose[] _headPoses;

		private static bool _headSome;

		private static HeadBox.Snap _headSnap;

		private static readonly string[] HeadModels;

		private static readonly string[] CreatureParts;

		private static bool _spriteMemoWas;

		private static bool _dockBoxesWas;

		private static readonly string[] OddNumbers;

		internal static string Names
		{
			get
			{
				if (Switches.Count == 0)
				{
					return "none";
				}
				string[] array = new string[Switches.Count];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = Switches[i].Name;
				}
				return string.Join(", ", array);
			}
		}

		static NpcTune()
		{
			Switches = new List<Switch>();
			LiveChecks = new List<LiveCheck>();
			Checks = new List<Check>
			{
				new Check("Mat3.RotX against itself (sanity)", (int n) => Compare(n, (float a) => Mat3.RotX(a), (float a) => Mat3.RotX(a)))
			};
			Awkward = new float[38]
			{
				0f,
				-0f,
				1f,
				-1f,
				0.5f,
				-0.5f,
				2f,
				0.1f,
				-0.1f,
				MathF.PI / 2f,
				-MathF.PI / 2f,
				MathF.PI,
				-MathF.PI,
				MathF.PI * 2f,
				MathF.PI * -2f,
				3.1415925f,
				MathF.PI,
				1E-07f,
				-1E-07f,
				1E-20f,
				1E-38f,
				float.Epsilon,
				-1E-45f,
				10000f,
				-10000f,
				123456.7f,
				1E+10f,
				-1E+10f,
				float.MaxValue,
				float.MinValue,
				float.PositiveInfinity,
				float.NegativeInfinity,
				float.NaN,
				0.9999999f,
				1.0000001f,
				MathF.PI * 2f,
				100.5f,
				1000.25f
			};
			_every = true;
			HeadModels = new string[15]
			{
				"person_human", "person_human_fem", "person_elf", "person_elf_fem", "person_orc", "person_dwarf_fem", "person_zombie", "person_beast_cat", "person_skeleton", "animal_wolf",
				"animal_chicken", "animal_snake", "monster_dragon", "monster_slime", "monster_ufo"
			};
			CreatureParts = new string[11]
			{
				"body", "head", "tail", "flames", "claw_l", "wing_l", "halo", "leg_fl", "leg_l", "leg_1",
				"seg_1"
			};
			OddNumbers = new string[21]
			{
				"", " ", "abc", "1,5", " 1.5", "1.5 ", "NaN", "Infinity", "-Infinity", "1e40",
				"-1e40", "1e-50", "-0", "+3", "0x10", "1.5f", ".5", "5.", "1e", "--1",
				"∞"
			};
			List<MethodInfo> list = new List<MethodInfo>();
			MethodInfo[] methods = typeof(NpcTune).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (MethodInfo methodInfo in methods)
			{
				if (methodInfo.Name.StartsWith("Register", StringComparison.Ordinal) && methodInfo.GetParameters().Length == 0 && methodInfo.ReturnType == typeof(void))
				{
					list.Add(methodInfo);
				}
			}
			list.Sort((MethodInfo a, MethodInfo b) => string.CompareOrdinal(a.Name, b.Name));
			foreach (MethodInfo item in list)
			{
				item.Invoke(null, null);
			}
		}

		internal static bool[] Save()
		{
			bool[] array = new bool[Switches.Count];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = Switches[i].Get();
			}
			return array;
		}

		internal static void Restore(bool[] on)
		{
			for (int i = 0; i < on.Length && i < Switches.Count; i++)
			{
				Switches[i].Set(on[i]);
			}
		}

		internal static void SetAll(bool on)
		{
			foreach (Switch @switch in Switches)
			{
				@switch.Set(on);
			}
		}

		internal static uint Bits(float f)
		{
			FloatBits floatBits = new FloatBits
			{
				F = f
			};
			return floatBits.U;
		}

		internal static float FromBits(uint u)
		{
			FloatBits floatBits = new FloatBits
			{
				U = u
			};
			return floatBits.F;
		}

		internal static string Show(float f)
		{
			return f.ToString("R", CultureInfo.InvariantCulture) + " (0x" + Bits(f).ToString("X8") + ")";
		}

		internal static string Differs(Vec3 a, Vec3 b, string prefix = "")
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

		internal static string Differs(Mat3 a, Mat3 b)
		{
			return Differs(a.C0, b.C0, "C0.") ?? Differs(a.C1, b.C1, "C1.") ?? Differs(a.C2, b.C2, "C2.");
		}

		private static string Differs(float a, float b)
		{
			if (Bits(a) == Bits(b))
			{
				return null;
			}
			return Show(a) + " vs " + Show(b);
		}

		internal static string Compare(int n, Func<float, float> old, Func<float, float> now)
		{
			Rng rng = new Rng(1592590337uL);
			for (int i = 0; i < n; i++)
			{
				float num = rng.Float(i);
				string text = Differs(old(num), now(num));
				if (text != null)
				{
					return "input " + Show(num) + ": " + text;
				}
			}
			return null;
		}

		internal static string Compare(int n, Func<float, Mat3> old, Func<float, Mat3> now)
		{
			Rng rng = new Rng(1592590338uL);
			for (int i = 0; i < n; i++)
			{
				float num = rng.Float(i);
				string text = Differs(old(num), now(num));
				if (text != null)
				{
					return "input " + Show(num) + ": " + text;
				}
			}
			return null;
		}

		internal static string Compare(int n, Func<float, float, float, Mat3> old, Func<float, float, float, Mat3> now)
		{
			Rng rng = new Rng(1592590339uL);
			for (int i = 0; i < n; i++)
			{
				int num = i / Awkward.Length;
				int num2 = i % Awkward.Length;
				float num3 = ((num == 0) ? Awkward[num2] : rng.Random());
				float num4 = ((num == 1) ? Awkward[num2] : rng.Random());
				float num5 = ((num == 2) ? Awkward[num2] : rng.Random());
				string text = Differs(old(num3, num4, num5), now(num3, num4, num5));
				if (text != null)
				{
					return "inputs " + Show(num3) + ", " + Show(num4) + ", " + Show(num5) + ": " + text;
				}
			}
			return null;
		}

		internal static string Compare(int n, Func<Mat3, Mat3, Mat3> old, Func<Mat3, Mat3, Mat3> now)
		{
			Rng rng = new Rng(1592590340uL);
			for (int i = 0; i < n; i++)
			{
				Mat3 arg = rng.Matrix(i);
				Mat3 arg2 = rng.Matrix(i + 5);
				string text = Differs(old(arg, arg2), now(arg, arg2));
				if (text != null)
				{
					return "input " + i + ": " + text;
				}
			}
			return null;
		}

		internal static string Compare(int n, Func<Mat3, Vec3, Vec3> old, Func<Mat3, Vec3, Vec3> now)
		{
			Rng rng = new Rng(1592590341uL);
			for (int i = 0; i < n; i++)
			{
				Mat3 arg = rng.Matrix(i);
				Vec3 arg2 = rng.Vector(i);
				string text = Differs(old(arg, arg2), now(arg, arg2));
				if (text != null)
				{
					return "input " + i + " (" + Show(arg2.X) + ", " + Show(arg2.Y) + ", " + Show(arg2.Z) + "): " + text;
				}
			}
			return null;
		}

		internal static string Compare<TIn, TOut>(int n, Make<TIn> input, Func<TIn, TOut> old, Func<TIn, TOut> now, Func<TOut, TOut, string> differs, Func<TIn, string> show = null)
		{
			Rng rng = new Rng(1592590342uL);
			for (int i = 0; i < n; i++)
			{
				TIn arg = input(ref rng, i);
				string text = differs(old(arg), now(arg));
				if (text != null)
				{
					return "input " + ((show != null) ? show(arg) : i.ToString()) + ": " + text;
				}
			}
			return null;
		}

		private static void RegisterGpu()
		{
			Switches.Add(new Switch("GpuCopiesInSteps", () => GpuModels.Phased, delegate(bool v)
			{
				GpuModels.Phased = v;
			}));
			Switches.Add(new Switch("GpuPoseTwins", () => GpuModels.PoseTwins, delegate(bool v)
			{
				GpuModels.PoseTwins = v;
			}));
			Switches.Add(new Switch("GpuWritesBehind", () => GpuModels.WriteBehind, delegate(bool v)
			{
				GpuModels.WriteBehind = v;
			}));
			Switches.Add(new Switch("GpuCullOnWorkers", () => GpuModels.CullOnWorkers, delegate(bool v)
			{
				GpuModels.CullOnWorkers = v;
			}));
			Switches.Add(new Switch("GpuLayOnWorkers", () => GpuInstances.LayOnWorkers, delegate(bool v)
			{
				GpuInstances.LayOnWorkers = v;
			}));
			Checks.Add(new Check("GpuModels posing on a worker's scratch against the Mat3/Vec3 operators", PosingCheck));
			LiveChecks.Add(new LiveCheck("gpu copies in steps", delegate(bool on)
			{
				if (on)
				{
					GpuModels.VerifyRuns = (GpuModels.VerifyDiffers = (GpuModels.VerifyCopies = (GpuModels.VerifyTwins = 0)));
					GpuModels.VerifyFirst = null;
					Array.Clear(GpuModels.VerifyMixes, 0, GpuModels.VerifyMixes.Length);
				}
				else if (GpuModels.Verify)
				{
					Log.Info(string.Format("NPCTUNE gpu copies in steps: {0} gatherings compared ({1} per mix of twins, writes behind, cull on workers), ", GpuModels.VerifyRuns, string.Join("/", GpuModels.VerifyMixes)) + $"{GpuModels.VerifyCopies} copies, {GpuModels.VerifyTwins} instances posed as a twin, {GpuModels.VerifyDiffers} differed; {GpuModels.WorkerCount} workers");
				}
				GpuModels.Verify = on;
			}, delegate
			{
				if (GpuModels.VerifyRuns == 0)
				{
					if (!GpuModels.Phased)
					{
						return "gathering in steps off";
					}
					return "no gathering in steps compared";
				}
				if (GpuModels.VerifyDiffers > 0)
				{
					return $"{GpuModels.VerifyDiffers} of {GpuModels.VerifyRuns} gatherings differed, the first: {GpuModels.VerifyFirst}";
				}
				for (int i = 0; i < GpuModels.VerifyMixes.Length; i++)
				{
					if (GpuModels.VerifyMixes[i] == 0)
					{
						return $"mix {i} of the steps' switches never compared ({GpuModels.VerifyRuns} gatherings)";
					}
				}
				return (string)null;
			}));
			LiveChecks.Add(new LiveCheck("gpu lay on workers", delegate(bool on)
			{
				if (on)
				{
					GpuInstances.LayChecks = (GpuInstances.LaySplit = (GpuInstances.LayDiffers = 0));
					GpuInstances.LayFirst = null;
				}
				else if (GpuInstances.VerifyLay)
				{
					Log.Info($"NPCTUNE gpu lay on workers: {GpuInstances.LayChecks} lays compared, {GpuInstances.LaySplit} split over the workers, {GpuInstances.LayDiffers} differed");
				}
				GpuInstances.VerifyLay = on;
			}, delegate
			{
				if (!GpuInstances.LayOnWorkers)
				{
					return "lay on workers off";
				}
				if (GpuInstances.LaySplit == 0)
				{
					return $"no lay split over the workers ({GpuInstances.LayChecks} compared)";
				}
				return (GpuInstances.LayDiffers > 0) ? $"{GpuInstances.LayDiffers} of {GpuInstances.LayChecks} lays differed, the first: {GpuInstances.LayFirst}" : null;
			}));
		}

		private static string PosingCheck(int n)
		{
			Rng rng = new Rng(1592617744uL);
			MeshModel meshModel = new MeshModel
			{
				Parts = new MeshPart[4]
			};
			MeshInstance meshInstance = new MeshInstance
			{
				Model = meshModel,
				PartRotation = new Mat3[4],
				PartOffset = new Vec3[4],
				PartScale = new float[4]
			};
			int num = Math.Max(1, n / 4);
			for (int i = 0; i < num; i++)
			{
				for (int j = 0; j < 4; j++)
				{
					meshModel.Parts[j] = meshModel.Parts[j] ?? new MeshPart();
					meshModel.Parts[j].Parent = ((j == 0) ? (-1) : ((int)(rng.Next() % (ulong)j)));
					meshModel.Parts[j].Pivot = rng.Vector(i + j);
					meshInstance.PartRotation[j] = rng.Matrix(i + j);
					meshInstance.PartOffset[j] = (((rng.Next() & 1) == 0L) ? new Vec3(0f, 0f, 0f) : rng.Vector(i + j + 5));
					meshInstance.PartScale[j] = (((rng.Next() & 3) == 0L) ? rng.Float(i + j) : 1f);
				}
				Mat3 basis = rng.Matrix(i + 3);
				Vec3 pos = rng.Vector(i + 9);
				string text = GpuModels.PosingDiffersForTest(meshInstance, meshModel, basis, pos);
				if (text != null)
				{
					return "run " + i + ": " + text;
				}
			}
			return null;
		}

		private static void RegisterZEverySpeedup()
		{
			List<Switch> others = new List<Switch>(Switches);
			if (others.Count < 2)
			{
				return;
			}
			Switches.Add(new Switch("every speed-up", () => _every, delegate(bool v)
			{
				if (v == _every)
				{
					return;
				}
				_every = v;
				foreach (Switch item in others)
				{
					item.Set(v);
				}
			}));
		}

		private static void RegisterZZLooks()
		{
			if (People.TimeLooks)
			{
				Switches.Add(new Switch("looks", () => People.LooksOn, delegate(bool v)
				{
					People.LooksOn = v;
				}));
			}
		}

		private static void RegisterMath()
		{
			Switches.Add(new Switch("Mat3.Fast", () => Mat3.Fast, delegate(bool v)
			{
				Mat3.Fast = v;
			}));
			Checks.Add(new Check("Mat3.RotX", (int n) => FastOn(() => Compare(n, (float a) => Mat3.RotXOld(a), (float a) => Mat3.RotX(a)))));
			Checks.Add(new Check("Mat3.RotY", (int n) => FastOn(() => Compare(n, (float a) => Mat3.RotYOld(a), (float a) => Mat3.RotY(a)))));
			Checks.Add(new Check("Mat3.RotZ", (int n) => FastOn(() => Compare(n, (float a) => Mat3.RotZOld(a), (float a) => Mat3.RotZ(a)))));
			Checks.Add(new Check("Mat3.RotX/Y/Z at quarter turns, tiny and negative angles", (int n) => FastOn(() => Compare(n, delegate(ref Rng rng, int i)
			{
				return AwkwardAngle(ref rng, i);
			}, (float a) => TurnsOf(a, old: true), (float a) => TurnsOf(a, old: false), DiffersTurns, (float a) => Show(a)))));
			Checks.Add(new Check("Mat3.RotZ * RotY * RotX", (int n) => FastOn(() => Compare(n, (float x, float y, float z) => Mat3.ProductOld(Mat3.ProductOld(Mat3.RotZOld(x), Mat3.RotYOld(y)), Mat3.RotXOld(z)), (float x, float y, float z) => Mat3.RotZ(x) * Mat3.RotY(y) * Mat3.RotX(z)))));
			Checks.Add(new Check("Mat3.Mul", (int n) => FastOn(() => Compare(n, (Mat3 m, Vec3 v) => m.MulOld(v), (Mat3 m, Vec3 v) => m.Mul(v)))));
			Checks.Add(new Check("Mat3.Mul, any entries", (int n) => FastOn(() => Compare(n, delegate(ref Rng rng, int i)
			{
				return (AnyMatrix(ref rng, i), new Vec3(AnyEntry(ref rng, i), AnyEntry(ref rng, i + 1), AnyEntry(ref rng, i + 2)));
			}, ((Mat3, Vec3) p) => p.Item1.MulOld(p.Item2), ((Mat3, Vec3) p) => p.Item1.Mul(p.Item2), (Vec3 a, Vec3 b) => Differs(a, b)))));
			Checks.Add(new Check("Mat3 * Mat3", (int n) => FastOn(() => Compare(n, (Mat3 a, Mat3 b) => Mat3.ProductOld(a, b), (Mat3 a, Mat3 b) => a * b))));
			Checks.Add(new Check("Mat3 * Mat3, any entries", (int n) => FastOn(() => Compare(n, delegate(ref Rng rng, int i)
			{
				return (AnyMatrix(ref rng, i), AnyMatrix(ref rng, i + 3));
			}, ((Mat3, Mat3) p) => Mat3.ProductOld(p.Item1, p.Item2), ((Mat3, Mat3) p) => p.Item1 * p.Item2, Differs))));
			Checks.Add(new Check("Mat3 * float", (int n) => FastOn(() => Compare(n, delegate(ref Rng rng, int i)
			{
				return (((rng.Next() & 1) == 0L) ? rng.Matrix(i) : AnyMatrix(ref rng, i), rng.Float(i));
			}, ((Mat3, float) p) => Mat3.ScaledOld(p.Item1, p.Item2), ((Mat3, float) p) => p.Item1 * p.Item2, Differs, ((Mat3, float) p) => Show(p.Item2)))));
			Checks.Add(new Check("Mat3.Identity", (int n) => FastOn(() => Differs(Mat3.IdentityOld, Mat3.Identity))));
			Checks.Add(new Check("Mat3.RotX/Y/Z and Mat3 * float handed unrounded sums, as the rigs call them", (int n) => FastOn(() => Compare(n, delegate(ref Rng rng, int i)
			{
				return (rng.Float(i), rng.Random(), rng.Matrix(i));
			}, ((float, float, Mat3) p) => Sums(p.Item1, p.Item2, p.Item3, old: true), ((float, float, Mat3) p) => Sums(p.Item1, p.Item2, p.Item3, old: false), DiffersSums, ((float, float, Mat3) p) => Show(p.Item1) + ", " + Show(p.Item2)))));
			LiveChecks.Add(new LiveCheck("Mat3.Fast", delegate(bool on)
			{
				Mat3Verify.Enable(on);
			}, delegate
			{
				Log.Info("CITYPERF live Mat3.Fast compared " + Mat3Verify.Compared + " (" + Mat3Verify.Counts + "), " + Mat3Verify.Differed + " differed");
				return Mat3Verify.Result();
			}));
		}

		private static string FastOn(Func<string> run)
		{
			bool fast = Mat3.Fast;
			Mat3.Fast = true;
			try
			{
				return run();
			}
			finally
			{
				Mat3.Fast = fast;
			}
		}

		private static float AwkwardAngle(ref Rng rng, int i)
		{
			if (i < Awkward.Length)
			{
				return Awkward[i];
			}
			ulong num = rng.Next();
			uint num2 = (((num & 1) != 0L) ? 2147483648u : 0u);
			switch ((int)((num >> 1) % 4))
			{
			case 0:
			case 1:
			{
				float num5 = (float)((double)((int)((num >> 8) % 40001) - 20000) * (Math.PI / 2.0));
				int num6 = (int)((num >> 40) % 9) - 4;
				uint num7 = Bits(num5);
				if (num5 == 0f)
				{
					return FromBits(num2 | (uint)Math.Abs(num6));
				}
				return FromBits(num7 + (uint)num6);
			}
			case 2:
			{
				uint num3 = (uint)((num >> 8) % 121);
				uint num4 = (uint)((int)(num >> 40) & 0x7FFFFF);
				return FromBits(num2 | (num3 << 23) | num4);
			}
			default:
				return FromBits((uint)(num >> 32));
			}
		}

		private static Mat3[] TurnsOf(float a, bool old)
		{
			if (old)
			{
				return new Mat3[3]
				{
					Mat3.RotXOld(a),
					Mat3.RotYOld(a),
					Mat3.RotZOld(a)
				};
			}
			return new Mat3[3]
			{
				Mat3.RotX(a),
				Mat3.RotY(a),
				Mat3.RotZ(a)
			};
		}

		private static Mat3[] Sums(float x, float y, Mat3 m, bool old)
		{
			if (old)
			{
				return new Mat3[4]
				{
					Mat3.RotXOld((0f - x) * 0.45f - y * 0.3f),
					Mat3.RotYOld(x * 1.3f + y),
					Mat3.RotZOld(0.1f + 0.06f * x * y),
					Mat3.ScaledOld(m, x * 0.7f - y)
				};
			}
			return new Mat3[4]
			{
				Mat3.RotX((0f - x) * 0.45f - y * 0.3f),
				Mat3.RotY(x * 1.3f + y),
				Mat3.RotZ(0.1f + 0.06f * x * y),
				m * (x * 0.7f - y)
			};
		}

		private static string DiffersSums(Mat3[] a, Mat3[] b)
		{
			for (int i = 0; i < a.Length; i++)
			{
				string text = Differs(a[i], b[i]);
				if (text != null)
				{
					object obj;
					switch (i)
					{
					default:
						obj = "* float";
						break;
					case 2:
						obj = "RotZ";
						break;
					case 1:
						obj = "RotY";
						break;
					case 0:
						obj = "RotX";
						break;
					}
					return (string)obj + " " + text;
				}
			}
			return null;
		}

		private static string DiffersTurns(Mat3[] a, Mat3[] b)
		{
			for (int i = 0; i < 3; i++)
			{
				string text = Differs(a[i], b[i]);
				if (text != null)
				{
					object obj;
					switch (i)
					{
					default:
						obj = "Z";
						break;
					case 1:
						obj = "Y";
						break;
					case 0:
						obj = "X";
						break;
					}
					return "Rot" + (string)obj + " " + text;
				}
			}
			return null;
		}

		private static float AnyEntry(ref Rng rng, int i)
		{
			ulong num = rng.Next();
			if ((num & 3) != 0L)
			{
				return rng.Random();
			}
			return Awkward[(uint)((num >> 8) % (ulong)Awkward.Length)];
		}

		private static Mat3 AnyMatrix(ref Rng rng, int i)
		{
			return new Mat3(new Vec3(AnyEntry(ref rng, i), AnyEntry(ref rng, i), AnyEntry(ref rng, i)), new Vec3(AnyEntry(ref rng, i), AnyEntry(ref rng, i), AnyEntry(ref rng, i)), new Vec3(AnyEntry(ref rng, i), AnyEntry(ref rng, i), AnyEntry(ref rng, i)));
		}

		private static void RegisterMotion()
		{
			Switches.Add(new Switch("body", () => BodyMotion.Fast, delegate(bool v)
			{
				BodyMotion.Fast = v;
			}));
			Switches.Add(new Switch("ground", () => SceneCollector.GroundMemo, delegate(bool v)
			{
				SceneCollector.GroundMemo = v;
			}));
			Switches.Add(new Switch("crowd", () => SceneCollector.CrowdFast, delegate(bool v)
			{
				SceneCollector.CrowdFast = v;
			}));
			Switches.Add(new Switch("steer", () => Steering.Fast, delegate(bool v)
			{
				Steering.Fast = v;
			}));
			Switches.Add(new Switch("steercrowd", () => Steering.CrowdQuick, delegate(bool v)
			{
				Steering.CrowdQuick = v;
			}));
			Checks.Add(new Check("Footprint.SampleDistance against Sample", SampleCheck));
			Checks.Add(new Check("Colliders.DistanceTo and DistanceIn against Distance", DistanceCheck));
			LiveChecks.Add(new LiveCheck("body", delegate(bool on)
			{
				if (on)
				{
					BodyMotion.Verified = (BodyMotion.Mismatched = (BodyMotion.RoutesKept = (BodyMotion.PlacesKept = 0L)));
					BodyMotion.FirstMismatch = null;
				}
				BodyMotion.Verify = on;
			}, () => Verdict(BodyMotion.Verified, BodyMotion.Mismatched, BodyMotion.FirstMismatch, $"steps ({BodyMotion.RoutesKept} with the route kept, {BodyMotion.PlacesKept} the place on it too)")));
			LiveChecks.Add(new LiveCheck("ground", delegate(bool on)
			{
				if (on)
				{
					SceneCollector.GroundVerified = (SceneCollector.GroundMismatched = 0L);
					SceneCollector.GroundFirstMismatch = null;
				}
				SceneCollector.GroundVerify = on;
			}, () => Verdict(SceneCollector.GroundVerified, SceneCollector.GroundMismatched, SceneCollector.GroundFirstMismatch, "tiles remembered")));
			LiveChecks.Add(new LiveCheck("crowd", delegate(bool on)
			{
				if (on)
				{
					SceneCollector.CrowdVerified = (SceneCollector.CrowdMismatched = 0L);
					SceneCollector.CrowdFirstMismatch = null;
				}
				SceneCollector.CrowdVerify = on;
			}, () => Verdict(SceneCollector.CrowdVerified, SceneCollector.CrowdMismatched, SceneCollector.CrowdFirstMismatch, "frames of walkers kept apart")));
			LiveChecks.Add(new LiveCheck("steer", delegate(bool on)
			{
				if (on)
				{
					Steering.Verified = (Steering.Mismatched = 0L);
					Steering.FirstMismatch = null;
				}
				Steering.Verify = on;
			}, () => Verdict(Steering.Verified, Steering.Mismatched, Steering.FirstMismatch, "probes (walkers stepped round each other among them)")));
			LiveChecks.Add(new LiveCheck("steercrowd", delegate(bool on)
			{
				if (on)
				{
					Steering.CrowdVerifiedCount = (Steering.CrowdMismatchedCount = 0L);
					Steering.CrowdFirstMismatch = null;
				}
				Steering.Verify = on;
			}, () => Verdict(Steering.CrowdVerifiedCount, Steering.CrowdMismatchedCount, Steering.CrowdFirstMismatch, "walkers stepped round each other")));
		}

		private static string Verdict(long compared, long differed, string first, string what)
		{
			Log.Info($"CITYPERF motion live: {compared} {what} compared, {differed} differed");
			if (compared == 0L)
			{
				return "nothing was compared";
			}
			if (differed != 0L)
			{
				return $"{differed} of {compared} {what} differ; the first: {first}";
			}
			return null;
		}

		private static Footprint TestFootprint(ref Rng rng)
		{
			int num = 12 + (int)(rng.Unit() * 40f);
			int num2 = 12 + (int)(rng.Unit() * 40f);
			Footprint footprint = new Footprint
			{
				X0 = rng.Unit() * 8f - 4f,
				Y0 = rng.Unit() * 8f - 4f,
				Cell = 0.05f + rng.Unit() * 0.3f,
				W = num,
				H = num2,
				Dist = new float[num * num2]
			};
			float num3 = (float)num * 0.5f;
			float num4 = (float)num2 * 0.5f;
			float num5 = (float)Math.Min(num, num2) * 0.3f;
			for (int i = 0; i < num2; i++)
			{
				for (int j = 0; j < num; j++)
				{
					float num6 = ((float)Math.Sqrt(((float)j - num3) * ((float)j - num3) + ((float)i - num4) * ((float)i - num4)) - num5) * footprint.Cell + (rng.Unit() - 0.5f) * 0.05f;
					if (rng.Unit() < 0.05f)
					{
						num6 = 0f;
					}
					footprint.Dist[i * num + j] = num6;
				}
			}
			footprint.Reach = (float)Math.Max(num, num2) * footprint.Cell;
			return footprint;
		}

		private static float Around(ref Rng rng, int i, float lo, float hi)
		{
			if (i < 40)
			{
				return rng.Float(i);
			}
			if (rng.Next() % 50 == 0L)
			{
				return rng.Random();
			}
			return lo + (hi - lo) * (rng.Unit() * 1.6f - 0.3f);
		}

		private static string SampleCheck(int n)
		{
			Rng rng = new Rng(1592594433uL);
			Footprint footprint = null;
			for (int i = 0; i < n; i++)
			{
				if (i % 5000 == 0)
				{
					footprint = TestFootprint(ref rng);
				}
				float num = Around(ref rng, i % 5000, footprint.X0, footprint.X0 + (float)footprint.W * footprint.Cell);
				float num2 = Around(ref rng, i * 7 % 5000, footprint.Y0, footprint.Y0 + (float)footprint.H * footprint.Cell);
				float f = 0f;
				float f2 = 0f;
				string text;
				try
				{
					f = footprint.Sample(num, num2, out var _, out var _);
					text = null;
				}
				catch (Exception ex)
				{
					text = ex.GetType().Name;
				}
				string text2;
				try
				{
					f2 = footprint.SampleDistance(num, num2);
					text2 = null;
				}
				catch (Exception ex2)
				{
					text2 = ex2.GetType().Name;
				}
				if (text != text2)
				{
					return "at " + Show(num) + ", " + Show(num2) + ": the old way " + (text ?? Show(f)) + ", the new " + (text2 ?? Show(f2));
				}
				if (text == null && Bits(f) != Bits(f2))
				{
					return "at " + Show(num) + ", " + Show(num2) + ": " + Show(f) + " vs " + Show(f2);
				}
			}
			return null;
		}

		private static string DistanceCheck(int n)
		{
			Rng rng = new Rng(1592594434uL);
			Footprint footprint = TestFootprint(ref rng);
			for (int i = 0; i < n; i++)
			{
				if (i % 5000 == 4999)
				{
					footprint = TestFootprint(ref rng);
				}
				Collider c = new Collider
				{
					X = rng.Unit() * 600f,
					Y = rng.Unit() * 600f
				};
				ulong num = rng.Next() % 3;
				switch (num)
				{
				case 0uL:
					c.Radius = rng.Unit() * 1.5f;
					break;
				case 1uL:
					c.Box = true;
					c.HalfW = rng.Unit() * 2f;
					c.HalfD = rng.Unit() * 2f;
					break;
				default:
				{
					float f = rng.Unit() * 7f;
					c.Shape = footprint;
					c.Cos = Mathf.Cos(f);
					c.Sin = Mathf.Sin(f);
					c.Scale = 0.2f + rng.Unit() * 2f;
					c.Mirror = (rng.Next() & 1) == 0;
					break;
				}
				}
				float num2 = ((num == 2) ? (footprint.Reach * c.Scale + 1.5f) : 3f);
				Vector2 q = new Vector2((i < 40) ? rng.Float(i) : (c.X + (rng.Unit() * 2f - 1f) * num2), (i < 40) ? rng.Float(i + 3) : (c.Y + (rng.Unit() * 2f - 1f) * num2));
				if (i >= 40 && rng.Next() % 64 == 0L)
				{
					q.x = rng.Random();
				}
				float f2 = 0f;
				float f3 = 0f;
				float f4 = 0f;
				Vector2 normal = default(Vector2);
				Vector2 normal2 = default(Vector2);
				string text;
				try
				{
					f2 = Colliders.Distance(c, q, out normal);
					text = null;
				}
				catch (Exception ex)
				{
					text = ex.GetType().Name;
				}
				string text2;
				try
				{
					f3 = Colliders.DistanceTo(in c, q);
					text2 = null;
				}
				catch (Exception ex2)
				{
					text2 = ex2.GetType().Name;
				}
				string text3;
				try
				{
					f4 = Colliders.DistanceIn(in c, q, out normal2);
					text3 = null;
				}
				catch (Exception ex3)
				{
					text3 = ex3.GetType().Name;
				}
				string text4 = $"kind {num} at {Show(q.x)}, {Show(q.y)}";
				if (text != text2 || text != text3)
				{
					return text4 + ": the old way " + (text ?? Show(f2)) + ", DistanceTo " + (text2 ?? Show(f3)) + ", DistanceIn " + (text3 ?? Show(f4));
				}
				if (text == null)
				{
					if (Bits(f2) != Bits(f3))
					{
						return text4 + ": DistanceTo " + Show(f3) + " vs " + Show(f2);
					}
					if (Bits(f2) != Bits(f4) || Bits(normal.x) != Bits(normal2.x) || Bits(normal.y) != Bits(normal2.y))
					{
						return text4 + ": DistanceIn " + Show(f4) + " (" + Show(normal2.x) + ", " + Show(normal2.y) + ") vs " + Show(f2) + " (" + Show(normal.x) + ", " + Show(normal.y) + ")";
					}
				}
			}
			return null;
		}

		private static string Counted(string verdict, string compared)
		{
			Log.Info("PEOPLE live check compared " + compared + ((verdict == null) ? "" : (": " + verdict)));
			return verdict;
		}

		private static string CreatureId(ref Rng r, int i)
		{
			if (_creatureIds == null)
			{
				_creatureIds = Models.CreatureIdsForTest();
				_creatureIds.AddRange(new string[10] { "", "zombie", "zombie_", "zombie_animal_", "zombie_animal_zombie_fox", "zombie_human", "civ_cat", "ZOMBIE_fox", "zombie_zombie_fox", "zombie_animal_animal_fox" });
			}
			if (i < _creatureIds.Count)
			{
				return _creatureIds[i];
			}
			string text = _creatureIds[(int)(r.Next() % (ulong)_creatureIds.Count)];
			switch ((int)(r.Next() % 4))
			{
			case 0:
				return text;
			case 1:
				return text + "_x";
			case 2:
				return text.Substring(0, (int)(r.Next() % (ulong)(text.Length + 1)));
			default:
				return "zombie_" + text.Substring((int)(r.Next() % (ulong)(text.Length + 1)));
			}
		}

		private static void RegisterPeople()
		{
			Switches.Add(new Switch("lazy head boxes", () => Hitboxes.LazyHeads, delegate(bool v)
			{
				Hitboxes.LazyHeads = v;
			}));
			Switches.Add(new Switch("armour remembered", () => ArmorMeshes.Remember, delegate(bool v)
			{
				ArmorMeshes.Remember = v;
			}));
			Switches.Add(new Switch("families remembered", () => Models.FamiliesRemembered, delegate(bool v)
			{
				Models.FamiliesRemembered = v;
			}));
			Switches.Add(new Switch("loot watch lean", () => Loot.Lean, delegate(bool v)
			{
				Loot.Lean = v;
			}));
			LiveChecks.Add(new LiveCheck("lazy head boxes", Hitboxes.VerifyHeads, () => Counted(Hitboxes.HeadVerdict(), $"{Hitboxes.HeadsCompared} heads")));
			LiveChecks.Add(new LiveCheck("armour remembered", People.VerifyArmor, () => Counted(People.ArmorVerdict(), People.ArmorCounts)));
			LiveChecks.Add(new LiveCheck("families remembered", People.VerifyFamilies, () => Counted(People.FamiliesVerdict(), $"{People.FamiliesCompared} families, {Models.ZombiesCompared} zombie animals' families")));
			LiveChecks.Add(new LiveCheck("loot watch lean", Loot.VerifyLean, () => Counted(Loot.LeanVerdict(), $"{Loot.LeanCompared} gear reads")));
			Checks.Add(new Check("Models.CreatureFamily by id, remembered", (int n) => Compare(n, CreatureId, Models.CreatureFamilyOldForTest, Models.CreatureFamilyNewForTest, (string x, string y) => (!(x == y)) ? ((x ?? "null") + " vs " + (y ?? "null")) : null, (string s) => "'" + s + "'")));
			Checks.Add(new Check("HeadBox.Snap against HeadBox.Of", (int n) => HeadPosesMissing() ?? Compare(n, HeadInput, HeadOld, HeadNew, HeadDiffers, (HeadPose p) => p.Body.Model?.Name ?? "no model")));
		}

		private static string HeadPosesMissing()
		{
			if (_headPoses == null)
			{
				List<HeadPose> list = new List<HeadPose>();
				string[] headModels = HeadModels;
				foreach (string text in headModels)
				{
					MeshModel meshModel = Models.Get(text);
					if (meshModel == null)
					{
						continue;
					}
					MeshModel[] array = new MeshModel[4]
					{
						meshModel,
						Models.Get(text + "_m"),
						Models.Get(text + "_f"),
						text.StartsWith("person_") ? Models.Distant(meshModel) : null
					};
					foreach (MeshModel meshModel2 in array)
					{
						if (meshModel2 != null && meshModel2.Parts != null && meshModel2.Parts.Length != 0)
						{
							List<int> list2 = new List<int>();
							for (int num = meshModel2.PartIndex("head"); num >= 0; num = meshModel2.Parts[num].Parent)
							{
								list2.Add(num);
							}
							if (list2.Count > 0 && HeadBox.Of(new MeshInstance
							{
								Model = meshModel2
							}, out var _))
							{
								_headSome = true;
							}
							int num2 = meshModel2.Parts.Length;
							list.Add(new HeadPose
							{
								Body = new MeshInstance
								{
									Model = meshModel2
								},
								Rot = new Mat3[num2],
								Off = new Vec3[num2],
								Scl = new float[num2],
								Chain = list2.ToArray()
							});
						}
					}
				}
				_headPoses = list.ToArray();
			}
			if (!_headSome)
			{
				return "no model with a head to try (" + _headPoses.Length + " models)";
			}
			return null;
		}

		private static float HeadNum(ref Rng r, int mode, float scale)
		{
			switch (mode)
			{
			case 1:
				return r.Float((int)(r.Next() % 300));
			case 2:
				return r.Random();
			default:
				return (r.Unit() * 2f - 1f) * scale;
			}
		}

		private static Mat3 HeadMat(ref Rng r, int mode)
		{
			if (mode == 3)
			{
				return r.Matrix(64 + (int)(r.Next() % 4096));
			}
			return new Mat3(new Vec3(HeadNum(ref r, mode, 2f), HeadNum(ref r, mode, 2f), HeadNum(ref r, mode, 2f)), new Vec3(HeadNum(ref r, mode, 2f), HeadNum(ref r, mode, 2f), HeadNum(ref r, mode, 2f)), new Vec3(HeadNum(ref r, mode, 2f), HeadNum(ref r, mode, 2f), HeadNum(ref r, mode, 2f)));
		}

		private static Vec3 HeadVec(ref Rng r, int mode, float scale)
		{
			return new Vec3(HeadNum(ref r, mode, scale), HeadNum(ref r, mode, scale), HeadNum(ref r, mode, scale));
		}

		private static HeadPose HeadInput(ref Rng r, int i)
		{
			HeadPose headPose = _headPoses[(uint)(r.Next() % (ulong)_headPoses.Length)];
			int num = (int)(r.Next() % 8);
			if (num > 3)
			{
				num = 0;
			}
			int[] chain = headPose.Chain;
			foreach (int num2 in chain)
			{
				headPose.Rot[num2] = HeadMat(ref r, num);
				headPose.Off[num2] = HeadVec(ref r, (num != 3) ? num : 0, 0.3f);
				headPose.Scl[num2] = ((num == 0 || num == 3) ? (0.5f + r.Unit()) : HeadNum(ref r, num, 2f));
			}
			headPose.Body.PartRotation = (((r.Next() & 7) == 0L) ? null : headPose.Rot);
			headPose.Body.PartOffset = (((r.Next() & 7) == 0L) ? null : headPose.Off);
			headPose.Body.PartScale = (((r.Next() & 7) == 0L) ? null : headPose.Scl);
			headPose.Body.Basis = HeadMat(ref r, num);
			headPose.Body.Position = HeadVec(ref r, num, 600f);
			return headPose;
		}

		private static HeadOut HeadOld(HeadPose p)
		{
			HeadOut result = default(HeadOut);
			result.Has = (result.Got = (result.Again = HeadBox.Of(p.Body, out result.Box)));
			result.Second = result.Box;
			return result;
		}

		private static HeadOut HeadNew(HeadPose p)
		{
			HeadOut result = new HeadOut
			{
				Has = _headSnap.Take(p.Body)
			};
			Mat3 mat = new Mat3(new Vec3(3f, -1f, 0.5f), new Vec3(0.25f, 2f, -7f), new Vec3(1f, 1f, 9f));
			int[] chain = p.Chain;
			foreach (int num in chain)
			{
				p.Rot[num] = mat;
				p.Off[num] = new Vec3(5f, -6f, 7f);
				p.Scl[num] = 3f;
			}
			p.Body.Basis = mat;
			p.Body.Position = new Vec3(-100000f, 200000f, 33f);
			result.Got = _headSnap.Get(out result.Box);
			result.Again = _headSnap.Get(out result.Second);
			return result;
		}

		private static string HeadDiffers(HeadOut a, HeadOut b)
		{
			if (a.Has != b.Has || a.Got != b.Got || a.Again != b.Again)
			{
				return $"has a head {a.Has} the old way; {b.Has}, asked {b.Got}, again {b.Again} from the copies";
			}
			return Differs(a.Box.Centre, b.Box.Centre, "Centre.") ?? Differs(a.Box.X, b.Box.X, "X.") ?? Differs(a.Box.Y, b.Box.Y, "Y.") ?? Differs(a.Box.Z, b.Box.Z, "Z.") ?? Differs(a.Second.Centre, b.Second.Centre, "again Centre.") ?? Differs(a.Second.X, b.Second.X, "again X.") ?? Differs(a.Second.Y, b.Second.Y, "again Y.") ?? Differs(a.Second.Z, b.Second.Z, "again Z.");
		}

		private static void RegisterRigs()
		{
			Switches.Add(new Switch("RigTransform", () => RigTune.Transform, delegate(bool v)
			{
				RigTune.Transform = v;
			}));
			Switches.Add(new Switch("RigPlant", () => RigTune.Plant, delegate(bool v)
			{
				RigTune.Plant = v;
			}));
			Switches.Add(new Switch("RigCreature", () => RigTune.Creature, delegate(bool v)
			{
				RigTune.Creature = v;
			}));
			Switches.Add(new Switch("RigLean", () => RigTune.Lean, delegate(bool v)
			{
				RigTune.Lean = v;
			}));
			Checks.Add(new Check("PersonRig.Transform and TransformDir float by float (any model, part, pose and placement)", CheckTransform));
			Checks.Add(new Check("PersonRig.Plant, the shin worked out once (an eighth as many inputs: each a whole body's feet)", (int n) => CheckPlant(n / 8)));
			Checks.Add(new Check("PersonRig.Pose and Fight without turns set afresh or turns of nothing (a sixteenth as many inputs: each a whole body)", (int n) => CheckLean(n / 16)));
			Checks.Add(new Check("CreatureRig.Pose without missing parts' turns, turns set afresh or turns of nothing, sizes read once (a sixteenth as many inputs: each a whole creature)", (int n) => CheckCreature(n / 16)));
			Checks.Add(new Check("Turns: a turn of nothing left out of RotX, RotY, RotZ and their products (a quarter as many inputs: four turns each)", (int n) => CheckTurns(n / 4)));
			LiveChecks.Add(RigLive("RigTransform", RigTune.TransformCheck, "of them TransformDir", "-"));
			LiveChecks.Add(RigLive("RigPlant", RigTune.PlantCheck, "with a leg leant to hold its foot", "with a foot slid on out of reach"));
			LiveChecks.Add(RigLive("RigLean", RigTune.LeanCheck, "Fights in a guard", "of them mid-strike"));
		}

		private static LiveCheck RigLive(string name, RigTune.Tally t, string busy, string busier)
		{
			return new LiveCheck(name, delegate(bool on)
			{
				t.Start(on);
			}, delegate
			{
				Log.Info($"CITYPERF live {name}: {t.Calls} calls compared ({t.Busy} {busy}, {t.Busier} {busier}), {t.Differed} differed");
				if (t.Differed > 0)
				{
					return $"{t.Differed} of {t.Calls} calls differed; the first: {t.First}";
				}
				return (t.Calls <= 0) ? "never called" : null;
			});
		}

		private static List<MeshModel> LibraryModels()
		{
			List<MeshModel> list = new List<MeshModel>();
			if (typeof(Models).GetField("_library", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) is Dictionary<string, MeshModel> dictionary)
			{
				foreach (KeyValuePair<string, MeshModel> item in dictionary)
				{
					if (item.Value?.Parts != null && item.Value.Parts.Length != 0)
					{
						list.Add(item.Value);
					}
				}
			}
			list.Sort((MeshModel a, MeshModel b) => string.CompareOrdinal(a.Name, b.Name));
			return list;
		}

		private static bool IsPerson(MeshModel m)
		{
			if (m.PartIndex("upper_leg_L") >= 0 && m.PartIndex("lower_leg_L") >= 0 && m.PartIndex("upper_leg_R") >= 0)
			{
				return m.PartIndex("lower_leg_R") >= 0;
			}
			return false;
		}

		private static bool IsCreature(MeshModel m)
		{
			if (IsPerson(m))
			{
				return false;
			}
			string[] creatureParts = CreatureParts;
			foreach (string name in creatureParts)
			{
				if (m.PartIndex(name) >= 0)
				{
					return true;
				}
			}
			return false;
		}

		private static float Some(ref Rng rng, float lo, float hi)
		{
			if ((rng.Next() & 7) != 0L)
			{
				return lo + (hi - lo) * rng.Unit();
			}
			return Odd(ref rng);
		}

		private static float OrNone(ref Rng rng, float lo, float hi)
		{
			switch (rng.Next() % 6)
			{
			default:
				return Some(ref rng, lo, hi);
			case 1uL:
				return -0f;
			case 0uL:
				return 0f;
			}
		}

		private static float Odd(ref Rng rng)
		{
			return rng.Float((int)(rng.Next() % 64));
		}

		private static Vec3 OddVector(ref Rng rng)
		{
			return new Vec3(Odd(ref rng), Odd(ref rng), Odd(ref rng));
		}

		private static Mat3[] TurnPool(ref Rng rng)
		{
			Mat3[] array = new Mat3[4096];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = rng.Matrix(i);
			}
			return array;
		}

		private static Mat3 AnyTurn(ref Rng rng, Mat3[] pool, int i)
		{
			if ((rng.Next() & 0xF) != 0L)
			{
				return pool[rng.Next() & 0xFFF];
			}
			return rng.Matrix(i);
		}

		private static Vec3 AnyShift(ref Rng rng)
		{
			switch ((int)(rng.Next() % 8))
			{
			case 0:
				return OddVector(ref rng);
			case 1:
			case 2:
			case 3:
				return default(Vec3);
			default:
				return new Vec3(rng.Unit() * 0.2f - 0.1f, rng.Unit() * 0.2f - 0.1f, rng.Unit() * 0.2f - 0.1f);
			}
		}

		private static float AnySize(ref Rng rng)
		{
			switch (rng.Next() % 8)
			{
			default:
				return 0.5f + rng.Unit();
			case 1uL:
			case 2uL:
			case 3uL:
			case 4uL:
				return 1f;
			case 0uL:
				return Odd(ref rng);
			}
		}

		private static Mat3 AnyBasis(ref Rng rng, Mat3[] pool)
		{
			switch ((int)(rng.Next() % 4))
			{
			case 0:
				return pool[rng.Next() & 0xFFF];
			case 1:
				return new Mat3(OddVector(ref rng), OddVector(ref rng), OddVector(ref rng));
			default:
			{
				float num = 0.3f + 2f * rng.Unit();
				float num2 = rng.Unit() * 20f - 10f;
				float num3 = (float)Math.Cos(num2);
				float num4 = (float)Math.Sin(num2);
				return new Mat3(new Vec3(num3 * (0f - num), num4 * (0f - num), 0f), new Vec3((0f - num4) * num, num3 * num, 0f), new Vec3(0f, 0f, num));
			}
			}
		}

		private static Vec3 AnyPlace(ref Rng rng)
		{
			if ((rng.Next() & 3) != 0L)
			{
				return new Vec3(rng.Unit() * 512f, rng.Unit() * 512f, rng.Unit() * 4f);
			}
			return OddVector(ref rng);
		}

		private static Vec3 AnyPoint(ref Rng rng, MeshModel m)
		{
			if ((rng.Next() & 7) != 0L)
			{
				return new Vec3(m.Min.X + (m.Max.X - m.Min.X) * rng.Unit(), m.Min.Y + (m.Max.Y - m.Min.Y) * rng.Unit(), m.Min.Z + (m.Max.Z - m.Min.Z) * rng.Unit());
			}
			return OddVector(ref rng);
		}

		private static Arrays ArraysFor(Dictionary<int, Arrays> kept, int n)
		{
			if (!kept.TryGetValue(n, out var value))
			{
				Arrays obj = new Arrays
				{
					Rot = new Mat3[n],
					Off = new Vec3[n],
					Scale = new float[n]
				};
				value = obj;
				kept[n] = obj;
			}
			return value;
		}

		private static void Scramble(ref Rng rng, MeshInstance m, Mat3[] pool)
		{
			m.EnsurePose();
			for (int i = 0; i < m.PartRotation.Length; i++)
			{
				m.PartRotation[i] = pool[rng.Next() & 0xFFF];
				m.PartOffset[i] = AnyShift(ref rng);
				m.PartScale[i] = AnySize(ref rng);
			}
		}

		private static void CopyOnto(MeshInstance from, MeshInstance into)
		{
			into.Model = from.Model;
			into.Position = from.Position;
			into.Basis = from.Basis;
			int num = from.PartRotation.Length;
			if (into.PartRotation == null || into.PartRotation.Length != num)
			{
				into.PartRotation = new Mat3[num];
				into.PartOffset = new Vec3[num];
				into.PartScale = new float[num];
			}
			Array.Copy(from.PartRotation, into.PartRotation, num);
			Array.Copy(from.PartOffset, into.PartOffset, num);
			Array.Copy(from.PartScale, into.PartScale, num);
		}

		private static string CheckTransform(int n)
		{
			List<MeshModel> list = LibraryModels();
			if (list.Count == 0)
			{
				return "no models in the library";
			}
			Rng rng = new Rng(1592619265uL);
			Mat3[] pool = TurnPool(ref rng);
			Dictionary<int, Arrays> kept = new Dictionary<int, Arrays>();
			MeshInstance meshInstance = new MeshInstance();
			for (int i = 0; i < n; i++)
			{
				MeshModel meshModel = list[(int)(rng.Next() % (ulong)list.Count)];
				int num = meshModel.Parts.Length;
				Arrays arrays = ArraysFor(kept, num);
				ulong num2 = rng.Next();
				meshInstance.Model = meshModel;
				meshInstance.PartRotation = (((num2 & 7) == 0L) ? null : arrays.Rot);
				meshInstance.PartOffset = ((((num2 >> 3) & 3) == 0L) ? null : arrays.Off);
				meshInstance.PartScale = ((((num2 >> 5) & 3) == 0L) ? null : arrays.Scale);
				int num3 = ((((num2 >> 7) & 0x1F) == 0L) ? (-1) : ((int)((num2 >> 12) % (ulong)num)));
				for (int num4 = num3; num4 >= 0; num4 = meshModel.Parts[num4].Parent)
				{
					if (meshInstance.PartRotation != null)
					{
						meshInstance.PartRotation[num4] = AnyTurn(ref rng, pool, i);
					}
					if (meshInstance.PartOffset != null)
					{
						meshInstance.PartOffset[num4] = AnyShift(ref rng);
					}
					if (meshInstance.PartScale != null)
					{
						meshInstance.PartScale[num4] = AnySize(ref rng);
					}
				}
				meshInstance.Basis = AnyBasis(ref rng, pool);
				meshInstance.Position = AnyPlace(ref rng);
				Vec3 vec = AnyPoint(ref rng, meshModel);
				string text = Differs(PersonRig.TransformOld(meshInstance, num3, vec), PersonRig.TransformFast(meshInstance, num3, vec));
				if (text == null)
				{
					text = Differs(PersonRig.TransformDirOld(meshInstance, num3, vec), PersonRig.TransformDirFast(meshInstance, num3, vec), "TransformDir: ");
				}
				if (text != null)
				{
					return string.Format("input {0} ({1}, part {2}, point {3}, turns {4}, shifts {5}, sizes {6}): {7}", i, meshModel.Name, num3, RigTune.Show(vec), (meshInstance.PartRotation != null) ? "yes" : "none", (meshInstance.PartOffset != null) ? "yes" : "none", (meshInstance.PartScale != null) ? "yes" : "none", text);
				}
			}
			return null;
		}

		private static string CheckPlant(int n)
		{
			List<MeshModel> list = new List<MeshModel>();
			foreach (MeshModel item in LibraryModels())
			{
				if (IsPerson(item))
				{
					list.Add(item);
				}
			}
			if (list.Count == 0)
			{
				return "no people's models in the library";
			}
			Rng rng = new Rng(1592619521uL);
			Mat3[] pool = TurnPool(ref rng);
			MeshInstance meshInstance = new MeshInstance();
			MeshInstance meshInstance2 = new MeshInstance();
			WalkCycle.Walker walker = new WalkCycle.Walker();
			WalkCycle.Walker walker2 = new WalkCycle.Walker();
			WalkCycle.Walker walker3 = new WalkCycle.Walker();
			bool transform = RigTune.Transform;
			int num = 0;
			int num2 = 0;
			PersonRig personRig = null;
			try
			{
				for (int i = 0; i < n; i++)
				{
					if (i % 4 == 0)
					{
						MeshModel meshModel = list[(int)(rng.Next() % (ulong)list.Count)];
						personRig = Models.Rig(meshModel);
						meshInstance.Model = meshModel;
						if ((rng.Next() & 7) == 0L)
						{
							Scramble(ref rng, meshInstance, pool);
						}
						else
						{
							walker3.Lift = rng.Unit();
							walker3.TwistL = rng.Unit() - 0.5f;
							walker3.TwistR = rng.Unit() - 0.5f;
							personRig.PoseOld(meshInstance, Some(ref rng, 0f, 1f), Some(ref rng, -20f, 20f), 0f, Some(ref rng, 0f, 1000f), Some(ref rng, -1f, 1f), Some(ref rng, -0.5f, 0.5f), Some(ref rng, 0f, 1f), ((rng.Next() & 1) == 0L) ? walker3 : null);
							if ((rng.Next() & 3) == 0L)
							{
								personRig.FightOld(meshInstance, (WeaponKind)(rng.Next() % 12), Some(ref rng, 0f, 1f), Some(ref rng, 0f, 1f), ((rng.Next() & 3) == 0L) ? rng.Unit() : 0f, 0f, ((rng.Next() & 1) == 0L) ? 1f : (-1f));
							}
						}
					}
					meshInstance.Basis = AnyBasis(ref rng, pool);
					meshInstance.Position = AnyPlace(ref rng);
					walker.Phase = Some(ref rng, -40f, 40f);
					walker.Run = Some(ref rng, 0f, 1f);
					float leg = 0.5f * (meshInstance.Model.Max.Z - meshInstance.Model.Min.Z) * Math.Abs(meshInstance.Basis.C2.Z);
					walker.FootL = Foot(ref rng, personRig.SoleWorld(meshInstance, left: true), leg);
					walker.FootR = Foot(ref rng, personRig.SoleWorld(meshInstance, left: false), leg);
					CopyOnto(meshInstance, meshInstance2);
					walker2.Phase = walker.Phase;
					walker2.Run = walker.Run;
					walker2.FootL = walker.FootL;
					walker2.FootR = walker.FootR;
					WalkCycle.Foot footL = walker.FootL;
					WalkCycle.Foot footR = walker.FootR;
					Mat3 a = meshInstance.PartRotation[personRig.UpperLegL];
					Mat3 a2 = meshInstance.PartRotation[personRig.UpperLegR];
					RigTune.Transform = false;
					personRig.PlantOld(meshInstance, walker);
					RigTune.Transform = transform;
					personRig.PlantFast(meshInstance2, walker2);
					string text = FootDiffers("left foot", walker.FootL, walker2.FootL) ?? FootDiffers("right foot", walker.FootR, walker2.FootR) ?? RigTune.PoseDiffers(meshInstance, meshInstance2);
					if (text != null)
					{
						return $"input {i} ({meshInstance.Model.Name}, phase {Show(walker2.Phase)}, run {Show(walker2.Run)}, feet {Show(footL.X)},{Show(footL.Y)} {footL.Down} / {Show(footR.X)},{Show(footR.Y)} {footR.Down}): {text}";
					}
					if (!RigTune.Same(a, meshInstance.PartRotation[personRig.UpperLegL]) || !RigTune.Same(a2, meshInstance.PartRotation[personRig.UpperLegR]))
					{
						num++;
					}
					if ((footL.Down && walker.FootL.Down && (Bits(footL.X) != Bits(walker.FootL.X) || Bits(footL.Y) != Bits(walker.FootL.Y))) || (footR.Down && walker.FootR.Down && (Bits(footR.X) != Bits(walker.FootR.X) || Bits(footR.Y) != Bits(walker.FootR.Y))))
					{
						num2++;
					}
					CopyOnto(meshInstance2, meshInstance);
				}
			}
			finally
			{
				RigTune.Transform = transform;
			}
			Log.Info($"CITYPERF check PersonRig.Plant: {n} bodies, {num} with a leg leant to hold its foot, {num2} with a foot moved on");
			if (num != 0)
			{
				return null;
			}
			return "no leg was ever leant: the check didn't reach the work";
		}

		private static WalkCycle.Foot Foot(ref Rng rng, Vec3 sole, float leg)
		{
			WalkCycle.Foot result = new WalkCycle.Foot
			{
				Down = true,
				X = sole.X,
				Y = sole.Y
			};
			switch ((int)(rng.Next() % 16))
			{
			case 0:
			case 1:
				result.Down = false;
				break;
			case 2:
				result.X += 1E-07f;
				break;
			case 3:
			case 4:
				result.X += leg * (0.65f + rng.Unit());
				break;
			case 5:
				result.X = Odd(ref rng);
				result.Y = Odd(ref rng);
				break;
			default:
			{
				float num = leg * 0.6f * rng.Unit() * rng.Unit();
				float num2 = rng.Unit() * (MathF.PI * 2f);
				result.X += num * (float)Math.Cos(num2);
				result.Y += num * (float)Math.Sin(num2);
				break;
			}
			}
			return result;
		}

		private static string FootDiffers(string which, WalkCycle.Foot old, WalkCycle.Foot now)
		{
			if (old.Down != now.Down)
			{
				return which + ": down " + old.Down + " vs " + now.Down;
			}
			if (Bits(old.X) != Bits(now.X))
			{
				return which + ": X " + Show(old.X) + " vs " + Show(now.X);
			}
			if (Bits(old.Y) != Bits(now.Y))
			{
				return which + ": Y " + Show(old.Y) + " vs " + Show(now.Y);
			}
			return null;
		}

		private static string CheckLean(int n)
		{
			List<MeshModel> list = new List<MeshModel>();
			foreach (MeshModel item in LibraryModels())
			{
				if (IsPerson(item))
				{
					list.Add(item);
				}
			}
			if (list.Count == 0)
			{
				return "no people's models in the library";
			}
			Rng rng = new Rng(1592619777uL);
			Mat3[] pool = TurnPool(ref rng);
			MeshInstance meshInstance = new MeshInstance();
			MeshInstance meshInstance2 = new MeshInstance();
			WalkCycle.Walker walker = new WalkCycle.Walker();
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < n; i++)
			{
				MeshModel meshModel = list[(int)(rng.Next() % (ulong)list.Count)];
				PersonRig personRig = Models.Rig(meshModel);
				meshInstance.Model = (meshInstance2.Model = meshModel);
				Scramble(ref rng, meshInstance, pool);
				Scramble(ref rng, meshInstance2, pool);
				float num3 = OrNone(ref rng, 0f, 1f);
				float num4 = Some(ref rng, -20f, 20f);
				float num5 = (((rng.Next() & 3) == 0L) ? Some(ref rng, 0f, 1f) : 0f);
				float breath = Some(ref rng, 0f, 1000f);
				float headTurn = OrNone(ref rng, -1f, 1f);
				float headNod = OrNone(ref rng, -0.5f, 0.5f);
				float num6 = Some(ref rng, -0.2f, 1.2f);
				WalkCycle.Walker walker2 = null;
				if ((rng.Next() & 1) == 0L)
				{
					walker2 = walker;
					walker.Lift = OrNone(ref rng, 0f, 1f);
					walker.TwistL = OrNone(ref rng, -0.7f, 0.7f);
					walker.TwistR = OrNone(ref rng, -0.7f, 0.7f);
				}
				personRig.PoseOld(meshInstance, num3, num4, num5, breath, headTurn, headNod, num6, walker2);
				personRig.PoseLean(meshInstance2, num3, num4, num5, breath, headTurn, headNod, num6, walker2);
				string text = RigTune.PoseDiffers(meshInstance, meshInstance2);
				if (text != null)
				{
					return $"Pose, input {i} ({meshModel.Name}, stride {Show(num3)}, phase {Show(num4)}, swing {Show(num5)}, run {Show(num6)}, walker {walker2 != null}): {text}";
				}
				WeaponKind weaponKind = (WeaponKind)(rng.Next() % 12);
				ulong num7 = rng.Next();
				float num8 = (((num7 & 1) == 0L) ? rng.Unit() : (((num7 & 6) == 0L) ? Odd(ref rng) : (((num7 & 6) == 2) ? 0f : (((num7 & 6) == 4) ? 1f : rng.Unit()))));
				float num9 = ((((num7 >> 3) & 3) == 0L) ? 0f : ((((num7 >> 3) & 3) == 1) ? Odd(ref rng) : (0.05f + rng.Unit())));
				float num10 = ((((num7 >> 5) & 3) == 0L) ? rng.Unit() : ((((num7 >> 5) & 7) == 1) ? Odd(ref rng) : 0f));
				float num11 = ((((num7 >> 8) & 3) == 0L) ? rng.Unit() : 0f);
				float num12 = ((((num7 >> 10) & 7) == 0L) ? Odd(ref rng) : ((((num7 >> 13) & 1) == 0L) ? 1f : (-1f)));
				if (((num7 >> 14) & 3) == 0L)
				{
					Scramble(ref rng, meshInstance, pool);
					CopyOnto(meshInstance, meshInstance2);
				}
				personRig.FightOld(meshInstance, weaponKind, num8, num9, num10, num11, num12);
				personRig.FightLean(meshInstance2, weaponKind, num8, num9, num10, num11, num12);
				text = RigTune.PoseDiffers(meshInstance, meshInstance2);
				if (text != null)
				{
					return $"Fight, input {i} ({meshModel.Name}, {weaponKind}, attack {Show(num8)}, stance {Show(num9)}, flinch {Show(num10)}, lean {Show(num11)}, side {Show(num12)}): {text}";
				}
				if (num9 > 0f)
				{
					num++;
					if (num8 > 0f && num8 < 1f)
					{
						num2++;
					}
				}
			}
			Log.Info($"CITYPERF check PersonRig.Pose and Fight: {n} bodies, {num} in a guard, {num2} of them mid-strike");
			return null;
		}

		private static string CheckCreature(int n)
		{
			List<MeshModel> list = new List<MeshModel>();
			foreach (MeshModel item in LibraryModels())
			{
				if (IsCreature(item))
				{
					list.Add(item);
				}
			}
			if (list.Count == 0)
			{
				return "no creatures' models in the library";
			}
			bool creature = RigTune.Creature;
			try
			{
				foreach (MeshModel item2 in LibraryModels())
				{
					Creatures.ShipInfo shipInfo = Creatures.ShipInfoOf(item2);
					float f = item2.InfoFloat("length", item2.Max.Y - item2.Min.Y);
					float f2 = item2.InfoFloat("ghostly", 0f);
					if (Bits(f) != Bits(shipInfo.Length) || Bits(f2) != Bits(shipInfo.Ghostly))
					{
						return item2.Name + ": ship length " + Show(f) + " vs " + Show(shipInfo.Length) + ", ghostly " + Show(f2) + " vs " + Show(shipInfo.Ghostly);
					}
				}
				foreach (MeshModel item3 in list)
				{
					CreatureRig creatureRig = Models.CreatureRigFor(item3);
					RigTune.Creature = false;
					float height = creatureRig.Height;
					float length = creatureRig.Length;
					float legLength = creatureRig.LegLength;
					RigTune.Creature = true;
					if (Bits(height) != Bits(creatureRig.Height) || Bits(length) != Bits(creatureRig.Length) || Bits(legLength) != Bits(creatureRig.LegLength))
					{
						return item3.Name + ": height " + Show(height) + " vs " + Show(creatureRig.Height) + ", length " + Show(length) + " vs " + Show(creatureRig.Length) + ", legs " + Show(legLength) + " vs " + Show(creatureRig.LegLength);
					}
				}
				Rng rng = new Rng(1592620033uL);
				Mat3[] pool = TurnPool(ref rng);
				MeshInstance meshInstance = new MeshInstance();
				MeshInstance meshInstance2 = new MeshInstance();
				int num = 0;
				int num2 = 0;
				for (int i = 0; i < n; i++)
				{
					MeshModel meshModel = list[(int)(rng.Next() % (ulong)list.Count)];
					CreatureRig creatureRig2 = Models.CreatureRigFor(meshModel);
					meshInstance.Model = (meshInstance2.Model = meshModel);
					Scramble(ref rng, meshInstance, pool);
					Scramble(ref rng, meshInstance2, pool);
					ulong num3 = rng.Next();
					float num4 = (((num3 & 3) == 0L) ? Odd(ref rng) : (((num3 & 3) == 1) ? (rng.Unit() * 2f) : (((num3 & 0xC) == 0L) ? 1f : OrNone(ref rng, 0f, 1f))));
					float num5 = Some(ref rng, -40f, 40f);
					float num6 = Some(ref rng, 0f, 10000f);
					float num7 = Some(ref rng, 0f, 11f);
					bool flag = ((num3 >> 2) & 1) == 0;
					float num8 = ((((num3 >> 3) & 1) == 0L) ? 0f : ((((num3 >> 4) & 3) == 0L) ? Odd(ref rng) : rng.Unit()));
					RigTune.Creature = false;
					creatureRig2.PoseOld(meshInstance, num4, num5, num6, flag, num8, num7);
					RigTune.Creature = true;
					creatureRig2.PoseFast(meshInstance2, num4, num5, num6, flag, num8, num7);
					string text = RigTune.PoseDiffers(meshInstance, meshInstance2);
					if (text != null)
					{
						return $"input {i} ({meshModel.Name}, kind {creatureRig2.Kind}, stride {Show(num4)}, phase {Show(num5)}, time {Show(num6)}, flying {flag}, attack {Show(num8)}, seed {Show(num7)}): {text}";
					}
					if (flag)
					{
						num++;
					}
					if (num8 > 0f)
					{
						num2++;
					}
				}
				Log.Info($"CITYPERF check CreatureRig.Pose: {list.Count} models, {n} poses ({num} flying, {num2} striking)");
			}
			finally
			{
				RigTune.Creature = creature;
			}
			return null;
		}

		private static float Angle(ref Rng rng)
		{
			switch ((int)(rng.Next() % 8))
			{
			case 0:
				return 0f;
			case 1:
				return -0f;
			case 2:
			case 3:
				return Odd(ref rng);
			default:
				return rng.Random();
			}
		}

		private static string CheckTurns(int n)
		{
			float[] array = new float[2] { 0f, -0f };
			foreach (float num in array)
			{
				string text = Differs(Mat3.RotX(num), Turns.Ident) ?? Differs(Mat3.RotY(num), Turns.Ident) ?? Differs(Mat3.RotZ(num), Turns.Ident) ?? Differs(Mat3.Identity, Turns.Ident);
				if (text != null)
				{
					return "a turn by " + Show(num) + " isn't the identity: " + text;
				}
			}
			Rng rng = new Rng(1592620289uL);
			for (int j = 0; j < n; j++)
			{
				float num2 = Angle(ref rng);
				float num3 = Angle(ref rng);
				string text2 = Differs(Mat3.RotZ(num2) * Mat3.RotX(num3), Turns.ZX(num2, num3));
				if (text2 == null)
				{
					text2 = Differs(Mat3.RotX(num2) * Mat3.RotY(num3), Turns.XY(num2, num3));
				}
				if (text2 == null)
				{
					text2 = Differs(Mat3.RotX(num2), Turns.X(num2));
				}
				if (text2 == null)
				{
					text2 = Differs(Mat3.RotY(num3), Turns.Y(num3));
				}
				if (text2 != null)
				{
					return $"input {j} ({Show(num2)}, {Show(num3)}): {text2}";
				}
			}
			return null;
		}

		private static void RegisterUnits()
		{
			Switches.Add(new Switch("SpriteMemo", () => SceneCollector.SpriteMemo, delegate(bool v)
			{
				SceneCollector.SpriteMemo = v;
			}));
			Switches.Add(new Switch("UnitChecks", () => SceneCollector.UnitChecks, delegate(bool v)
			{
				SceneCollector.UnitChecks = v;
			}));
			Switches.Add(new Switch("DockBoxes", () => DockDecks.Boxes, delegate(bool v)
			{
				DockDecks.Boxes = v;
			}));
			Switches.Add(new Switch("BuildingInfo", () => SceneCollector.BuildingInfo, delegate(bool v)
			{
				SceneCollector.BuildingInfo = v;
			}));
			Switches.Add(new Switch("PossessedFirst", () => OwnMind.PossessedFirst, delegate(bool v)
			{
				OwnMind.PossessedFirst = v;
			}));
			Checks.Add(new Check("DockBoxes: a point outside the boxes is beyond every dock's reach", CheckDockBoxes));
			Checks.Add(new Check("UnitChecks: creature kinds from the memo as the four tests have them", CheckKinds));
			Checks.Add(new Check("BuildingInfo: numbers parsed once, names made once", CheckInfo));
			Checks.Add(new Check("SpriteMemo: images from the memo as SpriteCache.Get gives them", CheckSpriteMemo));
			Checks.Add(new Check("units switches: the frame's billboards and colliders", CheckFrameExtras));
			LiveChecks.Add(new LiveCheck("SpriteMemo", delegate(bool on)
			{
				if (on)
				{
					_spriteMemoWas = SceneCollector.SpriteMemo;
					SceneCollector.SpriteMemo = true;
				}
				else if (SceneCollector.VerifySprites)
				{
					SceneCollector.SpriteMemo = _spriteMemoWas;
				}
				SceneCollector.VerifySprites = on;
				if (on)
				{
					SceneCollector.SpriteCompared = (SceneCollector.SpriteDiffered = 0);
					SceneCollector.SpriteFirst = null;
				}
			}, () => Verdict(SceneCollector.SpriteCompared, SceneCollector.SpriteDiffered, SceneCollector.SpriteFirst, "SpriteMemo: memo hits")));
			LiveChecks.Add(new LiveCheck("UnitChecks", delegate(bool on)
			{
				SceneCollector.VerifyKinds = on;
				if (on)
				{
					SceneCollector.KindCompared = (SceneCollector.KindDiffered = 0);
					SceneCollector.KindFirst = null;
				}
			}, () => Verdict(SceneCollector.KindCompared, SceneCollector.KindDiffered, SceneCollector.KindFirst, "UnitChecks: kinds and hit ages")));
			LiveChecks.Add(new LiveCheck("DockBoxes", delegate(bool on)
			{
				if (on)
				{
					_dockBoxesWas = DockDecks.Boxes;
					DockDecks.Boxes = true;
				}
				else if (DockDecks.VerifyBoxes)
				{
					DockDecks.Boxes = _dockBoxesWas;
				}
				DockDecks.VerifyBoxes = on;
				if (on)
				{
					DockDecks.BoxCompared = (DockDecks.BoxDiffered = (DockDecks.BoxFresh = 0));
					DockDecks.BoxFirst = null;
				}
			}, () => Verdict(DockDecks.BoxCompared + DockDecks.BoxFresh, DockDecks.BoxDiffered, DockDecks.BoxFirst, $"DockBoxes: looks ({DockDecks.BoxCompared} points skipped, scanned again; {DockDecks.BoxFresh} looks with the boxes made afresh, {DockDecks.Count} docks)")));
			LiveChecks.Add(new LiveCheck("BuildingInfo", delegate(bool on)
			{
				SceneCollector.VerifyInfo = on;
				if (on)
				{
					SceneCollector.InfoCompared = (SceneCollector.InfoDiffered = 0);
					SceneCollector.InfoFirst = null;
				}
			}, () => Verdict(SceneCollector.InfoCompared, SceneCollector.InfoDiffered, SceneCollector.InfoFirst, "BuildingInfo: numbers and names")));
			LiveChecks.Add(new LiveCheck("PossessedFirst", delegate(bool on)
			{
				OwnMind.Verify = on;
				if (on)
				{
					OwnMind.Compared = (OwnMind.Differed = 0);
					OwnMind.First = null;
				}
			}, () => Verdict(OwnMind.Compared, OwnMind.Differed, OwnMind.First, "PossessedFirst: answers")));
		}

		private static string Verdict(int compared, int differed, string first, string what)
		{
			Log.Info($"CITYPERF units live: {compared} {what} compared, {differed} differ");
			if (differed > 0)
			{
				return $"{differed} of {compared} {what} differ; the first: {first}";
			}
			if (compared != 0)
			{
				return null;
			}
			return "nothing compared";
		}

		private static string CheckDockBoxes(int n)
		{
			Rng rng = new Rng(1592590593uL);
			float[] boxes = new float[4];
			int num = 0;
			int num2 = 0;
			while (num2 < n)
			{
				ulong num3 = rng.Next() % 8;
				if (num3 > 2)
				{
					goto IL_007a;
				}
				switch (num3)
				{
				case 0uL:
					break;
				case 1uL:
					goto IL_0053;
				case 2uL:
					goto IL_005c;
				default:
					goto IL_007a;
				}
				float num4 = rng.Float(num2);
				goto IL_0089;
				IL_0053:
				num4 = 0f;
				goto IL_0089;
				IL_0089:
				float num5 = num4;
				float num6 = ((rng.Next() % 16 == 0L) ? rng.Float(num2 + 1) : ((rng.Unit() * 2f - 1f) * 3000f));
				float num7 = ((rng.Next() % 16 == 0L) ? rng.Float(num2 + 2) : ((rng.Unit() * 2f - 1f) * 3000f));
				float num10;
				float num11;
				switch ((int)(rng.Next() % 6))
				{
				case 0:
					num10 = rng.Float(num2 + 3);
					num11 = rng.Float(num2 + 5);
					break;
				case 1:
					num10 = num6 + (rng.Unit() * 2f - 1f) * 200f;
					num11 = num7 + (rng.Unit() * 2f - 1f) * 200f;
					break;
				default:
				{
					float num8 = (num5 + 1f + (rng.Unit() - 0.5f) * 0.1f) * ((rng.Unit() < 0.5f) ? (-1f) : 1f);
					float num9 = (rng.Unit() * 2f - 1f) * (num5 + 1f);
					if (rng.Unit() < 0.5f)
					{
						num10 = num6 + num8;
						num11 = num7 + num9;
					}
					else
					{
						num10 = num6 + num9;
						num11 = num7 + num8;
					}
					break;
				}
				}
				DockDecks.BoxOf(num6, num7, num5, boxes, 0);
				if (DockDecks.BoxesSkip(boxes, num10, num11))
				{
					num++;
					if (!DockDecks.BeyondForTest(num10, num11, num6, num7, num5))
					{
						return "dock (" + Show(num6) + ", " + Show(num7) + ") reach " + Show(num5) + ", point (" + Show(num10) + ", " + Show(num11) + "): the boxes skip it, the scan doesn't";
					}
				}
				num2++;
				continue;
				IL_007a:
				num4 = rng.Unit() * 60f;
				goto IL_0089;
				IL_005c:
				num4 = ((rng.Unit() < 0.5f) ? 99999.99f : 100000f);
				goto IL_0089;
			}
			if (num <= 0)
			{
				return "no point was ever skipped";
			}
			return null;
		}

		private static string CheckKinds(int n)
		{
			List<ActorAsset> list = new List<ActorAsset>();
			try
			{
				if (AssetManager.actor_library != null)
				{
					list.AddRange(AssetManager.actor_library.list);
				}
			}
			catch
			{
			}
			string[] array = new string[13]
			{
				"crabzilla", "dragon", "zombie_dragon", "god_finger", "UFO", "human", "", null, "Dragon", "crabzilla ",
				"god_finger_2", "zombie_dragon2", "sheep"
			};
			ActorAsset[] array2 = new ActorAsset[6];
			for (int i = 0; i < array2.Length; i++)
			{
				array2[i] = new ActorAsset
				{
					id = array[i % array.Length]
				};
			}
			Actor actor = (Actor)FormatterServices.GetUninitializedObject(typeof(Actor));
			SceneCollector.UnitMemo[] array3 = new SceneCollector.UnitMemo[4];
			Rng rng = new Rng(1592590594uL);
			for (int j = 0; j < n; j++)
			{
				ulong num = rng.Next();
				if (num % 7 == 0L)
				{
					ActorAsset actorAsset = array2[(uint)((num >> 8) % (ulong)array2.Length)];
					string text = array[(uint)((num >> 16) % (ulong)array.Length)];
					switch ((int)((num >> 24) % 3))
					{
					case 0:
						actorAsset.id = ((text != null) ? new string(text.ToCharArray()) : null);
						break;
					case 1:
						actorAsset.id = text;
						break;
					default:
						actorAsset.flag_ufo = !actorAsset.flag_ufo;
						break;
					}
				}
				int num2 = (int)((num >> 32) % (ulong)(list.Count + array2.Length + 1));
				actor.asset = ((num2 < list.Count) ? list[num2] : ((num2 < list.Count + array2.Length) ? array2[num2 - list.Count] : null));
				int slot = (int)((num >> 40) % (ulong)array3.Length);
				int num3 = SceneCollector.KindOldForTest(actor);
				int num4 = SceneCollector.KindNewForTest(actor, array3, slot);
				if (num3 != num4)
				{
					return string.Format("step {0}: kind {1} ufo {2}: memo {3} vs the tests {4}", j, (actor.asset != null) ? (actor.asset.id ?? "(null id)") : "(no asset)", actor.asset != null && actor.asset.flag_ufo, num4, num3);
				}
			}
			if (list.Count <= 0)
			{
				return "no creature kinds to try";
			}
			return null;
		}

		private static string CheckInfo(int n)
		{
			Rng rng = new Rng(1592590595uL);
			MeshModel meshModel = new MeshModel
			{
				Name = "check"
			};
			MeshModel meshModel2 = new MeshModel
			{
				Name = "bare"
			};
			SceneCollector.InfoMemo m = default(SceneCollector.InfoMemo);
			string[] array = new string[6] { "house_human_1", "hall_elf_2", "tw_house_orc_1", "", null, "docks_human" };
			string of = null;
			string name = null;
			string of2 = null;
			string name2 = null;
			for (int i = 0; i < n; i++)
			{
				ulong num = rng.Next();
				switch ((int)(num % 8))
				{
				case 0:
				{
					string text4;
					switch ((int)((num >> 8) % 4))
					{
					case 0:
						text4 = OddNumbers[(uint)((num >> 16) % (ulong)OddNumbers.Length)];
						break;
					case 1:
						text4 = rng.Float(i).ToString("R", CultureInfo.InvariantCulture);
						break;
					case 2:
						text4 = rng.Random().ToString("E3", CultureInfo.InvariantCulture);
						break;
					default:
						text4 = (rng.Unit() * 8f).ToString("F2", CultureInfo.InvariantCulture);
						break;
					}
					meshModel.ParseInfo("width=" + text4);
					break;
				}
				case 1:
				{
					string text = array[(uint)((num >> 8) % (ulong)array.Length)];
					if (text != null && (num & 0x10000) != 0L)
					{
						text = new string(text.ToCharArray());
					}
					bool num3 = (num & 0x20000) != 0;
					string text2 = (num3 ? SceneCollector.RuinName(ref of, ref name, text) : SceneCollector.SiteName(ref of2, ref name2, text));
					string text3 = (num3 ? "ruin_" : "site_") + text;
					if (!string.Equals(text2, text3, StringComparison.Ordinal))
					{
						return $"step {i}: name \"{text2}\" vs \"{text3}\"";
					}
					break;
				}
				case 2:
				{
					int num4 = (int)((num >> 8) % 4);
					float num5 = (((num & 0x100000) != 0L) ? rng.Float(i) : (rng.Unit() * 120f));
					MeshModel meshModel4 = SceneCollector.CropModelNamed(num4, num5);
					MeshModel meshModel5 = SceneCollector.CropModelOldForTest(num4, num5);
					if (meshModel4 != meshModel5)
					{
						return string.Format("step {0}: crop stage {1} at {2}: {3} vs {4}", i, num4, Show(num5), (meshModel4 != null) ? meshModel4.Name : "null", (meshModel5 != null) ? meshModel5.Name : "null");
					}
					break;
				}
				default:
				{
					MeshModel meshModel3 = (((num & 0x100) != 0L) ? meshModel : (((num & 0x200) != 0L) ? meshModel2 : meshModel));
					float num2 = rng.Float(i);
					float f = SceneCollector.InfoFloat(ref m, meshModel3, "width", num2);
					float f2 = meshModel3.InfoFloat("width", num2);
					if (Bits(f) != Bits(f2))
					{
						return string.Format("step {0}: width \"{1}\" fallback {2}: memo {3} vs {4}", i, meshModel3.Info("width"), Show(num2), Show(f), Show(f2));
					}
					break;
				}
				}
			}
			return null;
		}

		private static string CheckSpriteMemo(int n)
		{
			Texture2D texture2D = new Texture2D(8, 8, TextureFormat.RGBA32, mipChain: false);
			Color32[] array = new Color32[64];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = new Color32((byte)(i * 4), 200, (byte)(255 - i), byte.MaxValue);
			}
			texture2D.SetPixels32(array);
			texture2D.Apply();
			Sprite[] array2 = new Sprite[24];
			try
			{
				for (int j = 0; j < array2.Length; j++)
				{
					array2[j] = Sprite.Create(texture2D, new Rect(j % 4, j / 4 % 4, 1 + j % 4, 1 + j / 4 % 4), new Vector2(0.5f, 0f), 1f);
				}
				SpriteCache spriteCache = new SpriteCache();
				spriteCache.BeginFrame();
				Sprite[] array3 = new Sprite[8];
				SpriteImage[] array4 = new SpriteImage[8];
				int[] array5 = new int[8];
				Rng rng = new Rng(1592590596uL);
				int num = 0;
				int num2 = 0;
				int num3 = Math.Min(n, 400000);
				for (int k = 0; k < num3; k++)
				{
					ulong num4 = rng.Next();
					if (num4 % 997 == 0L)
					{
						spriteCache.Clear();
						num2++;
						continue;
					}
					int num5 = (int)((num4 >> 8) % 8);
					Sprite sprite = (((num4 >> 16) % 64 == 0L) ? null : array2[(uint)((num4 >> 24) % (ulong)array2.Length)]);
					bool flag = sprite != null && (object)array3[num5] == sprite;
					SpriteImage spriteImage = SceneCollector.MemoImage(ref array3[num5], ref array4[num5], ref array5[num5], sprite, spriteCache);
					SpriteImage spriteImage2 = spriteCache.Get(sprite);
					if (flag && spriteImage == spriteImage2)
					{
						num++;
					}
					if (spriteImage != spriteImage2)
					{
						return $"step {k} (after {num2} clears): sprite {Array.IndexOf(array2, sprite)} slot {num5}: memo {Id(spriteImage)} vs the cache's {Id(spriteImage2)}";
					}
				}
				return (num > 0 && num2 > 0) ? null : $"too little tried: {num} memo hits, {num2} clears";
			}
			finally
			{
				Sprite[] array6 = array2;
				foreach (Sprite sprite2 in array6)
				{
					if (sprite2 != null)
					{
						UnityEngine.Object.Destroy(sprite2);
					}
				}
				UnityEngine.Object.Destroy(texture2D);
			}
		}

		private static string Id(object o)
		{
			if (o != null)
			{
				return "#" + RuntimeHelpers.GetHashCode(o);
			}
			return "null";
		}

		private static string CheckFrameExtras(int n)
		{
			WorldBoxMod mod = WorldBoxMod.Instance;
			if (mod == null || !mod.IsFirstPerson)
			{
				return "not in first person";
			}
			SceneCollector sceneForTest = mod.SceneForTest;
			string[] mine = new string[5] { "SpriteMemo", "UnitChecks", "DockBoxes", "BuildingInfo", "PossessedFirst" };
			bool[] array = Save();
			bool paused = Config.paused;
			UnityEngine.Random.State random = UnityEngine.Random.state;
			bool flag = !PoseClock.Frozen;
			if (flag)
			{
				PoseClock.Freeze(PoseClock.Now);
			}
			object ships = null;
			object feet = null;
			try
			{
				Config.paused = true;
				for (int i = 0; i < 4; i++)
				{
					if (!Gather(null))
					{
						return "not a plain outdoor frame";
					}
				}
				ships = Creatures.HoldShipsForTest();
				feet = People.HoldFeetForTest();
				int num = 0;
				int num2 = 0;
				int num3 = 0;
				string[] array2 = new string[6] { "SpriteMemo", "UnitChecks", "DockBoxes", "BuildingInfo", "PossessedFirst", "*" };
				foreach (string text in array2)
				{
					string text2 = ((text == "*") ? "all on" : (text + " on"));
					int num4 = 0;
					while (true)
					{
						Gather(null);
						List<uint[]> list = Extras(sceneForTest);
						num = sceneForTest.Billboards.Count;
						num2 = Colliders.Near.Count;
						Gather(text);
						List<uint[]> a = Extras(sceneForTest);
						Gather(null);
						List<uint[]> b = Extras(sceneForTest);
						if (Same(a, list) || Same(a, b))
						{
							break;
						}
						if (Same(list, b))
						{
							return text2 + ": " + Differ(a, list, num) + " (the frame on either side the same)";
						}
						num3++;
						if (num4 == 3)
						{
							return text2 + ": " + Differ(a, list, num) + ", and the frame kept drifting round it";
						}
						num4++;
					}
				}
				Log.Info($"CITYPERF units extras: {num} billboards and {num2} colliders the same to the bit with each switch on ({num3} gatherings again where the frame drifted)");
				return (num + num2 > 0) ? null : "nothing to compare";
			}
			finally
			{
				if (ships != null)
				{
					Creatures.PutBackShipsForTest(ships);
				}
				if (feet != null)
				{
					People.PutBackFeetForTest(feet);
				}
				Restore(array);
				UnityEngine.Random.state = random;
				if (flag)
				{
					PoseClock.Thaw();
				}
				Config.paused = paused;
			}
			bool Gather(string only)
			{
				foreach (Switch @switch in Switches)
				{
					@switch.Set((only == "*") ? (Array.IndexOf(mine, @switch.Name) >= 0) : (@switch.Name == only));
				}
				UnityEngine.Random.state = random;
				if (ships != null)
				{
					Creatures.PutBackShipsForTest(ships);
				}
				if (feet != null)
				{
					People.PutBackFeetForTest(feet);
				}
				return mod.CollectSceneForTest(0f);
			}
		}

		private static bool Same(List<uint[]> a, List<uint[]> b)
		{
			if (a.Count != b.Count)
			{
				return false;
			}
			for (int i = 0; i < a.Count; i++)
			{
				if (!Same(a[i], b[i]))
				{
					return false;
				}
			}
			return true;
		}

		private static string Differ(List<uint[]> a, List<uint[]> b, int billboards)
		{
			if (a.Count != b.Count)
			{
				return $"{a.Count} vs {b.Count} billboards and colliders";
			}
			for (int i = 0; i < a.Count; i++)
			{
				if (!Same(a[i], b[i]))
				{
					return ((i < billboards) ? ("billboard " + i) : ("collider " + (i - billboards))) + " differs";
				}
			}
			return "the same";
		}

		private static bool Same(uint[] a, uint[] b)
		{
			if (a.Length != b.Length)
			{
				return false;
			}
			for (int i = 0; i < a.Length; i++)
			{
				if (a[i] != b[i])
				{
					return false;
				}
			}
			return true;
		}

		private static List<uint[]> Extras(SceneCollector scene)
		{
			List<uint[]> list = new List<uint[]>(scene.Billboards.Count + Colliders.Near.Count);
			foreach (Billboard billboard in scene.Billboards)
			{
				list.Add(new uint[14]
				{
					Bits(billboard.X),
					Bits(billboard.Y),
					Bits(billboard.Z),
					Bits(billboard.GroundZ),
					Bits(billboard.Width),
					Bits(billboard.Height),
					Ref(billboard.Image),
					(uint)billboard.Kind,
					Bits(billboard.Angle),
					(billboard.Flip ? 1u : 0u) | (uint)(billboard.Shadow ? 2 : 0) | (uint)(billboard.Emissive ? 4 : 0) | (uint)(billboard.Far ? 8 : 0) | (uint)(billboard.Additive ? 16 : 0),
					billboard.Flash,
					billboard.Multiply,
					(uint)billboard.Tag,
					billboard.Alpha
				});
			}
			foreach (Collider item in Colliders.Near)
			{
				list.Add(new uint[11]
				{
					Bits(item.X),
					Bits(item.Y),
					Bits(item.Radius),
					Bits(item.HalfW),
					Bits(item.HalfD),
					Ref(item.Shape),
					Bits(item.Cos),
					Bits(item.Sin),
					Bits(item.Scale),
					(item.Box ? 1u : 0u) | (uint)(item.Mirror ? 2 : 0) | (uint)(item.Mobile ? 4 : 0),
					Ref(item.Owner)
				});
			}
			return list;
		}

		private static uint Ref(object o)
		{
			if (o != null)
			{
				return (uint)RuntimeHelpers.GetHashCode(o);
			}
			return 0u;
		}
	}
}
