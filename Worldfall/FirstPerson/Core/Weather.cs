using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public sealed class Weather
	{
		public readonly List<StormCloud> Storms = new List<StormCloud>();

		public readonly List<Bolt> Bolts = new List<Bolt>();

		public Precip Global;

		public float GlobalStrength;

		public float Flash;

		public float Base = 15f;

		public float Thickness = 7f;

		public float WindX = 0.6f;

		public float WindY = 0.2f;

		public float SlantX;

		public float SlantY;

		public static readonly uint SnowCloud = TerrainCache.Rgb(206, 213, 224);

		public float MinX;

		public float MinY;

		public float MaxX;

		public float MaxY;

		public bool Any
		{
			get
			{
				if (Storms.Count <= 0 && (Global == Precip.None || !(GlobalStrength > 0.01f)) && Bolts.Count <= 0)
				{
					return Flash > 0.01f;
				}
				return true;
			}
		}

		public void Bounds()
		{
			MinX = (MinY = float.MaxValue);
			MaxX = (MaxY = float.MinValue);
			foreach (StormCloud storm in Storms)
			{
				MinX = Math.Min(MinX, storm.X - storm.HalfX * 1.3f);
				MaxX = Math.Max(MaxX, storm.X + storm.HalfX * 1.3f);
				MinY = Math.Min(MinY, storm.Y - storm.HalfY * 1.3f);
				MaxY = Math.Max(MaxY, storm.Y + storm.HalfY * 1.3f);
			}
		}

		public float Density(float x, float y, float h, float time, out int which)
		{
			float billow;
			return Density(x, y, h, time, out which, out billow);
		}

		public float Density(float x, float y, float h, float time, out int which, out float billow, float[] keep = null)
		{
			which = -1;
			billow = 0.5f;
			float num = 0f;
			for (int i = 0; i < Storms.Count; i++)
			{
				StormCloud stormCloud = Storms[i];
				float num2 = ((keep != null) ? keep[i] : 1f);
				if (num2 <= 0.01f)
				{
					continue;
				}
				float num3 = (x - stormCloud.X) / stormCloud.HalfX;
				float num4 = (y - stormCloud.Y) / stormCloud.HalfY;
				float num5 = num3 * num3 + num4 * num4;
				if (num5 > 1.69f)
				{
					continue;
				}
				float num6 = x - WindX * time + stormCloud.Seed * 37f;
				float num7 = y - WindY * time + stormCloud.Seed * 53f;
				float num8 = CloudNoise.Sample(num6 * 6f, num7 * 6f) * 0.55f + CloudNoise.Sample(num6 * 15f + 71f, num7 * 15f + 13f) * 0.3f + CloudNoise.Sample(num6 * 1.4f + 17f, num7 * 1.4f + 5f) * 0.12f;
				float num9 = 1f - num5 / 1.3f;
				float num11;
				if (stormCloud.Drops == Precip.Snow)
				{
					float num10 = 0.3f + 0.45f * Math.Max(0f, Math.Min(1f, num9 * 1.4f + 0.2f)) * (0.55f + 0.6f * num8);
					if (h > num10)
					{
						continue;
					}
					num11 = num9 * 1.15f + (num8 - 0.5f) * 2.2f - h * 0.15f;
					float num12 = (num10 - h) / Math.Max(0.05f, num10);
					if (num12 < 0.3f)
					{
						num11 -= (0.3f - num12) * 1.6f;
					}
					if (h < 0.16f)
					{
						num11 -= (0.16f - h) * 3.2f * (1.15f - num8);
					}
					num11 *= 0.85f;
				}
				else
				{
					float num13 = 0.2f + 0.8f * Math.Max(0f, Math.Min(1f, num9 * 1.6f + 0.15f)) * (0.3f + 0.95f * num8);
					if (h > num13)
					{
						continue;
					}
					num11 = num9 * 1.3f + (num8 - 0.5f) * 3f - h * 0.3f;
					float num14 = (num13 - h) / Math.Max(0.05f, num13);
					if (num14 < 0.25f)
					{
						num11 -= (0.25f - num14) * 2.4f;
					}
					if (h < 0.06f)
					{
						num11 -= (0.06f - h) * 8f * (1f - num8);
					}
				}
				num11 = Math.Min(1f, num11) * stormCloud.Opacity * num2;
				if (num11 > num)
				{
					num = num11;
					which = i;
					billow = num8;
				}
			}
			return num;
		}

		public float Cover(float x, float y, float time, out Precip drops)
		{
			drops = Precip.None;
			int which;
			float result = Density(x, y, 0.15f, time, out which);
			if (which >= 0)
			{
				drops = Storms[which].Drops;
			}
			return result;
		}
	}
}
