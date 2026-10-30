using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FirstPerson.Core;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class UfoTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private Actor _king;

		private string _options;

		private int _pass;

		private int _fail;

		private bool Wants(string part)
		{
			if (_options.Contains("only"))
			{
				return _options.Contains("only" + part);
			}
			return true;
		}

		private void Check(bool ok, string what)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			Log.Info((ok ? "UFO PASS " : "UFO FAIL ") + what);
		}

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			_options = File.ReadAllText(path);
			bool quit = _options.Contains("quit");
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
			Log.Info(string.Format("UFO world {0}x{1}, model {2}", MapBox.width, MapBox.height, (Models.Get("monster_ufo") != null) ? "found" : "MISSING"));
			if (Wants("dump"))
			{
				yield return Run("dump", Dump());
			}
			foreach (WorldTile item in KingTest.GoodSites())
			{
				_king = KingTest.FoundKingdom(item, "human", 3, 0);
				if (_king != null)
				{
					break;
				}
			}
			if (_king == null)
			{
				Log.Error("UFO could not found a kingdom");
				Finish(quit);
				yield break;
			}
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("UFO first person did not start");
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
			if (Wants("beam"))
			{
				yield return Run("beam", Beam());
			}
			if (Wants("crash"))
			{
				yield return Run("crash", Crash());
			}
			if (Wants("wind"))
			{
				yield return Run("wind", Wind());
			}
			if (Wants("ambient"))
			{
				yield return Run("ambient", Ambient());
			}
			s.TimeMode = savedMode;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Finish(quit);
		}

		private void Finish(bool quit)
		{
			Log.Info($"UFO {_pass} passed, {_fail} failed");
			Log.Info("UFO DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("UFO --- " + name);
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
					Log.Error("UFO " + name + " failed: " + ex);
				}
				break;
				IL_007b:
				yield return current;
			}
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			_mod.SetPitchDegrees(0f);
		}

		private IEnumerator Dump()
		{
			string dir = Path.Combine(Log.Folder, "ufo");
			Directory.CreateDirectory(dir);
			StringBuilder sb = new StringBuilder();
			List<KeyValuePair<string, Sprite>> pending = new List<KeyValuePair<string, Sprite>>();
			HashSet<Sprite> queued = new HashSet<Sprite>();
			try
			{
				ActorAsset actorAsset = AssetManager.actor_library.get("UFO");
				Line(string.Format("asset UFO: prefab {0} scale {1} height {2} speed {3} mass {4} shadow {5} sound_attack {6}", actorAsset.avatar_prefab, actorAsset.base_stats["scale"], actorAsset.default_height, actorAsset.base_stats["speed"], actorAsset.base_stats["mass"], actorAsset.shadow_texture, actorAsset.sound_attack));
				GameObject gameObject = Resources.Load<GameObject>("actors/" + actorAsset.avatar_prefab);
				if (gameObject == null)
				{
					Line("prefab not found");
				}
				else
				{
					Tree("ufo", gameObject.transform, "  ");
				}
			}
			catch (Exception ex)
			{
				Line("UFO asset failed: " + ex.Message);
			}
			string[] array = new string[2] { "fx_explosion_ufo", "fx_explosion_small" };
			foreach (string text in array)
			{
				try
				{
					EffectAsset effectAsset = AssetManager.effects_library.get(text);
					Line($"effect {text}: prefab {effectAsset.prefab_id} sprites {effectAsset.sprite_path} every {effectAsset.time_between_frames} light {effectAsset.draw_light_area} {effectAsset.draw_light_size}");
					if (!string.IsNullOrEmpty(effectAsset.sprite_path))
					{
						Frames(text, SpriteTextureLoader.getSpriteList(effectAsset.sprite_path));
					}
				}
				catch (Exception ex2)
				{
					Line("effect " + text + " failed: " + ex2.Message);
				}
			}
			array = new string[1] { "alien" };
			foreach (string text2 in array)
			{
				try
				{
					ActorAsset actorAsset2 = AssetManager.actor_library.get(text2);
					Line(string.Format("asset {0}: scale {1} texture {2} flying {3}", text2, actorAsset2.base_stats["scale"], actorAsset2.texture_asset?.texture_path_base, actorAsset2.flying));
				}
				catch (Exception ex3)
				{
					Line("asset " + text2 + " failed: " + ex3.Message);
				}
			}
			SpriteCache cache = new SpriteCache();
			for (int f = 0; f < 300; f++)
			{
				if (pending.Count <= 0)
				{
					break;
				}
				cache.BeginFrame();
				for (int num = pending.Count - 1; num >= 0; num--)
				{
					SpriteImage spriteImage = cache.Get(pending[num].Value);
					if (spriteImage != null)
					{
						Save(spriteImage, Path.Combine(dir, pending[num].Key), 6);
						pending.RemoveAt(num);
					}
				}
				yield return null;
			}
			Line($"sprites not written: {pending.Count}");
			File.WriteAllText(Path.Combine(dir, "ufo_dump.txt"), sb.ToString());
			void Frames(string tag, Sprite[] frames)
			{
				if (frames == null)
				{
					Line(tag + ": no frames");
				}
				else
				{
					for (int j = 0; j < frames.Length; j++)
					{
						Sprite sprite = frames[j];
						if (!(sprite == null))
						{
							Line($"{tag}[{j}] {sprite.name} {sprite.rect.width}x{sprite.rect.height} ppu {sprite.pixelsPerUnit} pivot {sprite.pivot}");
							if (queued.Add(sprite))
							{
								pending.Add(new KeyValuePair<string, Sprite>($"{tag}_{j}.png", sprite));
							}
						}
					}
				}
			}
			void Line(string s)
			{
				Log.Info("UFO DUMP " + s);
				sb.AppendLine(s);
			}
			void Tree(string tag, Transform t, string indent)
			{
				SpriteRenderer component = t.GetComponent<SpriteRenderer>();
				SpriteAnimation component2 = t.GetComponent<SpriteAnimation>();
				Line($"{indent}{t.name} pos {t.localPosition} scale {t.localScale} rot {t.localEulerAngles} active {t.gameObject.activeSelf}" + ((component != null) ? string.Format(" sprite {0} color {1} order {2}", (component.sprite != null) ? component.sprite.name : "-", component.color, component.sortingOrder) : " (no renderer)") + ((component2 != null) ? $" anim {((component2.frames != null) ? component2.frames.Length : 0)} frames every {component2.timeBetweenFrames} looped {component2.looped}" : ""));
				string text3 = tag + "_" + t.name.Replace(' ', '_');
				if (component2 != null)
				{
					Frames(text3, component2.frames);
				}
				else if (component != null && component.sprite != null)
				{
					Frames(text3, new Sprite[1] { component.sprite });
				}
				foreach (Transform item in t)
				{
					Tree(tag, item, indent + "  ");
				}
			}
		}

		private static void Save(SpriteImage img, string file, int k)
		{
			Texture2D texture2D = new Texture2D(img.Width * k, img.Height * k, TextureFormat.RGBA32, mipChain: false);
			Color32[] array = new Color32[img.Width * k * img.Height * k];
			for (int i = 0; i < img.Height * k; i++)
			{
				for (int j = 0; j < img.Width * k; j++)
				{
					uint num = img.Pixels[i / k * img.Width + j / k];
					array[i * img.Width * k + j] = new Color32((byte)(num & 0xFF), (byte)((num >> 8) & 0xFF), (byte)((num >> 16) & 0xFF), (byte)(num >> 24));
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false);
			File.WriteAllBytes(file, texture2D.EncodeToPNG());
			UnityEngine.Object.Destroy(texture2D);
		}

		private bool Site(float room, out Vector2 site)
		{
			site = Vector2.zero;
			TerrainCache terrainForTest = _mod.TerrainForTest;
			if (terrainForTest == null || terrainForTest.Width == 0)
			{
				return false;
			}
			float num = float.MaxValue;
			Vector2 current_position = _king.current_position;
			for (int i = (int)room + 4; (float)i < (float)terrainForTest.Height - room - 4f; i += 4)
			{
				for (int j = (int)room + 4; (float)j < (float)terrainForTest.Width - room - 4f; j += 4)
				{
					float num2 = float.MaxValue;
					float num3 = float.MinValue;
					int num4 = 0;
					int num5 = 0;
					for (int k = -(int)room; (float)k <= room; k += 3)
					{
						for (int l = -(int)room; (float)l <= room; l += 3)
						{
							int num6 = (i + k) * terrainForTest.Width + j + l;
							if ((terrainForTest.Flags[num6] & 3) != 0)
							{
								num4++;
								continue;
							}
							num2 = Mathf.Min(num2, terrainForTest.Ground[num6]);
							num3 = Mathf.Max(num3, terrainForTest.Ground[num6]);
							WorldTile tile = World.world.GetTile(j + l, i + k);
							if (tile != null && tile.building != null && AssetStyle.IsSolid(tile.building))
							{
								num5++;
							}
						}
					}
					if (num2 > num3)
					{
						continue;
					}
					float magnitude = (new Vector2(j, i) - current_position).magnitude;
					if (!(magnitude < room + 40f))
					{
						float num7 = (float)num4 * 4f + (float)num5 * 1.5f + (num3 - num2) + magnitude * 0.01f;
						if (num7 < num)
						{
							num = num7;
							site = new Vector2((float)j + 0.5f, (float)i + 0.5f);
						}
					}
				}
			}
			if (num < float.MaxValue)
			{
				Log.Info($"UFO site {site} (score {num:F1})");
			}
			return num < float.MaxValue;
		}

		private static void Clear(Vector2 at, int r)
		{
			for (int i = -r; i <= r; i++)
			{
				for (int j = -r; j <= r; j++)
				{
					if (j * j + i * i > r * r)
					{
						continue;
					}
					WorldTile tile = World.world.GetTile((int)at.x + j, (int)at.y + i);
					try
					{
						if (tile != null && tile.building != null)
						{
							tile.building.startDestroyBuilding();
						}
					}
					catch
					{
					}
				}
			}
		}

		private static Vector2 EyeFor(Actor ufo, float back, float side)
		{
			return ufo.current_position + new Vector2(0f - side, -1f).normalized * back;
		}

		private void Follow(Actor ufo, float back, float height, float pitch, float side = 0.6f)
		{
			Vector2 current_position = ufo.current_position;
			Vector2 debugEye = EyeFor(ufo, back, side);
			_mod.DebugEye = debugEye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = height;
			_mod.SetYaw(Mathf.Atan2(current_position.y - debugEye.y, current_position.x - debugEye.x));
			_mod.SetPitchDegrees(pitch);
		}

		private Actor SpawnUfo(Vector2 at)
		{
			WorldTile tile = World.world.GetTile((int)at.x, (int)at.y);
			Actor actor = null;
			try
			{
				actor = World.world.units.spawnNewUnit("UFO", tile, pSpawnSound: false, pMiracleSpawn: false, 0f);
			}
			catch (Exception ex)
			{
				Log.Error("UFO spawn: " + ex.Message);
			}
			if (actor != null)
			{
				Log.Info($"UFO spawned at {actor.current_position}, height {actor.position_height}, kingdom {actor.kingdom?.id}");
			}
			return actor;
		}

		private string Describe(Actor a)
		{
			if (a == null)
			{
				return "gone";
			}
			UFO uFO = null;
			try
			{
				uFO = a.getActorComponent<UFO>();
			}
			catch
			{
			}
			string text = ((uFO != null && uFO.beamAnim != null) ? string.Format("beam {0} f{1}", uFO.beamAnim.isOn ? "on" : "off", uFO.beamAnim.currentFrameIndex) : "beam ?");
			UfoFx ufoForTest = SceneCollector.UfoForTest;
			string text2 = ((ufoForTest != null) ? $" | fx beam t {ufoForTest.BeamTime:F2} strikes {ufoForTest.Strikes} dead {ufoForTest.Dead} at {ufoForTest.At.X:F1},{ufoForTest.At.Y:F1},{ufoForTest.At.Z:F1} d {ufoForTest.Diameter:F1}" : " | no fx");
			return string.Format("at {0:F1},{1:F1} h {2:F2} alive {3} task {4} speed {5:F0} {6} hp {7}{8}", a.current_position.x, a.current_position.y, a.position_height, a.isAlive(), ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.id, ((BaseSimObject)a).stats["speed"], text, a.data.health, text2);
		}

		private IEnumerator Beam()
		{
			if (!Site(12f, out var site))
			{
				Log.Error("UFO no site");
				yield break;
			}
			Clear(site, 9);
			Clear(site + new Vector2(-0.6f, -1f).normalized * 20f, 6);
			yield return new WaitForSecondsRealtime(1f);
			int framesBefore = SceneCollector.UfoFramesDrawn;
			Actor ufo = SpawnUfo(site);
			if (ufo == null)
			{
				yield break;
			}
			float start = Time.unscaledTime;
			bool arriving = false;
			float[] array = new float[4] { 0.05f, 0.25f, 0.5f, 0.9f };
			foreach (float k in array)
			{
				while (Time.unscaledTime - start < k)
				{
					Follow(ufo, 20f, 6f, 13f);
					yield return null;
				}
				arriving |= SceneCollector.UfoForTest != null && SceneCollector.UfoForTest.Arriving;
				_mod.SaveFrame($"ufo_warp_{k:0.00}.png");
			}
			Check(arriving, "a new UFO warps in");
			yield return new WaitForSecondsRealtime(1f);
			Follow(ufo, 20f, 6f, 13f);
			yield return null;
			_mod.SaveFrame("ufo_hover.png");
			Log.Info("UFO hover: " + Describe(ufo));
			Check(SceneCollector.UfoFramesDrawn - framesBefore > 20 && _mod.SceneForTest.UfoCount > 0, $"the saucer is drawn as its model ({SceneCollector.UfoFramesDrawn - framesBefore} frames)");
			int strikes0 = ((SceneCollector.UfoForTest != null) ? SceneCollector.UfoForTest.Strikes : 0);
			int strikesBefore = 0;
			HashSet<int> seen = new HashSet<int>();
			int shots = 0;
			for (int i = 0; i < 4; i++)
			{
				if (ufo == null || !ufo.isAlive())
				{
					Log.Info("UFO died (lightning?): calling another");
					strikesBefore += ((SceneCollector.UfoForTest != null) ? (SceneCollector.UfoForTest.Strikes - strikes0) : 0);
					ufo = SpawnUfo(site);
					if (ufo == null)
					{
						yield break;
					}
					yield return new WaitForSecondsRealtime(1.5f);
					strikes0 = ((SceneCollector.UfoForTest != null) ? SceneCollector.UfoForTest.Strikes : 0);
				}
				try
				{
					UFO.click(ufo);
				}
				catch (Exception ex)
				{
					Log.Error("UFO click: " + ex.Message);
				}
				start = Time.unscaledTime;
				int lastFrame = -2;
				int lastHp = ufo.data.health;
				while (Time.unscaledTime - start < 2.2f)
				{
					switch (i)
					{
					case 0:
						Follow(ufo, 22f, 4f, 12f);
						break;
					case 1:
						Follow(ufo, 11f, 3f, 24f);
						break;
					case 2:
						Follow(ufo, 6f, 1.6f, 34f);
						break;
					default:
						Follow(ufo, 30f, 9f, 6f, -0.8f);
						break;
					}
					UFO actorComponent = ufo.getActorComponent<UFO>();
					int num = (actorComponent.beamAnim.isOn ? actorComponent.beamAnim.currentFrameIndex : (-1));
					int health = ufo.data.health;
					if (health != lastHp)
					{
						string text = "-";
						try
						{
							object obj;
							if (ufo.attackedBy == null)
							{
								obj = "none";
							}
							else if (!ufo.attackedBy.isActor())
							{
								obj = "building";
							}
							else
							{
								string id = ufo.attackedBy.a.asset.id;
								Vector2 current_position = ufo.attackedBy.a.current_position;
								obj = id + " " + current_position.ToString();
							}
							text = (string)obj;
						}
						catch
						{
						}
						Log.Info($"UFO hp {lastHp} -> {health} at beam f{num}: last attack {ufo._last_attack_type}, by {text}");
						lastHp = health;
					}
					if (!ufo.isAlive())
					{
						break;
					}
					if (num != lastFrame)
					{
						if (num >= 0)
						{
							seen.Add(num);
						}
						Log.Info($"UFO beam {i} t={Time.unscaledTime - start:F2}: {Describe(ufo)}");
						if (num >= 0 && (i < 2 || (i == 2 && num == 5) || (i == 3 && num == 6)))
						{
							_mod.SaveFrame($"ufo_beam{i}_f{num:00}.png");
						}
						if (num < 0 && lastFrame >= 0)
						{
							break;
						}
						lastFrame = num;
					}
					yield return null;
				}
				shots++;
				yield return null;
			}
			int num2 = strikesBefore + ((SceneCollector.UfoForTest != null) ? (SceneCollector.UfoForTest.Strikes - strikes0) : 0);
			Check(seen.Count >= 10, $"the beam played the game's frames ({seen.Count} of 15 seen)");
			Check(num2 >= shots - 1, $"the beam struck the ground on the game's frame 4 ({num2} strikes in {shots} beams)");
			for (int i = 0; i < 8; i++)
			{
				if (!ufo.isAlive())
				{
					break;
				}
				Follow(ufo, 24f, 7f, 8f);
				yield return new WaitForSecondsRealtime(0.5f);
				if (i == 3 || i == 6)
				{
					_mod.SaveFrame($"ufo_flying_{i}.png");
				}
				Log.Info("UFO flying: " + Describe(ufo));
			}
			Follow(ufo, 16f, 6f, 14f);
			try
			{
				((BaseSimObject)ufo).getHit(5f, pFlash: true, AttackType.Other, (BaseSimObject)null, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: true);
			}
			catch (Exception ex2)
			{
				Log.Error("UFO hit: " + ex2.Message);
			}
			yield return null;
			yield return null;
			_mod.SaveFrame("ufo_hit.png");
			try
			{
				ufo.data.health = Mathf.Max(1, (int)((float)ufo.getMaxHealth() * 0.2f));
			}
			catch
			{
			}
			for (int i = 0; i < 20; i++)
			{
				Follow(ufo, 16f, 6f, 14f);
				yield return new WaitForSecondsRealtime(0.1f);
			}
			_mod.SaveFrame("ufo_hurt.png");
			Log.Info("UFO hurt: " + Describe(ufo));
			int crashes = SceneCollector.UfoCrashCount;
			try
			{
				ufo.dieAndDestroy(AttackType.Other);
			}
			catch
			{
			}
			yield return new WaitForSecondsRealtime(1f);
			Check(SceneCollector.UfoCrashCount == crashes, "a UFO taken away whole leaves no crash behind");
		}

		private IEnumerator Crash()
		{
			if (!Site(12f, out var site))
			{
				Log.Error("UFO no site");
				yield break;
			}
			Clear(site, 10);
			Actor ufo = SpawnUfo(site);
			if (ufo == null)
			{
				yield break;
			}
			Clear(EyeFor(ufo, 24f, 0.6f), 6);
			for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime)
			{
				Follow(ufo, 24f, 5f, 10f);
				yield return null;
			}
			if (!ufo.isAlive())
			{
				Log.Error("UFO died before it could be shot down");
				yield break;
			}
			Vector2 at = ufo.current_position;
			int aliensBefore = CountAliens(at, 14f);
			int falling0 = SceneCollector.UfoFallingFrames;
			int crashes0 = SceneCollector.UfoCrashCount;
			try
			{
				((BaseSimObject)ufo).getHit(100000f, pFlash: true, AttackType.Other, (BaseSimObject)null, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: true);
			}
			catch (Exception ex)
			{
				Log.Error("UFO kill: " + ex.Message);
			}
			float start = Time.unscaledTime;
			float landed = -1f;
			int shot = 0;
			while (Time.unscaledTime - start < 9f)
			{
				bool flag = ufo != null && ufo.exists && World.world.units.getSimpleList().Contains(ufo);
				float num = Time.unscaledTime - start;
				if (landed < 0f && SceneCollector.UfoCrashCount > crashes0)
				{
					landed = num;
				}
				if (num > ((shot < 12) ? ((float)shot * 0.15f) : (1.8f + (float)(shot - 12) * 0.6f)) && shot < 24)
				{
					Log.Info(string.Format("UFO crash t={0:F2}: listed {1}, {2} falling frames {3}, crashes {4}, wrecks {5}, aliens near {6}, particles {7}, frame {8:F1} ms", num, flag, flag ? Describe(ufo) : "", SceneCollector.UfoFallingFrames - falling0, SceneCollector.UfoCrashCount, SceneCollector.UfoWrecks, CountAliens(at, 14f) - aliensBefore, SceneCollector.BlastParticleCount, _mod.FrameMs));
					_mod.SaveFrame($"ufo_crash_{shot:00}.png");
					shot++;
				}
				yield return null;
			}
			Check(SceneCollector.UfoFallingFrames - falling0 > 10, $"the shot-down UFO was drawn tumbling down ({SceneCollector.UfoFallingFrames - falling0} frames)");
			Check(landed > 0.9f, $"its fall took long enough to watch ({landed:F2} s)");
			Check(landed > 0f, "it crashed in a blast of its own");
			Check(SceneCollector.UfoWrecks > 0, "its wreck lies burning where it came down");
			_mod.DebugEye = at + new Vector2(-4f, -9f);
			_mod.DebugEyeHeight = 2.5f;
			_mod.SetYaw(Mathf.Atan2(9f, 4f));
			_mod.SetPitchDegrees(-6f);
			yield return new WaitForSecondsRealtime(0.5f);
			_mod.SaveFrame("ufo_wreck.png");
		}

		private IEnumerator Wind()
		{
			if (!Site(10f, out var site))
			{
				Log.Error("UFO no site for the wind");
				yield break;
			}
			Clear(site, 5);
			SceneCollector scene = _mod.SceneForTest;
			Gusts g = scene.Gusts;
			Log.Info($"WIND before: {g.Count} gusts so far, {g.CalmLeft:F0} s of calm left, strength {g.Strength:F2}");
			Check(g.Count == 0 || g.CalmLeft > 0f || g.Strength < 0.95f, "the wind isn't blowing all the time");
			_mod.DebugEye = site;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = 0f;
			_mod.SetYaw(0.4f);
			_mod.SetPitchDegrees(-4f);
			g.Force = 0f;
			int falling = 0;
			float calmMs = 0f;
			for (float t = 0f; t < 6f; t += Time.unscaledDeltaTime)
			{
				falling = Mathf.Max(falling, scene.WindForTest.Falling);
				calmMs = Mathf.Max(calmMs, _mod.FrameMs);
				yield return null;
			}
			_mod.SaveFrame("wind_calm.png");
			Check(scene.WindForTest.Lines == 0 && GrassTufts.WindStrength < 0.01f, "in the calm there are no streaks of wind and the grass stands still");
			Log.Info($"WIND calm: {falling} leaves falling at most, frame {calmMs:F1} ms");
			float start = Time.unscaledTime;
			int lines = 0;
			int bits = 0;
			int shot = 0;
			float gustMs = 0f;
			while (Time.unscaledTime - start < 6f)
			{
				float num = Time.unscaledTime - start;
				g.Force = Mathf.Clamp01(num / 1.5f) * 0.9f;
				lines = Mathf.Max(lines, scene.WindForTest.Lines);
				bits = Mathf.Max(bits, scene.WindForTest.Bits);
				gustMs = Mathf.Max(gustMs, _mod.FrameMs);
				if (num > 1.2f + (float)shot * 1.2f && shot < 4)
				{
					_mod.SaveFrame($"wind_gust_{shot}.png");
					Log.Info($"WIND gust t={num:F1}: strength {g.Strength:F2}, {scene.WindForTest.Lines} streaks, {scene.WindForTest.Bits} bits, {scene.WindForTest.Drifts} drifts, grass {GrassTufts.WindStrength:F2}, frame {_mod.FrameMs:F1} ms");
					shot++;
				}
				yield return null;
			}
			Check(lines >= 4, $"a gust draws streaks of wind ({lines} at once at most)");
			Check(bits >= 10, $"a gust carries leaves and grass along ({bits} at once at most)");
			Check(GrassTufts.WindStrength > 0.5f, "the grass leans in the gust");
			_mod.SetYaw(3.5415928f);
			yield return new WaitForSecondsRealtime(0.4f);
			_mod.SaveFrame("wind_gust_back.png");
			g.CalmFor(60f);
			g.Force = -1f;
			yield return new WaitForSecondsRealtime(5f);
			Log.Info($"WIND after: strength {g.Strength:F2}, {scene.WindForTest.Lines} streaks, frame calm {calmMs:F1} ms, gust {gustMs:F1} ms");
			Check(g.Strength < 0.2f, $"the gust dies away when it is over ({g.Strength:F2})");
		}

		private IEnumerator Ambient()
		{
			if (!Site(10f, out var site))
			{
				Log.Error("UFO no site for the birds");
				yield break;
			}
			SceneCollector scene = _mod.SceneForTest;
			_mod.DebugEye = site;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = 0f;
			_mod.SetYaw(1.2f);
			_mod.SetPitchDegrees(18f);
			yield return new WaitForSecondsRealtime(0.5f);
			scene.FlockNowForTest(gulls: false);
			int birds = 0;
			for (float t = 0f; t < 9f; t += Time.unscaledDeltaTime)
			{
				birds = Mathf.Max(birds, scene.AmbientForTest.Birds);
				if (Mathf.Abs(t - 4f) < Time.unscaledDeltaTime)
				{
					_mod.SaveFrame("ambient_birds.png");
				}
				yield return null;
			}
			Check(birds >= 4, $"a flock of birds crosses the sky ({birds} birds)");
			Log.Info($"AMBIENT motes {scene.AmbientForTest.Motes}, next flock in {scene.AmbientForTest.NextFlock:F0} s");
			_mod.SetPitchDegrees(-2f);
			yield return new WaitForSecondsRealtime(0.5f);
			_mod.SaveFrame("ambient_fluff.png");
		}

		private static int CountAliens(Vector2 at, float r)
		{
			int num = 0;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple.asset != null && simple.asset.id == "alien" && (simple.current_position - at).sqrMagnitude < r * r)
				{
					num++;
				}
			}
			return num;
		}
	}
}
