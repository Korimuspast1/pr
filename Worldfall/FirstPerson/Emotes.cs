using System;
using UnityEngine;

namespace FirstPerson
{
	internal static class Emotes
	{
		public static readonly string[] Names = new string[] { "Wave", "Bow", "Cheer", "Laugh", "Dance", "Shrug" };
		public static readonly string[] Chat = new string[] { "*waves*", "*bows*", "*cheers!*", "*laughs*", "*dances*", "*shrugs*" };
		public static readonly string[] Icons = new string[] { "iconArrowDestination", "iconFavoriteStar", "iconShield", "iconTalk", "iconMusic", "iconQuestionMark" };

		public static void Do(int idx, Actor host, Action<string> toast)
		{
			if (host == null || idx < 0 || idx >= Names.Length) return;
			string msg = Chat[idx];
			try
			{
				// Try to send as local chat if multiplayer, else toast
				if (Net.NetSession.Current != null && !Net.NetSession.Current.IsSolo)
				{
					PlayerTalk.Say(msg);
				}
			}
			catch { }
			toast?.Invoke(Names[idx] + " — " + msg);
			Log.Info("emote: " + Names[idx] + " by " + WorldBoxMod.Describe(host));
			// trigger audio cue
			try { WorldBoxMod.Instance.AudioForTest?.PlayEffect("event:/SFX/UI/Click", 0.9f); } catch { }
		}
	}
}
