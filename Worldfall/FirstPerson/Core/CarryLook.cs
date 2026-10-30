using System;
using System.Collections.Generic;
using System.Globalization;

namespace FirstPerson.Core
{
	public static class CarryLook
	{
		private static readonly Dictionary<string, Carried> Known = new Dictionary<string, Carried>();

		private static readonly Dictionary<string, Carried> Loads = new Dictionary<string, Carried>();

		private static readonly Dictionary<string, Carried> Items = new Dictionary<string, Carried>();

		private static readonly Dictionary<string, Carried> Tools = new Dictionary<string, Carried>();

		private static readonly Carried None = new Carried();

		private static Func<string, MeshModel> _library;

		private static Func<string, MeshModel> Library()
		{
			Func<string, MeshModel> library = ViewMeshes.Library;
			if (library != null && (object)library != _library)
			{
				Known.Clear();
				Loads.Clear();
				Items.Clear();
				Tools.Clear();
				_library = library;
			}
			return library;
		}

		public static Carried Of(string name)
		{
			Func<string, MeshModel> func = Library();
			if (name == null || func == null)
			{
				return null;
			}
			if (Known.TryGetValue(name, out var value))
			{
				return value;
			}
			MeshModel meshModel = func(name) ?? func(name + "_m") ?? func(name + "_f");
			if (meshModel != null)
			{
				value = Read(name, meshModel);
			}
			if (Known.Count > 512)
			{
				Known.Clear();
			}
			Known[name] = value;
			return value;
		}

		private static Carried By(Dictionary<string, Carried> cache, string key, string prefix, string second = null)
		{
			if (key == null || Library() == null)
			{
				return null;
			}
			if (cache.TryGetValue(key, out var value))
			{
				if (value != None)
				{
					return value;
				}
				return null;
			}
			value = Of(prefix + key) ?? ((second != null) ? Of(second + key) : null);
			if (cache.Count > 256)
			{
				cache.Clear();
			}
			cache[key] = value ?? None;
			return value;
		}

		public static Carried Load(string key)
		{
			return By(Loads, key, "load_", "item_");
		}

		public static Carried Item(string res)
		{
			return By(Items, res, "item_");
		}

		public static Carried Tool(string tool)
		{
			return By(Tools, tool, "tool_");
		}

		private static Carried Read(string name, MeshModel m)
		{
			Carried carried = new Carried
			{
				Name = name
			};
			string text = m.Info("kind");
			string text2 = m.Info("carry");
			carried.Hold = m.Info("hold");
			if (text == "load")
			{
				int style;
				switch (text2)
				{
				default:
					style = 1;
					break;
				case "sack":
					style = 4;
					break;
				case "shoulder":
					style = 3;
					break;
				case "hug":
					style = 2;
					break;
				}
				carried.Style = (CarryStyle)style;
			}
			else if (text == "tool")
			{
				carried.Style = ((carried.Hold == "hand") ? CarryStyle.Hand : CarryStyle.None);
			}
			else
			{
				carried.Style = CarryStyle.Hand;
			}
			carried.Palm = m.Info("grip") == "palm";
			string text3 = m.Info("eat");
			carried.Edible = text3 == "bite" || text3 == "drink";
			carried.Drink = text3 == "drink";
			carried.HandR = Vector(m.Info("hand_r"), new Vec3(0f, 0.1f, 0f));
			string text4 = m.Info("hand_l");
			carried.LeftHand = text4 != null;
			carried.HandL = Vector(text4, new Vec3(0f - carried.HandR.X, carried.HandR.Y, carried.HandR.Z));
			carried.Lean = Number(m.Info("lean"), 0f);
			carried.Mouth = Vector(m.Info("mouth"), new Vec3(0f, 0f, 0.05f));
			string text5 = m.Info("swing");
			carried.Swing = ((text5 == "axe") ? WeaponKind.Axe : ((text5 == "hammer") ? WeaponKind.Hammer : WeaponKind.None));
			return carried;
		}

		private static Vec3 Vector(string s, Vec3 fallback)
		{
			if (string.IsNullOrEmpty(s))
			{
				return fallback;
			}
			string[] array = s.Split(',');
			if (array.Length != 3)
			{
				return fallback;
			}
			if (!float.TryParse(array[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var result) || !float.TryParse(array[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2) || !float.TryParse(array[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var result3))
			{
				return fallback;
			}
			return new Vec3(result, result2, result3);
		}

		private static float Number(string s, float fallback)
		{
			if (string.IsNullOrEmpty(s) || !float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				return fallback;
			}
			return result;
		}
	}
}
