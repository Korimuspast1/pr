using FirstPerson.Vr;
using UnityEngine;

namespace FirstPerson
{
	internal static class Help
	{
		internal sealed class Part
		{
			public string Name;

			public string Icon;

			public string[][] Rows;

			public string[] Lines;
		}

		public static bool Open;

		public static int Page;

		public static readonly int[] Section = new int[2];

		public static Vector2 Scroll;

		public static readonly string[] Pages = new string[2] { "Keys", "How to" };

		public static readonly Part[] Keys = new Part[12]
		{
			new Part
			{
				Name = "On foot",
				Icon = "iconArrowDestination",
				Rows = new string[12][]
				{
					new string[2] { "Mouse", "Look around" },
					new string[2] { "W A S D", "Run or swim" },
					new string[2] { "Shift", "Sprint" },
					new string[2] { "Alt", "Walk slowly, on and off" },
					new string[2] { "Ctrl", "Crouch, on and off. Shift stands you up" },
					new string[2] { "Space", "Jump" },
					new string[2] { "A A  D D  S S", "Dodge step that way (tap it twice)" },
					new string[2] { "Left click", "Attack. Hold to gather or draw a bow" },
					new string[2] { "Right click", "Guard. Raise it as a blow lands to parry" },
					new string[2] { "X", "Your kind's ability. Hold to aim" },
					new string[2] { "R", "Reload the shotgun. Vent the blaster" },
					new string[2] { "P", "Sit or get up. Hold for poses" }
				}
			},
			new Part
			{
				Name = "Hands and tools",
				Icon = "iconCrystalSword",
				Rows = new string[8][]
				{
					new string[2] { "1-8  Wheel", "Hold something from the hotbar" },
					new string[2] { "Same number", "Put away what you hold, again to take it up" },
					new string[2] { "Q", "Take out your last tool again" },
					new string[2] { "Hold Q", "Tools. Torch, compass, eat, bandage, put away" },
					new string[2] { "Z", "Binoculars. Hold, or tap to keep them up" },
					new string[2] { "H", "Quick heal — equip best bandage/salve" },
					new string[2] { "R", "Sort bag (outside menus)" },
					new string[2] { "Wheel", "Binoculars zoom. The compass picks its mark" },
					new string[2] { "Left click", "Eat the food you hold. Bind a wound with a bandage" },
					new string[2] { "Hold left click", "Hold your torch out. Light it at a fire" }
				}
			},
			new Part
			{
				Name = "Digging",
				Icon = "iconShovelMinus",
				Rows = new string[4][]
				{
					new string[2] { "Hold left click", "With a shovel or a pickaxe, dig or fill the ground you look at" },
					new string[2] { "R", "Switch dig and fill. Filling takes dirt, sand or stone, and fills water too" },
					new string[2] { "Hold right click", "Level the ground to your feet" },
					new string[2] { "Wheel", "Wider patch. It grows as you dig more" }
				}
			},
			new Part
			{
				Name = "People and things",
				Icon = "iconFavoriteStar",
				Rows = new string[8][]
				{
					new string[2] { "E", "Talk, enter, board, store, read a board" },
					new string[2] { "Hold E", "Everything else. Insult, steal, star, take over, hijack" },
					new string[2] { "T", "Emote — tap to wave, hold for wheel (6 emotes)" },
					new string[2] { "Middle click or =", "Take over who you look at" },
					new string[2] { "C", "Ping what you look at. Hold for the ping wheel" },
					new string[2] { "B", "Star who you look at" },
					new string[2] { "K", "Bookmark where you are (Shift+K remove, Ctrl+Shift+K clear)" },
					new string[2] { "L", "Headlamp on/off (stays on in dark)" },
					new string[2] { "F near water", "Fish — cast with F, reel on bite" },
					new string[2] { "[  ]", "Leap between favorites" },
					new string[2] { "Hold E on a door", "Force it" },
					new string[2] { "Hold E on a window", "Draw every curtain" }
				}
			},
			new Part
			{
				Name = "Windows",
				Icon = "iconWorldInfo",
				Rows = new string[10][]
				{
					new string[2] { "Tab", "You. Bag, crafting, magic, people. Hold it to use on the move" },
					new string[2] { "M", "World. Map, tasks, news, family, war, orders, council" },
					new string[2] { "N", "Minimap on or off. Hold for your explored map" },
					new string[2] { "Y", "Plan the town. Set down lights outdoors" },
					new string[2] { "F1", "Help" },
					new string[2] { "Esc  F2", "Settings. Esc closes a window first" },
					new string[2] { "F7", "Time of day" },
					new string[2] { "F9", "Screenshot" },
					new string[2] { "F10", "Photo mode (hide HUD, +/- FOV, Backspace reset)" },
					new string[2] { "Enter", "Chat, in multiplayer" },
					new string[2] { "Hold Caps Lock", "Talk, in multiplayer" }
				}
			},
			new Part
			{
				Name = "Talking",
				Icon = "iconTalk",
				Rows = new string[6][]
				{
					new string[2] { "W S  Wheel", "Choose" },
					new string[2] { "E  Enter  Space", "Say it" },
					new string[2] { "1-9", "Say that answer" },
					new string[2] { "Backspace", "Back to the topics" },
					new string[2] { "B", "Star them" },
					new string[2] { "Esc", "End the talk" }
				}
			},
			new Part
			{
				Name = "Views and flight",
				Icon = "iconInspect",
				Rows = new string[13][]
				{
					new string[2] { "V", "Map view. On the map, the 3D world" },
					new string[2] { "G", "Let the creature go" },
					new string[2] { "Hold F", "Leave the body and fly" },
					new string[2] { "F", "On the map, fly" },
					new string[2] { "Arrows", "Turn and tip the 3D world" },
					new string[2] { "Wheel  Q  E", "Zoom the 3D world. All the way in takes over" },
					new string[2] { "W A S D", "Fly where you look" },
					new string[2] { "Space  Ctrl", "Fly up, fly down" },
					new string[2] { "Shift  Wheel", "Fly faster, set the speed" },
					new string[2] { "Left click", "In flight, use the god power" },
					new string[2] { "Middle click", "In flight, take over who you look at" },
					new string[2] { "Tab", "In flight, a cursor for the game's buttons" },
					new string[2] { "F  V  Esc", "Land" }
				}
			},
			new Part
			{
				Name = "Ships",
				Icon = "iconBoat",
				Rows = new string[7][]
				{
					new string[2] { "E", "Go aboard. In the cabin, leave" },
					new string[2] { "Hold E", "Hijack her. In the cabin, take the wheel" },
					new string[2] { "W S", "More or less sail" },
					new string[2] { "A D", "Turn the wheel" },
					new string[2] { "Shift", "Fill the sails with a gust" },
					new string[2] { "Left click", "Fire her guns where you look" },
					new string[2] { "E at her wheel", "Get off, ashore or over the side" }
				}
			},
			new Part
			{
				Name = "Town planning",
				Icon = "iconBuildings",
				Rows = new string[7][]
				{
					new string[2] { "Y", "Plan, or stop planning" },
					new string[2] { "Q  E", "Kind of building" },
					new string[2] { "1-8  Wheel", "Pick a building" },
					new string[2] { "Left click", "Place the plan" },
					new string[2] { "Right click", "Take a plan back. Twice knocks a building down" },
					new string[2] { "R", "Turn a building or a light" },
					new string[2] { "Esc", "Stop planning" }
				}
			},
			new Part
			{
				Name = "War map",
				Icon = "iconWarfare",
				Rows = new string[9][]
				{
					new string[2] { "Click  Drag", "Select warriors. Shift adds" },
					new string[2] { "Double click", "Select them all" },
					new string[2] { "Right click", "Send them. On a foe, fight it" },
					new string[2] { "Shift right click", "Plant a flag. Drag for its area" },
					new string[2] { "Q E R T Y", "Attack, wait, destroy, kill, pillage" },
					new string[2] { "Ctrl 1-6", "Save a group" },
					new string[2] { "1-6", "Select a group and jump to it" },
					new string[2] { "Ctrl right click", "Ships fire there" },
					new string[2] { "View scene", "The fight in 3D. W A S D, arrows, F follows" }
				}
			},
			new Part
			{
				Name = "Controller",
				Icon = "iconOptions",
				Rows = new string[12][]
				{
					new string[2] { "Left stick", "Walk. Click to run until you stop. Flick back twice to backstep" },
					new string[2] { "Right stick", "Look. Click to ping, hold for the ping wheel" },
					new string[2] { "RT or X  LT", "Attack, gather, draw a bow. Guard" },
					new string[2] { "A  B", "Jump. Your ability" },
					new string[2] { "Y", "Talk and use. Hold for everything else" },
					new string[2] { "RB  LB", "Hotbar next and back. Zoom and pick" },
					new string[2] { "D-pad", "Up map, down eat, left minimap, right star" },
					new string[2] { "View  Menu", "Inventory, settings. Hold Menu for the quick menu" },
					new string[2] { "Deck L4 L5 R4 R5", "Put away, quick menu, reload, map view" },
					new string[2] { "Deck right trackpad", "Swipe for the compass, the torch, the binoculars" },
					new string[2] { "In windows", "D-pad moves, A picks, B backs out, RB LB tabs" },
					new string[2] { "On the map", "Right stick cursor, RT click, LT right click, RB LB zoom" }
				}
			},
			new Part
			{
				Name = "VR",
				Icon = "iconShiny",
				Rows = VrRows()
			}
		};

