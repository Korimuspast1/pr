using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class WorldSampler
	{
		private sealed class LandformsJob
		{
			public int World;

			public int Width;

			public int Height;

			public byte[] Codes;

			public Landforms Forms;

			public byte[] Sea;

			public List<int> Changed;

			public bool Mark;

			public short[] Gen;
		}

		private struct TypeInfo
		{
			public bool Known;

			public int HeightMin;

			public int BandTop;

			public bool Liquid;

			public bool Lava;

			public bool Pit;

			public bool Wall;

			public uint Color;

			public byte Material;
		}

		private struct Cone
		{
			public float X;

			public float Y;

			public float Scale;

			public bool Molten;
		}

		private sealed class SeabedJob
		{
			public int World;

			public int Width;

			public int Height;

			public byte[] Bands;

			public byte[] Depths;
		}

		private enum StandShape : byte
		{
			None,
			Tile,
			Crown,
			Footprint,
			Dome,
			Cone
		}

		private struct Stand
		{
			public StandShape Shape;

			public float Height;

			public float Radius;

			public float Up;

			public int Left;

			public int Right;

			public int Top;

			public int Bottom;
		}

		private Landforms _formsSpare = new Landforms();

		private int _landWorld;

		private bool _formsBusy;

		private volatile LandformsJob _formsDone;

		public readonly TerrainCache Cache = new TerrainCache
		{
			Seasonal = true
		};

		private double _readMs;

		private double _lightMs;

		private long _tilesRead;

		public float MaxHeight = 100f;

		private WorldTile[] _tiles = new WorldTile[0];

		private object _tilesMapRef;

		private byte[] _rawMaterial = new byte[0];

		private bool _eyeLit;

		private int _eyeCx;

		private int _eyeCy;

		private Vec3 _eyeLight;

		private float _eyeMaxHeight;

		private bool _eyeShadows;

		private int _eyeSunKey;

		private TypeInfo[] _types = new TypeInfo[0];

		private int _nextRow;

		private float _cycleMax;

		private byte[] _codes = new byte[0];

		private byte[] _codesBefore = new byte[0];

		private Landforms _forms = new Landforms();

		private byte[] _holes = new byte[0];

		private readonly List<int> _holesChanged = new List<int>();

		private readonly List<Cone> _cones = new List<Cone>();

		private readonly List<Cone> _conesBefore = new List<Cone>();

		private readonly List<Cone> _vents = new List<Cone>();

		private float _featureTimer;

		private const float FeatureSeconds = 8f;

		public readonly List<Vector3> Craters = new List<Vector3>();

		public Vec3 SweepLight = new Vec3(-0.45f, 0.55f, 0.7f).Normalized();

		private Vec3 _sweepStart = new Vec3(-0.45f, 0.55f, 0.7f).Normalized();

		private const int HeightChanged = 1;

		private const int LookChanged = 2;

		private static bool[] _glowTops;

		private static readonly uint TilledEarth = TerrainCache.Rgb(112, 82, 56);

		private static readonly uint CropGreen = TerrainCache.Rgb(98, 138, 56);

		private static readonly uint CropGold = TerrainCache.Rgb(206, 170, 86);

		private static readonly uint VolcanicRock = TerrainCache.Rgb(62, 52, 50);

		private static readonly uint MountainRock = TerrainCache.Rgb(132, 126, 120);

		private static readonly uint SnowColor = TerrainCache.Rgb(238, 243, 250);

		private static readonly uint MoltenColor = TerrainCache.Rgb(255, 104, 18);

		internal const int DumpMagic = 1145979212;

		public static bool Quick = true;

		private const long Unknown = -1L;

		private long[] _sig = new long[0];

		private object[] _sigBuilding = new object[0];

		private byte[] _crop = new byte[0];

		private float[] _depthsBefore = new float[0];

		private long _fullReads;

		internal double TotalReadMs;

		internal double TotalLightMs;

		internal long TotalTiles;

		public readonly Relief Relief = new Relief();

		private int[] _sigRelief = new int[0];

		private readonly List<int> _reliefChanged = new List<int>();

		private static readonly uint TurnedEarth = TerrainCache.Rgb(92, 66, 44);

		private short[] _genTarget;

		private short[] _genRaw;

		private bool[] _pins;

		private float[] _pinDist;

		private int[] _pinFrom;

		private float _easeTimer;

		private float _pinTimer;

		private const float EaseEvery = 0.4f;

		private const float PinEvery = 3f;

		private const int PinMargin = 2;

		private const float PinFade = 4f;

		internal int EasedForTest;

		internal int EaseLeftForTest;

		internal int PinnedForTest;

		private byte[] _bands = new byte[0];

		private byte[] _seaDepth = new byte[0];

		private bool _seabedDirty;

		private bool _seabedBusy;

		private int _seabedWorld;

		private long _seabedStarted;

		private volatile SeabedJob _seabedDone;

		private const double SeabedEvery = 1.0;

		private float _lowest = -1.5f;

		private const float SunBakeSeconds = 1f;

		private const float SunHeightsSeconds = 0.25f;

		private const float SunJumpCos = 0.99863f;

		private const float SunFaintLight = 0.01f;

		private const float CanopySeconds = 2f;

		private const double CanopyBudgetMs = 0.5;

		private const float CanopyHoldSeconds = 5f;

		private const float SpriteSeen = 0.85f;

		private const float TreeGrowFullTo = 6.5f;

		private const float GiantTreeTall = 15.5f;

		private int _heightsVersion;

		private Task<SunShadowField> _sunBake;

		private bool _sunBakeSnap;

		private bool _sunBakePatch;

		private bool _sunBakeRelights;

		private bool _sunBakeStale;

		private bool _sunBakeHadCanopy;

		private bool _sunAsked;

		private bool _sunHeld;

		private bool _sunFailed;

		private bool _sunFailLogged;

		private bool _sunLogged;

		private bool _sunRelightOwed;

		private object _sunBakeWorld;

		private Vec3 _sunBakeLight;

		private Vec3 _sunTickLight;

		private bool _sunTickSet;

		private float _sunBakeAt = -1E+09f;

		private float _sunPublishedAt;

		private int _sunBakeHeights = -1;

		private int _sunPublishes;

		private long _sunBakeCanopy;

		private float[] _sunHeights = new float[0];

		private float[] _sunCanopy = new float[0];

		private readonly List<(SunShadowField field, int at)> _sunSpares = new List<(SunShadowField, int)>();

		internal double SunBakeMs;

		internal double CanopyMs;

		internal int CanopyCount;

		internal int SunPatches;

		private float _canopyWantedUntil = -1E+09f;

		private float[] _canopy = new float[0];

		private float[] _canopyNext = new float[0];

		private long _canopyPrint;

		private long _canopyNextPrint;

		private bool _canopyReady;

		private bool _canopyUnits;

		private int _canopyAt = -1;

		private int _canopyW;

		private int _canopyH;

		private int _canopyNextCount;

		private float _canopyBegun = -1E+09f;

		private double _canopyPassMs;

		private bool _canopyFailed;

		private bool _canopyTaken;

		private readonly List<Building> _canopyBuildings = new List<Building>();

		private readonly List<Actor> _canopyActors = new List<Actor>();

		private readonly Dictionary<BuildingAsset, Stand> _stands = new Dictionary<BuildingAsset, Stand>();

		private readonly Dictionary<ActorAsset, float> _actorTall = new Dictionary<ActorAsset, float>();

		private static bool VentModels => Models.Get("nature_volcano") != null;

		public bool Ready => _tilesMapRef != null;

		internal Landforms Forms => _forms;

		internal WorldTile[] Tiles => _tiles;

		internal bool CanopyWanted => Time.unscaledTime < _canopyWantedUntil;

		private void StartLandformsAside(int w, int h)
		{
			if (_formsBusy)
			{
				return;
			}
			int n = w * h;
			LandformsJob job = new LandformsJob
			{
				World = _landWorld,
				Width = w,
				Height = h,
				Codes = (byte[])_codes.Clone(),
				Forms = _formsSpare,
				Mark = (_codesBefore.Length == n)
			};
			bool terrainy = Relief.Terrainy;
			int seed = Relief.Seed;
			Landforms before = _forms;
			_formsBusy = true;
			SlowLane.Run(delegate
			{
				try
				{
					job.Forms.Compute(job.Codes, w, h);
					job.Sea = TerrainCache.SeaReachOf(job.Codes, 0, w, h);
					if (job.Mark)
					{
						job.Changed = ChangedDepths(before, job.Forms, n);
					}
					if (terrainy)
					{
						job.Gen = GenerateFor(w, h, job.Codes, job.Forms, job.Sea, seed);
					}
				}
				catch (Exception ex)
				{
					job.Forms = null;
					Log.Error("landform scan beside the game failed: " + ex.Message);
				}
				_formsDone = job;
			});
		}

		private static List<int> ChangedDepths(Landforms a, Landforms b, int n)
		{
			List<int> list = new List<int>();
			if (a.Form.Length != n || b.Form.Length != n)
			{
				return null;
			}
			for (int i = 0; i < n; i++)
			{
				if (a.LandDepth[i] != b.LandDepth[i] || a.HillDepth[i] != b.HillDepth[i] || a.MountainDepth[i] != b.MountainDepth[i] || a.SummitDepth[i] != b.SummitDepth[i] || a.RangeSize[i] != b.RangeSize[i] || a.Form[i] != b.Form[i])
				{
					list.Add(i);
				}
			}
			return list;
		}

		private void TakeLandformsAside()
		{
			LandformsJob formsDone = _formsDone;
			if (formsDone == null)
			{
				return;
			}
			_formsDone = null;
			_formsBusy = false;
			int width = Cache.Width;
			int height = Cache.Height;
			int num = width * height;
			if (formsDone.Forms == null || formsDone.World != _landWorld || formsDone.Width != width || formsDone.Height != height || _codes.Length != num)
			{
				return;
			}
			_formsSpare = _forms;
			_forms = formsDone.Forms;
			Cache.ApplySeaReach(formsDone.Sea);
			if (formsDone.Gen != null && Relief.Terrainy)
			{
				TakeGen(formsDone.Gen);
			}
			if (formsDone.Mark)
			{
				if (formsDone.Changed == null)
				{
					ResetFingerprints(num);
				}
				else if (_sig.Length == num)
				{
					foreach (int item in formsDone.Changed)
					{
						_sig[item] = -1L;
					}
				}
			}
			if (_codesBefore.Length != num)
			{
				_codesBefore = new byte[num];
			}
			Buffer.BlockCopy(formsDone.Codes, 0, _codesBefore, 0, num);
		}

		public (double read, double light, long tiles, long full) TakeTimes()
		{
			(double, double, long, long) result = (_readMs, _lightMs, _tilesRead, TakeFullReads());
			TotalReadMs += _readMs;
			TotalLightMs += _lightMs;
			TotalTiles += _tilesRead;
			_readMs = (_lightMs = 0.0);
			_tilesRead = 0L;
			return result;
		}

		private static double Ms(long from)
		{
			return (double)(Stopwatch.GetTimestamp() - from) * 1000.0 / (double)Stopwatch.Frequency;
		}

		public bool EnsureWorld()
		{
			MapBox world = World.world;
			if (world == null || world.tiles_map == null)
			{
				return false;
			}
			int width = MapBox.width;
			int height = MapBox.height;
			if (width <= 0 || height <= 0)
			{
				return false;
			}
			WorldTile[,] tiles_map = world.tiles_map;
			if (tiles_map == _tilesMapRef && width == Cache.Width && height == Cache.Height && tiles_map[0, 0] == _tiles[0])
			{
				return true;
			}
			if (tiles_map.GetLength(0) < width || tiles_map.GetLength(1) < height)
			{
				return false;
			}
			BuildTypeTable();
			Cache.Allocate(width, height);
			Relief.Allocate(width, height);
			ResetReliefPrints(width * height);
			ForgetSunField();
			_rawMaterial = new byte[width * height];
			for (int i = 0; i < _rawMaterial.Length; i++)
			{
				_rawMaterial[i] = byte.MaxValue;
			}
			_eyeLit = false;
			_tiles = new WorldTile[width * height];
			for (int j = 0; j < height; j++)
			{
				for (int k = 0; k < width; k++)
				{
					_tiles[j * width + k] = tiles_map[k, j];
				}
			}
			_tilesMapRef = world.tiles_map;
			_landWorld++;
			_nextRow = 0;
			_cycleMax = 0f;
			Stopwatch stopwatch = Stopwatch.StartNew();
			_codes = new byte[width * height];
			_codesBefore = new byte[0];
			_holes = new byte[width * height];
			_holesChanged.Clear();
			try
			{
				ReliefWorld.Load(this);
			}
			catch (Exception ex)
			{
				Log.Error("relief: could not load: " + ex.Message);
			}
			UpdateLandforms(refreshChanged: false);
			LaySeabed();
			ResetFingerprints(width * height);
			RefreshRect(0, 0, width, height);
			MaxHeight = _cycleMax + 0.5f;
			Cache.MaxHeight = MaxHeight + 0.45f;
			UpdateCraters();
			Log.Info($"terrain cache built for {width}x{height} tiles in {stopwatch.ElapsedMilliseconds} ms, max height {MaxHeight:F1}");
			return true;
		}

		public void Invalidate()
		{
			_tilesMapRef = null;
		}

		internal void ScanLandformsForTest()
		{
			UpdateLandforms(refreshChanged: true);
		}

		public void RelightAll()
		{
			Cache.Relight(0, 0, Cache.Width, Cache.Height);
			SweepLight = (_sweepStart = Cache.LightDir);
		}

		public void Tick(float eyeX, float eyeY)
		{
			TakeReliefChanges();
			TakeSeabed();
			int bandChanges = SweepBand();
			TickLandforms(Time.unscaledDeltaTime);
			RefreshAroundEye(eyeX, eyeY, bandChanges);
		}

		public void TickTiles(float eyeX, float eyeY, bool eye)
		{
			TakeReliefChanges();
			TakeSeabed();
			int bandChanges = SweepBand();
			if (eye)
			{
				RefreshAroundEye(eyeX, eyeY, bandChanges);
			}
		}

		public void TickLandforms(float dt)
		{
			TakeLandformsAside();
			EaseRelief(dt);
			_featureTimer += dt;
			if (_featureTimer >= 8f)
			{
				_featureTimer = 0f;
				UpdateLandforms(refreshChanged: true, aside: true);
			}
		}

		private int SweepBand()
		{
			int width = Cache.Width;
			int height = Cache.Height;
			int num = Math.Max(2, Math.Min(height, 6000 / Math.Max(1, width)));
			int nextRow = _nextRow;
			int num2 = Math.Min(height, nextRow + num);
			int result = RefreshRect(0, nextRow, width, num2);
			_nextRow = ((num2 < height) ? num2 : 0);
			if (_nextRow == 0)
			{
				SweepLight = _sweepStart;
				_sweepStart = Cache.LightDir;
				MaxHeight = _cycleMax + 0.5f;
				Cache.MaxHeight = MaxHeight + 0.45f;
				_cycleMax = 0f;
			}
			return result;
		}

		private void RefreshAroundEye(float eyeX, float eyeY, int bandChanges)
		{
			int width = Cache.Width;
			int height = Cache.Height;
			int num = (int)eyeX;
			int num2 = (int)eyeY;
			int num3 = Math.Max(0, num - 28);
			int num4 = Math.Max(0, num2 - 28);
			int num5 = Math.Min(width, num + 28);
			int num6 = Math.Min(height, num2 + 28);
			if (num3 >= num5 || num4 >= num6)
			{
				return;
			}
			int num7 = RefreshTiles(num3, num4, num5, num6, writeMaterials: false);
			if ((num7 & 1) != 0)
			{
				Cache.UpdateBlocks(num3 - 1, num4 - 1, num5 + 1, num6 + 1);
			}
			Vec3 lightDir = Cache.LightDir;
			int num8 = SunKey();
			if (num7 != 0 || (bandChanges & 1) != 0 || !_eyeLit || num != _eyeCx || num2 != _eyeCy || Cache.MaxHeight != _eyeMaxHeight || Cache.CastShadows != _eyeShadows || lightDir.X != _eyeLight.X || lightDir.Y != _eyeLight.Y || lightDir.Z != _eyeLight.Z || num8 != _eyeSunKey)
			{
				long timestamp = Stopwatch.GetTimestamp();
				for (int i = num4; i < num6; i++)
				{
					Array.Copy(_rawMaterial, i * width + num3, Cache.Materials, i * width + num3, num5 - num3);
				}
				Cache.Relight(num3 - 1, num4 - 1, num5 + 1, num6 + 1);
				_lightMs += Ms(timestamp);
				_eyeLit = true;
				_eyeCx = num;
				_eyeCy = num2;
				_eyeLight = lightDir;
				_eyeMaxHeight = Cache.MaxHeight;
				_eyeShadows = Cache.CastShadows;
				_eyeSunKey = num8;
			}
		}

		private int RefreshRect(int x0, int y0, int x1, int y1)
		{
			int width = Cache.Width;
			int height = Cache.Height;
			x0 = Math.Max(0, x0);
			y0 = Math.Max(0, y0);
			x1 = Math.Min(width, x1);
			y1 = Math.Min(height, y1);
			if (x0 >= x1 || y0 >= y1)
			{
				return 0;
			}
			int result = RefreshTiles(x0, y0, x1, y1, writeMaterials: true);
			long timestamp = Stopwatch.GetTimestamp();
			Cache.UpdateBlocks(x0 - 1, y0 - 1, x1 + 1, y1 + 1);
			Cache.Relight(x0 - 1, y0 - 1, x1 + 1, y1 + 1);
			_lightMs += Ms(timestamp);
			return result;
		}

		private int RefreshTiles(int x0, int y0, int x1, int y1, bool writeMaterials)
		{
			long timestamp = Stopwatch.GetTimestamp();
			int width = Cache.Width;
			int num = 0;
			bool[] fires = FiresNow();
			for (int i = y0; i < y1; i++)
			{
				for (int j = x0; j < x1; j++)
				{
					num |= ReadTile(i * width + j, j, i, writeMaterials, fires);
				}
			}
			if (_holesChanged.Count > 0)
			{
				num |= RoundHoles();
			}
			_readMs += Ms(timestamp);
			_tilesRead += (long)(x1 - x0) * (long)(y1 - y0);
			return num;
		}

		private void UpdateLandforms(bool refreshChanged, bool aside = false)
		{
			int width = Cache.Width;
			int height = Cache.Height;
			int num = width * height;
			for (int i = 0; i < num; i++)
			{
				WorldTile worldTile = _tiles[i];
				TypeInfo main = ((worldTile != null) ? Info(worldTile.main_type) : default(TypeInfo));
				_codes[i] = (Relief.IsLake(i) ? LakeCode(i, worldTile, main) : Code(worldTile, main));
				if (!refreshChanged && i < _holes.Length)
				{
					_holes[i] = (byte)(((worldTile != null && IsHole(main, Info(worldTile.cur_tile_type ?? worldTile.main_type))) ? 1 : 0) | (Relief.Shaped(i) ? 4 : 0));
				}
			}
			bool flag = _codesBefore.Length != num;
			if (!flag)
			{
				for (int j = 0; j < num; j++)
				{
					if (_codes[j] != _codesBefore[j])
					{
						flag = true;
						break;
					}
				}
			}
			if (flag && aside)
			{
				StartLandformsAside(width, height);
			}
			else if (flag)
			{
				_landWorld++;
				int num2;
				if (refreshChanged)
				{
					num2 = ((_codesBefore.Length == num) ? 1 : 0);
					if (num2 != 0)
					{
						KeepDepthsBefore();
					}
				}
				else
				{
					num2 = 0;
				}
				_forms.Compute(_codes, width, height);
				Cache.UpdateSeaReach(_codes, 0);
				GenerateNow();
				if (num2 != 0)
				{
					ForgetChangedDepths();
				}
				if (_codesBefore.Length != num)
				{
					_codesBefore = new byte[num];
				}
				Buffer.BlockCopy(_codes, 0, _codesBefore, 0, num);
				if (!refreshChanged)
				{
					for (int k = 0; k < num && k < _holes.Length; k++)
					{
						if (_tiles[k] != null && OutOfPlace(Info(_tiles[k].main_type), k))
						{
							_holes[k] |= 2;
						}
					}
				}
			}
			_conesBefore.Clear();
			_conesBefore.AddRange(_cones);
			_cones.Clear();
			_vents.Clear();
			bool ventModels = VentModels;
			try
			{
				foreach (Building simple in World.world.buildings.getSimpleList())
				{
					if (simple != null && simple.asset != null && simple.current_tile != null && simple.isAlive())
					{
						string text = simple.asset.id ?? "";
						if (text.Contains("volcano"))
						{
							(ventModels ? _vents : _cones).Add(new Cone
							{
								X = (float)simple.current_tile.x + 0.5f,
								Y = (float)simple.current_tile.y + 0.5f,
								Scale = 1f,
								Molten = !text.Contains("dormant")
							});
						}
						else if (text.Contains("geyser") && !ventModels)
						{
							_cones.Add(new Cone
							{
								X = (float)simple.current_tile.x + 0.5f,
								Y = (float)simple.current_tile.y + 0.5f,
								Scale = 0.3f
							});
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("volcano scan failed: " + ex.Message);
			}
			UpdateCraters();
			if (!refreshChanged)
			{
				return;
			}
			int num3 = (int)Math.Ceiling(11.0) + 1;
			foreach (Cone c in _cones)
			{
				if (!_conesBefore.Exists((Cone o) => o.X == c.X && o.Y == c.Y))
				{
					Forget((int)c.X - num3, (int)c.Y - num3, (int)c.X + num3, (int)c.Y + num3);
					RefreshRect((int)c.X - num3, (int)c.Y - num3, (int)c.X + num3, (int)c.Y + num3);
				}
			}
			foreach (Cone c2 in _conesBefore)
			{
				if (!_cones.Exists((Cone o) => o.X == c2.X && o.Y == c2.Y))
				{
					Forget((int)c2.X - num3, (int)c2.Y - num3, (int)c2.X + num3, (int)c2.Y + num3);
					RefreshRect((int)c2.X - num3, (int)c2.Y - num3, (int)c2.X + num3, (int)c2.Y + num3);
				}
			}
		}

		private void UpdateCraters()
		{
			Craters.Clear();
			foreach (Cone cone in _cones)
			{
				if (cone.Molten)
				{
					int num = Mathf.Clamp((int)cone.X, 0, Cache.Width - 1);
					int num2 = Mathf.Clamp((int)cone.Y, 0, Cache.Height - 1);
					Craters.Add(new Vector3(cone.X, cone.Y, (Cache.Ground.Length != 0) ? Cache.Ground[num2 * Cache.Width + num] : 0f));
				}
			}
			foreach (Cone vent in _vents)
			{
				if (vent.Molten)
				{
					Vector3 item = SceneCollector.VolcanoVent(vent.X, vent.Y, Cache);
					Craters.Add(item);
				}
			}
		}

		private float ConeLift(int x, int y, out bool molten, out float basalt)
		{
			molten = false;
			basalt = 0f;
			float num = 0f;
			for (int i = 0; i < _cones.Count; i++)
			{
				Cone cone = _cones[i];
				float num2 = (float)x + 0.5f - cone.X;
				float num3 = (float)y + 0.5f - cone.Y;
				float num4 = 11f * cone.Scale;
				if (!(num2 * num2 + num3 * num3 >= num4 * num4))
				{
					float num5 = (float)Math.Sqrt(num2 * num2 + num3 * num3);
					bool crater;
					float num6 = TerrainModel.VolcanoCone(num5 / cone.Scale, out crater) * cone.Scale;
					if (num6 > num)
					{
						num = num6;
					}
					if (cone.Scale >= 1f)
					{
						basalt = Math.Max(basalt, 1f - num5 / num4);
					}
					if (cone.Molten && (crater || TerrainModel.LavaStream(num2, num3)))
					{
						molten = true;
					}
				}
			}
			return num;
		}

		private int RefreshTile(int i, int x, int y, bool writeMaterial)
		{
			WorldTile worldTile = _tiles[i];
			if (worldTile == null)
			{
				return 0;
			}
			TileType main_type = worldTile.main_type;
			TileTypeBase tileTypeBase = worldTile.cur_tile_type ?? main_type;
			TopTileType top_type = worldTile.top_type;
			TypeInfo typeInfo = Info(main_type);
			TypeInfo current = Info(tileTypeBase);
			bool flag = top_type != null && Info(top_type).Wall;
			bool molten = false;
			float basalt = 0f;
			float num = 0f;
			if (_cones.Count > 0)
			{
				num = ConeLift(x, y, out molten, out basalt);
			}
			bool flag2 = typeInfo.Liquid && num < 2f;
			if (flag2)
			{
				num = 0f;
				molten = false;
				basalt = 0f;
			}
			TerrainModel.TileInput t = new TerrainModel.TileInput
			{
				X = x,
				Y = y,
				HeightMin = typeInfo.HeightMin,
				BandTop = typeInfo.BandTop,
				DataHeight = ((worldTile.data != null) ? worldTile.data.height : typeInfo.HeightMin),
				Liquid = flag2,
				Lava = (typeInfo.Lava || current.Lava),
				Pit = typeInfo.Pit,
				VolcanoLift = num,
				Relief = Relief.GenOf(i),
				Dug = Relief.EditOf(i),
				Terraces = Relief.Terraces,
				RollLess = Relief.RollLess
			};
			if (i < _forms.Form.Length)
			{
				t.LandDepth = _forms.LandDepth[i];
				t.HillDepth = _forms.HillDepth[i];
				t.MountainDepth = _forms.MountainDepth[i];
				t.SummitDepth = _forms.SummitDepth[i];
				t.RangeSize = _forms.RangeSize[i];
				t.Form = _forms.Form[i];
			}
			float ground = TerrainModel.ComputeHeight(in t);
			float floor = ground;
			bool flag3 = flag2 && LakeBed(i, in t, ref ground, ref floor);
			SeabedUnder(i, typeInfo, flag2, t.Lava, ref ground, ref floor);
			float num2 = (flag ? (ground + 3.4f) : ground);
			int num3 = 0;
			if (Cache.Ground[i] != ground || Cache.Heights[i] != num2 || Cache.Floor[i] != floor)
			{
				Cache.Ground[i] = ground;
				Cache.Heights[i] = num2;
				Cache.Floor[i] = floor;
				num3 = 1;
				_heightsVersion++;
			}
			if (num2 > _cycleMax)
			{
				_cycleMax = num2;
			}
			uint color = (current.Known ? current.Color : typeInfo.Color);
			byte material = ((top_type != null && current.Known) ? current.Material : typeInfo.Material);
			if (worldTile.burned_stages > 0)
			{
				color = TerrainCache.Scale(color, 1f - Math.Min(1f, (float)worldTile.burned_stages / 15f) * 0.55f);
			}
			if (material == 7)
			{
				color = TerrainCache.Lerp(color, TilledEarth, 0.55f);
			}
			Building building = worldTile.building;
			bool flag4 = building != null && building.asset != null && building.asset.building_type == BuildingType.Building_Wheat;
			if (i < _crop.Length)
			{
				_crop[i] = (flag4 ? ((byte)1) : ((byte)0));
			}
			if (flag4)
			{
				float num4 = SceneCollector.CropGrowth(building);
				uint b = TerrainCache.Lerp(CropGreen, CropGold, Math.Max(0f, Math.Min(1f, (num4 - 0.5f) / 0.45f)));
				color = TerrainCache.Lerp(color, b, 0.15f + 0.5f * num4);
			}
			if (!flag2 && !t.Lava && !flag)
			{
				if (typeInfo.HeightMin >= 199 && material == 4)
				{
					color = TerrainCache.Lerp(color, MountainRock, 0.42f);
				}
				if (basalt > 0f)
				{
					float num5 = Math.Min(1f, basalt * 1.6f) * 0.85f;
					color = TerrainCache.Lerp(color, VolcanicRock, num5);
					if (num5 > 0.4f)
					{
						material = 4;
					}
				}
				if (ground > 18f)
				{
					float num6 = (ground - 18f) / 5f + (TerrainModel.Fbm(x * 3, y * 3) - 0.5f) * 0.7f;
					if (num6 > 0f)
					{
						color = TerrainCache.Lerp(color, SnowColor, Math.Min(1f, num6));
						if (num6 > 0.45f)
						{
							material = 5;
						}
					}
				}
			}
			if (molten)
			{
				color = MoltenColor;
			}
			else if (!flag2 && !flag)
			{
				RawEarth(i, ref color, ref material);
			}
			if (Cache.BaseColors[i] != color || _rawMaterial[i] != material)
			{
				Cache.BaseColors[i] = color;
				_rawMaterial[i] = material;
				num3 |= 2;
			}
			if (writeMaterial)
			{
				Cache.Materials[i] = material;
			}
			if (i < Cache.SeasonMaterials.Length)
			{
				Cache.SeasonMaterials[i] = material;
			}
			byte b2 = 0;
			if (t.Lava || molten)
			{
				b2 |= 2;
			}
			else if (flag2)
			{
				b2 |= 1;
				if (tileTypeBase != null && tileTypeBase.id == "ice")
				{
					b2 |= 0x80;
				}
				WorldTile[] neighbours = worldTile.neighbours;
				if (neighbours != null)
				{
					WorldTile[] array = neighbours;
					foreach (WorldTile worldTile2 in array)
					{
						if (worldTile2 != null && worldTile2.main_type != null && !Info(worldTile2.main_type).Liquid)
						{
							b2 |= 0x10;
							break;
						}
					}
				}
			}
			if (flag)
			{
				b2 |= 8;
			}
			if (worldTile.isOnFire())
			{
				b2 |= 4;
			}
			if (_holes.Length == Cache.Ground.Length)
			{
				byte b3 = (byte)((IsHole(typeInfo, current) ? 1u : 0u) | (uint)(OutOfPlace(typeInfo, i) ? 2 : 0) | (uint)(Relief.Shaped(i) ? 4 : 0));
				if (_holes[i] != b3)
				{
					_holes[i] = b3;
					_holesChanged.Add(i);
				}
				if ((b2 & 3) == 0 && TerrainCache.NearHole(_holes, Cache.Width, Cache.Height, x, y))
				{
					b2 |= 0x40;
				}
			}
			if ((Cache.Flags[i] & -33) != b2)
			{
				Cache.Flags[i] = b2;
				num3 |= 2;
			}
			if (i < Cache.Marks.Length)
			{
				byte b4 = Cache.Marks[i];
				byte b5 = (byte)((!flag2 && Relief.Shaped(i)) ? (b4 | 1) : (b4 & -2));
				b5 = (byte)(flag3 ? (b5 | 4) : (b5 & -5));
				b5 = (byte)((!flag2 && Glows(worldTile.top_type)) ? (b5 | 8) : (b5 & -9));
				if (b5 != b4)
				{
					Cache.Marks[i] = b5;
					num3 |= 2;
				}
			}
			return num3;
		}

		private static bool Glows(TopTileType top)
		{
			if (top == null)
			{
				return false;
			}
			if (_glowTops == null)
			{
				List<bool> list = new List<bool>();
				try
				{
					foreach (TopTileType item in AssetManager.top_tiles.list)
					{
						while (list.Count <= item.index_id)
						{
							list.Add(item: false);
						}
						BiomeKind biomeKind = BiomeMap.FromTileId(item.id);
						list[item.index_id] = biomeKind == BiomeKind.Crystal || biomeKind == BiomeKind.Enchanted || biomeKind == BiomeKind.Infernal || biomeKind == BiomeKind.Mushroom || biomeKind == BiomeKind.Celestial || biomeKind == BiomeKind.Singularity || biomeKind == BiomeKind.Cybertile;
					}
				}
				catch
				{
				}
				_glowTops = list.ToArray();
			}
			int index_id = top.index_id;
			if (index_id >= 0 && index_id < _glowTops.Length)
			{
				return _glowTops[index_id];
			}
			return false;
		}

		private static bool IsHole(TypeInfo main, TypeInfo current)
		{
			if (main.Pit && !main.Liquid && !main.Lava)
			{
				return !current.Lava;
			}
			return false;
		}

		private bool OutOfPlace(TypeInfo main, int i)
		{
			if (i < _forms.Form.Length)
			{
				return Landforms.OutOfPlace(main.HeightMin, main.Liquid, _forms.Form[i]);
			}
			return false;
		}

		private static byte Code(WorldTile tile, TypeInfo main)
		{
			if (tile == null)
			{
				return 0;
			}
			if (main.Lava)
			{
				return 1;
			}
			if (main.Liquid)
			{
				return 0;
			}
			if (main.HeightMin < 230)
			{
				return (byte)TerrainModel.ClassOf(main.HeightMin);
			}
			return 4;
		}

		private int RoundHoles()
		{
			int width = Cache.Width;
			int height = Cache.Height;
			int num = 2;
			int num2 = 0;
			for (int i = 0; i < _holesChanged.Count; i++)
			{
				int num3 = _holesChanged[i];
				int num4 = num3 % width;
				int num5 = num3 / width;
				int num6 = Math.Max(0, num4 - num);
				int num7 = Math.Max(0, num5 - num);
				int num8 = Math.Min(width, num4 + num + 1);
				int num9 = Math.Min(height, num5 + num + 1);
				for (int j = num7; j < num9; j++)
				{
					for (int k = num6; k < num8; k++)
					{
						if (k != num4 || j != num5)
						{
							num2 |= RefreshTile(j * width + k, k, j, writeMaterial: true);
						}
					}
				}
				Cache.UpdateBlocks(num6, num7, num8, num9);
				Cache.Relight(num6 - 1, num7 - 1, num8 + 1, num9 + 1);
			}
			_holesChanged.Clear();
			return num2;
		}

		private TypeInfo Info(TileTypeBase type)
		{
			if (type == null)
			{
				return default(TypeInfo);
			}
			int index_id = type.index_id;
			if (index_id >= 0 && index_id < _types.Length && _types[index_id].Known)
			{
				return _types[index_id];
			}
			TypeInfo typeInfo = Describe(type, 255);
			if (index_id >= 0)
			{
				if (index_id >= _types.Length)
				{
					Array.Resize(ref _types, index_id + 1);
				}
				_types[index_id] = typeInfo;
			}
			return typeInfo;
		}

		private void BuildTypeTable()
		{
			List<TileTypeBase> list = new List<TileTypeBase>();
			if (AssetManager.tiles != null)
			{
				foreach (TileType item in AssetManager.tiles.list)
				{
					list.Add(item);
				}
			}
			if (AssetManager.top_tiles != null)
			{
				foreach (TopTileType item2 in AssetManager.top_tiles.list)
				{
					list.Add(item2);
				}
			}
			SortedSet<int> sortedSet = new SortedSet<int>();
			if (AssetManager.tiles != null)
			{
				foreach (TileType item3 in AssetManager.tiles.list)
				{
					if (item3.used_in_generator || item3.height_min > 0)
					{
						sortedSet.Add(item3.height_min);
					}
				}
			}
			int num = 0;
			foreach (TileTypeBase item4 in list)
			{
				num = Math.Max(num, item4.index_id);
			}
			_types = new TypeInfo[num + 1];
			foreach (TileTypeBase item5 in list)
			{
				int bandTop = 255;
				foreach (int item6 in sortedSet)
				{
					if (item6 > item5.height_min)
					{
						bandTop = item6;
						break;
					}
				}
				if (item5.index_id >= 0)
				{
					_types[item5.index_id] = Describe(item5, bandTop);
				}
			}
			Log.Info(string.Format("tile types: {0}, height bands: {1}", list.Count, string.Join(",", sortedSet)));
		}

		private static TypeInfo Describe(TileTypeBase t, int bandTop)
		{
			Color32 color = t.color;
			string text = t.id ?? "";
			return new TypeInfo
			{
				Known = true,
				HeightMin = t.height_min,
				BandTop = bandTop,
				Liquid = (t.liquid || t.ocean),
				Lava = t.lava,
				Pit = text.StartsWith("pit_", StringComparison.Ordinal),
				Wall = t.wall,
				Color = TerrainCache.Rgb(color.r, color.g, color.b),
				Material = MaterialOf(t, text)
			};
		}

		private static byte MaterialOf(TileTypeBase t, string id)
		{
			if (t.road)
			{
				return 6;
			}
			if (t.farm_field)
			{
				return 7;
			}
			if (id.Contains("snow") || id.Contains("ice") || id.Contains("frozen") || id.Contains("permafrost"))
			{
				return 5;
			}
			if (t.mountains || t.summit || t.rocks || id.Contains("hills") || id.Contains("mountain"))
			{
				return 4;
			}
			if (t.sand || id.Contains("sand") || id.Contains("desert"))
			{
				return 2;
			}
			if (t.grass || t.is_biome || id.Contains("grass") || id.EndsWith("_low", StringComparison.Ordinal) || id.EndsWith("_high", StringComparison.Ordinal))
			{
				if (!t.soil || t.is_biome)
				{
					return 1;
				}
				return 3;
			}
			if (t.soil || id.Contains("soil"))
			{
				return 3;
			}
			return 0;
		}

		internal bool OutOfPlaceForTest(int x, int y)
		{
			int width = Cache.Width;
			if (x < 0 || y < 0 || x >= width || y >= Cache.Height)
			{
				return false;
			}
			WorldTile worldTile = _tiles[y * width + x];
			if (worldTile != null)
			{
				return OutOfPlace(Info(worldTile.main_type), y * width + x);
			}
			return false;
		}

		internal int FormForTest(int x, int y)
		{
			if (x >= 0 && y >= 0 && x < Cache.Width && y < Cache.Height && _forms.Form.Length != 0)
			{
				return _forms.Form[y * Cache.Width + x];
			}
			return 0;
		}

		internal void DumpForLab(string path)
		{
			int width = Cache.Width;
			int height = Cache.Height;
			using (BinaryWriter binaryWriter = new BinaryWriter(File.Create(path)))
			{
				binaryWriter.Write(1145979212);
				binaryWriter.Write(1);
				binaryWriter.Write(width);
				binaryWriter.Write(height);
				for (int i = 0; i < width * height; i++)
				{
					WorldTile worldTile = _tiles[i];
					TypeInfo main = ((worldTile != null) ? Info(worldTile.main_type) : default(TypeInfo));
					TypeInfo current = ((worldTile != null) ? Info(worldTile.cur_tile_type ?? worldTile.main_type) : default(TypeInfo));
					bool flag = worldTile != null && worldTile.top_type != null && Info(worldTile.top_type).Wall;
					int num = (int)((main.Liquid ? 1u : 0u) | (uint)((main.Lava || current.Lava) ? 2 : 0) | (uint)(main.Pit ? 4 : 0) | (uint)(IsHole(main, current) ? 8 : 0) | (uint)(flag ? 16 : 0)) | (((Cache.Flags[i] & 4) != 0) ? 32 : 0);
					binaryWriter.Write((byte)main.HeightMin);
					binaryWriter.Write((byte)main.BandTop);
					binaryWriter.Write((short)((worldTile != null && worldTile.data != null) ? worldTile.data.height : main.HeightMin));
					binaryWriter.Write((byte)num);
					binaryWriter.Write(Cache.BaseColors[i]);
					binaryWriter.Write(_rawMaterial[i]);
					binaryWriter.Write(Cache.Ground[i]);
				}
			}
			Log.Info($"land written for the Lab: {path} ({width}x{height})");
		}

		private void ResetFingerprints(int n)
		{
			_sig = new long[n];
			for (int i = 0; i < n; i++)
			{
				_sig[i] = -1L;
			}
			_sigBuilding = new object[n];
			_crop = new byte[n];
		}

		private static bool[] FiresNow()
		{
			try
			{
				return World.world?.tile_manager?.fires;
			}
			catch
			{
				return null;
			}
		}

		private static long Fingerprint(WorldTile tile, bool[] fires)
		{
			TileTypeBase main_type = tile.main_type;
			TileTypeBase cur_tile_type = tile.cur_tile_type;
			TileTypeBase top_type = tile.top_type;
			WorldTileData data = tile.data;
			long num = ((main_type != null) ? (main_type.index_id + 1) : 0) & 0xFFF;
			num |= (long)(((cur_tile_type != null) ? (cur_tile_type.index_id + 1) : 0) & 0xFFF) << 12;
			num |= (long)(((top_type != null) ? (top_type.index_id + 1) : 0) & 0xFFF) << 24;
			if (data != null)
			{
				num |= (long)(data.height & 0xFFFF) << 36;
				int tile_id = data.tile_id;
				if (fires != null && tile_id >= 0 && tile_id < fires.Length && fires[tile_id])
				{
					num |= 0x1000000000000000L;
				}
			}
			return num | ((long)(tile.burned_stages & 0xFF) << 52);
		}

		private int ReadTile(int i, int x, int y, bool writeMaterial, bool[] fires)
		{
			WorldTile worldTile = _tiles[i];
			if (!Quick || worldTile == null || i >= _sig.Length)
			{
				return RefreshTile(i, x, y, writeMaterial);
			}
			long num = Fingerprint(worldTile, fires);
			object building = worldTile.building;
			long num2 = _sig[i];
			if (num == num2 && building == _sigBuilding[i] && _crop[i] == 0 && !ReliefStale(i))
			{
				if (writeMaterial)
				{
					Cache.Materials[i] = _rawMaterial[i];
				}
				float num3 = Cache.Heights[i];
				if (num3 > _cycleMax)
				{
					_cycleMax = num3;
				}
				return 0;
			}
			NoteReliefRead(i);
			int num4 = RefreshTile(i, x, y, writeMaterial);
			_sig[i] = num;
			_sigBuilding[i] = building;
			_fullReads++;
			if (num2 != -1 && num != num2)
			{
				int width = Cache.Width;
				int height = Cache.Height;
				for (int j = -1; j <= 1; j++)
				{
					for (int k = -1; k <= 1; k++)
					{
						int num5 = x + k;
						int num6 = y + j;
						if ((k | j) != 0 && num5 >= 0 && num6 >= 0 && num5 < width && num6 < height)
						{
							int num7 = num6 * width + num5;
							num4 |= RefreshTile(num7, num5, num6, writeMaterial: false);
							_sig[num7] = -1L;
						}
					}
				}
			}
			return num4;
		}

		private void Forget(int x0, int y0, int x1, int y1)
		{
			int width = Cache.Width;
			int height = Cache.Height;
			for (int i = Math.Max(0, y0); i < Math.Min(height, y1); i++)
			{
				for (int j = Math.Max(0, x0); j < Math.Min(width, x1); j++)
				{
					if (i * width + j < _sig.Length)
					{
						_sig[i * width + j] = -1L;
					}
				}
			}
		}

		private void KeepDepthsBefore()
		{
			int num = _forms.Form.Length;
			if (_depthsBefore.Length != num * 6)
			{
				_depthsBefore = new float[num * 6];
			}
			Array.Copy(_forms.LandDepth, 0, _depthsBefore, 0, num);
			Array.Copy(_forms.HillDepth, 0, _depthsBefore, num, num);
			Array.Copy(_forms.MountainDepth, 0, _depthsBefore, num * 2, num);
			Array.Copy(_forms.SummitDepth, 0, _depthsBefore, num * 3, num);
			Array.Copy(_forms.RangeSize, 0, _depthsBefore, num * 4, num);
			for (int i = 0; i < num; i++)
			{
				_depthsBefore[num * 5 + i] = (int)_forms.Form[i];
			}
		}

		private void ForgetChangedDepths()
		{
			int num = _forms.Form.Length;
			if (_depthsBefore.Length != num * 6 || _sig.Length != num)
			{
				return;
			}
			for (int i = 0; i < num; i++)
			{
				if (_forms.LandDepth[i] != _depthsBefore[i] || _forms.HillDepth[i] != _depthsBefore[num + i] || _forms.MountainDepth[i] != _depthsBefore[num * 2 + i] || _forms.SummitDepth[i] != _depthsBefore[num * 3 + i] || _forms.RangeSize[i] != _depthsBefore[num * 4 + i] || (float)(int)_forms.Form[i] != _depthsBefore[num * 5 + i])
				{
					_sig[i] = -1L;
				}
			}
		}

		public long TakeFullReads()
		{
			long fullReads = _fullReads;
			_fullReads = 0L;
			return fullReads;
		}

		internal bool[] StaleForTest()
		{
			string[] why;
			return StaleForTest(out why);
		}

		internal bool[] StaleForTest(out string[] why)
		{
			int width = Cache.Width;
			int height = Cache.Height;
			int num = width * height;
			float[] array = (float[])Cache.Ground.Clone();
			float[] array2 = (float[])Cache.Heights.Clone();
			uint[] array3 = (uint[])Cache.BaseColors.Clone();
			byte[] array4 = (byte[])Cache.Flags.Clone();
			byte[] array5 = (byte[])_rawMaterial.Clone();
			bool quick = Quick;
			Quick = false;
			RefreshRect(0, 0, width, height);
			Quick = quick;
			for (int i = 0; i < num && i < _sig.Length; i++)
			{
				_sig[i] = -1L;
			}
			bool[] array6 = new bool[num];
			why = new string[num];
			for (int j = 0; j < num; j++)
			{
				array6[j] = array[j] != Cache.Ground[j] || array2[j] != Cache.Heights[j] || array3[j] != Cache.BaseColors[j] || array5[j] != _rawMaterial[j] || (array4[j] & -33) != (Cache.Flags[j] & -33);
				if (array6[j])
				{
					WorldTile worldTile = _tiles[j];
					why[j] = $"[ground {array[j]:F2}->{Cache.Ground[j]:F2} colour {array3[j]:X6}->{Cache.BaseColors[j]:X6} material {array5[j]}->{_rawMaterial[j]} flags {array4[j] & -33}->{Cache.Flags[j] & -33}; " + $"{worldTile?.main_type?.id}/{worldTile?.top_type?.id} burned {worldTile?.burned_stages} fire {worldTile?.isOnFire()} building {worldTile?.building?.asset?.id}]";
				}
			}
			return array6;
		}

		private void GenerateNow()
		{
			if (Relief.Terrainy)
			{
				_genTarget = null;
				Relief.SetGen(GenerateFor(Cache.Width, Cache.Height, _codes, _forms, Cache.SeaReach, Relief.Seed));
				CarveTarns();
			}
		}

		private void CarveTarns(short[] into = null)
		{
			short[] array = into ?? Relief.Gen;
			if (array == null || _tiles.Length != array.Length)
			{
				return;
			}
			List<int> list;
			lock (ReliefGen.LastTarns)
			{
				list = new List<int>(ReliefGen.LastTarns);
			}
			if (list.Count != 0)
			{
				int width = Cache.Width;
				List<int> tiles = ReliefGen.CarveTarns(array, list, width, Cache.Height, (int i) => (_tiles[i] == null) ? 0f : BandHeight(i, _tiles[i].main_type, into), (int i) => _tiles[i]?.main_type != null && _tiles[i].main_type.height_min >= 199 && !Info(_tiles[i].main_type).Liquid);
				if (into == null)
				{
					Relief.GenChanged(tiles);
					Log.Info($"relief: tarns sunk into the ranges: {ReliefGen.TarnsCarved} of {list.Count} sites ({ReliefGen.TarnsSteep} too steep)");
				}
			}
		}

		private static short[] GenerateFor(int w, int h, byte[] codes, Landforms forms, byte[] sea, int seed)
		{
			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				short[] result = ReliefGen.Build(w, h, codes, forms, sea, seed);
				Log.Info($"relief: Terrainy land worked out for {w}x{h} in {stopwatch.ElapsedMilliseconds} ms");
				return result;
			}
			catch (Exception ex)
			{
				Log.Error("relief: could not work out the land: " + ex.Message);
				return null;
			}
		}

		private void ResetReliefPrints(int n)
		{
			_sigRelief = new int[n];
			_reliefChanged.Clear();
			_genTarget = (_genRaw = null);
		}

		private bool ReliefStale(int i)
		{
			if (i < _sigRelief.Length)
			{
				return _sigRelief[i] != Relief.Stamp(i);
			}
			return false;
		}

		private void NoteReliefRead(int i)
		{
			if (i < _sigRelief.Length)
			{
				_sigRelief[i] = Relief.Stamp(i);
			}
		}

		private void TakeReliefChanges()
		{
			_reliefChanged.Clear();
			int num = Relief.TakeChanged(_reliefChanged);
			if (num == 0 || Cache.Width == 0)
			{
				return;
			}
			int width = Cache.Width;
			int height = Cache.Height;
			foreach (int item in _reliefChanged)
			{
				if (item >= 0 && item < width * height)
				{
					int num2 = item % width;
					int num3 = item / width;
					Forget(num2 - 1, num3 - 1, num2 + 2, num3 + 2);
				}
			}
			if (num > 64)
			{
				return;
			}
			foreach (int item2 in _reliefChanged)
			{
				if (item2 >= 0 && item2 < width * height)
				{
					int num4 = item2 % width;
					int num5 = item2 / width;
					RefreshRect(num4 - 1, num5 - 1, num4 + 2, num5 + 2);
				}
			}
		}

		private bool LakeBed(int i, in TerrainModel.TileInput water, ref float ground, ref float floor)
		{
			if (!Relief.Lake(i, out var level, out var main, out var _) || main <= 0 || main > _types.Length || !_types[main - 1].Known)
			{
				return false;
			}
			TypeInfo typeInfo = _types[main - 1];
			TerrainModel.TileInput t = water;
			t.HeightMin = typeInfo.HeightMin;
			t.BandTop = typeInfo.BandTop;
			t.Liquid = false;
			t.Lava = false;
			t.Pit = typeInfo.Pit;
			ground = Math.Max(val2: (floor = TerrainModel.ComputeHeight(in t)) + 0.02f, val1: level);
			return true;
		}

		private byte LakeCode(int i, WorldTile tile, TypeInfo main)
		{
			if (!Relief.Lake(i, out var _, out var main2, out var _) || main2 <= 0 || main2 > _types.Length || !_types[main2 - 1].Known)
			{
				return Code(tile, main);
			}
			TypeInfo typeInfo = _types[main2 - 1];
			if (typeInfo.HeightMin < 230)
			{
				return (byte)TerrainModel.ClassOf(typeInfo.HeightMin);
			}
			return 4;
		}

		internal static TileTypeBase TypeOfId(int id)
		{
			if (id <= 0)
			{
				return null;
			}
			try
			{
				foreach (TileType item in AssetManager.tiles.list)
				{
					if (item.index_id == id - 1)
					{
						return item;
					}
				}
				foreach (TopTileType item2 in AssetManager.top_tiles.list)
				{
					if (item2.index_id == id - 1)
					{
						return item2;
					}
				}
			}
			catch
			{
			}
			return null;
		}

		private void RawEarth(int i, ref uint color, ref byte material)
		{
			float num = Relief.RawOf(i);
			if (!(num <= 0f) && material != 2 && material != 4 && material != 5 && material != 6)
			{
				color = TerrainCache.Lerp(color, TerrainCache.Lerp(TurnedEarth, color, 0.25f), num * 0.9f);
				if (num > 0.25f)
				{
					material = 3;
				}
			}
		}

		internal float BandHeight(int i, TileTypeBase main, short[] gen = null)
		{
			if (i < 0 || i >= _tiles.Length || _tiles[i] == null)
			{
				return 0f;
			}
			int width = Cache.Width;
			int x = i % width;
			int y = i / width;
			TypeInfo typeInfo = Info(main);
			TerrainModel.TileInput t = new TerrainModel.TileInput
			{
				X = x,
				Y = y,
				HeightMin = typeInfo.HeightMin,
				BandTop = typeInfo.BandTop,
				DataHeight = ((_tiles[i].data != null) ? _tiles[i].data.height : typeInfo.HeightMin),
				Liquid = typeInfo.Liquid,
				Lava = typeInfo.Lava,
				Pit = typeInfo.Pit,
				Relief = ((gen != null && i < gen.Length) ? ((float)gen[i] * (1f / 64f)) : Relief.GenOf(i)),
				Terraces = Relief.Terraces,
				RollLess = Relief.RollLess
			};
			if (i < _forms.Form.Length)
			{
				t.LandDepth = _forms.LandDepth[i];
				t.HillDepth = _forms.HillDepth[i];
				t.MountainDepth = _forms.MountainDepth[i];
				t.SummitDepth = _forms.SummitDepth[i];
				t.RangeSize = _forms.RangeSize[i];
				t.Form = _forms.Form[i];
			}
			if (_cones.Count > 0)
			{
				t.VolcanoLift = ConeLift(x, y, out var _, out var _);
			}
			return TerrainModel.ComputeHeight(in t);
		}

		private void TakeGen(short[] gen)
		{
			short[] gen2 = Relief.Gen;
			if (gen2 == null || gen == null || gen2.Length != gen.Length)
			{
				_genTarget = null;
				Relief.SetGen(gen);
				CarveTarns();
			}
			else
			{
				CarveTarns(gen);
				_genRaw = gen;
				_genTarget = (short[])gen.Clone();
				_pinTimer = 0f;
				_easeTimer = 0f;
			}
		}

		private void EaseRelief(float dt)
		{
			short[] genTarget = _genTarget;
			if (genTarget == null)
			{
				return;
			}
			if (!Relief.Terrainy || Relief.Gen == null || Relief.Gen.Length != genTarget.Length)
			{
				_genTarget = null;
			}
			else if (!((_easeTimer -= dt) > 0f))
			{
				_easeTimer = 0.4f;
				if ((_pinTimer -= 0.4f) <= 0f || _pins == null || _pins.Length != genTarget.Length)
				{
					_pinTimer = 3f;
					FindPins(genTarget.Length);
					HoldUnderPins(genTarget);
				}
				EasedForTest = Relief.EaseToward(genTarget, 1, _pins, out var left);
				EaseLeftForTest = left;
				if (left == 0)
				{
					_genTarget = null;
				}
			}
		}

		private void HoldUnderPins(short[] target)
		{
			short[] genRaw = _genRaw;
			short[] gen = Relief.Gen;
			int num = target.Length;
			int width = Cache.Width;
			int height = Cache.Height;
			if (genRaw == null || gen == null || genRaw.Length != num || gen.Length != num || width * height != num)
			{
				return;
			}
			if (_pinDist == null || _pinDist.Length != num)
			{
				_pinDist = new float[num];
				_pinFrom = new int[num];
			}
			for (int i = 0; i < num; i++)
			{
				bool flag = _pins[i];
				_pinDist[i] = (flag ? 0f : 1000000f);
				_pinFrom[i] = (flag ? i : (-1));
			}
			NearestPin(_pinDist, _pinFrom, width, height);
			for (int j = 0; j < num; j++)
			{
				int num2 = _pinFrom[j];
				float num3 = _pinDist[j];
				if (num2 < 0 || num3 >= 4f)
				{
					target[j] = genRaw[j];
					continue;
				}
				float num4 = ((num3 <= 0f) ? 1f : (1f - num3 / 4f));
				num4 = num4 * num4 * (3f - 2f * num4);
				target[j] = (short)Math.Round((float)genRaw[j] + (float)(gen[num2] - genRaw[num2]) * num4);
			}
		}

		private static void NearestPin(float[] d, int[] from, int w, int h)
		{
			for (int i = 0; i < 2; i++)
			{
				bool flag = i == 1;
				for (int j = 0; j < h; j++)
				{
					int num = (flag ? (h - 1 - j) : j);
					for (int k = 0; k < w; k++)
					{
						int num2 = (flag ? (w - 1 - k) : k);
						int i2 = num * w + num2;
						int num3 = (flag ? 1 : (-1));
						int num4 = (flag ? 1 : (-1));
						Try(d, from, i2, num2 + num3, num, w, h, 1f);
						Try(d, from, i2, num2, num + num4, w, h, 1f);
						Try(d, from, i2, num2 + num3, num + num4, w, h, 1.4142f);
						Try(d, from, i2, num2 - num3, num + num4, w, h, 1.4142f);
					}
				}
			}
		}

		private static void Try(float[] d, int[] from, int i, int x, int y, int w, int h, float step)
		{
			if (x >= 0 && y >= 0 && x < w && y < h)
			{
				int num = y * w + x;
				if (from[num] >= 0 && d[num] + step < d[i])
				{
					d[i] = d[num] + step;
					from[i] = from[num];
				}
			}
		}

		private void FindPins(int n)
		{
			if (_pins == null || _pins.Length != n)
			{
				_pins = new bool[n];
			}
			else
			{
				Array.Clear(_pins, 0, n);
			}
			int width = Cache.Width;
			int height = Cache.Height;
			int num = 0;
			try
			{
				foreach (Building building in World.world.buildings)
				{
					if (building == null || building.asset == null || !building.isAlive() || AssetStyle.Classify(building.asset) != BuildingLook.House)
					{
						continue;
					}
					WorldTile current_tile = building.current_tile;
					if (current_tile == null)
					{
						continue;
					}
					BuildingFundament fundament = building.asset.fundament;
					int num2 = fundament?.left ?? 1;
					int num3 = fundament?.right ?? 1;
					int num4 = fundament?.top ?? 1;
					int num5 = fundament?.bottom ?? 1;
					int num6 = Math.Max(0, current_tile.x - num2 - 2);
					int num7 = Math.Min(width - 1, current_tile.x + num3 + 2);
					int num8 = Math.Max(0, current_tile.y - num5 - 2);
					int num9 = Math.Min(height - 1, current_tile.y + num4 + 2);
					for (int i = num8; i <= num9; i++)
					{
						for (int j = num6; j <= num7; j++)
						{
							int num10 = i * width + j;
							if (!_pins[num10])
							{
								_pins[num10] = true;
								num++;
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("relief: could not find the buildings' ground: " + ex.Message);
			}
			PinnedForTest = num;
		}

		private void LaySeabed()
		{
			int width = Cache.Width;
			int height = Cache.Height;
			int num = width * height;
			_seabedWorld++;
			_seabedDone = null;
			_seabedBusy = false;
			_bands = new byte[num];
			for (int i = 0; i < num; i++)
			{
				WorldTile worldTile = _tiles[i];
				if (worldTile != null && worldTile.main_type != null)
				{
					TypeInfo mi = Info(worldTile.main_type);
					TileTypeBase type = worldTile.cur_tile_type ?? worldTile.main_type;
					_bands[i] = BandOf(i, mi, mi.Liquid, mi.Lava || Info(type).Lava);
				}
			}
			if (Environment.GetEnvironmentVariable("FIRSTPERSON_OLDSEA") == "1")
			{
				_seaDepth = new byte[num];
				_seabedDirty = false;
				Cache.HasSeabed = false;
				Log.Info("seabed: none laid (FIRSTPERSON_OLDSEA)");
			}
			else
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				_seaDepth = Seabed.Depths(_bands, width, height);
				_seabedDirty = false;
				_lowest = (GodCamera.LowestGround = -1.5f);
				Cache.HasSeabed = true;
				Cache.OutsideColor = Seabed.OpenSea;
				Log.Info($"seabed: laid under {width}x{height} tiles in {stopwatch.ElapsedMilliseconds} ms");
			}
		}

		private byte BandOf(int i, TypeInfo mi, bool liquid, bool lava)
		{
			if (lava || (!liquid && !mi.Pit) || mi.HeightMin >= 98)
			{
				return 0;
			}
			byte b = Seabed.BandOf(mi.HeightMin);
			b = (liquid ? ((byte)(b | 8)) : ((byte)(b | 4)));
			if (Relief.IsLake(i) || Relief.EditOf(i) != 0f)
			{
				b |= 0x10;
			}
			return b;
		}

		private void SeabedUnder(int i, TypeInfo mi, bool liquid, bool lava, ref float ground, ref float floor)
		{
			if (i >= _bands.Length)
			{
				return;
			}
			byte b = _bands[i];
			byte b2 = BandOf(i, mi, liquid, lava);
			if (b2 != 0)
			{
				b2 |= (byte)(b & 8);
			}
			if (b2 != b)
			{
				_bands[i] = b2;
				_seabedDirty = true;
			}
			if (i < _seaDepth.Length && (b2 & 0x10) == 0 && _seaDepth[i] != 0)
			{
				float num = (float)(int)_seaDepth[i] * 0.1f;
				if (liquid)
				{
					floor = ground - num;
				}
				else if ((b2 & 4) != 0)
				{
					ground = (floor = 0f - num);
				}
			}
			if (ground < _lowest)
			{
				_lowest = ground;
				GodCamera.LowestGround = Math.Min(-1.5f, ground - 0.5f);
			}
		}

		private void TakeSeabed()
		{
			SeabedJob seabedDone = _seabedDone;
			if (seabedDone != null)
			{
				_seabedDone = null;
				_seabedBusy = false;
				ApplySeabed(seabedDone);
			}
			if (!_seabedDirty || _seabedBusy || _bands.Length == 0 || !Cache.HasSeabed)
			{
				return;
			}
			long timestamp = Stopwatch.GetTimestamp();
			if ((double)(timestamp - _seabedStarted) < 1.0 * (double)Stopwatch.Frequency)
			{
				return;
			}
			_seabedStarted = timestamp;
			_seabedDirty = false;
			_seabedBusy = true;
			SeabedJob next = new SeabedJob
			{
				World = _seabedWorld,
				Width = Cache.Width,
				Height = Cache.Height,
				Bands = (byte[])_bands.Clone()
			};
			SlowLane.Run(delegate
			{
				try
				{
					next.Depths = Seabed.Depths(next.Bands, next.Width, next.Height);
				}
				catch (Exception ex)
				{
					Log.Error("seabed: laying beside the game failed: " + ex.Message);
				}
				_seabedDone = next;
			});
		}

		private void ApplySeabed(SeabedJob job)
		{
			int width = Cache.Width;
			int height = Cache.Height;
			int num = width * height;
			if (job.Depths == null || job.World != _seabedWorld || job.Width != width || job.Height != height || _seaDepth.Length != num)
			{
				return;
			}
			byte[] depths = job.Depths;
			byte[] seaDepth = _seaDepth;
			int num2 = width;
			int num3 = height;
			int num4 = -1;
			int num5 = -1;
			int num6 = 0;
			for (int i = 0; i < num; i++)
			{
				if (depths[i] != seaDepth[i])
				{
					seaDepth[i] = depths[i];
					num6++;
					int num7 = i % width;
					int num8 = i / width;
					if (num7 < num2)
					{
						num2 = num7;
					}
					if (num7 > num4)
					{
						num4 = num7;
					}
					if (num8 < num3)
					{
						num3 = num8;
					}
					if (num8 > num5)
					{
						num5 = num8;
					}
				}
			}
			if (num6 != 0)
			{
				Forget(num2 - 1, num3 - 1, num4 + 2, num5 + 2);
				RefreshRect(num2 - 1, num3 - 1, num4 + 2, num5 + 2);
				Log.Info($"seabed: {num6} tiles' floor laid again in {num4 - num2 + 1}x{num5 - num3 + 1} tiles");
			}
		}

		public float SeaDepthOf(int i)
		{
			if (i < 0 || i >= _seaDepth.Length)
			{
				return 0f;
			}
			return (float)(int)_seaDepth[i] * 0.1f;
		}

		public byte SeaBandOf(int i)
		{
			if (i < 0 || i >= _bands.Length)
			{
				return 0;
			}
			return _bands[i];
		}

		public void TickSunField(bool now, float direct, bool mapDrawn)
		{
			TerrainCache cache = Cache;
			if (!cache.SoftLandShadows || cache.Width == 0 || cache.Height == 0)
			{
				DropSunField();
				return;
			}
			_sunHeld = true;
			float unscaledTime = Time.unscaledTime;
			if (_sunBake != null && _sunBake.IsCompleted)
			{
				PublishSunField(unscaledTime);
			}
			if (cache.SunFieldPrev != null && cache.SunFieldBlend < 1f)
			{
				cache.SunFieldBlend = Mathf.Clamp01((unscaledTime - _sunPublishedAt) / 1f);
			}
			if (now)
			{
				_sunAsked = true;
			}
			if (_sunTickSet && Vec3.Dot(cache.LightDir, _sunTickLight) < 0.99863f && direct >= 0.01f)
			{
				_sunAsked = true;
			}
			_sunTickLight = cache.LightDir;
			_sunTickSet = true;
			if (mapDrawn)
			{
				_canopyWantedUntil = unscaledTime + 5f;
			}
			bool flag = unscaledTime < _canopyWantedUntil;
			if (flag)
			{
				StepCanopy(unscaledTime);
			}
			else if (_canopyAt >= 0)
			{
				_canopyAt = -1;
				ReleaseCanopyLists();
			}
			if (_sunBake == null && SunBakeDue(unscaledTime, flag, out var snap, out var patch))
			{
				StartSunBake(unscaledTime, snap, patch, flag);
			}
		}

		public bool RelightWithNextField()
		{
			TerrainCache cache = Cache;
			if (!cache.SoftLandShadows || cache.SunField == null)
			{
				return false;
			}
			_sunRelightOwed = true;
			SweepLight = (_sweepStart = cache.LightDir);
			return true;
		}

		private void DropSunField()
		{
			if (_sunBake != null && _sunBake.IsCompleted)
			{
				_sunBake = null;
			}
			if (_sunHeld)
			{
				_sunHeld = false;
				_sunTickSet = false;
				TerrainCache cache = Cache;
				cache.SunField = (cache.SunFieldPrev = null);
				cache.SunFieldBlend = 1f;
				if (_sunBake != null)
				{
					_sunBakeStale = true;
				}
				_sunRelightOwed = false;
				_sunSpares.Clear();
				_sunHeights = new float[0];
				_sunCanopy = new float[0];
				_canopy = new float[0];
				_canopyNext = new float[0];
				_canopyAt = -1;
				_canopyReady = false;
				ReleaseCanopyLists();
			}
		}

		private void ForgetSunField()
		{
			_heightsVersion++;
			if (_sunBake != null)
			{
				_sunBakeStale = true;
			}
			_sunSpares.Clear();
			_canopyAt = -1;
			_canopyReady = false;
			ReleaseCanopyLists();
		}

		private int SunKey()
		{
			SunShadowField sunShadowField = (Cache.SoftLandShadows ? Cache.SunField : null);
			if (sunShadowField != null)
			{
				return sunShadowField.Version * 2 + ((Cache.SunFieldBlend >= 0.5f) ? 1 : 0);
			}
			return 0;
		}

		private bool SunBakeDue(float clock, bool canopy, out bool snap, out bool patch)
		{
			TerrainCache cache = Cache;
			snap = true;
			patch = false;
			if (_sunFailed && clock - _sunBakeAt < 5f)
			{
				return false;
			}
			if (cache.SunField == null || _sunAsked)
			{
				return true;
			}
			Vec3 lightDir = cache.LightDir;
			Vec3 sunBakeLight = _sunBakeLight;
			snap = false;
			if (clock - _sunPublishedAt < 1f)
			{
				return false;
			}
			if (_sunRelightOwed || lightDir.X != sunBakeLight.X || lightDir.Y != sunBakeLight.Y || lightDir.Z != sunBakeLight.Z)
			{
				return true;
			}
			patch = true;
			float num = clock - _sunBakeAt;
			if (_heightsVersion != _sunBakeHeights && num >= 0.25f)
			{
				return true;
			}
			if (canopy && _canopyReady && _canopyPrint != _sunBakeCanopy)
			{
				if (!(num >= 2f))
				{
					return !_sunBakeHadCanopy;
				}
				return true;
			}
			return false;
		}

		private void StartSunBake(float clock, bool snap, bool patch, bool withCanopy)
		{
			TerrainCache cache = Cache;
			int w = cache.Width;
			int h = cache.Height;
			int num = w * h;
			if (cache.Heights.Length < num)
			{
				return;
			}
			if (_sunHeights.Length != num)
			{
				_sunHeights = new float[num];
			}
			Array.Copy(cache.Heights, _sunHeights, num);
			float[] heights = _sunHeights;
			float[] canopy = null;
			if (withCanopy && _canopyReady && _canopy.Length == num)
			{
				if (_sunCanopy.Length != num)
				{
					_sunCanopy = new float[num];
				}
				Array.Copy(_canopy, _sunCanopy, num);
				canopy = _sunCanopy;
			}
			patch &= cache.SunField != null;
			Vec3 light = cache.LightDir;
			SunShadowField spare = TakeSpare();
			_sunBakeLight = light;
			_sunBakeAt = clock;
			_sunBakeHeights = _heightsVersion;
			_sunBakeCanopy = ((canopy != null) ? _canopyPrint : 0);
			_sunBakeHadCanopy = canopy != null;
			_sunBakeSnap = snap;
			_sunBakePatch = patch;
			_sunBakeRelights = _sunRelightOwed;
			_sunRelightOwed = false;
			_sunBakeWorld = _tilesMapRef;
			_sunBakeStale = false;
			_sunAsked = false;
			TaskCompletionSource<SunShadowField> bake = new TaskCompletionSource<SunShadowField>();
			_sunBake = bake.Task;
			SlowLane.RunSoon(delegate
			{
				try
				{
					long timestamp = Stopwatch.GetTimestamp();
					SunShadowField result = SunShadowField.Bake(heights, canopy, w, h, light, spare, oneThread: true);
					SunBakeMs = Ms(timestamp);
					bake.SetResult(result);
				}
				catch (Exception exception)
				{
					bake.SetException(exception);
				}
			});
		}

		private void PublishSunField(float clock)
		{
			Task<SunShadowField> sunBake = _sunBake;
			_sunBake = null;
			SunShadowField sunShadowField = null;
			_sunFailed = sunBake.IsFaulted || sunBake.IsCanceled;
			if (!_sunFailed)
			{
				sunShadowField = sunBake.Result;
			}
			else if (!_sunFailLogged)
			{
				_sunFailLogged = true;
				Log.Error("sun field: bake failed: " + ((sunBake.Exception != null) ? sunBake.Exception.GetBaseException().Message : "cancelled"));
			}
			TerrainCache cache = Cache;
			if (sunShadowField == null || _sunBakeStale || _sunBakeWorld != _tilesMapRef || sunShadowField.Width != cache.Width || sunShadowField.Height != cache.Height)
			{
				_sunBakeStale = false;
				_sunRelightOwed |= _sunBakeRelights;
				_sunAsked |= _sunBakeSnap;
				return;
			}
			bool flag = cache.SunField == null;
			if (_sunBakeSnap || flag)
			{
				Retire(cache.SunFieldPrev);
				Retire(cache.SunField);
				cache.SunFieldPrev = null;
				cache.SunFieldBlend = 1f;
			}
			else
			{
				Retire(cache.SunFieldPrev);
				cache.SunFieldPrev = cache.SunField;
				cache.SunFieldBlend = 0f;
				if (_sunBakePatch)
				{
					SunPatches++;
				}
			}
			_sunPublishedAt = clock;
			cache.SunField = sunShadowField;
			_sunPublishes++;
			if (!_sunLogged && _sunBakeHadCanopy)
			{
				_sunLogged = true;
				Log.Info($"sun field: {cache.Width}x{cache.Height} tiles baked in {SunBakeMs:F1} ms; what stands on the land: {CanopyCount} things gathered in {CanopyMs:F1} ms");
			}
			if (flag || _sunBakeRelights)
			{
				RelightAll();
			}
		}

		private void Retire(SunShadowField f)
		{
			if (f != null)
			{
				_sunSpares.Add((f, _sunPublishes));
				if (_sunSpares.Count > 2)
				{
					_sunSpares.RemoveAt(0);
				}
			}
		}

		private SunShadowField TakeSpare()
		{
			for (int i = 0; i < _sunSpares.Count; i++)
			{
				if (_sunSpares[i].at + 1 < _sunPublishes)
				{
					SunShadowField item = _sunSpares[i].field;
					_sunSpares.RemoveAt(i);
					return item;
				}
			}
			return null;
		}

		private void StepCanopy(float clock)
		{
			int width = Cache.Width;
			int height = Cache.Height;
			int num = width * height;
			if (_canopyAt < 0)
			{
				if (_canopyReady && _canopy.Length == num && clock - _canopyBegun < 2f)
				{
					return;
				}
				if (_canopyNext.Length != num)
				{
					_canopyNext = new float[num];
				}
				else
				{
					Array.Clear(_canopyNext, 0, num);
				}
				_canopyW = width;
				_canopyH = height;
				_canopyAt = 0;
				_canopyUnits = false;
				_canopyTaken = false;
				_canopyNextPrint = 0L;
				_canopyNextCount = 0;
				_canopyBegun = clock;
				_canopyPassMs = 0.0;
			}
			long timestamp = Stopwatch.GetTimestamp();
			bool flag = false;
			try
			{
				if (!_canopyUnits && StampBuildings(timestamp))
				{
					_canopyUnits = true;
					_canopyAt = 0;
					_canopyTaken = false;
				}
				if (_canopyUnits)
				{
					flag = StampUnits(timestamp);
				}
			}
			catch (Exception ex)
			{
				if (!_canopyFailed)
				{
					Log.Error("sun field: gathering what stands on the land failed: " + ex.Message);
				}
				_canopyFailed = true;
				flag = true;
			}
			_canopyPassMs += Ms(timestamp);
			if (flag)
			{
				ReleaseCanopyLists();
				float[] canopy = _canopy;
				_canopy = _canopyNext;
				_canopyNext = canopy;
				_canopyPrint = _canopyNextPrint;
				_canopyReady = true;
				_canopyAt = -1;
				CanopyMs = _canopyPassMs;
				CanopyCount = _canopyNextCount;
			}
		}

		private void ReleaseCanopyLists()
		{
			_canopyBuildings.Clear();
			_canopyActors.Clear();
			_canopyTaken = false;
		}

		private bool StampBuildings(long start)
		{
			List<Building> canopyBuildings = _canopyBuildings;
			if (!_canopyTaken)
			{
				canopyBuildings.Clear();
				List<Building> list = ((World.world != null && World.world.buildings != null) ? World.world.buildings.getSimpleList() : null);
				if (list != null)
				{
					canopyBuildings.AddRange(list);
				}
				_canopyTaken = true;
			}
			while (_canopyAt < canopyBuildings.Count)
			{
				if ((_canopyAt & 0x1F) == 31 && Ms(start) > 0.5)
				{
					return false;
				}
				Building building = canopyBuildings[_canopyAt];
				if (building != null)
				{
					try
					{
						StampBuilding(building);
					}
					catch
					{
					}
				}
				_canopyAt++;
			}
			return true;
		}

		private void StampBuilding(Building b)
		{
			if (b.asset == null || b.chopped || !b.isAlive())
			{
				return;
			}
			WorldTile current_tile = b.current_tile;
			if (current_tile == null)
			{
				return;
			}
			Stand stand = StandOf(b);
			if (stand.Shape == StandShape.None)
			{
				return;
			}
			int x = current_tile.x;
			int y = current_tile.y;
			float num = stand.Height;
			switch (stand.Shape)
			{
			case StandShape.Tile:
				Stamp(x, y, num);
				return;
			case StandShape.Crown:
				Stamp(x, y, num);
				Stamp(x - 1, y, num * 0.8f);
				Stamp(x + 1, y, num * 0.8f);
				Stamp(x, y - 1, num * 0.8f);
				Stamp(x, y + 1, num * 0.8f);
				return;
			case StandShape.Footprint:
			{
				if (b.isUnderConstruction())
				{
					num *= 0.5f;
				}
				else if (b.isRuin())
				{
					num *= 0.4f;
				}
				for (int i = y - stand.Bottom; i <= y + stand.Top; i++)
				{
					for (int j = x - stand.Left; j <= x + stand.Right; j++)
					{
						Stamp(j, i, num);
					}
				}
				return;
			}
			}
			float num2 = (float)x + 0.5f;
			float num3 = (float)y + 0.5f + stand.Up;
			float radius = stand.Radius;
			int num4 = (int)Math.Ceiling(radius);
			for (int k = (int)num3 - num4; k <= (int)num3 + num4; k++)
			{
				for (int l = (int)num2 - num4; l <= (int)num2 + num4; l++)
				{
					float num5 = (float)l + 0.5f - num2;
					float num6 = (float)k + 0.5f - num3;
					float num7 = (float)Math.Sqrt(num5 * num5 + num6 * num6) / radius;
					if (!(num7 >= 1f))
					{
						Stamp(l, k, (stand.Shape == StandShape.Dome) ? (num * (0.55f + 0.45f * (float)Math.Sqrt(1f - num7 * num7))) : (num * (1f - num7)));
					}
				}
			}
		}

		private bool StampUnits(long start)
		{
			List<Actor> canopyActors = _canopyActors;
			if (!_canopyTaken)
			{
				canopyActors.Clear();
				List<Actor> list = ((World.world != null && World.world.units != null) ? World.world.units.getSimpleList() : null);
				if (list != null)
				{
					canopyActors.AddRange(list);
				}
				_canopyTaken = true;
			}
			while (_canopyAt < canopyActors.Count)
			{
				if ((_canopyAt & 0x1F) == 31 && Ms(start) > 0.5)
				{
					return false;
				}
				Actor actor = canopyActors[_canopyAt];
				if (actor != null)
				{
					try
					{
						if (actor.asset != null && actor.isAlive() && !actor.is_inside_building && !actor.is_inside_boat && !(actor.position_height > 0.5f) && !((BaseSimObject)actor).isFlying())
						{
							Vector2 current_position = actor.current_position;
							float num = ActorTall(actor) * Math.Abs(actor.current_scale.y);
							if (num > 0.3f)
							{
								Stamp((int)current_position.x, (int)current_position.y, num);
							}
						}
					}
					catch
					{
					}
				}
				_canopyAt++;
			}
			return true;
		}

		private void Stamp(int x, int y, float tall)
		{
			if ((uint)x >= (uint)_canopyW || (uint)y >= (uint)_canopyH)
			{
				return;
			}
			int num = y * _canopyW + x;
			if (num < _canopyNext.Length)
			{
				if (tall > _canopyNext[num])
				{
					_canopyNext[num] = tall;
				}
				_canopyNextCount++;
				ulong num2 = (ulong)(((uint)num * -7046029254386353131L) ^ ((uint)(int)(tall * 8f) * -4417276706812531889L));
				_canopyNextPrint += (long)(num2 ^ (num2 >> 31));
			}
		}

		private Stand StandOf(Building b)
		{
			BuildingAsset asset = b.asset;
			if (_stands.TryGetValue(asset, out var value))
			{
				return value;
			}
			value = MeasureStand(b, out var finished);
			if (!Models.Ready || !finished)
			{
				return value;
			}
			if (_stands.Count > 4000)
			{
				_stands.Clear();
			}
			_stands[asset] = value;
			return value;
		}

		private static Stand MeasureStand(Building b, out bool finished)
		{
			BuildingAsset asset = b.asset;
			Stand result = default(Stand);
			finished = true;
			if (!AssetStyle.IsSolid(b))
			{
				return result;
			}
			BuildingLook buildingLook = AssetStyle.Classify(asset);
			if (buildingLook == BuildingLook.Plant || buildingLook == BuildingLook.Landform)
			{
				return result;
			}
			string text = asset.id ?? "";
			BuildingFundament fundament = asset.fundament;
			string text2 = (text.Contains("volcano") ? "nature_volcano" : ((!text.Contains("geyser")) ? null : (text.Contains("acid") ? "nature_geyser_acid" : "nature_geyser")));
			MeshModel meshModel = ((text2 != null) ? Models.Get(text2) : null);
			if (meshModel != null)
			{
				result.Shape = StandShape.Cone;
				result.Height = meshModel.InfoFloat("height", meshModel.Max.Z);
				result.Radius = Math.Max(1f, meshModel.InfoFloat("radius", Math.Max(meshModel.Max.X, meshModel.Max.Y)));
				result.Up = ((fundament != null) ? ((float)(fundament.top - fundament.bottom) * 0.5f) : 0f);
				return result;
			}
			float num = 0f;
			float num2 = 0f;
			try
			{
				bool flag = !b.isUnderConstruction() && !b.isRuin();
				Sprite sprite = (flag ? b.checkSpriteToRender() : FinishedSprite(b));
				finished = flag || sprite != null;
				if (sprite != null)
				{
					float num3 = ((sprite.pixelsPerUnit > 0f) ? sprite.pixelsPerUnit : 100f);
					num = sprite.rect.height / num3 * Math.Abs(asset.scale_base.y);
					num2 = sprite.rect.width / num3 * Math.Abs(asset.scale_base.x);
				}
			}
			catch
			{
			}
			if (!(num > 0f))
			{
				float num4;
				switch (buildingLook)
				{
				default:
					num4 = 1f;
					break;
				case BuildingLook.House:
					num4 = 2.5f;
					break;
				case BuildingLook.Tree:
					num4 = 4f;
					break;
				}
				num = num4;
				num2 = num * 0.6f;
			}
			if (buildingLook == BuildingLook.Tree)
			{
				float num5 = ((num <= 6.5f) ? 2.25f : ((14.625f + (num - 6.5f)) / num));
				float num6 = num2 * num5 * 0.79999995f;
				result.Height = num * num5 * 0.85f;
				if (num > 15.5f)
				{
					result.Shape = StandShape.Dome;
					result.Radius = Math.Max(1.5f, num6 * 0.42f);
					result.Up = ((fundament != null) ? ((float)(fundament.top - fundament.bottom) * 0.5f) : 0f);
				}
				else
				{
					result.Shape = ((!(num6 >= 2.5f)) ? StandShape.Tile : StandShape.Crown);
				}
				return result;
			}
			result.Height = num * 0.85f;
			if (result.Height < 0.5f)
			{
				return default(Stand);
			}
			result.Shape = StandShape.Footprint;
			if (fundament != null)
			{
				result.Left = Math.Max(0, Math.Min(8, fundament.left));
				result.Right = Math.Max(0, Math.Min(8, fundament.right));
				result.Top = Math.Max(0, Math.Min(8, fundament.top));
				result.Bottom = Math.Max(0, Math.Min(8, fundament.bottom));
			}
			return result;
		}

		private static Sprite FinishedSprite(Building b)
		{
			BuildingAnimationData buildingAnimationData = b.animData;
			if ((buildingAnimationData == null || buildingAnimationData.main == null || buildingAnimationData.main.Length == 0) && b.asset.building_sprites != null && b.asset.building_sprites.animation_data != null && b.asset.building_sprites.animation_data.Count > 0)
			{
				buildingAnimationData = b.asset.building_sprites.animation_data[0];
			}
			if (buildingAnimationData == null || buildingAnimationData.main == null || buildingAnimationData.main.Length == 0)
			{
				return null;
			}
			return buildingAnimationData.main[0];
		}

		private float ActorTall(Actor a)
		{
			ActorAsset asset = a.asset;
			if (_actorTall.TryGetValue(asset, out var value))
			{
				return value;
			}
			value = 1f;
			try
			{
				Sprite spriteToRender = a.getSpriteToRender();
				if (spriteToRender != null)
				{
					float num = ((spriteToRender.pixelsPerUnit > 0f) ? spriteToRender.pixelsPerUnit : 100f);
					value = Math.Max(0.3f, spriteToRender.rect.height / num * 0.85f);
				}
			}
			catch
			{
			}
			if (_actorTall.Count > 2000)
			{
				_actorTall.Clear();
			}
			_actorTall[asset] = value;
			return value;
		}
	}
}
