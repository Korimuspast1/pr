using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FirstPerson.Towns;

namespace FirstPerson
{
	internal static class Run
	{
		internal sealed class Record
		{
			public long Id;

			public string Founder = "";

			public int Generation = 1;

			public int StartYear;

			public int RenownAtStart;

			public int RenownGained;

			public int PeakGold;

			public int GoldAtStart;

			public int HousesAtStart;

			public int RuledAtStart;

			public int Quests;

			public int Trials;

			public int TraitsEarned;

			public int Founded;

			public int WarsWon;

			public int TownsTaken;

			public int Kingdoms;

			public bool Immortal;

			public int Spent;

			public string Serialize()
			{
				StringBuilder sb = new StringBuilder(160);
				Add("id", Id);
				Add("gen", Generation);
				Add("y0", StartYear);
				Add("r0", RenownAtStart);
				Add("rg", RenownGained);
				Add("peak", PeakGold);
				Add("g0", GoldAtStart);
				Add("h0", HousesAtStart);
				Add("t0", RuledAtStart);
				Add("q", Quests);
				Add("tr", Trials);
				Add("te", TraitsEarned);
				Add("f", Founded);
				Add("w", WarsWon);
				Add("tt", TownsTaken);
				Add("k", Kingdoms);
				Add("imm", Immortal ? 1 : 0);
				Add("sp", Spent);
				sb.Append("name=").Append(Founder.Replace(";", ",").Replace("=", "-"));
				return sb.ToString();
				void Add(string k, object v)
				{
					sb.Append(k).Append('=').Append(Convert.ToString(v, CultureInfo.InvariantCulture))
						.Append(';');
				}
			}

			public static Record Parse(string s)
			{
				if (string.IsNullOrEmpty(s))
				{
					return null;
				}
				Record record = new Record();
				string[] array = s.Split(';');
				foreach (string text in array)
				{
					int num = text.IndexOf('=');
					if (num > 0)
					{
						string text2 = text.Substring(0, num);
						string text3 = text.Substring(num + 1);
						int.TryParse(text3, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result);
						switch (text2)
						{
						case "id":
							long.TryParse(text3, NumberStyles.Integer, CultureInfo.InvariantCulture, out record.Id);
							break;
						case "gen":
							record.Generation = Math.Max(1, result);
							break;
						case "y0":
							record.StartYear = result;
							break;
						case "r0":
							record.RenownAtStart = result;
							break;
						case "rg":
							record.RenownGained = result;
							break;
						case "peak":
							record.PeakGold = result;
							break;
						case "g0":
							record.GoldAtStart = result;
							break;
						case "h0":
							record.HousesAtStart = result;
							break;
						case "t0":
							record.RuledAtStart = result;
							break;
						case "q":
							record.Quests = result;
							break;
						case "tr":
							record.Trials = result;
							break;
						case "te":
							record.TraitsEarned = result;
							break;
						case "f":
							record.Founded = result;
							break;
						case "w":
							record.WarsWon = result;
							break;
						case "tt":
							record.TownsTaken = result;
							break;
						case "k":
							record.Kingdoms = result;
							break;
						case "imm":
							record.Immortal = result != 0;
							break;
						case "sp":
							record.Spent = result;
							break;
						case "name":
							record.Founder = text3;
							break;
						}
					}
				}
				if (record.Id == 0L)
				{
					return null;
				}
				return record;
			}
		}

		internal enum Ask
		{
			None,
			Abandon,
			LeftBody
		}

		internal sealed class Legacy
		{
			public string When = "";

			public string Name = "";

			public string Species = "";

			public string How = "";

			public string Epitaph = "";

			public int Years;

			public int Generations;

			public int Survival;

			public int Wealth;

			public int Deeds;

			public int War;

			public int Total;

			public int Credits;

			public bool Abandoned;

			public int BudgetBefore;

			public int BudgetAfter;

			public long RunId;
		}

		public const string Key = "fp_run";

		internal static bool LockInOpen;

		internal static int Tab;

		internal static readonly HashSet<string> Picked = new HashSet<string>();

		internal static Ask Asking;

		internal static Actor LeapTo;

		internal static bool LegacyOpen;

		internal static Legacy LastLegacy;

		private static Record _run;

		private static Actor _body;

		private static float _peakCheck;

		private static string _seenName;

		private static int _seenRenown;

		private static int _seenHouses;

		private static int _seenRuled;

