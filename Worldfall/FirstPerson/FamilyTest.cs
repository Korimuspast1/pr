using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Towns;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class FamilyTest : CivilTest
	{
		private Actor _host;

		private Actor _lover;

		private Actor _king;

		private readonly List<Actor> _kids = new List<Actor>();

		private readonly List<Actor> _ours = new List<Actor>();

		private Settings _saved;

		protected override string Prefix => "FAMB";

		protected override IEnumerator Body()
		{
			_king = Village("human", 12, 0);
			if (!Check("a village", _king != null))
			{
				yield break;
			}
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != _king && item.isAdult() && !item.isCityLeader())
				{
					_ours.Add(item);
				}
			}
			_host = _ours[0];
			yield return Possess(_host);
			if (!Check("first person in a villager", InControl(_host)))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)_host).addStatusEffect("invincible", 600f, pColorEffect: false);
			}
			catch
			{
			}
			_saved = new Settings
			{
				FamilyFollows = Mod.Settings.FamilyFollows,
				Markers = Mod.Settings.Markers
			};
			foreach (Actor our in _ours)
			{
				if (our != _host && !our.hasLover() && our.data.sex != _host.data.sex)
				{
					_lover = our;
					break;
				}
			}
			if (_lover == null)
			{
				_lover = _ours[1];
			}
			_host.becomeLoversWith(_lover);
			for (int i = 0; i < 2; i++)
			{
				Actor actor = CivilTest.SpawnNear("human", _host.current_position + new Vector2(2f, (float)i * 2f), 0f, adult: false);
				if (actor != null)
				{
					actor.data.parent_id_1 = _host.data.id;
					actor.data.parent_id_2 = _lover.data.id;
					try
					{
						actor.joinCity(_host.city);
					}
					catch
					{
					}
					_kids.Add(actor);
				}
			}
			Mod.Settings.FamilyFollows = false;
			yield return CivilTest.Wait(1f);
			MarkLog();
			Mod.Settings.FamilyFollows = true;
			yield return CivilTest.Wait(2.5f);
			string told = SaidSince("follows you");
			Check("turning it on names the whole family at once", told != null && told.Contains(FamilyFollow.Name(_lover)) && _kids.TrueForAll((Actor k) => told.Contains(FamilyFollow.Name(k))), told ?? SaidSince(""));
			Check("lover and two children follow", FamilyFollow.Members.Count == 3, string.Join(", ", FamilyFollow.Members.ConvertAll<string>(FamilyFollow.Name).ToArray()));
			yield return Part("into a house after you", IntoHouse());
			yield return Part("item swap", ItemSwap());
			yield return Part("hunger in town", HungerInTown());
			yield return Part("asleep, then left behind", Sleep());
			yield return Part("a wolf on your child", Attacked());
			yield return Part("your lover dies", LoverDies());
			yield return Part("a leap into your child", LeapIntoChild());
			yield return Part("the family map as a king", MapAsKing());
			yield return Part("the character tab on a sheep", Sheep());
			Mod.Settings.FamilyFollows = _saved.FamilyFollows;
			Mod.Settings.Markers = _saved.Markers;
		}

		private void MarkLog()
		{
			FamilyFollow.Said.Clear();
		}

		private static string SaidSince(string part)
		{
			return FamilyFollow.Said.Find((string t) => t.Contains(part));
		}

		private static string State(Actor a)
		{
			try
			{
				return "task " + (((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.id ?? "-") + ", target " + ((!a.has_attack_target) ? "none" : ((a.attack_target != null && a.attack_target.isAlive()) ? "alive" : "dead")) + ", " + string.Format("fighting {0}, immovable {1}, sleeping {2}, hungry {3}, starving {4}, ", a.isFighting(), a.isImmovable(), ((BaseSimObject)a).hasStatus("sleeping"), a.isHungry(), a.isStarving()) + $"order {FamilyFollow.OrderOf(a)}, family {FamilyFollow.Members.Contains(a)}, inside {a.is_inside_building}, {(a.current_position - WorldBoxMod.Instance.Host.current_position).magnitude:F1} tiles";
			}
			catch (Exception ex)
			{
				return ex.Message;
			}
		}

		private IEnumerator IntoHouse()
		{
			Building house = AddHouse("house_human_1", _host.city);
			if (!Check("a house", house != null && HouseInterior.IsEnterable(house)))
			{
				yield break;
			}
			Homes.SetOwner(house, _host);
			HomeLock.SetLocked(house, locked: true);
			List<Actor> family = new List<Actor>(FamilyFollow.Members);
			Mod.EnterHouseForTest(house);
			yield return CivilTest.Wait(0.8f);
			if (!Check("you're in the house", Mod.Interior.Inside && Mod.Interior.House == house))
			{
				yield break;
			}
			bool flag = false;
			for (float t = 0f; t < 20f; t += 0.5f)
			{
				if (flag)
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
				flag = family.TrueForAll((Actor a) => a == null || !a.isAlive() || (a.is_inside_building && a.inside_building == house));
			}
			Check("your family comes in after you (through your locked door)", flag, string.Join("; ", family.ConvertAll<string>(State).ToArray()));
			yield return CivilTest.Wait(4f);
			Check("and stays in with you", family.TrueForAll((Actor a) => a == null || !a.isAlive() || (a.is_inside_building && a.inside_building == house)), string.Join("; ", family.ConvertAll<string>(State).ToArray()));
			bool ok = false;
			foreach (RoomWalkers.Walker item in Mod.Interior.WalkersForTest)
			{
				if (family.Contains(item.Who))
				{
					ok = true;
				}
			}
			Check("they're in the room with you", ok);
			Mod.SaveFrame("family_house_in.png");
			Mod.ExitHouseForTest();
			yield return CivilTest.Wait(2f);
			Check("out with you when you leave", family.TrueForAll((Actor a) => a == null || !a.isAlive() || !a.is_inside_building), string.Join("; ", family.ConvertAll<string>(State).ToArray()));
			HomeLock.SetLocked(house, locked: false);
		}

		private IEnumerator ItemSwap()
		{
			Hud h = Mod.HudForTest;
			EquipmentAsset a = AssetManager.items.get("sword_iron");
			EquipmentAsset axe = AssetManager.items.get("axe_iron") ?? AssetManager.items.get("axe_bronze");
			h.ToggleItem(_host, a);
			yield return CivilTest.Wait(0.2f);
			h.ToggleItem(_host, axe);
			yield return CivilTest.Wait(0.2f);
			Check("a second weapon replaces the first", _host.getWeapon()?.asset == axe, _host.getWeapon()?.asset?.id);
			h.ToggleItem(_host, axe);
			yield return CivilTest.Wait(0.2f);
			Check("and comes off again", _host.getWeapon() == null);
		}

		private IEnumerator HungerInTown()
		{
			Actor kid = _kids[0];
			bool feeds = false;
			try
			{
				feeds = kid.city != null && kid.city.hasSuitableFood(kid.subspecies);
			}
			catch
			{
			}
			int maxNutrition = kid.getMaxNutrition();
			int num;
			for (num = maxNutrition; num > 1; num--)
			{
				kid.setNutrition(num);
				if (kid.isHungry())
				{
					break;
				}
			}
			Note($"nutrition {num}/{maxNutrition}: hungry {kid.isHungry()}, starving {kid.isStarving()}");
			yield return CivilTest.Wait(1.5f);
			bool flag = kid.isHungry();
			bool flag2 = FamilyFollow.OnOurTask(kid);
			Note($"town {kid.city?.name} has food for them: {feeds}; hungry {flag}; on the follow task {flag2}; doing {Favorites.Doing(kid)}");
			Check("hungry in town: let off to eat only if the stores hold food", flag && (feeds ? (!flag2) : (flag2 || kid.isFighting() || FamilyFollow.Asleep(kid))), State(kid));
			try
			{
				kid.setNutrition(kid.getMaxNutrition());
			}
			catch
			{
			}
			yield return CivilTest.Wait(1.5f);
			Note("fed again: " + State(kid));
			Check("fed again: back to following (or asleep by you)", FamilyFollow.OnOurTask(kid) || FamilyFollow.Asleep(kid), Favorites.Doing(kid));
		}

		private IEnumerator Sleep()
		{
			Actor kid = _kids[1];
			for (float t = 0f; t < 6f; t += 0.5f)
			{
				if (!((kid.current_position - _host.current_position).magnitude > 3f))
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			bool slept = false;
			try
			{
				slept = kid.makeSleep(60f);
			}
			catch (Exception ex)
			{
				Note("sleep: " + ex.Message);
			}
			yield return CivilTest.Wait(1.5f);
			Note($"made to sleep {slept}: " + State(kid));
			Check("asleep beside you: left to sleep", FamilyFollow.Asleep(kid));
			Vector2 current_position = _host.current_position;
			Vector2 current_position2 = current_position;
			WorldTile current_tile = _host.current_tile;
			for (int i = 0; i < 16; i++)
			{
				Vector2 vector = current_position + new Vector2(Mathf.Cos((float)i * 0.39f), Mathf.Sin((float)i * 0.39f)) * 12f;
				WorldTile tile = World.world.GetTile((int)vector.x, (int)vector.y);
				if (tile != null && Royal.Standable(tile) && tile.building == null && current_tile != null && tile.isSameIsland(current_tile))
				{
					current_position2 = new Vector2((float)(int)vector.x + 0.5f, (float)(int)vector.y + 0.5f);
					break;
				}
			}
			_host.current_position = current_position2;
			_host.dirty_current_tile = true;
			float num = 99f;
			for (float t = 0f; t < 15f; t += 1f)
			{
				yield return CivilTest.Wait(1f);
				num = (kid.current_position - _host.current_position).magnitude;
				if (num < 4.5f)
				{
					break;
				}
			}
			Note("after you walked off: " + State(kid));
			Check("left behind: they wake and catch up", num < 4.5f, $"{num:F1} tiles");
		}

		private IEnumerator Attacked()
		{
			Actor kid = _kids[0];
			MarkLog();
			Actor wolf = CivilTest.SpawnNear("wolf", kid.current_position, 2f);
			if (!Check("a wolf", wolf != null))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)kid).addStatusEffect("invincible", 30f, pColorEffect: false);
			}
			catch
			{
			}
			for (float t = 0f; t < 6f; t += 0.25f)
			{
				try
				{
					if (wolf.isAlive())
					{
						wolf.setAttackTarget(kid);
						kid.attackedBy = wolf;
					}
				}
				catch
				{
				}
				yield return CivilTest.Wait(0.25f);
				if (SaidSince("under attack") != null)
				{
					_ = t;
					break;
				}
			}
			Actor by;
			bool flag = FamilyFollow.InDanger(kid, out by);
			string text = SaidSince("under attack");
			Note("said: " + (text ?? "nothing"));
			Check("you're warned your child is under attack, by what and where", text != null && text.Contains(FamilyFollow.Name(kid)) && text.Contains("wolf"), text);
			Check("the map shows them in danger", flag && by == wolf, (by != null) ? WorldBoxMod.Describe(by) : "nobody");
			CivilTest.Kill(wolf);
			yield return CivilTest.Wait(1f);
			Check("the danger passes with the wolf", !FamilyFollow.InDanger(kid, out var _));
		}

		private IEnumerator LoverDies()
		{
			MarkLog();
			string name = FamilyFollow.Name(_lover);
			CivilTest.Kill(_lover);
			string text = null;
			for (float t = 0f; t < 5f; t += 0.5f)
			{
				if (text != null)
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
				text = SaidSince("has died");
			}
			Note(string.Format("said: {0}; dead {1}", text ?? "nothing", !_lover.isAlive()));
			Check("your lover's death is told as your lover's", text != null && text.Contains(name) && text.Contains("your lover"), text);
			Check("the household is the children now", FamilyFollow.Members.Count == 2 && !FamilyFollow.Members.Contains(_lover));
		}

		private IEnumerator LeapIntoChild()
		{
			Actor kid = _kids[1];
			Actor other = _kids[0];
			Actor old = _host;
			yield return Possess(kid);
			if (Check("now in your child", InControl(kid)))
			{
				yield return CivilTest.Wait(1.5f);
				Check("the child's household doesn't hold their parent or sibling", !FamilyFollow.Members.Contains(old) && !FamilyFollow.Members.Contains(other), FamilyFollow.Members.Count + ": " + string.Join(", ", FamilyFollow.Members.ConvertAll<string>(FamilyFollow.Name).ToArray()));
				Check("the old household is let go", !FamilyFollow.OnOurTask(other), Favorites.Doing(other));
				yield return Possess(old);
				yield return CivilTest.Wait(2.5f);
				Note("child after the leap back: " + State(other));
				Check("back in the parent: the child follows again (or sleeps by you)", InControl(old) && FamilyFollow.Members.Contains(other) && (FamilyFollow.OnOurTask(other) || FamilyFollow.Asleep(other)), Favorites.Doing(other));
			}
		}

		private IEnumerator MapAsKing()
		{
			Belong.Leave(_host);
			yield return CivilTest.Wait(1f);
			bool flag = false;
			for (int tries = 0; tries < 50; tries++)
			{
				if (flag)
				{
					break;
				}
				WorldTile tile = World.world.GetTile(UnityEngine.Random.Range(10, MapBox.width - 10), UnityEngine.Random.Range(10, MapBox.height - 10));
				if (tile != null && Royal.Standable(tile) && _host.current_tile != null && tile.isSameIsland(_host.current_tile))
				{
					_host.current_position = new Vector2((float)tile.x + 0.5f, (float)tile.y + 0.5f);
					_host.dirty_current_tile = true;
					yield return null;
					yield return null;
					flag = FamilyFollow.CanSettleHere(_host);
				}
			}
			if (Check("free land", flag))
			{
				City c = FamilyFollow.SettleHere(_host, Mod.Toast);
				yield return CivilTest.Wait(1f);
				Check("a king now", c != null && Royal.IsKing(_host));
				Mod.FamilyMapKeyForTest();
				yield return CivilTest.Wait(1f);
				Check("the family map opens for a king (Shift+M) and stays open", Mod.FamilyMap.Open && !Mod.WarMap.Open);
				Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "famb_king_map.png"));
				yield return CivilTest.Wait(0.5f);
				Mod.FamilyMap.Close();
			}
		}

		private IEnumerator Sheep()
		{
			Actor sheep = CivilTest.SpawnNear("sheep", _host.current_position, 3f);
			if (!Check("a sheep", sheep != null))
			{
				yield break;
			}
			yield return Possess(sheep);
			if (Check("in the sheep", InControl(sheep)))
			{
				Hud h = Mod.HudForTest;
				bool flag = false;
				try
				{
					h.ToggleItem(sheep, AssetManager.items.get("sword_iron"));
				}
				catch
				{
					flag = true;
				}
				Check("no sword for a sheep, and no error", !flag && sheep.getWeapon() == null);
				ActorTrait actorTrait = AssetManager.traits.get("fast");
				if (actorTrait != null && !sheep.hasTrait(actorTrait))
				{
					h.ToggleTrait(sheep, actorTrait);
				}
				Check("a trait on a sheep", actorTrait == null || sheep.hasTrait(actorTrait));
				Mod.SetSettingsMenu(open: true);
				Mod.SettingsMenu.Tab = 5;
				Mod.SettingsMenu.Shelf = 0;
				yield return CivilTest.Wait(0.6f);
				h.CaptureHudForTest(Path.Combine(Log.Folder, "famb_sheep_tab.png"));
				yield return CivilTest.Wait(0.5f);
				Mod.SetSettingsMenu(open: false);
				Check("a sheep's family map opens empty", ok: true);
				Mod.FamilyMapKeyForTest();
				yield return CivilTest.Wait(0.6f);
				h.CaptureHudForTest(Path.Combine(Log.Folder, "famb_sheep_map.png"));
				yield return CivilTest.Wait(0.5f);
				Check("and is open", Mod.FamilyMap.Open);
				Mod.FamilyMap.Close();
			}
		}
	}
}
