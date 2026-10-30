using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace FirstPerson.Pad
{
	internal static class LinuxPad
	{
		private sealed class Device
		{
			public string Path;

			public string Name;

			public FileStream Stream;

			public volatile bool Gone;

			public readonly object Lock = new object();

			public readonly bool[] Buttons = new bool[32];

			public readonly short[] Axes = new short[16];

			public readonly bool[] AxisSeen = new bool[16];

			public uint Packet;

			public bool XboxLike;
		}

		private static readonly Device[] _slots = new Device[4];

		private const int XInputSlots = 4;

		private static float _scanAt = -10f;

		private static readonly HashSet<string> _refused = new HashSet<string>();

		public static bool Here
		{
			get
			{
				if (Application.platform != RuntimePlatform.LinuxPlayer)
				{
					return Application.platform == RuntimePlatform.LinuxEditor;
				}
				return true;
			}
		}

		public static void Scan(float now)
		{
			for (int i = 0; i < _slots.Length; i++)
			{
				if (_slots[i] != null && _slots[i].Gone)
				{
					Log.Info("pad: " + _slots[i].Path + " (" + _slots[i].Name + ") let go");
					_slots[i] = null;
				}
			}
			if (now < _scanAt)
			{
				return;
			}
			_scanAt = now + 2f;
			string[] files;
			try
			{
				files = Directory.GetFiles("/dev/input", "js*");
			}
			catch
			{
				return;
			}
			Array.Sort(files, StringComparer.Ordinal);
			bool[] array = new bool[2] { true, false };
			foreach (bool flag in array)
			{
				string[] array2 = files;
				foreach (string text in array2)
				{
					if (Open(text))
					{
						continue;
					}
					string text2 = NameOf(text);
					if (IsXboxLike(text2) != flag)
					{
						continue;
					}
					int num = Array.IndexOf(_slots, null);
					if (num < 0)
					{
						return;
					}
					Device d = new Device
					{
						Path = text,
						Name = text2,
						XboxLike = flag
					};
					try
					{
						d.Stream = new FileStream(text, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 8);
					}
					catch (Exception ex)
					{
						if (_refused.Add(text))
						{
							Log.Info("pad: can't open " + text + " (" + text2 + "): " + ex.Message);
						}
						continue;
					}
					_slots[num] = d;
					Log.Info("pad: " + text + " (" + text2 + ") in slot " + num + (flag ? "" : ", not an Xbox-style pad: buttons may be off"));
					Thread thread = new Thread((ThreadStart)delegate
					{
						ReadLoop(d);
					});
					thread.IsBackground = true;
					thread.Name = "Worldfall pad " + text;
					thread.Start();
				}
			}
		}

		private static bool Open(string path)
		{
			Device[] slots = _slots;
			foreach (Device device in slots)
			{
				if (device != null && device.Path == path)
				{
					return true;
				}
			}
			return false;
		}

		private static string NameOf(string path)
		{
			try
			{
				return File.ReadAllText("/sys/class/input/" + Path.GetFileName(path) + "/device/name").Trim();
			}
			catch
			{
				return "?";
			}
		}

		private static bool IsXboxLike(string name)
		{
			string text = (name ?? "").ToLowerInvariant();
			if (!text.Contains("x-box") && !text.Contains("xbox") && !text.Contains("steam virtual"))
			{
				return text.Contains("microsoft");
			}
			return true;
		}

		private static void ReadLoop(Device d)
		{
			byte[] array = new byte[512];
			try
			{
				while (true)
				{
					int num = d.Stream.Read(array, 0, array.Length);
					if (num <= 0)
					{
						break;
					}
					lock (d.Lock)
					{
						for (int i = 0; i + 8 <= num; i += 8)
						{
							short num2 = (short)(array[i + 4] | (array[i + 5] << 8));
							int num3 = array[i + 6] & 0x7F;
							int num4 = array[i + 7];
							if (num3 == 1 && num4 < d.Buttons.Length)
							{
								d.Buttons[num4] = num2 != 0;
							}
							else if (num3 == 2 && num4 < d.Axes.Length)
							{
								d.Axes[num4] = num2;
								d.AxisSeen[num4] = true;
							}
							d.Packet++;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Info("pad: reading " + d.Path + ": " + ex.Message);
			}
			try
			{
				d.Stream.Dispose();
			}
			catch
			{
			}
			d.Gone = true;
		}

		public static XInput.State Read(int slot)
		{
			Device d = ((slot >= 0 && slot < _slots.Length) ? _slots[slot] : null);
			if (d == null || d.Gone)
			{
				return default(XInput.State);
			}
			lock (d.Lock)
			{
				XInput.Buttons b = (XInput.Buttons)0;
				Map(0, XInput.Buttons.A);
				Map(1, XInput.Buttons.B);
				Map(2, XInput.Buttons.X);
				Map(3, XInput.Buttons.Y);
				Map(4, XInput.Buttons.LeftShoulder);
				Map(5, XInput.Buttons.RightShoulder);
				Map(6, XInput.Buttons.Back);
				Map(7, XInput.Buttons.Start);
				Map(9, XInput.Buttons.LeftThumb);
				Map(10, XInput.Buttons.RightThumb);
				short num = d.Axes[6];
				short num2 = d.Axes[7];
				if (num < -16000)
				{
					b |= XInput.Buttons.DpadLeft;
				}
				if (num > 16000)
				{
					b |= XInput.Buttons.DpadRight;
				}
				if (num2 < -16000)
				{
					b |= XInput.Buttons.DpadUp;
				}
				if (num2 > 16000)
				{
					b |= XInput.Buttons.DpadDown;
				}
				Map(11, XInput.Buttons.DpadUp);
				Map(12, XInput.Buttons.DpadDown);
				Map(13, XInput.Buttons.DpadLeft);
				Map(14, XInput.Buttons.DpadRight);
				return new XInput.State
				{
					Connected = true,
					Packet = d.Packet,
					Held = b,
					Left = Stick(d.Axes[0], d.Axes[1]),
					Right = Stick(d.Axes[3], d.Axes[4]),
					LeftTrigger = (d.AxisSeen[2] ? Trigger(d.Axes[2]) : 0f),
					RightTrigger = (d.AxisSeen[5] ? Trigger(d.Axes[5]) : 0f)
				};
				void Map(int i, XInput.Buttons to)
				{
					if (d.Buttons[i])
					{
						b |= to;
					}
				}
			}
		}

		private static Vector2 Stick(short x, short y)
		{
			return new Vector2(Mathf.Max(-1f, (float)x / 32767f), Mathf.Max(-1f, (float)(-y) / 32767f));
		}

		private static float Trigger(short v)
		{
			return Mathf.Clamp01(((float)v + 32767f) / 65534f);
		}
	}
}
