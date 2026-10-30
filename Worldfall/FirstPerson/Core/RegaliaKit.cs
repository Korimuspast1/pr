using System;

namespace FirstPerson.Core
{
	public static class RegaliaKit
	{
		public enum Set : byte
		{
			Soldier,
			Captain,
			Royal,
			Court,
			Honour,
			Robe
		}

		public struct SlotDefault
		{
			public string Name;

			public byte Kind;

			public byte Flags;

			public uint Color;

			public bool Kingdom;

			private SlotDefault(string name, byte kind, byte flags, int r, int g, int b, bool kingdom)
			{
				Name = name;
				Kind = kind;
				Flags = flags;
				Color = TerrainCache.Rgb(r, g, b);
				Kingdom = kingdom;
			}

			internal static SlotDefault K(string n, byte kind, int r, int g, int b, byte flags = 0)
			{
				return new SlotDefault(n, kind, flags, r, g, b, kingdom: true);
			}

			internal static SlotDefault F(string n, byte kind, int r, int g, int b, byte flags = 0)
			{
				return new SlotDefault(n, kind, flags, r, g, b, kingdom: false);
			}
		}

		public const int Sets = 6;

		public static Func<string, MeshModel> Library;

		public const string Prefix = "rg_";

		public const string BodyMan = "human";

		public const string BodyWoman = "human_fem";

		public const string BodyModelMan = "person_human";

		public const string BodyModelWoman = "person_human_fem";

		private static readonly string[] SetNames = new string[6] { "soldier", "captain", "royal", "court", "honour", "robe" };

		private static readonly string[] LodSuffixes = new string[3] { "", "_m", "_f" };

		private static readonly string[] Names = MakeNames();

		private static readonly char[] DerivedMarks = new char[4] { '~', '+', '/', '@' };

		private static readonly string[] HalberdNames = new string[3] { "rg_halberd", "rg_halberd_m", "rg_halberd_f" };

		public const string HallPoleWood = "rg_hallpole_wood";

		public const string HallPoleMetal = "rg_hallpole_metal";

		public const string HallPoleBroken = "rg_hallpole_broken";

		public const string BannerRodEnd = "rg_bannerrod_end";

		public const string BannerBracket = "rg_bannerbracket";

		public const string Standard = "rg_standard";

		public const string Hips = "hips";

		public const string Torso = "torso";

		public const string Head = "head";

		public const string UpperArmL = "upper_arm_L";

		public const string LowerArmL = "lower_arm_L";

		private static readonly string[] SoldierBones = new string[3] { "hips", "torso", "head" };

		private static readonly string[] CaptainBones = new string[2] { "hips", "torso" };

		private static readonly string[] CourtBones = new string[5] { "hips", "torso", "head", "upper_arm_L", "lower_arm_L" };

		private static readonly string[] RobeBones = new string[4] { "hips", "torso", "upper_arm_L", "lower_arm_L" };

		public const string PartSash = "sash";

		public const string PartSashKnot = "sashknot";

		public const string PartPlume = "plume";

		public const string PartCoif = "coif";

		public const string PartCoifCape = "coif_cape";

		public const string PartHelmCap = "helmcap";

		public const string PartPole = "pole";

		public const string PartHarnessBare = "harness_bare";

		public const string PartHarnessOver = "harness_over";

		public const string PartBanner = "banner";

		public const string PartBannerTail = "banner_tail";

		public const string PartCrown = "crown";

		public const string PartCrownLow = "crown_low";

		public const string PartHelm = "helm";

		public const string PartMantleYoke = "mantle_yoke";

		public const string PartMantleYokeOver = "mantle_yoke_over";

		public const string PartMantle = "mantle";

		public const string PartMantleHem = "mantle_hem";

		public const string PartHat = "hat";

		public const string PartHood = "hood";

		public const string PartHoodCape = "hood_cape";

		public const string PartChain = "chain";

		public const string PartChainOver = "chain_over";

