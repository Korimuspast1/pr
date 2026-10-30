using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public struct SightCut
	{
		public bool On;

		public float X;

		public float Y;

		public float Z;

		public float Fx;

		public float Fy;

		public float Tx;

		public float Ty;

		public float Tz;

		public float Target;

		public float Bottom;

		public float Aim;

		public float Margin;

		public float Strength;

		public static readonly SightCut Off;

		private const float Hidden = 0.45f;

		private const float Sight = 0.9f;

		public const float Clear = 0.6f;

		public const float RampSlope = 0.05f;

		public const float RampLeast = 0.4f;

		public static SightCut Make(ViewState camera, int width, int height, float targetX, float targetY, float targetZ, float strength)
		{
			float num = (float)Math.Cos(camera.Yaw);
			float num2 = (float)Math.Sin(camera.Yaw);
			float num3 = (targetX - camera.X) * num + (targetY - camera.Y) * num2;
			if (strength <= 0.001f || num3 < 1f || width <= 0 || height <= 0)
			{
				return Off;
			}
			float num4 = (float)width * 0.5f / (float)Math.Tan(camera.Fov * 0.5f);
			Lens lens = Lens.Of(in camera, (float)height / (float)width);
			float num5 = (float)height * 0.5f + lens.Shift * num4;
			return new SightCut
			{
				On = true,
				X = camera.X,
				Y = camera.Y,
				Z = camera.Z,
				Fx = num,
				Fy = num2,
				Tx = targetX,
				Ty = targetY,
				Tz = targetZ,
				Target = num3,
				Bottom = lens.SlopeOf((num5 - (float)height) / num4),
				Aim = (targetZ - camera.Z) / num3,
				Margin = MarginFor(num3),
				Strength = Math.Min(1f, strength)
			};
		}

		public static float MarginFor(float target)
		{
			return Math.Max(0.25f, Math.Min(3f, target * 0.05f));
		}

		public float Depth(float x, float y)
		{
			return (x - X) * Fx + (y - Y) * Fy;
		}

		public float Ceiling(float t)
		{
			if (!On || t <= 0f || t >= Target)
			{
				return float.MaxValue;
			}
			float num = t / Target;
			float num2 = Bottom - 0.04f;
			float num3 = Math.Max(Aim, num2);
			float num4;
			if (num <= 0.45f)
			{
				num4 = num2;
			}
			else if (num <= 0.9f)
			{
				float num5 = (num - 0.45f) / 0.45f;
				num4 = num2 + (num3 - num2) * num5 * num5 * (3f - 2f * num5);
			}
			else
			{
				float num6 = num - 0.9f;
				num4 = num3 + 6f * num6 + 50f * num6 * num6;
			}
			return Z + num4 * t;
		}

		public float Cap(float t)
		{
			float num = Ceiling(t);
			if (num != float.MaxValue)
			{
				return num - Margin;
			}
			return num;
		}

		public float Land(float t, float h)
		{
			float num = Cap(t);
			if (!(h <= num))
			{
				return h - (h - num) * Strength;
			}
			return h;
		}

		public float Veil(float t, float h)
		{
			float num = Cap(t);
			if (num == float.MaxValue || h <= num)
			{
				return 1f;
			}
			return 1f - 0.6f * Strength * Smooth((h - num) / Math.Max(0.4f, t * 0.05f));
		}

		public float Within(float t, float z, float h)
		{
			if (!On || h <= z)
			{
				return 0f;
			}
			return Strength * Smooth((h - z) / Math.Max(0.4f, t * 0.05f));
		}

		public float CloudNear(float distance)
		{
			if (!On)
			{
				return 1f;
			}
			float num = Smooth((distance - 3f) / 9f);
			return 1f - (1f - num) * Strength;
		}

		public float Storm(float x, float y, float halfX, float halfY, float low, float high)
		{
			if (!On)
			{
				return 1f;
			}
			float num = Z - Tz;
			if (num < 0.5f || Z <= low)
			{
				return 1f;
			}
			float num2 = Math.Max(0f, (Z - high) / num);
			float num3 = Math.Min(1f, (Z - low) / num);
			if (num3 <= num2)
			{
				return 1f;
			}
			float num4 = X + (Tx - X) * num2;
			float num5 = Y + (Ty - Y) * num2;
			float num6 = X + (Tx - X) * num3;
			float num7 = Y + (Ty - Y) * num3;
			float num8 = Math.Max(0.5f, halfX);
			float num9 = Math.Max(0.5f, halfY);
			float num10 = (num4 - x) / num8;
			float num11 = (num5 - y) / num9;
			float num12 = (num6 - num4) / num8;
			float num13 = (num7 - num5) / num9;
			float num14 = num12 * num12 + num13 * num13;
			float num15 = ((num14 > 1E-06f) ? Math.Max(0f, Math.Min(1f, (0f - (num10 * num12 + num11 * num13)) / num14)) : 0f);
			float num16 = num10 + num12 * num15;
			float num17 = num11 + num13 * num15;
			float num18 = Smooth(((float)Math.Sqrt(num16 * num16 + num17 * num17) - 1.3f) / 0.6f);
			return 1f - (1f - num18) * Strength;
		}

		private static float Smooth(float k)
		{
			k = ((k < 0f) ? 0f : ((k > 1f) ? 1f : k));
			return k * k * (3f - 2f * k);
		}

		public void Prune(TerrainCache terrain, List<MeshInstance> meshes, int creatureTags = 0)
		{
			if (!On || terrain == null || terrain.Width == 0 || meshes == null)
			{
				return;
			}
			for (int i = 0; i < meshes.Count; i++)
			{
				MeshInstance meshInstance = meshes[i];
				if (meshInstance.Model == null || meshInstance.Tag == -2 || (meshInstance.Tag >= 0 && meshInstance.Tag < creatureTags))
				{
					continue;
				}
				float num = Depth(meshInstance.Position.X, meshInstance.Position.Y);
				if (!(num <= 0f) && !(num >= Target) && !(meshInstance.Position.Z - terrain.SampleHeight(meshInstance.Position.X, meshInstance.Position.Y) > 2.5f))
				{
					float num2 = Veil(num, meshInstance.Position.Z + Math.Abs(meshInstance.Basis.C2.Z) * meshInstance.Model.Max.Z);
					if (num2 < meshInstance.Fade)
					{
						meshInstance.Fade = num2;
					}
				}
			}
		}
	}
}
