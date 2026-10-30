using System;

namespace FirstPerson.Core
{
	public static class GodCamera
	{
		public static float LowestGround = -1.5f;

		public const float DefaultLowestGround = -1.5f;

		public static ViewState Camera(float x, float y, float groundZ, float zoom, float turn, int width, int height)
		{
			ViewState result = DivePath.Camera(new ViewState
			{
				X = x,
				Y = y,
				Z = groundZ,
				Yaw = MathF.PI / 2f + turn,
				Pitch = -1f,
				Fov = MathF.PI / 3f,
				Shear = 1f
			}, groundZ, 0f, zoom, 0f, width, height);
			result.Shear = 1f;
			return result;
		}

		public static float Reach(ViewState camera, int width, int height)
		{
			float num = (float)width * 0.5f / (float)Math.Tan(camera.Fov * 0.5f);
			float num2 = ((float)height * 0.5f + camera.Pitch * num - 0.5f) / num;
			return Math.Min((num2 < -0.02f) ? ((camera.Z - LowestGround) / (0f - num2)) : 400f, 400f) + 8f;
		}

		public static Vec3 Ray(ViewState camera, int width, int height, float column, float row)
		{
			float num = (float)width * 0.5f / (float)Math.Tan(camera.Fov * 0.5f);
			float num2 = (float)height * 0.5f + camera.Pitch * num;
			float num3 = (float)Math.Cos(camera.Yaw);
			float num4 = (float)Math.Sin(camera.Yaw);
			float num5 = (column - (float)width * 0.5f) / num;
			return new Vec3(num3 + num4 * num5, num4 - num3 * num5, (num2 - row) / num);
		}

		public static Vec3 LensRay(ViewState camera, int width, int height, float column, float row)
		{
			Lens lens = Lens.Of(in camera, (float)height / (float)Math.Max(1, width));
			float num = (float)width * 0.5f / (float)Math.Tan(camera.Fov * 0.5f);
			float num2 = (column - (float)width * 0.5f) / num;
			float num3 = ((float)height * 0.5f + lens.Shift * num - row) / num;
			float num4 = (float)Math.Cos(camera.Yaw);
			float num5 = (float)Math.Sin(camera.Yaw);
			float num6 = lens.Cq - num3 * lens.Sq;
			float z = lens.Sq + num3 * lens.Cq;
			return new Vec3(num4 * num6 + num5 * num2, num5 * num6 - num4 * num2, z);
		}

		public static bool DrawnPoint(ViewState camera, int width, int height, float[] depth, float column, float row, out Vec3 point)
		{
			point = default(Vec3);
			int num = (int)column;
			int num2 = (int)row;
			if (depth == null || num < 0 || num2 < 0 || num >= width || num2 >= height || depth.Length < width * height)
			{
				return false;
			}
			float num3 = depth[(height - 1 - num2) * width + num];
			if (!(num3 < 1000000f))
			{
				return false;
			}
			Vec3 vec = Ray(camera, width, height, column, row);
			point = new Vec3(camera.X + vec.X * num3, camera.Y + vec.Y * num3, camera.Z + vec.Z * num3);
			return true;
		}

		public static bool GroundPoint(TerrainCache terrain, ViewState camera, int width, int height, float column, float row, out Vec3 point)
		{
			return GroundPoint(terrain, camera, width, height, column, row, SightCut.Off, out point);
		}

		public static bool GroundPoint(TerrainCache terrain, ViewState camera, int width, int height, float column, float row, SightCut cut, out Vec3 point)
		{
			point = default(Vec3);
			Vec3 ray = Ray(camera, width, height, column, row);
			if (ray.Z >= 0f)
			{
				return false;
			}
			float num = 0.05f;
			float num2 = 0f;
			for (int i = 0; i < 4000; i++)
			{
				if (!(num < 600f))
				{
					break;
				}
				float num3 = Above(terrain, camera, ray, cut, num);
				if (num3 <= 0f)
				{
					float num4 = num2;
					float num5 = num;
					for (int j = 0; j < 12; j++)
					{
						float num6 = (num4 + num5) * 0.5f;
						if (Above(terrain, camera, ray, cut, num6) > 0f)
						{
							num4 = num6;
						}
						else
						{
							num5 = num6;
						}
					}
					float num7 = (num4 + num5) * 0.5f;
					point = new Vec3(camera.X + ray.X * num7, camera.Y + ray.Y * num7, camera.Z + ray.Z * num7);
					return true;
				}
				num2 = num;
				num += Math.Max(0.1f, Math.Min(2f, num3 * 0.5f));
			}
			return false;
		}

		public static bool GroundAlong(TerrainCache terrain, Vec3 from, Vec3 direction, out Vec3 point)
		{
			point = default(Vec3);
			float num = (float)Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y + direction.Z * direction.Z);
			if (num < 1E-06f || direction.Z >= 0f)
			{
				return false;
			}
			Vec3 ray = new Vec3(direction.X / num, direction.Y / num, direction.Z / num);
			ViewState camera = new ViewState
			{
				X = from.X,
				Y = from.Y,
				Z = from.Z
			};
			float num2 = 0.05f;
			float num3 = 0f;
			for (int i = 0; i < 4000; i++)
			{
				if (!(num2 < 900f))
				{
					break;
				}
				float num4 = Above(terrain, camera, ray, SightCut.Off, num2);
				if (num4 <= 0f)
				{
					float num5 = num3;
					float num6 = num2;
					for (int j = 0; j < 12; j++)
					{
						float num7 = (num5 + num6) * 0.5f;
						if (Above(terrain, camera, ray, SightCut.Off, num7) > 0f)
						{
							num5 = num7;
						}
						else
						{
							num6 = num7;
						}
					}
					float num8 = (num5 + num6) * 0.5f;
					point = new Vec3(from.X + ray.X * num8, from.Y + ray.Y * num8, from.Z + ray.Z * num8);
					return true;
				}
				num3 = num2;
				num2 += Math.Max(0.1f, Math.Min(2f, num4 * 0.5f));
			}
			return false;
		}

		private static float Above(TerrainCache terrain, ViewState camera, Vec3 ray, SightCut cut, float t)
		{
			float x = camera.X + ray.X * t;
			float y = camera.Y + ray.Y * t;
			float num = camera.Z + ray.Z * t;
			float num2 = Math.Max(terrain.SampleHeight(x, y), 0f);
			if (cut.On)
			{
				num2 = cut.Land(t, num2);
			}
			return num - num2;
		}
	}
}
