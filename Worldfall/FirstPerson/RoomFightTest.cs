using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using FirstPerson.Core;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class RoomFightTest : CivilTest
	{
		private Actor _you;

		private Actor _villager;

		private Actor _veteran;

		private Actor _child;

		private Actor _foe;

		private Actor _witnessOut;

		private Building _house;

		private bool _ffWas;

		protected override string Prefix => "ROOMFIGHT";

		protected override IEnumerator Body()
		{
			WorldTile stage = CivilTest.Stage();
			if (!Check("open land", stage != null))
			{
				yield break;
			}
			CivilTest.ClearBox(stage.x - 10, stage.y - 8, stage.x + 16, stage.y + 8);
			_house = House(stage);
			if (!Check("a house to go into", _house != null && HouseInterior.IsEnterable(_house), (_house != null) ? _house.asset.id : "none built"))
			{
				yield break;
			}
			Vector2 door = new Vector2((float)stage.x + 0.5f, (float)stage.y - 2.5f);
			_you = CivilTest.Spawn("human", door);
			yield return Possess(_you);
			if (Check("first person in a villager", InControl(_you)))
			{
				CrownOffer.AutoAnswer = true;
				_ffWas = Mod.Settings.FriendlyFire;
				Mod.Settings.FriendlyFire = true;
				Mod.SetYaw(MathF.PI / 2f);
				Mod.SetPitchDegrees(0f);
				yield return CivilTest.Wait(1.5f);
				yield return Part("the people at home", Residents(stage));
				yield return Part("in the house: its door's bar", Enter());
				yield return Part("your blow in the room", YourBlow());
				yield return Part("the room fights back, the child runs", Reactions());
				yield return Part("a crime seen by the room only", Witnessed());
				yield return Part("dead in the room: a body, then a bag", Fallen());
				yield return Part("the fight goes out of the door with you", OutTheDoor());
				yield return Part("struck down against a house outdoors: falls clear of it", AgainstTheWall());
				yield return Part("a foe after you comes in after you", FollowsIn(door));
				yield return Part("an arrow loosed in the room", Shot());
				yield return Part("dead in the doorway: the door still yours", DeadInDoorway());
				yield return Part("cut in two in the doorway: the halves stay in", CutInDoorway());
				yield return Part("fire against the walls", Fire());
				Mod.Settings.FriendlyFire = _ffWas;
				CrownOffer.AutoAnswer = false;
				if (Mod.Interior.Inside)
				{
					Mod.LeaveRoomForTest();
				}
			}
		}

		private static Building House(WorldTile at)
		{
			try
			{
				Building building = World.world.buildings.addBuilding("house_human_1", at, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				building?.completeConstruction();
				return building;
			}
			catch (Exception ex)
			{
				Log.Error("ROOMFIGHT house: " + ex.Message);
				return null;
			}
		}

		private void Home(Actor a)
		{
			if (a == null)
			{
				return;
			}
			try
			{
				a.stayInBuilding(_house);
				a.timer_action = 90f;
				a.setMaxHealth();
			}
			catch (Exception ex)
			{
				Note("home: " + ex.Message);
			}
		}

		private IEnumerator Residents(WorldTile stage)
		{
			Vector2 current_position = _house.current_position;
			_villager = CivilTest.Spawn("human", current_position);
			_veteran = CivilTest.Spawn("human", current_position);
			_child = CivilTest.Spawn("human", current_position, adult: false);
			Check("a villager, a veteran and a child", _villager != null && _veteran != null && _child != null);
			try
			{
				_veteran.addTrait("veteran", pRemoveOpposites: true);
			}
			catch
			{
			}
			Check("the veteran is one", _veteran != null && _veteran.hasTrait("veteran"));
			try
			{
				Supplies.Give(_villager, "tw_item_bandage", 2);
			}
			catch (Exception ex)
			{
				Note("bandages: " + ex.Message);
			}
			Home(_villager);
			Home(_veteran);
			Home(_child);
			_witnessOut = CivilTest.Spawn("human", current_position + new Vector2(2.5f, -2.5f));
			try
			{
				_witnessOut.makeWait(60f);
			}
			catch
			{
			}
			yield return CivilTest.Wait(1.2f);
			Check("they're in the house as the game has it", _villager.is_inside_building && _veteran.is_inside_building && _child.is_inside_building);
		}

		private IEnumerator Enter()
		{
			Mod.EnterHouseForTest(_house);
			yield return CivilTest.Wait(1f);
			if (Check("in the house's room", Mod.Interior.Inside && Mod.Interior.House == _house))
			{
				Note(string.Format("room: {0}, {1} people in it", Mod.Interior.Fitted ? "fitted" : "of old", Mod.Interior.WalkersForTest.Count));
				Check("the room is one to fight in", RoomCombat.Active && RoomCombat.Key == _house);
				Check("its people are the room's", RoomCombat.In(_villager) && RoomCombat.In(_veteran) && RoomCombat.In(_child), $"villager {RoomCombat.In(_villager)}, veteran {RoomCombat.In(_veteran)}, child {RoomCombat.In(_child)}");
				Check("your body stands in the world where you stand in the room", (_you.current_position - RoomCombat.ToWorld(Mod.Interior.Eye)).sqrMagnitude < 0.01f, $"body {_you.current_position}, room eye in the world {RoomCombat.ToWorld(Mod.Interior.Eye)}");
				Interior room = Mod.Interior.Room;
				Check("the door has a bar of its own", room.DoorBarPart >= 0, $"part {room.DoorBarPart}");
				Vector2 vector = new Vector2(room.DoorX, room.DoorY);
				Mod.Interior.Eye = vector + (new Vector2(room.SpawnX, room.SpawnY) - vector).normalized * 1.2f;
				Mod.SetYaw(Mathf.Atan2(vector.y - Mod.Interior.Eye.y, vector.x - Mod.Interior.Eye.x));
				yield return CivilTest.Wait(1.5f);
				bool flag = Mod.Interior.DoorOpen > 0.9f;
				bool flag2 = room.Shell != null && (room.Shell.PartRotation == null || room.DoorBarPart >= room.Shell.PartRotation.Length || (room.Shell.PartRotation[room.DoorBarPart].C0.X > 0.999f && room.Shell.PartRotation[room.DoorBarPart].C2.Z > 0.999f));
				Check("the door open, its bar resting by the wall out of the doorway", flag && flag2 && Mod.Interior.DoorBar <= 0f, $"door {Mod.Interior.DoorOpen:F2}, bar {Mod.Interior.DoorBar:F2}");
				Mod.SaveFrame("roomfight_door_open.png");
			}
		}

		private bool FaceInRoom(Actor who, float distance = 0.55f)
		{
			RoomWalkers.Walker walker = null;
			foreach (RoomWalkers.Walker item in Mod.Interior.WalkersForTest)
			{
				if (item.Who == who)
				{
					walker = item;
				}
			}
			if (walker == null)
			{
				return false;
			}
			Vector2 pos = walker.Pos;
			Vector2 vector = Mod.Interior.Eye - pos;
			if (vector.sqrMagnitude < 0.0001f)
			{
				vector = Vector2.right;
			}
			for (int i = 0; i < 12; i++)
			{
				float f = Mathf.Atan2(vector.y, vector.x) + (float)i * MathF.PI / 6f;
				Vector2 vector2 = pos + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * distance;
				if (Mod.Interior.BlockedWhyForTest(vector2) == null)
				{
					Mod.Interior.Eye = vector2;
					Mod.SetYaw(Mathf.Atan2(pos.y - vector2.y, pos.x - vector2.x));
					Mod.SetPitchDegrees(-12f);
					return true;
				}
			}
			Mod.SetYaw(Mathf.Atan2(pos.y - Mod.Interior.Eye.y, pos.x - Mod.Interior.Eye.x));
			return true;
		}

		private void LookAt(Vector2 at, float distance, float pitch)
		{
			for (int i = 0; i < 16; i++)
			{
				float f = (float)i * MathF.PI / 8f;
				Vector2 vector = at + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * distance;
				if (Mod.Interior.BlockedWhyForTest(vector) == null)
				{
					Mod.Interior.Eye = vector;
					Mod.SetYaw(Mathf.Atan2(at.y - vector.y, at.x - vector.x));
					Mod.SetPitchDegrees(pitch);
					break;
				}
			}
		}

		private static float Health(Actor a)
		{
			try
			{
				return (a != null && a.isAlive()) ? ((float)a.getHealth()) : 0f;
			}
			catch
			{
				return 0f;
			}
		}

		private IEnumerator YourBlow()
		{
			if (Mod.Interior.Inside)
			{
				FaceInRoom(_villager);
				yield return CivilTest.Wait(0.4f);
				FaceInRoom(_villager);
				yield return null;
				Check("your aim is the room's", FirstPersonAim.Ready(_you) && FirstPersonAim.Terrain == Mod.Interior.Room.Floor);
				float before = Health(_villager);
				int blows = PossessionCombat.Blows;
				FriendlyFire.Trace = new List<KeyValuePair<BaseSimObject, string>>();
				PossessionCombat.AttackForTest(_you);
				yield return null;
				yield return null;
				float num = Health(_villager);
				string arg = string.Join(", ", FriendlyFire.Trace.ConvertAll((KeyValuePair<BaseSimObject, string> kv) => ((kv.Key == _villager) ? "villager " : "someone ") + kv.Value).ToArray());
				FriendlyFire.Trace = null;
				Check("your blow is struck at the one you face", PossessionCombat.Blows > blows && PossessionCombat.LastBlowAt == _villager, "at " + ((PossessionCombat.LastBlowAt != null) ? WorldBoxMod.Describe(PossessionCombat.LastBlowAt) : "no one"));
				Check("it lands (the game's own blow, in a room)", num < before, $"health {before:F0} -> {num:F0}; {arg}");
			}
		}

		private IEnumerator Reactions()
		{
			if (!Mod.Interior.Inside)
			{
				yield break;
			}
			yield return CivilTest.Wait(0.3f);
			Check("the one you struck fights you", RoomCombat.Fighting(_villager) && RoomCombat.TargetOf(_villager) == _you, $"fighting {RoomCombat.Fighting(_villager)}, fleeing {RoomCombat.Fleeing(_villager)}");
			Check("the veteran fights you", RoomCombat.Fighting(_veteran) && RoomCombat.TargetOf(_veteran) == _you, $"fighting {RoomCombat.Fighting(_veteran)}, fleeing {RoomCombat.Fleeing(_veteran)}");
			Check("the child runs for the door", RoomCombat.Fleeing(_child) || !RoomCombat.In(_child), $"fleeing {RoomCombat.Fleeing(_child)}");
			int blows = RoomCombat.Blows;
			int wound = WindUps.Begun;
			float hurt = 0f;
			bool framed = false;
			for (float t = 0f; t < 8f; t += 0.1f)
			{
				if (RoomCombat.Blows - blows >= 2)
				{
					break;
				}
				float h = Health(_you);
				yield return CivilTest.Wait(0.1f);
				float num = Health(_you);
				if (num < h)
				{
					hurt += h - num;
				}
				try
				{
					_you.setMaxHealth();
					_villager.setMaxHealth();
					_veteran.setMaxHealth();
				}
				catch
				{
				}
				if (!framed && (WindUps.Winding(_veteran) || WindUps.Winding(_villager)))
				{
					framed = true;
					Mod.SaveFrame("roomfight_windup.png");
				}
			}
			Check("they strike you in the room", RoomCombat.Blows - blows >= 1, $"{RoomCombat.Blows - blows} blows");
			Check("their blows were wound up first (as outdoors)", WindUps.Begun > wound, $"{WindUps.Begun - wound} wind-ups");
			Check("their blows land on you", hurt > 0f, $"{hurt:F0} hurt");
			for (float t = 0f; t < 6f; t += 0.2f)
			{
				if (!RoomCombat.In(_child))
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
			}
			Check("the child is out of the door", !RoomCombat.In(_child) && !_child.is_inside_building && RoomCombat.Fled > 0, $"in the room {RoomCombat.In(_child)}, inside {_child.is_inside_building}, fled {RoomCombat.Fled}");
		}

		private IEnumerator Witnessed()
		{
			if (Mod.Interior.Inside)
			{
				List<Actor> list = Law.Witnesses(_villager, _you, _villager.current_position);
				bool flag = list.Contains(_veteran);
				bool flag2 = list.Contains(_witnessOut);
				Check("the room sees it, the street doesn't", flag && !flag2, $"{list.Count} saw: veteran {flag}, the street {flag2}");
				yield return null;
			}
		}

		private IEnumerator Fallen()
		{
			if (!Mod.Interior.Inside)
			{
				yield break;
			}
			try
			{
				_villager.setHealth(1);
			}
			catch
			{
			}
			for (int i = 0; i < 12; i++)
			{
				if (!_villager.isAlive())
				{
					break;
				}
				FaceInRoom(_villager);
				yield return CivilTest.Wait(0.15f);
				try
				{
					_you.setMaxHealth();
					_villager.setHealth(1);
				}
				catch
				{
				}
				PossessionCombat.AttackForTest(_you);
				yield return CivilTest.Wait(0.35f);
			}
			if (!Check("the villager is dead", !_villager.isAlive()))
			{
				yield break;
			}
			yield return CivilTest.Wait(0.6f);
			Loot.Fallen body = null;
			foreach (Loot.Fallen body2 in Loot.Bodies)
			{
				if (body2.Space == _house)
				{
					body = body2;
				}
			}
			Check("their body lies in the room, to be searched", body != null, $"{Loot.Bodies.Count} bodies anywhere");
			if (body != null)
			{
				LookAt(RoomCombat.ToRoom(body.At), 1.1f, -32f);
				yield return CivilTest.Wait(0.3f);
			}
			Mod.SaveFrame("roomfight_body.png");
			if (body != null)
			{
				body.Age = body.Lie + 2.3f;
				yield return CivilTest.Wait(0.3f);
				Check("faded, it leaves a bag", body.Sack && Loot.Bodies.Contains(body), $"sack {body.Sack}");
				LookAt(RoomCombat.ToRoom(body.At), 1.1f, -32f);
				yield return CivilTest.Wait(0.5f);
				Mod.SaveFrame("roomfight_bag.png");
				LookAt(RoomCombat.ToRoom(body.At), 0.45f, -40f);
				yield return CivilTest.Wait(0.4f);
				Check("E opens it", Mod.Prompt != null && Mod.Prompt.Contains("bag"), Mod.Prompt ?? "no prompt");
				int num = Supplies.Count(_you, "tw_item_bandage");
				Loot.Take(_you, body, null);
				Check("what was in it is yours", Supplies.Count(_you, "tw_item_bandage") > num, string.Format("bandages {0} -> {1}", num, Supplies.Count(_you, "tw_item_bandage")));
			}
		}

		private IEnumerator OutTheDoor()
		{
			if (!Mod.Interior.Inside || _veteran == null || !_veteran.isAlive())
			{
				Check("the veteran still fighting you", ok: false);
				yield break;
			}
			Check("the veteran still fights you", RoomCombat.Fighting(_veteran));
			int out0 = RoomCombat.WentOut;
			Mod.LeaveRoomForTest();
			yield return CivilTest.Wait(0.5f);
			Check("out of the house", !Mod.Interior.Inside);
			Check("the veteran came out after you", !_veteran.is_inside_building && RoomCombat.WentOut > out0, $"inside {_veteran.is_inside_building}, out {RoomCombat.WentOut - out0}");
			bool ok = false;
			string detail;
			try
			{
				ok = _veteran.has_attack_target && _veteran.attack_target == _you;
				detail = string.Format("alive {0}, target {1}, task {2}, {3:F1} tiles off", _veteran.isAlive(), _veteran.has_attack_target ? WorldBoxMod.Describe(_veteran.attack_target.a) : "none", ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)_veteran.ai)?.task?.id, (_veteran.current_position - _you.current_position).magnitude);
			}
			catch (Exception ex)
			{
				detail = ex.Message;
			}
			Check("and fights you outdoors", ok, detail);
			CivilTest.Kill(_veteran);
			yield return CivilTest.Wait(0.3f);
		}

		private IEnumerator AgainstTheWall()
		{
			yield return null;
			int num = -1;
			for (int i = 0; i < Colliders.Near.Count; i++)
			{
				if (Colliders.Near[i].Owner == _house)
				{
					num = i;
				}
			}
			if (!Check("the house's outline is about you", num >= 0, $"{Colliders.Near.Count} colliders"))
			{
				yield break;
			}
			Collider c = Colliders.Near[num];
			Vector2 wall = new Vector2(c.X, c.Y);
			for (int j = 0; j < 200; j++)
			{
				if (!(Colliders.Distance(c, wall, out var _) < 0.22f))
				{
					break;
				}
				wall.x += 0.05f;
			}
			Actor victim = CivilTest.Spawn("human", wall);
			if (!Check("someone with their back to its wall", victim != null))
			{
				yield break;
			}
			_you.current_position = wall + new Vector2(1.2f, 0f);
			Mod.SetYaw(MathF.PI);
			Mod.SetPitchDegrees(-8f);
			for (int k = 0; k < 6; k++)
			{
				try
				{
					victim.current_position = wall;
					victim.makeWait(6f);
					victim.attackedBy = _you;
					_you.setMaxHealth();
				}
				catch
				{
				}
				yield return null;
			}
			int lain = Corpses.Count;
			CivilTest.Kill(victim);
			yield return CivilTest.Wait(0.5f);
			Vector2 lastAwayForTest = Corpses.LastAwayForTest;
			Check("they fell", Corpses.Count > lain, $"bodies {lain} -> {Corpses.Count}");
			Check("clear of the house, not into its wall", lastAwayForTest.x > -0.3f, $"fell {lastAwayForTest.x:F2},{lastAwayForTest.y:F2}, the wall {wall.x:F2},{wall.y:F2}");
			Mod.SaveFrame("roomfight_against_wall.png");
		}

		private IEnumerator FollowsIn(Vector2 door)
		{
			string[] array = new string[3] { "bandit", "skeleton", "orc" };
			foreach (string id in array)
			{
				_foe = CivilTest.Spawn(id, _you.current_position + new Vector2(1.6f, 0f));
				bool flag = false;
				try
				{
					flag = _foe != null && _foe.areFoes(_you);
				}
				catch
				{
				}
				if (flag)
				{
					break;
				}
				CivilTest.Kill(_foe);
				_foe = null;
			}
			if (!Check("a foe by you outdoors", _foe != null))
			{
				yield break;
			}
			try
			{
				_foe.setMaxHealth();
				_foe.addAggro(_you);
				_foe.startFightingWith(_you);
			}
			catch
			{
			}
			yield return CivilTest.Wait(1f);
			bool flag2 = false;
			try
			{
				flag2 = _foe.has_attack_target && _foe.attack_target == _you;
			}
			catch
			{
			}
			Note($"the {_foe.asset.id}: set on you {flag2}, noted as after you {RoomCombat.PursuingForTest(_foe)}, {(_foe.current_position - _you.current_position).magnitude:F1} tiles off");
			Mod.EnterHouseForTest(_house);
			int came = RoomCombat.CameIn;
			for (float t = 0f; t < 8f; t += 0.2f)
			{
				if (RoomCombat.In(_foe))
				{
					break;
				}
				try
				{
					_you.setMaxHealth();
				}
				catch
				{
				}
				yield return CivilTest.Wait(0.2f);
			}
			Check("it came in at the door after you", RoomCombat.In(_foe) && RoomCombat.CameIn > came && _foe.is_inside_building, $"in the room {RoomCombat.In(_foe)}, came in {RoomCombat.CameIn - came}");
			Check("and fights you in there", RoomCombat.Fighting(_foe) && RoomCombat.TargetOf(_foe) == _you);
			yield return CivilTest.Wait(1.2f);
			if (RoomCombat.In(_foe))
			{
				FaceInRoom(_foe, 1.2f);
			}
			Mod.SetPitchDegrees(-4f);
			yield return CivilTest.Wait(0.3f);
			Mod.SaveFrame("roomfight_foe_in.png");
		}

		private IEnumerator Shot()
		{
			if (!Mod.Interior.Inside || _foe == null || !_foe.isAlive() || !RoomCombat.In(_foe))
			{
				Check("the foe in the room to shoot", ok: false);
				yield break;
			}
			bool ok = false;
			try
			{
				ActorEquipmentSlot slot = _you.equipment.getSlot(EquipmentType.Weapon);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				EquipmentAsset pItemAsset = AssetManager.items.get("bow_wood");
				Item pItem = World.world.items.generateItem(pItemAsset, _you.kingdom, _you.getName(), 1, _you, 0, pByPlayer: true);
				slot.setItem(pItem, _you);
				_you.setStatsDirty();
				((BaseSimObject)_you).updateStats();
				ok = !_you.hasMeleeAttack();
			}
			catch (Exception ex)
			{
				Note("bow: " + ex.Message);
			}
			if (!Check("a bow in your hands", ok))
			{
				yield break;
			}
			try
			{
				_foe.makeStunned(4f);
				_foe.setMaxHealth();
			}
			catch
			{
			}
			FaceInRoom(_foe, 1.3f);
			yield return CivilTest.Wait(0.5f);
			FaceInRoom(_foe, 1.3f);
			yield return null;
			yield return null;
			if (Hitboxes.Get(_foe, out var box))
			{
				Vec3 eye = FirstPersonAim.Eye;
				float b = Mathf.Sqrt((box.Centre.X - eye.X) * (box.Centre.X - eye.X) + (box.Centre.Y - eye.Y) * (box.Centre.Y - eye.Y));
				Mod.SetYaw(Mathf.Atan2(box.Centre.Y - eye.Y, box.Centre.X - eye.X));
				Mod.SetPitchDegrees(Mathf.Atan2(box.Centre.Z - eye.Z, Mathf.Max(0.1f, b)) * 57.29578f);
			}
			yield return null;
			yield return null;
			int launched = FirstPersonShots.Launched;
			int struck = FirstPersonShots.Struck;
			float before = Health(_foe);
			FirstPersonShots.Trace = true;
			PossessionCombat.AttackForTest(_you);
			for (float t = 0f; t < 2f; t += 0.05f)
			{
				if (FirstPersonShots.Struck != struck)
				{
					break;
				}
				try
				{
					_you.setMaxHealth();
				}
				catch
				{
				}
				yield return CivilTest.Wait(0.05f);
			}
			FirstPersonShots.Trace = false;
			Check("the arrow is loosed in the room's frame", FirstPersonShots.Launched > launched, $"{FirstPersonShots.Launched - launched} loosed");
			Check("it strikes the foe", FirstPersonShots.Struck > struck && FirstPersonShots.LastVictim == _foe, $"ended at {FirstPersonShots.LastWhat} {FirstPersonShots.LastEnd.X:F2},{FirstPersonShots.LastEnd.Y:F2},{FirstPersonShots.LastEnd.Z:F2}");
			Check("and hurts it", Health(_foe) < before, $"health {before:F0} -> {Health(_foe):F0}");
		}

		private IEnumerator DeadInDoorway()
		{
			if (!Mod.Interior.Inside || _foe == null || !_foe.isAlive() || !RoomCombat.In(_foe))
			{
				Check("the foe in the room", ok: false);
				yield break;
			}
			Interior room = Mod.Interior.Room;
			Vector2 inward = new Vector2(0f - room.DoorOutX, 0f - room.DoorOutY);
			if (!Check("the room has a door", inward.sqrMagnitude > 0.0001f))
			{
				yield break;
			}
			inward.Normalize();
			bool flag = room.Fit != null && !float.IsNaN(room.Fit.DoorWallX);
			Vector2 doorway = (flag ? (new Vector2(room.Fit.DoorWallX, room.Fit.DoorWallY) + inward * 0.3f) : new Vector2(room.DoorX, room.DoorY));
			Actor foe = _foe;
			Note(string.Format("doorway at {0:F2},{1:F2} ({2}), before: {3}", doorway.x, doorway.y, flag ? "fitted" : "of old", Mod.Interior.BlockedWhyForTest(doorway) ?? "clear"));
			try
			{
				foe.makeStunned(4f);
				Supplies.Give(foe, "tw_item_bandage", 2);
			}
			catch (Exception ex)
			{
				Note("foe: " + ex.Message);
			}
			Vector2 far = doorway;
			foreach (float[] spot in room.Spots)
			{
				if ((spot.Length < 5 || spot[4] < 0.3f) && (new Vector2(spot[0], spot[1]) - doorway).sqrMagnitude > (far - doorway).sqrMagnitude)
				{
					far = new Vector2(spot[0], spot[1]);
				}
			}
			for (int i = 0; i < 4; i++)
			{
				foreach (RoomWalkers.Walker item in Mod.Interior.WalkersForTest)
				{
					if (item.Who == foe)
					{
						item.Pos = doorway;
						item.Path.Clear();
						item.Speed = 0f;
					}
					else if (item.Bed < 0 && !item.Pinned)
					{
						item.Pos = far;
						item.Path.Clear();
						item.Speed = 0f;
						item.Wait = 10f;
						item.Leaving = false;
					}
				}
				Mod.Interior.Eye = doorway + inward * 1.1f;
				Mod.SetYaw(Mathf.Atan2(0f - inward.y, 0f - inward.x));
				Mod.SetPitchDegrees(-6f);
				try
				{
					_you.setMaxHealth();
				}
				catch
				{
				}
				yield return null;
			}
			Loot.WatchForTest(foe);
			int lain = Corpses.Count;
			CivilTest.Kill(foe);
			yield return CivilTest.Wait(0.5f);
			Check("it fell", !foe.isAlive() && Corpses.Count > lain, $"alive {foe.isAlive()}, bodies {lain} -> {Corpses.Count}");
			bool flag2 = false;
			foreach (RoomWalkers.Walker item2 in Mod.Interior.WalkersForTest)
			{
				if (item2.Who == foe)
				{
					flag2 = true;
				}
			}
			string text = Mod.Interior.BlockedWhyForTest(doorway);
			Check("gone from the room's people, the doorway clear to walk", !flag2 && text != "someone there", string.Format("listed {0}, the doorway: {1}", flag2, text ?? "clear"));
			List<Actor> list = new List<Actor>();
			Check("a killing there is still the room's to judge", RoomCombat.Walkers != null && RoomCombat.Walkers.FellHere(foe) && RoomCombat.Witnesses(foe.kingdom, foe, _you, list));
			Vector2 lastAwayForTest = Corpses.LastAwayForTest;
			Check("it fell into the room, not out through the door", Vector2.Dot(lastAwayForTest, inward) > -0.3f, $"fell {lastAwayForTest.x:F2},{lastAwayForTest.y:F2}, in {inward.x:F2},{inward.y:F2}");
			Check("nor through a wall or the doorway, falling straight out", Vector2.Dot(RoomCombat.FallWay(RoomCombat.Key, doorway, 0f, -inward, 1.4f), inward) > 0.5f);
			Loot.Fallen body = null;
			foreach (Loot.Fallen body2 in Loot.Bodies)
			{
				if (body2.Space == _house && body2.Name != null && (RoomCombat.ToRoom(body2.At) - doorway).magnitude < 0.6f)
				{
					body = body2;
				}
			}
			Check("its body to search, in the doorway", body != null, $"{Loot.Bodies.Count} bodies");
			Mod.SaveFrame("roomfight_doorway_body.png");
			yield return CivilTest.Wait(0.2f);
			Check("looking at the door, E is the door's", Mod.Prompt != null && Mod.Prompt.Contains("door") && !Mod.Prompt.Contains("body"), Mod.Prompt ?? "no prompt");
			Mod.Interior.Eye = doorway + inward * 0.75f;
			Mod.SetPitchDegrees(-45f);
			yield return CivilTest.Wait(0.2f);
			Check("looking down at it, E searches it", body == null || (Mod.Prompt != null && Mod.Prompt.Contains("Search")), Mod.Prompt ?? "no prompt");
			if (body != null)
			{
				body.Age = body.Lie + 2.3f;
				yield return CivilTest.Wait(0.3f);
				Vector2 vector = RoomCombat.ToRoom(body.At);
				Check("its bag lies off the doorway", body.Sack && Vector2.Dot(vector - doorway, inward) > 0.25f, $"sack {body.Sack} at {vector.x:F2},{vector.y:F2}, {Vector2.Dot(vector - doorway, inward):F2} in from where it fell");
				Mod.Interior.Eye = doorway + inward * 1.1f;
				Mod.SetPitchDegrees(-6f);
				yield return CivilTest.Wait(0.2f);
				Check("and the door is still E's", Mod.Prompt != null && Mod.Prompt.Contains("door") && !Mod.Prompt.Contains("bag"), Mod.Prompt ?? "no prompt");
				Mod.SaveFrame("roomfight_doorway_bag.png");
				Loot.Take(_you, body, null);
			}
		}

		private IEnumerator CutInDoorway()
		{
			if (!Mod.Interior.Inside)
			{
				yield break;
			}
			Interior room = Mod.Interior.Room;
			Vector2 inward = new Vector2(0f - room.DoorOutX, 0f - room.DoorOutY).normalized;
			bool fitted = room.Fit != null && !float.IsNaN(room.Fit.DoorWallX);
			Vector2 doorway = (fitted ? (new Vector2(room.Fit.DoorWallX, room.Fit.DoorWallY) + inward * 0.3f) : new Vector2(room.DoorX, room.DoorY));
			Actor man = CivilTest.Spawn("human", _house.current_tile.posV3);
			Home(man);
			RoomWalkers.Walker w = null;
			for (float t = 0f; t < 6f; t += 0.2f)
			{
				if (w != null)
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
				foreach (RoomWalkers.Walker item in Mod.Interior.WalkersForTest)
				{
					if (item.Who == man)
					{
						w = item;
					}
				}
			}
			if (!Check("someone at home comes into the room", w != null))
			{
				yield break;
			}
			for (int i = 0; i < 4; i++)
			{
				w.Pos = doorway;
				w.Path.Clear();
				w.Speed = 0f;
				w.Wait = 5f;
				Mod.Interior.Eye = doorway + inward * 1.1f;
				Mod.SetYaw(Mathf.Atan2(0f - inward.y, 0f - inward.x));
				Mod.SetPitchDegrees(-6f);
				yield return null;
			}
			int cuts = Slicing.Cuts;
			Slicing.MarkForTest(man, -inward, 1);
			CivilTest.Kill(man);
			for (float t = 0f; t < 1f; t += 0.05f)
			{
				if (Slicing.Cuts != cuts)
				{
					break;
				}
				yield return CivilTest.Wait(0.05f);
			}
			if (Check("cut in two", Slicing.Cuts == cuts + 1))
			{
				yield return CivilTest.Wait(1.6f);
				List<KeyValuePair<Vector3, float>> list = new List<KeyValuePair<Vector3, float>>();
				List<bool> bodies = new List<bool>();
				Slicing.HalvesForTest(list, bodies);
				Vector2 vector = (fitted ? new Vector2(room.Fit.DoorWallX, room.Fit.DoorWallY) : (new Vector2(room.DoorX, room.DoorY) - inward * 0.45f));
				float num = -9f;
				StringBuilder stringBuilder = new StringBuilder();
				for (int j = Mathf.Max(0, list.Count - 3); j < list.Count; j++)
				{
					float num2 = Vector2.Dot(new Vector2(list[j].Key.x, list[j].Key.y) - vector, -inward);
					num = Mathf.Max(num, num2);
					stringBuilder.Append($" {num2:F2}");
				}
				Check("the halves (and the blade) lie in the room", num < 0.05f, $"out past the door's wall by:{stringBuilder}");
				Mod.SaveFrame("roomfight_doorway_cut.png");
			}
		}

		private IEnumerator Fire()
		{
			if (!Mod.Interior.Inside)
			{
				Mod.EnterHouseForTest(_house);
			}
			yield return CivilTest.Wait(0.5f);
			if (!Check("in the house again", Mod.Interior.Inside))
			{
				yield break;
			}
			Check("a shot leaving the room ends at its walls", RoomCombat.WallHit(new Vec3(Mod.Interior.Eye.x, Mod.Interior.Eye.y, 0.8f), new Vec3(Mod.Interior.Eye.x + 30f, Mod.Interior.Eye.y, 0.8f), out var at), $"at {at.X:F2},{at.Y:F2}");
			int kindled = RoomCombat.Kindled;
			RoomCombat.FireLanded(_you, "fireball");
			yield return CivilTest.Wait(1f);
			Check("the house catches", RoomCombat.Kindled > kindled && Torches.Burning(_house), $"burning {Torches.Burning(_house)}");
			bool ok = true;
			foreach (RoomWalkers.Walker item in Mod.Interior.WalkersForTest)
			{
				if (item.Who != null && item.Who.isAlive() && !RoomCombat.Fleeing(item.Who) && RoomCombat.TargetOf(item.Who) != _you)
				{
					ok = false;
				}
			}
			Check("those in it run out", ok);
			Mod.SetPitchDegrees(-8f);
			Mod.SetYaw(Mod.ViewYaw + MathF.PI);
			yield return CivilTest.Wait(0.8f);
			Mod.SaveFrame("roomfight_fire.png");
			try
			{
				_house.stopFire();
			}
			catch
			{
			}
			if (Mod.Interior.Inside)
			{
				Mod.LeaveRoomForTest();
			}
			CivilTest.Kill(_foe);
		}
	}
}
