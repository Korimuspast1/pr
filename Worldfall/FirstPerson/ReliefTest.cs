using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class ReliefTest : CivilTest
	{
		private Actor _king;

		private WorldSampler _s;

		private readonly List<(string name, Vector2 eye, float yaw, float pitch, float height)> _views = new List<(string, Vector2, float, float, float)>();

		protected override string Prefix => "RELIEF";

		protected override IEnumerator Body()
		{
			Mod.Settings.TimeMode = 1;
			Mod.ForceRelight();
			SiteLevel.HoldForTest = true;
			int slot = SaveSlot(Options);
			if (slot > 0)
			{
				string text = Path.Combine(Path.Combine(Path.Combine(Application.persistentDataPath, "saves"), "save" + slot), "map.wbox");
				bool flag = false;
				try
				{
					SaveManager.loadMapFromBytes(File.ReadAllBytes(text));
					flag = true;
				}
				catch (Exception ex)
				{
					Note("could not load " + text + ": " + ex.Message);
				}
				if (flag)
				{
					for (float t = 0f; t < 90f; t += 0.5f)
					{
						yield return CivilTest.Wait(0.5f);
						bool flag2 = true;
						try
						{
							flag2 = SmoothLoader.isLoading();
						}
						catch
						{
						}
						if (!flag2 && t > 3f)
						{
							break;
						}
					}
					Config.autosaves = false;
					yield return CivilTest.Wait(2f);
					Note($"loaded save {slot}: {MapBox.width}x{MapBox.height}");
					_king = Townsman();
				}
			}
			if (_king == null)
			{
				_king = Village("human", 6, 0);
			}
			if (!Check("a village to dig in", _king != null))
			{
				yield break;
			}
			yield return CivilTest.Wait(2f);
			Possess(_king);
			yield return CivilTest.Wait(3f);
			if (Check("first person", Mod.IsFirstPerson))
			{
				_s = Mod.SamplerForTest;
				try
				{
					_s.DumpForLab(Path.Combine(Log.Folder, "relief_" + ((slot > 0) ? ("map" + slot) : "test") + ".land"));
				}
				catch (Exception ex2)
				{
					Note("land not written: " + ex2.Message);
				}
				yield return Part("flat", Flat());
				yield return Part("bands", Bands());
				yield return Part("hand", Hand());
				yield return Part("rules", Rules());
				yield return Part("earthshaping", Earthshaping());
				yield return Part("look", Look());
				yield return Part("slopes", Slopes());
				yield return Part("save", SaveAndLoad());
				if (!Options.Contains("noterrainy"))
				{
					yield return Part("terrainy", Terrainy());
				}
				SiteLevel.HoldForTest = false;
				if (Options.Contains("soak"))
				{
					yield return Part("soak", Soak());
				}
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

		private static Actor Townsman()
		{
			City city = null;
			int num = 0;
			foreach (City item in World.world.cities.list)
			{
				if (item != null && item.isAlive())
				{
					int num2 = 0;
					try
					{
						num2 = item.getPopulationPeople();
					}
					catch
					{
					}
					if (num2 > num)
					{
						num = num2;
						city = item;
					}
				}
			}
			if (city == null)
			{
				return null;
			}
			foreach (Actor unit in World.world.units)
			{
				if (unit != null && unit.isAlive() && unit.city == city && unit.isAdult() && !unit.isKing() && !unit.isCityLeader() && !unit.isInsideSomething())
				{
					return unit;
				}
			}
			return null;
		}

		private void Possess(Actor a)
		{
			try
			{
				ControllableUnit.setControllableCreature(a);
			}
			catch (Exception ex)
			{
				Note("possess: " + ex.Message);
			}
		}

		private WorldTile Patch(int w, float from)
		{
			Vector2 current_position = _king.current_position;
			for (int i = 0; i < 1600; i++)
			{
				float f = (float)i * 0.71f;
				float num = from + (float)i * 0.05f;
				WorldTile tile = World.world.GetTile((int)(current_position.x + Mathf.Cos(f) * num), (int)(current_position.y + Mathf.Sin(f) * num));
				if (tile == null)
				{
					continue;
				}
				bool flag = true;
				for (int j = 0; j < w && flag; j++)
				{
					for (int k = 0; k < w && flag; k++)
					{
						WorldTile tile2 = World.world.GetTile(tile.x + k, tile.y + j);
						if (tile2?.main_type == null || tile2.main_type.liquid || tile2.main_type.block || !tile2.main_type.can_be_biome || tile2.main_type.height_min >= 199)
						{
							flag = false;
						}
						else if (tile2.building != null && AssetStyle.IsSolid(tile2.building))
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

		private IEnumerator Look()
		{
			Settings s = Mod.Settings;
			WorldTile g = Patch(4, 6f);
			if (Check("a patch to make crystal", g != null))
			{
				TopTileType pTopType = AssetManager.top_tiles.get("crystal_low") ?? AssetManager.top_tiles.get("enchanted_low");
				for (int i = 0; i < 4; i++)
				{
					for (int j = 0; j < 4; j++)
					{
						WorldTile tile = World.world.GetTile(g.x + j, g.y + i);
						try
						{
							MapAction.terraformTop(tile, pTopType, TerraformLibrary.draw);
						}
						catch (Exception ex)
						{
							Note("crystal: " + ex.Message);
						}
					}
				}
				yield return CivilTest.Wait(1.5f);
				int num = (g.y + 1) * _s.Cache.Width + g.x + 1;
				Check("a crystal biome's land is marked to glow", (_s.Cache.Marks[num] & 8) != 0, World.world.GetTile(g.x + 1, g.y + 1)?.top_type?.id);
				int num2 = 0;
				WorldTile tile2 = World.world.GetTile(Mathf.FloorToInt(_king.current_position.x), Mathf.FloorToInt(_king.current_position.y));
				if (tile2 != null && (tile2.top_type == null || BiomeMap.FromTileId(tile2.top_type.id) == BiomeKind.Grass))
				{
					num2 = _s.Cache.Marks[tile2.y * _s.Cache.Width + tile2.x] & 8;
				}
				Check("...and plain grass isn't", num2 == 0);
				s.TimeMode = 3;
				Mod.ForceRelight();
				Mod.DebugEyeSet = true;
				Mod.DebugEye = new Vector2((float)g.x + 2f, (float)g.y - 3.5f);
				Mod.DebugEyeHeight = 1.7f;
				Mod.SetYaw(MathF.PI / 2f);
				Mod.SetPitchDegrees(-20f);
				yield return CivilTest.Wait(2f);
				Mod.SaveFrame("relief_glow.png");
				Mod.DebugEyeSet = false;
			}
			WorldTile worldTile = Patch(3, 12f);
			if (Check("a patch to dig a cut in", worldTile != null))
			{
				for (int k = 0; k < 3; k++)
				{
					for (int l = 0; l < 3; l++)
					{
						WorldTile tile3 = World.world.GetTile(worldTile.x + l, worldTile.y + k);
						for (int m = 0; m < 9; m++)
						{
							Earthworks.Apply(tile3, fill: false, 0.2f, capped: false);
						}
					}
				}
				s.TimeMode = 0;
				Mod.Day.TimeOfDay = 0.262f;
				Mod.ForceRelight();
				Mod.DebugEyeSet = true;
				Mod.DebugEye = new Vector2((float)worldTile.x + 1.5f, (float)worldTile.y - 3.5f);
				Mod.DebugEyeHeight = 1.5f;
				Mod.SetYaw(MathF.PI / 2f);
				Mod.SetPitchDegrees(-20f);
				yield return CivilTest.Wait(2f);
				float dawnMist = Mod.Day.Air.DawnMist;
				Check("the morning has its mist", dawnMist > 0.2f, $"{dawnMist:0.00} at {Mod.Day.TimeOfDay:0.000}");
				Mod.SaveFrame("relief_mist.png");
				Mod.Day.TimeOfDay = 0.75f;
				yield return CivilTest.Wait(0.5f);
				Check("...and the evening none", Mod.Day.Air.DawnMist < 0.01f, $"{Mod.Day.Air.DawnMist:0.00}");
				Mod.DebugEyeSet = false;
			}
			s.TimeMode = 1;
			Mod.ForceRelight();
			Actor actor = CivilTest.SpawnNear("human", _king.current_position, 3f);
			if (Check("someone to fall", actor != null))
			{
				float num3 = actor.getHealth();
				int falls = FallHurt.Falls;
				Vector2 current_position = actor.current_position;
				FallHurt.Reset();
				for (int n = 0; n < 30; n++)
				{
					FallHurt.Track(actor, current_position, 5f, 0.02f);
				}
				for (int num4 = 1; num4 <= 7; num4++)
				{
					FallHurt.Track(actor, current_position + new Vector2(0.05f * (float)num4, 0f), 5f - 0.4f * (float)num4, 0.02f);
				}
				for (int num5 = 1; num5 <= 5; num5++)
				{
					FallHurt.Track(actor, current_position + new Vector2(0.35f + 0.1f * (float)num5, 0f), 2.2f, 0.02f);
				}
				Check("a fall of nearly three tiles hurts", FallHurt.Falls == falls + 1 && (float)actor.getHealth() < num3, $"fell {FallHurt.LastDrop:0.0}, health {num3:0} -> {actor.getHealth():0}");
				float num6 = actor.getHealth();
				FallHurt.Reset();
				for (int num7 = 0; num7 < 30; num7++)
				{
					FallHurt.Track(actor, current_position, 5f, 0.02f);
				}
				for (int num8 = 1; num8 <= 20; num8++)
				{
					FallHurt.Track(actor, current_position + new Vector2(0.05f * (float)num8, 0f), 5f - 0.15f * (float)num8, 0.02f);
				}
				for (int num9 = 0; num9 < 30; num9++)
				{
					FallHurt.Track(actor, current_position, 5f, 0.02f);
				}
				for (int num10 = 1; num10 <= 8; num10++)
				{
					FallHurt.Track(actor, current_position + new Vector2(0.05f * (float)num10, 0f), 5f - 0.24f * (float)num10, 0.02f);
				}
				for (int num11 = 0; num11 < 5; num11++)
				{
					FallHurt.Track(actor, current_position + new Vector2(0.5f + 0.1f * (float)num11, 0f), 3.08f, 0.02f);
				}
				Check("...a slope or a short drop doesn't", FallHurt.Falls == falls + 1 && (float)actor.getHealth() >= num6, $"{FallHurt.Falls - falls} falls, health {num6:0} -> {actor.getHealth():0}");
				FallHurt.Reset();
				CivilTest.Kill(actor);
			}
			yield return null;
		}

		private IEnumerator Soak()
		{
			Mod.DebugEyeSet = false;
			int begun = SiteLevel.Begun;
			int moved = SiteLevel.TilesMoved;
			int asked = SiteLevel.Asked;
			float t0 = Time.unscaledTime;
			while (Time.unscaledTime - t0 < 185f)
			{
				yield return CivilTest.Wait(5f);
			}
			Note($"soak: {SiteLevel.Begun - begun} sites levelled, {SiteLevel.TilesMoved - moved} tile steps, {SiteLevel.Asked - asked} sites asked of by the towns ({SiteLevel.Refused} refused in all), {_s.Relief.EditedCount()} tiles edited, {Lakes.Now.Count} lakes");
		}

		private IEnumerator Slopes()
		{
			TerrainCache cache = _s.Cache;
			int width = cache.Width;
			int height = cache.Height;
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			for (int i = 0; i < height - 1; i++)
			{
				for (int j = 0; j < width - 1; j++)
				{
					int num4 = i * width + j;
					if ((cache.Flags[num4] & 3) != 0)
					{
						continue;
					}
					for (int k = 0; k < 2; k++)
					{
						int num5 = ((k == 0) ? (num4 + 1) : (num4 + width));
						if ((cache.Flags[num5] & 3) == 0)
						{
							float num6 = Mathf.Abs(cache.Ground[num5] - cache.Ground[num4]);
							num++;
							if (num6 > 1.4f)
							{
								num2++;
							}
							if (num6 > 2.5f)
							{
								num3++;
							}
						}
					}
				}
			}
			Note($"land steps between tile middles: {num}, {num2} over 1.4 ({100f * (float)num2 / (float)Mathf.Max(1, num):0.00}%), {num3} over 2.5");
			Vector2 current_position = _king.current_position;
			int num7 = -1;
			for (int l = Mathf.Max(1, (int)current_position.y - 40); l < Mathf.Min(height - 1, (int)current_position.y + 40); l++)
			{
				for (int m = Mathf.Max(1, (int)current_position.x - 40); m < Mathf.Min(width - 1, (int)current_position.x + 40); m++)
				{
					int num8 = l * width + m;
					if ((cache.Flags[num8] & 3) == 0 && (num7 < 0 || cache.Ground[num8] > cache.Ground[num7]))
					{
						num7 = num8;
					}
				}
			}
			Actor actor = CivilTest.SpawnNear("human", current_position, 3f);
			if (!Check("the highest ground near the village, and someone to walk down it", num7 >= 0 && actor != null))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)actor).addStatusEffect("invincible", 60f, pColorEffect: false);
			}
			catch
			{
			}
			Vector2 vector = new Vector2((float)(num7 % width) + 0.5f, (float)(num7 / width) + 0.5f);
			float num9 = cache.SampleHeight(vector.x, vector.y);
			float num10 = 0f;
			float num11 = 0f;
			int falls = FallHurt.Falls;
			FallHurt.Reset();
			for (int n = 0; n < 30; n++)
			{
				FallHurt.Track(actor, vector, num9, 1f / 60f);
			}
			for (int num12 = 0; num12 < 2400; num12++)
			{
				float num13 = cache.SampleHeight(vector.x + 0.25f, vector.y) - cache.SampleHeight(vector.x - 0.25f, vector.y);
				float num14 = cache.SampleHeight(vector.x, vector.y + 0.25f) - cache.SampleHeight(vector.x, vector.y - 0.25f);
				Vector2 vector2 = new Vector2(0f - num13, 0f - num14);
				if (vector2.magnitude < 0.001f)
				{
					break;
				}
				float num15 = cache.SampleHeight(vector.x, vector.y);
				Vector2 vector3 = vector + vector2.normalized * 0.05f;
				int num16 = (int)vector3.y * width + (int)vector3.x;
				if (num16 < 0 || num16 >= width * height || (cache.Flags[num16] & 3) != 0)
				{
					break;
				}
				float num17 = cache.SampleHeight(vector3.x, vector3.y);
				if (num17 >= num15)
				{
					break;
				}
				num10 = Mathf.Max(num10, (num15 - num17) / 0.05f);
				num11 += 0.05f;
				vector = vector3;
				FallHurt.Track(actor, vector, num17, 1f / 60f);
			}
			for (int num18 = 0; num18 < 10; num18++)
			{
				FallHurt.Track(actor, vector, cache.SampleHeight(vector.x, vector.y), 1f / 60f);
			}
			float num19 = num9 - cache.SampleHeight(vector.x, vector.y);
			int num20 = FallHurt.Falls - falls;
			Note(string.Format("walked down from {0},{1} ({2:0.0} high): {3:0.0} down over {4:0.0} tiles, steepest {5:0.0} a tile; {6} falls counted{7}", num7 % width, num7 / width, num9, num19, num11, num10, num20, (num20 > 0) ? $" (the last {FallHurt.LastDrop:0.0} down)" : ""));
			Check("walking down the land's steepest way is no fall (only a drop hurts)", num20 == 0 || num10 > 4f, $"{num20} falls, steepest {num10:0.0} a tile");
			FallHurt.Reset();
			CivilTest.Kill(actor);
			yield return null;
		}

		private IEnumerator Flat()
		{
			Relief relief = _s.Relief;
			Check("the card answered itself Flat (a test)", ReliefWorld.Land == 1, "land " + ReliefWorld.Land);
			Check("a Flat world has no generated relief", relief.Gen == null);
			Check("...and nothing dug yet", relief.EditedCount() == 0);
			yield return Stale("the land is up to date read in full");
			PickViews();
			foreach (var view in _views)
			{
				yield return Frame(view, "flat");
			}
		}

		private IEnumerator Stale(string what)
		{
			Config.paused = true;
			int w = MapBox.width;
			int height = MapBox.height;
			int sweep = Mathf.CeilToInt((float)height / Mathf.Max(2f, Mathf.Min(height, Mathf.Floor(6000f / (float)w))));
			for (int k = 0; k < sweep + 6; k++)
			{
				yield return null;
			}
			string[] why;
			bool[] array = _s.StaleForTest(out why);
			int num = 0;
			List<string> list = new List<string>();
			for (int i = 0; i < array.Length; i++)
			{
				if (!array[i])
				{
					continue;
				}
				WorldTile tile = World.world.GetTile(i % w, i / w);
				if (tile?.main_type == null || (!tile.main_type.lava && (tile.cur_tile_type == null || !tile.cur_tile_type.lava)))
				{
					num++;
					if (list.Count < 4)
					{
						list.Add($"{i % w},{i / w} {why[i]}");
					}
				}
			}
			Check(what, num == 0, num + " tiles stale" + ((list.Count > 0) ? (": " + string.Join(" ", list)) : ""));
			Config.paused = false;
		}

		private void PickViews()
		{
			_views.Clear();
			int width = MapBox.width;
			int height = MapBox.height;
			TerrainCache cache = _s.Cache;
			Vector2 vector = Vector2.zero;
			Vector2 vector2 = Vector2.zero;
			Vector2 vector3 = Vector2.zero;
			float num = 0f;
			float num2 = 0f;
			float num3 = 0f;
			Vector2 current_position = _king.current_position;
			for (int i = 8; i < height - 8; i += 3)
			{
				for (int j = 8; j < width - 8; j += 3)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile?.main_type == null)
					{
						continue;
					}
					float num4 = 1f / (1f + (new Vector2(j, i) - current_position).magnitude / 60f);
					int num5 = i * width + j;
					bool liquid = tile.main_type.liquid;
					byte b = (byte)((num5 < cache.SeaReach.Length) ? cache.SeaReach[num5] : 0);
					if (liquid && b > 0 && b <= 12)
					{
						float num6 = num4;
						if (num6 > num)
						{
							num = num6;
							vector = new Vector2((float)j + 0.5f, (float)i + 0.5f);
						}
					}
					if (liquid || tile.main_type.height_min < 199 || tile.main_type.height_min >= 210)
					{
						continue;
					}
					int num7 = 0;
					for (int k = 0; k < 8; k++)
					{
						WorldTile tile2 = World.world.GetTile(j + (int)(Mathf.Cos((float)k * 0.785f) * 4f), i + (int)(Mathf.Sin((float)k * 0.785f) * 4f));
						if (tile2?.main_type != null && tile2.main_type.liquid)
						{
							num7++;
						}
					}
					if (num7 > 0 && num4 * (float)num7 > num2)
					{
						num2 = num4 * (float)num7;
						vector2 = new Vector2((float)j + 0.5f, (float)i + 0.5f);
					}
					if (num4 > num3)
					{
						num3 = num4;
						vector3 = new Vector2((float)j + 0.5f, (float)i + 0.5f);
					}
				}
			}
			if (num > 0f)
			{
				_views.Add(("river", vector + new Vector2(-6f, -3f), Mathf.Atan2(3f, 6f), -12f, 2.5f));
			}
			if (num2 > 0f)
			{
				_views.Add(("coast", vector2 + new Vector2(-8f, -6f), Mathf.Atan2(6f, 8f), -8f, 4f));
			}
			if (num3 > 0f)
			{
				_views.Add(("hills", vector3 + new Vector2(-10f, -4f), Mathf.Atan2(4f, 10f), -6f, 2f));
			}
			_views.Add(("village", current_position + new Vector2(-12f, -9f), Mathf.Atan2(9f, 12f), -10f, 5f));
			_views.Add(("far", current_position + new Vector2(-40f, -30f), Mathf.Atan2(30f, 40f), -15f, 26f));
			foreach (var view in _views)
			{
				Note($"view {view.name} at {view.eye.x:F0},{view.eye.y:F0}");
			}
		}

		private IEnumerator Frame((string name, Vector2 eye, float yaw, float pitch, float height) v, string tag)
		{
			if (Mod.Interior.Inside)
			{
				Note("frame " + v.name + "_" + tag + " skipped: indoors");
				yield break;
			}
			Mod.DebugEyeSet = true;
			Mod.DebugEye = v.eye;
			Mod.DebugEyeHeight = v.height;
			Mod.SetYaw(v.yaw);
			Mod.SetPitchDegrees(v.pitch);
			yield return CivilTest.Wait(0.8f);
			Mod.SaveFrame("relief_" + v.name + "_" + tag + ".png");
			yield return null;
			Mod.DebugEyeSet = false;
		}

		private WorldTile SoilTile(Vector2 near, float from, float to, bool needBiome)
		{
			City city = _king.city;
			for (float num = from; num <= to; num += 0.5f)
			{
				for (int i = 0; i < 24; i++)
				{
					float f = (float)i * MathF.PI / 12f;
					WorldTile tile = World.world.GetTile((int)(near.x + Mathf.Cos(f) * num), (int)(near.y + Mathf.Sin(f) * num));
					if (tile?.main_type == null || tile.main_type.liquid || tile.main_type.height_min < 108 || tile.main_type.height_min >= 199 || (city != null && tile.zone?.city != city) || (needBiome && (tile.top_type == null || !tile.top_type.is_biome)) || (tile.top_type != null && !tile.top_type.is_biome) || (tile.building != null && AssetStyle.IsSolid(tile.building)) || tile.hasUnits())
					{
						continue;
					}
					bool flag = false;
					WorldTile[] neighboursAll = tile.neighboursAll;
					foreach (WorldTile worldTile in neighboursAll)
					{
						if (worldTile?.main_type != null && (worldTile.main_type.liquid || worldTile.main_type.height_min < 98))
						{
							flag = true;
						}
					}
					if (!flag)
					{
						return tile;
					}
				}
			}
			return null;
		}

		private int Index(WorldTile t)
		{
			return t.y * _s.Cache.Width + t.x;
		}

		private IEnumerator Bands()
		{
			WorldTile t = SoilTile(_king.current_position, 5f, 14f, needBiome: true) ?? SoilTile(_king.current_position, 5f, 18f, needBiome: false);
			if (!Check("a soil tile of the village's to dig", t != null))
			{
				yield break;
			}
			int i = Index(t);
			TileType startMain = t.main_type;
			TopTileType startTop = t.top_type;
			BiomeAsset biome = startTop?.biome_asset;
			Note($"digging {t.x},{t.y}: {startMain.id}/{startTop?.id}");
			TerrainCache c = _s.Cache;
			yield return null;
			float startGround = c.Ground[i];
			uint startColor = c.BaseColors[i];
			float worstJump = 0f;
			float lastGround = startGround;
			string worstAt = "";
			int steps = 0;
			int swings = 0;
			List<string> path = new List<string> { startMain.id };
			bool roseOnce = false;
			bool biomeKept = true;
			for (int k = 0; k < 160; k++)
			{
				if (steps >= 6)
				{
					break;
				}
				TileType before = t.main_type;
				Earthworks.Result res = Earthworks.Apply(t, fill: false, 1f / 6f, capped: false);
				swings++;
				if (res.Refused != null)
				{
					Note($"refused after {swings} swings at {t.main_type.id}: {res.Refused}");
					break;
				}
				yield return null;
				yield return null;
				float num = c.Ground[i];
				if (num > lastGround + 0.02f)
				{
					roseOnce = true;
				}
				float num2 = Mathf.Abs(num - lastGround);
				if (res.Stepped)
				{
					steps++;
					path.Add(t.main_type.id);
					if (num2 > worstJump)
					{
						worstJump = num2;
						worstAt = before.id + "->" + t.main_type.id;
					}
					if (biome != null && t.main_type.can_be_biome && (t.top_type == null || t.top_type.biome_asset != biome))
					{
						biomeKept = false;
					}
				}
				lastGround = num;
				if (t.main_type.liquid)
				{
					Note("the hole filled with water at " + t.main_type.id);
					break;
				}
			}
			Note(string.Format("dug {0} swings, {1} band steps: {2}; ground {3:F2} -> {4:F2}", swings, steps, string.Join(" > ", path), startGround, c.Ground[i]));
			Check("digging stepped the game's tile down", steps >= 2, steps + " steps");
			Check("the ground never rose while digging", !roseOnce);
			Check("no jump as a band steps", worstJump < 0.25f, $"worst {worstJump:F2} at {worstAt}");
			Check("the biome kept on the soil below", biomeKept, biome?.id);
			Check("the dug tile is marked shaped", (c.Marks[i] & 1) != 0);
			Check("...and drawn rounded", (c.Flags[i] & 0x40) != 0);
			bool flag = _s.Relief.RawOf(i) > 0.5f;
			Check("its earth is raw", flag || t.main_type.sand || t.main_type.height_min < 98, $"raw {_s.Relief.RawOf(i):F2} colour {startColor:X6} -> {c.BaseColors[i]:X6}");
			float num3 = c.SampleHeight((float)t.x - 0.5f, (float)t.y + 0.5f);
			float num4 = c.SampleHeight((float)t.x + 0.5f, (float)t.y + 0.5f);
			Check("the hole's wall slopes (the edge between its rim and its floor)", num3 > num4, $"edge {num3:F2} middle {num4:F2}");
			if (t.main_type.liquid)
			{
				yield break;
			}
			int up = 0;
			for (int k = 0; k < 200; k++)
			{
				if (t.main_type == startMain)
				{
					break;
				}
				Earthworks.Result result = Earthworks.Apply(t, fill: true, 1f / 6f, capped: false);
				if (result.Refused != null)
				{
					Note("fill refused: " + result.Refused);
					break;
				}
				if (result.Stepped)
				{
					up++;
				}
				yield return null;
			}
			yield return null;
			yield return null;
			Check("filled back to its own band", t.main_type == startMain, $"{t.main_type.id} after {up} steps up");
			BiomeAsset biomeAsset = null;
			Dictionary<BiomeAsset, int> dictionary = new Dictionary<BiomeAsset, int>();
			WorldTile[] neighboursAll = t.neighboursAll;
			foreach (WorldTile worldTile in neighboursAll)
			{
				BiomeAsset biomeAsset2 = ((worldTile?.top_type != null && worldTile.top_type.is_biome) ? worldTile.top_type.biome_asset : null);
				if (biomeAsset2 != null)
				{
					dictionary.TryGetValue(biomeAsset2, out var value);
					dictionary[biomeAsset2] = value + 1;
					if (biomeAsset == null || dictionary[biomeAsset2] > dictionary[biomeAsset])
					{
						biomeAsset = biomeAsset2;
					}
				}
			}
			BiomeAsset biomeAsset3 = ((t.top_type != null && t.top_type.is_biome) ? t.top_type.biome_asset : null);
			Check("...its biome back on it (the land round it grows over)", startTop == null || (biomeAsset3 != null && (biomeAsset3 == biome || biomeAsset3 == biomeAsset)), t.top_type?.id + ", was " + biome?.id + ", round it " + biomeAsset?.id);
			Check("...and the ground about where it was", Mathf.Abs(c.Ground[i] - startGround) < 0.2f, $"{startGround:F2} -> {c.Ground[i]:F2}");
			Note($"band steps by hand so far: down {Earthworks.StepsDown}, up {Earthworks.StepsUp}");
		}

		private IEnumerator Hand()
		{
			Actor me = _king;
			Supplies.Give(me, "tw_item_shovel", 1);
			yield return CivilTest.Wait(0.3f);
			int num = BagOrder.Of(me).FindIndex((string key) => BagOrder.ResourceOf(key) == "tw_item_shovel");
			if (!Check("a shovel in the bag", num >= 0))
			{
				yield break;
			}
			if (num >= Hotbar.Size(me))
			{
				BagOrder.Swap(me, num, 0);
				num = 0;
			}
			Hotbar.Choose(me, num, wheel: false);
			Mod.Holstered = false;
			yield return CivilTest.Wait(0.5f);
			Check("the shovel is the tool in hand", Earthworks.ToolOf(me) == "tw_item_shovel");
			WorldTile t = SoilTile(me.current_position, 1.4f, 1.9f, needBiome: false);
			if (t == null)
			{
				WorldTile worldTile = SoilTile(me.current_position, 2f, 40f, needBiome: false);
				if (worldTile != null)
				{
					for (int num2 = 0; num2 < 8; num2++)
					{
						if (t != null)
						{
							break;
						}
						float f = (float)num2 * MathF.PI / 4f;
						WorldTile tile = World.world.GetTile((int)((float)worldTile.x + 0.5f + Mathf.Cos(f) * 1.6f), (int)((float)worldTile.y + 0.5f + Mathf.Sin(f) * 1.6f));
						if (tile?.main_type != null && !tile.main_type.liquid && !tile.main_type.block)
						{
							me.current_position = new Vector2((float)worldTile.x + 0.5f + Mathf.Cos(f) * 1.6f, (float)worldTile.y + 0.5f + Mathf.Sin(f) * 1.6f);
							try
							{
								me.dirty_current_tile = true;
							}
							catch
							{
							}
							t = worldTile;
						}
					}
					yield return CivilTest.Wait(0.5f);
					Note($"stood by open ground at {t?.x},{t?.y} (none within reach where the body was)");
				}
			}
			if (Check("ground within reach to dig", t != null))
			{
				int i = Index(t);
				Vector2 at = new Vector2((float)t.x + 0.5f, (float)t.y + 0.5f);
				Vector2 current_position = me.current_position;
				float dist = (at - current_position).magnitude;
				Mod.DebugEyeSet = true;
				Mod.DebugEye = current_position;
				Mod.DebugEyeHeight = 0f;
				Mod.SetYaw(Mathf.Atan2(at.y - current_position.y, at.x - current_position.x));
				float b = Mod.EyeForTest.z - _s.Cache.SampleHeight(at.x, at.y);
				Mod.SetPitchDegrees((0f - Mathf.Atan2(Mathf.Max(0.3f, b), dist)) * 57.29578f);
				yield return CivilTest.Wait(0.6f);
				b = Mod.EyeForTest.z - _s.Cache.SampleHeight(at.x, at.y);
				Mod.SetPitchDegrees((0f - Mathf.Atan2(Mathf.Max(0.3f, b), dist)) * 57.29578f);
				yield return CivilTest.Wait(0.4f);
				Check("the prompt offers to dig", Mod.Prompt != null && Mod.Prompt.Contains("Dig"), Mod.Prompt);
				int dirt0 = Civil.BagCount(me, "tw_item_dirt");
				int swings0 = Earthworks.Swings;
				float e0 = _s.Relief.EditOf(i);
				TileType m0 = t.main_type;
				Earthworks.ForceDig = true;
				yield return CivilTest.Wait(3.6f);
				Earthworks.ForceDig = false;
				int num3 = Earthworks.Swings - swings0;
				Check("held, the shovel swings", num3 >= 4, num3 + " swings");
				Check("the ground under the crosshair went down", _s.Relief.EditOf(i) < e0 - 0.01f || t.main_type != m0, $"{e0:F2} -> {_s.Relief.EditOf(i):F2}, {m0.id} -> {t.main_type.id}");
				int dirt1 = Civil.BagCount(me, "tw_item_dirt");
				Check("the bag gained dirt", dirt1 > dirt0, $"{dirt0} -> {dirt1}");
				Mod.SaveFrame("relief_dug_hand.png");
				Earthworks.Mode = Earthworks.Move.Fill;
				Earthworks.ForceDig = true;
				yield return CivilTest.Wait(2.4f);
				Earthworks.ForceDig = false;
				Earthworks.Mode = Earthworks.Move.Dig;
				int num4 = Civil.BagCount(me, "tw_item_dirt");
				Check("filling spent dirt", num4 < dirt1, $"{dirt1} -> {num4}");
				Mod.DebugEyeSet = false;
				Hotbar.Choose(me, Hotbar.Size(me) - 1, wheel: false);
			}
		}

		private IEnumerator Rules()
		{
			Actor other = Village("human", 3, 0, World.world.GetTile((int)_king.current_position.x, (int)_king.current_position.y), 40f);
			yield return CivilTest.Wait(1.5f);
			if (other?.city != null)
			{
				WorldTile worldTile = null;
				using (List<TileZone>.Enumerator enumerator = other.city.zones.GetEnumerator())
				{
					if (enumerator.MoveNext())
					{
						worldTile = enumerator.Current.centerTile;
					}
				}
				Check("another town's land refused", worldTile != null && !Earthworks.MayDig(_king, worldTile, out var why), (worldTile == null) ? "no zone" : (Earthworks.MayDig(_king, worldTile, out why) ? "allowed" : why));
			}
			else
			{
				Note("no second village: the land rule's refusal not checked");
			}
			WorldTile sand = null;
			for (int i = 4; i < MapBox.height - 4; i += 2)
			{
				if (sand != null)
				{
					break;
				}
				for (int j = 4; j < MapBox.width - 4; j += 2)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile?.main_type == null || !tile.main_type.sand || tile.main_type.liquid)
					{
						continue;
					}
					bool flag = false;
					WorldTile[] neighboursAll = tile.neighboursAll;
					foreach (WorldTile worldTile2 in neighboursAll)
					{
						if (worldTile2?.main_type != null && (worldTile2.main_type.liquid || worldTile2.main_type.height_min < 98))
						{
							flag = true;
						}
					}
					if (!flag && (tile.building == null || !AssetStyle.IsSolid(tile.building)))
					{
						sand = tile;
						break;
					}
				}
			}
			if (sand == null)
			{
				Note("no dry sand: the cap not checked");
				yield break;
			}
			TileType was = sand.main_type;
			string refused = null;
			for (int l = 0; l < 40; l++)
			{
				if (refused != null)
				{
					break;
				}
				refused = Earthworks.Apply(sand, fill: false, 1f / 6f, capped: true).Refused;
				yield return null;
			}
			Check("a locked run's hand cuts no lower than the beach", sand.main_type == was && refused != null, was.id + " -> " + sand.main_type.id + ": " + refused);
			for (int m = 0; m < 40; m++)
			{
				if (!(Mathf.Abs(_s.Relief.EditOf(Index(sand))) > 3f / 128f))
				{
					break;
				}
				Earthworks.Apply(sand, fill: true, 1f / 6f, capped: false);
			}
		}

		private IEnumerator Earthshaping()
		{
			Check("not known before it is taught", Abilities.EarthCastingsForTest(_king) == 0);
			Abilities.TeachEarth(_king);
			Check("taught: three castings on X", Abilities.EarthCastingsForTest(_king) == 3);
			WorldTile t = SoilTile(_king.current_position, 6f, 16f, needBiome: false);
			if (Check("ground to shape", t != null))
			{
				CivilTest.ClearBox(t.x - 3, t.y - 3, t.x + 3, t.y + 3);
				int i = Index(t);
				TileType was = t.main_type;
				float e0 = _s.Relief.EditOf(i);
				Vector2 at = new Vector2((float)t.x + 0.5f, (float)t.y + 0.5f);
				Abilities.CastEarthForTest(_king, at, 0);
				yield return null;
				Check("raise: the ground rose a band", t.main_type != was || _s.Relief.EditOf(i) > e0 + 0.05f, $"{was.id} -> {t.main_type.id}, moved {Abilities.EarthMoved} tiles");
				Abilities.CastEarthForTest(_king, at, 1);
				Abilities.CastEarthForTest(_king, at, 1);
				yield return null;
				Check("sink: and down again into a bowl", Abilities.EarthMoved > 0 && t.main_type.height_min <= was.height_min, $"{t.main_type.id}, moved {Abilities.EarthMoved}");
				Abilities.CastEarthForTest(_king, at, 2);
				Check("split: a trench across", Abilities.EarthMoved >= 8, Abilities.EarthMoved + " tiles");
				yield return CivilTest.Wait(0.5f);
			}
		}

		private IEnumerator SaveAndLoad()
		{
			Relief relief = _s.Relief;
			WorldTile worldTile = SoilTile(_king.current_position, 3f, 10f, needBiome: false);
			if (!Check("ground to dig before saving", worldTile != null))
			{
				yield break;
			}
			int i = Index(worldTile);
			for (int j = 0; j < 2; j++)
			{
				Earthworks.Apply(worldTile, fill: false, 1f / 6f, capped: false);
			}
			short want = (short)((relief.Edit != null) ? relief.Edit[i] : 0);
			int edited = relief.EditedCount();
			ReliefWorld.SaveNow();
			byte[] array;
			try
			{
				SavedMap savedMap = SaveManager.currentWorldToSavedMap();
				string text = savedMap.toJson();
				Check("the save holds the relief", text.Contains("fp_relief"), text.Length + " chars");
				Check("...and the world's land", text.Contains("fp_world_land"));
				array = savedMap.toZip();
			}
			catch (Exception ex)
			{
				Check("the world saved to bytes", ok: false, ex.Message);
				yield break;
			}
			Note($"saved {array.Length} bytes with {edited} tiles dug; loading it back");
			try
			{
				SaveManager.loadMapFromBytes(array);
			}
			catch (Exception ex2)
			{
				Check("the world loaded from bytes", ok: false, ex2.Message);
				yield break;
			}
			for (float s = 0f; s < 60f; s += 0.5f)
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
				if (!flag && Config.game_loaded && s > 2f)
				{
					break;
				}
			}
			yield return CivilTest.Wait(1f);
			try
			{
				SaveCustomData saveCustomData = World.world.map_stats?.custom_data;
				string pResult = null;
				int pResult2 = -1;
				saveCustomData?.get("fp_relief", out pResult, null);
				saveCustomData?.get("fp_world_land", out pResult2, -1);
				Note(string.Format("after the load: world data {0}, land {1}, record {2}", (saveCustomData == null) ? "none" : "there", pResult2, (pResult == null) ? "none" : (pResult.Length + " chars")));
			}
			catch (Exception ex3)
			{
				Note("world data: " + ex3.Message);
			}
			_s.EnsureWorld();
			yield return null;
			relief = _s.Relief;
			short num = (short)((relief.Edit != null && i < relief.Edit.Length) ? relief.Edit[i] : 0);
			Check("the dug tile came back", num == want && want != 0, $"{want} -> {num}");
			Check("...with all the others", relief.EditedCount() == edited, $"{edited} -> {relief.EditedCount()}");
			Check("...and the world's land", ReliefWorld.Land == 1, "land " + ReliefWorld.Land);
			_king = null;
			foreach (Actor unit in World.world.units)
			{
				if (unit != null && unit.isAlive() && unit.city != null && unit.asset != null && unit.asset.id == "human" && unit.isAdult() && !unit.isInsideSomething())
				{
					_king = unit;
					break;
				}
			}
			if (_king != null)
			{
				Possess(_king);
				yield return CivilTest.Wait(3f);
			}
		}

		private static byte[] CodesForTest()
		{
			int width = MapBox.width;
			int height = MapBox.height;
			byte[] array = new byte[width * height];
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < width; j++)
				{
					TileType tileType = World.world.GetTile(j, i)?.main_type;
					array[i * width + j] = (byte)((tileType != null) ? (tileType.lava ? 1 : ((!tileType.liquid && !tileType.ocean) ? ((tileType.height_min >= 230) ? 4 : ((byte)TerrainModel.ClassOf(tileType.height_min))) : 0)) : 0);
				}
			}
			return array;
		}

		private IEnumerator Terrainy()
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			ReliefWorld.Choose(2);
			_s.EnsureWorld();
			long ms = stopwatch.ElapsedMilliseconds;
			yield return null;
			Relief r = _s.Relief;
			Check("Terrainy: relief generated", r.Terrainy && r.Gen != null, $"in {ms} ms with the land built");
			if (r.Gen == null)
			{
				yield break;
			}
			int w = r.Width;
			int h = r.Height;
			int num = 0;
			float num2 = 0f;
			float num3 = 0f;
			float num4 = 0f;
			bool[] array = new bool[r.Gen.Length];
			int num5 = Mathf.CeilToInt(ReliefGen.TarnRadius + 0.5f);
			foreach (int item in ReliefGen.Chosen)
			{
				for (int i = Mathf.Max(0, item / w - num5); i <= Mathf.Min(h - 1, item / w + num5); i++)
				{
					for (int j = Mathf.Max(0, item % w - num5); j <= Mathf.Min(w - 1, item % w + num5); j++)
					{
						array[i * w + j] = true;
					}
				}
			}
			for (int k = 0; k < r.Gen.Length; k++)
			{
				if (r.Gen[k] != 0)
				{
					num++;
					float b = (float)r.Gen[k] * (1f / 64f);
					if (array[k])
					{
						num4 = Mathf.Min(num4, b);
						continue;
					}
					num2 = Mathf.Min(num2, b);
					num3 = Mathf.Max(num3, b);
				}
			}
			Note($"generated relief: {num} of {r.Gen.Length} tiles, {num2:F2} .. {num3:F2} tiles away from the tarns, the tarns' cirques down to {num4:F2}; {ReliefGen.Chosen.Count} tarns");
			Check("within walkable drama (the valleys and ridges)", num2 >= 0f - ReliefGen.MostDown - 0.01f && num3 <= ReliefGen.MostUp + 0.01f, $"{num2:F2} .. {num3:F2}");
			Check("...the tarns' cirques no deeper than their cut", num4 >= 0f - ReliefGen.TarnCut - ReliefGen.MostDown - 0.01f, $"{num4:F2}");
			TerrainCache c = _s.Cache;
			int num6 = 0;
			int num7 = 0;
			StringBuilder stringBuilder = new StringBuilder();
			for (int l = 1; l < h - 1; l++)
			{
				for (int m = 1; m < w - 1; m++)
				{
					int num8 = l * w + m;
					if ((c.Flags[num8] & 3) != 0)
					{
						continue;
					}
					if ((c.Marks[num8] & 2) != 0)
					{
						num7++;
					}
					if (r.EditOf(num8) == 0f)
					{
						bool num9 = (c.Flags[num8 - 1] & 1) != 0 || (c.Flags[num8 + 1] & 1) != 0 || (c.Flags[num8 - w] & 1) != 0 || (c.Flags[num8 + w] & 1) != 0;
						WorldTile tile = World.world.GetTile(m, l);
						if (num9 && c.Ground[num8] < -0.02f && tile?.main_type != null && tile.main_type.height_min >= 98 && num6++ < 4)
						{
							stringBuilder.Append($" {m},{l} {tile.main_type.id} ground {c.Ground[num8]:F2} gen {r.GenOf(num8):F2};");
						}
					}
				}
			}
			Check("Terrainy: no land beside water lies under it", num6 == 0, num6 + " tiles" + stringBuilder);
			Note($"cliff tiles: {num7}");
			yield return Stale("Terrainy: the land is up to date read in full");
			foreach (var view in _views)
			{
				yield return Frame(view, "terrainy");
			}
			int pixels = Mod.Settings.PixelDistance;
			Mod.Settings.PixelDistance = 1;
			foreach (var view2 in _views)
			{
				if (view2.name == "far" || view2.name == "hills")
				{
					yield return Frame(view2, "terrainy_pixel");
				}
			}
			Mod.Settings.PixelDistance = pixels;
			short[] array2 = ReliefGen.Build(w, h, CodesForTest(), _s.Forms, c.SeaReach, r.Seed);
			short[] array3 = ReliefGen.Build(w, h, CodesForTest(), _s.Forms, c.SeaReach, r.Seed);
			int num10 = 0;
			for (int n = 0; n < array2.Length; n++)
			{
				if (array2[n] != array3[n])
				{
					num10++;
				}
			}
			Check("Terrainy: the same land makes the same relief", num10 == 0, num10 + " tiles differ");
			Stopwatch stopwatch2 = Stopwatch.StartNew();
			for (int num11 = 0; num11 < 3; num11++)
			{
				ReliefGen.Build(w, h, CodesForTest(), _s.Forms, c.SeaReach, r.Seed);
			}
			Note($"generating the relief: {(float)stopwatch2.ElapsedMilliseconds / 3f:F0} ms for {w}x{h}");
			ReliefWorld.Choose(1);
			_s.EnsureWorld();
			Check("Flat again: nothing generated", _s.Relief.Gen == null && !_s.Relief.Terrainy);
		}
	}
}
