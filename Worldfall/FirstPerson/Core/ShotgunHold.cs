using System;

namespace FirstPerson.Core
{
	public static class ShotgunHold
	{
		public const float AttackSeconds = 0.75f;

		public static float Kick(float attack)
		{
			if (!(attack <= 0f) && !(attack >= 1f))
			{
				if (!(attack < 0.07f))
				{
					return (float)Math.Exp((0f - (attack - 0.07f)) * 9f);
				}
				return attack / 0.07f;
			}
			return 0f;
		}

		public static float Pump(float attack)
		{
			if (attack < 0.32f || attack > 0.72f)
			{
				return 0f;
			}
			if (attack < 0.44f)
			{
				return (attack - 0.32f) / 0.12f;
			}
			if (attack < 0.56f)
			{
				return 1f;
			}
			return 1f - (attack - 0.56f) / 0.16f;
		}

		public static bool Place(PersonRig rig, MeshInstance body, float stance, float attack, float height, float heading, float aim, out Vec3 position, out Mat3 basis)
		{
			position = body.Position;
			basis = Mat3.Identity;
			if (rig.LowerArmR < 0)
			{
				return false;
			}
			Vec3 vec = rig.HandWorld(body);
			Vec3 vec2 = rig.LeftHandWorld(body);
			Vec3 vec3 = new Vec3((float)Math.Cos(heading), (float)Math.Sin(heading), 0f);
			Vec3 vec4 = new Vec3(0f, 0f, 1f);
			Vec3 vec5 = Vec3.Cross(vec3, vec4);
			Vec3 vec6 = vec2 - vec;
			Vec3 vec7 = (vec3 * (float)Math.Cos(aim) + vec4 * (float)Math.Sin(aim)).Normalized();
			Vec3 vec8 = ((Vec3.Dot(vec6, vec6) > 1E-06f) ? (vec6.Normalized() * 0.5f + vec7 * 2.5f).Normalized() : vec7);
			Vec3 vec9 = (vec3 * 0.62f - vec4 * 0.74f - vec5 * 0.18f).Normalized();
			float num = stance * stance * (3f - 2f * stance);
			Vec3 vec10 = (vec9 + (vec8 - vec9) * num).Normalized();
			float num2 = Kick(attack) * num;
			Vec3 vec11 = Vec3.Cross(vec10, vec4);
			vec11 = ((Vec3.Dot(vec11, vec11) > 1E-06f) ? vec11.Normalized() : vec5);
			if (num2 > 0f)
			{
				vec10 = Vec3.Rotate(vec10, vec11, 0.42f * num2).Normalized();
			}
			Vec3 vec12 = Vec3.Cross(vec11, vec10);
			float num3 = Math.Max(0.3f, Math.Min(3f, height));
			basis = new Mat3(vec11 * num3, vec10 * num3, vec12 * num3);
			position = vec - vec10 * (0.025f * num2 * num3);
			return true;
		}
	}
}
