using System.Collections.Generic;
using FirstPerson.Pad;
using FirstPerson.Vr;
using UnityEngine;

namespace FirstPerson
{
	internal static class PlayInput
	{
		private static readonly HashSet<KeyCode> TestKeys = new HashSet<KeyCode>();

		private static readonly Dictionary<KeyCode, int> _testDownAt = new Dictionary<KeyCode, int>();

		private static readonly Dictionary<KeyCode, int> _testUpAt = new Dictionary<KeyCode, int>();

		public static KeyCode HeldBack = KeyCode.None;

		public static KeyCode HeldBack2 = KeyCode.None;

		private static int _injectFrame = -1;

		private static KeyCode _injectKey;

		private static bool _injectShift;

		private static bool InjectNow => _injectFrame == Time.frameCount;

		public static bool Virtual
		{
			get
			{
				if (!TouchControls.Shown && !VrControls.Active)
				{
					return PadControls.Active;
				}
				return true;
			}
		}

		private static bool RealMouse => !TouchControls.Shown;

		public static bool GetKey(KeyCode k)
		{
			if (!Masked(k))
			{
				if (!RawKey(k))
				{
					return Injected(k);
				}
				return true;
			}
			return false;
		}

		public static bool GetKeyDown(KeyCode k)
		{
			if (!Masked(k))
			{
				if (!RawKeyDown(k))
				{
					return Injected(k);
				}
				return true;
			}
			return false;
		}

		public static bool GetKeyUp(KeyCode k)
		{
			if (!Masked(k))
			{
				return RawKeyUp(k);
			}
			return false;
		}

		public static bool RawKey(KeyCode k)
		{
			if ((!Real(k) || !Input.GetKey(k)) && !TouchControls.Held(k) && !VrControls.Held(k) && !PadControls.Held(k))
			{
				return TestHeld(k);
			}
			return true;
		}

		public static bool RawKeyDown(KeyCode k)
		{
			if ((!Real(k) || !Input.GetKeyDown(k)) && !TouchControls.Down(k) && !VrControls.Down(k) && !PadControls.Down(k))
			{
				return TestDown(k);
			}
			return true;
		}

		public static bool RawKeyUp(KeyCode k)
		{
			if ((!Real(k) || !Input.GetKeyUp(k)) && !TouchControls.Up(k) && !VrControls.Up(k) && !PadControls.Up(k))
			{
				return TestUp(k);
			}
			return true;
		}

		internal static void TestPress(KeyCode k)
		{
			TestKeys.Add(k);
			_testDownAt[k] = Time.frameCount + 1;
		}

		internal static void TestRelease(KeyCode k)
		{
			if (TestKeys.Remove(k))
			{
				_testUpAt[k] = Time.frameCount + 1;
			}
		}

		internal static void TestReleaseAll()
		{
			foreach (KeyCode item in new List<KeyCode>(TestKeys))
			{
				TestRelease(item);
			}
		}

		private static bool TestHeld(KeyCode k)
		{
			if (TestKeys.Count > 0 && TestKeys.Contains(k) && _testDownAt.TryGetValue(k, out var value))
			{
				return Time.frameCount >= value;
			}
			return false;
		}

		private static bool TestDown(KeyCode k)
		{
			if (_testDownAt.Count > 0 && TestKeys.Contains(k) && _testDownAt.TryGetValue(k, out var value))
			{
				return Time.frameCount == value;
			}
			return false;
		}

		private static bool TestUp(KeyCode k)
		{
			if (_testUpAt.Count > 0 && _testUpAt.TryGetValue(k, out var value))
			{
				return Time.frameCount == value;
			}
			return false;
		}

		public static void Inject(KeyCode k, bool shift)
		{
			_injectFrame = Time.frameCount;
			_injectKey = k;
			_injectShift = shift;
		}

		private static bool IsShift(KeyCode k)
		{
			if (k != KeyCode.LeftShift)
			{
				return k == KeyCode.RightShift;
			}
			return true;
		}

		private static bool Injected(KeyCode k)
		{
			if (InjectNow)
			{
				if (k != _injectKey)
				{
					if (_injectShift)
					{
						return k == KeyCode.LeftShift;
					}
					return false;
				}
				return true;
			}
			return false;
		}

		private static bool Masked(KeyCode k)
		{
			if (k == KeyCode.None || (k != HeldBack && k != HeldBack2) || (InjectNow && k == _injectKey))
			{
				if (InjectNow && !_injectShift)
				{
					return IsShift(k);
				}
				return false;
			}
			return true;
		}

		public static bool GetMouseButton(int b)
		{
			return RawMouseButton(b);
		}

		public static bool GetMouseButtonDown(int b)
		{
			if ((!RealMouse || !Input.GetMouseButtonDown(b)) && !TouchControls.Down((KeyCode)(323 + b)) && !VrControls.Down((KeyCode)(323 + b)) && !PadControls.Down((KeyCode)(323 + b)))
			{
				return TestDown((KeyCode)(323 + b));
			}
			return true;
		}

		public static bool GetMouseButtonUp(int b)
		{
			if ((!RealMouse || !Input.GetMouseButtonUp(b)) && !TouchControls.Up((KeyCode)(323 + b)) && !VrControls.Up((KeyCode)(323 + b)) && !PadControls.Up((KeyCode)(323 + b)))
			{
				return TestUp((KeyCode)(323 + b));
			}
			return true;
		}

		public static bool RawMouseButton(int b)
		{
			if ((!RealMouse || !Input.GetMouseButton(b)) && !TouchControls.Held((KeyCode)(323 + b)) && !VrControls.Held((KeyCode)(323 + b)) && !PadControls.Held((KeyCode)(323 + b)))
			{
				return TestHeld((KeyCode)(323 + b));
			}
			return true;
		}

		private static bool Real(KeyCode k)
		{
			if (!RealMouse && k >= KeyCode.Mouse0)
			{
				return k > KeyCode.Mouse6;
			}
			return true;
		}
	}
}
