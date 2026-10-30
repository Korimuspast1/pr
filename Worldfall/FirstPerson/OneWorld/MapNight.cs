using System;
using UnityEngine;

namespace FirstPerson.OneWorld
{
	internal static class MapNight
	{
		private static readonly float[] Strength = new float[3] { 0f, 0.3f, 0.6f };

		private static readonly string[] Layers = new string[9] { "chaos", "moon", "magic", "sun", "rain_darkness", "winter", "ash", "night", "rain" };

		private const int NightLayer = 7;

		private static readonly float[] _alpha = new float[Layers.Length];

		private static readonly bool[] _on = new bool[Layers.Length];

		private static bool _set;

		internal static float ValueForTest;

		internal static float LayerForTest;

		internal static int StrengthForTest = -1;

		public static void Apply(Settings settings, DayCycle day, bool flatMap)
		{
			WorldAgeEffects instance = WorldAgeEffects.instance;
			WorldAgeManager worldAgeManager = null;
			try
			{
				worldAgeManager = World.world?.era_manager;
			}
			catch
			{
			}
			if (instance == null || worldAgeManager == null || instance.dict_effects == null)
			{
				Off();
				return;
			}
			int value = ((StrengthForTest >= 0) ? StrengthForTest : settings.MapNight);
			float num = (flatMap ? (NightAt(day.Hour) * Strength[Mathf.Clamp(value, 0, 2)]) : 0f);
			if (num <= 0.001f)
			{
				Off();
				return;
			}
			float a = 0f;
			try
			{
				a = worldAgeManager._night_multiplier;
			}
			catch
			{
			}
			float num2 = Mathf.Max(a, num);
			try
			{
				if (!_set)
				{
					for (int i = 0; i < Layers.Length; i++)
					{
						if (instance.dict_effects.TryGetValue(Layers[i], out var value2) && value2 != null)
						{
							_alpha[i] = value2.color.a;
							_on[i] = value2.enabled;
						}
					}
				}
				instance.override_night = true;
				instance.night_value_mat = num2;
				instance.night_value_top = 0f;
				_set = true;
				ValueForTest = num2;
				Layered(instance, num * 0.5f);
			}
			catch (Exception ex)
			{
				Log.Error("map night: " + ex.Message);
				Off();
			}
		}

		private static void Layered(WorldAgeEffects fx, float clockLayer)
		{
			WorldAgeAsset worldAgeAsset = null;
			try
			{
				worldAgeAsset = World.world_era;
			}
			catch
			{
			}
			float num = 1f;
			try
			{
				num = (float)PlayerConfig.getIntValue("age_overlay_effect") / 100f;
			}
			catch
			{
			}
			float num2 = ((worldAgeAsset != null) ? (worldAgeAsset.era_effect_overlay_alpha * num) : 0f);
			float num3 = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
			for (int i = 0; i < Layers.Length; i++)
			{
				if (!fx.dict_effects.TryGetValue(Layers[i], out var value) || value == null)
				{
					continue;
				}
				if (worldAgeAsset != null && Flag(worldAgeAsset, i))
				{
					_on[i] = num > 0f;
					_alpha[i] = ((_alpha[i] < num2) ? Mathf.Min(num2, _alpha[i] + num3 * 0.2f) : Mathf.Max(num2, _alpha[i] - num3 * 0.7f));
				}
				else if (_on[i] && _alpha[i] > 0f)
				{
					_alpha[i] -= num3 * 0.2f;
					if (_alpha[i] <= 0f)
					{
						_alpha[i] = 0f;
						_on[i] = false;
					}
				}
				float num4 = _alpha[i];
				bool enabled = _on[i];
				if (i == 7 && clockLayer > num4)
				{
					num4 = clockLayer;
					enabled = true;
				}
				Color color = value.color;
				color.a = num4;
				value.color = color;
				value.enabled = enabled;
				if (i == 7)
				{
					LayerForTest = num4;
				}
			}
		}

		private static bool Flag(WorldAgeAsset era, int layer)
		{
			switch (layer)
			{
			case 0:
				return era.overlay_chaos;
			case 1:
				return era.overlay_moon;
			case 2:
				return era.overlay_magic;
			case 3:
				return era.overlay_sun;
			case 4:
				return era.overlay_rain_darkness;
			case 5:
				return era.overlay_winter;
			case 6:
				return era.overlay_ash;
			case 7:
				return era.overlay_night;
			default:
				return era.overlay_rain;
			}
		}

		public static void Off()
		{
			ValueForTest = 0f;
			LayerForTest = 0f;
			if (!_set)
			{
				return;
			}
			_set = false;
			try
			{
				WorldAgeEffects instance = WorldAgeEffects.instance;
				if (instance != null)
				{
					instance.override_night = false;
				}
			}
			catch
			{
			}
		}

		internal static float NightAt(float hour)
		{
			hour -= Mathf.Floor(hour);
			float num = Mathf.Clamp01((Mathf.Sin((hour - 0.25f) * 2f * MathF.PI) * 64f * (MathF.PI / 180f) + 0.16f) / 0.22f);
			return 1f - num * num * (3f - 2f * num);
		}
	}
}
