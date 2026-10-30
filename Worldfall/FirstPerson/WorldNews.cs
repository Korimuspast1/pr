using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using db;

namespace FirstPerson
{
	internal static class WorldNews
	{
		private sealed class Held
		{
			public City City;

			public long Id;

			public string Name;

			public Vector2 At;
		}

		public sealed class Siege
		{
			public City City;

			public long Id;

			public string Name;

			public Kingdom By;

			public float Progress;

			public bool Capital;

			public bool Home;

			public Vector2 At;

			public float Since;

			public float LastSeen;
		}

		public const KeyCode Key = KeyCode.M;

		private const int Most = 250;

		private const int NewHistoryIlBytes = 29;

		public static readonly List<NewsItem> Items = new List<NewsItem>();

		public static bool Open;

		public static Vector2 Scroll;

		public static int Filter;

		public static readonly string[] Filters = new string[4] { "All", "Your realm", "Near you", "Wars" };

		public const float NearTiles = 70f;

		private static int _seq;

		private static int _seenSeq;

		private static bool _seeded;

		private static object _world;

		private static int _errors;

		private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);

		private static int _renamed;

		public static int Unread;

		public static int UnreadMine;

		public static NewsItem Banner;

		public static float BannerSince;

		public static float BannerUntil;

		private static readonly List<NewsItem> Alerts = new List<NewsItem>();

		public const float BannerSeconds = 6f;

		public const float BannerGap = 50f;

		private const float SameAgain = 180f;

		private const float Stale = 90f;

		private const int MostWaiting = 2;

		private static float _lastBanner = -1000f;

		private static readonly Dictionary<string, KeyValuePair<float, int>> _told = new Dictionary<string, KeyValuePair<float, int>>();

		private static int _mode = 1;

		private static bool _inFirstPerson;

		private static int _sound;

		public static readonly List<Siege> Sieges = new List<Siege>();

		private static readonly Dictionary<long, float> _warned = new Dictionary<long, float>();

		private static readonly Dictionary<long, float> _warnedLate = new Dictionary<long, float>();

		private const float RetellSiege = 120f;

		private static Actor _host;

		private static long _hostId = -1L;

		private static long _kId = -1L;

		private static long _capitalId = -1L;

		private static long _homeId = -1L;

		private static string _kName;

		private static Kingdom _kingdom;

		private static long _formerKId = -1L;

		private static string _formerName;

		private static float _formerUntil;

		private static readonly Dictionary<string, float> _warNames = new Dictionary<string, float>();

		private static readonly Dictionary<long, Held> _held = new Dictionary<long, Held>();

		private static readonly Dictionary<long, long> _owner = new Dictionary<long, long>();

		private static readonly Dictionary<long, string> _ownerName = new Dictionary<long, string>();

		private static float _tick;

		private static float _lastSeenFp = -100f;

		private static double _lookedAt = -1.0;

		private static readonly List<Siege> _pinned = new List<Siege>();

		public static bool Installed { get; private set; }

		private static bool InFirstPerson
		{
			get
			{
				if (_inFirstPerson)
				{
					return Time.unscaledTime - _lastSeenFp < 1f;
				}
				return false;
			}
		}

		public static Kingdom Realm => _kingdom;

		internal static int PendingAlerts => Alerts.Count + ((Banner != null) ? 1 : 0);

		internal static int WaitingAlerts => Alerts.Count;

		internal static string PendingForTest => ((Banner != null) ? ("[" + Tags.Replace(Banner.Text, "") + "] ") : "") + string.Join(" | ", Alerts.ConvertAll((NewsItem a) => Tags.Replace(a.Text, "")).ToArray());

