using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FirstPerson.Core;
using FirstPerson.Net;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class PlacedLights
	{
		public enum Kind
		{
			Torch,
			Wall,
			Lamp,
			Brazier
		}

		internal sealed class Light
		{
			public int Id;

			public Kind Kind;

			public float X;

			public float Y;

			public float Z;

			public float Heading;

			public long Owner = -1L;

			public long City = -1L;

			public long Building = -1L;

			public Vec3 Local;

			public Vec3 LocalOut;

			public string On;

			public int WallX = -1;

			public int WallY = -1;

			public readonly MeshInstance Mesh = new MeshInstance();

			public TorchFire Fire;

			public Vec3 FlameAt;

			public bool FlameKnown;

			public int PosedFrame = -1;

			public float Lonely;

			public bool Fresh;

			public bool OnBuilding => Building >= 0;

			public bool OnWallTile => WallX >= 0;
		}

		internal struct WallSpot
		{
			public Vec3 At;

			public float Heading;

			public long Building;

			public Vec3 Local;

			public Vec3 LocalOut;

			public string On;

			public int WallX;

			public int WallY;
		}

		private sealed class Seen
		{
			public MeshInstance Inst;

			public MeshModel Full;

			public int Frame;
		}

		public static readonly string[] ModelNames = new string[4] { "light_torch", "light_wall", "light_lamp", "light_brazier" };

		public static readonly string[] Names = new string[4] { "Standing torch", "Wall torch", "Lamp post", "Brazier" };

		public const float Scale = 0.5f;

		public const string Key = "fp_lights";

		public const int Most = 3000;

		private const float Apart = 0.55f;

		private static readonly float[] Bump = new float[4] { 0.08f, 0f, 0.09f, 0.19f };

		public static readonly List<Light> All = new List<Light>();

		private static int _nextId = 1;

		private static object _world;

		private static bool _read;

		private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

		private static readonly Dictionary<long, List<Light>> _onBuildings = new Dictionary<long, List<Light>>();

		private static int _sweep;

		private static float _sweepTimer;

		private static readonly float[] MapGlow = new float[4] { 0.4f, 0.32f, 0.5f, 0.62f };

		private static readonly float[] MapLift = new float[4] { 0.6f, 0.2f, 0.95f, 0.35f };

		private static bool _mapTried;

		internal static int MapGlowsForTest;

		private const float ModelRange = 48f;

		private const float FlameRange = 46f;

		private const float SmokeRange = 22f;

		private const float SkyRange = 150f;

		private const float SkyModelRange = 60f;

		internal const float Glow = 0.6f;

		private static int _frame;

		private static float _lastTime = -1f;

		internal static int DrawnForTest;

		internal static int FlamesForTest;

		internal static int LitForTest;

		private static readonly Dictionary<Building, Seen> _seen = new Dictionary<Building, Seen>();

		private static readonly List<Building> _stale = new List<Building>();

		private static readonly MeshInstance GhostMesh = new MeshInstance
		{
			NoShadow = true
		};

		private static readonly uint Green = (TerrainCache.Rgb(90, 230, 110) & 0xFFFFFF) | 0x96000000u;

		private static readonly uint Red = (TerrainCache.Rgb(240, 70, 60) & 0xFFFFFF) | 0x96000000u;

		private static readonly uint Taken = TerrainCache.Rgb(255, 240, 200) & 0xFFFFFF;

		public static int Version { get; private set; }

		public static string ItemOf(Kind k)
		{
			switch (k)
			{
			default:
				return "tw_item_torch";
			case Kind.Brazier:
				return "tw_item_brazier";
			case Kind.Lamp:
				return "tw_item_lamppost";
			}
		}

		public static void Track()
		{
			object obj = null;
			try
			{
				obj = World.world?.map_stats;
			}
			catch
			{
			}
			if (obj != _world)
			{
				_world = obj;
				_read = false;
				All.Clear();
				_onBuildings.Clear();
			}
			if (_read || obj == null)
			{
				return;
			}
			try
			{
				if (!Config.game_loaded || SmoothLoader.isLoading() || WorldBoxMod.WorldLoading)
				{
					return;
				}
			}
			catch
			{
				return;
			}
			_read = true;
			string pResult = null;
			try
			{
				World.world.map_stats.custom_data?.get("fp_lights", out pResult, null);
			}
			catch
			{
			}
			Parse(pResult);
			Log.Info($"lights: {All.Count} in this world");
		}

		private static void Parse(string raw)
		{
			All.Clear();
			_onBuildings.Clear();
			_nextId = 1;
			if (string.IsNullOrEmpty(raw))
			{
				return;
			}
			string[] array = raw.Split(';');
			for (int i = 0; i < array.Length; i++)
			{
				Light light = FromText(array[i]);
				if (light != null)
				{
					All.Add(light);
					if (light.Id >= _nextId)
					{
						_nextId = light.Id + 1;
					}
				}
			}
			Reindex();
		}

		private static string F2(float v)
		{
			return v.ToString("0.###", Inv);
		}

		private static void ToText(Light l, StringBuilder sb)
		{
			sb.Append(l.Id).Append(',').Append((int)l.Kind)
				.Append(',')
				.Append(F2(l.X))
				.Append(',')
				.Append(F2(l.Y))
				.Append(',')
				.Append(F2(l.Z))
				.Append(',')
				.Append(F2(l.Heading))
				.Append(',')
				.Append(l.Owner)
				.Append(',')
				.Append(l.City)
				.Append(',')
				.Append(l.Building)
				.Append(',')
				.Append(F2(l.Local.X))
				.Append(',')
				.Append(F2(l.Local.Y))
				.Append(',')
				.Append(F2(l.Local.Z))
				.Append(',')
				.Append(F2(l.LocalOut.X))
				.Append(',')
				.Append(F2(l.LocalOut.Y))
				.Append(',')
				.Append(F2(l.LocalOut.Z))
				.Append(',')
				.Append(l.On ?? "")
				.Append(',')
				.Append(l.WallX)
				.Append(',')
				.Append(l.WallY);
		}

		private static Light FromText(string s)
		{
			if (string.IsNullOrEmpty(s))
			{
				return null;
			}
			string[] array = s.Split(',');
			if (array.Length < 18)
			{
				return null;
			}
			try
			{
				Light light = new Light
				{
					Id = int.Parse(array[0], Inv),
					Kind = (Kind)Mathf.Clamp(int.Parse(array[1], Inv), 0, 3),
					X = float.Parse(array[2], Inv),
					Y = float.Parse(array[3], Inv),
					Z = float.Parse(array[4], Inv),
					Heading = float.Parse(array[5], Inv),
					Owner = long.Parse(array[6], Inv),
					City = long.Parse(array[7], Inv),
					Building = long.Parse(array[8], Inv),
					Local = new Vec3(float.Parse(array[9], Inv), float.Parse(array[10], Inv), float.Parse(array[11], Inv)),
					LocalOut = new Vec3(float.Parse(array[12], Inv), float.Parse(array[13], Inv), float.Parse(array[14], Inv)),
					On = ((array[15].Length > 0) ? array[15] : null),
					WallX = int.Parse(array[16], Inv),
					WallY = int.Parse(array[17], Inv)
				};
				if (float.IsNaN(light.X) || float.IsNaN(light.Y) || float.IsNaN(light.Z) || float.IsNaN(light.Heading))
				{
					return null;
				}
				return light;
			}
			catch
			{
				return null;
			}
		}

		public static string Text()
		{
			StringBuilder stringBuilder = new StringBuilder(All.Count * 48);
			for (int i = 0; i < All.Count; i++)
			{
				if (i > 0)
				{
					stringBuilder.Append(';');
				}
				ToText(All[i], stringBuilder);
			}
			return stringBuilder.ToString();
		}

		private static void Changed()
		{
			Version++;
			Reindex();
			try
			{
				MapStats mapStats = World.world?.map_stats;
				if (mapStats != null)
				{
					if (mapStats.custom_data == null)
					{
						mapStats.custom_data = new SaveCustomData();
					}
					mapStats.custom_data.set("fp_lights", Text());
				}
			}
			catch (Exception ex)
			{
				Log.Error("lights: could not keep them with the world: " + ex.Message);
			}
		}

		public static void FromHost(string raw)
		{
			Track();
			Dictionary<int, Light> dictionary = new Dictionary<int, Light>();
			foreach (Light item in All)
			{
				dictionary[item.Id] = item;
			}
			Parse(raw);
			foreach (Light item2 in All)
			{
				if (dictionary.TryGetValue(item2.Id, out var value))
				{
					item2.Fire = value.Fire;
					item2.FlameAt = value.FlameAt;
					item2.FlameKnown = value.FlameKnown;
				}
			}
			_read = true;
			Version++;
			try
			{
				MapStats mapStats = World.world?.map_stats;
				if (mapStats != null)
				{
					if (mapStats.custom_data == null)
					{
						mapStats.custom_data = new SaveCustomData();
					}
					mapStats.custom_data.set("fp_lights", raw ?? "");
				}
			}
			catch
			{
			}
		}

		private static void Reindex()
		{
			_onBuildings.Clear();
			foreach (Light item in All)
			{
				if (item.OnBuilding)
				{
					if (!_onBuildings.TryGetValue(item.Building, out var value))
					{
						value = (_onBuildings[item.Building] = new List<Light>(2));
					}
					value.Add(item);
				}
			}
		}

		public static string WhyNotAt(Vector2 at, Kind k)
		{
			Track();
			WorldTile worldTile;
			try
			{
				worldTile = World.world.GetTile(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y));
			}
			catch
			{
				worldTile = null;
			}
			if (worldTile == null || worldTile.Type == null)
			{
				return "Look at the ground";
			}
			try
			{
				if (worldTile.Type.lava)
				{
					return "Not in lava";
				}
				if (worldTile.Type.liquid || worldTile.Type.ocean)
				{
					return "Not on water";
				}
				if (OnTownWall(worldTile))
				{
					return "Not on a wall: hang a wall torch on it";
				}
				Building building = worldTile.building;
				if (building != null && building.asset != null && AssetStyle.IsSolid(building))
				{
					return (building.asset.building_type == BuildingType.Building_Tree) ? "A tree stands here" : "Something stands here";
				}
			}
			catch
			{
				return "Look at the ground";
			}
			if (All.Count >= 3000)
			{
				return "Too many lights in this world";
			}
			float num = 0.55f + ((k == Kind.Brazier) ? 0.2f : 0f);
			foreach (Light item in All)
			{
				if (item.Kind != Kind.Wall)
				{
					float num2 = item.X - at.x;
					float num3 = item.Y - at.y;
					if (num2 * num2 + num3 * num3 < num * num)
					{
						return "Too close to another light";
					}
				}
			}
			return null;
		}

		internal static bool OnTownWall(WorldTile t)
		{
			try
			{
				return t != null && t.Type != null && t.Type.wall;
			}
			catch
			{
				return false;
			}
		}

		public static int Carried(Actor a, Kind k)
		{
			if (a != null)
			{
				return Supplies.Count(a, ItemOf(k));
			}
			return 0;
		}

		public static string Lacks(Actor a, Kind k)
		{
			if (Carried(a, k) > 0)
			{
				return null;
			}
			Supplies.Kind kind = Supplies.Of(ItemOf(k));
			if (kind != null)
			{
				return "You have no " + kind.One + ". Make one in your bag (Tab): " + Recipe(kind);
			}
			return "You have none";
		}

		private static string Recipe(Supplies.Kind k)
		{
			string text = k.Amount1 + " " + ResName(k.Res1);
			if (k.Res2 == null)
			{
				return text;
			}
			return text + ", " + k.Amount2 + " " + ResName(k.Res2);
		}

		private static string ResName(string res)
		{
			if (!(res == "common_metals"))
			{
				return res;
			}
			return "metal";
		}

		public static City TownOf(Light l)
		{
			return Hub.TownAt(new Vector2(l.X, l.Y));
		}

		public static bool MayTake(Actor you, Light l)
		{
			if (you == null || l == null)
			{
				return false;
			}
			long id;
			try
			{
				id = you.data.id;
			}
			catch
			{
				return false;
			}
			if (l.Owner >= 0 && l.Owner == id)
			{
				return true;
			}
			if (Rep.Rules(you, TownOf(l)))
			{
				return true;
			}
			if (l.City >= 0)
			{
				try
				{
					City city = World.world.cities.get(l.City);
					if (city != null && city.isAlive() && Rep.Rules(you, city))
					{
						return true;
					}
				}
				catch
				{
				}
			}
			return false;
		}

		public static string Place(Actor you, Kind k, Vector2 at, float heading, WallSpot? wall)
		{
			Track();
			if (k == Kind.Wall && !wall.HasValue)
			{
				return "Aim at a wall";
			}
			string text = ((k != Kind.Wall) ? WhyNotAt(at, k) : ((All.Count >= 3000) ? "Too many lights in this world" : null));
			if (text != null)
			{
				return text;
			}
			text = Lacks(you, k);
			if (text != null)
			{
				return text;
			}
			if (WorldSync.ForwardLight(you, k, at, heading, wall))
			{
				Supplies.Take(you, ItemOf(k), 1);
				return null;
			}
			if (!Supplies.Take(you, ItemOf(k), 1))
			{
				return Lacks(you, k) ?? "You have none";
			}
			Light light = Add(you, k, at, heading, wall);
			Log.Info($"lights: {WorldBoxMod.Describe(you)} set down a {Names[(int)k].ToLowerInvariant()} at {light.X:F1},{light.Y:F1}" + (light.OnBuilding ? (" on building " + light.Building) : (light.OnWallTile ? " on a wall" : "")));
			return null;
		}

		internal static Light Add(Actor by, Kind k, Vector2 at, float heading, WallSpot? wall, City town = null)
		{
			Light light = new Light
			{
				Id = _nextId++,
				Kind = k,
				X = at.x,
				Y = at.y,
				Heading = heading
			};
			try
			{
				if (by != null)
				{
					light.Owner = by.data.id;
				}
			}
			catch
			{
			}
			try
			{
				if (town != null)
				{
					light.City = town.data.id;
				}
			}
			catch
			{
			}
			if (wall.HasValue)
			{
				WallSpot value = wall.Value;
				light.X = value.At.X;
				light.Y = value.At.Y;
				light.Z = value.At.Z;
				light.Heading = value.Heading;
				light.Building = value.Building;
				light.Local = value.Local;
				light.LocalOut = value.LocalOut;
				light.On = value.On;
				light.WallX = value.WallX;
				light.WallY = value.WallY;
			}
			light.Fresh = true;
			All.Add(light);
			Changed();
			if (by != null)
			{
				PlacedSound(light);
			}
			return light;
		}

		private static void PlacedSound(Light l)
		{
			try
			{
				if (Hub.Host != null && !((new Vector2(l.X, l.Y) - Hub.Host.current_position).sqrMagnitude > 900f))
				{
					Sounds.PlayAt((l.Kind == Kind.Lamp || l.Kind == Kind.Brazier) ? "event:/SFX/BUILDINGS/SpawnBuildingStone" : "event:/SFX/BUILDINGS/SpawnBuildingWood", new Vector2(l.X, l.Y));
				}
			}
			catch
			{
			}
		}

		public static string TakeBack(Actor you, Light l)
		{
			if (l != null && !All.Contains(l))
			{
				l = ById(l.Id);
			}
			if (l == null)
			{
				return "Look at a light";
			}
			if (!MayTake(you, l))
			{
				return "Not yours to take";
			}
			if (WorldSync.ForwardTakeLight(you, l))
			{
				Bag.Put(you, ItemOf(l.Kind), 1, spill: true, Hub.Toast);
				All.Remove(l);
				Reindex();
				return null;
			}
			Remove(l);
			Bag.Put(you, ItemOf(l.Kind), 1, spill: true, Hub.Toast);
			Log.Info($"lights: {WorldBoxMod.Describe(you)} took back a {Names[(int)l.Kind].ToLowerInvariant()} at {l.X:F1},{l.Y:F1}");
			return null;
		}

		internal static void Remove(Light l)
		{
			if (All.Remove(l))
			{
				Changed();
			}
		}

		internal static Light ById(int id)
		{
			foreach (Light item in All)
			{
				if (item.Id == id)
				{
					return item;
				}
			}
			return null;
		}

		public static Light Aimed(Vec3 eye, Vec3 ray, float reach, Func<float, float, float> ground)
		{
			Light result = null;
			float num = 0.3f;
			foreach (Light item in All)
			{
				Vec3 vec = Middle(item, ground) - eye;
				float num2 = vec.X * ray.X + vec.Y * ray.Y + vec.Z * ray.Z;
				if (!(num2 < 0.2f) && !(num2 > reach))
				{
					Vec3 vec2 = vec - ray * num2;
					float num3 = (float)Math.Sqrt(vec2.X * vec2.X + vec2.Y * vec2.Y + vec2.Z * vec2.Z);
					float num4 = ((item.Kind == Kind.Brazier) ? 0.32f : ((item.Kind == Kind.Wall) ? 0.22f : 0.2f));
					if (num3 - num4 < num)
					{
						num = num3 - num4;
						result = item;
					}
				}
			}
			return result;
		}

		private static Vec3 Middle(Light l, Func<float, float, float> ground)
		{
			if (l.Kind == Kind.Wall)
			{
				if (!l.FlameKnown)
				{
					return new Vec3(l.X, l.Y, l.Z + 0.1f);
				}
				return (new Vec3(l.X, l.Y, l.Z) + l.FlameAt) * 0.5f;
			}
			float num = 0f;
			try
			{
				num = ground?.Invoke(l.X, l.Y) ?? 0f;
			}
			catch
			{
			}
			float num2 = ((l.Kind == Kind.Lamp) ? 1.1f : ((l.Kind == Kind.Brazier) ? 0.35f : 0.7f));
			return new Vec3(l.X, l.Y, num + num2 * 0.6f);
		}

		public static bool FireNear(Vec3 flame, float reach)
		{
			foreach (Light item in All)
			{
				if (item.FlameKnown && item.Kind != Kind.Lamp)
				{
					float num = reach + ((item.Kind == Kind.Brazier) ? 0.18f : 0.05f);
					Vec3 vec = item.FlameAt + new Vec3(0f, 0f, 0.05f) - flame;
					if (vec.X * vec.X + vec.Y * vec.Y + vec.Z * vec.Z < num * num)
					{
						return true;
					}
				}
			}
			return false;
		}

		public static void Tick(float dt)
		{
			Track();
			HookMap();
			if (!_read || All.Count == 0 || WorldSync.Mirroring)
			{
				return;
			}
			_sweepTimer -= dt;
			if (_sweepTimer > 0f)
			{
				return;
			}
			_sweepTimer = 0.25f;
			int num = Mathf.Min(All.Count, 40);
			for (int i = 0; i < num; i++)
			{
				if (All.Count <= 0)
				{
					break;
				}
				_sweep = (_sweep + 1) % All.Count;
				Light light = All[_sweep];
				string text = Fallen(light, 0.25f * (float)All.Count / (float)num);
				if (text != null)
				{
					Remove(light);
					Log.Info($"lights: a {Names[(int)light.Kind].ToLowerInvariant()} at {light.X:F1},{light.Y:F1} fell: {text}");
					GiveBack(light, text);
					if (_sweep >= All.Count)
					{
						_sweep = 0;
					}
				}
			}
		}

		private static string Fallen(Light l, float since)
		{
			try
			{
				if (l.OnBuilding)
				{
					Building building = World.world.buildings.get(l.Building);
					if (building == null || !building.isAlive() || building.isRuin())
					{
						return "its wall came down";
					}
					return null;
				}
				if (l.OnWallTile)
				{
					if (!OnTownWall(World.world.GetTile(l.WallX, l.WallY)))
					{
						return "its wall came down";
					}
					return null;
				}
				WorldTile tile = World.world.GetTile(Mathf.FloorToInt(l.X), Mathf.FloorToInt(l.Y));
				if (tile == null || tile.Type == null)
				{
					return "off the map";
				}
				if (tile.Type.lava)
				{
					return "the lava took it";
				}
				if (tile.Type.liquid || tile.Type.ocean)
				{
					return "the water took it";
				}
				Building building2 = tile.building;
				if (building2 != null && building2.asset != null && building2.asset.building_type != BuildingType.Building_Tree && AssetStyle.IsSolid(building2))
				{
					return "a building went up over it";
				}
				if (l.Owner < 0)
				{
					City city = ((tile.zone != null) ? tile.zone.city : null);
					if (city == null || !city.isAlive())
					{
						l.Lonely += since;
						if (l.Lonely > 180f)
						{
							return "no town keeps it";
						}
					}
					else
					{
						l.Lonely = 0f;
					}
				}
			}
			catch
			{
			}
			return null;
		}

		private static void GiveBack(Light l, string why)
		{
			try
			{
				Actor host = Hub.Host;
				if (host != null && host.isAlive() && l.Owner == host.data.id && !((new Vector2(l.X, l.Y) - host.current_position).sqrMagnitude > 3600f))
				{
					Bag.Put(host, ItemOf(l.Kind), 1, spill: true, Hub.Toast);
					Hub.Toast?.Invoke("Your " + Names[(int)l.Kind].ToLowerInvariant() + " fell (" + why + "). It's back in your bag");
				}
			}
			catch
			{
			}
		}

		internal static void ReloadForTest()
		{
			_world = null;
			Track();
		}

		private static void HookMap()
		{
			if (_mapTried)
			{
				return;
			}
			QuantumSpriteAsset quantumSpriteAsset;
			try
			{
				quantumSpriteAsset = AssetManager.quantum_sprites?.get("draw_light_walls_light_blobs");
			}
			catch
			{
				return;
			}
			if (quantumSpriteAsset == null || quantumSpriteAsset.draw_call == null)
			{
				return;
			}
			_mapTried = true;
			try
			{
				QuantumSpriteUpdater game = quantumSpriteAsset.draw_call;
				quantumSpriteAsset.draw_call = delegate(QuantumSpriteAsset asset)
				{
					game(asset);
					MapGlows();
				};
				Log.Info("lights: they glow on the map with the game's night lights");
			}
			catch (Exception ex)
			{
				Log.Error("lights: no glow on the map: " + ex.Message);
			}
		}

		private static void MapGlows()
		{
			MapGlowsForTest = 0;
			if (All.Count == 0)
			{
				return;
			}
			try
			{
				if (!World.world.era_manager.shouldShowLights())
				{
					return;
				}
				List<LightBlobData> light_blobs = World.world.stack_effects.light_blobs;
				Camera camera = World.world.camera;
				Rect rect = default(Rect);
				bool flag = false;
				if (camera != null && camera.orthographic)
				{
					Vector3 position = camera.transform.position;
					float num = camera.orthographicSize + 2f;
					float num2 = num * camera.aspect + 2f;
					rect = new Rect(position.x - num2, position.y - num, num2 * 2f, num * 2f);
					flag = true;
				}
				foreach (Light item in All)
				{
					Vector2 vector = new Vector2(item.X, item.Y + MapLift[(int)item.Kind]);
					if (!flag || rect.Contains(vector))
					{
						light_blobs.Add(new LightBlobData
						{
							position = vector,
							radius = MapGlow[(int)item.Kind]
						});
						MapGlowsForTest++;
					}
				}
			}
			catch
			{
			}
		}

		public static void Collect(TownScene s)
		{
			Track();
			_frame = s.Frame;
			float unscaledTime = Time.unscaledTime;
			float dt = ((_lastTime < 0f) ? 0f : Mathf.Clamp(unscaledTime - _lastTime, 0f, 0.1f));
			_lastTime = unscaledTime;
			bool effectsPaused = GameClock.EffectsPaused;
			FxParticles fxPool = SceneCollector.FxPool;
			float num = 0.6f * Mathf.Clamp01((s.Night - 0.12f) / 0.4f);
			float x = s.View.X;
			float y = s.View.Y;
			float num2 = (s.FromSky ? 150f : 48f);
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			int count = s.Billboards.Count;
			Func<float, float, float> func = s.Ground;
			for (int i = 0; i < All.Count; i++)
			{
				Light light = All[i];
				if (light.OnBuilding && light.PosedFrame != _frame)
				{
					continue;
				}
				float num6 = light.X - x;
				float num7 = light.Y - y;
				float num8 = num6 * num6 + num7 * num7;
				if (num8 > num2 * num2 || !s.InView(light.X, light.Y, 1.5f))
				{
					continue;
				}
				MeshModel meshModel = Models.Get(ModelNames[(int)light.Kind]);
				if (meshModel == null)
				{
					continue;
				}
				MeshInstance mesh = light.Mesh;
				if (!light.OnBuilding)
				{
					float z = ((light.Kind == Kind.Wall) ? light.Z : (func(light.X, light.Y) - 0.02f));
					mesh.Model = meshModel;
					mesh.Place(light.X, light.Y, z, light.Heading, 0.5f, 0.5f, 0.5f);
				}
				float num9 = Mathf.Sqrt(num8);
				mesh.NoShadow = num9 > 30f;
				mesh.Static = true;
				mesh.Tag = -1;
				mesh.Flash = 0u;
				mesh.Fade = 1f;
				mesh.SortDepth = num9;
				if (light.Fire == null)
				{
					light.Fire = new TorchFire
					{
						Lit = true,
						Grown = 1f
					};
					if (light.Fresh)
					{
						light.Fire.Lit = false;
						light.Fire.Catch();
					}
				}
				light.Fresh = false;
				light.Fire.Lit = true;
				light.Fire.Pose(mesh, unscaledTime);
				light.FlameAt = light.Fire.Flame;
				light.FlameKnown = true;
				if (num9 < (s.FromSky ? 60f : 48f))
				{
					s.Meshes.Add(mesh);
					num3++;
				}
				if (num9 < (s.FromSky ? 150f : 46f))
				{
					light.Fire.AddFlames(s.Billboards, unscaledTime, num9 < 14f);
					num4++;
				}
				if (!effectsPaused && num9 < 22f)
				{
					light.Fire.Emit(fxPool, dt, unscaledTime, (num9 < 10f) ? 0.45f : 0.25f);
				}
				if (num > 0f)
				{
					light.Fire.Light(s.Lights, unscaledTime, num);
					num5++;
				}
				if (!s.FromSky && Bump[(int)light.Kind] > 0f && num8 < 400f)
				{
					Collider collider = new Collider
					{
						X = light.X,
						Y = light.Y,
						Radius = Bump[(int)light.Kind]
					};
					if (num8 < 144f)
					{
						Colliders.Near.Add(collider);
					}
					Steering.Add(collider, null);
				}
			}
			DrawnForTest = num3;
			FlamesForTest = s.Billboards.Count - count;
			LitForTest = num5;
			Ghost(s, unscaledTime);
		}

		public static void Building(TownBuilding tb)
		{
			if (tb.B == null || tb.Inst == null || tb.Full == null)
			{
				return;
			}
			if (tb.Dist < 16f && !tb.Ruin)
			{
				if (!_seen.TryGetValue(tb.B, out var value))
				{
					value = (_seen[tb.B] = new Seen());
				}
				value.Inst = tb.Inst;
				value.Full = tb.Full;
				value.Frame = tb.Scene.Frame;
				if (_seen.Count > 160)
				{
					Forget(tb.Scene.Frame);
				}
			}
			if (_onBuildings.Count == 0)
			{
				return;
			}
			long id;
			try
			{
				id = tb.B.data.id;
			}
			catch
			{
				return;
			}
			if (!_onBuildings.TryGetValue(id, out var value2))
			{
				return;
			}
			foreach (Light item in value2)
			{
				if (!(item.On != tb.Full.Name) || Reseat(item, tb.Full))
				{
					Vec3 vec = Anchors.World(tb.Inst, item.Local);
					Vec3 vec2 = tb.Inst.Basis.Mul(item.LocalOut);
					float num = Mathf.Atan2(vec2.Y, vec2.X);
					item.X = vec.X;
					item.Y = vec.Y;
					item.Z = vec.Z;
					item.Heading = num;
					item.Mesh.Model = Models.Get(ModelNames[1]);
					item.Mesh.Place(vec.X + Mathf.Cos(num) * 0.004f, vec.Y + Mathf.Sin(num) * 0.004f, vec.Z, num, 0.5f, 0.5f, 0.5f);
					item.PosedFrame = tb.Scene.Frame;
				}
			}
		}

		internal static Vec3? DrawnMiddleForTest(Building b)
		{
			if (b == null || !_seen.TryGetValue(b, out var value) || value.Inst == null || value.Full == null)
			{
				return null;
			}
			return Anchors.World(value.Inst, (value.Full.Min + value.Full.Max) * 0.5f);
		}

		private static void Forget(int frame)
		{
			_stale.Clear();
			foreach (KeyValuePair<Building, Seen> item in _seen)
			{
				if (frame - item.Value.Frame > 30)
				{
					_stale.Add(item.Key);
				}
			}
			foreach (Building item2 in _stale)
			{
				_seen.Remove(item2);
			}
		}

		private static bool Reseat(Light l, MeshModel now)
		{
			MeshModel meshModel = ((l.On != null) ? Models.Get(l.On) : null);
			Vec3 vec3;
			if (meshModel != null)
			{
				Vec3 vec = meshModel.Max - meshModel.Min;
				Vec3 vec2 = now.Max - now.Min;
				vec3 = new Vec3(now.Min.X + ((vec.X > 0.001f) ? ((l.Local.X - meshModel.Min.X) / vec.X) : 0.5f) * vec2.X, now.Min.Y + ((vec.Y > 0.001f) ? ((l.Local.Y - meshModel.Min.Y) / vec.Y) : 0.5f) * vec2.Y, l.Local.Z);
			}
			else
			{
				vec3 = l.Local;
			}
			Vec3 vec4 = new Vec3(l.LocalOut.X, l.LocalOut.Y, 0f).Normalized();
			float num = Math.Max(now.Max.X - now.Min.X, now.Max.Y - now.Min.Y) + 1f;
			for (int i = 0; i < 3; i++)
			{
				Vec3 vec5 = vec3 + vec4 * num;
				float z;
				switch (i)
				{
				default:
					z = 0f;
					break;
				case 2:
					z = 0.25f;
					break;
				case 1:
					z = -0.25f;
					break;
				}
				Vec3 vec6 = vec5 + new Vec3(0f, 0f, z);
				if (Ray(now, vec6, vec4 * -1f, num * 2f, out var best, out var normal) && !(Vec3.Dot(normal, vec4) < 0.5f) && !(Math.Abs(normal.Z) > 0.5f))
				{
					l.Local = vec6 + vec4 * (0f - best);
					l.LocalOut = new Vec3(normal.X, normal.Y, 0f).Normalized();
					l.On = now.Name;
					Changed();
					Log.Info("lights: a wall torch seated again on " + now.Name);
					return true;
				}
			}
			return false;
		}

		public static bool WallAt(Vec3 eye, Vec3 ray, Vec3 point, BaseSimObject picked, Func<float, float, float> ground, out WallSpot spot, out string why)
		{
			spot = default(WallSpot);
			why = "Aim at a house's wall or a town wall";
			if (picked is Building building && building.asset != null && building.asset.building_type != BuildingType.Building_Tree && _seen.TryGetValue(building, out var value) && value.Inst != null && value.Full != null && _frame - value.Frame <= 3)
			{
				MeshInstance inst = value.Inst;
				if (!ToLocal(inst, out var inv))
				{
					return false;
				}
				Vec3 vec = inv.Mul(eye - inst.Position);
				Vec3 vec2 = inv.Mul(ray);
				if (!Ray(value.Full, vec, vec2, 1000000f, out var best, out var normal))
				{
					return false;
				}
				if (Vec3.Dot(normal, vec2) > 0f)
				{
					normal *= -1f;
				}
				if (Math.Abs(normal.Z) > 0.55f)
				{
					why = ((normal.Z > 0f) ? "Aim at a wall, not the roof" : "Aim at a wall");
					return false;
				}
				Vec3 local = vec + vec2 * best;
				Vec3 at = Anchors.World(inst, local);
				Vec3 vec3 = inst.Basis.Mul(new Vec3(normal.X, normal.Y, 0f));
				long id;
				try
				{
					id = building.data.id;
				}
				catch
				{
					return false;
				}
				spot = new WallSpot
				{
					At = at,
					Heading = Mathf.Atan2(vec3.Y, vec3.X),
					Building = id,
					Local = local,
					LocalOut = new Vec3(normal.X, normal.Y, 0f).Normalized(),
					On = value.Full.Name,
					WallX = -1,
					WallY = -1
				};
				if (!HeightOk(at, ground, out why))
				{
					return true;
				}
				why = WallTaken(at);
				return true;
			}
			for (int i = 0; i < 3; i++)
			{
				Vec3 vec4 = point - ray * (0.02f * (float)i);
				WorldTile worldTile;
				try
				{
					worldTile = World.world.GetTile(Mathf.FloorToInt(vec4.X + ray.X * 0.05f), Mathf.FloorToInt(vec4.Y + ray.Y * 0.05f));
				}
				catch
				{
					worldTile = null;
				}
				if (OnTownWall(worldTile))
				{
					_ = worldTile.x;
					_ = worldTile.y;
					float heading = ((!(Math.Abs(ray.X) > Math.Abs(ray.Y))) ? ((ray.Y > 0f) ? (-MathF.PI / 2f) : (MathF.PI / 2f)) : ((ray.X > 0f) ? MathF.PI : 0f));
					Vec3 at2 = new Vec3(vec4.X, vec4.Y, vec4.Z);
					spot = new WallSpot
					{
						At = at2,
						Heading = heading,
						Building = -1L,
						WallX = worldTile.x,
						WallY = worldTile.y,
						On = null
					};
					if (!HeightOk(at2, ground, out why))
					{
						return true;
					}
					why = WallTaken(at2);
					return true;
				}
			}
			return false;
		}

		private static bool HeightOk(Vec3 at, Func<float, float, float> ground, out string why)
		{
			float num = 0f;
			try
			{
				num = ground(at.X, at.Y);
			}
			catch
			{
			}
			float num2 = at.Z - num;
			why = ((num2 < 0.38f) ? "Too low on the wall" : ((num2 > 1.6f) ? "Too high on the wall" : null));
			return why == null;
		}

		private static string WallTaken(Vec3 at)
		{
			if (All.Count >= 3000)
			{
				return "Too many lights in this world";
			}
			foreach (Light item in All)
			{
				if (item.Kind == Kind.Wall)
				{
					Vec3 vec = new Vec3(item.X, item.Y, item.Z) - at;
					if (vec.X * vec.X + vec.Y * vec.Y + vec.Z * vec.Z < 0.09f)
					{
						return "Too close to another torch";
					}
				}
			}
			return null;
		}

		private static bool ToLocal(MeshInstance inst, out Mat3 inv)
		{
			Mat3 basis = inst.Basis;
			Vec3 b = Vec3.Cross(basis.C1, basis.C2);
			Vec3 vec = Vec3.Cross(basis.C2, basis.C0);
			Vec3 vec2 = Vec3.Cross(basis.C0, basis.C1);
			float num = Vec3.Dot(basis.C0, b);
			inv = default(Mat3);
			if (Math.Abs(num) < 1E-09f)
			{
				return false;
			}
			float num2 = 1f / num;
			inv = new Mat3(new Vec3(b.X * num2, vec.X * num2, vec2.X * num2), new Vec3(b.Y * num2, vec.Y * num2, vec2.Y * num2), new Vec3(b.Z * num2, vec.Z * num2, vec2.Z * num2));
			return true;
		}

		internal static bool Ray(MeshModel m, Vec3 o, Vec3 d, float most, out float best, out Vec3 normal)
		{
			best = most;
			normal = default(Vec3);
			bool result = false;
			MeshPart[] parts = m.Parts;
			foreach (MeshPart obj in parts)
			{
				float[] pos = obj.Pos;
				int[] tri = obj.Tri;
				if (pos == null || tri == null)
				{
					continue;
				}
				for (int j = 0; j + 2 < tri.Length; j += 3)
				{
					int num = tri[j] * 3;
					int num2 = tri[j + 1] * 3;
					int num3 = tri[j + 2] * 3;
					float num4 = pos[num];
					float num5 = pos[num + 1];
					float num6 = pos[num + 2];
					float num7 = pos[num2] - num4;
					float num8 = pos[num2 + 1] - num5;
					float num9 = pos[num2 + 2] - num6;
					float num10 = pos[num3] - num4;
					float num11 = pos[num3 + 1] - num5;
					float num12 = pos[num3 + 2] - num6;
					float num13 = d.Y * num12 - d.Z * num11;
					float num14 = d.Z * num10 - d.X * num12;
					float num15 = d.X * num11 - d.Y * num10;
					float num16 = num7 * num13 + num8 * num14 + num9 * num15;
					if (num16 > -1E-09f && num16 < 1E-09f)
					{
						continue;
					}
					float num17 = 1f / num16;
					float num18 = o.X - num4;
					float num19 = o.Y - num5;
					float num20 = o.Z - num6;
					float num21 = (num18 * num13 + num19 * num14 + num20 * num15) * num17;
					if (num21 < 0f || num21 > 1f)
					{
						continue;
					}
					float num22 = num19 * num9 - num20 * num8;
					float num23 = num20 * num7 - num18 * num9;
					float num24 = num18 * num8 - num19 * num7;
					float num25 = (d.X * num22 + d.Y * num23 + d.Z * num24) * num17;
					if (!(num25 < 0f) && !(num21 + num25 > 1f))
					{
						float num26 = (num10 * num22 + num11 * num23 + num12 * num24) * num17;
						if (!(num26 <= 0.0001f) && !(num26 >= best))
						{
							best = num26;
							normal = Vec3.Cross(new Vec3(num7, num8, num9), new Vec3(num10, num11, num12)).Normalized();
							result = true;
						}
					}
				}
			}
			return result;
		}

		private static void Ghost(TownScene s, float time)
		{
			if (!Planning.LightAiming)
			{
				return;
			}
			Light aimedLight = Planning.AimedLight;
			if (aimedLight != null && All.Contains(aimedLight) && (!aimedLight.OnBuilding || aimedLight.PosedFrame == _frame) && MayTake(Hub.Host, aimedLight))
			{
				uint num = (uint)Mathf.Clamp(26f + 30f * Mathf.Sin(time * 5f), 0f, 255f);
				aimedLight.Mesh.Flash = Taken | (num << 24);
				aimedLight.Mesh.Static = false;
			}
			if (Planning.LightGhostShown)
			{
				MeshModel meshModel = Models.Get(ModelNames[(int)Planning.LightKind]);
				if (meshModel != null)
				{
					Vec3 lightAt = Planning.LightAt;
					GhostMesh.Model = meshModel;
					GhostMesh.Place(lightAt.X, lightAt.Y, lightAt.Z, Planning.LightHeading, 0.5f, 0.5f, 0.5f);
					GhostMesh.Fade = 0.55f;
					GhostMesh.Flash = (Planning.LightValid ? Green : Red);
					GhostMesh.NoShadow = true;
					GhostMesh.Static = false;
					GhostMesh.Palette = null;
					GhostMesh.SortDepth = Mathf.Sqrt(s.Dist2(lightAt.X, lightAt.Y));
					s.Meshes.Add(GhostMesh);
				}
			}
		}
	}
}
