using System;
using System.Collections.Generic;
using System.Reflection;
using FirstPerson.Net;
using UnityEngine;

namespace FirstPerson
{
	internal static class FriendlyFire
	{
		private static BaseSimObject _meant;

		private static bool _meantTried;

		internal static int Spared;

		internal static int Refused;

		internal static BaseSimObject LastRefused;

		private const float BattleRange = 20f;

		private static float _battleAt = -10f;

		private static Actor _battleFor;

		private static bool _battle;

		private static bool _reported;

		private const int SpecialAttackIlBytes = 332;

		internal static int FullBlows;

		internal static List<KeyValuePair<BaseSimObject, string>> Trace;

		internal static List<string> TraceOnYou;

		internal static int Bystanders;

		private const float FightRange = 12f;

		private static float _fightAt = -10f;

		private static Actor _fightFor;

		private static bool _fight;

		internal static BaseSimObject Meant
		{
			get
			{
				return _meant;
			}
			set
			{
				_meant = value;
				_meantTried = false;
			}
		}

		internal static bool Allowed
		{
			get
			{
				WorldBoxMod instance = WorldBoxMod.Instance;
				if ((object)instance == null)
				{
					return false;
				}
				return instance.Settings?.FriendlyFire == true;
			}
		}

		public static void Install(Action<Action, string> optional)
		{
			optional(delegate
			{
				Detour.Apply(typeof(MapBox).GetMethod("checkAttackFor", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), typeof(FriendlyFire).GetMethod("CheckAttackFor", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			}, "could not keep your blows and shots off your own side");
			optional(delegate
			{
				MethodInfo method = typeof(Actor).GetMethod("checkSpecialAttackLogic", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 332)
				{
					throw new InvalidOperationException("the game's checkSpecialAttackLogic has changed (" + num + " bytes of IL, not " + 332 + ")");
				}
				Detour.Apply(method, typeof(FriendlyFire).GetMethod("SpecialAttackLogic", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			}, "could not let your blows kill someone who is not at war with you (they lose the fight instead)");
		}

		private static void SpecialAttackLogic(Actor self, Actor pAttacker, AttackType pAttackType, float pInitialDamage, ref float pDamageFinal)
		{
			pDamageFinal = pInitialDamage;
			if (KingGuards.Ordered(pAttacker, self))
			{
				return;
			}
			bool flag = self.isSameKingdom(pAttacker);
			bool flag2 = ((BaseSimObject)pAttacker).hasStatus("tantrum") && !flag;
			bool flag3 = ((BaseSimObject)pAttacker).hasStatus("possessed");
			bool flag4 = false;
			if (!flag3 || self == pAttacker || !PossessionCombat.Yours(pAttacker) || PossessionCombat.Yours(self))
			{
				bool num = self.kingdom.isEnemy(pAttacker.kingdom);
				float pVal = 0.1f;
				if (self._has_status_possessed || flag3)
				{
					pVal = 0.7f;
				}
				else if (flag)
				{
					pVal = 0.5f;
				}
				if (num)
				{
					pVal = 0f;
				}
				bool flag5 = Randy.randomChance(pVal);
				if (flag2)
				{
					flag5 = true;
				}
				if (self.getHealthRatio() < 0.5f && flag5)
				{
					pDamageFinal = 1f;
					self.makeStunned();
					self.changeHappiness("lost_fight");
					self.finishAngryStatus();
					flag4 = true;
					if (flag2)
					{
						pAttacker.finishStatusEffect("tantrum");
					}
					if (Randy.randomChance(0.4f))
					{
						pAttacker.finishAngryStatus();
					}
				}
			}
			else if (self.getHealthRatio() < 0.5f && self.kingdom != null && !self.kingdom.isEnemy(pAttacker.kingdom))
			{
				FullBlows++;
			}
			bool flag6 = false;
			if (flag && pAttackType != AttackType.Eaten)
			{
				if (Randy.randomChance(0.3f) || flag3 || ((BaseSimObject)pAttacker).hasStatus("angry"))
				{
					self.checkAggroAgainst(pAttacker, flag3);
					flag6 = true;
				}
				if (flag4)
				{
					pDamageFinal = 0f;
					pAttacker.clearAttackTarget();
					pAttacker.makeWait(0.3f);
					if (((BaseSimObject)pAttacker).hasStatus("angry"))
					{
						pAttacker.finishAngryStatus();
					}
				}
			}
			if (!flag6 && flag3)
			{
				self.checkAggroAgainst(pAttacker, pCheckAllLists: false);
			}
		}

		private static AttackDataResult CheckAttackFor(AttackData pData, BaseSimObject pTargetToCheck)
		{
			if (pTargetToCheck.isRekt())
			{
				return AttackDataResult.Continue;
			}
			BaseSimObject initiator = pData.initiator;
			if (initiator.isRekt())
			{
				return AttackDataResult.Continue;
			}
			if (pTargetToCheck == initiator)
			{
				return AttackDataResult.Continue;
			}
			bool flag = initiator.isActor() && initiator.a._has_status_possessed && (PossessionCombat.Yours(initiator.a) || WorldSync.HostStrikesFor(initiator.a));
			if (flag && !pData.is_projectile && pData.target == null && _meant != null && _meant != initiator && _meant.isActor())
			{
				if (pTargetToCheck == _meant)
				{
					if (_meantTried)
					{
						return AttackDataResult.Continue;
					}
					_meantTried = true;
				}
				else if (!_meantTried)
				{
					_meantTried = true;
					if (!_meant.isRekt())
					{
						AttackDataResult result = Reach(pData, _meant, initiator, mine: true);
						if (result.state != ApplyAttackState.Continue && result.state != ApplyAttackState.Miss)
						{
							return result;
						}
					}
				}
			}
			return Reach(pData, pTargetToCheck, initiator, flag);
		}

		private static AttackDataResult Reach(AttackData pData, BaseSimObject pTargetToCheck, BaseSimObject by, bool mine)
		{
			if (RoomCombat.Judge(by, pTargetToCheck, !mine, out var allowed))
			{
				if (!allowed)
				{
					if (mine)
					{
						Refused++;
						LastRefused = pTargetToCheck;
						Traced(pTargetToCheck, "refused in the room");
					}
					OnYou(by, pTargetToCheck, "walls");
					return AttackDataResult.Continue;
				}
			}
			else if (!by.canAttackTarget(pTargetToCheck, !mine, pAttackBuildings: true))
			{
				if (mine)
				{
					Refused++;
					LastRefused = pTargetToCheck;
					Traced(pTargetToCheck, "refused");
				}
				OnYou(by, pTargetToCheck, "refused");
				return AttackDataResult.Continue;
			}
			if (mine && Spare(by.a, pTargetToCheck))
			{
				Traced(pTargetToCheck, "spared");
				return AttackDataResult.Continue;
			}
			if (mine && pTargetToCheck.isActor() && Bystander(by.a, pTargetToCheck.a, _meant, pData.is_projectile))
			{
				Bystanders++;
				Traced(pTargetToCheck, "passed over");
				return AttackDataResult.Continue;
			}
			if (pTargetToCheck.isActor() && pTargetToCheck.hasStatus("dodge"))
			{
				OnYou(by, pTargetToCheck, "dodged");
				if (Blocks.Watching)
				{
					Blocks.Dodging(pData, pTargetToCheck);
				}
				if (mine)
				{
					Traced(pTargetToCheck, "dodged");
				}
				return AttackDataResult.Continue;
			}
			Vector2 current_position = pTargetToCheck.current_position;
			Vector3 hit_position = pData.hit_position;
			float num = Toolbox.SquaredDist(current_position.x, current_position.y + pTargetToCheck.getHeight(), hit_position.x, hit_position.y + hit_position.z);
			float num2 = pData.area_of_effect + pTargetToCheck.stats["size"];
			if (!(num < num2 * num2))
			{
				if (mine && Trace != null)
				{
					Traced(pTargetToCheck, $"missed by {Mathf.Sqrt(num) - num2:F2}");
				}
				if (TracingOnYou(pTargetToCheck))
				{
					OnYou(by, pTargetToCheck, $"missed by {Mathf.Sqrt(num) - num2:F2} (at {current_position.x:F1},{current_position.y:F1}, hit {hit_position.x:F1},{hit_position.y:F1})");
				}
				return AttackDataResult.Miss;
			}
			if (!mine && pData.is_projectile && pTargetToCheck.isActor() && ShotWalls.Walled(pData, pTargetToCheck.a))
			{
				return AttackDataResult.Continue;
			}
			if ((!mine || by != Blocks.Host) && pTargetToCheck.isActor() && pTargetToCheck == Blocks.Host && GuardControl.Take(pData, by, pTargetToCheck.a, out var result))
			{
				if (Blocks.Watching && Trace != null)
				{
					Traced(pTargetToCheck, "guarded " + result.state);
				}
				if (TracingOnYou(pTargetToCheck))
				{
					OnYou(by, pTargetToCheck, "guarded " + result.state);
				}
				return result;
			}
			if (by != pTargetToCheck && pTargetToCheck.isActor() && WorldSync.RemoteGuardTake(pData, by, pTargetToCheck.a, out var result2))
			{
				return result2;
			}
			if (mine && by == Blocks.Host)
			{
				GuardControl.Riposte(ref pData);
			}
			else if (mine && by != null && by.isActor())
			{
				WorldSync.RemoteRiposte(ref pData, by.a);
			}
			bool parry = Blocks.Watching && Blocks.WouldParry(pTargetToCheck);
			Headshots.SkillScale(ref pData, Blocks.Host);
			bool flag = Headshots.Before(ref pData, pTargetToCheck);
			Quests.SparBlow(ref pData, pTargetToCheck);
			Rivals.BrawlBlow(ref pData, pTargetToCheck);
			Actor host = Blocks.Host;
			bool flag2 = host != null && by == host && pTargetToCheck.isActor();
			int healthBefore = (flag2 ? pTargetToCheck.a.getHealth() : 0);
			Actor actor = ((!mine && pTargetToCheck.isActor() && ControllableUnit.isControllingUnit(pTargetToCheck.a)) ? pTargetToCheck.a : null);
			double last_attack_timestamp = actor?.last_attack_timestamp ?? 0.0;
			float attack_timer = actor?.attack_timer ?? 0f;
			AttackDataResult result3 = MapBox.applyAttack(pData, pTargetToCheck);
			if (actor != null && result3.state == ApplyAttackState.Deflect)
			{
				actor.last_attack_timestamp = last_attack_timestamp;
				actor.attack_timer = attack_timer;
			}
			try
			{
				if (flag)
				{
					Headshots.After(pTargetToCheck, result3);
				}
				if (host != null)
				{
					try
					{
						Quests.TrialBlow(by, pTargetToCheck, result3.state == ApplyAttackState.Hit, pData.is_projectile);
					}
					catch
					{
					}
				}
				if (flag2 && result3.state == ApplyAttackState.Hit)
				{
					CombatFeedback.Struck(pTargetToCheck.a, healthBefore);
				}
				if (host != null && by == host && result3.state == ApplyAttackState.Hit && !pData.is_projectile)
				{
					Torches.Struck(pTargetToCheck);
				}
				if (host != null && by == host && result3.state == ApplyAttackState.Hit && !pTargetToCheck.isActor())
				{
					Dismantle.Struck(pTargetToCheck);
				}
				if (result3.state != ApplyAttackState.Continue && result3.state != ApplyAttackState.Miss)
				{
					RoomCombat.Struck(by, pTargetToCheck);
				}
			}
			catch (Exception ex)
			{
				if (!_reported)
				{
					_reported = true;
					Log.Error("your hit's headshot and number: " + ex);
				}
			}
			if (result3.state != ApplyAttackState.Hit && Blocks.Watching)
			{
				Blocks.Saw(pData, pTargetToCheck, result3, parry);
			}
			if (result3.state == ApplyAttackState.Hit)
			{
				hit_position.y += hit_position.z;
				EffectsLibrary.spawnAt(pData.critical ? "fx_hit_critical" : "fx_hit", hit_position, 0.1f);
			}
			if (mine && Trace != null)
			{
				Traced(pTargetToCheck, result3.state.ToString());
			}
			if (TracingOnYou(pTargetToCheck))
			{
				OnYou(by, pTargetToCheck, result3.state.ToString());
			}
			return result3;
		}

		private static bool TracingOnYou(BaseSimObject target)
		{
			if (TraceOnYou != null && target != null)
			{
				return target == Blocks.Host;
			}
			return false;
		}

		private static void OnYou(BaseSimObject by, BaseSimObject target, string what)
		{
			if (TraceOnYou != null && TraceOnYou.Count < 64 && target != null && target == Blocks.Host)
			{
				TraceOnYou.Add(((by != null && by.isActor()) ? by.a.asset.id : "?") + " " + what);
			}
		}

		private static void Traced(BaseSimObject victim, string what)
		{
			if (Trace != null && Trace.Count < 64)
			{
				Trace.Add(new KeyValuePair<BaseSimObject, string>(victim, what));
			}
		}

		private static bool Spare(Actor you, BaseSimObject victim)
		{
			try
			{
				if (Allowed || !Friendly(you, victim))
				{
					return false;
				}
				if (victim.isActor() && WorldSync.IsPlayerBody(victim.a))
				{
					return false;
				}
				if (!victim.isActor() && victim == Meant && !InBattle(you))
				{
					return false;
				}
				Spared++;
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static bool Bystander(Actor you, Actor victim, BaseSimObject meant, bool loose)
		{
			try
			{
				if (victim == null || victim == you || victim == meant)
				{
					return false;
				}
				if (!Person(victim))
				{
					return false;
				}
				if (Fair(you, victim))
				{
					return false;
				}
				return meant != null || !loose || Fighting(you);
			}
			catch
			{
				return false;
			}
		}

		public static bool PassesOver(Actor you, Actor victim, Actor aimed)
		{
			if (!PossessionCombat.Yours(you) || !Bystander(you, victim, aimed, loose: true))
			{
				return false;
			}
			Bystanders++;
			return true;
		}

		private static bool Fighting(Actor you)
		{
			float unscaledTime = Time.unscaledTime;
			if (you == _fightFor && unscaledTime - _fightAt < 0.25f)
			{
				return _fight;
			}
			_fightFor = you;
			_fightAt = unscaledTime;
			_fight = false;
			try
			{
				Vector2 current_position = you.current_position;
				List<Actor> simpleList = World.world.units.getSimpleList();
				for (int i = 0; i < simpleList.Count; i++)
				{
					if (_fight)
					{
						break;
					}
					Actor actor = simpleList[i];
					if (actor != null && actor != you)
					{
						float num = actor.current_position.x - current_position.x;
						float num2 = actor.current_position.y - current_position.y;
						if (num * num + num2 * num2 <= 144f && actor.isAlive() && InFight(you, actor))
						{
							_fight = true;
						}
					}
				}
			}
			catch
			{
			}
			return _fight;
		}

		public static bool MayHurt(Actor you, Actor victim)
		{
			try
			{
				return Allowed || victim == null || !PossessionCombat.Yours(you) || !Friendly(you, victim);
			}
			catch
			{
				return true;
			}
		}

		public static bool Holds(Actor you, Actor a)
		{
			try
			{
				return !Allowed && a != null && a != you && Friendly(you, a);
			}
			catch
			{
				return false;
			}
		}

		public static bool Bystanding(Actor you, Actor a)
		{
			try
			{
				return a != null && a != you && Person(a) && !Fair(you, a);
			}
			catch
			{
				return false;
			}
		}

		public static bool InFight(Actor you, Actor a)
		{
			try
			{
				if (a == null || a == you || !a.isAlive() || !a.has_attack_target)
				{
					return false;
				}
				BaseSimObject attack_target = a.attack_target;
				if (attack_target == null || !attack_target.isAlive())
				{
					return false;
				}
				if (attack_target != you && (!attack_target.isActor() || !Person(attack_target.a)))
				{
					return false;
				}
				Kingdom kingdom = you.kingdom;
				Kingdom kingdom2 = a.kingdom;
				if (kingdom2 == null || (kingdom != null && SameSide(kingdom, kingdom2)))
				{
					return false;
				}
				if (!kingdom2.isCiv())
				{
					return kingdom2.asset != null && kingdom2.asset.count_as_danger;
				}
				return kingdom != null && kingdom.isCiv() && kingdom2.isEnemy(kingdom);
			}
			catch
			{
				return false;
			}
		}

		private static bool Person(Actor a)
		{
			if (a.kingdom != null)
			{
				return a.kingdom.isCiv();
			}
			return false;
		}

		private static bool Fair(Actor you, Actor a)
		{
			Kingdom kingdom = you.kingdom;
			Kingdom kingdom2 = a.kingdom;
			bool flag = kingdom != null && kingdom2 != null && kingdom != kingdom2 && kingdom.isCiv() && kingdom2.isCiv() && kingdom2.isEnemy(kingdom);
			if (flag && !a.isWarrior() && !a.isKing() && !a.isCityLeader())
			{
				return CombatFeedback.StruckAtYou(a);
			}
			if (a.has_attack_target)
			{
				BaseSimObject attack_target = a.attack_target;
				if (attack_target == you || (attack_target != null && you.kingdom != null && attack_target.kingdom != a.kingdom && SameSide(you.kingdom, attack_target.kingdom)))
				{
					return true;
				}
			}
			if (kingdom == null || kingdom2 == null || SameSide(kingdom, kingdom2))
			{
				return false;
			}
			if (flag)
			{
				return true;
			}
			if (kingdom2.asset != null && kingdom2.asset.count_as_danger)
			{
				return you.areFoes(a);
			}
			return false;
		}

		public static bool Friendly(Actor you, BaseSimObject other)
		{
			if (other == null || other == you || you.kingdom == null)
			{
				return false;
			}
			if (other.isActor())
			{
				Actor a = other.a;
				if (a.has_attack_target && a.attack_target == you)
				{
					return false;
				}
				if (you.areFoes(a))
				{
					return false;
				}
			}
			return SameSide(you.kingdom, other.kingdom);
		}

		public static bool Foe(Actor you, Actor a)
		{
			if (a == null || a == you || !a.isAlive())
			{
				return false;
			}
			if (a.has_attack_target)
			{
				BaseSimObject attack_target = a.attack_target;
				if (attack_target == you)
				{
					return true;
				}
				if (attack_target != null && you.kingdom != null && attack_target.kingdom != a.kingdom && SameSide(you.kingdom, attack_target.kingdom))
				{
					return true;
				}
			}
			if (you.has_attack_target && you.attack_target == a)
			{
				return true;
			}
			Kingdom kingdom = you.kingdom;
			Kingdom kingdom2 = a.kingdom;
			if (kingdom == null || kingdom2 == null || SameSide(kingdom, kingdom2))
			{
				return false;
			}
			if (kingdom.isCiv() && kingdom2.isCiv() && kingdom2.isEnemy(kingdom))
			{
				return true;
			}
			if (kingdom2.asset != null && kingdom2.asset.count_as_danger)
			{
				return you.areFoes(a);
			}
			return false;
		}

		private static bool SameSide(Kingdom mine, Kingdom theirs)
		{
			if (mine == null || theirs == null)
			{
				return false;
			}
			if (theirs == mine)
			{
				return true;
			}
			if (mine.hasAlliance() && theirs.hasAlliance())
			{
				return mine.getAlliance() == theirs.getAlliance();
			}
			return false;
		}

		public static bool InBattle(Actor you)
		{
			float unscaledTime = Time.unscaledTime;
			if (you == _battleFor && unscaledTime - _battleAt < 0.25f)
			{
				return _battle;
			}
			_battleFor = you;
			_battleAt = unscaledTime;
			_battle = false;
			Kingdom kingdom = you.kingdom;
			if (kingdom == null)
			{
				return false;
			}
			try
			{
				Vector2 current_position = you.current_position;
				List<Actor> simpleList = World.world.units.getSimpleList();
				for (int i = 0; i < simpleList.Count; i++)
				{
					Actor actor = simpleList[i];
					if (actor == null || actor == you)
					{
						continue;
					}
					float num = actor.current_position.x - current_position.x;
					float num2 = actor.current_position.y - current_position.y;
					float num3 = num * num + num2 * num2;
					if (num3 > 400f || !actor.isAlive())
					{
						continue;
					}
					bool flag = SameSide(kingdom, actor.kingdom);
					if (!flag && num3 < 144f && actor.kingdom != null && actor.kingdom.isCiv() && kingdom.isCiv() && actor.kingdom.isEnemy(kingdom))
					{
						_battle = true;
						break;
					}
					if (!actor.has_attack_target)
					{
						continue;
					}
					BaseSimObject attack_target = actor.attack_target;
					if (attack_target == null || !attack_target.isAlive())
					{
						continue;
					}
					bool flag2 = attack_target == you || SameSide(kingdom, attack_target.kingdom);
					bool num4;
					if (flag)
					{
						if (flag2)
						{
							continue;
						}
						if (attack_target.kingdom != null && attack_target.kingdom.isCiv())
						{
							goto IL_0191;
						}
						if (!attack_target.isActor())
						{
							continue;
						}
						num4 = attack_target.a.has_attack_target;
					}
					else
					{
						num4 = flag2;
					}
					if (!num4)
					{
						continue;
					}
					goto IL_0191;
					IL_0191:
					_battle = true;
					break;
				}
			}
			catch
			{
			}
			return _battle;
		}
	}
}
