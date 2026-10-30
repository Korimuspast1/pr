using System;
using System.Globalization;

namespace FirstPerson.Net
{
	internal sealed class ServerInfo
	{
		public const string KName = "name";

		public string Name = "";

		public string World = "";

		public string MapSize = "";

		public string Age = "";

		public int Players;

		public int MaxPlayers = 8;

		public bool Locked;

		public HostRules Rules = new HostRules();

		public string ModVersion = "";

		public string Code = "";

		public string Address = "";

		public ulong SteamHost;

		public ulong LobbyId;

		public string Build = "";

		public int Ping = -1;

		public string Source = "";

		public void Write(Action<string, string> set)
		{
			set("name", Name);
			set("world", World);
			set("size", MapSize);
			set("age", Age);
			set("players", Players.ToString(CultureInfo.InvariantCulture));
			set("max", MaxPlayers.ToString(CultureInfo.InvariantCulture));
			set("locked", Locked ? "1" : "0");
			set("rules", RulesText(Rules));
			set("ver", ModVersion);
			set("wf_code", Code);
		}

		public static ServerInfo Read(Func<string, string> get)
		{
			return new ServerInfo
			{
				Name = MpPrefs.Clean(get("name"), 60),
				World = MpPrefs.Clean(get("world"), 60),
				MapSize = MpPrefs.Clean(get("size"), 20),
				Age = MpPrefs.Clean(get("age"), 40),
				Players = Int(get("players"), 0),
				MaxPlayers = Int(get("max"), 8),
				Locked = (get("locked") == "1"),
				Rules = ParseRules(get("rules")),
				ModVersion = MpPrefs.Clean(get("ver"), 20),
				Code = MpPrefs.Clean(get("wf_code"), 20)
			};
		}

		public void Pack(NetWriter w)
		{
			w.Str(Name);
			w.Str(World);
			w.Str(MapSize);
			w.Str(Age);
			w.U8(Players);
			w.U8(MaxPlayers);
			w.Bool(Locked);
			Rules.Pack(w);
			w.Str(ModVersion);
			w.Str(Code);
		}

		public static ServerInfo Unpack(NetReader r)
		{
			ServerInfo serverInfo = new ServerInfo
			{
				Name = MpPrefs.Clean(r.Str(), 60),
				World = MpPrefs.Clean(r.Str(), 60),
				MapSize = MpPrefs.Clean(r.Str(), 20),
				Age = MpPrefs.Clean(r.Str(), 40),
				Players = r.U8(),
				MaxPlayers = r.U8(),
				Locked = r.Bool(),
				Rules = HostRules.Unpack(r),
				ModVersion = MpPrefs.Clean(r.Str(), 20)
			};
			if (r.More)
			{
				serverInfo.Code = MpPrefs.Clean(r.Str(), 20);
			}
			return serverInfo;
		}

		public static string RulesText(HostRules r)
		{
			return r.Powers + "," + r.Death + "," + r.Runs + "," + (r.Pvp ? 1 : 0);
		}

		public static HostRules ParseRules(string s)
		{
			HostRules hostRules = new HostRules();
			if (string.IsNullOrEmpty(s))
			{
				return hostRules;
			}
			string[] array = s.Split(',');
			if (array.Length != 0)
			{
				hostRules.Powers = Math.Max(0, Math.Min(2, Int(array[0], 0)));
			}
			if (array.Length > 1)
			{
				hostRules.Death = Math.Max(0, Math.Min(2, Int(array[1], 0)));
			}
			if (array.Length > 2)
			{
				hostRules.Runs = Math.Max(0, Math.Min(2, Int(array[2], 0)));
			}
			if (array.Length > 3)
			{
				hostRules.Pvp = array[3] != "0";
			}
			return hostRules;
		}

		private static int Int(string s, int d)
		{
			if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				return d;
			}
			return result;
		}
	}
}
