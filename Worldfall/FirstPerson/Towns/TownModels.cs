using System;
using System.Collections.Generic;

namespace FirstPerson.Towns
{
	internal static class TownModels
	{
		public const string Prefix = "tw_";

		private static readonly Dictionary<string, string> BaseOf = new Dictionary<string, string>();

		private static readonly object Lock = new object();

		private static bool _pickFailed;

		private static int _bumps;

		internal static Func<Building, string, string> PickForTest;

		public static int Generation => Models.TownPacksLoaded + _bumps;

		public static void Bump()
		{
			_bumps++;
		}

		public static string Family(Building b, string family)
		{
			if (b == null || family == null || !Models.Ready)
			{
				return null;
			}
			string text = null;
			try
			{
				text = PickForTest?.Invoke(b, family) ?? HallModels.Pick(b, family);
			}
			catch (Exception ex)
			{
				if (!_pickFailed)
				{
					Log.Error("towns: picking a model for " + family + ": " + ex);
				}
				_pickFailed = true;
				return null;
			}
			if (text == null || !Models.Has(text) || !Models.PackReady(text))
			{
				return null;
			}
			lock (Lock)
			{
				BaseOf[text] = family;
				return text;
			}
		}

		public static string Base(string family)
		{
			if (family == null || !family.StartsWith("tw_", StringComparison.Ordinal))
			{
				return family;
			}
			lock (Lock)
			{
				string value;
				return BaseOf.TryGetValue(family, out value) ? value : family;
			}
		}
	}
}
