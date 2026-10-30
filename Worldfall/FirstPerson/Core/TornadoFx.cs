using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class TornadoFx
	{
		private readonly struct Shape
		{
			public readonly float X;

			public readonly float Y;

			public readonly float Z;

			public readonly float Height;

			public readonly float Foot;

			public readonly float Top;

			public readonly float Low;

			public readonly float Rope;

			public readonly float Sway;

			public readonly float Time;

			private readonly float _tx;

			private readonly float _ty;

			private readonly float _seed;

			public Shape(in Twister t, float s, float form, float height, float time)
			{
				X = t.X;
				Y = t.Y;
				Z = t.Z;
				Height = height;
				Time = time;
				Rope = (t.Dying ? (0.3f + 0.7f * form) : 1f);
				Foot = FootRadius(s) * Rope;
				Top = TopRadius(s) * (0.55f + 0.45f * Rope);
				Low = 1f - form;
				Sway = (0.5f + 0.45f * Math.Min(s, 2f)) * (t.Dying ? (2.2f - form) : 1f);
				_tx = t.TopX - t.X;
				_ty = t.TopY - t.Y;
				_seed = (float)(t.Seed & 0x3FF) * 0.37f;
			}

			public float Radius(float hk)
			{
				hk = Math.Max(0f, Math.Min(1f, hk));
				return Foot + (Top - Foot) * (float)Math.Pow(hk, 1.35) + Top * 0.45f * BlastFx.Smooth((hk - 0.78f) / 0.22f);
			}

			public void Axis(float hk, out float x, out float y)
			{
				float num = (float)Math.Pow(Math.Max(0f, hk), 1.5);
				float num2 = (float)Math.Sin(Math.PI * (double)Math.Min(1f, hk)) * 0.85f + 0.15f * hk;
				float time = Time;
				float num3 = (float)(Math.Sin(time * 0.7f + hk * 2.4f + _seed) * 0.8999999761581421 + Math.Sin(time * 1.9f + hk * 5f) * 0.30000001192092896);
				float num4 = (float)(Math.Cos(time * 0.55f + hk * 2.1f + _seed * 0.7f) * 0.8999999761581421 + Math.Cos(time * 1.6f + hk * 4f) * 0.30000001192092896);
				x = X + _tx * num + num3 * num2 * Sway;
				y = Y + _ty * num + num4 * num2 * Sway;
			}
		}

		private const float Tau = MathF.PI * 2f;

		public const float StormOpacity = 1.35f;

		public static readonly uint StormColor = TerrainCache.Rgb(112, 122, 124);

		internal static int Parts = 63;

		public static float FootRadius(float s)
		{
			return 0.45f + 1.4f * s;
		}

		public static float TopRadius(float s)
		{
			return 3.4f + 2.4f * s;
		}

		public static float DustRadius(float s)
		{
			return 1.8f + 5f * s;
		}

		public static float WallRadius(float s)
		{
			return TopRadius(s) * 2.6f + 4f;
		}

		public static float StormBase(float s)
		{
			return 28f + 4f * Math.Min(Math.Max(s, 0.1f), 3f);
		}

		public static float StormThickness(float s)
		{
			return 14f + 2f * Math.Min(Math.Max(s, 0.1f), 3f);
		}

		public static float StormHalf(float s)
		{
			return 48f + 14f * Math.Min(Math.Max(s, 0.1f), 3f);
		}

		public static Vortex Wind(in Twister t)
		{
			float num = Math.Max(0.1f, Math.Min(5f, t.Scale));
			float num2 = 0.3f + 0.7f * BlastFx.Clamp01(t.Form);
			return new Vortex
			{
				X = t.X,
				Y = t.Y,
				Core = Math.Max(1.2f, DustRadius(num) * 0.8f),
				Reach = 30f + 12f * Math.Min(num, 3f),
				Speed = (12f + 4f * Math.Min(num, 2f)) * num2
			};
		}

		private static float Spin(float s)
		{
			return 3.4f / (0.6f + 0.4f * s);
		}

		public static void Draw(List<Billboard> bills, in Twister t, float time, float eyeX, float eyeY, Vec3 light, float sunny, float detail)
		{
			detail = Math.Max(0.3f, Math.Min(1f, detail));
			float s = Math.Max(0.1f, Math.Min(5f, t.Scale));
			float form = BlastFx.Clamp01(t.Form);
			float height = Math.Max(6f, t.Base - t.Z);
			float num = t.X - eyeX;
			float num2 = t.Y - eyeY;
			float num3 = (float)Math.Sqrt(num * num + num2 * num2);
			Shape sh = new Shape(in t, s, form, height, time);
			int parts = Parts;
			if ((parts & 1) != 0)
			{
				Funnel(bills, in t, in sh, time, eyeX, eyeY, light, sunny, detail, num3, (parts & 2) != 0);
			}
			if ((parts & 4) != 0)
			{
				WallCloud(bills, in t, in sh, time, detail, num3);
			}
			if ((parts & 8) != 0)
			{
				DustCloud(bills, in t, in sh, time, detail, num3);
			}
			if ((parts & 0x10) != 0 && num3 < 75f)
			{
				Debris(bills, in t, in sh, time, detail, num3);
			}
			if ((parts & 0x20) != 0 && num3 < 60f)
			{
				Inflow(bills, in t, in sh, time, detail);
			}
		}

		private static void Funnel(List<Billboard> bills, in Twister t, in Shape sh, float time, float eyeX, float eyeY, Vec3 light, float sunny, float detail, float dist, bool ragged)
		{
			float s = Math.Max(0.1f, Math.Min(5f, t.Scale));
			sh.Axis(0.5f, out var x, out var y);
			float num = eyeX - x;
			float num2 = eyeY - y;
			float num3 = (float)Math.Sqrt(num * num + num2 * num2);
			if (num3 < 0.001f)
			{
				num = 1f;
				num2 = 0f;
				num3 = 1f;
			}
			num /= num3;
			num2 /= num3;
			float num4 = 0f - num2;
			float num5 = num;
			float num6 = (float)Math.Sqrt(light.X * light.X + light.Y * light.Y);
			int light2 = 8;
			if (sunny * num6 > 0.3f && light.Z > -0.05f)
			{
				light2 = ((int)Math.Round((float)Math.Atan2(light.X * num4 + light.Y * num5, light.X * num + light.Y * num2) / (MathF.PI / 4f)) % 8 + 8) % 8;
			}
			float num7 = time * Spin(s) * 3f / (MathF.PI * 2f);
			int num8 = (int)Math.Floor(((double)num7 - Math.Floor(num7)) * 12.0);
			uint num9 = TerrainCache.Rgb(226, 236, 232);
			uint a = DirtColour(in t);
			bool far = dist > 48f;
			int num10 = ((detail >= 0.85f) ? 30 : ((detail >= 0.55f) ? 22 : 14));
			for (int i = 0; i < num10; i++)
			{
				float num11 = (float)i / (float)num10;
				float num12 = (float)(i + 1) / (float)num10;
				float num13 = (num11 + num12) * 0.5f;
				if (!(num12 <= sh.Low))
				{
					float num14 = ((num11 < sh.Low) ? ((num12 - sh.Low) / (num12 - num11)) : 1f);
					float num15 = sh.Radius(num13) * (0.45f + 0.55f * num14);
					sh.Axis(num13, out var x2, out var y2);
					float z = sh.Z + num11 * sh.Height;
					float height = (num12 - num11) * sh.Height;
					uint multiply = ((num13 < 0.3f) ? TerrainCache.Lerp(a, num9, BlastFx.Smooth(num13 / 0.3f) * 0.85f + 0.15f) : num9);
					int phase = ((num8 - i * 4) % 12 + 12) % 12;
					bills.Add(new Billboard
					{
						X = x2,
						Y = y2,
						Z = z,
						GroundZ = sh.Z,
						Width = num15 * 2.3f,
						Height = height,
						Image = FunnelArt.Strip(light2, phase),
						Kind = BillboardKind.Facing,
						Tag = -1,
						Multiply = multiply,
						Far = far,
						Alpha = (byte)Math.Max(1, (int)(255f * num14))
					});
				}
			}
			if (!ragged)
			{
				return;
			}
			int num16 = ((!(detail >= 0.85f)) ? 1 : 2);
			int num17 = ((detail >= 0.55f) ? 1 : 2);
			for (int j = 0; j < num10; j += num17)
			{
				float num18 = ((float)j + 0.5f) / (float)num10;
				if (!(num18 <= sh.Low + 0.03f))
				{
					float num19 = sh.Radius(num18);
					sh.Axis(num18, out var x3, out var y3);
					float num20 = Spin(s) * (1.35f - 0.6f * num18);
					for (int k = 0; k < num16; k++)
					{
						uint num21 = TerrainModel.Hash((int)t.Seed + j * 977 + k * 31, j * 7 + k);
						float num22 = time * num20 + (float)k * (MathF.PI * 2f / (float)num16) + (float)j * 0.83f + (float)((num21 >> 4) & 0xFF) / 255f;
						float num23 = num19 * (0.82f + 0.25f * (float)((num21 >> 12) & 0xFF) / 255f);
						float num24 = (num19 * 0.95f + 0.25f) * (0.8f + 0.4f * (float)((num21 >> 20) & 0xFF) / 255f);
						uint multiply2 = TerrainCache.Scale((num18 < 0.3f) ? TerrainCache.Lerp(a, num9, num18 / 0.3f) : num9, 0.85f);
						bills.Add(new Billboard
						{
							X = x3 + (float)Math.Cos(num22) * num23,
							Y = y3 + (float)Math.Sin(num22) * num23,
							Z = sh.Z + num18 * sh.Height - num24 * 0.5f,
							GroundZ = sh.Z,
							Width = num24,
							Height = num24,
							Image = FireField.SoftSmoke((int)((num21 >> 2) & 1), (int)(num21 & 3)),
							Kind = BillboardKind.Facing,
							Flip = ((num21 & 1) != 0),
							Tag = -1,
							Multiply = multiply2,
							Alpha = (byte)(70 + (num21 >> 27) * 6),
							Far = far
						});
					}
				}
			}
		}

		private static void WallCloud(List<Billboard> bills, in Twister t, in Shape sh, float time, float detail, float dist)
		{
			float num = Math.Max(0.1f, Math.Min(5f, t.Scale));
			sh.Axis(1f, out var x, out var y);
			float num2 = sh.Z + sh.Height;
			float num3 = WallRadius(num);
			float num4 = 1.6f + 0.9f * Math.Min(num, 3f);
			uint multiply = TerrainCache.Rgb(112, 120, 124);
			bool far = dist > 40f;
			int num5 = ((detail >= 0.85f) ? 26 : ((detail >= 0.55f) ? 18 : 12));
			for (int i = 0; i < num5; i++)
			{
				uint num6 = TerrainModel.Hash((int)t.Seed + i * 4441, i * 3 + 9);
				float num7 = (float)Math.Sqrt(((float)i + 0.5f) / (float)num5);
				float num8 = num3 * (0.12f + 0.88f * num7) * (0.85f + 0.3f * (float)((num6 >> 6) & 0xFF) / 255f);
				float num9 = time * 0.3f * (1.2f - 0.6f * num7) + (float)i * 2.39996f;
				float num10 = sh.Top * 1.2f + num3 * 0.32f * (0.8f + 0.4f * (float)((num6 >> 14) & 0xFF) / 255f);
				float num11 = num4 * (1f - num7 * num7) + 0.4f * (float)((num6 >> 22) & 3);
				bills.Add(new Billboard
				{
					X = x + (float)Math.Cos(num9) * num8,
					Y = y + (float)Math.Sin(num9) * num8,
					Z = num2 - num11 - num10 * 0.22f,
					GroundZ = sh.Z,
					Width = num10 * 1.45f,
					Height = num10 * 0.6f,
					Image = FireField.SoftSmoke((int)((num6 >> 20) & 1), (int)(num6 & 3)),
					Kind = BillboardKind.Facing,
					Flip = ((num6 & 1) != 0),
					Tag = -1,
					Multiply = multiply,
					Alpha = (byte)(205 + ((num6 >> 25) & 0x1F)),
					Far = far
				});
			}
			int num12 = ((detail >= 0.55f) ? 10 : 4);
			for (int j = 0; j < num12; j++)
			{
				uint num13 = TerrainModel.Hash((int)t.Seed + j * 6151, j * 5 + 21);
				float num14 = 5f + (float)((num13 >> 3) & 7) * 0.6f;
				float num15 = Frac(time / num14 + (float)((num13 >> 8) & 0x3FF) / 1023f);
				float num16 = num3 * (1.1f - 0.8f * num15);
				float num17 = (float)((num13 >> 18) & 0x3FF) / 1023f * (MathF.PI * 2f) + num15 * 2.2f + time * 0.3f;
				float num18 = 1.2f + sh.Top * 0.5f;
				float num19 = (float)Math.Sin(Math.PI * (double)num15);
				bills.Add(new Billboard
				{
					X = x + (float)Math.Cos(num17) * num16,
					Y = y + (float)Math.Sin(num17) * num16,
					Z = num2 - num4 - 0.6f - 1.4f * (float)((num13 >> 28) & 3) / 3f - num18 * 0.3f,
					GroundZ = sh.Z,
					Width = num18 * 1.8f,
					Height = num18 * 0.55f,
					Image = FireField.SoftSmoke(1, (int)(num13 & 3)),
					Kind = BillboardKind.Facing,
					Flip = ((num13 & 2) != 0),
					Tag = -1,
					Multiply = multiply,
					Alpha = (byte)Math.Max(1, (int)(150f * num19)),
					Far = far
				});
			}
		}

		private static uint DirtColour(in Twister t)
		{
			if (t.Water)
			{
				return TerrainCache.Rgb(168, 178, 188);
			}
			return TerrainCache.Lerp(TerrainCache.Rgb(150, 112, 76), t.Dust, 0.22f);
		}

		private static void DustCloud(List<Billboard> bills, in Twister t, in Shape sh, float time, float detail, float dist)
		{
			float num = Math.Max(0.1f, Math.Min(5f, t.Scale));
			float num2 = BlastFx.Clamp01(t.Form);
			float num3 = BlastFx.Smooth(num2 * 1.6f) * (t.Dying ? (0.4f + 0.6f * num2) : 1f);
			if (num3 < 0.02f)
			{
				return;
			}
			float num4 = DustRadius(num) * (0.45f + 0.55f * num3);
			float num5 = Math.Min(sh.Height * 0.4f, 2.6f + 4f * num);
			uint num6 = DirtColour(in t);
			uint num7 = (t.Water ? TerrainCache.Rgb(196, 204, 212) : TerrainCache.Lerp(num6, TerrainCache.Rgb(196, 146, 104), 0.35f));
			uint num8 = (t.Water ? TerrainCache.Rgb(132, 142, 152) : TerrainCache.Scale(num6, 0.62f));
			float num9 = Spin(num) * 0.75f;
			bool far = dist > 40f;
			int num10 = ((detail >= 0.85f) ? 24 : ((detail >= 0.55f) ? 16 : 10));
			for (int i = 0; i < num10; i++)
			{
				uint num11 = TerrainModel.Hash((int)t.Seed + i * 2203, i * 11 + 4);
				float num12 = 2.2f + (float)((num11 >> 3) & 7) * 0.2f;
				float num13 = Frac(time / num12 + (float)((num11 >> 8) & 0x3FF) / 1023f);
				float num14 = num4 * (0.85f - 0.6f * num13) * (0.55f + 0.45f * (float)((num11 >> 18) & 0xFF) / 255f);
				float num15 = (float)((num11 >> 26) & 0x3F) / 63f * (MathF.PI * 2f) + time * num9 + num13 * 2.4f;
				float num16 = num5 * (float)Math.Pow(num13, 0.75) * (0.3f + 0.7f * (float)((num11 >> 13) & 0x1F) / 31f);
				float num17 = (0.9f + num4 * 0.42f) * (0.7f + 0.6f * num13);
				float num18 = ((num13 < 0.12f) ? (num13 / 0.12f) : (1f - BlastFx.Smooth((num13 - 0.7f) / 0.3f))) * (1f - 0.45f * num16 / Math.Max(0.1f, num5));
				bool flag = (num11 & 8) != 0;
				uint num19 = ((num16 > num5 * 0.55f && (num11 & 0x10) != 0) ? num7 : (((num11 & 4) != 0) ? num6 : num8));
				if (flag)
				{
					num19 = TerrainCache.Scale(num19, 0.78f);
				}
				bills.Add(new Billboard
				{
					X = sh.X + (float)Math.Cos(num15) * num14,
					Y = sh.Y + (float)Math.Sin(num15) * num14,
					Z = sh.Z + num16 - num17 * 0.35f,
					GroundZ = sh.Z,
					Width = num17 * 1.2f,
					Height = num17,
					Image = (flag ? BlastFx.Puff((int)(num11 >> 4)) : FireField.SoftSmoke((int)(3 + ((num11 >> 21) & 1)) + ((num13 > 0.5f) ? 1 : 0), (int)(num11 & 3))),
					Kind = BillboardKind.Facing,
					Flip = ((num11 & 1) != 0),
					Tag = -1,
					Multiply = num19,
					Alpha = (byte)Math.Max(1, (int)((float)(flag ? 185u : (150 + (num11 & 0x3F))) * num18 * num3)),
					Far = far
				});
			}
			int num20 = ((detail >= 0.85f) ? 18 : ((detail >= 0.55f) ? 12 : 7));
			for (int j = 0; j < num20; j++)
			{
				uint num21 = TerrainModel.Hash((int)t.Seed + j * 3307, j * 13 + 8);
				float num22 = num4 * (0.65f + 0.5f * (float)((num21 >> 5) & 0xFF) / 255f);
				float num23 = (float)j * (MathF.PI * 2f / (float)num20) + time * num9 * 0.8f + (float)((num21 >> 14) & 0xFF) / 255f;
				float num24 = 0.8f + num4 * 0.38f;
				float num25 = 0.75f + 0.25f * (float)Math.Sin(time * 1.7f + (float)j * 1.3f);
				bills.Add(new Billboard
				{
					X = sh.X + (float)Math.Cos(num23) * num22,
					Y = sh.Y + (float)Math.Sin(num23) * num22,
					Z = sh.Z - num24 * 0.12f,
					GroundZ = sh.Z,
					Width = num24 * 1.9f,
					Height = num24 * 0.6f * num25,
					Image = FireField.SoftSmoke(4, (int)(num21 & 3)),
					Kind = BillboardKind.Facing,
					Flip = ((num21 & 1) != 0),
					Tag = -1,
					Multiply = num6,
					Alpha = (byte)Math.Max(1, (int)((float)(95 + (num21 >> 27) * 4) * num3)),
					Far = far
				});
			}
		}

		private static void Inflow(List<Billboard> bills, in Twister t, in Shape sh, float time, float detail)
		{
			float s = Math.Max(0.1f, Math.Min(5f, t.Scale));
			float num = BlastFx.Smooth(BlastFx.Clamp01(t.Form) * 1.5f);
			if (!(num < 0.05f))
			{
				float num2 = DustRadius(s);
				uint multiply = DirtColour(in t);
				int num3 = ((detail >= 0.85f) ? 16 : 8);
				for (int i = 0; i < num3; i++)
				{
					uint num4 = TerrainModel.Hash((int)t.Seed + i * 1597, i * 17 + 3);
					float num5 = 1.6f + (float)((num4 >> 3) & 7) * 0.15f;
					float num6 = Frac(time / num5 + (float)((num4 >> 8) & 0x3FF) / 1023f);
					float num7 = num2 * (1.7f + 1.3f * (float)((num4 >> 18) & 0xFF) / 255f);
					float num8 = num7 + (num2 * 0.55f - num7) * (float)Math.Pow(num6, 0.85);
					float num9 = (float)((num4 >> 26) & 0x3F) / 63f * (MathF.PI * 2f) + num6 * 1.4f;
					float num10 = 0.9f + num2 * 0.2f;
					float num11 = (float)Math.Sin(Math.PI * (double)num6);
					bills.Add(new Billboard
					{
						X = sh.X + (float)Math.Cos(num9) * num8,
						Y = sh.Y + (float)Math.Sin(num9) * num8,
						Z = sh.Z + 0.05f + 0.3f * (float)((num4 >> 14) & 3) / 3f,
						GroundZ = sh.Z,
						Width = num10 * 2.2f,
						Height = num10 * 0.45f,
						Image = BlastFx.Soft((int)(num4 & 7)),
						Kind = BillboardKind.Facing,
						Flip = ((num4 & 1) != 0),
						Tag = -1,
						Multiply = multiply,
						Alpha = (byte)Math.Max(1, (int)(85f * num11 * num))
					});
				}
			}
		}

		private static void Debris(List<Billboard> bills, in Twister t, in Shape sh, float time, float detail, float dist)
		{
			float s = Math.Max(0.1f, Math.Min(5f, t.Scale));
			float num = BlastFx.Smooth(BlastFx.Clamp01(t.Form) * 1.4f);
			if (num < 0.1f || t.Water)
			{
				return;
			}
			int num2 = (int)Math.Round((float)((dist < 35f) ? 72 : 40) * detail * num);
			float num3 = Spin(s) * 1.1f;
			float num4 = DustRadius(s);
			for (int i = 0; i < num2; i++)
			{
				uint num5 = TerrainModel.Hash((int)t.Seed + i * 613, i * 29 + 2);
				float num6 = 3.2f + (float)(num5 & 7) * 0.35f;
				float num7 = Frac(time / num6 + (float)((num5 >> 8) & 0x3FF) / 1023f);
				int num8 = (int)((num5 >> 3) % 10);
				float num9 = 0.18f + 0.62f * (float)((num5 >> 20) & 0xFF) / 255f;
				if (num8 >= 6)
				{
					num9 *= 0.7f;
				}
				float num10 = 0.55f + 0.3f * (float)((num5 >> 28) & 0xF) / 15f;
				float num11 = (float)((num5 >> 12) & 0xFF) / 255f * (MathF.PI * 2f);
				float tumble = time * (5f + (float)(num5 & 0xF)) + (float)i;
				float x2;
				float y2;
				float num16;
				if (num7 < num10)
				{
					float num12 = num7 / num10;
					float num13 = num9 * (float)Math.Pow(num12, 0.8);
					float num14 = Math.Max(sh.Radius(num13) * 1.4f, num4 * 0.55f * (1f - num13 * 1.6f)) * (0.9f + 0.3f * (float)((num5 >> 16) & 0xF) / 15f);
					float num15 = num11 + time * num3 * (1.1f - num13 * 0.5f);
					sh.Axis(num13, out var x, out var y);
					x2 = x + (float)Math.Cos(num15) * num14;
					y2 = y + (float)Math.Sin(num15) * num14;
					num16 = sh.Z + 0.2f + num13 * sh.Height;
				}
				else
				{
					float num17 = (num7 - num10) * num6;
					float num18 = time - num17;
					float num19 = num9;
					float num20 = Math.Max(sh.Radius(num19) * 1.4f, num4 * 0.55f * (1f - num19 * 1.6f));
					float num21 = num11 + num18 * num3 * (1.1f - num19 * 0.5f);
					sh.Axis(num19, out var x3, out var y3);
					float num22 = x3 + (float)Math.Cos(num21) * num20;
					float num23 = y3 + (float)Math.Sin(num21) * num20;
					float num24 = sh.Z + 0.2f + num19 * sh.Height;
					float num25 = Math.Min(16f, num3 * num20 * 1.1f);
					float num26 = 3f + 3f * (float)((num5 >> 24) & 0xF) / 15f;
					float num27 = 0f - (float)Math.Sin(num21);
					float num28 = (float)Math.Cos(num21);
					x2 = num22 + (num27 * num25 + (float)Math.Cos(num21) * num26) * num17;
					y2 = num23 + (num28 * num25 + (float)Math.Sin(num21) * num26) * num17;
					num16 = num24 + 2.5f * num17 - 4.9f * num17 * num17;
					if (num16 < sh.Z - 0.5f)
					{
						continue;
					}
				}
				DebrisPiece(bills, num8, num5, x2, y2, num16, sh.Z, tumble);
			}
		}

		private static void DebrisPiece(List<Billboard> bills, int kind, uint h, float x, float y, float z, float ground, float tumble)
		{
			uint multiply = 0u;
			int frame = (int)(tumble * 0.9f) & 3;
			SpriteImage image;
			float width;
			float num;
			if (kind <= 3)
			{
				image = DebrisArt.Clod((int)((h >> 9) & 3));
				width = (num = 0.13f + 0.1f * (float)((h >> 13) & 3));
			}
			else if (kind <= 5)
			{
				image = DebrisArt.Branch(frame);
				width = (num = 0.45f + 0.15f * (float)((h >> 13) & 3));
			}
			else if (kind <= 7)
			{
				image = DebrisArt.Plank(frame);
				width = (num = 0.55f + 0.2f * (float)((h >> 13) & 3));
				multiply = (((h & 0x10000) != 0) ? TerrainCache.Rgb(170, 118, 70) : TerrainCache.Rgb(128, 92, 60));
			}
			else if (kind == 8)
			{
				image = DebrisArt.Board(frame);
				width = (num = 0.3f + 0.08f * (float)((h >> 13) & 3));
				multiply = (((h & 0x10000) != 0) ? TerrainCache.Rgb(168, 72, 56) : TerrainCache.Rgb(110, 104, 112));
			}
			else
			{
				image = DebrisArt.Stone();
				width = (num = 0.12f + 0.05f * (float)((h >> 13) & 3));
			}
			bills.Add(new Billboard
			{
				X = x,
				Y = y,
				Z = z - num * 0.5f,
				GroundZ = ground,
				Width = width,
				Height = num,
				Image = image,
				Kind = BillboardKind.Facing,
				Tag = -1,
				Multiply = multiply,
				Flip = ((h & 0x20000) != 0)
			});
		}

		private static float Frac(float v)
		{
			return v - (float)Math.Floor(v);
		}
	}
}
