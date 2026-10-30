using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class InventoryTest : CivilTest
	{
		private Actor _host;

		protected override string Prefix => "INV";

		private Hud H => Mod.HudForTest;

		private CharacterView View => Mod.YouView;

		private string InHand => Hotbar.Weapon(_host)?.asset?.id;

		protected override IEnumerator Body()
		{
			Actor actor = Village("human", 8, 0);
			if (!Check("a village", actor != null))
			{
				yield break;
			}
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != actor && item.isAdult())
				{
					_host = item;
					break;
				}
			}
			if (_host == null)
			{
				_host = actor;
			}
			CivilTest.SetSex(_host, female: false);
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
				Supplies.Clear(_host);
				Mod.Settings.HeadBob = 0f;
				yield return Part("keys", Keys());
				yield return Part("figure", Figure());
				yield return Part("gear", Gear());
				yield return Part("quick", Quick());
				yield return Part("bag", Bag());
				yield return Part("stacks", Stacks());
				yield return Part("hotbar", HotbarRow());
				yield return Part("others", Others());
				Mod.Craft.Open = false;
			}
		}

		private IEnumerator Press()
		{
			WorldBoxMod.PressForTest = Time.frameCount + 1;
			yield return null;
			yield return null;
			yield return null;
		}

		private IEnumerator Keys()
		{
			Mod.Craft.Open = false;
			yield return Press();
			Check("Tab opens the inventory", Mod.Craft.Open, $"quick {Mod.QuickInventory}, dead {Mod.DeathTimer:F1}, active {Mod.IsFirstPerson}");
			yield return CivilTest.Wait(0.3f);
			yield return Press();
			Check("and Tab closes it", !Mod.Craft.Open, $"quick {Mod.QuickInventory}");
			bool flag = false;
			bool flag2 = false;
			bool flag3 = false;
			Help.Part[] keys = Help.Keys;
			for (int i = 0; i < keys.Length; i++)
			{
				string[][] rows = keys[i].Rows;
				foreach (string[] array in rows)
				{
					flag |= array[0] == "Tab" && array[1].StartsWith("You", StringComparison.Ordinal) && array[1].Contains("people");
					flag2 |= array[0] == "P" && array[1].IndexOf("bag", StringComparison.OrdinalIgnoreCase) >= 0;
					flag3 |= array[0] == "I";
				}
			}
			Check("the help's keys say Tab for your bag and people, no P or I", flag && !flag2 && !flag3);
			bool flag4 = false;
			TouchControls.Tile[] tiles = TouchControls.Tiles;
			foreach (TouchControls.Tile tile in tiles)
			{
				flag4 |= tile.Name == "Inventory" && tile.Key == KeyCode.Tab;
			}
			Check("the phone's Inventory tile presses Tab", flag4);
		}

		private IEnumerator Figure()
		{
			Give(AssetManager.items.get("sword_iron"));
			Give(H.ShelfItemForTest(1, EquipmentType.Helmet));
			Give(H.ShelfItemForTest(1, EquipmentType.Armor));
			Give(H.ShelfItemForTest(1, EquipmentType.Boots));
			try
			{
				_host.addToInventory("wood", 12);
				_host.addToInventory("stone", 6);
				_host.addToInventory("berries", 3);
				_host.addToInventory("gold", 5);
			}
			catch (Exception ex)
			{
				Note("bag: " + ex.Message);
			}
			View.Turn = 0f;
			View.ResetTiming();
			Mod.Craft.Open = true;
			int drawn = View.Drawn;
			int frames = Mod.FramesShown;
			yield return CivilTest.Wait(1.5f);
			Check("the figure is drawn while the inventory is open", View.Drawn > drawn && View.Picture != null, "drawn " + (View.Drawn - drawn) + ", " + View.LastMs.ToString("F2") + " ms");
			Check("the world goes on being drawn behind it", Mod.FramesShown > frames + 5, Mod.FramesShown - frames + " frames");
			Check("drawn quickly", View.BestMs < 12f, $"best {View.BestMs:F2} ms, last {View.LastMs:F2} ms: {View.Timing}");
			Check("with your sword and armour on it", View.Parts >= 3, View.Parts + " parts");
			Framed("front");
			yield return Shot("inv_window_human.png");
			Save("inv_figure_front.png");
			View.Turn = MathF.PI / 2f;
			yield return CivilTest.Wait(0.5f);
			Save("inv_figure_side.png");
			View.Turn = MathF.PI;
			yield return CivilTest.Wait(0.5f);
			Framed("back");
			Save("inv_figure_back.png");
			View.Turn = 0f;
			int seen = View.Drawn;
			Mod.Craft.Open = false;
			yield return CivilTest.Wait(0.5f);
			Check("and not drawn once it is closed", View.Drawn == seen);
		}

		private IEnumerator Gear()
		{
			Mod.Craft.Open = true;
			yield return CivilTest.Wait(0.5f);
			string with = View.PartNames;
			EquipmentAsset a = AssetManager.items.get("sword_iron");
			H.ToggleItem(_host, a);
			yield return CivilTest.Wait(0.6f);
			Check("the sword leaves its hand when you take it off", View.PartNames != with && _host.getWeapon() == null, with + " -> " + View.PartNames);
			Save("inv_figure_nosword.png");
			Mod.Craft.Open = false;
			yield return CivilTest.Wait(0.3f);
		}

		private IEnumerator Quick()
		{
			Mod.Craft.Open = false;
			yield return null;
			WorldBoxMod.HoldForTest = true;
			WorldBoxMod.PressForTest = Time.frameCount + 1;
			yield return CivilTest.Wait(0.5f);
			for (int i = 0; i < 4; i++)
			{
				yield return null;
			}
			Check("Tab held: the inventory shows, see-through", Mod.Craft.Open && Mod.QuickInventory);
			Check("...and the movement keys stay yours", PossessionHooks.KeysTaken != null && !PossessionHooks.KeysTaken());
			yield return Shot("inv_quick.png");
			WorldBoxMod.HoldForTest = false;
			yield return null;
			yield return null;
			Check("let go, it goes away", !Mod.Craft.Open && !Mod.QuickInventory);
			yield return Press();
			Check("a tap leaves the whole window open", Mod.Craft.Open && !Mod.QuickInventory);
			Check("...standing still", PossessionHooks.KeysTaken != null && PossessionHooks.KeysTaken());
			yield return CivilTest.Wait(0.3f);
			yield return Press();
			Check("Tab again closes it", !Mod.Craft.Open);
		}

		private int Amount(string id)
		{
			try
			{
				Dictionary<string, ResourceContainer> dictionary = _host.inventory?.getResources();
				ResourceContainer value;
				return (dictionary != null && dictionary.TryGetValue(id, out value)) ? value.amount : 0;
			}
			catch
			{
				return 0;
			}
		}

		private IEnumerator Bag()
		{
			try
			{
				List<string> list = new List<string>();
				foreach (KeyValuePair<string, ResourceContainer> resource in _host.inventory.getResources())
				{
					if (resource.Value.amount > 0)
					{
						list.Add(resource.Key);
					}
				}
				foreach (string item in list)
				{
					_host.takeFromInventory(item, Amount(item));
				}
				_host.addToInventory("wood", 12);
				_host.addToInventory("berries", 3);
			}
			catch (Exception ex)
			{
				Note("bag: " + ex.Message);
			}
			Mod.Craft.Open = true;
			yield return CivilTest.Wait(0.5f);
			List<string> list2 = BagOrder.Of(_host);
			int num = list2.IndexOf("r:wood");
			int num2 = list2.IndexOf("r:berries");
			Check("the bag's slots have an order", num >= 0 && num2 >= 0, string.Join(",", list2.ToArray()));
			BagOrder.Swap(_host, num, num2);
			string pResult = null;
			try
			{
				_host.data.get("fp_bagorder", out pResult, null);
			}
			catch
			{
			}
			Check("moved, and kept with the body", BagOrder.Of(_host).IndexOf("r:wood") == num2 && pResult != null && pResult.Contains("r:wood"), pResult);
			BagOrder.Swap(_host, BagOrder.Of(_host).IndexOf("r:wood"), 7);
			yield return CivilTest.Wait(0.3f);
			Check("moved to an empty slot, it stays there", BagOrder.Of(_host).IndexOf("r:wood") == 7, string.Join(",", BagOrder.Of(_host).ToArray()));
			yield return Shot("inv_bag_order.png");
			try
			{
				_host.setNutrition(1);
			}
			catch
			{
			}
			int num3 = Amount("berries");
			bool flag = Eating.Edible(_host, "berries") && Eating.EatFromBag(_host, "berries", Mod.Toast);
			Check("berries clicked in the bag are eaten", flag && Amount("berries") == num3 - 1, num3 + " -> " + Amount("berries"));
			Mod.Craft.Open = false;
			Mod.LookForTest(0f, -45f);
			yield return CivilTest.Wait(0.3f);
			int woodHad = Amount("wood");
			bool flag2 = Mod.DropResource("wood", woodHad);
			Check("dropped wood leaves the bag", flag2 && Amount("wood") == 0, woodHad + " -> " + Amount("wood"));
			Bundles.Bundle sack = ((Bundles.List.Count > 0) ? Bundles.List[Bundles.List.Count - 1] : null);
			Check("...and lies at your feet", sack != null && sack.Resource == "wood" && sack.Count == woodHad && (sack.At - _host.current_position).magnitude < 1f);
			Check("E offers to pick it up", sack != null && Mod.BundleOfferedForTest == sack);
			FirstPerson.Gear.Add(_host, new Gear.Piece
			{
				Id = "sword_iron"
			});
			int pieces = FirstPerson.Gear.Count(_host);
			bool flag3 = Mod.DropPiece(pieces - 1);
			Check("a spare sword dropped", flag3 && FirstPerson.Gear.Count(_host) == pieces - 1 && Bundles.List.Exists((Bundles.Bundle b) => b.Piece != null && b.Piece.Id == "sword_iron"));
			yield return CivilTest.Wait(0.5f);
			yield return Shot("inv_dropped.png");
			Check("the wood picked up again", sack != null && Mod.PickUpForTest(sack) && Amount("wood") == woodHad && !Bundles.List.Contains(sack));
			Bundles.Bundle bundle = Bundles.List.Find((Bundles.Bundle b) => b.Piece != null && b.Piece.Id == "sword_iron");
			Check("the sword too", bundle != null && Mod.PickUpForTest(bundle) && FirstPerson.Gear.Count(_host) == pieces);
			Mod.LookForTest(0f, 0f);
		}

		private IEnumerator Stacks()
		{
			try
			{
				List<string> list = new List<string>();
				foreach (KeyValuePair<string, ResourceContainer> resource in _host.inventory.getResources())
				{
					if (resource.Value.amount > 0)
					{
						list.Add(resource.Key);
					}
				}
				foreach (string item in list)
				{
					_host.takeFromInventory(item, Amount(item));
				}
				_host.addToInventory("wood", FirstPerson.Bag.Stack("wood") * 2);
			}
			catch (Exception ex)
			{
				Note("bag: " + ex.Message);
			}
			Mod.Craft.Open = true;
			yield return CivilTest.Wait(0.5f);
			int st = FirstPerson.Bag.Stack("wood");
			int first = BagOrder.Of(_host).IndexOf("r:wood");
			int second = BagOrder.Of(_host).IndexOf("r:wood#2");
			Check("two stacks of wood, each in a slot of its own", first >= 0 && second >= 0 && first != second, Slots());
			if (first < 0 || second < 0)
			{
				Mod.Craft.Open = false;
				yield break;
			}
			int last = H.BagCellCountForTest - 1;
			BagOrder.Swap(_host, first, 0);
			BagOrder.Swap(_host, BagOrder.Of(_host).IndexOf("r:wood#2"), last);
			Hotbar.Moved();
			yield return CivilTest.Wait(0.3f);
			first = BagOrder.Of(_host).IndexOf("r:wood");
			second = BagOrder.Of(_host).IndexOf("r:wood#2");
			Check("...moved where they were put", first == 0 && second == last, Slots());
			Check("each shows a full stack", H.SlotAmountForTest(_host, first) == st && H.SlotAmountForTest(_host, second) == st, H.SlotAmountForTest(_host, first) + " / " + H.SlotAmountForTest(_host, second));
			int num = -1;
			int num2 = Hotbar.Size(_host) - 1;
			while (num2 > 0 && num < 0)
			{
				if (num2 >= BagOrder.Of(_host).Count || BagOrder.Of(_host)[num2].Length == 0)
				{
					num = num2;
				}
				num2--;
			}
			Check("an empty slot on the hotbar to point at", num > 0, Slots());
			string[] tips = new string[3];
			int[] cells = new int[3] { num, first, second };
			for (int t = 0; t < 3; t++)
			{
				Vector2? at = (Hud.BagMouseForTest = H.BagCellForTest(cells[t]));
				yield return null;
				yield return null;
				tips[t] = (at.HasValue ? H.BagTipForTest : "(not drawn)");
			}
			Hud.BagMouseForTest = H.BagCellForTest(second);
			yield return Shot("inv_stacks.png");
			Hud.BagMouseForTest = null;
			Check("an empty slot under the mouse: no tip", tips[0] == null, tips[0] ?? "none");
			Check("a stack's tip says its own count", tips[1] != null && tips[1].StartsWith(st + " ", StringComparison.Ordinal) && tips[2] != null && tips[2].StartsWith(st + " ", StringComparison.Ordinal), (tips[1] ?? "none") + " | " + (tips[2] ?? "none"));
			try
			{
				_host.addToInventory("wood", 5);
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.2f);
			int num3 = BagOrder.Of(_host).IndexOf("r:wood#3");
			Check("5 more: a third stack of 5, the others as they were", num3 >= 0 && H.SlotAmountForTest(_host, num3) == 5 && BagOrder.Of(_host).IndexOf("r:wood") == first && BagOrder.Of(_host).IndexOf("r:wood#2") == second && H.SlotAmountForTest(_host, first) == st && H.SlotAmountForTest(_host, second) == st, Slots());
			try
			{
				_host.takeFromInventory("wood", 5);
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.2f);
			Check("taken away again (built with, sold): the third goes, the others stay", BagOrder.Of(_host).IndexOf("r:wood#3") < 0 && H.SlotAmountForTest(_host, first) == st && H.SlotAmountForTest(_host, second) == st, Slots());
			Mod.LookForTest(0f, -45f);
			yield return CivilTest.Wait(0.3f);
			H.DropSlotForTest(_host, first, one: false);
			yield return CivilTest.Wait(0.2f);
			List<string> list2 = BagOrder.Of(_host);
			bool flag = first >= list2.Count || list2[first].Length == 0;
			bool flag2 = second < list2.Count && BagOrder.ResourceOf(list2[second]) == "wood" && H.SlotAmountForTest(_host, second) == st;
			Check("one stack dropped: its slot empties, the other keeps its stack in its slot", Amount("wood") == st && flag && flag2, Amount("wood") + " wood; " + Slots());
			Bundles.Bundle bundle = ((Bundles.List.Count > 0) ? Bundles.List[Bundles.List.Count - 1] : null);
			Check("...a stack of them lies at your feet", bundle != null && bundle.Resource == "wood" && bundle.Count == st, (bundle != null) ? (bundle.Count + " " + bundle.Resource) : "none");
			if (bundle != null)
			{
				Mod.PickUpForTest(bundle);
			}
			Mod.LookForTest(0f, 0f);
			Mod.Craft.Open = false;
			yield return null;
		}

		private string Slots()
		{
			return string.Join(",", BagOrder.Of(_host).ToArray());
		}

		private int SlotOf(string id)
		{
			for (int i = 0; i < BagOrder.Of(_host).Count; i++)
			{
				if (i != Hotbar.Chosen(_host) && PieceIn(i) == id)
				{
					return i;
				}
			}
			return -1;
		}

		private int Swords()
		{
			return FirstPerson.Gear.Of(_host).FindAll((Gear.Piece p) => p.Id.StartsWith("sword_", StringComparison.Ordinal)).Count + ((InHand != null && InHand.StartsWith("sword_", StringComparison.Ordinal)) ? 1 : 0);
		}

		private string PieceIn(int slot)
		{
			List<string> list = BagOrder.Of(_host);
			int num = ((slot < list.Count) ? BagOrder.PieceOf(list[slot]) : (-1));
			List<Gear.Piece> list2 = FirstPerson.Gear.Of(_host);
			if (num < 0 || num >= list2.Count)
			{
				return null;
			}
			return list2[num].Id;
		}

		private IEnumerator PressSlot(int slot)
		{
			WorldBoxMod.HotbarKeyForTest = slot;
			WorldBoxMod.HotbarKeyFrameForTest = Time.frameCount + 1;
			yield return null;
			yield return null;
			yield return null;
		}

		private IEnumerator HotbarRow()
		{
			Mod.Craft.Open = false;
			Mod.LookForTest(0f, 25f);
			Give(AssetManager.items.get("sword_iron"));
			yield return CivilTest.Wait(0.3f);
			int size = Hotbar.Size(_host);
			int chosen = Hotbar.Chosen(_host);
			int hand = BagOrder.Of(_host).IndexOf("h");
			Check("eight slots on the Frame pack's hotbar", size == 8, size.ToString());
			Check("the sword in your hand has the chosen slot", InHand == "sword_iron" && hand == chosen && hand < size, $"slot {hand}, chosen {chosen}: {Slots()}");
			int num = 0;
			foreach (KeyValuePair<string, int> item in FirstPerson.Bag.Contents(_host))
			{
				num += FirstPerson.Bag.SlotsFor(item.Key, item.Value);
			}
			int num2 = FirstPerson.Bag.Used(_host);
			Check("...and counts as a slot of the bag", num2 == FirstPerson.Gear.Count(_host) + num + 1, $"{num2} used, {FirstPerson.Gear.Count(_host)} pieces, {num} stacks");
			int swords = Swords();
			FirstPerson.Gear.Add(_host, new Gear.Piece
			{
				Id = "sword_bronze"
			});
			try
			{
				_host.addToInventory("berries", 2);
			}
			catch
			{
			}
			yield return null;
			yield return null;
			int bronze = (chosen + 2) % size;
			int food = (chosen + 3) % size;
			BagOrder.Swap(_host, BagOrder.Of(_host).IndexOf(BagOrder.PieceKey(FirstPerson.Gear.Count(_host) - 1)), bronze);
			BagOrder.Swap(_host, BagOrder.Of(_host).IndexOf(BagOrder.ResourceKey("berries")), food);
			Note("slots: " + Slots());
			yield return PressSlot(bronze);
			yield return CivilTest.Wait(0.3f);
			Check("its number key takes up the bronze sword", Hotbar.Chosen(_host) == bronze && InHand == "sword_bronze", $"chosen {Hotbar.Chosen(_host)}, holding {InHand}");
			Check("...the iron one back in its own slot", PieceIn(hand) == "sword_iron" && BagOrder.Of(_host).IndexOf("h") == bronze, Slots());
			Check("...no sword lost or doubled", Swords() == swords + 1, $"{swords} + the bronze one -> {Swords()}");
			yield return Shot("inv_hotbar_hud.png");
			Hotbar.Step(_host, 1);
			Hotbar.Sync(_host, base.Note);
			Check("a turn of the wheel: the hand waits", Hotbar.Chosen(_host) == food && InHand == "sword_bronze", $"chosen {Hotbar.Chosen(_host)}, holding {InHand}");
			Hotbar.Choose(_host, bronze, wheel: false);
			yield return null;
			yield return null;
			WorldBoxMod.HotbarWheelForTest = -1f;
			yield return null;
			yield return null;
			Check("the wheel steps to the next slot", Hotbar.Chosen(_host) == food, $"chosen {Hotbar.Chosen(_host)}");
			yield return CivilTest.Wait(0.4f);
			Check("...settled: the hands bare, the sword back in its slot", InHand == null && PieceIn(bronze) == "sword_bronze", "holding " + InHand + ": " + Slots());
			Check("berries in hand: a click eats, not strikes", Mod.HotbarFoodForTest == "berries" && PossessionHooks.BlockAttack, $"food {Mod.HotbarFoodForTest}, block {PossessionHooks.BlockAttack}");
			try
			{
				_host.setNutrition(1);
			}
			catch
			{
			}
			int had = Amount("berries");
			WorldBoxMod.HotbarClickForTest = Time.frameCount + 1;
			yield return null;
			yield return null;
			Check("...and the click eats one", Amount("berries") == had - 1, had + " -> " + Amount("berries"));
			try
			{
				_host.setNutrition(_host.getMaxNutrition());
			}
			catch
			{
			}
			yield return PressSlot(bronze);
			yield return CivilTest.Wait(0.3f);
			bool stowed = InHand == "sword_bronze" && Hotbar.Stow(_host, base.Note);
			yield return null;
			Check("put away, the hands are bare and the sword waits in a free slot", stowed && InHand == null && SlotOf("sword_bronze") >= 0 && Hotbar.Chosen(_host) == bronze, "holding " + InHand + ": " + Slots());
			yield return PressSlot(hand);
			yield return CivilTest.Wait(0.3f);
			Check("the iron sword taken up again", InHand == "sword_iron", InHand);
			Mod.Craft.Open = true;
			yield return CivilTest.Wait(0.5f);
			yield return Shot("inv_hotbar_window.png");
			Mod.Craft.Open = false;
			yield return CivilTest.Wait(4.2f);
			Check("the row fades when nothing changes", H.HotbarShownForTest < 0.01f, H.HotbarShownForTest.ToString("F2"));
			Mod.LookForTest(0f, 0f);
		}

		private IEnumerator Others()
		{
			Vector2 at = _host.current_position;
			Actor elf = CivilTest.SpawnNear("elf", at, 3f);
			if (elf != null)
			{
				CivilTest.SetSex(elf, female: true);
				yield return Possess(elf);
				if (Check("first person in an elf woman", InControl(elf)))
				{
					Mod.Craft.Open = true;
					yield return CivilTest.Wait(1f);
					Check("she is drawn", View.Picture != null && View.Parts >= 1, View.Parts + " parts");
					Framed("elf");
					Save("inv_figure_elf.png");
					Mod.Craft.Open = false;
				}
			}
			else
			{
				Check("an elf to be", ok: false);
			}
			Actor wolf = CivilTest.SpawnNear("wolf", at, 3f);
			if (wolf == null)
			{
				Check("a wolf to be", ok: false);
				yield break;
			}
			yield return Possess(wolf);
			if (Check("first person in a wolf", InControl(wolf)))
			{
				Mod.Craft.Open = true;
				yield return CivilTest.Wait(1f);
				bool flag = true;
				try
				{
					flag = wolf.equipment != null && wolf.understandsHowToUseItems();
				}
				catch
				{
				}
				Check("a wolf is drawn as a wolf, with no gear wells", View.Picture != null && !flag, View.Parts + " parts, wears " + flag);
				Framed("wolf");
				yield return Shot("inv_window_wolf.png");
				Save("inv_figure_wolf.png");
				Mod.Craft.Open = false;
			}
		}

		private void Give(EquipmentAsset a)
		{
			if (a == null)
			{
				Note("(nothing to give)");
				return;
			}
			Item item = null;
			try
			{
				item = _host.equipment.getSlot(a.equipment_type).getItem();
			}
			catch
			{
			}
			if (item?.asset != a)
			{
				H.ToggleItem(_host, a);
			}
		}

		private void Framed(string tag)
		{
			Texture2D picture = View.Picture;
			if (picture == null)
			{
				Check("the " + tag + " picture", ok: false);
				return;
			}
			Color32[] pixels = picture.GetPixels32();
			int width = picture.width;
			int height = picture.height;
			int num = width;
			int num2 = -1;
			int num3 = height;
			int num4 = -1;
			int num5 = 0;
			int num6 = 0;
			int num7 = 0;
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < width; j++)
				{
					switch (pixels[i * width + j].a)
					{
					case byte.MaxValue:
						num5++;
						num = Math.Min(num, j);
						num2 = Math.Max(num2, j);
						num3 = Math.Min(num3, i);
						num4 = Math.Max(num4, i);
						break;
					case 0:
						num6++;
						break;
					default:
						num7++;
						break;
					}
				}
			}
			float num8 = (float)(num4 - num3 + 1) / (float)height;
			float num9 = (float)(num2 - num + 1) / (float)width;
			bool flag = num > 0 && num2 < width - 1 && num3 > 0 && num4 < height - 1;
			Check("the " + tag + " figure framed whole", flag && Mathf.Max(num8, num9) > 0.42f && num8 < 0.98f && num6 > width * height / 4 && num7 > 0, $"{width}x{height}, rows {num3}-{num4} ({num8:P0}), cols {num}-{num2} ({num9:P0}), solid {num5}, clear {num6}, shadow {num7}");
		}

		private void Save(string name)
		{
			Texture2D picture = View.Picture;
			if (picture == null)
			{
				return;
			}
			Color32[] pixels = picture.GetPixels32();
			int width = picture.width;
			int height = picture.height;
			Texture2D texture2D = new Texture2D(width * 3, height * 3, TextureFormat.RGB24, mipChain: false);
			Color32[] array = new Color32[width * 3 * height * 3];
			Color32 color = new Color32(96, 98, 104, byte.MaxValue);
			for (int i = 0; i < height * 3; i++)
			{
				for (int j = 0; j < width * 3; j++)
				{
					Color32 color2 = pixels[i / 3 * width + j / 3];
					float num = (float)(int)color2.a / 255f;
					array[i * width * 3 + j] = new Color32((byte)((float)(int)color2.r * num + (float)(int)color.r * (1f - num)), (byte)((float)(int)color2.g * num + (float)(int)color.g * (1f - num)), (byte)((float)(int)color2.b * num + (float)(int)color.b * (1f - num)), byte.MaxValue);
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false);
			File.WriteAllBytes(Path.Combine(Log.Folder, name), texture2D.EncodeToPNG());
			UnityEngine.Object.Destroy(texture2D);
			Note("saved " + name);
		}

		private IEnumerator Shot(string name)
		{
			yield return CivilTest.Wait(0.3f);
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
