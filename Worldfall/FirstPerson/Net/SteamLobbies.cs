using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Steamworks;
using Steamworks.Data;

namespace FirstPerson.Net
{
	internal static class SteamLobbies
	{
		internal sealed class Found
		{
			public ulong LobbyId;

			public ulong Host;

			public string Build = "";

			public ServerInfo Info;
		}

		private const string Tag = "worldfall";

		private const string KHost = "host";

		private const string KBuild = "build";

		private const string KListed = "listed";

		private const string KCode = "wf_code";

		private static object _lobby;

		private static bool _hooked;

		private static bool _creating;

		private static bool _listed;

		private static int _generation;

		private static ServerInfo _latest;

		public static string Problem = "";

		private static bool _broken;

		public static bool Available
		{
			get
			{
				if (_broken)
				{
					return false;
				}
				try
				{
					return Valid();
				}
				catch (Exception ex)
				{
					_broken = true;
					Log.Info("NET steam unavailable: " + ex.GetType().Name + " " + ex.Message);
					return false;
				}
			}
		}

		public static ulong MySteamId
		{
			get
			{
				if (!Available)
				{
					return 0uL;
				}
				return SteamIdNow();
			}
		}

		public static string MySteamName
		{
			get
			{
				if (!Available)
				{
					return "";
				}
				return SteamNameNow();
			}
		}

		public static ulong LobbyId
		{
			get
			{
				if (_lobby == null || !Available)
				{
					return 0uL;
				}
				return LobbyIdNow();
			}
		}

		private static bool Valid()
		{
			if (SteamClient.IsValid)
			{
				return SteamClient.IsLoggedOn;
			}
			return false;
		}

		private static ulong SteamIdNow()
		{
			return SteamClient.SteamId.Value;
		}

		private static string SteamNameNow()
		{
			return SteamClient.Name;
		}

		public static void Publish(ServerInfo info, bool listed)
		{
			_latest = info;
			_listed = listed;
			if (!Available)
			{
				return;
			}
			try
			{
				Hook();
				Presence(on: true);
				if (_lobby != null)
				{
					Apply();
				}
				else if (!_creating)
				{
					Create();
				}
			}
			catch (Exception ex)
			{
				Problem = "Steam lobby: " + ex.Message;
				Log.Error("NET steam lobby: " + ex.Message);
			}
		}

		private static async void Create()
		{
			_creating = true;
			int generation = _generation;
			bool stale = false;
			try
			{
				object obj = await MakeLobby(Math.Max(2, _latest?.MaxPlayers ?? 8));
				if (obj == null)
				{
					Problem = "Steam did not open a lobby. Invites and the Steam list are off, joins from your profile still work";
					Log.Info("NET steam lobby not created");
					return;
				}
				if (generation != _generation || !NetSession.Hosting || _latest == null)
				{
					Leave(obj);
					stale = true;
					return;
				}
				_lobby = obj;
				Opened();
				Apply();
			}
			catch (Exception ex)
			{
				Problem = "Steam lobby: " + ex.Message;
				Log.Error("NET steam lobby: " + ex.Message);
			}
			finally
			{
				_creating = false;
			}
			if (stale && NetSession.Hosting && _lobby == null && _latest != null)
			{
				Publish(_latest, _listed);
			}
		}

		private static async Task<object> Boxed<T>(Task<T> task)
		{
			return await task;
		}

		private static Task<object> MakeLobby(int max)
		{
			return Boxed(SteamMatchmaking.CreateLobbyAsync(max));
		}

		private static void Leave(object lobby)
		{
			((Lobby)lobby).Leave();
		}

		private static void Opened()
		{
			Lobby lobby = (Lobby)_lobby;
			lobby.SetPublic();
			lobby.SetJoinable(b: true);
			Problem = "";
			Log.Info("NET steam lobby " + lobby.Id.ToString() + (_listed ? " (listed)" : " (by code and invite only)"));
		}

		private static string GetData(object lobby, string key)
		{
			return ((Lobby)lobby).GetData(key);
		}

		private static void SetData(object lobby, string key, string value)
		{
			((Lobby)lobby).SetData(key, value ?? "");
		}

		private static void Apply()
		{
			Lobby lobby = (Lobby)_lobby;
			ServerInfo latest = _latest;
			if (latest == null)
			{
				return;
			}
			lobby.SetData("worldfall", 12.ToString());
			lobby.SetData("host", SteamClient.SteamId.Value.ToString());
			lobby.SetData("build", NetProtocol.Build);
			lobby.SetData("listed", _listed ? "1" : "0");
			object box = _lobby;
			latest.Write(delegate(string k, string v)
			{
				SetData(box, k, v);
			});
			try
			{
				NetPingLocation? localPingLocation = SteamNetworkingUtils.LocalPingLocation;
				if (localPingLocation.HasValue)
				{
					lobby.SetData("ping_at", localPingLocation.Value.ToString());
				}
			}
			catch
			{
			}
		}

