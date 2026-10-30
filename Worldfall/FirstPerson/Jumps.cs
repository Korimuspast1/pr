using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal static class Jumps
	{
		private const float HumanForce = 1.5f;

		private static readonly Dictionary<string, float> Beasts = new Dictionary<string, float>
		{
			{ "cat", 1.3f },
			{ "monkey", 1.3f },
			{ "goat", 1.25f },
			{ "jumpy_skull", 1.2f },
			{ "fox", 1.15f },
			{ "rabbit", 1.1f },
			{ "unicorn", 1.1f },
			{ "wolf", 1.05f },
			{ "frog", 1f },
			{ "dog", 1f },
			{ "fire_elemental_horse", 1f },
			{ "angle", 1f },
			{ "plague_doctor", 1f },
			{ "hyena", 0.9f },
			{ "ostrich", 0.9f },
			{ "fire_elemental", 0.9f },
			{ "mush_unit", 0.9f },
			{ "raccoon", 0.8f },
			{ "alpaca", 0.8f },
			{ "fire_skull", 0.8f },
			{ "sheep", 0.75f },
			{ "tumor_monster_animal", 0.7f },
			{ "assimilator", 0.7f },
			{ "bear", 0.6f },
			{ "sand_spider", 0.6f },
			{ "mush_animal", 0.6f },
			{ "lil_pumpkin", 0.6f },
			{ "bioblob", 0.6f },
			{ "fire_elemental_blob", 0.6f },
			{ "chicken", 0.55f },
			{ "rat", 0.55f },
			{ "capybara", 0.5f },
			{ "piranha", 0.5f },
			{ "smore", 0.5f },
			{ "acid_blob", 0.5f },
			{ "garl", 0.5f },
			{ "crystal_sword", 0.5f },
			{ "snowman", 0.5f },
			{ "penguin", 0.45f },
			{ "armadillo", 0.45f },
			{ "cow", 0.45f },
			{ "buffalo", 0.45f },
			{ "crab", 0.4f },
			{ "scorpion", 0.4f },
			{ "flower_bud", 0.4f },
			{ "ant_black", 0.35f },
			{ "ant_green", 0.35f },
			{ "ant_blue", 0.35f },
			{ "ant_red", 0.35f },
			{ "snake", 0.3f },
			{ "rhino", 0.3f },
			{ "fire_elemental_slug", 0.3f },
			{ "fire_elemental_snake", 0.3f },
			{ "printer", 0.3f },
			{ "crocodile", 0.25f },
			{ "seal", 0.25f },
			{ "turtle", 0.2f },
			{ "living_plants", 0.2f },
			{ "worm", 0.15f },
			{ "lemon_snail", 0.15f }
		};

		private const int MinSteps = 6;

		internal static float LastShare;

		internal static float LastForce;

		internal static float LastAlong;

		internal static float LastPeak;

		private static bool _fallReported;

		private const float NominalTick = 0.02f;

		private static bool _nominal;

		internal static void CheckJump(StatusLibrary self, Actor actor)
		{
			try
			{
				if (actor == null || actor.under_forces || actor.asset == null || !actor.asset.control_can_jump)
				{
					return;
				}
				float num = Share(actor);
				float mass = Mathf.Max(0.01f, ((BaseSimObject)actor).stats["mass"]);
				bool flag = (_nominal = PossessionHooks.Active && PossessionHooks.FallHooked && actor == ControllableUnit._unit_main);
				float force;
				float along;
				int steps;
				try
				{
					Solve(num, mass, out force, out along, out steps);
					LastShare = num;
					LastForce = force;
					LastAlong = along;
					LastPeak = Peak(force, mass, out var _);
				}
				finally
				{
					_nominal = false;
				}
				if (steps < 6)
				{
					LastPeak = 0f;
				}
				else if (flag)
				{
					if (PossessionHooks.Leaping)
					{
						PossessionHooks.KeepJump();
					}
					else if (!(actor.position_height > 0.02f))
					{
						PossessionHooks.StartLeap(actor, LastPeak);
						EffectsLibrary.spawnAt("fx_jump", actor.current_position, 0.1f);
					}
				}
				else
				{
					Vector2 current_position = actor.current_position;
					Vector2 vector = current_position - ControllableUnit.getMovementVector();
					EffectsLibrary.spawnAt("fx_jump", current_position, 0.1f);
					actor.calculateForce(current_position.x, current_position.y, vector.x, vector.y, along, force, pCheckCancelJobOnLand: true);
				}
			}
			catch (Exception ex)
			{
				Log.Error("jump failed: " + ex.Message);
			}
		}

		internal static void UpdateFall(Actor self)
		{
			try
			{
				if ((PossessionHooks.Leaping && self == PossessionHooks.LeapOf) || self.position_height < 0f)
				{
					return;
				}
				float elapsed = World.world.elapsed;
				float num = SimGlobals.m.gravity * ((BaseSimObject)self).stats.get("mass");
				self.position_height -= num * elapsed;
				if (self.position_height <= 0f)
				{
					self.position_height = 0f;
					if (!self.under_forces)
					{
						self.stopForce();
					}
				}
			}
			catch (Exception ex)
			{
				if (!_fallReported)
				{
					_fallReported = true;
					Log.Error("fall hook failed: " + ex.Message);
				}
			}
		}

		internal static float Share(Actor actor)
		{
			ActorAsset asset = actor.asset;
			float value;
			if (asset.is_humanoid)
			{
				float num = Mathf.Clamp(SimGlobals.m.gravity * 0.5f / Mathf.Max(0.001f, ((BaseSimObject)actor).stats["mass_2"] * actor.actor_scale), 1.5f, 2.5f);
				value = 1f + 0.25f * (num - 1.5f);
			}
			else if (!Beasts.TryGetValue(asset.id ?? "", out value))
			{
				value = Mathf.Clamp(1f - 0.2f * Mathf.Log10(Mathf.Max(0.01f, ((BaseSimObject)actor).stats["mass_2"]) / 50f), 0.4f, 1.1f);
			}
			if (actor.hasTrait("weightless"))
			{
				value *= 1.15f;
			}
			return value;
		}

		internal static void Solve(float share, float mass, out float force, out float along, out int steps)
		{
			int steps2;
			float num = Peak(1.5f, 1f, out steps2) * share;
			float num2 = 0.05f;
			float num3 = 40f;
			if (Peak(num3, mass, out var steps3) <= num)
			{
				num2 = num3;
			}
			for (int i = 0; i < 40; i++)
			{
				if (!(num3 - num2 > 0.0001f))
				{
					break;
				}
				float num4 = (num2 + num3) * 0.5f;
				if (Peak(num4, mass, out steps3) < num)
				{
					num2 = num4;
				}
				else
				{
					num3 = num4;
				}
			}
			force = ((Mathf.Abs(share - 1f) < 0.0001f && Mathf.Abs(mass - 1f) < 0.0001f) ? 1.5f : ((num2 + num3) * 0.5f));
			Peak(force, mass, out steps);
			float num5 = 2.25f * (float)steps2 * Mathf.Sqrt(Mathf.Max(0.01f, share));
			along = num5 / Mathf.Max(0.001f, force * (float)Mathf.Max(1, steps));
		}

		internal static float Peak(float force, float mass, out int steps)
		{
			float num = 9.8f;
			float num2 = 0.02f;
			float num3 = 0.02f;
			try
			{
				num = SimGlobals.m.gravity;
				if (!_nominal)
				{
					num2 = World.world.fixed_delta_time;
					num3 = World.world.elapsed;
				}
			}
			catch
			{
			}
			if (num2 <= 0f)
			{
				num2 = 0.02f;
			}
			if (num3 <= 0f)
			{
				num3 = num2;
			}
			float num4 = Mathf.Min(mass * num, 20f);
			float num5 = 0f;
			float num6 = force;
			float num7 = 0f;
			steps = 0;
			while (steps < 2000)
			{
				steps++;
				if (num5 != 0f)
				{
					num5 -= num * mass * num3;
					if (num5 <= 0f)
					{
						num5 = 0f;
					}
				}
				num5 += num6 * num2 * num4;
				num6 -= num2 * num4 * 0.3f;
				if (num5 <= 0f)
				{
					break;
				}
				if (num5 > num7)
				{
					num7 = num5;
				}
			}
			return num7;
		}
	}
}
