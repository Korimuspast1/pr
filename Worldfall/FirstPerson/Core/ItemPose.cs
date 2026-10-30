using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class ItemPose
	{
		private sealed class Pose
		{
			public float AxisX;

			public float AxisY;

			public float GripX;

			public float GripY;

			public float Length;
		}

		private static readonly Dictionary<SpriteImage, Pose> Poses = new Dictionary<SpriteImage, Pose>();

		public static VoxelObject HoldItem(SpriteImage item, Vec3 grip, Vec3 blade, Vec3 right, float length, bool overlay)
		{
			if (!Poses.TryGetValue(item, out var value))
			{
				if (Poses.Count > 500)
				{
					Poses.Clear();
				}
				value = ComputePose(item);
				Poses[item] = value;
			}
			blade = blade.Normalized();
			Vec3 vec = (right - blade * Vec3.Dot(right, blade)).Normalized();
			float axisX = value.AxisX;
			float axisY = value.AxisY;
			float num = 0f - axisY;
			float num2 = axisX;
			if (length <= 0f)
			{
				length = Fm.Clamp(0.07f + value.Length * 0.003f, 0.09f, 0.13f);
			}
			float num3 = length / Math.Max(1f, value.Length);
			VoxelObject result = new VoxelObject
			{
				Shape = item.SlabShape(1),
				Overlay = overlay,
				Tag = -1
			};
			result.U = (blade * axisX + vec * num).Normalized();
			result.V = (blade * axisY + vec * num2).Normalized();
			result.N = Vec3.Cross(result.U, result.V);
			result.SizeU = (result.SizeV = (result.SizeN = num3));
			result.Origin = grip - result.U * (value.GripX * num3) - result.V * (value.GripY * num3);
			return result;
		}

		private static Pose ComputePose(SpriteImage img)
		{
			double num = 0.0;
			double num2 = 0.0;
			double num3 = 0.0;
			for (int i = 0; i < img.Height; i++)
			{
				for (int j = 0; j < img.Width; j++)
				{
					if (img.Pixels[i * img.Width + j] >> 24 >= 128)
					{
						num += (double)j + 0.5;
						num2 += (double)i + 0.5;
						num3 += 1.0;
					}
				}
			}
			Pose pose = new Pose
			{
				AxisX = 0f,
				AxisY = 1f,
				GripX = (float)img.Width * 0.5f,
				GripY = 0f,
				Length = img.Height
			};
			if (num3 < 2.0)
			{
				return pose;
			}
			double num4 = num / num3;
			double num5 = num2 / num3;
			double num6 = 0.0;
			double num7 = 0.0;
			double num8 = 0.0;
			for (int k = 0; k < img.Height; k++)
			{
				for (int l = 0; l < img.Width; l++)
				{
					if (img.Pixels[k * img.Width + l] >> 24 >= 128)
					{
						double num9 = (double)l + 0.5 - num4;
						double num10 = (double)k + 0.5 - num5;
						num6 += num9 * num9;
						num7 += num10 * num10;
						num8 += num9 * num10;
					}
				}
			}
			double num11 = 0.5 * Math.Atan2(2.0 * num8, num6 - num7);
			double num12 = Math.Cos(num11);
			double num13 = Math.Sin(num11);
			double num14 = double.MaxValue;
			double num15 = double.MinValue;
			for (int m = 0; m < img.Height; m++)
			{
				for (int n = 0; n < img.Width; n++)
				{
					if (img.Pixels[m * img.Width + n] >> 24 >= 128)
					{
						double num16 = ((double)n + 0.5 - num4) * num12 + ((double)m + 0.5 - num5) * num13;
						if (num16 < num14)
						{
							num14 = num16;
						}
						if (num16 > num15)
						{
							num15 = num16;
						}
					}
				}
			}
			double num17 = num5 + num13 * num14;
			double num18 = num5 + num13 * num15;
			double num19 = num4 + num12 * num14;
			double num20 = num4 + num12 * num15;
			if (!((Math.Abs(num17 - num18) > 0.5) ? (num17 < num18) : (num19 < num20)))
			{
				num12 = 0.0 - num12;
				num13 = 0.0 - num13;
				double num21 = num14;
				num14 = 0.0 - num15;
				num15 = 0.0 - num21;
			}
			pose.AxisX = (float)num12;
			pose.AxisY = (float)num13;
			pose.GripX = (float)(num4 + num12 * num14);
			pose.GripY = (float)(num5 + num13 * num14);
			pose.Length = (float)(num15 - num14);
			return pose;
		}
	}
}
