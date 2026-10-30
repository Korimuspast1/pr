using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FirstPerson.Core;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class DrillTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

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
			IEnumerator run = Run();
			while (true)
			{
				object current;
				try
				{
					if (!run.MoveNext())
					{
						break;
					}
					current = run.Current;
					goto IL_015c;
				}
				catch (Exception ex)
				{
					Log.Error("DRILL failed: " + ex);
				}
				break;
				IL_015c:
				yield return current;
			}
			_mod.DebugEyeSet = false;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info($"DRILL {_pass} passed, {_fail} failed");
			Log.Info("DRILL DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private void Check(string what, bool ok)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info("DRILL " + (ok ? "PASS" : "FAIL") + " " + what);
		}

		private IEnumerator Run()
		{
			Actor king = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item, "human", 12, 0);
				if (king != null)
				{
					break;
				}
			}
			if (king == null)
			{
				Log.Error("DRILL could not found a kingdom");
				yield break;
			}
			City city = king.city;
			WorldTile worldTile = OpenSpot(king.current_tile, 5, 12);
			if (worldTile == null)
			{
				Log.Error("DRILL no open spot for a dummy");
				yield break;
			}
			if (worldTile.building != null)
			{
				worldTile.building.startDestroyBuilding();
			}
			Building dummy = World.world.buildings.addBuilding("training_dummy", worldTile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
			if (dummy == null)
			{
				Log.Error("DRILL the dummy was not placed");
				yield break;
			}
			dummy.setKingdom(city.kingdom);
			city.listBuilding(dummy);
			if (dummy.isUnderConstruction())
			{
				dummy.completeConstruction();
			}
			Vector2 centre = new Vector2(worldTile.posV3.x, worldTile.posV3.y);
			float num = 0f;
			try
			{
				SpriteImage spriteImage = _mod.SpriteImageForTest(dummy.checkSpriteToRender());
				if (spriteImage != null)
				{
					num = spriteImage.UnitWidth * Mathf.Abs(dummy.asset.scale_base.x);
				}
			}
			catch
			{
			}
			Log.Info(string.Format("DRILL dummy at {0},{1} ({2:F2},{3:F2}), sprite {4:F2} tiles wide, model {5}", worldTile.x, worldTile.y, centre.x, centre.y, num, (Models.Get("training_dummy") != null) ? (Models.Get("training_dummy").Parts.Length + " parts") : "MISSING"));
			try
			{
				king.setCurrentTilePosition(World.world.GetTile(worldTile.x - 4, worldTile.y - 5) ?? worldTile);
			}
			catch
			{
			}
			try
			{
				((BaseSimObject)king).addStatusEffect("invincible", 600f, pColorEffect: false);
			}
			catch
			{
			}
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("DRILL first person did not start");
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
			List<Actor> trainees = new List<Actor>();
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple != king && simple.isAlive() && simple.city == city && simple.isWarrior() && trainees.Count < 5)
				{
					trainees.Add(simple);
				}
			}
			Log.Info($"DRILL {trainees.Count} soldiers of {city.name}");
			Check("the town has soldiers to train", trainees.Count >= 3);
			if (trainees.Count == 0)
			{
				yield break;
			}
			string[] array = new string[5] { "sword_iron", "axe_bronze", "spear_steel", "bow_wood", null };
			for (int i = 0; i < trainees.Count; i++)
			{
				Actor actor = trainees[i];
				try
				{
					((BaseSimObject)actor).addStatusEffect("invincible", 600f, pColorEffect: false);
				}
				catch
				{
				}
				if (array[i] != null)
				{
					Equip(actor, array[i]);
				}
				else
				{
					try
					{
						ActorEquipmentSlot slot = actor.equipment.getSlot(EquipmentType.Weapon);
						if (slot.getItem() != null)
						{
							slot.takeAwayItem();
						}
						actor.setStatsDirty();
					}
					catch
					{
					}
				}
				Train(actor);
			}
			MeshModel meshModel = Models.Get("training_dummy");
			Check("the dummy's posts are parts of their own (" + ((meshModel != null) ? string.Join(",", Array.ConvertAll(meshModel.Parts, (MeshPart p) => p.Name)) : "no model") + ")", meshModel != null && meshModel.PartIndex("dummy_0") >= 0 && meshModel.PartIndex("dummy_1") >= 0 && meshModel.PartIndex("dummy_2") >= 0);
			int savedResolution = _mod.Settings.Resolution;
			_mod.Settings.Resolution = WorldBoxMod.ResolutionWidths.Length - 1;
			_mod.DebugEyeSet = true;
			_mod.DebugEye = centre + new Vector2(1.3f, -2.3f);
			_mod.SetYaw(Mathf.Atan2(centre.y - _mod.DebugEye.y, centre.x - _mod.DebugEye.x));
			_mod.SetPitchDegrees(-5f);
			StringBuilder rec = new StringBuilder();
			rec.AppendLine("# dummy " + centre.x.ToString("F3", CultureInfo.InvariantCulture) + " " + centre.y.ToString("F3", CultureInfo.InvariantCulture));
			rec.AppendLine("# real world dt id task idx timer tilt x y moving shake");
			float real = 0f;
			float maxLean = 0f;
			int shots = 0;
			int punches = 0;
			int standing = 0;
			int nearSpot = 0;
			Dictionary<Actor, float> lastTilt = new Dictionary<Actor, float>();
			List<float> offs = new List<float>();
			HashSet<int> posts = new HashSet<int>();
			HashSet<Actor> posted = new HashSet<Actor>();
			int landed = 0;
			int late = 0;
			int arrows = 0;
			int landedWas = 0;
			int lateWas = 0;
			int landed2;
			float lean;
			float lean2;
			float lean3;
			int atPosts;
			int hits;
			while (real < 34f)
			{
				yield return null;
				real += Time.unscaledDeltaTime;
				double curWorldTime = World.world.getCurWorldTime();
				foreach (Actor item2 in trainees)
				{
					if (item2 == null || !item2.isAlive())
					{
						continue;
					}
					string text = ((item2.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)item2.ai).task != null) ? ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)item2.ai).task.id : "-");
					if (text != "warrior_train_with_dummy" && real < 28f)
					{
						Train(item2);
						text = "(re)" + text;
					}
					int num2 = ((item2.ai != null) ? ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)item2.ai).action_index : (-1));
					float z = item2.target_angle.z;
					lastTilt.TryGetValue(item2, out var value);
					if (Mathf.Abs(z) > Mathf.Abs(value) + 1f && real > 3f && real < 30f)
					{
						landed2 = punches++;
					}
					lastTilt[item2] = z;
					if (Drill.TraineeForTest(item2, out var slot2, out var post, out var spot, out lean, out lean2) && slot2 >= 0)
					{
						posted.Add(item2);
						posts.Add(post);
					}
					Vector2 current_position = item2.current_position;
					StringBuilder stringBuilder = rec.Append(real.ToString("F3", CultureInfo.InvariantCulture)).Append(' ').Append(curWorldTime.ToString("F3", CultureInfo.InvariantCulture))
						.Append(' ');
					lean2 = Time.deltaTime;
					stringBuilder.Append(lean2.ToString("F4", CultureInfo.InvariantCulture)).Append(' ').Append(item2.data.id)
						.Append(' ')
						.Append(text)
						.Append(' ')
						.Append(num2)
						.Append(' ')
						.Append(item2.timer_action.ToString("F3", CultureInfo.InvariantCulture))
						.Append(' ')
						.Append(z.ToString("F1", CultureInfo.InvariantCulture))
						.Append(' ')
						.Append(current_position.x.ToString("F3", CultureInfo.InvariantCulture))
						.Append(' ')
						.Append(current_position.y.ToString("F3", CultureInfo.InvariantCulture))
						.Append(' ')
						.Append(item2.is_moving ? 1 : 0)
						.Append(' ')
						.Append(dummy._shake_timer.ToString("F3", CultureInfo.InvariantCulture))
						.AppendLine();
					if (real > 6f && text == "warrior_train_with_dummy" && num2 >= 5 && !item2.is_moving && Drill.TraineeForTest(item2, out var slot3, out landed2, out var spot2, out lean2, out lean) && slot3 >= 0 && _mod.SceneForTest.MotionForTest(item2, out var drawn, out spot, out lean, out lean2, out landed2, out lean3))
					{
						landed2 = standing++;
						spot = drawn - spot2;
						float magnitude = spot.magnitude;
						offs.Add(magnitude);
						if (magnitude < 0.35f)
						{
							landed2 = nearSpot++;
						}
					}
				}
				if (Drill.YardForTest(dummy, out lean3, out var lean4, out var lean5, out var lean6))
				{
					maxLean = Mathf.Max(maxLean, Mathf.Max(lean4, Mathf.Max(lean5, lean6)));
				}
				Drill.CountForTest(out landed2, out atPosts, out var landed3, out var late2, out hits, out var arrows2);
				if (real > 3f && real < 30.5f)
				{
					landed += Mathf.Max(0, landed3 - landedWas);
					late += Mathf.Max(0, late2 - lateWas);
				}
				landedWas = landed3;
				lateWas = late2;
				arrows = Mathf.Max(arrows, arrows2);
				if (real > 9f && real < 13f && shots < 30 && Time.frameCount % 2 == 0)
				{
					_mod.SaveFrame($"drill_{shots:D2}.png");
					hits = shots++;
				}
				if (shots == 30 && real > 14f)
				{
					hits = shots++;
					_mod.DebugEye = centre + new Vector2(-2.5f, -0.6f);
					_mod.SetYaw(Mathf.Atan2(0.15000004f, 1.7f));
					_mod.SetPitchDegrees(-4f);
				}
				if (shots > 30 && shots < 55 && real > 15f && Time.frameCount % 2 == 0)
				{
					_mod.SaveFrame($"drill_side_{shots - 31:D2}.png");
					hits = shots++;
				}
				if (shots == 55 && real > 20f)
				{
					hits = shots++;
					Actor actor2 = ((trainees.Count > 3) ? trainees[3] : null);
					if (actor2 != null && Drill.TraineeForTest(actor2, out hits, out atPosts, out var spot3, out lean3, out lean2))
					{
						Vector2 normalized = (spot3 - centre).normalized;
						_mod.DebugEye = spot3 + normalized * 1.4f + new Vector2(normalized.y, 0f - normalized.x) * 0.5f;
						_mod.SetYaw(Mathf.Atan2(centre.y - _mod.DebugEye.y, centre.x - _mod.DebugEye.x));
						_mod.SetPitchDegrees(-3f);
					}
				}
				if (shots > 55 && shots < 70 && real > 21f && Time.frameCount % 3 == 0)
				{
					_mod.SaveFrame($"drill_archer_{shots - 56:D2}.png");
					atPosts = shots++;
				}
			}
			File.WriteAllText(Path.Combine(Log.Folder, "drill_rec.txt"), rec.ToString());
			_mod.Settings.Resolution = savedResolution;
			Drill.CountForTest(out atPosts, out hits, out landed2, out var _, out var hits2, out var _);
			foreach (Actor item3 in trainees)
			{
				if (Drill.TraineeForTest(item3, out var slot4, out var post2, out var spot4, out var lastError, out var body))
				{
					Log.Info(string.Format("DRILL {0} ({1}): slot {2}, post {3}, spot {4:F2},{5:F2} from the dummy, body {6:F2} tiles, last landing {7:F0} ms off", WorldBoxMod.Describe(item3), (item3.getWeapon() == null) ? "fists" : item3.getWeaponAsset()?.id, slot4, post2, spot4.x - centre.x, spot4.y - centre.y, body, lastError * 1000f));
				}
				else
				{
					Log.Info("DRILL " + WorldBoxMod.Describe(item3) + ": not at the dummy now");
				}
			}
			offs.Sort();
			float num3 = ((offs.Count > 0) ? offs[offs.Count / 2] : 99f);
			Drill.YardForTest(dummy, out var scale, out lean2, out lean3, out lean);
			Log.Info($"DRILL recorded {real:F0} s: {punches} punches, {landed} blows landed ({late} unannounced), dummy hit {hits2} times, up to {arrows} arrows in the straw, posts leaned up to {maxLean * 57.29578f:F1} deg; dummy scale {scale:F3} tiles per model unit; " + $"standing trainees drawn {num3:F2} tiles from their spot (median of {offs.Count}); render {_mod.RenderMs:F1} ms");
			Check(string.Format("each soldier at the dummy has a post to drill at ({0} of {1}; posts {2})", posted.Count, trainees.Count, string.Join(",", posts)), posted.Count >= Mathf.Min(3, trainees.Count) && posts.Count >= Mathf.Min(3, trainees.Count));
			Check($"standing soldiers are drawn at their striking spot, not in the posts ({nearSpot} of {standing} within 0.35 tiles)", standing > 0 && (float)nearSpot >= (float)standing * 0.85f);
			Check($"a blow lands for each of the game's punches ({landed} for {punches})", punches > 10 && (float)landed >= (float)punches * 0.85f);
			Check($"blows wound up ahead of the game's punch, not late ({late} unannounced)", late <= Mathf.Max(2, punches / 10));
			Check($"the posts rock when hit (up to {maxLean * 57.29578f:F1} deg, {hits2} hits)", maxLean > 0.02f && hits2 > 10);
			Check($"the archer's arrows stick in the straw ({arrows})", arrows > 0);
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
				Log.Error("DRILL equip " + id + ": " + ex.Message);
				return false;
			}
		}

		private static void Train(Actor a)
		{
			try
			{
				a.setTask("warrior_train_with_dummy");
			}
			catch (Exception ex)
			{
				Log.Error("DRILL setTask failed: " + ex.Message);
			}
		}

		private static WorldTile OpenSpot(WorldTile from, int min, int max)
		{
			for (int i = min; i <= max; i++)
			{
				for (int j = 0; j < 16; j++)
				{
					float f = (float)j * MathF.PI / 8f + (float)i * 0.3f;
					WorldTile tile = World.world.GetTile(from.x + (int)(Mathf.Cos(f) * (float)i), from.y + (int)(Mathf.Sin(f) * (float)i));
					if (tile == null)
					{
						continue;
					}
					bool flag = true;
					for (int k = -2; k <= 2 && flag; k++)
					{
						for (int l = -2; l <= 2 && flag; l++)
						{
							WorldTile tile2 = World.world.GetTile(tile.x + l, tile.y + k);
							flag = tile2 != null && tile2.Type != null && !tile2.Type.liquid && !tile2.Type.block && (tile2.building == null || !AssetStyle.IsSolid(tile2.building));
						}
					}
					if (flag)
					{
						return tile;
					}
				}
			}
			return null;
		}
	}
}
