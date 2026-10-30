using System;

namespace FirstPerson.Core
{
	public sealed class Atmosphere
	{
		private struct Key
		{
			public float T;

			public uint Zenith;

			public uint Horizon;

			public uint SunHorizon;

			public uint CloudLit;

			public uint CloudShade;

			public uint Sun;

			public float LR;

			public float LG;

			public float LB;

			public float AR;

			public float AG;

			public float AB;

			public float Haze;

			public float Stars;

			public float Rays;

			public Key(float t, uint zenith, uint horizon, uint sunHorizon, uint cloudLit, uint cloudShade, uint sun, float lr, float lg, float lb, float ar, float ag, float ab, float haze, float stars, float rays)
			{
				T = t;
				Zenith = zenith;
				Horizon = horizon;
				SunHorizon = sunHorizon;
				CloudLit = cloudLit;
				CloudShade = cloudShade;
				Sun = sun;
				LR = lr;
				LG = lg;
				LB = lb;
				AR = ar;
				AG = ag;
				AB = ab;
				Haze = haze;
				Stars = stars;
				Rays = rays;
			}
		}

		private struct Bodies
		{
			public float HourAngle;

			public float SunEl;

			public float MoonEl;

			public float MoonLit;

			public Vec3 Sun;

			public Vec3 Moon;

			public Vec3 Light;

			public float Direct;

			public void Lit(float lit)
			{
				MoonLit = lit;
				if (SunEl < -0.02f)
				{
					Direct = lit * Smooth(0.02f, 0.2f, MoonEl) * Smooth(0.02f, 0.2f, 0f - SunEl);
				}
			}
		}

		public Vec3 Light = new Vec3(-0.45f, 0.55f, 0.7f).Normalized();

		public float LightR = 0.62f;

		public float LightG = 0.6f;

		public float LightB = 0.55f;

		public float AmbR = 0.46f;

		public float AmbG = 0.48f;

		public float AmbB = 0.58f;

		public Vec3 SunDisc = new Vec3(-0.45f, 0.55f, 0.7f).Normalized();

		public Vec3 MoonDisc = new Vec3(0.35f, -0.5f, 0.79f).Normalized();

		public uint SunColor = TerrainCache.Rgb(255, 248, 220);

		public float SunVisible = 1f;

		public float MoonVisible;

		public uint Zenith = TerrainCache.Rgb(64, 110, 200);

		public uint Horizon = TerrainCache.Rgb(178, 208, 238);

		public uint SunHorizon = TerrainCache.Rgb(214, 226, 240);

		public float HazeDensity = 0.0052f;

		public float Clouds = 0.5f;

		public uint CloudLit = TerrainCache.Rgb(255, 250, 244);

		public uint CloudShade = TerrainCache.Rgb(160, 172, 204);

		public float Stars;

		public float Rays = 0.35f;

		public float Night;

		public float DawnMist;

		public bool Indoors;

		public bool Natural;

		public Vec3 ShadowLight = new Vec3(-0.45f, 0.55f, 0.7f).Normalized();

		public float MoonLit = 1f;

		public float StarTurn;

		public Vec3 StarPole = new Vec3(0f, -0.899f, 0.438f);

		public float SunElevation;

		public float CardFillR = 1f;

		public float CardFillG = 1f;

		public float CardFillB = 1f;

