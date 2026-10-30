using System;
using System.Diagnostics;
using System.Threading.Tasks;
using FirstPerson.Core;
using FirstPerson.Net;
using UnityEngine;

namespace FirstPerson
{
	internal static class ReliefWorld
	{
		public const string KeyRelief = "fp_relief";

		public const string KeyLand = "fp_world_land";

		public const string KeySeed = "fp_world_seed";

		public const int LandUnasked = 0;

		public const int LandFlat = 1;

		public const int LandTerrainy = 2;

		private static WorldSampler _sampler;

		private static object _loadedFor;

		private static int _land;

		private static int _savedVersion = int.MinValue;

		private static float _saveAt;

		private static float _lastHour = -1f;

		private static float _greenDays;

		internal static int Builds;

		internal static bool AutoAnswer;

		internal static int AutoLand = 1;

		public static bool CardOpen;

		public static float GreenDays = 3f;

		private static double _saveMs;

		private static double _snapMs;

		private static double _setMs;

		private static int _saves;

		private static int _savedChars;

		private static float _saveLogAt = 30f;

		private static Task<string> _encoding;

		private static Relief.Snapshot _snapshot;

		private static int _encodingVersion;

		private static object _encodingFor;

		private static string[] _typeNames;

		private static bool _putOff;

		private static bool _fromHost;

		private static object _hostFor;

		private static int _hostLand;

		private static int _hostSeed;

		private static string _hostRecord;

		public static int Land => _land;

		internal static int SeedNow
		{
			get
			{
				if (_sampler == null)
				{
					return 0;
				}
				return _sampler.Relief.Seed;
			}
		}

		internal static Relief Current => _sampler?.Relief;

		internal static WorldSampler Sampler => _sampler;

		internal static string TypeName(int id)
		{
			return WorldSampler.TypeOfId(id)?.id;
		}

		internal static int TypeId(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return 0;
			}
			try
			{
				TileTypeBase tileTypeBase = AssetManager.tiles.get(name);
				if (tileTypeBase == null)
				{
					tileTypeBase = AssetManager.top_tiles.get(name);
				}
				return (tileTypeBase != null) ? (tileTypeBase.index_id + 1) : 0;
			}
			catch
			{
				return 0;
			}
		}

		private static SaveCustomData Data(bool make)
		{
			MapStats mapStats = World.world?.map_stats;
			if (mapStats == null)
			{
				return null;
			}
			if (mapStats.custom_data == null && make)
			{
				mapStats.custom_data = new SaveCustomData();
			}
			return mapStats.custom_data;
		}

