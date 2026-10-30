using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class Models
	{
		private readonly struct PaletteKey : IEquatable<PaletteKey>
		{
			public readonly SpriteImage Image;

			public readonly MeshModel Model;

			public PaletteKey(SpriteImage i, MeshModel m)
			{
				Image = i;
				Model = m;
			}

			public bool Equals(PaletteKey o)
			{
				if (Image == o.Image)
				{
					return Model.SlotNames == o.Model.SlotNames;
				}
				return false;
			}

			public override bool Equals(object obj)
			{
				if (obj is PaletteKey o)
				{
					return Equals(o);
				}
				return false;
			}

			public override int GetHashCode()
			{
				return RuntimeHelpers.GetHashCode(Image) * 31 + RuntimeHelpers.GetHashCode(Model.SlotNames);
			}
		}

		private sealed class Pack
		{
			public string Key;

			public string Resource;

			public volatile Dictionary<string, MeshModel> Loaded;

			public volatile string Failed;

			public int State;

			public long Bytes;

			public double Seconds;

			public bool Towns;
		}

		private static Dictionary<string, MeshModel> _library;

		private static volatile bool _tried;

		private static readonly Dictionary<MeshModel, PersonRig> Rigs = new Dictionary<MeshModel, PersonRig>();

		public const float NearDetail = 11f;

		public const float MiddleDetail = 30f;

		public const float DistantDetail = 60f;

		private static readonly Dictionary<MeshModel, MeshModel> Distants = new Dictionary<MeshModel, MeshModel>();

		public static float LodZoom = 1f;

		private static readonly object LoadGate = new object();

		private static readonly Dictionary<string, string[]> LodNames = new Dictionary<string, string[]>();

		private static readonly Dictionary<string, string> CivBeasts = new Dictionary<string, string>
		{
			{ "cat", "cat" },
			{ "dog", "dog" },
			{ "fox", "fox" },
			{ "wolf", "fox" },
			{ "hyena", "dog" },
			{ "bear", "bear" },
			{ "rabbit", "rabbit" },
			{ "rat", "rat" },
			{ "capybara", "bear" },
			{ "monkey", "monkey" },
			{ "cow", "cow" },
			{ "buffalo", "buffalo" },
			{ "goat", "goat" },
			{ "sheep", "sheep" },
			{ "alpaca", "sheep" },
			{ "unicorn", "horse" },
			{ "rhino", "rhino" },
			{ "armadillo", "armadillo" },
			{ "chicken", "bird" },
			{ "penguin", "penguin" },
			{ "crocodile", "croc" },
			{ "snake", "snake" },
			{ "frog", "frog" },
			{ "turtle", "turtle" },
			{ "beetle", "bug" },
			{ "scorpion", "bug" },
			{ "crab", "crab" },
			{ "piranha", "fish" },
			{ "seal", "seal" },
			{ "liliar", "plant" },
			{ "garlic_man", "plant" },
			{ "lemon_man", "plant" },
			{ "crystal_golem", "crystal" },
			{ "acid_gentleman", "blob" },
			{ "candy_man", "blob" }
		};

		private static readonly HashSet<string> OwnBodies = new HashSet<string>
		{
			"elf", "orc", "dwarf", "bandit", "necromancer", "evil_mage", "white_mage", "druid", "plague_doctor", "skeleton",
			"demon", "alien", "cold_one"
		};

		private static readonly Dictionary<string, string> WomenOf = new Dictionary<string, string>();

		private static readonly Dictionary<string, string> ArmStyles = new Dictionary<string, string>();

		private static readonly Dictionary<string, string> CreatureFamilies = new Dictionary<string, string>
		{
			{ "fox", "animal_fox" },
			{ "dog", "animal_dog" },
			{ "wolf", "animal_wolf" },
			{ "hyena", "animal_hyena" },
			{ "cat", "animal_cat" },
			{ "raccoon", "animal_raccoon" },
			{ "bear", "animal_bear" },
			{ "cow", "animal_cow" },
			{ "buffalo", "animal_buffalo" },
			{ "rhino", "animal_rhino" },
			{ "sheep", "animal_sheep" },
			{ "goat", "animal_goat" },
			{ "alpaca", "animal_alpaca" },
			{ "unicorn", "animal_unicorn" },
			{ "capybara", "animal_capybara" },
			{ "rabbit", "animal_rabbit" },
			{ "rat", "animal_rat" },
			{ "armadillo", "animal_armadillo" },
			{ "monkey", "animal_monkey" },
			{ "garl", "animal_garl" },
			{ "chicken", "animal_chicken" },
			{ "penguin", "animal_penguin" },
			{ "ostrich", "animal_ostrich" },
			{ "crocodile", "animal_crocodile" },
			{ "turtle", "animal_turtle" },
			{ "snake", "animal_snake" },
			{ "piranha", "animal_fish" },
			{ "worm", "animal_worm" },
			{ "lemon_snail", "animal_snail" },
			{ "frog", "animal_frog" },
			{ "seal", "animal_seal" },
			{ "crab", "animal_crab" },
			{ "scorpion", "animal_scorpion" },
			{ "ant_black", "animal_ant" },
			{ "ant_green", "animal_ant" },
			{ "ant_blue", "animal_ant" },
			{ "ant_red", "animal_ant" },
			{ "sand_spider", "animal_spider" },
			{ "beetle", "animal_beetle" },
			{ "bee", "animal_bee" },
			{ "fly", "animal_fly" },
			{ "butterfly", "animal_butterfly" },
			{ "grasshopper", "animal_grasshopper" },
			{ "acid_blob", "monster_slime" },
			{ "bioblob", "monster_bioblob" },
			{ "tumor_monster_animal", "monster_slime" },
			{ "fire_elemental_blob", "monster_fire_blob" },
			{ "ghost", "monster_ghost" },
			{ "jumpy_skull", "monster_skull" },
			{ "fire_skull", "monster_fire_skull" },
			{ "snowman", "monster_snowman" },
			{ "fire_elemental", "monster_fire_spirit" },
			{ "fire_elemental_horse", "monster_fire_horse" },
			{ "fire_elemental_slug", "monster_fire_slug" },
			{ "fire_elemental_snake", "monster_fire_snake" },
			{ "crystal_sword", "monster_crystal_sword" },
			{ "smore", "monster_smore" },
			{ "flower_bud", "monster_flower_bud" },
			{ "angle", "monster_angle" },
			{ "fairy", "monster_fairy" },
			{ "greg", "monster_greg" },
			{ "mush_unit", "monster_mushroom" },
			{ "mush_animal", "monster_mushroom_animal" },
			{ "lil_pumpkin", "monster_pumpkin" },
			{ "assimilator", "monster_assimilator" },
			{ "printer", "monster_printer" },
			{ "tumor_monster_unit", "monster_tumor" },
			{ "living_plants", "monster_living_tree" },
			{ "living_house", "monster_living_tree" },
			{ "dragon", "monster_dragon" },
			{ "UFO", "monster_ufo" }
		};

		private static readonly Dictionary<MeshModel, CreatureRig> CreatureRigs = new Dictionary<MeshModel, CreatureRig>();

		private static readonly Dictionary<SpriteImage, float> OpaqueWidths = new Dictionary<SpriteImage, float>();

		internal static string FamilyForTest;

		private static readonly Dictionary<long, uint[]> Buildings = new Dictionary<long, uint[]>();

		private static readonly Dictionary<PaletteKey, uint[]> Palettes = new Dictionary<PaletteKey, uint[]>();

		private static volatile Dictionary<string, MeshModel> _overlay;

		private static readonly Dictionary<string, string> _packCreatures = new Dictionary<string, string>(StringComparer.Ordinal);

		private static Dictionary<string, MeshModel> _packCreaturesFor;

		private static readonly object PackCreatureGate = new object();

		private static Dictionary<string, Pack> _packOf;

		private static Dictionary<string, Pack> _packs;

		private const string PackPrefix = "FirstPerson.species.";

		private const string TownsPrefix = "FirstPerson.";

		private static readonly object PackLoading = new object();

		private static int _townPacksLoaded;

		internal static bool FamiliesRemembered = true;

		[ThreadStatic]
		private static bool _unsettled;

		private static readonly Dictionary<string, string> ZombieFamilies = new Dictionary<string, string>();

		private static bool _verifyFamilies;

		private static int _zombiesCompared;

		private static int _zombiesDiffer;

		private static string _zombiesFirst;

		public static bool Ready
		{
			get
			{
				if (!_tried)
				{
					EnsureLoaded(beside: false);
				}
				return _library != null;
			}
		}

		internal static bool Loaded
		{
			get
			{
				if (_tried)
				{
					return _library != null;
				}
				return false;
			}
		}

		internal static IEnumerable<string> SpeciesPacks
		{
			get
			{
				if (_packs == null)
				{
					return new string[0];
				}
				return _packs.Keys;
			}
		}

		internal static int TownPacksLoaded => Volatile.Read(ref _townPacksLoaded);

		internal static int ZombiesCompared => _zombiesCompared;

		public static MeshModel DetailedCreature(string family, float distance, MeshModel current)
		{
			distance /= LodZoom;
			if (family.StartsWith("boat_", StringComparison.Ordinal))
			{
				return Kept(family, distance, current);
			}
			if (IsDistant(current) && current.Name.StartsWith(family, StringComparison.Ordinal))
			{
				if (distance > 57f)
				{
					return current;
				}
				current = null;
			}
			else if (distance >= 60f + ((current != null) ? 3f : 0f))
			{
				return Distant(DetailedAt(family, 60f));
			}
			return Kept(family, distance, current);
		}

		public static MeshModel Distant(MeshModel model)
		{
			if (model == null)
			{
				return null;
			}
			if (!Distants.TryGetValue(model, out var value))
			{
				value = MeshBoxes.Boxes(model, model.Name + "_x");
				Distants[model] = value;
			}
			return value;
		}

		public static bool IsDistant(MeshModel model)
		{
			if (model != null && model.Name != null)
			{
				return model.Name.EndsWith("_x", StringComparison.Ordinal);
			}
			return false;
		}

		public static void Preload()
		{
			if (_tried)
			{
				return;
			}
			if (Application.platform == RuntimePlatform.OSXPlayer)
			{
				EnsureLoaded(beside: false);
				return;
			}
			ThreadPool.QueueUserWorkItem(delegate
			{
				EnsureLoaded(beside: true);
			});
		}

		private static void EnsureLoaded(bool beside)
		{
			lock (LoadGate)
			{
				if (!_tried)
				{
					Load(beside);
					_tried = true;
				}
			}
		}

		private static void Load(bool beside)
		{
			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				Assembly assembly = typeof(Models).Assembly;
				Stream stream = assembly.GetManifestResourceStream("FirstPerson.models.fpm2");
				bool flag = stream != null;
				if (stream == null)
				{
					stream = assembly.GetManifestResourceStream("FirstPerson.models.bin");
				}
				if (stream == null)
				{
					stream = assembly.GetManifestResourceStream("FirstPerson.models.fpz");
					if (stream != null)
					{
						stream = new InflateBlocks(stream);
						flag = true;
					}
				}
				if (stream == null)
				{
					Log.Error("models: the model library is missing from the mod");
					return;
				}
				using (stream)
				{
					_library = MeshLibrary.Load(stream);
				}
				ViewMeshes.Library = Get;
				Log.Info($"models: {_library.Count} loaded in {stopwatch.ElapsedMilliseconds} ms" + (flag ? " (packed)" : "") + (beside ? " (beside the game)" : ""));
				IndexPacks();
			}
			catch (Exception ex)
			{
				Log.Error("models: could not load: " + ex.Message);
				_library = null;
			}
		}

		public static MeshModel Get(string name)
		{
			if (!Ready)
			{
				return null;
			}
			Dictionary<string, MeshModel> overlay = _overlay;
			if (overlay != null && overlay.TryGetValue(name, out var value))
			{
				return value;
			}
			if (!_library.TryGetValue(name, out var value2))
			{
				return FromPack(name);
			}
			return value2;
		}

		public static MeshModel Detailed(string family, float distance)
		{
			return DetailedAt(family, distance / LodZoom);
		}

		private static MeshModel DetailedAt(string family, float distance)
		{
			return Get((distance < 11f) ? family : LodName(family, (distance < 30f) ? 1 : 2)) ?? Get(family);
		}

		private static string LodName(string family, int level)
		{
			lock (LodNames)
			{
				if (!LodNames.TryGetValue(family, out var value))
				{
					if (LodNames.Count > 8192)
					{
						LodNames.Clear();
					}
					value = new string[3]
					{
						family,
						family + "_m",
						family + "_f"
					};
					LodNames[family] = value;
				}
				return value[level];
			}
		}

		public static MeshModel Detailed(string family, float distance, MeshModel current)
		{
			return Kept(family, distance / LodZoom, current);
		}

		private static MeshModel Kept(string family, float distance, MeshModel current)
		{
			if (current != null && current.Name != null && current.Name.StartsWith(family, StringComparison.Ordinal))
			{
				string name = current.Name;
				int num = ((name.EndsWith("_f", StringComparison.Ordinal) && name.Length == family.Length + 2) ? 2 : ((name.EndsWith("_m", StringComparison.Ordinal) && name.Length == family.Length + 2) ? 1 : ((name.Length != family.Length) ? (-1) : 0)));
				if (num == 0 && distance < 12.5f)
				{
					return current;
				}
				if (num == 1 && distance > 9.5f && distance < 31.5f)
				{
					return current;
				}
				if (num == 2 && distance > 28.5f)
				{
					return current;
				}
			}
			return DetailedAt(family, distance);
		}

		public static PersonRig Rig(MeshModel m)
		{
			if (!Rigs.TryGetValue(m, out var value))
			{
				value = new PersonRig(m);
				Rigs[m] = value;
			}
			return value;
		}

		public static string PersonFamily(Actor a)
		{
			string text = BodyFamily(a);
			if (!IsFemale(a))
			{
				return text;
			}
			if (!WomenOf.TryGetValue(text, out var value))
			{
				value = ((Get(text + "_fem") != null) ? (text + "_fem") : null);
				if (Ready)
				{
					WomenOf[text] = value;
				}
			}
			return value ?? text;
		}

		public static bool IsFemale(Actor a)
		{
			try
			{
				return a?.isSexFemale() ?? false;
			}
			catch
			{
				return false;
			}
		}

		public static string ArmStyle(string family)
		{
			if (family == null)
			{
				return null;
			}
			if (ArmStyles.TryGetValue(family, out var value))
			{
				return value;
			}
			value = Get(family)?.Info("arm");
			if (string.IsNullOrEmpty(value))
			{
				object obj;
				switch (family)
				{
				default:
					obj = null;
					break;
				case "person_dwarf_fem":
					obj = "bare";
					break;
				case "person_orc_fem":
					obj = "bracer";
					break;
				case "person_elf_fem":
					obj = "bell";
					break;
				}
				value = (string)obj;
			}
			if (Ready)
			{
				ArmStyles[family] = value;
			}
			return value;
		}

		private static string BodyFamily(Actor a)
		{
			string text = Mutations.PersonFamily(a);
			if (text != null)
			{
				return text;
			}
			string text2 = ((a.asset != null) ? (a.asset.id ?? "") : "");
			string text3 = PackPerson(text2);
			if (text3 != null)
			{
				return text3;
			}
			bool flag = false;
			if (text2.StartsWith("zombie_", StringComparison.Ordinal))
			{
				text2 = text2.Substring(7);
				flag = true;
			}
			else if (text2 == "zombie")
			{
				text2 = "human";
				flag = true;
			}
			if (flag)
			{
				switch (text2)
				{
				case "human":
				case "elf":
				case "orc":
				case "dwarf":
				{
					string text4 = ((text2 == "human") ? "person_zombie" : ("person_zombie_" + text2));
					if (Asked(text4) != null)
					{
						return text4;
					}
					break;
				}
				}
			}
			if (text2.StartsWith("civ_", StringComparison.Ordinal) && Asked("person_" + text2.Substring(4)) != null)
			{
				return "person_" + text2.Substring(4);
			}
			if (text2.StartsWith("civ_", StringComparison.Ordinal) && CivBeasts.TryGetValue(text2.Substring(4), out var value) && Asked("person_beast_" + value) != null)
			{
				return "person_beast_" + value;
			}
			if (OwnBodies.Contains(text2) && Asked("person_" + text2) != null)
			{
				return "person_" + text2;
			}
			if (text2.Contains("elf"))
			{
				return "person_elf";
			}
			if (text2.Contains("orc"))
			{
				return "person_orc";
			}
			if (text2.Contains("dwarf"))
			{
				return "person_dwarf";
			}
			return "person_human";
		}

		public static string CreatureFamily(Actor a)
		{
			string text = Mutations.CreatureFamily(a);
			if (text != null)
			{
				return text;
			}
			string text2 = ((a.asset != null) ? (a.asset.id ?? "") : "");
			if (CreatureFamilies.TryGetValue(text2, out var value))
			{
				return FaunaVariant(a, value);
			}
			if (!text2.StartsWith("zombie_", StringComparison.Ordinal))
			{
				return PackCreature(text2);
			}
			if (FamiliesRemembered)
			{
				return ZombieFamily(text2);
			}
			return CreatureFamilyOf(text2);
		}

		private static string CreatureFamilyOf(string id)
		{
			if (id.StartsWith("zombie_animal_", StringComparison.Ordinal) && CreatureFamilies.TryGetValue(id.Substring(14), out var value))
			{
				return value;
			}
			if (id.StartsWith("zombie_", StringComparison.Ordinal) && CreatureFamilies.TryGetValue(id.Substring(7), out value))
			{
				return value;
			}
			return null;
		}

		public static string BoatFamily(Actor a)
		{
			return BoatFamilyOf(a.asset);
		}

		public static string BoatFamilyOf(ActorAsset asset)
		{
			if (asset == null || !asset.is_boat)
			{
				return null;
			}
			string text = asset.id ?? "";
			if (text.Contains("fishing"))
			{
				return "boat_fishing";
			}
			string text2 = CivBoat(text, text.Contains("trading") || asset.boat_type == "trading");
			if (text2 != null)
			{
				return text2;
			}
			bool flag = text.EndsWith("_human", StringComparison.Ordinal) || text.EndsWith("_elf", StringComparison.Ordinal) || text.EndsWith("_dwarf", StringComparison.Ordinal);
			if (text.Contains("trading") || asset.boat_type == "trading")
			{
				if (!flag)
				{
					return "boat_raft";
				}
				return "boat_cog";
			}
			if (!flag)
			{
				return "boat_barge";
			}
			return "boat_galleon";
		}

		public static CreatureRig CreatureRigFor(MeshModel m)
		{
			if (!CreatureRigs.TryGetValue(m, out var value))
			{
				value = new CreatureRig(m);
				CreatureRigs[m] = value;
			}
			return value;
		}

		public static float OpaqueWidth(SpriteImage img)
		{
			if (OpaqueWidths.TryGetValue(img, out var value))
			{
				return value;
			}
			if (OpaqueWidths.Count > 4000)
			{
				OpaqueWidths.Clear();
			}
			int num = img.Width;
			int num2 = -1;
			for (int i = 0; i < img.Height; i++)
			{
				for (int j = 0; j < img.Width; j++)
				{
					if (img.Pixels[i * img.Width + j] >> 24 >= 128)
					{
						if (j < num)
						{
							num = j;
						}
						if (j > num2)
						{
							num2 = j;
						}
					}
				}
			}
			value = ((num2 < num) ? 1f : ((float)(num2 - num + 1) / (float)Math.Max(1, img.Width)));
			OpaqueWidths[img] = value;
			return value;
		}

		public static uint[] CreaturePalette(MeshModel model, SpriteImage img)
		{
			return CreatureColors.FromSprite(model, img);
		}

		public static uint[] BoatColors(MeshModel model, Color? kingdom)
		{
			uint[] array = new uint[model.SlotNames.Length];
			if (!kingdom.HasValue)
			{
				return array;
			}
			Color value = kingdom.Value;
			uint num = TerrainCache.Rgb((int)(value.r * 255f), (int)(value.g * 255f), (int)(value.b * 255f));
			for (int i = 0; i < array.Length; i++)
			{
				if (model.SlotNames[i] == "banner")
				{
					array[i] = num;
				}
				else if (model.SlotNames[i] == "banner_dark")
				{
					array[i] = TerrainCache.Scale(num, 0.62f);
				}
			}
			return array;
		}

		public static string TreeFamily(string id, int variant)
		{
			id = id ?? "";
			string text = Flora.TreeFamily(id);
			if (text != null && Get(text) != null)
			{
				return text;
			}
			if (id.Contains("pine") || id.Contains("garlic") || id.Contains("snow"))
			{
				return "tree_pine";
			}
			if (id.Contains("palm"))
			{
				return "tree_palm";
			}
			if (id.Contains("savanna"))
			{
				return "tree_acacia";
			}
			if (id.Contains("cact"))
			{
				return "tree_cactus";
			}
			if (id.Contains("mushroom"))
			{
				return "tree_mushroom";
			}
			if (id.Contains("swamp"))
			{
				return "tree_willow";
			}
			if (id.Contains("birch"))
			{
				return "tree_birch";
			}
			if (id.Contains("corrupted") || id.Contains("infernal") || id.Contains("wasteland") || id.Contains("desert") || id.Contains("dead") || id.Contains("singularity") || id.Contains("paradox") || id.Contains("rocklands"))
			{
				return "tree_gnarled";
			}
			if (id.Contains("crystal") || id.Contains("candy") || id.Contains("enchanted") || id.Contains("lemon") || id.Contains("jungle") || id.Contains("clover") || id.Contains("flower") || id.Contains("_small") || id == "tree_green_1" || id == "tree_green_3")
			{
				return "tree_round";
			}
			if ((variant & 1) != 0)
			{
				return "tree_broadleaf_b";
			}
			return "tree_broadleaf";
		}

		public static string SpecialFamily(string id)
		{
			switch (id)
			{
			case "geyser":
				return "nature_geyser";
			case "geyser_acid":
				if (Get("nature_geyser_acid") == null)
				{
					return "nature_geyser";
				}
				return "nature_geyser_acid";
			case "volcano":
				if (Get("nature_volcano") == null)
				{
					return null;
				}
				return "nature_volcano";
			case "monolith":
				return "nature_monolith";
			case "flame_tower":
				return "nature_flame_tower";
			case "ice_tower":
				return "nature_ice_tower";
			case "angle_tower":
				return "nature_angle_tower";
			case "golden_brain":
				return "nature_golden_brain";
			case "corrupted_brain":
				return "nature_corrupted_brain";
			case "tumor":
				return "nature_tumor";
			case "biomass":
				return "nature_biomass";
			case "super_pumpkin":
				return "nature_super_pumpkin";
			case "cybercore":
				return "nature_cybercore";
			case "waypoint_alien_mold":
				return "nature_alien_mold";
			case "waypoint_computer":
				return "nature_computer";
			case "waypoint_golden_egg":
				return "nature_golden_egg";
			case "waypoint_harp":
				return "nature_harp";
			case "poop":
				return "nature_poop";
			default:
				return null;
			}
		}

		public static string PlantFamily(BuildingAsset asset)
		{
			string text = asset.id ?? "";
			string text2 = asset.type ?? "";
			string text3 = Flora.PlantFamily(text);
			if (text3 != null && Get(text3) != null)
			{
				return text3;
			}
			if (text.Contains("mushroom"))
			{
				return "plant_mushroom";
			}
			if (text.Contains("wheat") || text2 == "type_crops")
			{
				return "plant_wheat";
			}
			if (text.Contains("swamp"))
			{
				return "plant_reeds";
			}
			if (text.Contains("crystal") || text.Contains("singularity") || text.Contains("celestial"))
			{
				return "plant_crystal";
			}
			if (text.Contains("herb") || text.Contains("garlic") || text.Contains("clover"))
			{
				return "plant_herb";
			}
			if (text2 == "type_flower" || text.Contains("flower"))
			{
				return "plant_flower";
			}
			if (((text.Length * -1640531535) & 1) != 0)
			{
				return "plant_grass_b";
			}
			return "plant_grass";
		}

		public static string LumpFamily(string id)
		{
			id = id ?? "";
			if (id.StartsWith("mineral", StringComparison.Ordinal))
			{
				string text = ((id.Length > 8) ? id.Substring(8) : "");
				string text2 = ((text == "stone") ? "rock_plain" : ((text == "bones") ? "rock_bones" : ("rock_ore_" + text)));
				if (Get(text2) != null)
				{
					return text2;
				}
				if (!id.Contains("stone") && !id.Contains("bones"))
				{
					return "rock_ore";
				}
				return "rock_plain";
			}
			if (id.Contains("fruit") || id.Contains("berry"))
			{
				return "bush_fruit";
			}
			if (id.Contains("bush"))
			{
				return "bush";
			}
			return null;
		}

		public static string BuildingFamily(BuildingAsset asset)
		{
			if (asset == null)
			{
				return null;
			}
			string text = asset.id ?? "";
			string text2 = asset.type ?? "";
			string text3 = CivBuilding(text, text2);
			if (text3 != null)
			{
				return text3;
			}
			string text4 = (text.Contains("elf") ? "elf" : (text.Contains("orc") ? "orc" : (text.Contains("dwarf") ? "dwarf" : "human")));
			switch (text2)
			{
			case "type_house":
			{
				if (text.StartsWith("tent", StringComparison.Ordinal))
				{
					return "tent";
				}
				char c = ((text.Length > 0) ? text[text.Length - 1] : '0');
				return "house_" + text4 + "_" + ((c >= '0' && c <= '5') ? (c - 48) : 0);
			}
			case "type_hall":
				return "hall_" + text4;
			case "type_windmill":
				return "windmill";
			case "type_watch_tower":
				return "watch_tower";
			case "type_temple":
				return "temple";
			case "type_library":
				return "library";
			case "type_market":
				return "market";
			case "type_barracks":
				return "barracks";
			case "type_stockpile":
				return "stockpile";
			case "type_well":
				return "well";
			case "type_training_dummies":
				return "training_dummy";
			case "type_docks":
				if (!text.Contains("fishing"))
				{
					return "docks";
				}
				return "fishing_docks";
			case "type_mine":
				return "mine";
			case "type_statue":
				return "statue";
			case "type_hive":
				return "beehive";
			default:
				return null;
			}
		}

		private static string StatueOf(Building b)
		{
			string text = null;
			try
			{
				text = b.kingdom?.getActorAsset()?.id;
			}
			catch
			{
			}
			if (text == null || (text != "elf" && text != "orc" && text != "dwarf"))
			{
				try
				{
					text = b.current_tile?.zone?.city?.getActorAsset()?.id ?? text;
				}
				catch
				{
				}
			}
			switch (text)
			{
			case "elf":
			case "orc":
			case "dwarf":
			{
				string text2 = "statue_" + text;
				if (Get(text2) != null)
				{
					return text2;
				}
				break;
			}
			}
			return "statue";
		}

		public static string BuildingFamily(Building b)
		{
			string text = ((b != null) ? BuildingFamily(b.asset) : null);
			if (text == null)
			{
				return null;
			}
			if (text == "statue")
			{
				return StatueOf(b);
			}
			if (FamilyForTest != null && (text == "tent" || text.StartsWith("house_", StringComparison.Ordinal)))
			{
				return FamilyForTest;
			}
			string text2 = TownModels.Family(b, text);
			if (text2 != null)
			{
				return text2;
			}
			WorldTile current_tile = b.current_tile;
			if (current_tile == null)
			{
				return text;
			}
			string text3 = Species.Of(b.asset.id ?? "");
			if (text == "tent")
			{
				string text4 = "tent_" + text3 + ((ShapeAt(current_tile.x, current_tile.y, text3, 2) == 0) ? "" : "_b");
				if (!Has(text4))
				{
					if (!Has("tent_" + text3))
					{
						return text;
					}
					return "tent_" + text3;
				}
				return text4;
			}
			if (!text.StartsWith("house_", StringComparison.Ordinal))
			{
				return text;
			}
			int num = ShapeAt(current_tile.x, current_tile.y, text3, 3);
			if (num == 0)
			{
				return text;
			}
			string text5 = text + ((num == 1) ? "_b" : "_c");
			if (!Has(text5))
			{
				return text;
			}
			return text5;
		}

		internal static int ShapeAt(int x, int y, string race, int count)
		{
			return HouseShapes.At(x, y, race, count);
		}

		public static bool Mirrored(Building b)
		{
			WorldTile worldTile = b?.current_tile;
			if (worldTile != null)
			{
				return HouseShapes.Mirrored(worldTile.x, worldTile.y);
			}
			return false;
		}

		public static uint[] BuildingColors(MeshModel model, string family, uint kingdom)
		{
			long key = PaletteKeyOf(model, kingdom);
			if (Buildings.TryGetValue(key, out var value))
			{
				return value;
			}
			if (Buildings.Count > 800)
			{
				Buildings.Clear();
			}
			return Buildings[key] = BuildingPalette.Whole(model, family, kingdom);
		}

		public static uint[] RuinColors(MeshModel model, uint kingdom)
		{
			long key = PaletteKeyOf(model, kingdom);
			if (Buildings.TryGetValue(key, out var value))
			{
				return value;
			}
			if (Buildings.Count > 800)
			{
				Buildings.Clear();
			}
			return Buildings[key] = BuildingPalette.Ruin(model, model.Name ?? "", kingdom);
		}

		private static long PaletteKeyOf(MeshModel model, uint kingdom)
		{
			return ((long)RuntimeHelpers.GetHashCode(model) << 32) ^ kingdom;
		}

		public static uint[] PaletteFor(MeshModel model, SpriteImage img)
		{
			PaletteKey key = new PaletteKey(img, model);
			if (Palettes.TryGetValue(key, out var value))
			{
				return value;
			}
			if (Palettes.Count > 2000)
			{
				Palettes.Clear();
			}
			uint[] array = Flora.SpriteColors(model, img);
			Palettes[key] = array;
			return array;
		}

		private static string FaunaVariant(Actor a, string family)
		{
			object obj;
			switch (family)
			{
			default:
				obj = null;
				break;
			case "animal_chicken":
				obj = "animal_chicken_chick";
				break;
			case "animal_cat":
				obj = "animal_cat_kitten";
				break;
			case "animal_wolf":
				obj = "animal_wolf_pup";
				break;
			}
			string text = (string)obj;
			if (text == null || Overlaid(family))
			{
				return family;
			}
			bool flag = true;
			try
			{
				flag = a.isAdult();
			}
			catch
			{
			}
			if (!flag)
			{
				if (!Known(text))
				{
					return family;
				}
				return text;
			}
			if (family == "animal_chicken")
			{
				bool flag2 = false;
				try
				{
					flag2 = a.data.sex == ActorSex.Male;
				}
				catch
				{
				}
				if (flag2 && Known("animal_rooster"))
				{
					return "animal_rooster";
				}
			}
			return family;
		}

		internal static List<MeshModel> SetOverlay(Dictionary<string, MeshModel> next)
		{
			Dictionary<string, MeshModel> overlay = _overlay;
			List<MeshModel> list = new List<MeshModel>();
			HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
			if (overlay != null)
			{
				hashSet.UnionWith(overlay.Keys);
			}
			if (next != null)
			{
				hashSet.UnionWith(next.Keys);
			}
			foreach (string item in hashSet)
			{
				MeshModel meshModel = Under(overlay, item);
				MeshModel meshModel2 = Under(next, item);
				if (meshModel != null && meshModel != meshModel2)
				{
					list.Add(meshModel);
					if (Distants.TryGetValue(meshModel, out var value))
					{
						list.Add(value);
					}
				}
			}
			_overlay = next;
			return list;
		}

		private static MeshModel Under(Dictionary<string, MeshModel> overlay, string name)
		{
			if (overlay != null && overlay.TryGetValue(name, out var value))
			{
				return value;
			}
			if (_library != null && _library.TryGetValue(name, out var value2))
			{
				return value2;
			}
			if (_packOf != null && _packOf.TryGetValue(name, out var value3) && value3.Loaded != null && value3.Loaded.TryGetValue(name, out value2))
			{
				return value2;
			}
			return null;
		}

		internal static bool Known(string name)
		{
			if (!Ready)
			{
				return false;
			}
			if (!_library.ContainsKey(name))
			{
				if (_packOf != null)
				{
					return _packOf.ContainsKey(name);
				}
				return false;
			}
			return true;
		}

		internal static bool Overlaid(string name)
		{
			Dictionary<string, MeshModel> overlay = _overlay;
			if (overlay != null && overlay.TryGetValue(name, out var value))
			{
				return value != null;
			}
			return false;
		}

		internal static MeshModel Own(string name)
		{
			return Under(null, name);
		}

		private static string PackCreature(string id)
		{
			Dictionary<string, MeshModel> overlay = _overlay;
			lock (PackCreatureGate)
			{
				if (_packCreaturesFor != overlay)
				{
					_packCreatures.Clear();
					_packCreaturesFor = overlay;
				}
				if (_packCreatures.TryGetValue(id, out var value))
				{
					return value;
				}
			}
			string text = null;
			if (overlay != null)
			{
				string text2 = "animal_" + id;
				string text3 = "monster_" + id;
				MeshModel value3;
				if (overlay.TryGetValue(text2, out var value2) && value2 != null)
				{
					text = text2;
				}
				else if (overlay.TryGetValue(text3, out value3) && value3 != null)
				{
					text = text3;
				}
			}
			if (text == null)
			{
				PackModelNames.NoteCreature(id);
			}
			lock (PackCreatureGate)
			{
				if (_packCreaturesFor == overlay)
				{
					_packCreatures[id] = text;
				}
			}
			return text;
		}

		private static string PackPerson(string id)
		{
			if (_overlay == null || id.Length == 0)
			{
				return null;
			}
			if (!Overlaid("person_" + id))
			{
				return null;
			}
			return "person_" + id;
		}

		internal static List<string> AllNames()
		{
			List<string> list = new List<string>();
			if (!Ready)
			{
				return list;
			}
			list.AddRange(_library.Keys);
			if (_packOf != null)
			{
				list.AddRange(_packOf.Keys);
			}
			list.Sort(StringComparer.Ordinal);
			return list;
		}

		private static void IndexPacks()
		{
			_packOf = new Dictionary<string, Pack>();
			_packs = new Dictionary<string, Pack>();
			int num = 0;
			try
			{
				Assembly assembly = typeof(Models).Assembly;
				string[] manifestResourceNames = assembly.GetManifestResourceNames();
				foreach (string text in manifestResourceNames)
				{
					if (!text.EndsWith(".bin", StringComparison.Ordinal))
					{
						continue;
					}
					bool flag = text.StartsWith("FirstPerson.towns.", StringComparison.Ordinal);
					if (!flag && !text.StartsWith("FirstPerson.species.", StringComparison.Ordinal))
					{
						continue;
					}
					string text2 = (flag ? "FirstPerson." : "FirstPerson.species.");
					Pack pack = new Pack
					{
						Key = text.Substring(text2.Length, text.Length - text2.Length - 4),
						Resource = text,
						Towns = flag
					};
					if (flag)
					{
						num++;
					}
					using (Stream stream = assembly.GetManifestResourceStream(text))
					{
						pack.Bytes = stream.Length;
						foreach (string item in ModelPack.ReadIndex(stream))
						{
							_packOf[item] = pack;
						}
					}
					_packs[pack.Key] = pack;
				}
				if (_packs.Count > 0)
				{
					Log.Info(string.Format("models: {0} people's packs and {1} towns packs ({2}), {3} models in them", _packs.Count - num, num, string.Join(", ", _packs.Keys), _packOf.Count));
				}
			}
			catch (Exception ex)
			{
				Log.Error("models: could not read the people's packs: " + ex.Message);
			}
		}

		public static bool Has(string name)
		{
			if (!Ready)
			{
				return false;
			}
			Dictionary<string, MeshModel> overlay = _overlay;
			if (overlay != null && overlay.TryGetValue(name, out var value))
			{
				return value != null;
			}
			if (_library.ContainsKey(name))
			{
				return true;
			}
			if (_packOf != null)
			{
				return _packOf.ContainsKey(name);
			}
			return false;
		}

		private static MeshModel FromPack(string name)
		{
			if (_packOf == null || !_packOf.TryGetValue(name, out var value))
			{
				return null;
			}
			Dictionary<string, MeshModel> loaded = value.Loaded;
			if (loaded != null)
			{
				if (value.State != 2)
				{
					value.State = 2;
					Log.Info($"models: the {value.Key} pack loaded, {loaded.Count} models from {value.Bytes / 1024} KB in {value.Seconds * 1000.0:F0} ms");
				}
				if (!loaded.TryGetValue(name, out var value2))
				{
					return null;
				}
				return value2;
			}
			Request(value);
			return null;
		}

		private static void Request(Pack pack)
		{
			if (pack.State == 1 && pack.Failed != null)
			{
				pack.State = 3;
				Log.Error("models: the " + pack.Key + " pack could not be loaded: " + pack.Failed);
			}
			else if (pack.State == 0)
			{
				pack.State = 1;
				ThreadPool.QueueUserWorkItem(delegate
				{
					LoadPack(pack);
				});
			}
		}

		private static void LoadPack(Pack pack)
		{
			lock (PackLoading)
			{
				try
				{
					Stopwatch stopwatch = Stopwatch.StartNew();
					Dictionary<string, MeshModel> loaded;
					using (Stream stream = typeof(Models).Assembly.GetManifestResourceStream(pack.Resource))
					{
						loaded = ModelPack.Load(stream);
					}
					pack.Seconds = stopwatch.Elapsed.TotalSeconds;
					pack.Loaded = loaded;
					if (pack.Towns)
					{
						Interlocked.Increment(ref _townPacksLoaded);
					}
				}
				catch (Exception ex)
				{
					pack.Failed = ex.Message;
				}
			}
		}

		public static void WantSpecies(string key)
		{
			if (Ready && _packs != null && key != null && _packs.TryGetValue(key, out var value) && value.State == 0)
			{
				Request(value);
			}
		}

		internal static Dictionary<string, MeshModel> LoadSpeciesNow(string key, out double ms)
		{
			ms = 0.0;
			if (!Ready || _packs == null || !_packs.TryGetValue(key, out var value))
			{
				return null;
			}
			if (value.Loaded == null)
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				lock (PackLoading)
				{
					if (value.Loaded == null)
					{
						using (Stream stream = typeof(Models).Assembly.GetManifestResourceStream(value.Resource))
						{
							value.Loaded = ModelPack.Load(stream);
						}
					}
				}
				value.Seconds = stopwatch.Elapsed.TotalSeconds;
			}
			ms = value.Seconds * 1000.0;
			if (value.State < 2)
			{
				value.State = 2;
			}
			return value.Loaded;
		}

		internal static MeshModel Original(string name, bool mayLoad = true)
		{
			if (name == null || !Ready)
			{
				return null;
			}
			if (_library.TryGetValue(name, out var value))
			{
				return value;
			}
			if (_packOf == null || !_packOf.TryGetValue(name, out var value2))
			{
				return null;
			}
			if (value2.Loaded == null && !mayLoad)
			{
				return null;
			}
			if (value2.Loaded == null)
			{
				lock (PackLoading)
				{
					if (value2.Loaded == null)
					{
						try
						{
							using (Stream stream = typeof(Models).Assembly.GetManifestResourceStream(value2.Resource))
							{
								value2.Loaded = ModelPack.Load(stream);
							}
							if (value2.Towns)
							{
								Interlocked.Increment(ref _townPacksLoaded);
							}
						}
						catch (Exception ex)
						{
							value2.Failed = ex.Message;
							return null;
						}
					}
				}
			}
			if (!value2.Loaded.TryGetValue(name, out value))
			{
				return null;
			}
			return value;
		}

		internal static bool PackReady(string name)
		{
			if (!Ready || name == null)
			{
				return false;
			}
			if (_library.ContainsKey(name))
			{
				return true;
			}
			if (_packOf == null || !_packOf.TryGetValue(name, out var value))
			{
				return false;
			}
			if (value.Loaded != null)
			{
				return true;
			}
			Request(value);
			return false;
		}

		internal static List<string> TownsPacks()
		{
			List<string> list = new List<string>();
			if (_packs != null)
			{
				foreach (Pack value in _packs.Values)
				{
					if (value.Towns)
					{
						list.Add(value.Key);
					}
				}
			}
			list.Sort(StringComparer.Ordinal);
			return list;
		}

		internal static int TownsPackState(string key, out long bytes, out int models)
		{
			bytes = 0L;
			models = 0;
			if (_packs == null || !_packs.TryGetValue(key, out var value) || !value.Towns)
			{
				return -1;
			}
			bytes = value.Bytes;
			Dictionary<string, MeshModel> loaded = value.Loaded;
			if (loaded != null)
			{
				models = loaded.Count;
				return 2;
			}
			if (value.Failed == null)
			{
				return value.State;
			}
			return 3;
		}

		private static string CivBuilding(string id, string type)
		{
			if (id.IndexOf("_civ_", StringComparison.Ordinal) < 0)
			{
				return null;
			}
			string key = Species.Of(id);
			string text = Species.Family(type, key, id.Contains("fishing"));
			if (text == null || !Has(text))
			{
				return null;
			}
			WantSpecies(key);
			return text;
		}

		private static string CivBoat(string id, bool trading)
		{
			string text = Species.OfBoat(id);
			if (text == null || Species.IsClassic(text))
			{
				return null;
			}
			string text2 = (trading ? "boat_trading_" : "boat_transport_") + text;
			if (!Has(text2))
			{
				return null;
			}
			WantSpecies(text);
			return text2;
		}

		public static string CabinFor(string boatFamily)
		{
			return ((boatFamily != null) ? Get(boatFamily) : null)?.Info("cabin_room") ?? CabinBuilder.CabinFor(boatFamily);
		}

		private static MeshModel Asked(string name)
		{
			if (!Ready)
			{
				_unsettled = true;
				return null;
			}
			Pack value = null;
			bool flag = _packOf != null && _packOf.TryGetValue(name, out value) && value.Loaded != null;
			MeshModel meshModel = Get(name);
			if (meshModel == null && value != null && !flag)
			{
				_unsettled = true;
			}
			return meshModel;
		}

		internal static string PersonFamily(Actor a, out bool settled)
		{
			_unsettled = false;
			string result = PersonFamily(a);
			settled = !_unsettled && Ready;
			_unsettled = false;
			return result;
		}

		private static string ZombieFamily(string id)
		{
			if (!id.StartsWith("zombie_", StringComparison.Ordinal))
			{
				return null;
			}
			lock (ZombieFamilies)
			{
				if (ZombieFamilies.TryGetValue(id, out var value))
				{
					if (_verifyFamilies)
					{
						VerifyZombie(id, value);
					}
					return value;
				}
				value = CreatureFamilyOf(id);
				if (ZombieFamilies.Count > 1024)
				{
					ZombieFamilies.Clear();
				}
				ZombieFamilies[id] = value;
				return value;
			}
		}

		internal static void VerifyFamilies(bool on)
		{
			_verifyFamilies = on;
			if (on)
			{
				_zombiesCompared = (_zombiesDiffer = 0);
				_zombiesFirst = null;
			}
		}

		internal static string FamiliesVerdict()
		{
			if (_zombiesDiffer <= 0)
			{
				return null;
			}
			return $"{_zombiesDiffer} of {_zombiesCompared} zombie families differ; first: {_zombiesFirst}";
		}

		private static void VerifyZombie(string id, string remembered)
		{
			_zombiesCompared++;
			string text = CreatureFamilyOf(id);
			if (!(text == remembered))
			{
				_zombiesDiffer++;
				if (_zombiesFirst == null)
				{
					_zombiesFirst = id + ": " + (text ?? "none") + " the old way, " + (remembered ?? "none") + " remembered";
				}
			}
		}

		internal static string CreatureFamilyOldForTest(string id)
		{
			if (!CreatureFamilies.TryGetValue(id, out var value))
			{
				return CreatureFamilyOf(id);
			}
			return value;
		}

		internal static string CreatureFamilyNewForTest(string id)
		{
			if (!CreatureFamilies.TryGetValue(id, out var value))
			{
				return ZombieFamily(id);
			}
			return value;
		}

		internal static List<string> CreatureIdsForTest()
		{
			List<string> list = new List<string>();
			foreach (string key in CreatureFamilies.Keys)
			{
				list.Add(key);
				list.Add("zombie_" + key);
				list.Add("zombie_animal_" + key);
			}
			return list;
		}
	}
}
