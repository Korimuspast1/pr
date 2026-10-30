using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class RainFx
	{
		private struct Spot
		{
			public float X;

			public float Y;

			public float D;

			public float Strength;

			public uint H;

			public Precip Kind;
		}

		private const float Near = 22f;

		private const float Far = 80f;

		private const int Most = 10;

		public static int SnowVeils;

		[ThreadStatic]
		private static List<Spot> _spots;

		public static void Veils(List<Billboard> bills, Weather w, float eyeX, float eyeY, float fx, float fy, float time, Func<float, float, float> ground)
		{
			if (w == null || w.Storms.Count == 0)
			{
				SnowVeils = 0;
				return;
			}
			List<Spot> list = _spots ?? (_spots = new List<Spot>(32));
			list.Clear();
			for (int i = 0; i < w.Storms.Count; i++)
			{
				StormCloud stormCloud = w.Storms[i];
				if ((stormCloud.Drops != Precip.Rain && stormCloud.Drops != Precip.Acid && stormCloud.Drops != Precip.Rage && stormCloud.Drops != Precip.Snow) || stormCloud.Opacity < 0.2f)
				{
					continue;
				}
				float num = stormCloud.X - eyeX;
				float num2 = stormCloud.Y - eyeY;
				float num3 = 80f + Math.Max(stormCloud.HalfX, stormCloud.HalfY);
				if (num * num + num2 * num2 > num3 * num3)
				{
					continue;
				}
				int num4 = Math.Max(1, Math.Min(4, (int)(1f + Math.Max(stormCloud.HalfX, stormCloud.HalfY) / 12f)));
				for (int j = 0; j < num4; j++)
				{
					uint num5 = TerrainModel.Hash((int)(stormCloud.Seed * 977f) + j * 31 + 5, j * 131 + i);
					float num6 = stormCloud.X + ((float)(num5 & 0xFF) / 255f - 0.5f) * stormCloud.HalfX * 1.1f;
					float num7 = stormCloud.Y + ((float)((num5 >> 8) & 0xFF) / 255f - 0.5f) * stormCloud.HalfY * 1.1f;
					float num8 = num6 - eyeX;
					float num9 = num7 - eyeY;
					float num10 = (float)Math.Sqrt(num8 * num8 + num9 * num9);
					if (!(num10 < 22f) && !(num10 > 80f) && !(num8 * fx + num9 * fy < -0.25f * num10))
					{
						Precip drops;
						float num11 = w.Cover(num6, num7, time, out drops);
						if (!(num11 < 0.25f) && drops == stormCloud.Drops)
						{
							list.Add(new Spot
							{
								X = num6,
								Y = num7,
								D = num10,
								Strength = Math.Min(1f, (num11 - 0.25f) * 2.5f),
								H = num5,
								Kind = drops
							});
						}
					}
				}
			}
			if (list.Count > 10)
			{
				list.Sort((Spot a, Spot b) => a.D.CompareTo(b.D));
				list.RemoveRange(10, list.Count - 10);
			}
			int num12 = 0;
			foreach (Spot item in list)
			{
				float num13 = BlastFx.Smooth((item.D - 22f) / 12f) * (1f - BlastFx.Smooth((item.D - 60f) / 20f)) * item.Strength;
				if (!(num13 < 0.03f))
				{
					float num14 = ground(item.X, item.Y);
					bool flag = item.Kind == Precip.Snow;
					if (flag)
					{
						num12++;
					}
					float width = (flag ? (13f + 7f * (float)((item.H >> 16) & 0xFF) / 255f) : (9f + 5f * (float)((item.H >> 16) & 0xFF) / 255f));
					uint multiply = (flag ? TerrainCache.Rgb(126, 134, 150) : ((item.Kind == Precip.Acid) ? TerrainCache.Rgb(96, 150, 80) : ((item.Kind == Precip.Rage) ? TerrainCache.Rgb(150, 70, 70) : TerrainCache.Rgb(104, 114, 128))));
					bills.Add(new Billboard
					{
						X = item.X,
						Y = item.Y,
						Z = num14 - 0.5f,
						GroundZ = num14,
						Width = width,
						Height = Math.Max(4f, w.Base - num14) + 1.5f,
						Image = (flag ? RainArt.SnowVeil((int)((item.H >> 24) & 3)) : RainArt.Curtain((int)((item.H >> 24) & 3))),
						Kind = BillboardKind.Facing,
						Tag = -1,
						Flip = ((item.H & 0x800000) != 0),
						Multiply = multiply,
						Alpha = (byte)Math.Max(1, (int)((flag ? 205f : 165f) * num13)),
						Far = true
					});
				}
			}
			SnowVeils = num12;
		}
	}
}
