using System;
using UnityEngine;

namespace FirstPerson.Vr
{
	internal static class VrMenu
	{
		internal struct Item
		{
			public string Name;

			public KeyCode Key;

			public string Button;

			public Action Act;
		}

		public static readonly Item[] Items = new Item[20]
		{
			new Item
			{
				Name = "Map",
				Key = KeyCode.M,
				Button = "Y"
			},
			new Item
			{
				Name = "Inventory",
				Key = KeyCode.Tab,
				Button = "X"
			},
			new Item
			{
				Name = "Minimap",
				Key = KeyCode.N
			},
			new Item
			{
				Name = "Let go",
				Key = KeyCode.G
			},
			new Item
			{
				Name = "Map view",
				Key = KeyCode.V
			},
			new Item
			{
				Name = "Take over",
				Act = WorldBoxMod.MenuTakeOver
			},
			new Item
			{
				Name = "Settings",
				Key = KeyCode.F2,
				Button = "Menu"
			},
			new Item
			{
				Name = "Eat",
				Act = delegate
				{
					WorldBoxMod.MenuTool(WorldBoxMod.Tool.Eat);
				}
			},
			new Item
			{
				Name = "Torch",
				Act = delegate
				{
					WorldBoxMod.MenuTool(WorldBoxMod.Tool.Torch);
				}
			},
			new Item
			{
				Name = "Put away",
				Act = delegate
				{
					WorldBoxMod.MenuTool(WorldBoxMod.Tool.PutAway);
				}
			},
			new Item
			{
				Name = "Bandage",
				Act = delegate
				{
					WorldBoxMod.MenuTool(WorldBoxMod.Tool.Bandage);
				}
			},
			new Item
			{
				Name = "Ping",
				Key = KeyCode.C
			},
			new Item
			{
				Name = "Star",
				Key = KeyCode.B
			},
			new Item
			{
				Name = "Insult",
				Act = WorldBoxMod.MenuInsult
			},
			new Item
			{
				Name = "Steal",
				Act = WorldBoxMod.MenuSteal
			},
			new Item
			{
				Name = "Binoculars",
				Key = KeyCode.Z
			},
			new Item
			{
				Name = "Sit",
				Key = KeyCode.P
			},
			new Item
			{
				Name = "Favorite <",
				Key = KeyCode.LeftBracket
			},
			new Item
			{
				Name = "Favorite >",
				Key = KeyCode.RightBracket
			},
			new Item
			{
				Name = "Help",
				Key = KeyCode.F1
			}
		};

		public const int Columns = 4;

		public static KeyCode Pressed = KeyCode.None;

		private static int _hover = -1;

		private static float _stickNext;

		private static bool _stickIn;

		public static bool Open { get; private set; }

		public static float OpenedAt { get; private set; }

		public static int Choice { get; private set; } = -1;

		public static void Show()
		{
			Open = true;
			OpenedAt = Time.unscaledTime;
			Choice = (_hover = -1);
			_stickIn = false;
		}

		public static void Hover(int i)
		{
			if (Open)
			{
				if (i >= 0 && i != _hover)
				{
					Choice = i;
				}
				_hover = i;
			}
		}

		public static void Stick(Vector2 v)
		{
			if (!Open)
			{
				return;
			}
			float unscaledTime = Time.unscaledTime;
			if (v.magnitude < 0.6f)
			{
				_stickIn = false;
			}
			else
			{
				if (_stickIn && unscaledTime < _stickNext)
				{
					return;
				}
				_stickNext = unscaledTime + (_stickIn ? 0.18f : 0.4f);
				_stickIn = true;
				int num = (Items.Length + 4 - 1) / 4;
				if (Choice < 0)
				{
					Choice = 5;
					return;
				}
				int num2 = Choice % 4;
				int num3 = Choice / 4;
				if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
				{
					num2 = (num2 + ((v.x > 0f) ? 1 : 3)) % 4;
				}
				else
				{
					num3 = (num3 + ((v.y < 0f) ? 1 : (num - 1))) % num;
				}
				Choice = Mathf.Min(Items.Length - 1, num3 * 4 + num2);
			}
		}

		public static void Press(int i)
		{
			if (Open && i >= 0 && i < Items.Length)
			{
				if (Items[i].Act != null)
				{
					Items[i].Act();
				}
				else
				{
					Pressed = Items[i].Key;
				}
				Hide();
			}
		}

		public static KeyCode Close()
		{
			KeyCode result = KeyCode.None;
			if (Open && Choice >= 0 && Choice < Items.Length)
			{
				if (Items[Choice].Act != null)
				{
					Items[Choice].Act();
				}
				else
				{
					result = Items[Choice].Key;
				}
			}
			Hide();
			return result;
		}

		public static void Hide()
		{
			Open = false;
			Choice = (_hover = -1);
		}
	}
}
