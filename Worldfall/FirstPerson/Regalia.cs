using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using FirstPerson.Core;
using FirstPerson.Net;
using FirstPerson.Towns;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal static class Regalia
	{
		private sealed class Entry
		{
			public Actor Obj;

			public RoleInfo Info;

			public PeopleTable Table;

			public int TableSerial;

			public int Version;

			public float NextAt;

			public float Touched;

			public bool Away;

			public float LastCampaignAt;
		}

		private sealed class PeopleTable
		{
			public Kingdom K;

			public long Id;

			public long HeirId = -1L;

			public readonly long[] SeatIds = new long[5] { -1L, -1L, -1L, -1L, -1L };

			public Actor Champion;

			public long ChampionId = -1L;

			public int Serial;

			public bool Filled;

			public bool Dead;

			public float CreatedAt;

			public float FilledAt = -99f;

			public float DemandedAt;

			public int Phase;

			public int ScanAt;

			public Actor ScanBest;

			public long ScanBestId;

			public int ScanBestScore;
		}

		private sealed class LookEntry
		{
			public Kingdom K;

			public KingdomLook Look;

			public int Version;

			public float NextAt;
		}

		public const string StyleKey = "fp_style";

		public const float NearTiles = 96f;

		public const int MaxPeopleTables = 24;

		private const int MaxEntries = 8192;

		private const float FillBudgetMs = 0.5f;

		private static readonly Dictionary<long, Entry> Entries = new Dictionary<long, Entry>();

		private static readonly Stack<Entry> Spare = new Stack<Entry>();

		private static readonly Dictionary<long, PeopleTable> Tables = new Dictionary<long, PeopleTable>();

		private static readonly List<PeopleTable> TableList = new List<PeopleTable>();

		private static readonly List<long> PruneKeys = new List<long>();

		private static readonly Stopwatch FillWatch = new Stopwatch();

		private static PeopleTable _filling;

		private static int _pruneAt;

		private static float _pruneNext;

		private static float _dropNext;

		private static int _serial;

		private static object _world;

		private static float _settleUntil;

		private static int _mainThread;

		internal static int AssignedWhileLoadingForTest;

		internal static int QueriedWhileLoadingForTest;

		internal static int HeavyFillsForTest;

		private static readonly Dictionary<long, LookEntry> Looks = new Dictionary<long, LookEntry>();

		private static readonly Dictionary<long, int> StyleSerials = new Dictionary<long, int>();

		private static readonly Dictionary<long, string> Whys = new Dictionary<long, string>();

		private static readonly List<Kingdom> Unstyled = new List<Kingdom>();

		private static readonly HashSet<Kingdom> Near = new HashSet<Kingdom>();

		private static readonly Comparison<Kingdom> Oldest = CompareAge;

		private static float _sweepIn;

		private static bool _sweepNow;

		private static long _previewId;

		private static KingdomStyle _previewStyle = KingdomStyle.None;

		private static int _previewFrame = -10;

		internal static int PeopleTablesForTest => Tables.Count;

		public static int Version { get; private set; }

		internal static Actor ChampionOf(Kingdom k)
		{
			if (!OnMain() || k == null || WorldBoxMod.WorldLoading)
			{
				return null;
			}
			PeopleTable peopleTable = TableFor(k, Time.unscaledTime);
			if (peopleTable == null || !peopleTable.Filled || peopleTable.Champion == null)
			{
				return null;
			}
			try
			{
				return (peopleTable.Champion.isAlive() && peopleTable.Champion.data.id == peopleTable.ChampionId) ? peopleTable.Champion : null;
			}
			catch
			{
				return null;
			}
		}

		internal static bool OnCampaignForTest(Actor a)
		{
			return RoleOf(a).Has(RoleBits.OnCampaign);
		}

		internal static void ResetForTest()
		{
			ClearCaches();
			AssignedWhileLoadingForTest = (QueriedWhileLoadingForTest = (HeavyFillsForTest = 0));
			Version++;
		}

		public static void Update(float dt)
		{
			_mainThread = Thread.CurrentThread.ManagedThreadId;
			float unscaledTime = Time.unscaledTime;
			CheckWorld(unscaledTime);
			if (WorldBoxMod.WorldLoading)
			{
				_settleUntil = unscaledTime + 3f;
				return;
			}
			FillOne(unscaledTime);
			Prune(unscaledTime);
			if (unscaledTime >= _dropNext)
			{
				_dropNext = unscaledTime + 1f;
				DropIdleTables(unscaledTime);
			}
			if (!WorldSync.Mirroring)
			{
				UpdateStyles(dt, unscaledTime);
			}
		}

		private static bool OnMain()
		{
			if (_mainThread != 0)
			{
				return Thread.CurrentThread.ManagedThreadId == _mainThread;
			}
			return false;
		}

		private static bool CheckWorld(float now)
		{
			object obj = null;
			try
			{
				obj = ((World.world != null) ? World.world.map_stats : null);
			}
			catch
			{
			}
			if (obj == _world)
			{
				return true;
			}
			_world = obj;
			ClearCaches();
			_settleUntil = now + 3f;
			Version++;
			return false;
		}

		private static void ClearCaches()
		{
			Entries.Clear();
			Spare.Clear();
			foreach (PeopleTable table in TableList)
			{
				table.Dead = true;
			}
			Tables.Clear();
			TableList.Clear();
			PruneKeys.Clear();
			_pruneAt = 0;
			_filling = null;
			ClearStyleCaches();
		}

		public static RoleInfo RoleOf(Actor a, bool forFigure = false)
		{
			if (a == null || !OnMain())
			{
				return default(RoleInfo);
			}
			if (WorldBoxMod.WorldLoading)
			{
				QueriedWhileLoadingForTest++;
				return default(RoleInfo);
			}
			float unscaledTime = Time.unscaledTime;
			if (!CheckWorld(unscaledTime))
			{
				return default(RoleInfo);
			}
			long id;
			try
			{
				id = a.data.id;
			}
			catch
			{
				return default(RoleInfo);
			}
			if (Entries.TryGetValue(id, out var value) && value.Obj == a)
			{
				value.Touched = unscaledTime;
				PeopleTable table = value.Table;
				if (table != null && !table.Dead)
				{
					table.DemandedAt = unscaledTime;
				}
				if (!forFigure && unscaledTime < value.NextAt && value.Version == Version)
				{
					bool num;
					if (table != null)
					{
						if (table.Dead)
						{
							goto IL_018c;
						}
						num = table.Serial == value.TableSerial;
					}
					else
					{
						num = value.TableSerial == -1;
					}
					if (num)
					{
						return Previewed(value.Info, forFigure);
					}
				}
			}
			else if (value != null)
			{
				value.Obj = a;
				value.Away = false;
				value.Info = default(RoleInfo);
			}
			else if (Entries.Count < 8192)
			{
				value = ((Spare.Count > 0) ? Spare.Pop() : new Entry());
				value.Obj = a;
				value.Away = false;
				value.Info = default(RoleInfo);
				Entries[id] = value;
			}
			else
			{
				value = new Entry
				{
					Obj = a
				};
			}
			goto IL_018c;
			IL_018c:
			value.Touched = unscaledTime;
			Compute(a, id, value, unscaledTime);
			return Previewed(value.Info, forFigure);
		}

		public static GearLook GearOf(Actor a, bool handsBusy, bool forFigure = false)
		{
			RoleInfo roleInfo = RoleOf(a, forFigure);
			if (!roleInfo.Wears || !roleInfo.Kingdom.Valid)
			{
				return default(GearLook);
			}
			return RegaliaRules.Compose(roleInfo.Bits, roleInfo.Seat, roleInfo.Kingdom.Style, roleInfo.HelmetId, handsBusy);
		}

		private static RoleInfo Previewed(RoleInfo r, bool forFigure)
		{
			if (forFigure && r.Kingdom.Valid && Previewing(r.Kingdom.Id))
			{
				r.Kingdom.Style = _previewStyle;
			}
			return r;
		}

		private static void Compute(Actor a, long id, Entry e, float now)
		{
			RoleInfo y = new RoleInfo
			{
				Seat = CouncilSeat.None
			};
			e.NextAt = now + 1f + (float)(id & 7) * 0.0625f;
			e.Version = Version;
			e.Table = null;
			e.TableSerial = -1;
			Kingdom kingdom = null;
			bool flag = false;
			try
			{
				kingdom = a.kingdom;
				flag = a.isAlive() && kingdom != null && kingdom.isCiv();
			}
			catch
			{
			}
			if (flag)
			{
				y.Kingdom = LookOf(kingdom);
				PeopleTable peopleTable = (e.Table = TableFor(kingdom, now));
				e.TableSerial = peopleTable?.Serial ?? (-1);
				RoleBits roleBits = RoleBits.None;
				bool flag2 = false;
				bool flag3 = false;
				try
				{
					if (a.isKing())
					{
						roleBits |= RoleBits.King;
					}
					if (Models.IsFemale(a))
					{
						roleBits |= RoleBits.Female;
					}
					if (!a.isAdult())
					{
						roleBits |= RoleBits.Child;
					}
					if (a.isCityLeader())
					{
						roleBits |= RoleBits.Leader;
					}
					flag2 = Service.IsSoldier(a);
					flag3 = flag2 && a.isArmyGroupLeader();
				}
				catch
				{
				}
				if (flag2)
				{
					roleBits |= RoleBits.Soldier;
				}
				if (flag3)
				{
					roleBits |= RoleBits.Captain;
				}
				if (peopleTable != null && peopleTable.Filled)
				{
					if (peopleTable.HeirId == id)
					{
						roleBits |= RoleBits.Heir;
					}
					for (int i = 0; i < 5; i++)
					{
						if (peopleTable.SeatIds[i] == id)
						{
							roleBits |= RoleBits.Councillor;
							y.Seat = (CouncilSeat)i;
							break;
						}
					}
					if (peopleTable.ChampionId == id)
					{
						roleBits |= RoleBits.Champion;
					}
				}
				if (flag2)
				{
					if (Campaign(a, e, now))
					{
						roleBits |= RoleBits.OnCampaign;
					}
					bool flag4 = false;
					try
					{
						flag4 = a.city != null;
					}
					catch
					{
					}
					if (flag4 && !flag3 && !e.Away)
					{
						roleBits |= RoleBits.Guard;
					}
				}
				else
				{
					e.Away = false;
				}
				try
				{
					y.HelmetId = ((a.equipment != null) ? People.ItemIdOf(a.equipment.helmet) : null);
				}
				catch
				{
					y.HelmetId = null;
				}
				y.Bits = roleBits;
				y.Wears = (((roleBits & RoleBits.Child) != RoleBits.None) ? ((roleBits & (RoleBits.King | RoleBits.Heir)) != 0) : ((roleBits & (RoleBits.King | RoleBits.Heir | RoleBits.Leader | RoleBits.Councillor | RoleBits.Champion | RoleBits.Captain | RoleBits.Soldier | RoleBits.Guard)) != 0));
			}
			else
			{
				e.Away = false;
			}
			RoleInfo x = e.Info;
			y.Serial = ((x.Serial != 0 && Same(in x, in y)) ? x.Serial : (++_serial));
			e.Info = y;
		}

		private static bool Same(in RoleInfo x, in RoleInfo y)
		{
			if (x.Bits == y.Bits && x.Seat == y.Seat && x.Wears == y.Wears && x.HelmetId == y.HelmetId && x.Kingdom.Id == y.Kingdom.Id && x.Kingdom.Style == y.Kingdom.Style && x.Kingdom.Styled == y.Kingdom.Styled && x.Kingdom.StyleSerial == y.Kingdom.StyleSerial && x.Kingdom.Colors.Main == y.Kingdom.Colors.Main && x.Kingdom.Colors.Second == y.Kingdom.Colors.Second)
			{
				return x.Kingdom.Colors.Emblem == y.Kingdom.Colors.Emblem;
			}
			return false;
		}

		private static bool Campaign(Actor a, Entry e, float now)
		{
			bool flag = false;
			try
			{
				City city = a.city;
				if (a.isArmyGroupWarrior() && a.army != null && a.army.hasCaptain() && city != null && city.hasAttackZoneOrder())
				{
					flag = true;
				}
				else if (a.isArmyGroupLeader() && city != null && city.hasAttackZoneOrder())
				{
					flag = true;
				}
				else if (a.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task != null)
				{
					string id = ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.id;
					int num;
					switch (id)
					{
					default:
						num = ((id == "fp_royal_sail") ? 1 : 0);
						break;
					case "fp_royal_march":
					case "fp_royal_hold":
					case "fp_royal_board":
						num = 1;
						break;
					}
					flag = (byte)num != 0;
				}
				if (!flag && a.is_inside_boat)
				{
					flag = true;
				}
			}
			catch
			{
			}
			if (flag)
			{
				e.Away = true;
				e.LastCampaignAt = now;
			}
			else if (e.Away)
			{
				bool flag2 = false;
				try
				{
					TileZone current_zone = a.current_zone;
					flag2 = current_zone != null && a.city != null && current_zone.city == a.city;
				}
				catch
				{
				}
				if (flag2 || now - e.LastCampaignAt > 60f)
				{
					e.Away = false;
				}
			}
			return e.Away;
		}

		private static PeopleTable TableFor(Kingdom k, float now)
		{
			long id;
			try
			{
				id = k.data.id;
			}
			catch
			{
				return null;
			}
			if (Tables.TryGetValue(id, out var value))
			{
				if (value.K == k)
				{
					value.DemandedAt = now;
					return value;
				}
				Drop(value);
			}
			if (Tables.Count >= 24)
			{
				return null;
			}
			value = new PeopleTable
			{
				K = k,
				Id = id,
				CreatedAt = now,
				DemandedAt = now
			};
			Tables[id] = value;
			TableList.Add(value);
			return value;
		}

		private static void Drop(PeopleTable t)
		{
			t.Dead = true;
			Tables.Remove(t.Id);
			TableList.Remove(t);
			if (_filling == t)
			{
				_filling = null;
			}
		}

		private static void DropIdleTables(float now)
		{
			for (int num = TableList.Count - 1; num >= 0; num--)
			{
				if (now - TableList[num].DemandedAt > 15f)
				{
					Drop(TableList[num]);
				}
			}
		}

		private static void FillOne(float now)
		{
			if (_filling == null || _filling.Dead)
			{
				_filling = null;
				PeopleTable peopleTable = null;
				foreach (PeopleTable table in TableList)
				{
					if (!table.Filled)
					{
						_filling = table;
						break;
					}
					if (now - table.DemandedAt < 10f && now - table.FilledAt > 3f && (peopleTable == null || table.FilledAt < peopleTable.FilledAt))
					{
						peopleTable = table;
					}
				}
				if (_filling == null)
				{
					_filling = peopleTable;
				}
				if (_filling == null)
				{
					return;
				}
				_filling.Phase = 0;
			}
			FillWatch.Reset();
			FillWatch.Start();
			PeopleTable filling = _filling;
			Kingdom k = filling.K;
			bool flag = false;
			try
			{
				flag = k.isAlive() && k.data.id == filling.Id;
			}
			catch
			{
			}
			if (!flag)
			{
				Finish(filling, now);
				return;
			}
			if (filling.Phase == 0)
			{
				Actor a = null;
				try
				{
					a = SuccessionTool.findNextHeir(k, k.king);
				}
				catch
				{
				}
				filling.HeirId = IdOf(a);
				for (int i = 0; i < 5; i++)
				{
					Actor a2 = null;
					try
					{
						a2 = Court.Holder(k, (Seat)i);
					}
					catch
					{
					}
					filling.SeatIds[i] = IdOf(a2);
				}
				filling.Phase = 1;
				filling.ScanAt = 0;
				filling.ScanBest = null;
				filling.ScanBestId = -1L;
				filling.ScanBestScore = int.MinValue;
			}
			List<Actor> list = null;
			try
			{
				list = k.units;
			}
			catch
			{
			}
			int num = list?.Count ?? 0;
			while (filling.ScanAt < num)
			{
				Actor actor = list[filling.ScanAt++];
				if (Fighter(actor, k, out var id, out var score) && (score > filling.ScanBestScore || (score == filling.ScanBestScore && id < filling.ScanBestId)))
				{
					filling.ScanBest = actor;
					filling.ScanBestId = id;
					filling.ScanBestScore = score;
				}
				if ((filling.ScanAt & 0x3F) == 0 && FillWatch.Elapsed.TotalMilliseconds > 0.5)
				{
					return;
				}
			}
			if (filling.Champion == null || !Fighter(filling.Champion, k, out var id2, out var score2) || id2 != filling.ChampionId || score2 < filling.ScanBestScore)
			{
				filling.Champion = filling.ScanBest;
				filling.ChampionId = filling.ScanBestId;
			}
			Finish(filling, now);
		}

		private static void Finish(PeopleTable f, float now)
		{
			f.ScanBest = null;
			f.Filled = true;
			f.FilledAt = now;
			f.Serial++;
			f.Phase = 0;
			HeavyFillsForTest++;
			_filling = null;
		}

		private static bool Fighter(Actor a, Kingdom k, out long id, out int score)
		{
			id = -1L;
			score = 0;
			try
			{
				if (a == null || !a.isAlive() || a.kingdom != k || a == k.king || !a.isWarrior() || a.asset == null || a.asset.is_boat)
				{
					return false;
				}
				id = a.data.id;
				score = a.data.kills * 10 + a.data.level;
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static long IdOf(Actor a)
		{
			try
			{
				return a?.data.id ?? (-1);
			}
			catch
			{
				return -1L;
			}
		}

		private static void Prune(float now)
		{
			if (_pruneAt >= PruneKeys.Count)
			{
				if (now < _pruneNext)
				{
					return;
				}
				_pruneNext = now + 1f;
				PruneKeys.Clear();
				foreach (long key2 in Entries.Keys)
				{
					PruneKeys.Add(key2);
				}
				_pruneAt = 0;
			}
			int num = Math.Min(PruneKeys.Count, _pruneAt + 256);
			while (_pruneAt < num)
			{
				long key = PruneKeys[_pruneAt];
				if (Entries.TryGetValue(key, out var value) && now - value.Touched > 20f)
				{
					Entries.Remove(key);
					value.Obj = null;
					value.Table = null;
					value.Info = default(RoleInfo);
					if (Spare.Count < 1024)
					{
						Spare.Push(value);
					}
				}
				_pruneAt++;
			}
		}

		public static string StyleName(KingdomStyle s)
		{
			switch (s)
			{
			case KingdomStyle.IronSpikes:
				return "Iron spikes";
			case KingdomStyle.GoldWings:
				return "Gold wings";
			case KingdomStyle.GildedSpire:
				return "Gilded spire";
			default:
				return null;
			}
		}

		public static bool Styled(Kingdom k)
		{
			try
			{
				return k != null && k.isCiv() && Family(k) == "human";
			}
			catch
			{
				return false;
			}
		}

		private static string Family(Kingdom k)
		{
			try
			{
				return k.data.original_actor_asset;
			}
			catch
			{
				return null;
			}
		}

		private static KingdomStyle Stored(Kingdom k)
		{
			try
			{
				k.data.get("fp_style", out var pResult, -1);
				return (pResult >= 0 && pResult <= 2) ? ((KingdomStyle)pResult) : KingdomStyle.None;
			}
			catch
			{
				return KingdomStyle.None;
			}
		}

		public static int StyleSerial(Kingdom k)
		{
			long id;
			try
			{
				id = k.data.id;
			}
			catch
			{
				return 0;
			}
			if (!StyleSerials.TryGetValue(id, out var value))
			{
				return 0;
			}
			return value;
		}

		public static KingdomLook LookOf(Kingdom k, bool forFigure = false)
		{
			if (k == null || !OnMain())
			{
				return default(KingdomLook);
			}
			if (WorldBoxMod.WorldLoading)
			{
				QueriedWhileLoadingForTest++;
				return default(KingdomLook);
			}
			float unscaledTime = Time.unscaledTime;
			if (!CheckWorld(unscaledTime))
			{
				return default(KingdomLook);
			}
			long id;
			try
			{
				id = k.data.id;
			}
			catch
			{
				return default(KingdomLook);
			}
			if (!Looks.TryGetValue(id, out var value) || value.K != k)
			{
				if (value == null)
				{
					value = (Looks[id] = new LookEntry());
				}
				value.K = k;
				value.NextAt = -1f;
			}
			if (unscaledTime >= value.NextAt || value.Version != Version)
			{
				value.Look = Read(k, id);
				value.Version = Version;
				value.NextAt = unscaledTime + 3f + (float)(id & 7) * 0.125f;
			}
			KingdomLook look = value.Look;
			if (forFigure && look.Valid && Previewing(id))
			{
				look.Style = _previewStyle;
			}
			return look;
		}

		private static KingdomLook Read(Kingdom k, long id)
		{
			KingdomLook result = new KingdomLook
			{
				Style = KingdomStyle.None
			};
			bool flag = false;
			bool flag2 = false;
			try
			{
				flag = k.isCiv();
				flag2 = k.isAlive();
			}
			catch
			{
			}
			if (!flag)
			{
				return result;
			}
			result.Id = id;
			result.Style = Stored(k);
			if (result.Style == KingdomStyle.None && flag2)
			{
				_sweepNow = true;
			}
			result.Styled = Family(k) == "human";
			result.StyleSerial = (StyleSerials.TryGetValue(id, out var value) ? value : 0);
			uint main = uint.MaxValue;
			uint second = uint.MaxValue;
			uint banner = uint.MaxValue;
			try
			{
				ColorAsset color = k.getColor();
				if (color != null)
				{
					main = Pack(color.getColorMain());
					second = Pack(color.getColorMainSecond());
					banner = Pack(color.getColorBanner());
				}
			}
			catch
			{
			}
			result.Colors = RegaliaColors.From(main, second, banner);
			return result;
		}

		private static uint Pack(Color c)
		{
			return TerrainCache.Rgb(Mathf.Clamp((int)(c.r * 255f + 0.5f), 0, 255), Mathf.Clamp((int)(c.g * 255f + 0.5f), 0, 255), Mathf.Clamp((int)(c.b * 255f + 0.5f), 0, 255));
		}

		public static bool SetStyle(Actor king, KingdomStyle s)
		{
			if (!OnMain() || WorldBoxMod.WorldLoading || WorldSync.Mirroring)
			{
				return false;
			}
			if (s < KingdomStyle.IronSpikes || s > KingdomStyle.GildedSpire || !Royal.IsKing(king))
			{
				return false;
			}
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance == null || instance.Host != king)
			{
				return false;
			}
			Kingdom kingdom;
			long id;
			try
			{
				kingdom = king.kingdom;
				id = kingdom.data.id;
			}
			catch
			{
				return false;
			}
			if (!Styled(kingdom) || Stored(kingdom) == s)
			{
				return false;
			}
			CheckWorld(Time.unscaledTime);
			try
			{
				kingdom.data.set("fp_style", (int)s);
			}
			catch (Exception ex)
			{
				Log.Error("regalia: style: " + ex.Message);
				return false;
			}
			StyleSerials[id] = (StyleSerials.TryGetValue(id, out var value) ? value : 0) + 1;
			Version++;
			Log.Info("regalia: " + Royal.NameOf(kingdom) + " now wears " + StyleName(s) + " (the king's choice)");
			return true;
		}

		public static void PreviewStyle(Kingdom k, KingdomStyle s)
		{
			if (k != null && OnMain())
			{
				try
				{
					_previewId = k.data.id;
				}
				catch
				{
					return;
				}
				_previewStyle = s;
				_previewFrame = Time.frameCount;
			}
		}

		private static bool Previewing(long id)
		{
			if (id == _previewId && _previewStyle != KingdomStyle.None)
			{
				return Time.frameCount - _previewFrame <= 1;
			}
			return false;
		}

		internal static string WhyForTest(Kingdom k)
		{
			if (k == null)
			{
				return null;
			}
			try
			{
				if (Whys.TryGetValue(k.data.id, out var value))
				{
					return value;
				}
			}
			catch
			{
				return null;
			}
			Pick(k, out var why);
			return why;
		}

		private static void ClearStyleCaches()
		{
			Looks.Clear();
			StyleSerials.Clear();
			Whys.Clear();
			Unstyled.Clear();
			Near.Clear();
			_sweepNow = false;
			_sweepIn = 0f;
			_previewId = 0L;
		}

		private static void UpdateStyles(float dt, float now)
		{
			_sweepIn -= dt;
			if ((!(_sweepIn > 0f) || _sweepNow) && !(now < _settleUntil) && !WorldBoxMod.WorldLoading)
			{
				_sweepIn = 1f;
				_sweepNow = false;
				Sweep();
			}
		}

		private static void Sweep()
		{
			Unstyled.Clear();
			try
			{
				foreach (Kingdom item in World.world.kingdoms.list)
				{
					try
					{
						if (item == null || !item.isAlive() || !item.isCiv())
						{
							continue;
						}
						goto IL_0043;
					}
					catch
					{
					}
					continue;
					IL_0043:
					if (Stored(item) == KingdomStyle.None)
					{
						Unstyled.Add(item);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("regalia: sweep: " + ex.Message);
			}
			if (Unstyled.Count == 0)
			{
				return;
			}
			if (Unstyled.Count > 1)
			{
				Unstyled.Sort(Oldest);
			}
			foreach (Kingdom item2 in Unstyled)
			{
				Assign(item2);
			}
			Unstyled.Clear();
		}

		private static int CompareAge(Kingdom x, Kingdom y)
		{
			double num = 0.0;
			double value = 0.0;
			long num2 = 0L;
			long value2 = 0L;
			try
			{
				num = x.data.created_time;
				num2 = x.data.id;
			}
			catch
			{
			}
			try
			{
				value = y.data.created_time;
				value2 = y.data.id;
			}
			catch
			{
			}
			int num3 = num.CompareTo(value);
			if (num3 == 0)
			{
				return num2.CompareTo(value2);
			}
			return num3;
		}

		private static void Assign(Kingdom k)
		{
			if (WorldBoxMod.WorldLoading)
			{
				AssignedWhileLoadingForTest++;
				return;
			}
			string why;
			KingdomStyle kingdomStyle = Pick(k, out why);
			long id;
			try
			{
				id = k.data.id;
				k.data.set("fp_style", (int)kingdomStyle);
			}
			catch (Exception ex)
			{
				Log.Error("regalia: assign: " + ex.Message);
				return;
			}
			Whys[id] = why;
			Version++;
			Log.Info("regalia: " + Royal.NameOf(k) + " takes " + StyleName(kingdomStyle) + " (" + why + ")");
		}

		private static KingdomStyle Pick(Kingdom k, out string why)
		{
			string text = Family(k);
			Vector2 at;
			bool flag = Centre(k, out at);
			Neighbours(k, text, flag, at);
			bool flag2 = false;
			bool flag3 = false;
			bool flag4 = false;
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			float num4 = float.PositiveInfinity;
			float num5 = float.PositiveInfinity;
			float num6 = float.PositiveInfinity;
			try
			{
				foreach (Kingdom item in World.world.kingdoms.list)
				{
					if (item == null || item == k)
					{
						continue;
					}
					try
					{
						if (!item.isAlive() || !item.isCiv() || Family(item) != text)
						{
							continue;
						}
						goto IL_00a1;
					}
					catch
					{
					}
					continue;
					IL_00a1:
					KingdomStyle kingdomStyle = Stored(item);
					if (kingdomStyle != KingdomStyle.None)
					{
						bool flag5 = Near.Contains(item);
						Vector2 at2;
						float b = (flag5 ? 0f : ((flag && Centre(item, out at2)) ? Vector2.Distance(at, at2) : float.PositiveInfinity));
						switch (kingdomStyle)
						{
						case KingdomStyle.IronSpikes:
							num++;
							flag2 = flag2 || flag5;
							num4 = Mathf.Min(num4, b);
							break;
						case KingdomStyle.GoldWings:
							num2++;
							flag3 = flag3 || flag5;
							num5 = Mathf.Min(num5, b);
							break;
						default:
							num3++;
							flag4 = flag4 || flag5;
							num6 = Mathf.Min(num6, b);
							break;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("regalia: pick: " + ex.Message);
			}
			Near.Clear();
			long id = 0L;
			try
			{
				id = k.data.id;
			}
			catch
			{
			}
			ulong mixB = Mix(id, 0);
			ulong num7 = Mix(id, 1);
			ulong mixA = Mix(id, 2);
			int num8 = 0;
			if (Better(flag3, num2, num5, num7, flag2, num, num4, mixB))
			{
				num8 = 1;
			}
			if ((num8 == 0) ? Better(flag4, num3, num6, mixA, flag2, num, num4, mixB) : Better(flag4, num3, num6, mixA, flag3, num2, num5, num7))
			{
				num8 = 2;
			}
			why = "nb=" + (flag2 ? 1 : 0) + "," + (flag3 ? 1 : 0) + "," + (flag4 ? 1 : 0) + " used=" + num + "," + num2 + "," + num3 + " far=" + Far(num4) + "," + Far(num5) + "," + Far(num6) + " mix=" + mixB + "," + num7 + "," + mixA + " pick=" + num8;
			return (KingdomStyle)num8;
		}

		private static bool Better(bool nbA, int usedA, float farA, ulong mixA, bool nbB, int usedB, float farB, ulong mixB)
		{
			if (nbA != nbB)
			{
				return !nbA;
			}
			if (usedA != usedB)
			{
				return usedA < usedB;
			}
			if (farA != farB)
			{
				return farA > farB;
			}
			return mixA < mixB;
		}

		private static ulong Mix(long id, int s)
		{
			return (ulong)(id * -7046029254386353131L + (s + 1) * -4658895280553007687L) >> 40;
		}

		private static string Far(float d)
		{
			if (!float.IsPositiveInfinity(d))
			{
				return d.ToString("0.#", CultureInfo.InvariantCulture);
			}
			return "inf";
		}

		private static void Neighbours(Kingdom k, string family, bool hasCentre, Vector2 at)
		{
			Near.Clear();
			try
			{
				List<City> cities = k.cities;
				if (cities != null)
				{
					for (int i = 0; i < cities.Count; i++)
					{
						City city = cities[i];
						if (city == null || city.neighbours_kingdoms == null)
						{
							continue;
						}
						foreach (Kingdom neighbours_kingdom in city.neighbours_kingdoms)
						{
							if (neighbours_kingdom != null && neighbours_kingdom != k)
							{
								Near.Add(neighbours_kingdom);
							}
						}
					}
				}
			}
			catch
			{
			}
			if (!hasCentre)
			{
				return;
			}
			try
			{
				foreach (Kingdom item in World.world.kingdoms.list)
				{
					if (item == null || item == k || Near.Contains(item))
					{
						continue;
					}
					try
					{
						if (!item.isAlive() || !item.isCiv() || Family(item) != family)
						{
							continue;
						}
						goto IL_00e9;
					}
					catch
					{
					}
					continue;
					IL_00e9:
					if (Centre(item, out var at2) && (at2 - at).sqrMagnitude <= 9216f)
					{
						Near.Add(item);
					}
				}
			}
			catch
			{
			}
		}

		private static bool Centre(Kingdom k, out Vector2 at)
		{
			at = default(Vector2);
			try
			{
				City city = k.capital;
				if (city == null || !city.isAlive())
				{
					city = null;
					List<City> cities = k.cities;
					if (cities != null)
					{
						for (int i = 0; i < cities.Count; i++)
						{
							if (city != null)
							{
								break;
							}
							if (cities[i] != null && cities[i].isAlive())
							{
								city = cities[i];
							}
						}
					}
				}
				if (city == null)
				{
					return false;
				}
				at = city.city_center;
				if (at == Vector2.zero || at.x < 0f)
				{
					at = city.last_city_center;
				}
				return at != Vector2.zero && at.x >= 0f && at.y >= 0f;
			}
			catch
			{
				return false;
			}
		}

		public static string TitleFor(Actor npc, Actor you, bool high)
		{
			RoleInfo roleInfo = RoleOf(npc);
			if (!roleInfo.Kingdom.Valid || roleInfo.Has(RoleBits.King) || !Places(npc, out var realm, out var town))
			{
				return null;
			}
			if (high)
			{
				if (roleInfo.Has(RoleBits.Heir))
				{
					return "Heir of " + realm;
				}
				if (roleInfo.Has(RoleBits.Councillor))
				{
					switch (roleInfo.Seat)
					{
					case CouncilSeat.Marshal:
						return "Marshal of " + realm;
					case CouncilSeat.Steward:
						return "Steward of " + realm;
					case CouncilSeat.Chancellor:
						return "Chancellor of " + realm;
					case CouncilSeat.Spymaster:
						if (KnowsSpymaster(npc, you))
						{
							return "Spymaster of " + realm;
						}
						break;
					case CouncilSeat.Mage:
						return "Court mage of " + realm;
					}
				}
				if (roleInfo.Has(RoleBits.Champion))
				{
					return "Champion of " + realm;
				}
				if (roleInfo.Has(RoleBits.Captain))
				{
					return "Captain of " + (town ?? realm) + "'s army";
				}
				return null;
			}
			if (roleInfo.Has(RoleBits.Guard) && town != null)
			{
				return "Guard of " + town;
			}
			if (roleInfo.Has(RoleBits.Soldier))
			{
				return "Soldier of " + realm;
			}
			return null;
		}

		public static string NameplateRole(Actor host)
		{
			RoleInfo roleInfo = RoleOf(host);
			if (!roleInfo.Kingdom.Valid || roleInfo.Has(RoleBits.King) || !Places(host, out var realm, out var town))
			{
				return null;
			}
			if (roleInfo.Has(RoleBits.Heir))
			{
				return "Heir of " + realm + ((town != null) ? ("  ·  " + town) : "");
			}
			if (roleInfo.Has(RoleBits.Councillor))
			{
				switch (roleInfo.Seat)
				{
				case CouncilSeat.Marshal:
					return "Marshal of " + realm + ((town != null) ? ("  ·  " + town) : "");
				case CouncilSeat.Steward:
					return "Steward of " + realm + ((town != null) ? ("  ·  " + town) : "");
				case CouncilSeat.Chancellor:
					return "Chancellor of " + realm + ((town != null) ? ("  ·  " + town) : "");
				case CouncilSeat.Spymaster:
					return "Spymaster of " + realm + ((town != null) ? ("  ·  " + town) : "");
				case CouncilSeat.Mage:
					return "Court mage of " + realm + ((town != null) ? ("  ·  " + town) : "");
				}
			}
			if (roleInfo.Has(RoleBits.Champion))
			{
				return "Champion of " + realm + ((town != null) ? ("  ·  " + town) : "");
			}
			if (roleInfo.Has(RoleBits.Captain))
			{
				if (town == null)
				{
					return "Captain of " + realm + "'s army";
				}
				return "Captain of " + town + "'s army  ·  " + realm;
			}
			if (roleInfo.Has(RoleBits.Guard) && town != null)
			{
				return "Guard of " + town + "  ·  " + realm;
			}
			if (roleInfo.Has(RoleBits.Soldier))
			{
				return "Soldier of " + realm + ((town != null) ? ("  ·  " + town) : "");
			}
			return null;
		}

		private static bool Places(Actor a, out string realm, out string town)
		{
			realm = (town = null);
			try
			{
				realm = ((a.kingdom != null) ? a.kingdom.name : null);
				town = ((a.city != null) ? a.city.name : null);
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(town))
			{
				town = null;
			}
			return !string.IsNullOrEmpty(realm);
		}

		private static bool KnowsSpymaster(Actor npc, Actor you)
		{
			if (you == null)
			{
				return false;
			}
			if (you == npc)
			{
				return true;
			}
			try
			{
				return you.kingdom != null && you.kingdom == npc.kingdom && PeopleMet.Knows(you, npc);
			}
			catch
			{
				return false;
			}
		}
	}
}
