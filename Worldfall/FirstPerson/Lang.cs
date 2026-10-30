using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FirstPerson
{
	internal static class Lang
	{
		private sealed class Pattern
		{
			public string[] Parts;

			public string[] Holes;

			public string Out;

			public string English;

			public int Weight;

			public Regex Match;

			public int Lines;
		}

		private static readonly Dictionary<string, string> Exact = new Dictionary<string, string>(StringComparer.Ordinal);

		private static readonly Dictionary<string, string> Formats = new Dictionary<string, string>(StringComparer.Ordinal);

		private static readonly List<Pattern> Templates = new List<Pattern>();

		private static readonly Dictionary<char, List<Pattern>> ByFirst = new Dictionary<char, List<Pattern>>();

		private static readonly Dictionary<char, List<Pattern>> ByLast = new Dictionary<char, List<Pattern>>();

		private static readonly List<Pattern> Open = new List<Pattern>();

		private static readonly Dictionary<string, string> Cache = new Dictionary<string, string>(StringComparer.Ordinal);

		private static readonly HashSet<string> Outputs = new HashSet<string>(StringComparer.Ordinal);

		private static bool _active;

		private static bool _chosen;

		private static string _setting;

		private static string _game;

		private static string _overridePath;

		private static DateTime _overrideTime;

		private static float _nextFileCheck;

		private static float _nextFlush;

		private static readonly Regex CjkGap = new Regex("([\u3040-ヿ㐀-鿿\uff00-\uffef]) ([\u3040-ヿ㐀-鿿\uff00-\uffef])");

		private static readonly Regex NumberHead = new Regex("^([+-]?\\d[\\d.,]*%?)\\s+(\\S.*)$", RegexOptions.Singleline);

		private static readonly Regex HoleRx = new Regex("\\{(\\d+|[a-z_]+)\\}");

		private static readonly Regex Separators = new Regex("(\\s{2,}| · | \\| | - | — | \\(|\\)|: |, |\\. )");

		private const string ResourcePrefix = "FirstPerson.lang.";

		private static readonly Regex WordRx = new Regex("[A-Za-z]+");

		private static readonly HashSet<string> Plain = new HashSet<string>(StringComparer.Ordinal)
		{
			"a", "an", "the", "of", "in", "on", "at", "to", "for", "and",
			"or", "with", "by", "from", "is", "as", "its", "s"
		};

		private static bool _exported;

		private static readonly Dictionary<string, string> GameTexts = new Dictionary<string, string>(StringComparer.Ordinal);

		private static readonly Regex ParticleRx = new Regex("([^>])((?:<[^>]+>)*)(이\\(가\\)|가\\(이\\)|을\\(를\\)|를\\(을\\)|은\\(는\\)|는\\(은\\)|과\\(와\\)|와\\(과\\)|아\\(야\\)|\\(으\\)로|\\(이\\)에요|\\(이\\)(?=[가-힣]))");

		private static readonly HashSet<string> _missingSeen = new HashSet<string>(StringComparer.Ordinal);

		private static readonly List<string> _missingNew = new List<string>();

		private static bool _missingLoaded;

		private static Font _face;

		private static Font _foreign;

		private static bool _needFace;

		private static bool _faceBold;

		private static bool _foreignBold;

		private static bool _foreignTried;

		private static string _foreignFor;

		private static Font _system;

		private static bool _systemTried;

		private static readonly Dictionary<string, KeyValuePair<Font, bool>> FacesFor = new Dictionary<string, KeyValuePair<Font, bool>>(StringComparer.Ordinal);

		private static readonly GUIContent Content = new GUIContent();

		public static string Code { get; private set; } = "en";

		public static string Name { get; private set; } = "English";

		private static string Folder => Path.Combine(Log.Folder, "lang");

		private static bool WideStops
		{
			get
			{
				if (!(Code == "cz") && !(Code == "ch"))
				{
					return Code == "ja";
				}
				return true;
			}
		}

		private static string MissingPath => Path.Combine(Folder, "missing_" + Code + ".txt");

		public static int MissingCount => _missingSeen.Count;

		public static void Reload()
		{
			_chosen = false;
		}

		public static void Tick(string setting)
		{
			string text = GameCode();
			if (string.IsNullOrEmpty(setting))
			{
				setting = "auto";
			}
			if (!_chosen || !string.Equals(setting, _setting, StringComparison.Ordinal) || !string.Equals(text, _game, StringComparison.Ordinal))
			{
				_chosen = true;
				_setting = setting;
				_game = text;
				string text2 = ((setting == "auto") ? Nearest(text) : setting);
				Load(string.IsNullOrEmpty(text2) ? "en" : text2);
			}
			float unscaledTime = Time.unscaledTime;
			if (unscaledTime >= _nextFileCheck)
			{
				_nextFileCheck = unscaledTime + 2f;
				if (_overridePath != null && FileTime(_overridePath) != _overrideTime)
				{
					Log.Info("lang: " + _overridePath + " changed, reading it again");
					Load(Code);
				}
			}
			if (unscaledTime >= _nextFlush)
			{
				_nextFlush = unscaledTime + 5f;
				FlushMissing();
			}
		}

		private static string Nearest(string game)
		{
			if (game != "pt")
			{
				return game;
			}
			List<KeyValuePair<string, string>> list = Available();
			if (!list.Exists((KeyValuePair<string, string> l) => l.Key == "pt") && list.Exists((KeyValuePair<string, string> l) => l.Key == "br"))
			{
				return "br";
			}
			return game;
		}

		public static string GameCode()
		{
			try
			{
				return LocalizedTextManager.current_language?.id ?? "en";
			}
			catch
			{
				return "en";
			}
		}

		public static List<KeyValuePair<string, string>> Available()
		{
			SortedSet<string> sortedSet = new SortedSet<string>(StringComparer.Ordinal);
			string[] manifestResourceNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();
			foreach (string text in manifestResourceNames)
			{
				if (text.StartsWith("FirstPerson.lang.", StringComparison.Ordinal) && text.EndsWith(".txt", StringComparison.Ordinal))
				{
					sortedSet.Add(text.Substring("FirstPerson.lang.".Length, text.Length - "FirstPerson.lang.".Length - 4));
				}
			}
			try
			{
				manifestResourceNames = Directory.GetFiles(Folder, "*.txt");
				foreach (string path in manifestResourceNames)
				{
					sortedSet.Add(Path.GetFileNameWithoutExtension(path));
				}
				foreach (string item in Packs.LangCodes())
				{
					sortedSet.Add(item);
				}
			}
			catch
			{
			}
			List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>
			{
				new KeyValuePair<string, string>("en", "English")
			};
			foreach (string item2 in sortedSet)
			{
				if (!item2.StartsWith("_") && !item2.StartsWith("missing_") && !(item2 == "en") && !item2.Equals("README", StringComparison.OrdinalIgnoreCase))
				{
					list.Add(new KeyValuePair<string, string>(item2, NameOf(item2)));
				}
			}
			return list;
		}

		private static string NameOf(string code)
		{
			string text = Shipped(code) ?? ReadOrNull(Path.Combine(Folder, code + ".txt"));
			if (text != null)
			{
				Match match = Regex.Match(text, "^@name\\s*=\\s*(.+?)\\s*$", RegexOptions.Multiline);
				if (match.Success)
				{
					return match.Groups[1].Value;
				}
			}
			try
			{
				if (AssetManager.game_language_library != null && AssetManager.game_language_library.dict.TryGetValue(code, out var value) && !string.IsNullOrEmpty(value.name))
				{
					return value.name;
				}
			}
			catch
			{
			}
			return code;
		}

		private static string Shipped(string code)
		{
			try
			{
				using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("FirstPerson.lang." + code + ".txt"))
				{
					if (stream == null)
					{
						return null;
					}
					using (StreamReader streamReader = new StreamReader(stream, Encoding.UTF8))
					{
						return streamReader.ReadToEnd();
					}
				}
			}
			catch
			{
				return null;
			}
		}

		private static string ReadOrNull(string path)
		{
			try
			{
				return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
			}
			catch
			{
				return null;
			}
		}

		private static DateTime FileTime(string path)
		{
			try
			{
				return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
			}
			catch
			{
				return DateTime.MinValue;
			}
		}

		private static void Load(string code)
		{
			FlushMissing();
			Exact.Clear();
			Formats.Clear();
			Templates.Clear();
			Cache.Clear();
			Outputs.Clear();
			_missingSeen.Clear();
			_missingLoaded = false;
			Code = code;
			Name = ((code == "en") ? "English" : NameOf(code));
			_overridePath = null;
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			ExportShipped();
			if (code != "en")
			{
				num = Read(Shipped(code));
				foreach (string item in Packs.LangTexts(code))
				{
					num3 += Read(item);
				}
				_overridePath = Path.Combine(Folder, code + ".txt");
				_overrideTime = FileTime(_overridePath);
				num2 = Read(ReadOrNull(_overridePath));
				Templates.Sort((Pattern a, Pattern b) => b.Weight.CompareTo(a.Weight));
			}
			ByFirst.Clear();
			ByLast.Clear();
			Open.Clear();
			foreach (Pattern template in Templates)
			{
				string text = template.Parts[0];
				string text2 = template.Parts[template.Parts.Length - 1];
				if (text.Length > 0)
				{
					Bucket(ByFirst, text[0]).Add(template);
				}
				else if (text2.Length > 0)
				{
					Bucket(ByLast, text2[text2.Length - 1]).Add(template);
				}
				else
				{
					Open.Add(template);
				}
			}
			_active = Exact.Count + Templates.Count > 0;
			ChooseFace();
			ReAddGameTexts();
			Log.Info("lang: " + code + " (" + Name + "), " + num + " lines from the mod" + ((num3 > 0) ? (", " + num3 + " from packs") : "") + ((num2 > 0) ? (", " + num2 + " from " + _overridePath) : "") + ", " + Templates.Count + " templates; font " + ((_needFace && _face != null) ? _face.name : "Roboto"));
		}

		private static int Read(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return 0;
			}
			int num = 0;
			string[] array = text.Split('\n');
			for (int i = 0; i < array.Length; i++)
			{
				if (Split(array[i].TrimEnd('\r', ' ', '\t').TrimStart('\ufeff'), out var english, out var translation) && translation.Length != 0)
				{
					num++;
					Outputs.Add(translation);
					if (HoleRx.IsMatch(english))
					{
						Formats[english] = translation;
						AddTemplate(english, translation);
					}
					else
					{
						Exact[english] = translation;
					}
				}
			}
			return num;
		}

		private static void AddTemplate(string en, string tr)
		{
			MatchCollection matchCollection = HoleRx.Matches(en);
			string[] parts = new string[matchCollection.Count + 1];
			string[] array = new string[matchCollection.Count];
			int num = 0;
			for (int i = 0; i < matchCollection.Count; i++)
			{
				parts[i] = en.Substring(num, matchCollection[i].Index - num);
				array[i] = matchCollection[i].Groups[1].Value;
				num = matchCollection[i].Index + matchCollection[i].Length;
			}
			parts[matchCollection.Count] = en.Substring(num);
			int num2 = 0;
			bool flag = false;
			string[] array2 = parts;
			foreach (string input in array2)
			{
				foreach (Match item in WordRx.Matches(input))
				{
					num2 += item.Length;
					if (!Plain.Contains(item.Value.ToLowerInvariant()))
					{
						flag = true;
					}
				}
			}
			if (!flag || num2 < 2)
			{
				return;
			}
			Templates.RemoveAll((Pattern t) => SameParts(t.Parts, parts));
			int num3 = 0;
			for (int j = 0; j < en.Length; j++)
			{
				if (en[j] == '\n')
				{
					num3++;
				}
			}
			Templates.Add(new Pattern
			{
				Parts = parts,
				Holes = array,
				Out = tr,
				English = en,
				Lines = num3,
				Weight = num2 * 4 + en.Length - matchCollection.Count * 3
			});
		}

		private static List<Pattern> Bucket(Dictionary<char, List<Pattern>> d, char c)
		{
			if (!d.TryGetValue(c, out var value))
			{
				value = (d[c] = new List<Pattern>());
			}
			return value;
		}

		private static bool SameParts(string[] a, string[] b)
		{
			if (a.Length != b.Length)
			{
				return false;
			}
			for (int i = 0; i < a.Length; i++)
			{
				if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
				{
					return false;
				}
			}
			return true;
		}

		private static bool Split(string line, out string english, out string translation)
		{
			english = (translation = null);
			if (line.Length == 0 || line[0] == '#' || line[0] == '@')
			{
				return false;
			}
			int num = line.IndexOf(" = ", StringComparison.Ordinal);
			if (num < 0)
			{
				if (!line.EndsWith(" =", StringComparison.Ordinal))
				{
					return false;
				}
				english = Unescape(line.Substring(0, line.Length - 2).Trim());
				translation = "";
			}
			else
			{
				english = Unescape(line.Substring(0, num).Trim());
				translation = Unescape(line.Substring(num + 3).Trim());
			}
			return english.Length > 0;
		}

		private static string Unescape(string s)
		{
			if (s.IndexOf('\\') < 0)
			{
				return s;
			}
			StringBuilder stringBuilder = new StringBuilder(s.Length);
			for (int i = 0; i < s.Length; i++)
			{
				char c = s[i];
				if (c != '\\' || i + 1 >= s.Length)
				{
					stringBuilder.Append(c);
					continue;
				}
				char c2 = s[++i];
				int value;
				switch (c2)
				{
				default:
					value = c2;
					break;
				case 't':
					value = 9;
					break;
				case 'n':
					value = 10;
					break;
				}
				stringBuilder.Append((char)value);
			}
			return stringBuilder.ToString();
		}

		private static void ExportShipped()
		{
			if (_exported)
			{
				return;
			}
			_exported = true;
			try
			{
				string text = Path.Combine(Folder, "shipped");
				Directory.CreateDirectory(text);
				string[] manifestResourceNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();
				foreach (string text2 in manifestResourceNames)
				{
					if (text2.StartsWith("FirstPerson.lang.", StringComparison.Ordinal) && text2.EndsWith(".txt", StringComparison.Ordinal))
					{
						string text3 = text2.Substring("FirstPerson.lang.".Length);
						string text4 = Shipped(text3.Substring(0, text3.Length - 4));
						string path = Path.Combine(text, text3);
						if (text4 != null && ReadOrNull(path) != text4)
						{
							File.WriteAllText(path, text4, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Info("lang: could not write the shipped files (" + ex.Message + ")");
			}
		}

		public static void AddGameText(string key, string english)
		{
			GameTexts[key] = english;
			try
			{
				LocalizedTextManager.add(key, T(english), pReplace: true, null, pCheckForCharacters: false);
			}
			catch
			{
			}
		}

		private static void ReAddGameTexts()
		{
			foreach (KeyValuePair<string, string> gameText in GameTexts)
			{
				try
				{
					LocalizedTextManager.add(gameText.Key, T(gameText.Value), pReplace: true, null, pCheckForCharacters: false);
				}
				catch
				{
				}
			}
		}

		public static string T(string text)
		{
			if (!_active || string.IsNullOrEmpty(text))
			{
				return text;
			}
			if (Cache.TryGetValue(text, out var value))
			{
				return value;
			}
			value = Translate(text, 0, split: true);
			if (value == text && !Outputs.Contains(text))
			{
				NoteMissing(text);
			}
			if (Cache.Count > 8000)
			{
				Cache.Clear();
			}
			Cache[text] = value;
			return value;
		}

		public static string Typed(string english, int letters)
		{
			if (string.IsNullOrEmpty(english))
			{
				return "";
			}
			letters = Mathf.Clamp(letters, 0, english.Length);
			if (!_active)
			{
				return english.Substring(0, letters);
			}
			string text = T(english);
			if (letters >= english.Length)
			{
				return text;
			}
			int num = Mathf.Clamp(Mathf.RoundToInt((float)text.Length * (float)letters / (float)english.Length), 0, text.Length);
			if (num > 0 && num < text.Length && char.IsHighSurrogate(text[num - 1]))
			{
				num--;
			}
			return text.Substring(0, num);
		}

		public static string Template(string english)
		{
			if (!_active || !Formats.TryGetValue(english, out var value))
			{
				return english;
			}
			return value;
		}

		public static string Format(string english, params string[] args)
		{
			string input = Template(english);
			string text = HoleRx.Replace(input, delegate(Match m)
			{
				if (!int.TryParse(m.Groups[1].Value, out var result) || result >= args.Length)
				{
					return m.Value;
				}
				string text2 = args[result] ?? "";
				return (!_active || text2.Length <= 0) ? text2 : Translate(text2, 1, split: false);
			});
			if (!_active || !WideStops)
			{
				return Particles(text);
			}
			return CjkGap.Replace(text, "$1$2");
		}

		private static string Translate(string text, int depth, bool split)
		{
			if (Exact.TryGetValue(text, out var value))
			{
				return value;
			}
			if (!HasLetters(text))
			{
				return text;
			}
			int i = 0;
			int num;
			for (num = text.Length; i < num && char.IsWhiteSpace(text[i]); i++)
			{
			}
			while (num > i && char.IsWhiteSpace(text[num - 1]))
			{
				num--;
			}
			if (i > 0 || num < text.Length)
			{
				string text2 = text.Substring(i, num - i);
				string text3 = Translate(text2, depth, split);
				if ((object)text3 != text2)
				{
					return text.Substring(0, i) + text3 + text.Substring(num);
				}
				return text;
			}
			string text4 = OtherCase(text);
			if (text4 != null && Exact.TryGetValue(text4, out value))
			{
				return SameCase(text, value);
			}
			if (split && text.IndexOf('\n') >= 0)
			{
				string text5 = FromTemplate(text, depth);
				if (text5 != null)
				{
					return text5;
				}
				string[] array = text.Split('\n');
				bool flag = false;
				for (int j = 0; j < array.Length; j++)
				{
					string text6 = Translate(array[j], depth, split: true);
					if ((object)text6 != array[j])
					{
						array[j] = text6;
						flag = true;
					}
				}
				if (!flag)
				{
					return text;
				}
				return string.Join("\n", array);
			}
			string text7 = FromTemplate(text, depth) ?? ((text4 != null) ? FromTemplate(text4, depth) : null);
			if (text7 != null)
			{
				return text7;
			}
			if (depth > 2)
			{
				return text;
			}
			Match match = NumberHead.Match(text);
			if (match.Success)
			{
				string value2 = match.Groups[2].Value;
				string text8 = Translate(value2, depth + 1, split: false);
				if ((object)text8 != value2)
				{
					return match.Groups[1].Value + (WideStops ? "" : " ") + text8;
				}
			}
			char c = text[text.Length - 1];
			if (text.Length > 2 && (c == '.' || c == '!' || c == '?') && text[text.Length - 2] != '.')
			{
				string text9 = text.Substring(0, text.Length - 1);
				string text10 = Translate(text9, depth + 1, split: false);
				if ((object)text10 != text9)
				{
					return text10 + Stop(c);
				}
			}
			string[] array2 = Separators.Split(text);
			if (array2.Length > 1)
			{
				bool flag2 = false;
				for (int k = 0; k < array2.Length; k++)
				{
					string text11 = ((k % 2 == 0) ? Translate(array2[k], depth + 1, split: false) : Separator(array2[k]));
					if ((object)text11 != array2[k])
					{
						array2[k] = text11;
						if (k % 2 == 0)
						{
							flag2 = true;
						}
					}
				}
				if (flag2)
				{
					return string.Concat(array2);
				}
			}
			return text;
		}

		private static string OtherCase(string text)
		{
			char c = text[0];
			if (c > '\u007f' || !char.IsLetter(c))
			{
				return null;
			}
			return (char.IsUpper(c) ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c)) + text.Substring(1);
		}

		private static string SameCase(string text, string tr)
		{
			if (tr.Length == 0 || !char.IsLetter(tr[0]))
			{
				return tr;
			}
			return (char.IsUpper(text[0]) ? char.ToUpperInvariant(tr[0]) : char.ToLowerInvariant(tr[0])) + tr.Substring(1);
		}

		private static string Stop(char c)
		{
			if (WideStops)
			{
				switch (c)
				{
				default:
					return "？";
				case '!':
					return "！";
				case '.':
					return "。";
				}
			}
			return c.ToString();
		}

		private static string Separator(string sep)
		{
			if (WideStops)
			{
				switch (sep)
				{
				default:
					return sep;
				case ": ":
					return "：";
				case ". ":
					return "。";
				case ", ":
					return "，";
				}
			}
			return sep;
		}

		private static string FromTemplate(string text, int depth)
		{
			if (depth > 3 || text.Length == 0)
			{
				return null;
			}
			int num = 0;
			for (int i = 0; i < text.Length; i++)
			{
				if (text[i] == '\n')
				{
					num++;
				}
			}
			Pattern best = null;
			Match found = null;
			if (ByFirst.TryGetValue(text[0], out var value))
			{
				Fit(value, text, num, ref best, ref found);
			}
			if (ByLast.TryGetValue(text[text.Length - 1], out value))
			{
				Fit(value, text, num, ref best, ref found);
			}
			Fit(Open, text, num, ref best, ref found);
			if (best == null)
			{
				return null;
			}
			Pattern t = best;
			string[] caps = new string[t.Holes.Length];
			for (int j = 0; j < caps.Length; j++)
			{
				caps[j] = found.Groups[j + 1].Value;
			}
			for (int k = 0; k + 1 < caps.Length; k++)
			{
				string text2 = t.Parts[k + 1];
				if (text2.Length == 0)
				{
					Share(ref caps[k], ref caps[k + 1], depth);
				}
				else if (text2.Trim().Length == 0)
				{
					Split(ref caps[k], ref caps[k + 1], text2, depth);
				}
			}
			return Particles(HoleRx.Replace(t.Out, delegate(Match h)
			{
				string value2 = h.Groups[1].Value;
				for (int l = 0; l < t.Holes.Length; l++)
				{
					if (t.Holes[l] == value2)
					{
						return Translate(caps[l], depth + 1, split: false);
					}
				}
				return h.Value;
			}));
		}

		private static void Share(ref string a, ref string b, int depth)
		{
			string text = a + b;
			for (int i = 1; i < text.Length; i++)
			{
				char c = text[i];
				if (c == ' ' || c == ',' || c == '(' || c == '.' || c == '!' || c == '?' || c == ';' || c == ':')
				{
					string text2 = text.Substring(i);
					if (HasLetters(text2) && (object)Translate(text2, depth + 2, split: false) != text2)
					{
						a = text.Substring(0, i);
						b = text2;
						return;
					}
				}
			}
			a = text;
			b = "";
		}

		private static void Split(ref string a, ref string b, string gap, int depth)
		{
			string text = a + gap + b;
			int num = -1;
			int num2 = Readable(a, depth) + Readable(b, depth);
			if (num2 == 2)
			{
				return;
			}
			for (int num3 = text.IndexOf(gap, a.Length + 1, StringComparison.Ordinal); num3 > 0; num3 = text.IndexOf(gap, num3 + 1, StringComparison.Ordinal))
			{
				int num4 = Readable(text.Substring(0, num3), depth) + Readable(text.Substring(num3 + gap.Length), depth);
				if (num4 > num2)
				{
					num2 = num4;
					num = num3;
				}
				if (num4 == 2)
				{
					break;
				}
			}
			if (num >= 0)
			{
				a = text.Substring(0, num);
				b = text.Substring(num + gap.Length);
			}
		}

		private static int Readable(string s, int depth)
		{
			if (s.Length <= 0 || !HasLetters(s) || (object)Translate(s, depth + 2, split: false) == s)
			{
				return 0;
			}
			return 1;
		}

		private static string Particles(string s)
		{
			if (Code != "ko" || s.IndexOf('(') < 0)
			{
				return s;
			}
			return ParticleRx.Replace(s, delegate(Match m)
			{
				char c = m.Groups[1].Value[0];
				int num = FinalConsonant(c);
				if (num < 0)
				{
					return m.Value;
				}
				bool flag = num > 0;
				string value = m.Groups[3].Value;
				string text;
				if (value != null)
				{
					int length = value.Length;
					if (length != 4)
					{
						if (length == 5 && value == "(이)에요")
						{
							text = (flag ? "이에요" : "예요");
							goto IL_0282;
						}
					}
					else
					{
						char c2 = value[0];
						if ((uint)c2 <= 47484u)
						{
							if ((uint)c2 <= 44032u)
							{
								if (c2 != '(')
								{
									if (c2 == '가' && value == "가(이)")
									{
										goto IL_01ed;
									}
								}
								else if (value == "(으)로")
								{
									text = ((flag && num != 8) ? "으로" : "로");
									goto IL_0282;
								}
							}
							else if (c2 != '과')
							{
								if (c2 != '는')
								{
									if (c2 == '를' && value == "를(을)")
									{
										goto IL_0202;
									}
								}
								else if (value == "는(은)")
								{
									goto IL_0214;
								}
							}
							else if (value == "과(와)")
							{
								goto IL_0226;
							}
						}
						else if ((uint)c2 <= 50752u)
						{
							if (c2 != '아')
							{
								if (c2 == '와' && value == "와(과)")
								{
									goto IL_0226;
								}
							}
							else if (value == "아(야)")
							{
								text = (flag ? "아" : "야");
								goto IL_0282;
							}
						}
						else if (c2 != '은')
						{
							if (c2 != '을')
							{
								if (c2 == '이' && value == "이(가)")
								{
									goto IL_01ed;
								}
							}
							else if (value == "을(를)")
							{
								goto IL_0202;
							}
						}
						else if (value == "은(는)")
						{
							goto IL_0214;
						}
					}
				}
				text = (flag ? "이" : "");
				goto IL_0282;
				IL_0282:
				return c + m.Groups[2].Value + text;
				IL_0226:
				text = (flag ? "과" : "와");
				goto IL_0282;
				IL_0202:
				text = (flag ? "을" : "를");
				goto IL_0282;
				IL_01ed:
				text = (flag ? "이" : "가");
				goto IL_0282;
				IL_0214:
				text = (flag ? "은" : "는");
				goto IL_0282;
			});
		}

		private static int FinalConsonant(char c)
		{
			if (c >= '가' && c <= '힣')
			{
				return (c - 44032) % 28;
			}
			switch (c)
			{
			case '0':
				return 21;
			case '1':
			case '7':
			case '8':
				return 8;
			case '3':
				return 16;
			case '6':
				return 1;
			case '2':
			case '4':
			case '5':
			case '9':
				return 0;
			default:
				return -1;
			}
		}

		private static void Fit(List<Pattern> list, string text, int lines, ref Pattern best, ref Match found)
		{
			foreach (Pattern item in list)
			{
				if (best != null && item.Weight <= best.Weight)
				{
					break;
				}
				if (item.Lines > lines || (item.Lines == 0 && lines > 0))
				{
					continue;
				}
				string text2 = item.Parts[0];
				string text3 = item.Parts[item.Parts.Length - 1];
				if (text.Length < text2.Length + text3.Length || !text.StartsWith(text2, StringComparison.Ordinal) || !text.EndsWith(text3, StringComparison.Ordinal))
				{
					continue;
				}
				if (item.Match == null)
				{
					StringBuilder stringBuilder = new StringBuilder("^");
					for (int i = 0; i < item.Parts.Length; i++)
					{
						if (i > 0)
						{
							stringBuilder.Append("(.*?)");
						}
						stringBuilder.Append(Regex.Escape(item.Parts[i]));
					}
					stringBuilder.Append('$');
					item.Match = new Regex(stringBuilder.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant);
				}
				Match match = item.Match.Match(text);
				if (match.Success)
				{
					best = item;
					found = match;
					break;
				}
			}
		}

		private static bool HasLetters(string s)
		{
			for (int i = 0; i < s.Length; i++)
			{
				if (char.IsLetter(s[i]))
				{
					return true;
				}
			}
			return false;
		}

		private static void NoteMissing(string text)
		{
			int num = 0;
			int num2 = 0;
			string translation = text;
			foreach (char c in translation)
			{
				if (c < '\u0080' && char.IsLetter(c))
				{
					num2 = Math.Max(num2, ++num);
					continue;
				}
				if (Foreign(c))
				{
					return;
				}
				num = 0;
			}
			if (num2 < 3 || _missingSeen.Count > 6000)
			{
				return;
			}
			if (!_missingLoaded)
			{
				_missingLoaded = true;
				string text2 = ReadOrNull(MissingPath);
				if (text2 != null)
				{
					string[] array = text2.Split('\n');
					for (int i = 0; i < array.Length; i++)
					{
						if (Split(array[i].TrimEnd('\r'), out var english, out translation))
						{
							_missingSeen.Add(english);
						}
					}
				}
			}
			if (_missingSeen.Add(text))
			{
				_missingNew.Add(text);
			}
		}

		private static void FlushMissing()
		{
			if (_missingNew.Count == 0)
			{
				return;
			}
			try
			{
				Directory.CreateDirectory(Folder);
				StringBuilder stringBuilder = new StringBuilder();
				if (!File.Exists(MissingPath))
				{
					stringBuilder.Append("# Text the mod drew in ").Append(Name).Append(" with no translation in lang/")
						.Append(Code)
						.Append(".txt. Names of people and places are here too: leave those out.\n");
				}
				foreach (string item in _missingNew)
				{
					string text = item.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\t", "\\t")
						.Replace(" = ", " \\= ");
					if (text.StartsWith("#") || text.StartsWith("@"))
					{
						text = "\\" + text;
					}
					stringBuilder.Append(text).Append(" =\n");
				}
				File.AppendAllText(MissingPath, stringBuilder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			}
			catch
			{
			}
			_missingNew.Clear();
		}

		private static bool Foreign(char c)
		{
			if (c >= '\u0590' && (c < 'Ḁ' || c > 'ỿ') && (c < '\u2000' || c > '⯿'))
			{
				if (c >= 'ﬀ')
				{
					return c > 'ﬆ';
				}
				return true;
			}
			return false;
		}

		private static bool HasForeign(string s)
		{
			if (s == null)
			{
				return false;
			}
			for (int i = 0; i < s.Length; i++)
			{
				if (Foreign(s[i]))
				{
					return true;
				}
			}
			return false;
		}

		private static void ChooseFace()
		{
			_face = null;
			_needFace = false;
			if (!_active)
			{
				return;
			}
			HashSet<char> hashSet = new HashSet<char>();
			foreach (string value in Exact.Values)
			{
				foreach (char c in value)
				{
					if (Foreign(c))
					{
						hashSet.Add(c);
					}
				}
			}
			foreach (Pattern template in Templates)
			{
				string current = template.Out;
				foreach (char c2 in current)
				{
					if (Foreign(c2))
					{
						hashSet.Add(c2);
					}
				}
			}
			if (hashSet.Count != 0)
			{
				_needFace = true;
				_face = Covering(hashSet, GameFont(Code, out _faceBold), "the game's " + Code + " font") ?? Covering(hashSet, ChineseFont(), "the game's Chinese font") ?? Covering(hashSet, SystemFont(), "the system's fallback font");
				if (_face == ChineseFont())
				{
					_faceBold = true;
				}
				if (_face == null)
				{
					_face = GameFont(Code, out _faceBold) ?? SystemFont();
					Log.Info("lang: no font has all " + hashSet.Count + " letters; some may not show");
				}
			}
		}

		private static Font Covering(HashSet<char> letters, Font f, string what)
		{
			if (f == null)
			{
				return null;
			}
			int num = 0;
			try
			{
				foreach (char letter in letters)
				{
					if (!f.HasCharacter(letter))
					{
						num++;
					}
				}
			}
			catch
			{
				return f;
			}
			if (num * 50 > letters.Count)
			{
				Log.Info("lang: " + what + " (" + f.name + ") lacks " + num + " of " + letters.Count + " letters");
				return null;
			}
			return f;
		}

		private static Font GameFont(string code, out bool bold)
		{
			bold = false;
			try
			{
				if (AssetManager.game_language_library == null || !AssetManager.game_language_library.dict.TryGetValue(code, out var value) || value.font == null)
				{
					return null;
				}
				Font font = value.font();
				bold = font != null && font.name.IndexOf("Bold", StringComparison.OrdinalIgnoreCase) >= 0;
				return font;
			}
			catch
			{
				return null;
			}
		}

		private static Font ChineseFont()
		{
			try
			{
				return LocalizedTextManager.instance?.simplified_chinese_font;
			}
			catch
			{
				return null;
			}
		}

		private static Font SystemFont()
		{
			if (_systemTried)
			{
				return _system;
			}
			_systemTried = true;
			try
			{
				_system = Font.CreateDynamicFontFromOSFont(new string[13]
				{
					"Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Malgun Gothic", "Yu Gothic UI", "Meiryo", "Leelawadee UI", "Nirmala UI", "Segoe UI", "Arial Unicode MS",
					"Noto Sans CJK SC", "PingFang SC", "Droid Sans Fallback"
				}, 32);
			}
			catch
			{
				_system = null;
			}
			return _system;
		}

		private static Font ForeignFace(string text, out bool bold)
		{
			string text2 = GameCode();
			if (!_foreignTried || _foreignFor != text2)
			{
				_foreignTried = true;
				_foreignFor = text2;
				_foreign = GameFont(text2, out _foreignBold);
				if (_foreign == null || !HasAnyForeignGlyph(_foreign, text))
				{
					_foreign = ChineseFont();
					_foreignBold = true;
				}
			}
			if (_foreign != null && HasAnyForeignGlyph(_foreign, text))
			{
				bold = _foreignBold;
				return _foreign;
			}
			bold = false;
			return SystemFont();
		}

		private static bool HasAnyForeignGlyph(Font f, string text)
		{
			try
			{
				foreach (char c in text)
				{
					if (Foreign(c))
					{
						return f.HasCharacter(c);
					}
				}
			}
			catch
			{
			}
			return true;
		}

		public static Font FaceFor(string text, string code, Font usual, out bool bold)
		{
			bold = false;
			if (string.IsNullOrEmpty(text))
			{
				return usual;
			}
			string key = ((usual != null) ? usual.GetInstanceID() : 0) + "|" + code + "|" + text;
			if (FacesFor.TryGetValue(key, out var value) && value.Key != null)
			{
				bold = value.Value;
				return value.Key;
			}
			Font font = null;
			bool flag = false;
			bool bold2;
			Font font2;
			if (usual != null && !HasForeign(text) && HasAll(usual, text))
			{
				font = usual;
			}
			else if ((font2 = GameFont(code ?? "", out bold2)) != null && HasAll(font2, text))
			{
				font = font2;
				flag = bold2;
			}
			else if (_face != null && HasAll(_face, text))
			{
				font = _face;
				flag = _faceBold;
			}
			else if ((font2 = ChineseFont()) != null && HasAll(font2, text))
			{
				font = font2;
				flag = true;
			}
			else if ((font2 = SystemFont()) != null && HasAll(font2, text))
			{
				font = font2;
			}
			if (font == null)
			{
				font = usual;
			}
			if (font != null)
			{
				FacesFor[key] = new KeyValuePair<Font, bool>(font, flag);
			}
			bold = flag;
			return font;
		}

		private static bool HasAll(Font f, string text)
		{
			try
			{
				foreach (char c in text)
				{
					if (!char.IsWhiteSpace(c) && !f.HasCharacter(c))
					{
						return false;
					}
				}
			}
			catch
			{
			}
			return true;
		}

		public static bool Fit(GUIStyle st, string text, out Font wasFont, out FontStyle wasStyle)
		{
			wasFont = st.font;
			wasStyle = st.fontStyle;
			Font font;
			bool bold;
			if (_needFace && _face != null)
			{
				font = _face;
				bold = _faceBold;
			}
			else
			{
				if (!HasForeign(text))
				{
					return false;
				}
				font = ForeignFace(text, out bold);
			}
			if (font == null || font == st.font)
			{
				return false;
			}
			st.font = font;
			if (bold)
			{
				st.fontStyle = FontStyle.Normal;
			}
			return true;
		}

		public static void Restore(GUIStyle st, Font font, FontStyle style)
		{
			st.font = font;
			st.fontStyle = style;
		}

		public static void Label(Rect r, string text, GUIStyle st)
		{
			text = T(text);
			if (Fit(st, text, out var wasFont, out var wasStyle))
			{
				GUI.Label(r, text, st);
				Restore(st, wasFont, wasStyle);
			}
			else
			{
				GUI.Label(r, text, st);
			}
		}

		public static Vector2 Size(GUIStyle st, string text)
		{
			return SizeAsIs(st, T(text));
		}

		public static Vector2 SizeAsIs(GUIStyle st, string text)
		{
			Content.text = text;
			if (!Fit(st, Content.text, out var wasFont, out var wasStyle))
			{
				return st.CalcSize(Content);
			}
			Vector2 result = st.CalcSize(Content);
			Restore(st, wasFont, wasStyle);
			return result;
		}

		public static float Height(GUIStyle st, string text, float width)
		{
			Content.text = T(text);
			if (!Fit(st, Content.text, out var wasFont, out var wasStyle))
			{
				return st.CalcHeight(Content, width);
			}
			float result = st.CalcHeight(Content, width);
			Restore(st, wasFont, wasStyle);
			return result;
		}
	}
}
