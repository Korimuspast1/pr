using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class HungerTest : CivilTest
	{
		private sealed class Track
		{
			public string Name;

			public Actor A;

			public int Food;

			public int Health;

			public int Happy;

			public int Stamina;

			public int FoodLost;

			public int HealthLost;

			public int HappyLost;

			public int FoodTicks;

			public int HealthTicks;

			public readonly Dictionary<string, int> HurtBy = new Dictionary<string, int>();
		}

		private int _stuck;

		private int _frames;

		private int _exceptions;

		protected override string Prefix => "HUNGER";

		protected override IEnumerator Body()
		{
			Actor king = Village("human", 10, 0);
			if (king == null)
			{
				Check("a village was founded", ok: false);
				yield break;
			}
			yield return CivilTest.Wait(3f);
			Actor me = null;
			Actor him = null;
			foreach (Actor item in Founded)
			{
				if (item != null && item.isAlive() && item != king && item.isAdult())
				{
					if (me == null)
					{
						me = item;
					}
					else if (him == null)
					{
						him = item;
						break;
					}
				}
			}
			if (me == null || him == null)
			{
				Check("two villagers to compare", ok: false);
				yield break;
			}
			Actor[] array = new Actor[2] { me, him };
			foreach (Actor obj in array)
			{
				obj.setNutrition(obj.getMaxNutrition());
				obj.setHealth(obj.getMaxHealth());
				obj.setMaxHappiness();
			}
			Note($"you {Describe(me)}, him {Describe(him)}; metabolic rate {me.subspecies?.getMetabolicRate()}, max nutrition {me.getMaxNutrition()}, " + $"decay every {SimGlobals.m.interval_nutrition_decay:F0} s, starvation law {WorldLawLibrary.world_law_hunger.isEnabled()}, async {Mod.Settings.AsyncRender && WorldBoxMod.BesideAllowed}, speed {Config.time_scale_asset?.id} x{Config.time_scale_asset?.multiplier} ({Config.time_scale_asset?.ticks} ticks)");
			if (Options.Contains("hungergod"))
			{
				Mod.SetGodView(on: true);
				yield return CivilTest.Wait(3f);
				Note($"god view shown: {Mod.GodShown}");
			}
			yield return Possess(me);
			if (!Check("first person came up on the villager", InControl(me)))
			{
				yield break;
			}
			float seconds = 16f;
			string[] array2 = Options.Split(' ', '\n', '\r');
			foreach (string text in array2)
			{
				if (text.StartsWith("hungersecs=") && float.TryParse(text.Substring(11), out var result))
				{
					seconds = result;
				}
			}
			string only = null;
			array2 = Options.Split(' ', '\n', '\r');
			foreach (string text2 in array2)
			{
				if (text2.StartsWith("hungeronly="))
				{
					only = text2.Substring(11);
				}
			}
			Track you = new Track
			{
				Name = "you",
				A = me
			};
			Track other = new Track
			{
				Name = "him",
				A = him
			};
			Start(you);
			Start(other);
			int rate = Mathf.Max(1, me.subspecies?.getMetabolicRate() ?? 1);
			Application.logMessageReceived += OnLog;
			Vector2 home = DryRing(me.current_position);
			if (home != me.current_position)
			{
				Note($"home moved off the water's edge to {home.x:F0},{home.y:F0}");
			}
			List<string> unseen = new List<string>();
			foreach (ResourceAsset item2 in AssetManager.resources.list)
			{
				if (item2.gameplay_sprites == null || item2.gameplay_sprites.Length == 0)
				{
					unseen.Add(item2.id);
				}
			}
			Note(string.Format("{0} of {1} resources have no carried picture: {2}", unseen.Count, AssetManager.resources.list.Count, string.Join(", ", unseen.ToArray())));
			array2 = new string[1] { "rocklands_tree" };
			foreach (string text3 in array2)
			{
				BuildingAsset buildingAsset = AssetManager.buildings.get(text3);
				List<string> list = new List<string>();
				if (buildingAsset?.resources_given != null)
				{
					foreach (ResourceContainer item3 in buildingAsset.resources_given)
					{
						list.Add(item3.id + " x" + item3.amount);
					}
				}
				Note(text3 + " gives " + string.Join(", ", list.ToArray()));
			}
			StatusAsset possessed = AssetManager.status.get("possessed");
			WorldAction ownAction = possessed.action;
			string[] array3 = new string[13]
			{
				"broke_bare", "broke_guard", "broke_starve_bare", "broke_starve_guard", "carry", "chop_cliff", "stand", "walk", "run", "punch",
				"jump", "ability", "starve"
			};
			foreach (string phase in array3)
			{
				if (only != null && !only.Contains(phase))
				{
					continue;
				}
				if (!me.isAlive())
				{
					break;
				}
				bool broke = phase.StartsWith("broke_");
				if (!broke)
				{
					Hold(me, home, Mod.ViewYaw);
					try
					{
						me.finishStatusEffect("drowning");
						me.finishStatusEffect("burning");
					}
					catch
					{
					}
					yield return null;
				}
				Track[] array4;
				if (broke)
				{
					FrameGuard.Enabled = phase.EndsWith("_guard");
					possessed.action = delegate(BaseSimObject o, WorldTile t)
					{
						ownAction?.Invoke(o, t);
						throw new InvalidOperationException("HUNGER test: the game's frame cut short");
					};
					if (phase.Contains("starve"))
					{
						array4 = new Track[2] { you, other };
						foreach (Track track in array4)
						{
							track.A.setNutrition(0);
							track.A.setHealth(track.A.getMaxHealth());
							Start(track);
						}
					}
					World.world.timer_nutrition_decay = 0.3f;
				}
				string carried = null;
				if (phase == "carry")
				{
					carried = ((unseen.Count > 0) ? unseen[0] : "wood");
					me.addToInventory(carried, 3);
					Note($"carry: {carried} x3 in your bag (carrying {me.isCarryingResources()}, to render '{me.inventory.getItemIDToRender()}')");
				}
				Building tree = null;
				float yaw = 0f;
				if (phase.StartsWith("chop_"))
				{
					me.current_position = home;
					WorldTile spot = World.world.GetTile((int)home.x, (int)home.y + 2);
					tree = Put("rocklands_tree", spot);
					if (!Check(phase + ": a tree to fell", tree != null))
					{
						continue;
					}
					yaw = Mathf.Atan2(spot.posV3.y - home.y, spot.posV3.x - home.x);
					Mod.Holstered = true;
					float pitch = -2f;
					while (pitch > -45f && Mod.Picked != tree)
					{
						Mod.SetPitchDegrees(pitch);
						for (float h = 0f; h < 0.12f; h += Time.unscaledDeltaTime)
						{
							Hold(me, home, yaw);
							yield return null;
						}
						pitch -= 3f;
					}
					Note($"{phase}: {tree.asset.id} at {spot.x},{spot.y}, under the crosshair: {Mod.Picked == tree}, work target {Mod.Work.Target == tree}");
					Mod.Work.ForceSwing = true;
				}
				_stuck = 0;
				_frames = 0;
				if (phase == "starve")
				{
					array4 = new Track[2] { you, other };
					foreach (Track obj3 in array4)
					{
						obj3.A.setNutrition(0);
						Start(obj3);
					}
				}
				int food0 = you.FoodLost;
				int health0 = you.HealthLost;
				int hisFood0 = other.FoodLost;
				int hisHealth0 = other.HealthLost;
				int hurts0 = Mod.Feedback.Hurts;
				double world0 = World.world.getCurWorldTime();
				Note($"--- {phase} for {seconds:F0} s");
				float real = 0f;
				float nextSum = 1f;
				float act = 0f;
				bool abilityDown = false;
				bool eatPrompt = false;
				bool felled = false;
				while (real < seconds)
				{
					Vector2 scriptedInput;
					switch (phase)
					{
					default:
						scriptedInput = Vector2.zero;
						break;
					case "walk":
					case "run":
					case "jump":
						scriptedInput = new Vector2(0f, 1f);
						break;
					}
					PossessionHooks.ScriptedInput = scriptedInput;
					PossessionHooks.ScriptedRun = phase == "run";
					switch (phase)
					{
					case "walk":
					case "run":
					case "jump":
						Mod.SetYaw(Mod.ViewYaw + Time.unscaledDeltaTime * 0.6f);
						break;
					}
					if (tree != null)
					{
						Hold(me, home, yaw);
						if (Mod.Work.ForceSwing && (!tree.isAlive() || tree.chopped))
						{
							Mod.Work.ForceSwing = false;
							felled = true;
							Note($"t={real:F2}s {phase}: felled (alive {tree.isAlive()}, chopped {tree.chopped}), bag {Mod.Work.BagCount}");
						}
					}
					act -= Time.unscaledDeltaTime;
					if (act <= 0f)
					{
						switch (phase)
						{
						case "punch":
							PossessionCombat.AttackForTest(me);
							act = 0.6f;
							break;
						case "jump":
							PossessionHooks.ScriptedJump = true;
							act = 1.2f;
							break;
						case "ability":
						{
							WorldBoxMod mod = Mod;
							bool down;
							abilityDown = (down = !abilityDown);
							mod.PressAbilityForTest(down);
							act = (abilityDown ? 0.3f : 3f);
							break;
						}
						}
					}
					yield return null;
					real += Time.unscaledDeltaTime;
					_frames++;
					if (World.world.timer_nutrition_decay <= 0f && _stuck++ < 3)
					{
						Note($"t={real:F2}s the game's frame broke off before its end: nutrition timer {World.world.timer_nutrition_decay:F2}");
					}
					Step(you, real);
					Step(other, real);
					eatPrompt |= Mod.Prompt != null && Mod.Prompt.Contains(":Eat");
					if (real >= nextSum)
					{
						nextSum += 4f;
						Note($"t={real:F1}s world +{World.world.getCurWorldTime() - world0:F1}s step {Time.fixedDeltaTime:F4} elapsed {World.world.elapsed:F4} timer {World.world.timer_nutrition_decay:F2} | " + Line(you) + " | " + Line(other));
					}
					if (!me.isAlive())
					{
						Note($"t={real:F1}s you died ({me._last_attack_type})");
						break;
					}
					if (broke && phase.EndsWith("_bare") && ((you.FoodLost - food0 >= 60 && other.FoodLost - hisFood0 >= 60) || me.getHealth() < me.getMaxHealth() / 4))
					{
						Note($"t={real:F1}s {phase}: shown (you lost {you.FoodLost - food0} food, {you.HealthLost - health0} health): on to the next part");
						break;
					}
				}
				PossessionHooks.ScriptedInput = Vector2.zero;
				PossessionHooks.ScriptedRun = false;
				if (abilityDown)
				{
					Mod.PressAbilityForTest(down: false);
				}
				if (carried != null)
				{
					me.takeFromInventory(carried, 3);
				}
				if (broke)
				{
					possessed.action = ownAction;
					FrameGuard.Enabled = true;
					yield return null;
					yield return null;
				}
				if (tree != null)
				{
					Mod.Work.ForceSwing = false;
					Mod.Holstered = false;
					Mod.SetPitchDegrees(0f);
					Check(phase + ": the tree was felled", felled);
				}
				if (!broke)
				{
					Check($"{phase}: the game's frame ran to its end every time ({_stuck} of {_frames} broke off)", _stuck == 0);
				}
				double num = World.world.getCurWorldTime() - world0;
				int num2 = you.FoodLost - food0;
				int num3 = you.HealthLost - health0;
				int num4 = other.FoodLost - hisFood0;
				int num5 = other.HealthLost - hisHealth0;
				float num6 = (float)(num / (double)SimGlobals.m.interval_nutrition_decay + 1.0);
				float num7 = num6 * (float)rate;
				Note($"{phase}: over {real:F0} s ({num:F0} s of world time) you lost {num2} food and {num3} health, he lost {num4} food and {num5} health; " + $"{_stuck} of {_frames} frames cut short" + (broke ? (", guard " + (phase.EndsWith("_guard") ? "on" : "off")) : "") + $", {(float)_frames / Mathf.Max(0.01f, real):F0} fps");
				float num8 = Mathf.Ceil(num6) * (float)me.getMaxHealthPercent(SimGlobals.m.starvation_damage_multiplier);
				if (broke && phase.EndsWith("_bare"))
				{
					if (phase.Contains("starve"))
					{
						Check($"{phase}: cut short frame after frame, the starving lose health fast without the guard ({num3} health, the game's pace {num8:F0})", (float)num3 > num8 * 1.5f || !me.isAlive());
					}
					else
					{
						Check($"{phase}: cut short frame after frame, everyone goes hungry fast without the guard (you {num2}, he {num4} food, the game's pace {num7:F0})", (float)num2 > num7 * 5f && (float)num4 > num7 * 5f);
					}
				}
				else if (phase.Contains("starve"))
				{
					float num9 = num8;
					if (phase == "starve")
					{
						Check(phase + ": an empty stomach offers J to eat (prompt '" + Mod.Prompt + "')", eatPrompt);
					}
					if (num3 > 0)
					{
						Check($"{phase}: starving doesn't shake you as a blow does ({Mod.Feedback.Hurts - hurts0} hurts for {num3} health lost)", Mod.Feedback.Hurts == hurts0);
					}
					Check($"{phase}: you starve at the game's pace (lost {num3} health, at most {num9:F0} in {num:F0} s of world time)", (float)num3 <= num9 + 0.5f);
					Check($"{phase}: no faster than a villager left alone ({num3} vs his {num5} health)", num3 <= num5 + me.getMaxHealthPercent(SimGlobals.m.starvation_damage_multiplier));
				}
				else
				{
					Check($"{phase}: you go hungry at the game's pace (lost {num2}, at most {num7:F0} in {num:F0} s of world time)", (float)num2 <= num7 + 0.5f);
					Check($"{phase}: nothing hurts you ({num3} health lost)", num3 == 0);
				}
				array4 = new Track[2] { you, other };
				foreach (Track track2 in array4)
				{
					if (track2.A.isAlive())
					{
						track2.A.setNutrition(track2.A.getMaxNutrition());
						track2.A.setHealth(track2.A.getMaxHealth());
						Start(track2);
					}
				}
			}
			Application.logMessageReceived -= OnLog;
			Note($"in all you lost {you.FoodLost} food in {you.FoodTicks} steps, {you.HealthLost} health in {you.HealthTicks} hits ({Hurts(you)}), {you.HappyLost} happiness; " + $"he lost {other.FoodLost} food in {other.FoodTicks} steps, {other.HealthLost} health ({Hurts(other)}), {other.HappyLost} happiness");
			Check("you're still alive", me.isAlive());
		}

		private void OnLog(string message, string stack, LogType type)
		{
			if (type == LogType.Exception && _exceptions++ < 6)
			{
				Log.Info(Prefix + " EXCEPTION " + message + "\n" + stack);
			}
		}

		private static Vector2 DryRing(Vector2 from)
		{
			if (Dry(from, 26))
			{
				return from;
			}
			for (int i = 2; i <= 60; i += 2)
			{
				for (int j = 0; j < Mathf.Max(1, i * 2); j++)
				{
					float f = (float)j * MathF.PI * 2f / (float)Mathf.Max(1, i * 2);
					Vector2 c = from + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * i;
					if (Dry(c, 26))
					{
						return new Vector2(Mathf.Floor(c.x) + 0.5f, Mathf.Floor(c.y) + 0.5f);
					}
				}
			}
			return from;
		}

		private static bool Dry(Vector2 c, int radius)
		{
			float num = float.MaxValue;
			float num2 = float.MinValue;
			for (int i = -radius; i <= radius; i += 3)
			{
				for (int j = -radius; j <= radius; j += 3)
				{
					if (j * j + i * i > radius * radius)
					{
						continue;
					}
					WorldTile tile = World.world.GetTile((int)c.x + j, (int)c.y + i);
					if (tile == null || tile.Type == null || tile.Type.liquid)
					{
						return false;
					}
					try
					{
						if (tile.isOnFire() || (tile.building != null && tile.building.asset != null && tile.building.asset.id.Contains("bonfire")))
						{
							return false;
						}
					}
					catch
					{
					}
					float b = 0f;
					try
					{
						b = WorldBoxMod.Instance.Land.SampleHeight((float)tile.x + 0.5f, (float)tile.y + 0.5f);
					}
					catch
					{
					}
					num = Mathf.Min(num, b);
					num2 = Mathf.Max(num2, b);
				}
			}
			return num2 - num < 1.5f;
		}

		private static void Hold(Actor me, Vector2 home, float yaw)
		{
			if (me != null && me.isAlive())
			{
				me.current_position = home;
				me.dirty_current_tile = true;
				WorldBoxMod.Instance.SetYaw(yaw);
			}
		}

		private Building Put(string id, WorldTile t)
		{
			if (id == null || t == null || AssetManager.buildings.get(id) == null)
			{
				return null;
			}
			try
			{
				if (t.building != null)
				{
					t.building.removeBuildingFinal();
				}
				return World.world.buildings.addBuilding(id, t, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
			}
			catch (Exception ex)
			{
				Log.Error(Prefix + " put " + id + ": " + ex.Message);
				return null;
			}
		}

		private static void Start(Track t)
		{
			t.Food = t.A.getNutrition();
			t.Health = t.A.getHealth();
			t.Happy = t.A.getHappiness();
			t.Stamina = t.A.getStamina();
		}

		private void Step(Track t, float real)
		{
			Actor a = t.A;
			if (a == null || !a.isAlive())
			{
				return;
			}
			int nutrition = a.getNutrition();
			int health = a.getHealth();
			int happiness = a.getHappiness();
			if (nutrition != t.Food)
			{
				if (nutrition < t.Food)
				{
					t.FoodLost += t.Food - nutrition;
					t.FoodTicks++;
				}
				Note($"t={real:F2}s {t.Name} food {t.Food} -> {nutrition} (timer {World.world.timer_nutrition_decay:F2}, frame {Time.unscaledDeltaTime * 1000f:F0} ms, step {Time.fixedDeltaTime:F4})");
				t.Food = nutrition;
			}
			if (health != t.Health)
			{
				string text = a._last_attack_type.ToString() + ((a.attackedBy != null) ? (" by " + Who(a.attackedBy)) : "");
				if (health < t.Health)
				{
					t.HealthLost += t.Health - health;
					t.HealthTicks++;
					t.HurtBy[text] = (t.HurtBy.TryGetValue(text, out var value) ? value : 0) + (t.Health - health);
				}
				Note($"t={real:F2}s {t.Name} health {t.Health} -> {health} ({text}; statuses {Statuses(a)}; food {nutrition})");
				t.Health = health;
			}
			if (happiness != t.Happy)
			{
				if (happiness < t.Happy)
				{
					t.HappyLost += t.Happy - happiness;
				}
				Note($"t={real:F2}s {t.Name} happiness {t.Happy} -> {happiness} ({LastHappiness(a)})");
				t.Happy = happiness;
			}
		}

		private static string Line(Track t)
		{
			Actor a = t.A;
			if (a == null || !a.isAlive())
			{
				return t.Name + " dead";
			}
			return $"{t.Name}: food {a.getNutrition()}/{a.getMaxNutrition()} health {a.getHealth()}/{a.getMaxHealth()} happy {a.getHappiness()} stamina {a.getStamina()}/{a.getMaxStamina()} " + "task " + (((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.id ?? "-") + " statuses " + Statuses(a);
		}

		private static string Hurts(Track t)
		{
			if (t.HurtBy.Count == 0)
			{
				return "nothing";
			}
			StringBuilder stringBuilder = new StringBuilder();
			foreach (KeyValuePair<string, int> item in t.HurtBy)
			{
				stringBuilder.Append((stringBuilder.Length > 0) ? ", " : "").Append(item.Key).Append(' ')
					.Append(item.Value);
			}
			return stringBuilder.ToString();
		}

		private static string Statuses(Actor a)
		{
			StringBuilder stringBuilder = new StringBuilder();
			try
			{
				if (((BaseSimObject)a).hasAnyStatusEffect())
				{
					foreach (Status status in a.getStatuses())
					{
						stringBuilder.Append((stringBuilder.Length > 0) ? "," : "").Append(status.asset.id);
					}
				}
			}
			catch
			{
			}
			if (stringBuilder.Length <= 0)
			{
				return "-";
			}
			return stringBuilder.ToString();
		}

		private static string LastHappiness(Actor a)
		{
			try
			{
				string text = null;
				foreach (HappinessHistory item in a.happiness_change_history)
				{
					text = (item.asset?.id ?? item.index.ToString()) + " " + item.bonus;
				}
				return text ?? "-";
			}
			catch
			{
				return "?";
			}
		}

		private static string Who(BaseSimObject o)
		{
			try
			{
				return o.isActor() ? WorldBoxMod.Describe(o.a) : (o.isBuilding() ? "a building" : o.ToString());
			}
			catch
			{
				return "?";
			}
		}

		private static string Describe(Actor a)
		{
			return WorldBoxMod.Describe(a);
		}
	}
}
