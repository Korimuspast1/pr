using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FirstPerson.Net;
using UnityEngine;

namespace FirstPerson
{
	internal static class Loot
	{
		private sealed class Snap
		{
			public float At;

			public long[] Ids = new long[0];

			public Gear.Piece[] Pieces = new Gear.Piece[0];

			public int Money;

			public List<KeyValuePair<string, int>> Bag = new List<KeyValuePair<string, int>>();

			public bool ByYou;
		}

		internal sealed class Fallen
		{
			public object Space;

			public bool Sack;

			public float Z;

			public long Id;

			public string Name;

			public Vector2 At;

			public float Born;

			public float Lie;

			public float Age;

			public bool ByYou;

			public bool Reported;

			public Kingdom Kingdom;

			public readonly List<Gear.Piece> Pieces = new List<Gear.Piece>();

			public readonly List<long> ItemIds = new List<long>();

			public readonly List<bool> Existed = new List<bool>();

			public int Money;

			public readonly List<KeyValuePair<string, int>> Bag = new List<KeyValuePair<string, int>>();

			public bool Empty
			{
				get
				{
					if (Pieces.Count == 0 && Money <= 0)
					{
						return Bag.Count == 0;
					}
					return false;
				}
			}
		}

		private sealed class Handed
		{
			public Actor You;

			public float At;

			public int Money;

			public readonly List<KeyValuePair<string, int>> Bag = new List<KeyValuePair<string, int>>();
		}

		public const float Range = 16f;

		public const float Reach = 2.2f;

		public const float SackSeconds = 300f;

		private const float FadedAfter = 2.2f;

		private const float YoursLie = 30f;

		private const float OthersLie = 15f;

		private static readonly Dictionary<Actor, Snap> Snaps = new Dictionary<Actor, Snap>();

		public static readonly List<Fallen> Bodies = new List<Fallen>();

		private static readonly List<Actor> Stale = new List<Actor>();

		private static object _world;

		private static readonly HashSet<long> Looted = new HashSet<long>();

		private const int AddLootIl = 85;

		private const int TakeAllIl = 91;

		private static bool _killInstalled;

		private static bool _killReported;

		private static readonly Dictionary<long, Handed> HandedYou = new Dictionary<long, Handed>();

		private static readonly List<long> StaleHanded = new List<long>();

		[ThreadStatic]
		private static int _lootAdded;

		internal static int KeptOnBody;

		internal static bool Lean = true;

		private static readonly List<Item> Worn = new List<Item>(8);

		private static readonly Dictionary<Actor, Snap> Probes = new Dictionary<Actor, Snap>();

		private static bool _verify;

		private static int _compared;

		private static int _differ;

		private static string _first;

		public static bool KillInstalled => _killInstalled;

		internal static int LeanCompared => _compared;

		public static void Watch(Actor a)
		{
			ProbeWorn(a);
			try
			{
				WorldBoxMod instance = WorldBoxMod.Instance;
				Actor actor = instance?.Host;
				if (a == null || actor == null || a == actor || a == instance.Body || !a.isAlive() || (a.current_position - actor.current_position).sqrMagnitude > 256f)
				{
					return;
				}
				if (!Snaps.TryGetValue(a, out var value))
				{
					value = (Snaps[a] = new Snap());
				}
				if (Time.unscaledTime - value.At < 0.5f && value.At > 0f)
				{
					return;
				}
				value.At = Time.unscaledTime;
				if (Lean)
				{
					WornNow(a, value);
				}
				else
				{
					List<Item> list = (a.hasEquipment() ? a.equipment.getItems().ToList() : new List<Item>());
					bool flag = list.Count == value.Ids.Length;
					int num = 0;
					while (flag && num < list.Count)
					{
						flag = list[num].data.id == value.Ids[num];
						num++;
					}
					if (!flag)
					{
						value.Ids = list.Select((Item it) => it.data.id).ToArray();
						value.Pieces = list.Select(Gear.FromItem).ToArray();
					}
				}
				value.Money = a.money + a.loot;
				BaseSimObject attackedBy = a.attackedBy;
				value.ByYou = attackedBy != null && (attackedBy == actor || attackedBy == instance.Body);
				value.Bag.Clear();
				Dictionary<string, ResourceContainer> dictionary = a.inventory?.getResources();
				if (dictionary != null)
				{
					foreach (KeyValuePair<string, ResourceContainer> item in dictionary)
					{
						if (item.Value.amount > 0)
						{
							value.Bag.Add(new KeyValuePair<string, int>(item.Key, item.Value.amount));
						}
					}
				}
				Supplies.AddTo(a, value.Bag);
			}
			catch
			{
			}
		}

