using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal static class FirstPersonSave
	{
		internal static bool Suppressed;

		public const string PlayerKey = "fp_player";

		public const string InKey = "fp_player_in";

		private static string _slotPath;

		private static int _slot = -1;

		private static object _slotWorld;

		private static object _world;

		private static bool _resumeChecked;

		private static bool _hooked;

		private static bool _wasFp;

		private static string _seenPath;

		private static DateTime _seenWritten;

		private static float _seenAt;

		private static Building _resumeIn;

		private static float _resumeInSince;

		internal static int Saves;

		internal static int Restores;

		internal static string LastPath;

		internal static string PathForTest;

		internal static string SlotForTest
		{
			get
			{
				if (_slotWorld != World.world?.map_stats)
				{
					return null;
				}
				return _slotPath;
			}
		}

		public static void Update(WorldBoxMod mod)
		{
			Hook();
			object obj = ((World.world != null) ? World.world.map_stats : null);
			if (obj != _world)
			{
				_world = obj;
				_resumeChecked = false;
				_wasFp = false;
			}
			if (!WorldBoxMod.WorldLoading)
			{
				bool flag = mod != null && mod.IsFirstPerson;
				if (_wasFp && !flag)
				{
					Forget();
				}
				_wasFp = flag;
				FollowGameSave();
				if (!_resumeChecked)
				{
					_resumeChecked = true;
					Resume(mod);
				}
				ResumeIndoors(mod);
			}
		}

		private static void Hook()
		{
			if (_hooked)
			{
				return;
			}
			_hooked = true;
			try
			{
				MapBox.on_world_loaded = (Action)Delegate.Combine(MapBox.on_world_loaded, new Action(OnLoaded));
			}
			catch (Exception ex)
			{
				Log.Error("save: could not follow the world's loading: " + ex.Message);
			}
		}

		private static void OnLoaded()
		{
			string currentSavePath = SaveManager.currentSavePath;
			if (!string.IsNullOrEmpty(currentSavePath))
			{
				if (!SameWorld(currentSavePath, sameTime: true))
				{
					Log.Info("save: a world made or loaded, not from " + currentSavePath + ": saves go to a free slot");
					return;
				}
				_slotPath = Trim(currentSavePath);
				_slot = SaveManager.currentSlot;
				_slotWorld = World.world.map_stats;
				Log.Info("save: the world came from " + currentSavePath);
			}
		}

		private static void FollowGameSave()
		{
			if (_slotPath == null || _slotWorld != World.world?.map_stats || Time.unscaledTime - _seenAt < 3f)
			{
				return;
			}
			_seenAt = Time.unscaledTime;
			string currentSavePath = SaveManager.currentSavePath;
			if (string.IsNullOrEmpty(currentSavePath) || Trim(currentSavePath) == _slotPath)
			{
				return;
			}
			try
			{
				if (!Directory.Exists(currentSavePath))
				{
					return;
				}
				DateTime lastWriteTimeUtc = Directory.GetLastWriteTimeUtc(currentSavePath);
				if (!(currentSavePath == _seenPath) || !(lastWriteTimeUtc == _seenWritten))
				{
					_seenPath = currentSavePath;
					_seenWritten = lastWriteTimeUtc;
					if (SameWorld(currentSavePath, sameTime: false))
					{
						_slotPath = Trim(currentSavePath);
						_slot = SaveManager.currentSlot;
						_slotWorld = World.world.map_stats;
						Log.Info("save: the world was saved to " + currentSavePath + ", saving there from now on");
					}
				}
			}
			catch
			{
			}
		}

		private static bool SameWorld(string folder, bool sameTime)
		{
			try
			{
				if (!File.Exists(SaveManager.generateMetaPath(SaveManager.folderPath(folder))))
				{
					return false;
				}
				MapMetaData metaFor = SaveManager.getMetaFor(folder);
				MapStats mapStats = World.world?.map_stats;
				if (metaFor?.mapStats == null || mapStats == null)
				{
					return false;
				}
				if (metaFor.mapStats.name != mapStats.name || metaFor.width != Config.ZONE_AMOUNT_X || metaFor.height != Config.ZONE_AMOUNT_Y)
				{
					return false;
				}
				return !sameTime || Math.Abs(metaFor.mapStats.world_time - mapStats.world_time) < 1.0;
			}
			catch
			{
				return false;
			}
		}

		private static string Trim(string path)
		{
			return path?.TrimEnd('/', '\\');
		}

		public static bool SaveNow(Actor you, Building inside, Action<string> toast, string why = "slept")
		{
			if (Suppressed || you == null || World.world == null || WorldBoxMod.WorldLoading)
			{
				return false;
			}
			try
			{
				MapStats map_stats = World.world.map_stats;
				if (map_stats.custom_data == null)
				{
					map_stats.custom_data = new SaveCustomData();
				}
				map_stats.custom_data.set("fp_player", you.data.id.ToString());
				map_stats.custom_data.set("fp_player_in", (inside != null) ? inside.getID().ToString() : "");
				try
				{
					Chronicle.Flush(you);
				}
				catch
				{
				}
				try
				{
					Explored.Write(you, Explored.Of(you));
				}
				catch
				{
				}
				string text = ((_slotWorld == map_stats) ? _slotPath : null);
				int num = _slot;
				if (!string.IsNullOrEmpty(PathForTest))
				{
					text = Trim(PathForTest);
					num = -1;
				}
				else if (string.IsNullOrEmpty(text))
				{
					num = FreeSlot();
					if (num < 0)
					{
						toast?.Invoke("No free save slot");
						return false;
					}
					text = Trim(SaveManager.getSlotSavePath(num));
				}
				Stopwatch stopwatch = Stopwatch.StartNew();
				SaveManager.saveWorldToDirectory(text);
				SaveManager.setCurrentPathAndId(text, num);
				_slotPath = text;
				_slot = num;
				_slotWorld = map_stats;
				Saves++;
				LastPath = text;
				Log.Info(string.Format("save: {0}, saved to {1} (slot {2}) in {3:F0} ms, you {4}{5}", why, text, num, stopwatch.Elapsed.TotalMilliseconds, WorldBoxMod.Describe(you), (inside != null) ? " in a house" : ""));
				toast?.Invoke("Saved");
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("save: could not save: " + ex.Message);
				toast?.Invoke("Could not save");
				return false;
			}
		}

		private static void Forget()
		{
			try
			{
				SaveCustomData saveCustomData = World.world?.map_stats?.custom_data;
				if (saveCustomData != null)
				{
					saveCustomData.get("fp_player", out var pResult, null);
					if (!string.IsNullOrEmpty(pResult))
					{
						saveCustomData.set("fp_player", "");
						saveCustomData.set("fp_player_in", "");
					}
				}
			}
			catch
			{
			}
		}

		private static int FreeSlot()
		{
			for (int i = 1; i <= 60; i++)
			{
				try
				{
					if (!SaveManager.slotExists(i))
					{
						return i;
					}
				}
				catch
				{
				}
			}
			return -1;
		}

		private static void Resume(WorldBoxMod mod)
		{
			_resumeIn = null;
			if (Suppressed || mod == null)
			{
				return;
			}
			try
			{
				SaveCustomData saveCustomData = World.world.map_stats?.custom_data;
				if (saveCustomData == null)
				{
					return;
				}
				saveCustomData.get("fp_player", out var pResult, null);
				if (string.IsNullOrEmpty(pResult) || !long.TryParse(pResult, out var result) || result <= 0)
				{
					return;
				}
				Actor actor = World.world.units.get(result);
				if (actor == null || !actor.isAlive())
				{
					Log.Info("save: the body saved in this world is gone");
				}
				else
				{
					if (mod.IsFirstPerson && mod.Host == actor)
					{
						return;
					}
					saveCustomData.get("fp_player_in", out var pResult2, null);
					if (!string.IsNullOrEmpty(pResult2) && long.TryParse(pResult2, out var result2) && result2 > 0)
					{
						try
						{
							foreach (Building simple in World.world.buildings.getSimpleList())
							{
								if (simple != null && simple.getID() == result2 && simple.isAlive())
								{
									_resumeIn = simple;
									break;
								}
							}
						}
						catch
						{
						}
					}
					if (Possess.Swap(actor))
					{
						Restores++;
						_resumeInSince = Time.unscaledTime;
						Log.Info("save: back in " + WorldBoxMod.Describe(actor) + ((_resumeIn != null) ? ", in a house" : ""));
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("save: could not put you back: " + ex.Message);
			}
		}

		private static void ResumeIndoors(WorldBoxMod mod)
		{
			if (_resumeIn != null)
			{
				HouseWorld.Placement p;
				if (Time.unscaledTime - _resumeInSince > 20f || !_resumeIn.isAlive())
				{
					_resumeIn = null;
				}
				else if (mod.IsFirstPerson && mod.DiveDone && !mod.Interior.Inside && (HouseWorld.Of(_resumeIn, out p) || !(Time.unscaledTime - _resumeInSince < 5f)))
				{
					Building resumeIn = _resumeIn;
					_resumeIn = null;
					mod.EnterHouseForTest(resumeIn);
				}
			}
		}
	}
}
