using System;
using System.Collections.Generic;
using System.Text;

namespace FirstPerson.Towns
{
	internal static class Items
	{
		public const string Key = "fp_items";

		private static readonly StringBuilder Text = new StringBuilder(128);

		public static int Count(Actor a, string id)
		{
			if (a == null || string.IsNullOrEmpty(id))
			{
				return 0;
			}
			if (!Read(a).TryGetValue(id, out var value))
			{
				return 0;
			}
			return value;
		}

		public static void Give(Actor a, string id, int n = 1)
		{
			if (a != null && !string.IsNullOrEmpty(id) && n > 0)
			{
				Dictionary<string, int> dictionary = Read(a);
				dictionary.TryGetValue(id, out var value);
				dictionary[id] = value + n;
				Write(a, dictionary);
			}
		}

		public static bool Take(Actor a, string id, int n = 1)
		{
			if (a == null || string.IsNullOrEmpty(id) || n <= 0)
			{
				return false;
			}
			Dictionary<string, int> dictionary = Read(a);
			if (!dictionary.TryGetValue(id, out var value) || value < n)
			{
				return false;
			}
			if (value == n)
			{
				dictionary.Remove(id);
			}
			else
			{
				dictionary[id] = value - n;
			}
			Write(a, dictionary);
			return true;
		}

		public static Dictionary<string, int> All(Actor a)
		{
			if (a == null)
			{
				return new Dictionary<string, int>();
			}
			return Read(a);
		}

		private static Dictionary<string, int> Read(Actor a)
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			string pResult = null;
			try
			{
				a.data.get("fp_items", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return dictionary;
			}
			string[] array = pResult.Split('|');
			foreach (string text in array)
			{
				int num = text.LastIndexOf(':');
				if (num > 0 && int.TryParse(text.Substring(num + 1), out var result) && result > 0)
				{
					string key = text.Substring(0, num);
					dictionary.TryGetValue(key, out var value);
					dictionary[key] = value + result;
				}
			}
			return dictionary;
		}

		private static void Write(Actor a, Dictionary<string, int> all)
		{
			Text.Length = 0;
			List<string> list = new List<string>(all.Keys);
			list.Sort(StringComparer.Ordinal);
			foreach (string item in list)
			{
				if (all[item] > 0)
				{
					if (Text.Length > 0)
					{
						Text.Append('|');
					}
					Text.Append(item).Append(':').Append(all[item]);
				}
			}
			try
			{
				if (Text.Length == 0)
				{
					a.data.removeString("fp_items");
				}
				else
				{
					a.data.set("fp_items", Text.ToString());
				}
			}
			catch (Exception ex)
			{
				Log.Error("items: could not save: " + ex.Message);
			}
		}
	}
}
