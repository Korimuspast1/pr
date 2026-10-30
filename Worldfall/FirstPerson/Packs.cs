using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using FirstPerson.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace FirstPerson
{
	internal static class Packs
	{
		private sealed class Composite
		{
			public int Version;

			public List<string> Keys = new List<string>();

			public Dictionary<string, MeshModel> Overlay = new Dictionary<string, MeshModel>(StringComparer.Ordinal);

			public Dictionary<string, List<byte[]>> Sounds = new Dictionary<string, List<byte[]>>(StringComparer.Ordinal);

			public Dictionary<string, List<string>> Lang = new Dictionary<string, List<string>>(StringComparer.Ordinal);

			public Dictionary<string, List<string>> Problems = new Dictionary<string, List<string>>(StringComparer.Ordinal);

			public Dictionary<string, List<string>> Notes = new Dictionary<string, List<string>>(StringComparer.Ordinal);

			public double Ms;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		private struct ShFileOp
		{
			public IntPtr hwnd;

			public uint wFunc;

			public string pFrom;

			public string pTo;

			public ushort fFlags;

			[MarshalAs(UnmanagedType.Bool)]
			public bool fAnyOperationsAborted;

			public IntPtr hNameMappings;

			public string lpszProgressTitle;
		}

		public static readonly List<IPackKind> KindHandlers = new List<IPackKind>();

		private static int _wanted;

		private static int _applied;

		private static volatile Composite _ready;

		private static volatile bool _building;

		private static readonly object BuildGate = new object();

		private static Dictionary<string, List<string>> _lang = new Dictionary<string, List<string>>();

		public static Action<List<MeshModel>> ForgetDrawn;

		public static Func<object[]> CacheOwners;

		private static readonly Dictionary<string, PackContent> Decoded = new Dictionary<string, PackContent>(StringComparer.Ordinal);

		private static readonly object DecodeGate = new object();

		internal static readonly GlbLimits Limits = new GlbLimits();

		public const string WorldfallVersion = "0.9.8.9";

		public const string DataFolder = "Worldfall";

		public const string Manifest = "worldfall.json";

		public const string WorldfallGuid = "s3cond2.worldfall";

		public const ulong WorldfallItem = 3814551764uL;

		public const ulong NmlItem = 3080294469uL;

		public const uint WorldBoxApp = 1206560u;

		public const long MaxModelBytes = 50331648L;

		public const long MaxSoundBytes = 16777216L;

		public const long MaxLangBytes = 4194304L;

		public const long MaxPreviewBytes = 2097152L;

		public const int MaxSoundsPerSet = 32;

		private static readonly Regex NameRx = new Regex("^[a-z0-9_]{1,64}$");

		private static readonly Regex IdRx = new Regex("^[a-z0-9](?!.*\\.\\.)[a-z0-9_.-]{1,63}$");

		private static readonly List<PackInfo> _all = new List<PackInfo>();

		private static readonly List<string> _elsewhere = new List<string>();

		private static string _home;

		private static string _game;

		private static string _workshop;

		public static readonly string[] KnownKinds = new string[5] { "models", "sounds", "lang", "shader", "texture" };

		private const uint FO_DELETE = 3u;

		private const ushort FOF_SILENT = 4;

		private const ushort FOF_NOCONFIRMATION = 16;

		private const ushort FOF_ALLOWUNDO = 64;

		private const ushort FOF_NOERRORUI = 1024;

		private static readonly List<string> _active = new List<string>();

		private static Settings _settings;

		private static bool _started;

		private static float _nextWatch;

		private static volatile bool _watching;

		private static volatile List<string> _watchChanged;

		private static volatile bool _looking;

		private static volatile bool _looked;

		private static long _lookSig;

		private static long _scanSig;

		private static bool _scanned;

		internal const float Metre = 0.5714286f;

		private static readonly string[] BodyHeightKinds = new string[4] { "held_", "tool_", "item_", "load_" };

		private static readonly string[] TileModels = new string[4] { "monster_dragon", "crabzilla", "nature_volcano", "nature_geyser" };

		private static readonly HashSet<string> LengthKeys = new HashSet<string>(StringComparer.Ordinal)
		{
			"tip", "muzzle", "bead", "blade_base", "blade_tip", "butt", "ember", "flame", "gauges", "half",
			"half_width", "head", "knob", "knob_r", "length", "load_port", "port", "pump_grip", "pump_travel", "radius",
			"rim", "tail_end", "vents", "mouth", "hand_l", "hand_r", "eye", "height", "width", "wingspan",
			"maw", "vent", "body_height", "footing", "smoke"
		};

		private static readonly string[] LengthPrefixes = new string[8] { "tip_", "foot_", "hip_", "flank_", "finger_", "dactyl_", "palm_", "laser_" };

		public static Dictionary<string, List<byte[]>> SoundSets { get; private set; } = new Dictionary<string, List<byte[]>>();

		public static int AppliedVersion => _applied;

		public static int Generation { get; private set; }

		public static bool Busy
		{
			get
			{
				if (!_building && _ready == null)
				{
					return _wanted != _applied;
				}
				return true;
			}
		}

		public static double LastApplyMs { get; private set; }

		public static int LastCleared { get; private set; }

		public static Dictionary<string, List<string>> Problems { get; private set; } = new Dictionary<string, List<string>>();

		public static Dictionary<string, List<string>> Notes { get; private set; } = new Dictionary<string, List<string>>();

		public static List<PackInfo> All => _all;

		public static string LocalFolder => Path.Combine(_home ?? Log.Folder, "packs");

		public static List<PackInfo> Active
		{
			get
			{
				List<PackInfo> list = new List<PackInfo>();
				foreach (string item in _active)
				{
					PackInfo packInfo = Find(item);
					if (packInfo != null)
					{
						list.Add(packInfo);
					}
				}
				return list;
			}
		}

		public static List<PackInfo> Available => _all.FindAll((PackInfo p) => !_active.Contains(p.Key));

		public static List<string> ActiveKeys => new List<string>(_active);

		public static void RequestApply()
		{
			_wanted++;
			Kick();
		}

		private static void Kick()
		{
			lock (BuildGate)
			{
				if (_building)
				{
					return;
				}
				_building = true;
			}
			int version = _wanted;
			List<PackInfo> topFirst = Active;
			ThreadPool.QueueUserWorkItem(delegate
			{
				Composite composite = null;
				try
				{
					composite = Build(topFirst, version);
				}
				catch (Exception ex)
				{
					Log.Error("packs: could not put the packs together: " + ex);
				}
				lock (BuildGate)
				{
					_building = false;
					if (composite != null && version == _wanted)
					{
						_ready = composite;
					}
				}
				if (version != _wanted)
				{
					Kick();
				}
			});
		}

		private static Composite Build(List<PackInfo> topFirst, int version)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			Composite composite = new Composite
			{
				Version = version
			};
			for (int num = topFirst.Count - 1; num >= 0; num--)
			{
				PackInfo packInfo = topFirst[num];
				PackContent packContent = ContentOf(packInfo);
				composite.Keys.Insert(0, packInfo.Key);
				if (packContent.Problems.Count > 0)
				{
					composite.Problems[packInfo.Key] = packContent.Problems;
				}
				if (packContent.Warnings.Count > 0)
				{
					composite.Notes[packInfo.Key] = packContent.Warnings;
				}
				foreach (KeyValuePair<string, MeshModel> model in packContent.Models)
				{
					composite.Overlay[model.Key] = model.Value;
					if (IsLodName(model.Key))
					{
						continue;
					}
					string[] array = new string[2]
					{
						model.Key + "_m",
						model.Key + "_f"
					};
					foreach (string key in array)
					{
						if (!packContent.Models.ContainsKey(key) && !packInfo.ModelFiles.ContainsKey(key))
						{
							composite.Overlay[key] = null;
						}
					}
				}
				foreach (KeyValuePair<string, List<byte[]>> sound in packContent.Sounds)
				{
					composite.Sounds[sound.Key] = sound.Value;
				}
				foreach (KeyValuePair<string, string> item in packContent.Lang)
				{
					if (!composite.Lang.TryGetValue(item.Key, out var value))
					{
						value = (composite.Lang[item.Key] = new List<string>());
					}
					value.Add(item.Value);
				}
			}
			composite.Ms = stopwatch.Elapsed.TotalMilliseconds;
			return composite;
		}

		internal static bool IsLodName(string name)
		{
			if (name.Length > 2 && name[name.Length - 2] == '_')
			{
				if (name[name.Length - 1] != 'm')
				{
					return name[name.Length - 1] == 'f';
				}
				return true;
			}
			return false;
		}

		public static void ApplyPending()
		{
			Composite ready = _ready;
			if (ready == null)
			{
				return;
			}
			_ready = null;
			if (ready.Version != _wanted)
			{
				return;
			}
			Stopwatch stopwatch = Stopwatch.StartNew();
			try
			{
				PackTextures.Decode(ready.Overlay.Values);
				List<MeshModel> list = Models.SetOverlay((ready.Overlay.Count > 0) ? ready.Overlay : null);
				LastCleared = PackCaches.Clear(CacheOwners?.Invoke() ?? new object[0]);
				try
				{
					ForgetDrawn?.Invoke(list);
				}
				catch (Exception ex)
				{
					Log.Error("packs: could not let go of the old models: " + ex.Message);
				}
				ViewMeshes.Library = (string name) => Models.Get(name);
				SoundSets = ready.Sounds;
				_lang = ready.Lang;
				Lang.Reload();
				Problems = ready.Problems;
				Notes = ready.Notes;
				Generation++;
				List<PackInfo> active = Active;
				foreach (IPackKind kind in KindHandlers)
				{
					try
					{
						kind.Apply(active.FindAll((PackInfo p) => p.Kinds.Contains(kind.Kind)));
					}
					catch (Exception ex2)
					{
						Log.Error("packs: " + kind.Kind + " packs: " + ex2.Message);
					}
				}
				LastApplyMs = stopwatch.Elapsed.TotalMilliseconds;
				Log.Info("packs: " + ((ready.Keys.Count == 0) ? "none on" : string.Join(", ", ready.Keys.ToArray())) + "; " + Count(ready.Overlay, models: true) + " models, " + Count(ready.Overlay, models: false) + " hidden, " + ready.Sounds.Count + " sound sets, " + ready.Lang.Count + " languages; read in " + ready.Ms.ToString("0") + " ms, swapped in " + LastApplyMs.ToString("0.0") + " ms (" + LastCleared + " tables cleared, " + list.Count + " models let go)");
			}
			catch (Exception ex3)
			{
				Log.Error("packs: could not apply: " + ex3);
			}
			_applied = ready.Version;
		}

		private static int Count(Dictionary<string, MeshModel> overlay, bool models)
		{
			int num = 0;
			foreach (MeshModel value in overlay.Values)
			{
				if (value != null == models)
				{
					num++;
				}
			}
			return num;
		}

		internal static IEnumerable<string> LangTexts(string code)
		{
			if (!_lang.TryGetValue(code, out var value))
			{
				return new string[0];
			}
			return value;
		}

		internal static IEnumerable<string> LangCodes()
		{
			return new List<string>(_lang.Keys);
		}

		private static void ReadLinkedImages(PackInfo p, string name, string glbPath, MeshModel m)
		{
			if (m?.Textures == null)
			{
				return;
			}
			string text = Path.GetDirectoryName(Path.GetFullPath(glbPath)) ?? "";
			p.ModelTextures.TryGetValue(name, out var value);
			MeshTexture[] textures = m.Textures;
			foreach (MeshTexture meshTexture in textures)
			{
				meshTexture.Filter = value;
				if (meshTexture.Encoded != null || meshTexture.Uri == null || meshTexture.Source != null)
				{
					continue;
				}
				try
				{
					string fullPath = Path.GetFullPath(Path.Combine(text, meshTexture.Uri.Replace('/', Path.DirectorySeparatorChar)));
					if (fullPath.StartsWith(text, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath) && new FileInfo(fullPath).Length <= Limits.MaxBytes)
					{
						meshTexture.Encoded = File.ReadAllBytes(fullPath);
					}
				}
				catch
				{
				}
			}
			textures = m.Textures;
			foreach (MeshTexture meshTexture2 in textures)
			{
				if (meshTexture2.Source != null)
				{
					meshTexture2.Encoded = meshTexture2.Source.Encoded;
				}
			}
		}

		internal static PackContent ContentOf(PackInfo p)
		{
			lock (DecodeGate)
			{
				if (Decoded.TryGetValue(p.Key, out var value) && value.Sig == p.Sig)
				{
					return value;
				}
			}
			Stopwatch stopwatch = Stopwatch.StartNew();
			PackContent packContent = new PackContent
			{
				Key = p.Key,
				Stamp = p.Stamp,
				Sig = p.Sig
			};
			foreach (KeyValuePair<string, string> modelFile in p.ModelFiles)
			{
				try
				{
					byte[] data = File.ReadAllBytes(modelFile.Value);
					List<string> list = new List<string>();
					MeshModel meshModel = Glb.Read(data, modelFile.Key, Limits, list, out var extras);
					foreach (string item in list)
					{
						packContent.Problems.Add("models/" + modelFile.Key + ".glb: " + item);
					}
					if (meshModel != null)
					{
						meshModel = Finish(p, modelFile.Key, meshModel, extras, packContent.Problems);
						ReadLinkedImages(p, modelFile.Key, modelFile.Value, meshModel);
						PackTextures.Note("models/" + modelFile.Key + ".glb", meshModel, packContent.Problems, packContent.Warnings);
						packContent.Models[modelFile.Key] = meshModel;
						if (extras != null)
						{
							packContent.ModelExtras[modelFile.Key] = extras;
						}
					}
				}
				catch (Exception ex)
				{
					packContent.Problems.Add("models/" + modelFile.Key + ".glb: " + Short(ex.Message));
				}
			}
			foreach (KeyValuePair<string, List<string>> soundFile in p.SoundFiles)
			{
				List<byte[]> list2 = new List<byte[]>();
				foreach (string item2 in soundFile.Value)
				{
					try
					{
						byte[] array = File.ReadAllBytes(item2);
						string text = SoundProblem(array, Path.GetExtension(item2).ToLowerInvariant());
						if (text != null)
						{
							packContent.Problems.Add("sounds/" + soundFile.Key + "/" + Path.GetFileName(item2) + ": " + text);
						}
						else
						{
							list2.Add(array);
						}
					}
					catch (Exception ex2)
					{
						packContent.Problems.Add("sounds/" + soundFile.Key + "/" + Path.GetFileName(item2) + ": " + Short(ex2.Message));
					}
				}
				if (list2.Count > 0)
				{
					packContent.Sounds[soundFile.Key] = list2;
				}
			}
			foreach (KeyValuePair<string, string> langFile in p.LangFiles)
			{
				try
				{
					packContent.Lang[langFile.Key] = File.ReadAllText(langFile.Value, Encoding.UTF8);
				}
				catch (Exception ex3)
				{
					packContent.Problems.Add("lang/" + langFile.Key + ".txt: " + Short(ex3.Message));
				}
			}
			packContent.Ms = stopwatch.Elapsed.TotalMilliseconds;
			lock (DecodeGate)
			{
				Decoded[p.Key] = packContent;
				return packContent;
			}
		}

		internal static MeshModel PictureModel(PackInfo p)
		{
			PackContent value;
			lock (DecodeGate)
			{
				if (!Decoded.TryGetValue(p.Key, out value) || value.Sig != p.Sig)
				{
					value = null;
				}
			}
			IEnumerable<string> collection;
			if (value == null)
			{
				IEnumerable<string> keys = p.ModelFiles.Keys;
				collection = keys;
			}
			else
			{
				IEnumerable<string> keys = value.Models.Keys;
				collection = keys;
			}
			List<string> list = new List<string>(collection);
			list.Sort(StringComparer.Ordinal);
			for (int i = 0; i < 2; i++)
			{
				foreach (string item in list)
				{
					if (IsLodName(item) != (i == 1))
					{
						continue;
					}
					if (value != null)
					{
						return value.Models[item];
					}
					try
					{
						GlbExtras extras;
						MeshModel meshModel = Glb.Read(File.ReadAllBytes(p.ModelFiles[item]), item, Limits, new List<string>(), out extras);
						if (meshModel == null)
						{
							continue;
						}
						return Finish(p, item, meshModel, extras, null, mayLoad: false);
					}
					catch
					{
					}
				}
			}
			return null;
		}

		private static string SoundProblem(byte[] d, string ext)
		{
			if (d.Length < 44)
			{
				return "too short to be a sound";
			}
			if (ext == ".wav")
			{
				if (d[0] != 82 || d[1] != 73 || d[2] != 70 || d[3] != 70 || d[8] != 87 || d[9] != 65 || d[10] != 86 || d[11] != 69)
				{
					return "not a wav file";
				}
				return null;
			}
			if (ext == ".ogg")
			{
				if (d[0] != 79 || d[1] != 103 || d[2] != 103 || d[3] != 83)
				{
					return "not an ogg file";
				}
				return null;
			}
			return "only .wav and .ogg are read";
		}

		internal static void ForgetContent(string key)
		{
			lock (DecodeGate)
			{
				Decoded.Remove(key);
			}
		}

		public static void FindFolders()
		{
			if (_home != null)
			{
				return;
			}
			_home = Log.Folder;
			try
			{
				_game = Directory.GetParent(Application.dataPath)?.FullName;
				string text = ((_game == null) ? null : Directory.GetParent(Directory.GetParent(_game).FullName)?.FullName);
				if (text != null)
				{
					_workshop = Path.Combine(text, "workshop", "content", 1206560u.ToString());
				}
			}
			catch (Exception ex)
			{
				Log.Info("packs: could not find the game's folders (" + ex.Message + ")");
			}
		}

		public static void Discover()
		{
			FindFolders();
			long num = ActiveSig();
			_all.Clear();
			try
			{
				Directory.CreateDirectory(LocalFolder);
			}
			catch
			{
			}
			foreach (string item in SubFolders(LocalFolder))
			{
				Add(LocalData(item), item, PackSource.Local, 0uL, local: true);
			}
			if (_game != null)
			{
				foreach (string item2 in SubFolders(Path.Combine(_game, "Mods")))
				{
					if (Directory.Exists(Path.Combine(item2, "Worldfall")))
					{
						Add(Path.Combine(item2, "Worldfall"), item2, PackSource.Mods, 0uL);
					}
				}
			}
			HashSet<ulong> hashSet = null;
			Dictionary<ulong, string> dictionary = new Dictionary<ulong, string>();
			if (!WorldBoxMod.SafeStart && !CrashGuard.Suspect("steam"))
			{
				CrashGuard.Enter("steam");
				try
				{
					hashSet = PackSteam.SubscribedOrNull();
					dictionary = PackSteam.InstalledFolders();
				}
				catch (Exception ex)
				{
					Log.Info("packs: no Steam (" + ex.Message + ")");
				}
				finally
				{
					CrashGuard.Leave("steam");
				}
			}
			else
			{
				Log.Info("packs: Workshop items not asked of Steam (crash guard)");
			}
			HashSet<ulong> hashSet2 = new HashSet<ulong>();
			if (_workshop != null)
			{
				foreach (string item3 in SubFolders(_workshop))
				{
					if (ulong.TryParse(Path.GetFileName(item3), out var result) && result != 3814551764u && (hashSet == null || hashSet.Contains(result)) && Directory.Exists(Path.Combine(item3, "Worldfall")) && hashSet2.Add(result))
					{
						Add(Path.Combine(item3, "Worldfall"), item3, PackSource.Workshop, result);
					}
				}
			}
			_elsewhere.Clear();
			foreach (KeyValuePair<ulong, string> item4 in dictionary)
			{
				if (hashSet2.Add(item4.Key) && item4.Key != 3814551764u && Directory.Exists(Path.Combine(item4.Value, "Worldfall")))
				{
					Add(Path.Combine(item4.Value, "Worldfall"), item4.Value, PackSource.Workshop, item4.Key);
				}
				if (_workshop == null || !item4.Value.StartsWith(_workshop, StringComparison.OrdinalIgnoreCase))
				{
					_elsewhere.Add(item4.Value);
				}
			}
			NoteScan();
			if (_started && ActiveSig() != num)
			{
				RequestApply();
			}
			Log.Info("packs: " + _all.Count + " found" + ((_all.Count > 0) ? (" (" + string.Join(", ", _all.ConvertAll((PackInfo p) => p.Key + ((p.Errors.Count > 0) ? (" " + p.Errors.Count + " errors") : "")).ToArray()) + ")") : ""));
		}

		internal static string LocalData(string dir)
		{
			if (File.Exists(Path.Combine(dir, "worldfall.json")))
			{
				return dir;
			}
			if (Directory.Exists(Path.Combine(dir, "Worldfall")))
			{
				return Path.Combine(dir, "Worldfall");
			}
			List<string> list = new List<string>(SubFolders(dir));
			if (list.Count == 1 && !IsLink(list[0]))
			{
				if (File.Exists(Path.Combine(list[0], "worldfall.json")))
				{
					return list[0];
				}
				if (File.Exists(Path.Combine(Path.Combine(list[0], "Worldfall"), "worldfall.json")))
				{
					return Path.Combine(list[0], "Worldfall");
				}
			}
			return dir;
		}

		private static bool LooksLikePack(string dir)
		{
			string[] array = new string[3] { "models", "sounds", "lang" };
			foreach (string path in array)
			{
				if (Directory.Exists(Path.Combine(dir, path)))
				{
					return true;
				}
			}
			try
			{
				return Directory.GetFiles(dir, "*.glb").Length != 0;
			}
			catch
			{
				return false;
			}
		}

		private static IEnumerable<string> SubFolders(string dir)
		{
			string[] array;
			try
			{
				array = (Directory.Exists(dir) ? Directory.GetDirectories(dir) : new string[0]);
			}
			catch
			{
				array = new string[0];
			}
			Array.Sort(array, StringComparer.OrdinalIgnoreCase);
			return array;
		}

		internal static PackInfo Read(string data, string modFolder, PackSource source, ulong item, bool local = false)
		{
			string text = Path.Combine(data, "worldfall.json");
			bool flag = !File.Exists(text);
			if (flag && (!local || !LooksLikePack(data)))
			{
				return null;
			}
			PackInfo packInfo = new PackInfo
			{
				Folder = data,
				ModFolder = modFolder,
				Source = source,
				WorkshopId = item
			};
			if (flag)
			{
				packInfo.Errors.Add("No worldfall.json in its folder: copy one from a New pack");
			}
			else
			{
				try
				{
					if (new FileInfo(text).Length > 65536)
					{
						throw new InvalidDataException("worldfall.json is over 64 KB");
					}
					JObject jObject = JObject.Parse(File.ReadAllText(text));
					packInfo.Id = (string)jObject["id"];
					packInfo.Title = (string)jObject["title"];
					packInfo.Author = (string)jObject["author"];
					packInfo.Version = (string)jObject["version"];
					packInfo.Description = (string)jObject["description"];
					packInfo.MinWorldfall = (string)jObject["worldfall"];
					if (jObject["kinds"] is JArray jArray)
					{
						foreach (JToken item2 in jArray)
						{
							AddOnce(packInfo.Kinds, (((string)item2) ?? "").Trim().ToLowerInvariant());
						}
					}
					if (jObject["loadAfter"] is JArray jArray2)
					{
						foreach (JToken item3 in jArray2)
						{
							AddOnce(packInfo.LoadAfter, (string)item3);
						}
					}
					if (jObject["requires"] is JArray jArray3)
					{
						foreach (JToken item4 in jArray3)
						{
							if (ulong.TryParse((string)item4, out var result) && result != 3814551764u && result != 3080294469u)
							{
								packInfo.Requires.Add(result);
							}
						}
					}
					if (item == 0L && ulong.TryParse((string)jObject["workshop"], out var result2))
					{
						packInfo.WorkshopId = result2;
					}
					if (jObject["fit"] != null)
					{
						if (jObject["fit"].Type == JTokenType.Boolean)
						{
							packInfo.Fit = (bool)jObject["fit"];
						}
						else
						{
							packInfo.Warnings.Add("worldfall.json: \"fit\" should be true or false");
						}
					}
					if (jObject["models"] is JObject jObject2)
					{
						foreach (KeyValuePair<string, JToken> item5 in jObject2)
						{
							if (!(item5.Value is JObject jObject3))
							{
								continue;
							}
							if (jObject3["info"] != null)
							{
								packInfo.ModelInfo[item5.Key] = (string)jObject3["info"];
							}
							if (jObject3["fit"] != null)
							{
								if (jObject3["fit"].Type == JTokenType.Boolean)
								{
									packInfo.ModelFit[item5.Key] = (bool)jObject3["fit"];
								}
								else
								{
									packInfo.Warnings.Add("worldfall.json: \"fit\" of " + item5.Key + " should be true or false");
								}
							}
							if (jObject3["textures"] != null)
							{
								string text2 = (((string)jObject3["textures"]) ?? "").Trim().ToLowerInvariant();
								if (text2 == "crisp" || text2 == "smooth")
								{
									packInfo.ModelTextures[item5.Key] = text2;
								}
								else
								{
									packInfo.Warnings.Add("worldfall.json: \"textures\" of " + item5.Key + " should be \"crisp\" or \"smooth\"");
								}
							}
						}
					}
				}
				catch (Exception ex)
				{
					packInfo.Errors.Add("worldfall.json can't be read: " + Short(ex.Message));
				}
			}
			if (string.IsNullOrEmpty(packInfo.Id) || !IdRx.IsMatch(packInfo.Id))
			{
				if (packInfo.Errors.Count == 0 && !flag)
				{
					packInfo.Errors.Add("worldfall.json needs an id of 2-64 lowercase letters, digits, dots, dashes or underscores");
				}
				packInfo.Id = Slug(Path.GetFileName(modFolder));
			}
			if (string.IsNullOrEmpty(packInfo.Title))
			{
				packInfo.Title = packInfo.Id;
			}
			if (string.IsNullOrEmpty(packInfo.Author))
			{
				packInfo.Author = ((source == PackSource.Workshop) ? "Workshop" : "Unknown");
			}
			if (string.IsNullOrEmpty(packInfo.Version))
			{
				packInfo.Version = "1.0";
			}
			string id = packInfo.Id;
			object obj;
			switch (source)
			{
			default:
				obj = "ws" + item;
				break;
			case PackSource.Mods:
				obj = "mods";
				break;
			case PackSource.Local:
				obj = "local";
				break;
			}
			packInfo.Key = id + "@" + (string)obj;
			packInfo.NewerWorldfall = !string.IsNullOrEmpty(packInfo.MinWorldfall) && CompareVersions(packInfo.MinWorldfall, "0.9.8.9") > 0;
			if (packInfo.NewerWorldfall)
			{
				packInfo.Warnings.Add("Made for Worldfall " + packInfo.MinWorldfall + " or later");
			}
			ScanContent(packInfo);
			return packInfo;
		}

		private static void Add(string data, string modFolder, PackSource source, ulong item, bool local = false)
		{
			try
			{
				PackInfo p = Read(data, modFolder, source, item, local);
				if (p != null)
				{
					if (_all.Exists((PackInfo o) => o.Key == p.Key))
					{
						Log.Info("packs: " + p.Key + " twice, the second at " + data + " is left out");
					}
					else
					{
						_all.Add(p);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("packs: " + data + ": " + ex.Message);
			}
		}

		private static void ScanContent(PackInfo p)
		{
			string text = Path.Combine(p.Folder, "worldfall.json");
			DateTime dateTime = (File.Exists(text) ? File.GetLastWriteTimeUtc(text) : DateTime.MinValue);
			long num = Mix(17L, text);
			string dir = Path.Combine(p.Folder, "models");
			foreach (string item in Files(p, dir, "*"))
			{
				string text2 = Path.GetExtension(item).ToLowerInvariant();
				if (text2 != ".glb")
				{
					switch (text2)
					{
					case ".gltf":
					case ".fbx":
					case ".obj":
					case ".blend":
					case ".dae":
					case ".stl":
						p.Warnings.Add("models/" + Path.GetFileName(item) + ": only .glb is read (Blender: Export > glTF 2.0, format glTF Binary)");
						break;
					}
					continue;
				}
				string text3 = Path.GetFileNameWithoutExtension(item).ToLowerInvariant();
				if (!NameRx.IsMatch(text3))
				{
					p.Errors.Add("models/" + Path.GetFileName(item) + ": a model name is lowercase letters, digits and _");
				}
				else if (Size(item) > 50331648)
				{
					p.Errors.Add("models/" + text3 + ".glb is over " + 48L + " MB");
				}
				else
				{
					p.ModelFiles[text3] = item;
					dateTime = Max(dateTime, item);
					num = Mix(num, item);
				}
			}
			foreach (string item2 in SubFolders(dir))
			{
				p.Warnings.Add("models/" + Path.GetFileName(item2) + "/: models go straight in models/, not in folders");
			}
			string dir2 = Path.Combine(p.Folder, "sounds");
			foreach (string item3 in Files(p, dir2, "*"))
			{
				if (IsSoundFile(item3))
				{
					p.Warnings.Add("sounds/" + Path.GetFileName(item3) + ": sounds go in sounds/<set>/ (sets in ../sound_sets.txt)");
				}
			}
			List<string> list = null;
			try
			{
				list = PackModelNames.SoundSets?.Invoke();
			}
			catch
			{
			}
			foreach (string item4 in SubFolders(dir2))
			{
				if (IsLink(item4))
				{
					p.Errors.Add("sounds/" + Path.GetFileName(item4) + " is a link");
					continue;
				}
				string text4 = Path.GetFileName(item4).ToLowerInvariant();
				if (!NameRx.IsMatch(text4))
				{
					p.Errors.Add("sounds/" + text4 + ": a sound set's name is lowercase letters, digits and _");
					continue;
				}
				List<string> list2 = new List<string>();
				foreach (string item5 in Files(p, item4, "*"))
				{
					if (!IsSoundFile(item5))
					{
						switch (Path.GetExtension(item5).ToLowerInvariant())
						{
						case ".mp3":
						case ".flac":
						case ".m4a":
						case ".aiff":
						case ".wma":
							p.Warnings.Add("sounds/" + text4 + "/" + Path.GetFileName(item5) + ": only .wav and .ogg are read");
							break;
						}
						continue;
					}
					if (Size(item5) > 16777216)
					{
						p.Errors.Add("sounds/" + text4 + "/" + Path.GetFileName(item5) + " is over " + 16L + " MB");
						continue;
					}
					if (list2.Count >= 32)
					{
						p.Warnings.Add("sounds/" + text4 + ": only the first " + 32 + " files are used");
						break;
					}
					list2.Add(item5);
					dateTime = Max(dateTime, item5);
					num = Mix(num, item5);
				}
				list2.Sort(StringComparer.OrdinalIgnoreCase);
				if (list2.Count > 0)
				{
					p.SoundFiles[text4] = list2;
				}
				if (list2.Count > 0 && list != null && list.Count > 0 && !list.Contains(text4))
				{
					p.Warnings.Add("sounds/" + text4 + ": no such sound set in Worldfall");
				}
			}
			foreach (string item6 in Files(p, Path.Combine(p.Folder, "lang"), "*"))
			{
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item6);
				if (Path.GetExtension(item6).Equals(".txt", StringComparison.OrdinalIgnoreCase) && !fileNameWithoutExtension.StartsWith("_") && !fileNameWithoutExtension.StartsWith("missing_") && !fileNameWithoutExtension.Equals("README", StringComparison.OrdinalIgnoreCase))
				{
					if (Size(item6) > 4194304)
					{
						p.Errors.Add("lang/" + fileNameWithoutExtension + ".txt is over " + 4L + " MB");
					}
					else
					{
						p.LangFiles[fileNameWithoutExtension] = item6;
						dateTime = Max(dateTime, item6);
						num = Mix(num, item6);
					}
				}
			}
			try
			{
				string[] files = Directory.GetFiles(p.Folder, "*.glb");
				foreach (string path in files)
				{
					p.Warnings.Add(Path.GetFileName(path) + ": models go in models/");
				}
			}
			catch
			{
			}
			string text5 = Path.Combine(p.Folder, "preview.png");
			if (File.Exists(text5) && !IsLink(text5))
			{
				if (Size(text5) > 2097152)
				{
					p.Warnings.Add("preview.png is over " + 2L + " MB");
				}
				else
				{
					p.Preview = text5;
				}
				num = Mix(num, text5);
			}
			p.Sig = num;
			p.Quick = QuickSig(p.Folder);
			p.Stamp = dateTime;
			if (p.ModelFiles.Count > 0)
			{
				AddOnce(p.Kinds, "models");
			}
			if (p.SoundFiles.Count > 0)
			{
				AddOnce(p.Kinds, "sounds");
			}
			if (p.LangFiles.Count > 0)
			{
				AddOnce(p.Kinds, "lang");
			}
			foreach (string kind in p.Kinds)
			{
				if (Array.IndexOf(KnownKinds, kind) < 0)
				{
					p.Warnings.Add("Kind " + kind + " is not known to this Worldfall");
				}
			}
			if (p.Overrides == 0 && !HasOtherKinds(p))
			{
				p.Warnings.Add("Nothing in it that Worldfall uses");
			}
			if (!Models.Loaded)
			{
				return;
			}
			foreach (string key in p.ModelFiles.Keys)
			{
				if (!Models.Known(key) && !PackModelNames.IsAskedFor(key) && !PackModelNames.IsMadeInCode(key))
				{
					p.Warnings.Add("models/" + key + ": no such model in Worldfall");
				}
			}
		}

		private static bool HasOtherKinds(PackInfo p)
		{
			foreach (string kind in p.Kinds)
			{
				if (kind != "models" && kind != "sounds" && kind != "lang")
				{
					return true;
				}
			}
			return false;
		}

		private static IEnumerable<string> Files(PackInfo p, string dir, string pattern)
		{
			if (!Directory.Exists(dir) || IsLink(dir))
			{
				return new string[0];
			}
			string[] files;
			try
			{
				files = Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly);
			}
			catch
			{
				return new string[0];
			}
			Array.Sort(files, StringComparer.OrdinalIgnoreCase);
			string value = Path.GetFullPath(p.Folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
			List<string> list = new List<string>();
			string[] array = files;
			foreach (string path in array)
			{
				string fullPath = Path.GetFullPath(path);
				if (!fullPath.StartsWith(value, StringComparison.OrdinalIgnoreCase) || IsLink(fullPath))
				{
					p.Errors.Add(Path.GetFileName(path) + " is a link or outside the pack");
				}
				else
				{
					list.Add(fullPath);
				}
			}
			return list;
		}

		private static bool IsLink(string path)
		{
			try
			{
				return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
			}
			catch
			{
				return true;
			}
		}

		internal static long QuickSig(string folder)
		{
			long num = 29L;
			if (!Directory.Exists(folder))
			{
				return num;
			}
			List<string> list = new List<string>();
			list.Add(folder);
			list.Add(Path.Combine(folder, "models"));
			list.Add(Path.Combine(folder, "lang"));
			list.Add(Path.Combine(folder, "sounds"));
			list.AddRange(SubFolders(Path.Combine(folder, "sounds")));
			foreach (string item in list)
			{
				if (!Directory.Exists(item))
				{
					continue;
				}
				string[] files;
				try
				{
					files = Directory.GetFiles(item);
				}
				catch
				{
					continue;
				}
				Array.Sort(files, StringComparer.OrdinalIgnoreCase);
				num = Mix(num, item);
				string[] array = files;
				foreach (string f in array)
				{
					num = Mix(num, f);
				}
				foreach (string item2 in SubFolders(item))
				{
					num = Mix(num, item2);
				}
			}
			return num;
		}

		private static bool IsSoundFile(string f)
		{
			string text = Path.GetExtension(f).ToLowerInvariant();
			if (!(text == ".wav"))
			{
				return text == ".ogg";
			}
			return true;
		}

		private static long Mix(long h, string f)
		{
			long num = -1L;
			long num2 = 0L;
			long num3 = 0L;
			try
			{
				FileInfo fileInfo = new FileInfo(f);
				if (fileInfo.Exists)
				{
					num = fileInfo.Length;
					num2 = fileInfo.LastWriteTimeUtc.Ticks;
					num3 = fileInfo.CreationTimeUtc.Ticks;
				}
			}
			catch
			{
			}
			string text = f.ToLowerInvariant();
			foreach (char c in text)
			{
				h = (h ^ c) * 1099511628211L;
			}
			long[] array = new long[3] { num, num2, num3 };
			foreach (long num4 in array)
			{
				h = (h ^ num4) * 1099511628211L;
			}
			return h;
		}

		private static long Size(string f)
		{
			try
			{
				return new FileInfo(f).Length;
			}
			catch
			{
				return long.MaxValue;
			}
		}

		private static DateTime Max(DateTime a, string f)
		{
			try
			{
				DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(f);
				return (lastWriteTimeUtc > a) ? lastWriteTimeUtc : a;
			}
			catch
			{
				return a;
			}
		}

		private static void AddOnce(List<string> list, string s)
		{
			if (!string.IsNullOrEmpty(s) && !list.Contains(s))
			{
				list.Add(s);
			}
		}

		internal static string Slug(string s)
		{
			StringBuilder stringBuilder = new StringBuilder();
			string text = (s ?? "").ToLowerInvariant();
			foreach (char c in text)
			{
				stringBuilder.Append(((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.') ? c : '_');
			}
			string text2 = stringBuilder.ToString().Trim('_');
			if (text2.Length >= 2)
			{
				if (text2.Length <= 64)
				{
					return text2;
				}
				return text2.Substring(0, 64);
			}
			return "pack_" + text2;
		}

		private static string Short(string s)
		{
			if (s != null)
			{
				if (s.Length <= 90)
				{
					return s;
				}
				return s.Substring(0, 90) + "...";
			}
			return "";
		}

		public static int CompareVersions(string a, string b)
		{
			string[] array = (a ?? "").Split('.', '-', ' ');
			string[] array2 = (b ?? "").Split('.', '-', ' ');
			for (int i = 0; i < Math.Max(array.Length, array2.Length); i++)
			{
				int.TryParse((i < array.Length) ? array[i] : "0", out var result);
				int.TryParse((i < array2.Length) ? array2[i] : "0", out var result2);
				if (result != result2)
				{
					return result.CompareTo(result2);
				}
			}
			return 0;
		}

		public static PackInfo Find(string key)
		{
			return _all.Find((PackInfo p) => p.Key == key);
		}

		public static string Remove(string key)
		{
			PackInfo packInfo = Find(key);
			if (packInfo == null)
			{
				return "That pack is gone already";
			}
			string text;
			if (packInfo.Source == PackSource.Workshop)
			{
				try
				{
					text = PackSteam.Unsubscribe(packInfo.WorkshopId);
				}
				catch (Exception ex)
				{
					text = "Steam is out of reach. " + ex.Message;
				}
			}
			else
			{
				text = ToBin(packInfo.ModFolder);
			}
			if (text != null)
			{
				return text;
			}
			_active.Remove(key);
			_all.Remove(packInfo);
			ForgetContent(key);
			Changed();
			Log.Info("packs: " + key + " removed (" + ((packInfo.Source == PackSource.Workshop) ? "unsubscribed" : "to the recycle bin") + ")");
			return null;
		}

		private static string ToBin(string folder)
		{
			try
			{
				if (!Directory.Exists(folder))
				{
					return null;
				}
				if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
				{
					ShFileOp op = new ShFileOp
					{
						wFunc = 3u,
						pFrom = Path.GetFullPath(folder) + "\0\0",
						fFlags = 1108
					};
					int num = SHFileOperation(ref op);
					if (num != 0 || op.fAnyOperationsAborted)
					{
						return "Could not move it to the recycle bin (" + num + ")";
					}
					return Directory.Exists(folder) ? "It is still there. Close anything using it" : null;
				}
				string text = Path.Combine(_home ?? Log.Folder, "removed_packs");
				Directory.CreateDirectory(text);
				string destDirName = Path.Combine(text, Path.GetFileName(folder) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
				Directory.Move(folder, destDirName);
				return null;
			}
			catch (Exception ex)
			{
				return "Could not remove it. " + ex.Message;
			}
		}

		[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
		private static extern int SHFileOperation(ref ShFileOp op);

		public static bool IsActive(string key)
		{
			return _active.Contains(key);
		}

		public static void Start(Settings s)
		{
			_settings = s;
			FindFolders();
			_active.Clear();
			string[] array = (s?.Packs ?? "").Split(',');
			foreach (string text in array)
			{
				if (text.Trim().Length > 0 && !_active.Contains(text.Trim()))
				{
					_active.Add(text.Trim());
				}
			}
			try
			{
				Discover();
			}
			catch (Exception ex)
			{
				Log.Error("packs: could not look for packs: " + ex.Message);
			}
			_started = true;
			int count = _active.Count;
			_active.RemoveAll((string k) => Find(k) == null);
			if (_active.Count != count)
			{
				Save();
			}
			RequestApply();
		}

		public static void Follow(Settings s)
		{
			if (s != _settings)
			{
				_settings = s;
			}
		}

		public static void Activate(string key, int at = -1)
		{
			PackInfo packInfo = Find(key);
			if (packInfo == null || _active.Contains(key))
			{
				return;
			}
			if (at < 0)
			{
				at = 0;
				for (int i = 0; i < _active.Count; i++)
				{
					PackInfo packInfo2 = Find(_active[i]);
					if (packInfo2 != null && packInfo2.LoadAfter.Contains(packInfo.Id))
					{
						at = i + 1;
					}
				}
			}
			_active.Insert(Math.Max(0, Math.Min(at, _active.Count)), key);
			Changed();
		}

		public static void Deactivate(string key)
		{
			if (_active.Remove(key))
			{
				Changed();
			}
		}

		public static void MoveTo(string key, int at)
		{
			int num = _active.IndexOf(key);
			if (num >= 0)
			{
				at = Math.Max(0, Math.Min(at, _active.Count - 1));
				if (at != num)
				{
					_active.RemoveAt(num);
					_active.Insert(at, key);
					Changed();
				}
			}
		}

		public static void SetActive(IList<string> keys)
		{
			_active.Clear();
			foreach (string key in keys)
			{
				if (Find(key) != null && !_active.Contains(key))
				{
					_active.Add(key);
				}
			}
			Changed();
		}

		public static void WatchActive()
		{
			List<string> watchChanged = _watchChanged;
			if (watchChanged != null)
			{
				_watchChanged = null;
				bool flag = false;
				foreach (string key in watchChanged)
				{
					int num = _all.FindIndex((PackInfo x) => x.Key == key);
					if (num < 0)
					{
						continue;
					}
					PackInfo packInfo = _all[num];
					PackInfo packInfo2 = null;
					try
					{
						packInfo2 = Read(packInfo.Folder, packInfo.ModFolder, packInfo.Source, packInfo.WorkshopId, packInfo.Source == PackSource.Local);
					}
					catch (Exception ex)
					{
						Log.Error("packs: " + packInfo.Folder + ": " + ex.Message);
					}
					if (packInfo2 != null && packInfo2.Key == packInfo.Key && packInfo2.Sig == packInfo.Sig)
					{
						_all[num] = packInfo2;
						continue;
					}
					if (packInfo2 == null)
					{
						_all.RemoveAt(num);
					}
					else
					{
						_all[num] = packInfo2;
					}
					Log.Info("packs: " + key + ((packInfo2 == null) ? " is gone from its folder" : ((packInfo2.Key != key) ? (" is now " + packInfo2.Key + " (its id changed)") : " changed in its folder")));
					flag = true;
				}
				if (flag)
				{
					RequestApply();
				}
			}
			if (_watching || Time.unscaledTime < _nextWatch || _active.Count == 0)
			{
				return;
			}
			_nextWatch = Time.unscaledTime + 2f;
			List<PackInfo> watch = new List<PackInfo>();
			foreach (PackInfo item in Active)
			{
				if (item.Source != PackSource.Workshop)
				{
					watch.Add(item);
				}
			}
			if (watch.Count == 0)
			{
				return;
			}
			_watching = true;
			ThreadPool.QueueUserWorkItem(delegate
			{
				List<string> list = new List<string>();
				try
				{
					foreach (PackInfo item2 in watch)
					{
						if (QuickSig(item2.Folder) != item2.Quick)
						{
							list.Add(item2.Key);
						}
					}
				}
				catch (Exception ex2)
				{
					Log.Info("packs: could not look at the pack folders (" + ex2.Message + ")");
				}
				if (list.Count > 0)
				{
					_watchChanged = list;
				}
				_watching = false;
			});
		}

		public static void LookAgain()
		{
			if (_looking)
			{
				return;
			}
			_looking = true;
			string local = LocalFolder;
			string mods = ((_game != null) ? Path.Combine(_game, "Mods") : null);
			string workshop = _workshop;
			List<string> elsewhere = new List<string>(_elsewhere);
			SlowLane.Run(delegate
			{
				try
				{
					_lookSig = FolderSig(local, mods, workshop, elsewhere);
					_looked = true;
				}
				catch (Exception ex)
				{
					Log.Info("packs: could not look at the pack folders (" + ex.Message + ")");
				}
				finally
				{
					_looking = false;
				}
			});
		}

		private static long FolderSig(string local, string mods, string workshop, List<string> elsewhere)
		{
			long num = 37L;
			foreach (string item in SubFolders(local))
			{
				num = Mix(num, item) * 31 + QuickSig(LocalData(item));
			}
			string[] array = new string[2] { mods, workshop };
			foreach (string text in array)
			{
				if (text == null)
				{
					continue;
				}
				foreach (string item2 in SubFolders(text))
				{
					num = Seen(num, item2);
				}
			}
			foreach (string item3 in elsewhere)
			{
				num = Seen(num, item3);
			}
			return num;
		}

		private static long Seen(long h, string dir)
		{
			string text = Path.Combine(dir, "Worldfall");
			h = Mix(h, dir);
			if (!Directory.Exists(text))
			{
				return h;
			}
			return h * 31 + QuickSig(text);
		}

		private static void NoteScan()
		{
			try
			{
				_scanSig = FolderSig(LocalFolder, (_game != null) ? Path.Combine(_game, "Mods") : null, _workshop, _elsewhere);
				_scanned = true;
			}
			catch (Exception ex)
			{
				_scanned = false;
				Log.Info("packs: could not look at the pack folders (" + ex.Message + ")");
			}
		}

		public static bool TakeLook(out bool differs)
		{
			differs = false;
			if (!_looked)
			{
				return false;
			}
			_looked = false;
			differs = !_scanned || _lookSig != _scanSig;
			return true;
		}

		private static long ActiveSig()
		{
			long num = 23L;
			foreach (PackInfo item in Active)
			{
				num = (num * 31 + item.Key.GetHashCode()) * 31 + item.Sig;
			}
			return num;
		}

		private static void Changed()
		{
			Save();
			RequestApply();
		}

		private static void Save()
		{
			if (_settings == null)
			{
				return;
			}
			_settings.Packs = string.Join(",", _active.ToArray());
			try
			{
				_settings.Save();
			}
			catch (Exception ex)
			{
				Log.Error("packs: could not save the list: " + ex.Message);
			}
		}

		public static Dictionary<string, List<PackInfo>> Clashes()
		{
			Dictionary<string, List<PackInfo>> dictionary = new Dictionary<string, List<PackInfo>>(StringComparer.Ordinal);
			foreach (PackInfo item in Active)
			{
				foreach (string key in item.ModelFiles.Keys)
				{
					Provider(dictionary, "model:" + key, item);
				}
				foreach (string key2 in item.SoundFiles.Keys)
				{
					Provider(dictionary, "sound:" + key2, item);
				}
				foreach (string key3 in item.LangFiles.Keys)
				{
					Provider(dictionary, "lang:" + key3, item);
				}
			}
			return dictionary;
		}

		private static void Provider(Dictionary<string, List<PackInfo>> map, string key, PackInfo p)
		{
			if (!map.TryGetValue(key, out var value))
			{
				value = (map[key] = new List<PackInfo>());
			}
			value.Add(p);
		}

		private static bool IsLength(string key)
		{
			if (LengthKeys.Contains(key))
			{
				return true;
			}
			string[] lengthPrefixes = LengthPrefixes;
			foreach (string value in lengthPrefixes)
			{
				if (key.StartsWith(value, StringComparison.Ordinal))
				{
					return true;
				}
			}
			return false;
		}

		internal static float LibraryUnit(string name, MeshModel original)
		{
			string[] bodyHeightKinds = BodyHeightKinds;
			foreach (string value in bodyHeightKinds)
			{
				if (name.StartsWith(value, StringComparison.Ordinal))
				{
					return 0.5714286f;
				}
			}
			if (original != null)
			{
				if (!(original.Info("units") == "tiles") && !(original.Info("kind") == "vent"))
				{
					return 1f;
				}
				return 0.5714286f;
			}
			bodyHeightKinds = TileModels;
			foreach (string value2 in bodyHeightKinds)
			{
				if (name.StartsWith(value2, StringComparison.Ordinal))
				{
					return 0.5714286f;
				}
			}
			return 1f;
		}

		private static MeshModel ReplacedBy(string name, bool mayLoad)
		{
			MeshModel meshModel = Models.Original(name, mayLoad);
			if (meshModel == null && IsLodName(name))
			{
				meshModel = Models.Original(name.Substring(0, name.Length - 2), mayLoad);
			}
			if (meshModel == null && PackModelNames.IsMadeInCode(name))
			{
				meshModel = ToolMeshes.Fresh(name.Substring(5));
			}
			return meshModel;
		}

		internal static MeshModel Finish(PackInfo p, string name, MeshModel m, GlbExtras extras, List<string> notes, bool mayLoad = true)
		{
			MeshModel meshModel = ReplacedBy(name, mayLoad);
			List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
			AddPairs(list, m.InfoText());
			if (p.ModelInfo.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value))
			{
				AddPairs(list, value);
			}
			bool value2;
			bool num = (p.ModelFit.TryGetValue(name, out value2) ? value2 : p.Fit);
			float num2 = ((extras != null && extras.LibraryUnits) ? 1f : LibraryUnit(name, meshModel));
			if (num)
			{
				float num3 = Longest(m);
				if (meshModel != null && num3 < 1E-06f)
				{
					notes?.Add("models/" + name + ".glb: fit is on, but the model has no size to scale");
				}
				else if (meshModel != null)
				{
					num2 = Longest(meshModel) / num3;
				}
			}
			MeshModel meshModel2 = ((Math.Abs(num2 - 1f) < 1E-06f) ? m.Derive(m.Name, m.Parts) : Scaled(m, num2));
			meshModel2.ClearInfo();
			if (meshModel != null)
			{
				meshModel2.ParseInfo(meshModel.InfoText());
			}
			foreach (KeyValuePair<string, string> item in list)
			{
				meshModel2.ParseInfo(item.Key + "=" + ((IsLength(item.Key) && Math.Abs(num2 - 1f) >= 1E-06f) ? ScaleNumbers(item.Value, num2) : item.Value));
			}
			return meshModel2;
		}

		internal static float Longest(MeshModel m)
		{
			Vec3 vec = m.Max - m.Min;
			return Math.Max(vec.X, Math.Max(vec.Y, vec.Z));
		}

		internal static MeshModel Scaled(MeshModel m, float k)
		{
			MeshPart[] array = new MeshPart[m.Parts.Length];
			for (int i = 0; i < array.Length; i++)
			{
				MeshPart meshPart = m.Parts[i];
				float[] array2 = new float[meshPart.Pos.Length];
				for (int j = 0; j < array2.Length; j++)
				{
					array2[j] = meshPart.Pos[j] * k;
				}
				array[i] = new MeshPart
				{
					Name = meshPart.Name,
					Parent = meshPart.Parent,
					Pivot = meshPart.Pivot * k,
					Pos = array2,
					Nrm = meshPart.Nrm,
					Tri = meshPart.Tri,
					Slot = meshPart.Slot,
					Axis = meshPart.Axis,
					Uv = meshPart.Uv
				};
			}
			MeshModel meshModel = m.Derive(m.Name, array);
			meshModel.Min = m.Min * k;
			meshModel.Max = m.Max * k;
			return meshModel;
		}

		private static void AddPairs(List<KeyValuePair<string, string>> into, string info)
		{
			if (string.IsNullOrEmpty(info))
			{
				return;
			}
			string[] array = info.Split(';');
			foreach (string text in array)
			{
				int num = text.IndexOf('=');
				if (num > 0)
				{
					into.Add(new KeyValuePair<string, string>(text.Substring(0, num).Trim(), text.Substring(num + 1).Trim()));
				}
			}
		}

		internal static string ScaleNumbers(string value, float k)
		{
			string[] array = value.Split(',');
			string[] array2 = new string[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				if (!float.TryParse(array[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
				{
					return value;
				}
				array2[i] = (result * k).ToString("0.####", CultureInfo.InvariantCulture);
			}
			return string.Join(",", array2);
		}

		internal static string InfoInMetres(string name, MeshModel m)
		{
			float num = LibraryUnit(name, m);
			if (Math.Abs(num - 1f) < 1E-06f)
			{
				return m.InfoText();
			}
			List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
			AddPairs(list, m.InfoText());
			List<string> list2 = new List<string>();
			foreach (KeyValuePair<string, string> item in list)
			{
				list2.Add(item.Key + "=" + (IsLength(item.Key) ? ScaleNumbers(item.Value, 1f / num) : item.Value));
			}
			return string.Join(";", list2.ToArray());
		}
	}
}
