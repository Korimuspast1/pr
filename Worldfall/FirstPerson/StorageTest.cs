using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class StorageTest : CivilTest
	{
		private Actor _host;

		private City _city;

		private Building _house;

		protected override string Prefix => "ST";

		private Hud H => Mod.HudForTest;

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
			Mod.Settings.HeadBob = 0f;
			_house = PlaceHouse("house_human_1");
			if (!Check("a house", _house != null))
			{
				yield break;
			}
			_host.setHomeBuilding(_house);
			yield return CivilTest.Wait(1f);
			Check("living there isn't owning it: no storage yet", Workbench.HomeOf(_host) == _house && !Chest.Yours(_house, _host));
			Homes.SetOwner(_house, _host);
			Check("bought: it is yours", Chest.Yours(_house, _host));
			Check("its storage has a tier 1 house's slots", Chest.Size(_house) == 24, Chest.Size(_house).ToString());
			try
			{
				foreach (KeyValuePair<string, int> item2 in Bag.Contents(_host))
				{
					Civil.TakeFromBag(_host, item2.Key, item2.Value);
				}
				_host.data.removeString("fp_gear");
			}
			catch
			{
			}
			int st = Bag.Stack("wood");
			int n = st * 2 + st / 2;
			Bag.Put(_host, "wood", n, spill: false);
			Gear.Add(_host, new Gear.Piece
			{
				Id = "sword_iron",
				Mods = new string[1] { "power1" },
				Name = "Kept Blade",
				Kills = 2
			});
			Mod.EnterHouseForTest(_house);
			yield return CivilTest.Wait(1.2f);
			HouseInterior interior = Mod.Interior;
			if (Check("inside your house", interior.Inside && interior.House == _house))
			{
				if (Chest.ChestOf(interior.Room, out var at))
				{
					Vector2 vector = at - interior.Eye;
					interior.Eye = at - ((vector.sqrMagnitude > 0.01f) ? vector.normalized : Vector2.right) * 0.85f;
					Mod.LookForTest(Mathf.Atan2(at.y - interior.Eye.y, at.x - interior.Eye.x), -25f);
					Note($"chest at {at.x:F1},{at.y:F1}");
				}
				else
				{
					Note("no chest in the room: E anywhere");
				}
				yield return CivilTest.Wait(0.6f);
				Check("at your chest: E:Storage", Mod.Prompt != null && Mod.Prompt.Contains("E:Storage"), Mod.Prompt);
				PlayInput.TestPress(KeyCode.E);
				yield return CivilTest.Wait(0.2f);
				PlayInput.TestRelease(KeyCode.E);
				yield return CivilTest.Wait(0.2f);
				if (!Check("E opens the storage", Chest.Open && Chest.House == _house))
				{
					Chest.Show(_house);
				}
				int num = Chest.Store(_host, _house, "wood", n);
				Check(n + " wood put away", num == n && Chest.Count(_house, "wood") == n && Civil.BagCount(_host, "wood") == 0, string.Format("{0} moved, house {1}, bag {2}", num, Chest.Count(_house, "wood"), Civil.BagCount(_host, "wood")));
				Check("it takes 3 of the house's slots", Chest.Used(_house) == 3, Chest.Used(_house).ToString());
				int num2 = Gear.Of(_host).FindIndex((Gear.Piece p) => p.Name == "Kept Blade");
				Check("a spare sword put away", num2 >= 0 && Chest.StorePiece(_host, _house, num2) && Gear.Count(_host) == 0 && Chest.Pieces(_house).Count == 1);
				Gear.Piece piece = ((Chest.Pieces(_house).Count > 0) ? Chest.Pieces(_house)[0] : null);
				Check("it keeps its enchantment, name and kills", piece != null && piece.Id == "sword_iron" && piece.Name == "Kept Blade" && piece.Kills == 2 && Array.IndexOf(piece.Mods, "power1") >= 0);
				Check("crafting at home counts what's stored", CraftStores.Count(_host, "wood") == n && CraftStores.InChests(_host, "wood") == n, CraftStores.Count(_host, "wood").ToString());
				yield return Shot("storage.png");
				Check("some wood taken back", Chest.Fetch(_host, _house, "wood", 10) == 10 && Civil.BagCount(_host, "wood") == 10 && Chest.Count(_house, "wood") == n - 10);
				Check("and the sword", Chest.FetchPiece(_host, _house, 0) && Gear.Of(_host).Exists((Gear.Piece p) => p.Name == "Kept Blade") && Chest.Pieces(_house).Count == 0);
				Check("no more than its slots", Chest.Store(_host, _house, "wood", 10) == 10 && Chest.RoomFor(_house, "wood") == 21 * st + (3 * st - n), Chest.RoomFor(_house, "wood").ToString());
				PlayInput.TestPress(KeyCode.E);
				yield return CivilTest.Wait(0.2f);
				PlayInput.TestRelease(KeyCode.E);
				yield return CivilTest.Wait(0.2f);
				Check("E closes it", !Chest.Open);
				Chest.Show(_house);
				Mod.LeaveRoomForTest();
				yield return CivilTest.Wait(0.5f);
				Check("leaving the house closes it", !Chest.Open);
				Chest.Close();
				Building building = PlaceHouse("house_human_0");
				Check("someone else's house has no storage for you", building != null && !Chest.Yours(building, _host));
				Check("away from home crafting doesn't read it", !Workbench.AtHome(_host) || CraftStores.InChests(_host, "wood") == n, Workbench.AtHome(_host).ToString());
			}
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
