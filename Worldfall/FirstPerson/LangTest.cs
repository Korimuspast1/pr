using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class LangTest : CivilTest
	{
		protected override string Prefix => "LANG";

		private IEnumerator Capture(string name)
		{
			yield return CivilTest.Wait(0.7f);
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, name));
			yield return null;
			yield return null;
			Note("captured " + name);
		}

		protected override IEnumerator Body()
		{
			string code = "cz";
			int num = Options.IndexOf("lang=");
			if (num >= 0)
			{
				string text = Options.Substring(num + 5).Trim();
				int num2 = text.IndexOfAny(new char[3] { ' ', '\n', '\r' });
				code = ((num2 < 0) ? text : text.Substring(0, num2));
			}
			string was = Mod.Settings.Language;
			Mod.Settings.Language = code;
			yield return null;
			yield return null;
			Check("the language is " + code, Lang.Code == code, Lang.Code + " " + Lang.Name);
			string text2 = Lang.T("Field of view");
			Check("a plain text", text2 != "Field of view", text2);
			string text3 = Lang.T("120 tiles");
			Check("a template keeps its number", text3 != "120 tiles" && text3.Contains("120"), text3);
			string text4 = Lang.T("Store 7 resources");
			Check("a prompt's label", text4 != "Store 7 resources" && text4.Contains("7"), text4);
			string text5 = Lang.T("Ana");
			Check("a name stays as it is", text5 == "Ana", text5);
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Mod.SetSettingsMenu(open: true);
			SettingsMenu menu = Mod.SettingsMenu;
			for (int tab = 0; tab < SettingsMenu.Tabs.Length; tab++)
			{
				menu.Tab = tab;
				yield return Capture("lang_set_" + SettingsMenu.Tabs[tab].ToLowerInvariant() + ".png");
			}
			Mod.SetSettingsMenu(open: false);
			yield return CivilTest.Wait(0.6f);
			Actor actor = Village("human", 10, 2);
			if (!Check("a village", actor != null))
			{
				yield break;
			}
			List<Actor> list = new List<Actor>(Founded);
			Actor host = list[1];
			Actor npc = list[2];
			yield return Possess(host);
			if (!Check("first person", InControl(host)))
			{
				yield break;
			}
			CivilTest.StandBefore(npc, host, 1.6f);
			Mod.DebugPrompt = "E:Talk|R:Reload|LMB:Collect honey";
			Mod.Toast("Not out at sea. Leave the cabin first (E)");
			yield return Capture("lang_hud.png");
			Mod.DebugPrompt = null;
			Mod.HintUntil = Time.unscaledTime + 10f;
			yield return Capture("lang_help.png");
			Mod.HintUntil = 0f;
			Mod.StartConversationForTest(npc);
			yield return Capture("lang_talk.png");
			Conversation converse = Mod.Converse;
			Choice choice = converse.Choices.Find((Choice x) => x.Label.StartsWith("Tell me about your family"));
			if (choice != null)
			{
				converse.LineStart -= 999f;
				converse.Pick(converse.Choices.IndexOf(choice));
			}
			yield return Capture("lang_talk_family.png");
			Mod.EndConversation();
			yield return CivilTest.Wait(0.4f);
			Mod.Craft.Open = true;
			yield return Capture("lang_inventory.png");
			Mod.Craft.Open = false;
			WorldNews.Open = true;
			yield return Capture("lang_news.png");
			WorldNews.Open = false;
			Journal.Open = true;
			yield return Capture("lang_journal.png");
			Journal.Open = false;
			yield return CivilTest.Wait(0.6f);
			Note("drawn with no translation: " + Lang.MissingCount + " texts (lang/missing_" + code + ".txt)");
			yield return CivilTest.Wait(5.5f);
			string path = Path.Combine(Path.Combine(Log.Folder, "lang"), "missing_" + code + ".txt");
			if (File.Exists(path))
			{
				int num3 = 0;
				string[] array = File.ReadAllLines(path);
				foreach (string text6 in array)
				{
					if (!text6.StartsWith("#") && text6.Trim().Length != 0)
					{
						Note("  missing: " + text6);
						if (++num3 >= 80)
						{
							break;
						}
					}
				}
			}
			Mod.Settings.Language = was;
			yield return null;
			yield return null;
			Check("the language is put back", Lang.Code != code || was == code, Lang.Code);
		}
	}
}
