using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class AbilityTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private static readonly string[] Peoples = new string[15]
		{
			"human", "elf", "orc", "dwarf", "zombie", "bandit", "necromancer", "evil_mage", "white_mage", "druid",
			"plague_doctor", "skeleton", "demon", "alien", "cold_one"
		};

		private static readonly string[] Testers = new string[20]
		{
			"bandit", "skeleton", "evil_mage", "necromancer", "white_mage", "druid", "plague_doctor", "demon", "alien", "cold_one",
			"human", "elf", "orc", "dwarf", "wolf", "rhino", "cat", "snake", "monkey", "zombie"
		};

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
			List<WorldTile> sites = KingTest.GoodSites();
			if (sites.Count == 0)
			{
				Log.Error("ABIL no open land");
				Finish(quit);
				yield break;
			}
			WorldTile home = sites[0];
			Actor actor = Spawn("human", home);
			if (actor == null)
			{
				Log.Error("ABIL could not spawn a host");
				Finish(quit);
				yield break;
			}
			ControllableUnit.setControllableCreature(actor);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("ABIL first person did not start");
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
			if (options.Contains("idle"))
			{
				yield return Run("idle", Idle());
				s.TimeMode = savedMode;
				Finish(quit);
				yield break;
			}
			if (!options.Contains("noppl"))
			{
				yield return Run("people", People(home));
			}
			int num = options.IndexOf("kinds:", StringComparison.Ordinal);
			string[] kinds = ((num >= 0) ? options.Substring(num + 6).Split(new char[3] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)[0].Split(',') : Testers);
			if (!options.Contains("nouse"))
			{
				yield return Run("use", Uses(sites, kinds));
			}
			s.TimeMode = savedMode;
			_mod.ForceRelight();
			Finish(quit);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("ABIL --- " + name);
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
					Log.Error("ABIL " + name + " failed: " + ex);
				}
				break;
				IL_007b:
				yield return current;
			}
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
		}

		private static Actor Spawn(string id, WorldTile t)
		{
			try
			{
				return World.world.units.spawnNewUnit(id, t, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("ABIL spawn " + id + ": " + ex.Message);
				return null;
			}
		}

		private static WorldTile Tile(float x, float y)
		{
			return World.world.GetTile((int)x, (int)y);
		}

		private static void Hold(List<Actor> list)
		{
			foreach (Actor item in list)
			{
				try
				{
					if (item != null && item.isAlive())
					{
						item.cancelAllBeh();
						item.makeWait(5f);
					}
				}
				catch
				{
				}
			}
		}

		private IEnumerator Idle()
		{
			Actor me = _mod.Host;
			bool[] array = new bool[2] { false, true };
			foreach (bool keep in array)
			{
				Possess.KeepEnabled = keep;
				if (me == null || !me.isAlive())
				{
					break;
				}
				((BaseSimObject)me).addStatusEffect("possessed", 10f, pColorEffect: false);
				Vector2 start = me.current_position;
				float most = 0f;
				for (int t = 1; t <= 24; t++)
				{
					yield return new WaitForSecondsRealtime(1f);
					float magnitude = (me.current_position - start).magnitude;
					most = Mathf.Max(most, magnitude);
					if (t % 4 == 0)
					{
						string text = null;
						try
						{
							text = ((me.ai != null && ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)me.ai).task != null) ? ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)me.ai).task.id : null);
						}
						catch
						{
						}
						Log.Info(string.Format("ABIL idle keep={0} {1}s: moved {2:F2} tiles, possessed {3}, task {4}", keep, t, magnitude, ((BaseSimObject)me).hasStatus("possessed"), text ?? "none"));
					}
				}
				Log.Info($"ABIL idle keep={keep}: moved at most {most:F2} tiles in 24 s standing still");
				_mod.SaveFrame("abil_idle_" + (keep ? "kept" : "lapsed") + ".png");
			}
			Possess.KeepEnabled = true;
		}

		private IEnumerator People(WorldTile home)
		{
			Settings settings = _mod.Settings;
			int savedRes = settings.Resolution;
			settings.Resolution = WorldBoxMod.ResolutionWidths.Length - 1;
			for (int g = 0; g * 3 < Peoples.Length; g++)
			{
				float cx = (float)home.x + 0.5f + ((g % 2 == 0) ? 1f : (-1f)) * (float)(g / 2 + 1) * 6f;
				float cy = (float)home.y + 0.5f;
				WorldTile worldTile = Tile(cx, cy + 2.3f);
				if (worldTile == null || worldTile.Type == null || worldTile.Type.liquid || worldTile.Type.block)
				{
					cx = (float)home.x + 0.5f;
				}
				List<Actor> row = new List<Actor>();
				for (int i = 0; i < 3 && g * 3 + i < Peoples.Length; i++)
				{
					Actor actor = Spawn(Peoples[g * 3 + i], Tile(cx + (float)(i - 1) * 1f, cy + 2.3f));
					if (actor != null)
					{
						row.Add(actor);
					}
				}
				_mod.DebugEyeSet = true;
				_mod.DebugEyeHeight = 0.8f;
				for (int shot = 0; shot < 2; shot++)
				{
					Vector2 vector = ((shot == 0) ? new Vector2(cx, cy) : new Vector2(cx + 1.8f, cy + 0.9f));
					Vector2 vector2 = new Vector2(cx, cy + 2.3f) - vector;
					_mod.DebugEye = vector;
					_mod.SetYaw(Mathf.Atan2(vector2.y, vector2.x));
					_mod.SetPitchDegrees(-8f);
					for (float t = 0f; t < 1.4f; t += 0.1f)
					{
						Hold(row);
						yield return new WaitForSecondsRealtime(0.1f);
					}
					_mod.SaveFrame(string.Format("abil_people_{0}{1}.png", g, (shot == 0) ? "" : "_side"));
				}
				foreach (Actor item in row)
				{
					SpriteImage spriteImage = _mod.SpriteForTest(item);
					Humanoid.Palette palette = ((spriteImage != null) ? Humanoid.Extract(spriteImage, item.asset.id) : default(Humanoid.Palette));
					Log.Info($"ABIL kind {item.asset.id}: model {Models.PersonFamily(item)}, skin {palette.Skin:X8} hair {palette.Hair:X8} shirt {palette.Shirt:X8} pants {palette.Pants:X8} accent {palette.Accent:X8}");
				}
				foreach (Actor item2 in row)
				{
					try
					{
						if (item2.isAlive())
						{
							item2.dieSimpleNone();
						}
					}
					catch
					{
					}
				}
				yield return new WaitForSecondsRealtime(0.3f);
			}
			settings.Resolution = savedRes;
			_mod.DebugEyeSet = false;
		}

		private IEnumerator Uses(List<WorldTile> sites, string[] kinds)
		{
			int site = 0;
			foreach (string text in kinds)
			{
				WorldTile at = sites[Mathf.Min(sites.Count - 1, 1 + site++ % Mathf.Max(1, sites.Count - 1))];
				yield return Run("use " + text, UseOne(text, at));
			}
		}

		private IEnumerator UseOne(string kind, WorldTile at)
		{
			Actor me = Spawn(kind, at);
			if (me == null)
			{
				yield break;
			}
			Ability ab = Abilities.For(me);
			if (ab == null)
			{
				Log.Error("ABIL " + kind + ": no ability");
				try
				{
					me.dieSimpleNone();
					yield break;
				}
				catch
				{
					yield break;
				}
			}
			try
			{
				((BaseSimObject)me).addStatusEffect("invincible", 40f, pColorEffect: false);
			}
			catch
			{
			}
			for (float t = 0f; t < 6f; t += 0.1f)
			{
				if (!(_mod.DeathTimer >= 0f))
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.1f);
			}
			bool leapt = (_mod.IsFirstPerson ? Possess.JumpTo(me) : Possess.Swap(me));
			for (float t = 0f; t < 5f; t += 0.1f)
			{
				if (_mod.Host == me)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(0.1f);
			}
			yield return new WaitForSecondsRealtime(1.2f);
			if (_mod.Host != me)
			{
				Log.Error($"ABIL {kind}: could not take over (leap {leapt})");
				yield break;
			}
			object obj3;
			switch (kind)
			{
			default:
				obj3 = "bandit";
				break;
			case "bandit":
			case "necromancer":
			case "demon":
				obj3 = "human";
				break;
			}
			string preyKind = (string)obj3;
			float num = ((ab.Range > 0f && ab.Range < 3f) ? 1.4f : ((ab.Motion == AbilityMotion.Leap) ? 3.2f : ((ab.Range <= 0f) ? 1.6f : 5f)));
			Vector2 p = me.current_position;
			Actor prey = Spawn(preyKind, Tile(p.x, p.y + num));
			List<Actor> hold = new List<Actor> { prey };
			yield return new WaitForSecondsRealtime(0.3f);
			if (prey == null)
			{
				Log.Error("ABIL " + kind + ": no prey");
				yield break;
			}
			if (ab.Name == "Rally" || ab.Name == "Howl")
			{
				for (int i = 0; i < 4; i++)
				{
					Actor actor = Spawn(kind, Tile(p.x - 1.5f + (float)i, p.y - 1.2f));
					if (actor != null)
					{
						hold.Add(actor);
					}
				}
			}
			_mod.Audio.TakeAbilitySounds();
			try
			{
				me.addAggro(prey);
				prey.addAggro(me);
			}
			catch
			{
			}
			float health = prey.getHealth();
			int before = ((World.world.projectiles != null) ? World.world.projectiles.list.Count : 0);
			int skeletons = Count("skeleton");
			float myStamina = me.getStamina();
			float myMana = me.getMana();
			Vector2 vector = prey.current_position - me.current_position;
			_mod.SetYaw(Mathf.Atan2(vector.y, vector.x));
			_mod.SetPitchDegrees(-6f);
			for (float t = 0f; t < 0.6f; t += 0.1f)
			{
				Hold(hold);
				yield return new WaitForSecondsRealtime(0.1f);
			}
			_mod.PressAbilityForTest(down: true);
			for (float t = 0f; t < 0.6f; t += 0.1f)
			{
				Hold(hold);
				yield return new WaitForSecondsRealtime(0.1f);
			}
			bool aiming = _mod.AbilityAiming;
			if (ab.Aimed)
			{
				_mod.SaveFrame("abil_" + kind + "_aim.png");
				ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "abil_" + kind + "_hud.png"));
				yield return new WaitForSecondsRealtime(0.2f);
			}
			_mod.PressAbilityForTest(down: false);
			yield return new WaitForSecondsRealtime(ab.Delay + 0.08f);
			string told = ((_mod.ToastUntil > Time.unscaledTime) ? _mod.ToastText : null);
			_mod.SaveFrame("abil_" + kind + "_use.png");
			int flying = ((World.world.projectiles != null) ? World.world.projectiles.list.Count : 0);
			int bombs = 0;
			foreach (MeshInstance mesh in _mod.Renderer.Meshes)
			{
				if (mesh.Model != null && mesh.Model.Name != null && (mesh.Model.Name == "bomb_round" || mesh.Model.Name.StartsWith("thrown_") || mesh.Model.Name.StartsWith("rock_plain") || mesh.Model.Name == "snowball"))
				{
					bombs++;
				}
			}
			for (float t = 0f; t < 1.6f; t += 0.1f)
			{
				yield return new WaitForSecondsRealtime(0.1f);
			}
			_mod.SaveFrame("abil_" + kind + "_after.png");
			string text = "";
			try
			{
				if (!prey.isAlive())
				{
					text = "dead";
				}
				else
				{
					string[] array = new string[7] { "frozen", "stunned", "poisoned", "slowness", "burning", "surprised", "shield" };
					foreach (string text2 in array)
					{
						if (((BaseSimObject)prey).hasStatus(text2))
						{
							text = text + text2 + " ";
						}
					}
				}
			}
			catch
			{
			}
			string text3 = "";
			try
			{
				string[] array = new string[5] { "shield", "rage", "inspired", "dodge", "motivated" };
				foreach (string text4 in array)
				{
					if (((BaseSimObject)me).hasStatus(text4))
					{
						text3 = text3 + text4 + " ";
					}
				}
			}
			catch
			{
			}
			float num2 = (prey.isAlive() ? ((float)prey.getHealth()) : 0f);
			Log.Info($"ABIL {kind} {ab.Name}: aimed {ab.Aimed} aiming {aiming}, cooldown {Abilities.CooldownLeft(me):F1}s, stamina {myStamina}->{me.getStamina()}, mana {myMana}->{me.getMana()}, " + string.Format("toast [{0}], projectiles {1}->{2}, thrown models {3}, prey {4} health {5}->{6} [{7}], skeletons {8}->{9}, mine [{10}], ", told, before, flying, bombs, preyKind, health, num2, text.Trim(), skeletons, Count("skeleton"), text3.Trim()) + "sounds [" + _mod.Audio.TakeAbilitySounds() + "]");
			try
			{
				if (prey.isAlive())
				{
					prey.dieSimpleNone();
				}
			}
			catch
			{
			}
		}

		private static int Count(string id)
		{
			int num = 0;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple.asset != null && simple.asset.id == id)
				{
					num++;
				}
			}
			return num;
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
			Log.Info("ABIL DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