		private static readonly Key[] Keys = new Key[9]
		{
			new Key(0f, C(791094), C(3029616), C(3818624), C(6976680), C(2239064), C(15265023), 0.26f, 0.3f, 0.46f, 0.25f, 0.27f, 0.45f, 0.0065f, 1f, 0f),
			new Key(0.19f, C(1449548), C(4475018), C(6970006), C(8026288), C(2895972), C(15265023), 0.16f, 0.18f, 0.32f, 0.28f, 0.27f, 0.45f, 0.007f, 0.7f, 0f),
			new Key(0.25f, C(3820690), C(15245955), C(16758650), C(16765872), C(9075374), C(16767392), 0.62f, 0.44f, 0.34f, 0.34f, 0.32f, 0.46f, 0.009f, 0f, 0.55f),
			new Key(0.32f, C(5273804), C(13031150), C(15917256), C(16774890), C(10924244), C(16773328), 0.66f, 0.6f, 0.52f, 0.44f, 0.46f, 0.58f, 0.0062f, 0f, 0.4f),
			new Key(0.5f, C(3960008), C(11850482), C(13820660), C(16777215), C(11845852), C(16776170), 0.66f, 0.64f, 0.58f, 0.46f, 0.49f, 0.6f, 0.005f, 0f, 0.3f),
			new Key(0.66f, C(4875964), C(14995640), C(16766106), C(16771272), C(11049152), C(16769192), 0.74f, 0.58f, 0.4f, 0.42f, 0.4f, 0.56f, 0.0064f, 0f, 0.55f),
			new Key(0.74f, C(3356030), C(15768200), C(16751206), C(16758432), C(8019614), C(16756848), 0.66f, 0.38f, 0.3f, 0.36f, 0.28f, 0.46f, 0.009f, 0f, 0.75f),
			new Key(0.79f, C(1974880), C(8282276), C(12087444), C(11829420), C(4077166), C(16748640), 0.22f, 0.16f, 0.24f, 0.3f, 0.25f, 0.44f, 0.008f, 0.35f, 0.1f),
			new Key(0.86f, C(922680), C(3161202), C(4081284), C(6976680), C(2239064), C(15265023), 0.26f, 0.3f, 0.46f, 0.25f, 0.27f, 0.45f, 0.0065f, 1f, 0f)
		};

		private const float LowestNaturalLight = MathF.PI / 30f;

		private const float MoonlessFill = 0.85f;

		private const float CardMoonlessFill = 0.9f;

		private const float CardNightLift = 1.45f;

		private const double DeclinationStep = 0.00017453292519943296;

		private Vec3 _moonShadowLight;

		public float MoonScale = 1f;

		public Vec3 CastLight
		{
			get
			{
				if (!Natural)
				{
					return Light;
				}
				return ShadowLight;
			}
		}

		public float FlatLambert => Math.Max(0f, Light.Z);

		private static uint C(int hex)
		{
			return TerrainCache.Rgb((hex >> 16) & 0xFF, (hex >> 8) & 0xFF, hex & 0xFF);
		}

		private static Key KeyAt(float t)
		{
			int num = Keys.Length - 1;
			for (int i = 0; i < Keys.Length; i++)
			{
				if (Keys[i].T <= t)
				{
					num = i;
				}
			}
			Key a = Keys[num];
			Key b = ((num + 1 < Keys.Length) ? Keys[num + 1] : Keys[0]);
			float num2 = ((num + 1 < Keys.Length) ? b.T : (b.T + 1f));
			float num3 = ((num2 > a.T) ? ((t - a.T) / (num2 - a.T)) : 0f);
			num3 = num3 * num3 * (3f - 2f * num3);
			return Mixed(a, b, num3);
		}

		private static Key Mixed(Key a, Key b, float u)
		{
			return new Key(a.T, TerrainCache.Lerp(a.Zenith, b.Zenith, u), TerrainCache.Lerp(a.Horizon, b.Horizon, u), TerrainCache.Lerp(a.SunHorizon, b.SunHorizon, u), TerrainCache.Lerp(a.CloudLit, b.CloudLit, u), TerrainCache.Lerp(a.CloudShade, b.CloudShade, u), TerrainCache.Lerp(a.Sun, b.Sun, u), Mix(a.LR, b.LR, u), Mix(a.LG, b.LG, u), Mix(a.LB, b.LB, u), Mix(a.AR, b.AR, u), Mix(a.AG, b.AG, u), Mix(a.AB, b.AB, u), Mix(a.Haze, b.Haze, u), Mix(a.Stars, b.Stars, u), Mix(a.Rays, b.Rays, u));
		}

		private static Atmosphere Keyed(Key key)
		{
			return new Atmosphere
			{
				Zenith = key.Zenith,
				Horizon = key.Horizon,
				SunHorizon = key.SunHorizon,
				CloudLit = key.CloudLit,
				CloudShade = key.CloudShade,
				SunColor = key.Sun,
				LightR = key.LR,
				LightG = key.LG,
				LightB = key.LB,
				AmbR = key.AR,
				AmbG = key.AG,
				AmbB = key.AB,
				HazeDensity = key.Haze,
				Stars = key.Stars,
				Rays = key.Rays,
				Clouds = 0.46f
			};
		}

