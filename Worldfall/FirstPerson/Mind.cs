using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FirstPerson.Towns;

namespace FirstPerson
{
	internal static class Mind
	{
		internal sealed class Rec
		{
			public readonly List<ushort> Recent = new List<ushort>(24);

			public long Body;

			public int FirstDay = -1;

			public int LastDay = -1;

			public int Days;

			public string LastTopic;

			public int PrevDay = -1;

			public string PrevTopic;
		}

		public const string Key = "fp_mind";

		public const int Remembered = 24;

		private static readonly Dictionary<long, Rec> Cache = new Dictionary<long, Rec>();

		private static object _world;

		[ThreadStatic]
		private static StringBuilder _text;

		public static int Today
		{
			get
			{
				try
				{
					return FirstPerson.Towns.Calendar.Day;
				}
				catch
				{
					return 0;
				}
			}
		}

		private static void CheckWorld()
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
				Cache.Clear();
			}
		}

		public static Rec Of(Actor npc)
		{
			if (npc == null)
			{
				return null;
			}
			CheckWorld();
			long id;
			try
			{
				id = npc.data.id;
			}
			catch
			{
				return null;
			}
			if (Cache.TryGetValue(id, out var value))
			{
				return value;
			}
			value = new Rec();
			try
			{
				npc.data.get("fp_mind", out var pResult, null);
				if (!string.IsNullOrEmpty(pResult))
				{
					Parse(pResult, value);
				}
			}
			catch
			{
			}
			Cache[id] = value;
			return value;
		}

		public static Rec Talked(Actor npc, Actor you)
		{
			Rec rec = Of(npc);
			if (rec == null || you == null)
			{
				return rec;
			}
			long id = you.data.id;
			if (rec.Body != id)
			{
				rec.Body = id;
				rec.FirstDay = (rec.LastDay = -1);
				rec.Days = 0;
				rec.LastTopic = null;
			}
			int today = Today;
			rec.PrevDay = rec.LastDay;
			rec.PrevTopic = rec.LastTopic;
			if (rec.FirstDay < 0)
			{
				rec.FirstDay = today;
			}
			if (rec.LastDay != today)
			{
				rec.Days++;
				rec.LastDay = today;
			}
			Save(npc, rec);
			return rec;
		}

		public static void Heard(Rec r, ushort id)
		{
			if (r != null)
			{
				r.Recent.Remove(id);
				r.Recent.Add(id);
				while (r.Recent.Count > 24)
				{
					r.Recent.RemoveAt(0);
				}
			}
		}

		public static void Topic(Actor npc, string topic)
		{
			Rec rec = Of(npc);
			if (rec != null && !string.IsNullOrEmpty(topic))
			{
				rec.LastTopic = topic;
				Save(npc, rec);
			}
		}

		public static void Save(Actor npc, Rec r)
		{
			if (npc == null || r == null)
			{
				return;
			}
			try
			{
				npc.data.set("fp_mind", Write(r));
			}
			catch (Exception ex)
			{
				Log.Error("mind: could not write: " + ex.Message);
			}
		}

		private static string Write(Rec r)
		{
			if (_text == null)
			{
				_text = new StringBuilder(96);
			}
			StringBuilder text = _text;
			text.Length = 0;
			text.Append("m1|");
			foreach (ushort item in r.Recent)
			{
				text.Append(item.ToString("x4"));
			}
			text.Append('|').Append(r.Body).Append(',')
				.Append(r.FirstDay)
				.Append(',')
				.Append(r.LastDay)
				.Append(',')
				.Append(r.Days)
				.Append(',');
			if (r.LastTopic != null)
			{
				text.Append(r.LastTopic.Replace(",", "").Replace("|", ""));
			}
			return text.ToString();
		}

		private static void Parse(string s, Rec r)
		{
			string[] array = s.Split('|');
			if (array.Length < 3 || array[0] != "m1")
			{
				return;
			}
			string text = array[1];
			for (int i = 0; i + 4 <= text.Length; i += 4)
			{
				if (r.Recent.Count >= 24)
				{
					break;
				}
				if (ushort.TryParse(text.Substring(i, 4), NumberStyles.HexNumber, null, out var result))
				{
					r.Recent.Add(result);
				}
			}
			string[] array2 = array[2].Split(',');
			if (array2.Length >= 5)
			{
				long.TryParse(array2[0], out r.Body);
				if (!int.TryParse(array2[1], out r.FirstDay))
				{
					r.FirstDay = -1;
				}
				if (!int.TryParse(array2[2], out r.LastDay))
				{
					r.LastDay = -1;
				}
				int.TryParse(array2[3], out r.Days);
				r.LastTopic = ((array2[4].Length > 0) ? array2[4] : null);
			}
		}

		internal static void ForgetForTest()
		{
			Cache.Clear();
		}
	}
}
