using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using FirstPerson.Core;

namespace FirstPerson
{
	internal static class PackCaches
	{
		private static readonly string[][] Named = new string[5][]
		{
			new string[2] { "FirstPerson.Creatures", "States" },
			new string[2] { "FirstPerson.People", "Figures" },
			new string[2] { "FirstPerson.Models", "WomenOf" },
			new string[2] { "FirstPerson.Models", "ArmStyles" },
			new string[2] { "FirstPerson.Models", "ZombieFamilies" }
		};

		private static List<FieldInfo> _fields;

		private static readonly List<string> _missing = new List<string>();

		public static List<string> Found
		{
			get
			{
				Scan();
				return _fields.ConvertAll((FieldInfo f) => f.DeclaringType.Name + "." + f.Name);
			}
		}

		public static List<string> Missing
		{
			get
			{
				Scan();
				return _missing;
			}
		}

		private static void Scan()
		{
			if (_fields != null)
			{
				return;
			}
			_fields = new List<FieldInfo>();
			Type[] array;
			try
			{
				array = typeof(PackCaches).Assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException ex)
			{
				array = Array.FindAll(ex.Types, (Type t) => t != null);
			}
			Type[] array2 = array;
			foreach (Type type in array2)
			{
				if (type.ContainsGenericParameters)
				{
					continue;
				}
				FieldInfo[] fields = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (FieldInfo fieldInfo in fields)
				{
					if (fieldInfo.IsInitOnly && IsTable(fieldInfo.FieldType) && MentionsModel(fieldInfo.FieldType))
					{
						_fields.Add(fieldInfo);
					}
				}
			}
			string[][] named = Named;
			foreach (string[] array3 in named)
			{
				FieldInfo fieldInfo2 = typeof(PackCaches).Assembly.GetType(array3[0])?.GetField(array3[1], BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (fieldInfo2 == null || !IsTable(fieldInfo2.FieldType))
				{
					_missing.Add(array3[0] + "." + array3[1]);
				}
				else if (!_fields.Contains(fieldInfo2))
				{
					_fields.Add(fieldInfo2);
				}
			}
			if (_missing.Count > 0)
			{
				Log.Error("packs: cache tables not found: " + string.Join(", ", _missing.ToArray()));
			}
		}

		private static bool IsTable(Type t)
		{
			if (!t.IsGenericType)
			{
				return false;
			}
			Type genericTypeDefinition = t.GetGenericTypeDefinition();
			if (!(genericTypeDefinition == typeof(Dictionary<, >)))
			{
				return genericTypeDefinition == typeof(HashSet<>);
			}
			return true;
		}

		private static bool MentionsModel(Type t, int depth = 0)
		{
			if (t == typeof(MeshModel))
			{
				return true;
			}
			if (depth > 3)
			{
				return false;
			}
			if (t.IsArray)
			{
				return MentionsModel(t.GetElementType(), depth + 1);
			}
			if (t.IsGenericType)
			{
				Type[] genericArguments = t.GetGenericArguments();
				for (int i = 0; i < genericArguments.Length; i++)
				{
					if (MentionsModel(genericArguments[i], depth + 1))
					{
						return true;
					}
				}
			}
			return false;
		}

		public static int Clear(params object[] owners)
		{
			Scan();
			int num = 0;
			foreach (FieldInfo field in _fields)
			{
				if (ClearOne(field.GetValue(null), field.DeclaringType))
				{
					num++;
				}
			}
			foreach (object obj in owners)
			{
				if (obj == null)
				{
					continue;
				}
				FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (FieldInfo fieldInfo in fields)
				{
					if (fieldInfo.IsInitOnly && IsTable(fieldInfo.FieldType) && MentionsModel(fieldInfo.FieldType) && ClearOne(fieldInfo.GetValue(obj), obj.GetType()))
					{
						num++;
					}
				}
			}
			return num;
		}

		private static bool ClearOne(object table, Type owner)
		{
			if (table == null)
			{
				return false;
			}
			object obj = GateOf(owner);
			bool flag = false;
			bool flag2 = false;
			try
			{
				if (obj != null && !(flag2 = Monitor.TryEnter(obj, 250)))
				{
					Log.Info("packs: " + owner.Name + " busy, its table kept");
					return false;
				}
				if (!(flag = Monitor.TryEnter(table, 250)))
				{
					Log.Info("packs: a table of " + owner.Name + " busy, kept");
					return false;
				}
				table.GetType().GetMethod("Clear", Type.EmptyTypes)?.Invoke(table, null);
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("packs: could not clear a table of " + owner.Name + ": " + ex.Message);
				return false;
			}
			finally
			{
				if (flag)
				{
					Monitor.Exit(table);
				}
				if (flag2)
				{
					Monitor.Exit(obj);
				}
			}
		}

		private static object GateOf(Type t)
		{
			string[] array = new string[2] { "Gate", "Lock" };
			foreach (string name in array)
			{
				FieldInfo field = t.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (field != null && field.FieldType == typeof(object))
				{
					return field.GetValue(null);
				}
			}
			return null;
		}
	}
}
