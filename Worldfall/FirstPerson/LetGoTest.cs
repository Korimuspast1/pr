using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class LetGoTest : CivilTest
	{
		private Actor _host;

		private Actor _other;

		private float _ownStep;

		protected override string Prefix => "LG";

		protected override IEnumerator Body()
		{
			CrownOffer.AutoAnswer = true;
			bool godWas = Mod.Settings.GodView;
			Mod.Settings.GodView = false;
			FirstPersonSave.PathForTest = Path.Combine(Log.Folder, "lg_save");
			try
			{
				for (float t = 0f; t < 6f; t += 0.25f)
				{
					if (!GameClock.Active)
					{
						break;
					}
					yield return CivilTest.Wait(0.25f);
				}
				_ownStep = Time.fixedDeltaTime;
				Check("the game's own step to start with", !GameClock.Active, $"step {_ownStep:F4}, real-time clock {GameClock.Active}");
				Actor actor = Village("human", 10, 0);
				if (!Check("a village", actor != null))
				{
					yield break;
				}
				foreach (Actor item in Founded)
				{
					if (item != null && item.isAlive() && item != actor && item.isAdult() && !Service.IsSoldier(item))
					{
						if (_host == null)
						{
							_host = item;
						}
						else if (_other == null)
						{
							_other = item;
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
						((BaseSimObject)_host).addStatusEffect("invincible", 300f, pColorEffect: false);
					}
					catch
					{
					}
					yield return Part("the town's arms", TownArms());
					yield return Part("the dig sound", DigSound());
					yield return Part("the bedroll's slot", Bedroll());
					yield return Part("let go outdoors", LetGoAnd("outdoors, the mod's G", delegate
					{
						Mod.LetGoForPadTest();
					}));
					yield return Part("let go by the game", LetGoAnd("by the game's own button", delegate
					{
						ControllableUnit.clear();
					}));
					yield return Part("let go twice mid-dive", LetGoAtOnce());
					yield return Part("let go in a house", LetGoInHouse());
					yield return Part("let go from the settings", LetGoFromSettings());
					yield return Part("let go asleep", LetGoAsleep());
					yield return Part("let go with the rise out", LetGoWithRise());
				}
			}
			finally
			{
				LetGoTest letGoTest = this;
				CrownOffer.AutoAnswer = false;
				letGoTest.Mod.Settings.GodView = godWas;
				FirstPersonSave.PathForTest = null;
			}
		}

		private static bool Equip(Actor unit, string id)
		{
			try
			{
				ActorEquipmentSlot slot = unit.equipment.getSlot(EquipmentType.Weapon);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				if (equipmentAsset == null)
				{
					return false;
				}
				Item pItem = World.world.items.generateItem(equipmentAsset, unit.kingdom, unit.getName(), 1, unit, 0, pByPlayer: true);
				slot.setItem(pItem, unit);
				unit.setStatsDirty();
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("LG equip " + id + ": " + ex.Message);
				return false;
			}
		}

		private static long WeaponId(Actor a)
		{
			try
			{
				return a.equipment.getSlot(EquipmentType.Weapon).getItem()?.data.id ?? (-1);
			}
			catch
			{
				return -1L;
			}
		}

		private static Item Forge(City c, string id)
		{
			try
			{
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				return (equipmentAsset != null) ? World.world.items.generateItem(equipmentAsset, c.kingdom, "the smith", 1, null, 0, pByPlayer: true) : null;
			}
			catch (Exception ex)
			{
				Log.Error("LG forge " + id + ": " + ex.Message);
				return null;
			}
		}

		private IEnumerator TownArms()
		{
			City c = _host.city;
			if (c == null)
			{
				Note("(the villager has no town yet: part skipped)");
				yield break;
			}
			Check("the guard on the town's handing out of arms is in", GiveGuard.Installed);
			if (!Check("a wooden sword in your hand", Equip(_host, "sword_wood")))
			{
				yield break;
			}
			yield return null;
			long num = WeaponId(_host);
			Item item = Forge(c, "sword_adamantine");
			if (!Check("an adamantine sword in the armory", item != null))
			{
				yield break;
			}
			List<long> list = new List<long> { item.data.id };
			int kept = GiveGuard.Kept;
			bool flag = false;
			try
			{
				flag = City.giveItem(_host, list, c);
			}
			catch (Exception ex)
			{
				Log.Error("LG giveItem: " + ex.Message);
			}
			Check("the town gives you nothing for it", !flag && list.Count == 1 && GiveGuard.Kept == kept + 1, $"gave {flag}, left {list.Count}, kept {GiveGuard.Kept - kept}");
			Check("...and your sword is still in your hand", WeaponId(_host) == num, $"weapon {num} -> {WeaponId(_host)}");
			if (_other == null)
			{
				Note("(no second villager: the game's own steps not checked)");
			}
			else if (Check("a wooden sword for a villager", Equip(_other, "sword_wood")))
			{
				long num2 = WeaponId(_other);
				try
				{
					flag = City.giveItem(_other, list, c);
				}
				catch (Exception ex2)
				{
					Log.Error("LG giveItem other: " + ex2.Message);
				}
				Check("the town arms a villager as ever", flag && WeaponId(_other) == item.data.id, $"gave {flag}, weapon {num2} -> {WeaponId(_other)}");
				Check("...and their old sword went back into the list it came from", list.Count == 1 && list[0] == num2, string.Format("left {0}: {1}", list.Count, string.Join(",", list)));
			}
		}

		private static bool Held(string sound)
		{
			try
			{
				foreach (GodPower item in AssetManager.powers.list)
				{
					if (item != null && item.sound_drawing == sound)
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

		private IEnumerator DigSound()
		{
			Check("(the shovel power's sound is a held drawing sound)", Held("event:/SFX/POWERS/ShovelMinus"));
			for (int i = 0; i < 4; i++)
			{
				string text = Earthworks.SwingSound(i >= 2, i % 2 == 1);
				Check("a swing's sound is no held drawing sound (" + text + ")", !Held(text));
				bool flag = false;
				try
				{
					flag = MusicBox.isEventExists(text);
				}
				catch
				{
				}
				Check("...and the game has it", flag || !MusicBox.sounds_on);
			}
			yield break;
		}

		private IEnumerator Bedroll()
		{
			Supplies.Give(_host, "tw_item_bedroll", 1);
			yield return null;
			yield return null;
			List<string> list = BagOrder.Of(_host);
			int slot = list.IndexOf(BagOrder.ResourceKey("tw_item_bedroll"));
			if (!Check("a bedroll on the hotbar", slot >= 0 && slot < Hotbar.Size(_host), $"slot {slot} of {Hotbar.Size(_host)}"))
			{
				yield break;
			}
			Hotbar.Choose(_host, slot, wheel: false);
			yield return null;
			Check("the bedroll chosen", Hotbar.Supply(_host)?.Id == "tw_item_bedroll");
			string text = Mod.SleepOnBedrollForTest(_host);
			Check("the bedroll laid out", text == null, text);
			yield return null;
			Check("...and its slot is no longer the chosen one", Hotbar.Chosen(_host) != slot, $"chosen {Hotbar.Chosen(_host)}, the bedroll's {slot}");
			Check("...the chosen one isn't a bedroll", Hotbar.Supply(_host)?.Id != "tw_item_bedroll", Hotbar.InHand(_host));
			for (float t = 0f; t < 8f; t += 0.25f)
			{
				if (!HomeLife.Sleeping)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Check("the night passed", !HomeLife.Sleeping && WorldBoxMod.BedrollNights >= 1, $"sleeping {HomeLife.Sleeping}, nights {WorldBoxMod.BedrollNights}");
			Check("...and you're still in the body", InControl(_host));
		}

		private IEnumerator LetGoAnd(string how, Action letGo)
		{
			if (!InControl(_host))
			{
				yield return Possess(_host);
			}
			if (Check(how + ": in the body first", InControl(_host)))
			{
				yield return CivilTest.Wait(0.5f);
				try
				{
					letGo();
				}
				catch (Exception ex)
				{
					Log.Error("LG let go: " + ex.Message);
				}
				yield return WorldOk(how);
				yield return Possess(_host);
				Check(how + ": taken again after", InControl(_host));
			}
		}

		private IEnumerator LetGoAtOnce()
		{
			if (!InControl(_host))
			{
				yield return Possess(_host);
			}
			Mod.LetGoForPadTest();
			yield return CivilTest.Wait(0.2f);
			try
			{
				ControllableUnit.setControllableCreature(_host);
			}
			catch (Exception ex)
			{
				Log.Error("LG take mid-rise: " + ex.Message);
			}
			yield return CivilTest.Wait(0.3f);
			Mod.LetGoForPadTest();
			yield return WorldOk("twice, mid-rise and mid-dive");
			yield return Possess(_host);
			Check("twice: taken again after", InControl(_host));
		}

		private IEnumerator LetGoInHouse()
		{
			if (!InControl(_host))
			{
				yield return Possess(_host);
			}
			Building building = null;
			try
			{
				City city = _host.city;
				if (city != null)
				{
					foreach (Building building2 in city.buildings)
					{
						if (building2 != null && building2.isAlive() && building2.asset != null && building2.asset.id.StartsWith("house"))
						{
							building = building2;
							break;
						}
					}
				}
			}
			catch
			{
			}
			if (building == null && _host.city != null)
			{
				building = AddHouse("house_human_1", _host.city);
			}
			if (building == null)
			{
				Note("(no house in the village and none could be placed: part skipped)");
				yield break;
			}
			Mod.EnterHouseForTest(building);
			yield return CivilTest.Wait(1f);
			if (Check("in a house", Mod.Interior.Inside))
			{
				Mod.LetGoForPadTest();
				yield return WorldOk("from inside a house");
				Check("the body is left indoors", _host.isAlive());
				yield return Possess(_host);
				Check("house: taken again after", InControl(_host));
				if (Mod.Interior.Inside)
				{
					Mod.ExitHouseForTest();
				}
				yield return CivilTest.Wait(0.5f);
			}
		}

		private IEnumerator LetGoFromSettings()
		{
			if (!InControl(_host))
			{
				yield return Possess(_host);
			}
			Mod.SetSettingsMenu(open: true);
			yield return CivilTest.Wait(0.3f);
			Check("the settings open, offering to leave the body", Mod.SettingsMenu.Open && Mod.CanLeaveBody);
			Mod.AskLeaveBody();
			yield return WorldOk("from the settings");
			Check("settings: the menu closed", !Mod.SettingsMenu.Open);
			yield return Possess(_host);
			Check("settings: taken again after", InControl(_host));
		}

		private IEnumerator LetGoAsleep()
		{
			if (!InControl(_host))
			{
				yield return Possess(_host);
			}
			if (Supplies.Count(_host, "tw_item_bedroll") == 0)
			{
				Supplies.Give(_host, "tw_item_bedroll", 1);
			}
			yield return null;
			string text = Mod.SleepOnBedrollForTest(_host);
			if (Check("asleep on the bedroll", text == null, text))
			{
				yield return CivilTest.Wait(0.4f);
				Mod.LetGoForPadTest();
				yield return WorldOk("asleep, mid-fade");
				yield return Possess(_host);
				Check("asleep: taken again after", InControl(_host));
			}
		}

		private IEnumerator LetGoWithRise()
		{
			if (!InControl(_host))
			{
				yield return Possess(_host);
			}
			Mod.NoDive = false;
			try
			{
				yield return CivilTest.Wait(0.5f);
				Mod.LetGoForPadTest();
				yield return CivilTest.Wait(0.3f);
				Check("the rise out began", Mod.DivingOutForTest || !Mod.IsFirstPerson, $"rising {Mod.DivingOutForTest}, fp {Mod.IsFirstPerson}");
				yield return WorldOk("after the rise out");
			}
			finally
			{
				Mod.NoDive = true;
			}
			yield return Possess(_host);
			Check("rise: taken again after", InControl(_host));
		}

		private IEnumerator WorldOk(string how)
		{
			float t0 = Time.unscaledTime;
			while (Time.unscaledTime - t0 < 10f && (Mod.IsFirstPerson || Mod.DivingOutForTest))
			{
				yield return null;
			}
			bool flag = true;
			try
			{
				flag = ControllableUnit.isControllingUnit();
			}
			catch
			{
			}
			Check(how + ": first person over and the body let go", !Mod.IsFirstPerson && !Mod.DivingOutForTest && !flag, $"fp {Mod.IsFirstPerson}, rising {Mod.DivingOutForTest}, controlling {flag}, after {Time.unscaledTime - t0:F1} s");
			yield return CivilTest.Wait(0.5f);
			bool flag2 = false;
			try
			{
				flag2 = Config.paused || World.world.isPaused();
			}
			catch
			{
			}
			Check(how + ": the world isn't paused", !flag2);
			Check(how + ": the game's own step is back", Mathf.Abs(Time.fixedDeltaTime - _ownStep) < 0.0001f && !GameClock.Active, $"step {Time.fixedDeltaTime:F4} (own {_ownStep:F4}), real-time clock {GameClock.Active}");
			bool flag3 = false;
			try
			{
				flag3 = Config.time_scale_asset.render_skip;
			}
			catch
			{
			}
			Check(how + ": the flat map is drawn again (no render skip held)", !flag3);
			int num = -1;
			try
			{
				num = World.world.camera.cullingMask;
			}
			catch
			{
			}
			Check(how + ": the map camera sees its layers", num != 0, "mask " + num);
			bool flag4 = false;
			try
			{
				flag4 = World.world.console != null && World.world.console.isActive();
			}
			catch
			{
			}
			Check(how + ": the game's console stays shut", !flag4);
			Check(how + ": the cursor is free", Cursor.lockState == CursorLockMode.None && Cursor.visible, $"lock {Cursor.lockState}, visible {Cursor.visible}");
			double w0 = 0.0;
			try
			{
				w0 = World.world.getCurWorldTime();
			}
			catch
			{
			}
			int frames = Time.frameCount;
			yield return CivilTest.Wait(1.5f);
			double num2 = 0.0;
			try
			{
				num2 = World.world.getCurWorldTime();
			}
			catch
			{
			}
			Check(how + ": the world goes on", num2 > w0 + 0.5, $"world time {w0:F2} -> {num2:F2}");
			Check(how + ": the frames go on", Time.frameCount > frames + 5, $"{Time.frameCount - frames} frames in 1.5 s");
			Check(how + ": the body lives on", _host != null && _host.isAlive());
		}
	}
}
