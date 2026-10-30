using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class HeldLoadsTest : CivilTest
	{
		private static readonly (string res, string model)[] Carried = new(string, string)[24]
		{
			("wood", "load_wood"),
			("wheat", "load_wheat"),
			("leather", "load_leather"),
			("fertilizer", "load_fertilizer"),
			("stone", "load_stone"),
			("gold", "load_gold"),
			("common_metals", "load_common_metals"),
			("crystal_salt", "load_crystal_salt"),
			("mythril", "load_mythril"),
			("adamantine", "load_adamantine"),
			("silver", "load_silver"),
			("gems", "load_gems"),
			("bones", "load_bones"),
			("dragon_scales", "load_dragon_scales"),
			("tea", "load_tea"),
			("pie", "load_bag_food"),
			("fish", "item_fish"),
			("bread", "item_bread"),
			("meat", "item_meat"),
			("bananas", "item_bananas"),
			("berries", "item_berries"),
			("mushrooms", "item_mushrooms"),
			("lemons", "item_lemons"),
			("honey", "item_honey")
		};

		private static readonly (string task, string tool)[] Tasks = new(string, string)[9]
		{
			("chop_trees", "axe"),
			("mine_deposit", "pickaxe"),
			("farmer_make_field", "hoe"),
			("cleaning", "hammer"),
			("manure_cleaning", "basket"),
			("put_out_fire", "bucket"),
			("claim_land", "flag"),
			("try_to_read", "book"),
			("progress_plot", "coffee_cup")
		};

		protected override string Prefix => "HELD";

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			if (worldTile == null)
			{
				Check("a site for the test", ok: false);
				yield break;
			}
			CivilTest.ClearBox(worldTile.x - 9, worldTile.y - 4, worldTile.x + 9, worldTile.y + 10);
			Actor host = CivilTest.Spawn("human", new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f));
			CivilTest.SetSex(host, female: false);
			yield return CivilTest.Wait(0.5f);
			yield return Possess(host);
			if (Check("first person on a host", InControl(host)))
			{
				Mod.Settings.TimeMode = 1;
				Mod.ForceRelight();
				Check("the carried models are in the library", Models.Get("load_wood") != null && Models.Get("item_fish") != null && Models.Get("tool_axe") != null, string.Format("load_wood {0}, item_fish {1}, tool_axe {2}", Models.Get("load_wood") != null, Models.Get("item_fish") != null, Models.Get("tool_axe") != null));
				if (!Options.Contains("noloads"))
				{
					yield return Part("loads", Loads());
				}
				if (!Options.Contains("notools"))
				{
					yield return Part("tools", Tools());
				}
				if (!Options.Contains("nohand"))
				{
					yield return Part("hand", Hand(host));
				}
				if (!Options.Contains("nofigure"))
				{
					yield return Part("figure", Figure(host));
				}
			}
		}

		private IEnumerator Figure(Actor host)
		{
			try
			{
				host.addToInventory("gold", 2);
				host.addToInventory("bread", 2);
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.3f);
			string[] array = new string[2] { "gold", "bread" };
			foreach (string res in array)
			{
				int num = BagOrder.Of(host).FindIndex((string e) => BagOrder.ResourceOf(e) == res);
				if (num < 0)
				{
					Check("figure: " + res + " in the bag", ok: false);
					continue;
				}
				if (num >= Hotbar.Size(host))
				{
					BagOrder.Swap(host, num, 0);
					num = 0;
				}
				Hotbar.Choose(host, num, wheel: false);
				Mod.Holstered = false;
				yield return CivilTest.Wait(0.4f);
				Mod.Craft.Open = true;
				yield return CivilTest.Wait(1.2f);
				Check("figure: holds the " + res, People.FigureItem == res, People.FigureItem ?? "nothing");
				SaveFigure("held_figure_" + res + ".png");
				Mod.Craft.Open = false;
				yield return CivilTest.Wait(0.3f);
			}
		}

		private void SaveFigure(string name)
		{
			Texture2D picture = Mod.YouView.Picture;
			if (picture == null)
			{
				Note("no figure picture for " + name);
				return;
			}
			Color32[] pixels = picture.GetPixels32();
			int width = picture.width;
			int height = picture.height;
			Texture2D texture2D = new Texture2D(width * 3, height * 3, TextureFormat.RGB24, mipChain: false);
			Color32[] array = new Color32[width * 3 * height * 3];
			Color32 color = new Color32(96, 98, 104, byte.MaxValue);
			for (int i = 0; i < height * 3; i++)
			{
				for (int j = 0; j < width * 3; j++)
				{
					Color32 color2 = pixels[i / 3 * width + j / 3];
					float num = (float)(int)color2.a / 255f;
					array[i * width * 3 + j] = new Color32((byte)((float)(int)color2.r * num + (float)(int)color.r * (1f - num)), (byte)((float)(int)color2.g * num + (float)(int)color.g * (1f - num)), (byte)((float)(int)color2.b * num + (float)(int)color.b * (1f - num)), byte.MaxValue);
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false);
			File.WriteAllBytes(Path.Combine(Log.Folder, name), texture2D.EncodeToPNG());
			UnityEngine.Object.Destroy(texture2D);
		}

		private static string Base(string name)
		{
			if (name != null)
			{
				if (!name.EndsWith("_m", StringComparison.Ordinal) && !name.EndsWith("_f", StringComparison.Ordinal))
				{
					return name;
				}
				return name.Substring(0, name.Length - 2);
			}
			return null;
		}

		private IEnumerator Loads()
		{
			Vector2 eye = Mod.Host.current_position + new Vector2(0f, 1f);
			ClearAll((int)eye.x - 9, (int)eye.y - 3, (int)eye.x + 9, (int)eye.y + 9);
			MutationsTest.Pave((int)eye.x - 9, (int)eye.y - 3, (int)eye.x + 9, (int)eye.y + 9);
			int drawn = 0;
			for (int g = 0; g * 4 < Carried.Length; g++)
			{
				List<Actor> row = new List<Actor>();
				List<string> want = new List<string>();
				for (int i = 0; i < 4 && g * 4 + i < Carried.Length; i++)
				{
					(string res, string model) tuple = Carried[g * 4 + i];
					string item = tuple.res;
					string item2 = tuple.model;
					Actor actor = CivilTest.Spawn("human", eye + new Vector2(((float)i - 1.5f) * 1.15f, 3.2f));
					if (actor == null)
					{
						Note("loads: no villager for " + item);
						continue;
					}
					CivilTest.SetSex(actor, i % 2 == 1);
					try
					{
						actor.makeWait(30f);
					}
					catch
					{
					}
					try
					{
						actor.addToInventory(item, 3);
					}
					catch (Exception ex)
					{
						Note("loads: " + item + " not given: " + ex.Message);
					}
					row.Add(actor);
					want.Add(item2);
				}
				yield return CivilTest.Wait(0.5f);
				Config.paused = true;
				Mod.DebugEyeSet = true;
				Mod.DebugEye = eye;
				Mod.DebugEyeHeight = 0.62f;
				Mod.SetYaw(MathF.PI / 2f);
				Mod.SetPitchDegrees(-5f);
				Corpses.Clear();
				yield return CivilTest.Wait(1.4f);
				Mod.SaveFrame($"held_loads_{g + 1}.png");
				for (int j = 0; j < row.Count; j++)
				{
					Actor actor2 = row[j];
					string text = null;
					bool flag = false;
					try
					{
						flag = actor2.isCarryingResources();
						text = actor2.inventory.getItemIDToRender();
					}
					catch
					{
					}
					string text2 = Base(People.CarriedModelForTest(actor2));
					if (text2 == want[j])
					{
						drawn++;
					}
					Check("loads: carrying " + (text ?? "nothing") + " drawn with " + (text2 ?? "nothing"), flag && text2 == want[j], "wanted " + want[j]);
				}
				Mod.DebugEye = eye + new Vector2(-2.3f, 1f);
				Mod.SetYaw(Mathf.Atan2(2.2f, 2.3f));
				yield return CivilTest.Wait(1f);
				Mod.SaveFrame($"held_loads_{g + 1}_34.png");
				Config.paused = false;
				foreach (Actor item3 in row)
				{
					Gone(item3);
				}
				yield return CivilTest.Wait(0.6f);
				Corpses.Clear();
				Mod.DebugEyeSet = false;
				yield return CivilTest.Wait(0.3f);
			}
			Check("loads: every carried resource drawn with its own model", drawn == Carried.Length, $"{drawn} of {Carried.Length}");
		}

		private IEnumerator Tools()
		{
			Vector2 eye = Mod.Host.current_position + new Vector2(0f, 1f);
			int held = 0;
			int tasked = 0;
			for (int g = 0; g * 5 < Tasks.Length; g++)
			{
				List<Actor> row = new List<Actor>();
				List<string> want = new List<string>();
				for (int i = 0; i < 5 && g * 5 + i < Tasks.Length; i++)
				{
					string item = Tasks[g * 5 + i].tool;
					Actor actor = CivilTest.Spawn("human", eye + new Vector2(((float)i - 2f) * 1f, 3f));
					if (actor != null)
					{
						CivilTest.SetSex(actor, i % 2 == 0);
						row.Add(actor);
						want.Add(item);
					}
				}
				yield return CivilTest.Wait(0.4f);
				for (int j = 0; j < row.Count; j++)
				{
					try
					{
						row[j].setTask(Tasks[g * 5 + j].task, pClean: true, pCleanJob: true);
					}
					catch (Exception ex)
					{
						Note("tools: " + Tasks[g * 5 + j].task + " not set: " + ex.Message);
					}
				}
				Config.paused = true;
				Mod.DebugEyeSet = true;
				Mod.DebugEye = eye;
				Mod.DebugEyeHeight = 0.62f;
				Mod.SetYaw(MathF.PI / 2f);
				Mod.SetPitchDegrees(-5f);
				Corpses.Clear();
				yield return CivilTest.Wait(1.4f);
				Mod.SaveFrame($"held_tools_{g + 1}.png");
				for (int k = 0; k < row.Count; k++)
				{
					Actor actor2 = row[k];
					string text = null;
					string text2 = null;
					try
					{
						text = ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)actor2.ai)?.task?.id;
						text2 = ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)actor2.ai)?.task?.force_hand_tool;
					}
					catch
					{
					}
					if (text2 == want[k])
					{
						tasked++;
					}
					string text3 = Base(People.HeldModelForTest(actor2));
					bool flag = text3 == "tool_" + want[k];
					if (flag)
					{
						held++;
					}
					if (text2 == want[k])
					{
						Check("tools: " + want[k] + " (" + text + ") held as " + (text3 ?? "nothing"), flag, "wanted tool_" + want[k]);
					}
					else
					{
						Note("tools: " + want[k] + ": the task " + Tasks[g * 5 + k].task + " didn't hold (task " + (text ?? "none") + ", tool " + (text2 ?? "none") + "), drawn " + (text3 ?? "nothing"));
					}
				}
				Mod.DebugEye = eye + new Vector2(-1.2f, 1.8f);
				Mod.SetYaw(1.1309735f);
				yield return CivilTest.Wait(1f);
				Mod.SaveFrame($"held_tools_{g + 1}_close.png");
				Config.paused = false;
				foreach (Actor item2 in row)
				{
					Gone(item2);
				}
				yield return CivilTest.Wait(0.6f);
				Corpses.Clear();
				Mod.DebugEyeSet = false;
				yield return CivilTest.Wait(0.3f);
			}
			Check("tools: the tools in hand are their own models", tasked > 0 && held == tasked, $"{held} of {tasked} with their task's tool");
		}

		private IEnumerator Hand(Actor host)
		{
			Mod.DebugEyeSet = false;
			try
			{
				host.addToInventory("bread", 3);
				host.addToInventory("ale", 2);
				host.addToInventory("fish", 2);
				host.addToInventory("gold", 2);
				host.setNutrition(Mathf.Max(1, host.getMaxNutrition() / 5));
			}
			catch (Exception ex)
			{
				Note("hand: not given: " + ex.Message);
			}
			yield return CivilTest.Wait(0.6f);
			Mod.SetYaw(MathF.PI / 2f);
			Mod.SetPitchDegrees(-6f);
			string[] array = new string[4] { "bread", "ale", "fish", "gold" };
			foreach (string res in array)
			{
				int num = BagOrder.Of(host).FindIndex((string e) => BagOrder.ResourceOf(e) == res);
				if (!Check("hand: " + res + " on the bag's rows", num >= 0))
				{
					continue;
				}
				if (num >= Hotbar.Size(host))
				{
					BagOrder.Swap(host, num, 0);
					num = 0;
				}
				Hotbar.Choose(host, num, wheel: false);
				Mod.Holstered = false;
				yield return CivilTest.Wait(0.8f);
				Carried item = Held.Item(host);
				Check("hand: " + res + " held up in your hand", item != null && item.Name == "item_" + res && People.HostCarried == item, item?.Name ?? "nothing");
				Mod.SaveFrame("held_hand_" + res + ".png");
				if (item != null && item.Edible)
				{
					int before = Count(host, res);
					try
					{
						host.setNutrition(Mathf.Max(1, host.getMaxNutrition() / 5));
					}
					catch
					{
					}
					WorldBoxMod.HotbarClickForTest = Time.frameCount + 1;
					yield return null;
					yield return null;
					yield return CivilTest.Wait(0.42f);
					Mod.SaveFrame("held_eat_" + res + ".png");
					Check("hand: a " + (item.Drink ? "drink" : "bite") + " of " + res + " at your mouth", Mod.SinceEaten < 1f && Count(host, res) == before - 1, $"since {Mod.SinceEaten:F2} s, {before} -> {Count(host, res)}");
					yield return CivilTest.Wait(1f);
				}
			}
		}

		private static void Gone(Actor a)
		{
			if (a == null)
			{
				return;
			}
			try
			{
				a.current_position = new Vector2(2f, 2f);
			}
			catch
			{
			}
			try
			{
				a.dieAndDestroy(AttackType.Other);
			}
			catch
			{
				CivilTest.Kill(a);
			}
		}

		private static void ClearAll(int x0, int y0, int x1, int y1)
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

		private static int Count(Actor a, string res)
		{
			try
			{
				Dictionary<string, ResourceContainer> dictionary = a.inventory?.getResources();
				ResourceContainer value;
				return (dictionary != null && dictionary.TryGetValue(res, out value)) ? value.amount : 0;
			}
			catch
			{
				return -1;
			}
		}
	}
}
