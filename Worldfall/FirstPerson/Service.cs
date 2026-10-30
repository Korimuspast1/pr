using System;
using System.Collections.Generic;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal static class Service
	{
		internal enum CommandKind : byte
		{
			Follow,
			March
		}

		public const string EnlistedKey = "fp_enlisted";

		public const string PaidKey = "fp_paid";

		public const string KillsKey = "fp_kills";

		public const string CaptainKey = "fp_capt";

		public const string FoundKey = "fp_found";

		public const string SettlersKey = "fp_settlers";

		public const float PromoteAt = 3f;

		private const float Never = float.MinValue;

		public const int Wage = 4;

		private const float Year = 60f;

		public const int MaxSettlers = 8;

		private static Order _last;

		private static Actor _lastFor;

		private static float _lastAt = -99f;

		private const float DefendReach = 70f;

		private static City _defending;

		private static readonly Dictionary<City, int> Counts = new Dictionary<City, int>();

		private static readonly Dictionary<City, Vector2> Sums = new Dictionary<City, Vector2>();

		private static readonly Dictionary<City, Kingdom> Civ = new Dictionary<City, Kingdom>();

		public static CommandKind Command;

		public static WorldTile CommandTile;

		public static string CommandLabel = "Following you";

		private static Actor _commandBy;

		private static List<Actor> _army = new List<Actor>();

		private static Actor _armyFor;

		private static float _armyAt;

		private static Commission _commission;

		private static long _commissionFor = -1L;

		private static string _heardKey;

		private static string _pendingKey;

		private static float _pendingSince;

		public const float Settle = 1.2f;

		private static bool _discharged;

		private static Actor _heardFor;

		private static City _attacking;

		private static float _tick;

		private static float _settleTick;

		private static float _siteMovedAt = -99f;

		public const string RoundsKey = "fp_rounds";

		private const float PostReach = 4f;

		private const int Posts = 3;

		private static readonly List<Vector2> _posts = new List<Vector2>();

		private static long _postsFor = -1L;

		private static int _post;

		private static Actor _roundsBody;

		private const string RoundsPostKey = "fp_rounds_post";

		internal static IReadOnlyList<Vector2> PostsForTest => _posts;

		internal static int PostForTest => _post;

		public static bool IsSoldier(Actor a)
		{
			try
			{
				return a != null && a.isAlive() && a.isWarrior() && a.city != null && a.kingdom != null && a.kingdom.isCiv();
			}
			catch
			{
				return false;
			}
		}

		public static bool CanEnlistYou(Actor npc, Actor you)
		{
			try
			{
				City city = you.city;
				if (city == null || npc == null || npc.isBaby())
				{
					return false;
				}
				if (npc.kingdom != you.kingdom || !you.kingdom.isCiv())
				{
					return false;
				}
				if (npc.isKing())
				{
					return true;
				}
				if (npc.city != city)
				{
					return false;
				}
				if (npc.isCityLeader())
				{
					return true;
				}
				return CaptainOf(city) == npc || (!city.hasLeader() && npc.isWarrior() && npc.isAdult());
			}
			catch
			{
				return false;
			}
		}

		public static string EnlistRefusal(Actor npc, Actor you)
		{
			try
			{
				City city = you.city;
				if (you.isBaby() || !you.isAdult())
				{
					return "Grow up first. The army has no use for children.";
				}
				if (you.isKing())
				{
					return Dialogue.Pick("*laughs* You're the king. The whole army is yours already.", "Your Majesty commands the army. You don't enlist in it.");
				}
				if (you.isWarrior())
				{
					return "You already serve. Look to your orders (M).";
				}
				if (you.isCityLeader())
				{
					return "You lead " + city.name + ". Who'd lead it while you march?";
				}
				if (Law.BountyOn(city.kingdom, you) > 0)
				{
					return "There's a price on your head. We don't arm outlaws.";
				}
				if (Chronicle.Opinion(npc, you) < -15)
				{
					return Dialogue.Pick("I wouldn't trust you with a spear.", "*folds arms* No. Not you.");
				}
				if (city.hasCulture())
				{
					if (you.isSexFemale() && city.culture.hasTrait("conscription_male_only"))
					{
						return "In " + city.name + " only the men bear arms. It's our way.";
					}
					if (you.isSexMale() && city.culture.hasTrait("conscription_female_only"))
					{
						return "In " + city.name + " only the women bear arms. It's our way.";
					}
				}
				return null;
			}
			catch (Exception ex)
			{
				Log.Error("service: enlist refusal: " + ex.Message);
				return "*hesitates* Not today.";
			}
		}

		public static bool Enlist(Actor you)
		{
			try
			{
				City city = you.city;
				if (city == null)
				{
					return false;
				}
				GiveUpCommission(you, null);
				city.makeWarrior(you);
				try
				{
					if (city.hasArmy())
					{
						you.setArmy(city.army);
					}
				}
				catch
				{
				}
				you.data.set("fp_enlisted", (float)Quests.Now);
				you.data.set("fp_paid", (float)Quests.Now);
				you.data.set("fp_kills", 0);
				Chronicle.AddOwnMemory(you, "fp_enlisted", city.name);
				Log.Info($"service: {WorldBoxMod.Describe(you)} enlists in {city.name} ({city.kingdom?.name}); warrior {you.isWarrior()}, army {you.army?.name}");
				_last = null;
				return you.isWarrior();
			}
			catch (Exception ex)
			{
				Log.Error("service: enlist: " + ex);
				return false;
			}
		}

		public static void Discharge(Actor you)
		{
			try
			{
				you.stopBeingWarrior();
				_discharged = true;
				you.data.removeFloat("fp_enlisted");
				you.data.removeFloat("fp_paid");
				you.data.removeInt("fp_kills");
				you.data.removeBool("fp_capt");
				Log.Info("service: " + WorldBoxMod.Describe(you) + " leaves the army of " + you.city?.name);
			}
			catch (Exception ex)
			{
				Log.Error("service: discharge: " + ex.Message);
			}
			_last = null;
		}

		public static float YearsServed(Actor you)
		{
			try
			{
				you.data.get("fp_enlisted", out var pResult, float.MinValue);
				return (pResult == float.MinValue) ? 0f : Mathf.Max(0f, (float)(Quests.Now - (double)pResult) / 60f);
			}
			catch
			{
				return 0f;
			}
		}

		public static Order OrderFor(Actor you, Royal royal, WarMap map)
		{
			if (!IsSoldier(you))
			{
				return null;
			}
			if (_last != null && _lastFor == you && Time.unscaledTime - _lastAt < 0.5f)
			{
				return _last;
			}
			_lastFor = you;
			_lastAt = Time.unscaledTime;
			try
			{
				_last = Work(you, royal, map);
				_last.MakeKey();
			}
			catch (Exception ex)
			{
				Log.Error("service: orders: " + ex.Message);
				_last = null;
			}
			return _last;
		}

		private static Order Work(Actor you, Royal royal, WarMap map)
		{
			City city = you.city;
			Kingdom kingdom = you.kingdom;
			Order order = new Order
			{
				Home = city,
				Realm = kingdom,
				King = kingdom.king,
				Captain = CaptainOf(city)
			};
			order.From = Lord(kingdom, city);
			order.YouLead = order.Captain == you;
			try
			{
				order.Soldiers = (city.hasArmy() ? city.army.countUnits() : city.countWarriors());
				order.Wanted = Mathf.Max(1, Mathf.CeilToInt((float)city.getMaxWarriors() * 0.7f));
			}
			catch
			{
			}
			if (royal != null && royal.Active != null && royal.Active.Home == kingdom && royal.Active.Troops.Contains(you) && royal.Active.Objective != null)
			{
				City objective = royal.Active.Objective;
				order.Kind = OrderKind.March;
				order.Target = objective;
				order.Enemy = objective.kingdom;
				Point(order, CityPoint(objective));
				order.Title = "March on " + objective.name;
				order.Detail = "The war council sends you with the army against " + objective.kingdom?.name + ". Take " + objective.name + ".";
				return order;
			}
			WarFlag warFlag = null;
			try
			{
				warFlag = map?.FlagOf(you);
			}
			catch
			{
			}
			if (warFlag != null && warFlag.Tile != null)
			{
				order.Kind = OrderKind.Hold;
				order.Flag = warFlag.Number;
				Point(order, warFlag.P);
				order.Title = "Hold the king's flag " + warFlag.Number;
				order.Detail = $"You were sent to flag {warFlag.Number} on the war map. Stand there and fight whatever comes" + ((warFlag.Holding > 0) ? $", with {warFlag.Holding} others holding it." : ".");
				return order;
			}
			Vector2 at;
			int foes;
			Kingdom raider;
			City city2 = Threatened(you, kingdom, city, out at, out foes, out raider);
			if (city2 != null)
			{
				order.Kind = OrderKind.Defend;
				order.Target = city2;
				order.Enemy = raider;
				order.Foes = foes;
				Point(order, at);
				order.Title = "Defend " + city2.name;
				string text = ((raider != null && raider.isCiv()) ? raider.name : "Raiders");
				order.Detail = string.Format("{0} in {1}! {2} {3} inside the town. Drive them out.", text, city2.name, foes, (foes == 1) ? "foe" : "foes");
				return order;
			}
			City target_attack_city = city.target_attack_city;
			bool flag = false;
			try
			{
				flag = target_attack_city != null && target_attack_city.isAlive() && target_attack_city.kingdom != null && target_attack_city.kingdom != kingdom;
			}
			catch
			{
			}
			if (flag)
			{
				order.Kind = OrderKind.Attack;
				order.Target = target_attack_city;
				order.Enemy = target_attack_city.kingdom;
				TileZone target_attack_zone = city.target_attack_zone;
				Point(order, (target_attack_zone != null && target_attack_zone.centerTile != null) ? new Vector2((float)target_attack_zone.centerTile.x + 0.5f, (float)target_attack_zone.centerTile.y + 0.5f) : CityPoint(target_attack_city));
				order.Title = (order.YouLead ? "Lead the army on " : "March on ") + target_attack_city.name;
				string text2 = (order.YouLead ? "The army follows you." : ((order.Captain != null) ? ("Captain " + NameOf(order.Captain) + " leads it.") : "It has no captain yet."));
				order.Detail = $"Take {target_attack_city.name} from {target_attack_city.kingdom.name}. The army of {city.name} marches, {order.Soldiers} strong. {text2}";
				return order;
			}
			List<Kingdom> list = Wars(kingdom);
			if (list.Count > 0)
			{
				Kingdom kingdom2 = list[0];
				order.Kind = OrderKind.Muster;
				order.Enemy = kingdom2;
				City city3 = (order.Target = NearestCity(kingdom2, CityPoint(city)));
				Point(order, MusterPoint(city));
				order.Title = "Muster at " + city.name;
				string text3 = ((city3 != null) ? $" When it's ready ({order.Wanted} soldiers) it marches, likely on {city3.name}." : "");
				string text4 = "War with " + kingdom2.name + ((list.Count > 1) ? (" and " + (list.Count - 1) + " more") : "") + ".";
				order.Detail = ((order.Soldiers >= order.Wanted) ? ($"{text4} The army of {city.name} stands ready, {order.Soldiers} strong, and waits for the word to march" + ((city3 != null) ? (", likely on " + city3.name + ".") : ".")) : $"{text4} The army of {city.name} gathers: {order.Soldiers} of {order.Wanted}.{text3}");
				return order;
			}
			RoundsOrder(you, order, city, kingdom);
			return order;
		}

		private static void Point(Order o, Vector2 p)
		{
			o.Where = p;
			o.HasWhere = p != Vector2.zero;
		}

		private static City Threatened(Actor you, Kingdom realm, City home, out Vector2 at, out int foes, out Kingdom raider)
		{
			at = Vector2.zero;
			foes = 0;
			raider = null;
			Counts.Clear();
			Sums.Clear();
			Civ.Clear();
			List<Actor> simpleList = World.world.units.getSimpleList();
			for (int i = 0; i < simpleList.Count; i++)
			{
				Actor actor = simpleList[i];
				if (actor == null || !actor.isAlive() || actor.kingdom == null || actor.kingdom == realm)
				{
					continue;
				}
				City city;
				try
				{
					city = actor.current_tile?.zone?.city;
				}
				catch
				{
					continue;
				}
				if (city == null || city.kingdom != realm)
				{
					continue;
				}
				bool flag;
				try
				{
					flag = actor.kingdom.isEnemy(realm) && !actor.isBaby() && (!actor.kingdom.isCiv() || actor.isWarrior() || actor.isFighting());
				}
				catch
				{
					flag = false;
				}
				if (flag)
				{
					Counts.TryGetValue(city, out var value);
					Counts[city] = value + 1;
					Sums.TryGetValue(city, out var value2);
					Sums[city] = value2 + actor.current_position;
					if (actor.kingdom.isCiv())
					{
						Civ[city] = actor.kingdom;
					}
					else if (!Civ.ContainsKey(city))
					{
						Civ[city] = actor.kingdom;
					}
				}
			}
			City city2 = null;
			int num = 0;
			Vector2 current_position = you.current_position;
			Vector2 vector = CityPoint(home);
			foreach (KeyValuePair<City, int> count in Counts)
			{
				City key = count.Key;
				Kingdom value3;
				bool flag2 = Civ.TryGetValue(key, out value3) && value3.isCiv();
				bool flag3 = key == _defending;
				if (count.Value < 2 && !flag3 && !(key == home && flag2))
				{
					continue;
				}
				Vector2 vector2 = CityPoint(key);
				if (key == home || flag3 || !((vector2 - current_position).sqrMagnitude > 4900f) || !((vector2 - vector).sqrMagnitude > 4900f))
				{
					int num2 = count.Value + ((key == home) ? 1000 : 0) + (flag3 ? 500 : 0);
					if (num2 > num)
					{
						num = num2;
						city2 = key;
					}
				}
			}
			_defending = city2;
			if (city2 == null)
			{
				return null;
			}
			foes = Counts[city2];
			at = Sums[city2] / foes;
			Civ.TryGetValue(city2, out raider);
			return city2;
		}

		public static List<Kingdom> Wars(Kingdom realm)
		{
			List<Kingdom> list = new List<Kingdom>();
			foreach (Kingdom item in Royal.Rivals(realm))
			{
				if (Royal.AtWar(realm, item))
				{
					list.Add(item);
				}
			}
			return list;
		}

		public static Actor CaptainOf(City c)
		{
			try
			{
				return (c != null && c.hasArmy() && c.army.hasCaptain()) ? c.army.getCaptain() : null;
			}
			catch
			{
				return null;
			}
		}

		public static string Lord(Kingdom realm, City home)
		{
			try
			{
				Actor king = realm.king;
				if (king != null && king.isAlive())
				{
					return (king.isSexFemale() ? "Queen " : "King ") + NameOf(king);
				}
				if (home != null && home.leader != null && home.leader.isAlive())
				{
					return "Lord " + NameOf(home.leader);
				}
				return "the lords of " + realm.name;
			}
			catch
			{
				return "the crown";
			}
		}

		private static Vector2 MusterPoint(City home)
		{
			try
			{
				Building building = home.getBuildingOfType("type_training_dummies", pCountOnlyFinished: true, pRandom: false, pOnlyFreeTile: false, (TileIsland)null) ?? home.getBuildingOfType("type_barracks", pCountOnlyFinished: true, pRandom: false, pOnlyFreeTile: false, (TileIsland)null);
				if (building != null && building.current_tile != null)
				{
					return new Vector2((float)building.current_tile.x + 0.5f, (float)building.current_tile.y + 0.5f);
				}
			}
			catch
			{
			}
			return CityPoint(home);
		}

		public static Vector2 CityPoint(City c)
		{
			try
			{
				WorldTile worldTile = c?.getTile();
				if (worldTile != null)
				{
					return new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f);
				}
			}
			catch
			{
			}
			return Vector2.zero;
		}

		private static City NearestCity(Kingdom k, Vector2 from)
		{
			City result = null;
			float num = float.MaxValue;
			try
			{
				foreach (City city in k.getCities())
				{
					if (city != null && city.isAlive())
					{
						float sqrMagnitude = (CityPoint(city) - from).sqrMagnitude;
						if (sqrMagnitude < num)
						{
							num = sqrMagnitude;
							result = city;
						}
					}
				}
			}
			catch
			{
			}
			return result;
		}

		public static string NameOf(Actor a)
		{
			try
			{
				string name = a.getName();
				return string.IsNullOrEmpty(name) ? "?" : name;
			}
			catch
			{
				return "?";
			}
		}

		public static string Heading(Vector2 from, Vector2 to)
		{
			Vector2 vector = to - from;
			float magnitude = vector.magnitude;
			if (magnitude < 4f)
			{
				return "here";
			}
			string[] array = new string[8] { "east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east" };
			int num = Mathf.RoundToInt(Mathf.Atan2(vector.y, vector.x) / (MathF.PI / 4f));
			return Mathf.RoundToInt(magnitude) + " tiles " + array[(num % 8 + 8) % 8];
		}

		public static bool IsCaptain(Actor you)
		{
			if (IsSoldier(you))
			{
				return CaptainOf(you.city) == you;
			}
			return false;
		}

		public static int Kills(Actor you)
		{
			try
			{
				you.data.get("fp_kills", out var pResult, 0);
				return pResult;
			}
			catch
			{
				return 0;
			}
		}

		public static void AddKills(Actor you, int n)
		{
			if (n <= 0 || !IsSoldier(you))
			{
				return;
			}
			try
			{
				you.data.set("fp_kills", Kills(you) + n);
			}
			catch
			{
			}
		}

		public static void OnKill(Actor victim)
		{
			Actor actor = WorldBoxMod.Instance?.Host;
			if (victim != null && IsSoldier(actor) && IsFoe(actor.kingdom, victim) && !Law.WarCivilian(victim, actor))
			{
				AddKills(actor, 1);
				Log.Info($"service: {WorldBoxMod.Describe(actor)} felled a foe ({WorldBoxMod.Describe(victim)}); {Kills(actor)} felled, merit {Merit(actor):F1}");
			}
		}

		public static bool IsFoe(Kingdom realm, Actor a)
		{
			try
			{
				Kingdom kingdom = a.kingdom;
				if (realm == null || kingdom == null || kingdom == realm)
				{
					return false;
				}
				if (kingdom.isCiv())
				{
					return Royal.AtWar(realm, kingdom);
				}
				return kingdom.isEnemy(realm);
			}
			catch
			{
				return false;
			}
		}

		public static float Merit(Actor you)
		{
			return YearsServed(you) + (float)Kills(you) * 0.5f;
		}

		private static void CheckPromotion(Actor you, Action<string> toast, Action horn)
		{
			if (!IsSoldier(you))
			{
				return;
			}
			try
			{
				you.data.get("fp_enlisted", out var pResult, float.MinValue);
				if (pResult == float.MinValue)
				{
					you.data.set("fp_enlisted", (float)Quests.Now);
				}
				else
				{
					if (IsCaptain(you) || Merit(you) < 3f)
					{
						return;
					}
					City city = you.city;
					Actor actor = CaptainOf(city);
					Army army = (city.hasArmy() ? city.army : World.world.armies.newArmy(you, city));
					if (army == null)
					{
						return;
					}
					if (you.army != army)
					{
						you.setArmy(army);
					}
					if (army.getCaptain() != you)
					{
						army.setCaptain(you);
					}
					if (CaptainOf(city) == you)
					{
						_last = null;
						you.data.get("fp_capt", out var pResult2, pDefault: false);
						if (pResult2)
						{
							Log.Info("service: " + WorldBoxMod.Describe(you) + " is captain of " + city.name + " again");
						}
						else
						{
							you.data.set("fp_capt", pData: true);
							Chronicle.AddOwnMemory(you, "fp_captain", city.name);
							Log.Info(string.Format("service: {0} made captain of the army of {1} (merit {2:F1}; was {3})", WorldBoxMod.Describe(you), city.name, Merit(you), (actor != null) ? NameOf(actor) : "none"));
							toast?.Invoke(Lord(you.kingdom, city) + " makes you captain of the army of " + city.name + "! Your soldiers follow you. Command them from your map (M).");
							horn?.Invoke();
							_last = null;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("service: promotion: " + ex.Message);
			}
		}

		public static List<Actor> ArmyOf(Actor you)
		{
			List<Actor> list = new List<Actor>();
			Army army = null;
			try
			{
				army = you.army;
			}
			catch
			{
			}
			if (army == null)
			{
				return list;
			}
			List<Actor> simpleList = World.world.units.getSimpleList();
			for (int i = 0; i < simpleList.Count; i++)
			{
				Actor actor = simpleList[i];
				try
				{
					if (actor != null && actor != you && actor.isAlive() && actor.army == army)
					{
						list.Add(actor);
					}
				}
				catch
				{
				}
			}
			return list;
		}

		public static int CommandArmy(Actor you, CommandKind kind, WorldTile tile, string label)
		{
			if (!IsCaptain(you))
			{
				return 0;
			}
			Command = kind;
			CommandTile = ((kind == CommandKind.March) ? tile : null);
			CommandLabel = label;
			_commandBy = you;
			List<Actor> list = ArmyOf(you);
			foreach (Actor item in list)
			{
				try
				{
					if (kind == CommandKind.Follow)
					{
						if (Marching(item))
						{
							item.cancelAllBeh();
						}
					}
					else
					{
						Send(item, tile);
					}
				}
				catch (Exception ex)
				{
					Log.Error("service: command: " + ex.Message);
				}
			}
			Log.Info($"service: captain {WorldBoxMod.Describe(you)} commands {list.Count} soldier(s): {label}" + ((tile != null) ? $" at {tile.x},{tile.y}" : ""));
			return list.Count;
		}

		private static void KeepCommand(Actor you)
		{
			if (Command == CommandKind.Follow)
			{
				return;
			}
			if (_commandBy != you || !IsCaptain(you) || CommandTile == null)
			{
				Command = CommandKind.Follow;
				CommandTile = null;
				CommandLabel = "Following you";
				return;
			}
			foreach (Actor item in ArmyOf(you))
			{
				try
				{
					if (!item.isFighting() && !item.is_inside_building && !((item.current_position - new Vector2((float)CommandTile.x + 0.5f, (float)CommandTile.y + 0.5f)).magnitude < 4.5f) && (!Marching(item) || item.beh_tile_target == null || Mathf.Abs(item.beh_tile_target.x - CommandTile.x) > 4 || Mathf.Abs(item.beh_tile_target.y - CommandTile.y) > 4))
					{
						Send(item, CommandTile);
					}
				}
				catch
				{
				}
			}
		}

		private static void Send(Actor a, WorldTile tile)
		{
			if (a.ai != null && tile != null)
			{
				Royal.RegisterTask();
				a.setTask("fp_royal_march");
				a.beh_tile_target = Near(tile);
			}
		}

		private static bool Marching(Actor a)
		{
			try
			{
				return a.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id == "fp_royal_march";
			}
			catch
			{
				return false;
			}
		}

		public static List<Actor> ArmyCached(Actor you)
		{
			if (_armyFor != you || Time.unscaledTime - _armyAt > 0.4f)
			{
				_army = ArmyOf(you);
				_armyFor = you;
				_armyAt = Time.unscaledTime;
			}
			return _army;
		}

		public static bool ArmyMiddle(Actor you, out Vector2 at, out int count)
		{
			at = Vector2.zero;
			List<Actor> list = ArmyCached(you);
			count = list.Count;
			if (count == 0)
			{
				return false;
			}
			foreach (Actor item in list)
			{
				at += item.current_position;
			}
			at /= (float)count;
			return true;
		}

		public static Commission CommissionOf(Actor you)
		{
			if (you == null)
			{
				return null;
			}
			long id;
			try
			{
				id = you.data.id;
			}
			catch
			{
				return null;
			}
			if (id == _commissionFor)
			{
				return _commission;
			}
			_commissionFor = id;
			_commission = null;
			try
			{
				you.data.get("fp_found", out var pResult, null);
				if (string.IsNullOrEmpty(pResult))
				{
					return null;
				}
				string[] array = pResult.Split('|');
				if (array.Length < 6)
				{
					return null;
				}
				Commission commission = new Commission
				{
					X = int.Parse(array[0]),
					Y = int.Parse(array[1]),
					HomeId = long.Parse(array[2]),
					RealmId = long.Parse(array[3]),
					Giver = array[4],
					HomeName = array[5],
					RealmName = ((array.Length > 6) ? array[6] : "")
				};
				you.data.get("fp_settlers", out var pResult2, null);
				if (!string.IsNullOrEmpty(pResult2))
				{
					string[] array2 = pResult2.Split(',');
					for (int i = 0; i < array2.Length; i++)
					{
						if (long.TryParse(array2[i], out var result))
						{
							commission.Settlers.Add(result);
						}
					}
				}
				_commission = commission;
			}
			catch (Exception ex)
			{
				Log.Error("service: reading a commission: " + ex.Message);
			}
			return _commission;
		}

		private static void Save(Actor you, Commission c)
		{
			_commissionFor = you.data.id;
			_commission = c;
			if (c == null)
			{
				you.data.removeString("fp_found");
				you.data.removeString("fp_settlers");
				return;
			}
			you.data.set("fp_found", $"{c.X}|{c.Y}|{c.HomeId}|{c.RealmId}|{Clean(c.Giver)}|{Clean(c.HomeName)}|{Clean(c.RealmName)}");
			you.data.set("fp_settlers", string.Join(",", c.Settlers.ConvertAll((long x) => x.ToString()).ToArray()));
		}

		private static string Clean(string s)
		{
			return (s ?? "").Replace('|', '/');
		}

		public static string FoundRefusal(Actor npc, Actor you)
		{
			try
			{
				City city = you.city;
				if (you.isBaby() || !you.isAdult())
				{
					return "A village needs grown hands. Come back when you're older.";
				}
				if (you.isKing())
				{
					return "You're the king. Your place is in " + (you.kingdom.capital?.name ?? city.name) + ". Send someone else.";
				}
				if (CommissionOf(you) != null)
				{
					return "You already have land to settle. Look at your map (M).";
				}
				if (Law.BountyOn(city.kingdom, you) > 0)
				{
					return "There's a price on your head. Pay your fine before you ask for our land.";
				}
				if (Chronicle.Opinion(npc, you) < -15)
				{
					return "I wouldn't trust you with a woodshed, never mind a village.";
				}
				if (Sites(you, city, 1).Count == 0)
				{
					return "There's no free land left on our shores. Not even past the hills.";
				}
				return null;
			}
			catch (Exception ex)
			{
				Log.Error("service: found refusal: " + ex.Message);
				return "*hesitates* Not today.";
			}
		}

		public static List<TileZone> Sites(Actor you, City home, int max, Vector2? nearest = null)
		{
			List<TileZone> list = new List<TileZone>();
			try
			{
				CityPlaceFinder city_place_finder = World.world.city_zone_helper.city_place_finder;
				if (city_place_finder.isDirty())
				{
					city_place_finder.recalc();
				}
				WorldTile worldTile = home?.getTile() ?? you.current_tile;
				if (worldTile == null)
				{
					return list;
				}
				Vector2 p = nearest ?? new Vector2(worldTile.x, worldTile.y);
				foreach (TileZone zone in city_place_finder.zones)
				{
					if (zone == null || zone.centerTile == null || zone.hasCity())
					{
						continue;
					}
					try
					{
						if (!zone.centerTile.isSameIsland(worldTile))
						{
							continue;
						}
					}
					catch
					{
						continue;
					}
					if (zone.isGoodForNewCity(you))
					{
						list.Add(zone);
					}
				}
				list.Sort((TileZone a, TileZone b) => (new Vector2(a.centerTile.x, a.centerTile.y) - p).sqrMagnitude.CompareTo((new Vector2(b.centerTile.x, b.centerTile.y) - p).sqrMagnitude));
				if (list.Count > max)
				{
					list.RemoveRange(max, list.Count - max);
				}
			}
			catch (Exception ex)
			{
				Log.Error("service: sites: " + ex.Message);
			}
			return list;
		}

		public static List<Actor> Volunteers(Actor you, City home)
		{
			List<Actor> list = new List<Actor>();
			foreach (Actor a in Civil.PeopleOf(home))
			{
				if (a == you || Relation(you, a) == null)
				{
					continue;
				}
				bool flag;
				try
				{
					flag = !a.isKing() && !a.isCityLeader() && !Economy.Hired.Exists((Hireling h) => h.Guard == a);
				}
				catch
				{
					flag = false;
				}
				if (flag)
				{
					list.Add(a);
				}
			}
			list.Sort((Actor a2, Actor b) => Rank(Relation(you, a2)).CompareTo(Rank(Relation(you, b))));
			if (list.Count > 8)
			{
				list.RemoveRange(8, list.Count - 8);
			}
			return list;
		}

		public static string Relation(Actor you, Actor a)
		{
			try
			{
				if (you == null || a == null || a == you)
				{
					return null;
				}
				bool flag = a.isSexFemale();
				if (you.lover == a)
				{
					return "lover";
				}
				long id = you.data.id;
				long parent_id_ = a.data.parent_id_1;
				long parent_id_2 = a.data.parent_id_2;
				long m1 = you.data.parent_id_1;
				long m2 = you.data.parent_id_2;
				if (id > 0 && (parent_id_ == id || parent_id_2 == id))
				{
					return flag ? "daughter" : "son";
				}
				if (a.data.id == m1 || a.data.id == m2)
				{
					return flag ? "mother" : "father";
				}
				if (Shared(parent_id_) || Shared(parent_id_2))
				{
					return flag ? "sister" : "brother";
				}
				bool Shared(long p)
				{
					if (p > 0)
					{
						if (p != m1)
						{
							return p == m2;
						}
						return true;
					}
					return false;
				}
			}
			catch
			{
			}
			return null;
		}

		private static int Rank(string relation)
		{
			switch (relation)
			{
			case "lover":
				return 0;
			case "son":
			case "daughter":
				return 1;
			case "mother":
			case "father":
				return 2;
			default:
				return 3;
			}
		}

		public static Commission Commission(Actor you, Actor giver, TileZone site, List<Actor> settlers)
		{
			City city = you.city;
			if (you.isWarrior())
			{
				Discharge(you);
			}
			Commission commission = new Commission
			{
				X = site.centerTile.x,
				Y = site.centerTile.y,
				HomeId = city.data.id,
				RealmId = you.kingdom.data.id,
				Giver = NameOf(giver),
				HomeName = city.name,
				RealmName = you.kingdom.name
			};
			foreach (Actor settler in settlers)
			{
				commission.Settlers.Add(settler.data.id);
			}
			Save(you, commission);
			Chronicle.AddOwnMemory(you, "fp_commissioned", city.name);
			Log.Info($"service: {WorldBoxMod.Describe(you)} sent to found a village at {commission.X},{commission.Y} ({Heading(CityPoint(city), commission.Site)} of {city.name}) with {settlers.Count} settler(s)");
			return commission;
		}

		public static bool MoveSite(Actor you, TileZone zone)
		{
			Commission commission = CommissionOf(you);
			if (commission == null || zone?.centerTile == null)
			{
				return false;
			}
			commission.X = zone.centerTile.x;
			commission.Y = zone.centerTile.y;
			Save(you, commission);
			return true;
		}

		public static void GiveUpCommission(Actor you, Action<string> toast)
		{
			if (CommissionOf(you) == null)
			{
				return;
			}
			foreach (Actor item in SettlersOf(you))
			{
				StopFollowing(item);
			}
			Save(you, null);
			toast?.Invoke("You gave up your charge to found a village.");
		}

		public static List<Actor> SettlersOf(Actor you)
		{
			List<Actor> list = new List<Actor>();
			Commission commission = CommissionOf(you);
			if (commission == null)
			{
				return list;
			}
			foreach (long settler in commission.Settlers)
			{
				Actor actor = Chronicle.Find(settler);
				try
				{
					if (actor != null && actor.isAlive())
					{
						list.Add(actor);
					}
				}
				catch
				{
				}
			}
			return list;
		}

		public static TileZone FoundableHere(Actor you)
		{
			try
			{
				if (CommissionOf(you) == null || you.kingdom == null || !you.kingdom.isCiv() || you.isKing())
				{
					return null;
				}
				TileZone tileZone = (World.world.GetTile((int)you.current_position.x, (int)you.current_position.y) ?? you.current_tile)?.zone;
				if (tileZone == null || tileZone.hasCity())
				{
					return null;
				}
				return tileZone.isGoodForNewCity(you) ? tileZone : null;
			}
			catch
			{
				return null;
			}
		}

		public static City Found(Actor you, Action<string> toast)
		{
			TileZone tileZone = FoundableHere(you);
			if (tileZone == null)
			{
				return null;
			}
			Commission commission = CommissionOf(you);
			try
			{
				if (you.kingdom == null || you.kingdom.data.id != commission.RealmId)
				{
					toast?.Invoke("You no longer serve " + commission.RealmName + ". Your charge is void.");
					Save(you, null);
					return null;
				}
				List<Actor> list = SettlersOf(you);
				City city = World.world.cities.buildNewCity(you, tileZone);
				if (city == null)
				{
					return null;
				}
				Belong.Join(you, city);
				try
				{
					city.setLeader(you, pNew: true);
				}
				catch (Exception ex)
				{
					Log.Error("service: leader: " + ex.Message);
				}
				int num = 0;
				foreach (Actor item in list)
				{
					try
					{
						if (item.kingdom == you.kingdom)
						{
							StopFollowing(item);
							item.stopBeingWarrior();
							item.joinCity(city);
							item.cancelAllBeh();
							num++;
						}
					}
					catch (Exception ex2)
					{
						Log.Error("service: settler: " + ex2.Message);
					}
				}
				Save(you, null);
				Chronicle.AddOwnMemory(you, "fp_founded", city.name);
				Founding.Founded(you, city, charter: true, toast);
				Log.Info($"service: {WorldBoxMod.Describe(you)} founded {city.name} for {city.kingdom?.name} with {num} settler(s); leader {city.leader == you}");
				toast?.Invoke("You founded " + city.name + " for " + city.kingdom?.name + "! You lead it now." + ((num <= 0) ? "" : ((num == 1) ? " Your family settles there with you." : $" {num} of your family settle there with you.")));
				return city;
			}
			catch (Exception ex3)
			{
				Log.Error("service: founding: " + ex3);
				toast?.Invoke("The village wouldn't take root here.");
				return null;
			}
		}

		public static void Update(Actor you, Royal royal, WarMap map, float dt, Action<string> toast, Action horn)
		{
			if (you != null)
			{
				_tick -= dt;
				if (_tick <= 0f)
				{
					_tick = 0.5f;
					CheckOutlaw(you, toast);
					Order o = OrderFor(you, royal, map);
					Hear(you, o, toast, horn);
					WalkRounds(you, o, toast);
					PayWage(you, toast);
					CheckPromotion(you, toast, horn);
					CheckCommission(you, toast);
				}
				_settleTick -= dt;
				if (_settleTick <= 0f)
				{
					_settleTick = 1.2f;
					KeepSettlers(you);
					KeepCommand(you);
				}
			}
		}

		private static void CheckOutlaw(Actor you, Action<string> toast)
		{
			try
			{
				Kingdom kingdom = you.kingdom;
				if (kingdom != null && kingdom.isCiv() && !you.isKing() && Law.BountyOn(kingdom, you) > 0)
				{
					if (IsSoldier(you))
					{
						Discharge(you);
						toast?.Invoke("Discharged from the army of " + kingdom.name + ": you are wanted. Pay your fine to serve again.");
						Log.Info($"service: {WorldBoxMod.Describe(you)} discharged, wanted in {kingdom.name} ({Law.BountyOn(kingdom, you)} gold)");
					}
					if (CommissionOf(you) != null)
					{
						Void(you);
						toast?.Invoke("The crown takes back your charge to found a village: you are wanted.");
						Log.Info("service: " + WorldBoxMod.Describe(you) + "'s charge to found a village void, wanted in " + kingdom.name);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("service: outlaw: " + ex.Message);
			}
		}

		private static void Hear(Actor you, Order o, Action<string> toast, Action horn)
		{
			string text = o?.Key;
			if (_heardFor != you)
			{
				_heardFor = you;
				_heardKey = text;
				_attacking = ((o != null && (o.Kind == OrderKind.Attack || o.Kind == OrderKind.March)) ? o.Target : null);
				if (o != null && (int)o.Kind >= 2)
				{
					toast?.Invoke("Your orders from " + o.From + ": " + o.Title + "   (M for the map)");
				}
				return;
			}
			if (_attacking != null && o == null)
			{
				_attacking = null;
			}
			if (_attacking != null)
			{
				bool flag = false;
				bool flag2 = false;
				try
				{
					flag2 = !_attacking.isAlive();
					flag = !flag2 && _attacking.kingdom == you.kingdom;
				}
				catch
				{
					flag2 = true;
				}
				if (flag || flag2)
				{
					string name = _attacking.name;
					int num = 15 + Mathf.RoundToInt(YearsServed(you));
					int num2 = CrownPays(you, num);
					toast?.Invoke((flag ? (name + " is taken!") : (name + " has fallen!")) + " " + PaidLine(num2, num, toYou: true));
					horn?.Invoke();
					Chronicle.AddOwnMemory(you, "fp_took", name);
					Log.Info(string.Format("service: {0} {1}; {2} rewarded {3} of {4}", name, flag ? "taken" : "fell", WorldBoxMod.Describe(you), num2, num));
					_attacking = null;
					_heardKey = text;
					return;
				}
			}
			if (text == _heardKey)
			{
				_pendingKey = null;
			}
			else if (text != _pendingKey)
			{
				_pendingKey = text;
				_pendingSince = Time.unscaledTime;
			}
			else
			{
				if (Time.unscaledTime - _pendingSince < 1.2f)
				{
					return;
				}
				_pendingKey = null;
				string heardKey = _heardKey;
				_heardKey = text;
				if (o == null)
				{
					if (heardKey != null && !_discharged)
					{
						toast?.Invoke("You are no longer a soldier.");
					}
					return;
				}
				_discharged = false;
				if (heardKey == null && o.Kind == OrderKind.Patrol)
				{
					Log.Info($"service: orders for {WorldBoxMod.Describe(you)}: {o.Kind} {o.Title} at {o.Where}");
					return;
				}
				if (o.Kind == OrderKind.Attack || o.Kind == OrderKind.March)
				{
					_attacking = o.Target;
				}
				switch (o.Kind)
				{
				case OrderKind.Attack:
				case OrderKind.Defend:
				case OrderKind.March:
				case OrderKind.Hold:
					toast?.Invoke("New orders from " + o.From + ": " + o.Title + "!   (M for the map)");
					horn?.Invoke();
					break;
				case OrderKind.Muster:
					toast?.Invoke("War with " + (Order.NameOf(o.Enemy) ?? "the enemy") + "! " + o.From + " calls the army to muster at " + (Order.NameOf(o.Home) ?? "home") + ".");
					horn?.Invoke();
					break;
				default:
				{
					bool flag3 = heardKey != null && !heardKey.StartsWith(OrderKind.Patrol.ToString() + ":", StringComparison.Ordinal);
					toast?.Invoke(flag3 ? ("The war is over. " + o.Title + ".") : ("Your post: " + o.Title + "."));
					break;
				}
				}
				Log.Info($"service: orders for {WorldBoxMod.Describe(you)}: {o.Kind} {o.Title} at {o.Where} ({o.Detail})");
			}
		}

		private static int CrownPays(Actor you, int amount)
		{
			Actor actor = null;
			try
			{
				actor = you.kingdom?.king;
				if (actor == you || (actor != null && !actor.isAlive()))
				{
					actor = null;
				}
			}
			catch
			{
			}
			try
			{
				return Economy.PayOut(you, amount, you.city, actor);
			}
			catch (Exception ex)
			{
				Log.Error("service: pay: " + ex.Message);
				return 0;
			}
		}

		private static string PaidLine(int got, int owed, bool toYou)
		{
			if (got >= owed)
			{
				if (!toYou)
				{
					return $"The crown pays {got} gold.";
				}
				return $"The crown pays you {got} gold.";
			}
			if (got > 0)
			{
				if (!toYou)
				{
					return $"The crown pays {got} gold, all its coffers could spare of {owed}.";
				}
				return $"The crown pays you {got} gold, all its coffers could spare of {owed}.";
			}
			return "The treasury and the king's purse are empty: no gold this time.";
		}

		private static void PayWage(Actor you, Action<string> toast)
		{
			if (!IsSoldier(you))
			{
				return;
			}
			try
			{
				you.data.get("fp_paid", out var pResult, float.MinValue);
				double now = Quests.Now;
				if (pResult == float.MinValue)
				{
					you.data.set("fp_paid", (float)now);
				}
				else if (!(now - (double)pResult < 60.0))
				{
					you.data.set("fp_paid", (float)now);
					int num = 4 + Mathf.Min(6, Mathf.FloorToInt(YearsServed(you) / 5f));
					int num2 = CrownPays(you, num);
					toast?.Invoke("A soldier's pay: " + PaidLine(num2, num, toYou: false));
					Log.Info($"service: wage for {WorldBoxMod.Describe(you)}: {num2} of {num}");
				}
			}
			catch (Exception ex)
			{
				Log.Error("service: wage: " + ex.Message);
			}
		}

		private static void CheckCommission(Actor you, Action<string> toast)
		{
			Commission commission = CommissionOf(you);
			if (commission == null)
			{
				return;
			}
			bool flag = false;
			bool flag2 = false;
			try
			{
				flag = you.kingdom != null && you.kingdom.data.id == commission.RealmId && you.kingdom.isAlive();
				flag2 = you.isKing();
			}
			catch
			{
			}
			if (flag && !flag2)
			{
				if (Time.unscaledTime - _siteMovedAt < 4f)
				{
					return;
				}
				TileZone tileZone = null;
				bool flag3 = false;
				try
				{
					tileZone = World.world.GetTile(commission.X, commission.Y)?.zone;
					flag3 = tileZone != null && !tileZone.hasCity() && tileZone.isGoodForNewCity(you);
				}
				catch
				{
					flag3 = true;
				}
				if (!flag3)
				{
					bool flag4 = tileZone?.hasCity() ?? false;
					_siteMovedAt = Time.unscaledTime;
					List<TileZone> list = Sites(you, you.city, 1, commission.Site);
					if (list.Count == 0)
					{
						toast?.Invoke("Your site is lost, and there's no free land left. Your charge is void.");
						Void(you);
					}
					else
					{
						MoveSite(you, list[0]);
						toast?.Invoke((flag4 ? "Someone settled your site first." : "A town has grown up too close to your site.") + " Your map (M) shows the nearest free land, " + Heading(you.current_position, commission.Site) + ".");
						Log.Info(string.Format("service: the founder's site was {0}; moved to {1},{2}", flag4 ? "taken" : "crowded", commission.X, commission.Y));
					}
				}
			}
			else
			{
				toast?.Invoke(flag2 ? ("You wear the crown now: a king doesn't leave " + (you.kingdom.capital?.name ?? "the capital") + " to found a village. Your charge is void.") : ("Your charge to found a village for " + commission.RealmName + " is void: you no longer serve it."));
				Void(you);
			}
		}

		private static void Void(Actor you)
		{
			foreach (Actor item in SettlersOf(you))
			{
				StopFollowing(item);
			}
			Save(you, null);
		}

		private static void KeepSettlers(Actor you)
		{
			Commission commission = CommissionOf(you);
			if (commission == null || commission.Settlers.Count == 0)
			{
				return;
			}
			WorldTile worldTile;
			try
			{
				worldTile = World.world.GetTile((int)you.current_position.x, (int)you.current_position.y) ?? you.current_tile;
			}
			catch
			{
				return;
			}
			if (worldTile == null)
			{
				return;
			}
			foreach (Actor item in SettlersOf(you))
			{
				try
				{
					if (item.ai == null)
					{
						continue;
					}
					float magnitude = (item.current_position - you.current_position).magnitude;
					if (!(magnitude < 5f) && !(magnitude > 90f))
					{
						if (item.is_inside_building)
						{
							item.exitBuilding();
						}
						if (((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)item.ai).task == null || !(((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)item.ai).task.id == "fp_royal_march") || item.beh_tile_target == null || Mathf.Abs(item.beh_tile_target.x - worldTile.x) > 3 || Mathf.Abs(item.beh_tile_target.y - worldTile.y) > 3)
						{
							Royal.RegisterTask();
							item.setTask("fp_royal_march");
							item.beh_tile_target = Near(worldTile);
						}
					}
				}
				catch (Exception ex)
				{
					Log.Error("service: settler follow: " + ex.Message);
				}
			}
		}

		private static void StopFollowing(Actor s)
		{
			try
			{
				if (((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)s.ai)?.task != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)s.ai).task.id == "fp_royal_march")
				{
					s.cancelAllBeh();
				}
			}
			catch
			{
			}
		}

		private static WorldTile Near(WorldTile t)
		{
			for (int i = 0; i < 10; i++)
			{
				WorldTile tile = World.world.GetTile(t.x + UnityEngine.Random.Range(-3, 4), t.y + UnityEngine.Random.Range(-3, 4));
				if (tile != null && tile != t && tile.Type != null && tile.Type.ground && !tile.Type.block && !tile.is_liquid)
				{
					return tile;
				}
			}
			return t;
		}

		public static void Reset()
		{
			Command = CommandKind.Follow;
			CommandTile = null;
			CommandLabel = "Following you";
			_commandBy = null;
			_last = null;
			_lastFor = null;
			_heardFor = null;
			_roundsBody = null;
			_postsFor = -1L;
			_heardKey = null;
			_pendingKey = null;
			_attacking = null;
			_defending = null;
			_armyFor = null;
			_commission = null;
			_commissionFor = -1L;
		}

		private static void RoundsOrder(Actor you, Order o, City home, Kingdom realm)
		{
			o.Kind = OrderKind.Patrol;
			Vector2 vector = CityPoint(home);
			if (_roundsBody != you)
			{
				_roundsBody = you;
				_postsFor = -1L;
			}
			if (OffDuty(you, out var months))
			{
				o.HasWhere = false;
				o.Title = "Off duty in " + home.name;
				o.Detail = string.Format("{0} is at peace. Your rounds are walked; the next in {1} {2}. Orders come with war.", realm.name, months, (months == 1) ? "month" : "months");
				return;
			}
			if (vector != Vector2.zero && (you.current_position - vector).sqrMagnitude > 4900f)
			{
				Point(o, vector);
				o.Title = "Return to " + home.name;
				o.Detail = realm.name + " is at peace. Your post is in " + home.name + ": go back to walk its rounds.";
				return;
			}
			PlanPosts(home, you);
			if (_posts.Count == 0)
			{
				o.HasWhere = false;
				o.Title = "Keep the peace in " + home.name;
				o.Detail = realm.name + " is at peace. Orders come with war.";
			}
			else
			{
				int num = Mathf.Clamp(_post, 0, _posts.Count - 1);
				Point(o, _posts[num]);
				o.Title = $"Walk the rounds of {home.name}: post {num + 1} of {_posts.Count}";
				o.Detail = realm.name + " is at peace. Check the posts round " + home.name + ": walk to each marker. The crown pays when the rounds are done.";
			}
		}

		private static int SavedPost(Actor you, long town)
		{
			try
			{
				you.data.get("fp_rounds_post", out var pResult, null);
				int num = pResult?.IndexOf(':') ?? (-1);
				if (num > 0 && long.TryParse(pResult.Substring(0, num), out var result) && result == town && int.TryParse(pResult.Substring(num + 1), out var result2))
				{
					return Mathf.Clamp(result2, 0, 3);
				}
			}
			catch
			{
			}
			return 0;
		}

		private static void SavePost(Actor you, int post)
		{
			try
			{
				if (post <= 0)
				{
					you.data.removeString("fp_rounds_post");
				}
				else
				{
					you.data.set("fp_rounds_post", _postsFor + ":" + post);
				}
			}
			catch
			{
			}
		}

		private static bool OffDuty(Actor you, out int months)
		{
			months = 0;
			try
			{
				you.data.get("fp_rounds", out var pResult, float.MinValue);
				if (pResult == float.MinValue)
				{
					return false;
				}
				double num = (double)(pResult + 60f) - Quests.Now;
				if (num <= 0.0)
				{
					return false;
				}
				months = Mathf.Max(1, Mathf.CeilToInt((float)num / 5f));
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static void PlanPosts(City home, Actor you)
		{
			long iD;
			try
			{
				iD = home.getID();
			}
			catch
			{
				return;
			}
			if (iD == _postsFor && _posts.Count > 0)
			{
				return;
			}
			_postsFor = iD;
			_posts.Clear();
			_post = SavedPost(you, iD);
			Vector2 vector = CityPoint(home);
			try
			{
				foreach (Building building in home.buildings)
				{
					if (_posts.Count < 3)
					{
						if (building != null && building.isAlive() && building.asset != null && !(building.asset.type != "type_watch_tower") && building.current_tile != null)
						{
							_posts.Add(new Vector2((float)building.current_tile.x + 0.5f, (float)building.current_tile.y - 1f));
						}
						continue;
					}
					break;
				}
				if (_posts.Count < 3 && home.zones != null)
				{
					Vector2[] best = new Vector2[3];
					float[] array = new float[3];
					foreach (TileZone zone in home.zones)
					{
						if (zone == null || zone.centerTile == null)
						{
							continue;
						}
						Vector2 vector2 = new Vector2((float)zone.centerTile.x + 0.5f, (float)zone.centerTile.y + 0.5f);
						Vector2 vector3 = vector2 - vector;
						if (!(vector3.sqrMagnitude < 4f) && zone.centerTile.Type != null && !zone.centerTile.Type.liquid && !zone.centerTile.Type.block)
						{
							int num = Mathf.Clamp((int)(Mathf.Repeat(Mathf.Atan2(vector3.y, vector3.x) + MathF.PI, MathF.PI * 2f) / (MathF.PI * 2f / 3f)), 0, 2);
							if (vector3.sqrMagnitude > array[num])
							{
								array[num] = vector3.sqrMagnitude;
								best[num] = vector2;
							}
						}
					}
					int s;
					for (s = 0; s < 3; s++)
					{
						if (_posts.Count >= 3)
						{
							break;
						}
						if (array[s] > 0f && !_posts.Exists((Vector2 q) => (q - best[s]).sqrMagnitude < 9f))
						{
							_posts.Add(best[s]);
						}
					}
				}
				Vector2 muster = MusterPoint(home);
				if (_posts.Count < 3 && muster != Vector2.zero && !_posts.Exists((Vector2 q) => (q - muster).sqrMagnitude < 9f))
				{
					_posts.Add(muster);
				}
			}
			catch (Exception ex)
			{
				Log.Error("service: rounds: " + ex.Message);
			}
			if (_posts.Count > 0)
			{
				Log.Info(string.Format("service: rounds of {0}: {1} posts ({2})", home.name, _posts.Count, string.Join(", ", _posts.ConvertAll((Vector2 p) => p.ToString("F0")).ToArray())));
			}
		}

		private static bool AtPost(Actor you, Vector2 post)
		{
			if ((you.current_position - post).sqrMagnitude <= 16f)
			{
				return true;
			}
			try
			{
				TileZone tileZone = you.current_tile?.zone;
				WorldTile tile = World.world.GetTile((int)post.x, (int)post.y);
				return tileZone != null && tile != null && tile.zone == tileZone;
			}
			catch
			{
				return false;
			}
		}

		private static void WalkRounds(Actor you, Order o, Action<string> toast)
		{
			if (o == null || o.Kind != OrderKind.Patrol || !o.HasWhere || _posts.Count == 0 || !o.Title.StartsWith("Walk the rounds", StringComparison.Ordinal) || !AtPost(you, o.Where))
			{
				return;
			}
			_post++;
			SavePost(you, (_post < _posts.Count) ? _post : 0);
			if (_post < _posts.Count)
			{
				toast?.Invoke($"Post {_post} of {_posts.Count} checked.");
				Log.Info($"service: rounds post {_post} of {_posts.Count} checked");
				return;
			}
			int num = 2 + Mathf.RoundToInt(YearsServed(you));
			int num2 = CrownPays(you, num);
			try
			{
				you.data.set("fp_rounds", (float)Quests.Now);
			}
			catch
			{
			}
			string text = Order.NameOf(o.Home) ?? "the town";
			try
			{
				Chronicle.AddOwnMemory(you, "fp_rounds", text);
			}
			catch
			{
			}
			toast?.Invoke("Rounds walked. " + PaidLine(num2, num, toYou: true));
			Log.Info($"service: rounds of {text} walked; paid {num2} of {num}");
			_post = 0;
			_postsFor = -1L;
		}
	}
}
