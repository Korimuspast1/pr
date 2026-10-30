using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class KingOrdersTest : CivilTest
	{
		private readonly List<Actor> _people = new List<Actor>();

		protected override string Prefix => "KORD";

		protected override IEnumerator Body()
		{
			Actor king = Village("human", 16, 2);
			if (!Check("our village", king != null))
			{
				yield break;
			}
			_people.AddRange(Founded);
			Kingdom home = king.kingdom;
			City town = king.city;
			yield return Possess(king);
			if (Check("the king in first person", InControl(king) && Royal.IsKing(king)))
			{
				try
				{
					king.addTrait("invincible", pRemoveOpposites: true);
				}
				catch
				{
				}
				yield return Part("guards", Guards(king, home, town));
				yield return Part("ships", Ships(king, home, town));
			}
		}

		private IEnumerator Guards(Actor king, Kingdom home, City town)
		{
			int num = 0;
			foreach (Actor person in _people)
			{
				if (person != null && person.isAlive() && person.isWarrior())
				{
					num++;
				}
			}
			Note($"{num} warriors in {town.name}");
			Actor actor = null;
			List<Actor> subjects = new List<Actor>();
			foreach (Actor person2 in _people)
			{
				if (person2 != null && person2.isAlive() && person2 != king)
				{
					if (!person2.isAdult())
					{
						actor = actor ?? person2;
					}
					else if (!person2.isWarrior())
					{
						subjects.Add(person2);
					}
				}
			}
			if (!Check("subjects to name", subjects.Count >= 2, subjects.Count + " non-warrior adults"))
			{
				yield break;
			}
			if (actor != null)
			{
				Check("a child is never offered", KingGuards.Refusal(king, actor) == "", "refusal '" + KingGuards.Refusal(king, actor) + "'");
			}
			Actor shown = subjects[0];
			string prompt = null;
			for (int i = 0; i < 120; i++)
			{
				yield return null;
				CivilTest.StandBefore(shown, king, 1.4f);
				Vector2 vector = shown.current_position - king.current_position;
				if (vector.sqrMagnitude > 0.01f)
				{
					Mod.LookForTest(Mathf.Atan2(vector.y, vector.x), -6f);
				}
				if (Mod.Talk.Candidate == shown && Mod.Prompt != null)
				{
					prompt = Mod.ShownPrompt(Mod.Prompt);
					break;
				}
			}
			int num2 = Mod.ActionsForTest();
			Check("hold E offers the guards", prompt != null && prompt.Contains("Hold E") && num2 >= 6, string.Format("prompt '{0}' (raw '{1}'), {2} on the wheel, candidate {3}", prompt, Mod.Prompt, num2, (Mod.Talk.Candidate != null) ? WorldBoxMod.Describe(Mod.Talk.Candidate) : "none"));
			if (Mod.Converse.Open)
			{
				Mod.EndConversation();
			}
			Actor victim = subjects[0];
			bool flag = KingGuards.Give(king, victim, KingGuards.Kind.Kill, Mod.Toast);
			Check("the guards go to kill a subject", flag && KingGuards.CountForTest > 0, "orders " + KingGuards.CountForTest);
			yield return Until(() => !victim.isAlive(), 45f);
			Check("the subject is killed", !victim.isAlive(), "end " + KingGuards.LastEndForTest);
			yield return CivilTest.Wait(1f);
			Actor condemned = subjects[1];
			CivilTest.StandBefore(condemned, king, 4f);
			int cuts = Slicing.Cuts;
			flag = KingGuards.Give(king, condemned, KingGuards.Kind.Execute, Mod.Toast);
			Check("the guards seize someone for execution", flag);
			bool knelt = false;
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 40f && condemned.isAlive())
			{
				if (KingGuards.Kneels(condemned, out var pose) && pose.Amount > 0.5f && !knelt)
				{
					knelt = true;
					Note($"on their knees after {Time.realtimeSinceStartup - t0:F1} s");
					Vector2 vector2 = condemned.current_position - king.current_position;
					Vector2 dir = ((vector2.sqrMagnitude > 0.01f) ? new Vector2(0f - vector2.y, vector2.x).normalized : Vector2.right);
					EyeOn(condemned.current_position, dir, 2.6f, 0.5f);
					yield return CivilTest.Wait(0.5f);
					yield return Capture("kord_kneel.png");
				}
				yield return CivilTest.Wait(0.2f);
			}
			Check("they kneel before the blow", knelt);
			yield return Until(() => KingGuards.CountForTest == 0, 2f);
			Check("they are executed", !condemned.isAlive() && KingGuards.LastEndForTest == "executed", "end " + KingGuards.LastEndForTest);
			yield return CivilTest.Wait(0.6f);
			Note($"bodies cut: {cuts} -> {Slicing.Cuts} (only if drawn as they fell)");
			yield return Capture("kord_executed.png");
			Mod.DebugEyeSet = false;
			Actor wolf = CivilTest.SpawnNear("wolf", Royal.Position(town), 6f);
			if (wolf != null)
			{
				try
				{
					wolf.makeWait(3f);
				}
				catch
				{
				}
				flag = KingGuards.Give(king, wolf, KingGuards.Kind.Kill, Mod.Toast);
				Check("the guards go after a wolf", flag);
				yield return Until(() => !wolf.isAlive(), 45f);
				Check("the wolf is killed", !wolf.isAlive(), "end " + KingGuards.LastEndForTest);
			}
			else
			{
				Note("(no wolf could be spawned: skipped)");
			}
			Actor rivalKing = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				if (!((new Vector2(item.x, item.y) - Royal.Position(town)).magnitude < 40f))
				{
					rivalKing = KingTest.FoundKingdom(item, "orc", 4, 0);
					if (rivalKing != null && rivalKing.kingdom != home)
					{
						break;
					}
					rivalKing = null;
				}
			}
			if (rivalKing == null)
			{
				Note("(no site for a rival: the foreign check is skipped)");
				yield break;
			}
			Kingdom rival = rivalKing.kingdom;
			yield return CivilTest.Wait(2f);
			Actor visitor = null;
			int num3 = 0;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple.kingdom == rival)
				{
					num3++;
					if (visitor == null && simple != rivalKing && simple.isAdult() && Dialogue.IsPerson(simple))
					{
						visitor = simple;
					}
				}
			}
			Note($"{Royal.NameOf(rival)}: {num3} of theirs alive, king {WorldBoxMod.Describe(rivalKing)}");
			visitor = visitor ?? rivalKing;
			if (Check("a visitor from the rival realm", visitor != null && !Royal.AtWar(home, rival), string.Format("visitor {0}, at war {1}", (visitor != null) ? WorldBoxMod.Describe(visitor) : "none", Royal.AtWar(home, rival))))
			{
				visitor.current_position = Royal.Position(town) + new Vector2(3f, 1f);
				try
				{
					visitor.makeWait(5f);
				}
				catch
				{
				}
				float before = Peace.GrudgeOf(rival, home);
				Peace.Offend(rival, home, 40f);
				flag = KingGuards.Give(king, visitor, KingGuards.Kind.Kill, Mod.Toast);
				Check("the guards go to kill a foreign visitor", flag);
				yield return Until(() => !visitor.isAlive(), 45f);
				float num4 = Peace.GrudgeOf(rival, home);
				Check("the visitor is killed", !visitor.isAlive());
				Check("their realm holds it against you", num4 >= 45f, $"grudge {before:F0} -> {num4:F0}");
				Check("and goes to war over it", Royal.AtWar(rival, home));
			}
		}

		private IEnumerator Ships(Actor king, Kingdom home, City town)
		{
			Check("no docks: refused", Shipyard.Refusal(king, town, Shipyard.Hull.Transport) == "No docks" || Shipyard.HasDocks(town), "'" + Shipyard.Refusal(king, town, Shipyard.Hull.Transport) + "'");
			Building building = Harbour(king, town);
			if (!Check("a harbour for the town", building != null && Shipyard.Docks(town, Shipyard.Hull.Transport).Count >= 1, (building == null) ? "none" : $"at {building.current_tile.x},{building.current_tile.y}, ocean tiles {building.component_docks.tiles_ocean.Count}, city {building.city == town}"))
			{
				yield break;
			}
			int count = Shipyard.Docks(town, Shipyard.Hull.Transport).Count;
			int had = Shipyard.Afloat(town, Shipyard.Hull.Transport);
			int num = Shipyard.Cap(town, Shipyard.Hull.Transport) - had;
			try
			{
				king.addMoney(Shipyard.Prices[0] * (num + 2));
			}
			catch
			{
			}
			int num2 = CivilTest.Money(king);
			int ordered = 0;
			for (int i = 0; i < num; i++)
			{
				if (Shipyard.Order(king, town, Shipyard.Hull.Transport, Mod.Toast))
				{
					ordered++;
				}
			}
			Check("transports ordered up to the docks' room", ordered == num && num > 0, $"{count} docks, {had} afloat, ordered {ordered} of {num}");
			Check("paid from the purse", CivilTest.Money(king) == num2 - ordered * Shipyard.Prices[0], $"{num2} -> {CivilTest.Money(king)}");
			Check("one more is refused", !Shipyard.Order(king, town, Shipyard.Hull.Transport, Mod.Toast), Shipyard.Refusal(king, town, Shipyard.Hull.Transport));
			string pResult = null;
			try
			{
				town.data.get("fp_shipyard", out pResult, null);
			}
			catch
			{
			}
			Check("the order is kept on the town", !string.IsNullOrEmpty(pResult), pResult);
			Vector2 vector = new Vector2((float)building.current_tile.x + 0.5f, (float)building.current_tile.y + 0.5f);
			WorldTile shore = DryNear(vector, 3f, 12f);
			if (shore != null)
			{
				king.current_position = new Vector2((float)shore.x + 0.5f, (float)shore.y + 0.5f);
				Vector2 vector2 = vector - king.current_position;
				Mod.SetYaw(Mathf.Atan2(vector2.y, vector2.x));
				Mod.SetPitchDegrees(-6f);
			}
			WorldTile worldTile = null;
			try
			{
				foreach (ref WorldTile item in building.component_docks.tiles_ocean)
				{
					WorldTile current = item;
					if (current != null && current.isGoodForBoat())
					{
						worldTile = current;
						break;
					}
				}
			}
			catch
			{
			}
			if (worldTile != null)
			{
				Vector2 vector3 = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f);
				Vector2 vector4 = vector3 - vector;
				vector4 = ((vector4.sqrMagnitude > 0.01f) ? vector4.normalized : Vector2.left);
				Vector2 vector5 = new Vector2(0f - vector4.y, vector4.x);
				Vector2 dir = -(vector4 * 6f + vector5 * 3f).normalized;
				EyeOn(vector3, dir, 6.7f, 1.6f);
			}
			yield return CivilTest.Wait(3f);
			float soonest;
			int num3 = Shipyard.Building(town, out soonest);
			Check("one is on the stocks", num3 == ordered && soonest >= 0f, $"{num3} on order, next in {soonest:F1} s");
			yield return Capture("kord_hull.png");
			yield return CivilTest.Wait(3.5f);
			yield return Capture("kord_hull2.png");
			Mod.DebugEyeSet = false;
			int launched0 = Shipyard.LaunchedForTest;
			yield return Until(() => Shipyard.LaunchedForTest - launched0 >= ordered, 150f);
			Check("all are launched", Shipyard.LaunchedForTest - launched0 == ordered, Shipyard.LaunchedForTest - launched0 + " launched");
			yield return Capture("kord_launched.png");
			yield return CivilTest.Wait(25f);
			int num4 = Shipyard.Afloat(town, Shipyard.Hull.Transport);
			Check("they stay afloat past the game's one to a dock", num4 >= had + ordered, $"{num4} transports of {town.name} afloat");
			List<Actor> band = new List<Actor>();
			foreach (Actor person in _people)
			{
				if (person != null && person.isAlive() && person.isWarrior() && person.kingdom == home && band.Count < 4)
				{
					band.Add(person);
				}
			}
			if (shore != null)
			{
				for (int num5 = 0; num5 < band.Count; num5++)
				{
					band[num5].current_position = new Vector2((float)shore.x + 0.5f + (float)(num5 % 2), (float)shore.y + 0.5f + (float)(num5 / 2));
				}
			}
			WarMap map = Mod.WarMap;
			if (!map.Open)
			{
				map.Toggle(king, Mod.Toast);
			}
			yield return CivilTest.Wait(0.8f);
			if (map.Open)
			{
				map.Centre = Royal.Position(town);
				map.Zoom = Mathf.Clamp(map.Span / 50f, map.ZoomMin, map.ZoomMax);
				map.Marked.Clear();
				foreach (Actor item2 in band)
				{
					map.Marked.Add(item2);
				}
				Mod.HudForTest.CardCityForTest = town;
				yield return CivilTest.Wait(0.8f);
				Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "kord_card.png"));
				for (int i2 = 0; i2 < 20; i2++)
				{
					if (!Mod.HudForTest.CapturePending)
					{
						break;
					}
					yield return null;
				}
				Note("card: " + ((Mod.HudForTest.TownCardRectForTest.width > 0f) ? ("drawn " + Mod.HudForTest.TownCardRectForTest.ToString()) : "not drawn"));
				Mod.HudForTest.CardCityForTest = null;
				map.Toggle(king, Mod.Toast);
				yield return CivilTest.Wait(0.3f);
			}
			Actor ship = map.TransportFor(king, band);
			if (Check("Board finds a transport for them", ship != null && WarMap.Transport(ship), band.Count + " warriors"))
			{
				int cantReach;
				int noRoom;
				string why;
				int n = map.Board(Mod.Royal, king, band, ship, null, out cantReach, out noRoom, out why);
				Check("they go aboard", n == band.Count, $"{n} going, {cantReach} other land, {noRoom} no room, {why}");
				float b0 = Time.realtimeSinceStartup;
				yield return Until(() => WarMap.AboardCount(ship) >= n, 60f);
				Check("all aboard", WarMap.AboardCount(ship) >= n, $"{WarMap.AboardCount(ship)} aboard after {Time.realtimeSinceStartup - b0:F0} s");
			}
		}

		private IEnumerator Until(Func<bool> done, float seconds)
		{
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < seconds)
			{
				bool flag;
				try
				{
					flag = done();
				}
				catch
				{
					flag = true;
				}
				if (flag)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
		}

		private IEnumerator Capture(string file)
		{
			yield return CivilTest.Wait(0.3f);
			try
			{
				Mod.SaveFrame(file);
			}
			catch (Exception ex)
			{
				Note("picture failed: " + ex.Message);
			}
		}

		private void EyeOn(Vector2 target, Vector2 dir, float dist, float height)
		{
			Mod.DebugEyeSet = true;
			Mod.DebugEye = target - dir * dist;
			Mod.DebugEyeHeight = height;
			Mod.SetYaw(Mathf.Atan2(dir.y, dir.x));
			Mod.SetPitchDegrees(-10f);
		}

		private static bool Wet(int x, int y)
		{
			WorldTile tile = World.world.GetTile(x, y);
			try
			{
				return tile != null && tile.is_liquid && tile.Type != null && !tile.Type.lava;
			}
			catch
			{
				return false;
			}
		}

		private Building Harbour(Actor king, City town)
		{
			Vector2 vector = Royal.Position(town);
			WorldTile worldTile = null;
			for (int i = 4; i < 120; i += 2)
			{
				if (worldTile != null)
				{
					break;
				}
				for (int j = 0; j < 64; j++)
				{
					if (worldTile != null)
					{
						break;
					}
					float f = (float)j / 64f * MathF.PI * 2f;
					int num = (int)(vector.x + Mathf.Cos(f) * (float)i);
					int num2 = (int)(vector.y + Mathf.Sin(f) * (float)i);
					if (num < 16 || num2 < 16 || num >= MapBox.width - 16 || num2 >= MapBox.height - 16)
					{
						continue;
					}
					bool flag = true;
					for (int k = 0; k <= 4 && flag; k++)
					{
						for (int l = -2; l <= 2 && flag; l++)
						{
							if (!Wet(num + l, num2 + k))
							{
								flag = false;
							}
						}
					}
					if (!flag)
					{
						continue;
					}
					for (int m = 0; m < 8; m++)
					{
						if (worldTile != null)
						{
							break;
						}
						float f2 = (float)m * MathF.PI / 4f;
						int x = num + Mathf.RoundToInt(Mathf.Cos(f2) * 4.5f);
						int y = num2 + 2 + Mathf.RoundToInt(Mathf.Sin(f2) * 4.5f);
						if (Wet(x, y))
						{
							continue;
						}
						bool flag2 = true;
						for (int n = 4; n <= 14 && flag2; n++)
						{
							if (!Wet(num + Mathf.RoundToInt((0f - Mathf.Cos(f2)) * (float)n), num2 + 2 + Mathf.RoundToInt((0f - Mathf.Sin(f2)) * (float)n)))
							{
								flag2 = false;
							}
						}
						if (flag2)
						{
							worldTile = World.world.GetTile(num, num2);
						}
					}
				}
			}
			if (worldTile == null)
			{
				Note("no coast for a harbour");
				return null;
			}
			try
			{
				for (int num3 = -1; num3 <= 6; num3++)
				{
					for (int num4 = -3; num4 <= 3; num4++)
					{
						WorldTile tile = World.world.GetTile(worldTile.x + num4, worldTile.y + num3);
						if (tile != null && tile.zone != null && tile.zone.city == null)
						{
							town.addZone(tile.zone);
						}
					}
				}
				Building building = World.world.buildings.addBuilding("docks_human", worldTile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				if (building == null)
				{
					Note("the harbour could not be built");
					return null;
				}
				building.completeConstruction();
				try
				{
					building.setKingdom(king.kingdom);
				}
				catch
				{
				}
				List<Building> buildingListOfType = town.getBuildingListOfType("type_docks");
				if (buildingListOfType == null || !buildingListOfType.Contains(building))
				{
					town.listBuilding(building);
				}
				bool flag3 = building.component_docks != null && building.component_docks.hasOceanTiles();
				Note($"harbour at {worldTile.x},{worldTile.y} ({(float)worldTile.x - vector.x:F0},{(float)worldTile.y - vector.y:F0} from the town), water by it {flag3}");
				return building;
			}
			catch (Exception ex)
			{
				Log.Error("KORD harbour: " + ex);
				return null;
			}
		}

		private static WorldTile DryNear(Vector2 p, float min, float max)
		{
			for (float num = min; num <= max; num += 1f)
			{
				for (int i = 0; i < 32; i++)
				{
					float f = (float)i * MathF.PI / 16f;
					WorldTile tile = World.world.GetTile(Mathf.FloorToInt(p.x + Mathf.Cos(f) * num), Mathf.FloorToInt(p.y + Mathf.Sin(f) * num));
					if (Royal.Standable(tile))
					{
						return tile;
					}
				}
			}
			return null;
		}
	}
}
