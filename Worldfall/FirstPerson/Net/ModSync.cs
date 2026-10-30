using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Steamworks.Ugc;
using UnityEngine;

namespace FirstPerson.Net
{
	internal static class ModSync
	{
		internal sealed class Mod
		{
			public string Id = "";

			public string Name = "";

			public string Version = "";

			public string Hash = "";

			public ulong Workshop;

			public long Size;

			public string Folder;

			public bool Dll;

			public void Pack(NetWriter w)
			{
				w.Str(Id);
				w.Str(Name);
				w.Str(Version);
				w.Str(Hash);
				w.VarU(Workshop);
				w.VarU((ulong)Size);
				w.Bool(Dll);
			}

			public static Mod Unpack(NetReader r)
			{
				return new Mod
				{
					Id = (r.Str() ?? ""),
					Name = (r.Str() ?? ""),
					Version = (r.Str() ?? ""),
					Hash = (r.Str() ?? ""),
					Workshop = r.VarU(),
					Size = (long)r.VarU(),
					Dll = r.Bool()
				};
			}
		}

		private sealed class Sending
		{
			public NetPlayer P;

			public Mod M;

			public int Next;
		}

		private const long MaxSize = 314572800L;

		private const int MaxFiles = 4000;

		private static string _dataPath;

		private static string _streamingPath;

		private static List<Mod> _installed;

		private static Task<List<Mod>> _listing;

		private static readonly Dictionary<string, Task<byte[]>> _packs = new Dictionary<string, Task<byte[]>>();

		private static readonly List<Sending> _sending = new List<Sending>();

		private const int Piece = 49152;

		public static readonly List<Mod> Missing = new List<Mod>();

		public static bool Asking;

		public static bool TrustHost;

		public static string Progress = "";

		public static string Note = "";

		private static readonly Dictionary<string, List<byte[]>> _incoming = new Dictionary<string, List<byte[]>>();

		private static int _waitingFiles;

		private static int _waitingWorkshop;

		private static int _gen;

		private static string GameRoot => Path.GetDirectoryName(_dataPath ?? (_dataPath = Application.dataPath));

		private static string ModsFolder => Path.Combine(GameRoot, "Mods");

		private static string DllFolder => Path.Combine(_streamingPath ?? (_streamingPath = Application.streamingAssetsPath), "mods");

		private static string WorkshopFolder
		{
			get
			{
				string directoryName = Path.GetDirectoryName(GameRoot);
				string text = ((directoryName != null) ? Path.GetDirectoryName(directoryName) : null);
				if (text == null)
				{
					return "";
				}
				return Path.Combine(Path.Combine(Path.Combine(text, "workshop"), "content"), "1206560");
			}
		}

		public static bool PromptShown
		{
			get
			{
				if (Asking)
				{
					return NetSession.Joined;
				}
				return false;
			}
		}

		public static bool FetchingWorkshop => _waitingWorkshop > 0;

		public static bool FetchingFiles => _waitingFiles > 0;

		public static void Prepare()
		{
			if (_installed == null && _listing == null)
			{
				_dataPath = Application.dataPath;
				_streamingPath = Application.streamingAssetsPath;
				_listing = Task.Run(() => List());
			}
		}

		public static List<Mod> Installed()
		{
			if (_installed != null)
			{
				return _installed;
			}
			Prepare();
			try
			{
				_installed = _listing.Result;
			}
			catch (Exception ex)
			{
				Log.Error("NET listing mods: " + ex.Message);
				_installed = new List<Mod>();
			}
			return _installed;
		}

