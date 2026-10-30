using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class Crafting
	{
		internal sealed class Recipe
		{
			public EquipmentAsset Asset;

			public Supplies.Kind Supply;

			public string Name;

			public string Cost;

			public EquipmentType Slot;

			public string Res1;

			public string Res2;

			public int Amount1;

			public int Amount2;

			public int Rank;

			public float Seconds;

			public string Id
			{
				get
				{
					if (Supply == null)
					{
						return Asset?.id;
					}
					return Supply.Id;
				}
			}
		}

		internal sealed class Order
		{
			public Recipe Recipe;

			public int Left;

			public int Total;
		}

		internal sealed class Made
		{
			public Recipe Recipe;

			public Gear.Piece Piece;

			public Rarity Quality;

			public string Name;

			public int Count;

			public bool Spilled;

			public float At;

			public int Serial;
		}

		public const float CraftSeconds = 4f;

		public const int MaxQueue = 4;

		public const int Batch = 5;

		private const float Impact = 0.26f;

		public readonly List<Recipe> Recipes = new List<Recipe>();

		public bool Open;

		public int Tab;

		public Vector2 Scroll;

		public Recipe Selected;

		public Recipe Working;

		public float WorkingFor;

		public bool ShowAll;

		public string Waiting;

		public bool AtBench;

		public int Swings;

		public int Blows;

		public float BlowAt = -9f;

		public Made Last;

		private int _swung;

		private int _struck;

		private int _serial;

		private static readonly string[] Earned = new string[7] { "flame", "ice", "poison", "stun", "slowness", "divine_rune", "eternal" };

		public readonly List<Order> Queue = new List<Order>();

		private Actor _queueFor;

		private long _queueForId = -1L;

		public const string QueueKey = "fp_forge";

		private bool _waitTold;

		public float Progress
		{
			get
			{
				if (Working == null)
				{
					return -1f;
				}
				return Mathf.Clamp01(WorkingFor / Mathf.Max(0.1f, Working.Seconds));
			}
		}

		public void Toggle()
		{
			Open = !Open;
			if (Open)
			{
				EnsureRecipes();
			}
		}

		public void EnsureRecipes()
		{
			if (Recipes.Count == 0)
			{
				LoadRecipes();
			}
		}

		internal static bool IsTool(Recipe r)
		{
			if (r != null && r.Asset != null && r.Asset.id != null)
			{
				return r.Asset.id.StartsWith("hammer_", StringComparison.Ordinal);
			}
			return false;
		}

		private static bool ByHand(string id)
		{
			if (!(id == "hammer_wood"))
			{
				return id == "hammer_stone";
			}
			return true;
		}

		private static float SecondsFor(Recipe r)
		{
			if (r.Supply == null)
			{
				return 2f + 0.4f * (float)Mathf.Clamp(r.Rank, 0, 5);
			}
			return 1.4f + 0.3f * (float)r.Rank;
		}

		private void LoadRecipes()
		{
			Recipes.Clear();
			try
			{
				foreach (EquipmentAsset item in AssetManager.items.list)
				{
					if (item != null && !string.IsNullOrEmpty(item.id))
					{
						EquipmentType equipment_type = item.equipment_type;
						if ((equipment_type == EquipmentType.Weapon || equipment_type == EquipmentType.Helmet || equipment_type == EquipmentType.Armor || equipment_type == EquipmentType.Boots) && !string.IsNullOrEmpty(item.cost_resource_id_1) && !(item.cost_resource_id_1 == "none") && item.cost_resource_1 > 0 && (!HasEarned(item) || item.id.StartsWith("hammer_", StringComparison.Ordinal)))
						{
							Recipe recipe = new Recipe
							{
								Asset = item,
								Slot = equipment_type,
								Res1 = item.cost_resource_id_1,
								Amount1 = item.cost_resource_1,
								Res2 = ((item.cost_resource_id_2 != "none" && item.cost_resource_2 > 0) ? item.cost_resource_id_2 : null),
								Amount2 = item.cost_resource_2,
								Name = ItemName(item),
								Rank = ((!ByHand(item.id)) ? Workbench.Rank(item) : 0)
							};
							recipe.Cost = recipe.Amount1 + " " + Work.ResourceName(recipe.Res1) + ((recipe.Res2 != null) ? (", " + recipe.Amount2 + " " + Work.ResourceName(recipe.Res2)) : "");
							recipe.Seconds = SecondsFor(recipe);
							Recipes.Add(recipe);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("could not read the item recipes: " + ex);
			}
			AddJewellery();
			Recipes.Sort(delegate(Recipe x, Recipe y)
			{
				int num2 = IsTool(y).CompareTo(IsTool(x));
				if (num2 == 0)
				{
					int slot = (int)x.Slot;
					num2 = slot.CompareTo((int)y.Slot);
				}
				if (num2 == 0)
				{
					num2 = x.Rank.CompareTo(y.Rank);
				}
				return (num2 == 0) ? x.Asset.equipment_value.CompareTo(y.Asset.equipment_value) : num2;
			});
			Supplies.Kind[] all = Supplies.All;
			foreach (Supplies.Kind kind in all)
			{
				if (!kind.Dug && kind.Res1 != null)
				{
					Recipe recipe2 = new Recipe
					{
						Supply = kind,
						Name = char.ToUpperInvariant(kind.One[0]) + kind.One.Substring(1),
						Res1 = kind.Res1,
						Amount1 = kind.Amount1,
						Res2 = kind.Res2,
						Amount2 = kind.Amount2,
						Rank = kind.Rank
					};
					recipe2.Cost = recipe2.Amount1 + " " + Work.ResourceName(recipe2.Res1) + ((recipe2.Res2 != null) ? (", " + recipe2.Amount2 + " " + Work.ResourceName(recipe2.Res2)) : "") + ((kind.Makes > 1) ? (". Makes " + kind.Makes) : "");
					recipe2.Seconds = SecondsFor(recipe2);
					Recipes.Add(recipe2);
				}
			}
			Log.Info("crafting: " + Recipes.Count + " recipes");
		}

		private void AddJewellery()
		{
			string[,] array = new string[5, 5]
			{
				{ "bone", "bones", "2", null, "0" },
				{ "copper", "common_metals", "2", "gold", "1" },
				{ "bronze", "common_metals", "3", "gold", "1" },
				{ "silver", "silver", "2", "gems", "1" },
				{ "mythril", "mythril", "2", "gems", "2" }
			};
			string[] array2 = new string[2] { "ring_", "amulet_" };
			foreach (string text in array2)
			{
				for (int j = 0; j < array.GetLength(0); j++)
				{
					try
					{
						EquipmentAsset a = AssetManager.items.get(text + array[j, 0]);
						string text2 = array[j, 1];
						string text3 = array[j, 3];
						if (a != null && AssetManager.resources.get(text2) != null && (text3 == null || AssetManager.resources.get(text3) != null) && !Recipes.Exists((Recipe x) => x.Asset == a))
						{
							Recipe recipe = new Recipe
							{
								Asset = a,
								Slot = a.equipment_type,
								Name = ItemName(a),
								Res1 = text2,
								Amount1 = int.Parse(array[j, 2]),
								Res2 = text3,
								Amount2 = int.Parse(array[j, 4])
							};
							recipe.Rank = Math.Max(Workbench.Rank(a), Math.Min(5, (text3 == "gems") ? 3 : ((!(text2 == "bones")) ? 2 : 0)));
							recipe.Cost = recipe.Amount1 + " " + Work.ResourceName(recipe.Res1) + ((recipe.Res2 != null) ? (", " + recipe.Amount2 + " " + Work.ResourceName(recipe.Res2)) : "");
							recipe.Seconds = SecondsFor(recipe);
							Recipes.Add(recipe);
						}
					}
					catch (Exception ex)
					{
						Log.Error("crafting: " + text + array[j, 0] + ": " + ex.Message);
					}
				}
			}
		}

		internal static bool IsEarned(string mod)
		{
			if (mod != null)
			{
				return Array.IndexOf(Earned, mod) >= 0;
			}
			return false;
		}

		private static bool HasEarned(EquipmentAsset a)
		{
			try
			{
				if (a.item_modifier_ids == null)
				{
					return false;
				}
				string[] item_modifier_ids = a.item_modifier_ids;
				for (int i = 0; i < item_modifier_ids.Length; i++)
				{
					if (IsEarned(item_modifier_ids[i]))
					{
						return true;
					}
				}
			}
			catch
			{
			}
			return false;
		}

		internal static void Ordinary(Item item)
		{
			try
			{
				bool flag = false;
				string[] earned = Earned;
				foreach (string text in earned)
				{
					if (item.hasMod(text))
					{
						item.removeMod(text);
						flag = true;
					}
				}
				if (flag)
				{
					item.calculateValues();
				}
			}
			catch (Exception ex)
			{
				Log.Error("crafting: enchantments: " + ex.Message);
			}
		}

		private static string ItemName(EquipmentAsset a)
		{
			try
			{
				string translatedName = a.getTranslatedName();
				if (!string.IsNullOrEmpty(translatedName) && !translatedName.StartsWith("$"))
				{
					return translatedName;
				}
			}
			catch
			{
			}
			string text = a.id.Replace('_', ' ');
			return char.ToUpperInvariant(text[0]) + text.Substring(1);
		}

		public static int Rolls(Actor host)
		{
			return 1 + Workbench.Usable(host);
		}

		public static int AffordCount(Actor host, Recipe r)
		{
			int num = CraftStores.Count(host, r.Res1);
			if (r.Res2 == null || r.Amount2 <= 0)
			{
				if (r.Amount1 <= 0)
				{
					return 99;
				}
				return num / r.Amount1;
			}
			if (r.Res2 == r.Res1)
			{
				return num / Math.Max(1, r.Amount1 + r.Amount2);
			}
			return Math.Min((r.Amount1 > 0) ? (num / r.Amount1) : 99, CraftStores.Count(host, r.Res2) / Math.Max(1, r.Amount2));
		}

		public static bool CanAfford(Actor host, Recipe r)
		{
			return AffordCount(host, r) >= 1;
		}

		public static string Locked(Actor host, Recipe r)
		{
			if (r.Rank <= 0)
			{
				return null;
			}
			int num = Workbench.Level(host);
			if (num == 0)
			{
				return "Needs a workbench, which needs a house";
			}
			if (num < r.Rank)
			{
				return $"Needs a level {r.Rank} workbench. Yours is level {num}";
			}
			if (!Workbench.AtHome(host))
			{
				return "Made at your workbench, at home";
			}
			return null;
		}

		public static bool CanMake(Actor host, Recipe r)
		{
			if (Locked(host, r) == null)
			{
				return CanAfford(host, r);
			}
			return false;
		}

		public int Queued(Recipe r)
		{
			int num = 0;
			foreach (Order item in Queue)
			{
				if (item.Recipe == r)
				{
					num += item.Left;
				}
			}
			return num;
		}

		public string WhyNot(Actor host, Recipe r, int count = 1)
		{
			bool flag = false;
			try
			{
				flag = host.equipment != null && host.understandsHowToUseItems();
			}
			catch
			{
			}
			if (!flag)
			{
				if (r.Supply == null)
				{
					return "This creature can't use gear";
				}
				return "This creature can't make that";
			}
			string text = Locked(host, r);
			if (text != null)
			{
				return text;
			}
			if (Queue.Count >= 4 && IsQueueOwner(host))
			{
				return "Already making " + 4 + " things. Wait for one";
			}
			if (Fits(host, r, 1) < 1)
			{
				return "No room in your bag for it";
			}
			if (!CanAfford(host, r))
			{
				return "You need " + r.Cost + " for a " + r.Name;
			}
			return null;
		}

		private int Fits(Actor host, Recipe r, int want)
		{
			if (r.Supply != null)
			{
				return Mathf.Clamp((Bag.RoomFor(host, r.Supply.Id) - r.Supply.Makes * (IsQueueOwner(host) ? Queued(r) : 0)) / Math.Max(1, r.Supply.Makes), 0, want);
			}
			int num = 0;
			if (IsQueueOwner(host))
			{
				foreach (Order item in Queue)
				{
					if (item.Recipe.Supply == null)
					{
						num += item.Left;
					}
				}
			}
			if (!Gear.HasRoom(host, num + 1))
			{
				return 0;
			}
			return Math.Min(1, want);
		}

		public bool Start(Actor host, Recipe r, Action<string> toast, int count = 1)
		{
			if (Queue.Count > 0 && !IsQueueOwner(host))
			{
				Abandon();
			}
			string text = WhyNot(host, r);
			if (text != null)
			{
				toast(text);
				return false;
			}
			int num = ((r.Supply == null) ? 1 : Math.Min(Fits(host, r, Math.Max(1, count)), AffordCount(host, r)));
			if (num < 1)
			{
				toast("You need " + r.Cost + " for a " + r.Name);
				return false;
			}
			bool flag = CraftStores.Take(host, r.Res1, r.Amount1 * num);
			if (flag && r.Res2 != null && !CraftStores.Take(host, r.Res2, r.Amount2 * num))
			{
				GiveBack(host, r.Res1, r.Amount1 * num);
				flag = false;
			}
			if (!flag)
			{
				toast("You need " + r.Cost + " for a " + r.Name);
				return false;
			}
			_queueFor = host;
			_queueForId = IdOf(host);
			Queue.Add(new Order
			{
				Recipe = r,
				Left = num,
				Total = num
			});
			SaveQueue();
			if (Queue.Count == 1)
			{
				StartPiece();
			}
			Working = Queue[0].Recipe;
			Log.Info("crafting: started " + r.Id + ((num > 1) ? (" x" + num) : "") + " (" + Queue.Count + " in hand)");
			return true;
		}

		public bool Cancel(Actor host, int index)
		{
			if (index < 0 || index >= Queue.Count)
			{
				return false;
			}
			Order order = Queue[index];
			Queue.RemoveAt(index);
			Refund(IsQueueOwner(host) ? host : _queueFor, order.Recipe, order.Left);
			SaveQueue();
			if (index == 0)
			{
				StartPiece();
			}
			Working = ((Queue.Count > 0) ? Queue[0].Recipe : null);
			Log.Info("crafting: called off " + order.Recipe.Id + ((order.Left > 1) ? (" x" + order.Left) : "") + ", materials back");
			return true;
		}

		public bool CancelLast(Actor host, Recipe r)
		{
			for (int num = Queue.Count - 1; num >= 0; num--)
			{
				if (Queue[num].Recipe == r)
				{
					return Cancel(host, num);
				}
			}
			return false;
		}

		private void Refund(Actor a, Recipe r, int n)
		{
			bool flag = false;
			try
			{
				flag = a?.isAlive() ?? false;
			}
			catch
			{
			}
			if (flag && n > 0)
			{
				GiveBack(a, r.Res1, r.Amount1 * n);
				if (r.Res2 != null)
				{
					GiveBack(a, r.Res2, r.Amount2 * n);
				}
			}
		}

		private void StartPiece()
		{
			WorkingFor = 0f;
			_swung = (_struck = 0);
		}

		private static long IdOf(Actor a)
		{
			try
			{
				return a?.data.id ?? (-1);
			}
			catch
			{
				return -1L;
			}
		}

		private bool IsQueueOwner(Actor a)
		{
			if (a != null && a == _queueFor)
			{
				return IdOf(a) == _queueForId;
			}
			return false;
		}

		public void Abandon()
		{
			Actor queueFor = _queueFor;
			bool flag = false;
			try
			{
				flag = queueFor != null && queueFor.isAlive() && IdOf(queueFor) == _queueForId;
			}
			catch
			{
			}
			if (flag)
			{
				foreach (Order item in Queue)
				{
					Refund(queueFor, item.Recipe, item.Left);
				}
			}
			if (Queue.Count > 0)
			{
				Log.Info("crafting: " + Queue.Count + " left unmade" + (flag ? ", materials back in the bag" : ", kept on its body"));
			}
			Queue.Clear();
			if (flag)
			{
				SaveQueue();
			}
			Working = null;
			StartPiece();
			_queueFor = null;
			_queueForId = -1L;
			Waiting = null;
			AtBench = false;
		}

		private void SaveQueue()
		{
			Actor queueFor = _queueFor;
			if (queueFor == null || IdOf(queueFor) != _queueForId)
			{
				return;
			}
			try
			{
				if (Queue.Count == 0)
				{
					queueFor.data.removeString("fp_forge");
					return;
				}
				List<string> list = new List<string>(Queue.Count);
				foreach (Order item in Queue)
				{
					list.Add(item.Recipe.Id + ":" + item.Left + ":" + item.Total);
				}
				queueFor.data.set("fp_forge", string.Join("|", list.ToArray()));
			}
			catch
			{
			}
		}

		internal void ForgetForTest()
		{
			Queue.Clear();
			Working = null;
			StartPiece();
			_queueFor = null;
			_queueForId = -1L;
		}

		private void RestoreQueue(Actor host)
		{
			string pResult = null;
			try
			{
				host.data.get("fp_forge", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return;
			}
			EnsureRecipes();
			string[] array = pResult.Split('|');
			foreach (string text in array)
			{
				string[] f = text.Split(':');
				if (f.Length >= 3 && int.TryParse(f[1], out var result) && int.TryParse(f[2], out var result2) && result > 0)
				{
					Recipe recipe = Recipes.Find((Recipe x) => x.Id == f[0]);
					if (recipe != null && Queue.Count < 4)
					{
						Queue.Add(new Order
						{
							Recipe = recipe,
							Left = result,
							Total = Math.Max(result, result2)
						});
					}
				}
			}
			_queueFor = host;
			_queueForId = IdOf(host);
			if (Queue.Count == 0)
			{
				SaveQueue();
				return;
			}
			StartPiece();
			Working = Queue[0].Recipe;
			Log.Info("crafting: " + Queue.Count + " order(s) kept through a load, carried on");
		}

		private static void GiveBack(Actor a, string resource, int amount)
		{
			try
			{
				if (amount > 0)
				{
					a.addToInventory(resource, amount);
				}
			}
			catch
			{
			}
		}

		private static int BlowsFor(Recipe r)
		{
			return Math.Max(2, Mathf.RoundToInt(r.Seconds / 0.7f));
		}

		public void Update(Actor host, float dt, Action<string> toast, bool busy = false)
		{
			Workbench.Watch(host, dt, toast);
			AtBench = false;
			Waiting = null;
			if (Queue.Count > 0 && !IsQueueOwner(host))
			{
				Abandon();
			}
			if (Queue.Count == 0 && host != null)
			{
				RestoreQueue(host);
			}
			if (Queue.Count == 0)
			{
				Working = null;
				_waitTold = false;
				return;
			}
			Order order = Queue[0];
			Working = order.Recipe;
			if (Locked(host, Working) != null)
			{
				if (Workbench.Level(host) < Working.Rank)
				{
					Queue.RemoveAt(0);
					Refund(host, order.Recipe, order.Left);
					SaveQueue();
					StartPiece();
					Working = ((Queue.Count > 0) ? Queue[0].Recipe : null);
					toast("Your workbench can't finish the " + order.Recipe.Name + ". Its materials are back in your bag");
					Log.Info("crafting: " + order.Recipe.Id + " given up, workbench level " + Workbench.Level(host));
				}
				else
				{
					Waiting = "Waits at your workbench, at home";
					if (!_waitTold)
					{
						_waitTold = true;
						toast("The " + Working.Name + " waits at your workbench, at home");
					}
				}
				return;
			}
			_waitTold = false;
			bool flag = Working.Supply == null && Workbench.AtHome(host);
			if (flag && busy)
			{
				Waiting = "Stand still to work";
				return;
			}
			AtBench = flag;
			WorkingFor += dt;
			int num = BlowsFor(Working);
			float num2 = Working.Seconds / (float)num;
			while (_swung < num && WorkingFor >= (float)_swung * num2)
			{
				_swung++;
				Swings++;
			}
			while (_struck < num && WorkingFor >= (float)_struck * num2 + Mathf.Min(0.26f, num2 * 0.5f))
			{
				_struck++;
				Blows++;
				BlowAt = Time.unscaledTime;
				Sounds.PlayAt(BlowSound(Working), host.current_position);
			}
			if (!(WorkingFor < Working.Seconds))
			{
				Recipe working = Working;
				order.Left--;
				if (order.Left <= 0)
				{
					Queue.RemoveAt(0);
				}
				SaveQueue();
				StartPiece();
				Working = ((Queue.Count > 0) ? Queue[0].Recipe : null);
				if (working.Supply != null)
				{
					MakeSupply(host, working, toast);
				}
				else
				{
					MakePiece(host, working, toast);
				}
			}
		}

		private static string BlowSound(Recipe r)
		{
			if (r.Supply != null)
			{
				return "event:/SFX/CIVILIZATIONS/CollectHerbs";
			}
			if (r.Rank <= 0)
			{
				if (!(r.Res1 == "stone"))
				{
					return "event:/SFX/HIT/HitWood";
				}
				return "event:/SFX/HIT/HitStone";
			}
			if (r.Rank != 1)
			{
				return "event:/SFX/HIT/HitMetal";
			}
			return "event:/SFX/HIT/HitStone";
		}

		private void MakeSupply(Actor host, Recipe r, Action<string> toast)
		{
			int num = Bag.Put(host, r.Supply.Id, r.Supply.Makes, spill: true);
			if (num < r.Supply.Makes)
			{
				toast("No room for " + Work.Amount(r.Supply.Id, r.Supply.Makes - num) + ". At your feet (E to pick up)");
			}
			Sounds.PlayAt("event:/SFX/UI/YesPoint", host.current_position);
			Made last = Last;
			if (last != null && last.Recipe == r && last.Piece == null && Time.unscaledTime - last.At < 2.2f)
			{
				last.Count += r.Supply.Makes;
				last.Name = Supplies.Counted(r.Supply, last.Count);
				last.Spilled |= num < r.Supply.Makes;
				last.At = Time.unscaledTime - 0.3f;
			}
			else
			{
				Done(new Made
				{
					Recipe = r,
					Quality = Rarity.R0_Normal,
					Name = Supplies.Counted(r.Supply, r.Supply.Makes),
					Count = r.Supply.Makes,
					Spilled = (num < r.Supply.Makes)
				});
			}
			Log.Info("crafted " + r.Supply.Makes + " " + r.Supply.Id + " for " + WorldBoxMod.Describe(host) + " into the bag");
		}

		private void MakePiece(Actor host, Recipe r, Action<string> toast)
		{
			try
			{
				int num = Rolls(host);
				Item item = World.world.items.generateItem(r.Asset, host.kingdom, host.getName(), num, host, 0, pByPlayer: true);
				Ordinary(item);
				Gear.Piece piece = Gear.FromItem(item);
				Gear.LetGo(item);
				if (piece == null)
				{
					toast("The " + r.Name + " broke while you were making it");
					return;
				}
				bool flag = Bag.PutPiece(host, piece, toast);
				Rarity rarity = CraftOdds.QualityOf(r.Asset, piece.Mods);
				Sounds.PlayAt("event:/SFX/HIT/HitSwordSword", host.current_position);
				Sounds.PlayAt("event:/SFX/UI/YesPoint", host.current_position);
				Done(new Made
				{
					Recipe = r,
					Piece = piece,
					Quality = rarity,
					Name = Gear.NameOf(piece),
					Count = 1,
					Spilled = !flag
				});
				Log.Info(string.Format("crafted {0} ({1}, {2} rolls, mods {3}) for {4} into the bag", r.Asset.id, CraftOdds.Word(rarity), num, string.Join("+", piece.Mods), WorldBoxMod.Describe(host)));
			}
			catch (Exception ex)
			{
				Log.Error("crafting " + r.Asset.id + " failed: " + ex);
				toast("The " + r.Name + " broke while you were making it");
			}
		}

		private void Done(Made m)
		{
			m.At = Time.unscaledTime;
			m.Serial = ++_serial;
			Last = m;
		}
	}
}
