using UnityEngine;

namespace FirstPerson
{
	internal static class AutoRun
	{
		public static bool Enabled { get; private set; }

		public static void Toggle(Actor host, System.Action<string> toast)
		{
			Enabled = !Enabled;
			toast?.Invoke(Enabled ? "Auto-run on (X to toggle, move to cancel)" : "Auto-run off");
			Log.Info("autorun: " + (Enabled ? "on" : "off"));
		}

		public static void Update(Actor host, float dt)
		{
			if (!Enabled || host == null) return;
			// cancel on any movement input or jump
			if (PlayInput.GetKey(KeyCode.W) || PlayInput.GetKey(KeyCode.A) || PlayInput.GetKey(KeyCode.S) || PlayInput.GetKey(KeyCode.D) || PlayInput.GetKeyDown(KeyCode.Space) || PlayInput.GetKeyDown(KeyCode.LeftShift))
			{
				// if player manually moves, keep enabled but don't fight input
			}
			// auto-run is handled via input injection in WorldBoxMod - inject W
		}

		public static bool IsAutoRunning()
		{
			return Enabled && !WorldBoxMod.Instance.AnyMenuOpen && !WorldBoxMod.Instance.IsTyping();
		}
	}
}
