using System;
using System.Collections.Generic;
using System.Text;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class Law
	{
		internal sealed class Board
		{
			public City City;

			public long Id;

			public Vector2 At;

			public float Yaw;

			public float Ground;

			public readonly List<Poster> Posters = new List<Poster>();

			public float PostersAt = -99f;
		}

		public const string WantedKey = "fp_wanted";

		public const string FavourKey = "fp_favour";

		public const string FavourTimeKey = "fp_favour_t";

		public const float Witness = 11f;

		public const float GuardSight = 20f;

		public const int BreakInFine = 35;

		public const int ArsonFine = 60;

		public const int MurderFine = 100;

		public const int TheftFine = 40;

		public const int AssaultFine = 25;

		public static string LastCrime;

		public static int LastWitnesses;

		private static float _guardTimer;

		private static float _theftAt = -99f;

		private static Actor _theftVictim;

		private static readonly List<Actor> Scratch = new List<Actor>();

		private static readonly StringBuilder Text = new StringBuilder(128);

		private static List<KeyValuePair<Kingdom, int>> _wanted = new List<KeyValuePair<Kingdom, int>>();

		private static long _wantedFor = -1L;

		private static float _wantedAt = -99f;

		private static readonly Dictionary<long, bool> Fighters = new Dictionary<long, bool>();

		private static readonly List<Actor> OwnScratch = new List<Actor>();

		public static bool LastWar;

		public static int LastOwnWitnesses;

		public const int ShipTheftFine = 80;

		private static readonly Dictionary<long, float> Struck = new Dictionary<long, float>();

		private static object _recordsWorld;

		private static float _lastGuardToast = -99f;

		private static readonly HashSet<long> _atWarWith = new HashSet<long>();

		private static readonly List<long> _peaceScratch = new List<long>();

		private static bool _warsKnown;

		private static float _warsTimer;

		private static Kingdom _warsOf;

		private static bool _favourRegistered;

		private static readonly Dictionary<City, Board> Boards = new Dictionary<City, Board>();

		private static float _boardsAt = -99f;

		private static object _boardsWorld;

		public static bool BoardOpen;

		public static Board OpenBoard;

		private static readonly List<Board> _facingScratch = new List<Board>();

		public static void LootSeen(Kingdom k, Vector2 at, string whose, Action<string> toast)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			Actor actor = ((instance != null) ? instance.Body : null);
			try
			{
				if (actor == null || k == null || !k.isCiv() || (actor.kingdom != null && actor.kingdom != k && actor.kingdom.isEnemy(k)) || Peace.PeaceLately(actor.kingdom, k))
				{
					return;
				}
				Scratch.Clear();
				foreach (Actor item in Civil.SubjectsOf(k))
				{
					if (item != null && item != actor && item.isAlive() && item.isAdult() && !item.is_inside_building && !((item.current_position - at).sqrMagnitude > 121f))
					{
						Scratch.Add(item);
						Chronicle.Note(item, actor, 0, 0, 0, 1);
						Chronicle.ChangeOpinion(item, actor, -25);
					}
				}
				int num = (LastWitnesses = Scratch.Count);
				LastCrime = "theft";
				if (num == 0)
				{
					Log.Info("law: robbing " + whose + "'s body, unseen");
					return;
				}
				int num2 = BountyOn(k, actor) + 40;
				SetBounty(k, actor, num2);
				toast?.Invoke($"{num} saw you rob {whose}'s body! Wanted in {k.name}: {num2} gold");
				Hub.OnCrime(actor, at, k, Scratch, "theft");
				Log.Info($"law: robbing {whose}'s body seen by {num}; bounty in {k.name} now {num2}");
				_guardTimer = 0f;
			}
			catch (Exception ex)
			{
				Log.Error("law: loot: " + ex.Message);
			}
		}

		public static void ArsonSeen(Kingdom k, Vector2 at, Action<string> toast)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			Actor actor = ((instance != null) ? instance.Body : null);
			try
			{
				if (actor == null || k == null || !k.isCiv() || (actor.kingdom != null && actor.kingdom != k && actor.kingdom.isEnemy(k)) || Peace.PeaceLately(actor.kingdom, k))
				{
					return;
				}
				Witnesses(k, null, actor, at, Scratch);
				foreach (Actor item in Scratch)
				{
					Chronicle.Note(item, actor, 0, 0, 0, 1);
					Chronicle.ChangeOpinion(item, actor, -30);
				}
				int num = (LastWitnesses = Scratch.Count);
				LastCrime = "arson";
				if (num == 0)
				{
					Log.Info("law: arson in " + k.name + ", unseen");
					return;
				}
				int num2 = BountyOn(k, actor) + 60;
				SetBounty(k, actor, num2);
				toast?.Invoke($"{num} saw you set the fire. Wanted in {k.name}: {num2} gold");
				Hub.OnCrime(actor, at, k, Scratch, "arson");
				Log.Info($"law: arson seen by {num}; bounty in {k.name} now {num2}");
				_guardTimer = 0f;
			}
			catch (Exception ex)
			{
				Log.Error("law: arson: " + ex.Message);
			}
		}

		public static void BreakIn(Building house, List<Actor> inside, Action<string> toast)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			Actor actor = ((instance != null) ? instance.Body : null);
			try
			{
				Kingdom kingdom = house?.kingdom;
				if (actor == null || kingdom == null || !kingdom.isCiv() || (actor.kingdom != null && actor.kingdom != kingdom && actor.kingdom.isEnemy(kingdom)) || Rep.Reigns(actor, kingdom))
				{
					return;
				}
				Vector2 current_position = house.current_position;
				Witnesses(kingdom, null, actor, current_position, Scratch);
				foreach (Actor item in inside)
				{
					if (item != null && !Scratch.Contains(item))
					{
						Scratch.Add(item);
					}
				}
				foreach (Actor item2 in Scratch)
				{
					Chronicle.Note(item2, actor, 0, 0, 0, 1);
					Chronicle.ChangeOpinion(item2, actor, -20);
				}
				int num = (LastWitnesses = Scratch.Count);
				LastCrime = "breaking in";
				if (num == 0)
				{
					Log.Info("law: a break-in in " + kingdom.name + ", unseen");
					return;
				}
				int num2 = BountyOn(kingdom, actor) + 35;
				SetBounty(kingdom, actor, num2);
				toast?.Invoke($"{num} heard you break in. Wanted in {kingdom.name}: {num2} gold");
				Hub.OnCrime(actor, current_position, kingdom, Scratch, "breaking in");
				Log.Info($"law: a break-in heard by {num}; bounty in {kingdom.name} now {num2}");
				_guardTimer = 0f;
			}
			catch (Exception ex)
			{
				Log.Error("law: break-in: " + ex.Message);
			}
		}

		public static int BountyOn(Kingdom k, Actor body)
		{
			if (k == null || body == null || body.data == null)
			{
				return 0;
			}
			foreach (KeyValuePair<long, int> item in Read(k))
			{
				if (item.Key == body.data.id)
				{
					return item.Value;
				}
			}
			return 0;
		}

		private static List<KeyValuePair<long, int>> Read(Kingdom k)
		{
			List<KeyValuePair<long, int>> list = new List<KeyValuePair<long, int>>();
			try
			{
				k.data.get("fp_wanted", out var pResult, null);
				if (string.IsNullOrEmpty(pResult))
				{
					return list;
				}
				string[] array = pResult.Split('|');
				foreach (string text in array)
				{
					int num = text.IndexOf(':');
					if (num > 0 && long.TryParse(text.Substring(0, num), out var result) && int.TryParse(text.Substring(num + 1), out var result2) && result2 > 0)
					{
						list.Add(new KeyValuePair<long, int>(result, result2));
					}
				}
			}
			catch
			{
			}
			return list;
		}

		private static void Write(Kingdom k, List<KeyValuePair<long, int>> list)
		{
			_wantedAt = -99f;
			Text.Length = 0;
			foreach (KeyValuePair<long, int> item in list)
			{
				if (item.Value > 0)
				{
					if (Text.Length > 0)
					{
						Text.Append('|');
					}
					Text.Append(item.Key).Append(':').Append(item.Value);
				}
			}
			try
			{
				if (Text.Length == 0)
				{
					k.data.removeString("fp_wanted");
				}
				else
				{
					k.data.set("fp_wanted", Text.ToString());
				}
			}
			catch (Exception ex)
			{
				Log.Error("law: " + ex.Message);
			}
		}

		public static void SetBounty(Kingdom k, Actor body, int amount)
		{
			if (k != null && body != null)
			{
				List<KeyValuePair<long, int>> list = Read(k);
				list.RemoveAll((KeyValuePair<long, int> kv) => kv.Key == body.data.id);
				if (amount > 0)
				{
					list.Add(new KeyValuePair<long, int>(body.data.id, Mathf.Min(amount, 99999)));
				}
				Write(k, list);
			}
		}

		public static List<KeyValuePair<Kingdom, int>> WantedIn(Actor body)
		{
			List<KeyValuePair<Kingdom, int>> list = new List<KeyValuePair<Kingdom, int>>();
			if (body == null)
			{
				return list;
			}
			try
			{
				foreach (Kingdom item in World.world.kingdoms.list)
				{
					if (item != null && item.isAlive() && item.isCiv())
					{
						int num = BountyOn(item, body);
						if (num > 0)
						{
							list.Add(new KeyValuePair<Kingdom, int>(item, num));
						}
					}
				}
			}
			catch
			{
			}
			return list;
		}

		public static List<KeyValuePair<Kingdom, int>> WantedCached(Actor body)
		{
			if (body == null)
			{
				return _wanted;
			}
			if (body.data.id != _wantedFor || Time.unscaledTime - _wantedAt > 0.5f)
			{
				_wanted = WantedIn(body);
				_wantedFor = body.data.id;
				_wantedAt = Time.unscaledTime;
			}
			return _wanted;
		}

		private static bool ThiefYouInvestigate(long id)
		{
			foreach (Quest item in Quests.Log)
			{
				if (item.Kind == QuestKind.Investigation && item.Stage < 2 && item.Target == id)
				{
					return true;
				}
			}
			return false;
		}

		private static bool Protected(Actor victim, Actor you)
		{
			try
			{
				if (victim == null || victim == you || !victim.isSapient())
				{
					return false;
				}
				Kingdom kingdom = victim.kingdom;
				if (kingdom == null || !kingdom.isCiv())
				{
					return false;
				}
				if (you.kingdom != null && you.kingdom != kingdom && you.kingdom.isEnemy(kingdom))
				{
					return false;
				}
				if (CombatFeedback.BeganAtWar(victim) && (Peace.PeaceLately(you.kingdom, kingdom) || victim.attack_target == you || CombatFeedback.StruckAtYou(victim)))
				{
					return false;
				}
				if (victim.hasTrait("thief") && ThiefYouInvestigate(victim.data.id))
				{
					return false;
				}
				if (CombatFeedback.TheyStarted(victim, you))
				{
					return false;
				}
				return true;
			}
			catch
			{
				return false;
			}
		}

		public static bool Lawful(Actor target, Actor you)
		{
			if (target == null || you == null)
			{
				return true;
			}
			if (!Protected(target, you))
			{
				return !WarCivilian(target, you);
			}
			return false;
		}

		public static bool WarCivilian(Actor victim, Actor you)
		{
			try
			{
				if (victim == null || you == null || victim == you || !victim.isSapient())
				{
					return false;
				}
				Kingdom kingdom = victim.kingdom;
				Kingdom kingdom2 = you.kingdom;
				if (kingdom == null || !kingdom.isCiv() || kingdom2 == null || kingdom2 == kingdom || !kingdom2.isEnemy(kingdom))
				{
					return false;
				}
				if (Fighters.TryGetValue(victim.data.id, out var value))
				{
					if (value)
					{
						return false;
					}
				}
				else if (!victim.isAlive() || Fighter(victim))
				{
					return false;
				}
				return !CombatFeedback.StruckFirst(victim);
			}
			catch
			{
				return false;
			}
		}

		private static bool Fighter(Actor a)
		{
			if (!a.isWarrior() && !a.isKing())
			{
				return a.isCityLeader();
			}
			return true;
		}

		public static void Striking(Actor victim)
		{
			try
			{
				if (victim != null)
				{
					if (Fighters.Count > 256)
					{
						Fighters.Clear();
					}
					Fighters[victim.data.id] = Fighter(victim);
				}
			}
			catch
			{
			}
		}

		public static List<Actor> Witnesses(Actor victim, Actor you, Vector2 at)
		{
			return Witnesses(victim.kingdom, victim, you, at, Scratch);
		}

		private static List<Actor> Witnesses(Kingdom k, Actor victim, Actor you, Vector2 at, List<Actor> into)
		{
			into.Clear();
			try
			{
				if (RoomCombat.Witnesses(k, victim, you, into))
				{
					return into;
				}
				if (Curtains.Witnesses(k, victim, you, at, 11f, into))
				{
					return into;
				}
				foreach (Actor item in Civil.SubjectsOf(k))
				{
					if (item != null && item != victim && item != you && item.isAlive() && item.isAdult() && !item.is_inside_building && (item.current_position - at).sqrMagnitude <= 121f)
					{
						into.Add(item);
					}
				}
			}
			catch
			{
			}
			return into;
		}

		private static void Crime(Actor victim, string what, int fine, Action<string> toast)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			Actor actor = ((instance != null) ? instance.Body : null);
			if (actor == null)
			{
				return;
			}
			bool flag = WarCivilian(victim, actor);
			if (!flag && !Protected(victim, actor))
			{
				return;
			}
			Vector2 current_position = victim.current_position;
			List<Actor> list = Witnesses(victim, actor, current_position);
			bool flag2 = false;
			try
			{
				flag2 = actor.isKing() || Service.IsSoldier(actor);
			}
			catch
			{
			}
			List<Actor> list2 = ((flag && !flag2) ? Witnesses(actor.kingdom, victim, actor, current_position, OwnScratch) : null);
			LastWar = flag;
			LastOwnWitnesses = list2?.Count ?? 0;
			LastWitnesses = list.Count;
			LastCrime = what;
			if (LastOwnWitnesses > 0)
			{
				foreach (Actor item in list2)
				{
					Chronicle.Note(item, actor, 0, 0, 0, 1);
					Chronicle.ChangeOpinion(item, actor, (what == "murder") ? (-45) : (-25));
				}
			}
			if (list.Count == 0)
			{
				if (LastOwnWitnesses > 0)
				{
					toast?.Invoke($"{LastOwnWitnesses} of your own saw your war crime");
					OwnRealmHears(actor, current_position, what, theirsSaw: false);
				}
				Log.Info("law: " + (flag ? "war crime, " : "") + what + " of " + WorldBoxMod.Describe(victim) + ", unseen" + (flag ? " by theirs" : ""));
				return;
			}
			Kingdom kingdom = victim.kingdom;
			int num = BountyOn(kingdom, actor) + fine;
			SetBounty(kingdom, actor, num);
			foreach (Actor item2 in list)
			{
				Chronicle.Note(item2, actor, 0, 0, 0, 1);
				Chronicle.ChangeOpinion(item2, actor, (what == "murder") ? (-45) : (-25));
			}
			if (what != "murder")
			{
				Chronicle.Note(victim, actor, 0, 0, 0, 1);
				Chronicle.ChangeOpinion(victim, actor, -40);
			}
			toast?.Invoke(flag ? $"{list.Count} saw your war crime! Wanted in {kingdom.name}: {num} gold" : $"{list.Count} saw your {what}! Wanted in {kingdom.name}: {num} gold");
			Hub.OnCrime(actor, current_position, kingdom, list, what, flag && flag2);
			if (LastOwnWitnesses > 0)
			{
				OwnRealmHears(actor, current_position, what, theirsSaw: true);
			}
			Log.Info(string.Format("law: {0}{1} of {2} seen by {3}; bounty in {4} now {5}", flag ? "war crime, " : "", what, WorldBoxMod.Describe(victim), list.Count, kingdom.name, num));
			_guardTimer = 0f;
		}

		private static void OwnRealmHears(Actor you, Vector2 at, string what, bool theirsSaw)
		{
			City city = Hub.TownAt(at);
			if (!theirsSaw || city == null || city.kingdom != you.kingdom)
			{
				Hub.OnWarCrimeSeen(you, you.kingdom, what);
				Log.Info($"law: war crime ({what}) seen by {LastOwnWitnesses} of your own");
			}
		}

		public static void TookShip(Actor ship, Actor you, Action<string> toast)
		{
			Kingdom kingdom = ship.kingdom;
			if (you == null || kingdom == null || !kingdom.isCiv() || you.kingdom == kingdom || (you.kingdom != null && you.kingdom.isEnemy(kingdom)) || Peace.PeaceLately(you.kingdom, kingdom))
			{
				return;
			}
			List<Actor> list = Witnesses(ship, you, ship.current_position);
			LastWitnesses = list.Count;
			LastCrime = "theft";
			if (list.Count == 0)
			{
				Log.Info("law: theft of " + WorldBoxMod.Describe(ship) + ", unseen");
				return;
			}
			int num = BountyOn(kingdom, you) + 80;
			SetBounty(kingdom, you, num);
			foreach (Actor item in list)
			{
				Chronicle.Note(item, you, 0, 0, 0, 1);
				Chronicle.ChangeOpinion(item, you, -25);
			}
			toast?.Invoke($"{list.Count} saw you take the ship. Wanted in {kingdom.name} for {num} gold");
			Hub.OnCrime(you, ship.current_position, kingdom, list, "theft");
			Log.Info($"law: theft of {WorldBoxMod.Describe(ship)} seen by {list.Count}; bounty in {kingdom.name} now {num}");
			_guardTimer = 0f;
		}

		public static void OnKill(Actor victim)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			Crime(victim, "murder", 100, (instance != null) ? new Action<string>(instance.Toast) : null);
		}

		public static void OnHit(Actor victim)
		{
			if (victim != null && !Quests.Sparring(victim))
			{
				Economy.OnYourHit(victim);
				long id = victim.data.id;
				if (!Struck.TryGetValue(id, out var value) || !(Time.unscaledTime - value < 30f))
				{
					Struck[id] = Time.unscaledTime;
					WorldBoxMod instance = WorldBoxMod.Instance;
					Crime(victim, "assault", 25, (instance != null) ? new Action<string>(instance.Toast) : null);
				}
			}
		}

		public static void WatchTheft(Actor candidate)
		{
			if (candidate != null)
			{
				_theftVictim = candidate;
				_theftAt = Time.unscaledTime;
			}
		}

		public static void Update(Actor you, float dt, Action<string> toast)
		{
			object obj = World.world?.map_stats;
			if (obj != _recordsWorld)
			{
				_recordsWorld = obj;
				Fighters.Clear();
				Struck.Clear();
				_atWarWith.Clear();
				_warsKnown = false;
			}
			if (you == null)
			{
				return;
			}
			PeaceMade(you, dt);
			if (_theftVictim != null && Time.unscaledTime - _theftAt < 1.5f)
			{
				bool flag = false;
				try
				{
					flag = _theftVictim.hasHappinessEntry("got_robbed", 3f);
				}
				catch
				{
				}
				if (flag)
				{
					Actor theftVictim = _theftVictim;
					_theftVictim = null;
					Crime(theftVictim, "theft", 40, toast);
				}
			}
			else
			{
				_theftVictim = null;
			}
			_guardTimer -= dt;
			if (_guardTimer > 0f)
			{
				return;
			}
			_guardTimer = 2f;
			try
			{
				SendGuards(you, toast);
			}
			catch (Exception ex)
			{
				Log.Error("law: guards: " + ex.Message);
			}
		}

		private static void SendGuards(Actor you, Action<string> toast)
		{
			foreach (KeyValuePair<Kingdom, int> item in WantedIn(you))
			{
				Kingdom key = item.Key;
				int value = item.Value;
				int num = 0;
				foreach (Actor item2 in Civil.SubjectsOf(key))
				{
					if (item2 == null || item2 == you || !item2.isAlive() || !item2.isAdult() || item2.is_inside_building || (!item2.isWarrior() && !item2.isCityLeader() && !item2.isKing() && value < 200) || (item2.current_position - you.current_position).sqrMagnitude > 400f)
					{
						continue;
					}
					if (item2.has_attack_target && item2.attack_target == you)
					{
						num++;
						continue;
					}
					if (num >= 6)
					{
						break;
					}
					try
					{
						item2.addAggro(you);
						item2.startFightingWith(you);
						num++;
					}
					catch
					{
					}
					if (RoomCombat.Active)
					{
						RoomCombat.Pursue(item2);
					}
				}
				if (num > 0 && Time.unscaledTime - _lastGuardToast > 12f)
				{
					_lastGuardToast = Time.unscaledTime;
					toast?.Invoke($"Guards of {key.name} come for you! ({value} gold on your head; pay a leader, or flee)");
				}
			}
		}

		public static void Choices(Conversation c, List<Choice> list)
		{
			Actor with = c.With;
			Actor you = c.You;
			Kingdom k = with.kingdom;
			if (k == null || !k.isCiv())
			{
				return;
			}
			bool flag = with.isKing() || with.isCityLeader() || with.isWarrior();
			int bounty = BountyOn(k, you);
			if (bounty > 0 && flag)
			{
				int num = 0;
				try
				{
					num = you.money;
				}
				catch
				{
				}
				Choice choice = new Choice($"I'll pay my fine. ({bounty} gold)", delegate
				{
					PayFine(c, k, bounty);
				}, ChoiceTone.Risk);
				if (num < bounty)
				{
					choice.Off($"You have only {num} gold.");
				}
				list.Add(choice);
			}
			if (!with.isKing() && !with.isCityLeader())
			{
				return;
			}
			foreach (Quest item in Quests.Log)
			{
				if (!item.FromBoard || item.Kind != QuestKind.Bounty || item.Stage != 1)
				{
					continue;
				}
				City city = Quests.CityById(item.Giver);
				if (city != null && city.kingdom == k)
				{
					Quest qq = item;
					list.Add(new Choice("I've come for the bounty on the " + item.TargetName + ".", delegate
					{
						Quests.Reward(qq, you, c.Toast);
						c.Say(Dialogue.Pick("Good riddance. Here's your reward.", "The board pays its debts. Well done."));
						c.Offer(Dialogue.Topics(c));
					}, ChoiceTone.Quest));
				}
			}
		}

		private static void PayFine(Conversation c, Kingdom k, int bounty)
		{
			c.LastTopic = "fine";
			try
			{
				if (c.You.money < bounty)
				{
					c.Say("That's not enough.");
					c.Offer(Dialogue.Topics(c));
					return;
				}
				City town = null;
				try
				{
					if (c.With != null && c.With.city != null && c.With.city.kingdom == k)
					{
						town = c.With.city;
					}
				}
				catch
				{
				}
				if (!Economy.Pay(c.You, bounty, town))
				{
					c.Say("That's not enough.");
					c.Offer(Dialogue.Topics(c));
					return;
				}
				SetBounty(k, c.You, 0);
				c.Say(Dialogue.Pick("Paid in full. Behave yourself.", "Your debt to the crown is settled. Don't make me regret it."));
				c.Toast?.Invoke("You are no longer wanted in " + k.name);
				Hub.OnFinePaid(c.You, k);
				Log.Info($"law: fine of {bounty} paid in {k.name}");
				StandDown(k, c.You);
			}
			catch (Exception ex)
			{
				Log.Error("law: fine: " + ex.Message);
				c.Say("*counts the coins twice*");
			}
			c.Offer(Dialogue.Topics(c));
		}

		private static void PeaceMade(Actor you, float dt)
		{
			if ((_warsTimer -= dt) > 0f)
			{
				return;
			}
			_warsTimer = 1f;
			Kingdom kingdom = you.kingdom;
			if (kingdom != _warsOf)
			{
				_warsOf = kingdom;
				_atWarWith.Clear();
				_warsKnown = false;
			}
			if (kingdom == null || !kingdom.isCiv())
			{
				return;
			}
			try
			{
				_peaceScratch.Clear();
				foreach (long item in _atWarWith)
				{
					_peaceScratch.Add(item);
				}
				_atWarWith.Clear();
				foreach (Kingdom item2 in World.world.kingdoms.list)
				{
					if (item2 != null && item2 != kingdom && item2.isAlive() && item2.isCiv() && kingdom.isEnemy(item2))
					{
						_atWarWith.Add(item2.getID());
					}
				}
				if (!_warsKnown)
				{
					_warsKnown = true;
					return;
				}
				foreach (long item3 in _peaceScratch)
				{
					if (!_atWarWith.Contains(item3))
					{
						Kingdom kingdom2 = World.world.kingdoms.get(item3);
						if (kingdom2 != null && kingdom2.isAlive() && BountyOn(kingdom2, you) <= 0)
						{
							StandDown(kingdom2, you);
							Log.Info("law: peace with " + kingdom2.name + ": its people near you stand down");
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("law peace: " + ex.Message);
			}
		}

		public static void StandDown(Kingdom k, Actor you)
		{
			long id = you.data.id;
			foreach (Actor item in Civil.SubjectsOf(k))
			{
				if (item == null || !item.isAlive())
				{
					continue;
				}
				bool flag = false;
				try
				{
					flag = item.isInAggroList(you);
				}
				catch
				{
				}
				bool flag2 = item.has_attack_target && item.attack_target == you;
				if (!flag && !flag2)
				{
					continue;
				}
				try
				{
					if (item._aggression_targets != null)
					{
						item._aggression_targets.Remove(id);
						if (item._aggression_targets.Count == 0)
						{
							item.finishStatusEffect("angry");
						}
					}
					if (flag2)
					{
						item.clearAttackTarget();
					}
				}
				catch (Exception ex)
				{
					Log.Error("law: stand down: " + ex.Message);
				}
			}
		}

		public static void LoyaltyBoost(City city, int amount)
		{
			RegisterFavour();
			try
			{
				int num = Favour(city);
				city.data.set("fp_favour", Mathf.Clamp(num + amount, -15, 30));
				city.data.set("fp_favour_t", (float)World.world.getCurWorldTime());
				city.getLoyalty(pForceRecalc: true);
			}
			catch (Exception ex)
			{
				Log.Error("law: favour: " + ex.Message);
			}
		}

		public static int Favour(City city)
		{
			try
			{
				city.data.get("fp_favour", out var pResult, 0);
				if (pResult == 0)
				{
					return 0;
				}
				city.data.get("fp_favour_t", out var pResult2, 0f);
				int yearsSince = Date.getYearsSince(pResult2);
				return (pResult > 0) ? Mathf.Max(0, pResult - yearsSince * 5) : Mathf.Min(0, pResult + yearsSince * 5);
			}
			catch
			{
				return 0;
			}
		}

		private static void RegisterFavour()
		{
			if (_favourRegistered)
			{
				return;
			}
			_favourRegistered = true;
			try
			{
				if (AssetManager.loyalty_library.get("fp_favour") == null)
				{
					LoyaltyAsset loyaltyAsset = new LoyaltyAsset
					{
						id = "fp_favour",
						translation_key = "fp_favour",
						translation_key_negative = "fp_favour"
					};
					loyaltyAsset.calc = Favour;
					AssetManager.loyalty_library.add(loyaltyAsset);
					Lang.AddGameText("fp_favour", "Favours from the crown");
				}
			}
			catch (Exception ex)
			{
				Log.Error("law: could not add the favour to loyalty: " + ex.Message);
			}
		}

		public static List<Board> BoardsNear(Vector2 at, float range)
		{
			return BoardsNear(at, range, new List<Board>());
		}

		public static List<Board> BoardsNear(Vector2 at, float range, List<Board> list)
		{
			list.Clear();
			try
			{
				object obj = World.world?.map_stats;
				if (obj != _boardsWorld)
				{
					_boardsWorld = obj;
					Boards.Clear();
					_boardsAt = -99f;
				}
				if (Time.unscaledTime - _boardsAt > 5f)
				{
					_boardsAt = Time.unscaledTime;
					List<City> list2 = new List<City>();
					foreach (KeyValuePair<City, Board> board2 in Boards)
					{
						if (board2.Key == null || !board2.Key.isAlive() || board2.Key.data == null || board2.Key.data.id != board2.Value.Id)
						{
							list2.Add(board2.Key);
						}
					}
					foreach (City item in list2)
					{
						Boards.Remove(item);
					}
					foreach (City item2 in World.world.cities.list)
					{
						if (item2 == null || !item2.isAlive())
						{
							continue;
						}
						if (Boards.TryGetValue(item2, out var value))
						{
							if (Hub.BoardSpot(item2, out var at2, out var yaw))
							{
								value.At = at2;
								value.Yaw = yaw;
							}
							continue;
						}
						Board board = Place(item2);
						if (board != null)
						{
							board.Id = item2.data.id;
							Boards[item2] = board;
						}
					}
				}
				foreach (Board value2 in Boards.Values)
				{
					if ((value2.At - at).sqrMagnitude < range * range)
					{
						list.Add(value2);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("law: boards: " + ex.Message);
			}
			return list;
		}

		private static Board Place(City c)
		{
			if (Hub.BoardSpot(c, out var at, out var yaw))
			{
				return new Board
				{
					City = c,
					At = at,
					Yaw = yaw
				};
			}
			Building building = null;
			foreach (Building building2 in c.buildings)
			{
				if (building2 != null && building2.isAlive() && building2.asset != null && building2.asset.type == "type_hall")
				{
					building = building2;
					break;
				}
			}
			WorldTile worldTile = ((building != null) ? building.current_tile : c.getTile());
			if (worldTile == null)
			{
				return null;
			}
			BuildingFundament buildingFundament = building?.asset.fundament;
			int num = ((buildingFundament != null) ? (Mathf.Max(Mathf.Max(buildingFundament.left, buildingFundament.right), Mathf.Max(buildingFundament.top, buildingFundament.bottom)) + 2) : 3);
			int[] array = new int[8] { 6, 5, 7, 4, 0, 3, 1, 2 };
			for (int i = num; i <= num + 3; i++)
			{
				int[] array2 = array;
				for (int j = 0; j < array2.Length; j++)
				{
					float f = (float)array2[j] * MathF.PI / 4f;
					int num2 = worldTile.x + Mathf.RoundToInt(Mathf.Cos(f) * (float)i);
					int num3 = worldTile.y + Mathf.RoundToInt(Mathf.Sin(f) * (float)i);
					if (Free(num2, num3) && Free(num2 + 1, num3) && Free(num2, num3 + 1))
					{
						Vector2 vector = new Vector2((float)num2 + 0.5f, (float)num3 + 0.5f);
						Vector2 vector2 = vector - new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f);
						return new Board
						{
							City = c,
							At = vector,
							Yaw = Mathf.Atan2(vector2.y, vector2.x)
						};
					}
				}
			}
			return null;
		}

		private static bool Free(int x, int y)
		{
			WorldTile tile = World.world.GetTile(x, y);
			if (tile == null || tile.main_type == null || tile.main_type.liquid || tile.main_type.block)
			{
				return false;
			}
			if (tile.building != null)
			{
				return !AssetStyle.IsSolid(tile.building);
			}
			return true;
		}

		public static List<Poster> Posters(Board b, Actor you)
		{
			if (Time.unscaledTime - b.PostersAt < 4f)
			{
				return b.Posters;
			}
			b.PostersAt = Time.unscaledTime;
			b.Posters.Clear();
			City city = b.City;
			try
			{
				List<KeyValuePair<float, Actor>> list = new List<KeyValuePair<float, Actor>>();
				List<KeyValuePair<float, Actor>> list2 = new List<KeyValuePair<float, Actor>>();
				List<KeyValuePair<float, Actor>> list3 = new List<KeyValuePair<float, Actor>>();
				Kingdom kingdom = city.kingdom;
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple == null || !simple.isAlive() || simple.asset == null || simple == you)
					{
						continue;
					}
					float sqrMagnitude = (simple.current_position - b.At).sqrMagnitude;
					if (sqrMagnitude >= 14400f)
					{
						continue;
					}
					string id = simple.asset.id;
					List<KeyValuePair<float, Actor>> list4;
					float key;
					if (id == "bandit" && sqrMagnitude < 8100f)
					{
						list4 = list2;
						key = -(simple.data.kills * 10 + simple.data.level);
					}
					else if (QuestsBeast(id) && sqrMagnitude < 3025f && (simple.kingdom == null || simple.kingdom != kingdom))
					{
						list4 = list;
						key = sqrMagnitude;
					}
					else
					{
						if (kingdom == null || simple.kingdom == null || simple.kingdom == kingdom || !simple.kingdom.isCiv() || !kingdom.isEnemy(simple.kingdom) || !simple.isWarrior())
						{
							continue;
						}
						list4 = list3;
						key = -(simple.data.kills * 10 + simple.data.level);
					}
					if (you == null || Lawful(simple, you))
					{
						list4.Add(new KeyValuePair<float, Actor>(key, simple));
					}
				}
				list.Sort((KeyValuePair<float, Actor> x, KeyValuePair<float, Actor> y) => x.Key.CompareTo(y.Key));
				list2.Sort((KeyValuePair<float, Actor> x, KeyValuePair<float, Actor> y) => x.Key.CompareTo(y.Key));
				list3.Sort((KeyValuePair<float, Actor> x, KeyValuePair<float, Actor> y) => x.Key.CompareTo(y.Key));
				for (int num = 0; num < list.Count && num < 3; num++)
				{
					b.Posters.Add(Poster(list[num].Value, "prowls near " + city.name, 15 + list[num].Value.getMaxHealth() / 8));
				}
				for (int num2 = 0; num2 < list2.Count && num2 < 2; num2++)
				{
					b.Posters.Add(Poster(list2[num2].Value, (num2 == 0) ? "bandit chief" : "bandit", 40 + list2[num2].Value.data.kills * 10));
				}
				for (int num3 = 0; num3 < list3.Count && num3 < 2; num3++)
				{
					b.Posters.Add(Poster(list3[num3].Value, "champion of " + list3[num3].Value.kingdom.name, 50 + list3[num3].Value.data.kills * 8));
				}
				if (you != null && kingdom != null)
				{
					int num4 = BountyOn(kingdom, you);
					if (num4 > 0)
					{
						b.Posters.Add(new Poster
						{
							Target = you,
							Id = you.data.id,
							Name = Dialogue.NameOf(you),
							Why = "wanted for crimes",
							Reward = num4,
							You = true
						});
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("law: posters: " + ex.Message);
			}
			return b.Posters;
		}

		private static bool QuestsBeast(string id)
		{
			switch (id)
			{
			default:
				if (!id.StartsWith("zombie"))
				{
					switch (id)
					{
					default:
						return id == "scorpion";
					case "demon":
					case "fire_skull":
					case "ghost":
					case "evil_mage":
					case "necromancer":
					case "cold_one":
					case "snowman":
					case "greg":
					case "dragon":
					case "sand_spider":
						break;
					}
				}
				break;
			case "wolf":
			case "bear":
			case "hyena":
			case "crocodile":
			case "skeleton":
				break;
			}
			return true;
		}

		private static Poster Poster(Actor a, string why, int reward)
		{
			return new Poster
			{
				Target = a,
				Id = a.data.id,
				Name = ((a.asset.id == "bandit" || a.isSapient()) ? Dialogue.NameOf(a) : Kind(a)),
				Why = why,
				Reward = Mathf.Clamp(reward, 10, 500)
			};
		}

		private static string Kind(Actor a)
		{
			string text = a.asset.id.Replace('_', ' ');
			if (text.Length <= 0)
			{
				return text;
			}
			return char.ToUpper(text[0]) + text.Substring(1);
		}

		public static Quest TakenAt(City c)
		{
			if (c != null)
			{
				return Quests.Read(c.data, "fp_bq");
			}
			return null;
		}

		public static bool Take(Board b, Poster p, Actor you, Action<string> toast)
		{
			if (p.You)
			{
				toast?.Invoke("That's you on the poster. Best not linger.");
				return false;
			}
			if (p.Target != null && p.Target.isAlive() && p.Target.data != null && p.Target.data.id == p.Id && !Lawful(p.Target, you))
			{
				toast?.Invoke("Killing " + p.Name + " would be murder.");
				return false;
			}
			Quest quest = TakenAt(b.City);
			if (quest != null && quest.Stage < 2)
			{
				toast?.Invoke("Someone already took a bounty from this board.");
				return false;
			}
			return Quests.Accept(new Quest
			{
				Kind = QuestKind.Bounty,
				FromBoard = true,
				Giver = b.City.data.id,
				GiverName = b.City.name,
				Target = p.Id,
				TargetName = p.Name,
				Money = p.Reward,
				Renown = 4 + p.Reward / 20,
				Xp = 10 + p.Reward / 5,
				Opinion = 0
			}, you, toast);
		}

		public static bool Claim(Board b, Actor you, Action<string> toast)
		{
			foreach (Quest item in Quests.Log)
			{
				if (item.FromBoard && item.Kind == QuestKind.Bounty && item.Giver == b.City.data.id && item.Stage == 1)
				{
					Quests.Reward(item, you, toast);
					return true;
				}
			}
			return false;
		}

		public static Board Facing(Vector2 you, float yaw)
		{
			foreach (Board item in BoardsNear(you, 4f, _facingScratch))
			{
				Vector2 vector = item.At - you;
				float magnitude = vector.magnitude;
				if (!(magnitude > 2.6f) && !(magnitude < 0.05f) && Mathf.Abs(Mathf.DeltaAngle(yaw * 57.29578f, Mathf.Atan2(vector.y, vector.x) * 57.29578f)) < 40f)
				{
					return item;
				}
			}
			return null;
		}
	}
}
