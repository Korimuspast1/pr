using System;
using System.Collections.Generic;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal static class Bandages
	{
		internal sealed class Heal
		{
			public Actor Who;

			public long Id;

			public Supplies.Kind Kind;

			public int Total;

			public int Given;

			public float Seconds;

			public float Age;
		}

		public static readonly List<Heal> Active = new List<Heal>();

		public const float WrapSeconds = 1.1f;

		public static float WrapAt = -10f;

		public static bool WrapOther;

		public static Actor WrapBy;

		public static bool WrapOver;

		private const float ThanksEvery = 90f;

		private const int Goodwill = 3;

		private static readonly Dictionary<long, float> Thanked = new Dictionary<long, float>();

		private static double _lastWorld = -1.0;

		private static object _world;

		private static string _hintPrompt;

		private static string _hintAdd;

		private static string _hint;

		public static bool Wrapping => Time.unscaledTime - WrapAt < 1.1f;

		public static float WrapProgress
		{
			get
			{
				if (!Wrapping)
				{
					return 0f;
				}
				return Mathf.Clamp01((Time.unscaledTime - WrapAt) / 1.1f);
			}
		}

		public static void Tick()
		{
			object obj = World.world?.map_stats;
			if (obj != _world)
			{
				_world = obj;
				Active.Clear();
				Thanked.Clear();
				_lastWorld = -1.0;
			}
			if (Active.Count == 0)
			{
				_lastWorld = -1.0;
			}
			else
			{
				if (World.world == null)
				{
					return;
				}
				double actionNow = WorldPace.ActionNow;
				float num = ((_lastWorld < 0.0) ? 0f : ((float)(actionNow - _lastWorld)));
				_lastWorld = actionNow;
				if (num < 0f)
				{
					num = 0f;
				}
				for (int num2 = Active.Count - 1; num2 >= 0; num2--)
				{
					Heal heal = Active[num2];
					if (!Alive(heal))
					{
						Active.RemoveAt(num2);
					}
					else
					{
						heal.Age += num;
						int num3 = ((heal.Age >= heal.Seconds) ? heal.Total : Mathf.FloorToInt((float)heal.Total * heal.Age / heal.Seconds));
						int num4 = num3 - heal.Given;
						if (num4 > 0)
						{
							heal.Given = num3;
							try
							{
								heal.Who.restoreHealth(num4);
							}
							catch
							{
							}
						}
						if (heal.Age >= heal.Seconds)
						{
							Active.RemoveAt(num2);
						}
					}
				}
			}
		}

		private static bool Alive(Heal h)
		{
			try
			{
				return h.Who != null && h.Who.isAlive() && h.Who.data != null && h.Who.data.id == h.Id;
			}
			catch
			{
				return false;
			}
		}

		public static bool Healing(Actor a)
		{
			if (a == null || Active.Count == 0)
			{
				return false;
			}
			foreach (Heal item in Active)
			{
				if (item.Who == a && Alive(item))
				{
					return true;
				}
			}
			return false;
		}

		public static int Pending(Actor a)
		{
			if (a == null || Active.Count == 0)
			{
				return 0;
			}
			int num = 0;
			foreach (Heal item in Active)
			{
				if (item.Who == a && Alive(item))
				{
					num += item.Total - item.Given;
				}
			}
			return Mathf.Min(num, Missing(a));
		}

		public static int Timer(Actor a, out float left, out float length)
		{
			left = (length = 0f);
			int num = 0;
			if (a == null)
			{
				return 0;
			}
			foreach (Heal item in Active)
			{
				if (item.Who == a && Alive(item))
				{
					num++;
					float num2 = item.Seconds - item.Age;
					if (num2 > left)
					{
						left = num2;
						length = item.Seconds;
					}
				}
			}
			return num;
		}

		private static int Missing(Actor a)
		{
			try
			{
				return Mathf.Max(0, a.getMaxHealth() - a.getHealth());
			}
			catch
			{
				return 0;
			}
		}

		public static Actor Target(Actor you)
		{
			Actor actor = WorldBoxMod.Instance?.Talk.Candidate;
			if (actor == null || actor == you || !Wants(you, actor))
			{
				return you;
			}
			return actor;
		}

		private static bool Wants(Actor you, Actor a)
		{
			try
			{
				if (a == null || !a.isAlive() || (a.asset != null && a.asset.is_boat))
				{
					return false;
				}
				if (Missing(a) <= Pending(a))
				{
					return false;
				}
				if (Dialogue.Enemies(you, a))
				{
					return false;
				}
				if (a.has_attack_target && a.attack_target == you)
				{
					return false;
				}
				if (you.has_attack_target && you.attack_target == a)
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

		public static Supplies.Kind Best(Actor you, Actor target)
		{
			Supplies.Kind kind = Supplies.Of("tw_item_bandage");
			Supplies.Kind kind2 = Supplies.Of("tw_item_salve");
			bool flag = Supplies.Count(you, kind.Id) > 0;
			bool flag2 = Supplies.Count(you, kind2.Id) > 0;
			float num = 0f;
			try
			{
				num = (float)(Missing(target) - Pending(target)) / (float)Mathf.Max(1, target.getMaxHealth());
			}
			catch
			{
			}
			if (flag2 && (num >= 0.4f || !flag))
			{
				return kind2;
			}
			if (!flag)
			{
				return null;
			}
			return kind;
		}

		public static bool Quick(Actor you, Action<string> toast)
		{
			if (you == null || !Alive(you))
			{
				return false;
			}
			Actor target = Target(you);
			Supplies.Kind kind = Best(you, target);
			if (kind == null)
			{
				toast?.Invoke("No bandages or salves in your bag");
				return false;
			}
			return Use(you, kind, target, toast);
		}

		public static bool Use(Actor you, Supplies.Kind kind, Actor target, Action<string> toast)
		{
			if (you == null || kind == null || target == null || !Alive(you) || !Alive(target))
			{
				return false;
			}
			bool flag = target == you;
			string text = (flag ? null : Dialogue.NameOf(target));
			int num = Missing(target);
			if (num <= 0)
			{
				toast?.Invoke(flag ? "You're not hurt" : (text + " isn't hurt"));
				return false;
			}
			if (Pending(target) >= num)
			{
				toast?.Invoke(flag ? "Your wounds are bound already" : (text + "'s wounds are bound already"));
				return false;
			}
			if (Wrapping)
			{
				return false;
			}
			if (!Supplies.Take(you, kind.Id, 1))
			{
				toast?.Invoke("No " + kind.One + "s in your bag");
				return false;
			}
			WrapOver = Healing(target);
			Start(target, kind);
			WrapAt = Time.unscaledTime;
			WrapOther = !flag;
			WrapBy = you;
			Hotbar.ChangedAt = Time.unscaledTime;
			try
			{
				Sounds.PlayAt("event:/SFX/CIVILIZATIONS/CollectHerbs", target.current_position);
			}
			catch
			{
			}
			WorldBoxMod.Instance?.Rustle();
			string text2 = ((kind.Id == "tw_item_salve") ? "dress" : "bind");
			toast?.Invoke(flag ? ("You " + text2 + " your wounds") : ("You " + text2 + " " + text + "'s wounds"));
			if (!flag)
			{
				Thank(you, target, text);
			}
			Log.Info(string.Format("bandages: {0} used a {1} on {2}, {3} hp missing", WorldBoxMod.Describe(you), kind.One, flag ? "themself" : WorldBoxMod.Describe(target), num));
			return true;
		}

		public static void Start(Actor a, Supplies.Kind kind)
		{
			int num = 1;
			long id = -1L;
			try
			{
				num = Mathf.Max(1, a.getMaxHealth());
				id = a.data.id;
			}
			catch
			{
			}
			Active.Add(new Heal
			{
				Who = a,
				Id = id,
				Kind = kind,
				Total = Mathf.Max(1, Mathf.RoundToInt((float)num * kind.Heal)),
				Seconds = kind.Seconds
			});
			if (Active.Count > 64)
			{
				Active.RemoveAt(0);
			}
		}

		private static void Thank(Actor you, Actor them, string name)
		{
			try
			{
				if (!Dialogue.IsPerson(them))
				{
					return;
				}
				long id = them.data.id;
				float unscaledTime = Time.unscaledTime;
				if (!Thanked.TryGetValue(id, out var value) || !(unscaledTime - value < 90f))
				{
					Thanked[id] = unscaledTime;
					if (Thanked.Count > 200)
					{
						Thanked.Clear();
					}
					Chronicle.ChangeOpinion(them, you, 3);
					if (them.city != null)
					{
						Rep.Add(you, them.city, 8, "bound " + name + "'s wounds");
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("bandages: thanks: " + ex.Message);
			}
		}

		private static bool Alive(Actor a)
		{
			try
			{
				return a?.isAlive() ?? false;
			}
			catch
			{
				return false;
			}
		}

		public static string WithHint(string prompt, Actor you)
		{
			Actor actor = WorldBoxMod.Instance?.Talk.Candidate;
			if (string.IsNullOrEmpty(prompt) || you == null || actor == null || actor == you || !Wants(you, actor) || Best(you, actor) == null)
			{
				return prompt;
			}
			string text = (Supplies.Heals(Supplies.Of(BagOrder.ResourceOf(Hotbar.InHand(you)))) ? "LMB:Bandage" : "Hold Q:Bandage");
			if (_hint == null || !string.Equals(prompt, _hintPrompt, StringComparison.Ordinal) || (object)text != _hintAdd)
			{
				_hintPrompt = prompt;
				_hintAdd = text;
				_hint = prompt + "|" + text;
			}
			return _hint;
		}
	}
}
