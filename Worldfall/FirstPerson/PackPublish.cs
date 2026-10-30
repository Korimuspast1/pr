using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FirstPerson.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Steamworks;
using Steamworks.Ugc;

namespace FirstPerson
{
	internal static class PackPublish
	{
		internal sealed class Job
		{
			public PackInfo Pack;

			public int Visibility;

			public string Changelog = "";

			public bool Dry;

			public string Stage;

			public string Preview;

			public readonly List<string> Report = new List<string>();

			public string State = "";

			public float Progress;

			public bool Done;

			public bool Failed;

			public bool Retried;

			public ulong Item;

			public Task<PublishResult> Upload;

			public ProgressBox Box;
		}

		internal sealed class ProgressBox : IProgress<float>
		{
			public volatile float Value;

			public void Report(float v)
			{
				Value = v;
			}
		}

		public static readonly string[] Visibilities = new string[3] { "Public", "Friends", "Hidden" };

		public static Job Current;

		public static List<string> Check(PackInfo p)
		{
			List<string> list = new List<string>();
			if (p == null)
			{
				list.Add("No pack chosen");
				return list;
			}
			if (p.Source != PackSource.Local)
			{
				list.Add("Only packs in FirstPerson/packs can be published from here");
			}
			foreach (string error in p.Errors)
			{
				list.Add(error);
			}
			if (Packs.Problems.TryGetValue(p.Key, out var value))
			{
				list.AddRange(value);
			}
			if (p.Overrides == 0 && !p.Kinds.Exists((string k) => k == "shader" || k == "texture"))
			{
				list.Add("It has nothing Worldfall uses");
			}
			if (p.Title.Length > 128)
			{
				list.Add("The title is over 128 letters");
			}
			if ((p.Description ?? "").Length > 7000)
			{
				list.Add("The description is over 7000 letters");
			}
			if (p.Preview != null && new FileInfo(p.Preview).Length >= 1048576)
			{
				list.Add("preview.png must be under 1 MB for the Workshop");
			}
			if (Encoding.UTF8.GetByteCount(Metadata(p)) > 5000)
			{
				list.Add("worldfall.json is too long for the Workshop (5000 bytes)");
			}
			return list;
		}

