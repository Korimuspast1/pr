using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal sealed class LampsTest : CivilTest
	{
		private Actor _you;

		private Actor _king;

		private City _town;

		private Vector2 _home;

		private readonly List<PlacedLights.Light> _mine = new List<PlacedLights.Light>();

		protected override string Prefix => "LAMPS";

		private Hud H => Mod.HudForTest;

		protected override IEnumerator Body()
		{
			for (int i = 0; i < 4; i++)
			{
				Check("model " + PlacedLights.ModelNames[i], Models.Get(PlacedLights.ModelNames[i]) != null);
			}
			Check("bag icons", Supplies.Icon("tw_item_lamppost") != null && Supplies.Icon("tw_item_brazier") != null);
			Check("bar icons", SupplyIcons.Light(0) != null && SupplyIcons.Light(1) != null && SupplyIcons.Light(2) != null && SupplyIcons.Light(3) != null);
			Supplies.Kind kind = Supplies.Of("tw_item_lamppost");
			Supplies.Kind kind2 = Supplies.Of("tw_item_brazier");
			Check("a lamp post: 2 wood, 1 metal, by hand", kind != null && kind.Res1 == "wood" && kind.Amount1 == 2 && kind.Res2 == "common_metals" && kind.Amount2 == 1 && kind.Rank == 0);
			Check("a brazier: 3 stone, 1 metal, by hand", kind2 != null && kind2.Res1 == "stone" && kind2.Amount1 == 3 && kind2.Res2 == "common_metals" && kind2.Amount2 == 1 && kind2.Rank == 0);
			Check("the planning bar has the Lights", PlanPieces.Lights != null && PlanPieces.Lights.Pieces.Count == 4 && PlanPieces.All.Contains(PlanPieces.Lights));
			WorldTile worldTile = CivilTest.Stage();
			_king = ((worldTile != null) ? Village("human", 5, 1, worldTile) : Village("human", 5, 1));
			if (!Check("a village", _king != null))
			{
				yield break;
			}
			_town = _king.city;
			_you = Founded.Find((Actor u) => u != null && u != _king && u.isAdult() && u.city == _town && !Rep.Rules(u, _town));
			if (!Check("someone to be who leads no town", _you != null))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)_you).addStatusEffect("invincible", 900f, pColorEffect: false);
			}
			catch
			{
			}
			yield return Possess(_you);
			if (!Check("first person", InControl(_you)))
			{
				yield break;
			}
			foreach (Actor item in Founded)
			{
				if (item != null && item != _you && item.isAlive())
				{
					try
					{
						item.makeWait(300f);
					}
					catch
					{
					}
				}
			}
			bool gpu = Mod.Settings.GpuRender;
			int timeMode = Mod.Settings.TimeMode;
			if (Options.Contains("cpu"))
			{
				Mod.Settings.GpuRender = false;
			}
			Mod.Settings.TimeMode = 3;
			Mod.ForceRelight();
			Supplies.Clear(_you);
			Supplies.Give(_you, "tw_item_torch", 4);
			Supplies.Give(_you, "tw_item_lamppost", 2);
			Supplies.Give(_you, "tw_item_brazier", 1);
			_home = OpenGround(_you.current_position);
			CivilTest.ClearBox((int)_home.x - 7, (int)_home.y - 7, (int)_home.x + 7, (int)_home.y + 7);
			foreach (Actor item2 in Founded)
			{
				if (item2 != null && item2 != _you && item2.isAlive() && (item2.current_position - _home).sqrMagnitude < 196f)
				{
					try
					{
						item2.current_position = _home + new Vector2(0f, -16f);
						item2.dirty_current_tile = true;
					}
					catch
					{
					}
				}
			}
			_you.current_position = _home;
			_you.dirty_current_tile = true;
			yield return CivilTest.Wait(0.5f);
			yield return Part("Y opens the Lights", OpenLights());
			yield return Part("a standing torch", StandingTorch());
			yield return Part("a lamp post and a brazier", LampAndBrazier());
			yield return Part("drawn at night", AtNight());
			yield return Part("a wall torch", WallTorch());
			yield return Part("kept with the world", Kept());
			yield return Part("taken back", TakeBack());
			yield return Part("a house over one", HouseOver());
			yield return Part("towns put up their own", TownsOwn());
			Planning.LightPointForTest = null;
			Planning.LightPickedForTest = null;
			if (Planning.Aiming)
			{
				Planning.Escape();
			}
			Mod.DebugEyeSet = false;
			foreach (PlacedLights.Light item3 in _mine)
			{
				PlacedLights.Remove(item3);
			}
			Mod.Settings.GpuRender = gpu;
			Mod.Settings.TimeMode = timeMode;
			Mod.ForceRelight();
		}

		private IEnumerator OpenLights()
		{
			Check("you rule no town here", !Rep.Rules(_you, _town));
			Planning.Key(_you);
			yield return Frames(2);
			Check("Y opens the bar", Planning.Aiming && !Planning.Founding);
			Check("...with only the Lights", Planning.LightsOnly && Planning.Category == PlanPieces.Lights, Planning.Category?.Name);
			Check("...no town", Planning.Town == null);
		}

		private Vec3 Point(Vector2 at)
		{
			return new Vec3(at.x, at.y, Ground(at) + 0.02f);
		}

		private static float Ground(Vector2 at)
		{
			return FirstPersonAim.Terrain?.SampleHeight(at.x, at.y) ?? 0f;
		}

		private IEnumerator Aim(PlacedLights.Kind k, Vec3 at, BaseSimObject picked = null)
		{
			Planning.PickLightForTest(k);
			Planning.LightPointForTest = at;
			Planning.LightPickedForTest = picked;
			yield return Frames(3);
		}

		private IEnumerator StandingTorch()
		{
			Vector2 spot = _home + new Vector2(2.5f, 0.5f);
			int torches = Torches.Count(_you);
			int before = PlacedLights.All.Count;
			yield return Aim(PlacedLights.Kind.Torch, Point(spot));
			Check("its ghost is green on open ground", Planning.LightAiming && Planning.LightGhostShown && Planning.LightValid, Planning.Why);
			Check("the prompt says how many you carry", Planning.Prompt != null && Planning.Prompt.Contains("4 torches"), Planning.Prompt);
			Planning.ClickForTest = true;
			yield return Frames(3);
			PlacedLights.Light light = ((PlacedLights.All.Count > before) ? PlacedLights.All[PlacedLights.All.Count - 1] : null);
			Check("a click sets it down", light != null && light.Kind == PlacedLights.Kind.Torch, PlacedLights.All.Count.ToString());
			if (light != null)
			{
				_mine.Add(light);
				Check("...where you aimed", (new Vector2(light.X, light.Y) - spot).magnitude < 0.05f, $"{light.X:F2},{light.Y:F2}");
				Check("...yours", light.Owner == _you.data.id && light.City < 0);
				Check("...a torch out of your bag", Torches.Count(_you) == torches - 1, $"{torches} -> {Torches.Count(_you)}");
				string pResult = null;
				try
				{
					World.world.map_stats.custom_data.get("fp_lights", out pResult, null);
				}
				catch
				{
				}
				Check("...kept in the world's own data", pResult?.Contains("," + light.Owner + ",") ?? false, (pResult != null) ? (pResult.Length + " chars") : "none");
				yield return Aim(PlacedLights.Kind.Torch, Point(spot + new Vector2(0.2f, 0f)));
				Check("not right on top of it", !Planning.LightValid && Planning.Why == "Too close to another light", Planning.Why);
				WorldTile worldTile = WaterNear(_home, 8);
				if (worldTile != null)
				{
					yield return Aim(PlacedLights.Kind.Torch, new Vec3((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f, 0f));
					Check("not on water", !Planning.LightValid && Planning.Why == "Not on water", Planning.Why);
				}
				else
				{
					Note("no water near: skipped the water check");
				}
				yield return Aim(PlacedLights.Kind.Torch, Point(_home + new Vector2(30f, 0f)));
				Check("not too far off", !Planning.LightValid && Planning.Why == "Too far", Planning.Why);
			}
		}

		private IEnumerator LampAndBrazier()
		{
			int before = PlacedLights.All.Count;
			Vector2 lampAt = _home + new Vector2(0.5f, 2.5f);
			yield return Aim(PlacedLights.Kind.Lamp, Point(lampAt));
			Check("the lamp post's ghost is green", Planning.LightValid, Planning.Why + " (" + What(lampAt) + ")");
			Planning.ClickForTest = true;
			yield return Frames(3);
			yield return Aim(PlacedLights.Kind.Brazier, Point(_home + new Vector2(-1.5f, 2f)));
			Check("the brazier's ghost is green", Planning.LightValid, Planning.Why);
			Planning.ClickForTest = true;
			yield return Frames(3);
			Check("both set down", PlacedLights.All.Count == before + 2, $"{before} -> {PlacedLights.All.Count}");
			for (int i = before; i < PlacedLights.All.Count; i++)
			{
				_mine.Add(PlacedLights.All[i]);
			}
			Check("a lamp post and the brazier out of the bag", PlacedLights.Carried(_you, PlacedLights.Kind.Lamp) == 1 && PlacedLights.Carried(_you, PlacedLights.Kind.Brazier) == 0, $"lamp posts {PlacedLights.Carried(_you, PlacedLights.Kind.Lamp)}, braziers {PlacedLights.Carried(_you, PlacedLights.Kind.Brazier)}");
			yield return Aim(PlacedLights.Kind.Brazier, Point(_home + new Vector2(-4f, 3f)));
			Check("no brazier left: says how to make one", !Planning.LightValid && Planning.Why != null && Planning.Why.StartsWith("You have no brazier", StringComparison.Ordinal), Planning.Why);
		}

		private IEnumerator AtNight()
		{
			yield return Aim(PlacedLights.Kind.Torch, Point(_home + new Vector2(2f, 3.5f)));
			Vector2 at = _home + new Vector2(0.5f, 1.5f);
			Look(at, 5.5f, -10f);
			yield return CivilTest.Wait(1.2f);
			Check("drawn", PlacedLights.DrawnForTest >= 3, PlacedLights.DrawnForTest.ToString());
			Check("burning", PlacedLights.FlamesForTest >= 3, PlacedLights.FlamesForTest + " flames");
			Check("lighting the night", PlacedLights.LitForTest >= 3, PlacedLights.LitForTest + " lights");
			yield return Shot("lamps_night.png");
			Mod.SaveFrame("lamps_night_frame.png");
			Mod.Settings.TimeMode = 2;
			Mod.ForceRelight();
			yield return CivilTest.Wait(1f);
			yield return Shot("lamps_dusk.png");
			Mod.Settings.TimeMode = 3;
			Mod.ForceRelight();
			PlacedLights.Light light = _mine.Find((PlacedLights.Light l) => l.Kind == PlacedLights.Kind.Torch);
			if (light != null)
			{
				Check("its flame is known", light.FlameKnown, $"{light.FlameAt.X:F2},{light.FlameAt.Y:F2},{light.FlameAt.Z:F2}");
				TerrainCache tc = FirstPersonAim.Terrain;
				Check("a hand torch held to it catches", Torches.FireAt(light.FlameAt + new Vec3(0f, 0f, 0.03f), 0.08f, (float x, float y) => (tc == null) ? 0f : tc.SampleHeight(x, y)));
			}
			PlacedLights.Light light2 = _mine.Find((PlacedLights.Light l) => l.Kind == PlacedLights.Kind.Lamp);
			if (light2 != null)
			{
				Check("...but not a lantern behind its glass", !PlacedLights.FireNear(light2.FlameAt, 0.08f));
			}
			Mod.DebugEyeSet = false;
		}

		private IEnumerator WallTorch()
		{
			Building house = AddHouse("house_human_1", _town) ?? AddHouse("house_human_0", _town);
			if (!Check("a house to hang it on", house != null))
			{
				yield break;
			}
			Vector2 at = house.current_position;
			Vector2 current_position = at + new Vector2(0.3f, -4f);
			_you.current_position = current_position;
			_you.dirty_current_tile = true;
			Mod.SetYaw(Mathf.Atan2(at.y - current_position.y, at.x - current_position.x));
			Vec3? vec = null;
			float until = Time.unscaledTime + 8f;
			while (!vec.HasValue && Time.unscaledTime < until)
			{
				yield return null;
				vec = PlacedLights.DrawnMiddleForTest(house);
			}
			if (Check("the house drawn as a model near you", vec.HasValue, $"{house.asset.id} at {at.x:F1},{at.y:F1}, you {(_you.current_position - at).magnitude:F1} off"))
			{
				Vec3 m = vec.Value;
				Vec3 at2 = new Vec3(m.X + 0.3f, m.Y, Ground(new Vector2(m.X, m.Y)) + 0.85f);
				yield return Aim(PlacedLights.Kind.Wall, at2, house);
				Check("its ghost hangs on the wall, green", Planning.LightGhostShown && Planning.LightValid, Planning.Why);
				float num = Planning.LightAt.Z - Ground(new Vector2(Planning.LightAt.X, Planning.LightAt.Y));
				Check("...on the wall that faces you, out toward you", Mathf.Abs(Mathf.DeltaAngle(Planning.LightHeading * 57.29578f, -90f)) < 50f && num > 0.5f && num < 1.3f, $"heading {Planning.LightHeading * 57.29578f:F0} deg, {num:F2} up, at {Planning.LightAt.Y - m.Y:F2} from the middle");
				int before = PlacedLights.All.Count;
				int torches = Torches.Count(_you);
				Planning.ClickForTest = true;
				yield return Frames(3);
				PlacedLights.Light l = ((PlacedLights.All.Count > before) ? PlacedLights.All[PlacedLights.All.Count - 1] : null);
				if (Check("a click hangs it", l != null && l.Kind == PlacedLights.Kind.Wall && l.Building == house.data.id, (l != null) ? (l.Kind.ToString() + " on " + l.Building) : "none"))
				{
					_mine.Add(l);
					Check("...a torch out of your bag", Torches.Count(_you) == torches - 1);
					yield return Frames(3);
					Check("drawn where the house puts it", l.PosedFrame >= 0 && Time.frameCount > 0 && l.FlameKnown, $"posed {l.PosedFrame}");
					Look(new Vector2(l.X, l.Y) + new Vector2(0f, -0.4f), 3.2f, -4f);
					yield return CivilTest.Wait(1f);
					yield return Shot("lamps_wall.png");
					Mod.SaveFrame("lamps_wall_frame.png");
					Mod.DebugEyeSet = false;
					string drawn = l.On;
					l.On = (("house_human_0" == drawn) ? "house_human_2" : "house_human_0");
					yield return Frames(4);
					Check("seated again on the model drawn", l.On == drawn, l.On + " (drawn " + drawn + ")");
					Vec3 vec2 = new Vec3(l.X, l.Y, l.Z);
					Check("...still on its wall", Mathf.Abs(vec2.Y - Planning.LightAt.Y) < 0.6f, $"{vec2.Y - Planning.LightAt.Y:F2}");
					yield return Aim(PlacedLights.Kind.Wall, new Vec3(m.X + 0.3f, m.Y, Ground(new Vector2(m.X, m.Y)) + 2.8f), house);
					Check("not up under the roof", !Planning.LightValid, Planning.Why);
				}
			}
		}

		private IEnumerator Kept()
		{
			int n = PlacedLights.All.Count;
			List<PlacedLights.Kind> kinds = new List<PlacedLights.Kind>();
			foreach (PlacedLights.Light item in PlacedLights.All)
			{
				kinds.Add(item.Kind);
			}
			PlacedLights.ReloadForTest();
			yield return null;
			bool flag = PlacedLights.All.Count == n;
			int num = 0;
			while (flag && num < n)
			{
				flag = PlacedLights.All[num].Kind == kinds[num];
				num++;
			}
			Check("read again from the world, all there", flag, $"{n} -> {PlacedLights.All.Count}");
			HashSet<int> hashSet = new HashSet<int>();
			foreach (PlacedLights.Light item2 in _mine)
			{
				hashSet.Add(item2.Id);
			}
			_mine.Clear();
			foreach (PlacedLights.Light item3 in PlacedLights.All)
			{
				if (hashSet.Contains(item3.Id))
				{
					_mine.Add(item3);
				}
			}
		}

		private IEnumerator TakeBack()
		{
			PlacedLights.Light lamp = _mine.Find((PlacedLights.Light l) => l.Kind == PlacedLights.Kind.Lamp);
			if (Check("your lamp post", lamp != null))
			{
				_you.current_position = _home + new Vector2(0.5f, -1.5f);
				_you.dirty_current_tile = true;
				int had = PlacedLights.Carried(_you, PlacedLights.Kind.Lamp);
				float num = Ground(new Vector2(lamp.X, lamp.Y));
				yield return Aim(PlacedLights.Kind.Torch, new Vec3(lamp.X, lamp.Y, num + 0.6f));
				Check("the crosshair on it", Planning.AimedLight == lamp, (Planning.AimedLight != null) ? Planning.AimedLight.Kind.ToString() : "none");
				Check("...the prompt offers it back", Planning.Prompt != null && Planning.Prompt.Contains("RMB:Take back the lamp post"), Planning.Prompt);
				Planning.RightClickForTest = true;
				yield return Frames(3);
				Check("a right-click takes it back", !PlacedLights.All.Contains(lamp));
				Check("...into your bag", PlacedLights.Carried(_you, PlacedLights.Kind.Lamp) == had + 1, $"{had} -> {PlacedLights.Carried(_you, PlacedLights.Kind.Lamp)}");
				_mine.Remove(lamp);
				Vector2 at = _home + new Vector2(-2.5f, -1.5f);
				PlacedLights.Light theirs = PlacedLights.Add(_king, PlacedLights.Kind.Torch, at, 0f, null);
				_mine.Add(theirs);
				yield return Aim(PlacedLights.Kind.Torch, new Vec3(at.x, at.y, Ground(at) + 0.4f));
				Check("someone else's: not yours to take", Planning.AimedLight == theirs && Planning.Prompt.Contains("Not yours to take"), Planning.Prompt);
				Planning.RightClickForTest = true;
				yield return Frames(3);
				Check("...and it stays", PlacedLights.All.Contains(theirs));
			}
		}

		private IEnumerator HouseOver()
		{
			Vector2 at = _home + new Vector2(4.5f, -3.5f);
			PlacedLights.Light l = PlacedLights.Add(_you, PlacedLights.Kind.Torch, at, 0f, null);
			int torches = Torches.Count(_you);
			WorldTile tile = World.world.GetTile((int)at.x, (int)at.y);
			Building b = null;
			try
			{
				if (tile?.building != null)
				{
					tile.building.removeBuildingFinal();
				}
				b = World.world.buildings.addBuilding("house_human_0", tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
			}
			catch
			{
			}
			if (!Check("a house over it", b != null && tile.building == b))
			{
				PlacedLights.Remove(l);
				yield break;
			}
			try
			{
				b.completeConstruction();
			}
			catch
			{
			}
			float until = Time.unscaledTime + 4f;
			while (PlacedLights.All.Contains(l) && Time.unscaledTime < until)
			{
				yield return null;
			}
			Check("the torch fell", !PlacedLights.All.Contains(l));
			Check("...back in your bag, you being near", Torches.Count(_you) == torches + 1, $"{torches} -> {Torches.Count(_you)}");
			try
			{
				b.removeBuildingFinal();
			}
			catch
			{
			}
		}

		private IEnumerator TownsOwn()
		{
			Check("a human town puts up lamp posts", TownLights.KindFor(_town) == PlacedLights.Kind.Lamp);
			WorldTile tile = _town.getTile();
			if (tile != null)
			{
				CivilTest.ClearBox(tile.x - 16, tile.y - 6, tile.x + 16, tile.y);
			}
			int laid = 0;
			for (int i = -14; i <= 14; i++)
			{
				if (tile == null)
				{
					break;
				}
				WorldTile tile2 = World.world.GetTile(tile.x + i, tile.y - 3);
				if (tile2 != null && tile2.Type != null && tile2.Type.ground && !tile2.Type.liquid && tile2.zone != null && tile2.zone.city == _town && (tile2.building == null || !AssetStyle.IsSolid(tile2.building)))
				{
					try
					{
						MapAction.terraformTop(tile2, TopTileLibrary.road, TerraformLibrary.draw);
						laid++;
					}
					catch
					{
					}
				}
			}
			Note("road tiles laid: " + laid);
			if (!_town.hasStorages())
			{
				Note("a stockpile: " + (AddHouse("stockpile", _town)?.asset?.id ?? "none"));
				yield return CivilTest.Wait(1.5f);
				Note("stores now: " + _town.hasStorages());
			}
			try
			{
				_town.addResourcesToRandomStockpile("wood", 20);
				_town.addResourcesToRandomStockpile("common_metals", 6);
			}
			catch (Exception ex)
			{
				Note("stores: " + ex.Message);
			}
			int metal = 0;
			try
			{
				metal = _town.getResourcesAmount("common_metals");
			}
			catch
			{
			}
			int before = PlacedLights.All.Count;
			bool put = TownLights.LookForTest(_town);
			yield return null;
			PlacedLights.Light light = ((PlacedLights.All.Count > before) ? PlacedLights.All[PlacedLights.All.Count - 1] : null);
			if (!Check("the town put one up", put && light != null, $"{laid} roads, metal {metal}: {TownLights.WhyNoneForTest}"))
			{
				yield break;
			}
			_mine.Add(light);
			Check("...a lamp post, the town's own", light.Kind == PlacedLights.Kind.Lamp && light.Owner < 0 && light.City == _town.data.id, $"{light.Kind} owner {light.Owner}");
			int num = 0;
			try
			{
				num = _town.getResourcesAmount("common_metals");
			}
			catch
			{
			}
			Check("...paid from its stores", num == metal - 1, $"{metal} -> {num}");
			bool ok = false;
			for (int j = -1; j <= 1; j++)
			{
				for (int k = -1; k <= 1; k++)
				{
					WorldTile tile3 = World.world.GetTile((int)light.X + j, (int)light.Y + k);
					if (tile3 != null && tile3.Type != null && tile3.Type.road && (j == 0 || k == 0))
					{
						ok = true;
					}
				}
			}
			Check("...at a roadside", ok, $"{light.X:F1},{light.Y:F1}");
			PlacedLights.Light light2 = (TownLights.LookForTest(_town) ? PlacedLights.All[PlacedLights.All.Count - 1] : null);
			if (light2 != null)
			{
				_mine.Add(light2);
				Check("the next a road's length away", new Vector2(light2.X - light.X, light2.Y - light.Y).magnitude >= 6f, $"{new Vector2(light2.X - light.X, light2.Y - light.Y).magnitude:F1}");
			}
			else
			{
				Note("no second light (the street wants " + laid / 9 + ")");
			}
			Check("you may take your town's lights only if you rule it", !PlacedLights.MayTake(_you, light) && PlacedLights.MayTake(_town.kingdom?.king ?? _king, light));
			Look(new Vector2(light.X, light.Y), 5f, -8f);
			yield return CivilTest.Wait(1f);
			yield return Shot("lamps_town.png");
			Mod.DebugEyeSet = false;
		}

		private static string What(Vector2 at)
		{
			try
			{
				WorldTile tile = World.world.GetTile((int)at.x, (int)at.y);
				return (tile == null) ? "off the map" : ((tile.building != null) ? (tile.building.asset.id + " (" + tile.building.asset.building_type.ToString() + ")") : ("nothing on " + tile.Type?.id));
			}
			catch
			{
				return "?";
			}
		}

		private static IEnumerator Frames(int n)
		{
			for (int i = 0; i < n; i++)
			{
				yield return null;
			}
		}

		private static Vector2 OpenGround(Vector2 from)
		{
			for (float num = 8f; num < 26f; num += 1.5f)
			{
				for (int i = 0; i < 16; i++)
				{
					float f = (float)i * MathF.PI / 8f;
					Vector2 vector = from + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num;
					bool flag = true;
					for (int j = -5; j <= 5 && flag; j++)
					{
						for (int k = -5; k <= 5 && flag; k++)
						{
							WorldTile tile = World.world.GetTile((int)vector.x + k, (int)vector.y + j);
							if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block || tile.Type.road || (tile.building != null && AssetStyle.IsSolid(tile.building)))
							{
								flag = false;
							}
						}
					}
					if (flag)
					{
						return new Vector2((float)(int)vector.x + 0.5f, (float)(int)vector.y + 0.5f);
					}
				}
			}
			return from;
		}

		private static WorldTile WaterNear(Vector2 at, int reach)
		{
			for (int i = 2; i <= reach; i++)
			{
				for (int j = 0; j < 24; j++)
				{
					WorldTile tile = World.world.GetTile(Mathf.RoundToInt(at.x + Mathf.Cos((float)j * 0.2618f) * (float)i), Mathf.RoundToInt(at.y + Mathf.Sin((float)j * 0.2618f) * (float)i));
					if (tile != null && tile.Type != null && tile.Type.liquid)
					{
						return tile;
					}
				}
			}
			return null;
		}

		private void Look(Vector2 at, float from, float pitch)
		{
			Vector2 debugEye = at + new Vector2(0f, -1f) * from;
			Mod.DebugEye = debugEye;
			Mod.DebugEyeSet = true;
			Mod.SetYaw(Mathf.Atan2(at.y - debugEye.y, at.x - debugEye.x));
			Mod.SetPitchDegrees(pitch);
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
