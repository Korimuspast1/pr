using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal static class Weight
	{
		private static readonly Dictionary<string, float> Peoples = new Dictionary<string, float>
		{
			{ "elf", 0.8f },
			{ "human", 1f },
			{ "dwarf", 1.15f },
			{ "orc", 1.3f }
		};

		public const float Least = 0.3f;

		public const float Most = 2.5f;

		private static Actor _last;

		private static float _lastWeight;

		private static float _checked = -99f;

		public static float Of(Actor a)
		{
			if (a == null)
			{
				return 1f;
			}
			float unscaledTime = Time.unscaledTime;
			if (a == _last && unscaledTime - _checked < 1f)
			{
				return _lastWeight;
			}
			_last = a;
			_checked = unscaledTime;
			_lastWeight = Work(a);
			return _lastWeight;
		}

		private static float Work(Actor a)
		{
			try
			{
				if (Crabzillas.Is(a))
				{
					return 2.5f;
				}
				string text = ((a.asset != null) ? a.asset.id : null);
				float num;
				if (text != null && Peoples.TryGetValue(text, out var value))
				{
					num = value;
				}
				else
				{
					float num2 = 65f;
					try
					{
						num2 = Mathf.Max(0.5f, ((BaseSimObject)a).stats["mass_2"]);
					}
					catch
					{
					}
					num = Mathf.Pow(num2 / 65f, 0.4f);
					if (a.asset != null && !a.asset.is_humanoid)
					{
						num *= 0.8f;
					}
					if (a.hasTrait("weightless"))
					{
						num *= 0.85f;
					}
				}
				if (a.isBaby())
				{
					num *= 0.8f;
				}
				return Mathf.Clamp(num, 0.3f, 2.5f);
			}
			catch
			{
				return 1f;
			}
		}

		public static void Clear()
		{
			_last = null;
			_checked = -99f;
		}
	}
}
