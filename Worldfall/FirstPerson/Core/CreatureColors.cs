using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class CreatureColors
	{
		public static Action<string> Trace;

		public static uint[] FromSprite(MeshModel model, SpriteImage img)
		{
			Dictionary<uint, int> dictionary = new Dictionary<uint, int>();
			List<uint> list = new List<uint>();
			for (int i = 0; i < img.Pixels.Length; i++)
			{
				uint num = img.Pixels[i];
				if (num >> 24 >= 128)
				{
					num |= 0xFF000000u;
					list.Add(num);
					uint key = num & 0xFFF0F0F0u;
					dictionary.TryGetValue(key, out var value);
					dictionary[key] = value + 1;
				}
			}
			uint[] array = new uint[model.SlotNames.Length];
			if (list.Count == 0)
			{
				return array;
			}
			uint num2 = 0u;
			int num3 = -1;
			foreach (KeyValuePair<uint, int> item in dictionary)
			{
				int num4 = item.Value * ((Luma(item.Key | 0xFF000000u) < 300) ? 1 : 4);
				if (num4 > num3)
				{
					num3 = num4;
					num2 = item.Key | 0xFF080808u;
				}
			}
			list.Sort((uint a, uint b) => Luma(a).CompareTo(Luma(b)));
			uint num5 = list[list.Count * 12 / 100];
			uint num6 = list[Math.Min(list.Count - 1, list.Count * 86 / 100)];
			if (Math.Abs(Luma(num6) - Luma(num2)) < 150)
			{
				num6 = TerrainCache.Lerp(num2, TerrainCache.Rgb(240, 232, 216), 0.35f);
			}
			if (Math.Abs(Luma(num5) - Luma(num2)) < 150)
			{
				num5 = TerrainCache.Scale(num2, 0.55f);
			}
			uint num7 = 0u;
			num3 = 0;
			foreach (KeyValuePair<uint, int> item2 in dictionary)
			{
				uint num8 = item2.Key | 0xFF080808u;
				if (!(HueDistance(num8, num2) < 0.08f) && !(NaturePalette.Saturation(num8) < 0.25f) && Luma(num8) >= 350 && item2.Value > num3)
				{
					num3 = item2.Value;
					num7 = num8;
				}
			}
			if (num7 == 0)
			{
				num7 = TerrainCache.Lerp(num6, TerrainCache.Rgb(210, 70, 60), 0.5f);
			}
			for (int num9 = 0; num9 < array.Length; num9++)
			{
				switch (model.SlotNames[num9])
				{
				case "fur":
					array[num9] = num2;
					break;
				case "fur_light":
					array[num9] = num6;
					break;
				case "dark":
					array[num9] = num5;
					break;
				case "accent":
					array[num9] = num7;
					break;
				case "shell":
					array[num9] = num2;
					break;
				case "horn":
					array[num9] = TerrainCache.Lerp(num6, TerrainCache.Rgb(232, 222, 196), 0.6f);
					break;
				case "nose":
					array[num9] = TerrainCache.Lerp(num5, TerrainCache.Rgb(150, 90, 90), 0.3f);
					break;
				case "wing":
					array[num9] = TerrainCache.Lerp(num6, TerrainCache.Rgb(236, 240, 244), 0.6f);
					break;
				case "spots":
					array[num9] = SecondCoat(img, num2);
					break;
				}
			}
			return array;
		}

		public static uint[] FromSideSprite(MeshModel model, SpriteImage img)
		{
			uint[] array = FromSprite(model, img);
			string text = model.Info("kind") ?? "";
			switch (text)
			{
			case "blob":
			case "skull":
			case "ghost":
			case "snowman":
			case "seraph":
			case "floating":
			case "insect":
				return array;
			default:
			{
				bool flag = text == "biped";
				int width = img.Width;
				int height = img.Height;
				int num = width;
				int num2 = -1;
				int num3 = height;
				int num4 = -1;
				for (int i = 0; i < height; i++)
				{
					for (int j = 0; j < width; j++)
					{
						if (img.Pixels[i * width + j] >> 24 >= 128)
						{
							if (j < num)
							{
								num = j;
							}
							if (j > num2)
							{
								num2 = j;
							}
							if (i < num3)
							{
								num3 = i;
							}
							if (i > num4)
							{
								num4 = i;
							}
						}
					}
				}
				float num5 = (flag ? model.Min.X : model.Min.Y);
				float num6 = (flag ? (model.Max.X - model.Min.X) : (model.Max.Y - model.Min.Y));
				float num7 = model.Max.Z - model.Min.Z;
				if (num2 < num || num6 < 0.0001f || num7 < 0.0001f)
				{
					return array;
				}
				int num8 = model.SlotNames.Length;
				bool[] array2 = new bool[num8];
				int num9 = 0;
				for (int k = 0; k < num8; k++)
				{
					if (Mapped(model.SlotNames[k]))
					{
						array2[k] = true;
						num9++;
					}
				}
				if (num9 == 0)
				{
					return array;
				}
				int num10 = width * height;
				float[] array3 = new float[num8 * num10];
				int num11 = num2 - num + 1;
				int num12 = num4 - num3 + 1;
				MeshPart[] parts = model.Parts;
				foreach (MeshPart meshPart in parts)
				{
					float[] pos = meshPart.Pos;
					int[] tri = meshPart.Tri;
					for (int m = 0; m < meshPart.TriangleCount; m++)
					{
						int num13 = meshPart.Slot[m];
						if (num13 >= num8 || !array2[num13])
						{
							continue;
						}
						int num14 = tri[m * 3] * 3;
						int num15 = tri[m * 3 + 1] * 3;
						int num16 = tri[m * 3 + 2] * 3;
						float num17 = pos[num15] - pos[num14];
						float num18 = pos[num15 + 1] - pos[num14 + 1];
						float num19 = pos[num15 + 2] - pos[num14 + 2];
						float num20 = pos[num16] - pos[num14];
						float num21 = pos[num16 + 1] - pos[num14 + 1];
						float num22 = pos[num16 + 2] - pos[num14 + 2];
						float num23 = num18 * num22 - num19 * num21;
						float num24 = num19 * num20 - num17 * num22;
						float num25 = num17 * num21 - num18 * num20;
						float num26 = 0.5f * (float)Math.Sqrt(num23 * num23 + num24 * num24 + num25 * num25);
						if (num26 < 1E-09f)
						{
							continue;
						}
						float num27 = num26 * (0.35f + 0.65f * Math.Abs(flag ? num24 : num23) / (2f * num26));
						for (int n = 0; n < 4; n++)
						{
							float num28;
							switch (n)
							{
							default:
								num28 = 0.2f;
								break;
							case 1:
								num28 = 0.6f;
								break;
							case 0:
								num28 = 1f / 3f;
								break;
							}
							float num29 = num28;
							float num30;
							switch (n)
							{
							default:
								num30 = 0.2f;
								break;
							case 2:
								num30 = 0.6f;
								break;
							case 0:
								num30 = 1f / 3f;
								break;
							}
							float num31 = num30;
							float num32 = 1f - num29 - num31;
							int num33 = ((!flag) ? 1 : 0);
							float num34 = pos[num14 + num33] * num29 + pos[num15 + num33] * num31 + pos[num16 + num33] * num32;
							float num35 = pos[num14 + 2] * num29 + pos[num15 + 2] * num31 + pos[num16 + 2] * num32;
							int val = num + Math.Min(num11 - 1, (int)((num34 - num5) / num6 * (float)num11));
							int val2 = num3 + Math.Min(num12 - 1, (int)((num35 - model.Min.Z) / num7 * (float)num12));
							int num36 = Nearest(img, Math.Max(num, val), Math.Max(num3, val2), 3);
							if (num36 >= 0)
							{
								array3[num13 * num10 + num36] += num27 * ((n == 0) ? 0.4f : 0.2f);
							}
						}
					}
				}
				Dictionary<uint, float>[] votes = new Dictionary<uint, float>[num8];
				Dictionary<uint, (float, float, float, float)>[] array4 = new Dictionary<uint, (float, float, float, float)>[num8];
				for (int num37 = 0; num37 < num8; num37++)
				{
					for (int num38 = 0; num38 < num10; num38++)
					{
						float num39 = array3[num37 * num10 + num38];
						if (!(num39 <= 0f))
						{
							uint num40 = img.Pixels[num38];
							uint key = num40 & 0xF0F0F0;
							Dictionary<uint, float>[] array5 = votes;
							int l = num37;
							if (array5[l] == null)
							{
								array5[l] = new Dictionary<uint, float>();
							}
							Dictionary<uint, (float, float, float, float)>[] array6 = array4;
							l = num37;
							if (array6[l] == null)
							{
								array6[l] = new Dictionary<uint, (float, float, float, float)>();
							}
							votes[num37].TryGetValue(key, out var value);
							votes[num37][key] = value + num39;
							array4[num37].TryGetValue(key, out var value2);
							array4[num37][key] = (value2.Item1 + (float)(num40 & 0xFF) * num39, value2.Item2 + (float)((num40 >> 8) & 0xFF) * num39, value2.Item3 + (float)((num40 >> 16) & 0xFF) * num39, value2.Item4 + num39);
						}
					}
				}
				int num41 = model.SlotIndex("fur");
				uint num42 = ((num41 >= 0) ? array[num41] : 0u);
				uint[] array7 = new uint[num8];
				int s;
				for (s = 0; s < num8; s++)
				{
					if (votes[s] == null)
					{
						continue;
					}
					List<uint> list = new List<uint>(votes[s].Keys);
					list.Sort((uint p, uint q) => votes[s][q].CompareTo(votes[s][p]));
					List<(uint, float, float, float, float)> list2 = new List<(uint, float, float, float, float)>();
					float all = 0f;
					foreach (uint item in list)
					{
						(float, float, float, float) tuple = array4[s][item];
						uint c = TerrainCache.Rgb((int)(tuple.Item1 / tuple.Item4), (int)(tuple.Item2 / tuple.Item4), (int)(tuple.Item3 / tuple.Item4));
						all += votes[s][item];
						int num43 = list2.FindIndex(((uint color, float w, float r, float g, float b) gr) => Near(gr.color, c));
						if (num43 < 0)
						{
							list2.Add((c, votes[s][item], tuple.Item1, tuple.Item2, tuple.Item3));
							continue;
						}
						(uint, float, float, float, float) tuple2 = list2[num43];
						list2[num43] = (tuple2.Item1, tuple2.Item2 + votes[s][item], tuple2.Item3 + tuple.Item1, tuple2.Item4 + tuple.Item2, tuple2.Item5 + tuple.Item3);
					}
					int index = 0;
					for (int num44 = 1; num44 < list2.Count; num44++)
					{
						if (list2[num44].Item2 > list2[index].Item2)
						{
							index = num44;
						}
					}
					if (Trace != null)
					{
						Trace(model.Name + " " + model.SlotNames[s] + ": " + string.Join(", ", list2.ConvertAll(((uint color, float w, float r, float g, float b) gr) => $"{gr.color & 0xFFFFFF:X6} {gr.w / all:P0}")));
					}
					if (!(model.SlotNames[s] == "dark") || !(list2[index].Item2 < all * 0.5f))
					{
						(uint, float, float, float, float) tuple3 = list2[index];
						array7[s] = 0xFF000000u | TerrainCache.Rgb((int)(tuple3.Item3 / tuple3.Item2), (int)(tuple3.Item4 / tuple3.Item2), (int)(tuple3.Item5 / tuple3.Item2));
					}
				}
				if (num41 >= 0 && array7[num41] != 0)
				{
					num42 = array7[num41];
				}
				uint num45 = 0u;
				for (int num46 = 0; num46 < num8; num46++)
				{
					string text2 = model.SlotNames[num46];
					uint num47 = array7[num46];
					bool flag2 = num47 == 0 || (num46 != num41 && num42 != 0 && Near(num47, num42));
					if (text2 == "dark" && flag2)
					{
						num47 = ((num42 != 0) ? DarkShade(img, num42) : 0u);
					}
					else if (flag2 && num47 != 0)
					{
						if (text2 == "fur_light")
						{
							num47 = TerrainCache.Lerp(num42, TerrainCache.Rgb(240, 232, 216), model.InfoFloat("light_mix", 0.22f));
						}
						else if (text2 == "horn")
						{
							num47 = TerrainCache.Lerp(num42, TerrainCache.Rgb(232, 222, 196), 0.6f);
						}
					}
					if (num47 != 0)
					{
						array[num46] = num47 | 0xFF000000u;
						if (text2 == "dark")
						{
							num45 = array[num46];
						}
					}
				}
				int num48 = model.SlotIndex("spots");
				if (num48 >= 0 && num42 != 0)
				{
					array[num48] = SecondCoat(img, num42);
				}
				if (num45 != 0)
				{
					int num49 = model.SlotIndex("nose");
					if (num49 >= 0)
					{
						array[num49] = TerrainCache.Lerp(num45, TerrainCache.Rgb(150, 90, 90), 0.3f) | 0xFF000000u;
					}
				}
				return array;
			}
			}
		}

		private static bool Near(uint a, uint b)
		{
			if (Math.Abs((int)((a & 0xFF) - (b & 0xFF))) + Math.Abs((int)(((a >> 8) & 0xFF) - ((b >> 8) & 0xFF))) + Math.Abs((int)(((a >> 16) & 0xFF) - ((b >> 16) & 0xFF))) <= 80)
			{
				return true;
			}
			if (NaturePalette.Saturation(a | 0xFF000000u) < 0.25f || NaturePalette.Saturation(b | 0xFF000000u) < 0.25f || HueDistance(a | 0xFF000000u, b | 0xFF000000u) > 0.04f)
			{
				return false;
			}
			int val = Luma(a | 0xFF000000u);
			int val2 = Luma(b | 0xFF000000u);
			return (float)Math.Max(val, val2) <= 2.2f * (float)Math.Min(val, val2) + 1f;
		}

		private static uint SecondCoat(SpriteImage img, uint fur)
		{
			List<(uint, int)> list = new List<(uint, int)>();
			int num = 0;
			uint[] pixels = img.Pixels;
			foreach (uint num2 in pixels)
			{
				if (num2 >> 24 >= 128)
				{
					num++;
					uint c = num2 | 0xFF000000u;
					int num3 = list.FindIndex(((uint color, int n) gr) => Near(gr.color, c));
					if (num3 < 0)
					{
						list.Add((c, 1));
					}
					else
					{
						list[num3] = (list[num3].Item1, list[num3].Item2 + 1);
					}
				}
			}
			uint num4 = 0u;
			int num5 = 0;
			foreach (var item in list)
			{
				if (!Near(item.Item1, fur) && Math.Abs(Luma(item.Item1) - Luma(fur)) >= 200 && item.Item2 > num5 && (NaturePalette.Saturation(item.Item1) < 0.4f || HueDistance(item.Item1, fur) < 0.06f))
				{
					num5 = item.Item2;
					(num4, _) = item;
				}
			}
			if (num4 == 0 || num5 * 100 < num * 8)
			{
				return TerrainCache.Scale(fur, 0.8f) | 0xFF000000u;
			}
			return num4;
		}

		private static uint DarkShade(SpriteImage img, uint fur)
		{
			Dictionary<uint, int> dictionary = new Dictionary<uint, int>();
			int num = 0;
			uint[] pixels = img.Pixels;
			foreach (uint num2 in pixels)
			{
				if (num2 >> 24 >= 128)
				{
					num++;
					uint key = num2 & 0xF0F0F0;
					dictionary.TryGetValue(key, out var value);
					dictionary[key] = value + 1;
				}
			}
			List<uint> list = new List<uint>();
			pixels = img.Pixels;
			foreach (uint num3 in pixels)
			{
				if (num3 >> 24 >= 128)
				{
					uint num4 = num3 | 0xFF000000u;
					float num5 = NaturePalette.Saturation(num4);
					if (dictionary[num3 & 0xF0F0F0] * 100 >= num * 8 || !(num5 >= 0.3f) || (!(HueDistance(num4, fur) >= 0.06f) && !(NaturePalette.Saturation(fur) < 0.15f) && !(num5 >= NaturePalette.Saturation(fur) + 0.25f)))
					{
						list.Add(num4);
					}
				}
			}
			uint result = TerrainCache.Scale(fur, 0.72f) | 0xFF000000u;
			if (list.Count == 0)
			{
				return result;
			}
			list.Sort((uint a, uint b) => Luma(a).CompareTo(Luma(b)));
			uint num6 = list[list.Count * 12 / 100];
			if (!Near(num6, fur) && Luma(num6) < Luma(fur) - 60)
			{
				return num6;
			}
			return result;
		}

		private static bool Mapped(string slot)
		{
			switch (slot)
			{
			default:
				return slot == "horn";
			case "fur":
			case "fur_light":
			case "dark":
			case "shell":
				return true;
			}
		}

		private static int Nearest(SpriteImage img, int x, int y, int reach)
		{
			if (x < img.Width && y < img.Height && img.Pixels[y * img.Width + x] >> 24 >= 128)
			{
				return y * img.Width + x;
			}
			int result = -1;
			int num = int.MaxValue;
			for (int i = -reach; i <= reach; i++)
			{
				for (int j = -reach; j <= reach; j++)
				{
					int num2 = x + j;
					int num3 = y + i;
					if (num2 >= 0 && num3 >= 0 && num2 < img.Width && num3 < img.Height && img.Pixels[num3 * img.Width + num2] >> 24 >= 128)
					{
						int num4 = j * j + i * i;
						if (num4 < num)
						{
							num = num4;
							result = num3 * img.Width + num2;
						}
					}
				}
			}
			return result;
		}

		internal static int Luma(uint c)
		{
			return (int)((c & 0xFF) * 3 + ((c >> 8) & 0xFF) * 6 + ((c >> 16) & 0xFF));
		}

		internal static float HueDistance(uint a, uint b)
		{
			float num = Math.Abs(NaturePalette.Hue(a) - NaturePalette.Hue(b));
			return Math.Min(num, 1f - num);
		}
	}
}