		public static float Fell(Actor a)
		{
			if (a == null || !Snaps.TryGetValue(a, out var value))
			{
				return 0f;
			}
			Snaps.Remove(a);
			if (Time.unscaledTime - value.At > 3f)
			{
				return 0f;
			}
			Fallen fallen = new Fallen
			{
				Born = Time.unscaledTime,
				Money = value.Money,
				Space = RoomCombat.FallingIn,
				Z = RoomCombat.FallingZ
			};
			try
			{
				fallen.Id = a.data.id;
				fallen.Name = Dialogue.NameOf(a);
				fallen.At = a.current_position;
				fallen.Kingdom = a.kingdom;
				fallen.ByYou = Quests.YouKilled(a.data.id);
			}
			catch
			{
			}
			for (int i = 0; i < value.Pieces.Length; i++)
			{
				if (value.Pieces[i] != null && value.Pieces[i].Asset != null)
				{
					fallen.Pieces.Add(value.Pieces[i]);
					fallen.ItemIds.Add(value.Ids[i]);
				}
			}
			fallen.Bag.AddRange(value.Bag);
			int b = 0;
			Dictionary<string, ResourceContainer> dictionary = null;
			try
			{
				b = a.money + a.loot;
				dictionary = a.inventory?.getResources();
			}
			catch
			{
			}
			fallen.Money = Mathf.Min(fallen.Money, Mathf.Max(0, b));
			for (int num = fallen.Bag.Count - 1; num >= 0; num--)
			{
				if (!Supplies.Is(fallen.Bag[num].Key))
				{
					int num2 = 0;
					try
					{
						if (dictionary != null && dictionary.TryGetValue(fallen.Bag[num].Key, out var value2))
						{
							num2 = value2.amount;
						}
					}
					catch
					{
					}
					if (num2 <= 0)
					{
						fallen.Bag.RemoveAt(num);
					}
					else if (num2 < fallen.Bag[num].Value)
					{
						fallen.Bag[num] = new KeyValuePair<string, int>(fallen.Bag[num].Key, num2);
					}
				}
			}
			if (TakeBack(a, fallen))
			{
				fallen.ByYou = true;
			}
			fallen.Bag.RemoveAll((KeyValuePair<string, int> kv) => kv.Key == "tw_item_torch");
			foreach (long itemId in fallen.ItemIds)
			{
				bool item = false;
				try
				{
					item = World.world.items.get(itemId) != null;
				}
				catch
				{
				}
				fallen.Existed.Add(item);
			}
			if (fallen.Empty)
			{
				return 0f;
			}
			fallen.Lie = ((fallen.ByYou || value.ByYou) ? 30f : 15f);
			Bodies.Add(fallen);
			if (Bodies.Count > 32)
			{
				Bodies.RemoveAt(0);
			}
			return fallen.Lie;
		}

		public static void Tick()
		{
			float unscaledTime = Time.unscaledTime;
			object obj = World.world?.map_stats;
			if (obj != _world)
			{
				_world = obj;
				Clear();
			}
			foreach (Fallen body in Bodies)
			{
				body.Age += Time.unscaledDeltaTime;
				if (body.Space != null && !body.Sack && body.Age > body.Lie + 2.2f && !body.Empty)
				{
					body.Sack = true;
					body.Lie = body.Age + 300f;
				}
			}
			Bodies.RemoveAll((Fallen f) => (f.Sack ? (f.Age > f.Lie) : ((f.Space != null) ? (f.Age > f.Lie + 2.2f) : (f.Age > f.Lie + 0.6f))) || f.Empty);
			if (Time.frameCount % 120 != 0)
			{
				return;
			}
			ForgetHanded(unscaledTime);
			Stale.Clear();
			foreach (KeyValuePair<Actor, Snap> snap in Snaps)
			{
				if (snap.Key == null || unscaledTime - snap.Value.At > 10f)
				{
					Stale.Add(snap.Key);
				}
			}
			foreach (Actor item in Stale)
			{
				Snaps.Remove(item);
			}
		}

