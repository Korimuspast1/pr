using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace FirstPerson.Net
{
	internal static class MpUi
	{
		public static readonly List<ServerInfo> Rows = new List<ServerInfo>();

		public static string RefreshNote = "";

		private static bool _steamBusy;

		private static bool _lanBusy;

		private static float _lanSince;

		private static float _refreshedAt = -999f;

		public static string Problem = "";

		public static string ProblemKey = "";

		public static float ProblemAt = -999f;

		public static string HostProblem = "";

		public static string BannerTitle = "";

		public static string BannerText = "";

		public static float BannerUntil;

		public static bool BannerBad;

		public static bool Typing;

		public static string _joinPassword = "";

		public static string JoinAddress = "";

		private static float _codeSince = -1f;

		private static float _resolving = -1f;

		public static bool ChatOpen;

		public static string ChatText = "";

		public static bool ChatParty;

		public static bool CaretToEnd;

		private static int _openedFrame;

		public static bool Refreshing
		{
			get
			{
				if (!_steamBusy)
				{
					return _lanBusy;
				}
				return true;
			}
		}

		public static bool FindingCode
		{
			get
			{
				if (_codeSince >= 0f)
				{
					return Time.realtimeSinceStartup - _codeSince < 20f;
				}
				return false;
			}
		}

		public static bool Resolving
		{
			get
			{
				if (_resolving >= 0f)
				{
					return Time.realtimeSinceStartup - _resolving < 20f;
				}
				return false;
			}
		}

		public static void Banner(string title, string text, bool bad, float seconds = 12f)
		{
			BannerTitle = title ?? "";
			BannerText = text ?? "";
			BannerBad = bad;
			BannerUntil = Time.realtimeSinceStartup + seconds;
		}

		public static void JoinFailed(string key, string title, string reason, bool bad = true)
		{
			reason = Plain(reason);
			Problem = reason;
			ProblemKey = key ?? "";
			ProblemAt = Time.realtimeSinceStartup;
			Banner(title, reason, bad);
			Log.Info("NET join failed (" + key + "): " + reason);
		}

		public static void HostFailed(string title, string reason)
		{
			HostProblem = Plain(reason);
			Banner(title, HostProblem, bad: true);
			Log.Info("NET hosting: " + title + ". " + HostProblem);
		}

		private static string Plain(string s)
		{
			s = (s ?? "").Trim();
			if (!s.EndsWith(".", StringComparison.Ordinal) || s.EndsWith("..", StringComparison.Ordinal))
			{
				return s;
			}
			return s.Substring(0, s.Length - 1);
		}

		private static void Started(string key, string error, string name)
		{
			if (error != null)
			{
				JoinFailed(key, "Could not join " + name, error);
				return;
			}
			Problem = "";
			ProblemKey = key;
			BannerUntil = 0f;
		}

		public static void Refresh()
		{
			if (!Refreshing)
			{
				_refreshedAt = Time.realtimeSinceStartup;
				Rows.Clear();
				RefreshNote = "";
				if (SteamLobbies.Available)
				{
					_steamBusy = true;
					SearchSteam();
				}
				_lanBusy = true;
				_lanSince = Time.realtimeSinceStartup;
				LanFind.Search();
			}
		}

		public static void Poll()
		{
			if (!_lanBusy || LanFind.Busy || Time.realtimeSinceStartup - _lanSince < 0.2f)
			{
				return;
			}
			_lanBusy = false;
			foreach (LanFind.Hit h in LanFind.Hits)
			{
				if (!NetSession.Hosting || !(h.Info.Code != "") || !(h.Info.Code == NetSession.Current.Code))
				{
					Rows.RemoveAll((ServerInfo r) => r.Source == "Steam" && h.Info.Code != "" && r.Code == h.Info.Code);
					Rows.Add(h.Info);
				}
			}
		}

		public static void RefreshIfStale()
		{
			if (!Refreshing && Time.realtimeSinceStartup - _refreshedAt > 60f)
			{
				Refresh();
			}
		}

		private static async void SearchSteam()
		{
			try
			{
				List<SteamLobbies.Found> list = await SteamLobbies.Search();
				if (list == null)
				{
					return;
				}
				ulong mySteamId = SteamLobbies.MySteamId;
				ulong lobbyId = SteamLobbies.LobbyId;
				foreach (SteamLobbies.Found item in list)
				{
					if ((mySteamId == 0L || item.Host != mySteamId) && (lobbyId == 0L || item.LobbyId != lobbyId))
					{
						item.Info.SteamHost = item.Host;
						item.Info.LobbyId = item.LobbyId;
						item.Info.Build = item.Build;
						item.Info.Source = "Steam";
						Rows.Insert(0, item.Info);
					}
				}
			}
			catch (Exception ex)
			{
				AddNote("Steam: " + ex.Message);
			}
			finally
			{
				_steamBusy = false;
			}
		}

		private static void AddNote(string n)
		{
			RefreshNote = ((RefreshNote == "") ? n : (RefreshNote + ". " + n));
		}

		public static string RowKey(ServerInfo row)
		{
			if (!(row.Source == "Steam"))
			{
				return "ip:" + (row.Address ?? "").Trim();
			}
			if (row.SteamHost == 0L)
			{
				return "lobby:" + row.LobbyId;
			}
			return "steam:" + row.SteamHost;
		}

		public static string Mismatch(ServerInfo row)
		{
			if (row.ModVersion != "" && row.ModVersion != "0.9.8.9")
			{
				return "This world runs Worldfall " + row.ModVersion + ", you have 0.9.8.9. Install the same version";
			}
			if (row.Build != "" && row.Build != NetProtocol.Build)
			{
				return "This world runs another Worldfall build (" + row.Build + ", yours " + NetProtocol.Build + "). Install the same download";
			}
			return null;
		}

		public static void Join(ServerInfo row)
		{
			string key = RowKey(row);
			string title = "Could not join " + row.Name;
			if (row.Locked && string.IsNullOrEmpty(_joinPassword))
			{
				JoinFailed(key, title, "This world needs a password. Type it under Join by address, then join again");
				return;
			}
			string text = Mismatch(row);
			if (text != null)
			{
				Updater.Seen(row.ModVersion, row.Build, otherHosts: true);
				JoinFailed(key, title, text);
			}
			else if (row.Source == "Steam")
			{
				if (row.SteamHost != 0L)
				{
					Started(key, NetSession.JoinSteam(row.SteamHost, row.Name, _joinPassword), row.Name);
				}
				else if (row.LobbyId != 0L)
				{
					JoinSteamLobby(row.LobbyId, 0uL, row.Name, _joinPassword);
				}
				else
				{
					JoinFailed(key, title, "This Steam world gives no host to connect to. Refresh the list");
				}
			}
			else
			{
				Started(key, NetSession.JoinIp(row.Address, _joinPassword), row.Name);
			}
		}

		public static void JoinTyped()
		{
			string text = (JoinAddress ?? "").Trim();
			if (JoinCode.Looks(text))
			{
				JoinByCode(text);
			}
			else
			{
				Started("ip:" + text, NetSession.JoinIp(text, _joinPassword), (text == "") ? "that address" : text);
			}
		}

		private static async void JoinByCode(string typed)
		{
			JoinCode.Parsed code = JoinCode.Read(typed);
			string key = "code:" + code.Code;
			string title = "Could not join " + code.Code;
			if (FindingCode)
			{
				return;
			}
			if (NetSession.Current != null)
			{
				JoinFailed(key, title, NetSession.Hosting ? "Stop hosting first" : "Leave this multiplayer game first");
				return;
			}
			MpPrefs.Current.AddRecent(code.Code);
			ProblemKey = key;
			Problem = "";
			_codeSince = Time.realtimeSinceStartup;
			try
			{
				LanFind.Search(0.8f, code.Port);
				await Task.Delay(900);
				foreach (LanFind.Hit hit in LanFind.Hits)
				{
					if (hit.Info.Code == code.Code)
					{
						_codeSince = -1f;
						Log.Info("NET code " + code.Code + " is on this network at " + hit.Address);
						string text = NetSession.JoinIp(hit.Address, _joinPassword);
						if (text == null && NetSession.Current != null)
						{
							NetSession.Current.ShownAs(code.Code);
						}
						Started(key, text, hit.Info.Name);
						return;
					}
				}
				SteamLobbies.Found found = ((!NetSession.SteamOn) ? null : (await SteamLobbies.FindByCode(code.Code)));
				SteamLobbies.Found found2 = found;
				_codeSince = -1f;
				if (found2 != null)
				{
					string text2 = Mismatch(found2.Info);
					if (text2 != null)
					{
						Updater.Seen(found2.Info.ModVersion, found2.Build, otherHosts: true);
						JoinFailed(key, title, text2);
					}
					else if (found2.Host != 0L)
					{
						Started(key, NetSession.JoinSteam(found2.Host, found2.Info.Name, _joinPassword), found2.Info.Name);
					}
					else
					{
						JoinSteamLobby(found2.LobbyId, 0uL, found2.Info.Name, _joinPassword);
					}
				}
				else if (code.SteamOnly)
				{
					JoinFailed(key, title, NetSession.SteamOn ? "No world with that code is open on Steam. Check the code" : "This code works through Steam only. Start Steam and join again");
				}
				else
				{
					string address = IpTransport.HostPort(code.Host, code.Port);
					Log.Info("NET code " + code.Code + " -> direct address");
					string text3 = NetSession.JoinIp(address, _joinPassword);
					if (text3 == null && NetSession.Current != null)
					{
						NetSession.Current.ShownAs(code.Code);
					}
					Started(key, text3, code.Code);
				}
			}
			catch (Exception ex)
			{
				_codeSince = -1f;
				JoinFailed(key, title, ex.Message);
			}
		}

		public static void JoinSteamHost(ulong host, string name, string how)
		{
			if (string.IsNullOrEmpty(name))
			{
				name = "a friend";
			}
			Log.Info("NET joining steam:" + host + " (" + how + ")");
			Started("steam:" + host, NetSession.JoinSteam(host, name, _joinPassword), name);
		}

		public static void JoinSteamLobby(ulong lobby, ulong friend, string name, string password = null)
		{
			string key = "lobby:" + lobby;
			if (!SteamLobbies.Available)
			{
				JoinFailed(key, "Could not join " + ((name == "") ? "a friend" : name), "Steam is not running. Start Steam to join through it");
				return;
			}
			if (NetSession.Active && NetSession.Hosting)
			{
				JoinFailed(key, "Could not join " + ((name == "") ? "a friend" : name), "Stop hosting first");
				return;
			}
			ProblemKey = key;
			Problem = "";
			_resolving = Time.realtimeSinceStartup;
			SteamLobbies.Resolve(lobby, friend, delegate(ulong host, string world, string error)
			{
				_resolving = -1f;
				string text = ((world != "") ? world : ((name != "") ? name : "a friend's world"));
				if (error != null)
				{
					JoinFailed(key, "Could not join " + text, error);
				}
				else
				{
					if (password != null)
					{
						_joinPassword = password;
					}
					JoinSteamHost(host, text, "a Steam lobby");
				}
			});
		}

		public static void Keys(bool otherTyping)
		{
			if (!NetSession.Active)
			{
				ChatOpen = false;
			}
			else if (ChatOpen && Time.frameCount != _openedFrame && (ChatText.Trim().Length == 0 || ChatText.Trim() == "/") && Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
			{
				ChatOpen = false;
				ChatText = "";
			}
			else if (!(ChatOpen || otherTyping))
			{
				bool flag = Input.GetKeyDown(KeyCode.Slash) || Input.inputString.IndexOf('/') >= 0;
				if (flag || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
				{
					ChatOpen = true;
					ChatText = (flag ? "/" : "");
					CaretToEnd = flag;
					_openedFrame = Time.frameCount;
				}
			}
		}

		public static void OpenChat()
		{
			if (NetSession.Active)
			{
				ChatOpen = true;
				ChatText = "";
				_openedFrame = Time.frameCount;
			}
		}

		public static void ChatKey(Event e)
		{
			if (!ChatOpen || e.type != EventType.KeyDown)
			{
				return;
			}
			if (Time.frameCount == _openedFrame)
			{
				e.Use();
			}
			else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
			{
				string text = ChatText.Trim();
				bool flag = false;
				if (text.StartsWith("/p ", StringComparison.Ordinal))
				{
					ChatParty = true;
					text = text.Substring(3);
				}
				else if (text.StartsWith("/a ", StringComparison.Ordinal))
				{
					flag = true;
					text = text.Substring(3);
				}
				switch (text)
				{
				case "/":
				case "/p":
				case "/a":
					text = "";
					break;
				}
				if (text != "" && PlayerTalk.Talking && !ChatParty && !flag)
				{
					PlayerTalk.SayLine(text);
				}
				else if (text != "")
				{
					NetSession.Current?.SendChat(text, ChatParty);
				}
				ChatParty = false;
				ChatOpen = false;
				ChatText = "";
				e.Use();
			}
			else if (e.keyCode == KeyCode.Escape)
			{
				ChatOpen = false;
				ChatText = "";
				e.Use();
			}
		}
	}
}
