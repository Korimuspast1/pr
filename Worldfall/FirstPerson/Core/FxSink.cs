using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class FxSink
	{
		public List<Billboard> Billboards;

		public List<PointLight> Lights;

		public List<MeshInstance> Meshes;

		public List<Streak> Streaks;

		public float EyeX;

		public float EyeY;

		public float EyeZ;

		public float Yaw;

		public Func<string, MeshModel> Model;

		public Func<float, float, float> Ground;

		public float Day = 1f;

		public float Flash;

		public uint FlashColor = TerrainCache.Rgb(255, 248, 232);

		public float Shake;

		public const int OwnFadeTag = -2;

		public void AddMesh(MeshInstance m)
		{
			m.Tag = -2;
			Meshes.Add(m);
		}

		public void Begin()
		{
			Flash = 0f;
			Shake = 0f;
		}

		public void AddFlash(float strength, uint color)
		{
			if (!(strength <= Flash))
			{
				Flash = Math.Min(1f, strength);
				FlashColor = color;
			}
		}

		public float Distance(float x, float y)
		{
			float num = x - EyeX;
			float num2 = y - EyeY;
			return (float)Math.Sqrt(num * num + num2 * num2);
		}

		public float Facing(float x, float y)
		{
			float num = x - EyeX;
			float num2 = y - EyeY;
			float num3 = (float)Math.Sqrt(num * num + num2 * num2);
			if (num3 < 0.001f)
			{
				return 1f;
			}
			return (num * (float)Math.Cos(Yaw) + num2 * (float)Math.Sin(Yaw)) / num3;
		}

		public void Light(float x, float y, float z, float radius, uint color, float strength, float glow = 0f)
		{
			if (!(strength <= 0.01f))
			{
				Lights.Add(new PointLight
				{
					X = x,
					Y = y,
					Z = z,
					Radius = radius,
					Glow = glow,
					R = (float)(color & 0xFF) / 255f * strength,
					G = (float)((color >> 8) & 0xFF) / 255f * strength,
					B = (float)((color >> 16) & 0xFF) / 255f * strength
				});
			}
		}

		public void Sprite(SpriteImage img, float x, float y, float z, float w, float h, uint multiply, byte alpha, bool emissive, bool additive = false, bool flip = false)
		{
			if (img != null && !(w <= 0f) && !(h <= 0f))
			{
				float num = x - EyeX;
				float num2 = y - EyeY;
				Billboards.Add(new Billboard
				{
					X = x,
					Y = y,
					Z = z - h * 0.5f,
					GroundZ = z - h * 0.5f,
					Width = w,
					Height = h,
					Image = img,
					Kind = BillboardKind.Facing,
					Tag = -1,
					Emissive = emissive,
					Multiply = multiply,
					Alpha = alpha,
					Additive = (additive && alpha != 0),
					Flip = flip,
					Far = (num * num + num2 * num2 > 900f || w > 6f)
				});
			}
		}

		public void Line(Vec3 a, Vec3 b, uint color, float alphaA, float alphaB, float width, float glow = 1f)
		{
			if (Streaks != null && (!(alphaA <= 0.004f) || !(alphaB <= 0.004f)))
			{
				Streaks.Add(new Streak
				{
					A = a,
					B = b,
					Color = color,
					AlphaA = Math.Min(1f, alphaA),
					AlphaB = Math.Min(1f, alphaB),
					Width = width,
					Glow = glow
				});
			}
		}

		public void Glow(float x, float y, float z, float size, uint color, float strength)
		{
			int num = (int)(strength * 255f);
			if (num >= 2)
			{
				Sprite(BlastFx.GlowSprite, x, y, z, size, size, color, (byte)Math.Min(255, num), emissive: true, additive: true);
			}
		}
	}
}
