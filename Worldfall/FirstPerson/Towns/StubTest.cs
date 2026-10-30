using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal abstract class StubTest : MonoBehaviour
	{
		internal static bool NoQuit;

		internal static readonly List<string> Ran = new List<string>();

		protected abstract string Keyword { get; }

		protected abstract string Package { get; }

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = (File.Exists(path) ? File.ReadAllText(path) : "");
			while (!Config.game_loaded || World.world == null)
			{
				yield return null;
			}
			yield return new WaitForSecondsRealtime(NoQuit ? 0.1f : 2f);
			string text = Keyword.ToUpperInvariant();
			Log.Info(text + " " + Keyword + ": not built yet (package " + Package + " of the Towns update)");
			Log.Info(text + " 0 passed, 0 failed");
			Log.Info(text + " DONE");
			Ran.Add(Keyword);
			if (NoQuit)
			{
				Object.Destroy(this);
			}
			else if (options.Contains("quit"))
			{
				Application.Quit();
			}
		}
	}
}