		public static Atmosphere ForTime(float t)
		{
			t -= (float)Math.Floor(t);
			Atmosphere atmosphere = Keyed(KeyAt(t));
			float num = (float)Math.Sin((double)(t - 0.25f) * 2.0 * Math.PI) * 64f * (MathF.PI / 180f);
			float num2 = (t - 0.25f) * 2f * MathF.PI;
			atmosphere.SunDisc = Dir(num2, num);
			atmosphere.SunVisible = Smooth(-0.06f, 0.02f, num);
			float num3 = (float)Math.Sin((double)(t - 0.75f) * 2.0 * Math.PI) * 58f * (MathF.PI / 180f);
			float num4 = (t - 0.75f) * 2f * MathF.PI + 0.4f;
			atmosphere.MoonDisc = Dir(num4, num3);
			atmosphere.MoonVisible = Smooth(-0.04f, 0.05f, num3);
			float num5 = Smooth(-0.02f, 0.1f, num);
			bool num6 = num < 0.02f;
			float azimuth = (num6 ? num4 : num2);
			float val = (num6 ? num3 : num);
			atmosphere.Light = Dir(azimuth, Math.Max(val, 0.17453292f));
			float num7 = (num6 ? Smooth(0.02f, 0.2f, num3) : num5);
			atmosphere.LightR *= num7;
			atmosphere.LightG *= num7;
			atmosphere.LightB *= num7;
			atmosphere.Night = 1f - Smooth(-0.16f, 0.06f, num);
			atmosphere.DawnMist = ((t < 0.5f) ? Mist(num) : 0f);
			atmosphere.ShadowLight = atmosphere.Light;
			atmosphere.SunElevation = num;
			return atmosphere;
		}

		public static Atmosphere NaturalAt(float t, float declination, float moonCycle, float moonLit = -1f)
		{
			t -= (float)Math.Floor(t);
			moonCycle -= (float)Math.Floor(moonCycle);
			Bodies bodies = NaturalBodies(t, declination, moonCycle);
			if (moonLit >= 0f)
			{
				bodies.Lit(Math.Min(1f, moonLit));
			}
			float num = SunPath.EveningShare(bodies.HourAngle);
			float t2 = SunPath.MorningHour(bodies.SunEl, declination);
			float t3 = SunPath.EveningHour(bodies.SunEl, declination);
			Key key = KeyAt((num >= 1f) ? Wrap(t3) : Wrap(t2));
			if (num > 0f && num < 1f)
			{
				key = Mixed(key, KeyAt(Wrap(t3)), num);
			}
			Atmosphere atmosphere = Keyed(key);
			atmosphere.Natural = true;
			atmosphere.SunDisc = bodies.Sun;
			atmosphere.SunVisible = Smooth(-0.06f, 0.02f, bodies.SunEl);
			atmosphere.MoonDisc = bodies.Moon;
			atmosphere.MoonVisible = Smooth(-0.04f, 0.05f, bodies.MoonEl);
			atmosphere.MoonLit = bodies.MoonLit;
			atmosphere.Light = bodies.Light;
			atmosphere.Night = 1f - Smooth(-0.16f, 0.06f, bodies.SunEl);
			atmosphere.DawnMist = ((num <= 0f) ? Mist(bodies.SunEl) : 0f);
			atmosphere.MoonFill(MoonShare(bodies.MoonLit, bodies.MoonEl));
			atmosphere.LightR *= bodies.Direct;
			atmosphere.LightG *= bodies.Direct;
			atmosphere.LightB *= bodies.Direct;
			float num2 = SunPath.StepHour(t);
			double num3 = Math.Round((moonCycle * 8f + (num2 - t)) * 4000f) / 4000.0;
			float declination2 = (float)(Math.Round((double)declination / 0.00017453292519943296) * 0.00017453292519943296);
			Bodies bodies2 = NaturalBodies(num2, declination2, (float)(num3 / 8.0));
			atmosphere.ShadowLight = bodies2.Light;
			atmosphere._moonShadowLight = Lifted(bodies2.Moon);
			atmosphere.StarTurn = bodies.HourAngle;
			atmosphere.StarPole = SunPath.StarPole;
			atmosphere.SunElevation = bodies.SunEl;
			return atmosphere;
		}

		private static float MoonShare(float moonLit, float moonElevation)
		{
			return moonLit * Smooth(0.02f, 0.2f, moonElevation);
		}

		private void MoonFill(float moon)
		{
			MoonFills(Stars, moon, out var fill, out var card);
			AmbR *= fill;
			AmbG *= fill;
			AmbB *= fill;
			CardFillR = (CardFillG = (CardFillB = card));
		}

