using System;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Vr;
using UnityEngine;

namespace FirstPerson.Pad
{
	internal static class PadControls
	{
		public enum Mode
		{
			Off,
			Play,
			Menu,
			Map
		}

		public enum Pictures
		{
			Xbox,
			Deck,
			PlayStation
		}

		public const KeyCode QuickMenuKey = KeyCode.KeypadDivide;

		public const KeyCode NextToolKey = KeyCode.KeypadMultiply;

		public const KeyCode PrevToolKey = KeyCode.F4;

		public static bool Active;

		public static bool Shown;

		public static Pictures Kind;

		public static Vector2 Move;

		public static Vector2 LeftStick;

		public static float LookScale = 1f;

		public static bool Driving;

		public static Vector2 Cursor;

		public static bool CursorMoved;

		public static bool CursorLeft;

		public static bool CursorRight;

		public static XInput.State? Simulated;

		public static bool DeckForTest;

		private static readonly HashSet<KeyCode> Now = new HashSet<KeyCode>();

		private static readonly HashSet<KeyCode> Before = new HashSet<KeyCode>();

		private static readonly HashSet<KeyCode> Tapped = new HashSet<KeyCode>();

		private static XInput.State _pad;

		private static XInput.State _was;

		private static int _slot = -1;

		private static Vector2 _look;

		private static int _step;

		private static int _tool;

		private static int _navX;

		private static int _navY;

		private static int _wheel;

		private static bool _rt;

		private static bool _lt;

		private static bool _sprint;

		private static bool _backWas;

		private static bool _backTapped;

		private static bool _menuHeldFired;

		private static bool _clickQueued;

		private static bool _clickDown;

		private static float _edge;

		private static float _still;

		private static float _backDownAt;

		private static float _backUpAt = -10f;

		private static float _menuDownAt = -1f;

		private static float _navNext;

		private static float _bumpNext;

		private static int _navHeldX;

		private static int _navHeldY;

		private static int _bumpHeld;

		private static float _lastPadUse = -10f;

		private static float _lastRealUse = -10f;

		private static Vector3 _realMouseWas;

		private static CursorLockMode _lockWas;

		private static float _lockChangedAt = -10f;

		private static bool _realMouseSeen;

		private static bool _loggedKind;

		private static XInput.Buttons _swallowed;

		private static bool _swallowRt;

		private static bool _swallowLt;

		private static Mode _modeWas;

		internal static bool BackstepTap;

		private static KeyCode[] _keyboardKeys;

		private static readonly KeyCode[] LayoutKeys = new KeyCode[6]
		{
			KeyCode.H,
			KeyCode.R,
			KeyCode.V,
			KeyCode.KeypadDivide,
			KeyCode.KeypadMultiply,
			KeyCode.F4
		};

		private static bool? _onDeck;

		private static float _namesAt = -10f;

		private static bool _playStation;

		private static bool Rt
		{
			get
			{
				if (_rt)
				{
					return !_swallowRt;
				}
				return false;
			}
		}

		private static bool Lt
		{
			get
			{
				if (_lt)
				{
					return !_swallowLt;
				}
				return false;
			}
		}

