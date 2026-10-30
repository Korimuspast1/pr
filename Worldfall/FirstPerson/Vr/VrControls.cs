using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson.Vr
{
	internal static class VrControls
	{
		public static bool Active;

		public static Vector2 Move;

		private static readonly HashSet<KeyCode> Now = new HashSet<KeyCode>();

		private static readonly HashSet<KeyCode> Before = new HashSet<KeyCode>();

		private static float _turn;

		private static bool _snapArmed = true;

		private static bool _sprint;

		private static bool _sprintWas;

		private static bool _attack;

		private static bool _kick;

		private static bool _use;

		private static bool _context;

		private static bool _upArmed = true;

		private static bool _downArmed = true;

		private static bool _attackLaser;

		private static bool _kickLaser;

		private static bool _useLaser;

		private static bool _contextLaser;

		private const float PlanHold = 0.8f;

		private const float QuickHold = 0.6f;

		private static float _planHeld;

		private static float _quickHeld;

		private const float MenuHold = 0.35f;

		private static float _aHeld;

		private const float MapHold = 0.6f;

		private static float _menuHeld;

		private static bool _menuFired;

		private static Vector2Int _nav;

		private static Vector2Int _navDir;

		private static float _navHeld;

		private static int _tab;

		private static bool _tabArmed = true;

		public static bool NavHeld;

		public static bool WristTaken;

		private static bool _planFired;

		private static bool _quickFired;

		private static bool _aMenu;

		private static bool _bShut;

		public static readonly string[][] Help = new string[18][]
		{
			new string[2] { "VR left stick", "Walk. Click to sprint until you stop. Hold the click standing still for a bandage" },
			new string[2] { "VR right stick", "Turn (snap or smooth); pull back to backstep; click: compass" },
			new string[2] { "VR right trigger", "Attack; hold to gather. A bow: bring the right hand to the string, pull back, let go" },
			new string[2] { "VR A", "Tap to jump. Hold for the quick menu, point at a button and let go" },
			new string[2] { "VR left trigger", "Guard (hold to block)" },
			new string[2] { "VR right grip", "Talk, enter, use (E). Hold for everything else" },
			new string[2] { "VR left grip", "Reload, vent (R)" },
			new string[2] { "VR B", "Your ability. Backs out of a talk or a window" },
			new string[2] { "VR X / Y", "Inventory, hold X to plan the town / map (M)" },
			new string[2] { "VR menu", "Settings (F2). Hold for the map" },
			new string[2] { "VR off-hand wrist", "Turn it up like a watch: hotbar, minimap, tasks, gold and you. Point at a slot and pull the trigger" },
			new string[2] { "VR hold right grip", "The action wheel. Point at a choice and let go" },
			new string[2] { "VR in a window", "Left stick moves the highlight, A presses it, right stick scrolls and flicks tabs" },
			new string[2] { "VR in a window", "Or point with either hand. Trigger clicks, grip right-clicks" },
			new string[2] { "VR on the map", "Point with either hand. Trigger uses the power, or takes over who you point at" },
			new string[2] { "VR on the map", "B puts the power down or closes a window. Y switches 3D and flat" },
			new string[2] { "VR on the map", "Left stick moves. Right stick turns and zooms, click to bring the controls back" },
			new string[2] { "VR on the map", "Grip and pull to drag the land" }
		};

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

		public static Vector2Int TakeNav()
		{
			Vector2Int nav = _nav;
			_nav = Vector2Int.zero;
			return nav;
		}

		public static int TakeTab()
		{
			int tab = _tab;
			_tab = 0;
			return tab;
		}

		public static float TakeTurn()
		{
			float turn = _turn;
			_turn = 0f;
			return turn;
		}

		public static void Update(VrSystem vr, Settings s, bool menu, bool map, bool laser, float dt, bool talk = false)
		{
			NavHeld = false;
			Before.Clear();
			Before.UnionWith(Now);
			Now.Clear();
			Active = vr != null && vr.Running && vr.Focused;
			if (!Active)
			{
				VrMenu.Hide();
				VrMenu.Pressed = KeyCode.None;
				_aHeld = 0f;
				_aMenu = false;
				Move = Vector2.zero;
				_sprint = false;
				_turn = 0f;
				_menuHeld = 0f;
				_menuFired = false;
				_nav = Vector2Int.zero;
				_tab = 0;
				return;
			}
			_attack = Pulled(vr.AttackPull, _attack);
			_kick = Pulled(vr.KickPull, _kick);
			_use = Pulled(vr.UsePull, _use);
			_context = Pulled(vr.ContextPull, _context);
			if (map)
			{
				Move = Vector2.zero;
				_sprint = false;
				_turn = 0f;
				_attackLaser = _attack;
				_kickLaser = _kick;
				_useLaser = _use;
				_contextLaser = _context;
				VrMenu.Hide();
				VrMenu.Pressed = KeyCode.None;
				_aHeld = 0f;
				_aMenu = false;
				if (vr.AbilityButton)
				{
					Now.Add(KeyCode.Escape);
				}
				if (vr.MapButton)
				{
					Now.Add(KeyCode.V);
				}
				MenuButton(vr.MenuButton, KeyCode.V, dt);
				return;
			}
			if (laser)
			{
				_attackLaser = _attack;
				_kickLaser = _kick;
				_useLaser = _use;
				_contextLaser = _context;
			}
			if (WristTaken)
			{
				_attackLaser = _attack;
			}
			if (!_attack)
			{
				_attackLaser = false;
			}
			if (!_kick)
			{
				_kickLaser = false;
			}
			if (!_use)
			{
				_useLaser = false;
			}
			if (!_context)
			{
				_contextLaser = false;
			}
			if (_attack && !laser && !_attackLaser)
			{
				Now.Add(KeyCode.Mouse0);
			}
			if (_kick && !laser && !_kickLaser)
			{
				Now.Add(KeyCode.Mouse1);
			}
			if (_use && !laser && !_useLaser)
			{
				Now.Add(KeyCode.E);
			}
			if (_context && !laser && !_contextLaser)
			{
				Now.Add(KeyCode.R);
			}
			if (menu)
			{
				VrMenu.Hide();
			}
			if (VrMenu.Pressed != KeyCode.None)
			{
				Now.Add(VrMenu.Pressed);
				VrMenu.Pressed = KeyCode.None;
			}
			NavHeld = menu && !talk && vr.JumpButton;
			if (vr.JumpButton)
			{
				_aHeld += dt;
				if (!VrMenu.Open && !_aMenu && !menu && _aHeld >= 0.35f)
				{
					VrMenu.Show();
					_aMenu = true;
				}
			}
			else
			{
				if (VrMenu.Open)
				{
					KeyCode keyCode = VrMenu.Close();
					if (keyCode != KeyCode.None)
					{
						Now.Add(keyCode);
					}
				}
				else if (_aHeld > 0f && !_aMenu && (!menu || talk))
				{
					Now.Add(KeyCode.Space);
				}
				_aHeld = 0f;
				_aMenu = false;
			}
			if (vr.AbilityButton)
			{
				if (VrMenu.Open || _bShut)
				{
					VrMenu.Hide();
					_bShut = true;
				}
				else if (menu)
				{
					Now.Add(KeyCode.Escape);
					_bShut = true;
				}
				else
				{
					Now.Add(KeyCode.X);
				}
			}
			else
			{
				_bShut = false;
			}
			if (vr.MapButton)
			{
				Now.Add(KeyCode.M);
			}
			if (vr.BinocularsButton)
			{
				Now.Add(KeyCode.O);
			}
			MenuButton(vr.MenuButton, KeyCode.M, dt);
			Vector2 move = DeadZone(vr.MoveStick, 0.18f);
			if (vr.CraftButton)
			{
				_planHeld += dt;
				if (_planHeld >= 0.8f && !_planFired)
				{
					Now.Add(KeyCode.Y);
					_planFired = true;
				}
			}
			else
			{
				if (_planHeld > 0f && !_planFired)
				{
					Now.Add(KeyCode.Tab);
				}
				_planHeld = 0f;
				_planFired = false;
			}
			_quickHeld = ((vr.SprintButton && !menu && move.sqrMagnitude < 0.01f) ? (_quickHeld + dt) : 0f);
			if (_quickHeld >= 0.6f && !_quickFired)
			{
				WorldBoxMod.MenuTool(WorldBoxMod.Tool.Bandage);
				_quickFired = true;
			}
			if (_quickHeld <= 0f)
			{
				_quickFired = false;
			}
			if (VrMenu.Open)
			{
				VrMenu.Stick(vr.MoveStick);
				Move = Vector2.zero;
				_sprint = false;
				return;
			}
			if (menu && !talk)
			{
				Move = Vector2.zero;
				_sprint = false;
				Vector2 moveStick = vr.MoveStick;
				Vector2Int vector2Int = Vector2Int.zero;
				if (moveStick.magnitude > 0.7f)
				{
					vector2Int = ((Mathf.Abs(moveStick.x) > Mathf.Abs(moveStick.y)) ? new Vector2Int((moveStick.x > 0f) ? 1 : (-1), 0) : new Vector2Int(0, (!(moveStick.y > 0f)) ? 1 : (-1)));
				}
				if (vector2Int == Vector2Int.zero)
				{
					if (moveStick.magnitude < 0.4f)
					{
						_navDir = Vector2Int.zero;
						_navHeld = 0f;
					}
				}
				else if (vector2Int != _navDir)
				{
					_nav = vector2Int;
					_navDir = vector2Int;
					_navHeld = 0f;
				}
				else if ((_navHeld += dt) >= 0.45f)
				{
					_nav = vector2Int;
					_navHeld = 0.32999998f;
				}
				float x = vr.TurnStick.x;
				if (Mathf.Abs(x) > 0.75f && Mathf.Abs(vr.TurnStick.y) < 0.5f)
				{
					if (_tabArmed)
					{
						_tab = ((x > 0f) ? 1 : (-1));
					}
					_tabArmed = false;
				}
				else if (Mathf.Abs(x) < 0.35f)
				{
					_tabArmed = true;
				}
				return;
			}
			_navDir = Vector2Int.zero;
			if (menu)
			{
				Move = Vector2.zero;
				_sprint = false;
				Vector2 vector = (VrMouse.Now.OnScreen ? Vector2.zero : vr.TurnStick);
				float num = ((Mathf.Abs(vr.MoveStick.y) > Mathf.Abs(vector.y)) ? vr.MoveStick.y : vector.y);
				if (num > 0.7f)
				{
					if (_upArmed)
					{
						Now.Add(KeyCode.UpArrow);
					}
					_upArmed = false;
				}
				else if (num < 0.3f)
				{
					_upArmed = true;
				}
				if (num < -0.7f)
				{
					if (_downArmed)
					{
						Now.Add(KeyCode.DownArrow);
					}
					_downArmed = false;
				}
				else if (num > -0.3f)
				{
					_downArmed = true;
				}
				return;
			}
			Move = move;
			if (vr.SprintButton && !_sprintWas)
			{
				_sprint = !_sprint;
			}
			_sprintWas = vr.SprintButton;
			if (move.sqrMagnitude < 0.01f)
			{
				_sprint = false;
			}
			if (_sprint)
			{
				Now.Add(KeyCode.LeftShift);
			}
			float x2 = vr.TurnStick.x;
			if (s.VrTurn == 0)
			{
				if (Mathf.Abs(x2) > 0.75f)
				{
					if (_snapArmed)
					{
						_turn -= Mathf.Sign(x2) * s.VrSnapAngle * (MathF.PI / 180f);
					}
					_snapArmed = false;
				}
				else if (Mathf.Abs(x2) < 0.35f)
				{
					_snapArmed = true;
				}
			}
			else
			{
				float num2 = ((Mathf.Abs(x2) < 0.15f) ? 0f : ((x2 - Mathf.Sign(x2) * 0.15f) / 0.85f));
				_turn -= num2 * Mathf.Abs(num2) * s.VrTurnSpeed * (MathF.PI / 180f) * dt;
			}
			if (vr.TurnStick.y < -0.75f && Mathf.Abs(x2) < 0.5f)
			{
				Now.Add(KeyCode.LeftControl);
			}
		}

		private static bool Pulled(float v, bool was)
		{
			if (!was)
			{
				return v > 0.55f;
			}
			return v > 0.4f;
		}

		private static void MenuButton(bool down, KeyCode held, float dt)
		{
			if (down)
			{
				_menuHeld += dt;
				if (_menuHeld >= 0.6f && !_menuFired)
				{
					Now.Add(held);
					_menuFired = true;
				}
			}
			else
			{
				if (_menuHeld > 0f && !_menuFired)
				{
					Now.Add(KeyCode.F2);
				}
				_menuHeld = 0f;
				_menuFired = false;
			}
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
	}
}
