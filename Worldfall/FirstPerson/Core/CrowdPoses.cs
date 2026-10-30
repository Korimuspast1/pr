using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class CrowdPoses
	{
		public const int Steps = 32;

		public const int WalkRow = 0;

		public const int RunRow = 33;

		public const int IdleRow = 66;

		public const int VariantRows = 68;

		public const int Adult = 0;

		public const int Elder = 1;

		public const int BigHead = 2;

		public const int Variants = 3;

		public const int Strikes = 16;

		public const int StrikeRows = 17;

		public static readonly WeaponKind[] WorkKinds = new WeaponKind[3]
		{
			WeaponKind.Axe,
			WeaponKind.Hammer,
			WeaponKind.None
		};

		public static readonly WeaponKind[] FightKinds = new WeaponKind[8]
		{
			WeaponKind.None,
			WeaponKind.Sword,
			WeaponKind.Axe,
			WeaponKind.Spear,
			WeaponKind.Hammer,
			WeaponKind.Stick,
			WeaponKind.Staff,
			WeaponKind.Dagger
		};

		public const int WorkBase = 204;

		public const float WalkPace = 0.9f;

		public const float RunPace = 2.6f;

		public const float WorkStance = 0.35f;

		private static float _walkStride = -1f;

		private static float _runStride;

		private static float _runShare;

		private static readonly Dictionary<MeshModel, float[]> Recorded = new Dictionary<MeshModel, float[]>();

		[ThreadStatic]
		private static float[] _scratch;

		public static int FightBase => 204 + WorkKinds.Length * 17;

		public static int Rows => FightBase + FightKinds.Length * 2 * 17;

		public static float WalkStride
		{
			get
			{
				Strides();
				return _walkStride;
			}
		}

		public static float RunStride
		{
			get
			{
				Strides();
				return _runStride;
			}
		}

		public static float RunShare
		{
			get
			{
				Strides();
				return _runShare;
			}
		}

		public static int Width(MeshModel model)
		{
			return model.Parts.Length * 3;
		}

		private static void Strides()
		{
			if (!(_walkStride >= 0f))
			{
				WalkCycle.Walker walker = new WalkCycle.Walker();
				for (int i = 0; i < 300; i++)
				{
					walker.Step(0.9f, 1f, 0f, 0f, 1f / 60f);
				}
				float stride = walker.Stride;
				walker = new WalkCycle.Walker();
				for (int j = 0; j < 300; j++)
				{
					walker.Step(2.6f, 1f, 0f, 0f, 1f / 60f);
				}
				_runStride = walker.Stride;
				_runShare = walker.Run;
				_walkStride = stride;
			}
		}

		public static float[] For(MeshModel model, PersonRig rig)
		{
			lock (Recorded)
			{
				if (Recorded.TryGetValue(model, out var value))
				{
					return value;
				}
				if (Recorded.Count > 200)
				{
					Recorded.Clear();
				}
				value = Bake(model, rig);
				Recorded[model] = value;
				return value;
			}
		}

		public static float[] Bake(MeshModel model, PersonRig rig)
		{
			int num = model.Parts.Length;
			int num2 = num * 3;
			float[] array = new float[Rows * num2 * 4];
			MeshInstance meshInstance = new MeshInstance
			{
				Model = model
			};
			meshInstance.EnsurePose();
			WalkCycle.Walker walker = new WalkCycle.Walker();
			Mat3[] pm = new Mat3[num];
			Vec3[] pt = new Vec3[num];
			float walkStride = WalkStride;
			float runStride = RunStride;
			float runShare = RunShare;
			for (int i = 0; i < 3; i++)
			{
				int num3 = i * 68;
				for (int j = 0; j <= 32; j++)
				{
					float phase = (float)(Math.PI * 2.0 * (double)(j % 32) / 32.0);
					Pose(rig, meshInstance, walkStride, phase, 0f, i, walker);
					Write(array, num3 + j, meshInstance, model, pm, pt);
					Pose(rig, meshInstance, runStride, phase, runShare, i, walker);
					Write(array, num3 + 33 + j, meshInstance, model, pm, pt);
				}
				Pose(rig, meshInstance, 0f, 0f, 0f, i, walker);
				Write(array, num3 + 66, meshInstance, model, pm, pt);
				Write(array, num3 + 66 + 1, meshInstance, model, pm, pt);
			}
			for (int k = 0; k < WorkKinds.Length; k++)
			{
				for (int l = 0; l <= 16; l++)
				{
					Pose(rig, meshInstance, 0f, 0f, 0f, 0, walker);
					rig.Fight(meshInstance, WorkKinds[k], (float)(l % 16) / 16f, (WorkKinds[k] == WeaponKind.None) ? 0f : 0.35f, 0f, 0f, 1f);
					Write(array, 204 + k * 17 + l, meshInstance, model, pm, pt);
				}
			}
			for (int m = 0; m < FightKinds.Length; m++)
			{
				for (int n = 0; n < 2; n++)
				{
					for (int num4 = 0; num4 <= 16; num4++)
					{
						Pose(rig, meshInstance, 0f, 0f, 0f, 0, walker);
						rig.Fight(meshInstance, FightKinds[m], (float)(num4 % 16) / 16f, 1f, 0f, 0f, (n == 0) ? 1f : (-1f));
						Write(array, FightBase + (m * 2 + n) * 17 + num4, meshInstance, model, pm, pt);
					}
				}
			}
			return array;
		}

		public static int VariantOf(PersonRig rig, bool child, bool elder)
		{
			if (!child || rig.ChildBody)
			{
				if (!elder || child)
				{
					return 0;
				}
				return 1;
			}
			return 2;
		}

		public static int WorkRow(WeaponKind swing)
		{
			int num = Array.IndexOf(WorkKinds, swing);
			if (num >= 0)
			{
				return 204 + num * 17;
			}
			return -1;
		}

		public static int FightRow(WeaponKind kind, float side)
		{
			int num = Array.IndexOf(FightKinds, kind);
			if (num >= 0)
			{
				return FightBase + (num * 2 + ((!(side >= 0f)) ? 1 : 0)) * 17;
			}
			return -1;
		}

		private static void Pose(PersonRig rig, MeshInstance m, float stride, float phase, float run, int variant, WalkCycle.Walker walker)
		{
			m.ResetPose();
			rig.Pose(m, stride, phase, 0f, 0f, 0f, 0f, run, walker);
			if (variant == 2 && rig.Head >= 0)
			{
				m.PartScale[rig.Head] = 1.28f;
			}
			if (variant == 1 && rig.Torso >= 0)
			{
				m.PartRotation[rig.Torso] = m.PartRotation[rig.Torso] * Mat3.RotX(0.16f);
			}
		}

		private static void Write(float[] data, int row, MeshInstance m, MeshModel model, Mat3[] pm, Vec3[] pt)
		{
			int num = model.Parts.Length;
			int num2 = row * num * 12;
			for (int i = 0; i < num; i++)
			{
				MeshPart meshPart = model.Parts[i];
				Mat3 a = ((m.PartRotation != null) ? m.PartRotation[i] : Mat3.Identity);
				float num3 = ((m.PartScale != null) ? m.PartScale[i] : 1f);
				if (num3 != 1f)
				{
					a *= num3;
				}
				Vec3 vec = meshPart.Pivot - a.Mul(meshPart.Pivot);
				if (m.PartOffset != null)
				{
					vec += m.PartOffset[i];
				}
				if (meshPart.Parent >= 0)
				{
					pm[i] = pm[meshPart.Parent] * a;
					pt[i] = pm[meshPart.Parent].Mul(vec) + pt[meshPart.Parent];
				}
				else
				{
					pm[i] = a;
					pt[i] = vec;
				}
				Mat3 mat = pm[i];
				Vec3 vec2 = pt[i];
				int num4 = num2 + i * 12;
				data[num4] = mat.C0.X;
				data[num4 + 1] = mat.C1.X;
				data[num4 + 2] = mat.C2.X;
				data[num4 + 3] = vec2.X;
				data[num4 + 4] = mat.C0.Y;
				data[num4 + 5] = mat.C1.Y;
				data[num4 + 6] = mat.C2.Y;
				data[num4 + 7] = vec2.Y;
				data[num4 + 8] = mat.C0.Z;
				data[num4 + 9] = mat.C1.Z;
				data[num4 + 10] = mat.C2.Z;
				data[num4 + 11] = vec2.Z;
			}
		}

		public static void PartAt(float[] data, MeshModel model, float a, float b, float w, int part, out Mat3 r, out Vec3 t)
		{
			int parts = model.Parts.Length;
			float[] array = _scratch ?? (_scratch = new float[12]);
			Blend(data, parts, a, part, array, 1f - w, first: true);
			Blend(data, parts, b, part, array, w, first: false);
			r = new Mat3(new Vec3(array[0], array[4], array[8]), new Vec3(array[1], array[5], array[9]), new Vec3(array[2], array[6], array[10]));
			t = new Vec3(array[3], array[7], array[11]);
		}

		private static void Blend(float[] data, int parts, float row, int part, float[] into, float k, bool first)
		{
			int num = (int)row;
			float num2 = row - (float)num;
			int num3 = (num * parts + part) * 12;
			int num4 = ((num + 1) * parts + part) * 12;
			for (int i = 0; i < 12; i++)
			{
				float num5 = data[num3 + i] + (data[num4 + i] - data[num3 + i]) * num2;
				into[i] = (first ? (num5 * k) : (into[i] + num5 * k));
			}
		}

		public static void Motion(int variant, float phase, float stride, float run, out float a, out float b, out float w)
		{
			float num = phase / (MathF.PI * 2f);
			float num2 = (num - (float)Math.Floor(num)) * 32f;
			int num3 = variant * 68;
			float num4 = ((RunShare > 0.01f) ? Math.Min(1f, run / RunShare) : 0f);
			if (num4 > 0.05f)
			{
				a = (float)num3 + num2;
				b = (float)(num3 + 33) + num2;
				w = num4;
			}
			else
			{
				float num5 = ((WalkStride > 0.0001f) ? Math.Min(1.4f, stride / WalkStride) : 0f);
				a = num3 + 66;
				b = (float)num3 + num2;
				w = num5;
			}
		}

		public static float Stroke(int row, float at)
		{
			at -= (float)Math.Floor(at);
			return (float)row + at * 16f;
		}
	}
}
