using System;

namespace FirstPerson.Core
{
	public sealed class BiomeMix
	{
		public readonly float[] Near = new float[28];

		public readonly float[] Area = new float[28];

		private readonly float[] _nearTarget = new float[28];

		private readonly float[] _areaTarget = new float[28];

		private float _survey = -1f;

		private bool _seeded;

		public const float Radius = 26f;

		public float Shimmer;

		public float Mirage;

		public float Warp;

		public void Reset()
		{
			Array.Clear(Near, 0, Near.Length);
			Array.Clear(Area, 0, Area.Length);
			_seeded = false;
			_survey = -1f;
			Shimmer = (Mirage = (Warp = 0f));
		}

		public void Step(BiomeMap map, float eyeX, float eyeY, float dt)
		{
			_survey -= dt;
			if (_survey <= 0f || !_seeded)
			{
				_survey = 0.25f;
				map.Shares(eyeX, eyeY, 26f, _nearTarget, _areaTarget);
				_seeded = true;
			}
			float num = 1f - (float)Math.Exp((0f - dt) / 2.6f);
			float num2 = 1f - (float)Math.Exp((0f - dt) / 1.2f);
			for (int i = 0; i < Near.Length; i++)
			{
				Near[i] += (_nearTarget[i] - Near[i]) * num;
				Area[i] += (_areaTarget[i] - Area[i]) * num2;
				if (Near[i] < 0.0001f)
				{
					Near[i] = 0f;
				}
				if (Area[i] < 0.0001f)
				{
					Area[i] = 0f;
				}
			}
		}

