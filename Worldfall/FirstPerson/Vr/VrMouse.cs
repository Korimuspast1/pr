using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace FirstPerson.Vr
{
	internal static class VrMouse
	{
		public struct Laser
		{
			public bool InPlay;

			public bool OnScreen;

			public Vector2 Pixel;

			public bool Moved;

			public bool Left;

			public bool Right;

			public bool Middle;

			public float Scroll;

			public bool Gui;
		}

		public delegate void AimFn(ref Laser laser);

		private struct Point
		{
			public int X;

			public int Y;
		}

		private delegate bool EnumProc(IntPtr window, IntPtr data);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate byte ButtonFn(int button);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void Vector3Fn(out Vector3 value);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void Vector2Fn(out Vector2 value);

		public static AimFn Aim;

		public static readonly Vector2 Away = new Vector2(-4000f, -4000f);

		private static Laser _now;

		private static Laser _was;

		private static int _frame = -1;

		private static int _guiFrame = -1;

		private static bool _driving;

		private static bool _aimFailed;

		private static float _laserAt = -1f;

		private static float _realAt = -1f;

		private static Vector3 _realLast;

		private static bool _realSeen;

		public static int GuiPresses;

		public static bool IgnoreRealMouse;

		private static IntPtr _window;

		private static bool _windowLooked;

		private static readonly bool[] _told = new bool[3];

		private static int _toldUntil = -1;

		private static Vector2 _toldAt;

		private const uint WmMouseMove = 512u;

		private const uint WmMouseWheel = 522u;

		private static readonly uint[] WmDown = new uint[3] { 513u, 516u, 519u };

		private static readonly uint[] WmUp = new uint[3] { 514u, 517u, 520u };

		private static readonly int[] MkButton = new int[3] { 1, 2, 16 };

		private static bool _patched;

		private static bool _tried;

		private static int _answered;

		private static readonly List<Detour.Handle> _handles = new List<Detour.Handle>();

		public static string Error;

		private static ButtonFn _button;

		private static ButtonFn _down;

		private static ButtonFn _up;

		private static Vector3Fn _position;

		private static Vector2Fn _scroll;

		[ThreadStatic]
		private static int _depth;

		private static bool _reentered;

		public static bool Driving
		{
			get
			{
				Sample();
				return _driving;
			}
		}

		public static Laser Now
		{
			get
			{
				Sample();
				return _now;
			}
		}

		public static Vector2 Pixel
		{
			get
			{
				Sample();
				if (!_now.OnScreen)
				{
					return Away;
				}
				return _now.Pixel;
			}
		}

		public static Vector2 GuiPoint
		{
			get
			{
				Sample();
				if (!_now.OnScreen)
				{
					return Away;
				}
				return new Vector2(_now.Pixel.x, (float)Screen.height - _now.Pixel.y);
			}
		}

		public static bool Posts
		{
			get
			{
				if (_patched)
				{
					return Window() != IntPtr.Zero;
				}
				return false;
			}
		}

		public static bool Patched => _patched;

		public static void Sample()
		{
			int frameCount = Time.frameCount;
			if (frameCount == _frame)
			{
				return;
			}
			_frame = frameCount;
			_was = _now;
			_now = default(Laser);
			try
			{
				Aim?.Invoke(ref _now);
			}
			catch (Exception ex)
			{
				_now = default(Laser);
				if (!_aimFailed)
				{
					Log.Error("vr: the laser: " + ex);
				}
				_aimFailed = true;
			}
			float unscaledTime = Time.unscaledTime;
			if (_now.InPlay && (!_was.InPlay || _now.Left || _now.Right || _now.Middle || _now.Scroll != 0f || _now.Moved || _now.OnScreen != _was.OnScreen || (_now.OnScreen && (_now.Pixel - _was.Pixel).sqrMagnitude > 64f)))
			{
				_laserAt = unscaledTime;
			}
			if (_patched)
			{
				Vector3 vector = RealPosition();
				bool flag = Application.isFocused && !IgnoreRealMouse && frameCount > _toldUntil;
				if (!_realSeen || !flag)
				{
					_realLast = vector;
				}
				else if ((vector - _realLast).sqrMagnitude > 400f || (RealButton(0) && !_told[0]) || (RealButton(1) && !_told[1]) || (RealButton(2) && !_told[2]))
				{
					_realAt = unscaledTime;
					_realLast = vector;
				}
				if (_laserAt == unscaledTime)
				{
					_realLast = vector;
				}
				_realSeen = true;
			}
			bool flag2 = _driving && (_was.Left || _was.Right || _was.Middle);
			bool flag3 = _now.InPlay && _laserAt >= 0f && (flag2 || _laserAt >= _realAt);
			if (!flag3 && flag2)
			{
				_now = _was;
				_now.Left = (_now.Right = (_now.Middle = false));
				_now.Scroll = 0f;
				_now.Moved = false;
				_driving = true;
				if (_patched)
				{
					Tell();
				}
			}
			else
			{
				_driving = flag3;
				if (!_driving)
				{
					_now.Left = (_now.Right = (_now.Middle = false));
					_now.Scroll = 0f;
				}
				if (_patched)
				{
					Tell();
				}
			}
		}

		private static void Tell()
		{
			IntPtr intPtr = Window();
			if (intPtr == IntPtr.Zero)
			{
				return;
			}
			Vector2 vector = (_now.OnScreen ? new Vector2(_now.Pixel.x, (float)Screen.height - _now.Pixel.y) : _toldAt);
			int num = Mathf.Clamp(Mathf.RoundToInt(vector.x), 0, 32767);
			int num2 = Mathf.Clamp(Mathf.RoundToInt(vector.y), 0, 32767);
			IntPtr lParam = new IntPtr((num2 << 16) | (num & 0xFFFF));
			int num3 = 0;
			bool flag = false;
			for (int i = 0; i < 3; i++)
			{
				bool flag2 = _driving && Held(i) && (_told[i] || (_now.OnScreen && _now.Gui));
				if (flag2 != _told[i])
				{
					if (flag2)
					{
						GuiPresses++;
					}
					_told[i] = flag2;
					flag = true;
					PostMessage(intPtr, flag2 ? WmDown[i] : WmUp[i], new IntPtr(Flags()), lParam);
				}
				if (_told[i])
				{
					num3 |= MkButton[i];
				}
			}
			if (!flag && num3 != 0 && (vector - _toldAt).sqrMagnitude > 0.25f)
			{
				PostMessage(intPtr, 512u, new IntPtr(num3), lParam);
				flag = true;
			}
			if (_driving && _now.OnScreen && _now.Scroll != 0f)
			{
				Point point = new Point
				{
					X = num,
					Y = num2
				};
				ClientToScreen(intPtr, ref point);
				int num4 = Mathf.RoundToInt(_now.Scroll * 120f);
				PostMessage(intPtr, 522u, new IntPtr((num4 << 16) | num3), new IntPtr((point.Y << 16) | (point.X & 0xFFFF)));
				flag = true;
			}
			if (flag)
			{
				_toldAt = vector;
				_toldUntil = _frame + 3;
			}
		}

		private static int Flags()
		{
			int num = 0;
			for (int i = 0; i < 3; i++)
			{
				if (_told[i])
				{
					num |= MkButton[i];
				}
			}
			return num;
		}

		private static IntPtr Window()
		{
			if (_windowLooked)
			{
				return _window;
			}
			_windowLooked = true;
			try
			{
				uint me = (uint)Process.GetCurrentProcess().Id;
				StringBuilder name = new StringBuilder(64);
				EnumWindows(delegate(IntPtr h, IntPtr _)
				{
					GetWindowThreadProcessId(h, out var process);
					if (process != me)
					{
						return true;
					}
					name.Length = 0;
					GetClassName(h, name, name.Capacity);
					if (name.ToString() != "UnityWndClass")
					{
						return true;
					}
					_window = h;
					return false;
				}, IntPtr.Zero);
			}
			catch (Exception ex)
			{
				Log.Error("vr: the game's window: " + ex.Message);
			}
			Log.Info((_window != IntPtr.Zero) ? "vr: the laser's clicks go to the mod's windows through the game's window" : "vr: the game's window wasn't found: the laser's clicks are played to the mod's windows");
			return _window;
		}

		[DllImport("user32.dll")]
		private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll")]
		private static extern bool EnumWindows(EnumProc proc, IntPtr data);

		[DllImport("user32.dll")]
		private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern int GetClassName(IntPtr window, StringBuilder name, int size);

		[DllImport("user32.dll")]
		private static extern bool ClientToScreen(IntPtr window, ref Point point);

		private static bool Held(int b)
		{
			switch (b)
			{
			case 2:
				return _now.Middle;
			default:
				return false;
			case 1:
				return _now.Right;
			case 0:
				return _now.Left;
			}
		}

		private static bool HeldBefore(int b)
		{
			switch (b)
			{
			case 2:
				return _was.Middle;
			default:
				return false;
			case 1:
				return _was.Right;
			case 0:
				return _was.Left;
			}
		}

		public static void GuiEvents(List<Event> into)
		{
			into.Clear();
			Sample();
			if (!_driving || _guiFrame == _frame)
			{
				return;
			}
			_guiFrame = _frame;
			Vector2 guiPoint = GuiPoint;
			Vector2 vector = (_was.OnScreen ? new Vector2(_was.Pixel.x, (float)Screen.height - _was.Pixel.y) : guiPoint);
			bool flag = (guiPoint - vector).sqrMagnitude > 0.25f;
			for (int i = 0; i < 3; i++)
			{
				bool flag2 = Held(i);
				bool flag3 = HeldBefore(i);
				if (flag2 && !flag3)
				{
					into.Add(new Event
					{
						type = EventType.MouseDown,
						button = i,
						mousePosition = guiPoint,
						clickCount = 1
					});
				}
				else if (flag2 && flag)
				{
					into.Add(new Event
					{
						type = EventType.MouseDrag,
						button = i,
						mousePosition = guiPoint,
						delta = guiPoint - vector
					});
				}
				else if (!flag2 && flag3)
				{
					into.Add(new Event
					{
						type = EventType.MouseUp,
						button = i,
						mousePosition = guiPoint,
						clickCount = 1
					});
				}
			}
			foreach (Event item in into)
			{
				if (item.type == EventType.MouseDown)
				{
					GuiPresses++;
				}
			}
			if (_now.Scroll != 0f)
			{
				into.Add(new Event
				{
					type = EventType.ScrollWheel,
					mousePosition = guiPoint,
					delta = new Vector2(0f, -3f * _now.Scroll)
				});
			}
			if (into.Count == 0 && flag && _now.OnScreen)
			{
				into.Add(new Event
				{
					type = EventType.MouseMove,
					mousePosition = guiPoint,
					delta = guiPoint - vector
				});
			}
		}

		[DllImport("mono-2.0-bdwgc", EntryPoint = "mono_lookup_internal_call")]
		private static extern IntPtr LookupInternalCall(IntPtr method);

		public static bool Install()
		{
			if (_tried)
			{
				return _patched;
			}
			_tried = true;
			if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor)
			{
				Error = "not on Windows";
				return false;
			}
			Type typeFromHandle = typeof(Input);
			Type typeFromHandle2 = typeof(VrMouse);
			try
			{
				MethodInfo method = typeFromHandle.GetMethod("GetMouseButton", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(int) }, null);
				MethodInfo method2 = typeFromHandle.GetMethod("GetMouseButtonDown", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(int) }, null);
				MethodInfo method3 = typeFromHandle.GetMethod("GetMouseButtonUp", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(int) }, null);
				MethodInfo method4 = typeFromHandle.GetMethod("get_mousePosition_Injected", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				MethodInfo method5 = typeFromHandle.GetMethod("get_mouseScrollDelta_Injected", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (method == null || method2 == null || method3 == null || method4 == null || method5 == null)
				{
					throw new Exception("this engine's Input has no such calls");
				}
				_button = Native<ButtonFn>(method, out var fn);
				_down = Native<ButtonFn>(method2, out var fn2);
				_up = Native<ButtonFn>(method3, out var fn3);
				_position = Native<Vector3Fn>(method4, out var fn4);
				_scroll = Native<Vector2Fn>(method5, out var fn5);
				if (Detour.Hooker == null)
				{
					(MethodInfo, IntPtr)[] array = new(MethodInfo, IntPtr)[5]
					{
						(method, fn),
						(method2, fn2),
						(method3, fn3),
						(method4, fn4),
						(method5, fn5)
					};
					for (int i = 0; i < array.Length; i++)
					{
						var (methodInfo, intPtr) = array[i];
						if (Detour.Target(methodInfo) == intPtr)
						{
							throw new Exception("Input." + methodInfo.Name + " has no code of its own to patch");
						}
					}
				}
				if (!Detour.Begin())
				{
					throw new Exception("patching skipped after a crash on this platform");
				}
				try
				{
					_handles.Add(Detour.Apply(method, typeFromHandle2.GetMethod("GetMouseButton", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)));
					_handles.Add(Detour.Apply(method2, typeFromHandle2.GetMethod("GetMouseButtonDown", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)));
					_handles.Add(Detour.Apply(method3, typeFromHandle2.GetMethod("GetMouseButtonUp", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)));
					_handles.Add(Detour.Apply(method4, typeFromHandle2.GetMethod("MousePosition", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)));
					_handles.Add(Detour.Apply(method5, typeFromHandle2.GetMethod("ScrollDelta", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)));
				}
				finally
				{
					Detour.End();
				}
				_patched = true;
				int answered = _answered;
				Ask();
				if (_answered == answered)
				{
					throw new Exception("Input's calls don't come through the patch (the engine calls them directly)");
				}
				_reentered = false;
				_depth++;
				try
				{
					_button(0);
					_position(out var _);
				}
				finally
				{
					_depth--;
				}
				if (_reentered)
				{
					throw new Exception("the engine's own mouse functions were patched too");
				}
				Log.Info("vr: the controllers' laser works the game's mouse");
			}
			catch (Exception ex)
			{
				Error = ex.Message;
				Log.Error("vr: the laser can't work the game's own mouse, only the mod's windows: " + ex);
				foreach (Detour.Handle handle in _handles)
				{
					try
					{
						Detour.Undo(handle);
					}
					catch
					{
					}
				}
				_handles.Clear();
				_patched = false;
			}
			return _patched;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void Ask()
		{
			Input.GetMouseButton(0);
			_ = Input.mousePosition;
		}

		private static T Native<T>(MethodInfo m, out IntPtr fn) where T : class
		{
			fn = LookupInternalCall(m.MethodHandle.Value);
			if (fn == IntPtr.Zero)
			{
				throw new Exception("the engine's function for Input." + m.Name + " wasn't found");
			}
			return Marshal.GetDelegateForFunctionPointer(fn, typeof(T)) as T;
		}

		private static bool RealButton(int b)
		{
			if (_button != null)
			{
				return _button(b) != 0;
			}
			return false;
		}

		private static Vector3 RealPosition()
		{
			if (_position == null)
			{
				return Vector3.zero;
			}
			_position(out var value);
			return value;
		}

		private static bool GetMouseButton(int button)
		{
			if (_depth > 0)
			{
				_reentered = true;
				return false;
			}
			_depth++;
			try
			{
				_answered++;
				Sample();
				if (_driving && button >= 0 && button <= 2)
				{
					return Held(button);
				}
				return _button(button) != 0;
			}
			finally
			{
				_depth--;
			}
		}

		private static bool GetMouseButtonDown(int button)
		{
			if (_depth > 0)
			{
				return false;
			}
			_depth++;
			try
			{
				_answered++;
				Sample();
				if (_driving && button >= 0 && button <= 2)
				{
					return Held(button) && !HeldBefore(button);
				}
				return _down(button) != 0;
			}
			finally
			{
				_depth--;
			}
		}

		private static bool GetMouseButtonUp(int button)
		{
			if (_depth > 0)
			{
				return false;
			}
			_depth++;
			try
			{
				_answered++;
				Sample();
				if (_driving && button >= 0 && button <= 2)
				{
					return !Held(button) && HeldBefore(button);
				}
				return _up(button) != 0;
			}
			finally
			{
				_depth--;
			}
		}

		private static void MousePosition(out Vector3 ret)
		{
			ret = Vector3.zero;
			if (_depth > 0)
			{
				_reentered = true;
				return;
			}
			_depth++;
			try
			{
				_answered++;
				Sample();
				if (_driving)
				{
					Vector2 vector = (_now.OnScreen ? _now.Pixel : Away);
					ret = new Vector3(vector.x, vector.y, 0f);
				}
				else
				{
					_position(out ret);
				}
			}
			finally
			{
				_depth--;
			}
		}

		private static void ScrollDelta(out Vector2 ret)
		{
			ret = Vector2.zero;
			if (_depth > 0)
			{
				return;
			}
			_depth++;
			try
			{
				_answered++;
				Sample();
				if (_driving)
				{
					ret = new Vector2(0f, _now.Scroll);
				}
				else
				{
					_scroll(out ret);
				}
			}
			finally
			{
				_depth--;
			}
		}
	}
}
