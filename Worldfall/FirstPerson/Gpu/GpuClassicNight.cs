using System;
using FirstPerson.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPerson.Gpu
{
	internal static class GpuClassicNight
	{
		public static float Gain = 1f;

		public static bool Wanted
		{
			get
			{
				try
				{
					return WorldBoxMod.Instance != null && WorldBoxMod.Instance.Settings != null && WorldBoxMod.Instance.Settings.PixelDistance >= 1;
				}
				catch
				{
					return false;
				}
			}
		}

		public static void Apply(CommandBuffer cmd, FrameRenderer cfg, bool classic)
		{
			Gain = 1f;
			cmd.SetGlobalVector("_FP_NightLamp", Vector4.zero);
			Atmosphere air = cfg.Air;
			if (Wanted && air != null && !air.Indoors && !(GpuDark.Shown > 0.001f))
			{
				float num = GpuDark.Smooth(0.15f, 0.85f, air.Night);
				if (!(num <= 0.001f))
				{
					float num2 = GpuStyle.Value("night_fill");
					float num3 = GpuStyle.Value("night_moon");
					Vector3 vector = new Vector3(num2 * 0.85f, num2 * 0.95f, Mathf.Min(1f, num2 * 1.5f));
					cmd.SetGlobalVector("_FP_NightLamp", new Vector4(GpuStyle.Value("night_lamp") * num, 1f / Mathf.Lerp(1f, vector.x, num), 1f / Mathf.Lerp(1f, vector.y, num), 1f / Mathf.Lerp(1f, vector.z, num)));
					Vector3 vector2 = new Vector3(num3 * 0.8f, num3 * 0.9f, Mathf.Min(1f, num3 * 1.3f));
					float num4 = (classic ? 1f : air.CardFillR);
					float num5 = (classic ? 1f : air.CardFillG);
					float num6 = (classic ? 1f : air.CardFillB);
					cmd.SetGlobalVector("_FP_AmbCol", new Vector4(air.AmbR * num4 * Mathf.Lerp(1f, vector.x, num), air.AmbG * num5 * Mathf.Lerp(1f, vector.y, num), air.AmbB * num6 * Mathf.Lerp(1f, vector.z, num), 0f));
					cmd.SetGlobalVector("_FP_LightCol", new Vector4(air.LightR * Mathf.Lerp(1f, vector2.x, num), air.LightG * Mathf.Lerp(1f, vector2.y, num), air.LightB * Mathf.Lerp(1f, vector2.z, num), 0f));
					float num7 = GpuStyle.Value("night_sky");
					float hue = GpuStyle.Value("night_hue");
					float num8 = GpuStyle.Value("night_chroma");
					float num9 = GpuStyle.Value("night_clouds");
					cmd.SetGlobalVector("_FP_Fog0", Indigo(GpuDark.Col(air.Horizon), num, num7, hue, num8));
					cmd.SetGlobalVector("_FP_Fog1", Indigo(GpuDark.Col(air.SunHorizon), num, num7, hue, num8));
					cmd.SetGlobalVector("_FP_Zenith", Indigo(GpuDark.Col(air.Zenith), num, Mathf.Min(1f, num7 * 1.35f), hue, num8 * 1.2f));
					cmd.SetGlobalVector("_FP_CloudLit", Indigo(GpuDark.Col(air.CloudLit), num, num9, hue, num8 * 0.6f));
					cmd.SetGlobalVector("_FP_CloudShade", Indigo(GpuDark.Col(air.CloudShade), num, num9 * 0.85f, hue, num8 * 0.8f));
				}
			}
		}

		private static Color Indigo(Color c, float n, float light, float hue, float chroma)
		{
			Vector3 vector = GpuDark.Lab(c);
			float num = Mathf.Sqrt(vector.y * vector.y + vector.z * vector.z);
			float num2 = Mathf.Atan2(vector.z, vector.y);
			float t = Mathf.Clamp01(num / 0.03f);
			num2 += Mathf.DeltaAngle(num2 * 57.29578f, hue * 57.29578f) * (MathF.PI / 180f) * n * Mathf.Lerp(1f, 0.85f, t);
			num = Mathf.Lerp(num, Mathf.Max(num, chroma), n);
			return GpuDark.FromLab(new Vector3(vector.x * Mathf.Lerp(1f, light, n), Mathf.Cos(num2) * num, Mathf.Sin(num2) * num));
		}
	}
}
