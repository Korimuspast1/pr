using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FirstPerson.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPerson.Gpu
{
	internal static class GpuDark
	{
		public static readonly Vector4[] Params = new Vector4[7];

		private static readonly Dictionary<string, int> Slots = new Dictionary<string, int>
		{
			{ "dark_sharpen", 0 },
			{ "dark_sharpen_max", 1 },
			{ "dark_bloom", 2 },
			{ "dark_bloom_from", 3 },
			{ "dark_emerald", 4 },
			{ "dark_vibrance", 5 },
			{ "dark_shadow_tint", 6 },
			{ "dark_contrast", 7 },
			{ "dark_gloom_dim", 8 },
			{ "dark_warm_mute", 9 },
			{ "dark_water", 10 },
			{ "dark_sparkle", 11 },
			{ "dark_haze_keep", 12 },
			{ "dark_indoor", 13 },
			{ "dark_night_lift", 14 },
			{ "dark_water_day", 15 },
			{ "dark_fog_hue", 16 },
			{ "dark_fog_pull", 17 },
			{ "dark_fog_chroma", 18 },
			{ "dark_fog_light", 19 },
			{ "dark_fog_gloom", 20 },
			{ "dark_night_fog", 21 },
			{ "dark_sky_pull", 22 },
			{ "dark_cloud_pull", 23 },
			{ "dark_gloom_grey", 24 },
			{ "dark_sky_gloom", 25 },
			{ "dark_gamma", 26 },
			{ "dark_deepen", 27 }
		};

		private static readonly Dictionary<string, float> Defaults = new Dictionary<string, float>
		{
			{ "dark_sharpen", 1f },
			{ "dark_sharpen_max", 0.12f },
			{ "dark_bloom", 0.4f },
			{ "dark_bloom_from", 0.75f },
			{ "dark_emerald", 0.6f },
			{ "dark_vibrance", 0.35f },
			{ "dark_shadow_tint", 0.07f },
			{ "dark_contrast", 1.15f },
			{ "dark_gloom_dim", 0.1f },
			{ "dark_warm_mute", 0.3f },
			{ "dark_water", 0.55f },
			{ "dark_sparkle", 0.8f },
			{ "dark_water_day", 0.6f },
			{ "dark_haze_keep", 0.65f },
			{ "dark_indoor", 0.6f },
			{ "dark_night_lift", 0.08f },
			{ "dark_fog_hue", 4.75f },
			{ "dark_fog_pull", 0.7f },
			{ "dark_fog_chroma", 0.055f },
			{ "dark_fog_light", 0.85f },
			{ "dark_fog_gloom", 0.62f },
			{ "dark_night_fog", 0.26f },
			{ "dark_sky_pull", 0.4f },
			{ "dark_cloud_pull", 0.35f },
			{ "dark_gloom_grey", 0.45f },
			{ "dark_sky_gloom", 0.72f },
			{ "dark_gamma", 1.12f },
			{ "dark_deepen", 0.1f }
		};

		private static float _shown;

		private static float _gloom;

		private static int _frame = -1;

		private static string _path;

		private static DateTime _stamp = DateTime.MaxValue;

		private static float _nextCheck = -1f;

		public static float GloomForTest = -1f;

		public static bool Wanted
		{
			get
			{
				try
				{
					return WorldBoxMod.Instance != null && WorldBoxMod.Instance.Settings != null && WorldBoxMod.Instance.Settings.DarkFantasy == 1;
				}
				catch
				{
					return false;
				}
			}
		}

		public static float Shown => _shown;

		public static float Gloom => _gloom;

		public static void Apply(CommandBuffer cmd, FrameRenderer cfg)
		{
			Ease();
			ReadParams();
			Atmosphere air = cfg.Air;
			float num = air?.Night ?? 0f;
			cmd.SetGlobalVectorArray("_FP_DarkStyle", Params);
			cmd.SetGlobalVector("_FP_Dark", new Vector4(_shown, _gloom, _shown * Smooth(0.25f, 0.8f, num), 0f));
			if (!(_shown <= 0.001f) && air != null && !air.Indoors)
			{
				float light = Mathf.Lerp(Get(19), Get(20), _gloom);
				float least = Get(21) * num;
				float num2 = Get(24) * _gloom;
				float num3 = Mathf.Lerp(Get(17), Mathf.Min(1f, Get(17) + 0.15f), num);
				float chroma = Get(18) * (1f + 0.1f * num);
				Color b = Turn(Col(air.Horizon), num3, chroma, light, least, num2);
				Color b2 = Turn(Col(air.SunHorizon), num3 * 0.55f, chroma, light, least, num2);
				Color b3 = Turn(Col(air.Zenith), Get(22) + 0.3f * num, Get(18) * 1.3f, Mathf.Lerp(0.95f, Get(25), _gloom), 0f, num2);
				Color b4 = Turn(Col(air.CloudLit), Get(23) + 0.3f * num, Get(18) * 0.4f, Mathf.Lerp(0.97f, 0.75f, _gloom), 0f, 0f);
				Color b5 = Turn(Col(air.CloudShade), Mathf.Min(1f, Get(23) + 0.25f + 0.3f * num), Get(18) * 0.8f, Mathf.Lerp(0.92f, 0.68f, _gloom), 0f, num2 * 0.5f);
				cmd.SetGlobalVector("_FP_Fog0", Color.Lerp(Col(air.Horizon), b, _shown));
				cmd.SetGlobalVector("_FP_Fog1", Color.Lerp(Col(air.SunHorizon), b2, _shown));
				cmd.SetGlobalVector("_FP_Zenith", Color.Lerp(Col(air.Zenith), b3, _shown));
				cmd.SetGlobalVector("_FP_CloudLit", Color.Lerp(Col(air.CloudLit), b4, _shown));
				cmd.SetGlobalVector("_FP_CloudShade", Color.Lerp(Col(air.CloudShade), b5, _shown));
			}
		}

		private static void Ease()
		{
			if (_frame == Time.frameCount)
			{
				return;
			}
			float num = ((_frame < 0) ? 1f : Mathf.Min(0.25f, Time.unscaledDeltaTime));
			_frame = Time.frameCount;
			_shown = Mathf.MoveTowards(_shown, Wanted ? 1f : 0f, num * 3f);
			float num2 = ((GloomForTest >= 0f) ? GloomForTest : 0f);
			if (GloomForTest < 0f)
			{
				try
				{
					num2 = ((WorldBoxMod.Instance != null) ? WorldBoxMod.Instance.DarkGloom() : 0f);
				}
				catch
				{
				}
			}
			_gloom = ((GloomForTest >= 0f) ? num2 : Mathf.MoveTowards(_gloom, num2, num / 3f));
		}

		private static float Get(int slot)
		{
			return Params[slot >> 2][slot & 3];
		}

		private static void Set(string key, float value)
		{
			if (Slots.TryGetValue(key, out var value2))
			{
				Vector4 vector = Params[value2 >> 2];
				vector[value2 & 3] = value;
				Params[value2 >> 2] = vector;
			}
		}

		private static void ReadParams()
		{
			if (Time.unscaledTime < _nextCheck && _stamp != DateTime.MaxValue)
			{
				return;
			}
			_nextCheck = Time.unscaledTime + 1f;
			if (_path == null)
			{
				_path = Path.Combine(Log.Folder, "gpu_style.txt");
			}
			DateTime dateTime = DateTime.MinValue;
			try
			{
				if (File.Exists(_path))
				{
					dateTime = File.GetLastWriteTimeUtc(_path);
				}
			}
			catch
			{
			}
			if (dateTime == _stamp)
			{
				return;
			}
			_stamp = dateTime;
			foreach (KeyValuePair<string, float> @default in Defaults)
			{
				Set(@default.Key, @default.Value);
			}
			if (dateTime == DateTime.MinValue)
			{
				return;
			}
			try
			{
				string[] array = File.ReadAllLines(_path);
				for (int i = 0; i < array.Length; i++)
				{
					string text = array[i];
					int num = text.IndexOf('#');
					if (num >= 0)
					{
						text = text.Substring(0, num);
					}
					int num2 = text.IndexOf('=');
					if (num2 > 0)
					{
						string text2 = text.Substring(0, num2).Trim();
						if (text2.StartsWith("dark_", StringComparison.Ordinal) && float.TryParse(text.Substring(num2 + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
						{
							Set(text2, result);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("gpu: dark style: " + ex.Message);
			}
		}

		private static Color Turn(Color c, float pull, float chroma, float light, float least, float grey)
		{
			Vector3 vector = Lab(c);
			float num = Mathf.Sqrt(vector.y * vector.y + vector.z * vector.z);
			float num2 = Mathf.Atan2(vector.z, vector.y);
			float num3 = Get(16);
			float t = Mathf.Clamp01(num / 0.03f);
			num2 += Mathf.DeltaAngle(num2 * 57.29578f, num3 * 57.29578f) * (MathF.PI / 180f) * Mathf.Lerp(1f, pull, t);
			num = Mathf.Lerp(num, chroma, pull) * (1f - grey);
			return FromLab(new Vector3(Mathf.Max(vector.x * light, least), Mathf.Cos(num2) * num, Mathf.Sin(num2) * num));
		}

		internal static Color Col(uint c)
		{
			return new Color((float)(c & 0xFF) / 255f, (float)((c >> 8) & 0xFF) / 255f, (float)((c >> 16) & 0xFF) / 255f, 1f);
		}

		private static float Lin(float c)
		{
			if (!(c <= 0.04045f))
			{
				return Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
			}
			return c / 12.92f;
		}

		private static float Srgb(float c)
		{
			c = Mathf.Max(0f, c);
			return Mathf.Clamp01((c <= 0.0031308f) ? (c * 12.92f) : (1.055f * Mathf.Pow(c, 5f / 12f) - 0.055f));
		}

		private static float Cbrt(float x)
		{
			return Mathf.Pow(Mathf.Max(0f, x), 1f / 3f);
		}

		internal static Vector3 Lab(Color c)
		{
			float num = Lin(c.r);
			float num2 = Lin(c.g);
			float num3 = Lin(c.b);
			float num4 = Cbrt(0.41222146f * num + 0.53633255f * num2 + 0.051445995f * num3);
			float num5 = Cbrt(0.2119035f * num + 0.6806995f * num2 + 0.10739696f * num3);
			float num6 = Cbrt(0.08830246f * num + 0.28171885f * num2 + 0.6299787f * num3);
			return new Vector3(0.21045426f * num4 + 0.7936178f * num5 - 0.004072047f * num6, 1.9779985f * num4 - 2.4285922f * num5 + 0.4505937f * num6, 0.025904037f * num4 + 0.78277177f * num5 - 0.80867577f * num6);
		}

		internal static Color FromLab(Vector3 lab)
		{
			float num = lab.x + 0.39633778f * lab.y + 0.21580376f * lab.z;
			float num2 = lab.x - 0.105561346f * lab.y - 0.06385417f * lab.z;
			float num3 = lab.x - 0.08948418f * lab.y - 1.2914855f * lab.z;
			num = num * num * num;
			num2 = num2 * num2 * num2;
			num3 = num3 * num3 * num3;
			return new Color(Srgb(4.0767417f * num - 3.3077116f * num2 + 0.23096994f * num3), Srgb(-1.268438f * num + 2.6097574f * num2 - 0.34131938f * num3), Srgb(-0.0041960864f * num - 0.7034186f * num2 + 1.7076147f * num3), 1f);
		}

		internal static float Smooth(float e0, float e1, float x)
		{
			float num = Mathf.Clamp01((x - e0) / (e1 - e0));
			return num * num * (3f - 2f * num);
		}
	}
}
