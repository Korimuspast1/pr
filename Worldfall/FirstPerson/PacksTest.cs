using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FirstPerson.Core;
using FirstPerson.Gpu;
using Steamworks;
using Steamworks.Ugc;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class PacksTest : CivilTest
	{
		private int _errors;

		private readonly List<string> _errorLines = new List<string>();

		private const string A = "wftest_a@local";

		private const string B = "wftest_b@local";

		private const string NewOne = "wftest_new@local";

		private const string Bad = "wftest_bad@local";

		private MeshModel _srcA;

		private MeshModel _srcB;

		private MeshModel _srcBeast;

		private Actor _host;

		private Actor _beast;

		private const string LiveKey = "wftest_live@local";

		private static readonly DateTime OldDate = new DateTime(2020, 1, 1, 12, 0, 0, DateTimeKind.Utc);

		private const string GuidePack = "guide_examples@local";

		private const string Tex = "wftest_textures@local";

		private const string Units = "wftest_units@local";

		protected override string Prefix => "PACKS";

		private bool LiveOnly
		{
			get
			{
				if (!Options.Contains("livepublish"))
				{
					return Options.Contains("livesubscribe");
				}
				return true;
			}
		}

		private void OnLog(string condition, string trace, LogType type)
		{
			switch (type)
			{
			case LogType.Error:
				if (!condition.Contains("packs"))
				{
					break;
				}
				goto case LogType.Exception;
			case LogType.Exception:
				_errors++;
				if (_errorLines.Count < 5)
				{
					_errorLines.Add((condition.Length > 160) ? condition.Substring(0, 160) : condition);
				}
				break;
			}
		}

		protected override IEnumerator Body()
		{
			Application.logMessageReceived += OnLog;
			List<string> wasActive = Packs.ActiveKeys;
			string wasLanguage = Mod.Settings.Language;
			try
			{
				yield return LiveOnly ? Live() : Offline();
			}
			finally
			{
				Application.logMessageReceived -= OnLog;
			}
			Packs.SetActive(wasActive);
			Mod.Settings.Language = wasLanguage;
			Check("no errors from packs or exceptions all along", _errors == 0, _errors + ((_errorLines.Count > 0) ? (": " + string.Join(" | ", _errorLines.ToArray())) : ""));
		}

		private IEnumerator Offline()
		{
			yield return Part("making the packs", MakePacks());
			yield return Part("finding them", Finding());
			yield return Part("a model, its far versions, a creature, sounds, text", PackA());
			if (Options.Contains("guide"))
			{
				yield return Part("the Modding page's example pack", Guide());
				yield return Part("all off", AllOff());
				yield break;
			}
			if (Options.Contains("onlytextures"))
			{
				yield return Part("textures: images on pack models", Textures());
				yield return Part("all off", AllOff());
				yield break;
			}
			if (Options.Contains("onlysizes"))
			{
				yield return Part("sizes: metres, fit, info, the dig shovel", Sizes());
				yield return Part("all off", AllOff());
				yield break;
			}
			yield return Part("two packs, the top one wins", Order());
			yield return Part("a pack for a newer Worldfall", Newer());
			yield return Part("toggled live", Stress());
			yield return Part("sizes: metres, fit, info, the dig shovel", Sizes());
			yield return Part("textures: images on pack models", Textures());
			yield return Part("all off", AllOff());
			yield return Part("the pack folder", Folder());
			yield return Part("publish dry run", DryRun());
			yield return Part("the Workshop, read only", Browse());
			yield return Part("the Mods window", Window());
			if (Options.Contains("modspack"))
			{
				yield return Part("a pack in the game's Mods folder", ModsFolder());
			}
		}

		private static string Manifest(string id, string title, string extra = "")
		{
			return "{\"id\":\"" + id + "\",\"title\":\"" + title + "\",\"author\":\"Selftest\",\"version\":\"1.0.0\",\"worldfall\":\"0.9\"" + extra + "}";
		}

		private IEnumerator MakePacks()
		{
			while (!Models.Ready)
			{
				yield return null;
			}
			string[] array = (Directory.Exists(Packs.LocalFolder) ? Directory.GetDirectories(Packs.LocalFolder, "wftest_*") : new string[0]);
			for (int i = 0; i < array.Length; i++)
			{
				Directory.Delete(array[i], recursive: true);
			}
			_srcA = Models.Own("windmill") ?? Models.Own("rock_plain");
			_srcB = Models.Own("tree_palm") ?? Models.Own("tree_oak");
			_srcBeast = Models.Own("animal_wolf");
			Check("library models to copy", _srcA != null && _srcB != null && _srcBeast != null);
			if (_srcA != null && _srcB != null && _srcBeast != null)
			{
				string dir = MakePack("wftest_a", Manifest("wftest_a", "Test A"));
				Put(dir, "models/tree_pine.glb", ToGlb(_srcA));
				Put(dir, "models/animal_wftestbeast.glb", ToGlb(_srcBeast));
				Put(dir, "sounds/heart/one.wav", Tone(220f, 0.3f));
				Put(dir, "sounds/heart/two.wav", Tone(330f, 0.3f));
				Put(dir, "lang/cz.txt", Encoding.UTF8.GetBytes("Field of view = PACK-A-FOV\n"));
				string dir2 = MakePack("wftest_b", Manifest("wftest_b", "Test B", ",\"loadAfter\":[\"wftest_a\"]"));
				Put(dir2, "models/tree_pine.glb", ToGlb(_srcB));
				Put(dir2, "models/tree_pine_m.glb", ToGlb(_srcB));
				Put(dir2, "lang/cz.txt", Encoding.UTF8.GetBytes("Field of view = PACK-B-FOV\n"));
				string dir3 = MakePack("wftest_bad", "{\"title\":\"Broken\"}");
				byte[] array2 = ToGlb(_srcA);
				Put(dir3, "models/Bad-Name.glb", array2);
				Put(dir3, "models/tree_oak.glb", Cut(array2, array2.Length / 2));
				Put(dir3, "models/rock_plain.glb", WithNaN(array2));
				Put(dir3, "models/tree_birch.glb", array2);
				Put(dir3, "sounds/heart/noise.wav", Encoding.ASCII.GetBytes(new string('x', 100)));
				MakePack("wftest_json", "{ this is not json");
				Put(MakePack("wftest_new", Manifest("wftest_new", "Test New").Replace("\"worldfall\":\"0.9\"", "\"worldfall\":\"99.0\"")), "lang/cz.txt", Encoding.UTF8.GetBytes("Walking pace = PACK-NEW\n"));
				Note("packs written to " + Packs.LocalFolder);
			}
		}

		private static byte[] Cut(byte[] d, int n)
		{
			byte[] array = new byte[n];
			Array.Copy(d, array, n);
			return array;
		}

		private static byte[] WithNaN(byte[] d)
		{
			byte[] array = (byte[])d.Clone();
			int num = BitConverter.ToInt32(array, 12);
			int destinationIndex = 20 + num + 8;
			Array.Copy(BitConverter.GetBytes(float.NaN), 0, array, destinationIndex, 4);
			return array;
		}

		private IEnumerator Finding()
		{
			Packs.Discover();
			PackInfo packInfo = Packs.Find("wftest_a@local");
			PackInfo packInfo2 = Packs.Find("wftest_b@local");
			PackInfo packInfo3 = Packs.Find("wftest_bad@local");
			PackInfo packInfo4 = Packs.Find("wftest_json@local");
			PackInfo packInfo5 = Packs.Find("wftest_new@local");
			Check("the packs found", packInfo != null && packInfo2 != null && packInfo3 != null && packInfo4 != null && packInfo5 != null, string.Join(", ", Packs.All.ConvertAll((PackInfo p) => p.Key).ToArray()));
			if (packInfo != null && packInfo3 != null)
			{
				Check("pack A's contents by name", packInfo.ModelFiles.Count == 2 && packInfo.SoundFiles.ContainsKey("heart") && packInfo.SoundFiles["heart"].Count == 2 && packInfo.LangFiles.ContainsKey("cz"), packInfo.ModelFiles.Count + " models, " + packInfo.SoundFiles.Count + " sets, " + packInfo.LangFiles.Count + " languages; kinds " + string.Join(",", packInfo.Kinds.ToArray()));
				Check("no errors on pack A", packInfo.Errors.Count == 0, string.Join(" | ", packInfo.Errors.ToArray()) + " / warnings " + string.Join(" | ", packInfo.Warnings.ToArray()));
				Check("a manifest without an id is an error", packInfo3.Errors.Exists((string e) => e.Contains("id")), string.Join(" | ", packInfo3.Errors.ToArray()));
				Check("a bad model name is an error", packInfo3.Errors.Exists((string e) => e.Contains("Bad-Name")));
				Check("a manifest that isn't JSON is an error", packInfo4 != null && packInfo4.Errors.Count > 0, (packInfo4 != null) ? string.Join(" | ", packInfo4.Errors.ToArray()) : "");
				PackContent packContent = Packs.ContentOf(packInfo3);
				Check("a cut GLB refused, a NaN GLB refused, a fake WAV refused", packContent.Problems.Exists((string p) => p.Contains("tree_oak")) && packContent.Problems.Exists((string p) => p.Contains("rock_plain")) && packContent.Problems.Exists((string p) => p.Contains("noise")), string.Join(" | ", packContent.Problems.ToArray()));
				Check("and the good file beside them loads", packContent.Models.ContainsKey("tree_birch") && !packContent.Models.ContainsKey("tree_oak") && !packContent.Models.ContainsKey("rock_plain"));
				PackContent packContent2 = Packs.ContentOf(packInfo);
				MeshModel meshModel = (packContent2.Models.ContainsKey("tree_pine") ? packContent2.Models["tree_pine"] : null);
				Check("a GLB read back as the model it was written from", meshModel != null && Same(meshModel, _srcA), (meshModel == null) ? string.Join(" | ", packContent2.Problems.ToArray()) : (Describe(meshModel) + " vs " + Describe(_srcA)));
				yield return null;
			}
		}

		private static string Describe(MeshModel m)
		{
			int num = 0;
			MeshPart[] parts = m.Parts;
			foreach (MeshPart meshPart in parts)
			{
				num += meshPart.TriangleCount;
			}
			return m.Parts.Length + " parts, " + m.SlotNames.Length + " slots, " + num + " triangles, " + m.Min.ToString() + ".." + m.Max;
		}

		private static bool Same(MeshModel a, MeshModel b)
		{
			if (a.Parts.Length != b.Parts.Length)
			{
				return false;
			}
			for (int i = 0; i < a.Parts.Length; i++)
			{
				if (a.Parts[i].TriangleCount != b.Parts[i].TriangleCount || a.Parts[i].Name != b.Parts[i].Name || a.Parts[i].Parent != b.Parts[i].Parent)
				{
					return false;
				}
				Vec3 vec = a.Parts[i].Pivot - b.Parts[i].Pivot;
				if (Math.Abs(vec.X) + Math.Abs(vec.Y) + Math.Abs(vec.Z) > 0.001f)
				{
					return false;
				}
			}
			Vec3 vec2 = a.Min - b.Min;
			Vec3 vec3 = a.Max - b.Max;
			return Math.Abs(vec2.X) + Math.Abs(vec2.Y) + Math.Abs(vec2.Z) + Math.Abs(vec3.X) + Math.Abs(vec3.Y) + Math.Abs(vec3.Z) < 0.001f;
		}

		private IEnumerator Applied(float limit = 15f)
		{
			float until = Time.unscaledTime + limit;
			while (Packs.Busy && Time.unscaledTime < until)
			{
				yield return null;
			}
			yield return null;
			Check("applied in time", !Packs.Busy, "swap " + Packs.LastApplyMs.ToString("0.0") + " ms, " + Packs.LastCleared + " tables cleared");
		}

		private IEnumerator PackA()
		{
			Packs.SetActive(new string[1] { "wftest_a@local" });
			yield return Applied();
			PackContent packContent = Packs.ContentOf(Packs.Find("wftest_a@local"));
			MeshModel meshModel = Models.Get("tree_pine");
			Check("tree_pine is the pack's", meshModel != null && meshModel == packContent.Models["tree_pine"]);
			Check("its middle and far versions are hidden", Models.Get("tree_pine_m") == null && Models.Get("tree_pine_f") == null && !Models.Has("tree_pine_m"));
			Check("so at 40 and 100 tiles the pack's model is drawn", Models.Detailed("tree_pine", 40f) == meshModel && Models.Detailed("tree_pine", 100f) == meshModel);
			Check("another tree untouched", Models.Get("tree_palm") == Models.Own("tree_palm"));
			List<string> found = PackCaches.Found;
			List<string> missing = PackCaches.Missing;
			Check("the model caches found", found.Count >= 30 && missing.Count == 0, found.Count + " tables" + ((missing.Count > 0) ? ("; missing " + string.Join(", ", missing.ToArray())) : ""));
			Note("tables: " + string.Join(" ", found.ToArray()));
			MethodInfo method = typeof(Models).GetMethod("PackCreature", BindingFlags.Static | BindingFlags.NonPublic);
			Check("an unknown creature asks for animal_<id>", method != null && (string)method.Invoke(null, new object[1] { "wftestbeast" }) == "animal_wftestbeast");
			bool num = CloneAsset("wftestbeast", "wolf");
			WorldTile worldTile = CivilTest.Stage();
			_host = CivilTest.Spawn("human", worldTile.posV);
			if (num)
			{
				_beast = CivilTest.Spawn("wftestbeast", (Vector2)worldTile.posV + new Vector2(2.5f, 0f));
			}
			if (_host == null)
			{
				yield break;
			}
			yield return Possess(_host);
			Check("first person", InControl(_host));
			if (_beast != null)
			{
				Mod.DebugEyeSet = false;
				bool seen = false;
				for (int i = 0; i < 60; i++)
				{
					if (seen)
					{
						break;
					}
					yield return null;
					foreach (MeshInstance mesh in Mod.SceneForTest.Meshes)
					{
						if (mesh.Model != null && mesh.Model.Name.StartsWith("animal_wftestbeast", StringComparison.Ordinal))
						{
							seen = true;
						}
					}
				}
				Check("the modded creature drawn with the pack's model", seen, "creature id " + _beast.asset.id + ", family " + Models.CreatureFamily(_beast));
			}
			else
			{
				Note("could not clone a creature asset: the modded creature checked by its lookup only");
			}
			float until = Time.unscaledTime + 10f;
			while (!Mod.Audio.SetFromPack("heart") && Time.unscaledTime < until)
			{
				yield return null;
			}
			Check("the heartbeat is the pack's", Mod.Audio.SetFromPack("heart") && Mod.Audio.SetSize("heart") == 2, "size " + Mod.Audio.SetSize("heart"));
			Mod.Settings.Language = "cz";
			yield return null;
			yield return null;
			Check("the pack's text in Chinese", Lang.T("Field of view") == "PACK-A-FOV", Lang.T("Field of view"));
			Check("the shipped lines still there", Lang.T("Walking pace") != "Walking pace", Lang.T("Walking pace"));
		}

		private bool CloneAsset(string id, string from)
		{
			try
			{
				object actor_library = AssetManager.actor_library;
				if (AssetManager.actor_library.get(id) != null)
				{
					return true;
				}
				MethodInfo method = actor_library.GetType().GetMethod("clone", new Type[2]
				{
					typeof(string),
					typeof(string)
				});
				if (method == null)
				{
					return false;
				}
				method.Invoke(actor_library, new object[2] { id, from });
				return AssetManager.actor_library.get(id) != null;
			}
			catch (Exception ex)
			{
				Note("clone failed: " + ex.Message);
				return false;
			}
		}

		private IEnumerator Order()
		{
			PackContent ca = Packs.ContentOf(Packs.Find("wftest_a@local"));
			PackContent cb = Packs.ContentOf(Packs.Find("wftest_b@local"));
			Packs.Activate("wftest_b@local");
			yield return Applied();
			Check("B went above A (loadAfter)", Packs.ActiveKeys.IndexOf("wftest_b@local") == 0, string.Join(", ", Packs.ActiveKeys.ToArray()));
			Check("the top pack's tree_pine wins", Models.Get("tree_pine") == cb.Models["tree_pine"]);
			Check("and its middle version is drawn", Models.Get("tree_pine_m") == cb.Models["tree_pine_m"] && Models.Get("tree_pine_f") == null);
			Dictionary<string, List<PackInfo>> dictionary = Packs.Clashes();
			Check("the clash shown, B winning over A", dictionary.ContainsKey("model:tree_pine") && dictionary["model:tree_pine"].Count == 2 && dictionary["model:tree_pine"][0].Key == "wftest_b@local");
			Check("B's text wins", Lang.T("Field of view") == "PACK-B-FOV", Lang.T("Field of view"));
			Check("the creature still A's", Models.Get("animal_wftestbeast") == ca.Models["animal_wftestbeast"]);
			Packs.MoveTo("wftest_a@local", 0);
			yield return Applied();
			Check("A moved to the top wins again", Models.Get("tree_pine") == ca.Models["tree_pine"] && Models.Get("tree_pine_m") == null, string.Join(", ", Packs.ActiveKeys.ToArray()));
			Check("and A's text", Lang.T("Field of view") == "PACK-A-FOV", Lang.T("Field of view"));
		}

		private IEnumerator Newer()
		{
			PackInfo packInfo = Packs.Find("wftest_new@local");
			Check("made for a newer Worldfall", packInfo != null && packInfo.NewerWorldfall && packInfo.Warnings.Count > 0);
			PacksUi.Toggle("wftest_new@local");
			Check("turning it on asks first", PacksUi.Confirm == "newer" && !Packs.IsActive("wftest_new@local"));
			PacksUi.Confirmed();
			Check("yes turns it on", Packs.IsActive("wftest_new@local"));
			yield return Applied();
			Check("its text in", Lang.T("Walking pace") == "PACK-NEW", Lang.T("Walking pace"));
			Packs.Deactivate("wftest_new@local");
			yield return Applied();
		}

		private IEnumerator Stress()
		{
			System.Random rng = new System.Random(7);
			string[] keys = new string[4] { "wftest_a@local", "wftest_b@local", "wftest_new@local", "wftest_bad@local" };
			int errorsBefore = _errors;
			for (int i = 0; i < 60; i++)
			{
				string key = keys[rng.Next(keys.Length)];
				switch (rng.Next(4))
				{
				case 0:
					Packs.Activate(key);
					break;
				case 1:
					Packs.Deactivate(key);
					break;
				case 2:
					Packs.MoveTo(key, rng.Next(4));
					break;
				default:
				{
					List<string> list = new List<string>();
					string[] array = keys;
					foreach (string item in array)
					{
						if (rng.Next(2) == 0)
						{
							list.Add(item);
						}
					}
					Packs.SetActive(list);
					break;
				}
				}
				int frames = rng.Next(3);
				for (int f = 0; f < frames; f++)
				{
					yield return null;
				}
			}
			yield return Applied(20f);
			for (int i = 0; i < 10; i++)
			{
				yield return null;
			}
			Check("no errors while toggling", _errors == errorsBefore, string.Join(" | ", _errorLines.ToArray()));
			Dictionary<string, MeshModel> dictionary = new Dictionary<string, MeshModel>();
			List<PackInfo> active = Packs.Active;
			for (int num = active.Count - 1; num >= 0; num--)
			{
				Dictionary<string, MeshModel> models = Packs.ContentOf(active[num]).Models;
				foreach (KeyValuePair<string, MeshModel> item2 in models)
				{
					dictionary[item2.Key] = item2.Value;
					if (Packs.IsLodName(item2.Key))
					{
						continue;
					}
					string[] array = new string[2]
					{
						item2.Key + "_m",
						item2.Key + "_f"
					};
					foreach (string key2 in array)
					{
						if (!models.ContainsKey(key2))
						{
							dictionary[key2] = null;
						}
					}
				}
			}
			int num2 = 0;
			foreach (KeyValuePair<string, MeshModel> item3 in dictionary)
			{
				if (Models.Get(item3.Key) != item3.Value)
				{
					num2++;
				}
			}
			Check("every name the top pack's after the last change", num2 == 0, num2 + " wrong of " + dictionary.Count + "; list " + string.Join(", ", Packs.ActiveKeys.ToArray()));
			int num3 = 0;
			int num4 = 0;
			string text = null;
			foreach (MeshInstance mesh in Mod.SceneForTest.Meshes)
			{
				if (mesh.Model == null || mesh.Model.Name == null)
				{
					continue;
				}
				MeshModel meshModel = Models.Get(mesh.Model.Name);
				if (Models.Own(mesh.Model.Name) == null && !IsPackModel(mesh.Model))
				{
					continue;
				}
				num4++;
				if (meshModel != mesh.Model)
				{
					num3++;
					if (text == null)
					{
						text = mesh.Model.Name;
					}
				}
			}
			Check("no old model in the scene", num3 == 0 && num4 > 0, num3 + " of " + num4 + ((text != null) ? (", first " + text) : ""));
			Check("still in first person", InControl(_host));
		}

		private static bool IsPackModel(MeshModel m)
		{
			string[] array = new string[4] { "wftest_a@local", "wftest_b@local", "wftest_new@local", "wftest_bad@local" };
			for (int i = 0; i < array.Length; i++)
			{
				PackInfo packInfo = Packs.Find(array[i]);
				if (packInfo != null && Packs.ContentOf(packInfo).Models.ContainsValue(m))
				{
					return true;
				}
			}
			return false;
		}

		private IEnumerator AllOff()
		{
			Packs.SetActive(new string[0]);
			yield return Applied();
			Check("tree_pine the mod's own again", Models.Get("tree_pine") == Models.Own("tree_pine") && Models.Get("tree_pine_m") != null);
			Check("the creature without a model again", Models.Get("animal_wftestbeast") == null);
			for (int i = 0; i < 5; i++)
			{
				if (!Mod.Audio.SetFromPack("heart"))
				{
					break;
				}
				yield return null;
			}
			Check("the heartbeat the mod's own", !Mod.Audio.SetFromPack("heart") && Mod.Audio.SetSize("heart") >= 1, "size " + Mod.Audio.SetSize("heart"));
			Check("the shipped translation back", Lang.T("Field of view") != "PACK-A-FOV" && Lang.T("Field of view") != "PACK-B-FOV", Lang.T("Field of view"));
			Check("the list saved empty", Mod.Settings.Packs == "", Mod.Settings.Packs);
		}

		private IEnumerator DryRun()
		{
			PackPublish.Job job = PackPublish.Start(Packs.Find("wftest_a@local"), 0, "selftest", dry: true);
			Check("the dry run done", job.Done && !job.Failed, job.State);
			if (job.Failed)
			{
				yield break;
			}
			string stage = job.Stage;
			Check("mod.json requires Worldfall", File.Exists(Path.Combine(stage, "mod.json")) && File.ReadAllText(Path.Combine(stage, "mod.json")).Contains("s3cond2.worldfall"));
			Check("the stub source", File.Exists(Path.Combine(Path.Combine(stage, "Code"), "Pack.cs")) && File.ReadAllText(Path.Combine(Path.Combine(stage, "Code"), "Pack.cs")).Contains("BasicMod<Pack>"));
			Check("the pack's files under Worldfall/", File.Exists(Path.Combine(stage, "Worldfall/models/tree_pine.glb")) && File.Exists(Path.Combine(stage, "Worldfall/sounds/heart/one.wav")) && File.Exists(Path.Combine(stage, "Worldfall/worldfall.json")));
			Check("a preview drawn from its model", File.Exists(Path.Combine(stage, "Worldfall/preview.png")) && File.Exists(Path.Combine(stage, "icon.png")) && new FileInfo(Path.Combine(stage, "icon.png")).Length > 1000);
			foreach (string item in job.Report)
			{
				Note("dry run: " + item.Replace("\n", "\n    "));
			}
			Check("nothing published: no Workshop id kept", Packs.Find("wftest_a@local").WorkshopId == 0);
			yield return null;
		}

		private IEnumerator Browse()
		{
			bool flag;
			try
			{
				flag = PackSteam.Available;
			}
			catch
			{
				flag = false;
			}
			if (!flag)
			{
				Note("no Steam here: the Workshop query left out");
				yield break;
			}
			PackSteam.Query("", null, 0, 1);
			float until = Time.unscaledTime + 30f;
			while (PackSteam.Querying && Time.unscaledTime < until)
			{
				yield return null;
			}
			Check("the Workshop answered the packs query", !PackSteam.Querying && !PackSteam.Status.StartsWith("The Workshop") && !PackSteam.Status.StartsWith("No answer"), PackSteam.Status + ", " + PackSteam.Total + " packs");
			PackSteam.AnyWithTag = "Unverified Mod";
			PackSteam.Query("", null, 3, 1);
			until = Time.unscaledTime + 30f;
			while (PackSteam.Querying && Time.unscaledTime < until)
			{
				yield return null;
			}
			Check("real items listed (any mod)", PackSteam.Results.Count > 0, PackSteam.Status);
			foreach (PackSteam.Entry item in PackSteam.Results.GetRange(0, Math.Min(8, PackSteam.Results.Count)))
			{
				Note("item " + item.Id + " \"" + item.Title + "\" by " + item.Author + ", +" + item.Up + " -" + item.Down + ", " + item.Size + " bytes");
			}
			for (int i = 0; i < 4 && i < PackSteam.Results.Count; i++)
			{
				PackSteam.WantPreview(PackSteam.Results[i]);
			}
			until = Time.unscaledTime + 15f;
			while (Time.unscaledTime < until && !PackSteam.Results.Exists((PackSteam.Entry e) => e.Preview != null))
			{
				yield return null;
			}
			Check("a preview image loaded", PackSteam.Results.Exists((PackSteam.Entry e) => e.Preview != null));
			Check("subscribed state readable", PackSteam.IsSubscribed(3814551764uL) || !PackSteam.IsSubscribed(3814551764uL));
			PackSteam.AnyWithTag = null;
		}

		private IEnumerator Window()
		{
			Packs.SetActive(new string[2] { "wftest_a@local", "wftest_b@local" });
			yield return Applied();
			bool capture = Options.Contains("capture");
			Mod.SetSettingsMenu(open: true);
			Mod.SettingsMenu.Tab = 7;
			yield return CivilTest.Wait(0.6f);
			if (capture)
			{
				yield return Shot("packs_f2.png");
			}
			PacksUi.OpenAt(0);
			yield return CivilTest.Wait(0.8f);
			Check("the window open", PacksUi.Open && Mod.SettingsMenu.Open);
			if (capture)
			{
				yield return Shot("packs_installed.png");
			}
			PacksUi.FocusOn("wftest_b@local");
			PacksUi.Details = "wftest_b@local";
			yield return CivilTest.Wait(0.5f);
			if (capture)
			{
				yield return Shot("packs_details.png");
			}
			PacksUi.Details = null;
			PacksUi.AskRemove("wftest_bad@local");
			yield return CivilTest.Wait(0.4f);
			if (capture)
			{
				yield return Shot("packs_confirm.png");
			}
			PacksUi.Confirm = null;
			PacksUi.Page = 1;
			PacksUi.Search = "wftest no such pack";
			PacksUi.RunSearch();
			float until = Time.unscaledTime + 20f;
			while (PackSteam.Querying && Time.unscaledTime < until)
			{
				yield return null;
			}
			yield return CivilTest.Wait(1f);
			Check("a search with no answer shows its empty page", PackSteam.Results.Count == 0 && !PackSteam.Querying, PackSteam.Status);
			if (capture)
			{
				yield return Shot("packs_browse_empty.png");
			}
			PacksUi.Search = "";
			PackSteam.AnyWithTag = "Unverified Mod";
			PacksUi.RunSearch();
			until = Time.unscaledTime + 20f;
			while (PackSteam.Querying && Time.unscaledTime < until)
			{
				yield return null;
			}
			yield return CivilTest.Wait(3f);
			if (capture)
			{
				yield return Shot("packs_browse.png");
			}
			PackSteam.AnyWithTag = null;
			PacksUi.Page = 2;
			PacksUi.PublishKey = "wftest_a@local";
			yield return CivilTest.Wait(0.5f);
			if (capture)
			{
				yield return Shot("packs_publish.png");
			}
			PacksUi.Open = false;
			Mod.SetSettingsMenu(open: false);
			yield return CivilTest.Wait(0.3f);
			Check("the window drawn on every page without trouble", _errors == 0);
		}

		private IEnumerator ModsFolder()
		{
			Packs.Discover();
			PackInfo p = Packs.Find("wftest_mods@mods");
			Check("the Mods-folder pack found", p != null && p.Source == PackSource.Mods, string.Join(", ", Packs.All.ConvertAll((PackInfo x) => x.Key).ToArray()));
			if (p == null)
			{
				yield break;
			}
			Check("with no errors", p.Errors.Count == 0, string.Join(" | ", p.Errors.ToArray()));
			Packs.SetActive(new string[1] { p.Key });
			yield return Applied();
			Check("its tree_pine in place", Models.Overlaid("tree_pine") && Models.Get("tree_pine") == Packs.ContentOf(p).Models["tree_pine"]);
			bool flag = false;
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for (int num = 0; num < assemblies.Length; num++)
			{
				Type[] array = SafeTypes(assemblies[num]);
				for (int num2 = 0; num2 < array.Length; num2++)
				{
					if (array[num2].FullName == "WorldfallPack_wftest_mods.Pack")
					{
						flag = true;
					}
				}
			}
			Check("NeoModLoader compiled its stub (the pack is a loaded mod)", flag || !Options.Contains("nml"));
			Packs.SetActive(new string[0]);
			yield return Applied();
		}

		private static Type[] SafeTypes(Assembly a)
		{
			try
			{
				return a.GetTypes();
			}
			catch (ReflectionTypeLoadException ex)
			{
				return Array.FindAll(ex.Types, (Type t) => t != null);
			}
			catch
			{
				return new Type[0];
			}
		}

		private IEnumerator Shot(string name)
		{
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, name));
			for (int i = 0; i < 10; i++)
			{
				if (!Mod.HudForTest.CapturePending)
				{
					break;
				}
				yield return null;
			}
			Note("captured " + name);
		}

		internal static byte[] ToGlb(MeshModel m)
		{
			return GlbWriter.Write(m, "Worldfall packs selftest");
		}

		internal static byte[] Tone(float hz, float seconds)
		{
			int num = 44100;
			int num2 = (int)((float)num * seconds);
			MemoryStream memoryStream = new MemoryStream();
			BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
			binaryWriter.Write(Encoding.ASCII.GetBytes("RIFF"));
			binaryWriter.Write(36 + num2 * 2);
			binaryWriter.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
			binaryWriter.Write(16);
			binaryWriter.Write((short)1);
			binaryWriter.Write((short)1);
			binaryWriter.Write(num);
			binaryWriter.Write(num * 2);
			binaryWriter.Write((short)2);
			binaryWriter.Write((short)16);
			binaryWriter.Write(Encoding.ASCII.GetBytes("data"));
			binaryWriter.Write(num2 * 2);
			for (int i = 0; i < num2; i++)
			{
				binaryWriter.Write((short)(Math.Sin((double)((float)i * hz * 2f) * Math.PI / (double)num) * 8000.0 * Math.Min(1.0, (double)(num2 - i) / 2000.0)));
			}
			return memoryStream.ToArray();
		}

		private static string MakePack(string folder, string manifest)
		{
			string text = Path.Combine(Packs.LocalFolder, folder);
			if (Directory.Exists(text))
			{
				Directory.Delete(text, recursive: true);
			}
			Directory.CreateDirectory(text);
			if (manifest != null)
			{
				File.WriteAllText(Path.Combine(text, "worldfall.json"), manifest);
			}
			return text;
		}

		private static void Put(string dir, string rel, byte[] data)
		{
			string path = Path.Combine(dir, rel.Replace('/', Path.DirectorySeparatorChar));
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllBytes(path, data);
		}

		private IEnumerator Folder()
		{
			string text = PacksUi.NewPack();
			PackInfo packInfo = Packs.Find(text);
			Check("New pack found at once", packInfo != null && packInfo.Source == PackSource.Local, text);
			if (packInfo != null)
			{
				bool ok = Directory.Exists(Path.Combine(packInfo.Folder, "models")) && Directory.Exists(Path.Combine(packInfo.Folder, "sounds")) && Directory.Exists(Path.Combine(packInfo.Folder, "lang")) && File.Exists(Path.Combine(packInfo.Folder, "README.txt"));
				Check("with models/, sounds/, lang/ and a README", ok, packInfo.Folder);
				Check("its worldfall.json reads with no error", packInfo.Errors.Count == 0 && packInfo.Version == "1.0.0", string.Join(" | ", packInfo.Errors.ToArray()));
				Check("an empty pack says so", packInfo.Warnings.Exists((string w) => w.Contains("Nothing in it")), string.Join(" | ", packInfo.Warnings.ToArray()));
				Directory.Delete(packInfo.Folder, recursive: true);
			}
			Check("the folder opener is the system's", PacksUi.FolderOpener() != null && (Application.platform != RuntimePlatform.WindowsPlayer || File.Exists(PacksUi.FolderOpener())), PacksUi.FolderOpener());
			if (Options.Contains("showfolder"))
			{
				PacksUi.ShowFolder(Packs.LocalFolder);
			}
			PacksUi.Open = false;
			string live = MakePack("wftest_live", Manifest("wftest_live", "Test Live"));
			Packs.Discover();
			Packs.SetActive(new string[1] { "wftest_live@local" });
			yield return Applied();
			Check("an empty pack on: the mod's own tree_pine", Models.Get("tree_pine") == Models.Own("tree_pine"));
			string pine = Path.Combine(Path.Combine(live, "models"), "tree_pine.glb");
			PutCopied(pine, ToGlb(_srcA));
			yield return Until(() => Models.Overlaid("tree_pine"), 8f);
			MeshModel meshModel = Models.Get("tree_pine");
			Check("a model copied in (an old date) shows with the window shut", meshModel != null && Models.Overlaid("tree_pine") && Same(meshModel, _srcA), (meshModel == null) ? "none" : Describe(meshModel));
			PutCopied(pine, ToGlb(_srcB));
			yield return Until(() => Models.Get("tree_pine") != null && Same(Models.Get("tree_pine"), _srcB), 8f);
			meshModel = Models.Get("tree_pine");
			Check("replaced by another with the same date: the new one shows", meshModel != null && Same(meshModel, _srcB), (meshModel == null) ? "none" : Describe(meshModel));
			Put(live, "sounds/heart/one.wav", Tone(440f, 0.2f));
			yield return Until(() => Mod.Audio.SetFromPack("heart"), 8f);
			Check("a sound set dropped in plays from the pack", Mod.Audio.SetFromPack("heart") && Mod.Audio.SetSize("heart") == 1, "size " + Mod.Audio.SetSize("heart"));
			File.Delete(pine);
			yield return Until(() => Models.Get("tree_pine") == Models.Own("tree_pine"), 8f);
			Check("the model deleted: the mod's own back", Models.Get("tree_pine") == Models.Own("tree_pine") && Models.Get("tree_pine_m") != null);
			PacksUi.Open = true;
			PutCopied(pine, ToGlb(_srcA));
			yield return Until(() => Models.Overlaid("tree_pine"), 10f);
			Check("with the window open the change shows too", Models.Overlaid("tree_pine") && Same(Models.Get("tree_pine"), _srcA));
			PacksUi.Open = false;
			PackInfo packInfo2 = Packs.Find("wftest_live@local");
			Stopwatch stopwatch = Stopwatch.StartNew();
			for (int num = 0; num < 20; num++)
			{
				Packs.QuickSig(packInfo2.Folder);
			}
			double num2 = stopwatch.Elapsed.TotalMilliseconds / 20.0;
			stopwatch = Stopwatch.StartNew();
			for (int num3 = 0; num3 < 20; num3++)
			{
				Packs.Read(packInfo2.Folder, packInfo2.ModFolder, packInfo2.Source, 0uL, local: true);
			}
			double num4 = stopwatch.Elapsed.TotalMilliseconds / 20.0;
			Check("the watch's listing unchanged while nothing changes", Packs.QuickSig(packInfo2.Folder) == packInfo2.Quick);
			Check("a changed pack read again in under 10 ms", num4 < 10.0, "listing " + num2.ToString("0.00") + " ms (worker), read " + num4.ToString("0.00") + " ms (main thread, on a change only)");
			Directory.Delete(live, recursive: true);
			yield return Until(() => !Models.Overlaid("tree_pine") && Packs.Find("wftest_live@local") == null, 8f);
			Check("its folder deleted: gone from the list, the mod's own back", Packs.Find("wftest_live@local") == null && Models.Get("tree_pine") == Models.Own("tree_pine") && !Mod.Audio.SetFromPack("heart"));
			Packs.SetActive(new string[0]);
			yield return Applied();
			string nest = MakePack("wftest_nest", null);
			string dir = Path.Combine(nest, "wftest_nest");
			Put(dir, "worldfall.json", Encoding.UTF8.GetBytes(Manifest("wftest_nest", "Test Nest")));
			Put(dir, "models/tree_pine.glb", ToGlb(_srcA));
			string bare = MakePack("wftest_nomanifest", null);
			Put(bare, "models/tree_pine.glb", ToGlb(_srcA));
			string junk = MakePack("wftest_junk", null);
			Put(junk, "notes.txt", Encoding.UTF8.GetBytes("not a pack"));
			string wrong = MakePack("wftest_wrong", Manifest("wftest_wrong", "Test Wrong"));
			Put(wrong, "models/tree_pine.glb", ToGlb(_srcA));
			Put(wrong, "models/tree_oak.fbx", new byte[64]);
			Put(wrong, "models/more/tree_oak.glb", ToGlb(_srcA));
			Put(wrong, "tree_birch.glb", ToGlb(_srcA));
			Put(wrong, "sounds/loose.wav", Tone(300f, 0.1f));
			Put(wrong, "sounds/heart/beat.mp3", new byte[64]);
			Put(wrong, "sounds/wftest_noset/a.wav", Tone(300f, 0.1f));
			Put(wrong, "lang/README.txt", Encoding.UTF8.GetBytes("notes"));
			Packs.Discover();
			PackInfo packInfo3 = Packs.Find("wftest_nest@local");
			Check("a zip unpacked into a folder of its own found", packInfo3 != null && packInfo3.Errors.Count == 0 && packInfo3.ModelFiles.ContainsKey("tree_pine") && packInfo3.ModFolder.TrimEnd(Path.DirectorySeparatorChar) == nest.TrimEnd(Path.DirectorySeparatorChar), (packInfo3 == null) ? "not found" : (packInfo3.Folder + " / " + string.Join(" | ", packInfo3.Errors.ToArray())));
			PackInfo packInfo4 = Packs.Find("wftest_nomanifest@local");
			Check("a folder with no worldfall.json shown with that as its error", packInfo4?.Errors.Exists((string e) => e.Contains("worldfall.json")) ?? false, (packInfo4 == null) ? "not found" : string.Join(" | ", packInfo4.Errors.ToArray()));
			Check("a folder that isn't a pack left out", !Packs.All.Exists((PackInfo p) => p.Folder.TrimEnd(Path.DirectorySeparatorChar).EndsWith("wftest_junk", StringComparison.Ordinal)));
			PackInfo packInfo5 = Packs.Find("wftest_wrong@local");
			string[] array = new string[6] { "tree_oak.fbx", "models/more/", "tree_birch.glb", "sounds/loose.wav", "beat.mp3", "wftest_noset" };
			List<string> list = new List<string>();
			string[] array2;
			if (packInfo5 != null)
			{
				array2 = array;
				foreach (string s in array2)
				{
					if (packInfo5.Warnings.Exists((string x) => x.Contains(s)))
					{
						list.Add(s);
					}
				}
			}
			Check("files of the wrong kind or place each told on the card", packInfo5 != null && list.Count == array.Length && packInfo5.Errors.Count == 0 && packInfo5.ModelFiles.Count == 1 && !packInfo5.LangFiles.ContainsKey("README"), (packInfo5 == null) ? "not found" : (string.Join(" | ", packInfo5.Warnings.ToArray()) + " / errors " + string.Join(" | ", packInfo5.Errors.ToArray())));
			Check("and the good file in it still used", packInfo5?.ModelFiles.ContainsKey("tree_pine") ?? false);
			if (packInfo3 != null)
			{
				Packs.SetActive(new string[1] { packInfo3.Key });
				yield return Applied();
				Check("the unpacked-zip pack works when on", Models.Overlaid("tree_pine"));
				Packs.SetActive(new string[0]);
				yield return Applied();
			}
			array2 = new string[4] { nest, bare, junk, wrong };
			for (int num5 = 0; num5 < array2.Length; num5++)
			{
				Directory.Delete(array2[num5], recursive: true);
			}
			Packs.Discover();
		}

		private static void PutCopied(string path, byte[] data)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path));
			File.WriteAllBytes(path, data);
			File.SetLastWriteTimeUtc(path, OldDate);
		}

		private IEnumerator Until(Func<bool> done, float limit)
		{
			float until = Time.unscaledTime + limit;
			while (!done() && Time.unscaledTime < until)
			{
				yield return null;
			}
			while (Packs.Busy && Time.unscaledTime < until)
			{
				yield return null;
			}
			yield return null;
		}

		private static void EquipItem(Actor unit, EquipmentType type, string id)
		{
			try
			{
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				if (equipmentAsset != null)
				{
					ActorEquipmentSlot slot = unit.equipment.getSlot(type);
					if (slot.getItem() != null)
					{
						slot.takeAwayItem();
					}
					slot.setItem(World.world.items.generateItem(equipmentAsset, unit.kingdom, unit.getName(), 1, unit, 0, pByPlayer: true), unit);
					unit.setStatsDirty();
				}
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.Log("equip failed: " + ex.Message);
			}
		}

		private static void ClearAround(Vector2 c, int r)
		{
			for (int i = (int)c.y - r; i <= (int)c.y + r; i++)
			{
				for (int j = (int)c.x - r; j <= (int)c.x + r; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile != null && tile.building != null)
					{
						try
						{
							tile.building.removeBuildingFinal();
						}
						catch
						{
						}
					}
				}
			}
		}

		private IEnumerator Guide()
		{
			Packs.Discover();
			PackInfo g = Packs.Find("guide_examples@local");
			if (!Check("the guide's example pack is there", g != null, Packs.LocalFolder))
			{
				yield break;
			}
			Packs.SetActive(new string[1] { "guide_examples@local" });
			yield return Applied();
			PackContent packContent = Packs.ContentOf(g);
			List<string> list = new List<string>(g.Errors);
			foreach (string warning in g.Warnings)
			{
				if (!warning.StartsWith("Made for Worldfall", StringComparison.Ordinal))
				{
					list.Add(warning);
				}
			}
			list.AddRange(packContent.Problems);
			Check("its three models load with nothing to report", packContent.Models.Count == 3 && list.Count == 0, packContent.Models.Count + " models; " + string.Join(" | ", list.ToArray()));
			foreach (KeyValuePair<string, MeshModel> model in packContent.Models)
			{
				List<string> list2 = new List<string>();
				MeshPart[] parts = model.Value.Parts;
				foreach (MeshPart meshPart in parts)
				{
					list2.Add(meshPart.Name);
				}
				MeshModel m = Models.Own(model.Key) ?? ((model.Key == "tool_shovel") ? ToolMeshes.Tool("shovel") : null);
				Note(model.Key + ": " + Size(model.Value) + " long (the original " + Size(m) + "), parts " + string.Join(" ", list2.ToArray()) + ", slots " + string.Join(" ", model.Value.SlotNames) + ", info " + model.Value.InfoText());
			}
			MeshModel meshModel = Models.Get("tool_shovel");
			MeshModel meshModel2 = Models.Get("held_sword_iron");
			MeshModel meshModel3 = Models.Get("animal_wolf");
			Check("the shovel about the code one's size", meshModel != null && Near(Packs.Longest(meshModel), Packs.Longest(ToolMeshes.Tool("shovel")), 0.06f), Size(meshModel));
			Check("the sword about the library's size", meshModel2 != null && Near(Packs.Longest(meshModel2), Packs.Longest(Models.Own("held_sword_iron")), 0.06f), Size(meshModel2));
			MeshModel meshModel4 = Models.Own("animal_wolf");
			bool flag = meshModel3 != null && meshModel4 != null && meshModel3.Parts.Length == meshModel4.Parts.Length;
			if (flag)
			{
				MeshPart[] parts = meshModel4.Parts;
				foreach (MeshPart meshPart2 in parts)
				{
					if (meshModel3.PartIndex(meshPart2.Name) < 0)
					{
						flag = false;
					}
				}
			}
			Check("the wolf has every part the game's has", flag);
			Check("the wolf about the library's size", meshModel3 != null && Near(Packs.Longest(meshModel3), Packs.Longest(meshModel4), 0.1f), Size(meshModel3) + " vs " + Size(meshModel4));
			if (_host == null || !InControl(_host))
			{
				yield break;
			}
			try
			{
				_host.equipment.getSlot(EquipmentType.Weapon).takeAwayItem();
			}
			catch
			{
			}
			Supplies.Give(_host, "tw_item_shovel", 1);
			yield return null;
			int num = BagOrder.Of(_host).FindIndex((string e) => BagOrder.ResourceOf(e) == "tw_item_shovel");
			if (num >= Hotbar.Size(_host))
			{
				BagOrder.Swap(_host, num, 0);
				num = 0;
			}
			if (num >= 0)
			{
				Hotbar.Choose(_host, num, wheel: false);
			}
			Mod.Holstered = false;
			Mod.DebugEyeSet = false;
			Mod.SetPitchDegrees(30f);
			yield return new WaitForSeconds(1.2f);
			Mod.SaveFrame("guide_shovel.png");
			EquipItem(_host, EquipmentType.Weapon, "sword_iron");
			Mod.SetPitchDegrees(0f);
			yield return new WaitForSeconds(1.2f);
			Mod.SaveFrame("guide_sword.png");
			Vector2 vector = _host.current_position + new Vector2(3.5f, 0f);
			ClearAround(vector, 6);
			Actor w = CivilTest.Spawn("wolf", vector);
			if (w == null)
			{
				Note("no wolf spawned");
				yield break;
			}
			try
			{
				_host.equipment.getSlot(EquipmentType.Weapon).takeAwayItem();
			}
			catch
			{
			}
			Mod.Holstered = true;
			yield return new WaitForSeconds(0.6f);
			Config.paused = true;
			try
			{
				Vector2 current_position = w.current_position;
				Vector2 debugEye = current_position + new Vector2(-1.4f, -2.4f);
				Mod.DebugEyeSet = true;
				Mod.DebugEye = debugEye;
				Mod.DebugEyeHeight = 0f;
				Mod.SetYaw(Mathf.Atan2(current_position.y - debugEye.y, current_position.x - debugEye.x));
				Mod.SetPitchDegrees(-12f);
			}
			catch (Exception ex)
			{
				Note("eye: " + ex.Message);
			}
			yield return new WaitForSecondsRealtime(1f);
			Mod.SaveFrame("guide_wolf.png");
			Packs.SetActive(new string[0]);
			yield return Applied();
			yield return new WaitForSecondsRealtime(0.8f);
			Mod.SaveFrame("guide_wolf_game.png");
			Config.paused = false;
			Mod.DebugEyeSet = false;
			if (!Options.Contains("capture"))
			{
				yield break;
			}
			Mod.Settings.Language = "auto";
			Packs.SetActive(new string[0]);
			yield return Applied();
			string[] directories = Directory.GetDirectories(Packs.LocalFolder, "wftest_*");
			foreach (string path in directories)
			{
				try
				{
					Directory.Delete(path, recursive: true);
				}
				catch
				{
				}
			}
			Packs.Discover();
			Packs.SetActive(new string[1] { "guide_examples@local" });
			yield return Applied();
			Mod.SetSettingsMenu(open: true);
			Mod.SettingsMenu.Tab = 7;
			PacksUi.OpenAt(0);
			PacksUi.FocusOn("guide_examples@local");
			yield return CivilTest.Wait(0.8f);
			yield return Shot("guide_mods.png");
			PacksUi.Details = "guide_examples@local";
			yield return CivilTest.Wait(0.5f);
			yield return Shot("guide_details.png");
			PacksUi.Details = null;
			PacksUi.Page = 2;
			PacksUi.PublishKey = "guide_examples@local";
			yield return CivilTest.Wait(0.5f);
			yield return Shot("guide_publish.png");
			PacksUi.Open = false;
			Mod.SetSettingsMenu(open: false);
			yield return CivilTest.Wait(0.3f);
		}

		private static string LiveFile(string name)
		{
			return Path.Combine(Packs.LocalFolder, name);
		}

		private IEnumerator Live()
		{
			while (!Models.Ready)
			{
				yield return null;
			}
			if (Options.Contains("livepublish"))
			{
				yield return Part("published through Publish, hidden", LivePublish());
			}
			if (Options.Contains("livesubscribe"))
			{
				yield return Part("subscribed, downloaded, turned on", LiveSubscribe());
			}
		}

		private IEnumerator LivePublish()
		{
			Packs.Discover();
			string text = LiveFile("live_packs.txt");
			Check("the list of packs to put up", File.Exists(text), text);
			if (!File.Exists(text))
			{
				yield break;
			}
			List<string> items = new List<string>();
			string[] array = File.ReadAllLines(text);
			foreach (string text2 in array)
			{
				string id = text2.Trim();
				if (id.Length == 0)
				{
					continue;
				}
				PackInfo p = Packs.Find(id + "@local");
				Check(id + " found, no errors", p != null && p.Errors.Count == 0, (p == null) ? "missing" : string.Join(" | ", p.Errors.ToArray()));
				if (p == null)
				{
					continue;
				}
				Packs.SetActive(new string[1] { p.Key });
				yield return Applied();
				List<string> list = PackPublish.Check(p);
				Check(id + " may go up", list.Count == 0, string.Join(" | ", list.ToArray()));
				if (list.Count <= 0)
				{
					PackPublish.Job job = PackPublish.Start(p, 2, "First version", dry: false);
					float until = Time.unscaledTime + 300f;
					while (!job.Done && Time.unscaledTime < until)
					{
						yield return null;
					}
					Check(id + " published", job.Done && !job.Failed && job.Item != 0, job.State);
					if (job.Item != 0L)
					{
						items.Add(id + " " + job.Item);
					}
				}
			}
			Packs.SetActive(new string[0]);
			yield return Applied();
			File.WriteAllLines(LiveFile("live_items.txt"), items.ToArray());
			Note("items: " + string.Join(", ", items.ToArray()));
			yield return Mine();
		}

		private IEnumerator Mine()
		{
			Task<ResultPage?> t = null;
			try
			{
				t = Query.Items.WhereUserPublished(SteamClient.SteamId).WithTag("Worldfall Pack").GetPageAsync(1);
			}
			catch (Exception ex)
			{
				Note("own items unknown: " + ex.Message);
			}
			if (t == null)
			{
				yield break;
			}
			float until = Time.unscaledTime + 30f;
			while (!t.IsCompleted && Time.unscaledTime < until)
			{
				yield return null;
			}
			if (!t.IsCompleted || t.IsFaulted || !t.Result.HasValue)
			{
				Note("own items: no answer");
				yield break;
			}
			using (ResultPage resultPage = t.Result.Value)
			{
				foreach (Steamworks.Ugc.Item entry in resultPage.Entries)
				{
					Note("own item " + entry.Id.Value + " \"" + entry.Title + "\" " + entry.SizeBytes + " bytes, " + (entry.IsPrivate ? "hidden" : (entry.IsFriendsOnly ? "friends" : (entry.IsPublic ? "public" : "?"))));
				}
			}
		}

		private IEnumerator LiveSubscribe()
		{
			string file = LiveFile("live_items.txt");
			Check("the items to subscribe to", File.Exists(file), file);
			if (!File.Exists(file))
			{
				yield break;
			}
			Check("Steam signed in", PackSteam.Available);
			_host = CivilTest.Spawn("human", CivilTest.Stage().posV);
			if (_host != null)
			{
				yield return Possess(_host);
			}
			Check("first person", _host != null && InControl(_host));
			float t = Time.unscaledTime + 3f;
			while (Time.unscaledTime < t)
			{
				yield return null;
			}
			string[] array = File.ReadAllLines(file);
			foreach (string obj in array)
			{
				string[] array2 = obj.Trim().Split(' ');
				if (array2.Length < 2 || !ulong.TryParse(array2[1], out var item))
				{
					continue;
				}
				string id = array2[0];
				Packs.SetActive(new string[0]);
				yield return Applied();
				if (PackSteam.IsSubscribed(item))
				{
					PackSteam.Unsubscribe(item);
					float t2 = Time.unscaledTime + 5f;
					while (Time.unscaledTime < t2)
					{
						yield return null;
					}
				}
				string text = PackSteam.Subscribe(item);
				Check(id + ": subscribed", text == null, text ?? "");
				t = Time.unscaledTime + 240f;
				PackInfo p = null;
				while (Time.unscaledTime < t)
				{
					p = Packs.All.Find((PackInfo x) => x.Source == PackSource.Workshop && x.WorkshopId == item);
					if (p != null && Packs.IsActive(p.Key) && !Packs.Busy)
					{
						break;
					}
					yield return null;
				}
				Check(id + ": downloaded and found as a Workshop pack", p != null, (p == null) ? ("status " + PackSteam.DownloadOf(item) + ", installed " + PackSteam.IsInstalled(item)) : (p.Key + " in " + p.ModFolder));
				if (p == null)
				{
					continue;
				}
				Check(id + ": no errors", p.Errors.Count == 0, string.Join(" | ", p.Errors.ToArray()));
				Check(id + ": turned on by itself", Packs.IsActive(p.Key), string.Join(", ", Packs.ActiveKeys.ToArray()));
				yield return Applied();
				if (p.ModelFiles.Count > 0)
				{
					int num = 0;
					foreach (string key in p.ModelFiles.Keys)
					{
						if (Models.Overlaid(key))
						{
							num++;
						}
					}
					Check(id + ": its models in the world", num == p.ModelFiles.Count, num + " of " + p.ModelFiles.Count);
				}
				foreach (string key2 in p.SoundFiles.Keys)
				{
					Check(id + ": its sound set " + key2, Mod.Audio.SetFromPack(key2));
				}
				foreach (string key3 in p.LangFiles.Keys)
				{
					bool flag = false;
					foreach (string item2 in Packs.LangCodes())
					{
						if (item2 == key3)
						{
							flag = true;
						}
					}
					Check(id + ": its language " + key3, flag);
					if (!flag || !(key3 == "es"))
					{
						continue;
					}
					string was = Mod.Settings.Language;
					Mod.Settings.Language = "es";
					for (int i2 = 0; i2 < 5; i2++)
					{
						yield return null;
					}
					Check(id + ": the menus in Spanish", Lang.Code == "es" && Lang.T("Field of view") == "Campo de visión", Lang.Code + ": " + Lang.T("Field of view"));
					if (Options.Contains("capture"))
					{
						PacksUi.OpenAt(0);
						for (int i2 = 0; i2 < 30; i2++)
						{
							yield return null;
						}
						yield return Shot("packs_live_spanish.png");
						PacksUi.Open = false;
					}
					Mod.Settings.Language = was;
				}
				if (Options.Contains("capture"))
				{
					PacksUi.OpenAt(0);
					for (int i2 = 0; i2 < 30; i2++)
					{
						yield return null;
					}
					yield return Shot("packs_live_" + id + ".png");
					PacksUi.Open = false;
				}
				Packs.SetActive(new string[0]);
				yield return Applied();
				if (!Options.Contains("livekeep"))
				{
					PackSteam.Unsubscribe(item);
				}
			}
		}

		private static byte[] TexturedCubeGlb(string material, byte[] image, string mime, string uri, float[] factor = null)
		{
			List<float> pos = new List<float>();
			List<float> nrm = new List<float>();
			List<float> uv = new List<float>();
			List<ushort> idx = new List<ushort>();
			float num = 0.5f;
			Face(new float[3] { 0f, 0f, 1f }, new float[3]
			{
				0f - num,
				0f - num,
				num
			}, new float[3]
			{
				num,
				0f - num,
				num
			}, new float[3] { num, num, num }, new float[3]
			{
				0f - num,
				num,
				num
			});
			Face(new float[3] { 0f, 0f, -1f }, new float[3]
			{
				num,
				0f - num,
				0f - num
			}, new float[3]
			{
				0f - num,
				0f - num,
				0f - num
			}, new float[3]
			{
				0f - num,
				num,
				0f - num
			}, new float[3]
			{
				num,
				num,
				0f - num
			});
			Face(new float[3] { 1f, 0f, 0f }, new float[3]
			{
				num,
				0f - num,
				num
			}, new float[3]
			{
				num,
				0f - num,
				0f - num
			}, new float[3]
			{
				num,
				num,
				0f - num
			}, new float[3] { num, num, num });
			Face(new float[3] { -1f, 0f, 0f }, new float[3]
			{
				0f - num,
				0f - num,
				0f - num
			}, new float[3]
			{
				0f - num,
				0f - num,
				num
			}, new float[3]
			{
				0f - num,
				num,
				num
			}, new float[3]
			{
				0f - num,
				num,
				0f - num
			});
			Face(new float[3] { 0f, 1f, 0f }, new float[3]
			{
				0f - num,
				num,
				num
			}, new float[3] { num, num, num }, new float[3]
			{
				num,
				num,
				0f - num
			}, new float[3]
			{
				0f - num,
				num,
				0f - num
			});
			Face(new float[3] { 0f, -1f, 0f }, new float[3]
			{
				0f - num,
				0f - num,
				0f - num
			}, new float[3]
			{
				num,
				0f - num,
				0f - num
			}, new float[3]
			{
				num,
				0f - num,
				num
			}, new float[3]
			{
				0f - num,
				0f - num,
				num
			});
			MemoryStream bin = new MemoryStream();
			BinaryWriter w = new BinaryWriter(bin);
			List<string> views = new List<string>();
			int num2 = View(delegate
			{
				foreach (float item in pos)
				{
					w.Write(item);
				}
			}, 34962);
			int num3 = View(delegate
			{
				foreach (float item2 in nrm)
				{
					w.Write(item2);
				}
			}, 34962);
			int num4 = View(delegate
			{
				foreach (float item3 in uv)
				{
					w.Write(item3);
				}
			}, 34962);
			int num5 = View(delegate
			{
				foreach (ushort item4 in idx)
				{
					w.Write(item4);
				}
			}, 34963);
			int num6 = ((image != null && uri == null) ? View(delegate
			{
				w.Write(image);
			}, 0) : (-1));
			while (bin.Length % 4 != 0L)
			{
				w.Write((byte)0);
			}
			int num7 = pos.Count / 3;
			string text = ((uri != null) ? ("{\"uri\":\"" + uri + "\",\"name\":\"linked\"}") : ("{\"bufferView\":" + num6 + ",\"mimeType\":\"" + mime + "\",\"name\":\"cube\"}"));
			string text2 = "\"baseColorTexture\":{\"index\":0}" + ((factor != null) ? (",\"baseColorFactor\":[" + F(factor[0]) + "," + F(factor[1]) + "," + F(factor[2]) + ",1]") : "");
			string s = "{\"asset\":{\"version\":\"2.0\",\"generator\":\"Worldfall packs selftest\"},\"scene\":0,\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"mesh\":0,\"name\":\"cube\"}],\"meshes\":[{\"name\":\"cube\",\"primitives\":[{\"attributes\":{\"POSITION\":0,\"NORMAL\":1,\"TEXCOORD_0\":2},\"indices\":3,\"material\":0}]}],\"materials\":[{\"name\":\"" + material + "\",\"pbrMetallicRoughness\":{" + text2 + "}}],\"textures\":[{\"source\":0}],\"images\":[" + text + "],\"accessors\":[{\"bufferView\":" + num2 + ",\"componentType\":5126,\"count\":" + num7 + ",\"type\":\"VEC3\",\"min\":[-0.5,-0.5,-0.5],\"max\":[0.5,0.5,0.5]},{\"bufferView\":" + num3 + ",\"componentType\":5126,\"count\":" + num7 + ",\"type\":\"VEC3\"},{\"bufferView\":" + num4 + ",\"componentType\":5126,\"count\":" + num7 + ",\"type\":\"VEC2\"},{\"bufferView\":" + num5 + ",\"componentType\":5123,\"count\":" + idx.Count + ",\"type\":\"SCALAR\"}],\"bufferViews\":[" + string.Join(",", views.ToArray()) + "],\"buffers\":[{\"byteLength\":" + bin.Length + "}]}";
			byte[] bytes = Encoding.UTF8.GetBytes(s);
			int num8 = (4 - bytes.Length % 4) % 4;
			MemoryStream memoryStream = new MemoryStream();
			BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
			binaryWriter.Write(1179937895);
			binaryWriter.Write(2);
			binaryWriter.Write(20 + bytes.Length + num8 + 8 + (int)bin.Length);
			binaryWriter.Write(bytes.Length + num8);
			binaryWriter.Write(1313821514);
			binaryWriter.Write(bytes);
			for (int num9 = 0; num9 < num8; num9++)
			{
				binaryWriter.Write((byte)32);
			}
			binaryWriter.Write((int)bin.Length);
			binaryWriter.Write(5130562);
			binaryWriter.Write(bin.ToArray());
			return memoryStream.ToArray();
			string F(float v)
			{
				return v.ToString("R", CultureInfo.InvariantCulture);
			}
			void Face(float[] n, float[] a, float[] b, float[] c, float[] d)
			{
				int num10 = pos.Count / 3;
				float[][] array = new float[4][] { a, b, c, d };
				foreach (float[] collection in array)
				{
					pos.AddRange(collection);
					nrm.AddRange(n);
				}
				uv.AddRange(new float[8] { 0f, 1f, 1f, 1f, 1f, 0f, 0f, 0f });
				idx.AddRange(new ushort[6]
				{
					(ushort)num10,
					(ushort)(num10 + 1),
					(ushort)(num10 + 2),
					(ushort)num10,
					(ushort)(num10 + 2),
					(ushort)(num10 + 3)
				});
			}
			int View(Action write, int target)
			{
				while (bin.Length % 4 != 0L)
				{
					w.Write((byte)0);
				}
				long length = bin.Length;
				write();
				views.Add("{\"buffer\":0,\"byteOffset\":" + length + ",\"byteLength\":" + (bin.Length - length) + ((target > 0) ? (",\"target\":" + target) : "") + "}");
				return views.Count - 1;
			}
		}

		private static Texture2D Quad(bool hole)
		{
			Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
			texture2D.SetPixels32(new Color32[4]
			{
				new Color32(0, 0, byte.MaxValue, byte.MaxValue),
				new Color32(byte.MaxValue, byte.MaxValue, 0, (byte)((!hole) ? byte.MaxValue : 0)),
				new Color32(byte.MaxValue, 0, 0, byte.MaxValue),
				new Color32(0, byte.MaxValue, 0, byte.MaxValue)
			});
			texture2D.Apply(updateMipmaps: false);
			return texture2D;
		}

		private static string Hex(uint c)
		{
			return (c & 0xFFFFFF).ToString("X6");
		}

		private IEnumerator Textures()
		{
			Texture2D texture2D = Quad(hole: false);
			Texture2D texture2D2 = Quad(hole: true);
			byte[] array = texture2D.EncodeToPNG();
			byte[] image = texture2D2.EncodeToPNG();
			byte[] array2 = texture2D.EncodeToJPG(100);
			UnityEngine.Object.Destroy(texture2D);
			UnityEngine.Object.Destroy(texture2D2);
			List<string> list = new List<string>();
			MeshModel meshModel = Glb.Read(TexturedCubeGlb("body|wood", array, "image/png", null), "cube", Packs.Limits, list, out var extras);
			if (!Check("a textured cube reads", meshModel != null && list.Count == 0, string.Join(" | ", list.ToArray())))
			{
				yield break;
			}
			PacksTest packsTest = this;
			bool ok = meshModel.Parts[0].Uv != null && meshModel.Parts[0].Uv.Length == meshModel.Parts[0].VertexCount * 2;
			float[] uv = meshModel.Parts[0].Uv;
			object detail;
			int w;
			if (uv == null)
			{
				detail = null;
			}
			else
			{
				w = uv.Length;
				detail = w.ToString();
			}
			packsTest.Check("its part has UVs, 2 a vertex", ok, (string)detail);
			Check("its slot has the image", meshModel.Textured && meshModel.SlotTexture[0] == 0 && meshModel.Textures.Length == 1 && meshModel.Textures[0].Encoded != null && meshModel.Textures[0].Encoded.Length == array.Length);
			Check("the image's header: PNG 2x2", PackTextures.Describe(array, out var w2, out var h) == "PNG" && w2 == 2 && h == 2, w2 + "x" + h);
			Check("a JPEG's header too", PackTextures.Describe(array2, out var w3, out var h2) == "JPEG" && w3 == 2 && h2 == 2, w3 + "x" + h2);
			Check("WebP is not", PackTextures.Describe(Encoding.ASCII.GetBytes("RIFF....WEBPVP8 ........................"), out w, out var h3) == null);
			PackTextures.Decode(meshModel);
			MeshTexture meshTexture = meshModel.Textures[0];
			Check("decoded 2x2, the top row first: red then green, blue under", meshTexture.Pixels != null && meshTexture.Width == 2 && meshTexture.Height == 2 && Hex(meshTexture.Pixels[0]) == "0000FF" && Hex(meshTexture.Pixels[1]) == "00FF00" && Hex(meshTexture.Pixels[2]) == "FF0000", (meshTexture.Pixels != null) ? (Hex(meshTexture.Pixels[0]) + " " + Hex(meshTexture.Pixels[1]) + " " + Hex(meshTexture.Pixels[2]) + " " + Hex(meshTexture.Pixels[3])) : "none");
			Check("small, so crisp; no holes", meshTexture.Crisp && !meshTexture.Cutout && meshTexture.Encoded == null);
			meshModel = Glb.Read(TexturedCubeGlb("body|wood", array, "image/png", null, new float[3] { 0.5f, 0.5f, 0.5f }), "cube", Packs.Limits, list, out extras);
			PackTextures.Decode(meshModel);
			Check("a half-grey base colour under the texture: a tinted copy", meshModel != null && meshModel.Textures.Length == 2 && meshModel.SlotTexture[0] == 1 && meshModel.Textures[1].Source == meshModel.Textures[0] && meshModel.Textures[1].Pixels != null && (meshModel.Textures[1].Pixels[0] & 0xFF) < 195 && (meshModel.Textures[1].Pixels[0] & 0xFF) > 180, (meshModel != null && meshModel.Textures.Length == 2 && meshModel.Textures[1].Pixels != null) ? Hex(meshModel.Textures[1].Pixels[0]) : "?");
			meshModel = Glb.Read(TexturedCubeGlb("body|wood", image, "image/png", null), "cube", Packs.Limits, list, out extras);
			PackTextures.Decode(meshModel);
			Check("a clear texel makes a cutout", meshModel != null && meshModel.Textures[0].Cutout && meshModel.Textures[0].Pixels[3] >> 24 == 0);
			Texture2D texture2D3 = PackPreview.Render(meshModel, 96, 96);
			if (Check("the CPU renderer drew the cube", texture2D3 != null))
			{
				int num = 0;
				int num2 = 0;
				int num3 = 0;
				int num4 = 0;
				int num5 = 0;
				Color32[] pixels = texture2D3.GetPixels32();
				for (h3 = 0; h3 < pixels.Length; h3++)
				{
					Color32 color = pixels[h3];
					bool num6 = color.r > 90 && color.g < color.r / 2 && color.b < color.r / 2;
					bool flag = color.g > 90 && color.r < color.g / 2 && color.b < color.g / 2;
					bool flag2 = color.b > 90 && color.r < color.b / 2 && color.g < color.b / 2;
					bool flag3 = color.r > 90 && color.g > 90 && color.b < color.r / 2;
					if (num6)
					{
						num++;
					}
					if (flag)
					{
						num2++;
					}
					if (flag2)
					{
						num3++;
					}
					if (flag3)
					{
						num4++;
					}
					if (num6 || flag || flag2 || flag3)
					{
						num5++;
					}
				}
				int num7 = ((num > 20) ? 1 : 0) + ((num2 > 20) ? 1 : 0) + ((num3 > 20) ? 1 : 0) + ((num4 > 20) ? 1 : 0);
				Check("the image's colours on its faces (3 of 4 hues, the 4th a hole)", num7 >= 2 && num4 < 20 && num5 > 400, num + " red, " + num2 + " green, " + num3 + " blue, " + num4 + " yellow of " + texture2D3.width * texture2D3.height);
				if (Options.Contains("capture"))
				{
					File.WriteAllBytes(Path.Combine(Packs.LocalFolder, "wftest_textures_preview.png"), texture2D3.EncodeToPNG());
				}
				UnityEngine.Object.Destroy(texture2D3);
			}
			meshModel = Glb.Read(TexturedCubeGlb("body|wood", array, "image/png", null), "cube", Packs.Limits, list, out extras);
			PackTextures.Decode(meshModel);
			GpuModels.KeepAtlasReadable = true;
			Vector4 rect;
			Texture2D texture2D4 = GpuModels.AtlasForTest(meshModel, out rect);
			GpuModels.KeepAtlasReadable = false;
			if (Check("an atlas built for the card", texture2D4 != null && rect.z < 0f && rect.w > 0f, (texture2D4 == null) ? "none" : rect.ToString()))
			{
				int num8 = (int)(rect.x * (float)texture2D4.width);
				int num9 = (int)(rect.y * (float)texture2D4.height);
				Color32 color2 = texture2D4.GetPixel(num8, num9);
				Color32 color3 = texture2D4.GetPixel(num8, num9 + 1);
				Color32 color4 = texture2D4.GetPixel(num8 - 1, num9 - 1);
				PacksTest packsTest2 = this;
				bool ok2 = color2.r == byte.MaxValue && color2.g == 0 && color3.b == byte.MaxValue && color4.r == byte.MaxValue;
				string[] obj = new string[14]
				{
					"at ",
					num8.ToString(),
					",",
					num9.ToString(),
					": ",
					null,
					null,
					null,
					null,
					null,
					null,
					null,
					null,
					null
				};
				Color32 color5 = color2;
				obj[5] = color5.ToString();
				obj[6] = " ";
				color5 = color3;
				obj[7] = color5.ToString();
				obj[8] = " pad ";
				color5 = color4;
				obj[9] = color5.ToString();
				obj[10] = " atlas ";
				obj[11] = texture2D4.width.ToString();
				obj[12] = "x";
				obj[13] = texture2D4.height.ToString();
				packsTest2.Check("the image's top row lowest in the atlas (v runs up it), padded with its edge", ok2, string.Concat(obj));
				UnityEngine.Object.Destroy(texture2D4);
			}
			Check("the model shader loaded draws textures", !Mod.Settings.GpuRender || GpuModels.ShaderTextured());
			string dir = MakePack("wftest_textures", Manifest("wftest_textures", "Test Textures", ",\"models\":{\"held_axe_iron\":{\"textures\":\"smooth\"}}"));
			Put(dir, "models/held_sword_iron.glb", TexturedCubeGlb("blade|metal", array, "image/png", null));
			Put(dir, "models/held_axe_iron.glb", TexturedCubeGlb("head|metal", null, null, "axe_head.png"));
			Put(dir, "models/axe_head.png", array);
			Put(dir, "models/tool_pickaxe.glb", TexturedCubeGlb("head|metal", array2, "image/jpeg", null));
			Put(dir, "models/tool_hoe.glb", TexturedCubeGlb("head|metal", Encoding.ASCII.GetBytes("RIFF....WEBPVP8 ........................"), "image/webp", null));
			Packs.Discover();
			Packs.SetActive(new string[1] { "wftest_textures@local" });
			yield return Applied();
			PackInfo packInfo = Packs.Find("wftest_textures@local");
			PackContent packContent = ((packInfo != null) ? Packs.ContentOf(packInfo) : null);
			if (!Check("the textures pack loaded", packContent != null && packContent.Models.Count == 4, (packContent == null) ? "no pack" : (packContent.Models.Count + " models; " + string.Join(" | ", packContent.Problems.ToArray()))))
			{
				yield break;
			}
			MeshModel meshModel2 = Models.Get("held_sword_iron");
			MeshModel meshModel3 = Models.Get("held_axe_iron");
			MeshModel meshModel4 = Models.Get("tool_pickaxe");
			MeshModel meshModel5 = Models.Get("tool_hoe");
			Check("the embedded image decoded on apply", meshModel2 != null && meshModel2.Textured && meshModel2.Textures[0].Pixels != null && meshModel2.Textures[0].Crisp);
			Check("the linked image read from models/ and decoded", meshModel3 != null && meshModel3.Textured && meshModel3.Textures[0].Pixels != null, (meshModel3 != null && meshModel3.Textured) ? (meshModel3.Textures[0].Problem ?? "ok") : "no texture");
			Check("\"textures\": \"smooth\" blends a small image", meshModel3 != null && meshModel3.Textured && !meshModel3.Textures[0].Crisp);
			Check("the JPEG decoded", meshModel4 != null && meshModel4.Textured && meshModel4.Textures[0].Pixels != null && meshModel4.Textures[0].Width == 2);
			Check("the WebP is an error on the card, the model drawn flat", meshModel5 != null && (meshModel5.Textures == null || meshModel5.Textures[0].Pixels == null) && packContent.Problems.Exists((string s) => s.Contains("tool_hoe") && s.Contains("PNG")), string.Join(" | ", packContent.Problems.ToArray()));
			Check("a warning per texture drawn", Packs.Notes.TryGetValue("wftest_textures@local", out var value) && value.Count == 3 && value.Exists((string s) => s.Contains("held_sword_iron") && s.Contains("2x2 PNG")) && value.Exists((string s) => s.Contains("JPEG")), (value != null) ? string.Join(" | ", value.ToArray()) : "none");
			if (_host != null && InControl(_host))
			{
				EquipItem(_host, EquipmentType.Weapon, "sword_iron");
				Mod.Holstered = false;
				Mod.DebugEyeSet = false;
				Mod.SetPitchDegrees(10f);
				bool gpu = Mod.Settings.GpuRender;
				yield return new WaitForSeconds(1.2f);
				Mod.SaveFrame("textures_sword_" + (gpu ? "gpu" : "cpu") + ".png");
				Mod.Settings.GpuRender = !gpu;
				yield return new WaitForSeconds(1.2f);
				Mod.SaveFrame("textures_sword_" + (gpu ? "cpu" : "gpu") + ".png");
				Mod.Settings.GpuRender = gpu;
				yield return null;
				try
				{
					_host.equipment.getSlot(EquipmentType.Weapon).takeAwayItem();
				}
				catch
				{
				}
			}
			Packs.SetActive(new string[0]);
			yield return Applied();
		}

		private static MeshModel Bare(MeshModel m, float k)
		{
			MeshModel meshModel = Packs.Scaled(m, k);
			meshModel.ClearInfo();
			return meshModel;
		}

		private static string Size(MeshModel m)
		{
			if (m != null)
			{
				return Packs.Longest(m).ToString("0.###", CultureInfo.InvariantCulture);
			}
			return "none";
		}

		private static bool Near(float a, float b, float within = 0.01f)
		{
			return Math.Abs(a - b) <= within * Math.Max(Math.Abs(a), Math.Abs(b));
		}

		private IEnumerator Sizes()
		{
			MeshModel sword = Models.Own("held_sword_iron");
			MeshModel meshModel = Models.Own("held_axe_iron");
			MeshModel pick = Models.Own("tool_pickaxe");
			MeshModel codeShovel = ToolMeshes.Tool("shovel");
			if (!Check("library models to size against", sword != null && meshModel != null && pick != null && codeShovel != null))
			{
				yield break;
			}
			string dir = MakePack("wftest_units", Manifest("wftest_units", "Test Units", ",\"models\":{\"tool_pickaxe\":{\"fit\":true},\"held_axe_iron\":{\"info\":\"tip=0,0.3,0.7\"}}"));
			Put(dir, "models/held_sword_iron.glb", GlbWriter.Write(Bare(sword, 1.75f), "selftest", libraryUnits: false));
			Put(dir, "models/held_axe_iron.glb", GlbWriter.Write(Bare(meshModel, 1.75f), "selftest", libraryUnits: false));
			Put(dir, "models/tool_pickaxe.glb", GlbWriter.Write(Bare(pick, 3f), "selftest", libraryUnits: false));
			Put(dir, "models/tool_shovel.glb", GlbWriter.Write(Bare(codeShovel, 2.1000001f), "selftest", libraryUnits: false));
			Put(dir, "models/tree_pine.glb", ToGlb(Models.Own("tree_pine")));
			Packs.Discover();
			Packs.SetActive(new string[1] { "wftest_units@local" });
			yield return Applied();
			PackInfo packInfo = Packs.Find("wftest_units@local");
			PackContent packContent = ((packInfo != null) ? Packs.ContentOf(packInfo) : null);
			if (!Check("the sizes pack loaded", packContent != null && packContent.Models.Count == 5, (packContent == null) ? "no pack" : (packContent.Models.Count + " models; " + string.Join(" | ", packContent.Problems.ToArray()))))
			{
				yield break;
			}
			MeshModel meshModel2 = Models.Get("held_sword_iron");
			Check("a sword made in metres is the library's size", meshModel2 == packContent.Models["held_sword_iron"] && Near(Packs.Longest(meshModel2), Packs.Longest(sword)), Size(meshModel2) + " vs " + Size(sword));
			Check("with no info of its own it keeps the original's", meshModel2.Info("kind") == "sword" && meshModel2.Info("tip") == sword.Info("tip"), meshModel2.InfoText());
			MeshModel meshModel3 = Models.Get("held_axe_iron");
			Check("worldfall.json's tip read as metres", meshModel3 != null && meshModel3.Info("tip") == Packs.ScaleNumbers("0,0.3,0.7", 0.5714286f) && meshModel3.Info("kind") == "axe", meshModel3?.InfoText());
			MeshModel meshModel4 = Models.Get("tool_pickaxe");
			Check("a pickaxe three times too big, fitted to the original", meshModel4 != null && Near(Packs.Longest(meshModel4), Packs.Longest(pick)), Size(meshModel4) + " vs " + Size(pick));
			MeshModel sh = Models.Get("tool_shovel");
			Check("a shovel in metres, 1.2 x the code one's length", sh != null && Near(Packs.Longest(sh), Packs.Longest(codeShovel) * 1.2f), Size(sh) + " vs " + Size(codeShovel));
			MeshModel meshModel5 = Models.Get("tree_pine");
			Check("a file in the library's units kept its size", meshModel5 != null && Near(Packs.Longest(meshModel5), Packs.Longest(Models.Own("tree_pine")), 0.001f), Size(meshModel5));
			PackInfo p = new PackInfo
			{
				Fit = true
			};
			MeshModel m = Packs.Finish(p, "tool_shovel", Bare(codeShovel, 3f), null, new List<string>());
			Check("fit on tool_shovel fits it to the shovel made in code", Near(Packs.Longest(m), Packs.Longest(codeShovel)), Size(m));
			List<string> list = new List<string>();
			MeshModel m2 = Packs.Finish(p, "animal_wftestnothing", Bare(Models.Own("animal_wolf"), 2f), null, list);
			Check("fit with nothing to fit to keeps the size, no error", list.Count == 0 && Near(Packs.Longest(m2), Packs.Longest(Models.Own("animal_wolf")) * 2f), Size(m2));
			Check("the dragon and Crabzilla are in tiles", Packs.LibraryUnit("monster_dragon", Models.Own("monster_dragon")) == 0.5714286f && (Models.Own("crabzilla") == null || Packs.LibraryUnit("crabzilla", Models.Own("crabzilla")) == 0.5714286f) && Packs.LibraryUnit("animal_wolf", Models.Own("animal_wolf")) == 1f && Packs.LibraryUnit("house_human_0", Models.Own("house_human_0")) == 1f);
			string path = Path.Combine(Packs.LocalFolder, "model_names.txt");
			try
			{
				if (File.Exists(path))
				{
					File.Delete(path);
				}
			}
			catch
			{
			}
			PackModelNames.Write();
			string text = null;
			if (File.Exists(path))
			{
				string[] array = File.ReadAllLines(path);
				foreach (string text2 in array)
				{
					if (text2.StartsWith("held_sword_iron\t", StringComparison.Ordinal))
					{
						text = text2;
						break;
					}
				}
			}
			string[] array2 = text?.Split('\t');
			string text3 = Packs.ScaleNumbers(sword.Info("tip"), 1.7499999f);
			Check("model_names.txt gives the sword in metres", array2 != null && array2.Length >= 5 && array2[1].EndsWith(" m", StringComparison.Ordinal) && array2[2].Contains("tip=" + text3), text);
			if (_host == null || !InControl(_host))
			{
				Note("no host in control: the hands checked by lookup only");
			}
			else
			{
				Supplies.Give(_host, "tw_item_shovel", 1);
				yield return null;
				int num = BagOrder.Of(_host).FindIndex((string e) => BagOrder.ResourceOf(e) == "tw_item_shovel");
				if (num >= Hotbar.Size(_host))
				{
					BagOrder.Swap(_host, num, 0);
					num = 0;
				}
				if (num >= 0)
				{
					Hotbar.Choose(_host, num, wheel: false);
				}
				Mod.Holstered = false;
				yield return null;
				yield return null;
				MethodInfo method = typeof(WorldBoxMod).GetMethod("DigHands", BindingFlags.Instance | BindingFlags.NonPublic);
				object[] array3 = new object[2]
				{
					_host,
					default(HandsInput)
				};
				method?.Invoke(Mod, array3);
				HandsInput handsInput = (HandsInput)array3[1];
				Check("the dig shovel in your hands is the pack's", Earthworks.ToolOf(_host) == "tw_item_shovel" && handsInput.HeldModel == sh, "tool " + Earthworks.ToolOf(_host) + ", held " + (handsInput.HeldModel?.Name ?? "none") + " " + Size(handsInput.HeldModel));
				if (Options.Contains("capture"))
				{
					yield return new WaitForSeconds(0.8f);
					Mod.SaveFrame("units_shovel.png");
				}
			}
			Check("CarryLook finds the pack's tool_shovel", CarryLook.Tool("shovel")?.Model(0) == sh);
		}
	}
}
