using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace FirstPerson
{
	internal static class UserWaypoints
	{
		public const string SaveKey = "fp_bookmarks";

		public sealed class Bookmark
		{
			public string Id;
			public float X;
			public float Y;
			public string Name;
			public string ColorHex;
		}

		private static readonly List<Bookmark> _marks = new List<Bookmark>();

		private static bool _loadedForThisWorld;

		private static object _worldToken;

		public static IReadOnlyList<Bookmark> All => _marks;

		public static int Count => _marks.Count;

		public static void EnsureLoaded()
		{
			object token = World.world?.map_stats;
			if (token != null && ReferenceEquals(token, _worldToken) && _loadedForThisWorld)
				return;
			_worldToken = token;
			_loadedForThisWorld = true;
			_marks.Clear();
			if (token == null)
				return;
			try
			{
				var md = World.world.map_stats.custom_data;
				if (md == null)
					return;
				md.get(SaveKey, out string json, null);
				if (string.IsNullOrEmpty(json))
					return;
				var list = JsonConvert.DeserializeObject<List<Bookmark>>(json);
				if (list != null)
					_marks.AddRange(list);
			}
			catch (Exception ex)
			{
				Log.Error("bookmarks: load failed: " + ex.Message);
			}
		}

		public static void Save()
		{
			try
			{
				var md = World.world?.map_stats?.custom_data;
				if (md == null)
					return;
				if (_marks.Count == 0)
				{
					md.removeString(SaveKey);
					return;
				}
				string json = JsonConvert.SerializeObject(_marks, Formatting.None);
				md.set(SaveKey, json);
			}
			catch (Exception ex)
			{
				Log.Error("bookmarks: save failed: " + ex.Message);
			}
		}

		public static Bookmark Add(Vector2 at, string name = null)
		{
			EnsureLoaded();
			string id = Guid.NewGuid().ToString("N").Substring(0, 8);
			var bm = new Bookmark
			{
				Id = id,
				X = at.x,
				Y = at.y,
				Name = string.IsNullOrEmpty(name) ? $"Mark { _marks.Count + 1}" : name,
				ColorHex = "#4ec9ff"
			};
			_marks.Add(bm);
			Save();
			return bm;
		}

		public static bool Remove(string id)
		{
			EnsureLoaded();
			int idx = _marks.FindIndex(m => m.Id == id);
			if (idx < 0) return false;
			_marks.RemoveAt(idx);
			Save();
			return true;
		}

		public static bool RemoveNearest(Vector2 from, float maxDist = 8f)
		{
			EnsureLoaded();
			if (_marks.Count == 0) return false;
			int best = -1;
			float bestD2 = maxDist * maxDist;
			for (int i = 0; i < _marks.Count; i++)
			{
				Vector2 p = new Vector2(_marks[i].X, _marks[i].Y);
				float d2 = (p - from).sqrMagnitude;
				if (d2 < bestD2)
				{
					bestD2 = d2;
					best = i;
				}
			}
			if (best < 0) return false;
			_marks.RemoveAt(best);
			Save();
			return true;
		}

		public static void Clear()
		{
			EnsureLoaded();
			if (_marks.Count == 0) return;
			_marks.Clear();
			Save();
		}

		public static Color ColorOf(Bookmark bm)
		{
			if (bm == null || string.IsNullOrEmpty(bm.ColorHex))
				return new Color(0.30f, 0.80f, 1f);
			Color c;
			if (ColorUtility.TryParseHtmlString(bm.ColorHex, out c))
				return c;
			return new Color(0.30f, 0.80f, 1f);
		}

		public static void WorldChanged()
		{
			_loadedForThisWorld = false;
			_worldToken = null;
		}
	}
}