		public static Fallen Near(Vector2 you, float yaw, float reach, object space = null)
		{
			Fallen result = null;
			float num = reach;
			foreach (Fallen body in Bodies)
			{
				if (body.Space == space)
				{
					Vector2 vector = body.At - you;
					float magnitude = vector.magnitude;
					if (!(magnitude > num) && (!(magnitude > 0.6f) || !(Mathf.Abs(Mathf.DeltaAngle(yaw * 57.29578f, Mathf.Atan2(vector.y, vector.x) * 57.29578f)) > 50f)))
					{
						result = body;
						num = magnitude;
					}
				}
			}
			return result;
		}

		public static bool Take(Actor you, Fallen f, Action<string> toast)
		{
			if (you == null || f == null)
			{
				return false;
			}
			List<string> list = new List<string>();
			if (f.Money > 0)
			{
				try
				{
					you.addMoney(f.Money);
				}
				catch
				{
				}
				list.Add(f.Money + " gold");
				f.Money = 0;
			}
			int num = 0;
			while (num < f.Pieces.Count && Gear.HasRoom(you))
			{
				Gear.Piece p = f.Pieces[num];
				long num2 = f.ItemIds[num];
				bool flag = !Looted.Contains(num2) && OutOfArmory(num2, num < f.Existed.Count && f.Existed[num]);
				if (flag)
				{
					Looted.Add(num2);
				}
				if (flag && Gear.Add(you, p))
				{
					list.Add(Gear.NameOf(p).ToLowerInvariant());
				}
				else if (flag)
				{
					break;
				}
				f.Pieces.RemoveAt(num);
				f.ItemIds.RemoveAt(num);
				if (num < f.Existed.Count)
				{
					f.Existed.RemoveAt(num);
				}
			}
			for (int num3 = f.Bag.Count - 1; num3 >= 0; num3--)
			{
				KeyValuePair<string, int> keyValuePair = f.Bag[num3];
				int num4 = Bag.Put(you, keyValuePair.Key, keyValuePair.Value, spill: false);
				if (num4 > 0)
				{
					list.Add(Work.Amount(keyValuePair.Key, num4));
				}
				if (num4 >= keyValuePair.Value)
				{
					f.Bag.RemoveAt(num3);
				}
				else
				{
					f.Bag[num3] = new KeyValuePair<string, int>(keyValuePair.Key, keyValuePair.Value - num4);
				}
			}
			if (!f.Reported && list.Count > 0)
			{
				f.Reported = true;
				Law.LootSeen(f.Kingdom, f.At, f.Name, toast);
			}
			if (list.Count == 0)
			{
				toast?.Invoke(f.Empty ? (f.Name + " has nothing left.") : ("No room in your " + Bag.Name(you).ToLowerInvariant() + "."));
				return false;
			}
			Sounds.PlayAt("event:/SFX/CIVILIZATIONS/CollectFruits", you.current_position);
			toast?.Invoke("From " + f.Name + ": " + string.Join(", ", list.ToArray()) + (f.Empty ? "." : (". More is left (no room in your " + Bag.Name(you).ToLowerInvariant() + ").")));
			Log.Info("loot: " + WorldBoxMod.Describe(you) + " took from " + f.Name + ": " + string.Join(", ", list.ToArray()));
			return true;
		}

		private static bool OutOfArmory(long id, bool existed)
		{
			try
			{
				Item item = World.world.items.get(id);
				if (item == null)
				{
					return !existed;
				}
				if (item.hasActor())
				{
					return false;
				}
				City city = item.getCity();
				if (city != null)
				{
					city.getEquipmentList(item.getAsset().equipment_type)?.Remove(id);
					item.clearCity();
					city._storage_version++;
				}
				Gear.LetGo(item);
			}
			catch
			{
			}
			return true;
		}

		public static void Clear()
		{
			Snaps.Clear();
			Bodies.Clear();
			Looted.Clear();
			HandedYou.Clear();
		}

		public static void CapLie(Actor a, float seconds)
		{
			long num = 0L;
			try
			{
				num = a.data.id;
			}
			catch
			{
				return;
			}
			foreach (Fallen body in Bodies)
			{
				if (body.Id == num)
				{
					body.Lie = Mathf.Min(body.Lie, seconds);
				}
			}
		}

		internal static void WatchForTest(Actor a)
		{
			if (Snaps.TryGetValue(a, out var value))
			{
				value.At = 0f;
			}
			Watch(a);
		}

