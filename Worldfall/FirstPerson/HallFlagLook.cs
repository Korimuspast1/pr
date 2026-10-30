using System;
using System.Collections.Generic;
using System.Globalization;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class HallFlagLook
	{
		private sealed class Entry
		{
			public Kingdom K;

			public FlagLook Look;

			public int Frame;

			public int Version;
		}

		public const string Key = "fp_hallflag";

		private static readonly uint Grime = TerrainCache.Rgb(128, 120, 110);

		private static readonly Dictionary<long, Entry> Looks = new Dictionary<long, Entry>();

		private static readonly Dictionary<long, FlagLook> Saved = new Dictionary<long, FlagLook>();

		internal static int Written;

		internal static int WrittenWhileLoading;

		public static void Reset()
		{
			Looks.Clear();
			Saved.Clear();
		}

		public static bool Of(Kingdom k, SpriteCache sprites, out FlagLook look)
		{
			look = default(FlagLook);
			if (k == null || WorldBoxMod.WorldLoading)
			{
				return false;
			}
			long id;
			try
			{
				if (!k.isCiv())
				{
					return false;
				}
				id = k.data.id;
			}
			catch
			{
				return false;
			}
			int frameCount = Time.frameCount;
			if (!Looks.TryGetValue(id, out var value) || value.K != k)
			{
				if (value == null)
				{
					if (Looks.Count > 512)
					{
						Looks.Clear();
					}
					value = (Looks[id] = new Entry());
				}
				value.K = k;
				value.Frame = -100000;
			}
			if (frameCount - value.Frame >= 60 || value.Version != Regalia.Version)
			{
				FlagLook look2 = value.Look;
				value.Look = Read(k, id);
				if (look2.GreysKnown && look2.Set == value.Look.Set && look2.Bg == value.Look.Bg && look2.Icon == value.Look.Icon)
				{
					value.Look.GreyField = look2.GreyField;
					value.Look.GreyDark = look2.GreyDark;
					value.Look.GreyEmblem = look2.GreyEmblem;
					value.Look.GreysKnown = true;
				}
				value.Frame = frameCount;
				value.Version = Regalia.Version;
			}
			if (!value.Look.GreysKnown && sprites != null)
			{
				FillGreys(ref value.Look, sprites);
			}
			look = value.Look;
			return look.Known;
		}

		private static FlagLook Read(Kingdom k, long id)
		{
			FlagLook result = new FlagLook
			{
				KingdomId = id,
				Style = -1,
				Known = true,
				GreyField = byte.MaxValue,
				GreyDark = 200,
				GreyEmblem = byte.MaxValue
			};
			try
			{
				result.Set = k.getActorAsset()?.banner_id;
			}
			catch
			{
			}
			try
			{
				result.Bg = k.data.banner_background_id;
				result.Icon = k.data.banner_icon_id;
			}
			catch
			{
			}
			result.Main2 = (result.Banner = TerrainCache.Rgb(255, 255, 255));
			try
			{
				ColorAsset color = k.getColor();
				if (color != null)
				{
					result.Main2 = Pack(color.getColorMainSecond());
					result.Banner = Pack(color.getColorBanner());
				}
			}
			catch
			{
			}
			KingdomLook kingdomLook = Regalia.LookOf(k);
			if (kingdomLook.Valid && kingdomLook.Styled && kingdomLook.Style != KingdomStyle.None)
			{
				result.Style = (sbyte)kingdomLook.Style;
			}
			result.StyleSerial = kingdomLook.StyleSerial;
			return result;
		}

		private static uint Pack(Color c)
		{
			return TerrainCache.Rgb(Mathf.Clamp((int)(c.r * 255f + 0.5f), 0, 255), Mathf.Clamp((int)(c.g * 255f + 0.5f), 0, 255), Mathf.Clamp((int)(c.b * 255f + 0.5f), 0, 255));
		}

		public static void FillGreys(ref FlagLook l, SpriteCache sprites)
		{
			if (!l.GreysKnown && l.Known && HallFlagModels.Greys(l.Set, l.Bg, l.Icon, sprites, out var field, out var dark, out var emblem))
			{
				l.GreyField = field;
				l.GreyDark = dark;
				l.GreyEmblem = emblem;
				l.GreysKnown = true;
			}
		}

		public static bool Same(in FlagLook a, in FlagLook b)
		{
			if (a.Known == b.Known && a.KingdomId == b.KingdomId && a.Bg == b.Bg && a.Icon == b.Icon && a.Main2 == b.Main2 && a.Banner == b.Banner && a.Style == b.Style && a.StyleSerial == b.StyleSerial)
			{
				return string.Equals(a.Set, b.Set);
			}
			return false;
		}

		public static bool SamePaint(in FlagLook a, in FlagLook b)
		{
			if (Same(in a, in b) && a.GreyField == b.GreyField && a.GreyDark == b.GreyDark)
			{
				return a.GreyEmblem == b.GreyEmblem;
			}
			return false;
		}

		public static void Paint(uint[] pal, MeshModel m, in FlagLook l, bool faded)
		{
			if (pal != null)
			{
				Array.Clear(pal, 0, pal.Length);
				if (m != null && pal.Length >= 3 && m.SlotColor != null && m.SlotColor.Length >= 3)
				{
					bool num = m.SlotColor.Length > 3;
					int num2 = (int)(num ? (m.SlotColor[0] & 0xFF) : l.GreyField);
					int num3 = (int)(num ? (m.SlotColor[1] & 0xFF) : l.GreyDark);
					int num4 = (int)(num ? (m.SlotColor[2] & 0xFF) : l.GreyEmblem);
					pal[0] = Faded(TerrainCache.Scale(l.Main2, (float)num2 / 255f), faded);
					pal[1] = Faded(TerrainCache.Scale(l.Main2, (float)num3 / 255f), faded);
					pal[2] = Faded(TerrainCache.Scale(l.Banner, (float)num4 / 255f), faded);
				}
			}
		}

		public static void PaintPlain(uint[] pal, uint colour, bool faded)
		{
			if (pal != null)
			{
				Array.Clear(pal, 0, pal.Length);
				if (pal.Length >= 3)
				{
					pal[0] = Faded(colour, faded);
					pal[1] = Faded(TerrainCache.Scale(colour, 40f / 51f), faded);
					pal[2] = pal[0];
				}
			}
		}

		private static uint Faded(uint c, bool faded)
		{
			if (!faded)
			{
				return c;
			}
			int num = (int)(c & 0xFF);
			int num2 = (int)((c >> 8) & 0xFF);
			int num3 = (int)((c >> 16) & 0xFF);
			int num4 = Math.Max(num, Math.Max(num2, num3));
			return TerrainCache.Scale(TerrainCache.Lerp(TerrainCache.Rgb((num4 + num) / 2, (num4 + num2) / 2, (num4 + num3) / 2), Grime, 0.15f), 0.72f);
		}

		public static void Remember(Building b, in FlagLook l)
		{
			if (b == null || !l.Known)
			{
				return;
			}
			if (WorldBoxMod.WorldLoading)
			{
				WrittenWhileLoading++;
				return;
			}
			long id;
			try
			{
				id = b.data.id;
			}
			catch
			{
				return;
			}
			if (!Saved.TryGetValue(id, out var value))
			{
				Recall(b, out value);
			}
			if (!value.Known || value.KingdomId != l.KingdomId || value.Bg != l.Bg || value.Icon != l.Icon || value.Main2 != l.Main2 || value.Banner != l.Banner || value.Style != l.Style || !string.Equals(value.Set, l.Set))
			{
				string[] obj2 = new string[14]
				{
					"1|",
					l.KingdomId.ToString(CultureInfo.InvariantCulture),
					"|",
					l.Set,
					"|",
					l.Bg.ToString(CultureInfo.InvariantCulture),
					"|",
					l.Icon.ToString(CultureInfo.InvariantCulture),
					"|",
					Hex(l.Main2),
					"|",
					Hex(l.Banner),
					"|",
					null
				};
				int style = l.Style;
				obj2[13] = style.ToString(CultureInfo.InvariantCulture);
				string pData = string.Concat(obj2);
				try
				{
					b.data.set("fp_hallflag", pData);
				}
				catch (Exception ex)
				{
					Log.Error("hall flags: remember: " + ex.Message);
					return;
				}
				FlagLook value2 = l;
				value2.StyleSerial = 0;
				Saved[id] = value2;
				Written++;
			}
		}

		public static bool Recall(Building b, out FlagLook l)
		{
			l = default(FlagLook);
			l.Style = -1;
			if (b == null || WorldBoxMod.WorldLoading)
			{
				return false;
			}
			string pResult = null;
			long id;
			try
			{
				id = b.data.id;
				b.data.get("fp_hallflag", out pResult, null);
			}
			catch
			{
				return false;
			}
			if (!string.IsNullOrEmpty(pResult))
			{
				string[] array = pResult.Split('|');
				if (array.Length >= 8 && array[0] == "1" && long.TryParse(array[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) && int.TryParse(array[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result2) && int.TryParse(array[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result3) && uint.TryParse(array[5], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result4) && uint.TryParse(array[6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result5) && int.TryParse(array[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result6))
				{
					l = new FlagLook
					{
						KingdomId = result,
						Set = ((array[2].Length > 0) ? array[2] : null),
						Bg = result2,
						Icon = result3,
						Main2 = FromHex(result4),
						Banner = FromHex(result5),
						Style = (sbyte)((result6 >= 0 && result6 <= 2) ? result6 : (-1)),
						Known = true,
						GreyField = byte.MaxValue,
						GreyDark = 200,
						GreyEmblem = byte.MaxValue
					};
				}
			}
			if (Saved.Count > 4096)
			{
				Saved.Clear();
			}
			Saved[id] = l;
			return l.Known;
		}

		private static string Hex(uint c)
		{
			return (((c & 0xFF) << 16) | (((c >> 8) & 0xFF) << 8) | ((c >> 16) & 0xFF)).ToString("x6", CultureInfo.InvariantCulture);
		}

		private static uint FromHex(uint rrggbb)
		{
			return TerrainCache.Rgb((int)((rrggbb >> 16) & 0xFF), (int)((rrggbb >> 8) & 0xFF), (int)(rrggbb & 0xFF));
		}

		public static void Watch(City c)
		{
			if (c == null || WorldBoxMod.WorldLoading)
			{
				return;
			}
			try
			{
				if (c.buildings_dict_type == null || !c.buildings_dict_type.TryGetValue("type_hall", out var value) || value == null)
				{
					return;
				}
				for (int i = 0; i < value.Count; i++)
				{
					Building building = value[i];
					if (building != null && building.isAlive() && !building.isRuin() && Of(building.kingdom, null, out var look))
					{
						Remember(building, in look);
					}
				}
			}
			catch
			{
			}
		}
	}
}
