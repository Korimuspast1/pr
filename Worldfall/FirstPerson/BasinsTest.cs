using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BasinsTest : CivilTest
	{
		private Actor _king;

		private WorldSampler _s;

		protected override string Prefix => "BASINS";

		protected override IEnumerator Body()
		{
			Mod.Settings.TimeMode = 1;
			Mod.ForceRelight();
			int num = SaveSlot(Options);
			if (num > 0)
			{
				yield return Part("tarns", Tarns(num));
				yield break;
			}
			_king = Village("human", 5, 0);
			if (!Check("a village", _king != null))
			{
				yield break;
			}
			try
			{
				WorldTile tile = _king.city.getTile();
				for (int i = -3; i <= 3; i++)
				{
					for (int j = -3; j <= 3; j++)
					{
						WorldTile tile2 = World.world.GetTile(tile.x + j * 8, tile.y + i * 8);
						if (tile2?.zone != null && tile2.zone.city == null)
						{
							_king.city.addZone(tile2.zone);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Note("land: " + ex.Message);
			}
			yield return CivilTest.Wait(2f);
			ControllableUnit.setControllableCreature(_king);
			yield return CivilTest.Wait(3f);
			if (Check("first person", Mod.IsFirstPerson))
			{
				_s = Mod.SamplerForTest;
				yield return Part("pond", Pond());
			}
		}

		private static int SaveSlot(string options)
		{
			int num = options.IndexOf("map", StringComparison.Ordinal);
			if (num < 0)
			{
				return 0;
			}
			int num2 = 0;
			int num3 = num + 3;
			while (num3 < options.Length && char.IsDigit(options[num3]))
			{
				num2 = num2 * 10 + (options[num3++] - 48);
			}
			return num2;
		}

		private WorldTile Patch(Vector2 near)
		{
			City city = _king.city;
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			float num6 = float.MaxValue;
			for (float num7 = 5f; num7 < 30f; num7 += 1f)
			{
				for (int i = 0; i < 24; i++)
				{
					float f = (float)i * MathF.PI / 12f;
					int num8 = (int)(near.x + Mathf.Cos(f) * num7);
					int num9 = (int)(near.y + Mathf.Sin(f) * num7);
					bool flag = true;
					float num10 = float.MaxValue;
					float num11 = float.MinValue;
					for (int j = -2; j <= 2 && flag; j++)
					{
						for (int k = -2; k <= 2 && flag; k++)
						{
							WorldTile tile = World.world.GetTile(num8 + k, num9 + j);
							if (tile?.main_type == null || tile.main_type.liquid || tile.main_type.height_min < 108 || tile.main_type.height_min >= 199)
							{
								flag = false;
								num++;
								break;
							}
							float b = _s.Cache.Floor[tile.y * _s.Cache.Width + tile.x];
							num10 = Mathf.Min(num10, b);
							num11 = Mathf.Max(num11, b);
							if (Mathf.Abs(k) <= 1 && Mathf.Abs(j) <= 1)
							{
								if (city != null && tile.zone?.city != city)
								{
									flag = false;
									num2++;
								}
								else if (tile.building != null && AssetStyle.IsSolid(tile.building))
								{
									flag = false;
									num3++;
								}
								else if (tile.top_type != null && !tile.top_type.is_biome)
								{
									flag = false;
									num4++;
								}
							}
						}
					}
					if (flag)
					{
						num6 = Mathf.Min(num6, num11 - num10);
					}
					if (flag && num11 - num10 < 0.4f)
					{
						return World.world.GetTile(num8, num9);
					}
					if (flag)
					{
						num5++;
					}
				}
			}
			Note($"no patch: {num} not soil, {num2} not the village's, {num3} built on, {num4} no biome, {num5} too steep (flattest {num6:0.00})");
			return null;
		}

		private IEnumerator Pond()
		{
			WorldTile mid = Patch(_king.current_position);
			if (!Check("a patch of soil to dig a pond in", mid != null))
			{
				yield break;
			}
			int w = _s.Cache.Width;
			Relief r = _s.Relief;
			List<WorldTile> tiles = new List<WorldTile>();
			for (int i = -1; i <= 1; i++)
			{
				for (int j = -1; j <= 1; j++)
				{
					tiles.Add(World.world.GetTile(mid.x + j, mid.y + i));
				}
			}
			tiles.ConvertAll((WorldTile t) => t.main_type).ToArray();
			tiles.ConvertAll((WorldTile t) => t.top_type).ToArray();
			Note($"pond at {mid.x},{mid.y}: {mid.main_type.id}/{mid.top_type?.id}");
			for (int num = 0; num < 9; num++)
			{
				int num2 = ((num == 4) ? 14 : 8);
				for (int num3 = 0; num3 < num2; num3++)
				{
					Earthworks.Apply(tiles[num], fill: false, 1f / 6f, capped: false);
				}
			}
			for (int f = 0; f < 4; f++)
			{
				yield return null;
			}
			TileType[] dugMains = tiles.ConvertAll((WorldTile t) => t.main_type).ToArray();
			TopTileType[] dugTops = tiles.ConvertAll((WorldTile t) => t.top_type).ToArray();
			Lakes.FindNowForTest();
			int i2 = mid.y * w + mid.x;
			Lakes.Lake lake = null;
			foreach (Lakes.Lake item in Lakes.Now)
			{
				if (item.Basin.Tiles.Contains(i2))
				{
					lake = item;
				}
			}
			if (lake == null)
			{
				StringBuilder stringBuilder = new StringBuilder();
				for (int num4 = 3; num4 >= -3; num4--)
				{
					for (int num5 = -3; num5 <= 3; num5++)
					{
						stringBuilder.Append($"{_s.Cache.Floor[(mid.y + num4) * w + mid.x + num5],6:F2}");
					}
					stringBuilder.Append(" |");
				}
				Basin basin = Basins.Pour(_s.Cache.Floor, (int num10) => false, w, _s.Cache.Height, i2, 900);
				Note(string.Format("beds round the pond: {0}; pour from its middle: {1}", stringBuilder, (basin != null) ? (basin.Count + " tiles, rim " + basin.Rim.ToString("F2")) : Basins.Why));
			}
			if (!Check("the dug hollow is a basin", lake != null, $"{Lakes.Found} basins found"))
			{
				yield break;
			}
			Note($"basin: {lake.Basin.Count} tiles, floor {lake.Basin.Floor:F2}, rim {lake.Basin.Rim:F2}, holds {lake.Capacity:F2}");
			Check("...and starts dry", lake.Volume <= 0f && !r.IsLake(i2));
			Mod.SaveFrame("basins_dug.png");
			int flooded0 = Lakes.Flooded;
			for (int f = 0; f < 15; f++)
			{
				Lakes.FlowForTest(0.1f, 1f);
				yield return null;
			}
			for (int f = 0; f < 4; f++)
			{
				yield return null;
			}
			lake = null;
			foreach (Lakes.Lake item2 in Lakes.Now)
			{
				if (item2.Basin.Tiles.Contains(i2))
				{
					lake = item2;
				}
			}
			if (!Check("the rain filled it", lake != null && lake.Volume > 0f && r.IsLake(i2), (lake != null) ? $"volume {lake.Volume:F2} of {lake.Capacity:F2}, level {lake.Level:F2}" : "gone"))
			{
				yield break;
			}
			Check("...its tiles are the game's water", mid.main_type.liquid, mid.main_type.id);
			TerrainCache cache = _s.Cache;
			float level = lake.Level;
			Check("...drawn at the pond's own level", Mathf.Abs(cache.Ground[i2] - level) < 0.03f && cache.Ground[i2] > 0.05f, $"ground {cache.Ground[i2]:F2} level {level:F2}");
			Check("...over its bed", cache.Floor[i2] < cache.Ground[i2] - 0.02f, $"bed {cache.Floor[i2]:F2}");
			Check("...marked a lake", (cache.Marks[i2] & 4) != 0 && (cache.Flags[i2] & 1) != 0);
			Check("...swum in at its level", Mathf.Abs(cache.LiquidSurface((float)mid.x + 0.5f, (float)mid.y + 0.5f) - level) < 0.03f);
			Check("full in a day and a half or less", lake.Volume >= lake.Capacity * 0.99f, $"{lake.Volume / Mathf.Max(0.0001f, lake.Capacity):P0} full");
			Note($"tiles flooded: {Lakes.Flooded - flooded0}");
			Mod.DebugEyeSet = true;
			Mod.DebugEye = new Vector2((float)mid.x + 0.5f - 4f, (float)mid.y + 0.5f - 3f);
			Mod.DebugEyeHeight = 1.6f;
			Mod.SetYaw(Mathf.Atan2(3f, 4f));
			Mod.SetPitchDegrees(-14f);
			yield return CivilTest.Wait(1f);
			Mod.SaveFrame("basins_pond.png");
			BasinsTest basinsTest = this;
			bool ok = lake.Stream != null && lake.Stream.Count >= 2;
			object detail;
			int top;
			if (lake.Stream == null)
			{
				detail = "none";
			}
			else
			{
				top = lake.Stream.Count;
				detail = top + " tiles of stream";
			}
			basinsTest.Check("its overflow runs on", ok, (string)detail);
			if (lake.Stream != null && lake.Stream.Count >= 2)
			{
				Lakes.FlowForTest(0.05f, 1f);
				int num6 = lake.Stream[lake.Stream.Count - 1];
				int num7 = lake.Stream[0];
				Vector2 vector = new Vector2((float)(num7 % w) + 0.5f, (float)(num7 / w) + 0.5f);
				Vector2 vector2 = new Vector2((float)(num6 % w) + 0.5f, (float)(num6 / w) + 0.5f);
				Vector2 vector3 = (((vector2 - vector).sqrMagnitude > 0.01f) ? (vector2 - vector).normalized : Vector2.up);
				Mod.DebugEyeSet = true;
				Mod.DebugEye = vector - vector3 * 3f + new Vector2(0f - vector3.y, vector3.x) * 1.5f;
				Mod.DebugEyeHeight = 2.2f;
				Mod.SetYaw(Mathf.Atan2(vector3.y, vector3.x));
				Mod.SetPitchDegrees(-22f);
				int f = 0;
				while (f < 6)
				{
					Lakes.FlowForTest(0.02f, 1f);
					yield return CivilTest.Wait(0.15f);
					top = f++;
				}
				Check("...drawn as a stream", Streams.DrawnForTest > 0, Streams.FallsForTest + " falls on it");
				Mod.SaveFrame("basins_stream.png");
			}
			Mod.DebugEyeSet = false;
			ReliefWorld.SaveNow();
			string pResult = null;
			World.world.map_stats.custom_data?.get("fp_relief", out pResult, null);
			Relief relief = new Relief();
			relief.Allocate(r.Width, r.Height);
			bool flag = relief.Load(pResult, ReliefWorld.TypeId);
			Check("the save holds the lake", flag && relief.IsLake(i2) && relief.LakeTileCount() == r.LakeTileCount(), $"{relief.LakeTileCount()} of {r.LakeTileCount()} lake tiles");
			relief.Lake(i2, out var level2, out var main, out top);
			r.Lake(i2, out var level3, out var main2, out top);
			Check("...with the land under it", main == main2 && main != 0 && Mathf.Abs(level2 - level3) < 0.02f, $"{ReliefWorld.TypeName(main)} at {level2:F2}");
			Check("the pond stays in what was dug", r.LakeTileCount() <= 25, r.LakeTileCount() + " tiles under water");
			int drained0 = Lakes.Drained;
			float dry = Lakes.DryPerDay;
			Lakes.DryPerDay = 2f;
			for (int f = 0; f < 40; f++)
			{
				if (!r.IsLake(i2))
				{
					break;
				}
				Lakes.FlowForTest(0.1f, 0f, 1f);
				yield return null;
			}
			Lakes.DryPerDay = dry;
			for (int f = 0; f < 4; f++)
			{
				yield return null;
			}
			Check("drought drains it", !r.IsLake(i2) && !mid.main_type.liquid, mid.main_type.id);
			int num8 = 0;
			for (int num9 = 0; num9 < 9; num9++)
			{
				if (tiles[num9].main_type == dugMains[num9] && (dugTops[num9] == null || tiles[num9].top_type?.biome_asset == dugTops[num9].biome_asset))
				{
					num8++;
				}
			}
			Check("...the land and its grass come back as they were dug", num8 == 9, num8 + " of 9");
			Note($"tiles drained: {Lakes.Drained - drained0}");
		}

		private IEnumerator Tarns(int slot)
		{
			string text = Path.Combine(Path.Combine(Path.Combine(Application.persistentDataPath, "saves"), "save" + slot), "map.wbox");
			bool ok = false;
			try
			{
				SaveManager.loadMapFromBytes(File.ReadAllBytes(text));
				ok = true;
			}
			catch (Exception ex)
			{
				Note("could not load " + text + ": " + ex.Message);
			}
			if (!Check("the save loaded", ok))
			{
				yield break;
			}
			for (float t = 0f; t < 90f; t += 0.5f)
			{
				yield return CivilTest.Wait(0.5f);
				bool flag = true;
				try
				{
					flag = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag && t > 3f)
				{
					break;
				}
			}
			Config.autosaves = false;
			yield return CivilTest.Wait(2f);
			_s = Mod.SamplerForTest;
			_s.EnsureWorld();
			ReliefWorld.Choose(2);
			_s.EnsureWorld();
			for (int f = 0; f < 10; f++)
			{
				yield return null;
			}
			Lakes.Tick(Mod.Day, null);
			yield return CivilTest.Wait(2f);
			Lakes.FindNowForTest();
			int tarns = 0;
			int full = 0;
			int spilling = 0;
			Lakes.Lake show = null;
			foreach (Lakes.Lake item in Lakes.Now)
			{
				if (item.Tarn)
				{
					tarns++;
					if (item.Volume >= item.Capacity * 0.99f)
					{
						full++;
					}
					if (show == null || item.Basin.Count > show.Basin.Count)
					{
						show = item;
					}
				}
			}
			for (int f = 0; f < 3; f++)
			{
				Lakes.FlowForTest(0.05f, 0f);
				yield return null;
			}
			foreach (Lakes.Lake item2 in Lakes.Now)
			{
				if (item2.Tarn && item2.Stream != null)
				{
					spilling++;
				}
			}
			Note($"tarns: {ReliefGen.LastTarns.Count} made, {tarns} holding water, {full} full, {spilling} spilling");
			Check("Terrainy ranges hold tarns", tarns > 0);
			Check("...full from their springs", tarns > 0 && full == tarns);
			Check("...and spilling down the range", spilling > 0);
			if (show != null)
			{
				int f = show.Basin.Tiles[0];
				int w = _s.Cache.Width;
				Mod.DebugEyeSet = true;
				Mod.DebugEye = new Vector2((float)(f % w) + 0.5f - 9f, (float)(f / w) + 0.5f - 7f);
				Mod.DebugEyeHeight = 6f;
				Mod.SetYaw(Mathf.Atan2(7f, 9f));
				Mod.SetPitchDegrees(-20f);
				foreach (Actor unit in World.world.units)
				{
					if (unit != null && unit.isAlive() && unit.isAdult() && !unit.isInsideSomething() && unit.asset != null && unit.asset.id == "human")
					{
						ControllableUnit.setControllableCreature(unit);
						break;
					}
				}
				yield return CivilTest.Wait(3f);
				Mod.DebugEyeSet = true;
				Mod.DebugEye = new Vector2((float)(f % w) + 0.5f - 6f, (float)(f / w) + 0.5f - 6f);
				Mod.DebugEyeHeight = 7f;
				Mod.SetYaw(MathF.PI / 4f);
				Mod.SetPitchDegrees(-32f);
				yield return CivilTest.Wait(1f);
				Mod.SaveFrame("basins_tarn.png");
				Mod.DebugEyeSet = false;
			}
			ReliefWorld.Choose(1);
		}
	}
}
