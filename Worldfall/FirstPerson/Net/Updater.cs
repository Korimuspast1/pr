using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Steamworks.Ugc;
using UnityEngine;
using UnityEngine.Networking;

namespace FirstPerson.Net
{
	internal static class Updater
	{
		public enum Step
		{
			Idle,
			Checking,
			Downloading,
			Restarting,
			Failed
		}

		private enum Source
		{
			Workshop,
			Website,
			NmlWebsite,
			Phone
		}

		private struct ZipEntry
		{
			public string Name;

			public int Method;

			public int Compressed;

			public int Size;

			public int Offset;
		}

		private const string Repo = "s3cond2/worldfall";

		public static Step State = Step.Idle;

		public static string Status = "";

		public static string OtherVersion = "";

		public static string OtherBuild = "";

		public static bool OtherHosts;

		public static float SeenAt = -999f;

		internal static string DllForTest;

		private static string _rejoin = "";

		private static string _password = "";

		private static UnityWebRequest _req;

		private static Action<UnityWebRequest> _done;

		private static ulong _lastBytes;

		private static float _lastGrowth;

		public static bool Mismatch
		{
			get
			{
				if (Time.realtimeSinceStartup - SeenAt < 600f)
				{
					if (!(OtherVersion != "0.9.8.9"))
					{
						return OtherBuild != NetProtocol.Build;
					}
					return true;
				}
				return false;
			}
		}

		public static int Compare
		{
			get
			{
				if (OtherVersion == "")
				{
					return -1;
				}
				return Math.Sign(VersionOrder(OtherVersion).CompareTo(VersionOrder("0.9.8.9")));
			}
		}

		public static bool ShouldUpdate
		{
			get
			{
				if (Mismatch)
				{
					return Compare >= 0;
				}
				return false;
			}
		}

		public static string Advice
		{
			get
			{
				if (!Mismatch)
				{
					return "";
				}
				string text = (OtherHosts ? "The host" : "A player");
				if (Compare < 0)
				{
					return text + " has Worldfall " + ((OtherVersion == "") ? "an older version" : OtherVersion) + ", you have 0.9.8.9. " + (OtherHosts ? "The host needs" : "They need") + " to update";
				}
				if (Compare > 0)
				{
					return text + " has Worldfall " + OtherVersion + ", you have 0.9.8.9";
				}
				return text + " has another download of Worldfall " + OtherVersion;
			}
		}

		private static Source From => Source.Website;

		public static void Seen(string version, string build, bool otherHosts)
		{
			OtherVersion = MpPrefs.Clean(version, 20);
			OtherBuild = MpPrefs.Clean(build, 20);
			OtherHosts = otherHosts;
			SeenAt = Time.realtimeSinceStartup;
			if (State == Step.Failed)
			{
				State = Step.Idle;
				Status = "";
			}
			Log.Info("NET other build seen: " + ((OtherVersion == "") ? "?" : OtherVersion) + " " + OtherBuild + (otherHosts ? " (host)" : " (joiner)") + ", ours 0.9.8.9 " + NetProtocol.Build);
		}

		private static long VersionOrder(string v)
		{
			long num = 0L;
			string[] array = (v ?? "").Split('.');
			for (int i = 0; i < 4; i++)
			{
				int result = 0;
				if (i < array.Length)
				{
					string text = "";
					string text2 = array[i];
					for (int j = 0; j < text2.Length; j++)
					{
						char c = text2[j];
						if (!char.IsDigit(c))
						{
							break;
						}
						text += c;
					}
					int.TryParse(text, out result);
				}
				num = num * 10000 + Math.Min(9999, result);
			}
			return num;
		}

		private static string DllPath()
		{
			if (DllForTest != null)
			{
				return DllForTest;
			}
			try
			{
				string location = typeof(Updater).Assembly.Location;
				if (!string.IsNullOrEmpty(location) && File.Exists(location))
				{
					return location;
				}
			}
			catch
			{
			}
			try
			{
				string text = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "Worldfall.dll");
				if (File.Exists(text))
				{
					return text;
				}
			}
			catch
			{
			}
			return null;
		}