		private static void Presence(bool on)
		{
			SteamFriends.SetRichPresence("connect", on ? ("+wf_join " + SteamClient.SteamId.Value) : null);
		}

		public static void Close()
		{
			_generation++;
			_latest = null;
			Problem = "";
			if (!Available)
			{
				_lobby = null;
				return;
			}
			try
			{
				CloseNow();
			}
			catch
			{
			}
			_lobby = null;
		}

		private static void CloseNow()
		{
			if (_lobby != null)
			{
				((Lobby)_lobby).Leave();
			}
			Presence(on: false);
		}

		private static ulong LobbyIdNow()
		{
			return ((Lobby)_lobby).Id.Value;
		}

		public static string Invite()
		{
			if (!Available)
			{
				return "Steam is not running";
			}
			if (_lobby == null)
			{
				if (!_creating)
				{
					if (!(Problem != ""))
					{
						return "No Steam lobby";
					}
					return Problem;
				}
				return "Steam is still opening the lobby. Try again in a moment";
			}
			try
			{
				return InviteNow();
			}
			catch (Exception ex)
			{
				Log.Error("NET steam invite: " + ex.Message);
				return "Steam: " + ex.Message;
			}
		}

		private static string InviteNow()
		{
			if (!SteamUtils.IsOverlayEnabled)
			{
				return "The Steam overlay is off for WorldBox. Friends can still join from your Steam profile";
			}
			SteamFriends.OpenGameInviteOverlay(((Lobby)_lobby).Id);
			return null;
		}

		public static Task<List<Found>> Search()
		{
			if (!Available)
			{
				return Task.FromResult<List<Found>>(null);
			}
			return SearchNow();
		}

		private static async Task<List<Found>> SearchNow()
		{
			List<Found> found = new List<Found>();
			try
			{
				object obj = await ListLobbies();
				if (obj == null)
				{
					return found;
				}
				Rows(obj, found);
			}
			catch (Exception ex)
			{
				Log.Error("NET steam search: " + ex.Message);
			}
			return found;
		}

		private static Task<object> ListLobbies()
		{
			return Boxed(SteamMatchmaking.LobbyList.WithKeyValue("worldfall", 12.ToString()).WithKeyValue("listed", "1").FilterDistanceWorldwide()
				.WithMaxResults(100)
				.RequestAsync());
		}

		public static async Task<Found> FindByCode(string code)
		{
			if (!Available || string.IsNullOrEmpty(code))
			{
				return null;
			}
			try
			{
				object obj = await CodeLobbies(code);
				if (obj == null)
				{
					return null;
				}
				List<Found> list = new List<Found>();
				Rows(obj, list);
				return (list.Count > 0) ? list[0] : null;
			}
			catch (Exception ex)
			{
				Log.Error("NET steam code search: " + ex.Message);
				return null;
			}
		}

		private static Task<object> CodeLobbies(string code)
		{
			return Boxed(SteamMatchmaking.LobbyList.WithKeyValue("wf_code", code).FilterDistanceWorldwide().WithMaxResults(4)
				.RequestAsync());
		}

		private static void Rows(object lobbies, List<Found> found)
		{
			Lobby[] array = (Lobby[])lobbies;
			for (int i = 0; i < array.Length; i++)
			{
				Lobby lobby = array[i];
				object box = lobby;
				ServerInfo serverInfo = ServerInfo.Read((string k) => GetData(box, k));
				serverInfo.Ping = -1;
				try
				{
					NetPingLocation? netPingLocation = NetPingLocation.TryParseFromString(lobby.GetData("ping_at"));
					if (netPingLocation.HasValue)
					{
						serverInfo.Ping = SteamNetworkingUtils.EstimatePingTo(netPingLocation.Value);
					}
				}
				catch
				{
				}
				ulong num = ParseId(lobby.GetData("host"));
				if (num == 0L)
				{
					num = lobby.Owner.Id.Value;
				}
				serverInfo.SteamHost = num;
				serverInfo.LobbyId = lobby.Id.Value;
				serverInfo.Build = lobby.GetData("build") ?? "";
				serverInfo.Source = "Steam";
				found.Add(new Found
				{
					LobbyId = lobby.Id.Value,
					Host = num,
					Build = serverInfo.Build,
					Info = serverInfo
				});
			}
		}

		public static void Resolve(ulong lobbyId, ulong friendHint, Action<ulong, string, string> done)
		{
			if (!Available)
			{
				done(0uL, "", "Steam is not running");
				return;
			}
			try
			{
				ResolveNow(lobbyId, friendHint, done);
			}
			catch (Exception ex)
			{
				done(0uL, "", "Steam: " + ex.Message);
			}
		}