		public const string PartSeal = "seal";

		public const string PartSealOver = "seal_over";

		public const string PartKeys = "keys";

		public const string PartKeysOver = "keys_over";

		public const string PartBaton = "baton";

		public const string PartLedger = "ledger";

		public const string PartCirclet = "circlet";

		public const string PartCircletLow = "circlet_low";

		public const string PartCircletCoif = "circlet_coif";

		public const string PartLaurel = "laurel";

		public const string PartLaurelLow = "laurel_low";

		public const string PartLaurelHigh = "laurel_high";

		public const string PartLaurelCoif = "laurel_coif";

		public const string PartRobeYoke = "robe_yoke";

		public const string PartRobeYokeOver = "robe_yoke_over";

		public const string PartRobe = "robe";

		public const string PartRobeHem = "robe_hem";

		public const string PartRobeLapels = "robe_lapels";

		public const string PartStaff = "staff";

		public const string PartHalberd = "halberd";

		private static readonly string[] SoldierParts = new string[6] { "sash", "sashknot", "plume", "coif", "coif_cape", "helmcap" };

		private static readonly string[] CaptainParts = new string[5] { "pole", "harness_bare", "harness_over", "banner", "banner_tail" };

		private static readonly string[] RoyalParts = new string[7] { "crown", "crown_low", "helm", "mantle_yoke", "mantle_yoke_over", "mantle", "mantle_hem" };

		private static readonly string[] CourtParts = new string[11]
		{
			"hat", "hood", "hood_cape", "chain", "chain_over", "seal", "seal_over", "keys", "keys_over", "baton",
			"ledger"
		};

		private static readonly string[] HonourParts = new string[7] { "circlet", "circlet_low", "circlet_coif", "laurel", "laurel_low", "laurel_high", "laurel_coif" };

		private static readonly string[] RobeParts = new string[6] { "robe_yoke", "robe_yoke_over", "robe", "robe_hem", "robe_lapels", "staff" };

		public const string InfoKind = "kind";

		public const string KindRegalia = "regalia";

		public const string InfoSet = "set";

		public const string InfoStyle = "style";

		public const string InfoBody = "body";

		public const string InfoLod = "lod";

		public const string InfoHidePrefix = "hide.";

		public const string HideHair = "hair";

		public const string HideBeard = "beard";

		public const string HideHats = "hats";

		public const string BodyHairPrefix = "hair_";

		public const string BodyBeardPrefix = "beard_";

		public const string BodyHatPrefix = "hat_";

		public const string BodyCutPrefix = "cut_";

		public const string BodyBeltPrefix = "belt_";

		public const string InfoTorso = "torso";

		public const string InfoTwoSided = "twosided";

		public const string SlotBanner = "banner";

		public const string SlotBannerDark = "banner_dark";

		public const string SlotBanner2 = "banner_2";

		public const string SlotEmblem = "emblem";

		public const string SlotGem = "gem";

		public const string SlotSash = "sash";

		public const string SlotSashDark = "sash_dark";

		public const string SlotStripe = "stripe";

		public const string SlotEdge = "edge";

		public const string SlotHelm = "helm";

		public const string SlotGold = "gold";

		public const string SlotGoldHi = "gold_hi";

		public const string SlotGoldDark = "gold_dark";

		public const string SlotIron = "iron";

		public const string SlotIronHi = "iron_hi";

		public const string SlotRivet = "rivet";

		public const string SlotSilver = "silver";

		public const string SlotSilverHi = "silver_hi";

		public const string SlotMail = "mail";

		public const string SlotMailHi = "mail_hi";

		public const string SlotStar = "star";

		public const string SlotLaurel = "laurel";

		public const string SlotVoid = "void";

		public const string SlotPad = "pad";

		public const string SlotFur = "fur";

		public const string SlotFurSpot = "fur_spot";

		public const string SlotLeather = "leather";

		public const string SlotWood = "wood";

		public const string SlotWoodDark = "wood_dark";

