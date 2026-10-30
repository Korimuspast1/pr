using System;
using System.Collections.Generic;
using FirstPerson.Core;

namespace FirstPerson
{
	internal sealed class PackContent
	{
		public string Key;

		public DateTime Stamp;

		public long Sig;

		public readonly Dictionary<string, MeshModel> Models = new Dictionary<string, MeshModel>(StringComparer.Ordinal);

		public readonly Dictionary<string, GlbExtras> ModelExtras = new Dictionary<string, GlbExtras>(StringComparer.Ordinal);

		public readonly Dictionary<string, List<byte[]>> Sounds = new Dictionary<string, List<byte[]>>(StringComparer.Ordinal);

		public readonly Dictionary<string, string> Lang = new Dictionary<string, string>(StringComparer.Ordinal);

		public readonly List<string> Problems = new List<string>();

		public readonly List<string> Warnings = new List<string>();

		public double Ms;
	}
}
