using System;
using UnityEngine;

namespace FirstPerson
{
	internal static class HelmetLight
	{
		private const string Key = "fp_headlamp";

		public static bool Enabled { get; private set; }

		public static float Intensity { get; private set; } = 1f;

		public static void Load(Actor a)
		{
			if (a == null) { Enabled = false; return; }
			try
			{
				a.data.get(Key, out int v, 0);
				Enabled = v != 0;
			}
			catch { Enabled = false; }
		}

		public static void Save(Actor a)
		{
			if (a == null) return;
			try { a.data.set(Key, Enabled ? 1 : 0); } catch { }
		}

		public static void Toggle(Actor a, Action<string> toast)
		{
			Enabled = !Enabled;
			Save(a);
			toast?.Invoke(Enabled ? "Headlamp on (L)" : "Headlamp off (L)");
			Log.Info("helmet: " + (Enabled ? "on" : "off") + " for " + (a != null ? WorldBoxMod.Describe(a) : "null"));
		}

		public static void SetForActor(Actor a)
		{
			Load(a);
		}

		// Call each frame to emit light into the current light list
		public static void Emit(Vec3 eye, float yaw, float pitch, System.Collections.Generic.List<Core.PointLight> lights, float dt)
		{
			if (!Enabled || lights == null) return;
			// warm white, radius depends on night darkness but keep simple
			float radius = 14f;
			float glow = 0.35f;
			// slight ahead of eyes to avoid self-shadow
			float ahead = 0.4f;
			float cosYaw = Mathf.Cos(yaw);
			float sinYaw = Mathf.Sin(yaw);
			// pitch slightly down
			float cosPitch = Mathf.Cos(pitch * (float)Math.PI / 180f);
			float sinPitch = Mathf.Sin(pitch * (float)Math.PI / 180f);
			Vec3 forward = new Vec3(cosYaw * cosPitch, sinYaw * cosPitch, -sinPitch);
			Vec3 pos = eye + forward * ahead + new Vec3(0, 0, 0.25f);

			// flicker subtle
			float flick = 1f + 0.03f * Mathf.Sin(Time.unscaledTime * 7f) + 0.02f * Mathf.Sin(Time.unscaledTime * 3.3f);
			float s = Intensity * flick;

			lights.Add(new Core.PointLight
			{
				X = pos.X,
				Y = pos.Y,
				Z = pos.Z,
				Radius = radius,
				R = 1.0f * s,
				G = 0.96f * s,
				B = 0.82f * s,
				Glow = glow
			});
		}
	}
}
