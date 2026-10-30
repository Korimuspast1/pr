using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FirstPerson.Core;
using FirstPerson.Net;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class Earthworks
	{
		public struct Result
		{
			public bool Changed;

			public bool Stepped;

			public string Refused;

			public float Before;

			public float After;
		}

		public enum Move
		{
			Dig,
			Fill
		}

		public const string Shovel = "tw_item_shovel";

		public const string Pickaxe = "tw_item_pickaxe";

		public const string Dirt = "tw_item_dirt";

		public const string Sand = "tw_item_sand";

		public const string Stone = "stone";

		public const string Iron = "common_metals";

		public const string Gold = "gold";

		public const string Mythril = "mythril";

		public const float Reach = 2.3f;

		private const float SwingSeconds = 0.55f;

		public static float SoilSwings = 6f;

		public static float RockSwings = 15f;

		public static float PickInSoil = 2f;

		public const float MinThick = 0.12f;

		public const float LastBand = 0.6f;

		private const float Tolerance = 0.01578125f;

		public static float PerBand = 2f;

		public static float IronChance = 0.3f;

		public static float GoldChance = 0.06f;

		public static float MythrilChance = 0.015f;

		internal const string SkillsKey = "fp_skills";

		private static int _busyFrame = -10;

		private static float _blowAt;

		private static float _dirt;

		private static float _sand;

		private static float _stone;

		private static float _spend;

		private static float _toastAt = -9f;

		private static readonly System.Random Rng = new System.Random();

		internal static int StepsDown;

		internal static int StepsUp;

		internal static int Swings;

		internal static Result Last;

		internal static bool ForceDig;

		internal static bool ForceLevel;

		private const int Yard = 2;

		public static readonly string[] MoveNames = new string[2] { "Dig", "Fill" };

		public static Move Mode;

		public static int Size = 1;

		public const int MostSize = 4;

		private static readonly float[] Quicker = new float[5] { 1f, 1f, 1.12f, 1.25f, 1.4f };

		private static readonly float[] Deeper = new float[5] { 1f, 1f, 1.12f, 1.25f, 1.43f };

		private static readonly List<(WorldTile t, float w)> _patch = new List<(WorldTile, float)>();

		private static readonly List<(WorldTile t, float w)> _preview = new List<(WorldTile, float)>();

		private static int _previewFrame = -10;

		internal static bool LastFilled;

		public const float MasterXp = 3000f;

		public static bool Busy => Time.frameCount - _busyFrame <= 1;

		private static Move NextMove
		{
			get
			{
				if (Mode != Move.Dig)
				{
					return Move.Dig;
				}
				return Move.Fill;
			}
		}

		internal static IReadOnlyList<(WorldTile t, float w)> Preview
		{
			get
			{
				if (Time.frameCount - _previewFrame > 1)
				{
					return Array.Empty<(WorldTile, float)>();
				}
				return _preview;
			}
		}

		internal static bool PreviewLevel { get; private set; }

		private static string Wide
		{
			get
			{
				if (Size <= 1)
				{
					return "";
				}
				return " " + Size + " wide";
			}
		}

		public static string ToolOf(Actor a)
		{
			if (a == null)
			{
				return null;
			}
			try
			{
				Supplies.Kind kind = Hotbar.Supply(a);
				string text = kind?.Base ?? kind?.Id;
				return (text == "tw_item_shovel" || text == "tw_item_pickaxe") ? text : null;
			}
			catch
			{
				return null;
			}
		}

		public static bool IsRock(TileTypeBase t)
		{
			if (t != null)
			{
				return t.height_min >= 199;
			}
			return false;
		}

		internal static TileType LandOf(WorldTile t)
		{
			WorldSampler sampler = ReliefWorld.Sampler;
			if (sampler != null && sampler.Relief.Lake(t.y * sampler.Relief.Width + t.x, out var _, out var main, out var _) && WorldSampler.TypeOfId(main) is TileType result)
			{
				return result;
			}
			return t.main_type;
		}

		public static bool MayDig(Actor who, WorldTile t, out string why)
		{
			why = null;
			if (OwnLand(who, t))
			{
				return true;
			}
			City city = null;
			City city2 = null;
			try
			{
				city = t.zone?.city;
				city2 = who?.city;
				if (city2 != null && !city2.isAlive())
				{
					city2 = null;
				}
				if (city != null && !city.isAlive())
				{
					city = null;
				}
			}
			catch
			{
			}
			if (city2 != null)
			{
				if (city == city2)
				{
					return true;
				}
				why = ((city != null) ? ("This is " + SafeName(city) + "'s land.") : ("Only the land of " + SafeName(city2) + "."));
				return false;
			}
			if (Run.Current == null || city == null)
			{
				return true;
			}
			why = "This is " + SafeName(city) + "'s land.";
			return false;
		}

		public static bool OwnLand(Actor who, WorldTile t)
		{
			if (who == null || t == null)
			{
				return false;
			}
			Building building = null;
			try
			{
				building = HomeLife.OwnHouse(who) ?? LandPlot.PendingSite(who);
			}
			catch
			{
			}
			if (building == null || building.asset == null || building.current_tile == null)
			{
				return false;
			}
			try
			{
				BuildingFundament fundament = building.asset.fundament;
				WorldTile current_tile = building.current_tile;
				int num = fundament?.left ?? 0;
				int num2 = fundament?.right ?? 0;
				int num3 = fundament?.top ?? 0;
				int num4 = fundament?.bottom ?? 0;
				return t.x >= current_tile.x - num - 2 && t.x <= current_tile.x + num2 + 2 && t.y >= current_tile.y - num4 - 2 && t.y <= current_tile.y + num3 + 2;
			}
			catch
			{
				return false;
			}
		}

		private static string SafeName(City c)
		{
			try
			{
				return c.name;
			}
			catch
			{
				return "a town";
			}
		}

		public static int LevelOf(Actor a)
		{
			try
			{
				Supplies.Kind kind = ((a != null) ? Hotbar.Supply(a) : null);
				return (kind == null) ? 1 : Mathf.Clamp(kind.Level, 1, 4);
			}
			catch
			{
				return 1;
			}
		}

		public static float SwingTime(int level)
		{
			return 0.55f / Quicker[Mathf.Clamp(level, 1, 4)];
		}

		public static int MaxSize(Actor a)
		{
			return 1 + Mathf.Clamp(Mathf.FloorToInt(Mastery(a) * 3f + 0.0001f), 0, 3);
		}

		public static void SwitchMove(Action<string> toast)
		{
			Mode = NextMove;
			Sounds.PlayUI("event:/SFX/Ui/Click");
			toast?.Invoke((Mode == Move.Dig) ? "Dig" : "Fill with dirt, sand or stone");
		}

		public static bool Widen(Actor a, int by)
		{
			int max = MaxSize(a);
			int size = Size;
			Size = Mathf.Clamp(Size + by, 1, max);
			if (Size != size)
			{
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
			if (Size == size)
			{
				return by < 0;
			}
			return true;
		}

		internal static void Patch(Vector2 at, int size, bool flat, List<(WorldTile t, float w)> into)
		{
			into.Clear();
			WorldTile tile = World.world.GetTile(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y));
			if (size <= 1)
			{
				if (tile != null)
				{
					into.Add((tile, 1f));
				}
				return;
			}
			Vector2 vector = ((size % 2 == 1) ? new Vector2(Mathf.Floor(at.x) + 0.5f, Mathf.Floor(at.y) + 0.5f) : new Vector2(Mathf.Round(at.x), Mathf.Round(at.y)));
			float num = (float)size * 0.5f;
			for (int i = Mathf.FloorToInt(vector.y - num); i <= Mathf.FloorToInt(vector.y + num); i++)
			{
				for (int j = Mathf.FloorToInt(vector.x - num); j <= Mathf.FloorToInt(vector.x + num); j++)
				{
					float magnitude = (new Vector2((float)j + 0.5f, (float)i + 0.5f) - vector).magnitude;
					if (!(magnitude > num))
					{
						WorldTile tile2 = World.world.GetTile(j, i);
						if (tile2?.main_type != null)
						{
							float num2 = magnitude / num;
							into.Add((tile2, flat ? 1f : Mathf.Max(0.5f, 1f - 0.5f * num2 * num2)));
						}
					}
				}
			}
		}

		public static bool HandWork(Work w, Actor host, Vector2 you, bool mouseFree, bool holstered, CombatFeedback feedback, Action<string> toast, SceneCollector scene = null)
		{
			string text = ToolOf(host);
			if (text == null || holstered)
			{
				return false;
			}
			WorldSampler sampler = ReliefWorld.Sampler;
			if (sampler == null || !sampler.Ready)
			{
				return false;
			}
			if (Size > MaxSize(host))
			{
				Size = MaxSize(host);
			}
			Vec3 point;
			WorldTile tile;
			try
			{
				if (!FirstPersonAim.Ready(host) || !FirstPersonAim.Point(out point, out var creature) || creature != null)
				{
					return false;
				}
				if ((new Vector2(point.X, point.Y) - you).magnitude > 2.3f)
				{
					return false;
				}
				tile = World.world.GetTile(Mathf.FloorToInt(point.X), Mathf.FloorToInt(point.Y));
			}
			catch
			{
				return false;
			}
			if (tile == null || tile.main_type == null)
			{
				return false;
			}
			_busyFrame = Time.frameCount;
			Vector2 at = new Vector2(point.X, point.Y);
			bool flag = (PreviewLevel = mouseFree && (ForceLevel || PlayInput.GetMouseButton(1)));
			if (Size > 1 || flag)
			{
				Patch(at, Size, flag, _preview);
				_previewFrame = Time.frameCount;
			}
			if (!flag)
			{
				return DigWork(w, host, tile, at, text, mouseFree, feedback, toast, scene);
			}
			return LevelWork(w, host, you, tile, at, text, feedback, toast, scene);
		}

		private static bool DigWork(Work w, Actor host, WorldTile t, Vector2 at, string tool, bool mouseFree, CombatFeedback feedback, Action<string> toast, SceneCollector scene)
		{
			WorldSampler sampler = ReliefWorld.Sampler;
			bool canDig;
			bool canFill;
			string text = WhyNot(host, t, tool, out canDig, out canFill);
			if (!canDig && !canFill)
			{
				w.Needs = text;
				return text != null;
			}
			bool flag = Mode == Move.Fill;
			int i = t.y * sampler.Cache.Width + t.x;
			bool flag2 = LandOf(t).liquid || LandOf(t).ocean;
			float num = (flag2 ? 0f : DepthBelowAround(sampler.Cache, t.x, t.y));
			string text2 = ((Math.Abs(num) < 0.05f) ? "" : ((num > 0f) ? $" ({num:0.0} below)" : $" ({0f - num:0.0} above)"));
			string text3 = "|R:" + MoveNames[(int)NextMove];
			if (flag ? (!canFill) : (!canDig))
			{
				w.Needs = (flag ? "No higher" : (flag2 ? "Under water" : "Can't dig here")) + "|Hold RMB:Level" + text3;
				return true;
			}
			w.Needs = (flag ? "Hold LMB:Fill" : "Hold LMB:Dig") + Wide + text2 + "|Hold RMB:Level" + text3;
			w.Progress = BandProgress(sampler, t, i, flag && FillWith(host, t) == "tw_item_sand");
			if (!mouseFree || (!ForceDig && !PlayInput.GetMouseButton(0)))
			{
				return true;
			}
			int num2 = LevelOf(host);
			float unscaledTime = Time.unscaledTime;
			if (unscaledTime - _blowAt < SwingTime(num2))
			{
				return true;
			}
			_blowAt = unscaledTime;
			if (flag && FillWith(host, t) == null)
			{
				Say(toast, IsRock(LandOf(t)) ? "No stone to fill with." : "No dirt or sand to fill with.");
				return true;
			}
			Patch(at, Size, flat: false, _patch);
			bool flag3 = false;
			bool flag4 = false;
			bool capped = Run.LockedOn(host);
			string text4 = null;
			foreach (var item3 in _patch)
			{
				WorldTile item = item3.t;
				float item2 = item3.w;
				bool canDig2 = canDig;
				bool canFill2 = canFill;
				if ((item != t && WhyNot(host, item, tool, out canDig2, out canFill2) != null && !canDig2 && !canFill2) || (flag ? (!canFill2) : (!canDig2)))
				{
					continue;
				}
				bool rock = IsRock(LandOf(item));
				string text5 = null;
				if (flag && (text5 = FillWith(host, item)) == null)
				{
					break;
				}
				float share = item2 * Deeper[num2] / BaseSwings(tool, rock);
				if (WorldSync.ForwardDig(host, item.x, item.y, flag, share, text5 == "tw_item_sand"))
				{
					flag4 = (flag3 = true);
					Account(host, item, flag, rock, share, stepped: false, text5, toast);
					continue;
				}
				LandOf(item);
				Result last = Apply(item, flag, share, capped, float.NaN, text5 == "tw_item_sand");
				if (item == t)
				{
					Last = last;
				}
				if (last.Refused != null)
				{
					if (item == t || text4 == null)
					{
						text4 = last.Refused;
					}
				}
				else
				{
					Account(host, item, flag, rock, share, last.Stepped, text5, toast);
					flag3 = true;
				}
			}
			if (!flag3)
			{
				if (text4 != null)
				{
					Say(toast, text4);
				}
				return true;
			}
			Swing(feedback, t, at, tool, IsRock(LandOf(t)), flag, host, scene);
			if (flag4)
			{
				return true;
			}
			Swings++;
			Learn(host);
			return true;
		}

		private static bool LevelWork(Work w, Actor host, Vector2 you, WorldTile t, Vector2 at, string tool, CombatFeedback feedback, Action<string> toast, SceneCollector scene)
		{
			bool canDig;
			bool canFill;
			string text = WhyNot(host, t, tool, out canDig, out canFill);
			if (!canDig && !canFill)
			{
				w.Needs = text;
				return text != null;
			}
			WorldTile tile = World.world.GetTile(Mathf.FloorToInt(you.x), Mathf.FloorToInt(you.y));
			float num = WorkHeight(tile);
			if (float.IsNaN(num) || tile.main_type.liquid)
			{
				w.Needs = "Stand on dry ground to level to it";
				return true;
			}
			float num2 = WorkHeight(t);
			string text2 = ((float.IsNaN(num2) || Mathf.Abs(num2 - num) < 0.05f) ? "" : ((num2 > num) ? $" ({num2 - num:0.0} above)" : $" ({num - num2:0.0} below)"));
			w.Needs = "Hold RMB:Level" + Wide + " to your feet" + text2;
			int level = LevelOf(host);
			float unscaledTime = Time.unscaledTime;
			if (unscaledTime - _blowAt < SwingTime(level))
			{
				return true;
			}
			_blowAt = unscaledTime;
			Patch(at, Size, flat: true, _patch);
			bool flag = false;
			bool flag2 = false;
			bool flag3 = false;
			bool capped = Run.LockedOn(host);
			bool flag4 = false;
			bool flag5 = false;
			string text3 = null;
			foreach (var item2 in _patch)
			{
				WorldTile item = item2.t;
				bool canDig2 = canDig;
				bool canFill2 = canFill;
				if (item != t && WhyNot(host, item, tool, out canDig2, out canFill2) != null && !canDig2 && !canFill2)
				{
					continue;
				}
				float num3 = WorkHeight(item);
				if (float.IsNaN(num3) || Mathf.Abs(num3 - num) <= 0.0315625f)
				{
					continue;
				}
				bool flag6 = num3 < num;
				if (flag6 ? (!canFill2) : (!canDig2))
				{
					continue;
				}
				bool rock = IsRock(LandOf(item));
				string text4 = null;
				if (flag6 && (text4 = FillWith(host, item)) == null)
				{
					flag3 = true;
					continue;
				}
				float num4 = num;
				float num5 = float.MaxValue;
				if (flag6)
				{
					num5 = Mathf.Max(0f, (float)Civil.BagCount(host, text4) - _spend) / PerBand;
					if (!LandOf(item).liquid && !LandOf(item).ocean)
					{
						float num6 = BandThick(item, down: false, text4 == "tw_item_sand");
						if ((num - num3) / num6 > num5)
						{
							num4 = num3 + num5 * num6;
							flag3 = true;
						}
						if (num4 <= num3 + 0.01578125f)
						{
							continue;
						}
					}
				}
				if (WorldSync.ForwardDigTo(host, item.x, item.y, flag6, 1f, num4, text4 == "tw_item_sand"))
				{
					flag2 = (flag = true);
					Account(host, item, flag6, rock, Mathf.Min(num5, Mathf.Abs(num4 - num3) / BandThick(item, !flag6, text4 == "tw_item_sand")), stepped: false, text4, toast);
					flag4 = flag4 || flag6;
					flag5 = flag5 || !flag6;
					continue;
				}
				LandOf(item);
				bool stepped;
				string refused;
				float num7 = LevelTile(item, flag6, num4, capped, out stepped, out refused, num5, text4 == "tw_item_sand");
				if (refused != null && text3 == null)
				{
					text3 = refused;
				}
				if (flag6 && num7 >= num5 - 0.01f && WorkHeight(item) < num4 - 0.0315625f)
				{
					flag3 = true;
				}
				if (!(num7 <= 0f))
				{
					Account(host, item, flag6, rock, num7, stepped, text4, toast);
					flag4 = flag4 || flag6;
					flag5 = flag5 || !flag6;
					flag = true;
				}
			}
			if (flag3)
			{
				Say(toast, IsRock(LandOf(t)) ? "Not enough stone to fill the low side." : "Not enough dirt to fill the low side.");
			}
			else if (!flag)
			{
				Say(toast, text3 ?? "Level already.");
			}
			if (!flag)
			{
				return true;
			}
			Swing(feedback, t, at, tool, IsRock(LandOf(t)), flag4 && !flag5, host, scene);
			if (flag2)
			{
				return true;
			}
			Swings++;
			Learn(host);
			return true;
		}

		internal static float LevelTile(WorldTile t, bool fill, float target, bool capped, out bool stepped, out string refused, float most = float.MaxValue, bool sand = false)
		{
			stepped = false;
			refused = null;
			float num = 0f;
			for (int i = 0; i < 8; i++)
			{
				float num2 = WorkHeight(t);
				if (float.IsNaN(num2) || (fill ? (num2 >= target - 0.01578125f) : (num2 <= target + 0.01578125f)))
				{
					break;
				}
				float num3 = Mathf.Min(1f, most - num);
				if (num3 < 0.01f)
				{
					break;
				}
				float num4 = BandThick(t, !fill, sand);
				TileType tileType = LandOf(t);
				Result result = Apply(t, fill, num3, capped, target, sand);
				if (result.Refused != null)
				{
					refused = result.Refused;
					break;
				}
				if (!result.Changed)
				{
					break;
				}
				float num5 = ((!tileType.liquid && !tileType.ocean) ? Mathf.Abs(WorkHeight(t) - num2) : (result.Stepped ? num4 : Mathf.Abs(result.After - result.Before)));
				num += num5 / num4;
				stepped |= result.Stepped;
				if (num5 < 1f / 128f)
				{
					break;
				}
			}
			return num;
		}

		internal static float BandThickness(WorldTile t, bool down, bool sand = false)
		{
			return BandThick(t, down, sand);
		}

		private static float BandThick(WorldTile t, bool down, bool sand = false)
		{
			WorldSampler sampler = ReliefWorld.Sampler;
			if (sampler == null || !sampler.Ready || t == null)
			{
				return 1f;
			}
			int i = t.y * sampler.Cache.Width + t.x;
			TileType tileType = LandOf(t);
			float num = sampler.BandHeight(i, tileType);
			if (down)
			{
				return Mathf.Max(0.12f, num - ((tileType.decrease_to != null) ? sampler.BandHeight(i, tileType.decrease_to) : (num - 0.6f)));
			}
			TileType tileType2 = UpOf(tileType, sand);
			return Mathf.Max(0.12f, ((tileType2 != null) ? sampler.BandHeight(i, tileType2) : (num + 0.6f)) - num);
		}

		internal static TileType UpOf(TileType main, bool sand)
		{
			TileType tileType = main?.increase_to;
			if (tileType == null)
			{
				return null;
			}
			bool flag = main.liquid || main.ocean;
			if (flag && tileType.can_be_filled_with_ocean && !string.IsNullOrEmpty(tileType.fill_to_ocean))
			{
				TileType tileType2 = null;
				try
				{
					tileType2 = AssetManager.tiles.get(tileType.fill_to_ocean);
				}
				catch
				{
				}
				if (tileType2 != null)
				{
					tileType = tileType2;
				}
			}
			if (!sand && tileType.sand && (flag || main.can_be_filled_with_ocean) && tileType.increase_to != null && !tileType.increase_to.sand && !IsRock(tileType.increase_to))
			{
				tileType = tileType.increase_to;
			}
			return tileType;
		}

		private static string WhyNot(Actor host, WorldTile t, string tool, out bool canDig, out bool canFill)
		{
			canDig = (canFill = false);
			TileType tileType = LandOf(t);
			TypeOf(tileType, out var liquid, out var lava);
			if (lava)
			{
				return "Lava";
			}
			try
			{
				if (t.top_type != null && t.top_type.wall)
				{
					return "A wall stands here";
				}
				Building building = t.building;
				if (building != null && building.isAlive() && building.asset != null && building.asset.docks && (tileType.liquid || tileType.ocean))
				{
					ListPool<WorldTile> listPool = building.component_docks?.tiles_ocean;
					if (listPool != null && listPool.Contains(t))
					{
						return "The harbour's berth";
					}
					if (t == building.current_tile)
					{
						return "The harbour stands here";
					}
				}
				else if (building != null && building.isAlive() && building.asset != null && AssetStyle.IsSolid(building))
				{
					return null;
				}
			}
			catch
			{
			}
			if (WorldSync.GuestDigHeld && !MayDig(host, t, out var why))
			{
				return why;
			}
			if (IsRock(tileType) && tool == "tw_item_shovel")
			{
				return "Rock: needs a pickaxe";
			}
			canDig = !liquid;
			canFill = tileType.increase_to != null || !liquid;
			return null;
		}

		private static void TypeOf(TileTypeBase t, out bool liquid, out bool lava)
		{
			liquid = t != null && (t.liquid || t.ocean);
			lava = t?.lava ?? false;
		}

		private static float BaseSwings(string tool, bool rock)
		{
			if (!rock)
			{
				return SoilSwings * ((tool == "tw_item_pickaxe") ? PickInSoil : 1f);
			}
			return RockSwings;
		}

		public static float SwingsFor(Actor host, string tool, bool rock)
		{
			return Mathf.Max(1f, BaseSwings(tool, rock) / Deeper[LevelOf(host)]);
		}

		public static string SwingSound(bool rock, bool fill)
		{
			if (!rock)
			{
				return "event:/SFX/CIVILIZATIONS/BuildRoad";
			}
			return "event:/SFX/HIT/HitStone";
		}

		private static void Swing(CombatFeedback feedback, WorldTile t, Vector2 at, string tool, bool rock, bool fill, Actor host, SceneCollector scene)
		{
			LastFilled = fill;
			feedback?.WorkSwing((tool == "tw_item_shovel") ? FirstPerson.Core.Swing.Dig : FirstPerson.Core.Swing.Mine);
			Sounds.PlayAt(at: new Vector2((float)t.x + 0.5f, (float)t.y + 0.5f), eventPath: SwingSound(rock, fill));
			WorldSampler sampler = ReliefWorld.Sampler;
			float num = sampler?.Cache.SampleHeight(at.x, at.y) ?? 0f;
			if (!fill)
			{
				WorldBoxMod.Instance?.Work.YieldFrom(new Vector3(at.x, at.y, num + 0.1f));
			}
			if (scene != null && sampler != null && host != null)
			{
				uint color = 0u;
				try
				{
					color = sampler.Cache.Colors[t.y * sampler.Cache.Width + t.x];
				}
				catch
				{
				}
				if (rock)
				{
					color = 0xFF000000u | TerrainCache.Rgb(120, 116, 110);
				}
				else if (fill)
				{
					color = 0xFF000000u | ((FillWith(host, t) == "tw_item_sand") ? TerrainCache.Rgb(214, 190, 132) : TerrainCache.Rgb(112, 82, 56));
				}
				scene.Clods(new Vec3(at.x, at.y, num), host.current_position, color, 3 + Size, fill);
			}
		}

		private static string FillWith(Actor host, WorldTile t)
		{
			if (IsRock(LandOf(t)))
			{
				if (Civil.BagCount(host, "stone") <= 0)
				{
					return null;
				}
				return "stone";
			}
			if (Civil.BagCount(host, "tw_item_dirt") > 0)
			{
				return "tw_item_dirt";
			}
			if (Civil.BagCount(host, "tw_item_sand") > 0)
			{
				return "tw_item_sand";
			}
			return null;
		}

		private static void Account(Actor host, WorldTile t, bool fill, bool rock, float share, bool stepped, string need, Action<string> toast)
		{
			float num = PerBand * share;
			if (fill)
			{
				_spend += num;
				while (_spend >= 1f && need != null)
				{
					_spend -= 1f;
					if (!Civil.TakeFromBag(host, need, 1))
					{
						_spend = 0f;
						break;
					}
				}
				return;
			}
			float num2 = 1f + 0.2f * Mastery(host);
			if (rock)
			{
				Give(host, "stone", ref _stone, num * num2, toast);
			}
			else if (LandOf(t).sand)
			{
				Give(host, "tw_item_sand", ref _sand, num * num2, toast);
			}
			else
			{
				Give(host, "tw_item_dirt", ref _dirt, num * num2, toast);
			}
			if (stepped && rock)
			{
				double num3 = Rng.NextDouble();
				string text = ((num3 < (double)MythrilChance) ? "mythril" : ((num3 < (double)(MythrilChance + GoldChance)) ? "gold" : ((num3 < (double)(MythrilChance + GoldChance + IronChance)) ? "common_metals" : null)));
				if (text != null)
				{
					Bag.Put(host, text, 1, spill: true, toast);
				}
			}
		}

		private static void Give(Actor host, string what, ref float acc, float units, Action<string> toast)
		{
			acc += units;
			int num = (int)acc;
			if (num > 0)
			{
				acc -= num;
				Bag.Put(host, what, num, spill: true, toast);
			}
		}

		private static void Say(Action<string> toast, string text)
		{
			if (!(Time.unscaledTime - _toastAt < 2.5f))
			{
				_toastAt = Time.unscaledTime;
				toast?.Invoke(text);
			}
		}

		public static Result Apply(WorldTile t, bool fill, float share, bool capped, float toward = float.NaN, bool sand = false)
		{
			Result result = default(Result);
			WorldSampler sampler = ReliefWorld.Sampler;
			if (sampler == null || !sampler.Ready || t?.main_type == null)
			{
				result.Refused = "The land isn't ready.";
				return result;
			}
			Relief relief = sampler.Relief;
			int width = sampler.Cache.Width;
			int i = t.y * width + t.x;
			float level;
			int main;
			int top;
			bool flag = relief.Lake(i, out level, out main, out top);
			TileType tileType = LandOf(t);
			TypeOf(tileType, out var liquid, out var lava);
			if (lava)
			{
				result.Refused = "Lava";
				return result;
			}
			TileType decrease_to = tileType.decrease_to;
			TileType tileType2 = UpOf(tileType, sand);
			float num = sampler.BandHeight(i, tileType);
			float num2 = ((decrease_to != null) ? sampler.BandHeight(i, decrease_to) : (num - 0.6f));
			float num3 = ((tileType2 != null) ? sampler.BandHeight(i, tileType2) : (num + 0.6f));
			float num4 = Mathf.Max(0.12f, num - num2);
			float num5 = Mathf.Max(0.12f, num3 - num);
			float num6 = (result.Before = relief.EditOf(i));
			if (!fill)
			{
				if (liquid)
				{
					result.Refused = "Under water";
					return result;
				}
				num6 -= num4 * share;
				if (!float.IsNaN(toward))
				{
					num6 = Mathf.Max(num6, Mathf.Min(result.Before, toward - num));
				}
				if (num6 <= 0f - num4 + 0.01578125f)
				{
					if (decrease_to == null)
					{
						num6 = 0f - num4;
						if (result.Before <= 0f - num4 + 0.01578125f)
						{
							result.Refused = "Bedrock: no deeper.";
							return result;
						}
					}
					else if (capped && decrease_to.height_min < 98)
					{
						num6 = 0f - num4 + 1f / 32f;
						if (result.Before <= num6 + 0.01578125f)
						{
							result.Refused = "Too deep to dig by hand.";
							return result;
						}
					}
					else
					{
						float num7 = num + num6;
						if (flag)
						{
							relief.SetLake(i, level, decrease_to.index_id + 1, decrease_to.can_be_biome ? top : 0);
						}
						else if (!Step(t, decrease_to))
						{
							result.Refused = "The ground won't give.";
							return result;
						}
						num6 = num7 - num2;
						result.Stepped = true;
						StepsDown++;
					}
				}
			}
			else
			{
				num6 += num5 * share;
				if (!float.IsNaN(toward))
				{
					num6 = Mathf.Min(num6, Mathf.Max(result.Before, toward - num));
				}
				if (num6 >= num5 - 0.01578125f)
				{
					if (tileType2 == null)
					{
						num6 = num5;
						if (result.Before >= num5 - 0.01578125f)
						{
							result.Refused = "No higher.";
							return result;
						}
					}
					else if (capped && tileType2.height_min >= 210)
					{
						num6 = num5 - 1f / 32f;
						if (result.Before >= num6 - 0.01578125f)
						{
							result.Refused = "Too high to raise by hand.";
							return result;
						}
					}
					else
					{
						float num8 = num + num6;
						if (flag)
						{
							relief.SetLake(i, level, tileType2.index_id + 1, tileType2.can_be_biome ? top : 0);
						}
						else if (!Step(t, tileType2))
						{
							result.Refused = "The ground won't take it.";
							return result;
						}
						num6 = num8 - num3;
						if (tileType2.liquid || tileType2.ocean)
						{
							num6 = 0f;
						}
						result.Stepped = true;
						StepsUp++;
					}
				}
			}
			if (Mathf.Abs(num6) < 1f / 64f)
			{
				num6 = (fill ? (1f / 64f) : (-1f / 64f));
			}
			relief.SetEdit(i, num6);
			result.After = num6;
			result.Changed = true;
			TileType tileType3 = LandOf(t);
			if (!fill && !IsRock(tileType3) && !tileType3.sand)
			{
				relief.SetRaw(i);
			}
			else if (fill && !IsRock(tileType3))
			{
				relief.SetRaw(i, Mathf.Max(relief.RawOf(i), 0.8f));
			}
			return result;
		}

		private static bool Step(WorldTile t, TileType to)
		{
			try
			{
				TopTileType top_type = t.top_type;
				TopTileType pTopType = null;
				if (to.can_be_biome)
				{
					BiomeAsset biomeAsset = ((top_type != null && top_type.is_biome) ? top_type.biome_asset : null);
					if (biomeAsset == null)
					{
						biomeAsset = BiomeAround(t);
					}
					if (biomeAsset != null)
					{
						string text = ((to.rank_type == TileRank.Low) ? biomeAsset.tile_low : biomeAsset.tile_high);
						if (!string.IsNullOrEmpty(text))
						{
							pTopType = AssetManager.top_tiles.get(text);
						}
					}
				}
				Building building = t.building;
				if (building != null && building.asset != null && building.asset.docks && !to.liquid && !to.ocean)
				{
					building.tiles?.Remove(t);
					t.building = null;
				}
				MapAction.terraformTile(t, to, pTopType, TerraformLibrary.flash);
				return t.main_type == to;
			}
			catch (Exception ex)
			{
				Log.Error("earthworks: stepping a tile: " + ex.Message);
				return false;
			}
		}

		private static BiomeAsset BiomeAround(WorldTile t)
		{
			BiomeAsset result = null;
			int num = 0;
			WorldTile[] neighboursAll = t.neighboursAll;
			if (neighboursAll == null)
			{
				return null;
			}
			for (int i = 0; i < neighboursAll.Length; i++)
			{
				BiomeAsset biomeAsset = ((neighboursAll[i]?.top_type != null && neighboursAll[i].top_type.is_biome) ? neighboursAll[i].top_type.biome_asset : null);
				if (biomeAsset == null)
				{
					continue;
				}
				int num2 = 0;
				for (int j = 0; j < neighboursAll.Length; j++)
				{
					if (neighboursAll[j]?.top_type != null && neighboursAll[j].top_type.biome_asset == biomeAsset)
					{
						num2++;
					}
				}
				if (num2 > num)
				{
					num = num2;
					result = biomeAsset;
				}
			}
			return result;
		}

		public static float WorkHeight(WorldTile t)
		{
			WorldSampler sampler = ReliefWorld.Sampler;
			if (sampler == null || !sampler.Ready || t == null)
			{
				return float.NaN;
			}
			int i = t.y * sampler.Cache.Width + t.x;
			TileType tileType = LandOf(t);
			if (tileType != null)
			{
				return sampler.BandHeight(i, tileType) + sampler.Relief.EditOf(i);
			}
			return float.NaN;
		}

		public static float EditWithinBand(WorldTile t, float target)
		{
			WorldSampler sampler = ReliefWorld.Sampler;
			if (sampler == null || !sampler.Ready || t?.main_type == null)
			{
				return float.NaN;
			}
			TileType tileType = LandOf(t);
			if (tileType == null || tileType.liquid || tileType.ocean || tileType.lava || t.main_type.liquid)
			{
				return float.NaN;
			}
			int i = t.y * sampler.Cache.Width + t.x;
			float num = sampler.BandHeight(i, tileType);
			float num2 = ((tileType.decrease_to != null) ? sampler.BandHeight(i, tileType.decrease_to) : (num - 0.6f));
			float num3 = ((tileType.increase_to != null) ? sampler.BandHeight(i, tileType.increase_to) : (num + 0.6f));
			float a = 0f - Mathf.Max(0.12f, num - num2) + 1f / 32f;
			float a2 = Mathf.Max(0.12f, num3 - num) - 1f / 32f;
			float num4 = Mathf.Clamp(target - num, Mathf.Min(a, 0f), Mathf.Max(a2, 0f));
			if (!(Mathf.Abs(num4) < 1f / 64f))
			{
				return num4;
			}
			return 0f;
		}

		public static float BandsDown(WorldTile t, int bands)
		{
			WorldSampler sampler = ReliefWorld.Sampler;
			if (sampler == null || !sampler.Ready || t == null)
			{
				return float.NaN;
			}
			int i = t.y * sampler.Cache.Width + t.x;
			TileType tileType = LandOf(t);
			float result = WorkHeight(t);
			for (int j = 0; j < bands; j++)
			{
				if (tileType == null)
				{
					break;
				}
				if (tileType.decrease_to == null)
				{
					return sampler.BandHeight(i, tileType) - 0.6f;
				}
				tileType = tileType.decrease_to;
				result = sampler.BandHeight(i, tileType);
			}
			return result;
		}

		public static float BandUp(WorldTile t)
		{
			WorldSampler sampler = ReliefWorld.Sampler;
			if (sampler == null || !sampler.Ready || t == null)
			{
				return float.NaN;
			}
			int i = t.y * sampler.Cache.Width + t.x;
			TileType tileType = LandOf(t);
			if (tileType == null)
			{
				return float.NaN;
			}
			if (tileType.increase_to == null)
			{
				return sampler.BandHeight(i, tileType) + 0.6f;
			}
			return sampler.BandHeight(i, tileType.increase_to);
		}

		private static float BandProgress(WorldSampler s, WorldTile t, int i, bool sand)
		{
			float num = s.Relief.EditOf(i);
			if (Mathf.Abs(num) <= 3f / 128f)
			{
				return 0f;
			}
			TileType tileType = LandOf(t);
			TileType decrease_to = tileType.decrease_to;
			TileType tileType2 = UpOf(tileType, sand);
			float num2 = s.BandHeight(i, tileType);
			if (num < 0f)
			{
				float num3 = Mathf.Max(0.12f, num2 - ((decrease_to != null) ? s.BandHeight(i, decrease_to) : (num2 - 0.6f)));
				return Mathf.Clamp01((0f - num) / num3);
			}
			float num4 = Mathf.Max(0.12f, ((tileType2 != null) ? s.BandHeight(i, tileType2) : (num2 + 0.6f)) - num2);
			return Mathf.Clamp01(num / num4);
		}

		public static float DepthBelowAround(TerrainCache c, int x, int y)
		{
			if (c == null || c.Width == 0)
			{
				return 0f;
			}
			float num = c.Ground[y * c.Width + x];
			float num2 = 0f;
			int num3 = 0;
			for (int i = -2; i <= 2; i++)
			{
				for (int j = -2; j <= 2; j++)
				{
					if (Math.Max(Math.Abs(j), Math.Abs(i)) == 2)
					{
						int num4 = x + j;
						int num5 = y + i;
						if (num4 >= 0 && num5 >= 0 && num4 < c.Width && num5 < c.Height)
						{
							num2 += c.Ground[num5 * c.Width + num4];
							num3++;
						}
					}
				}
			}
			if (num3 <= 0)
			{
				return 0f;
			}
			return num2 / (float)num3 - num;
		}

		public static float Mastery(Actor a)
		{
			return Mathf.Clamp01(Xp(a) / 3000f);
		}

		private static float Xp(Actor a)
		{
			if (a?.data == null)
			{
				return 0f;
			}
			string pResult = null;
			try
			{
				a.data.get("fp_skills", out pResult, null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(pResult))
			{
				return 0f;
			}
			string[] array = pResult.Split('|');
			foreach (string text in array)
			{
				if (text.StartsWith("earth:", StringComparison.Ordinal) && float.TryParse(text.Substring(6), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
				{
					return result;
				}
			}
			return 0f;
		}

		private static void Learn(Actor a)
		{
			if (a?.data == null)
			{
				return;
			}
			try
			{
				a.data.get("fp_skills", out var pResult, null);
				float result = 0f;
				StringBuilder stringBuilder = new StringBuilder();
				if (!string.IsNullOrEmpty(pResult))
				{
					string[] array = pResult.Split('|');
					foreach (string text in array)
					{
						if (text.Length == 0)
						{
							continue;
						}
						if (text.StartsWith("earth:", StringComparison.Ordinal))
						{
							float.TryParse(text.Substring(6), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
							continue;
						}
						if (stringBuilder.Length > 0)
						{
							stringBuilder.Append('|');
						}
						stringBuilder.Append(text);
					}
				}
				if (!(result >= 3000f))
				{
					result += 1f;
					if (stringBuilder.Length > 0)
					{
						stringBuilder.Append('|');
					}
					stringBuilder.Append("earth:").Append(((int)result).ToString(CultureInfo.InvariantCulture));
					a.data.set("fp_skills", stringBuilder.ToString());
				}
			}
			catch
			{
			}
		}
	}
}
