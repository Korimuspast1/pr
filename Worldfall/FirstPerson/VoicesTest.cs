using System;
using System.Collections;
using System.Collections.Generic;
using FirstPerson.Core;

namespace FirstPerson
{
	internal sealed class VoicesTest : CivilTest
	{
		private Actor _host;

		private Actor _npc;

		private readonly List<Actor> _village = new List<Actor>();

		protected override string Prefix => "VOICES";

		protected override IEnumerator Body()
		{
			yield return Part("every temper, people and age", Register());
			Actor actor = Village("human", 12, 3);
			if (!Check("a village founded", actor != null))
			{
				yield break;
			}
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive())
				{
					_village.Add(item);
				}
			}
			foreach (Actor item2 in _village)
			{
				if (item2 != actor && item2.isAdult() && _host == null)
				{
					_host = item2;
				}
			}
			foreach (Actor item3 in _village)
			{
				if (item3 != actor && item3 != _host && item3.isAdult() && _npc == null)
				{
					_npc = item3;
				}
			}
			if (Check("a host and someone to talk to", _host != null && _npc != null))
			{
				yield return Possess(_host);
				if (Check("first person on a villager", InControl(_host)))
				{
					yield return Part("a talk in their voice", Talk());
					yield return Part("no greeting twice in 12 talks", Openers());
					yield return Part("asked twice", Again());
					yield return Part("what they remember", Remembered());
					yield return Part("small talk and goodbye", SmallTalk());
					yield return Part("the village's personas", Personas());
				}
			}
		}

		private IEnumerator Register()
		{
			Voices.Temper[] array = new Voices.Temper[10]
			{
				Voices.Temper.Plain,
				Voices.Temper.Cruel,
				Voices.Temper.Hothead,
				Voices.Temper.Sly,
				Voices.Temper.Greedy,
				Voices.Temper.Wary,
				Voices.Temper.Pious,
				Voices.Temper.Gentle,
				Voices.Temper.Clever,
				Voices.Temper.Dull
			};
			Voices.Accent[] array2 = new Voices.Accent[4]
			{
				Voices.Accent.Human,
				Voices.Accent.Elf,
				Voices.Accent.Orc,
				Voices.Accent.Dwarf
			};
			Voices.AgeBand[] array3 = new Voices.AgeBand[4]
			{
				Voices.AgeBand.Child,
				Voices.AgeBand.Young,
				Voices.AgeBand.Grown,
				Voices.AgeBand.Elder
			};
			Voices.Slots slots = new Voices.Slots
			{
				You = "Aldric",
				Name = "Bram",
				Town = "Oakvale",
				Realm = "Ardan"
			};
			Voices.Scene s = new Voices.Scene
			{
				Hour = Voices.Hour.Day,
				Season = 1
			};
			int num = 0;
			int num2 = 0;
			long num3 = 1L;
			Voices.Temper[] array4 = array;
			foreach (Voices.Temper temper in array4)
			{
				Voices.Accent[] array5 = array2;
				foreach (Voices.Accent accent in array5)
				{
					Voices.AgeBand[] array6 = array3;
					foreach (Voices.AgeBand ageBand in array6)
					{
						Voices.Persona p = new Voices.Persona
						{
							Id = num3++,
							First = temper,
							Second = temper,
							Accent = accent,
							Age = ageBand,
							Met = true,
							Job = "farmer"
						};
						string text = Voices.Line("open", in p, in s, slots, Voices.Seed(p.Id, 1, 0, "open"), null);
						string text2 = Voices.Line("part", in p, in s, slots, Voices.Seed(p.Id, 1, 1, "part"), null);
						if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(text2))
						{
							num2++;
						}
						else
						{
							num++;
						}
						if (!Options.Contains("nolog"))
						{
							Note($"  {temper} {accent} {ageBand}: \"{text}\" ... \"{text2}\"");
						}
					}
				}
			}
			Check("every one of 160 has a greeting and a parting", num2 == 0, $"{num} said, {num2} empty");
			yield break;
		}

		private static Choice Find(Conversation c, string start)
		{
			foreach (Choice choice in c.Choices)
			{
				if (choice.Label.StartsWith(start, StringComparison.Ordinal))
				{
					return choice;
				}
			}
			return null;
		}

		private IEnumerator Say(Conversation c, string label, List<string> heard)
		{
			heard.Clear();
			for (int i = 0; i < 12; i++)
			{
				if (!c.MorePages)
				{
					break;
				}
				c.LineStart -= 999f;
				c.Next();
			}
			Choice choice = Find(c, label);
			if (choice == null)
			{
				Note("(no choice \"" + label + "\": " + string.Join(" | ", c.Choices.ConvertAll((Choice x) => x.Label).ToArray()) + ")");
			}
			else
			{
				c.LineStart -= 999f;
				c.Pick(c.Choices.IndexOf(choice));
				yield return CivilTest.Wait(0.15f);
				ReadAll(c, heard);
			}
		}

		private static void ReadAll(Conversation c, List<string> into)
		{
			into.Clear();
			for (int i = 0; i < c.Pages.Count; i++)
			{
				into.Add(c.Pages[i]);
			}
		}

		private static string FirstLine(Conversation c)
		{
			string text = ((c.Pages.Count > 0) ? c.Pages[0] : "");
			int num = text.IndexOf('\n');
			if (num < 0)
			{
				return text;
			}
			return text.Substring(0, num);
		}

		private IEnumerator Open()
		{
			Mod.EndConversation();
			yield return CivilTest.Wait(0.2f);
			CivilTest.StandBefore(_npc, _host, 1.4f);
			yield return CivilTest.Wait(0.2f);
			Mod.StartConversationForTest(_npc);
			yield return CivilTest.Wait(0.2f);
		}

		private IEnumerator Talk()
		{
			yield return Open();
			Conversation converse = Mod.Converse;
			Check("the talk opens", converse.Open);
			Check("they speak through the voice engine", converse.Slots != null && Dialogue.Voiced(converse), (converse.Slots == null) ? "no persona" : null);
			Check("the persona is theirs", converse.Persona.Id == _npc.data.id, converse.Persona.Id + " vs " + _npc.data.id);
			List<string> list = new List<string>();
			ReadAll(converse, list);
			Check("they greet you", list.Count > 0 && list[0].Length > 1, (list.Count > 0) ? list[0].Replace("\n", " / ") : null);
			Note(string.Format("persona: {0}/{1} {2} {3} {4} mood {5} warmth {6} {7} tie {8} plight {9}; scene {10} {11} season {12}", converse.Persona.First, converse.Persona.Second, converse.Persona.Age, converse.Persona.Accent, converse.Persona.Job ?? "-", converse.Persona.Mood, converse.Persona.Warmth, converse.Persona.Bond, converse.Persona.Tie, converse.Persona.Plight, converse.Scene.Hour, converse.Scene.Sky, converse.Scene.Season));
			Note("card: \"" + converse.Relation + converse.CardSuffix + "\" mood word \"" + converse.MoodWord + "\"");
		}

		private static int PieceOf(string said, Voices.Slots slots, string prefix)
		{
			foreach (KeyValuePair<string, string[]> allPool in Voices.AllPools)
			{
				if (!allPool.Key.StartsWith(prefix, StringComparison.Ordinal))
				{
					continue;
				}
				string[] value = allPool.Value;
				foreach (string line in value)
				{
					if (Voices.Fill(line, slots) == said)
					{
						return Voices.IdOf(line);
					}
				}
			}
			return -1;
		}

		private IEnumerator Openers()
		{
			List<string> openers = new List<string>();
			int remembered = 0;
			int unknown = 0;
			for (int i = 0; i < 13; i++)
			{
				List<ushort> before = new List<ushort>(Mind.Of(_npc)?.Recent ?? new List<ushort>());
				yield return Open();
				Conversation converse = Mod.Converse;
				if (!converse.Open)
				{
					Note("talk " + i + " did not open");
					continue;
				}
				string text = FirstLine(converse);
				openers.Add(text);
				int num = PieceOf(text, converse.Slots, "open.");
				if (num < 0)
				{
					unknown++;
				}
				else if (before.Contains((ushort)num))
				{
					remembered++;
					Note($"  talk {i}: \"{text}\" was among their last {before.Count} pieces");
				}
			}
			int num2 = 0;
			for (int j = 0; j < openers.Count; j++)
			{
				for (int k = Math.Max(0, j - 7); k < j; k++)
				{
					if (openers[j] == openers[k])
					{
						num2++;
						Note($"  talk {j} repeats talk {k}: \"{openers[j]}\"");
					}
				}
			}
			if (!Options.Contains("nolog"))
			{
				for (int l = 0; l < openers.Count; l++)
				{
					Note($"  talk {l}: \"{openers[l]}\"");
				}
			}
			Check("13 talks, no opener they said lately (their last 24 pieces)", openers.Count == 13 && remembered == 0 && unknown == 0, $"{openers.Count} talks, {remembered} from memory, {unknown} not from the pools");
			Check("no opener twice within 8 talks", num2 == 0, num2 + " repeats");
			Check("the openers aren't all alike", new HashSet<string>(openers).Count >= 10, new HashSet<string>(openers).Count + " different");
		}

		private IEnumerator Again()
		{
			yield return Open();
			Conversation c = Mod.Converse;
			List<string> heard = new List<string>();
			yield return Say(c, "Tell me about your family.", heard);
			string first = ((heard.Count > 0) ? heard[0] : "");
			yield return Say(c, "Let's talk about something else.", heard);
			yield return Say(c, "Tell me about your family.", heard);
			string text = ((heard.Count > 0) ? heard[0] : "");
			HashSet<string> hashSet = new HashSet<string>();
			foreach (KeyValuePair<string, string[]> allPool in Voices.AllPools)
			{
				if (allPool.Key.StartsWith("again.", StringComparison.Ordinal))
				{
					string[] value = allPool.Value;
					foreach (string line in value)
					{
						hashSet.Add(Voices.Fill(line, c.Slots));
					}
				}
			}
			string item = FirstLine(c);
			Check("the first time, a plain answer", first.Length > 0 && !hashSet.Contains(first.Split('\n')[0]), first.Replace("\n", " / "));
			Check("the second time opens with \"you asked me that\"", hashSet.Contains(item), text.Replace("\n", " / "));
		}

		private IEnumerator Remembered()
		{
			string pResult = null;
			try
			{
				_npc.data.get("fp_mind", out pResult, null);
			}
			catch
			{
			}
			Check("what they remember is in their saved data", !string.IsNullOrEmpty(pResult), pResult);
			Mind.Rec rec = Mind.Of(_npc);
			int num = rec?.Days ?? (-1);
			int num2 = rec?.Recent.Count ?? (-1);
			string text = rec?.LastTopic;
			Mind.ForgetForTest();
			Mind.Rec rec2 = Mind.Of(_npc);
			Check("...and reads back the same", rec2 != null && rec2.Days == num && rec2.Recent.Count == num2 && rec2.LastTopic == text, $"days {num}/{rec2?.Days}, recent {num2}/{rec2?.Recent.Count}, topic {text}/{rec2?.LastTopic}");
			Check($"they remember {24} pieces at most", rec2 != null && rec2.Recent.Count <= 24 && rec2.Recent.Count > 0, rec2?.Recent.Count.ToString());
			Check("the last topic was the family", text == "family", text);
			yield break;
		}

		private IEnumerator SmallTalk()
		{
			yield return Open();
			Conversation c = Mod.Converse;
			List<string> heard = new List<string>();
			List<string> said = new List<string>();
			yield return Say(c, "Small talk", heard);
			if (heard.Count > 0)
			{
				said.Add(heard[0]);
			}
			for (int i = 0; i < 3; i++)
			{
				yield return Say(c, "Go on, tell me more.", heard);
				if (heard.Count > 0)
				{
					said.Add(heard[0]);
				}
			}
			foreach (string item in said)
			{
				Note("  small talk: \"" + item.Replace("\n", " / ") + "\"");
			}
			Check("four turns of small talk, each with words", said.Count == 4 && said.TrueForAll((string s) => s.Length > 1), said.Count + " turns");
			Check("...and not the same thing each time", new HashSet<string>(said).Count >= 3, new HashSet<string>(said).Count + " different");
			yield return Say(c, "Let's talk about something else.", heard);
			Check("\"something else\" gets a word", heard.Count > 0 && heard[0].Length > 1, (heard.Count > 0) ? heard[0] : null);
			yield return Say(c, "Goodbye.", heard);
			Check("goodbye gets their parting words", heard.Count > 0 && heard[0].Length > 1 && c.EndAfter, (heard.Count > 0) ? heard[0].Replace("\n", " / ") : null);
			Mod.EndConversation();
		}

		private IEnumerator Personas()
		{
			HashSet<string> hashSet = new HashSet<string>();
			int num = 0;
			foreach (Actor item in _village)
			{
				if (item != null && item.isAlive() && item != _host)
				{
					Voice v = Dialogue.VoiceOf(item, _host);
					Voices.Persona p = FirstPerson.Personas.Of(item, _host, v);
					string text = p.First.ToString() + "/" + p.Second.ToString() + " " + p.Age.ToString() + " " + p.Mood;
					hashSet.Add(text);
					num++;
					Note(string.Format("  {0}: {1} {2} {3} warmth {4} {5} tie {6} \"{7}\"", Dialogue.NameOf(item), text, p.Accent, p.Job ?? "-", p.Warmth, p.Bond, p.Tie, Voices.MoodWord(in p)));
				}
			}
			Check("the villagers aren't all one persona", num < 3 || hashSet.Count >= 2, $"{hashSet.Count} kinds among {num}");
			yield break;
		}
	}
}
