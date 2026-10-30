using System;

namespace FirstPerson.Core
{
	public static class FlagWave
	{
		public const float LowerSeconds = 2.4f;

		public const float PauseSeconds = 0.5f;

		public const float RaiseSeconds = 2.4f;

		public const float FoldShare = 0.33f;

		public const float Total = 5.3f;

		public const float HingeLimit = 0.3f;

		public static void Pose(MeshInstance cloth, int strips, float length, float wind, float clock, float seed, bool torn)
		{
			MeshModel meshModel = cloth?.Model;
			if (meshModel == null || strips <= 1 || meshModel.Parts.Length < strips)
			{
				return;
			}
			if (cloth.PartRotation == null || cloth.PartRotation.Length != meshModel.Parts.Length)
			{
				cloth.EnsurePose();
			}
			double num = ((wind < 0f) ? 0.0 : ((wind > 1f) ? 1.0 : ((double)wind)));
			double num2 = Math.Max(0.3, length);
			double num3 = 3.0 + 5.0 * num;
			double num4 = (1.25 + -0.65 * num) * 1.9 / num2;
			if (torn)
			{
				num3 *= 0.7;
				num4 *= 0.8;
			}
			double phase = num3 * (double)clock + (double)seed;
			double b = Math.PI * 2.0 / (0.9 * num2);
			double alpha = 0.45 * num4;
			double beta = 0.55 * num4 / num2;
			float num5 = 2.4f / (float)strips;
			cloth.PartRotation[0] = Mat3.RotZ((float)(0.08 * Math.Sin(0.7 * num3 * (double)clock + (double)seed)));
			double num6 = Math.Max(meshModel.Max.X, (double)meshModel.Parts[strips - 1].Pivot.X + 0.001);
			for (int i = 1; i < strips; i++)
			{
				double num7 = meshModel.Parts[i].Pivot.X;
				double num8 = meshModel.Parts[i - 1].Pivot.X;
				double num9 = ((i + 1 < strips) ? ((double)meshModel.Parts[i + 1].Pivot.X) : num6);
				double x = 0.5 * (num8 + num7);
				double num10 = Bend(0.5 * (num7 + num9), alpha, beta, b, phase) - Bend(x, alpha, beta, b, phase);
				if (num10 > (double)num5)
				{
					num10 = num5;
				}
				else if (num10 < (double)(0f - num5))
				{
					num10 = 0f - num5;
				}
				cloth.PartRotation[i] = Mat3.RotZ((float)num10);
			}
		}

		private static double Bend(double x, double alpha, double beta, double b, double phase)
		{
			double num = phase - b * x;
			return (alpha + beta * x) * Math.Cos(num) / b + beta * Math.Sin(num) / (b * b);
		}

		public static float FarYaw(float wind, float clock, float seed)
		{
			double num = ((wind < 0f) ? 0.0 : ((wind > 1f) ? 1.0 : ((double)wind)));
			return (float)(0.1 * Math.Sin((3.0 + 5.0 * num) * (double)clock + (double)seed));
		}

		public static void Swap(float t, out float height01, out float fold01, out bool showNew)
		{
			if (t <= 0f)
			{
				height01 = 1f;
				fold01 = 0f;
				showNew = false;
			}
			else if (t < 2.4f)
			{
				float num = t / 2.4f;
				height01 = 1f - Smooth(num);
				fold01 = Clamp01((num - 0.66999996f) / 0.33f);
				showNew = false;
			}
			else if (t < 2.9f)
			{
				height01 = 0f;
				fold01 = 1f;
				showNew = t >= 2.65f;
			}
			else if (t < 5.3f)
			{
				float num2 = (t - 2.4f - 0.5f) / 2.4f;
				height01 = Smooth(num2);
				fold01 = 1f - Clamp01(num2 / 0.33f);
				showNew = true;
			}
			else
			{
				height01 = 1f;
				fold01 = 0f;
				showNew = true;
			}
		}

		public static float LowerFrom(float height01)
		{
			float num = Clamp01(1f - height01);
			return (float)(0.5 - Math.Sin(Math.Asin(1.0 - 2.0 * (double)num) / 3.0)) * 2.4f;
		}

		private static float Smooth(float s)
		{
			if (!(s <= 0f))
			{
				if (!(s >= 1f))
				{
					return s * s * (3f - 2f * s);
				}
				return 1f;
			}
			return 0f;
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
