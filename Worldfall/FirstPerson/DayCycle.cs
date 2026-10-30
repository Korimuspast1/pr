using System;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class DayCycle
	{
		public static readonly string[] ModeNames = new string[4] { "Day and night", "Always day", "Golden hour", "Always night" };

		public float TimeOfDay = 0.36f;

		public float WorldNight;

		private const float NightMax = 0.6f;

		internal float? TestDeclination;

		internal float? TestMoonCycle;

		internal float? TestMoonLit;

		private double _calendarDays;

		private bool _calendarSet;

		private bool _declinationSet;

		private const float DeclinationEase = MathF.PI / 90f;

		private const double MoonStart = 0.4;

		private string _age;

		private string _previousAge;

		private float _ageBlend = 1f;

		private int _days;

		private float _fixedClock;

		private readonly int _nightBase = new System.Random().Next(600);

		public Precip AgeFalls;

		public float AgeFallStrength;

		public float Hour { get; private set; } = 0.36f;

		public Atmosphere Air { get; private set; } = Atmosphere.ForTime(0.36f);

		public bool NaturalCycle { get; private set; }

		public float Declination { get; private set; }

		public float MoonCycle { get; private set; }

		public int Night { get; private set; }

		public float Cold { get; private set; }

		public string Age => _age;

		public static float Darkness(int setting)
		{
			if (setting > 0)
			{
				if (setting != 1)
				{
					return 0.8f;
				}
				return 0.62f;
			}
			return 0f;
		}

		public void Advance(Settings settings, float worldDt, float realDt)
		{
			NaturalCycle = settings.TimeMode == 0 && WorldBoxMod.Instance != null && WorldBoxMod.Instance.NaturalSun;
			float timeOfDay = TimeOfDay;
			if (worldDt > 0f)
			{
				TimeOfDay += worldDt * Rate(settings);
				if (TimeOfDay >= 1f)
				{
					_days++;
				}
				TimeOfDay -= Mathf.Floor(TimeOfDay);
				if (settings.TimeMode != 0)
				{
					_fixedClock += worldDt / Mathf.Max(1f, settings.DayMinutes * 60f);
				}
			}
			float num = TimeOfDay - timeOfDay;
			FollowSeasons((num < 0f) ? (num + 1f) : num, realDt);
			Night = _nightBase + ((settings.TimeMode == 0) ? (_days + ((TimeOfDay >= 0.5f) ? 1 : 0)) : Mathf.FloorToInt(_fixedClock));
			Hour = Shown(settings);
		}

		private float Shown(Settings settings)
		{
			switch (settings.TimeMode)
			{
			case 1:
				return 0.46f;
			case 2:
				return 0.705f;
			case 3:
				return 0.02f;
			default:
				return TimeOfDay;
			}
		}

		public void Update(Settings settings, float dt, bool fromAbove = false)
		{
			NaturalCycle = settings.TimeMode == 0 && WorldBoxMod.Instance != null && WorldBoxMod.Instance.NaturalSun;
			float t = (Hour = Shown(settings));
			Atmosphere atmosphere = SkyAt(t);
			try
			{
				float num2 = ((World.world != null && World.world.era_manager != null) ? World.world.era_manager._night_multiplier : 0f);
				WorldNight = Mathf.Clamp01(num2 / 0.6f);
			}
			catch
			{
				WorldNight = 0f;
			}
			if (WorldNight > 0.01f && settings.TimeMode != 3)
			{
				atmosphere = atmosphere.Blend(Atmosphere.ForTime(0.02f), WorldNight);
			}
			atmosphere = atmosphere.NightDarker(Darkness(settings.NightDark) * (fromAbove ? 0.5f : 1f));
			string text = null;
			try
			{
				text = ((!(World.world != null) || World.world.era_manager == null) ? null : World.world.era_manager.getCurrentAge()?.id);
			}
			catch
			{
			}
			if (text != _age)
			{
				_previousAge = ((_ageBlend > 0.5f) ? _age : _previousAge);
				_age = text;
				_ageBlend = 0f;
			}
			_ageBlend = Mathf.MoveTowards(_ageBlend, 1f, dt / 5f);
			Precip falls;
			float strength;
			Atmosphere atmosphere2 = atmosphere.WithAge(_age, 1f, out falls, out strength);
			if (_ageBlend < 1f && _previousAge != null)
			{
				atmosphere2 = atmosphere.WithAge(_previousAge, 1f, out var falls2, out var strength2).Blend(atmosphere2, _ageBlend);
				if (_ageBlend < 0.5f)
				{
					falls = falls2;
					strength = strength2 * (1f - _ageBlend * 2f);
				}
				else
				{
					strength *= (_ageBlend - 0.5f) * 2f;
				}
			}
			else if (_ageBlend < 1f)
			{
				atmosphere2 = atmosphere.Blend(atmosphere2, _ageBlend);
				strength *= _ageBlend;
			}
			AgeFalls = falls;
			AgeFallStrength = strength;
			SeasonLook.Light(atmosphere2);
			Air = atmosphere2;
			float target = ((_age == "age_ice") ? 1f : ((_age == "age_despair") ? 0.6f : 0f));
			Cold = Mathf.MoveTowards(Cold, target, dt / 5f);
		}

		public Atmosphere SkyAt(float t)
		{
			if (!NaturalCycle)
			{
				return Atmosphere.ForTime(t);
			}
			return Atmosphere.NaturalAt(t, Declination, MoonCycle, TestMoonLit ?? (-1f));
		}

		internal float Rate(Settings settings)
		{
			if (NaturalCycle)
			{
				return NaturalRate(settings);
			}
			if (Run.Locked)
			{
				Calendar.DayNight(_age, out var day, out var night);
				bool flag = TimeOfDay >= 0.25f && TimeOfDay < 0.75f;
				return 0.5f / Mathf.Max(1f, (flag ? day : night) * 60f);
			}
			return 1f / Mathf.Max(1f, settings.DayMinutes * 60f);
		}

		private float NaturalRate(Settings settings)
		{
			NaturalHalves(settings, out var day, out var night);
			bool flag = TimeOfDay >= 0.25f && TimeOfDay < 0.75f;
			return 0.5f / Mathf.Max(1f, (flag ? day : night) * 60f);
		}

		private void NaturalHalves(Settings settings, out float day, out float night)
		{
			float num = 0.1f * (SeasonLook.Summer - SeasonLook.Winter);
			float num2 = 0.5f + num;
			float num3 = settings.DayMinutes;
			if (Run.Locked)
			{
				Calendar.DayNight(_age, out var day2, out var night2);
				num3 = day2 + night2;
				num2 = Mathf.Clamp(day2 / Mathf.Max(1f, num3) + num, 0.2f, 0.8f);
			}
			day = num2 * num3;
			night = (1f - num2) * num3;
		}

		internal void HalfMinutes(Settings settings, out float day, out float night)
		{
			if (NaturalCycle)
			{
				NaturalHalves(settings, out day, out night);
			}
			else if (Run.Locked)
			{
				Calendar.DayNight(_age, out day, out night);
			}
			else
			{
				day = (night = settings.DayMinutes * 0.5f);
			}
		}

		private void FollowSeasons(float advance, float dt)
		{
			_calendarDays += advance;
			float num = 0f;
			if (World.world != null)
			{
				try
				{
					double num2 = Calendar.SinceDawn;
					double num3 = (double)Calendar.Day + num2;
					double num4 = num3 - _calendarDays;
					bool flag = num4 > -1.02 && num4 < -0.98 && num2 < 0.05;
					if (!_calendarSet || (Math.Abs(num4) > 0.01 && !flag))
					{
						_calendarDays = num3;
						_declinationSet = false;
					}
					_calendarSet = true;
					int num5 = (int)Math.Floor(_calendarDays);
					int dayIn;
					int length;
					Season num6 = Calendar.SeasonOf(num5, out dayIn, out length);
					float num7 = (float)(((double)dayIn + (_calendarDays - (double)num5)) / (double)Math.Max(1, length));
					num = SunPath.Declination(((float)num6 + num7 - 0.5f) * (MathF.PI / 2f));
				}
				catch
				{
				}
			}
			Declination = (_declinationSet ? Mathf.MoveTowards(Declination, num, MathF.PI / 90f * dt) : num);
			_declinationSet = true;
			if (TestDeclination.HasValue)
			{
				Declination = TestDeclination.Value;
				_declinationSet = false;
			}
			double num8 = _calendarDays / 8.0 + 0.4;
			MoonCycle = TestMoonCycle ?? ((float)(num8 - Math.Floor(num8)));
		}

		internal void FinishAgeChange()
		{
			_ageBlend = 1f;
		}

		public string Describe(Settings settings)
		{
			if (settings.TimeMode != 0)
			{
				return ModeNames[settings.TimeMode];
			}
			float timeOfDay = TimeOfDay;
			string text = ((timeOfDay < 0.2f || timeOfDay > 0.83f) ? "night" : ((timeOfDay < 0.3f) ? "dawn" : ((timeOfDay < 0.62f) ? "day" : ((timeOfDay < 0.78f) ? "evening" : "dusk"))));
			return ModeNames[0] + " (now " + text + ")";
		}
	}
}
