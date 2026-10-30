using System;
using System.Collections.Generic;
using System.Text;

namespace FirstPerson.Core
{
	public static class Mumble
	{
		public enum Tone : byte
		{
			Normal,
			Happy,
			Sad,
			Angry,
			Dark
		}

		public struct Speaker
		{
			public int Seed;

			public bool Female;

			public bool Child;

			public bool Elder;

			public bool Young;

			public bool Broken;

			public bool Flat;

			public Tone Tone;

			public float Pace;
		}

		public sealed class Phrase
		{
			public float[] S;

			public int Syllables;

			public float Seconds => (float)S.Length / 48000f;
		}

		private struct Unit
		{
			public char Vowel;

			public int Sentence;

			public bool WordEnd;

			public bool SentenceEnd;

			public char Punct;

			public bool Emphasis;

			public bool Light;

			public uint Key;
		}

		private struct Note
		{
			public int Unit;

			public float Dur;

			public float Gap;

			public float St0;

			public float St1;

			public float Db;

			public bool Fade;
		}

		private sealed class Rng
		{
			private uint _s;

			public Rng(uint seed)
			{
				_s = ((seed == 0) ? 1831565813u : seed);
			}

			public uint Next()
			{
				_s ^= _s << 13;
				_s ^= _s >> 17;
				_s ^= _s << 5;
				return _s;
			}

			public float Unit()
			{
				return (float)(Next() & 0xFFFFFF) / 16777216f;
			}

			public int Int(int n)
			{
				return (int)(Next() % (uint)n);
			}
		}

		public static int PulseBlend = 2;

		private const int Rate = 48000;

		public static Phrase Line(VoiceSet set, Speaker who, string text, float seconds, bool opening = false)
		{
			if (set == null || set.Usable == 0 || string.IsNullOrEmpty(text))
			{
				return null;
			}
			List<Unit> list = Units(text);
			if (list.Count == 0)
			{
				return null;
			}
			Rng rng = new Rng(Hash(who.Seed, text));
			float num = RateOf(who);
			int n = (opening ? Math.Min(list.Count, 1 + rng.Int(3)) : Clamp((int)Math.Round(seconds * num), 1, list.Count));
			List<int> picks = (opening ? First(n) : Choose(list, n));
			List<Note> notes = Plan(list, picks, who, rng, opening);
			if (!opening)
			{
				Fit(notes, list, seconds, who);
			}
			return Render(set, who, list, notes, rng);
		}

		public static Phrase Chatter(VoiceSet set, Speaker who, int seed)
		{
			if (set == null || set.Usable == 0)
			{
				return null;
			}
			Rng rng = new Rng((uint)((int)(seed * 2654435761u) ^ who.Seed ^ -1640531527));
			int num = 1 + rng.Int(3);
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < num; i++)
			{
				stringBuilder.Append("bdghklmnrstvw"[rng.Int("bdghklmnrstvw".Length)]);
				stringBuilder.Append("aeiou"[rng.Int("aeiou".Length)]);
				if (i + 1 < num && rng.Unit() < 0.5f)
				{
					stringBuilder.Append(' ');
				}
			}
			float num2 = rng.Unit();
			stringBuilder.Append((who.Tone == Tone.Angry && num2 < 0.35f) ? "!" : ((num2 < 0.25f) ? "?" : "."));
			List<Unit> list = Units(stringBuilder.ToString());
			List<int> list2 = new List<int>();
			for (int j = 0; j < list.Count; j++)
			{
				list2.Add(j);
			}
			return Render(set, who, list, Plan(list, list2, who, rng, opening: false), rng);
		}

