using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FirstPerson.Pad
{
	internal static class XInput
	{
		[Flags]
		public enum Buttons : ushort
		{
			DpadUp = 1,
			DpadDown = 2,
			DpadLeft = 4,
			DpadRight = 8,
			Start = 0x10,
			Back = 0x20,
			LeftThumb = 0x40,
			RightThumb = 0x80,
			LeftShoulder = 0x100,
			RightShoulder = 0x200,
			A = 0x1000,
			B = 0x2000,
			X = 0x4000,
			Y = 0x8000
		}

		public struct State
		{
			public bool Connected;

			public Buttons Held;

			public Vector2 Left;

			public Vector2 Right;

			public float LeftTrigger;

			public float RightTrigger;

			public uint Packet;

			public bool Has(Buttons b)
			{
				return (Held & b) != 0;
			}
		}

		private struct Gamepad
		{
			public ushort Buttons;

			public byte LeftTrigger;

			public byte RightTrigger;

			public short ThumbLX;

			public short ThumbLY;

			public short ThumbRX;

			public short ThumbRY;
		}

		private struct RawState
		{
			public uint Packet;

			public Gamepad Pad;
		}

		private delegate uint GetStateFn(uint index, out RawState state);

		private const uint NotConnected = 1167u;

		private const int Slots = 4;

		private static GetStateFn _get;

		private static bool _tried;

		private static readonly State[] _pads = new State[4];

		private static readonly float[] _lookAgain = new float[4];

		private static bool _linux;

		public static bool Available
		{
			get
			{
				Load();
				if (_get == null)
				{
					return _linux;
				}
				return true;
			}
		}

		public static string Library { get; private set; } = "none";

		public static int Count => 4;

		[DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
		private static extern uint GetState14(uint index, out RawState state);

		[DllImport("xinput1_3.dll", EntryPoint = "XInputGetState")]
		private static extern uint GetState13(uint index, out RawState state);

		[DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
		private static extern uint GetState91(uint index, out RawState state);

		private static void Load()
		{
			if (_tried)
			{
				return;
			}
			_tried = true;
			if (LinuxPad.Here)
			{
				_linux = true;
				Library = "linux joystick";
				return;
			}
			if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor)
			{
				Library = "not on Windows";
				return;
			}
			(string, GetStateFn)[] array = new(string, GetStateFn)[3]
			{
				("xinput1_4", GetState14),
				("xinput1_3", GetState13),
				("xinput9_1_0", GetState91)
			};
			for (int i = 0; i < array.Length; i++)
			{
				var (text, getStateFn) = array[i];
				try
				{
					getStateFn(0u, out var _);
					_get = getStateFn;
					Library = text;
					break;
				}
				catch (DllNotFoundException)
				{
				}
				catch (EntryPointNotFoundException)
				{
				}
				catch (Exception ex3)
				{
					Log.Error("pad: " + text + ": " + ex3.Message);
				}
			}
		}

		public static void Poll(float now)
		{
			Load();
			if (_linux)
			{
				LinuxPad.Scan(now);
				for (int i = 0; i < 4; i++)
				{
					_pads[i] = LinuxPad.Read(i);
				}
			}
			else
			{
				if (_get == null)
				{
					return;
				}
				for (int j = 0; j < 4; j++)
				{
					if (!_pads[j].Connected && now < _lookAgain[j])
					{
						continue;
					}
					uint num;
					RawState state;
					try
					{
						num = _get((uint)j, out state);
					}
					catch (Exception ex)
					{
						Log.Error("pad: reading slot " + j + ": " + ex.Message);
						_get = null;
						break;
					}
					if (num != 0)
					{
						if (_pads[j].Connected)
						{
							Log.Info("pad: slot " + j + " let go");
						}
						_pads[j] = default(State);
						_lookAgain[j] = now + ((num == 1167) ? 2f : 5f);
						continue;
					}
					if (!_pads[j].Connected)
					{
						Log.Info("pad: slot " + j + " connected (" + Library + ")");
					}
					_pads[j] = new State
					{
						Connected = true,
						Packet = state.Packet,
						Held = (Buttons)state.Pad.Buttons,
						Left = Stick(state.Pad.ThumbLX, state.Pad.ThumbLY),
						Right = Stick(state.Pad.ThumbRX, state.Pad.ThumbRY),
						LeftTrigger = (float)(int)state.Pad.LeftTrigger / 255f,
						RightTrigger = (float)(int)state.Pad.RightTrigger / 255f
					};
				}
			}
		}

		public static State Pad(int slot)
		{
			if (slot < 0 || slot >= 4)
			{
				return default(State);
			}
			return _pads[slot];
		}

		private static Vector2 Stick(short x, short y)
		{
			return new Vector2(Mathf.Max(-1f, (float)x / 32767f), Mathf.Max(-1f, (float)y / 32767f));
		}
	}
}
