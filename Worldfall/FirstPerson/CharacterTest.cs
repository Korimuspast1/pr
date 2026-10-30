using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Towns;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class CharacterTest : CivilTest
	{
		private Actor _host;

		private Actor _kingA;

		private Actor _kingB;

		private readonly List<Actor> _ours = new List<Actor>();

		private readonly List<Actor> _theirs = new List<Actor>();

		private Settings _saved;

		protected override string Prefix => "CHAR";

		private Hud H => Mod.HudForTest;

		private SettingsMenu Menu => Mod.SettingsMenu;

		protected override IEnumerator Body()
		{
			_kingA = Village("human", 12, 0);
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
			_kingB = Village("human", 8, 0, _kingA.city.getTile(), 40f);
			if (!Check("a second kingdom a way off", _kingB != null && _kingB.kingdom != _kingA.kingdom))
			{
				yield break;
			}
			foreach (Actor item2 in Founded)
			{
				if (item2 != null && item2.isAlive() && item2 != _kingB && item2.isAdult())
				{
					_theirs.Add(item2);
				}
			}
			_host = ((_ours.Count > 0) ? _ours[0] : _kingA);
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
				_saved = new Settings
				{
					FriendlyFire = Mod.Settings.FriendlyFire,
					Markers = Mod.Settings.Markers,
					FamilyFollows = Mod.Settings.FamilyFollows
				};
				Mod.Settings.FamilyFollows = false;
				Mod.Settings.HeadBob = 0f;
				yield return Part("tab", Tab());
				yield return Part("gear", Gear());
				yield return Part("traits", Traits());
				yield return Part("family follows", Family());
				yield return Part("war", War());
				yield return Part("friendly fire", Fire());
				yield return Part("markers", Markers());
				yield return Part("family map", FamilyMapPart());
				Mod.Settings.FriendlyFire = _saved.FriendlyFire;
				Mod.Settings.Markers = _saved.Markers;
				Mod.Settings.FamilyFollows = _saved.FamilyFollows;
			}
		}

		private IEnumerator Shot(string name)
		{
			yield return CivilTest.Wait(0.5f);
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

		private IEnumerator Tab()
		{
			Check("a Character tab", SettingsMenu.Tabs[5] == "Character");
			Mod.SetSettingsMenu(open: true);
			Menu.Tab = 5;
			List<SettingsMenu.Row> list = Menu.RowsOf(5);
			Check("its friendly fire and markers rows", list != null && list.Exists((SettingsMenu.Row r) => r.Name == "Friendly fire") && list.Exists((SettingsMenu.Row r) => r.Name == "Markers"));
			Menu.Shelf = 0;
			yield return Shot("char_weapons.png");
			Check("shelves of weapons, armor, jewelry and traits", H.ShelfCountForTest(0) > 5 && H.ShelfCountForTest(1) > 3 && H.ShelfCountForTest(2) > 0 && H.ShelfCountForTest(3) > 10, $"{H.ShelfCountForTest(0)} / {H.ShelfCountForTest(1)} / {H.ShelfCountForTest(2)} / {H.ShelfCountForTest(3)}");
		}

		private int Spares(EquipmentAsset a)
		{
			return FirstPerson.Gear.Of(_host).FindAll((Gear.Piece p) => p.Asset == a).Count;
		}

		private IEnumerator Gear()
		{
			EquipmentAsset sword = AssetManager.items.get("sword_iron");
			H.ToggleItem(_host, sword);
			yield return CivilTest.Wait(0.3f);
			Check("an iron sword given", _host.getWeapon()?.asset == sword, Weapon());
			H.ToggleItem(_host, sword);
			yield return CivilTest.Wait(0.3f);
			Check("and taken off again", _host.getWeapon() == null, Weapon());
			int swords = Spares(sword);
			for (int i = 0; i < 5; i++)
			{
				H.ToggleItem(_host, sword);
				yield return CivilTest.Wait(0.2f);
			}
			Check("given and taken five times: in hand, no copies in the bag", _host.getWeapon()?.asset == sword && Spares(sword) == swords, Weapon() + ", spares " + swords + " -> " + Spares(sword));
			H.ToggleItem(_host, sword);
			yield return CivilTest.Wait(0.3f);
			Check("...and taken away for good, not back in hand", _host.getWeapon() == null && Spares(sword) == swords, Weapon() + ", spares " + Spares(sword));
			EquipmentAsset axe = AssetManager.items.get("axe_iron") ?? AssetManager.items.get("axe_bronze");
			H.ToggleItem(_host, sword);
			yield return CivilTest.Wait(0.2f);
			H.ToggleItem(_host, axe);
			yield return CivilTest.Wait(0.2f);
			H.ToggleItem(_host, sword);
			yield return CivilTest.Wait(0.3f);
			Check("sword, axe, sword: the bag's sword taken back, not another made", _host.getWeapon()?.asset == sword && Spares(sword) == swords && Spares(axe) >= 1, Weapon() + ", sword spares " + Spares(sword) + ", axes " + Spares(axe));
			H.ToggleItem(_host, sword);
			yield return CivilTest.Wait(0.3f);
			EquipmentAsset helmet = H.ShelfItemForTest(1, EquipmentType.Helmet);
			if (helmet != null)
			{
				H.ToggleItem(_host, helmet);
			}
			yield return CivilTest.Wait(0.3f);
			Item item = null;
			try
			{
				item = _host.equipment.getSlot(EquipmentType.Helmet).getItem();
			}
			catch
			{
			}
			Check("a helmet given", helmet != null && item?.asset == helmet, helmet?.id);
			H.ToggleItem(_host, sword);
			Menu.Shelf = 1;
			yield return Shot("char_armor.png");
			int num = Economy.Money(_host);
			H.Cheat(_host, 1);
			Check("the gold cheat", Economy.Money(_host) == num + 1000, num + " -> " + Economy.Money(_host));
			City city = Hud.CheatTown(_host);
			int num2 = Rep.Points(_host, city);
			H.Cheat(_host, 2);
			Check("the reputation cheat", city == null || Rep.Points(_host, city) > num2, num2 + " -> " + Rep.Points(_host, city));
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

		private IEnumerator Traits()
		{
			ActorTrait trait = AssetManager.traits.get("strong");
			if (trait == null || _host.hasTrait(trait))
			{
				trait = AssetManager.traits.get("fast");
			}
			float before = ((BaseSimObject)_host).stats["damage"];
			H.ToggleTrait(_host, trait);
			yield return CivilTest.Wait(0.5f);
			Check("a trait given", _host.hasTrait(trait), trait?.id + ", damage " + before + " -> " + ((BaseSimObject)_host).stats["damage"]);
			Menu.Shelf = 3;
			yield return Shot("char_traits.png");
			H.ToggleTrait(_host, trait);
			yield return CivilTest.Wait(0.3f);
			Check("and taken away again", !_host.hasTrait(trait));
			Mod.SetSettingsMenu(open: false);
		}

		private IEnumerator Family()
		{
			Actor lover = null;
			foreach (Actor our in _ours)
			{
				if (our != _host && our.isAlive() && our.isAdult() && !our.hasLover() && !our.isKing() && !our.isCityLeader() && our.data.sex != _host.data.sex)
				{
					lover = our;
					break;
				}
			}
			if (lover == null)
			{
				foreach (Actor our2 in _ours)
				{
					if (our2 != _host && our2.isAlive() && !our2.isKing() && !our2.isCityLeader())
					{
						lover = our2;
						break;
					}
				}
			}
			try
			{
				_host.becomeLoversWith(lover);
			}
			catch (Exception ex)
			{
				Note("lovers: " + ex.Message);
			}
			List<Actor> kids = new List<Actor>();
			for (int i = 0; i < 2; i++)
			{
				Actor actor = CivilTest.SpawnNear("human", _host.current_position + new Vector2(3f, (float)i * 2f), 0f, adult: false);
				if (actor != null)
				{
					actor.data.parent_id_1 = _host.data.id;
					actor.data.parent_id_2 = lover?.data.id ?? (-1);
					try
					{
						actor.joinCity(_host.city);
					}
					catch
					{
					}
					kids.Add(actor);
				}
			}
			Check("a lover and two children", _host.lover == lover && lover != null && kids.Count == 2, $"lover {Who(lover)}, {kids.Count} kids");
			yield return CivilTest.Wait(0.5f);
			Mod.Settings.FamilyFollows = true;
			yield return CivilTest.Wait(1.5f);
			Check("all three follow you", FamilyFollow.Members.Count == 3 && FamilyFollow.Members.Contains(lover), string.Format("{0}: {1}", FamilyFollow.Members.Count, string.Join(", ", FamilyFollow.Members.ConvertAll<string>(Who).ToArray())));
			Vector2 start = _host.current_position;
			Vector2 current_position = start;
			for (int j = 0; j < 16; j++)
			{
				float f = (float)j * MathF.PI / 8f;
				Vector2 vector = start + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * 14f;
				WorldTile tile = World.world.GetTile((int)vector.x, (int)vector.y);
				WorldTile tile2 = World.world.GetTile((int)start.x, (int)start.y);
				if (tile != null && Royal.Standable(tile) && tile.building == null && tile2 != null && tile.isSameIsland(tile2))
				{
					current_position = new Vector2((float)(int)vector.x + 0.5f, (float)(int)vector.y + 0.5f);
					break;
				}
			}
			_host.current_position = current_position;
			_host.dirty_current_tile = true;
			float num = 0f;
			for (float t = 0f; t < 20f; t += 1f)
			{
				yield return CivilTest.Wait(1f);
				num = 0f;
				foreach (Actor member in FamilyFollow.Members)
				{
					num = Mathf.Max(num, (member.current_position - _host.current_position).magnitude);
				}
				Note(string.Format("t {0:0}s: farthest of the family {1:F1} tiles, tasks {2}", t + 1f, num, string.Join(",", FamilyFollow.Members.ConvertAll((Actor a) => ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.id ?? "-").ToArray())));
				if (num < 4.5f)
				{
					break;
				}
			}
			Check("after you walk 14 tiles off, they catch up", num < 4.5f, $"farthest {num:F1} tiles");
			Mod.Settings.Markers = 2;
			float facing = Mod.ViewYaw;
			Mod.SetYaw(facing + MathF.PI);
			Mod.SetPitchDegrees(-12f);
			yield return CivilTest.Wait(0.6f);
			yield return Shot("char_family.png");
			Mod.SetYaw(facing);
			Mod.Settings.Markers = 0;
			City home = _host.city;
			bool left = Belong.Leave(_host);
			yield return CivilTest.Wait(1.2f);
			Check("you leave your town", left && _host.city == null);
			Check("your family leaves it with you", FamilyFollow.Members.TrueForAll((Actor a) => a.city == null && a.kingdom == _host.kingdom), string.Join(", ", FamilyFollow.Members.ConvertAll((Actor a) => Who(a) + " of " + (a.city?.name ?? "no town") + "/" + (a.kingdom?.name ?? "-")).ToArray()));
			City there = _kingB.city;
			bool joined = Belong.Join(_host, there);
			yield return CivilTest.Wait(1.2f);
			Check("you join another kingdom's town", joined && _host.city == there);
			Check("your family joins it with you", FamilyFollow.Members.TrueForAll((Actor a) => a.city == there), string.Join(", ", FamilyFollow.Members.ConvertAll((Actor a) => Who(a) + " of " + (a.city?.name ?? "no town")).ToArray()));
			Mod.Settings.FamilyFollows = false;
			yield return CivilTest.Wait(1f);
			bool flag = true;
			foreach (Actor item in kids)
			{
				if (((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)item.ai)?.task?.id == "fp_family_follow" || ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)item.ai)?.task?.id == "fp_family_run")
				{
					flag = false;
				}
			}
			Check("off: they're let go", flag && FamilyFollow.Members.TrueForAll((Actor a) => FamilyFollow.OrderOf(a) == FamilyFollow.Orders.Free));
			Belong.Join(_host, home);
			_host.current_position = start;
			_host.dirty_current_tile = true;
			yield return CivilTest.Wait(0.5f);
		}

		private IEnumerator FamilyMapPart()
		{
			List<Actor> fam = new List<Actor>(FamilyFollow.Members);
			Check("the family is known", fam.Count >= 3, $"{fam.Count}");
			if (fam.Count == 0)
			{
				yield break;
			}
			Vector2 me = _host.current_position;
			for (int i = 0; i < fam.Count; i++)
			{
				try
				{
					fam[i].cancelAllBeh();
				}
				catch
				{
				}
				fam[i].current_position = me + new Vector2(Mathf.Cos((float)i * 2.1f), Mathf.Sin((float)i * 2.1f)) * 5f;
				fam[i].dirty_current_tile = true;
			}
			yield return CivilTest.Wait(0.5f);
			Mod.FamilyMapKeyForTest();
			yield return CivilTest.Wait(0.6f);
			Check("the Family tab opens the family map", Mod.FamilyMap.Open);
			yield return Shot("char_familymap.png");
			FamilyFollow.Give(_host, null, FamilyFollow.Orders.Follow, null);
			yield return CivilTest.Wait(1.5f);
			Check("Follow me: all follow", fam.TrueForAll((Actor a) => FamilyFollow.OrderOf(a) == FamilyFollow.Orders.Follow) && fam.Exists(FamilyFollow.OnOurTask));
			Actor kid = fam[fam.Count - 1];
			WorldTile spot = null;
			for (int num = 0; num < 16; num++)
			{
				if (spot != null)
				{
					break;
				}
				Vector2 vector = me + new Vector2(Mathf.Cos((float)num * 0.4f), Mathf.Sin((float)num * 0.4f)) * 9f;
				WorldTile tile = World.world.GetTile((int)vector.x, (int)vector.y);
				if (tile != null && Royal.Standable(tile) && tile.building == null && kid.current_tile != null && tile.isSameIsland(kid.current_tile))
				{
					spot = tile;
				}
			}
			Mod.FamilyMap.Picked.Clear();
			Mod.FamilyMap.Picked.Add(kid);
			FamilyFollow.Give(_host, Mod.FamilyMap.Whom(), FamilyFollow.Orders.Wait, spot);
			float num2 = 99f;
			for (float t = 0f; t < 15f; t += 1f)
			{
				if (spot == null)
				{
					break;
				}
				yield return CivilTest.Wait(1f);
				num2 = (kid.current_position - new Vector2((float)spot.x + 0.5f, (float)spot.y + 0.5f)).magnitude;
				if (num2 < 3f)
				{
					break;
				}
			}
			Check("Wait there: the picked one goes and waits", spot != null && num2 < 3f && FamilyFollow.OrderOf(kid) == FamilyFollow.Orders.Wait && FamilyFollow.OrderOf(fam[0]) == FamilyFollow.Orders.Follow, $"{num2:F1} tiles from the spot");
			yield return Shot("char_familymap_wait.png");
			FamilyFollow.Give(_host, Mod.FamilyMap.Whom(), FamilyFollow.Orders.Follow, null);
			for (float t = 0f; t < 10f; t += 0.5f)
			{
				if (!((kid.current_position - _host.current_position).magnitude > 3f))
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			int num3 = 0;
			try
			{
				kid.setNutrition(3);
				num3 = kid.getNutrition();
				_host.addToInventory("berries", 3);
			}
			catch (Exception ex)
			{
				Note("food: " + ex.Message);
			}
			string text = FamilyFollow.ShareFood(_host, Mod.FamilyMap.Whom());
			Note("share: " + text);
			Check("Share food: the hungry child eats from your bag", kid.getNutrition() > num3, $"{num3} -> {kid.getNutrition()}");
			Mod.FamilyMap.Picked.Clear();
			FamilyFollow.Give(_host, null, FamilyFollow.Orders.Free, null);
			yield return CivilTest.Wait(1f);
			Check("Live your lives: let go", fam.TrueForAll((Actor a) => !FamilyFollow.OnOurTask(a)));
			FamilyFollow.Give(_host, null, FamilyFollow.Orders.Follow, null);
			yield return CivilTest.Wait(1f);
			Belong.Leave(_host);
			yield return CivilTest.Wait(1.5f);
			yield return new WaitForEndOfFrame();
			Check("they left town with you", fam.TrueForAll((Actor a) => a.city == null), string.Join(", ", fam.ConvertAll((Actor a) => Who(a) + " of " + (a.city?.name ?? "-")).ToArray()));
			bool flag = false;
			for (int tries = 0; tries < 40; tries++)
			{
				if (flag)
				{
					break;
				}
				WorldTile tile2 = World.world.GetTile(UnityEngine.Random.Range(10, MapBox.width - 10), UnityEngine.Random.Range(10, MapBox.height - 10));
				if (tile2 != null && Royal.Standable(tile2) && _host.current_tile != null && tile2.isSameIsland(_host.current_tile))
				{
					_host.current_position = new Vector2((float)tile2.x + 0.5f, (float)tile2.y + 0.5f);
					_host.dirty_current_tile = true;
					yield return null;
					yield return null;
					flag = FamilyFollow.CanSettleHere(_host);
				}
			}
			Check("free land found to settle", flag);
			if (!flag)
			{
				yield break;
			}
			foreach (Actor item in fam)
			{
				item.current_position = _host.current_position + new Vector2(1.5f, 0f);
				item.dirty_current_tile = true;
			}
			Mod.FamilyMap.Fit(_host);
			yield return CivilTest.Wait(0.5f);
			yield return Shot("char_familymap_nomads.png");
			City founded = FamilyFollow.SettleHere(_host, Mod.Toast);
			yield return CivilTest.Wait(1.5f);
			Check("Settle here: you found a village", founded != null && _host.city == founded, founded?.name);
			Check("and your family lives in it", founded != null && fam.TrueForAll((Actor a) => a.city == founded), string.Join(", ", fam.ConvertAll((Actor a) => Who(a) + " of " + (a.city?.name ?? "-")).ToArray()));
			Mod.FamilyMap.Fit(_host);
			yield return CivilTest.Wait(1f);
			yield return Shot("char_familymap_village.png");
			Mod.FamilyMap.Close();
		}

		private Actor Townsman()
		{
			foreach (Actor our in _ours)
			{
				if (our != null && our != _host && our.isAlive() && our.kingdom == _host.kingdom && !FamilyFollow.Members.Contains(our) && (our.current_position - _host.current_position).magnitude < 8f)
				{
					return our;
				}
			}
			return null;
		}

		private static string Who(Actor a)
		{
			try
			{
				return (a == null) ? "nobody" : a.getName();
			}
			catch
			{
				return "?";
			}
		}

		private IEnumerator War()
		{
			Kingdom a = _kingA.kingdom;
			Kingdom b = _kingB.kingdom;
			try
			{
				World.world.diplomacy.startWar(b, a, WarTypeLibrary.normal, pLog: false);
			}
			catch (Exception ex)
			{
				Log.Error("CHAR war: " + ex.Message);
			}
			yield return CivilTest.Wait(0.5f);
			Check("the kingdoms are at war", a.isEnemy(b));
			Vector2 current_position = _host.current_position;
			float viewYaw = Mod.ViewYaw;
			for (int i = 0; i < 3 && i < _theirs.Count; i++)
			{
				Actor actor = _theirs[i];
				try
				{
					actor.cancelAllBeh();
				}
				catch
				{
				}
				actor.current_position = current_position + new Vector2(Mathf.Cos(viewYaw + (float)(i - 1) * 0.35f), Mathf.Sin(viewYaw + (float)(i - 1) * 0.35f)) * (7f + (float)i);
				actor.dirty_current_tile = true;
			}
			List<Actor> list = _ours.FindAll((Actor x) => x != null && x != _host && x.isAlive() && x.kingdom == _host.kingdom && !FamilyFollow.Members.Contains(x));
			for (int num = 1; num < 4 && num - 1 < list.Count; num++)
			{
				Actor actor2 = list[num - 1];
				try
				{
					actor2.cancelAllBeh();
				}
				catch
				{
				}
				actor2.current_position = current_position + new Vector2(Mathf.Cos(viewYaw + (float)(num - 2) * 0.5f), Mathf.Sin(viewYaw + (float)(num - 2) * 0.5f)) * 3.5f;
				actor2.dirty_current_tile = true;
			}
			yield return CivilTest.Wait(0.6f);
			Check("a foe of the warring kingdom counts as one", _theirs.Count > 0 && FriendlyFire.Foe(_host, _theirs[0]));
			Actor actor3 = Townsman();
			Check("a townsman counts as a friend, not a foe", actor3 != null && FriendlyFire.Friendly(_host, actor3) && !FriendlyFire.Foe(_host, actor3), Who(actor3));
		}

		private IEnumerator Fire()
		{
			bool flag = FriendlyFire.InBattle(_host);
			Actor a = Townsman();
			Mod.Settings.FriendlyFire = false;
			bool flag2 = FriendlyFire.Holds(_host, a);
			Mod.Settings.FriendlyFire = true;
			bool flag3 = FriendlyFire.Holds(_host, a);
			Check("off: in a battle your aim looks past a friend", !flag || flag2, $"battle {flag}");
			Check("on: friends are fair game", !flag3);
			Mod.Settings.FriendlyFire = _saved.FriendlyFire;
			yield break;
		}

		private IEnumerator Markers()
		{
			Mod.Settings.Markers = 2;
			Mod.SetPitchDegrees(-5f);
			yield return CivilTest.Wait(1f);
			Note($"marks: {H.MarksFoe} foes, {H.MarksFriend} friends");
			Check("red marks over foes in view", H.MarksFoe > 0, $"{H.MarksFoe}");
			Check("blue marks over friends in view", H.MarksFriend > 0, $"{H.MarksFriend}");
			yield return Shot("char_markers.png");
			Mod.Settings.Markers = 1;
			yield return CivilTest.Wait(0.5f);
			Check("Foes only: no friends marked", H.MarksFriend == 0 && H.MarksFoe > 0, $"{H.MarksFoe} / {H.MarksFriend}");
			Mod.Settings.Markers = 0;
			yield return CivilTest.Wait(0.5f);
			Check("Off: none", H.MarksFriend == 0 && H.MarksFoe == 0);
		}
	}
}
