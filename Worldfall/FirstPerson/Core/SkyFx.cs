using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class SkyFx
	{
		private static SpriteImage _rock;

		public static void Meteor(List<Billboard> bills, List<PointLight> lights, Vec3 head, Vec3 back, float size, float time, uint seed, SpriteImage rock, float groundZ)
		{
			for (int i = 0; i < 6; i++)
			{
				uint num = TerrainModel.Hash((int)seed + i * 131, i * 7 + 3);
				float num2 = time * 9f + (float)i * 1.05f;
				Vec3 vec = head + new Vec3((float)Math.Cos(num2), (float)Math.Sin(num2), (float)Math.Sin(num2 * 1.3f)) * (size * 0.18f);
				float num3 = size * (0.75f + 0.25f * (float)((num >> 8) & 0xFF) / 255f);
				bills.Add(new Billboard
				{
					X = vec.X,
					Y = vec.Y,
					Z = vec.Z - num3 * 0.5f,
					GroundZ = groundZ,
					Width = num3,
					Height = num3,
					Far = true,
					Image = DragonBreath.Ball((i >= 3) ? 1 : 0, (int)num),
					Kind = BillboardKind.Facing,
					Emissive = true,
					Tag = -1
				});
			}
			if (rock != null)
			{
				float num4 = size * 0.7f;
				bills.Add(new Billboard
				{
					X = head.X - back.X * 0.05f,
					Y = head.Y - back.Y * 0.05f,
					Z = head.Z - num4 * 0.5f,
					GroundZ = groundZ,
					Width = num4,
					Height = num4,
					Far = true,
					Image = rock,
					Kind = BillboardKind.Facing,
					Emissive = true,
					Tag = -1
				});
			}
			for (int j = 1; j <= 30; j++)
			{
				uint num5 = TerrainModel.Hash((int)seed + j * 7919, j * 13 + 1);
				float num6 = (float)j / 30f;
				float num7 = size * (0.35f + num6 * num6 * 14f);
				float num8 = size * (0.08f + 0.6f * num6);
				float num9 = time * (6f + (float)(num5 >> 28)) + (float)j * 1.7f;
				Vec3 vec2 = Vec3.Cross(back, new Vec3(0f, 0f, 1f));
				if (Vec3.Dot(vec2, vec2) < 0.0001f)
				{
					vec2 = new Vec3(1f, 0f, 0f);
				}
				vec2 = vec2.Normalized();
				Vec3 vec3 = Vec3.Cross(vec2, back);
				Vec3 vec4 = head + back * num7 + vec2 * ((float)Math.Sin(num9) * num8) + vec3 * ((float)Math.Cos(num9 * 0.8f) * num8);
				float num10 = size * (0.8f + 1.8f * num6) * (0.8f + 0.4f * (float)((num5 >> 8) & 0xFF) / 255f);
				if (num6 < 0.45f)
				{
					bills.Add(new Billboard
					{
						X = vec4.X,
						Y = vec4.Y,
						Z = vec4.Z - num10 * 0.5f,
						GroundZ = groundZ,
						Width = num10,
						Height = num10,
						Far = true,
						Image = DragonBreath.Ball((!(num6 < 0.15f)) ? ((num6 < 0.3f) ? 1 : 2) : 0, (int)num5),
						Kind = BillboardKind.Facing,
						Emissive = true,
						Tag = -1
					});
				}
				else
				{
					int shade = Math.Min(5, (int)((num6 - 0.45f) / 0.55f * 6f * 0.8f));
					bills.Add(new Billboard
					{
						X = vec4.X,
						Y = vec4.Y,
						Z = vec4.Z - num10 * 0.5f,
						GroundZ = groundZ,
						Width = num10,
						Height = num10,
						Far = true,
						Image = FireField.SoftSmoke(shade, (int)num5),
						Kind = BillboardKind.Facing,
						Tag = -1,
						Alpha = (byte)Math.Max(1, (int)(190f * (1f - (num6 - 0.45f) / 0.55f)))
					});
				}
			}
			float num11 = 0.85f + 0.15f * (float)Math.Sin(time * 27f + (float)seed);
			lights.Add(new PointLight
			{
				X = head.X,
				Y = head.Y,
				Z = head.Z,
				Radius = 10f + size * 3f,
				R = 1.3f * num11,
				G = 0.8f * num11,
				B = 0.4f * num11,
				Glow = size * 0.9f
			});
		}

		public static SpriteImage Rock()
		{
			if (_rock != null)
			{
				return _rock;
			}
			uint[] array = new uint[144];
			for (int i = 0; i < 12; i++)
			{
				for (int j = 0; j < 12; j++)
				{
					float num = ((float)j + 0.5f) / 12f * 2f - 1f;
					float num2 = ((float)i + 0.5f) / 12f * 2f - 1f;
					float num3 = (float)Math.Atan2(num2, num);
					float num4 = 0.82f + 0.1f * (float)Math.Sin(num3 * 4f + 1f) + 0.06f * (float)Math.Sin(num3 * 7f);
					float num5 = (float)Math.Sqrt(num * num + num2 * num2) / num4;
					if (!(num5 > 1f))
					{
						uint num6 = TerrainModel.Hash(j * 5 + 1, i * 9 + 2);
						uint num7 = (((num6 & 7) == 0 || Math.Abs((float)Math.Sin((float)j * 1.3f + (float)i * 0.9f)) < 0.12f) ? TerrainCache.Rgb(255, 150, 50) : TerrainCache.Scale(TerrainCache.Rgb(70, 56, 50), 0.8f + 0.4f * (1f - num5) + (float)(num6 & 3) * 0.04f));
						array[i * 12 + j] = num7 | 0xFF000000u;
					}
				}
			}
			SpriteImage spriteImage = new SpriteImage();
			spriteImage.Width = 12;
			spriteImage.Height = 12;
			spriteImage.Pixels = array;
			spriteImage.PivotX = 0.5f;
			spriteImage.UnitWidth = 1f;
			spriteImage.UnitHeight = 1f;
			spriteImage.MeasureOpaqueRows();
			_rock = spriteImage;
			return spriteImage;
		}
	}
}
