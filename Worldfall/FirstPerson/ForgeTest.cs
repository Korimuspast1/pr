using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class ForgeTest : CivilTest
	{
		private Actor _host;

		private City _city;

		private Building _house;

		protected override string Prefix => "FG";

		private Hud H => Mod.HudForTest;

		private Crafting Craft => Mod.Craft;

		protected override IEnumerator Body()
		{
			Actor actor = Village("human", 6, 0);
			if (!Check("a village", actor != null))
			{
				yield break;
			}
			_city = actor.city;
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
					_host.data.removeString("fp_gear");
				}
				catch
				{
				}
				Supplies.Clear(_host);
				Mod.Settings.HeadBob = 0f;
				Craft.Toggle();
				Craft.Open = false;
				yield return Part("times", Times());
				yield return Part("odds", Odds());
				yield return Part("batch and cancel", BatchAndCancel());
				yield return Part("rolls and the house", Rolls());
				yield return Part("made", Made());
				yield return Part("the card", Card());
				yield return Part("at the workbench", AtBench());
				WorldBoxMod.BusyForTest = false;
			}
		}

		private Crafting.Recipe Recipe(string id)
		{
			return Craft.Recipes.Find((Crafting.Recipe r) => r.Id == id);
		}

		private IEnumerator Times()
		{
			Crafting.Recipe recipe = Recipe("sword_wood");
			Crafting.Recipe recipe2 = Recipe("sword_iron");
			Crafting.Recipe recipe3 = Recipe("sword_adamantine");
			Crafting.Recipe recipe4 = Recipe("tw_item_bandage");
			if (!Check("the recipes", recipe != null && recipe2 != null && recipe4 != null))
			{
				yield break;
			}
			Check("by hand takes 2 s, iron 3.2 s, adamantine 4 s", Mathf.Approximately(recipe.Seconds, 2f) && Mathf.Approximately(recipe2.Seconds, 3.2f) && (recipe3 == null || Mathf.Approximately(recipe3.Seconds, 4f)), $"{recipe.Seconds} / {recipe2.Seconds} / {recipe3?.Seconds}");
			Check("a bandage is quicker", recipe4.Seconds < recipe.Seconds, recipe4.Seconds.ToString("F1"));
			bool ok = true;
			foreach (Crafting.Recipe recipe5 in Craft.Recipes)
			{
				if (recipe5.Seconds < 1f || recipe5.Seconds > 4.01f)
				{
					ok = false;
				}
			}
			Check("every recipe takes between 1 s and CraftSeconds", ok);
			Check("lamp posts and braziers are made by hand", Recipe("tw_item_lamppost") == null || (Recipe("tw_item_lamppost").Rank == 0 && (Recipe("tw_item_brazier") == null || Recipe("tw_item_brazier").Rank == 0)));
		}

		private IEnumerator Odds()
		{
			EquipmentAsset sword = AssetManager.items.get("sword_iron");
			EquipmentAsset a = AssetManager.items.get("helmet_iron");
			float[] array = CraftOdds.For(sword, 1);
			float[] array2 = CraftOdds.For(sword, 6);
			float[] o = CraftOdds.For(a, 1);
			if (!Check("odds worked out", array != null && array2 != null))
			{
				yield break;
			}
			float num = 0f;
			float[] array3 = array;
			foreach (float num2 in array3)
			{
				num += num2;
			}
			Check("the odds add up to one", Mathf.Abs(num - 1f) < 0.001f, num.ToString("F4"));
			Note("iron sword, 1 roll: " + Fmt(array) + "; 6 rolls: " + Fmt(array2) + "; iron helmet, 1 roll: " + Fmt(o));
			Check("one roll is mostly Normal", array[0] >= 0.45f, Fmt(array));
			Check("more rolls, better odds", array2[0] < array[0] && array2[1] + array2[2] + array2[3] > array[1] + array[2] + array[3], Fmt(array) + " -> " + Fmt(array2));
			Check("asked again, the same odds (worked out once)", CraftOdds.For(sword, 1) == array);
			Check("a quality from kept enchantments", CraftOdds.QualityOf(sword, new string[0]) == Rarity.R0_Normal);
			int[] array4 = new int[2] { 1, 4 };
			foreach (int num3 in array4)
			{
				float[] array5 = new float[4];
				for (int k = 0; k < 600; k++)
				{
					Item item = null;
					try
					{
						item = World.world.items.generateItem(sword, _host.kingdom, "test", num3, _host, 0, pByPlayer: true);
						Crafting.Ordinary(item);
						Gear.Piece piece = Gear.FromItem(item);
						array5[(int)CraftOdds.QualityOf(sword, piece.Mods)] += 0.0016666667f;
					}
					catch (Exception ex)
					{
						Note("generate: " + ex.Message);
						break;
					}
					finally
					{
						if (item != null)
						{
							Gear.LetGo(item);
						}
					}
				}
				float[] array6 = CraftOdds.For(sword, num3);
				float num4 = 0f;
				for (int l = 0; l < 4; l++)
				{
					num4 = Mathf.Max(num4, Mathf.Abs(array5[l] - array6[l]));
				}
				Check($"{num3} roll(s): the game's own pieces come out as the odds say", num4 < 0.07f, "made " + Fmt(array5) + ", odds " + Fmt(array6));
				yield return null;
			}
		}

		private static string Fmt(float[] o)
		{
			if (o != null)
			{
				return string.Join(" ", Array.ConvertAll(o, (float f) => Mathf.RoundToInt(f * 100f) + "%"));
			}
			return "none";
		}

		private IEnumerator BatchAndCancel()
		{
			try
			{
				_host.clearHomeBuilding();
			}
			catch
			{
			}
			Crafting.Recipe band = Recipe("tw_item_bandage");
			Crafting.Recipe sword = Recipe("sword_wood");
			if (Check("bandage and sword recipes", band != null && sword != null))
			{
				try
				{
					_host.addToInventory(band.Res1, band.Amount1 * 5);
					_host.addToInventory(band.Res2, band.Amount2 * 5);
				}
				catch
				{
				}
				int h0 = CraftStores.Count(_host, band.Res1);
				int w0 = CraftStores.Count(_host, band.Res2);
				bool flag = Craft.Start(_host, band, Mod.Toast, 5);
				int num = Craft.Queued(band);
				Check("x5: five bandage makes, one order", flag && Craft.Queue.Count == 1 && num == 5, $"{Craft.Queue.Count} orders, {num} makes, toast \"{Mod.ToastText}\"");
				Check("paid for all five at once", h0 - CraftStores.Count(_host, band.Res1) == band.Amount1 * num && w0 - CraftStores.Count(_host, band.Res2) == band.Amount2 * num, $"{band.Res1} {h0} -> {CraftStores.Count(_host, band.Res1)}");
				yield return CivilTest.Wait(0.5f);
				Check("calling it off", Craft.Cancel(_host, 0) && Craft.Queue.Count == 0);
				Check("gives back all it cost", CraftStores.Count(_host, band.Res1) == h0 && CraftStores.Count(_host, band.Res2) == w0, $"{band.Res1} {CraftStores.Count(_host, band.Res1)} of {h0}");
				try
				{
					_host.addToInventory(sword.Res1, sword.Amount1 * 2);
				}
				catch
				{
				}
				int wood0 = CraftStores.Count(_host, sword.Res1);
				int had = Gear.Of(_host).FindAll((Gear.Piece p) => p.Id == "sword_wood").Count;
				Craft.Start(_host, sword, Mod.Toast);
				Craft.Start(_host, sword, Mod.Toast);
				Check("two swords waiting", Craft.Queued(sword) == 2);
				Check("the second called off (a right click on its square)", Craft.CancelLast(_host, sword) && Craft.Queued(sword) == 1 && CraftStores.Count(_host, sword.Res1) == wood0 - sword.Amount1, $"{sword.Res1} {wood0} -> {CraftStores.Count(_host, sword.Res1)}");
				Craft.ForgetForTest();
				yield return CivilTest.Wait(0.3f);
				Check("a load: the queue kept on the body carries on", Craft.Queued(sword) == 1 && CraftStores.Count(_host, sword.Res1) == wood0 - sword.Amount1, $"{Craft.Queued(sword)} queued, {sword.Res1} {CraftStores.Count(_host, sword.Res1)}");
				int blows = Craft.Blows;
				yield return Done();
				Check("the first is made", Gear.Of(_host).FindAll((Gear.Piece p) => p.Id == "sword_wood").Count == had + 1);
				Check("struck a few times", Craft.Blows - blows >= 2, Craft.Blows - blows + " blows");
			}
		}

		private IEnumerator Rolls()
		{
			try
			{
				_host.clearHomeBuilding();
			}
			catch
			{
			}
			Check("no workbench: one roll", Crafting.Rolls(_host) == 1, Crafting.Rolls(_host).ToString());
			_house = PlaceHouse("house_human_0");
			if (Check("a house", _house != null))
			{
				_host.setHomeBuilding(_house);
				Move(HouseSpot());
				yield return CivilTest.Wait(0.6f);
				int num = Workbench.Level(_host);
				Check("at home: a roll more per workbench level", num > 0 && Crafting.Rolls(_host) == 1 + num, $"level {num}, rolls {Crafting.Rolls(_host)}");
				Move(HouseSpot() + new Vector2(18f, 0f));
				yield return CivilTest.Wait(0.4f);
				Check("away: one roll", Crafting.Rolls(_host) == 1, Crafting.Rolls(_host).ToString());
				Move(HouseSpot());
				yield return CivilTest.Wait(0.4f);
			}
		}

		private IEnumerator Made()
		{
			Crafting.Recipe sword = Recipe("sword_wood");
			Crafting.Recipe iron = Recipe("sword_iron");
			if (sword == null)
			{
				yield break;
			}
			try
			{
				_host.addToInventory(sword.Res1, sword.Amount1);
			}
			catch
			{
			}
			int serial = Craft.Last?.Serial ?? 0;
			Craft.Open = true;
			Craft.Selected = sword;
			Craft.Start(_host, sword, Mod.Toast);
			for (float t = 0f; t < 12f; t += 0.1f)
			{
				if ((Craft.Last?.Serial ?? 0) != serial)
				{
					break;
				}
				yield return CivilTest.Wait(0.1f);
			}
			Crafting.Made m = Craft.Last;
			if (Check("what's made is kept for its card", m != null && m.Serial != serial && m.Piece != null && m.Piece.Id == "sword_wood", m?.Name))
			{
				Check("with the quality the game rolled", m.Quality == CraftOdds.QualityOf(sword.Asset, m.Piece.Mods), CraftOdds.Word(m.Quality) + ", mods " + string.Join("+", m.Piece.Mods));
				yield return Shot("fg_result.png", 0.15f);
				Check("and it is in the bag", Gear.Of(_host).Exists((Gear.Piece p) => p.Id == "sword_wood") && !m.Spilled);
				if (iron != null)
				{
					Craft.Last = new Crafting.Made
					{
						Recipe = iron,
						Piece = new Gear.Piece
						{
							Id = "sword_iron",
							Mods = new string[2] { "power1", "sharpness1" }
						},
						Quality = Rarity.R2_Epic,
						Name = "Iron sword",
						Count = 1,
						At = Time.unscaledTime,
						Serial = 90001
					};
					yield return Shot("fg_epic.png", 0.35f);
					Craft.Last = new Crafting.Made
					{
						Recipe = iron,
						Piece = new Gear.Piece
						{
							Id = "sword_iron",
							Mods = new string[1] { "power3" },
							Name = "Ashbringer"
						},
						Quality = Rarity.R3_Legendary,
						Name = "Ashbringer",
						Count = 1,
						At = Time.unscaledTime,
						Serial = 90002
					};
					yield return Shot("fg_legendary.png", 0.5f);
					Craft.Last.At = -99f;
				}
				Craft.Open = false;
			}
		}

		private IEnumerator Card()
		{
			Crafting.Recipe recipe = Recipe("sword_iron");
			Crafting.Recipe stone = Recipe("sword_stone");
			Crafting.Recipe band = Recipe("tw_item_bandage");
			Craft.Open = true;
			Craft.ShowAll = true;
			Craft.Selected = recipe;
			string text = ((recipe != null) ? Craft.WhyNot(_host, recipe) : null);
			Check("iron needs more workbench than a small house's", text?.Contains("level 3") ?? false, text);
			yield return Shot("fg_card_locked.png");
			Craft.ShowAll = false;
			if (stone != null)
			{
				try
				{
					_host.addToInventory(stone.Res1, stone.Amount1 * 2);
					if (stone.Res2 != null)
					{
						_host.addToInventory(stone.Res2, stone.Amount2 * 2);
					}
				}
				catch
				{
				}
				Craft.Selected = stone;
				Check("a stone sword can be made at home", Craft.WhyNot(_host, stone) == null, Craft.WhyNot(_host, stone));
				yield return Shot("fg_card_stone.png");
			}
			if (band != null)
			{
				try
				{
					_host.addToInventory(band.Res1, band.Amount1 * 3);
					_host.addToInventory(band.Res2, band.Amount2 * 3);
				}
				catch
				{
				}
				Craft.Selected = band;
				yield return Shot("fg_card_supply.png");
				if (stone != null)
				{
					Craft.Start(_host, stone, Mod.Toast);
				}
				Craft.Start(_host, band, Mod.Toast, 5);
				Craft.Selected = stone;
				for (float t = 0f; t < 3f; t += 0.02f)
				{
					if (Time.unscaledTime - Craft.BlowAt < 0.12f && Craft.Progress > 0.3f)
					{
						break;
					}
					yield return null;
				}
				yield return Shot("fg_queue.png", 0f);
				Check("both orders queued, the batch as one", Craft.Queue.Count == 2 && Craft.Queue[1].Left >= 1, Craft.Queue.Count + " orders");
				yield return Done();
			}
			Craft.Open = false;
		}

		private IEnumerator AtBench()
		{
			Mod.EnterHouseForTest(_house);
			yield return CivilTest.Wait(1f);
			HouseInterior interior = Mod.Interior;
			if (!Check("inside your house, by your workbench", interior.Inside && interior.HasBench, $"inside {interior.Inside}, bench {interior.HasBench}"))
			{
				yield break;
			}
			Vector2 vector = new Vector2(interior.BenchX, interior.BenchY);
			Vector2 eye = interior.Eye;
			Vector2 vector2 = vector - eye;
			interior.Eye = vector - ((vector2.sqrMagnitude > 0.01f) ? vector2.normalized : Vector2.right) * 1f;
			Mod.LookForTest(Mathf.Atan2(vector.y - interior.Eye.y, vector.x - interior.Eye.x), -14f);
			yield return CivilTest.Wait(0.6f);
			Check("facing it: E is for your workbench", Mod.Prompt == "E:Workbench", Mod.Prompt);
			PlayInput.Inject(KeyCode.E, shift: false);
			yield return null;
			yield return null;
			Check("E opens the whole inventory on crafting", Craft.Open && !Mod.QuickInventory && Craft.Tab == 0, $"open {Craft.Open}, quick {Mod.QuickInventory}, tab {Craft.Tab}");
			yield return Shot("fg_bench_window.png");
			Craft.Open = false;
			yield return null;
			Crafting.Recipe recipe = Recipe("sword_stone");
			if (recipe == null)
			{
				yield break;
			}
			try
			{
				_host.addToInventory(recipe.Res1, recipe.Amount1 * 2);
				if (recipe.Res2 != null)
				{
					_host.addToInventory(recipe.Res2, recipe.Amount2 * 2);
				}
			}
			catch
			{
			}
			Check("started at the workbench", Craft.Start(_host, recipe, Mod.Toast));
			yield return CivilTest.Wait(0.5f);
			Check("forged at the workbench: the hands hold a hammer", Craft.AtBench && Mod.ForgeHandsForTest, $"at bench {Craft.AtBench}, hands {Mod.ForgeHandsForTest}, waiting {Craft.Waiting}");
			for (float t = 0f; t < 1.5f; t += 0.02f)
			{
				if (!(Time.unscaledTime - Craft.BlowAt > 0.08f))
				{
					break;
				}
				yield return null;
			}
			yield return Shot("fg_hammer.png", 0f);
			WorldBoxMod.BusyForTest = true;
			yield return CivilTest.Wait(0.2f);
			float was = Craft.Progress;
			yield return CivilTest.Wait(0.8f);
			Check("walking about, the work stops", Craft.Waiting == "Stand still to work" && Mathf.Abs(Craft.Progress - was) < 0.01f && !Mod.ForgeHandsForTest, $"waiting \"{Craft.Waiting}\", {was:F2} -> {Craft.Progress:F2}, hands {Mod.ForgeHandsForTest}");
			yield return Shot("fg_wait.png", 0f);
			WorldBoxMod.BusyForTest = false;
			yield return CivilTest.Wait(0.3f);
			Check("standing still, it goes on", Craft.Waiting == null && Craft.Progress > was, $"{was:F2} -> {Craft.Progress:F2}");
			yield return Shot("fg_forgebar.png", 0f);
			Craft.Open = true;
			yield return CivilTest.Wait(0.3f);
			Check("the inventory open: no hammer in the hands", !Mod.ForgeHandsForTest);
			Craft.Open = false;
			yield return Done();
			Check("made at the workbench", Craft.Queue.Count == 0 && Gear.Of(_host).Exists((Gear.Piece p) => p.Id == "sword_stone"));
			Mod.LeaveRoomForTest();
			yield return CivilTest.Wait(0.5f);
		}

		private IEnumerator Done()
		{
			for (float t = 0f; t < 20f; t += 0.25f)
			{
				if (Craft.Queue.Count <= 0)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			yield return null;
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

		private IEnumerator Shot(string name, float wait = 0.4f)
		{
			if (wait > 0f)
			{
				yield return CivilTest.Wait(wait);
			}
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