		public static bool OnDeck
		{
			get
			{
				if (DeckForTest)
				{
					return true;
				}
				if (_onDeck.HasValue)
				{
					return _onDeck.Value;
				}
				bool flag = false;
				try
				{
					flag = Environment.GetEnvironmentVariable("SteamDeck") == "1";
				}
				catch
				{
				}
				try
				{
					string text = SystemInfo.graphicsDeviceName ?? "";
					if (text.IndexOf("Custom GPU 0405", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Custom GPU 0932", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						flag = true;
					}
				}
				catch
				{
				}
				if (!flag && LinuxPad.Here)
				{
					try
					{
						string text2 = File.ReadAllText("/sys/devices/virtual/dmi/id/product_name").Trim();
						flag = text2 == "Jupiter" || text2 == "Galileo";
					}
					catch
					{
					}
				}
				_onDeck = flag;
				return flag;
			}
		}

		public static bool Held(KeyCode k)
		{
			if (Active)
			{
				return Now.Contains(k);
			}
			return false;
		}

		public static bool Down(KeyCode k)
		{
			if (Active && Now.Contains(k))
			{
				return !Before.Contains(k);
			}
			return false;
		}

		public static bool Up(KeyCode k)
		{
			if (Active && !Now.Contains(k))
			{
				return Before.Contains(k);
			}
			return false;
		}

		public static Vector2 TakeLook()
		{
			Vector2 look = _look;
			_look = Vector2.zero;
			return look;
		}

		public static int TakeStep()
		{
			int step = _step;
			_step = 0;
			return step;
		}

		public static int TakeTool()
		{
			int tool = _tool;
			_tool = 0;
			return tool;
		}

		public static Vector2Int TakeNav()
		{
			Vector2Int result = new Vector2Int(_navX, _navY);
			_navX = (_navY = 0);
			return result;
		}

		public static int TakeWheel()
		{
			int wheel = _wheel;
			_wheel = 0;
			return wheel;
		}

		public static void SwipeForTest(int step)
		{
			_tool += step;
		}

		public static void Tap(KeyCode k)
		{
			Tapped.Add(k);
		}

		public static void Click()
		{
			_clickQueued = true;
		}

		public static void Place(Vector2 pixel)
		{
			Cursor = pixel;
			CursorMoved = true;
			Driving = true;
		}

		public static void Update(Settings s, Mode mode, bool talk, float dt)
		{
			Before.Clear();
			Before.UnionWith(Now);
			Now.Clear();
			float unscaledTime = Time.unscaledTime;
			ReadPad(unscaledTime);
			Active = s.Controller && _pad.Connected && mode != Mode.Off;
			Kind = PicturesFor(s);
			if (!Active)
			{
				Move = (LeftStick = Vector2.zero);
				_look = Vector2.zero;
				_sprint = false;
				Driving = (CursorLeft = (CursorRight = false));
				Tapped.Clear();
				if (!s.Controller || !_pad.Connected)
				{
					Shown = false;
				}
				if (PadMenu.Open)
				{
					PadMenu.Hide();
				}
				return;
			}
			if (Used(_pad, _was))
			{
				_lastPadUse = unscaledTime;
			}
			if (RealUsed())
			{
				_lastRealUse = unscaledTime;
			}
			Shown = _lastPadUse >= _lastRealUse && _lastPadUse > 0f;
			foreach (KeyCode item in Tapped)
			{
				Now.Add(item);
			}
			Tapped.Clear();
			LeftStick = DeadZone(_pad.Left, 0.2f);
			_rt = (_rt ? (_pad.RightTrigger > 0.33f) : (_pad.RightTrigger > 0.5f));
			_lt = (_lt ? (_pad.LeftTrigger > 0.33f) : (_pad.LeftTrigger > 0.5f));
			if (mode != _modeWas)
			{
				Swallow();
			}
			_modeWas = mode;
			_swallowed &= _pad.Held;
			if (!_rt)
			{
				_swallowRt = false;
			}
			if (!_lt)
			{
				_swallowLt = false;
			}
			bool num = mode == Mode.Play;
			if (!num)
			{
				_tool = 0;
			}
			else if (Input.GetKeyDown(KeyCode.KeypadMultiply))
			{
				_tool++;
			}
			else if (Input.GetKeyDown(KeyCode.F4))
			{
				_tool--;
			}
			bool flag = num && Input.GetKeyDown(KeyCode.KeypadDivide);
			if (mode == Mode.Map)
			{
				UpdateMap(dt);
				return;
			}
			if (PadMenu.Open && mode != Mode.Play)
			{
				PadMenu.Hide();
			}
			if (mode == Mode.Menu)
			{
				UpdateMenu(talk, dt);
				return;
			}
			Driving = (CursorLeft = (CursorRight = false));
			if (flag)
			{
				if (PadMenu.Open)
				{
					PadMenu.Hide();
				}
				else
				{
					PadMenu.Show();
				}
			}
			if (Pressed(XInput.Buttons.Start))
			{
				_menuDownAt = unscaledTime;
				_menuHeldFired = false;
			}
			if (_pad.Has(XInput.Buttons.Start) && _menuDownAt >= 0f && !_menuHeldFired && unscaledTime - _menuDownAt >= 0.45f)
			{
				_menuHeldFired = true;
				if (!PadMenu.Open)
				{
					PadMenu.Show();
				}
			}
			if (Released(XInput.Buttons.Start))
			{
				if (!_menuHeldFired && _menuDownAt >= 0f)
				{
					if (PadMenu.Open)
					{
						PadMenu.Hide();
					}
					else
					{
						Now.Add(KeyCode.F2);
					}
				}
				_menuDownAt = -1f;
			}
			if (PadMenu.Open)
			{
				Move = Vector2.zero;
				_sprint = false;
				Nav(dt);
				Vector2Int vector2Int = TakeNav();
				if (vector2Int != Vector2Int.zero)
				{
					PadMenu.Hop(vector2Int);
				}
				if (Pressed(XInput.Buttons.A))
				{
					KeyCode keyCode = PadMenu.Close();
					if (keyCode != KeyCode.None)
					{
						Tap(keyCode);
					}
					Swallow();
				}
				if (Pressed(XInput.Buttons.B))
				{
					PadMenu.Hide();
					Swallow();
				}
				return;
			}
			Move = LeftStick;
			BackstepTap = false;
			if (Pressed(XInput.Buttons.LeftThumb))
			{
				if (Move.sqrMagnitude < 0.02f)
				{
					Tap(KeyCode.LeftControl);
				}
				else
				{
					_sprint = !_sprint;
				}
			}
			_still = ((Move.sqrMagnitude < 0.02f) ? (_still + dt) : 0f);
			if (_still > 0.15f)
			{
				_sprint = false;
			}
			if (_sprint)
			{
				Now.Add(KeyCode.LeftShift);
			}
			int num2;
			if (_pad.Left.y < -0.75f)
			{
				num2 = ((Mathf.Abs(_pad.Left.x) < 0.6f) ? 1 : 0);
				if (num2 != 0 && !_backWas)
				{
					if (_backTapped && unscaledTime - _backUpAt < 0.35f)
					{
						BackstepTap = true;
						_backTapped = false;
					}
					_backDownAt = unscaledTime;
				}
			}
			else
			{
				num2 = 0;
			}
			if (num2 == 0 && _backWas)
			{
				_backTapped = unscaledTime - _backDownAt < 0.3f;
				_backUpAt = unscaledTime;
			}
			_backWas = (byte)num2 != 0;
			Look(s, dt);
			if (Rt || On(XInput.Buttons.X))
			{
				Now.Add(KeyCode.Mouse0);
			}
			if (Lt)
			{
				Now.Add(KeyCode.Mouse1);
			}
			if (On(XInput.Buttons.A))
			{
				Now.Add(KeyCode.Space);
			}
			if (On(XInput.Buttons.B))
			{
				Now.Add(KeyCode.X);
			}
			if (On(XInput.Buttons.Y))
			{
				Now.Add(KeyCode.E);
			}
			if (On(XInput.Buttons.RightThumb))
			{
				Now.Add(KeyCode.C);
			}
			if (On(XInput.Buttons.Back))
			{
				Now.Add(KeyCode.Tab);
			}
			if (On(XInput.Buttons.DpadUp))
			{
				Now.Add(KeyCode.M);
			}
			if (On(XInput.Buttons.DpadDown))
			{
				Now.Add(KeyCode.J);
			}
			if (On(XInput.Buttons.DpadLeft))
			{
				Now.Add(KeyCode.N);
			}
			if (On(XInput.Buttons.DpadRight))
			{
				Now.Add(KeyCode.B);
			}
			if (Pressed(XInput.Buttons.RightShoulder))
			{
				_step++;
			}
			if (Pressed(XInput.Buttons.LeftShoulder))
			{
				_step--;
			}
		}

		private static void UpdateMenu(bool talk, float dt)
		{
			Move = Vector2.zero;
			_sprint = false;
			_menuDownAt = -1f;
			Nav(dt);
			Cursoring(dt);
			if (talk)
			{
				Vector2Int vector2Int = TakeNav();
				if (vector2Int.y < 0)
				{
					Now.Add(KeyCode.UpArrow);
				}
				if (vector2Int.y > 0)
				{
					Now.Add(KeyCode.DownArrow);
				}
				if (Pressed(XInput.Buttons.A) || Pressed(XInput.Buttons.Y))
				{
					Now.Add(KeyCode.Space);
				}
				if (Pressed(XInput.Buttons.B))
				{
					Now.Add(KeyCode.Backspace);
				}
				if (Pressed(XInput.Buttons.Start))
				{
					Now.Add(KeyCode.Escape);
				}
				CursorLeft = Driving && (Rt || On(XInput.Buttons.X));
				CursorRight = Driving && Lt;
				return;
			}
			CursorLeft = On(XInput.Buttons.A) || Rt || On(XInput.Buttons.X);
			CursorRight = Lt;
			if (_clickQueued)
			{
				CursorLeft = !_clickDown;
				_clickDown = !_clickDown;
				_clickQueued = _clickDown;
			}
			if (Pressed(XInput.Buttons.B))
			{
				Now.Add(KeyCode.Escape);
			}
			if (Pressed(XInput.Buttons.Start))
			{
				Now.Add(KeyCode.Escape);
			}
			if (On(XInput.Buttons.Back))
			{
				Now.Add(KeyCode.Tab);
			}
			if (On(XInput.Buttons.Y))
			{
				Now.Add(KeyCode.E);
			}
			Bumpers();
		}

		private static void UpdateMap(float dt)
		{
			Move = Vector2.zero;
			_sprint = false;
			_menuDownAt = -1f;
			Cursoring(dt);
			CursorLeft = Rt || On(XInput.Buttons.A);
			CursorRight = Lt;
			if (_clickQueued)
			{
				CursorLeft = !_clickDown;
				_clickDown = !_clickDown;
				_clickQueued = _clickDown;
			}
			if (CursorLeft || CursorRight)
			{
				Driving = true;
			}
			if (Pressed(XInput.Buttons.B))
			{
				Now.Add(KeyCode.Escape);
			}
			if (Pressed(XInput.Buttons.Start))
			{
				Now.Add(KeyCode.F2);
			}
			if (Pressed(XInput.Buttons.Back))
			{
				Now.Add(KeyCode.V);
			}
			int num = (_pad.Has(XInput.Buttons.RightShoulder) ? 1 : (_pad.Has(XInput.Buttons.LeftShoulder) ? (-1) : 0));
			float unscaledTime = Time.unscaledTime;
			if (num != 0 && (num != _bumpHeld || unscaledTime >= _bumpNext))
			{
				_wheel += num;
				_bumpNext = unscaledTime + ((num != _bumpHeld) ? 0.35f : 0.09f);
				Driving = true;
			}
			_bumpHeld = num;
		}

		private static void Bumpers()
		{
			if (Pressed(XInput.Buttons.RightShoulder))
			{
				_step++;
			}
			if (Pressed(XInput.Buttons.LeftShoulder))
			{
				_step--;
			}
		}

		private static void Nav(float dt)
		{
			int num = (_pad.Has(XInput.Buttons.DpadRight) ? 1 : (_pad.Has(XInput.Buttons.DpadLeft) ? (-1) : 0));
			int num2 = (_pad.Has(XInput.Buttons.DpadDown) ? 1 : (_pad.Has(XInput.Buttons.DpadUp) ? (-1) : 0));
			if (num == 0 && num2 == 0)
			{
				Vector2 left = _pad.Left;
				if (left.magnitude > 0.6f)
				{
					if (Mathf.Abs(left.x) > Mathf.Abs(left.y))
					{
						num = ((left.x > 0f) ? 1 : (-1));
					}
					else
					{
						num2 = ((!(left.y > 0f)) ? 1 : (-1));
					}
				}
			}
			float unscaledTime = Time.unscaledTime;
			bool flag = num != _navHeldX || num2 != _navHeldY;
			if ((num != 0 || num2 != 0) && (flag || unscaledTime >= _navNext))
			{
				_navX = num;
				_navY = num2;
				_navNext = unscaledTime + (flag ? 0.4f : 0.13f);
			}
			_navHeldX = num;
			_navHeldY = num2;
		}

		private static void Cursoring(float dt)
		{
			CursorMoved = false;
			Vector2 vector = DeadZone(_pad.Right, 0.15f);
			if (!(vector.sqrMagnitude <= 0f))
			{
				if (!Driving)
				{
					Cursor = new Vector2((float)Screen.width * 0.5f, (float)Screen.height * 0.5f);
				}
				float magnitude = vector.magnitude;
				float num = (float)Screen.height * (0.25f + 1.1f * magnitude * magnitude);
				Cursor += vector / magnitude * (magnitude * num * dt);
				Cursor.x = Mathf.Clamp(Cursor.x, 0f, (float)Screen.width - 1f);
				Cursor.y = Mathf.Clamp(Cursor.y, 0f, (float)Screen.height - 1f);
				CursorMoved = true;
				Driving = true;
			}
		}

		private static void Look(Settings s, float dt)
		{
			Vector2 right = _pad.Right;
			float magnitude = right.magnitude;
			if (magnitude <= 0.14f)
			{
				_edge = 0f;
				return;
			}
			float num = Mathf.Min(1f, (magnitude - 0.14f) / 0.86f);
			float num2 = 0.25f * num + 0.75f * num * num * num;
			_edge = ((num > 0.93f) ? (_edge + dt) : 0f);
			float num3 = 1f + Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.6f, _edge));
			float num4 = 170f * s.PadLook * num2 * num3 * LookScale;
			Vector2 vector = right / magnitude;
			_look.x += vector.x * num4 * dt;
			_look.y += vector.y * num4 * 0.7f * dt * (s.PadInvert ? (-1f) : 1f);
		}