		private static async void ResolveNow(ulong lobbyId, ulong friendHint, Action<ulong, string, string> done)
		{
			ulong host = 0uL;
			string name = "";
			string error = null;
			bool entered = false;
			try
			{
				host = HostOf(lobbyId);
				if (host == 0L && friendHint != 0L)
				{
					ulong num = ParseConnect(FriendConnect(friendHint));
					if (num != 0L)
					{
						host = num;
					}
				}
				if (host == 0L)
				{
					Refresh(lobbyId);
					for (int i = 0; i < 25; i++)
					{
						if (host != 0L)
						{
							break;
						}
						await Task.Delay(120);
						host = HostOf(lobbyId);
					}
				}
				if (host == 0L)
				{
					object obj = await JoinLobby(lobbyId);
					if (Entered(obj))
					{
						entered = true;
						host = HostOf(lobbyId);
					}
					else
					{
						error = "Steam would not open that world's lobby (" + obj?.ToString() + "). It may have closed";
					}
				}
				name = LobbyData(lobbyId, "name") ?? "";
			}
			catch (Exception ex)
			{
				error = "Steam: " + ex.Message;
			}
			finally
			{
				if (entered)
				{
					try
					{
						LeaveLobby(lobbyId);
					}
					catch
					{
					}
				}
			}
			if (host == 0L && error == null)
			{
				error = "That world's lobby names no host. It may have closed";
			}
			Log.Info("NET steam lobby " + lobbyId + " -> host " + host + ((error != null) ? (" (" + error + ")") : ""));
			try
			{
				done(host, name, (host != 0L) ? null : error);
			}
			catch (Exception ex2)
			{
				Log.Error("NET steam lobby join: " + ex2);
			}
		}

		private static string FriendConnect(ulong friend)
		{
			return new Friend(friend).GetRichPresence("connect");
		}

		private static void Refresh(ulong lobby)
		{
			new Lobby(lobby).Refresh();
		}

		private static Task<object> JoinLobby(ulong lobby)
		{
			return Boxed(new Lobby(lobby).Join());
		}

		private static bool Entered(object roomEnter)
		{
			return (RoomEnter)roomEnter == RoomEnter.Success;
		}

		private static string LobbyData(ulong lobby, string key)
		{
			return new Lobby(lobby).GetData(key);
		}

		private static void LeaveLobby(ulong lobby)
		{
			new Lobby(lobby).Leave();
		}

		private static ulong HostOf(ulong lobby)
		{
			return HostOf(new Lobby(lobby));
		}

		private static ulong HostOf(Lobby l)
		{
			ulong num = ParseId(l.GetData("host"));
			if (num == 0L)
			{
				num = l.Owner.Id.Value;
			}
			return num;
		}

		private static void Hook()
		{
			if (_hooked)
			{
				return;
			}
			SteamFriends.OnGameRichPresenceJoinRequested += delegate(Friend friend, string connect)
			{
				try
				{
					Log.Info("NET steam join request from " + friend.Name + ": " + connect);
					ulong num = ParseConnect(connect);
					ulong num2 = ParseLobby(connect);
					if (num != 0L)
					{
						MpUi.JoinSteamHost(num, friend.Name, "Steam's Join game");
					}
					else if (num2 != 0L)
					{
						MpUi.JoinSteamLobby(num2, friend.Id.Value, friend.Name);
					}
					else
					{
						MpUi.JoinFailed("steam:", "Could not join " + friend.Name, "Their game sent no world to join");
					}
				}
				catch (Exception ex)
				{
					Log.Error("NET steam join request: " + ex.Message);
				}
			};
			SteamFriends.OnGameLobbyJoinRequested += delegate(Lobby lobby, SteamId friendId)
			{
				try
				{
					Log.Info("NET steam lobby invite " + lobby.Id.ToString() + " via " + friendId.Value);
					MpUi.JoinSteamLobby(lobby.Id.Value, friendId.Value, "");
				}
				catch (Exception ex)
				{
					Log.Error("NET steam lobby join request: " + ex.Message);
				}
			};
			_hooked = true;
			Log.Info("NET listening for Steam invites");
		}

		public static bool Listen()
		{
			if (!Available)
			{
				return false;
			}
			try
			{
				Hook();
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("NET steam hooks: " + ex.Message);
				return false;
			}
		}

		public static ulong ParseConnect(string connect)
		{
			return Arg(connect, "+wf_join");
		}

		public static ulong ParseLobby(string connect)
		{
			return Arg(connect, "+connect_lobby");
		}

		private static ulong Arg(string line, string key)
		{
			if (string.IsNullOrEmpty(line))
			{
				return 0uL;
			}
			string[] array = line.Split(new char[2] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i + 1 < array.Length; i++)
			{
				if (array[i] == key && ulong.TryParse(array[i + 1], out var result))
				{
					return result;
				}
			}
			return 0uL;
		}

		private static ulong ParseId(string s)
		{
			if (!ulong.TryParse(s ?? "", out var result))
			{
				return 0uL;
			}
			return result;
		}
	}
}
