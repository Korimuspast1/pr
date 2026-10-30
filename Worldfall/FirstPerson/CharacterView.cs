using System;
using System.Collections.Generic;
using System.Diagnostics;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class CharacterView
	{
		private const float Fov = MathF.PI / 6f;

		private const float Spot = 16f;

		private const float Deep = -40f;

		private readonly FrameRenderer _r = new FrameRenderer();

		private readonly TerrainCache _floor = new TerrainCache();

		private readonly List<MeshInstance> _meshes = new List<MeshInstance>();

		private readonly List<VoxelObject> _voxels = new List<VoxelObject>();

		private readonly List<Billboard> _none = new List<Billboard>();

		private readonly HashSet<MeshInstance> _moved = new HashSet<MeshInstance>();

		private readonly Atmosphere _air = Atmosphere.Noon();

		private uint[] _rgba = new uint[0];

		private Texture2D _tex;

		private int _frame = -1;

		private bool _ok;

		private Actor _failedFor;

		private Actor _of;

		private float _distance;

		private float _middle;

		private float _tall;

		private float _reach;

		public float Turn;

		public bool Dragging;

		public int Drawn;

		public int Parts;

		public string PartNames = "";

		public float LastMs;

		private string _timing = "";

		public float BestMs = float.MaxValue;

		private float _buildMs;

		private static readonly Dictionary<MeshModel, Vec3> HeadMiddles = new Dictionary<MeshModel, Vec3>();

		public string Timing => _timing;

		public Texture2D Picture
		{
			get
			{
				if (!_ok)
				{
					return null;
				}
				return _tex;
			}
		}

		public uint[] Rgba => _rgba;

		public void ResetTiming()
		{
			BestMs = float.MaxValue;
			_timing = "";
		}

		public CharacterView()
		{
			_floor.Allocate(32, 32);
			for (int i = 0; i < 1024; i++)
			{
				_floor.Heights[i] = (_floor.Ground[i] = -40f);
				_floor.BaseColors[i] = TerrainCache.Rgb(0, 0, 0);
			}
			_floor.MaxHeight = -40f;
			_floor.OutsideHeight = -40f;
			_floor.OutsideIsWater = false;
			_floor.CastShadows = false;
			_floor.DitherEdges = false;
			_floor.Relight(0, 0, 32, 32);
			_r.Vignette = 0f;
			_r.ShadowReach = 6f;
			_r.TerrainShadowsOnModels = false;
			_r.ActorClarity = false;
			_air.HazeDensity = 0f;
			_air.Rays = 0f;
			_air.Clouds = 0f;
		}

		public bool Draw(Actor host, SpriteCache sprites, float facing, int w, int h, bool palette)
		{
			if (Time.frameCount == _frame)
			{
				return _ok;
			}
			if (!Dragging && _ok && _tex != null && Time.frameCount - _frame == 1 && _tex.width == w && _tex.height == h)
			{
				return true;
			}
			_frame = Time.frameCount;
			_ok = false;
			if (host == null || host == _failedFor || sprites == null || !Models.Ready || w < 8 || h < 8)
			{
				return false;
			}
			Stopwatch stopwatch = Stopwatch.StartNew();
			try
			{
				bool flag = Build(host, sprites, facing);
				_buildMs = (float)stopwatch.Elapsed.TotalMilliseconds;
				_ok = flag && Shoot(host, facing, w, h, palette);
				if (_ok)
				{
					Drawn++;
				}
				LastMs = (float)stopwatch.Elapsed.TotalMilliseconds;
				if (_ok && LastMs < BestMs)
				{
					BestMs = LastMs;
					_timing = $"build {_buildMs:F2}, land {_r.TerrainMs:F2}, meshes {_r.MeshMs:F2} (shadow {_r.ModelShadowMs:F2}, {_r.MeshTriangles} triangles), " + $"lights {_r.LightsMs:F2}, weather {_r.WeatherMs:F2}, overlay {_r.OverlayMs:F2}, finish {_r.FinishMs:F2}, after objects {_r.PostMs:F2} ms; {PartNames}";
				}
			}
			catch (Exception ex)
			{
				_failedFor = host;
				Log.Error("character view: " + ex);
			}
			finally
			{
				_meshes.Clear();
				_voxels.Clear();
				_r.Meshes.Clear();
			}
			return _ok;
		}

		public bool DrawHead(Actor who, SpriteCache sprites, int w, int h, bool palette)
		{
			if (who == null || sprites == null || !Models.Ready || w < 8 || h < 8)
			{
				return false;
			}
			try
			{
				if (!Build(who, sprites, 0f))
				{
					return false;
				}
				return ShootHead(0f, w, h, palette);
			}
			catch (Exception ex)
			{
				Log.Error("character view (head): " + ex.Message);
				return false;
			}
			finally
			{
				_meshes.Clear();
				_voxels.Clear();
				_r.Meshes.Clear();
			}
		}

		private static Vec3 HeadMiddle(MeshModel m, int head)
		{
			if (HeadMiddles.TryGetValue(m, out var value))
			{
				return value;
			}
			MeshPart meshPart = m.Parts[head];
			Vec3 vec = new Vec3(0f, 0f, 0f);
			int vertexCount = meshPart.VertexCount;
			for (int i = 0; i < vertexCount; i++)
			{
				vec += new Vec3(meshPart.Pos[i * 3], meshPart.Pos[i * 3 + 1], meshPart.Pos[i * 3 + 2]);
			}
			value = ((vertexCount > 0) ? (vec * (1f / (float)vertexCount)) : meshPart.Pivot);
			if (HeadMiddles.Count > 200)
			{
				HeadMiddles.Clear();
			}
			HeadMiddles[m] = value;
			return value;
		}

		private bool ShootHead(float facing, int w, int h, bool palette)
		{
			MeshInstance meshInstance = null;
			int num = -1;
			foreach (MeshInstance mesh in _meshes)
			{
				if (mesh.Model != null && (num = mesh.Model.PartIndex("head")) >= 0)
				{
					meshInstance = mesh;
					break;
				}
			}
			if (meshInstance == null)
			{
				return false;
			}
			Vec3 vec = PersonRig.Transform(meshInstance, num, HeadMiddle(meshInstance.Model, num));
			float num2 = Mathf.Sqrt(Vec3.Dot(meshInstance.Basis.C2, meshInstance.Basis.C2));
			float num3 = 0.5f * num2;
			float num4 = 0.4537856f;
			float num5 = num3 * 0.5f / Mathf.Tan(num4 * 0.5f) * 1.1f;
			float num6 = facing - 0.32f;
			ViewState view = new ViewState
			{
				X = vec.X + Mathf.Cos(num6) * num5,
				Y = vec.Y + Mathf.Sin(num6) * num5,
				Z = vec.Z - 0.06f * num2,
				Yaw = num6 + MathF.PI,
				Fov = num4
			};
			view.Pitch = 0.05f;
			float f = num6 + 0.75f;
			_air.Light = new Vec3(Mathf.Cos(f) * 0.55f, Mathf.Sin(f) * 0.55f, 0.8f).Normalized();
			_air.SunDisc = _air.Light;
			_r.Resize(w, h);
			_r.MaxDistance = (_r.BillboardDistance = Mathf.Clamp(num5 + 3f, 4f, 20f));
			_r.Time = Time.unscaledTime;
			_r.Air = _air;
			_r.PaletteLevels = (palette ? 18 : 0);
			_r.Meshes.Clear();
			_r.Meshes.AddRange(_meshes);
			_r.Render(_floor, view, _none, _voxels, -40f);
			int num7 = w * h;
			if (_rgba.Length != num7)
			{
				_rgba = new uint[num7];
			}
			uint[] pixels = _r.Pixels;
			float[] depth = _r.Depth;
			for (int i = 0; i < num7; i++)
			{
				_rgba[i] = ((depth[i] < 1000000f) ? (pixels[i] | 0xFF000000u) : 0u);
			}
			return true;
		}

		private bool Build(Actor host, SpriteCache sprites, float facing)
		{
			_meshes.Clear();
			_voxels.Clear();
			if (!host.isAlive() || Crabzillas.Is(host) || (host.asset != null && host.asset.is_boat))
			{
				return false;
			}
			SpriteImage spriteImage = sprites.Get(host.getSpriteToRender());
			if (spriteImage == null)
			{
				return false;
			}
			Vector2 current_position = host.current_position;
			float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
			if (People.IsHumanoid(host))
			{
				float raw = spriteImage.UnitHeight * Mathf.Abs(host.current_scale.y) * spriteImage.VisibleFraction;
				float body = Mathf.Clamp(Stature.Of(host, raw, dt), 0.3f, 3f);
				People.AddFigure(_meshes, _voxels, host, spriteImage, sprites, current_position.x, current_position.y, 0f, body, facing, dt);
			}
			else if (!Creatures.AddFigure(_meshes, host, spriteImage, current_position.x, current_position.y, 0f, facing, dt))
			{
				return false;
			}
			Parts = _meshes.Count + _voxels.Count;
			PartNames = "";
			foreach (MeshInstance mesh in _meshes)
			{
				PartNames = PartNames + ((mesh.Model != null) ? mesh.Model.Name : "?") + " ";
			}
			PartNames = PartNames + _voxels.Count + " sprites";
			if (_meshes.Count == 0)
			{
				return false;
			}
			float num = 16f - current_position.x;
			float num2 = 16f - current_position.y;
			_moved.Clear();
			foreach (MeshInstance mesh2 in _meshes)
			{
				StuckArrows.Unsee(mesh2);
				if (_moved.Add(mesh2))
				{
					mesh2.Position = new Vec3(mesh2.Position.X + num, mesh2.Position.Y + num2, mesh2.Position.Z);
				}
			}
			for (int i = 0; i < _voxels.Count; i++)
			{
				VoxelObject value = _voxels[i];
				value.Origin = new Vec3(value.Origin.X + num, value.Origin.Y + num2, value.Origin.Z);
				_voxels[i] = value;
			}
			return true;
		}

		private bool Shoot(Actor host, float facing, int w, int h, bool palette)
		{
			if (!Bounds(out var reach, out var bottom, out var top))
			{
				return false;
			}
			float num = Mathf.Max(0.2f, top - bottom);
			float num2 = (top + bottom) * 0.5f;
			if (_of != host || _distance <= 0f)
			{
				_of = host;
				_tall = num;
				_middle = num2;
				_reach = reach;
			}
			float t = 1f - Mathf.Exp(-3f * Mathf.Min(Time.unscaledDeltaTime, 0.1f));
			_tall = Mathf.Lerp(_tall, num, t);
			_middle = Mathf.Lerp(_middle, num2, t);
			_reach = Mathf.Lerp(_reach, reach, t);
			float num3 = Mathf.Tan(MathF.PI / 12f);
			float num4 = num3 * (float)h / (float)w;
			_distance = Mathf.Max(_tall * 0.56f / num4 + _reach * 0.4f, _reach * (0.92f / num3 + 0.5f));
			float num5 = facing - 0.55f + Turn;
			float num6 = Mathf.Cos(num5);
			float num7 = Mathf.Sin(num5);
			ViewState view = new ViewState
			{
				X = 16f + num6 * _distance,
				Y = 16f + num7 * _distance,
				Z = _middle + _tall * 0.22f,
				Yaw = num5 + MathF.PI,
				Fov = MathF.PI / 6f
			};
			view.Pitch = (0f - (view.Z - _middle)) / _distance;
			float f = num5 + 0.75f;
			_air.Light = new Vec3(Mathf.Cos(f) * 0.55f, Mathf.Sin(f) * 0.55f, 0.8f).Normalized();
			_air.SunDisc = _air.Light;
			_r.Resize(w, h);
			_r.MaxDistance = (_r.BillboardDistance = Mathf.Clamp(_distance + _reach * 2f + 2f, 6f, 60f));
			_r.Time = Time.unscaledTime;
			_r.Air = _air;
			_r.PaletteLevels = (palette ? 18 : 0);
			_r.Meshes.Clear();
			_r.Meshes.AddRange(_meshes);
			_r.Render(_floor, view, _none, _voxels, -40f);
			float num8 = (float)w * 0.5f;
			float num9 = (float)h * 0.5f;
			float num10 = 0f;
			float num11 = 0f;
			if (_r.Project(16f, 16f, 0f, out var col, out var row, out var _, out var scale))
			{
				num8 = col;
				num9 = row;
				num10 = Mathf.Max(2f, Mathf.Min(_reach, _tall * 0.3f) * 0.8f * scale);
				num11 = Mathf.Max(1.5f, num10 * view.Z / Mathf.Sqrt(view.Z * view.Z + _distance * _distance));
			}
			int num12 = w * h;
			if (_rgba.Length != num12)
			{
				_rgba = new uint[num12];
			}
			uint[] pixels = _r.Pixels;
			float[] depth2 = _r.Depth;
			for (int i = 0; i < h; i++)
			{
				int num13 = i * w;
				float num14 = ((num11 > 0f) ? (((float)(h - 1 - i) + 0.5f - num9) / num11) : 9f);
				for (int j = 0; j < w; j++)
				{
					if (depth2[num13 + j] < 1000000f)
					{
						_rgba[num13 + j] = pixels[num13 + j] | 0xFF000000u;
						continue;
					}
					float num15 = ((num10 > 0f) ? (((float)j + 0.5f - num8) / num10) : 9f);
					float num16 = num15 * num15 + num14 * num14;
					_rgba[num13 + j] = ((num16 < 0.45f) ? 1711276032u : ((num16 < 1f) ? 855638016u : 0u));
				}
			}
			if (_tex == null || _tex.width != w || _tex.height != h)
			{
				if (_tex != null)
				{
					UnityEngine.Object.Destroy(_tex);
				}
				_tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false)
				{
					filterMode = FilterMode.Point,
					wrapMode = TextureWrapMode.Clamp,
					name = "FirstPersonCharacter"
				};
			}
			_tex.SetPixelData(_rgba, 0);
			_tex.Apply(updateMipmaps: false);
			return true;
		}

		private bool Bounds(out float reach, out float bottom, out float top)
		{
			reach = 0f;
			bottom = float.MaxValue;
			top = float.MinValue;
			foreach (MeshInstance mesh in _meshes)
			{
				MeshModel model = mesh.Model;
				if (model != null && !mesh.ShadowOnly)
				{
					for (int i = 0; i < 8; i++)
					{
						Vec3 v = new Vec3(((i & 1) == 0) ? model.Min.X : model.Max.X, ((i & 2) == 0) ? model.Min.Y : model.Max.Y, ((i & 4) == 0) ? model.Min.Z : model.Max.Z);
						Vec3 vec = mesh.Position + mesh.Basis.Mul(v);
						reach = Mathf.Max(reach, Mathf.Sqrt((vec.X - 16f) * (vec.X - 16f) + (vec.Y - 16f) * (vec.Y - 16f)));
						bottom = Mathf.Min(bottom, vec.Z);
						top = Mathf.Max(top, vec.Z);
					}
				}
			}
			if (top < bottom)
			{
				return false;
			}
			bottom = Mathf.Max(bottom, 0f);
			reach = Mathf.Clamp(reach, 0.15f, 12f);
			return true;
		}
	}
}
