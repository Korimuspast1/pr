using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class ArcheryTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

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
			Actor king = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item, "human", 6, 0);
				if (king != null)
				{
					break;
				}
			}
			if (king == null)
			{
				Log.Error("ARCH could not found a village");
				Finish(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(1f);
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(2f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("ARCH first person did not start");
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
			Settings s = _mod.Settings;
			int savedMode = s.TimeMode;
			s.TimeMode = 1;
			_mod.ForceRelight();
			yield return Run("archers", Archers(king));
			yield return Run("wall", Wall(king));
			s.TimeMode = savedMode;
			Finish(quit);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("ARCH --- " + name);
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
					Log.Error("ARCH " + name + " failed: " + ex);
				}
				break;
				IL_007b:
				yield return current;
			}
			_mod.DebugEyeSet = false;
		}

		private IEnumerator Archers(Actor king)
		{
			Vector2 current_position = king.current_position;
			int kx = (int)current_position.x;
			int ky = (int)current_position.y;
			List<Actor> archers = new List<Actor>();
			for (int i = 0; i < 3; i++)
			{
				try
				{
					WorldTile tile = World.world.GetTile(kx - 1 + i, ky + 4);
					Actor actor = World.world.units.spawnNewUnit("human", tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
					if (actor != null)
					{
						if (king.city != null)
						{
							actor.joinCity(king.city);
						}
						if (Equip(actor, "bow_wood"))
						{
							archers.Add(actor);
						}
					}
				}
				catch (Exception ex)
				{
					Log.Error("ARCH archer: " + ex.Message);
				}
			}
			List<Actor> bandits = new List<Actor>();
			for (int j = 0; j < 2; j++)
			{
				try
				{
					Actor actor2 = World.world.units.spawnNewUnit("bandit", World.world.GetTile(kx + 9, ky + 3 + j * 2), pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
					if (actor2 != null)
					{
						bandits.Add(actor2);
					}
				}
				catch (Exception ex2)
				{
					Log.Error("ARCH bandit: " + ex2.Message);
				}
			}
			if (archers.Count == 0 || bandits.Count == 0)
			{
				Log.Error($"ARCH archers {archers.Count}, bandits {bandits.Count}");
				yield break;
			}
			yield return new WaitForSecondsRealtime(0.3f);
			_mod.DebugEye = new Vector2((float)kx + 0.5f, (float)ky + 1.9f);
			_mod.DebugEyeSet = true;
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(0f);
			int lodged0 = StuckArrows.LodgedCount;
			int fixed0 = StuckArrows.FixedCount;
			for (int k = 0; k < archers.Count; k++)
			{
				try
				{
					archers[k].startFightingWith(bandits[k % bandits.Count]);
				}
				catch (Exception ex3)
				{
					Log.Error("ARCH fight: " + ex3.Message);
				}
			}
			foreach (Actor item in bandits)
			{
				try
				{
					item.startFightingWith(archers[0]);
				}
				catch
				{
				}
			}
			float t0 = Time.realtimeSinceStartup;
			int shot = 0;
			int projectiles = 0;
			int mostLodged = 0;
			while (Time.realtimeSinceStartup - t0 < 7f)
			{
				yield return null;
				Actor actor3 = null;
				foreach (Actor item2 in bandits)
				{
					try
					{
						if (item2.isAlive())
						{
							actor3 = item2;
							break;
						}
					}
					catch
					{
					}
				}
				foreach (Actor item3 in archers)
				{
					try
					{
						if (actor3 != null && item3.isAlive() && !item3.has_attack_target)
						{
							item3.startFightingWith(actor3);
						}
					}
					catch
					{
					}
				}
				if (World.world.projectiles != null)
				{
					projectiles = Mathf.Max(projectiles, World.world.projectiles.list.Count);
				}
				mostLodged = Mathf.Max(mostLodged, StuckArrows.LodgedCount);
				if (shot == 5)
				{
					_mod.DebugEye = new Vector2((float)kx - 3.2f, (float)ky + 3.1f);
					_mod.SetYaw(0.12f);
				}
				if (Time.realtimeSinceStartup - t0 > 0.6f + (float)shot * 0.5f && shot < 10)
				{
					_mod.SaveFrame($"arch_fight_{shot++:D2}.png");
				}
			}
			int num = 0;
			int num2 = 0;
			foreach (Actor item4 in archers)
			{
				try
				{
					if (item4.has_attack_target)
					{
						num++;
					}
				}
				catch
				{
				}
			}
			foreach (Actor item5 in bandits)
			{
				try
				{
					if (item5.isAlive())
					{
						num2++;
					}
				}
				catch
				{
				}
			}
			Log.Info($"ARCH archers {archers.Count}, aiming {num}, most arrows in the air {projectiles}, bandits alive {num2} of {bandits.Count}, lodged in bodies {lodged0} -> {StuckArrows.LodgedCount} (most {mostLodged}), stuck about {fixed0} -> {StuckArrows.FixedCount}");
			foreach (Actor item6 in archers)
			{
				try
				{
					if (item6.isAlive())
					{
						item6.dieSimpleNone();
					}
				}
				catch
				{
				}
			}
			foreach (Actor item7 in bandits)
			{
				try
				{
					if (item7.isAlive())
					{
						item7.dieSimpleNone();
					}
				}
				catch
				{
				}
			}
			yield return new WaitForSecondsRealtime(0.3f);
			Actor sheep = null;
			try
			{
				sheep = World.world.units.spawnNewUnit("sheep", World.world.GetTile(kx + 2, ky - 3), pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex4)
			{
				Log.Error("ARCH sheep: " + ex4.Message);
			}
			if (sheep == null)
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(0.3f);
			Vector2 current_position2 = sheep.current_position;
			_mod.DebugEye = current_position2 + new Vector2(-1.3f, -1.1f);
			_mod.SetYaw(Mathf.Atan2(1.1f, 1.3f));
			_mod.SetPitchDegrees(-8f);
			int before = StuckArrows.LodgedCount;
			for (int l = 0; l < 4; l++)
			{
				if (!sheep.isAlive())
				{
					break;
				}
				try
				{
					current_position2 = sheep.current_position;
					Vector3 pLaunchPosition = new Vector3(current_position2.x - 1.8f + (float)l * 0.9f, current_position2.y - 2.2f, 0f);
					World.world.projectiles.spawn(king, sheep, "arrow", pLaunchPosition, new Vector3(current_position2.x, current_position2.y, 0f), 0.3f, 0.6f);
				}
				catch (Exception ex5)
				{
					Log.Error("ARCH arrow: " + ex5.Message);
				}
				yield return new WaitForSecondsRealtime(0.25f);
			}
			yield return new WaitForSecondsRealtime(0.5f);
			Log.Info($"ARCH sheep shot: lodged {before} -> {StuckArrows.LodgedCount}, sheep alive {sheep.isAlive()}, arrow strikes heard {_mod.Audio.ArrowImpactsPlayed}");
			_mod.SaveFrame("arch_sheep.png");
			yield return new WaitForSecondsRealtime(1f);
			_mod.SaveFrame("arch_sheep_after.png");
			try
			{
				if (sheep.isAlive())
				{
					sheep.dieSimpleNone();
				}
			}
			catch
			{
			}
		}

		private IEnumerator Wall(Actor king)
		{
			Vector2 current_position = king.current_position;
			WorldTile tile = World.world.GetTile((int)current_position.x + 6, (int)current_position.y + 6);
			Building house = null;
			try
			{
				house = World.world.buildings.addBuilding("house_human_1", tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				house?.completeConstruction();
			}
			catch (Exception ex)
			{
				Log.Error("ARCH house: " + ex.Message);
			}
			if (house == null)
			{
				Log.Error("ARCH no house");
				yield break;
			}
			Vector2 hp = new Vector2(tile.posV3.x, tile.posV3.y);
			_mod.DebugEye = hp + new Vector2(2.6f, -3.4f);
			_mod.DebugEyeSet = true;
			_mod.SetYaw(Mathf.Atan2(3.4f, -2.6f));
			_mod.SetPitchDegrees(0f);
			yield return new WaitForSecondsRealtime(1f);
			int fixed0 = StuckArrows.FixedCount;
			for (int i = 0; i < 5; i++)
			{
				try
				{
					Vector3 pLaunchPosition = new Vector3(hp.x - 2.5f + (float)i * 0.5f, hp.y - 5f, 0f);
					Vector3 pTargetPosition = new Vector3(hp.x + (float)i * 0.3f, hp.y + 4f, 0f);
					Projectile projectile = World.world.projectiles.spawn(king, null, "arrow", pLaunchPosition, pTargetPosition, 0f, 0.5f + (float)(i % 3) * 0.25f);
					if (projectile != null)
					{
						projectile._velocity = new Vector3((pTargetPosition.x - pLaunchPosition.x) * 1.6f, (pTargetPosition.y - pLaunchPosition.y) * 1.6f, 1.2f);
					}
				}
				catch (Exception ex2)
				{
					Log.Error("ARCH arrow: " + ex2.Message);
				}
				yield return new WaitForSecondsRealtime(0.12f);
			}
			yield return new WaitForSecondsRealtime(1.2f);
			_mod.SaveFrame("arch_wall.png");
			List<Vec3> list = StuckArrows.NewestPoints(StuckArrows.FixedCount - fixed0);
			List<string> list2 = new List<string>();
			foreach (Vec3 item in list)
			{
				list2.Add($"({item.X - hp.x:F2},{item.Y - hp.y:F2},{item.Z:F2})");
			}
			Log.Info(string.Format("ARCH house at {0}, arrows stuck about {1} -> {2}: {3}; arrow strikes heard {4}", hp, fixed0, StuckArrows.FixedCount, string.Join(" ", list2), _mod.Audio.ArrowImpactsPlayed));
			_mod.DebugEye = hp + new Vector2(-2.2f, -1.6f);
			_mod.SetYaw(Mathf.Atan2(1.6f, 2.2f));
			yield return new WaitForSecondsRealtime(0.4f);
			_mod.SaveFrame("arch_wall_close.png");
			int inWall = StuckArrows.FixedCount;
			try
			{
				house.startDestroyBuilding();
			}
			catch (Exception ex3)
			{
				Log.Error("ARCH destroy: " + ex3.Message);
			}
			yield return new WaitForSecondsRealtime(1.5f);
			int fixedCount = StuckArrows.FixedCount;
			Log.Info(string.Format("ARCH {0} house gone, arrows in it fall out: {1} -> {2} (before the house {3})", (inWall > fixed0 && fixedCount <= fixed0) ? "PASS" : "FAIL", inWall, fixedCount, fixed0));
			_mod.SaveFrame("arch_wall_gone.png");
		}

		private static bool Equip(Actor unit, string id)
		{
			try
			{
				ActorEquipmentSlot slot = unit.equipment.getSlot(EquipmentType.Weapon);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				if (equipmentAsset == null)
				{
					return false;
				}
				Item pItem = World.world.items.generateItem(equipmentAsset, unit.kingdom, unit.getName(), 1, unit, 0, pByPlayer: true);
				slot.setItem(pItem, unit);
				unit.setStatsDirty();
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("ARCH equip " + id + ": " + ex.Message);
				return false;
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
			Log.Info("ARCH DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