		internal static void Load(WorldSampler s)
		{
			_sampler = s;
			Builds++;
			Relief relief = s.Relief;
			MapStats mapStats = World.world?.map_stats;
			_loadedFor = (WorldBoxMod.WorldLoading ? null : mapStats);
			_land = 0;
			relief.Terrainy = false;
			relief.Seed = 0;
			if (_fromHost && NetSession.Joined && _hostFor == mapStats)
			{
				_land = _hostLand;
				relief.Terrainy = _hostLand == 2;
				relief.Seed = _hostSeed;
				ApplyRecord(relief, _hostRecord);
				WorldSync.ApplyHeldRelief(relief);
				_savedVersion = relief.Version;
				return;
			}
			if (!NetSession.Joined)
			{
				ForgetHost();
			}
			SaveCustomData saveCustomData = mapStats?.custom_data;
			if (saveCustomData == null)
			{
				_savedVersion = relief.Version;
				return;
			}
			try
			{
				saveCustomData.get("fp_world_land", out var pResult, 0);
				saveCustomData.get("fp_world_seed", out var pResult2, 0);
				_land = ((pResult == 1 || pResult == 2) ? pResult : 0);
				relief.Terrainy = _land == 2;
				relief.Seed = pResult2;
				saveCustomData.get("fp_relief", out var pResult3, null);
				Log.Info(string.Format("relief: reading the world's data ({0}): land {1}, seed {2}, record {3}", (_loadedFor == null) ? "still loading" : "loaded", pResult, pResult2, (pResult3 == null) ? "none" : (pResult3.Length + " chars")));
				if (!string.IsNullOrEmpty(pResult3))
				{
					if (relief.Load(pResult3, TypeId))
					{
						Log.Info($"relief: {relief.EditedCount()} tiles dug or filled, {relief.LakeTileCount()} under lakes, read from the save");
					}
					else
					{
						Log.Error("relief: the saved record isn't for this map (" + pResult3.Substring(0, Math.Min(24, pResult3.Length)) + "...)");
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("relief: reading the save: " + ex.Message);
			}
			_savedVersion = relief.Version;
		}

		public static void Tick(bool firstPerson, DayCycle day)
		{
			if (_sampler == null || WorldBoxMod.WorldLoading)
			{
				return;
			}
			MapStats mapStats = World.world?.map_stats;
			if (mapStats == null)
			{
				return;
			}
			if (mapStats != _loadedFor && _sampler.Ready)
			{
				_loadedFor = mapStats;
				_sampler.Invalidate();
				_lastHour = -1f;
				return;
			}
			Relief relief = _sampler.Relief;
			if (day != null)
			{
				float timeOfDay = day.TimeOfDay;
				if (_lastHour >= 0f)
				{
					float num = timeOfDay - _lastHour;
					if (num < 0f)
					{
						num += 1f;
					}
					if (num < 0.25f)
					{
						_greenDays += num;
					}
				}
				_lastHour = timeOfDay;
				if (_greenDays >= 0.05f && !NetSession.Joined)
				{
					relief.Green(_greenDays / GreenDays);
					_greenDays = 0f;
				}
			}
			FinishSave();
			if (!NetSession.Joined && relief.Version != _savedVersion && Time.unscaledTime >= _saveAt)
			{
				SaveInBackground();
			}
			if (firstPerson && _land == 0 && !NetSession.Joined && !CardOpen && !_putOff)
			{
				if (AutoAnswer)
				{
					Choose(AutoLand);
				}
				else
				{
					CardOpen = true;
				}
			}
			if (!firstPerson)
			{
				CardOpen = (_putOff = false);
			}
		}

		public static void SaveNow()
		{
			if (_sampler == null || NetSession.Joined)
			{
				return;
			}
			Relief relief = _sampler.Relief;
			_saveAt = Time.unscaledTime + 2f;
			int version = relief.Version;
			long timestamp = Stopwatch.GetTimestamp();
			try
			{
				SaveCustomData saveCustomData = Data(make: true);
				if (saveCustomData == null)
				{
					return;
				}
				string text = relief.Save(TypeName);
				if (text != null)
				{
					saveCustomData.set("fp_relief", text);
				}
				else
				{
					saveCustomData.removeString("fp_relief");
				}
				_savedVersion = version;
				_savedChars = text?.Length ?? 0;
			}
			catch (Exception ex)
			{
				Log.Error("relief: saving: " + ex.Message);
				_savedVersion = version;
			}
			_saveMs += (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
			_saves++;
			if (Time.unscaledTime >= _saveLogAt)
			{
				_saveLogAt = Time.unscaledTime + 60f;
				Log.Info($"relief: kept in the world's data {_saves} times in the last minute, {_saveMs:0.0} ms in all, record {_savedChars} chars, {relief.EditedCount()} tiles edited");
				_saves = 0;
				_saveMs = 0.0;
			}
		}

		private static void SaveInBackground()
		{
			if (_sampler == null || NetSession.Joined || _encoding != null)
			{
				return;
			}
			Relief relief = _sampler.Relief;
			_saveAt = Time.unscaledTime + 2f;
			long timestamp = Stopwatch.GetTimestamp();
			Relief.Snapshot snap = (_snapshot = relief.TakeSnapshot(_snapshot));
			string[] names = TypeNames();
			_encodingVersion = relief.Version;
			_encodingFor = World.world?.map_stats;
			_encoding = Task.Run(() => Relief.Encode(snap, (int id) => (id <= 0 || id >= names.Length) ? null : names[id]));
			_snapMs += (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
		}

		private static void FinishSave()
		{
			Task<string> encoding = _encoding;
			if (encoding == null || !encoding.IsCompleted)
			{
				return;
			}
			_encoding = null;
			if (encoding.IsFaulted)
			{
				Log.Error("relief: saving: " + encoding.Exception?.GetBaseException().Message);
				_savedVersion = _encodingVersion;
			}
			else
			{
				if (_encodingFor != World.world?.map_stats || _sampler == null || _encodingVersion - _savedVersion <= 0)
				{
					return;
				}
				long timestamp = Stopwatch.GetTimestamp();
				try
				{
					SaveCustomData saveCustomData = Data(make: true);
					if (saveCustomData == null)
					{
						return;
					}
					string result = encoding.Result;
					if (result != null)
					{
						saveCustomData.set("fp_relief", result);
					}
					else
					{
						saveCustomData.removeString("fp_relief");
					}
					_savedVersion = _encodingVersion;
					_savedChars = result?.Length ?? 0;
				}
				catch (Exception ex)
				{
					Log.Error("relief: saving: " + ex.Message);
					_savedVersion = _encodingVersion;
				}
				_setMs += (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
				_saves++;
				if (Time.unscaledTime >= _saveLogAt)
				{
					_saveLogAt = Time.unscaledTime + 60f;
					Log.Info($"relief: kept in the world's data {_saves} times in the last minute, {_snapMs + _setMs:0.0} ms of the game's frames in all (copies {_snapMs:0.0}, into the data {_setMs:0.0}), record {_savedChars} chars");
					_saves = 0;
					_snapMs = (_setMs = 0.0);
				}
			}
		}

		private static string[] TypeNames()
		{
			if (_typeNames != null)
			{
				return _typeNames;
			}
			string[] names = new string[1];
			try
			{
				foreach (TileType item in AssetManager.tiles.list)
				{
					Put(ref names, item);
				}
				foreach (TopTileType item2 in AssetManager.top_tiles.list)
				{
					Put(ref names, item2);
				}
			}
			catch
			{
			}
			return _typeNames = names;
		}

		private static void Put(ref string[] names, TileTypeBase t)
		{
			if (t == null)
			{
				return;
			}
			int num = t.index_id + 1;
			if (num > 0)
			{
				if (num >= names.Length)
				{
					Array.Resize(ref names, Math.Max(num + 1, names.Length * 2));
				}
				if (names[num] == null)
				{
					names[num] = t.id;
				}
			}
		}

		public static void Postpone()
		{
			CardOpen = false;
			_putOff = true;
		}

		public static void Choose(int land)
		{
			CardOpen = false;
			if (land != 1 && land != 2)
			{
				return;
			}
			SaveCustomData saveCustomData = Data(make: true);
			if (saveCustomData == null)
			{
				return;
			}
			saveCustomData.get("fp_world_seed", out var pResult, 0);
			if (pResult == 0)
			{
				pResult = UnityEngine.Random.Range(1, int.MaxValue);
				saveCustomData.set("fp_world_seed", pResult);
			}
			saveCustomData.set("fp_world_land", land);
			_land = land;
			Log.Info("relief: this world's land is " + ((land == 2) ? "Terrainy" : "Flat") + $" (seed {pResult})");
			if (_sampler != null)
			{
				Relief relief = _sampler.Relief;
				bool flag = land == 2;
				relief.Seed = pResult;
				if (relief.Terrainy != flag)
				{
					SaveNow();
					relief.Terrainy = flag;
					if (!flag)
					{
						relief.SetGen(null);
					}
					_sampler.Invalidate();
				}
			}
			WorldSync.SendLandToAll();
		}

		internal static void FromHost(int land, int seed, string record)
		{
			_fromHost = true;
			_hostFor = World.world?.map_stats;
			_hostLand = ((land == 1 || land == 2) ? land : 0);
			_hostSeed = seed;
			_hostRecord = record;
			_land = _hostLand;
			if (_sampler == null)
			{
				return;
			}
			Relief relief = _sampler.Relief;
			bool flag = _hostLand == 2;
			if (_sampler.Ready && relief.Width * relief.Height != 0)
			{
				if (relief.Terrainy != flag || relief.Seed != seed)
				{
					relief.Terrainy = flag;
					relief.Seed = seed;
					_sampler.Invalidate();
				}
				else
				{
					ApplyRecord(relief, record);
				}
			}
		}

		private static void ApplyRecord(Relief r, string record)
		{
			if (string.IsNullOrEmpty(record))
			{
				r.ClearEdits();
			}
			else if (!r.Load(record, TypeId))
			{
				Log.Error("relief: the host's record isn't for this map");
			}
		}

		internal static void ForgetHost()
		{
			_fromHost = false;
			_hostFor = null;
			_hostRecord = null;
			WorldSync.ForgetHeldRelief();
		}
	}
}
