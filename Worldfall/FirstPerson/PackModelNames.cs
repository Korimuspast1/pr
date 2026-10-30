using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FirstPerson.Core;

namespace FirstPerson
{
	internal static class PackModelNames
	{
		private static readonly HashSet<string> Asked = new HashSet<string>(StringComparer.Ordinal);

		private static readonly object Gate = new object();

		private static int _written = -1;

		public static Func<List<string>> SoundSets;

		internal static readonly string[] CodeTools = new string[1] { "shovel" };

		private static readonly List<string> _unloaded = new List<string>();

		public static List<string> AskedIds
		{
			get
			{
				lock (Gate)
				{
					List<string> list = new List<string>(Asked);
					list.Sort(StringComparer.Ordinal);
					return list;
				}
			}
		}

		public static void NoteCreature(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return;
			}
			lock (Gate)
			{
				Asked.Add(id);
			}
		}

		public static bool IsMadeInCode(string name)
		{
			if (name.StartsWith("tool_", StringComparison.Ordinal))
			{
				return Array.IndexOf(CodeTools, name.Substring(5)) >= 0;
			}
			return false;
		}

		public static bool IsAskedFor(string name)
		{
			if (!name.StartsWith("animal_", StringComparison.Ordinal) && !name.StartsWith("monster_", StringComparison.Ordinal))
			{
				return name.StartsWith("person_", StringComparison.Ordinal);
			}
			return true;
		}

		public static void Write()
		{
			try
			{
				Directory.CreateDirectory(Packs.LocalFolder);
				string path = Path.Combine(Packs.LocalFolder, "model_names.txt");
				if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddDays(-1.0) || _written < 0 || PeopleLoadedSince())
				{
					_unloaded.Clear();
					StringBuilder stringBuilder = new StringBuilder();
					stringBuilder.AppendLine("# Every model Worldfall 0.9.8.9 has. A pack replaces one with models/<name>.glb.");
					stringBuilder.AppendLine("# <name>_m and <name>_f are the middle and far versions; a pack that gives only <name> is drawn with it at every distance.");
					stringBuilder.AppendLine("# size: the model's box in metres, as a pack maker builds it (width x depth x height: Blender's X, Y, Z).");
					stringBuilder.AppendLine("# info: kept from this model when a pack's has none of its own; its lengths and points are in metres.");
					stringBuilder.AppendLine("# Guide: https://worldfall3d.com/modding/");
					stringBuilder.AppendLine("# name<TAB>size<TAB>info<TAB>parts<TAB>slots");
					foreach (string item in Models.AllNames())
					{
						MeshModel meshModel = Models.Own(item);
						if (meshModel == null)
						{
							_unloaded.Add(item);
							stringBuilder.AppendLine(item + "\t(in a people's pack, loaded when that people is in the world)");
							continue;
						}
						List<string> list = new List<string>();
						MeshPart[] parts = meshModel.Parts;
						foreach (MeshPart meshPart in parts)
						{
							list.Add(meshPart.Name);
						}
						stringBuilder.Append(item).Append('\t').Append(SizeOf(item, meshModel))
							.Append('\t')
							.Append(Packs.InfoInMetres(item, meshModel))
							.Append('\t')
							.Append(string.Join(" ", list.ToArray()))
							.Append('\t')
							.AppendLine(string.Join(" ", meshModel.SlotNames));
					}
					string[] codeTools = CodeTools;
					foreach (string text in codeTools)
					{
						if (!Models.Known("tool_" + text))
						{
							MeshModel meshModel2 = ToolMeshes.Tool(text);
							if (meshModel2 != null)
							{
								stringBuilder.Append("tool_").Append(text).Append('\t')
									.Append(SizeOf("tool_" + text, meshModel2))
									.Append('\t')
									.Append("(made in code; held like a pickaxe)")
									.Append('\t')
									.Append("item")
									.Append('\t')
									.AppendLine(string.Join(" ", meshModel2.SlotNames));
							}
						}
					}
					File.WriteAllText(path, stringBuilder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				}
				List<string> list2 = SoundSets?.Invoke();
				if (list2 != null && list2.Count > 0)
				{
					File.WriteAllText(Path.Combine(Packs.LocalFolder, "sound_sets.txt"), "# The sound sets a pack replaces with sounds/<set>/*.wav or .ogg (the whole set, files played in turn).\n" + string.Join("\n", list2.ToArray()) + "\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				}
				List<string> askedIds = AskedIds;
				if (askedIds.Count == _written)
				{
					return;
				}
				StringBuilder stringBuilder2 = new StringBuilder();
				stringBuilder2.AppendLine("# Creatures seen in this game with no model of their own (another mod's, or ones Worldfall draws from their sprites).");
				stringBuilder2.AppendLine("# A pack gives one as models/animal_<id>.glb (or person_<id>.glb for one that walks like a person).");
				foreach (string item2 in askedIds)
				{
					stringBuilder2.AppendLine("animal_" + item2);
				}
				File.WriteAllText(Path.Combine(Packs.LocalFolder, "names_asked.txt"), stringBuilder2.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				_written = askedIds.Count;
			}
			catch (Exception ex)
			{
				Log.Info("packs: could not write the model names (" + ex.Message + ")");
			}
		}

		private static bool PeopleLoadedSince()
		{
			foreach (string item in _unloaded)
			{
				if (Models.Own(item) != null)
				{
					return true;
				}
			}
			return false;
		}

		private static string SizeOf(string name, MeshModel m)
		{
			float num = 1f / Packs.LibraryUnit(name, m);
			Vec3 vec = (m.Max - m.Min) * num;
			CultureInfo invariantCulture = CultureInfo.InvariantCulture;
			return vec.X.ToString("0.##", invariantCulture) + " x " + vec.Y.ToString("0.##", invariantCulture) + " x " + vec.Z.ToString("0.##", invariantCulture) + " m";
		}
	}
}
