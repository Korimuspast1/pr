using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace FirstPerson.Net
{
	internal static class LanFind
	{
		internal sealed class Hit
		{
			public string Address;

			public ServerInfo Info;
		}

		private static readonly List<Hit> _hits = new List<Hit>();

		private static volatile bool _busy;

		public static bool Busy => _busy;

		public static List<Hit> Hits
		{
			get
			{
				lock (_hits)
				{
					return new List<Hit>(_hits);
				}
			}
		}

		public static void Search(float seconds = 1.2f, int alsoPort = 0)
		{
			if (_busy)
			{
				return;
			}
			_busy = true;
			lock (_hits)
			{
				_hits.Clear();
			}
			List<int> ports = new List<int> { 27776, 27777 };
			int port = MpPrefs.Current.Port;
			if (port > 0 && !ports.Contains(port))
			{
				ports.Add(port);
			}
			if (alsoPort > 0 && !ports.Contains(alsoPort))
			{
				ports.Add(alsoPort);
			}
			Thread thread = new Thread((ThreadStart)delegate
			{
				try
				{
					Run(ports, seconds);
				}
				catch (Exception ex)
				{
					Log.Info("NET lan search: " + ex.Message);
				}
				finally
				{
					_busy = false;
				}
			});
			thread.IsBackground = true;
			thread.Name = "net-lan";
			thread.Start();
		}

		private static void Run(List<int> ports, float seconds)
		{
			using (UdpClient udpClient = new UdpClient(AddressFamily.InterNetwork))
			{
				udpClient.EnableBroadcast = true;
				udpClient.Client.ReceiveTimeout = 200;
				byte[] array = IpTransport.LanAsk();
				List<IPAddress> list = new List<IPAddress>
				{
					IPAddress.Broadcast,
					IPAddress.Loopback
				};
				foreach (IPAddress item in DirectedBroadcasts())
				{
					if (!list.Contains(item))
					{
						list.Add(item);
					}
				}
				for (int i = 0; i < 2; i++)
				{
					foreach (IPAddress item2 in list)
					{
						foreach (int port2 in ports)
						{
							try
							{
								udpClient.Send(array, array.Length, new IPEndPoint(item2, port2));
							}
							catch
							{
							}
						}
					}
				}
				DateTime dateTime = DateTime.UtcNow.AddSeconds(seconds);
				while (DateTime.UtcNow < dateTime)
				{
					IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
					byte[] p;
					try
					{
						p = udpClient.Receive(ref remoteEP);
					}
					catch (SocketException)
					{
						continue;
					}
					if (!IpTransport.ReadLanReply(p, out var port, out var info))
					{
						continue;
					}
					bool flag = IPAddress.IsLoopback(remoteEP.Address);
					string address = remoteEP.Address?.ToString() + ":" + port;
					lock (_hits)
					{
						int num = _hits.FindIndex((Hit h) => h.Address == address || (info.Code != "" && h.Info.Code == info.Code && h.Address.EndsWith(":" + port, StringComparison.Ordinal)));
						if (num < 0 || (!flag && _hits[num].Address.StartsWith("127.", StringComparison.Ordinal)))
						{
							if (num >= 0)
							{
								_hits.RemoveAt(num);
							}
							info.Address = address;
							info.Source = "Same network";
							_hits.Add(new Hit
							{
								Address = address,
								Info = info
							});
						}
					}
				}
			}
		}

		private static List<IPAddress> DirectedBroadcasts()
		{
			List<IPAddress> list = new List<IPAddress>();
			try
			{
				NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
				foreach (NetworkInterface networkInterface in allNetworkInterfaces)
				{
					if (networkInterface.OperationalStatus != OperationalStatus.Up || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
					{
						continue;
					}
					foreach (UnicastIPAddressInformation unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
					{
						if (unicastAddress.Address.AddressFamily != AddressFamily.InterNetwork)
						{
							continue;
						}
						IPAddress iPv4Mask;
						try
						{
							iPv4Mask = unicastAddress.IPv4Mask;
						}
						catch
						{
							continue;
						}
						if (iPv4Mask == null)
						{
							continue;
						}
						byte[] addressBytes = unicastAddress.Address.GetAddressBytes();
						byte[] addressBytes2 = iPv4Mask.GetAddressBytes();
						if (addressBytes2[0] != 0)
						{
							for (int j = 0; j < 4; j++)
							{
								addressBytes[j] = (byte)(addressBytes[j] | ~addressBytes2[j]);
							}
							list.Add(new IPAddress(addressBytes));
						}
					}
				}
			}
			catch
			{
			}
			return list;
		}
	}
}
