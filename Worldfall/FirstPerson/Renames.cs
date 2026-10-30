using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FirstPerson
{
	internal static class Renames
	{
		private struct Found
		{
			public string Now;

			public int Standing;
		}

		private const float Every = 2f;

		private const int Shortest = 3;

		private const int Lesser = 1;

		private const int Greater = 2;

		private static readonly List<KeyValuePair<string, string>> _swaps = new List<KeyValuePair<string, string>>();

		private static readonly Dictionary<string, Found> _found = new Dictionary<string, Found>();

		private static readonly Dictionary<string, int> _current = new Dictionary<string, int>();

		private static float _lookedAt = -100f;

		private static int _version;

		private static int _hash;

		private static object _world;

		public static int Version
		{
			get
			{
				Look();
				return _version;
			}
		}

		public static int Count
		{
			get
			{
				Look();
				return _swaps.Count;
			}
		}

		public static string Apply(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return text;
			}
			Look();
			for (int i = 0; i < _swaps.Count; i++)
			{
				text = Swap(text, _swaps[i].Key, _swaps[i].Value);
			}
			return text;
		}

		private static string Swap(string text, string old, string now)
		{
			int num = text.IndexOf(old, StringComparison.Ordinal);
			if (num < 0)
			{
				return text;
			}
			StringBuilder stringBuilder = null;
			int num2 = 0;
			while (num >= 0)
			{
				int num3 = num + old.Length;
				bool flag = (num == 0 || !char.IsLetterOrDigit(text[num - 1])) && (num3 >= text.Length || !char.IsLetterOrDigit(text[num3]));
				if (flag && !InsideNew(text, num, old, now))
				{
					if (stringBuilder == null)
					{
						stringBuilder = new StringBuilder(text.Length + 16);
					}
					stringBuilder.Append(text, num2, num - num2).Append(now);
					num2 = num3;
				}
				num = text.IndexOf(old, flag ? num3 : (num + 1), StringComparison.Ordinal);
			}
			if (stringBuilder == null)
			{
				return text;
			}
			stringBuilder.Append(text, num2, text.Length - num2);
			return stringBuilder.ToString();
		}

		private static bool InsideNew(string text, int at, string old, string now)
		{
			for (int num = now.IndexOf(old, StringComparison.Ordinal); num >= 0; num = now.IndexOf(old, num + 1, StringComparison.Ordinal))
			{
				int num2 = at - num;
				if (num2 >= 0 && num2 + now.Length <= text.Length && string.CompareOrdinal(text, num2, now, 0, now.Length) == 0)
				{
					return true;
				}
			}
			return false;
		}

		private static void Look()
		{
			object obj = null;
			try
			{
				obj = ((World.world != null) ? World.world.map_stats : null);
			}
			catch
			{
			}
			if (obj != _world)
			{
				_world = obj;
				_lookedAt = -100f;
			}
			float unscaledTime = Time.unscaledTime;
			if (unscaledTime - _lookedAt < 2f)
			{
				return;
			}
			_lookedAt = unscaledTime;
			if (obj == null)
			{
				Set();
				return;
			}
			_found.Clear();
			_current.Clear();
			try
			{
				MapBox world = World.world;
				Scan(world.kingdoms.list, (Kingdom o) => o.data, 2);
				Scan(world.cities.list, (City o) => o.data, 2);
				Scan(world.alliances.list, (Alliance o) => o.data, 2);
				Scan(world.wars.list, (War o) => o.data, 2);
				Scan(world.clans.list, (Clan o) => o.data, 1);
				Scan(world.cultures.list, (Culture o) => o.data, 1);
				Scan(world.religions.list, (Religion o) => o.data, 1);
				Scan(world.languages.list, (Language o) => o.data, 1);
				Scan(world.families.list, (Family o) => o.data, 1);
				Scan(world.armies.list, (Army o) => o.data, 1);
				Scan(world.subspecies.list, (Subspecies o) => o.data, 1);
				List<Actor> simpleList = world.units.getSimpleList();
				for (int num = 0; num < simpleList.Count; num++)
				{
					Actor actor = simpleList[num];
					if (actor != null && actor.data != null)
					{
						Note(actor.data.name, actor.data.past_names, 1);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("renames: " + ex.Message);
			}
			Set();
		}

		private static void Scan<T>(List<T> all, Func<T, BaseSystemData> data, int standing) where T : class
		{
			if (all == null)
			{
				return;
			}
			for (int i = 0; i < all.Count; i++)
			{
				BaseSystemData baseSystemData;
				try
				{
					baseSystemData = ((all[i] != null) ? data(all[i]) : null);
				}
				catch
				{
					continue;
				}
				if (baseSystemData != null)
				{
					Note(baseSystemData.name, baseSystemData.past_names, standing);
				}
			}
		}

		private static void Note(string now, List<NameEntry> past, int standing)
		{
			if (string.IsNullOrEmpty(now))
			{
				return;
			}
			if (!_current.TryGetValue(now, out var value) || value < standing)
			{
				_current[now] = standing;
			}
			if (past == null)
			{
				return;
			}
			for (int i = 0; i < past.Count; i++)
			{
				string name = past[i].name;
				if (!string.IsNullOrEmpty(name) && name.Length >= 3 && !(name == now))
				{
					if (!_found.TryGetValue(name, out var value2) || value2.Standing < standing)
					{
						_found[name] = new Found
						{
							Now = now,
							Standing = standing
						};
					}
					else if (value2.Standing == standing && value2.Now != now)
					{
						_found[name] = new Found
						{
							Now = null,
							Standing = standing
						};
					}
				}
			}
		}

		private static void Set()
		{
			_swaps.Clear();
			int num = 17;
			List<string> list = new List<string>();
			List<string> list2 = new List<string>();
			foreach (KeyValuePair<string, int> item in _current)
			{
				if (item.Value >= 2)
				{
					list2.Add(item.Key);
				}
			}
			foreach (KeyValuePair<string, Found> kv in _found)
			{
				Found value = kv.Value;
				if (value.Now == null || (_current.TryGetValue(kv.Key, out var value2) && value2 >= value.Standing))
				{
					list.Add(kv.Key);
				}
				else if (value.Standing < 2 && list2.Exists((string g) => g != kv.Key && WordIn(g, kv.Key)))
				{
					list.Add(kv.Key);
				}
				else
				{
					_swaps.Add(new KeyValuePair<string, string>(kv.Key, value.Now));
				}
			}
			_swaps.Sort((KeyValuePair<string, string> a, KeyValuePair<string, string> b) => (a.Key.Length == b.Key.Length) ? string.CompareOrdinal(a.Key, b.Key) : b.Key.Length.CompareTo(a.Key.Length));
			foreach (KeyValuePair<string, string> swap in _swaps)
			{
				num = (num * 31 + swap.Key.GetHashCode()) * 31 + swap.Value.GetHashCode();
			}
			if (num != _hash)
			{
				_hash = num;
				_version++;
				if (_swaps.Count > 0 || list.Count > 0)
				{
					Log.Info("renames: " + _swaps.Count + " old names now read as the new (" + Sample() + ")" + ((list.Count > 0) ? ("; left alone, as something else has or had them: " + string.Join(", ", list.GetRange(0, Mathf.Min(4, list.Count)).ToArray()) + ((list.Count > 4) ? ", ..." : "")) : ""));
				}
			}
			_found.Clear();
			_current.Clear();
		}

		private static bool WordIn(string text, string word)
		{
			for (int num = text.IndexOf(word, StringComparison.Ordinal); num >= 0; num = text.IndexOf(word, num + 1, StringComparison.Ordinal))
			{
				bool num2 = num == 0 || !char.IsLetter(text[num - 1]);
				bool flag = num + word.Length == text.Length || !char.IsLetter(text[num + word.Length]);
				if (num2 && flag)
				{
					return true;
				}
			}
			return false;
		}

		private static string Sample()
		{
			List<string> list = new List<string>();
			for (int i = 0; i < _swaps.Count && i < 4; i++)
			{
				list.Add(_swaps[i].Key + " -> " + _swaps[i].Value);
			}
			return string.Join(", ", list.ToArray()) + ((_swaps.Count > 4) ? ", ..." : "");
		}
	}
}
