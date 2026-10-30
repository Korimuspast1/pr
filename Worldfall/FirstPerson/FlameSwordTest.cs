using System;
using System.Collections;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class FlameSwordTest : CivilTest
	{
		private Actor _host;

		private Vector2 _home;

		protected override string Prefix => "FLAME";

		protected override IEnumerator Body()
		{
			Check("the game has the flame sword", AssetManager.items.get("flame_sword") != null);
			Check("its model is in the library", Models.Get("held_flame_sword") != null);
			MeshModel meshModel = ViewMeshes.Item(ViewMeshes.LookOf("flame_sword", null));
			Check("a flame sword is drawn with it", FlameSword.IsModel(meshModel), meshModel?.Name);
			Check("an iron sword is not", !FlameSword.IsModel(ViewMeshes.Item(ViewMeshes.LookOf("sword_iron", null))));
			WorldTile worldTile = CivilTest.Stage();
			Actor king = ((worldTile != null) ? Village("human", 4, 0, worldTile) : Village("human", 4, 0));
			if (!Check("a village", king != null))
			{
				yield break;
			}
			_host = Founded.Find((Actor u) => u != null && u != king && u.isAdult());
			if (!Check("someone to be", _host != null))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)_host).addStatusEffect("invincible", 600f, pColorEffect: false);
			}
			catch
			{
			}
			yield return Possess(_host);
			if (!Check("first person", InControl(_host)))
			{
				yield break;
			}
			_home = OpenGround(_host.current_position);
			_host.current_position = _home;
			foreach (Actor item in Founded)
			{
				if (item != null && item != _host && item.isAlive())
				{
					item.current_position = _home + new Vector2(14f, 9f);
					try
					{
						item.makeWait(120f);
					}
					catch
					{
					}
				}
			}
			bool gpu = Mod.Settings.GpuRender;
			int timeMode = Mod.Settings.TimeMode;
			Mod.Settings.TimeMode = 1;
			Mod.ForceRelight();
			yield return Part("in your hand", InHand());
			yield return Part("what it costs", Cost());
			yield return Part("a cut", Cut());
			yield return Part("at night", Night());
			Mod.Settings.TimeMode = 1;
			Mod.ForceRelight();
			yield return Part("someone else's", Holder());
			Mod.Settings.GpuRender = gpu;
			Mod.Settings.TimeMode = timeMode;
			Mod.ForceRelight();
		}

		private static Vector2 OpenGround(Vector2 from)
		{
			for (float num = 9f; num < 20f; num += 1.5f)
			{
				for (int i = 0; i < 16; i++)
				{
					float f = (float)i * MathF.PI / 8f;
					Vector2 vector = from + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num;
					bool flag = true;
					for (int j = -3; j <= 3 && flag; j++)
					{
						for (int k = -3; k <= 3 && flag; k++)
						{
							WorldTile tile = World.world.GetTile((int)vector.x + k, (int)vector.y + j);
							if (tile == null || tile.main_type == null || tile.main_type.liquid || tile.main_type.block)
							{
								flag = false;
							}
							else if (tile.building != null && AssetStyle.IsSolid(tile.building) && tile.building.asset.building_type != BuildingType.Building_Tree && tile.building.asset.building_type != BuildingType.Building_Mineral && tile.building.asset.building_type != BuildingType.Building_Plant)
							{
								flag = false;
							}
						}
					}
					if (flag)
					{
						return new Vector2((float)(int)vector.x + 0.5f, (float)(int)vector.y + 0.5f);
					}
				}
			}
			return from;
		}

		private static bool Equip(Actor a, string id)
		{
			try
			{
				ActorEquipmentSlot slot = a.equipment.getSlot(EquipmentType.Weapon);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				if (equipmentAsset == null)
				{
					return false;
				}
				Item pItem = World.world.items.generateItem(equipmentAsset, a.kingdom, a.getName(), 1, a, 0, pByPlayer: true);
				slot.setItem(pItem, a);
				a.setStatsDirty();
				return true;
			}
			catch (Exception ex)
			{
				Log.Info("FLAME equip " + id + ": " + ex.Message);
				return false;
			}
		}

		private int Embers()
		{
			int num = 0;
			foreach (FxParticles.P item in SceneCollector.FxPool.List)
			{
				if (item.Kind == FxParticles.Kind.Ember)
				{
					num++;
				}
			}
			return num;
		}

		private void Picture(string name)
		{
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "flame_" + name + ".png"));
		}

		private IEnumerator InHand()
		{
			Mod.Holstered = false;
			if (Check("the flame sword in hand", Equip(_host, "flame_sword")))
			{
				Mod.SetPitchDegrees(4f);
				yield return CivilTest.Wait(1.2f);
				Hands hands = Mod.HandsForTest;
				Check("drawn with its own model, its fire posed", hands.FlameShown, $"shown {hands.Flame.Shown}, posed at {hands.Flame.PosedAt:F2} (now {Time.unscaledTime:F2})");
				Check("flames on the blade", Mod.FlameSpritesForTest >= 7, $"{Mod.FlameSpritesForTest} flames");
				Check("no light of its own by day", Mod.FlameLightsForTest == 0, $"{Mod.FlameLightsForTest} lights");
				int embers = 0;
				for (int i = 0; i < 30; i++)
				{
					embers = Math.Max(embers, Embers());
					yield return null;
				}
				Check("embers rising off it", embers > 0, $"{embers} at most");
				Note($"blade root {hands.Flame.Base}, point {hands.Flame.Point}, scale {hands.Flame.Scale:F2}, eye {Mod.EyeForTest}");
				bool[] array = new bool[2] { false, true };
				foreach (bool card in array)
				{
					Mod.Settings.GpuRender = card;
					yield return CivilTest.Wait(0.6f);
					string text = (card ? "gpu" : "cpu");
					Picture("ready_" + text);
					Mod.SaveFrame("flame_ready_frame_" + text + ".png");
					yield return CivilTest.Wait(0.3f);
					Check("still drawn on the " + (card ? "graphics card" : "CPU"), hands.FlameShown && Mod.FlameSpritesForTest >= (card ? 10 : 7), $"{Mod.FlameSpritesForTest} flames, frame {Mod.FrameMs:F1} ms");
				}
			}
		}

		private IEnumerator Cut()
		{
			Mod.Holstered = false;
			Mod.DebugEyeSet = false;
			_host.current_position = _home;
			yield return CivilTest.Wait(0.3f);
			Vector3 eyeForTest = Mod.EyeForTest;
			float viewYaw = Mod.ViewYaw;
			Actor sheep = CivilTest.Spawn("sheep", new Vector2(eyeForTest.x + Mathf.Cos(viewYaw) * 1.1f, eyeForTest.y + Mathf.Sin(viewYaw) * 1.1f));
			if (sheep != null)
			{
				try
				{
					sheep.data.health = 100000;
					sheep.makeWait(30f);
				}
				catch
				{
				}
			}
			yield return CivilTest.Wait(0.5f);
			Hands hands = Mod.HandsForTest;
			bool[] array = new bool[2] { true, false };
			foreach (bool flag in array)
			{
				Mod.Settings.GpuRender = flag;
				string tag = (flag ? "gpu" : "cpu");
				yield return CivilTest.Wait(0.8f);
				if (sheep != null && sheep.isAlive())
				{
					Vector2 current_position = sheep.current_position;
					Mod.SetYaw(Mathf.Atan2(current_position.y - Mod.EyeForTest.y, current_position.x - Mod.EyeForTest.x));
					Mod.SetPitchDegrees(-12f);
				}
				yield return null;
				PossessionCombat.AttackForTest(_host);
				float t0 = Time.unscaledTime;
				float heat = 0f;
				float firstArc = -1f;
				float lastArc = -1f;
				int arc = 0;
				int pictures = 0;
				while (Time.unscaledTime - t0 < 1.4f)
				{
					_host.current_position = _home;
					heat = Mathf.Max(heat, hands.Flame.Heat);
					if (hands.Flame.ArcFrames >= 2)
					{
						if (firstArc < 0f)
						{
							firstArc = Time.unscaledTime - t0;
						}
						lastArc = Time.unscaledTime - t0;
						arc = Math.Max(arc, hands.Flame.ArcFrames);
						if (pictures < 2)
						{
							Picture($"cut_{tag}_{pictures}");
							Mod.SaveFrame($"flame_cut_frame_{tag}_{pictures}.png");
							pictures++;
						}
					}
					yield return null;
				}
				Note($"{tag}: arc from {firstArc:F2} s to {lastArc:F2} s after the click, {arc} frames of it at most, heat {heat:F2}, frame {Mod.FrameMs:F1} ms");
				Check("the swing fans the fire (" + tag + ")", heat > 0.6f, heat.ToString("F2"));
				Check("a cut lays a crescent of fire (" + tag + ")", arc >= 2, $"{arc} frames");
				Check("which burns out after (" + tag + ")", hands.Flame.ArcFrames < 2 && lastArc < 1.2f, $"{hands.Flame.ArcFrames} frames left, last seen {lastArc:F2} s");
				yield return CivilTest.Wait(0.6f);
			}
			CivilTest.Kill(sheep);
		}

		private IEnumerator Night()
		{
			Mod.Settings.TimeMode = 3;
			Mod.ForceRelight();
			Mod.SetPitchDegrees(-8f);
			yield return CivilTest.Wait(1.5f);
			bool[] array = new bool[2] { true, false };
			foreach (bool card in array)
			{
				Mod.Settings.GpuRender = card;
				yield return CivilTest.Wait(0.6f);
				string tag = (card ? "gpu" : "cpu");
				Picture("night_" + tag);
				Mod.SaveFrame("flame_night_frame_" + tag + ".png");
				yield return null;
				Check("it lights the night (" + tag + ")", Mod.FlameLightsForTest >= 1 && Mod.HandsForTest.FlameShown, $"{Mod.FlameLightsForTest} lights");
			}
		}

		private IEnumerator Holder()
		{
			Mod.Holstered = true;
			Mod.DebugEyeSet = false;
			_host.current_position = _home;
			yield return CivilTest.Wait(0.3f);
			Vector3 eye = Mod.EyeForTest;
			float yaw = Mod.ViewYaw;
			Actor man = CivilTest.Spawn("human", new Vector2(eye.x + Mathf.Cos(yaw) * 3f, eye.y + Mathf.Sin(yaw) * 3f));
			if (!Check("someone in front of you", man != null))
			{
				yield break;
			}
			try
			{
				man.makeWait(30f);
			}
			catch
			{
			}
			Check("holding the flame sword", Equip(man, "flame_sword"));
			int seen = 0;
			int others = 0;
			for (int i = 0; i < 90; i++)
			{
				man.current_position = new Vector2(eye.x + Mathf.Cos(yaw) * 3f, eye.y + Mathf.Sin(yaw) * 3f);
				Vector2 current_position = man.current_position;
				Mod.SetYaw(Mathf.Atan2(current_position.y - eye.y, current_position.x - eye.x));
				Mod.SetPitchDegrees(-6f);
				if (i % 10 == 0)
				{
					try
					{
						man.makeWait(30f);
					}
					catch
					{
					}
				}
				foreach (FlameSword item in People.FlamesDrawn)
				{
					if (item.Shown && Time.unscaledTime - item.PosedAt < 0.1f)
					{
						seen++;
						break;
					}
				}
				others = Math.Max(others, Mod.FlameOthersForTest);
				if (i == 60)
				{
					Picture("holder");
					Mod.SaveFrame("flame_holder_frame.png");
				}
				yield return null;
			}
			Check("their flame sword drawn with its fire", seen > 30, $"{seen} of 90 frames");
			Check("flames on their blade", others >= 5, $"{others} flames");
			CivilTest.Kill(man);
			Mod.Holstered = false;
		}

		private IEnumerator Cost()
		{
			Mod.Holstered = false;
			Mod.DebugEyeSet = false;
			Mod.SetPitchDegrees(4f);
			bool[] array = new bool[2] { false, true };
			foreach (bool card in array)
			{
				Mod.Settings.GpuRender = card;
				float[] sum = new float[2];
				int[] n = new int[2];
				for (int pass = 0; pass < 4; pass++)
				{
					int which = pass & 1;
					Equip(_host, (which == 0) ? "sword_iron" : "flame_sword");
					yield return CivilTest.Wait(0.6f);
					float t0 = Time.unscaledTime;
					while (Time.unscaledTime - t0 < 1.5f)
					{
						_host.current_position = _home;
						sum[which] += Mod.FrameMs;
						n[which]++;
						yield return null;
					}
				}
				float num = sum[0] / (float)Math.Max(1, n[0]);
				float num2 = sum[1] / (float)Math.Max(1, n[1]);
				string text = (card ? "graphics card" : "CPU");
				Note($"cost on the {text} ({Mod.Renderer.Width}x{Mod.Renderer.Height}): iron sword {num:F2} ms a frame, flame sword {num2:F2} ms");
				Check("the flame sword costs under 2.5 ms a frame on the " + text, num2 - num < 2.5f, $"{num2 - num:+0.00;-0.00} ms");
			}
			Equip(_host, "flame_sword");
			yield return CivilTest.Wait(0.5f);
		}
	}
}
