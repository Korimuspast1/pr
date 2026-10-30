using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FirstPerson.Core;
using Newtonsoft.Json.Linq;
using Steamworks;
using UnityEngine;

namespace FirstPerson
{
	internal static class PacksUi
	{
		private sealed class Pic
		{
			public long Sig;

			public Texture2D Tex;

			public bool Tried;

			public Task<MeshModel> Model;
		}

		public const int Installed = 0;

		public const int Browse = 1;

		public const int Publish = 2;

		public static readonly string[] Pages = new string[3] { "Installed", "Browse", "Publish" };

		public static readonly string[] KindFilter = new string[6] { "All", "Models", "Sounds", "Lang", "Shader", "Texture" };

		public static bool Open;

		public static int Page;

		public static bool FocusActive;

		public static int Focus;

		public static string Details;

		public static string Confirm;

		public static string ConfirmKey;

		public static Vector2 ScrollAvailable;

		public static Vector2 ScrollActive;

		public static Vector2 ScrollDetails;

		public static Vector2 ScrollBrowse;

		public static Vector2 ScrollPublish;

		public static Vector2 ScrollReport;

		public static string PressKey;

		public static bool PressActive;

		public static bool Dragging;

		public static Vector2 PressAt;

		public static float LastClickAt = -1f;

		public static string LastClickKey;

		public static string Search = "";

		public static int Kind;

		public static int Sort;

		public static bool Searched;

		public static string PublishKey;

		public static int Visibility;

		public static string Changelog = "";

		public static string Note = "";

		public static float NoteUntil;

		private static float _nextScan;

		private static int _confirmAt = -1;

		private static readonly Dictionary<string, Pic> Pics = new Dictionary<string, Pic>();

		private static int _drawnThisFrame = -1;

		public static void Say(string line)
		{
			Note = line ?? "";
			NoteUntil = Time.unscaledTime + 5f;
			if (!string.IsNullOrEmpty(line))
			{
				Log.Info("packs ui: " + line);
			}
		}

		public static void OpenAt(int page)
		{
			Open = true;
			Page = page;
			Details = null;
			Confirm = null;
			Rescan();
			PackModelNames.Write();
			if (page == 1 && !Searched)
			{
				RunSearch();
			}
			Sounds.PlayUI("event:/SFX/UI/WindowWhoosh");
		}

		public static void Rescan()
		{
			string text = FocusedKey();
			try
			{
				Packs.Discover();
			}
			catch (Exception ex)
			{
				Log.Error("packs: " + ex.Message);
			}
			if (text != null)
			{
				FocusOn(text);
			}
			_nextScan = Time.unscaledTime + 4f;
		}

		public static void Tick()
		{
			if (!Open)
			{
				Packs.WatchActive();
				if (PackSteam.NewlyInstalled && PackSteam.Arrived.Count != 0)
				{
					PackSteam.NewlyInstalled = false;
					Rescan();
					TurnOnArrived();
				}
				return;
			}
			bool flag = false;
			try
			{
				flag = PackSteam.NewlyInstalled;
				PackSteam.NewlyInstalled = false;
			}
			catch
			{
			}
			if (Time.unscaledTime >= _nextScan)
			{
				_nextScan = Time.unscaledTime + 4f;
				Packs.LookAgain();
			}
			bool differs;
			bool flag2 = Packs.TakeLook(out differs) && differs;
			if (flag || flag2)
			{
				int count = Packs.All.Count;
				Rescan();
				if (flag && !TurnOnArrived() && Packs.All.Count > count)
				{
					Say("Downloaded. It is in Available");
				}
			}
		}

		private static bool TurnOnArrived()
		{
			bool result = false;
			ulong[] array = PackSteam.Arrived.ToArray();
			foreach (ulong id in array)
			{
				PackInfo packInfo = Packs.All.Find((PackInfo x) => x.Source == PackSource.Workshop && x.WorkshopId == id);
				if (packInfo == null)
				{
					continue;
				}
				PackSteam.Arrived.Remove(id);
				if (PackSteam.TurnOnWhenIn.Remove(id) && !Packs.IsActive(packInfo.Key))
				{
					Toggle(packInfo.Key, 0);
					if (Confirm == null)
					{
						Say("Downloaded and turned on " + packInfo.Title);
					}
					result = true;
				}
			}
			return result;
		}

