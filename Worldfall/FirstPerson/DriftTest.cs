using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class DriftTest : CivilTest
	{
		private const float Watch = 5f;

		private const float Allowed = 0.35f;

		protected override string Prefix => "DRIFT";

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			if (!Check("open land", worldTile != null))
			{
				yield break;
			}
			Vector2 home = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f);
			CivilTest.ClearBox(worldTile.x - 8, worldTile.y - 8, worldTile.x + 8, worldTile.y + 8);
			List<string> list = new List<string>();
			foreach (ActorAsset item in AssetManager.actor_library.list)
			{
				if (item != null && !item.is_boat && !(item.id == "crabzilla"))
				{
					list.Add(item.id);
				}
			}
			string environmentVariable = Environment.GetEnvironmentVariable("DRIFT_ONLY");
			if (!string.IsNullOrEmpty(environmentVariable))
			{
				list = new List<string>(environmentVariable.Split(','));
			}
			Note(list.Count + " kinds");
			Actor left = null;
			int tried = 0;
			int moved = 0;
			List<string> walkers = new List<string>();
			foreach (string id in list)
			{
				Actor you = CivilTest.Spawn(id, home);
				if (you == null)
				{
					continue;
				}
				bool flag = false;
				try
				{
					flag = you.canBePossessed();
				}
				catch
				{
				}
				if (!flag)
				{
					CivilTest.Kill(you);
					continue;
				}
				yield return Possess(you, 0.5f);
				if (left != null && left != you)
				{
					CivilTest.Kill(left);
				}
				left = you;
				if (!InControl(you))
				{
					Note(id + ": not taken over");
					continue;
				}
				ClearNear(you, home);
				you.current_position = home;
				you.dirty_current_tile = true;
				yield return CivilTest.Wait(0.3f);
				Vector2 start = you.current_position;
				float most = 0f;
				string tasks = "";
				for (float t = 0f; t < 5f; t += Time.unscaledDeltaTime)
				{
					if (!you.isAlive())
					{
						break;
					}
					yield return null;
					if (!you.isAlive())
					{
						break;
					}
					try
					{
						you.setMaxHealth();
					}
					catch
					{
					}
					float magnitude = (you.current_position - start).magnitude;
					string text = null;
					try
					{
						text = ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)you.ai)?.task?.id;
					}
					catch
					{
					}
					if (text != null && !tasks.Contains(text))
					{
						tasks = tasks + ((tasks.Length > 0) ? "," : "") + text;
					}
					most = Mathf.Max(most, magnitude);
				}
				tried++;
				string text2 = "";
				try
				{
					text2 = string.Join(",", you.getStatusesIds().ToArray());
				}
				catch
				{
				}
				bool flag2 = most > 0.35f;
				if (flag2)
				{
					moved++;
					walkers.Add(id);
				}
				Note(string.Format("{0}: moved {1:F2} tiles by itself in {2:F0} s{3}, tasks [{4}], statuses [{5}], ", id, most, 5f, flag2 ? "  <-- MOVES" : "", tasks, text2) + $"flying {Flag(you)}, alive {you.isAlive()}");
			}
			if (left != null)
			{
				CivilTest.Kill(left);
			}
			Check("kinds taken over and watched", tried > 10, tried.ToString());
			Check("none moved by itself", moved == 0, moved + ": " + string.Join(", ", walkers.ToArray()));
		}

		private static string Flag(Actor a)
		{
			try
			{
				return ((BaseSimObject)a).isFlying().ToString();
			}
			catch
			{
				return "?";
			}
		}

		private static void ClearNear(Actor you, Vector2 at)
		{
			try
			{
				List<Actor> list = new List<Actor>();
				foreach (Actor unit in World.world.units)
				{
					if (unit != you && unit.isAlive() && (unit.current_position - at).sqrMagnitude < 144f)
					{
						list.Add(unit);
					}
				}
				foreach (Actor item in list)
				{
					CivilTest.Kill(item);
				}
			}
			catch
			{
			}
		}
	}
}
