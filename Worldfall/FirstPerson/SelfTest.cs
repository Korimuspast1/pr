using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class SelfTest : MonoBehaviour
	{
		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			bool quit = File.ReadAllText(path).Contains("quit");
			Log.Info("SELFTEST waiting for a loaded world");
			float waited = 0f;
			while (!Config.game_loaded || World.world == null || World.world.units == null)
			{
				waited += Time.unscaledDeltaTime;
				if (waited > 180f)
				{
					Log.Error("SELFTEST gave up waiting for the world");
					yield break;
				}
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
				if (!flag && t >= 4f && World.world.units.Count > 0)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(1f);
			}
			Log.Info($"SELFTEST world {MapBox.width}x{MapBox.height}, units {World.world.units.Count}, buildings {World.world.buildings.Count}");
			Actor unit = PickUnit();
			if (unit == null)
			{
				Log.Info("SELFTEST no creature to possess, spawning humans");
				unit = SpawnSome();
				yield return new WaitForSecondsRealtime(2f);
			}
			if (unit == null)
			{
				Log.Error("SELFTEST FAIL could not find or spawn a creature");
				yield break;
			}
			Sprite spriteToRender = unit.getSpriteToRender();
			if (spriteToRender != null)
			{
				Log.Info($"SELFTEST host {unit.asset.id}: sprite {spriteToRender.name} rect {spriteToRender.rect} ppu {spriteToRender.pixelsPerUnit} scale {unit.current_scale} readable {spriteToRender.texture.isReadable}");
			}
			ControllableUnit.setControllableCreature(unit);
			yield return new WaitForSecondsRealtime(1.5f);
			WorldBoxMod mod = WorldBoxMod.Instance;
			Check("first person activates on possession", mod != null && mod.IsFirstPerson);
			Check("movement hook installed" + ((PossessionHooks.Error != null) ? (" (" + PossessionHooks.Error + ")") : ""), PossessionHooks.Installed);
			Check("cursor locked", Cursor.lockState == CursorLockMode.Locked);
			float[] array = new float[3] { 0f, 90f, 225f };
			for (int i = 0; i < array.Length; i++)
			{
				float t = array[i];
				PossessionHooks.Yaw = t * (MathF.PI / 180f);
				if (mod != null)
				{
					mod.SetYaw(t * (MathF.PI / 180f));
				}
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				for (int j = 0; j < 20; j++)
				{
					yield return null;
				}
				ControllableUnit.updateMovementVectorKeyboard();
				Vector2 v = ControllableUnit._movement_vector;
				PossessionHooks.ScriptedInput = Vector2.zero;
				yield return new WaitForSecondsRealtime(0.4f);
				Vector2 vector = new Vector2(Mathf.Cos(t * (MathF.PI / 180f)), Mathf.Sin(t * (MathF.PI / 180f)));
				Check($"forward at yaw {t} -> {v} (expected {vector})", v.sqrMagnitude > 0f && (v.normalized - vector).magnitude < 0.03f);
			}
			List<float> times = new List<float>();
			for (int i = 0; i < 4; i++)
			{
				if (!(mod != null))
				{
					break;
				}
				if (!mod.IsFirstPerson)
				{
					break;
				}
				mod.SetYaw((float)i * MathF.PI * 0.5f);
				for (int j = 0; j < 30; j++)
				{
					yield return null;
					times.Add(mod.RenderMs);
				}
				string text = mod.SaveScreenshot();
				Log.Info(string.Format("SELFTEST view yaw {0}: {1} billboards, picked {2}, eye z {3:F2}, frame {4}", i * 90, mod.BillboardCount, (mod.Picked != null) ? mod.Picked.GetType().Name : "nothing", mod.View.Z, text));
			}
			if (times.Count > 0)
			{
				times.Sort();
				Log.Info($"SELFTEST render ms median {times[times.Count / 2]:F2}, p90 {times[times.Count * 9 / 10]:F2}, fps {1f / Time.smoothDeltaTime:F0}");
			}
			if (mod != null && mod.HostImage != null)
			{
				Log.Info($"SELFTEST host visible rows {mod.HostImage.OpaqueBottom}-{mod.HostImage.OpaqueTop} of {mod.HostImage.Height}, eye z {mod.View.Z:F2}");
			}
			if (mod != null && mod.IsFirstPerson)
			{
				bool[] array2 = new bool[2] { true, false };
				foreach (bool flora in array2)
				{
					Vector2? vector2 = DensestCluster(flora);
					if (!vector2.HasValue)
					{
						Log.Info("SELFTEST no " + (flora ? "forest" : "village") + " found");
						continue;
					}
					float num = (flora ? 90f : 45f);
					Vector2 vector3 = new Vector2(Mathf.Cos(num * (MathF.PI / 180f)), Mathf.Sin(num * (MathF.PI / 180f))) * (flora ? 6f : 9f);
					mod.DebugEye = vector2.Value - vector3;
					mod.DebugEyeSet = true;
					mod.SetYaw(num * (MathF.PI / 180f));
					yield return new WaitForSecondsRealtime(1.2f);
					string name = (flora ? "selftest_forest.png" : "selftest_village.png");
					ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, name));
					yield return new WaitForSecondsRealtime(0.4f);
					Log.Info($"SELFTEST {name} at {mod.DebugEye}: {mod.VoxelCount} voxel models, {mod.BillboardCount} sprites, terrain {mod.Renderer.TerrainMs:F2} ms, objects {mod.Renderer.ObjectsMs:F2} ms");
				}
				mod.DebugEyeSet = false;
				mod.SetYaw(0f);
				yield return new WaitForSecondsRealtime(0.5f);
				mod.SaveScreenshot();
				mod.Feedback.Hurt = 0.7f;
				mod.Feedback.HitMarker = 1f;
				mod.Feedback.SinceAttack = 0.1f;
				mod.Feedback.Texts.Add(new FloatingText
				{
					World = new Vector3(unit.current_position.x + Mathf.Cos(mod.View.Yaw) * 3f, unit.current_position.y + Mathf.Sin(mod.View.Yaw) * 3f, mod.View.Z),
					Text = "-12",
					Color = new Color(1f, 0.9f, 0.3f)
				});
			}
			yield return null;
			string capture = Path.Combine(Log.Folder, "selftest_screen.png");
			ScreenCapture.CaptureScreenshot(capture);
			yield return new WaitForSecondsRealtime(1f);
			Log.Info($"SELFTEST full-screen capture {capture}; terrain {mod.Renderer.TerrainMs:F2} ms, objects {mod.Renderer.ObjectsMs:F2} ms");
			if (mod != null && mod.IsFirstPerson)
			{
				mod.ForceSwim = true;
				yield return new WaitForSecondsRealtime(1.5f);
				string name = Path.Combine(Log.Folder, "selftest_swim.png");
				ScreenCapture.CaptureScreenshot(name);
				yield return new WaitForSecondsRealtime(0.5f);
				mod.ForceSwim = false;
				Log.Info($"SELFTEST swim capture {name}, eye z {mod.View.Z:F2}");
			}
			if (mod != null && mod.IsFirstPerson)
			{
				mod.DeathName = WorldBoxMod.Describe(unit);
				mod.DeathTimer = 0f;
				yield return new WaitForSecondsRealtime(1.5f);
				ScreenCapture.CaptureScreenshot(Path.Combine(Log.Folder, "selftest_death.png"));
				yield return new WaitForSecondsRealtime(2.5f);
				Check("view returns after the death screen while still in control", mod.IsFirstPerson);
			}
			ControllableUnit.clear(pCallKill: false);
			yield return new WaitForSecondsRealtime(1f);
			Check("first person ends when control is released", mod != null && !mod.IsFirstPerson);
			Check("cursor released", Cursor.lockState == CursorLockMode.None);
			Log.Info("SELFTEST DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private static void Check(string what, bool ok)
		{
			Log.Info("SELFTEST " + (ok ? "PASS " : "FAIL ") + what);
		}

		internal static Actor PickUnit()
		{
			Actor result = null;
			int num = -1;
			foreach (Actor simple in World.world.units.getSimpleList())
			{
				if (simple == null || !simple.isAlive() || !simple.canBePossessed())
				{
					continue;
				}
				int num2 = 0;
				foreach (Building simple2 in World.world.buildings.getSimpleList())
				{
					if (simple2 != null && simple2.isAlive() && simple2.asset != null && simple2.asset.flora && simple2.current_tile != null && (new Vector2(simple2.current_tile.x, simple2.current_tile.y) - simple.current_position).sqrMagnitude < 225f)
					{
						num2++;
					}
				}
				int num3 = (simple.isKingdomCiv() ? 1000 : 0) + Mathf.Min(num2, 200) * 5 + UnityEngine.Random.Range(0, 50);
				if (num3 > num)
				{
					result = simple;
					num = num3;
				}
			}
			return result;
		}

		internal static Vector2? DensestCluster(bool flora)
		{
			List<Vector2> list = new List<Vector2>();
			foreach (Building simple in World.world.buildings.getSimpleList())
			{
				if (simple == null || !simple.isAlive() || simple.asset == null || simple.current_tile == null)
				{
					continue;
				}
				bool num;
				if (!flora)
				{
					if (simple.asset.flora)
					{
						continue;
					}
					num = simple.isKingdomCiv();
				}
				else
				{
					num = simple.asset.flora;
				}
				if (num)
				{
					list.Add(new Vector2((float)simple.current_tile.x + 0.5f, (float)simple.current_tile.y + 0.5f));
				}
			}
			if (list.Count == 0)
			{
				return null;
			}
			Vector2 value = list[0];
			int num2 = -1;
			int num3 = Math.Max(1, list.Count / 400);
			for (int i = 0; i < list.Count; i += num3)
			{
				int num4 = 0;
				foreach (Vector2 item in list)
				{
					if ((item - list[i]).sqrMagnitude < 64f)
					{
						num4++;
					}
				}
				if (num4 > num2)
				{
					num2 = num4;
					value = list[i];
				}
			}
			return value;
		}

		internal static Actor SpawnSome()
		{
			MapBox world = World.world;
			Actor actor = null;
			for (int i = 0; i < 4000; i++)
			{
				if (actor != null)
				{
					break;
				}
				WorldTile tile = world.GetTile(UnityEngine.Random.Range(0, MapBox.width), UnityEngine.Random.Range(0, MapBox.height));
				if (!IsPlainGround(tile))
				{
					continue;
				}
				for (int j = 0; j < 6; j++)
				{
					WorldTile tile2 = world.GetTile(Mathf.Clamp(tile.x + UnityEngine.Random.Range(-4, 5), 0, MapBox.width - 1), Mathf.Clamp(tile.y + UnityEngine.Random.Range(-4, 5), 0, MapBox.height - 1));
					if (IsPlainGround(tile2))
					{
						Actor actor2 = world.units.spawnNewUnitByPlayer("human", tile2, pSpawnSound: false, pMiracleSpawn: false, 0f);
						if (actor == null && actor2 != null && actor2.canBePossessed())
						{
							actor = actor2;
						}
					}
				}
			}
			Log.Info("SELFTEST spawned " + ((actor != null) ? WorldBoxMod.Describe(actor) : "nothing"));
			return actor;
		}

		private static bool IsPlainGround(WorldTile tile)
		{
			if (tile != null && tile.main_type != null && !tile.main_type.liquid && !tile.main_type.lava && tile.main_type.height_min >= 98)
			{
				return tile.main_type.height_min < 199;
			}
			return false;
		}
	}
}
