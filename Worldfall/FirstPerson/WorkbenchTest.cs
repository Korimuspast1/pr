using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class WorkbenchTest : CivilTest
	{
		private sealed class TestChest : CraftStores.Store
		{
			public readonly Dictionary<string, int> Held = new Dictionary<string, int>();

			public override string Name => "a test chest";

			public override int Count(string resource)
			{
				if (!Held.TryGetValue(resource, out var value))
				{
					return 0;
				}
				return value;
			}

			public override int Take(string resource, int amount)
			{
				int num = Math.Min(amount, Count(resource));
				Held[resource] = Count(resource) - num;
				return num;
			}
		}

		private Actor _host;

		private Actor _giver;

		private City _city;

		private Building _house;

		protected override string Prefix => "WB";

		private Hud H => Mod.HudForTest;

		private Crafting Craft => Mod.Craft;

		protected override IEnumerator Body()
		{
			Actor actor = Village("human", 8, 0);
			if (!Check("a village", actor != null))
			{
				yield break;
			}
			_city = actor.city;
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != actor && item.isAdult())
				{
					if (_host == null)
					{
						_host = item;
					}
					else if (_giver == null)
					{
						_giver = item;
						break;
					}
				}
			}
			if (_host == null)
			{
				_host = actor;
			}
			yield return Possess(_host);
			if (Check("first person in a villager", InControl(_host)))
			{
				try
				{
					((BaseSimObject)_host).addStatusEffect("invincible", 600f, pColorEffect: false);
				}
				catch
				{
				}
				try
				{
					_host.data.removeInt("fp_bench");
				}
				catch
				{
				}
				Mod.Settings.HeadBob = 0f;
				Craft.Toggle();
				Craft.Open = false;
				yield return Part("ranks", Ranks());
				yield return Part("by hand", ByHand());
				yield return Part("a house", House());
				yield return Part("tools from a task", Tools());
				yield return Part("a grander house", Grander());
				yield return Part("enchantments", Enchantments());
				yield return Part("the bag and chests", BagAndChests());
				yield return Part("the bench at home", Bench());
				yield return Part("odd cases", OddCases());
				yield return Part("a new body", NewBody());
				Quests.ToolsForTest = -1;
				Quests.ForceKindForTest = -1;
			}
		}

		private IEnumerator Ranks()
		{
			string bad = "";
			Want("sword_wood", 0);
			Want("sword_stone", 1);
			Want("sword_copper", 2);
			Want("sword_bronze", 2);
			Want("sword_iron", 3);
			Want("sword_silver", 3);
			Want("sword_steel", 3);
			Want("sword_mythril", 4);
			Want("sword_adamantine", 5);
			Want("armor_leather", 0);
			Want("helmet_iron", 3);
			Want("boots_adamantine", 5);
			Check("what each item needs: wood by hand, stone 1 ... adamantine 5", bad.Length == 0, (bad.Length == 0) ? null : bad);
			Check("no recipe for gear that comes enchanted (a flame sword)", Recipe("flame_sword") == null && AssetManager.items.get("flame_sword") != null);
			Crafting.Recipe recipe = Recipe("plague_doctor_staff");
			Check("a staff of mythril and gems needs level 4", recipe == null || recipe.Rank == 4, (recipe != null) ? recipe.Rank.ToString() : "none");
			int num = 0;
			foreach (Crafting.Recipe recipe2 in Craft.Recipes)
			{
				if (recipe2.Rank > 0)
				{
					num++;
				}
			}
			Check("most recipes need a workbench", Craft.Recipes.Count > 20 && num > Craft.Recipes.Count / 2, num + " of " + Craft.Recipes.Count);
			void Want(string id, int rank)
			{
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				if (equipmentAsset == null)
				{
					Note("(no item " + id + ")");
				}
				else if (Workbench.Rank(equipmentAsset) != rank)
				{
					bad += $" {id}={Workbench.Rank(equipmentAsset)} (not {rank})";
				}
			}
			yield break;
		}

		private Crafting.Recipe Recipe(string id)
		{
			return Craft.Recipes.Find((Crafting.Recipe r) => r.Asset != null && r.Asset.id == id);
		}

		private IEnumerator Make(string id, bool wear = true, float wait = 2.3f)
		{
			Crafting.Recipe recipe = Recipe(id);
			if (recipe == null)
			{
				Note("(no recipe " + id + ")");
				yield break;
			}
			Give(recipe);
			bool started = Craft.Start(_host, recipe, Mod.Toast);
			yield return CivilTest.Wait(wait);
			yield return Done();
			if (started && wear)
			{
				int num = Gear.Of(_host).FindLastIndex((Gear.Piece p) => p.Id == id);
				if (num >= 0)
				{
					Gear.PutOn(_host, num, Mod.Toast);
				}
			}
		}

		private IEnumerator Done()
		{
			for (float t = 0f; t < 12f; t += 0.25f)
			{
				if (Craft.Queue.Count <= 0)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			yield return null;
		}

		private void Give(Crafting.Recipe r)
		{
			try
			{
				_host.addToInventory(r.Res1, r.Amount1 + 1);
				if (r.Res2 != null)
				{
					_host.addToInventory(r.Res2, r.Amount2 + 1);
				}
			}
			catch (Exception ex)
			{
				Note("resources: " + ex.Message);
			}
		}

		private string Weapon()
		{
			try
			{
				return _host.getWeapon()?.asset.id ?? "none";
			}
			catch
			{
				return "?";
			}
		}

		private void Move(Vector2 at)
		{
			try
			{
				_host.setCurrentTilePosition(World.world.GetTile((int)at.x, (int)at.y));
			}
			catch (Exception ex)
			{
				Note("move: " + ex.Message);
			}
		}

		private IEnumerator ByHand()
		{
			try
			{
				_host.clearHomeBuilding();
			}
			catch
			{
			}
			Check("no house, no workbench", Workbench.Level(_host) == 0 && Workbench.Usable(_host) == 0);
			Crafting.Recipe recipe = Recipe("sword_stone");
			Check("a stone sword needs a house's workbench", recipe != null && Crafting.Locked(_host, recipe) == "Needs a workbench, which needs a house", (recipe != null) ? Crafting.Locked(_host, recipe) : null);
			yield return Make("sword_stone");
			Check("and isn't made", Weapon() != "sword_stone", Weapon() + ", toast \"" + Mod.ToastText + "\"");
			yield return Make("sword_wood");
			Check("a wooden sword is made by hand anywhere", Weapon() == "sword_wood", Weapon());
		}

		private IEnumerator House()
		{
			_house = PlaceHouse("house_human_0");
			if (Check("a small house", _house != null))
			{
				_host.setHomeBuilding(_house);
				yield return CivilTest.Wait(1f);
				Check("it is your home", Workbench.HomeOf(_host) == _house);
				Check("its workbench is level 1", Workbench.HouseLevel(_host) == 1 && Workbench.Level(_host) == 1, $"house {Workbench.HouseLevel(_host)}, level {Workbench.Level(_host)}");
				Vector2 door = HouseSpot();
				Move(door + new Vector2(18f, 0f));
				yield return CivilTest.Wait(0.5f);
				Crafting.Recipe stone = Recipe("sword_stone");
				Crafting.Recipe copper = Recipe("sword_copper");
				Check("away from home a stone sword waits for the workbench", !Workbench.AtHome(_host) && Crafting.Locked(_host, stone) == "Made at your workbench, at home", Crafting.Locked(_host, stone));
				Craft.Open = true;
				yield return Shot("wb_away.png");
				Move(door);
				yield return CivilTest.Wait(0.5f);
				Check("by your house you are at your workbench", Workbench.AtHome(_host) && Workbench.Usable(_host) == 1);
				Check("copper is beyond a level 1 workbench", copper == null || Crafting.Locked(_host, copper) == "Needs a level 2 workbench. Yours is level 1", (copper != null) ? Crafting.Locked(_host, copper) : null);
				yield return Make("sword_stone");
				Check("a stone sword is made at home", Weapon() == "sword_stone", Weapon());
				yield return Shot("wb_home.png");
				Craft.Open = false;
				yield return HouseOnMap();
				yield return AwayMidWork(door, stone);
			}
		}

		private IEnumerator AwayMidWork(Vector2 door, Crafting.Recipe stone)
		{
			if (stone == null)
			{
				yield break;
			}
			Give(stone);
			if (!Check("a stone sword started at home", Craft.Start(_host, stone, Mod.Toast)))
			{
				yield break;
			}
			yield return CivilTest.Wait(0.3f);
			Move(door + new Vector2(18f, 0f));
			yield return CivilTest.Wait(0.3f);
			float was = Craft.Progress;
			yield return CivilTest.Wait(1.5f);
			Check("away from home the work waits", Craft.Queue.Count == 1 && Mathf.Abs(Craft.Progress - was) < 0.01f && Mod.ToastText != null && Mod.ToastText.Contains("waits at your workbench"), $"{was:F2} -> {Craft.Progress:F2}, toast \"{Mod.ToastText}\"");
			Move(door);
			yield return Done();
			Check("back home it is finished", Craft.Queue.Count == 0 && Gear.Of(_host).Exists((Gear.Piece p) => p.Id == "sword_stone"), Craft.Queue.Count + " in hand");
			Give(stone);
			int had = CraftStores.Count(_host, stone.Res1);
			Check("another one started", Craft.Start(_host, stone, Mod.Toast));
			try
			{
				_host.clearHomeBuilding();
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.5f);
			int num = CraftStores.Count(_host, stone.Res1);
			Check("no house any more: it is given up and paid back", Craft.Queue.Count == 0 && num == had && Mod.ToastText != null && Mod.ToastText.Contains("can't finish"), $"{Craft.Queue.Count} in hand, {stone.Res1} {had} -> {num}, toast \"{Mod.ToastText}\"");
			_host.setHomeBuilding(_house);
			yield return CivilTest.Wait(0.5f);
			for (int num2 = 0; num2 < 8; num2++)
			{
				int num3 = Gear.Of(_host).FindLastIndex((Gear.Piece x) => x.Id == "sword_stone");
				if (num3 >= 0)
				{
					Gear.Remove(_host, num3);
					continue;
				}
				break;
			}
		}

		private IEnumerator HouseOnMap()
		{
			Check("the map knows where your house is", Hud.HomeAt(_host, out var at) && Vector2.Distance(at, new Vector2((float)_house.current_tile.x + 0.5f, (float)_house.current_tile.y + 0.5f)) < 0.1f);
			H.HomeMarkDrawnForTest = null;
			Mod.ShowMapPage(MapTabs.Page.Map);
			yield return null;
			Journal.Centre = at;
			Journal.Across = 64f;
			Journal.GlideTo = null;
			yield return Shot("wb_map_home.png");
			Check("your house is marked on the map", Journal.Open && H.HomeMarkDrawnForTest.HasValue, (!Journal.Open) ? "not open" : (H.HomeMarkDrawnForTest.HasValue ? $"at {H.HomeMarkDrawnForTest.Value.x:F0},{H.HomeMarkDrawnForTest.Value.y:F0}" : "open, no mark"));
			Mod.CloseMapWindow();
			yield return null;
		}

		private IEnumerator Tools()
		{
			Actor giver = _giver ?? _host;
			Quests.ToolsForTest = 1;
			Quests.ForceKindForTest = 3;
			try
			{
				if (giver.getWeapon() != null)
				{
					giver.equipment.getSlot(EquipmentType.Weapon).takeAwayItem();
				}
			}
			catch
			{
			}
			Quest quest = Quests.Offer(giver, _host);
			if (quest == null)
			{
				quest = new Quest
				{
					Kind = QuestKind.Errand,
					Giver = giver.data.id,
					GiverName = "test",
					Money = 5,
					Bench = true
				};
				Note("(no craft task: a made-up one)");
			}
			Check("a task that pays in workbench tools", quest.Bench, quest.Kind.ToString());
			string text = Quests.Write(quest);
			giver.data.set("fp_q_wbtest", text);
			Quest quest2 = Quests.Read(giver.data, "fp_q_wbtest");
			string[] array = text.Split(';');
			giver.data.set("fp_q_wbtest", string.Join(";", array, 0, Math.Min(22, array.Length)));
			Quest quest3 = Quests.Read(giver.data, "fp_q_wbtest");
			giver.data.removeString("fp_q_wbtest");
			Check("the reward is kept when saved, and older saves read as without it", quest2 != null && quest2.Bench && quest3 != null && !quest3.Bench);
			int num = Workbench.Level(_host);
			Check("the task taken on", Quests.Accept(quest, _host, Mod.Toast), Quests.Log.Count + " tasks");
			Quests.Reward(quest, _host, Mod.Toast);
			Check("done: the workbench goes up a level", Workbench.Tools(_host) == 1 && Workbench.Level(_host) == num + 1, $"{num} -> {Workbench.Level(_host)}, toast \"{Mod.ToastText}\"");
			yield return Make("sword_copper");
			Check("and copper can be worked at home now", Weapon() == "sword_copper", Weapon());
			Quests.ToolsForTest = 0;
			Quest quest4 = Quests.Offer(giver, _host);
			Check("a task can pay without tools", quest4 == null || !quest4.Bench);
			Quests.ToolsForTest = -1;
			Quests.ForceKindForTest = -1;
		}

		private IEnumerator Grander()
		{
			Building grand = PlaceHouse("house_human_4");
			if (Check("a grander house", grand != null))
			{
				Mod.ToastText = null;
				int before = Workbench.Level(_host);
				_host.setHomeBuilding(grand);
				float t = 0f;
				while (t < 5f && (Mod.ToastText == null || !Mod.ToastText.StartsWith("Your house has grown", StringComparison.Ordinal)))
				{
					t += 0.25f;
					yield return CivilTest.Wait(0.25f);
				}
				Check("moving in raises the workbench, and says so", Workbench.HouseLevel(_host) == 3 && Workbench.Level(_host) == 4 && Mod.ToastText != null && Mod.ToastText.StartsWith("Your house has grown", StringComparison.Ordinal), $"{before} -> {Workbench.Level(_host)}, toast \"{Mod.ToastText}\"");
				_house = grand;
				Move(HouseSpot());
				yield return CivilTest.Wait(0.5f);
				yield return Make("sword_mythril");
				Check("mythril at a level 4 workbench", Weapon() == "sword_mythril", Weapon());
				Crafting.Recipe recipe = Recipe("sword_adamantine");
				Check("adamantine still needs level 5", recipe == null || Crafting.Locked(_host, recipe) == "Needs a level 5 workbench. Yours is level 4", (recipe != null) ? Crafting.Locked(_host, recipe) : null);
			}
		}

		private IEnumerator Enchantments()
		{
			EquipmentAsset pItemAsset = AssetManager.items.get("sword_iron");
			Item item = World.world.items.generateItem(pItemAsset, _host.kingdom, "test", 1, _host, 0, pByPlayer: true);
			bool flag = false;
			try
			{
				item.addMod("flame");
				item.addMod("poison");
				flag = item.hasMod("flame") && item.hasMod("poison");
			}
			catch (Exception ex)
			{
				Note("mods: " + ex.Message);
			}
			Crafting.Ordinary(item);
			Check("made gear keeps no earned enchantment", flag && !item.hasMod("flame") && !item.hasMod("poison"));
			yield break;
		}

		private IEnumerator BagAndChests()
		{
			try
			{
				_host.data.removeString("fp_gear");
			}
			catch
			{
			}
			string worn = Weapon();
			Crafting.Recipe sword = Recipe("sword_wood");
			Crafting.Recipe axe = Recipe("axe_wood");
			if (!Check("wooden recipes", sword != null && axe != null))
			{
				yield break;
			}
			Give(sword);
			Craft.Open = true;
			Check("a click starts it", Craft.Start(_host, sword, Mod.Toast) && Craft.Working == sword);
			yield return CivilTest.Wait(0.8f);
			float progress = Craft.Progress;
			Check("it is seen being made", progress > 0.2f && progress < 0.95f && Craft.Queued(sword) == 1, progress.ToString("F2"));
			yield return Shot("wb_progress.png");
			yield return Done();
			Check("then it goes into your bag, not your hand", Gear.Of(_host).Exists((Gear.Piece x) => x.Id == "sword_wood") && Weapon() == worn && Craft.Working == null, $"bag {Gear.Count(_host)}, holding {Weapon()}");
			Give(axe);
			Give(axe);
			Craft.Start(_host, axe, Mod.Toast);
			Craft.Start(_host, axe, Mod.Toast);
			Check("a second waits its turn", Craft.Queued(axe) == 2);
			for (float t = 0f; t < 10f; t += 0.25f)
			{
				if (Gear.Of(_host).FindAll((Gear.Piece x) => x.Id == "axe_wood").Count >= 2)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Check("and both are made", Gear.Of(_host).FindAll((Gear.Piece x) => x.Id == "axe_wood").Count == 2, Gear.Count(_host) + " in the bag");
			int index = Gear.Of(_host).FindIndex((Gear.Piece x) => x.Id == "axe_wood");
			Check("putting on an axe from the bag", Gear.PutOn(_host, index, Mod.Toast) && Weapon() == "axe_wood" && Gear.Of(_host).Exists((Gear.Piece x) => x.Id == worn), $"holding {Weapon()}, bag has {worn}: {Gear.Of(_host).Exists((Gear.Piece x) => x.Id == worn)}");
			Gear.Add(_host, new Gear.Piece
			{
				Id = "sword_iron",
				Mods = new string[1] { "power1" },
				Name = "Test Blade",
				Kills = 3
			});
			index = Gear.Of(_host).FindIndex((Gear.Piece x) => x.Name == "Test Blade");
			Gear.PutOn(_host, index, Mod.Toast);
			Item item = null;
			try
			{
				item = _host.equipment.getSlot(EquipmentType.Weapon).getItem();
			}
			catch
			{
			}
			bool ok = item != null && item.asset.id == "sword_iron" && item.hasMod("power1") && item.data.name == "Test Blade" && item.data.kills == 3;
			Check("a piece from the bag keeps its enchantment, name and kills", ok, (item != null) ? string.Format("{0} power1 {1} '{2}' {3}", item.asset.id, item.hasMod("power1"), item.data.name, item.data.kills) : "nothing held");
			Check("taking it off puts it back in the bag as it was", Gear.TakeOff(_host, EquipmentType.Weapon, Mod.Toast) && Weapon() == "none" && Gear.Of(_host).Exists((Gear.Piece x) => x.Name == "Test Blade" && Array.IndexOf(x.Mods, "power1") >= 0 && x.Kills == 3));
			string pResult = null;
			try
			{
				_host.data.get("fp_gear", out pResult, null);
			}
			catch
			{
			}
			Check("the bag's text reads back the same", pResult != null && Gear.Parse(pResult).Count == Gear.Count(_host), pResult);
			TestChest chest = new TestChest();
			chest.Held["stone"] = 10;
			Func<Actor, IEnumerable<CraftStores.Store>> near = (Actor you) => new CraftStores.Store[1] { chest };
			CraftStores.Chests.Add(near);
			Crafting.Recipe recipe = Recipe("sword_stone");
			try
			{
				int resource = _host.inventory.getResource("stone");
				_host.takeFromInventory("stone", resource);
			}
			catch
			{
			}
			bool flag = recipe != null && Crafting.CanMake(_host, recipe);
			bool flag2 = flag && Craft.Start(_host, recipe, Mod.Toast);
			Check("a chest near you pays when your bag can't", flag && flag2 && chest.Count("stone") == 10 - recipe.Amount1, string.Format("chest {0}, cost {1}", chest.Count("stone"), recipe?.Amount1));
			yield return Done();
			CraftStores.Chests.Remove(near);
			Check("and what is made still goes into your bag", Gear.Of(_host).Exists((Gear.Piece x) => x.Id == "sword_stone"));
			while (Gear.HasRoom(_host))
			{
				Gear.Add(_host, new Gear.Piece
				{
					Id = "sword_wood"
				});
			}
			Give(sword);
			Check("a full bag takes nothing more", !Craft.Start(_host, sword, Mod.Toast) && Mod.ToastText == "No room in your bag for it", Mod.ToastText);
			yield return Shot("wb_bag.png");
			try
			{
				_host.data.removeString("fp_gear");
			}
			catch
			{
			}
			Give(sword);
			Craft.ShowAll = false;
			yield return Shot("wb_grid.png");
			Craft.ShowAll = true;
			yield return Shot("wb_grid_all.png");
			Craft.ShowAll = false;
			Craft.Open = false;
		}

		private IEnumerator Bench()
		{
			Mod.ToastText = null;
			Mod.EnterHouseForTest(_house);
			Check("coming in, you're told of your workbench", Mod.ToastText != null && Mod.ToastText.StartsWith("Home. Your workbench is level", StringComparison.Ordinal), Mod.ToastText);
			yield return CivilTest.Wait(1f);
			HouseInterior interior = Mod.Interior;
			if (Check("inside your house, your workbench stands there", interior.Inside && interior.HasBench, $"inside {interior.Inside}, bench {interior.HasBench}"))
			{
				Vector2 vector = new Vector2(interior.BenchX, interior.BenchY);
				Vector2 vector2 = interior.Eye;
				Vector2 vector3 = vector - vector2;
				if (vector3.magnitude > 2.2f)
				{
					vector2 = vector - vector3.normalized * 2.2f;
				}
				interior.Eye = vector2;
				Mod.LookForTest(Mathf.Atan2(vector.y - vector2.y, vector.x - vector2.x), -12f);
				yield return CivilTest.Wait(1f);
				Mod.SaveFrame("wb_bench.png");
				Check("the workbench is what you can craft at", Workbench.AtHome(_host) && Workbench.Usable(_host) == 4);
				yield return CivilTest.Wait(0.5f);
				Mod.LeaveRoomForTest();
			}
		}

		private IEnumerator OddCases()
		{
			Crafting.Recipe r = new Crafting.Recipe
			{
				Asset = AssetManager.items.get("sword_wood"),
				Name = "test",
				Cost = "2 + 3 wood",
				Res1 = "wood",
				Amount1 = 2,
				Res2 = "wood",
				Amount2 = 3
			};
			try
			{
				_host.takeFromInventory("wood", _host.inventory.getResource("wood"));
				_host.addToInventory("wood", 4);
			}
			catch
			{
			}
			bool flag = Crafting.CanAfford(_host, r);
			try
			{
				_host.addToInventory("wood", 1);
			}
			catch
			{
			}
			bool flag2 = Crafting.CanAfford(_host, r);
			Check("the same thing twice is counted twice", !flag && flag2, $"4 wood {flag}, 5 wood {flag2}");
			Craft.Recipes.Clear();
			Craft.EnsureRecipes();
			Check("the recipes load when the window opens some other way", Craft.Recipes.Count > 20, Craft.Recipes.Count.ToString());
			yield break;
		}

		private IEnumerator NewBody()
		{
			Actor other = _giver;
			if (other == null || !other.isAlive())
			{
				Note("(no one else to become)");
				yield break;
			}
			try
			{
				_host.data.removeString("fp_gear");
				other.data.removeString("fp_gear");
			}
			catch
			{
			}
			Crafting.Recipe sword = Recipe("sword_wood");
			for (int i = 0; i < 4; i++)
			{
				Give(sword);
			}
			int before = 0;
			int paid = 0;
			try
			{
				before = _host.inventory.getResource("wood");
			}
			catch
			{
			}
			int num = 0;
			for (int j = 0; j < 4; j++)
			{
				if (Craft.Start(_host, sword, Mod.Toast))
				{
					num++;
				}
			}
			try
			{
				paid = before - _host.inventory.getResource("wood");
			}
			catch
			{
			}
			Check("four swords paid for and waiting", num == 4 && paid == 4 * sword.Amount1, $"{num} started, {paid} wood paid");
			yield return Possess(other);
			yield return CivilTest.Wait(0.5f);
			int count = Gear.Of(_host).FindAll((Gear.Piece x) => x.Id == "sword_wood").Count;
			int num2 = 0;
			try
			{
				num2 = _host.inventory.getResource("wood") - (before - paid);
			}
			catch
			{
			}
			Check("leaving that body stops the work and gives back what wasn't made", InControl(other) && Craft.Queue.Count == 0 && count + num2 / Math.Max(1, sword.Amount1) == 4, $"made {count}, wood back {num2}, queue {Craft.Queue.Count}");
			Check("nothing turns up in the new body's bag", Gear.Count(other) == 0, Gear.Count(other) + " pieces");
		}

		private Vector2 HouseSpot()
		{
			WorldTile current_tile = _house.current_tile;
			return new Vector2(current_tile.posV3.x, current_tile.posV3.y - 2f);
		}

		private Building PlaceHouse(string id)
		{
			WorldTile tile = _city.getTile();
			for (int i = 3; i < 20; i++)
			{
				for (int j = 0; j < 16; j++)
				{
					float f = (float)j * MathF.PI / 8f;
					WorldTile tile2 = World.world.GetTile(tile.x + (int)(Mathf.Cos(f) * (float)i), tile.y + (int)(Mathf.Sin(f) * (float)i));
					if (tile2 == null || tile2.main_type == null || tile2.main_type.liquid || tile2.building != null)
					{
						continue;
					}
					try
					{
						if (tile2.zone == null || tile2.zone.city != _city)
						{
							continue;
						}
						goto IL_00a8;
					}
					catch
					{
					}
					continue;
					IL_00a8:
					try
					{
						Building building = World.world.buildings.addBuilding(id, tile2, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
						if (building != null)
						{
							Note($"{id} at {tile2.x},{tile2.y}");
							return building;
						}
					}
					catch
					{
					}
				}
			}
			return null;
		}

		private IEnumerator Shot(string name)
		{
			yield return CivilTest.Wait(0.4f);
			H.CaptureHudForTest(Path.Combine(Log.Folder, name));
			for (int i = 0; i < 20; i++)
			{
				if (!H.CapturePending)
				{
					break;
				}
				yield return null;
			}
			yield return null;
		}
	}
}