		private static Actor _died;

		private static long _diedId;

		private static object _world;

		private static Dictionary<string, int> _deadBag;

		private static List<Gear.Piece> _deadGear;

		private static int _deadBagTier;

		private static string _deadMoves;

		private static int _deadMove;

		internal static readonly Dictionary<string, int> Costs = new Dictionary<string, int>
		{
			["backstep"] = 1,
			["attractive"] = 1,
			["fertile"] = 1,
			["poison_immune"] = 1,
			["freeze_proof"] = 1,
			["acid_proof"] = 1,
			["titan_lungs"] = 1,
			["miner"] = 1,
			["honest"] = 1,
			["ambitious"] = 1,
			["agile"] = 2,
			["dash"] = 2,
			["block"] = 2,
			["dodge"] = 2,
			["deflect_projectile"] = 2,
			["eagle_eyed"] = 2,
			["immune"] = 2,
			["fire_proof"] = 2,
			["sunblessed"] = 2,
			["moonchild"] = 2,
			["nightchild"] = 2,
			["strong_minded"] = 2,
			["strong"] = 3,
			["tough"] = 3,
			["fast"] = 3,
			["hard_skin"] = 3,
			["boosted_vitality"] = 3,
			["lucky"] = 3,
			["genius"] = 3,
			["thorns"] = 3,
			["arcane_reflexes"] = 3,
			["healing_aura"] = 3,
			["regeneration"] = 4,
			["long_liver"] = 4,
			["heart_of_wizard"] = 4,
			["battle_reflexes"] = 4,
			["giant"] = 4,
			["chosen_one"] = 6,
			["ugly"] = -1,
			["fat"] = -1,
			["infertile"] = -1,
			["gluttonous"] = -1,
			["paranoid"] = -1,
			["hotheaded"] = -1,
			["clumsy"] = -2,
			["short_sighted"] = -2,
			["stupid"] = -2,
			["heliophobia"] = -2,
			["tiny"] = -2,
			["weak"] = -3,
			["slow"] = -3,
			["fragile_health"] = -3,
			["unlucky"] = -3
		};

		private static int _credits = -1;

		private static List<Legacy> _hall;

		private static readonly int[] Steps = new int[7] { 10, 30, 60, 100, 150, 220, 300 };

		private static bool _hallBroken;

		private static string _hallFileOverride;

		internal static bool WindowOpen
		{
			get
			{
				if (!LockInOpen && !LegacyOpen)
				{
					return Asking != Ask.None;
				}
				return true;
			}
		}

		internal static Record Current => _run;

		internal static Actor Body => _body;

		internal static bool Locked
		{
			get
			{
				WorldBoxMod instance = WorldBoxMod.Instance;
				if (_run != null && _body != null && instance != null)
				{
					if (instance.Host != _body)
					{
						return instance.Body == _body;
					}
					return true;
				}
				return false;
			}
		}

		internal static int Year
		{
			get
			{
				try
				{
					return Date.getCurrentYear();
				}
				catch
				{
					return 0;
				}
			}
		}

		private static string HallFile => _hallFileOverride ?? Path.Combine(Log.Folder, "runs.txt");

		internal static int Credits
		{
			get
			{
				LoadHall();
				return _credits;
			}
		}

		internal static IReadOnlyList<Legacy> Hall
		{
			get
			{
				LoadHall();
				return _hall;
			}
		}

		internal static string HallFileForTest
		{
			set
			{
				_hallFileOverride = value;
				_hall = null;
			}
		}

		internal static void CheckWorld()
		{
			object obj = World.world?.map_stats;
			if (obj != _world)
			{
				if (_world != null && _run != null)
				{
					Log.Info("run: another world loaded; the run stays in its body there");
					Forget();
				}
				if (_world != null)
				{
					GreatWar.Reset(null);
				}
				_world = obj;
			}
		}

		internal static bool LockedOn(Actor a)
		{
			if (_run != null && a != null)
			{
				return a == _body;
			}
			return false;
		}

		internal static bool CanLock(Actor a, out string why)
		{
			why = null;
			try
			{
				if (a == null || !a.isAlive())
				{
					why = "Take over someone first.";
					return false;
				}
				if (a.asset == null || (!a.asset.civ && !People.IsHumanoid(a)) || string.IsNullOrEmpty(a.asset.kingdom_id_civilization))
				{
					why = "Only people who can live in a town can be locked into: not " + WorldBoxMod.Describe(a) + ".";
					return false;
				}
				if (Crabzillas.Is(a))
				{
					why = "Not Crabzilla.";
					return false;
				}
			}
			catch
			{
				why = "This creature can't be locked into.";
				return false;
			}
			return true;
		}

