using System;
using UnityEngine;

namespace FirstPerson.Pad
{
	internal static class PadMenu
	{
		internal struct Item
		{
			public string Name;

			public KeyCode Key;

			public Action Act;
		}

		public static readonly Item[] Items = new Item[20]
		{
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
				Name = "Take over",
				Act = WorldBoxMod.MenuTakeOver
			},
			new Item
			{
				Name = "Leap back",
				Key = KeyCode.LeftBracket
			},
			new Item
			{
				Name = "Leap next",
				Key = KeyCode.RightBracket
			},
			new Item
			{
				Name = "Compass",
				Act = delegate
				{
					WorldBoxMod.MenuTool(WorldBoxMod.Tool.Compass);
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
				Name = "Binoculars",
				Key = KeyCode.Z
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
				Name = "Reload",
				Key = KeyCode.R
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
				Name = "Bandage",
				Act = delegate
				{
					WorldBoxMod.MenuTool(WorldBoxMod.Tool.Bandage);
				}
			},
			new Item
			{
				Name = "Plan town",
				Key = KeyCode.Y
			},
			new Item
			{
				Name = "Map view",
				Key = KeyCode.V
			},
			new Item
			{
				Name = "Let go",
				Act = WorldBoxMod.MenuLetGo
			},
			new Item
			{
				Name = "Help",
				Key = KeyCode.F1
			},
			new Item
			{
				Name = "Screenshot",
				Key = KeyCode.F9
			},
			new Item
			{
				Name = "Time of day",
				Key = KeyCode.F7
			},
			new Item
			{
				Name = "Sit",
				Key = KeyCode.P
			},
			new Item
			{
				Name = "Ping",
				Key = KeyCode.C
			}
		};

		public const int Columns = 5;

		public static bool Open { get; private set; }

		public static float OpenedAt { get; private set; }

		public static int Choice { get; private set; }

		public static void Show()
		{
			Open = true;
			OpenedAt = Time.unscaledTime;
			Choice = 0;
			Sounds.PlayUI("event:/SFX/UI/WindowWhoosh");
		}

		public static void Hop(Vector2Int dir)
		{
			if (Open)
			{
				int num = (Items.Length + 5 - 1) / 5;
				int num2 = Choice % 5;
				int num3 = Choice / 5;
				if (dir.x != 0)
				{
					num2 = (num2 + dir.x + 5) % 5;
				}
				else if (dir.y != 0)
				{
					num3 = (num3 + dir.y + num) % num;
				}
				Choice = Mathf.Min(Items.Length - 1, num3 * 5 + num2);
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
		}

		public static void Point(int i)
		{
			if (Open && i >= 0 && i < Items.Length)
			{
				Choice = i;
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
			if (Open)
			{
				Sounds.PlayUI("event:/SFX/UI/WindowClose");
			}
			Open = false;
		}
	}
}
