using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class SizesTest : MonoBehaviour
	{
		private sealed class Near
		{
			public Building B;

			public MeshInstance Mesh;

			public string Family;

			public string Look;

			public string Route;

			public float Scale;

			public float Width;

			public float Height;

			public float Depth;

			public float Offset;

			public float Radius;

			public bool Solid;

			public SpriteImage Image;
		}

		private sealed class Kind
		{
			public string Label;

			public string Id;

			public int Sex;

			public bool Adult;

			public Kind(string label, string id, int sex, bool adult)
			{
				Label = label;
				Id = id;
				Sex = sex;
				Adult = adult;
			}
		}

		private sealed class Job
		{
			public string Id;

			public string Family;

			public string Note;
		}

		private sealed class Level
		{
			public int Count;

			public float MinX = float.MaxValue;

			public float MinY = float.MaxValue;

			public float MaxX = float.MinValue;

			public float MaxY = float.MinValue;

			public float SumX;

			public float SumY;

			public float NearDoor = float.MaxValue;

			public float ExX;

			public float ExY;

			public float ExZ;

			public float ExDist = float.MaxValue;

			public float MinNeed = float.MaxValue;

			public readonly Dictionary<byte, int> What = new Dictionary<byte, int>();
		}

		private sealed class Spot
		{
			public int Cell;

			public float Z;

			public byte What;

			public float Need = float.MaxValue;
		}

		private sealed class Heap
		{
			private readonly List<float> _k = new List<float>();

			private readonly List<int> _v = new List<int>();

			public int Count => _k.Count;

			public void Push(float k, int v)
			{
				_k.Add(k);
				_v.Add(v);
				int num = _k.Count - 1;
				while (num > 0)
				{
					int num2 = (num - 1) / 2;
					if (!(_k[num2] <= _k[num]))
					{
						Swap(num, num2);
						num = num2;
						continue;
					}
					break;
				}
			}

			public int Pop(out float k)
			{
				k = _k[0];
				int result = _v[0];
				int index = _k.Count - 1;
				_k[0] = _k[index];
				_v[0] = _v[index];
				_k.RemoveAt(index);
				_v.RemoveAt(index);
				int num = 0;
				while (true)
				{
					int num2 = num * 2 + 1;
					int num3 = num2 + 1;
					int num4 = num;
					if (num2 < _k.Count && _k[num2] < _k[num4])
					{
						num4 = num2;
					}
					if (num3 < _k.Count && _k[num3] < _k[num4])
					{
						num4 = num3;
					}
					if (num4 == num)
					{
						break;
					}
					Swap(num, num4);
					num = num4;
				}
				return result;
			}

			private void Swap(int a, int b)
			{
				float value = _k[a];
				_k[a] = _k[b];
				_k[b] = value;
				int value2 = _v[a];
				_v[a] = _v[b];
				_v[b] = value2;
			}
		}

		private WorldBoxMod _mod;

		private Actor _king;

		private Actor _man;

		private WorldTile _home;

		private WorldTile _spot;

		private List<Vector2Int> _sites;

		private readonly List<string> _bodyRows = new List<string>();

		private readonly List<string> _buildingRows = new List<string>();

		private static readonly string[] Races = new string[4] { "human", "elf", "orc", "dwarf" };

		private static FieldInfo _fBuildings;

		private static FieldInfo _fWalls;

		private static FieldInfo _fLibrary;

		private static FieldInfo _fInfo;

		private static FieldInfo _nbBuilding;

		private static FieldInfo _nbMesh;

		private static FieldInfo _nbFamily;

		private static FieldInfo _nbScale;

		private static FieldInfo _nbWidth;

		private static FieldInfo _nbHeight;

		private static FieldInfo _nbDepth;

		private static FieldInfo _nbOffset;

		private static FieldInfo _nbRadius;

		private static FieldInfo _nbSolid;

		private static FieldInfo _nbLook;

		private static FieldInfo _nbRoute;

		private static FieldInfo _nbImage;

		private static readonly Kind[] Kinds = new Kind[21]
		{
			new Kind("human man", "human", 1, adult: true),
			new Kind("human woman", "human", 2, adult: true),
			new Kind("elf man", "elf", 1, adult: true),
			new Kind("elf woman", "elf", 2, adult: true),
			new Kind("orc man", "orc", 1, adult: true),
			new Kind("orc woman", "orc", 2, adult: true),
			new Kind("dwarf man", "dwarf", 1, adult: true),
			new Kind("dwarf woman", "dwarf", 2, adult: true),
			new Kind("human child", "human", 0, adult: false),
			new Kind("wolf", "wolf", 0, adult: true),
			new Kind("bear", "bear", 0, adult: true),
			new Kind("sheep", "sheep", 0, adult: true),
			new Kind("cow", "cow", 0, adult: true),
			new Kind("chicken", "chicken", 0, adult: true),
			new Kind("cat", "cat", 0, adult: true),
			new Kind("dog", "dog", 0, adult: true),
			new Kind("fox", "fox", 0, adult: true),
			new Kind("rabbit", "rabbit", 0, adult: true),
			new Kind("buffalo", "buffalo", 0, adult: true),
			new Kind("rhino", "rhino", 0, adult: true),
			new Kind("crocodile", "crocodile", 0, adult: true)
		};

		private static readonly string[] WallTypes = new string[7] { "wall_order", "wall_ancient", "wall_evil", "wall_iron", "wall_green", "wall_light", "wall_wild" };

		private string _forceId;

		private string _force;

		private WorldTile _forceTile;

		private static readonly string[] KindNames = new string[20]
		{
			"plain", "leaves", "bark", "wood", "stone", "roof", "thatch", "cloth", "skin", "hair",
			"metal", "fire", "glass", "plaster", "rock", "eyes", "ancient", "obsidian", "plates", "marble"
		};

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(path);
			bool quit = options.Contains("quit");
			while (!Config.game_loaded || World.world == null || World.world.units == null)
			{
				yield return null;
			}
			for (float t = 0f; t < 60f; t += 1f)
			{
				bool flag = false;
				try
				{
					flag = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag && t >= 4f)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(1f);
			}
			Config.autosaves = false;
			_mod = WorldBoxMod.Instance;
			float eyeHeight = _mod.Settings.EyeHeight;
			_mod.Settings.EyeHeight = 1f;
			Log.Info(string.Format("SIZES world {0}x{1}, models {2}, eye height setting {3:F2} -> 1", MapBox.width, MapBox.height, Models.Ready ? "ready" : "missing", eyeHeight));
			LogClock();
			Reflect();
			foreach (WorldTile item in KingTest.GoodSites())
			{
				_king = KingTest.FoundKingdom(item, "human", 4, 0);
				if (_king != null)
				{
					break;
				}
			}
			if (_king == null)
			{
				Log.Error("SIZES could not found a village");
				Finish(quit);
				yield break;
			}
			_home = _king.current_tile;
			yield return new WaitForSecondsRealtime(1.5f);
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("SIZES first person did not start");
				Finish(quit);
				yield break;
			}
			_mod.HintUntil = 0f;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Invincible(_king);
			Log.Info($"SIZES village at {_home.x},{_home.y}");
			bool all = !options.Contains("sz_");
			if (all || options.Contains("sz_rom"))
			{
				yield return Run("rooms", Rooms());
			}
			if (all || options.Contains("sz_bod") || options.Contains("sz_jmp"))
			{
				yield return Run("bodies", Bodies(!all && !options.Contains("sz_bod")));
			}
			if (all || options.Contains("sz_jmp"))
			{
				yield return Run("jumps", HumanJumps());
			}
			if (all || options.Contains("sz_wal") || options.Contains("sz_bld"))
			{
				for (float t = 0f; t < 20f; t += 0.5f)
				{
					if (_mod.TerrainForTest != null && _mod.TerrainForTest.Width >= MapBox.width)
					{
						break;
					}
					yield return Wait(0.5f);
				}
				yield return Wait(2f);
				Log.Info($"SIZES land read: {_mod.TerrainForTest?.Width}x{_mod.TerrainForTest?.Height}");
				int[] array = new int[4] { 18, 15, 13, 11 };
				foreach (int num in array)
				{
					_sites = FlatSites(3, num, 16, 30);
					Log.Info($"SIZES flat sites of {num} tiles: {_sites.Count}");
					if (_sites.Count >= 2)
					{
						break;
					}
				}
				foreach (Vector2Int site in _sites)
				{
					Log.Info($"SIZES flat site {site.x},{site.y}: ground within {Rise(site.x - 8, site.y - 8, 17, 17):F2} tiles");
				}
			}
			if (all || options.Contains("sz_wal"))
			{
				yield return Run("walls", Walls());
			}
			if (all || options.Contains("sz_bld"))
			{
				yield return Run("buildings", Buildings());
			}
			if (all || options.Contains("sz_nat"))
			{
				yield return Run("nature", Nature());
			}
			Summary();
			TownModels.PickForTest = null;
			Finish(quit);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("SIZES --- " + name);
			yield return Guard(name, part);
		}

		private IEnumerator Guard(string name, IEnumerator part)
		{
			Stack<IEnumerator> stack = new Stack<IEnumerator>();
			stack.Push(part);
			bool failed = false;
			while (stack.Count > 0 && !failed)
			{
				IEnumerator enumerator = stack.Peek();
				object obj = null;
				bool flag = false;
				try
				{
					if (!enumerator.MoveNext())
					{
						flag = true;
					}
					else
					{
						obj = enumerator.Current;
					}
				}
				catch (Exception ex)
				{
					Log.Error("SIZES " + name + " failed: " + ex);
					failed = true;
				}
				if (failed)
				{
					break;
				}
				if (flag)
				{
					stack.Pop();
				}
				else if (obj is IEnumerator item)
				{
					stack.Push(item);
				}
				else
				{
					yield return obj;
				}
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
			PossessionHooks.ScriptedJump = false;
		}

		private static void Finish(bool quit)
		{
			TownModels.PickForTest = null;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("SIZES DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private static WaitForSecondsRealtime Wait(float s)
		{
			return new WaitForSecondsRealtime(s);
		}

		private void LogClock()
		{
			float num = 1f;
			float num2 = 0f;
			float num3 = 0f;
			int num4 = 1;
			bool flag = false;
			try
			{
				num = Config.time_scale_asset.multiplier;
				num4 = Config.time_scale_asset.ticks;
			}
			catch
			{
			}
			try
			{
				flag = Config.fps_lock_30;
			}
			catch
			{
			}
			try
			{
				num2 = SimGlobals.m.gravity;
				num3 = SimGlobals.m.unit_speed_multiplier;
			}
			catch
			{
			}
			float num5 = 0f;
			float num6 = 0f;
			try
			{
				num5 = World.world.elapsed;
				num6 = World.world.fixed_delta_time;
			}
			catch
			{
			}
			Log.Info($"SIZES clock: Time.fixedDeltaTime {Time.fixedDeltaTime:F4} s, time scale x{num} ({num4} sim ticks a frame), fps lock 30 {flag}, target frame rate {Application.targetFrameRate}, vsync {QualitySettings.vSyncCount}, " + $"world elapsed {num5:F4} / fixed_delta_time {num6:F4} a tick, gravity {num2}, unit speed multiplier {num3}; people scale (Creatures.RealScale) {0.5714286f:F4} tiles/m");
		}

		private static void Reflect()
		{
			_fBuildings = typeof(SceneCollector).GetField("_buildings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			_fWalls = typeof(SceneCollector).GetField("_wallMeshes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			Type nestedType = typeof(SceneCollector).GetNestedType("NearBuilding", BindingFlags.Public | BindingFlags.NonPublic);
			if (nestedType != null)
			{
				_nbBuilding = nestedType.GetField("Building", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_nbMesh = nestedType.GetField("Mesh", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_nbFamily = nestedType.GetField("MeshFamily", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_nbScale = nestedType.GetField("ModelScale", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_nbWidth = nestedType.GetField("Width", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_nbHeight = nestedType.GetField("Height", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_nbDepth = nestedType.GetField("DepthTiles", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_nbOffset = nestedType.GetField("CenterOffset", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_nbRadius = nestedType.GetField("Radius", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_nbSolid = nestedType.GetField("Solid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_nbLook = nestedType.GetField("Look", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_nbRoute = nestedType.GetField("Route", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				_nbImage = nestedType.GetField("Image", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			_fLibrary = typeof(Models).GetField("_library", BindingFlags.Static | BindingFlags.NonPublic);
			_fInfo = typeof(MeshModel).GetField("_info", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			Log.Info($"SIZES reflection: scene buildings {_fBuildings != null}, near building {nestedType != null && _nbMesh != null && _nbBuilding != null}, wall pieces {_fWalls != null}, model library {_fLibrary != null}, model info {_fInfo != null}");
		}

		private static T Get<T>(FieldInfo f, object o, T fallback)
		{
			try
			{
				return (f != null && o != null && f.GetValue(o) is T val) ? val : fallback;
			}
			catch
			{
				return fallback;
			}
		}

		private static Near Read(object o)
		{
			return new Near
			{
				B = Get<Building>(_nbBuilding, o, null),
				Mesh = Get<MeshInstance>(_nbMesh, o, null),
				Family = Get<string>(_nbFamily, o, null),
				Scale = Get(_nbScale, o, 0f),
				Width = Get(_nbWidth, o, 0f),
				Height = Get(_nbHeight, o, 0f),
				Depth = Get(_nbDepth, o, 0f),
				Offset = Get(_nbOffset, o, 0f),
				Radius = Get(_nbRadius, o, 0f),
				Solid = Get(_nbSolid, o, fallback: false),
				Look = ((!(_nbLook != null)) ? "?" : _nbLook.GetValue(o)?.ToString()),
				Route = ((!(_nbRoute != null)) ? "?" : _nbRoute.GetValue(o)?.ToString()),
				Image = Get<SpriteImage>(_nbImage, o, null)
			};
		}

		private List<Near> Nears()
		{
			List<Near> list = new List<Near>();
			if (!(_fBuildings?.GetValue(_mod.SceneForTest) is IList list2))
			{
				return list;
			}
			for (int i = 0; i < list2.Count; i++)
			{
				if (list2[i] != null)
				{
					list.Add(Read(list2[i]));
				}
			}
			return list;
		}

		private Near NearOf(Building b)
		{
			if (b == null || !(_fBuildings?.GetValue(_mod.SceneForTest) is IList list))
			{
				return null;
			}
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i] != null && Get<Building>(_nbBuilding, list[i], null) == b)
				{
					return Read(list[i]);
				}
			}
			return null;
		}

		private static Dictionary<string, MeshModel> Library()
		{
			return _fLibrary?.GetValue(null) as Dictionary<string, MeshModel>;
		}

		private static string InfoOf(MeshModel m, int max = 14)
		{
			if (m == null || !(_fInfo?.GetValue(m) is Dictionary<string, string> dictionary) || dictionary.Count == 0)
			{
				return "";
			}
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, string> item in dictionary)
			{
				if (list.Count >= max)
				{
					break;
				}
				if (!item.Key.StartsWith("a_", StringComparison.Ordinal))
				{
					string text = item.Value ?? "";
					list.Add(item.Key + "=" + ((text.Length > 36) ? (text.Substring(0, 36) + "...") : text));
				}
			}
			return string.Join(" ", list);
		}

		private float Ground(Vector2 p)
		{
			return _mod.SamplerForTest.Cache.SampleHeight(p.x, p.y);
		}

		private float Rise(int x0, int y0, int w, int h)
		{
			float num = float.MaxValue;
			float num2 = float.MinValue;
			for (int i = y0; i < y0 + h; i++)
			{
				for (int j = x0; j < x0 + w; j++)
				{
					float b = Ground(new Vector2((float)j + 0.5f, (float)i + 0.5f));
					num = Mathf.Min(num, b);
					num2 = Mathf.Max(num2, b);
				}
			}
			return num2 - num;
		}

		private static bool DryTile(WorldTile t)
		{
			if (t != null && t.Type != null && !t.Type.liquid && !t.Type.block && !t.Type.lava)
			{
				return t.Type.ground;
			}
			return false;
		}

		private static bool Clearable(Building b)
		{
			BuildingType buildingType = ((b.asset == null) ? BuildingType.Building_Tree : b.asset.building_type);
			if (buildingType != BuildingType.Building_Tree && buildingType != BuildingType.Building_Mineral)
			{
				return buildingType == BuildingType.Building_Plant;
			}
			return true;
		}

		private static bool Open(WorldTile t, int r)
		{
			for (int i = -r; i <= r; i++)
			{
				for (int j = -r; j <= r; j++)
				{
					WorldTile tile = World.world.GetTile(t.x + j, t.y + i);
					if (!DryTile(tile) || (tile.building != null && !Clearable(tile.building)))
					{
						return false;
					}
				}
			}
			return true;
		}

		private static void ClearBox(int x0, int y0, int w, int h)
		{
			for (int i = y0; i < y0 + h; i++)
			{
				for (int j = x0; j < x0 + w; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile != null && tile.building != null && Clearable(tile.building))
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

		private List<Vector2Int> FlatSites(int count, int size, int fromHome, int apart)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			List<(Vector2Int, float)> list = new List<(Vector2Int, float)>();
			int num = size / 2;
			for (int i = num + 2; i < terrainForTest.Height - num - 2; i += 3)
			{
				for (int j = num + 2; j < terrainForTest.Width - num - 2; j += 3)
				{
					float num2 = Mathf.Sqrt((j - _home.x) * (j - _home.x) + (i - _home.y) * (i - _home.y));
					if (num2 < (float)fromHome)
					{
						continue;
					}
					float num3 = float.MaxValue;
					float num4 = float.MinValue;
					bool flag = false;
					for (int k = -num; k <= num; k++)
					{
						if (flag)
						{
							break;
						}
						for (int l = -num; l <= num; l++)
						{
							if (flag)
							{
								break;
							}
							int num5 = (i + k) * terrainForTest.Width + j + l;
							if ((terrainForTest.Flags[num5] & 0xB) != 0)
							{
								flag = true;
								break;
							}
							WorldTile tile = World.world.GetTile(j + l, i + k);
							if (!DryTile(tile) || (tile.building != null && !Clearable(tile.building)))
							{
								flag = true;
								break;
							}
							float b = terrainForTest.Ground[num5];
							num3 = Mathf.Min(num3, b);
							num4 = Mathf.Max(num4, b);
						}
					}
					if (!flag)
					{
						list.Add((new Vector2Int(j, i), num4 - num3 + num2 * 0.002f));
					}
				}
			}
			list.Sort(((Vector2Int at, float score) a, (Vector2Int at, float score) tuple) => a.score.CompareTo(tuple.score));
			List<Vector2Int> list2 = new List<Vector2Int>();
			foreach (var item in list)
			{
				if (list2.Count >= count)
				{
					break;
				}
				bool flag2 = true;
				foreach (Vector2Int item2 in list2)
				{
					if ((item2 - item.Item1).magnitude < (float)apart)
					{
						flag2 = false;
						break;
					}
				}
				if (flag2)
				{
					list2.Add(item.Item1);
				}
			}
			return list2;
		}

		private static void Invincible(Actor a)
		{
			try
			{
				((BaseSimObject)a).addStatusEffect("invincible", 3000f, pColorEffect: false);
			}
			catch
			{
			}
		}

		private static void Remove(Actor a)
		{
			if (a == null)
			{
				return;
			}
			try
			{
				if (a.isAlive())
				{
					a.finishStatusEffect("invincible");
					a.dieSimpleNone();
				}
			}
			catch
			{
			}
		}

		private void Retire(Actor a)
		{
			if (a != null)
			{
				if (a == _man || a == _king)
				{
					Place(a, new Vector2((float)_home.x + 0.5f, (float)_home.y + 0.5f));
				}
				else
				{
					Remove(a);
				}
			}
		}

		private static void Place(Actor a, Vector2 at)
		{
			try
			{
				a.current_position = at;
				a.dirty_current_tile = true;
			}
			catch
			{
			}
		}

		private float RawHeight(Actor a)
		{
			SpriteImage spriteImage = _mod.SpriteForTest(a);
			if (spriteImage == null)
			{
				return 0f;
			}
			return spriteImage.UnitHeight * spriteImage.VisibleFraction * Mathf.Abs(a.current_scale.y);
		}

		private static string Traits(Actor a)
		{
			List<string> list = new List<string>();
			try
			{
				foreach (ActorTrait trait in a.getTraits())
				{
					if (trait != null)
					{
						list.Add(trait.id);
					}
				}
			}
			catch
			{
			}
			return string.Join(",", list);
		}

		private Actor Spawn(Kind k, WorldTile at, out string how)
		{
			how = "";
			Actor actor = null;
			Actor actor2 = null;
			List<Actor> list = new List<Actor>();
			for (int i = 0; i < 10; i++)
			{
				if (actor != null)
				{
					break;
				}
				Actor actor3 = null;
				try
				{
					actor3 = World.world.units.spawnNewUnit(k.Id, at, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, k.Adult);
				}
				catch (Exception ex)
				{
					Log.Error("SIZES spawn " + k.Id + ": " + ex.Message);
					break;
				}
				if (actor3 == null)
				{
					break;
				}
				bool flag = false;
				try
				{
					flag = actor3.isSexFemale();
				}
				catch
				{
				}
				if (k.Sex == 0 || k.Sex == 2 == flag)
				{
					actor = actor3;
					how = $"spawn {i + 1}";
				}
				else
				{
					list.Add(actor3);
					actor2 = actor3;
				}
			}
			if (actor == null && actor2 != null)
			{
				actor = actor2;
				list.Remove(actor2);
				try
				{
					actor.data.sex = ((k.Sex == 2) ? ActorSex.Female : ActorSex.Male);
				}
				catch
				{
				}
				how = "sex set by hand";
			}
			foreach (Actor item in list)
			{
				Remove(item);
			}
			return actor;
		}

		private IEnumerator Bodies(bool manOnly)
		{
			WorldTile worldTile = null;
			for (int i = 18; i < 120; i += 3)
			{
				if (worldTile != null)
				{
					break;
				}
				for (int j = 0; j < 48; j++)
				{
					if (worldTile != null)
					{
						break;
					}
					WorldTile tile = World.world.GetTile(_home.x + (int)(Mathf.Cos((float)j * 0.1309f) * (float)i), _home.y + (int)(Mathf.Sin((float)j * 0.1309f) * (float)i));
					if (tile != null && Open(tile, 3) && !(Rise(tile.x - 3, tile.y - 3, 7, 7) > 0.3f))
					{
						worldTile = tile;
					}
				}
			}
			if (worldTile == null)
			{
				Log.Error("SIZES no open flat land for the bodies");
				yield break;
			}
			_spot = worldTile;
			ClearBox(worldTile.x - 5, worldTile.y - 5, 11, 11);
			Log.Info($"SIZES body spot {worldTile.x},{worldTile.y}, ground within {Rise(worldTile.x - 3, worldTile.y - 3, 7, 7):F2} tiles over 7x7");
			Kind[] kinds = Kinds;
			foreach (Kind kind in kinds)
			{
				if (!manOnly || !(kind.Label != "human man"))
				{
					yield return Guard("body " + kind.Label, Body(kind));
				}
			}
			if (!manOnly)
			{
				Dragon();
			}
		}

		private IEnumerator Body(Kind k)
		{
			Vector2 at = new Vector2((float)_spot.x + 0.5f, (float)_spot.y + 0.5f);
			WorldTile at2 = World.world.GetTile(_spot.x + 2, _spot.y) ?? _spot;
			string how;
			Actor a = Spawn(k, at2, out how);
			if (a == null)
			{
				Log.Error("SIZES body " + k.Label + ": could not spawn a " + k.Id);
				yield break;
			}
			Invincible(a);
			yield return Wait(0.4f);
			Actor previous = _mod.Host;
			bool possessed = _mod.Host == a || Possess.JumpTo(a);
			if (possessed)
			{
				yield return Wait(1.6f);
			}
			possessed = possessed && _mod.Host == a;
			if (possessed && previous != null && previous != a)
			{
				Retire(previous);
			}
			bool female = false;
			try
			{
				female = a.isSexFemale();
			}
			catch
			{
			}
			string sprite = "?";
			try
			{
				sprite = a.getSpriteToRender()?.name ?? "null";
			}
			catch
			{
			}
			if (!possessed)
			{
				Log.Info("SIZES body " + k.Label + ": could not be taken over (" + WorldBoxMod.Describe(a) + "), measured as drawn in front of " + WorldBoxMod.Describe(_mod.Host));
				yield return Unpossessed(k, a, female, sprite, how);
				Remove(a);
				yield break;
			}
			if (k.Label == "human man")
			{
				_man = a;
			}
			Place(a, at);
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(0f);
			for (float w = 0f; w < 3f; w += 0.1f)
			{
				if (!a.under_forces && !(a.position_height > 0f))
				{
					break;
				}
				yield return Wait(0.1f);
			}
			Place(a, at);
			yield return Wait(2.2f);
			float rawLo = 99f;
			float rawHi = 0f;
			float eyeLo = 99f;
			float eyeHi = -99f;
			for (float w = 0f; w < 0.6f; w += Time.unscaledDeltaTime)
			{
				yield return null;
				float b = RawHeight(a);
				rawLo = Mathf.Min(rawLo, b);
				rawHi = Mathf.Max(rawHi, b);
				float b2 = _mod.EyeForTest.z - Ground(a.current_position) - _mod.LiftForTest;
				eyeLo = Mathf.Min(eyeLo, b2);
				eyeHi = Mathf.Max(eyeHi, b2);
			}
			float num = Stature.Peek(a, RawHeight(a));
			float num2 = Mathf.Clamp(num, 0.3f, 8f);
			string text;
			if (Creatures.DrawnAs(a, out var model, out var scale, out var _, out var tall))
			{
				CreatureRig creatureRig = Models.CreatureRigFor(model);
				text = $"drawn as {model.Name} {tall:F3} tall (rig {creatureRig.Height:F2} m long {creatureRig.Length:F2} m, legs {creatureRig.LegLength * scale:F3}), scale {scale:F4} tiles/m";
			}
			else
			{
				string text2 = null;
				try
				{
					text2 = Models.PersonFamily(a);
				}
				catch
				{
				}
				MeshModel meshModel = ((text2 != null) ? Models.Get(text2) : null);
				float num3 = ((meshModel != null) ? Models.Rig(meshModel).Height : 0f);
				text = ((meshModel != null) ? $"drawn as {text2} {num2:F3} tall (model {num3:F2} m: {num2 / Mathf.Max(0.01f, num3):F4} tiles/m)" : "no model");
			}
			float num4 = 0f;
			float num5 = 0f;
			float num6 = 0f;
			float num7 = 0f;
			try
			{
				num4 = ((BaseSimObject)a).stats["speed"];
				num5 = ((BaseSimObject)a).stats["mass"];
				num6 = ((BaseSimObject)a).stats["mass_2"];
				num7 = ((BaseSimObject)a).stats["size"];
			}
			catch
			{
			}
			float num8 = 1f;
			try
			{
				num8 = Config.time_scale_asset.multiplier;
			}
			catch
			{
			}
			float num9 = PossessionHooks.SpeedForTest(a);
			float num10 = Jumps.Share(a);
			Jumps.Solve(num10, Mathf.Max(0.01f, num5), out var force, out var along, out var steps);
			int steps2;
			float num11 = Jumps.Peak(force, Mathf.Max(0.01f, num5), out steps2);
			float bodyRadius = PossessionHooks.BodyRadius;
			string text3 = string.Format("{0}: {1} ({2}), adult {3}, sprite {4}; body {5:F3} tiles (sprite {6:F3}..{7:F3}, steady {8:F3}), eye {9:F3}..{10:F3} over the ground ", k.Label, female ? "female" : "male", how, SafeAdult(a), sprite, num2, rawLo, rawHi, num, eyeLo, eyeHi) + $"(0.9 x body {0.9f * num2:F3}), radius {bodyRadius:F3} (0.17 x body {Mathf.Clamp(0.17f * num2, 0.1f, 0.6f):F3}), step {RoomFloor.StepOf(num2):F2}, head {RoomFloor.HeadOf(num2):F3}; {text}; " + $"stats speed {num4:F1} mass {num5:F2} mass_2 {num6:F2} size {num7:F2} actor_scale {a.actor_scale:F3}; full speed {num9:F2} tiles/game s (walk {num9 * num8 * PossessionHooks.RunFactor:F2}, sprint {num9 * num8 * 1.35f:F2} tiles/real s); " + string.Format("jump share {0:F2} force {1:F3} along {2:F3} -> peak {3:F3} tiles, {4} ticks in the air{5}; {6}; traits {7}", num10, force, along, num11, steps2, (steps < 6) ? " (no jump: under 6 ticks)" : "", Colliders.BodyForTest, Traits(a));
			Log.Info("SIZES body " + text3);
			_bodyRows.Add(text3);
			yield return KindJump(k, a);
		}

		private static string SafeAdult(Actor a)
		{
			try
			{
				return a.isAdult() ? "yes" : (a.isBaby() ? "baby" : "no");
			}
			catch
			{
				return "?";
			}
		}

		private IEnumerator Unpossessed(Kind k, Actor a, bool female, string sprite, string how)
		{
			Actor host = _mod.Host;
			if (host == null)
			{
				yield break;
			}
			Vector2 at = host.current_position + new Vector2(0f, 2.5f);
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(-10f);
			float rawLo = 99f;
			float rawHi = 0f;
			for (float t = 0f; t < 2.6f; t += Time.unscaledDeltaTime)
			{
				try
				{
					a.cancelAllBeh();
				}
				catch
				{
				}
				Place(a, at);
				yield return null;
				float b = RawHeight(a);
				if (t > 1.8f)
				{
					rawLo = Mathf.Min(rawLo, b);
					rawHi = Mathf.Max(rawHi, b);
				}
			}
			float num = Stature.Peek(a, RawHeight(a));
			string text = null;
			try
			{
				text = Models.PersonFamily(a);
			}
			catch
			{
			}
			float b2 = 0f;
			try
			{
				b2 = ((BaseSimObject)a).stats["mass"];
			}
			catch
			{
			}
			float num2 = Jumps.Share(a);
			Jumps.Solve(num2, Mathf.Max(0.01f, b2), out var force, out var _, out var _);
			int steps2;
			float num3 = Jumps.Peak(force, Mathf.Max(0.01f, b2), out steps2);
			string text2 = string.Format("{0}: {1} ({2}), adult {3}, sprite {4}; drawn body {5:F3} tiles (sprite {6:F3}..{7:F3}) as {8}; eye would be {9:F3}, radius {10:F3}, step {11:F2}; ", k.Label, female ? "female" : "male", how, SafeAdult(a), sprite, num, rawLo, rawHi, text, 0.9f * num, Mathf.Clamp(0.17f * num, 0.1f, 0.6f), RoomFloor.StepOf(num)) + $"speed {PossessionHooks.SpeedForTest(a):F2} tiles/game s; jump share {num2:F2} -> peak {num3:F3} tiles, {steps2} ticks; not taken over";
			Log.Info("SIZES body " + text2);
			_bodyRows.Add(text2);
		}

		private IEnumerator KindJump(Kind k, Actor a)
		{
			PossessionHooks.ScriptedInput = Vector2.zero;
			yield return Wait(0.3f);
			for (float w = 0f; w < 2f; w += 0.1f)
			{
				if (!a.under_forces && !(a.position_height > 0f))
				{
					break;
				}
				yield return Wait(0.1f);
			}
			Jumps.LastShare = -1f;
			Vector2 from = a.current_position;
			Vector2 land = from;
			float peak = 0f;
			float air = 0f;
			int frames = 0;
			bool up = false;
			bool down = false;
			WorldTime();
			double wUp = 0.0;
			double wDown = 0.0;
			PossessionHooks.ScriptedJump = true;
			for (float w = 0f; w < 2.6f; w += Time.unscaledDeltaTime)
			{
				yield return null;
				float position_height = a.position_height;
				if (position_height > 0.0005f && !down)
				{
					if (!up)
					{
						wUp = WorldTime();
					}
					up = true;
					frames++;
					air += Time.unscaledDeltaTime;
					peak = Mathf.Max(peak, position_height);
				}
				else if (up && !down)
				{
					down = true;
					land = a.current_position;
					wDown = WorldTime();
				}
			}
			PossessionHooks.ScriptedJump = false;
			string text = ((Jumps.LastShare < 0f) ? " (not through the mod's jump)" : "");
			Log.Info($"SIZES bodyjump {k.Label}: expected {Jumps.LastPeak:F3} tiles, reached {peak:F3}, {frames} frames / {air:F3} real s / {wDown - wUp:F3} world s in the air, moved {(land - from).magnitude:F3} tiles{text}");
			_bodyRows.Add($"  jump {k.Label}: reached {peak:F3} tiles, {frames} frames, {air:F3} s");
		}

		private static double WorldTime()
		{
			try
			{
				return World.world.getCurWorldTime();
			}
			catch
			{
				return 0.0;
			}
		}

		private void Dragon()
		{
			MeshModel meshModel = Models.Get("monster_dragon");
			if (meshModel != null)
			{
				float num = 0.8f;
				Log.Info(string.Format("SIZES body dragon (from its model, not spawned): {0} units {1} drawn x{2}: {3:F2} wide x {4:F2} long x {5:F2} tall (z {6:F2}..{7:F2}) tiles, with its wings as modelled; {8}", meshModel.Name, meshModel.Info("units"), num, (meshModel.Max.X - meshModel.Min.X) * num, (meshModel.Max.Y - meshModel.Min.Y) * num, (meshModel.Max.Z - meshModel.Min.Z) * num, meshModel.Min.Z * num, meshModel.Max.Z * num, InfoOf(meshModel, 8)));
			}
			try
			{
				ActorAsset actorAsset = AssetManager.actor_library.get("dragon");
				if (actorAsset != null)
				{
					Log.Info(string.Format("SIZES body dragon asset: speed {0:F1} mass {1:F2} mass_2 {2:F2} size {3:F2} scale {4:F3} flying {5} can jump {6}", actorAsset.base_stats["speed"], actorAsset.base_stats["mass"], actorAsset.base_stats["mass_2"], actorAsset.base_stats["size"], actorAsset.base_stats["scale"], actorAsset.flying, actorAsset.control_can_jump));
				}
			}
			catch (Exception ex)
			{
				Log.Info("SIZES body dragon asset: " + ex.Message);
			}
		}

		private IEnumerator HumanJumps()
		{
			Actor man = ((_man != null && _man.isAlive()) ? _man : _king);
			if (_mod.Host != man)
			{
				Actor left = _mod.Host;
				if (!Possess.JumpTo(man))
				{
					Log.Error("SIZES jumps: could not take over " + WorldBoxMod.Describe(man));
					yield break;
				}
				yield return Wait(1.6f);
				if (_mod.Host == man)
				{
					Retire(left);
				}
			}
			if (_mod.Host != man)
			{
				Log.Error("SIZES jumps: not the man");
				yield break;
			}
			Invincible(man);
			int num = 0;
			int num2 = 0;
			float num3 = float.MaxValue;
			for (int i = 0; i < 110; i += 3)
			{
				for (int j = 0; j < 24; j++)
				{
					int num4 = _home.x + (int)(Mathf.Cos((float)j * 0.2618f) * (float)i) - 12;
					int num5 = _home.y + (int)(Mathf.Sin((float)j * 0.2618f) * (float)i);
					if (num4 < 4 || num5 < 4 || num4 + 30 > MapBox.width || num5 + 4 > MapBox.height)
					{
						continue;
					}
					bool flag = true;
					for (int k = num5 - 1; k <= num5 + 1 && flag; k++)
					{
						for (int l = num4; l < num4 + 26 && flag; l++)
						{
							WorldTile tile = World.world.GetTile(l, k);
							flag = DryTile(tile) && (tile.building == null || Clearable(tile.building)) && (tile.zone == null || tile.zone.city == null);
						}
					}
					if (flag)
					{
						float num6 = Rise(num4, num5 - 1, 26, 3);
						if (num6 < num3)
						{
							num3 = num6;
							num = num4;
							num2 = num5;
						}
					}
				}
			}
			if (num3 == float.MaxValue)
			{
				Log.Error("SIZES jumps: no open strip of land");
				yield break;
			}
			ClearBox(num - 1, num2 - 2, 28, 5);
			Log.Info($"SIZES jump strip {num},{num2} east 26 tiles, ground within {num3:F2} tiles; {WorldBoxMod.Describe(man)}, traits {Traits(man)}, body {Colliders.BodyForTest}");
			Vector2 start = new Vector2((float)num + 1.5f, (float)num2 + 0.5f);
			for (int m = 1; m <= 3; m++)
			{
				Place(man, start + new Vector2(4f + (float)m * 3f, 0f));
				_mod.SetYaw(0f);
				_mod.SetPitchDegrees(0f);
				yield return Wait(1.2f);
				for (float w = 0f; w < 2f; w += 0.1f)
				{
					if (!man.under_forces && !(man.position_height > 0f))
					{
						break;
					}
					yield return Wait(0.1f);
				}
				yield return Trace($"standing #{m}", man, forward: false, run: false);
			}
			for (int m = 1; m <= 3; m++)
			{
				bool run = m > 1;
				Place(man, start);
				_mod.SetYaw(0f);
				_mod.SetPitchDegrees(0f);
				yield return Wait(1f);
				for (float w2 = 0f; w2 < 2f; w2 += 0.1f)
				{
					if (!man.under_forces && !(man.position_height > 0f))
					{
						break;
					}
					yield return Wait(0.1f);
				}
				PossessionHooks.ScriptedRun = run;
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				Vector2 mark = man.current_position;
				float w = 0f;
				float t = 0f;
				while (t < 1.8f)
				{
					yield return null;
					t += Time.unscaledDeltaTime;
					if (w == 0f && t >= 1.3f)
					{
						w = t;
						mark = man.current_position;
					}
				}
				float pace = (man.current_position - mark).magnitude / Mathf.Max(0.01f, t - w);
				yield return Trace(run ? $"running #{m - 1}" : "walking", man, forward: true, run, pace);
				PossessionHooks.ScriptedInput = Vector2.zero;
				PossessionHooks.ScriptedRun = false;
			}
		}

		private IEnumerator Trace(string label, Actor a, bool forward, bool run, float pace = 0f)
		{
			StringBuilder sb = new StringBuilder();
			Vector2 p0 = a.current_position;
			Vector2 prev = p0;
			Vector2 pTake = p0;
			Vector2 pLand = p0;
			float t = 0f;
			float peak = 0f;
			float tTake = -1f;
			float tPeak = 0f;
			float tLand = -1f;
			float peakLift = 0f;
			float fpsTime = 0f;
			int frame = 0;
			int fTake = -1;
			int fPeak = -1;
			int fLand = -1;
			int fpsN = 0;
			WorldTime();
			double wTake = 0.0;
			double wPeak = 0.0;
			double wLand = 0.0;
			Jumps.LastShare = -1f;
			PossessionHooks.ScriptedJump = true;
			while (t < 3.5f)
			{
				yield return null;
				float unscaledDeltaTime = Time.unscaledDeltaTime;
				t += unscaledDeltaTime;
				frame++;
				fpsTime += unscaledDeltaTime;
				fpsN++;
				float position_height = a.position_height;
				float liftForTest = _mod.LiftForTest;
				Vector2 current_position = a.current_position;
				if (fTake < 0 && position_height > 0.0005f)
				{
					fTake = frame;
					tTake = t;
					pTake = prev;
					wTake = WorldTime();
				}
				if (fTake >= 0 && fLand < 0)
				{
					if (position_height > peak)
					{
						peak = position_height;
						peakLift = liftForTest;
						fPeak = frame;
						tPeak = t;
						wPeak = WorldTime();
					}
					if (position_height <= 0f && frame > fTake)
					{
						fLand = frame;
						tLand = t;
						pLand = current_position;
						wLand = WorldTime();
					}
				}
				if (fLand < 0 || frame <= fLand + 3)
				{
					sb.Append($"[{t * 1000f:F0} {position_height:F3} {liftForTest:F3} {(current_position - p0).magnitude:F2}] ");
				}
				prev = current_position;
				if (fLand > 0 && frame > fLand + 3)
				{
					if (forward)
					{
						PossessionHooks.ScriptedInput = Vector2.zero;
						PossessionHooks.ScriptedRun = false;
					}
					break;
				}
			}
			PossessionHooks.ScriptedJump = false;
			float num = (float)fpsN / Mathf.Max(0.001f, fpsTime);
			if (fTake < 0 || fLand < 0)
			{
				Log.Error(string.Format("SIZES jump {0}: {1} (share {2:F2})", label, (fTake < 0) ? "never left the ground" : "never came down", Jumps.LastShare));
				Log.Info($"SIZES jumptrace {label}: {sb}");
				yield break;
			}
			int num2 = fLand - fTake;
			Log.Info(string.Format("SIZES jump {0}: {1}peak {2:F3} tiles (the mod's lift {3:F3}) {4:F3} s / {5} frames / {6:F3} world s after leaving the ground; ", label, forward ? ((run ? "sprinting" : "walking") + $" at {pace:F2} tiles/s, ") : "standing, ", peak, peakLift, tPeak - tTake, fPeak - fTake, wPeak - wTake) + $"in the air {tLand - tTake:F3} s / {num2} frames / {wLand - wTake:F3} world s; {(pLand - pTake).magnitude:F3} tiles from takeoff to landing; Space to takeoff {tTake:F3} s ({fTake} frames); " + $"{num:F1} fps; expected {Jumps.LastPeak:F3} tiles (share {Jumps.LastShare:F2} force {Jumps.LastForce:F3} along {Jumps.LastAlong:F3})");
			Log.Info($"SIZES jumptrace {label} [ms since Space, position_height, the mod's lift, tiles along]: {sb}");
		}

		private IEnumerator Walls()
		{
			string[] wallTypes = WallTypes;
			foreach (string text in wallTypes)
			{
				StringBuilder stringBuilder = new StringBuilder();
				string[] array = new string[7] { "post", "end", "straight", "straight_b", "corner", "tee", "cross" };
				foreach (string text2 in array)
				{
					MeshModel meshModel = Models.Get(text + "_" + text2);
					if (meshModel == null)
					{
						stringBuilder.Append(text2 + ": none; ");
						continue;
					}
					float num = 0.5f;
					stringBuilder.Append($"{text2} {(meshModel.Max.X - meshModel.Min.X) * num:F2}x{(meshModel.Max.Y - meshModel.Min.Y) * num:F2} up to {meshModel.Max.Z * num:F2} (from {meshModel.Min.Z * num:F2}); ");
				}
				Log.Info($"SIZES wallmodels {text} (x{0.5f} tiles a unit): {stringBuilder}");
			}
			if (_sites == null || _sites.Count == 0)
			{
				Log.Error("SIZES walls: no flat site");
				yield break;
			}
			Vector2Int vector2Int = _sites[_sites.Count - 1];
			TopTileType grass = AssetManager.top_tiles.get("grass_low");
			TerrainCache c = _mod.TerrainForTest;
			Actor host = _mod.Host;
			if (host == null)
			{
				yield break;
			}
			int sx = vector2Int.x - 3;
			int sy = vector2Int.y;
			List<Vector2Int> tiles = new List<Vector2Int>();
			for (int k = 0; k < 7; k++)
			{
				tiles.Add(new Vector2Int(sx + k, sy));
			}
			tiles.Add(new Vector2Int(sx, sy + 1));
			tiles.Add(new Vector2Int(sx, sy + 2));
			tiles.Add(new Vector2Int(sx + 9, sy));
			ClearBox(sx - 2, sy - 2, 14, 6);
			string[] wallTypes2 = WallTypes;
			foreach (string type in wallTypes2)
			{
				TopTileType topTileType = AssetManager.top_tiles.get(type);
				if (topTileType == null)
				{
					Log.Info("SIZES walls: no tile type " + type);
					continue;
				}
				foreach (Vector2Int item in tiles)
				{
					WorldTile tile = World.world.GetTile(item.x, item.y);
					try
					{
						tile?.setTopTileType(topTileType);
					}
					catch (Exception ex)
					{
						Log.Info("SIZES walls: " + ex.Message);
					}
				}
				Place(host, new Vector2((float)sx + 3.5f, (float)sy - 4.5f));
				_mod.SetYaw(MathF.PI / 2f);
				_mod.SetPitchDegrees(10f);
				yield return Wait(1.6f);
				StringBuilder stringBuilder2 = new StringBuilder();
				Dictionary<int, MeshInstance> dictionary = _fWalls?.GetValue(_mod.SceneForTest) as Dictionary<int, MeshInstance>;
				Vector2Int[] array2 = new Vector2Int[4]
				{
					tiles[3],
					tiles[0],
					tiles[8],
					tiles[9]
				};
				for (int i = 0; i < array2.Length; i++)
				{
					Vector2Int vector2Int2 = array2[i];
					int num2 = vector2Int2.y * c.Width + vector2Int2.x;
					bool flag = (c.Flags[num2] & 8) != 0;
					string text3 = "no piece";
					if (dictionary != null && dictionary.TryGetValue(num2, out var value) && value.Model != null)
					{
						MeshModel meshModel2 = Models.Get((value.Model.Name.EndsWith("_m") || value.Model.Name.EndsWith("_f")) ? value.Model.Name.Substring(0, value.Model.Name.Length - 2) : value.Model.Name) ?? value.Model;
						text3 = $"{value.Model.Name} at {value.Position.Z - c.Ground[num2]:F2} over its ground, x{value.Basis.C2.Z:F2}: top {value.Position.Z + meshModel2.Max.Z * value.Basis.C2.Z - c.Ground[num2]:F2}";
					}
					stringBuilder2.Append($"({vector2Int2.x},{vector2Int2.y}) wall flag {flag}, terrain block {c.Heights[num2] - c.Ground[num2]:F2} over the ground (ground {c.Ground[num2]:F2}), {text3}; ");
				}
				float num3 = Ground(new Vector2((float)sx + 3.5f, (float)sy - 0.6f));
				float num4 = Ground(new Vector2((float)sx + 3.5f, (float)sy + 0.5f));
				Log.Info($"SIZES walls {type}: {stringBuilder2}sampled ground in front {num3:F2}, on the wall {num4:F2} ({num4 - num3:F2} up)");
				if (type == "wall_order")
				{
					_mod.SaveFrame("sizes_wall_order.png");
				}
				foreach (Vector2Int item2 in tiles)
				{
					WorldTile tile2 = World.world.GetTile(item2.x, item2.y);
					try
					{
						if (tile2 != null && grass != null)
						{
							tile2.setTopTileType(grass);
						}
					}
					catch
					{
					}
				}
				yield return Wait(0.4f);
			}
			Log.Info($"SIZES walls from the code: TerrainModel.WallHeight {3.4f} + merlons {0.45f} (plain blocks only), WallPieces.Scale {0.5f}");
		}

		private List<Job> Jobs()
		{
			List<Job> jobs = new List<Job>();
			string[] races = Races;
			foreach (string text in races)
			{
				string[] array;
				for (int j = 0; j < 6; j++)
				{
					array = new string[3] { "", "_b", "_c" };
					foreach (string arg in array)
					{
						Add($"house_{text}_{j}", $"house_{text}_{j}{arg}");
					}
				}
				array = new string[2] { "", "_b" };
				foreach (string text2 in array)
				{
					Add("tent_" + text, "tent_" + text + text2);
				}
				for (int l = 0; l < 3; l++)
				{
					Add($"hall_{text}_{l}", Models.Has($"tw_hall_{text}_{l}") ? $"tw_hall_{text}_{l}" : null);
				}
				if (Models.Has("tw_keep_" + text))
				{
					Add("hall_" + text + "_2", "tw_keep_" + text, "the capital's keep");
				}
				array = new string[9]
				{
					"windmill_" + text + "_0",
					"windmill_" + text + "_1",
					"watch_tower_" + text,
					"barracks_" + text,
					"temple_" + text,
					"library_" + text,
					"market_" + text,
					"docks_" + text,
					"fishing_docks_" + text
				};
				for (int k = 0; k < array.Length; k++)
				{
					Add(array[k], null);
				}
			}
			races = new string[8] { "bonfire", "statue", "well", "stockpile", "stockpile_fireproof", "stockpile_acidproof", "mine", "training_dummy" };
			for (int i = 0; i < races.Length; i++)
			{
				Add(races[i], null);
			}
			HashSet<string> hashSet = new HashSet<string>(jobs.Select((Job job) => job.Id));
			List<string> list = new List<string>();
			foreach (BuildingAsset item in AssetManager.buildings.list)
			{
				string text3 = item.id ?? "";
				string text4 = item.type ?? "";
				if (text3.Contains("fence") || text3.Contains("wall") || text3.Contains("gate"))
				{
					list.Add(text3 + "(" + text4 + ")");
				}
				bool num = text3.StartsWith("mineral", StringComparison.Ordinal);
				int num2;
				switch (text4)
				{
				default:
					num2 = (text3.Contains("fence") ? 1 : 0);
					break;
				case "type_statue":
				case "type_well":
				case "type_training_dummies":
				case "type_mine":
				case "type_hive":
				case "type_stockpile":
				case "type_bonfire":
					num2 = 1;
					break;
				}
				bool flag = (byte)num2 != 0;
				if ((num || flag) && !hashSet.Contains(text3) && !text3.Contains("civ_"))
				{
					Add(text3, null);
					hashSet.Add(text3);
				}
			}
			Log.Info("SIZES buildings named for fences, walls or gates: " + ((list.Count == 0) ? "none" : string.Join(" ", list)));
			return jobs;
			void Add(string id, string family, string note = null)
			{
				if (AssetManager.buildings.get(id) == null)
				{
					Log.Info("SIZES bld " + id + ": no such building");
				}
				else if (family != null && !Models.Has(family))
				{
					Log.Info("SIZES bld " + id + " as " + family + ": no such model");
				}
				else
				{
					jobs.Add(new Job
					{
						Id = id,
						Family = family,
						Note = note
					});
				}
			}
		}

		private string Pick(Building b, string family)
		{
			try
			{
				return (_force != null && b != null && b.current_tile == _forceTile && b.asset != null && b.asset.id == _forceId) ? _force : null;
			}
			catch
			{
				return null;
			}
		}

		private IEnumerator Buildings()
		{
			Actor host = _mod.Host;
			if (host == null || !host.isAlive())
			{
				Log.Error("SIZES buildings: no body");
				yield break;
			}
			if (_sites == null || _sites.Count < 1)
			{
				Log.Error("SIZES buildings: no flat sites");
				yield break;
			}
			Invincible(host);
			float waited = 0f;
			while (!Models.PackReady("tw_hall_human_0") && Models.Has("tw_hall_human_0") && waited < 12f)
			{
				yield return Wait(0.2f);
				waited += 0.2f;
			}
			List<Job> jobs = Jobs();
			Log.Info($"SIZES buildings: {jobs.Count} to place, body radius {PossessionHooks.BodyRadius:F3}, step {RoomFloor.StepOf(1f):F2} for a body of 1; {Colliders.BodyForTest}");
			TownModels.PickForTest = Pick;
			int sites = Mathf.Min(2, _sites.Count);
			Building[] at = new Building[sites];
			for (int k = 0; k < jobs.Count; k++)
			{
				int slot = k % sites;
				if (!host.isAlive())
				{
					Log.Error("SIZES buildings: the body died");
					break;
				}
				yield return Guard("bld " + jobs[k].Id, Measure(jobs[k], _sites[slot], at, slot, host));
				Building building = at[slot];
				if (building == null)
				{
					continue;
				}
				try
				{
					if (building.isAlive())
					{
						building.removeBuildingFinal();
					}
				}
				catch
				{
				}
			}
			for (int i = 0; i < at.Length; i++)
			{
				if (at[i] == null)
				{
					continue;
				}
				try
				{
					if (at[i].isAlive())
					{
						at[i].removeBuildingFinal();
					}
				}
				catch
				{
				}
			}
			TownModels.PickForTest = null;
			_force = null;
		}

		private IEnumerator Measure(Job job, Vector2Int site, Building[] at, int slot, Actor host)
		{
			Building old = at[slot];
			if (old != null)
			{
				for (float w = 0f; w < 4f; w += 0.1f)
				{
					if (BuildingFloors.ModelOf(old) == null)
					{
						break;
					}
					yield return Wait(0.1f);
				}
			}
			at[slot] = null;
			ClearBox(site.x - 8, site.y - 8, 17, 17);
			WorldTile tile = World.world.GetTile(site.x, site.y);
			_forceId = job.Id;
			_forceTile = tile;
			_force = job.Family;
			Building b = null;
			try
			{
				if (tile.building != null)
				{
					tile.building.removeBuildingFinal();
				}
				b = World.world.buildings.addBuilding(job.Id, tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				b?.completeConstruction();
			}
			catch (Exception ex)
			{
				Log.Info("SIZES bld " + job.Id + ": placing failed: " + ex.Message);
			}
			if (b == null)
			{
				Log.Info("SIZES bld " + job.Id + ": could not be placed");
				yield break;
			}
			at[slot] = b;
			float num = SpriteWidth(b.asset);
			Vector2 vector = new Vector2((float)site.x + 0.5f, (float)site.y + 0.5f);
			Vector2 stand = vector + new Vector2(0.6f, 0f - (0.5f * num + 2.2f));
			Place(host, stand);
			_mod.SetYaw(Mathf.Atan2(vector.y + 1f - stand.y, vector.x - stand.x));
			_mod.SetPitchDegrees(-5f);
			bool flag = false;
			Near near;
			for (float w = 0f; w < 7f; w += 0.1f)
			{
				if (flag)
				{
					break;
				}
				yield return Wait(0.1f);
				Place(host, stand);
				near = NearOf(b);
				bool num2 = near != null && near.Mesh != null && near.Mesh.Model != null && (job.Family == null || near.Family == job.Family);
				bool flag2 = near != null && near.Route == "Building" && near.Solid && (near.Family == null || !near.Family.Contains("docks"));
				flag = num2 && (!flag2 || BuildingFloors.ReadyFor(b));
			}
			yield return Wait(0.3f);
			near = NearOf(b);
			if (near == null || near.Mesh == null || near.Mesh.Model == null)
			{
				Log.Info(string.Format("SIZES bld {0}{1}: never drawn as a model ({2}), alive {3}", job.Id, (job.Family != null) ? (" as " + job.Family) : "", (near == null) ? "not in the scene" : ("no model: family " + near.Family + ", look " + near.Look), b.isAlive()));
				yield break;
			}
			try
			{
				Report(job, b, near, host);
			}
			catch (Exception arg)
			{
				Log.Error($"SIZES bld {job.Id}: report failed: {arg}");
			}
		}

		private float SpriteWidth(BuildingAsset a)
		{
			try
			{
				a.checkSpritesAreLoaded();
				Sprite sprite = null;
				if (a.building_sprites != null && a.building_sprites.animation_data != null && a.building_sprites.animation_data.Count > 0)
				{
					BuildingAnimationData buildingAnimationData = a.building_sprites.animation_data[0];
					if (buildingAnimationData.main != null && buildingAnimationData.main.Length != 0)
					{
						sprite = buildingAnimationData.main[0];
					}
				}
				SpriteImage spriteImage = ((sprite != null) ? _mod.SpriteImageForTest(sprite) : null);
				if (spriteImage != null)
				{
					return spriteImage.UnitWidth * Mathf.Abs(a.scale_base.x);
				}
			}
			catch
			{
			}
			return 4f;
		}

		private static float Len(Vec3 v)
		{
			return Mathf.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
		}

		private static string KindName(byte k)
		{
			if (k >= KindNames.Length)
			{
				return k.ToString();
			}
			return KindNames[k];
		}

		private void Report(Job job, Building b, Near nb, Actor host)
		{
			MeshInstance mesh = nb.Mesh;
			MeshModel meshModel = Models.Get(nb.Family) ?? mesh.Model;
			float num = Len(mesh.Basis.C0);
			float num2 = Len(mesh.Basis.C1);
			float z = mesh.Basis.C2.Z;
			if (num < 0.001f || num2 < 0.001f || z < 0.001f)
			{
				Log.Info($"SIZES bld {job.Id}: drawn at no size ({num:F4},{num2:F4},{z:F4})");
				return;
			}
			bool flag = mesh.Basis.C0.X < 0f;
			float num3 = (meshModel.Max.X - meshModel.Min.X) * num;
			float num4 = (meshModel.Max.Y - meshModel.Min.Y) * num2;
			float num5 = meshModel.Max.Z * z - 0.05f;
			float num6 = meshModel.Min.Z * z;
			float num7 = Ground(new Vector2(mesh.Position.X, mesh.Position.Y));
			string text = ((nb.Image != null) ? $"{nb.Image.Width}x{nb.Image.Height} px, {nb.Width:F2}x{nb.Height:F2} tiles" : "?");
			float num8 = meshModel.InfoFloat("width", float.NaN);
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(string.Format("{0}{1} -> {2} (drawn {3}, route {4}, look {5}, solid {6}{7}): ", job.Id, (job.Note != null) ? (" (" + job.Note + ")") : "", nb.Family, mesh.Model.Name, nb.Route, nb.Look, nb.Solid, flag ? ", mirrored" : ""));
			stringBuilder.Append($"sprite {text}; scale {z:F4} tiles a model unit ({z * 1.75f:F3} x people's 1/1.75), kept {nb.Scale:F4}; ");
			stringBuilder.Append(string.Format("model {0:F2} x {1:F2} x {2:F2} units{3} -> {4:F2} wide x {5:F2} deep x {6:F2} tall tiles over the ground{7}; ", meshModel.Max.X - meshModel.Min.X, meshModel.Max.Y - meshModel.Min.Y, meshModel.Max.Z, float.IsNaN(num8) ? "" : $" (info width {num8:F2})", num3, num4, num5, (num6 < -0.06f) ? $" ({0f - num6:F2} more below: footing)" : ""));
			stringBuilder.Append($"base {mesh.Position.Z - num7:F2} off the ground under its middle; ");
			Vector2 doorWorld = new Vector2(float.NaN, float.NaN);
			if (DoorBox(meshModel, out var lo, out var hi))
			{
				doorWorld = ToWorld(mesh, (lo.X + hi.X) * 0.5f, lo.Y);
				stringBuilder.Append($"door {(hi.X - lo.X) * num:F2} wide x {(hi.Z - lo.Z) * z:F2} high, its foot {lo.Z * z - 0.05f:F2} over the ground; ");
			}
			else
			{
				stringBuilder.Append("no door; ");
			}
			if (BuildingFloors.ReadyFor(b) && BuildingFloors.DoorClimbForTest(b, 0.153f, RoomFloor.StepOf(0.9f), out var up, out var from, out var door))
			{
				stringBuilder.Append(string.Format("walking straight at the door (radius 0.153) with a {0:F2} step you get {1:F2} up, with a {2:F2} step {3}; ", RoomFloor.StepOf(0.9f), up - 0.05f, RoomFloor.StepOf(1f), BuildingFloors.DoorClimbForTest(b, 0.153f, RoomFloor.StepOf(1f), out var up2, out door, out from) ? (up2 - 0.05f).ToString("F2") : "?"));
			}
			string summary;
			string value = Reach(b, meshModel, mesh, z, Mathf.Clamp(0.153f, 0.1f, 0.6f), 0.9f, doorWorld, out summary);
			stringBuilder.Append(value);
			stringBuilder.Append("; info " + InfoOf(meshModel));
			Log.Info("SIZES bld " + stringBuilder);
			_buildingRows.Add(string.Format("{0}->{1}: {2:F2}x{3:F2}x{4:F2}, scale {5:F3}, door {6}; {7}", job.Id, nb.Family, num3, num4, num5, z, float.IsNaN(doorWorld.x) ? "none" : "yes", summary));
		}

		private string Reach(Building b, MeshModel full, MeshInstance inst, float s, float r, float body, Vector2 doorWorld, out string summary)
		{
			float num = Math.Max(full.Max.X - full.Min.X, full.Max.Y - full.Min.Y);
			float num2 = Math.Max(0.1f, num / 220f);
			ColumnField columnField = ColumnField.OfModel(full, num2);
			float num3 = RoomFloor.StepOf(body);
			float num4 = RoomFloor.HeadOf(body);
			int steps;
			float num5 = Jumps.Peak(1.5f, 1f, out steps);
			float r2 = r / s;
			float tol = 0.03f / s;
			float num6 = 0.05f / s;
			float num7 = 0.1f / s;
			float num8 = 0.8f / s;
			float num9 = full.Min.X - num8;
			float num10 = full.Min.Y - num8;
			int num11 = (int)((full.Max.X - full.Min.X + 2f * num8) / num7) + 1;
			int num12 = (int)((full.Max.Y - full.Min.Y + 2f * num8) / num7) + 1;
			List<Spot> list = new List<Spot>();
			List<int>[] array = new List<int>[num11 * num12];
			for (int i = 0; i < num12; i++)
			{
				for (int j = 0; j < num11; j++)
				{
					float x = num9 + (float)j * num7;
					float y = num10 + (float)i * num7;
					int num13 = i * num11 + j;
					List<int> list2 = new List<int>(2);
					float maxZ = full.Max.Z + 1f;
					for (int k = 0; k < 8; k++)
					{
						if (columnField == null)
						{
							break;
						}
						if (!columnField.Support(x, y, r2, maxZ, tol, out var z, out var what))
						{
							break;
						}
						if (z < num6 + 0.04f / s)
						{
							break;
						}
						if (!columnField.Blocked(x, y, r2, z + num3 / s, z + num4 / s))
						{
							list2.Add(list.Count);
							list.Add(new Spot
							{
								Cell = num13,
								Z = z,
								What = what
							});
						}
						maxZ = z - 0.04f / s;
					}
					if (columnField == null || !columnField.Blocked(x, y, r2, num6 + num3 / s, num6 + num4 / s))
					{
						list2.Add(list.Count);
						list.Add(new Spot
						{
							Cell = num13,
							Z = num6,
							What = byte.MaxValue
						});
					}
					array[num13] = list2;
				}
			}
			Heap heap = new Heap();
			for (int l = 0; l < list.Count; l++)
			{
				Spot spot = list[l];
				if (spot.What == byte.MaxValue)
				{
					int num14 = spot.Cell % num11;
					int num15 = spot.Cell / num11;
					float num16 = num9 + (float)num14 * num7;
					float num17 = num10 + (float)num15 * num7;
					if (!(num16 > full.Min.X) || !(num16 < full.Max.X) || !(num17 > full.Min.Y) || !(num17 < full.Max.Y))
					{
						spot.Need = 0f;
						heap.Push(0f, l);
					}
				}
			}
			while (heap.Count > 0)
			{
				float k2;
				int index = heap.Pop(out k2);
				Spot spot2 = list[index];
				if (k2 > spot2.Need)
				{
					continue;
				}
				int num18 = spot2.Cell % num11;
				int num19 = spot2.Cell / num11;
				float num20 = num9 + (float)num18 * num7;
				float num21 = num10 + (float)num19 * num7;
				float num22 = columnField?.CeilingAt(num20, num21, r2, spot2.Z + 0.02f / s) ?? float.PositiveInfinity;
				bool flag = false;
				for (int m = 0; m < 4; m++)
				{
					int num23;
					switch (m)
					{
					default:
						num23 = 0;
						break;
					case 1:
						num23 = -1;
						break;
					case 0:
						num23 = 1;
						break;
					}
					int num24 = num18 + num23;
					int num25;
					switch (m)
					{
					default:
						num25 = 0;
						break;
					case 3:
						num25 = -1;
						break;
					case 2:
						num25 = 1;
						break;
					}
					int num26 = num19 + num25;
					if (num24 < 0 || num26 < 0 || num24 >= num11 || num26 >= num12)
					{
						continue;
					}
					bool flag2 = false;
					foreach (int item in array[num26 * num11 + num24])
					{
						Spot spot3 = list[item];
						float num27 = (spot3.Z - spot2.Z) * s;
						float val = 0f;
						if (Math.Abs(num27) <= num3 + 0.005f)
						{
							flag2 = true;
						}
						if (num27 > num3 + 0.005f)
						{
							if ((num22 - spot3.Z) * s < num4)
							{
								continue;
							}
							val = num27;
						}
						float num28 = Math.Max(k2, val);
						if (num28 < spot3.Need - 1E-05f)
						{
							spot3.Need = num28;
							heap.Push(num28, item);
						}
					}
					if (!flag2)
					{
						flag = true;
					}
				}
				if (!flag || columnField == null)
				{
					continue;
				}
				for (int n = 0; n < 8; n++)
				{
					int num29;
					switch (n)
					{
					default:
						num29 = 0;
						break;
					case 1:
					case 6:
					case 7:
						num29 = -1;
						break;
					case 0:
					case 4:
					case 5:
						num29 = 1;
						break;
					}
					int num30 = num29;
					int num31;
					switch (n)
					{
					default:
						num31 = 0;
						break;
					case 3:
					case 5:
					case 7:
						num31 = -1;
						break;
					case 2:
					case 4:
					case 6:
						num31 = 1;
						break;
					}
					int num32 = num31;
					for (int num33 = 2; num33 <= 4; num33++)
					{
						int num34 = num18 + num30 * num33;
						int num35 = num19 + num32 * num33;
						if (num34 < 0 || num35 < 0 || num34 >= num11 || num35 >= num12)
						{
							break;
						}
						foreach (int item2 in array[num35 * num11 + num34])
						{
							Spot spot4 = list[item2];
							float num36 = (spot4.Z - spot2.Z) * s;
							if (Math.Abs(num36) <= num3 + 0.005f || (num36 > 0f && (num22 - spot4.Z) * s < num4))
							{
								continue;
							}
							float num37 = Math.Max(spot2.Z, spot4.Z);
							float num38 = num9 + (float)num34 * num7;
							float num39 = num10 + (float)num35 * num7;
							bool flag3 = true;
							for (int num40 = 1; num40 < num33 * 2 && flag3; num40++)
							{
								float num41 = (float)num40 / ((float)num33 * 2f);
								if (columnField.Blocked(num20 + (num38 - num20) * num41, num21 + (num39 - num21) * num41, num7 * 0.5f, num37 + 0.02f / s, num37 + num4 / s))
								{
									flag3 = false;
								}
							}
							if (flag3)
							{
								float num42 = Math.Max(k2, (num36 > 0f) ? num36 : 0f);
								if (num42 < spot4.Need - 1E-05f)
								{
									spot4.Need = num42;
									heap.Push(num42, item2);
								}
							}
						}
					}
				}
			}
			SortedDictionary<int, Level> sortedDictionary = new SortedDictionary<int, Level>();
			foreach (Spot item3 in list)
			{
				if (item3.What != byte.MaxValue)
				{
					int key = Mathf.RoundToInt((item3.Z * s - 0.05f) / 0.05f);
					if (!sortedDictionary.TryGetValue(key, out var value))
					{
						value = (sortedDictionary[key] = new Level());
					}
					int num43 = item3.Cell % num11;
					int num44 = item3.Cell / num11;
					float num45 = num9 + (float)num43 * num7;
					float num46 = num10 + (float)num44 * num7;
					value.Count++;
					value.MinX = Mathf.Min(value.MinX, num45);
					value.MaxX = Mathf.Max(value.MaxX, num45);
					value.MinY = Mathf.Min(value.MinY, num46);
					value.MaxY = Mathf.Max(value.MaxY, num46);
					value.SumX += num45;
					value.SumY += num46;
					value.What[item3.What] = ((!value.What.TryGetValue(item3.What, out var value2)) ? 1 : (value2 + 1));
					value.MinNeed = Mathf.Min(value.MinNeed, item3.Need);
					if (!float.IsNaN(doorWorld.x))
					{
						value.NearDoor = Mathf.Min(value.NearDoor, (ToWorld(inst, num45, num46) - doorWorld).magnitude);
					}
				}
			}
			foreach (Spot item4 in list)
			{
				if (item4.What != byte.MaxValue)
				{
					Level level2 = sortedDictionary[Mathf.RoundToInt((item4.Z * s - 0.05f) / 0.05f)];
					int num47 = item4.Cell % num11;
					int num48 = item4.Cell / num11;
					float num49 = num9 + (float)num47 * num7;
					float num50 = num10 + (float)num48 * num7;
					float num51 = level2.SumX / (float)level2.Count;
					float num52 = level2.SumY / (float)level2.Count;
					float num53 = (num49 - num51) * (num49 - num51) + (num50 - num52) * (num50 - num52);
					if (num53 < level2.ExDist)
					{
						level2.ExDist = num53;
						level2.ExX = num49;
						level2.ExY = num50;
						level2.ExZ = item4.Z;
					}
				}
			}
			float cellArea = 0.010000001f;
			StringBuilder stringBuilder = new StringBuilder();
			float num54 = 0f;
			float num55 = 0f;
			float num56 = 0f;
			float num57 = 0f;
			float num58 = float.NaN;
			float num59 = float.NaN;
			foreach (KeyValuePair<int, Level> item5 in sortedDictionary)
			{
				Level value3 = item5.Value;
				float num60 = (float)item5.Key * 0.05f;
				float num61 = (float)value3.Count * cellArea;
				if (!(num61 < 0.04f))
				{
					byte key2 = value3.What.OrderByDescending((KeyValuePair<byte, int> p) => p.Value).First().Key;
					string text = ((value3.ExDist < float.MaxValue) ? SurfaceAt(full, value3.ExX, value3.ExY, value3.ExZ, 0.06f / s) : null);
					float num62 = (value3.MaxX - value3.MinX) * s + 0.1f;
					float num63 = (value3.MaxY - value3.MinY) * s + 0.1f;
					string text2 = ((value3.MinNeed >= float.MaxValue) ? "out of reach" : ((value3.MinNeed <= 0f) ? "walk" : $"climb {value3.MinNeed:F2}"));
					stringBuilder.Append(string.Format("{0:F2} ({1:F2} t2, {2:F1}x{3:F1}, {4}, {5}{6}; {7}) [{8}]; ", num60, num61, num62, num63, KindName(key2), text ?? "?", (value3.NearDoor < 50f) ? $", {value3.NearDoor:F1} from the door" : "", text2, Guess(num60, num61, value3.NearDoor, text, key2)));
					if (value3.MinNeed <= 0f)
					{
						num54 = Mathf.Max(num54, num60);
					}
					if (value3.MinNeed <= num5)
					{
						num55 = Mathf.Max(num55, num60);
					}
					if (num60 > num56)
					{
						num56 = num60;
						num57 = value3.MinNeed;
					}
					if (value3.MinNeed > num5 && value3.MinNeed < float.MaxValue && float.IsNaN(num58))
					{
						num58 = num60;
						num59 = value3.MinNeed;
					}
				}
			}
			StringBuilder stringBuilder2 = new StringBuilder();
			float[] obj = new float[12]
			{
				0f, 0.4f, 0.5f, 0f, 0.75f, 0.9f, 1f, 1.25f, 1.5f, 2f,
				3f, 5f
			};
			obj[0] = num3;
			obj[3] = num5;
			float[] array2 = obj;
			foreach (float num64 in array2)
			{
				float num65 = 0f;
				float num66 = 0f;
				foreach (KeyValuePair<int, Level> item6 in sortedDictionary)
				{
					if (!(item6.Value.MinNeed > num64 + 0.0001f))
					{
						num65 = Mathf.Max(num65, (float)item6.Key * 0.05f);
						num66 += (float)item6.Value.Count * cellArea;
					}
				}
				stringBuilder2.Append($"{num64:F2}: up to {num65:F2} ({num66:F1} t2); ");
			}
			string text3 = "";
			List<KeyValuePair<int, Level>> list3 = sortedDictionary.Where((KeyValuePair<int, Level> kv) => (float)kv.Value.Count * cellArea >= 0.04f).ToList();
			if (list3.Count > 0 && BuildingFloors.ReadyFor(b))
			{
				Level value4 = list3[list3.Count - 1].Value;
				if (value4.ExDist < float.MaxValue)
				{
					Vector2 vector = ToWorld(inst, value4.ExX, value4.ExY);
					float z2;
					byte what2;
					bool flag4 = BuildingFloors.StandOn(vector.x, vector.y, r, inst.Position.Z + (value4.ExZ + 0.03f / s) * s, out z2, out what2);
					text3 = "; BuildingFloors at its highest level's middle: " + (flag4 ? (z2 - inst.Position.Z - 0.05f).ToString("F2") : "nothing");
				}
			}
			else if (!BuildingFloors.ReadyFor(b))
			{
				text3 = "; (not in BuildingFloors: nothing to stand on in the game)";
			}
			summary = string.Format("walk up to {0:F2}, today's {1:F2} jump up to {2:F2}, first beyond it {3}, highest {4:F2} ({5})", num54, num5, num55, float.IsNaN(num58) ? "-" : $"{num58:F2} (climb {num59:F2})", num56, (num57 >= float.MaxValue) ? "out of reach" : ((num57 <= 0f) ? "walk" : ("climb " + num57.ToString("F2"))));
			return string.Format("body {0:F2} (radius {1:F3}, step {2:F2}, head {3:F2}), field cell {4:F2} units: {5}; by jump height {6}levels: {7}{8}", body, r, num3, num4, num2, summary, stringBuilder2, (stringBuilder.Length == 0) ? "none" : stringBuilder.ToString(), text3);
		}

		private static Vector2 ToWorld(MeshInstance inst, float mx, float my)
		{
			Vec3 vec = inst.Position + inst.Basis.Mul(new Vec3(mx, my, 0f));
			return new Vector2(vec.X, vec.Y);
		}

		private static string Guess(float lvl, float area, float doorDist, string surface, byte what)
		{
			string text = (surface ?? "").ToLowerInvariant();
			if (text.Contains("roof") || what == 5 || what == 6)
			{
				if (!(lvl > 1.2f))
				{
					return "low roof/eave/awning";
				}
				return "roof (a flat part)";
			}
			if (doorDist < 1f && lvl <= 0.45f)
			{
				return "doorstep/stoop";
			}
			if (text.Contains("step") || text.Contains("stair"))
			{
				return "steps";
			}
			if (text.Contains("crate") || text.Contains("box"))
			{
				return "crate";
			}
			if (text.Contains("barrel") || text.Contains("cask") || text.Contains("keg"))
			{
				return "barrel";
			}
			if (text.Contains("porch") || text.Contains("deck") || text.Contains("plank") || text.Contains("terrace") || text.Contains("plinth"))
			{
				return "porch/deck/plinth";
			}
			if (area >= 2.5f && lvl <= 0.8f)
			{
				return "platform/porch/plinth";
			}
			if (area < 1.5f && lvl <= 1.3f)
			{
				return "prop (crate, barrel, bench, trough...)";
			}
			if (lvl > 1.3f)
			{
				return "wall top/upper deck/flat roof";
			}
			return "ledge/low wall";
		}

		private static bool DoorBox(MeshModel m, out Vec3 lo, out Vec3 hi)
		{
			lo = (hi = default(Vec3));
			int num = m.SlotIndex("door");
			if (num < 0)
			{
				return false;
			}
			float num2 = float.MaxValue;
			MeshPart[] parts = m.Parts;
			foreach (MeshPart meshPart in parts)
			{
				for (int j = 0; j < meshPart.TriangleCount; j++)
				{
					if (meshPart.Slot[j] == num)
					{
						for (int k = 0; k < 3; k++)
						{
							num2 = Math.Min(num2, meshPart.Pos[meshPart.Tri[j * 3 + k] * 3 + 2]);
						}
					}
				}
			}
			float num3 = float.MaxValue;
			float num4 = float.MaxValue;
			float num5 = float.MaxValue;
			float num6 = float.MinValue;
			float num7 = float.MinValue;
			float num8 = float.MinValue;
			parts = m.Parts;
			foreach (MeshPart meshPart2 in parts)
			{
				for (int l = 0; l < meshPart2.TriangleCount; l++)
				{
					if (meshPart2.Slot[l] != num)
					{
						continue;
					}
					float num9 = float.MaxValue;
					for (int n = 0; n < 3; n++)
					{
						num9 = Math.Min(num9, meshPart2.Pos[meshPart2.Tri[l * 3 + n] * 3 + 2]);
					}
					if (!(num9 > num2 + 0.35f))
					{
						for (int num10 = 0; num10 < 3; num10++)
						{
							int num11 = meshPart2.Tri[l * 3 + num10] * 3;
							num3 = Math.Min(num3, meshPart2.Pos[num11]);
							num6 = Math.Max(num6, meshPart2.Pos[num11]);
							num4 = Math.Min(num4, meshPart2.Pos[num11 + 1]);
							num7 = Math.Max(num7, meshPart2.Pos[num11 + 1]);
							num5 = Math.Min(num5, meshPart2.Pos[num11 + 2]);
							num8 = Math.Max(num8, meshPart2.Pos[num11 + 2]);
						}
					}
				}
			}
			if (num3 > num6)
			{
				return false;
			}
			lo = new Vec3(num3, num4, num5);
			hi = new Vec3(num6, num7, num8);
			return true;
		}

		private static string SurfaceAt(MeshModel m, float mx, float my, float mz, float tol)
		{
			string result = null;
			float num = tol;
			MeshPart[] parts = m.Parts;
			foreach (MeshPart meshPart in parts)
			{
				for (int j = 0; j < meshPart.TriangleCount; j++)
				{
					int num2 = meshPart.Tri[j * 3] * 3;
					int num3 = meshPart.Tri[j * 3 + 1] * 3;
					int num4 = meshPart.Tri[j * 3 + 2] * 3;
					float num5 = meshPart.Pos[num2];
					float num6 = meshPart.Pos[num2 + 1];
					float num7 = meshPart.Pos[num2 + 2];
					float num8 = meshPart.Pos[num3];
					float num9 = meshPart.Pos[num3 + 1];
					float num10 = meshPart.Pos[num3 + 2];
					float num11 = meshPart.Pos[num4];
					float num12 = meshPart.Pos[num4 + 1];
					float num13 = meshPart.Pos[num4 + 2];
					float num14 = num8 - num5;
					float num15 = num9 - num6;
					float num16 = num10 - num7;
					float num17 = num11 - num5;
					float num18 = num12 - num6;
					float num19 = num13 - num7;
					float num20 = num15 * num19 - num16 * num18;
					float num21 = num16 * num17 - num14 * num19;
					float num22 = num14 * num18 - num15 * num17;
					float num23 = Mathf.Sqrt(num20 * num20 + num21 * num21 + num22 * num22);
					if (num23 < 1E-09f || Mathf.Abs(num22) / num23 < 0.6f)
					{
						continue;
					}
					float num24 = (num9 - num12) * (num5 - num11) + (num11 - num8) * (num6 - num12);
					if (Mathf.Abs(num24) < 1E-12f)
					{
						continue;
					}
					float num25 = ((num9 - num12) * (mx - num11) + (num11 - num8) * (my - num12)) / num24;
					float num26 = ((num12 - num6) * (mx - num11) + (num5 - num11) * (my - num12)) / num24;
					float num27 = 1f - num25 - num26;
					if (!(num25 < -0.05f) && !(num26 < -0.05f) && !(num27 < -0.05f))
					{
						float num28 = Mathf.Abs(num25 * num7 + num26 * num10 + num27 * num13 - mz);
						if (!(num28 >= num))
						{
							num = num28;
							int num29 = meshPart.Slot[j];
							result = (meshPart.Name ?? "?") + "/" + ((num29 < m.SlotNames.Length) ? m.SlotNames[num29] : num29.ToString());
						}
					}
				}
			}
			return result;
		}

		private IEnumerator Rooms()
		{
			if (!Models.Ready)
			{
				Log.Error("SIZES rooms: no models");
				yield break;
			}
			Log.Info($"SIZES rooms from the code: InteriorBuilder.WallHeight {1.6f}, furniture and cabins {0.5714286f:F4} tiles/m, RoomFloor.StepOf(1) {RoomFloor.StepOf(1f):F2}, HeadOf(1) {RoomFloor.HeadOf(1f):F2}, room body radius {0.1f}");
			Dictionary<string, MeshModel> lib = Library();
			if (lib != null)
			{
				int n = 0;
				foreach (string item in lib.Keys.Where((string k) => k.StartsWith("int_", StringComparison.Ordinal) && !k.EndsWith("_m", StringComparison.Ordinal) && !k.EndsWith("_f", StringComparison.Ordinal)).OrderBy((string k) => k, StringComparer.Ordinal))
				{
					MeshModel meshModel = lib[item];
					float num = 0.5714286f;
					string text = meshModel.Info("use");
					Log.Info(string.Format("SIZES furniture {0}{1}: {2:F2} wide x {3:F2} deep, top {4:F2} (from {5:F2}) tiles at 1/1.75 (model {6:F2} m tall)", item, (text != null) ? (" (" + text + ")") : "", (meshModel.Max.X - meshModel.Min.X) * num, (meshModel.Max.Y - meshModel.Min.Y) * num, meshModel.Max.Z * num, meshModel.Min.Z * num, meshModel.Max.Z));
					int num2 = n + 1;
					n = num2;
					if (num2 % 40 == 0)
					{
						yield return null;
					}
				}
				Log.Info($"SIZES furniture: {n} int_ models; library {lib.Count} models");
			}
			uint wall = TerrainCache.Rgb(214, 196, 160);
			uint roof = TerrainCache.Rgb(168, 62, 50);
			string[] races = Races;
			foreach (string race in races)
			{
				int[] array = new int[3] { 0, 2, 5 };
				foreach (int tier in array)
				{
					int w = 5 + Mathf.RoundToInt((float)tier * 0.6f);
					int d = 4 + (tier + 1) / 3;
					TryRoom($"{race} tier {tier} ({w}x{d})", () => InteriorBuilder.Build(w, d, wall, roof, 1, race, tier, Models.Get));
					yield return null;
				}
			}
			List<(string, string)> list = new List<(string, string)>
			{
				("human_stone", "human"),
				("human_timber", "human"),
				("elf_spire", "elf"),
				("elf_pavilion", "elf"),
				("orc_lodge", "orc"),
				("orc_skull", "orc"),
				("dwarf_lodge", "dwarf"),
				("dwarf_stump", "dwarf")
			};
			string[] races2 = Races;
			foreach (string text2 in races2)
			{
				string[] array2 = new string[2] { "", "_b" };
				foreach (string text3 in array2)
				{
					string style = Models.Get("tent_" + text2 + text3)?.Info("style");
					if (style != null && !list.Any<(string, string)>(((string style, string race) p) => p.style == style))
					{
						list.Add((style, text2));
					}
				}
			}
			foreach (var item2 in list)
			{
				var (style2, race2) = item2;
				TryRoom(style2 + " tier 2 (6x5)", () => InteriorBuilder.Build(6, 5, wall, roof, 1, race2, 2, Models.Get, style2));
				yield return null;
			}
			races = new string[4] { "int_mine", "cabin_galleon", "cabin_transport", "cabin_trading" };
			foreach (string text4 in races)
			{
				MeshModel m = Models.Get(text4);
				if (m == null)
				{
					Log.Info("SIZES room " + text4 + ": no model");
					continue;
				}
				TryRoom(text4, () => CabinBuilder.Build(m, 0u));
				yield return null;
			}
			float waited = 0f;
			while (Models.Has("tw_room_hall_human_0") && !Models.PackReady("tw_room_hall_human_0") && waited < 12f)
			{
				yield return Wait(0.2f);
				waited += 0.2f;
			}
			races = Races;
			foreach (string text5 in races)
			{
				string[] array3 = new string[3]
				{
					"tw_room_hall_" + text5 + "_0",
					"tw_room_hall_" + text5 + "_2",
					"tw_room_keep_" + text5
				};
				foreach (string text6 in array3)
				{
					MeshModel m2 = Models.Get(text6);
					if (m2 == null)
					{
						Log.Info("SIZES room " + text6 + ": no model");
						continue;
					}
					TryRoom(text6, () => HallRoomBuilder.Build(m2, 0u));
					yield return null;
				}
			}
		}

		private void TryRoom(string label, Func<Interior> build)
		{
			try
			{
				RoomReport(label, build());
			}
			catch (Exception arg)
			{
				Log.Error($"SIZES room {label}: {arg}");
			}
		}

		private void RoomReport(string label, Interior room)
		{
			if (room == null)
			{
				Log.Info("SIZES room " + label + ": none");
				return;
			}
			StringBuilder stringBuilder = new StringBuilder();
			foreach (MeshInstance mesh in room.Meshes)
			{
				if (mesh?.Model != null)
				{
					MeshModel model = mesh.Model;
					float z = mesh.Basis.C2.Z;
					float num = mesh.Position.Z + model.Max.Z * z;
					float num2 = mesh.Position.Z + model.Min.Z * z;
					string text = model.Info("use");
					stringBuilder.Append(string.Format("{0}{1} {2:F2}x{3:F2} top {4:F2}{5}{6}; ", model.Name, (text != null) ? ("(" + text + ")") : "", (model.Max.X - model.Min.X) * Len(mesh.Basis.C0), (model.Max.Y - model.Min.Y) * Len(mesh.Basis.C1), num, (num2 > 0.03f) ? $" from {num2:F2}" : "", (Mathf.Abs(z - 0.5714286f) > 0.002f) ? $" x{z:F3}" : ""));
				}
			}
			Log.Info($"SIZES room {label}: walkable {room.MaxX - room.MinX:F2} x {room.MaxY - room.MinY:F2} tiles, top {room.Top:F2}, door at ({room.DoorX:F2},{room.DoorY:F2}), {room.Meshes.Count} models: {stringBuilder}");
			RoomFloor roomFloor = null;
			try
			{
				RoomFloor.Hatches(room);
				roomFloor = RoomFloor.Build(room);
			}
			catch (Exception ex)
			{
				Log.Info("SIZES room " + label + ": no field (" + ex.Message + ")");
			}
			if (roomFloor?.Field == null)
			{
				return;
			}
			SortedDictionary<int, (int, float, float, float)> sortedDictionary = new SortedDictionary<int, (int, float, float, float)>();
			for (float num3 = room.MinX; num3 <= room.MaxX; num3 += 0.05f)
			{
				for (float num4 = room.MinY; num4 <= room.MaxY; num4 += 0.05f)
				{
					float maxZ = room.Top + 2f;
					for (int i = 0; i < 6; i++)
					{
						if (!roomFloor.Field.Support(num3, num4, 0.1f, maxZ, 0.03f, out var z2, out var _))
						{
							break;
						}
						if (z2 < 0.04f)
						{
							break;
						}
						int key = Mathf.RoundToInt(z2 / 0.05f);
						sortedDictionary[key] = (sortedDictionary.TryGetValue(key, out var value) ? (value.Item1 + 1, value.Item2, value.Item3, value.Item4) : (1, num3, num4, z2));
						maxZ = z2 - 0.04f;
					}
				}
			}
			StringBuilder stringBuilder2 = new StringBuilder();
			foreach (KeyValuePair<int, (int, float, float, float)> item in sortedDictionary)
			{
				float num5 = (float)item.Value.Item1 * 0.05f * 0.05f;
				if (!(num5 < 0.01f))
				{
					stringBuilder2.Append(string.Format("{0:F2} ({1:F2} t2, {2}); ", (float)item.Key * 0.05f, num5, PieceAt(room, item.Value.Item2, item.Value.Item3, item.Value.Item4) ?? "?"));
				}
			}
			Log.Info("SIZES roomtops " + label + ": " + ((stringBuilder2.Length == 0) ? "none" : stringBuilder2.ToString()));
		}

		private static string PieceAt(Interior room, float x, float y, float z)
		{
			foreach (MeshInstance mesh in room.Meshes)
			{
				if (mesh?.Model == null)
				{
					continue;
				}
				Vec3 c = mesh.Basis.C0;
				Vec3 c2 = mesh.Basis.C1;
				float num = c.X * c.X + c.Y * c.Y;
				float num2 = c2.X * c2.X + c2.Y * c2.Y;
				float z2 = mesh.Basis.C2.Z;
				if (num < 1E-08f || num2 < 1E-08f || Mathf.Abs(z2) < 1E-06f)
				{
					continue;
				}
				float num3 = x - mesh.Position.X;
				float num4 = y - mesh.Position.Y;
				float num5 = (num3 * c.X + num4 * c.Y) / num;
				float num6 = (num3 * c2.X + num4 * c2.Y) / num2;
				float num7 = (z - mesh.Position.Z) / z2;
				MeshModel model = mesh.Model;
				if (!(num5 < model.Min.X - 0.01f) && !(num5 > model.Max.X + 0.01f) && !(num6 < model.Min.Y - 0.01f) && !(num6 > model.Max.Y + 0.01f) && !(num7 < model.Min.Z - 0.05f) && !(num7 > model.Max.Z + 0.05f))
				{
					string text = SurfaceAt(model, num5, num6, num7, 0.06f / Mathf.Abs(z2));
					if (text != null)
					{
						return model.Name + ":" + text;
					}
				}
			}
			return null;
		}

		private IEnumerator Nature()
		{
			Actor host = _mod.Host;
			if (host == null)
			{
				yield break;
			}
			Place(host, new Vector2((float)_home.x + 0.5f, (float)_home.y - 6.5f));
			Dictionary<Building, (string id, string family, string look, float h, float w, float r, float s)> seen = new Dictionary<Building, (string, string, string, float, float, float, float)>();
			for (int q = 0; q < 8; q++)
			{
				_mod.SetYaw((float)q * MathF.PI * 0.25f);
				_mod.SetPitchDegrees(0f);
				yield return Wait(0.6f);
				Vector2 current_position = host.current_position;
				foreach (Near item in Nears())
				{
					if (item.B != null && item.Mesh != null && item.Mesh.Model != null && !seen.ContainsKey(item.B) && (!(item.Look != "Tree") || !(item.Look != "Lump")) && item.B.current_tile != null && !((new Vector2(item.B.current_tile.x, item.B.current_tile.y) - current_position).magnitude > 40f))
					{
						MeshModel meshModel = Models.Get(item.Family) ?? item.Mesh.Model;
						float z = item.Mesh.Basis.C2.Z;
						float num = Len(item.Mesh.Basis.C0);
						seen[item.B] = (item.B.asset.id, item.Family, item.Look, meshModel.Max.Z * z, Mathf.Max(meshModel.Max.X - meshModel.Min.X, meshModel.Max.Y - meshModel.Min.Y) * num, item.Radius, z);
					}
				}
			}
			foreach (IGrouping<string, (string, string, string, float, float, float, float)> item2 in (from v in seen.Values
				group v by v.id + " -> " + v.family).OrderBy((IGrouping<string, (string id, string family, string look, float h, float w, float r, float s)> g) => g.Key, StringComparer.Ordinal))
			{
				List<(string, string, string, float, float, float, float)> list = item2.ToList();
				Log.Info($"SIZES nature {item2.Key} ({list[0].Item3}): {list.Count} seen, {list.Min<(string, string, string, float, float, float, float)>(((string id, string family, string look, float h, float w, float r, float s) v) => v.h):F2}..{list.Max<(string, string, string, float, float, float, float)>(((string id, string family, string look, float h, float w, float r, float s) v) => v.h):F2} tall (mean {list.Average<(string, string, string, float, float, float, float)>(((string id, string family, string look, float h, float w, float r, float s) v) => v.h):F2}), {list.Average<(string, string, string, float, float, float, float)>(((string id, string family, string look, float h, float w, float r, float s) v) => v.w):F2} across, " + $"collider radius {list.Average<(string, string, string, float, float, float, float)>(((string id, string family, string look, float h, float w, float r, float s) v) => v.r):F2}, scale {list.Average<(string, string, string, float, float, float, float)>(((string id, string family, string look, float h, float w, float r, float s) v) => v.s):F3} tiles a unit");
			}
			Log.Info($"SIZES nature: {seen.Count} trees, bushes and rocks drawn as models round {_home.x},{_home.y}; trees grow x{2.25f} their sprites' height, {1.8f} wide");
		}

		private void Summary()
		{
			Log.Info($"SIZES summary: {_bodyRows.Count} body lines, {_buildingRows.Count} buildings");
			foreach (string bodyRow in _bodyRows)
			{
				Log.Info("SIZES sum body " + bodyRow);
			}
			foreach (string buildingRow in _buildingRows)
			{
				Log.Info("SIZES sum bld " + buildingRow);
			}
		}
	}
}
