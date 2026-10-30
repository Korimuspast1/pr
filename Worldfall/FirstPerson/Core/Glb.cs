using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FirstPerson.Core
{
	public static class Glb
	{
		private sealed class Bad : Exception
		{
			public Bad(string message)
				: base(message)
			{
			}
		}

		private sealed class SlotDef
		{
			public string Name;

			public byte Kind;

			public byte Flags;

			public int R;

			public int G;

			public int B;

			public bool Foliage;

			public int Image = -1;

			public int Base;
		}

		private sealed class PartBuild
		{
			public string Name;

			public int Parent;

			public int Node;

			public double PX;

			public double PY;

			public double PZ;

			public readonly List<float> Pos = new List<float>();

			public readonly List<float> Nrm = new List<float>();

			public readonly List<float> Uv = new List<float>();

			public readonly List<int> Tri = new List<int>();

			public readonly List<byte> Slot = new List<byte>();

			public bool AnyUv;

			public double Order = double.NaN;
		}

		private struct Acc
		{
			public int Count;

			public int Comps;

			public int CompType;

			public int CompSize;

			public int Stride;

			public long Start;

			public bool Normalized;
		}

		private sealed class Reader
		{
			private readonly byte[] _d;

			private readonly string _name;

			private readonly GlbLimits _lim;

			private readonly List<string> _problems;

			private long _binStart = -1L;

			private long _binLength;

			private List<object> _accessors;

			private List<object> _views;

			private List<object> _buffers;

			private List<object> _meshes;

			private List<object> _nodes;

			private List<object> _materials;

			private List<object> _textures;

			private List<object> _images;

			private readonly List<SlotDef> _slots = new List<SlotDef>();

			private readonly Dictionary<string, int> _slotByName = new Dictionary<string, int>();

			private readonly Dictionary<int, int> _slotOfMaterial = new Dictionary<int, int>();

			private readonly List<PartBuild> _parts = new List<PartBuild>();

			private readonly HashSet<string> _partNames = new HashSet<string>();

			private readonly HashSet<string> _noted = new HashSet<string>();

			private bool[] _visited;

			private int _verts;

			private int _tris;

			private long _turned;

			public GlbExtras Extras;

			private readonly Dictionary<long, (float[] Data, int Comps)> _floats = new Dictionary<long, (float[], int)>();

			private static readonly List<object> Empty = new List<object>();

			public Reader(byte[] data, string name, GlbLimits limits, List<string> problems)
			{
				_d = data;
				_name = name;
				_lim = limits;
				_problems = problems;
			}

			private void Note(string problem)
			{
				if (_noted.Add(problem))
				{
					_problems.Add(problem);
				}
			}

			public MeshModel Run()
			{
				if (!(JsonReader.Parse(Container()) is Dictionary<string, object> dictionary))
				{
					throw new Bad("the file's JSON is not an object");
				}
				Dictionary<string, object> dictionary2 = Obj(dictionary, "asset");
				string text = ((dictionary2 != null) ? Str(dictionary2, "version") : null);
				if (text == null || !text.StartsWith("2.", StringComparison.Ordinal))
				{
					throw new Bad("not glTF 2.0 (asset.version " + (text ?? "missing") + ")");
				}
				foreach (object item in Arr(dictionary, "extensionsRequired"))
				{
					string text2 = item as string;
					switch (text2)
					{
					case "KHR_draco_mesh_compression":
					case "EXT_meshopt_compression":
					case "KHR_mesh_quantization":
						throw new Bad("the meshes are compressed (" + text2 + "): export without compression");
					}
					Note("the file needs " + text2 + ", which is not read (ignored)");
				}
				_accessors = Arr(dictionary, "accessors");
				_views = Arr(dictionary, "bufferViews");
				_buffers = Arr(dictionary, "buffers");
				_meshes = Arr(dictionary, "meshes");
				_nodes = Arr(dictionary, "nodes");
				_materials = Arr(dictionary, "materials");
				_textures = Arr(dictionary, "textures");
				_images = Arr(dictionary, "images");
				List<object> list = Arr(dictionary, "scenes");
				if (_nodes.Count > _lim.MaxNodes)
				{
					throw new Bad("too many nodes (" + _nodes.Count + ", at most " + _lim.MaxNodes + ")");
				}
				foreach (object buffer in _buffers)
				{
					if (buffer is Dictionary<string, object> dictionary3 && dictionary3.ContainsKey("uri"))
					{
						throw new Bad("the model's data is in another file or a data: link; only data inside the .glb is read");
					}
				}
				Extras = new GlbExtras();
				ReadImages();
				List<int> list2 = new List<int>();
				Dictionary<string, object> dictionary4 = null;
				int num = (dictionary.ContainsKey("scene") ? Int(dictionary, "scene", -1) : ((list.Count <= 0) ? (-1) : 0));
				if (num >= 0)
				{
					if (num >= list.Count)
					{
						throw new Bad("the default scene " + num + " does not exist");
					}
					dictionary4 = list[num] as Dictionary<string, object>;
					if (dictionary4 == null)
					{
						throw new Bad("scene " + num + " is not an object");
					}
					foreach (object item2 in Arr(dictionary4, "nodes"))
					{
						list2.Add(ToInt(item2, "a scene's node"));
					}
				}
				else
				{
					bool[] array = new bool[_nodes.Count];
					for (int i = 0; i < _nodes.Count; i++)
					{
						foreach (object item3 in Arr(NodeAt(i), "children"))
						{
							int num2 = ToInt(item3, "a node's child");
							if (num2 >= 0 && num2 < array.Length)
							{
								array[num2] = true;
							}
						}
					}
					for (int j = 0; j < _nodes.Count; j++)
					{
						if (!array[j])
						{
							list2.Add(j);
						}
					}
				}
				string text3 = ExtraString(dictionary4, "slots") ?? ExtraString(dictionary2, "slots");
				if (text3 != null)
				{
					PresetSlots(text3);
				}
				Extras.LibraryUnits = (ExtraString(dictionary4, "units") ?? ExtraString(dictionary2, "units")) == "library";
				_visited = new bool[_nodes.Count];
				foreach (int item4 in list2)
				{
					Walk(item4, Identity(), -1, 0);
				}
				if (_parts.Count == 0)
				{
					throw new Bad("no meshes in the scene");
				}
				if (_tris == 0)
				{
					throw new Bad("no triangles in the scene");
				}
				Reorder();
				string info = ExtraString(dictionary4, "info") ?? ExtraString(dictionary2, "info") ?? ((list2.Count > 0 && list2[0] >= 0 && list2[0] < _nodes.Count) ? ExtraString(_nodes[list2[0]] as Dictionary<string, object>, "info") : null);
				return Build(info);
			}

			private uint U32(long at)
			{
				return (uint)(_d[at] | (_d[at + 1] << 8) | (_d[at + 2] << 16) | (_d[at + 3] << 24));
			}

			private string Container()
			{
				if (_d.Length < 12)
				{
					throw new Bad("not a .glb file (too short)");
				}
				if (_d[0] != 103 || _d[1] != 108 || _d[2] != 84 || _d[3] != 70)
				{
					throw new Bad("not a .glb file (no glTF header)");
				}
				uint num = U32(4L);
				if (num != 2)
				{
					throw new Bad("glTF binary version " + num + ", only 2 is read");
				}
				long num2 = U32(8L);
				if (num2 != _d.Length)
				{
					throw new Bad("the file is cut short or has bytes after its end (header says " + num2 + " bytes, the file has " + _d.Length + ")");
				}
				if (_d.Length > _lim.MaxBytes)
				{
					throw new Bad("the file is too big (" + _d.Length + " bytes, at most " + _lim.MaxBytes + ")");
				}
				if (_d.Length < 20)
				{
					throw new Bad("no JSON chunk");
				}
				long num3 = U32(12L);
				if (U32(16L) != 1313821514)
				{
					throw new Bad("the first chunk is not JSON");
				}
				if (num3 > _lim.MaxJsonBytes)
				{
					throw new Bad("the JSON chunk is too big (" + num3 + " bytes, at most " + _lim.MaxJsonBytes + ")");
				}
				if (20 + num3 > _d.Length)
				{
					throw new Bad("the JSON chunk runs past the end of the file");
				}
				string result;
				try
				{
					int num4 = 20;
					int num5 = (int)num3;
					if (num5 >= 3 && _d[20] == 239 && _d[21] == 187 && _d[22] == 191)
					{
						num4 += 3;
						num5 -= 3;
					}
					result = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(_d, num4, num5);
				}
				catch (ArgumentException)
				{
					throw new Bad("the JSON chunk is not UTF-8 text");
				}
				long num6;
				long num7;
				for (num6 = 20 + num3; num6 + 8 <= _d.Length; num6 += 8 + num7)
				{
					num7 = U32(num6);
					uint num8 = U32(num6 + 4);
					if (num6 + 8 + num7 > _d.Length)
					{
						throw new Bad("a chunk runs past the end of the file");
					}
					if (num8 == 5130562 && _binStart < 0)
					{
						_binStart = num6 + 8;
						_binLength = num7;
					}
				}
				if (num6 != _d.Length)
				{
					throw new Bad("stray bytes after the last chunk");
				}
				return result;
			}

			private Acc Accessor(int index, string what)
			{
				if (index < 0 || index >= _accessors.Count)
				{
					throw new Bad(what + ": accessor " + index + " does not exist");
				}
				if (!(_accessors[index] is Dictionary<string, object> dictionary))
				{
					throw new Bad(what + ": accessor " + index + " is not an object");
				}
				if (dictionary.ContainsKey("sparse"))
				{
					throw new Bad(what + ": sparse accessors are not read");
				}
				if (!dictionary.ContainsKey("bufferView"))
				{
					throw new Bad(what + ": accessor " + index + " has no buffer view");
				}
				int num = Int(dictionary, "bufferView", -1);
				if (num < 0 || num >= _views.Count)
				{
					throw new Bad(what + ": buffer view " + num + " does not exist");
				}
				if (!(_views[num] is Dictionary<string, object> dictionary2))
				{
					throw new Bad(what + ": buffer view " + num + " is not an object");
				}
				ViewRange(dictionary2, num, what, out var offset, out var length);
				long num2 = (dictionary2.ContainsKey("byteStride") ? Long(dictionary2, "byteStride", 0L) : 0);
				if (num2 != 0L && (num2 < 1 || num2 > 252))
				{
					throw new Bad(what + ": buffer view " + num + " has a bad stride (" + num2 + ")");
				}
				string text = Str(dictionary, "type");
				int num3;
				switch (text)
				{
				default:
					num3 = 0;
					break;
				case "VEC4":
					num3 = 4;
					break;
				case "VEC3":
					num3 = 3;
					break;
				case "VEC2":
					num3 = 2;
					break;
				case "SCALAR":
					num3 = 1;
					break;
				}
				int num4 = num3;
				if (num4 == 0)
				{
					throw new Bad(what + ": accessor " + index + " has an unreadable type (" + (text ?? "none") + ")");
				}
				int num5 = Int(dictionary, "componentType", -1);
				int num6;
				switch (num5)
				{
				default:
					num6 = 0;
					break;
				case 5125:
				case 5126:
					num6 = 4;
					break;
				case 5122:
				case 5123:
					num6 = 2;
					break;
				case 5120:
				case 5121:
					num6 = 1;
					break;
				}
				int num7 = num6;
				if (num7 == 0)
				{
					throw new Bad(what + ": accessor " + index + " has an unknown component type (" + num5 + ")");
				}
				long num8 = Long(dictionary, "count", -1L);
				if (num8 < 1)
				{
					throw new Bad(what + ": accessor " + index + " has no elements");
				}
				if (num8 > 268435455)
				{
					throw new Bad(what + ": accessor " + index + " claims " + num8 + " elements");
				}
				long num9 = num4 * num7;
				long num10 = ((num2 == 0L) ? num9 : num2);
				if (num10 < num9)
				{
					throw new Bad(what + ": buffer view " + num + "'s stride is smaller than an element");
				}
				long num11 = Long(dictionary, "byteOffset", 0L);
				if (num11 < 0 || num11 + (num8 - 1) * num10 + num9 > length)
				{
					throw new Bad(what + ": accessor " + index + " runs past its buffer view");
				}
				return new Acc
				{
					Count = (int)num8,
					Comps = num4,
					CompType = num5,
					CompSize = num7,
					Stride = (int)num10,
					Start = _binStart + offset + num11,
					Normalized = Bool(dictionary, "normalized")
				};
			}

			private void ViewRange(Dictionary<string, object> view, int vi, string what, out long offset, out long length)
			{
				int num = Int(view, "buffer", -1);
				if (num < 0 || num >= _buffers.Count)
				{
					throw new Bad(what + ": buffer " + num + " does not exist");
				}
				if (num != 0)
				{
					throw new Bad(what + ": data in buffer " + num + "; only the .glb's own buffer (0) is read");
				}
				if (_binStart < 0)
				{
					throw new Bad(what + ": the file has no binary chunk");
				}
				long num2 = Long((_buffers[num] as Dictionary<string, object>) ?? throw new Bad(what + ": buffer " + num + " is not an object"), "byteLength", -1L);
				if (num2 < 0 || num2 > _binLength)
				{
					throw new Bad(what + ": the buffer is longer than the file's binary chunk");
				}
				offset = Long(view, "byteOffset", 0L);
				length = Long(view, "byteLength", -1L);
				if (offset < 0 || length < 0 || offset + length > num2)
				{
					throw new Bad(what + ": buffer view " + vi + " lies outside the data");
				}
			}

			private float Component(long at, int ct, bool normalized)
			{
				switch (ct)
				{
				case 5126:
					if (BitConverter.IsLittleEndian)
					{
						return BitConverter.ToSingle(_d, (int)at);
					}
					return BitConverter.ToSingle(new byte[4]
					{
						_d[at + 3],
						_d[at + 2],
						_d[at + 1],
						_d[at]
					}, 0);
				case 5121:
					if (!normalized)
					{
						return (int)_d[at];
					}
					return (float)(int)_d[at] / 255f;
				case 5123:
				{
					int num2 = _d[at] | (_d[at + 1] << 8);
					if (!normalized)
					{
						return num2;
					}
					return (float)num2 / 65535f;
				}
				case 5125:
					return U32(at);
				case 5120:
				{
					sbyte b = (sbyte)_d[at];
					if (!normalized)
					{
						return b;
					}
					return Math.Max((float)b / 127f, -1f);
				}
				default:
				{
					short num = (short)(_d[at] | (_d[at + 1] << 8));
					if (!normalized)
					{
						return num;
					}
					return Math.Max((float)num / 32767f, -1f);
				}
				}
			}

			private float[] Floats(int index, string what, int minComps, int maxComps, bool allowNormalized, out int comps)
			{
				long key = ((long)index << 16) | ((long)minComps << 8) | ((long)maxComps << 1) | (allowNormalized ? 1 : 0);
				if (_floats.TryGetValue(key, out (float[], int) value))
				{
					comps = value.Item2;
					return value.Item1;
				}
				float[] array = ReadFloats(index, what, minComps, maxComps, allowNormalized, out comps);
				_floats[key] = (array, comps);
				return array;
			}

			private float[] ReadFloats(int index, string what, int minComps, int maxComps, bool allowNormalized, out int comps)
			{
				Acc acc = Accessor(index, what);
				if (acc.Comps < minComps || acc.Comps > maxComps)
				{
					throw new Bad(what + ": wrong element type");
				}
				if (acc.CompType != 5126 && (!allowNormalized || !acc.Normalized || (acc.CompType != 5121 && acc.CompType != 5123)))
				{
					throw new Bad(what + ": unsupported component type (" + acc.CompType + (acc.Normalized ? ", normalized" : "") + ")");
				}
				if (acc.Count > _lim.MaxVertices)
				{
					throw new Bad(what + ": " + acc.Count + " vertices, at most " + _lim.MaxVertices);
				}
				comps = acc.Comps;
				float[] array = new float[acc.Count * comps];
				int i = 0;
				int num = 0;
				for (; i < acc.Count; i++)
				{
					long num2 = acc.Start + (long)i * (long)acc.Stride;
					int num3 = 0;
					while (num3 < comps)
					{
						float num4 = Component(num2 + num3 * acc.CompSize, acc.CompType, acc.Normalized);
						if (float.IsNaN(num4) || float.IsInfinity(num4))
						{
							throw new Bad(what + ": not a number in the data");
						}
						array[num] = num4;
						num3++;
						num++;
					}
				}
				return array;
			}

			private int[] Indices(int index, string what, int vertexCount)
			{
				Acc acc = Accessor(index, what);
				if (acc.Comps != 1 || acc.Normalized || (acc.CompType != 5121 && acc.CompType != 5123 && acc.CompType != 5125))
				{
					throw new Bad(what + ": indices must be unsigned bytes, shorts or ints");
				}
				if (acc.Count / 3 > _lim.MaxTriangles)
				{
					throw new Bad(what + ": " + acc.Count / 3 + " triangles, at most " + _lim.MaxTriangles);
				}
				int[] array = new int[acc.Count];
				for (int i = 0; i < acc.Count; i++)
				{
					long num = acc.Start + (long)i * (long)acc.Stride;
					long num2 = ((acc.CompType == 5121) ? _d[num] : ((acc.CompType == 5123) ? ((long)(_d[num] | (_d[num + 1] << 8))) : ((long)U32(num))));
					if (num2 >= vertexCount)
					{
						throw new Bad(what + ": index " + num2 + " is out of range (" + vertexCount + " vertices)");
					}
					array[i] = (int)num2;
				}
				return array;
			}

			private void ReadImages()
			{
				for (int i = 0; i < _images.Count; i++)
				{
					Dictionary<string, object> dictionary = _images[i] as Dictionary<string, object>;
					GlbImage glbImage = new GlbImage();
					Extras.Images.Add(glbImage);
					if (dictionary == null)
					{
						Note("image " + i + " is not an object (skipped)");
						continue;
					}
					glbImage.Name = Str(dictionary, "name");
					glbImage.MimeType = Str(dictionary, "mimeType");
					if (dictionary.ContainsKey("bufferView"))
					{
						int num = Int(dictionary, "bufferView", -1);
						try
						{
							if (num < 0 || num >= _views.Count)
							{
								throw new Bad("buffer view " + num + " does not exist");
							}
							if (!(_views[num] is Dictionary<string, object> view))
							{
								throw new Bad("buffer view " + num + " is not an object");
							}
							ViewRange(view, num, "image " + i, out var offset, out var length);
							glbImage.BufferView = num;
							glbImage.Offset = (int)(_binStart + offset);
							glbImage.Length = (int)length;
						}
						catch (Bad bad)
						{
							Note("image " + i + " skipped: " + bad.Message);
						}
					}
					else if (dictionary.ContainsKey("uri"))
					{
						string text = Str(dictionary, "uri") ?? "";
						string text2 = SafeUri(text);
						if (text2 != null)
						{
							glbImage.Uri = text2;
							continue;
						}
						if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
						{
							Note("image " + i + ": data: links are not read (pack the image into the .glb)");
							continue;
						}
						Note("image " + i + ": the link '" + Clip(text) + "' points outside the pack (only relative paths inside it)");
					}
					else
					{
						Note("image " + i + " has no data");
					}
				}
			}

			private static string Clip(string s)
			{
				if (s.Length <= 60)
				{
					return s;
				}
				return s.Substring(0, 60) + "...";
			}

			private static string SafeUri(string uri)
			{
				if (uri.Length == 0 || uri.Length > 260)
				{
					return null;
				}
				StringBuilder stringBuilder = new StringBuilder();
				for (int i = 0; i < uri.Length; i++)
				{
					char c = uri[i];
					if (c == '%')
					{
						if (i + 2 >= uri.Length)
						{
							return null;
						}
						int num = Hex(uri[i + 1]);
						int num2 = Hex(uri[i + 2]);
						if (num < 0 || num2 < 0)
						{
							return null;
						}
						c = (char)(num * 16 + num2);
						i += 2;
					}
					if (c < ' ' || c == ':' || c == '*' || c == '?' || c == '"' || c == '<' || c == '>' || c == '|' || c == '\u007f')
					{
						return null;
					}
					stringBuilder.Append((c == '\\') ? '/' : c);
				}
				string text = stringBuilder.ToString();
				if (text.StartsWith("/", StringComparison.Ordinal))
				{
					return null;
				}
				string[] array = text.Split('/');
				foreach (string text2 in array)
				{
					if (text2 == ".." || text2.Length == 0)
					{
						return null;
					}
				}
				return text;
			}

			private static int Hex(char c)
			{
				if (c < '0' || c > '9')
				{
					if (c < 'a' || c > 'f')
					{
						if (c < 'A' || c > 'F')
						{
							return -1;
						}
						return c - 65 + 10;
					}
					return c - 97 + 10;
				}
				return c - 48;
			}

			private Dictionary<string, object> NodeAt(int i)
			{
				if (i < 0 || i >= _nodes.Count)
				{
					throw new Bad("node " + i + " does not exist");
				}
				return (_nodes[i] as Dictionary<string, object>) ?? throw new Bad("node " + i + " is not an object");
			}

			private void Walk(int ni, double[] parentWorld, int parentPart, int depth)
			{
				Dictionary<string, object> dictionary = NodeAt(ni);
				if (_visited[ni])
				{
					throw new Bad("node " + ni + " is reached twice (its tree loops or shares a child)");
				}
				_visited[ni] = true;
				if (depth > 256)
				{
					throw new Bad("the node tree is too deep");
				}
				double[] array = Mul(parentWorld, Local(dictionary, ni));
				int parentPart2 = parentPart;
				if (dictionary.ContainsKey("skin"))
				{
					Note("skins are not read: the mesh is taken in its rest pose");
				}
				if (dictionary.ContainsKey("mesh"))
				{
					parentPart2 = AddPart(dictionary, ni, array, parentPart);
				}
				foreach (object item in Arr(dictionary, "children"))
				{
					Walk(ToInt(item, "a node's child"), array, parentPart2, depth + 1);
				}
			}

			private double[] Local(Dictionary<string, object> node, int ni)
			{
				if (node.ContainsKey("matrix"))
				{
					double[] array = Numbers(node, "matrix", 16, "node " + ni + "'s matrix");
					if (Math.Abs(array[3]) > 1E-06 || Math.Abs(array[7]) > 1E-06 || Math.Abs(array[11]) > 1E-06 || Math.Abs(array[15] - 1.0) > 1E-06)
					{
						Note("node " + ni + "'s matrix is not a plain transform (its projection row is ignored)");
						array[3] = (array[7] = (array[11] = 0.0));
						array[15] = 1.0;
					}
					return array;
				}
				double[] array2 = (node.ContainsKey("translation") ? Numbers(node, "translation", 3, "node " + ni + "'s translation") : new double[3]);
				double[] array3 = (node.ContainsKey("rotation") ? Numbers(node, "rotation", 4, "node " + ni + "'s rotation") : new double[4] { 0.0, 0.0, 0.0, 1.0 });
				double[] array4 = (node.ContainsKey("scale") ? Numbers(node, "scale", 3, "node " + ni + "'s scale") : new double[3] { 1.0, 1.0, 1.0 });
				double num = Math.Sqrt(array3[0] * array3[0] + array3[1] * array3[1] + array3[2] * array3[2] + array3[3] * array3[3]);
				if (num < 1E-09)
				{
					Note("node " + ni + " has a zero rotation (taken as none)");
					array3 = new double[4] { 0.0, 0.0, 0.0, 1.0 };
					num = 1.0;
				}
				double num2 = array3[0] / num;
				double num3 = array3[1] / num;
				double num4 = array3[2] / num;
				double num5 = array3[3] / num;
				return new double[16]
				{
					(1.0 - 2.0 * (num3 * num3 + num4 * num4)) * array4[0],
					2.0 * (num2 * num3 + num4 * num5) * array4[0],
					2.0 * (num2 * num4 - num3 * num5) * array4[0],
					0.0,
					2.0 * (num2 * num3 - num4 * num5) * array4[1],
					(1.0 - 2.0 * (num2 * num2 + num4 * num4)) * array4[1],
					2.0 * (num3 * num4 + num2 * num5) * array4[1],
					0.0,
					2.0 * (num2 * num4 + num3 * num5) * array4[2],
					2.0 * (num3 * num4 - num2 * num5) * array4[2],
					(1.0 - 2.0 * (num2 * num2 + num3 * num3)) * array4[2],
					0.0,
					array2[0],
					array2[1],
					array2[2],
					1.0
				};
			}

			private static double[] Identity()
			{
				return new double[16]
				{
					1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0,
					1.0, 0.0, 0.0, 0.0, 0.0, 1.0
				};
			}

			private static double[] Mul(double[] a, double[] b)
			{
				double[] array = new double[16];
				for (int i = 0; i < 4; i++)
				{
					for (int j = 0; j < 4; j++)
					{
						array[i * 4 + j] = a[j] * b[i * 4] + a[4 + j] * b[i * 4 + 1] + a[8 + j] * b[i * 4 + 2] + a[12 + j] * b[i * 4 + 3];
					}
				}
				return array;
			}

			private string PartName(string raw, int ni)
			{
				string text = StripNumber(raw ?? "");
				StringBuilder stringBuilder = new StringBuilder();
				string text2 = text;
				foreach (char c in text2)
				{
					stringBuilder.Append(((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '.' || c == '-') ? c : '_');
				}
				string text3 = ((stringBuilder.Length > 0) ? stringBuilder.ToString() : ("part" + ni));
				if (text3.Length > 64)
				{
					text3 = text3.Substring(0, 64);
				}
				string text4 = text3;
				int num = 2;
				while (!_partNames.Add(text4))
				{
					text4 = text3 + "_" + num;
					num++;
				}
				if (text4 != text3)
				{
					Note("two parts named '" + text3 + "': the second is '" + text4 + "'");
				}
				return text4;
			}

			private int AddPart(Dictionary<string, object> node, int ni, double[] world, int parentPart)
			{
				if (_parts.Count >= _lim.MaxParts)
				{
					throw new Bad("more than " + _lim.MaxParts + " parts");
				}
				int num = Int(node, "mesh", -1);
				if (num < 0 || num >= _meshes.Count)
				{
					throw new Bad("node " + ni + "'s mesh " + num + " does not exist");
				}
				if (!(_meshes[num] is Dictionary<string, object> o))
				{
					throw new Bad("mesh " + num + " is not an object");
				}
				for (int i = 0; i < 16; i++)
				{
					if (double.IsNaN(world[i]) || double.IsInfinity(world[i]))
					{
						throw new Bad("node " + ni + "'s transform is not a number");
					}
				}
				PartBuild partBuild = new PartBuild
				{
					Name = PartName(Str(node, "name") ?? Str(o, "name"), ni),
					Parent = parentPart,
					Node = ni
				};
				partBuild.PX = world[12];
				partBuild.PY = 0.0 - world[14];
				partBuild.PZ = world[13];
				Dictionary<string, object> dictionary = Obj(node, "extras");
				if (dictionary != null && dictionary.TryGetValue("order", out var value) && value is double)
				{
					partBuild.Order = (double)value;
				}
				double num2 = world[0];
				double num3 = world[4];
				double num4 = world[8];
				double num5 = world[1];
				double num6 = world[5];
				double num7 = world[9];
				double num8 = world[2];
				double num9 = world[6];
				double num10 = world[10];
				double num11 = num6 * num10 - num7 * num9;
				double num12 = 0.0 - (num5 * num10 - num7 * num8);
				double num13 = num5 * num9 - num6 * num8;
				double num14 = 0.0 - (num3 * num10 - num4 * num9);
				double num15 = num2 * num10 - num4 * num8;
				double num16 = 0.0 - (num2 * num9 - num3 * num8);
				double num17 = num3 * num7 - num4 * num6;
				double num18 = 0.0 - (num2 * num7 - num4 * num5);
				double num19 = num2 * num6 - num3 * num5;
				double num20 = num2 * num11 + num3 * num12 + num4 * num13;
				bool flip = num20 < 0.0;
				bool flag = Math.Abs(num20) < 1E-18;
				if (flag)
				{
					Note("node " + ni + " is scaled flat (its normals are worked out from its faces)");
				}
				double[] nm = new double[9] { num11, num12, num13, num14, num15, num16, num17, num18, num19 };
				foreach (object item in Arr(o, "primitives"))
				{
					if (!(item is Dictionary<string, object> dictionary2))
					{
						continue;
					}
					int num21 = Int(dictionary2, "mode", 4);
					if (num21 != 4)
					{
						Note("mesh '" + (Str(o, "name") ?? num.ToString()) + "': a primitive of " + ModeName(num21) + " skipped (only triangles are read)");
						continue;
					}
					Dictionary<string, object> dictionary3 = Obj(dictionary2, "attributes");
					if (dictionary3 == null || !dictionary3.ContainsKey("POSITION"))
					{
						Note("a primitive without positions skipped");
						continue;
					}
					if (dictionary2.ContainsKey("targets"))
					{
						Note("morph targets are not read");
					}
					AddPrimitive(partBuild, dictionary2, dictionary3, world, nm, flip, flag);
				}
				_parts.Add(partBuild);
				return _parts.Count - 1;
			}

			private static string ModeName(int mode)
			{
				switch (mode)
				{
				default:
					return "mode " + mode;
				case 6:
					return "triangle fans";
				case 5:
					return "triangle strips";
				case 3:
					return "line strips";
				case 2:
					return "line loops";
				case 1:
					return "lines";
				case 0:
					return "points";
				}
			}

			private void AddPrimitive(PartBuild p, Dictionary<string, object> prim, Dictionary<string, object> attrs, double[] m, double[] nm, bool flip, bool flat)
			{
				int comps;
				float[] array = Floats(Int(attrs, "POSITION", -1), "positions", 3, 3, allowNormalized: false, out comps);
				int num = array.Length / 3;
				_turned += num;
				if (_turned > 16L * (long)_lim.MaxVertices)
				{
					throw new Bad("more than " + 16L * (long)_lim.MaxVertices + " vertices to read");
				}
				float[] array2 = null;
				float[] array3 = null;
				float[] array4 = null;
				int comps2 = 3;
				if (attrs.ContainsKey("NORMAL") && !flat)
				{
					array2 = Floats(Int(attrs, "NORMAL", -1), "normals", 3, 3, allowNormalized: false, out comps);
					if (array2.Length != array.Length)
					{
						throw new Bad("normals and positions differ in count");
					}
				}
				if (attrs.ContainsKey("TEXCOORD_0"))
				{
					array3 = Floats(Int(attrs, "TEXCOORD_0", -1), "texture coordinates", 2, 2, allowNormalized: true, out comps);
					if (array3.Length != num * 2)
					{
						throw new Bad("texture coordinates and positions differ in count");
					}
				}
				if (attrs.ContainsKey("COLOR_0"))
				{
					array4 = Floats(Int(attrs, "COLOR_0", -1), "vertex colours", 3, 4, allowNormalized: true, out comps2);
					if (array4.Length != num * comps2)
					{
						throw new Bad("vertex colours and positions differ in count");
					}
				}
				int[] array5;
				if (prim.ContainsKey("indices"))
				{
					array5 = Indices(Int(prim, "indices", -1), "indices", num);
				}
				else
				{
					array5 = new int[num];
					for (int i = 0; i < num; i++)
					{
						array5[i] = i;
					}
				}
				if (array5.Length % 3 != 0)
				{
					Note("a primitive's index count is not a multiple of 3 (the rest dropped)");
				}
				int num2 = array5.Length / 3;
				if (_tris + num2 > _lim.MaxTriangles)
				{
					throw new Bad("more than " + _lim.MaxTriangles + " triangles");
				}
				float[] array6 = new float[num * 3];
				float[] array7 = ((array2 != null) ? new float[num * 3] : null);
				for (int j = 0; j < num; j++)
				{
					double num3 = array[j * 3];
					double num4 = array[j * 3 + 1];
					double num5 = array[j * 3 + 2];
					double num6 = m[0] * num3 + m[4] * num4 + m[8] * num5 + m[12];
					double num7 = m[1] * num3 + m[5] * num4 + m[9] * num5 + m[13];
					double num8 = m[2] * num3 + m[6] * num4 + m[10] * num5 + m[14];
					array6[j * 3] = Finite((float)num6);
					array6[j * 3 + 1] = Finite((float)(0.0 - num8));
					array6[j * 3 + 2] = Finite((float)num7);
					if (array7 != null)
					{
						double num9 = array2[j * 3];
						double num10 = array2[j * 3 + 1];
						double num11 = array2[j * 3 + 2];
						double num12 = nm[0] * num9 + nm[1] * num10 + nm[2] * num11;
						double num13 = nm[3] * num9 + nm[4] * num10 + nm[5] * num11;
						double num14 = nm[6] * num9 + nm[7] * num10 + nm[8] * num11;
						if (flip)
						{
							num12 = 0.0 - num12;
							num13 = 0.0 - num13;
							num14 = 0.0 - num14;
						}
						double num15 = Math.Sqrt(num12 * num12 + num13 * num13 + num14 * num14);
						if (num15 > 1E-12)
						{
							num12 /= num15;
							num13 /= num15;
							num14 /= num15;
						}
						else
						{
							num12 = 0.0;
							num13 = 1.0;
							num14 = 0.0;
						}
						array7[j * 3] = (float)num12;
						array7[j * 3 + 1] = (float)(0.0 - num14);
						array7[j * 3 + 2] = (float)num13;
					}
				}
				int num16 = SlotOfMaterial(prim.ContainsKey("material") ? Int(prim, "material", -1) : (-1));
				int num17 = p.Pos.Count / 3;
				if (array7 != null)
				{
					if (_verts + num > _lim.MaxVertices)
					{
						throw new Bad("more than " + _lim.MaxVertices + " vertices");
					}
					_verts += num;
					for (int k = 0; k < num * 3; k++)
					{
						p.Pos.Add(array6[k]);
						p.Nrm.Add(array7[k]);
					}
					for (int l = 0; l < num; l++)
					{
						p.Uv.Add((array3 != null) ? array3[l * 2] : 0f);
						p.Uv.Add((array3 != null) ? array3[l * 2 + 1] : 0f);
					}
				}
				if (array3 != null)
				{
					p.AnyUv = true;
				}
				for (int n = 0; n < num2; n++)
				{
					int num18 = array5[n * 3];
					int num19 = array5[n * 3 + 1];
					int num20 = array5[n * 3 + 2];
					if (flip)
					{
						int num21 = num19;
						num19 = num20;
						num20 = num21;
					}
					int num22 = ((array4 != null) ? ColourSlot(num16, array4, comps2, num18, num19, num20) : num16);
					bool foliage = _slots[num22].Foliage;
					if (array7 != null && !foliage)
					{
						p.Tri.Add(num17 + num18);
						p.Tri.Add(num17 + num19);
						p.Tri.Add(num17 + num20);
					}
					else
					{
						float num23 = array6[num19 * 3] - array6[num18 * 3];
						float num24 = array6[num19 * 3 + 1] - array6[num18 * 3 + 1];
						float num25 = array6[num19 * 3 + 2] - array6[num18 * 3 + 2];
						float num26 = array6[num20 * 3] - array6[num18 * 3];
						float num27 = array6[num20 * 3 + 1] - array6[num18 * 3 + 1];
						float num28 = array6[num20 * 3 + 2] - array6[num18 * 3 + 2];
						double num29 = num24 * num28 - num25 * num27;
						double num30 = num25 * num26 - num23 * num28;
						double num31 = num23 * num27 - num24 * num26;
						double num32 = Math.Sqrt(num29 * num29 + num30 * num30 + num31 * num31);
						if (num32 > 1E-20)
						{
							num29 /= num32;
							num30 /= num32;
							num31 /= num32;
						}
						else
						{
							num29 = 0.0;
							num30 = 0.0;
							num31 = 1.0;
						}
						if (foliage)
						{
							if (num31 < 0.0)
							{
								num29 = 0.0 - num29;
								num30 = 0.0 - num30;
								num31 = 0.0 - num31;
							}
							num29 *= 0.35;
							num30 *= 0.35;
							num31 = num31 * 0.35 + 1.0;
							num32 = Math.Sqrt(num29 * num29 + num30 * num30 + num31 * num31);
							num29 /= num32;
							num30 /= num32;
							num31 /= num32;
						}
						if (_verts + 3 > _lim.MaxVertices)
						{
							throw new Bad("more than " + _lim.MaxVertices + " vertices");
						}
						_verts += 3;
						int num33 = p.Pos.Count / 3;
						int[] array8 = new int[3] { num18, num19, num20 };
						foreach (int num35 in array8)
						{
							p.Pos.Add(array6[num35 * 3]);
							p.Pos.Add(array6[num35 * 3 + 1]);
							p.Pos.Add(array6[num35 * 3 + 2]);
							p.Nrm.Add((float)num29);
							p.Nrm.Add((float)num30);
							p.Nrm.Add((float)num31);
							p.Uv.Add((array3 != null) ? array3[num35 * 2] : 0f);
							p.Uv.Add((array3 != null) ? array3[num35 * 2 + 1] : 0f);
						}
						p.Tri.Add(num33);
						p.Tri.Add(num33 + 1);
						p.Tri.Add(num33 + 2);
					}
					p.Slot.Add((byte)num22);
					_tris++;
				}
			}

			private static float Finite(float v)
			{
				if (float.IsNaN(v) || float.IsInfinity(v))
				{
					throw new Bad("a position is out of range after its node's transform");
				}
				return v;
			}

			private int AddSlot(SlotDef s)
			{
				if (_slots.Count >= Math.Min(_lim.MaxSlots, 256))
				{
					throw new Bad("more than " + Math.Min(_lim.MaxSlots, 256) + " slots (materials)");
				}
				_slots.Add(s);
				_slotByName[s.Name] = _slots.Count - 1;
				return _slots.Count - 1;
			}

			private int SlotOfMaterial(int mi)
			{
				if (_slotOfMaterial.TryGetValue(mi, out var value))
				{
					return value;
				}
				SlotDef slotDef;
				if (mi < 0)
				{
					slotDef = new SlotDef
					{
						Name = "plain",
						R = 200,
						G = 200,
						B = 200
					};
				}
				else
				{
					if (mi >= _materials.Count)
					{
						throw new Bad("material " + mi + " does not exist");
					}
					Dictionary<string, object> mat = (_materials[mi] as Dictionary<string, object>) ?? new Dictionary<string, object>();
					slotDef = FromMaterial(mat, mi);
				}
				if (!_slotByName.TryGetValue(slotDef.Name, out var value2))
				{
					value2 = (slotDef.Base = AddSlot(slotDef));
				}
				else if (_slots[value2].Image < 0)
				{
					_slots[value2].Image = slotDef.Image;
				}
				_slotOfMaterial[mi] = value2;
				return value2;
			}

			private void PresetSlots(string list)
			{
				string[] array = list.Split(';');
				foreach (string text in array)
				{
					if (text.Trim().Length != 0)
					{
						int num = text.LastIndexOf('#');
						SlotDef slotDef = Label((num >= 0) ? text.Substring(0, num) : text);
						slotDef.R = (slotDef.G = (slotDef.B = 255));
						string text2 = ((num >= 0) ? text.Substring(num + 1).Trim() : "");
						if (text2.Length == 6 && text2.ToLowerInvariant().Trim("0123456789abcdef".ToCharArray()).Length == 0)
						{
							int num2 = Convert.ToInt32(text2, 16);
							slotDef.R = (num2 >> 16) & 0xFF;
							slotDef.G = (num2 >> 8) & 0xFF;
							slotDef.B = num2 & 0xFF;
						}
						else
						{
							Note("the slot list's '" + Clip(text) + "' has no colour");
						}
						if (!_slotByName.ContainsKey(slotDef.Name))
						{
							slotDef.Base = AddSlot(slotDef);
						}
					}
				}
			}

			private SlotDef FromMaterial(Dictionary<string, object> mat, int mi)
			{
				SlotDef slotDef = Label(Str(mat, "name"));
				double c = 1.0;
				double c2 = 1.0;
				double c3 = 1.0;
				Dictionary<string, object> dictionary = Obj(mat, "pbrMetallicRoughness");
				if (dictionary != null)
				{
					if (dictionary.ContainsKey("baseColorFactor"))
					{
						double[] array = Numbers(dictionary, "baseColorFactor", 4, "material " + mi + "'s colour");
						c = array[0];
						c2 = array[1];
						c3 = array[2];
					}
					Dictionary<string, object> dictionary2 = Obj(dictionary, "baseColorTexture");
					if (dictionary2 != null)
					{
						int num = Int(dictionary2, "index", -1);
						Dictionary<string, object> dictionary3 = ((num >= 0 && num < _textures.Count) ? (_textures[num] as Dictionary<string, object>) : null);
						int num2 = ((dictionary3 != null) ? Int(dictionary3, "source", -1) : (-1));
						if (num2 >= 0 && num2 < _images.Count)
						{
							slotDef.Image = num2;
						}
						else
						{
							Note("material " + mi + "'s texture has no image");
						}
					}
				}
				slotDef.R = Srgb(c);
				slotDef.G = Srgb(c2);
				slotDef.B = Srgb(c3);
				return slotDef;
			}

			private SlotDef Label(string raw)
			{
				SlotDef slotDef = new SlotDef();
				string[] array = StripNumber(raw ?? "").Split('|');
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = array[i].Trim().ToLowerInvariant();
				}
				slotDef.Name = ((raw == null) ? "plain" : ((array[0].Length > 0) ? array[0] : "plain"));
				if (slotDef.Name.Length > 64)
				{
					slotDef.Name = slotDef.Name.Substring(0, 64);
				}
				int val = Array.IndexOf(Kinds, slotDef.Name);
				slotDef.Kind = (byte)Math.Max(0, val);
				for (int j = 1; j < array.Length; j++)
				{
					string text = array[j];
					int num = Array.IndexOf(Kinds, text);
					int num2 = Array.IndexOf(FlagNames, text);
					if (num >= 0)
					{
						slotDef.Kind = (byte)num;
					}
					else if (num2 >= 0)
					{
						slotDef.Flags |= (byte)(1 << num2);
					}
					else if (text == "foliage")
					{
						slotDef.Foliage = true;
					}
					else if (text.Length > 0)
					{
						Note("material '" + raw + "': '" + text + "' is not a kind or flag");
					}
				}
				return slotDef;
			}

			private int ColourSlot(int baseSlot, float[] col, int cc, int a, int b, int c)
			{
				SlotDef slotDef = _slots[baseSlot];
				double num = (double)(col[a * cc] + col[b * cc] + col[c * cc]) / 3.0;
				double num2 = (double)(col[a * cc + 1] + col[b * cc + 1] + col[c * cc + 1]) / 3.0;
				double num3 = (double)(col[a * cc + 2] + col[b * cc + 2] + col[c * cc + 2]) / 3.0;
				int num4 = Q(Srgb(Linear(slotDef.R) * num));
				int num5 = Q(Srgb(Linear(slotDef.G) * num2));
				int num6 = Q(Srgb(Linear(slotDef.B) * num3));
				if (num4 == Q(slotDef.R) && num5 == Q(slotDef.G) && num6 == Q(slotDef.B))
				{
					return baseSlot;
				}
				string text = slotDef.Name + "~" + num4.ToString("x2") + num5.ToString("x2") + num6.ToString("x2");
				if (_slotByName.TryGetValue(text, out var value))
				{
					return value;
				}
				if (_slots.Count < Math.Min(_lim.MaxSlots, 256))
				{
					return AddSlot(new SlotDef
					{
						Name = text,
						Kind = slotDef.Kind,
						Flags = slotDef.Flags,
						Foliage = slotDef.Foliage,
						Image = slotDef.Image,
						R = num4,
						G = num5,
						B = num6,
						Base = baseSlot
					});
				}
				Note("too many vertex colours: some take the nearest colour slot");
				int result = baseSlot;
				long num7 = long.MaxValue;
				for (int i = 0; i < _slots.Count; i++)
				{
					if (_slots[i].Base == baseSlot)
					{
						long num8 = _slots[i].R - num4;
						long num9 = _slots[i].G - num5;
						long num10 = _slots[i].B - num6;
						long num11 = num8 * num8 + num9 * num9 + num10 * num10;
						if (num11 < num7)
						{
							num7 = num11;
							result = i;
						}
					}
				}
				return result;
			}

			private static int Q(int v)
			{
				int num = (v * 31 + 127) / 255;
				return (num << 3) | (num >> 2);
			}

			private static int Srgb(double c)
			{
				c = Math.Max(0.0, Math.Min(1.0, c));
				return (int)Math.Round(((c <= 0.0031308) ? (12.92 * c) : (1.055 * Math.Pow(c, 5.0 / 12.0) - 0.055)) * 255.0);
			}

			private static double Linear(int v)
			{
				double num = (double)v / 255.0;
				if (!(num <= 0.04045))
				{
					return Math.Pow((num + 0.055) / 1.055, 2.4);
				}
				return num / 12.92;
			}

			private void Reorder()
			{
				HashSet<double> hashSet = new HashSet<double>();
				foreach (PartBuild part in _parts)
				{
					if (double.IsNaN(part.Order) || !hashSet.Add(part.Order))
					{
						return;
					}
				}
				int[] array = new int[_parts.Count];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = i;
				}
				Array.Sort(array, (int x, int y) => _parts[x].Order.CompareTo(_parts[y].Order));
				int[] array2 = new int[_parts.Count];
				for (int num = 0; num < array.Length; num++)
				{
					array2[array[num]] = num;
				}
				for (int num2 = 0; num2 < _parts.Count; num2++)
				{
					if (_parts[num2].Parent >= 0 && array2[_parts[num2].Parent] > array2[num2])
					{
						return;
					}
				}
				List<PartBuild> list = new List<PartBuild>();
				int[] array3 = array;
				foreach (int index in array3)
				{
					PartBuild partBuild = _parts[index];
					if (partBuild.Parent >= 0)
					{
						partBuild.Parent = array2[partBuild.Parent];
					}
					list.Add(partBuild);
				}
				_parts.Clear();
				_parts.AddRange(list);
			}

			private MeshModel Build(string info)
			{
				MeshModel meshModel = new MeshModel
				{
					Name = _name
				};
				int count = _slots.Count;
				meshModel.SlotNames = new string[count];
				meshModel.SlotKind = new byte[count];
				meshModel.SlotFlags = new byte[count];
				meshModel.SlotColor = new uint[count];
				Extras.SlotImage = new int[count];
				for (int i = 0; i < count; i++)
				{
					SlotDef slotDef = _slots[i];
					meshModel.SlotNames[i] = slotDef.Name;
					meshModel.SlotKind[i] = slotDef.Kind;
					meshModel.SlotFlags[i] = slotDef.Flags;
					meshModel.SlotColor[i] = TerrainCache.Rgb(slotDef.R, slotDef.G, slotDef.B);
					Extras.SlotImage[i] = slotDef.Image;
				}
				if (!string.IsNullOrEmpty(info))
				{
					meshModel.ParseInfo(info);
				}
				AttachTextures(meshModel);
				meshModel.Parts = new MeshPart[_parts.Count];
				Extras.PartUv = new float[_parts.Count][];
				float num = float.MaxValue;
				float num2 = float.MaxValue;
				float num3 = float.MaxValue;
				float num4 = float.MinValue;
				float num5 = float.MinValue;
				float num6 = float.MinValue;
				for (int j = 0; j < _parts.Count; j++)
				{
					PartBuild partBuild = _parts[j];
					MeshPart meshPart = new MeshPart
					{
						Name = partBuild.Name,
						Parent = partBuild.Parent,
						Pivot = new Vec3((float)partBuild.PX, (float)partBuild.PY, (float)partBuild.PZ),
						Pos = partBuild.Pos.ToArray(),
						Nrm = partBuild.Nrm.ToArray(),
						Tri = partBuild.Tri.ToArray(),
						Slot = partBuild.Slot.ToArray()
					};
					meshPart.Axis = new byte[meshPart.TriangleCount];
					for (int k = 0; k < meshPart.Axis.Length; k++)
					{
						meshPart.Axis[k] = MeshLibrary.DominantAxis(meshPart, k);
					}
					for (int l = 0; l < meshPart.Pos.Length; l += 3)
					{
						float num7 = meshPart.Pos[l];
						float num8 = meshPart.Pos[l + 1];
						float num9 = meshPart.Pos[l + 2];
						if (num7 < num)
						{
							num = num7;
						}
						if (num8 < num2)
						{
							num2 = num8;
						}
						if (num9 < num3)
						{
							num3 = num9;
						}
						if (num7 > num4)
						{
							num4 = num7;
						}
						if (num8 > num5)
						{
							num5 = num8;
						}
						if (num9 > num6)
						{
							num6 = num9;
						}
					}
					if (partBuild.AnyUv)
					{
						Extras.PartUv[j] = (meshPart.Uv = partBuild.Uv.ToArray());
					}
					meshModel.Parts[j] = meshPart;
				}
				if (num > num4)
				{
					num = (num2 = (num3 = (num4 = (num5 = (num6 = 0f)))));
				}
				meshModel.Min = new Vec3(num, num2, num3);
				meshModel.Max = new Vec3(num4, num5, num6);
				return meshModel;
			}

			private void AttachTextures(MeshModel model)
			{
				int count = _slots.Count;
				bool flag = false;
				for (int i = 0; i < count; i++)
				{
					if (_slots[i].Image >= 0)
					{
						flag = true;
					}
				}
				if (!flag)
				{
					return;
				}
				List<MeshTexture> list = new List<MeshTexture>();
				Dictionary<int, int> dictionary = new Dictionary<int, int>();
				model.SlotTexture = new int[count];
				for (int j = 0; j < count; j++)
				{
					model.SlotTexture[j] = -1;
					int image = _slots[j].Image;
					if (image < 0 || image >= Extras.Images.Count)
					{
						continue;
					}
					GlbImage glbImage = Extras.Images[image];
					if (glbImage.Uri == null && glbImage.Length <= 0)
					{
						continue;
					}
					if (!dictionary.TryGetValue(image, out var value))
					{
						MeshTexture meshTexture = new MeshTexture
						{
							Name = (glbImage.Name ?? ("image " + image)),
							MimeType = glbImage.MimeType,
							Uri = glbImage.Uri
						};
						if (glbImage.Length > 0)
						{
							meshTexture.Encoded = new byte[glbImage.Length];
							Array.Copy(_d, glbImage.Offset, meshTexture.Encoded, 0, glbImage.Length);
						}
						value = list.Count;
						list.Add(meshTexture);
						dictionary[image] = value;
					}
					uint num = model.SlotColor[j];
					if ((num & 0xFFFFFF) != 16777215)
					{
						int num2 = -1;
						for (int k = 0; k < list.Count; k++)
						{
							if (list[k].Source == list[value] && list[k].Factor == num)
							{
								num2 = k;
							}
						}
						if (num2 < 0)
						{
							MeshTexture meshTexture2 = list[value];
							list.Add(new MeshTexture
							{
								Name = meshTexture2.Name,
								MimeType = meshTexture2.MimeType,
								Uri = meshTexture2.Uri,
								Encoded = meshTexture2.Encoded,
								Factor = num,
								Source = meshTexture2
							});
							num2 = list.Count - 1;
						}
						value = num2;
					}
					model.SlotTexture[j] = value;
				}
				model.Textures = list.ToArray();
			}

			private static string ExtraString(Dictionary<string, object> o, string key)
			{
				Dictionary<string, object> dictionary = ((o != null) ? Obj(o, "extras") : null);
				if (dictionary == null || !dictionary.TryGetValue(key, out var value) || !(value is string text) || text.Trim().Length <= 0)
				{
					return null;
				}
				return text.Trim();
			}

			private static Dictionary<string, object> Obj(Dictionary<string, object> o, string key)
			{
				if (!o.TryGetValue(key, out var value))
				{
					return null;
				}
				return value as Dictionary<string, object>;
			}

			private static List<object> Arr(Dictionary<string, object> o, string key)
			{
				if (!o.TryGetValue(key, out var value) || value == null)
				{
					return Empty;
				}
				return (value as List<object>) ?? throw new Bad("'" + key + "' is not a list");
			}

			private static string Str(Dictionary<string, object> o, string key)
			{
				if (!o.TryGetValue(key, out var value))
				{
					return null;
				}
				return value as string;
			}

			private static bool Bool(Dictionary<string, object> o, string key)
			{
				if (o.TryGetValue(key, out var value) && value is bool)
				{
					return (bool)value;
				}
				return false;
			}

			private static long Long(Dictionary<string, object> o, string key, long fallback)
			{
				if (!o.TryGetValue(key, out var value) || value == null)
				{
					return fallback;
				}
				if (!(value is double num))
				{
					throw new Bad("'" + key + "' is not a number");
				}
				if (num != Math.Floor(num) || num < -9000000000000000.0 || num > 9000000000000000.0)
				{
					throw new Bad("'" + key + "' is not a whole number");
				}
				return (long)num;
			}

			private static int Int(Dictionary<string, object> o, string key, int fallback)
			{
				long num = Long(o, key, fallback);
				if (num < int.MinValue || num > int.MaxValue)
				{
					throw new Bad("'" + key + "' is out of range");
				}
				return (int)num;
			}

			private static int ToInt(object v, string what)
			{
				if (!(v is double num))
				{
					throw new Bad(what + " is not a number");
				}
				if (num != Math.Floor(num) || num < 0.0 || num > 2147483647.0)
				{
					throw new Bad(what + " is not a valid index");
				}
				return (int)num;
			}

			private static double[] Numbers(Dictionary<string, object> o, string key, int count, string what)
			{
				object value;
				List<object> list = (o.TryGetValue(key, out value) ? (value as List<object>) : null);
				if (list == null || list.Count != count)
				{
					throw new Bad(what + ": expected " + count + " numbers");
				}
				double[] array = new double[count];
				for (int i = 0; i < count; i++)
				{
					if (!(list[i] is double))
					{
						throw new Bad(what + ": not a number");
					}
					array[i] = (double)list[i];
					if (double.IsNaN(array[i]) || double.IsInfinity(array[i]) || Math.Abs(array[i]) > 1E+30)
					{
						throw new Bad(what + ": not a number or out of range");
					}
				}
				return array;
			}
		}

		private sealed class JsonReader
		{
			private const int MaxDepth = 64;

			private readonly string _s;

			private int _i;

			private int _depth;

			private JsonReader(string s)
			{
				_s = s;
			}

			public static object Parse(string s)
			{
				JsonReader jsonReader = new JsonReader(s);
				jsonReader.Ws();
				object result = jsonReader.Value();
				jsonReader.Ws();
				if (jsonReader._i != s.Length)
				{
					jsonReader.Fail("unexpected text after the end");
				}
				return result;
			}

			private void Fail(string why)
			{
				throw new Bad("bad JSON at character " + _i + ": " + why);
			}

			private void Ws()
			{
				while (_i < _s.Length)
				{
					char c = _s[_i];
					if (c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '\0')
					{
						_i++;
						continue;
					}
					break;
				}
			}

			private object Value()
			{
				if (_i >= _s.Length)
				{
					Fail("it ends early");
				}
				char c = _s[_i];
				switch (c)
				{
				case '{':
					return Object();
				case '[':
					return Array();
				case '"':
					return String();
				case 't':
					Word("true");
					return true;
				case 'f':
					Word("false");
					return false;
				case 'n':
					Word("null");
					return null;
				default:
					switch (c)
					{
					case '-':
					case '0':
					case '1':
					case '2':
					case '3':
					case '4':
					case '5':
					case '6':
					case '7':
					case '8':
					case '9':
						return Number();
					default:
						Fail("unexpected '" + c + "'");
						return null;
					}
				}
			}

			private void Word(string w)
			{
				if (string.CompareOrdinal(_s, _i, w, 0, w.Length) != 0)
				{
					Fail("unknown word");
				}
				_i += w.Length;
			}

			private void Enter()
			{
				if (++_depth > 64)
				{
					Fail("nested too deep");
				}
			}

			private Dictionary<string, object> Object()
			{
				Enter();
				Dictionary<string, object> dictionary = new Dictionary<string, object>();
				_i++;
				Ws();
				if (_i < _s.Length && _s[_i] == '}')
				{
					_i++;
					_depth--;
					return dictionary;
				}
				while (true)
				{
					Ws();
					if (_i >= _s.Length || _s[_i] != '"')
					{
						Fail("expected a key");
					}
					string key = String();
					Ws();
					if (_i >= _s.Length || _s[_i] != ':')
					{
						Fail("expected ':'");
					}
					_i++;
					Ws();
					dictionary[key] = Value();
					Ws();
					if (_i >= _s.Length)
					{
						Fail("it ends inside an object");
					}
					if (_s[_i] == ',')
					{
						_i++;
						continue;
					}
					if (_s[_i] == '}')
					{
						break;
					}
					Fail("expected ',' or '}'");
				}
				_i++;
				_depth--;
				return dictionary;
			}

			private List<object> Array()
			{
				Enter();
				List<object> list = new List<object>();
				_i++;
				Ws();
				if (_i < _s.Length && _s[_i] == ']')
				{
					_i++;
					_depth--;
					return list;
				}
				while (true)
				{
					Ws();
					list.Add(Value());
					Ws();
					if (_i >= _s.Length)
					{
						Fail("it ends inside a list");
					}
					if (_s[_i] == ',')
					{
						_i++;
						continue;
					}
					if (_s[_i] == ']')
					{
						break;
					}
					Fail("expected ',' or ']'");
				}
				_i++;
				_depth--;
				return list;
			}

			private string String()
			{
				_i++;
				StringBuilder stringBuilder = new StringBuilder();
				while (true)
				{
					if (_i >= _s.Length)
					{
						Fail("a string runs to the end");
					}
					char c = _s[_i++];
					if (c == '"')
					{
						break;
					}
					if (c < ' ')
					{
						Fail("a control character in a string");
					}
					if (c != '\\')
					{
						stringBuilder.Append(c);
						continue;
					}
					if (_i >= _s.Length)
					{
						Fail("a string runs to the end");
					}
					switch (_s[_i++])
					{
					case '"':
						stringBuilder.Append('"');
						break;
					case '\\':
						stringBuilder.Append('\\');
						break;
					case '/':
						stringBuilder.Append('/');
						break;
					case 'b':
						stringBuilder.Append('\b');
						break;
					case 'f':
						stringBuilder.Append('\f');
						break;
					case 'n':
						stringBuilder.Append('\n');
						break;
					case 'r':
						stringBuilder.Append('\r');
						break;
					case 't':
						stringBuilder.Append('\t');
						break;
					case 'u':
					{
						if (_i + 4 > _s.Length)
						{
							Fail("a short \\u escape");
						}
						int num = 0;
						for (int i = 0; i < 4; i++)
						{
							int num2 = Hex(_s[_i + i]);
							if (num2 < 0)
							{
								Fail("a bad \\u escape");
							}
							num = num * 16 + num2;
						}
						_i += 4;
						stringBuilder.Append((char)num);
						break;
					}
					default:
						Fail("a bad escape");
						break;
					}
				}
				return stringBuilder.ToString();
			}

			private static int Hex(char c)
			{
				if (c < '0' || c > '9')
				{
					if (c < 'a' || c > 'f')
					{
						if (c < 'A' || c > 'F')
						{
							return -1;
						}
						return c - 65 + 10;
					}
					return c - 97 + 10;
				}
				return c - 48;
			}

			private double Number()
			{
				int i = _i;
				if (_s[_i] == '-')
				{
					_i++;
				}
				if (_i >= _s.Length)
				{
					Fail("a bad number");
				}
				if (_s[_i] == '0')
				{
					_i++;
				}
				else if (_s[_i] >= '1' && _s[_i] <= '9')
				{
					while (_i < _s.Length && char.IsDigit(_s[_i]) && _s[_i] <= '9')
					{
						_i++;
					}
				}
				else
				{
					Fail("a bad number");
				}
				if (_i < _s.Length && _s[_i] == '.')
				{
					_i++;
					int i2 = _i;
					while (_i < _s.Length && _s[_i] >= '0' && _s[_i] <= '9')
					{
						_i++;
					}
					if (_i == i2)
					{
						Fail("a bad number");
					}
				}
				if (_i < _s.Length && (_s[_i] == 'e' || _s[_i] == 'E'))
				{
					_i++;
					if (_i < _s.Length && (_s[_i] == '+' || _s[_i] == '-'))
					{
						_i++;
					}
					int i3 = _i;
					while (_i < _s.Length && _s[_i] >= '0' && _s[_i] <= '9')
					{
						_i++;
					}
					if (_i == i3)
					{
						Fail("a bad number");
					}
				}
				if (_i - i > 64)
				{
					Fail("a number too long");
				}
				if (!double.TryParse(_s.Substring(i, _i - i), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || double.IsInfinity(result) || double.IsNaN(result))
				{
					Fail("a number out of range");
				}
				return result;
			}
		}

		private static readonly string[] Kinds = new string[20]
		{
			"plain", "leaves", "bark", "wood", "stone", "roof", "thatch", "cloth", "skin", "hair",
			"metal", "fire", "glass", "plaster", "rock", "eyes", "ancient", "obsidian", "plates", "marble"
		};

		private static readonly string[] FlagNames = new string[8] { "emissive", "nightglow", "portal", "sheer", "shiny", "selfglow", "crown", "shell" };

		public static MeshModel Read(byte[] data, string name, GlbLimits limits, List<string> problems, out GlbExtras extras)
		{
			extras = null;
			if (problems == null)
			{
				problems = new List<string>();
			}
			if (limits == null)
			{
				limits = new GlbLimits();
			}
			try
			{
				if (data == null)
				{
					throw new Bad("no data");
				}
				Reader reader = new Reader(data, name ?? "model", limits, problems);
				MeshModel result = reader.Run();
				extras = reader.Extras;
				return result;
			}
			catch (Bad bad)
			{
				problems.Add(bad.Message);
			}
			catch (Exception ex)
			{
				problems.Add("could not read the file (" + ex.GetType().Name + ": " + ex.Message + ")");
			}
			return null;
		}

		private static string StripNumber(string s)
		{
			int num = s.LastIndexOf('.');
			if (num < 0 || s.Length - num - 1 < 3)
			{
				return s;
			}
			for (int i = num + 1; i < s.Length; i++)
			{
				if (s[i] < '0' || s[i] > '9')
				{
					return s;
				}
			}
			return s.Substring(0, num);
		}
	}
}
