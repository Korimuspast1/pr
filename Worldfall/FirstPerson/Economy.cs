using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal static class Economy
	{
		public const string GuardKey = "fp_guard";

		public const string GuardUntilKey = "fp_guard_t";

		public const string ProtectKey = "fp_guarded";

		public const int MaxHirelings = 3;

		public const float ThreatRange = 12f;

		public const float Heel = 4.5f;

		public static readonly List<Hireling> Hired = new List<Hireling>();

		private static Actor _hiredFor;

		private static float _tick;

		private static float _protectToastAt = -99f;

		private static readonly List<Actor> Threats = new List<Actor>();

		private static readonly Predicate<Actor> IsSparring = Quests.Sparring;

		private static readonly StringBuilder Text = new StringBuilder(64);

		private static readonly Dictionary<string, int> PriceCache = new Dictionary<string, int>();

		private static readonly EquipmentType[] ArmorySlots = new EquipmentType[6]
		{
			EquipmentType.Weapon,
			EquipmentType.Helmet,
			EquipmentType.Armor,
			EquipmentType.Boots,
			EquipmentType.Ring,
			EquipmentType.Amulet
		};

		private const float MonthTime = 5f;

		private static readonly List<Kingdom> Protectors = new List<Kingdom>();

		private static float _protectorsAt = -99f;

		private static Actor _protectorsFor;

		private static Actor _hpFor;

		private static int _hp;

		private static float _hurtAt = -99f;

		public static int Money(Actor a)
		{
			try
			{
				return a?.money ?? 0;
			}
			catch
			{
				return 0;
			}
		}

		public static bool Pay(Actor you, int amount, City town, Actor person = null)
		{
			if (amount <= 0)
			{
				return true;
			}
			if (Money(you) < amount)
			{
				return false;
			}
			try
			{
				you.spendMoney(amount);
				if (!Deposit(town, amount) && person != null && person != you)
				{
					person.addMoney(amount);
				}
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("economy: pay: " + ex.Message);
				return false;
			}
		}

		public static bool HasStores(City c)
		{
			try
			{
				return c?.hasStorages() ?? false;
			}
			catch
			{
				return false;
			}
		}

		public static int Treasury(City c)
		{
			try
			{
				return c?.getResourcesAmount("gold") ?? 0;
			}
			catch
			{
				return 0;
			}
		}

		public static bool Deposit(City c, int amount)
		{
			if (amount <= 0)
			{
				return true;
			}
			if (!HasStores(c))
			{
				return false;
			}
			try
			{
				c.addResourcesToRandomStockpile("gold", amount);
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("economy: deposit: " + ex.Message);
				return false;
			}
		}

		public static int CanPay(City c, Actor trader)
		{
			return Treasury(c) + Money(trader);
		}

		public static int PayOut(Actor you, int amount, City c, Actor trader)
		{
			if (amount <= 0)
			{
				return 0;
			}
			int num = Mathf.Min(amount, Treasury(c));
			try
			{
				if (num > 0)
				{
					c.takeResource("gold", num);
				}
			}
			catch
			{
				num = 0;
			}
			int num2 = Mathf.Min(amount - num, Money(trader));
			try
			{
				if (num2 > 0)
				{
					trader.spendMoney(num2);
				}
			}
			catch
			{
				num2 = 0;
			}
			int num3 = num + num2;
			try
			{
				if (num3 > 0)
				{
					you.addMoney(num3);
				}
			}
			catch (Exception ex)
			{
				Log.Error("economy: pay out: " + ex.Message);
			}
			return num3;
		}

		public static int PriceOf(string res)
		{
			if (string.IsNullOrEmpty(res))
			{
				return 1;
			}
			if (PriceCache.TryGetValue(res, out var value))
			{
				return value;
			}
			int num = 0;
			try
			{
				ResourceAsset resourceAsset = AssetManager.resources.get(res);
				if (resourceAsset != null)
				{
					num = ((resourceAsset.money_cost > 0) ? resourceAsset.money_cost : ((resourceAsset.trade_cost > 0) ? resourceAsset.trade_cost : resourceAsset.loot_value));
					if (num <= 0)
					{
						num = (resourceAsset.food ? 2 : ((!resourceAsset.mineral) ? 1 : 4));
					}
				}
			}
			catch
			{
			}
			switch (res)
			{
			case "common_metals":
			case "leather":
			case "silver":
				num = Mathf.Max(num, 3);
				break;
			case "gold":
				num = 1;
				break;
			case "mythril":
				num = Mathf.Max(num, 14);
				break;
			case "adamantine":
				num = Mathf.Max(num, 24);
				break;
			case "dragon_scales":
				num = Mathf.Max(num, 30);
				break;
			}
			num = Mathf.Clamp(num, 1, 80);
			PriceCache[res] = num;
			return num;
		}

		public static float Markup(Conversation c)
		{
			float num = 1f - 0.07f * (float)c.Voice.Warmth;
			if (Dialogue.HasTrait(c.With, "greedy"))
			{
				num += 0.2f;
			}
			if (Dialogue.HasTrait(c.With, "honest"))
			{
				num -= 0.05f;
			}
			if (!Dialogue.SameTribe(c.With, c.You))
			{
				num += 0.15f;
			}
			return Mathf.Clamp(num, 0.7f, 1.6f);
		}

		public static int BuyPrice(string res, int count, float markup)
		{
			if (!(res == "gold"))
			{
				return Mathf.Max(1, Mathf.CeilToInt((float)(PriceOf(res) * count) * 1.5f * markup));
			}
			return count;
		}

		public static int SellPrice(string res, int count, float markup)
		{
			if (!(res == "gold"))
			{
				return Mathf.Max(1, Mathf.FloorToInt((float)(PriceOf(res) * count) * 0.6f / markup));
			}
			return count;
		}

		public static int ItemPrice(Item item, float markup)
		{
			try
			{
				EquipmentAsset asset = item.getAsset();
				int num = asset.cost_gold;
				if (!string.IsNullOrEmpty(asset.cost_resource_id_1) && asset.cost_resource_id_1 != "none")
				{
					num += asset.cost_resource_1 * PriceOf(asset.cost_resource_id_1);
				}
				if (!string.IsNullOrEmpty(asset.cost_resource_id_2) && asset.cost_resource_id_2 != "none")
				{
					num += asset.cost_resource_2 * PriceOf(asset.cost_resource_id_2);
				}
				num = Mathf.Max((asset.equipment_type == EquipmentType.Weapon) ? 20 : 14, num);
				num = Mathf.Max(num, Traders.Worth(asset)) + Traders.ModWorth(asset, item.data.modifiers);
				float num2 = 1f + 0.45f * (float)item.getQuality();
				return Mathf.Clamp(Mathf.RoundToInt((float)num * num2 * markup), 5, 2000);
			}
			catch
			{
				return 25;
			}
		}

		public static string ItemName(Item item)
		{
			try
			{
				string text = item.getAsset().getTranslatedName();
				if (string.IsNullOrEmpty(text) || text.StartsWith("$"))
				{
					text = item.getAsset().id.Replace('_', ' ');
				}
				Rarity quality = item.getQuality();
				return ((quality == Rarity.R0_Normal) ? "" : (quality.ToString().Substring(quality.ToString().IndexOf('_') + 1).ToLowerInvariant() + " ")) + text.ToLowerInvariant();
			}
			catch
			{
				return "piece";
			}
		}

		public static int GuardFee(Actor guard, int years)
		{
			int num = 1;
			try
			{
				num = Mathf.Max(1, guard.data.level);
			}
			catch
			{
			}
			float num2 = 3f + (float)num * 1.2f + (guard.isWarrior() ? 3f : 0f);
			return Mathf.Max(5, Mathf.RoundToInt(num2 * (float)years * ((years >= 10) ? 0.8f : 1f)));
		}

		public static List<Item> Armory(City c, int most)
		{
			List<Item> list = new List<Item>();
			if (c == null)
			{
				return list;
			}
			try
			{
				EquipmentType[] armorySlots = ArmorySlots;
				foreach (EquipmentType pType in armorySlots)
				{
					List<long> equipmentList = c.getEquipmentList(pType);
					if (equipmentList == null)
					{
						continue;
					}
					List<Item> list2 = new List<Item>();
					foreach (long item2 in equipmentList)
					{
						Item item = World.world.items.get(item2);
						if (item != null && !item.isCursed())
						{
							list2.Add(item);
						}
					}
					list2.Sort((Item x, Item y) => ItemPrice(y, 1f).CompareTo(ItemPrice(x, 1f)));
					for (int num = 0; num < list2.Count && num < 2; num++)
					{
						list.Add(list2[num]);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("economy: armory: " + ex.Message);
			}
			if (list.Count > most)
			{
				list.RemoveRange(most, list.Count - most);
			}
			return list;
		}

		public static bool Equip(Actor you, Item item, City c, out string note)
		{
			note = "";
			try
			{
				EquipmentType equipment_type = item.getAsset().equipment_type;
				ActorEquipmentSlot slot = you.equipment.getSlot(equipment_type);
				Item item2 = slot.getItem();
				if (item2 != null && item2.isCursed())
				{
					note = "Your cursed " + ItemName(item2) + " won't come off.";
					return false;
				}
				List<long> equipmentList = c.getEquipmentList(equipment_type);
				if (equipmentList == null || !equipmentList.Remove(item.data.id))
				{
					note = "It's gone from the armory.";
					return false;
				}
				item.clearCity();
				c._storage_version++;
				if (item2 != null)
				{
					Gear.Piece piece = Gear.FromItem(item2);
					bool value = Bag.Free(you) > 0;
					slot.takeAwayItem();
					Gear.LetGo(item2);
					bool flag = piece != null && Bag.PutPiece(you, piece, null, value);
					note = ((piece == null) ? "" : (flag ? (" Your old " + Gear.NameOf(piece).ToLowerInvariant() + " is in your bag.") : (" Your old " + Gear.NameOf(piece).ToLowerInvariant() + " is at your feet.")));
				}
				slot.setItem(item, you);
				you.setStatsDirty();
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("economy: equip: " + ex);
				note = "Something went wrong with the sale.";
				return false;
			}
		}

		public static Hireling HirelingOf(Actor a)
		{
			foreach (Hireling item in Hired)
			{
				if (item.Guard == a)
				{
					return item;
				}
			}
			return null;
		}

		public static bool CanHire(Actor you, Actor npc, out string why)
		{
			why = null;
			try
			{
				if (npc.isKing() || npc.isCityLeader())
				{
					why = "Rulers don't hire out.";
					return false;
				}
				if (!npc.isAdult())
				{
					why = "Too young to fight.";
					return false;
				}
				if (Dialogue.Enemies(npc, you))
				{
					why = "They'd sooner fight you.";
					return false;
				}
				if (Law.BountyOn(npc.kingdom, you) > 0)
				{
					why = "Not while you're wanted here.";
					return false;
				}
				if (Hired.Count >= 3 && HirelingOf(npc) == null)
				{
					why = $"You already have {3} guards.";
					return false;
				}
				return true;
			}
			catch
			{
				why = "Not now.";
				return false;
			}
		}

		public static void Hire(Actor you, Actor guard, int years, int fee)
		{
			Hireling hireling = HirelingOf(guard);
			double now = Quests.Now;
			double num = (double)years * 12.0 * 5.0;
			if (hireling == null)
			{
				hireling = new Hireling
				{
					Guard = guard,
					Until = now + num,
					Id = guard.data.id
				};
				Hired.Add(hireling);
			}
			else
			{
				hireling.Until = Math.Max(hireling.Until, now) + num;
			}
			SaveHireling(you, hireling);
			Chronicle.ChangeOpinion(guard, you, 6);
			try
			{
				guard.changeHappiness("just_received_gift");
			}
			catch
			{
			}
			Log.Info($"economy: hired {WorldBoxMod.Describe(guard)} for {years}y ({fee} gold), until {hireling.Until:F0}");
			Heel_(you, guard, force: true);
		}

		public static void Dismiss(Hireling h, string why, Action<string> toast)
		{
			Hired.Remove(h);
			Actor guard = h.Guard;
			if (guard == null)
			{
				return;
			}
			bool flag = false;
			try
			{
				flag = guard.data != null && (h.Id == 0L || guard.data.id == h.Id);
			}
			catch
			{
			}
			if (!flag)
			{
				if (why != null)
				{
					toast?.Invoke(why);
				}
				return;
			}
			try
			{
				guard.data.removeLong("fp_guard");
				guard.data.removeFloat("fp_guard_t");
			}
			catch
			{
			}
			try
			{
				if (guard.isAlive() && Marching(guard))
				{
					guard.cancelAllBeh();
					guard.beh_tile_target = null;
				}
			}
			catch
			{
			}
			if (why != null)
			{
				toast?.Invoke(why);
			}
			Log.Info("economy: " + Dialogue.NameOf(guard) + " no longer guards you" + ((why != null) ? (" (" + why + ")") : ""));
		}

		private static void SaveHireling(Actor you, Hireling h)
		{
			try
			{
				h.Guard.data.set("fp_guard", you.data.id);
				h.Guard.data.set("fp_guard_t", (float)h.Until);
			}
			catch (Exception ex)
			{
				Log.Error("economy: save hireling: " + ex.Message);
			}
		}

		private static void RebuildHirelings(Actor you)
		{
			Hired.Clear();
			_hiredFor = you;
			if (you == null)
			{
				return;
			}
			long id = you.data.id;
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple == null || !simple.isAlive())
					{
						continue;
					}
					simple.data.get("fp_guard", out var pResult, 0L);
					if (pResult == id)
					{
						simple.data.get("fp_guard_t", out var pResult2, 0f);
						Hired.Add(new Hireling
						{
							Guard = simple,
							Until = pResult2,
							Id = simple.data.id
						});
						if (Hired.Count >= 3)
						{
							break;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("economy: rebuild: " + ex.Message);
			}
			if (Hired.Count > 0)
			{
				Log.Info($"economy: {Hired.Count} guard(s) still sworn to {WorldBoxMod.Describe(you)}");
			}
		}

		public static string TimeLeft(double until)
		{
			double num = (until - Quests.Now) / 5.0;
			if (num <= 0.0)
			{
				return "done";
			}
			if (num < 12.0)
			{
				int num2 = Mathf.CeilToInt((float)num);
				return num2 + ((num2 == 1) ? " month" : " months");
			}
			int num3 = Mathf.FloorToInt((float)(num / 12.0));
			return num3 + ((num3 == 1) ? " year" : " years");
		}

		public static double ProtectedUntil(Kingdom k, Actor you)
		{
			if (k == null || you == null)
			{
				return 0.0;
			}
			try
			{
				k.data.get("fp_guarded", out var pResult, null);
				if (string.IsNullOrEmpty(pResult))
				{
					return 0.0;
				}
				string[] array = pResult.Split('|');
				foreach (string text in array)
				{
					int num = text.IndexOf(':');
					if (num > 0 && long.TryParse(text.Substring(0, num), out var result) && result == you.data.id && double.TryParse(text.Substring(num + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
					{
						return result2;
					}
				}
			}
			catch
			{
			}
			return 0.0;
		}

		public static bool IsProtected(Kingdom k, Actor you)
		{
			return ProtectedUntil(k, you) > Quests.Now;
		}

		public static void SetProtection(Kingdom k, Actor you, double until)
		{
			if (k == null || you == null)
			{
				return;
			}
			try
			{
				k.data.get("fp_guarded", out var pResult, null);
				Text.Length = 0;
				if (!string.IsNullOrEmpty(pResult))
				{
					string[] array = pResult.Split('|');
					foreach (string text in array)
					{
						if (!string.IsNullOrEmpty(text) && !text.StartsWith(you.data.id + ":", StringComparison.Ordinal))
						{
							if (Text.Length > 0)
							{
								Text.Append('|');
							}
							Text.Append(text);
						}
					}
				}
				if (until > Quests.Now)
				{
					if (Text.Length > 0)
					{
						Text.Append('|');
					}
					Text.Append(you.data.id).Append(':').Append(until.ToString("F1", CultureInfo.InvariantCulture));
				}
				if (Text.Length == 0)
				{
					k.data.removeString("fp_guarded");
				}
				else
				{
					k.data.set("fp_guarded", Text.ToString());
				}
				_protectorsAt = -99f;
			}
			catch (Exception ex)
			{
				Log.Error("economy: protection: " + ex.Message);
			}
		}

		public static int ProtectionFee(Kingdom k, int years)
		{
			int num = 0;
			try
			{
				foreach (Actor item in Civil.SubjectsOf(k))
				{
					if (item.isWarrior())
					{
						num++;
					}
				}
			}
			catch
			{
			}
			float num2 = 6f + (float)Mathf.Min(num, 60) * 0.5f;
			return Mathf.Max(10, Mathf.RoundToInt(num2 * (float)years * ((years >= 20) ? 0.8f : 1f)));
		}

		public static void Protect(Kingdom k, Actor you, int years)
		{
			double now = Quests.Now;
			double num = Math.Max(ProtectedUntil(k, you), now) + (double)years * 12.0 * 5.0;
			SetProtection(k, you, num);
			Log.Info($"economy: {k.name} protects {WorldBoxMod.Describe(you)} for {years}y (until {num:F0})");
		}

		public static List<Kingdom> ProtectorsOf(Actor you)
		{
			if (you == _protectorsFor && Time.unscaledTime - _protectorsAt < 3f)
			{
				return Protectors;
			}
			_protectorsAt = Time.unscaledTime;
			_protectorsFor = you;
			Protectors.Clear();
			try
			{
				foreach (Kingdom item in World.world.kingdoms.list)
				{
					if (item != null && item.isAlive() && item.isCiv() && IsProtected(item, you))
					{
						Protectors.Add(item);
					}
				}
			}
			catch
			{
			}
			return Protectors;
		}

		public static void OnYourHit(Actor victim)
		{
			if (victim == null || Hired.Count == 0)
			{
				return;
			}
			Actor actor = WorldBoxMod.Instance?.Host;
			if (actor == null || !Deserves(victim, actor))
			{
				return;
			}
			foreach (Hireling item in Hired)
			{
				Actor guard = item.Guard;
				if (guard != null && guard != victim && guard.isAlive())
				{
					bool flag = false;
					try
					{
						flag = guard.kingdom != null && victim.kingdom == guard.kingdom && victim.attack_target != actor;
					}
					catch
					{
					}
					if (!flag)
					{
						Attack(guard, victim);
					}
				}
			}
		}

		private static bool Deserves(Actor victim, Actor you)
		{
			try
			{
				if (victim.has_attack_target && victim.attack_target == you)
				{
					return true;
				}
				if (!Dialogue.IsPerson(victim))
				{
					return true;
				}
				return Dialogue.Enemies(victim, you);
			}
			catch
			{
				return false;
			}
		}

		public static void Update(Actor you, float dt, Action<string> toast)
		{
			if (you == null)
			{
				return;
			}
			if (you != _hiredFor)
			{
				RebuildHirelings(you);
			}
			_tick -= dt;
			if (_tick > 0f)
			{
				return;
			}
			_tick = 0.5f;
			double now = Quests.Now;
			for (int num = Hired.Count - 1; num >= 0; num--)
			{
				Hireling hireling = Hired[num];
				Actor guard = hireling.Guard;
				if (!hireling.Still)
				{
					Dismiss(hireling, (guard != null && guard.isAlive() && guard.data != null && guard.data.id == hireling.Id) ? ("Your guard " + Dialogue.NameOf(guard) + " has fallen.") : "Your guard has fallen.", toast);
				}
				else if (hireling.Until <= now)
				{
					Dismiss(hireling, Dialogue.NameOf(guard) + "'s contract is up. (Talk to them to hire them again.)", toast);
				}
				else if (Law.BountyOn(guard.kingdom, you) > 0)
				{
					Dismiss(hireling, Dialogue.NameOf(guard) + " won't guard an outlaw of " + guard.kingdom.name + ".", toast);
				}
				else if (Dialogue.Enemies(guard, you))
				{
					Dismiss(hireling, Dialogue.NameOf(guard) + " leaves you: your realms are at war.", toast);
				}
			}
			foreach (Kingdom item in ProtectorsOf(you))
			{
				if (Law.BountyOn(item, you) > 0)
				{
					SetProtection(item, you, 0.0);
					toast?.Invoke(item.name + " withdraws its protection: you broke its law.");
					break;
				}
			}
			FindThreats(you);
			foreach (Hireling item2 in Hired)
			{
				Actor guard2 = item2.Guard;
				Actor actor = Nearest(guard2);
				if (actor != null)
				{
					Attack(guard2, actor);
					continue;
				}
				bool flag = false;
				try
				{
					flag = guard2.has_attack_target && guard2.attack_target != null && guard2.attack_target.isAlive();
				}
				catch
				{
				}
				if (!flag)
				{
					Heel_(you, guard2, force: false);
				}
			}
			if (Threats.Count > 0)
			{
				CallTheGuard(you, toast);
			}
		}

		private static void FindThreats(Actor you)
		{
			Threats.Clear();
			Vector2 current_position = you.current_position;
			int num = 0;
			try
			{
				num = you.getHealth();
			}
			catch
			{
			}
			if (you != _hpFor)
			{
				_hpFor = you;
				_hp = num;
				_hurtAt = -99f;
			}
			if (num < _hp)
			{
				_hurtAt = Time.unscaledTime;
			}
			_hp = num;
			try
			{
				BaseSimObject attackedBy = you.attackedBy;
				if (Time.unscaledTime - _hurtAt < 5f && attackedBy != null && attackedBy.isAlive() && attackedBy.isActor() && (attackedBy.current_position - current_position).sqrMagnitude < 144f)
				{
					Threats.Add(attackedBy.a);
				}
			}
			catch
			{
			}
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple != you && !((simple.current_position - current_position).sqrMagnitude > 144f) && simple.isAlive() && !Threats.Contains(simple) && simple.has_attack_target && simple.attack_target == you)
					{
						Threats.Add(simple);
					}
				}
			}
			catch
			{
			}
			foreach (Hireling item in Hired)
			{
				Threats.Remove(item.Guard);
			}
			Threats.RemoveAll(IsSparring);
		}

		public static bool InAFight(Actor you)
		{
			if (you == _hpFor)
			{
				if (!(Time.unscaledTime - _hurtAt < 8f))
				{
					return Threats.Count > 0;
				}
				return true;
			}
			return false;
		}

		private static Actor Nearest(Actor from)
		{
			Actor result = null;
			float num = float.MaxValue;
			foreach (Actor threat in Threats)
			{
				float sqrMagnitude = (threat.current_position - from.current_position).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					result = threat;
				}
			}
			return result;
		}

		private static void Attack(Actor g, Actor target)
		{
			try
			{
				if (!g.has_attack_target || g.attack_target != target)
				{
					if (Marching(g))
					{
						g.cancelAllBeh();
						g.beh_tile_target = null;
					}
					g.addAggro(target);
					g.startFightingWith(target);
				}
			}
			catch (Exception ex)
			{
				Log.Error("economy: guard attack: " + ex.Message);
			}
		}

		private static void Heel_(Actor you, Actor g, bool force)
		{
			try
			{
				float magnitude = (g.current_position - you.current_position).magnitude;
				if ((force || !(magnitude < 4.5f)) && g.ai != null && !g.is_inside_building)
				{
					WorldTile worldTile = World.world.GetTile((int)you.current_position.x, (int)you.current_position.y) ?? you.current_tile;
					if (worldTile != null && (force || !Marching(g) || g.beh_tile_target == null || Mathf.Abs(g.beh_tile_target.x - worldTile.x) > 3 || Mathf.Abs(g.beh_tile_target.y - worldTile.y) > 3))
					{
						Royal.RegisterTask();
						g.setTask("fp_royal_march");
						g.beh_tile_target = Beside(worldTile);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("economy: heel: " + ex.Message);
			}
		}

		private static bool Marching(Actor a)
		{
			try
			{
				return a.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id == "fp_royal_march";
			}
			catch
			{
				return false;
			}
		}

		private static WorldTile Beside(WorldTile t)
		{
			for (int i = 0; i < 10; i++)
			{
				WorldTile tile = World.world.GetTile(t.x + UnityEngine.Random.Range(-2, 3), t.y + UnityEngine.Random.Range(-2, 3));
				if (tile != null && tile != t && tile.Type != null && tile.Type.ground && !tile.Type.block && !tile.is_liquid)
				{
					return tile;
				}
			}
			return t;
		}

		private static void CallTheGuard(Actor you, Action<string> toast)
		{
			List<Kingdom> list = ProtectorsOf(you);
			if (list.Count == 0)
			{
				return;
			}
			foreach (Kingdom item in list)
			{
				int num = 0;
				foreach (Actor item2 in Civil.SubjectsOf(item))
				{
					if (num >= 4)
					{
						break;
					}
					if (item2 == null || item2 == you || !item2.isAlive() || !item2.isWarrior() || item2.is_inside_building || (item2.current_position - you.current_position).sqrMagnitude > 400f)
					{
						continue;
					}
					Actor actor = Nearest(item2);
					if (actor == null || actor.kingdom == item)
					{
						continue;
					}
					if (item2.has_attack_target && item2.attack_target != null && item2.attack_target.isAlive())
					{
						if (item2.attack_target.isActor() && Threats.Contains(item2.attack_target.a))
						{
							num++;
						}
					}
					else
					{
						Attack(item2, actor);
						num++;
					}
				}
				if (num > 0 && Time.unscaledTime - _protectToastAt > 15f)
				{
					_protectToastAt = Time.unscaledTime;
					toast?.Invoke("Guards of " + item.name + " come to your aid!");
					Log.Info($"economy: {num} guard(s) of {item.name} defend {WorldBoxMod.Describe(you)}");
				}
			}
		}
	}
}
