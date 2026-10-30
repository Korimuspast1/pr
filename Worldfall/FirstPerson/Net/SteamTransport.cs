using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Steamworks;
using Steamworks.Data;

namespace FirstPerson.Net
{
	internal sealed class SteamTransport : Transport
	{
		private sealed class SteamLink : NetLink
		{
			private readonly SteamTransport _t;

			private readonly uint _cid;

			private readonly Queue<byte[]> _waiting = new Queue<byte[]>();

			private long _queued;

			private float _ping;

			public bool Up;

			public bool Closed;

			public string ByeReason;

			public MemoryStream Pieces;

			public override float PingMs => _ping;

			public override long Queued => _queued;

			public override bool Open => !Closed;

			public SteamLink(SteamTransport t, uint connection)
			{
				_t = t;
				_cid = connection;
			}

			public void Pong(double sentAt)
			{
				float num = (float)(NowMs - sentAt);
				if (!(num < 0f) && !(num > 60000f))
				{
					_ping = ((_ping <= 0f) ? num : (_ping * 0.8f + num * 0.2f));
				}
			}

			public override void Send(byte[] data, int off, int len, Delivery how)
			{
				if (Closed)
				{
					return;
				}
				if (how == Delivery.Unreliable && len < 1100 && _waiting.Count == 0)
				{
					byte[] array = new byte[len + 1];
					array[0] = 0;
					Buffer.BlockCopy(data, off, array, 1, len);
					Raw(array, 0, array.Length, SendType.NoNagle);
				}
				else if (how != Delivery.Unreliable || _queued <= 262144)
				{
					while (len > 262144)
					{
						Reliable(4, data, off, 262144);
						off += 262144;
						len -= 262144;
					}
					Reliable(0, data, off, len);
				}
			}

			private void Reliable(byte kind, byte[] data, int off, int len)
			{
				byte[] array = new byte[len + 1];
				array[0] = kind;
				Buffer.BlockCopy(data, off, array, 1, len);
				if (_waiting.Count > 0 || Raw(array, 0, array.Length, SendType.Reliable) == Result.LimitExceeded)
				{
					_waiting.Enqueue(array);
					_queued += array.Length;
				}
			}

			public void Flush()
			{
				while (_waiting.Count > 0)
				{
					byte[] array = _waiting.Peek();
					if (Raw(array, 0, array.Length, SendType.Reliable) == Result.LimitExceeded)
					{
						break;
					}
					_waiting.Dequeue();
					_queued -= array.Length;
				}
			}

			public Result Raw(byte[] m, int off, int len, SendType how)
			{
				try
				{
					return ((Connection)_cid).SendMessage(m, off, len, how);
				}
				catch
				{
					return Result.Fail;
				}
			}

			public override void Close(string reason)
			{
				if (!Closed)
				{
					Closed = true;
					byte[] bytes = Encoding.UTF8.GetBytes(reason ?? "");
					byte[] array = new byte[bytes.Length + 1];
					array[0] = 3;
					bytes.CopyTo(array, 1);
					Raw(array, 0, array.Length, SendType.Reliable);
					try
					{
						((Connection)_cid).Close(linger: true, 0, reason ?? "closing");
					}
					catch
					{
					}
					_t._links.Remove(_cid);
					_t.PostDown(this, reason);
				}
			}
		}

		private const byte KData = 0;

		private const byte KPing = 1;

		private const byte KPong = 2;

		private const byte KBye = 3;

		private const byte KPiece = 4;

		private const int MaxUnreliable = 1100;

		private const int MaxPiece = 262144;

		private const long MaxQueuedUnreliable = 262144L;

		private object _socket;

		private object _conn;

		private readonly Dictionary<uint, SteamLink> _links = new Dictionary<uint, SteamLink>();

		private readonly bool _server;

		private static readonly Stopwatch Clock = Stopwatch.StartNew();

		private double _pingedAt;

		private byte[] _scratch = new byte[2048];

		private static bool _tuned;

		public override string Name => "Steam";

		private static double NowMs => Clock.Elapsed.TotalMilliseconds;

		private SteamTransport(bool server)
		{
			_server = server;
		}

		public static SteamTransport Host()
		{
			Tune();
			SteamTransport steamTransport = new SteamTransport(server: true);
			SocketManager socketManager = SteamGlue.OpenHost(steamTransport);
			if (socketManager == null)
			{
				throw new Exception("Steam would not open a relay socket");
			}
			steamTransport._socket = socketManager;
			Log.Info("NET steam relay socket open as " + SteamClient.SteamId.ToString());
			return steamTransport;
		}

		public static SteamTransport Connect(ulong host)
		{
			Tune();
			SteamTransport steamTransport = new SteamTransport(server: false);
			ConnectionManager connectionManager = SteamGlue.Connect(steamTransport, host);
			if (connectionManager == null)
			{
				throw new Exception("Steam would not start the connection");
			}
			steamTransport._conn = connectionManager;
			SteamLink value = new SteamLink(steamTransport, connectionManager.Connection.Id)
			{
				Address = "steam:" + host
			};
			steamTransport._links[connectionManager.Connection.Id] = value;
			return steamTransport;
		}

