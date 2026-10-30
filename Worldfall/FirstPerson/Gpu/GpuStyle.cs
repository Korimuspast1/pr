using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace FirstPerson.Gpu
{
	internal static class GpuStyle
	{
		public static readonly Vector4[] Params = new Vector4[16];

		private static readonly Dictionary<string, int> Slots = new Dictionary<string, int>
		{
			{ "back_start", 0 },
			{ "back_end", 1 },
			{ "haze_gain", 2 },
			{ "haze_bands", 3 },
			{ "haze_hue", 4 },
			{ "haze_chroma", 5 },
			{ "haze_lift", 6 },
			{ "haze_tint", 7 },
			{ "mist", 8 },
			{ "mist_height", 9 },
			{ "value_keep", 10 },
			{ "far_desat", 11 },
			{ "brush", 12 },
			{ "brush_near", 13 },
			{ "outline", 14 },
			{ "outline_gap", 15 },
			{ "levels_near", 16 },
			{ "levels_far", 17 },
			{ "hue_shift", 18 },
			{ "chroma_steps", 19 },
			{ "saturation", 20 },
			{ "contrast", 21 },
			{ "dither", 22 },
			{ "vignette", 23 },
			{ "warm_r", 24 },
			{ "warm_g", 25 },
			{ "warm_b", 26 },
			{ "hue_steps", 27 },
			{ "cool_r", 28 },
			{ "cool_g", 29 },
			{ "cool_b", 30 },
			{ "shadow_lift", 31 },
			{ "style", 32 },
			{ "bloom", 33 },
			{ "bloom_from", 34 },
			{ "shafts", 35 },
			{ "supersample", 36 },
			{ "cloud_scale", 37 },
			{ "cloud_contrast", 38 },
			{ "shoulder", 39 },
			{ "pixel_sky", 40 },
			{ "pixel_jitter", 41 },
			{ "pixel_shades", 42 },
			{ "pixel_full", 43 },
			{ "pixel_dither", 44 },
			{ "pixel_split", 45 },
			{ "indoor_lift", 46 },
			{ "pixel_crisp", 47 },
			{ "night_fill", 48 },
			{ "night_moon", 49 },
			{ "night_sky", 50 },
			{ "night_hue", 51 },
			{ "night_chroma", 52 },
			{ "night_clouds", 53 },
			{ "pixel_near", 54 },
			{ "night_lamp", 55 }
		};

		private static readonly Dictionary<string, float> Painterly = new Dictionary<string, float>
		{
			{ "back_start", 0.22f },
			{ "back_end", 0.78f },
			{ "haze_gain", 0.35f },
			{ "haze_bands", 5f },
			{ "haze_hue", 0.45f },
			{ "haze_chroma", 1.8f },
			{ "haze_lift", 0f },
			{ "haze_tint", 0.8f },
			{ "mist", 0.3f },
			{ "mist_height", 2.5f },
			{ "value_keep", 0.5f },
			{ "far_desat", 0f },
			{ "brush", 5f },
			{ "brush_near", 0f },
			{ "outline", 0.6f },
			{ "outline_gap", 0.2f },
			{ "levels_near", 18f },
			{ "levels_far", 10f },
			{ "hue_shift", 0.4f },
			{ "chroma_steps", 10f },
			{ "saturation", 1.1f },
			{ "contrast", 1.1f },
			{ "dither", 0.35f },
			{ "vignette", 0.25f },
			{ "warm_r", 1f },
			{ "warm_g", 0.75f },
			{ "warm_b", 0.4f },
			{ "hue_steps", 0f },
			{ "cool_r", 0.35f },
			{ "cool_g", 0.3f },
			{ "cool_b", 0.75f },
			{ "shadow_lift", 0.06f },
			{ "style", 1f },
			{ "bloom", 0.4f },
			{ "bloom_from", 0.8f },
			{ "shafts", 1f },
			{ "supersample", 2f },
			{ "cloud_scale", 0.6f },
			{ "cloud_contrast", 1.3f },
			{ "shoulder", 0.72f },
			{ "pixel_sky", -9f },
			{ "pixel_jitter", 0.7f },
			{ "pixel_shades", 10f },
			{ "pixel_full", 0.7f },
			{ "pixel_dither", 0f },
			{ "pixel_split", 0.003f },
			{ "indoor_lift", 0.1f },
			{ "pixel_crisp", 0f },
			{ "night_fill", 0.55f },
			{ "night_moon", 0.6f },
			{ "night_sky", 0.6f },
			{ "night_hue", 4.6f },
			{ "night_chroma", 0.045f },
			{ "night_clouds", 0.7f },
			{ "night_lamp", 0f },
			{ "pixel_near", 0f }
		};

		private static string _path;

		private static DateTime _stamp;

		private static float _nextCheck = -1f;

		private static bool _classic;

		private static bool _retro;

		public static int Version;

		public static int Supersample => Mathf.Clamp(Mathf.RoundToInt(GpuDark.Wanted ? Mathf.Max(2f, Get(36)) : Get(36)), 1, 4);

		public static float PixelFull => Mathf.Clamp(Get(43), 0.1f, 1f);

		private static float Get(int slot)
		{
			return Params[slot >> 2][slot & 3];
		}

		public static float Value(string key)
		{
			if (!Slots.TryGetValue(key, out var value))
			{
				return 0f;
			}
			return Get(value);
		}

		public static void Update(bool classic)
		{
			if (_path == null)
			{
				_path = Path.Combine(Log.Folder, "gpu_style.txt");
			}
			bool num = Time.unscaledTime >= _nextCheck;
			bool wanted = GpuClassicNight.Wanted;
			if (!num && classic == _classic && wanted == _retro && Version > 0)
			{
				return;
			}
			_nextCheck = Time.unscaledTime + 1f;
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
			if (classic == _classic && wanted == _retro && dateTime == _stamp && Version > 0)
			{
				return;
			}
			_classic = classic;
			_retro = wanted;
			_stamp = dateTime;
			foreach (KeyValuePair<string, float> item in Painterly)
			{
				Set(item.Key, item.Value);
			}
			if (classic)
			{
				string[] array = new string[10] { "haze_gain", "haze_bands", "haze_tint", "mist", "far_desat", "brush", "outline", "style", "bloom", "indoor_lift" };
				for (int i = 0; i < array.Length; i++)
				{
					Set(array[i], 0f);
				}
			}
			if (classic)
			{
				Set("cloud_scale", 1f);
				Set("cloud_contrast", 1f);
			}
			if (classic)
			{
				Set("value_keep", 1f);
			}
			if (classic || wanted)
			{
				Set("pixel_crisp", 1f);
				Set("pixel_shades", 0f);
				Set("pixel_near", 6f);
			}
			if (classic)
			{
				Set("supersample", 1f);
			}
			if (dateTime != DateTime.MinValue)
			{
				try
				{
					string[] array = File.ReadAllLines(_path);
					for (int i = 0; i < array.Length; i++)
					{
						string text = array[i];
						int num2 = text.IndexOf('#');
						if (num2 >= 0)
						{
							text = text.Substring(0, num2);
						}
						int num3 = text.IndexOf('=');
						if (num3 > 0)
						{
							string key = text.Substring(0, num3).Trim();
							if (float.TryParse(text.Substring(num3 + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
							{
								Set(key, result);
							}
						}
					}
					Log.Info("gpu: style file read (" + _path + ")");
				}
				catch (Exception ex)
				{
					Log.Error("gpu: style file: " + ex.Message);
				}
			}
			Version++;
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
	}
}
