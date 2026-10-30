using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FirstPerson
{
	internal static class GreatWar
	{
		internal sealed class Rival
		{
			public long War;

			public long Kingdom;

			public string Name;

			public int Towns;

			public int People;

			public bool Real;

			public bool Done;

			public bool AtWarLately;

			public List<long> Cities = new List<long>();

			public List<long> Taken = new List<long>();

			public Dictionary<long, Vector2Int> Spots = new Dictionary<long, Vector2Int>();

			public Dictionary<long, string> Names = new Dictionary<long, string>();

			public List<long> Razed = new List<long>();

			public List<long> Claimed = new List<long>();

			private static string Ids(List<long> l)
			{
				return string.Join(",", l.Select((long c) => c.ToString()).ToArray());
			}

			private static void ReadIds(string f, List<long> into)
			{
				if (f.Length > 0)
				{
					into.AddRange(f.Split(',').Select(long.Parse));
				}
			}

			private static string Clean(string s)
			{
				return (s ?? "").Replace(":", " ").Replace(",", " ").Replace(";", " ")
					.Replace("|", " ");
			}

			public string Write()
			{
				string value;
				return string.Join(";", War.ToString(), Kingdom.ToString(), (Name ?? "").Replace(";", " ").Replace("|", " "), Towns.ToString(), People.ToString(), Real ? "1" : "0", Done ? "1" : "0", Ids(Cities), Ids(Taken), string.Join(",", Spots.Select((KeyValuePair<long, Vector2Int> kv) => kv.Key + ":" + kv.Value.x + ":" + kv.Value.y + ":" + Clean(Names.TryGetValue(kv.Key, out value) ? value : null)).ToArray()), Ids(Razed), Ids(Claimed));
			}

			public static Rival Read(string s)
			{
				string[] array = s.Split(';');
				if (array.Length < 9)
				{
					return null;
				}
				try
				{
					Rival rival = new Rival
					{
						War = long.Parse(array[0]),
						Kingdom = long.Parse(array[1]),
						Name = array[2],
						Towns = int.Parse(array[3]),
						People = int.Parse(array[4]),
						Real = (array[5] == "1"),
						Done = (array[6] == "1")
					};
					ReadIds(array[7], rival.Cities);
					ReadIds(array[8], rival.Taken);
					if (array.Length >= 12)
					{
						if (array[9].Length > 0)
						{
							string[] array2 = array[9].Split(',');
							for (int i = 0; i < array2.Length; i++)
							{
								string[] array3 = array2[i].Split(':');
								if (array3.Length >= 3)
								{
									long key = long.Parse(array3[0]);
									rival.Spots[key] = new Vector2Int(int.Parse(array3[1]), int.Parse(array3[2]));
									if (array3.Length > 3 && array3[3].Length > 0)
									{
										rival.Names[key] = array3[3];
									}
								}
							}
						}
						ReadIds(array[10], rival.Razed);
						ReadIds(array[11], rival.Claimed);
					}
					return rival;
				}
				catch
				{
					return null;
				}
			}
		}

		internal struct Ruin
		{
			public Rival Of;

			public long City;

			public Vector2Int At;

			public string Name;
		}

		private const string Key = "fp_rivals";

		private static readonly List<Rival> Rivals = new List<Rival>();

		private static long _for = -1L;

		private static float _tick;

		private const string WarsKey = "fp_warsdone";

		private const string TownsKey = "fp_townstaken";

		private static readonly HashSet<long> WarsDone = new HashSet<long>();

		private static readonly HashSet<long> TownsDone = new HashSet<long>();

		internal static int WinsForTest;

		private const int ClaimSettlers = 4;

		internal static List<Rival> RivalsForTest => Rivals;

		internal static void Reset(Actor body)
		{
			Rivals.Clear();
			WarsDone.Clear();
			TownsDone.Clear();
			_for = -1L;
			try
			{
				if (body != null && body.data != null)
				{
					body.data.removeString("fp_rivals");
					body.data.removeString("fp_warsdone");
					body.data.removeString("fp_townstaken");
				}
			}
			catch
			{
			}
		}

		public static void Tick(Actor you, float dt, Action<string> toast)
		{
			if (you == null)
			{
				return;
			}
			_tick -= dt;
			if (_tick > 0f)
			{
				return;
			}
			_tick = 1f;
			try
			{
				if (_for != you.data.id)
				{
					Load(you);
				}
				Kingdom mine = you.kingdom;
				bool flag = you.isKing() && mine != null && mine.isCiv();
				bool flag2 = false;
				if (flag)
				{
					foreach (War w in mine.getWars())
					{
						if (w == null)
						{
							continue;
						}
						foreach (Kingdom enemy in w.getOppositeSideKingdom(mine))
						{
							if (enemy != null && enemy.isAlive() && enemy.countCities() != 0 && !Rivals.Exists((Rival rival2) => rival2.War == w.data.id && rival2.Kingdom == enemy.data.id))
							{
								Rival rival = new Rival
								{
									War = w.data.id,
									Kingdom = enemy.data.id,
									Name = enemy.name,
									Towns = enemy.countCities(),
									People = enemy.getPopulationPeople(),
									Cities = (from c in enemy.getCities()
										where c?.isAlive() ?? false
										select c.data.id).ToList()
								};
								rival.Real = rival.Towns >= 3 || (rival.People >= mine.getPopulationPeople() && (rival.Towns >= 2 || rival.People >= 30));
								Rivals.Add(rival);
								flag2 = true;
								Log.Info(string.Format("great war: {0} at war with {1} ({2} towns, {3} people){4}", mine.name, rival.Name, rival.Towns, rival.People, rival.Real ? ": a real rival" : ""));
								if (rival.Real)
								{
									toast?.Invoke($"A great war! {rival.Name} ({rival.Towns} towns, {rival.People} people) is a real rival. Destroy it while you wear the crown, and you will live forever.");
								}
							}
						}
					}
				}
				foreach (Rival r in Rivals)
				{
					if (r.Done)
					{
						continue;
					}
					foreach (long city3 in r.Cities)
					{
						City city = Quests.CityById(city3);
						if (city != null && city.isAlive())
						{
							WorldTile worldTile = Middle(city);
							if (worldTile != null && (!r.Spots.TryGetValue(city3, out var value) || value.x != worldTile.x || value.y != worldTile.y))
							{
								r.Spots[city3] = new Vector2Int(worldTile.x, worldTile.y);
								r.Names[city3] = city.name;
								flag2 = true;
							}
						}
						else if (!r.Razed.Contains(city3) && !r.Taken.Contains(city3) && r.Spots.ContainsKey(city3))
						{
							r.Razed.Add(city3);
							flag2 = true;
							Log.Info("great war: " + Named(r, city3) + " of " + r.Name + " razed");
						}
						if (city != null && city.isAlive() && mine != null && city.kingdom == mine && !r.Taken.Contains(city3))
						{
							r.Taken.Add(city3);
							flag2 = true;
							if (TownsDone.Add(city3))
							{
								Run.Tally(you, "town");
							}
						}
					}
					Kingdom kingdom = KingdomById(r.Kingdom);
					bool flag3 = kingdom == null || !kingdom.isAlive() || kingdom.countCities() == 0;
					bool flag4 = r.Cities.All(delegate(long id)
					{
						City city2 = Quests.CityById(id);
						return city2 == null || !city2.isAlive() || (mine != null && (city2.kingdom == mine || Royal.Allied(mine, city2.kingdom)));
					});
					if (flag3 && flag4)
					{
						r.Done = true;
						flag2 = true;
						if (r.Taken.Count > 0 || r.Razed.Count > 0 || r.AtWarLately)
						{
							Run.Tally(you, "kingdom");
							if (WarsDone.Add(r.War))
							{
								Run.Tally(you, "war");
							}
						}
						Log.Info($"great war: {r.Name} destroyed (real {r.Real}, {r.Taken.Count} taken, {r.Razed.Count} razed, you king {flag})");
						if (r.Real && flag)
						{
							WinsForTest++;
						}
						if (r.Real && flag && !you.hasTrait("immortal"))
						{
							Immortal(you, r, toast);
						}
						else
						{
							toast?.Invoke(r.Name + " is no more.");
						}
					}
					else
					{
						if (flag3)
						{
							continue;
						}
						bool flag5 = mine != null && mine.getWars().Any((War war) => war != null && war.data.id == r.War);
						r.AtWarLately = flag5;
						if (!flag5)
						{
							r.Done = true;
							flag2 = true;
							if (mine != null && r.Taken.Exists(delegate(long id)
							{
								City city2 = Quests.CityById(id);
								return city2 != null && city2.isAlive() && city2.kingdom == mine;
							}) && WarsDone.Add(r.War))
							{
								Run.Tally(you, "war");
							}
							Log.Info($"great war: the war with {r.Name} ended, {r.Taken.Count} of its towns taken");
						}
					}
				}
				if (flag2)
				{
					Save(you);
				}
			}
			catch (Exception ex)
			{
				Log.Error("great war: " + ex.Message);
			}
		}

		internal static void Inherit(long deadId, Actor heir)
		{
			try
			{
				if (heir != null && heir.data != null && deadId != 0L && _for == deadId)
				{
					_for = heir.data.id;
					Save(heir);
				}
			}
			catch
			{
			}
		}

		private static Kingdom KingdomById(long id)
		{
			try
			{
				return World.world.kingdoms.get(id);
			}
			catch
			{
				return null;
			}
		}

		private static void Immortal(Actor you, Rival r, Action<string> toast)
		{
			try
			{
				ActorTrait actorTrait = AssetManager.traits.get("immortal");
				if (actorTrait != null && you.addTrait(actorTrait, pRemoveOpposites: true))
				{
					TraitSwitch.Earned(you, "immortal");
				}
			}
			catch (Exception ex)
			{
				Log.Error("great war: immortal: " + ex.Message);
			}
			Run.Tally(you, "immortal");
			Chronicle.AddOwnMemory(you, "fp_immortal", r.Name);
			string text = Dialogue.NameOf(you);
			WorldNews.Announce("fp_immortal", text + " destroyed " + r.Name + " in a great war, and will live forever.", NewsTone.Good, you.current_position);
			WorldBoxMod.Instance?.Audio.PlayVictory();
			WorldBoxMod.Instance?.Celebrate(1200f);
			toast?.Invoke(r.Name + " is no more. The great war is won: you are immortal.");
			Log.Info("great war: " + WorldBoxMod.Describe(you) + " is immortal (" + r.Name + " destroyed)");
		}

		public static string Card(Actor you)
		{
			if (you == null || you.data == null || !Run.LockedOn(you) || you.data.id != _for)
			{
				return null;
			}
			foreach (Rival rival in Rivals)
			{
				if (rival.Done || !rival.Real)
				{
					continue;
				}
				int num = 0;
				Kingdom kingdom = you.kingdom;
				foreach (long city2 in rival.Cities)
				{
					City city = Quests.CityById(city2);
					if (city != null && city.isAlive() && city.kingdom != kingdom && !Royal.Allied(city.kingdom, kingdom))
					{
						num++;
					}
				}
				return string.Format("Great war: {0}, {1} town{2} left", rival.Name, num, (num == 1) ? "" : "s");
			}
			return null;
		}

		private static void Load(Actor you)
		{
			_for = you.data.id;
			Rivals.Clear();
			WarsDone.Clear();
			TownsDone.Clear();
			try
			{
				you.data.get("fp_warsdone", out var pResult, null);
				if (!string.IsNullOrEmpty(pResult))
				{
					string[] array = pResult.Split(',');
					for (int i = 0; i < array.Length; i++)
					{
						if (long.TryParse(array[i], out var result))
						{
							WarsDone.Add(result);
						}
					}
				}
				you.data.get("fp_townstaken", out var pResult2, null);
				if (!string.IsNullOrEmpty(pResult2))
				{
					string[] array = pResult2.Split(',');
					for (int i = 0; i < array.Length; i++)
					{
						if (long.TryParse(array[i], out var result2))
						{
							TownsDone.Add(result2);
						}
					}
				}
			}
			catch
			{
			}
			try
			{
				you.data.get("fp_rivals", out var pResult3, null);
				if (string.IsNullOrEmpty(pResult3))
				{
					return;
				}
				string[] array = pResult3.Split('|');
				for (int i = 0; i < array.Length; i++)
				{
					Rival rival = Rival.Read(array[i]);
					if (rival != null)
					{
						Rivals.Add(rival);
					}
				}
			}
			catch
			{
			}
		}

		private static void Save(Actor you)
		{
			while (Rivals.Count > 16)
			{
				int num = Rivals.FindIndex((Rival r) => r.Done);
				if (num < 0)
				{
					break;
				}
				Rivals.RemoveAt(num);
			}
			try
			{
				you.data.set("fp_rivals", string.Join("|", Rivals.Select((Rival r) => r.Write()).ToArray()));
				you.data.set("fp_warsdone", string.Join(",", WarsDone.Select((long x) => x.ToString()).ToArray()));
				you.data.set("fp_townstaken", string.Join(",", TownsDone.Select((long x) => x.ToString()).ToArray()));
			}
			catch
			{
			}
		}

		private static WorldTile Middle(City c)
		{
			try
			{
				foreach (TileZone zone in c.zones)
				{
					if (zone?.centerTile != null)
					{
						return zone.centerTile;
					}
				}
			}
			catch
			{
			}
			try
			{
				return c.getTile();
			}
			catch
			{
				return null;
			}
		}

		private static string Named(Rival r, long id)
		{
			if (!r.Names.TryGetValue(id, out var value) || string.IsNullOrEmpty(value))
			{
				return "a town";
			}
			return value;
		}

		internal static List<Ruin> Ruins(Actor you)
		{
			List<Ruin> list = new List<Ruin>();
			if (you == null || you.data == null)
			{
				return list;
			}
			if (_for != you.data.id)
			{
				Load(you);
			}
			foreach (Rival rival in Rivals)
			{
				foreach (long item in rival.Razed)
				{
					if (!rival.Claimed.Contains(item) && rival.Spots.TryGetValue(item, out var value))
					{
						WorldTile worldTile = null;
						try
						{
							worldTile = World.world.GetTile(value.x, value.y);
						}
						catch
						{
						}
						if (worldTile?.zone != null && !worldTile.zone.hasCity())
						{
							list.Add(new Ruin
							{
								Of = rival,
								City = item,
								At = value,
								Name = Named(rival, item)
							});
						}
					}
				}
			}
			Vector2 p = you.current_position;
			list.Sort((Ruin a, Ruin b) => (new Vector2(a.At.x, a.At.y) - p).sqrMagnitude.CompareTo((new Vector2(b.At.x, b.At.y) - p).sqrMagnitude));
			return list;
		}

		private static float Gap(City c, WorldTile at)
		{
			WorldTile worldTile = Middle(c);
			if (worldTile != null)
			{
				return new Vector2(worldTile.x - at.x, worldTile.y - at.y).sqrMagnitude;
			}
			return float.MaxValue;
		}

		internal static City ClaimRuin(Actor you, Ruin ruin, Action<string> toast)
		{
			Kingdom kingdom = you?.kingdom;
			if (kingdom == null || !you.isKing() || ruin.Of == null)
			{
				return null;
			}
			WorldTile at = null;
			try
			{
				at = World.world.GetTile(ruin.At.x, ruin.At.y);
			}
			catch
			{
			}
			if (at?.zone == null)
			{
				return null;
			}
			List<City> list = new List<City>();
			foreach (City city3 in kingdom.getCities())
			{
				if (city3 != null && city3.isAlive() && Middle(city3) != null && Quests.OnFoot(city3, at))
				{
					list.Add(city3);
				}
			}
			if (list.Count == 0)
			{
				toast?.Invoke("None of your towns can reach " + ruin.Name + " by land.");
				return null;
			}
			list.Sort((City a, City b) => Gap(a, at).CompareTo(Gap(b, at)));
			City city = null;
			List<Actor> list2 = new List<Actor>();
			foreach (City item in list)
			{
				foreach (Actor unit in item.getUnits())
				{
					if (list2.Count >= 4)
					{
						break;
					}
					try
					{
						if (unit != null && unit.isAlive() && unit != you && unit.isAdult() && !unit.isKing() && !unit.isCityLeader() && !unit.isWarrior() && !ControllableUnit.isControllingUnit(unit))
						{
							list2.Add(unit);
						}
					}
					catch
					{
					}
				}
				if (list2.Count > 0)
				{
					city = item;
					break;
				}
			}
			if (city == null)
			{
				toast?.Invoke("None of your towns has anyone to spare for " + ruin.Name + ".");
				return null;
			}
			TileZone tileZone = null;
			List<TileZone> list3 = new List<TileZone> { at.zone };
			try
			{
				list3.AddRange(at.zone.neighbours_all);
			}
			catch
			{
			}
			foreach (TileZone item2 in list3)
			{
				try
				{
					if (item2 != null && !item2.hasCity() && item2.isGoodForNewCity(list2[0]))
					{
						tileZone = item2;
						break;
					}
				}
				catch
				{
				}
			}
			if (tileZone == null)
			{
				toast?.Invoke("The land at " + ruin.Name + " won't take a town yet.");
				return null;
			}
			City city2;
			try
			{
				city2 = World.world.cities.buildNewCity(list2[0], tileZone);
				if (city2 == null)
				{
					return null;
				}
				foreach (Actor item3 in list2)
				{
					try
					{
						item3.stopBeingWarrior();
						item3.joinCity(city2);
						item3.cancelAllBeh();
					}
					catch (Exception ex)
					{
						Log.Error("great war: settler: " + ex.Message);
					}
				}
				try
				{
					if (!city2.hasLeader())
					{
						city2.setLeader(list2[0], pNew: true);
					}
				}
				catch
				{
				}
			}
			catch (Exception ex2)
			{
				Log.Error("great war: claiming ruins: " + ex2);
				toast?.Invoke("The land at " + ruin.Name + " won't take a town yet.");
				return null;
			}
			ruin.Of.Claimed.Add(ruin.City);
			Save(you);
			Log.Info($"great war: {list2.Count} settler(s) from {city.name} claim the ruins of {ruin.Name} as {city2.name} for {kingdom.name}");
			toast?.Invoke(string.Format("{0} settler{1} from {2} set out to build {3} on the ruins of {4}.", list2.Count, (list2.Count == 1) ? "" : "s", city.name, city2.name, ruin.Name));
			return city2;
		}

		internal static List<City> AllyHolds(Actor you, Kingdom ally)
		{
			List<City> list = new List<City>();
			if (you == null || you.data == null || ally == null)
			{
				return list;
			}
			if (_for != you.data.id)
			{
				Load(you);
			}
			foreach (Rival rival in Rivals)
			{
				foreach (long city2 in rival.Cities)
				{
					City city = Quests.CityById(city2);
					try
					{
						if (city != null && city.isAlive() && city.kingdom == ally && !list.Contains(city))
						{
							list.Add(city);
						}
					}
					catch
					{
					}
				}
			}
			return list;
		}
	}
}
