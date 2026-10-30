using System;
using System.Collections.Generic;
using WorldBoxConsole;

namespace FirstPerson
{
	internal static class GameConsole
	{
		private static bool _wasCovered;

		private static bool _reopen;

		private static bool _trimmed;

		private static void TrimLoaderListing()
		{
			_trimmed = true;
			try
			{
				Queue<string> texts = WorldBoxConsole.Console._texts;
				if (texts == null || texts.Count == 0)
				{
					return;
				}
				List<string> list = new List<string>(texts.Count);
				int num = 0;
				foreach (string item in texts)
				{
					if (item != null && (item.Contains("[Worldfall") || item.Contains("[FirstPerson] ")) && !item.Contains(" INFO ") && !item.Contains(" ERROR ") && !item.Contains("Loading "))
					{
						num++;
					}
					else
					{
						list.Add(item);
					}
				}
				if (num == 0)
				{
					return;
				}
				texts.Clear();
				foreach (string item2 in list)
				{
					texts.Enqueue(item2);
				}
				Log.Info($"game console: the loader's {num} lines listing the mod's classes taken out of it");
			}
			catch (Exception ex)
			{
				Log.Error("game console trim: " + ex.Message);
			}
		}

		public static void Update(bool covered)
		{
			if (!_trimmed)
			{
				TrimLoaderListing();
			}
			if (covered == _wasCovered && !covered)
			{
				return;
			}
			try
			{
				WorldBoxConsole.Console console = ((World.world != null) ? World.world.console : null);
				if (console == null)
				{
					_wasCovered = covered;
					return;
				}
				if (covered)
				{
					if (!_wasCovered)
					{
						_reopen = console.isActive();
					}
					if (console.isActive())
					{
						console.Hide();
						Log.Info("game console closed while the 3D view covers it (it rebuilds all its text on every log line)");
					}
				}
				else if (_reopen)
				{
					_reopen = false;
					if (!console.isActive())
					{
						console.Show();
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("game console: " + ex.Message);
			}
			_wasCovered = covered;
		}
	}
}
