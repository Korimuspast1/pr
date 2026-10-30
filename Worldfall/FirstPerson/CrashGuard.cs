using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal static class CrashGuard
	{
		public const string Gpu = "gpu";

		public const string Spatial = "spatial";

		public const string Shaders = "shaders";

		public const string Rooms = "gpurooms";

		public const string Steam = "steam";

		public const string Start = "start";

		private static readonly string[] Steps = new string[6] { "start", "shaders", "gpu", "gpurooms", "steam", "spatial" };

		private static readonly bool _guarded = (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor) || Environment.GetEnvironmentVariable("FIRSTPERSON_CRASHGUARD") == "1";

		private static readonly Dictionary<string, bool> _suspect = new Dictionary<string, bool>();

		private static readonly HashSet<string> _held = new HashSet<string>();

		private static bool _quitHooked;

		private static bool Guarded => _guarded;

		private static string Build
		{
			get
			{
				try
				{
					return typeof(CrashGuard).Module.ModuleVersionId.ToString();
				}
				catch
				{
					return "?";
				}
			}
		}

		private static string PathOf(string step)
		{
			return Path.Combine(Log.Folder, "crash_guard_" + step + ".txt");
		}

		public static bool Suspect(string step)
		{
			if (!Guarded)
			{
				return false;
			}
			if (_suspect.TryGetValue(step, out var value))
			{
				return value;
			}
			bool flag = false;
			try
			{
				string path = PathOf(step);
				flag = File.Exists(path) && File.ReadAllText(path).Trim() == Build;
			}
			catch
			{
			}
			_suspect[step] = flag;
			return flag;
		}

		public static void Enter(string step)
		{
			if (!Guarded)
			{
				return;
			}
			Suspect(step);
			if (_held.Contains(step))
			{
				return;
			}
			try
			{
				File.WriteAllText(PathOf(step), Build);
				_held.Add(step);
				if (!_quitHooked)
				{
					_quitHooked = true;
					Application.quitting += LeaveAll;
				}
			}
			catch
			{
			}
		}

		public static void Leave(string step)
		{
			if (!Guarded || !_held.Remove(step))
			{
				return;
			}
			try
			{
				File.Delete(PathOf(step));
			}
			catch
			{
			}
		}

		public static bool Held(string step)
		{
			return _held.Contains(step);
		}

		internal static void LeaveAll()
		{
			foreach (string item in new List<string>(_held))
			{
				if (item != "start" || !Suspect("start"))
				{
					Leave(item);
				}
			}
		}

		private static string What(string step)
		{
			switch (step)
			{
			case "start":
				return "starting (safe start: Steam calls, map sizes and frame markers off)";
			case "shaders":
				return "loading the GPU shaders";
			case "gpu":
				return "starting the GPU renderer";
			case "gpurooms":
				return "drawing a room on the GPU";
			case "steam":
				return "asking Steam for Workshop items and invites";
			case "spatial":
				return "starting the 3D sound";
			default:
				return step;
			}
		}

		private static string Short(string step)
		{
			switch (step)
			{
			case "start":
				return "safe start";
			case "shaders":
			case "gpu":
				return "GPU renderer off";
			case "gpurooms":
				return "GPU rooms off";
			case "steam":
				return "Steam calls off";
			case "spatial":
				return "3D sound off";
			default:
				return step;
			}
		}

		public static void Report()
		{
			try
			{
				Log.Info($"platform {Application.platform}, {SystemInfo.operatingSystem}, {SystemInfo.processorType}, graphics {SystemInfo.graphicsDeviceType} ({SystemInfo.graphicsDeviceName}), patching {Detour.Describe()}");
			}
			catch
			{
			}
			string[] steps = Steps;
			foreach (string step in steps)
			{
				if (Suspect(step))
				{
					string text = "";
					try
					{
						text = PathOf(step);
					}
					catch
					{
					}
					Log.Error("crash guard: the game crashed last time while " + What(step) + "; it stays off with this build. Delete " + text + " to try again.");
				}
			}
		}

		public static string Notice()
		{
			List<string> list = new List<string>();
			string[] steps = Steps;
			foreach (string step in steps)
			{
				if (Suspect(step))
				{
					string item = Short(step);
					if (!list.Contains(item))
					{
						list.Add(item);
					}
				}
			}
			if (list.Count == 0)
			{
				return null;
			}
			return "Worldfall: the game crashed last time. " + string.Join(", ", list.ToArray()) + ". See FirstPerson.log";
		}
	}
}