		public const string SlotDark = "dark";

		public const string SlotDarkIn = "dark_in";

		public const string SlotNight = "night";

		public const string SlotPaper = "paper";

		private const byte Shiny = 16;

		public static readonly SlotDefault[] AllSlots = new SlotDefault[33]
		{
			SlotDefault.K("banner", 7, 176, 40, 40, 0),
			SlotDefault.K("banner_dark", 7, 110, 26, 26, 0),
			SlotDefault.K("banner_2", 7, 230, 200, 90, 0),
			SlotDefault.K("emblem", 7, 240, 236, 224, 0),
			SlotDefault.K("gem", 0, 196, 52, 44, 0),
			SlotDefault.K("sash", 7, 176, 40, 40, 0),
			SlotDefault.K("sash_dark", 7, 110, 26, 26, 0),
			SlotDefault.K("stripe", 7, 176, 40, 40, 0),
			SlotDefault.K("edge", 7, 110, 26, 26, 0),
			SlotDefault.K("helm", 10, 86, 87, 90, 16),
			SlotDefault.F("gold", 10, 232, 190, 80, 16),
			SlotDefault.F("gold_hi", 10, 250, 214, 120, 16),
			SlotDefault.F("gold_dark", 10, 170, 128, 44, 16),
			SlotDefault.F("iron", 10, 60, 62, 68, 16),
			SlotDefault.F("iron_hi", 10, 104, 108, 116, 16),
			SlotDefault.F("rivet", 0, 150, 152, 158, 16),
			SlotDefault.F("silver", 10, 196, 200, 208, 16),
			SlotDefault.F("silver_hi", 10, 236, 240, 246, 16),
			SlotDefault.F("mail", 10, 150, 154, 162, 16),
			SlotDefault.F("mail_hi", 10, 196, 200, 206, 16),
			SlotDefault.F("star", 10, 236, 206, 110, 16),
			SlotDefault.F("laurel", 10, 214, 170, 60, 16),
			SlotDefault.F("void", 0, 14, 14, 18, 0),
			SlotDefault.F("pad", 7, 34, 44, 96, 0),
			SlotDefault.F("fur", 9, 240, 238, 230, 0),
			SlotDefault.F("fur_spot", 0, 22, 20, 22, 0),
			SlotDefault.F("leather", 7, 96, 62, 38, 0),
			SlotDefault.F("wood", 3, 124, 86, 52, 0),
			SlotDefault.F("wood_dark", 3, 82, 56, 34, 0),
			SlotDefault.F("dark", 7, 34, 34, 40, 0),
			SlotDefault.F("dark_in", 0, 12, 12, 14, 0),
			SlotDefault.F("night", 7, 30, 34, 82, 0),
			SlotDefault.F("paper", 0, 232, 224, 200, 0)
		};

		private static readonly string[] SoldierKingdom = new string[7] { "sash", "sash_dark", "stripe", "edge", "banner", "banner_dark", "banner_2" };

		private static readonly string[] CaptainKingdom = new string[3] { "banner", "banner_2", "emblem" };

		private static readonly string[] RoyalKingdom = new string[3] { "banner", "banner_dark", "gem" };

		private static readonly string[] CourtKingdom = new string[2] { "banner", "banner_2" };

		private static readonly string[] HonourKingdom = new string[0];

		private static readonly string[] RobeKingdom = new string[1] { "banner" };

		public const float DarkShade = 0.62f;

		public static readonly uint Gold = TerrainCache.Rgb(232, 190, 80);

		public static readonly uint GoldDark = TerrainCache.Rgb(170, 128, 44);

		public static readonly uint SashGold = TerrainCache.Rgb(214, 168, 58);

		public static readonly uint StripeLight = TerrainCache.Rgb(236, 232, 220);

		public static string SetName(Set s)
		{
			return SetNames[(uint)s];
		}

		public static bool Styled(Set s)
		{
			if (s != Set.Captain)
			{
				return s == Set.Royal;
			}
			return true;
		}