		public static Job Start(PackInfo p, int visibility, string changelog, bool dry)
		{
			Job job = (Current = new Job
			{
				Pack = p,
				Visibility = visibility,
				Changelog = (changelog ?? ""),
				Dry = dry
			});
			List<string> list = Check(p);
			if (list.Count > 0)
			{
				job.Failed = (job.Done = true);
				job.State = list[0];
				job.Report.AddRange(list);
				return job;
			}
			try
			{
				Stage(job);
				Describe(job);
				File.WriteAllText(Path.Combine(Path.Combine(Log.Folder, "publish"), p.Id + (dry ? "_dryrun.txt" : "_publish.txt")), string.Join("\n", job.Report.ToArray()) + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				Log.Info("packs: " + (dry ? "dry run of publishing " : "publishing ") + p.Key + ":\n  " + string.Join("\n  ", job.Report.ToArray()));
				if (dry)
				{
					job.Done = true;
					job.State = "Dry run done. Nothing was sent";
					return job;
				}
				if (!PackSteam.Available)
				{
					throw new InvalidOperationException("Steam is not running");
				}
				Submit(job);
			}
			catch (Exception ex)
			{
				job.Failed = (job.Done = true);
				job.State = "Could not publish. " + ex.Message;
				Log.Error("packs: publishing " + p.Key + ": " + ex);
			}
			return job;
		}

		private static void Stage(Job job)
		{
			PackInfo pack = job.Pack;
			string fullPath = Path.GetFullPath(Path.Combine(Log.Folder, "publish"));
			string fullPath2 = Path.GetFullPath(Path.Combine(fullPath, pack.Id));
			if (!fullPath2.StartsWith(fullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			{
				throw new IOException("bad pack id: " + pack.Id);
			}
			if (Directory.Exists(fullPath2))
			{
				Directory.Delete(fullPath2, recursive: true);
			}
			string text = Path.Combine(fullPath2, "Worldfall");
			Directory.CreateDirectory(text);
			job.Stage = fullPath2;
			Copy(Path.Combine(pack.Folder, "worldfall.json"), Path.Combine(text, "worldfall.json"));
			foreach (KeyValuePair<string, string> modelFile in pack.ModelFiles)
			{
				Copy(modelFile.Value, Path.Combine(Path.Combine(text, "models"), modelFile.Key + ".glb"));
			}
			foreach (KeyValuePair<string, List<string>> soundFile in pack.SoundFiles)
			{
				foreach (string item in soundFile.Value)
				{
					Copy(item, Path.Combine(Path.Combine(Path.Combine(text, "sounds"), soundFile.Key), Path.GetFileName(item)));
				}
			}
			foreach (KeyValuePair<string, string> langFile in pack.LangFiles)
			{
				Copy(langFile.Value, Path.Combine(Path.Combine(text, "lang"), langFile.Key + ".txt"));
			}
			string text2 = Path.Combine(text, "preview.png");
			if (pack.Preview != null)
			{
				Copy(pack.Preview, text2);
			}
			else
			{
				MeshModel meshModel = FirstModel(pack);
				if (meshModel == null || !PackPreview.Save(meshModel, text2))
				{
					text2 = null;
				}
				else
				{
					job.Report.Add("preview: drawn from its model " + meshModel.Name);
				}
			}
			job.Preview = text2;
			if (text2 != null)
			{
				Copy(text2, Path.Combine(fullPath2, "icon.png"));
			}
			File.WriteAllText(Path.Combine(fullPath2, "mod.json"), ModJson(pack), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			Directory.CreateDirectory(Path.Combine(fullPath2, "Code"));
			File.WriteAllText(Path.Combine(Path.Combine(fullPath2, "Code"), "Pack.cs"), Stub(pack), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		}

		private static MeshModel FirstModel(PackInfo p)
		{
			PackContent packContent = Packs.ContentOf(p);
			List<string> list = new List<string>(packContent.Models.Keys);
			list.Sort(StringComparer.Ordinal);
			foreach (string item in list)
			{
				if (!Packs.IsLodName(item))
				{
					return packContent.Models[item];
				}
			}
			if (list.Count <= 0)
			{
				return null;
			}
			return packContent.Models[list[0]];
		}

		private static void Copy(string from, string to)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(to));
			File.Copy(from, to, overwrite: true);
		}

		internal static string ModJson(PackInfo p)
		{
			return new JObject
			{
				["name"] = p.Title,
				["author"] = p.Author,
				["version"] = p.Version,
				["description"] = (string.IsNullOrEmpty(p.Description) ? "A pack for Worldfall" : p.Description),
				["GUID"] = "worldfall_pack." + p.Id,
				["iconPath"] = "icon.png",
				["Dependencies"] = new JArray("s3cond2.worldfall")
			}.ToString();
		}

		internal static string Stub(PackInfo p)
		{
			string text = "WorldfallPack_" + Packs.Slug(p.Id).Replace('.', '_').Replace('-', '_');
			return "// A Worldfall pack: its models, sounds and text are read by Worldfall from the Worldfall folder beside this one.\n// NeoModLoader lists a mod only when it has code, so this is all there is.\nusing NeoModLoader.api;\n\nnamespace " + text + "\n{\n    public class Pack : BasicMod<Pack>\n    {\n        protected override void OnModLoad()\n        {\n        }\n    }\n}\n";
		}

		private static string Metadata(PackInfo p)
		{
			try
			{
				return JObject.Parse(File.ReadAllText(Path.Combine(p.Folder, "worldfall.json"))).ToString(Formatting.None);
			}
			catch
			{
				return "{}";
			}
		}

		private static string WorkshopDescription(PackInfo p)
		{
			StringBuilder stringBuilder = new StringBuilder();
			if (!string.IsNullOrEmpty(p.Description))
			{
				stringBuilder.AppendLine(p.Description).AppendLine();
			}
			stringBuilder.AppendLine("A pack for [url=https://steamcommunity.com/sharedfiles/filedetails/?id=" + 3814551764uL + "]Worldfall[/url]. Turn it on in Worldfall's F2 > Mods.");
			stringBuilder.AppendLine();
			if (p.ModelFiles.Count > 0)
			{
				stringBuilder.AppendLine("Models: " + string.Join(", ", new List<string>(p.ModelFiles.Keys).ToArray()));
			}
			if (p.SoundFiles.Count > 0)
			{
				stringBuilder.AppendLine("Sounds: " + string.Join(", ", new List<string>(p.SoundFiles.Keys).ToArray()));
			}
			if (p.LangFiles.Count > 0)
			{
				stringBuilder.AppendLine("Languages: " + string.Join(", ", new List<string>(p.LangFiles.Keys).ToArray()));
			}
			return stringBuilder.ToString();
		}

		private static List<string> Tags(PackInfo p)
		{
			List<string> list = new List<string> { "Unverified Mod", "Worldfall Pack" };
			foreach (string kind in p.Kinds)
			{
				list.Add("wf:" + kind);
			}
			return list;
		}

		private static void Describe(Job job)
		{
			PackInfo pack = job.Pack;
			List<string> report = job.Report;
			report.Insert(0, (job.Dry ? "DRY RUN (nothing sent). " : "") + ((pack.WorkshopId != 0L) ? ("Update of Workshop item " + pack.WorkshopId) : "New Workshop item") + " for pack " + pack.Key);
			report.Add("title: " + pack.Title);
			report.Add("visibility: " + Visibilities[Math.Max(0, Math.Min(2, job.Visibility))]);
			report.Add("tags: " + string.Join(", ", Tags(pack).ToArray()));
			report.Add(string.Concat("key-value tags: worldfall=pack", string.Concat(pack.Kinds.ConvertAll((string k) => ", wf_kind=" + k).ToArray())));
			report.Add("metadata (worldfall.json): " + Encoding.UTF8.GetByteCount(Metadata(pack)) + " bytes");
			report.Add("required items: Worldfall " + 3814551764uL + ", NeoModLoader " + 3080294469uL + ((pack.Requires.Count > 0) ? (", " + string.Join(", ", pack.Requires.ConvertAll((ulong x) => x.ToString()).ToArray())) : ""));
			report.Add("preview: " + ((job.Preview != null) ? (Path.GetFileName(job.Preview) + " " + new FileInfo(job.Preview).Length + " bytes") : "none"));
			report.Add("changelog: " + ((job.Changelog.Length > 0) ? job.Changelog : "(none)"));
			report.Add("description:\n    " + WorkshopDescription(pack).TrimEnd().Replace("\n", "\n    "));
			long num = 0L;
			report.Add("content " + job.Stage + ":");
			string[] files = Directory.GetFiles(job.Stage, "*", SearchOption.AllDirectories);
			foreach (string text in files)
			{
				long length = new FileInfo(text).Length;
				num += length;
				report.Add("    " + text.Substring(job.Stage.Length + 1).Replace('\\', '/') + "  " + length + " bytes");
			}
			report.Add("total: " + num + " bytes");
			report.Add("mod.json:\n    " + ModJson(pack).Replace("\n", "\n    "));
		}

		private static void Submit(Job job)
		{
			PackInfo pack = job.Pack;
			Editor editor = ((pack.WorkshopId != 0L) ? new Editor(pack.WorkshopId) : Editor.NewCommunityFile).WithTitle(pack.Title).WithDescription(WorkshopDescription(pack)).WithContent(job.Stage)
				.WithMetaData(Metadata(pack))
				.WithChangeLog(job.Changelog);
			if (job.Preview != null)
			{
				editor = editor.WithPreviewFile(job.Preview);
			}
			foreach (string item in Tags(pack))
			{
				editor = editor.WithTag(item);
			}
			editor = editor.AddKeyValueTag("worldfall", "pack");
			foreach (string kind in pack.Kinds)
			{
				editor = editor.AddKeyValueTag("wf_kind", kind);
			}
			editor = ((job.Visibility == 1) ? editor.WithFriendsOnlyVisibility() : ((job.Visibility == 2) ? editor.WithPrivateVisibility() : editor.WithPublicVisibility()));
			job.Box = new ProgressBox();
			job.Upload = editor.SubmitAsync(job.Box);
			job.State = "Uploading";
		}

		public static void Tick()
		{
			Job current = Current;
			if (current == null || current.Done || current.Upload == null)
			{
				return;
			}
			current.Progress = ((current.Box != null) ? current.Box.Value : 0f);
			if (!current.Upload.IsCompleted)
			{
				return;
			}
			try
			{
				if (current.Upload.IsFaulted)
				{
					throw current.Upload.Exception.GetBaseException();
				}
				PublishResult result = current.Upload.Result;
				if (!result.Success)
				{
					if (result.FileId.Value != 0L && current.Pack.WorkshopId != result.FileId.Value)
					{
						Remember(current.Pack, result.FileId.Value);
					}
					if (!current.Retried && result.Result == Result.Timeout)
					{
						current.Retried = true;
						Log.Info("packs: publishing " + current.Pack.Key + ": Steam said Timeout, trying again" + ((result.FileId.Value != 0L) ? (" into item " + result.FileId.Value) : ""));
						Submit(current);
						return;
					}
					throw new InvalidOperationException("Steam said " + result.Result.ToString() + (result.NeedsWorkshopAgreement ? ". Accept the Workshop agreement on Steam first" : "") + ((current.Pack.WorkshopId != 0L) ? (". Publish again to finish item " + current.Pack.WorkshopId) : ""));
				}
				current.Item = result.FileId.Value;
				PackSteam.AddDependency(current.Item, 3814551764uL);
				PackSteam.AddDependency(current.Item, 3080294469uL);
				foreach (ulong require in current.Pack.Requires)
				{
					PackSteam.AddDependency(current.Item, require);
				}
				Remember(current.Pack, current.Item);
				current.State = "Published. Item " + current.Item;
				Log.Info("packs: published " + current.Pack.Key + " as Workshop item " + current.Item + (result.NeedsWorkshopAgreement ? " (the Workshop agreement is not accepted yet: the item stays hidden)" : ""));
			}
			catch (Exception ex)
			{
				current.Failed = true;
				current.State = "Could not publish. " + ex.Message;
				Log.Error("packs: publishing " + current.Pack.Key + ": " + ex.Message);
			}
			current.Done = true;
		}

		private static void Remember(PackInfo p, ulong item)
		{
			string path = Path.Combine(p.Folder, "worldfall.json");
			JObject jObject = JObject.Parse(File.ReadAllText(path));
			jObject["workshop"] = item.ToString();
			File.WriteAllText(path, jObject.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			p.WorkshopId = item;
		}
	}
}
