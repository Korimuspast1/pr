using System;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class PackPreview
	{
		private const float Spot = 16f;

		private const float Deep = -40f;

		private const float Fov = MathF.PI / 6f;

		private static FrameRenderer _r;

		private static TerrainCache _floor;

		private static readonly List<VoxelObject> NoVoxels = new List<VoxelObject>();

		private static readonly List<Billboard> NoBillboards = new List<Billboard>();

		public static Texture2D Render(MeshModel model, int w, int h)
		{
			if (model == null || model.Parts == null || model.Parts.Length == 0)
			{
				return null;
			}
			try
			{
				Setup();
				PackTextures.Decode(model);
				float a = model.Max.X - model.Min.X;
				float num = model.Max.Y - model.Min.Y;
				float num2 = model.Max.Z - model.Min.Z;
				float num3 = Mathf.Max(0.01f, Mathf.Max(a, Mathf.Max(num, num2)));
				float num4 = 1f / num3;
				MeshInstance meshInstance = new MeshInstance
				{
					Model = model,
					NoShadow = false
				};
				meshInstance.Place(16f - (model.Min.X + model.Max.X) * 0.5f * num4, 16f - (model.Min.Y + model.Max.Y) * 0.5f * num4, (0f - model.Min.Z) * num4, -MathF.PI / 2f, num4, num4, num4);
				float num5 = num2 * num4;
				float num6 = Mathf.Max(a, num) * num4;
				float num7 = num5 * 0.5f;
				float num8 = Mathf.Tan(MathF.PI / 12f);
				float num9 = num8 * (float)h / (float)w;
				float num10 = Mathf.Max(num5 * 0.58f / num9, num6 * 0.6f / num8) + num6 * 0.5f;
				float num11 = -1.2207965f;
				ViewState view = new ViewState
				{
					X = 16f + Mathf.Cos(num11) * num10,
					Y = 16f + Mathf.Sin(num11) * num10,
					Z = num7 + num5 * 0.25f + 0.1f,
					Yaw = num11 + MathF.PI,
					Fov = MathF.PI / 6f
				};
				view.Pitch = (0f - (view.Z - num7)) / num10;
				Atmosphere atmosphere = Atmosphere.Noon();
				atmosphere.HazeDensity = 0f;
				atmosphere.Rays = 0f;
				atmosphere.Clouds = 0f;
				float f = num11 + 0.75f;
				atmosphere.Light = new Vec3(Mathf.Cos(f) * 0.55f, Mathf.Sin(f) * 0.55f, 0.8f).Normalized();
				atmosphere.SunDisc = atmosphere.Light;
				_r.Resize(w, h);
				_r.MaxDistance = (_r.BillboardDistance = num10 + num6 * 2f + 4f);
				_r.Time = Time.unscaledTime;
				_r.Air = atmosphere;
				_r.PaletteLevels = 0;
				_r.Meshes.Clear();
				_r.Meshes.Add(meshInstance);
				_r.Render(_floor, view, NoBillboards, NoVoxels, -40f);
				Color32[] array = new Color32[w * h];
				uint[] pixels = _r.Pixels;
				float[] depth = _r.Depth;
				for (int i = 0; i < h; i++)
				{
					for (int j = 0; j < w; j++)
					{
						int num12 = i * w + j;
						if (depth[num12] < 1000000f)
						{
							uint num13 = pixels[num12];
							array[num12] = new Color32((byte)(num13 & 0xFF), (byte)((num13 >> 8) & 0xFF), (byte)((num13 >> 16) & 0xFF), byte.MaxValue);
							continue;
						}
						float num14 = (float)i / (float)h;
						float num15 = ((float)j - (float)w * 0.5f) / (float)w;
						float num16 = ((float)i - (float)h * 0.3f) / (float)h;
						float num17 = Mathf.Clamp01(1f - (num15 * num15 + num16 * num16) * 6f) * 0.1f;
						float num18 = 0.12f + num14 * 0.1f + num17;
						array[num12] = new Color32((byte)(num18 * 205f), (byte)(num18 * 215f), (byte)(num18 * 240f), byte.MaxValue);
					}
				}
				Texture2D texture2D = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
				texture2D.filterMode = FilterMode.Bilinear;
				texture2D.wrapMode = TextureWrapMode.Clamp;
				texture2D.name = "WorldfallPackPreview";
				texture2D.SetPixels32(array);
				texture2D.Apply(updateMipmaps: false);
				_r.Meshes.Clear();
				return texture2D;
			}
			catch (Exception ex)
			{
				Log.Error("packs: could not draw " + model.Name + " for a preview: " + ex.Message);
				return null;
			}
		}

		public static bool Save(MeshModel model, string path, int size = 512)
		{
			Texture2D texture2D = Render(model, size, size);
			if (texture2D == null)
			{
				return false;
			}
			try
			{
				File.WriteAllBytes(path, texture2D.EncodeToPNG());
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("packs: could not save a preview: " + ex.Message);
				return false;
			}
			finally
			{
				UnityEngine.Object.Destroy(texture2D);
			}
		}

		private static void Setup()
		{
			if (_r == null)
			{
				_r = new FrameRenderer();
				_floor = new TerrainCache();
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
			}
		}
	}
}
