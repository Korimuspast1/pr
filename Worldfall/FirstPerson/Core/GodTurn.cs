using System;

namespace FirstPerson.Core
{
	public static class GodTurn
	{
		public const float MapElevation = MathF.PI / 4f;

		public const float LowestElevation = 0.4886922f;

		public static float Elevation(float tip)
		{
			return MathF.PI / 4f + MathF.PI * -17f / 180f * Fm.Clamp01(tip);
		}

		public static ViewState Tip(ViewState camera, Vec3 look, float tip)
		{
			if (tip <= 0f)
			{
				return camera;
			}
			float num = look.X - camera.X;
			float num2 = look.Y - camera.Y;
			float num3 = (float)Math.Sqrt(num * num + num2 * num2);
			float num4 = camera.Z - look.Z;
			if (num3 < 0.001f || num4 <= 0f)
			{
				return camera;
			}
			float num5 = (float)Math.Tan((float)Math.Atan2(num4, num3) * Elevation(tip) / (MathF.PI / 4f));
			camera.Z = look.Z + num3 * num5;
			camera.Pitch = 0f - num5;
			return camera;
		}

		public static float HomeSeconds(float turn, float tip)
		{
			return Fm.Clamp(0.35f + 0.6f * Math.Abs(turn) / MathF.PI + 0.25f * Fm.Clamp01(tip), 0.35f, 1.2f);
		}

		public static float Home(float from, float rate, float seconds, float time, out float speed)
		{
			float num = Math.Max(0.001f, seconds);
			float num2 = Fm.Clamp01(time / num);
			if (num2 >= 1f)
			{
				speed = 0f;
				return 0f;
			}
			float num3 = (2f * num2 - 3f) * num2 * num2 + 1f;
			float num4 = num2 * (num2 - 1f) * (num2 - 1f);
			speed = from * (6f * num2 * num2 - 6f * num2) / num + rate * (3f * num2 * num2 - 4f * num2 + 1f);
			return from * num3 + rate * num * num4;
		}

		public static bool Project(ViewState camera, float width, float height, Vec3 point, out float column, out float row)
		{
			float num = width * 0.5f / (float)Math.Tan(camera.Fov * 0.5f);
			float num2 = height * 0.5f + camera.Pitch * num;
			float num3 = (float)Math.Cos(camera.Yaw);
			float num4 = (float)Math.Sin(camera.Yaw);
			float num5 = point.X - camera.X;
			float num6 = point.Y - camera.Y;
			float num7 = num5 * num3 + num6 * num4;
			if (num7 < 0.05f)
			{
				column = (row = 0f);
				return false;
			}
			column = width * 0.5f + (num5 * num4 - num6 * num3) / num7 * num;
			row = num2 - (point.Z - camera.Z) / num7 * num;
			return true;
		}
	}
}