		private static void Tune()
		{
			if (_tuned)
			{
				return;
			}
			_tuned = true;
			try
			{
				SteamNetworkingUtils.InitRelayNetworkAccess();
				SteamNetworkingUtils.SendBufferSize = 8388608;
				SteamNetworkingUtils.ConnectionTimeout = 15000;
				SteamNetworkingUtils.Timeout = 20000;
			}
			catch (Exception ex)
			{
				Log.Error("NET steam tuning: " + ex.Message);
			}
		}

		public override void Pump(INetEvents ev)
		{
			try
			{
				(_socket as SocketManager)?.Receive(64);
				(_conn as ConnectionManager)?.Receive(64);
			}
			catch (Exception ex)
			{
				Log.Error("NET steam receive: " + ex);
			}
			double nowMs = NowMs;
			foreach (SteamLink value in _links.Values)
			{
				if (value.Open)
				{
					value.Flush();
					if (nowMs - _pingedAt >= 1000.0)
					{
						byte[] array = new byte[9] { 1, 0, 0, 0, 0, 0, 0, 0, 0 };
						BitConverter.GetBytes(nowMs).CopyTo(array, 1);
						value.Raw(array, 0, array.Length, SendType.NoNagle);
					}
				}
			}
			if (nowMs - _pingedAt >= 1000.0)
			{
				_pingedAt = nowMs;
			}
			base.Pump(ev);
		}

		internal void Message(uint cid, IntPtr data, int size)
		{
			_links.TryGetValue(cid, out var value);
			Received(value, data, size);
		}

		private void Received(SteamLink link, IntPtr data, int size)
		{
			if (size < 1 || link == null)
			{
				return;
			}
			if (_scratch.Length < size)
			{
				_scratch = new byte[Math.Max(size, _scratch.Length * 2)];
			}
			Marshal.Copy(data, _scratch, 0, size);
			switch (_scratch[0])
			{
			case 0:
				if (link.Pieces != null && link.Pieces.Length > 0)
				{
					link.Pieces.Write(_scratch, 1, size - 1);
					byte[] data2 = link.Pieces.ToArray();
					link.Pieces = null;
					PostData(link, data2);
				}
				else
				{
					byte[] array = new byte[size - 1];
					Buffer.BlockCopy(_scratch, 1, array, 0, size - 1);
					PostData(link, array);
				}
				break;
			case 4:
				if (link.Pieces == null)
				{
					link.Pieces = new MemoryStream();
				}
				if (link.Pieces.Length + size > (_server ? 4194304 : 50331648))
				{
					link.Pieces = null;
					link.Close("Bad data from the other side");
				}
				else
				{
					link.Pieces.Write(_scratch, 1, size - 1);
				}
				break;
			case 1:
				_scratch[0] = 2;
				link.Raw(_scratch, 0, size, SendType.NoNagle);
				break;
			case 2:
				if (size >= 9)
				{
					link.Pong(BitConverter.ToDouble(_scratch, 1));
				}
				break;
			case 3:
				link.ByeReason = MpPrefs.Clean(Encoding.UTF8.GetString(_scratch, 1, Math.Min(size - 1, 1024)), 200);
				break;
			}
		}

		internal void Up(Connection c, ConnectionInfo info)
		{
			if (!_links.TryGetValue(c.Id, out var value))
			{
				value = new SteamLink(this, c.Id)
				{
					Address = "steam:" + info.Identity.SteamId.Value
				};
				_links[c.Id] = value;
			}
			value.Up = true;
			PostUp(value);
		}

		internal void Down(Connection c, ConnectionInfo info)
		{
			if (!_links.TryGetValue(c.Id, out var value))
			{
				return;
			}
			_links.Remove(c.Id);
			if (!value.Closed)
			{
				value.Closed = true;
				string text = value.ByeReason;
				if (string.IsNullOrEmpty(text))
				{
					text = ((info.State != ConnectionState.ClosedByPeer) ? ((!value.Up) ? ("Could not reach the host through Steam. They must be hosting with Steam running (" + info.EndReason.ToString() + ")") : ("Connection lost (" + info.EndReason.ToString() + ")")) : (_server ? "Connection closed" : (value.Up ? "The host closed the connection" : "The host turned the Steam connection down")));
				}
				PostDown(value, text);
			}
		}

		public override void Shutdown(string reason)
		{
			foreach (SteamLink item in new List<SteamLink>(_links.Values))
			{
				item.Close(reason);
			}
			object socket = _socket;
			object conn = _conn;
			_socket = null;
			_conn = null;
			Transport.Later(1.5f, delegate
			{
				try
				{
					(socket as SocketManager)?.Close();
				}
				catch
				{
				}
				try
				{
					(conn as ConnectionManager)?.Close();
				}
				catch
				{
				}
				SteamGlue.Forget(socket);
				SteamGlue.Forget(conn);
			});
		}
	}
}
