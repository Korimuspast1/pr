using System;
using System.Collections;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class BonfireTest : CivilTest
	{
		protected override string Prefix => "BONFIRE";

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			if (!Check("open land", worldTile != null))
			{
				yield break;
			}
			Actor actor = Village("human", 4, 0, worldTile);
			if (!Check("a village", actor != null && actor.city != null))
			{
				yield break;
			}
			Building fire = AddHouse("bonfire", actor.city);
			if (!Check("a bonfire, built", fire != null && !fire.isUnderConstruction()))
			{
				yield break;
			}
			Actor you = ((Founded.Count > 1) ? Founded[1] : actor);
			yield return Possess(you);
			if (!Check("first person", InControl(you)))
			{
				yield break;
			}
			Vector2 at = fire.current_position + new Vector2(0f, -3f);
			Put(you, at);
			Mod.Day.TimeOfDay = 0.45f;
			SceneCollector.FireSpot spot = default(SceneCollector.FireSpot);
			bool seen = false;
			for (float t = 0f; t < 5f; t += 0.2f)
			{
				if (seen)
				{
					break;
				}
				yield return CivilTest.Wait(0.2f);
				SceneCollector.FireSpot[] bonfires = Mod.SceneForTest.Bonfires;
				for (int i = 0; i < bonfires.Length; i++)
				{
					SceneCollector.FireSpot fireSpot = bonfires[i];
					if ((fireSpot.At - fire.current_position).magnitude < 2f && (!seen || fireSpot.Radius > spot.Radius))
					{
						spot = fireSpot;
						seen = true;
					}
				}
			}
			if (!Check("its flames drawn", seen))
			{
				yield break;
			}
			Note($"flames at {spot.At} reach {spot.Radius:F2} top {spot.Top - spot.Ground:F2} over the ground; building at {fire.current_position}");
			float postAt = -1f;
			float num = float.MaxValue;
			Collider c = default(Collider);
			bool ok = false;
			foreach (Collider item in Colliders.Near)
			{
				if (item.Owner == fire)
				{
					c = item;
					ok = true;
				}
			}
			if (!Check("its collider near you", ok))
			{
				yield break;
			}
			for (int j = 0; j < 360; j++)
			{
				float num2 = (float)j * (MathF.PI / 180f);
				for (float num3 = 0.3f; num3 < 3f; num3 += 0.05f)
				{
					float num4 = Colliders.DistanceTo(in c, spot.At + new Vector2(Mathf.Cos(num2), Mathf.Sin(num2)) * num3);
					if (num4 < num)
					{
						num = num4;
						postAt = num2;
					}
				}
			}
			Note($"nearest of your collider: {postAt * 57.29578f:F0} deg from the middle (distance {num:F2})");
			int reached = 0;
			int burnt = 0;
			int stopped = 0;
			for (int k = 0; k < 8; k++)
			{
				float t = postAt + MathF.PI / 180f * (new float[8] { 180f, 0f, 30f, 60f, 90f, 120f, 150f, 210f })[k];
				Vector2 dir = new Vector2(Mathf.Cos(t), Mathf.Sin(t));
				try
				{
					you.finishStatusEffect("burning");
				}
				catch
				{
				}
				try
				{
					you.restoreHealth(you.getMaxHealth());
				}
				catch
				{
				}
				Put(you, spot.At - dir * 3f);
				Mod.SetYaw(t);
				yield return CivilTest.Wait(0.5f);
				try
				{
					you.finishStatusEffect("burning");
				}
				catch
				{
				}
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				float closest = float.MaxValue;
				bool burning = false;
				for (float t2 = 0f; t2 < 4f; t2 += Time.unscaledDeltaTime)
				{
					Mod.SetYaw(t);
					yield return null;
					float magnitude = (you.current_position - spot.At).magnitude;
					if (magnitude < closest)
					{
						closest = magnitude;
						_ = t2;
					}
					burning |= Torches.Burning(you);
					if (Vector2.Dot(you.current_position - spot.At, dir) > 0.6f)
					{
						break;
					}
				}
				PossessionHooks.ScriptedInput = Vector2.zero;
				bool flag = closest < spot.Radius;
				if (flag)
				{
					reached++;
				}
				else
				{
					stopped++;
				}
				if (burning)
				{
					burnt++;
				}
				Note(string.Format("from {0:F0} deg: came within {1:F2} of the middle ({2}), {3}", t * 57.29578f, closest, flag ? "into the fire" : "stopped", burning ? "caught fire" : "not alight"));
				if (!you.isAlive())
				{
					Note("you burnt to death: a new body");
					break;
				}
			}
			try
			{
				you.finishStatusEffect("burning");
			}
			catch
			{
			}
			Check("into the fire from every side off the spit's line", reached == 6, $"{reached} of 8 in, {stopped} stopped");
			Check("a post stops you on the spit's line, both ends", stopped == 2, $"{stopped} stopped");
			Check("walking into it sets you alight", burnt >= reached && reached > 0, $"{burnt} alight of {reached} in");
		}

		private static void Put(Actor a, Vector2 at)
		{
			try
			{
				a.current_position = at;
				a.dirty_current_tile = true;
			}
			catch
			{
			}
		}
	}
}
