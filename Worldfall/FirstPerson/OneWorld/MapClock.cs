using System;
using System.Diagnostics;
using FirstPerson.Net;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson.OneWorld
{
	internal static class MapClock
	{
		private const float MaxStep = 5f;

		private static object _world;

		private static double _last = double.NaN;

		private static float _failedAt = -100f;

		internal static float CostMs;

		internal static float OccupancyMs;

		internal static float InsideMs;

		internal static float HousesMs;

		internal static float ActsMs;

		private static readonly Stopwatch Clock = new Stopwatch();

		internal static float LastStepForTest;

		public static void Failed(Exception e)
		{
			MapNight.Off();
			if (!(Time.unscaledTime - _failedAt < 60f))
			{
				_failedAt = Time.unscaledTime;
				Log.Error("one world: " + e);
			}
		}

		private static float Lap(ref long at)
		{
			long elapsedTicks = Clock.ElapsedTicks;
			float result = (float)((double)(elapsedTicks - at) * 1000.0 / (double)Stopwatch.Frequency);
			at = elapsedTicks;
			return result;
		}

		private static void Ease(ref float into, float ms)
		{
			into += (ms - into) * 0.05f;
		}

		public static void Tick(WorldBoxMod mod, bool flatMap)
		{
			DayCycle day = mod.Day;
			if (day == null || mod.Settings == null)
			{
				return;
			}
			if (!Config.game_loaded || World.world == null || WorldBoxMod.WorldLoading)
			{
				_last = double.NaN;
				MapNight.Off();
				return;
			}
			object obj = null;
			try
			{
				obj = World.world.map_stats;
			}
			catch
			{
			}
			double actionNow = WorldPace.ActionNow;
			float num = 0f;
			if (obj != _world || double.IsNaN(_last) || actionNow < _last)
			{
				_world = obj;
			}
			else
			{
				bool flag = false;
				try
				{
					flag = Config.fps_lock_30;
				}
				catch
				{
				}
				float num2 = Time.fixedDeltaTime * (flag ? 2f : 1f);
				num = Math.Min(((num2 > 0f) ? ((float)(actionNow - _last) / num2) : 0f) * Time.unscaledDeltaTime, 5f);
			}
			_last = actionNow;
			LastStepForTest = num;
			day.Advance(mod.Settings, num, Time.unscaledDeltaTime);
			if (WorldSync.HostHour(out var hour, out var day2))
			{
				day.TimeOfDay = hour;
				Calendar.FromHost(day2, hour);
			}
			else if (NetSession.Joined)
			{
				Calendar.Hold(day.TimeOfDay);
			}
			else
			{
				Calendar.Tick(day.TimeOfDay);
			}
			MapNight.Apply(mod.Settings, day, flatMap);
			Clock.Restart();
			long at = 0L;
			HouseOccupancy.Update(Time.unscaledDeltaTime);
			Ease(ref OccupancyMs, Lap(ref at));
			InsideSpots.Tick();
			Ease(ref InsideMs, Lap(ref at));
			RealActs.Tick(day);
			Ease(ref ActsMs, Lap(ref at));
			MapHouses.Tick(mod.Settings, flatMap);
			Ease(ref HousesMs, Lap(ref at));
			Ease(ref CostMs, (float)((double)Clock.ElapsedTicks * 1000.0 / (double)Stopwatch.Frequency));
		}
	}
}