		internal static bool Lock(Actor a, IEnumerable<string> traits, Action<string> toast)
		{
			if (!CanLock(a, out var why))
			{
				toast?.Invoke(why);
				return false;
			}
			int num = Budget();
			int num2 = 0;
			List<string> list = new List<string>();
			foreach (string item in (from t in traits.Where(Costs.ContainsKey)
				orderby (Costs[t] >= 0) ? 1 : 0
				select t).ToList())
			{
				int num3 = Costs[item];
				try
				{
					if (!a.hasTrait(item) && num2 + num3 <= num && !Clashes(a, item, list))
					{
						ActorTrait actorTrait = AssetManager.traits.get(item);
						if (actorTrait != null && a.addTrait(actorTrait, pRemoveOpposites: true))
						{
							num2 += num3;
							list.Add(item);
							TraitSwitch.Earned(a, item);
						}
					}
				}
				catch (Exception ex)
				{
					Log.Error("lock-in trait " + item + ": " + ex.Message);
				}
			}
			_run = new Record
			{
				Id = DateTime.UtcNow.Ticks,
				Founder = SafeName(a),
				StartYear = Year,
				RenownAtStart = RenownOf(a),
				PeakGold = MoneyOf(a),
				GoldAtStart = MoneyOf(a),
				HousesAtStart = HousesOf(a),
				RuledAtStart = RuledBy(a),
				Spent = num2
			};
			GreatWar.Reset(a);
			_body = a;
			_died = null;
			_diedId = 0L;
			_seenName = SafeName(a);
			_seenRenown = RenownOf(a);
			_seenHouses = _run.HousesAtStart;
			_seenRuled = _run.RuledAtStart;
			Save();
			Log.Info(string.Format("run locked into {0} with {1} traits ({2} of {3} points): {4}", WorldBoxMod.Describe(a), list.Count, num2, num, string.Join(", ", list)));
			toast?.Invoke("Locked in as " + SafeName(a) + ". Live long. (Esc > Give up the run ends it)");
			return true;
		}

		internal static void Save()
		{
			if (_run == null || _body == null)
			{
				return;
			}
			try
			{
				_body.data.set("fp_run", _run.Serialize());
			}
			catch (Exception ex)
			{
				Log.Error("run save: " + ex.Message);
			}
		}

		private static void Clear(Actor a)
		{
			try
			{
				a?.data.set("fp_run", "");
			}
			catch
			{
			}
		}

		internal static void Tick(Actor host, float dt)
		{
			if (host == null)
			{
				return;
			}
			CheckWorld();
			if (_run == null || _body != host)
			{
				Record record = null;
				try
				{
					host.data.get("fp_run", out var pResult, null);
					record = Record.Parse(pResult);
				}
				catch
				{
				}
				if (record != null && (_run == null || _body == null || !_body.isAlive()))
				{
					_run = record;
					_body = host;
					Log.Info($"run carried on in {WorldBoxMod.Describe(host)} (generation {_run.Generation})");
					WorldBoxMod.Instance?.Toast($"Your run goes on: {SafeName(host)}, generation {_run.Generation}.");
				}
			}
			if (!LockedOn(host))
			{
				return;
			}
			_peakCheck -= dt;
			if (!(_peakCheck <= 0f))
			{
				return;
			}
			_peakCheck = 1f;
			try
			{
				if (host.isAlive() && host.data != null)
				{
					_seenName = SafeName(host);
					_seenRenown = RenownOf(host);
					_seenHouses = HousesOf(host);
					_seenRuled = RuledBy(host);
				}
			}
			catch
			{
			}
			int num = MoneyOf(host);
			if (num > _run.PeakGold)
			{
				_run.PeakGold = num;
				Save();
			}
		}

		internal static void Tally(Actor who, string what, int n = 1)
		{
			if (_run != null && who != null && who == _body)
			{
				switch (what)
				{
				case "quest":
					_run.Quests += n;
					break;
				case "trial":
					_run.Trials += n;
					break;
				case "trait":
					_run.TraitsEarned += n;
					break;
				case "founded":
					_run.Founded += n;
					break;
				case "war":
					_run.WarsWon += n;
					break;
				case "town":
					_run.TownsTaken += n;
					break;
				case "kingdom":
					_run.Kingdoms += n;
					break;
				case "immortal":
					_run.Immortal = true;
					break;
				}
				Save();
			}
		}