		private static void MoonFills(float stars, float moon, out float fill, out float card)
		{
			float num = stars * (1f - moon);
			fill = 1f - num * 0.14999998f;
			card = (1f + stars * 0.45000005f) * (1f - num * 0.100000024f) / fill;
		}

		private static float Wrap(float t)
		{
			return t - (float)Math.Floor(t);
		}

		private static Vec3 Lifted(Vec3 body)
		{
			return Dir((float)Math.Atan2(body.Y, body.X), Math.Max(SunPath.Elevation(body), MathF.PI / 30f));
		}

		private static Bodies NaturalBodies(float t, float declination, float moonCycle)
		{
			Bodies result = new Bodies
			{
				HourAngle = SunPath.HourAngle(t, declination)
			};
			result.Sun = SunPath.Direction(result.HourAngle, declination);
			result.SunEl = SunPath.Elevation(result.Sun);
			SunPath.Moon(result.HourAngle, declination, moonCycle, out var hourAngle, out var moonDeclination);
			result.Moon = SunPath.Direction(hourAngle, moonDeclination);
			result.MoonEl = SunPath.Elevation(result.Moon);
			bool flag = result.SunEl < -0.02f;
			result.Light = Lifted(flag ? result.Moon : result.Sun);
			result.Direct = Smooth(-0.02f, 0.1f, result.SunEl);
			result.Lit(SunPath.MoonLit(moonCycle));
			return result;
		}

		public Atmosphere Blend(Atmosphere other, float k)
		{
			if (k <= 0f)
			{
				return this;
			}
			if (Natural)
			{
				return BlendNatural(other, Math.Min(1f, k));
			}
			if (k >= 1f)
			{
				return other;
			}
			return new Atmosphere
			{
				Light = (Light * (1f - k) + other.Light * k).Normalized(),
				LightR = Mix(LightR, other.LightR, k),
				LightG = Mix(LightG, other.LightG, k),
				LightB = Mix(LightB, other.LightB, k),
				AmbR = Mix(AmbR, other.AmbR, k),
				AmbG = Mix(AmbG, other.AmbG, k),
				AmbB = Mix(AmbB, other.AmbB, k),
				SunDisc = ((k < 0.5f) ? SunDisc : other.SunDisc),
				MoonDisc = ((k < 0.5f) ? MoonDisc : other.MoonDisc),
				SunColor = TerrainCache.Lerp(SunColor, other.SunColor, k),
				SunVisible = Mix(SunVisible, other.SunVisible, k),
				MoonVisible = Mix(MoonVisible, other.MoonVisible, k),
				Zenith = TerrainCache.Lerp(Zenith, other.Zenith, k),
				Horizon = TerrainCache.Lerp(Horizon, other.Horizon, k),
				SunHorizon = TerrainCache.Lerp(SunHorizon, other.SunHorizon, k),
				HazeDensity = Mix(HazeDensity, other.HazeDensity, k),
				Clouds = Mix(Clouds, other.Clouds, k),
				CloudLit = TerrainCache.Lerp(CloudLit, other.CloudLit, k),
				CloudShade = TerrainCache.Lerp(CloudShade, other.CloudShade, k),
				Stars = Mix(Stars, other.Stars, k),
				Rays = Mix(Rays, other.Rays, k),
				Night = Mix(Night, other.Night, k),
				DawnMist = Mix(DawnMist, other.DawnMist, k),
				MoonScale = Mix(MoonScale, other.MoonScale, k),
				Indoors = other.Indoors,
				Natural = other.Natural,
				ShadowLight = (ShadowLight * (1f - k) + other.ShadowLight * k).Normalized(),
				MoonLit = Mix(MoonLit, other.MoonLit, k),
				StarTurn = ((k < 0.5f) ? StarTurn : other.StarTurn),
				StarPole = ((k < 0.5f) ? StarPole : other.StarPole),
				SunElevation = Mix(SunElevation, other.SunElevation, k),
				CardFillR = Mix(CardFillR, other.CardFillR, k),
				CardFillG = Mix(CardFillG, other.CardFillG, k),
				CardFillB = Mix(CardFillB, other.CardFillB, k)
			};
		}

