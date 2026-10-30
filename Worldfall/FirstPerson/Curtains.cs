using System;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class Curtains
	{
		private sealed class Shown
		{
			public float[] Open = new float[0];

			public int Frame = -1;

			public float Seen;

			public float At;
		}

		private struct Win
		{
			public Vec3 Mid;

			public Vec3 R;

			public Vec3 In;

			public float RodHalf;

			public float Panel;

			public float Top;

			public float Drop;

			public int Index;

			public int Level;
		}

		private sealed class RoomSet
		{
			public Interior Room;

			public long Id;

			public Building House;

			public Actor Ship;

			public Win[] Wins;

			public MeshInstance[] A;

			public MeshInstance[] B;

			public MeshInstance[] Rods;

			public uint Cloth;

			public int Count;
		}

		private sealed class StreetSet
		{
			public MeshInstance[] A;

			public MeshInstance[] B;

			public uint Cloth;

			public float Seen;
		}

		private const string DrawnKey = "fp_curtain";

		private const string StampKey = "fp_curtain_t";

		private const int All = -1;

		private const float DrawTime = 0.9f;

		private const float HoldAll = 0.45f;

		private const float FaceIn = 0.084f;

		private const float CabinFaceIn = -0.03f;

		private const float ToWall = 0.11f;

		private const float Reach = 2.4f;

		private static readonly Dictionary<long, Shown> _shown = new Dictionary<long, Shown>();

		private static readonly Dictionary<Interior, RoomSet> _rooms = new Dictionary<Interior, RoomSet>();

		private static RoomSet _pressSet;

		private static int _pressWin = -1;

		private static float _pressAt;

		private static bool _pressAll;

		private static float _failAt = -99f;

		private const float StreetFar = 42f;

		private const float StreetOut = 0.012f;

		private const float StreetGlow = 0.45f;

		private static readonly Dictionary<long, StreetSet> _street = new Dictionary<long, StreetSet>();

		private static readonly Dictionary<string, HousePlan> _plans = new Dictionary<string, HousePlan>();

		private static readonly HashSet<string> _measuring = new HashSet<string>();

		private static int _streetFrame = -10;

		private static int _streetCount;

		private const float StandOff = 0.38f;

		private static Building _errandHouse;

		private static int _errandWant;

		private static float _errandUntil;

		internal static bool ErrandRunningForTest => _errandHouse != null;

		internal static int StreetPanelsForTest
		{
			get
			{
				if (Time.frameCount - _streetFrame >= 5)
				{
					return 0;
				}
				return _streetCount;
			}
		}

		private static int Mask(Building b)
		{
			try
			{
				b.data.get("fp_curtain", out var pResult, 0);
				return pResult;
			}
			catch
			{
				return 0;
			}
		}

		private static void SetMask(Building b, int mask)
		{
			if (WorldBoxMod.WorldLoading)
			{
				return;
			}
			try
			{
				if (mask == 0)
				{
					b.data.removeInt("fp_curtain");
				}
				else
				{
					b.data.set("fp_curtain", mask);
				}
			}
			catch
			{
			}
		}

		private static int Mask(Actor ship)
		{
			try
			{
				ship.data.get("fp_curtain", out var pResult, 0);
				return pResult;
			}
			catch
			{
				return 0;
			}
		}

		private static void SetMask(Actor ship, int mask)
		{
			if (WorldBoxMod.WorldLoading)
			{
				return;
			}
			try
			{
				if (mask == 0)
				{
					ship.data.removeInt("fp_curtain");
				}
				else
				{
					ship.data.set("fp_curtain", mask);
				}
			}
			catch
			{
			}
		}

		private static bool Bit(int mask, int i)
		{
			if (i >= 0 && i < 32)
			{
				return (mask & (1 << i)) != 0;
			}
			return false;
		}

		private static int With(int mask, int i, bool on)
		{
			if (i >= 0 && i < 32)
			{
				if (!on)
				{
					return mask & ~(1 << i);
				}
				return mask | (1 << i);
			}
			return mask;
		}

		private static bool AllDrawn(int mask, RoomSet set)
		{
			Win[] wins = set.Wins;
			for (int i = 0; i < wins.Length; i++)
			{
				Win win = wins[i];
				if (win.Index >= 0 && !Bit(mask, win.Index))
				{
					return false;
				}
			}
			return true;
		}

		private static Shown Animate(long id, int mask, int count)
		{
			if (!_shown.TryGetValue(id, out var value))
			{
				if (_shown.Count > 400)
				{
					Forget();
				}
				value = (_shown[id] = new Shown
				{
					At = Time.unscaledTime
				});
			}
			value.Seen = Time.unscaledTime;
			if (value.Open.Length < count)
			{
				float[] array = new float[count];
				Array.Copy(value.Open, array, value.Open.Length);
				for (int i = value.Open.Length; i < count; i++)
				{
					array[i] = (Bit(mask, i) ? 1f : 0f);
				}
				value.Open = array;
			}
			if (value.Frame == Time.frameCount)
			{
				return value;
			}
			value.Frame = Time.frameCount;
			float maxDelta = Mathf.Clamp(Time.unscaledTime - value.At, 0f, 0.25f) / 0.9f;
			value.At = Time.unscaledTime;
			for (int j = 0; j < value.Open.Length; j++)
			{
				value.Open[j] = Mathf.MoveTowards(value.Open[j], Bit(mask, j) ? 1f : 0f, maxDelta);
			}
			return value;
		}

		private static void Forget()
		{
			float unscaledTime = Time.unscaledTime;
			List<long> list = new List<long>();
			foreach (KeyValuePair<long, Shown> item in _shown)
			{
				if (unscaledTime - item.Value.Seen > 30f)
				{
					list.Add(item.Key);
				}
			}
			foreach (long item2 in list)
			{
				_shown.Remove(item2);
			}
		}

		private static float Width(float open)
		{
			float t = open * open * (3f - 2f * open);
			return Mathf.Lerp(0.22f, 1f, t);
		}

		private static void Routine(Building b, Actor you, RoomSet inRoom)
		{
			try
			{
				if (b == null || b.data == null || !b.hasResidents() || (you != null && b == Workbench.HomeOf(you)))
				{
					return;
				}
				float num = (float)(TerrainModel.Hash((int)(b.data.id & 0xFFFFF), 77) & 0x3FF) / 1023f;
				float num2 = Hour();
				bool flag = num2 >= 0.77f + 0.06f * num || num2 < 0.22f + 0.05f * num;
				int num3 = Calendar.Day * 2 + (flag ? 1 : 0);
				b.data.get("fp_curtain_t", out var pResult, int.MinValue);
				if (pResult != num3 && !WorldBoxMod.WorldLoading)
				{
					b.data.set("fp_curtain_t", num3);
					int num4 = (flag ? (-1) : 0);
					if (Mask(b) != num4 && (inRoom == null || pResult == int.MinValue || !Errands(inRoom, flag)))
					{
						SetMask(b, num4);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("curtains: routine: " + ex.Message);
			}
		}

		private static float Hour()
		{
			return ((WorldBoxMod.Instance != null) ? WorldBoxMod.Instance.Day : null)?.Hour ?? Calendar.TimeOfDay;
		}

		private static void Touched(Building b)
		{
			try
			{
				float num = (float)(TerrainModel.Hash((int)(b.data.id & 0xFFFFF), 77) & 0x3FF) / 1023f;
				float num2 = Hour();
				bool flag = num2 >= 0.77f + 0.06f * num || num2 < 0.22f + 0.05f * num;
				b.data.set("fp_curtain_t", Calendar.Day * 2 + (flag ? 1 : 0));
			}
			catch
			{
			}
		}

		private static RoomSet SetFor(Interior room, Building house, Actor ship, bool cabin)
		{
			if (room == null || room.Windows == null)
			{
				return null;
			}
			if (_rooms.TryGetValue(room, out var value))
			{
				return value;
			}
			if (_rooms.Count > 8)
			{
				_rooms.Clear();
			}
			value = new RoomSet
			{
				Room = room,
				House = house,
				Ship = ship
			};
			try
			{
				value.Id = house?.data.id ?? ship?.data.id ?? 0;
				value.Cloth = ClothOf((house != null) ? house.kingdom : ship?.kingdom, value.Id);
			}
			catch
			{
				value.Cloth = ClothOf(null, 0L);
			}
			int count = room.Windows.Count;
			value.Wins = new Win[count];
			value.A = new MeshInstance[count];
			value.B = new MeshInstance[count];
			value.Rods = new MeshInstance[count];
			FitInfo fit = room.Fit;
			HousePlan housePlan = fit?.Plan;
			value.Count = housePlan?.Windows.Count ?? count;
			for (int i = 0; i < count; i++)
			{
				LightWindow w = room.Windows[i];
				Win win = new Win
				{
					Index = ((housePlan != null) ? PlanIndex(fit, w) : i)
				};
				Vec3 vec = Flat(w.Inward);
				Vec3 vec2 = Flat(w.Right);
				if (vec2.X * vec.Y - vec2.Y * vec.X < 0f)
				{
					vec2 *= -1f;
				}
				float num = (cabin ? (-0.03f) : 0.084f);
				Vec3 vec3 = w.Center + vec * (num + 0.11f);
				float num2 = w.Center.Z + w.HalfHeight + (cabin ? 0.06f : 0.07f);
				float num3 = w.Center.Z - w.HalfHeight - (cabin ? 0.07f : 0.05f);
				if (fit != null)
				{
					win.Level = fit.StoreyAt(num3 + 0.06f);
					float num4 = fit.CeilingAt(win.Level, vec3.X, vec3.Y);
					if (!float.IsNaN(num4))
					{
						num2 = Math.Min(num2, num4 - 0.035f);
					}
				}
				float num5 = (cabin ? 0.05f : 0.12f);
				win.Mid = new Vec3(vec3.X, vec3.Y, num2);
				win.R = vec2;
				win.In = vec;
				win.RodHalf = w.HalfWidth + num5;
				win.Panel = win.RodHalf + 0.02f;
				win.Top = num2;
				win.Drop = Math.Max(0.1f, num2 - num3 - 0.015f);
				value.Wins[i] = win;
				value.A[i] = Panel(value.Cloth);
				value.B[i] = Panel(value.Cloth);
				value.Rods[i] = new MeshInstance
				{
					Model = CurtainMesh.Rod(0.11f),
					NoShadow = true,
					Tag = -1
				};
				value.Rods[i].Basis = new Mat3(vec2 * (2f * win.RodHalf), vec, new Vec3(0f, 0f, 1f));
				value.Rods[i].Position = new Vec3(vec3.X, vec3.Y, num2) - vec2 * win.RodHalf;
			}
			_rooms[room] = value;
			return value;
		}

		private static Vec3 Flat(Vec3 v)
		{
			float num = (float)Math.Sqrt(v.X * v.X + v.Y * v.Y);
			if (!(num > 1E-05f))
			{
				return new Vec3(1f, 0f, 0f);
			}
			return new Vec3(v.X / num, v.Y / num, 0f);
		}

		private static int PlanIndex(FitInfo fit, LightWindow w)
		{
			HousePlan plan = fit.Plan;
			int result = -1;
			float num = float.MaxValue;
			for (int i = 0; i < plan.Windows.Count; i++)
			{
				HousePlan.Opening opening = plan.Windows[i];
				float num2 = fit.RoomX(opening.X) - w.Center.X;
				float num3 = fit.RoomY(opening.Y) - w.Center.Y;
				float num4 = ((opening.Z0 + opening.Z1) * 0.5f - plan.FloorZ) * fit.Scale - w.Center.Z;
				float num5 = num2 * num2 + num3 * num3 + num4 * num4 * 0.5f;
				if (num5 < num)
				{
					num = num5;
					result = i;
				}
			}
			return result;
		}

		private static MeshInstance Panel(uint cloth)
		{
			MeshInstance meshInstance = new MeshInstance();
			meshInstance.Model = CurtainMesh.Room(0);
			meshInstance.Palette = new uint[1] { cloth };
			meshInstance.NoShadow = true;
			meshInstance.Tag = -1;
			return meshInstance;
		}

		internal static uint ClothOf(Kingdom k, long id)
		{
			Color a = new Color(0.82f, 0.76f, 0.64f);
			bool flag = false;
			try
			{
				flag = k?.isCiv() ?? false;
			}
			catch
			{
			}
			Color b;
			if (flag)
			{
				b = Royal.ColorOf(k);
			}
			else
			{
				Color[] array = new Color[5]
				{
					new Color(0.62f, 0.22f, 0.18f),
					new Color(0.24f, 0.34f, 0.55f),
					new Color(0.72f, 0.6f, 0.26f),
					new Color(0.33f, 0.45f, 0.28f),
					new Color(0.82f, 0.76f, 0.64f)
				};
				b = array[(uint)((ulong)id % (ulong)array.Length)];
			}
			Color color = Color.Lerp(a, b, 0.36f) * 0.86f;
			return TerrainCache.Rgb((int)(Mathf.Clamp01(color.r) * 255f), (int)(Mathf.Clamp01(color.g) * 255f), (int)(Mathf.Clamp01(color.b) * 255f));
		}

		private static void Lay(RoomSet set, int i, float open, Vec3 eye)
		{
			Win win = set.Wins[i];
			float stretch;
			MeshModel model = CurtainMesh.Room(CurtainMesh.Step(Width(open), out stretch));
			float num = win.Panel * stretch;
			Vec3 c = new Vec3(0f, 0f, win.Drop);
			Vec3 vec = new Vec3(0f, 0f, -0.012f);
			MeshInstance obj = set.A[i];
			MeshInstance meshInstance = set.B[i];
			obj.Model = model;
			obj.Basis = new Mat3(win.R * num, win.In * win.Panel, c);
			obj.Position = win.Mid - win.R * win.RodHalf + vec;
			meshInstance.Model = model;
			meshInstance.Basis = new Mat3(win.R * (0f - num), win.In * (0f - win.Panel), c);
			meshInstance.Position = win.Mid + win.R * win.RodHalf + vec;
			float sortDepth = (float)Math.Sqrt((win.Mid.X - eye.X) * (win.Mid.X - eye.X) + (win.Mid.Y - eye.Y) * (win.Mid.Y - eye.Y));
			obj.SortDepth = (meshInstance.SortDepth = (set.Rods[i].SortDepth = sortDepth));
		}

		public static void AddRoom(List<MeshInstance> meshes, HouseInterior house, float dt)
		{
			try
			{
				if (house == null || house.Room == null || house.Virtual || house.House == null || HouseInterior.RoomName(house.House.asset) != "house")
				{
					return;
				}
				RoomSet roomSet = SetFor(house.Room, house.House, null, cabin: false);
				if (roomSet != null && roomSet.Wins.Length != 0)
				{
					WorldBoxMod instance = WorldBoxMod.Instance;
					Actor you = ((instance != null) ? (instance.Body ?? instance.Host) : null);
					if (!house.Preview)
					{
						CheckErrands(house);
						Routine(house.House, you, roomSet);
					}
					AddSet(meshes, roomSet, Mask(house.House), dt, instance);
				}
			}
			catch (Exception e)
			{
				Fail("room", e);
			}
		}

		public static void AddCabin(List<MeshInstance> meshes, BoatCabin cabin, float dt)
		{
			try
			{
				if (cabin != null && cabin.Room != null && cabin.Boat != null && cabin.Inside)
				{
					RoomSet roomSet = SetFor(cabin.Room, null, cabin.Boat, cabin: true);
					if (roomSet != null && roomSet.Wins.Length != 0)
					{
						AddSet(meshes, roomSet, Mask(cabin.Boat), dt, WorldBoxMod.Instance);
					}
				}
			}
			catch (Exception e)
			{
				Fail("cabin", e);
			}
		}

		private static void AddSet(List<MeshInstance> meshes, RoomSet set, int mask, float dt, WorldBoxMod mod)
		{
			Shown shown = Animate(set.Id, mask, set.Count);
			Vec3 eye = ((mod != null) ? new Vec3(mod.View.X, mod.View.Y, mod.View.Z) : set.Wins[0].Mid);
			for (int i = 0; i < set.Wins.Length; i++)
			{
				int index = set.Wins[i].Index;
				float open = ((index >= 0 && index < shown.Open.Length) ? shown.Open[index] : 0f);
				Lay(set, i, open, eye);
				meshes.Add(set.Rods[i]);
				meshes.Add(set.A[i]);
				meshes.Add(set.B[i]);
			}
		}

		public static bool Covers(Interior room, LightWindow w)
		{
			if (room == null || !_rooms.TryGetValue(room, out var value))
			{
				return false;
			}
			if (!_shown.TryGetValue(value.Id, out var value2))
			{
				return false;
			}
			for (int i = 0; i < room.Windows.Count && i < value.Wins.Length; i++)
			{
				LightWindow lightWindow = room.Windows[i];
				if (lightWindow.Center.X == w.Center.X && lightWindow.Center.Y == w.Center.Y && lightWindow.Center.Z == w.Center.Z)
				{
					int index = value.Wins[i].Index;
					if (index >= 0 && index < value2.Open.Length)
					{
						return value2.Open[index] >= 0.995f;
					}
					return false;
				}
			}
			return false;
		}

		public static bool RoomPrompt(PromptAsk ask)
		{
			HouseInterior house = ask.Room;
			if (house == null || !house.Inside || house.Virtual || house.House == null || house.Room == null)
			{
				return false;
			}
			if (HouseInterior.RoomName(house.House.asset) != "house")
			{
				return false;
			}
			if (!_rooms.TryGetValue(house.Room, out var value))
			{
				return false;
			}
			string text = Prompt(value, () => Mask(house.House), delegate(int m)
			{
				SetMask(house.House, m);
				Touched(house.House);
			});
			if (text == null)
			{
				return false;
			}
			ask.Prompt = text;
			return true;
		}

		public static string CabinPrompt(BoatCabin cabin)
		{
			try
			{
				if (cabin == null || !cabin.Inside || cabin.Room == null || cabin.Boat == null)
				{
					return null;
				}
				if (!_rooms.TryGetValue(cabin.Room, out var value))
				{
					return null;
				}
				Actor ship = cabin.Boat;
				return Prompt(value, () => Mask(ship), delegate(int m)
				{
					SetMask(ship, m);
				});
			}
			catch (Exception e)
			{
				Fail("cabin prompt", e);
				return null;
			}
		}

		private static string Prompt(RoomSet set, Func<int> read, Action<int> write)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance == null)
			{
				return null;
			}
			if (_pressWin >= 0 && (_pressSet != set || (!PlayInput.GetKey(KeyCode.E) && Time.unscaledTime - _pressAt > 2f)))
			{
				_pressSet = null;
				_pressWin = -1;
			}
			if (_pressSet == set && _pressWin >= 0)
			{
				bool key = PlayInput.GetKey(KeyCode.E);
				if (!_pressAll && key && Time.unscaledTime - _pressAt >= 0.45f)
				{
					_pressAll = true;
					bool flag = !Bit(read(), set.Wins[_pressWin].Index);
					int num = read();
					Win[] wins = set.Wins;
					for (int i = 0; i < wins.Length; i++)
					{
						Win win = wins[i];
						num = With(num, win.Index, flag);
					}
					write(num);
					Sound(1f);
					Log.Info(string.Format("curtains: every window {0} ({1})", flag ? "drawn" : "opened", set.Wins.Length));
				}
				if (!key)
				{
					if (!_pressAll)
					{
						int index = set.Wins[_pressWin].Index;
						int mask = read();
						write(With(mask, index, !Bit(mask, index)));
						Sound(0.8f);
					}
					_pressSet = null;
					_pressWin = -1;
				}
			}
			int num2 = Aimed(set, instance.View);
			if (num2 < 0)
			{
				return null;
			}
			bool num3 = Bit(read(), set.Wins[num2].Index);
			if (PlayInput.GetKeyDown(KeyCode.E) && _pressWin < 0)
			{
				_pressSet = set;
				_pressWin = num2;
				_pressAt = Time.unscaledTime;
				_pressAll = false;
			}
			return (num3 ? "E:Open the curtains" : "E:Draw the curtains") + "|Hold E:Every window";
		}

		private static void Sound(float volume)
		{
			try
			{
				WorldBoxMod.Instance?.Audio?.PlayEffect("curtain", 0.55f * volume);
			}
			catch
			{
			}
		}

		private static int Aimed(RoomSet set, ViewState v)
		{
			float num = 1f / Mathf.Sqrt(1f + v.Pitch * v.Pitch);
			Vec3 vec = new Vec3(Mathf.Cos(v.Yaw) * num, Mathf.Sin(v.Yaw) * num, v.Pitch * num);
			Vec3 vec2 = new Vec3(v.X, v.Y, v.Z);
			int result = -1;
			float num2 = float.MaxValue;
			for (int i = 0; i < set.Wins.Length; i++)
			{
				Win win = set.Wins[i];
				float num3 = Vec3.Dot(vec, win.In);
				if (num3 > -0.15f)
				{
					continue;
				}
				Vec3 vec3 = new Vec3(win.Mid.X, win.Mid.Y, win.Top - win.Drop * 0.5f);
				float num4 = Vec3.Dot(vec3 - vec2, win.In) / num3;
				if (!(num4 < 0.1f) && !(num4 > 2.4f) && !(num4 > num2))
				{
					Vec3 a = vec2 + vec * num4 - vec3;
					if (!(Math.Abs(Vec3.Dot(a, win.R)) > win.RodHalf + 0.06f) && !(Math.Abs(a.Z) > win.Drop * 0.5f + 0.08f))
					{
						result = i;
						num2 = num4;
					}
				}
			}
			return result;
		}

		public static bool Witnesses(Kingdom k, Actor victim, Actor you, Vector2 at, float reach, List<Actor> into)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			HouseInterior houseInterior = ((instance != null) ? instance.Interior : null);
			if (houseInterior == null || !houseInterior.Inside || houseInterior.Virtual || houseInterior.House == null)
			{
				return false;
			}
			Building house = houseInterior.House;
			Rect rect = FirstPersonAudio.Footprint(house);
			if (at.x < rect.xMin - 0.5f || at.x > rect.xMax + 0.5f || at.y < rect.yMin - 0.5f || at.y > rect.yMax + 0.5f)
			{
				return false;
			}
			into.Clear();
			foreach (Actor item in Civil.SubjectsOf(k))
			{
				if (item != null && item != victim && item != you && item.isAlive() && item.isAdult())
				{
					bool flag = false;
					try
					{
						flag = item.is_inside_building && item.inside_building == house;
					}
					catch
					{
					}
					if (flag && !HouseOccupancy.Asleep(item))
					{
						into.Add(item);
					}
				}
			}
			Log.Info(string.Format("curtains: done in {0}{1}: only {2} in the room saw", house.asset.id, Private(houseInterior) ? " behind drawn curtains" : "", into.Count));
			return true;
		}

		private static bool Private(HouseInterior room)
		{
			if (room.DoorOpen > 0.05f)
			{
				return false;
			}
			if (!_rooms.TryGetValue(room.Room, out var value))
			{
				return false;
			}
			return AllDrawn(Mask(room.House), value);
		}

		internal static int MaskForTest(Building b)
		{
			return Mask(b);
		}

		internal static void SetMaskForTest(Building b, int mask)
		{
			SetMask(b, mask);
			Touched(b);
		}

		internal static void StampForTest(Building b, int stamp)
		{
			if (stamp == int.MinValue)
			{
				b.data.removeInt("fp_curtain_t");
			}
			else
			{
				b.data.set("fp_curtain_t", stamp);
			}
		}

		internal static void SetShipMaskForTest(Actor ship, int mask)
		{
			SetMask(ship, mask);
		}

		internal static int WindowsForTest(Interior room)
		{
			if (room == null || !_rooms.TryGetValue(room, out var value))
			{
				return -1;
			}
			return value.Wins.Length;
		}

		internal static float OpenForTest(Interior room, int i)
		{
			if (room == null || !_rooms.TryGetValue(room, out var value) || i < 0 || i >= value.Wins.Length)
			{
				return -1f;
			}
			if (!_shown.TryGetValue(value.Id, out var value2))
			{
				return -1f;
			}
			int index = value.Wins[i].Index;
			if (index < 0 || index >= value2.Open.Length)
			{
				return -1f;
			}
			return value2.Open[index];
		}

		internal static int AimedForTest(Interior room, ViewState v)
		{
			if (room == null || !_rooms.TryGetValue(room, out var value))
			{
				return -1;
			}
			return Aimed(value, v);
		}

		internal static void ForgetForTest()
		{
			_shown.Clear();
			_rooms.Clear();
			_street.Clear();
		}

		private static void Fail(string what, Exception e)
		{
			if (!(Time.unscaledTime - _failAt < 10f))
			{
				_failAt = Time.unscaledTime;
				Log.Error("curtains: " + what + ": " + e);
			}
		}

		private static HousePlan PlanSoon(MeshModel model)
		{
			if (model == null)
			{
				return null;
			}
			lock (_plans)
			{
				if (_plans.TryGetValue(model.Name, out var value))
				{
					return value;
				}
				if (!_measuring.Add(model.Name))
				{
					return null;
				}
			}
			SlowLane.Run(delegate
			{
				HousePlan value2 = null;
				try
				{
					value2 = HousePlan.Of(model);
				}
				catch
				{
				}
				lock (_plans)
				{
					_plans[model.Name] = value2;
					_measuring.Remove(model.Name);
				}
			});
			return null;
		}

		public static void Building(TownBuilding tb)
		{
			try
			{
				if (!tb.Seen || tb.Ruin || tb.Dist > 42f || tb.B == null || tb.Inst == null)
				{
					return;
				}
				Building b = tb.B;
				if ((tb.Scene.Collector != null && b == tb.Scene.Collector.SkipBuilding) || b.asset == null || HouseInterior.RoomName(b.asset) != "house")
				{
					return;
				}
				if (_errandHouse != null)
				{
					CheckErrands((WorldBoxMod.Instance != null) ? WorldBoxMod.Instance.Interior : null);
				}
				Routine(b, tb.Scene.Host, null);
				int num = Mask(b);
				long id = b.data.id;
				bool flag = num != 0;
				if (!flag && _shown.TryGetValue(id, out var value))
				{
					float[] open = value.Open;
					for (int i = 0; i < open.Length; i++)
					{
						if (open[i] > 0.01f)
						{
							flag = true;
							break;
						}
					}
				}
				if (!flag)
				{
					return;
				}
				HousePlan housePlan = PlanSoon(tb.Full);
				if (housePlan == null || housePlan.Windows.Count == 0)
				{
					return;
				}
				Shown shown = Animate(id, num, housePlan.Windows.Count);
				if (!_street.TryGetValue(id, out var value2) || value2.A.Length != housePlan.Windows.Count)
				{
					if (_street.Count > 160)
					{
						ForgetStreet();
					}
					value2 = new StreetSet
					{
						A = new MeshInstance[housePlan.Windows.Count],
						B = new MeshInstance[housePlan.Windows.Count],
						Cloth = ClothOf(b.kingdom, id)
					};
					_street[id] = value2;
				}
				value2.Seen = Time.unscaledTime;
				MeshInstance inst = tb.Inst;
				float glow = inst.Glow * 0.45f;
				for (int j = 0; j < housePlan.Windows.Count; j++)
				{
					float num2 = ((j < shown.Open.Length) ? shown.Open[j] : 0f);
					if (num2 < 0.02f)
					{
						continue;
					}
					HousePlan.Opening opening = housePlan.Windows[j];
					Vec3 vec = inst.Basis.Mul(new Vec3(opening.OutX, opening.OutY, 0f));
					float num3 = (float)Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y);
					if (num3 < 1E-05f)
					{
						continue;
					}
					vec = new Vec3(vec.X / num3, vec.Y / num3, 0f);
					float scale = tb.Scale;
					Vec3 vec2 = inst.Position + inst.Basis.Mul(new Vec3(opening.X, opening.Y, opening.Z1)) + vec * (0.012f * scale);
					float num4 = tb.Scene.View.X - vec2.X;
					float num5 = tb.Scene.View.Y - vec2.Y;
					if (num4 * vec.X + num5 * vec.Y <= 0f)
					{
						continue;
					}
					Vec3 vec3 = new Vec3(vec.Y, 0f - vec.X, 0f);
					float num6 = opening.HalfWidth * scale;
					float num7 = num6 + 0.01f;
					float num8 = (opening.Z1 - opening.Z0) * scale;
					if (!(num6 < 0.02f) && !(num8 < 0.05f))
					{
						float stretch;
						MeshModel model = CurtainMesh.Street(CurtainMesh.Step(Width(num2), out stretch));
						MeshInstance meshInstance = value2.A[j] ?? (value2.A[j] = StreetPanel(value2.Cloth));
						MeshInstance meshInstance2 = value2.B[j] ?? (value2.B[j] = StreetPanel(value2.Cloth));
						Vec3 c = new Vec3(0f, 0f, num8);
						float num9 = num7 * stretch;
						float num10 = num7 * 0.6f;
						meshInstance.Model = model;
						meshInstance.Basis = new Mat3(vec3 * num9, vec * num10, c);
						meshInstance.Position = vec2 - vec3 * num6;
						meshInstance2.Model = model;
						meshInstance2.Basis = new Mat3(vec3 * (0f - num9), vec * (0f - num10), c);
						meshInstance2.Position = vec2 + vec3 * num6;
						meshInstance.Glow = (meshInstance2.Glow = glow);
						meshInstance.SortDepth = (meshInstance2.SortDepth = tb.Dist);
						tb.Scene.Meshes.Add(meshInstance);
						tb.Scene.Meshes.Add(meshInstance2);
						if (_streetFrame != Time.frameCount)
						{
							_streetFrame = Time.frameCount;
							_streetCount = 0;
						}
						_streetCount += 2;
					}
				}
			}
			catch (Exception e)
			{
				Fail("street", e);
			}
		}

		private static MeshInstance StreetPanel(uint cloth)
		{
			MeshInstance meshInstance = new MeshInstance();
			meshInstance.Model = CurtainMesh.Street(0);
			meshInstance.Palette = new uint[1] { cloth };
			meshInstance.NoShadow = true;
			meshInstance.Tag = -1;
			return meshInstance;
		}

		private static void ForgetStreet()
		{
			float unscaledTime = Time.unscaledTime;
			List<long> list = new List<long>();
			foreach (KeyValuePair<long, StreetSet> item in _street)
			{
				if (unscaledTime - item.Value.Seen > 20f)
				{
					list.Add(item.Key);
				}
			}
			foreach (long item2 in list)
			{
				_street.Remove(item2);
			}
			if (_street.Count > 160)
			{
				_street.Clear();
			}
		}

		private static bool Errands(RoomSet set, bool draw)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			HouseInterior houseInterior = ((instance != null) ? instance.Interior : null);
			if (houseInterior == null || !houseInterior.Inside || houseInterior.Room != set.Room || set.House == null)
			{
				return false;
			}
			Actor actor = null;
			using (IEnumerator<Actor> enumerator = houseInterior.WalkersAwake.GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					actor = enumerator.Current;
				}
			}
			if (actor == null)
			{
				return false;
			}
			int mask = Mask(set.House);
			List<int> list = new List<int>();
			for (int i = 0; i < set.Wins.Length; i++)
			{
				if (Bit(mask, set.Wins[i].Index) != draw)
				{
					list.Add(i);
				}
			}
			if (list.Count == 0)
			{
				return false;
			}
			Finish();
			_errandHouse = set.House;
			_errandWant = (draw ? (-1) : 0);
			_errandUntil = Time.unscaledTime + 12f + 6f * (float)list.Count;
			if (Next(houseInterior, set, actor, list, draw))
			{
				Log.Info(string.Format("curtains: {0} goes round {1} windows to {2} them", Dialogue.NameOf(actor), list.Count + 1, draw ? "draw" : "open"));
				return true;
			}
			_errandHouse = null;
			return false;
		}

		private static bool Next(HouseInterior room, RoomSet set, Actor who, List<int> left, bool draw)
		{
			Vector2 vector = room.PositionOf(who);
			int index = 0;
			float num = float.MaxValue;
			for (int i = 0; i < left.Count; i++)
			{
				Win win = set.Wins[left[i]];
				float sqrMagnitude = (new Vector2(win.Mid.X, win.Mid.Y) - vector).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					index = i;
				}
			}
			Win w = set.Wins[left[index]];
			left.RemoveAt(index);
			Vector2 to = new Vector2(w.Mid.X + w.In.X * 0.38f, w.Mid.Y + w.In.Y * 0.38f);
			float faceYaw = Mathf.Atan2(0f - w.In.Y, 0f - w.In.X);
			Building house = set.House;
			return room.SendWalker(who, to, w.Level, faceYaw, delegate
			{
				if (_errandHouse == house)
				{
					SetMask(house, With(Mask(house), w.Index, draw));
					Sound(0.45f);
					if (left.Count == 0)
					{
						SetMask(house, _errandWant);
						_errandHouse = null;
					}
					else if (!Next(room, set, who, left, draw))
					{
						Finish();
					}
				}
			});
		}

		private static void Finish()
		{
			if (_errandHouse != null)
			{
				SetMask(_errandHouse, _errandWant);
				_errandHouse = null;
			}
		}

		private static void CheckErrands(HouseInterior room)
		{
			if (_errandHouse != null && (Time.unscaledTime > _errandUntil || room == null || !room.Inside || room.House != _errandHouse))
			{
				Finish();
			}
		}

		internal static bool DrawnOnMap(Building b, int day, float hour, Building yourHome)
		{
			try
			{
				if (b == null || b.data == null)
				{
					return false;
				}
				if (b == yourHome)
				{
					return Mask(b) != 0;
				}
				if (!b.hasResidents())
				{
					return Mask(b) != 0;
				}
				float num = (float)(TerrainModel.Hash((int)(b.data.id & 0xFFFFF), 77) & 0x3FF) / 1023f;
				bool flag = hour >= 0.77f + 0.06f * num || hour < 0.22f + 0.05f * num;
				b.data.get("fp_curtain_t", out var pResult, int.MinValue);
				if (pResult == day * 2 + (flag ? 1 : 0))
				{
					return Mask(b) != 0;
				}
				return flag;
			}
			catch
			{
				return false;
			}
		}
	}
}