		public static readonly Part[] Guides = new Part[10]
		{
			new Part
			{
				Name = "First steps",
				Icon = "iconQuestionMark",
				Lines = new string[6] { "On the map, select a creature and press G to take it over. The view dives into its eyes.", "Walk with W A S D and look with the mouse. Tab is everything about you, M everything about the world.", "Hold E on anyone or anything to see all you can do there. Hold Q for your tools.", "G lets the creature go. Hold F to leave its body and fly. V shows the map.", "Esc opens the settings. Leave body and Start a run sit under them.", "Sleep in your bed (E) or on a bedroll to save. The game doesn't save by itself while you are in a body." }
			},
			new Part
			{
				Name = "Fighting",
				Icon = "iconAttack",
				Rows = null,
				Lines = new string[5] { "Each weapon swings at its own pace. Holding the button keeps swinging, clicking faster does nothing.", "Hold the right button to guard. Raise it just as a blow comes down to parry, and your next blow is a critical.", "Blows aimed at you are drawn back first, with a sound. A guard breaks out of stamina or against something twice your size.", "Bows draw while held. A full draw flies faster and flatter. Arrows to the head hurt more.", "X is your kind's own ability. Tab > Magic picks a spell for it once you know one." }
			},
			new Part
			{
				Name = "Towns and work",
				Icon = "iconCity",
				Lines = new string[6] { "Say hello with E. People tell you their name, and you remember them under Tab > People.", "Ask anyone for work. Tasks show on M > Map and the compass leads to the nearest.", "Every town has a bounty board by its hall. E reads it.", "E at a storehouse or the hall stores what you gathered. Hold E at the hall to store instead of entering.", "To join a town, ask its leader. Your standing with each town shows on M > Map.", "Crimes seen by others put a price on your head in that realm. Pay it to a leader." }
			},
			new Part
			{
				Name = "Your own village",
				Icon = "iconBuildings",
				Lines = new string[7] { "Leave your town first. Then press Y on open land, or Settle here on M > Family.", "Choose where the bonfire burns and click. The village is founded there and you lead it.", "Build the bonfire by holding the left button at it, with a hammer or bare hands.", "Y plans buildings. Q and E change the kind, 1-8 or the wheel pick one, a click places it.", "Plans cost resources from the stockpile. Hammer them yourself, or the builders take them in turn.", "Right click takes a plan back with its cost. Twice knocks a building down for half its cost.", "The town adds homes by itself for those who come to live there." }
			},
			new Part
			{
				Name = "Lights",
				Icon = "iconFire",
				Lines = new string[5] { "Y anywhere outdoors opens the Lights: a standing torch, a wall torch, a lamp post and a brazier. In a town you lead they are a tab of the plans.", "Each takes one from your bag. A torch is 1 wood, a lamp post 2 wood and 1 metal, a brazier 3 stone and 1 metal, made by hand under Tab.", "Set them on any dry land. A wall torch hangs on the wall of a house or a town wall you aim at.", "They burn for good, and your torch catches at them. Right click in the Lights takes one back: yours, or any in a town you rule.", "Towns put up their own along their roads." }
			},
			new Part
			{
				Name = "The map and houses",
				Icon = "iconHoused",
				Lines = new string[6] { "The day goes on on the map too, at the game's speed. At night the map darkens. F2 > Game > Map night: Off, Light, Full.", "A house's windows glow only while someone is in and awake. Smoke rises while someone is home. Curtains close at night.", "Zoom in close and point at a house: its roof lifts to show who is in, and where. Hold a finger on it on a touchscreen.", "Click someone inside to select them. G takes them over: you wake where they are, in bed or at the table.", "A body you leave indoors stays in. The view rises out through the roof.", "Near you, people go in and out by the door, sit where they sit, and the king and council meet in the hall by day." }
			},
			new Part
			{
				Name = "Serving the crown",
				Icon = "iconWarriors",
				Lines = new string[4] { "Ask your town's leader or your king to enlist. Choose a soldier or a founder.", "A soldier's orders are on M > Orders and the compass. Years of service make you captain.", "A captain commands the army from M > Orders, or with Go here and Defend pings.", "A founder is sent to free land. Stand on it and press E to found a village for the realm." }
			},
			new Part
			{
				Name = "Ruling",
				Icon = "iconCrown",
				Lines = new string[5] { "When the crown would fall to you, a herald asks first. Accept, and you rule.", "M > War is your war map. Select warriors, send them, plant numbered flags with orders.", "M > Council holds war and peace, alliances, gifts, tribute and strikes paid with your realm's kills.", "M > News tells when a city of yours is attacked. M opens on it while the news is fresh.", "Sit on the throne in the capital's keep to hold court. Petitioners and envoys come one by one." }
			},
			new Part
			{
				Name = "Ships",
				Icon = "iconBoat",
				Lines = new string[5] { "E by a ship goes aboard. Hold E to hijack her and take her wheel.", "At the wheel, W and S set the sails, A and D turn, Shift fills the sails with a gust.", "Left click fires her guns where you look.", "E at the wheel gets you off, onto a dock or beach near, or over the side.", "With wood in your bag, E at open water builds a boat." }
			},
			new Part
			{
				Name = "Lock-in runs",
				Icon = "iconHealth",
				Lines = new string[4] { "Esc > Start a run locks you into the life you wear. Pick your traits and live it out.", "You can't leap into others while it lasts. In a run you steal only from the sleeping.", "When you die, a child or kin with a home can carry the run on.", "Esc > Give up the run ends it. Every run is kept in the Hall of Lives." }
			}
		};

		public static Part[] PartsOf(int page)
		{
			if (page != 0)
			{
				return Guides;
			}
			return Keys;
		}

		private static string[][] VrRows()
		{
			string[][] help = VrControls.Help;
			string[][] array = new string[help.Length][];
			for (int i = 0; i < help.Length; i++)
			{
				string text = (help[i][0].StartsWith("VR ") ? help[i][0].Substring(3) : help[i][0]);
				array[i] = new string[2]
				{
					char.ToUpperInvariant(text[0]) + text.Substring(1),
					help[i][1]
				};
			}
			return array;
		}
	}
}