		private static void ReadPad(float now)
		{
			_was = _pad;
			if (Simulated.HasValue)
			{
				_pad = Simulated.Value;
				return;
			}
			XInput.Poll(now);
			int num = -1;
			for (int i = 0; i < XInput.Count; i++)
			{
				if (XInput.Pad(i).Connected)
				{
					if (num < 0)
					{
						num = i;
					}
					if (i == _slot)
					{
						num = i;
					}
				}
			}
			for (int j = 0; j < XInput.Count; j++)
			{
				XInput.State state = XInput.Pad(j);
				if (state.Connected && j != _slot && (state.Held != 0 || state.Left.magnitude > 0.5f || state.Right.magnitude > 0.5f || state.RightTrigger > 0.5f))
				{
					num = j;
					break;
				}
			}
			if (num != _slot)
			{
				_was = default(XInput.State);
			}
			_slot = num;
			_pad = ((num >= 0) ? XInput.Pad(num) : default(XInput.State));
		}

		private static bool Pressed(XInput.Buttons b)
		{
			if (_pad.Has(b))
			{
				return !_was.Has(b);
			}
			return false;
		}

		private static bool On(XInput.Buttons b)
		{
			if (_pad.Has(b))
			{
				return (_swallowed & b) == 0;
			}
			return false;
		}

