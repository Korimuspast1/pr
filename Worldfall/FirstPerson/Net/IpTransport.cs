using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace FirstPerson.Net
{
	internal sealed class IpTransport : Transport
	{
		private sealed class IpLink : NetLink
		{
			private readonly IpTransport _t;

			private readonly TcpClient _tcp;

			private readonly Stream _stream;

			private readonly Queue<byte[]> _out = new Queue<byte[]>();

			private long _queued;

			private volatile bool _open;

			private int _closing;

			private float _ping;

			public int Token;

			public volatile IPEndPoint UdpTo;

			public volatile bool UdpOk;

			public double HeardAt;

			public double PingedAt;

			public double UpAt;

			public bool DownPosted;

			private Timer _closer;

			public bool UdpUsable
			{
				get
				{
					if (_t._udp != null)
					{
						if (!_t._server)
						{
							return UdpOk;
						}
						return UdpTo != null;
					}
					return false;
				}
			}

			public override float PingMs => _ping;

			public override long Queued => Interlocked.Read(ref _queued);

			public override bool Open => _open;

			public IpLink(IpTransport t, TcpClient tcp)
			{
				_t = t;
				_tcp = tcp;
				_stream = tcp?.GetStream();
				_open = tcp != null;
				HeardAt = (UpAt = NowMs);
			}

			public void Pong(double sentAt)
			{
				float num = (float)(NowMs - sentAt);
				if (!(num < 0f) && !(num > 60000f))
				{
					_ping = ((_ping <= 0f) ? num : (_ping * 0.8f + num * 0.2f));
				}
			}

			public void StartThreads()
			{
				_t.Start("net-recv", RecvLoop);
				_t.Start("net-send", SendLoop);
			}

			public override void Send(byte[] data, int off, int len, Delivery how)
			{
				if (_open)
				{
					if (how == Delivery.Unreliable && len + 7 <= 1200 && UdpUsable)
					{
						byte[] array = new byte[len + 7];
						Udp(array, Token, 0);
						Buffer.BlockCopy(data, off, array, 7, len);
						_t.SendUdp(this, array, array.Length);
					}
					else if (how != Delivery.Unreliable || Queued <= 262144)
					{
						SendFrame(0, data, off, len);
					}
				}
			}

			public void SendFrame(byte kind, byte[] data, int off, int len)
			{
				byte[] array = new byte[5 + len];
				WriteI32(array, 0, len + 1);
				array[4] = kind;
				Buffer.BlockCopy(data, off, array, 5, len);
				lock (_out)
				{
					if (_closing == 0)
					{
						_out.Enqueue(array);
						Interlocked.Add(ref _queued, array.Length);
						Monitor.Pulse(_out);
					}
				}
			}

			private void SendLoop()
			{
				try
				{
					while (true)
					{
						byte[] array;
						lock (_out)
						{
							while (_out.Count == 0 && _closing == 0)
							{
								Monitor.Wait(_out, 500);
							}
							if (_out.Count == 0)
							{
								break;
							}
							array = _out.Dequeue();
						}
						_stream.Write(array, 0, array.Length);
						Interlocked.Add(ref _queued, -array.Length);
					}
				}
				catch (Exception ex)
				{
					Close("Connection lost (" + ex.Message + ")", tell: false);
				}
				finally
				{
					if (_closing != 0)
					{
						try
						{
							_tcp?.Client?.Shutdown(SocketShutdown.Send);
						}
						catch
						{
							try
							{
								_tcp?.Close();
							}
							catch
							{
							}
						}
					}
				}
			}

			private void RecvLoop()
			{
				byte[] b = new byte[4];
				try
				{
					while (_open)
					{
						if (!ReadFull(b, 4))
						{
							Close("Connection closed");
							break;
						}
						int num = ReadI32(b, 0);
						int num2 = ((!_t._server) ? 50331648 : ((Tag == null) ? 65536 : 4194304));
						if (num < 1 || num > num2)
						{
							Close("Bad data from the other side");
							break;
						}
						byte[] array = new byte[num];
						if (!ReadFull(array, num))
						{
							Close("Connection closed");
							break;
						}
						HeardAt = NowMs;
						switch (array[0])
						{
						case 0:
						{
							byte[] array2 = new byte[num - 1];
							Buffer.BlockCopy(array, 1, array2, 0, num - 1);
							_t.PostData(this, array2);
							break;
						}
						case 1:
							if (!_t._server)
							{
								if (num < 9)
								{
									Close("Bad hello");
									return;
								}
								Token = ReadI32(array, 1);
								int num3 = ReadI32(array, 5);
								if (num3 != 12)
								{
									Close((num3 > 12) ? "The host has a newer Worldfall" : "The host has an older Worldfall");
									return;
								}
								UpAt = NowMs;
								_t.PostUp(this);
							}
							break;
						case 2:
							if (!_t._server)
							{
								UdpOk = true;
							}
							break;
						case 3:
							SendFrame(4, array, 1, num - 1);
							break;
						case 4:
							if (num >= 9)
							{
								Pong(BitConverter.ToDouble(array, 1));
							}
							break;
						case 5:
							Close(MpPrefs.Clean(Encoding.UTF8.GetString(array, 1, Math.Min(num - 1, 1024)), 200), tell: false);
							return;
						}
					}
				}
				catch (Exception ex)
				{
					Close("Connection lost (" + ex.Message + ")");
				}
			}

			private bool ReadFull(byte[] b, int n)
			{
				int num;
				for (int i = 0; i < n; i += num)
				{
					num = _stream.Read(b, i, n - i);
					if (num <= 0)
					{
						return false;
					}
				}
				return true;
			}

			public override void Close(string reason)
			{
				Close(reason, tell: true);
			}

			private void Close(string reason, bool tell)
			{
				if (tell && _stream != null)
				{
					byte[] bytes = Encoding.UTF8.GetBytes(reason ?? "");
					byte[] array = new byte[5 + bytes.Length];
					WriteI32(array, 0, bytes.Length + 1);
					array[4] = 5;
					Buffer.BlockCopy(bytes, 0, array, 5, bytes.Length);
					lock (_out)
					{
						if (Interlocked.Exchange(ref _closing, 1) != 0)
						{
							return;
						}
						_open = false;
						_out.Clear();
						_out.Enqueue(array);
						Interlocked.Exchange(ref _queued, 0L);
						Monitor.PulseAll(_out);
					}
					_closer = new Timer(delegate
					{
						try
						{
							_tcp.Close();
						}
						catch
						{
						}
					}, null, 1000, -1);
				}
				else
				{
					if (Interlocked.Exchange(ref _closing, 1) != 0)
					{
						return;
					}
					_open = false;
					lock (_out)
					{
						Monitor.PulseAll(_out);
					}
					try
					{
						_tcp?.Close();
					}
					catch
					{
					}
				}
				lock (_t._links)
				{
					_t._byToken.Remove(Token);
				}
				DownPosted = true;
				_t.PostDown(this, reason);
			}
		}

		public const int DefaultPort = 27777;

		private const int MaxFrame = 50331648;

		private const int MaxFrameUnjoined = 65536;

		private const int MaxUnjoined = 8;

		private const int MaxUnjoinedPerAddress = 2;

		private const int MaxFrameJoined = 4194304;

		private const long MaxQueuedUnreliable = 262144L;

		private const int MaxDatagram = 1200;

		private const byte TData = 0;

		private const byte THello = 1;

		private const byte TUdpOk = 2;

		private const byte TPing = 3;

		private const byte TPong = 4;

		private const byte TBye = 5;

		private const byte UData = 0;

		private const byte UPunch = 1;

		private const byte UPing = 3;

		private const byte UPong = 4;

		private const byte ULan = 7;

		private const byte ULanReply = 8;

		private const byte M0 = 87;

		private const byte M1 = 70;

		private readonly bool _server;

		private readonly int _port;

		private TcpListener _listener;

		private UdpClient _udp;

		private IPEndPoint _hostUdp;

		private readonly List<IpLink> _links = new List<IpLink>();

		private readonly Dictionary<int, IpLink> _byToken = new Dictionary<int, IpLink>();

		private readonly Random _rng = new Random();

		private volatile bool _stopped;

		private static readonly Stopwatch Clock = Stopwatch.StartNew();

		public static bool LoopbackOnly;

		public static volatile byte[] LanInfo;

		private static double _lanAnsweredAt;

		private static int _lanAnswers;

		public const int LanPort = 27776;

		private UdpClient _lan;

		public override string Name
		{
			get
			{
				if (!_server)
				{
					return "IP";
				}
				return "IP " + _port;
			}
		}

		private static double NowMs => Clock.Elapsed.TotalMilliseconds;

		private IpTransport(bool server, int port)
		{
			_server = server;
			_port = port;
		}

		public static IpTransport Listen(int port)
		{
			IpTransport ipTransport = new IpTransport(server: true, port);
			IPAddress iPAddress = (LoopbackOnly ? IPAddress.Loopback : IPAddress.Any);
			ipTransport._listener = new TcpListener(iPAddress, port);
			ipTransport._listener.Start();
			try
			{
				ipTransport._udp = new UdpClient(new IPEndPoint(iPAddress, port));
				NoConnReset(ipTransport._udp);
			}
			catch (Exception ex)
			{
				Log.Info("NET udp port " + port + " not opened (" + ex.Message + "): unreliable messages go over TCP");
				ipTransport._udp = null;
			}
			ipTransport.Start("net-accept", ipTransport.AcceptLoop);
			if (ipTransport._udp != null)
			{
				ipTransport.Start("net-udp", ipTransport.UdpLoop);
			}
			ipTransport.OpenLanAnswers(iPAddress);
			ipTransport.Start("net-tick", ipTransport.TickLoop);
			Log.Info("NET listening on port " + port);
			return ipTransport;
		}

		public static IpTransport Connect(string host, int port)
		{
			IpTransport t = new IpTransport(server: false, port);
			t.Start("net-connect", delegate
			{
				t.ConnectLoop(host, port);
			});
			return t;
		}

		private void Start(string name, ThreadStart body)
		{
			Thread thread = new Thread((ThreadStart)delegate
			{
				try
				{
					body();
				}
				catch (Exception ex)
				{
					if (!_stopped)
					{
						Log.Error("NET " + name + ": " + ex);
					}
				}
			});
			thread.IsBackground = true;
			thread.Name = name;
			thread.Start();
		}

		private static void NoConnReset(UdpClient u)
		{
			try
			{
				u.Client.IOControl(-1744830452, new byte[4], null);
			}
			catch
			{
			}
		}

		private void AcceptLoop()
		{
			while (!_stopped)
			{
				TcpClient tcpClient;
				try
				{
					tcpClient = _listener.AcceptTcpClient();
				}
				catch
				{
					if (_stopped)
					{
						break;
					}
					Thread.Sleep(50);
					continue;
				}
				try
				{
					tcpClient.NoDelay = true;
					string text = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString();
					lock (_links)
					{
						int num = 0;
						int num2 = 0;
						foreach (IpLink link in _links)
						{
							if (link.Open && link.Tag == null)
							{
								num++;
								if (link.Address == text)
								{
									num2++;
								}
							}
						}
						if (num >= 8 || num2 >= 2)
						{
							try
							{
								tcpClient.Close();
							}
							catch
							{
							}
							continue;
						}
					}
					IpLink ipLink = new IpLink(this, tcpClient);
					ipLink.Address = text;
					lock (_links)
					{
						do
						{
							ipLink.Token = _rng.Next(1, int.MaxValue);
						}
						while (_byToken.ContainsKey(ipLink.Token));
						_links.Add(ipLink);
						_byToken[ipLink.Token] = ipLink;
					}
					byte[] array = new byte[8];
					WriteI32(array, 0, ipLink.Token);
					WriteI32(array, 4, 12);
					ipLink.SendFrame(1, array, 0, array.Length);
					ipLink.StartThreads();
					PostUp(ipLink);
				}
				catch (Exception ex)
				{
					Log.Error("NET accept: " + ex.Message);
					try
					{
						tcpClient.Close();
					}
					catch
					{
					}
				}
			}
		}

		private static string Shown(string host, int port)
		{
			return ((host.IndexOf(':') >= 0) ? ("[" + host + "]") : host) + ":" + port;
		}

		internal static string NoAnswer(string host, int port)
		{
			return "No answer from " + Shown(host, port) + ". The host must forward TCP and UDP port " + port + " on their router, or use Steam";
		}

		private void ConnectLoop(string host, int port)
		{
			TcpClient tcpClient = null;
			IpLink ipLink = null;
			try
			{
				IPAddress[] hostAddresses;
				try
				{
					hostAddresses = Dns.GetHostAddresses(host);
				}
				catch (Exception ex)
				{
					PostDown(new IpLink(this, null)
					{
						Address = host
					}, "Could not find " + host + ". Check the address (" + ex.Message + ")");
					return;
				}
				IPAddress iPAddress = ((hostAddresses.Length == 0) ? null : Array.Find(hostAddresses, (IPAddress a) => a.AddressFamily == AddressFamily.InterNetwork));
				if (iPAddress == null)
				{
					PostDown(new IpLink(this, null)
					{
						Address = host
					}, (hostAddresses.Length == 0) ? ("Could not find " + host + ". Check the address") : (Shown(host, port) + " is an IPv6 address. Hosts take IPv4 addresses only, ask for theirs or use Steam"));
					return;
				}
				tcpClient = new TcpClient(AddressFamily.InterNetwork);
				IAsyncResult asyncResult = tcpClient.BeginConnect(iPAddress, port, null, null);
				if (!asyncResult.AsyncWaitHandle.WaitOne(8000))
				{
					try
					{
						tcpClient.Close();
					}
					catch
					{
					}
					PostDown(new IpLink(this, null)
					{
						Address = host
					}, NoAnswer(host, port));
					return;
				}
				try
				{
					tcpClient.EndConnect(asyncResult);
				}
				catch (SocketException ex2)
				{
					try
					{
						tcpClient.Close();
					}
					catch
					{
					}
					string reason = ((ex2.SocketErrorCode == SocketError.ConnectionRefused) ? (Shown(host, port) + " refused the connection. Nobody hosts on that port, or the host turned Join by address off") : ((ex2.SocketErrorCode == SocketError.TimedOut) ? NoAnswer(host, port) : ((ex2.SocketErrorCode == SocketError.HostUnreachable || ex2.SocketErrorCode == SocketError.NetworkUnreachable) ? ("Could not reach " + Shown(host, port) + ". Check the address and your connection") : ("Could not connect to " + Shown(host, port) + " (" + ex2.Message + ")"))));
					PostDown(new IpLink(this, null)
					{
						Address = host
					}, reason);
					return;
				}
				if (!tcpClient.Connected)
				{
					try
					{
						tcpClient.Close();
					}
					catch
					{
					}
					PostDown(new IpLink(this, null)
					{
						Address = host
					}, NoAnswer(host, port));
					return;
				}
				tcpClient.NoDelay = true;
				ipLink = new IpLink(this, tcpClient)
				{
					Address = iPAddress.ToString()
				};
				lock (_links)
				{
					if (_stopped)
					{
						try
						{
							tcpClient.Close();
							return;
						}
						catch
						{
							return;
						}
					}
					_links.Add(ipLink);
				}
				_hostUdp = new IPEndPoint(iPAddress, port);
				try
				{
					UdpClient udpClient = new UdpClient(iPAddress.AddressFamily);
					udpClient.Client.Bind(new IPEndPoint((iPAddress.AddressFamily == AddressFamily.InterNetworkV6) ? IPAddress.IPv6Any : IPAddress.Any, 0));
					NoConnReset(udpClient);
					lock (_links)
					{
						if (_stopped)
						{
							try
							{
								udpClient.Close();
								return;
							}
							catch
							{
								return;
							}
						}
						_udp = udpClient;
					}
				}
				catch
				{
					_udp = null;
				}
				ipLink.StartThreads();
				if (_udp != null)
				{
					Start("net-udp", UdpLoop);
				}
				Start("net-tick", TickLoop);
			}
			catch (Exception ex3)
			{
				try
				{
					tcpClient?.Close();
				}
				catch
				{
				}
				PostDown(ipLink ?? new IpLink(this, null)
				{
					Address = host
				}, "Could not connect to " + Shown(host, port) + " (" + ex3.Message + ")");
			}
		}

		private void UdpLoop()
		{
			IPEndPoint iPEndPoint = new IPEndPoint(IPAddress.Any, 0);
			while (!_stopped)
			{
				IPEndPoint remoteEP = iPEndPoint;
				byte[] array;
				try
				{
					array = _udp.Receive(ref remoteEP);
				}
				catch
				{
					if (_stopped)
					{
						break;
					}
					Thread.Sleep(5);
					continue;
				}
				if (array.Length < 7 || array[0] != 87 || array[1] != 70)
				{
					continue;
				}
				int num = ReadI32(array, 2);
				byte b = array[6];
				if (_server && num == 0 && b == 7)
				{
					LanAnswer(remoteEP, _udp);
				}
				else
				{
					if (num == 0 || (!_server && (_hostUdp == null || !remoteEP.Address.Equals(_hostUdp.Address) || remoteEP.Port != _hostUdp.Port)))
					{
						continue;
					}
					IpLink value;
					lock (_links)
					{
						if (_server)
						{
							_byToken.TryGetValue(num, out value);
						}
						else
						{
							value = ((_links.Count > 0 && _links[0].Token == num) ? _links[0] : null);
						}
					}
					if (value == null || !value.Open)
					{
						continue;
					}
					value.HeardAt = NowMs;
					if (_server && (b == 1 || value.UdpTo == null || !value.UdpTo.Equals(remoteEP)))
					{
						bool num2 = value.UdpTo == null;
						value.UdpTo = remoteEP;
						if (num2 || b == 1)
						{
							value.SendFrame(2, new byte[0], 0, 0);
						}
					}
					switch (b)
					{
					case 0:
					{
						byte[] array2 = new byte[array.Length - 7];
						Buffer.BlockCopy(array, 7, array2, 0, array2.Length);
						PostData(value, array2);
						break;
					}
					case 3:
						array[6] = 4;
						SendUdp(value, array, array.Length);
						break;
					case 4:
						if (array.Length >= 15)
						{
							value.Pong(BitConverter.ToDouble(array, 7));
						}
						break;
					}
				}
			}
		}

		public static byte[] LanAsk()
		{
			byte[] array = new byte[96];
			Udp(array, 0, 7);
			return array;
		}

		private void LanAnswer(IPEndPoint from, UdpClient via)
		{
			byte[] lanInfo = LanInfo;
			if (lanInfo == null || !JoinCode.Private(from.Address.ToString()) || from.Address.AddressFamily != AddressFamily.InterNetwork)
			{
				return;
			}
			double nowMs = NowMs;
			if (nowMs - _lanAnsweredAt > 1000.0)
			{
				_lanAnsweredAt = nowMs;
				_lanAnswers = 0;
			}
			if (++_lanAnswers > 40)
			{
				return;
			}
			byte[] array = new byte[9 + lanInfo.Length];
			Udp(array, 0, 8);
			array[7] = (byte)_port;
			array[8] = (byte)(_port >> 8);
			Buffer.BlockCopy(lanInfo, 0, array, 9, lanInfo.Length);
			try
			{
				via.Send(array, array.Length, from);
			}
			catch
			{
			}
		}

		private void OpenLanAnswers(IPAddress on)
		{
			try
			{
				UdpClient udpClient = new UdpClient();
				udpClient.ExclusiveAddressUse = false;
				udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true);
				udpClient.Client.Bind(new IPEndPoint(on, 27776));
				NoConnReset(udpClient);
				_lan = udpClient;
				Start("net-lan-answer", LanLoop);
			}
			catch (Exception ex)
			{
				Log.Info("NET same-network answers only on the game port (" + 27776 + ": " + ex.Message + ")");
			}
		}

		private void LanLoop()
		{
			IPEndPoint iPEndPoint = new IPEndPoint(IPAddress.Any, 0);
			while (!_stopped)
			{
				IPEndPoint remoteEP = iPEndPoint;
				byte[] array;
				try
				{
					array = _lan.Receive(ref remoteEP);
				}
				catch
				{
					if (_stopped)
					{
						break;
					}
					Thread.Sleep(5);
					continue;
				}
				if (array.Length >= 7 && array[0] == 87 && array[1] == 70 && ReadI32(array, 2) == 0 && array[6] == 7)
				{
					LanAnswer(remoteEP, _lan);
				}
			}
		}

		public static bool ReadLanReply(byte[] p, out int port, out ServerInfo info)
		{
			port = 0;
			info = null;
			if (p == null || p.Length < 10 || p[0] != 87 || p[1] != 70 || p[6] != 8 || ReadI32(p, 2) != 0)
			{
				return false;
			}
			port = p[7] | (p[8] << 8);
			try
			{
				info = ServerInfo.Unpack(new NetReader(p, 9, p.Length - 9));
			}
			catch
			{
				return false;
			}
			return port > 0;
		}

		private void TickLoop()
		{
			double num = 0.0;
			while (!_stopped)
			{
				Thread.Sleep(250);
				double nowMs = NowMs;
				List<IpLink> list;
				lock (_links)
				{
					list = new List<IpLink>(_links);
				}
				foreach (IpLink item in list)
				{
					if (!item.Open)
					{
						continue;
					}
					if (nowMs - item.HeardAt > 20000.0)
					{
						item.Close((!_server && item.Token == 0) ? ("No Worldfall host answered at " + Shown(item.Address, _port) + ". Check the address and port") : "Connection timed out");
						continue;
					}
					if (nowMs - item.PingedAt >= 1000.0)
					{
						item.PingedAt = nowMs;
						byte[] bytes = BitConverter.GetBytes(nowMs);
						if (item.UdpUsable)
						{
							byte[] array = new byte[15];
							Udp(array, item.Token, 3);
							Buffer.BlockCopy(bytes, 0, array, 7, 8);
							SendUdp(item, array, array.Length);
						}
						else
						{
							item.SendFrame(3, bytes, 0, bytes.Length);
						}
					}
					if (!_server && _udp != null && !item.UdpUsable && item.Token != 0 && nowMs - num > 500.0 && nowMs - item.UpAt < 10000.0)
					{
						num = nowMs;
						byte[] array2 = new byte[7];
						Udp(array2, item.Token, 1);
						SendUdp(item, array2, array2.Length);
					}
				}
				lock (_links)
				{
					_links.RemoveAll((IpLink x) => !x.Open && x.DownPosted);
				}
			}
		}

		private static void Udp(byte[] p, int token, byte kind)
		{
			p[0] = 87;
			p[1] = 70;
			WriteI32(p, 2, token);
			p[6] = kind;
		}

		private void SendUdp(IpLink l, byte[] p, int n)
		{
			try
			{
				if (_udp != null)
				{
					IPEndPoint iPEndPoint = (_server ? l.UdpTo : _hostUdp);
					if (iPEndPoint != null)
					{
						_udp.Send(p, n, iPEndPoint);
					}
				}
			}
			catch
			{
			}
		}

		internal static string HostPort(string ip, int port)
		{
			return ((ip.IndexOf(':') >= 0 && !ip.StartsWith("[", StringComparison.Ordinal)) ? ("[" + ip + "]") : ip) + ":" + port;
		}

		public static List<string> LocalAddresses()
		{
			List<string> list = new List<string>();
			try
			{
				NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
				foreach (NetworkInterface networkInterface in allNetworkInterfaces)
				{
					if (networkInterface.OperationalStatus != OperationalStatus.Up)
					{
						continue;
					}
					NetworkInterfaceType networkInterfaceType = networkInterface.NetworkInterfaceType;
					if (networkInterfaceType == NetworkInterfaceType.Loopback || networkInterfaceType == NetworkInterfaceType.Tunnel)
					{
						continue;
					}
					foreach (UnicastIPAddressInformation unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
					{
						IPAddress address = unicastAddress.Address;
						if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
						{
							continue;
						}
						string text = address.ToString();
						if (!text.StartsWith("169.254.", StringComparison.Ordinal) && !list.Contains(text))
						{
							if (text.StartsWith("192.168.", StringComparison.Ordinal) || text.StartsWith("10.", StringComparison.Ordinal) || IsPrivate172(text))
							{
								list.Insert(0, text);
							}
							else
							{
								list.Add(text);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Info("NET listing this PC's addresses: " + ex.Message);
			}
			if (list.Count == 0)
			{
				try
				{
					IPAddress[] hostAddresses = Dns.GetHostAddresses(Dns.GetHostName());
					foreach (IPAddress iPAddress in hostAddresses)
					{
						if (iPAddress.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(iPAddress) && !list.Contains(iPAddress.ToString()))
						{
							list.Add(iPAddress.ToString());
						}
					}
				}
				catch
				{
				}
			}
			return list;
		}

		private static bool IsPrivate172(string ip)
		{
			if (!ip.StartsWith("172.", StringComparison.Ordinal))
			{
				return false;
			}
			string[] array = ip.Split('.');
			if (array.Length > 1 && int.TryParse(array[1], out var result) && result >= 16)
			{
				return result <= 31;
			}
			return false;
		}

		public override void Shutdown(string reason)
		{
			List<IpLink> list;
			lock (_links)
			{
				_stopped = true;
				list = new List<IpLink>(_links);
			}
			foreach (IpLink item in list)
			{
				item.Close(reason);
			}
			try
			{
				_listener?.Stop();
			}
			catch
			{
			}
			try
			{
				_udp?.Close();
			}
			catch
			{
			}
			try
			{
				_lan?.Close();
			}
			catch
			{
			}
		}

		private static void WriteI32(byte[] b, int at, int v)
		{
			b[at] = (byte)v;
			b[at + 1] = (byte)(v >> 8);
			b[at + 2] = (byte)(v >> 16);
			b[at + 3] = (byte)(v >> 24);
		}

		private static int ReadI32(byte[] b, int at)
		{
			return b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24);
		}
	}
}
