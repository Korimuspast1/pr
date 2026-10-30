using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class ToolWorkTest : CivilTest
	{
		private Actor _host;

		private Vector2 _home;

		private float _yaw;

		private WorldTile _spot;

		private float _chopTime;

		protected override string Prefix => "TOOLS";

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			if (!Check("open land", worldTile != null))
			{
				yield break;
			}
			CivilTest.ClearBox(worldTile.x - 6, worldTile.y - 6, worldTile.x + 6, worldTile.y + 6);
			_host = CivilTest.SpawnNear("human", new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f));
			if (Check("a villager", _host != null))
			{
				yield return Possess(_host);
				if (Check("first person in them", InControl(_host)))
				{
					_home = _host.current_position;
					_spot = World.world.GetTile((int)_home.x, (int)_home.y + 2);
					_yaw = Mathf.Atan2(_spot.posV3.y - _home.y, _spot.posV3.x - _home.x);
					yield return Part("the rules", Rules());
					yield return Part("a tree", TreeWork());
					yield return Part("a rock", RockWork());
					yield return Part("a rock with the hotbar pickaxe", PickaxeMining());
					Mod.Holstered = false;
					Mod.Work.ForceSwing = false;
				}
			}
		}

		private IEnumerator Rules()
		{
			Building tree = Put(TreeId(), _spot);
			if (!Check("a tree to judge by", tree != null))
			{
				yield break;
			}
			WorldTile tile = World.world.GetTile(_spot.x + 3, _spot.y);
			Building rock = Put("mineral_stone", tile);
			if (!Check("a rock to judge by", rock != null))
			{
				yield break;
			}
			yield return null;
			Rule("a sword", "sword_iron", tree, treeOk: false, rock, rockOk: false);
			Rule("an axe", "axe_iron", tree, treeOk: true, rock, rockOk: true);
			Rule("a hammer", "hammer_iron", tree, treeOk: false, rock, rockOk: false);
			Equip(_host, "sword_iron");
			string text = Work.ToolInHand(_host, holstered: true);
			Check("a sword put away is bare hands: a tree yes, a rock no", text == "hands" && Work.RightTool(tree, text) && !Work.RightTool(rock, text), text);
			Unequip(_host);
			string text2 = Work.ToolInHand(_host, holstered: false);
			Check("nothing held is bare hands: a tree yes, a rock no", text2 == "hands" && Work.RightTool(tree, text2) && !Work.RightTool(rock, text2), text2);
			Check("a pickaxe: a rock yes, a tree no", Work.RightTool(rock, "pickaxe_iron") && !Work.RightTool(tree, "pickaxe_iron"));
			Supplies.Give(_host, "tw_item_pickaxe", 1);
			yield return CivilTest.Wait(0.3f);
			int num = BagOrder.Of(_host).FindIndex((string e) => BagOrder.ResourceOf(e) == "tw_item_pickaxe");
			if (Check("a pickaxe in the bag", num >= 0))
			{
				if (num >= Hotbar.Size(_host))
				{
					BagOrder.Swap(_host, num, 0);
					num = 0;
				}
				Hotbar.Choose(_host, num, wheel: false);
				Mod.Holstered = false;
				yield return CivilTest.Wait(0.3f);
				string text3 = Work.ToolInHand(_host, holstered: false);
				Check("the hotbar pickaxe: a rock yes, a tree no", text3 == "tw_item_pickaxe" && Work.RightTool(rock, text3) && !Work.RightTool(tree, text3), text3);
				string text4 = Work.ToolInHand(_host, holstered: true);
				Check("...put away it's bare hands", text4 == "hands", text4);
				Supplies.Take(_host, "tw_item_pickaxe", 1);
				yield return CivilTest.Wait(0.2f);
			}
			Actor actor = CivilTest.SpawnNear("wolf", _home + new Vector2(-4f, 0f));
			if (actor != null)
			{
				string text5 = Work.ToolInHand(actor, holstered: false);
				Check("a wolf has no hands: neither", text5 == null && !Work.RightTool(tree, text5) && !Work.RightTool(rock, text5), text5 ?? "null");
				CivilTest.Kill(actor);
			}
			Remove(tree);
			Remove(rock);
		}

		private void Rule(string what, string id, Building tree, bool treeOk, Building rock, bool rockOk)
		{
			bool flag = Equip(_host, id);
			string text = Work.ToolInHand(_host, holstered: false);
			Check(what + ": a tree " + (treeOk ? "yes" : "no") + ", a rock " + (rockOk ? "yes" : "no"), flag && text == id && Work.RightTool(tree, text) == treeOk && Work.RightTool(rock, text) == rockOk, text);
		}

		private IEnumerator TreeWork()
		{
			Building tree = Put(TreeId(), _spot);
			if (!Check("a tree two tiles ahead", tree != null))
			{
				yield break;
			}
			Equip(_host, "sword_iron");
			yield return Aim(tree);
			if (Check("the crosshair on it", Mod.Picked == tree, Picked()))
			{
				Check("with a sword it's no work target", Mod.Work.Target == null && Mod.Work.Needs != null && Mod.Work.Needs.Contains("axe"), "needs '" + Mod.Work.Needs + "'");
				Check("...and the prompt says what would do", Mod.Prompt == Mod.Work.Needs, "prompt '" + Mod.Prompt + "'");
				Mod.Work.ForceSwing = true;
				yield return Hold(2f);
				Mod.Work.ForceSwing = false;
				Check("...swinging leaves it standing", tree.isAlive() && !tree.chopped && Mod.Work.Progress == 0f);
				Mod.Holstered = true;
				yield return Hold(0.2f);
				Check("put away (H): bare hands chop it", Mod.Work.Target == tree && Mod.Prompt != null && Mod.Prompt.StartsWith("LMB:Chop"), "prompt '" + Mod.Prompt + "'");
				int wood = Bag("wood");
				yield return Chop(tree);
				float byHand = _chopTime;
				Check("...and it falls", tree.chopped || !tree.isAlive(), $"{byHand:F2} s");
				Check("...giving wood", Bag("wood") > wood, string.Format("wood {0} -> {1}", wood, Bag("wood")));
				Mod.Holstered = false;
				Remove(tree);
				yield return null;
				tree = Put(TreeId(), _spot);
				if (Check("another tree", tree != null))
				{
					Equip(_host, "axe_iron");
					yield return Aim(tree);
					Check("with an axe it's a work target", Mod.Work.Target == tree, Picked());
					yield return Chop(tree);
					Check("...it falls in fewer swings than by hand", (tree.chopped || !tree.isAlive()) && _chopTime < byHand - 0.4f, $"axe {_chopTime:F2} s, hands {byHand:F2} s");
					Remove(tree);
				}
			}
		}

		private IEnumerator RockWork()
		{
			Building rock = Put("mineral_stone", _spot);
			if (Check("a rock two tiles ahead", rock != null))
			{
				Equip(_host, "axe_iron");
				Mod.Holstered = true;
				yield return Aim(rock);
				if (Check("the crosshair on it", Mod.Picked == rock, Picked()))
				{
					Check("bare hands won't mine it", Mod.Work.Target == null && Mod.Work.Needs != null && Mod.Work.Needs.Contains("pickaxe"), "needs '" + Mod.Work.Needs + "'");
					Check("...and the prompt offers to take the axe up", Mod.Prompt != null && Mod.Prompt.Contains("Take it up"), "prompt '" + Mod.Prompt + "'");
					Mod.Holstered = false;
					Equip(_host, "hammer_iron");
					yield return Hold(0.2f);
					Check("a hammer won't mine it", Mod.Work.Target == null && Mod.Work.Needs != null && !Mod.Work.Needs.Contains("|"), "needs '" + Mod.Work.Needs + "', prompt '" + Mod.Prompt + "'");
					Mod.Work.ForceSwing = true;
					yield return Hold(1.5f);
					Mod.Work.ForceSwing = false;
					Check("...swinging leaves it whole", rock.isAlive() && !rock.chopped && Mod.Work.Progress == 0f);
					Equip(_host, "axe_iron");
					yield return Hold(0.2f);
					Check("an axe mines it", Mod.Work.Target == rock && Mod.Prompt != null && Mod.Prompt.StartsWith("LMB:Mine"), "prompt '" + Mod.Prompt + "'");
					int stone = Bag("stone");
					yield return Chop(rock);
					Check("...it gives way, stone in the bag", Bag("stone") > stone, string.Format("{0:F2} s, stone {1} -> {2}", _chopTime, stone, Bag("stone")));
					Remove(rock);
				}
			}
		}

		private IEnumerator PickaxeMining()
		{
			Building rock = Put("mineral_stone", _spot);
			if (!Check("a rock two tiles ahead for the pickaxe", rock != null))
			{
				yield break;
			}
			Unequip(_host);
			Supplies.Give(_host, "tw_item_pickaxe", 1);
			yield return CivilTest.Wait(0.3f);
			int num = BagOrder.Of(_host).FindIndex((string e) => BagOrder.ResourceOf(e) == "tw_item_pickaxe");
			if (!Check("a pickaxe in the bag", num >= 0))
			{
				Remove(rock);
				yield break;
			}
			if (num >= Hotbar.Size(_host))
			{
				BagOrder.Swap(_host, num, 0);
				num = 0;
			}
			Hotbar.Choose(_host, num, wheel: false);
			Mod.Holstered = false;
			yield return Aim(rock);
			if (!Check("the crosshair on it", Mod.Picked == rock, Picked()))
			{
				Remove(rock);
				yield break;
			}
			Check("the pickaxe chosen: the rock is a work target", Mod.Work.Target == rock && Mod.Prompt != null && Mod.Prompt.StartsWith("LMB:Mine"), "prompt '" + Mod.Prompt + "'");
			int stone = Bag("stone");
			int bag = Mod.Work.BagCount;
			float blows = 0f;
			PlayInput.TestPress(KeyCode.Mouse0);
			float t;
			for (t = 0f; t < 10f; t += Time.unscaledDeltaTime)
			{
				if (!rock.isAlive())
				{
					break;
				}
				if (rock.chopped)
				{
					break;
				}
				if (Mod.Work.BagCount != bag)
				{
					break;
				}
				Stay();
				blows = Mathf.Max(blows, Mod.Work.Progress);
				yield return null;
			}
			PlayInput.TestRelease(KeyCode.Mouse0);
			yield return Hold(0.3f);
			Check("the left button held: the rock took blows", blows > 0f || !rock.isAlive() || rock.chopped || Mod.Work.BagCount > bag, $"progress {blows:F2} after {t:F2} s");
			Check("...it gives way, stone in the bag", Bag("stone") > stone, string.Format("stone {0} -> {1}", stone, Bag("stone")));
			Remove(rock);
			Supplies.Take(_host, "tw_item_pickaxe", 1);
			yield return CivilTest.Wait(0.2f);
		}

		private IEnumerator Chop(Building b)
		{
			int bag = Mod.Work.BagCount;
			Mod.Work.ForceSwing = true;
			float t;
			for (t = 0f; t < 10f; t += Time.unscaledDeltaTime)
			{
				if (!b.isAlive())
				{
					break;
				}
				if (b.chopped)
				{
					break;
				}
				if (Mod.Work.BagCount != bag)
				{
					break;
				}
				Stay();
				yield return null;
			}
			Mod.Work.ForceSwing = false;
			_chopTime = t;
			yield return Hold(0.2f);
		}

		private IEnumerator Aim(Building b)
		{
			for (float pitch = -2f; pitch > -45f; pitch -= 3f)
			{
				Mod.SetPitchDegrees(pitch);
				yield return Hold(0.12f);
				if (Mod.Picked == b)
				{
					yield return Hold(0.12f);
					break;
				}
			}
		}

		private IEnumerator Hold(float seconds)
		{
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				Stay();
				yield return null;
			}
		}

		private void Stay()
		{
			if (_host != null && _host.isAlive())
			{
				_host.current_position = _home;
				_host.dirty_current_tile = true;
				Mod.SetYaw(_yaw);
			}
		}

		private string Picked()
		{
			object obj;
			if (!(Mod.Picked is Building building))
			{
				obj = Mod.Picked?.ToString();
				if (obj == null)
				{
					return "nothing";
				}
			}
			else
			{
				obj = building.asset.id;
			}
			return (string)obj;
		}

		private int Bag(string resource)
		{
			foreach (KeyValuePair<string, int> item in Mod.Work.Bag)
			{
				if (item.Key == resource)
				{
					return item.Value;
				}
			}
			return 0;
		}

		private static string TreeId()
		{
			if (AssetManager.buildings.get("oak_tree") != null)
			{
				return "oak_tree";
			}
			foreach (BuildingAsset item in AssetManager.buildings.list)
			{
				if (item.building_type == BuildingType.Building_Tree && item.resources_given != null && item.resources_given.Count > 0)
				{
					return item.id;
				}
			}
			return null;
		}

		private Building Put(string id, WorldTile t)
		{
			if (id == null || t == null)
			{
				return null;
			}
			try
			{
				if (t.building != null)
				{
					t.building.removeBuildingFinal();
				}
				Building building = World.world.buildings.addBuilding(id, t, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				if (building != null)
				{
					Note($"{id} at {t.x},{t.y}");
				}
				return building;
			}
			catch (Exception ex)
			{
				Log.Error(Prefix + " put " + id + ": " + ex.Message);
				return null;
			}
		}

		private static void Remove(Building b)
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

		private bool Equip(Actor unit, string id)
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
				Log.Error(Prefix + " equip " + id + ": " + ex.Message);
				return false;
			}
		}

		private void Unequip(Actor unit)
		{
			try
			{
				ActorEquipmentSlot slot = unit.equipment.getSlot(EquipmentType.Weapon);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				unit.setStatsDirty();
			}
			catch (Exception ex)
			{
				Log.Error(Prefix + " unequip: " + ex.Message);
			}
		}
	}
}
