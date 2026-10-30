using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using FirstPerson.Core;

namespace FirstPerson
{
	internal static class LightMode
	{
		private sealed class Entry
		{
			public FieldInfo Field;

			public Func<object, object> Light;
		}

		private static readonly List<Entry> Entries;

		private static readonly Dictionary<FieldInfo, object> _own;

		private static readonly Dictionary<FieldInfo, object> _light;

		private static Settings _settings;

		private static bool _extrasOn;

		public static bool On { get; private set; }

		public static bool NoRoomShadows => On;

		private static void Add(string field, Func<object, object> light)
		{
			FieldInfo field2 = typeof(Settings).GetField(field, BindingFlags.Instance | BindingFlags.Public);
			if (field2 == null)
			{
				Log.Error("light mode: no setting " + field);
				return;
			}
			Entries.Add(new Entry
			{
				Field = field2,
				Light = light
			});
		}

		private static Func<object, object> Fixed(object v)
		{
			return (object _) => v;
		}

		private static Func<object, object> AtMost(float v)
		{
			return (object o) => Math.Min((float)o, v);
		}

		static LightMode()
		{
			Entries = new List<Entry>();
			_own = new Dictionary<FieldInfo, object>();
			_light = new Dictionary<FieldInfo, object>();
			Add("Resolution", Fixed(0));
			Add("GpuLook", Fixed(0));
			Add("Shadows", Fixed(0));
			Add("DarkFantasy", Fixed(0));
			Add("PixelDistance", Fixed(0));
			Add("WindowView", Fixed(0));
			Add("DrawDistance", AtMost(140f));
			Add("DetailDistance", AtMost(40f));
			Add("CreatureDistance", AtMost(80f));
			Add("FarModels", Fixed(0));
			Add("OffscreenMotion", Fixed(1));
			Add("FarPoses", Fixed(1));
			Add("LensFlare", Fixed(false));
			Add("BiomeFx", Fixed(0));
			Add("BiomeParticles", Fixed(0f));
			Add("WindGusts", Fixed(false));
			Add("SpatialSound", Fixed(false));
		}

		public static bool Wanted(Settings s)
		{
			if (s != null)
			{
				if (s.Light != 1)
				{
					if (s.Light == 0)
					{
						return Device.OnPhone;
					}
					return false;
				}
				return true;
			}
			return false;
		}

		public static void Tick(WorldBoxMod mod)
		{
			Settings settings = mod.Settings;
			if (settings != null)
			{
				if (settings != _settings)
				{
					_own.Clear();
					_light.Clear();
					On = false;
					_settings = settings;
				}
				bool flag = Wanted(settings);
				if (flag && !On)
				{
					Apply(settings);
				}
				else if (!flag && On)
				{
					Restore(settings);
				}
				else if (On)
				{
					KeepChoices(settings);
				}
				Extras(mod, On);
			}
		}

		private static void Apply(Settings s)
		{
			_own.Clear();
			_light.Clear();
			StringBuilder stringBuilder = new StringBuilder();
			foreach (Entry entry in Entries)
			{
				object value = entry.Field.GetValue(s);
				object obj = entry.Light(value);
				_own[entry.Field] = value;
				_light[entry.Field] = obj;
				if (!object.Equals(value, obj))
				{
					entry.Field.SetValue(s, obj);
					stringBuilder.Append(' ').Append(entry.Field.Name).Append(' ')
						.Append(value)
						.Append("->")
						.Append(obj);
				}
			}
			On = true;
			Log.Info("light mode on (" + ((s.Light == 0) ? ("auto, " + Device.Is) : "chosen") + "):" + stringBuilder);
		}

		private static void Restore(Settings s)
		{
			foreach (KeyValuePair<FieldInfo, object> item in _own)
			{
				item.Key.SetValue(s, item.Value);
			}
			_own.Clear();
			_light.Clear();
			On = false;
			Log.Info("light mode off: the player's own settings back");
		}

		private static void KeepChoices(Settings s)
		{
			List<FieldInfo> list = null;
			foreach (KeyValuePair<FieldInfo, object> item in _light)
			{
				if (!object.Equals(item.Key.GetValue(s), item.Value))
				{
					(list ?? (list = new List<FieldInfo>())).Add(item.Key);
				}
			}
			if (list == null)
			{
				return;
			}
			foreach (FieldInfo item2 in list)
			{
				_light.Remove(item2);
				_own.Remove(item2);
				Log.Info("light mode: " + item2.Name + " set by the player (" + item2.GetValue(s)?.ToString() + "), kept");
			}
		}

		internal static void Swap(Settings s, bool light)
		{
			if (!On || s != _settings)
			{
				return;
			}
			if (!light)
			{
				KeepChoices(s);
			}
			foreach (KeyValuePair<FieldInfo, object> item in light ? _light : _own)
			{
				item.Key.SetValue(s, item.Value);
			}
		}

		private static void Extras(WorldBoxMod mod, bool on)
		{
			if (on != _extrasOn)
			{
				_extrasOn = on;
				mod.Renderer.SeaWaves = !on;
				SnowCover.Hidden = on;
			}
		}
	}
}
