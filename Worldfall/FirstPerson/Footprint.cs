using UnityEngine;

namespace FirstPerson
{
	internal sealed class Footprint
	{
		public float X0;

		public float Y0;

		public float Cell;

		public int W;

		public int H;

		public float[] Dist;

		public float Reach;

		public int Used;

		public float Sample(float x, float y, out float gx, out float gy)
		{
			float num = (x - X0) / Cell - 0.5f;
			float num2 = (y - Y0) / Cell - 0.5f;
			float num3 = 0f;
			float num4 = 0f;
			if (num < 0f || num2 < 0f || num >= (float)(W - 1) || num2 >= (float)(H - 1))
			{
				float num5 = Mathf.Clamp(num, 0f, (float)W - 1.001f);
				float num6 = Mathf.Clamp(num2, 0f, (float)H - 1.001f);
				num3 = (num - num5) * Cell;
				num4 = (num2 - num6) * Cell;
				num = num5;
				num2 = num6;
			}
			int num7 = (int)num;
			int num8 = (int)num2;
			float num9 = num - (float)num7;
			float num10 = num2 - (float)num8;
			int num11 = num8 * W + num7;
			float num12 = Dist[num11];
			float num13 = Dist[num11 + 1];
			float num14 = Dist[num11 + W];
			float num15 = Dist[num11 + W + 1];
			float num16 = num12 + (num13 - num12) * num9;
			float num17 = num14 + (num15 - num14) * num9;
			gx = (num13 - num12) * (1f - num10) + (num15 - num14) * num10;
			gy = (num14 - num12) * (1f - num9) + (num15 - num13) * num9;
			float num18 = Mathf.Sqrt(gx * gx + gy * gy);
			if (num18 > 1E-06f)
			{
				gx /= num18;
				gy /= num18;
			}
			else
			{
				gx = 0f;
				gy = 1f;
			}
			float num19 = num16 + (num17 - num16) * num10;
			if (num3 == 0f && num4 == 0f)
			{
				return num19;
			}
			float num20 = Mathf.Sqrt(num3 * num3 + num4 * num4);
			float num21 = gx * Mathf.Max(0f, num19) + num3;
			float num22 = gy * Mathf.Max(0f, num19) + num4;
			float num23 = Mathf.Sqrt(num21 * num21 + num22 * num22);
			if (num23 > 1E-06f)
			{
				gx = num21 / num23;
				gy = num22 / num23;
			}
			return num19 + num20;
		}

		public float SampleDistance(float x, float y)
		{
			float num = (x - X0) / Cell - 0.5f;
			float num2 = (y - Y0) / Cell - 0.5f;
			float num3 = 0f;
			float num4 = 0f;
			if (num < 0f || num2 < 0f || num >= (float)(W - 1) || num2 >= (float)(H - 1))
			{
				float num5 = Mathf.Clamp(num, 0f, (float)W - 1.001f);
				float num6 = Mathf.Clamp(num2, 0f, (float)H - 1.001f);
				num3 = (num - num5) * Cell;
				num4 = (num2 - num6) * Cell;
				num = num5;
				num2 = num6;
			}
			int num7 = (int)num;
			int num8 = (int)num2;
			float num9 = num - (float)num7;
			float num10 = num2 - (float)num8;
			int num11 = num8 * W + num7;
			float num12 = Dist[num11];
			float num13 = Dist[num11 + 1];
			float num14 = Dist[num11 + W];
			float num15 = Dist[num11 + W + 1];
			float num16 = num12 + (num13 - num12) * num9;
			float num17 = num14 + (num15 - num14) * num9;
			float num18 = num16 + (num17 - num16) * num10;
			if (num3 == 0f && num4 == 0f)
			{
				return num18;
			}
			float num19 = Mathf.Sqrt(num3 * num3 + num4 * num4);
			return num18 + num19;
		}
	}
}
