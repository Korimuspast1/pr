using System;
using System.Collections.Generic;
using FirstPerson.Towns;
using UnityEngine;
using ai;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class Royal
	{
		internal sealed class Campaign
		{
			public Kingdom Home;

			public Kingdom Target;

			public string TargetName;

			public float Fraction;

			public readonly List<Actor> Troops = new List<Actor>();

			public readonly Dictionary<Actor, long> Ids = new Dictionary<Actor, long>();

			public int Sent;

			public long KingId = -1L;

			public City Objective;

			public string Status;
		}

		private sealed class BehSailAsOrdered : BehaviourActionActor
		{
			private static readonly HashSet<long> _told = new HashSet<long>();

			public override BehResult execute(Actor a)
			{
				WorldTile beh_tile_target = a.beh_tile_target;
				if (beh_tile_target == null || !WarMap.Ordered(a, out var _))
				{
					return BehResult.Stop;
				}
				if (a.goTo(beh_tile_target) != ExecuteEvent.False)
				{
					return BehResult.Continue;
				}
				long item = 0L;
				try
				{
					item = a.data.id * 100003 + (long)beh_tile_target.x * 1009L + beh_tile_target.y;
				}
				catch
				{
				}
				if (_told.Count > 512)
				{
					_told.Clear();
				}
				if (_told.Add(item))
				{
					Log.Info("fleet: " + WorldBoxMod.Describe(a) + " finds no way to " + beh_tile_target.x + "," + beh_tile_target.y + " just now; trying again");
				}
				a.timer_action = 1f;
				return BehResult.RepeatStep;
			}
		}

		private sealed class BehRideAsOrdered : BehaviourActionActor
		{
			public override BehResult execute(Actor a)
			{
				if (!WarMap.Ordered(a, out var _))
				{
					return BehResult.Continue;
				}
				a.timer_action = 1f;
				return BehResult.RepeatStep;
			}
		}

		internal enum Deed
		{
			Bandits,
			Undead,
			Demons,
			Zombies,
			Plague,
			Madness,
			Meteor,
			Tornado,
			Earthquake,
			FireCloud,
			AcidCloud,
			LavaCloud,
			Rebellion,
			Assassin,
			MeteorStorm,
			LightningStorm,
			BombStorm,
			Napalm,
			CrabBombs,
			Nuke,
			CzarBomba
		}

		internal sealed class RaidParty
		{
			public City Target;

			public string TargetName;

			public string What;

			public Kingdom By;

			public readonly List<Actor> Units = new List<Actor>();

			private readonly List<long> _ids = new List<long>();

			public int Sent;

			public string Status;

			public void Add(Actor a)
			{
				Units.Add(a);
				long item = -1L;
				try
				{
					item = a.data.id;
				}
				catch
				{
				}
				_ids.Add(item);
			}

			public void Prune()
			{
				for (int num = Units.Count - 1; num >= 0; num--)
				{
					Actor actor = Units[num];
					bool flag;
					try
					{
						flag = actor != null && actor.isAlive() && actor.data.id == _ids[num];
					}
					catch
					{
						flag = false;
					}
					if (!flag)
					{
						Units.RemoveAt(num);
						_ids.RemoveAt(num);
					}
				}
			}
		}

		internal enum Storm
		{
			Meteors,
			Lightning,
			Wrath,
			Bombs,
			Napalm,
			Crabs
		}

		internal sealed class StormRun
		{
			public City Target;

			public string TargetName;

			public Storm Kind;

			public Actor Caller;

			public Kingdom By;

			public int Meteors;

			public int MeteorsLeft;

			public int Bolts;

			public int BoltsLeft;

			public float NextMeteor;

			public float NextBolt;

			public string DropId;

			public int Drops;

			public int DropsLeft;

			public float NextDrop;

			public float GapMin;

			public float GapMax;

			public Vector2 LineFrom;

			public Vector2 LineTo;

			public string Status;
		}

		public const string MarchTask = "fp_royal_march";

		private const float OrderSeconds = 1.5f;

		public bool Open;

		public Vector2 Scroll;

		public Kingdom Selected;

		public int TownIndex;

		public Campaign Active;

		private float _timer;

		private static bool _taskRegistered;

		public const string HoldTask = "fp_royal_hold";

		private static bool _holdRegistered;

		public const string SailTask = "fp_royal_sail";

		private static bool _sailRegistered;

		public const string BoardTask = "fp_royal_board";

		private static bool _boardRegistered;

		internal static readonly Deed[] Deeds = (Deed[])Enum.GetValues(typeof(Deed));

		internal static readonly Deed[][] DeedRows = new Deed[3][]
		{
			new Deed[8]
			{
				Deed.Bandits,
				Deed.Undead,
				Deed.Demons,
				Deed.Zombies,
				Deed.Plague,
				Deed.Madness,
				Deed.Rebellion,
				Deed.Assassin
			},
			new Deed[8]
			{
				Deed.Meteor,
				Deed.MeteorStorm,
				Deed.LightningStorm,
				Deed.Tornado,
				Deed.Earthquake,
				Deed.FireCloud,
				Deed.AcidCloud,
				Deed.LavaCloud
			},
			new Deed[5]
			{
				Deed.BombStorm,
				Deed.Napalm,
				Deed.CrabBombs,
				Deed.Nuke,
				Deed.CzarBomba
			}
		};

		private Kingdom _by;

		public readonly List<RaidParty> Raids = new List<RaidParty>();

		private static Royal _instance;

		private readonly Dictionary<string, float> _cooldown = new Dictionary<string, float>();

		private float _raidTimer;

		internal static readonly Storm[] StormKinds = new Storm[3]
		{
			Storm.Meteors,
			Storm.Lightning,
			Storm.Wrath
		};

		public readonly List<StormRun> Storms = new List<StormRun>();

		public bool ObeliskOpen;

		public Vector2 ObeliskScroll;

		public const string SpentKey = "fp_kills_spent";

		private Deed _armed;

		private City _armedAt;

		private float _armedUntil;

		public static RaidParty Raid
		{
			get
			{
				if (_instance == null || _instance.Raids.Count <= 0)
				{
					return null;
				}
				return _instance.Raids[0];
			}
		}

		public static bool IsKing(Actor a)
		{
			try
			{
				return a != null && a.isAlive() && a.isKing() && a.kingdom != null && a.kingdom.isCiv();
			}
			catch
			{
				return false;
			}
		}

		public void Toggle(Actor host, Action<string> toast)
		{
			if (Open)
			{
				Open = false;
			}
			else if (!IsKing(host))
			{
				string text = "a " + ((host?.asset != null) ? host.asset.id.Replace('_', ' ') : "creature");
				try
				{
					if (host != null && host.isCityLeader())
					{
						text = "a city leader, not the king";
					}
				}
				catch
				{
				}
				toast("Only a king can call the royal council. You are " + text + ".");
			}
			else
			{
				Open = true;
			}
		}

		public static List<Kingdom> Rivals(Kingdom home)
		{
			List<Kingdom> list = new List<Kingdom>();
			try
			{
				foreach (Kingdom item in World.world.kingdoms.list)
				{
					if (item != null && item != home && item.isAlive() && item.isCiv() && item.hasCities())
					{
						list.Add(item);
					}
				}
				list.Sort((Kingdom a, Kingdom b) => Distance(home, a).CompareTo(Distance(home, b)));
			}
			catch (Exception ex)
			{
				Log.Error("listing kingdoms failed: " + ex.Message);
			}
			return list;
		}

		private static float Distance(Kingdom a, Kingdom b)
		{
			try
			{
				return Kingdom.distanceBetweenKingdom(a, b);
			}
			catch
			{
				return 0f;
			}
		}

		public static bool AtWar(Kingdom a, Kingdom b)
		{
			try
			{
				return a.isInWarWith(b);
			}
			catch
			{
				return false;
			}
		}

		public static bool Allied(Kingdom a, Kingdom b)
		{
			try
			{
				Alliance alliance = a.getAlliance();
				return alliance != null && alliance == b.getAlliance();
			}
			catch
			{
				return false;
			}
		}

		public static int Warriors(Kingdom k)
		{
			try
			{
				return k.countTotalWarriors();
			}
			catch
			{
				return 0;
			}
		}

		public static int CityCount(Kingdom k)
		{
			try
			{
				return k.countCities();
			}
			catch
			{
				return 0;
			}
		}

		public static int Population(Kingdom k)
		{
			try
			{
				return k.countUnits();
			}
			catch
			{
				return 0;
			}
		}

		public static string NameOf(Kingdom k)
		{
			try
			{
				return k.name;
			}
			catch
			{
				return "?";
			}
		}

		public static Color ColorOf(Kingdom k)
		{
			try
			{
				return k.getColor().getColorMain();
			}
			catch
			{
				return Color.white;
			}
		}

		private static bool Ruling(Actor host, Action<string> toast)
		{
			bool flag = false;
			try
			{
				flag = host != null && host.isAlive() && host.kingdom != null && host.kingdom.isAlive();
			}
			catch
			{
			}
			if (!flag)
			{
				toast("You no longer rule a kingdom.");
			}
			return flag;
		}

		public void DeclareWar(Actor host, Kingdom target, Action<string> toast, Action horn)
		{
			if (!Ruling(host, toast))
			{
				return;
			}
			if (target == null || !target.isAlive())
			{
				toast("That kingdom is no more.");
				return;
			}
			Kingdom kingdom = host.kingdom;
			if (AtWar(kingdom, target))
			{
				toast("You are already at war with " + NameOf(target) + ".");
				return;
			}
			if (Allied(kingdom, target))
			{
				toast(NameOf(target) + " is your ally. You cannot declare war on an ally.");
				return;
			}
			try
			{
				try
				{
					World.world.diplomacy.startWar(kingdom, target, WarTypeLibrary.normal, pLog: true);
				}
				catch (NullReferenceException)
				{
					if (AtWar(kingdom, target))
					{
						throw;
					}
					World.world.diplomacy.startWar(kingdom, target, WarTypeLibrary.normal, pLog: false);
				}
				toast(NameOf(kingdom) + " declares war on " + NameOf(target) + "!");
				horn();
				Log.Info("war declared: " + NameOf(kingdom) + " -> " + NameOf(target));
			}
			catch (Exception ex2)
			{
				Log.Error("declaring war failed: " + ex2);
				toast("The heralds could not deliver your declaration.");
			}
		}

		public void SendArmy(Actor host, Kingdom target, float fraction, Action<string> toast, Action horn, City at = null)
		{
			if (!Ruling(host, toast))
			{
				return;
			}
			Kingdom kingdom = host.kingdom;
			if (!AtWar(kingdom, target))
			{
				toast("You are not at war with " + NameOf(target) + ". Declare war first.");
				return;
			}
			bool flag = false;
			try
			{
				flag = at != null && at.isAlive() && at.kingdom == target;
			}
			catch
			{
			}
			City city = (flag ? at : NearestCity(target, CapitalPosition(kingdom, host)));
			if (city == null)
			{
				toast(NameOf(target) + " has no cities left to take.");
				return;
			}
			bool flag2 = false;
			if (flag && Active != null && Active.Home == kingdom && Active.Target == target && Active.Objective != city)
			{
				Active.Objective = city;
				foreach (Actor troop in Active.Troops)
				{
					try
					{
						if (troop != null && troop.isAlive() && IsMarching(troop))
						{
							troop.cancelAllBeh();
						}
					}
					catch
					{
					}
				}
				_timer = 0f;
				flag2 = true;
				toast("Your army turns on " + city.name + ".");
				horn();
				Log.Info("campaign: turned on " + city.name + " (" + NameOf(target) + ")");
			}
			List<Actor> list = new List<Actor>();
			try
			{
				foreach (Actor unit in kingdom.getUnits())
				{
					if (unit != null && unit != host && unit.isAlive() && unit.isWarrior())
					{
						list.Add(unit);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("listing warriors failed: " + ex.Message);
			}
			if (list.Count == 0)
			{
				toast("You have no warriors. Your cities train them as they grow and arm themselves.");
				return;
			}
			Vector2 goal = CityPosition(city);
			list.RemoveAll((Actor a) => !Reaches(a, goal));
			if (list.Count == 0)
			{
				toast("None of your warriors can reach " + city.name + " by land.");
				return;
			}
			list.Sort((Actor a, Actor b) => (a.current_position - goal).sqrMagnitude.CompareTo((b.current_position - goal).sqrMagnitude));
			int num = Mathf.Clamp(Mathf.RoundToInt((float)list.Count * fraction), 1, list.Count);
			if (Active != null && Active.Home == kingdom && Active.Target == target)
			{
				Active.Troops.RemoveAll((Actor a) => a == null || !a.isAlive());
				int num2 = 0;
				for (int num3 = 0; num3 < list.Count; num3++)
				{
					if (Active.Troops.Count >= num)
					{
						break;
					}
					if (!Active.Troops.Contains(list[num3]))
					{
						Active.Troops.Add(list[num3]);
						num2++;
					}
				}
				string text = ((Active.Objective != null) ? Active.Objective.name : city.name);
				if (num2 == 0)
				{
					if (!flag2)
					{
						toast("Your army is already marching on " + text + " (" + Active.Troops.Count + " warriors).");
					}
				}
				else
				{
					Active.Sent += num2;
					Active.Fraction = Mathf.Max(Active.Fraction, fraction);
					Order(Active);
					horn();
					toast(num2 + " more warriors join the march on " + text + "!");
					Log.Info($"campaign: {num2} more warriors of {NameOf(kingdom)} join the march on {text} ({Active.Troops.Count} in all)");
				}
				return;
			}
			RegisterTask();
			if (Active != null)
			{
				Recall(null);
			}
			Active = new Campaign
			{
				Home = kingdom,
				Target = target,
				TargetName = NameOf(target),
				Fraction = fraction,
				Objective = city,
				Sent = num
			};
			try
			{
				Active.KingId = ((kingdom.king != null) ? kingdom.king.data.id : (-1));
			}
			catch
			{
			}
			for (int num4 = 0; num4 < num; num4++)
			{
				Active.Troops.Add(list[num4]);
			}
			_timer = 0f;
			Order(Active);
			horn();
			toast(num + " of your " + list.Count + " warriors march on " + city.name + "!");
			Log.Info($"campaign: {num}/{list.Count} warriors of {NameOf(kingdom)} -> {city.name} ({NameOf(target)})");
		}

		public void Recall(Action<string> toast)
		{
			if (Active == null)
			{
				return;
			}
			foreach (Actor troop in Active.Troops)
			{
				try
				{
					if (troop != null && troop.isAlive() && IsMarching(troop))
					{
						troop.cancelAllBeh();
					}
				}
				catch
				{
				}
			}
			toast?.Invoke("Your army is recalled. The warriors return home.");
			Active = null;
		}

		public void Update(float dt, Action<string> toast)
		{
			Peace.Update(dt);
			try
			{
				Regalia.Update(dt);
			}
			catch (Exception ex)
			{
				Log.Error("regalia: " + ex.Message);
			}
			UpdateRaids(dt, toast);
			UpdateStorms(toast);
			if (Active == null)
			{
				return;
			}
			_timer -= dt;
			if (_timer > 0f)
			{
				return;
			}
			_timer = 1.5f;
			Campaign c = Active;
			bool flag;
			try
			{
				flag = c.Home == null || !c.Home.isAlive() || c.Target == null;
			}
			catch
			{
				flag = true;
			}
			if (flag)
			{
				Active = null;
				return;
			}
			long num = -1L;
			try
			{
				num = ((c.Home.king != null && c.Home.king.isAlive()) ? c.Home.king.data.id : (-1));
			}
			catch
			{
			}
			if (c.KingId != -1 && num != c.KingId)
			{
				Log.Info("campaign: the king who sent it no longer rules; the army goes home");
				Recall(null);
				return;
			}
			c.Troops.RemoveAll((Actor a) => a == null || !a.isAlive() || a.kingdom != c.Home || Reborn(c, a));
			if (c.Troops.Count == 0)
			{
				toast("Your army has fallen. None of the " + c.Sent + " warriors you sent are left.");
				Active = null;
				return;
			}
			if (!AtWar(c.Home, c.Target))
			{
				toast("The war with " + c.TargetName + " is over. Your warriors come home.");
				Recall(null);
				return;
			}
			if (c.Objective == null || !c.Objective.isAlive() || c.Objective.kingdom != c.Target)
			{
				City objective = c.Objective;
				c.Objective = NearestCity(c.Target, Centroid(c.Troops));
				if (c.Objective == null)
				{
					toast(c.TargetName + " has fallen! Your warriors return in triumph.");
					Recall(null);
					return;
				}
				if (objective != null)
				{
					toast((objective.isAlive() ? (objective.name + " is taken") : "The city is destroyed") + ". Onward to " + c.Objective.name + "!");
				}
			}
			Order(c);
		}

		private static bool Reaches(Actor a, Vector2 goal)
		{
			try
			{
				WorldTile tile = World.world.GetTile((int)goal.x, (int)goal.y);
				return tile == null || WarMap.CanWalkTo(a, tile);
			}
			catch
			{
				return true;
			}
		}

		private static bool Reborn(Campaign c, Actor a)
		{
			try
			{
				long id = a.data.id;
				if (!c.Ids.TryGetValue(a, out var value))
				{
					c.Ids[a] = id;
					return false;
				}
				return value != id;
			}
			catch
			{
				return false;
			}
		}

		private void Order(Campaign c)
		{
			Vector2 vector = CityPosition(c.Objective);
			int num = 0;
			int num2 = 0;
			foreach (Actor troop in c.Troops)
			{
				try
				{
					if (troop.isFighting())
					{
						num2++;
						continue;
					}
					bool flag = (troop.current_position - vector).sqrMagnitude < 36f;
					if (flag)
					{
						num++;
					}
					if ((!IsMarching(troop) || flag) && (flag || Reaches(troop, vector)))
					{
						WorldTile worldTile = RandomTileOf(c.Objective);
						if (worldTile != null)
						{
							troop.setTask("fp_royal_march");
							troop.beh_tile_target = worldTile;
						}
					}
				}
				catch (Exception ex)
				{
					Log.Error("marching order failed: " + ex.Message);
				}
			}
			c.Status = c.Troops.Count + " warriors marching on " + c.Objective.name + ((num2 > 0) ? (", " + num2 + " fighting") : "") + ((num > 0) ? (", " + num + " at the walls") : "");
		}

		private static bool IsMarching(Actor a)
		{
			try
			{
				return a.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id == "fp_royal_march" && a.beh_tile_target != null;
			}
			catch
			{
				return false;
			}
		}

		internal static void RegisterTask()
		{
			if (_taskRegistered)
			{
				return;
			}
			try
			{
				if (AssetManager.tasks_actor.get("fp_royal_march") == null)
				{
					BehaviourTaskActor behaviourTaskActor = new BehaviourTaskActor
					{
						id = "fp_royal_march",
						locale_key = "task_unit_move"
					};
					behaviourTaskActor.setIcon("ui/Icons/iconArrowAttackTarget");
					BehGoToTileTarget behGoToTileTarget = new BehGoToTileTarget();
					((BehaviourActionBase<Actor>)behGoToTileTarget).setupErrorChecks();
					behaviourTaskActor.addBeh(behGoToTileTarget);
					BehRandomWait behRandomWait = new BehRandomWait(0.5f, 1.5f);
					((BehaviourActionBase<Actor>)behRandomWait).setupErrorChecks();
					behaviourTaskActor.addBeh(behRandomWait);
					AssetManager.tasks_actor.add(behaviourTaskActor);
				}
				_taskRegistered = true;
			}
			catch (Exception ex)
			{
				Log.Error("could not register the march task: " + ex);
			}
		}

		private static WorldTile RandomTileOf(City city)
		{
			try
			{
				if (city.buildings != null && city.buildings.Count > 0)
				{
					Building building = city.buildings[UnityEngine.Random.Range(0, city.buildings.Count)];
					if (building != null && building.current_tile != null)
					{
						return building.current_tile;
					}
				}
				if (city.zones != null && city.zones.Count > 0)
				{
					TileZone tileZone = city.zones[UnityEngine.Random.Range(0, city.zones.Count)];
					if (tileZone.tiles != null && tileZone.tiles.Length != 0)
					{
						return tileZone.tiles[UnityEngine.Random.Range(0, tileZone.tiles.Length)];
					}
				}
				return city.getTile();
			}
			catch
			{
				return null;
			}
		}

		private static Vector2 CityPosition(City c)
		{
			try
			{
				WorldTile tile = c.getTile();
				if (tile != null)
				{
					return new Vector2(tile.posV3.x, tile.posV3.y);
				}
			}
			catch
			{
			}
			return Vector2.zero;
		}

		private static Vector2 CapitalPosition(Kingdom k, Actor fallback)
		{
			try
			{
				if (k.capital != null)
				{
					return CityPosition(k.capital);
				}
			}
			catch
			{
			}
			return fallback.current_position;
		}

		private static Vector2 Centroid(List<Actor> actors)
		{
			Vector2 zero = Vector2.zero;
			foreach (Actor actor in actors)
			{
				zero += actor.current_position;
			}
			if (actors.Count <= 0)
			{
				return Vector2.zero;
			}
			return zero / actors.Count;
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
						float sqrMagnitude = (CityPosition(city) - from).sqrMagnitude;
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

		public int SendTo(Actor host, List<Actor> warriors, WorldTile tile, Action<string> toast)
		{
			if (!Ruling(host, toast) || tile == null)
			{
				return 0;
			}
			RegisterTask();
			int num = 0;
			foreach (Actor warrior in warriors)
			{
				try
				{
					if (warrior != null && warrior.isAlive() && warrior.kingdom == host.kingdom && warrior != host && warrior.ai != null)
					{
						warrior.setTask("fp_royal_march");
						warrior.beh_tile_target = Around(tile);
						if (Active != null)
						{
							Active.Troops.Remove(warrior);
						}
						num++;
					}
				}
				catch (Exception ex)
				{
					Log.Error("war map order failed: " + ex.Message);
				}
			}
			if (Active != null && Active.Troops.Count == 0)
			{
				Active = null;
			}
			return num;
		}

		public void LeaveCampaign(Actor a)
		{
			if (Active != null)
			{
				Active.Troops.Remove(a);
				if (Active.Troops.Count == 0)
				{
					Active = null;
				}
			}
		}

		internal static bool Hold(Actor a, WorldTile flag, float spread)
		{
			if (a == null || a.ai == null || flag == null)
			{
				return false;
			}
			RegisterHoldTask();
			try
			{
				a.setTask("fp_royal_hold");
				a.beh_tile_target = Near(flag, spread);
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("flag order failed: " + ex.Message);
				return false;
			}
		}

		internal static bool IsHolding(Actor a)
		{
			try
			{
				return a.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id == "fp_royal_hold" && a.beh_tile_target != null;
			}
			catch
			{
				return false;
			}
		}

		internal static bool IsMarchingTo(Actor a, out WorldTile to)
		{
			to = null;
			if (!IsMarching(a))
			{
				return false;
			}
			to = a.beh_tile_target;
			return true;
		}

		internal static bool Sail(Actor ship, WorldTile water)
		{
			if (ship == null || ship.ai == null || water == null)
			{
				return false;
			}
			RegisterSailTask();
			try
			{
				if (ship.is_inside_building)
				{
					ship.exitBuilding();
				}
				ship.setTask("fp_royal_sail");
				ship.beh_tile_target = water;
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("fleet order failed: " + ex.Message);
				return false;
			}
		}

		internal static bool IsSailing(Actor a)
		{
			try
			{
				return a.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id == "fp_royal_sail" && a.beh_tile_target != null;
			}
			catch
			{
				return false;
			}
		}

		internal static void ReleaseShip(Actor a)
		{
			try
			{
				if (a != null && a.isAlive() && IsSailing(a))
				{
					a.cancelAllBeh();
				}
			}
			catch
			{
			}
		}

		private static void RegisterSailTask()
		{
			if (_sailRegistered)
			{
				return;
			}
			try
			{
				if (AssetManager.tasks_actor.get("fp_royal_sail") == null)
				{
					BehaviourTaskActor behaviourTaskActor = new BehaviourTaskActor
					{
						id = "fp_royal_sail",
						locale_key = "task_unit_move"
					};
					behaviourTaskActor.setIcon("ui/Icons/iconBoat");
					BehSailAsOrdered behSailAsOrdered = new BehSailAsOrdered();
					((BehaviourActionBase<Actor>)behSailAsOrdered).setupErrorChecks();
					behaviourTaskActor.addBeh(behSailAsOrdered);
					BehRideAsOrdered behRideAsOrdered = new BehRideAsOrdered();
					((BehaviourActionBase<Actor>)behRideAsOrdered).setupErrorChecks();
					behaviourTaskActor.addBeh(behRideAsOrdered);
					AssetManager.tasks_actor.add(behaviourTaskActor);
				}
				_sailRegistered = true;
			}
			catch (Exception ex)
			{
				Log.Error("could not register the sail task: " + ex);
			}
		}

		internal static bool Board(Actor a, WorldTile shore, float spread)
		{
			if (a == null || a.ai == null || shore == null)
			{
				return false;
			}
			RegisterBoardTask();
			try
			{
				if (a.is_inside_building)
				{
					a.exitBuilding();
				}
				a.setTask("fp_royal_board");
				a.beh_tile_target = Near(shore, spread);
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("boarding order failed: " + ex.Message);
				return false;
			}
		}

		internal static bool IsBoarding(Actor a)
		{
			try
			{
				return a.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id == "fp_royal_board" && a.beh_tile_target != null;
			}
			catch
			{
				return false;
			}
		}

		internal static void StopBoarding(Actor a)
		{
			try
			{
				if (a != null && a.isAlive() && IsBoarding(a))
				{
					a.cancelAllBeh();
				}
			}
			catch
			{
			}
		}

		private static void RegisterBoardTask()
		{
			if (_boardRegistered)
			{
				return;
			}
			try
			{
				if (AssetManager.tasks_actor.get("fp_royal_board") == null)
				{
					BehaviourTaskActor behaviourTaskActor = new BehaviourTaskActor
					{
						id = "fp_royal_board",
						locale_key = "task_unit_move"
					};
					behaviourTaskActor.setIcon("ui/Icons/iconBoat");
					BehGoToTileTarget behGoToTileTarget = new BehGoToTileTarget();
					((BehaviourActionBase<Actor>)behGoToTileTarget).setupErrorChecks();
					behaviourTaskActor.addBeh(behGoToTileTarget);
					BehRandomWait behRandomWait = new BehRandomWait(5f, 9f);
					((BehaviourActionBase<Actor>)behRandomWait).setupErrorChecks();
					behaviourTaskActor.addBeh(behRandomWait);
					AssetManager.tasks_actor.add(behaviourTaskActor);
				}
				_boardRegistered = true;
			}
			catch (Exception ex)
			{
				Log.Error("could not register the boarding task: " + ex);
			}
		}

		internal static void StandDown(Actor a)
		{
			try
			{
				if (a != null && a.isAlive() && IsHolding(a))
				{
					a.cancelAllBeh();
				}
			}
			catch
			{
			}
		}

		private static void RegisterHoldTask()
		{
			if (_holdRegistered)
			{
				return;
			}
			try
			{
				if (AssetManager.tasks_actor.get("fp_royal_hold") == null)
				{
					BehaviourTaskActor behaviourTaskActor = new BehaviourTaskActor
					{
						id = "fp_royal_hold",
						locale_key = "task_unit_move"
					};
					behaviourTaskActor.setIcon("ui/Icons/iconArrowAttackTarget");
					BehGoToTileTarget behGoToTileTarget = new BehGoToTileTarget();
					((BehaviourActionBase<Actor>)behGoToTileTarget).setupErrorChecks();
					behaviourTaskActor.addBeh(behGoToTileTarget);
					BehRandomWait behRandomWait = new BehRandomWait(4f, 9f);
					((BehaviourActionBase<Actor>)behRandomWait).setupErrorChecks();
					behaviourTaskActor.addBeh(behRandomWait);
					AssetManager.tasks_actor.add(behaviourTaskActor);
				}
				_holdRegistered = true;
			}
			catch (Exception ex)
			{
				Log.Error("could not register the hold task: " + ex);
			}
		}

		internal static bool Standable(WorldTile t)
		{
			try
			{
				return t != null && t.Type != null && t.Type.ground && !t.Type.block && !t.Type.lava && !t.is_liquid;
			}
			catch
			{
				return false;
			}
		}

		private static WorldTile Near(WorldTile t, float spread)
		{
			if (t == null)
			{
				return null;
			}
			try
			{
				for (int i = 0; i < 14; i++)
				{
					float f = UnityEngine.Random.value * MathF.PI * 2f;
					float num = Mathf.Sqrt(UnityEngine.Random.value) * spread;
					WorldTile tile = World.world.GetTile(t.x + Mathf.RoundToInt(Mathf.Cos(f) * num), t.y + Mathf.RoundToInt(Mathf.Sin(f) * num));
					if (tile != null && Standable(tile) && (tile == t || tile.isSameIsland(t)))
					{
						return tile;
					}
				}
			}
			catch
			{
			}
			return t;
		}

		public Royal()
		{
			_instance = this;
		}

		public float CooldownLeft(string power)
		{
			if (!_cooldown.TryGetValue(power, out var value))
			{
				return 0f;
			}
			return Mathf.Max(0f, value - Time.unscaledTime);
		}

		private void Cool(string power, float seconds)
		{
			_cooldown[power] = Time.unscaledTime + seconds;
		}

		internal void ReadyForTest(string power)
		{
			_cooldown.Remove(power);
		}

		public static string Label(Deed d)
		{
			switch (d)
			{
			case Deed.Bandits:
				return "Hire bandits";
			case Deed.Undead:
				return "Raise the dead";
			case Deed.Demons:
				return "Summon demons";
			case Deed.Zombies:
				return "Zombie outbreak";
			case Deed.Plague:
				return "Spread plague";
			case Deed.Madness:
				return "Sow madness";
			case Deed.Meteor:
				return "Meteor strike";
			case Deed.Tornado:
				return "Tornado";
			case Deed.Earthquake:
				return "Earthquake";
			case Deed.FireCloud:
				return "Fire storm";
			case Deed.AcidCloud:
				return "Acid rain";
			case Deed.LavaCloud:
				return "Lava storm";
			case Deed.Rebellion:
				return "Incite rebellion";
			case Deed.Assassin:
				return "Assassinate king";
			case Deed.MeteorStorm:
				return "Meteor storm";
			case Deed.LightningStorm:
				return "Lightning storm";
			case Deed.BombStorm:
				return "Bomb storm";
			case Deed.Napalm:
				return "Napalm strike";
			case Deed.CrabBombs:
				return "Crab bombs";
			case Deed.Nuke:
				return "Nuke";
			default:
				return "Czar Bomba";
			}
		}

		internal static bool StormOf(Deed d, out Storm s)
		{
			switch (d)
			{
			case Deed.MeteorStorm:
				s = Storm.Meteors;
				return true;
			case Deed.LightningStorm:
				s = Storm.Lightning;
				return true;
			case Deed.BombStorm:
				s = Storm.Bombs;
				return true;
			case Deed.Napalm:
				s = Storm.Napalm;
				return true;
			case Deed.CrabBombs:
				s = Storm.Crabs;
				return true;
			default:
				s = Storm.Meteors;
				return false;
			}
		}

		internal static string KeyOf(Deed d)
		{
			if (!StormOf(d, out var s))
			{
				return "deed_" + d;
			}
			return "storm_" + s;
		}

		internal static float CooldownOf(Deed d)
		{
			if (StormOf(d, out var s))
			{
				return CooldownOf(s);
			}
			switch (d)
			{
			case Deed.Meteor:
				return 8f;
			case Deed.Bandits:
			case Deed.Tornado:
				return 10f;
			case Deed.FireCloud:
			case Deed.AcidCloud:
			case Deed.LavaCloud:
				return 15f;
			case Deed.Undead:
			case Deed.Demons:
			case Deed.Earthquake:
				return 20f;
			case Deed.Zombies:
			case Deed.Plague:
			case Deed.Madness:
				return 30f;
			case Deed.Rebellion:
				return 45f;
			case Deed.Nuke:
				return 60f;
			case Deed.CzarBomba:
				return 120f;
			default:
				return 60f;
			}
		}

		public void Unleash(Actor host, City city, Deed deed, Action<string> toast, Action horn)
		{
			if (!IsKing(host))
			{
				toast("Only a king can order strikes on other kingdoms.");
				return;
			}
			string power = KeyOf(deed);
			if (CooldownLeft(power) > 0f)
			{
				toast(Label(deed) + " is not ready yet (" + Mathf.CeilToInt(CooldownLeft(power)) + " s).");
				return;
			}
			if (city == null || !city.isAlive())
			{
				toast("That town is no more.");
				return;
			}
			if (host.kingdom == null || city.kingdom == host.kingdom)
			{
				toast("You can only do that to a rival's town.");
				return;
			}
			WorldTile worldTile = SafeTile(city);
			if (worldTile == null)
			{
				toast("Your agents can't find " + city.name + ".");
				return;
			}
			int num = PriceOf(host.kingdom, deed);
			if (!CanAfford(host.kingdom, num, Label(deed), toast))
			{
				return;
			}
			_by = host.kingdom;
			Kingdom kingdom = city.kingdom;
			bool flag = Peace.Open(deed);
			if ((flag && Peace.HoldOff(host.kingdom, kingdom, Label(deed), toast)) || HoldOffBlast(host, city, deed, worldTile, toast))
			{
				return;
			}
			string name = city.name;
			bool flag2;
			try
			{
				if (StormOf(deed, out var s))
				{
					flag2 = StartStorm(city, s, toast);
				}
				else
				{
					object obj;
					string pParam;
					object obj2;
					switch (deed)
					{
					case Deed.Bandits:
						flag2 = SendRaiders(city, "bandit", 6, 11, "Bandits", toast);
						if (flag2)
						{
							toast("A band of brigands takes your gold and rides for " + name + "!");
						}
						break;
					case Deed.Undead:
						flag2 = RaiseDead(city, toast);
						break;
					case Deed.Demons:
						flag2 = SendRaiders(city, "demon", 3, 5, "Demons", toast);
						if (flag2)
						{
							toast("The earth splits outside " + name + ". Demons climb out.");
						}
						break;
					case Deed.Zombies:
						flag2 = Afflict(city, "infected", 0.25f, (Actor a) => a.asset != null && a.asset.can_turn_into_zombie && !a.hasTrait("zombie")) > 0;
						if (flag2)
						{
							toast("A sickness that wakes the dead spreads in " + name + ".");
						}
						else
						{
							toast("No one in " + name + " can catch it.");
						}
						break;
					case Deed.Plague:
						flag2 = Afflict(city, "plague", 0.3f, null) > 0;
						if (flag2)
						{
							toast("Plague breaks out in " + name + ".");
						}
						break;
					case Deed.Madness:
						flag2 = Afflict(city, "madness", 0.2f, null) > 0;
						if (flag2)
						{
							toast("Whispers drive the people of " + name + " mad.");
						}
						break;
					case Deed.Meteor:
						Meteorite.spawnMeteorite(Somewhere(city) ?? worldTile);
						toast("You call down fire from the heavens on " + name + "!");
						flag2 = true;
						break;
					case Deed.Tornado:
						EffectsLibrary.spawnAtTile("fx_tornado", Somewhere(city) ?? worldTile, 0.5f);
						toast("A tornado tears towards " + name + "!");
						flag2 = true;
						break;
					case Deed.Earthquake:
						if (Earthquake.isQuakeActive())
						{
							toast("The earth is already shaking. Wait a moment.");
							return;
						}
						Earthquake.startQuake(worldTile);
						toast("The ground under " + name + " heaves and cracks!");
						flag2 = true;
						break;
					case Deed.LavaCloud:
						obj = "cloud_lava";
						goto IL_03df;
					case Deed.AcidCloud:
						obj = "cloud_acid";
						goto IL_03df;
					case Deed.FireCloud:
						obj = "cloud_fire";
						goto IL_03df;
					case Deed.Rebellion:
						flag2 = Rebel(city, toast);
						break;
					case Deed.Nuke:
					case Deed.CzarBomba:
						flag2 = World.world.drop_manager.spawn(worldTile, (deed == Deed.Nuke) ? "atomic_bomb" : "czar_bomba", -1f, -1f, -1L) != null;
						if (flag2)
						{
							toast((deed == Deed.Nuke) ? ("A bomber drones high over " + name + ". Something falls from it...") : ("The Czar Bomba falls on " + name + ". Look away."));
						}
						else
						{
							toast("The bomb never left the hangar.");
						}
						break;
					default:
						{
							flag2 = Assassinate(city.kingdom, toast);
							break;
						}
						IL_03df:
						pParam = (string)obj;
						for (int num2 = 0; num2 < 2; num2++)
						{
							EffectsLibrary.spawn("fx_cloud", Somewhere(city) ?? worldTile, pParam);
						}
						switch (deed)
						{
						default:
							obj2 = "Clouds of molten rock";
							break;
						case Deed.AcidCloud:
							obj2 = "Acid clouds";
							break;
						case Deed.FireCloud:
							obj2 = "Burning clouds";
							break;
						}
						toast((string)obj2 + " gather over " + name + ".");
						flag2 = true;
						break;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("royal deed " + deed.ToString() + " failed: " + ex);
				toast("Your plan against " + name + " came to nothing.");
				return;
			}
			if (flag2)
			{
				Spend(host.kingdom, num);
				Cool(power, CooldownOf(deed));
				if (flag)
				{
					Peace.Struck(host.kingdom, kingdom, city, num, toast, horn);
				}
				if (deed == Deed.Bandits || deed == Deed.Undead || deed == Deed.Demons || deed == Deed.BombStorm || deed == Deed.Nuke || deed == Deed.CzarBomba)
				{
					horn();
				}
				Log.Info($"royal deed: {deed} -> {name} ({NameOf(city.kingdom)}) for {num} kills, {KillsToSpend(host.kingdom)} left");
			}
		}

		private bool SendRaiders(City city, string asset, int min, int max, string what, Action<string> toast)
		{
			if (!AssetManager.actor_library.has(asset))
			{
				toast("No " + what.ToLowerInvariant() + " answer your call in this world.");
				return false;
			}
			WorldTile worldTile = OutsideTile(city);
			if (worldTile == null)
			{
				return false;
			}
			RegisterTask();
			EffectsLibrary.spawn("fx_spawn", worldTile);
			RaidParty raidParty = new RaidParty
			{
				Target = city,
				TargetName = city.name,
				What = what,
				By = _by
			};
			int num = UnityEngine.Random.Range(min, max + 1);
			for (int i = 0; i < num; i++)
			{
				Actor actor = World.world.units.spawnNewUnit(asset, worldTile, i == 0, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
				if (actor != null)
				{
					raidParty.Add(actor);
					March(actor, city);
				}
			}
			if (raidParty.Units.Count == 0)
			{
				toast("The " + what.ToLowerInvariant() + " never showed up.");
				return false;
			}
			raidParty.Sent = raidParty.Units.Count;
			Raids.Insert(0, raidParty);
			return true;
		}

		private bool RaiseDead(City city, Action<string> toast)
		{
			if (!AssetManager.actor_library.has("skeleton"))
			{
				toast("The dead stay buried in this world.");
				return false;
			}
			Building building = null;
			try
			{
				building = city.getBuildingOfType("type_mine", pCountOnlyFinished: true, pRandom: false, pOnlyFreeTile: false, (TileIsland)null);
			}
			catch
			{
			}
			WorldTile worldTile = ((building != null && building.current_tile != null) ? building.current_tile : OutsideTile(city));
			if (worldTile == null)
			{
				return false;
			}
			RegisterTask();
			EffectsLibrary.spawn("fx_spawn", worldTile);
			RaidParty raidParty = new RaidParty
			{
				Target = city,
				TargetName = city.name,
				What = "The dead",
				By = _by
			};
			if (AssetManager.actor_library.has("necromancer"))
			{
				Actor actor = World.world.units.spawnNewUnit("necromancer", worldTile, pSpawnSound: true, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
				if (actor != null)
				{
					raidParty.Add(actor);
				}
			}
			int num = UnityEngine.Random.Range(8, 16);
			for (int i = 0; i < num; i++)
			{
				Actor actor2 = World.world.units.spawnNewUnit("skeleton", worldTile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
				if (actor2 != null)
				{
					raidParty.Add(actor2);
					March(actor2, city);
				}
			}
			if (raidParty.Units.Count == 0)
			{
				return false;
			}
			raidParty.Sent = raidParty.Units.Count;
			Raids.Insert(0, raidParty);
			toast("The graves outside " + city.name + " open. The dead march!");
			return true;
		}

		private static int Afflict(City city, string trait, float share, Func<Actor, bool> can)
		{
			int num = 0;
			if (city.units == null)
			{
				return 0;
			}
			foreach (Actor item in new List<Actor>(city.units))
			{
				if (item != null && item.isAlive() && !(UnityEngine.Random.value > share) && (can == null || can(item)) && item.addTrait(trait))
				{
					num++;
				}
			}
			if (num == 0)
			{
				foreach (Actor unit in city.units)
				{
					if (unit != null && unit.isAlive() && (can == null || can(unit)) && unit.addTrait(trait))
					{
						num = 1;
						break;
					}
				}
			}
			return num;
		}

		private static bool Rebel(City city, Action<string> toast)
		{
			Kingdom kingdom = city.kingdom;
			if (World.world.cities.isLocked())
			{
				toast("The time isn't right for a rebellion. Try again.");
				return false;
			}
			if (city.isCapitalCity())
			{
				toast(city.name + " is their capital: it won't rise against its own king.");
				return false;
			}
			if (kingdom == null || CityCount(kingdom) < 2)
			{
				toast(NameOf(kingdom) + " has only one town: there is no one to rebel against.");
				return false;
			}
			if (!city.hasLeader())
			{
				toast(city.name + " has no leader to rally a rebellion.");
				return false;
			}
			DropsLibrary.action_inspiration(city.getTile(), "inspiration");
			bool flag = city.kingdom != kingdom;
			toast(flag ? (city.name + " rises up and breaks away from " + NameOf(kingdom) + "!") : ("Your agents stir up " + city.name + ". Its leader begins to listen..."));
			return true;
		}

		private static bool Assassinate(Kingdom k, Action<string> toast)
		{
			Actor actor = k?.king;
			if (actor == null || !actor.isAlive())
			{
				toast(NameOf(k) + " has no king to kill.");
				return false;
			}
			string text = WorldBoxMod.Describe(actor);
			if (UnityEngine.Random.value < 0.5f)
			{
				((BaseSimObject)actor).getHitFullHealth(AttackType.Poison);
				toast("Your assassin strikes: " + text + " is dead!");
			}
			else
			{
				((BaseSimObject)actor).addStatusEffect("poisoned", 0f, pColorEffect: true);
				toast("Your assassin's poison is found in time. " + text + " survives, sick and furious.");
			}
			return true;
		}

		public void MakePeace(Actor host, Kingdom target, Action<string> toast)
		{
			if (!Ruling(host, toast))
			{
				return;
			}
			Kingdom kingdom = host.kingdom;
			if (!AtWar(kingdom, target))
			{
				toast("You are not at war with " + NameOf(target) + ".");
				return;
			}
			try
			{
				int num = 0;
				War war;
				while ((war = World.world.wars.getWar(kingdom, target, pOnlyMain: false)) != null && num < 8)
				{
					bool flag = war.isMainAttacker(kingdom) || war.isMainDefender(kingdom);
					bool flag2 = war.isMainAttacker(target) || war.isMainDefender(target);
					if (war.isTotalWar())
					{
						war.leaveWar(kingdom);
					}
					else if (flag && flag2)
					{
						World.world.wars.endWar(war, WarWinner.Peace);
					}
					else
					{
						war.leaveWar(flag2 ? kingdom : target);
					}
					num++;
				}
				if (Active != null && Active.Target == target)
				{
					Recall(null);
				}
				try
				{
					World.world.diplomacy.getRelation(kingdom, target).data.timestamp_last_war_ended = World.world.getCurWorldTime();
				}
				catch
				{
				}
				toast("Peace with " + NameOf(target) + ". The envoys sign a truce.");
				Log.Info("peace: " + NameOf(kingdom) + " <-> " + NameOf(target));
			}
			catch (Exception ex)
			{
				Log.Error("making peace failed: " + ex);
				toast("The envoys could not agree.");
			}
		}

		public void ProposeAlliance(Actor host, Kingdom target, Action<string> toast)
		{
			if (!Ruling(host, toast))
			{
				return;
			}
			Kingdom kingdom = host.kingdom;
			if (AtWar(kingdom, target))
			{
				toast("Make peace with " + NameOf(target) + " first.");
				return;
			}
			if (Allied(kingdom, target))
			{
				toast("You are already allies.");
				return;
			}
			try
			{
				int num = Court.AllianceShift(kingdom);
				if (!((num == 0) ? target.isOpinionTowardsKingdomGood(kingdom) : (World.world.diplomacy.getOpinion(target, kingdom).total >= num)))
				{
					toast(NameOf(target) + " doesn't trust you enough. Send gifts first.");
				}
				else
				{
					toast(Join(kingdom, target) ? (NameOf(kingdom) + " and " + NameOf(target) + " are allies!") : (NameOf(target) + " turns your offer down."));
				}
			}
			catch (Exception ex)
			{
				Log.Error("alliance failed: " + ex);
				toast(NameOf(target) + " turns your offer down.");
			}
		}

		public void AskForTown(Actor host, Kingdom ally, City town, Action<string> toast)
		{
			if (!Ruling(host, toast))
			{
				return;
			}
			Kingdom kingdom = host.kingdom;
			if (town == null || !town.isAlive() || town.kingdom != ally)
			{
				toast("That town isn't theirs to give.");
				return;
			}
			if (!Allied(kingdom, ally))
			{
				toast(NameOf(ally) + " is no ally of yours.");
				return;
			}
			try
			{
				int num = Court.AllianceShift(kingdom);
				if (!((num == 0) ? ally.isOpinionTowardsKingdomGood(kingdom) : (World.world.diplomacy.getOpinion(ally, kingdom).total >= num)))
				{
					toast(NameOf(ally) + " doesn't trust you enough to give up " + town.name + ". Send gifts first.");
					return;
				}
				town.joinAnotherKingdom(kingdom, pCaptured: true);
				bool flag = town.kingdom == kingdom;
				Log.Info("royal: asked " + NameOf(ally) + " for " + town.name + ": " + (flag ? "handed over" : "kept"));
				toast(flag ? (NameOf(ally) + " hands " + town.name + " to " + NameOf(kingdom) + ".") : (NameOf(ally) + " keeps " + town.name + "."));
			}
			catch (Exception ex)
			{
				Log.Error("asking for a town failed: " + ex);
				toast(NameOf(ally) + " keeps " + town.name + ".");
			}
		}

		internal static bool Join(Kingdom home, Kingdom target)
		{
			Alliance alliance = home.getAlliance() ?? target.getAlliance();
			if (alliance == null)
			{
				World.world.alliances.newAlliance(home, target);
				return Allied(home, target);
			}
			Kingdom[] array = new Kingdom[2] { home, target };
			foreach (Kingdom kingdom in array)
			{
				Alliance alliance2 = kingdom.getAlliance();
				if (alliance2 != null && alliance2 != alliance)
				{
					if (!alliance.canJoin(kingdom))
					{
						return false;
					}
					alliance2.leave(kingdom);
				}
			}
			alliance.join(home);
			alliance.join(target);
			return Allied(home, target);
		}

		public void SendGift(Actor host, Kingdom target, Action<string> toast)
		{
			if (!Ruling(host, toast))
			{
				return;
			}
			Kingdom kingdom = host.kingdom;
			if (CooldownLeft("gift") > 0f)
			{
				toast("Your treasurers are still counting the last gift.");
				return;
			}
			if (AtWar(kingdom, target))
			{
				toast("They won't accept gifts while you're at war.");
				return;
			}
			try
			{
				City capital = kingdom.capital;
				City capital2 = target.capital;
				int num = ((capital != null) ? Mathf.Min(40, capital.getResourcesAmount("gold")) : 0);
				int num2 = 0;
				int num3 = 0;
				if (num > 0 && capital2 != null && capital2.getRandomStockpile() != null)
				{
					Actor actor = Court.Holder(kingdom, Seat.Steward);
					num3 = ((actor != null) ? Mathf.RoundToInt((float)num * Court.GiftSkim(kingdom)) : 0);
					capital.takeResource("gold", num);
					capital2.addResourcesToRandomStockpile("gold", num - num3);
					if (num3 > 0)
					{
						actor.addMoney(num3);
					}
					num2 = num - num3;
				}
				World.world.diplomacy.getRelation(kingdom, target).data.timestamp_last_war_ended = World.world.getCurWorldTime();
				Cool("gift", 20f);
				toast((num2 > 0 || num3 > 0) ? ("You send " + num2 + " gold to " + NameOf(target) + ". They will remember it." + ((num3 > 0) ? (" Your Steward kept " + num3 + ".") : "")) : ("You send fine gifts to " + NameOf(target) + ". They will remember it."));
			}
			catch (Exception ex)
			{
				Log.Error("gift failed: " + ex);
				toast("The gift never arrived.");
			}
		}

		public void DemandTribute(Actor host, Kingdom target, Action<string> toast)
		{
			if (!Ruling(host, toast))
			{
				return;
			}
			Kingdom kingdom = host.kingdom;
			if (CooldownLeft("tribute") > 0f)
			{
				toast("You demanded tribute only a moment ago.");
				return;
			}
			Cool("tribute", 30f);
			try
			{
				if ((float)target.power >= (float)kingdom.power * Court.TributeReach(kingdom))
				{
					toast(NameOf(target) + " laughs at your demand.");
					return;
				}
				City capital = target.capital;
				City capital2 = kingdom.capital;
				int a = Mathf.Max(1, Mathf.RoundToInt(60f * Court.TributeScale(kingdom)));
				int num = ((capital != null) ? Mathf.Min(a, capital.getResourcesAmount("gold")) : 0);
				if (num <= 0 || capital2 == null)
				{
					toast(NameOf(target) + " bows, but its coffers are empty.");
					return;
				}
				if (capital2.getRandomStockpile() == null)
				{
					toast(NameOf(target) + " bows, but " + capital2.name + " has no stockpile to keep the gold.");
					return;
				}
				capital.takeResource("gold", num);
				capital2.addResourcesToRandomStockpile("gold", num);
				toast(NameOf(target) + " bows to your might and pays " + num + " gold.");
			}
			catch (Exception ex)
			{
				Log.Error("tribute failed: " + ex);
				toast("The tribute never came.");
			}
		}

		public void Levy(Actor host, Action<string> toast)
		{
			if (!Ruling(host, toast))
			{
				return;
			}
			Kingdom kingdom = host.kingdom;
			int num = 0;
			int num2 = 0;
			float num3 = Court.LevyShare(kingdom);
			try
			{
				foreach (City item in new List<City>(kingdom.getCities()))
				{
					if (item == null || !item.isAlive() || item.units == null)
					{
						continue;
					}
					foreach (Actor item2 in new List<Actor>(item.units))
					{
						if (item2 != null && item2.isAlive() && item2 != host && item2.is_profession_citizen && item2.isAdult() && item.checkCanMakeWarrior(item2))
						{
							num2++;
							if (!(num3 < 1f) || num < Mathf.CeilToInt((float)num2 * num3))
							{
								item.makeWarrior(item2);
								num++;
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("levy failed: " + ex.Message);
			}
			toast((num > 0) ? (num + " men and women take up arms for " + NameOf(kingdom) + "!") : "Your cities can't spare anyone: the barracks are full or the granaries too empty.");
		}

		public void Rally(Actor host, Action<string> toast)
		{
			if (!Ruling(host, toast))
			{
				return;
			}
			int num = 0;
			RegisterTask();
			WorldTile current_tile = host.current_tile;
			foreach (Actor item in WarriorsOf(host.kingdom, host))
			{
				if (item.ai != null)
				{
					item.setTask("fp_royal_march");
					item.beh_tile_target = Around(current_tile);
					num++;
				}
			}
			if (Active != null)
			{
				Active = null;
			}
			toast((num > 0) ? (num + " warriors answer your call and march to your side.") : "You have no warriors to rally.");
		}

		public void Defend(Actor host, Action<string> toast)
		{
			if (!Ruling(host, toast))
			{
				return;
			}
			City capital = host.kingdom.capital;
			if (capital == null)
			{
				toast("You have no capital to defend.");
				return;
			}
			int num = 0;
			RegisterTask();
			foreach (Actor item in WarriorsOf(host.kingdom, host))
			{
				if (item.ai != null)
				{
					WorldTile worldTile = RandomTileOf(capital);
					if (worldTile != null)
					{
						item.setTask("fp_royal_march");
						item.beh_tile_target = worldTile;
						num++;
					}
				}
			}
			if (Active != null)
			{
				Active = null;
			}
			toast((num > 0) ? (num + " warriors fall back to guard " + capital.name + ".") : "You have no warriors to call back.");
		}

		private static List<Actor> WarriorsOf(Kingdom k, Actor except)
		{
			List<Actor> list = new List<Actor>();
			try
			{
				foreach (Actor unit in k.getUnits())
				{
					if (unit != null && unit != except && unit.isAlive() && unit.isWarrior())
					{
						list.Add(unit);
					}
				}
			}
			catch
			{
			}
			return list;
		}

		private void UpdateRaids(float dt, Action<string> toast)
		{
			if (Raids.Count == 0)
			{
				return;
			}
			_raidTimer -= dt;
			if (_raidTimer > 0f)
			{
				return;
			}
			_raidTimer = 1.5f;
			for (int num = Raids.Count - 1; num >= 0; num--)
			{
				RaidParty raidParty = Raids[num];
				raidParty.Prune();
				bool flag = false;
				bool flag2;
				try
				{
					flag2 = raidParty.Target == null || !raidParty.Target.isAlive();
				}
				catch
				{
					flag2 = true;
				}
				try
				{
					if (!flag2 && raidParty.By != null)
					{
						flag = raidParty.Target.kingdom == raidParty.By || Allied(raidParty.By, raidParty.Target.kingdom);
					}
				}
				catch
				{
				}
				if (flag)
				{
					toast(raidParty.TargetName + " is no longer your enemy's. The " + raidParty.What.ToLowerInvariant() + " are left to themselves.");
					Raids.RemoveAt(num);
				}
				else if (raidParty.Units.Count == 0)
				{
					toast(raidParty.What + " sent against " + raidParty.TargetName + " are all dead.");
					Raids.RemoveAt(num);
				}
				else if (flag2)
				{
					toast(raidParty.TargetName + " has fallen to the " + raidParty.What.ToLowerInvariant() + "!");
					Raids.RemoveAt(num);
				}
				else
				{
					int num2 = 0;
					foreach (Actor unit in raidParty.Units)
					{
						try
						{
							if (unit.isFighting())
							{
								num2++;
							}
							else if (unit.ai != null && (((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)unit.ai).task == null || !(((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)unit.ai).task.id == "fp_royal_march") || unit.beh_tile_target == null))
							{
								March(unit, raidParty.Target);
							}
						}
						catch
						{
						}
					}
					raidParty.Status = raidParty.What + ": " + raidParty.Units.Count + "/" + raidParty.Sent + " on " + raidParty.TargetName + ((num2 > 0) ? (", " + num2 + " fighting") : "");
				}
			}
		}

		private static void March(Actor a, City city)
		{
			if (a != null && a.ai != null)
			{
				WorldTile worldTile = RandomTileOf(city);
				if (worldTile != null)
				{
					a.setTask("fp_royal_march");
					a.beh_tile_target = worldTile;
				}
			}
		}

		private static WorldTile SafeTile(City c)
		{
			try
			{
				return c.getTile();
			}
			catch
			{
				return null;
			}
		}

		private static WorldTile Somewhere(City c)
		{
			try
			{
				if (c.buildings != null && c.buildings.Count > 0)
				{
					Building building = c.buildings[UnityEngine.Random.Range(0, c.buildings.Count)];
					if (building != null && building.current_tile != null)
					{
						return building.current_tile;
					}
				}
			}
			catch
			{
			}
			return SafeTile(c);
		}

		private static WorldTile OutsideTile(City c)
		{
			WorldTile worldTile = SafeTile(c);
			if (worldTile == null)
			{
				return null;
			}
			try
			{
				List<TileZone> list = new List<TileZone>(c.neighbour_zones);
				for (int i = 0; i < 40; i++)
				{
					if (list.Count <= 0)
					{
						break;
					}
					TileZone tileZone = list[UnityEngine.Random.Range(0, list.Count)];
					if (tileZone.city == null && tileZone.tiles != null && tileZone.tiles.Length != 0)
					{
						WorldTile worldTile2 = tileZone.tiles[UnityEngine.Random.Range(0, tileZone.tiles.Length)];
						if (worldTile2.Type != null && worldTile2.Type.ground && !worldTile2.Type.block && !worldTile2.Type.lava && !worldTile2.is_liquid && worldTile2.isSameIsland(worldTile))
						{
							return worldTile2;
						}
					}
				}
			}
			catch
			{
			}
			return worldTile;
		}

		private static WorldTile Around(WorldTile t)
		{
			if (t == null)
			{
				return null;
			}
			try
			{
				for (int i = 0; i < 12; i++)
				{
					WorldTile tile = World.world.GetTile(t.x + UnityEngine.Random.Range(-3, 4), t.y + UnityEngine.Random.Range(-3, 4));
					if (tile != null && tile.Type != null && tile.Type.ground && !tile.Type.block && !tile.is_liquid)
					{
						return tile;
					}
				}
			}
			catch
			{
			}
			return t;
		}

		public static List<City> CitiesOf(Kingdom k, Vector2 from)
		{
			List<City> list = new List<City>();
			try
			{
				foreach (City city in k.getCities())
				{
					if (city != null && city.isAlive())
					{
						list.Add(city);
					}
				}
				list.Sort((City a, City b) => (CityPosition(a) - from).sqrMagnitude.CompareTo((CityPosition(b) - from).sqrMagnitude));
			}
			catch
			{
			}
			return list;
		}

		public static string Label(Storm s)
		{
			switch (s)
			{
			case Storm.Meteors:
				return "Meteor storm";
			case Storm.Lightning:
				return "Lightning storm";
			case Storm.Wrath:
				return "Wrath of heaven";
			case Storm.Bombs:
				return "Bomb storm";
			case Storm.Napalm:
				return "Napalm strike";
			default:
				return "Crab bombs";
			}
		}

		public static string Describe(Storm s)
		{
			switch (s)
			{
			case Storm.Meteors:
				return "A dozen meteors fall on the village, one after another.";
			case Storm.Lightning:
				return "Thunderclouds gather over it and lightning strikes again and again.";
			case Storm.Wrath:
				return "Meteors and lightning together. Little will be left standing.";
			case Storm.Bombs:
				return "Bombs rain down on its houses and streets.";
			case Storm.Napalm:
				return "Three napalm bombs laid in a line across the town.";
			default:
				return "Crabs with bombs parachute in.";
			}
		}

		internal static float CooldownOf(Storm s)
		{
			switch (s)
			{
			case Storm.Meteors:
			case Storm.Lightning:
				return 45f;
			case Storm.Wrath:
				return 90f;
			case Storm.Bombs:
				return 30f;
			default:
				return 20f;
			}
		}

		public static bool IsObelisk(Building b)
		{
			try
			{
				return b != null && b.isAlive() && b.asset != null && b.asset.id == "monolith";
			}
			catch
			{
				return false;
			}
		}

		public void TouchObelisk(Actor host, Action<string> toast)
		{
			if (ObeliskOpen)
			{
				ObeliskOpen = false;
				return;
			}
			if (!IsKing(host))
			{
				toast("The obelisk hums under your hand, but stays silent. Only a king can wake it.");
				return;
			}
			Open = false;
			ObeliskOpen = true;
		}

		public void CallStorm(Actor host, City city, Storm kind, Action<string> toast)
		{
			string power = "storm_" + kind;
			if (!IsKing(host))
			{
				toast("Only a king can wake the obelisk.");
				return;
			}
			if (CooldownLeft(power) > 0f)
			{
				toast("The obelisk is still gathering its strength (" + Mathf.CeilToInt(CooldownLeft(power)) + " s).");
				return;
			}
			if (city == null || !city.isAlive())
			{
				toast("That village is no more.");
				return;
			}
			if (host.kingdom == null || city.kingdom == host.kingdom)
			{
				toast("The obelisk will not strike your own people.");
				return;
			}
			if (SafeTile(city) == null)
			{
				toast("The obelisk can't find " + city.name + ".");
				return;
			}
			int num = CostOf(kind);
			if (!CanAfford(host.kingdom, num, Label(kind), toast))
			{
				return;
			}
			Kingdom kingdom = city.kingdom;
			if (Peace.HoldOff(host.kingdom, kingdom, Label(kind), toast))
			{
				return;
			}
			_by = host.kingdom;
			if (StartStorm(city, kind, null))
			{
				Spend(host.kingdom, num);
				Cool(power, CooldownOf(kind));
				Peace.Struck(host.kingdom, kingdom, city, num, toast, (WorldBoxMod.Instance != null) ? new Action(WorldBoxMod.Instance.PlayHorn) : null);
				switch (kind)
				{
				case Storm.Meteors:
					toast("The obelisk flares. The sky over " + city.name + " splits with falling fire!");
					break;
				case Storm.Lightning:
					toast("The obelisk crackles. Black clouds boil up over " + city.name + "!");
					break;
				default:
					toast("The obelisk screams. Heaven's wrath falls on " + city.name + "!");
					break;
				}
				Log.Info($"obelisk storm: {kind} -> {city.name} ({NameOf(city.kingdom)}) for {num} kills, {KillsToSpend(host.kingdom)} left");
			}
		}

		private bool StartStorm(City city, Storm kind, Action<string> toast)
		{
			StormRun stormRun = new StormRun
			{
				Target = city,
				TargetName = city.name,
				Kind = kind,
				Caller = null,
				By = _by
			};
			float unscaledTime = Time.unscaledTime;
			switch (kind)
			{
			case Storm.Bombs:
			case Storm.Napalm:
			case Storm.Crabs:
			{
				object dropId;
				switch (kind)
				{
				default:
					dropId = "crab_bomb";
					break;
				case Storm.Napalm:
					dropId = "napalm_bomb";
					break;
				case Storm.Bombs:
					dropId = "bomb";
					break;
				}
				stormRun.DropId = (string)dropId;
				if (AssetManager.drops == null || AssetManager.drops.get(stormRun.DropId) == null)
				{
					toast?.Invoke("No " + Label(kind).ToLowerInvariant() + " can be had in this world.");
					return false;
				}
				stormRun.Drops = (stormRun.DropsLeft = ((kind == Storm.Bombs) ? UnityEngine.Random.Range(16, 23) : 3));
				float gapMin;
				switch (kind)
				{
				default:
					gapMin = 0.6f;
					break;
				case Storm.Napalm:
					gapMin = 0.9f;
					break;
				case Storm.Bombs:
					gapMin = 0.2f;
					break;
				}
				stormRun.GapMin = gapMin;
				float gapMax;
				switch (kind)
				{
				default:
					gapMax = 1f;
					break;
				case Storm.Napalm:
					gapMax = 1.1f;
					break;
				case Storm.Bombs:
					gapMax = 0.55f;
					break;
				}
				stormRun.GapMax = gapMax;
				stormRun.NextDrop = unscaledTime + 0.8f;
				if (kind == Storm.Napalm)
				{
					Vector2 vector = CityPosition(city);
					float f = UnityEngine.Random.value * MathF.PI;
					Vector2 vector2 = new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * 9f;
					stormRun.LineFrom = vector - vector2;
					stormRun.LineTo = vector + vector2;
				}
				Storms.Insert(0, stormRun);
				switch (kind)
				{
				case Storm.Bombs:
					toast?.Invoke("Bombers darken the sky over " + city.name + "!");
					break;
				case Storm.Napalm:
					toast?.Invoke("A line of fire is laid across " + city.name + "!");
					break;
				default:
					toast?.Invoke("Crabs with bombs strapped to them parachute towards " + city.name + "!");
					break;
				}
				return true;
			}
			default:
				stormRun.Meteors = (stormRun.MeteorsLeft = ((kind == Storm.Wrath) ? UnityEngine.Random.Range(8, 11) : UnityEngine.Random.Range(10, 15)));
				stormRun.NextMeteor = unscaledTime + 1f;
				break;
			case Storm.Lightning:
				break;
			}
			if (kind != Storm.Meteors)
			{
				stormRun.Bolts = (stormRun.BoltsLeft = ((kind == Storm.Wrath) ? UnityEngine.Random.Range(16, 22) : UnityEngine.Random.Range(20, 29)));
				stormRun.NextBolt = unscaledTime + 0.6f;
				try
				{
					if (AssetManager.clouds != null && AssetManager.clouds.get("cloud_lightning") != null)
					{
						for (int i = 0; i < 2; i++)
						{
							EffectsLibrary.spawn("fx_cloud", Somewhere(city) ?? SafeTile(city), "cloud_lightning");
						}
					}
				}
				catch (Exception ex)
				{
					Log.Error("storm cloud failed: " + ex.Message);
				}
			}
			Storms.Insert(0, stormRun);
			switch (kind)
			{
			case Storm.Meteors:
				toast?.Invoke("The sky over " + city.name + " splits with falling fire!");
				break;
			case Storm.Lightning:
				toast?.Invoke("Black clouds boil up over " + city.name + "!");
				break;
			}
			return true;
		}

		private void UpdateStorms(Action<string> toast)
		{
			if (Storms.Count == 0)
			{
				return;
			}
			float unscaledTime = Time.unscaledTime;
			bool flag = false;
			try
			{
				flag = Config.paused || World.world.isPaused();
			}
			catch
			{
			}
			for (int num = Storms.Count - 1; num >= 0; num--)
			{
				StormRun stormRun = Storms[num];
				bool flag2 = false;
				bool flag3;
				try
				{
					flag3 = stormRun.Target == null || !stormRun.Target.isAlive();
				}
				catch
				{
					flag3 = true;
				}
				if (flag3)
				{
					toast(stormRun.TargetName + " is gone. The storm over it clears.");
					Storms.RemoveAt(num);
				}
				else
				{
					try
					{
						flag2 = stormRun.By != null && (stormRun.Target.kingdom == stormRun.By || Allied(stormRun.By, stormRun.Target.kingdom));
					}
					catch
					{
					}
					if (flag2)
					{
						toast(stormRun.TargetName + " is no longer your enemy's. The storm over it clears.");
						Storms.RemoveAt(num);
					}
					else if (flag)
					{
						stormRun.NextMeteor += Time.unscaledDeltaTime;
						stormRun.NextBolt += Time.unscaledDeltaTime;
						stormRun.NextDrop += Time.unscaledDeltaTime;
					}
					else
					{
						try
						{
							Actor pActor = ((stormRun.Caller != null && stormRun.Caller.isAlive()) ? stormRun.Caller : null);
							if (stormRun.MeteorsLeft > 0 && unscaledTime >= stormRun.NextMeteor)
							{
								WorldTile worldTile = StrikeTile(stormRun.Target);
								if (worldTile != null)
								{
									Meteorite.spawnMeteorite(worldTile, pActor);
								}
								stormRun.MeteorsLeft--;
								stormRun.NextMeteor = unscaledTime + UnityEngine.Random.Range(0.5f, 1.3f);
							}
							if (stormRun.BoltsLeft > 0 && unscaledTime >= stormRun.NextBolt)
							{
								WorldTile worldTile2 = StrikeTile(stormRun.Target);
								if (worldTile2 != null)
								{
									if (UnityEngine.Random.value < 0.6f)
									{
										MapBox.spawnLightningBig(worldTile2, 0.25f, pActor);
									}
									else
									{
										MapBox.spawnLightningMedium(worldTile2, 0.25f, pActor);
									}
								}
								stormRun.BoltsLeft--;
								stormRun.NextBolt = unscaledTime + UnityEngine.Random.Range(0.25f, 0.8f);
							}
							if (stormRun.DropsLeft > 0 && unscaledTime >= stormRun.NextDrop)
							{
								WorldTile worldTile3;
								if (stormRun.Kind == Storm.Napalm)
								{
									Vector2 vector = Vector2.Lerp(stormRun.LineFrom, stormRun.LineTo, (stormRun.Drops > 1) ? ((float)(stormRun.Drops - stormRun.DropsLeft) / (float)(stormRun.Drops - 1)) : 0.5f);
									worldTile3 = World.world.GetTile(Mathf.FloorToInt(vector.x), Mathf.FloorToInt(vector.y)) ?? StrikeTile(stormRun.Target);
								}
								else
								{
									worldTile3 = StrikeTile(stormRun.Target);
								}
								if (worldTile3 != null)
								{
									World.world.drop_manager.spawn(worldTile3, stormRun.DropId, -1f, -1f, -1L);
								}
								stormRun.DropsLeft--;
								stormRun.NextDrop = unscaledTime + UnityEngine.Random.Range(stormRun.GapMin, stormRun.GapMax);
							}
						}
						catch (Exception ex)
						{
							Log.Error("storm strike failed: " + ex.Message);
							stormRun.MeteorsLeft = (stormRun.BoltsLeft = (stormRun.DropsLeft = 0));
						}
						if (stormRun.MeteorsLeft <= 0 && stormRun.BoltsLeft <= 0 && stormRun.DropsLeft <= 0)
						{
							toast((stormRun.DropId != null) ? ("The last of the " + Label(stormRun.Kind).ToLowerInvariant() + " falls on " + stormRun.TargetName + ".") : ("The storm over " + stormRun.TargetName + " passes."));
							Storms.RemoveAt(num);
						}
						else
						{
							string text = ((stormRun.Kind == Storm.Wrath) ? "Wrath" : ((stormRun.Kind == Storm.Meteors) ? "Meteors" : ((stormRun.Kind == Storm.Lightning) ? "Lightning" : ((stormRun.Kind == Storm.Bombs) ? "Bombs" : ((stormRun.Kind == Storm.Napalm) ? "Napalm" : "Crab bombs")))));
							int num2 = stormRun.Meteors - stormRun.MeteorsLeft + (stormRun.Bolts - stormRun.BoltsLeft) + (stormRun.Drops - stormRun.DropsLeft);
							int num3 = stormRun.Meteors + stormRun.Bolts + stormRun.Drops;
							stormRun.Status = text + " over " + stormRun.TargetName + ": " + num2 + "/" + num3;
						}
					}
				}
			}
		}

		private static WorldTile StrikeTile(City c)
		{
			if (UnityEngine.Random.value < 0.65f)
			{
				return Somewhere(c);
			}
			return RandomTileOf(c);
		}

		public static List<City> RivalVillages(Kingdom home, Vector2 from)
		{
			List<City> list = new List<City>();
			try
			{
				foreach (Kingdom item in Rivals(home))
				{
					foreach (City city in item.getCities())
					{
						if (city != null && city.isAlive())
						{
							list.Add(city);
						}
					}
				}
				list.Sort((City a, City b) => (CityPosition(a) - from).sqrMagnitude.CompareTo((CityPosition(b) - from).sqrMagnitude));
			}
			catch (Exception ex)
			{
				Log.Error("listing villages failed: " + ex.Message);
			}
			return list;
		}

		public static float DistanceTo(City c, Vector2 from)
		{
			return (CityPosition(c) - from).magnitude;
		}

		public static long KillsEarned(Kingdom k)
		{
			try
			{
				return k?.data.total_kills ?? 0;
			}
			catch
			{
				return 0L;
			}
		}

		public static long KillsSpent(Kingdom k)
		{
			try
			{
				if (k == null)
				{
					return 0L;
				}
				k.data.get("fp_kills_spent", out var pResult, 0L);
				return pResult;
			}
			catch
			{
				return 0L;
			}
		}

		public static long KillsToSpend(Kingdom k)
		{
			return Math.Max(0L, KillsEarned(k) - KillsSpent(k));
		}

		private static void Spend(Kingdom k, int kills)
		{
			try
			{
				k.data.set("fp_kills_spent", KillsSpent(k) + kills);
			}
			catch (Exception ex)
			{
				Log.Error("war chest: " + ex.Message);
			}
		}

		internal static int CostOf(Deed d)
		{
			switch (d)
			{
			case Deed.Bandits:
				return 10;
			case Deed.Undead:
				return 20;
			case Deed.Demons:
				return 25;
			case Deed.Zombies:
				return 30;
			case Deed.Plague:
				return 30;
			case Deed.Madness:
				return 20;
			case Deed.Rebellion:
				return 25;
			case Deed.Assassin:
				return 40;
			case Deed.Meteor:
				return 15;
			case Deed.Tornado:
				return 15;
			case Deed.Earthquake:
				return 25;
			case Deed.FireCloud:
			case Deed.AcidCloud:
				return 20;
			case Deed.LavaCloud:
				return 25;
			case Deed.MeteorStorm:
				return CostOf(Storm.Meteors);
			case Deed.LightningStorm:
				return CostOf(Storm.Lightning);
			case Deed.BombStorm:
				return CostOf(Storm.Bombs);
			case Deed.Napalm:
				return CostOf(Storm.Napalm);
			case Deed.CrabBombs:
				return CostOf(Storm.Crabs);
			case Deed.Nuke:
				return 150;
			default:
				return 400;
			}
		}

		internal static int PriceOf(Kingdom k, Deed d)
		{
			return Court.Price(k, d, CostOf(d));
		}

		internal static int CostOf(Storm s)
		{
			switch (s)
			{
			case Storm.Meteors:
				return 60;
			case Storm.Lightning:
				return 50;
			case Storm.Wrath:
				return 100;
			case Storm.Bombs:
				return 40;
			case Storm.Napalm:
				return 35;
			default:
				return 30;
			}
		}

		private static bool CanAfford(Kingdom k, int cost, string what, Action<string> toast)
		{
			long num = KillsToSpend(k);
			if (num >= cost)
			{
				return true;
			}
			toast(what + " costs " + cost + " kills. " + NameOf(k) + " has " + num + " to spend: win battles to earn more.");
			return false;
		}

		internal static Vector2 Position(City c)
		{
			return CityPosition(c);
		}

		internal static float BlastRadius(Deed d)
		{
			switch (d)
			{
			default:
				return 0f;
			case Deed.CzarBomba:
				return 70f;
			case Deed.Nuke:
				return 30f;
			}
		}

		private bool HoldOffBlast(Actor host, City city, Deed deed, WorldTile at, Action<string> toast)
		{
			float num = BlastRadius(deed);
			if (num <= 0f || at == null)
			{
				return false;
			}
			float unscaledTime = Time.unscaledTime;
			if (_armed == deed && _armedAt == city && unscaledTime < _armedUntil)
			{
				_armedUntil = 0f;
				return false;
			}
			Vector2 vector = new Vector2((float)at.x + 0.5f, (float)at.y + 0.5f);
			string text = null;
			try
			{
				foreach (City city2 in host.kingdom.getCities())
				{
					if (city2 != null && city2.isAlive() && (CityPosition(city2) - vector).magnitude < num + 6f)
					{
						text = "Your own town " + city2.name + " is inside the blast!";
						break;
					}
				}
				if (text == null && (host.current_position - vector).magnitude < num + 4f)
				{
					text = "You stand inside the blast!";
				}
			}
			catch
			{
			}
			if (text == null)
			{
				return false;
			}
			_armed = deed;
			_armedAt = city;
			_armedUntil = unscaledTime + 5f;
			toast(text + " Click " + Label(deed) + " again within 5 seconds to drop it anyway.");
			return true;
		}
	}
}