		public static void InstallKill()
		{
			if (!_killInstalled)
			{
				MethodInfo method = typeof(Actor).GetMethod("addLoot", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(int) }, null);
				MethodInfo method2 = typeof(Actor).GetMethod("takeAllResources", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(Actor) }, null);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				int num2 = method2?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 85 || num2 != 91)
				{
					throw new InvalidOperationException($"the game's addLoot or takeAllResources has changed ({num} and {num2} bytes of IL, not {85} and {91})");
				}
				_lootAdded = -1;
				Detour.Apply(method, typeof(Loot).GetMethod("AddLoot", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				Detour.Apply(method2, typeof(Loot).GetMethod("TakeAllResources", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_killInstalled = true;
			}
		}

		private static bool Yours(Actor a)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			if (instance != null && a != null)
			{
				if (a != instance.Host)
				{
					return a == instance.Body;
				}
				return true;
			}
			return false;
		}

		private static void AddLoot(Actor self, int pLootValue)
		{
			bool flag = false;
			try
			{
				flag = Yours(self);
			}
			catch
			{
			}
			if (pLootValue == 0)
			{
				if (flag)
				{
					_lootAdded = 0;
				}
				return;
			}
			int loot = self.data.loot;
			self.data.loot += pLootValue;
			self.data.loot = Mathf.Clamp(self.data.loot, 0, 99999);
			if (flag)
			{
				_lootAdded = self.data.loot - loot;
			}
			EffectsLibrary.showMoneyEffect("fx_money_got_loot", self.current_position, self.current_zone, self.actor_scale);
		}

		private static void TakeAllResources(Actor self, Actor pActorTarget)
		{
			int lootAdded = _lootAdded;
			_lootAdded = -1;
			Handed handed = null;
			try
			{
				if (Yours(self) && pActorTarget != null && !WorldSync.Mirroring)
				{
					handed = new Handed
					{
						You = self,
						At = Time.unscaledTime,
						Money = Mathf.Max(0, lootAdded)
					};
				}
			}
			catch
			{
			}
			if (pActorTarget.isCarryingResources())
			{
				foreach (KeyValuePair<string, ResourceContainer> resource in pActorTarget.inventory.getResources())
				{
					if (handed != null && resource.Value.amount > 0)
					{
						handed.Bag.Add(new KeyValuePair<string, int>(resource.Key, resource.Value.amount));
					}
					self.addToInventory(resource.Value);
				}
				pActorTarget.inventory.empty();
			}
			if (handed == null || (handed.Money <= 0 && handed.Bag.Count == 0))
			{
				return;
			}
			try
			{
				HandedYou[pActorTarget.data.id] = handed;
			}
			catch (Exception ex)
			{
				if (!_killReported)
				{
					_killReported = true;
					Log.Error("loot: kill: " + ex.Message);
				}
			}
		}

		private static bool TakeBack(Actor dead, Fallen f)
		{
			long id;
			try
			{
				id = dead.data.id;
			}
			catch
			{
				return false;
			}
			if (!HandedYou.TryGetValue(id, out var value))
			{
				return false;
			}
			HandedYou.Remove(id);
			Actor you = value.You;
			if (Time.unscaledTime - value.At > 3f || you == null)
			{
				return false;
			}
			int num = 0;
			List<string> list = new List<string>();
			try
			{
				if (!you.isAlive())
				{
					return false;
				}
				num = Mathf.Min(value.Money, you.data.loot);
				if (num > 0)
				{
					you.data.loot -= num;
					f.Money += num;
				}
				foreach (KeyValuePair<string, int> kv in value.Bag)
				{
					int num2 = Mathf.Min(kv.Value, Civil.BagCount(you, kv.Key));
					if (num2 > 0 && Civil.TakeFromBag(you, kv.Key, num2))
					{
						int num3 = f.Bag.FindIndex((KeyValuePair<string, int> x) => x.Key == kv.Key);
						if (num3 >= 0)
						{
							f.Bag[num3] = new KeyValuePair<string, int>(kv.Key, f.Bag[num3].Value + num2);
						}
						else
						{
							f.Bag.Add(new KeyValuePair<string, int>(kv.Key, num2));
						}
						list.Add(num2 + " " + kv.Key);
					}
				}
			}
			catch (Exception ex)
			{
				if (!_killReported)
				{
					_killReported = true;
					Log.Error("loot: kill: " + ex.Message);
				}
			}
			if (num <= 0 && list.Count == 0)
			{
				return false;
			}
			KeptOnBody++;
			Log.Info($"loot: your kill of {f.Name}: {num} gold" + ((list.Count > 0) ? (", " + string.Join(", ", list.ToArray())) : "") + " left with the body");
			return true;
		}

		private static void ForgetHanded(float now)
		{
			if (HandedYou.Count == 0)
			{
				return;
			}
			StaleHanded.Clear();
			foreach (KeyValuePair<long, Handed> item in HandedYou)
			{
				if (now - item.Value.At > 10f)
				{
					StaleHanded.Add(item.Key);
				}
			}
			foreach (long item2 in StaleHanded)
			{
				HandedYou.Remove(item2);
			}
		}

		private static void WornNow(Actor a, Snap s)
		{
			long[] ids = s.Ids;
			Gear.Piece[] pieces = s.Pieces;
			List<Item> worn = Worn;
			worn.Clear();
			if (a.hasEquipment())
			{
				foreach (Item item in a.equipment.getItems())
				{
					worn.Add(item);
				}
			}
			bool flag = worn.Count == s.Ids.Length;
			int num = 0;
			while (flag && num < worn.Count)
			{
				flag = worn[num].data.id == s.Ids[num];
				num++;
			}
			if (!flag)
			{
				long[] array = new long[worn.Count];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = worn[i].data.id;
				}
				s.Ids = array;
				Gear.Piece[] array2 = new Gear.Piece[worn.Count];
				for (int j = 0; j < array2.Length; j++)
				{
					array2[j] = Gear.FromItem(worn[j]);
				}
				s.Pieces = array2;
			}
			if (_verify)
			{
				VerifyWorn(a, s, ids, pieces);
			}
			worn.Clear();
		}

