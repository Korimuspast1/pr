using System;
using System.Collections.Generic;

namespace FirstPerson
{
	internal sealed class PackInfo
	{
		public string Key;

		public string Id;

		public string Title;

		public string Author;

		public string Version;

		public string Description;

		public string MinWorldfall;

		public PackSource Source;

		public ulong WorkshopId;

		public string Folder;

		public string ModFolder;

		public string Preview;

		public readonly List<string> Kinds = new List<string>();

		public readonly List<string> LoadAfter = new List<string>();

		public readonly List<ulong> Requires = new List<ulong>();

		public readonly Dictionary<string, string> ModelFiles = new Dictionary<string, string>(StringComparer.Ordinal);

		public readonly Dictionary<string, List<string>> SoundFiles = new Dictionary<string, List<string>>(StringComparer.Ordinal);

		public readonly Dictionary<string, string> LangFiles = new Dictionary<string, string>(StringComparer.Ordinal);

		public readonly Dictionary<string, string> ModelInfo = new Dictionary<string, string>(StringComparer.Ordinal);

		public bool Fit;

		public readonly Dictionary<string, bool> ModelFit = new Dictionary<string, bool>(StringComparer.Ordinal);

		public readonly Dictionary<string, string> ModelTextures = new Dictionary<string, string>(StringComparer.Ordinal);

		public readonly List<string> Errors = new List<string>();

		public readonly List<string> Warnings = new List<string>();

		public DateTime Stamp;

		public long Quick;

		public long Sig;

		public bool NewerWorldfall;

		public int Overrides => ModelFiles.Count + SoundFiles.Count + LangFiles.Count;

		public string SourceName
		{
			get
			{
				if (Source != PackSource.Local)
				{
					if (Source != PackSource.Mods)
					{
						return "Workshop";
					}
					return "Mods folder";
				}
				return "Local";
			}
		}

		public override string ToString()
		{
			return Key;
		}
	}
}
