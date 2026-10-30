using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class TentLookTest : CivilTest
	{
		private Actor _king;

		private City _city;

		protected override string Prefix => "TENTLOOK";

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
			try
			{
				((BaseSimObject)_king).addStatusEffect("invincible", 1200f, pColorEffect: false);
			}
			catch
			{
			}
			if (_city.countBuildingsType("type_bonfire") == 0)
			{
				BuildingAsset buildingAsset = FirstPerson.Towns.Village.BonfireOf(_king);
				if (buildingAsset != null)
				{
					AddHouse(buildingAsset.id, _city);
				}
			}
			string[] array;
			if (AddHouse("stockpile", _city) != null)
			{
				array = new string[4] { "wood", "stone", "gold", "common_metals" };
				foreach (string pResourceID in array)
				{
					try
					{
						_city.addResourcesToRandomStockpile(pResourceID, 80);
					}
					catch
					{
					}
				}
			}
			yield return Possess(_king, 5f);
			if (!Check("playing the king", InControl(_king)))
			{
				yield break;
			}
			Vector2 middle = ((_city.getTile() != null) ? ((Vector2)_city.getTile().posV3) : _king.current_position);
			_king.current_position = middle;
			yield return CivilTest.Wait(0.5f);
			array = new string[4] { "wood", "stone", "gold", "common_metals" };
			foreach (string pResourceID2 in array)
			{
				try
				{
					_city.addResourcesToRandomStockpile(pResourceID2, 80);
				}
				catch
				{
				}
			}
			Note("stores: " + _city.getResourcesAmount("wood") + " wood");
			Planning.Begin(_king, _city);
			if (!Check("planning", Planning.Aiming))
			{
				yield break;
			}
			Check("a tent on the bar", Planning.PickForTest("order_tent"));
			BuildingAsset asset = Planning.Asset;
			Note("planned asset " + asset?.id + ", family " + Models.BuildingFamily(asset));
			WorldTile spot = FreeSpot(asset, middle, 5f, 16f);
			if (!Check("land for the tent", spot != null))
			{
				yield break;
			}
			int num = ((asset.fundament != null) ? (asset.fundament.top / 2) : 0);
			Planning.AimForTest = World.world.GetTile(spot.x, spot.y + num) ?? spot;
			for (int j = 0; j < 4; j++)
			{
				yield return null;
			}
			Planning.ClickForTest = true;
			for (int j = 0; j < 4; j++)
			{
				yield return null;
			}
			Planning.AimForTest = null;
			Planning.Escape();
			Building b = spot.building;
			if (!Check("the plan is placed", b?.isUnderConstruction() ?? false, Planning.Why))
			{
				yield break;
			}
			Vector2 at = new Vector2((float)spot.x + 0.5f, (float)spot.y + 1f);
			LookFrom(at + new Vector2(-3f, -8f), at, 4f);
			yield return CivilTest.Wait(1f);
			Note("site drawn as " + Drawn(b));
			yield return Shot("tentlook_site.png");
			Mod.DebugEyeSet = false;
			Blueprints.Pay(_city, b, _king, out var _);
			for (int k = 0; k < 600; k++)
			{
				if (!b.isUnderConstruction())
				{
					break;
				}
				Blueprints.Blow(_king, b, 1f);
			}
			if (!Check("the tent stands", !b.isUnderConstruction() && b.isAlive()))
			{
				yield break;
			}
			List<KeyValuePair<string, bool>> list = new List<KeyValuePair<string, bool>>();
			string next;
			bool flag = HomeGrowth.Next(b, out next, list);
			Note("grows to " + next + ": " + string.Join("; ", list.ConvertAll((KeyValuePair<string, bool> keyValuePair) => (keyValuePair.Value ? "has " : "needs ") + keyValuePair.Key)) + " | hint: " + HomeGrowth.Hint(b));
			Check("the tent says what it grows to and what it needs", flag && next == "Cottage" && list.Count > 0 && HomeGrowth.SizeName(b) == "Tent");
			Deals();
			yield return Views(b, at, "built");
			_king.current_position = at + new Vector2(0f, -3f);
			yield return CivilTest.Wait(1f);
			Mod.EnterHouseForTest(b);
			yield return CivilTest.Wait(1.5f);
			Note("inside: " + Mod.Interior.Inside + ", fitted " + Mod.Interior.Fitted);
			yield return Shot("tentlook_inside.png");
			Mod.ExitHouseForTest();
			yield return CivilTest.Wait(1.5f);
			yield return Views(b, at, "after");
			if (!b.asset.id.StartsWith("tent", StringComparison.Ordinal))
			{
				Note("(grown by the town meanwhile: the Home card part needs a tent)");
				yield break;
			}
			Homes.SetOwner(b, _king);
			_king.current_position = at + new Vector2(0f, -3f);
			Mod.DebugEyeSet = false;
			HomeCard.Show(b, _king);
			yield return Shot("tentlook_homecard.png");
			string text = HomeGrowth.GrowNow(b);
			Check("you grow it to a cottage", text == null && b.asset != null && b.asset.id.StartsWith("house", StringComparison.Ordinal), text ?? b.asset?.id);
			HomeCard.Close();
			yield return Views(b, at, "grown");
			int n = 0;
			foreach (Building o in new List<Building>(_city.buildings))
			{
				if (o != null && o != b && o.isAlive() && o.asset != null && o.asset.id.StartsWith("tent", StringComparison.Ordinal))
				{
					Vector2 current_position = o.current_position;
					LookFrom(current_position + new Vector2(-3f, -8f), current_position, 4f);
					yield return CivilTest.Wait(1f);
					Note("town tent " + o.asset.id + " drawn as " + Drawn(o));
					if (n++ == 0)
					{
						yield return Shot("tentlook_towns.png");
					}
					if (n >= 3)
					{
						break;
					}
				}
			}
			Mod.DebugEyeSet = false;
		}

		private void Deals()
		{
			Actor actor = null;
			foreach (Actor unit in _city.units)
			{
				if (unit != null && unit != _king && unit.isAlive() && unit.isAdult())
				{
					actor = unit;
					break;
				}
			}
			if (Check("a townsperson to deal with", actor != null))
			{
				Check("no dealings yet today", Quests.DealsToday(actor) == 0 && !Quests.DoneForToday(actor));
				Quests.NoteDeal(actor);
				Quests.NoteDeal(actor);
				Check("two: still work for you", Quests.DealsToday(actor) == 2 && !Quests.DoneForToday(actor));
				Quests.NoteDeal(actor);
				Check("three: done for today", Quests.DoneForToday(actor), Quests.DealsToday(actor).ToString());
				try
				{
					actor.data.set("fp_deals", Calendar.BillDay - 1 + ":3");
				}
				catch
				{
				}
				Check("a new day: work again", Quests.DealsToday(actor) == 0 && !Quests.DoneForToday(actor));
			}
		}

		private IEnumerator Views(Building b, Vector2 at, string when)
		{
			float[] dist = new float[3] { 7f, 22f, 55f };
			string[] name = new string[3] { "near", "mid", "far" };
			for (int i = 0; i < dist.Length; i++)
			{
				LookFrom(at + new Vector2(-0.35f, -1f).normalized * dist[i], at, Mathf.Max(3f, dist[i] * 0.45f));
				yield return CivilTest.Wait(1f);
				string text = Drawn(b);
				Note(when + " " + name[i] + ": asset " + b.asset?.id + ", family " + Models.BuildingFamily(b) + ", drawn " + text);
				if (b.asset != null && b.asset.id.StartsWith("tent", StringComparison.Ordinal) && text != "-")
				{
					Check(when + " " + name[i] + ": a tent is drawn as a tent", text.StartsWith("tent", StringComparison.Ordinal) || text.StartsWith("site_tent", StringComparison.Ordinal), text);
				}
				yield return Shot("tentlook_" + when + "_" + name[i] + ".png");
			}
			Mod.DebugEyeSet = false;
		}

		private static string Drawn(Building b)
		{
			if (!HouseWorld.Of(b, out var p) || p.Model == null)
			{
				return "-";
			}
			return p.Model.Name;
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

		private void LookFrom(Vector2 eye, Vector2 at, float height)
		{
			CivilTest.ClearBox(Mathf.FloorToInt(Mathf.Min(eye.x, at.x)) - 2, Mathf.FloorToInt(Mathf.Min(eye.y, at.y)) - 2, Mathf.CeilToInt(Mathf.Max(eye.x, at.x)) + 2, Mathf.CeilToInt(Mathf.Max(eye.y, at.y)) + 2);
			Mod.DebugEye = eye;
			Mod.DebugEyeHeight = height;
			Mod.DebugEyeSet = true;
			Mod.SetYaw(Mathf.Atan2(at.y - eye.y, at.x - eye.x));
			Mod.SetPitchDegrees((0f - Mathf.Atan2(height, (at - eye).magnitude)) * 57.29578f);
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