		private Atmosphere BlendNatural(Atmosphere other, float k)
		{
			Vec3 b = other.Light;
			Vec3 b2 = other.CastLight;
			float num = other.LightR;
			float num2 = other.LightG;
			float num3 = other.LightB;
			float num4 = other.AmbR;
			float num5 = other.AmbG;
			float num6 = other.AmbB;
			float b3 = other.CardFillR;
			float b4 = other.CardFillG;
			float b5 = other.CardFillB;
			float num7 = other.MoonVisible;
			if (!other.Natural && !Indoors)
			{
				float num8 = SunPath.Elevation(MoonDisc);
				float num9 = MoonShare(MoonLit, num8);
				b = Lifted(MoonDisc);
				b2 = _moonShadowLight;
				MoonFills(other.Stars, num9, out var fill, out var card);
				num4 *= fill;
				num5 *= fill;
				num6 *= fill;
				b3 = (b4 = (b5 = card));
				num *= num9;
				num2 *= num9;
				num3 *= num9;
				num7 = Math.Min(num7, Smooth(-0.04f, 0.05f, num8));
			}
			float num10 = (1f - k) * (LightR + LightG + LightB + 0.01f);
			float num11 = k * (num + num2 + num3 + 0.01f);
			float w = (float)Math.Round(num11 / (num10 + num11) * 64f) / 64f;
			return new Atmosphere
			{
				Light = Turned(Light, b, num11 / (num10 + num11)),
				LightR = Mix(LightR, num, k),
				LightG = Mix(LightG, num2, k),
				LightB = Mix(LightB, num3, k),
				AmbR = Mix(AmbR, num4, k),
				AmbG = Mix(AmbG, num5, k),
				AmbB = Mix(AmbB, num6, k),
				SunDisc = SunDisc,
				MoonDisc = MoonDisc,
				SunColor = TerrainCache.Lerp(SunColor, other.SunColor, k),
				SunVisible = Mix(SunVisible, other.SunVisible, k),
				MoonVisible = Mix(MoonVisible, num7, k),
				Zenith = TerrainCache.Lerp(Zenith, other.Zenith, k),
				Horizon = TerrainCache.Lerp(Horizon, other.Horizon, k),
				SunHorizon = TerrainCache.Lerp(SunHorizon, other.SunHorizon, k),
				HazeDensity = Mix(HazeDensity, other.HazeDensity, k),
				Clouds = Mix(Clouds, other.Clouds, k),
				CloudLit = TerrainCache.Lerp(CloudLit, other.CloudLit, k),
				CloudShade = TerrainCache.Lerp(CloudShade, other.CloudShade, k),
				Stars = Mix(Stars, other.Stars, k),
				Rays = Mix(Rays, other.Rays, k),
				Night = Mix(Night, other.Night, k),
				DawnMist = Mix(DawnMist, other.DawnMist, k),
				MoonScale = Mix(MoonScale, other.MoonScale, k),
				Indoors = other.Indoors,
				Natural = true,
				ShadowLight = Turned(ShadowLight, b2, w),
				_moonShadowLight = _moonShadowLight,
				MoonLit = MoonLit,
				StarTurn = StarTurn,
				StarPole = StarPole,
				SunElevation = SunElevation,
				CardFillR = Mix(CardFillR, b3, k),
				CardFillG = Mix(CardFillG, b4, k),
				CardFillB = Mix(CardFillB, b5, k)
			};
		}

		private static Vec3 Turned(Vec3 a, Vec3 b, float w)
		{
			if (w <= 0f || (a.X == b.X && a.Y == b.Y && a.Z == b.Z))
			{
				return a;
			}
			if (w >= 1f)
			{
				return b;
			}
			return (a * (1f - w) + b * w).Normalized();
		}

		public Atmosphere NightDarker(float k)
		{
			float num = Math.Min(1f, k) * Night;
			if (num <= 0.001f)
			{
				return this;
			}
			Atmosphere obj = (Atmosphere)MemberwiseClone();
			float num2 = 1f - num;
			float num3 = 1f - num * 0.98f;
			float num4 = 1f - num * 0.85f;
			float s = 1f - num * 0.78f;
			obj.LightR = LightR * num2;
			obj.LightG = LightG * num2;
			obj.LightB = LightB * (1f - num * 0.9f);
			obj.AmbR = AmbR * num3;
			obj.AmbG = AmbG * num3;
			obj.AmbB = AmbB * num4;
			obj.Zenith = TerrainCache.Scale(Zenith, s);
			obj.Horizon = TerrainCache.Scale(Horizon, s);
			obj.SunHorizon = TerrainCache.Scale(SunHorizon, s);
			obj.CloudLit = TerrainCache.Scale(CloudLit, 1f - num * 0.5f);
			obj.CloudShade = TerrainCache.Scale(CloudShade, s);
			return obj;
		}

