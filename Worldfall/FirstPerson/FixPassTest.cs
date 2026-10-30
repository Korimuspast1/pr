using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class FixPassTest : CivilTest
	{
		private Actor _king;

		private Actor _you;

		private City _city;

		private Building _home;

		protected override string Prefix => "FIXPASS";

		private Hud H => Mod.HudForTest;

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			if (!Check("open land", worldTile != null))
			{
				yield break;
			}
			_king = Village("human", 7, 0, worldTile);
			if (!Check("a village", _king != null && _king.city != null))
			{
				yield break;
			}
			_city = _king.city;
			_you = ((Founded.Count > 1) ? Founded[1] : null);
			if (!Check("a villager to play", _you != null && _you != _king))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)_king).addStatusEffect("invincible", 1200f, pColorEffect: false);
				((BaseSimObject)_you).addStatusEffect("invincible", 1200f, pColorEffect: false);
			}
			catch
			{
			}
			try
			{
				if (_city.countBuildingsType("type_bonfire") == 0)
				{
					BuildingAsset buildingAsset = FirstPerson.Towns.Village.BonfireOf(_king);
					Building building = ((buildingAsset != null) ? AddHouse(buildingAsset.id, _city) : null);
					Note("bonfire " + buildingAsset?.id + ": " + ((building != null) ? "added" : "no land"));
				}
			}
			catch (Exception ex)
			{
				Note("bonfire: " + ex.Message);
			}
			yield return Part("craft", Craft());
			if (!Options.Contains("skipdoor"))
			{
				yield return Part("doors", Doors());
			}
			if (!Options.Contains("skipruler"))
			{
				yield return Part("king", King());
			}
			if (!Options.Contains("skipturn"))
			{
				yield return Part("turn and pace", TurnAndPace());
			}
			if (!Options.Contains("skiprows"))
			{
				yield return Part("rows and streets", RowsAndStreets());
			}
			if (!Options.Contains("skipwpn"))
			{
				yield return Part("weapons", Weapons());
			}
			if (Planning.Aiming)
			{
				Planning.Escape();
			}
			Mod.DebugEyeHeight = 0f;
			Planning.AimForTest = null;
		}

		private IEnumerator Craft()
		{
			Mod.Craft.EnsureRecipes();
			Crafting.Recipe recipe = Mod.Craft.Recipes.Find((Crafting.Recipe r) => r.Id == "hammer_wood");
			Crafting.Recipe recipe2 = Mod.Craft.Recipes.Find((Crafting.Recipe r) => r.Id == "hammer_stone");
			Crafting.Recipe recipe3 = Mod.Craft.Recipes.Find((Crafting.Recipe r) => r.Id == "hammer_iron");
			Check("hammers are recipes", recipe != null && recipe2 != null && recipe3 != null, $"{Mod.Craft.Recipes.Count} recipes");
			Check("...under Tools, first in the list", recipe != null && Crafting.IsTool(recipe) && Mod.Craft.Recipes.Count > 0 && Crafting.IsTool(Mod.Craft.Recipes[0]), (Mod.Craft.Recipes.Count > 0) ? Mod.Craft.Recipes[0].Id : "none");
			Check("...wood and stone made by hand, iron at a workbench", recipe != null && recipe2 != null && recipe3 != null && recipe.Rank == 0 && recipe2.Rank == 0 && recipe3.Rank > 0, $"ranks {recipe?.Rank} {recipe2?.Rank} {recipe3?.Rank}");
			yield return null;
		}

		private IEnumerator Doors()
		{
			_home = AddHouse("house_human_2", _city);
			if (!Check("your house", _home != null))
			{
				yield break;
			}
			Homes.SetOwner(_home, _you);
			yield return Possess(_you);
			if (!Check("first person", InControl(_you)))
			{
				yield break;
			}
			_you.current_position = _home.current_position + new Vector2(0f, -3f);
			for (int i = 0; i < 40; i++)
			{
				if (HouseWorld.Of(_home, out var _))
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			yield return CivilTest.Wait(1f);
			Mod.EnterHouseForTest(_home);
			yield return CivilTest.Wait(1.2f);
			if (Check("in your house, its fitted room", Mod.Interior.Inside && Mod.Interior.House == _home && Mod.Interior.Fitted, Mod.Interior.Name))
			{
				HouseInterior room = Mod.Interior;
				Interior room2 = room.Room;
				Vector2 door = new Vector2(room2.DoorX, room2.DoorY);
				Vector2 vector = new Vector2(room2.SpawnX, room2.SpawnY) - door;
				if (vector.sqrMagnitude < 0.01f)
				{
					vector = -new Vector2(room2.DoorOutX, room2.DoorOutY);
				}
				vector.Normalize();
				Vector2 stand = (room.Eye = door + vector * 1.2f);
				float toDoor = Mathf.Atan2(door.y - stand.y, door.x - stand.x);
				Mod.SetYaw(toDoor + MathF.PI);
				Mod.SetPitchDegrees(-5f);
				yield return Frames(20);
				string prompt = Mod.Prompt ?? "";
				Check("near the door looking away: no Leave on screen", !prompt.Contains("Leave") && !prompt.Contains("door"), "'" + prompt + "'");
				yield return Shot("fixpass_door_away.png");
				Mod.SetYaw(toDoor);
				yield return Frames(20);
				prompt = Mod.Prompt ?? "";
				Check("looking at the door: it's aimed at", room.DoorAimed(Mod.View));
				Check("...its prompt: the door, hold E to leave, Shift+E your Home card", prompt.Contains("the door") && prompt.Contains("Hold E:Leave") && prompt.Contains("Shift+E:Home"), "'" + prompt + "'");
				yield return CivilTest.Wait(1f);
				Check("just come in: the door stands open", room.DoorOpen > 0.9f && prompt.Contains("Close"), $"open {room.DoorOpen:F2}, '{prompt}'");
				yield return Shot("fixpass_door_aim.png");
				yield return TapE();
				yield return CivilTest.Wait(2.5f);
				Check("E closes it, standing right by it", room.DoorOpen < 0.05f && !room.DoorHeldOpen, $"open {room.DoorOpen:F2}");
				yield return CivilTest.Wait(1f);
				Check("...and it stays shut", room.DoorOpen < 0.05f, $"open {room.DoorOpen:F2}");
				Check("...its prompt says open now", (Mod.Prompt ?? "").Contains("E:Open the door"), "'" + Mod.Prompt + "'");
				yield return TapE();
				yield return CivilTest.Wait(2f);
				Check("E opens it", room.DoorOpen > 0.9f && HouseInterior.Propped(_home), $"open {room.DoorOpen:F2}");
				room.Eye = Far(room, door);
				yield return CivilTest.Wait(1.5f);
				Check("...and it stays open from across the room", room.DoorOpen > 0.9f, $"open {room.DoorOpen:F2}, {(room.Eye - door).magnitude:F2} from it");
				room.Eye = stand;
				Mod.SetYaw(toDoor);
				yield return Frames(10);
				yield return TapE();
				yield return CivilTest.Wait(2.5f);
				Check("closed again", room.DoorOpen < 0.05f && !HouseInterior.Propped(_home), $"open {room.DoorOpen:F2}");
				for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime)
				{
					room.Move(1f, 0f, toDoor, Time.unscaledDeltaTime);
					yield return null;
				}
				yield return CivilTest.Wait(0.8f);
				Check("walking into it pushes it open", room.DoorOpen > 0.7f && room.Inside, $"open {room.DoorOpen:F2}, {(room.Eye - door).magnitude:F2} from it");
				for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
				{
					room.Move(1f, 0f, toDoor + MathF.PI, Time.unscaledDeltaTime);
					yield return null;
				}
				yield return CivilTest.Wait(1.4f);
				Check("walking off, it swings shut behind you", room.DoorOpen < 0.05f, $"open {room.DoorOpen:F2}, {(room.Eye - door).magnitude:F2} from it");
				Check("door toggled by hand", HouseInterior.DoorToggles >= 3, $"{HouseInterior.DoorToggles}");
				Mod.ExitHouseForTest();
				yield return CivilTest.Wait(1f);
			}
		}

		private IEnumerator TapE()
		{
			PlayInput.Inject(KeyCode.E, shift: false);
			yield return null;
			yield return null;
			yield return null;
		}

		private static Vector2 Far(HouseInterior room, Vector2 door)
		{
			Interior room2 = room.Room;
			Vector2 result = room.Eye;
			float num = 0f;
			for (float num2 = room2.MinY; num2 < room2.MaxY; num2 += 0.1f)
			{
				for (float num3 = room2.MinX; num3 < room2.MaxX; num3 += 0.1f)
				{
					float magnitude = (new Vector2(num3, num2) - door).magnitude;
					if (magnitude > num && (room2.Fit == null || room2.Fit.Clear(0, num3, num2, 0.3f)))
					{
						num = magnitude;
						result = new Vector2(num3, num2);
					}
				}
			}
			return result;
		}

		private IEnumerator King()
		{
			if (!Check("a king", _king.isKing() && _king.kingdom == _city.kingdom))
			{
				yield break;
			}
			Building house = AddHouse("house_human_1", _city);
			Actor owner = ((house != null) ? CivilTest.SpawnNear("human", house.current_position + new Vector2(0f, -3f)) : null);
			if (!Check("a subject's house", house != null && owner != null))
			{
				yield break;
			}
			try
			{
				owner.joinCity(_city);
				((BaseSimObject)owner).addStatusEffect("invincible", 600f, pColorEffect: false);
			}
			catch
			{
			}
			Homes.SetOwner(house, owner);
			HomeLock.SetLocked(house, locked: true);
			Check("a stranger can't walk into the locked house", !HouseDoors.MayWalkIn(house, _you));
			Check("the king walks into it", HouseDoors.MayWalkIn(house, _king));
			Check("...no door barred to him", HouseInterior.Barred(house, _king) == null);
			Chronicle.ChangeOpinion(owner, _king, -40);
			Voice v = Dialogue.VoiceOf(owner, _king);
			Check("a subject who thinks ill of him still speaks with respect", v.Sovereign && v.Warmth >= 0, $"warmth {v.Warmth}, opinion {Chronicle.Opinion(owner, _king)}");
			Check("...not to a commoner", !Dialogue.VoiceOf(owner, _you).Sovereign);
			string text = Dialogue.Greeting(owner, _king);
			string text2 = Dialogue.Address(v, owner, _king);
			string text3 = Dialogue.Retort(owner, _king, v, fights: false, child: false);
			Check("...greets him by his title", Royal(text), "'" + text + "'");
			Check("...names him by it", Royal(text2), "'" + text2 + "'");
			Check("...an insult answered meekly", !text3.Contains("dare") && !text3.Contains("Who do you think"), "'" + text3 + "'");
			Note("farewell: '" + Dialogue.Farewell(owner, _king) + "'");
			yield return Possess(_king, 5f);
			if (!Check("playing the king", InControl(_king)))
			{
				yield break;
			}
			int num = Law.BountyOn(_city.kingdom, _king);
			Law.BreakIn(house, new List<Actor> { owner }, null);
			Check("forcing a door in his realm is no crime", Law.BountyOn(_city.kingdom, _king) == num, $"bounty {num} -> {Law.BountyOn(_city.kingdom, _king)}");
			_king.current_position = house.current_position + new Vector2(0f, -3f);
			try
			{
				owner.current_position = house.current_position;
				owner.stayInBuilding(house);
			}
			catch (Exception ex)
			{
				Note("indoors: " + ex.Message);
			}
			for (int i = 0; i < 40; i++)
			{
				if (HouseWorld.Of(house, out var _))
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			yield return CivilTest.Wait(1f);
			int welcomes = Mod.RoyalWelcomes;
			Mod.EnterHouseForTest(house);
			Mod.SetPitchDegrees(-6f);
			yield return CivilTest.Wait(0.6f);
			yield return Shot("fixpass_king_welcome.png");
			yield return CivilTest.Wait(0.5f);
			Check("in the subject's house", Mod.Interior.Inside && Mod.Interior.House == house);
			Note($"welcomes said: {Mod.RoyalWelcomes - welcomes} ({HouseOccupancy.Of(house).Inside.Count} in)");
			Mod.ExitHouseForTest();
			yield return CivilTest.Wait(1f);
			HomeLock.SetLocked(house, locked: false);
		}

		private static bool Royal(string s)
		{
			if (s != null)
			{
				if (!s.Contains("Majesty") && !s.Contains("my king") && !s.Contains("my queen") && !s.Contains("sire") && !s.Contains("my lady") && !s.Contains("Sire") && !s.Contains("My king") && !s.Contains("My queen"))
				{
					return s.Contains("My lady");
				}
				return true;
			}
			return false;
		}

		private IEnumerator TurnAndPace()
		{
			if (!InControl(_king))
			{
				yield return Possess(_king, 5f);
			}
			if (!Check("playing the king", InControl(_king)))
			{
				yield break;
			}
			Building store = AddHouse("stockpile", _city);
			for (int i = 0; i < 12; i++)
			{
				if (store == null)
				{
					break;
				}
				if (_city.hasStockpiles())
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			if (store != null)
			{
				string[] array = new string[4] { "wood", "stone", "gold", "common_metals" };
				foreach (string pResourceID in array)
				{
					try
					{
						_city.addResourcesToRandomStockpile(pResourceID, 60);
					}
					catch
					{
					}
				}
			}
			Note(string.Format("stores: {0} wood{1}", _city.getResourcesAmount("wood"), (store == null) ? " (no land for a stockpile)" : ""));
			Vector2 middle = ((_city.getTile() != null) ? ((Vector2)_city.getTile().posV3) : _king.current_position);
			_king.current_position = middle;
			yield return CivilTest.Wait(0.5f);
			Planning.Begin(_king, _city);
			if (!Check("planning in his town", Planning.Aiming && Planning.Town == _city))
			{
				yield break;
			}
			Check("a tent to plan", Planning.PickForTest("order_tent"), "no tent on the bar");
			BuildingAsset tent = Planning.Asset;
			int want = Facing.Next(tent, 0);
			Check("...faces the game's way first", Planning.Face == 0);
			PlayInput.Inject(KeyCode.R, shift: false);
			yield return Frames(3);
			Check("R turns it" + (Facing.Quarter(tent) ? " a quarter" : " end for end (a long footprint)"), Planning.Face == want && want != 0, $"face {Planning.Face}, want {want}");
			WorldTile spot = FreeSpot(tent, middle, 5f, 16f);
			if (!Check("land for the tent", spot != null))
			{
				yield break;
			}
			int num = ((tent.fundament != null) ? (tent.fundament.top / 2) : 0);
			Planning.AimForTest = World.world.GetTile(spot.x, spot.y + num) ?? spot;
			yield return Frames(4);
			Check("the turned ghost is green", Planning.Valid, Planning.Why);
			LookFrom(new Vector2((float)spot.x - 3f, (float)spot.y - 7f), new Vector2((float)spot.x + 0.5f, (float)spot.y + 1f), 4.5f);
			yield return CivilTest.Wait(0.5f);
			yield return Shot("fixpass_ghost_turned.png");
			Mod.DebugEyeSet = false;
			Planning.ClickForTest = true;
			yield return Frames(4);
			Building b = spot.building;
			if (!Check("the plan is placed", b?.isUnderConstruction() ?? false, Planning.Why))
			{
				yield break;
			}
			Check("...facing as the ghost did", Facing.Of(b) == want, $"face {Facing.Of(b)}");
			Planning.AimForTest = null;
			Planning.Escape();
			bool alone = FirstPerson.Towns.Village.PlansAlone(_city);
			int needed = Mathf.Max(1, b.asset.construction_progress_needed);
			Blueprints.Pay(_city, b, _king, out var _);
			Blueprints.Blow(_king, b, 1f);
			BuildHelp.Worked(_king, b);
			Check("your site is the town's site now", _city.under_construction_building == b && BuildHelp.YourSite == b, _city.under_construction_building?.asset?.id ?? "none");
			for (int i = 0; i < 12; i++)
			{
				yield return CivilTest.Wait(0.5f);
				BuildHelp.Worked(_king, b);
			}
			int num2 = 0;
			foreach (Actor unit in _city.units)
			{
				try
				{
					if (unit != null && unit.isAlive() && unit.citizen_job != null && unit.citizen_job.id == "builder")
					{
						num2++;
					}
				}
				catch
				{
				}
			}
			Check("...at most the game's three builders on it", num2 + BuildHelp.Helpers.Count <= 3, $"{num2} builders, {BuildHelp.Helpers.Count} helpers, {BuildHelp.Sent} sent");
			Note($"builders {num2}, helpers {BuildHelp.Helpers.Count}, progress {Blueprints.ProgressOf(b):P0}");
			Equip(_king, "hammer_iron");
			Vector2 site = b.current_position;
			Vector2 stand = site + Facing.Turn(new Vector2(0f, -1f), want) * 3.2f;
			yield return AimAt(b, stand, site);
			float before = Blueprints.ProgressOf(b);
			Mod.Work.ForceSwing = true;
			for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime)
			{
				Stay(stand, site);
				yield return null;
			}
			yield return Shot("fixpass_hammering.png");
			for (float t = 0f; t < 0.6f; t += Time.unscaledDeltaTime)
			{
				Stay(stand, site);
				yield return null;
			}
			Mod.Work.ForceSwing = false;
			Unequip(_king);
			Note($"hammered by hand: progress {before:P0} -> {Blueprints.ProgressOf(b):P0}");
			Mod.DebugEyeSet = false;
			int num3 = 1;
			for (int k = 0; k < 400; k++)
			{
				if (!b.isUnderConstruction())
				{
					break;
				}
				Blueprints.Blow(_king, b, 1f);
				num3++;
			}
			Check("the tent stands", !b.isUnderConstruction() && b.isAlive());
			int num4 = Mathf.CeilToInt((alone ? 2f : 1f) * (float)needed / 8f) + 1;
			Check("...in a quarter of the old blows or fewer", num3 <= num4, string.Format("{0} blows for a need of {1} ({2}; at most {3})", num3, needed, alone ? "held town" : "town builds too", num4));
			_king.current_position = new Vector2((float)spot.x + 0.5f, (float)spot.y + 1f) + Facing.Turn(new Vector2(0f, -1f), want) * 5f;
			LookFrom(new Vector2((float)spot.x + 0.5f, (float)spot.y + 1f) + Facing.Turn(new Vector2(0.45f, -1f), want) * 9f, new Vector2((float)spot.x + 0.5f, (float)spot.y + 1f), 3f);
			float hour = Mod.Day.TimeOfDay;
			Mod.Day.TimeOfDay = 0.68f;
			yield return CivilTest.Wait(1.5f);
			yield return Shot("fixpass_turned.png");
			Mod.Day.TimeOfDay = hour;
			Mod.DebugEyeSet = false;
			Vector2 centre = b.current_position;
			if (HouseWorld.Of(b, out var p))
			{
				Check("drawn turned", p.Face == want, $"face {p.Face}");
				centre = new Vector2(p.X, p.Y);
			}
			else
			{
				Note("(the tent wasn't drawn near you)");
			}
			Vector2 doorDir = Facing.Turn(new Vector2(0f, -1f), want);
			if (Steering.DoorOf(b, out var door))
			{
				Check("its door point is on the turned side", Vector2.Dot(door - centre, doorDir) > 0.3f, $"door {door - centre}, facing {doorDir}");
			}
			else
			{
				Note("(no door point yet)");
			}
			_king.current_position = centre + doorDir * 3f;
			for (int i = 0; i < 40; i++)
			{
				if (HouseWorld.Of(b, out var _))
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			yield return CivilTest.Wait(1f);
			Mod.EnterHouseForTest(b);
			yield return CivilTest.Wait(1.5f);
			if (Check("into the turned tent", Mod.Interior.Inside && Mod.Interior.House == b))
			{
				Interior room = Mod.Interior.Room;
				Vector2 outward = new Vector2(room.DoorOutX, room.DoorOutY);
				Vector2 normalized = outward.normalized;
				if (HouseInterior.WalksIn(b.asset))
				{
					Check("...its room's doorway faces the turned way", Mod.Interior.Fitted && Vector2.Dot(normalized, doorDir) > 0.7f, $"fitted {Mod.Interior.Fitted}, out {normalized}, want {doorDir}");
				}
				else
				{
					Note($"(a tent's room of old: doorway out {normalized}, the tent turned to {doorDir})");
				}
				if (Mod.Interior.DoorwayInWorld(out var middle2, out var _, out var _, out outward))
				{
					Check("...the doorway in the world on that side", Vector2.Dot(new Vector2(middle2.x, middle2.y) - centre, doorDir) > 0.2f, $"doorway {new Vector2(middle2.x, middle2.y) - centre}");
				}
				Mod.ExitHouseForTest();
				yield return CivilTest.Wait(1f);
			}
		}

		private IEnumerator RowsAndStreets()
		{
			if (!InControl(_king))
			{
				yield return Possess(_king, 5f);
			}
			Unequip(_king);
			Planning.Escape();
			Vector2 middle = ((_city.getTile() != null) ? ((Vector2)_city.getTile().posV3) : _king.current_position);
			_king.current_position = middle;
			yield return CivilTest.Wait(0.3f);
			Planning.Begin(_king, _city);
			if (!Check("planning again", Planning.Aiming))
			{
				yield break;
			}
			Planning.PickForTest("order_tent");
			BuildingAsset asset = Planning.Asset;
			WorldTile from = FreeSpot(asset, middle, 6f, 20f);
			if (from != null)
			{
				int w = ((asset.fundament != null) ? (asset.fundament.left + asset.fundament.right + 1) : 3);
				int up = ((asset.fundament != null) ? (asset.fundament.top / 2) : 0);
				Planning.AimForTest = World.world.GetTile(from.x, from.y + up) ?? from;
				yield return Frames(3);
				Planning.BeginDragForTest();
				Planning.DragHeldForTest = true;
				Planning.AimForTest = World.world.GetTile(from.x + 3 * (w + 1), from.y + up) ?? from;
				yield return Frames(4);
				int ok = 0;
				foreach (bool item in Planning.RowOk)
				{
					if (item)
					{
						ok++;
					}
				}
				Check("dragging lays out a row of four", Planning.Dragging && Planning.Row.Count == 4, $"{Planning.Row.Count} in the row, {ok} may stand; '{Planning.Prompt}'");
				Check("...the prompt says how many on release", Planning.Prompt != null && (Planning.Prompt.StartsWith("Release:") || Planning.Prompt.StartsWith("Nowhere")), "'" + Planning.Prompt + "'");
				LookFrom(new Vector2((float)from.x + 1.5f * (float)(w + 1) - 3f, (float)from.y - 8f), new Vector2((float)from.x + 1.5f * (float)(w + 1), (float)from.y + 1f), 6f);
				yield return CivilTest.Wait(0.5f);
				yield return Shot("fixpass_row.png");
				Mod.DebugEyeSet = false;
				int before = Blueprints.CountOf(_city);
				Planning.DragHeldForTest = false;
				yield return Frames(3);
				Check("let go, the row is planned", Blueprints.CountOf(_city) - before == ok && !Planning.Dragging, $"{before} -> {Blueprints.CountOf(_city)} plans, {ok} green");
			}
			else
			{
				Note("(no land for a row)");
			}
			Check("a street on the bar", Planning.PickSpecialForTest(street: true) && Planning.Piece != null && Planning.Piece.Street, Planning.Piece?.Name);
			WorldTile a = null;
			for (int i = 0; i < 200; i++)
			{
				if (a != null)
				{
					break;
				}
				WorldTile tile = World.world.GetTile(Mathf.FloorToInt(middle.x) + i % 20 - 10, Mathf.FloorToInt(middle.y) - 4 - i / 20);
				if (tile != null && tile.zone != null && tile.zone.city == _city && tile.Type != null && tile.Type.ground && !tile.Type.road && !tile.Type.liquid)
				{
					WorldTile tile2 = World.world.GetTile(tile.x + 6, tile.y);
					if (tile2 != null && tile2.zone != null && tile2.zone.city == _city)
					{
						a = tile;
					}
				}
			}
			if (!Check("town land for a street", a != null))
			{
				yield break;
			}
			int waiting = Planning.StreetTilesWaiting;
			Planning.AimForTest = a;
			yield return Frames(3);
			Planning.BeginDragForTest();
			Planning.DragHeldForTest = true;
			Planning.AimForTest = World.world.GetTile(a.x + 6, a.y);
			yield return Frames(4);
			int good = 0;
			foreach (bool item2 in Planning.RowOk)
			{
				if (item2)
				{
					good++;
				}
			}
			Check("a street dragged seven tiles long", Planning.Row.Count == 7 && good > 0, $"{Planning.Row.Count} tiles, {good} may be paved; '{Planning.Prompt}'");
			LookFrom(new Vector2((float)a.x + 3.5f - 2f, (float)a.y - 6f), new Vector2((float)a.x + 3.5f, (float)a.y + 0.5f), 4f);
			yield return CivilTest.Wait(0.5f);
			yield return Shot("fixpass_street.png");
			Mod.DebugEyeSet = false;
			Planning.DragHeldForTest = false;
			yield return Frames(3);
			Check("let go, the road builders have it to pave", Planning.StreetTilesWaiting - waiting == good, $"{waiting} -> {Planning.StreetTilesWaiting}");
			Check("a palisade on the bar", Planning.PickSpecialForTest(street: false, 0) && Planning.Piece != null && Planning.Piece.Wall == 0, Planning.Piece?.Name);
			WorldTile w2 = null;
			for (int j = 0; j < 200; j++)
			{
				if (w2 != null)
				{
					break;
				}
				WorldTile tile3 = World.world.GetTile(Mathf.FloorToInt(middle.x) + j % 20 - 10, Mathf.FloorToInt(middle.y) + 4 + j / 20);
				if (tile3 != null && WallPlans.WhyNotAt(_city, tile3) == null)
				{
					bool flag = true;
					for (int k = 1; k <= 4 && flag; k++)
					{
						flag = WallPlans.WhyNotAt(_city, World.world.GetTile(tile3.x + k, tile3.y)) == null;
					}
					if (flag)
					{
						w2 = tile3;
					}
				}
			}
			if (!Check("town land for a palisade", w2 != null))
			{
				Planning.Escape();
				yield break;
			}
			Planning.AimForTest = w2;
			yield return Frames(3);
			Planning.BeginDragForTest();
			Planning.DragHeldForTest = true;
			Planning.AimForTest = World.world.GetTile(w2.x + 4, w2.y);
			yield return Frames(4);
			int walls = 0;
			foreach (bool item3 in Planning.RowOk)
			{
				if (item3)
				{
					walls++;
				}
			}
			Check("a palisade dragged five tiles", Planning.Row.Count == 5 && walls > 0, $"{Planning.Row.Count} tiles, {walls} may take one; '{Planning.Prompt}'");
			Planning.DragHeldForTest = false;
			yield return Frames(3);
			Check("let go, the wall plans wait", WallPlans.Of(_city).Count == walls, $"{WallPlans.Of(_city).Count} waiting");
			Planning.AimForTest = null;
			Planning.Escape();
			int raised = WallPlans.Raised;
			for (int before = 0; before < 60; before++)
			{
				if (WallPlans.Of(_city).Count <= 0)
				{
					break;
				}
				try
				{
					if (_city.under_construction_building != null && _city.under_construction_building.isUnderConstruction())
					{
						for (int l = 0; l < 400; l++)
						{
							if (!_city.under_construction_building.isUnderConstruction())
							{
								break;
							}
							_city.under_construction_building.updateBuild(5);
						}
					}
				}
				catch
				{
				}
				yield return CivilTest.Wait(1f);
			}
			Check("the town raises the palisade", WallPlans.Raised > raised, $"{WallPlans.Raised - raised} raised, {WallPlans.Of(_city).Count} waiting");
			Check("...the game's own wall on the tile", w2.top_type != null && w2.top_type.wall, w2.top_type?.id);
			foreach (Building item4 in Blueprints.SitesOf(_city))
			{
				for (int m = 0; m < 400; m++)
				{
					if (!item4.isUnderConstruction())
					{
						break;
					}
					Blueprints.Blow(_king, item4, 1.5f);
				}
			}
			float hour = Mod.Day.TimeOfDay;
			Mod.Day.TimeOfDay = 0.66f;
			LookFrom(middle + new Vector2(-6f, -16f), middle + new Vector2(0f, 2f), 22f);
			yield return CivilTest.Wait(2f);
			yield return Shot("fixpass_town_aerial.png");
			LookFrom(new Vector2((float)w2.x + 2.5f, (float)w2.y + 0.5f) + new Vector2(-4f, 6f), new Vector2((float)w2.x + 2.5f, (float)w2.y + 0.5f), 1.8f);
			yield return CivilTest.Wait(1.2f);
			yield return Shot("fixpass_palisade_dusk.png");
			Mod.Day.TimeOfDay = hour;
			LookFrom(new Vector2((float)w2.x + 2.5f + 3f, (float)w2.y + 7f), new Vector2((float)w2.x + 2.5f, (float)w2.y + 0.5f), 3f);
			yield return CivilTest.Wait(0.6f);
			yield return Shot("fixpass_palisade.png");
			Mod.DebugEyeSet = false;
		}

		private IEnumerator Weapons()
		{
			if (!InControl(_king))
			{
				yield return Possess(_king, 5f);
			}
			if (!Check("playing the king", InControl(_king)))
			{
				yield break;
			}
			float hour = Mod.Day.TimeOfDay;
			WorldTile worldTile = OpenGround((_city.getTile() != null) ? ((Vector2)_city.getTile().posV3) : _king.current_position, 10, 8);
			if (!Check("open ground for the line", worldTile != null))
			{
				yield break;
			}
			Vector2 line = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 5.5f);
			Vector2 eye = new Vector2((float)worldTile.x + 4.5f, (float)worldTile.y + 2.9f);
			string[] array = new string[8] { "evil_staff", "necromancer_staff", "white_staff", "druid_staff", "plague_doctor_staff", "stick_wood", "ice_hammer", "flame_hammer" };
			List<Actor> holders = new List<Actor>();
			for (int i = 0; i < array.Length; i++)
			{
				Actor actor = CivilTest.Spawn("human", line + new Vector2((float)i * 1.15f, 0f));
				if (actor != null)
				{
					try
					{
						((BaseSimObject)actor).addStatusEffect("invincible", 300f, pColorEffect: false);
						actor.makeWait(60f);
					}
					catch
					{
					}
					Check("a villager holds the " + array[i], Equip(actor, array[i]));
					holders.Add(actor);
				}
			}
			_king.current_position = eye;
			Unequip(_king);
			Mod.DebugEye = eye;
			Mod.DebugEyeHeight = 0.6f;
			Mod.DebugEyeSet = true;
			Mod.SetYaw(MathF.PI / 2f);
			Mod.SetPitchDegrees(-5f);
			Mod.Day.TimeOfDay = 0.42f;
			for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
			{
				Hold(holders, line);
				yield return null;
			}
			yield return Shot("fixpass_weapons_held.png");
			Mod.Day.TimeOfDay = 0.93f;
			for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime)
			{
				Hold(holders, line);
				yield return null;
			}
			yield return Shot("fixpass_weapons_night.png");
			Mod.DebugEyeSet = false;
			Mod.DebugEyeHeight = 0f;
			Check("you hold the flame hammer", Equip(_king, "flame_hammer"));
			for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
			{
				Hold(holders, line);
				_king.current_position = eye;
				Mod.SetYaw(MathF.PI / 2f);
				yield return null;
			}
			yield return Shot("fixpass_flame_hammer_hand.png");
			Check("you hold the necromancer's staff", Equip(_king, "necromancer_staff"));
			for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
			{
				Hold(holders, line);
				_king.current_position = eye;
				Mod.SetYaw(MathF.PI / 2f);
				yield return null;
			}
			yield return Shot("fixpass_staff_hand.png");
			Mod.Day.TimeOfDay = hour;
			foreach (Actor item in holders)
			{
				CivilTest.Kill(item);
			}
		}

		private static void Hold(List<Actor> holders, Vector2 line)
		{
			for (int i = 0; i < holders.Count; i++)
			{
				try
				{
					Actor actor = holders[i];
					actor.current_position = line + new Vector2((float)i * 1.15f, 0f);
					actor.makeWait(5f);
					actor.lookTowardsPosition(line + new Vector2((float)i * 1.15f, -5f));
				}
				catch
				{
				}
			}
		}

		private static void Unequip(Actor unit)
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
			catch
			{
			}
		}

		private static WorldTile OpenGround(Vector2 around, int w, int h)
		{
			for (int i = 0; i < 600; i++)
			{
				float f = (float)i * 0.61f;
				float num = 4f + (float)i * 0.08f;
				int num2 = Mathf.FloorToInt(around.x + Mathf.Cos(f) * num);
				int num3 = Mathf.FloorToInt(around.y + Mathf.Sin(f) * num);
				bool flag = true;
				for (int j = num3; j < num3 + h && flag; j++)
				{
					for (int k = num2; k < num2 + w && flag; k++)
					{
						WorldTile tile = World.world.GetTile(k, j);
						flag = tile != null && tile.Type != null && tile.Type.ground && !tile.Type.block && !tile.Type.liquid && (tile.top_type == null || !tile.top_type.wall) && (tile.building == null || tile.building.city == null);
					}
				}
				if (flag)
				{
					CivilTest.ClearBox(num2, num3, num2 + w - 1, num3 + h - 1);
					return World.world.GetTile(num2, num3);
				}
			}
			return null;
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

		private void Stay(Vector2 stand, Vector2 at)
		{
			if (_king != null && _king.isAlive())
			{
				_king.current_position = stand;
				_king.dirty_current_tile = true;
				Mod.SetYaw(Mathf.Atan2(at.y - stand.y, at.x - stand.x));
			}
		}

		private IEnumerator AimAt(Building b, Vector2 stand, Vector2 at)
		{
			for (float pitch = 0f; pitch > -50f; pitch -= 3f)
			{
				Mod.SetPitchDegrees(pitch);
				for (float t = 0f; t < 0.12f; t += Time.unscaledDeltaTime)
				{
					Stay(stand, at);
					yield return null;
				}
				if (Mod.Picked == b || Mod.Work.Target == b)
				{
					for (int i = 0; i < 4; i++)
					{
						Stay(stand, at);
						yield return null;
					}
					break;
				}
			}
		}

		private WorldTile FreeSpot(BuildingAsset a, Vector2 around, float min, float max)
		{
			List<TileZone> list = new List<TileZone>();
			for (int i = 0; i < 400; i++)
			{
				float f = (float)i * 0.77f;
				float num = min + (max - min) * ((float)i / 400f);
				WorldTile tile = World.world.GetTile(Mathf.FloorToInt(around.x + Mathf.Cos(f) * num), Mathf.FloorToInt(around.y + Mathf.Sin(f) * num));
				if (tile != null)
				{
					list.Clear();
					if (Blueprints.WhyNotHere(_city, a, tile, list) == null && list.Count == 0)
					{
						return tile;
					}
				}
			}
			return null;
		}

		private void Look(Vector2 at, float from, float pitch = -16f)
		{
			Vector2 debugEye = at + new Vector2(-0.6f, -0.8f) * from;
			Mod.DebugEye = debugEye;
			Mod.DebugEyeSet = true;
			Mod.SetYaw(Mathf.Atan2(at.y - debugEye.y, at.x - debugEye.x));
			Mod.SetPitchDegrees(pitch);
		}

		private void LookFrom(Vector2 eye, Vector2 at, float height)
		{
			if ((at - eye).sqrMagnitude < 4f)
			{
				eye = at + new Vector2(-2f, -5f);
			}
			CivilTest.ClearBox(Mathf.FloorToInt(Mathf.Min(eye.x, at.x)) - 2, Mathf.FloorToInt(Mathf.Min(eye.y, at.y)) - 2, Mathf.CeilToInt(Mathf.Max(eye.x, at.x)) + 2, Mathf.CeilToInt(Mathf.Max(eye.y, at.y)) + 2);
			Mod.DebugEye = eye;
			Mod.DebugEyeHeight = height;
			Mod.DebugEyeSet = true;
			Mod.SetYaw(Mathf.Atan2(at.y - eye.y, at.x - eye.x));
			Mod.SetPitchDegrees((0f - Mathf.Atan2(height, (at - eye).magnitude)) * 57.29578f);
		}

		private IEnumerator Frames(int n)
		{
			for (int i = 0; i < n; i++)
			{
				yield return null;
			}
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
