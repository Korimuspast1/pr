using System;
using System.Collections.Generic;

namespace FirstPerson
{
	internal static class BagOrder
	{
		public const string Key = "fp_bagorder";

		private static long _for = -1L;

		private static string _raw;

		private static readonly List<string> _slots = new List<string>();

		private static readonly Dictionary<string, List<string>> _stackKeys = new Dictionary<string, List<string>>();

		private static readonly Dictionary<string, string> _resourceOf = new Dictionary<string, string>();

		public static List<string> Of(Actor a)
		{
			string pResult = null;
			long num = -1L;
			try
			{
				num = a.data.id;
				a.data.get("fp_bagorder", out pResult, null);
			}
			catch
			{
			}
			if (num == _for && pResult == _raw)
			{
				return _slots;
			}
			_for = num;
			_raw = pResult;
			_slots.Clear();
			if (!string.IsNullOrEmpty(pResult))
			{
				_slots.AddRange(pResult.Split('|'));
			}
			return _slots;
		}

		private static void Save(Actor a)
		{
			while (_slots.Count > 0 && _slots[_slots.Count - 1].Length == 0)
			{
				_slots.RemoveAt(_slots.Count - 1);
			}
			string text = string.Join("|", _slots.ToArray());
			try
			{
				if (text.Length == 0)
				{
					a.data.removeString("fp_bagorder");
				}
				else
				{
					a.data.set("fp_bagorder", text);
				}
				_raw = ((text.Length == 0) ? null : text);
			}
			catch (Exception ex)
			{
				Log.Error("bag order: save: " + ex.Message);
			}
		}

		public static string ResourceKey(string id)
		{
			return StackKey(id, 0);
		}

		public static string PieceKey(int index)
		{
			return "g:" + index;
		}

		public static string StackKey(string id, int n)
		{
			if (id == null)
			{
				id = "";
			}
			if (n < 0)
			{
				n = 0;
			}
			if (!_stackKeys.TryGetValue(id, out var value))
			{
				if (_stackKeys.Count > 512)
				{
					_stackKeys.Clear();
				}
				value = (_stackKeys[id] = new List<string>());
			}
			while (value.Count <= n)
			{
				value.Add((value.Count == 0) ? ("r:" + id) : ("r:" + id + "#" + (value.Count + 1)));
			}
			return value[n];
		}

		public static string ResourceOf(string key)
		{
			if (key == null || !key.StartsWith("r:", StringComparison.Ordinal))
			{
				return null;
			}
			if (_resourceOf.TryGetValue(key, out var value))
			{
				return value;
			}
			int num = key.IndexOf('#', 2);
			value = ((num < 0) ? key.Substring(2) : key.Substring(2, num - 2));
			if (_resourceOf.Count > 1024)
			{
				_resourceOf.Clear();
			}
			_resourceOf[key] = value;
			return value;
		}

		public static int StackOf(string key)
		{
			if (key == null || !key.StartsWith("r:", StringComparison.Ordinal))
			{
				return -1;
			}
			int num = key.IndexOf('#', 2);
			if (num < 0)
			{
				return 0;
			}
			int num2 = 0;
			if (num + 1 >= key.Length)
			{
				return -1;
			}
			for (int i = num + 1; i < key.Length; i++)
			{
				char c = key[i];
				if (c < '0' || c > '9' || num2 > 100000)
				{
					return -1;
				}
				num2 = num2 * 10 + (c - 48);
			}
			if (num2 < 2)
			{
				return -1;
			}
			return num2 - 1;
		}

		public static int StackAmount(string key, int total)
		{
			string text = ResourceOf(key);
			int num = StackOf(key);
			if (text == null || num < 0)
			{
				return 0;
			}
			int num2 = Bag.Stack(text);
			return Math.Max(0, Math.Min(num2, total - num * num2));
		}

		private static int Total(List<KeyValuePair<string, int>> bag, string res)
		{
			foreach (KeyValuePair<string, int> item in bag)
			{
				if (item.Key == res)
				{
					return item.Value;
				}
			}
			return 0;
		}

		public static int TakeFrom(Actor a, int cell, int count, List<KeyValuePair<string, int>> bag)
		{
			if (a == null || cell < 0 || count <= 0)
			{
				return -1;
			}
			List<string> list = Of(a);
			if (cell >= list.Count)
			{
				return -1;
			}
			string text = ResourceOf(list[cell]);
			if (text == null)
			{
				return -1;
			}
			int num = Total(bag, text);
			int num2 = Bag.Stack(text);
			int num3 = Math.Max(0, (num + num2 - 1) / num2 - 1);
			int num4 = list.IndexOf(StackKey(text, num3));
			if (num4 < 0 || num4 == cell)
			{
				return -1;
			}
			bool flag = count >= StackAmount(list[cell], num);
			bool flag2 = num % num2 == 0;
			if (!flag && !flag2)
			{
				return -1;
			}
			int num5 = StackOf(list[cell]);
			Swap(a, cell, num4);
			if (flag && !flag2 && num5 >= 0 && num5 < num3 - 1)
			{
				Swap(a, num4, list.IndexOf(StackKey(text, num3 - 1)));
			}
			return num4;
		}

