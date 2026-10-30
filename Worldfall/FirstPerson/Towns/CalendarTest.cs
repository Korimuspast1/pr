using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal sealed class CalendarTest : CivilTest
	{
		protected override string Prefix => "CAL";

		protected override IEnumerator Body()
		{
			Check("an even age: 2 days a season", Show(Count("age_hope")) == "2/2/2/2", Show(Count("age_hope")));
			Check("the Age of Sun: a long summer, no winter", Show(Count("age_sun")) == "2/4/2/0", Show(Count("age_sun")));
			Check("the Age of Ice: a long winter", Show(Count("age_ice")) == "1/1/2/4", Show(Count("age_ice")));
			Check("the Ages of Despair and Ash", Show(Count("age_despair")) == "2/1/2/3" && Show(Count("age_ash")) == "2/2/3/1");
			Check("a new year starts in spring", Calendar.SeasonOf(8, Calendar.SharesOf("age_ice"), out var dayIn, out var _) == Season.Spring && dayIn == 0);
			Calendar.DayNight("age_hope", out var day, out var night);
			Calendar.DayNight("age_moon", out var day2, out var night2);
			Calendar.DayNight("age_dark", out var day3, out var night3);
			Check("day and night: 12 and 8 minutes, a long night in the Age of Moon, longer in the Age of Dark", day == 12f && night == 8f && day2 == 8f && night2 == 12f && day3 == 5f && night3 == 15f && day + night == 20f && day2 + night2 == 20f);
			string text = Path.Combine(Log.Folder, "runs_caltest.txt");
			try
			{
				if (File.Exists(text))
				{
					File.Delete(text);
				}
			}
			catch
			{
			}
			Run.HallFileForTest = text;
			Run.Forget();
			Actor king = Village("human", 6, 0);
			if (!Check("a village", king != null))
			{
				yield break;
			}
			Actor me = Founded.FirstOrDefault((Actor u) => u != null && u != king && u.isAdult()) ?? king;
			foreach (Actor item in Founded)
			{
				try
				{
					((BaseSimObject)item).addStatusEffect("invincible", 600f, pColorEffect: false);
				}
				catch
				{
				}
			}
			yield return Possess(me);
			if (!Check("first person", InControl(me)))
			{
				yield break;
			}
			Mod.Settings.TimeMode = 0;
			Run.Picked.Clear();
			Check("locked in", Run.Lock(me, Run.Picked, base.Note) && Run.Locked);
			string age = Mod.Day.Age;
			Calendar.DayNight(age, out var wantDay, out var wantNight);
			Mod.Day.TimeOfDay = 0.4f;
			float t0 = Time.unscaledTime;
			float h0 = Mod.Day.TimeOfDay;
			yield return new WaitForSecondsRealtime(3f);
			float dayRate = (Mod.Day.TimeOfDay - h0) / Mathf.Max(0.1f, Time.unscaledTime - t0);
			Mod.Day.TimeOfDay = 0.85f;
			t0 = Time.unscaledTime;
			h0 = Mod.Day.TimeOfDay;
			yield return new WaitForSecondsRealtime(3f);
			float b = (Mod.Day.TimeOfDay - h0) / Mathf.Max(0.1f, Time.unscaledTime - t0);
			float num = 0.5f / Mathf.Max(1E-06f, dayRate) / 60f;
			float num2 = 0.5f / Mathf.Max(1E-06f, b) / 60f;
			Note($"in {age}: the day went at {num:F1} minutes a day, the night at {num2:F1} (want {wantDay}/{wantNight})");
			Check("the day lasts as the age has it (within 15%)", Mathf.Abs(num / wantDay - 1f) < 0.15f, $"{num:F1} min");
			Check("...and the night", Mathf.Abs(num2 / wantNight - 1f) < 0.15f, $"{num2:F1} min");
			List<int> dawns = new List<int>();
			Action<int> onDawn = delegate(int d)
			{
				dawns.Add(d);
			};
			Calendar.Dawn += onDawn;
			int before = Calendar.Day;
			Mod.Day.TimeOfDay = 0.2488f;
			for (float t1 = 0f; t1 < 6f; t1 += Time.unscaledDeltaTime)
			{
				if (dawns.Count != 0)
				{
					break;
				}
				yield return null;
			}
			yield return new WaitForSecondsRealtime(1f);
			Calendar.Dawn -= onDawn;
			Check("a dawn crossed counts one day", dawns.Count == 1 && Calendar.Day == before + 1, $"{dawns.Count} dawns, day {before} -> {Calendar.Day}");
			Mod.Day.TimeOfDay = 0.45f;
			TerrainCache land = Mod.TerrainForTest;
			Vector2 at = me.current_position;
			int greyTiles = 0;
			float satNear = 0f;
			int summerDay = Find(Season.Summer);
			int winterDay = Find(Season.Winter);
			int autumnDay = Find(Season.Autumn);
			if (summerDay >= 0)
			{
				Calendar.SetDayForTest(summerDay + 8);
				yield return new WaitForSecondsRealtime(6f);
			}
			float summerGrey = GreyNear();
			float summerGreen = satNear;
			Capture("cal_summer");
			if (winterDay >= 0)
			{
				Calendar.SetDayForTest(winterDay + 16);
				yield return new WaitForSecondsRealtime(6f);
				float[] lookForTest = Calendar.LookForTest;
				Check("in winter the look is winter", lookForTest[3] > 0.9f && SeasonLook.Winter > 0.9f && SeasonLook.Stamp != 0, string.Join(" ", lookForTest.Select((float v) => v.ToString("0.00")).ToArray()));
				Mod.SamplerForTest.RelightAll();
				yield return null;
				float num3 = GreyNear();
				float t1 = satNear;
				if (greyTiles < 10)
				{
					Note($"winter on the land round you: only {greyTiles} tiles of grass, soil or fields near (summer {summerGrey:F0}, winter {num3:F0})");
				}
				else
				{
					Check("the grass round you goes dun in winter", t1 < summerGreen - 10f, $"green over the rest: summer {summerGreen:F0}, winter {t1:F0}, {greyTiles} tiles");
					Check("...and no snow of the season's own", num3 < 170f, $"darkest channel: summer {summerGrey:F0}, winter {num3:F0}");
				}
				Check("the age panel's date", Calendar.DateText == "Winter, day 1", Calendar.DateText);
				WinterTrees();
				Capture("cal_winter");
				yield return new WaitForSecondsRealtime(0.5f);
				int num4 = Find(Season.Spring);
				if (num4 >= 0)
				{
					Calendar.SetDayForTest(num4 + 24);
					yield return new WaitForSecondsRealtime(6f);
					Mod.SamplerForTest.RelightAll();
					yield return new WaitForSecondsRealtime(1f);
					float num5 = ((GreyNear() > 0f) ? satNear : 0f);
					if (greyTiles >= 10)
					{
						Check("green again in spring", num5 > t1 + 10f, $"winter {t1:F0}, spring {num5:F0}");
					}
					Capture("cal_spring");
				}
			}
			else
			{
				Note("no winter in " + age + ": no snow to look for");
			}
			if (autumnDay >= 0)
			{
				Calendar.SetDayForTest(autumnDay + 24);
				yield return new WaitForSecondsRealtime(5f);
				Check("in autumn the look is autumn", SeasonLook.Autumn > 0.9f && SeasonLook.Winter < 0.05f);
				Capture("cal_autumn");
				yield return new WaitForSecondsRealtime(0.5f);
			}
			Calendar.SetDayForTest(((summerDay >= 0) ? summerDay : 0) + 32 + Calendar.SharesOf(age)[1] - 1);
			Mod.Day.TimeOfDay = 0.15f;
			yield return new WaitForSecondsRealtime(5f);
			float[] lookForTest2 = Calendar.LookForTest;
			Check("late on summer's last day, autumn is coming in", lookForTest2[1] > 0.05f && lookForTest2[2] > 0.05f, string.Join(" ", lookForTest2.Select((float v) => v.ToString("0.00")).ToArray()));
			Run.Asking = Run.Ask.Abandon;
			Mod.RunAnswer(abandon: true);
			Run.LegacyOpen = false;
			Run.HallFileForTest = null;
			int[] Count(string age2)
			{
				int[] array = new int[4];
				for (int i = 0; i < 8; i++)
				{
					array[(int)Calendar.SeasonOf(i, Calendar.SharesOf(age2), out var _, out var _)]++;
				}
				return array;
			}
			int Find(Season want)
			{
				for (int i = 0; i < 8; i++)
				{
					if (Calendar.SeasonOf(i, Calendar.SharesOf(age), out var dayIn2, out var _) == want && dayIn2 == 0)
					{
						return i;
					}
				}
				return -1;
			}
			float GreyNear()
			{
				float num6 = 0f;
				float num7 = 0f;
				int num8 = 0;
				for (int i = (int)at.y - 12; i <= (int)at.y + 12; i++)
				{
					for (int j = (int)at.x - 12; j <= (int)at.x + 12; j++)
					{
						if (j >= 0 && i >= 0 && j < land.Width && i < land.Height)
						{
							int num9 = i * land.Width + j;
							if ((land.Flags[num9] & 3) == 0)
							{
								byte b2 = ((land.SeasonMaterials.Length == land.Materials.Length) ? land.SeasonMaterials[num9] : land.Materials[num9]);
								if (b2 == 1 || b2 == 0 || b2 == 3 || b2 == 7)
								{
									uint num10 = land.Colors[num9];
									num6 += Mathf.Min(num10 & 0xFF, Mathf.Min((num10 >> 8) & 0xFF, (num10 >> 16) & 0xFF));
									num7 += (float)((num10 >> 8) & 0xFF) - 0.5f * (float)((num10 & 0xFF) + ((num10 >> 16) & 0xFF));
									num8++;
								}
							}
						}
					}
				}
				greyTiles = num8;
				satNear = ((num8 > 0) ? (num7 / (float)num8) : 0f);
				if (num8 <= 0)
				{
					return 0f;
				}
				return num6 / (float)num8;
			}
			string Show(int[] n)
			{
				return string.Join("/", n.Select((int x) => x.ToString()).ToArray());
			}
		}

		private void WinterTrees()
		{
			MeshModel meshModel = Models.Get("tree_maple") ?? Models.Get("tree_birch_b");
			if (meshModel == null)
			{
				Note("no broadleaf tree model to check");
				return;
			}
			int num = meshModel.SlotIndex("leaf_light");
			uint[] array = new uint[meshModel.SlotNames.Length];
			if (num >= 0)
			{
				array[num] = TerrainCache.Rgb(70, 140, 60) | 0xFF000000u;
			}
			uint[] array2 = SeasonLook.Tree(meshModel, array, 5u);
			float num2 = ((num >= 0) ? ((float)((array2[num] >> 8) & 0xFF)) : 0f);
			Check("a broadleaf crown turns dull, not white", num >= 0 && array2 != array && Bright(array2[num]) < Bright(array[num]) + 15f && num2 < 140f, (num >= 0) ? $"{Bright(array[num]):F0} -> {Bright(array2[num]):F0}" : "no leaf slot");
		}

		private static float Bright(uint c)
		{
			return (float)((c & 0xFF) + ((c >> 8) & 0xFF) + ((c >> 16) & 0xFF)) / 3f;
		}

		private void Capture(string name)
		{
			try
			{
				Mod.SaveFrame(name + ".png");
			}
			catch (Exception ex)
			{
				Note("picture: " + ex.Message);
			}
		}
	}
}