		internal static bool HasHome(Actor a)
		{
			try
			{
				if (a == null)
				{
					return false;
				}
				if (a.getHomeBuilding() != null)
				{
					return true;
				}
				return Homes.Owned(a).Any();
			}
			catch
			{
				return false;
			}
		}

		internal static void OnDeath(Actor dead)
		{
			if (!LockedOn(dead))
			{
				return;
			}
			_died = dead;
			try
			{
				_diedId = dead.data.id;
				_seenName = SafeName(dead);
				_seenRenown = RenownOf(dead);
			}
			catch
			{
			}
			_deadBag = new Dictionary<string, int>();
			try
			{
				Dictionary<string, ResourceContainer> dictionary = dead.inventory?.getResources();
				if (dictionary != null)
				{
					foreach (KeyValuePair<string, ResourceContainer> item in dictionary)
					{
						if (item.Value.amount > 0)
						{
							_deadBag[item.Key] = item.Value.amount;
						}
					}
				}
				Supplies.Kind[] all = Supplies.All;
				foreach (Supplies.Kind kind in all)
				{
					int num = Supplies.Count(dead, kind.Id);
					if (num > 0)
					{
						_deadBag[kind.Id] = num;
					}
				}
			}
			catch
			{
			}
			_deadGear = new List<Gear.Piece>(Gear.Of(dead));
			_deadBagTier = Bag.Tier(dead);
			Moves.Read(dead, out _deadMoves, out _deadMove);
		}

		internal static void PassTo(Actor dead, Actor heir)
		{
			if (_run == null || dead == null || heir == null || dead != _body)
			{
				return;
			}
			_run.RenownGained += Math.Max(0, (Gone(dead) ? _seenRenown : RenownOf(dead)) - _run.RenownAtStart);
			_run.RenownAtStart = RenownOf(heir);
			_run.Generation++;
			Dictionary<string, int> deadBag = _deadBag;
			int num = deadBag?.Values.Sum() ?? 0;
			_deadBag = null;
			try
			{
				if (dead.data != null && dead.data.id == _diedId)
				{
					Clear(dead);
				}
			}
			catch
			{
			}
			_body = heir;
			Save();
			GreatWar.Inherit(_diedId, heir);
			Moves.PassOn(_deadMoves, _deadMove, heir);
			_deadMoves = null;
			Bag.SetTier(heir, _deadBagTier);
			if (deadBag != null)
			{
				foreach (KeyValuePair<string, int> item in deadBag)
				{
					try
					{
						Bag.Put(heir, item.Key, item.Value, spill: true);
					}
					catch
					{
					}
				}
			}
			int num2 = _deadGear?.Count ?? 0;
			if (_deadGear != null)
			{
				foreach (Gear.Piece item2 in _deadGear)
				{
					Bag.PutPiece(heir, item2);
				}
			}
			_deadGear = null;
			Log.Info($"run passed to {WorldBoxMod.Describe(heir)}: generation {_run.Generation}, {num} things from the bag, {num2} spare pieces, a {Bag.Name(heir).ToLowerInvariant()}");
		}

		internal static Legacy End(string how, bool abandoned)
		{
			if (_run == null)
			{
				return null;
			}
			Legacy legacy = Score(_run, _body, how, abandoned);
			WriteHall(legacy);
			Clear(_body);
			Log.Info(string.Format("run over ({0}{1}): score {2}, {3} credits", how, abandoned ? ", abandoned" : "", legacy.Total, legacy.Credits));
			_run = null;
			_body = null;
			_deadBag = null;
			LastLegacy = legacy;
			LegacyOpen = true;
			return legacy;
		}

		private static int RenownOf(Actor a)
		{
			try
			{
				return a.data.renown;
			}
			catch
			{
				return 0;
			}
		}

		private static int MoneyOf(Actor a)
		{
			try
			{
				return a.money;
			}
			catch
			{
				return 0;
			}
		}

		internal static string SafeName(Actor a)
		{
			try
			{
				string name = a.getName();
				return string.IsNullOrEmpty(name) ? WorldBoxMod.Describe(a) : name;
			}
			catch
			{
				return "someone";
			}
		}