		public static int PieceOf(string key)
		{
			if (key == null || !key.StartsWith("g:", StringComparison.Ordinal) || !int.TryParse(key.Substring(2), out var result))
			{
				return -1;
			}
			return result;
		}

		public static List<string> Fit(Actor a, List<KeyValuePair<string, int>> bag, int pieces, int cells)
		{
			List<string> list = Of(a);
			bool flag = Clean(a, list, bag, pieces);
			int num = -1;
			try
			{
				int num2 = Hotbar.Chosen(a);
				if (num2 < list.Count && list[num2].Length == 0)
				{
					num = num2;
				}
				else if (num2 >= list.Count)
				{
					num = num2;
				}
			}
			catch
			{
			}
			foreach (KeyValuePair<string, int> item in bag)
			{
				bool flag2 = false;
				try
				{
					flag2 = num >= 0 && Eating.Edible(a, item.Key);
				}
				catch
				{
				}
				int i = 0;
				for (int num3 = Bag.SlotsFor(item.Key, item.Value); i < num3; i++)
				{
					if (!list.Contains(StackKey(item.Key, i)))
					{
						Place(list, StackKey(item.Key, i), cells, flag2 ? num : (-1));
						flag = true;
					}
				}
			}
			for (int j = 0; j < pieces; j++)
			{
				if (!list.Contains(PieceKey(j)))
				{
					Place(list, PieceKey(j), cells);
					flag = true;
				}
			}
			for (int k = cells; k < list.Count; k++)
			{
				if (list[k].Length != 0)
				{
					bool flag3 = false;
					try
					{
						string text = ResourceOf(list[k]);
						flag3 = num >= 0 && text != null && Eating.Edible(a, text);
					}
					catch
					{
					}
					int num4 = FirstEmpty(list, cells, flag3 ? num : (-1));
					if (num4 < 0)
					{
						break;
					}
					list[num4] = list[k];
					list[k] = "";
					flag = true;
				}
			}
			if (flag)
			{
				Save(a);
			}
			return list;
		}

		public static void Tidy(Actor a, List<KeyValuePair<string, int>> bag, int pieces)
		{
			if (Clean(a, Of(a), bag, pieces))
			{
				Save(a);
			}
		}

		private static bool Clean(Actor a, List<string> s, List<KeyValuePair<string, int>> bag, int pieces)
		{
			bool result = false;
			for (int i = 0; i < s.Count; i++)
			{
				if (s[i].Length != 0)
				{
					bool flag = false;
					string text = ResourceOf(s[i]);
					if (text != null)
					{
						flag = StackOf(s[i]) >= 0 && StackOf(s[i]) < Bag.SlotsFor(text, Total(bag, text));
					}
					int num = PieceOf(s[i]);
					if (num >= 0)
					{
						flag = num < pieces;
					}
					if (s[i] == "h")
					{
						flag = Hotbar.Weapon(a) != null;
					}
					if (flag && s.IndexOf(s[i]) < i)
					{
						flag = false;
					}
					if (!flag)
					{
						s[i] = "";
						result = true;
					}
				}
			}
			return result;
		}

		public static void Set(Actor a, int i, string key)
		{
			if (i >= 0)
			{
				List<string> list = Of(a);
				while (list.Count <= i)
				{
					list.Add("");
				}
				list[i] = key ?? "";
				Save(a);
			}
		}

		private static void Place(List<string> s, string key, int cells, int skip = -1)
		{
			int num = FirstEmpty(s, cells, skip);
			if (num < 0)
			{
				num = FirstEmpty(s, int.MaxValue, skip);
			}
			while (num >= s.Count)
			{
				s.Add("");
			}
			if (num >= 0)
			{
				s[num] = key;
			}
			else
			{
				s.Add(key);
			}
		}

		private static int FirstEmpty(List<string> s, int within, int skip = -1)
		{
			for (int i = 0; i < s.Count && i < within; i++)
			{
				if (s[i].Length == 0 && i != skip)
				{
					return i;
				}
			}
			for (int j = s.Count; j < within; j++)
			{
				if (j != skip)
				{
					return j;
				}
			}
			return -1;
		}

		public static void Swap(Actor a, int i, int j)
		{
			if (i != j && i >= 0 && j >= 0)
			{
				List<string> list = Of(a);
				while (list.Count <= Math.Max(i, j))
				{
					list.Add("");
				}
				string value = list[i];
				list[i] = list[j];
				list[j] = value;
				Save(a);
			}
		}

		public static void PieceGone(Actor a, int index)
		{
			List<string> list = Of(a);
			for (int i = 0; i < list.Count; i++)
			{
				int num = PieceOf(list[i]);
				if (num >= 0 && num >= index)
				{
					list[i] = ((num == index) ? "" : PieceKey(num - 1));
				}
			}
			Save(a);
		}
	}
}
