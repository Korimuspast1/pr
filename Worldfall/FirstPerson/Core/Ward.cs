using System;

namespace FirstPerson.Core
{
	public static class Ward
	{
		public const float Seconds = 0.55f;

		public static float Envelope(float age, float rise)
		{
			if (age < 0f || age >= 0.55f)
			{
				return 0f;
			}
			if (age < rise)
			{
				float num = age / rise;
				return 1f - (1f - num) * (1f - num);
			}
			float num2 = Math.Min(1f, (age - rise) / (0.55f - rise));
			return 1f - num2 * num2 * (3f - 2f * num2);
		}

		public static void Pose(PersonRig rig, MeshInstance m, WeaponKind kind, bool dodge, float age, float side, float weight)
		{
			if (rig == null || m == null)
			{
				return;
			}
			float num = Envelope(age, dodge ? 0.08f : 0.05f);
			if (num <= 0f)
			{
				return;
			}
			m.EnsurePose();
			if (dodge)
			{
				Mul(m, rig.Torso, Mat3.RotY(side * 0.34f * num) * Mat3.RotX(0.08f * num));
				Mul(m, rig.Head, Mat3.RotY((0f - side) * 0.14f * num));
				if (rig.Hips >= 0)
				{
					m.PartOffset[rig.Hips] = m.PartOffset[rig.Hips] + new Vec3(side * 0.07f * num, 0f, -0.04f * num);
				}
				Mul(m, rig.UpperLegL, Mat3.RotX(-0.2f * num));
				Mul(m, rig.UpperLegR, Mat3.RotX(-0.2f * num));
				Mul(m, rig.LowerLegL, Mat3.RotX(0.35f * num));
				Mul(m, rig.LowerLegR, Mat3.RotX(0.35f * num));
				Mul(m, rig.UpperArmL, Mat3.RotY((0f - side) * 0.25f * num));
				Mul(m, rig.UpperArmR, Mat3.RotY((0f - side) * 0.25f * num));
				return;
			}
			float num2 = num * (0.75f + 0.5f * Math.Max(0f, Math.Min(1f, weight)));
			Mul(m, rig.Torso, Mat3.RotX(-0.2f * num2));
			Mul(m, rig.Head, Mat3.RotX(-0.14f * num2));
			if (rig.Hips >= 0)
			{
				m.PartOffset[rig.Hips] = m.PartOffset[rig.Hips] + new Vec3(0f, -0.03f * num2, -0.035f * num2);
			}
			Mul(m, rig.LowerLegL, Mat3.RotX(0.28f * num2));
			Mul(m, rig.LowerLegR, Mat3.RotX(0.22f * num2));
			switch (kind)
			{
			case WeaponKind.None:
				Mul(m, rig.UpperArmL, Mat3.RotX(-0.9f * num) * Mat3.RotY(0.3f * num));
				Mul(m, rig.UpperArmR, Mat3.RotX(-0.9f * num) * Mat3.RotY(-0.3f * num));
				Mul(m, rig.LowerArmL, Mat3.RotX(-0.9f * num));
				Mul(m, rig.LowerArmR, Mat3.RotX(-0.9f * num));
				break;
			case WeaponKind.Spear:
			case WeaponKind.Staff:
			case WeaponKind.Shotgun:
				Mul(m, rig.UpperArmL, Mat3.RotX(-0.45f * num2));
				Mul(m, rig.UpperArmR, Mat3.RotX(-0.45f * num2));
				break;
			default:
				Mul(m, rig.UpperArmR, Mat3.RotX(-0.55f * num2) * Mat3.RotY(0.25f * num2));
				Mul(m, rig.LowerArmR, Mat3.RotX(-0.3f * num2));
				Mul(m, rig.UpperArmL, Mat3.RotY(-0.3f * num2));
				break;
			}
		}

		public static float Knock(float age)
		{
			if (age < 0f || age >= 0.4f)
			{
				return 0f;
			}
			if (age < 0.04f)
			{
				return age / 0.04f;
			}
			float num = (age - 0.04f) / 0.36f;
			return 1f - num * num * (3f - 2f * num);
		}

		public static void HeadKnock(PersonRig rig, MeshInstance m, float age, float side)
		{
			if (rig != null && m != null)
			{
				float num = Knock(age);
				if (!(num <= 0f))
				{
					m.EnsurePose();
					Mul(m, rig.Head, Mat3.RotX(-0.55f * num) * Mat3.RotY(0.18f * side * num));
					Mul(m, rig.Torso, Mat3.RotX(-0.1f * num));
				}
			}
		}

		public static void HeadKnock(MeshInstance m, int head, float age)
		{
			if (m != null && head >= 0)
			{
				float num = Knock(age);
				if (!(num <= 0f))
				{
					m.EnsurePose();
					Mul(m, head, Mat3.RotX(-0.5f * num));
				}
			}
		}

		public static void Coil(MeshInstance m, int head, float k)
		{
			if (m != null && head >= 0 && !(k <= 0f))
			{
				m.EnsurePose();
				Mul(m, head, Mat3.RotX(0.32f * k));
			}
		}

		private static void Mul(MeshInstance m, int part, Mat3 r)
		{
			if (part >= 0)
			{
				m.PartRotation[part] = m.PartRotation[part] * r;
			}
		}
	}
}
