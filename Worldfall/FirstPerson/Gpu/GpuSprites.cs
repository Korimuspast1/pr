using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPerson.Gpu
{
	internal sealed class GpuSprites : IDisposable
	{
		private struct SprData
		{
			public Vector4 A;

			public Vector4 Across;

			public Vector4 Uv;

			public Vector4 Flash;

			public Vector4 Mult;

			public Vector4 Misc;

			public const int Size = 96;
		}

		private const int AtlasSize = 2048;

		private static readonly int SprBaseId = Shader.PropertyToID("_SprBase");

		private Texture2D _atlas;

		private readonly Dictionary<SpriteImage, Texture2D> _textures = new Dictionary<SpriteImage, Texture2D>();

		private readonly Dictionary<SpriteImage, RectInt> _placed = new Dictionary<SpriteImage, RectInt>();

		private int _shelfX;

		private int _shelfY;

		private int _shelfH;

		internal static int Wraps;

		private static float _wrapLogged = -99f;

		private static float _wrapTwiceLogged = -99f;

		private bool _wrapped;

		private readonly List<SprData> _solid = new List<SprData>();

		private readonly List<SprData> _blend = new List<SprData>();

		private readonly List<SprData> _add = new List<SprData>();

		private readonly List<KeyValuePair<float, SprData>> _sortBlend = new List<KeyValuePair<float, SprData>>();

		private readonly List<KeyValuePair<float, SprData>> _sortAdd = new List<KeyValuePair<float, SprData>>();

		private SprData[] _all = new SprData[0];

		private ComputeBuffer _buffer;

		private readonly MaterialPropertyBlock _props = new MaterialPropertyBlock();

		private int _solidStart;

		private int _blendStart;

		private int _addStart;

		internal static bool GlowsLastForTest;

		public int Drawn;

		private float[] _sortKeys = new float[256];

		private int[] _sortIdx = new int[256];

		private Texture2D Source(SpriteImage img)
		{
			if (_textures.TryGetValue(img, out var value))
			{
				return value;
			}
			value = new Texture2D(img.Width, img.Height, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
				name = "FirstPersonSprite"
			};
			value.SetPixelData(img.Pixels, 0);
			value.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			_textures[img] = value;
			return value;
		}

		private bool Place(CommandBuffer cmd, SpriteImage img, out RectInt r)
		{
			if (_placed.TryGetValue(img, out r))
			{
				return true;
			}
			int width = img.Width;
			int height = img.Height;
			if (width <= 0 || height <= 0 || width > 512 || height > 512)
			{
				return false;
			}
			if (_atlas == null)
			{
				_atlas = new Texture2D(2048, 2048, TextureFormat.RGBA32, mipChain: false)
				{
					filterMode = FilterMode.Point,
					wrapMode = TextureWrapMode.Clamp,
					name = "FirstPersonSpriteAtlas"
				};
				_atlas.Apply(updateMipmaps: false, makeNoLongerReadable: true);
			}
			if (_shelfX + width + 1 > 2048)
			{
				_shelfY += _shelfH + 1;
				_shelfX = 0;
				_shelfH = 0;
			}
			if (_shelfY + height + 1 > 2048)
			{
				Wraps++;
				if (Time.unscaledTime - _wrapLogged > 10f)
				{
					_wrapLogged = Time.unscaledTime;
					Log.Info($"ATLASWRAP the sprite atlas filled: started again, the frame placed anew ({Wraps} so far, {_placed.Count} placed, {_textures.Count} sprite textures)");
				}
				_placed.Clear();
				_wrapped = true;
				_shelfX = (_shelfY = (_shelfH = 0));
				if (_textures.Count > 6000)
				{
					ClearTextures();
				}
			}
			r = new RectInt(_shelfX, _shelfY, width, height);
			_shelfX += width + 1;
			_shelfH = Math.Max(_shelfH, height);
			cmd.CopyTexture(Source(img), 0, 0, 0, 0, width, height, _atlas, 0, 0, r.x, r.y);
			_placed[img] = r;
			return true;
		}

		public void Prepare(CommandBuffer cmd, List<Billboard> list, ViewState view, float focal, float maxDistance, float billboardDistance, Func<int, bool> isActor)
		{
			float num = Mathf.Cos(view.Yaw);
			float num2 = Mathf.Sin(view.Yaw);
			float rx = num2;
			float ry = 0f - num;
			_wrapped = false;
			for (int i = 0; i < 2; i++)
			{
				Drawn = 0;
				_solid.Clear();
				_sortBlend.Clear();
				_sortAdd.Clear();
				if (list != null)
				{
					for (int j = 0; j < list.Count; j++)
					{
						Billboard b = list[j];
						if (b.Image != null)
						{
							One(cmd, b, view, focal, maxDistance, billboardDistance, num, num2, rx, ry, isActor);
						}
					}
				}
				if (_wrapped && i == 1 && Time.unscaledTime - _wrapTwiceLogged > 10f)
				{
					_wrapTwiceLogged = Time.unscaledTime;
					Log.Error($"ATLASWRAP the sprite atlas filled again on the frame's second pass: more sprites than the atlas holds, some show others' pixels ({list?.Count ?? 0} billboards)");
				}
				if (!_wrapped || i == 1)
				{
					break;
				}
				_wrapped = false;
			}
			int num3 = _solid.Count + _sortBlend.Count + _sortAdd.Count;
			if (_all.Length < Math.Max(1, num3))
			{
				_all = new SprData[Math.Max(256, num3 * 3 / 2)];
			}
			_solidStart = 0;
			_solid.CopyTo(_all, 0);
			_blendStart = _solid.Count;
			SortedInto(_sortBlend, _blendStart);
			_addStart = _blendStart + _sortBlend.Count;
			SortedInto(_sortAdd, _addStart);
			if (_buffer == null || _buffer.count < _all.Length)
			{
				_buffer?.Release();
				_buffer = new ComputeBuffer(_all.Length, 96, ComputeBufferType.Structured);
			}
			_buffer.SetData(_all, 0, 0, Math.Max(1, num3));
			cmd.SetGlobalBuffer("_FP_Sprites", _buffer);
			if (_atlas != null)
			{
				cmd.SetGlobalTexture("_FP_Atlas", _atlas);
			}
			cmd.SetGlobalVector("_FP_AtlasSize", new Vector4(2048f, 2048f, 0.00048828125f, 0.00048828125f));
		}

		private void SortedInto(List<KeyValuePair<float, SprData>> src, int start)
		{
			int count = src.Count;
			if (_sortKeys.Length < count)
			{
				_sortKeys = new float[count * 3 / 2];
				_sortIdx = new int[count * 3 / 2];
			}
			for (int i = 0; i < count; i++)
			{
				_sortKeys[i] = 0f - src[i].Key;
				_sortIdx[i] = i;
			}
			Array.Sort(_sortKeys, _sortIdx, 0, count);
			for (int j = 0; j < count; j++)
			{
				_all[start + j] = src[_sortIdx[j]].Value;
			}
		}

		private void One(CommandBuffer cmd, Billboard b, ViewState view, float focal, float maxDistance, float billboardDistance, float fx, float fy, float rx, float ry, Func<int, bool> isActor)
		{
			SpriteImage image = b.Image;
			float num = b.X - view.X;
			float num2 = b.Y - view.Y;
			float num3 = num * fx + num2 * fy;
			if (b.Kind == BillboardKind.Facing && b.Tag == -1 && !b.Shadow && !b.Far)
			{
				float num4 = Mathf.Max(b.Width, b.Height);
				if (num4 > 0f)
				{
					float num5 = Mathf.Max(b.Z, Mathf.Min(b.Z + b.Height, view.Z)) - view.Z;
					float num6 = Mathf.Sqrt(num * num + num2 * num2 + num5 * num5) / num4;
					if (num6 < 1.2f)
					{
						float num7 = (num6 - 0.6f) / 0.6f;
						if (num7 <= 0f)
						{
							return;
						}
						int num8 = ((b.Alpha != 0) ? b.Alpha : byte.MaxValue);
						b.Alpha = (byte)Mathf.Max(1, (int)((float)num8 * num7));
					}
				}
			}
			float num9 = (b.Far ? maxDistance : billboardDistance);
			if (b.Kind == BillboardKind.Cross)
			{
				if (num3 > billboardDistance + b.Width)
				{
					return;
				}
			}
			else
			{
				if (num3 < 0.2f || num3 > num9)
				{
					return;
				}
				float num10 = 0.35f * (float)GpuStyle.Supersample;
				if (b.Width * focal / num3 < num10 || b.Height * focal / num3 < num10)
				{
					return;
				}
			}
			if (!Place(cmd, image, out var r))
			{
				return;
			}
			SprData d = default(SprData);
			uint flash = b.Flash;
			uint multiply = b.Multiply;
			d.Flash = new Vector4((float)(flash & 0xFF) / 255f, (float)((flash >> 8) & 0xFF) / 255f, (float)((flash >> 16) & 0xFF) / 255f, (float)(flash >> 24) / 255f);
			d.Mult = ((multiply != 0) ? new Vector4((float)(multiply & 0xFF) / 255f, (float)((multiply >> 8) & 0xFF) / 255f, (float)((multiply >> 16) & 0xFF) / 255f, 1f) : Vector4.zero);
			d.Misc = new Vector4((float)(int)b.Alpha / 255f, b.Emissive ? 1f : 0f, b.Tag, (isActor != null && isActor(b.Tag)) ? 4f : 7f);
			_ = _solid;
			if (b.Kind == BillboardKind.Cross)
			{
				float width = b.Width;
				float pivotX = image.PivotX;
				d.Uv = new Vector4(r.x, r.y, r.width, r.height);
				for (int i = 0; i < 2; i++)
				{
					float f = b.Angle + (float)i * MathF.PI * 0.5f;
					float num11 = Mathf.Cos(f);
					float num12 = Mathf.Sin(f);
					d.A = new Vector4(b.X - num11 * width * pivotX, b.Y - num12 * width * pivotX, b.Z, b.Height);
					d.Across = new Vector4(num11 * width, num12 * width, 0f, 0f);
					Add(b, d, num3);
				}
			}
			else
			{
				float num13 = (b.Flip ? (1f - image.PivotX) : image.PivotX);
				d.Uv = (b.Flip ? new Vector4(r.x + r.width, r.y, -r.width, r.height) : new Vector4(r.x, r.y, r.width, r.height));
				d.A = new Vector4(b.X - rx * b.Width * num13, b.Y - ry * b.Width * num13, b.Z, b.Height);
				d.Across = new Vector4(rx * b.Width, ry * b.Width, 0f, 0f);
				Add(b, d, num3);
			}
			Drawn++;
		}

		private void Add(Billboard b, SprData d, float depth)
		{
			if (b.Alpha == 0)
			{
				_solid.Add(d);
				return;
			}
			if (b.Additive && GlowsLastForTest)
			{
				_sortAdd.Add(new KeyValuePair<float, SprData>(depth, d));
				return;
			}
			if (b.Additive)
			{
				d.Across.w = 1f;
			}
			_sortBlend.Add(new KeyValuePair<float, SprData>(depth, d));
		}

		public void DrawSolid(CommandBuffer cmd, Material mat)
		{
			Draw(cmd, mat, 0, _solidStart, _solid.Count);
		}

		public void DrawBlended(CommandBuffer cmd, Material mat)
		{
			Draw(cmd, mat, 1, _blendStart, _sortBlend.Count);
			Draw(cmd, mat, 2, _addStart, _sortAdd.Count);
		}

		private void Draw(CommandBuffer cmd, Material mat, int pass, int start, int count)
		{
			if (count > 0)
			{
				_props.SetFloat(SprBaseId, start);
				cmd.DrawProcedural(Matrix4x4.identity, mat, pass, MeshTopology.Triangles, count * 6, 1, _props);
			}
		}

		private void ClearTextures()
		{
			foreach (Texture2D value in _textures.Values)
			{
				if (value != null)
				{
					UnityEngine.Object.Destroy(value);
				}
			}
			_textures.Clear();
		}

		public void Dispose()
		{
			ClearTextures();
			_placed.Clear();
			if (_atlas != null)
			{
				UnityEngine.Object.Destroy(_atlas);
			}
			_atlas = null;
			_buffer?.Release();
			_buffer = null;
		}
	}
}