		internal static void Forget()
		{
			_run = null;
			_body = null;
			_deadBag = null;
		}

		internal static bool Opposed(string a, string b)
		{
			try
			{
				ActorTrait actorTrait = AssetManager.traits.get(a);
				ActorTrait actorTrait2 = AssetManager.traits.get(b);
				if (actorTrait == null || actorTrait2 == null)
				{
					return false;
				}
				return (actorTrait.opposite_traits != null && actorTrait.opposite_traits.Contains(actorTrait2)) || (actorTrait2.opposite_traits != null && actorTrait2.opposite_traits.Contains(actorTrait)) || (actorTrait.opposite_list != null && actorTrait.opposite_list.Contains(b)) || (actorTrait2.opposite_list != null && actorTrait2.opposite_list.Contains(a));
			}
			catch
			{
				return false;
			}
		}

		internal static bool Clashes(Actor a, string id, IEnumerable<string> picked)
		{
			foreach (string item in picked)
			{
				if (item != id && Opposed(id, item))
				{
					return true;
				}
			}
			try
			{
				foreach (string key in Costs.Keys)
				{
					if (key != id && a.hasTrait(key) && Opposed(id, key))
					{
						return true;
					}
				}
			}
			catch
			{
			}
			return false;
		}

		internal static int PointsLeft(IEnumerable<string> picked, int budget)
		{
			int num = budget;
			foreach (string item in picked)
			{
				if (Costs.TryGetValue(item, out var value))
				{
					num -= value;
				}
			}
			return num;
		}

		private static Legacy Score(Record r, Actor last, string how, bool abandoned)
		{
			Legacy legacy = new Legacy
			{
				When = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
				Name = (Gone(last) ? (_seenName ?? r.Founder) : SafeName(last)),
				RunId = r.Id,
				How = how,
				Abandoned = abandoned,
				Generations = r.Generation,
				Years = Math.Max(0, Year - r.StartYear),
				BudgetBefore = Budget()
			};
			try
			{
				legacy.Species = ((last != null && last.asset != null) ? last.asset.id.Replace('_', ' ') : "");
			}
			catch
			{
			}
			bool flag = Gone(last);
			int num = r.RenownGained + Math.Max(0, (flag ? _seenRenown : RenownOf(last)) - r.RenownAtStart);
			int num2 = (flag ? _seenHouses : HousesOf(last));
			int num3 = (flag ? _seenRuled : RuledBy(last));
			legacy.Survival = 2 * legacy.Years + 25 * (r.Generation - 1);
			legacy.Wealth = Math.Max(0, r.PeakGold - r.GoldAtStart) / 10 + 50 * Math.Max(0, num2 - r.HousesAtStart) + 150 * r.Founded + 30 * Math.Max(0, num3 - r.RuledAtStart);
			legacy.Deeds = num + 10 * r.Quests + 40 * r.Trials + 20 * r.TraitsEarned;
			legacy.War = 100 * r.WarsWon + 40 * r.TownsTaken + 300 * r.Kingdoms + (r.Immortal ? 2000 : 0);
			legacy.Total = legacy.Survival + legacy.Wealth + legacy.Deeds + legacy.War;
			legacy.Credits = (abandoned ? (legacy.Total / 200) : (legacy.Total / 100));
			legacy.Epitaph = Epitaph(legacy, r, num3, num2);
			return legacy;
		}

		private static bool Gone(Actor a)
		{
			try
			{
				return a == null || a == _died || a.data == null || !a.isAlive();
			}
			catch
			{
				return true;
			}
		}

		private static int HousesOf(Actor a)
		{
			try
			{
				return (a != null && a.data != null) ? Homes.CountOwned(a) : 0;
			}
			catch
			{
				return 0;
			}
		}

		private static int RuledBy(Actor a)
		{
			int num = 0;
			try
			{
				if (a == null || a.data == null)
				{
					return 0;
				}
				foreach (City item in World.world.cities.list)
				{
					if (item != null && item.isAlive() && (item.leader == a || (item.kingdom != null && item.kingdom.king == a)))
					{
						num++;
					}
				}
			}
			catch
			{
			}
			return num;
		}

