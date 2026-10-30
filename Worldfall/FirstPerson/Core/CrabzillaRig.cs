using System;
using System.Globalization;

namespace FirstPerson.Core
{
	public sealed class CrabzillaRig
	{
		public const int Left = 0;

		public const int Right = 1;

		public const int Legs = 8;

		public static readonly string[] LegNames = new string[8] { "l1", "l2", "l3", "l4", "r1", "r2", "r3", "r4" };

		public readonly MeshModel Model;

		public readonly int Body;

		public readonly int Eyes;

		public readonly int[] Arm = new int[2];

		public readonly int[] Wrist = new int[2];

		public readonly int[] Claw = new int[2];

		public readonly int[] Pincer = new int[2];

		public readonly int[] Glow = new int[2];

		public readonly int[] Thigh = new int[8];

		public readonly int[] Shin = new int[8];

		public readonly Vec3[] Hip = new Vec3[8];

		public readonly Vec3[] Knee = new Vec3[8];

		public readonly Vec3[] Foot = new Vec3[8];

		public readonly Vec3[] Shoulder = new Vec3[2];

		public readonly Vec3[] Tip = new Vec3[2];

		public readonly Vec3[] Aim = new Vec3[2];

		public readonly Vec3[] Wrist0 = new Vec3[2];

		public readonly Vec3[] Finger = new Vec3[2];

		public readonly Vec3[] Dactyl = new Vec3[2];

		public readonly Vec3[] Palm = new Vec3[2];

		public readonly Vec3 Eye;

		public readonly float Height;

		private readonly float[] _thighLen = new float[8];

		private readonly float[] _shinLen = new float[8];

		public const float CarryFollow = 0.9f;

		public static bool Fits(MeshModel m)
		{
			if (m != null)
			{
				return m.Info("kind") == "crabzilla";
			}
			return false;
		}

		public CrabzillaRig(MeshModel m)
		{
			Model = m;
			Body = m.PartIndex("body");
			Eyes = m.PartIndex("eyes");
			string[] array = new string[2] { "l", "r" };
			for (int i = 0; i < 2; i++)
			{
				Arm[i] = m.PartIndex("arm_" + array[i]);
				Wrist[i] = m.PartIndex("wrist_" + array[i]);
				Claw[i] = m.PartIndex("claw_" + array[i]);
				Pincer[i] = m.PartIndex("pincer_" + array[i]);
				Glow[i] = m.PartIndex("glow_" + array[i]);
				Shoulder[i] = ((Arm[i] >= 0) ? m.Parts[Arm[i]].Pivot : new Vec3(0f, 0f, 0f));
				Wrist0[i] = ((Claw[i] >= 0) ? m.Parts[Claw[i]].Pivot : Shoulder[i]);
				Tip[i] = InfoVec(m, "laser_" + array[i], (Glow[i] >= 0) ? m.Parts[Glow[i]].Pivot : Shoulder[i]);
				Aim[i] = InfoVec(m, "aim_" + array[i], new Vec3(0f, -1f, 0f)).Normalized();
				Finger[i] = InfoVec(m, "finger_" + array[i], Tip[i] + Aim[i] * 3f);
				Dactyl[i] = InfoVec(m, "dactyl_" + array[i], Tip[i] + Aim[i] * 3f);
				Palm[i] = InfoVec(m, "palm_" + array[i], Tip[i] - Aim[i] * 1.5f);
			}
			for (int j = 0; j < 8; j++)
			{
				Thigh[j] = m.PartIndex("leg_" + LegNames[j]);
				Shin[j] = m.PartIndex("shin_" + LegNames[j]);
				Hip[j] = ((Thigh[j] >= 0) ? m.Parts[Thigh[j]].Pivot : new Vec3(0f, 0f, 0f));
				Knee[j] = ((Shin[j] >= 0) ? m.Parts[Shin[j]].Pivot : Hip[j]);
				Foot[j] = InfoVec(m, "foot_" + LegNames[j], Knee[j]);
				_thighLen[j] = Length(Knee[j] - Hip[j]);
				_shinLen[j] = Length(Foot[j] - Knee[j]);
			}
			Eye = InfoVec(m, "eye", new Vec3(0f, m.Min.Y, m.Max.Z));
			Height = m.InfoFloat("height", m.Max.Z);
		}