		public static void RunSearch()
		{
			Searched = true;
			ScrollBrowse = Vector2.zero;
			try
			{
				PackSteam.Query(Search, (Kind == 0) ? null : KindFilter[Kind].ToLowerInvariant(), Sort, 1);
			}
			catch (Exception ex)
			{
				PackSteam.Status = "The Workshop is out of reach. " + ex.Message;
			}
		}

		public static List<PackInfo> ListOf(bool active)
		{
			if (!active)
			{
				return Packs.Available;
			}
			return Packs.Active;
		}

		public static string FocusedKey()
		{
			List<PackInfo> list = ListOf(FocusActive);
			if (Focus < 0 || Focus >= list.Count)
			{
				return null;
			}
			return list[Focus].Key;
		}

		public static void FocusOn(string key)
		{
			int num = Packs.Active.FindIndex((PackInfo p) => p.Key == key);
			if (num >= 0)
			{
				FocusActive = true;
				Focus = num;
				return;
			}
			num = Packs.Available.FindIndex((PackInfo p) => p.Key == key);
			if (num >= 0)
			{
				FocusActive = false;
				Focus = num;
			}
		}

		public static void ClampFocus()
		{
			int count = ListOf(FocusActive).Count;
			if (count == 0 && ListOf(!FocusActive).Count > 0)
			{
				FocusActive = !FocusActive;
				count = ListOf(FocusActive).Count;
			}
			Focus = Math.Max(0, Math.Min(Focus, count - 1));
		}

		public static void Toggle(string key, int at = -1)
		{
			PackInfo packInfo = Packs.Find(key);
			if (packInfo == null)
			{
				return;
			}
			if (Packs.IsActive(key))
			{
				Packs.Deactivate(key);
				Say("Turned off " + packInfo.Title);
			}
			else
			{
				if (packInfo.NewerWorldfall && Confirm != "newer")
				{
					Confirm = "newer";
					ConfirmKey = key;
					_confirmAt = at;
					return;
				}
				Packs.Activate(key, at);
				Say("Turned on " + packInfo.Title);
			}
			FocusOn(key);
		}

		public static void Confirmed()
		{
			string confirm = Confirm;
			string confirmKey = ConfirmKey;
			Confirm = null;
			PackInfo packInfo = Packs.Find(confirmKey);
			if (confirm == "newer" && packInfo != null)
			{
				Packs.Activate(confirmKey, _confirmAt);
				Say("Turned on " + packInfo.Title);
				FocusOn(confirmKey);
			}
			else if (confirm == "remove" && packInfo != null)
			{
				Say(Packs.Remove(confirmKey) ?? ((packInfo.Source == PackSource.Workshop) ? ("Unsubscribed from " + packInfo.Title) : ("Moved " + packInfo.Title + " to the recycle bin")));
				if (Details == confirmKey)
				{
					Details = null;
				}
				ClampFocus();
			}
			else if (confirm == "publish" && packInfo != null)
			{
				PackPublish.Start(packInfo, Visibility, Changelog, dry: false);
			}
		}

		public static void AskRemove(string key)
		{
			Confirm = "remove";
			ConfirmKey = key;
		}

		public static void MoveActive(string key, int by)
		{
			int num = Packs.ActiveKeys.IndexOf(key);
			if (num >= 0)
			{
				Packs.MoveTo(key, num + by);
				FocusOn(key);
			}
		}

		public static void Drop(string key, bool ontoActive, int at)
		{
			bool flag = Packs.IsActive(key);
			if (ontoActive && flag)
			{
				int num = Packs.ActiveKeys.IndexOf(key);
				Packs.MoveTo(key, (at > num) ? (at - 1) : at);
			}
			else if (ontoActive)
			{
				Toggle(key, at);
			}
			else if (flag)
			{
				Toggle(key);
			}
			FocusOn(key);
		}

