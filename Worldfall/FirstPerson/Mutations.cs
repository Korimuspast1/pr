using System;
using System.Collections.Generic;
using FirstPerson.Core;

namespace FirstPerson
{
	internal static class Mutations
	{
		internal static bool On = true;

		private static readonly Dictionary<string, int> SkinIndex = MakeSkinIndex();

		private static readonly string[] PersonNames = Names("person_mut_");

		private static readonly string[] CreatureNames = Names("mut_");

		private static readonly Dictionary<string, int> EggIndex = MakeEggIndex();

		private static volatile bool[] _personHas;

		private static volatile bool[] _creatureHas;

		private static volatile bool[] _eggHas;

		internal static string SkinOf(Actor a)
		{
			if (!On || a == null)
			{
				return null;
			}
			try
			{
				Subspecies subspecies = a.subspecies;
				if (subspecies == null || !subspecies.has_mutation_reskin)
				{
					return null;
				}
				SubspeciesTrait mutation_skin_asset = subspecies.mutation_skin_asset;
				int value;
				return (mutation_skin_asset != null && mutation_skin_asset.id != null && SkinIndex.TryGetValue(mutation_skin_asset.id, out value)) ? MutationLook.Skins[value] : null;
			}
			catch
			{
				return null;
			}
		}

		private static Dictionary<string, int> MakeSkinIndex()
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			for (int i = 0; i < MutationLook.Skins.Length; i++)
			{
				dictionary["mutation_skin_" + MutationLook.Skins[i]] = i;
			}
			return dictionary;
		}

		private static Dictionary<string, int> MakeEggIndex()
		{
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			for (int i = 0; i < MutationLook.Eggs.Length; i++)
			{
				dictionary["egg_" + MutationLook.Eggs[i]] = i;
			}
			return dictionary;
		}

		private static string[] Names(string prefix)
		{
			string[] array = new string[MutationLook.Skins.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = prefix + MutationLook.Skins[i];
			}
			return array;
		}