		public static Atmosphere Indoor(uint ceilingTop, uint ceilingBottom, Atmosphere outside)
		{
			float num = 1f - outside.Night;
			return new Atmosphere
			{
				Indoors = true,
				Zenith = ceilingTop,
				Horizon = ceilingBottom,
				SunHorizon = ceilingBottom,
				HazeDensity = 0.012f,
				Clouds = 0f,
				Stars = 0f,
				Rays = 0f,
				SunVisible = 0f,
				MoonVisible = 0f,
				Night = outside.Night,
				Light = new Vec3(-0.3f, 0.78f, 0.55f).Normalized(),
				ShadowLight = new Vec3(-0.3f, 0.78f, 0.55f).Normalized(),
				LightR = 0.2f + 0.35f * num,
				LightG = 0.17f + 0.3f * num,
				LightB = 0.14f + 0.22f * num,
				AmbR = 0.3f + 0.12f * num,
				AmbG = 0.25f + 0.11f * num,
				AmbB = 0.24f + 0.12f * num,
				SunColor = outside.SunColor,
				Natural = outside.Natural,
				MoonLit = outside.MoonLit,
				SunElevation = outside.SunElevation
			};
		}

		public Atmosphere SunThroughWindows(Atmosphere outside)
		{
			if (!Indoors || !SunBeam(outside, out var beam))
			{
				return this;
			}
			Light = (ShadowLight = Turned(Light, beam, 1f - Smooth(0.9f, 1f, outside.Night)));
			return this;
		}

		public static Vec3 DaylightFrom(Atmosphere outside)
		{
			if (!SunBeam(outside, out var beam))
			{
				return outside?.Light ?? default(Vec3);
			}
			Vec3 light = outside.Light;
			float num = 1f - Smooth(0.9f, 1f, outside.Night);
			if (num >= 1f)
			{
				return beam;
			}
			if (num <= 0f)
			{
				return light;
			}
			float num2 = (float)Math.Acos(Math.Max(-1f, Math.Min(1f, Vec3.Dot(light, beam))));
			if ((float)Math.Sin(num2) < 0.001f)
			{
				return Turned(light, beam, num);
			}
			return (light * (float)Math.Sin((1f - num) * num2) + beam * (float)Math.Sin(num * num2)).Normalized();
		}

		private static bool SunBeam(Atmosphere outside, out Vec3 beam)
		{
			beam = default(Vec3);
			if (outside == null || !outside.Natural || outside.Night >= 1f)
			{
				return false;
			}
			Vec3 sunDisc = outside.SunDisc;
			float num = (float)Math.Sqrt(sunDisc.X * sunDisc.X + sunDisc.Y * sunDisc.Y);
			if (num < 0.0001f)
			{
				return false;
			}
			float num2 = Math.Max(MathF.PI * 2f / 45f, (float)Math.Asin(Math.Max(-1f, Math.Min(1f, sunDisc.Z))));
			float num3 = (float)Math.Cos(num2);
			beam = new Vec3(sunDisc.X / num * num3, sunDisc.Y / num * num3, (float)Math.Sin(num2));
			return true;
		}

		public static Atmosphere Noon()
		{
			return ForTime(0.5f);
		}

		public Atmosphere Overcast(float k, uint cloud)
		{
			if (k <= 0.001f)
			{
				return this;
			}
			k = Math.Min(1f, k);
			float num = 1f - (1f - k) * (1f - k);
			int num2 = (int)((float)(Luma(Zenith) + Luma(Horizon)) * 0.5f);
			uint num3 = TerrainCache.Lerp(TerrainCache.Rgb(num2, num2, num2 + 8), cloud, 0.3f);
			uint b = TerrainCache.Scale(num3, 0.72f);
			Atmosphere obj = (Atmosphere)MemberwiseClone();
			obj.Zenith = TerrainCache.Lerp(Zenith, b, num * 0.9f);
			obj.Horizon = TerrainCache.Lerp(Horizon, num3, num * 0.85f);
			obj.SunHorizon = TerrainCache.Lerp(SunHorizon, num3, num * 0.9f);
			obj.CloudLit = TerrainCache.Lerp(CloudLit, TerrainCache.Scale(num3, 1.05f), num * 0.85f);
			obj.CloudShade = TerrainCache.Lerp(CloudShade, TerrainCache.Scale(num3, 0.55f), num * 0.8f);
			obj.LightR = LightR * (1f - 0.7f * k);
			obj.LightG = LightG * (1f - 0.7f * k);
			obj.LightB = LightB * (1f - 0.7f * k);
			obj.AmbR = AmbR * (1f - 0.22f * k);
			obj.AmbG = AmbG * (1f - 0.22f * k);
			obj.AmbB = AmbB * (1f - 0.18f * k);
			obj.HazeDensity = HazeDensity * (1f + 1.6f * k);
			obj.Clouds = Clouds + (1f - Clouds) * 0.85f * num;
			obj.SunVisible = SunVisible * (1f - 0.9f * k);
			obj.Rays = Rays * (1f - k);
			obj.Stars = Stars * (1f - k);
			return obj;
		}

