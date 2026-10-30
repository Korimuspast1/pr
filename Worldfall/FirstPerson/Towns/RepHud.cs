using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class RepHud
	{
		private const int ToastAtLeast = 5;

		private const float BarSlide = 0.8f;

		private const string Chime = "event:/SFX/UI/YesPoint";

		private static long _city = -1L;

		private static long _realm = -1L;

		private static int _from;

		private static int _to;

		private static int _realmFrom;

		private static int _realmTo;

		private static float _at = -10f;

		private static float _levelAt = -10f;

		private static float _realmAt = -10f;

		private static float _realmLevelAt = -10f;

		public static readonly Color Gold = new Color(1f, 0.82f, 0.32f);

		private static readonly Color DistrustedColor = new Color(1f, 0.66f, 0.36f);

		private static readonly Color OutlawColor = new Color(0.95f, 0.38f, 0.32f);

		private static readonly List<KeyValuePair<string, string>> Waiting = new List<KeyValuePair<string, string>>(4);

		private static string _ours;

		private static bool _oursLevel;

		internal static int WaitingForTest => Waiting.Count;

		internal static void Install()
		{
			Rep.Shown = Shown;
			Rep.ShownRealm = ShownRealm;
		}

		private static void Shown(Actor body, City c, int before, int after, string reason)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance == null || body == null || body != instance.Host)
			{
				return;
			}
			int num = after - before;
			RepLevel repLevel = Rep.LevelOf(before);
			RepLevel repLevel2 = Rep.LevelOf(after);
			string text = Rep.NameOf(c) ?? "the town";
			float unscaledTime = Time.unscaledTime;
			long num2 = Rep.IdOf(c);
			_from = ((_city == num2 && unscaledTime - _at < 0.8f) ? Mathf.RoundToInt(Mathf.Lerp(_from, _to, Ease((unscaledTime - _at) / 0.8f))) : before);
			_city = num2;
			_to = after;
			_at = unscaledTime;
			instance.TownShown = c;
			instance.TownUntil = Mathf.Max(instance.TownUntil, unscaledTime + 5.5f);
			if (repLevel2 != repLevel)
			{
				_levelAt = unscaledTime;
				if (repLevel2 > repLevel)
				{
					Sounds.PlayUI("event:/SFX/UI/YesPoint");
				}
				Notice("town:" + num2, LevelLine(repLevel, repLevel2, text));
			}
			else if (Mathf.Abs(num) >= 5)
			{
				Notice(null, string.Format("{0}{1} {2}", (num > 0) ? "+" : "", num, text) + (string.IsNullOrEmpty(reason) ? "" : (" · " + reason)));
			}
		}

		private static void ShownRealm(Actor body, Kingdom k, int before, int after, string reason, bool alone)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance == null || body == null || body != instance.Host)
			{
				return;
			}
			float unscaledTime = Time.unscaledTime;
			long num = Rep.IdOf(k);
			_realmFrom = ((_realm == num && unscaledTime - _realmAt < 0.8f) ? Mathf.RoundToInt(Mathf.Lerp(_realmFrom, _realmTo, Ease((unscaledTime - _realmAt) / 0.8f))) : before);
			_realm = num;
			_realmTo = after;
			_realmAt = unscaledTime;
			if (Rep.Reigns(body, k))
			{
				return;
			}
			if (alone)
			{
				City here = Hub.Here;
				if (here != null && Rep.RealmOf(here) == k)
				{
					instance.TownShown = here;
					instance.TownUntil = Mathf.Max(instance.TownUntil, unscaledTime + 5.5f);
				}
			}
			RepLevel repLevel = Rep.LevelOf(before);
			RepLevel repLevel2 = Rep.LevelOf(after);
			string place = "the realm of " + (Rep.NameOf(k) ?? "this realm");
			if (repLevel2 != repLevel)
			{
				_realmLevelAt = unscaledTime;
				if (repLevel2 > repLevel)
				{
					Sounds.PlayUI("event:/SFX/UI/YesPoint");
				}
				Notice("realm:" + num, LevelLine(repLevel, repLevel2, place));
			}
			else if (alone && Mathf.Abs(after - before) >= 5)
			{
				Notice(null, string.Format("{0}{1} {2}", (after > before) ? "+" : "", after - before, Rep.NameOf(k)) + (string.IsNullOrEmpty(reason) ? "" : (" · " + reason)));
			}
		}

		private static string LevelLine(RepLevel was, RepLevel now, string place)
		{
			if (now > was && now <= RepLevel.Stranger)
			{
				return "No longer " + ((was == RepLevel.Outlaw) ? "an outlaw" : "distrusted") + " in " + place + ".";
			}
			if (now <= was)
			{
				return Rep.Name(now) + " in " + place + " now.";
			}
			return Rep.Name(now) + " in " + place + ".";
		}

		private static void Notice(string place, string line)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance == null || string.IsNullOrEmpty(line))
			{
				return;
			}
			if ((!(Time.unscaledTime < instance.ToastUntil) || (!(instance.ToastText != _ours) && !_oursLevel)) && Waiting.Count == 0)
			{
				Show(instance, line, place != null);
			}
			else if (place != null)
			{
				Waiting.RemoveAll((KeyValuePair<string, string> w) => w.Key == place);
				if (Waiting.Count >= 4)
				{
					Waiting.RemoveAt(0);
				}
				Waiting.Add(new KeyValuePair<string, string>(place, line));
			}
		}

		internal static void Tick()
		{
			if (Waiting.Count != 0)
			{
				WorldBoxMod instance = WorldBoxMod.Instance;
				if (!(instance == null) && !(Time.unscaledTime < instance.ToastUntil))
				{
					string value = Waiting[0].Value;
					Waiting.RemoveAt(0);
					Show(instance, value, level: true);
				}
			}
		}

		private static void Show(WorldBoxMod mod, string line, bool level)
		{
			mod.Toast(line);
			_ours = mod.ToastText;
			_oursLevel = level;
		}

		internal static void Reset()
		{
			Waiting.Clear();
			_ours = null;
			_oursLevel = false;
		}

		public static float BannerExtra(City c)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance == null || instance.Host == null || c == null)
			{
				return 0f;
			}
			return Mathf.Round(((Rep.RealmOf(c) != null) ? 18f : 9f) * GameArt.P);
		}

		public static void DrawInBanner(Rect r, City c, float alpha)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			Actor actor = ((instance != null) ? instance.Host : null);
			if (actor == null || c == null || r.height <= 0f)
			{
				return;
			}
			float p = GameArt.P;
			float unscaledTime = Time.unscaledTime;
			Kingdom kingdom = Rep.RealmOf(c);
			float num = ((kingdom != null) ? Mathf.Round(r.height * 0.5f) : r.height);
			float tagW = Mathf.Round(GameArt.Measure("Realm", 3.6f).x + 3f * p);
			float levelW = Mathf.Round(Mathf.Max(GameArt.Measure("Distrusted", 4.2f).x + 4f * p, 34f * p));
			int points = Rep.Points(actor, c);
			if (_city == Rep.IdOf(c) && unscaledTime - _at < 0.8f)
			{
				points = Mathf.RoundToInt(Mathf.Lerp(_from, _to, Ease((unscaledTime - _at) / 0.8f)));
			}
			Row(new Rect(r.x, r.y, r.width, num), "Town", points, Rep.Rules(actor, c), _city == Rep.IdOf(c) && unscaledTime - _levelAt < 1.6f, alpha, tagW, levelW);
			if (kingdom != null)
			{
				int points2 = Rep.StandingPoints(actor, kingdom);
				if (_realm == Rep.IdOf(kingdom) && unscaledTime - _realmAt < 0.8f)
				{
					points2 = Mathf.RoundToInt(Mathf.Lerp(_realmFrom, _realmTo, Ease((unscaledTime - _realmAt) / 0.8f)));
				}
				Row(new Rect(r.x, r.y + num, r.width, r.height - num), "Realm", points2, Rep.Reigns(actor, kingdom), _realm == Rep.IdOf(kingdom) && unscaledTime - _realmLevelAt < 1.6f, alpha, tagW, levelW);
			}
		}

		private static void Row(Rect r, string tag, int points, bool yours, bool flash, float alpha, float tagW, float levelW)
		{
			float p = GameArt.P;
			float unscaledTime = Time.unscaledTime;
			RepLevel repLevel = Rep.LevelOf(points);
			string text = (yours ? "Yours" : Rep.Name(repLevel));
			float frac = (yours ? 1f : Rep.Progress(points));
			Color grey = GameArt.Grey;
			grey.a = alpha;
			GameArt.Label(new Rect(r.x, r.y, tagW, r.height), tag, 3.6f, TextAnchor.MiddleLeft, grey);
			Color color = (yours ? Gold : LevelColor(repLevel));
			if (flash)
			{
				color = Color.Lerp(Color.white, color, 0.5f + 0.5f * Mathf.Sin(unscaledTime * 12f));
			}
			color.a = alpha;
			GameArt.Label(new Rect(r.x + tagW, r.y, levelW, r.height), text, 4.2f, TextAnchor.MiddleLeft, color);
			float num = r.x + tagW + levelW;
			GameArt.Bar(new Rect(num, Mathf.Round(r.y + r.height * 0.5f - 2.5f * p), Mathf.Max(8f * p, r.xMax - num), Mathf.Round(5f * p)), frac, yours ? Gold : BarColor(repLevel), 0f, null, alpha);
		}

		public static Color LevelColor(RepLevel l)
		{
			switch (l)
			{
			case RepLevel.Outlaw:
				return OutlawColor;
			case RepLevel.Distrusted:
				return DistrustedColor;
			case RepLevel.Trusted:
				return new Color(0.62f, 0.86f, 0.52f);
			case RepLevel.Honored:
				return new Color(0.55f, 0.78f, 1f);
			case RepLevel.Champion:
				return Gold;
			default:
				return GameArt.Grey;
			}
		}

		public static Color BarColor(RepLevel l)
		{
			if (l != RepLevel.Champion)
			{
				if (l >= RepLevel.Stranger)
				{
					return GameArt.AgeYellow;
				}
				return LevelColor(l);
			}
			return Gold;
		}

		private static float Ease(float x)
		{
			return 1f - (1f - Mathf.Clamp01(x)) * (1f - Mathf.Clamp01(x));
		}

		public static List<string> Titles(Actor body)
		{
			List<string> list = new List<string>();
			if (body == null)
			{
				return list;
			}
			long id;
			try
			{
				id = body.data.id;
			}
			catch
			{
				return list;
			}
			try
			{
				if (body.isKing() && body.kingdom != null)
				{
					list.Add((Models.IsFemale(body) ? "Queen of " : "King of ") + body.kingdom.name);
				}
				else if (body.isCityLeader() && body.city != null)
				{
					list.Add("Leader of " + body.city.name);
				}
			}
			catch
			{
			}
			try
			{
				foreach (Kingdom item in World.world.kingdoms.list)
				{
					if (item == null || !item.isAlive())
					{
						continue;
					}
					item.data.get("fp_champion", out var pResult, null);
					if (pResult == id.ToString())
					{
						list.Add("Champion of " + item.name);
					}
					item.data.get("fp_lords", out var pResult2, null);
					if (string.IsNullOrEmpty(pResult2))
					{
						continue;
					}
					string[] array = pResult2.Split('|');
					foreach (string text in array)
					{
						int num = text.IndexOf(':');
						if (num > 0 && !(text.Substring(num + 1) != id.ToString()) && long.TryParse(text.Substring(0, num), out var result))
						{
							City city = Quests.CityById(result);
							if (city != null)
							{
								list.Add("Lord of " + city.name);
							}
						}
					}
				}
			}
			catch
			{
			}
			return list;
		}
	}
}
