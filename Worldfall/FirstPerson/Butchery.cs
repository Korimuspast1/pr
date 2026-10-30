using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FirstPerson
{
	internal static class Butchery
	{
		internal sealed class Carcass
		{
			public long Id;

			public long Herd;

			public string Kind;

			public string Name;

			public Vector2 At;

			public float Age;

			public float Lie;

			public float LastCut = -9f;

			public bool ByYou;

			public bool Linked;

			public int Total;

			public int Cuts;

			public readonly List<KeyValuePair<string, int>> Parts = new List<KeyValuePair<string, int>>();

			public int Left
			{
				get
				{
					int num = 0;
					foreach (KeyValuePair<string, int> part in Parts)
					{
						num += part.Value;
					}
					return num;
				}
			}

			public int MeatLeft
			{
				get
				{
					foreach (KeyValuePair<string, int> part in Parts)
					{
						if (part.Key == "meat")
						{
							return part.Value;
						}
					}
					return 0;
				}
			}
		}

		private const int GameIlBytes = 80;

		public const float Reach = 2.3f;

		public const float Near = 40f;

		private const float YoursLie = 60f;

		private const float WildLie = 40f;

		private const float SlicedLie = 9f;

		private const float UnseenFor = 1.5f;

		private const int Max = 40;

		public const string HerdKey = "fp_herd";

		public static readonly List<Carcass> All = new List<Carcass>();

		private static bool _installed;

		private static bool _reported;

		private static object _world;

		internal static int Kept;

		internal static int LeftToGame;

		private static int _tickFrame = -1;

		public static bool Installed => _installed;

		public static void Install()
		{
			if (!_installed)
			{
				MethodInfo method = typeof(Actor).GetMethod("pickupResourcesFromKill", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(Actor) }, null);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 80)
				{
					throw new InvalidOperationException("the game's pickupResourcesFromKill has changed (" + num + " bytes of IL, not " + 80 + ")");
				}
				Detour.Apply(method, typeof(Butchery).GetMethod("PickupResourcesFromKill", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
			}
		}

		private static void PickupResourcesFromKill(Actor self, Actor pAttacker)
		{
			try
			{
				if (KeepOnCarcass(self, pAttacker))
				{
					return;
				}
			}
			catch (Exception ex)
			{
				if (!_reported)
				{
					_reported = true;
					Log.Error("butchery: " + ex.Message);
				}
			}
			if (!pAttacker.hasCity())
			{
				return;
			}
			foreach (ResourceContainer item in self.getResourcesFromActor())
			{
				if (!self.isSameSpecies(pAttacker) || pAttacker.hasTrait("savage"))
				{
					pAttacker.addToInventory(item);
				}
			}
			if (Mine(pAttacker))
			{
				LeftToGame++;
			}
		}

		private static bool Mine(Actor a)
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

		public static bool CanCut(Actor you)
		{
			if (you != null)
			{
				return People.IsHumanoid(you);
			}
			return false;
		}

		private static bool KeepOnCarcass(Actor self, Actor killer)
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			Actor actor = instance?.Body ?? instance?.Host;
			if (actor == null || !instance.IsFirstPerson || self == null || killer == null || self.asset == null)
			{
				return false;
			}
			bool flag = Mine(killer);
			if (!flag && killer.hasCity())
			{
				return false;
			}
			if (!self.asset.source_meat || People.IsHumanoid(self))
			{
				return false;
			}
			if (!CanCut(actor))
			{
				return false;
			}
			if ((self.current_position - actor.current_position).sqrMagnitude > 1600f)
			{
				return false;
			}
			if (self.isSameSpecies(killer) && !killer.hasTrait("savage"))
			{
				return false;
			}
			Carcass c = new Carcass
			{
				Id = self.data.id,
				Kind = self.asset.id,
				Name = NameOf(self),
				At = self.current_position,
				ByYou = flag,
				Lie = (flag ? 60f : 40f)
			};
			foreach (ResourceContainer item in self.getResourcesFromActor())
			{
				Add(c.Parts, item.id, item.amount);
			}
			if (c.Parts.Count == 0)
			{
				return false;
			}
			c.Parts.Sort((KeyValuePair<string, int> a, KeyValuePair<string, int> b) => Order(a.Key).CompareTo(Order(b.Key)));
			c.Total = c.Left;
			try
			{
				self.data.get("fp_herd", out var pResult, null);
				if (!string.IsNullOrEmpty(pResult))
				{
					long.TryParse(pResult, out c.Herd);
				}
			}
			catch
			{
			}
			All.RemoveAll((Carcass o) => o.Id == c.Id);
			All.Add(c);
			if (All.Count > 40)
			{
				All.RemoveAt(0);
			}
			Kept++;
			Log.Info("butchery: a " + c.Name + " fell to " + (flag ? "you" : (killer.asset?.id ?? "a beast")) + ": " + Describe(c) + ((c.Herd != 0L) ? " (a hunt's herd)" : ""));
			return true;
		}

		private static void Add(List<KeyValuePair<string, int>> parts, string id, int amount)
		{
			if (string.IsNullOrEmpty(id) || amount <= 0)
			{
				return;
			}
			for (int i = 0; i < parts.Count; i++)
			{
				if (parts[i].Key == id)
				{
					parts[i] = new KeyValuePair<string, int>(id, parts[i].Value + amount);
					return;
				}
			}
			parts.Add(new KeyValuePair<string, int>(id, amount));
		}

		private static int Order(string id)
		{
			if (!(id == "meat"))
			{
				if (!(id == "bones"))
				{
					return 1;
				}
				return 2;
			}
			return 0;
		}

		public static string NameOf(Actor a)
		{
			try
			{
				return a.asset.id.Replace('_', ' ');
			}
			catch
			{
				return "animal";
			}
		}

		public static string Describe(Carcass c)
		{
			List<string> list = new List<string>();
			foreach (KeyValuePair<string, int> part in c.Parts)
			{
				list.Add(part.Value + " " + Work.ResourceName(part.Key).ToLowerInvariant());
			}
			if (list.Count != 0)
			{
				return string.Join(", ", list.ToArray());
			}
			return "nothing left";
		}

		public static float Fell(Actor a, bool sliced)
		{
			long id;
			try
			{
				id = a.data.id;
			}
			catch
			{
				return 0f;
			}
			foreach (Carcass item in All)
			{
				if (item.Id == id && !item.Linked)
				{
					item.Linked = true;
					if (sliced)
					{
						item.Lie = Mathf.Min(item.Lie, 9f);
					}
					return item.Lie;
				}
			}
			return 0f;
		}

		public static float Remaining(long id, out float sinceCut)
		{
			sinceCut = 99f;
			if (id == 0L)
			{
				return -1f;
			}
			foreach (Carcass item in All)
			{
				if (item.Id == id)
				{
					sinceCut = item.Age - item.LastCut;
					return (item.Total > 0) ? ((float)item.Left / (float)item.Total) : 0f;
				}
			}
			return 0f;
		}

		public static void Tick(float dt)
		{
			if (Time.frameCount == _tickFrame)
			{
				return;
			}
			_tickFrame = Time.frameCount;
			object obj = World.world?.map_stats;
			if (obj != _world)
			{
				_world = obj;
				All.Clear();
			}
			for (int num = All.Count - 1; num >= 0; num--)
			{
				Carcass carcass = All[num];
				carcass.Age += dt;
				if (carcass.Age > carcass.Lie + 0.6f || (carcass.Left == 0 && carcass.Age - carcass.LastCut > 2f) || (!carcass.Linked && !carcass.ByYou && carcass.Age > 1.5f))
				{
					All.RemoveAt(num);
				}
			}
		}

		public static void Clear()
		{
			All.Clear();
		}

		public static Carcass Aimed(Vector2 eye, Vector2 flat, float reach)
		{
			Carcass result = null;
			float num = reach;
			foreach (Carcass item in All)
			{
				if (item.Left > 0)
				{
					Vector2 to = item.At - eye;
					float magnitude = to.magnitude;
					if (!(magnitude > num) && (!(magnitude > 0.5f) || !(Vector2.Angle(flat, to) > 50f)))
					{
						result = item;
						num = magnitude;
					}
				}
			}
			return result;
		}

		public static bool Blade(Actor you)
		{
			try
			{
				if (WorldBoxMod.Instance != null && WorldBoxMod.Instance.Holstered)
				{
					return false;
				}
				string text = you.getWeapon()?.asset?.id ?? "";
				return text.StartsWith("sword", StringComparison.Ordinal) || text.StartsWith("axe", StringComparison.Ordinal) || text.StartsWith("dagger", StringComparison.Ordinal);
			}
			catch
			{
				return false;
			}
		}

		public static int Cut(Actor you, Carcass c, Action<string> toast)
		{
			if (you == null || c == null || c.Left <= 0)
			{
				return 0;
			}
			int num = ((!Blade(you)) ? 1 : 2);
			int num2 = 0;
			int num3 = 0;
			bool flag = false;
			List<string> list = new List<string>();
			try
			{
				WorldBoxMod instance = WorldBoxMod.Instance;
				instance?.Work.YieldFrom(new Vector3(c.At.x, c.At.y, instance.GroundAt(c.At) + 0.25f));
			}
			catch
			{
			}
			while (num2 < num && c.Parts.Count > 0)
			{
				KeyValuePair<string, int> keyValuePair = c.Parts[0];
				if (Bag.Put(you, keyValuePair.Key, 1, spill: false) <= 0)
				{
					flag = true;
					break;
				}
				num2++;
				if (keyValuePair.Key == "meat")
				{
					num3++;
				}
				list.Add(keyValuePair.Key);
				if (keyValuePair.Value <= 1)
				{
					c.Parts.RemoveAt(0);
				}
				else
				{
					c.Parts[0] = new KeyValuePair<string, int>(keyValuePair.Key, keyValuePair.Value - 1);
				}
			}
			c.Cuts++;
			c.LastCut = c.Age;
			if (num2 > 0)
			{
				Sounds.PlayAt("event:/SFX/HIT/HitFlesh", c.At);
				if (num3 > 0)
				{
					Quests.OnMeatCut(you, c.Herd, num3);
				}
				if (c.Left == 0)
				{
					toast?.Invoke("Nothing more on the " + c.Name + ".");
				}
				Log.Info("butchery: cut " + string.Join(", ", list.ToArray()) + " from the " + c.Name + " (" + Describe(c) + ")");
			}
			if (flag)
			{
				toast?.Invoke("No room in your " + Bag.Name(you).ToLowerInvariant() + ". The rest stays on the " + c.Name + ".");
			}
			return num2;
		}

		public static string PromptFor(Carcass c)
		{
			return "LMB:Butcher the " + c.Name + " (" + Describe(c) + ")";
		}

		public static int HerdMeatLying(long herd)
		{
			int num = 0;
			if (herd == 0L)
			{
				return 0;
			}
			foreach (Carcass item in All)
			{
				if (item.Herd == herd)
				{
					num += item.MeatLeft;
				}
			}
			return num;
		}

		internal static Carcass Of(long id)
		{
			foreach (Carcass item in All)
			{
				if (item.Id == id)
				{
					return item;
				}
			}
			return null;
		}
	}
}