		public Atmosphere Snowfall(float k)
		{
			if (k <= 0.001f)
			{
				return this;
			}
			k = Math.Min(1f, k);
			int num = (int)((float)(Luma(Zenith) + Luma(Horizon)) * 0.5f);
			int num2 = Math.Min(255, num + 28);
			uint num3 = TerrainCache.Rgb(num2 - 6, num2 - 2, (num2 + 4 > 255) ? 255 : (num2 + 4));
			Atmosphere obj = (Atmosphere)MemberwiseClone();
			obj.Horizon = TerrainCache.Lerp(Horizon, num3, 0.7f * k);
			obj.SunHorizon = TerrainCache.Lerp(SunHorizon, num3, 0.7f * k);
			obj.Zenith = TerrainCache.Lerp(Zenith, TerrainCache.Scale(num3, 0.86f), 0.55f * k);
			obj.HazeDensity = HazeDensity * (1f + 2.6f * k);
			obj.LightR = LightR * (1f - 0.25f * k);
			obj.LightG = LightG * (1f - 0.25f * k);
			obj.LightB = LightB * (1f - 0.2f * k);
			float num4 = (AmbR + AmbG + AmbB) * (1f / 3f) * 1.12f;
			obj.AmbR = AmbR + (num4 - AmbR) * 0.65f * k;
			obj.AmbG = AmbG + (num4 - AmbG) * 0.65f * k;
			obj.AmbB = AmbB + (num4 * 1.03f - AmbB) * 0.65f * k;
			obj.SunVisible = SunVisible * (1f - 0.6f * k);
			return obj;
		}

		public Atmosphere WithAge(string age, float k, out Precip falls, out float strength)
		{
			falls = Precip.None;
			strength = 0f;
			if (string.IsNullOrEmpty(age) || k <= 0.001f)
			{
				return this;
			}
			Atmosphere atmosphere = this;
			switch (age)
			{
			case "age_sun":
				atmosphere = Tinted(TerrainCache.Rgb(255, 184, 80), 0.4f * k, 1f + 0.18f * k, TerrainCache.Rgb(255, 210, 130));
				atmosphere.Rays = Math.Min(1f, Rays + 0.4f * k);
				atmosphere.HazeDensity = HazeDensity * (1f + 0.5f * k);
				falls = Precip.Sun;
				strength = 0.35f * k;
				break;
			case "age_dark":
				atmosphere = Tinted(TerrainCache.Rgb(70, 60, 110), 0.35f * k, 1f - 0.25f * k, TerrainCache.Rgb(150, 140, 200));
				atmosphere.Stars = Math.Max(Stars, 0.6f * k);
				break;
			case "age_tears":
				atmosphere = Overcast(0.75f * k, TerrainCache.Rgb(108, 151, 216));
				falls = Precip.Rain;
				strength = 0.7f * k;
				break;
			case "age_moon":
				atmosphere = Tinted(TerrainCache.Rgb(120, 230, 240), 0.3f * k, 1f, TerrainCache.Rgb(141, 255, 243));
				atmosphere.MoonVisible = Math.Max(MoonVisible, k);
				atmosphere.MoonScale = 1f + 1.6f * k;
				atmosphere.Stars = Math.Max(Stars, 0.8f * k);
				break;
			case "age_chaos":
				atmosphere = Tinted(TerrainCache.Rgb(214, 52, 28), 0.68f * k, 1f, TerrainCache.Rgb(255, 110, 70));
				atmosphere.HazeDensity = HazeDensity * (1f + 0.8f * k);
				falls = Precip.Fire;
				strength = 0.12f * k;
				break;
			case "age_wonders":
				atmosphere = Tinted(TerrainCache.Rgb(214, 85, 156), 0.3f * k, 1.05f, TerrainCache.Rgb(255, 170, 230));
				atmosphere.Clouds = Clouds * (1f - 0.3f * k);
				falls = Precip.Magic;
				strength = 0.4f * k;
				break;
			case "age_ice":
				atmosphere = Overcast(0.45f * k, TerrainCache.Rgb(220, 245, 255));
				atmosphere = atmosphere.Tinted(TerrainCache.Rgb(200, 240, 255), 0.25f * k, 1f, TerrainCache.Rgb(220, 248, 255));
				falls = Precip.Snow;
				strength = 0.75f * k;
				break;
			case "age_ash":
				atmosphere = Overcast(0.6f * k, TerrainCache.Rgb(198, 176, 119));
				atmosphere = atmosphere.Tinted(TerrainCache.Rgb(170, 140, 100), 0.3f * k, 0.9f, TerrainCache.Rgb(220, 190, 140));
				atmosphere.HazeDensity *= 1f + 0.8f * k;
				falls = Precip.Ash;
				strength = 0.65f * k;
				break;
			case "age_despair":
				atmosphere = Overcast(0.55f * k, TerrainCache.Rgb(114, 133, 153));
				atmosphere = atmosphere.Tinted(TerrainCache.Rgb(90, 104, 130), 0.3f * k, 0.85f, TerrainCache.Rgb(160, 176, 200));
				falls = Precip.Snow;
				strength = 0.55f * k;
				break;
			}
			return atmosphere;
		}