		public Atmosphere Apply(Atmosphere air, float level, float time)
		{
			Shimmer = (Mirage = (Warp = 0f));
			if (air == null || air.Indoors || level <= 0f)
			{
				return air;
			}
			float num = 0f;
			for (int i = 1; i < Near.Length; i++)
			{
				num += Near[i];
			}
			if (num < 0.003f)
			{
				return air;
			}
			float num2 = 0f;
			float num3 = 0f;
			float num4 = 0f;
			float num5 = 0f;
			float num6 = 0f;
			float num7 = 0f;
			float num8 = 0f;
			float num9 = 0f;
			float num10 = 0f;
			float num11 = 0f;
			float num12 = 0f;
			float num13 = 0f;
			float num14 = 0f;
			float num15 = 0f;
			float num16 = 0f;
			float num17 = 0f;
			float num18 = 0f;
			float num19 = 0f;
			float num20 = 0f;
			float num21 = 0f;
			float num22 = 0f;
			float num23 = 0f;
			float num24 = 0f;
			float num25 = 0f;
			float num26 = 0f;
			for (int j = 1; j < Near.Length; j++)
			{
				float num27 = Near[j];
				if (!(num27 <= 0f))
				{
					BiomeAir biomeAir = BiomeLooks.Air((BiomeKind)j);
					uint num28 = biomeAir.Sky;
					if (biomeAir.HueDrift > 0f)
					{
						num28 = BiomeLooks.HueTurn(num28, time * biomeAir.HueDrift / 60f);
					}
					float num29 = num27 * biomeAir.Amount;
					num2 += num29;
					num10 += (float)(num28 & 0xFF) * num29;
					num11 += (float)((num28 >> 8) & 0xFF) * num29;
					num12 += (float)((num28 >> 16) & 0xFF) * num29;
					num3 += num27 * (biomeAir.Light - 1f);
					if (biomeAir.LightTint != 0)
					{
						uint num30 = ((biomeAir.HueDrift > 0f) ? BiomeLooks.HueTurn(biomeAir.LightTint, time * biomeAir.HueDrift / 60f) : biomeAir.LightTint);
						num13 += (float)(num30 & 0xFF) * num29;
						num14 += (float)((num30 >> 8) & 0xFF) * num29;
						num15 += (float)((num30 >> 16) & 0xFF) * num29;
						num16 += num29;
					}
					num4 += num27 * (biomeAir.Haze - 1f);
					num5 += num27 * biomeAir.Rays;
					num6 += num27 * biomeAir.Stars;
					num7 += num27 * (biomeAir.Clouds - 1f);
					if (biomeAir.ZenithAmount > 0f)
					{
						uint num31 = ((biomeAir.Zenith == 0 && biomeAir.HueDrift > 0f) ? TerrainCache.Scale(BiomeLooks.HueTurn(num28, 0.5f), 0.55f) : biomeAir.Zenith);
						float num32 = num27 * biomeAir.ZenithAmount;
						num8 += num32;
						num17 += (float)(num31 & 0xFF) * num32;
						num18 += (float)((num31 >> 8) & 0xFF) * num32;
						num19 += (float)((num31 >> 16) & 0xFF) * num32;
					}
					if (biomeAir.Sun != 0)
					{
						num20 += (float)(biomeAir.Sun & 0xFF) * num27;
						num21 += (float)((biomeAir.Sun >> 8) & 0xFF) * num27;
						num22 += (float)((biomeAir.Sun >> 16) & 0xFF) * num27;
						num23 += num27;
					}
					num24 += num27 * biomeAir.Shimmer;
					num25 += num27 * biomeAir.Mirage;
					num26 += num27 * biomeAir.Warp;
					num9 += num27 * biomeAir.Pulse;
				}
			}
			if (num2 <= 0.0001f && num8 <= 0.0001f && num6 <= 0.0001f)
			{
				return air;
			}
			float night = air.Night;
			float num33 = level * (1f - 0.35f * night);
			uint sky = ((num2 > 0.0001f) ? TerrainCache.Rgb((int)(num10 / num2), (int)(num11 / num2), (int)(num12 / num2)) : air.Horizon);
			uint lightTint = ((num16 > 0.0001f) ? TerrainCache.Rgb((int)(num13 / num16), (int)(num14 / num16), (int)(num15 / num16)) : TerrainCache.Rgb(255, 255, 255));
			float num34 = 1f + num3 * level;
			if (num9 > 0f)
			{
				float num35 = Heartbeat(time);
				num34 *= 1f + 0.1f * num9 * level * num35;
			}
			Atmosphere atmosphere = air.Tinted(sky, Math.Min(0.85f, num2 * num33), num34, lightTint);
			if (num8 > 0.0001f)
			{
				uint b = TerrainCache.Rgb((int)(num17 / num8), (int)(num18 / num8), (int)(num19 / num8));
				atmosphere.Zenith = TerrainCache.Lerp(atmosphere.Zenith, b, Math.Min(0.9f, num8 * level * (1f - 0.6f * night)));
			}
			atmosphere.HazeDensity = air.HazeDensity * Math.Max(0.3f, 1f + num4 * level);
			atmosphere.Rays = Math.Max(0f, Math.Min(1.2f, air.Rays + num5 * level * (1f - night)));
			if (num6 > 0f)
			{
				atmosphere.Stars = Math.Max(air.Stars, Math.Min(1f, num6 * level));
			}
			atmosphere.Clouds = Math.Max(0f, Math.Min(1f, air.Clouds * (1f + num7 * level)));
			if (num23 > 0.0001f)
			{
				atmosphere.SunColor = TerrainCache.Lerp(atmosphere.SunColor, TerrainCache.Rgb((int)(num20 / num23), (int)(num21 / num23), (int)(num22 / num23)), Math.Min(1f, num23 * level));
			}
			if (num9 > 0f)
			{
				float num36 = Heartbeat(time);
				atmosphere.HazeDensity *= 1f + 0.25f * num9 * level * num36;
			}
			float num37 = Math.Max(0f, Math.Min(1f, (air.Light.Z - 0.15f) * 2.5f)) * (1f - night);
			Shimmer = Math.Min(1.5f, num24 * level) * (0.25f + 0.75f * num37);
			Mirage = Math.Min(1f, num25 * level) * num37;
			Warp = Math.Min(1.2f, num26 * level);
			return atmosphere;
		}

		public static float Heartbeat(float time)
		{
			float num = time / 1.5f;
			float t = num - (float)Math.Floor(num);
			float val = Bump(t, 0.06f, 0.07f);
			float val2 = Bump(t, 0.24f, 0.08f) * 0.6f;
			return Math.Max(val, val2);
		}

		private static float Bump(float t, float at, float width)
		{
			float num = (t - at) / width;
			if (!(num * num >= 1f))
			{
				return (1f - num * num) * (1f - num * num);
			}
			return 0f;
		}
	}
}
