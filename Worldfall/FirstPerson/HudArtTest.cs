using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class HudArtTest : CivilTest
	{
		protected override string Prefix => "HUDA";

		private IEnumerator Capture(string name, float wait = 0.6f)
		{
			if (wait > 0f)
			{
				yield return CivilTest.Wait(wait);
			}
			ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, name));
			yield return null;
			yield return null;
			Note("captured " + name);
		}

		protected override IEnumerator Body()
		{
			Actor king = Village("human", 10, 2);
			if (!Check("a village", king != null))
			{
				yield break;
			}
			List<Actor> list = new List<Actor>(Founded);
			Actor host = list[2];
			Actor npc = list[3];
			foreach (Actor item in list)
			{
				try
				{
					((BaseSimObject)item).addStatusEffect("invincible", 400f, pColorEffect: false);
				}
				catch
				{
				}
			}
			yield return Possess(host);
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			yield return CivilTest.Wait(1.5f);
			try
			{
				Camera camera = World.world.camera;
				camera.transform.position = new Vector3(host.current_position.x, host.current_position.y, camera.transform.position.z);
				camera.orthographicSize = 28f;
			}
			catch
			{
			}
			yield return Capture("huda_map.png", 1.2f);
			float speed = Mod.Settings.DiveSpeed;
			Mod.Settings.DiveSpeed = 0.35f;
			Mod.NoDive = false;
			try
			{
				ControllableUnit.setControllableCreature(host);
			}
			catch (Exception ex)
			{
				Note("possess: " + ex.Message);
			}
			for (int i = 0; i < 24; i++)
			{
				yield return Capture($"huda_dive_{i:00}.png", 0.12f);
			}
			Mod.Settings.DiveSpeed = speed;
			for (float t = 0f; t < 6f; t += 0.25f)
			{
				if (Mod.IsFirstPerson && Mod.Host == host && !Mod.Diving)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
			}
			Mod.NoDive = true;
			Mod.HintUntil = 0f;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			if (!Check("first person", InControl(host)))
			{
				yield break;
			}
			Check("the game's art", GameArt.Ready && GameArt.Heart != null && GameArt.BottomBar != null && GameArt.Font != null, string.Format("P {0:F2}, heart {1}, bar {2}, font {3}", GameArt.P, GameArt.Heart != null, GameArt.BottomBar != null, (GameArt.Font != null) ? GameArt.Font.name : "none"));
			Note($"game UI scale {GameArt.P:F2} at {Screen.width}x{Screen.height}");
			yield return Capture("huda_rest.png", 1.2f);
			int max = 100;
			try
			{
				max = host.getMaxHealth();
				host.setHealth(Mathf.RoundToInt((float)max * 0.62f));
			}
			catch
			{
			}
			yield return Capture("huda_hit.png", 0.06f);
			yield return Capture("huda_hurt.png", 0.9f);
			try
			{
				host.setHealth(Mathf.RoundToInt((float)max * 0.18f));
			}
			catch
			{
			}
			yield return Capture("huda_low.png", 0.5f);
			try
			{
				host.setHealth(Mathf.RoundToInt((float)max * 0.55f));
			}
			catch
			{
			}
			yield return Capture("huda_heal.png", 0.08f);
			try
			{
				host.setNutrition(Mathf.RoundToInt((float)host.getMaxNutrition() * 0.15f));
			}
			catch
			{
			}
			try
			{
				host.changeStamina(-Mathf.RoundToInt((float)host.getMaxStamina() * 0.9f));
			}
			catch
			{
			}
			try
			{
				((BaseSimObject)host).addStatusEffect("poisoned", 20f, pColorEffect: false);
				((BaseSimObject)host).addStatusEffect("inspired", 30f, pColorEffect: false);
			}
			catch
			{
			}
			yield return Capture("huda_spent.png", 0.5f);
			try
			{
				host.setNutrition(host.getMaxNutrition());
				host.setHealth(max);
			}
			catch
			{
			}
			CivilTest.StandBefore(npc, host, 1.4f);
			try
			{
				npc.setHealth(Mathf.RoundToInt((float)npc.getMaxHealth() * 0.6f));
			}
			catch
			{
			}
			yield return Capture("huda_prompt.png", 0.8f);
			Mod.Toast("A toast, as the game's tooltips show one");
			yield return Capture("huda_toast.png", 0.3f);
			float hour = Mod.Day.TimeOfDay;
			Mod.Day.TimeOfDay = 0.93f;
			yield return Capture("huda_night.png", 1f);
			Mod.Day.TimeOfDay = hour;
			Mod.HintUntil = Time.unscaledTime + 4f;
			yield return Capture("huda_help.png", 0.4f);
			Mod.HintUntil = 0f;
			yield return Possess(king);
			if (Check("the king", InControl(king)))
			{
				yield return Capture("huda_king.png", 1f);
			}
			Actor mage = CivilTest.SpawnNear("evil_mage", host.current_position, 4f);
			if (mage != null)
			{
				try
				{
					((BaseSimObject)mage).addStatusEffect("invincible", 200f, pColorEffect: false);
				}
				catch
				{
				}
				yield return Possess(mage);
				if (Check("the mage", InControl(mage)))
				{
					try
					{
						mage.setMana(Mathf.RoundToInt((float)mage.getMaxMana() * 0.6f));
					}
					catch
					{
					}
					yield return Capture("huda_mage.png", 1f);
				}
			}
			yield return Possess(host);
			yield return CivilTest.Wait(1f);
			speed = Mod.Settings.DiveSpeed;
			Mod.Settings.DiveSpeed = 0.35f;
			Mod.NoDive = false;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			for (int i = 0; i < 14; i++)
			{
				yield return Capture($"huda_out_{i:00}.png", 0.1f);
			}
			Mod.Settings.DiveSpeed = speed;
			yield return CivilTest.Wait(2f);
			Mod.NoDive = true;
			yield return Possess(npc);
			if (InControl(npc))
			{
				CivilTest.Kill(npc);
				yield return Capture("huda_dead.png", 1.6f);
			}
		}
	}
}