		public static string StyleLetter(KingdomStyle s)
		{
			switch (s)
			{
			default:
				return null;
			case KingdomStyle.GildedSpire:
				return "c";
			case KingdomStyle.GoldWings:
				return "b";
			case KingdomStyle.IronSpikes:
				return "a";
			}
		}

		public static string LodSuffix(int lod)
		{
			return LodSuffixes[(lod >= 0) ? ((lod > 2) ? 2 : lod) : 0];
		}

		private static string[] MakeNames()
		{
			string[] array = new string[144];
			for (int i = 0; i < 6; i++)
			{
				for (int j = 0; j < 4; j++)
				{
					for (int k = 0; k < 2; k++)
					{
						for (int l = 0; l < 3; l++)
						{
							string text = StyleLetter((KingdomStyle)(j - 1));
							bool flag = Styled((Set)i);
							if (!flag || text != null)
							{
								array[((i * 4 + j) * 2 + k) * 3 + l] = "rg_" + SetNames[i] + (flag ? ("_" + text) : "") + "_" + ((k == 1) ? "human_fem" : "human") + LodSuffixes[l];
							}
						}
					}
				}
			}
			return array;
		}

		public static string ModelName(Set s, KingdomStyle style, bool female, int lod)
		{
			int num = (int)((style >= KingdomStyle.None && style <= KingdomStyle.GildedSpire) ? (style + 1) : KingdomStyle.IronSpikes);
			return Names[(((int)s * 4 + num) * 2 + (female ? 1 : 0)) * 3 + ((lod >= 0) ? ((lod > 2) ? 2 : lod) : 0)];
		}

		public static int LodOf(MeshModel body)
		{
			return LodOf(body?.Name);
		}

		public static int LodOf(string bodyName)
		{
			if (string.IsNullOrEmpty(bodyName))
			{
				return 0;
			}
			int num = bodyName.IndexOfAny(DerivedMarks);
			if (num < 0)
			{
				num = bodyName.Length;
			}
			if (num >= 2 && bodyName[num - 2] == '_' && bodyName[num - 1] == 'x')
			{
				num -= 2;
			}
			if (num < 2 || bodyName[num - 2] != '_')
			{
				return 0;
			}
			char c = bodyName[num - 1];
			if (c != 'f')
			{
				return (c == 'm') ? 1 : 0;
			}
			return 2;
		}

		public static string BodyOf(MeshModel body)
		{
			string text = BaseOf(body?.Name);
			if (text.EndsWith("_f", StringComparison.Ordinal) || text.EndsWith("_m", StringComparison.Ordinal))
			{
				text = text.Substring(0, text.Length - 2);
			}
			if (!(text == "person_human"))
			{
				if (!(text == "person_human_fem"))
				{
					return null;
				}
				return "human_fem";
			}
			return "human";
		}

		private static string BaseOf(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return "";
			}
			int num = name.IndexOfAny(DerivedMarks);
			if (num >= 0)
			{
				name = name.Substring(0, num);
			}
			if (name.EndsWith("_x", StringComparison.Ordinal))
			{
				name = name.Substring(0, name.Length - 2);
			}
			return name;
		}

		public static string HalberdName(int lod)
		{
			return HalberdNames[(lod >= 0) ? ((lod > 2) ? 2 : lod) : 0];
		}

		public static string FinialName(KingdomStyle s)
		{
			switch (s)
			{
			default:
				return "rg_finial_n";
			case KingdomStyle.GildedSpire:
				return "rg_finial_c";
			case KingdomStyle.GoldWings:
				return "rg_finial_b";
			case KingdomStyle.IronSpikes:
				return "rg_finial_a";
			}
		}

		public static string[] SkeletonOf(Set s)
		{
			switch (s)
			{
			default:
				return SoldierBones;
			case Set.Robe:
				return RobeBones;
			case Set.Court:
				return CourtBones;
			case Set.Captain:
				return CaptainBones;
			}
		}

