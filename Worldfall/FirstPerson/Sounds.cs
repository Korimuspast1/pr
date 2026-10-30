using UnityEngine;

namespace FirstPerson
{
	internal static class Sounds
	{
		public const string Click = "event:/SFX/Ui/Click";

		public const string WindowOpen = "event:/SFX/UI/WindowWhoosh";

		public const string WindowClose = "event:/SFX/UI/WindowClose";

		public static void PlayAt(string eventPath, Vector2 at)
		{
			try
			{
				MusicBox.playSound(eventPath, at.x, at.y);
			}
			catch
			{
			}
		}

		public static void PlayUI(string eventPath)
		{
			try
			{
				MusicBox.playSoundUI(eventPath);
			}
			catch
			{
			}
		}
	}
}
