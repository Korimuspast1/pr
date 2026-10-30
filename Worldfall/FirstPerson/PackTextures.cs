using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class PackTextures
	{
		public const int MaxSide = 2048;

		public const int CrispUpTo = 64;

		public static string Describe(byte[] d, out int w, out int h)
		{
			w = (h = 0);
			if (d == null || d.Length < 24)
			{
				return null;
			}
			if (d[0] == 137 && d[1] == 80 && d[2] == 78 && d[3] == 71 && d[12] == 73 && d[13] == 72 && d[14] == 68 && d[15] == 82)
			{
				w = (d[16] << 24) | (d[17] << 16) | (d[18] << 8) | d[19];
				h = (d[20] << 24) | (d[21] << 16) | (d[22] << 8) | d[23];
				return "PNG";
			}
			if (d[0] == byte.MaxValue && d[1] == 216)
			{
				int num = 2;
				while (num + 9 < d.Length)
				{
					if (d[num] != byte.MaxValue)
					{
						num++;
						continue;
					}
					int num2 = d[num + 1];
					switch (num2)
					{
					case 255:
						num++;
						continue;
					case 192:
					case 193:
					case 194:
					case 195:
					case 197:
					case 198:
					case 199:
					case 201:
					case 202:
					case 203:
					case 205:
					case 206:
					case 207:
						h = (d[num + 5] << 8) | d[num + 6];
						w = (d[num + 7] << 8) | d[num + 8];
						return "JPEG";
					}
					if (num2 == 216 || (num2 >= 208 && num2 <= 215) || num2 == 1)
					{
						num += 2;
						continue;
					}
					int num3 = (d[num + 2] << 8) | d[num + 3];
					if (num3 < 2)
					{
						break;
					}
					num += 2 + num3;
				}
				return "JPEG";
			}
			return null;
		}

		public static void Note(string file, MeshModel m, List<string> problems, List<string> warnings)
		{
			if (m?.Textures == null)
			{
				return;
			}
			MeshTexture[] textures = m.Textures;
			foreach (MeshTexture meshTexture in textures)
			{
				if (meshTexture.Source != null)
				{
					continue;
				}
				if (meshTexture.Encoded == null)
				{
					problems.Add(file + ": texture '" + meshTexture.Name + "' " + ((meshTexture.Uri != null) ? ("links to " + meshTexture.Uri + ", which isn't in the pack") : "has no data") + " (drawn flat)");
					meshTexture.Problem = "missing";
					continue;
				}
				int w;
				int h;
				string text = Describe(meshTexture.Encoded, out w, out h);
				if (text == null)
				{
					problems.Add(file + ": texture '" + meshTexture.Name + "' is not a PNG or JPEG" + ((meshTexture.MimeType != null) ? (" (" + meshTexture.MimeType + ")") : "") + ": in Blender's glTF export set Data > Images to PNG (drawn flat)");
					meshTexture.Problem = "format";
					continue;
				}
				string text2 = ((w > 0) ? (" " + w + "x" + h) : "");
				if (Math.Max(w, h) > 2048)
				{
					warnings.Add(file + ": texture '" + meshTexture.Name + "'" + text2 + " " + text + " is drawn, shrunk to " + 2048 + " px");
				}
				else
				{
					warnings.Add(file + ": texture '" + meshTexture.Name + "'" + text2 + " " + text + " is drawn" + ((Math.Max(w, h) <= 64 && meshTexture.Filter == null) ? ", crisp" : ""));
				}
			}
		}

		public static void Decode(MeshModel m)
		{
			if (m?.Textures == null)
			{
				return;
			}
			MeshTexture[] textures = m.Textures;
			foreach (MeshTexture meshTexture in textures)
			{
				if (meshTexture.Source == null)
				{
					DecodeOne(meshTexture);
				}
			}
			textures = m.Textures;
			foreach (MeshTexture meshTexture2 in textures)
			{
				if (meshTexture2.Source == null || meshTexture2.Pixels != null)
				{
					continue;
				}
				MeshTexture source = meshTexture2.Source;
				if (source.Pixels == null)
				{
					meshTexture2.Problem = source.Problem;
					meshTexture2.Encoded = null;
					continue;
				}
				uint[] array = new uint[source.Pixels.Length];
				for (int j = 0; j < array.Length; j++)
				{
					array[j] = (TerrainCache.Multiply(source.Pixels[j], meshTexture2.Factor) & 0xFFFFFF) | (source.Pixels[j] & 0xFF000000u);
				}
				meshTexture2.Pixels = array;
				meshTexture2.Width = source.Width;
				meshTexture2.Height = source.Height;
				meshTexture2.Crisp = source.Crisp;
				meshTexture2.Cutout = source.Cutout;
				meshTexture2.Encoded = null;
			}
		}

		public static void Decode(IEnumerable<MeshModel> models)
		{
			if (models == null)
			{
				return;
			}
			foreach (MeshModel model in models)
			{
				if (model != null && model.Textures != null)
				{
					Decode(model);
				}
			}
		}

		private static void DecodeOne(MeshTexture t)
		{
			if (t.Pixels != null || t.Encoded == null || t.Problem != null)
			{
				return;
			}
			Texture2D texture2D = null;
			try
			{
				if (Describe(t.Encoded, out var _, out var _) == null)
				{
					t.Problem = "format";
					return;
				}
				texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: true);
				if (!texture2D.LoadImage(t.Encoded, markNonReadable: false))
				{
					t.Problem = "unreadable";
					return;
				}
				int num = texture2D.width;
				int num2 = texture2D.height;
				Color32[] pixels = texture2D.GetPixels32();
				uint[] array = new uint[num * num2];
				bool cutout = false;
				for (int i = 0; i < num2; i++)
				{
					int num3 = (num2 - 1 - i) * num;
					int num4 = i * num;
					for (int j = 0; j < num; j++)
					{
						Color32 color = pixels[num3 + j];
						if (color.a < 128)
						{
							cutout = true;
						}
						array[num4 + j] = (uint)(color.r | (color.g << 8) | (color.b << 16) | (color.a << 24));
					}
				}
				while (Math.Max(num, num2) > 2048)
				{
					array = Halved(array, num, num2);
					num = (num + 1) / 2;
					num2 = (num2 + 1) / 2;
				}
				t.Pixels = array;
				t.Width = num;
				t.Height = num2;
				t.Cutout = cutout;
				t.Crisp = t.Filter == "crisp" || (t.Filter != "smooth" && Math.Max(num, num2) <= 64);
			}
			catch (Exception ex)
			{
				t.Problem = ex.Message;
			}
			finally
			{
				if (texture2D != null)
				{
					UnityEngine.Object.Destroy(texture2D);
				}
				t.Encoded = null;
			}
		}

		private static uint[] Halved(uint[] px, int w, int h)
		{
			int num = (w + 1) / 2;
			int num2 = (h + 1) / 2;
			uint[] array = new uint[num * num2];
			for (int i = 0; i < num2; i++)
			{
				for (int j = 0; j < num; j++)
				{
					int num3 = j * 2;
					int num4 = i * 2;
					int num5 = Math.Min(num3 + 1, w - 1);
					int num6 = Math.Min(num4 + 1, h - 1);
					uint num7 = px[num4 * w + num3];
					uint num8 = px[num4 * w + num5];
					uint num9 = px[num6 * w + num3];
					uint num10 = px[num6 * w + num5];
					uint num11 = 0u;
					for (int k = 0; k < 32; k += 8)
					{
						num11 |= ((num7 >> k) & 0xFF) + ((num8 >> k) & 0xFF) + ((num9 >> k) & 0xFF) + ((num10 >> k) & 0xFF) + 2 >> 2 << k;
					}
					array[i * num + j] = num11;
				}
			}
			return array;
		}
	}
}
