using System;
using System.Collections;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal sealed class TownArtTest : CivilTest
	{
		private static readonly string[] Races = new string[4] { "human", "elf", "orc", "dwarf" };

		private City _capital;

		private Building _capitalHall;

		private readonly List<double> _hallMs = new List<double>();

		private double _lastMs;

		protected override string Prefix => "TOWNART";

		protected override IEnumerator Body()
		{
			yield return Part("the pack", Pack());
			yield return Part("the models", Carry());
			string only = null;
			string[] races = Races;
			foreach (string text in races)
			{
				if (Options.Contains("only" + text))
				{
					only = text;
				}
			}
			string[] races2 = Races;
			foreach (string text2 in races2)
			{
				if (only == null || !(text2 != only))
				{
					yield return Part(text2 + " halls", Levels(text2));
				}
			}
			yield return Part("the keep follows the capital", CapitalMoves());
			yield return Part("a house to compare the rooms with", HouseBaseline());
		}

		private static IEnumerable<string> Names()
		{
			string[] races = Races;
			foreach (string r in races)
			{
				for (int l = 0; l < 3; l++)
				{
					yield return $"tw_hall_{r}_{l}";
				}
				yield return "tw_keep_" + r;
			}
		}

		private IEnumerator Pack()
		{
			List<string> list = Models.TownsPacks();
			Check("the halls pack is in the mod", list.Contains("towns.halls"), string.Join(", ", list.ToArray()));
			List<string> list2 = new List<string>();
			foreach (string item2 in Names())
			{
				string[] array = new string[5]
				{
					item2,
					item2 + "_m",
					item2 + "_f",
					"ruin_" + item2,
					"site_" + item2
				};
				foreach (string item in array)
				{
					if (!Models.Has(item))
					{
						list2.Add(item);
					}
				}
			}
			Check("every hall, its distance versions, ruin and site are indexed", list2.Count == 0, list2.Count + " missing: " + string.Join(", ", list2.ToArray()));
			float waited = 0f;
			while (!Models.PackReady("tw_hall_human_0") && waited < 10f)
			{
				yield return CivilTest.Wait(0.1f);
				waited += 0.1f;
			}
			Check("the pack loads when first asked for", Models.PackReady("tw_hall_human_0"), waited.ToString("0.0") + " s");
			long bytes;
			int models;
			int num = Models.TownsPackState("towns.halls", out bytes, out models);
			Note($"towns.halls: state {num}, {bytes / 1024} KB, {models} models");
		}

		private IEnumerator Carry()
		{
			List<string> list = new List<string>();
			List<string> list2 = new List<string>();
			List<string> list3 = new List<string>();
			int num = 0;
			foreach (string item in Names())
			{
				MeshModel meshModel = Models.Get(item);
				if (meshModel == null)
				{
					list.Add(item + " (no model)");
					continue;
				}
				num++;
				if (!Steering.DoorOf(meshModel, out var at))
				{
					list.Add(item);
				}
				else if (at.y > meshModel.Min.Y + 3.5f)
				{
					list.Add(item + $" (door deep inside at y {at.y:F1})");
				}
				if (Anchors.Of(meshModel, "a_sign").Length == 0)
				{
					list2.Add(item);
				}
				float num2 = meshModel.InfoFloat("width", 0f);
				float num3 = num2 / (num2 / 0.36f * 0.25f);
				if (meshModel.Max.Y - meshModel.Min.Y > 5.4f * num3 || meshModel.Max.X - meshModel.Min.X > num2 + 1.2f)
				{
					list3.Add($"{item} {meshModel.Max.X - meshModel.Min.X:F1}x{meshModel.Max.Y - meshModel.Min.Y:F1} m");
				}
			}
			Check("16 hall models read", num == 16, num.ToString());
			Check("each has a door the scene finds at its front", list.Count == 0, string.Join(", ", list.ToArray()));
			Check("each marks the notice board's spot (a_sign)", list2.Count == 0, string.Join(", ", list2.ToArray()));
			Check("each keeps to its footprint", list3.Count == 0, string.Join(", ", list3.ToArray()));
			yield break;
		}

		private static Building HallOf(City c)
		{
			try
			{
				if (c.buildings_dict_type != null && c.buildings_dict_type.TryGetValue("type_hall", out var value))
				{
					foreach (Building item in value)
					{
						if (item != null && item.isAlive())
						{
							return item;
						}
					}
				}
			}
			catch
			{
			}
			return null;
		}

		private Building AddHall(string id, City city)
		{
			try
			{
				WorldTile tile = city.getTile();
				if (tile == null)
				{
					return null;
				}
				for (int i = 0; i < 80; i++)
				{
					float f = (float)i * 0.61f;
					float num = 6f + (float)i * 0.2f;
					WorldTile tile2 = World.world.GetTile(tile.x + (int)(Mathf.Cos(f) * num), tile.y + (int)(Mathf.Sin(f) * num));
					if (tile2 == null || tile2.Type == null || !tile2.Type.ground || tile2.Type.block || tile2.building != null)
					{
						continue;
					}
					Building building = World.world.buildings.addBuilding(id, tile2, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					if (building != null)
					{
						try
						{
							building.completeConstruction();
							building.setKingdom(city.kingdom);
						}
						catch
						{
						}
						if (building.city != city)
						{
							city.listBuilding(building);
						}
						return building;
					}
				}
			}
			catch (Exception ex)
			{
				Note("place " + id + ": " + ex.Message);
			}
			return null;
		}

		private IEnumerator Front(Building b, float back = 8.5f)
		{
			WorldTile current_tile = b.current_tile;
			Vector2 hall = new Vector2((float)current_tile.x + 0.5f, (float)current_tile.y + 1.5f);
			CivilTest.ClearBox(current_tile.x - 9, current_tile.y - 12, current_tile.x + 9, current_tile.y - 2);
			Vector2 eye = new Vector2(hall.x + back * 0.45f, hall.y - back);
			float[] array = new float[2] { 0.45f, -0.45f };
			foreach (float num in array)
			{
				float[] array2 = new float[3] { 1f, 0.8f, 0.65f };
				int num2 = 0;
				Vector2 vector;
				while (num2 < array2.Length)
				{
					float num3 = array2[num2];
					vector = new Vector2(hall.x + num * back * num3, hall.y - back * num3);
					WorldTile tile = World.world.GetTile((int)vector.x, (int)vector.y);
					if (tile == null || tile.main_type == null || tile.main_type.liquid || tile.Type == null || !tile.Type.ground)
					{
						num2++;
						continue;
					}
					goto IL_0180;
				}
				continue;
				IL_0180:
				eye = vector;
				break;
			}
			yield return CivilTest.Wait(0.3f);
			Mod.DebugEye = eye;
			Mod.DebugEyeSet = true;
			Mod.SetYaw(Mathf.Atan2(hall.y - eye.y, hall.x - eye.x));
			Mod.SetPitchDegrees(5f);
			yield return CivilTest.Wait(1.2f);
		}

		private IEnumerator Levels(string race)
		{
			Actor king = Village(race, 8, 0);
			if (!Check("a " + race + " village", king != null))
			{
				yield break;
			}
			City city = king.city;
			yield return CivilTest.Wait(1f);
			Actor you = Founded.Find((Actor a) => a != null && a != king && a.isAlive() && a.isAdult());
			if (you != null)
			{
				try
				{
					((BaseSimObject)you).addStatusEffect("invincible", 600f, pColorEffect: false);
				}
				catch
				{
				}
				yield return Possess(you);
			}
			Check("first person on a villager", you != null && InControl(you));
			Building hall = HallOf(city) ?? AddHall("hall_" + race + "_0", city);
			if (!Check("it has a hall", hall != null))
			{
				yield break;
			}
			yield return CivilTest.Wait(1.5f);
			try
			{
				if (hall.isUnderConstruction())
				{
					hall.completeConstruction();
				}
			}
			catch
			{
			}
			float waited = 0f;
			while (!TownTier.IsCapital(city) && waited < 6f)
			{
				yield return CivilTest.Wait(0.25f);
				waited += 0.25f;
			}
			if (!TownTier.IsCapital(city))
			{
				Note("the game hasn't named a capital yet: the village made it");
				try
				{
					city.kingdom.capital = city;
				}
				catch (Exception ex)
				{
					Note("capital: " + ex.Message);
				}
			}
			Check("the village is its realm's capital", TownTier.IsCapital(city), waited.ToString("0.0") + " s");
			for (int level = 0; level <= 2; level++)
			{
				if (level > 0)
				{
					bool up = false;
					for (int tries = 0; tries < 8; tries++)
					{
						if (up)
						{
							break;
						}
						bool flag = false;
						try
						{
							flag = hall.canBeUpgraded();
							up = flag && hall.upgradeBuilding();
						}
						catch (Exception ex2)
						{
							Note("upgrade: " + ex2.Message);
						}
						if (!up)
						{
							Note($"upgrade refused (can be upgraded: {flag}), asking again");
							yield return CivilTest.Wait(0.5f);
							hall = HallOf(city) ?? hall;
						}
					}
					yield return CivilTest.Wait(0.5f);
					hall = HallOf(city) ?? hall;
					if (!Check($"the game upgrades it to hall {level}", up && hall.asset.id == $"hall_{race}_{level}", hall.asset.id))
					{
						yield break;
					}
				}
				string text = ((level == 2) ? ("tw_keep_" + race) : $"tw_hall_{race}_{level}");
				string text2 = Models.BuildingFamily(hall);
				Check($"hall {level} is drawn by {text}", text2 == text, text2);
				yield return Front(hall, (level == 0) ? 7.5f : 8.5f);
				string tag = ((level == 2) ? "keep" : level.ToString());
				if (level != 1)
				{
					yield return BoardBeside(city, race, tag);
				}
				Mod.SaveFrame("townart_" + race + "_" + tag + ".png");
				yield return Room(hall, race, tag);
			}
			if (race == "human")
			{
				Settings s = Mod.Settings;
				int level = s.TimeMode;
				SceneCollector.AllLit = true;
				s.TimeMode = 3;
				Mod.ForceRelight();
				yield return CivilTest.Wait(1.5f);
				Mod.SaveFrame("townart_human_keep_night.png");
				SceneCollector.AllLit = false;
				s.TimeMode = level;
				Mod.ForceRelight();
				_capital = city;
				_capitalHall = hall;
			}
			Mod.DebugEyeSet = false;
		}

		private IEnumerator BoardBeside(City city, string race, string tag)
		{
			Law.Board board = null;
			bool kept = false;
			string why = null;
			Vector2 hallAt = default(Vector2);
			Vector2 spot = default(Vector2);
			for (float t = 0f; t < 7f; t += 0.5f)
			{
				kept = HallBoard.ForTest(city, out hallAt, out spot, out why);
				board = null;
				foreach (Law.Board item in Law.BoardsNear((spot == default(Vector2)) ? hallAt : spot, 40f))
				{
					if (item.City == city)
					{
						board = item;
					}
				}
				if (kept && board != null && (board.At - spot).magnitude < 0.05f)
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			if (!kept && why != null)
			{
				Note(race + " hall " + tag + ": the board's spots are taken (" + why + "): Law's own spot kept");
			}
			else if (Check(race + " hall " + tag + ": its board's spot is known", kept, why))
			{
				Check(race + " hall " + tag + ": the town's bounty board stands there", board != null && (board.At - spot).magnitude < 0.05f, (board != null) ? $"board {board.At.x:F1},{board.At.y:F1}, spot {spot.x:F1},{spot.y:F1}" : "no board");
				Check(race + " hall " + tag + ": beside the hall, not in front of its door", Mathf.Abs(spot.x - hallAt.x) >= 1.4f && spot.y < hallAt.y, $"{spot.x - hallAt.x:F1} tiles across, {spot.y - hallAt.y:F1} along");
			}
		}

		private IEnumerator Room(Building hall, string race, string tag)
		{
			bool keep = tag == "keep";
			Check(race + " hall " + tag + ": its door leads in", HallRooms.CanEnter(hall), HallRooms.RoomFor(hall));
			float waited = 0f;
			string name = HallRooms.RoomFor(hall);
			while (name != null && !Models.PackReady(name) && waited < 5f)
			{
				yield return CivilTest.Wait(0.1f);
				waited += 0.1f;
			}
			for (float t = 0f; t < 4f; t += 0.5f)
			{
				if (Mod.Interior.Inside)
				{
					break;
				}
				Mod.EnterHouseForTest(hall);
				yield return CivilTest.Wait(0.5f);
			}
			yield return CivilTest.Wait(0.5f);
			Interior room = (Mod.Interior.Inside ? Mod.Interior.Room : null);
			string text = HallRooms.KindOf(room);
			if (!Check(race + " hall " + tag + ": inside, in the " + (keep ? "keep's" : "hall's") + " own room", room != null && text == (keep ? "keep" : "hall"), text + " " + Mod.Interior.Name))
			{
				if (Mod.Interior.Inside)
				{
					Mod.LeaveRoomForTest();
				}
				yield break;
			}
			int num = HallRooms.Spots(room, "a_seat").Length;
			Check($"{race} hall {tag}: {(keep ? 6 : 5)} council seats, the war table, the cell and its door", num == (keep ? 6 : 5) && HallRooms.Spots(room, "war_table").Length == 1 && HallRooms.Spots(room, "cell").Length == 3 && HallRooms.Spots(room, "cell_door").Length == 1, $"{num} seats");
			List<string> list = new List<string>();
			string[] names = HallRoomBuilder.Names;
			foreach (string arg in names)
			{
				Anchors.Spot[] array = HallRooms.Spots(room, arg);
				for (int j = 0; j < array.Length; j++)
				{
					Anchors.Spot spot = array[j];
					if (spot.At.X < room.MinX - 0.3f || spot.At.X > room.MaxX + 0.3f || spot.At.Y < room.MinY - 0.3f || spot.At.Y > room.MaxY + 0.3f)
					{
						list.Add($"{arg} {spot.At.X:F1},{spot.At.Y:F1}");
					}
				}
			}
			Check(race + " hall " + tag + ": every spot is in the room", list.Count == 0, string.Join("; ", list.ToArray()));
			if (keep)
			{
				Check("the keep: the throne, a petitioner's place, the steward's desk", HallRooms.Spots(room, "throne").Length == 1 && HallRooms.Spots(room, "petition").Length == 1 && HallRooms.Spots(room, "steward").Length == 1);
			}
			Mod.SaveFrame("townart_" + race + "_" + tag + "_inside.png");
			yield return MeasureRoom(race + " hall " + tag);
			_hallMs.Add(_lastMs);
			Anchors.Spot[] array2 = (keep ? HallRooms.Spots(room, "petition") : HallRooms.Spots(room, "war_table"));
			if (array2.Length != 0)
			{
				Mod.Interior.Eye = new Vector2(array2[0].At.X, array2[0].At.Y - (keep ? 0.4f : 1.9f));
				Mod.SetYaw(MathF.PI / 2f);
				yield return CivilTest.Wait(0.8f);
				Mod.SaveFrame("townart_" + race + "_" + tag + "_" + (keep ? "throne" : "council") + ".png");
			}
			Mod.LeaveRoomForTest();
			yield return CivilTest.Wait(0.5f);
		}

		private IEnumerator MeasureRoom(string name)
		{
			Mod.ResetPerf();
			yield return CivilTest.Wait(0.5f);
			float t0 = Time.unscaledTime;
			int frames = 0;
			double ms = 0.0;
			double tris = 0.0;
			List<double> renders = new List<double>();
			while (Time.unscaledTime - t0 < 3f)
			{
				yield return null;
				ms += (double)Time.unscaledDeltaTime * 1000.0;
				tris += (double)Mod.Renderer.MeshTriangles;
				renders.Add(Mod.RenderMs);
				frames++;
			}
			renders.Sort();
			_lastMs = ((renders.Count > 0) ? renders[renders.Count / 2] : 0.0);
			if (frames > 0)
			{
				Note($"{name} room: {1000.0 / (ms / (double)frames):F0} fps, {ms / (double)frames:F1} ms a frame, {tris / (double)frames:F0} triangles drawn, render {_lastMs:F1} ms (median), " + $"{Mod.Renderer.Lights.Count} lights, {Mod.Renderer.Windows.Count} windows");
			}
		}

		private IEnumerator HouseBaseline()
		{
			if (_capital == null)
			{
				yield break;
			}
			Building house = AddHall("house_human_2", _capital);
			if (!Check("a house to compare with", house != null))
			{
				yield break;
			}
			yield return CivilTest.Wait(1f);
			for (float t = 0f; t < 4f; t += 0.5f)
			{
				if (Mod.Interior.Inside)
				{
					break;
				}
				Mod.EnterHouseForTest(house);
				yield return CivilTest.Wait(0.5f);
			}
			if (!Check("inside the house", Mod.Interior.Inside && HallRooms.KindOf(Mod.Interior.Room) == null))
			{
				yield break;
			}
			yield return MeasureRoom("an ordinary house (tier 2)");
			double houseMs = _lastMs;
			Mod.LeaveRoomForTest();
			yield return CivilTest.Wait(0.5f);
			double num = 0.0;
			foreach (double hallM in _hallMs)
			{
				num = Math.Max(num, hallM);
			}
			List<double> list = new List<double>(_hallMs);
			list.Sort();
			double num2 = ((list.Count > 0) ? list[list.Count / 2] : 0.0);
			Note($"hall rooms render {num2:F1} ms (the middle one), at most {num:F1} ms; the house {houseMs:F1} ms");
			Check("the hall rooms draw within twice a house's time", houseMs <= 0.0 || num2 <= houseMs * 2.0 || num2 < 2.0, $"{num2:F1} vs {houseMs:F1} ms");
		}

		private IEnumerator CapitalMoves()
		{
			if (_capital == null || _capitalHall == null)
			{
				Note("no human capital from before: skipped");
				yield break;
			}
			Kingdom realm = _capital.kingdom;
			Actor actor = Village("human", 4, 0, _capital.getTile(), 45f);
			if (!Check("a second village", actor != null))
			{
				yield break;
			}
			City town = actor.city;
			try
			{
				town.joinAnotherKingdom(realm);
			}
			catch (Exception ex)
			{
				Note("join: " + ex.Message);
			}
			yield return CivilTest.Wait(1f);
			Building hall = HallOf(town) ?? AddHall("hall_human_0", town);
			if (!Check("it joins the realm and has a hall", town.kingdom == realm && hall != null, town.kingdom?.name))
			{
				yield break;
			}
			try
			{
				if (hall.isUnderConstruction())
				{
					hall.completeConstruction();
				}
			}
			catch
			{
			}
			for (int k = 0; k < 2; k++)
			{
				try
				{
					if (hall.canBeUpgraded())
					{
						hall.upgradeBuilding();
					}
				}
				catch (Exception ex2)
				{
					Note("upgrade: " + ex2.Message);
				}
				yield return CivilTest.Wait(0.5f);
				hall = HallOf(town) ?? hall;
			}
			Check("its level-2 hall is a city hall, not a keep", Models.BuildingFamily(hall) == "tw_hall_human_2", hall.asset.id + " " + Models.BuildingFamily(hall));
			HallModels.Watch(10f);
			int generation = TownModels.Generation;
			try
			{
				realm.capital = town;
			}
			catch (Exception ex3)
			{
				Note("capital: " + ex3.Message);
			}
			HallModels.Watch(10f);
			Check("a capital moving makes the scene pick its halls again", TownModels.Generation > generation, generation + " -> " + TownModels.Generation);
			Check("the new capital's hall is the keep", Models.BuildingFamily(hall) == "tw_keep_human", Models.BuildingFamily(hall));
			Check("the old capital's is a city hall again", Models.BuildingFamily(_capitalHall) == "tw_hall_human_2", Models.BuildingFamily(_capitalHall));
			try
			{
				realm.capital = _capital;
			}
			catch
			{
			}
			HallModels.Watch(10f);
		}
	}
}
