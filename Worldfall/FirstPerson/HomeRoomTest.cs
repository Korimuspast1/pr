using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class HomeRoomTest : MonoBehaviour
	{
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
			WorldBoxMod mod = WorldBoxMod.Instance;
			Actor king = null;
			foreach (WorldTile item3 in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item3, "human", 8, 2);
				if (king != null)
				{
					break;
				}
			}
			if (king == null)
			{
				Log.Error("HOMEROOM could not found a village");
				Finish(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(2f);
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(3f);
			if (!mod.IsFirstPerson)
			{
				Log.Error("HOMEROOM first person did not start");
				Finish(quit);
				yield break;
			}
			mod.HintUntil = 0f;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			List<Actor> people = new List<Actor>();
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple != null && simple != king && simple.isAlive() && People.IsHumanoid(simple) && simple.kingdom == king.kingdom && people.Count < 3)
				{
					people.Add(simple);
				}
			}
			Log.Info($"HOMEROOM {people.Count} people to fill the rooms, {SystemInfo.graphicsDeviceName}, {mod.Renderer.Width}x{mod.Renderer.Height}, gpu {mod.Settings.GpuRender}");
			List<(string, int, string)> list = new List<(string, int, string)>();
			string[] array = new string[4] { "human", "elf", "orc", "dwarf" };
			foreach (string item in array)
			{
				int[] array2 = new int[2] { 1, 3 };
				foreach (int item2 in array2)
				{
					list.Add((item, item2, null));
				}
			}
			array = new string[8] { "human_stone", "human_timber", "elf_spire", "elf_pavilion", "orc_lodge", "orc_skull", "dwarf_lodge", "dwarf_stump" };
			foreach (string text in array)
			{
				list.Add((text.Substring(0, text.IndexOf('_')), 3, text));
			}
			array = new string[4] { "tent_human", "tent_elf", "tent_orc", "tent_dwarf" };
			foreach (string text2 in array)
			{
				list.Add((text2.Substring(5), 0, text2));
			}
			bool cpu = options.Contains("cpu");
			int seatedRooms = 0;
			int roomsWithSeats = 0;
			foreach (var item4 in list)
			{
				string text3 = item4.Item3;
				if (text3 == null)
				{
					(text3, _, _) = item4;
				}
				string tag = $"{text3}_{item4.Item2}";
				try
				{
					mod.EnterRoomForTest(people, item4.Item1, item4.Item2, item4.Item3);
				}
				catch (Exception arg)
				{
					Log.Error($"HOMEROOM {tag}: could not go in: {arg}");
					continue;
				}
				yield return new WaitForSecondsRealtime(0.6f);
				if (!mod.Interior.Inside || mod.Interior.Room == null)
				{
					Log.Error("HOMEROOM " + tag + ": no room");
					continue;
				}
				Interior room = mod.Interior.Room;
				int seats = 0;
				foreach (float[] spot in room.Spots)
				{
					if (spot.Length > 3 && spot[3] > 0f)
					{
						seats++;
					}
				}
				float t = mod.ViewYaw;
				mod.SetPitchDegrees(-12f);
				(string, float)[] array3 = new(string, float)[3]
				{
					("door", 0f),
					("left", 0.85f),
					("right", -0.85f)
				};
				for (int k = 0; k < array3.Length; k++)
				{
					var (view, num) = array3[k];
					mod.SetYaw(t + num);
					yield return new WaitForSecondsRealtime(0.35f);
					mod.SaveFrame("homeroom_" + tag + "_" + view + ".png");
				}
				mod.SetYaw(t);
				int seatedEver = 0;
				float worstMs = 0f;
				float sumMs = 0f;
				int frames = 0;
				for (float t2 = 0f; t2 < 9f; t2 += Time.unscaledDeltaTime)
				{
					yield return null;
					worstMs = Mathf.Max(worstMs, Time.unscaledDeltaTime * 1000f);
					sumMs += Time.unscaledDeltaTime * 1000f;
					frames++;
					int num2 = 0;
					foreach (RoomWalkers.Walker item5 in mod.Interior.WalkersForTest)
					{
						if (item5.Sit > 0.9f)
						{
							num2++;
						}
					}
					seatedEver = Math.Max(seatedEver, num2);
				}
				mod.SaveFrame("homeroom_" + tag + "_later.png");
				if (cpu)
				{
					mod.Settings.GpuRender = false;
					yield return new WaitForSecondsRealtime(0.5f);
					mod.SaveFrame("homeroom_" + tag + "_cpu.png");
					mod.Settings.GpuRender = true;
				}
				List<string> list2 = new List<string>();
				foreach (RoomWalkers.Walker item6 in mod.Interior.WalkersForTest)
				{
					list2.Add($"({item6.Pos.x:F1},{item6.Pos.y:F1} sit {item6.Sit:F1})");
				}
				Log.Info(string.Format("HOMEROOM {0}: {1} models, {2} lights, {3} spots ({4} seats), skipped {5}{6}; ", tag, room.Meshes.Count, room.Lights.Count, room.Spots.Count, seats, room.Skipped.Count, (room.Skipped.Count > 0) ? (" (" + string.Join("; ", room.Skipped) + ")") : "") + string.Format("{0:F0} fps (worst {1:F0} ms), most seated at once {2}; people {3}", (float)frames / 9f, worstMs, seatedEver, string.Join(" ", list2)));
				if (seats > 0)
				{
					roomsWithSeats++;
					if (seatedEver > 0)
					{
						seatedRooms++;
					}
				}
				mod.LeaveRoomForTest();
				yield return new WaitForSecondsRealtime(0.4f);
			}
			Log.Info($"HOMEROOM someone sat down in {seatedRooms} of {roomsWithSeats} rooms with seats");
			if (roomsWithSeats > 0 && seatedRooms == 0)
			{
				Log.Error("HOMEROOM nobody ever sat on a seat");
			}
			Finish(quit);
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
			Log.Info("HOMEROOM DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
