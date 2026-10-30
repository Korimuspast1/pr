using System;
using System.Reflection;
using FirstPerson.Core;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal static class Climbing
	{
		private const int GameCheckIlBytes = 331;

		private static bool _installed;

		private static bool _warned;

		internal const float CalmSeconds = 30f;

		private static Actor _you;

		private static Actor _left;

		private static long _leftId = -1L;

		private static double _leftAt;

		private const float UpFree = 0.5f;

		private const float Up = 0.45f;

		private const float UpFloor = 0.35f;

		private const float DownFree = 1f;

		private const float Down = 0.15f;

		private const float DownFloor = 0.65f;

		private const float Reach = 0.5f;

		private static Vector2 _slope;

		private static float _feltAt = -10f;

		public static bool On
		{
			get
			{
				if (_installed)
				{
					return DryBoards.Installed;
				}
				return false;
			}
		}

		internal static Actor LeftForTest => _left;

		internal static Vector2 SlopeForTest => _slope;

		public static void Install()
		{
			if (!_installed)
			{
				MethodInfo method = typeof(Actor).GetMethod("checkVelocityAgainstBlock", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(Vector2) }, null);
				int num = method?.GetMethodBody()?.GetILAsByteArray()?.Length ?? (-1);
				if (num != 331)
				{
					throw new InvalidOperationException("the game's block check has changed (" + num + " bytes of IL, not " + 331 + ")");
				}
				Detour.Apply(method, typeof(Climbing).GetMethod("CheckVelocityAgainstBlock", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
				_installed = true;
			}
		}

		internal static bool IsRock(WorldTile t)
		{
			try
			{
				return t != null && t.Type != null && t.Type.block && t.Type.mountains;
			}
			catch
			{
				return false;
			}
		}

		internal static bool Climbs(Actor a)
		{
			try
			{
				return On && PossessionHooks.Active && a != null && a == ControllableUnit._unit_main && a.asset != null && !a.asset.is_boat;
			}
			catch
			{
				return false;
			}
		}

		internal static bool IsWall(WorldTile t)
		{
			try
			{
				return t != null && t.Type != null && t.Type.block && t.Type.wall;
			}
			catch
			{
				return false;
			}
		}

		private static WorldTile Under(Actor a)
		{
			try
			{
				return Toolbox.getTileAt(a.current_position.x, a.current_position.y);
			}
			catch
			{
				return null;
			}
		}

		internal static bool Stops(Actor a, WorldTile t)
		{
			if (PossessionHooks.IsBlock(t))
			{
				if (IsRock(t))
				{
					return !Climbs(a);
				}
				return true;
			}
			return false;
		}

		private static Vector2 CheckVelocityAgainstBlock(Actor self, Vector2 next)
		{
			try
			{
				WorldTile tileAt = Toolbox.getTileAt(next.x, next.y);
				if (IsRock(tileAt) && Climbs(self))
				{
					return next;
				}
				TileTypeBase type = self.current_tile.Type;
				if (type.block && (!type.mountains || tileAt.Type.mountains))
				{
					return next;
				}
				if (tileAt == self.current_tile)
				{
					return next;
				}
				if (self.asset.is_boat)
				{
					if (tileAt.Type.liquid)
					{
						return next;
					}
				}
				else
				{
					if (!tileAt.Type.block)
					{
						return next;
					}
					if (((BaseSimObject)self).getHeight() > tileAt.Type.block_height)
					{
						return next;
					}
				}
				Vector2 wallNormal = self.getWallNormal(next, (Vector2)tileAt.posV3);
				float num = self.velocity.x * wallNormal.x + self.velocity.y * wallNormal.y;
				float num2 = self.velocity.x - 2f * num * wallNormal.x;
				float num3 = self.velocity.y - 2f * num * wallNormal.y;
				self.velocity.x = num2 * 0.8f;
				self.velocity.y = num3 * 0.8f;
				return self.current_position;
			}
			catch (Exception ex)
			{
				if (!_warned)
				{
					_warned = true;
					Log.Error("block check: " + ex);
				}
				return next;
			}
		}

		internal static void Watch()
		{
			Actor actor = null;
			try
			{
				if (On && PossessionHooks.Active)
				{
					actor = ControllableUnit._unit_main;
				}
			}
			catch
			{
			}
			if (actor == _you)
			{
				return;
			}
			try
			{
				if (_you != null && _you.isAlive() && _you.data != null && _you.asset != null && !_you.asset.is_boat)
				{
					_left = _you;
					_leftId = _you.data.id;
					_leftAt = WorldPace.ActionNow;
				}
			}
			catch
			{
			}
			_you = actor;
		}

		internal static bool Step(Actor a)
		{
			if (a == null || (a != _you && a != _left))
			{
				return false;
			}
			try
			{
				if (a._update_done || a.position_height > 0f || ((BaseSimObject)a).isFlying())
				{
					return false;
				}
				if (a == _you && (IsWall(a.current_tile) || IsWall(Under(a))) && Climbs(a) && !a.ignoresBlocks())
				{
					return true;
				}
				if (!IsRock(a.current_tile) || a.ignoresBlocks())
				{
					if (a == _left && a != _you && !IsRock(Toolbox.getTileAt(a.current_position.x, a.current_position.y)))
					{
						_left = null;
					}
					return false;
				}
				if (a == _you)
				{
					return Climbs(a);
				}
				if (a.data == null || a.data.id != _leftId || !a.isAlive() || WorldPace.ActionNow - _leftAt > 30.0)
				{
					_left = null;
					return false;
				}
				if (a.asset.move_from_block && !a.is_moving && (!a.hasTask() || !((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai).task.move_from_block))
				{
					a.setTask("move_from_block", pClean: true, pCleanJob: true);
				}
				return true;
			}
			catch (Exception ex)
			{
				if (!_warned)
				{
					_warned = true;
					Log.Error("mountain step: " + ex);
				}
				return false;
			}
		}

		public static void Feel(Vector2 p, TerrainCache land, float dt)
		{
			if (land != null)
			{
				float x = land.SampleHeight(p.x + 0.5f, p.y) - land.SampleHeight(p.x - 0.5f, p.y);
				float y = land.SampleHeight(p.x, p.y + 0.5f) - land.SampleHeight(p.x, p.y - 0.5f);
				Vector2 vector = new Vector2(x, y) / 1f;
				if (!float.IsNaN(vector.x) && !float.IsNaN(vector.y))
				{
					_slope = ((Time.unscaledTime - _feltAt < 0.5f) ? Vector2.Lerp(_slope, vector, 1f - Mathf.Exp(-8f * Mathf.Clamp(dt, 0f, 0.1f))) : vector);
					_feltAt = Time.unscaledTime;
				}
			}
		}

		public static float Pace(Vector2 heading)
		{
			if (Time.unscaledTime - _feltAt > 0.5f || heading.sqrMagnitude < 1E-08f)
			{
				return 1f;
			}
			float num = Vector2.Dot(_slope, heading.normalized);
			if (num > 0.5f)
			{
				return Mathf.Max(0.35f, 1f / (1f + 0.45f * (num - 0.5f)));
			}
			if (num < -1f)
			{
				return Mathf.Max(0.65f, 1f / (1f + 0.15f * (0f - num - 1f)));
			}
			return 1f;
		}
	}
}
