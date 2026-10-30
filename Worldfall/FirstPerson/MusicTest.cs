using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class MusicTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private Actor _king;

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			bool quit = File.ReadAllText(path).Contains("quit");
			while (!Config.game_loaded || World.world == null || World.world.units == null)
			{
				yield return null;
			}
			for (float t = 0f; t < 60f; t += 1f)
			{
				bool flag = false;
				try
				{
					flag = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag && t >= 4f)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(1f);
			}
			Config.autosaves = false;
			_mod = WorldBoxMod.Instance;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				_king = KingTest.FoundKingdom(item, "human", 8, 0);
				if (_king != null)
				{
					break;
				}
			}
			if (_king == null)
			{
				Log.Error("TUNES could not found a village");
				Finish(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(1f);
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("TUNES first person did not start");
				Finish(quit);
				yield break;
			}
			_mod.HintUntil = 0f;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			FirstPersonAudio a = _mod.Audio;
			a.CalmNowForTest();
			float waited = 0f;
			while (!a.CalmOn && waited < 60f)
			{
				yield return new WaitForSecondsRealtime(0.5f);
				waited += 0.5f;
			}
			Expect(string.Format("a calm piece ({0}) plays: {1} after {2:F1} s", a.Mood, a.CalmName ?? "none", waited), a.CalmOn);
			yield return new WaitForSecondsRealtime(3f);
			for (int round = 0; round < 2; round++)
			{
				bool win = round == 1;
				Log.Info(string.Format("TUNES --- fight {0} ({1})", round + 1, win ? "won" : "survived"));
				try
				{
					((BaseSimObject)_king).addStatusEffect("invincible", 120f, pColorEffect: false);
				}
				catch
				{
				}
				_ = a.LastEnding;
				a.LastEnding = null;
				List<Actor> bandits = new List<Actor>();
				Vector2 current_position = _king.current_position;
				for (int i = 0; i < 4; i++)
				{
					try
					{
						Actor actor = World.world.units.spawnNewUnit("bandit", World.world.GetTile((int)current_position.x + 3 + i % 2, (int)current_position.y + 2 + i / 2), pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
						if (actor != null)
						{
							bandits.Add(actor);
							actor.addAggro(_king);
							((BaseSimObject)actor).addStatusEffect("invincible", 30f, pColorEffect: false);
						}
					}
					catch (Exception ex)
					{
						Log.Error("TUNES bandits: " + ex.Message);
					}
				}
				float t = 0f;
				bool battle = false;
				for (float t2 = 0f; t2 < 14f; t2 += 0.5f)
				{
					yield return new WaitForSecondsRealtime(0.5f);
					battle |= a.BattleOn;
					t = Mathf.Max(t, a.LayerLevel(0));
					int num = 0;
					foreach (Actor item2 in bandits)
					{
						try
						{
							if (item2 != null && item2.isAlive())
							{
								num++;
							}
						}
						catch
						{
						}
					}
					if (t2 % 2f < 0.01f)
					{
						Log.Info($"TUNES fight {t2:F0}s: danger {a.Danger:F2} (now {a.DangerNow:F2}), battle {a.BattleOn}, layers {a.LayerLevel(0):F2} {a.LayerLevel(1):F2} {a.LayerLevel(2):F2}, bandits alive {num}");
					}
				}
				Expect($"the battle music came in (drums up to {t:F2})", battle && t > 0.25f);
				Expect("...and didn't end while the bandits still fought (ended: " + (a.LastEnding ?? "no") + ")", a.LastEnding == null && a.BattleOn);
				foreach (Actor item3 in bandits)
				{
					try
					{
						if (item3 != null && item3.isAlive())
						{
							item3.finishStatusEffect("invincible");
							item3.dieSimpleNone();
						}
					}
					catch
					{
					}
				}
				if (win)
				{
					_mod.Feedback.Kills++;
				}
				float endedAt = -1f;
				float restarted = -1f;
				bool sawEnding = false;
				for (float t2 = 0f; t2 < 40f; t2 += 0.25f)
				{
					yield return new WaitForSecondsRealtime(0.25f);
					if (a.LastEnding != null && endedAt < 0f)
					{
						endedAt = t2;
					}
					if (endedAt >= 0f && a.Heard(4) > 0f)
					{
						sawEnding = true;
					}
					if (endedAt >= 0f && a.BattleOn && restarted < 0f)
					{
						restarted = t2;
					}
					if (t2 % 2f < 0.01f)
					{
						Log.Info($"TUNES after {t2:F0}s: danger {a.Danger:F2}, battle {a.BattleOn}, layers heard {a.Heard(0):F2} {a.Heard(1):F2} {a.Heard(2):F2}, ending heard {a.Heard(4):F2} [{a.LastEnding}]");
					}
					if (endedAt >= 0f && t2 > endedAt + 8f)
					{
						break;
					}
				}
				string text = (win ? "battle_win" : "battle_end");
				Expect("the fight's music ended with " + (a.LastEnding ?? "nothing") + " " + ((endedAt >= 0f) ? (endedAt.ToString("F1") + " s after") : "") + " (want " + text + ")", a.LastEnding == text);
				Expect("the ending was heard", sawEnding);
				Expect("the battle didn't start again as the danger died down", restarted < 0f);
				yield return new WaitForSecondsRealtime(3f);
			}
			Finish(quit);
			void Expect(string what, bool ok)
			{
				Log.Info("TUNES " + (ok ? "PASS" : "FAIL") + ": " + what);
			}
		}

		private static void Finish(bool quit)
		{
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("TUNES DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
