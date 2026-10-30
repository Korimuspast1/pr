using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class Blocks
	{
		public const float OthersRange = 40f;

		public const float PoseSeconds = 0.55f;

		public const float FlashSeconds = 0.18f;

		public static readonly uint SteelRgb = TerrainCache.Rgb(120, 178, 255) & 0xFFFFFF;

		private static Actor _host;

		private static int _hostFrame = -10;

		private static int _serial;

		private static readonly List<BlockEvent> Recent = new List<BlockEvent>();

		private static readonly Dictionary<Actor, BlockEvent> LastOn = new Dictionary<Actor, BlockEvent>();

		private static float _othersWindow;

		private static int _othersInWindow;

		internal static int YourBlowsStopped;

		internal static int BlowsOnYouStopped;

		internal static int OthersStopped;

		private static readonly List<Actor> _gone = new List<Actor>();

		private static bool _reported;

		public static Actor Host
		{
			get
			{
				if (Time.frameCount - _hostFrame > 2)
				{
					return null;
				}
				return _host;
			}
			set
			{
				_host = value;
				_hostFrame = Time.frameCount;
			}
		}

		public static bool Watching
		{
			get
			{
				if (_host != null)
				{
					return Time.frameCount - _hostFrame <= 2;
				}
				return false;
			}
		}

		public static int Newest => _serial;

		public static List<BlockEvent> Events => Recent;

		internal static bool WouldParry(BaseSimObject target)
		{
			try
			{
				Actor host = Host;
				if (host == null || target != host)
				{
					return false;
				}
				return ControllableUnit.isControllingUnit(host) && host.hasMeleeAttack() && host.isJustAttacked();
			}
			catch
			{
				return false;
			}
		}

		internal static void Dodging(AttackData data, BaseSimObject target)
		{
			try
			{
				if (target != null && target.isActor())
				{
					Vector2 current_position = target.current_position;
					Vector3 hit_position = data.hit_position;
					float num = Toolbox.SquaredDist(current_position.x, current_position.y + target.getHeight(), hit_position.x, hit_position.y + hit_position.z);
					float num2 = data.area_of_effect + target.stats["size"];
					if (num < num2 * num2)
					{
						Record(data, target.a, Stop.Dodged, parry: false);
					}
				}
			}
			catch (Exception e)
			{
				Report(e);
			}
		}

		internal static void Guarded(AttackData data, Actor you, Stop kind)
		{
			try
			{
				Record(data, you, kind, kind == Stop.Parried);
			}
			catch (Exception e)
			{
				Report(e);
			}
		}

		internal static void Saw(AttackData data, BaseSimObject target, AttackDataResult result, bool parry)
		{
			try
			{
				if (target != null && target.isActor())
				{
					switch (result.state)
					{
					case ApplyAttackState.Deflect:
						Record(data, target.a, parry ? Stop.Parried : (data.is_projectile ? Stop.Deflected : Stop.Blocked), parry);
						break;
					case ApplyAttackState.Block:
						Record(data, target.a, Stop.Blocked, parry: false);
						break;
					case ApplyAttackState.Continue:
						Record(data, target.a, Stop.Dodged, parry: false);
						break;
					}
				}
			}
			catch (Exception e)
			{
				Report(e);
			}
		}

		private static void Record(AttackData data, Actor defender, Stop kind, bool parry)
		{
			Actor host = Host;
			if (host == null || defender == null)
			{
				return;
			}
			BaseSimObject initiator = data.initiator;
			bool flag = initiator != null && initiator == host;
			bool flag2 = defender == host;
			if (flag && flag2)
			{
				return;
			}
			float unscaledTime = Time.unscaledTime;
			if (!flag && !flag2)
			{
				if ((defender.current_position - host.current_position).sqrMagnitude > 1600f)
				{
					return;
				}
				if (unscaledTime - _othersWindow > 1f)
				{
					_othersWindow = unscaledTime;
					_othersInWindow = 0;
				}
				if (++_othersInWindow > 8)
				{
					return;
				}
			}
			for (int num = Recent.Count - 1; num >= 0; num--)
			{
				BlockEvent blockEvent = Recent[num];
				if (unscaledTime - blockEvent.Time > 0.2f)
				{
					break;
				}
				if (blockEvent.Defender == defender && blockEvent.Attacker == initiator && blockEvent.Kind == kind)
				{
					blockEvent.Count++;
					return;
				}
			}
			Vector2 vector = ((initiator != null && initiator.isAlive()) ? initiator.current_position : new Vector2(data.initiator_position.x, data.initiator_position.y));
			BlockEvent blockEvent2 = new BlockEvent
			{
				Serial = ++_serial,
				Kind = kind,
				YourBlow = flag,
				OnYou = flag2,
				Shot = data.is_projectile,
				Defender = defender,
				Attacker = initiator,
				Time = unscaledTime,
				Weight = WeightOf(initiator, data.is_projectile)
			};
			blockEvent2.Material = MaterialOf(defender, initiator, blockEvent2.Shot, data.metallic_weapon);
			blockEvent2.At = ContactOn(defender, vector, flag2, out blockEvent2.Toward);
			Recent.Add(blockEvent2);
			LastOn[defender] = blockEvent2;
			if (flag)
			{
				YourBlowsStopped++;
			}
			else if (flag2)
			{
				BlowsOnYouStopped++;
			}
			else
			{
				OthersStopped++;
			}
			Prune(unscaledTime);
		}

		private static void Prune(float now)
		{
			int i;
			for (i = 0; i < Recent.Count && (now - Recent[i].Time > 3f || Recent.Count - i > 64); i++)
			{
			}
			if (i > 0)
			{
				Recent.RemoveRange(0, i);
			}
			if (LastOn.Count <= 48)
			{
				return;
			}
			_gone.Clear();
			foreach (KeyValuePair<Actor, BlockEvent> item in LastOn)
			{
				if (now - item.Value.Time > 0.55f)
				{
					_gone.Add(item.Key);
				}
			}
			foreach (Actor item2 in _gone)
			{
				LastOn.Remove(item2);
			}
		}

		private static Clash MaterialOf(Actor defender, BaseSimObject by, bool shot, bool metallicBlow)
		{
			try
			{
				if (defender.getWeapon() != null)
				{
					ItemAsset weaponAsset = defender.getWeaponAsset();
					if (weaponAsset != null)
					{
						return (!weaponAsset.metallic) ? Clash.Wood : Clash.Metal;
					}
				}
				if (defender.haveMetallicArmor())
				{
					return Clash.Metal;
				}
			}
			catch
			{
			}
			return Clash.Flesh;
		}

		private static float WeightOf(BaseSimObject by, bool shot)
		{
			if (shot)
			{
				return 0.15f;
			}
			try
			{
				if (by != null && by.isActor())
				{
					Actor a = by.a;
					string text = ((a.getWeapon() == null) ? null : a.getWeaponAsset()?.id);
					if (!string.IsNullOrEmpty(text))
					{
						return Mathf.Clamp01(StrikeTiming.For(ViewMeshes.KindOf(text), melee: true, hands: true).Weight);
					}
					return Mathf.Clamp(0.25f + 0.3f * (((BaseSimObject)a).stats["size"] - 1f), 0.2f, 1f);
				}
			}
			catch
			{
			}
			return 0.4f;
		}

		private static Vec3 ContactOn(Actor defender, Vector2 from, bool onYou, out Vector2 toward)
		{
			Vector2 current_position = defender.current_position;
			toward = from - current_position;
			if (toward.sqrMagnitude < 1E-06f)
			{
				toward = (onYou ? FirstPersonAim.Flat : new Vector2(defender.flip ? 1f : (-1f), 0f));
			}
			toward.Normalize();
			if (onYou)
			{
				Vec3 eye = FirstPersonAim.Eye;
				return new Vec3(eye.X + toward.x * 0.68f, eye.Y + toward.y * 0.68f, eye.Z - 0.16f);
			}
			if (Hitboxes.Get(defender, out var box))
			{
				float aX = box.AX;
				float aY = box.AY;
				float num = Mathf.Abs(toward.x * aX + toward.y * aY);
				float num2 = Mathf.Abs((0f - toward.x) * aY + toward.y * aX);
				float a = Mathf.Min((num > 0.0001f) ? (box.HalfA / num) : 99f, (num2 > 0.0001f) ? (box.HalfB / num2) : 99f);
				a = Mathf.Min(a, Mathf.Max(box.HalfA, box.HalfB)) + 0.05f;
				float z = box.Bottom + (box.Top - box.Bottom) * 0.62f;
				return new Vec3(box.Centre.X + toward.x * a, box.Centre.Y + toward.y * a, z);
			}
			float num3 = 0f;
			try
			{
				if (Hitboxes.GroundAt != null)
				{
					num3 = Hitboxes.GroundAt(current_position.x, current_position.y);
				}
			}
			catch
			{
			}
			return new Vec3(current_position.x + toward.x * 0.2f, current_position.y + toward.y * 0.2f, num3 + 0.55f);
		}

		public static bool PoseOf(Actor a, out BlockEvent e, out float age)
		{
			age = 99f;
			if (a == null || !LastOn.TryGetValue(a, out e))
			{
				e = null;
				return false;
			}
			age = e.Age;
			if (age < 0.55f)
			{
				return age >= 0f;
			}
			return false;
		}

		public static uint FlashOf(Actor a)
		{
			if (a == null || !LastOn.TryGetValue(a, out var value) || value.Kind == Stop.Dodged)
			{
				return 0u;
			}
			float age = value.Age;
			if (age < 0f || age >= 0.18f)
			{
				return 0u;
			}
			float num = 1f - age / 0.18f;
			return ((uint)(100f * num * num) << 24) | SteelRgb;
		}

		public static string Word(Stop kind)
		{
			switch (kind)
			{
			case Stop.Deflected:
				return "DEFLECTED";
			case Stop.Dodged:
				return "DODGED";
			case Stop.Parried:
				return "PARRIED";
			default:
				return "BLOCKED";
			}
		}

		public static void Clear()
		{
			Recent.Clear();
			LastOn.Clear();
		}

		private static void Report(Exception e)
		{
			if (!_reported)
			{
				_reported = true;
				Log.Error("blocks: " + e);
			}
		}
	}
}
