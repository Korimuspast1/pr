using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class FxDump
	{
		private static readonly string[] Prefabs = new string[8] { "effects/prefabs/PrefabMeteorite", "effects/prefabs/PrefabBoulder", "effects/prefabs/PrefabBoulderImpact", "effects/prefabs/PrefabBoulderImpactWater", "effects/prefabs/PrefabFireballExplosion", "Prefabs/PrefabBoulderCharge", "effects/prefabs/PrefabExplosionSmall", "effects/prefabs/PrefabExplosionMeteorite" };

		public static IEnumerator Run(string dir)
		{
			StringBuilder sb = new StringBuilder();
			List<KeyValuePair<string, Sprite>> pending = new List<KeyValuePair<string, Sprite>>();
			HashSet<Sprite> queued = new HashSet<Sprite>();
			try
			{
				HeatRayEffect heat_ray_fx = World.world.heat_ray_fx;
				Line("heat_ray_fx " + ((heat_ray_fx != null) ? "found" : "null"));
				if (heat_ray_fx != null)
				{
					Line(string.Format("heatray lossy {0} parent {1}", heat_ray_fx.transform.lossyScale, (heat_ray_fx.transform.parent != null) ? heat_ray_fx.transform.parent.name : "-"));
					Tree("heatray", heat_ray_fx.transform, "  ");
				}
			}
			catch (Exception ex)
			{
				Line("heatray failed: " + ex.Message);
			}
			string[] array = new string[4] { "crab_bomb", "crab_bomb_shrapnel", "bomb", "napalm_bomb" };
			foreach (string text in array)
			{
				try
				{
					DropAsset dropAsset = AssetManager.drops.get(text);
					Sprite[] spriteList = SpriteTextureLoader.getSpriteList(dropAsset.path_texture);
					Line($"drop {text}: texture {dropAsset.path_texture} scale {dropAsset.default_scale} animated {dropAsset.animated} speed {dropAsset.animation_speed} rotation {dropAsset.animation_rotation} height {dropAsset.falling_height}");
					Frames("drop_" + text, spriteList);
				}
				catch (Exception ex2)
				{
					Line("drop " + text + " failed: " + ex2.Message);
				}
			}
			array = Prefabs;
			foreach (string text2 in array)
			{
				try
				{
					GameObject gameObject = Resources.Load<GameObject>(text2);
					if (gameObject == null)
					{
						Line(text2 + ": not found");
						continue;
					}
					Line("prefab " + text2);
					Tree(Path.GetFileName(text2).Replace("Prefab", "").ToLowerInvariant(), gameObject.transform, "  ");
				}
				catch (Exception ex3)
				{
					Line(text2 + " failed: " + ex3.Message);
				}
			}
			array = new string[6] { "fx_meteorite", "fx_boulder", "fx_boulder_impact", "fx_explosion_crab_bomb", "fx_explosion_small", "fx_explosion_meteorite" };
			foreach (string text3 in array)
			{
				try
				{
					EffectAsset effectAsset = AssetManager.effects_library.get(text3);
					Line($"effect {text3}: prefab {effectAsset.prefab_id} basic {effectAsset.use_basic_prefab} sprites {effectAsset.sprite_path} every {effectAsset.time_between_frames} light {effectAsset.draw_light_area} {effectAsset.draw_light_size}");
					if (!string.IsNullOrEmpty(effectAsset.sprite_path))
					{
						Frames(text3, SpriteTextureLoader.getSpriteList(effectAsset.sprite_path));
					}
				}
				catch (Exception ex4)
				{
					Line("effect " + text3 + " failed: " + ex4.Message);
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
			WorldTile tile = World.world.GetTile(MapBox.width / 2, MapBox.height / 2);
			try
			{
				Meteorite.spawnMeteorite(tile);
			}
			catch (Exception ex5)
			{
				Line("meteor spawn failed: " + ex5.Message);
			}
			try
			{
				Vector2 vector = (Boulder._initial_charge_position = tile.posV);
				Boulder boulder = EffectsLibrary.spawnAt("fx_boulder", vector, 1f) as Boulder;
				if (boulder != null)
				{
					boulder.spawnOn(vector);
				}
			}
			catch (Exception ex6)
			{
				Line("boulder spawn failed: " + ex6.Message);
			}
			for (int f = 0; f < 200; f++)
			{
				foreach (BaseEffectController item in World.world.stack_effects.list)
				{
					if (item == null || item._list == null)
					{
						continue;
					}
					foreach (BaseEffect item2 in item._list)
					{
						if (!(item2 == null) && item2.active)
						{
							if (item2 is Meteorite meteorite && f % 10 == 0)
							{
								Line(string.Format("meteor f{0}: root renderer {1} pos {2} offset {3} main {4} lossy {5}", f, (((BaseEffect)meteorite).sprite_renderer != null) ? "yes" : "null", meteorite.transform.position, meteorite.current_position, meteorite.mainSprite.transform.localPosition, meteorite.mainSprite.transform.lossyScale));
							}
							if (item2 is Boulder boulder2 && f % 5 == 0)
							{
								Line(string.Format("boulder f{0}: root renderer {1} pos {2} height {3} bounces {4} main {5} scale {6} lossy {7}", f, (((BaseEffect)boulder2).sprite_renderer != null) ? "yes" : "null", boulder2.current_position, boulder2.position_height, boulder2._bounces_left, boulder2.mainTransform.localPosition, boulder2.mainTransform.localScale, boulder2.mainTransform.lossyScale));
							}
						}
					}
				}
				yield return null;
			}
			File.WriteAllText(Path.Combine(dir, "fx_dump.txt"), sb.ToString());
			Line("DONE");
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
								pending.Add(new KeyValuePair<string, Sprite>($"fx_{tag}_{j}.png", sprite));
							}
						}
					}
				}
			}
			void Line(string s)
			{
				Log.Info("FXDUMP " + s);
				sb.AppendLine(s);
			}
			void Tree(string tag, Transform t, string indent)
			{
				SpriteRenderer component = t.GetComponent<SpriteRenderer>();
				SpriteAnimation component2 = t.GetComponent<SpriteAnimation>();
				Line($"{indent}{t.name} pos {t.localPosition} scale {t.localScale} rot {t.localEulerAngles} active {t.gameObject.activeSelf}" + ((component != null) ? string.Format(" sprite {0} color {1} order {2}", (component.sprite != null) ? component.sprite.name : "-", component.color, component.sortingOrder) : " (no renderer)") + ((component2 != null) ? $" anim {((component2.frames != null) ? component2.frames.Length : 0)} frames every {component2.timeBetweenFrames}" : ""));
				string tag2 = tag + "_" + t.name.Replace(' ', '_');
				if (component2 != null)
				{
					Frames(tag2, component2.frames);
				}
				else if (component != null && component.sprite != null)
				{
					Frames(tag2, new Sprite[1] { component.sprite });
				}
				foreach (Transform item3 in t)
				{
					Tree(tag, item3, indent + "  ");
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
	}
}
