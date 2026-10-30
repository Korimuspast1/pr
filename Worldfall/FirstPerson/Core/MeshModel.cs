using System.Collections.Generic;
using System.Globalization;

namespace FirstPerson.Core
{
	public sealed class MeshModel
	{
		public string Name;

		public string[] SlotNames;

		public byte[] SlotKind;

		public byte[] SlotFlags;

		public uint[] SlotColor;

		public MeshTexture[] Textures;

		public int[] SlotTexture;

		public MeshPart[] Parts;

		public Vec3 Min;

		public Vec3 Max;

		private readonly Dictionary<string, string> _info = new Dictionary<string, string>();

		public bool Textured
		{
			get
			{
				if (SlotTexture != null && Textures != null)
				{
					return Textures.Length != 0;
				}
				return false;
			}
		}

		public int SlotIndex(string name)
		{
			for (int i = 0; i < SlotNames.Length; i++)
			{
				if (SlotNames[i] == name)
				{
					return i;
				}
			}
			return -1;
		}

		public int PartIndex(string name)
		{
			for (int i = 0; i < Parts.Length; i++)
			{
				if (Parts[i].Name == name)
				{
					return i;
				}
			}
			return -1;
		}

		public string Info(string key)
		{
			if (!_info.TryGetValue(key, out var value))
			{
				return null;
			}
			return value;
		}

		public string InfoText()
		{
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, string> item in _info)
			{
				list.Add(item.Key + "=" + item.Value);
			}
			return string.Join(";", list.ToArray());
		}

		public float InfoFloat(string key, float fallback)
		{
			if (!float.TryParse(Info(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				return fallback;
			}
			return result;
		}

		public MeshModel Derive(string name, MeshPart[] parts)
		{
			MeshModel meshModel = new MeshModel
			{
				Name = name,
				SlotNames = SlotNames,
				SlotKind = SlotKind,
				SlotFlags = SlotFlags,
				SlotColor = SlotColor,
				Textures = Textures,
				SlotTexture = SlotTexture,
				Parts = parts,
				Min = Min,
				Max = Max
			};
			foreach (KeyValuePair<string, string> item in _info)
			{
				meshModel._info[item.Key] = item.Value;
			}
			return meshModel;
		}

		internal void ClearInfo()
		{
			_info.Clear();
		}

		internal void ParseInfo(string info)
		{
			string[] array = info.Split(';');
			foreach (string text in array)
			{
				int num = text.IndexOf('=');
				if (num > 0)
				{
					_info[text.Substring(0, num)] = text.Substring(num + 1);
				}
			}
		}
	}
}
