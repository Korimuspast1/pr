using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class Wardrobe
	{
		public enum Job : byte
		{
			None,
			Farmer,
			Miner,
			Smith,
			Hunter,
			Fisher,
			Woodcutter,
			Builder,
			Gatherer,
			Trader,
			Elder
		}

		public enum Hat : byte
		{
			None,
			Straw,
			Miner,
			Hunter,
			Wool,
			Cap,
			Hood,
			Scarf
		}

		public enum Age : byte
		{
			Child,
			Young,
			Grown,
			Elder
		}

		public enum Build : byte
		{
			Average,
			Thin,
			Stout,
			Broad
		}

		[Flags]
		public enum Hurt : byte
		{
			None = 0,
			Eyepatch = 1,
			Scars = 2,
			Burns = 4,
			Crippled = 8
		}

		public struct Who
		{
			public long Id;

			public string Race;

			public bool Female;

			public Age Age;

			public Job Job;

			public Hurt Hurt;

			public bool Role;
		}

		public struct Look
		{
			public sbyte Face;

			public sbyte Hair;

			public sbyte Beard;

			public sbyte Cut;

			public sbyte Belt;

			public sbyte Skirt;

			public Hat Hat;

			public Job Job;

			public bool Aged;

			public bool Patch;

			public bool Scar;

			public bool ScarBold;

			public bool Burns;

			public bool Bandage;

			public bool Limp;

			public bool Crutch;

			public bool Cane;

			public bool Trousers;

			public float Tall;

			public Build Build;

			public uint Shirt;

			public uint Pants;

			public uint Cloth;

			public bool Set;

			public ulong Key()
			{
				ulong num = (byte)Face | ((ulong)(byte)Hair << 8) | ((ulong)(byte)Beard << 16) | ((ulong)(byte)Cut << 24) | ((ulong)(byte)Belt << 32) | ((ulong)(byte)Skirt << 40) | ((ulong)Hat << 48) | ((ulong)Job << 52);
				ulong num2 = (ulong)(Aged ? 1 : 0) | (ulong)(Patch ? 2 : 0) | (ulong)(Scar ? 4 : 0) | (ulong)(ScarBold ? 8 : 0) | (ulong)(Burns ? 16 : 0) | (ulong)(Bandage ? 32 : 0) | (ulong)(Limp ? 64 : 0) | (ulong)(Crutch ? 128 : 0) | (ulong)(Cane ? 256 : 0) | (ulong)(Trousers ? 512 : 0) | ((ulong)Build << 10) | ((ulong)Math.Round(Tall * 100f) << 12);
				return num ^ (ulong)((long)num2 * -7046029254386353131L) ^ (ulong)(Shirt * -4417276706812531889L) ^ (ulong)((long)Pants * 1609587929392839161L) ^ ((ulong)Cloth << 7);
			}
		}

		public sealed class Parts
		{
			public int Head = -1;

			public int Aged = -1;

			public int[] Faces = Empty;

			public int[] Hairs = Empty;

			public int[] Beards = Empty;

			public int[] Cuts = Empty;

			public int[] Belts = Empty;

			public int[] Skirts = Empty;

			public readonly ulong[] Hats = new ulong[HatNames.Length];

			public readonly ulong[] Jobs = new ulong[JobNames.Length];

			public readonly ulong[] Marks = new ulong[MarkNames.Length];

			public ulong Managed;

			public bool[] TallHair = new bool[0];

			public bool[] ElderHair = new bool[0];

			public int HatHair = -1;

			public bool HoodHides = true;

			public bool Has;

			private static readonly int[] Empty = new int[0];
		}

		public struct Counts
		{
			public int Faces;

			public int Hairs;

			public int Beards;

			public int Cuts;

			public int Belts;

			public int Skirts;

			public bool[] ElderHair;

			public bool HasHats;

			public static Counts Of(Parts p)
			{
				if (p != null)
				{
					return new Counts
					{
						Faces = p.Faces.Length,
						Hairs = p.Hairs.Length,
						Beards = p.Beards.Length,
						Cuts = p.Cuts.Length,
						Belts = p.Belts.Length,
						Skirts = p.Skirts.Length,
						ElderHair = p.ElderHair,
						HasHats = (p.Hats[5] != 0L || p.Hats[7] != 0)
					};
				}
				return default(Counts);
			}
		}

		public struct Paint
		{
			public int Shirt;

			public int ShirtDark;

			public int Pants;

			public int Cloth;

			public int ClothDark;

			public int Kingdom;

			public int Scar;

			public int Burn;

			public int Hair;

			public int Stubble;

			public int Crease;

			public int Eyes;

			public MeshModel Model;

			public static Paint Of(MeshModel m)
			{
				if (m == null)
				{
					return new Paint
					{
						Shirt = -1,
						ShirtDark = -1,
						Pants = -1,
						Cloth = -1,
						ClothDark = -1,
						Kingdom = -1,
						Scar = -1,
						Burn = -1,
						Hair = -1,
						Stubble = -1,
						Crease = -1,
						Eyes = -1
					};
				}
				lock (PaintCache)
				{
					if (PaintCache.TryGetValue(m, out var value))
					{
						return value;
					}
					value = new Paint
					{
						Model = m,
						Shirt = m.SlotIndex("shirt"),
						ShirtDark = m.SlotIndex("shirt_dark"),
						Pants = m.SlotIndex("pants"),
						Cloth = m.SlotIndex("cloth"),
						ClothDark = m.SlotIndex("cloth_dark"),
						Kingdom = m.SlotIndex("kingdom"),
						Scar = m.SlotIndex("scar"),
						Burn = m.SlotIndex("burn"),
						Hair = m.SlotIndex("hair"),
						Stubble = m.SlotIndex("stubble"),
						Crease = m.SlotIndex("crease"),
						Eyes = m.SlotIndex("eyes")
					};
					if (PaintCache.Count > 400)
					{
						PaintCache.Clear();
					}
					PaintCache[m] = value;
					return value;
				}
			}
		}

		private static readonly string[] JobNames = new string[11]
		{
			null, "farmer", "miner", "smith", "hunter", "fisher", "woodcutter", "builder", "gatherer", "trader",
			"elder"
		};

		private static readonly string[] HatNames = new string[8] { null, "straw", "miner", "hunter", "wool", "cap", "hood", "scarf" };

		private static readonly string[] MarkNames = new string[5] { "patch", "scar", "scar_bold", "burns", "bandage" };

		private static readonly Dictionary<MeshModel, Parts> PartsCache = new Dictionary<MeshModel, Parts>();

		private static readonly uint[] HumanDyes = new uint[12]
		{
			C(222, 206, 172),
			C(198, 176, 136),
			C(122, 86, 58),
			C(152, 76, 52),
			C(98, 114, 66),
			C(92, 112, 142),
			C(188, 148, 72),
			C(134, 130, 124),
			C(158, 66, 58),
			C(74, 70, 68),
			C(110, 96, 140),
			C(176, 120, 90)
		};

		private static readonly uint[] ElfDyes = new uint[10]
		{
			C(152, 172, 122),
			C(74, 112, 72),
			C(192, 198, 202),
			C(212, 198, 142),
			C(142, 172, 192),
			C(54, 102, 102),
			C(112, 132, 92),
			C(228, 222, 202),
			C(122, 140, 170),
			C(170, 150, 190)
		};

		private static readonly uint[] OrcDyes = new uint[9]
		{
			C(132, 102, 72),
			C(84, 62, 46),
			C(64, 60, 58),
			C(142, 72, 46),
			C(202, 190, 160),
			C(98, 98, 62),
			C(122, 42, 38),
			C(112, 92, 72),
			C(150, 128, 92)
		};

		private static readonly uint[] DwarfDyes = new uint[9]
		{
			C(152, 72, 54),
			C(112, 76, 52),
			C(72, 92, 62),
			C(182, 142, 62),
			C(92, 98, 106),
			C(112, 52, 62),
			C(202, 182, 142),
			C(142, 92, 58),
			C(60, 76, 96)
		};

		private static readonly Dictionary<MeshModel, Paint> PaintCache = new Dictionary<MeshModel, Paint>();

		public static Hat HatOf(Job job)
		{
			switch (job)
			{
			case Job.Farmer:
				return Hat.Straw;
			case Job.Miner:
				return Hat.Miner;
			case Job.Hunter:
				return Hat.Hunter;
			case Job.Woodcutter:
				return Hat.Wool;
			default:
				return Hat.None;
			}
		}

		public static bool Rough(Job job)
		{
			if (job != Job.Hunter && job != Job.Miner && job != Job.Woodcutter && job != Job.Smith)
			{
				return job == Job.Fisher;
			}
			return true;
		}

		public static Parts PartsOf(MeshModel m)
		{
			if (m == null)
			{
				return null;
			}
			lock (PartsCache)
			{
				if (PartsCache.TryGetValue(m, out var value))
				{
					return value;
				}
				value = Read(m);
				if (PartsCache.Count > 400)
				{
					PartsCache.Clear();
				}
				PartsCache[m] = value;
				return value;
			}
		}

		private static Parts Read(MeshModel m)
		{
			Parts parts = new Parts();
			SortedList<int, int> sortedList = new SortedList<int, int>();
			SortedList<int, int> sortedList2 = new SortedList<int, int>();
			SortedList<int, int> sortedList3 = new SortedList<int, int>();
			SortedList<int, int> sortedList4 = new SortedList<int, int>();
			SortedList<int, int> sortedList5 = new SortedList<int, int>();
			SortedList<int, int> sortedList6 = new SortedList<int, int>();
			for (int i = 0; i < m.Parts.Length && i < 64; i++)
			{
				string text = m.Parts[i].Name ?? "";
				ulong num = (ulong)(1L << i);
				if (text == "head")
				{
					parts.Head = i;
				}
				else if (text == "aged")
				{
					parts.Aged = i;
					parts.Managed |= num;
				}
				else if (Numbered(text, "face_", sortedList, i) || Numbered(text, "hair_", sortedList2, i) || Numbered(text, "beard_", sortedList3, i) || Numbered(text, "cut_", sortedList4, i) || Numbered(text, "belt_", sortedList5, i) || Numbered(text, "skirt_", sortedList6, i))
				{
					parts.Managed |= num;
				}
				else if (text.StartsWith("hat_", StringComparison.Ordinal))
				{
					int num2 = Array.IndexOf(HatNames, text.Substring(4));
					if (num2 > 0)
					{
						parts.Hats[num2] |= num;
					}
					parts.Managed |= num;
				}
				else if (text.StartsWith("job_", StringComparison.Ordinal))
				{
					string text2 = text.Substring(4);
					int num3 = text2.IndexOf('_');
					int num4 = Array.IndexOf(JobNames, (num3 > 0) ? text2.Substring(0, num3) : text2);
					if (num4 > 0)
					{
						parts.Jobs[num4] |= num;
					}
					parts.Managed |= num;
				}
				else if (text.StartsWith("mark_", StringComparison.Ordinal))
				{
					int num5 = Array.IndexOf(MarkNames, text.Substring(5));
					if (num5 >= 0)
					{
						parts.Marks[num5] |= num;
					}
					parts.Managed |= num;
				}
			}
			parts.Faces = Dense(sortedList);
			parts.Hairs = Dense(sortedList2);
			parts.Beards = Dense(sortedList3);
			parts.Cuts = Dense(sortedList4);
			parts.Belts = Dense(sortedList5);
			parts.Skirts = Dense(sortedList6);
			parts.Has = parts.Faces.Length != 0 || parts.Hairs.Length != 0;
			parts.TallHair = Flags(m.Info("hair_tall"), parts.Hairs.Length);
			parts.ElderHair = Flags(m.Info("hair_elder"), parts.Hairs.Length);
			parts.HatHair = (int)m.InfoFloat("hair_hat", -1f);
			return parts;
		}

		private static bool Numbered(string n, string prefix, SortedList<int, int> into, int part)
		{
			if (!n.StartsWith(prefix, StringComparison.Ordinal))
			{
				return false;
			}
			if (int.TryParse(n.Substring(prefix.Length), out var result) && result >= 0 && result < 32)
			{
				into[result] = part;
			}
			return true;
		}

		private static int[] Dense(SortedList<int, int> list)
		{
			if (list.Count == 0)
			{
				return new int[0];
			}
			int num = list.Keys[list.Count - 1] + 1;
			int[] array = new int[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = -1;
			}
			foreach (KeyValuePair<int, int> item in list)
			{
				array[item.Key] = item.Value;
			}
			return array;
		}

		private static bool[] Flags(string list, int n)
		{
			bool[] array = new bool[n];
			if (string.IsNullOrEmpty(list))
			{
				return array;
			}
			string[] array2 = list.Split(',');
			for (int i = 0; i < array2.Length; i++)
			{
				if (int.TryParse(array2[i].Trim(), out var result) && result >= 0 && result < n)
				{
					array[result] = true;
				}
			}
			return array;
		}

		public static ulong Hidden(Parts p, in Look look)
		{
			if (p == null || !p.Has)
			{
				return 0uL;
			}
			ulong shown = 0uL;
			bool num = Pick(p.Faces, look.Face, ref shown);
			int num2 = look.Hair;
			if (look.Hat != Hat.None && num2 >= 0 && p.Hats[(uint)look.Hat] != 0L)
			{
				if (look.Hat == Hat.Hunter || look.Hat == Hat.Hood || look.Hat == Hat.Scarf)
				{
					num2 = -1;
				}
				else if (num2 < p.TallHair.Length && p.TallHair[num2])
				{
					num2 = p.HatHair;
				}
			}
			Pick(p.Hairs, num2, ref shown);
			Pick(p.Beards, look.Beard, ref shown);
			Pick(p.Cuts, look.Cut, ref shown);
			Pick(p.Belts, look.Belt, ref shown);
			if (!look.Trousers)
			{
				Pick(p.Skirts, look.Skirt, ref shown);
			}
			shown |= p.Hats[(uint)look.Hat] | p.Jobs[(uint)look.Job];
			if (look.Aged && p.Aged >= 0)
			{
				shown |= (ulong)(1L << p.Aged);
			}
			if (look.Patch)
			{
				shown |= p.Marks[0];
			}
			if (look.Scar)
			{
				shown |= ((look.ScarBold && p.Marks[2] != 0L) ? p.Marks[2] : p.Marks[1]);
			}
			if (look.Burns)
			{
				shown |= p.Marks[3];
			}
			if (look.Bandage)
			{
				shown |= p.Marks[4];
			}
			ulong num3 = p.Managed & ~shown;
			if (num && p.Head >= 0)
			{
				num3 |= (ulong)(1L << p.Head);
			}
			return num3;
		}

		private static bool Pick(int[] group, int which, ref ulong shown)
		{
			if (which < 0 || which >= group.Length || group[which] < 0)
			{
				return false;
			}
			shown |= (ulong)(1L << group[which]);
			return true;
		}

		public static Look Plain(bool female)
		{
			return new Look
			{
				Set = true,
				Face = 0,
				Hair = 0,
				Beard = -1,
				Cut = 0,
				Belt = 0,
				Skirt = (sbyte)((!female) ? (-1) : 0),
				Tall = 1f
			};
		}

		public static Look Choose(in Who w, Counts body, Counts adult)
		{
			Look look = new Look
			{
				Set = true,
				Face = -1,
				Hair = -1,
				Beard = -1,
				Cut = -1,
				Belt = -1,
				Skirt = -1,
				Tall = 1f
			};
			ulong seed = Seed(w.Id, w.Race, w.Female);
			bool flag = w.Age == Age.Child;
			bool flag2 = w.Age == Age.Elder;
			string text = w.Race ?? "";
			look.Face = Variant(seed, 1, (adult.Faces > 0) ? adult.Faces : body.Faces);
			int num = ((adult.Hairs > 0) ? adult.Hairs : body.Hairs);
			bool[] array = adult.ElderHair ?? body.ElderHair;
			int num2 = ((array != null) ? (num - Count(array)) : num);
			look.Hair = Variant(seed, 2, num);
			if (array != null && num2 > 0 && num2 < num)
			{
				int num3 = Variant(seed, 2, num2);
				for (int i = 0; i < num; i++)
				{
					if ((i >= array.Length || !array[i]) && num3-- == 0)
					{
						look.Hair = (sbyte)i;
						break;
					}
				}
			}
			if (flag2 && array != null && Chance(seed, 3, 0.55f))
			{
				int num4 = Variant(seed, 4, Count(array));
				for (int j = 0; j < array.Length; j++)
				{
					if (array[j] && num4-- == 0)
					{
						look.Hair = (sbyte)j;
						break;
					}
				}
			}
			if (!flag && !w.Female && body.Beards > 0)
			{
				float p = ((text == "dwarf") ? 1f : ((text == "elf") ? 0.17f : 0.72f));
				if (Chance(seed, 5, p))
				{
					look.Beard = Variant(seed, 6, body.Beards);
				}
			}
			look.Job = ((!flag && !w.Role) ? ((w.Job == Job.None && flag2) ? Job.Elder : w.Job) : Job.None);
			look.Hat = HatOf(look.Job);
			if (look.Hat == Hat.None && body.HasHats && text != "elf" && !w.Role && Chance(seed, 7, flag ? 0.15f : 0.25f))
			{
				int hat;
				switch ((int)(Bits(seed, 8) % 3))
				{
				default:
					hat = 7;
					break;
				case 1:
					hat = 6;
					break;
				case 0:
					hat = 5;
					break;
				}
				look.Hat = (Hat)hat;
			}
			look.Cut = Variant(seed, 9, body.Cuts);
			look.Belt = Variant(seed, 10, body.Belts);
			look.Trousers = w.Female && !flag && Rough(look.Job);
			if (w.Female && !look.Trousers)
			{
				look.Skirt = Variant(seed, 11, body.Skirts);
			}
			look.Aged = flag2;
			bool flag3 = Chance(seed, 12, 0.22f);
			look.Patch = (w.Hurt & Hurt.Eyepatch) != 0;
			look.Scar = (w.Hurt & Hurt.Scars) != 0;
			look.ScarBold = look.Scar && flag3;
			look.Burns = (w.Hurt & Hurt.Burns) != 0;
			look.Bandage = flag3 && (look.Burns || look.Scar) && Chance(seed, 13, 0.6f);
			look.Crutch = (w.Hurt & Hurt.Crippled) != 0 && flag3 && !flag;
			look.Limp = (w.Hurt & Hurt.Crippled) != Hurt.None && !look.Crutch;
			look.Cane = flag2 && !look.Crutch && Chance(seed, 14, 0.5f);
			float num5 = Unit(seed, 15);
			look.Tall = ((num5 < 0.25f) ? 0.94f : ((num5 > 0.75f) ? 1.06f : 1f));
			look.Tall += (Unit(seed, 16) - 0.5f) * 0.02f;
			if (!flag)
			{
				look.Build = (Build)(Bits(seed, 17) % 4);
			}
			Dyes(text, seed, ref look);
			return look;
		}

		private static int Count(bool[] a)
		{
			int num = 0;
			for (int i = 0; i < a.Length; i++)
			{
				if (a[i])
				{
					num++;
				}
			}
			return num;
		}

		private static uint C(int r, int g, int b)
		{
			return TerrainCache.Rgb(r, g, b);
		}

		private static uint[] DyesOf(string race)
		{
			switch (race)
			{
			default:
				return HumanDyes;
			case "dwarf":
				return DwarfDyes;
			case "orc":
				return OrcDyes;
			case "elf":
				return ElfDyes;
			}
		}

		private static void Dyes(string race, ulong seed, ref Look look)
		{
			uint[] array = DyesOf(race);
			int num = (int)(Bits(seed, 20) % (ulong)array.Length);
			int num2 = (int)(Bits(seed, 21) % (ulong)(array.Length - 1));
			if (num2 >= num)
			{
				num2++;
			}
			int num3 = (int)(Bits(seed, 22) % (ulong)array.Length);
			look.Shirt = array[num];
			look.Pants = array[num2];
			look.Cloth = array[num3];
		}

		public static void Colour(MeshModel model, uint[] palette, in Look look, uint kingdom, uint skin)
		{
			Colour(Paint.Of(model), palette, in look, kingdom, skin);
		}

		public static void Colour(in Paint s, uint[] palette, in Look look, uint kingdom, uint skin)
		{
			if (palette == null)
			{
				return;
			}
			Put(palette, s.Shirt, look.Shirt);
			Put(palette, s.ShirtDark, TerrainCache.Scale(look.Shirt, 0.68f));
			Put(palette, s.Pants, look.Pants);
			Put(palette, s.Cloth, look.Cloth);
			Put(palette, s.ClothDark, TerrainCache.Scale(look.Cloth, 0.7f));
			if (kingdom != 0)
			{
				Put(palette, s.Kingdom, kingdom);
			}
			if (skin != 0)
			{
				Put(palette, s.Scar, TerrainCache.Lerp(TerrainCache.Scale(skin, 0.85f), C(190, 112, 108), 0.5f));
				Put(palette, s.Burn, TerrainCache.Lerp(TerrainCache.Scale(skin, 0.65f), C(150, 62, 52), 0.62f));
				Put(palette, s.Crease, TerrainCache.Lerp(TerrainCache.Scale(skin, 0.7f), C(120, 70, 60), 0.18f));
				uint num = ((s.Hair >= 0 && s.Hair < palette.Length) ? palette[s.Hair] : 0u);
				if (num != 0)
				{
					Put(palette, s.Stubble, TerrainCache.Lerp(TerrainCache.Scale(skin, 0.82f), num, 0.55f));
				}
				uint num2 = ((s.Eyes >= 0 && s.Eyes < palette.Length) ? palette[s.Eyes] : 0u);
				if (num2 != 0)
				{
					Put(palette, s.Eyes, TerrainCache.Lerp(num2, C(26, 22, 22), 0.8f));
				}
			}
		}

		private static void Put(uint[] palette, int slot, uint c)
		{
			if (slot >= 0 && slot < palette.Length)
			{
				palette[slot] = c | 0xFF000000u;
			}
		}

		public static void Shape(PersonRig rig, MeshInstance m, Build build)
		{
			if (build != Build.Average && m.PartRotation != null)
			{
				float num;
				float num2;
				float num3;
				float num4;
				float num5;
				float num6;
				switch (build)
				{
				case Build.Thin:
					num = 0.94f;
					num2 = 0.95f;
					num3 = 0.93f;
					num4 = 0.94f;
					num5 = 0.91f;
					num6 = 0.93f;
					break;
				case Build.Stout:
					num = 1.08f;
					num2 = 1.1f;
					num3 = 1.07f;
					num4 = 1.1f;
					num5 = 1.05f;
					num6 = 1.06f;
					break;
				default:
					num = 1.02f;
					num2 = 1f;
					num3 = 1.09f;
					num4 = 1.03f;
					num5 = 1.08f;
					num6 = 1.03f;
					break;
				}
				Widen(m, rig.Hips, num, num2, 1f, 1f);
				Widen(m, rig.Torso, num3, num4, num, num2);
				Widen(m, rig.Head, 1f, 1f, num3, num4);
				Widen(m, rig.UpperArmL, num5, num5, num3, num4);
				Widen(m, rig.UpperArmR, num5, num5, num3, num4);
				Widen(m, rig.UpperLegL, num6, num6, num, num2);
				Widen(m, rig.UpperLegR, num6, num6, num, num2);
			}
		}

		private static void Widen(MeshInstance m, int part, float x, float y, float px, float py)
		{
			if (part >= 0 && part < m.PartRotation.Length)
			{
				Mat3 mat = m.PartRotation[part];
				float num = 1f / px;
				float num2 = 1f / py;
				mat = new Mat3(new Vec3(mat.C0.X * num, mat.C0.Y * num2, mat.C0.Z) * x, new Vec3(mat.C1.X * num, mat.C1.Y * num2, mat.C1.Z) * y, new Vec3(mat.C2.X * num, mat.C2.Y * num2, mat.C2.Z));
				m.PartRotation[part] = mat;
			}
		}

		public static ulong Seed(long id, string race, bool female)
		{
			ulong num = 1469598103934665603uL;
			string text = race ?? "";
			foreach (char c in text)
			{
				num ^= c;
				num *= 1099511628211L;
			}
			return Mix((ulong)id ^ (num << 1) ^ (ulong)(female ? 1540483477 : 0));
		}

		private static ulong Mix(ulong z)
		{
			z += 11400714819323198485uL;
			z = (z ^ (z >> 30)) * 13787848793156543929uL;
			z = (z ^ (z >> 27)) * 10723151780598845931uL;
			return z ^ (z >> 31);
		}

		private static ulong Bits(ulong seed, int salt)
		{
			return Mix(seed ^ (ulong)(salt * -3335678366873096957L));
		}

		private static float Unit(ulong seed, int salt)
		{
			return (float)(Bits(seed, salt) >> 40) / 16777216f;
		}

		private static bool Chance(ulong seed, int salt, float p)
		{
			return Unit(seed, salt) < p;
		}

		private static sbyte Variant(ulong seed, int salt, int count)
		{
			if (count > 0)
			{
				return (sbyte)(Bits(seed, salt) % (ulong)count);
			}
			return -1;
		}
	}
}
