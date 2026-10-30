using System;
using System.Collections;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class HeadshotTest : CivilTest
	{
		private enum Target
		{
			Head,
			Chest,
			Legs
		}

		private Actor _host;

		private Vector2 _home;

		private float _yaw;

		private readonly float[] _chances = new float[3] { -1f, -1f, -1f };

		private static readonly string[] Actions = new string[3] { "combat_deflect_projectile", "combat_dodge", "combat_block" };

		private bool _knocked;

		private bool _blowPictured;

		protected override string Prefix => "HEADS";

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			if (!Check("open land", worldTile != null))
			{
				yield break;
			}
			CivilTest.ClearBox(worldTile.x - 14, worldTile.y - 14, worldTile.x + 14, worldTile.y + 14);
			_host = CivilTest.SpawnNear("human", new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f));
			if (!Check("a villager", _host != null))
			{
				yield break;
			}
			yield return Possess(_host);
			if (Check("first person in them", InControl(_host)))
			{
				_home = _host.current_position;
				_yaw = Mod.ViewYaw;
				Mod.Settings.HeadBob = 0f;
				for (int i = 0; i < Actions.Length; i++)
				{
					_chances[i] = SetChance(Actions[i], 0f);
				}
				yield return Part("a blow", Blows());
				yield return Part("an arrow", Arrows());
				yield return Part("others fighting", Others());
				Restore();
			}
		}

		private void OnDestroy()
		{
			Restore();
		}

		private void Restore()
		{
			for (int i = 0; i < Actions.Length; i++)
			{
				if (_chances[i] >= 0f)
				{
					SetChance(Actions[i], _chances[i]);
					_chances[i] = -1f;
				}
			}
		}

		private static float SetChance(string id, float chance)
		{
			try
			{
				CombatActionAsset combatActionAsset = AssetManager.combat_action_library.get(id);
				if (combatActionAsset == null)
				{
					return -1f;
				}
				float chance2 = combatActionAsset.chance;
				combatActionAsset.chance = chance;
				return chance2;
			}
			catch
			{
				return -1f;
			}
		}

		private IEnumerator Blows()
		{
			if (!Check("a sword", Equip(_host, "sword_iron")))
			{
				yield break;
			}
			CombatFeedback fb = Mod.Feedback;
			Actor bandit = Foe(1.05f, "sword_iron");
			if (!Check("a bandit before you", bandit != null))
			{
				yield break;
			}
			yield return Settle(bandit, Target.Head);
			HeadBox head2;
			bool head = Hitboxes.GetHead(bandit, out head2);
			Hitbox box;
			bool flag = Hitboxes.Get(bandit, out box);
			Check("their head, as drawn, at the top of their body", head && flag && head2.Centre.Z > box.Centre.Z + box.HalfUp * 0.4f && head2.Top <= box.Top + 0.12f, (head && flag) ? $"head at {head2.Centre.Z - box.Bottom:F2} of {box.Top - box.Bottom:F2} tall, top {head2.Top - box.Bottom:F2}" : $"head {head}, body {flag}");
			int landed = Headshots.Landed;
			int played = Mod.Audio.HeadshotsPlayed;
			float before = Health(bandit);
			yield return Blow(bandit, Target.Head);
			float num = Health(bandit);
			bool knocked = _knocked;
			Check("a blow on the head is a headshot", Headshots.Landed == landed + 1 && Mathf.Approximately(Headshots.LastTimes, 1.5f), $"headshots {landed}->{Headshots.Landed}, times {Headshots.LastTimes:F1}, health {before}->{num}");
			Check("...its damage half as much again, through the game's own hit", Headshots.LastDamage == Mathf.Max(Headshots.LastBase + 1, Mathf.RoundToInt((float)Headshots.LastBase * 1.5f)) && num < before, $"damage {Headshots.LastBase} -> {Headshots.LastDamage}, health {before}->{num}");
			FloatingText floatingText = fb.Texts.Find((FloatingText floatingText4) => floatingText4.Victim == bandit && !floatingText4.Word);
			Check("the number in red with HEADSHOT, the red marker", floatingText != null && floatingText.Head && fb.HitWasHead, (floatingText == null) ? "no number" : $"head {floatingText.Head}, marker head {fb.HitWasHead}");
			Check("the crack", Mod.Audio.HeadshotsPlayed > played, $"played {played}->{Mod.Audio.HeadshotsPlayed}");
			Check("their head knocked back", knocked);
			yield return CivilTest.Wait(1.2f);
			Heal(bandit);
			landed = Headshots.Landed;
			yield return Settle(bandit, Target.Chest);
			before = Health(bandit);
			yield return Blow(bandit, Target.Chest);
			num = Health(bandit);
			FloatingText floatingText2 = fb.Texts.Find((FloatingText floatingText4) => floatingText4.Victim == bandit && !floatingText4.Word && floatingText4.Age < 0.6f);
			Check("a blow on the body is not a headshot, its number plain", Headshots.Landed == landed && num < before && floatingText2 != null && !floatingText2.Head && !fb.HitWasHead, $"headshots {landed}->{Headshots.Landed}, health {before}->{num}, number {floatingText2?.Text} head {floatingText2?.Head}");
			yield return CivilTest.Wait(1.2f);
			int kills = fb.Kills;
			try
			{
				((BaseSimObject)bandit).getHit(Health(bandit) - 3f, pFlash: false, AttackType.Other, (BaseSimObject)null, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: false);
			}
			catch (Exception ex)
			{
				Log.Error("HEADS wear down: " + ex.Message);
			}
			yield return Settle(bandit, Target.Head);
			float low = Health(bandit);
			int headsBefore = Headshots.Landed;
			int blowsBefore = PossessionCombat.Blows;
			yield return Blow(bandit, Target.Head);
			for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
			{
				if (!bandit.isAlive())
				{
					break;
				}
				Stay();
				yield return null;
			}
			yield return null;
			FloatingText floatingText3 = fb.Texts.Find((FloatingText floatingText4) => floatingText4.Text == "KILL");
			Check("a killing blow is your kill, a headshot's at that", !bandit.isAlive() && fb.Kills == kills + 1 && floatingText3 != null && floatingText3.Head, string.Format("health {0} before, {1} after, alive {2}, blows {3}->{4} at {5}, headshots {6}->{7}, kills {8}->{9}, KILL {10}", low, Health(bandit), bandit.isAlive(), blowsBefore, PossessionCombat.Blows, (PossessionCombat.LastBlowAt == bandit) ? "the bandit" : ((PossessionCombat.LastBlowAt == null) ? "nobody" : "someone else"), headsBefore, Headshots.Landed, kills, fb.Kills, (floatingText3 == null) ? "none" : (floatingText3.Head ? "with HEADSHOT" : "plain")));
			CivilTest.Kill(bandit);
			yield return CivilTest.Wait(0.5f);
		}

		private IEnumerator Later(Actor target, string file)
		{
			for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime)
			{
				Stay();
				Hold(target);
				yield return null;
			}
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, file));
		}

		private IEnumerator Blow(Actor target, Target where)
		{
			Mod.Melee.PressForTest();
			float h0 = Health(target);
			for (float t = 0f; t < 1.4f; t += Time.unscaledDeltaTime)
			{
				Stay();
				Hold(target);
				Aim(target, where);
				yield return null;
				if ((Health(target) < h0 || !target.isAlive()) && t > 0.1f)
				{
					_knocked = Headshots.KnockOf(target, out var _);
					if (where == Target.Head && !_blowPictured)
					{
						_blowPictured = true;
						yield return Later(target, "headshot_blow.png");
					}
					for (int i = 0; i < 4; i++)
					{
						Stay();
						Hold(target);
						yield return null;
					}
					break;
				}
			}
		}

		private IEnumerator Arrows()
		{
			if (Check("a bow", Equip(_host, "bow_wood")))
			{
				yield return CivilTest.Wait(0.6f);
				Actor bandit = Foe(6f, null);
				if (Check("a bandit six tiles off", bandit != null))
				{
					yield return Settle(bandit, Target.Head);
					int landed = Headshots.Landed;
					float before = Health(bandit);
					yield return Shoot(bandit, Target.Head, "headshot_arrow.png");
					Check("an arrow in the head is a headshot, twice the damage", Headshots.Landed == landed + 1 && Mathf.Approximately(Headshots.LastTimes, 2f) && Health(bandit) < before, $"headshots {landed}->{Headshots.Landed}, times {Headshots.LastTimes:F1}, damage {Headshots.LastBase}->{Headshots.LastDamage}, health {before}->{Health(bandit)}, shot ended {FirstPersonShots.Struck}");
					yield return CivilTest.Wait(1f);
					Heal(bandit);
					landed = Headshots.Landed;
					yield return Settle(bandit, Target.Legs);
					before = Health(bandit);
					yield return Shoot(bandit, Target.Legs, null);
					Check("an arrow in the legs is not", Headshots.Landed == landed && Health(bandit) < before, $"headshots {landed}->{Headshots.Landed}, health {before}->{Health(bandit)}");
					CivilTest.Kill(bandit);
					yield return CivilTest.Wait(0.5f);
				}
			}
		}

		private IEnumerator Shoot(Actor target, Target where, string picture)
		{
			float h0 = Health(target);
			Aim(target, where);
			yield return null;
			PossessionCombat.AttackForTest(_host);
			for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
			{
				Stay();
				Hold(target);
				yield return null;
				if (Health(target) < h0)
				{
					if (picture != null)
					{
						yield return Later(target, picture);
					}
					for (int i = 0; i < 4; i++)
					{
						Stay();
						Hold(target);
						yield return null;
					}
					break;
				}
			}
		}

		private IEnumerator Others()
		{
			float f = _yaw + 0.87266463f;
			Vector2 vector = _home + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * 4f;
			Actor bandit = CivilTest.SpawnNear("bandit", vector);
			Actor wolf = CivilTest.Spawn("wolf", vector + new Vector2(0.9f, 0f));
			if (!Check("a bandit and a wolf beside you", bandit != null && wolf != null && Equip(bandit, "sword_iron")))
			{
				CivilTest.Kill(bandit);
				CivilTest.Kill(wolf);
				yield break;
			}
			int landed = Headshots.Landed;
			float b0 = Health(bandit);
			float w0 = Health(wolf);
			for (float t = 0f; t < 8f; t += 0.1f)
			{
				try
				{
					if (wolf.isAlive() && (!wolf.has_attack_target || wolf.attack_target != bandit))
					{
						wolf.startFightingWith(bandit);
					}
					if (bandit.isAlive() && (!bandit.has_attack_target || bandit.attack_target != wolf))
					{
						bandit.startFightingWith(wolf);
					}
				}
				catch
				{
				}
				Stay();
				yield return CivilTest.Wait(0.1f);
				if ((Health(bandit) < b0 || !bandit.isAlive()) && (Health(wolf) < w0 || !wolf.isAlive()) && t > 2f)
				{
					break;
				}
			}
			Check("they hurt each other, and none of it is a headshot", (Health(bandit) < b0 || Health(wolf) < w0) && Headshots.Landed == landed, $"bandit {b0}->{Health(bandit)}, wolf {w0}->{Health(wolf)}, headshots {landed}->{Headshots.Landed}");
			CivilTest.Kill(bandit);
			CivilTest.Kill(wolf);
		}

		private Actor Foe(float dist, string weapon)
		{
			Vector2 at = _home + new Vector2(Mathf.Cos(_yaw), Mathf.Sin(_yaw)) * dist;
			Actor actor = CivilTest.SpawnNear("bandit", at);
			if (actor == null)
			{
				return null;
			}
			if (weapon != null && !Equip(actor, weapon))
			{
				CivilTest.Kill(actor);
				return null;
			}
			Heal(actor);
			Hold(actor);
			return actor;
		}

		private IEnumerator Settle(Actor target, Target where)
		{
			for (int i = 0; i < 25; i++)
			{
				Stay();
				Hold(target);
				Aim(target, where);
				yield return null;
			}
		}

		private void Aim(Actor target, Target where)
		{
			Vec3 vec;
			if (where == Target.Head && Hitboxes.GetHead(target, out var head))
			{
				vec = head.Centre;
			}
			else
			{
				if (!Hitboxes.Get(target, out var box))
				{
					return;
				}
				vec = new Vec3(box.Centre.X, box.Centre.Y, (where == Target.Legs) ? (box.Bottom + (box.Top - box.Bottom) * 0.18f) : (box.Centre.Z - box.HalfUp * 0.1f));
			}
			Vector3 eyeForTest = Mod.EyeForTest;
			float num = vec.X - eyeForTest.x;
			float num2 = vec.Y - eyeForTest.y;
			float num3 = Mathf.Max(0.05f, Mathf.Sqrt(num * num + num2 * num2));
			Mod.SetYaw(Mathf.Atan2(num2, num));
			Mod.SetPitchDegrees(Mathf.Clamp(Mathf.Atan((vec.Z - eyeForTest.z) / num3) * 57.29578f, -44f, 44f));
		}

		private static void Hold(Actor a)
		{
			try
			{
				if (a != null && a.isAlive())
				{
					a.cancelAllBeh();
					a.makeWait(5f);
					a.clearAttackTarget();
				}
			}
			catch
			{
			}
		}

		private static void Heal(Actor a)
		{
			try
			{
				if (a != null && a.isAlive())
				{
					a.restoreHealth(a.getMaxHealth());
				}
			}
			catch
			{
			}
		}

		private void Stay()
		{
			if (_host == null || !_host.isAlive())
			{
				return;
			}
			_host.current_position = _home;
			_host.dirty_current_tile = true;
			try
			{
				_host.restoreHealth(_host.getMaxHealth());
			}
			catch
			{
			}
		}

		private static float Health(Actor a)
		{
			try
			{
				return (a != null && a.isAlive()) ? ((float)a.getHealth()) : 0f;
			}
			catch
			{
				return 0f;
			}
		}

		private static bool Equip(Actor unit, string id)
		{
			try
			{
				ActorEquipmentSlot slot = unit.equipment.getSlot(EquipmentType.Weapon);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				if (equipmentAsset == null)
				{
					return false;
				}
				Item pItem = World.world.items.generateItem(equipmentAsset, unit.kingdom, unit.getName(), 1, unit, 0, pByPlayer: true);
				slot.setItem(pItem, unit);
				unit.setStatsDirty();
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("HEADS equip " + id + ": " + ex.Message);
				return false;
			}
		}
	}
}
