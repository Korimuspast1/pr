using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace FirstPerson.Gpu
{
	internal static class GpuSupport
	{
		private static bool _checked;

		private static string _missing;

		public static bool WatchEverywhere;

		private static readonly int[] WatchFrames = new int[3] { 20, 90, 300 };

		private static int _looked;

		private static int _bad;

		private static RenderTexture _small;

		private static Texture2D _read;

		public static string Missing
		{
			get
			{
				if (!_checked)
				{
					_checked = true;
					try
					{
						_missing = Check();
					}
					catch (Exception ex)
					{
						_missing = "graphics check failed: " + ex.Message;
					}
					Log.Info("gpu: graphics " + SystemInfo.graphicsDeviceType.ToString() + " (" + SystemInfo.graphicsDeviceName + ", " + SystemInfo.graphicsDeviceVersion + "), shader level " + SystemInfo.graphicsShaderLevel + ", buffers in vertex/fragment shaders " + SystemInfo.maxComputeBufferInputsVertex + "/" + SystemInfo.maxComputeBufferInputsFragment + ", targets " + SystemInfo.supportedRenderTargetCount + ((_missing == null) ? ": the GPU renderer can run" : (": the CPU renderer draws (" + _missing + ")")));
				}
				return _missing ?? Broken;
			}
		}

		public static string Broken { get; private set; }

		private static bool Windows
		{
			get
			{
				if (Application.platform != RuntimePlatform.WindowsPlayer)
				{
					return Application.platform == RuntimePlatform.WindowsEditor;
				}
				return true;
			}
		}

		public static TextureFormat FilteredFloat
		{
			get
			{
				try
				{
					if (Windows || SystemInfo.IsFormatSupported(GraphicsFormat.R32_SFloat, FormatUsage.Linear))
					{
						return TextureFormat.RFloat;
					}
					if (SystemInfo.SupportsTextureFormat(TextureFormat.RHalf))
					{
						return TextureFormat.RHalf;
					}
				}
				catch (Exception)
				{
				}
				return TextureFormat.RFloat;
			}
		}

		private static string Check()
		{
			if (!SystemInfo.supportsComputeShaders)
			{
				return "no compute shader support";
			}
			if (Windows)
			{
				return null;
			}
			if (SystemInfo.graphicsShaderLevel < 45)
			{
				return "shader level " + SystemInfo.graphicsShaderLevel + ", 45 needed";
			}
			if (SystemInfo.maxComputeBufferInputsVertex < 3)
			{
				return "the driver allows " + SystemInfo.maxComputeBufferInputsVertex + " data buffers in vertex shaders, 3 needed";
			}
			if (SystemInfo.maxComputeBufferInputsFragment < 4)
			{
				return "the driver allows " + SystemInfo.maxComputeBufferInputsFragment + " data buffers in fragment shaders, 4 needed";
			}
			if (SystemInfo.supportedRenderTargetCount < 2)
			{
				return "no drawing into two pictures at once";
			}
			RenderTextureFormat[] array = new RenderTextureFormat[4]
			{
				RenderTextureFormat.ARGBHalf,
				RenderTextureFormat.ARGBFloat,
				RenderTextureFormat.RFloat,
				RenderTextureFormat.ARGB32
			};
			for (int i = 0; i < array.Length; i++)
			{
				RenderTextureFormat format = array[i];
				if (!SystemInfo.SupportsRenderTextureFormat(format))
				{
					return "no " + format.ToString() + " render textures";
				}
			}
			if ((SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) == 0)
			{
				return "no texture copies";
			}
			return null;
		}

		public static bool Watch(RenderTexture output, int frame)
		{
			if (Broken != null || output == null || (Windows && !WatchEverywhere))
			{
				return false;
			}
			if (_looked >= WatchFrames.Length || frame < WatchFrames[_looked])
			{
				return false;
			}
			_looked++;
			string text = null;
			try
			{
				text = Judge(output);
			}
			catch (Exception ex)
			{
				Log.Info("gpu: picture check skipped: " + ex.Message);
				return false;
			}
			if (text == null)
			{
				Log.Info("gpu: picture check " + _looked + " at frame " + frame + ": drawn");
				return false;
			}
			_bad++;
			Log.Error("gpu: picture check " + _looked + " at frame " + frame + ": " + text);
			if (_bad < 2)
			{
				return false;
			}
			Broken = "the GPU's pictures came out wrong on this machine (" + text + ")";
			Log.Error("gpu: " + Broken + "; the CPU renderer draws for the rest of this run. Please send FirstPerson.log to the mod's author.");
			Release();
			return true;
		}

		private static string Judge(RenderTexture output)
		{
			if (_small == null)
			{
				_small = new RenderTexture(8, 8, 0, RenderTextureFormat.ARGB32)
				{
					filterMode = FilterMode.Point,
					name = "FirstPersonGpuCheck"
				};
				_small.Create();
			}
			if (_read == null)
			{
				_read = new Texture2D(8, 8, TextureFormat.RGBA32, mipChain: false);
			}
			FilterMode filterMode = output.filterMode;
			output.filterMode = FilterMode.Point;
			Graphics.Blit(output, _small);
			output.filterMode = filterMode;
			RenderTexture active = RenderTexture.active;
			RenderTexture.active = _small;
			_read.ReadPixels(new Rect(0f, 0f, 8f, 8f), 0, 0, recalculateMipMaps: false);
			_read.Apply(updateMipmaps: false);
			RenderTexture.active = active;
			Color32[] pixels = _read.GetPixels32();
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			Color32 color = pixels[0];
			Color32[] array = pixels;
			for (int i = 0; i < array.Length; i++)
			{
				Color32 color2 = array[i];
				if (color2.a < 128)
				{
					num++;
				}
				if (color2.r > 230 && color2.b > 230 && color2.g < 40)
				{
					num2++;
				}
				if (color2.r == color.r && color2.g == color.g && color2.b == color.b)
				{
					num3++;
				}
			}
			if (num * 10 >= pixels.Length * 9)
			{
				return "the finish drew nothing (alpha 0)";
			}
			if (num2 * 2 >= pixels.Length)
			{
				return "shader error pink";
			}
			if (num3 == pixels.Length)
			{
				Color32 color3 = color;
				return "one flat colour " + color3.ToString();
			}
			return null;
		}

		private static void Release()
		{
			if (_small != null)
			{
				_small.Release();
				UnityEngine.Object.Destroy(_small);
				_small = null;
			}
			if (_read != null)
			{
				UnityEngine.Object.Destroy(_read);
				_read = null;
			}
		}
	}
}
