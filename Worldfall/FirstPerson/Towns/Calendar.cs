using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class Calendar
	{
		public const string Key = "fp_cal";

		public const float DawnAt = 0.25f;

		public const int DaysPerSeason = 2;

		private static int _day;

		private static int _billDay;

		private static bool _awayCounted;

		public const string BillKey = "fp_cal_bill";

		public const string AwayKey = "fp_cal_away";

		private static object _world;

		private static float _last = -1f;

		private static float _tickedAt = -100f;

		private static readonly Dictionary<string, int[]> Shares = new Dictionary<string, int[]>
		{
			["age_sun"] = new int[4] { 2, 4, 2, 0 },
			["age_ice"] = new int[4] { 1, 1, 2, 4 },
			["age_despair"] = new int[4] { 2, 1, 2, 3 },
			["age_ash"] = new int[4] { 2, 2, 3, 1 }
		};

		private static readonly int[] Even = new int[4] { 2, 2, 2, 2 };

		private static int _textDay = -1;

		private static string _textAge;

		private static string _text;

		private static readonly float[] _look = new float[4];

		private static bool _lookSet;

		private static readonly float[] _want = new float[4];

		public static int Day
		{
			get
			{
				if (World.world != null)
				{
					CheckWorld();
				}
				return _day;
			}
			private set
			{
				_day = value;
			}
		}

		public static int BillDay
		{
			get
			{
				if (World.world != null)
				{
					CheckWorld();
				}
				return _billDay;
			}
		}

		public static float TimeOfDay => ((WorldBoxMod.Instance != null) ? WorldBoxMod.Instance.Day : null)?.TimeOfDay ?? 0.36f;

		public static Season Season
		{
			get
			{
				int dayIn;
				int length;
				return SeasonOf(Day, out dayIn, out length);
			}
		}

		public static float SeasonProgress
		{
			get
			{
				SeasonOf(Day, out var dayIn, out var length);
				return Mathf.Clamp01(((float)dayIn + SinceDawn) / (float)Mathf.Max(1, length));
			}
		}

		internal static float SinceDawn
		{
			get
			{
				float num = TimeOfDay - 0.25f;
				if (!(num < 0f))
				{
					return num;
				}
				return num + 1f;
			}
		}

		public static bool Running => Time.unscaledTime - _tickedAt < 2f;

		private static string AgeNow
		{
			get
			{
				try
				{
					return (World.world_era != null) ? World.world_era.id : null;
				}
				catch
				{
					return null;
				}
			}
		}

		internal static string DateText
		{
			get
			{
				int day = Day;
				string ageNow = AgeNow;
				if (day != _textDay || ageNow != _textAge || _text == null)
				{
					_text = SeasonOf(day, SharesOf(ageNow), out var dayIn, out var _).ToString() + ", day " + (dayIn + 1);
					_textDay = day;
					_textAge = ageNow;
				}
				return _text;
			}
		}

		internal static float[] LookForTest => (float[])_look.Clone();

		public static event Action<int> BillDawn;

		public static event Action<int> Dawn;

		internal static void Tick(float timeOfDay)
		{
			if (World.world == null)
			{
				return;
			}
			_tickedAt = Time.unscaledTime;
			CheckWorld();
			if (WithBody())
			{
				_awayCounted = false;
			}
			float last = _last;
			_last = timeOfDay;
			if (last < 0f)
			{
				return;
			}
			bool num;
			if (!(timeOfDay < last))
			{
				if (!(last < 0.25f))
				{
					return;
				}
				num = timeOfDay >= 0.25f;
			}
			else
			{
				if (last < 0.25f)
				{
					goto IL_0075;
				}
				num = timeOfDay >= 0.25f;
			}
			if (!num)
			{
				return;
			}
			goto IL_0075;
			IL_0075:
			NewDay();
		}

		internal static void FromHost(int day, float timeOfDay)
		{
			if (World.world == null)
			{
				return;
			}
			_tickedAt = Time.unscaledTime;
			CheckWorld();
			_last = timeOfDay;
			if (WithBody())
			{
				_awayCounted = false;
			}
			if (day <= Day)
			{
				return;
			}
			if (day - Day > 3)
			{
				Day = day;
				_billDay = Math.Max(_billDay, Day - 1);
			}
			else
			{
				while (Day < day)
				{
					NewDay(save: false);
				}
			}
		}

		internal static void Hold(float timeOfDay)
		{
			if (!(World.world == null))
			{
				_tickedAt = Time.unscaledTime;
				CheckWorld();
				_last = timeOfDay;
			}
		}

		public static int SkipToDawn()
		{
			DayCycle dayCycle = ((WorldBoxMod.Instance != null) ? WorldBoxMod.Instance.Day : null);
			if (dayCycle != null)
			{
				dayCycle.TimeOfDay = 0.255f;
			}
			_last = 0.255f;
			NewDay();
			return Day;
		}

		private static void NewDay(bool save = true)
		{
			Day++;
			bool flag = WithBody() || !_awayCounted;
			if (flag)
			{
				_billDay++;
				if (!WithBody())
				{
					_awayCounted = true;
				}
			}
			if (save)
			{
				Save();
			}
			Log.Info(string.Format("calendar: dawn of day {0} ({1}, {2:0.00} through it){3}", Day, Season, SeasonProgress, flag ? "" : ", not a bill day"));
			try
			{
				Calendar.Dawn?.Invoke(Day);
			}
			catch (Exception e)
			{
				Hub.Fail("dawn", e);
			}
			if (flag)
			{
				try
				{
					Calendar.BillDawn?.Invoke(_billDay);
				}
				catch (Exception e2)
				{
					Hub.Fail("bill dawn", e2);
				}
			}
		}

		private static bool WithBody()
		{
			try
			{
				return ControllableUnit.isControllingUnit();
			}
			catch
			{
				return false;
			}
		}

		private static void CheckWorld()
		{
			object map_stats = World.world.map_stats;
			if (map_stats == _world)
			{
				return;
			}
			_world = map_stats;
			_last = -1f;
			Day = 0;
			_billDay = 0;
			_awayCounted = false;
			try
			{
				SaveCustomData saveCustomData = World.world.map_stats?.custom_data;
				if (saveCustomData != null)
				{
					saveCustomData.get("fp_cal", out var pResult, 0);
					Day = Mathf.Max(0, pResult);
					saveCustomData.get("fp_cal_bill", out var pResult2, -1);
					_billDay = ((pResult2 >= 0) ? Mathf.Min(pResult2, Day) : Day);
					saveCustomData.get("fp_cal_away", out var pResult3, 0);
					_awayCounted = pResult3 != 0;
				}
			}
			catch (Exception ex)
			{
				Log.Error("calendar: could not read the date: " + ex.Message);
			}
		}

		private static void Save()
		{
			try
			{
				MapStats mapStats = ((World.world != null) ? World.world.map_stats : null);
				if (mapStats != null)
				{
					if (mapStats.custom_data == null)
					{
						mapStats.custom_data = new SaveCustomData();
					}
					mapStats.custom_data.set("fp_cal", Day);
					mapStats.custom_data.set("fp_cal_bill", _billDay);
					mapStats.custom_data.set("fp_cal_away", _awayCounted ? 1 : 0);
				}
			}
			catch (Exception ex)
			{
				Log.Error("calendar: could not save the date: " + ex.Message);
			}
		}

		internal static void SetDayForTest(int day)
		{
			if (!(World.world == null))
			{
				CheckWorld();
				Day = Mathf.Max(0, day);
				_billDay = Day;
				Save();
			}
		}

		internal static int[] SharesOf(string age)
		{
			if (age == null || !Shares.TryGetValue(age, out var value))
			{
				return Even;
			}
			return value;
		}

		internal static Season SeasonOf(int day, out int dayIn, out int length)
		{
			return SeasonOf(day, SharesOf(AgeNow), out dayIn, out length);
		}

		internal static Season SeasonOf(int day, int[] shares, out int dayIn, out int length)
		{
			int num = 0;
			foreach (int num2 in shares)
			{
				num += num2;
			}
			int num3 = ((num > 0) ? ((day % num + num) % num) : 0);
			for (int j = 0; j < 4; j++)
			{
				if (num3 < shares[j])
				{
					dayIn = num3;
					length = shares[j];
					return (Season)j;
				}
				num3 -= shares[j];
			}
			dayIn = 0;
			length = 1;
			return Season.Spring;
		}

		private static Season After(Season s, int[] shares)
		{
			for (int i = 1; i <= 4; i++)
			{
				Season season = (Season)((int)(s + i) % 4);
				if (shares[(int)season] > 0)
				{
					return season;
				}
			}
			return s;
		}

		internal static void DayNight(string age, out float day, out float night)
		{
			switch (age)
			{
			case "age_sun":
				day = 15f;
				night = 5f;
				break;
			case "age_wonders":
				day = 13f;
				night = 7f;
				break;
			case "age_ash":
			case "age_chaos":
				day = 10f;
				night = 10f;
				break;
			case "age_ice":
				day = 9f;
				night = 11f;
				break;
			case "age_moon":
				day = 8f;
				night = 12f;
				break;
			case "age_despair":
				day = 7f;
				night = 13f;
				break;
			case "age_dark":
				day = 5f;
				night = 15f;
				break;
			default:
				day = 12f;
				night = 8f;
				break;
			}
		}

		internal static void UpdateLook(float dt)
		{
			if (!(World.world == null))
			{
				float[] want = _want;
				want[0] = (want[1] = (want[2] = (want[3] = 0f)));
				int[] shares = SharesOf(AgeNow);
				int dayIn;
				int length;
				Season season = SeasonOf(Day, shares, out dayIn, out length);
				float num = Mathf.Clamp01(((float)dayIn + SinceDawn - ((float)length - 0.5f)) / 0.5f);
				Season season2 = After(season, shares);
				want[(int)season] += 1f - num;
				want[(int)season2] += num;
				if (AgeNow == "age_ice")
				{
					want[3] = Mathf.Min(1f, want[3] * 1.2f);
				}
				float maxDelta = (_lookSet ? (dt / 4f) : 1f);
				for (int i = 0; i < 4; i++)
				{
					_look[i] = Mathf.MoveTowards(_look[i], want[i], maxDelta);
				}
				_lookSet = true;
				SeasonLook.Set(_look[0], _look[1], _look[2], _look[3]);
			}
		}
	}
}
