using System;
using System.Collections.Generic;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson.Towns
{
	internal static class Raiders
	{
		public enum Phase
		{
			Warning,
			Fighting,
			Fleeing,
			Held,
			Lost
		}

		public enum Band
		{
			Bandits,
			Orcs,
			Dead,
			Demons
		}

		internal sealed class Wave
		{
			public string[] Units = new string[0];

			public string Chief;
		}

		internal sealed class Raid
		{
			public City Town;

			public long TownId;

			public string TownName;

			public string What;

			public Band Kind;

			public Kingdom Realm;

			public readonly List<Wave> Waves = new List<Wave>();

			public Phase State;

			public string Why;

			public double PhaseStart;

			public double Started;

			public int WavesSent;

			public int Sent;

			public int Dead;

			public int Fled;

			public int DeadBeforeLast = -1;

			public int LastWave;

			public float Warning = 60f;

			public float Between = 60f;

			public long ChiefId = -1L;

			public string ChiefName;

			public WorldTile Gather;

			public readonly List<Actor> Units = new List<Actor>();

			private readonly List<long> _ids = new List<long>();

			internal readonly HashSet<long> AllIds = new HashSet<long>();

			public float Order;

			public bool Over
			{
				get
				{
					if (State != Phase.Held)
					{
						return State == Phase.Lost;
					}
					return true;
				}
			}

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
				AllIds.Add(item);
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
						Dead++;
					}
				}
			}

			public void Gone(int i)
			{
				Units.RemoveAt(i);
				_ids.RemoveAt(i);
				Fled++;
			}

			public float Next(double now)
			{
				if (State == Phase.Warning)
				{
					return Mathf.Max(0f, Warning - (float)(now - PhaseStart));
				}
				if (State == Phase.Fighting && WavesSent < Waves.Count)
				{
					return Mathf.Max(0f, Between - (float)(now - PhaseStart));
				}
				return -1f;
			}

			public string Status(double now)
			{
				switch (State)
				{
				case Phase.Warning:
					return $"{What} coming for {TownName}: here in {Mathf.CeilToInt(Next(now))} s";
				case Phase.Fighting:
				{
					string text = ((WavesSent < Waves.Count) ? $", wave {WavesSent + 1} in {Mathf.CeilToInt(Next(now))} s" : "");
					return string.Format("Raid on {0}: wave {1} of {2}, {3} {4} left{5}", TownName, WavesSent, Waves.Count, Units.Count, (Units.Count == 1) ? "raider" : "raiders", text);
				}
				case Phase.Fleeing:
					return $"Raid on {TownName}: they're fleeing! {Units.Count} left";
				case Phase.Held:
					return TownName + " held";
				default:
					return "Raid on " + TownName + " lost" + ((Why != null) ? (": " + Why) : "");
				}
			}
		}

		public static Raid Current;

		private static object _world;

		public static Action<Raid> Horn;

		public static Action<Raid> Ended;

		public const string RaiderKey = "fp_raider";

		private static double Now => WorldPace.ActionNow;

		public static List<Wave> Plan(int strength, out Band band, out string what)
		{
			strength = Mathf.Max(0, strength);
			band = ((strength > 1) ? ((strength <= 3) ? Band.Orcs : ((strength <= 5) ? Band.Dead : ((strength % 2 == 0) ? Band.Demons : Band.Dead))) : Band.Bandits);
			if (band == Band.Orcs && !Has("orc"))
			{
				band = Band.Bandits;
			}
			if (band == Band.Demons && !Has("demon"))
			{
				band = Band.Dead;
			}
			if (band == Band.Dead && !Has("skeleton"))
			{
				band = Band.Bandits;
			}
			what = ((band == Band.Bandits) ? "Bandits" : ((band == Band.Orcs) ? "An orc warband" : ((band == Band.Dead) ? "The dead" : "Demons")));
			int num = Mathf.Clamp(3 + strength / 2, 3, 10);
			List<Wave> list = new List<Wave>();
			System.Random rng = new System.Random(strength * 7919 + 13);
			for (int i = 0; i < 3; i++)
			{
				List<string> list2 = new List<string>();
				for (int j = 0; j < num; j++)
				{
					list2.Add(Raider(band, rng));
				}
				list.Add(new Wave
				{
					Units = list2.ToArray(),
					Chief = ((i == 2) ? Chief(band) : null)
				});
			}
			return list;
		}

		private static bool Has(string id)
		{
			try
			{
				return AssetManager.actor_library.has(id);
			}
			catch
			{
				return false;
			}
		}

		private static string Raider(Band band, System.Random rng)
		{
			switch (band)
			{
			case Band.Orcs:
				return "orc";
			case Band.Dead:
				if (rng.Next(3) == 0)
				{
					string[] array = new string[5] { "zombie_human", "zombie_orc", "zombie_elf", "zombie_dwarf", "zombie" };
					string text = array[rng.Next(array.Length)];
					if (Has(text))
					{
						return text;
					}
				}
				return "skeleton";
			case Band.Demons:
				return "demon";
			default:
				return "bandit";
			}
		}

		private static string Chief(Band band)
		{
			switch (band)
			{
			case Band.Orcs:
				return "orc";
			case Band.Dead:
				if (!Has("necromancer"))
				{
					return "skeleton";
				}
				return "necromancer";
			case Band.Demons:
				return "demon";
			default:
				return "bandit";
			}
		}

		public static string ChiefWord(Band band)
		{
			switch (band)
			{
			default:
				return "the greater demon";
			case Band.Dead:
				return "the necromancer";
			case Band.Orcs:
				return "the orc warlord";
			case Band.Bandits:
				return "the bandit chief";
			}
		}

		public static Raid Start(City town, List<Wave> waves, Band kind, string what, float warning = 60f, float between = 60f)
		{
			if (town == null || waves == null || waves.Count == 0)
			{
				return null;
			}
			if (Current != null && !Current.Over)
			{
				return null;
			}
			WorldTile worldTile = GatherTile(town);
			if (worldTile == null)
			{
				return null;
			}
			Raid raid = new Raid
			{
				Town = town,
				TownId = town.data.id,
				TownName = town.name,
				What = what,
				Kind = kind,
				Realm = town.kingdom,
				Warning = warning,
				Between = between,
				Gather = worldTile,
				PhaseStart = Now,
				Started = Now
			};
			raid.Waves.AddRange(waves);
			Current = raid;
			_world = World.world?.map_stats;
			Log.Info($"raid: {what} coming for {town.name} ({waves.Count} waves of {waves[0].Units.Length}, gathering at {worldTile.x},{worldTile.y}) in {warning:F0} s");
			return raid;
		}

		public static void Update(float dt)
		{
			Raid current = Current;
			if (current == null)
			{
				return;
			}
			if (World.world?.map_stats != _world)
			{
				Current = null;
			}
			else
			{
				if (current.Over)
				{
					return;
				}
				double now = Now;
				bool flag;
				try
				{
					flag = current.Town == null || !current.Town.isAlive();
				}
				catch
				{
					flag = true;
				}
				if (flag)
				{
					End(current, Phase.Lost, current.TownName + " fell");
					return;
				}
				Kingdom kingdom = null;
				try
				{
					kingdom = current.Town.kingdom;
				}
				catch
				{
				}
				if (current.Realm != null && kingdom != current.Realm)
				{
					End(current, Phase.Lost, current.TownName + " was taken");
					return;
				}
				if (now - current.Started > 900.0)
				{
					End(current, Phase.Lost, "they never broke");
					return;
				}
				switch (current.State)
				{
				case Phase.Warning:
					if (now - current.PhaseStart >= (double)current.Warning)
					{
						current.State = Phase.Fighting;
						SendWave(current, now);
					}
					break;
				case Phase.Fighting:
					if (current.WavesSent < current.Waves.Count && now - current.PhaseStart >= (double)current.Between)
					{
						SendWave(current, now);
					}
					break;
				}
				current.Prune();
				if (current.State == Phase.Fighting && current.WavesSent >= current.Waves.Count)
				{
					if (current.Units.Count == 0)
					{
						End(current, Phase.Held, null);
						return;
					}
					bool flag2 = current.Dead - current.DeadBeforeLast >= (current.LastWave + 1) / 2 || ChiefDown(current);
					if (current.Dead * 2 >= current.Sent && flag2)
					{
						Flee(current);
					}
				}
				current.Order -= dt;
				if (current.Order > 0f)
				{
					return;
				}
				current.Order = 2f;
				if (current.State != Phase.Fleeing)
				{
					foreach (Actor unit in current.Units)
					{
						March(unit, current.Town);
					}
					return;
				}
				KeepFleeing(current, now);
			}
		}

		private static void SendWave(Raid r, double now)
		{
			Wave wave = r.Waves[r.WavesSent];
			r.WavesSent++;
			r.PhaseStart = now;
			WorldTile worldTile = r.Gather ?? GatherTile(r.Town);
			if (worldTile == null)
			{
				return;
			}
			try
			{
				EffectsLibrary.spawn("fx_spawn", worldTile);
			}
			catch
			{
			}
			int num = 0;
			string[] units = wave.Units;
			for (int i = 0; i < units.Length; i++)
			{
				Actor actor = Spawn(units[i], worldTile, r.Kind);
				if (actor != null)
				{
					r.Add(actor);
					March(actor, r.Town);
					num++;
				}
			}
			if (wave.Chief != null)
			{
				Actor actor2 = Spawn(wave.Chief, worldTile, r.Kind);
				if (actor2 != null)
				{
					MakeChief(actor2);
					r.Add(actor2);
					r.ChiefId = actor2.data.id;
					r.ChiefName = ChiefWord(r.Kind);
					March(actor2, r.Town);
					num++;
				}
			}
			r.Sent += num;
			if (r.WavesSent >= r.Waves.Count)
			{
				r.Prune();
				r.DeadBeforeLast = r.Dead;
				r.LastWave = num;
			}
			Log.Info($"raid: wave {r.WavesSent} of {r.Waves.Count} on {r.TownName}: {num} {r.What.ToLowerInvariant()}" + ((wave.Chief != null) ? (" with " + ChiefWord(r.Kind)) : ""));
			Horn?.Invoke(r);
		}

		private static bool ChiefDown(Raid r)
		{
			if (r.ChiefId < 0)
			{
				return false;
			}
			try
			{
				Actor actor = World.world.units.get(r.ChiefId);
				return actor == null || !actor.isAlive();
			}
			catch
			{
				return true;
			}
		}

		private static Actor Spawn(string id, WorldTile center, Band kind)
		{
			if (!Has(id))
			{
				return null;
			}
			for (int i = 0; i < 12; i++)
			{
				WorldTile tile = World.world.GetTile(center.x + UnityEngine.Random.Range(-3, 4), center.y + UnityEngine.Random.Range(-3, 4));
				if (!Dry(tile) || !tile.isSameIsland(center))
				{
					continue;
				}
				try
				{
					Actor actor = World.world.units.spawnNewUnit(id, tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
					if (actor == null)
					{
						return null;
					}
					if (kind == Band.Orcs)
					{
						Kingdom kingdom = null;
						try
						{
							kingdom = World.world.kingdoms_wild.get("bandit");
						}
						catch
						{
						}
						if (kingdom != null)
						{
							actor.setKingdom(kingdom);
						}
					}
					actor.data.set("fp_raider", pData: true);
					return actor;
				}
				catch (Exception ex)
				{
					Log.Error("raid: spawning " + id + ": " + ex.Message);
					return null;
				}
			}
			return null;
		}

		private static void MakeChief(Actor c)
		{
			string[] array = new string[3] { "giant", "strong", "tough" };
			foreach (string pTraitID in array)
			{
				try
				{
					c.addTrait(pTraitID, pRemoveOpposites: true);
				}
				catch
				{
				}
			}
			try
			{
				c.setStatsDirty();
			}
			catch
			{
			}
		}

		private static void March(Actor a, City town)
		{
			try
			{
				if (a != null && a.isAlive() && a.ai != null && !a.isFighting() && (((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task == null || !(((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id == "fp_royal_march") || a.beh_tile_target == null))
				{
					WorldTile worldTile = TileOf(town);
					if (worldTile != null)
					{
						Royal.RegisterTask();
						a.setTask("fp_royal_march");
						a.beh_tile_target = worldTile;
					}
				}
			}
			catch
			{
			}
		}

		private static void Flee(Raid r)
		{
			if (r.State != Phase.Fleeing)
			{
				r.State = Phase.Fleeing;
				r.PhaseStart = Now;
				Log.Info($"raid: the {r.What.ToLowerInvariant()} on {r.TownName} break and flee ({r.Dead} of {r.Sent} dead)");
				KeepFleeing(r, r.PhaseStart);
			}
		}

		private static void KeepFleeing(Raid r, double now)
		{
			Vector2 vector = Centre(r.Town);
			Vector2 vector2 = ((r.Gather != null) ? (new Vector2(r.Gather.x, r.Gather.y) - vector) : Vector2.right);
			if (vector2.sqrMagnitude < 1f)
			{
				vector2 = Vector2.right;
			}
			vector2.Normalize();
			for (int num = r.Units.Count - 1; num >= 0; num--)
			{
				Actor actor = r.Units[num];
				float num2;
				try
				{
					num2 = (actor.current_position - vector).magnitude;
				}
				catch
				{
					num2 = 999f;
				}
				if (num2 > 45f || now - r.PhaseStart > 40.0)
				{
					try
					{
						actor.dieSimpleNone();
					}
					catch
					{
					}
					r.Gone(num);
				}
				else
				{
					try
					{
						if (actor.ai != null)
						{
							WorldTile tile = World.world.GetTile((int)(vector.x + vector2.x * 55f), (int)(vector.y + vector2.y * 55f));
							if (tile != null)
							{
								Royal.RegisterTask();
								actor.setTask("fp_royal_march");
								actor.beh_tile_target = tile;
							}
						}
					}
					catch
					{
					}
				}
			}
			if (r.Units.Count == 0)
			{
				End(r, Phase.Held, null);
			}
		}

		private static void End(Raid r, Phase how, string why)
		{
			r.State = how;
			r.Why = why;
			Log.Info(string.Format("raid on {0}: {1}: {2} came, {3} dead, {4} fled", r.TownName, (how == Phase.Held) ? "held" : ("lost (" + why + ")"), r.Sent, r.Dead, r.Fled));
			try
			{
				Ended?.Invoke(r);
			}
			catch (Exception ex)
			{
				Log.Error("raid ended: " + ex.Message);
			}
		}

		public static void Release(Raid r)
		{
			if (r != null && Current == r)
			{
				Current = null;
			}
		}

		public static bool IsRaider(Actor a)
		{
			Raid current = Current;
			if (current == null || a == null)
			{
				return false;
			}
			try
			{
				return current.AllIds.Contains(a.data.id);
			}
			catch
			{
				return false;
			}
		}

		public static Vector2 Centre(City c)
		{
			try
			{
				WorldTile tile = c.getTile();
				if (tile != null)
				{
					return new Vector2((float)tile.x + 0.5f, (float)tile.y + 0.5f);
				}
			}
			catch
			{
			}
			return Vector2.zero;
		}

		private static bool Dry(WorldTile t)
		{
			if (t != null && t.main_type != null && !t.main_type.liquid && !t.main_type.block)
			{
				if (t.Type != null)
				{
					return !t.Type.lava;
				}
				return true;
			}
			return false;
		}

		private static WorldTile GatherTile(City town)
		{
			WorldTile worldTile = null;
			try
			{
				worldTile = town.getTile();
			}
			catch
			{
			}
			if (worldTile == null)
			{
				return null;
			}
			WorldTile worldTile2 = null;
			for (int i = 0; i < 200; i++)
			{
				double num = (double)UnityEngine.Random.value * Math.PI * 2.0;
				float num2 = 22f + UnityEngine.Random.value * 8f;
				WorldTile tile = World.world.GetTile(worldTile.x + (int)(Math.Cos(num) * (double)num2), worldTile.y + (int)(Math.Sin(num) * (double)num2));
				if (!Dry(tile))
				{
					continue;
				}
				try
				{
					if (!tile.isSameIsland(worldTile))
					{
						continue;
					}
				}
				catch
				{
					continue;
				}
				if (worldTile2 == null)
				{
					worldTile2 = tile;
				}
				if (tile.zone_city != null)
				{
					continue;
				}
				int num3 = 0;
				for (int j = -2; j <= 2; j++)
				{
					for (int k = -2; k <= 2; k++)
					{
						if (Dry(World.world.GetTile(tile.x + k, tile.y + j)))
						{
							num3++;
						}
					}
				}
				if (num3 >= 18)
				{
					return tile;
				}
			}
			return worldTile2 ?? worldTile;
		}

		private static WorldTile TileOf(City c)
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
				return c.getTile();
			}
			catch
			{
				return null;
			}
		}
	}
}
