using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPerson.Gpu
{
	internal sealed class GpuWeather : IDisposable
	{
		private readonly RenderTargetIdentifier[] _targets = new RenderTargetIdentifier[2];

		private const int MaxStorms = 16;

		private readonly Vector4[] _s0 = new Vector4[16];

		private readonly Vector4[] _s1 = new Vector4[16];

		private readonly Vector4[] _s2 = new Vector4[16];

		private readonly List<int> _order = new List<int>();

		private RenderTexture _clouds;

		private RenderTexture _front;

		public int Storms;

		public bool CloudsOn;

		public bool FallsOn;

		public void SetUniforms(CommandBuffer cmd, FrameRenderer cfg, ViewState view)
		{
			Weather w = cfg.Weather;
			Storms = 0;
			CloudsOn = (FallsOn = false);
			bool indoors = cfg.Air.Indoors;
			if (w != null && !indoors)
			{
				w.Bounds();
				_order.Clear();
				for (int i = 0; i < w.Storms.Count; i++)
				{
					_order.Add(i);
				}
				_order.Sort(delegate(int a, int b)
				{
					StormCloud stormCloud2 = w.Storms[a];
					StormCloud stormCloud3 = w.Storms[b];
					float num3 = (stormCloud2.X - view.X) * (stormCloud2.X - view.X) + (stormCloud2.Y - view.Y) * (stormCloud2.Y - view.Y);
					float value = (stormCloud3.X - view.X) * (stormCloud3.X - view.X) + (stormCloud3.Y - view.Y) * (stormCloud3.Y - view.Y);
					return num3.CompareTo(value);
				});
				foreach (int item in _order)
				{
					if (Storms >= 16)
					{
						break;
					}
					StormCloud stormCloud = w.Storms[item];
					_s0[Storms] = new Vector4(stormCloud.X, stormCloud.Y, Mathf.Max(0.5f, stormCloud.HalfX), Mathf.Max(0.5f, stormCloud.HalfY));
					uint color = stormCloud.Color;
					_s1[Storms] = new Vector4((float)(color & 0xFF) / 255f, (float)((color >> 8) & 0xFF) / 255f, (float)((color >> 16) & 0xFF) / 255f, stormCloud.Opacity);
					float w2 = (cfg.Cut.On ? cfg.Cut.Storm(stormCloud.X, stormCloud.Y, stormCloud.HalfX, stormCloud.HalfY, w.Base, w.Base + w.Thickness) : 1f);
					_s2[Storms] = new Vector4((int)stormCloud.Drops, stormCloud.Glows ? 1f : 0f, stormCloud.Seed, w2);
					Storms++;
				}
			}
			cmd.SetGlobalVectorArray("_FP_Storm0", _s0);
			cmd.SetGlobalVectorArray("_FP_Storm1", _s1);
			cmd.SetGlobalVectorArray("_FP_Storm2", _s2);
			float num = ((w != null && !indoors) ? ((float)(int)w.Global) : 0f);
			float num2 = ((w != null) ? w.GlobalStrength : 0f);
			cmd.SetGlobalVector("_FP_WeatherA", (w != null) ? new Vector4(w.Base, w.Thickness, w.WindX, w.WindY) : new Vector4(15f, 7f, 0f, 0f));
			cmd.SetGlobalVector("_FP_WeatherB", new Vector4((w != null && !indoors) ? w.Flash : 0f, num, num2, Storms));
			cmd.SetGlobalVector("_FP_WeatherC", (w != null && Storms > 0) ? new Vector4(w.MinX, w.MinY, w.MaxX, w.MaxY) : Vector4.zero);
			float x = (float.IsNaN(cfg.PrecipX) ? view.X : cfg.PrecipX);
			float y = (float.IsNaN(cfg.PrecipX) ? view.Y : cfg.PrecipY);
			cmd.SetGlobalVector("_FP_Precip", new Vector4(x, y, cfg.PrecipCell, 0f));
			cmd.SetGlobalVector("_FP_Slant", (w != null) ? new Vector4(w.SlantX, w.SlantY, 0f, 0f) : Vector4.zero);
			CloudsOn = Storms > 0;
			FallsOn = Storms > 0 || (num > 0.5f && num2 > 0.01f);
			cmd.SetGlobalVector("_FP_CloudsOn", new Vector4(CloudsOn ? 1f : 0f, 0f, 0f, 0f));
		}

		public void March(CommandBuffer cmd, Material mat, int width, int height)
		{
			if (!CloudsOn)
			{
				return;
			}
			if (_clouds == null || _clouds.width != width || _clouds.height != height)
			{
				if (_clouds != null)
				{
					_clouds.Release();
				}
				if (_front != null)
				{
					_front.Release();
				}
				_clouds = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf)
				{
					filterMode = FilterMode.Point,
					name = "FirstPersonGpuClouds"
				};
				_clouds.Create();
				_front = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat)
				{
					filterMode = FilterMode.Point,
					name = "FirstPersonGpuCloudFront"
				};
				_front.Create();
			}
			_targets[0] = _clouds;
			_targets[1] = _front;
			cmd.SetRenderTarget(_targets, _clouds.depthBuffer);
			cmd.DrawProcedural(Matrix4x4.identity, mat, 0, MeshTopology.Triangles, 3);
			cmd.SetGlobalTexture("_Clouds", _clouds);
			cmd.SetGlobalTexture("_CloudFront", _front);
		}

		public void DrawFalls(CommandBuffer cmd, Material mat)
		{
			if (FallsOn)
			{
				cmd.DrawProcedural(Matrix4x4.identity, mat, 1, MeshTopology.Triangles, 58806);
			}
		}

		public void Dispose()
		{
			if (_clouds != null)
			{
				_clouds.Release();
			}
			if (_front != null)
			{
				_front.Release();
			}
			_clouds = (_front = null);
		}
	}
}