		private static List<Mod> List()
		{
			List<Mod> list = new List<Mod>();
			try
			{
				if (Directory.Exists(ModsFolder))
				{
					string[] directories = Directory.GetDirectories(ModsFolder);
					foreach (string dir in directories)
					{
						AddFolder(list, dir, 0uL);
					}
				}
				if (Directory.Exists(WorkshopFolder))
				{
					string[] directories = Directory.GetDirectories(WorkshopFolder);
					foreach (string text in directories)
					{
						if (ulong.TryParse(Path.GetFileName(text), out var result))
						{
							AddFolder(list, text, result);
						}
					}
				}
				if (Directory.Exists(DllFolder))
				{
					string[] directories = Directory.GetFiles(DllFolder, "*.dll");
					foreach (string text2 in directories)
					{
						string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text2);
						switch (fileNameWithoutExtension)
						{
						case "FirstPerson":
						case "Worldfall":
						case "NeoModLoader":
							continue;
						}
						FileInfo fileInfo = new FileInfo(text2);
						list.Add(new Mod
						{
							Id = "dll:" + fileNameWithoutExtension,
							Name = fileNameWithoutExtension,
							Hash = HashFiles(new string[1] { text2 }, Path.GetDirectoryName(text2)),
							Size = fileInfo.Length,
							Folder = text2,
							Dll = true
						});
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET listing mods: " + ex.Message);
			}
			return list;
		}

		private static void AddFolder(List<Mod> list, string dir, ulong workshop)
		{
			string path = Path.Combine(dir, "mod.json");
			if (!File.Exists(path))
			{
				return;
			}
			string json = File.ReadAllText(path);
			string text = JsonField(json, "GUID");
			string text2 = JsonField(json, "name");
			string text3 = JsonField(json, "version");
			if (!(text == "s3cond2.worldfall") && !(text2 == "Worldfall"))
			{
				if (string.IsNullOrEmpty(text))
				{
					text = text2 ?? Path.GetFileName(dir);
				}
				string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
				long num = 0L;
				string[] array = files;
				foreach (string fileName in array)
				{
					num += new FileInfo(fileName).Length;
				}
				list.Add(new Mod
				{
					Id = text,
					Name = (text2 ?? text),
					Version = (text3 ?? ""),
					Workshop = workshop,
					Folder = dir,
					Size = num,
					Hash = HashFiles(files, dir)
				});
			}
		}

		private static string JsonField(string json, string field)
		{
			try
			{
				return (string)JObject.Parse(json)[field];
			}
			catch
			{
				return null;
			}
		}

		private static string HashFiles(string[] files, string root)
		{
			Array.Sort(files, StringComparer.Ordinal);
			using (SHA1 sHA = SHA1.Create())
			{
				foreach (string text in files)
				{
					byte[] bytes = Encoding.UTF8.GetBytes(Relative(root, text).Replace('\\', '/'));
					sHA.TransformBlock(bytes, 0, bytes.Length, null, 0);
					byte[] array = File.ReadAllBytes(text);
					sHA.TransformBlock(array, 0, array.Length, null, 0);
				}
				sHA.TransformFinalBlock(new byte[0], 0, 0);
				return Convert.ToBase64String(sHA.Hash).Substring(0, 16);
			}
		}

		private static string Relative(string root, string file)
		{
			return file.Substring(root.Length).TrimStart('\\', '/');
		}

		public static void SendList(NetPlayer p)
		{
			NetWriter netWriter = new NetWriter(1024);
			netWriter.VarU(8uL);
			List<Mod> list = Installed();
			netWriter.VarU((ulong)list.Count);
			foreach (Mod item in list)
			{
				item.Pack(netWriter);
			}
			p.Link.Send(netWriter, Delivery.Reliable);
		}

		public static void SendFiles(NetPlayer p, NetReader r)
		{
			int num = r.Count(64);
			for (int i = 0; i < num; i++)
			{
				string id = r.Str();
				Mod m = Installed().Find((Mod x) => x.Id == id && x.Workshop == 0);
				if (m == null || _sending.Exists((Sending x) => x.P == p && x.M == m))
				{
					continue;
				}
				if (!_packs.ContainsKey(m.Id))
				{
					_packs[m.Id] = Task.Run(() => PackMod(m));
				}
				_sending.Add(new Sending
				{
					P = p,
					M = m
				});
			}
		}

		public static void HostTick(NetSession s)
		{
			for (int num = _sending.Count - 1; num >= 0; num--)
			{
				Sending sending = _sending[num];
				if (sending.P.Link == null || !sending.P.Link.Open || !s.Players.ContainsKey(sending.P.Id))
				{
					_sending.RemoveAt(num);
				}
				else
				{
					Task<byte[]> task = _packs[sending.M.Id];
					if (task.IsCompleted)
					{
						if (task.IsFaulted)
						{
							Log.Error("NET packing mod " + sending.M.Name + ": " + task.Exception?.GetBaseException().Message);
							_sending.RemoveAt(num);
						}
						else
						{
							byte[] result = task.Result;
							int num2 = (result.Length + 49152 - 1) / 49152;
							int num3 = 0;
							while (sending.Next < num2 && num3 < 8 && sending.P.Link.Queued < 2097152)
							{
								NetWriter netWriter = new NetWriter(49216);
								netWriter.VarU(9uL);
								netWriter.Str(sending.M.Id);
								netWriter.VarU((ulong)sending.Next);
								netWriter.VarU((ulong)num2);
								int num4 = sending.Next * 49152;
								netWriter.Bytes(result, num4, Math.Min(49152, result.Length - num4));
								sending.P.Link.Send(netWriter, Delivery.Reliable);
								num3++;
								sending.Next++;
							}
							if (sending.Next >= num2)
							{
								Log.Info("NET sent mod " + sending.M.Name + " to " + sending.P?.ToString() + " (" + result.Length / 1024 + " KB)");
								_sending.RemoveAt(num);
							}
						}
					}
				}
			}
		}

		private static byte[] PackMod(Mod m)
		{
			using (MemoryStream memoryStream = new MemoryStream())
			{
				using (DeflateStream output = new DeflateStream(memoryStream, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
				{
					using (BinaryWriter binaryWriter = new BinaryWriter(output))
					{
						string[] array = ((!m.Dll) ? Directory.GetFiles(m.Folder, "*", SearchOption.AllDirectories) : new string[1] { m.Folder });
						string root = (m.Dll ? Path.GetDirectoryName(m.Folder) : m.Folder);
						binaryWriter.Write(array.Length);
						string[] array2 = array;
						foreach (string text in array2)
						{
							binaryWriter.Write(Relative(root, text).Replace('\\', '/'));
							byte[] array3 = File.ReadAllBytes(text);
							binaryWriter.Write(array3.Length);
							binaryWriter.Write(array3);
						}
					}
				}
				return memoryStream.ToArray();
			}
		}

		public static void Reset()
		{
			_gen++;
			Asking = false;
			_waitingFiles = (_waitingWorkshop = 0);
			_incoming.Clear();
			Missing.Clear();
			_sending.Clear();
			_packs.Clear();
		}

		public static void ListArrived(NetSession s, NetReader r)
		{
			int num = r.Count(512);
			Missing.Clear();
			List<Mod> list = Installed();
			for (int i = 0; i < num; i++)
			{
				Mod m = Mod.Unpack(r);
				Mod mod = list.Find((Mod x) => x.Id == m.Id);
				if (mod == null || !(mod.Hash == m.Hash))
				{
					Missing.Add(m);
				}
			}
			if (Missing.Count == 0)
			{
				Ready(s);
				return;
			}
			Asking = true;
			TrustHost = false;
			Note = "";
			s.Stage = "This world uses " + Missing.Count + ((Missing.Count == 1) ? " mod" : " mods") + " you don't have (F2, Multiplayer)";
			Log.Info("NET the host's mods missing here: " + string.Join(", ", Missing.ConvertAll((Mod x) => x.Name + " " + x.Version).ToArray()));
		}

		private static void Ready(NetSession s)
		{
			NetWriter netWriter = new NetWriter(16);
			netWriter.VarU(10uL);
			s.ToHost(netWriter, Delivery.Reliable);
		}

		public static void SkipThem(NetSession s)
		{
			Asking = false;
			Note = "";
			Ready(s);
			s.Stage = "Waiting for the world";
		}

		public static void GetThem(NetSession s)
		{
			bool available = SteamLobbies.Available;
			int num = 0;
			foreach (Mod item in Missing)
			{
				if ((item.Workshop != 0 && available) || (item.Workshop == 0L && TrustHost))
				{
					num++;
				}
			}
			if (num == 0)
			{
				Note = ((Missing.Exists((Mod m) => m.Workshop == 0) && !TrustHost) ? "Tick I trust this host to get their own mod files, or join without them" : "Steam is not running, so Workshop mods can't be fetched. Start Steam, or join without them");
				return;
			}
			Asking = false;
			Note = "";
			List<string> list = new List<string>();
			_waitingWorkshop = 0;
			foreach (Mod item2 in Missing)
			{
				if (item2.Workshop != 0 && available)
				{
					_waitingWorkshop++;
					Workshop(item2.Workshop);
				}
				else if (item2.Workshop == 0L && TrustHost)
				{
					list.Add(item2.Id);
				}
			}
			_waitingFiles = list.Count;
			if (list.Count > 0)
			{
				NetWriter netWriter = new NetWriter();
				netWriter.VarU(9uL);
				netWriter.VarU((ulong)list.Count);
				foreach (string item3 in list)
				{
					netWriter.Str(item3);
				}
				s.ToHost(netWriter, Delivery.Reliable);
			}
			Progress = "Getting mods...";
			s.Stage = Progress;
			CheckDone(s);
		}

		private static async void Workshop(ulong id)
		{
			int gen = _gen;
			try
			{
				bool flag = await WorkshopGet(id);
				Log.Info("NET workshop mod " + id + (flag ? " downloaded" : " failed"));
			}
			catch (Exception ex)
			{
				Log.Error("NET workshop " + id + ": " + ex.Message);
			}
			if (gen == _gen)
			{
				_waitingWorkshop--;
				if (NetSession.Current != null)
				{
					CheckDone(NetSession.Current);
				}
			}
		}

		private static async Task<bool> WorkshopGet(ulong id)
		{
			await WorkshopSubscribe(id);
			return await WorkshopDownload(id);
		}

		private static Task<bool> WorkshopSubscribe(ulong id)
		{
			return new Steamworks.Ugc.Item(id).Subscribe();
		}

		private static Task<bool> WorkshopDownload(ulong id)
		{
			return new Steamworks.Ugc.Item(id).DownloadAsync();
		}

		public static void FileArrived(NetSession s, NetReader r)
		{
			string id = r.Str() ?? "";
			int num = (int)r.VarU();
			int num2 = (int)r.VarU();
			byte[] value = r.Bytes(65536);
			Mod mod = Missing.Find((Mod x) => x.Id == id);
			if (mod == null || !TrustHost || num2 <= 0 || num >= num2 || num < 0 || (long)num2 > 4802L)
			{
				return;
			}
			if (!_incoming.TryGetValue(id, out var value2))
			{
				value2 = (_incoming[id] = new List<byte[]>(new byte[num2][]));
			}
			if (value2.Count != num2)
			{
				return;
			}
			value2[num] = value;
			int num3 = 0;
			foreach (byte[] item in value2)
			{
				if (item != null)
				{
					num3++;
				}
			}
			Progress = "Getting " + mod.Name + " " + 100 * num3 / num2 + "%";
			s.Stage = Progress;
			if (num3 >= num2)
			{
				_incoming.Remove(id);
				try
				{
					Unpack(mod, value2);
				}
				catch (Exception ex)
				{
					Log.Error("NET installing mod " + mod.Name + ": " + ex.Message);
					NetSession.Leave("Could not install the host's mod " + mod.Name + " (" + ex.Message + ")");
					return;
				}
				_waitingFiles--;
				CheckDone(s);
			}
		}

		private static void Unpack(Mod m, List<byte[]> parts)
		{
			MemoryStream memoryStream = new MemoryStream();
			foreach (byte[] part in parts)
			{
				memoryStream.Write(part, 0, part.Length);
			}
			memoryStream.Position = 0L;
			string text = SafeName(m.Dll ? m.Name : m.Id);
			string text2 = (m.Dll ? DllFolder : Path.Combine(ModsFolder, text));
			string fullPath = Path.GetFullPath(text2);
			string text3 = null;
			if (!m.Dll && Directory.Exists(text2))
			{
				text3 = Path.Combine(Path.Combine(GameRoot, "Mods_replaced"), text + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
				Directory.CreateDirectory(Path.GetDirectoryName(text3));
				Directory.Move(text2, text3);
			}
			Directory.CreateDirectory(text2);
			List<string> list = new List<string>();
			try
			{
				UnpackInto(m, memoryStream, text2, fullPath, list);
			}
			catch
			{
				try
				{
					if (m.Dll)
					{
						foreach (string item in list)
						{
							File.Delete(item);
						}
					}
					else
					{
						Directory.Delete(text2, recursive: true);
						if (text3 != null)
						{
							Directory.Move(text3, text2);
						}
					}
				}
				catch (Exception ex)
				{
					Log.Error("NET taking a broken mod out: " + ex.Message);
				}
				throw;
			}
			Log.Info("NET installed the host's mod " + m.Name + " into " + text2);
		}

		private static void UnpackInto(Mod m, Stream all, string target, string full, List<string> written)
		{
			long num = 0L;
			using (DeflateStream input = new DeflateStream(all, CompressionMode.Decompress))
			{
				using (BinaryReader binaryReader = new BinaryReader(input))
				{
					int num2 = binaryReader.ReadInt32();
					if (num2 < 0 || num2 > 4000)
					{
						throw new IOException("too many files");
					}
					for (int i = 0; i < num2; i++)
					{
						string text = binaryReader.ReadString().Replace('/', Path.DirectorySeparatorChar);
						int num3 = binaryReader.ReadInt32();
						num += num3;
						if (num3 < 0 || num > 314572800)
						{
							throw new IOException("too big");
						}
						string fullPath = Path.GetFullPath(Path.Combine(target, text));
						if (!fullPath.StartsWith(full, StringComparison.OrdinalIgnoreCase) || text.Contains(".."))
						{
							throw new IOException("bad path " + text);
						}
						if (m.Dll && !fullPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
						{
							throw new IOException("not a dll");
						}
						Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
						byte[] array = binaryReader.ReadBytes(num3);
						if (array.Length != num3)
						{
							throw new IOException("the mod arrived cut short");
						}
						written.Add(fullPath);
						File.WriteAllBytes(fullPath, array);
					}
				}
			}
		}

		private static string SafeName(string s)
		{
			StringBuilder stringBuilder = new StringBuilder();
			string text = s ?? "mod";
			foreach (char c in text)
			{
				stringBuilder.Append((char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.') ? c : '_');
			}
			string text2 = stringBuilder.ToString().Trim('.');
			if (text2.Length != 0)
			{
				if (text2.Length <= 60)
				{
					return text2;
				}
				return text2.Substring(0, 60);
			}
			return "mod";
		}

		private static void CheckDone(NetSession s)
		{
			if (_waitingFiles <= 0 && _waitingWorkshop <= 0)
			{
				MpPrefs current = MpPrefs.Current;
				current.Rejoin = s.RejoinAddress;
				current.RejoinPassword = s.Password;
				current.Save();
				Log.Info("NET mods in place; restarting to load them, then rejoining " + current.Rejoin);
				s.Stage = "Mods ready. Restarting the game to load them...";
				NetSession.Leave("Restarting to load the host's mods");
				Restart();
			}
		}

		private static void Restart()
		{
			try
			{
				if (Application.platform == RuntimePlatform.WindowsPlayer)
				{
					Process.Start(new ProcessStartInfo("cmd.exe", "/c ping -n 6 127.0.0.1 >nul & start \"\" \"steam://rungameid/1206560\"")
					{
						CreateNoWindow = true,
						UseShellExecute = false
					});
				}
			}
			catch (Exception ex)
			{
				Log.Error("NET restarting: " + ex.Message);
			}
			Application.Quit();
		}
	}
}