		public static void Install()
		{
			if (!Installed)
			{
				MethodInfo method = typeof(HistoryHud).GetMethod("newHistory", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 29)
				{
					throw new InvalidOperationException("the game's history log has changed (" + num + " bytes of IL, not " + 29 + ")");
				}
				Detour.Apply(method, typeof(WorldNews).GetMethod("NewHistory", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				Installed = true;
			}
		}

		private static void NewHistory(HistoryHud self, WorldLogMessage message)
		{
			try
			{
				Heard(message);
			}
			catch (Exception ex)
			{
				if (_errors++ < 5)
				{
					Log.Error("news: " + ex.Message);
				}
			}
			if (self.checkEnabled())
			{
				self.newText(message);
				self.gameObject.SetActive(value: true);
			}
		}

		private static void Heard(WorldLogMessage m)
		{
			if (m == null)
			{
				return;
			}
			CheckWorld();
			NewsItem newsItem = FromLog(m);
			if (newsItem != null)
			{
				newsItem.Seq = ++_seq;
				newsItem.Read = !InFirstPerson;
				bool flag = (newsItem.Kind == "diplomacy_war_started" || newsItem.Kind == "total_war_started") && Names(Tags.Replace(m.special1 ?? "", "").Trim(), _kName);
				if (flag)
				{
					newsItem.Tone = NewsTone.Warn;
				}
				Add(newsItem);
				if (newsItem.Mine && !flag && Important(newsItem.Kind, _mode))
				{
					newsItem.About = newsItem.Kind + "|" + Tags.Replace(m.special1 ?? "", "").Trim() + "|" + Tags.Replace(m.special2 ?? "", "").Trim();
					Alert(newsItem);
				}
			}
		}

		internal static string Format(WorldLogMessage m)
		{
			try
			{
				WorldLogAsset asset = m.getAsset();
				if (asset == null)
				{
					return null;
				}
				string pText = LocalizedTextManager.getText((asset.random_ids > 0) ? asset.getLocaleID(m.timestamp % asset.random_ids + 1) : asset.getLocaleID());
				if (asset.text_replacer != null)
				{
					asset.text_replacer(m, ref pText);
				}
				return Renames.Apply(pText);
			}
			catch
			{
				return null;
			}
		}

		private static NewsItem FromLog(WorldLogMessage m)
		{
			string text = Format(m);
			if (string.IsNullOrEmpty(text))
			{
				return null;
			}
			NewsItem newsItem = new NewsItem
			{
				Kind = (m.asset_id ?? ""),
				Text = text.Trim(),
				Time = m.timestamp
			};
			try
			{
				WorldLogAsset asset = m.getAsset();
				if (asset != null)
				{
					newsItem.Group = asset.group;
					newsItem.Icon = asset.path_icon;
				}
			}
			catch
			{
			}
			try
			{
				if (m.hasLocation())
				{
					Vector3 location = m.getLocation();
					if (location.x != 0f || location.y != 0f)
					{
						newsItem.HasPlace = true;
						newsItem.At = new Vector2(location.x, location.y);
					}
				}
			}
			catch
			{
			}
			try
			{
				Actor unit = m.unit;
				if (unit != null && unit.isAlive())
				{
					newsItem.Unit = unit;
				}
			}
			catch
			{
			}
			newsItem.Key = newsItem.Kind + "|" + m.timestamp + "|" + m.special1 + "|" + m.special2 + "|" + m.special3;
			newsItem.Mine = AboutRealm(m);
			newsItem.Tone = (newsItem.Mine ? ToneOf(newsItem.Kind) : NewsTone.World);
			return newsItem;
		}

		private static void Add(NewsItem n)
		{
			Items.Add(n);
			if (Items.Count > 250)
			{
				Items.RemoveRange(0, Items.Count - 250);
			}
		}

		private static bool Important(string kind, int mode)
		{
			switch (kind)
			{
			case "total_war_started":
			case "log_city_revolted":
			case "city_destroyed":
			case "diplomacy_war_started":
				return mode >= 1;
			case "kingdom_destroyed":
			case "kingdom_fractured":
			case "kingdom_shattered":
			case "king_fled_capital":
			case "king_fled_city":
			case "king_dead":
			case "king_left":
			case "king_new":
			case "king_killed":
			case "diplomacy_war_ended":
			case "alliance_new":
			case "alliance_dissolved":
				return mode >= 2;
			default:
				return false;
			}
		}

		private static NewsTone ToneOf(string kind)
		{
			switch (kind)
			{
			case "city_new":
			case "king_new":
			case "diplomacy_war_ended":
			case "alliance_new":
			case "kingdom_royal_clan_new":
			case "kingdom_new":
				return NewsTone.Good;
			case "favorite_dead":
			case "favorite_killed":
			case "kingdom_royal_clan_changed":
				return NewsTone.World;
			default:
				return NewsTone.Bad;
			}
		}

		public static void SeedFromSave()
		{
			CheckWorld();
			if (_seeded && Installed)
			{
				return;
			}
			_seeded = true;
			HashSet<string> hashSet = new HashSet<string>();
			foreach (NewsItem item in Items)
			{
				if (item.Key != null)
				{
					hashSet.Add(item.Key);
				}
			}
			int num = 0;
			try
			{
				using (ListPool<WorldLogMessage> listPool = DBGetter.getWorldLogMessages())
				{
					foreach (ref WorldLogMessage item2 in listPool)
					{
						WorldLogMessage current2 = item2;
						if (num < 150)
						{
							NewsItem newsItem = FromLog(current2);
							if (newsItem != null && hashSet.Add(newsItem.Key))
							{
								newsItem.Seq = 0;
								Items.Add(newsItem);
								num++;
							}
							continue;
						}
						break;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("news: reading the world log: " + ex.Message);
			}
			Items.Sort((NewsItem a, NewsItem b) => (a.Time == b.Time) ? a.Seq.CompareTo(b.Seq) : a.Time.CompareTo(b.Time));
			if (Items.Count > 250)
			{
				Items.RemoveRange(0, Items.Count - 250);
			}
			Log.Info("news: " + num + " lines read from the save, " + Items.Count + " kept");
		}

		private static void Rename()
		{
			int version = Renames.Version;
			if (version == _renamed)
			{
				return;
			}
			_renamed = version;
			foreach (NewsItem item in Items)
			{
				item.Text = Renames.Apply(item.Text);
			}
			foreach (NewsItem alert in Alerts)
			{
				alert.Text = Renames.Apply(alert.Text);
			}
			if (Banner != null)
			{
				Banner.Text = Renames.Apply(Banner.Text);
			}
			foreach (Siege siege in Sieges)
			{
				siege.Name = Renames.Apply(siege.Name);
			}
		}

		private static void CheckWorld()
		{
			object obj = null;
			try
			{
				obj = World.world.map_stats;
			}
			catch
			{
			}
			if (obj != _world)
			{
				_world = obj;
				Items.Clear();
				Alerts.Clear();
				_told.Clear();
				_lastBanner = -1000f;
				Sieges.Clear();
				_warned.Clear();
				_warnedLate.Clear();
				_warNames.Clear();
				Banner = null;
				_seeded = false;
				_seenSeq = _seq;
				_host = null;
			}
		}

		private static void CountUnread()
		{
			int num = 0;
			int num2 = 0;
			for (int num3 = Items.Count - 1; num3 >= 0; num3--)
			{
				NewsItem newsItem = Items[num3];
				if (newsItem.Seq > _seenSeq && !newsItem.Read)
				{
					num++;
					if (newsItem.Mine)
					{
						num2++;
					}
				}
			}
			Unread = num;
			UnreadMine = num2;
		}

		public static bool IsNew(NewsItem n)
		{
			if (n.Seq > _seenSeq)
			{
				return !n.Read;
			}
			return false;
		}

		public static void Toggle()
		{
			Open = !Open;
			if (Open)
			{
				OnOpen();
			}
			else
			{
				MarkRead();
			}
		}

		public static void OnOpen()
		{
			SeedFromSave();
			Scroll = Vector2.zero;
			Banner = null;
			Alerts.Clear();
		}

		public static void MarkRead()
		{
			_seenSeq = _seq;
			Unread = (UnreadMine = 0);
		}

		public static List<NewsItem> Shown(Vector2 you, bool hasYou)
		{
			Rename();
			List<NewsItem> list = new List<NewsItem>();
			for (int num = Items.Count - 1; num >= 0; num--)
			{
				NewsItem newsItem = Items[num];
				switch (Filter)
				{
				case 1:
					if (!newsItem.Mine)
					{
						continue;
					}
					break;
				case 2:
				{
					if (!hasYou || !newsItem.Where(out var at) || (at - you).sqrMagnitude > 4900f)
					{
						continue;
					}
					break;
				}
				case 3:
					if (!newsItem.War)
					{
						continue;
					}
					break;
				}
				list.Add(newsItem);
			}
			return list;
		}

		private static string AboutOf(NewsItem n)
		{
			return n.About ?? n.Kind;
		}

		private static void Alert(NewsItem n)
		{
			if (_mode <= 0 || !InFirstPerson || Open)
			{
				return;
			}
			float num = (n.Heard = Time.unscaledTime);
			string about = AboutOf(n);
			int num2 = Rank(n);
			if (Banner != null && num < BannerUntil && AboutOf(Banner) == about)
			{
				if (num2 > Rank(Banner))
				{
					Banner = n;
					BannerUntil = Mathf.Max(BannerUntil, num + 6f);
					_told[about] = new KeyValuePair<float, int>(num, num2);
					_sound = SoundOf(n);
				}
				return;
			}
			int num3 = Alerts.FindIndex((NewsItem q) => AboutOf(q) == about);
			if (num3 >= 0)
			{
				if (num2 >= Rank(Alerts[num3]))
				{
					Alerts[num3] = n;
				}
			}
			else
			{
				if (_told.TryGetValue(about, out var value) && num - value.Key < 180f && num2 <= value.Value)
				{
					return;
				}
				Alerts.Add(n);
				while (Alerts.Count > 2)
				{
					int index = 0;
					for (int num4 = 1; num4 < Alerts.Count; num4++)
					{
						if (Rank(Alerts[num4]) < Rank(Alerts[index]))
						{
							index = num4;
						}
					}
					Alerts.RemoveAt(index);
				}
			}
		}

		private static int Rank(NewsItem n)
		{
			string kind = n.Kind;
			if (kind != null)
			{
				int length = kind.Length;
				if (length <= 14)
				{
					if (length != 8)
					{
						if (length == 14 && kind == "city_destroyed")
						{
							goto IL_00d5;
						}
					}
					else
					{
						char c = kind[3];
						if (c != 's')
						{
							if (c == 't' && kind == "fp_taken")
							{
								goto IL_00d5;
							}
						}
						else if (kind == "fp_siege")
						{
							if (!n.Late)
							{
								return 2;
							}
							return 3;
						}
					}
				}
				else if (length != 17)
				{
					if (length == 21 && kind == "diplomacy_war_started")
					{
						goto IL_00e3;
					}
				}
				else
				{
					char c = kind[0];
					if (c != 'k')
					{
						if (c != 'l')
						{
							if (c == 't' && kind == "total_war_started")
							{
								goto IL_00e3;
							}
						}
						else if (kind == "log_city_revolted")
						{
							goto IL_00d5;
						}
					}
					else if (kind == "kingdom_destroyed")
					{
						goto IL_00d5;
					}
				}
			}
			if (n.Tone != NewsTone.Bad && n.Tone != NewsTone.Warn)
			{
				return 0;
			}
			return 1;
			IL_00e3:
			return 2;
			IL_00d5:
			return 4;
		}

		private static int SoundOf(NewsItem n)
		{
			if (n.Tone != NewsTone.Bad && n.Tone != NewsTone.Warn)
			{
				return 1;
			}
			return 2;
		}

		private static bool SiegeOn(string about)
		{
			foreach (Siege siege in Sieges)
			{
				if ("city|" + siege.Id == about)
				{
					return true;
				}
			}
			return false;
		}

		private static void UpdateBanner(bool menu, float dt)
		{
			float unscaledTime = Time.unscaledTime;
			if (menu)
			{
				if (Banner != null)
				{
					BannerUntil += dt;
					BannerSince = Mathf.Min(BannerSince + dt, unscaledTime);
				}
			}
			else
			{
				if (Banner != null && unscaledTime < BannerUntil)
				{
					return;
				}
				Banner = null;
				if (Alerts.Count == 0)
				{
					return;
				}
				for (int num = Alerts.Count - 1; num >= 0; num--)
				{
					if (unscaledTime - Alerts[num].Heard > 90f || (Alerts[num].Kind == "fp_siege" && !SiegeOn(Alerts[num].About)))
					{
						Alerts.RemoveAt(num);
					}
				}
				if (Alerts.Count == 0 || unscaledTime - _lastBanner < 50f)
				{
					return;
				}
				int index = 0;
				for (int i = 1; i < Alerts.Count; i++)
				{
					if (Rank(Alerts[i]) > Rank(Alerts[index]))
					{
						index = i;
					}
				}
				Banner = Alerts[index];
				Alerts.RemoveAt(index);
				BannerSince = unscaledTime;
				BannerUntil = unscaledTime + 6f;
				_lastBanner = unscaledTime;
				_told[AboutOf(Banner)] = new KeyValuePair<float, int>(unscaledTime, Rank(Banner));
				_sound = SoundOf(Banner);
			}
		}

		public static int Update(Actor host, float dt, int mode, bool menu)
		{
			_mode = mode;
			if (Time.unscaledTime - _lastSeenFp > 5f)
			{
				Banner = null;
				Alerts.Clear();
			}
			_inFirstPerson = true;
			_lastSeenFp = Time.unscaledTime;
			CheckWorld();
			Rename();
			if (_host == null || (host != _host && IdOf(RealmOf(host)) != _kId))
			{
				TakeStock(host);
				return 0;
			}
			if (host != _host)
			{
				_host = host;
				try
				{
					_hostId = host.data.id;
				}
				catch
				{
					_hostId = -1L;
				}
			}
			_tick -= dt;
			if (_tick <= 0f)
			{
				_tick = 0.5f;
				try
				{
					Watch(host);
				}
				catch (Exception ex)
				{
					if (_errors++ < 5)
					{
						Log.Error("news: watching your realm: " + ex.Message);
					}
				}
				CountUnread();
			}
			if (mode <= 0)
			{
				Banner = null;
				Alerts.Clear();
			}
			UpdateBanner(menu, dt);
			int sound = _sound;
			_sound = 0;
			if (mode <= 0)
			{
				return 0;
			}
			return sound;
		}

		public static void Away()
		{
			_inFirstPerson = false;
			Open = false;
		}

		private static long IdOf(City c)
		{
			try
			{
				return c?.data.id ?? (-1);
			}
			catch
			{
				return -1L;
			}
		}

		private static long IdOf(Kingdom k)
		{
			try
			{
				return k?.data.id ?? (-1);
			}
			catch
			{
				return -1L;
			}
		}

		private static bool Alive(City c)
		{
			try
			{
				return c?.isAlive() ?? false;
			}
			catch
			{
				return false;
			}
		}

		private static Kingdom RealmOf(Actor host)
		{
			try
			{
				Kingdom kingdom = ((host != null && host.isAlive()) ? host.kingdom : null);
				return (kingdom != null && kingdom.isAlive() && kingdom.isCiv()) ? kingdom : null;
			}
			catch
			{
				return null;
			}
		}

		private static Vector2 PlaceOf(City c)
		{
			try
			{
				return (c.city_center != Vector2.zero) ? c.city_center : c.last_city_center;
			}
			catch
			{
				return Vector2.zero;
			}
		}

		private static void TakeStock(Actor host)
		{
			_host = host;
			try
			{
				_hostId = host?.data.id ?? (-1);
			}
			catch
			{
				_hostId = -1L;
			}
			Sieges.Clear();
			_owner.Clear();
			_ownerName.Clear();
			try
			{
				foreach (City item in World.world.cities.list)
				{
					if (Alive(item))
					{
						Kingdom kingdom = item.kingdom;
						_owner[IdOf(item)] = IdOf(kingdom);
						_ownerName[IdOf(item)] = kingdom?.name;
					}
				}
			}
			catch
			{
			}
			Rebase(host);
			_formerKId = -1L;
			_formerName = null;
			_warNames.Clear();
			_tick = 0.5f;
			try
			{
				_lookedAt = World.world.getCurWorldTime();
			}
			catch
			{
			}
		}

		private static void Rebase(Actor host)
		{
			_kingdom = RealmOf(host);
			_kId = IdOf(_kingdom);
			_kName = ((_kingdom != null) ? Royal.NameOf(_kingdom) : null);
			_held.Clear();
			_capitalId = -1L;
			_homeId = -1L;
			if (_kingdom == null)
			{
				return;
			}
			try
			{
				foreach (City city in _kingdom.getCities())
				{
					if (Alive(city))
					{
						_held[IdOf(city)] = new Held
						{
							City = city,
							Id = IdOf(city),
							Name = city.name,
							At = PlaceOf(city)
						};
					}
				}
				if (Alive(_kingdom.capital))
				{
					_capitalId = IdOf(_kingdom.capital);
				}
			}
			catch
			{
			}
			try
			{
				if (Alive(host.city))
				{
					_homeId = IdOf(host.city);
				}
			}
			catch
			{
			}
		}

		private static bool Rebels(Kingdom k)
		{
			try
			{
				return k != null && _lookedAt >= 0.0 && k.data.created_time >= _lookedAt;
			}
			catch
			{
				return false;
			}
		}

		private static void Watch(Actor host)
		{
			double lookedAt = 0.0;
			try
			{
				lookedAt = World.world.getCurWorldTime();
			}
			catch
			{
			}
			try
			{
				WatchNow(host);
			}
			finally
			{
				_lookedAt = lookedAt;
			}
		}

		private static void WatchNow(Actor host)
		{
			Kingdom kingdom = RealmOf(host);
			long num = IdOf(kingdom);
			City city = null;
			try
			{
				city = (Alive(host.city) ? host.city : null);
			}
			catch
			{
			}
			bool flag = false;
			if (_kId >= 0)
			{
				foreach (Held item in new List<Held>(_held.Values))
				{
					if (!Alive(item.City) || IdOf(item.City) != item.Id)
					{
						continue;
					}
					Kingdom kingdom2;
					try
					{
						kingdom2 = item.City.kingdom;
					}
					catch
					{
						continue;
					}
					if (kingdom2 != null && IdOf(kingdom2) != _kId && !Rebels(kingdom2))
					{
						bool capital = item.Id == _capitalId;
						bool flag2 = item.Id == _homeId;
						if (flag2 && num == IdOf(kingdom2))
						{
							flag = true;
						}
						Lost(item, kingdom2, capital, flag2 && num == IdOf(kingdom2));
					}
				}
			}
			try
			{
				foreach (City item2 in World.world.cities.list)
				{
					if (!Alive(item2))
					{
						continue;
					}
					long key = IdOf(item2);
					Kingdom kingdom3 = item2.kingdom;
					long num2 = IdOf(kingdom3);
					if (_owner.TryGetValue(key, out var value) && value != num2 && value >= 0 && num2 >= 0)
					{
						_ownerName.TryGetValue(key, out var value2);
						if (!Rebels(kingdom3) && (!_held.ContainsKey(key) || value != _kId))
						{
							Taken(item2, kingdom3, value, value2, num2 == num && num >= 0);
						}
					}
					_owner[key] = num2;
					_ownerName[key] = kingdom3?.name;
				}
			}
			catch
			{
			}
			if (num != _kId)
			{
				_formerKId = _kId;
				_formerName = _kName;
				_formerUntil = Time.unscaledTime + 60f;
				if (num >= 0 && !flag)
				{
					Own("fp_joined", "ui/Icons/iconKingdom", "You now belong to " + Named(kingdom) + ".", NewsTone.World, PlaceOf(city), city != null, alert: false);
				}
				Rebase(host);
				Sieges.Clear();
				return;
			}
			if (kingdom == null)
			{
				Sieges.Clear();
				return;
			}
			long num3 = -1L;
			City c = null;
			try
			{
				c = kingdom.capital;
				if (Alive(c))
				{
					num3 = IdOf(c);
				}
			}
			catch
			{
			}
			if (num3 != _capitalId && num3 >= 0)
			{
				Own("fp_capital", "ui/Icons/iconKingdom", Named(c) + " is now the capital of " + Named(kingdom) + ".", NewsTone.World, PlaceOf(c), hasPlace: true, alert: false);
			}
			try
			{
				foreach (War war in kingdom.getWars())
				{
					if (war != null && !string.IsNullOrEmpty(war.name))
					{
						_warNames[war.name] = Time.unscaledTime;
					}
				}
			}
			catch
			{
			}
			float clock = Time.unscaledTime;
			foreach (City city2 in kingdom.getCities())
			{
				if (!Alive(city2))
				{
					continue;
				}
				Kingdom kingdom4 = null;
				float num4 = 0f;
				try
				{
					kingdom4 = city2.being_captured_by;
					num4 = city2.getCaptureTicks();
					if (kingdom4 == null && city2.isGettingCaptured())
					{
						kingdom4 = city2.getCapturingKingdom();
					}
				}
				catch
				{
				}
				if (kingdom4 == null || num4 <= 0f || IdOf(kingdom4) == num || LiveKingdom(kingdom4) == null)
				{
					continue;
				}
				long id = IdOf(city2);
				Siege siege = Sieges.Find((Siege q) => q.Id == id);
				if (siege == null)
				{
					siege = new Siege
					{
						City = city2,
						Id = id,
						Name = city2.name,
						Since = clock
					};
					Sieges.Add(siege);
				}
				siege.LastSeen = clock;
				siege.By = kingdom4;
				try
				{
					siege.Name = city2.name;
				}
				catch
				{
				}
				siege.Progress = Mathf.Clamp01(num4 / 100f);
				siege.Capital = id == num3;
				siege.Home = city != null && id == IdOf(city);
				siege.At = PlaceOf(city2);
				if (!_warned.TryGetValue(id, out var value3) || clock - value3 > 120f)
				{
					_warned[id] = clock;
					string text = (siege.Capital ? ("Your capital " + Named(city2)) : (siege.Home ? ("Your home " + Named(city2)) : Named(city2)));
					string text2 = AttackerName(kingdom4);
					if (siege.Since == clock)
					{
						Log.Info(string.Format("news: siege of {0}#{1} ({2}) by {3}#{4}, ticks {5:0}{6}", siege.Name, id, Royal.NameOf(kingdom), text2 ?? "unknown", IdOf(kingdom4), num4, siege.Capital ? ", your capital" : (siege.Home ? ", your home" : "")));
					}
					Own("fp_siege", "ui/Icons/iconWar", text + ((text2 != null) ? (" is under attack by " + Colored(text2, kingdom4) + "!") : " is under attack!"), NewsTone.Warn, siege.At, hasPlace: true, alert: true, "city|" + id);
				}
				if (siege.Progress >= 0.6f && (!_warnedLate.TryGetValue(id, out var value4) || clock - value4 > 120f))
				{
					_warnedLate[id] = clock;
					_warned[id] = clock;
					string text3 = AttackerName(kingdom4);
					Own("fp_siege", "ui/Icons/iconWar", Named(city2) + ((text3 != null) ? (" is about to fall to " + Colored(text3, kingdom4) + "!") : " is about to fall!"), NewsTone.Warn, siege.At, hasPlace: true, siege.Capital || siege.Home, "city|" + id, late: true);
				}
			}
			Sieges.RemoveAll((Siege q) => clock - q.LastSeen > 1.5f || !Alive(q.City));
			_held.Clear();
			foreach (City city3 in kingdom.getCities())
			{
				if (Alive(city3))
				{
					_held[IdOf(city3)] = new Held
					{
						City = city3,
						Id = IdOf(city3),
						Name = city3.name,
						At = PlaceOf(city3)
					};
				}
			}
			_capitalId = num3;
			_homeId = IdOf(city);
		}

		private static void Lost(Held h, Kingdom by, bool capital, bool wasHome)
		{
			string text = ((by != null) ? Named(by) : "another realm");
			string text2 = (capital ? ("Your capital " + Named(h.City) + " has fallen to " + text + "!") : (Named(h.City) + " was taken by " + text + "."));
			if (wasHome)
			{
				text2 += " Your town is theirs now, and so are you.";
			}
			Own("fp_taken", "ui/Icons/iconCitySelect", text2, NewsTone.Bad, h.At, hasPlace: true, alert: true, "city|" + h.Id);
			Sieges.RemoveAll((Siege q) => q.Id == h.Id);
		}

		private static void Taken(City c, Kingdom by, long fromId, string fromName, bool byYou)
		{
			string text = ((fromName != null) ? Colored(fromName, KingdomById(fromId)) : "another realm");
			if (byYou)
			{
				Own("fp_won", "ui/Icons/iconCitySelect", Named(by) + " took " + Named(c) + " from " + text + ".", NewsTone.Good, PlaceOf(c), hasPlace: true, alert: false);
			}
			else
			{
				NewsItem newsItem = Make("fp_taken_world", "ui/Icons/iconCitySelect", Named(by) + " took " + Named(c) + " from " + text + ".", NewsTone.World, PlaceOf(c), hasPlace: true);
				newsItem.Mine = fromId == _kId && _kId >= 0;
				newsItem.Group = "wars";
				Add(newsItem);
			}
		}

		private static Kingdom KingdomById(long id)
		{
			try
			{
				foreach (Kingdom item in World.world.kingdoms.list)
				{
					if (IdOf(item) == id)
					{
						return item;
					}
				}
			}
			catch
			{
			}
			return null;
		}

		private static NewsItem Make(string kind, string icon, string text, NewsTone tone, Vector2 at, bool hasPlace)
		{
			double time = 0.0;
			try
			{
				time = World.world.getCurWorldTime();
			}
			catch
			{
			}
			return new NewsItem
			{
				Seq = ++_seq,
				Kind = kind,
				Group = "realm",
				Icon = icon,
				Text = text,
				Time = time,
				Tone = tone,
				Mine = true,
				At = at,
				HasPlace = (hasPlace && at != Vector2.zero)
			};
		}

		internal static void Announce(string kind, string text, NewsTone tone, Vector2 at)
		{
			Own(kind, "ui/Icons/iconKings", text, tone, at, hasPlace: true, alert: true, kind);
		}

		private static void Own(string kind, string icon, string text, NewsTone tone, Vector2 at, bool hasPlace, bool alert, string about = null, bool late = false)
		{
			NewsItem newsItem = Make(kind, icon, text, tone, at, hasPlace);
			newsItem.About = about;
			newsItem.Late = late;
			Add(newsItem);
			Log.Info("news: " + Tags.Replace(text, ""));
			if (alert)
			{
				Alert(newsItem);
			}
		}

		private static bool AboutRealm(WorldLogMessage m)
		{
			bool flag = _formerKId >= 0 && Time.unscaledTime < _formerUntil;
			if (_kId < 0 && !flag)
			{
				return false;
			}
			try
			{
				long kingdom_id = m.kingdom_id;
				if (kingdom_id > 0 && (kingdom_id == _kId || (flag && kingdom_id == _formerKId)))
				{
					return true;
				}
			}
			catch
			{
			}
			try
			{
				if (_hostId >= 0 && m.unit_id == _hostId)
				{
					return true;
				}
			}
			catch
			{
			}
			string[] array = new string[3] { m.special1, m.special2, m.special3 };
			foreach (string text in array)
			{
				if (string.IsNullOrEmpty(text))
				{
					continue;
				}
				string text2 = Tags.Replace(text, "").Trim();
				if (Names(text2, _kName) || (flag && Names(text2, _formerName)))
				{
					return true;
				}
				if (_warNames.TryGetValue(text2, out var value) && Time.unscaledTime - value < 60f)
				{
					return true;
				}
				foreach (Held value2 in _held.Values)
				{
					if (text2 == value2.Name)
					{
						return true;
					}
				}
			}
			return false;
		}

		private static bool Names(string special, string realm)
		{
			if (string.IsNullOrEmpty(realm))
			{
				return false;
			}
			if (special == realm)
			{
				return true;
			}
			if (realm.Length < 3)
			{
				return false;
			}
			int num = special.IndexOf(realm, StringComparison.Ordinal);
			if (num >= 0 && (num == 0 || special[num - 1] == ' '))
			{
				if (num + realm.Length != special.Length)
				{
					return !char.IsLetter(special[num + realm.Length]);
				}
				return true;
			}
			return false;
		}

		private static string Named(Kingdom k)
		{
			if (LiveKingdom(k) != null)
			{
				return Colored(AttackerName(k) ?? "another realm", k);
			}
			return "another realm";
		}

		private static Kingdom LiveKingdom(Kingdom k)
		{
			try
			{
				return (k != null && k.isAlive() && k.data != null) ? k : null;
			}
			catch
			{
				return null;
			}
		}

		internal static string AttackerName(Kingdom k)
		{
			k = LiveKingdom(k);
			if (k == null)
			{
				return null;
			}
			try
			{
				if (!k.isCiv())
				{
					return "raiders";
				}
			}
			catch
			{
				return null;
			}
			string text = Royal.NameOf(k);
			if (!string.IsNullOrEmpty(text) && !(text == "?"))
			{
				return text;
			}
			return null;
		}

		private static string Named(City c)
		{
			if (c == null)
			{
				return "?";
			}
			string name;
			try
			{
				name = c.name;
			}
			catch
			{
				name = "?";
			}
			Kingdom k = null;
			try
			{
				k = c.kingdom;
			}
			catch
			{
			}
			return Colored(name, k);
		}

		private static string Colored(string name, Kingdom k)
		{
			if (string.IsNullOrEmpty(name))
			{
				return "?";
			}
			try
			{
				if (k != null && k.isCiv())
				{
					return "<color=#" + ColorUtility.ToHtmlStringRGB(k.getColor().getColorText()) + ">" + name + "</color>";
				}
			}
			catch
			{
			}
			return name;
		}

		public static void AddMarks(List<KeyValuePair<float, Color>> marks, Vector2 p)
		{
			if (!InFirstPerson)
			{
				return;
			}
			Color value = Color.Lerp(new Color(1f, 0.25f, 0.18f), Color.white, 0.75f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f)));
			foreach (Siege item in Worst(Sieges, _pinned, 3))
			{
				Vector2 vector = item.At - p;
				if (vector.sqrMagnitude > 9f)
				{
					marks.Add(new KeyValuePair<float, Color>(Mathf.Atan2(vector.y, vector.x), value));
				}
			}
		}

		internal static List<Siege> Worst(List<Siege> all, List<Siege> into, int most)
		{
			into.Clear();
			into.AddRange(all);
			into.Sort(delegate(Siege a, Siege b)
			{
				int num = (a.Capital ? 2 : 0) + (a.Home ? 1 : 0);
				int num2 = (b.Capital ? 2 : 0) + (b.Home ? 1 : 0);
				return (num == num2) ? b.Progress.CompareTo(a.Progress) : num2.CompareTo(num);
			});
			if (into.Count > most)
			{
				into.RemoveRange(most, into.Count - most);
			}
			return into;
		}

		public static string When(NewsItem n)
		{
			try
			{
				int year = Date.getYear(n.Time);
				int monthsSince = Date.getMonthsSince(n.Time);
				string text = ((monthsSince <= 0) ? "this month" : ((monthsSince < 12) ? (monthsSince + ((monthsSince == 1) ? " month ago" : " months ago")) : (monthsSince / 12 + ((monthsSince < 24) ? " year ago" : " years ago"))));
				return "Year " + year + ", " + text;
			}
			catch
			{
				return "";
			}
		}

		internal static void WatchNowForTest(Actor host)
		{
			_tick = 0f;
			_host = host;
			Watch(host);
			CountUnread();
		}

		internal static void TakeStockForTest(Actor host)
		{
			TakeStock(host);
		}

		internal static void GapOverForTest()
		{
			_lastBanner = -1000f;
		}

		internal static void BannerDoneForTest()
		{
			if (Banner != null)
			{
				BannerUntil = Time.unscaledTime;
			}
		}

		internal static NewsItem AlertForTest(string kind, string text, string about)
		{
			NewsItem newsItem = Make(kind, "ui/Icons/iconWar", text, NewsTone.Bad, Vector2.zero, hasPlace: false);
			newsItem.About = about;
			Add(newsItem);
			Alert(newsItem);
			return newsItem;
		}

		internal static bool PendingHas(string kind)
		{
			if (Banner == null || !(Banner.Kind == kind))
			{
				return Alerts.Exists((NewsItem a) => a.Kind == kind);
			}
			return true;
		}
	}
}
