using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal static class TouchControls
	{
		internal sealed class Button
		{
			public string Name;

			public string Icon;

			public KeyCode Key;

			public Action Act;

			public Vector2 C;

			public float R;

			public bool Visible;

			public bool Pressed;

			public bool Lit;

			public bool Turns;

			public bool Hit(Vector2 p)
			{
				if (Visible)
				{
					return (p - C).sqrMagnitude <= R * R * 1.25f;
				}
				return false;
			}
		}

		internal sealed class Tile
		{
			public string Name;

			public string Icon;

			public KeyCode Key;

			public Func<Action> Act;

			public Rect Box;
		}

		private enum Role
		{
			Stick,
			Look,
			Button,
			Tile,
			Ignore
		}

		private sealed class Finger
		{
			public int Id;

			public Vector2 Pos;

			public Role Role;

			public Button Button;

			public Tile Tile;

			public bool Seen;
		}

		public static bool Shown;

		public static bool MenuMode;

		public static bool PointerMode;

		public static float LookSpeed = 1f;

		public static bool CanUse;

		public static bool HasAbility;

		public static string RAction;

		public static Action Back;

		public static Action LetGo;

		public static float TopRightFloor;

		internal static readonly Button Attack = new Button
		{
			Name = "Attack",
			Icon = "iconAttack",
			Key = KeyCode.Mouse0,
			Turns = true
		};

		internal static readonly Button Jump = new Button
		{
			Name = "Jump",
			Key = KeyCode.Space
		};

		internal static readonly Button Guard = new Button
		{
			Name = "Guard",
			Key = KeyCode.Mouse1
		};

		internal static readonly Button Use = new Button
		{
			Name = "Use",
			Key = KeyCode.E
		};

		internal static readonly Button Ability = new Button
		{
			Name = "Ability",
			Icon = "iconMana",
			Key = KeyCode.X
		};

		internal static readonly Button Context = new Button
		{
			Name = "R",
			Key = KeyCode.R
		};

		internal static readonly Button More = new Button
		{
			Name = "More",
			Icon = "iconOptions"
		};

		internal static readonly Button BackButton = new Button
		{
			Name = "Back",
			Icon = "iconArrowBack"
		};

		internal static readonly Button[] Buttons = new Button[8] { Attack, Jump, Guard, Use, Ability, Context, More, BackButton };

		internal static readonly Tile[] Tiles = new Tile[18]
		{
			new Tile
			{
				Name = "Inventory",
				Icon = "iconCityInventory",
				Key = KeyCode.Tab
			},
			new Tile
			{
				Name = "Binoculars",
				Icon = "iconInspect",
				Key = KeyCode.Z
			},
			new Tile
			{
				Name = "Minimap",
				Icon = "iconWorldInfo",
				Key = KeyCode.N
			},
			new Tile
			{
				Name = "Ping",
				Icon = "iconArrowDestination",
				Key = KeyCode.C
			},
			new Tile
			{
				Name = "Crouch",
				Icon = "iconArrowDown",
				Key = KeyCode.LeftControl
			},
			new Tile
			{
				Name = "Insult",
				Icon = "iconSwearing",
				Act = () => WorldBoxMod.MenuInsult
			},
			new Tile
			{
				Name = "Steal",
				Icon = "iconMoney",
				Act = () => WorldBoxMod.MenuSteal
			},
			new Tile
			{
				Name = "Take over",
				Icon = "iconFavoriteStar_Map",
				Act = () => WorldBoxMod.MenuTakeOver
			},
			new Tile
			{
				Name = "Put away",
				Icon = "iconCrystalSword",
				Act = () => delegate
				{
					WorldBoxMod.MenuTool(WorldBoxMod.Tool.PutAway);
				}
			},
			new Tile
			{
				Name = "Eat",
				Icon = "iconHunger",
				Act = () => delegate
				{
					WorldBoxMod.MenuTool(WorldBoxMod.Tool.Eat);
				}
			},
			new Tile
			{
				Name = "Torch",
				Icon = "iconDivineLight",
				Act = () => delegate
				{
					WorldBoxMod.MenuTool(WorldBoxMod.Tool.Torch);
				}
			},
			new Tile
			{
				Name = "Compass",
				Icon = "iconArrowDestination",
				Act = () => delegate
				{
					WorldBoxMod.MenuTool(WorldBoxMod.Tool.Compass);
				}
			},
			new Tile
			{
				Name = "Star",
				Icon = "iconFavoriteStar",
				Key = KeyCode.B
			},
			new Tile
			{
				Name = "Map",
				Icon = "iconWorldInfo",
				Key = KeyCode.M
			},
			new Tile
			{
				Name = "Plan",
				Icon = "iconBuildings",
				Key = KeyCode.Y
			},
			new Tile
			{
				Name = "Bandage",
				Icon = "iconHealth",
				Act = () => delegate
				{
					WorldBoxMod.MenuTool(WorldBoxMod.Tool.Bandage);
				}
			},
			new Tile
			{
				Name = "Settings",
				Icon = "iconOptions",
				Key = KeyCode.F2
			},
			new Tile
			{
				Name = "Let go",
				Icon = "iconPossessed",
				Act = () => LetGo
			}
		};

		internal static Rect Panel;

		internal static Vector2 StickRest;

		internal static float StickRadius;

		internal static float KnobRadius;

		internal static Vector2 StickBase;

		internal static Vector2 StickKnob;

		internal static bool StickHeld;

		internal static bool LockLit;

		internal static Rect StickZone;

		private static float _laidW;

		private static float _laidH;

		private static float _laidFloor;

		private static readonly List<Finger> _fingers = new List<Finger>();

		private static readonly HashSet<KeyCode> _held = new HashSet<KeyCode>();

		private static readonly HashSet<KeyCode> _was = new HashSet<KeyCode>();

		private static readonly List<KeyCode> _pulse = new List<KeyCode>();

		private static Vector2 _move;

		private static Vector2 _look;

		private static bool _sprint;

		private static bool _autoRun;

		private static bool _moreOpen;

		private static int _polled = -1;

		private static Action _pending;

		private static readonly Dictionary<int, Vector2> _scripted = new Dictionary<int, Vector2>();

		private static bool _scriptedLast;

		public static Vector2 Move
		{
			get
			{
				Poll();
				return _move;
			}
		}

		public static bool Sprint
		{
			get
			{
				Poll();
				return _sprint;
			}
		}

		public static Vector2 Look
		{
			get
			{
				Poll();
				return _look;
			}
		}

		public static bool AutoRun => _autoRun;

		public static bool MoreOpen => _moreOpen;

		internal static bool ScriptedForTest => _scripted.Count > 0;

		public static bool Held(KeyCode k)
		{
			Poll();
			return _held.Contains(k);
		}

		public static bool Down(KeyCode k)
		{
			Poll();
			if (_held.Contains(k))
			{
				return !_was.Contains(k);
			}
			return false;
		}

		public static bool Up(KeyCode k)
		{
			Poll();
			if (!_held.Contains(k))
			{
				return _was.Contains(k);
			}
			return false;
		}

		private static void Lay(float w, float h)
		{
			if (w != _laidW || h != _laidH || TopRightFloor != _laidFloor)
			{
				_laidW = w;
				_laidH = h;
				_laidFloor = TopRightFloor;
				float num = 0.035f * h;
				float num2 = (Device.OnPhone ? (0.025f * w) : 0f);
				Rect safeArea = Screen.safeArea;
				if (Device.Native && safeArea.width > 0f)
				{
					num2 = Mathf.Max(num2, safeArea.x, w - safeArea.xMax);
				}
				float num3 = 0.905f * h;
				StickRadius = 0.13f * h;
				KnobRadius = 0.058f * h;
				StickRest = new Vector2(num2 + num + 0.19f * h, num3 - 0.21f * h);
				StickZone = Rect.MinMaxRect(0f, 0.3f * h, 0.45f * w, h);
				Attack.R = 0.12f * h;
				Attack.C = new Vector2(w - num2 - num - 0.17f * h, num3 - 0.21f * h);
				Place(Jump, 195f, 0.27f, 0.07f, h);
				Place(Guard, 152f, 0.27f, 0.065f, h);
				Place(Use, 112f, 0.27f, 0.07f, h);
				Place(Ability, 72f, 0.27f, 0.07f, h);
				Place(Context, 132f, 0.47f, 0.06f, h);
				if (Context.C.y - Context.R < TopRightFloor + num)
				{
					Context.C.y = TopRightFloor + num + Context.R;
				}
				More.R = (BackButton.R = 0.055f * h);
				More.C = new Vector2(num2 + num + More.R, 0.2f * h + More.R);
				BackButton.C = More.C;
				int num4 = (Tiles.Length + 7 - 1) / 7;
				float num5 = 0.17f * h;
				float num6 = 0.02f * h;
				float num7 = 7f * num5 + 6f * num6;
				float num8 = (float)num4 * num5 + (float)(num4 - 1) * num6;
				Panel = new Rect((w - num7) * 0.5f, (h - num8) * 0.5f, num7, num8);
				for (int i = 0; i < Tiles.Length; i++)
				{
					Tiles[i].Box = new Rect(Panel.x + (float)(i % 7) * (num5 + num6), Panel.y + (float)(i / 7) * (num5 + num6), num5, num5);
				}
			}
		}

		private static void Place(Button b, float degrees, float distance, float radius, float h)
		{
			float f = degrees * (MathF.PI / 180f);
			b.R = radius * h;
			b.C = Attack.C + new Vector2(Mathf.Cos(f), 0f - Mathf.Sin(f)) * distance * h;
		}

		internal static void TouchForTest(int id, Vector2 gui)
		{
			_scripted[id] = gui;
		}

		internal static void LiftForTest(int id)
		{
			_scripted.Remove(id);
		}

		internal static void LiftAllForTest()
		{
			_scripted.Clear();
		}

		internal static void PulseForTest(params KeyCode[] keys)
		{
			_pulse.AddRange(keys);
		}

		private static void Poll()
		{
			if (Time.frameCount == _polled)
			{
				return;
			}
			_polled = Time.frameCount;
			_was.Clear();
			foreach (KeyCode item in _held)
			{
				_was.Add(item);
			}
			_held.Clear();
			_look = Vector2.zero;
			_move = Vector2.zero;
			_sprint = false;
			foreach (KeyCode item2 in _pulse)
			{
				_held.Add(item2);
			}
			_pulse.Clear();
			float num = Screen.width;
			float num2 = Screen.height;
			Button[] buttons;
			if (!Shown || num < 16f || num2 < 16f)
			{
				_fingers.Clear();
				_autoRun = false;
				_moreOpen = false;
				StickHeld = (LockLit = false);
				buttons = Buttons;
				for (int i = 0; i < buttons.Length; i++)
				{
					buttons[i].Pressed = false;
				}
				return;
			}
			Lay(num, num2);
			Attack.Visible = (Jump.Visible = (Guard.Visible = (Use.Visible = !MenuMode && !_moreOpen)));
			Ability.Visible = HasAbility && !MenuMode && !_moreOpen;
			Context.Visible = RAction != null && !MenuMode && !_moreOpen;
			Context.Name = RAction ?? "R";
			Use.Lit = CanUse;
			More.Visible = !MenuMode && !_moreOpen;
			BackButton.Visible = MenuMode;
			if (MenuMode)
			{
				_moreOpen = false;
			}
			foreach (Finger finger2 in _fingers)
			{
				finger2.Seen = false;
			}
			ReadFingers(num2);
			for (int num3 = _fingers.Count - 1; num3 >= 0; num3--)
			{
				Finger finger = _fingers[num3];
				if (!finger.Seen)
				{
					Lift(finger);
					_fingers.RemoveAt(num3);
				}
			}
			buttons = Buttons;
			for (int i = 0; i < buttons.Length; i++)
			{
				buttons[i].Pressed = false;
			}
			StickHeld = false;
			foreach (Finger finger3 in _fingers)
			{
				if (finger3.Role == Role.Button && finger3.Button != null && finger3.Button.Visible)
				{
					finger3.Button.Pressed = true;
					if (finger3.Button.Key != KeyCode.None)
					{
						_held.Add(finger3.Button.Key);
					}
				}
				else if (finger3.Role == Role.Stick)
				{
					Steer(finger3);
				}
			}
			if (!StickHeld)
			{
				LockLit = false;
			}
			if (_autoRun && !MenuMode && !StickHeld)
			{
				_move = new Vector2(0f, 1f);
			}
			if (_sprint)
			{
				_held.Add(KeyCode.LeftShift);
			}
			if (MenuMode)
			{
				_held.Clear();
				_look = Vector2.zero;
				_move = Vector2.zero;
			}
		}

		internal static void RunPending()
		{
			Poll();
			if (_pending == null)
			{
				return;
			}
			Action pending = _pending;
			_pending = null;
			try
			{
				pending();
			}
			catch (Exception ex)
			{
				Log.Error("touch controls: " + ex.Message);
			}
		}

		private static void ReadFingers(float h)
		{
			if (_scripted.Count > 0 || _scriptedLast)
			{
				_scriptedLast = _scripted.Count > 0;
				{
					foreach (KeyValuePair<int, Vector2> item in _scripted)
					{
						See(item.Key, item.Value, h);
					}
					return;
				}
			}
			if (!PointerMode)
			{
				int touchCount = Input.touchCount;
				for (int i = 0; i < touchCount; i++)
				{
					Touch touch = Input.GetTouch(i);
					if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
					{
						See(touch.fingerId, new Vector2(touch.position.x, (float)Screen.height - touch.position.y), h);
					}
				}
			}
			else if (Input.GetMouseButton(0))
			{
				Vector3 mousePosition = Input.mousePosition;
				See(-1, new Vector2(mousePosition.x, (float)Screen.height - mousePosition.y), h);
			}
		}

		private static void See(int id, Vector2 p, float h)
		{
			Finger finger = _fingers.Find((Finger x) => x.Id == id);
			if (finger == null)
			{
				finger = new Finger
				{
					Id = id,
					Pos = p
				};
				Begin(finger, p);
				_fingers.Add(finger);
			}
			Vector2 vector = p - finger.Pos;
			finger.Pos = p;
			finger.Seen = true;
			if ((finger.Role == Role.Look || (finger.Role == Role.Button && finger.Button != null && finger.Button.Turns)) && !MenuMode)
			{
				float num = 180f * LookSpeed / Mathf.Max(1f, h);
				_look += new Vector2(vector.x * num, (0f - vector.y) * num);
			}
		}

		private static void Begin(Finger f, Vector2 p)
		{
			if (_moreOpen)
			{
				f.Role = Role.Tile;
				f.Tile = null;
				Tile[] tiles = Tiles;
				foreach (Tile tile in tiles)
				{
					if (tile.Box.Contains(p))
					{
						f.Tile = tile;
					}
				}
				return;
			}
			Button[] buttons = Buttons;
			foreach (Button button in buttons)
			{
				if (button.Hit(p))
				{
					f.Role = Role.Button;
					f.Button = button;
					if (button == More)
					{
						_moreOpen = true;
						f.Role = Role.Ignore;
					}
					else if (button == BackButton)
					{
						_pending = Back;
						f.Role = Role.Ignore;
					}
					return;
				}
			}
			if (MenuMode)
			{
				f.Role = Role.Ignore;
			}
			else if (StickZone.Contains(p) && !_fingers.Exists((Finger x) => x.Role == Role.Stick))
			{
				f.Role = Role.Stick;
				_autoRun = false;
				StickBase = new Vector2(Mathf.Max(p.x, StickRadius * 0.8f), Mathf.Min(p.y, (float)Screen.height - StickRadius * 0.8f));
			}
			else
			{
				f.Role = ((!_fingers.Exists((Finger x) => x.Role == Role.Look)) ? Role.Look : Role.Ignore);
			}
		}

		private static void Lift(Finger f)
		{
			if (f.Role == Role.Stick && LockLit)
			{
				_autoRun = true;
			}
			if (f.Role != Role.Tile || !_moreOpen)
			{
				return;
			}
			Tile tile = f.Tile;
			_moreOpen = false;
			if (tile != null && tile.Box.Contains(f.Pos))
			{
				if (tile.Key != KeyCode.None)
				{
					_pulse.Add(tile.Key);
				}
				else if (tile.Act != null)
				{
					_pending = tile.Act();
				}
			}
		}

		private static void Steer(Finger f)
		{
			StickHeld = true;
			Vector2 vector = f.Pos - StickBase;
			float num = Mathf.Max(1f, StickRadius);
			Vector2 move = new Vector2(vector.x, 0f - vector.y) / num;
			float magnitude = move.magnitude;
			LockLit = move.y > 1.75f && Mathf.Abs(move.x) < 0.9f;
			_sprint = magnitude > 1.12f && move.y > 0.7f * magnitude;
			if (magnitude > 1f)
			{
				move /= magnitude;
			}
			if (magnitude < 0.12f)
			{
				move = Vector2.zero;
			}
			_move = move;
			StickKnob = StickBase + new Vector2(move.x, 0f - move.y) * num;
		}
	}
}
