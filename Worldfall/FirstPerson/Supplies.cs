using System;
using System.Collections.Generic;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class Supplies
	{
		internal sealed class Kind
		{
			public string Id;

			public string Name;

			public string One;

			public int Stack;

			public float Heal;

			public float Seconds;

			public int Price;

			public string Res1;

			public string Res2;

			public int Amount1;

			public int Amount2;

			public int Makes;

			public int Rank;

			public bool Dug;

			public int Level;

			public string Base;
		}

		public const string Bandage = "tw_item_bandage";

		public const string Salve = "tw_item_salve";

		public const string Torch = "tw_item_torch";

		public const string Bedroll = "tw_item_bedroll";

		public const string LampPost = "tw_item_lamppost";

		public const string Brazier = "tw_item_brazier";

		public const string Shovel = "tw_item_shovel";

		public const string Pickaxe = "tw_item_pickaxe";

		public const string Dirt = "tw_item_dirt";

		public const string Sand = "tw_item_sand";

		private const string KitKey = "fp_kit";

		private const string TorchKitKey = "fp_kit_t";

		private const string DigKitKey = "fp_kit_d";

		public static readonly Kind[] All = new Kind[16]
		{
			new Kind
			{
				Id = "tw_item_bandage",
				Name = "Bandages",
				One = "bandage",
				Stack = 10,
				Heal = 0.25f,
				Seconds = 8f,
				Price = 4,
				Res1 = "herbs",
				Amount1 = 2,
				Res2 = "wheat",
				Amount2 = 1,
				Makes = 2,
				Rank = 0
			},
			new Kind
			{
				Id = "tw_item_salve",
				Name = "Salves",
				One = "salve",
				Stack = 5,
				Heal = 0.5f,
				Seconds = 10f,
				Price = 12,
				Res1 = "herbs",
				Amount1 = 2,
				Res2 = "honey",
				Amount2 = 1,
				Makes = 1,
				Rank = 1
			},
			new Kind
			{
				Id = "tw_item_torch",
				Name = "Torches",
				One = "torch",
				Stack = 50,
				Price = 3,
				Res1 = "wood",
				Amount1 = 1,
				Makes = 1,
				Rank = 0
			},
			new Kind
			{
				Id = "tw_item_bedroll",
				Name = "Bedrolls",
				One = "bedroll",
				Stack = 1,
				Price = 15,
				Res1 = "leather",
				Amount1 = 2,
				Res2 = "wheat",
				Amount2 = 2,
				Makes = 1,
				Rank = 0
			},
			new Kind
			{
				Id = "tw_item_lamppost",
				Name = "Lamp posts",
				One = "lamp post",
				Stack = 50,
				Price = 9,
				Res1 = "wood",
				Amount1 = 2,
				Res2 = "common_metals",
				Amount2 = 1,
				Makes = 1,
				Rank = 0
			},
			new Kind
			{
				Id = "tw_item_brazier",
				Name = "Braziers",
				One = "brazier",
				Stack = 50,
				Price = 10,
				Res1 = "stone",
				Amount1 = 3,
				Res2 = "common_metals",
				Amount2 = 1,
				Makes = 1,
				Rank = 0
			},
			new Kind
			{
				Id = "tw_item_shovel",
				Name = "Shovels",
				One = "shovel",
				Stack = 1,
				Price = 15,
				Res1 = "wood",
				Amount1 = 2,
				Res2 = "stone",
				Amount2 = 1,
				Makes = 1,
				Rank = 0
			},
			new Kind
			{
				Id = "tw_item_pickaxe",
				Name = "Pickaxes",
				One = "pickaxe",
				Stack = 1,
				Price = 25,
				Res1 = "wood",
				Amount1 = 2,
				Res2 = "common_metals",
				Amount2 = 2,
				Makes = 1,
				Rank = 1
			},
			new Kind
			{
				Id = "tw_item_shovel_2",
				Name = "Level 2 shovels",
				One = "level 2 shovel",
				Stack = 1,
				Price = 45,
				Level = 2,
				Base = "tw_item_shovel"
			},
			new Kind
			{
				Id = "tw_item_shovel_3",
				Name = "Level 3 shovels",
				One = "level 3 shovel",
				Stack = 1,
				Price = 105,
				Level = 3,
				Base = "tw_item_shovel"
			},
			new Kind
			{
				Id = "tw_item_shovel_4",
				Name = "Level 4 shovels",
				One = "level 4 shovel",
				Stack = 1,
				Price = 225,
				Level = 4,
				Base = "tw_item_shovel"
			},
			new Kind
			{
				Id = "tw_item_pickaxe_2",
				Name = "Level 2 pickaxes",
				One = "level 2 pickaxe",
				Stack = 1,
				Price = 55,
				Level = 2,
				Base = "tw_item_pickaxe"
			},
			new Kind
			{
				Id = "tw_item_pickaxe_3",
				Name = "Level 3 pickaxes",
				One = "level 3 pickaxe",
				Stack = 1,
				Price = 115,
				Level = 3,
				Base = "tw_item_pickaxe"
			},
			new Kind
			{
				Id = "tw_item_pickaxe_4",
				Name = "Level 4 pickaxes",
				One = "level 4 pickaxe",
				Stack = 1,
				Price = 235,
				Level = 4,
				Base = "tw_item_pickaxe"
			},
			new Kind
			{
				Id = "tw_item_dirt",
				Name = "Dirt",
				One = "dirt",
				Stack = 50,
				Price = 1,
				Dug = true
			},
			new Kind
			{
				Id = "tw_item_sand",
				Name = "Sand",
				One = "sand",
				Stack = 50,
				Price = 1,
				Dug = true
			}
		};

		private static Dictionary<string, int> _byId;

		private static long _readId = -2L;

		private static string _readRaw;

		private static readonly int[] _stored = new int[All.Length];

		private static bool _torchKit;

		private static bool _digKit;

		private static Dictionary<string, int> ById
		{
			get
			{
				if (_byId != null)
				{
					return _byId;
				}
				Dictionary<string, int> dictionary = new Dictionary<string, int>();
				for (int i = 0; i < All.Length; i++)
				{
					if (!dictionary.ContainsKey(All[i].Id))
					{
						dictionary[All[i].Id] = i;
					}
				}
				return _byId = dictionary;
			}
		}

		public static string Leveled(string tool, int level)
		{
			if (level > 1)
			{
				return tool + "_" + level;
			}
			return tool;
		}

		public static Kind Of(string id)
		{
			if (id == null || !id.StartsWith("tw_item_", StringComparison.Ordinal))
			{
				return null;
			}
			if (!ById.TryGetValue(id, out var value))
			{
				return null;
			}
			return All[value];
		}

		public static bool Is(string id)
		{
			return Of(id) != null;
		}

		private static int IndexOf(Kind k)
		{
			if (!ById.TryGetValue(k.Id, out var value) || All[value] != k)
			{
				return Array.IndexOf(All, k);
			}
			return value;
		}

		public static int Count(Actor a, string id)
		{
			Kind kind = Of(id);
			if (a == null || kind == null)
			{
				return 0;
			}
			bool flag = Stored(a);
			if (kind.Id == "tw_item_torch")
			{
				flag = _torchKit;
			}
			else if (kind.Id == "tw_item_shovel" || kind.Id == "tw_item_pickaxe")
			{
				flag = _digKit;
			}
			int num = _stored[IndexOf(kind)];
			if (!flag)
			{
				return num + Seed(a, kind);
			}
			return num;
		}

		private static bool Stored(Actor a)
		{
			long num = -1L;
			string pResult = null;
			int pResult2 = 0;
			int pResult3 = 0;
			int pResult4 = 0;
			try
			{
				num = a.data.id;
				a.data.get("fp_items", out pResult, null);
				a.data.get("fp_kit", out pResult2, 0);
				a.data.get("fp_kit_t", out pResult3, 0);
				a.data.get("fp_kit_d", out pResult4, 0);
			}
			catch
			{
			}
			_torchKit = pResult3 != 0;
			_digKit = pResult4 != 0;
			if (num != _readId || !string.Equals(pResult, _readRaw, StringComparison.Ordinal))
			{
				_readId = num;
				_readRaw = pResult;
				for (int i = 0; i < _stored.Length; i++)
				{
					_stored[i] = 0;
				}
				if (!string.IsNullOrEmpty(pResult))
				{
					string[] array = pResult.Split('|');
					foreach (string text in array)
					{
						int num2 = text.LastIndexOf(':');
						if (num2 > 0 && int.TryParse(text.Substring(num2 + 1), out var result) && result > 0)
						{
							Kind kind = Of(text.Substring(0, num2));
							if (kind != null)
							{
								_stored[IndexOf(kind)] += result;
							}
						}
					}
				}
			}
			return pResult2 != 0;
		}

		public static void Give(Actor a, string id, int n)
		{
			if (a != null && n > 0 && Of(id) != null)
			{
				Ensure(a);
				Items.Give(a, id, n);
			}
		}

		public static bool Take(Actor a, string id, int n)
		{
			if (a == null || n <= 0 || Of(id) == null)
			{
				return false;
			}
			Ensure(a);
			return Items.Take(a, id, n);
		}

		public static void Clear(Actor a)
		{
			Kind[] all = All;
			foreach (Kind kind in all)
			{
				int num = Count(a, kind.Id);
				if (num > 0)
				{
					Take(a, kind.Id, num);
				}
			}
		}

		public static void Own(Actor a)
		{
			if (a != null)
			{
				Ensure(a);
			}
		}

		private static void Ensure(Actor a)
		{
			bool flag = Stored(a);
			bool torchKit = _torchKit;
			bool digKit = _digKit;
			if (flag && torchKit && digKit)
			{
				return;
			}
			try
			{
				Kind[] all = All;
				foreach (Kind kind in all)
				{
					if (!((kind.Id == "tw_item_torch") ? torchKit : ((kind.Id == "tw_item_shovel" || kind.Id == "tw_item_pickaxe") ? digKit : flag)))
					{
						int num = Seed(a, kind);
						if (num > 0)
						{
							Items.Give(a, kind.Id, num);
						}
					}
				}
				if (!flag)
				{
					a.data.set("fp_kit", 1);
				}
				if (!torchKit)
				{
					a.data.set("fp_kit_t", 1);
				}
				if (!digKit)
				{
					a.data.set("fp_kit_d", 1);
				}
			}
			catch (Exception ex)
			{
				Log.Error("supplies: kit: " + ex.Message);
			}
		}

		public static void AddTo(Actor a, List<KeyValuePair<string, int>> list)
		{
			if (a == null || list == null)
			{
				return;
			}
			bool flag = Stored(a);
			bool torchKit = _torchKit;
			bool digKit = _digKit;
			for (int i = 0; i < All.Length; i++)
			{
				Kind kind = All[i];
				int num = (((kind.Id == "tw_item_torch") ? torchKit : ((kind.Id == "tw_item_shovel" || kind.Id == "tw_item_pickaxe") ? digKit : flag)) ? _stored[i] : (_stored[i] + Seed(a, kind)));
				if (num > 0)
				{
					list.Add(new KeyValuePair<string, int>(kind.Id, num));
				}
			}
		}

		private static int Seed(Actor a, Kind k)
		{
			try
			{
				string text = ((a.asset != null) ? a.asset.id : null);
				if (k.Id == "tw_item_torch")
				{
					return (text == "bandit" || (a.isAdult() && Dialogue.IsPerson(a))) ? 1 : 0;
				}
				if (k.Id == "tw_item_shovel" || k.Id == "tw_item_pickaxe")
				{
					if (!a.isAdult() || !Dialogue.IsPerson(a) || a.citizen_job == null)
					{
						return 0;
					}
					string text2 = a.citizen_job.id ?? "";
					return (!(k.Id == "tw_item_shovel")) ? (text2.Contains("miner") ? 1 : 0) : ((text2.Contains("farmer") || text2.Contains("builder")) ? 1 : 0);
				}
				if (k.Id != "tw_item_bandage" && k.Id != "tw_item_salve")
				{
					return 0;
				}
				bool flag = k.Id == "tw_item_bandage";
				float num;
				int num2;
				if (text == "bandit")
				{
					num = (flag ? 0.6f : 0.12f);
					num2 = ((!flag) ? 1 : 3);
				}
				else
				{
					if (!a.isAdult() || !Dialogue.IsPerson(a))
					{
						return 0;
					}
					if (a.isWarrior())
					{
						num = (flag ? 0.4f : 0.08f);
						num2 = ((!flag) ? 1 : 2);
					}
					else
					{
						if (a.city != null)
						{
							return 0;
						}
						num = (flag ? 0.35f : 0.05f);
						num2 = ((!flag) ? 1 : 2);
					}
				}
				ulong num3 = (ulong)((a.data.id * -7046029254386353131L) ^ ((IndexOf(k) + 1) * -4417276706812531889L));
				num3 ^= num3 >> 29;
				num3 *= 13787848793156543929uL;
				num3 ^= num3 >> 32;
				if ((float)(num3 % 1000) / 1000f >= num)
				{
					return 0;
				}
				return 1 + (int)(num3 / 1000 % (ulong)num2);
			}
			catch
			{
				return 0;
			}
		}

		public static string Name(string id)
		{
			return Of(id)?.Name;
		}

		public static string Counted(Kind k, int n)
		{
			if (!k.Dug)
			{
				if (n != 1)
				{
					return n + " " + k.One + (k.One.EndsWith("ch", StringComparison.Ordinal) ? "es" : "s");
				}
				return "a " + k.One;
			}
			if (n != 1)
			{
				return n + " " + k.One;
			}
			return "some " + k.One;
		}

		public static bool Heals(Kind k)
		{
			if (k != null)
			{
				return k.Heal > 0f;
			}
			return false;
		}

		public static Sprite Icon(string id)
		{
			return SupplyIcons.Get(id);
		}
	}
}
