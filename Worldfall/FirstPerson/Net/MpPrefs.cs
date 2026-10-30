using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FirstPerson.Net
{
	internal sealed class MpPrefs
	{
		public string Nickname = "";

		public string PlayerKey = "";

		public string LastAddress = "";

		public List<string> Recent = new List<string>();

		public string Rejoin = "";

		public string RejoinPassword = "";

		public string WorldTitle = "";

		public int Port = 27777;

		public int MaxPlayers = 8;

		public string Password = "";

		public bool ListOnSteam = true;

		public bool RouterPort = true;

		public bool AllowIp = true;

		public HostRules Rules = new HostRules();

		public List<string> Banned = new List<string>();

		public bool Voice = true;

		public bool OpenMic;

		public int TalkKey = 301;

		public float MicGate = 0.04f;

		public float VoiceVolume = 1f;

		private static MpPrefs _current;

		private static readonly Regex Tags = new Regex("<[^>]*>");

		private static string FilePath => Path.Combine(Log.Folder, "multiplayer.txt");

		public static MpPrefs Current => _current ?? (_current = Load());

		public string ShownName
		{
			get
			{
				if (!string.IsNullOrEmpty(Nickname))
				{
					return Nickname;
				}
				return "Wanderer" + (Math.Abs(PlayerKey.GetHashCode()) % 1000).ToString("000");
			}
		}

		public static MpPrefs Load()
		{
			MpPrefs mpPrefs = new MpPrefs();
			try
			{
				if (File.Exists(FilePath))
				{
					string[] array = File.ReadAllLines(FilePath);
					foreach (string text in array)
					{
						int num = text.IndexOf('=');
						if (num <= 0)
						{
							continue;
						}
						string text2 = text.Substring(0, num).Trim();
						string text3 = text.Substring(num + 1).Trim();
						switch (text2)
						{
						case "nickname":
							mpPrefs.Nickname = Clean(text3, 20);
							continue;
						case "player_key":
							mpPrefs.PlayerKey = text3;
							continue;
						case "last_address":
							mpPrefs.LastAddress = text3;
							continue;
						case "recent":
							mpPrefs.Recent.Add(text3);
							continue;
						case "rejoin":
							mpPrefs.Rejoin = text3;
							continue;
						case "rejoin_password":
							mpPrefs.RejoinPassword = text3;
							continue;
						case "world_title":
							mpPrefs.WorldTitle = Clean(text3, 40);
							continue;
						case "port":
							mpPrefs.Port = Math.Max(1024, Math.Min(65535, Int(text3, mpPrefs.Port)));
							continue;
						case "max_players":
							mpPrefs.MaxPlayers = Math.Max(2, Math.Min(8, Int(text3, mpPrefs.MaxPlayers)));
							continue;
						case "password":
							mpPrefs.Password = text3;
							continue;
						case "list_steam":
							mpPrefs.ListOnSteam = text3 == "1";
							continue;
						case "router_port":
							mpPrefs.RouterPort = text3 == "1";
							continue;
						case "allow_ip":
							mpPrefs.AllowIp = text3 == "1";
							continue;
						case "banned":
							mpPrefs.Banned.Add(text3);
							continue;
						case "voice":
							mpPrefs.Voice = text3 == "1";
							continue;
						case "open_mic":
							mpPrefs.OpenMic = text3 == "1";
							continue;
						case "talk_key":
							mpPrefs.TalkKey = Int(text3, mpPrefs.TalkKey);
							continue;
						case "mic_gate":
							mpPrefs.MicGate = Float(text3, mpPrefs.MicGate);
							continue;
						case "voice_volume":
							mpPrefs.VoiceVolume = Math.Max(0f, Math.Min(2f, Float(text3, mpPrefs.VoiceVolume)));
							continue;
						}
						if (text2.StartsWith("rule_", StringComparison.Ordinal))
						{
							mpPrefs.Rules.Read(text2.Substring(5), text3);
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET multiplayer.txt: " + ex.Message);
			}
			if (string.IsNullOrEmpty(mpPrefs.PlayerKey))
			{
				mpPrefs.PlayerKey = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N").Substring(0, 8);
				mpPrefs.Save();
			}
			return mpPrefs;
		}

		public void Save()
		{
			try
			{
				StringBuilder sb = new StringBuilder();
				Line("nickname", Nickname);
				Line("player_key", PlayerKey);
				Line("last_address", LastAddress);
				foreach (string item in Recent)
				{
					Line("recent", item);
				}
				if (Rejoin != "")
				{
					Line("rejoin", Rejoin);
					Line("rejoin_password", RejoinPassword);
				}
				Line("world_title", WorldTitle);
				Line("port", Port);
				Line("max_players", MaxPlayers);
				Line("password", Password);
				Line("list_steam", ListOnSteam ? 1 : 0);
				Line("router_port", RouterPort ? 1 : 0);
				Line("allow_ip", AllowIp ? 1 : 0);
				foreach (string item2 in Banned)
				{
					Line("banned", item2);
				}
				Line("voice", Voice ? 1 : 0);
				Line("open_mic", OpenMic ? 1 : 0);
				Line("talk_key", TalkKey);
				Line("mic_gate", MicGate);
				Line("voice_volume", VoiceVolume);
				Rules.Write(delegate(string k, object v)
				{
					Line("rule_" + k, v);
				});
				File.WriteAllText(FilePath, sb.ToString());
				void Line(string k, object v)
				{
					sb.Append(k).Append('=').Append(Convert.ToString(v, CultureInfo.InvariantCulture))
						.Append('\n');
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET saving multiplayer.txt: " + ex.Message);
			}
		}

		public void AddRecent(string address)
		{
			Recent.Remove(address);
			Recent.Insert(0, address);
			if (Recent.Count > 5)
			{
				Recent.RemoveRange(5, Recent.Count - 5);
			}
			LastAddress = address;
			Save();
		}

		internal static string Clean(string s, int max)
		{
			if (s == null)
			{
				return "";
			}
			if (s.Length > max * 4)
			{
				s = s.Substring(0, max * 4);
			}
			StringBuilder stringBuilder = new StringBuilder();
			string text = s;
			foreach (char c in text)
			{
				if (!char.IsControl(c))
				{
					stringBuilder.Append(c);
				}
			}
			string text2 = Tags.Replace(stringBuilder.ToString(), "").Trim();
			if (text2.Length <= max)
			{
				return text2;
			}
			return text2.Substring(0, max);
		}

		private static int Int(string v, int d)
		{
			if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				return d;
			}
			return result;
		}

		private static float Float(string v, float d)
		{
			if (!float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				return d;
			}
			return result;
		}
	}
}
