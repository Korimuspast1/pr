using UnityEngine;

namespace FirstPerson
{
	internal static class PhotoMode
	{
		public static bool Active { get; private set; }

		public static float FovOffset { get; private set; }

		public static void Toggle(Action<string> toast)
		{
			Active = !Active;
			if (Active)
				toast?.Invoke("Photo mode: HUD hidden — F10 to exit, F9 screenshot, WASD+mouse fly");
			else
				toast?.Invoke("Photo mode off");
			Log.Info("photo: " + (Active ? "on" : "off"));
		}

		public static void Update(bool menu)
		{
			if (!Active) return;
			// allow FOV tweaking with +/-
			if (PlayInput.GetKey(KeyCode.Equals) || PlayInput.GetKey(KeyCode.Plus) || PlayInput.GetKey(KeyCode.KeypadPlus))
				FovOffset = Mathf.Min(30f, FovOffset + Time.unscaledDeltaTime * 12f);
			if (PlayInput.GetKey(KeyCode.Minus) || PlayInput.GetKey(KeyCode.KeypadMinus))
				FovOffset = Mathf.Max(-30f, FovOffset - Time.unscaledDeltaTime * 12f);
			if (PlayInput.GetKeyDown(KeyCode.Backspace))
				FovOffset = 0f;
		}
	}
}
