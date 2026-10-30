using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class FeedbackTest : CivilTest
	{
		private Actor _host;

		private Actor _kingA;

		private Actor _kingB;

		private City _townA;

		private City _townB;

		private readonly List<Actor> _ours = new List<Actor>();

		private Settings _saved;

		protected override string Prefix => "FEEDBACK";

		private Hud H => Mod.HudForTest;

		protected override IEnumerator Body()
		{
			_saved = new Settings
			{
				FriendlyFire = Mod.Settings.FriendlyFire,
				UiScale = Mod.Settings.UiScale
			};
			Mod.Settings.FriendlyFire = false;
			_kingA = Village("human", 10, 0);
			if (!Check("a village of your own", _kingA != null))
			{
				yield break;
			}
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != _kingA && item.isAdult())
				{
					_ours.Add(item);
				}
			}
			WorldTile tile = _kingA.city.getTile();
			_kingB = Village("human", 6, 0, tile, 40f);
			if (!Check("a second kingdom a way off", _kingB != null && _kingB.kingdom != _kingA.kingdom))
			{
				yield break;
			}
			_townA = _kingA.city;
			_townB = _kingB.city;
			_host = ((_ours.Count > 0) ? _ours[0] : _kingA);
			yield return Possess(_host);
			if (Check("first person in a villager", InControl(_host)))
			{
				try
				{
					((BaseSimObject)_host).addStatusEffect("invincible", 900f, pColorEffect: false);
				}
				catch
				{
				}
				Mod.Settings.HeadBob = 0f;
				Equip(_host, "sword_iron");
				yield return Part("interface size", Size());
				yield return Part("friendly fire off", Friends());
				yield return Part("a building coming down", Dismantling());
				yield return Part("a town being taken", Capture());
				Mod.Settings.FriendlyFire = _saved.FriendlyFire;
				Mod.Settings.UiScale = _saved.UiScale;
			}
		}

		private IEnumerator Size()
		{
			float game = GameArt.GameP;
			Note($"screen {Screen.width}x{Screen.height}, the game's UI scale {game:F2}, the HUD's {GameArt.P:F2}");
			yield return Shot("fb_ui_100.png");
			Mod.Settings.UiScale = 1.5f;
			yield return CivilTest.Wait(1.4f);
			Check("interface size 150%: the HUD is drawn half as big again", Mathf.Abs(GameArt.P - Mathf.Clamp(game * 1.5f, 1f, 10f)) < 0.05f, $"P {GameArt.P:F2} for the game's {game:F2}");
			Check("...and the text with it", Mathf.Abs(Device.HudScale - (Device.OnPhone ? 1.8f : 1.5f)) < 0.01f, $"HudScale {Device.HudScale:F2}");
			yield return Shot("fb_ui_150.png");
			Mod.SettingsMenu.Open = true;
			Mod.SettingsMenu.Tab = 0;
			yield return CivilTest.Wait(0.8f);
			yield return Shot("fb_menu_150.png");
			Mod.SettingsMenu.Open = false;
			yield return CivilTest.Wait(0.5f);
			Mod.Settings.UiScale = 1f;
			yield return CivilTest.Wait(1.4f);
			Check("back to 100%", Mathf.Abs(GameArt.P - game) < 0.05f, $"P {GameArt.P:F2}");
		}

		private IEnumerator Friends()
		{
			Actor friend = ((_ours.Count > 1) ? _ours[1] : null);
			Actor actor = null;
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != _kingB && item.isAdult())
				{
					actor = item;
					break;
				}
			}
			if (!Check("a townsman and a stranger", friend != null && actor != null))
			{
				yield break;
			}
			Mod.Settings.FriendlyFire = false;
			Check("off: a creature power's bite or charge passes over a townsman", !FriendlyFire.MayHurt(_host, friend));
			Check("...but not over a stranger", FriendlyFire.MayHurt(_host, actor));
			Mod.Settings.FriendlyFire = true;
			Check("on: it lands on the townsman", FriendlyFire.MayHurt(_host, friend));
			Mod.Settings.FriendlyFire = false;
			Actor wild = CivilTest.SpawnNear("sheep", _host.current_position + new Vector2(3f, 0f));
			if (!Check("a sheep to stand beside the townsman", wild != null))
			{
				yield break;
			}
			Vector2 spot;
			int pass;
			Actor[] array;
			for (pass = 0; pass < 2; pass++)
			{
				bool on = pass == 1;
				Mod.Settings.FriendlyFire = on;
				spot = _host.current_position + new Vector2(3f, 0f);
				array = new Actor[2] { friend, wild };
				foreach (Actor actor2 in array)
				{
					try
					{
						actor2.finishStatusEffect("burning");
						actor2.cancelAllBeh();
						actor2.makeWait(3f);
						actor2.current_position = spot;
						actor2.dirty_current_tile = true;
					}
					catch
					{
					}
				}
				yield return CivilTest.Wait(0.3f);
				int spared = FriendlyBurns.Spared;
				try
				{
					ActionLibrary.startBurningObjects(_host, null, World.world.GetTile((int)spot.x, (int)spot.y));
				}
				catch (Exception ex)
				{
					Note("burning: " + ex.Message);
				}
				bool flag = false;
				bool ok = false;
				try
				{
					flag = ((BaseSimObject)friend).hasStatus("burning");
					ok = ((BaseSimObject)wild)?.hasStatus("burning") ?? false;
				}
				catch
				{
				}
				Check(on ? "friendly fire on: your fire catches the townsman too" : "friendly fire off: your fire passes over the townsman", on ? flag : (!flag && FriendlyBurns.Spared > spared), $"townsman burning {flag}, spared {spared}->{FriendlyBurns.Spared}");
				Check("...and catches the sheep beside them", ok);
				array = new Actor[2] { friend, wild };
				foreach (Actor actor3 in array)
				{
					try
					{
						actor3.finishStatusEffect("burning");
						actor3.restoreHealthPercent(1f);
					}
					catch
					{
					}
				}
			}
			Mod.Settings.FriendlyFire = false;
			spot = _host.current_position + new Vector2(3f, 0f);
			array = new Actor[2] { friend, wild };
			foreach (Actor actor4 in array)
			{
				try
				{
					actor4.cancelAllBeh();
					actor4.makeWait(3f);
					actor4.current_position = spot;
					actor4.dirty_current_tile = true;
					actor4.restoreHealthPercent(1f);
				}
				catch
				{
				}
			}
			yield return CivilTest.Wait(0.3f);
			float f0 = Health(friend);
			float s0 = Health(wild);
			pass = FriendlyBurns.BlastSpared;
			try
			{
				World.world.applyForceOnTile(World.world.GetTile((int)spot.x, (int)spot.y), 3, 1.5f, pForceOut: true, 12, null, _host);
			}
			catch (Exception ex2)
			{
				Note("blast: " + ex2.Message);
			}
			yield return CivilTest.Wait(0.2f);
			float num = Health(friend);
			float num2 = Health(wild);
			Check("friendly fire off: your blast passes over the townsman", num >= f0 && FriendlyBurns.BlastSpared > pass, $"townsman {f0}->{num}, spared {pass}->{FriendlyBurns.BlastSpared}");
			Check("...and hurts the sheep beside them", num2 < s0, $"sheep {s0}->{num2}");
			array = new Actor[2] { friend, wild };
			foreach (Actor actor5 in array)
			{
				try
				{
					actor5.restoreHealthPercent(1f);
					actor5.finishStatusEffect("stunned");
				}
				catch
				{
				}
			}
			CivilTest.Kill(wild);
		}

		private IEnumerator Dismantling()
		{
			State("houses");
			Building stranger = House(Centre(_townB), _townB);
			if (!Check("another realm's house to knock down", stranger != null && stranger.city == _townB))
			{
				yield break;
			}
			yield return Swings(stranger, 4, "another realm's house", shouldLand: true);
			yield return Shot("fb_dismantle.png");
			Check("its bar is up, with how far it's come", Dismantle.Current() == stranger && Hud.DismantleFillForTest > 0f, $"fill {Hud.DismantleFillForTest:F2}");
			Remove(stranger);
			yield return CivilTest.Wait(0.3f);
			State("an empty house");
			Building empty = House(Centre(_townA), _townA);
			if (empty != null)
			{
				try
				{
					empty.makeAbandoned();
				}
				catch (Exception ex)
				{
					Note("abandon: " + ex.Message);
				}
				Note("abandoned house: kingdom " + ((empty.kingdom != null) ? (empty.kingdom.data.id.ToString() ?? "") : "none") + ", town " + ((empty.city != null) ? empty.city.name : "none"));
				yield return Swings(empty, 3, "an abandoned house", shouldLand: true);
				Remove(empty);
				yield return CivilTest.Wait(0.3f);
			}
			State("your own house");
			Building mine = House(Centre(_townA), _townA);
			if (!Check("a house of your own town", mine != null && mine.city == _townA, (mine != null && mine.city != null) ? mine.city.name : "none"))
			{
				yield break;
			}
			Check("no battle on", !FriendlyFire.InBattle(_host));
			yield return Swings(mine, 3, "your own house, aimed at, friendly fire off", shouldLand: true);
			Actor sheep = CivilTest.SpawnNear("sheep", Between(mine, 0.45f));
			if (sheep != null)
			{
				yield return CivilTest.Wait(0.3f);
				float h0 = Health(mine);
				for (int i = 0; i < 3; i++)
				{
					try
					{
						sheep.cancelAllBeh();
						sheep.makeWait(3f);
						sheep.current_position = Between(mine, 0.45f);
					}
					catch
					{
					}
					Look(sheep.current_position, 0.3f);
					yield return CivilTest.Wait(0.15f);
					PossessionCombat.AttackForTest(_host);
					yield return CivilTest.Wait(0.5f);
				}
				Check("a blow at someone by your own house spares the house", Health(mine) >= h0, string.Format("house {0}->{1}, blow at {2}", h0, Health(mine), (PossessionCombat.LastBlowAt != null) ? PossessionCombat.LastBlowAt.asset.id : "nothing"));
				CivilTest.Kill(sheep);
			}
			Remove(mine);
		}

		private IEnumerator Swings(Building b, int blows, string what, bool shouldLand)
		{
			Vector2 c = b.current_position;
			Vector2 from = c + new Vector2(0f, -2.2f);
			_host.current_position = from;
			_host.dirty_current_tile = true;
			yield return CivilTest.Wait(0.3f);
			float h0 = Health(b);
			float max = 0f;
			try
			{
				max = b.getMaxHealth();
			}
			catch
			{
			}
			string picked = "";
			for (int i = 0; i < blows; i++)
			{
				_host.current_position = from;
				Look(c, 0.6f);
				yield return CivilTest.Wait(0.2f);
				picked = ((!(Mod.Picked is Building building)) ? ((Mod.Picked != null) ? Mod.Picked.GetType().Name : "nothing") : ((building == b) ? "the house" : building.asset.id));
				PossessionCombat.AttackForTest(_host);
				yield return CivilTest.Wait(0.55f);
			}
			float num = Health(b);
			bool flag = num < h0;
			Check(shouldLand ? ("blows land on " + what) : ("blows pass over " + what), flag == shouldLand, string.Format("health {0}->{1} of {2}, crosshair on {3}, struck {4}", h0, num, max, picked, (Dismantle.Last == b) ? "it" : "something else"));
		}

		private Building House(Vector2 near, City city)
		{
			for (int i = 0; i < 80; i++)
			{
				float f = (float)i * 0.7f;
				float num = 5f + (float)i * 0.3f;
				WorldTile tile = World.world.GetTile((int)(near.x + Mathf.Cos(f) * num), (int)(near.y + Mathf.Sin(f) * num));
				if (tile == null || tile.Type == null || !tile.Type.ground || tile.Type.block || tile.Type.liquid || tile.building != null)
				{
					continue;
				}
				bool flag = false;
				try
				{
					flag = tile.zone != null && tile.zone.city != null;
				}
				catch
				{
				}
				if (city == null && flag)
				{
					continue;
				}
				try
				{
					Building building = World.world.buildings.addBuilding("house_human_1", tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					if (building == null)
					{
						continue;
					}
					building.completeConstruction();
					if (city != null && building.city != city)
					{
						city.listBuilding(building);
					}
					Note(string.Format("house at {0},{1}, town {2}, health {3}", tile.x, tile.y, (building.city != null) ? building.city.name : "none", Health(building)));
					return building;
				}
				catch (Exception ex)
				{
					Note("house: " + ex.Message);
				}
			}
			return null;
		}

		private void Remove(Building b)
		{
			try
			{
				if (b != null && b.isAlive())
				{
					b.removeBuildingFinal();
				}
			}
			catch
			{
			}
		}

		private Vector2 Between(Building b, float t)
		{
			Vector2 current_position = _host.current_position;
			Vector2 current_position2 = b.current_position;
			return Vector2.Lerp(current_position, current_position2, t);
		}

		private void Look(Vector2 at, float up)
		{
			Vector3 eyeForTest = Mod.EyeForTest;
			float num = at.x - eyeForTest.x;
			float num2 = at.y - eyeForTest.y;
			float num3 = Mathf.Max(0.05f, Mathf.Sqrt(num * num + num2 * num2));
			float num4 = 0f;
			try
			{
				num4 = Mod.GroundAt(at);
			}
			catch
			{
			}
			Mod.SetYaw(Mathf.Atan2(num2, num));
			Mod.SetPitchDegrees(Mathf.Clamp(Mathf.Atan((num4 + up - eyeForTest.z) / num3) * 57.29578f, -44f, 44f));
		}

		private IEnumerator Capture()
		{
			State("capture");
			City a = _townA;
			City b = _townB;
			if (Check("two towns", a != null && b != null && a.isAlive() && b.isAlive()))
			{
				Config.paused = true;
				try
				{
					b.being_captured_by = _kingA.kingdom;
					b._capture_ticks = 40f;
				}
				catch (Exception ex)
				{
					Check("set a capture going", ok: false, ex.Message);
				}
				Stand(Centre(b));
				yield return CivilTest.Wait(1.6f);
				string captureLineForTest = Hud.CaptureLineForTest;
				Check("in a town your side is taking: a bar says so", captureLineForTest?.StartsWith("Taking", StringComparison.Ordinal) ?? false, captureLineForTest);
				Check("...filled as far as the game's count", Mathf.Abs(Hud.CaptureFillForTest - 0.4f) < 0.05f, $"fill {Hud.CaptureFillForTest:F2}");
				yield return Shot("fb_capture_taking.png");
				try
				{
					b.being_captured_by = null;
					b._capture_ticks = 0f;
				}
				catch
				{
				}
				try
				{
					a.being_captured_by = _kingB.kingdom;
					a._capture_ticks = 70f;
				}
				catch (Exception ex2)
				{
					Check("set a capture going", ok: false, ex2.Message);
				}
				Stand(Centre(a));
				yield return CivilTest.Wait(1.6f);
				captureLineForTest = Hud.CaptureLineForTest;
				Check("in your own town being taken: a red bar", captureLineForTest?.Contains("is falling to") ?? false, captureLineForTest);
				Check("...filled as far as the game's count", Mathf.Abs(Hud.CaptureFillForTest - 0.7f) < 0.05f, $"fill {Hud.CaptureFillForTest:F2}");
				Mod.Settings.UiScale = 1.5f;
				yield return CivilTest.Wait(1.4f);
				yield return Shot("fb_capture_falling_150.png");
				Mod.Settings.UiScale = 1f;
				try
				{
					a.being_captured_by = null;
					a._capture_ticks = 0f;
				}
				catch
				{
				}
				yield return CivilTest.Wait(1.6f);
				Check("the bar goes once it's over", Hud.CaptureLineForTest == null, Hud.CaptureLineForTest);
				Config.paused = false;
			}
		}

		private void State(string when)
		{
			string text = "?";
			try
			{
				text = ((_host.city != null) ? _host.city.name : "no town") + ", " + ((_host.kingdom != null) ? _host.kingdom.name : "no kingdom");
			}
			catch
			{
			}
			Note("state before " + when + ": your town " + Town(_townA) + ", king A " + King(_kingA) + "; their town " + Town(_townB) + ", king B " + King(_kingB) + "; you: " + text);
			string King(Actor k)
			{
				try
				{
					return (k == null) ? "none" : ((k.isAlive() ? "alive" : "dead") + ", town " + ((k.city != null) ? k.city.name : "none"));
				}
				catch
				{
					return "?";
				}
			}
			string Town(City c)
			{
				try
				{
					return (c == null) ? "none" : (c.name + (c.isAlive() ? "" : " (gone)") + " of " + ((c.kingdom != null) ? c.kingdom.name : "no kingdom"));
				}
				catch
				{
					return "?";
				}
			}
		}

		private static Vector2 Centre(City c)
		{
			try
			{
				return (c.city_center != Vector2.zero) ? c.city_center : c.last_city_center;
			}
			catch
			{
				return Vector2.zero;
			}
		}

		private void Stand(Vector2 at)
		{
			_host.current_position = at;
			_host.dirty_current_tile = true;
		}

		private IEnumerator Shot(string name)
		{
			yield return CivilTest.Wait(0.3f);
			H.CaptureHudForTest(Path.Combine(Log.Folder, name));
			for (int i = 0; i < 30; i++)
			{
				if (!H.CapturePending)
				{
					break;
				}
				yield return null;
			}
			yield return null;
		}

		private static float Health(BaseSimObject o)
		{
			try
			{
				return (o != null && o.isAlive()) ? ((float)o.getHealth()) : 0f;
			}
			catch
			{
				return 0f;
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
				Log.Error("FEEDBACK equip " + id + ": " + ex.Message);
				return false;
			}
		}
	}
}
