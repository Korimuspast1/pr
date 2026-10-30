using System;
using System.Collections.Generic;
using System.Text;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class Finding
	{
		public const string HeardKey = "fp_heard";

		private const int MaxHeard = 200;

		public static readonly FindRole[] Roles = new FindRole[6]
		{
			FindRole.Leader,
			FindRole.Monarch,
			FindRole.Smith,
			FindRole.Healer,
			FindRole.Captain,
			FindRole.Teacher
		};

		public static readonly string[] Groups = new string[5] { "ABCDE", "FGHIJ", "KLMNO", "PQRST", "UVWXYZ" };

		private static long _heardFor = -1L;

		private static object _heardWorld;

		private static readonly List<long> HeardList = new List<long>();

		private static readonly HashSet<long> HeardSet = new HashSet<long>();

		public static string Asked(FindRole r)
		{
			switch (r)
			{
			case FindRole.Leader:
				return "The town's leader";
			case FindRole.Monarch:
				return "The king or queen";
			case FindRole.Smith:
				return "The smith";
			case FindRole.Healer:
				return "A healer";
			case FindRole.Captain:
				return "The guard captain";
			default:
				return "A teacher";
			}
		}

		public static string Word(FindRole r, Actor a)
		{
			switch (r)
			{
			case FindRole.Leader:
				return "leader";
			case FindRole.Monarch:
				if (!Models.IsFemale(a))
				{
					return "king";
				}
				return "queen";
			case FindRole.Smith:
				return "smith";
			case FindRole.Healer:
				return "healer";
			case FindRole.Captain:
				return "captain";
			default:
				return "teacher";
			}
		}

		public static Actor Holder(City c, FindRole r, Vector2 from)
		{
			if (c == null)
			{
				return null;
			}
			try
			{
				switch (r)
				{
				case FindRole.Leader:
					return Alive(c.leader);
				case FindRole.Monarch:
				{
					Kingdom kingdom = c.kingdom;
					Actor actor2 = ((kingdom != null) ? Alive(kingdom.king) : null);
					if (actor2 == null)
					{
						return null;
					}
					return (kingdom.capital == c || actor2.city == c) ? actor2 : null;
				}
				case FindRole.Smith:
				{
					Actor actor = Keeper(c, ShopKind.Smithy);
					if (actor != null && Dialogue.IsSmith(actor))
					{
						return actor;
					}
					return Nearest(c, from, (Actor a) => Dialogue.JobName(a) == "smith" && Dialogue.IsSmith(a)) ?? Nearest(c, from, (Actor a) => Dialogue.IsSmith(a) && !a.isKing());
				}
				case FindRole.Healer:
					return Nearest(c, from, (Actor a) => Dialogue.HasTrait(a, "healer"));
				case FindRole.Captain:
					return Nearest(c, from, IsCaptain);
				default:
					return Nearest(c, from, (Actor a) => a.isAdult() && Teachers.CanTeach(a).Count > 0);
				}
			}
			catch (Exception ex)
			{
				Log.Error("finding: " + r.ToString() + ": " + ex.Message);
				return null;
			}
		}

		private static Actor Alive(Actor a)
		{
			try
			{
				return (a != null && a.isAlive()) ? a : null;
			}
			catch
			{
				return null;
			}
		}

		private static Actor Keeper(City c, ShopKind kind)
		{
			foreach (Building item in Shops.In(c))
			{
				if (Shops.Of(item) == kind)
				{
					Actor actor = Shops.Keeper(item);
					if (actor != null)
					{
						return actor;
					}
				}
			}
			return null;
		}

		private static bool IsCaptain(Actor a)
		{
			try
			{
				return a.army != null && a.army.getCaptain() == a;
			}
			catch
			{
				return false;
			}
		}

		private static Actor Nearest(City c, Vector2 from, Func<Actor, bool> fits)
		{
			Actor result = null;
			float num = float.MaxValue;
			foreach (Actor item in Civil.PeopleOf(c))
			{
				bool flag;
				try
				{
					flag = fits(item);
				}
				catch
				{
					flag = false;
				}
				if (flag)
				{
					float sqrMagnitude = (item.current_position - from).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						result = item;
					}
				}
			}
			return result;
		}

		public static string RoleOf(Actor a)
		{
			if (a == null)
			{
				return null;
			}
			try
			{
				if (a.isKing())
				{
					return Models.IsFemale(a) ? "queen" : "king";
				}
				if (a.isCityLeader())
				{
					return "leader";
				}
				if (IsCaptain(a))
				{
					return "captain";
				}
				Building homeBuilding = a.getHomeBuilding();
				if (homeBuilding != null && Shops.Keeper(homeBuilding) == a && Shops.Of(homeBuilding) == ShopKind.Smithy)
				{
					return "smith";
				}
				if (!a.isAdult())
				{
					return "child";
				}
			}
			catch
			{
			}
			return Dialogue.JobName(a);
		}

		public static List<Actor> Residents(City c, Actor except1 = null, Actor except2 = null)
		{
			List<Actor> list = new List<Actor>();
			foreach (Actor item in Civil.PeopleOf(c))
			{
				if (item == except1 || item == except2 || !Dialogue.IsPerson(item))
				{
					continue;
				}
				try
				{
					if (item.isBaby())
					{
						continue;
					}
				}
				catch
				{
				}
				list.Add(item);
			}
			list.Sort((Actor x, Actor y) => string.Compare(Dialogue.NameOf(x), Dialogue.NameOf(y), StringComparison.OrdinalIgnoreCase));
			return list;
		}

		public static int GroupOf(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return Groups.Length - 1;
			}
			char value = char.ToUpperInvariant(name[0]);
			for (int i = 0; i < Groups.Length; i++)
			{
				if (Groups[i].IndexOf(value) >= 0)
				{
					return i;
				}
			}
			return Groups.Length - 1;
		}

		public static string GroupLabel(int g)
		{
			return Groups[g][0] + " to " + Groups[g][Groups[g].Length - 1];
		}

		public static string Whereabouts(Actor who, Vector2 from)
		{
			Vector2 d = who.current_position - from;
			float magnitude = d.magnitude;
			if (magnitude < 6f)
			{
				return "right over there";
			}
			string text = Direction(d);
			if (magnitude < 20f)
			{
				return text + " of here, not far";
			}
			if (magnitude < 60f)
			{
				return text + " of here, a fair walk";
			}
			return "a long way " + text;
		}

		private static string Direction(Vector2 d)
		{
			float num = Mathf.Atan2(d.y, d.x) * 57.29578f;
			string[] obj = new string[8] { "east", "northeast", "north", "northwest", "west", "southwest", "south", "southeast" };
			int num2 = Mathf.RoundToInt((num + 360f) % 360f / 45f) % 8;
			return obj[num2];
		}

		private static void LoadHeard(Actor you)
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
			if (num == _heardFor && obj2 == _heardWorld)
			{
				return;
			}
			_heardFor = num;
			_heardWorld = obj2;
			HeardList.Clear();
			HeardSet.Clear();
			if (num < 0)
			{
				return;
			}
			try
			{
				you.data.get("fp_heard", out var pResult, null);
				if (string.IsNullOrEmpty(pResult))
				{
					return;
				}
				string[] array = pResult.Split(',');
				for (int i = 0; i < array.Length; i++)
				{
					if (long.TryParse(array[i], out var result) && HeardSet.Add(result))
					{
						HeardList.Add(result);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("finding: could not read heard names: " + ex.Message);
			}
		}

		public static bool Heard(Actor you, Actor who)
		{
			if (you == null || who == null)
			{
				return false;
			}
			LoadHeard(you);
			try
			{
				return HeardSet.Contains(who.data.id);
			}
			catch
			{
				return false;
			}
		}

		public static void Hear(Actor you, Actor who)
		{
			if (you == null || who == null || who == you)
			{
				return;
			}
			LoadHeard(you);
			long id;
			try
			{
				id = who.data.id;
			}
			catch
			{
				return;
			}
			if (HeardSet.Contains(id))
			{
				HeardList.Remove(id);
				HeardList.Add(id);
			}
			else
			{
				HeardSet.Add(id);
				HeardList.Add(id);
			}
			while (HeardList.Count > 200)
			{
				HeardSet.Remove(HeardList[0]);
				HeardList.RemoveAt(0);
			}
			StringBuilder stringBuilder = new StringBuilder(HeardList.Count * 8);
			for (int i = 0; i < HeardList.Count; i++)
			{
				if (i > 0)
				{
					stringBuilder.Append(',');
				}
				stringBuilder.Append(HeardList[i]);
			}
			try
			{
				you.data.set("fp_heard", stringBuilder.ToString());
			}
			catch (Exception ex)
			{
				Log.Error("finding: could not write heard names: " + ex.Message);
			}
		}

		public static void Reset()
		{
			_heardFor = -1L;
			HeardList.Clear();
			HeardSet.Clear();
		}

		public static bool Mark(Actor you, Actor who)
		{
			if (who == null)
			{
				return false;
			}
			Hear(you, who);
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance != null)
			{
				return instance.PingFound(who);
			}
			return false;
		}
	}
}
