using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPerson
{
	internal sealed class SpriteCache
	{
		private sealed class Pending
		{
			public int Generation;

			public RenderTexture Target;

			public bool Done;

			public bool Failed;

			public Color32[] Block;
		}

		private const int GpuReadsPerFrame = 8;

		private const int MaxEntries = 8000;

		private readonly Dictionary<Sprite, SpriteImage> _images = new Dictionary<Sprite, SpriteImage>();

		private readonly HashSet<Sprite> _failed = new HashSet<Sprite>();

		private readonly HashSet<Texture2D> _gpuOnly = new HashSet<Texture2D>();

		private int _gpuBudget;

		public bool AsyncReads;

		private static bool _asyncChecked;

		private static bool _asyncBroken;

		private static int _asyncFailures;

		private readonly Dictionary<Sprite, Pending> _pending = new Dictionary<Sprite, Pending>();

		private int _generation;

		private readonly Dictionary<Sprite, int> _emptyTries = new Dictionary<Sprite, int>();

		public int Count => _images.Count;

		public void BeginFrame()
		{
			_gpuBudget = 8;
			if (_images.Count > 8000)
			{
				_images.Clear();
				_failed.Clear();
				_emptyTries.Clear();
				DropPending();
			}
			try
			{
				DynamicSpritesLibrary.units?.checkAtlasDirty();
				DynamicSpritesLibrary.boats?.checkAtlasDirty();
				DynamicSpritesLibrary.items?.checkAtlasDirty();
				DynamicSpritesLibrary.icons?.checkAtlasDirty();
				DynamicSpritesLibrary.zombies?.checkAtlasDirty();
			}
			catch
			{
			}
		}

		public void Clear()
		{
			_images.Clear();
			_failed.Clear();
			DropPending();
		}

		private void DropPending()
		{
			_generation++;
			_pending.Clear();
		}

		public SpriteImage Get(Sprite sprite)
		{
			if ((object)sprite == null)
			{
				return null;
			}
			if (_images.TryGetValue(sprite, out var value))
			{
				return value;
			}
			if (_failed.Contains(sprite) || sprite == null)
			{
				return null;
			}
			try
			{
				value = Extract(sprite);
			}
			catch (Exception ex)
			{
				_failed.Add(sprite);
				Log.Error("sprite copy failed for " + sprite.name + ": " + ex.Message);
				return null;
			}
			if (value == null)
			{
				return null;
			}
			if (value.OpaqueTop <= value.OpaqueBottom || !HasOpaque(value))
			{
				_emptyTries.TryGetValue(sprite, out var value2);
				_emptyTries[sprite] = value2 + 1;
				if (value2 < 30)
				{
					return null;
				}
			}
			_images[sprite] = value;
			return value;
		}

		private static bool HasOpaque(SpriteImage img)
		{
			uint[] pixels = img.Pixels;
			for (int i = 0; i < pixels.Length; i++)
			{
				if (pixels[i] >> 24 >= 128)
				{
					return true;
				}
			}
			return false;
		}

		private SpriteImage Extract(Sprite sprite)
		{
			Texture2D texture = sprite.texture;
			if (texture == null)
			{
				return null;
			}
			Rect rect = sprite.rect;
			int num = Mathf.Max(1, Mathf.RoundToInt(rect.width));
			int num2 = Mathf.Max(1, Mathf.RoundToInt(rect.height));
			Rect rect2;
			Vector2 vector;
			try
			{
				rect2 = sprite.textureRect;
				vector = sprite.textureRectOffset;
			}
			catch
			{
				Vector2[] uv = sprite.uv;
				Vector2 lhs = uv[0];
				Vector2 lhs2 = uv[0];
				Vector2[] array = uv;
				foreach (Vector2 rhs in array)
				{
					lhs = Vector2.Min(lhs, rhs);
					lhs2 = Vector2.Max(lhs2, rhs);
				}
				rect2 = new Rect(lhs.x * (float)texture.width, lhs.y * (float)texture.height, (lhs2.x - lhs.x) * (float)texture.width, (lhs2.y - lhs.y) * (float)texture.height);
				vector = Vector2.zero;
			}
			int num3 = Mathf.Clamp(Mathf.RoundToInt(rect2.x), 0, texture.width - 1);
			int num4 = Mathf.Clamp(Mathf.RoundToInt(rect2.y), 0, texture.height - 1);
			int num5 = Mathf.Clamp(Mathf.RoundToInt(rect2.width), 1, texture.width - num3);
			int num6 = Mathf.Clamp(Mathf.RoundToInt(rect2.height), 1, texture.height - num4);
			Color32[] array2 = null;
			if (texture.isReadable && !_gpuOnly.Contains(texture))
			{
				Color[] pixels = texture.GetPixels(num3, num4, num5, num6);
				array2 = new Color32[pixels.Length];
				bool flag = false;
				for (int j = 0; j < pixels.Length; j++)
				{
					array2[j] = pixels[j];
					flag |= array2[j].a >= 128;
				}
				if (!flag)
				{
					return null;
				}
			}
			if (array2 == null)
			{
				if (AsyncReads && !_asyncBroken && SystemInfo.supportsAsyncGPUReadback)
				{
					if (!_pending.TryGetValue(sprite, out var value))
					{
						if (_gpuBudget <= 0)
						{
							return null;
						}
						_gpuBudget--;
						RequestThroughGpu(sprite, texture, num3, num4, num5, num6);
						return null;
					}
					if (!value.Done)
					{
						return null;
					}
					_pending.Remove(sprite);
					if (value.Failed || value.Block == null || value.Block.Length != num5 * num6)
					{
						if (value.Generation == _generation && ++_asyncFailures >= 8 && !_asyncBroken)
						{
							_asyncBroken = true;
							Log.Info("sprite reads: the card's later reads keep failing; reading directly");
						}
						return null;
					}
					array2 = value.Block;
					if (!_asyncChecked)
					{
						_asyncChecked = true;
						Color32[] array3 = ReadThroughGpu(texture, num3, num4, num5, num6);
						bool flag2 = array3.Length == array2.Length;
						int num7 = 0;
						while (flag2 && num7 < array2.Length)
						{
							flag2 = array3[num7].r == array2[num7].r && array3[num7].g == array2[num7].g && array3[num7].b == array2[num7].b && array3[num7].a == array2[num7].a;
							num7++;
						}
						Log.Info(flag2 ? $"sprite reads: read later on the card ({num5}x{num6} matches the direct read)" : $"sprite reads: the card's later read differs from the direct one ({num5}x{num6}); reading directly");
						if (!flag2)
						{
							_asyncBroken = true;
							array2 = array3;
						}
					}
				}
				else
				{
					if (_gpuBudget <= 0)
					{
						return null;
					}
					_gpuBudget--;
					array2 = ReadThroughGpu(texture, num3, num4, num5, num6);
				}
			}
			uint[] array4 = new uint[num * num2];
			int num8 = Mathf.RoundToInt(vector.x);
			int num9 = Mathf.RoundToInt(vector.y);
			for (int k = 0; k < num6; k++)
			{
				int num10 = k + num9;
				if (num10 < 0 || num10 >= num2)
				{
					continue;
				}
				for (int l = 0; l < num5; l++)
				{
					int num11 = l + num8;
					if (num11 >= 0 && num11 < num)
					{
						Color32 color = array2[k * num5 + l];
						array4[num10 * num + num11] = (uint)(color.r | (color.g << 8) | (color.b << 16) | (color.a << 24));
					}
				}
			}
			float num12 = ((sprite.pixelsPerUnit > 0f) ? sprite.pixelsPerUnit : 100f);
			SpriteImage spriteImage = new SpriteImage();
			spriteImage.Width = num;
			spriteImage.Height = num2;
			spriteImage.Pixels = array4;
			spriteImage.PivotX = Mathf.Clamp01(sprite.pivot.x / rect.width);
			spriteImage.PivotY = Mathf.Clamp01(sprite.pivot.y / rect.height);
			spriteImage.UnitWidth = rect.width / num12;
			spriteImage.UnitHeight = rect.height / num12;
			spriteImage.MeasureOpaqueRows();
			return spriteImage;
		}

		private void RequestThroughGpu(Sprite sprite, Texture2D texture, int x, int y, int w, int h)
		{
			RenderTextureReadWrite readWrite = ((!texture.isDataSRGB) ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
			RenderTexture temporary = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, readWrite);
			Pending p = new Pending
			{
				Generation = _generation,
				Target = temporary
			};
			RenderTexture active = RenderTexture.active;
			try
			{
				Vector2 scale = new Vector2((float)w / (float)texture.width, (float)h / (float)texture.height);
				Vector2 offset = new Vector2((float)x / (float)texture.width, (float)y / (float)texture.height);
				Graphics.Blit(texture, temporary, scale, offset);
				RenderTexture.active = active;
				_pending[sprite] = p;
				AsyncGPUReadback.Request(temporary, 0, TextureFormat.RGBA32, delegate(AsyncGPUReadbackRequest r)
				{
					try
					{
						if (!r.hasError && p.Generation == _generation)
						{
							p.Block = r.GetData<Color32>().ToArray();
						}
						else
						{
							p.Failed = true;
						}
					}
					catch
					{
						p.Failed = true;
					}
					finally
					{
						p.Done = true;
						RenderTexture.ReleaseTemporary(p.Target);
						p.Target = null;
					}
				});
			}
			catch
			{
				_pending.Remove(sprite);
				RenderTexture.ReleaseTemporary(temporary);
				throw;
			}
		}

		private static Color32[] ReadThroughGpu(Texture2D texture, int x, int y, int w, int h)
		{
			RenderTextureReadWrite readWrite = ((!texture.isDataSRGB) ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
			RenderTexture temporary = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, readWrite);
			RenderTexture active = RenderTexture.active;
			Texture2D texture2D = null;
			try
			{
				Vector2 scale = new Vector2((float)w / (float)texture.width, (float)h / (float)texture.height);
				Vector2 offset = new Vector2((float)x / (float)texture.width, (float)y / (float)texture.height);
				Graphics.Blit(texture, temporary, scale, offset);
				RenderTexture.active = temporary;
				texture2D = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
				texture2D.ReadPixels(new Rect(0f, 0f, w, h), 0, 0, recalculateMipMaps: false);
				return texture2D.GetPixels32();
			}
			finally
			{
				RenderTexture.active = active;
				RenderTexture.ReleaseTemporary(temporary);
				if (texture2D != null)
				{
					UnityEngine.Object.Destroy(texture2D);
				}
			}
		}
	}
}
