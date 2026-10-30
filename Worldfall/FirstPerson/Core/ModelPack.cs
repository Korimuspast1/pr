using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FirstPerson.Core
{
	public static class ModelPack
	{
		public const string Magic = "FPP2";

		private const float SharpCos = 0.7431448f;

		public static List<string> ReadIndex(Stream stream)
		{
			BinaryReader binaryReader = new BinaryReader(stream);
			if (Encoding.ASCII.GetString(binaryReader.ReadBytes(4)) != "FPP2")
			{
				throw new InvalidDataException("not a model pack");
			}
			int num = binaryReader.ReadInt32();
			List<string> list = new List<string>(num);
			for (int i = 0; i < num; i++)
			{
				list.Add(Str(binaryReader));
			}
			return list;
		}

		public static Dictionary<string, MeshModel> Load(Stream stream)
		{
			BinaryReader binaryReader = new BinaryReader(stream);
			if (Encoding.ASCII.GetString(binaryReader.ReadBytes(4)) != "FPP2")
			{
				throw new InvalidDataException("not a model pack");
			}
			int num = binaryReader.ReadInt32();
			for (int i = 0; i < num; i++)
			{
				Str(binaryReader);
			}
			int outLength = binaryReader.ReadInt32();
			int num2 = binaryReader.ReadInt32();
			byte[] array = binaryReader.ReadBytes(num2);
			if (array.Length != num2)
			{
				throw new InvalidDataException("model pack cut short");
			}
			byte[] buffer = Inflate.Raw(array, 0, num2, outLength);
			Dictionary<string, MeshModel> dictionary = new Dictionary<string, MeshModel>(num);
			using (BinaryReader binaryReader2 = new BinaryReader(new MemoryStream(buffer)))
			{
				int num3 = binaryReader2.ReadInt32();
				for (int j = 0; j < num3; j++)
				{
					MeshModel meshModel = ReadModel(binaryReader2);
					dictionary[meshModel.Name] = meshModel;
				}
				return dictionary;
			}
		}

		private static MeshModel ReadModel(BinaryReader r)
		{
			MeshModel meshModel = new MeshModel
			{
				Name = Str(r)
			};
			int num = r.ReadByte();
			meshModel.SlotNames = new string[num];
			meshModel.SlotKind = new byte[num];
			meshModel.SlotFlags = new byte[num];
			meshModel.SlotColor = new uint[num];
			for (int i = 0; i < num; i++)
			{
				meshModel.SlotNames[i] = Str(r);
				meshModel.SlotKind[i] = r.ReadByte();
				meshModel.SlotFlags[i] = r.ReadByte();
				int r2 = r.ReadByte();
				int g = r.ReadByte();
				int b = r.ReadByte();
				meshModel.SlotColor[i] = TerrainCache.Rgb(r2, g, b);
			}
			meshModel.ParseInfo(Str(r));
			int num2 = r.ReadByte();
			meshModel.Parts = new MeshPart[num2];
			Vec3 vec = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
			Vec3 max = new Vec3(float.MinValue, float.MinValue, float.MinValue);
			for (int j = 0; j < num2; j++)
			{
				MeshPart meshPart = new MeshPart
				{
					Name = Str(r),
					Parent = r.ReadInt16()
				};
				meshPart.Pivot = new Vec3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
				float num3 = r.ReadSingle();
				float num4 = r.ReadSingle();
				float num5 = r.ReadSingle();
				float num6 = (r.ReadSingle() - num3) / 65535f;
				float num7 = (r.ReadSingle() - num4) / 65535f;
				float num8 = (r.ReadSingle() - num5) / 65535f;
				int num9 = r.ReadInt32();
				float[] array = new float[num9 * 3];
				for (int k = 0; k < num9; k++)
				{
					array[k * 3] = num3 + (float)(int)r.ReadUInt16() * num6;
					array[k * 3 + 1] = num4 + (float)(int)r.ReadUInt16() * num7;
					array[k * 3 + 2] = num5 + (float)(int)r.ReadUInt16() * num8;
				}
				int num10 = r.ReadInt32();
				int[] array2 = new int[num10 * 3];
				bool flag = num9 > 65536;
				for (int l = 0; l < num10 * 3; l++)
				{
					array2[l] = (flag ? r.ReadInt32() : r.ReadUInt16());
				}
				byte[] code = r.ReadBytes(num10);
				Expand(meshPart, array, array2, code);
				int m = 0;
				for (int num11 = meshPart.Pos.Length; m < num11; m += 3)
				{
					float num12 = meshPart.Pos[m];
					float num13 = meshPart.Pos[m + 1];
					float num14 = meshPart.Pos[m + 2];
					if (num12 < vec.X)
					{
						vec.X = num12;
					}
					if (num13 < vec.Y)
					{
						vec.Y = num13;
					}
					if (num14 < vec.Z)
					{
						vec.Z = num14;
					}
					if (num12 > max.X)
					{
						max.X = num12;
					}
					if (num13 > max.Y)
					{
						max.Y = num13;
					}
					if (num14 > max.Z)
					{
						max.Z = num14;
					}
				}
				meshModel.Parts[j] = meshPart;
			}
			if (vec.X > max.X)
			{
				vec = new Vec3(0f, 0f, 0f);
				max = vec;
			}
			meshModel.Min = vec;
			meshModel.Max = max;
			return meshModel;
		}

		private static void Expand(MeshPart part, float[] corner, int[] index, byte[] code)
		{
			int num = code.Length;
			int num2 = corner.Length / 3;
			float[] array = new float[num * 3];
			float[] array2 = new float[num * 3];
			for (int i = 0; i < num; i++)
			{
				int num3 = index[i * 3] * 3;
				int num4 = index[i * 3 + 1] * 3;
				int num5 = index[i * 3 + 2] * 3;
				float num6 = corner[num4] - corner[num3];
				float num7 = corner[num4 + 1] - corner[num3 + 1];
				float num8 = corner[num4 + 2] - corner[num3 + 2];
				float num9 = corner[num5] - corner[num3];
				float num10 = corner[num5 + 1] - corner[num3 + 1];
				float num11 = corner[num5 + 2] - corner[num3 + 2];
				float num12 = num7 * num11 - num8 * num10;
				float num13 = num8 * num9 - num6 * num11;
				float num14 = num6 * num10 - num7 * num9;
				array[i * 3] = num12;
				array[i * 3 + 1] = num13;
				array[i * 3 + 2] = num14;
				float num15 = (float)Math.Sqrt(num12 * num12 + num13 * num13 + num14 * num14);
				if (num15 > 1E-12f)
				{
					array2[i * 3] = num12 / num15;
					array2[i * 3 + 1] = num13 / num15;
					array2[i * 3 + 2] = num14 / num15;
				}
				else
				{
					array2[i * 3 + 2] = 1f;
				}
			}
			int[] array3 = new int[num2 + 1];
			for (int j = 0; j < num; j++)
			{
				if (code[j] >> 6 == 1)
				{
					for (int k = 0; k < 3; k++)
					{
						array3[index[j * 3 + k] + 1]++;
					}
				}
			}
			for (int l = 0; l < num2; l++)
			{
				array3[l + 1] += array3[l];
			}
			int[] array4 = new int[num2];
			int[] array5 = new int[array3[num2]];
			for (int m = 0; m < num; m++)
			{
				if (code[m] >> 6 == 1)
				{
					for (int n = 0; n < 3; n++)
					{
						int num16 = index[m * 3 + n];
						array5[array3[num16] + array4[num16]++] = m;
					}
				}
			}
			List<float> list = new List<float>(num * 6);
			List<float> list2 = new List<float>(num * 6);
			int[] array6 = new int[num * 3];
			byte[] array7 = new byte[num];
			Dictionary<long, int> dictionary = new Dictionary<long, int>(num * 2);
			for (int num17 = 0; num17 < num; num17++)
			{
				int num18 = code[num17] >> 6;
				array7[num17] = (byte)(code[num17] & 0x3F);
				float num19 = array2[num17 * 3];
				float num20 = array2[num17 * 3 + 1];
				float num21 = array2[num17 * 3 + 2];
				for (int num22 = 0; num22 < 3; num22++)
				{
					int num23 = index[num17 * 3 + num22];
					float num24 = num19;
					float num25 = num20;
					float num26 = num21;
					switch (num18)
					{
					case 1:
					{
						float num29 = 0f;
						float num30 = 0f;
						float num31 = 0f;
						for (int num32 = array3[num23]; num32 < array3[num23 + 1]; num32++)
						{
							int num33 = array5[num32];
							if (!(array2[num33 * 3] * num19 + array2[num33 * 3 + 1] * num20 + array2[num33 * 3 + 2] * num21 < 0.7431448f))
							{
								num29 += array[num33 * 3];
								num30 += array[num33 * 3 + 1];
								num31 += array[num33 * 3 + 2];
							}
						}
						float num34 = (float)Math.Sqrt(num29 * num29 + num30 * num30 + num31 * num31);
						if (num34 > 1E-12f)
						{
							num24 = num29 / num34;
							num25 = num30 / num34;
							num26 = num31 / num34;
						}
						break;
					}
					case 2:
					{
						float num27 = ((num21 >= 0f) ? 1f : (-1f));
						num24 = num19 * num27 * 0.35f;
						num25 = num20 * num27 * 0.35f;
						num26 = 1f + num21 * num27 * 0.35f;
						float num28 = (float)Math.Sqrt(num24 * num24 + num25 * num25 + num26 * num26);
						num24 /= num28;
						num25 /= num28;
						num26 /= num28;
						break;
					}
					}
					long key = ((long)num23 << 39) | ((long)Q(num24) << 26) | ((long)Q(num25) << 13) | Q(num26);
					if (!dictionary.TryGetValue(key, out var value))
					{
						value = (dictionary[key] = list.Count / 3);
						list.Add(corner[num23 * 3]);
						list.Add(corner[num23 * 3 + 1]);
						list.Add(corner[num23 * 3 + 2]);
						list2.Add(num24);
						list2.Add(num25);
						list2.Add(num26);
					}
					array6[num17 * 3 + num22] = value;
				}
			}
			part.Pos = list.ToArray();
			part.Nrm = list2.ToArray();
			part.Tri = array6;
			part.Slot = array7;
			part.Axis = new byte[num];
			for (int num36 = 0; num36 < num; num36++)
			{
				part.Axis[num36] = MeshLibrary.DominantAxis(part, num36);
			}
		}

		private static int Q(float c)
		{
			return Math.Max(0, Math.Min(8191, (int)Math.Round(c * 4000f) + 4096));
		}

		private static string Str(BinaryReader r)
		{
			return Encoding.UTF8.GetString(r.ReadBytes(r.ReadUInt16()));
		}
	}
}
