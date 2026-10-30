using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal static class SupplyIcons
	{
		private const int Size = 28;

		private static readonly Dictionary<string, Sprite> Made = new Dictionary<string, Sprite>();

		private static readonly Dictionary<char, Color32> BandageInk = new Dictionary<char, Color32>
		{
			{
				'O',
				new Color32(96, 80, 78, byte.MaxValue)
			},
			{
				'1',
				new Color32(150, 136, 126, byte.MaxValue)
			},
			{
				'S',
				new Color32(150, 136, 126, byte.MaxValue)
			},
			{
				'2',
				new Color32(190, 178, 166, byte.MaxValue)
			},
			{
				'3',
				new Color32(222, 212, 196, byte.MaxValue)
			},
			{
				'4',
				new Color32(242, 238, 226, byte.MaxValue)
			},
			{
				'5',
				new Color32(byte.MaxValue, byte.MaxValue, 252, byte.MaxValue)
			},
			{
				'k',
				new Color32(84, 70, 68, byte.MaxValue)
			},
			{
				'r',
				new Color32(150, 40, 38, byte.MaxValue)
			},
			{
				'R',
				new Color32(204, 64, 54, byte.MaxValue)
			},
			{
				'x',
				new Color32(238, 116, 96, byte.MaxValue)
			}
		};

		private static readonly string[] BandageMap = new string[28]
		{
			"............................", "............................", "............................", "............................", "............................", "............................", "............................", ".......OOOOOOOOOOOOOOO......", ".....OO4444S5555xx5555O.....", ".....O442224S444xx44444O....",
			"....O4424442S444RR44444O....", "....O4242224S333RR33333O....", "....O424kk23S333RR33333O....", "....O424kk13S333RR33333O....", "....O4231113S333RR33333O....", "....O4233313S222rr22222O....", ".....O311113S222rr22222O....", ".....O33333S2222rr22222O....", "......OO3SS11111rr1111O.....", ".......OOOOOOOOOOOOOOO......",
			"..........O44444444444O.....", "...........O3333333332O.....", "............OOOOOOOOOOO.....", "............................", "............................", "............................", "............................", "............................"
		};

		private static readonly Dictionary<char, Color32> SalveInk = new Dictionary<char, Color32>
		{
			{
				'O',
				new Color32(92, 44, 32, byte.MaxValue)
			},
			{
				'W',
				new Color32(byte.MaxValue, 226, 184, byte.MaxValue)
			},
			{
				'h',
				new Color32(246, 184, 128, byte.MaxValue)
			},
			{
				'c',
				new Color32(226, 144, 90, byte.MaxValue)
			},
			{
				'C',
				new Color32(198, 110, 64, byte.MaxValue)
			},
			{
				'd',
				new Color32(156, 80, 48, byte.MaxValue)
			},
			{
				'D',
				new Color32(124, 60, 38, byte.MaxValue)
			},
			{
				'G',
				new Color32(170, 218, 108, byte.MaxValue)
			},
			{
				'g',
				new Color32(116, 182, 74, byte.MaxValue)
			},
			{
				'q',
				new Color32(70, 124, 50, byte.MaxValue)
			},
			{
				'w',
				new Color32(236, 226, 200, byte.MaxValue)
			},
			{
				'v',
				new Color32(198, 184, 158, byte.MaxValue)
			},
			{
				'l',
				new Color32(118, 184, 66, byte.MaxValue)
			},
			{
				'L',
				new Color32(66, 120, 44, byte.MaxValue)
			}
		};

		private static readonly string[] SalveMap = new string[28]
		{
			"............................", "............................", "............................", "............................", "....................lL......", "...................llL......", "..........OOOOOOOOlllL......", ".........OcGGGGGGGclL.......", ".........OcGgggggqcOL.......", ".........OccqqqqqccO........",
			"..........OOcCCCCOO.........", "........OwwwwwwwwwO.........", ".......OvwwwwwwwwvvO........", ".......OhhhccccccCCO........", "......OhhWWcccccCCCCO.......", ".....OcccWccccccCCCCdO......", ".....OcccccccccCCCCCdO......", ".....OcccccccCCCCCCddO......", ".....OccccccCCCCCCdddO......", ".....OCCCCCCCCCCCddddO......",
			"......OCCCCCCCCCddddO.......", ".......OCCCCCCdddddO........", "........OOdddddddOO.........", "..........OOOOOOO...........", "............................", "............................", "............................", "............................"
		};

		private static readonly Dictionary<char, Color32> TorchInk = new Dictionary<char, Color32>
		{
			{
				'O',
				new Color32(60, 22, 28, byte.MaxValue)
			},
			{
				'1',
				new Color32(84, 36, 40, byte.MaxValue)
			},
			{
				'2',
				new Color32(112, 50, 48, byte.MaxValue)
			},
			{
				'3',
				new Color32(150, 80, 66, byte.MaxValue)
			},
			{
				'r',
				new Color32(176, 102, 70, byte.MaxValue)
			},
			{
				'R',
				new Color32(226, 72, 22, byte.MaxValue)
			},
			{
				'o',
				new Color32(byte.MaxValue, 142, 32, byte.MaxValue)
			},
			{
				'y',
				new Color32(byte.MaxValue, 214, 72, byte.MaxValue)
			},
			{
				'w',
				new Color32(byte.MaxValue, 250, 214, byte.MaxValue)
			}
		};

		private static readonly string[] TorchMap = new string[28]
		{
			"............................", "..............R.............", ".............RR......R......", "......R......RoR....RR......", "......RR....RooR...RoR......", ".......RR..RRoyoR.RooR......", ".......RoR.RoyyoRRooR.......", "........RoRRoyyyoRoR........", "........RooRoyyyyooR........", ".......RooooyywyyyooR.......",
			".......RoyyyywwwyyyoR.......", ".......RoyywwwwwwyyoR.......", "........RoywwwwwwwyoR.......", "........OrrrrrrrrrrrO.......", "........O32222222221O.......", ".........O312121212O........", ".........O321212121O........", "..........O3121212O.........", "...........O32221O..........", "............O321O...........",
			"............O321O...........", "............O321O...........", "...........O33321O..........", "...........O11111O..........", "............O321O...........", "............O321O...........", "............O211O...........", ".............OOO............"
		};

		private static readonly Dictionary<char, Color32> BedrollInk = new Dictionary<char, Color32>
		{
			{
				'O',
				new Color32(44, 40, 30, byte.MaxValue)
			},
			{
				'k',
				new Color32(52, 66, 44, byte.MaxValue)
			},
			{
				'1',
				new Color32(70, 92, 60, byte.MaxValue)
			},
			{
				'2',
				new Color32(88, 116, 72, byte.MaxValue)
			},
			{
				'3',
				new Color32(112, 140, 86, byte.MaxValue)
			},
			{
				'4',
				new Color32(140, 166, 104, byte.MaxValue)
			},
			{
				'S',
				new Color32(128, 84, 50, byte.MaxValue)
			},
			{
				'b',
				new Color32(214, 180, 96, byte.MaxValue)
			}
		};

		private static readonly string[] BedrollMap = new string[17]
		{
			"............................", "............................", "............................", "............................", "............................", "............................", "............................", ".......OOOOOOOOOOOOOOOOOO...", "......O333O4444SS4444SS44O..", ".....O33k33O444SS4444SS44O..",
			".....O3kkk3O333SS3333SS33O..", ".....O3k3k3O333bS3333bS33O..", ".....O3kkk3O222SS2222SS22O..", ".....O33k33O222SS2222SS22O..", "......O333O1111SS1111SS11O..", ".......OOOOOOOOOOOOOOOOOO...", "............................"
		};

		private static readonly Dictionary<char, Color32> LightInk = new Dictionary<char, Color32>
		{
			{
				'O',
				new Color32(46, 30, 30, byte.MaxValue)
			},
			{
				'1',
				new Color32(84, 36, 40, byte.MaxValue)
			},
			{
				'2',
				new Color32(112, 50, 48, byte.MaxValue)
			},
			{
				'3',
				new Color32(150, 80, 66, byte.MaxValue)
			},
			{
				'r',
				new Color32(176, 102, 70, byte.MaxValue)
			},
			{
				'i',
				new Color32(72, 70, 78, byte.MaxValue)
			},
			{
				'I',
				new Color32(132, 128, 134, byte.MaxValue)
			},
			{
				's',
				new Color32(158, 152, 140, byte.MaxValue)
			},
			{
				'S',
				new Color32(112, 106, 98, byte.MaxValue)
			},
			{
				'g',
				new Color32(byte.MaxValue, 178, 84, byte.MaxValue)
			},
			{
				'k',
				new Color32(60, 36, 30, byte.MaxValue)
			},
			{
				'e',
				new Color32(232, 104, 42, byte.MaxValue)
			},
			{
				'R',
				new Color32(226, 72, 22, byte.MaxValue)
			},
			{
				'o',
				new Color32(byte.MaxValue, 142, 32, byte.MaxValue)
			},
			{
				'y',
				new Color32(byte.MaxValue, 214, 72, byte.MaxValue)
			},
			{
				'w',
				new Color32(byte.MaxValue, 250, 214, byte.MaxValue)
			}
		};

		private static readonly string[] LampMap = new string[28]
		{
			"..........O.................", ".........OIO................", "........OIIIO...............", ".......OiiiiiO.......O......", "........O321OOOOOOOOOIO.....", "........O321iiiiiiiiiiO.....", "........OiiiOOOiOOOIOO......", "........O321OOiOOiiiiiO.....", "........O321OiOOIIIIIIIO....", "........O321iO.OiggiggiO....",
			"........O321O..OigyiygiO....", "........O321O..OigyiygiO....", "........O321O..OigyiygiO....", "........O321O..OigyiygiO....", "........O321O..OiggiggiO....", "........O321O..OIIIIIIIO....", "........O321O...OOiiiOO.....", "........O321O.....OOO.......", "........O321O...............", "........O321O...............",
			"........OiiiO...............", "........O321O...............", ".......OO321O...............", "......OSSSSSSO..............", ".....OSSSSSSSSO.............", ".....OssssssssO.............", ".....OssssssssO.............", "......OOOOOOOO.............."
		};

		private static readonly string[] BrazierMap = new string[28]
		{
			"............................", "............OOO.............", "...........ORyRO............", "...........ORyRRO...........", "..........ORoyoRO...........", "..........ORoyoROOOO........", "........OORoyyyoRRyRO.......", ".......ORyRoywyoRRyRRO......", "......ORoyoRywyoRoyoRO......", ".....ORRyyyoRwyRoyyyoRO.....",
			".....ORoywyoRwyRoywyoRO.....", ".....ORoywyoRwyRoywyoRO.....", "....OOkkekkekkekkekkekOO....", "...OIIIIIIIIIIIIIIIIIIIIO...", "....OiiIiiiIiiiIiiiIiiiO....", ".....OiiiiiiiiiiiiiiiiO.....", "......OiiiiiiiiiiiiiiO......", ".......OOiiiiiiiiiiOO.......", ".........OiOOiiOOiO.........", ".........OiOOiiOOiO.........",
			".........OiOOiiOOiO.........", "........OiOOOiiOOOiO........", "........OIIIIIIIIIIO........", "........OiOOOiiOOOiO........", "......OOiO..OiiO..OiOO......", ".....OiOiO..OiiO..OiOiO.....", "......OiO..OiiiiO..OiO......", ".......O....OOOO....O......."
		};

		private static readonly string[] StandingMap = new string[28]
		{
			"............OOO.............", "...........ORyRO............", "...........ORyRRO...........", "..........ORoyoRO...........", ".........ORoyyyoRO..........", ".........ORoywyoRO..........", ".........ORoywyoRO..........", ".........ORoywyoRO..........", ".........OrrrrrrrO..........", ".........O2121212O..........",
			".........O2121212O..........", "..........O22222O...........", "...........O222O............", "...........O321O............", "...........O321O............", "...........O321O............", "...........OrrrO............", "...........O321O............", "...........O321O............", "...........O321O............",
			"...........OrrrO............", "...........O321O............", "...........O321O............", ".........OOO321OOO..........", "........OsSs321SSSO.........", "........OsssssssSSO.........", ".........OOsssssOO..........", "...........OOOOO............"
		};

		private static readonly string[] WallMap = new string[28]
		{
			"........OOO.................", ".......ORyRO................", ".......ORyRRO...............", "......ORoyoRO...............", ".....ORoyyyoRO....O.........", ".....ORoywyoRO...OiO........", ".....ORoywyoRO..OIiiO.......", ".....ORoywyoRO..OIiiO.......", ".....OrrrrrrrO..OIiiO.......", ".....O2121212O..OIIiO.......",
			".....O2121212O..OIiiO.......", "......O22222O...OIiiO.......", ".......OO32O....OIiiO.......", "........O32OO...OIiiO.......", ".........OIIIOOOOIiiO.......", ".........OIIIiiiiIiiO.......", ".........OIIIOOOOIiiO.......", "..........O32O..OIiiO.......", "...........O32O.OIiiO.......", "...........O32O.OIiiO.......",
			"............O32OOIiiO.......", "...........OIIIOOIIiO.......", "...........OIIIiiIiiO.......", "...........OIIIOOIiiO.......", "............OOO.OIiiO.......", ".................OiO........", "..................O.........", "............................"
		};

		private static readonly Dictionary<char, Color32> ToolInk = new Dictionary<char, Color32>
		{
			{
				'O',
				new Color32(50, 34, 26, byte.MaxValue)
			},
			{
				'1',
				new Color32(92, 58, 34, byte.MaxValue)
			},
			{
				'2',
				new Color32(138, 92, 54, byte.MaxValue)
			},
			{
				'3',
				new Color32(178, 128, 78, byte.MaxValue)
			},
			{
				'm',
				new Color32(92, 96, 106, byte.MaxValue)
			},
			{
				'M',
				new Color32(150, 156, 166, byte.MaxValue)
			},
			{
				'L',
				new Color32(212, 218, 226, byte.MaxValue)
			},
			{
				'g',
				new Color32(232, 190, 72, byte.MaxValue)
			}
		};

		private static readonly Dictionary<char, Color32> DirtInk = new Dictionary<char, Color32>
		{
			{
				'O',
				new Color32(46, 30, 20, byte.MaxValue)
			},
			{
				'a',
				new Color32(78, 52, 34, byte.MaxValue)
			},
			{
				'b',
				new Color32(112, 78, 50, byte.MaxValue)
			},
			{
				'c',
				new Color32(146, 106, 70, byte.MaxValue)
			},
			{
				'd',
				new Color32(88, 60, 38, byte.MaxValue)
			},
			{
				'k',
				new Color32(64, 44, 30, byte.MaxValue)
			}
		};

		private static readonly Dictionary<char, Color32> SandInk = new Dictionary<char, Color32>
		{
			{
				'O',
				new Color32(120, 96, 52, byte.MaxValue)
			},
			{
				'a',
				new Color32(178, 150, 92, byte.MaxValue)
			},
			{
				'b',
				new Color32(214, 188, 124, byte.MaxValue)
			},
			{
				'c',
				new Color32(236, 216, 160, byte.MaxValue)
			},
			{
				'e',
				new Color32(196, 166, 104, byte.MaxValue)
			}
		};

		private static readonly string[] ShovelMap = new string[28]
		{
			"............................", "..................OOOOO.....", ".................OO333O.....", ".................O332OOO....", ".................OO2223O....", ".................OO3222OO...", "................OO331221O...", "...............OO331OO11O...", "..............OO331OOOOOO...", "..............O331OO........",
			".............OO31OO.........", "............OO33OO..........", "...........OO331O...........", ".......OOOOO331OO...........", "......OOLLLm31OO............", ".....OOLLMmmmOO.............", "....OOLLMmmmMOO.............", "....OLLMMMmMmmO.............", "...OOLMMMMMmmOO.............", "...OLLMMMMMmOO..............",
			"...OLMMMMMmmO...............", "...OOMMMmmmOO...............", "....OLmmmOOO................", "....OOOOOO..................", "............................", "............................", "............................", "............................"
		};

		private static readonly string[] PickaxeMap = new string[28]
		{
			"............................", "........OOOO................", "........OLLOOOO.............", "........OLMMLLOO............", "........OOOmMMMOOO..........", "..........OOOMMMLOO.........", "............OOMMMMOOO.......", ".............OOMMmmmO.......", "..............OOMmmmOOO.....", "...............OOmmmMLOO....",
			"..............OO3mmmMMMOO...", ".............OO331OOMMMMOOO.", "............OO331OOOOmMMMLO.", "...........OO331OO..OOOMMmO.", "...........O331OO.....OOMMOO", "..........OO31OO.......OOMmO", ".........OO33OO.........OLmO", "........OO331O..........OOmO", ".......OO331OO...........OOO", "......OO331OO...............",
			".....OO331OO................", "....OO331OO.................", "....O331OO..................", "....O31OO...................", "....OOOO....................", "............................", "............................", "............................"
		};

		private static readonly string[] DirtMap = new string[28]
		{
			"............................", "............................", "............................", "............................", "............................", "............................", "............................", "............................", "............................", "..........OOOOOOOO..........",
			"........OOOdccccdOOO........", ".......OOdcdbbbbbdcOO.......", "......OOccbbbbdbkkbbOO......", ".....OOdbbbbbbbbbbbbcOO.....", ".....OccbkkbbbbbbbbbbaO.....", "....OObbbbdbbbbbbbbbbbOO....", "...OOcdbbbbbbdbbbbbbbdcOO...", "...OccbbbbbbbbbbbbbbbbbaO...", "..OObdbddbbbbdbbbbbkkbbbOO..", "..OcbdbdbbbbkkbdbbbbbbdbcO..",
			".OOcbbbbbbbbbbbbdbbbbbbbbOO.", ".OccbbbbbbbbbbbbbbbbbbbbbaO.", ".ObbbbbbbbbbbbbbbbbbbbbbbaO.", ".OaaaaaaaaaaaaaaaaaaaaaaaaO.", ".OOOOOOOOOOOOOOOOOOOOOOOOOO.", "............................", "............................", "............................"
		};

		private static readonly string[] SandMap = new string[28]
		{
			"............................", "............................", "............................", "............................", "............................", "............................", "............................", "............................", "............................", "..........OOOOOOOO..........",
			"........OOOccccccOOO........", ".......OOcccbbbbbbcOO.......", "......OOccbbbbbbbbbbOO......", ".....OOcebbbbbbbbbbbcOO.....", ".....OccbbebbbbbbbbebaO.....", "....OObbebbbbbbbbbbbbbOO....", "...OOcbbbbbbbebbbebbebcOO...", "...OccbbebbbbbbbbbbbbeeaO...", "..OObbbbebbbbbbbbbbebbbbOO..", "..OcbbbbbbbebbbebebebebbcO..",
			".OOcebbbbbbbbbbbebbebbbbbOO.", ".OccbbbbbbebbbbbebbbbbbbbaO.", ".ObbbbbbbbbbbebbbebbbbbbbaO.", ".OaaaaaaaaaaaaaaaaaaaaaaaaO.", ".OOOOOOOOOOOOOOOOOOOOOOOOOO.", "............................", "............................", "............................"
		};

		public static Sprite Light(int kind)
		{
			string text = "fp_light_" + kind;
			if (Made.TryGetValue(text, out var value) && value != null)
			{
				return value;
			}
			string[] map;
			switch (kind)
			{
			default:
				map = BrazierMap;
				break;
			case 2:
				map = LampMap;
				break;
			case 1:
				map = WallMap;
				break;
			case 0:
				map = StandingMap;
				break;
			}
			value = ToSprite(map, LightInk, text);
			Made[text] = value;
			return value;
		}

		public static Sprite Get(string id)
		{
			if (id == null)
			{
				return null;
			}
			if (Made.TryGetValue(id, out var value) && value != null)
			{
				return value;
			}
			switch (id)
			{
			case "tw_item_bandage":
				value = ToSprite(BandageMap, BandageInk, id);
				break;
			case "tw_item_salve":
				value = ToSprite(SalveMap, SalveInk, id);
				break;
			case "tw_item_torch":
				value = ToSprite(TorchMap, TorchInk, id);
				break;
			case "tw_item_bedroll":
				value = ToSprite(BedrollMap, BedrollInk, id);
				break;
			case "tw_item_lamppost":
				value = ToSprite(LampMap, LightInk, id);
				break;
			case "tw_item_brazier":
				value = ToSprite(BrazierMap, LightInk, id);
				break;
			case "tw_item_shovel":
				value = ToSprite(ShovelMap, ToolInk, id);
				break;
			case "tw_item_pickaxe":
				value = ToSprite(PickaxeMap, ToolInk, id);
				break;
			case "tw_item_dirt":
				value = ToSprite(DirtMap, DirtInk, id);
				break;
			case "tw_item_sand":
				value = ToSprite(SandMap, SandInk, id);
				break;
			default:
			{
				Supplies.Kind kind = Supplies.Of(id);
				if (kind != null && kind.Level > 1 && (kind.Base == "tw_item_shovel" || kind.Base == "tw_item_pickaxe"))
				{
					value = ToSprite(LevelPips((kind.Base == "tw_item_shovel") ? ShovelMap : PickaxeMap, kind.Level), ToolInk, id);
					break;
				}
				return null;
			}
			}
			Made[id] = value;
			return value;
		}

		private static string[] LevelPips(string[] map, int level)
		{
			char[][] array = new char[map.Length][];
			for (int i = 0; i < map.Length; i++)
			{
				array[i] = map[i].ToCharArray();
			}
			for (int j = 0; j < level - 1; j++)
			{
				int num = 22 - j * 5;
				int num2 = 22;
				for (int k = num2 - 1; k <= num2 + 2 && k < array.Length; k++)
				{
					for (int l = num - 1; l <= num + 2 && l < array[k].Length; l++)
					{
						array[k][l] = ((k == num2 - 1 || k == num2 + 2 || l == num - 1 || l == num + 2) ? 'O' : 'g');
					}
				}
			}
			string[] array2 = new string[array.Length];
			for (int m = 0; m < array.Length; m++)
			{
				array2[m] = new string(array[m]);
			}
			return array2;
		}

		private static Sprite ToSprite(string[] map, Dictionary<char, Color32> ink, string name)
		{
			Texture2D texture2D = new Texture2D(28, 28, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
				name = "fp_" + name
			};
			Color32[] array = new Color32[784];
			for (int i = 0; i < 28 && i < map.Length; i++)
			{
				for (int j = 0; j < 28 && j < map[i].Length; j++)
				{
					if (ink.TryGetValue(map[i][j], out var value))
					{
						array[(27 - i) * 28 + j] = value;
					}
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false);
			return Sprite.Create(texture2D, new Rect(0f, 0f, 28f, 28f), new Vector2(0.5f, 0.5f), 100f);
		}
	}
}
