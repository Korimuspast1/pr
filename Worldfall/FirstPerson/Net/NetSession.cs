using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace FirstPerson.Net
{
	internal sealed class NetSession : INetEvents
	{
		public sealed class ChatLine
		{
			public int From;

			public string Name;

			public string Text;

			public float At;

			public bool System;

			public bool Party;

			public bool Private;

			public string Shown;

			public float ShownH;

			public float ShownW;

			public float ShownK;
		}

		public readonly bool IsHost;

		private readonly List<Transport> _transports = new List<Transport>();

		public readonly Dictionary<int, NetPlayer> Players = new Dictionary<int, NetPlayer>();

		private readonly NetWriter _w = new NetWriter(1024);

		private readonly NetReader _r = new NetReader();

		private int _nextPlayerId = 2;

		private float _infoAt;

		public NetPlayer Me;

		private NetLink _host;

		public string Stage = "";

		public static string LastEnd = "";

		public ServerInfo HostInfo;

		public HostRules Rules = new HostRules();

		public readonly List<ChatLine> Chat = new List<ChatLine>();

		private readonly bool _infoOnly;

		private Action<ServerInfo, string> _infoDone;

		public bool WorldGone;

		public List<string> LanAddresses = new List<string>();

		public string OpenProblems = "";

		private float _startedAt;

		private float _upAt = -1f;

		private float _progressAt;

		private bool _welcomed;

		private string _joinName = "";

		private string _ipHost = "";

		private int _ipPort;

		private const float ConnectLimit = 30f;

		private const float AnswerLimit = 15f;

		private const float WorldLimit = 60f;

		internal static bool NoSteamForTest;

		private static readonly List<NetSession> _asking = new List<NetSession>();

		private string _password = "";

		private string _viaCode = "";

		private bool _answered;

		private ulong _steamHost;

		private string _ipAddress = "";

		private static bool _backgroundSet;

		private static bool _argsRead;

		private static bool _listening;

		private static float _listenAt;

		private static float _startWaitSince = -1f;

		private static ulong _joinOnStart;

		private static ulong _lobbyOnStart;

		private float _playersAt;

		private bool _ipOpen;

		private string _steamCode = "";

		private string _lastAnnounced = "";

		private readonly Dictionary<NetLink, float> _waitingJoin = new Dictionary<NetLink, float>();

		private int _badLogged;

		private float _refusedShownAt = -99f;

		public static NetSession Current { get; private set; }

		public static bool Active => Current != null;

		public static bool Hosting
		{
			get
			{
				if (Current != null)
				{
					return Current.IsHost;
				}
				return false;
			}
		}

		public static bool Joined
		{
			get
			{
				if (Current != null)
				{
					return !Current.IsHost;
				}
				return false;
			}
		}

		public static bool SteamOn
		{
			get
			{
				if (!NoSteamForTest)
				{
					return SteamLobbies.Available;
				}
				return false;
			}
		}

		private static bool WorldReady
		{
			get
			{
				try
				{
					return World.world != null && Config.game_loaded && !SmoothLoader.isLoading();
				}
				catch
				{
					return false;
				}
			}
		}

		public string RejoinAddress
		{
			get
			{
				if (_steamHost == 0L)
				{
					return "ip:" + _ipAddress;
				}
				return "steam:" + _steamHost;
			}
		}

		public string Password => _password;

		public string JoinKey => RejoinAddress;

		public string Code
		{
			get
			{
				if (!IsHost)
				{
					return "";
				}
				if (_ipOpen && IpTransport.LoopbackOnly)
				{
					return JoinCode.ForAddress("127.0.0.1", MpPrefs.Current.Port);
				}
				if (_ipOpen && Upnp.State == Upnp.Phase.Working)
				{
					return "";
				}
				if (_ipOpen && Upnp.PublicIp != "" && !Upnp.CarrierNat)
				{
					return JoinCode.ForAddress(Upnp.PublicIp, MpPrefs.Current.Port);
				}
				if (!SteamOn)
				{
					return "";
				}
				return _steamCode;
			}
		}

		private bool Live
		{
			get
			{
				if (!_infoOnly)
				{
					return Current == this;
				}
				return true;
			}
		}

		public NetLink HostLink => _host;

		public static event Action<ChatLine> ChatSeen;

		private NetSession(bool host)
		{
			IsHost = host;
			_startedAt = (_progressAt = Time.realtimeSinceStartup);
		}

		public static string Host()
		{
			if (Current != null)
			{
				if (!Current.IsHost)
				{
					return "Leave this multiplayer game first";
				}
				return "Already hosting";
			}
			if (!WorldReady)
			{
				return "Load a world first";
			}
			MpPrefs current = MpPrefs.Current;
			NetSession netSession = new NetSession(host: true);
			netSession.Rules = current.Rules;
			netSession.Me = new NetPlayer
			{
				Id = 1,
				Name = current.ShownName,
				Host = true,
				Key = current.PlayerKey,
				Ready = true
			};
			netSession.Players[1] = netSession.Me;
			string text = "";
			if (current.AllowIp)
			{
				try
				{
					netSession._transports.Add(IpTransport.Listen(current.Port));
					netSession._ipOpen = true;
				}
				catch (Exception ex)
				{
					text = text + "Port " + current.Port + " is taken (" + ex.Message + "). ";
				}
			}
			netSession._steamCode = JoinCode.SteamOnly(new global::System.Random());
			if (SteamOn)
			{
				try
				{
					netSession._transports.Add(SteamTransport.Host());
				}
				catch (Exception ex2)
				{
					text = text + "Steam would not open its relay (" + ex2.Message + "). ";
				}
			}
			if (netSession._transports.Count == 0)
			{
				if (text == "")
				{
					text = (current.AllowIp ? "Nothing could be opened" : "Join by address is off and Steam is not running. Turn Join by address on or start Steam");
				}
				return "Could not open the world. " + text.Trim();
			}
			foreach (Transport transport in netSession._transports)
			{
				transport.BudgetMs = 8f;
				transport.InboxCap = 16777216L;
			}
			ModSync.Prepare();
			Current = netSession;
			netSession.OpenProblems = text.Trim();
			MpUi.HostProblem = "";
			KeepRunning(on: true);
			netSession.LanAddresses = (current.AllowIp ? IpTransport.LocalAddresses() : new List<string>());
			if (netSession._ipOpen && current.RouterPort && !IpTransport.LoopbackOnly)
			{
				Upnp.Open(current.Port);
			}
			if (netSession._ipOpen)
			{
				WinNet.Check();
			}
			HostPause.Install();
			WorldSync.HostStart(netSession);
			netSession.Me.Body = WorldSync.LocalBodyId();
			netSession.Me.God = netSession.Me.Body == 0;
			netSession.Announce(force: true);
			netSession.System("Hosting " + netSession.Describe().Name + ((text == "") ? "" : (". " + text.Trim())));
			Log.Info("NET hosting: " + string.Join(", ", netSession._transports.ConvertAll((Transport t) => t.Name).ToArray()) + ((text == "") ? "" : (" (" + text.Trim() + ")")) + ((netSession.LanAddresses.Count > 0) ? (", this PC at " + string.Join(", ", netSession.LanAddresses.ToArray())) : "") + ", build " + NetProtocol.Build);
			return null;
		}

		public static string JoinIp(string address, string password)
		{
			if (Current != null)
			{
				if (!Current.IsHost)
				{
					return "Leave this multiplayer game first";
				}
				return "Stop hosting first";
			}
			address = (address ?? "").Trim();
			if (address == "")
			{
				return "Type the host's address, like 192.168.1.20 or 203.0.113.5:27777";
			}
			if (!SplitAddress(address, out var host, out var port))
			{
				return "That address can't be read. Type it like 192.168.1.20 or myhost.net:27777";
			}
			if (!WorldReady)
			{
				return "Wait for the world to load, then join";
			}
			Current = new NetSession(host: false)
			{
				_password = (password ?? ""),
				_ipAddress = address,
				_ipHost = host,
				_ipPort = port,
				_joinName = address,
				Stage = "Connecting to " + address,
				_transports = { (Transport)IpTransport.Connect(host, port) }
			};
			KeepRunning(on: true);
			MpPrefs.Current.AddRecent(address);
			Log.Info("NET joining " + address + " (build " + NetProtocol.Build + ")");
			return null;
		}

		public void ShownAs(string code)
		{
			_joinName = code;
			_viaCode = code;
			Stage = "Connecting to " + code;
			MpPrefs current = MpPrefs.Current;
			current.Recent.Remove(_ipAddress);
			current.AddRecent(code);
		}

		public static string JoinSteam(ulong hostId, string name, string password = "")
		{
			if (Current != null)
			{
				if (!Current.IsHost && Current._steamHost == hostId)
				{
					return null;
				}
				if (!Current.IsHost)
				{
					return "Leave this multiplayer game first";
				}
				return "Stop hosting first";
			}
			if (hostId == 0L)
			{
				return "Steam gave no host to connect to";
			}
			if (!SteamLobbies.Available)
			{
				return "Steam is not running. Start Steam to join through it";
			}
			if (hostId == SteamLobbies.MySteamId)
			{
				return "That is your own world";
			}
			if (!WorldReady)
			{
				return "Wait for the world to load, then join";
			}
			name = (string.IsNullOrEmpty(name) ? "the host" : name);
			NetSession netSession = new NetSession(host: false)
			{
				_password = (password ?? ""),
				_steamHost = hostId,
				_joinName = name,
				Stage = "Connecting to " + name + " through Steam"
			};
			try
			{
				netSession._transports.Add(SteamTransport.Connect(hostId));
			}
			catch (Exception ex)
			{
				return "Steam would not start the connection (" + ex.Message + ")";
			}
			Current = netSession;
			KeepRunning(on: true);
			Log.Info("NET joining steam:" + hostId + " (" + name + ", build " + NetProtocol.Build + ")");
			return null;
		}

		public static void AskInfo(string address, Action<ServerInfo, string> done)
		{
			if (!SplitAddress(address, out var host, out var port))
			{
				done(null, "Bad address");
				return;
			}
			NetSession netSession = new NetSession(host: false, infoOnly: true)
			{
				_infoDone = done
			};
			netSession._transports.Add(IpTransport.Connect(host, port));
			_asking.Add(netSession);
		}

		private NetSession(bool host, bool infoOnly)
			: this(host)
		{
			_infoOnly = infoOnly;
			_infoAt = Time.realtimeSinceStartup;
		}

		public static void Leave(string reason = null)
		{
			NetSession current = Current;
			current?.End(reason ?? (current.IsHost ? "The host closed the world" : "Left"), byMe: true);
		}

		private void End(string reason, bool byMe)
		{
			if (Current != this)
			{
				return;
			}
			bool flag = !IsHost && WorldSync.Mirroring;
			Current = null;
			LastEnd = (byMe ? "" : reason);
			foreach (Transport transport in _transports)
			{
				try
				{
					transport.Shutdown(reason);
				}
				catch
				{
				}
			}
			_transports.Clear();
			if (IsHost)
			{
				try
				{
					SteamLobbies.Close();
				}
				catch
				{
				}
				IpTransport.LanInfo = null;
				try
				{
					Upnp.Close(wait: false);
				}
				catch
				{
				}
			}
			try
			{
				WorldSync.Stop(this, reason, byMe);
			}
			catch (Exception ex)
			{
				Log.Error("NET stopping world sync: " + ex);
			}
			VoiceChat.Stop();
			ModSync.Reset();
			PlayerTalk.Reset();
			Parties.Reset();
			KeepRunning(on: false);
			if (!byMe)
			{
				if (IsHost)
				{
					MpUi.HostFailed("Hosting stopped", reason);
				}
				else if (flag)
				{
					MpUi.JoinFailed(JoinKey, "Left " + (HostInfo?.Name ?? _joinName), reason, bad: false);
				}
				else
				{
					if (_viaCode != "" && !_answered)
					{
						reason = "Could not reach the host. Their router port may be closed, or they stopped hosting. Steam players can still join them";
					}
					MpUi.JoinFailed(JoinKey, "Could not join " + (HostInfo?.Name ?? _joinName), reason);
				}
			}
			Log.Info("NET session ended: " + reason);
		}

		private static bool CloseForQuit()
		{
			NetSession current = Current;
			if (current == null)
			{
				Transport.RunLater(all: true);
				return true;
			}
			Current = null;
			foreach (Transport transport in current._transports)
			{
				try
				{
					transport.Shutdown(current.IsHost ? "The host closed the game" : "Left");
				}
				catch
				{
				}
			}
			current._transports.Clear();
			Transport.RunLater(all: true);
			if (current.IsHost)
			{
				try
				{
					SteamLobbies.Close();
				}
				catch
				{
				}
				IpTransport.LanInfo = null;
				try
				{
					Upnp.Close(wait: true);
				}
				catch
				{
				}
			}
			try
			{
				VoiceChat.Stop();
			}
			catch
			{
			}
			Log.Info("NET session closed: the game is quitting");
			return true;
		}

		private static void KeepRunning(bool on)
		{
			try
			{
				if (on)
				{
					if (!Application.runInBackground)
					{
						Application.runInBackground = true;
						_backgroundSet = true;
					}
				}
				else if (_backgroundSet)
				{
					_backgroundSet = false;
					bool flag = WorldBoxMod.QuietTestCopy;
					try
					{
						flag |= WorldBoxMod.Instance != null && WorldBoxMod.Instance.VrRunning;
					}
					catch
					{
					}
					if (!flag)
					{
						Application.runInBackground = false;
					}
				}
			}
			catch
			{
			}
		}

		private static void RejoinAfterRestart()
		{
			MpPrefs current = MpPrefs.Current;
			if (!(current.Rejoin == "") && Current == null && WorldReady)
			{
				string rejoin = current.Rejoin;
				string rejoinPassword = current.RejoinPassword;
				current.Rejoin = "";
				current.RejoinPassword = "";
				current.Save();
				Log.Info("NET rejoining " + rejoin + " after the restart");
				if (rejoin.StartsWith("steam:", StringComparison.Ordinal) && ulong.TryParse(rejoin.Substring(6), out var result))
				{
					MpUi._joinPassword = rejoinPassword;
					MpUi.JoinSteamHost(result, "the world", "a restart for its mods");
				}
				else if (rejoin.StartsWith("ip:", StringComparison.Ordinal) || rejoin.StartsWith("code:", StringComparison.Ordinal))
				{
					MpUi._joinPassword = rejoinPassword;
					MpUi.JoinAddress = rejoin.Substring(rejoin.IndexOf(':') + 1);
					MpUi.JoinTyped();
				}
			}
		}

		private static void ListenForInvites()
		{
			if (!_argsRead)
			{
				_argsRead = true;
				Application.wantsToQuit += CloseForQuit;
				try
				{
					string connect = string.Join(" ", Environment.GetCommandLineArgs());
					_joinOnStart = SteamLobbies.ParseConnect(connect);
					_lobbyOnStart = SteamLobbies.ParseLobby(connect);
					if (_joinOnStart != 0L || _lobbyOnStart != 0L)
					{
						Log.Info("NET started by Steam to join " + ((_joinOnStart != 0L) ? ("steam:" + _joinOnStart) : ("lobby " + _lobbyOnStart)));
					}
				}
				catch (Exception ex)
				{
					Log.Error("NET reading the command line: " + ex.Message);
				}
			}
			if (_listening || Time.realtimeSinceStartup < _listenAt)
			{
				return;
			}
			_listenAt = Time.realtimeSinceStartup + 2f;
			if (WorldBoxMod.SafeStart || CrashGuard.Suspect("steam"))
			{
				_listening = true;
				Log.Info("NET Steam invites not listened for (crash guard)");
			}
			else
			{
				if (!SteamLobbies.Available)
				{
					return;
				}
				CrashGuard.Enter("steam");
				try
				{
					_listening = SteamLobbies.Listen();
				}
				catch (Exception ex2)
				{
					Log.Error("NET invites: " + ex2.Message);
				}
				finally
				{
					CrashGuard.Leave("steam");
				}
			}
		}

		private static void JoinFromStart()
		{
			if ((_joinOnStart == 0L && _lobbyOnStart == 0L) || Current != null || !WorldReady)
			{
				return;
			}
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (!SteamLobbies.Available)
			{
				if (_startWaitSince < 0f)
				{
					_startWaitSince = realtimeSinceStartup;
				}
				if (!(realtimeSinceStartup - _startWaitSince < 60f))
				{
					_joinOnStart = (_lobbyOnStart = 0uL);
					MpUi.JoinFailed("steam:", "Could not join a friend", "Steam is not running. Start Steam to join through it");
				}
			}
			else
			{
				ulong joinOnStart = _joinOnStart;
				ulong lobbyOnStart = _lobbyOnStart;
				_joinOnStart = (_lobbyOnStart = 0uL);
				if (joinOnStart != 0L)
				{
					MpUi.JoinSteamHost(joinOnStart, "a friend", "Steam's command line");
				}
				else
				{
					MpUi.JoinSteamLobby(lobbyOnStart, 0uL, "");
				}
			}
		}

		public static void Tick()
		{
			Transport.RunLater();
			ListenForInvites();
			RejoinAfterRestart();
			try
			{
				JoinFromStart();
			}
			catch (Exception ex)
			{
				Log.Error("NET joining from Steam's start: " + ex.Message);
			}
			try
			{
				Updater.Tick();
			}
			catch (Exception ex2)
			{
				Log.Error("NET updater: " + ex2);
			}
			try
			{
				MpUi.Keys(WorldBoxMod.Instance != null && WorldBoxMod.Instance.KeyboardTaken && !MpUi.Typing);
			}
			catch
			{
			}
			PanelBlocker.Tick();
			for (int num = _asking.Count - 1; num >= 0; num--)
			{
				NetSession netSession = _asking[num];
				foreach (Transport transport2 in netSession._transports)
				{
					transport2.Pump(netSession);
				}
				if (netSession._infoDone == null || Time.realtimeSinceStartup - netSession._infoAt > 10f)
				{
					netSession._infoDone?.Invoke(null, "No answer");
					foreach (Transport transport3 in netSession._transports)
					{
						transport3.Shutdown("");
					}
					_asking.RemoveAt(num);
				}
			}
			NetSession current = Current;
			if (current == null)
			{
				return;
			}
			KeepRunning(on: true);
			if (current.IsHost && WorldSync.HostWorldChanged())
			{
				current.WorldGone = true;
				current.End("The host loaded another world", byMe: true);
				MpUi.HostFailed("Hosting stopped", "Another world was loaded. Host again to share it");
				return;
			}
			Transport[] array = current._transports.ToArray();
			foreach (Transport transport in array)
			{
				try
				{
					transport.Pump(current);
				}
				catch (Exception ex3)
				{
					Log.Error("NET pump " + transport.Name + ": " + ex3);
				}
				if (Current != current)
				{
					return;
				}
			}
			if (!current.IsHost)
			{
				if (WorldSync.JoinerWorldChanged())
				{
					current.End("You loaded another world", byMe: true);
					return;
				}
				current.JoinerWatch();
				if (Current != current)
				{
					return;
				}
			}
			try
			{
				WorldSync.Tick(current);
			}
			catch (Exception ex4)
			{
				Log.Error("NET world sync: " + ex4);
			}
			try
			{
				VoiceChat.Tick(current);
			}
			catch (Exception ex5)
			{
				Log.Error("NET voice: " + ex5.Message);
			}
			try
			{
				PlayerTalk.Tick();
			}
			catch (Exception ex6)
			{
				Log.Error("NET talk: " + ex6);
			}
			if (!current.IsHost)
			{
				return;
			}
			current.HostTick();
			try
			{
				ModSync.HostTick(current);
			}
			catch (Exception ex7)
			{
				Log.Error("NET sending mods: " + ex7.Message);
			}
		}

		private void JoinerWatch()
		{
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			if (WorldSync.Mirroring)
			{
				return;
			}
			if (ModSync.Asking || ModSync.FetchingWorkshop)
			{
				_progressAt = realtimeSinceStartup;
			}
			else if (ModSync.FetchingFiles)
			{
				if (realtimeSinceStartup - _progressAt > 60f)
				{
					End("The host stopped sending its mods", byMe: false);
				}
			}
			else if (_host == null)
			{
				if (realtimeSinceStartup - _startedAt > 30f)
				{
					End((_steamHost != 0L) ? ("Could not reach " + _joinName + " through Steam. They must be hosting with Steam running") : IpTransport.NoAnswer(_ipHost, _ipPort), byMe: false);
				}
			}
			else if (!_welcomed)
			{
				if (realtimeSinceStartup - _upAt > 15f)
				{
					End("The host did not answer. Their game may be paused, minimized or loading a world", byMe: false);
				}
			}
			else if (!WorldSync.LoadingHostWorld && realtimeSinceStartup - _progressAt > 60f)
			{
				End("The host stopped sending its world", byMe: false);
			}
		}

		private void HostTick()
		{
			float realtimeSinceStartup = Time.realtimeSinceStartup;
			Me.Body = WorldSync.LocalBodyId();
			Me.God = Me.Body == 0;
			foreach (KeyValuePair<NetLink, float> item in new List<KeyValuePair<NetLink, float>>(_waitingJoin))
			{
				if (realtimeSinceStartup - item.Value > 15f)
				{
					_waitingJoin.Remove(item.Key);
					item.Key.Close("No join message");
				}
			}
			if (!(realtimeSinceStartup - _playersAt > 2f))
			{
				return;
			}
			_playersAt = realtimeSinceStartup;
			foreach (NetPlayer value in Players.Values)
			{
				if (value.Link != null)
				{
					value.PingMs = (int)value.Link.PingMs;
				}
			}
			SendPlayers();
			Announce(force: false);
		}

		public ServerInfo Describe()
		{
			MpPrefs current = MpPrefs.Current;
			return new ServerInfo
			{
				Name = ((!string.IsNullOrEmpty(current.WorldTitle)) ? current.WorldTitle : WorldSync.WorldName()),
				World = WorldSync.WorldName(),
				MapSize = WorldSync.MapSizeName(),
				Age = WorldSync.AgeName(),
				Players = Players.Count,
				MaxPlayers = current.MaxPlayers,
				Locked = !string.IsNullOrEmpty(current.Password),
				Rules = Rules,
				ModVersion = "0.9.8.9",
				Code = (IsHost ? Code : "")
			};
		}

		private void Announce(bool force)
		{
			ServerInfo serverInfo = Describe();
			StringBuilder sb = new StringBuilder();
			serverInfo.Write(delegate(string k, string v)
			{
				sb.Append(k).Append('=').Append(v)
					.Append(';');
			});
			string text = sb.ToString();
			if (force || text != _lastAnnounced)
			{
				_lastAnnounced = text;
				if (SteamOn)
				{
					SteamLobbies.Publish(serverInfo, MpPrefs.Current.ListOnSteam);
				}
				if (_ipOpen)
				{
					NetWriter netWriter = new NetWriter(160);
					serverInfo.Pack(netWriter);
					IpTransport.LanInfo = netWriter.ToArray();
				}
			}
		}

		public void LinkUp(NetLink link)
		{
			if (Live)
			{
				if (_infoOnly)
				{
					_w.Reset();
					_w.VarU(4uL);
					link.Send(_w, Delivery.Reliable);
					return;
				}
				if (IsHost)
				{
					link.Tag = null;
					_waitingJoin[link] = Time.realtimeSinceStartup;
					return;
				}
				_host = link;
				_answered = true;
				_upAt = (_progressAt = Time.realtimeSinceStartup);
				ModSync.Prepare();
				Stage = "Waiting for " + _joinName + " to answer";
				MpPrefs current = MpPrefs.Current;
				_w.Reset();
				_w.VarU(1uL);
				_w.VarU(12uL);
				_w.Str("0.9.8.9");
				_w.Str(current.ShownName);
				_w.Str(current.PlayerKey);
				_w.Str(PasswordProof(_password));
				_w.VarU(SteamLobbies.MySteamId);
				_w.Str(NetProtocol.Build);
				link.Send(_w, Delivery.Reliable);
			}
		}

		public void LinkDown(NetLink link, string reason)
		{
			if (!Live)
			{
				return;
			}
			if (_infoOnly)
			{
				_infoDone?.Invoke(null, reason);
				_infoDone = null;
			}
			else if (IsHost)
			{
				_waitingJoin.Remove(link);
				if (link.Tag is NetPlayer netPlayer && Players.ContainsKey(netPlayer.Id))
				{
					Players.Remove(netPlayer.Id);
					try
					{
						WorldSync.PlayerLeft(this, netPlayer);
					}
					catch (Exception ex)
					{
						Log.Error("NET player left: " + ex);
					}
					System(netPlayer.Name + " left" + ((reason == "Left" || string.IsNullOrEmpty(reason)) ? "" : (" (" + reason + ")")));
					SendPlayers();
					Announce(force: false);
					Log.Info("NET " + netPlayer?.ToString() + " left: " + reason);
				}
			}
			else
			{
				End(string.IsNullOrEmpty(reason) ? "The connection to the host closed" : reason, byMe: false);
			}
		}

		public void Data(NetLink link, byte[] data, int off, int len)
		{
			if (!Live || !link.Open)
			{
				return;
			}
			_r.Set(data, off, len);
			int id;
			try
			{
				id = (int)_r.VarU();
			}
			catch
			{
				return;
			}
			try
			{
				if (_infoOnly)
				{
					InfoOnlyData(id);
				}
				else if (IsHost)
				{
					HostData(link, id, _r);
				}
				else
				{
					JoinerData(id, _r);
				}
			}
			catch (FormatException ex)
			{
				if (_badLogged++ < 20)
				{
					Log.Info("NET bad message " + id + " from " + link?.ToString() + ": " + ex.Message);
				}
				if (IsHost && !(link.Tag is NetPlayer))
				{
					link.Close("Bad data");
				}
			}
		}

		private void InfoOnlyData(int id)
		{
			if (id == 4 && _infoDone != null)
			{
				ServerInfo arg = ServerInfo.Unpack(_r);
				_infoDone(arg, null);
				_infoDone = null;
			}
		}

		private void HostData(NetLink link, int id, NetReader r)
		{
			if (!(link.Tag is NetPlayer netPlayer))
			{
				switch (id)
				{
				case 4:
					_w.Reset();
					_w.VarU(4uL);
					Describe().Pack(_w);
					link.Send(_w, Delivery.Reliable);
					break;
				case 1:
					HostJoin(link, r);
					break;
				default:
					link.Close("Not a Worldfall join");
					break;
				}
			}
			else
			{
				if (!Players.ContainsKey(netPlayer.Id))
				{
					return;
				}
				switch (id)
				{
				case 7:
				{
					string text = MpPrefs.Clean(r.Str(), 200);
					bool party = r.Bool();
					if (text != "")
					{
						Say(netPlayer, text, party);
					}
					break;
				}
				case 56:
					Parties.HostMessage(this, netPlayer, r);
					break;
				case 57:
					PlayerTalk.HostMessage(this, netPlayer, r);
					break;
				case 10:
					if (!netPlayer.Ready && !netPlayer.WorldSent)
					{
						netPlayer.WorldSent = true;
						WorldSync.SendWorld(this, netPlayer);
					}
					break;
				case 9:
					ModSync.SendFiles(netPlayer, r);
					break;
				case 55:
				{
					byte[] array = r.Bytes(65536);
					VoiceChat.Relay(this, netPlayer, array, 0, array.Length);
					break;
				}
				case 23:
					netPlayer.Ready = true;
					WorldSync.PlayerReady(this, netPlayer);
					if (netPlayer.Resync)
					{
						WorldSync.HostResynced(netPlayer);
						System(netPlayer.Name + " resynced");
					}
					else
					{
						System(netPlayer.Name + " joined");
					}
					SendPlayers();
					break;
				case 64:
					WorldSync.HostResync(this, netPlayer);
					break;
				default:
					WorldSync.HostMessage(this, netPlayer, id, r);
					break;
				}
			}
		}

		private void HostJoin(NetLink link, NetReader r)
		{
			_waitingJoin.Remove(link);
			MpPrefs current = MpPrefs.Current;
			int num = (int)r.VarU();
			string text = r.Str() ?? "";
			string text2 = MpPrefs.Clean(r.Str(), 20);
			string text3 = r.Str() ?? "";
			string text4 = r.Str() ?? "";
			ulong num2 = r.VarU();
			string text5 = (r.More ? (r.Str() ?? "") : "");
			string text6 = null;
			if (text != "0.9.8.9")
			{
				text6 = "The host runs Worldfall 0.9.8.9, you have " + ((text == "") ? "another version" : text) + ". Install the same version";
			}
			else if (num != 12)
			{
				text6 = "The host runs another Worldfall build (multiplayer " + 12 + ", yours " + num + "). Install the same download on both PCs";
			}
			else if (text5 != NetProtocol.Build)
			{
				text6 = "The host runs another Worldfall build (" + NetProtocol.Build + ", yours " + ((text5 == "") ? "older" : text5) + "). Install the same download on both PCs";
			}
			else if ((text3 != "" && current.Banned.Contains(text3)) || (!SharedAddress(link.Address) && current.Banned.Contains(link.Address)) || (num2 != 0L && current.Banned.Contains("steam:" + num2)))
			{
				text6 = "You are banned from this world. The host can lift bans in F2, Multiplayer";
			}
			else if (Players.Count >= current.MaxPlayers)
			{
				text6 = "The world is full (" + current.MaxPlayers + " players)";
			}
			else if (!string.IsNullOrEmpty(current.Password) && text4 != PasswordProof(current.Password))
			{
				text6 = (string.IsNullOrEmpty(text4) ? "This world needs a password. Type it under Password and join again" : "Wrong password");
			}
			else if (text3.Length < 16)
			{
				text6 = "The join message was broken. Join again";
			}
			foreach (NetPlayer value in Players.Values)
			{
				if (text6 == null && value.Key == text3)
				{
					text6 = "You are already in this world. Wait a few seconds and join again";
				}
			}
			if (text6 != null)
			{
				_w.Reset();
				_w.VarU(3uL);
				_w.Str(text6);
				_w.Str("0.9.8.9");
				_w.Str(NetProtocol.Build);
				link.Send(_w, Delivery.Reliable);
				if (text != "0.9.8.9" || text5 != NetProtocol.Build)
				{
					Updater.Seen(text, text5, otherHosts: false);
				}
				link.Close(text6);
				Log.Info("NET refused " + link?.ToString() + " (" + text2 + "): " + text6);
				float realtimeSinceStartup = Time.realtimeSinceStartup;
				if (realtimeSinceStartup - _refusedShownAt > 5f)
				{
					_refusedShownAt = realtimeSinceStartup;
					Note(new ChatLine
					{
						Text = ((text2 == "") ? "Someone" : text2) + " could not join (" + (text6.StartsWith("You are banned", StringComparison.Ordinal) ? "banned" : text6) + ")",
						System = true,
						At = realtimeSinceStartup
					});
				}
				return;
			}
			if (text2 == "")
			{
				text2 = "Wanderer";
			}
			string name = text2;
			int num3 = 2;
			while (Exists(name))
			{
				name = text2 + " " + num3;
				num3++;
			}
			NetPlayer netPlayer = (NetPlayer)(link.Tag = new NetPlayer
			{
				Id = _nextPlayerId++,
				Name = name,
				Key = text3,
				Link = link,
				SteamId = num2
			});
			Players[netPlayer.Id] = netPlayer;
			netPlayer.LastBody = WorldSync.LastBodyFor(netPlayer);
			_w.Reset();
			_w.VarU(2uL);
			_w.VarU((ulong)netPlayer.Id);
			Describe().Pack(_w);
			_w.VarU((ulong)Players.Count);
			foreach (NetPlayer value2 in Players.Values)
			{
				value2.Pack(_w);
			}
			link.Send(_w, Delivery.Reliable);
			Log.Info("NET " + netPlayer?.ToString() + " joining from " + link.Address);
			System(netPlayer.Name + " is joining");
			ModSync.SendList(netPlayer);
			Announce(force: false);
		}

		private bool Exists(string name)
		{
			foreach (NetPlayer value in Players.Values)
			{
				if (string.Equals(value.Name, name, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		private static bool SharedAddress(string address)
		{
			if (string.IsNullOrEmpty(address))
			{
				return true;
			}
			if (address.StartsWith("steam:", StringComparison.Ordinal))
			{
				return address.Length <= 6;
			}
			if (IPAddress.TryParse(address, out var address2))
			{
				return IPAddress.IsLoopback(address2);
			}
			return true;
		}

		public void Kick(NetPlayer p, string reason, bool ban)
		{
			if (!IsHost || p == null || p.Host || p.Link == null)
			{
				return;
			}
			if (ban)
			{
				MpPrefs.Current.Banned.Add(p.Key);
				if (!SharedAddress(p.Link.Address))
				{
					MpPrefs.Current.Banned.Add(p.Link.Address);
				}
				MpPrefs.Current.Save();
			}
			_w.Reset();
			_w.VarU(6uL);
			_w.Str(reason);
			p.Link.Send(_w, Delivery.Reliable);
			p.Link.Close(reason);
		}

		public void SendPlayers()
		{
			if (!IsHost)
			{
				return;
			}
			_w.Reset();
			_w.VarU(5uL);
			_w.VarU((ulong)Players.Count);
			foreach (NetPlayer value in Players.Values)
			{
				value.Pack(_w);
			}
			Rules.Pack(_w);
			_w.Bool(Rules.GuestsDig);
			ToAll(_w, Delivery.Reliable, readyOnly: false);
		}

		public void ToAll(NetWriter w, Delivery how, bool readyOnly = true, NetPlayer except = null)
		{
			foreach (NetPlayer value in Players.Values)
			{
				if (value.Link != null && value != except && (!readyOnly || value.Ready))
				{
					value.Link.Send(w, how);
				}
			}
		}

		private void JoinerData(int id, NetReader r)
		{
			switch (id)
			{
			case 2:
			{
				int num2 = (int)r.VarU();
				HostInfo = ServerInfo.Unpack(r);
				Rules = HostInfo.Rules;
				ReadPlayers(r);
				Players.TryGetValue(num2, out Me);
				if (Me == null)
				{
					Me = new NetPlayer
					{
						Id = num2,
						Name = MpPrefs.Current.ShownName
					};
				}
				_welcomed = true;
				_progressAt = Time.realtimeSinceStartup;
				Stage = "Getting the world";
				Log.Info("NET welcomed as " + Me?.ToString() + " into " + HostInfo.Name);
				return;
			}
			case 3:
			case 6:
			{
				_answered = true;
				string text2 = r.Str() ?? "";
				if (id == 3 && r.More)
				{
					string text3 = r.Str() ?? "";
					string text4 = (r.More ? (r.Str() ?? "") : "");
					if (text3 != "0.9.8.9" || text4 != NetProtocol.Build)
					{
						Updater.Seen(text3, text4, otherHosts: true);
					}
				}
				End((text2 != "") ? text2 : ((id == 6) ? "Removed by the host" : "The host refused the join"), byMe: false);
				return;
			}
			case 56:
				Parties.JoinerMessage(this, r);
				return;
			case 57:
				PlayerTalk.JoinerMessage(this, r);
				return;
			case 8:
				_progressAt = Time.realtimeSinceStartup;
				ModSync.ListArrived(this, r);
				return;
			case 9:
				_progressAt = Time.realtimeSinceStartup;
				ModSync.FileArrived(this, r);
				return;
			case 62:
				VoiceChat.Received((int)r.VarU(), r.Bytes(65536));
				return;
			case 5:
			{
				ReadPlayers(r);
				if (r.More)
				{
					Rules = HostRules.Unpack(r);
					if (r.More)
					{
						Rules.GuestsDig = r.Bool();
					}
				}
				if (Me != null && Players.TryGetValue(Me.Id, out var value))
				{
					Me = value;
				}
				return;
			}
			case 7:
			{
				int num = (int)r.VarU();
				string name = MpPrefs.Clean(r.Str(), 24);
				string text = MpPrefs.Clean(r.Str(), 300);
				bool party = r.Bool();
				bool system = r.Bool();
				AddChat(new ChatLine
				{
					From = num,
					Name = name,
					Text = text,
					Party = party,
					System = system,
					At = Time.realtimeSinceStartup
				});
				return;
			}
			case 20:
			case 21:
			case 22:
				_progressAt = Time.realtimeSinceStartup;
				break;
			}
			WorldSync.JoinerMessage(this, id, r);
		}

		private void ReadPlayers(NetReader r)
		{
			int num = r.Count(64);
			Players.Clear();
			for (int i = 0; i < num; i++)
			{
				NetPlayer netPlayer = NetPlayer.Unpack(r);
				Players[netPlayer.Id] = netPlayer;
			}
		}

		public void ToHost(NetWriter w, Delivery how)
		{
			_host?.Send(w, how);
		}

		public void SendChat(string text, bool party)
		{
			text = MpPrefs.Clean(text, 200);
			if (!(text == ""))
			{
				if (IsHost)
				{
					Say(Me, text, party);
					return;
				}
				_w.Reset();
				_w.VarU(7uL);
				_w.Str(text);
				_w.Bool(party);
				ToHost(_w, Delivery.Reliable);
			}
		}

		private void Say(NetPlayer from, string text, bool party)
		{
			if (party && from.Party == 0)
			{
				party = false;
			}
			ChatLine line = new ChatLine
			{
				From = from.Id,
				Name = from.Name,
				Text = text,
				Party = party,
				At = Time.realtimeSinceStartup
			};
			_w.Reset();
			_w.VarU(7uL);
			_w.VarU((ulong)from.Id);
			_w.Str(from.Name);
			_w.Str(text);
			_w.Bool(party);
			_w.Bool(v: false);
			foreach (NetPlayer value in Players.Values)
			{
				if (!party || value.Party == from.Party)
				{
					if (value.Host)
					{
						AddChat(line);
					}
					else
					{
						value.Link?.Send(_w, Delivery.Reliable);
					}
				}
			}
		}

		public void System(string text)
		{
			AddChat(new ChatLine
			{
				Text = text,
				System = true,
				At = Time.realtimeSinceStartup
			});
			if (IsHost)
			{
				_w.Reset();
				_w.VarU(7uL);
				_w.VarU(0uL);
				_w.Str("");
				_w.Str(text);
				_w.Bool(v: false);
				_w.Bool(v: true);
				ToAll(_w, Delivery.Reliable, readyOnly: false);
			}
		}

		public void Note(ChatLine line)
		{
			AddChat(line);
		}

		private void AddChat(ChatLine line)
		{
			Chat.Add(line);
			if (Chat.Count > 100)
			{
				Chat.RemoveRange(0, Chat.Count - 100);
			}
			NetSession.ChatSeen?.Invoke(line);
		}

		public static bool SplitAddress(string address, out string host, out int port)
		{
			host = null;
			port = 27777;
			if (string.IsNullOrEmpty(address))
			{
				return false;
			}
			address = address.Trim();
			if (address.Length == 0 || address.IndexOf(' ') >= 0)
			{
				return false;
			}
			if (address.StartsWith("[", StringComparison.Ordinal))
			{
				int num = address.IndexOf(']');
				if (num < 0)
				{
					return false;
				}
				host = address.Substring(1, num - 1);
				string text = address.Substring(num + 1);
				if (text != "" && (text[0] != ':' || !int.TryParse(text.Substring(1), out port) || port < 1 || port > 65535))
				{
					return false;
				}
				return host.Length > 0;
			}
			int num2 = address.LastIndexOf(':');
			if (num2 >= 0 && address.IndexOf(':') == num2)
			{
				if (!int.TryParse(address.Substring(num2 + 1), out port) || port < 1 || port > 65535)
				{
					return false;
				}
				host = address.Substring(0, num2);
			}
			else
			{
				host = address;
			}
			return host.Length > 0;
		}

		private static string PasswordProof(string password)
		{
			if (string.IsNullOrEmpty(password))
			{
				return "";
			}
			using (SHA256 sHA = SHA256.Create())
			{
				return Convert.ToBase64String(sHA.ComputeHash(Encoding.UTF8.GetBytes("worldfall:" + password)));
			}
		}

		public NetPlayer PlayerByBody(long actorId)
		{
			if (actorId == 0L)
			{
				return null;
			}
			foreach (NetPlayer value in Players.Values)
			{
				if (value.Body == actorId)
				{
					return value;
				}
			}
			return null;
		}
	}
}