		public void Pose(MeshInstance inst, Vec3 bodyOffset, Mat3 bodyTurn, Vec3[] feet, Vec3[] aim, float[] open, bool glowLeft, bool glowRight, bool hideEyes, Mat3[] carry = null)
		{
			inst.EnsurePose();
			inst.ResetPose();
			inst.Hidden = 0uL;
			if (Body < 0)
			{
				return;
			}
			inst.PartRotation[Body] = bodyTurn;
			inst.PartOffset[Body] = bodyOffset;
			Vec3 pivot = Model.Parts[Body].Pivot;
			Mat3 mat = Transpose(bodyTurn);
			if (feet != null)
			{
				for (int i = 0; i < 8; i++)
				{
					if (Thigh[i] >= 0 && Shin[i] >= 0)
					{
						Vec3 foot = mat.Mul(feet[i] - pivot - bodyOffset) + pivot;
						SolveLeg(inst, i, foot);
					}
				}
			}
			for (int j = 0; j < 2; j++)
			{
				if (Arm[j] < 0)
				{
					continue;
				}
				if (carry != null && Claw[j] >= 0)
				{
					Mat3 mat2 = carry[j];
					inst.PartRotation[Arm[j]] = mat2;
					if (aim != null && !float.IsNaN(aim[j].X))
					{
						Vec3 vec = mat.Mul(aim[j] - pivot - bodyOffset) + pivot;
						Vec3 vec2 = Shoulder[j] + mat2.Mul(Wrist0[j] - Shoulder[j]);
						Mat3 b = mat2;
						for (int k = 0; k < 4; k++)
						{
							Vec3 vec3 = vec2 + b.Mul(Tip[j] - Wrist0[j]);
							Vec3 vec4 = vec - vec3;
							if (Vec3.Dot(vec4, vec4) < 0.0001f)
							{
								break;
							}
							b = Turn(Aim[j], vec4.Normalized(), j);
						}
						inst.PartRotation[Claw[j]] = Transpose(mat2) * b;
					}
				}
				else if (aim != null && !float.IsNaN(aim[j].X))
				{
					Vec3 vec5 = mat.Mul(aim[j] - pivot - bodyOffset) + pivot;
					Mat3 mat3 = Mat3.Identity;
					for (int l = 0; l < 4; l++)
					{
						Vec3 vec6 = Shoulder[j] + mat3.Mul(Tip[j] - Shoulder[j]);
						Vec3 vec7 = vec5 - vec6;
						if (Vec3.Dot(vec7, vec7) < 0.0001f)
						{
							break;
						}
						mat3 = Turn(Aim[j], vec7.Normalized(), j);
					}
					inst.PartRotation[Arm[j]] = mat3;
				}
				if (Pincer[j] >= 0 && open != null && open[j] > 0f)
				{
					Vec3 axis = Vec3.Cross(Aim[j], new Vec3(0f, 0f, 1f)).Normalized();
					inst.PartRotation[Pincer[j]] = Mat3.Rotation(axis, 0.6f * Clamp01(open[j]));
				}
			}
			for (int m = 0; m < 2; m++)
			{
				if (Glow[m] >= 0 && Glow[m] < 64 && !((m == 0) ? glowLeft : glowRight))
				{
					inst.Hidden |= (ulong)(1L << Glow[m]);
				}
			}
			if (hideEyes && Eyes >= 0 && Eyes < 64)
			{
				inst.Hidden |= (ulong)(1L << Eyes);
			}
		}

