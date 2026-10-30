using System;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class CombatFeedback
	{
		public float Hurt;

		public float Shake;

		public float HitMarker;

		public bool HitWasKill;

		public bool HitWasHead;

		public float Kick;

		public float SinceAttack = 99f;

		public int Attacks;

		public int Kills;

		public int Hits;

		public int Hurts;

		public bool LastAttackMelee = true;

		public Swing LastSwing;

		public string Banner;

		public Vector2 HitFrom;

		public float HitFromAge = 99f;

		public float BannerUntil;

		public readonly List<FloatingText> Texts = new List<FloatingText>();

		public readonly List<HurtMark> HurtMarks = new List<HurtMark>();

		private const int MaxHurtMarks = 4;

		public float BlockMarker;

		public Stop BlockKind;

		public float GuardFlash;

		public Stop GuardKind;

		public string GuardWord;

		public float GuardWordAge = 99f;

		public bool GuardWordBroken;

		public float StoppedAt;

		public float StoppedWeight;

		public bool StoppedRings;

		public float SinceClash = 99f;

		public float ClashWeight;

		public bool ClashRings;

		private int _blockSerial;

		private readonly Dictionary<Actor, int> _watched = new Dictionary<Actor, int>();

		private readonly List<Actor> _scratch = new List<Actor>();

		private readonly HashSet<Actor> _struck = new HashSet<Actor>();

		private float _watchLeft;

		private int _hostHealth;

		private int _hostMax;

		private double _lastAttackStamp;

		private bool _swingPending;

		private float _swingPendingFor;

		private static readonly List<KeyValuePair<Actor, int>> Landed = new List<KeyValuePair<Actor, int>>();

		private readonly Dictionary<Actor, int> _landedFrom = new Dictionary<Actor, int>();

		private static readonly HashSet<long> _fightingYou = new HashSet<long>();

		private static readonly Dictionary<long, KeyValuePair<bool, float>> _started = new Dictionary<long, KeyValuePair<bool, float>>();

		private static object _lawWorld;

		private static readonly HashSet<long> _atWar = new HashSet<long>();

		private static readonly Dictionary<long, float> _struckAtYou = new Dictionary<long, float>();

		private static readonly Dictionary<long, bool> _struckFirst = new Dictionary<long, bool>();

		private float _lastHitAt = -10f;

		public static void ShotLanded(Actor victim, int healthBefore)
		{
			if (victim != null && Landed.Count < 64)
			{
				Landed.Add(new KeyValuePair<Actor, int>(victim, healthBefore));
			}
			Law.Striking(victim);
		}

		public static void Struck(Actor victim, int healthBefore)
		{
			ShotLanded(victim, healthBefore);
			Slicing.Struck(victim);
		}

		public static bool TheyStarted(Actor victim, Actor you)
		{
			if (victim == null)
			{
				return false;
			}
			try
			{
				if (_started.TryGetValue(victim.data.id, out var value) && Time.unscaledTime - value.Value < 120f)
				{
					return value.Key;
				}
				return victim.attack_target == you;
			}
			catch
			{
				return false;
			}
		}

		public static void NoteProvoked(Actor victim, Actor you)
		{
			NoteWhoStarted(victim, you);
		}

		private static void NoteWhoStarted(Actor victim, Actor host)
		{
			try
			{
				long id = victim.data.id;
				float unscaledTime = Time.unscaledTime;
				if (_started.TryGetValue(id, out var value) && unscaledTime - value.Value < 60f)
				{
					_started[id] = new KeyValuePair<bool, float>(value.Key, unscaledTime);
					return;
				}
				if (_started.Count > 256)
				{
					_started.Clear();
					_struckFirst.Clear();
					_atWar.Clear();
				}
				bool key = _fightingYou.Contains(id) || (host != null && host.attackedBy == victim) || StruckAtYou(victim);
				_started[id] = new KeyValuePair<bool, float>(key, unscaledTime);
				_struckFirst[id] = StruckAtYou(victim);
				if (host != null && host.kingdom != null && victim.kingdom != null && host.kingdom != victim.kingdom && host.kingdom.isEnemy(victim.kingdom))
				{
					_atWar.Add(id);
				}
				else
				{
					_atWar.Remove(id);
				}
			}
			catch
			{
			}
		}

		public static bool BeganAtWar(Actor v)
		{
			try
			{
				return v != null && _atWar.Contains(v.data.id) && FightingLately(v, 120f);
			}
			catch
			{
				return false;
			}
		}

		public static bool FightingLately(Actor a, float seconds)
		{
			try
			{
				KeyValuePair<bool, float> value;
				return a != null && _started.TryGetValue(a.data.id, out value) && Time.unscaledTime - value.Value < seconds;
			}
			catch
			{
				return false;
			}
		}

		private static void NoteStruckBy(BaseSimObject by)
		{
			try
			{
				if (by is Actor actor)
				{
					if (_struckAtYou.Count > 128)
					{
						_struckAtYou.Clear();
					}
					long id = actor.data.id;
					float unscaledTime = Time.unscaledTime;
					_struckAtYou[id] = unscaledTime;
					if (_started.TryGetValue(id, out var value) && unscaledTime - value.Value < 60f)
					{
						_started[id] = new KeyValuePair<bool, float>(value.Key, unscaledTime);
					}
				}
			}
			catch
			{
			}
		}

		public static bool StruckAtYou(Actor a)
		{
			try
			{
				float value;
				return a != null && _struckAtYou.TryGetValue(a.data.id, out value) && Time.unscaledTime - value < 60f;
			}
			catch
			{
				return false;
			}
		}

		public static bool StruckFirst(Actor victim)
		{
			if (victim == null)
			{
				return false;
			}
			try
			{
				long id = victim.data.id;
				if (_started.TryGetValue(id, out var value) && Time.unscaledTime - value.Value < 120f && _struckFirst.TryGetValue(id, out var value2))
				{
					return value2;
				}
				return StruckAtYou(victim);
			}
			catch
			{
				return false;
			}
		}

		private static void RememberWhoFightsYou(Actor host, List<BaseSimObject> nearby)
		{
			_fightingYou.Clear();
			if (host == null || nearby == null)
			{
				return;
			}
			for (int i = 0; i < nearby.Count; i++)
			{
				try
				{
					if (nearby[i] is Actor actor && actor != host && actor.isAlive() && actor.has_attack_target && actor.attack_target == host)
					{
						_fightingYou.Add(actor.data.id);
					}
				}
				catch
				{
				}
			}
		}

		public void Reset(Actor host)
		{
			_watched.Clear();
			Landed.Clear();
			_struck.Clear();
			Texts.Clear();
			HurtMarks.Clear();
			Hurt = (Shake = (HitMarker = (Kick = 0f)));
			SinceAttack = 99f;
			BlockMarker = (GuardFlash = (StoppedAt = 0f));
			HitWasHead = false;
			GuardWordAge = (SinceClash = 99f);
			Blocks.Clear();
			_blockSerial = Blocks.Newest;
			_swingPending = false;
			Kills = 0;
			Banner = null;
			_hostHealth = host?.getHealth() ?? 0;
			_hostMax = host?.getMaxHealth() ?? 0;
			_lastAttackStamp = host?._possession_attack_happened_frame ?? 0.0;
		}

		public void Update(Actor host, float dt, List<BaseSimObject> nearby, Func<float, float, float> groundAt)
		{
			object obj = World.world?.map_stats;
			if (obj != _lawWorld)
			{
				_lawWorld = obj;
				_started.Clear();
				_struckFirst.Clear();
				_struckAtYou.Clear();
				_atWar.Clear();
			}
			Hurt = Mathf.Max(0f, Hurt - dt * 1.4f);
			Shake = Mathf.Max(0f, Shake - dt * 3f);
			HitMarker = Mathf.Max(0f, HitMarker - dt * 3.5f);
			Kick = Mathf.Max(0f, Kick - dt * 7f);
			BlockMarker = Mathf.Max(0f, BlockMarker - dt * 2.2f);
			GuardFlash = Mathf.Max(0f, GuardFlash - dt * 2.2f);
			GuardWordAge += dt;
			SinceClash += dt;
			SinceAttack += dt;
			_swingPendingFor += dt;
			if (_swingPending && _swingPendingFor > 1.5f)
			{
				_swingPending = false;
			}
			HitFromAge += dt;
			for (int num = Texts.Count - 1; num >= 0; num--)
			{
				Texts[num].Age += dt;
				if (Texts[num].Age >= Texts[num].Life)
				{
					Texts.RemoveAt(num);
				}
			}
			for (int num2 = HurtMarks.Count - 1; num2 >= 0; num2--)
			{
				HurtMarks[num2].Age += dt;
				if (HurtMarks[num2].Age >= 2.2f)
				{
					HurtMarks.RemoveAt(num2);
				}
			}
			Blocks.Host = ((host != null && host.isAlive()) ? host : null);
			if (host == null || !host.isAlive())
			{
				return;
			}
			double possession_attack_happened_frame = host._possession_attack_happened_frame;
			if (possession_attack_happened_frame != _lastAttackStamp)
			{
				_lastAttackStamp = possession_attack_happened_frame;
				OnAttack(host, nearby, !_swingPending);
				_swingPending = false;
			}
			TakeBlocks(host, groundAt);
			if (Landed.Count > 0)
			{
				_landedFrom.Clear();
				foreach (KeyValuePair<Actor, int> item in Landed)
				{
					if (item.Key != host && (!_landedFrom.TryGetValue(item.Key, out var value) || item.Value > value))
					{
						_landedFrom[item.Key] = item.Value;
					}
				}
				foreach (KeyValuePair<Actor, int> item2 in _landedFrom)
				{
					_watched[item2.Key] = item2.Value;
					_struck.Add(item2.Key);
					NoteWhoStarted(item2.Key, host);
				}
				Landed.Clear();
				_watchLeft = Mathf.Max(_watchLeft, 0.6f);
			}
			RememberWhoFightsYou(host, nearby);
			int health = host.getHealth();
			int maxHealth = host.getMaxHealth();
			bool flag = false;
			if (health < _hostHealth)
			{
				bool flag2 = false;
				try
				{
					flag2 = host._last_attack_type == AttackType.Starvation && host.attackedBy == null;
				}
				catch
				{
				}
				bool flag3 = _hostMax > 0 && maxHealth < _hostMax && health >= maxHealth;
				if (flag2)
				{
					Hurt = Mathf.Max(Hurt, 0.15f);
				}
				flag = flag2 || flag3;
			}
			if (health < _hostHealth && !flag)
			{
				float num3 = (float)(_hostHealth - health) / (float)Mathf.Max(1, maxHealth);
				Hurt = Mathf.Min(1f, Hurt + 0.35f + num3 * 3f);
				Hurts++;
				Shake = Mathf.Min(1.5f, Shake + 0.4f + num3 * 4f);
				try
				{
					BaseSimObject attackedBy = host.attackedBy;
					if (attackedBy != null && attackedBy.isAlive())
					{
						HitFrom = attackedBy.current_position - host.current_position;
						HitFromAge = 0f;
						MarkHurt(attackedBy);
						NoteStruckBy(attackedBy);
					}
				}
				catch
				{
				}
			}
			_hostHealth = health;
			_hostMax = maxHealth;
			FollowHurtMarks(host);
			if (_watchLeft <= 0f)
			{
				return;
			}
			_watchLeft -= dt;
			_scratch.Clear();
			_scratch.AddRange(_watched.Keys);
			foreach (Actor item3 in _scratch)
			{
				int num4 = _watched[item3];
				Vector2 current_position = item3.current_position;
				if (!item3.isAlive())
				{
					_watched.Remove(item3);
					if (ByYou(item3, host) || (_struck.Contains(item3) && NoOtherHitter(item3, host)))
					{
						HitMarker = 1f;
						HitWasKill = true;
						HitWasHead = Headshots.Killed(item3);
						Headshots.Take(item3);
						Kills++;
						AddText(current_position, groundAt(current_position.x, current_position.y), "KILL", new Color(1f, 0.25f, 0.2f));
						if (HitWasHead)
						{
							Texts[Texts.Count - 1].Head = true;
						}
						Banner = "You killed " + WorldBoxMod.Describe(item3);
						BannerUntil = Time.unscaledTime + 3f;
						try
						{
							Quests.OnKill(item3);
						}
						catch (Exception ex)
						{
							Log.Error("kill hook: " + ex.Message);
						}
						Hub.OnHitLanded(host, item3, kill: true);
					}
					continue;
				}
				int health2 = item3.getHealth();
				if (health2 >= num4)
				{
					continue;
				}
				_watched[item3] = health2;
				if (!ByYou(item3, host))
				{
					continue;
				}
				_struck.Add(item3);
				HitMarker = 1f;
				if (Time.unscaledTime - _lastHitAt > 0.12f)
				{
					Hits++;
				}
				_lastHitAt = Time.unscaledTime;
				if (health2 > 0)
				{
					try
					{
						Law.OnHit(item3);
					}
					catch (Exception ex2)
					{
						Log.Error("hit hook: " + ex2.Message);
					}
				}
				Hub.OnHitLanded(host, item3, kill: false);
				HitWasKill = false;
				HitWasHead = Headshots.Take(item3);
				AddDamage(item3, current_position, groundAt(current_position.x, current_position.y), num4 - health2, HitWasHead);
			}
			if (_watchLeft <= 0f)
			{
				_watched.Clear();
				_struck.Clear();
			}
		}

		private void MarkHurt(BaseSimObject by, bool blocked = false)
		{
			HurtMark hurtMark = null;
			HurtMark hurtMark2 = null;
			foreach (HurtMark hurtMark3 in HurtMarks)
			{
				if (hurtMark3.From == by)
				{
					hurtMark = hurtMark3;
				}
				if (hurtMark2 == null || hurtMark3.Age > hurtMark2.Age)
				{
					hurtMark2 = hurtMark3;
				}
			}
			if (hurtMark == null)
			{
				if (HurtMarks.Count >= 4)
				{
					HurtMarks.Remove(hurtMark2);
				}
				hurtMark = new HurtMark
				{
					From = by
				};
				HurtMarks.Add(hurtMark);
			}
			hurtMark.At = by.current_position;
			hurtMark.Age = 0f;
			hurtMark.Blocked = blocked;
		}

		private void TakeBlocks(Actor host, Func<float, float, float> groundAt)
		{
			if (Blocks.Newest == _blockSerial)
			{
				return;
			}
			List<BlockEvent> events = Blocks.Events;
			for (int i = 0; i < events.Count; i++)
			{
				BlockEvent blockEvent = events[i];
				if (blockEvent.Serial <= _blockSerial)
				{
					continue;
				}
				_blockSerial = blockEvent.Serial;
				if (blockEvent.Age > 1f)
				{
					continue;
				}
				if (blockEvent.YourBlow && blockEvent.Defender != null)
				{
					BlockMarker = 1f;
					BlockKind = blockEvent.Kind;
					Texts.Add(new FloatingText
					{
						World = new Vector3(blockEvent.At.X, blockEvent.At.Y, blockEvent.At.Z + 0.35f),
						Text = Blocks.Word(blockEvent.Kind),
						Color = WordColor(blockEvent.Kind, yours: false),
						Victim = blockEvent.Defender,
						Word = true,
						Life = 0.95f
					});
					if (!blockEvent.Shot && blockEvent.Kind != Stop.Dodged && LastSwing == Swing.Attack && LastAttackMelee && SinceAttack < 1.5f && StoppedAt <= 0f)
					{
						StoppedAt = Mathf.Max(0.01f, SinceAttack);
						StoppedWeight = blockEvent.Weight;
						StoppedRings = blockEvent.Material == Clash.Metal;
						Shake = Mathf.Max(Shake, 0.22f + 0.3f * blockEvent.Weight);
					}
				}
				else
				{
					if (!blockEvent.OnYou)
					{
						continue;
					}
					GuardFlash = 1f;
					GuardKind = blockEvent.Kind;
					GuardWord = Blocks.Word(blockEvent.Kind);
					GuardWordAge = 0f;
					GuardWordBroken = false;
					try
					{
						if (blockEvent.Attacker != null && blockEvent.Attacker.isAlive())
						{
							MarkHurt(blockEvent.Attacker, blocked: true);
							NoteStruckBy(blockEvent.Attacker);
						}
					}
					catch
					{
					}
					if (blockEvent.Kind != Stop.Dodged)
					{
						SinceClash = 0f;
						ClashWeight = Mathf.Max(0.1f, blockEvent.Weight);
						ClashRings = blockEvent.Material == Clash.Metal;
						Shake = Mathf.Max(Shake, 0.15f + 0.3f * blockEvent.Weight);
					}
				}
			}
		}

		public static Color WordColor(Stop kind, bool yours)
		{
			if (kind == Stop.Dodged)
			{
				if (!yours)
				{
					return new Color(0.86f, 0.88f, 0.9f);
				}
				return new Color(0.78f, 0.96f, 0.86f);
			}
			if (!yours)
			{
				return new Color(0.72f, 0.83f, 0.97f);
			}
			if (kind != Stop.Parried)
			{
				return new Color(0.5f, 0.88f, 1f);
			}
			return new Color(1f, 0.86f, 0.42f);
		}

		private void FollowHurtMarks(Actor host)
		{
			Vector2 current_position = host.current_position;
			foreach (HurtMark hurtMark in HurtMarks)
			{
				if (hurtMark.From != null)
				{
					try
					{
						if (hurtMark.From.isAlive())
						{
							hurtMark.At = hurtMark.From.current_position;
						}
						else
						{
							hurtMark.From = null;
						}
					}
					catch
					{
						hurtMark.From = null;
					}
				}
				Vector2 toward = hurtMark.At - current_position;
				if (toward.sqrMagnitude > 0.0025f)
				{
					hurtMark.Toward = toward;
				}
			}
		}

		private static bool ByYou(Actor a, Actor host)
		{
			try
			{
				return a.attackedBy == host;
			}
			catch
			{
				return false;
			}
		}

		private static bool NoOtherHitter(Actor a, Actor host)
		{
			try
			{
				BaseSimObject attackedBy = a.attackedBy;
				return attackedBy == null || attackedBy == host || !attackedBy.isAlive();
			}
			catch
			{
				return true;
			}
		}

		private void OnAttack(Actor host, List<BaseSimObject> nearby, bool animate)
		{
			if (animate)
			{
				Attacks++;
				SinceAttack = 0f;
				StoppedAt = 0f;
				LastAttackMelee = host.hasMeleeAttack();
				LastSwing = Swing.Attack;
			}
			Kick = (LastAttackMelee ? 0.35f : 1f);
			_watchLeft = (LastAttackMelee ? 0.8f : 2.5f);
			float num = host.getAttackRange() + 1.5f;
			Vector2 current_position = host.current_position;
			_watched.Clear();
			_struck.Clear();
			foreach (BaseSimObject item in nearby)
			{
				if (item is Actor actor && actor != host && actor.isAlive() && (actor.current_position - current_position).sqrMagnitude <= num * num)
				{
					_watched[actor] = actor.getHealth();
				}
			}
		}

		public void SwingStarted(bool melee)
		{
			Attacks++;
			SinceAttack = 0f;
			StoppedAt = 0f;
			LastAttackMelee = melee;
			LastSwing = Swing.Attack;
			_swingPending = true;
			_swingPendingFor = 0f;
		}

		public void WorkSwing(Swing kind)
		{
			Attacks++;
			SinceAttack = 0f;
			StoppedAt = 0f;
			Kick = 0.6f;
			LastAttackMelee = true;
			LastSwing = kind;
		}

		private void AddDamage(Actor victim, Vector2 at, float ground, int amount, bool head = false)
		{
			foreach (FloatingText text in Texts)
			{
				if (text.Victim == victim && !(text.Age > 0.3f) && !text.Word)
				{
					text.Amount += amount;
					text.Text = "-" + text.Amount;
					text.Age = Mathf.Min(text.Age, 0.08f);
					text.Head |= head;
					return;
				}
			}
			at = (RoomCombat.Active ? RoomCombat.ToRoom(at) : at);
			Texts.Add(new FloatingText
			{
				World = new Vector3(at.x, at.y, ground + 1.1f),
				Text = "-" + amount,
				Color = new Color(1f, 0.9f, 0.3f),
				Victim = victim,
				Amount = amount,
				Head = head
			});
		}

		private void AddText(Vector2 at, float ground, string text, Color color)
		{
			at = (RoomCombat.Active ? RoomCombat.ToRoom(at) : at);
			Texts.Add(new FloatingText
			{
				World = new Vector3(at.x, at.y, ground + 1.1f),
				Text = text,
				Color = color
			});
		}
	}
}
