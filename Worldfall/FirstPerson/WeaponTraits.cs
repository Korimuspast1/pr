using System;
using System.Collections.Generic;
using System.Linq;
using FirstPerson.Towns;

namespace FirstPerson
{
	internal static class WeaponTraits
	{
		internal static readonly string[] All = new string[7] { "flame", "ice", "poison", "stun", "slowness", "divine_rune", "eternal" };

		internal static readonly string[] Common = new string[5] { "flame", "ice", "poison", "stun", "slowness" };

		private const string BoonKey = "fp_boon";

		private const int BoonDays = 3;

		private const string LastBoonKey = "fp_boonday";

		internal static string Name(string mod)
		{
			try
			{
				string text = AssetManager.items_modifiers.get(mod)?.getTranslatedName();
				if (!string.IsNullOrEmpty(text) && !text.StartsWith("$"))
				{
					return text;
				}
			}
			catch
			{
			}
			if (!(mod == "divine_rune"))
			{
				return char.ToUpperInvariant(mod[0]) + mod.Substring(1);
			}
			return "Divine rune";
		}

		internal static string Does(string mod)
		{
			switch (mod)
			{
			case "flame":
				return "sets them alight";
			case "ice":
				return "freezes them where they stand";
			case "poison":
				return "poisons the blood";
			case "stun":
				return "knocks them senseless";
			case "slowness":
				return "makes them sluggish";
			case "divine_rune":
				return "a rune of the gods: stronger in every way";
			case "eternal":
				return "never wears out";
			default:
				return mod;
			}
		}

		internal static Item Weapon(Actor a)
		{
			try
			{
				return a?.getWeapon();
			}
			catch
			{
				return null;
			}
		}

		internal static string CantPut(Actor a, string mod)
		{
			Item item = Weapon(a);
			if (item == null)
			{
				return "You're not holding a weapon.";
			}
			try
			{
				if (item.hasMod(mod))
				{
					return "Your weapon already has that.";
				}
				ItemModAsset itemModAsset = AssetManager.items_modifiers.get(mod);
				if (itemModAsset == null)
				{
					return "No such thing.";
				}
				if (item.data.modifiers != null)
				{
					foreach (ref string modifier in item.data.modifiers)
					{
						string current = modifier;
						if (AssetManager.items_modifiers.get(current)?.mod_type == itemModAsset.mod_type)
						{
							return "Your weapon already has that.";
						}
					}
				}
			}
			catch
			{
			}
			return null;
		}

		internal static bool Put(Actor a, string mod)
		{
			if (CantPut(a, mod) != null)
			{
				return false;
			}
			Item item = Weapon(a);
			try
			{
				if (!item.addMod(mod))
				{
					return false;
				}
				int durability = item.data.durability;
				item.calculateValues();
				item.data.durability = durability;
				a.setStatsDirty();
				Log.Info("weapon traits: " + mod + " on " + WorldBoxMod.Describe(a) + "'s " + item.getAsset()?.id);
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("weapon traits: " + ex.Message);
				return false;
			}
		}

		internal static List<string> On(Actor a)
		{
			List<string> list = new List<string>();
			Item item = Weapon(a);
			if (item == null)
			{
				return list;
			}
			string[] all = All;
			foreach (string text in all)
			{
				try
				{
					if (item.hasMod(text))
					{
						list.Add(text);
					}
				}
				catch
				{
				}
			}
			return list;
		}

		internal static void Choose(Conversation c, IEnumerable<string> mods, Action<string> done)
		{
			Actor you = c.You;
			List<Choice> list = new List<Choice>();
			foreach (string mod in mods)
			{
				string m = mod;
				string text = CantPut(you, m);
				if (!(text == "Your weapon already has that."))
				{
					Choice choice = new Choice(Name(m) + ": " + Does(m), delegate
					{
						bool flag = Put(you, m);
						c.Toast?.Invoke(flag ? ("Your " + WeaponName(you) + " now " + Doing(m) + ".") : "It didn't take.");
						done?.Invoke(flag ? m : null);
					}, ChoiceTone.Quest);
					if (text != null)
					{
						choice.Off(text);
					}
					list.Add(choice);
				}
			}
			if (list.Count == 0 || Weapon(you) == null)
			{
				c.Say((Weapon(you) == null) ? "*looks at your empty hands* Come back with a weapon in your hand." : "*turns your weapon over* There's nothing I can add to this.");
				done?.Invoke(null);
			}
			else
			{
				list.Add(new Choice("Not now.", delegate
				{
					done?.Invoke(null);
				}, ChoiceTone.Back));
				c.Offer(list);
			}
		}

		private static string Doing(string mod)
		{
			switch (mod)
			{
			case "flame":
				return "burns what it strikes";
			case "ice":
				return "freezes what it strikes";
			case "poison":
				return "poisons what it strikes";
			case "stun":
				return "stuns what it strikes";
			case "slowness":
				return "slows what it strikes";
			case "divine_rune":
				return "bears a divine rune";
			case "eternal":
				return "will never wear out";
			default:
				return "is changed";
			}
		}

		internal static string WeaponName(Actor a)
		{
			Item item = Weapon(a);
			try
			{
				string text = item?.getAsset()?.getTranslatedName();
				if (!string.IsNullOrEmpty(text) && !text.StartsWith("$"))
				{
					return text.ToLowerInvariant();
				}
				return item?.getAsset()?.id.Replace('_', ' ') ?? "weapon";
			}
			catch
			{
				return "weapon";
			}
		}

