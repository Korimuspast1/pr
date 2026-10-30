using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class MonolithTest : CivilTest
	{
		protected override string Prefix => "MONO";

		protected override IEnumerator Body()
		{
			WorldTile stage = CivilTest.Stage();
			if (!Check("a stage", stage != null))
			{
				yield break;
			}
			Actor you = CivilTest.SpawnNear("human", new Vector2((float)stage.x + 0.5f, (float)stage.y + 0.5f));
			if (!Check("someone to look through", you != null))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)you).addStatusEffect("invincible", 600f, pColorEffect: false);
			}
			catch
			{
			}
			yield return Possess(you);
			CivilTest.ClearBox(stage.x - 8, stage.y - 6, stage.x + 8, stage.y + 10);
			Building stone = null;
			try
			{
				stone = World.world.buildings.addBuilding("monolith", World.world.GetTile(stage.x, stage.y + 5), pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
			}
			catch (Exception ex)
			{
				Note("monolith: " + ex.Message);
			}
			if (!Check("a monolith", stone != null && stone.component_monolith != null))
			{
				yield break;
			}
			Vector2 vector = new Vector2((float)stage.x + 0.5f, (float)stage.y - 2.5f);
			Mod.DebugEyeSet = true;
			Mod.DebugEyeHeight = 0.8f;
			Mod.DebugEye = vector;
			Vector2 vector2 = stone.current_position - vector;
			Mod.SetYaw(Mathf.Atan2(vector2.y, vector2.x));
			Mod.SetPitchDegrees(6f);
			yield return CivilTest.Wait(2f);
			Mod.SaveFrame("mono_still.png");
			Mod.SceneForTest.DumpNear(stone.current_position, 2f);
			SceneCollector.MonolithFlashSeen = 0;
			SceneCollector.MonolithSpritesTaken = 0;
			SceneCollector.EffectTrace = new List<string>();
			stone.component_monolith.spawnMainEffect();
			int maxFlash = 0;
			for (int i = 0; i < 12; i++)
			{
				yield return CivilTest.Wait(0.1f);
				if (i == 2)
				{
					Mod.SaveFrame("mono_wake.png");
				}
				maxFlash = Mathf.Max(maxFlash, SceneCollector.MonolithFlashSeen);
			}
			Mod.SceneForTest.DumpNear(stone.current_position, 2f);
			Note($"stone at {stone.current_position}, tile {stone.current_tile.posV3}");
			List<string> list = SceneCollector.EffectTrace.Where((string e) => e.StartsWith("fx_monolith", StringComparison.Ordinal)).Distinct().ToList();
			SceneCollector.EffectTrace = null;
			Check("the game's flat flash is not drawn", list.Count == 0, string.Join(", ", list));
			Check("the flash was seen and taken", SceneCollector.MonolithSpritesTaken > 0, $"taken {SceneCollector.MonolithSpritesTaken}");
			Check("the 3D monolith flashes", maxFlash > 15, $"flash {maxFlash}");
			yield return CivilTest.Wait(1.5f);
			SceneCollector.MonolithFlashSeen = 0;
			yield return CivilTest.Wait(0.3f);
			Check("and settles back", SceneCollector.MonolithFlashSeen == 0, $"flash {SceneCollector.MonolithFlashSeen}");
			Mod.SaveFrame("mono_after.png");
			Check("the obelisk has its middle and far versions", Models.Get("nature_monolith_m") != null && Models.Get("nature_monolith_f") != null);
			Vector2 at = stone.current_position;
			Settings settings = Mod.Settings;
			int savedMode = settings.TimeMode;
			int[] array = new int[2] { 1, 3 };
			for (int i = 0; i < array.Length; i++)
			{
				int mode = (settings.TimeMode = array[i]);
				Mod.ForceRelight();
				string tag = ((mode == 3) ? "_night" : "");
				yield return Look(at + new Vector2(0f, -7.5f), at, 0.8f, 6f, "mono_front" + tag + ".png");
				yield return Look(at + new Vector2(5.2f, -5.6f), at, 1.4f, 8f, "mono_side" + tag + ".png");
				yield return Look(at + new Vector2(0.35f, -2.3f), at, 0.8f, 32f, "mono_close" + tag + ".png");
				yield return Look(at + new Vector2(-4.5f, 6.5f), at, 1.2f, 8f, "mono_back" + tag + ".png");
				if (mode == 1)
				{
					yield return Look(at + new Vector2(-14f, -33f), at, 1.2f, 2f, "mono_far.png");
				}
				if (mode == 3)
				{
					yield return Look(at + new Vector2(2.4f, -6.8f), at, 0.9f, 8f, "mono_dark.png");
					stone.component_monolith.spawnMainEffect();
					yield return CivilTest.Wait(0.25f);
					Mod.SaveFrame("mono_wake_night.png");
				}
			}
			settings.TimeMode = savedMode;
			Mod.ForceRelight();
			Mod.DebugEyeSet = false;
			yield return CivilTest.Wait(0.5f);
			Vector2 from = at + new Vector2(0f, -4.6f);
			PossessionHooks.ScriptedInput = Vector2.zero;
			you.current_position = from;
			you.dirty_current_tile = true;
			Mod.SetYaw(MathF.PI / 2f);
			Mod.SetPitchDegrees(8f);
			yield return CivilTest.Wait(0.4f);
			PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
			yield return CivilTest.Wait(4f);
			PossessionHooks.ScriptedInput = Vector2.zero;
			yield return CivilTest.Wait(0.4f);
			Vector2 current_position = you.current_position;
			Vector2 vector3 = new Vector2(stone.current_tile.posV3.x, stone.current_tile.posV3.y);
			string text = Mod.Prompt ?? "";
			Note($"walked in from {from - at} to {current_position - at}, {(current_position - vector3).magnitude:F2} from its tile, prompt '{text}'");
			Mod.SaveFrame("mono_walked_up.png");
			Check("the way in at its front is open", (current_position - from).magnitude > 1.5f, $"walked {(current_position - from).magnitude:F2}");
			Check("near enough to touch it", (current_position - vector3).magnitude < 3f, $"{(current_position - vector3).magnitude:F2} from its tile");
			Check("the obelisk is offered", text.Contains("obelisk"), text);
		}

		private IEnumerator Look(Vector2 eye, Vector2 at, float height, float pitch, string frame)
		{
			Mod.DebugEyeSet = true;
			Mod.DebugEyeHeight = height;
			Mod.DebugEye = eye;
			Vector2 vector = at - eye;
			Mod.SetYaw(Mathf.Atan2(vector.y, vector.x));
			Mod.SetPitchDegrees(pitch);
			yield return CivilTest.Wait(1.2f);
			Mod.SaveFrame(frame);
		}
	}
}
