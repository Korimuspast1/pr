using System;
using System.Collections.Generic;
using System.Reflection;
using FirstPerson.Net;
using UnityEngine;

namespace FirstPerson
{
	internal static class CrabGuard
	{
		private struct Pending
		{
			public int X;

			public int Y;

			public int Owner;

			public float At;
		}

		private const int CreateIlBytes = 258;

		private const int UpdateIlBytes = 944;

		private static bool _installed;

		private static readonly List<Pending> _foreign = new List<Pending>();

		private const float PendingFor = 30f;

		private const float PendingReach = 6f;

		internal static Action<Actor, int> Hatched;

		public static bool Installed => _installed;

		public static void Install()
		{
			if (!_installed)
			{
				MethodInfo original = Step("create", 258, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, typeof(Actor));
				MethodInfo original2 = Step("update", 944, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, typeof(float));
				Detour.Apply(original, typeof(CrabGuard).GetMethod("Create", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				Detour.Apply(original2, typeof(CrabGuard).GetMethod("Update", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
			}
		}

		private static MethodInfo Step(string name, int ilBytes, BindingFlags flags, Type arg)
		{
			MethodInfo method = typeof(Crabzilla).GetMethod(name, flags | BindingFlags.DeclaredOnly, null, new Type[1] { arg }, null);
			int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
			if (num != ilBytes)
			{
				throw new InvalidOperationException("the game's Crabzilla." + name + " has changed (" + num + " bytes of IL, not " + ilBytes + ")");
			}
			return method;
		}

		internal static void NoteForeign(int x, int y, int owner = 0)
		{
			_foreign.Add(new Pending
			{
				X = x,
				Y = y,
				Owner = owner,
				At = Time.realtimeSinceStartup
			});
		}

		private static bool Foreign(Actor crab)
		{
			int owner;
			return Foreign(crab, out owner);
		}

		private static bool Foreign(Actor crab, out int owner)
		{
			owner = 0;
			if (NetSession.Joined)
			{
				return true;
			}
			float now = Time.realtimeSinceStartup;
			_foreign.RemoveAll((Pending p) => now - p.At > 30f);
			WorldTile worldTile = crab?.current_tile;
			if (worldTile == null)
			{
				return false;
			}
			for (int num = 0; num < _foreign.Count; num++)
			{
				float num2 = _foreign[num].X - worldTile.x;
				float num3 = _foreign[num].Y - worldTile.y;
				if (!(num2 * num2 + num3 * num3 > 36f))
				{
					owner = _foreign[num].Owner;
					_foreign.RemoveAt(num);
					return true;
				}
			}
			return false;
		}

		private static void Create(Crabzilla self, Actor pActor)
		{
			((BaseActorComponent)self).actor = pActor;
			self.transform.position = ((BaseActorComponent)self).actor.current_position;
			self.bodyPos = new Vector3(0f, 27.8f, 0f);
			self.bodyPosTarget = new Vector3(0f, 27.8f, 0f);
			self.mouthSpriteAnim = self.mouthSprite.GetComponent<SpriteAnimation>();
			self.createLimbs();
			bool flag = false;
			int owner = 0;
			try
			{
				flag = Foreign(((BaseActorComponent)self).actor, out owner);
			}
			catch (Exception ex)
			{
				Log.Error("crab guard: " + ex.Message);
			}
			if (!flag)
			{
				ControllableUnit.setControllableCreature(((BaseActorComponent)self).actor);
				WorldTip.showNow(Config.isMobile ? "crabzilla_controls_mobile" : "crabzilla_controls_pc", pTranslate: true, "top", 8f);
			}
			else
			{
				Log.Info("a Crabzilla made by another player: your controls stay yours");
				if (owner != 0)
				{
					Hatched?.Invoke(((BaseActorComponent)self).actor, owner);
				}
			}
			Vector3 position = self.transform.position;
			position.z = self.z_pos;
			self.transform.position = position;
			((BaseActorComponent)self).actor.current_position = self.transform.position;
		}

		private static void Update(Crabzilla self, float pElapsed)
		{
			bool flag = ControllableUnit.isControllingUnit(((BaseActorComponent)self).actor);
			CrabFire.Shot shot = null;
			if (!flag && CrabFire.Of(((BaseActorComponent)self).actor, out var shot2) && shot2.Any)
			{
				shot = shot2;
			}
			if (shot != null)
			{
				Vector2 vector = CrabFire.MapPoint(shot);
				self.armTarget.transform.position = new Vector3(vector.x, vector.y, self.armTarget.transform.position.z);
			}
			if (self.bodyPosTimeout > 0f)
			{
				self.bodyPosTimeout -= pElapsed;
			}
			if (flag)
			{
				self.arm1.update(pElapsed);
				self.arm2.update(pElapsed);
			}
			else
			{
				Look(self.arm1, pElapsed);
				Look(self.arm2, pElapsed);
			}
			CrabLeg[] list_legs = self.list_legs;
			for (int i = 0; i < list_legs.Length; i++)
			{
				list_legs[i].update(pElapsed);
			}
			if (self.isAnyLimbFlickering())
			{
				self.list_limbs[self.active_limb].update(pElapsed);
			}
			self._beam_enabled = (flag ? ControllableUnit.isAttackPressedLeft() : (shot != null));
			self.mouthSprite.SetActive(self.isBeamEnabled());
			if (self.mouthSprite.gameObject.activeSelf)
			{
				self.mouthSpriteAnim.update(pElapsed);
				MusicBox.inst.playDrawingSound("event:/SFX/UNIQUE/Crabzilla/CrabzillaVoice", ((BaseActorComponent)self).actor.current_position.x, ((BaseActorComponent)self).actor.current_position.y);
			}
			Vector2 vector2 = (flag ? ControllableUnit.getMovementVector() : Vector2.zero);
			if (flag && !ControllableUnit.isMovementActionActive())
			{
				vector2 = Vector2.zero;
			}
			if (vector2.x > 0f)
			{
				self.bodyRotationTarget.z = self.moveRotationLimit;
			}
			else if (vector2.x < 0f)
			{
				self.bodyRotationTarget.z = 0f - self.moveRotationLimit;
			}
			else
			{
				self.bodyRotationTarget.z = 0f;
			}
			float num = World.world.elapsed * 60f;
			self.bodyRotation = Vector3.MoveTowards(self.bodyRotation, self.bodyRotationTarget, 0.7f * num);
			if (vector2.y > 0f && self.bodyRotation.z > self.moveRotationLimit)
			{
				self.bodyRotation.z = self.moveRotationLimit;
			}
			else if (vector2.y < 0f && self.bodyRotation.z < 0f - self.moveRotationLimit)
			{
				self.bodyRotation.z = 0f - self.moveRotationLimit;
			}
			self.bodyPos.z = 0f;
			self.bodyPosTarget.z = 0f;
			self.mainBody.transform.localRotation = Quaternion.Euler(self.bodyRotation);
			self.bodyPos = Vector2.MoveTowards(self.bodyPos, self.bodyPosTarget, 0.7f * num);
			self.mainBody.transform.localPosition = self.bodyPos;
			if (!flag)
			{
				Vector2 current_position = ((BaseActorComponent)self).actor.current_position;
				self.transform.position = new Vector3(current_position.x + ((BaseActorComponent)self).actor.shake_offset.x, current_position.y + ((BaseActorComponent)self).actor.shake_offset.y, self.z_pos);
				((BaseActorComponent)self).actor.dirty_current_tile = true;
				return;
			}
			Vector3 position = self.transform.position;
			if (!vector2.Equals(Vector2.zero))
			{
				Vector2 vector3 = self.transform.position;
				vector3 = Vector2.MoveTowards(vector3, vector3 + vector2 * 0.2f * num, 1f * num);
				position = new Vector3(vector3.x, vector3.y);
				if (position.x < 0f)
				{
					position.x = 0f;
				}
				if (position.y < 0f)
				{
					position.y = 0f;
				}
				if (position.x > (float)MapBox.width)
				{
					position.x = MapBox.width;
				}
				if (position.y > (float)MapBox.height)
				{
					position.y = MapBox.height;
				}
				position.z = self.z_pos;
			}
			position.x += ((BaseActorComponent)self).actor.shake_offset.x;
			position.y += ((BaseActorComponent)self).actor.shake_offset.y;
			self.transform.position = position;
			((BaseActorComponent)self).actor.current_position = self.transform.position;
			((BaseActorComponent)self).actor.dirty_current_tile = true;
			self.updateArms();
		}

		private static void Look(CrabArm arm, float pElapsed)
		{
			if (arm == null)
			{
				return;
			}
			Camera camera = World.world.camera;
			if (camera != null)
			{
				Vector3 vector = camera.WorldToScreenPoint(arm.crabzilla.armTarget.transform.position);
				Vector3 vector2 = camera.WorldToScreenPoint(arm.joint.transform.position);
				float num = Mathf.Atan2(vector.y - vector2.y, vector.x - vector2.x) * 57.29578f + 90f;
				if (arm.mirrored)
				{
					num += 180f;
				}
				arm.joint.transform.rotation = Quaternion.Euler(0f, 0f, num);
			}
			arm.updateLaser(pElapsed);
			if (arm.crabzilla.isBeamEnabled())
			{
				World.world.stack_effects.light_blobs.Add(new LightBlobData
				{
					position = new Vector2(arm.laser.transform.position.x, arm.laser.transform.position.y),
					radius = 1.5f
				});
			}
		}
	}
}