		public static string BoneParent(string bone)
		{
			switch (bone)
			{
			default:
				return null;
			case "lower_arm_R":
				return "upper_arm_R";
			case "lower_arm_L":
				return "upper_arm_L";
			case "head":
			case "upper_arm_L":
			case "upper_arm_R":
				return "torso";
			case "torso":
			case "upper_leg_L":
			case "upper_leg_R":
				return "hips";
			}
		}

		public static Vec3 RestPivot(string bone, bool female)
		{
			switch (bone)
			{
			case "hips":
				return new Vec3(0f, 0f, female ? 0.9f : 0.92f);
			case "torso":
				return new Vec3(0f, 0f, female ? 0.98f : 1f);
			case "head":
				return new Vec3(0f, 0f, female ? 1.42f : 1.46f);
			case "upper_arm_L":
				if (!female)
				{
					return new Vec3(-0.2f, 0f, 1.38f);
				}
				return new Vec3(-0.1522f, 0f, 1.34f);
			case "lower_arm_L":
				if (!female)
				{
					return new Vec3(-0.222f, 0.012f, 1.11f);
				}
				return new Vec3(-0.1689f, 0.012f, 1.08f);
			default:
				return default(Vec3);
			}
		}

		public static string[] PartsOf(Set s)
		{
			switch (s)
			{
			case Set.Soldier:
				return SoldierParts;
			case Set.Captain:
				return CaptainParts;
			case Set.Royal:
				return RoyalParts;
			case Set.Court:
				return CourtParts;
			case Set.Honour:
				return HonourParts;
			default:
				return RobeParts;
			}
		}

		public static bool HasPart(Set s, string part, int lod)
		{
			if (!(part != "helmcap"))
			{
				return lod == 2;
			}
			return true;
		}

		public static string ParentOf(string part)
		{
			switch (part)
			{
			case "sashknot":
			case "keys":
			case "keys_over":
				return "hips";
			case "hood":
			case "coif":
			case "helm":
			case "crown_low":
			case "plume":
			case "crown":
			case "helmcap":
			case "circlet":
			case "laurel_high":
			case "circlet_low":
			case "laurel_coif":
			case "laurel":
			case "laurel_low":
			case "hat":
			case "circlet_coif":
				return "head";
			case "banner":
				return "pole";
			case "banner_tail":
				return "banner";
			case "mantle_hem":
				return "mantle";
			case "robe_hem":
				return "robe";
			case "staff":
			case "baton":
			case "ledger":
				return "lower_arm_L";
			case "halberd":
				return null;
			default:
				return "torso";
			}
		}

		public static bool IsHinge(string part)
		{
			switch (part)
			{
			default:
				return part == "plume";
			case "mantle":
			case "mantle_hem":
			case "robe":
			case "robe_hem":
			case "banner":
			case "banner_tail":
				return true;
			}
		}

		public static bool IsKingdomSlot(string slot)
		{
			SlotDefault[] allSlots = AllSlots;
			for (int i = 0; i < allSlots.Length; i++)
			{
				SlotDefault slotDefault = allSlots[i];
				if (slotDefault.Name == slot)
				{
					return slotDefault.Kingdom;
				}
			}
			return false;
		}

		public static int MaxSlots(Set s)
		{
			switch (s)
			{
			default:
				return 64;
			case Set.Captain:
				return 12;
			case Set.Royal:
				return 14;
			case Set.Soldier:
				return 16;
			}
		}

		public static string[] KingdomSlotsOf(Set s)
		{
			switch (s)
			{
			case Set.Soldier:
				return SoldierKingdom;
			case Set.Captain:
				return CaptainKingdom;
			case Set.Royal:
				return RoyalKingdom;
			case Set.Court:
				return CourtKingdom;
			case Set.Honour:
				return HonourKingdom;
			default:
				return RobeKingdom;
			}
		}
	}
}
