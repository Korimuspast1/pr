using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FMOD;
using FMODUnity;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class OldGameNotice : MonoBehaviour
	{
		internal string Message;

		private GUIStyle _box;

		private GUIStyle _button;

		private static readonly bool Quiet = Environment.GetEnvironmentVariable("FIRSTPERSON_QUIET") == "1";

		private float _quietAt;

		internal static string Text(string version)
		{
			return "Worldfall needs WorldBox 0.51 or newer and stays off. This game is " + version + ". In Steam open WorldBox's Properties, set Betas to None and let it update.";
		}

		[DllImport("user32.dll")]
		private static extern bool ShowWindow(IntPtr window, int command);

		[DllImport("user32.dll")]
		private static extern bool IsIconic(IntPtr window);

		private void Update()
		{
			if (!Quiet || Time.unscaledTime < _quietAt)
			{
				return;
			}
			_quietAt = Time.unscaledTime + 0.5f;
			Application.runInBackground = true;
			try
			{
				if (Screen.fullScreenMode != FullScreenMode.Windowed || Screen.width > 960)
				{
					Screen.SetResolution(960, 540, FullScreenMode.Windowed);
				}
				IntPtr mainWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
				if (mainWindowHandle != IntPtr.Zero && !IsIconic(mainWindowHandle))
				{
					ShowWindow(mainWindowHandle, 7);
				}
				if (RuntimeManager.CoreSystem.getMasterChannelGroup(out var channelgroup) == RESULT.OK)
				{
					channelgroup.setMute(mute: true);
				}
			}
			catch
			{
			}
		}

		private void OnGUI()
		{
			if (!string.IsNullOrEmpty(Message))
			{
				GUI.depth = -1000;
				int num = Mathf.Max(14, Screen.height / 45);
				if (_box == null)
				{
					_box = new GUIStyle(GUI.skin.box)
					{
						wordWrap = true,
						alignment = TextAnchor.MiddleCenter
					};
					_button = new GUIStyle(GUI.skin.button);
				}
				_box.fontSize = num;
				_button.fontSize = num;
				float num2 = Mathf.Min((float)Screen.width - 32f, (float)num * 34f);
				float height = _box.CalcHeight(new GUIContent(Message), num2) + (float)num * 1.2f;
				Rect position = new Rect(((float)Screen.width - num2) * 0.5f, num, num2, height);
				GUI.Box(position, Message, _box);
				if (GUI.Button(new Rect(position.center.x - (float)num * 3f, position.yMax + (float)num * 0.4f, (float)num * 6f, (float)num * 2f), "OK", _button))
				{
					UnityEngine.Object.Destroy(this);
				}
			}
		}
	}
}
