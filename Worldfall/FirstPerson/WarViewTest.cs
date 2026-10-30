using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class WarViewTest : CivilTest
	{
		private readonly Queue<Event> _events = new Queue<Event>();

		protected override string Prefix => "WVIEW";

		protected override IEnumerator Body()
		{
			Actor king = Village("human", 12, 2);
			if (!Check("our village", king != null))
			{
				yield break;
			}
			List<Actor> list = new List<Actor>(Founded);
			WorldTile current_tile = king.current_tile;
			Actor ally = Near("elf", current_tile);
			Actor foe = Near("orc", current_tile);
			Actor other = Near("dwarf", current_tile);
			Actor fourth = Near("human", current_tile);
			Check("four other villages", ally != null && foe != null && other != null && fourth != null, $"elf {ally != null}, orc {foe != null}, dwarf {other != null}, human {fourth != null}");
			if (ally == null || foe == null || other == null)
			{
				yield break;
			}
			foreach (Actor item in list)
			{
				try
				{
					((BaseSimObject)item).addStatusEffect("invincible", 900f, pColorEffect: false);
				}
				catch
				{
				}
			}
			yield return Possess(king);
			if (!Check("the king in first person", InControl(king) && Royal.IsKing(king)))
			{
				yield break;
			}
			Kingdom k = king.kingdom;
			Kingdom allyK = ally.kingdom;
			Kingdom foeK = foe.kingdom;
			Kingdom otherK = other.kingdom;
			Kingdom fourthK = fourth?.kingdom;
			try
			{
				if (World.world.alliances.newAlliance(k, allyK) == null)
				{
					Note("no alliance made");
				}
			}
			catch (Exception ex)
			{
				Note("alliance: " + ex.Message);
			}
			try
			{
				Mod.Royal.DeclareWar(king, foeK, Mod.Toast, Mod.PlayHorn);
			}
			catch (Exception ex2)
			{
				Note("war: " + ex2.Message);
			}
			if (fourthK != null)
			{
				try
				{
					World.world.diplomacy.startWar(otherK, fourthK, WarTypeLibrary.normal, pLog: false);
				}
				catch (Exception ex3)
				{
					Note("their war: " + ex3.Message);
				}
			}
			Check("allied with the elves", Royal.Allied(k, allyK));
			Check("at war with the orcs", Royal.AtWar(k, foeK));
			Check("the dwarves at war with the others", fourthK == null || Royal.AtWar(otherK, fourthK));
			yield return CivilTest.Wait(1.5f);
			WarMap map = Mod.WarMap;
			map.Overlay = WarMap.Layer.Kingdoms;
			map.Tab = 0;
			map.Picked = null;
			map.Toggle(king, Mod.Toast);
			yield return CivilTest.Wait(1.2f);
			if (!Check("the war map opens", map.Open))
			{
				yield break;
			}
			WarMap.Realm realm = map.RealmOf(k);
			WarMap.Realm realm2 = map.RealmOf(allyK);
			WarMap.Realm realm3 = map.RealmOf(foeK);
			WarMap.Realm realm4 = map.RealmOf(otherK);
			Check("your realm listed as yours", realm != null && realm.Side == WarMap.Side.Own);
			Check("the elves listed as allies", realm2 != null && realm2.Side == WarMap.Side.Ally, (realm2 != null) ? realm2.Side.ToString() : "missing");
			Check("the orcs listed as enemies", realm3 != null && realm3.Side == WarMap.Side.Enemy, (realm3 != null) ? realm3.Side.ToString() : "missing");
			Check("the dwarves listed as neither", realm4 != null && realm4.Side == WarMap.Side.Neutral, (realm4 != null) ? realm4.Side.ToString() : "missing");
			Check("allies first in the list", map.Realms.Count > 1 && map.Realms[0].K == k && map.Realms[1].K == allyK, string.Join(", ", map.Realms.ConvertAll((WarMap.Realm r) => r.Name + " " + r.Side).ToArray()));
			Note("realms: " + string.Join("; ", map.Realms.ConvertAll((WarMap.Realm r) => string.Format("{0} {1} towns {2} warriors {3} opinion {4} foes {5} friends {6}", r.Name, r.Side, r.Towns, r.Warriors, r.HasOpinion ? r.Opinion.ToString() : "-", r.Foes.Count, r.Friends.Count)).ToArray()));
			WarMap.WarEntry warEntry = map.AllWars.Find((WarMap.WarEntry e) => e.Ours);
			WarMap.WarEntry warEntry2 = map.AllWars.Find((WarMap.WarEntry e) => !e.Ours);
			Check("our war listed, the orcs the foe", warEntry != null && warEntry.Foe == foeK && warEntry.WeAttack, (warEntry != null) ? $"{warEntry.Name}: attackers {warEntry.Attackers.Count}, defenders {warEntry.Defenders.Count}, foe {Royal.NameOf(warEntry.Foe)}" : "none");
			Check("the dwarves' war listed as another's", fourthK == null || (warEntry2 != null && warEntry2.Attackers.Contains(otherK) && warEntry2.Defenders.Contains(fourthK)), (warEntry2 != null) ? warEntry2.Name : "none");
			WarMap.Pact pact = map.Pacts.Find((WarMap.Pact p) => p.Ours);
			Check("our alliance listed with both members", pact != null && pact.Members.Contains(k) && pact.Members.Contains(allyK), (pact != null) ? (pact.Name + ", " + pact.Members.Count + " members") : "none");
			Check("the ties each way", realm != null && realm.Friends.Contains(allyK) && realm.Foes.Contains(foeK) && realm3 != null && realm3.Foes.Contains(k));
			Check("the zones read", map.ZonesX > 0 && map.ZonesY > 0, $"{map.ZonesX}x{map.ZonesY} zones of {map.ZoneSize} tiles");
			int num = map.OwnerAt(king.current_position);
			Check("your town's land is yours", num >= 0 && num == map.IndexOf(k), $"owner {num}, yours {map.IndexOf(k)}");
			Check("the elves' land is theirs", map.OwnerAt(ally.current_position) == map.IndexOf(allyK));
			Check("borders drawn between realms", map.Borders.Count >= 8, map.Borders.Count + " stretches");
			Check("the land picture for each layer", map.TerritoryFor(WarMap.Layer.Kingdoms) != null && map.TerritoryFor(WarMap.Layer.Sides) != null && map.TerritoryFor(WarMap.Layer.Alliances) != null);
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			foreach (WarMap.Unit unit in map.Units)
			{
				if (unit.Side == WarMap.Side.Ally)
				{
					num2++;
				}
				else if (unit.Side == WarMap.Side.Neutral)
				{
					num3++;
				}
				else if (unit.Side == WarMap.Side.Enemy)
				{
					num4++;
				}
			}
			Check("allied warriors on the map", num2 > 0, $"allies {num2}, enemies {num4}, others {num3}");
			Check("others' warriors on the map", num3 > 0);
			Stopwatch stopwatch = Stopwatch.StartNew();
			map.RefreshRealmsForTest();
			stopwatch.Stop();
			Note($"a full refresh took {stopwatch.Elapsed.TotalMilliseconds:F2} ms ({map.Realms.Count} realms, {map.AllWars.Count} wars, {map.Pacts.Count} alliances, {map.Borders.Count} borders)");
			Check("a refresh is quick", stopwatch.Elapsed.TotalMilliseconds < 25.0, $"{stopwatch.Elapsed.TotalMilliseconds:F2} ms");
			map.Zoom = map.ZoomMin;
			map.Centre = new Vector2(WarMap.WorldWidth * 0.5f, WarMap.WorldHeight * 0.5f);
			yield return Capture("wview_kingdoms.png", 0.8f);
			map.Overlay = WarMap.Layer.Sides;
			yield return Capture("wview_sides.png");
			map.Overlay = WarMap.Layer.Alliances;
			yield return Capture("wview_alliances.png");
			map.Overlay = WarMap.Layer.Kingdoms;
			WarMap.Town town = map.Towns.Find((WarMap.Town t) => t.Owner == otherK);
			if (Check("the dwarves' town on the map", town.Owner == otherK))
			{
				map.Centre = town.P;
				map.Zoom = Mathf.Clamp(map.Span / 90f, map.ZoomMin, map.ZoomMax);
				yield return null;
				yield return null;
				yield return CivilTest.Wait(0.3f);
				yield return Click(0, Screen(map, town.P) + new Vector2(0f, -6f) * GameArt.P);
				Check("a click on a town picks its kingdom", map.Picked == otherK, "picked " + Royal.NameOf(map.Picked));
				map.Zoom = map.ZoomMin;
				map.Centre = new Vector2(WarMap.WorldWidth * 0.5f, WarMap.WorldHeight * 0.5f);
				yield return Capture("wview_picked.png", 0.6f);
				yield return Click(0, Screen(map, town.P) + new Vector2(0f, -6f) * GameArt.P);
				Check("a second click lets it go", map.Picked == null, "picked " + Royal.NameOf(map.Picked));
			}
			map.FocusWar(foeK);
			yield return CivilTest.Wait(0.8f);
			Check("the map on our war", map.Enemy == foeK, Royal.NameOf(map.Enemy));
			map.Overlay = WarMap.Layer.Sides;
			yield return Capture("wview_war.png", 0.6f);
			map.Centre = king.current_position;
			map.Zoom = Mathf.Clamp(map.Span / 60f, map.ZoomMin, map.ZoomMax);
			yield return Capture("wview_close.png", 0.6f);
			map.Tab = 1;
			map.Overlay = WarMap.Layer.Kingdoms;
			yield return Capture("wview_orders.png", 0.6f);
			map.Tab = 0;
			map.Close();
			yield return CivilTest.Wait(0.4f);
		}

		private Actor Near(string species, WorldTile home)
		{
			Actor actor = Village(species, 8, 1, home, 30f);
			if (actor == null)
			{
				actor = Village(species, 8, 1);
			}
			return actor;
		}

		private static Vector2 Screen(WarMap map, Vector2 world)
		{
			return WorldBoxMod.Instance.HudForTest.WarMapToScreenForTest?.Invoke(world) ?? Vector2.zero;
		}

		private IEnumerator Click(int button, Vector2 at)
		{
			_events.Enqueue(new Event
			{
				type = EventType.MouseDown,
				button = button,
				mousePosition = at,
				clickCount = 1
			});
			_events.Enqueue(new Event
			{
				type = EventType.MouseUp,
				button = button,
				mousePosition = at,
				clickCount = 1
			});
			while (_events.Count > 0)
			{
				yield return null;
			}
			yield return null;
			yield return null;
		}

		private IEnumerator Capture(string name, float wait = 0.5f)
		{
			if (wait > 0f)
			{
				yield return CivilTest.Wait(wait);
			}
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, name));
			for (int i = 0; i < 20; i++)
			{
				if (!Mod.HudForTest.CapturePending)
				{
					break;
				}
				yield return null;
			}
			Note("captured " + name);
		}

		private void OnGUI()
		{
			if (_events.Count == 0 || Mod == null || !Mod.WarMap.Open)
			{
				return;
			}
			Event current = Event.current;
			try
			{
				while (_events.Count > 0)
				{
					Event.current = _events.Dequeue();
					Mod.HudForTest.DrawWarMap();
				}
				Event.current = new Event
				{
					type = EventType.Layout,
					mousePosition = current.mousePosition
				};
				Mod.HudForTest.DrawWarMap();
			}
			catch (Exception ex)
			{
				Log.Error("WVIEW events: " + ex);
			}
			finally
			{
				Event.current = current;
			}
		}
	}
}
