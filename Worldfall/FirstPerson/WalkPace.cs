using System;
using System.Reflection;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal static class WalkPace
	{
		private const int GameStepIlBytes = 49;

		private static bool _installed;

		private static float _blend;

		private static float _factor = 1f;

		private static Vector2 _eye;

		private static float _near = 200f;

		private static float _near2;

		private static float _far2;

		private static float _span = 1f;

		private const float CliffPace = 0.6f;

		public static bool Installed => _installed;

		internal static float ForTest(Actor a)
		{
			return Of(a);
		}

		public static void Install()
		{
			if (!_installed)
			{
				MethodInfo method = typeof(Actor).GetMethod("u10_checkSmoothMovement", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(float) }, null);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 49)
				{
					throw new InvalidOperationException("the game's walking step has changed (" + num + " bytes of IL, not " + 49 + ")");
				}
				Detour.Apply(method, typeof(WalkPace).GetMethod("Step", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
			}
		}

		public static void Update(bool slow, float factor, Vector2 eye, float reach, float dt)
		{
			_blend = Mathf.MoveTowards(_blend, (slow && factor < 0.999f) ? 1f : 0f, Mathf.Clamp(dt, 0f, 0.1f) * 1.5f);
			_factor = Mathf.Clamp(factor, 0.2f, 1f);
			_eye = eye;
			_near = Mathf.Max(60f, reach);
			_near2 = _near * _near;
			_far2 = _near2 * 1.5625f;
			_span = _near * 0.25f;
		}

		public static float Of(Actor a)
		{
			if (_blend <= 0f || a == null)
			{
				return 1f;
			}
			try
			{
				float num = a.current_position.x - _eye.x;
				float num2 = a.current_position.y - _eye.y;
				float num3 = num * num + num2 * num2;
				if (num3 >= _far2)
				{
					return 1f;
				}
				if (a.has_attack_target || a == ControllableUnit._unit_main || BoatHelm.Steers(a))
				{
					return 1f;
				}
				if (a.hasTask() && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.speed_multiplier >= 1.5f)
				{
					return 1f;
				}
				float num4 = ((num3 <= _near2) ? 1f : (1f - Mathf.SmoothStep(0f, 1f, (Mathf.Sqrt(num3) - _near) / _span)));
				return Mathf.Lerp(1f, _factor, _blend * num4) * Wading.OthersPace(a);
			}
			catch
			{
				return 1f;
			}
		}

		private static void Step(Actor self, float elapsed)
		{
			if (!self._update_done && !self.is_immovable)
			{
				if (!Config.time_scale_asset.sonic)
				{
					self.checkCalibrateTargetPosition();
				}
				self.updateMovement(((_blend > 0f) ? (elapsed * Of(self)) : elapsed) * Steep(self), 0f);
			}
		}

		private static float Steep(Actor a)
		{
			try
			{
				byte[] array = ReliefWorld.Sampler?.Cache?.Marks;
				WorldTile current_tile = a.current_tile;
				if (array == null || current_tile == null || a.is_inside_boat || a == ControllableUnit._unit_main)
				{
					return 1f;
				}
				int num = current_tile.y * ReliefWorld.Sampler.Cache.Width + current_tile.x;
				return (num >= 0 && num < array.Length && (array[num] & 2) != 0) ? 0.6f : 1f;
			}
			catch
			{
				return 1f;
			}
		}
	}
}