		public static void Start(string rejoin, string password)
		{
			if (State != Step.Checking && State != Step.Downloading && State != Step.Restarting)
			{
				_rejoin = ((rejoin != null && (rejoin.StartsWith("ip:", StringComparison.Ordinal) || rejoin.StartsWith("steam:", StringComparison.Ordinal) || rejoin.StartsWith("code:", StringComparison.Ordinal))) ? rejoin : "");
				_password = password ?? "";
				switch (From)
				{
				case Source.Phone:
					Fail("Get Worldfall " + ((OtherVersion == "") ? "" : (OtherVersion + " ")) + "from worldfall3d.com and install it again");
					break;
				case Source.Workshop:
					WorkshopUpdate();
					break;
				default:
					WebsiteUpdate();
					break;
				}
			}
		}

		private static void Fail(string why)
		{
			State = Step.Failed;
			Status = why;
			Log.Info("NET update: " + why);
		}

		private static async void WorkshopUpdate()
		{
			State = Step.Checking;
			Status = "Asking Steam for the newest Worldfall";
			if (!SteamLobbies.Available)
			{
				Fail("Steam is not running. Start Steam, then update");
				return;
			}
			try
			{
				bool flag;
				using (CancellationTokenSource giveUp = new CancellationTokenSource(TimeSpan.FromMinutes(5.0)))
				{
					flag = await WorkshopDownload(delegate(float p)
					{
						Status = "Downloading Worldfall " + (int)(p * 100f) + "%";
					}, giveUp.Token);
				}
				if (!flag)
				{
					Fail("Steam would not download the Workshop item. Unsubscribe and subscribe to Worldfall again");
					return;
				}
				string text = DllPath();
				if (text != null)
				{
					byte[] hay = File.ReadAllBytes(text);
					if ((OtherBuild == "" || !Contains(hay, Encoding.Unicode.GetBytes(OtherBuild))) && Contains(hay, Encoding.Unicode.GetBytes(NetProtocol.Build)))
					{
						Fail("Steam has no newer Worldfall yet. Try again in a few minutes");
						return;
					}
				}
				Restart(null);
			}
			catch (Exception ex)
			{
				Fail("Steam: " + ex.Message);
			}
		}

		private static Task<bool> WorkshopDownload(Action<float> progress, CancellationToken giveUp)
		{
			return new Steamworks.Ugc.Item(3814551764uL).DownloadAsync(progress, 60, giveUp);
		}

		private static void WebsiteUpdate()
		{
			string dll = DllPath();
			if (dll == null)
			{
				Fail("Could not find Worldfall's own file. Install the newest download from worldfall3d.com by hand");
				return;
			}
			State = Step.Checking;
			Status = "Looking for the newest Worldfall";
			Get("https://api.github.com/repos/s3cond2/worldfall/releases/latest", 120, delegate(UnityWebRequest req)
			{
				string text = ((From == Source.NmlWebsite) ? "Worldfall_NML.zip" : "Worldfall.zip");
				string text2 = null;
				string text3 = "";
				try
				{
					JObject jObject = JObject.Parse(req.downloadHandler.text);
					text3 = ((string)jObject["tag_name"]) ?? "";
					foreach (JToken item in (JArray)jObject["assets"])
					{
						if ((string)item["name"] == text)
						{
							text2 = (string)item["browser_download_url"];
						}
					}
				}
				catch (Exception ex)
				{
					Fail("GitHub's answer could not be read (" + ex.Message + ")");
					return;
				}
				if (text2 == null)
				{
					Fail("The newest release has no " + text);
				}
				else
				{
					string newest = text3.TrimStart('v', 'b', 'e', 't', 'a', '-');
					if (OtherVersion != "" && VersionOrder(newest) < VersionOrder(OtherVersion))
					{
						Fail("Worldfall " + OtherVersion + " isn't on GitHub yet (newest " + newest + "). The other player has a test build");
					}
					else
					{
						State = Step.Downloading;
						Status = "Downloading Worldfall " + newest;
						Get(text2, 0, delegate(UnityWebRequest zipReq)
						{
							Install(zipReq.downloadHandler.data, dll, newest);
						});
					}
				}
			});
		}

