using System;
using System.Reflection;
using FirstPerson.Net;

namespace FirstPerson
{
	internal static class FriendlyBurns
	{
		private const int BurnIlBytes = 81;

		internal static int Spared;

		private const int BlastIlBytes = 386;

		internal static int BlastSpared;

		public static void Install(Action<Action, string> optional)
		{
			optional(delegate
			{
				MethodInfo method = typeof(ActionLibrary).GetMethod("addBurningEffectOnTarget", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 81)
				{
					throw new InvalidOperationException("the game's addBurningEffectOnTarget has changed (" + num + " bytes of IL, not " + 81 + ")");
				}
				Detour.Apply(method, typeof(FriendlyBurns).GetMethod("AddBurning", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			}, "could not keep your fire off your own side");
			optional(delegate
			{
				MethodInfo method = typeof(MapBox).GetMethod("applyForceOnTile", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 386)
				{
					throw new InvalidOperationException("the game's applyForceOnTile has changed (" + num + " bytes of IL, not " + 386 + ")");
				}
				Detour.Apply(method, typeof(FriendlyBurns).GetMethod("ApplyForceOnTile", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
			}, "could not keep your blasts off your own side");
		}

		private static void ApplyForceOnTile(MapBox self, WorldTile pTile, int pRad, float pForceAmount, bool pForceOut, int pDamage, string[] pIgnoreKingdoms, BaseSimObject pByWho, TerraformOptions pOptions, bool pChangeHappiness)
		{
			int num = pRad * pRad;
			Actor actor = pByWho?.a;
			bool flag = false;
			try
			{
				flag = actor != null && actor == Blocks.Host && actor._has_status_possessed && PossessionCombat.Yours(actor);
			}
			catch
			{
			}
			foreach (Actor item in Finder.getUnitsFromChunk(pTile, 1))
			{
				if (item == actor || (float)Toolbox.SquaredDistTile(item.current_tile, pTile) > (float)num || (pOptions != null && item.asset.very_high_flyer && !pOptions.applies_to_high_flyers))
				{
					continue;
				}
				if (pIgnoreKingdoms != null)
				{
					bool flag2 = false;
					for (int i = 0; i < pIgnoreKingdoms.Length; i++)
					{
						if (item.kingdom == self.kingdoms_wild.get(pIgnoreKingdoms[i]))
						{
							flag2 = true;
							break;
						}
					}
					if (flag2)
					{
						continue;
					}
				}
				if (flag && SpareFromBlast(actor, item))
				{
					continue;
				}
				item.makeStunned(4f);
				if (pChangeHappiness)
				{
					item.changeHappiness("just_forced_power");
				}
				if (item.asset.can_be_hurt_by_powers && pDamage > 0)
				{
					AttackType pAttackType = AttackType.Other;
					if (pOptions != null)
					{
						pAttackType = pOptions.attack_type;
					}
					int healthBefore = (flag ? item.getHealth() : 0);
					((BaseSimObject)item).getHit((float)pDamage, pFlash: true, pAttackType, pByWho, pSkipIfShake: true, pMetallicWeapon: false, pCheckDamageReduction: true);
					if (flag)
					{
						try
						{
							CombatFeedback.Struck(item, healthBefore);
						}
						catch
						{
						}
					}
				}
				if (pForceAmount > 0f)
				{
					if (pForceOut)
					{
						item.calculateForce((float)item.current_tile.x, (float)item.current_tile.y, (float)pTile.x, (float)pTile.y, pForceAmount, 0f, pCheckCancelJobOnLand: true);
					}
					else
					{
						item.calculateForce((float)pTile.x, (float)pTile.y, (float)item.current_tile.x, (float)item.current_tile.y, pForceAmount, 0f, pCheckCancelJobOnLand: true);
					}
				}
			}
		}

		private static bool SpareFromBlast(Actor you, Actor a)
		{
			try
			{
				if (FriendlyFire.Allowed || !FriendlyFire.Friendly(you, a))
				{
					return false;
				}
				BlastSpared++;
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static bool AddBurning(BaseSimObject pSelf, BaseSimObject pTarget, WorldTile pTile)
		{
			if (!pTarget.isAlive())
			{
				return false;
			}
			if (Spare(pSelf, pTarget))
			{
				return false;
			}
			if (pTarget.isBuilding() && pTarget.b.isBurnable())
			{
				pTarget.addStatusEffect("burning", 0f, pColorEffect: true);
				return true;
			}
			if (pTarget.isActor())
			{
				pTarget.addStatusEffect("burning", 0f, pColorEffect: true);
				return true;
			}
			return false;
		}

		private static bool Spare(BaseSimObject by, BaseSimObject target)
		{
			try
			{
				if (by == null || target == null || by == target || !by.isActor())
				{
					return false;
				}
				Actor a = by.a;
				if (!a._has_status_possessed || (!PossessionCombat.Yours(a) && !WorldSync.HostStrikesFor(a)))
				{
					return false;
				}
				if (FriendlyFire.Allowed || !FriendlyFire.Friendly(a, target))
				{
					return false;
				}
				if (target.isActor() && WorldSync.IsPlayerBody(target.a))
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
	}
}