		private static void Look()
		{
			if (_eggHas == null && Models.Ready)
			{
				bool[] array = new bool[PersonNames.Length];
				bool[] array2 = new bool[CreatureNames.Length];
				bool[] array3 = new bool[MutationLook.Eggs.Length];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = Models.Get(PersonNames[i]) != null;
					array2[i] = Models.Get(CreatureNames[i]) != null;
				}
				for (int j = 0; j < array3.Length; j++)
				{
					array3[j] = Models.Get("egg_" + MutationLook.Eggs[j]) != null;
				}
				_personHas = array;
				_creatureHas = array2;
				_eggHas = array3;
			}
		}

		private static int Index(string skin)
		{
			for (int i = 0; i < MutationLook.Skins.Length; i++)
			{
				if ((object)MutationLook.Skins[i] == skin)
				{
					return i;
				}
			}
			return -1;
		}

		internal static bool Glows(Actor a)
		{
			string text = SkinOf(a);
			if (text != null)
			{
				if ((object)text != MutationLook.Skins[5])
				{
					return (object)text == MutationLook.Skins[3];
				}
				return true;
			}
			return false;
		}

		internal static bool EggNow(Actor a)
		{
			if (!On || a == null)
			{
				return false;
			}
			try
			{
				return a.isEgg();
			}
			catch
			{
				return false;
			}
		}

		internal static string EggFamily(Actor a)
		{
			if (!EggNow(a))
			{
				return null;
			}
			Look();
			bool[] eggHas = _eggHas;
			if (eggHas == null)
			{
				return null;
			}
			string text = null;
			try
			{
				text = ((a.subspecies != null) ? a.subspecies.egg_id : null);
			}
			catch
			{
			}
			if (text != null && EggIndex.TryGetValue(text, out var value) && eggHas[value])
			{
				return text;
			}
			if (!eggHas[0])
			{
				return null;
			}
			return "egg_shell_plain";
		}

		internal static string PersonFamily(Actor a)
		{
			string text = SkinOf(a);
			if (text == null)
			{
				return null;
			}
			Look();
			int num = Index(text);
			bool[] personHas = _personHas;
			if (num < 0 || personHas == null || !personHas[num])
			{
				return null;
			}
			return PersonNames[num];
		}

		internal static string CreatureFamily(Actor a)
		{
			if (!On || a == null)
			{
				return null;
			}
			string text = EggFamily(a);
			if (text != null)
			{
				return text;
			}
			string text2 = SkinOf(a);
			if (text2 == null || People.IsHumanoid(a))
			{
				return null;
			}
			Look();
			int num = Index(text2);
			bool[] creatureHas = _creatureHas;
			if (num < 0 || creatureHas == null || !creatureHas[num])
			{
				return null;
			}
			return CreatureNames[num];
		}

		internal static float Visible(Actor a, SpriteImage img)
		{
			string text = SkinOf(a);
			if (text != null && !EggNow(a))
			{
				bool child = false;
				try
				{
					child = !a.isAdult();
				}
				catch
				{
				}
				float num = MutationLook.VisibleShare(text, child);
				if (num > 0f)
				{
					return num;
				}
			}
			return img.VisibleFraction;
		}

		internal static bool CreatureShare(Actor a, string family, out float share)
		{
			share = 0f;
			if (family == null || !family.StartsWith("mut_", StringComparison.Ordinal))
			{
				return false;
			}
			string text = SkinOf(a);
			if (text == null)
			{
				return false;
			}
			bool child = false;
			try
			{
				child = !a.isAdult();
			}
			catch
			{
			}
			share = MutationLook.VisibleShare(text, child);
			return share > 0f;
		}

		internal static MutationLook.Who WhoOf(Actor a, string skin)
		{
			MutationLook.Who result = new MutationLook.Who
			{
				Skin = skin
			};
			try
			{
				result.Female = Models.IsFemale(a);
				result.Child = !a.isAdult();
				result.Zombie = a.asset != null && (a.asset.unit_zombie || (a.asset.id != null && a.asset.id.StartsWith("zombie", StringComparison.Ordinal)));
				result.Head = ((a.data != null) ? a.data.head : 0);
				result.SkinColor = People.GameSkin(a);
				int role;
				switch ((a.data != null) ? a.data.profession : UnitProfession.Nothing)
				{
				default:
					role = 0;
					break;
				case UnitProfession.Warrior:
					role = 3;
					break;
				case UnitProfession.Leader:
					role = 2;
					break;
				case UnitProfession.King:
					role = 1;
					break;
				}
				result.Role = (MutationLook.Role)role;
				if (a.kingdom != null)
				{
					ColorAsset color = a.kingdom.getColor();
					if (color != null)
					{
						result.Kingdom = TerrainCache.Rgb(color.k_color_1.r, color.k_color_1.g, color.k_color_1.b);
						result.KingdomDark = TerrainCache.Rgb(color.k_color_3.r, color.k_color_3.g, color.k_color_3.b);
					}
				}
			}
			catch
			{
			}
			return result;
		}

		internal static void Dress(Actor a, MeshInstance inst)
		{
			if (inst != null && inst.Model != null && inst.Model.Info("mutation") != null)
			{
				MutationLook.Dress(inst, WhoOf(a, inst.Model.Info("mutation")));
			}
		}

		internal static void Animate(Actor a, MeshInstance inst, float phase, float stride)
		{
			if (inst != null && inst.Model != null && inst.Model.Info("mutation") != null)
			{
				MutationLook.Animate(inst, WhoOf(a, inst.Model.Info("mutation")), PoseClock.Now, phase, stride, Seed(a));
			}
		}

		internal static void Creature(Actor a, MeshInstance inst, float phase, float stride)
		{
			if (inst != null && inst.Model != null && MutationLook.Owns(inst.Model))
			{
				string text = inst.Model.Info("mutation");
				MutationLook.Who who = ((text != null) ? WhoOf(a, text) : new MutationLook.Who
				{
					SkinColor = EggSkin(a)
				});
				if (text != null)
				{
					who.Role = MutationLook.Role.Citizen;
				}
				MutationLook.Dress(inst, in who);
				MutationLook.Animate(inst, in who, PoseClock.Now, phase, stride, Seed(a));
				if (text == null && inst.Model.Info("look") == "eyeball")
				{
					MutationLook.LookAbout(inst, inst.Model.PartIndex("head"), PoseClock.Now, Seed(a));
				}
			}
		}

		private static uint EggSkin(Actor a)
		{
			uint num = People.GameSkin(a);
			if (num == 0)
			{
				return TerrainCache.Rgb(226, 178, 134);
			}
			return num;
		}

		private static float Seed(Actor a)
		{
			try
			{
				return (float)(((a.data != null) ? a.data.id : a.GetHashCode()) & 0x3FF) / 1023f;
			}
			catch
			{
				return 0.3f;
			}
		}
	}
}
