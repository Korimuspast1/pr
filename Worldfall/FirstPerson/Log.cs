using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal static class Log
	{
		private sealed class Repeat
		{
			public int At;

			public int Count;
		}

		private static string _path;

		private static StreamWriter _writer;

		private static readonly object Gate = new object();

		private const int RepeatMs = 60000;

		private static readonly Dictionary<string, Repeat> Repeats = new Dictionary<string, Repeat>();

		public static string Folder
		{
			get
			{
				string text = Environment.GetEnvironmentVariable("FIRSTPERSON_DATA");
				if (string.IsNullOrEmpty(text))
				{
					text = Path.Combine(Application.persistentDataPath, "FirstPerson");
				}
				Directory.CreateDirectory(text);
				return text;
			}
		}

		public static void Info(string message)
		{
			Write("INFO", message);
		}

		public static void Error(string message)
		{
			message = message ?? "";
			int num = 0;
			int tickCount = Environment.TickCount;
			lock (Repeats)
			{
				if (Repeats.TryGetValue(message, out var value))
				{
					if (tickCount - value.At < 60000)
					{
						value.Count++;
						return;
					}
					num = value.Count;
					value.At = tickCount;
					value.Count = 0;
				}
				else
				{
					if (Repeats.Count >= 500)
					{
						Repeats.Clear();
					}
					Repeats[message] = new Repeat
					{
						At = tickCount
					};
				}
			}
			Write("ERROR", (num > 0) ? (message + " (" + num + " more times in the last minute)") : message);
		}

		private static void Write(string level, string message)
		{
			if (level == "ERROR")
			{
				Debug.LogError("[FirstPerson] " + level + " " + message);
			}
			lock (Gate)
			{
				try
				{
					if (_path == null)
					{
						_path = Path.Combine(Folder, "FirstPerson.log");
						try
						{
							if (File.Exists(_path))
							{
								File.Copy(_path, Path.Combine(Folder, "FirstPerson.prev.log"), overwrite: true);
							}
						}
						catch
						{
						}
						File.WriteAllText(_path, "");
					}
					if (_writer == null)
					{
						_writer = new StreamWriter(new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
						{
							AutoFlush = true
						};
					}
					_writer.Write(DateTime.Now.ToString("HH:mm:ss.fff") + " " + level + " " + message + Environment.NewLine);
				}
				catch
				{
					try
					{
						_writer?.Dispose();
					}
					catch
					{
					}
					_writer = null;
				}
			}
		}
	}
}
