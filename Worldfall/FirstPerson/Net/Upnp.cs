using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace FirstPerson.Net
{
	internal static class Upnp
	{
		public enum Phase
		{
			Off,
			Working,
			Open,
			Failed
		}

		public static volatile Phase State = Phase.Off;

		public static volatile string PublicIp = "";

		public static volatile string Note = "";

		public static volatile bool CarrierNat;

		private static readonly object Gate = new object();

		private static string _control;

		private static string _service;

		private static string _localIp;

		private static int _port;

		private static int _generation;

		private static bool _tcp;

		private static bool _udp;

		private static int _lease;

		private static readonly string[] StunServers = new string[3] { "stun.l.google.com:19302", "stun.cloudflare.com:3478", "stun1.l.google.com:19302" };

		public static void Open(int port)
		{
			int gen;
			lock (Gate)
			{
				gen = ++_generation;
				_port = port;
			}
			State = Phase.Working;
			PublicIp = "";
			Note = "";
			CarrierNat = false;
			bool flag = false;
			try
			{
				flag = Application.internetReachability == NetworkReachability.ReachableViaCarrierDataNetwork;
			}
			catch
			{
			}
			if (flag)
			{
				State = Phase.Failed;
				CarrierNat = true;
				Note = "On mobile data nobody outside can reach this phone. Host on Wi-Fi";
				Log.Info("NET upnp: on mobile data, not asked");
				return;
			}
			Thread thread = new Thread((ThreadStart)delegate
			{
				try
				{
					Run(gen, port);
				}
				catch (Exception ex)
				{
					Log.Info("NET upnp: " + ex.Message);
					if (gen == _generation)
					{
						State = Phase.Failed;
						Note = "The router didn't answer";
					}
				}
			});
			thread.IsBackground = true;
			thread.Name = "net-upnp";
			thread.Start();
		}

		public static void Close(bool wait)
		{
			string control;
			string service;
			int port;
			bool tcp;
			bool udp;
			lock (Gate)
			{
				_generation++;
				control = _control;
				service = _service;
				_ = _localIp;
				port = _port;
				tcp = _tcp;
				udp = _udp;
				_tcp = (_udp = false);
			}
			State = Phase.Off;
			PublicIp = "";
			Note = "";
			if (control == null || (!tcp && !udp))
			{
				return;
			}
			Thread thread = new Thread((ThreadStart)delegate
			{
				try
				{
					if (tcp)
					{
						Soap(control, service, "DeletePortMapping", Remove(port, "TCP"), 1000);
					}
				}
				catch
				{
				}
				try
				{
					if (udp)
					{
						Soap(control, service, "DeletePortMapping", Remove(port, "UDP"), 1000);
					}
				}
				catch
				{
				}
				Log.Info("NET upnp: port " + port + " closed on the router");
			})
			{
				IsBackground = true,
				Name = "net-upnp-close"
			};
			thread.Start();
			if (wait)
			{
				thread.Join(1500);
			}
		}

		private static void Run(int gen, int port)
		{
			string outside = "";
			string text = Map(gen, port, ref outside);
			if (gen != _generation)
			{
				return;
			}
			if (text == null && !JoinCode.Private(outside))
			{
				PublicIp = outside;
				State = Phase.Open;
				Note = "";
				Log.Info("NET upnp: port " + port + " open on the router, public address found");
				KeepLease(gen, port);
				return;
			}
			string text2 = Stun();
			if (gen != _generation)
			{
				return;
			}
			if (text == null)
			{
				CarrierNat = outside.StartsWith("100.", StringComparison.Ordinal) && !JoinCode.Private(text2 ?? "");
				State = Phase.Failed;
				Note = (CarrierNat ? "Your internet provider shares your address with others. Players outside your network can join only through Steam" : ("Port opened on your router, but another router sits in front of it. Forward port " + port + " there too, or use Steam"));
				if (!string.IsNullOrEmpty(text2))
				{
					PublicIp = text2;
				}
				Log.Info("NET upnp: router outside address is private (" + (CarrierNat ? "carrier NAT" : "double NAT") + ")");
				KeepLease(gen, port);
			}
			else
			{
				State = Phase.Failed;
				if (!string.IsNullOrEmpty(text2))
				{
					PublicIp = text2;
				}
				Note = (text.EndsWith("Pick another port", StringComparison.Ordinal) ? (text + ", or use Steam") : (text + ". Forward TCP and UDP port " + port + " on your router by hand, or use Steam"));
				Log.Info("NET upnp: " + text + (string.IsNullOrEmpty(text2) ? ", no public address" : ", public address from STUN"));
			}
		}

		private static string Map(int gen, int port, ref string outside)
		{
			List<string> list = Discover();
			if (list.Count == 0)
			{
				return "Your router didn't answer (UPnP may be off on it)";
			}
			foreach (string item in list)
			{
				if (gen != _generation)
				{
					return "stopped";
				}
				string control;
				string service;
				try
				{
					if (!FindService(item, out control, out service))
					{
						continue;
					}
				}
				catch (Exception ex)
				{
					Log.Info("NET upnp: " + item + ": " + ex.Message);
					continue;
				}
				string text = LocalIpToward(new Uri(item).Host);
				if (text == null)
				{
					continue;
				}
				string error;
				bool flag = Add(control, service, port, "TCP", text, out error);
				string error2;
				bool flag2 = Add(control, service, port, "UDP", text, out error2);
				bool flag3;
				lock (Gate)
				{
					flag3 = gen != _generation;
					if (!flag3)
					{
						_control = control;
						_service = service;
						_localIp = text;
						_tcp = flag;
						_udp = flag2;
					}
				}
				if (flag3)
				{
					try
					{
						if (flag)
						{
							Soap(control, service, "DeletePortMapping", Remove(port, "TCP"), 1000);
						}
					}
					catch
					{
					}
					try
					{
						if (flag2)
						{
							Soap(control, service, "DeletePortMapping", Remove(port, "UDP"), 1000);
						}
					}
					catch
					{
					}
					return "stopped";
				}
				try
				{
					Match match = Regex.Match(Soap(control, service, "GetExternalIPAddress", "", 3000), "<NewExternalIPAddress>\\s*([0-9.]+)\\s*<");
					if (match.Success)
					{
						outside = match.Groups[1].Value;
					}
				}
				catch (Exception ex2)
				{
					Log.Info("NET upnp: outside address: " + ex2.Message);
				}
				if (!flag && !flag2)
				{
					string text2 = error ?? error2 ?? "";
					return text2.Contains("718") ? ("Another device on your network already uses port " + port + ". Pick another port") : ("Your router refused to open the port" + ((text2 != "") ? (" (" + text2 + ")") : ""));
				}
				return null;
			}
			return "Your router answered but has no port forwarding service";
		}

		private static bool Add(string control, string service, int port, string proto, string local, out string error)
		{
			error = null;
			int[] array = new int[2] { 0, 3600 };
			for (int i = 0; i < array.Length; i++)
			{
				int lease = array[i];
				try
				{
					Soap(control, service, "AddPortMapping", "<NewRemoteHost></NewRemoteHost><NewExternalPort>" + port + "</NewExternalPort><NewProtocol>" + proto + "</NewProtocol><NewInternalPort>" + port + "</NewInternalPort><NewInternalClient>" + local + "</NewInternalClient><NewEnabled>1</NewEnabled><NewPortMappingDescription>Worldfall</NewPortMappingDescription><NewLeaseDuration>" + lease + "</NewLeaseDuration>", 3000);
					lock (Gate)
					{
						_lease = lease;
					}
					return true;
				}
				catch (Exception ex)
				{
					error = ex.Message;
				}
			}
			return false;
		}

		private static void KeepLease(int gen, int port)
		{
			int lease;
			string control;
			string service;
			string localIp;
			lock (Gate)
			{
				lease = _lease;
				control = _control;
				service = _service;
				localIp = _localIp;
			}
			if (lease <= 0 || control == null)
			{
				return;
			}
			while (true)
			{
				for (int i = 0; i < lease / 2; i++)
				{
					Thread.Sleep(1000);
					if (gen != _generation)
					{
						return;
					}
				}
				Add(control, service, port, "TCP", localIp, out var error);
				Add(control, service, port, "UDP", localIp, out error);
			}
		}

		private static string Remove(int port, string proto)
		{
			return "<NewRemoteHost></NewRemoteHost><NewExternalPort>" + port + "</NewExternalPort><NewProtocol>" + proto + "</NewProtocol>";
		}

		private static List<string> Discover()
		{
			List<string> list = new List<string>();
			string[] array = new string[3] { "urn:schemas-upnp-org:device:InternetGatewayDevice:1", "urn:schemas-upnp-org:device:InternetGatewayDevice:2", "urn:schemas-upnp-org:service:WANIPConnection:1" };
			using (UdpClient udpClient = new UdpClient(AddressFamily.InterNetwork))
			{
				udpClient.Client.ReceiveTimeout = 400;
				IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);
				for (int i = 0; i < 2; i++)
				{
					if (list.Count != 0)
					{
						break;
					}
					string[] array2 = array;
					foreach (string text in array2)
					{
						byte[] bytes = Encoding.ASCII.GetBytes("M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nST: " + text + "\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\n\r\n");
						try
						{
							udpClient.Send(bytes, bytes.Length, endPoint);
						}
						catch
						{
						}
					}
					DateTime dateTime = DateTime.UtcNow.AddSeconds(2.5);
					while (DateTime.UtcNow < dateTime)
					{
						try
						{
							IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
							Match match = Regex.Match(Encoding.ASCII.GetString(udpClient.Receive(ref remoteEP)), "^LOCATION:\\s*(\\S+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
							if (match.Success && !list.Contains(match.Groups[1].Value))
							{
								list.Add(match.Groups[1].Value);
							}
						}
						catch (SocketException)
						{
						}
					}
				}
			}
			return list;
		}

		private static bool FindService(string location, out string control, out string service)
		{
			control = (service = null);
			string text = Get(location, 3000);
			foreach (Match item in Regex.Matches(text, "<service>(.*?)</service>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
			{
				string value = item.Groups[1].Value;
				string text2 = Tag(value, "serviceType");
				if (text2 != null && (text2.IndexOf("WANIPConnection", StringComparison.Ordinal) >= 0 || text2.IndexOf("WANPPPConnection", StringComparison.Ordinal) >= 0))
				{
					string text3 = Tag(value, "controlURL");
					if (text3 != null)
					{
						string text4 = Tag(text, "URLBase");
						control = new Uri(new Uri(string.IsNullOrEmpty(text4) ? location : text4), text3).ToString();
						service = text2;
						return true;
					}
				}
			}
			return false;
		}

		private static string Tag(string xml, string name)
		{
			Match match = Regex.Match(xml, "<" + name + ">\\s*(.*?)\\s*</" + name + ">", RegexOptions.IgnoreCase | RegexOptions.Singleline);
			if (!match.Success)
			{
				return null;
			}
			return match.Groups[1].Value;
		}

		private static string LocalIpToward(string router)
		{
			try
			{
				using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
				{
					socket.Connect(router, 1900);
					return ((IPEndPoint)socket.LocalEndPoint).Address.ToString();
				}
			}
			catch
			{
				return null;
			}
		}

		private static string Get(string url, int timeout)
		{
			HttpWebRequest obj = (HttpWebRequest)WebRequest.Create(url);
			int timeout2 = (obj.ReadWriteTimeout = timeout);
			obj.Timeout = timeout2;
			obj.Proxy = null;
			using (HttpWebResponse httpWebResponse = (HttpWebResponse)obj.GetResponse())
			{
				using (StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream()))
				{
					return streamReader.ReadToEnd();
				}
			}
		}

		private static string Soap(string control, string service, string action, string args, int timeout)
		{
			string s = "<?xml version=\"1.0\"?>\r\n<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\"><s:Body><u:" + action + " xmlns:u=\"" + service + "\">" + args + "</u:" + action + "></s:Body></s:Envelope>";
			byte[] bytes = Encoding.UTF8.GetBytes(s);
			HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(control);
			httpWebRequest.Method = "POST";
			int timeout2 = (httpWebRequest.ReadWriteTimeout = timeout);
			httpWebRequest.Timeout = timeout2;
			httpWebRequest.Proxy = null;
			httpWebRequest.ContentType = "text/xml; charset=\"utf-8\"";
			httpWebRequest.Headers.Add("SOAPACTION", "\"" + service + "#" + action + "\"");
			httpWebRequest.ContentLength = bytes.Length;
			using (Stream stream = httpWebRequest.GetRequestStream())
			{
				stream.Write(bytes, 0, bytes.Length);
			}
			try
			{
				using (HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse())
				{
					using (StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream()))
					{
						return streamReader.ReadToEnd();
					}
				}
			}
			catch (WebException ex) when (ex.Response != null)
			{
				string input;
				using (StreamReader streamReader2 = new StreamReader(ex.Response.GetResponseStream()))
				{
					input = streamReader2.ReadToEnd();
				}
				Match match = Regex.Match(input, "<errorCode>\\s*(\\d+)", RegexOptions.IgnoreCase);
				Match match2 = Regex.Match(input, "<errorDescription>\\s*(.*?)\\s*<", RegexOptions.IgnoreCase);
				throw new IOException((match.Success ? match.Groups[1].Value : "error") + (match2.Success ? (" " + match2.Groups[1].Value) : ""));
			}
		}

		private static string Stun()
		{
			string[] stunServers = StunServers;
			foreach (string text in stunServers)
			{
				try
				{
					string[] array = text.Split(':');
					IPAddress iPAddress = null;
					IPAddress[] hostAddresses = Dns.GetHostAddresses(array[0]);
					foreach (IPAddress iPAddress2 in hostAddresses)
					{
						if (iPAddress2.AddressFamily == AddressFamily.InterNetwork)
						{
							iPAddress = iPAddress2;
							break;
						}
					}
					if (iPAddress == null)
					{
						continue;
					}
					using (UdpClient udpClient = new UdpClient(AddressFamily.InterNetwork))
					{
						udpClient.Client.ReceiveTimeout = 1500;
						byte[] array2 = new byte[20]
						{
							0, 1, 0, 0, 33, 18, 164, 66, 0, 0,
							0, 0, 0, 0, 0, 0, 0, 0, 0, 0
						};
						byte[] array3 = new byte[12];
						new System.Random().NextBytes(array3);
						Buffer.BlockCopy(array3, 0, array2, 8, 12);
						udpClient.Send(array2, array2.Length, new IPEndPoint(iPAddress, int.Parse(array[1])));
						IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
						string text2 = StunAddress(udpClient.Receive(ref remoteEP));
						if (text2 != null)
						{
							return text2;
						}
					}
				}
				catch
				{
				}
			}
			return null;
		}

		private static string StunAddress(byte[] r)
		{
			int num = 20;
			while (num + 4 <= r.Length)
			{
				int num2 = (r[num] << 8) | r[num + 1];
				int num3 = (r[num + 2] << 8) | r[num + 3];
				int num4 = num + 4;
				if (num4 + num3 > r.Length)
				{
					break;
				}
				if ((num2 == 32 || num2 == 1) && num3 >= 8 && r[num4 + 1] == 1)
				{
					byte[] array = new byte[4]
					{
						r[num4 + 4],
						r[num4 + 5],
						r[num4 + 6],
						r[num4 + 7]
					};
					if (num2 == 32)
					{
						array[0] ^= 33;
						array[1] ^= 18;
						array[2] ^= 164;
						array[3] ^= 66;
					}
					return array[0] + "." + array[1] + "." + array[2] + "." + array[3];
				}
				num = num4 + ((num3 + 3) & -4);
			}
			return null;
		}
	}
}