		private static void ProbeWorn(Actor a)
		{
			if (!_verify || a == null)
			{
				return;
			}
			try
			{
				if (!Probes.TryGetValue(a, out var value))
				{
					value = (Probes[a] = new Snap());
				}
				WornNow(a, value);
			}
			catch
			{
			}
		}

		internal static void VerifyLean(bool on)
		{
			_verify = on;
			Probes.Clear();
			if (on)
			{
				_compared = (_differ = 0);
				_first = null;
			}
		}

		internal static string LeanVerdict()
		{
			if (_differ > 0)
			{
				return $"{_differ} of {_compared} gear reads differ; first: {_first}";
			}
			if (_compared != 0)
			{
				return null;
			}
			return "no gear read was compared";
		}

		private static void VerifyWorn(Actor a, Snap s, long[] idsBefore, Gear.Piece[] piecesBefore)
		{
			_compared++;
			long[] array = idsBefore;
			Gear.Piece[] array2 = piecesBefore;
			List<Item> list = (a.hasEquipment() ? a.equipment.getItems().ToList() : new List<Item>());
			bool flag = list.Count == array.Length;
			int num = 0;
			while (flag && num < list.Count)
			{
				flag = list[num].data.id == array[num];
				num++;
			}
			if (!flag)
			{
				array = list.Select((Item it) => it.data.id).ToArray();
				array2 = list.Select(Gear.FromItem).ToArray();
			}
			string text = null;
			if (flag != (s.Ids == idsBefore && s.Pieces == piecesBefore))
			{
				text = (flag ? "kept the old way, made again lean" : "made again the old way, kept lean");
			}
			else if (array.Length != s.Ids.Length || array2.Length != s.Pieces.Length)
			{
				text = $"{array.Length} ids, {array2.Length} pieces the old way; {s.Ids.Length}, {s.Pieces.Length} lean";
			}
			int num2 = 0;
			while (text == null && num2 < array.Length)
			{
				if (array[num2] != s.Ids[num2])
				{
					text = $"id {num2}: {array[num2]} the old way, {s.Ids[num2]} lean";
				}
				num2++;
			}
			int num3 = 0;
			while (text == null && num3 < array2.Length)
			{
				if (!SamePiece(array2[num3], s.Pieces[num3]))
				{
					text = $"piece {num3} ({array2[num3]?.Id}) differs";
				}
				num3++;
			}
			if (text != null)
			{
				_differ++;
				if (_first == null)
				{
					_first = text;
				}
			}
		}

		private static bool SamePiece(Gear.Piece x, Gear.Piece y)
		{
			if (x == y)
			{
				return true;
			}
			if (x == null || y == null || x.Id != y.Id || x.By != y.By || x.Name != y.Name || x.Kills != y.Kills || x.Mods.Length != y.Mods.Length)
			{
				return false;
			}
			for (int i = 0; i < x.Mods.Length; i++)
			{
				if (x.Mods[i] != y.Mods[i])
				{
					return false;
				}
			}
			return true;
		}
	}
}
