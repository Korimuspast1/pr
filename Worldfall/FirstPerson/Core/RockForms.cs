using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class RockForms
	{
		public const string Styles = "wsbj";

		public const float RefHeight = 3f;

		public const float MaxWidth = 1.55f;

		public static string Look(string family, char style, int form)
		{
			if (style != 'w' || form != 1)
			{
				return family + "_" + style + form;
			}
			return family;
		}

		public static char StyleFor(string biome, bool byWater, bool high)
		{
			if (byWater)
			{
				return 's';
			}
			string text = biome ?? "";
			if (text.Contains("infernal") || text.Contains("corrupt") || text.Contains("singularity"))
			{
				return 'j';
			}
			if (text.Contains("swamp") || text.Contains("jungle") || text.Contains("candy") || text.Contains("mushroom"))
			{
				return 's';
			}
			if (high || text.Contains("rockland") || text.Contains("desert") || text.Contains("permafrost") || text.Contains("snow") || text.Contains("wasteland"))
			{
				return 'b';
			}
			return 'w';
		}

		public static int FormFor(int tx, int ty)
		{
			int num = (int)((TerrainModel.Hash(tx * 13 + 5, ty * 7 + 11) >> 8) % 100);
			if (num >= 35)
			{
				if (num >= 60)
				{
					if (num >= 85)
					{
						return 4;
					}
					return 3;
				}
				return 2;
			}
			return 1;
		}

		public static string Pick(string family, char style, int tx, int ty, Func<string, bool> has)
		{
			int form = FormFor(tx, ty);
			string text = Look(family, style, form);
			if (has(text))
			{
				return text;
			}
			text = Look(family, 'w', form);
			if (!has(text))
			{
				return family;
			}
			return text;
		}

		public static List<string> Looks(string family, Func<string, bool> has)
		{
			List<string> list = new List<string>();
			string text = "wsbj";
			foreach (char style in text)
			{
				for (int j = 1; j <= 4; j++)
				{
					string text2 = Look(family, style, j);
					if (has(text2))
					{
						list.Add(text2);
					}
				}
			}
			if (list.Count == 0)
			{
				list.Add(family);
			}
			return list;
		}

		public static char StyleOf(string look)
		{
			if (look == null)
			{
				return 'w';
			}
			int num = look.Length;
			if (look.EndsWith("_m", StringComparison.Ordinal) || look.EndsWith("_f", StringComparison.Ordinal))
			{
				num -= 2;
			}
			if (num <= 3 || look[num - 3] != '_' || "wsbj".IndexOf(look[num - 2]) < 0 || look[num - 1] < '1' || look[num - 1] > '4')
			{
				return 'w';
			}
			return look[num - 2];
		}

		public static int FormOf(string look)
		{
			if (look == null || look.Length < 4)
			{
				return 1;
			}
			int num = look.Length;
			if (look.EndsWith("_m", StringComparison.Ordinal) || look.EndsWith("_f", StringComparison.Ordinal))
			{
				num -= 2;
			}
			char c = look[num - 1];
			char value = look[num - 2];
			if (look[num - 3] != '_' || c < '1' || c > '4' || "wsbj".IndexOf(value) < 0)
			{
				return 1;
			}
			return c - 48;
		}

		public static float Size(string look, int tx, int ty)
		{
			float num = (float)Math.Pow((float)((TerrainModel.Hash(tx * 31 + 7, ty * 17 + 3) >> 12) & 0x3FF) / 1023f, 1.8);
			switch (FormOf(look))
			{
			case 2:
				return 0.65f + 0.8f * num;
			case 3:
				return 0.75f + 0.55f * num;
			case 4:
				return 0.65f + 0.45f * num;
			default:
				return 0.55f + 1.05f * num;
			}
		}

		public static float Scale(MeshModel model, float visible, float lumpHeight, string look, int tx, int ty)
		{
			float num = model.InfoFloat("scale_ref", 0f);
			if (num <= 0f)
			{
				return visible * lumpHeight / Math.Max(0.3f, model.Max.Z) * (0.9f + 0.2f * (float)((TerrainModel.Hash(tx, ty) >> 12) & 0xFF) / 255f);
			}
			float num2 = visible * lumpHeight / num * Size(look, tx, ty);
			float num3 = Math.Max(model.Max.X - model.Min.X, model.Max.Y - model.Min.Y);
			if (!(num3 * num2 > 1.55f))
			{
				return num2;
			}
			return 1.55f / num3;
		}
	}
}
