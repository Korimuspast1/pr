using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class CannonFx
	{
		private static MeshModel _ball;

		public static readonly uint GunSmoke = TerrainCache.Rgb(252, 249, 242);

		public static readonly uint GunSmokeOld = TerrainCache.Rgb(206, 204, 202);

		public static readonly uint Foam = TerrainCache.Rgb(232, 241, 250);

		public static readonly uint FoamShade = TerrainCache.Rgb(176, 208, 236);

		public const int Guns = 5;

		private static readonly int[] Order = new int[5] { 2, 0, 4, 1, 3 };

		private static readonly Dictionary<MeshModel, Vec3[]> PortCache = new Dictionary<MeshModel, Vec3[]>();

		public static MeshModel Ball
		{
			get
			{
				if (_ball != null)
				{
					return _ball;
				}
				MeshBuilder meshBuilder = new MeshBuilder("cannonball");
				int slot = meshBuilder.Slot("iron", 10, TerrainCache.Rgb(62, 60, 64), 0);
				meshBuilder.Part("main", -1, new Vec3(0f, 0f, 0f));
				meshBuilder.Blob(slot, new Vec3(0f, 0f, 0f), 0.5f, 0.5f, 0.5f, 10);
				return _ball = meshBuilder.Build();
			}
		}

		public static float R(uint seed, int i)
		{
			return BlastFx.Rand(seed, i);
		}

		public static Vec3 Cone(Vec3 axis, float spread, float r1, float r2)
		{
			Vec3 vec = axis.Normalized();
			Vec3 vec2 = ((Math.Abs(vec.Z) < 0.9f) ? Vec3.Cross(new Vec3(0f, 0f, 1f), vec).Normalized() : new Vec3(1f, 0f, 0f));
			Vec3 vec3 = Vec3.Cross(vec, vec2);
			float num = r1 * 6.2832f;
			float num2 = spread * (float)Math.Sqrt(r2);
			return (vec + vec2 * ((float)Math.Cos(num) * num2) + vec3 * ((float)Math.Sin(num) * num2)).Normalized();
		}

		public static Vec3 Port(MeshModel model, int side, int shot)
		{
			if (!PortCache.TryGetValue(model, out var value))
			{
				value = FindPorts(model);
				if (PortCache.Count > 64)
				{
					PortCache.Clear();
				}
				PortCache[model] = value;
			}
			int num = Order[(shot % 5 + 5) % 5];
			return value[((side <= 0) ? 5 : 0) + num];
		}

		private static Vec3[] FindPorts(MeshModel model)
		{
			Vec3[] array = new Vec3[10];
			MeshInstance m = new MeshInstance
			{
				Model = model
			};
			float y = model.Min.Y;
			float num = Math.Max(1f, model.Max.Y - model.Min.Y);
			float num2 = Math.Max(Math.Abs(model.Min.X), Math.Abs(model.Max.X)) + 1f;
			float[] array2 = new float[4] { 0.85f, 0.65f, 0.48f, 0.32f };
			for (int i = 0; i < 2; i++)
			{
				float num3 = ((i == 0) ? 1f : (-1f));
				for (int j = 0; j < 5; j++)
				{
					float y2 = y + num * (0.3f + 0.38f * ((float)j + 0.5f) / 5f);
					Vec3 vec = new Vec3(num3 * (num2 - 1f) * 0.8f, y2, 0.7f);
					float[] array3 = array2;
					foreach (float z in array3)
					{
						Vec3 vec2 = new Vec3(num3 * num2, y2, z);
						Vec3 vec3 = new Vec3(0f, y2, z);
						if (MeshRay.Segment(m, vec2, vec3, out var hit))
						{
							vec = vec2 + (vec3 - vec2) * hit + new Vec3(num3 * 0.14f, 0f, 0f);
							break;
						}
					}
					array[i * 5 + j] = vec;
				}
			}
			return array;
		}

		public static float Heel(float t, int side, float strength = 1f)
		{
			if (t < 0f || t > 4f)
			{
				return 0f;
			}
			float num = (1f - (float)Math.Exp((0f - t) / 0.045f)) * (float)Math.Exp((0f - t) / 0.75f) * (float)Math.Cos(t * 6.2832f / 1.5f);
			return (float)(-side) * 0.075f * strength * num;
		}

		public static void FoamRing(FxSink sink, Vec3 c, float radius, float size, float alpha, uint seed, int n, uint color)
		{
			if (!(alpha < 0.02f))
			{
				byte alpha2 = (byte)Math.Max(2, Math.Min(250, (int)(alpha * 250f)));
				for (int i = 0; i < n; i++)
				{
					float num = ((float)i + R(seed, i) * 0.6f) / (float)n * 6.2832f;
					float num2 = radius * (0.86f + 0.28f * R(seed, i + 50));
					float x = c.X + (float)Math.Cos(num) * num2;
					float y = c.Y + (float)Math.Sin(num) * num2;
					float w = size * (0.9f + 0.5f * R(seed, i + 90));
					sink.Sprite(BlastFx.Soft(i + (int)(seed & 7)), x, y, c.Z + size * 0.1f, w, size * 0.34f, color, alpha2, emissive: false, additive: false, (i & 1) != 0);
				}
			}
		}
	}
}
