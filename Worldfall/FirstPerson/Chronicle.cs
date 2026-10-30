using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace FirstPerson
{
	internal static class Chronicle
	{
		public const string KinKey = "fp_kin";

		public const string MemKey = "fp_mem";

		public const string StampKey = "fp_memt";

		public const string YouKey = "fp_you";

		public static int PerFrame = 48;

		public static double BudgetMs = 0.3;

		public const int MaxMemories = 12;

		public const int MaxChildren = 16;

		private static readonly HashSet<string> Notable = new HashSet<string>
		{
			"death_family_member", "death_lover", "death_child", "death_best_friend", "fallen_in_love", "just_had_child", "become_king", "become_leader", "lost_crown", "just_won_war",
			"just_lost_war", "just_started_war", "just_made_peace", "was_conquered", "kingdom_fell_apart", "just_rebelled", "just_lost_house", "just_found_house", "got_robbed", "got_caught",
			"just_injured", "just_killed", "wrote_book", "had_nightmare", "had_bad_dream", "just_made_friend", "just_became_adult", "conquered_city", "destroyed_city", "razed_city",
			"lost_city", "lost_capital", "razed_capital", "just_cursed", "just_felt_the_divine", "just_enchanted", "lost_fight", "just_got_out_of_egg", "just_finished_plot", "become_alpha"
		};

		private static readonly Dictionary<long, Record> Records = new Dictionary<long, Record>();

		private static readonly Dictionary<long, Kin> Known = new Dictionary<long, Kin>();

		private static object _world;

		private static int _cursor;

		private static readonly List<Actor> Batch = new List<Actor>(64);

		private static readonly StringBuilder Text = new StringBuilder(256);

		public static double SweepMs;

		public static double SweepMsMax;

		public static int Visited;

		public static int Writes;

		public static int Cycles;

		public static double TotalMs;

		public static int TotalFrames;

		private static int _frames;

		private static HappinessHistory[] _historyBuf = new HappinessHistory[32];

		public static int Year
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

		public static void Tick()
		{
			if (!Config.game_loaded || World.world == null || World.world.units == null)
			{
				return;
			}
			long timestamp = Stopwatch.GetTimestamp();
			CheckWorld();
			List<Actor> simpleList = World.world.units.getSimpleList();
			int count = simpleList.Count;
			if (count == 0)
			{
				return;
			}
			if (_cursor >= count)
			{
				_cursor = 0;
				Cycles++;
			}
			int num = Math.Min(count, _cursor + PerFrame);
			Batch.Clear();
			for (int i = _cursor; i < num; i++)
			{
				Batch.Add(simpleList[i]);
			}
			long num2 = (long)(BudgetMs * (double)Stopwatch.Frequency / 1000.0);
			int num3 = 0;
			foreach (Actor item in Batch)
			{
				num3++;
				try
				{
					if (IsPerson(item))
					{
						Capture(item);
					}
				}
				catch (Exception ex)
				{
					Log.Error("chronicle: " + ex.Message);
				}
				if (Stopwatch.GetTimestamp() - timestamp > num2)
				{
					break;
				}
			}
			_cursor += num3;
			double num4 = (double)(Stopwatch.GetTimestamp() - timestamp) * 1000.0 / (double)Stopwatch.Frequency;
			SweepMs += num4;
			SweepMsMax = Math.Max(SweepMsMax, num4);
			TotalMs += num4;
			TotalFrames++;
			if (++_frames >= 1800)
			{
				Log.Info($"chronicle: {SweepMs / (double)_frames:F3} ms a frame (worst {SweepMsMax:F2}), {Records.Count} people kept, {Visited} visits, {Writes} writes, {Cycles} sweeps");
				_frames = 0;
				SweepMs = 0.0;
				SweepMsMax = 0.0;
			}
		}

		private static void CheckWorld()
		{
			object map_stats = World.world.map_stats;
			if (map_stats != _world)
			{
				_world = map_stats;
				Records.Clear();
				Known.Clear();
				_cursor = 0;
			}
		}

		public static void Reset()
		{
			_world = null;
			Records.Clear();
			Known.Clear();
			_cursor = 0;
		}

		public static bool IsPerson(Actor a)
		{
			if (a == null || !a.isAlive() || a.asset == null || a.asset.is_boat)
			{
				return false;
			}
			try
			{
				return a.isSapient() && a.hasEmotions();
			}
			catch
			{
				return false;
			}
		}

		public static Actor Find(long id)
		{
			if (id <= 0 || World.world == null || World.world.units == null)
			{
				return null;
			}
			try
			{
				Actor actor = World.world.units.get(id);
				return (actor != null && actor.isAlive()) ? actor : null;
			}
			catch
			{
				return null;
			}
		}

		private static int YearOf(double time)
		{
			try
			{
				return Date.getYear(time);
			}
			catch
			{
				return 0;
			}
		}

		private static int BornOf(Actor a)
		{
			try
			{
				return YearOf(a.data.created_time);
			}
			catch
			{
				return int.MinValue;
			}
		}

		public static Record Of(Actor a)
		{
			if (a == null)
			{
				return null;
			}
			CheckWorldSafe();
			Record record = Load(a);
			if (record.Swept == 0 && a.isAlive() && IsPerson(a))
			{
				Capture(a);
			}
			return record;
		}

		private static void CheckWorldSafe()
		{
			try
			{
				if (World.world != null)
				{
					CheckWorld();
				}
			}
			catch
			{
			}
		}

		private static Record Load(Actor a)
		{
			long id = a.data.id;
			if (Records.TryGetValue(id, out var value))
			{
				return value;
			}
			value = new Record
			{
				Id = id
			};
			try
			{
				a.data.get("fp_kin", out var pResult, null);
				if (!string.IsNullOrEmpty(pResult))
				{
					ParseKin(pResult, value.Kin);
				}
				a.data.get("fp_mem", out var pResult2, null);
				if (!string.IsNullOrEmpty(pResult2))
				{
					ParseMemories(pResult2, value.Memories);
				}
				a.data.get("fp_memt", out var pResult3, 0L);
				value.LastStamp = pResult3;
				a.data.get("fp_you", out var pResult4, null);
				if (!string.IsNullOrEmpty(pResult4))
				{
					ParseRegards(pResult4, value.Regards);
				}
			}
			catch (Exception ex)
			{
				Log.Error("chronicle: could not read " + id + ": " + ex.Message);
			}
			foreach (Kin item in value.Kin)
			{
				Learn(item);
			}
			Records[id] = value;
			return value;
		}

		private static void Learn(Kin k)
		{
			if (k.Id > 0 && !string.IsNullOrEmpty(k.Name) && (!Known.TryGetValue(k.Id, out var value) || (k.Dead && !value.Dead)))
			{
				Known[k.Id] = k.Copy("?");
			}
		}

		public static Kin Named(long id)
		{
			if (id <= 0 || !Known.TryGetValue(id, out var value))
			{
				return null;
			}
			return value;
		}

		private static Kin Describe(Actor a, string role)
		{
			Kin obj = new Kin
			{
				Role = role,
				Id = a.data.id,
				Name = SafeName(a),
				Female = Models.IsFemale(a),
				Born = BornOf(a)
			};
			Learn(obj);
			return obj;
		}

		public static string SafeName(Actor a)
		{
			string text = null;
			try
			{
				text = a.getName();
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(text))
			{
				text = ((a.asset != null) ? a.asset.id.Replace('_', ' ') : "someone");
			}
			return text;
		}

		public static void Capture(Actor a)
		{
			Record record = Load(a);
			record.Swept++;
			Visited++;
			int year = Year;
			long num = 0L;
			long num2 = 0L;
			try
			{
				num = a.data.parent_id_1;
				num2 = a.data.parent_id_2;
			}
			catch
			{
			}
			Kin parent = Relative(record, "p1", num, year);
			Kin parent2 = Relative(record, "p2", num2, year);
			Grandparents(record, parent, "g1", "g2", year);
			Grandparents(record, parent2, "g3", "g4", year);
			Tell(num, a, year);
			Tell(num2, a, year);
			Actor actor = null;
			try
			{
				actor = a.lover;
			}
			catch
			{
			}
			Kin kin = record.Get("lv");
			if (actor != null && actor.isAlive())
			{
				if (kin == null || kin.Id != actor.data.id)
				{
					if (kin != null)
					{
						kin.Role = "xl";
						TrimRole(record, "xl", 3);
					}
					record.Kin.Add(Describe(actor, "lv"));
					record.KinDirty = true;
				}
				else
				{
					Refresh(kin, actor, record);
				}
			}
			else if (kin != null && !kin.Dead && Find(kin.Id) == null)
			{
				kin.Died = year;
				record.KinDirty = true;
				Learn(kin);
			}
			long id = 0L;
			try
			{
				id = a.data.best_friend_id;
			}
			catch
			{
			}
			Relative(record, "bf", id, year);
			foreach (Kin item in record.Kin)
			{
				if (!(item.Role != "ch") && !item.Dead && Find(item.Id) == null)
				{
					item.Died = year;
					record.KinDirty = true;
					Learn(item);
				}
			}
			foreach (Kin item2 in record.Kin)
			{
				if (!item2.Dead && !(item2.Role == "ch") && !(item2.Role == "lv") && item2.Role[0] == 'g' && Find(item2.Id) == null)
				{
					item2.Died = year;
					record.KinDirty = true;
					Learn(item2);
				}
			}
			Remember(a, record);
			if (record.KinDirty)
			{
				Save(a, "fp_kin", WriteKin(record.Kin));
				record.KinDirty = false;
			}
			if (record.MemDirty)
			{
				Save(a, "fp_mem", WriteMemories(record.Memories));
				try
				{
					a.data.set("fp_memt", record.LastStamp);
				}
				catch
				{
				}
				record.MemDirty = false;
			}
			if (record.YouDirty)
			{
				Save(a, "fp_you", WriteRegards(record.Regards));
				record.YouDirty = false;
			}
		}

		private static void Save(Actor a, string key, string value)
		{
			try
			{
				if (string.IsNullOrEmpty(value))
				{
					a.data.removeString(key);
				}
				else
				{
					a.data.set(key, value);
				}
				Writes++;
			}
			catch (Exception ex)
			{
				Log.Error("chronicle: could not write " + key + ": " + ex.Message);
			}
		}

		private static Kin Relative(Record r, string role, long id, int year)
		{
			Kin kin = r.Get(role);
			if (id <= 0)
			{
				return kin;
			}
			if (kin == null || kin.Id != id)
			{
				Actor actor = Find(id);
				Kin kin2 = ((actor != null) ? Describe(actor, role) : Named(id)?.Copy(role));
				if (kin2 == null)
				{
					return kin;
				}
				if (kin != null)
				{
					r.Kin.Remove(kin);
				}
				r.Kin.Add(kin2);
				r.KinDirty = true;
				return kin2;
			}
			if (!kin.Dead)
			{
				Actor actor2 = Find(id);
				if (actor2 == null)
				{
					kin.Died = year;
					r.KinDirty = true;
					Learn(kin);
				}
				else
				{
					Refresh(kin, actor2, r);
				}
			}
			return kin;
		}

		private static void Refresh(Kin k, Actor who, Record r)
		{
			if ((r.Swept & 7) == 1)
			{
				string text = SafeName(who);
				bool flag = Models.IsFemale(who);
				if (text != k.Name || flag != k.Female)
				{
					k.Name = text;
					k.Female = flag;
					r.KinDirty = true;
					Learn(k);
				}
			}
		}

		private static void Grandparents(Record r, Kin parent, string roleA, string roleB, int year)
		{
			if (parent == null)
			{
				return;
			}
			Record value;
			Record record = (Records.TryGetValue(parent.Id, out value) ? value : null);
			if (record == null)
			{
				Actor actor = Find(parent.Id);
				if (actor != null)
				{
					record = Load(actor);
				}
			}
			if (record != null)
			{
				Copy(r, record.Get("p1"), roleA);
				Copy(r, record.Get("p2"), roleB);
			}
		}

		private static void Copy(Record r, Kin from, string role)
		{
			if (from == null)
			{
				return;
			}
			Kin kin = r.Get(role);
			if (kin == null || kin.Id != from.Id || kin.Died != from.Died || !(kin.Name == from.Name))
			{
				if (kin != null)
				{
					r.Kin.Remove(kin);
				}
				r.Kin.Add(from.Copy(role));
				r.KinDirty = true;
			}
		}

		private static void Tell(long parentId, Actor child, int year)
		{
			Actor actor = Find(parentId);
			if (actor != null && IsPerson(actor))
			{
				Record record = Load(actor);
				if (record.Child(child.data.id) == null)
				{
					record.Kin.Add(Describe(child, "ch"));
					TrimRole(record, "ch", 16);
					record.KinDirty = true;
					Save(actor, "fp_kin", WriteKin(record.Kin));
					record.KinDirty = false;
				}
			}
		}

		private static void TrimRole(Record r, string role, int keep)
		{
			int num = 0;
			for (int num2 = r.Kin.Count - 1; num2 >= 0; num2--)
			{
				if (r.Kin[num2].Role == role && ++num > keep)
				{
					r.Kin.RemoveAt(num2);
				}
			}
		}

		private static void Remember(Actor a, Record r)
		{
			bool flag;
			try
			{
				flag = a.hasHappinessHistory();
			}
			catch
			{
				flag = false;
			}
			if (!flag)
			{
				return;
			}
			Queue<HappinessHistory> happiness_change_history;
			try
			{
				happiness_change_history = a.happiness_change_history;
			}
			catch
			{
				return;
			}
			if (happiness_change_history == null || happiness_change_history.Count == 0)
			{
				return;
			}
			HappinessHistory happinessHistory = default(HappinessHistory);
			foreach (HappinessHistory item in happiness_change_history)
			{
				happinessHistory = item;
			}
			if (r.HasLast && happinessHistory.index == r.LastIndex && happinessHistory.bonus == r.LastBonus && happinessHistory.timestamp == r.LastTime)
			{
				return;
			}
			int count = happiness_change_history.Count;
			if (_historyBuf.Length < count)
			{
				_historyBuf = new HappinessHistory[Math.Max(32, count)];
			}
			happiness_change_history.CopyTo(_historyBuf, 0);
			HappinessHistory[] historyBuf = _historyBuf;
			int num = 0;
			if (r.HasLast)
			{
				num = -1;
				for (int num2 = count - 1; num2 >= 0; num2--)
				{
					HappinessHistory happinessHistory2 = historyBuf[num2];
					if (happinessHistory2.index == r.LastIndex && happinessHistory2.bonus == r.LastBonus && happinessHistory2.timestamp == r.LastTime)
					{
						num = num2 + 1;
						break;
					}
				}
				if (num < 0)
				{
					num = count;
					for (int i = 0; i < count; i++)
					{
						if (historyBuf[i].timestamp >= r.LastTime)
						{
							num = i;
							break;
						}
					}
				}
			}
			HappinessHistory happinessHistory3 = historyBuf[count - 1];
			bool flag2 = !r.HasLast || happinessHistory3.index != r.LastIndex || happinessHistory3.bonus != r.LastBonus || happinessHistory3.timestamp != r.LastTime;
			for (int j = num; j < count; j++)
			{
				HappinessHistory happinessHistory4 = historyBuf[j];
				HappinessAsset asset;
				try
				{
					asset = happinessHistory4.asset;
				}
				catch
				{
					continue;
				}
				if (asset != null && Notable.Contains(asset.id))
				{
					AddMemory(a, r, asset.id, happinessHistory4.timestamp, (long)(happinessHistory4.timestamp * 1000.0));
				}
			}
			r.HasLast = true;
			r.LastIndex = happinessHistory3.index;
			r.LastBonus = happinessHistory3.bonus;
			r.LastTime = happinessHistory3.timestamp;
			long num3 = (long)(happinessHistory3.timestamp * 1000.0);
			if (flag2 && num3 >= r.LastStamp)
			{
				r.LastStamp = num3;
				r.MemDirty = true;
			}
		}

		private static void AddMemory(Actor a, Record r, string kind, double time, long stamp)
		{
			foreach (Memory memory in r.Memories)
			{
				if (memory.Kind == kind && Math.Abs(memory.Stamp - stamp) < 3000)
				{
					return;
				}
			}
			int month = 0;
			try
			{
				month = Date.getMonth(time);
			}
			catch
			{
			}
			r.Memories.Add(new Memory
			{
				Kind = kind,
				Year = YearOf(time),
				Month = month,
				Stamp = stamp,
				Other = WhoElse(a, r, kind)
			});
			r.Memories.Sort((Memory x, Memory y) => x.Stamp.CompareTo(y.Stamp));
			while (r.Memories.Count > 12)
			{
				r.Memories.RemoveAt(0);
			}
			r.MemDirty = true;
		}

		private static string WhoElse(Actor a, Record r, string kind)
		{
			try
			{
				if (kind != null)
				{
					switch (kind.Length)
					{
					default:
						goto end_IL_0000;
					case 11:
						switch (kind[6])
						{
						default:
							goto end_IL_0000;
						case 'l':
							if (kind == "death_lover")
							{
								Kin kin4 = r.Get("lv");
								if (kin4 != null && (kin4.Dead || Find(kin4.Id) == null))
								{
									return kin4.Name;
								}
								Kin kin5 = null;
								foreach (Kin item in r.All("xl"))
								{
									kin5 = item;
								}
								return kin5?.Name ?? kin4?.Name;
							}
							goto end_IL_0000;
						case 'c':
							if (kind == "death_child")
							{
								Kin kin3 = null;
								foreach (Kin item2 in r.All("ch"))
								{
									if ((item2.Dead || Find(item2.Id) == null) && (kin3 == null || item2.Died >= kin3.Died))
									{
										kin3 = item2;
									}
								}
								return kin3?.Name;
							}
							goto end_IL_0000;
						case '_':
							if (kind == "become_king")
							{
								break;
							}
							goto end_IL_0000;
						}
						goto IL_056a;
					case 14:
						switch (kind[0])
						{
						case 'f':
							if (!(kind == "fallen_in_love"))
							{
								break;
							}
							return (a.lover != null) ? SafeName(a.lover) : r.Get("lv")?.Name;
						case 'j':
						{
							if (!(kind == "just_had_child"))
							{
								break;
							}
							Kin kin = null;
							foreach (Kin item3 in r.All("ch"))
							{
								if (kin == null || item3.Born >= kin.Born)
								{
									kin = item3;
								}
							}
							return kin?.Name;
						}
						}
						goto end_IL_0000;
					case 16:
					{
						char c = kind[5];
						if (c != 'f')
						{
							switch (c)
							{
							default:
								goto end_IL_0000;
							case 'm':
								if (kind == "just_made_friend")
								{
									return r.Get("bf")?.Name;
								}
								goto end_IL_0000;
							case 's':
								if (kind == "just_started_war")
								{
									break;
								}
								goto end_IL_0000;
							}
							goto IL_056a;
						}
						if (kind == "just_found_house")
						{
							break;
						}
						goto end_IL_0000;
					}
					case 13:
					{
						char c = kind[5];
						if ((uint)c > 108u)
						{
							switch (c)
							{
							default:
								goto end_IL_0000;
							case 'o':
								if (kind == "was_conquered")
								{
									break;
								}
								goto end_IL_0000;
							case 'r':
								if (kind == "just_rebelled")
								{
									break;
								}
								goto end_IL_0000;
							}
							goto IL_056a;
						}
						if (c != 'e')
						{
							if (c == 'l' && kind == "just_lost_war")
							{
								goto IL_056a;
							}
						}
						else if (kind == "become_leader")
						{
							break;
						}
						goto end_IL_0000;
					}
					case 15:
					{
						char c = kind[5];
						if (c != 'l')
						{
							if (c == 'm' && kind == "just_made_peace")
							{
								goto IL_056a;
							}
						}
						else if (kind == "just_lost_house")
						{
							break;
						}
						goto end_IL_0000;
					}
					case 19:
						if (kind == "death_family_member")
						{
							Kin kin2 = null;
							foreach (Kin item4 in r.Kin)
							{
								if (!(item4.Role == "lv") && !(item4.Role == "xl") && !(item4.Role == "bf") && (item4.Dead || Find(item4.Id) == null) && (kin2 == null || item4.Died > kin2.Died))
								{
									kin2 = item4;
								}
							}
							foreach (Kin item5 in SiblingsOf(r))
							{
								if (item5.Dead && (kin2 == null || item5.Died > kin2.Died))
								{
									kin2 = item5;
								}
							}
							return kin2?.Name;
						}
						goto end_IL_0000;
					case 17:
						if (kind == "death_best_friend")
						{
							return r.Get("bf")?.Name;
						}
						goto end_IL_0000;
					case 10:
						if (kind == "lost_crown")
						{
							goto IL_056a;
						}
						goto end_IL_0000;
					case 18:
						if (kind == "kingdom_fell_apart")
						{
							goto IL_056a;
						}
						goto end_IL_0000;
					case 12:
						{
							if (kind == "just_won_war")
							{
								goto IL_056a;
							}
							goto end_IL_0000;
						}
						IL_056a:
						return (a.kingdom != null && a.kingdom.isCiv()) ? a.kingdom.name : null;
					}
					return (a.city != null) ? a.city.name : null;
				}
				end_IL_0000:;
			}
			catch
			{
			}
			return null;
		}

		public static List<Kin> SiblingsOf(Record r)
		{
			List<Kin> list = new List<Kin>();
			string[] array = new string[2] { "p1", "p2" };
			foreach (string role in array)
			{
				Kin kin = r.Get(role);
				if (kin == null || !Records.TryGetValue(kin.Id, out var value))
				{
					continue;
				}
				foreach (Kin item in value.All("ch"))
				{
					if (item.Id == r.Id)
					{
						continue;
					}
					bool flag = false;
					foreach (Kin item2 in list)
					{
						if (item2.Id == item.Id)
						{
							flag = true;
							break;
						}
					}
					if (!flag)
					{
						list.Add(item);
					}
				}
			}
			return list;
		}

		public static Record Cached(long id)
		{
			if (!Records.TryGetValue(id, out var value))
			{
				return null;
			}
			return value;
		}

		public static Regard RegardOf(Actor npc, Actor you, bool create = true)
		{
			if (npc == null || you == null)
			{
				return null;
			}
			Record record = Load(npc);
			long id = you.data.id;
			foreach (Regard regard2 in record.Regards)
			{
				if (regard2.Body == id)
				{
					return regard2;
				}
			}
			if (!create)
			{
				return null;
			}
			Regard regard = new Regard
			{
				Body = id,
				Opinion = StartingOpinion(npc, you)
			};
			record.Regards.Add(regard);
			while (record.Regards.Count > 6)
			{
				record.Regards.RemoveAt(0);
			}
			record.YouDirty = true;
			return regard;
		}

		private static int StartingOpinion(Actor npc, Actor you)
		{
			int num = 0;
			try
			{
				num = ((npc.lover == you) ? 70 : ((npc.isParentOf(you) || you.isParentOf(npc)) ? 60 : ((npc.family != null && npc.family == you.family) ? 35 : ((npc.data.best_friend_id == you.data.id) ? 50 : (Dialogue.Enemies(npc, you) ? (-45) : ((!Dialogue.SameTribe(npc, you)) ? (-5) : 10))))));
				if (npc.hasTrait("peaceful") || npc.hasTrait("honest"))
				{
					num += 8;
				}
				if (npc.hasTrait("evil"))
				{
					num -= 10;
				}
				if (npc.hasTrait("paranoid"))
				{
					num -= 8;
				}
				if (you.hasTrait("attractive"))
				{
					num += 5;
				}
				if (you.isKing() && npc.kingdom == you.kingdom)
				{
					num += 15;
				}
			}
			catch
			{
			}
			return Mathf.Clamp(num, -100, 100);
		}

		public static int Opinion(Actor npc, Actor you)
		{
			return RegardOf(npc, you)?.Opinion ?? 0;
		}

		public static void ChangeOpinion(Actor npc, Actor you, int delta)
		{
			Regard regard = RegardOf(npc, you);
			if (regard != null)
			{
				regard.Opinion = Mathf.Clamp(regard.Opinion + delta, -100, 100);
				Flush(npc);
			}
		}

		public static void Note(Actor npc, Actor you, int talks = 0, int gifts = 0, int quests = 0, int crimes = 0)
		{
			Regard regard = RegardOf(npc, you);
			if (regard != null)
			{
				regard.Talks += talks;
				regard.Gifts += gifts;
				regard.Quests += quests;
				regard.Crimes += crimes;
				Flush(npc);
			}
		}

		public static void Flush(Actor a)
		{
			Record record = Load(a);
			record.YouDirty = true;
			if (record.KinDirty)
			{
				Save(a, "fp_kin", WriteKin(record.Kin));
				record.KinDirty = false;
			}
			if (record.MemDirty)
			{
				Save(a, "fp_mem", WriteMemories(record.Memories));
				try
				{
					a.data.set("fp_memt", record.LastStamp);
				}
				catch
				{
				}
				record.MemDirty = false;
			}
			Save(a, "fp_you", WriteRegards(record.Regards));
			record.YouDirty = false;
		}

		public static void AddOwnMemory(Actor a, string kind, string other)
		{
			Record record = Load(a);
			double num = 0.0;
			try
			{
				num = World.world.getCurWorldTime();
			}
			catch
			{
			}
			int month = 0;
			try
			{
				month = Date.getMonth(num);
			}
			catch
			{
			}
			record.Memories.Add(new Memory
			{
				Kind = kind,
				Year = Year,
				Month = month,
				Stamp = (long)(num * 1000.0),
				Other = other
			});
			while (record.Memories.Count > 12)
			{
				record.Memories.RemoveAt(0);
			}
			record.MemDirty = true;
			Flush(a);
		}

		private static string Clean(string s)
		{
			if (!string.IsNullOrEmpty(s))
			{
				return s.Replace(",", " ").Replace("|", " ").Replace(":", " ")
					.Replace(";", " ");
			}
			return "";
		}

		private static string Int(int v)
		{
			if (v != int.MinValue)
			{
				return v.ToString();
			}
			return "";
		}

		private static int ParseInt(string s)
		{
			if (!int.TryParse(s, out var result))
			{
				return int.MinValue;
			}
			return result;
		}

		public static string WriteKin(List<Kin> kin)
		{
			Text.Length = 0;
			foreach (Kin item in kin)
			{
				if (Text.Length > 0)
				{
					Text.Append('|');
				}
				Text.Append(item.Role).Append(':').Append(item.Id)
					.Append(',')
					.Append(Clean(item.Name))
					.Append(',')
					.Append(item.Female ? 'f' : 'm')
					.Append(',')
					.Append(Int(item.Born))
					.Append(',')
					.Append(Int(item.Died));
			}
			return Text.ToString();
		}

		public static void ParseKin(string s, List<Kin> into)
		{
			string[] array = s.Split('|');
			foreach (string text in array)
			{
				int num = text.IndexOf(':');
				if (num > 0)
				{
					string[] array2 = text.Substring(num + 1).Split(',');
					if (array2.Length >= 5 && long.TryParse(array2[0], out var result))
					{
						into.Add(new Kin
						{
							Role = text.Substring(0, num),
							Id = result,
							Name = array2[1],
							Female = (array2[2] == "f"),
							Born = ParseInt(array2[3]),
							Died = ParseInt(array2[4])
						});
					}
				}
			}
		}

		public static string WriteMemories(List<Memory> mems)
		{
			Text.Length = 0;
			foreach (Memory mem in mems)
			{
				if (Text.Length > 0)
				{
					Text.Append('|');
				}
				Text.Append(mem.Kind).Append(',').Append(mem.Year)
					.Append(',')
					.Append(mem.Month)
					.Append(',')
					.Append(mem.Stamp)
					.Append(',')
					.Append(Clean(mem.Other));
			}
			return Text.ToString();
		}

		public static void ParseMemories(string s, List<Memory> into)
		{
			string[] array = s.Split('|');
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(',');
				if (array2.Length >= 5)
				{
					into.Add(new Memory
					{
						Kind = array2[0],
						Year = ParseInt(array2[1]),
						Month = ParseInt(array2[2]),
						Stamp = (long.TryParse(array2[3], out var result) ? result : 0),
						Other = ((array2[4].Length > 0) ? array2[4] : null)
					});
				}
			}
		}

		private static string WriteRegards(List<Regard> regards)
		{
			Text.Length = 0;
			foreach (Regard regard in regards)
			{
				if (Text.Length > 0)
				{
					Text.Append('|');
				}
				Text.Append(regard.Body).Append(':').Append(regard.Opinion)
					.Append(',')
					.Append(regard.Talks)
					.Append(',')
					.Append(regard.Gifts)
					.Append(',')
					.Append(regard.Quests)
					.Append(',')
					.Append(regard.Crimes);
			}
			return Text.ToString();
		}

		private static void ParseRegards(string s, List<Regard> into)
		{
			string[] array = s.Split('|');
			foreach (string text in array)
			{
				int num = text.IndexOf(':');
				if (num > 0 && long.TryParse(text.Substring(0, num), out var result))
				{
					string[] array2 = text.Substring(num + 1).Split(',');
					if (array2.Length >= 5)
					{
						into.Add(new Regard
						{
							Body = result,
							Opinion = Math.Max(-100, Math.Min(100, (ParseInt(array2[0]) != int.MinValue) ? ParseInt(array2[0]) : 0)),
							Talks = Math.Max(0, ParseInt(array2[1])),
							Gifts = Math.Max(0, ParseInt(array2[2])),
							Quests = Math.Max(0, ParseInt(array2[3])),
							Crimes = Math.Max(0, ParseInt(array2[4]))
						});
					}
				}
			}
		}
	}
}