		private static List<Unit> Units(string text)
		{
			List<Unit> list = new List<Unit>();
			StringBuilder stringBuilder = new StringBuilder(text.Length);
			bool flag = false;
			string text2 = text;
			foreach (char c in text2)
			{
				if (c == '*')
				{
					flag = !flag;
				}
				else if (!flag)
				{
					stringBuilder.Append(c);
				}
			}
			string[] array = stringBuilder.ToString().Split(new char[4] { ' ', '\n', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries);
			int num = 0;
			int first = 0;
			int longest = -1;
			int num2 = 5;
			StringBuilder stringBuilder2 = new StringBuilder();
			for (int j = 0; j < array.Length; j++)
			{
				string text3 = array[j];
				stringBuilder2.Length = 0;
				bool flag2 = true;
				int num3 = 0;
				text2 = text3;
				foreach (char c2 in text2)
				{
					if (char.IsLetter(c2))
					{
						stringBuilder2.Append(char.ToLowerInvariant(c2));
						if (char.IsUpper(c2))
						{
							num3++;
						}
						else
						{
							flag2 = false;
						}
					}
				}
				char c3 = PunctOf(text3);
				if (stringBuilder2.Length == 0)
				{
					if (c3 != 0 && list.Count > 0)
					{
						Unit value = list[list.Count - 1];
						if (value.Punct == '\0' || c3 == '…')
						{
							value.Punct = c3;
						}
						list[list.Count - 1] = value;
						if (IsEnd(c3))
						{
							EndSentence(list, first, longest);
							num++;
							first = list.Count;
							longest = -1;
							num2 = 5;
						}
					}
					continue;
				}
				string text4 = stringBuilder2.ToString();
				bool flag3 = flag2 && num3 >= 2;
				int count = list.Count;
				AddSyllables(list, text4, num);
				for (int k = count; k < list.Count; k++)
				{
					Unit value2 = list[k];
					value2.Light = text4.Length <= 2;
					value2.Emphasis = flag3 || c3 == '!';
					if (k == list.Count - 1)
					{
						value2.WordEnd = true;
						value2.Punct = c3;
					}
					list[k] = value2;
				}
				if (text4.Length > num2)
				{
					num2 = text4.Length;
					longest = count;
				}
				if (IsEnd(c3) || j == array.Length - 1)
				{
					EndSentence(list, first, longest);
					num++;
					first = list.Count;
					longest = -1;
					num2 = 5;
				}
			}
			return list;
		}

		private static void EndSentence(List<Unit> units, int first, int longest)
		{
			if (units.Count != 0 && first < units.Count)
			{
				Unit value = units[units.Count - 1];
				value.SentenceEnd = true;
				units[units.Count - 1] = value;
				if (longest >= first && longest < units.Count)
				{
					Unit value2 = units[longest];
					value2.Emphasis = true;
					units[longest] = value2;
				}
			}
		}

		private static bool IsEnd(char p)
		{
			if (p != '.' && p != '!' && p != '?')
			{
				return p == '…';
			}
			return true;
		}

		private static char PunctOf(string tok)
		{
			if (tok.EndsWith("...") || tok.EndsWith("…"))
			{
				return '…';
			}
			for (int num = tok.Length - 1; num >= 0; num--)
			{
				char c = tok[num];
				switch (c)
				{
				case '!':
				case '?':
					return c;
				case '.':
					return '.';
				case ',':
				case '-':
				case ':':
				case ';':
				case '—':
					return ',';
				}
				if (char.IsLetterOrDigit(c))
				{
					break;
				}
			}
			return '\0';
		}

		private static bool IsVowel(string w, int i)
		{
			switch (w[i])
			{
			case 'y':
				return i > 0;
			default:
				return false;
			case 'a':
			case 'e':
			case 'i':
			case 'o':
			case 'u':
				return true;
			}
		}

		private static void AddSyllables(List<Unit> units, string word, int sentence)
		{
			List<int> list = new List<int>();
			for (int i = 0; i < word.Length; i++)
			{
				if (IsVowel(word, i) && (i == 0 || !IsVowel(word, i - 1)))
				{
					list.Add(i);
				}
			}
			if (list.Count > 3)
			{
				list = new List<int>
				{
					list[0],
					list[list.Count / 2],
					list[list.Count - 1]
				};
			}
			uint num = Fnv(word);
			if (list.Count == 0)
			{
				units.Add(new Unit
				{
					Vowel = 'u',
					Sentence = sentence,
					Key = num
				});
				return;
			}
			for (int j = 0; j < list.Count; j++)
			{
				units.Add(new Unit
				{
					Vowel = word[list[j]],
					Sentence = sentence,
					Key = Mix(num, (uint)(j + 1))
				});
			}
		}

		private static List<int> Choose(List<Unit> units, int n)
		{
			int count = units.Count;
			List<int> list = new List<int>(n);
			if (n >= count)
			{
				for (int i = 0; i < count; i++)
				{
					list.Add(i);
				}
				return list;
			}
			for (int j = 0; j < n; j++)
			{
				list.Add(Math.Min(count - 1, (int)(((float)j + 0.5f) * (float)count / (float)n)));
			}
			list[n - 1] = count - 1;
			for (int k = 0; k < count - 1; k++)
			{
				if (!units[k].SentenceEnd || list.Contains(k))
				{
					continue;
				}
				int num = -1;
				for (int l = 0; l < list.Count; l++)
				{
					int num2 = list[l];
					if (units[num2].Sentence == units[k].Sentence && !units[num2].SentenceEnd && (num < 0 || Math.Abs(num2 - k) < Math.Abs(list[num] - k)))
					{
						num = l;
					}
				}
				if (num >= 0)
				{
					list[num] = k;
				}
			}
			list.Sort();
			for (int num3 = list.Count - 1; num3 > 0; num3--)
			{
				if (list[num3] == list[num3 - 1])
				{
					list.RemoveAt(num3);
				}
			}
			return list;
		}

		private static List<int> First(int n)
		{
			List<int> list = new List<int>(n);
			for (int i = 0; i < n; i++)
			{
				list.Add(i);
			}
			return list;
		}

		private static float RateOf(Speaker who)
		{
			float num = ((who.Tone == Tone.Happy) ? 5.6f : ((who.Tone == Tone.Angry) ? 6.2f : ((who.Tone == Tone.Sad) ? 3.8f : ((who.Tone == Tone.Dark) ? 4.2f : 5f))));
			num *= ((who.Pace > 0f) ? who.Pace : 1f);
			num *= 0.92f + 0.16f * Hash01(who.Seed, 3);
			if (who.Elder)
			{
				num *= 0.88f;
			}
			if (who.Child)
			{
				num *= 1.08f;
			}
			if (who.Broken)
			{
				num *= 0.6f;
			}
			return num;
		}

		private static List<Note> Plan(List<Unit> units, List<int> picks, Speaker who, Rng rng, bool opening)
		{
			List<Note> list = new List<Note>(picks.Count);
			float num = (who.Flat ? 0.3f : ((who.Tone == Tone.Happy) ? 3f : ((who.Tone == Tone.Angry) ? 2.5f : ((who.Tone == Tone.Sad) ? 0.7f : ((who.Tone == Tone.Dark) ? 1f : 1.5f)))));
			float num2 = ((who.Tone == Tone.Angry) ? 0.7f : ((who.Tone == Tone.Sad) ? 1.6f : ((who.Tone == Tone.Dark) ? 1.3f : ((who.Tone == Tone.Happy) ? 0.9f : 1f))));
			if (who.Broken)
			{
				num2 *= 1.8f;
			}
			if (who.Elder)
			{
				num2 *= 1.2f;
			}
			float num3 = 0f;
			bool flag = true;
			for (int i = 0; i < picks.Count; i++)
			{
				Unit unit = units[picks[i]];
				bool flag2 = i == picks.Count - 1;
				Unit? unit2 = (flag2 ? ((Unit?)null) : new Unit?(units[picks[i + 1]]));
				bool flag3 = unit.SentenceEnd || flag2 || (unit2.HasValue && unit2.Value.Sentence != unit.Sentence);
				char c = unit.Punct;
				if (!flag2)
				{
					for (int j = picks[i] + 1; j < picks[i + 1]; j++)
					{
						if (Stronger(units[j].Punct, c))
						{
							c = units[j].Punct;
						}
					}
				}
				if (flag3 && !IsEnd(c))
				{
					for (int k = picks[i]; k < units.Count; k++)
					{
						if (units[k].Sentence == unit.Sentence && units[k].SentenceEnd)
						{
							c = units[k].Punct;
							break;
						}
					}
					if (!IsEnd(c))
					{
						c = ((opening && !flag2) ? ',' : '.');
					}
				}
				if (opening && flag2 && !unit.SentenceEnd)
				{
					c = ',';
				}
				Note item = new Note
				{
					Unit = picks[i]
				};
				bool flag4 = unit.Emphasis && !unit.Light;
				float num4 = (flag3 ? 0.22f : (flag4 ? 0.18f : (unit.Light ? 0.1f : 0.12f)));
				item.Dur = num4 * (0.85f + 0.3f * rng.Unit()) * (who.Broken ? 1.15f : 1f);
				float num5 = (flag2 ? 0f : (flag3 ? 0.27f : ((c == ',') ? 0.17f : (unit.WordEnd ? 0.06f : 0.03f))));
				if (c == '…' && !flag2)
				{
					num5 = 0.36f;
				}
				item.Gap = (flag2 ? 0f : Math.Max(0.015f, num5 * num2 + (rng.Unit() - 0.5f) * 0.04f));
				int num6 = 0;
				int num7 = 0;
				for (int l = 0; l < picks.Count; l++)
				{
					if (units[picks[l]].Sentence == unit.Sentence)
					{
						if (l < i)
						{
							num6++;
						}
						num7++;
					}
				}
				float num8;
				if (who.Tone == Tone.Happy && !who.Flat)
				{
					if (num6 == 0)
					{
						flag = true;
					}
					num8 = (flag ? 1f : (-0.4f)) * num * (0.6f + 0.4f * rng.Unit());
					flag = !flag;
				}
				else
				{
					num8 = (rng.Unit() * 2f - 1f) * num;
					if (Math.Abs(num8 - num3) < 0.3f * num)
					{
						num8 = 0f - num8;
					}
				}
				num3 = num8;
				float num9 = (who.Flat ? 0f : (-2f * ((num7 > 1) ? ((float)num6 / (float)(num7 - 1)) : 0f)));
				float num10 = num8 + num9;
				if (flag4)
				{
					num10 += ((who.Tone == Tone.Angry) ? 4f : 3f);
				}
				if (who.Broken)
				{
					num10 += (rng.Unit() * 2f - 1f) * 1f;
				}
				float num11 = (rng.Unit() - 0.5f) * 1f;
				if (flag3 || c == ',')
				{
					float num12;
					switch (c)
					{
					default:
						num12 = -2.5f;
						break;
					case '…':
						num12 = -1.5f;
						break;
					case ',':
						num12 = 1f;
						break;
					case '!':
						num12 = -1.5f;
						break;
					case '?':
						num12 = 4.5f;
						break;
					}
					num11 = num12;
				}
				if (who.Flat)
				{
					num11 *= 0.25f;
				}
				if (SentencePunct(units, unit.Sentence) == '!' && (flag3 || (unit2.HasValue && units[picks[i + 1]].SentenceEnd && unit2.Value.Sentence == unit.Sentence)))
				{
					num10 += 2f;
					item.Db += 3f;
				}
				item.St0 = num10;
				item.St1 = num10 + num11;
				item.Db += (flag4 ? 3f : 0f);
				item.Fade = c == '…' && flag3;
				list.Add(item);
			}
			return list;
		}

		private static bool Stronger(char a, char b)
		{
			return Strength(a) > Strength(b);
		}

		private static int Strength(char p)
		{
			switch (p)
			{
			default:
				return (p == ',') ? 1 : 0;
			case '.':
				return 2;
			case '…':
				return 3;
			case '!':
				return 3;
			case '?':
				return 4;
			}
		}

		private static char SentencePunct(List<Unit> units, int sentence)
		{
			for (int i = 0; i < units.Count; i++)
			{
				if (units[i].Sentence == sentence && units[i].SentenceEnd)
				{
					return units[i].Punct;
				}
			}
			return '\0';
		}

		private static void Fit(List<Note> notes, List<Unit> units, float seconds, Speaker who)
		{
			for (int i = 0; i < 64; i++)
			{
				if (notes.Count <= 0)
				{
					break;
				}
				float num = 0f;
				float num2 = 0f;
				foreach (Note note2 in notes)
				{
					num += note2.Dur;
					num2 += note2.Gap;
				}
				float num3 = num + num2;
				if (num3 > seconds + 0.2f)
				{
					float num4 = seconds + 0.1f;
					float num5 = num2 * 0.4f;
					float num6 = num * 0.7f;
					float num7 = num3 - num4;
					float num8 = Math.Min(num7, num2 - num5);
					float num9 = ((num2 > 0f) ? ((num2 - num8) / num2) : 1f);
					float num10 = Math.Min(num7 - num8, num - num6);
					float num11 = ((num > 0f) ? ((num - num10) / num) : 1f);
					for (int j = 0; j < notes.Count; j++)
					{
						Note value = notes[j];
						value.Gap *= num9;
						value.Dur *= num11;
						notes[j] = value;
					}
					if (num8 + num10 >= num7 - 0.001f || notes.Count == 1)
					{
						break;
					}
					int num12 = -1;
					for (int k = 0; k < notes.Count - 1; k++)
					{
						if (num12 >= 0)
						{
							break;
						}
						if (units[notes[k].Unit].Light)
						{
							num12 = k;
						}
					}
					for (int l = 0; l < notes.Count - 1; l++)
					{
						if (num12 >= 0)
						{
							break;
						}
						if (!units[notes[l].Unit].WordEnd && !units[notes[l].Unit].Emphasis)
						{
							num12 = l;
						}
					}
					if (num12 < 0)
					{
						num12 = notes.Count / 2 - ((notes.Count % 2 == 0) ? 1 : 0);
					}
					if (num12 < 0 || num12 >= notes.Count - 1)
					{
						break;
					}
					Note note = notes[num12];
					if (num12 > 0)
					{
						Note value2 = notes[num12 - 1];
						value2.Gap += note.Gap;
						notes[num12 - 1] = value2;
					}
					notes.RemoveAt(num12);
					continue;
				}
				if (num3 < seconds * 0.9f && notes.Count > 0)
				{
					float num13 = seconds * 0.95f - num3;
					float val = num2 * 1.5f;
					float num14 = Math.Min(num13, val);
					float num15 = ((num2 > 0f) ? ((num2 + num14) / num2) : 1f);
					float num16 = Math.Min(1.25f, (num > 0f) ? ((num + (num13 - num14)) / num) : 1f);
					for (int m = 0; m < notes.Count; m++)
					{
						Note value3 = notes[m];
						value3.Gap *= num15;
						value3.Dur *= num16;
						notes[m] = value3;
					}
				}
				break;
			}
		}

		private static Phrase Render(VoiceSet set, Speaker who, List<Unit> units, List<Note> notes, Rng rng)
		{
			if (notes.Count == 0)
			{
				return null;
			}
			float num = (Hash01(who.Seed, 1) - 0.5f) * 4f + KindSt(set.Kind);
			if (who.Female)
			{
				num += 5.5f;
			}
			if (who.Child)
			{
				num += 7f;
			}
			if (who.Young && !who.Child)
			{
				num += 1f;
			}
			if (who.Elder)
			{
				num -= (who.Female ? 2.5f : 1f);
			}
			num += ((who.Tone == Tone.Happy) ? 2.5f : ((who.Tone == Tone.Sad) ? (-2.5f) : ((who.Tone == Tone.Angry) ? (-1f) : ((who.Tone == Tone.Dark) ? (-2f) : 0f))));
			float num2 = (who.Child ? 1.16f : (who.Female ? 1.09f : 1f)) * ((who.Young && !who.Child) ? 1.02f : 1f) * (1f + (Hash01(who.Seed, 2) - 0.5f) * 0.04f);
			float num3 = ((who.Tone == Tone.Angry) ? 2f : ((who.Tone == Tone.Sad) ? (-3f) : ((who.Tone == Tone.Dark) ? (-2f) : ((who.Tone == Tone.Happy) ? 1f : 0f))));
			if (who.Broken)
			{
				num3 -= 1f;
			}
			float num4 = 0f;
			foreach (Note note in notes)
			{
				num4 += note.Dur + note.Gap;
			}
			int num5 = 1200;
			float[] array = new float[(int)((num4 + 0.05f) * num2 * 48000f) + 2 * num5];
			float num6 = 0f;
			int num7 = -1;
			int prev = -1;
			float num8 = 0f;
			int num9 = 0;
			float[] array2 = new float[0];
			foreach (Note note2 in notes)
			{
				Unit u = units[note2.Unit];
				int num10 = Pick(set, u, who, rng, num7, prev);
				prev = num7;
				num7 = num10;
				VoiceSet.Syllable syllable = set.Syllables[num10];
				VoiceSet.Clip c = set.Clips[syllable.Clip];
				int num11 = Math.Max(960, (int)(note2.Dur * num2 * 48000f));
				int start = syllable.Start;
				int s = Math.Min(syllable.End, syllable.Start + (int)((float)num11 * 1.5f));
				float num12 = Align(set.MedianHz, syllable.Hz);
				float r = Clamp((float)Math.Pow(2.0, (num + note2.St0) / 12f) * num12 / num2, 0.45f, 2.2f);
				float r2 = Clamp((float)Math.Pow(2.0, (num + note2.St1) / 12f) * num12 / num2, 0.45f, 2.2f);
				float num13 = Clamp(set.MedianLevel - syllable.Level, -6f, 6f);
				float gain = (float)Math.Pow(10.0, (num13 + note2.Db + num3) / 20f);
				if (array2.Length < num11 + 2 * num5)
				{
					array2 = new float[num11 + 2 * num5];
				}
				else
				{
					Array.Clear(array2, 0, num11 + 2 * num5);
				}
				Psola(c, start, s, array2, num5, num11, r, r2, who.Elder, gain, rng);
				int num14 = 288;
				int num15 = (int)(num6 * num2 * 48000f);
				for (int i = 0; i < num11 + 2 * num5; i++)
				{
					int num16 = i - num5;
					float num17 = ((num16 < 0 || num16 >= num11) ? 0f : Math.Min(1f, Math.Min((float)(num16 + 1) / (float)num14, (float)(num11 - num16) / (float)num14)));
					if (note2.Fade && num16 >= 0)
					{
						num17 *= Math.Min(1f, (float)(num11 - num16) / (0.6f * (float)num11));
					}
					if (who.Elder)
					{
						num17 *= 1f - 0.12f * (0.5f + 0.5f * (float)Math.Sin(Math.PI * 19.0 * (double)(num15 + num16) / 48000.0));
					}
					float num18 = array2[i] * num17;
					int num19 = num15 + i;
					if (num19 >= 0 && num19 < array.Length)
					{
						array[num19] += num18;
						if (num16 >= 0 && num16 < num11)
						{
							num8 += num18 * num18;
							num9++;
						}
					}
				}
				num6 += note2.Dur + note2.Gap;
			}
			float[] array3 = ((num2 == 1f) ? array : Resample(array, num2));
			if (who.Broken)
			{
				for (int j = 0; j < array3.Length; j++)
				{
					array3[j] = (float)Math.Tanh(array3[j] * 2.2f) / 2.2f * 1.4f;
				}
			}
			float num20 = ((num9 > 0) ? ((float)Math.Sqrt(num8 / (float)num9)) : 0f);
			float num21 = 0f;
			for (int k = 0; k < array3.Length; k++)
			{
				num21 = Math.Max(num21, Math.Abs(array3[k]));
			}
			if (num20 > 1E-06f)
			{
				float num22 = 0.1f / num20;
				if (num21 * num22 > 0.89f)
				{
					num22 = 0.89f / num21;
				}
				for (int l = 0; l < array3.Length; l++)
				{
					array3[l] *= num22;
				}
			}
			int m = 0;
			int num23;
			for (num23 = array3.Length - 1; m < num23 && Math.Abs(array3[m]) < 0.0001f; m++)
			{
			}
			while (num23 > m && Math.Abs(array3[num23]) < 0.0001f)
			{
				num23--;
			}
			m = Math.Max(0, m - 240);
			num23 = Math.Min(array3.Length - 1, num23 + 240);
			float[] array4 = new float[num23 - m + 1];
			Array.Copy(array3, m, array4, 0, array4.Length);
			return new Phrase
			{
				S = array4,
				Syllables = notes.Count
			};
		}

		private static float Align(float kindHz, float ownHz)
		{
			if (kindHz <= 0f || ownHz <= 0f)
			{
				return 1f;
			}
			while (ownHz > kindHz * 1.414f)
			{
				ownHz *= 0.5f;
			}
			while (ownHz < kindHz * 0.707f)
			{
				ownHz *= 2f;
			}
			float v = 8.4f * (float)(Math.Log(kindHz / ownHz) / Math.Log(2.0));
			return (float)Math.Pow(2.0, Clamp(v, -3f, 3f) / 12f);
		}

		private static float KindSt(string kind)
		{
			switch (kind)
			{
			default:
				return 0f;
			case "elf":
				return 1f;
			case "orc":
				return -3f;
			case "dwarf":
				return -2f;
			}
		}

		private static int Pick(VoiceSet set, Unit u, Speaker who, Rng rng, int prev1, int prev2)
		{
			int num = ((u.Vowel == 'a') ? 1 : ((u.Vowel == 'e' || u.Vowel == 'i' || u.Vowel == 'y') ? 2 : 0));
			if (who.Broken && rng.Unit() < 0.5f)
			{
				num = 0;
			}
			List<int> list = set.ByClass[num];
			if (!who.Broken && set.VoicedByClass != null && set.VoicedByClass[num].Count >= 2)
			{
				list = set.VoicedByClass[num];
			}
			int num2 = (int)(Mix(u.Key, (uint)who.Seed) % (uint)list.Count);
			for (int i = 0; i < list.Count; i++)
			{
				if (list[num2] != prev1 && list[num2] != prev2)
				{
					break;
				}
				num2 = (num2 + 1) % list.Count;
			}
			return list[num2];
		}

		private static void Psola(VoiceSet.Clip c, int s0, int s1, float[] dst, int d0, int dLen, float r0, float r1, bool waver, float gain, Rng rng)
		{
			int[] marks = c.Marks;
			float[] period = c.Period;
			if (marks == null || marks.Length == 0)
			{
				return;
			}
			int num = Lower(marks, s0);
			int num2 = Lower(marks, s1) - 1;
			if (num >= marks.Length)
			{
				return;
			}
			if (num2 < num)
			{
				num2 = num;
			}
			float num3 = s1 - s0;
			int i = num;
			float num15;
			for (double num4 = 0.0; num4 < (double)dLen; num4 += (double)Math.Max(16f, num15))
			{
				float num5 = (float)(num4 / (double)dLen);
				float num6;
				for (num6 = (float)s0 + num5 * num3; i < num2 && (float)marks[i + 1] <= num6; i++)
				{
				}
				int num7 = ((i < num2 && (float)marks[i + 1] - num6 < num6 - (float)marks[i]) ? (i + 1) : i);
				float num8 = ((i < num2 && marks[i + 1] > marks[i]) ? Clamp((num6 - (float)marks[i]) / (float)(marks[i + 1] - marks[i]), 0f, 1f) : 0f);
				int num9 = marks[num7];
				bool num10 = period[num7] > 0f;
				float num11 = ((num10 && c.Smooth != null && c.Smooth[num7] > 0f) ? c.Smooth[num7] : period[num7]);
				float num12 = ((num10 && c.Even != null && c.Peak[num7] > 0.0001f) ? Clamp(c.Even[num7] / c.Peak[num7], 0.5f, 2f) : 1f);
				float num13 = (num10 ? num11 : 240f);
				float num14 = r0 * (float)Math.Pow(r1 / r0, Smooth(num5));
				if (waver)
				{
					num14 *= (float)Math.Pow(2.0, 0.35 * Math.Sin(Math.PI * 11.0 * num4 / 48000.0) / 12.0);
				}
				num15 = (num10 ? (num11 / num14) : 240f);
				float num16 = gain * Math.Min(1.5f, num15 / num13) * num12;
				int num17 = Math.Max(8, (int)num13);
				int num18 = d0 + (int)num4;
				if (num10)
				{
					int pulseBlend = PulseBlend;
					float num19 = (float)i + num8;
					float num20 = 0f;
					for (int j = i - pulseBlend; j <= i + pulseBlend + 1; j++)
					{
						if (j >= 0 && j < marks.Length && !(period[j] <= 0f))
						{
							float num21 = (float)(pulseBlend + 1) - Math.Abs((float)j - num19);
							if (num21 > 0f)
							{
								num20 += num21;
							}
						}
					}
					if (num20 <= 0f)
					{
						num20 = 1f;
					}
					for (int k = i - pulseBlend; k <= i + pulseBlend + 1; k++)
					{
						int num22 = k;
						if (num22 < 0 || num22 >= marks.Length || period[num22] <= 0f)
						{
							continue;
						}
						float num23 = (float)(pulseBlend + 1) - Math.Abs((float)k - num19);
						if (num23 <= 0f)
						{
							continue;
						}
						float num24 = num23 / num20 * num16;
						int num25 = marks[num22];
						for (int l = -num17; l <= num17; l++)
						{
							int num26 = num25 + l;
							int num27 = num18 + l;
							if (num26 >= 0 && num26 < c.S.Length && num27 >= 0 && num27 < dst.Length)
							{
								dst[num27] += c.S[num26] * (0.5f + 0.5f * (float)Math.Cos(Math.PI * (double)l / (double)num17)) * num24;
							}
						}
					}
					continue;
				}
				float num28 = (float)Math.Pow(num14, 0.4);
				float num29 = (float)num9 + (rng.Unit() - 0.5f) * 240f;
				for (int m = -num17; m <= num17; m++)
				{
					int num30 = num18 + m;
					if (num30 >= 0 && num30 < dst.Length)
					{
						float num31 = num29 + (float)m * num28;
						int num32 = (int)Math.Floor(num31);
						if (num32 >= 0 && num32 + 1 < c.S.Length)
						{
							float num33 = num31 - (float)num32;
							dst[num30] += (c.S[num32] + (c.S[num32 + 1] - c.S[num32]) * num33) * (0.5f + 0.5f * (float)Math.Cos(Math.PI * (double)m / (double)num17)) * num16;
						}
					}
				}
			}
		}

		private static int Lower(int[] a, int v)
		{
			int num = 0;
			int num2 = a.Length;
			while (num < num2)
			{
				int num3 = num + num2 >> 1;
				if (a[num3] < v)
				{
					num = num3 + 1;
				}
				else
				{
					num2 = num3;
				}
			}
			return num;
		}

		private static float[] Resample(float[] x, float factor)
		{
			int num = (int)((float)x.Length / factor);
			float[] array = new float[num];
			for (int i = 0; i < num; i++)
			{
				double num2 = (double)i * (double)factor;
				int num3 = (int)num2;
				float num4 = (float)(num2 - (double)num3);
				float num5 = ((num3 > 0) ? x[num3 - 1] : 0f);
				float num6 = x[num3];
				float num7 = ((num3 + 1 < x.Length) ? x[num3 + 1] : 0f);
				float num8 = ((num3 + 2 < x.Length) ? x[num3 + 2] : 0f);
				array[i] = num6 + 0.5f * num4 * (num7 - num5 + num4 * (2f * num5 - 5f * num6 + 4f * num7 - num8 + num4 * (3f * (num6 - num7) + num8 - num5)));
			}
			return array;
		}

		private static float Smooth(float x)
		{
			x = Clamp(x, 0f, 1f);
			return x * x * (3f - 2f * x);
		}

		private static float Clamp(float v, float lo, float hi)
		{
			if (!(v < lo))
			{
				if (!(v > hi))
				{
					return v;
				}
				return hi;
			}
			return lo;
		}

		private static int Clamp(int v, int lo, int hi)
		{
			if (v >= lo)
			{
				if (v <= hi)
				{
					return v;
				}
				return hi;
			}
			return lo;
		}

		private static uint Fnv(string s)
		{
			uint num = 2166136261u;
			foreach (char c in s)
			{
				num ^= c;
				num *= 16777619;
			}
			return num;
		}

		private static uint Mix(uint a, uint b)
		{
			int num = (int)a ^ ((int)b * -1640531527);
			uint num2 = ((uint)num ^ ((uint)num >> 16)) * 2146121005;
			int num3 = (int)(num2 ^ (num2 >> 15)) * -2073254261;
			return (uint)num3 ^ ((uint)num3 >> 16);
		}

		private static uint Hash(int seed, string text)
		{
			return Mix(Fnv(text), (uint)seed);
		}

		private static float Hash01(int seed, int what)
		{
			return (float)(Mix((uint)seed, (uint)(what * 7919)) & 0xFFFFFF) / 16777216f;
		}
	}
}
