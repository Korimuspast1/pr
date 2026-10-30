using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class ArmorTest : MonoBehaviour
	{
		private static readonly string[] Metals = new string[8] { "leather", "copper", "bronze", "silver", "iron", "steel", "mythril", "adamantine" };

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
			WorldTile home = null;
			using (List<WorldTile>.Enumerator enumerator = KingTest.GoodSites().GetEnumerator())
			{
				if (enumerator.MoveNext())
				{
					WorldTile current = enumerator.Current;
					home = current;
				}
			}
			if (home == null)
			{
				Log.Error("AR no site");
				Finish(quit);
				yield break;
			}
			Actor host = null;
			try
			{
				host = World.world.units.spawnNewUnit("human", home, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("AR host: " + ex.Message);
			}
			if (host == null)
			{
				Log.Error("AR no host");
				Finish(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(0.5f);
			ControllableUnit.setControllableCreature(host);
			yield return new WaitForSecondsRealtime(2f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("AR first person did not start");
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
			WorldTile worldTile = home;
			float num = float.MaxValue;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				float num2 = Roughness(item.x, item.y);
				if (num2 < num)
				{
					num = num2;
					worldTile = item;
				}
			}
			Log.Info($"AR stage {worldTile.x},{worldTile.y}, roughness {num:F0}, resolution {_mod.Renderer.Width}x{_mod.Renderer.Height}");
			Vector2 eye = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f - 3.5f);
			yield return Run("row", Row(eye, s));
			yield return Run("mixed", Mixed(eye, s));
			yield return Run("arms", Arms(host, s));
			s.TimeMode = savedMode;
			Config.paused = false;
			Log.Info($"AR {_pass} passed, {_fail} failed");
			Finish(quit);
		}

		private IEnumerator Row(Vector2 eye, Settings s)
		{
			s.TimeMode = 1;
			_mod.ForceRelight();
			ClearBox((int)eye.x - 7, (int)eye.y - 2, (int)eye.x + 7, (int)eye.y + 9);
			List<Actor> people = new List<Actor>();
			for (int i = 0; i < Metals.Length; i++)
			{
				Actor actor = Spawn("human", eye + new Vector2(((float)i - 3.5f) * 1.05f, 4.5f));
				if (actor != null)
				{
					try
					{
						actor.data.sex = ActorSex.Male;
					}
					catch
					{
					}
					Wear(actor, Metals[i], Metals[i], Metals[i]);
					people.Add(actor);
				}
			}
			yield return new WaitForSecondsRealtime(0.3f);
			Config.paused = true;
			Look(eye, -4f);
			yield return new WaitForSecondsRealtime(3f);
			_mod.SaveFrame("ar_row.png");
			for (int j = 0; j < people.Count; j++)
			{
				int num = People.ArmorShown(people[j]);
				Check($"{Metals[j]} set drawn as one model ({num})", num == 1);
			}
			for (int pair = 0; pair < people.Count / 2; pair++)
			{
				Vector2 mid;
				try
				{
					mid = (people[pair * 2].current_position + people[pair * 2 + 1].current_position) * 0.5f;
				}
				catch
				{
					continue;
				}
				Look(mid + new Vector2(0f, -1.7f), -3f);
				yield return new WaitForSecondsRealtime(1f);
				_mod.SaveFrame("ar_close_" + Metals[pair * 2] + "_" + Metals[pair * 2 + 1] + ".png");
				Look(mid + new Vector2(0f, -1.05f), 2f);
				yield return new WaitForSecondsRealtime(0.8f);
				_mod.SaveFrame("ar_heads_" + Metals[pair * 2] + "_" + Metals[pair * 2 + 1] + ".png");
			}
			Config.paused = false;
			foreach (Actor item in people)
			{
				try
				{
					if (item.isAlive())
					{
						item.dieSimpleNone();
					}
				}
				catch
				{
				}
			}
			yield return new WaitForSecondsRealtime(0.5f);
		}

		private IEnumerator Mixed(Vector2 eye, Settings s)
		{
			List<Actor> people = new List<Actor>();
			Actor mix = Spawn("human", eye + new Vector2(-1.6f, 4f));
			if (mix != null)
			{
				Wear(mix, "steel", "bronze", "leather");
				people.Add(mix);
			}
			string[] array = new string[3] { "elf", "orc", "dwarf" };
			string[] sets = new string[3] { "mythril", "adamantine", "steel" };
			for (int i = 0; i < array.Length; i++)
			{
				Actor actor = Spawn(array[i], eye + new Vector2(-0.5f + (float)i * 1.1f, 4f));
				if (actor != null)
				{
					Wear(actor, sets[i], sets[i], sets[i]);
					people.Add(actor);
				}
			}
			yield return new WaitForSecondsRealtime(0.3f);
			Config.paused = true;
			Look(eye + new Vector2(0f, 1.2f), -3f);
			yield return new WaitForSecondsRealtime(2.5f);
			_mod.SaveFrame("ar_mixed.png");
			if (mix != null)
			{
				int num = People.ArmorShown(mix);
				Check($"three materials drawn as three models ({num})", num == 3);
			}
			for (int j = 1; j < people.Count; j++)
			{
				int num2 = People.ArmorShown(people[j]);
				Check($"{people[j].asset.id} in {sets[j - 1]} drawn ({num2})", num2 == 1);
			}
			Config.paused = false;
			foreach (Actor item in people)
			{
				try
				{
					if (item.isAlive())
					{
						item.dieSimpleNone();
					}
				}
				catch
				{
				}
			}
			yield return new WaitForSecondsRealtime(0.5f);
		}

		private IEnumerator Arms(Actor host, Settings s)
		{
			_mod.DebugEyeSet = false;
			_mod.SetPitchDegrees(-6f);
			string[] metals = Metals;
			foreach (string m in metals)
			{
				Wear(host, null, m, null);
				yield return new WaitForSecondsRealtime(1.2f);
				string text = People.ChestArmor(host);
				Check("you wear armor_" + m + " (" + text + ")", text == "armor_" + m);
				_mod.SaveFrame("ar_arms_" + m + ".png");
			}
		}

		private static void Wear(Actor a, string helmet, string chest, string boots)
		{
			Put(a, (helmet == null) ? null : ("helmet_" + helmet));
			Put(a, (chest == null) ? null : ("armor_" + chest));
			Put(a, (boots == null) ? null : ("boots_" + boots));
		}

		private static void Put(Actor a, string id)
		{
			if (id == null)
			{
				return;
			}
			try
			{
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				if (equipmentAsset == null || a.equipment == null)
				{
					Log.Error("AR no item " + id);
					return;
				}
				ActorEquipmentSlot slot = a.equipment.getSlot(equipmentAsset.equipment_type);
				Item item = World.world.items.generateItem(equipmentAsset, a.kingdom, a.getName(), 1, a, 0, pByPlayer: true);
				if (item == null)
				{
					Log.Error("AR item not made " + id);
					return;
				}
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				slot.setItem(item, a);
				a.setStatsDirty();
			}
			catch (Exception ex)
			{
				Log.Error("AR wear " + id + ": " + ex.Message);
			}
		}

		private void Look(Vector2 at, float pitch)
		{
			_mod.DebugEye = at;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = 0f;
			_mod.SetYaw(MathF.PI / 2f);
			_mod.SetPitchDegrees(pitch);
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
			Log.Info("AR " + (ok ? "PASS" : "FAIL") + " " + what);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("AR --- " + name);
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
					Log.Error("AR " + name + " failed: " + ex);
				}
				break;
				IL_007b:
				yield return current;
			}
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			Config.paused = false;
		}

		private static float Roughness(int cx, int cy)
		{
			int num = int.MaxValue;
			int num2 = int.MinValue;
			int num3 = 0;
			for (int i = cy - 6; i <= cy + 7; i++)
			{
				for (int j = cx - 7; j <= cx + 7; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile == null || tile.main_type == null)
					{
						num3++;
						continue;
					}
					if (tile.main_type.liquid || tile.main_type.block)
					{
						num3++;
					}
					num = Mathf.Min(num, tile.main_type.height_min);
					num2 = Mathf.Max(num2, tile.main_type.height_min);
				}
			}
			return (float)num3 * 100f + (float)(num2 - num);
		}

		private static void ClearBox(int x0, int y0, int x1, int y1)
		{
			for (int i = y0; i <= y1; i++)
			{
				for (int j = x0; j <= x1; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile != null && tile.building != null)
					{
						try
						{
							tile.building.removeBuildingFinal();
						}
						catch
						{
						}
					}
				}
			}
		}

		private static Actor Spawn(string id, Vector2 at)
		{
			WorldTile tile = World.world.GetTile((int)at.x, (int)at.y);
			if (tile == null || tile.main_type == null || tile.main_type.liquid)
			{
				return null;
			}
			try
			{
				return World.world.units.spawnNewUnit(id, tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("AR spawn " + id + ": " + ex.Message);
				return null;
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
			Log.Info("AR DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
