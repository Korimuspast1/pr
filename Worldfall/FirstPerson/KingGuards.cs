using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class KingGuards
	{
		internal enum Kind : byte
		{
			Kill,
			Execute
		}

		private enum Stage : byte
		{
			Coming,
			Kneeling
		}

		private sealed class Order
		{
			public Actor Target;

			public long TargetId;

			public Kind What;

			public Stage Now;

			public readonly List<Actor> Guards = new List<Actor>();

			public Kingdom By;

			public Kingdom Theirs;

			public bool Person;

			public bool Yields;

			public bool Foreign;

			public bool Swung;

			public string Name;

			public float Since;

			public float StageAt = -1f;

			public float Next;

			public float FillAt;

			public float KneelFrom = -1f;

			public float KneelHeading;

			public float BlowAt = -1f;

			public Actor Headsman;
		}

		private const float Reach = 25f;

		private const float SightReach = 60f;

		private const int Most = 3;

		private const float GiveUpAfter = 90f;

		private const float SeizeReach = 1.8f;

		private const float SlainGrudge = 12f;

		private const float SlainLordGrudge = 30f;

		private static readonly List<Order> Orders = new List<Order>();

		private static readonly List<Actor> Candidates = new List<Actor>();

		private static readonly HashSet<Actor> Busy = new HashSet<Actor>();

		private static readonly System.Random Rng = new System.Random();

		internal static string LastEndForTest;

		internal static int CountForTest => Orders.Count;

		public static string Refusal(Actor host, Actor t)
		{
			try
			{
				if (!Royal.IsKing(host) || t == null || t == host || !t.isAlive())
				{
					return "";
				}
				if (t.asset != null && t.asset.is_boat)
				{
					return "";
				}
				if (Dialogue.IsPerson(t) && !t.isAdult())
				{
					return "";
				}
				if ((t.current_position - host.current_position).sqrMagnitude > 3600f)
				{
					return "";
				}
				if (On(t) != null)
				{
					return "Your guards are on it";
				}
				if (t.is_inside_building || t.is_inside_boat)
				{
					return "They are inside";
				}
				return null;
			}
			catch
			{
				return "";
			}
		}

		public static bool CanExecute(Actor t)
		{
			if (t != null)
			{
				return Dialogue.IsPerson(t);
			}
			return false;
		}

		public static bool Give(Actor host, Actor t, Kind what, Action<string> toast)
		{
			string text = Refusal(host, t);
			if (text != null)
			{
				if (text.Length > 0)
				{
					toast?.Invoke(text);
				}
				return false;
			}
			bool flag = Dialogue.IsPerson(t);
			if (!flag)
			{
				what = Kind.Kill;
			}
			Order order = new Order
			{
				Target = t,
				TargetId = t.data.id,
				What = what,
				By = host.kingdom,
				Person = flag,
				Name = NameOf(t),
				Since = Time.unscaledTime
			};
			Fill(order, host);
			if (order.Guards.Count == 0)
			{
				toast?.Invoke("No guards near " + order.Name);
				return false;
			}
			try
			{
				Kingdom kingdom = t.kingdom;
				if (flag && kingdom != null && kingdom != order.By && kingdom.isCiv() && !Royal.AtWar(order.By, kingdom))
				{
					order.Foreign = true;
					order.Theirs = kingdom;
				}
			}
			catch
			{
			}
			order.Yields = what == Kind.Execute || (flag && Rng.NextDouble() < 0.5);
			Orders.Add(order);
			int count = order.Guards.Count;
			string text2 = ((count == 1) ? "A guard goes" : (count + " guards go"));
			toast?.Invoke((what == Kind.Execute) ? ("Guards seize " + order.Name + ". The execution is where they stand") : (flag ? (text2 + " to kill " + order.Name) : (text2 + " after " + order.Name)));
			Log.Info(string.Format("king's guards: {0} {1} by {2} ({3}{4})", what, WorldBoxMod.Describe(t), count, order.Yields ? "gives up" : "resists", order.Foreign ? ", foreign" : ""));
			return true;
		}

		private static string NameOf(Actor a)
		{
			try
			{
				if (Dialogue.IsPerson(a))
				{
					return Renames.Apply(a.getName());
				}
				return "the " + ((a.asset != null) ? a.asset.id.Replace('_', ' ') : "creature");
			}
			catch
			{
				return "them";
			}
		}

		private static Order On(Actor t)
		{
			foreach (Order order in Orders)
			{
				if (order.Target == t)
				{
					return order;
				}
			}
			return null;
		}

		private static bool Answering(Actor a)
		{
			foreach (Order order in Orders)
			{
				if (order.Guards.Contains(a))
				{
					return true;
				}
			}
			return false;
		}

		private static void Fill(Order o, Actor host)
		{
			if (o.Guards.Count >= 3 || o.By == null)
			{
				return;
			}
			Vector2 at = o.Target.current_position;
			Candidates.Clear();
			try
			{
				foreach (Actor unit in o.By.getUnits())
				{
					if (unit != null && unit != o.Target && unit != host && unit.isAlive() && unit.ai != null && unit.isWarrior() && unit.isAdult() && !unit.is_inside_building && !unit.is_inside_boat && !((BaseSimObject)unit).hasStatus("possessed") && !o.Guards.Contains(unit) && !Answering(unit) && !((unit.current_position - at).sqrMagnitude > 625f))
					{
						Candidates.Add(unit);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("king's guards: picking: " + ex.Message);
			}
			WorldBoxMod instance = WorldBoxMod.Instance;
			Busy.Clear();
			if (instance != null)
			{
				foreach (Actor candidate in Candidates)
				{
					if (instance.WarMap.CommandOf(candidate) != null || (instance.Royal.Active != null && instance.Royal.Active.Troops.Contains(candidate)))
					{
						Busy.Add(candidate);
					}
				}
			}
			Candidates.Sort(delegate(Actor a, Actor b)
			{
				int num2 = (Busy.Contains(a) ? 1 : 0);
				int num3 = (Busy.Contains(b) ? 1 : 0);
				return (num2 != num3) ? num2.CompareTo(num3) : (a.current_position - at).sqrMagnitude.CompareTo((b.current_position - at).sqrMagnitude);
			});
			for (int num = 0; num < Candidates.Count; num++)
			{
				if (o.Guards.Count >= 3)
				{
					break;
				}
				Actor actor = Candidates[num];
				if (Busy.Contains(actor) && instance != null)
				{
					instance.WarMap.TakeOffFlag(actor);
					instance.Royal.LeaveCampaign(actor);
				}
				o.Guards.Add(actor);
			}
			Candidates.Clear();
			Busy.Clear();
		}

		public static void Update(Actor host, Action<string> toast, FirstPersonAudio audio)
		{
			if (Orders.Count == 0)
			{
				return;
			}
			float unscaledTime = Time.unscaledTime;
			bool paused = false;
			try
			{
				paused = Config.paused;
			}
			catch
			{
			}
			for (int num = Orders.Count - 1; num >= 0; num--)
			{
				Order o = Orders[num];
				try
				{
					if (!Step(o, host, unscaledTime, paused, toast, audio))
					{
						Release(o);
						Orders.RemoveAt(num);
					}
				}
				catch (Exception ex)
				{
					Log.Error("king's guards: " + ex.Message);
					Release(o);
					Orders.RemoveAt(num);
				}
			}
		}

		private static bool Step(Order o, Actor host, float now, bool paused, Action<string> toast, FirstPersonAudio audio)
		{
			Actor target = o.Target;
			bool flag;
			try
			{
				flag = Royal.IsKing(host) && host.kingdom == o.By;
			}
			catch
			{
				flag = false;
			}
			if (!flag)
			{
				return false;
			}
			bool flag2;
			try
			{
				flag2 = target != null && target.isAlive() && target.data.id == o.TargetId;
			}
			catch
			{
				flag2 = false;
			}
			if (!flag2)
			{
				Dead(o, toast);
				return false;
			}
			if (paused)
			{
				float unscaledDeltaTime = Time.unscaledDeltaTime;
				o.Since += unscaledDeltaTime;
				if (o.StageAt >= 0f)
				{
					o.StageAt += unscaledDeltaTime;
				}
				if (o.KneelFrom >= 0f)
				{
					o.KneelFrom += unscaledDeltaTime;
				}
				return true;
			}
			if (o.Now == Stage.Coming && now - o.Since > 90f)
			{
				toast?.Invoke("The guards lost " + o.Name);
				LastEndForTest = "lost";
				return false;
			}
			for (int num = o.Guards.Count - 1; num >= 0; num--)
			{
				bool flag3;
				try
				{
					flag3 = o.Guards[num] != null && o.Guards[num].isAlive() && o.Guards[num].kingdom == o.By;
				}
				catch
				{
					flag3 = false;
				}
				if (!flag3)
				{
					o.Guards.RemoveAt(num);
				}
			}
			if (o.Now == Stage.Coming && o.Guards.Count < 3 && now >= o.FillAt)
			{
				o.FillAt = now + 2f;
				Fill(o, host);
			}
			if (o.Guards.Count == 0)
			{
				toast?.Invoke("No guards left to take " + o.Name);
				LastEndForTest = "no guards";
				return false;
			}
			if (o.Now == Stage.Kneeling)
			{
				return Kneeling(o, now, toast, audio, host);
			}
			if (now < o.Next)
			{
				return true;
			}
			o.Next = now + 0.4f;
			if (o.What == Kind.Kill)
			{
				Kill(o);
			}
			else
			{
				Seize(o, now, toast, audio, host);
			}
			return true;
		}

		private static void Kill(Order o)
		{
			Actor target = o.Target;
			foreach (Actor guard in o.Guards)
			{
				Attack(guard, target);
			}
			if (!o.Person)
			{
				return;
			}
			float dist;
			Actor actor = Nearest(o, out dist);
			if (o.Yields)
			{
				Hold(target);
				if (o.KneelFrom < 0f && dist < 6f)
				{
					o.KneelFrom = Time.unscaledTime;
					o.KneelHeading = Toward(target, actor);
				}
				else if (o.KneelFrom >= 0f && actor != null)
				{
					o.KneelHeading = Toward(target, actor);
				}
			}
			else
			{
				if (actor == null)
				{
					return;
				}
				try
				{
					if (!target.has_attack_target || !(target.attack_target is Actor item) || !o.Guards.Contains(item))
					{
						target.addAggro(actor);
						target.startFightingWith(actor);
					}
				}
				catch
				{
				}
			}
		}

		private static void Seize(Order o, float now, Action<string> toast, FirstPersonAudio audio, Actor host)
		{
			Actor target = o.Target;
			Hold(target);
			WorldTile worldTile = TileOf(target);
			foreach (Actor guard in o.Guards)
			{
				if ((guard.current_position - target.current_position).magnitude <= 1.8f)
				{
					Hold(guard);
				}
				else
				{
					if (Royal.IsHolding(guard) && guard.beh_tile_target != null && worldTile != null && Mathf.Abs(guard.beh_tile_target.x - worldTile.x) + Mathf.Abs(guard.beh_tile_target.y - worldTile.y) <= 1)
					{
						continue;
					}
					try
					{
						if (guard.has_attack_target)
						{
							guard.clearAttackTarget();
						}
					}
					catch
					{
					}
					Royal.Hold(guard, worldTile, 1.2f);
				}
			}
			float dist;
			Actor actor = Nearest(o, out dist);
			if (actor != null && !(dist > 1.8f))
			{
				o.Now = Stage.Kneeling;
				o.StageAt = now;
				o.KneelFrom = now;
				o.Headsman = actor;
				o.KneelHeading = Toward(target, actor);
				Hold(actor);
				Vector2 you = host?.current_position ?? target.current_position;
				audio?.PlayFrom("exec_drum", 0.9f, target.current_position, you, 45f);
				toast?.Invoke(NameOf(actor) + ": \"By the king's word.\"");
				Log.Info("king's guards: " + WorldBoxMod.Describe(target) + " kneels before " + WorldBoxMod.Describe(actor));
			}
		}

		private static bool Kneeling(Order o, float now, Action<string> toast, FirstPersonAudio audio, Actor host)
		{
			Actor target = o.Target;
			Actor headsman = o.Headsman;
			bool flag;
			try
			{
				flag = headsman != null && headsman.isAlive() && o.Guards.Contains(headsman);
			}
			catch
			{
				flag = false;
			}
			if (!flag)
			{
				o.Now = Stage.Coming;
				o.KneelFrom = -1f;
				o.Swung = false;
				o.BlowAt = -1f;
				o.Headsman = null;
				return true;
			}
			float num = now - o.StageAt;
			if (now >= o.Next)
			{
				o.Next = now + 0.4f;
				Hold(target);
				Hold(headsman);
				Watchers(target);
			}
			Attention.Watch(headsman, target.current_position, 0.5f);
			if (!o.Swung && num >= 2.2f)
			{
				o.Swung = true;
				try
				{
					headsman.last_attack_timestamp = World.world.getCurWorldTime();
				}
				catch
				{
				}
			}
			if (num < 2.5f)
			{
				return true;
			}
			if (o.BlowAt >= 0f)
			{
				if (now - o.BlowAt < 1.5f)
				{
					return true;
				}
				toast?.Invoke(o.Name + " survives the blow");
				LastEndForTest = "survived";
				return false;
			}
			o.BlowAt = now;
			Vector2 toward = target.current_position - headsman.current_position;
			if (toward.sqrMagnitude < 0.0001f)
			{
				toward = Vector2.right;
			}
			Slicing.MarkCut(target, toward, (Rng.Next(2) == 0) ? 1 : (-1));
			try
			{
				((BaseSimObject)target).getHit((float)target.getMaxHealth() * 10f + 1000f, pFlash: true, AttackType.Weapon, (BaseSimObject)headsman, pSkipIfShake: false, pMetallicWeapon: true, pCheckDamageReduction: false);
			}
			catch (Exception ex)
			{
				Log.Error("king's guards: the blow: " + ex.Message);
			}
			bool flag2;
			try
			{
				flag2 = !target.isAlive();
			}
			catch
			{
				flag2 = true;
			}
			if (!flag2)
			{
				try
				{
					((BaseSimObject)target).getHitFullHealth(AttackType.Weapon);
				}
				catch
				{
				}
			}
			return true;
		}

		private static void Dead(Order o, Action<string> toast)
		{
			string text = ((o.Now == Stage.Kneeling) ? (o.Name + " is executed") : (o.Person ? (o.Name + " is dead") : ("The guards killed " + o.Name)));
			LastEndForTest = ((o.Now == Stage.Kneeling) ? "executed" : "dead");
			if (o.Foreign && o.Theirs != null && o.By != null)
			{
				try
				{
					bool flag = false;
					try
					{
						flag = o.Target.isKing() || o.Target.isCityLeader();
					}
					catch
					{
					}
					float num = Peace.Offend(o.Theirs, o.By, flag ? 30f : 12f);
					string text2 = Royal.NameOf(o.Theirs);
					if (o.Theirs.isAlive() && !Royal.AtWar(o.Theirs, o.By) && !Royal.Allied(o.Theirs, o.By) && num >= 45f)
					{
						World.world.diplomacy.startWar(o.Theirs, o.By, WarTypeLibrary.normal, pLog: false);
						text = text + ". " + text2 + " declares war on you over its slain";
						Log.Info($"king's guards: {text2} goes to war over its slain (grudge {num:F0})");
					}
					else
					{
						text = text + ". " + text2 + " holds it against you";
					}
				}
				catch (Exception ex)
				{
					Log.Error("king's guards: grudge: " + ex.Message);
				}
			}
			toast?.Invoke(text);
			Log.Info("king's guards: " + text);
		}

		private static void Release(Order o)
		{
			long targetId = o.TargetId;
			foreach (Actor guard in o.Guards)
			{
				try
				{
					if (guard == null || !guard.isAlive())
					{
						continue;
					}
					if (guard._aggression_targets != null)
					{
						guard._aggression_targets.Remove(targetId);
						if (guard._aggression_targets.Count == 0)
						{
							guard.finishStatusEffect("angry");
						}
					}
					if (guard.has_attack_target && guard.attack_target == o.Target)
					{
						guard.clearAttackTarget();
					}
					if (Royal.IsHolding(guard))
					{
						guard.cancelAllBeh();
						guard.beh_tile_target = null;
					}
				}
				catch
				{
				}
			}
			o.Guards.Clear();
			o.KneelFrom = -1f;
		}

		private static void Attack(Actor g, Actor target)
		{
			try
			{
				if (!g.has_attack_target || g.attack_target != target)
				{
					if (Royal.IsHolding(g) || Royal.IsMarchingTo(g, out var _))
					{
						g.cancelAllBeh();
						g.beh_tile_target = null;
					}
					g.addAggro(target);
					g.startFightingWith(target);
				}
			}
			catch (Exception ex)
			{
				Log.Error("king's guards: attack: " + ex.Message);
			}
		}

		private static void Hold(Actor a)
		{
			try
			{
				a.cancelAllBeh();
				a.stopMovement();
				a.makeWait(1f);
			}
			catch
			{
			}
		}

		private static Actor Nearest(Order o, out float dist)
		{
			Actor result = null;
			dist = float.MaxValue;
			Vector2 current_position = o.Target.current_position;
			foreach (Actor guard in o.Guards)
			{
				float magnitude = (guard.current_position - current_position).magnitude;
				if (magnitude < dist)
				{
					dist = magnitude;
					result = guard;
				}
			}
			return result;
		}

		private static float Toward(Actor from, Actor to)
		{
			if (from == null || to == null)
			{
				return 0f;
			}
			Vector2 vector = to.current_position - from.current_position;
			if (!(vector.sqrMagnitude < 1E-06f))
			{
				return Mathf.Atan2(vector.y, vector.x);
			}
			return 0f;
		}

		private static WorldTile TileOf(Actor a)
		{
			try
			{
				return World.world.GetTile(Mathf.FloorToInt(a.current_position.x), Mathf.FloorToInt(a.current_position.y));
			}
			catch
			{
				return null;
			}
		}

		private static void Watchers(Actor t)
		{
			Vector2 current_position = t.current_position;
			int num = 0;
			try
			{
				Kingdom kingdom = t.kingdom;
				if (kingdom == null)
				{
					return;
				}
				foreach (Actor unit in kingdom.getUnits())
				{
					if (unit != null && unit != t && unit.isAlive() && !unit.is_inside_building && !((unit.current_position - current_position).sqrMagnitude > 196f))
					{
						Attention.Watch(unit, current_position, 1f);
						if (++num >= 40)
						{
							break;
						}
					}
				}
			}
			catch
			{
			}
		}

		public static bool Ordered(Actor attacker, Actor victim)
		{
			if (Orders.Count == 0 || attacker == null || victim == null)
			{
				return false;
			}
			foreach (Order order in Orders)
			{
				if (order.Target == victim && order.Guards.Contains(attacker))
				{
					return true;
				}
			}
			return false;
		}

		public static bool Kneels(Actor a, out RestPose pose)
		{
			pose = default(RestPose);
			if (Orders.Count == 0 || a == null)
			{
				return false;
			}
			foreach (Order order in Orders)
			{
				if (order.Target == a && !(order.KneelFrom < 0f))
				{
					float amount = Mathf.Clamp01((Time.unscaledTime - order.KneelFrom) / 0.7f);
					pose = RestPose.Of(RestStyle.KneelBoth, amount, order.KneelHeading);
					return true;
				}
			}
			return false;
		}

		public static void Clear()
		{
			foreach (Order order in Orders)
			{
				Release(order);
			}
			Orders.Clear();
		}
	}
}
