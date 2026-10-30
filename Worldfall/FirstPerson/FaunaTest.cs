using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class FaunaTest : CivilTest
	{
		private static readonly (string id, bool adult, bool female, string family, string tag)[] Kinds = new(string, bool, bool, string, string)[7]
		{
			("wolf", true, false, "animal_wolf", "wolf"),
			("wolf", false, false, "animal_wolf_pup", "pup"),
			("cat", true, false, "animal_cat", "cat"),
			("cat", false, false, "animal_cat_kitten", "kitten"),
			("chicken", true, true, "animal_chicken", "hen"),
			("chicken", true, false, "animal_rooster", "rooster"),
			("chicken", false, true, "animal_chicken_chick", "chick")
		};

		protected override string Prefix => "FAUNA";

		protected override IEnumerator Body()
		{
			WorldTile worldTile = CivilTest.Stage();
			if (worldTile == null)
			{
				Check("a site for the test", ok: false);
				yield break;
			}
			CivilTest.ClearBox(worldTile.x - 16, worldTile.y - 4, worldTile.x + 16, worldTile.y + 12);
			MutationsTest.Pave(worldTile.x - 16, worldTile.y - 4, worldTile.x + 16, worldTile.y + 12);
			Actor host = CivilTest.Spawn("human", new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f));
			yield return CivilTest.Wait(0.5f);
			yield return Possess(host);
			if (Check("first person on a host", InControl(host)))
			{
				Settings s = Mod.Settings;
				bool savedGpu = s.GpuRender;
				int savedMode = s.TimeMode;
				int savedRes = s.Resolution;
				s.GpuRender = true;
				s.TimeMode = 1;
				s.Resolution = WorldBoxMod.ResolutionWidths.Length - 1;
				Mod.ForceRelight();
				(string, bool, bool, string, string)[] kinds = Kinds;
				for (int i = 0; i < kinds.Length; i++)
				{
					(string, bool, bool, string, string) tuple = kinds[i];
					Check(tuple.Item4 + " is in the library", Models.Get(tuple.Item4) != null && Models.Get(tuple.Item4 + "_m") != null && Models.Get(tuple.Item4 + "_f") != null);
				}
				Vector2 eye = host.current_position;
				yield return Part("one by one", OneByOne(eye + new Vector2(0f, 5f)));
				yield return Part("line-up", LineUp(eye + new Vector2(0f, 5f)));
				yield return Part("walking", Walking(eye + new Vector2(0f, 5f)));
				yield return Part("hunting", Hunting(eye + new Vector2(0f, 5f)));
				yield return Part("pecking", Pecking(eye + new Vector2(0f, 5f)));
				s.GpuRender = savedGpu;
				s.TimeMode = savedMode;
				s.Resolution = savedRes;
				Mod.DebugEyeSet = false;
			}
		}

		private Actor Make((string id, bool adult, bool female, string family, string tag) k, Vector2 at)
		{
			Actor actor = CivilTest.Spawn(k.id, at, k.adult);
			if (actor == null)
			{
				Note("no " + k.tag);
				return null;
			}
			CivilTest.SetSex(actor, k.female);
			try
			{
				if (actor.isEgg())
				{
					actor.finishStatusEffect("egg");
				}
			}
			catch (Exception ex)
			{
				Note("hatch: " + ex.Message);
			}
			try
			{
				actor.makeWait(60f);
			}
			catch
			{
			}
			return actor;
		}

		private IEnumerator Shot(Vector2 eye, float height, Vector2 lookAt, float pitch, string file)
		{
			Mod.DebugEyeSet = true;
			Mod.DebugEye = eye;
			Mod.DebugEyeHeight = height;
			Mod.SetYaw(Mathf.Atan2(lookAt.y - eye.y, lookAt.x - eye.x));
			Mod.SetPitchDegrees(pitch);
			yield return CivilTest.Wait(0.7f);
			if (file != null)
			{
				Mod.SaveFrame(file);
			}
		}

		private IEnumerator OneByOne(Vector2 mid)
		{
			float wolfSize = 0f;
			float catSize = 0f;
			float henSize = 0f;
			List<Actor> done = new List<Actor>();
			for (int i = 0; i < Kinds.Length; i++)
			{
				(string id, bool adult, bool female, string family, string tag) k = Kinds[i];
				Vector2 at = mid + new Vector2((float)(i - 3) * 3.5f, 0f);
				Actor a = Make(k, at);
				if (a != null)
				{
					done.Add(a);
					yield return CivilTest.Wait(0.6f);
					try
					{
						a.current_position = at;
					}
					catch
					{
					}
					yield return Shot(at + new Vector2(0f, -2f), 0.4f, at, -8f, null);
					string text = Models.CreatureFamily(a);
					Check(k.tag + " drawn as " + k.family, text == k.family && Creatures.DrawnModel(a) != null && Creatures.DrawnModel(a).StartsWith(k.family, StringComparison.Ordinal), "family " + text + ", drawn " + Creatures.DrawnModel(a));
					float size = Mathf.Max(0.1f, Creatures.DrawnSize(a));
					Note($"{k.tag}: {size:F2} tiles");
					if (k.tag == "wolf")
					{
						wolfSize = size;
					}
					if (k.tag == "cat")
					{
						catSize = size;
					}
					if (k.tag == "hen")
					{
						henSize = size;
					}
					if (k.tag == "pup")
					{
						Check("a pup is half a wolf or so", size > wolfSize * 0.35f && size < wolfSize * 0.7f, $"{size:F2} against {wolfSize:F2}");
					}
					if (k.tag == "kitten")
					{
						Check("a kitten is half a cat or so", size > catSize * 0.35f && size < catSize * 0.7f, $"{size:F2} against {catSize:F2}");
					}
					if (k.tag == "chick")
					{
						Check("a chick is a third of a hen or so", size > henSize * 0.2f && size < henSize * 0.6f, $"{size:F2} against {henSize:F2}");
					}
					float d = Mathf.Clamp(size * 2.2f + 0.45f, 0.8f, 6f);
					float h = Mathf.Max(0.1f, size * 0.5f + 0.06f);
					Config.paused = true;
					yield return Shot(at + new Vector2(0f, 0f - d), h, at, -8f, "fauna_" + k.tag + "_side.png");
					yield return Shot(at + new Vector2(-0.72f, -0.69f) * d, h, at, -10f, "fauna_" + k.tag + "_34.png");
					yield return Shot(at + new Vector2(0.25f, -0.97f) * d * 0.55f, h * 0.8f, at, -6f, "fauna_" + k.tag + "_face.png");
					if (k.tag == "wolf" || k.tag == "cat")
					{
						Mod.Settings.TimeMode = 3;
						Mod.ForceRelight();
						yield return Shot(at + new Vector2(0.25f, -0.97f) * d * 0.55f, h * 0.8f, at, -6f, null);
						yield return Shot(at + new Vector2(0.25f, -0.97f) * d * 0.55f, h * 0.8f, at, -6f, "fauna_" + k.tag + "_night.png");
						float eyeH = Mathf.Max(0.1f, size * 0.62f);
						yield return Shot(at + new Vector2(d * 0.7f, -0.05f), eyeH, at, -2f, "fauna_" + k.tag + "_night_e.png");
						yield return Shot(at + new Vector2((0f - d) * 0.7f, -0.05f), eyeH, at, -2f, "fauna_" + k.tag + "_night_w.png");
						Mod.Settings.TimeMode = 1;
						Mod.ForceRelight();
					}
					Config.paused = false;
				}
			}
			foreach (Actor item in done)
			{
				CivilTest.Kill(item);
			}
			yield return CivilTest.Wait(0.8f);
			Corpses.Clear();
			Mod.DebugEyeSet = false;
		}

		private IEnumerator LineUp(Vector2 at)
		{
			List<Actor> row = new List<Actor>();
			for (int i = 0; i < Kinds.Length; i++)
			{
				Actor actor = Make(Kinds[i], at + new Vector2((float)(i - 3) * 0.75f, 0f));
				if (actor != null)
				{
					row.Add(actor);
				}
			}
			yield return CivilTest.Wait(1.2f);
			for (int j = 0; j < row.Count; j++)
			{
				try
				{
					row[j].current_position = at + new Vector2((float)(j - 3) * 0.75f, 0f);
				}
				catch
				{
				}
			}
			Config.paused = true;
			yield return Shot(at + new Vector2(0f, -3.6f), 0.55f, at, -9f, "fauna_lineup.png");
			yield return Shot(at + new Vector2(2.2f, -2.4f), 0.45f, at, -9f, "fauna_lineup_34.png");
			Config.paused = false;
			foreach (Actor item in row)
			{
				CivilTest.Kill(item);
			}
			yield return CivilTest.Wait(0.8f);
			Corpses.Clear();
			Mod.DebugEyeSet = false;
		}

		private IEnumerator Walking(Vector2 at)
		{
			(string id, bool adult, bool female, string family, string tag)[] array = new(string, bool, bool, string, string)[3]
			{
				Kinds[0],
				Kinds[2],
				Kinds[4]
			};
			for (int i = 0; i < array.Length; i++)
			{
				(string id, bool adult, bool female, string family, string tag) k = array[i];
				Actor a = CivilTest.Spawn(k.id, at + new Vector2(-3f, 0f), k.adult);
				if (a != null)
				{
					CivilTest.SetSex(a, k.female);
					yield return CivilTest.Wait(0.5f);
					try
					{
						a.goTo(World.world.GetTile((int)at.x + 4, (int)at.y));
					}
					catch (Exception ex)
					{
						Note("goTo: " + ex.Message);
					}
					yield return CivilTest.Wait(0.8f);
					for (int n = 0; n < 6; n++)
					{
						Vector2 current_position = a.current_position;
						Mod.DebugEyeSet = true;
						float num = Mathf.Max(0.1f, Creatures.DrawnSize(a));
						Mod.DebugEye = current_position + new Vector2(0f, 0f - (num * 2.2f + 0.45f));
						Mod.DebugEyeHeight = num * 0.5f + 0.06f;
						Mod.SetYaw(MathF.PI / 2f);
						Mod.SetPitchDegrees(-8f);
						yield return CivilTest.Wait(0.12f);
						Mod.SaveFrame($"fauna_{k.tag}_walk_{n}.png");
					}
					CivilTest.Kill(a);
					yield return CivilTest.Wait(0.6f);
					Corpses.Clear();
				}
			}
			Mod.DebugEyeSet = false;
		}

		private IEnumerator Hunting(Vector2 at)
		{
			Actor wolf = CivilTest.Spawn("wolf", at);
			Actor prey = CivilTest.Spawn("sheep", at + new Vector2(1.4f, 0f));
			if (wolf == null || prey == null)
			{
				Note("no wolf or sheep to hunt");
				yield break;
			}
			try
			{
				prey.makeWait(60f);
			}
			catch
			{
			}
			yield return CivilTest.Wait(0.5f);
			try
			{
				wolf.startFightingWith(prey);
			}
			catch (Exception ex)
			{
				Note("fight: " + ex.Message);
			}
			bool prowled = false;
			bool bit = false;
			for (int n = 0; n < 30; n++)
			{
				if (!wolf.isAlive())
				{
					break;
				}
				Vector2 current_position = wolf.current_position;
				Mod.DebugEyeSet = true;
				Mod.DebugEye = current_position + new Vector2(-0.6f, -1.9f);
				Mod.DebugEyeHeight = 0.45f;
				Mod.SetYaw(Mathf.Atan2(1.9f, 0.6f));
				Mod.SetPitchDegrees(-8f);
				yield return CivilTest.Wait(0.15f);
				bool flag = false;
				try
				{
					flag = wolf.has_attack_target;
				}
				catch
				{
				}
				prowled = prowled || flag;
				float num = 9f;
				try
				{
					num = WorldPace.Since(wolf.last_attack_timestamp);
				}
				catch
				{
				}
				if (num < 0.45f)
				{
					bit = true;
				}
				Mod.SaveFrame($"fauna_wolf_hunt_{n:00}.png");
			}
			Check("the wolf went after its prey (prowling)", prowled);
			Note($"the wolf bit: {bit}");
			CivilTest.Kill(wolf);
			CivilTest.Kill(prey);
			yield return CivilTest.Wait(0.6f);
			Corpses.Clear();
			Mod.DebugEyeSet = false;
		}

		private IEnumerator Pecking(Vector2 at)
		{
			Actor hen = Make(Kinds[4], at);
			if (hen != null)
			{
				yield return CivilTest.Wait(0.8f);
				for (int n = 0; n < 16; n++)
				{
					yield return Shot(at + new Vector2(0f, -1f), 0.18f, at, -10f, $"fauna_hen_idle_{n:00}.png");
				}
				CivilTest.Kill(hen);
				yield return CivilTest.Wait(0.5f);
				Corpses.Clear();
				Mod.DebugEyeSet = false;
			}
		}
	}
}
