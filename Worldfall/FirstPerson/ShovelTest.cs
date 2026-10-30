using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class ShovelTest : CivilTest
	{
		private Actor _me;

		private WorldSampler _s;

		protected override string Prefix => "SHOVEL";

		protected override IEnumerator Body()
		{
			Settings settings = Mod.Settings;
			settings.TimeMode = 1;
			if (Options.Contains("cpu"))
			{
				settings.GpuRender = false;
			}
			Mod.ForceRelight();
			SiteLevel.HoldForTest = true;
			_me = Village("human", 10, 0);
			if (Check("a village to dig in", _me != null))
			{
				yield return CivilTest.Wait(2f);
				yield return Part("kit", Kit());
				try
				{
					ControllableUnit.setControllableCreature(_me);
				}
				catch (Exception ex)
				{
					Note("possess: " + ex.Message);
				}
				yield return CivilTest.Wait(3f);
				if (Check("first person", Mod.IsFirstPerson))
				{
					_s = Mod.SamplerForTest;
					yield return Part("hint", Hint());
					yield return Part("size", Size());
					yield return Part("patch", PatchDig());
					yield return Part("levels", ToolLevels());
					yield return Part("level", LevelMove());
					yield return Part("fill", FillMove());
					yield return Part("water", WaterFill());
					yield return Part("harbour", HarbourFill());
					Mod.DebugEyeSet = false;
					Earthworks.Mode = Earthworks.Move.Dig;
					Earthworks.Size = 1;
					SiteLevel.HoldForTest = false;
				}
			}
		}

		private IEnumerator Kit()
		{
			Actor farmer = null;
			Actor miner = null;
			Actor idle = null;
			foreach (Actor unit in World.world.units)
			{
				if (unit != null && unit.isAlive() && unit.city == _me.city && unit.isAdult() && unit != _me && !unit.isKing() && !unit.isCityLeader())
				{
					if (farmer == null)
					{
						farmer = Job(unit, "farmer");
					}
					else if (miner == null)
					{
						miner = Job(unit, "miner");
					}
					else if (idle == null && unit.citizen_job == null)
					{
						idle = unit;
					}
				}
			}
			yield return CivilTest.Wait(0.2f);
			if (farmer != null)
			{
				Check("a farmer or a builder carries a shovel", Supplies.Count(farmer, "tw_item_shovel") == 1, farmer.citizen_job?.id);
			}
			else
			{
				Note("no farmer or builder yet: their shovel not checked");
			}
			if (miner != null)
			{
				Check("a miner carries a pickaxe", Supplies.Count(miner, "tw_item_pickaxe") == 1);
			}
			else
			{
				Note("no miner yet: their pickaxe not checked");
			}
			if (idle != null)
			{
				Check("one with no job carries no digging tool", Supplies.Count(idle, "tw_item_shovel") == 0 && Supplies.Count(idle, "tw_item_pickaxe") == 0);
			}
			Check("a reworked tool isn't sold or made", Supplies.Of(Supplies.Leveled("tw_item_shovel", 3))?.Res1 == null && Supplies.Of(Supplies.Leveled("tw_item_shovel", 3)).Level == 3);
			Check("a reworked tool has its icon", SupplyIcons.Get(Supplies.Leveled("tw_item_pickaxe", 4)) != null);
		}

		private IEnumerator Hint()
		{
			Mod.Settings.DigSeen = false;
			SetXp(0);
			Supplies.Give(_me, "tw_item_shovel", 1);
			Supplies.Give(_me, "tw_item_dirt", 30);
			yield return CivilTest.Wait(0.3f);
			int num = BagOrder.Of(_me).FindIndex((string e) => BagOrder.ResourceOf(e) == "tw_item_shovel");
			if (Check("a shovel in the bag", num >= 0))
			{
				if (num >= Hotbar.Size(_me))
				{
					BagOrder.Swap(_me, num, 0);
					num = 0;
				}
				Hotbar.Choose(_me, num, wheel: false);
				Mod.Holstered = false;
				yield return CivilTest.Wait(0.6f);
				Check("the shovel is in hand", Earthworks.ToolOf(_me) == "tw_item_shovel" && Earthworks.LevelOf(_me) == 1);
				Check("the first time, how digging works", Mod.Settings.DigSeen && Mod.ToastText != null && Mod.ToastText.Contains("R switches"), Mod.ToastText);
				Earthworks.Mode = Earthworks.Move.Dig;
				List<Earthworks.Move> seen = new List<Earthworks.Move>();
				for (int i = 0; i < 2; i++)
				{
					PlayInput.TestPress(KeyCode.R);
					yield return null;
					yield return null;
					PlayInput.TestRelease(KeyCode.R);
					yield return null;
					seen.Add(Earthworks.Mode);
				}
				Check("R switches dig and fill", seen.Count == 2 && seen[0] == Earthworks.Move.Fill && seen[1] == Earthworks.Move.Dig, string.Join(",", seen));
			}
		}

		private IEnumerator Size()
		{
			Check("the skill sets the widest patch", MaxAt(0) == 1 && MaxAt(999) == 1 && MaxAt(1000) == 2 && MaxAt(2000) == 3 && MaxAt(3000) == 4, $"{MaxAt(0)} {MaxAt(999)} {MaxAt(1000)} {MaxAt(2000)} {MaxAt(3000)}");
			SetXp(0);
			Earthworks.Size = 1;
			int chosen = Hotbar.Chosen(_me);
			WorldBoxMod.HotbarWheelForTest = 1f;
			yield return CivilTest.Wait(0.2f);
			Check("a novice's wheel stays at one tile", Earthworks.Size == 1, Earthworks.Size.ToString());
			Check("...and keeps the shovel in hand", Hotbar.Chosen(_me) == chosen && Earthworks.ToolOf(_me) == "tw_item_shovel");
			Check("...saying why", Mod.ToastText != null && Mod.ToastText.Contains("Dig more"), Mod.ToastText);
			SetXp(3000);
			for (int i = 0; i < 4; i++)
			{
				WorldBoxMod.HotbarWheelForTest = 1f;
				yield return CivilTest.Wait(0.1f);
			}
			Check("a master's wheel goes to four", Earthworks.Size == 4, Earthworks.Size.ToString());
			WorldBoxMod.HotbarWheelForTest = -1f;
			yield return CivilTest.Wait(0.1f);
			Check("...and back down", Earthworks.Size == 3, Earthworks.Size.ToString());
		}

		private IEnumerator TimeSwings(float seconds, List<float> at)
		{
			int last = Earthworks.Swings;
			Earthworks.ForceDig = true;
			float t0 = Time.unscaledTime;
			while (Time.unscaledTime - t0 < seconds)
			{
				yield return null;
				if (Earthworks.Swings != last)
				{
					last = Earthworks.Swings;
					at.Add(Time.unscaledTime);
				}
			}
			Earthworks.ForceDig = false;
		}

		private static float MeanGap(List<float> at)
		{
			float num = 0f;
			int num2 = 0;
			for (int i = 1; i < at.Count; i++)
			{
				if (at[i] - at[i - 1] < 1f)
				{
					num += at[i] - at[i - 1];
					num2++;
				}
			}
			if (num2 < 2)
			{
				return 0f;
			}
			return num / (float)num2;
		}

		private static Actor Job(Actor a, string id)
		{
			CitizenJobAsset citizenJobAsset = null;
			try
			{
				citizenJobAsset = AssetManager.citizen_job_library.get(id);
			}
			catch
			{
			}
			if (citizenJobAsset == null)
			{
				return null;
			}
			try
			{
				a.setCitizenJob(citizenJobAsset);
			}
			catch
			{
				return null;
			}
			if (a.citizen_job != citizenJobAsset)
			{
				return null;
			}
			return a;
		}

		private int MaxAt(int xp)
		{
			SetXp(xp);
			return Earthworks.MaxSize(_me);
		}

		private IEnumerator PatchDig()
		{
			SetXp(3000);
			Earthworks.Mode = Earthworks.Move.Dig;
			Earthworks.Size = 3;
			WorldTile c = null;
			for (int tries = 0; tries < 4; tries++)
			{
				WorldTile worldTile = OpenSoil(5, 4f + (float)tries * 6f);
				if (worldTile == null)
				{
					break;
				}
				c = World.world.GetTile(worldTile.x + 2, worldTile.y + 2);
				yield return Stand(c, 1.8f);
				yield return Aim(Mid(c));
				if (Mod.Prompt != null && Mod.Prompt.Contains("Dig"))
				{
					break;
				}
				Note($"no digging at {c.x},{c.y} ({Mod.Prompt}; {c.building?.asset?.id}): another");
			}
			if (!Check("open soil to dig", c != null))
			{
				yield break;
			}
			Check("the patch is marked on the ground", Earthworks.Preview.Count == 9, Earthworks.Preview.Count + " tiles");
			Check("the prompt says how wide", Mod.Prompt != null && Mod.Prompt.Contains("3 wide") && Mod.Prompt.Contains("R:Fill") && Mod.Prompt.Contains("RMB:Level"), Mod.Prompt);
			WorldTile worldTile2 = null;
			foreach (var item in Earthworks.Preview)
			{
				var (worldTile3, _) = item;
				if (item.w >= 0.999f)
				{
					worldTile2 = worldTile3;
				}
			}
			if (worldTile2 != null)
			{
				c = worldTile2;
			}
			Dictionary<WorldTile, float> before = new Dictionary<WorldTile, float>();
			for (int i = -1; i <= 1; i++)
			{
				for (int j = -1; j <= 1; j++)
				{
					WorldTile tile = World.world.GetTile(c.x + j, c.y + i);
					before[tile] = Earthworks.WorkHeight(tile);
				}
			}
			int clods = SceneCollector.ClodsThrown;
			int swings = Earthworks.Swings;
			int ci = c.y * _s.Cache.Width + c.x;
			float drawn0 = _s.Cache.Ground[ci];
			uint color0 = _s.Cache.BaseColors[ci];
			Mod.SaveFrame("shovel_ready.png");
			Earthworks.ForceDig = true;
			yield return CivilTest.Wait(0.6f);
			Earthworks.ForceDig = false;
			yield return CivilTest.Wait(0.2f);
			Dictionary<WorldTile, float> dictionary = new Dictionary<WorldTile, float>();
			foreach (KeyValuePair<WorldTile, float> item2 in before)
			{
				dictionary[item2.Key] = (item2.Value - Earthworks.WorkHeight(item2.Key)) / Earthworks.BandThickness(item2.Key, down: true);
			}
			float num = 0f;
			foreach (KeyValuePair<WorldTile, float> item3 in dictionary)
			{
				if (item3.Key != c)
				{
					num += item3.Value / 8f;
				}
			}
			Check("...deepest in the middle (its share of a band)", dictionary[c] > num * 1.2f, $"middle {dictionary[c]:F2}, rim {num:F2} of a band");
			Earthworks.ForceDig = true;
			yield return CivilTest.Wait(0.3f);
			Mod.SaveFrame("shovel_scoop.png");
			yield return CivilTest.Wait(1.5f);
			Earthworks.ForceDig = false;
			yield return CivilTest.Wait(0.6f);
			Check("the drawn ground went down", _s.Cache.Ground[ci] < drawn0 - 0.1f, $"{drawn0:F2} -> {_s.Cache.Ground[ci]:F2}");
			string text = c.top_type?.id ?? "";
			if (text.Contains("desert") || text.Contains("sand") || Earthworks.LandOf(c).sand)
			{
				Note("turned earth not checked: " + text + " stays sand when dug");
			}
			else
			{
				Check("...turned earth showing", _s.Cache.BaseColors[ci] != color0, $"{color0:X8} -> {_s.Cache.BaseColors[ci]:X8}, {c.main_type?.id}/{text}, raw {_s.Relief.RawOf(ci):F2}");
			}
			int num2 = 0;
			float num3 = 0f;
			int num4 = 0;
			foreach (KeyValuePair<WorldTile, float> item4 in before)
			{
				float num5 = item4.Value - Earthworks.WorkHeight(item4.Key);
				if (num5 > 0.01f)
				{
					num2++;
				}
				if (item4.Key != c)
				{
					num3 += num5;
					num4++;
				}
			}
			float num6 = before[c] - Earthworks.WorkHeight(c);
			Check("a 3-wide swing digs nine tiles", num2 >= 8, $"{num2} of 9, {Earthworks.Swings - swings} swings, middle {num6:F2} down, rim {num3 / (float)Math.Max(1, num4):F2}");
			Check("...throwing clods", SceneCollector.ClodsThrown > clods, SceneCollector.ClodsThrown - clods + " swings");
			Mod.SaveFrame("shovel_patch.png");
		}

		private IEnumerator ToolLevels()
		{
			Earthworks.Mode = Earthworks.Move.Dig;
			Earthworks.Size = 1;
			WorldTile c = OpenSoil(3, 4f);
			if (Check("more open soil", c != null))
			{
				c = World.world.GetTile(c.x + 1, c.y + 1);
				yield return Stand(c, 1.7f);
				yield return Aim(Mid(c));
				List<float> plainAt = new List<float>();
				yield return TimeSwings(4f, plainAt);
				float plain = MeanGap(plainAt);
				Dialogue.Rework(_me, "tw_item_shovel", Supplies.Leveled("tw_item_shovel", 4));
				yield return CivilTest.Wait(0.4f);
				Check("reworked, it stays in hand", Earthworks.ToolOf(_me) == "tw_item_shovel" && Earthworks.LevelOf(_me) == 4, $"{Hotbar.InHand(_me)}, level {Earthworks.LevelOf(_me)}");
				Check("...the old one gone", Supplies.Count(_me, "tw_item_shovel") == 0 && Supplies.Count(_me, Supplies.Leveled("tw_item_shovel", 4)) == 1);
				Check("...each swing moves more", Earthworks.SwingsFor(_me, "tw_item_shovel", rock: false) < 4.3f, Earthworks.SwingsFor(_me, "tw_item_shovel", rock: false).ToString("F2"));
				WorldTile c2 = World.world.GetTile(c.x, c.y + 1);
				yield return Stand(c2, 1.7f);
				yield return Aim(Mid(c2));
				List<float> quickAt = new List<float>();
				yield return TimeSwings(4f, quickAt);
				float num = MeanGap(quickAt);
				Check("level 4 swings quicker", plain > 0f && num > 0f && num < plain / 1.25f && Earthworks.SwingTime(4) < Earthworks.SwingTime(1) / 1.35f, $"a swing every {plain:F2} s -> {num:F2} s ({plainAt.Count} and {quickAt.Count} swings)");
			}
		}

		private IEnumerator LevelMove()
		{
			Earthworks.Mode = Earthworks.Move.Dig;
			Earthworks.Size = 3;
			WorldTile c = OpenSoil(5, 4f);
			if (!Check("open soil to level", c != null))
			{
				yield break;
			}
			c = World.world.GetTile(c.x + 2, c.y + 2);
			yield return Stand(c, 1.9f);
			WorldTile tile = World.world.GetTile((int)_me.current_position.x, (int)_me.current_position.y);
			float target = Earthworks.WorkHeight(tile);
			int num = 0;
			for (int i = -1; i <= 1; i++)
			{
				for (int j = -1; j <= 1; j++)
				{
					WorldTile tile2 = World.world.GetTile(c.x + j, c.y + i);
					bool fill = num++ % 2 == 0 && !Earthworks.IsRock(Earthworks.LandOf(tile2).increase_to);
					Earthworks.Apply(tile2, fill, 0.6f, capped: false);
				}
			}
			Supplies.Give(_me, "tw_item_dirt", 200);
			yield return Aim(Mid(c));
			Check("the prompt says level", Mod.Prompt != null && Mod.Prompt.Contains("RMB:Level"), Mod.Prompt);
			int dirt0 = Civil.BagCount(_me, "tw_item_dirt");
			int s0 = Earthworks.Swings;
			Earthworks.ForceLevel = true;
			yield return CivilTest.Wait(0.25f);
			Earthworks.ForceLevel = false;
			yield return CivilTest.Wait(0.3f);
			int num2 = 0;
			List<string> list = new List<string>();
			for (int k = -1; k <= 1; k++)
			{
				for (int l = -1; l <= 1; l++)
				{
					WorldTile tile3 = World.world.GetTile(c.x + l, c.y + k);
					float num3 = Earthworks.WorkHeight(tile3);
					if (Mathf.Abs(num3 - target) < 0.06f)
					{
						num2++;
					}
					else
					{
						list.Add(string.Format("{0:+0.00;-0.00}{1}", num3 - target, Earthworks.IsRock(Earthworks.LandOf(tile3)) ? " rock" : ""));
					}
				}
			}
			Check("one swing levels the patch to your feet", Earthworks.Swings - s0 == 1 && num2 >= 8, string.Format("{0} of 9 level, {1} swings, off by {2}", num2, Earthworks.Swings - s0, string.Join(" ", list)));
			Check("...its dirt settled from the bag", Civil.BagCount(_me, "tw_item_dirt") != dirt0, string.Format("{0} -> {1}", dirt0, Civil.BagCount(_me, "tw_item_dirt")));
			Dictionary<WorldTile, float> low = new Dictionary<WorldTile, float>();
			for (int m = -1; m <= 1; m++)
			{
				for (int n = -1; n <= 1; n++)
				{
					WorldTile tile4 = World.world.GetTile(c.x + n, c.y + m);
					Earthworks.Apply(tile4, fill: false, 0.9f, capped: false);
					low[tile4] = Earthworks.WorkHeight(tile4);
				}
			}
			int num4 = Civil.BagCount(_me, "tw_item_dirt");
			if (num4 > 2)
			{
				Civil.TakeFromBag(_me, "tw_item_dirt", num4 - 2);
			}
			yield return Aim(Mid(c));
			string prompt = Mod.Prompt;
			int s1 = Earthworks.Swings;
			Earthworks.ForceLevel = true;
			yield return CivilTest.Wait(0.25f);
			Earthworks.ForceLevel = false;
			yield return CivilTest.Wait(0.3f);
			float num5 = 0f;
			foreach (KeyValuePair<WorldTile, float> item in low)
			{
				float num6 = Earthworks.WorkHeight(item.Key) - item.Value;
				if (num6 > 0f)
				{
					num5 += num6 / Earthworks.BandThickness(item.Key, down: false);
				}
			}
			Check("with two dirt, it fills only a band's worth", num5 > 0.05f && num5 <= 2f / Earthworks.PerBand + 0.35f, string.Format("raised {0:F2} bands, {1} dirt left, {2} swings, prompt '{3}', toast '{4}'", num5, Civil.BagCount(_me, "tw_item_dirt"), Earthworks.Swings - s1, prompt, Mod.ToastText));
			Mod.SaveFrame("shovel_level.png");
		}

		private IEnumerator FillMove()
		{
			Earthworks.Mode = Earthworks.Move.Fill;
			Earthworks.Size = 1;
			WorldTile c = OpenSoil(3, 3f);
			if (Check("open soil to fill", c != null))
			{
				c = World.world.GetTile(c.x + 1, c.y + 1);
				yield return Stand(c, 1.6f);
				Supplies.Give(_me, "tw_item_dirt", 30);
				yield return Aim(Mid(c));
				Check("the prompt says fill", Mod.Prompt != null && Mod.Prompt.Contains("LMB:Fill") && Mod.Prompt.Contains("R:Dig"), Mod.Prompt);
				float h0 = Earthworks.WorkHeight(c);
				int dirt0 = Civil.BagCount(_me, "tw_item_dirt");
				Earthworks.ForceDig = true;
				yield return CivilTest.Wait(0.18f);
				Mod.SaveFrame("shovel_stroke_in.png");
				yield return CivilTest.Wait(0.14f);
				Mod.SaveFrame("shovel_stroke_lever.png");
				yield return CivilTest.Wait(0.13f);
				Mod.SaveFrame("shovel_stroke_toss.png");
				yield return CivilTest.Wait(1.6f);
				Earthworks.ForceDig = false;
				Check("LMB fills when R has fill", Earthworks.WorkHeight(c) > h0 + 0.05f, $"{h0:F2} -> {Earthworks.WorkHeight(c):F2}");
				Check("...spending dirt", Civil.BagCount(_me, "tw_item_dirt") < dirt0, string.Format("{0} -> {1}", dirt0, Civil.BagCount(_me, "tw_item_dirt")));
			}
		}

		private IEnumerator WaterFill()
		{
			TileType tileType = AssetManager.tiles.get("deep_ocean");
			TileType tileType2 = AssetManager.tiles.get("close_ocean");
			TileType tileType3 = AssetManager.tiles.get("shallow_waters");
			Check("deep sea fills to close", Earthworks.UpOf(tileType, sand: false) == tileType2, Earthworks.UpOf(tileType, sand: false)?.id);
			Check("close sea fills to shallows", Earthworks.UpOf(tileType2, sand: false) == tileType3, Earthworks.UpOf(tileType2, sand: false)?.id);
			Check("shallows fill to soil with dirt, sand with sand", Earthworks.UpOf(tileType3, sand: false)?.id == "soil_low" && Earthworks.UpOf(tileType3, sand: true)?.id == "sand", Earthworks.UpOf(tileType3, sand: false)?.id + " / " + Earthworks.UpOf(tileType3, sand: true)?.id);
			WorldTile worldTile = Sea(tileType, 0f);
			if (worldTile != null)
			{
				List<string> list = new List<string> { worldTile.main_type.id };
				for (int i = 0; i < 80; i++)
				{
					if (!worldTile.main_type.liquid && !worldTile.main_type.ocean)
					{
						break;
					}
					Earthworks.Apply(worldTile, fill: true, 1f / 6f, capped: false, float.NaN, sand: true);
					if (list[list.Count - 1] != worldTile.main_type.id)
					{
						list.Add(worldTile.main_type.id);
					}
				}
				Check("deep sea filled with sand: no dry bed on the way", string.Join(",", list) == "deep_ocean,close_ocean,shallow_waters,sand", string.Join(",", list));
			}
			else
			{
				Note("no deep sea on this map: its steps checked by the chain only");
			}
			Earthworks.Mode = Earthworks.Move.Fill;
			Earthworks.Size = 1;
			WorldTile w = Sea(null, 3f);
			if (!Check("water at a shore", w != null))
			{
				yield break;
			}
			WorldTile worldTile2 = null;
			WorldTile[] neighboursAll = w.neighboursAll;
			foreach (WorldTile worldTile3 in neighboursAll)
			{
				if (worldTile3?.main_type != null && !worldTile3.main_type.liquid && !worldTile3.main_type.block && !worldTile3.main_type.ocean && worldTile3.building == null)
				{
					worldTile2 = worldTile3;
					break;
				}
			}
			_me.current_position = new Vector2((float)worldTile2.x + 0.5f, (float)worldTile2.y + 0.5f);
			try
			{
				_me.dirty_current_tile = true;
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.5f);
			Supplies.Give(_me, "tw_item_dirt", 60);
			yield return Aim(Mid(w));
			Check("the prompt offers to fill the water", Mod.Prompt != null && Mod.Prompt.Contains("LMB:Fill"), Mod.Prompt);
			Earthworks.Mode = Earthworks.Move.Dig;
			yield return CivilTest.Wait(0.2f);
			Check("...and digging it says why not", Mod.Prompt != null && Mod.Prompt.Contains("Under water") && Mod.Prompt.Contains("R:Fill"), Mod.Prompt);
			Earthworks.Mode = Earthworks.Move.Fill;
			string was = w.main_type.id;
			int dirt0 = Civil.BagCount(_me, "tw_item_dirt");
			Earthworks.ForceDig = true;
			float t0 = Time.unscaledTime;
			while (Time.unscaledTime - t0 < 12f && (w.main_type.liquid || w.main_type.ocean))
			{
				yield return null;
			}
			Earthworks.ForceDig = false;
			yield return CivilTest.Wait(0.3f);
			Check("water filled by hand turns to soil", w.main_type.id == "soil_low", string.Format("{0} -> {1}, {2} -> {3} dirt", was, w.main_type.id, dirt0, Civil.BagCount(_me, "tw_item_dirt")));
			Check("...spending dirt", Civil.BagCount(_me, "tw_item_dirt") < dirt0, string.Format("{0} -> {1}", dirt0, Civil.BagCount(_me, "tw_item_dirt")));
			Mod.SaveFrame("shovel_water.png");
		}

		private WorldTile Sea(TileType type, float from)
		{
			Vector2 current_position = _me.current_position;
			for (int i = 0; i < 20000; i++)
			{
				float f = (float)i * 0.71f;
				float num = from + (float)i * 0.02f;
				WorldTile tile = World.world.GetTile((int)(current_position.x + Mathf.Cos(f) * num), (int)(current_position.y + Mathf.Sin(f) * num));
				if (tile?.main_type == null || !tile.main_type.ocean || !tile.main_type.liquid)
				{
					continue;
				}
				if (type != null)
				{
					if (tile.main_type == type)
					{
						return tile;
					}
				}
				else
				{
					if (tile.neighboursAll == null || Earthworks.LandOf(tile) != tile.main_type)
					{
						continue;
					}
					WorldTile[] neighboursAll = tile.neighboursAll;
					foreach (WorldTile worldTile in neighboursAll)
					{
						if (worldTile?.main_type != null && !worldTile.main_type.liquid && !worldTile.main_type.block && !worldTile.main_type.ocean && worldTile.building == null)
						{
							return tile;
						}
					}
				}
			}
			return null;
		}

		private IEnumerator HarbourFill()
		{
			Earthworks.Mode = Earthworks.Move.Fill;
			Earthworks.Size = 1;
			WorldTile gap = null;
			WorldTile worldTile = null;
			Building dock = null;
			bool ruinBefore = false;
			Vector2 c = _me.current_position;
			List<WorldTile> tried = new List<WorldTile>();
			float t;
			for (int attempt = 0; attempt < 6; attempt++)
			{
				if (gap != null)
				{
					break;
				}
				WorldTile spot = null;
				for (int i = 3; i < 140; i++)
				{
					if (spot != null)
					{
						break;
					}
					for (int j = 0; j < 72; j++)
					{
						if (spot != null)
						{
							break;
						}
						float f = (float)j / 72f * MathF.PI * 2f;
						int num = (int)(c.x + Mathf.Cos(f) * (float)i);
						int num2 = (int)(c.y + Mathf.Sin(f) * (float)i);
						if (num < 16 || num2 < 16 || num >= MapBox.width - 16 || num2 >= MapBox.height - 16)
						{
							continue;
						}
						bool flag = true;
						for (int k = 0; k <= 4 && flag; k++)
						{
							for (int l = -2; l <= 2 && flag; l++)
							{
								WorldTile tile = World.world.GetTile(num + l, num2 + k);
								if (tile?.main_type == null || !tile.main_type.liquid || !tile.main_type.ocean || tile.building != null || Earthworks.LandOf(tile) != tile.main_type)
								{
									flag = false;
								}
							}
						}
						bool flag2 = false;
						foreach (WorldTile item2 in tried)
						{
							if (Mathf.Abs(item2.x - num) < 10 && Mathf.Abs(item2.y - num2) < 10)
							{
								flag2 = true;
							}
						}
						if (flag && !flag2 && Shore(num, num2).water != null)
						{
							spot = World.world.GetTile(num, num2);
						}
					}
				}
				if (!Check("a coast for a harbour", spot != null))
				{
					yield break;
				}
				if (_me.city != null)
				{
					for (int m = -3; m <= 7; m++)
					{
						for (int n = -5; n <= 5; n++)
						{
							WorldTile tile2 = World.world.GetTile(spot.x + n, spot.y + m);
							if (tile2?.zone != null && tile2.zone.city == null)
							{
								_me.city.addZone(tile2.zone);
							}
						}
					}
				}
				dock = World.world.buildings.addBuilding("docks_human", spot, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				if (!Check("a harbour built", dock != null))
				{
					yield break;
				}
				dock.completeConstruction();
				try
				{
					if (_me.kingdom != null)
					{
						dock.setKingdom(_me.kingdom);
					}
				}
				catch (Exception ex)
				{
					Note("the harbour not the kingdom's: " + ex.Message);
				}
				yield return CivilTest.Wait(0.5f);
				ruinBefore = dock.isRuin();
				WorldTile item = Shore(spot.x, spot.y).land;
				_me.current_position = new Vector2((float)item.x + 0.5f, (float)item.y + 0.5f);
				try
				{
					_me.dirty_current_tile = true;
				}
				catch
				{
				}
				for (t = 0f; t < 4f; t += 0.25f)
				{
					if (DockDecks.Count != 0)
					{
						break;
					}
					yield return CivilTest.Wait(0.25f);
				}
				(gap, worldTile) = Shore(spot.x, spot.y, dock);
				if (gap == null)
				{
					Note($"the harbour at {spot.x},{spot.y} has no open water beside the land (its pier reaches the shore): another");
					tried.Add(spot);
					try
					{
						dock.startDestroyBuilding();
					}
					catch
					{
					}
				}
			}
			if (gap == null)
			{
				Note("no harbour with water between it and the land in 6 coasts: not checked");
				yield break;
			}
			int berths = (dock.component_docks?.tiles_ocean?.Count).GetValueOrDefault();
			_me.current_position = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f);
			try
			{
				_me.dirty_current_tile = true;
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.6f);
			Supplies.Give(_me, "tw_item_dirt", 40);
			yield return Aim(Mid(gap));
			Check("the prompt offers to fill by the harbour", Mod.Prompt != null && Mod.Prompt.Contains("LMB:Fill"), $"{Mod.Prompt} at {gap.x},{gap.y}, {gap.main_type.id}");
			string was = gap.main_type.id;
			Earthworks.ForceDig = true;
			string ruinedAt = null;
			t = Time.unscaledTime;
			while (Time.unscaledTime - t < 10f && (gap.main_type.liquid || gap.main_type.ocean))
			{
				yield return null;
				if (ruinedAt == null && dock.isRuin())
				{
					ruinedAt = $"ruined with the gap {gap.main_type.id}, swings {Earthworks.Swings}";
				}
			}
			Earthworks.ForceDig = false;
			for (int attempt = 0; attempt < 30; attempt++)
			{
				if (ruinedAt != null)
				{
					break;
				}
				yield return null;
				if (dock.isRuin())
				{
					ruinedAt = $"ruined {attempt} frames after";
				}
			}
			if (ruinedAt != null)
			{
				Note("harbour " + ruinedAt);
			}
			yield return CivilTest.Wait(2f);
			Check("the water between the harbour and the land filled", !gap.main_type.liquid && gap.building == null, was + " -> " + gap.main_type.id + ", " + gap.building?.asset?.id);
			bool flag3 = true;
			try
			{
				foreach (ref WorldTile item3 in dock.component_docks.tiles_ocean)
				{
					if (!item3.Type.ocean)
					{
						flag3 = false;
					}
				}
			}
			catch
			{
				flag3 = false;
			}
			Check("...the harbour still stands, its berths open", dock.isAlive() && !ruinBefore && !dock.isRuin() && flag3 && (dock.component_docks?.tiles_ocean?.Count).GetValueOrDefault() == berths, $"alive {dock.isAlive()}, ruin {ruinBefore} -> {dock.isRuin()}, berths {berths} -> {dock.component_docks?.tiles_ocean?.Count}");
			Mod.SaveFrame("shovel_harbour.png");
			if (gap.main_type.liquid || gap.main_type.ocean)
			{
				for (int num3 = 0; num3 < 60; num3++)
				{
					if (!gap.main_type.liquid && !gap.main_type.ocean)
					{
						break;
					}
					Earthworks.Apply(gap, fill: true, 1f, capped: false);
				}
				yield return CivilTest.Wait(2f);
				Note($"the gap filled by the test itself: {gap.main_type.id}, harbour alive {dock.isAlive()}, in its tiles {dock.tiles.Contains(gap)}");
			}
			try
			{
				typeof(Building).GetMethod("fillTiles", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(dock, null);
			}
			catch (Exception ex2)
			{
				Note("fillTiles: " + ex2.Message);
			}
			bool back = dock.tiles.Contains(gap);
			yield return CivilTest.Wait(2f);
			Check("...and after its tiles are laid out again (a load, an upgrade), still standing", gap.main_type.ground && dock.isAlive() && !dock.isRuin() && !dock.tiles.Contains(gap), string.Format("gap back in its tiles {0}, alive {1}, ruin {2}, the gap's now {3} in them; detour {4}", back, dock.isAlive(), dock.isRuin(), dock.tiles.Contains(gap) ? "still" : "not", DockGround.Installed));
		}

		private static (WorldTile water, WorldTile land) Shore(int x, int y, Building dock = null)
		{
			for (int i = 0; i <= 4; i++)
			{
				for (int j = -2; j <= 2; j++)
				{
					WorldTile tile = World.world.GetTile(x + j, y + i);
					if (tile == null || (dock != null && (tile.building != dock || DockDecks.On(new Vector2((float)tile.x + 0.5f, (float)tile.y + 0.5f)))))
					{
						continue;
					}
					try
					{
						if (dock != null && (tile == dock.current_tile || (dock.component_docks?.tiles_ocean != null && dock.component_docks.tiles_ocean.Contains(tile))))
						{
							continue;
						}
					}
					catch
					{
					}
					for (int k = 0; k < 4; k++)
					{
						MapBox world = World.world;
						int x2 = tile.x;
						int num;
						switch (k)
						{
						default:
							num = 0;
							break;
						case 1:
							num = -1;
							break;
						case 0:
							num = 1;
							break;
						}
						int pX = x2 + num;
						int y2 = tile.y;
						int num2;
						switch (k)
						{
						default:
							num2 = 0;
							break;
						case 3:
							num2 = -1;
							break;
						case 2:
							num2 = 1;
							break;
						}
						WorldTile tile2 = world.GetTile(pX, y2 + num2);
						if (tile2?.main_type != null && !tile2.main_type.liquid && !tile2.main_type.ocean && !tile2.main_type.block && tile2.building == null)
						{
							return (water: tile, land: tile2);
						}
					}
				}
			}
			return (water: null, land: null);
		}

		private void SetXp(int xp)
		{
			string pResult = null;
			try
			{
				_me.data.get("fp_skills", out pResult, null);
			}
			catch
			{
			}
			List<string> list = new List<string>();
			if (!string.IsNullOrEmpty(pResult))
			{
				string[] array = pResult.Split('|');
				foreach (string text in array)
				{
					if (text.Length > 0 && !text.StartsWith("earth:", StringComparison.Ordinal))
					{
						list.Add(text);
					}
				}
			}
			list.Add("earth:" + xp);
			_me.data.set("fp_skills", string.Join("|", list.ToArray()));
		}

		private static Vector2 Mid(WorldTile t)
		{
			return new Vector2((float)t.x + 0.5f, (float)t.y + 0.5f);
		}

		private WorldTile OpenSoil(int w, float from)
		{
			Vector2 current_position = _me.current_position;
			for (int i = 0; i < 2400; i++)
			{
				float f = (float)i * 0.71f;
				float num = from + (float)i * 0.04f;
				WorldTile tile = World.world.GetTile((int)(current_position.x + Mathf.Cos(f) * num), (int)(current_position.y + Mathf.Sin(f) * num));
				if (tile == null)
				{
					continue;
				}
				bool flag = true;
				for (int j = -1; j <= w && flag; j++)
				{
					for (int k = -1; k <= w && flag; k++)
					{
						WorldTile tile2 = World.world.GetTile(tile.x + k, tile.y + j);
						string why;
						if (tile2?.main_type == null || tile2.main_type.liquid || tile2.main_type.block || !tile2.main_type.can_be_biome || tile2.main_type.height_min >= 199)
						{
							flag = false;
						}
						else if (tile2.Type != null && tile2.Type.road)
						{
							flag = false;
						}
						else if (tile2.building != null && AssetStyle.IsSolid(tile2.building) && tile2.building.asset.building_type != BuildingType.Building_Tree && tile2.building.asset.building_type != BuildingType.Building_Plant && tile2.building.asset.building_type != BuildingType.Building_Mineral)
						{
							flag = false;
						}
						else if (float.IsNaN(Earthworks.WorkHeight(tile2)) || !Earthworks.MayDig(_me, tile2, out why))
						{
							flag = false;
						}
					}
				}
				if (flag)
				{
					CivilTest.ClearBox(tile.x - 1, tile.y - 1, tile.x + w, tile.y + w);
					return tile;
				}
			}
			return null;
		}

		private IEnumerator Stand(WorldTile t, float away)
		{
			for (int i = 0; i < 8; i++)
			{
				float f = (float)i * MathF.PI / 4f + MathF.PI;
				Vector2 current_position = new Vector2((float)t.x + 0.5f + Mathf.Cos(f) * away, (float)t.y + 0.5f + Mathf.Sin(f) * away);
				WorldTile tile = World.world.GetTile((int)current_position.x, (int)current_position.y);
				if (tile?.main_type != null && !tile.main_type.liquid && !tile.main_type.block)
				{
					_me.current_position = current_position;
					try
					{
						_me.dirty_current_tile = true;
					}
					catch
					{
					}
					break;
				}
			}
			yield return CivilTest.Wait(0.5f);
		}

		private IEnumerator Aim(Vector2 at)
		{
			Vector2 current_position = _me.current_position;
			float dist = Mathf.Max(0.2f, (at - current_position).magnitude);
			Mod.DebugEyeSet = true;
			Mod.DebugEye = current_position;
			Mod.DebugEyeHeight = 0f;
			Mod.SetYaw(Mathf.Atan2(at.y - current_position.y, at.x - current_position.x));
			for (int k = 0; k < 2; k++)
			{
				float b = Mod.EyeForTest.z - _s.Cache.SampleHeight(at.x, at.y);
				Mod.SetPitchDegrees((0f - Mathf.Atan2(Mathf.Max(0.3f, b), dist)) * 57.29578f);
				yield return CivilTest.Wait(0.4f);
			}
		}
	}
}