		public static Texture2D PictureOf(PackInfo p)
		{
			if (Pics.TryGetValue(p.Key, out var value) && value.Sig == p.Sig)
			{
				if (value.Model != null && value.Model.IsCompleted && Time.frameCount != _drawnThisFrame)
				{
					_drawnThisFrame = Time.frameCount;
					Task<MeshModel> model = value.Model;
					value.Model = null;
					try
					{
						MeshModel meshModel = ((model.Status == TaskStatus.RanToCompletion) ? model.Result : null);
						if (meshModel != null)
						{
							value.Tex = PackPreview.Render(meshModel, 96, 96);
						}
					}
					catch (Exception ex)
					{
						Log.Info("packs: no picture for " + p.Key + " (" + ex.Message + ")");
					}
				}
				return value.Tex;
			}
			if (value != null && value.Tex != null)
			{
				UnityEngine.Object.Destroy(value.Tex);
			}
			if (Time.frameCount == _drawnThisFrame)
			{
				return null;
			}
			_drawnThisFrame = Time.frameCount;
			value = new Pic
			{
				Sig = p.Sig,
				Tried = true
			};
			Pics[p.Key] = value;
			try
			{
				if (p.Preview != null)
				{
					Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false)
					{
						name = "PackPreview " + p.Key,
						filterMode = FilterMode.Bilinear
					};
					if (texture2D.LoadImage(File.ReadAllBytes(p.Preview)))
					{
						value.Tex = texture2D;
					}
					else
					{
						UnityEngine.Object.Destroy(texture2D);
					}
				}
				else if (p.ModelFiles.Count > 0 && Models.Loaded)
				{
					TaskCompletionSource<MeshModel> read = new TaskCompletionSource<MeshModel>();
					value.Model = read.Task;
					PackInfo pack = p;
					SlowLane.Run(delegate
					{
						try
						{
							read.SetResult(Packs.PictureModel(pack));
						}
						catch (Exception exception)
						{
							read.SetException(exception);
						}
					});
				}
			}
			catch (Exception ex2)
			{
				Log.Info("packs: no picture for " + p.Key + " (" + ex2.Message + ")");
			}
			return value.Tex;
		}

		public static string NewPack()
		{
			int num = 1;
			string text;
			do
			{
				text = "my_pack" + ((num == 1) ? "" : ("_" + num));
			}
			while (Directory.Exists(Path.Combine(Packs.LocalFolder, text)) && ++num < 100);
			string path = Path.Combine(Packs.LocalFolder, text);
			Directory.CreateDirectory(Path.Combine(path, "models"));
			Directory.CreateDirectory(Path.Combine(path, "sounds"));
			Directory.CreateDirectory(Path.Combine(path, "lang"));
			string text2 = "";
			try
			{
				text2 = SteamClient.Name;
			}
			catch
			{
			}
			JObject jObject = new JObject
			{
				["id"] = text,
				["title"] = "My pack",
				["author"] = (string.IsNullOrEmpty(text2) ? "Me" : text2),
				["version"] = "1.0.0",
				["worldfall"] = "0.9.8.9",
				["description"] = "",
				["kinds"] = new JArray(),
				["loadAfter"] = new JArray()
			};
			File.WriteAllText(Path.Combine(path, "worldfall.json"), jObject.ToString());
			File.WriteAllText(Path.Combine(path, "README.txt"), "A Worldfall pack. Put models in models/<name>.glb (names in ../model_names.txt), sounds in sounds/<set>/*.wav or .ogg\n(sets in ../sound_sets.txt), translations in lang/<code>.txt, and a 512x512 preview.png here. Edit worldfall.json,\nthen turn it on in F2 > Mods. While it is on, files changed here show in the game within a few seconds.\n");
			Rescan();
			return text + "@local";
		}

		public static void ShowFolder(string dir)
		{
			try
			{
				Directory.CreateDirectory(dir);
				dir = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				string text = FolderOpener();
				if (text != null)
				{
					try
					{
						Process.Start(new ProcessStartInfo(text, "\"" + dir + "\"")
						{
							UseShellExecute = false
						});
						Log.Info("packs ui: opened " + dir);
						return;
					}
					catch (Exception ex)
					{
						Log.Info("packs ui: " + text + " would not start (" + ex.Message + "): opening it as an address");
					}
				}
				Application.OpenURL(new Uri(dir).AbsoluteUri);
			}
			catch (Exception ex2)
			{
				Say("Could not open the folder. " + ex2.Message);
			}
		}

		internal static string FolderOpener()
		{
			switch (Application.platform)
			{
			case RuntimePlatform.WindowsPlayer:
			case RuntimePlatform.WindowsEditor:
				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
			case RuntimePlatform.OSXEditor:
			case RuntimePlatform.OSXPlayer:
				return "/usr/bin/open";
			case RuntimePlatform.LinuxPlayer:
			case RuntimePlatform.LinuxEditor:
				return "xdg-open";
			default:
				return null;
			}
		}
	}
}
