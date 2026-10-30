using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FirstPerson.Core
{
	public static class MeshLibrary
	{
		public static Dictionary<string, MeshModel> Load(Stream stream)
		{
			byte[] array = new byte[4];
			ReadAll(stream, array, 4);
			string text = Encoding.ASCII.GetString(array);
			if (text == "FPM2")
			{
				return LoadPacked(stream);
			}
			if (text != "FPM1")
			{
				throw new InvalidDataException("not a model library");
			}
			if (!BitConverter.IsLittleEndian)
			{
				return LoadEach(stream);
			}
			Dictionary<string, MeshModel> dictionary = new Dictionary<string, MeshModel>();
			byte[] bytes = new byte[0];
			float[] array2 = new float[0];
			using (BinaryReader binaryReader = new BinaryReader(stream))
			{
				int num = binaryReader.ReadInt32();
				for (int i = 0; i < num; i++)
				{
					MeshModel meshModel = new MeshModel
					{
						Name = Str(binaryReader)
					};
					int num2 = binaryReader.ReadInt32();
					meshModel.SlotNames = new string[num2];
					meshModel.SlotKind = new byte[num2];
					meshModel.SlotFlags = new byte[num2];
					meshModel.SlotColor = new uint[num2];
					for (int j = 0; j < num2; j++)
					{
						meshModel.SlotNames[j] = Str(binaryReader);
						meshModel.SlotKind[j] = binaryReader.ReadByte();
						meshModel.SlotFlags[j] = binaryReader.ReadByte();
						int r = binaryReader.ReadByte();
						int g = binaryReader.ReadByte();
						int b = binaryReader.ReadByte();
						meshModel.SlotColor[j] = TerrainCache.Rgb(r, g, b);
					}
					meshModel.ParseInfo(Str(binaryReader));
					int num3 = binaryReader.ReadInt32();
					meshModel.Parts = new MeshPart[num3];
					float num4 = float.MaxValue;
					float num5 = float.MaxValue;
					float num6 = float.MaxValue;
					float num7 = float.MinValue;
					float num8 = float.MinValue;
					float num9 = float.MinValue;
					for (int k = 0; k < num3; k++)
					{
						MeshPart meshPart = new MeshPart
						{
							Name = Str(binaryReader),
							Parent = binaryReader.ReadInt32()
						};
						meshPart.Pivot = new Vec3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
						int num10 = binaryReader.ReadInt32();
						meshPart.Pos = new float[num10 * 3];
						meshPart.Nrm = new float[num10 * 3];
						int num11 = num10 * 6;
						if (array2.Length < num11)
						{
							array2 = new float[num11];
						}
						ReadBlock(binaryReader, ref bytes, num11 * 4);
						Buffer.BlockCopy(bytes, 0, array2, 0, num11 * 4);
						float[] pos = meshPart.Pos;
						float[] nrm = meshPart.Nrm;
						int num12 = 0;
						int num13 = 0;
						int num14 = 0;
						while (num12 < num10)
						{
							float num15 = array2[num13];
							float num16 = array2[num13 + 1];
							float num17 = array2[num13 + 2];
							pos[num14] = num15;
							pos[num14 + 1] = num16;
							pos[num14 + 2] = num17;
							nrm[num14] = array2[num13 + 3];
							nrm[num14 + 1] = array2[num13 + 4];
							nrm[num14 + 2] = array2[num13 + 5];
							num4 = Math.Min(num4, num15);
							num5 = Math.Min(num5, num16);
							num6 = Math.Min(num6, num17);
							num7 = Math.Max(num7, num15);
							num8 = Math.Max(num8, num16);
							num9 = Math.Max(num9, num17);
							num12++;
							num13 += 6;
							num14 += 3;
						}
						int num18 = binaryReader.ReadInt32();
						meshPart.Tri = new int[num18 * 3];
						ReadBlock(binaryReader, ref bytes, num18 * 12);
						Buffer.BlockCopy(bytes, 0, meshPart.Tri, 0, num18 * 12);
						meshPart.Slot = binaryReader.ReadBytes(num18);
						meshPart.Axis = new byte[num18];
						for (int l = 0; l < num18; l++)
						{
							meshPart.Axis[l] = DominantAxis(meshPart, l);
						}
						meshModel.Parts[k] = meshPart;
					}
					meshModel.Min = new Vec3(num4, num5, num6);
					meshModel.Max = new Vec3(num7, num8, num9);
					dictionary[meshModel.Name] = meshModel;
				}
				return dictionary;
			}
		}

		private static void ReadBlock(BinaryReader r, ref byte[] bytes, int count)
		{
			if (bytes.Length < count)
			{
				bytes = new byte[Math.Max(count, bytes.Length * 2)];
			}
			int num;
			for (int i = 0; i < count; i += num)
			{
				num = r.Read(bytes, i, count - i);
				if (num <= 0)
				{
					throw new EndOfStreamException("model library cut short");
				}
			}
		}

		private static Dictionary<string, MeshModel> LoadEach(Stream stream)
		{
			Dictionary<string, MeshModel> dictionary = new Dictionary<string, MeshModel>();
			using (BinaryReader binaryReader = new BinaryReader(stream))
			{
				int num = binaryReader.ReadInt32();
				for (int i = 0; i < num; i++)
				{
					MeshModel meshModel = new MeshModel
					{
						Name = Str(binaryReader)
					};
					int num2 = binaryReader.ReadInt32();
					meshModel.SlotNames = new string[num2];
					meshModel.SlotKind = new byte[num2];
					meshModel.SlotFlags = new byte[num2];
					meshModel.SlotColor = new uint[num2];
					for (int j = 0; j < num2; j++)
					{
						meshModel.SlotNames[j] = Str(binaryReader);
						meshModel.SlotKind[j] = binaryReader.ReadByte();
						meshModel.SlotFlags[j] = binaryReader.ReadByte();
						int r = binaryReader.ReadByte();
						int g = binaryReader.ReadByte();
						int b = binaryReader.ReadByte();
						meshModel.SlotColor[j] = TerrainCache.Rgb(r, g, b);
					}
					meshModel.ParseInfo(Str(binaryReader));
					int num3 = binaryReader.ReadInt32();
					meshModel.Parts = new MeshPart[num3];
					Vec3 min = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
					Vec3 max = new Vec3(float.MinValue, float.MinValue, float.MinValue);
					for (int k = 0; k < num3; k++)
					{
						MeshPart meshPart = new MeshPart
						{
							Name = Str(binaryReader),
							Parent = binaryReader.ReadInt32()
						};
						meshPart.Pivot = new Vec3(binaryReader.ReadSingle(), binaryReader.ReadSingle(), binaryReader.ReadSingle());
						int num4 = binaryReader.ReadInt32();
						meshPart.Pos = new float[num4 * 3];
						meshPart.Nrm = new float[num4 * 3];
						for (int l = 0; l < num4; l++)
						{
							for (int m = 0; m < 3; m++)
							{
								meshPart.Pos[l * 3 + m] = binaryReader.ReadSingle();
							}
							for (int n = 0; n < 3; n++)
							{
								meshPart.Nrm[l * 3 + n] = binaryReader.ReadSingle();
							}
							float val = meshPart.Pos[l * 3];
							float val2 = meshPart.Pos[l * 3 + 1];
							float val3 = meshPart.Pos[l * 3 + 2];
							min = new Vec3(Math.Min(min.X, val), Math.Min(min.Y, val2), Math.Min(min.Z, val3));
							max = new Vec3(Math.Max(max.X, val), Math.Max(max.Y, val2), Math.Max(max.Z, val3));
						}
						int num5 = binaryReader.ReadInt32();
						meshPart.Tri = new int[num5 * 3];
						for (int num6 = 0; num6 < num5 * 3; num6++)
						{
							meshPart.Tri[num6] = binaryReader.ReadInt32();
						}
						meshPart.Slot = binaryReader.ReadBytes(num5);
						meshPart.Axis = new byte[num5];
						for (int num7 = 0; num7 < num5; num7++)
						{
							meshPart.Axis[num7] = DominantAxis(meshPart, num7);
						}
						meshModel.Parts[k] = meshPart;
					}
					meshModel.Min = min;
					meshModel.Max = max;
					dictionary[meshModel.Name] = meshModel;
				}
				return dictionary;
			}
		}

		internal static byte DominantAxis(MeshPart p, int t)
		{
			int num = p.Tri[t * 3] * 3;
			int num2 = p.Tri[t * 3 + 1] * 3;
			int num3 = p.Tri[t * 3 + 2] * 3;
			float num4 = p.Pos[num2] - p.Pos[num];
			float num5 = p.Pos[num2 + 1] - p.Pos[num + 1];
			float num6 = p.Pos[num2 + 2] - p.Pos[num + 2];
			float num7 = p.Pos[num3] - p.Pos[num];
			float num8 = p.Pos[num3 + 1] - p.Pos[num + 1];
			float num9 = p.Pos[num3 + 2] - p.Pos[num + 2];
			float num10 = Math.Abs(num5 * num9 - num6 * num8);
			float num11 = Math.Abs(num6 * num7 - num4 * num9);
			float num12 = Math.Abs(num4 * num8 - num5 * num7);
			return (byte)((!(num10 >= num11) || !(num10 >= num12)) ? ((num11 >= num12) ? 1u : 2u) : 0u);
		}

		private static string Str(BinaryReader r)
		{
			return Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));
		}

		private static void ReadAll(Stream s, byte[] into, int count)
		{
			int num;
			for (int i = 0; i < count; i += num)
			{
				num = s.Read(into, i, count - i);
				if (num <= 0)
				{
					throw new EndOfStreamException("model library cut short");
				}
			}
		}

		private static Dictionary<string, MeshModel> LoadPacked(Stream stream)
		{
			byte[] array = new byte[8];
			ReadAll(stream, array, 8);
			int outLength = I32(array, 0);
			int num = I32(array, 4);
			byte[] array2 = new byte[num];
			ReadAll(stream, array2, num);
			byte[] array3 = Inflate.Raw(array2, 0, num, outLength);
			array2 = null;
			int num2 = I32(array3, 0);
			int o = 28;
			int num3 = o + I32(array3, 4);
			int num4 = num3 + I32(array3, 8);
			int num5 = num4 + I32(array3, 12);
			int num6 = num5 + I32(array3, 16);
			int num7 = num6 + I32(array3, 20);
			if (num7 + I32(array3, 24) != array3.Length)
			{
				throw new InvalidDataException("model library: its streams don't add up");
			}
			Dictionary<string, MeshModel> dictionary = new Dictionary<string, MeshModel>(num2);
			float[] array4 = new float[3];
			float[] array5 = new float[3];
			for (int i = 0; i < num2; i++)
			{
				MeshModel meshModel = new MeshModel
				{
					Name = S(array3, ref o)
				};
				int num8 = I32(array3, o);
				o += 4;
				meshModel.SlotNames = new string[num8];
				meshModel.SlotKind = new byte[num8];
				meshModel.SlotFlags = new byte[num8];
				meshModel.SlotColor = new uint[num8];
				for (int j = 0; j < num8; j++)
				{
					meshModel.SlotNames[j] = S(array3, ref o);
					meshModel.SlotKind[j] = array3[o];
					meshModel.SlotFlags[j] = array3[o + 1];
					meshModel.SlotColor[j] = TerrainCache.Rgb(array3[o + 2], array3[o + 3], array3[o + 4]);
					o += 5;
				}
				meshModel.ParseInfo(S(array3, ref o));
				int num9 = I32(array3, o);
				o += 4;
				meshModel.Parts = new MeshPart[num9];
				float num10 = float.MaxValue;
				float num11 = float.MaxValue;
				float num12 = float.MaxValue;
				float num13 = float.MinValue;
				float num14 = float.MinValue;
				float num15 = float.MinValue;
				for (int k = 0; k < num9; k++)
				{
					MeshPart meshPart = new MeshPart
					{
						Name = S(array3, ref o),
						Parent = I32(array3, o)
					};
					meshPart.Pivot = new Vec3(F32(array3, o + 4), F32(array3, o + 8), F32(array3, o + 12));
					int num16 = I32(array3, o + 16);
					int num17 = I32(array3, o + 20);
					o += 24;
					float[] array6 = (meshPart.Pos = new float[num16 * 3]);
					float[] array7 = (meshPart.Nrm = new float[num16 * 3]);
					if (num16 > 0)
					{
						for (int l = 0; l < 3; l++)
						{
							array4[l] = F32(array3, o + l * 4);
							array5[l] = F32(array3, o + 12 + l * 4);
						}
						o += 24;
						for (int m = 0; m < 3; m++)
						{
							float num18 = array4[m];
							float num19 = array5[m];
							int num20 = 0;
							int num21 = m;
							while (num20 < num16)
							{
								array6[num21] = num18 + (float)(array3[num3] | (array3[num3 + 1] << 8)) * num19;
								num20++;
								num21 += 3;
								num3 += 2;
							}
						}
						int num22 = 0;
						int num23 = 0;
						while (num22 < num16)
						{
							int num24 = array3[num4] | ((array3[num4 + 1] & 0xF) << 8);
							int num25 = (array3[num4 + 1] >> 4) | (array3[num4 + 2] << 4);
							float num26 = (float)num24 / 4095f * 2f - 1f;
							float num27 = (float)num25 / 4095f * 2f - 1f;
							float num28 = 1f - Math.Abs(num26) - Math.Abs(num27);
							float num29 = Math.Max(0f - num28, 0f);
							num26 += ((num26 >= 0f) ? (0f - num29) : num29);
							num27 += ((num27 >= 0f) ? (0f - num29) : num29);
							float num30 = (float)Math.Sqrt(num26 * num26 + num27 * num27 + num28 * num28);
							array7[num23] = num26 / num30;
							array7[num23 + 1] = num27 / num30;
							array7[num23 + 2] = num28 / num30;
							float num31 = array6[num23];
							float num32 = array6[num23 + 1];
							float num33 = array6[num23 + 2];
							if (num31 < num10)
							{
								num10 = num31;
							}
							if (num32 < num11)
							{
								num11 = num32;
							}
							if (num33 < num12)
							{
								num12 = num33;
							}
							if (num31 > num13)
							{
								num13 = num31;
							}
							if (num32 > num14)
							{
								num14 = num32;
							}
							if (num33 > num15)
							{
								num15 = num33;
							}
							num22++;
							num23 += 3;
							num4 += 3;
						}
					}
					int[] array8 = (meshPart.Tri = new int[num17 * 3]);
					int num34 = 0;
					if (num16 < 32768)
					{
						int num35 = 0;
						while (num35 < array8.Length)
						{
							num34 = (array8[num35] = num34 + (short)(array3[num5] | (array3[num5 + 1] << 8)));
							num35++;
							num5 += 2;
						}
					}
					else
					{
						int num36 = 0;
						while (num36 < array8.Length)
						{
							num34 = (array8[num36] = num34 + I32(array3, num5));
							num36++;
							num5 += 4;
						}
					}
					meshPart.Slot = new byte[num17];
					Buffer.BlockCopy(array3, num6, meshPart.Slot, 0, num17);
					num6 += num17;
					byte[] array9 = (meshPart.Axis = new byte[num17]);
					for (int n = 0; n < num17; n++)
					{
						array9[n] = (byte)((array3[num7 + (n >> 2)] >> 2 * (n & 3)) & 3);
					}
					num7 += num17 + 3 >> 2;
					meshModel.Parts[k] = meshPart;
				}
				if (num10 > num13)
				{
					num10 = (num11 = (num12 = (num13 = (num14 = (num15 = 0f)))));
				}
				meshModel.Min = new Vec3(num10, num11, num12);
				meshModel.Max = new Vec3(num13, num14, num15);
				dictionary[meshModel.Name] = meshModel;
			}
			return dictionary;
		}

		private static int I32(byte[] b, int o)
		{
			return b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24);
		}

		private static float F32(byte[] b, int o)
		{
			if (BitConverter.IsLittleEndian)
			{
				return BitConverter.ToSingle(b, o);
			}
			return BitConverter.ToSingle(new byte[4]
			{
				b[o + 3],
				b[o + 2],
				b[o + 1],
				b[o]
			}, 0);
		}

		private static string S(byte[] b, ref int o)
		{
			int num = I32(b, o);
			string result = Encoding.UTF8.GetString(b, o + 4, num);
			o += 4 + num;
			return result;
		}
	}
}
