using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class Hub
	{
		public const float NearTiles = 60f;

		public static readonly List<City> Near = new List<City>();

		internal static bool KeysFree;

		internal static bool InRoom;

		private static object _world;

		private static float _nearTimer;

		private static float _shopTimer;

		private static int _shopCursor;

		private static readonly Dictionary<string, float> ErrorAt = new Dictionary<string, float>();

		private static readonly List<City> CrimeTowns = new List<City>();

		private static readonly List<Kingdom> CrimeRealms = new List<Kingdom>();

		public static Actor Host { get; private set; }

		public static City Here { get; private set; }

		public static Action<string> Toast { get; private set; }

		public static bool AnyWindowOpen
		{
			get
			{
				if (!Planning.Open && !Chest.Open && !HomeCard.Open)
				{
					return Ledger.Open;
				}
				return true;
			}
		}

		public static bool AnyModeActive
		{
			get
			{
				if (!Planning.Aiming)
				{
					return LandPlot.Aiming;
				}
				return true;
			}
		}

		public static string ModePrompt
		{
			get
			{
				if (!Planning.Aiming)
				{
					if (!LandPlot.Aiming)
					{
						return null;
					}
					return LandPlot.Prompt;
				}
				return Planning.Prompt;
			}
		}

		public static void Update(Actor host, float dt, Action<string> toast)
		{
			Toast = toast;
			Host = host;
			CheckWorld();
			Look(host, dt);
			try
			{
				TownTier.Watch(Near, dt, toast);
			}
			catch (Exception e)
			{
				Fail("tiers", e);
			}
			try
			{
				RepHud.Tick();
			}
			catch (Exception e2)
			{
				Fail("rep notices", e2);
			}
			PickShops(dt);
			TownKeys(host);
			try
			{
				Homes.Update(host, dt);
			}
			catch (Exception e3)
			{
				Fail("homes", e3);
			}
			try
			{
				Planning.Update(host, dt);
			}
			catch (Exception e4)
			{
				Fail("planning", e4);
			}
			try
			{
				Court.Update(host, dt);
			}
			catch (Exception e5)
			{
				Fail("court", e5);
			}
			try
			{
				HallModels.Watch(dt);
			}
			catch (Exception e6)
			{
				Fail("hall models", e6);
			}
			try
			{
				Patrons.Update(host, dt);
			}
			catch (Exception e7)
			{
				Fail("gifts", e7);
			}
			try
			{
				LandPlot.Update(host, dt);
			}
			catch (Exception e8)
			{
				Fail("land", e8);
			}
			try
			{
				HomeLife.Update(host, dt);
			}
			catch (Exception e9)
			{
				Fail("home", e9);
			}
			try
			{
				Homebodies.Update(host, dt);
			}
			catch (Exception e10)
			{
				Fail("homebodies", e10);
			}
			try
			{
				Ledger.Update(host);
			}
			catch (Exception e11)
			{
				Fail("ledger", e11);
			}
		}

		private static void TownKeys(Actor host)
		{
			if (!KeysFree)
			{
				return;
			}
			try
			{
				if (PlayInput.GetKeyDown(KeyCode.Y))
				{
					if (InRoom)
					{
						Toast?.Invoke("Step outside to set down lights");
					}
					else
					{
						Planning.Key(host);
					}
				}
			}
			catch (Exception e)
			{
				Fail("keys", e);
			}
		}

		private static void Look(Actor host, float dt)
		{
			City city = null;
			try
			{
				WorldTile worldTile = host?.current_tile;
				if (worldTile != null && worldTile.zone != null && worldTile.zone.city != null && worldTile.zone.city.isAlive())
				{
					city = worldTile.zone.city;
				}
			}
			catch
			{
			}
			Here = city;
			_nearTimer -= dt;
			if (_nearTimer > 0f && host != null)
			{
				return;
			}
			_nearTimer = 2f;
			Near.Clear();
			if (host == null)
			{
				return;
			}
			Vector2 current_position;
			try
			{
				current_position = host.current_position;
			}
			catch
			{
				return;
			}
			try
			{
				foreach (City item in World.world.cities.list)
				{
					if (item != null && item.isAlive() && ((Shops.Centre(item) - current_position).sqrMagnitude <= 3600f || item == city))
					{
						Near.Add(item);
					}
				}
			}
			catch (Exception e)
			{
				Fail("near", e);
			}
		}

		private static void PickShops(float dt)
		{
			_shopTimer -= dt;
			if (_shopTimer > 0f || Near.Count == 0)
			{
				return;
			}
			_shopTimer = 0.5f;
			_shopCursor = (_shopCursor + 1) % Near.Count;
			try
			{
				Shops.Assign(Near[_shopCursor]);
			}
			catch (Exception e)
			{
				Fail("shop picks", e);
			}
		}

		private static void CheckWorld()
		{
			object obj = ((World.world != null) ? World.world.map_stats : null);
			if (obj != _world)
			{
				_world = obj;
				Reset();
			}
		}

		internal static void Reset()
		{
			Near.Clear();
			Here = null;
			_nearTimer = (_shopTimer = 0f);
			TownTier.Reset();
			RepHud.Reset();
		}

		public static void Collect(TownScene s)
		{
			try
			{
				HouseLife.Collect(s);
			}
			catch (Exception e)
			{
				Fail("house life", e);
			}
			try
			{
				PlanGhosts.Collect(s);
			}
			catch (Exception e2)
			{
				Fail("plan ghosts", e2);
			}
			try
			{
				LandPlot.Collect(s);
			}
			catch (Exception e3)
			{
				Fail("plot ghost", e3);
			}
			try
			{
				PlacedLights.Collect(s);
			}
			catch (Exception e4)
			{
				Fail("lights", e4);
			}
			try
			{
				Streams.Collect(s);
			}
			catch (Exception e5)
			{
				Fail("streams", e5);
			}
		}

		public static void Building(TownBuilding tb)
		{
			try
			{
				HouseLife.Building(tb);
			}
			catch (Exception e)
			{
				Fail("house life", e);
			}
			try
			{
				PlacedLights.Building(tb);
			}
			catch (Exception e2)
			{
				Fail("wall torches", e2);
			}
			try
			{
				HallBoard.Building(tb);
			}
			catch (Exception e3)
			{
				Fail("hall board", e3);
			}
			try
			{
				Curtains.Building(tb);
			}
			catch (Exception e4)
			{
				Fail("curtains", e4);
			}
		}

		public static bool BoardSpot(City c, out Vector2 at, out float yaw)
		{
			at = default(Vector2);
			yaw = 0f;
			try
			{
				return c != null && HallBoard.Spot(c, out at, out yaw);
			}
			catch (Exception e)
			{
				Fail("hall board spot", e);
				return false;
			}
		}

		public static void DrawHud(float sw, float sh)
		{
			try
			{
				HomeLife.DrawHud(sw, sh);
			}
			catch (Exception e)
			{
				Fail("sleep HUD", e);
			}
			try
			{
				HudPlanning.DrawBar(sw, sh);
			}
			catch (Exception e2)
			{
				Fail("planning bar", e2);
			}
		}

		public static void DrawWindows()
		{
			try
			{
				if (Planning.Open)
				{
					HudPlanning.Draw();
				}
			}
			catch (Exception e)
			{
				Fail("planning window", e);
			}
		}

		public static bool Escape()
		{
			try
			{
				return Planning.Escape() || Chest.Escape() || LandPlot.Escape() || Ledger.Escape();
			}
			catch (Exception e)
			{
				Fail("escape", e);
				return false;
			}
		}

		public static void CloseAll()
		{
			for (int i = 0; i < 8; i++)
			{
				if (!Escape())
				{
					break;
				}
			}
			Host = null;
			Here = null;
		}

		public static bool Prompt(PromptAsk ask)
		{
			try
			{
				if (LandPlot.PromptAt(ask))
				{
					return true;
				}
			}
			catch (Exception e)
			{
				Fail("plot prompt", e);
			}
			return false;
		}

		public static bool RoomPrompt(PromptAsk ask)
		{
			try
			{
				if (Curtains.RoomPrompt(ask))
				{
					return true;
				}
			}
			catch (Exception e)
			{
				Fail("curtains prompt", e);
			}
			try
			{
				if (Homes.RoomPrompt(ask))
				{
					return true;
				}
			}
			catch (Exception e2)
			{
				Fail("home prompt", e2);
			}
			try
			{
				if (HomeLife.RoomPrompt(ask))
				{
					return true;
				}
			}
			catch (Exception e3)
			{
				Fail("bed prompt", e3);
			}
			try
			{
				if (Court.RoomPrompt(ask))
				{
					return true;
				}
			}
			catch (Exception e4)
			{
				Fail("court prompt", e4);
			}
			try
			{
				if (Ledger.RoomPrompt(ask))
				{
					return true;
				}
			}
			catch (Exception e5)
			{
				Fail("ledger prompt", e5);
			}
			return false;
		}

		public static List<JournalEntry> JournalEntries()
		{
			return new List<JournalEntry>();
		}

		public static void OnKill(Actor killer, Actor victim)
		{
		}

		public static void OnHitLanded(Actor attacker, Actor victim, bool kill)
		{
		}

		public static void OnCrime(Actor body, Vector2 at, Kingdom victims, List<Actor> witnesses, string crime, bool onlyTheirs = false)
		{
			if (body == null)
			{
				return;
			}
			try
			{
				int num = ((crime == "murder") ? 750 : ((crime == "theft") ? 200 : 100));
				string reason = crime + " seen";
				CrimeTowns.Clear();
				CrimeRealms.Clear();
				City city = TownAt(at);
				if (city != null)
				{
					CrimeTowns.Add(city);
				}
				else if (witnesses != null)
				{
					foreach (Actor witness in witnesses)
					{
						City city2 = null;
						try
						{
							if (witness != null && witness.city != null && witness.city.isAlive())
							{
								city2 = witness.city;
							}
						}
						catch
						{
						}
						if (city2 != null && !CrimeTowns.Contains(city2))
						{
							CrimeTowns.Add(city2);
						}
					}
				}
				if (Rep.Civ(victims))
				{
					CrimeRealms.Add(victims);
				}
				if (onlyTheirs)
				{
					CrimeTowns.RemoveAll((City c) => Rep.RealmOf(c) != victims);
				}
				foreach (City crimeTown in CrimeTowns)
				{
					Rep.Add(body, crimeTown, -num, reason, realm: false);
					Kingdom kingdom = Rep.RealmOf(crimeTown);
					if (kingdom != null && !CrimeRealms.Contains(kingdom))
					{
						CrimeRealms.Add(kingdom);
					}
				}
				foreach (Kingdom crimeRealm in CrimeRealms)
				{
					Rep.AddStanding(body, crimeRealm, -num, reason);
				}
				NoteAlsoCharged(body, victims);
				Log.Info(string.Format("rep: {0} seen {1}: {2} towns, {3} realms lose {4}", crime, (city != null) ? ("in " + Rep.NameOf(city)) : "on open land", CrimeTowns.Count, CrimeRealms.Count, num));
			}
			catch (Exception e)
			{
				Fail("crime", e);
			}
		}

		public static void OnWarCrimeSeen(Actor body, Kingdom own, string crime)
		{
			if (body == null || own == null)
			{
				return;
			}
			try
			{
				int num = ((crime == "murder") ? 750 : 100);
				Rep.AddStanding(body, own, -num, "war crime seen");
			}
			catch (Exception e)
			{
				Fail("war crime", e);
			}
		}

		public static void OnFinePaid(Actor body, Kingdom k)
		{
			try
			{
				Rep.Pardon(body, k, "fine paid");
			}
			catch (Exception e)
			{
				Fail("fine paid", e);
			}
			try
			{
				string text = AlsoKey(k);
				if (text == null)
				{
					return;
				}
				body.data.get(text, out var pResult, null);
				if (string.IsNullOrEmpty(pResult))
				{
					return;
				}
				body.data.set(text, "");
				string[] array = pResult.Split(',');
				for (int i = 0; i < array.Length; i++)
				{
					if (long.TryParse(array[i], out var result))
					{
						Kingdom kingdom = null;
						try
						{
							kingdom = World.world.kingdoms.get(result);
						}
						catch
						{
						}
						if (kingdom != null && kingdom != k && kingdom.isAlive())
						{
							Rep.Pardon(body, kingdom, "fine paid");
						}
					}
				}
			}
			catch (Exception e2)
			{
				Fail("fine paid elsewhere", e2);
			}
		}

		private static string AlsoKey(Kingdom bountyRealm)
		{
			try
			{
				return (bountyRealm != null) ? ("fp_crime_also_" + bountyRealm.data.id) : null;
			}
			catch
			{
				return null;
			}
		}

		private static void NoteAlsoCharged(Actor body, Kingdom victims)
		{
			string text = AlsoKey(victims);
			if (text == null)
			{
				return;
			}
			try
			{
				body.data.get(text, out var pResult, null);
				string text2 = "," + pResult + ",";
				string text3 = pResult ?? "";
				foreach (Kingdom crimeRealm in CrimeRealms)
				{
					if (crimeRealm != null && crimeRealm != victims)
					{
						string text4 = crimeRealm.data.id.ToString();
						if (!text2.Contains("," + text4 + ","))
						{
							text3 = ((text3.Length > 0) ? (text3 + "," + text4) : text4);
							text2 = text2 + text4 + ",";
						}
					}
				}
				if (text3 != (pResult ?? ""))
				{
					body.data.set(text, text3);
				}
			}
			catch
			{
			}
		}

		public static City TownAt(Vector2 at)
		{
			try
			{
				WorldTile tile = World.world.GetTile(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y));
				City city = ((tile != null && tile.zone != null) ? tile.zone.city : null);
				return (city != null && city.isAlive()) ? city : null;
			}
			catch
			{
				return null;
			}
		}

		public static void OnQuestDone(Actor body, City c, int renown, bool bounty = false)
		{
			if (body == null || c == null)
			{
				return;
			}
			try
			{
				int amount = (bounty ? Mathf.Clamp(100 + renown * 25, 100, 300) : Mathf.Clamp(75 + renown * 15, 75, 200));
				Rep.Add(body, c, amount, bounty ? "bounty claimed" : "task done");
			}
			catch (Exception e)
			{
				Fail("quest done", e);
			}
		}

		public static void OnBodyDying(Actor body)
		{
			try
			{
				Rep.Remember(body);
			}
			catch (Exception e)
			{
				Fail("rep dying", e);
			}
			try
			{
				Homes.OnBodyDying(body);
			}
			catch (Exception e2)
			{
				Fail("homes dying", e2);
			}
			try
			{
				LandPlot.OnBodyDying(body);
			}
			catch (Exception e3)
			{
				Fail("land dying", e3);
			}
		}

		public static void OnBodyDied(Actor body, Actor heir)
		{
			try
			{
				Rep.Inherit(body, heir);
			}
			catch (Exception e)
			{
				Fail("rep heir", e);
			}
			try
			{
				Homes.OnBodyDied(body, heir);
			}
			catch (Exception e2)
			{
				Fail("homes heir", e2);
			}
			try
			{
				LandPlot.OnBodyDied(body, heir);
			}
			catch (Exception e3)
			{
				Fail("land heir", e3);
			}
		}

		internal static void Fail(string what, Exception e)
		{
			float unscaledTime = Time.unscaledTime;
			if (!ErrorAt.TryGetValue(what, out var value) || !(unscaledTime - value < 30f))
			{
				ErrorAt[what] = unscaledTime;
				Log.Error("towns: " + what + ": " + e);
			}
		}
	}
}
