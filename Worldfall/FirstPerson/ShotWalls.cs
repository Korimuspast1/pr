using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class ShotWalls
	{
		private sealed class Seen
		{
			public Vec3 Last;

			public int Frame;
		}

		private static readonly Dictionary<Projectile, Seen> Followed = new Dictionary<Projectile, Seen>();

		private static readonly List<Projectile> Gone = new List<Projectile>();

		private static SceneCollector _scene;

		private static TerrainCache _terrain;

		private static Actor _you;

		private static int _frame = -10;

		private const float Range = 40f;

		internal static bool On = true;

		internal static bool Trace;

		internal static int Stopped;

		internal static int Refused;

		public static void Update(TerrainCache terrain, SceneCollector scene, Actor you)
		{
			_terrain = terrain;
			_scene = scene;
			_you = you;
			_frame = Time.frameCount;
			MapBox world = World.world;
			if (!On || world == null || world.projectiles == null || terrain == null || scene == null || you == null)
			{
				Followed.Clear();
				return;
			}
			Vector2 current_position = you.current_position;
			List<Projectile> list = world.projectiles.list;
			for (int i = 0; i < list.Count; i++)
			{
				Projectile projectile = list[i];
				try
				{
					Follow(projectile, current_position);
				}
				catch (Exception ex)
				{
					Log.Error("others' shot against walls: " + ex.Message);
					Followed.Remove(projectile);
				}
			}
			Gone.Clear();
			foreach (KeyValuePair<Projectile, Seen> item in Followed)
			{
				if (item.Value.Frame != _frame)
				{
					Gone.Add(item.Key);
				}
			}
			foreach (Projectile item2 in Gone)
			{
				Followed.Remove(item2);
			}
		}

		private static void Follow(Projectile p, Vector2 you)
		{
			if (!Watched(p))
			{
				return;
			}
			Vector3 current_position_3d = p._current_position_3d;
			float num = current_position_3d.x - you.x;
			float num2 = current_position_3d.y - you.y;
			if (!(num * num + num2 * num2 > 1600f))
			{
				Vec3 vec = Drawn(current_position_3d);
				if (!Followed.TryGetValue(p, out var value))
				{
					Dictionary<Projectile, Seen> followed = Followed;
					Seen obj = new Seen
					{
						Last = Loosed(p, vec)
					};
					value = obj;
					followed[p] = obj;
				}
				value.Frame = _frame;
				if (_scene.Strikes(value.Last, vec, out var at))
				{
					Stop(p, value.Last, at);
				}
				value.Last = vec;
			}
		}

		private static bool Watched(Projectile p)
		{
			if (p == null || p.asset == null || p._state != ProjectileState.Active)
			{
				return false;
			}
			if (FirstPersonShots.Steered(p) || FirstPersonShots.IsHandled(p))
			{
				return false;
			}
			BaseSimObject by_who = p.by_who;
			if (by_who != null && by_who.isActor())
			{
				return by_who != _you;
			}
			return false;
		}

		private static Vec3 Drawn(Vector3 g)
		{
			return new Vec3(g.x, g.y, _terrain.SampleHeight(g.x, g.y) + g.z);
		}

		private static Vec3 Loosed(Projectile p, Vec3 now)
		{
			Vector2 vector_start = p._vector_start;
			float num = 0.5f;
			try
			{
				if (p.by_who != null && p.by_who.isActor())
				{
					num = Mathf.Max(0.3f, ((BaseSimObject)p.by_who.a).getHeight() + 0.5f * BodyHeight(p.by_who.a));
				}
			}
			catch
			{
			}
			Vec3 result = new Vec3(vector_start.x, vector_start.y, _terrain.SampleHeight(vector_start.x, vector_start.y) + num);
			float num2 = now.X - result.X;
			float num3 = now.Y - result.Y;
			if (!(num2 * num2 + num3 * num3 > 36f))
			{
				return result;
			}
			return now;
		}

		private static float BodyHeight(Actor a)
		{
			if (!Hitboxes.Get(a, out var box))
			{
				return 0.5f;
			}
			return box.Top - box.Bottom;
		}

		private static void Stop(Projectile p, Vec3 from, Vec3 wall)
		{
			Stopped++;
			if (Trace)
			{
				Log.Info($"others' shot {p.asset.id} stopped at {wall.X:F2},{wall.Y:F2},{wall.Z:F2} from {from.X:F2},{from.Y:F2},{from.Z:F2}");
			}
			Vec3 vec = wall - from;
			Vec3 vec2 = ((Vec3.Dot(vec, vec) > 1E-08f) ? vec.Normalized() : new Vec3(1f, 0f, 0f));
			Vec3 vec3 = wall - new Vec3(vec2.X, vec2.Y, 0f) * 0.12f;
			if (p.asset.id == "arrow")
			{
				StuckArrows.Stick(wall + vec2 * 0.0968f, vec2, ground: false);
				FirstPersonShots.MarkHandled(p);
			}
			p._current_position_3d = new Vector3(vec3.X, vec3.Y, 0f);
			p._velocity = Vector3.zero;
			if (!string.IsNullOrEmpty(p.asset.terraform_option) || !string.IsNullOrEmpty(p.asset.end_effect))
			{
				p._collision_timeout = 0f;
				return;
			}
			try
			{
				EffectsLibrary.spawnAt("fx_miss", new Vector3(vec3.X, vec3.Y, 0f), 0.1f);
			}
			catch
			{
			}
			p.setState(p.asset.can_be_left_on_ground ? ProjectileState.AlphaAnimation : ProjectileState.ToRemove);
			p.targetReached();
		}

		public static bool Walled(AttackData data, Actor you)
		{
			if (!On || !data.is_projectile || you == null || you != _you || _scene == null || _terrain == null)
			{
				return false;
			}
			if (Time.frameCount - _frame > 2)
			{
				return false;
			}
			try
			{
				Vector3 hit_position = data.hit_position;
				Vec3 vec = Drawn(hit_position);
				Vec3 a = default(Vec3);
				bool flag = false;
				foreach (KeyValuePair<Projectile, Seen> item in Followed)
				{
					Vector3 current_position_3d = item.Key._current_position_3d;
					if (current_position_3d.x == hit_position.x && current_position_3d.y == hit_position.y && current_position_3d.z == hit_position.z)
					{
						a = item.Value.Last;
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					BaseSimObject initiator = data.initiator;
					if (initiator == null || !initiator.isActor())
					{
						return false;
					}
					Vector2 current_position = initiator.current_position;
					a = new Vec3(current_position.x, current_position.y, _terrain.SampleHeight(current_position.x, current_position.y) + Mathf.Max(0.3f, ((BaseSimObject)initiator.a).getHeight() + 0.5f * BodyHeight(initiator.a)));
				}
				Vector2 current_position2 = you.current_position;
				Vec3 b = new Vec3(current_position2.x, current_position2.y, _terrain.SampleHeight(current_position2.x, current_position2.y) + 0.5f * BodyHeight(you));
				Vec3 at;
				bool flag2 = _scene.Strikes(a, vec, out at);
				bool flag3 = !flag2 && _scene.Strikes(vec, b, out at);
				if (Trace)
				{
					Log.Info($"others' shot on you at {vec.X:F2},{vec.Y:F2},{vec.Z:F2} from {a.X:F2},{a.Y:F2},{a.Z:F2} (known {flag}): walled {flag2} / {flag3}");
				}
				if (!flag2 && !flag3)
				{
					return false;
				}
				Refused++;
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}
