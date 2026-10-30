using FirstPerson.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPerson.Gpu
{
	internal sealed class GpuNightSky
	{
		private static readonly Vector3 MilkyPole = new Vector3(0.62f, -0.7f, 0.34f).normalized;

		private readonly Vector4[] _meteorA = new Vector4[8];

		private readonly Vector4[] _meteorB = new Vector4[8];

		private readonly Vector4[] _meteorC = new Vector4[8];

		private readonly Vector4[] _meteorD = new Vector4[8];

		private readonly Vector4[] _meteorE = new Vector4[8];

		private int _snowCheck = -1000;

		private float _snowNear;

		private bool _turned;

		private Vec3 _st0;

		private Vec3 _st1;

		private Vec3 _st2;

		public void SetUniforms(CommandBuffer cmd, FrameRenderer cfg, TerrainCache land, ViewState view, float focalOut)
		{
			Atmosphere air = cfg.Air;
			float num = (air.Indoors ? 0f : air.Stars);
			float time = cfg.Time;
			int nightNumber = cfg.NightNumber;
			float px = 1f / Mathf.Max(1f, focalOut);
			cmd.SetGlobalVector("_FP_Night", new Vector4(num, 0f, 0f, 0f));
			_turned = FrameRenderer.StarTurnRows(air, out _st0, out _st1, out _st2);
			cmd.SetGlobalVector("_FP_StarRot0", _turned ? new Vector4(_st0.X, _st0.Y, _st0.Z, 1f) : Vector4.zero);
			cmd.SetGlobalVector("_FP_StarRot1", _turned ? new Vector4(_st1.X, _st1.Y, _st1.Z, 0f) : Vector4.zero);
			cmd.SetGlobalVector("_FP_StarRot2", _turned ? new Vector4(_st2.X, _st2.Y, _st2.Z, 0f) : Vector4.zero);
			if (num <= 0.01f)
			{
				cmd.SetGlobalVector("_FP_CometA", Vector4.zero);
				cmd.SetGlobalVector("_FP_Meteors", Vector4.zero);
				cmd.SetGlobalVector("_FP_Aurora", Vector4.zero);
				return;
			}
			Comet(cmd, cfg, nightNumber, px);
			Meteors(cmd, cfg, nightNumber, time, px);
			if (_snowCheck != Mathf.FloorToInt(time * 2f))
			{
				_snowCheck = Mathf.FloorToInt(time * 2f);
				_snowNear = SnowAround(land, view);
			}
			float num2 = Mathf.Max(cfg.ColdSky, Mathf.Min(1f, _snowNear * 1.6f));
			uint num3 = Rand(nightNumber, 3301);
			float num4 = Mathf.Max((num3 % 4 == 0) ? (0.55f + (float)((num3 >> 8) & 3) * 0.12f) : 0f, num2 * 0.95f);
			if (cfg.ForceAurora >= 0f)
			{
				num4 = cfg.ForceAurora;
			}
			float num5 = num4 * num;
			cmd.SetGlobalVector("_FP_Aurora", new Vector4((num5 < 0.02f) ? 0f : num5, time + (float)((num3 >> 12) & 0xFF) * 3.1f, 0f, 0f));
		}

		private void Comet(CommandBuffer cmd, FrameRenderer cfg, int night, float px)
		{
			int num = FloorDiv(night, 6);
			int num2 = night - num * 6;
			float num3;
			switch (num2)
			{
			default:
				num3 = 0f;
				break;
			case 3:
				num3 = 0.75f;
				break;
			case 1:
			case 2:
				num3 = 1f;
				break;
			case 0:
				num3 = 0.7f;
				break;
			}
			float num4 = num3;
			if (cfg.ForceComet >= 0f)
			{
				num4 = cfg.ForceComet;
			}
			if (num4 <= 0.01f)
			{
				cmd.SetGlobalVector("_FP_CometA", Vector4.zero);
				return;
			}
			uint num5 = Rand(num, 1013);
			bool flag = num % 3 == 1;
			float f = (float)(num5 & 0x3FF) / 1023f * 6.2832f + (float)num2 * 0.07f;
			float f2 = 0.2f + (float)((num5 >> 10) & 0xFF) / 255f * 0.3f + (float)num2 * 0.025f;
			float num6 = Mathf.Cos(f2);
			Vector3 vector = new Vector3(Mathf.Cos(f) * num6, Mathf.Sin(f) * num6, Mathf.Sin(f2));
			if (_turned)
			{
				Vec3 vec = FrameRenderer.AmongStarsBack(_st0, _st1, _st2, new Vec3(vector.x, vector.y, vector.z));
				vector = new Vector3(vec.X, vec.Y, vec.Z);
			}
			Vec3 sunDisc = cfg.Air.SunDisc;
			Vector3 vector2 = new Vector3(sunDisc.X, sunDisc.Y, sunDisc.Z);
			Vector3 vector3 = vector * Vector3.Dot(vector, vector2) - vector2;
			Vector3 normalized = (Vector3.forward - vector * vector.z).normalized;
			if (vector3.sqrMagnitude < 0.0001f)
			{
				vector3 = normalized;
			}
			Vector3 vector4 = Vector3.Cross(vector, normalized) * (((num5 & 0x40000000) != 0) ? 1f : (-1f));
			Vector3 normalized2 = (vector3.normalized * 0.75f + vector4 * 0.55f).normalized;
			Vector3 vector5 = Vector3.Cross(vector, normalized2);
			float num7 = (flag ? 0.75f : (0.38f + (float)((num5 >> 18) & 0x3F) / 63f * 0.16f)) * (0.75f + 0.25f * num4);
			float y = (flag ? 0.2f : 0.17f);
			float z = ((((num5 >> 24) & 1) == 0) ? 1f : (-1f)) * (0.1f + (float)((num5 >> 25) & 7) * 0.02f);
			float x = Mathf.Max(1.2f * px, 0.0035f);
			float y2 = Mathf.Max(3.5f * px, flag ? 0.017f : 0.013f);
			float w = Mathf.Cos(Mathf.Min(1.4f, num7 * 1.3f + 0.08f));
			cmd.SetGlobalVector("_FP_CometA", new Vector4(vector.x, vector.y, vector.z, num4));
			cmd.SetGlobalVector("_FP_CometB", new Vector4(normalized2.x, normalized2.y, normalized2.z, num7));
			cmd.SetGlobalVector("_FP_CometC", new Vector4(vector5.x, vector5.y, vector5.z, w));
			cmd.SetGlobalVector("_FP_CometD", new Vector4(0.008f, y, z, 0f));
			cmd.SetGlobalVector("_FP_CometE", new Vector4(x, y2, 0f, 0f));
		}

		private void Meteors(CommandBuffer cmd, FrameRenderer cfg, int night, float time, float px)
		{
			bool flag = ((cfg.ForceShower >= 0f) ? (cfg.ForceShower > 0.5f) : (Rand(night, 4099) % 5 == 0));
			float num = (flag ? 0.4f : 7f);
			uint num2 = Rand(night, 811);
			float f = (float)(num2 & 0x3FF) / 1023f * 6.2832f;
			float f2 = 0.55f + (float)((num2 >> 10) & 0xFF) / 255f * 0.6f;
			Vector3 vector = new Vector3(Mathf.Cos(f) * Mathf.Cos(f2), Mathf.Sin(f) * Mathf.Cos(f2), Mathf.Sin(f2));
			int num3 = Mathf.FloorToInt(time / num);
			int num4 = (int)(5f / num) + 1;
			int num5 = 0;
			for (int i = num3 - num4; i <= num3 && num5 < 8; i++)
			{
				uint num6 = Rand(i, 2203 + night * 131);
				if ((num6 & 0xFF) > (uint)(flag ? 235 : 200))
				{
					continue;
				}
				bool flag2 = ((num6 >> 8) & 0x1F) == 0;
				float num7 = (float)i * num + (float)((num6 >> 13) & 0xFF) / 255f * num * 0.9f;
				float num8 = (flag2 ? (1.6f + (float)((num6 >> 21) & 7) * 0.08f) : (0.35f + (float)((num6 >> 21) & 7) * 0.07f));
				float num9 = (flag2 ? 3.2f : 0.25f);
				float num10 = time - num7;
				if (num10 < 0f || num10 > num8 + num9)
				{
					continue;
				}
				uint num11 = Rand((int)num6, i + 99);
				Vector3 vector4;
				Vector3 normalized2;
				if (flag && !flag2)
				{
					float f3 = (float)(num11 & 0x3FF) / 1023f * 6.2832f;
					float f4 = 0.12f + (float)((num11 >> 10) & 0xFF) / 255f * 0.7f;
					Vector3 normalized = Vector3.Cross(vector, Vector3.forward).normalized;
					Vector3 vector2 = Vector3.Cross(vector, normalized);
					Vector3 vector3 = normalized * Mathf.Cos(f3) + vector2 * Mathf.Sin(f3);
					vector4 = (vector * Mathf.Cos(f4) + vector3 * Mathf.Sin(f4)).normalized;
					normalized2 = (vector3 * Mathf.Cos(f4) - vector * Mathf.Sin(f4)).normalized;
				}
				else
				{
					float f5 = (float)(num11 & 0x3FF) / 1023f * 6.2832f;
					float f6 = 0.3f + (float)((num11 >> 10) & 0xFF) / 255f * 0.75f;
					float num12 = Mathf.Cos(f6);
					vector4 = new Vector3(Mathf.Cos(f5) * num12, Mathf.Sin(f5) * num12, Mathf.Sin(f6));
					Vector3 normalized3 = Vector3.Cross(Vector3.forward, vector4).normalized;
					Vector3 vector5 = Vector3.Cross(vector4, normalized3);
					float f7 = ((float)((num11 >> 18) & 0xFF) / 255f - 0.5f) * 2.4f;
					normalized2 = (vector5 * (0f - Mathf.Cos(f7)) + normalized3 * Mathf.Sin(f7)).normalized;
				}
				float num13 = (flag2 ? (0.24f + (float)((num11 >> 26) & 3) * 0.03f) : (0.6f + (float)((num11 >> 26) & 7) * 0.09f));
				float num14 = Mathf.Min(num10, num8);
				Vector3 normalized4 = (vector4 + normalized2 * (num13 * num14)).normalized;
				Vector3 normalized5 = (-normalized2 - normalized4 * Vector3.Dot(-normalized2, normalized4)).normalized;
				Vector3 vector6 = Vector3.Cross(normalized4, normalized5);
				float num15 = Mathf.Max(Mathf.Min(num13 * num14, flag2 ? 0.32f : (0.13f + (float)((num11 >> 6) & 7) * 0.016f)), 2f * px);
				float num17;
				float num18;
				if (num10 < num8)
				{
					float num16 = num10 / num8;
					num17 = ((num16 < 0.15f) ? (num16 / 0.15f) : (1f - (num16 - 0.15f) / 0.85f * 0.8f));
					if (flag2 && num16 > 0.6f && num16 < 0.75f)
					{
						num17 += 0.8f;
					}
					num18 = (flag2 ? (0.35f * Mathf.Min(1f, num16 * 3f)) : 0f);
				}
				else
				{
					num17 = 0f;
					float num19 = 1f - (num10 - num8) / num9;
					num18 = (flag2 ? 0.5f : 0.3f) * num19 * num19;
					if (num18 < 0.02f)
					{
						continue;
					}
				}
				float num20 = (flag2 ? 1.6f : 0.8f) * px * (1f + ((num10 > num8) ? ((num10 - num8) * 0.6f) : 0f));
				float num21 = (flag2 ? 2.6f : 1.2f) * px;
				bool flag3 = ((num11 >> 29) & 3) == 0;
				Color color = (flag2 ? new Color(0.7058824f, 1f, 40f / 51f) : (flag3 ? new Color(1f, 0.9254902f, 38f / 51f) : new Color(44f / 51f, 0.9254902f, 1f)));
				Color c = (flag2 ? new Color(1f, 0.5882353f, 14f / 51f) : new Color(2f / 3f, 38f / 51f, 1f));
				Vector3 normalized6 = (normalized4 + normalized5 * (num15 * 0.5f)).normalized;
				float w = Mathf.Cos(num15 * 0.5f + num20 * 3f + num21 * 3f + 0.01f);
				_meteorA[num5] = new Vector4(normalized6.x, normalized6.y, normalized6.z, w);
				_meteorB[num5] = new Vector4(normalized5.x, normalized5.y, normalized5.z, num15);
				_meteorC[num5] = new Vector4(vector6.x, vector6.y, vector6.z, num20);
				_meteorD[num5] = new Vector4(num17, num18, num21, 0f);
				_meteorE[num5] = new Vector4(color.r, color.g, color.b, 0f);
				_meteorD[num5].w = PackColor(c);
				num5++;
			}
			cmd.SetGlobalVector("_FP_Meteors", new Vector4(num5, 0f, 0f, 0f));
			cmd.SetGlobalVectorArray("_FP_MeteorA", _meteorA);
			cmd.SetGlobalVectorArray("_FP_MeteorB", _meteorB);
			cmd.SetGlobalVectorArray("_FP_MeteorC", _meteorC);
			cmd.SetGlobalVectorArray("_FP_MeteorD", _meteorD);
			cmd.SetGlobalVectorArray("_FP_MeteorE", _meteorE);
		}

		private static float PackColor(Color c)
		{
			return Mathf.Round(c.r * 255f) + Mathf.Round(c.g * 255f) * 256f + Mathf.Round(c.b * 255f) * 65536f;
		}

		private static float SnowAround(TerrainCache t, ViewState view)
		{
			if (t == null || t.Materials == null || t.Width <= 0 || t.Materials.Length < t.Width * t.Height)
			{
				return 0f;
			}
			int num = 0;
			int num2 = 0;
			for (int i = -3; i <= 3; i++)
			{
				for (int j = -3; j <= 3; j++)
				{
					int num3 = (int)(view.X + (float)j * 9f);
					int num4 = (int)(view.Y + (float)i * 9f);
					if (num3 >= 0 && num4 >= 0 && num3 < t.Width && num4 < t.Height)
					{
						num2++;
						if (t.Materials[num4 * t.Width + num3] == 5)
						{
							num++;
						}
					}
				}
			}
			if (num2 <= 0)
			{
				return 0f;
			}
			return (float)num / (float)num2;
		}

		private static uint Rand(int a, int b)
		{
			uint num = TerrainModel.Hash(a, b);
			uint num2 = (num ^ (num >> 15)) * 739982445;
			uint num3 = (num2 ^ (num2 >> 12)) * 695872825;
			return num3 ^ (num3 >> 15);
		}

		private static int FloorDiv(int a, int b)
		{
			if (a < 0)
			{
				return -((-a + b - 1) / b);
			}
			return a / b;
		}
	}
}
