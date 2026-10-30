using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPerson.Gpu
{
	internal sealed class GpuSpecks : IDisposable
	{
		private Mesh _specks;

		private Mesh _streaks;

		private readonly List<Vector3> _v = new List<Vector3>();

		private readonly List<Vector4> _uv = new List<Vector4>();

		private readonly List<Vector4> _info = new List<Vector4>();

		private readonly List<Vector4> _rect = new List<Vector4>();

		private readonly List<Color32> _c = new List<Color32>();

		private readonly List<int> _t = new List<int>();

		public int Props;

		public int Actors;

		public int Trails;

		public int Plain;

		private const int AtlasSize = 1024;

		private Texture2D _atlas;

		private readonly Dictionary<FarLook, Vector2Int> _placed = new Dictionary<FarLook, Vector2Int>();

		private int _shelfX;

		private int _shelfY;

		private int _shelfH;

		private bool _atlasDirty;

		private bool _atlasFull;

		private static readonly int FarLooksId = Shader.PropertyToID("_FarLooks");

		public int Looks => _placed.Count;

		public void Draw(CommandBuffer cmd, Material mat, FrameRenderer cfg, ViewState view, float focal, int ss, float fogDistance)
		{
			Props = (Actors = (Trails = (Plain = 0)));
			if (_atlasFull)
			{
				_placed.Clear();
				_shelfX = (_shelfY = (_shelfH = 0));
				_atlasFull = false;
			}
			float num = Mathf.Cos(view.Yaw);
			float num2 = Mathf.Sin(view.Yaw);
			Clear();
			bool indoors = cfg.Air.Indoors;
			bool flag = mat.HasProperty(FarLooksId);
			if (!indoors)
			{
				float num3 = 0.7f * (float)ss;
				foreach (FarProp farProp in cfg.FarProps)
				{
					float num4 = (farProp.X - view.X) * num + (farProp.Y - view.Y) * num2;
					if (num4 < 0.5f || num4 > fogDistance)
					{
						continue;
					}
					float num5 = farProp.Height * focal / num4;
					if (num5 < num3)
					{
						continue;
					}
					if (flag && farProp.Look != null && Place(farProp.Look, out var at))
					{
						int num6 = farProp.Look.LevelFor(num5 / (float)ss);
						int num7 = at.x;
						for (int i = 0; i < num6; i++)
						{
							num7 += farProp.Look.W[i] + 1;
						}
						LookQuad(new Vector3(farProp.X, farProp.Y, farProp.Z), farProp.Width, farProp.Height, farProp.Look.Left, farProp.Look.Right, new Vector4(num7, at.y, farProp.Look.W[num6], farProp.Look.H[num6]), farProp.Look.Creature);
					}
					else
					{
						Quad(new Vector3(farProp.X, farProp.Y, farProp.Z), farProp.Width, farProp.Height, farProp.Tree ? 0f : 1f, farProp.Color);
						Plain++;
					}
					Props++;
				}
				if (cfg.ActorClarity)
				{
					foreach (FarActor farActor in cfg.FarActors)
					{
						float num8 = (farActor.X - view.X) * num + (farActor.Y - view.Y) * num2;
						if (!(num8 < 0.5f) && !(num8 > fogDistance))
						{
							Quad(new Vector3(farActor.X, farActor.Y, farActor.Z), farActor.Width, farActor.Height, 2f, farActor.Color);
							Actors++;
						}
					}
				}
			}
			if (_v.Count > 0)
			{
				if (_specks == null)
				{
					_specks = new Mesh
					{
						name = "FirstPersonFarSpecks",
						indexFormat = IndexFormat.UInt32
					};
				}
				Fill(_specks);
				if (_atlasDirty)
				{
					_atlas.Apply(updateMipmaps: false, makeNoLongerReadable: false);
					_atlasDirty = false;
				}
				if (_atlas != null)
				{
					cmd.SetGlobalTexture("_FP_FarAtlas", _atlas);
				}
				cmd.DrawMesh(_specks, Matrix4x4.identity, mat, 0, 0);
			}
			cfg.FarProps.Clear();
			cfg.FarActors.Clear();
		}

		private bool Place(FarLook look, out Vector2Int at)
		{
			if (_placed.TryGetValue(look, out at))
			{
				return true;
			}
			int num = 0;
			for (int i = 0; i < look.Levels; i++)
			{
				num += look.W[i] + 1;
			}
			int num2 = look.H[0] + 1;
			if (num > 1024 || num2 > 1024)
			{
				return false;
			}
			if (_atlas == null)
			{
				_atlas = new Texture2D(1024, 1024, TextureFormat.RGBA32, mipChain: false, linear: false)
				{
					filterMode = FilterMode.Point,
					wrapMode = TextureWrapMode.Clamp,
					name = "FirstPersonFarLooks"
				};
				_atlas.SetPixels32(new Color32[1048576]);
			}
			if (_shelfX + num > 1024)
			{
				_shelfY += _shelfH;
				_shelfX = 0;
				_shelfH = 0;
			}
			if (_shelfY + num2 > 1024)
			{
				_atlasFull = true;
				return false;
			}
			at = new Vector2Int(_shelfX, _shelfY);
			int num3 = _shelfX;
			for (int j = 0; j < look.Levels; j++)
			{
				int num4 = look.W[j];
				int num5 = look.H[j];
				uint[] array = look.Pixels[j];
				Color32[] array2 = new Color32[num4 * num5];
				for (int k = 0; k < array2.Length; k++)
				{
					uint num6 = array[k];
					array2[k] = new Color32((byte)(num6 & 0xFF), (byte)((num6 >> 8) & 0xFF), (byte)((num6 >> 16) & 0xFF), (byte)(num6 >> 24));
				}
				_atlas.SetPixels32(num3, _shelfY, num4, num5, array2);
				num3 += num4 + 1;
			}
			_shelfX += num;
			_shelfH = Mathf.Max(_shelfH, num2);
			_placed[look] = at;
			_atlasDirty = true;
			return true;
		}

		public void DrawTrails(CommandBuffer cmd, Material mat, FrameRenderer cfg, ViewState view)
		{
			Clear();
			float num = Mathf.Cos(view.Yaw);
			float num2 = Mathf.Sin(view.Yaw);
			foreach (Streak streak in cfg.Streaks)
			{
				Vec3 a = streak.A;
				Vec3 b = streak.B;
				float num3 = streak.AlphaA;
				float num4 = streak.AlphaB;
				float num5 = (a.X - view.X) * num + (a.Y - view.Y) * num2;
				float num6 = (b.X - view.X) * num + (b.Y - view.Y) * num2;
				if (num5 < 0.12f && num6 < 0.12f)
				{
					continue;
				}
				if (num5 < 0.12f)
				{
					float num7 = (0.12f - num5) / (num6 - num5);
					a += (b - a) * num7;
					num3 += (num4 - num3) * num7;
				}
				else if (num6 < 0.12f)
				{
					float num8 = (0.12f - num6) / (num5 - num6);
					b += (a - b) * num8;
					num4 += (num3 - num4) * num8;
				}
				if (!(num3 <= 0.004f) || !(num4 <= 0.004f))
				{
					Vector3 vector = new Vector3(a.X, a.Y, a.Z);
					Vector3 vector2 = new Vector3(b.X, b.Y, b.Z);
					Color32 item = Col(streak.Color);
					int count = _v.Count;
					for (int i = 0; i < 4; i++)
					{
						bool flag = i < 2;
						float w = (((i & 1) == 0) ? (-1f) : 1f);
						_v.Add(flag ? vector : vector2);
						Vector3 vector3 = (flag ? vector2 : vector);
						_uv.Add(new Vector4(vector3.x, vector3.y, vector3.z, w));
						_info.Add(new Vector4(flag ? num3 : num4, streak.Width, streak.Glow, streak.Edge));
						_c.Add(item);
					}
					_t.Add(count);
					_t.Add(count + 1);
					_t.Add(count + 3);
					_t.Add(count);
					_t.Add(count + 3);
					_t.Add(count + 2);
					Trails++;
				}
			}
			if (_v.Count > 0)
			{
				if (_streaks == null)
				{
					_streaks = new Mesh
					{
						name = "FirstPersonTrails",
						indexFormat = IndexFormat.UInt32
					};
				}
				Fill(_streaks);
				mat.SetFloat("_StreakMax", FrameRenderer.StreakMaxPixels);
				cmd.DrawMesh(_streaks, Matrix4x4.identity, mat, 0, 1);
			}
			cfg.Streaks.Clear();
		}

		private void Quad(Vector3 foot, float w, float h, float kind, uint color)
		{
			int count = _v.Count;
			Color32 item = Col(color);
			for (int i = 0; i < 4; i++)
			{
				float x = ((i == 1 || i == 2) ? 0.5f : (-0.5f));
				float y = ((i >= 2) ? 1f : 0f);
				_v.Add(foot);
				_uv.Add(new Vector4(x, y, w, h));
				_info.Add(new Vector4(kind, 0f, 0f, 0f));
				_rect.Add(Vector4.zero);
				_c.Add(item);
			}
			_t.Add(count);
			_t.Add(count + 1);
			_t.Add(count + 2);
			_t.Add(count);
			_t.Add(count + 2);
			_t.Add(count + 3);
		}

		private void LookQuad(Vector3 foot, float w, float h, float left, float right, Vector4 rect, bool creature = false)
		{
			int count = _v.Count;
			for (int i = 0; i < 4; i++)
			{
				float x = ((i == 1 || i == 2) ? 1f : 0f);
				float y = ((i >= 2) ? 1f : 0f);
				_v.Add(foot);
				_uv.Add(new Vector4(x, y, w, h));
				_info.Add(new Vector4(3f, left, right, creature ? 1f : 0f));
				_rect.Add(rect);
				_c.Add(new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue));
			}
			_t.Add(count);
			_t.Add(count + 1);
			_t.Add(count + 2);
			_t.Add(count);
			_t.Add(count + 2);
			_t.Add(count + 3);
		}

		private static Color32 Col(uint c)
		{
			return new Color32((byte)(c & 0xFF), (byte)((c >> 8) & 0xFF), (byte)((c >> 16) & 0xFF), byte.MaxValue);
		}

		private void Clear()
		{
			_v.Clear();
			_uv.Clear();
			_info.Clear();
			_rect.Clear();
			_c.Clear();
			_t.Clear();
		}

		private void Fill(Mesh m)
		{
			m.Clear();
			m.SetVertices(_v);
			m.SetUVs(0, _uv);
			m.SetUVs(1, _info);
			m.SetUVs(2, (_rect.Count == _v.Count) ? _rect : _info);
			m.SetColors(_c);
			m.SetTriangles(_t, 0, calculateBounds: false);
			m.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
		}

		public void Dispose()
		{
			if (_specks != null)
			{
				UnityEngine.Object.Destroy(_specks);
			}
			if (_streaks != null)
			{
				UnityEngine.Object.Destroy(_streaks);
			}
			_specks = (_streaks = null);
			if (_atlas != null)
			{
				UnityEngine.Object.Destroy(_atlas);
			}
			_atlas = null;
			_placed.Clear();
			_shelfX = (_shelfY = (_shelfH = 0));
		}
	}
}