		private static void Swallow()
		{
			_swallowed = _pad.Held;
			_swallowRt = _rt;
			_swallowLt = _lt;
		}

		private static bool Released(XInput.Buttons b)
		{
			if (!_pad.Has(b))
			{
				return _was.Has(b);
			}
			return false;
		}

		private static bool Used(XInput.State p, XInput.State was)
		{
			if (p.Held == was.Held && !(p.Left.magnitude > 0.3f) && !(p.Right.magnitude > 0.3f) && !(p.LeftTrigger > 0.3f))
			{
				return p.RightTrigger > 0.3f;
			}
			return true;
		}

		private static bool RealUsed()
		{
			Vector3 mousePosition = Input.mousePosition;
			CursorLockMode lockState = UnityEngine.Cursor.lockState;
			float unscaledTime = Time.unscaledTime;
			if (lockState != _lockWas)
			{
				_lockChangedAt = unscaledTime;
			}
			_lockWas = lockState;
			bool flag = unscaledTime - _lockChangedAt > 0.3f;
			bool num = flag && _realMouseSeen && lockState == CursorLockMode.None && !VrMouse.Driving && (mousePosition - _realMouseWas).sqrMagnitude > 64f;
			_realMouseWas = mousePosition;
			_realMouseSeen = true;
			if (num || (!VrMouse.Driving && Input.mouseScrollDelta.y != 0f))
			{
				return true;
			}
			if (flag)
			{
				try
				{
					if (Mathf.Abs(Input.GetAxisRaw("Mouse X")) + Mathf.Abs(Input.GetAxisRaw("Mouse Y")) > 0.5f)
					{
						return true;
					}
				}
				catch (ArgumentException)
				{
				}
			}
			if (!VrMouse.Driving && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
			{
				return true;
			}
			if (!Input.anyKeyDown)
			{
				return false;
			}
			if (_keyboardKeys == null)
			{
				List<KeyCode> list = new List<KeyCode>();
				foreach (KeyCode value in Enum.GetValues(typeof(KeyCode)))
				{
					if (value > KeyCode.None && value < KeyCode.Mouse0 && Array.IndexOf(LayoutKeys, value) < 0)
					{
						list.Add(value);
					}
				}
				_keyboardKeys = list.ToArray();
			}
			KeyCode[] keyboardKeys = _keyboardKeys;
			for (int i = 0; i < keyboardKeys.Length; i++)
			{
				if (Input.GetKeyDown(keyboardKeys[i]))
				{
					return true;
				}
			}
			return false;
		}

		private static Pictures PicturesFor(Settings s)
		{
			if (s.PadPictures == 1)
			{
				return Pictures.Deck;
			}
			if (s.PadPictures == 2)
			{
				return Pictures.Xbox;
			}
			if (s.PadPictures == 3)
			{
				return Pictures.PlayStation;
			}
			Pictures result = (OnDeck ? Pictures.Deck : (PlayStationNamed() ? Pictures.PlayStation : Pictures.Xbox));
			if (!_loggedKind && Active)
			{
				_loggedKind = true;
				Log.Info("pad: button pictures " + result.ToString() + (OnDeck ? " (Steam Deck)" : ""));
			}
			return result;
		}

		private static bool PlayStationNamed()
		{
			float unscaledTime = Time.unscaledTime;
			if (unscaledTime - _namesAt < 5f)
			{
				return _playStation;
			}
			_namesAt = unscaledTime;
			_playStation = false;
			try
			{
				string[] joystickNames = Input.GetJoystickNames();
				for (int i = 0; i < joystickNames.Length; i++)
				{
					string text = (joystickNames[i] ?? "").ToLowerInvariant();
					if (text.Contains("dualsense") || text.Contains("dualshock") || text.Contains("wireless controller") || text.Contains("playstation"))
					{
						_playStation = true;
						break;
					}
				}
			}
			catch
			{
			}
			return _playStation;
		}

		private static Vector2 DeadZone(Vector2 v, float dead)
		{
			float magnitude = v.magnitude;
			if (magnitude <= dead)
			{
				return Vector2.zero;
			}
			return v / magnitude * Mathf.Min(1f, (magnitude - dead) / (1f - dead));
		}

		public static string Describe()
		{
			return "xinput " + XInput.Library + (OnDeck ? ", Steam Deck" : "") + (_pad.Connected ? (", a pad in slot " + _slot) : ", no pad");
		}
	}
}
