using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FirstPerson.Core
{
	public static class GlbWriter
	{
		private static readonly string[] KindNames = new string[20]
		{
			"plain", "leaves", "bark", "wood", "stone", "roof", "thatch", "cloth", "skin", "hair",
			"metal", "fire", "glass", "plaster", "rock", "eyes", "ancient", "obsidian", "plates", "marble"
		};

		private static readonly string[] FlagNames = new string[8] { "emissive", "nightglow", "portal", "sheer", "shiny", "selfglow", "crown", "shell" };

		private static string F(float v)
		{
			return v.ToString("R", CultureInfo.InvariantCulture);
		}

		public static byte[] Write(MeshModel m, string generator = "Worldfall", bool libraryUnits = true)
		{
			MemoryStream bin = new MemoryStream();
			BinaryWriter w = new BinaryWriter(bin);
			List<string> views = new List<string>();
			List<string> accessors = new List<string>();
			List<string> list = new List<string>();
			List<string> list2 = new List<string>();
			List<List<int>> list3 = new List<List<int>>();
			List<int> list4 = new List<int>();
			for (int i = 0; i < m.Parts.Length; i++)
			{
				MeshPart meshPart = m.Parts[i];
				Vec3 pivot = meshPart.Pivot;
				int vertexCount = meshPart.VertexCount;
				float[] array = new float[3]
				{
					float.MaxValue,
					float.MaxValue,
					float.MaxValue
				};
				float[] array2 = new float[3]
				{
					float.MinValue,
					float.MinValue,
					float.MinValue
				};
				Pad();
				long length = bin.Length;
				for (int j = 0; j < vertexCount; j++)
				{
					float num = meshPart.Pos[j * 3] - pivot.X;
					float num2 = meshPart.Pos[j * 3 + 1] - pivot.Y;
					float num3 = meshPart.Pos[j * 3 + 2] - pivot.Z;
					float[] array3 = new float[3]
					{
						num,
						num3,
						0f - num2
					};
					for (int k = 0; k < 3; k++)
					{
						w.Write(array3[k]);
						array[k] = Math.Min(array[k], array3[k]);
						array2[k] = Math.Max(array2[k], array3[k]);
					}
				}
				int num4 = Accessor(View(length, (long)vertexCount * 12L, 34962), 5126, vertexCount, "VEC3", ",\"min\":[" + F(array[0]) + "," + F(array[1]) + "," + F(array[2]) + "],\"max\":[" + F(array2[0]) + "," + F(array2[1]) + "," + F(array2[2]) + "]");
				long length2 = bin.Length;
				for (int l = 0; l < vertexCount; l++)
				{
					w.Write(meshPart.Nrm[l * 3]);
					w.Write(meshPart.Nrm[l * 3 + 2]);
					w.Write(0f - meshPart.Nrm[l * 3 + 1]);
				}
				int num5 = Accessor(View(length2, (long)vertexCount * 12L, 34962), 5126, vertexCount, "VEC3");
				List<string> list5 = new List<string>();
				for (int n = 0; n < m.SlotNames.Length; n++)
				{
					List<int> list6 = new List<int>();
					for (int num6 = 0; num6 < meshPart.TriangleCount; num6++)
					{
						if (meshPart.Slot[num6] == n)
						{
							list6.Add(meshPart.Tri[num6 * 3]);
							list6.Add(meshPart.Tri[num6 * 3 + 1]);
							list6.Add(meshPart.Tri[num6 * 3 + 2]);
						}
					}
					if (list6.Count == 0)
					{
						continue;
					}
					Pad();
					long length3 = bin.Length;
					foreach (int item in list6)
					{
						w.Write((uint)item);
					}
					int num7 = Accessor(View(length3, (long)list6.Count * 4L, 34963), 5125, list6.Count, "SCALAR");
					list5.Add("{\"attributes\":{\"POSITION\":" + num4 + ",\"NORMAL\":" + num5 + "},\"indices\":" + num7 + ",\"material\":" + n + ",\"mode\":4}");
				}
				list.Add("{\"name\":\"" + Esc(meshPart.Name) + "\",\"primitives\":[" + string.Join(",", list5.ToArray()) + "]}");
				Vec3 vec = ((meshPart.Parent >= 0) ? new Vec3(pivot.X - m.Parts[meshPart.Parent].Pivot.X, pivot.Y - m.Parts[meshPart.Parent].Pivot.Y, pivot.Z - m.Parts[meshPart.Parent].Pivot.Z) : pivot);
				list2.Add("{\"name\":\"" + Esc(meshPart.Name) + "\",\"mesh\":" + i + ",\"translation\":[" + F(vec.X) + "," + F(vec.Z) + "," + F(0f - vec.Y) + "]#C" + i + "#}");
				list3.Add(new List<int>());
				if (meshPart.Parent >= 0)
				{
					list3[meshPart.Parent].Add(i);
				}
				else
				{
					list4.Add(i);
				}
			}
			for (int num8 = 0; num8 < list2.Count; num8++)
			{
				list2[num8] = list2[num8].Replace("#C" + num8 + "#", (list3[num8].Count > 0) ? (",\"children\":[" + string.Join(",", list3[num8].ConvertAll((int c) => c.ToString()).ToArray()) + "]") : "");
			}
			List<string> list7 = new List<string>();
			for (int num9 = 0; num9 < m.SlotNames.Length; num9++)
			{
				string text = m.SlotNames[num9] + "|" + ((m.SlotKind[num9] < KindNames.Length) ? KindNames[m.SlotKind[num9]] : "plain");
				for (int num10 = 0; num10 < FlagNames.Length; num10++)
				{
					if ((m.SlotFlags[num9] & (1 << num10)) != 0)
					{
						text = text + "|" + FlagNames[num10];
					}
				}
				uint num11 = m.SlotColor[num9];
				list7.Add("{\"name\":\"" + Esc(text) + "\",\"pbrMetallicRoughness\":{\"baseColorFactor\":[" + F(Lin(num11 & 0xFF)) + "," + F(Lin((num11 >> 8) & 0xFF)) + "," + F(Lin((num11 >> 16) & 0xFF)) + ",1]}}");
			}
			Pad();
			string s = "{\"asset\":{\"version\":\"2.0\",\"generator\":\"" + Esc(generator) + "\"},\"scene\":0,\"scenes\":[{\"nodes\":[" + string.Join(",", list4.ConvertAll((int r) => r.ToString()).ToArray()) + "],\"extras\":{\"info\":\"" + Esc(m.InfoText()) + "\"" + (libraryUnits ? ",\"units\":\"library\"" : "") + "}}],\"nodes\":[" + string.Join(",", list2.ToArray()) + "],\"meshes\":[" + string.Join(",", list.ToArray()) + "],\"materials\":[" + string.Join(",", list7.ToArray()) + "],\"accessors\":[" + string.Join(",", accessors.ToArray()) + "],\"bufferViews\":[" + string.Join(",", views.ToArray()) + "],\"buffers\":[{\"byteLength\":" + bin.Length + "}]}";
			byte[] bytes = Encoding.UTF8.GetBytes(s);
			int num12 = (4 - bytes.Length % 4) % 4;
			byte[] array4 = bin.ToArray();
			MemoryStream memoryStream = new MemoryStream();
			BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
			binaryWriter.Write(1179937895u);
			binaryWriter.Write(2u);
			binaryWriter.Write((uint)(20 + bytes.Length + num12 + 8 + array4.Length));
			binaryWriter.Write((uint)(bytes.Length + num12));
			binaryWriter.Write(1313821514u);
			binaryWriter.Write(bytes);
			for (int num13 = 0; num13 < num12; num13++)
			{
				binaryWriter.Write((byte)32);
			}
			binaryWriter.Write((uint)array4.Length);
			binaryWriter.Write(5130562u);
			binaryWriter.Write(array4);
			return memoryStream.ToArray();
			int Accessor(int view, int componentType, int count, string type, string extra = "")
			{
				accessors.Add("{\"bufferView\":" + view + ",\"componentType\":" + componentType + ",\"count\":" + count + ",\"type\":\"" + type + "\"" + extra + "}");
				return accessors.Count - 1;
			}
			void Pad()
			{
				while (bin.Length % 4 != 0L)
				{
					w.Write((byte)0);
				}
			}
			int View(long offset, long num14, int target)
			{
				views.Add("{\"buffer\":0,\"byteOffset\":" + offset + ",\"byteLength\":" + num14 + ",\"target\":" + target + "}");
				return views.Count - 1;
			}
		}

		private static float Lin(uint c)
		{
			float num = (float)c / 255f;
			if (!(num <= 0.04045f))
			{
				return (float)Math.Pow((num + 0.055f) / 1.055f, 2.4);
			}
			return num / 12.92f;
		}

		private static string Esc(string s)
		{
			return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
		}
	}
}
