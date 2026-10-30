using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class HallFlagsTest : CivilTest
	{
		private sealed class Hall
		{
			public string Name;

			public string Race;

			public Building B;

			public Kingdom K;

			public Actor King;

			public long Id;
		}

		private sealed class Tick
		{
			public float T;

			public float Clock;

			public float H;

			public float Z;

			public int Phase;

			public int Style;

			public long Shown;

			public string Banner;
		}

		private sealed class Kept
		{
			public Hall H;

			public long Owner;

			public bool Torn;

			public bool Abandoned;
		}

		private static readonly string[] Parts = new string[5] { "wave", "capture", "torn", "god", "upgrade" };

		private readonly List<Hall> _halls = new List<Hall>();

		private Hall _h1;

		private Hall _h2;

		private Hall _h3;

		private Hall _elf;

		private Hall _orc;

		private Hall _dwarf;

		private Hall _cat;

		private Actor _you;

		private bool _savedGod;

		private bool _away;

		private float _savedGodZoom;

		private readonly float[] _wave = new float[4];

		private static FieldInfo _sceneFrame;

		private static readonly string[] Hardware = new string[6] { "hf_", "rg_hallpole", "rg_finial", "rg_bannerrod", "rg_bannerbracket", "rg_standard" };

		private readonly List<Kept> _kept = new List<Kept>();

		protected override string Prefix => "HF";

		private float Clock => Mod.SceneForTest.HallFlagClockForTest;

		protected override IEnumerator Body()
		{
			int num = 0;
			string[] parts = Parts;
			foreach (string text in parts)
			{
				if (Options.Contains("only" + text))
				{
					num++;
				}
			}
			bool all = num == 0 || num == Parts.Length;
			_savedGod = Mod.Settings.GodView;
			_savedGodZoom = Mod.Settings.GodViewZoom;
			yield return Part("setup", Setup());
			if (_halls.Count != 0)
			{
				yield return Part("flags", Flags());
				if (all || Options.Contains("onlywave"))
				{
					yield return Part("wave", Wave());
				}
				if (all || Options.Contains("onlygod"))
				{
					yield return Part("god", God());
				}
				if (all || Options.Contains("onlycapture"))
				{
					yield return Part("capture", Capture());
				}
				if (all || Options.Contains("onlyupgrade"))
				{
					yield return Part("upgrade", Upgrade());
				}
				if (all || Options.Contains("onlytorn"))
				{
					yield return Part("torn", Torn());
				}
				if (all || Options.Contains("onlycache"))
				{
					yield return Part("cache", Cache());
				}
				Restore();
				Note($"models built {HallFlagModels.Built}, cached {HallFlagModels.Cached}, build ms {HallFlagModels.BuildMs:F1}, stand-ins {HallFlagModels.StandInsBuilt}, " + $"written {HallFlagLook.Written}, written while loading {HallFlagLook.WrittenWhileLoading}, spots {FlagSpots.Done}");
			}
		}

		private void Restore()
		{
			SceneCollector.HallFlagsOn = true;
			if (Mod?.SceneForTest != null)
			{
				Mod.SceneForTest.Gusts.Force = -1f;
			}
			Mod.Settings.GodView = _savedGod;
			Mod.Settings.GodViewZoom = _savedGodZoom;
			Config.paused = false;
		}

		private IEnumerator Setup()
		{
			string[] array = new string[7] { "human", "human", "human", "elf", "orc", "dwarf", "civ_cat" };
			Actor first = null;
			List<Actor> home = null;
			int humans = 0;
			string[] array2 = array;
			foreach (string race in array2)
			{
				Actor king = ((first == null) ? Village(race, 8, 0) : (Village(race, 7, 0, first.current_tile, 26f) ?? Village(race, 7, 0)));
				if (!Check("a " + race + " village", king != null))
				{
					continue;
				}
				if (first == null)
				{
					first = king;
					home = new List<Actor>(Founded);
				}
				yield return CivilTest.Wait(0.8f);
				Building building = HallFor(king, race);
				string text;
				if (!(race == "human"))
				{
					text = race;
				}
				else
				{
					int num = humans + 1;
					humans = num;
					text = "human_k" + num;
				}
				string text2 = text;
				if (!Check(text2 + ": the village has a hall", building != null, building?.asset?.id))
				{
					continue;
				}
				Hall hall = new Hall
				{
					Name = text2,
					Race = race,
					B = building,
					K = king.kingdom,
					King = king,
					Id = building.data.id
				};
				_halls.Add(hall);
				switch (text2)
				{
				case "human_k1":
					_h1 = hall;
					continue;
				case "human_k2":
					_h2 = hall;
					continue;
				case "human_k3":
					_h3 = hall;
					continue;
				}
				switch (race)
				{
				case "elf":
					_elf = hall;
					break;
				case "orc":
					_orc = hall;
					break;
				case "dwarf":
					_dwarf = hall;
					break;
				default:
					_cat = hall;
					break;
				}
			}
			if (first != null)
			{
				_you = home.Find((Actor a) => a != null && a.isAlive() && a.isAdult() && !a.isKing() && !a.isWarrior()) ?? home.Find((Actor a) => a != null && a.isAlive() && !a.isKing()) ?? first;
				try
				{
					((BaseSimObject)_you).addStatusEffect("invincible", 900f, pColorEffect: false);
				}
				catch
				{
				}
				yield return Possess(_you);
				Check("first person", InControl(_you));
				yield return CivilTest.Wait(3f);
			}
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
				if (AssetManager.buildings.get(id) == null)
				{
					return null;
				}
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

		private Building HallFor(Actor king, string race)
		{
			City city = king.city;
			Building building = ((city != null) ? HallOf(city) : null);
			if (building == null && city != null)
			{
				string[] array = new string[2]
				{
					"hall_" + race + "_0",
					"hall_" + race.Replace("civ_", "") + "_0"
				};
				foreach (string id in array)
				{
					if ((building = AddHall(id, city)) != null)
					{
						break;
					}
				}
			}
			try
			{
				if (building != null && building.isUnderConstruction())
				{
					building.completeConstruction();
				}
			}
			catch
			{
			}
			return building;
		}

		private SceneCollector.HallFlagState State(Hall h)
		{
			if (h?.B != null)
			{
				return Mod.SceneForTest.HallFlagForTest(h.B);
			}
			return null;
		}

		private static int StoredStyle(Kingdom k)
		{
			try
			{
				k.data.get("fp_style", out var pResult, -1);
				return (pResult >= 0 && pResult <= 2) ? pResult : (-1);
			}
			catch
			{
				return -1;
			}
		}

		private static uint Pack(Color c)
		{
			return TerrainCache.Rgb(Mathf.Clamp((int)(c.r * 255f + 0.5f), 0, 255), Mathf.Clamp((int)(c.g * 255f + 0.5f), 0, 255), Mathf.Clamp((int)(c.b * 255f + 0.5f), 0, 255));
		}

		private static float Len(Vec3 v)
		{
			return Mathf.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
		}

		private static float Det(Mat3 m)
		{
			return m.C0.X * (m.C1.Y * m.C2.Z - m.C1.Z * m.C2.Y) - m.C1.X * (m.C0.Y * m.C2.Z - m.C0.Z * m.C2.Y) + m.C2.X * (m.C0.Y * m.C1.Z - m.C0.Z * m.C1.Y);
		}

		private static float Angle(Mat3 r)
		{
			return Mathf.Atan2(r.C0.Y, r.C0.X);
		}

		private static float Bend(MeshInstance cloth)
		{
			if (cloth?.PartRotation == null)
			{
				return float.NaN;
			}
			float num = 0f;
			for (int i = 1; i < cloth.PartRotation.Length; i++)
			{
				num += Angle(cloth.PartRotation[i]);
			}
			return num;
		}

		private static float AngleDiff(float a, float b)
		{
			return Mathf.Abs(Mathf.Repeat(a - b + MathF.PI, MathF.PI * 2f) - MathF.PI);
		}

		private static void Hsv(uint c, out float s, out float v)
		{
			float a = (float)(c & 0xFF) / 255f;
			float a2 = (float)((c >> 8) & 0xFF) / 255f;
			float b = (float)((c >> 16) & 0xFF) / 255f;
			float num = Mathf.Max(a, Mathf.Max(a2, b));
			float num2 = Mathf.Min(a, Mathf.Min(a2, b));
			v = num;
			s = ((num > 0.0001f) ? ((num - num2) / num) : 0f);
		}

		private static string Hex(uint c)
		{
			return (((c & 0xFF) << 16) | (((c >> 8) & 0xFF) << 8) | ((c >> 16) & 0xFF)).ToString("x6");
		}

		private static string FrontName(int f)
		{
			switch (f)
			{
			default:
				return "none";
			case 4:
				return "from a standard";
			case 3:
				return "under a bracket";
			case 2:
				return "on its wall";
			case 1:
				return "on its wall (anchor)";
			}
		}

		private int SceneFrame()
		{
			try
			{
				if (_sceneFrame == null)
				{
					_sceneFrame = typeof(SceneCollector).GetField("_frame", BindingFlags.Instance | BindingFlags.NonPublic);
				}
				return (int)_sceneFrame.GetValue(Mod.SceneForTest);
			}
			catch
			{
				return Time.frameCount;
			}
		}

		private static long RememberedId(Building b)
		{
			string text = Remembered(b);
			if (string.IsNullOrEmpty(text))
			{
				return 0L;
			}
			string[] array = text.Split('|');
			if (array.Length <= 1 || !long.TryParse(array[1], out var result))
			{
				return 0L;
			}
			return result;
		}

		private static void Clear(int x0, int y0, int x1, int y1)
		{
			for (int i = y0; i <= y1; i++)
			{
				for (int j = x0; j <= x1; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile == null || tile.building == null || tile.building.asset == null)
					{
						continue;
					}
					BuildingType building_type = tile.building.asset.building_type;
					if (building_type == BuildingType.Building_Tree || building_type == BuildingType.Building_Mineral || building_type == BuildingType.Building_Plant || building_type == BuildingType.Building_Fruits || building_type == BuildingType.Building_Nature || building_type == BuildingType.Building_Hives || building_type == BuildingType.Building_Poops)
					{
						try
						{
							tile.building.removeBuildingFinal();
						}
						catch
						{
						}
					}
				}
			}
		}

		private static bool IsFlagPiece(MeshInstance m)
		{
			string text = m?.Model?.Name;
			if (text == null)
			{
				return false;
			}
			string[] hardware = Hardware;
			foreach (string value in hardware)
			{
				if (text.StartsWith(value, StringComparison.Ordinal))
				{
					return true;
				}
			}
			return false;
		}

		private MeshInstance HallByTag(int tag)
		{
			if (tag < 0)
			{
				return null;
			}
			List<MeshInstance> meshes = Mod.SceneForTest.Meshes;
			for (int i = 0; i < meshes.Count; i++)
			{
				MeshInstance meshInstance = meshes[i];
				if (meshInstance != null && meshInstance.Tag == tag && meshInstance.Model != null && !IsFlagPiece(meshInstance) && meshInstance.Model.Name != null && (meshInstance.Model.Name.Contains("hall") || meshInstance.Model.Name.Contains("keep")))
				{
					return meshInstance;
				}
			}
			return null;
		}

		private int FlagPieces()
		{
			int num = 0;
			List<MeshInstance> meshes = Mod.SceneForTest.Meshes;
			for (int i = 0; i < meshes.Count; i++)
			{
				if (meshes[i] != null && IsFlagPiece(meshes[i]))
				{
					num++;
				}
			}
			return num;
		}

		private float SizeOf(Hall h, SceneCollector.HallFlagState st)
		{
			if (st?.Shaft == null || st.Cloth == null)
			{
				return float.NaN;
			}
			MeshInstance meshInstance = HallByTag(st.Cloth.Tag);
			MeshModel meshModel = Models.Get(Models.BuildingFamily(h.B));
			if (meshInstance == null || meshModel == null || !FlagSpots.TryGet(meshModel, out var spot) || spot.Rig <= 0f)
			{
				return float.NaN;
			}
			float num = Len(meshInstance.Basis.C1);
			if (!(num > 1E-05f))
			{
				return float.NaN;
			}
			return Len(st.Shaft.Basis.C0) / (num * spot.Rig);
		}

		private string Short(SceneCollector.HallFlagState st)
		{
			if (st == null)
			{
				return "no flag state";
			}
			return $"#{st.Shown.KingdomId} style {st.Shown.Style} cloth {st.ClothModel?.Name} pole {st.Shaft?.Model?.Name} finial {st.Finial?.Model?.Name} " + string.Format("banner {0} front {1} phase {2} h {3:F2}", (st.Banner == null) ? "-" : st.BannerModel?.Name, st.Front, st.SwapPhase, st.Height01);
		}

		private string Describe(Hall h)
		{
			SceneCollector.HallFlagState hallFlagState = State(h);
			if (hallFlagState == null)
			{
				return "no flag state";
			}
			string text = "?";
			try
			{
				text = ((h.B.kingdom != null) ? (h.B.kingdom.name + " #" + h.B.kingdom.data.id) : "none");
			}
			catch
			{
			}
			return $"{h.B.asset?.id} ({Models.BuildingFamily(h.B)}) of {text}: shown #{hallFlagState.Shown.KingdomId} set {hallFlagState.Shown.Set} bg {hallFlagState.Shown.Bg} icon {hallFlagState.Shown.Icon} style {hallFlagState.Shown.Style} serial {hallFlagState.Shown.StyleSerial} " + string.Format("greys {0}/{1}/{2}{3} pole {4} finial {5} cloth {6} ", hallFlagState.Shown.GreyField, hallFlagState.Shown.GreyDark, hallFlagState.Shown.GreyEmblem, hallFlagState.Shown.GreysKnown ? "" : "?", hallFlagState.Shaft?.Model?.Name, hallFlagState.Finial?.Model?.Name, hallFlagState.ClothModel?.Name) + string.Format("banner {0} front {1} phase {2} h {3:F2} foot ({4:F1},{5:F1},{6:F1}) ", (hallFlagState.Banner == null) ? "-" : hallFlagState.BannerModel?.Name, hallFlagState.Front, hallFlagState.SwapPhase, hallFlagState.Height01, hallFlagState.FootWorld.X, hallFlagState.FootWorld.Y, hallFlagState.FootWorld.Z) + string.Format("pal {0} multiply {1:X8}", (hallFlagState.Cloth?.Palette != null && hallFlagState.Cloth.Palette.Length > 2) ? (Hex(hallFlagState.Cloth.Palette[0]) + "/" + Hex(hallFlagState.Cloth.Palette[1]) + "/" + Hex(hallFlagState.Cloth.Palette[2])) : "-", hallFlagState.Cloth?.Multiply ?? 0);
		}

		private void SetEye(Building hall, float back, float side, bool banner)
		{
			if (Mod.Interior.Inside)
			{
				Note("out of the room " + (Mod.Interior.House?.asset?.id ?? "?"));
				Mod.LeaveRoomForTest();
			}
			WorldTile current_tile = hall.current_tile;
			Vector2 vector = new Vector2((float)current_tile.x + 0.5f, (float)current_tile.y + 1.5f);
			SceneCollector.HallFlagState hallFlagState = Mod.SceneForTest.HallFlagForTest(hall);
			if (!banner && hallFlagState?.Cloth != null && hallFlagState.Shaft != null && hallFlagState.Finial == null)
			{
				Vec3 position = hallFlagState.Cloth.Position;
				Vector2 debugEye = new Vector2(position.X + side * back, position.Y - back);
				float num = ((Mod.TerrainForTest != null) ? Mod.TerrainForTest.SampleHeight(debugEye.x, debugEye.y) : 0f);
				Mod.DebugEye = debugEye;
				Mod.DebugEyeSet = true;
				Mod.DebugEyeHeight = Mathf.Max(1.6f, (position.Z - num) * 0.7f);
				Mod.SetYaw(Mathf.Atan2(position.Y - debugEye.y, position.X - debugEye.x));
				Mod.SetPitchDegrees(Mathf.Atan2(position.Z - 0.5f - (num + Mod.DebugEyeHeight), back * Mathf.Sqrt(1f + side * side)) * 57.29578f);
				return;
			}
			float num2 = ((Mod.TerrainForTest != null) ? Mod.TerrainForTest.SampleHeight(vector.x, vector.y) : 0f);
			float num3 = ((hallFlagState != null && hallFlagState.FootWorld.Z > 0f) ? (hallFlagState.FootWorld.Z + 3.5f - num2) : 8f);
			if (banner && hallFlagState != null && hallFlagState.Stand != null)
			{
				num2 = hallFlagState.Stand.Position.Z;
			}
			float num4 = ((banner && hallFlagState != null && hallFlagState.Stand != null) ? 1.6f : ((banner && hallFlagState != null && hallFlagState.Banner != null) ? (hallFlagState.Banner.Position.Z - num2 - 0.6f) : (num3 * 0.62f)));
			float num5 = (banner ? back : Mathf.Max(back, num3 * 1.25f));
			if (banner && hallFlagState != null && hallFlagState.Stand != null)
			{
				vector = new Vector2(hallFlagState.Stand.Position.X, hallFlagState.Stand.Position.Y + 1f);
			}
			else if (banner && hallFlagState != null && hallFlagState.Banner != null)
			{
				vector = new Vector2(hallFlagState.Banner.Position.X, hallFlagState.Banner.Position.Y + 1f);
			}
			Vector2 debugEye2 = new Vector2(vector.x + side * num5, vector.y - num5);
			Mod.DebugEye = debugEye2;
			Mod.DebugEyeSet = true;
			Mod.DebugEyeHeight = (banner ? Mathf.Max(1.6f, num4 * 0.8f) : Mathf.Max(2f, num3 * 0.42f));
			Mod.SetYaw(Mathf.Atan2(vector.y - debugEye2.y, vector.x - debugEye2.x));
			Mod.SetPitchDegrees(Mathf.Atan2(num4 - Mod.DebugEyeHeight, Mathf.Sqrt(num5 * num5 * (1f + side * side))) * 57.29578f);
		}

		private IEnumerator Look(Building hall, float back, float side, bool banner)
		{
			WorldTile current_tile = hall.current_tile;
			Clear(current_tile.x - 13, current_tile.y - 20, current_tile.x + 13, current_tile.y - 2);
			Clear(current_tile.x - 8, current_tile.y - 2, current_tile.x + 8, current_tile.y + 5);
			SetEye(hall, back, side, banner);
			yield return CivilTest.Wait(0.6f);
			SetEye(hall, back, side, banner);
			yield return CivilTest.Wait(1.4f);
		}

		private IEnumerator LookAway(Hall h)
		{
			_away = false;
			Vector2 p = new Vector2((float)h.B.current_tile.x + 0.5f, (float)h.B.current_tile.y + 0.5f);
			Hall far = null;
			float best = 0f;
			foreach (Hall hall in _halls)
			{
				if (hall == h || hall.B == null)
				{
					continue;
				}
				try
				{
					if (!hall.B.isAlive() || hall.B.isRuin())
					{
						continue;
					}
					goto IL_00dd;
				}
				catch
				{
				}
				continue;
				IL_00dd:
				float num = Vector2.Distance(p, new Vector2((float)hall.B.current_tile.x + 0.5f, (float)hall.B.current_tile.y + 0.5f));
				if (num > best)
				{
					best = num;
					far = hall;
				}
			}
			Vector2 eye;
			Vector2 vector3;
			if (far != null && best >= 45f)
			{
				Vector2 vector = new Vector2((float)far.B.current_tile.x + 0.5f, (float)far.B.current_tile.y + 0.5f);
				Vector2 normalized = (vector - p).normalized;
				Vector2 vector2 = new Vector2(0f - normalized.y, normalized.x);
				eye = vector - vector2 * 11f;
				vector3 = vector2;
			}
			else
			{
				vector3 = (new Vector2((p.x < (float)MapBox.width * 0.5f) ? (MapBox.width - 8) : 8, (p.y < (float)MapBox.height * 0.5f) ? (MapBox.height - 8) : 8) - p).normalized;
				eye = p + vector3 * 80f;
			}
			Mod.DebugEye = eye;
			Mod.DebugEyeSet = true;
			Mod.DebugEyeHeight = 3f;
			Mod.SetYaw(Mathf.Atan2(vector3.y, vector3.x));
			Mod.SetPitchDegrees(-6f);
			yield return CivilTest.Wait(0.5f);
			int last = State(h)?.Frame ?? (SceneFrame() - 1000);
			int num2 = 0;
			float waited = 0f;
			while (waited < 120f)
			{
				yield return null;
				waited += Time.unscaledDeltaTime;
				SceneCollector.HallFlagState hallFlagState = State(h);
				if (hallFlagState != null && hallFlagState.Frame != last)
				{
					last = hallFlagState.Frame;
				}
				num2 = SceneFrame() - last;
				if (num2 > 135 && waited > 1f)
				{
					break;
				}
			}
			_away = num2 > 135;
			Note(string.Format("looking away from {0}: {1:F0} tiles off, {2}, undrawn {3} frames, {4:F1} s", h.Name, Vector2.Distance(p, eye), (far != null && best >= 45f) ? ("beside " + far.Name) : "toward a corner", num2, waited));
		}

		private void HoldCam(Vector2 at, float zoom)
		{
			try
			{
				Camera camera = World.world.camera;
				camera.transform.position = new Vector3(at.x, at.y, camera.transform.position.z);
				camera.orthographicSize = zoom;
				World.world.move_camera?.setTargetZoom(zoom);
				World.world.setZoomOrthographic(zoom);
			}
			catch
			{
			}
		}

		private IEnumerator Hold(Vector2 at, float zoom, float seconds)
		{
			for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
			{
				HoldCam(at, zoom);
				yield return null;
			}
		}

		private IEnumerator InBody()
		{
			if (_you == null || !_you.isAlive())
			{
				_you = null;
				try
				{
					foreach (Actor unit in World.world.units)
					{
						if (unit != null && unit.isAlive() && unit.isAdult() && !unit.isKing() && unit.kingdom != null && unit.kingdom.isCiv())
						{
							_you = unit;
							break;
						}
					}
				}
				catch
				{
				}
				try
				{
					((BaseSimObject)_you)?.addStatusEffect("invincible", 900f, pColorEffect: false);
				}
				catch
				{
				}
			}
			if (_you != null && !InControl(_you))
			{
				yield return Possess(_you);
			}
		}

		private IEnumerator Flags()
		{
			if (_h3 != null)
			{
				yield return Look(_h3.B, 9f, 0.35f, banner: false);
				try
				{
					_h3.K.data.removeInt("fp_style");
				}
				catch (Exception ex)
				{
					Note("style: " + ex.Message);
				}
				Regalia.ResetForTest();
				bool swapped = false;
				bool neutral = false;
				for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
				{
					yield return null;
					SceneCollector.HallFlagState hallFlagState = State(_h3);
					if (hallFlagState != null)
					{
						if (hallFlagState.SwapPhase != 0)
						{
							swapped = true;
						}
						if (hallFlagState.Shown.Style < 0)
						{
							neutral = true;
						}
					}
				}
				int num = StoredStyle(_h3.K);
				SceneCollector.HallFlagState hallFlagState2 = State(_h3);
				Check("a first style comes in place, no lowering (the sweep's assignment)", num >= 0 && !swapped && hallFlagState2 != null && hallFlagState2.Shown.Style == num, $"style {num}, neutral meanwhile {neutral}, swapped {swapped}, shown {hallFlagState2?.Shown.Style}");
			}
			Hall[] array = new Hall[3] { _h1, _h2, _h3 };
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] != null)
				{
					try
					{
						array[i].K.data.set("fp_style", i);
					}
					catch (Exception ex2)
					{
						Note("style: " + ex2.Message);
					}
				}
			}
			Regalia.ResetForTest();
			yield return CivilTest.Wait(0.5f);
			foreach (Hall hall in _halls)
			{
				yield return Inspect(hall);
			}
			if (_h1 != null)
			{
				yield return HallUntouched(_h1);
			}
			if (_h3 != null)
			{
				yield return Recolour(_h3);
			}
			Note($"models built {HallFlagModels.Built}, cached {HallFlagModels.Cached}, build ms {HallFlagModels.BuildMs:F1}, flags drawn {SceneCollector.HallFlagsDrawn} in {SceneCollector.HallFlagsMs:F2} ms, written {HallFlagLook.Written}");
		}

		private IEnumerator Inspect(Hall h)
		{
			yield return Look(h.B, 9f, 0.35f, banner: false);
			SceneCollector.HallFlagState hallFlagState = State(h);
			Note(h.Name + ": " + Describe(h));
			if (!Check(h.Name + ": its flag flies (pole, finial, cloth)", hallFlagState != null && hallFlagState.Shaft != null && hallFlagState.Finial != null && hallFlagState.Cloth != null && hallFlagState.ClothModel != null, Short(hallFlagState)))
			{
				yield break;
			}
			Kingdom kingdom = h.B.kingdom;
			string set = null;
			long num = 0L;
			int bg = -1;
			int icon = -1;
			uint main2 = 0u;
			uint emb = 0u;
			try
			{
				set = kingdom.getActorAsset()?.banner_id;
				num = kingdom.data.id;
				bg = kingdom.data.banner_background_id;
				icon = kingdom.data.banner_icon_id;
				ColorAsset color = kingdom.getColor();
				main2 = Pack(color.getColorMainSecond());
				emb = Pack(color.getColorBanner());
			}
			catch (Exception ex)
			{
				Note("realm: " + ex.Message);
			}
			FlagLook shown = hallFlagState.Shown;
			Check($"{h.Name}: its realm's emblem (banner {set}, background {bg}, icon {icon})", shown.Known && shown.KingdomId == num && shown.Set == set && shown.Bg == bg && shown.Icon == icon, $"shown #{shown.KingdomId} {shown.Set} {shown.Bg} {shown.Icon}, realm #{num}");
			int num2 = ((h.Race == "human") ? StoredStyle(kingdom) : (-1));
			FlagShape s = FlagCloth.ShapeOf(num2);
			string value = "hf_cloth_" + set + "_" + icon + "_" + FlagCloth.ShapeName(s) + "_";
			object obj;
			switch (num2)
			{
			default:
				obj = "n";
				break;
			case 2:
				obj = "c";
				break;
			case 1:
				obj = "b";
				break;
			case 0:
				obj = "a";
				break;
			}
			string text = "rg_finial_" + (string)obj;
			string value2 = ((num2 == 0 || num2 == 2) ? "rg_hallpole_metal" : "rg_hallpole_wood");
			string text2 = ((num2 < 0) ? "neutral" : ("style " + (char)(65 + num2)));
			Check(h.Name + ": shape, pole and finial " + text2 + " (" + FlagCloth.ShapeName(s) + ", " + text + ")", shown.Style == num2 && hallFlagState.ClothModel.Name.StartsWith(value, StringComparison.Ordinal) && hallFlagState.Finial.Model.Name.StartsWith(text, StringComparison.Ordinal) && hallFlagState.Shaft.Model.Name.StartsWith(value2, StringComparison.Ordinal), hallFlagState.ClothModel.Name + ", " + hallFlagState.Finial.Model.Name + ", " + hallFlagState.Shaft.Model.Name);
			Check(h.Name + ": Blender's pole and finial (no stand-ins)", !hallFlagState.Shaft.Model.Name.Contains("~standin") && !hallFlagState.Finial.Model.Name.Contains("~standin"), hallFlagState.Shaft.Model.Name + ", " + hallFlagState.Finial.Model.Name);
			uint[] palette = hallFlagState.Cloth.Palette;
			uint num3 = TerrainCache.Scale(main2, (float)(int)shown.GreyField / 255f);
			uint num4 = TerrainCache.Scale(main2, (float)(int)shown.GreyDark / 255f);
			uint num5 = TerrainCache.Scale(emb, (float)(int)shown.GreyEmblem / 255f);
			Check(h.Name + ": the cloth in its realm's colours (main 2 and banner, times the sprite's greys)", palette != null && palette.Length > 2 && palette[0] == num3 && palette[1] == num4 && palette[2] == num5 && shown.Main2 == main2 && shown.Banner == emb, string.Format("{0} want {1}/{2}/{3}, greys {4}/{5}/{6}{7}", (palette != null && palette.Length > 2) ? (Hex(palette[0]) + "/" + Hex(palette[1]) + "/" + Hex(palette[2])) : "-", Hex(num3), Hex(num4), Hex(num5), shown.GreyField, shown.GreyDark, shown.GreyEmblem, shown.GreysKnown ? "" : " (defaults)"));
			Check(h.Name + ": pole and finial Static, the waving cloth not", hallFlagState.Shaft.Static && hallFlagState.Finial.Static && hallFlagState.Cloth.Static == (hallFlagState.Cloth.Model.Parts.Length == 1), $"pole {hallFlagState.Shaft.Static}, finial {hallFlagState.Finial.Static}, cloth {hallFlagState.Cloth.Static} ({hallFlagState.Cloth.Model.Parts.Length} parts)");
			bool flag = TownTier.IsCapital(h.B.city);
			float num6 = SizeOf(h, hallFlagState);
			Check(h.Name + ": the flag sized to its hall (" + (flag ? "a capital's: 1.25x" : "a town's: 1x") + ")", !float.IsNaN(num6) && Mathf.Abs(num6 - (flag ? 1.25f : 1f)) < 0.02f, num6.ToString("F3"));
			Mod.SaveFrame("hf_" + h.Name + ((num2 >= 0) ? ("_" + (char)(97 + num2)) : "") + ".png");
			yield return Look(h.B, 5.5f, 0.2f, banner: true);
			hallFlagState = State(h);
			if (hallFlagState != null)
			{
				string value3 = "hf_banner_" + set + "_" + bg + "_" + icon + "_";
				bool flag2 = hallFlagState.Banner != null && hallFlagState.BannerModel != null && hallFlagState.Front != 0;
				Note(h.Name + " banner: " + Describe(h));
				if (Check(h.Name + ": its realm's banner on the front (" + FrontName(hallFlagState.Front) + ")", flag2 && hallFlagState.BannerModel.Name.StartsWith(value3, StringComparison.Ordinal), hallFlagState.BannerModel?.Name))
				{
					MeshModel bannerModel = hallFlagState.BannerModel;
					uint[] palette2 = hallFlagState.Banner.Palette;
					bool flag3 = bannerModel.SlotColor != null && bannerModel.SlotColor.Length > 3;
					uint num7 = (flag3 ? TerrainCache.Scale(main2, (float)(bannerModel.SlotColor[0] & 0xFF) / 255f) : 0u);
					uint num8 = (flag3 ? TerrainCache.Scale(main2, (float)(bannerModel.SlotColor[1] & 0xFF) / 255f) : 0u);
					uint num9 = (flag3 ? TerrainCache.Scale(emb, (float)(bannerModel.SlotColor[2] & 0xFF) / 255f) : 0u);
					Check(h.Name + ": the banner in the game's tints (background main 2, icon banner)", flag3 && palette2 != null && palette2.Length > 2 && palette2[0] == num7 && palette2[1] == num8 && palette2[2] == num9, ((palette2 != null && palette2.Length > 2) ? (Hex(palette2[0]) + "/" + Hex(palette2[1]) + "/" + Hex(palette2[2])) : "-") + " want " + Hex(num7) + "/" + Hex(num8) + "/" + Hex(num9));
					float num10 = Det(hallFlagState.Banner.Basis);
					Check(h.Name + ": the banner reads the right way round (never mirrored) and is Static", num10 > 0f && hallFlagState.Banner.Static, $"det {num10:E2}, static {hallFlagState.Banner.Static}");
				}
				Mod.SaveFrame("hf_" + h.Name + "_banner.png");
			}
		}

		private IEnumerator HallUntouched(Hall h)
		{
			yield return Look(h.B, 9f, 0.35f, banner: false);
			SceneCollector.HallFlagState hallFlagState = State(h);
			MeshInstance on = HallByTag((hallFlagState?.Cloth != null) ? hallFlagState.Cloth.Tag : (-1));
			bool sOn = on?.Static ?? false;
			MeshModel mOn = on?.Model;
			uint multOn = on?.Multiply ?? 0;
			Vec3 at = on?.Position ?? default(Vec3);
			int pieces = FlagPieces();
			SceneCollector.HallFlagsOn = false;
			yield return CivilTest.Wait(0.5f);
			MeshInstance off = null;
			List<MeshInstance> meshes = Mod.SceneForTest.Meshes;
			for (int i = 0; i < meshes.Count; i++)
			{
				if (on == null)
				{
					break;
				}
				MeshInstance meshInstance = meshes[i];
				if (meshInstance != null && meshInstance.Model == mOn && Mathf.Abs(meshInstance.Position.X - at.X) < 0.01f && Mathf.Abs(meshInstance.Position.Y - at.Y) < 0.01f)
				{
					off = meshInstance;
					break;
				}
			}
			int piecesOff = FlagPieces();
			bool sOff = off?.Static ?? false;
			uint multOff = off?.Multiply ?? 0;
			SceneCollector.HallFlagsOn = true;
			yield return CivilTest.Wait(0.3f);
			Check("the hall itself is left as it was (Static, model, tint with and without its flag)", on != null && off != null && sOn == sOff && multOn == multOff, $"static {sOn}/{sOff}, model {mOn?.Name}/{off?.Model?.Name}, tint {multOn:X8}/{multOff:X8}");
			Check("flags off: none drawn", pieces > 0 && piecesOff == 0, $"{pieces} flag pieces on, {piecesOff} off");
		}

		private IEnumerator Recolour(Hall h)
		{
			yield return Look(h.B, 9f, 0.35f, banner: false);
			Kingdom kingdom = h.B.kingdom;
			ColorAsset was = null;
			ColorAsset to = null;
			try
			{
				was = kingdom.getColor();
				uint num = Pack(was.getColorMainSecond());
				foreach (ColorAsset item in ((MetaObject<KingdomData>)kingdom).getColorLibrary().list)
				{
					if (item != null && item != was)
					{
						uint num2 = Pack(item.getColorMainSecond());
						if (Math.Abs((int)((num2 & 0xFF) - (num & 0xFF))) + Math.Abs((int)(((num2 >> 8) & 0xFF) - ((num >> 8) & 0xFF))) + Math.Abs((int)(((num2 >> 16) & 0xFF) - ((num >> 16) & 0xFF))) >= 90 && kingdom.updateColor(item))
						{
							to = item;
							break;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Note("recolour: " + ex.Message);
			}
			if (!Check("a new colour for the third human realm", to != null, to?.id))
			{
				yield break;
			}
			uint main2 = Pack(to.getColorMainSecond());
			uint emb = Pack(to.getColorBanner());
			bool swapped = false;
			bool banner = false;
			float at = -1f;
			for (float t = 0f; t < 6f; t += Time.unscaledDeltaTime)
			{
				yield return null;
				SceneCollector.HallFlagState hallFlagState = State(h);
				if (hallFlagState?.Cloth?.Palette != null && hallFlagState.Cloth.Palette.Length >= 3)
				{
					if (hallFlagState.SwapPhase != 0)
					{
						swapped = true;
					}
					FlagLook shown = hallFlagState.Shown;
					if (at < 0f && hallFlagState.Cloth.Palette[0] == TerrainCache.Scale(main2, (float)(int)shown.GreyField / 255f) && hallFlagState.Cloth.Palette[2] == TerrainCache.Scale(emb, (float)(int)shown.GreyEmblem / 255f))
					{
						at = t;
					}
					MeshModel bannerModel = hallFlagState.BannerModel;
					if (at >= 0f && hallFlagState.Banner?.Palette != null && bannerModel?.SlotColor != null && bannerModel.SlotColor.Length > 3 && hallFlagState.Banner.Palette[0] == TerrainCache.Scale(main2, (float)(bannerModel.SlotColor[0] & 0xFF) / 255f))
					{
						banner = true;
						break;
					}
				}
			}
			Check("a recolour: cloth, emblem and banner follow within 3 s, in place", at >= 0f && at <= 3f && banner && !swapped, string.Format("{0}, banner {1}, lowered {2}, {3} -> {4}", (at >= 0f) ? (at.ToString("F1") + " s") : "never", banner, swapped, was?.id, to.id));
			Mod.SaveFrame("hf_recolour.png");
		}

		private IEnumerator SampleWave(Hall h, float seconds)
		{
			List<float> bends = new List<float>();
			List<float> clocks = new List<float>();
			float last = float.NaN;
			for (float t = 0f; t < 60f; t += Time.unscaledDeltaTime)
			{
				if (bends.Count >= 60 && !(clocks[clocks.Count - 1] - clocks[0] < seconds))
				{
					break;
				}
				yield return null;
				SceneCollector.HallFlagState hallFlagState = State(h);
				float clock = Clock;
				if (hallFlagState?.Cloth?.PartRotation != null && hallFlagState.Cloth.Model != null && hallFlagState.Cloth.Model.Parts.Length >= 4 && clock != last)
				{
					last = clock;
					bends.Add(Bend(hallFlagState.Cloth));
					clocks.Add(clock);
				}
			}
			float num = float.MaxValue;
			float num2 = float.MinValue;
			float num3 = 0f;
			foreach (float item in bends)
			{
				num = Mathf.Min(num, item);
				num2 = Mathf.Max(num2, item);
				num3 += item;
			}
			if (bends.Count > 0)
			{
				num3 /= (float)bends.Count;
			}
			int num4 = 0;
			for (int i = 1; i < bends.Count; i++)
			{
				if ((bends[i] - num3) * (bends[i - 1] - num3) < 0f)
				{
					num4++;
				}
			}
			float num5 = ((clocks.Count > 1) ? (clocks[clocks.Count - 1] - clocks[0]) : 0f);
			_wave[0] = ((bends.Count > 0) ? (num2 - num) : 0f);
			_wave[1] = ((num5 > 0f) ? ((float)num4 / num5) : 0f);
			_wave[2] = num5;
			_wave[3] = bends.Count;
		}

		private IEnumerator Wave()
		{
			Hall h = _h2 ?? _halls[0];
			yield return InBody();
			yield return Look(h.B, 9f, 0.35f, banner: false);
			Gusts g = Mod.SceneForTest.Gusts;
			g.CalmFor(900f);
			g.Force = 0f;
			yield return CivilTest.Wait(2f);
			yield return SampleWave(h, 3.5f);
			float calmRange = _wave[0];
			float calmRate = _wave[1];
			float calmSpan = _wave[2];
			int calmN = (int)_wave[3];
			Mod.SaveFrame("hf_wave_calm.png");
			g.Force = 0.9f;
			yield return CivilTest.Wait(2.5f);
			yield return SampleWave(h, 3.5f);
			float num = _wave[0];
			float num2 = _wave[1];
			float num3 = _wave[2];
			int num4 = (int)_wave[3];
			Mod.SaveFrame("hf_wave_wind.png");
			Check("the flag stirs in still air (gusts at 0)", calmN > 10 && calmRange > 0.02f, $"bend range {calmRange:F3} rad over {calmSpan:F1} s of world clock, {calmN} samples");
			Check("...and flaps faster in a strong wind (0.9)", num4 > 10 && num2 > calmRate * 1.3f, $"{calmRate:F2} -> {num2:F2} crossings a second; bend range {calmRange:F3} -> {num:F3} rad (the cloth stretches out in a strong wind, bends wider in still air), {num3:F1} s");
			SceneCollector.HallFlagState hallFlagState = State(h);
			Vector3 hallFlagWindForTest = Mod.SceneForTest.HallFlagWindForTest;
			float num5 = ((hallFlagState?.Cloth != null) ? Mathf.Atan2(hallFlagState.Cloth.Basis.C0.Y, hallFlagState.Cloth.Basis.C0.X) : float.NaN);
			float num6 = Mathf.Atan2(g.DirY, g.DirX);
			float num7 = Mathf.Atan2(hallFlagWindForTest.y, hallFlagWindForTest.x);
			Check("the cloth flies downwind (within 10 degrees of the gusts' way)", !float.IsNaN(num5) && AngleDiff(num5, num6) <= 0.17453292f, $"cloth {num5 * 57.29578f:F1}, gusts {num6 * 57.29578f:F1}, eased {num7 * 57.29578f:F1} deg, strength {hallFlagWindForTest.z:F2}");
			Config.paused = true;
			yield return CivilTest.Wait(0.4f);
			float c0 = Mod.SceneForTest.HallFlagClockForTest;
			float b0 = Bend(State(h)?.Cloth);
			yield return CivilTest.Wait(1f);
			float hallFlagClockForTest = Mod.SceneForTest.HallFlagClockForTest;
			float num8 = Bend(State(h)?.Cloth);
			Config.paused = false;
			Check("the paused game holds the flag still", hallFlagClockForTest == c0 && !float.IsNaN(b0) && Mathf.Abs(num8 - b0) < 1E-05f, $"clock {c0:F3} -> {hallFlagClockForTest:F3}, bend {b0:F4} -> {num8:F4}");
			g.Force = -1f;
		}

		private IEnumerator God()
		{
			Hall h = _h2 ?? _halls[0];
			Settings s = Mod.Settings;
			Gusts g = Mod.SceneForTest.Gusts;
			g.Force = 0f;
			Mod.DebugEyeSet = false;
			Mod.DebugEyeHeight = 0f;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			yield return CivilTest.Wait(1f);
			s.GodView = true;
			Vector2 at = new Vector2((float)h.B.current_tile.x + 0.5f, (float)h.B.current_tile.y + 0.5f);
			yield return Hold(at, 12f, 2.5f);
			List<float> swings = new List<float>();
			bool banner = false;
			float c0 = Clock;
			float lastClock = float.NaN;
			float h2 = float.NaN;
			for (float t = 0f; t < 40f; t += Time.unscaledDeltaTime)
			{
				if (swings.Count >= 30 && !(Clock - c0 < 2.5f))
				{
					break;
				}
				HoldCam(at, 12f);
				yield return null;
				SceneCollector.HallFlagState hallFlagState = State(h);
				if (hallFlagState?.Banner != null)
				{
					banner = true;
				}
				if (hallFlagState?.Cloth?.Model != null && Clock != lastClock)
				{
					lastClock = Clock;
					float num = ((hallFlagState.Cloth.Model.Parts.Length >= 4 && hallFlagState.Cloth.PartRotation != null) ? Bend(hallFlagState.Cloth) : 0f);
					float num2 = Mathf.Atan2(hallFlagState.Cloth.Basis.C0.Y, hallFlagState.Cloth.Basis.C0.X);
					if (float.IsNaN(h2))
					{
						h2 = num2;
					}
					swings.Add(num + Mathf.DeltaAngle(h2 * 57.29578f, num2 * 57.29578f) * (MathF.PI / 180f));
				}
			}
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			foreach (float item in swings)
			{
				num3 = Mathf.Min(num3, item);
				num4 = Mathf.Max(num4, item);
			}
			Check("the god view shows the 3D world", Mod.GodShown);
			Check("the flag waves in the god view's own breeze, with no wind", swings.Count >= 30 && num4 - num3 > 0.02f, $"swing {((swings.Count > 0) ? (num4 - num3) : 0f):F3} rad over {Clock - c0:F1} s of world clock, {swings.Count} samples; {Describe(h)}");
			Check("its banner hangs on the front in the god view", banner);
			if (Mod.GodShown)
			{
				Mod.SaveFrame("hf_god.png");
			}
			float num5 = float.MaxValue;
			float num6 = float.MaxValue;
			float num7 = float.MinValue;
			float num8 = float.MinValue;
			foreach (Hall hall in _halls)
			{
				WorldTile current_tile = hall.B.current_tile;
				num5 = Mathf.Min(num5, current_tile.x);
				num6 = Mathf.Min(num6, current_tile.y);
				num7 = Mathf.Max(num7, current_tile.x);
				num8 = Mathf.Max(num8, current_tile.y);
			}
			Vector2 mid = new Vector2((num5 + num7) * 0.5f, (num6 + num8) * 0.5f + 2f);
			float num9 = Mathf.Max(1f, (float)Screen.width / (float)Mathf.Max(1, Screen.height));
			float zoom = Mathf.Max((num8 - num6) * 0.5f, (num7 - num5) * 0.5f / num9) + 12f;
			s.GodViewZoom = Mathf.Min(150f, Mathf.Max(s.GodViewZoom, zoom + 8f));
			yield return Hold(mid, zoom, 3f);
			List<double> ms = new List<double>();
			int drawn = 0;
			for (int f = 0; f < 150; f++)
			{
				HoldCam(mid, zoom);
				yield return null;
				if (SceneCollector.HallFlagsDrawn > 0)
				{
					ms.Add(SceneCollector.HallFlagsMs);
					drawn = Math.Max(drawn, SceneCollector.HallFlagsDrawn);
				}
			}
			ms.Sort();
			double num10 = ((ms.Count > 0) ? ms[ms.Count / 2] : double.NaN);
			double num11 = ((ms.Count > 0) ? ms[Math.Min(ms.Count - 1, ms.Count * 9 / 10)] : double.NaN);
			Check($"the flags cost 0.3 ms a frame or less with {drawn} halls in view (5 or more)", drawn >= 5 && num10 <= 0.3, $"median {num10:F3} ms, p90 {num11:F3} ms over {ms.Count} frames, zoom {zoom:F0}, shown {Mod.GodShown}");
			if (Mod.GodShown)
			{
				Mod.SaveFrame("hf_god_all.png");
			}
			List<float> off = new List<float>();
			List<float> on = new List<float>();
			for (int f = 0; f < 2; f++)
			{
				SceneCollector.HallFlagsOn = false;
				for (int i = 0; i < 60; i++)
				{
					HoldCam(mid, zoom);
					yield return null;
					if (i >= 10)
					{
						off.Add(Mod.FrameMs);
					}
				}
				SceneCollector.HallFlagsOn = true;
				for (int i = 0; i < 60; i++)
				{
					HoldCam(mid, zoom);
					yield return null;
					if (i >= 10)
					{
						on.Add(Mod.FrameMs);
					}
				}
			}
			off.Sort();
			on.Sort();
			Note($"perf: frame median {((off.Count > 0) ? off[off.Count / 2] : 0f):F2} ms with flags off, {((on.Count > 0) ? on[on.Count / 2] : 0f):F2} ms on; {SceneCollector.HallFlagsDrawn} flags drawn");
			List<Kingdom> realms = new List<Kingdom>();
			List<int> icons = new List<int>();
			List<int> counts = new List<int>();
			foreach (Hall hall2 in _halls)
			{
				Kingdom kingdom = hall2.B.kingdom;
				try
				{
					if (kingdom != null && kingdom.isCiv() && !realms.Contains(kingdom))
					{
						BannerAsset bannerAsset = AssetManager.kingdom_banners_library.get(kingdom.getActorAsset().banner_id);
						if (bannerAsset?.icons != null && bannerAsset.icons.Count >= 2)
						{
							realms.Add(kingdom);
							icons.Add(kingdom.data.banner_icon_id);
							counts.Add(bannerAsset.icons.Count);
						}
					}
				}
				catch
				{
				}
			}
			int looks = 0;
			int most = 0;
			int cachedMost = HallFlagModels.Cached;
			int last = HallFlagModels.Built;
			int built0 = HallFlagModels.Built;
			int bare = 0;
			int steps = ((realms.Count > 0) ? ((60 + realms.Count - 1) / realms.Count) : 0);
			Dictionary<Hall, int> frames = new Dictionary<Hall, int>();
			for (int f = 1; f <= steps + 1; f++)
			{
				bool back = f > steps;
				for (int j = 0; j < realms.Count; j++)
				{
					try
					{
						realms[j].data.banner_icon_id = (back ? icons[j] : ((icons[j] + f) % counts[j]));
					}
					catch
					{
					}
					if (!back)
					{
						looks++;
					}
				}
				Regalia.ResetForTest();
				for (int i = 0; i < (back ? 40 : 14); i++)
				{
					HoldCam(mid, zoom);
					yield return null;
					int built1 = HallFlagModels.Built;
					most = Math.Max(most, built1 - last);
					last = built1;
					cachedMost = Math.Max(cachedMost, HallFlagModels.Cached);
					foreach (Hall hall3 in _halls)
					{
						SceneCollector.HallFlagState hallFlagState2 = State(hall3);
						if (hallFlagState2 != null)
						{
							if (frames.TryGetValue(hall3, out var value) && value != hallFlagState2.Frame && hallFlagState2.Cloth == null && hallFlagState2.Shaft != null)
							{
								bare++;
							}
							frames[hall3] = hallFlagState2.Frame;
						}
					}
				}
			}
			Check($"{looks} looks forced: at most {2} models built a frame", looks >= 60 && most <= 2, $"{HallFlagModels.Built - built0} built, at most {most} a frame, {realms.Count} realms");
			Check($"the model cache stays within its soft cap ({600})", cachedMost <= 600, $"{cachedMost} held at most");
			Check("every flag drawn meanwhile had a cloth (plain until its own is built)", bare == 0, $"{bare} drawn without");
			s.GodView = _savedGod;
			s.GodViewZoom = _savedGodZoom;
			g.Force = -1f;
			yield return CivilTest.Wait(0.5f);
			yield return InBody();
		}

		private static string Phases(List<Tick> ticks)
		{
			List<int> list = new List<int>();
			foreach (Tick tick in ticks)
			{
				if (list.Count == 0 || list[list.Count - 1] != tick.Phase)
				{
					list.Add(tick.Phase);
				}
			}
			return string.Join(",", list);
		}

		private Tick Sample(SceneCollector.HallFlagState st, float t)
		{
			return new Tick
			{
				T = t,
				Clock = Mod.SceneForTest.HallFlagClockForTest,
				H = st.Height01,
				Z = ((st.Cloth != null) ? st.Cloth.Position.Z : float.NaN),
				Phase = st.SwapPhase,
				Style = st.Shown.Style,
				Shown = st.Shown.KingdomId,
				Banner = ((st.Banner != null && st.BannerModel != null && st.Banner.Palette != null && st.Banner.Palette.Length != 0) ? (st.BannerModel.Name + "|" + Hex(st.Banner.Palette[0])) : null)
			};
		}

		private static string Remembered(Building b)
		{
			string pResult = null;
			try
			{
				b.data.get("fp_hallflag", out pResult, null);
			}
			catch
			{
			}
			return pResult;
		}

		private static long IdOf(Kingdom k)
		{
			try
			{
				return k?.data.id ?? 0;
			}
			catch
			{
				return 0L;
			}
		}

		private IEnumerator Capture()
		{
			yield return InBody();
			bool began;
			List<Tick> ticks;
			bool retaken;
			float clock0;
			float prev;
			if (_h1 != null && _h2 != null)
			{
				Hall a = _h1;
				Kingdom to = _h2.K;
				long toId = IdOf(to);
				long fromId = IdOf(a.B.kingdom);
				yield return Look(a.B, 9f, 0.35f, banner: false);
				Note("before the capture: " + Describe(a));
				Mod.SaveFrame("hf_capture_0.png");
				SceneCollector.HallFlagState hallFlagState = State(a);
				string banner0 = ((hallFlagState != null) ? Sample(hallFlagState, 0f).Banner : null);
				try
				{
					a.B.city.joinAnotherKingdom(to, pCaptured: true);
				}
				catch (Exception ex)
				{
					Note("capture: " + ex.Message);
				}
				ticks = new List<Tick>();
				float[] shots = new float[5] { 1.2f, 2.2f, 2.75f, 4.2f, 6.5f };
				int shot = 0;
				began = false;
				clock0 = Clock;
				float t = 0f;
				while (t < 120f)
				{
					yield return null;
					t += Time.unscaledDeltaTime;
					SceneCollector.HallFlagState hallFlagState2 = State(a);
					if (hallFlagState2 != null)
					{
						ticks.Add(Sample(hallFlagState2, t));
					}
					if (shot < shots.Length && Clock - clock0 >= shots[shot])
					{
						shot++;
						Note($"capture +{Clock - clock0:F1}s ({t:F1} s real): {Short(hallFlagState2)}");
						Mod.SaveFrame($"hf_capture_{shot}.png");
					}
					if (hallFlagState2 != null && hallFlagState2.SwapPhase != 0)
					{
						began = true;
					}
					if ((began && hallFlagState2 != null && hallFlagState2.SwapPhase == 0 && shot >= shots.Length) || (!began && t > 20f))
					{
						break;
					}
				}
				string text = Phases(ticks);
				Check("a capture in view: the old flag comes down, a pause, the new one goes up", text == "1,2,3,0" || text == "0,1,2,3,0", text);
				bool flag = true;
				float num = float.MaxValue;
				foreach (Tick item in ticks)
				{
					if (item.Phase == 1 && !float.IsNaN(item.Z))
					{
						if (item.Z > num + 0.002f)
						{
							flag = false;
						}
						num = item.Z;
					}
				}
				Check("...lowering all the way (the cloth never rises on the way down)", flag && num < float.MaxValue);
				Tick tick = ticks.Find((Tick tick5) => tick5.Shown == toId);
				Check("...the new realm's flag shows only at the bottom", tick != null && tick.Phase == 2 && tick.H == 0f, (tick != null) ? $"phase {tick.Phase}, h {tick.H:F2}" : "never");
				Tick tick2 = ticks.Find((Tick tick5) => tick5.Banner != null && banner0 != null && tick5.Banner != banner0);
				Check("...the front banner changes at the bottom too", tick2 != null && tick2.Phase == 2, (tick2 != null) ? $"phase {tick2.Phase} at {tick2.T:F2} s ({banner0} -> {tick2.Banner})" : ("never (" + banner0 + ")"));
				Tick start = ticks.Find((Tick tick5) => tick5.Phase != 0);
				Tick tick3 = ((start != null) ? ticks.Find((Tick tick5) => tick5.T > start.T && tick5.Phase == 0) : null);
				float num2 = ((start != null && tick3 != null) ? (tick3.Clock - start.Clock) : (-1f));
				float num3 = ((start != null && tick3 != null) ? (tick3.T - start.T) : (-1f));
				Check("...over 1.5 to 8 s", num2 >= 1.5f && num2 <= 8f, $"{num2:F2} s of world clock, {num3:F2} s real");
				SceneCollector.HallFlagState hallFlagState3 = State(a);
				Check("...and the new owner's flag flies, fully up", hallFlagState3 != null && hallFlagState3.Shown.KingdomId == toId && hallFlagState3.SwapPhase == 0 && hallFlagState3.Height01 == 1f, Short(hallFlagState3));
				Check("...and the hall remembers it (fp_hallflag)", RememberedId(a.B) == toId, Remembered(a.B));
				bool flag2 = TownTier.IsCapital(a.B.city);
				float num4 = SizeOf(a, hallFlagState3);
				Check("...a taken town flies " + (flag2 ? "a capital's" : "a town's") + " flag (" + (flag2 ? "1.25x" : "1x") + ")", !float.IsNaN(num4) && Mathf.Abs(num4 - (flag2 ? 1.25f : 1f)) < 0.02f, $"{num4:F3}, was #{fromId}");
				if (_h3 != null)
				{
					Kingdom k = _h3.K;
					try
					{
						a.B.city.joinAnotherKingdom(k, pCaptured: true);
					}
					catch (Exception ex2)
					{
						Note("capture: " + ex2.Message);
					}
					List<Tick> back = new List<Tick>();
					retaken = false;
					t = 0f;
					prev = float.NaN;
					float t2 = 0f;
					while (t2 < 150f)
					{
						yield return null;
						t2 += Time.unscaledDeltaTime;
						SceneCollector.HallFlagState hallFlagState4 = State(a);
						if (hallFlagState4 == null)
						{
							continue;
						}
						if (!retaken && t2 > 90f)
						{
							break;
						}
						if (!float.IsNaN(prev))
						{
							t = Mathf.Max(t, Mathf.Abs(hallFlagState4.Height01 - prev));
						}
						prev = hallFlagState4.Height01;
						if (retaken)
						{
							back.Add(Sample(hallFlagState4, t2));
						}
						if (!retaken && hallFlagState4.SwapPhase == 3 && hallFlagState4.Height01 > 0.3f)
						{
							try
							{
								a.B.city.joinAnotherKingdom(to, pCaptured: true);
							}
							catch (Exception ex3)
							{
								Note("capture: " + ex3.Message);
							}
							retaken = true;
							Mod.SaveFrame("hf_capture_back.png");
						}
						if (retaken && back.Count > 5 && hallFlagState4.SwapPhase == 0)
						{
							break;
						}
					}
					SceneCollector.HallFlagState hallFlagState5 = State(a);
					string text2 = Phases(back);
					Check("taken back while the new flag went up: it comes down from where it was (no jump)", retaken && t < 0.15f && (text2 == "1,2,3,0" || text2 == "3,1,2,3,0"), $"retaken {retaken}, phases {text2}, largest step {t:F3}");
					Check("...and the right flag ends up flying", hallFlagState5 != null && hallFlagState5.Shown.KingdomId == toId && hallFlagState5.SwapPhase == 0 && hallFlagState5.Height01 == 1f && RememberedId(a.B) == toId, Short(hallFlagState5) + "; " + Remembered(a.B));
				}
			}
			int drawn;
			if (_orc != null && _h3 != null)
			{
				Hall a = _orc;
				Kingdom to = _h3.K;
				long fromId = IdOf(to);
				yield return LookAway(a);
				Check("looking away, the orc hall's flag isn't drawn", _away);
				try
				{
					a.B.city.joinAnotherKingdom(to, pCaptured: true);
				}
				catch (Exception ex4)
				{
					Note("capture: " + ex4.Message);
				}
				yield return CivilTest.Wait(1f);
				int shot = State(a)?.Frame ?? int.MinValue;
				WorldTile current_tile = a.B.current_tile;
				CivilTest.ClearBox(current_tile.x - 12, current_tile.y - 18, current_tile.x + 12, current_tile.y - 2);
				SetEye(a.B, 9f, 0.35f, banner: false);
				Tick first = null;
				began = false;
				drawn = 0;
				for (clock0 = 0f; clock0 < 40f && (drawn < 30 || clock0 < 2.5f); clock0 += Time.unscaledDeltaTime)
				{
					yield return null;
					SceneCollector.HallFlagState hallFlagState6 = State(a);
					if (hallFlagState6 != null && hallFlagState6.Frame != shot)
					{
						shot = hallFlagState6.Frame;
						drawn++;
						if (first == null)
						{
							first = Sample(hallFlagState6, clock0);
						}
						if (hallFlagState6.SwapPhase != 0)
						{
							began = true;
						}
					}
				}
				SetEye(a.B, 9f, 0.35f, banner: false);
				yield return CivilTest.Wait(0.5f);
				Check("a capture out of view: on approach the new flag is already up (no lowering)", first != null && first.Shown == fromId && first.Phase == 0 && first.H == 1f && !began, (first != null) ? $"first drawn: #{first.Shown} phase {first.Phase} h {first.H:F2}, lowered later {began}; want #{fromId}" : "never drawn");
				Mod.SaveFrame("hf_capture_unseen.png");
			}
			if (_h2 == null)
			{
				yield break;
			}
			Actor king = null;
			try
			{
				king = _h2.K.king;
			}
			catch
			{
			}
			if (!Check("the second realm has a king to choose", king?.isAlive() ?? false))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)king).addStatusEffect("invincible", 900f, pColorEffect: false);
			}
			catch
			{
			}
			yield return Possess(king);
			yield return Look(_h2.B, 9f, 0.35f, banner: false);
			drawn = StoredStyle(_h2.K);
			KingdomStyle next = (KingdomStyle)((Math.Max(0, drawn) + 1) % 3);
			began = Regalia.SetStyle(king, next);
			ticks = new List<Tick>();
			retaken = false;
			bool began2 = false;
			clock0 = Clock;
			prev = 0f;
			while (prev < 120f)
			{
				yield return null;
				prev += Time.unscaledDeltaTime;
				SceneCollector.HallFlagState hallFlagState7 = State(_h2);
				if (hallFlagState7 != null)
				{
					ticks.Add(Sample(hallFlagState7, prev));
				}
				if (!retaken && Clock - clock0 > 1.2f)
				{
					retaken = true;
					Note($"restyle +{Clock - clock0:F1}s: {Short(hallFlagState7)}");
					Mod.SaveFrame("hf_restyle_lowering.png");
				}
				if (hallFlagState7 != null && hallFlagState7.SwapPhase != 0)
				{
					began2 = true;
				}
				if ((began2 && hallFlagState7 != null && hallFlagState7.SwapPhase == 0) || (!began2 && prev > 20f))
				{
					break;
				}
			}
			Mod.SaveFrame("hf_restyle_after.png");
			Tick tick4 = ticks.Find((Tick tick5) => tick5.Style == (int)next);
			SceneCollector.HallFlagState hallFlagState8 = State(_h2);
			string value = "rg_finial_" + (char)(97 + next);
			Check("a king's new style: the flag comes down and goes up in the new shape", began && Phases(ticks) == "1,2,3,0" && tick4 != null && tick4.Phase == 2 && hallFlagState8 != null && (int)hallFlagState8.Shown.Style == (int)next && hallFlagState8.Finial != null && hallFlagState8.Finial.Model.Name.StartsWith(value, StringComparison.Ordinal) && hallFlagState8.ClothModel.Name.Contains("_" + FlagCloth.ShapeName(FlagCloth.ShapeOf((int)next)) + "_"), $"set {began}, {drawn} -> {(int)next}, phases {Phases(ticks)}, turned at phase {tick4?.Phase}; {Short(hallFlagState8)}");
			yield return InBody();
		}

		private IEnumerator Upgrade()
		{
			Hall h = _h3 ?? _h2;
			if (h == null)
			{
				yield break;
			}
			yield return InBody();
			for (int level = 1; level <= 2; level++)
			{
				yield return Look(h.B, 10f, 0.35f, banner: false);
				SceneCollector.HallFlagState st0 = State(h);
				if (!Check($"level {level - 1}: its flag flies before the rebuild", st0 != null && st0.Cloth != null, Short(st0)))
				{
					break;
				}
				string fam0 = Models.BuildingFamily(h.B);
				Vec3 foot0 = st0.FootWorld;
				long kid = st0.Shown.KingdomId;
				bool same = true;
				bool still = true;
				bool up = false;
				int drawn = 0;
				for (int tries = 0; tries < 8; tries++)
				{
					if (up)
					{
						break;
					}
					bool flag = false;
					try
					{
						flag = h.B.canBeUpgraded();
						up = flag && h.B.upgradeBuilding();
					}
					catch (Exception ex)
					{
						Note("upgrade: " + ex.Message);
					}
					if (!up)
					{
						Note($"upgrade refused (can be upgraded: {flag}), asking again");
						for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
						{
							yield return null;
						}
					}
				}
				if (!Check($"the game upgrades the hall to level {level}", up && h.B.asset.id.EndsWith("_" + level), h.B.asset.id))
				{
					break;
				}
				int frame0 = SceneFrame();
				for (float t = 0f; t < 40f && (t < 3f || SceneFrame() - frame0 < 30); t += Time.unscaledDeltaTime)
				{
					yield return null;
					SceneCollector.HallFlagState hallFlagState = State(h);
					if (hallFlagState != st0)
					{
						same = false;
					}
					if (hallFlagState != null)
					{
						if (hallFlagState.SwapPhase != 0 || hallFlagState.Height01 < 1f)
						{
							still = false;
						}
						if (hallFlagState.Cloth != null)
						{
							drawn++;
						}
					}
				}
				SceneCollector.HallFlagState hallFlagState2 = State(h);
				string text = Models.BuildingFamily(h.B);
				Check($"level {level}: the flag stays up through the rebuild (the same flag, never lowered)", same && still && drawn > 0 && hallFlagState2?.Cloth != null && hallFlagState2.Shown.KingdomId == kid, $"{fam0} -> {text}, same {same}, still {still}, drawn {drawn} frames, foot ({foot0.X:F1},{foot0.Y:F1},{foot0.Z:F1}) -> ({hallFlagState2?.FootWorld.X:F1},{hallFlagState2?.FootWorld.Y:F1},{hallFlagState2?.FootWorld.Z:F1})");
				yield return Look(h.B, (level == 2) ? 12f : 10f, 0.35f, banner: false);
				hallFlagState2 = State(h);
				bool flag2 = TownTier.IsCapital(h.B.city);
				float num = SizeOf(h, hallFlagState2);
				Check(string.Format("level {0}: the flag sized to the new hall ({1})", level, flag2 ? "a capital's: 1.25x" : "a town's: 1x"), !float.IsNaN(num) && Mathf.Abs(num - (flag2 ? 1.25f : 1f)) < 0.02f, $"{num:F3}; {Short(hallFlagState2)}");
				Mod.SaveFrame((level == 2) ? "hf_keep.png" : "hf_upgrade_1.png");
			}
		}

		private IEnumerator Torn()
		{
			yield return InBody();
			_kept.Clear();
			if (_elf != null)
			{
				Hall e = _elf;
				yield return Look(e.B, 7f, 0.5f, banner: false);
				SceneCollector.HallFlagState st0 = State(e);
				FlagLook live = st0?.Shown ?? default(FlagLook);
				uint liveField = ((st0?.Cloth?.Palette != null && st0.Cloth.Palette.Length != 0) ? st0.Cloth.Palette[0] : 0u);
				try
				{
					e.B.makeRuins();
				}
				catch (Exception ex)
				{
					Note("ruin: " + ex.Message);
				}
				yield return CivilTest.Wait(0.5f);
				yield return Look(e.B, 7f, 0.5f, banner: false);
				SceneCollector.HallFlagState hallFlagState = State(e);
				Note($"ruin: {Describe(e)} (the same flag state: {hallFlagState == st0})");
				Check("a ruin: a torn cloth on a broken pole, no finial, no banner", hallFlagState != null && hallFlagState.ClothModel != null && hallFlagState.ClothModel.Name.Contains("_t") && hallFlagState.Shaft?.Model != null && hallFlagState.Shaft.Model.Name.StartsWith("rg_hallpole_broken", StringComparison.Ordinal) && hallFlagState.Finial == null && hallFlagState.Banner == null, Short(hallFlagState));
				Check("...its last realm's flag still", hallFlagState != null && live.Known && hallFlagState.Shown.KingdomId == live.KingdomId, $"#{hallFlagState?.Shown.KingdomId}, was #{live.KingdomId}");
				if (hallFlagState?.Cloth?.Palette != null && hallFlagState.ClothModel != null && hallFlagState.Cloth.Palette.Length > 2)
				{
					uint[] array = new uint[hallFlagState.ClothModel.SlotNames.Length];
					HallFlagLook.Paint(array, hallFlagState.ClothModel, in hallFlagState.Shown, faded: true);
					uint num = hallFlagState.Cloth.Palette[0];
					Hsv(num, out var s, out var v);
					Hsv(liveField, out var s2, out var v2);
					Check("...faded: its colours dulled and darkened (the ruin's paint)", array[0] == num && array[1] == hallFlagState.Cloth.Palette[1] && array[2] == hallFlagState.Cloth.Palette[2] && s < s2 && v < 0.8f * v2, $"field {Hex(num)} (saturation {s:F2}, value {v:F2}) against {Hex(liveField)} ({s2:F2}, {v2:F2})");
					int num2 = 0;
					int num3 = 0;
					float num4 = 0f;
					string text = null;
					try
					{
						uint[] array2 = new uint[array.Length];
						uint[] array3 = new uint[array.Length];
						foreach (ColorAsset item in ((MetaObject<KingdomData>)(_h2 ?? _h3 ?? _h1).K).getColorLibrary().list)
						{
							if (item == null)
							{
								continue;
							}
							FlagLook l = new FlagLook
							{
								Known = true,
								Main2 = Pack(item.getColorMainSecond()),
								Banner = Pack(item.getColorBanner()),
								GreyField = byte.MaxValue,
								GreyDark = 200,
								GreyEmblem = byte.MaxValue,
								Style = -1
							};
							HallFlagLook.Paint(array2, hallFlagState.ClothModel, in l, faded: false);
							HallFlagLook.Paint(array3, hallFlagState.ClothModel, in l, faded: true);
							Hsv(array2[0], out var s3, out var v3);
							Hsv(array3[0], out var s4, out v3);
							if (!(s3 < 0.2f))
							{
								num2++;
								float num5 = s4 / s3;
								if (num5 < 0.7f)
								{
									num3++;
								}
								if (num5 > num4)
								{
									num4 = num5;
									text = item.id;
								}
							}
						}
					}
					catch (Exception ex2)
					{
						Note("colours: " + ex2.Message);
					}
					Check("...below 0.7x the standing cloth's saturation, for every realm colour", num2 > 0 && num3 == num2, $"{num3} of {num2} colours; worst {num4:F2}x ({text})");
				}
				if (hallFlagState?.Shaft != null)
				{
					Vec3 c = hallFlagState.Shaft.Basis.C2;
					float num6 = Mathf.Atan2(Mathf.Sqrt(c.X * c.X + c.Y * c.Y), c.Z);
					Check("...the snapped pole leans", num6 >= 0.15f, $"{num6 * 57.29578f:F1} deg");
				}
				Mod.SaveFrame("hf_ruin.png");
				_kept.Add(new Kept
				{
					H = e,
					Owner = live.KingdomId,
					Torn = true
				});
			}
			if (_dwarf != null)
			{
				Hall e = _dwarf;
				yield return Look(e.B, 9f, 0.35f, banner: false);
				long owner = State(e)?.Shown.KingdomId ?? 0;
				bool can = false;
				bool ruins = false;
				try
				{
					can = e.B.asset.can_be_abandoned;
					ruins = e.B.asset.has_ruin_state;
				}
				catch
				{
				}
				try
				{
					TileZone zone = e.B.current_tile.zone;
					(zone?.city)?.removeZone(zone);
					e.B.makeAbandoned();
				}
				catch (Exception ex3)
				{
					Note("abandon: " + ex3.Message);
				}
				bool abandoned = false;
				for (float t = 0f; t < 10f; t += 0.25f)
				{
					if (abandoned)
					{
						break;
					}
					yield return CivilTest.Wait(0.25f);
					try
					{
						abandoned = e.B.isAbandoned();
					}
					catch
					{
					}
				}
				yield return Look(e.B, 9f, 0.35f, banner: false);
				SceneCollector.HallFlagState hallFlagState2 = State(e);
				try
				{
					abandoned = e.B.isAbandoned();
				}
				catch
				{
				}
				Note(string.Format("abandoned: {0}, kingdom {1}, town {2}, can be abandoned {3}, has a ruin {4}, ruin now {5}", Describe(e), (e.B.kingdom != null) ? e.B.kingdom.name : "none", (e.B.city != null) ? e.B.city.name : "none", can, ruins, e.B.isRuin()));
				Check("an abandoned hall keeps its flag up, whole, weathered", abandoned && hallFlagState2 != null && hallFlagState2.Cloth != null && hallFlagState2.Finial != null && !hallFlagState2.ClothModel.Name.Contains("_t") && hallFlagState2.Shown.KingdomId == owner && hallFlagState2.Cloth.Multiply != 0 && hallFlagState2.Shaft != null && hallFlagState2.Shaft.Multiply == hallFlagState2.Cloth.Multiply, $"abandoned {abandoned}, #{hallFlagState2?.Shown.KingdomId} (was #{owner}), multiply {hallFlagState2?.Cloth?.Multiply:X8}");
				Mod.SaveFrame("hf_abandoned.png");
				_kept.Add(new Kept
				{
					H = e,
					Owner = owner,
					Abandoned = true
				});
			}
			if (_cat != null && _h2 != null)
			{
				Hall e = _cat;
				Kingdom to = _h2.K;
				long owner = IdOf(to);
				yield return Look(e.B, 9f, 0.35f, banner: false);
				yield return LookAway(e);
				Check("looking away, the cat hall's flag isn't drawn", _away);
				try
				{
					e.B.city.joinAnotherKingdom(to, pCaptured: true);
				}
				catch (Exception ex4)
				{
					Note("capture: " + ex4.Message);
				}
				float t = 0f;
				int frame0 = SceneFrame();
				for (; t < 90f && (RememberedId(e.B) != owner || t < 1f); t += Time.unscaledDeltaTime)
				{
					yield return null;
				}
				SceneCollector.HallFlagState hallFlagState3 = State(e);
				Check("its data follows the new realm unseen (fp_hallflag)", RememberedId(e.B) == owner, $"{Remembered(e.B)} after {t:F1} s, {SceneFrame() - frame0} frames; still undrawn: {hallFlagState3 == null || SceneFrame() - hallFlagState3.Frame > 120}");
				try
				{
					e.B.makeRuins();
				}
				catch (Exception ex5)
				{
					Note("ruin: " + ex5.Message);
				}
				yield return CivilTest.Wait(0.5f);
				yield return Look(e.B, 7f, 0.5f, banner: false);
				SceneCollector.HallFlagState hallFlagState4 = State(e);
				Note("taken and ruined: " + Describe(e));
				Check("a hall taken unseen, then ruined: its new realm's flag, torn", hallFlagState4 != null && hallFlagState4.Shown.KingdomId == owner && hallFlagState4.ClothModel != null && hallFlagState4.ClothModel.Name.Contains("_t") && hallFlagState4.Finial == null, $"#{hallFlagState4?.Shown.KingdomId}, want #{owner}; {Short(hallFlagState4)}");
				Mod.SaveFrame("hf_ruin_taken.png");
				_kept.Add(new Kept
				{
					H = e,
					Owner = owner,
					Torn = true
				});
			}
			foreach (Hall hall in _halls)
			{
				bool flag = false;
				try
				{
					flag = hall.B != null && hall.B.isAlive() && !hall.B.isRuin() && !hall.B.isAbandoned();
				}
				catch
				{
				}
				if (flag)
				{
					_kept.Add(new Kept
					{
						H = hall,
						Owner = IdOf(hall.B.kingdom)
					});
				}
			}
			yield return Reload();
		}

		private Dictionary<long, SceneCollector.HallFlagState> StatesForTest()
		{
			try
			{
				return typeof(SceneCollector).GetField("_hallFlags", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(Mod.SceneForTest) as Dictionary<long, SceneCollector.HallFlagState>;
			}
			catch
			{
				return null;
			}
		}

		private IEnumerator Reload()
		{
			int whileLoading = HallFlagLook.WrittenWhileLoading;
			byte[] array;
			try
			{
				SavedMap savedMap = SaveManager.currentWorldToSavedMap();
				Dictionary<long, string> dictionary = new Dictionary<long, string>();
				foreach (Kept item in _kept)
				{
					dictionary[item.H.Id] = item.H.Name;
				}
				foreach (BuildingData building in savedMap.buildings)
				{
					if (building != null && dictionary.TryGetValue(building.id, out var value))
					{
						Note($"in the save: {value} #{building.id} {building.asset_id} state {building.state} at {building.mainX},{building.mainY} town #{building.cityID}");
					}
				}
				List<string> list = new List<string>();
				foreach (Building building2 in World.world.buildings)
				{
					if (building2?.asset != null && building2.asset.type == "type_hall")
					{
						list.Add(string.Format("{0} #{1} at {2},{3}{4}", building2.asset.id, building2.data.id, building2.current_tile.x, building2.current_tile.y, building2.isRuin() ? " ruin" : ""));
					}
				}
				Note(string.Format("the save holds {0} buildings, {1} towns; halls now: {2}", savedMap.buildings.Count, savedMap.cities.Count, string.Join("; ", list.ToArray())));
				array = savedMap.toZip();
			}
			catch (Exception ex)
			{
				Check("the world saved to bytes", ok: false, ex.Message);
				yield break;
			}
			List<Building> oldBuildings = new List<Building>();
			Dictionary<Hall, Vector2Int> tiles = new Dictionary<Hall, Vector2Int>();
			Dictionary<Hall, BuildingAsset> kinds = new Dictionary<Hall, BuildingAsset>();
			foreach (Kept item2 in _kept)
			{
				oldBuildings.Add(item2.H.B);
				bool flag = false;
				bool flag2 = false;
				try
				{
					flag = item2.H.B.isAlive();
					flag2 = item2.H.B.isRuin();
					tiles[item2.H] = new Vector2Int(item2.H.B.current_tile.x, item2.H.B.current_tile.y);
					kinds[item2.H] = item2.H.B.asset;
				}
				catch
				{
				}
				Note($"saving {item2.H.Name}: {item2.H.B.asset?.id} #{item2.H.B.data.id} (set up as #{item2.H.Id}), alive {flag}, ruin {flag2}, data {Remembered(item2.H.B)}");
			}
			object world0 = World.world.map_stats;
			Note($"saved {array.Length} bytes; loading it back");
			Mod.DebugEyeSet = false;
			try
			{
				SaveManager.loadMapFromBytes(array);
			}
			catch (Exception ex2)
			{
				Check("the world loaded from bytes", ok: false, ex2.Message);
				yield break;
			}
			int loadingFrames = 0;
			int writtenLoading = 0;
			int written0 = HallFlagLook.Written;
			for (float t = 0f; t < 60f; t += Time.unscaledDeltaTime)
			{
				yield return null;
				if (WorldBoxMod.WorldLoading)
				{
					loadingFrames++;
					writtenLoading += HallFlagLook.Written - written0;
				}
				written0 = HallFlagLook.Written;
				bool flag3 = true;
				try
				{
					flag3 = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag3 && Config.game_loaded && !WorldBoxMod.WorldLoading && World.world.map_stats != world0 && t > 2f)
				{
					break;
				}
			}
			yield return CivilTest.Wait(1.5f);
			if (!Check("the world loaded again", World.world.map_stats != world0 && !WorldBoxMod.WorldLoading, $"{loadingFrames} frames loading"))
			{
				yield break;
			}
			Check("nothing written to a hall while the world loaded", HallFlagLook.WrittenWhileLoading == whileLoading && writtenLoading == 0, $"{HallFlagLook.WrittenWhileLoading - whileLoading} refused, {writtenLoading} written in {loadingFrames} loading frames");
			Dictionary<long, Building> byId = new Dictionary<long, Building>();
			List<string> list2 = new List<string>();
			try
			{
				foreach (Building building3 in World.world.buildings)
				{
					if (building3 != null)
					{
						byId[building3.data.id] = building3;
						if (building3.asset != null && building3.asset.type == "type_hall")
						{
							list2.Add(string.Format("{0} #{1} at {2},{3}{4}", building3.asset.id, building3.data.id, building3.current_tile.x, building3.current_tile.y, building3.isRuin() ? " ruin" : ""));
						}
					}
				}
			}
			catch (Exception ex3)
			{
				Note("buildings: " + ex3.Message);
			}
			Note(string.Format("after the reload: {0} buildings, {1} towns; halls: {2}", byId.Count, World.world.cities.Count, string.Join("; ", list2.ToArray())));
			foreach (Hall hall in _halls)
			{
				hall.B = (byId.TryGetValue(hall.Id, out var value2) ? value2 : null);
				if (hall.B == null && tiles.TryGetValue(hall, out var value3))
				{
					WorldTile tile = World.world.GetTile(value3.x, value3.y);
					Note(string.Format("{0}: no building #{1} after the reload; on its tile: {2}", hall.Name, hall.Id, (tile?.building != null) ? (tile.building.asset.id + " #" + tile.building.data.id) : "nothing"));
				}
				hall.K = hall.B?.kingdom;
			}
			_you = null;
			yield return InBody();
			bool fresh = true;
			int standing = 0;
			foreach (Kept k in _kept)
			{
				Hall h = k.H;
				if (h.B == null)
				{
					Vector2Int value4;
					BuildingAsset value5;
					string text = ((tiles.TryGetValue(h, out value4) && kinds.TryGetValue(h, out value5)) ? LoadRefuses(World.world.GetTile(value4.x, value4.y), value5) : "no tile kept");
					if (k.Torn && text == null)
					{
						Check(h.Name + ": the ruin is back after the reload", ok: false, "its tiles would take it");
					}
					else
					{
						Note(h.Name + ": not back after the reload (the game's load refused its tiles: " + (text ?? "they'd take it now") + ")");
					}
					continue;
				}
				if (!k.Torn && !k.Abandoned)
				{
					standing++;
				}
				yield return Look(h.B, k.Torn ? 8f : 9f, 0.35f, banner: false);
				SceneCollector.HallFlagState hallFlagState = State(h);
				Note(h.Name + " after the reload: " + Describe(h));
				if (hallFlagState != null && hallFlagState.SwapPhase != 0)
				{
					fresh = false;
				}
				if (k.Torn)
				{
					Check(h.Name + ": still torn after the reload, its last realm's flag", hallFlagState != null && hallFlagState.Shown.KingdomId == k.Owner && hallFlagState.ClothModel != null && hallFlagState.ClothModel.Name.Contains("_t") && hallFlagState.Finial == null, $"#{hallFlagState?.Shown.KingdomId}, want #{k.Owner}; {Short(hallFlagState)}");
				}
				else if (k.Abandoned)
				{
					Check(h.Name + ": abandoned, still its realm's flag, weathered, after the reload", hallFlagState != null && hallFlagState.Shown.KingdomId == k.Owner && hallFlagState.Cloth != null && hallFlagState.Cloth.Multiply != 0, $"#{hallFlagState?.Shown.KingdomId}, want #{k.Owner}, multiply {hallFlagState?.Cloth?.Multiply:X8}");
				}
				else
				{
					Check(h.Name + ": its realm's flag after the reload, up", hallFlagState != null && hallFlagState.Shown.KingdomId == k.Owner && hallFlagState.SwapPhase == 0 && hallFlagState.Height01 == 1f && RememberedId(h.B) == k.Owner, $"#{hallFlagState?.Shown.KingdomId}, want #{k.Owner}; {Remembered(h.B)}");
				}
				Mod.SaveFrame("hf_reload_" + h.Name + ".png");
			}
			Check("a standing hall back after the reload to look at", standing > 0, $"{standing} back");
			Check("no flag lowered on first sight after the reload", fresh);
			int num = 0;
			int num2 = 0;
			Dictionary<long, SceneCollector.HallFlagState> dictionary2 = StatesForTest();
			if (dictionary2 != null)
			{
				foreach (KeyValuePair<long, SceneCollector.HallFlagState> item3 in dictionary2)
				{
					num2++;
					if (!byId.TryGetValue(item3.Key, out var value6) || value6 != item3.Value.B)
					{
						num++;
					}
				}
			}
			bool flag4 = true;
			foreach (Building item4 in oldBuildings)
			{
				if (item4 != null && Mod.SceneForTest.HallFlagForTest(item4) != null && !byId.ContainsValue(item4))
				{
					flag4 = false;
				}
			}
			Check("the old world's flags were let go with it (map_stats changed)", dictionary2 != null && num == 0 && flag4, $"{num2} flag states, {num} of the old world");
		}

		private static string LoadRefuses(WorldTile at, BuildingAsset kind)
		{
			if (at == null || kind == null)
			{
				return "no tile";
			}
			try
			{
				if (World.world.buildings.canBuildFrom(at, kind, (City)null, BuildPlacingType.Load, pFloraGrowth: false))
				{
					return null;
				}
			}
			catch (Exception ex)
			{
				return "canBuildFrom: " + ex.Message;
			}
			List<string> list = new List<string>();
			try
			{
				BuildingFundament fundament = kind.fundament;
				for (int i = at.x - fundament.left; i < at.x - fundament.left + fundament.width; i++)
				{
					for (int j = at.y - fundament.bottom; j < at.y - fundament.bottom + fundament.height; j++)
					{
						WorldTile tile = World.world.GetTile(i, j);
						if (tile == null)
						{
							list.Add($"off the map at {i},{j}");
							continue;
						}
						bool flag = tile.isOnFire();
						if (tile.Type.liquid || tile.Type.block || tile.Type.lava || flag || tile.building != null)
						{
							list.Add(string.Format("{0}{1} at {2},{3}{4}", (tile.building != null) ? (tile.building.asset.id + " #" + tile.building.data.id + " on ") : "", tile.Type.id, i, j, flag ? " burning" : ""));
						}
					}
				}
			}
			catch
			{
			}
			if (list.Count <= 0)
			{
				return "refused";
			}
			return string.Join("; ", list.ToArray());
		}

		private IEnumerator Cache()
		{
			yield return InBody();
			Hall h = _halls.Find((Hall x) => x.B != null && x.B.isAlive() && !x.B.isRuin());
			if (h != null)
			{
				yield return Look(h.B, 9f, 0.35f, banner: false);
			}
			SpriteCache sprites = null;
			try
			{
				sprites = (typeof(SceneCollector).GetField("_townScene", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(Mod.SceneForTest) as TownScene)?.Sprites;
			}
			catch
			{
			}
			if (!Check("the scene's sprite cache", sprites != null))
			{
				yield break;
			}
			List<KeyValuePair<string, int>> sets = new List<KeyValuePair<string, int>>();
			try
			{
				foreach (BannerAsset item in AssetManager.kingdom_banners_library.list)
				{
					if (item?.icons != null && item.icons.Count > 0)
					{
						sets.Add(new KeyValuePair<string, int>(item.id, item.icons.Count));
					}
				}
			}
			catch (Exception ex)
			{
				Note("banner sets: " + ex.Message);
			}
			List<int[]> keys = new List<int[]>();
			for (int num = 0; num < sets.Count; num++)
			{
				if (keys.Count >= 4000)
				{
					break;
				}
				for (int num2 = 0; num2 < sets[num].Value; num2++)
				{
					for (int num3 = -1; num3 <= 3; num3++)
					{
						for (int num4 = 0; num4 < 4; num4++)
						{
							for (int num5 = 0; num5 < 3; num5++)
							{
								keys.Add(new int[5] { num, num2, num4, num5, num3 });
							}
						}
					}
				}
			}
			int most = HallFlagModels.Cached;
			int start = HallFlagModels.Cached;
			int next = 0;
			int stuck = 0;
			int frameMost = 0;
			float t0 = Time.unscaledTime;
			while (HallFlagModels.Cached < 600 && next < keys.Count && Time.unscaledTime - t0 < 60f)
			{
				int built = HallFlagModels.Built;
				for (int num6 = 0; num6 < 4; num6++)
				{
					if (next >= keys.Count)
					{
						break;
					}
					int[] array = keys[next];
					if (HallFlagModels.Cloth(new FlagLook
					{
						Set = sets[array[0]].Key,
						Icon = array[1],
						Known = true,
						Style = -1
					}, (FlagShape)array[2], array[3], array[4], sprites) != null)
					{
						next++;
						stuck = 0;
						continue;
					}
					int num7 = stuck + 1;
					stuck = num7;
					if (num7 > 40)
					{
						next++;
						stuck = 0;
					}
					break;
				}
				frameMost = Math.Max(frameMost, HallFlagModels.Built - built);
				most = Math.Max(most, HallFlagModels.Cached);
				if (h != null)
				{
					SetEye(h.B, 9f, 0.35f, banner: false);
				}
				yield return null;
				most = Math.Max(most, HallFlagModels.Cached);
			}
			int num8 = 600;
			Check($"the model cache fills to its soft cap ({num8}) and no further", HallFlagModels.Cached == num8 && most <= num8, $"{start} -> {HallFlagModels.Cached} held (at most {most}) in {Time.unscaledTime - t0:F1} s, {next} of {keys.Count} looks, at most {frameMost} built in a frame here");
			MeshModel meshModel = HallFlagModels.Cloth(new FlagLook
			{
				Set = "hf_test_none",
				Icon = 0,
				Known = true,
				Style = -1
			}, FlagShape.Plain, 0, -1, sprites);
			MeshModel meshModel2 = HallFlagModels.Plain(FlagShape.Plain, 0, -1);
			Check("past the cap a new look gets the plain cloth", meshModel == null && meshModel2 != null && HallFlagModels.Cached <= num8, (meshModel?.Name ?? "none built") + ", plain " + meshModel2?.Name);
			if (h != null)
			{
				SceneCollector.HallFlagState hallFlagState = State(h);
				Check("flags still drawn at the cap", hallFlagState != null && hallFlagState.Cloth != null, Short(hallFlagState));
			}
		}
	}
}