		private static void Install(byte[] zip, string dll, string newest)
		{
			try
			{
				byte[] array = null;
				byte[] array2 = null;
				foreach (ZipEntry item in ZipEntries(zip))
				{
					string text = item.Name.Replace('\\', '/');
					if (text.EndsWith("Worldfall.dll", StringComparison.OrdinalIgnoreCase))
					{
						array = Inflate(zip, item);
					}
					else if (text.EndsWith("/mod.json", StringComparison.OrdinalIgnoreCase))
					{
						array2 = Inflate(zip, item);
					}
				}
				if (array == null || array.Length < 1048576)
				{
					Fail("The download has no Worldfall.dll");
					return;
				}
				if (OtherBuild != "" && !Contains(array, Encoding.Unicode.GetBytes(OtherBuild)))
				{
					if (Contains(array, Encoding.Unicode.GetBytes(NetProtocol.Build)))
					{
						Fail("You already have the newest Worldfall. The other player needs to update");
						return;
					}
					Log.Info("NET update: the newest download matches neither build; installing it anyway");
				}
				File.WriteAllBytes(dll + ".new", array);
				if (array2 != null && From == Source.NmlWebsite)
				{
					File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(dll), "mod.json.new"), array2);
				}
				Log.Info("NET update: Worldfall " + newest + " written beside " + dll);
				Restart(dll);
			}
			catch (Exception ex)
			{
				Fail("Could not write the update (" + ex.Message + ")");
			}
		}

		private static void Restart(string dll)
		{
			State = Step.Restarting;
			Status = "Restarting WorldBox";
			MpPrefs current = MpPrefs.Current;
			current.Rejoin = _rejoin;
			current.RejoinPassword = ((_rejoin == "") ? "" : _password);
			current.Save();
			bool mirroring = WorldSync.Mirroring;
			try
			{
				NetSession.Leave("Restarting to update Worldfall");
			}
			catch
			{
			}
			if (!mirroring && World.world != null && !WorldBoxMod.WorldLoading)
			{
				try
				{
					AutoSaveManager.autoSave(pSkipDelete: true, pForce: true);
					Log.Info("NET update: the world autosaved before the restart");
				}
				catch (Exception ex)
				{
					Log.Error("NET update: autosave: " + ex);
				}
			}
			try
			{
				string path = ((dll == null) ? null : Path.GetDirectoryName(dll));
				if (Application.platform == RuntimePlatform.WindowsPlayer)
				{
					string text = Path.Combine(Path.GetTempPath(), "worldfall_update.cmd");
					File.WriteAllText(text, SwapScript(dll != null, "start \"\" \"steam://rungameid/1206560\""));
					Process.Start(new ProcessStartInfo("cmd.exe", "/c \"\"" + text + "\"" + ((dll != null) ? (" \"" + dll + "\"") : "") + "\"")
					{
						CreateNoWindow = true,
						UseShellExecute = false
					});
				}
				else
				{
					if (dll != null)
					{
						Swap(dll + ".new", dll);
						if (File.Exists(Path.Combine(path, "mod.json.new")))
						{
							Swap(Path.Combine(path, "mod.json.new"), Path.Combine(path, "mod.json"));
						}
					}
					string text2 = ((Application.platform == RuntimePlatform.OSXPlayer) ? "open" : "xdg-open");
					Process.Start(new ProcessStartInfo("/bin/sh", "-c \"sleep 5; " + text2 + " steam://rungameid/1206560\"")
					{
						UseShellExecute = false,
						CreateNoWindow = true
					});
				}
			}
			catch (Exception ex2)
			{
				Fail("Could not restart by itself (" + ex2.Message + "). Start WorldBox again");
				return;
			}
			Application.Quit();
		}

		internal static string SwapScript(bool swap, string launch)
		{
			StringBuilder stringBuilder = new StringBuilder("@echo off\r\n");
			if (swap)
			{
				stringBuilder.Append("for /l %%i in (1,1,60) do (\r\n  move /y \"%~1.new\" \"%~1\" >nul 2>&1 && goto moved\r\n  ping -n 2 127.0.0.1 >nul\r\n)\r\n:moved\r\n");
				stringBuilder.Append("if exist \"%~dp1mod.json.new\" move /y \"%~dp1mod.json.new\" \"%~dp1mod.json\" >nul 2>&1\r\n");
			}
			else
			{
				stringBuilder.Append("ping -n 6 127.0.0.1 >nul\r\n");
			}
			return stringBuilder.Append(launch).Append("\r\n").ToString();
		}

		private static void Swap(string from, string to)
		{
			if (File.Exists(to))
			{
				File.Delete(to);
			}
			File.Move(from, to);
		}

		private static void Get(string url, int timeout, Action<UnityWebRequest> done)
		{
			try
			{
				_req = UnityWebRequest.Get(url);
				_req.SetRequestHeader("User-Agent", "Worldfall/0.9.8.9");
				_req.timeout = timeout;
				_lastBytes = 0uL;
				_lastGrowth = Time.realtimeSinceStartup;
				_req.SendWebRequest();
				_done = done;
			}
			catch (Exception ex)
			{
				Fail("Could not reach GitHub (" + ex.Message + ")");
			}
		}

		public static void Tick()
		{
			if (_req == null)
			{
				return;
			}
			if (State == Step.Downloading && !_req.isDone)
			{
				ulong downloadedBytes = _req.downloadedBytes;
				if (downloadedBytes != _lastBytes)
				{
					_lastBytes = downloadedBytes;
					_lastGrowth = Time.realtimeSinceStartup;
				}
				else if (Time.realtimeSinceStartup - _lastGrowth > 30f)
				{
					UnityWebRequest req = _req;
					_req = null;
					_done = null;
					try
					{
						req.Abort();
						req.Dispose();
					}
					catch
					{
					}
					Fail("The download stopped. Try again, or get Worldfall from worldfall3d.com");
					return;
				}
				Status = "Downloading Worldfall " + (int)(_req.downloadProgress * 100f) + "%";
			}
			if (!_req.isDone)
			{
				return;
			}
			UnityWebRequest req2 = _req;
			Action<UnityWebRequest> done = _done;
			_req = null;
			_done = null;
			try
			{
				if (req2.result != UnityWebRequest.Result.Success)
				{
					Fail("Could not reach GitHub (" + req2.error + ")");
				}
				else
				{
					done?.Invoke(req2);
				}
			}
			catch (Exception ex)
			{
				Fail(ex.Message);
			}
			finally
			{
				try
				{
					req2.Dispose();
				}
				catch
				{
				}
			}
		}

		private static List<ZipEntry> ZipEntries(byte[] z)
		{
			List<ZipEntry> list = new List<ZipEntry>();
			int num = -1;
			for (int num2 = z.Length - 22; num2 >= Math.Max(0, z.Length - 65557); num2--)
			{
				if (U32(z, num2) == 101010256)
				{
					num = num2;
					break;
				}
			}
			if (num < 0)
			{
				throw new IOException("not a zip");
			}
			int num3 = U16(z, num + 10);
			int num4 = (int)U32(z, num + 16);
			for (int i = 0; i < num3; i++)
			{
				if (U32(z, num4) != 33639248)
				{
					throw new IOException("broken zip");
				}
				int num5 = U16(z, num4 + 28);
				int num6 = U16(z, num4 + 30);
				int num7 = U16(z, num4 + 32);
				list.Add(new ZipEntry
				{
					Method = U16(z, num4 + 10),
					Compressed = (int)U32(z, num4 + 20),
					Size = (int)U32(z, num4 + 24),
					Offset = (int)U32(z, num4 + 42),
					Name = Encoding.UTF8.GetString(z, num4 + 46, num5)
				});
				num4 += 46 + num5 + num6 + num7;
			}
			return list;
		}

		private static byte[] Inflate(byte[] z, ZipEntry e)
		{
			int num = e.Offset + 30 + U16(z, e.Offset + 26) + U16(z, e.Offset + 28);
			byte[] array = new byte[e.Size];
			if (e.Method == 0)
			{
				Buffer.BlockCopy(z, num, array, 0, e.Size);
				return array;
			}
			if (e.Method != 8)
			{
				throw new IOException("unknown zip method " + e.Method);
			}
			using (DeflateStream deflateStream = new DeflateStream(new MemoryStream(z, num, e.Compressed), CompressionMode.Decompress))
			{
				int num2;
				for (int i = 0; i < e.Size; i += num2)
				{
					num2 = deflateStream.Read(array, i, e.Size - i);
					if (num2 <= 0)
					{
						throw new IOException("the zip ended early");
					}
				}
				return array;
			}
		}

		private static int U16(byte[] b, int at)
		{
			return b[at] | (b[at + 1] << 8);
		}

		private static uint U32(byte[] b, int at)
		{
			return (uint)(b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24));
		}

		private static bool Contains(byte[] hay, byte[] needle)
		{
			if (needle.Length == 0)
			{
				return false;
			}
			for (int i = 0; i + needle.Length <= hay.Length; i++)
			{
				if (hay[i] == needle[0])
				{
					int j;
					for (j = 1; j < needle.Length && hay[i + j] == needle[j]; j++)
					{
					}
					if (j == needle.Length)
					{
						return true;
					}
				}
			}
			return false;
		}
	}
}
