using System.Collections;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class OwnMindTest : CivilTest
	{
		private static readonly string[] Kinds = new string[4] { "assimilator", "acid_blob", "bandit", "human" };

		private static readonly string[] Foes = new string[6] { "human", "bandit", "sheep", "wolf", "skeleton", "orc" };

		private const float Watch = 8f;

		private Vector2 _home;

		private Actor _you;

		private Actor _left;

		private int _foughtBefore;

		private int _measured;

		protected override string Prefix => "MIND";

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			if (Check("open land", worldTile != null))
			{
				_home = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f);
				CivilTest.ClearBox(worldTile.x - 6, worldTile.y - 6, worldTile.x + 6, worldTile.y + 6);
				string[] kinds = Kinds;
				foreach (string text in kinds)
				{
					yield return Part("a " + text + " with an enemy by it", Idle(text));
				}
				OwnMind.Enabled = true;
				Check("every kind had an enemy to stand by", _measured == Kinds.Length, $"{_measured} of {Kinds.Length}");
				Check("without the fix, the game's steps did fight for at least one of them", _foughtBefore > 0, $"{_foughtBefore} of {_measured}");
				yield return Part("a blow of your own still lands", OwnBlow());
				OwnMind.Enabled = true;
			}
		}

		private Actor Enemy(Actor you, out string kind)
		{
			string[] foes = Foes;
			foreach (string text in foes)
			{
				Actor actor = CivilTest.Spawn(text, _home + new Vector2(0.9f, 0f));
				if (actor != null)
				{
					bool flag = false;
					try
					{
						flag = you.areFoes(actor) && ((BaseSimObject)you).canAttackTarget((BaseSimObject)actor, pCheckForFactions: true, pAttackBuildings: false);
					}
					catch
					{
					}
					if (flag)
					{
						kind = text;
						return actor;
					}
					CivilTest.Kill(actor);
				}
			}
			kind = null;
			return null;
		}

		private IEnumerator Idle(string kind)
		{
			Actor you = CivilTest.Spawn(kind, _home + new Vector2(0f, 0.5f));
			if (!Check("a " + kind, you != null))
			{
				yield break;
			}
			if (_left != null && _left.isAlive() && ControllableUnit.isControllingUnit())
			{
				try
				{
					FirstPerson.Possess.Swap(you);
				}
				catch
				{
				}
				CivilTest.Kill(_left);
			}
			yield return Possess(you);
			_left = you;
			if (!Check("first person in the " + kind, InControl(you)))
			{
				yield break;
			}
			if (_you == null)
			{
				Check("the game's fighting steps are taken over", OwnMind.Installed);
			}
			_you = you;
			int[] blows = new int[2];
			bool measured = true;
			bool[] array = new bool[2] { false, true };
			foreach (bool on in array)
			{
				OwnMind.Enabled = on;
				you.current_position = _home;
				you.dirty_current_tile = true;
				try
				{
					you.cancelAllBeh();
				}
				catch
				{
				}
				string foeKind;
				Actor foe = Enemy(you, out foeKind);
				if (!Check("an enemy of the " + kind + " by it (" + (foeKind ?? ("none of " + string.Join(", ", Foes))) + ")", foe != null))
				{
					measured = false;
					break;
				}
				bool foes = true;
				double last = you.last_attack_timestamp;
				int struck = 0;
				int sighted = 0;
				int samples = 0;
				int foeAfterYou = 0;
				float most = 0f;
				float watched = 0f;
				Vector2 start = you.current_position;
				for (; watched < 8f; watched += Time.unscaledDeltaTime)
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
					if (foe.isAlive())
					{
						try
						{
							foe.setMaxHealth();
						}
						catch
						{
						}
						if ((foe.current_position - you.current_position).sqrMagnitude > 2.5600002f)
						{
							foe.current_position = you.current_position + new Vector2(0.9f, 0f);
							foe.dirty_current_tile = true;
						}
						if (foe.has_attack_target && foe.attack_target == you)
						{
							foeAfterYou++;
						}
					}
					if (you.last_attack_timestamp != last)
					{
						struck++;
						last = you.last_attack_timestamp;
					}
					samples++;
					if (you.has_attack_target)
					{
						sighted++;
					}
					most = Mathf.Max(most, (you.current_position - start).magnitude);
				}
				string text = null;
				try
				{
					text = ((you.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)you.ai).task != null) ? ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)you.ai).task.id : null);
				}
				catch
				{
				}
				if (watched < 7.2f)
				{
					measured = false;
				}
				Note(string.Format("{0} {1} OwnMind, {2} by it (foes {3}): {4} blows struck by itself in {5:F1} s, ", kind, on ? "with" : "without", foeKind, foes, struck, watched) + $"sights on someone {100f * (float)sighted / (float)Mathf.Max(1, samples):F0}% of the time, the {foeKind} after it {100f * (float)foeAfterYou / (float)Mathf.Max(1, samples):F0}%, " + string.Format("moved {0:F2} tiles, task {1}, possessed {2}", most, text ?? "none", ((BaseSimObject)you).hasStatus("possessed")));
				blows[on ? 1u : 0u] = struck;
				CivilTest.Kill(foe);
				yield return CivilTest.Wait(0.5f);
				foeKind = null;
			}
			OwnMind.Enabled = true;
			if (Check("the " + kind + " was watched the whole time", measured && you.isAlive()))
			{
				_measured++;
				if (blows[0] > 0)
				{
					_foughtBefore++;
				}
				Check("the " + kind + " strikes no blow by itself", blows[1] == 0, $"{blows[1]} with OwnMind, {blows[0]} without");
				Check("the " + kind + " is still yours", InControl(you) && ((BaseSimObject)you).hasStatus("possessed"));
			}
		}

		private IEnumerator OwnBlow()
		{
			Actor you = _you;
			if (!Check("still in someone", you != null && you.isAlive() && InControl(you)))
			{
				yield break;
			}
			you.current_position = _home;
			you.dirty_current_tile = true;
			Mod.SetYaw(0f);
			Mod.SetPitchDegrees(-10f);
			for (int i = 0; i < 6; i++)
			{
				yield return null;
			}
			Actor sheep = CivilTest.Spawn("sheep", _home + new Vector2(1f, 0f));
			if (Check("a sheep in front", sheep != null))
			{
				for (int i = 0; i < 20; i++)
				{
					Hold(sheep, _home + new Vector2(1f, 0f));
					Face(sheep);
					yield return null;
				}
				float before = Health(sheep);
				PossessionCombat.AttackForTest(you);
				for (int i = 0; i < 24; i++)
				{
					Hold(sheep, _home + new Vector2(1f, 0f));
					yield return null;
				}
				float num = Health(sheep);
				Check("your click lands on the sheep", PossessionCombat.LastBlowAt == sheep && num < before, string.Format("blow at {0}, sheep {1}->{2}", (PossessionCombat.LastBlowAt != null) ? WorldBoxMod.Describe(PossessionCombat.LastBlowAt) : "nothing", before, num));
				CivilTest.Kill(sheep);
			}
		}

		private static void Hold(Actor a, Vector2 at)
		{
			if (a != null && a.isAlive())
			{
				try
				{
					a.cancelAllBeh();
					a.makeWait(5f);
				}
				catch
				{
				}
				a.current_position = at;
				a.dirty_current_tile = true;
			}
		}

		private void Face(Actor target)
		{
			if (target != null && Hitboxes.Get(target, out var box))
			{
				Vector3 eyeForTest = Mod.EyeForTest;
				float num = box.Centre.X - eyeForTest.x;
				float num2 = box.Centre.Y - eyeForTest.y;
				float num3 = Mathf.Max(0.05f, Mathf.Sqrt(num * num + num2 * num2));
				Mod.SetYaw(Mathf.Atan2(num2, num));
				Mod.SetPitchDegrees(Mathf.Clamp(Mathf.Atan((box.Centre.Z - eyeForTest.z) / num3) * 57.29578f, -44f, 44f));
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
	}
}
