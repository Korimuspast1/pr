using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class WondersTest : CivilTest
	{
		private static readonly (string Id, string Family, string Short)[] All = new(string, string, string)[13]
		{
			("waypoint_golden_egg", "nature_golden_egg", "egg"),
			("waypoint_harp", "nature_harp", "harp"),
			("waypoint_alien_mold", "nature_alien_mold", "mold"),
			("waypoint_computer", "nature_computer", "computer"),
			("tumor", "nature_tumor", "tumor"),
			("biomass", "nature_biomass", "biomass"),
			("super_pumpkin", "nature_super_pumpkin", "pumpkin"),
			("cybercore", "nature_cybercore", "cyber"),
			("golden_brain", "nature_golden_brain", "goldenbrain"),
			("corrupted_brain", "nature_corrupted_brain", "badbrain"),
			("flame_tower", "nature_flame_tower", "flametower"),
			("ice_tower", "nature_ice_tower", "icetower"),
			("angle_tower", "nature_angle_tower", "angletower")
		};

		private Actor _you;

		protected override string Prefix => "WOND";

		protected override IEnumerator Body()
		{
			WorldTile stage = CivilTest.Stage();
			if (!Check("a stage", stage != null))
			{
				yield break;
			}
			_you = CivilTest.SpawnNear("human", new Vector2((float)stage.x + 0.5f, (float)stage.y + 0.5f));
			if (!Check("someone to look through", _you != null))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)_you).addStatusEffect("invincible", 900f, pColorEffect: false);
			}
			catch
			{
			}
			yield return Possess(_you);
			bool only = Options.Contains("only");
			(string Id, string Family, string Short)[] all = All;
			for (int i = 0; i < all.Length; i++)
			{
				(string, string, string) tuple = all[i];
				if (!only || Options.Contains("only" + tuple.Item3))
				{
					yield return Part(tuple.Item3, One(stage, tuple.Item1, tuple.Item2, tuple.Item3));
				}
			}
			if (!Options.Contains("nofigure"))
			{
				yield return Part("figure", Figure());
			}
		}

		private IEnumerator One(WorldTile stage, string id, string family, string name)
		{
			MeshModel meshModel = Models.Get(family);
			Check(name + ": its model is the new one", meshModel != null && meshModel.Info("kind") == "wonder" && Models.Get(family + "_m") != null && Models.Get(family + "_f") != null, (meshModel != null) ? ("kind " + meshModel.Info("kind")) : "none");
			CivilTest.ClearBox(stage.x - 14, stage.y - 8, stage.x + 14, stage.y + 22);
			Clear(stage, 30f);
			Building b = null;
			try
			{
				b = World.world.buildings.addBuilding(id, World.world.GetTile(stage.x, stage.y + 8), pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
			}
			catch (Exception ex)
			{
				Note(name + ": " + ex.Message);
			}
			if (!Check(name + ": set down", b != null))
			{
				yield break;
			}
			try
			{
				b.completeConstruction();
			}
			catch
			{
			}
			Vector2 at = b.current_position;
			float tall = meshModel?.Max.Z ?? 8f;
			Mod.DebugEyeSet = true;
			Mod.DebugEyeHeight = 1f;
			Mod.DebugEye = at + new Vector2(0f, (0f - tall) * 1.1f - 4f);
			Mod.SetYaw(MathF.PI / 2f);
			float waited = 0f;
			int seen = SceneCollector.WondersPosed;
			while (SceneCollector.WondersPosed == seen && waited < 5f)
			{
				yield return CivilTest.Wait(0.25f);
				waited += 0.25f;
			}
			Note($"{name}: first drawn after {waited:F2} s");
			int posed = SceneCollector.WondersPosed;
			yield return Look(at + new Vector2(0f, (0f - tall) * 1.1f - 4f), at, 1f, 10f, "wond_" + name + "_front.png");
			Mod.SceneForTest.DumpNear(at, 2f);
			Check(name + ": drawn alive", SceneCollector.WondersPosed > posed, SceneCollector.WondersPosed - posed + " poses");
			yield return Look(at + new Vector2(tall * 0.75f + 3f, (0f - tall) * 0.8f - 3f), at, 1.4f, 12f, "wond_" + name + "_side.png");
			yield return Look(at + new Vector2(0.6f, 0f - Mathf.Max(3f, tall * 0.35f)), at, 0.95f, 30f, "wond_" + name + "_close.png");
			yield return CivilTest.Wait(0.6f);
			Mod.SaveFrame("wond_" + name + "_close2.png");
			string wonderHeard = FirstPersonAudio.WonderHeard;
			Check(name + ": heard close by", wonderHeard?.StartsWith("wonder_", StringComparison.Ordinal) ?? false, $"{wonderHeard} at {FirstPersonAudio.WonderHeardVolume:F3}");
			List<float> times = new List<float>();
			for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime)
			{
				times.Add(Mod.FrameMs);
				yield return null;
			}
			times.Sort();
			Note($"{name}: mod frame {((times.Count > 0) ? times[times.Count / 2] : 0f):F1} ms close by (middle of {times.Count}), worst {((times.Count > 0) ? times[times.Count - 1] : 0f):F1} ms");
			if (b.component_waypoint != null)
			{
				seen = SceneCollector.WonderPulsesSeen;
				int played = FirstPersonAudio.WonderCallsPlayed;
				SceneCollector.EffectTrace = new List<string>();
				yield return Look(at + new Vector2(0f, (0f - tall) * 1.1f - 6f), at, 1.2f, 10f, null);
				b.component_waypoint.spawnMainEffect();
				yield return CivilTest.Wait(0.35f);
				Mod.SaveFrame("wond_" + name + "_call.png");
				yield return CivilTest.Wait(0.5f);
				bool flag = false;
				foreach (string item in SceneCollector.EffectTrace)
				{
					flag |= item.StartsWith("fx_waypoint", StringComparison.Ordinal);
				}
				SceneCollector.EffectTrace = null;
				Check(name + ": its call bursts in 3D, not as a flat picture", SceneCollector.WonderPulsesSeen > seen && !flag, $"calls {SceneCollector.WonderPulsesSeen - seen}, flat {flag}");
				Check(name + ": and is heard", FirstPersonAudio.WonderCallsPlayed > played, FirstPersonAudio.WonderCallsPlayed - played + " played");
			}
			Settings settings = Mod.Settings;
			int savedMode = settings.TimeMode;
			settings.TimeMode = 3;
			Mod.ForceRelight();
			yield return Look(at + new Vector2(1.5f, (0f - tall) * 1.1f - 4f), at, 1f, 10f, "wond_" + name + "_night.png");
			settings.TimeMode = savedMode;
			Mod.ForceRelight();
			yield return Look(at + new Vector2(-20f, -55f), at, 1.2f, 3f, "wond_" + name + "_far.png");
			Mod.DebugEyeSet = false;
			try
			{
				b.removeBuildingFinal();
			}
			catch
			{
			}
			Clear(stage, 30f);
			yield return CivilTest.Wait(0.5f);
		}

		private IEnumerator Figure()
		{
			try
			{
				_you.addToInventory("gold", 3);
				_you.addToInventory("gems", 2);
			}
			catch (Exception ex)
			{
				Note("bag: " + ex.Message);
			}
			yield return CivilTest.Wait(0.3f);
			List<string> order = BagOrder.Of(_you);
			int num = order.IndexOf("r:gold");
			Note(string.Format("bag order: {0}; gold in slot {1}", string.Join(" ", order), num));
			if (Check("the gold is on the hotbar", num >= 0 && num < Hotbar.Size(_you)))
			{
				Hotbar.Choose(_you, num, wheel: false);
				yield return CivilTest.Wait(0.4f);
				Mod.Craft.Open = true;
				yield return CivilTest.Wait(1.2f);
				Check("the figure holds the gold", People.FigureItem == "gold", People.FigureItem ?? "nothing");
				SaveFigure("wond_figure_gold.png");
				Mod.YouView.Turn = 0.9f;
				yield return CivilTest.Wait(0.5f);
				SaveFigure("wond_figure_gold_turned.png");
				Mod.YouView.Turn = 0f;
				int num2 = order.IndexOf("r:gems");
				if (num2 >= 0 && num2 < Hotbar.Size(_you))
				{
					Hotbar.Choose(_you, num2, wheel: false);
					yield return CivilTest.Wait(0.6f);
					Check("and the gems when you pick those", People.FigureItem == "gems", People.FigureItem ?? "nothing");
					SaveFigure("wond_figure_gems.png");
				}
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

		private void Clear(WorldTile stage, float reach)
		{
			Vector2 vector = new Vector2(stage.x, stage.y + 8);
			foreach (Actor item in new List<Actor>(World.world.units.getSimpleList()))
			{
				if (item != null && item != _you && item.isAlive() && !((item.current_position - vector).sqrMagnitude > reach * reach))
				{
					try
					{
						item.dieSimpleNone();
					}
					catch
					{
					}
				}
			}
		}

		private IEnumerator Look(Vector2 eye, Vector2 at, float height, float pitch, string frame)
		{
			Mod.DebugEyeSet = true;
			Mod.DebugEyeHeight = height;
			Mod.DebugEye = eye;
			Vector2 vector = at - eye;
			Mod.SetYaw(Mathf.Atan2(vector.y, vector.x));
			Mod.SetPitchDegrees(pitch);
			yield return CivilTest.Wait(1.3f);
			if (frame != null)
			{
				Mod.SaveFrame(frame);
			}
		}
	}
}