		public Mat3 Carry(int side, Vec3 wrist, float yaw, float pitch)
		{
			Vec3 a = (Wrist0[side] - Shoulder[side]).Normalized();
			Vec3 vec = (wrist - Shoulder[side]).Normalized();
			float a2 = (float)(Math.Atan2(vec.Y, vec.X) - Math.Atan2(a.Y, a.X));
			float num = (float)Math.Asin(Clamp(a.Z, -1f, 1f));
			float num2 = (float)Math.Asin(Clamp(vec.Z, -1f, 1f));
			Vec3 vec2 = Vec3.Cross(a, new Vec3(0f, 0f, 1f));
			if (Vec3.Dot(vec2, vec2) < 1E-06f)
			{
				vec2 = new Vec3(1f, 0f, 0f);
			}
			Mat3 b = Mat3.RotZ(a2) * Mat3.Rotation(vec2.Normalized(), num2 - num);
			Vec3 vec3 = Vec3.Cross(b.Mul(Wrist0[side] - Shoulder[side]), new Vec3(0f, 0f, 1f));
			if (Vec3.Dot(vec3, vec3) < 1E-06f)
			{
				vec3 = new Vec3(1f, 0f, 0f);
			}
			return Mat3.RotZ(yaw) * Mat3.Rotation(vec3.Normalized(), pitch) * b;
		}

		public static Vec3 CarryWrist(int side)
		{
			return new Vec3((side == 0) ? 6.9f : (-6.9f), -9.8f, 7.85f);
		}

		public static Vec3 CarryAim(Vec3 eye, Vec3 target, float least = 40f)
		{
			float num = target.X - eye.X;
			float num2 = target.Y - eye.Y;
			float num3 = (float)Math.Sqrt(num * num + num2 * num2);
			if (num3 >= least || num3 < 0.001f)
			{
				return target;
			}
			return new Vec3(eye.X + num * (least / num3), eye.Y + num2 * (least / num3), target.Z);
		}

		public static float CarryPitch(float viewPitch)
		{
			return Clamp(viewPitch * 0.9f, -0.3f, 0.5f);
		}

		public Vec3 TipWorld(MeshInstance inst, int side)
		{
			if (Claw[side] < 0)
			{
				return inst.Position;
			}
			return PersonRig.Transform(inst, Claw[side], Tip[side]);
		}

		public void Sources(MeshInstance inst, int side, Vec3[] into)
		{
			into[0] = ((Claw[side] >= 0) ? PersonRig.Transform(inst, Claw[side], Finger[side]) : inst.Position);
			into[1] = ((Pincer[side] >= 0) ? PersonRig.Transform(inst, Pincer[side], Dactyl[side]) : into[0]);
			into[2] = ((Claw[side] >= 0) ? PersonRig.Transform(inst, Claw[side], Palm[side]) : into[0]);
		}

		public Vec3 AimWorld(MeshInstance inst, int side)
		{
			if (Claw[side] < 0)
			{
				return new Vec3(0f, -1f, 0f);
			}
			Vec3 vec = PersonRig.Transform(inst, Claw[side], Tip[side]);
			return (PersonRig.Transform(inst, Claw[side], Tip[side] + Aim[side]) - vec).Normalized();
		}

		public Vec3 BodyPoint(MeshInstance inst, Vec3 local)
		{
			if (Body < 0)
			{
				return inst.Basis.Mul(local) + inst.Position;
			}
			return PersonRig.Transform(inst, Body, local);
		}

