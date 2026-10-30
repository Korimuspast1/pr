using System;

namespace FirstPerson.Core
{
	public static class DivePath
	{
		public const float MapNearest = 10f;

		public const float FadeFar = 18f;

		public const float FadeNear = 7f;

		public const float MapFov = 60f;

		private const float Settle = 0.45f;

		private const float Elevation = MathF.PI / 4f;

		public static float Ease(float t)
		{
			t = Fm.Clamp01(t);
			return t * t * t * (t * (t * 6f - 15f) + 10f);
		}

		public static float InverseEase(float k)
		{
			k = Fm.Clamp01(k);
			float num = 0f;
			float num2 = 1f;
			for (int i = 0; i < 24; i++)
			{
				float num3 = (num + num2) * 0.5f;
				if (Ease(num3) < k)
				{
					num = num3;
				}
				else
				{
					num2 = num3;
				}
			}
			return (num + num2) * 0.5f;
		}

		public static float Seconds(float mapZoom)
		{
			return Fm.Clamp(0.45f + 0.2f * Range(mapZoom), 1f, 1.9f);
		}

		private static float Range(float mapZoom)
		{
			return (float)(Math.Log(Math.Max(mapZoom, 10f) + 0.45f) - Math.Log(0.44999998807907104));
		}

		public static float ClosenessAt(float zoom, float mapZoom)
		{
			float num = (float)Math.Log(Math.Max(mapZoom, 10f) + 0.45f);
			float num2 = (float)Math.Log(0.44999998807907104);
			return Fm.Clamp01((num - (float)Math.Log(Math.Max(0f, zoom) + 0.45f)) / (num - num2));
		}

		public static float Zoom(float closeness, float mapZoom)
		{
			float num = (float)Math.Log(Math.Max(mapZoom, 10f) + 0.45f);
			float num2 = (float)Math.Log(0.44999998807907104);
			return (float)Math.Exp(num + (num2 - num) * Fm.Clamp01(closeness)) - 0.45f;
		}

		public static float Swoop(float zoom)
		{
			float num = (float)Math.Log(0.44999998807907104);
			float num2 = (float)Math.Log(10.449999809265137);
			float v = ((float)Math.Log(Math.Max(0f, zoom) + 0.45f) - num) / (num2 - num);
			v = Fm.Clamp01(v);
			return v * v * (3f - 2f * v);
		}

		public static float ViewAlpha(float closeness, float zoom)
		{
			float num = Fm.Clamp01(((float)Math.Log(18.0) - (float)Math.Log(Math.Max(0.001f, zoom))) / ((float)Math.Log(18.0) - (float)Math.Log(7.0)));
			float val = num * num * (3f - 2f * num);
			float num2 = Fm.Clamp01(closeness / 0.2f);
			return Math.Min(val, num2 * num2 * (3f - 2f * num2));
		}

		public static ViewState Camera(ViewState eye, float groundZ, float closeness, float mapZoom, float turn, int width, int height)
		{
			float num = Zoom(closeness, mapZoom);
			float num2 = Swoop(num);
			float num3 = Fm.Lerp(eye.Fov, MathF.PI / 3f, num2);
			float num4 = eye.Yaw + turn * num2;
			float num5 = Fm.Lerp((float)Math.Atan(eye.Pitch), -MathF.PI / 4f, num2);
			float num6 = MathF.PI / 4f * num2;
			float num7 = (float)width * 0.5f / (float)Math.Tan(num3 * 0.5f);
			float num8 = 2f * (float)Math.Sqrt(2.0) * num7 * num / (float)Math.Max(1, height);
			float num9 = eye.Z + (groundZ - eye.Z) * num2;
			float num10 = num8 * (float)Math.Cos(num6);
			return new ViewState
			{
				X = eye.X - (float)Math.Cos(num4) * num10,
				Y = eye.Y - (float)Math.Sin(num4) * num10,
				Z = num9 + num8 * (float)Math.Sin(num6),
				Yaw = num4,
				Pitch = (float)Math.Tan(num5),
				Fov = num3,
				Shear = eye.Shear + (1f - eye.Shear) * num2,
				Free = eye.Free
			};
		}

		public static float Distance(ViewState camera, ViewState eye)
		{
			float num = camera.X - eye.X;
			float num2 = camera.Y - eye.Y;
			float num3 = camera.Z - eye.Z;
			return (float)Math.Sqrt(num * num + num2 * num2 + num3 * num3);
		}

		public static float Wrap(float a)
		{
			while ((double)a > Math.PI)
			{
				a -= MathF.PI * 2f;
			}
			while ((double)a < -Math.PI)
			{
				a += MathF.PI * 2f;
			}
			return a;
		}
	}
}