		public Atmosphere Tinted(uint sky, float amount, float lightScale, uint lightTint)
		{
			Atmosphere obj = (Atmosphere)MemberwiseClone();
			obj.Zenith = TerrainCache.Lerp(Zenith, TerrainCache.Scale(sky, 0.7f), amount);
			obj.Horizon = TerrainCache.Lerp(Horizon, sky, amount);
			obj.SunHorizon = TerrainCache.Lerp(SunHorizon, sky, amount * 0.8f);
			obj.CloudLit = TerrainCache.Lerp(CloudLit, TerrainCache.Lerp(sky, uint.MaxValue, 0.5f), amount * 0.7f);
			obj.CloudShade = TerrainCache.Lerp(CloudShade, TerrainCache.Scale(sky, 0.6f), amount * 0.7f);
			float num = (float)(lightTint & 0xFF) / 255f;
			float num2 = (float)((lightTint >> 8) & 0xFF) / 255f;
			float num3 = (float)((lightTint >> 16) & 0xFF) / 255f;
			float num4 = Math.Max(num, Math.Max(num2, num3));
			num /= num4;
			num2 /= num4;
			num3 /= num4;
			float num5 = Math.Min(1f, amount * 1.4f);
			obj.LightR = LightR * lightScale * (1f + (num - 1f) * num5);
			obj.LightG = LightG * lightScale * (1f + (num2 - 1f) * num5);
			obj.LightB = LightB * lightScale * (1f + (num3 - 1f) * num5);
			obj.AmbR = AmbR * (1f + (num - 1f) * num5 * 0.6f);
			obj.AmbG = AmbG * (1f + (num2 - 1f) * num5 * 0.6f);
			obj.AmbB = AmbB * (1f + (num3 - 1f) * num5 * 0.6f);
			obj.SunColor = TerrainCache.Lerp(SunColor, lightTint, amount);
			return obj;
		}

		private static int Luma(uint c)
		{
			return (int)(((c & 0xFF) * 3 + ((c >> 8) & 0xFF) * 6 + ((c >> 16) & 0xFF)) / 10);
		}

		private static Vec3 Dir(float azimuth, float elevation)
		{
			float num = (float)Math.Cos(elevation);
			return new Vec3((float)Math.Cos(azimuth) * num, (float)Math.Sin(azimuth) * num, (float)Math.Sin(elevation));
		}

		private static float Mix(float a, float b, float t)
		{
			return a + (b - a) * t;
		}

		private static float Mist(float sunElevation)
		{
			return Smooth(-0.12f, 0f, sunElevation) * (1f - Smooth(0.1f, 0.28f, sunElevation));
		}

		private static float Smooth(float e0, float e1, float x)
		{
			float num = (x - e0) / (e1 - e0);
			num = ((num < 0f) ? 0f : ((num > 1f) ? 1f : num));
			return num * num * (3f - 2f * num);
		}
	}
}
