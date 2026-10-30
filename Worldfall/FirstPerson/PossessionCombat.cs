using System;
using System.Reflection;
using FirstPerson.Core;
using FirstPerson.Net;
using UnityEngine;

namespace FirstPerson
{
	internal static class PossessionCombat
	{
		internal static int StunsShrugged;

		internal static int Blows;

		internal static Actor LastBlowAt;

		private static bool _reported;

		public static void Install(Action<Action, string> optional)
		{
			Type status = typeof(StatusLibrary);
			Type self = typeof(PossessionCombat);
			optional(delegate
			{
				Detour.Apply(status.GetMethod("checkPossessedAttackLeft", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), self.GetMethod("AttackLeft", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			}, "could not take over possessed attacks, blows and shots go where the game aims them");
			optional(delegate
			{
				Detour.Apply(status.GetMethod("checkPossessedAttackRight", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), self.GetMethod("AttackRight", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			}, "could not take the right button from the kick (your guard: GuardControl)");
			optional(delegate
			{
				Detour.Apply(typeof(Actor).GetMethod("makeStunned", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic), self.GetMethod("MakeStunned", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			}, "could not keep the creature you are in from being stunned");
			FriendlyFire.Install(optional);
			FriendlyBurns.Install(optional);
		}

		private static bool InFirstPerson(Actor a)
		{
			if (PossessionHooks.Active && a != null && a == ControllableUnit._unit_main && !BoatHelm.Steers(a))
			{
				return FirstPersonAim.Ready(a);
			}
			return false;
		}

		internal static bool Yours(Actor a)
		{
			try
			{
				return a != null && ControllableUnit.isControllingUnit(a) && ((BaseSimObject)a).hasStatus("possessed");
			}
			catch
			{
				return false;
			}
		}

		private static void AttackLeft(StatusLibrary lib, Actor actor)
		{
			try
			{
				if (!ControllableUnit.isAttackPressedLeft() || (Config.joyControls && !TouchPossessionController.isSelectedActionAttack()) || actor.asset.skip_fight_logic || !actor.isAttackReady())
				{
					return;
				}
				if (InFirstPerson(actor))
				{
					Attack(actor);
					return;
				}
				Actor actorTargetAttack = lib.getActorTargetAttack(actor, 0f);
				float attackRange = actor.getAttackRange();
				Vector3 vector = lib.getAttackPos(actor, 0f);
				if (actorTargetAttack != null && actor.hasMeleeAttack())
				{
					float num = Vector2.Distance(actor.current_position, actorTargetAttack.current_position);
					if (num > attackRange)
					{
						num = attackRange;
					}
					vector = Toolbox.getNewPointVec2(actor.current_position, vector, num);
				}
				Kingdom pForceKingdom = World.world.kingdoms_wild.get("possessed");
				FriendlyFire.Meant = actorTargetAttack;
				try
				{
					actor.tryToAttack((BaseSimObject)null, pDoChecks: false, (Action)null, vector, pForceKingdom, actorTargetAttack?.current_tile, attackRange * 0.2f);
				}
				finally
				{
					FriendlyFire.Meant = null;
				}
				actor.setPossessionAttackHappened();
			}
			catch (Exception e)
			{
				Report(e);
			}
		}

		private static void Attack(Actor actor)
		{
			float attackRange = actor.getAttackRange();
			Kingdom pForceKingdom = World.world.kingdoms_wild.get("possessed");
			if (actor.hasMeleeAttack())
			{
				bool active = RoomCombat.Active;
				if (active)
				{
					RoomCombat.Sync();
				}
				Actor actor2 = FirstPersonAim.Melee(actor, Reach(attackRange));
				WorldTile worldTile = null;
				bool flag = false;
				Vector3 vector;
				if (actor2 != null)
				{
					Doorstep.StepOut(actor2);
					Vector2 current_position = actor2.current_position;
					vector = new Vector3(current_position.x, current_position.y + ((BaseSimObject)actor2).getHeight(), 0f);
					worldTile = actor2.current_tile;
				}
				else
				{
					Vec3 eye = FirstPersonAim.Eye;
					Vector2 room = new Vector2(eye.X, eye.Y) + FirstPersonAim.Flat * Mathf.Max(0.4f, attackRange);
					if (active)
					{
						RoomCombat.SwungAt(actor, Reach(attackRange));
						room = RoomCombat.ToWorld(room);
					}
					vector = new Vector3(room.x, room.y, 0f);
					Building building = (active ? null : AimedBuilding());
					Butchery.Carcass carcass = ((!active && Butchery.CanCut(actor)) ? Butchery.Aimed(new Vector2(eye.X, eye.Y), FirstPersonAim.Flat, 2.3f) : null);
					if (carcass != null)
					{
						flag = true;
						vector = new Vector3(carcass.At.x, carcass.At.y, 0f);
						Butchery.Cut(actor, carcass, (WorldBoxMod.Instance != null) ? new Action<string>(WorldBoxMod.Instance.Toast) : null);
					}
					else if (building != null && (building.current_position - new Vector2(eye.X, eye.Y)).sqrMagnitude <= (attackRange + 2f) * (attackRange + 2f))
					{
						vector = new Vector3(building.current_position.x, building.current_position.y, 0f);
						worldTile = building.current_tile;
					}
				}
				FriendlyFire.Meant = ((actor2 != null) ? ((BaseSimObject)actor2) : ((BaseSimObject)((flag || active) ? null : AimedBuilding())));
				float num = ((actor2 != null && Headshots.Aimed(actor2, Reach(attackRange))) ? 1.5f : 1f);
				Headshots.Aim(actor, actor2, num);
				Slicing.Blow = true;
				Moves.BlowScale = Moves.TakePending();
				if (WorldSync.ForwardStrike(actor, actor2, vector, worldTile, attackRange, num, Slicing.Blow, Moves.BlowScale))
				{
					FriendlyFire.Meant = null;
					Headshots.Clear();
					Slicing.Blow = false;
					Moves.BlowScale = 1f;
					Blows++;
					LastBlowAt = actor2;
					actor.setPossessionAttackHappened();
					return;
				}
				try
				{
					actor.tryToAttack((BaseSimObject)(active ? actor2 : null), pDoChecks: false, (Action)null, vector, pForceKingdom, worldTile, attackRange * 0.2f);
				}
				finally
				{
					FriendlyFire.Meant = null;
					Headshots.Clear();
					Slicing.Blow = false;
					Moves.BlowScale = 1f;
				}
				Blows++;
				LastBlowAt = actor2;
			}
			else
			{
				if (RoomCombat.Active)
				{
					RoomCombat.Sync();
				}
				Vec3 point;
				Actor creature;
				Vector3 pAttackPosition = (FirstPersonAim.ShotPoint(out point, out creature, pastFriends: true) ? ((Vector3)(RoomCombat.Active ? RoomCombat.ToWorld(new Vector2(point.X, point.Y)) : new Vector2(point.X, point.Y))) : ((Vector3)(actor.current_position + FirstPersonAim.ShotFlat * Mathf.Max(4f, attackRange))));
				WorldTile tile = World.world.GetTile((int)pAttackPosition.x, (int)pAttackPosition.y);
				FirstPersonShots.BeforeAttack();
				actor.tryToAttack((BaseSimObject)null, pDoChecks: false, (Action)null, pAttackPosition, pForceKingdom, tile, attackRange * 0.2f);
				FirstPersonShots.AfterAttack(actor);
			}
			actor.setPossessionAttackHappened();
		}

		internal static bool SwingHit(Actor actor, Actor victim, bool head)
		{
			try
			{
				if (!InFirstPerson(actor) || victim == null || !victim.isAlive() || actor.asset.skip_fight_logic)
				{
					return false;
				}
				if (WorldSync.ForwardSwing(actor, victim, head))
				{
					Blows++;
					LastBlowAt = victim;
					actor.setPossessionAttackHappened();
					return true;
				}
				float attackRange = actor.getAttackRange();
				Kingdom pForceKingdom = World.world.kingdoms_wild.get("possessed");
				Vector2 current_position = victim.current_position;
				Vector3 pAttackPosition = new Vector3(current_position.x, current_position.y + ((BaseSimObject)victim).getHeight(), 0f);
				FriendlyFire.Meant = victim;
				Headshots.Aim(actor, victim, head ? 1.5f : 1f);
				Slicing.Blow = true;
				try
				{
					actor.tryToAttack((BaseSimObject)null, pDoChecks: false, (Action)null, pAttackPosition, pForceKingdom, victim.current_tile, attackRange * 0.2f);
				}
				finally
				{
					FriendlyFire.Meant = null;
					Headshots.Clear();
					Slicing.Blow = false;
				}
				Blows++;
				LastBlowAt = victim;
				actor.setPossessionAttackHappened();
				return true;
			}
			catch (Exception e)
			{
				Report(e);
				return false;
			}
		}

		internal static bool Punch(Actor actor, Actor victim, bool head)
		{
			try
			{
				if (!InFirstPerson(actor) || victim == null || !victim.isAlive() || !FriendlyFire.MayHurt(actor, victim))
				{
					return false;
				}
				if (WorldSync.ForwardPunch(actor, victim, head))
				{
					Blows++;
					LastBlowAt = victim;
					return true;
				}
				float pDamage = BareDamage(actor) * (head ? 1.5f : 1f);
				((BaseSimObject)victim).getHit(pDamage, pFlash: true, AttackType.Weapon, (BaseSimObject)actor, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: true);
				Sounds.PlayAt("event:/SFX/HIT/HitFlesh", victim.current_position);
				Blows++;
				LastBlowAt = victim;
				return true;
			}
			catch (Exception e)
			{
				Report(e);
				return false;
			}
		}

		internal static float BareDamage(Actor a)
		{
			float num = 10f;
			float num2 = 0f;
			try
			{
				num = ((BaseSimObject)a).stats["damage"];
			}
			catch
			{
			}
			try
			{
				ItemAsset weaponAsset = a.getWeaponAsset();
				if (weaponAsset != null && a.getWeapon() != null)
				{
					num2 = weaponAsset.base_stats["damage"];
				}
			}
			catch
			{
			}
			return Mathf.Max(1f, num - num2);
		}

		private static Building AimedBuilding()
		{
			try
			{
				if (!(WorldBoxMod.Instance?.Picked is Building building) || !building.isAlive() || building.asset == null)
				{
					return null;
				}
				BuildingType building_type = building.asset.building_type;
				return (building_type == BuildingType.Building_Tree || building_type == BuildingType.Building_Mineral || building_type == BuildingType.Building_Plant) ? null : building;
			}
			catch
			{
				return null;
			}
		}

		private static float Reach(float range)
		{
			return Mathf.Max(0.55f, range * 1.2f + 0.15f);
		}

		internal static float ReachOf(Actor a)
		{
			try
			{
				return Reach(a.getAttackRange());
			}
			catch
			{
				return 0.8f;
			}
		}

		private static void AttackRight(StatusLibrary lib, Actor actor)
		{
			try
			{
				if (!InFirstPerson(actor) && ControllableUnit.isAttackJustPressedRight() && actor.asset.control_can_kick && !actor.asset.skip_fight_logic && actor.isAttackReady())
				{
					Vector2 possessionControlTargetPosition = actor.getPossessionControlTargetPosition();
					actor.punchTargetAnimation((Vector3)possessionControlTargetPosition, pFlip: false, pReverse: false, -20f);
					actor.setActionTimeout(1f);
					actor.spawnSlashKick(possessionControlTargetPosition);
					actor.lookTowardsPosition(possessionControlTargetPosition);
					actor.setPossessionAttackHappened();
					Actor actorTargetAttack = lib.getActorTargetAttack(actor, 2f);
					if (actorTargetAttack != null && !(Vector2.Distance(actor.current_position, actorTargetAttack.current_position) >= 2f))
					{
						Shove(actor, actorTargetAttack, possessionControlTargetPosition);
					}
				}
			}
			catch (Exception e)
			{
				Report(e);
			}
		}

		private static void Shove(Actor actor, Actor target, Vector2 at)
		{
			if (FriendlyFire.MayHurt(actor, target))
			{
				target.makeStunned();
				Vector2 current_position = actor.current_position;
				current_position.y += actor.current_scale.y;
				float num = Randy.randomFloat(1.5f, 3f);
				float num2 = Randy.randomFloat(1.5f, 3f);
				if (actor.under_forces || ((BaseSimObject)actor).hasStatus("dash"))
				{
					num *= 2f;
					num2 *= 1.5f;
				}
				target.calculateForce(at.x, at.y, current_position.x, current_position.y, num, num2, pCheckCancelJobOnLand: true);
				target.addAggro(actor);
			}
		}

		private static void MakeStunned(Actor self, float time)
		{
			try
			{
				if (Yours(self))
				{
					StunsShrugged++;
					return;
				}
				time += Randy.randomFloat(0f, time * 0.1f);
				self.cancelAllBeh();
				self.makeWait(time);
				if (((BaseSimObject)self).addStatusEffect("stunned", time, pColorEffect: true))
				{
					self.finishAngryStatus();
				}
			}
			catch (Exception e)
			{
				Report(e);
			}
		}

		public static void ShakeOffStuns(Actor host)
		{
			try
			{
				if (host != null && host.isAlive() && Yours(host) && ((BaseSimObject)host).hasStatus("stunned"))
				{
					host.finishStatusEffect("stunned");
					host.timer_action = 0f;
					host.setStatsDirty();
					StunsShrugged++;
				}
			}
			catch
			{
			}
		}

		internal static void AttackForTest(Actor a)
		{
			try
			{
				a.resetAttackTimeout();
			}
			catch
			{
			}
			if (InFirstPerson(a))
			{
				Attack(a);
			}
			else
			{
				Log.Error("SHOT attack for test: not in first person");
			}
		}

		private static void Report(Exception e)
		{
			if (!_reported)
			{
				_reported = true;
				Log.Error("possessed attack hook failed: " + e);
			}
		}
	}
}
