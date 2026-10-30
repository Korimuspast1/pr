using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FirstPerson
{
	internal static class PeopleMet
	{
		public const string Key = "fp_met";

		public const int Max = 120;

		public static Vector2 Scroll;

		public static int Sort;

		private static long _cachedFor = -1L;

		private static object _cachedWorld;

		private static int _renamed = -1;

		private static readonly List<Met> Cache = new List<Met>();

		private static readonly StringBuilder Text = new StringBuilder(512);

		private static float _familyCheck;

		private static long _familyFor = -1L;

		public static List<Met> Of(Actor you)
		{
			long num = -1L;
			try
			{
				num = you?.data.id ?? (-1);
			}
			catch
			{
			}
			object obj2 = null;
			try
			{
				obj2 = World.world?.map_stats;
			}
			catch
			{
			}
			if (num != _cachedFor || obj2 != _cachedWorld)
			{
				_cachedFor = num;
				_cachedWorld = obj2;
				Cache.Clear();
				if (num >= 0)
				{
					try
					{
						you.data.get("fp_met", out var pResult, null);
						if (!string.IsNullOrEmpty(pResult))
						{
							Parse(pResult, Cache);
						}
					}
					catch (Exception ex)
					{
						Log.Error("met: could not read: " + ex.Message);
					}
				}
				_renamed = -1;
			}
			int version = Renames.Version;
			if (version != _renamed)
			{
				_renamed = version;
				foreach (Met item in Cache)
				{
					item.Name = Renames.Apply(item.Name);
				}
			}
			return Cache;
		}

		public static void Note(Actor you, Actor npc)
		{
			Add(you, npc, talk: true);
		}

		private static void Add(Actor you, Actor npc, bool talk)
		{
			if (you == null || npc == null)
			{
				return;
			}
			List<Met> list = Of(you);
			long id;
			try
			{
				id = npc.data.id;
			}
			catch
			{
				return;
			}
			Met met = null;
			long num = 0L;
			foreach (Met item in list)
			{
				if (item.Id == id)
				{
					met = item;
				}
				num = Math.Max(num, item.Order);
			}
			if (met != null && !talk)
			{
				return;
			}
			int year = Chronicle.Year;
			if (met == null)
			{
				met = new Met
				{
					Id = id,
					FirstYear = year
				};
				list.Add(met);
			}
			met.Name = Dialogue.NameOf(npc);
			try
			{
				met.Kind = ((npc.asset != null) ? npc.asset.id : "");
			}
			catch
			{
				met.Kind = "";
			}
			met.Female = Models.IsFemale(npc);
			met.LastYear = year;
			if (talk)
			{
				met.Talks++;
			}
			met.Order = num + 1;
			if (list.Count > 120)
			{
				int index = 0;
				for (int i = 1; i < list.Count; i++)
				{
					if (list[i].Order < list[index].Order)
					{
						index = i;
					}
				}
				list.RemoveAt(index);
			}
			try
			{
				you.data.set("fp_met", Write(list));
			}
			catch (Exception ex)
			{
				Log.Error("met: could not write: " + ex.Message);
			}
		}

		public static List<Met> Sorted(Actor you)
		{
			List<Met> list = new List<Met>(Of(you));
			if (Sort == 1)
			{
				list.Sort((Met a, Met b) => (b.Talks == a.Talks) ? b.Order.CompareTo(a.Order) : b.Talks.CompareTo(a.Talks));
			}
			else if (Sort == 2)
			{
				list.Sort((Met a, Met b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
			}
			else
			{
				list.Sort((Met a, Met b) => b.Order.CompareTo(a.Order));
			}
			return list;
		}

		public static bool Knows(Actor you, Actor npc)
		{
			if (you == null || npc == null)
			{
				return false;
			}
			if (npc == you)
			{
				return true;
			}
			try
			{
				long id = npc.data.id;
				foreach (Met item in Of(you))
				{
					if (item.Id == id)
					{
						return true;
					}
				}
				if (npc.isKing() || (npc.isCityLeader() && npc.city != null && npc.city == you.city))
				{
					return true;
				}
				return IsKin(you, npc);
			}
			catch
			{
				return false;
			}
		}

		private static bool IsKin(Actor you, Actor npc)
		{
			if (npc.lover == you || you.lover == npc || npc.isParentOf(you) || you.isParentOf(npc))
			{
				return true;
			}
			long parent_id_ = you.data.parent_id_1;
			long parent_id_2 = you.data.parent_id_2;
			long parent_id_3 = npc.data.parent_id_1;
			long parent_id_4 = npc.data.parent_id_2;
			if ((parent_id_ > 0 && (parent_id_ == parent_id_3 || parent_id_ == parent_id_4)) || (parent_id_2 > 0 && (parent_id_2 == parent_id_3 || parent_id_2 == parent_id_4)))
			{
				return true;
			}
			Record record = Chronicle.Of(you);
			if (record != null)
			{
				foreach (Kin item in record.Kin)
				{
					if (item.Id == npc.data.id)
					{
						return true;
					}
				}
			}
			Record record2 = Chronicle.Of(npc);
			if (record2 != null)
			{
				foreach (Kin item2 in record2.Kin)
				{
					if (item2.Id == you.data.id)
					{
						return true;
					}
				}
			}
			return false;
		}

		public static string Label(Actor you, Actor a)
		{
			if (a == null)
			{
				return "someone";
			}
			if (!Dialogue.IsPerson(a) || Knows(you, a) || Finding.Heard(you, a))
			{
				return Dialogue.NameOf(a);
			}
			return Looks(a);
		}

		public static string Looks(Actor a)
		{
			string text = People.LookWords(a);
			if (text != null)
			{
				return text;
			}
			string text2 = "Person";
			try
			{
				if (a.asset != null)
				{
					text2 = a.asset.id.Replace('_', ' ');
				}
			}
			catch
			{
			}
			bool flag = Models.IsFemale(a);
			string text3 = People.AgeLabel(a);
			string text4 = ((!(text3 == "child")) ? (flag ? "woman" : "man") : (flag ? "girl" : "boy"));
			string text5 = Lang.Format((text3 == "elder") ? "old {0} {1}" : "{0} {1}", text2, text4);
			return char.ToUpperInvariant(text5[0]) + text5.Substring(1);
		}

		public static void KeepFamily(Actor you, float dt)
		{
			if (you == null)
			{
				return;
			}
			long id;
			try
			{
				id = you.data.id;
			}
			catch
			{
				return;
			}
			_familyCheck -= dt;
			if (_familyCheck > 0f && id == _familyFor)
			{
				return;
			}
			_familyCheck = 10f;
			_familyFor = id;
			try
			{
				FamilyTree familyTree = Dialogue.TreeOf(you);
				TreeNode[] parents = familyTree.Parents;
				for (int i = 0; i < parents.Length; i++)
				{
					Add(parents[i]);
				}
				parents = familyTree.Grandparents;
				for (int i = 0; i < parents.Length; i++)
				{
					Add(parents[i]);
				}
				foreach (TreeNode sibling in familyTree.Siblings)
				{
					Add(sibling);
				}
				if (familyTree.Partners.Count > 0 && you.lover != null && familyTree.Partners[0].Actor == you.lover)
				{
					Add(familyTree.Partners[0]);
				}
				foreach (TreeNode child in familyTree.Children)
				{
					Add(child);
				}
			}
			catch (Exception ex)
			{
				Log.Error("met: family: " + ex.Message);
			}
			void Add(TreeNode n)
			{
				if (n != null && !n.Dead && n.Actor != null && n.Actor != you)
				{
					try
					{
						if (!n.Actor.isAlive())
						{
							return;
						}
					}
					catch
					{
						return;
					}
					PeopleMet.Add(you, n.Actor, talk: false);
				}
			}
		}

		public static void Reset()
		{
			_cachedFor = -1L;
			_familyFor = -1L;
			Cache.Clear();
			Scroll = Vector2.zero;
		}

		private static string Clean(string s)
		{
			if (!string.IsNullOrEmpty(s))
			{
				return s.Replace(",", " ").Replace("|", " ");
			}
			return "";
		}

		private static string Write(List<Met> list)
		{
			Text.Length = 0;
			foreach (Met item in list)
			{
				if (Text.Length > 0)
				{
					Text.Append('|');
				}
				Text.Append(item.Id).Append(',').Append(Clean(item.Name))
					.Append(',')
					.Append(Clean(item.Kind))
					.Append(',')
					.Append(item.Female ? 'f' : 'm')
					.Append(',')
					.Append(item.FirstYear)
					.Append(',')
					.Append(item.LastYear)
					.Append(',')
					.Append(item.Talks)
					.Append(',')
					.Append(item.Order);
			}
			return Text.ToString();
		}

		private static void Parse(string s, List<Met> into)
		{
			string[] array = s.Split('|');
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(',');
				if (array2.Length >= 8 && long.TryParse(array2[0], out var result))
				{
					int.TryParse(array2[4], out var result2);
					int.TryParse(array2[5], out var result3);
					int.TryParse(array2[6], out var result4);
					long.TryParse(array2[7], out var result5);
					into.Add(new Met
					{
						Id = result,
						Name = array2[1],
						Kind = array2[2],
						Female = (array2[3] == "f"),
						FirstYear = result2,
						LastYear = result3,
						Talks = result4,
						Order = result5
					});
				}
			}
		}
	}
}
