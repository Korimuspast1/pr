using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class TorchTest : CivilTest
	{
		private Actor _host;

		private Vector2 _home;

		protected override string Prefix => "TORCH";

		private Hands Hands => Mod.HandsForTest;

		protected override IEnumerator Body()
		{
			Check("its model is in the library", Models.Get("held_torch") != null);
			Check("its icon", Supplies.Icon("tw_item_torch") != null);
			WorldTile worldTile = CivilTest.Stage();
			Actor king = ((worldTile != null) ? Village("human", 4, 1, worldTile) : Village("human", 4, 1));
			if (!Check("a village", king != null))
			{
				yield break;
			}
			_host = Founded.Find((Actor u) => u != null && u != king && u.isAdult());
			Actor actor = Founded.Find((Actor u) => u != null && !u.isAdult());
			if (!Check("someone to be", _host != null))
			{
				yield break;
			}
			Check("a grown person carries a torch", Torches.Count(_host) == 1, Torches.Count(_host).ToString());
			if (actor != null)
			{
				Check("a child doesn't", Torches.Count(actor) == 0, Torches.Count(actor).ToString());
			}
			Check("it isn't lit", !Torches.Lit(_host));
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
			Check("still one in the bag once yours", Torches.Count(_host) == 1, Torches.Count(_host).ToString());
			_home = OpenGround(_host.current_position);
			_host.current_position = _home;
			foreach (Actor item in Founded)
			{
				if (item != null && item != _host && item.isAlive())
				{
					item.current_position = _home + new Vector2(16f, 10f);
					try
					{
						item.makeWait(160f);
					}
					catch
					{
					}
				}
			}
			bool gpu = Mod.Settings.GpuRender;
			int timeMode = Mod.Settings.TimeMode;
			Mod.Settings.TimeMode = 3;
			Mod.ForceRelight();
			Torches.Out = false;
			yield return Part("in your left hand, unlit", LeftUnlit());
			yield return Part("lit at a fire", LitAtFire());
			yield return Part("rain puts it out", Rain());
			yield return Part("water puts it out", Water());
			yield return Part("in your right hand, a blow", RightHand());
			yield return Part("held against a tree", AgainstTree());
			yield return Part("people carry theirs", Carriers());
			yield return Part("what it costs", Cost());
			Torches.Out = false;
			Mod.TorchReachForTest = false;
			Mod.TorchRainForTest = -1f;
			Mod.TorchWaterForTest = false;
			Mod.Settings.GpuRender = gpu;
			Mod.Settings.TimeMode = timeMode;
			Mod.ForceRelight();
		}

		private static Vector2 OpenGround(Vector2 from)
		{
			for (float num = 9f; num < 22f; num += 1.5f)
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
							if (tile == null || tile.main_type == null || tile.main_type.liquid || tile.main_type.block || tile.building != null)
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

		private void Picture(string name)
		{
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, "torch_" + name + ".png"));
			Mod.SaveFrame("torch_" + name + "_frame.png");
		}

		private int Count(FxParticles.Kind kind)
		{
			int num = 0;
			foreach (FxParticles.P item in SceneCollector.FxPool.List)
			{
				if (item.Kind == kind)
				{
					num++;
				}
			}
			return num;
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
				Log.Info("TORCH equip " + id + ": " + ex.Message);
				return false;
			}
		}

		private static void Unequip(Actor a)
		{
			try
			{
				ActorEquipmentSlot slot = a.equipment.getSlot(EquipmentType.Weapon);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				a.setStatsDirty();
			}
			catch
			{
			}
		}

		private static string V(Vec3 v)
		{
			return $"({v.X:F2}, {v.Y:F2}, {v.Z:F2})";
		}

		private void Lights(string when)
		{
			int num = 0;
			Vector3 eyeForTest = Mod.EyeForTest;
			foreach (PointLight light in Mod.Renderer.Lights)
			{
				if ((light.X - eyeForTest.x) * (light.X - eyeForTest.x) + (light.Y - eyeForTest.y) * (light.Y - eyeForTest.y) < 4f)
				{
					num++;
					Note($"{when}: light near you at ({light.X:F2}, {light.Y:F2}, {light.Z:F2}) r {light.Radius:F1} rgb {light.R:F2} {light.G:F2} {light.B:F2}");
				}
			}
			Note($"{when}: torch lights {Mod.TorchLightsForTest}, all {Mod.Renderer.Lights.Count}, near you {num}, night {Mod.Renderer.Air.Night:F2}, gpu {Mod.Settings.GpuRender}");
		}

		private IEnumerator TakeUp()
		{
			Mod.Holstered = false;
			if (!Torches.Out)
			{
				Torches.Toggle(_host, base.Note);
			}
			yield return CivilTest.Wait(0.8f);
		}

		private IEnumerator LeftUnlit()
		{
			Torches.SetLit(_host, lit: false);
			Mod.SetPitchDegrees(-4f);
			Unequip(_host);
			yield return TakeUp();
			Check("L with empty hands: in your right hand", Torches.Out && Hands.TorchShown && Hands.TorchInMain && !Hands.TorchLeftShown, $"out {Torches.Out}, right {Hands.TorchShown}, left {Hands.TorchLeftShown}");
			Picture("right_from_l");
			Check("a sword in hand", Equip(_host, "sword_iron"));
			yield return CivilTest.Wait(1f);
			Check("L beside a sword: in your left hand", Torches.Out && Hands.TorchLeftShown && !Hands.TorchShown, $"left {Hands.TorchLeftShown}, right {Hands.TorchShown}");
			Check("unlit: no flames", Mod.TorchSpritesForTest == 0, $"{Mod.TorchSpritesForTest} flames");
			Check("unlit: no light", Mod.TorchLightsForTest == 0, $"{Mod.TorchLightsForTest} lights");
			Note($"flame at {V(Hands.TorchLeft.Flame)}, eye {Mod.EyeForTest}, scale {Hands.TorchLeft.Scale:F2}");
			Picture("left_unlit");
			Torches.Toggle(_host, base.Note);
			yield return CivilTest.Wait(0.5f);
			Check("L again: put away", !Torches.Out && !Hands.TorchLeftShown);
			yield return TakeUp();
		}

		private IEnumerator LitAtFire()
		{
			Torches.SetLit(_host, lit: false);
			Mod.TorchReachForTest = true;
			Mod.SetPitchDegrees(-20f);
			yield return CivilTest.Wait(0.6f);
			Vec3 flame = Hands.TorchLeft.Flame;
			Note($"held out: flame at {V(flame)}, eye {Mod.EyeForTest}");
			List<WorldTile> burning = new List<WorldTile>();
			for (int i = -1; i <= 1; i++)
			{
				for (int j = -1; j <= 1; j++)
				{
					WorldTile tile = World.world.GetTile((int)flame.X + j, (int)flame.Y + i);
					try
					{
						if (tile != null && tile.startFire(pForce: true))
						{
							burning.Add(tile);
						}
					}
					catch
					{
					}
				}
			}
			if (!Check("the ground before you burning", burning.Count > 0, $"{burning.Count} tiles"))
			{
				Mod.TorchReachForTest = false;
				yield break;
			}
			float t0 = Time.unscaledTime;
			while (Time.unscaledTime - t0 < 4f && !Torches.Lit(_host))
			{
				_host.current_position = _home;
				foreach (WorldTile item in burning)
				{
					try
					{
						if (!item.isOnFire())
						{
							item.startFire(pForce: true);
						}
					}
					catch
					{
					}
				}
				yield return null;
			}
			Check("held into the fire, it catches", Torches.Lit(_host), $"after {Time.unscaledTime - t0:F1} s, last {Mod.TorchLastForTest}");
			foreach (WorldTile item2 in burning)
			{
				try
				{
					item2.stopFire();
				}
				catch
				{
				}
			}
			Mod.TorchReachForTest = false;
			Mod.SetPitchDegrees(-4f);
			yield return CivilTest.Wait(1.2f);
			Check("its flame over the cup", Mod.TorchSpritesForTest >= 1, $"{Mod.TorchSpritesForTest} flames");
			Check("its light at night", Mod.TorchLightsForTest >= 1, $"{Mod.TorchLightsForTest} lights");
			int embers = 0;
			int smoke = 0;
			for (int k = 0; k < 40; k++)
			{
				embers = Math.Max(embers, Count(FxParticles.Kind.Ember));
				smoke = Math.Max(smoke, Count(FxParticles.Kind.Smoke));
				yield return null;
			}
			Check("embers and smoke off it", embers > 0 && smoke > 0, $"{embers} embers, {smoke} smoke");
			bool[] array = new bool[2] { false, true };
			foreach (bool card in array)
			{
				Mod.Settings.GpuRender = card;
				yield return CivilTest.Wait(0.6f);
				Picture("left_lit_" + (card ? "gpu" : "cpu"));
				Check("lit on the " + (card ? "graphics card" : "CPU"), Hands.TorchLeftShown && Mod.TorchSpritesForTest >= 1, $"{Mod.TorchSpritesForTest} flames, frame {Mod.FrameMs:F1} ms");
			}
			Torches.Toggle(_host, base.Note);
			yield return CivilTest.Wait(0.6f);
			Torches.Toggle(_host, base.Note);
			yield return CivilTest.Wait(0.8f);
			Check("put away and taken up again, still lit", Torches.Lit(_host) && Mod.TorchSpritesForTest >= 1);
		}

		private IEnumerator Rain()
		{
			Torches.SetLit(_host, lit: true);
			yield return CivilTest.Wait(0.5f);
			Mod.TorchRainForTest = 1f;
			float t0 = Time.unscaledTime;
			float picture = -1f;
			while (Time.unscaledTime - t0 < 8f && Torches.Lit(_host))
			{
				if (picture < 0f && Time.unscaledTime - t0 > 2.4f)
				{
					picture = 1f;
					Picture("guttering");
				}
				yield return null;
			}
			float num = Time.unscaledTime - t0;
			Check("in heavy rain it gutters and goes out", !Torches.Lit(_host) && num > 2f && num < 7f, $"{num:F1} s, last {Mod.TorchLastForTest}");
			yield return CivilTest.Wait(0.3f);
			Check("steam off it as it goes out", Count(FxParticles.Kind.Steam) > 0, $"{Count(FxParticles.Kind.Steam)} puffs");
			Mod.TorchRainForTest = -1f;
		}

		private IEnumerator Water()
		{
			Torches.SetLit(_host, lit: true);
			yield return CivilTest.Wait(0.4f);
			Mod.TorchWaterForTest = true;
			yield return CivilTest.Wait(0.3f);
			Check("water puts it out at once", !Torches.Lit(_host), Mod.TorchLastForTest);
			Mod.TorchWaterForTest = false;
		}

		private IEnumerator RightHand()
		{
			Torches.Out = false;
			Torches.SetLit(_host, lit: true);
			Hotbar.Sync(_host, base.Note);
			int num = BagOrder.Of(_host).IndexOf(BagOrder.ResourceKey("tw_item_torch"));
			int num2 = Hotbar.Chosen(_host);
			if (num >= 0 && num != num2)
			{
				BagOrder.Swap(_host, num, num2);
			}
			Hotbar.Moved();
			yield return CivilTest.Wait(0.8f);
			Check("on the hotbar: in your right hand", Torches.InMain(_host) && Hands.TorchShown && Hands.TorchInMain, $"in hand {Hotbar.InHand(_host)}, shown {Hands.TorchShown}");
			Check("lit, in your right hand", Mod.TorchSpritesForTest >= 1, $"{Mod.TorchSpritesForTest} flames");
			Lights("right hand");
			Check("its light at night", Mod.TorchLightsForTest >= 1, $"{Mod.TorchLightsForTest} lights");
			Picture("right_lit");
			Vector3 eyeForTest = Mod.EyeForTest;
			float viewYaw = Mod.ViewYaw;
			Actor sheep = CivilTest.Spawn("sheep", new Vector2(eyeForTest.x + Mathf.Cos(viewYaw) * 1f, eyeForTest.y + Mathf.Sin(viewYaw) * 1f));
			if (!Check("a sheep before you", sheep != null))
			{
				yield break;
			}
			try
			{
				sheep.data.health = 100000;
				sheep.makeWait(30f);
			}
			catch
			{
			}
			bool burning = false;
			for (int k = 0; k < 4; k++)
			{
				if (burning)
				{
					break;
				}
				Vector2 current_position = sheep.current_position;
				Mod.SetYaw(Mathf.Atan2(current_position.y - Mod.EyeForTest.y, current_position.x - Mod.EyeForTest.x));
				Mod.SetPitchDegrees(-14f);
				yield return null;
				PossessionCombat.AttackForTest(_host);
				float t0 = Time.unscaledTime;
				while (Time.unscaledTime - t0 < 1f && !burning)
				{
					burning = Torches.Burning(sheep);
					yield return null;
				}
			}
			Check("a blow with it lit sets the sheep burning", burning);
			if (burning)
			{
				Picture("sheep_burning");
			}
			CivilTest.Kill(sheep);
			Mod.TorchReachForTest = true;
			yield return CivilTest.Wait(0.6f);
			Check("held out ahead", Mod.HandsForTest.TorchShown && PossessionHooks.BlockAttack, $"blocks {PossessionHooks.BlockAttack}");
			Picture("right_held_out");
			Mod.TorchReachForTest = false;
			yield return CivilTest.Wait(0.3f);
		}

		private IEnumerator AgainstTree()
		{
			Building target = null;
			Building building = null;
			float num = 3600f;
			float num2 = 3600f;
			foreach (Building simple in World.world.buildings.getSimpleList())
			{
				try
				{
					if (simple != null && simple.isAlive() && simple.asset != null && simple.current_tile != null && simple.isBurnable())
					{
						float sqrMagnitude = (new Vector2(simple.current_tile.x, simple.current_tile.y) - _home).sqrMagnitude;
						if (simple.asset.flora && sqrMagnitude < num)
						{
							num = sqrMagnitude;
							target = simple;
						}
						else if (!simple.asset.flora && simple.city != null && sqrMagnitude < num2)
						{
							num2 = sqrMagnitude;
							building = simple;
						}
					}
				}
				catch
				{
				}
			}
			bool town = target == null;
			if (town)
			{
				target = building;
			}
			if (!Check("something that burns near", target != null))
			{
				yield break;
			}
			Note(string.Format("held against {0} ({1}), {2:F1} tiles off", target.asset.id, town ? "a town's" : "a plant", Mathf.Sqrt(town ? num2 : num)));
			Torches.SetLit(_host, lit: true);
			Vector2 at = new Vector2((float)target.current_tile.x + 0.5f, (float)target.current_tile.y + 0.5f);
			Vector2 stand = at + new Vector2(-1.1f, 0f);
			for (int i = 1; i < 5; i++)
			{
				if (World.world.GetTile((int)stand.x, (int)stand.y)?.building == null)
				{
					break;
				}
				stand = at + new Vector2(-1.1f - (float)i * 0.5f, 0f);
			}
			Law.LastCrime = null;
			Mod.TorchReachForTest = true;
			float t0 = Time.unscaledTime;
			bool caught = false;
			while (Time.unscaledTime - t0 < 4f && !caught)
			{
				_host.current_position = stand;
				Mod.SetYaw(0f);
				Mod.SetPitchDegrees(-6f);
				caught = Torches.Burning(target) || (target.current_tile != null && target.current_tile.isOnFire());
				yield return null;
			}
			Check("held against it, it catches", caught, $"{target.asset.id}: last {Mod.TorchLastForTest}, flame {V(Mod.HandsForTest.Torch.Flame)}, it at {at}");
			if (town)
			{
				Note(string.Format("crime: {0}, {1} saw", Law.LastCrime ?? "none", Law.LastWitnesses));
			}
			if (town)
			{
				Check("a town's building set alight is arson", Law.LastCrime == "arson", Law.LastCrime);
			}
			Picture("set_alight");
			Mod.TorchReachForTest = false;
			_host.current_position = _home;
			yield return CivilTest.Wait(0.5f);
		}

		private IEnumerator Carriers()
		{
			Hotbar.Sync(_host, base.Note);
			Mod.Holstered = true;
			Torches.Out = false;
			Vector3 eye = Mod.EyeForTest;
			float yaw = Mod.ViewYaw;
			Actor bandit = CivilTest.Spawn("bandit", new Vector2(eye.x + Mathf.Cos(yaw) * 3f, eye.y + Mathf.Sin(yaw) * 3f));
			if (!Check("a bandit before you", bandit != null))
			{
				yield break;
			}
			try
			{
				bandit.makeWait(30f);
			}
			catch
			{
			}
			Equip(bandit, "sword_iron");
			int seen = 0;
			int flames = 0;
			for (int i = 0; i < 90; i++)
			{
				bandit.current_position = new Vector2(eye.x + Mathf.Cos(yaw) * 3f, eye.y + Mathf.Sin(yaw) * 3f);
				Vector2 current_position = bandit.current_position;
				Mod.SetYaw(Mathf.Atan2(current_position.y - eye.y, current_position.x - eye.x));
				Mod.SetPitchDegrees(-6f);
				if (i % 10 == 0)
				{
					try
					{
						bandit.makeWait(30f);
					}
					catch
					{
					}
				}
				if (People.TorchesDrawn.Count > 0)
				{
					seen++;
				}
				flames = Math.Max(flames, Mod.TorchOthersForTest);
				if (i == 60)
				{
					Picture("carrier");
				}
				yield return null;
			}
			Check("a bandit carries a torch lit at night", seen > 30, $"{seen} of 90 frames");
			Check("its flame drawn", flames >= 1, $"{flames} flames");
			CivilTest.Kill(bandit);
			Mod.Holstered = false;
		}

		private IEnumerator Cost()
		{
			Mod.Holstered = false;
			if (Torches.InMain(_host))
			{
				Hotbar.Choose(_host, (Hotbar.Chosen(_host) + 1) % Math.Max(1, Hotbar.Size(_host)), wheel: false);
			}
			Torches.SetLit(_host, lit: true);
			Mod.SetPitchDegrees(-4f);
			bool[] array = new bool[2] { false, true };
			foreach (bool card in array)
			{
				Mod.Settings.GpuRender = card;
				float[] sum = new float[2];
				int[] n = new int[2];
				for (int pass = 0; pass < 6; pass++)
				{
					int which = pass & 1;
					Torches.Out = which == 1;
					yield return CivilTest.Wait(0.7f);
					float t0 = Time.unscaledTime;
					while (Time.unscaledTime - t0 < 1.5f)
					{
						_host.current_position = _home;
						if (pass >= 2)
						{
							sum[which] += Mod.FrameMs;
							n[which]++;
						}
						yield return null;
					}
				}
				float num = sum[0] / (float)Math.Max(1, n[0]);
				float num2 = sum[1] / (float)Math.Max(1, n[1]);
				string text = (card ? "graphics card" : "CPU");
				Note($"cost on the {text} ({Mod.Renderer.Width}x{Mod.Renderer.Height}) at night: no torch {num:F2} ms a frame, lit torch {num2:F2} ms");
				if (num > (card ? 12f : 30f))
				{
					Note("(the " + text + "'s frames are too slow here to judge the torch's cost: the machine is busy)");
				}
				else
				{
					Check("a lit torch costs under 2.5 ms a frame on the " + text, num2 - num < 2.5f, $"{num2 - num:+0.00;-0.00} ms");
				}
			}
		}
	}
}