		public static void BoonChoices(Conversation c, List<Choice> list)
		{
			Actor with = c.With;
			Actor you = c.You;
			if (!IsLordOf(with, you))
			{
				return;
			}
			bool flag = false;
			try
			{
				flag = with.isKing();
			}
			catch
			{
			}
			if (flag && !Abilities.KnowsEarth(you))
			{
				Choice choice = new Choice("Grant me the secret of moving the earth.", delegate
				{
					Abilities.TeachEarth(you);
					c.Say("*nods slowly* The court's earth-singers will teach you. Use it for the realm.");
					Sounds.PlayAt("event:/SFX/NATURE/EarthQuake", you.current_position);
					c.Offer(Dialogue.Topics(c));
				}, ChoiceTone.Quest);
				bool flag2 = false;
				try
				{
					flag2 = Served(with, you) && Chronicle.Opinion(with, you) >= 0;
				}
				catch
				{
				}
				if (!flag2)
				{
					choice.Off("a great deed for the realm first");
				}
				list.Add(choice);
			}
			string text = BoonRefusal(with, you);
			if (!(text == "already"))
			{
				Choice choice2 = new Choice("I've served you well. Will you honour my blade?", delegate
				{
					Boon(c);
				}, ChoiceTone.Quest);
				if (text != null)
				{
					choice2.Off(text);
				}
				list.Add(choice2);
			}
		}

		private static bool IsLordOf(Actor lord, Actor you)
		{
			try
			{
				if (lord == null || you == null || lord == you || !lord.isAlive())
				{
					return false;
				}
				if (lord.isKing())
				{
					return lord.kingdom != null && lord.kingdom == you.kingdom;
				}
				return lord.isCityLeader() && lord.city != null && lord.city == you.city;
			}
			catch
			{
				return false;
			}
		}

		internal static string BoonRefusal(Actor lord, Actor you)
		{
			try
			{
				if (lord == null || you == null || lord.data == null || !lord.isAlive())
				{
					return "can't";
				}
				if (Granted(lord, you))
				{
					return "already";
				}
				if (Weapon(you) == null)
				{
					return "hold a weapon";
				}
				if (!BoonOffer(lord, you).Exists((string m) => CantPut(you, m) == null))
				{
					return "already";
				}
				if (DaysSinceBoon(you) < 3)
				{
					return $"not so soon after your last ({3} days)";
				}
				if (Chronicle.Opinion(lord, you) < 0)
				{
					return "they don't think well of you";
				}
				if (!Served(lord, you))
				{
					return "serve them first";
				}
			}
			catch
			{
				return "can't";
			}
			return null;
		}

		private static bool Served(Actor lord, Actor you)
		{
			try
			{
				if (Service.IsCaptain(you) || (Service.IsSoldier(you) && Service.Merit(you) >= 8f))
				{
					return true;
				}
				if (lord.isKing() && you.isCityLeader() && you.city != null && you.city.kingdom == lord.kingdom)
				{
					return true;
				}
				return you.data.renown >= (lord.isKing() ? 100 : 60);
			}
			catch
			{
				return false;
			}
		}

		private static string BoonWho(Actor you)
		{
			if (Run.LockedOn(you) && Run.Current != null)
			{
				return "r" + Run.Current.Id;
			}
			return you.data.id.ToString();
		}

		private static bool Granted(Actor lord, Actor you)
		{
			try
			{
				lord.data.get("fp_boon", out var pResult, null);
				if (string.IsNullOrEmpty(pResult))
				{
					return false;
				}
				string[] pArray = pResult.Split(',');
				return pArray.Contains(BoonWho(you)) || pArray.Contains(you.data.id.ToString());
			}
			catch
			{
				return false;
			}
		}

		private static int DaysSinceBoon(Actor you)
		{
			try
			{
				you.data.get("fp_boonday", out var pResult, -999);
				return Calendar.BillDay - pResult;
			}
			catch
			{
				return 999;
			}
		}

		private static void MarkGranted(Actor lord, Actor you)
		{
			try
			{
				lord.data.get("fp_boon", out var pResult, null);
				string text = BoonWho(you);
				lord.data.set("fp_boon", string.IsNullOrEmpty(pResult) ? text : (pResult + "," + text));
				you.data.set("fp_boonday", Calendar.BillDay);
			}
			catch
			{
			}
		}

		private static void Boon(Conversation c)
		{
			c.LastTopic = "boon";
			Actor lord = c.With;
			Actor you = c.You;
			c.Think(0.9f);
			string text = (lord.isKing() ? "The crown" : "This town");
			c.Say("*looks at your " + WeaponName(you) + ", then at you* " + text + " remembers who served it.", "Choose, then. My smith knows the old ways.");
			Choose(c, BoonOffer(lord, you), delegate(string got)
			{
				if (got == null)
				{
					c.Offer(Dialogue.Topics(c));
				}
				else
				{
					MarkGranted(lord, you);
					Chronicle.AddOwnMemory(lord, "fp_boon", Dialogue.NameOf(you));
					Chronicle.ChangeOpinion(lord, you, 5);
					c.Say("*hands it back* Carry it well. It " + Doing(got) + " now.");
					c.Offer(Dialogue.Topics(c));
				}
			});
		}

		internal static List<string> BoonOffer(Actor lord, Actor you)
		{
			List<string> list = new List<string>();
			int seed = 0;
			try
			{
				seed = (int)((lord.data.id * 31 + you.data.id) & 0x7FFFFFFF);
			}
			catch
			{
			}
			Random random = new Random(seed);
			List<string> list2 = Common.ToList();
			while (list.Count < 2 && list2.Count > 0)
			{
				int index = random.Next(list2.Count);
				list.Add(list2[index]);
				list2.RemoveAt(index);
			}
			int num = 0;
			try
			{
				num = you.data.renown;
			}
			catch
			{
			}
			if (num >= 250)
			{
				list.Add("divine_rune");
			}
			if (num >= 400)
			{
				list.Add("eternal");
			}
			return list;
		}
	}
}