		private void SolveLeg(MeshInstance inst, int i, Vec3 foot)
		{
			Vec3 vec = Hip[i];
			Vec3 vec2 = Knee[i];
			Vec3 vec3 = Foot[i];
			float num = _thighLen[i];
			float num2 = _shinLen[i];
			Vec3 vec4 = foot - vec;
			float num3 = Length(vec4);
			if (num3 < 0.0001f)
			{
				return;
			}
			Vec3 vec5 = vec4 * (1f / num3);
			num3 = Math.Max(Math.Abs(num - num2) * 1.02f + 0.05f, Math.Min(num3, (num + num2) * 0.998f));
			float num4 = (num * num - num2 * num2 + num3 * num3) / (2f * num3);
			float num5 = (float)Math.Sqrt(Math.Max(0f, num * num - num4 * num4));
			Vec3 vec6 = new Vec3(0f, 0f, 1f);
			Vec3 vec7 = vec6 - vec5 * Vec3.Dot(vec6, vec5);
			if (Vec3.Dot(vec7, vec7) < 1E-06f)
			{
				vec7 = new Vec3(Math.Sign((vec.X == 0f) ? 1f : vec.X), 0f, 0f);
			}
			vec7 = vec7.Normalized();
			Vec3 vec8 = vec + vec5 * num4 + vec7 * num5;
			Vec3 vec9 = vec + vec5 * num3;
			Vec3 vec10 = (vec2 - vec).Normalized();
			Vec3 vec11 = (vec3 - vec2).Normalized();
			Vec3 vec12 = Vec3.Cross(vec10, vec11);
			if (!(Vec3.Dot(vec12, vec12) < 1E-08f))
			{
				vec12 = vec12.Normalized();
				Vec3 vec13 = (vec8 - vec).Normalized();
				Vec3 b = (vec9 - vec8).Normalized();
				Vec3 vec14 = Vec3.Cross(vec13, b);
				if (Vec3.Dot(vec14, vec14) < 1E-08f)
				{
					vec14 = vec12;
				}
				vec14 = vec14.Normalized();
				Mat3 m = new Mat3(vec10, vec12, Vec3.Cross(vec10, vec12));
				Mat3 b2 = new Mat3(vec13, vec14, Vec3.Cross(vec13, vec14)) * Transpose(m);
				inst.PartRotation[Thigh[i]] = b2;
				Vec3 a = b2.Mul(vec11);
				float angle = (float)Math.Atan2(Vec3.Dot(vec14, Vec3.Cross(a, b)), Vec3.Dot(a, b));
				Mat3 b3 = Mat3.Rotation(vec14, angle);
				inst.PartRotation[Shin[i]] = Transpose(b2) * b3 * b2;
			}
		}

		private static Mat3 Turn(Vec3 from, Vec3 to, int side)
		{
			float num = (float)Math.Atan2(from.Y, from.X);
			float num2 = (float)Math.Atan2(to.Y, to.X) - num;
			while ((double)num2 > Math.PI)
			{
				num2 -= MathF.PI * 2f;
			}
			for (; (double)num2 < -Math.PI; num2 += MathF.PI * 2f)
			{
			}
			num2 = ((side == 0) ? Clamp(num2, -0.55f, 1.9f) : Clamp(num2, -1.9f, 0.55f));
			float num3 = (float)Math.Asin(Clamp(from.Z, -1f, 1f));
			float angle = Clamp((float)Math.Asin(Clamp(to.Z, -1f, 1f)) - num3, -1.15f, 1.15f);
			Vec3 vec = Vec3.Cross(from, new Vec3(0f, 0f, 1f));
			if (Vec3.Dot(vec, vec) < 1E-06f)
			{
				vec = new Vec3(1f, 0f, 0f);
			}
			return Mat3.RotZ(num2) * Mat3.Rotation(vec.Normalized(), angle);
		}

		public static Mat3 Transpose(Mat3 m)
		{
			return new Mat3(new Vec3(m.C0.X, m.C1.X, m.C2.X), new Vec3(m.C0.Y, m.C1.Y, m.C2.Y), new Vec3(m.C0.Z, m.C1.Z, m.C2.Z));
		}

		private static Vec3 InfoVec(MeshModel m, string key, Vec3 fallback)
		{
			string text = m.Info(key);
			if (text == null)
			{
				return fallback;
			}
			string[] array = text.Split(',');
			if (array.Length != 3)
			{
				return fallback;
			}
			CultureInfo invariantCulture = CultureInfo.InvariantCulture;
			if (!float.TryParse(array[0], NumberStyles.Float, invariantCulture, out var result) || !float.TryParse(array[1], NumberStyles.Float, invariantCulture, out var result2) || !float.TryParse(array[2], NumberStyles.Float, invariantCulture, out var result3))
			{
				return fallback;
			}
			return new Vec3(result, result2, result3);
		}

		private static float Length(Vec3 v)
		{
			return (float)Math.Sqrt(Vec3.Dot(v, v));
		}

		private static float Clamp(float v, float lo, float hi)
		{
			if (!(v < lo))
			{
				if (!(v > hi))
				{
					return v;
				}
				return hi;
			}
			return lo;
		}

		private static float Clamp01(float v)
		{
			if (!(v < 0f))
			{
				if (!(v > 1f))
				{
					return v;
				}
				return 1f;
			}
			return 0f;
		}
	}
}
