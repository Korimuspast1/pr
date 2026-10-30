using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class NpcTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private WorldTile _open;

		private WorldTile _site;

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(path);
			bool quit = options.Contains("quit");
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
			Log.Info($"NPCTEST world {MapBox.width}x{MapBox.height}, units {World.world.units.Count}");
			Actor king = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item, "human", 14, 2);
				if (king != null)
				{
					_site = item;
					break;
				}
			}
			if (king == null)
			{
				Log.Error("NPCTEST could not found a village");
				Finish(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(2f);
			Actor me = null;
			foreach (Actor unit in king.kingdom.getUnits())
			{
				if (unit != null && unit != king && unit.isAlive() && unit.isAdult() && !unit.isWarrior())
				{
					me = unit;
					break;
				}
			}
			if (me == null)
			{
				Log.Error("NPCTEST no villager to take over");
				Finish(quit);
				yield break;
			}
			ControllableUnit.setControllableCreature(me);
			yield return new WaitForSecondsRealtime(2f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("NPCTEST first person did not start");
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
			_mod.Settings.TimeMode = 1;
			_mod.ForceRelight();
			Guards.Verbose = true;
			Steering.Verbose = true;
			if (options.Contains("walkers"))
			{
				_open = ToOpenLand(me);
				yield return Run("walkers", Walkers(me));
				yield return Run("defend", Defend(me, king));
			}
			else
			{
				yield return Run("stature", Stature(me));
				yield return Run("blocks", Blocks(me));
				yield return Run("walkers", Walkers(me));
				yield return Run("defend", Defend(me, king));
				yield return Run("leap", Leap(me, king));
			}
			Log.Info($"NPCTEST render {_mod.RenderMs:F1} ms, walkers let through after getting stuck: {Steering.Ghosted}");
			Finish(quit);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("NPCTEST --- " + name);
			while (true)
			{
				object current;
				try
				{
					if (!part.MoveNext())
					{
						break;
					}
					current = part.Current;
					goto IL_007b;
				}
				catch (Exception ex)
				{
					Log.Error("NPCTEST " + name + " failed: " + ex);
				}
				break;
				IL_007b:
				yield return current;
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			PossessionHooks.ScriptedRun = false;
			_mod.DebugEyeSet = false;
		}

		private float RawHeight(Actor a)
		{
			SpriteImage spriteImage = _mod.SpriteForTest(a);
			if (spriteImage == null)
			{
				return 0f;
			}
			return spriteImage.UnitHeight * spriteImage.VisibleFraction * Mathf.Abs(a.current_scale.y);
		}

		private IEnumerator Stature(Actor me)
		{
			_open = ToOpenLand(me);
			yield return new WaitForSecondsRealtime(0.5f);
			Actor walker = null;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple != me && simple.isAlive() && simple.is_moving && People.IsHumanoid(simple) && (simple.current_position - me.current_position).sqrMagnitude < 900f)
				{
					walker = simple;
					break;
				}
			}
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			_mod.SetYaw(UnityEngine.Random.value * MathF.PI * 2f);
			yield return new WaitForSecondsRealtime(0.5f);
			float rawLo = 99f;
			float rawHi = 0f;
			float keptLo = 99f;
			float keptHi = 0f;
			float wLo = 99f;
			float wHi = 0f;
			float wkLo = 99f;
			float wkHi = 0f;
			float jerk = 0f;
			float lastEye = float.NaN;
			float lastStep = 0f;
			TerrainCache terrain = _mod.TerrainForTest;
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 3f)
			{
				yield return null;
				float num = RawHeight(me);
				float b = FirstPerson.Stature.Peek(me, num);
				rawLo = Mathf.Min(rawLo, num);
				rawHi = Mathf.Max(rawHi, num);
				keptLo = Mathf.Min(keptLo, b);
				keptHi = Mathf.Max(keptHi, b);
				Vector2 current_position = me.current_position;
				float num2 = _mod.View.Z - terrain.SampleHeight(current_position.x, current_position.y);
				if (!float.IsNaN(lastEye))
				{
					float num3 = num2 - lastEye;
					jerk = Mathf.Max(jerk, Mathf.Abs(num3 - lastStep));
					lastStep = num3;
				}
				lastEye = num2;
				if (walker != null && walker.isAlive())
				{
					float num4 = RawHeight(walker);
					float b2 = FirstPerson.Stature.Peek(walker, num4);
					wLo = Mathf.Min(wLo, num4);
					wHi = Mathf.Max(wHi, num4);
					wkLo = Mathf.Min(wkLo, b2);
					wkHi = Mathf.Max(wkHi, b2);
				}
			}
			PossessionHooks.ScriptedInput = Vector2.zero;
			Log.Info($"NPCTEST walking: sprite height {rawLo:F3}..{rawHi:F3} (the eye used to follow this), kept {keptLo:F3}..{keptHi:F3}; biggest frame-to-frame jolt of the eye {jerk:F4}");
			if (walker != null)
			{
				Log.Info($"NPCTEST a villager walking by ({WorldBoxMod.Describe(walker)}): sprite height {wLo:F3}..{wHi:F3}, drawn at {wkLo:F3}..{wkHi:F3}");
			}
		}

		private IEnumerator Blocks(Actor me)
		{
			Vector2 current_position = me.current_position;
			WorldTile mountain = null;
			for (int i = 2; i < 80; i++)
			{
				if (mountain != null)
				{
					break;
				}
				for (int j = 0; j < 32; j++)
				{
					if (mountain != null)
					{
						break;
					}
					float f = (float)j / 32f * MathF.PI * 2f;
					WorldTile tile = World.world.GetTile((int)(current_position.x + Mathf.Cos(f) * (float)i), (int)(current_position.y + Mathf.Sin(f) * (float)i));
					if (tile != null && PossessionHooks.IsBlock(tile) && !tile.Type.liquid)
					{
						mountain = tile;
					}
				}
			}
			if (mountain == null)
			{
				Log.Info("NPCTEST no mountain or wall nearby, skipped");
				yield break;
			}
			me.setCurrentTilePosition(mountain);
			float high = 0f;
			float t0 = Time.realtimeSinceStartup;
			while (Time.realtimeSinceStartup - t0 < 2.5f)
			{
				yield return null;
				high = Mathf.Max(high, me.position_height);
			}
			WorldTile tileAt = Toolbox.getTileAt(me.current_position.x, me.current_position.y);
			Log.Info("NPCTEST set down on a " + mountain.Type.id + " tile: now on " + ((tileAt != null) ? tileAt.Type.id : "?") + " (" + (PossessionHooks.IsBlock(tileAt) ? "on it" : "off it") + ", should be " + ((Climbing.IsRock(mountain) && Climbing.On) ? "on" : "off") + "), " + $"{(me.current_position - new Vector2(mountain.posV3.x, mountain.posV3.y)).magnitude:F2} tiles from it, highest hop {high:F2}");
			_mod.SaveFrame("npc_mountain.png");
		}

		private IEnumerator Walkers(Actor me)
		{
			WorldTile worldTile = _open ?? ToOpenLand(me);
			if (worldTile == null)
			{
				Log.Error("NPCTEST no open land");
				yield break;
			}
			me.setCurrentTilePosition(worldTile);
			yield return new WaitForSecondsRealtime(0.5f);
			Vector2 current_position = me.current_position;
			Building house = null;
			Vector2Int[] array = new Vector2Int[6]
			{
				new Vector2Int(7, 0),
				new Vector2Int(7, 3),
				new Vector2Int(7, -3),
				new Vector2Int(9, 0),
				new Vector2Int(-7, 0),
				new Vector2Int(0, 7)
			};
			for (int i = 0; i < array.Length; i++)
			{
				Vector2Int vector2Int = array[i];
				WorldTile tile = World.world.GetTile((int)current_position.x + vector2Int.x, (int)current_position.y + vector2Int.y);
				if (tile == null)
				{
					continue;
				}
				try
				{
					house = World.world.buildings.addBuilding("house_human_2", tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					house?.completeConstruction();
					if (house != null && me.kingdom != null)
					{
						house.setKingdom(me.kingdom);
					}
				}
				catch (Exception ex)
				{
					Log.Error("NPCTEST house: " + ex.Message);
				}
				if (house != null)
				{
					break;
				}
			}
			if (house == null)
			{
				Log.Error("NPCTEST no house placed");
				yield break;
			}
			yield return new WaitForSecondsRealtime(1f);
			Vector2 hp = new Vector2(house.current_tile.posV3.x, house.current_tile.posV3.y);
			Royal.RegisterTask();
			List<Actor> walkers = new List<Actor>();
			List<WorldTile> goals = new List<WorldTile>();
			for (int j = 0; j < 5; j++)
			{
				try
				{
					WorldTile tile2 = World.world.GetTile((int)hp.x - 4, (int)hp.y - 1 + j);
					Actor actor = World.world.units.spawnNewUnit("human", tile2, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
					if (actor != null)
					{
						if (me.city != null)
						{
							actor.joinCity(me.city);
						}
						WorldTile item = ((j < 4) ? World.world.GetTile((int)hp.x + 5, (int)hp.y - 1 + j) : house.current_tile);
						walkers.Add(actor);
						goals.Add(item);
					}
				}
				catch (Exception ex2)
				{
					Log.Error("NPCTEST walker: " + ex2.Message);
				}
			}
			yield return new WaitForSecondsRealtime(0.3f);
			for (int k = 0; k < walkers.Count; k++)
			{
				walkers[k].cancelAllBeh();
				walkers[k].setTask("fp_royal_march");
				walkers[k].beh_tile_target = goals[k];
			}
			_mod.DebugEye = hp + new Vector2(0f, -9f);
			_mod.DebugEyeHeight = 7f;
			_mod.DebugEyeSet = true;
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(-32f);
			float[] deepest = new float[walkers.Count];
			bool[] reached = new bool[walkers.Count];
			int ghosts = Steering.Ghosted;
			float t0 = Time.realtimeSinceStartup;
			int shot = 0;
			while (Time.realtimeSinceStartup - t0 < 12f)
			{
				yield return null;
				for (int l = 0; l < walkers.Count; l++)
				{
					Actor actor2 = walkers[l];
					if (actor2 == null || !actor2.isAlive())
					{
						continue;
					}
					if (actor2.is_inside_building)
					{
						reached[l] = true;
						continue;
					}
					Vector2 current_position2 = actor2.current_position;
					if ((current_position2 - new Vector2(goals[l].posV3.x, goals[l].posV3.y)).sqrMagnitude < 1f)
					{
						reached[l] = true;
					}
					if (l < 4 && !reached[l])
					{
						deepest[l] = Mathf.Max(deepest[l], Steering.OverlapForTest(current_position2, 0.12f));
					}
				}
				if (Time.realtimeSinceStartup - t0 > (float)shot * 1.5f && shot < 6)
				{
					_mod.SaveFrame($"npc_walkers_{shot++}.png");
				}
			}
			for (int m = 0; m < walkers.Count; m++)
			{
				Log.Info(string.Format("NPCTEST walker {0} ({1}): {2}", m, (m < 4) ? "past the house" : "into the house", reached[m] ? "got there" : "DID NOT GET THERE") + ((m < 4) ? $", deepest into anything solid {deepest[m]:F2} tiles" : ""));
			}
			Log.Info($"NPCTEST walkers let through after getting stuck: {Steering.Ghosted - ghosts}; steering costs {Steering.CostMs:F2} ms a frame for {Steering.Walkers} walkers");
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			foreach (Actor item2 in walkers)
			{
				try
				{
					if (item2 != null && item2.isAlive())
					{
						item2.cancelAllBeh();
					}
				}
				catch
				{
				}
			}
		}

		private IEnumerator Defend(Actor me, Actor king)
		{
			if (_open != null)
			{
				me.setCurrentTilePosition(_open);
			}
			yield return new WaitForSecondsRealtime(0.3f);
			Vector2 p = me.current_position;
			WorldTile kt = World.world.GetTile((int)p.x + 4, (int)p.y + 3);
			king.setCurrentTilePosition(kt);
			king.cancelAllBeh();
			try
			{
				king.setHealth(king.getMaxHealth());
			}
			catch
			{
			}
			List<Actor> subjects = new List<Actor>();
			for (int i = 0; i < 5; i++)
			{
				try
				{
					WorldTile tile = World.world.GetTile(kt.x - 3 + i, kt.y + 3);
					Actor actor = World.world.units.spawnNewUnit("human", tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
					if (actor != null && king.city != null)
					{
						actor.joinCity(king.city);
						subjects.Add(actor);
					}
				}
				catch (Exception ex)
				{
					Log.Error("NPCTEST subject: " + ex.Message);
				}
			}
			yield return new WaitForSecondsRealtime(0.5f);
			Actor bandit = World.world.units.spawnNewUnit("bandit", World.world.GetTile(kt.x + 2, kt.y), pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			if (bandit == null)
			{
				Log.Error("NPCTEST no bandit");
				yield break;
			}
			bandit.startFightingWith(king);
			_mod.SetYaw(Mathf.Atan2(kt.posV3.y - p.y, kt.posV3.x - p.x));
			_mod.SetPitchDegrees(-4f);
			int most = 0;
			float t0 = Time.realtimeSinceStartup;
			int shot = 0;
			int health = king.getHealth();
			bool struck = false;
			while (Time.realtimeSinceStartup - t0 < 6f)
			{
				yield return null;
				if (!struck && Time.realtimeSinceStartup - t0 > 1f)
				{
					struck = true;
					if (king.getHealth() >= health)
					{
						((BaseSimObject)king).getHit(5f, pFlash: true, AttackType.Weapon, (BaseSimObject)bandit, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: true);
					}
				}
				int num = 0;
				foreach (Actor unit in king.kingdom.getUnits())
				{
					if (unit != null && unit.isAlive() && unit.has_attack_target && unit.attack_target == bandit)
					{
						num++;
					}
				}
				most = Mathf.Max(most, num);
				if (Time.realtimeSinceStartup - t0 > 1f + (float)shot && shot < 3)
				{
					_mod.SaveFrame($"npc_defend_{shot++}.png");
				}
			}
			Log.Info($"NPCTEST the king attacked by a bandit: {most} of his people went for it ({subjects.Count} spawned beside him) (last rally called {Guards.LastRallied}), king alive {king.isAlive()}, bandit alive {bandit.isAlive()}");
			try
			{
				if (bandit.isAlive())
				{
					bandit.dieSimpleNone();
				}
			}
			catch
			{
			}
		}

		private IEnumerator Leap(Actor me, Actor king)
		{
			if (king == null || !king.isAlive())
			{
				Log.Error("NPCTEST no king to leap into");
				yield break;
			}
			Vector2 vector = king.current_position - me.current_position;
			_mod.SetYaw(Mathf.Atan2(vector.y, vector.x));
			yield return new WaitForSecondsRealtime(0.5f);
			Log.Info($"NPCTEST can leap into the king: {Possess.CanJumpTo(king)}, into yourself: {Possess.CanJumpTo(me)}");
			bool ok = Possess.JumpTo(king);
			for (int i = 0; i < 4; i++)
			{
				yield return new WaitForSecondsRealtime(0.12f);
				_mod.SaveFrame($"npc_leap_{i}.png");
			}
			yield return new WaitForSecondsRealtime(1f);
			string text = null;
			try
			{
				text = ((me.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)me.ai).task != null) ? ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)me.ai).task.id : "none");
			}
			catch
			{
			}
			Log.Info(string.Format("NPCTEST leapt: {0}; you are {1} (the king: {2}), ", ok, (ControllableUnit._unit_main != null) ? WorldBoxMod.Describe(ControllableUnit._unit_main) : "nobody", ControllableUnit._unit_main == king) + string.Format("first person host is the king: {0}; the body left behind still possessed: {1}, controlled: {2}, its task now: {3}", _mod.Host == king, ((BaseSimObject)me).hasStatus("possessed"), ControllableUnit.isControllingUnit(me), text));
			_mod.SaveFrame("npc_leap_done.png");
			Vector2 current_position = king.current_position;
			Actor bandit = World.world.units.spawnNewUnit("bandit", World.world.GetTile((int)current_position.x + 2, (int)current_position.y), pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			if (bandit == null)
			{
				yield break;
			}
			try
			{
				king.setHealth(king.getMaxHealth());
			}
			catch
			{
			}
			bandit.startFightingWith(king);
			int most = 0;
			float t0 = Time.realtimeSinceStartup;
			int health = king.getHealth();
			bool struck = false;
			while (Time.realtimeSinceStartup - t0 < 5f)
			{
				yield return null;
				if (!struck && Time.realtimeSinceStartup - t0 > 1f)
				{
					struck = true;
					Log.Info("NPCTEST after a second the bandit " + ((king.getHealth() < health) ? "has hurt you" : "hasn't touched you: it strikes now"));
					if (king.getHealth() >= health)
					{
						((BaseSimObject)king).getHit(5f, pFlash: true, AttackType.Weapon, (BaseSimObject)bandit, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: true);
					}
				}
				int num = 0;
				foreach (Actor unit in king.kingdom.getUnits())
				{
					if (unit != null && unit.isAlive() && unit.has_attack_target && unit.attack_target == bandit)
					{
						num++;
					}
				}
				most = Mathf.Max(most, num);
			}
			Log.Info($"NPCTEST you (the king) attacked: {most} of your people went for the bandit; toast \"{_mod.ToastText}\"");
			try
			{
				if (bandit.isAlive())
				{
					bandit.dieSimpleNone();
				}
			}
			catch
			{
			}
		}

		private WorldTile ToOpenLand(Actor unit)
		{
			Vector2 current_position = unit.current_position;
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			for (int i = 0; i < 200; i += 2)
			{
				for (int j = 0; j < 32; j++)
				{
					float f = (float)j / 32f * MathF.PI * 2f;
					int num5 = (int)(current_position.x + Mathf.Cos(f) * (float)i);
					int num6 = (int)(current_position.y + Mathf.Sin(f) * (float)i);
					bool flag = num5 > 12 && num6 > 12 && num5 < MapBox.width - 16 && num6 < MapBox.height - 12;
					num4++;
					if (!flag)
					{
						num++;
					}
					for (int k = -5; k <= 5 && flag; k++)
					{
						for (int l = -6; l <= 13 && flag; l++)
						{
							WorldTile tile = World.world.GetTile(num5 + l, num6 + k);
							flag = tile != null && tile.Type != null && !tile.Type.liquid && !tile.Type.block && !tile.Type.lava;
							if (!flag)
							{
								num2++;
								break;
							}
							if (((l >= -1 && l <= 1 && k >= -1 && k <= 1) || (l >= 4 && l <= 10 && k >= -3 && k <= 4)) && tile.building != null && AssetStyle.IsSolid(tile.building))
							{
								flag = false;
								num3++;
							}
						}
					}
					if (flag)
					{
						WorldTile tile2 = World.world.GetTile(num5, num6);
						try
						{
							unit.setCurrentTilePosition(tile2);
						}
						catch (Exception ex)
						{
							Log.Error("NPCTEST move: " + ex.Message);
						}
						Log.Info($"NPCTEST open ground at {num5},{num6}");
						return tile2;
					}
				}
			}
			Log.Info($"NPCTEST no open ground: {num4} spots tried, {num} off the map, {num2} with water, mountains or lava, {num3} with something in the way; using the village site");
			if (_site == null)
			{
				return null;
			}
			try
			{
				unit.setCurrentTilePosition(_site);
			}
			catch
			{
			}
			return _site;
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
			Log.Info("NPCTEST DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
