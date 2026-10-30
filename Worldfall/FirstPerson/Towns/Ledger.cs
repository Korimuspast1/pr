using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class Ledger
	{
		internal sealed class Row
		{
			public Actor A;

			public string Name;

			public string Role;

			public string Town;
		}

		private const float DeskReach = 1.7f;

		private const float Refresh = 5f;

		public static bool Open;

		public static Building Hall;

		public static bool Royal;

		public static string Title = "Town ledger";

		public static string Search = "";

		public static Vector2 Scroll;

		public static readonly List<Row> Offices = new List<Row>();

		public static readonly List<Row> People = new List<Row>();

		private static float _builtAt = -100f;

		private static int _openedFrame = -1;

		private static int _closedFrame = -1;

		public static bool Escape()
		{
			if (!Open)
			{
				return false;
			}
			Close();
			return true;
		}

		public static void Show(Building hall)
		{
			Hall = hall;
			Open = hall != null;
			Search = "";
			Scroll = Vector2.zero;
			_openedFrame = Time.frameCount;
			Build();
			Log.Info($"ledger: opened ({Title}, {Offices.Count} offices, {People.Count} people)");
		}

		public static void Close()
		{
			if (Open)
			{
				_closedFrame = Time.frameCount;
			}
			Open = false;
			Hall = null;
		}

		public static void Update(Actor host)
		{
			if (Open)
			{
				WorldBoxMod instance = WorldBoxMod.Instance;
				bool flag = false;
				try
				{
					flag = Hall != null && Hall.isAlive();
				}
				catch
				{
				}
				if (!flag || host == null || instance == null || !instance.Interior.Inside || instance.Interior.House != Hall)
				{
					Close();
				}
				else if (Time.frameCount != _openedFrame && PlayInput.GetKeyDown(KeyCode.E) && GUIUtility.keyboardControl == 0)
				{
					Close();
				}
				else if (Time.unscaledTime - _builtAt > 5f)
				{
					Build();
				}
			}
		}

		public static bool RoomPrompt(PromptAsk ask)
		{
			if (ask?.Room?.Room == null || ask.House == null || ask.Host == null || ask.Room.Virtual)
			{
				return false;
			}
			if (!IsHall(ask.House))
			{
				return false;
			}
			Anchors.Spot[] array = HallRooms.Spots(ask.Room.Room, "a_steward");
			if (array.Length != 0 && !Near(ask.Eye, array))
			{
				return false;
			}
			City city;
			try
			{
				city = ask.House.city;
			}
			catch
			{
				return false;
			}
			if (city == null)
			{
				return false;
			}
			bool flag = TownTier.IsCapital(city);
			ask.Prompt = (flag ? "E:Royal ledger" : "E:Town ledger");
			if (!PlayInput.GetKeyDown(KeyCode.E) || Open || Time.frameCount == _closedFrame)
			{
				return true;
			}
			string text = ClosedTo(ask.Host, city);
			if (text != null)
			{
				Hub.Toast?.Invoke(text);
				return true;
			}
			Show(ask.House);
			return true;
		}

		private static string ClosedTo(Actor you, City town)
		{
			try
			{
				Kingdom kingdom = town.kingdom;
				Kingdom kingdom2 = you.kingdom;
				if (kingdom != null && kingdom2 != null && kingdom != kingdom2 && FirstPerson.Royal.AtWar(kingdom, kingdom2))
				{
					return "The ledger is closed to enemies of " + kingdom.name;
				}
			}
			catch
			{
			}
			return null;
		}

		private static bool IsHall(Building b)
		{
			try
			{
				return b != null && b.asset != null && b.asset.type == "type_hall";
			}
			catch
			{
				return false;
			}
		}

		private static bool Near(Vector2 eye, Anchors.Spot[] spots)
		{
			for (int i = 0; i < spots.Length; i++)
			{
				Anchors.Spot spot = spots[i];
				float num = spot.At.X - eye.x;
				float num2 = spot.At.Y - eye.y;
				if (num * num + num2 * num2 <= 2.89f)
				{
					return true;
				}
			}
			return false;
		}

		private static void Build()
		{
			_builtAt = Time.unscaledTime;
			Offices.Clear();
			People.Clear();
			City city = null;
			try
			{
				city = ((Hall != null) ? Hall.city : null);
			}
			catch
			{
			}
			if (city == null)
			{
				return;
			}
			Kingdom kingdom = null;
			try
			{
				kingdom = city.kingdom;
			}
			catch
			{
			}
			Royal = TownTier.IsCapital(city) && kingdom != null;
			Title = (Royal ? ("Royal ledger of " + kingdom.name) : ("Ledger of " + city.name));
			Vector2 vector = Hub.Host?.current_position ?? Vector2.zero;
			FindRole[] roles = Finding.Roles;
			foreach (FindRole r in roles)
			{
				Actor actor = Finding.Holder(city, r, vector);
				if (actor != null)
				{
					Offices.Add(new Row
					{
						A = actor,
						Name = Dialogue.NameOf(actor),
						Role = Finding.Word(r, actor),
						Town = TownOf(actor)
					});
				}
			}
			List<Actor> list;
			if (Royal)
			{
				list = new List<Actor>();
				foreach (Actor item in Civil.SubjectsOf(kingdom))
				{
					if (!Dialogue.IsPerson(item))
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
			}
			else
			{
				list = Finding.Residents(city);
			}
			foreach (Actor item2 in list)
			{
				People.Add(new Row
				{
					A = item2,
					Name = Dialogue.NameOf(item2),
					Role = Finding.RoleOf(item2),
					Town = TownOf(item2)
				});
			}
		}

		private static string TownOf(Actor a)
		{
			try
			{
				return (a.city != null) ? a.city.name : null;
			}
			catch
			{
				return null;
			}
		}

		public static void Find(Row row)
		{
			Actor host = Hub.Host;
			bool flag = false;
			try
			{
				flag = row?.A != null && row.A.isAlive();
			}
			catch
			{
			}
			if (!flag)
			{
				Hub.Toast?.Invoke("They are no longer alive");
				_builtAt = -100f;
			}
			else if (row.A == host)
			{
				Hub.Toast?.Invoke("That is you");
			}
			else if (Finding.Mark(host, row.A))
			{
				Hub.Toast?.Invoke(row.Name + " marked");
			}
		}
	}
}