		private static string Epitaph(Legacy l, Record r, int ruled, int houses)
		{
			if (r.Immortal)
			{
				return l.Name + " beat death itself.";
			}
			if (r.Kingdoms > 0)
			{
				return l.Name + " wiped a kingdom from the map.";
			}
			if (ruled > 1)
			{
				return $"{l.Name} ruled {ruled} towns.";
			}
			if (ruled == 1)
			{
				return l.Name + " led a town.";
			}
			if (r.Founded > 0)
			{
				return l.Name + " founded a town.";
			}
			if (l.Generations > 2)
			{
				return $"The line of {r.Founder} went on for {l.Generations} generations.";
			}
			if (houses > 0)
			{
				return l.Name + " had a home of their own.";
			}
			if (r.Quests >= 5)
			{
				return l.Name + " was always ready to help.";
			}
			if (l.Years < 5)
			{
				return l.Name + " did not last long.";
			}
			return $"{l.Name} lived {l.Years} years.";
		}

		private static int Threshold(int k)
		{
			if (k >= Steps.Length)
			{
				return Steps[Steps.Length - 1] + 120 * (k - Steps.Length + 1);
			}
			return Steps[k];
		}

		internal static int Budget()
		{
			return BudgetFor(Credits);
		}

		internal static int BudgetFor(int credits)
		{
			int num = 3;
			for (int i = 0; i < 200 && credits >= Threshold(i); i++)
			{
				num++;
			}
			return num;
		}

		internal static int ToNextPoint()
		{
			int credits = Credits;
			for (int i = 0; i < 200; i++)
			{
				if (credits < Threshold(i))
				{
					return Threshold(i) - credits;
				}
			}
			return 0;
		}

		private static void LoadHall()
		{
			if (_hall != null)
			{
				return;
			}
			_hall = new List<Legacy>();
			_credits = 0;
			_hallBroken = false;
			try
			{
				if (!File.Exists(HallFile))
				{
					return;
				}
				string[] array = File.ReadAllLines(HallFile);
				foreach (string text in array)
				{
					if (text.StartsWith("credits="))
					{
						int.TryParse(text.Substring(8), out _credits);
						continue;
					}
					string[] array2 = text.Split('|');
					if (array2.Length >= 10)
					{
						Legacy legacy = new Legacy
						{
							When = array2[0],
							Name = array2[1],
							Species = array2[2],
							How = array2[8],
							Epitaph = ((array2.Length > 9) ? array2[9] : "")
						};
						int.TryParse(array2[3], out legacy.Years);
						int.TryParse(array2[4], out legacy.Generations);
						int.TryParse(array2[5], out legacy.Total);
						int.TryParse(array2[6], out legacy.Credits);
						legacy.Abandoned = array2[7] == "1";
						if (array2.Length > 10)
						{
							long.TryParse(array2[10], out legacy.RunId);
						}
						_hall.Insert(0, legacy);
					}
				}
			}
			catch (Exception ex)
			{
				_hallBroken = true;
				Log.Error("runs.txt: " + ex.Message);
			}
		}

		private static void WriteHall(Legacy l)
		{
			LoadHall();
			if (l.RunId != 0L && _hall.Exists((Legacy h) => h.RunId == l.RunId))
			{
				l.Credits = 0;
				l.How += " (ended before: no credits again)";
			}
			_credits += l.Credits;
			l.BudgetAfter = BudgetFor(_credits);
			_hall.Insert(0, l);
			try
			{
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.AppendLine("credits=" + _credits);
				for (int num = _hall.Count - 1; num >= 0; num--)
				{
					Legacy legacy = _hall[num];
					stringBuilder.AppendLine(string.Join("|", Clean(legacy.When), Clean(legacy.Name), Clean(legacy.Species), legacy.Years, legacy.Generations, legacy.Total, legacy.Credits, legacy.Abandoned ? 1 : 0, Clean(legacy.How), Clean(legacy.Epitaph), legacy.RunId));
				}
				if (_hallBroken)
				{
					Log.Error("runs.txt: not written over (it couldn't be read)");
					return;
				}
				string text = HallFile + ".new";
				File.WriteAllText(text, stringBuilder.ToString());
				if (File.Exists(HallFile))
				{
					File.Replace(text, HallFile, null);
				}
				else
				{
					File.Move(text, HallFile);
				}
			}
			catch (Exception ex)
			{
				Log.Error("runs.txt: " + ex.Message);
			}
			string Clean(string s)
			{
				return (s ?? "").Replace('|', '/').Replace('\n', ' ');
			}
		}
	}
}
