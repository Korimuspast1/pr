using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace FirstPerson
{
	internal static class UiDump
	{
		private static readonly Dictionary<Texture, Color32[]> _pixels = new Dictionary<Texture, Color32[]>();

		public static IEnumerator Run(string root)
		{
			string dir = Path.Combine(root, "ui");
			Directory.CreateDirectory(dir);
			StringBuilder log = new StringBuilder();
			HashSet<Sprite> saved = new HashSet<Sprite>();
			List<string> list = new List<string>
			{
				"ui/Icons/iconHealth", "ui/Icons/iconHunger", "ui/Icons/iconHungry", "ui/Icons/iconStamina", "ui/Icons/iconMana", "ui/Icons/iconAge", "ui/Icons/iconClock", "ui/Icons/iconKills", "ui/Icons/iconSkulls", "ui/Icons/iconDead",
				"ui/Icons/iconExperience", "ui/Icons/iconLevel", "ui/Icons/iconLevels", "ui/Icons/iconNutrition", "ui/Icons/iconHappiness_1", "ui/Icons/iconHappiness_3", "ui/Icons/iconHappiness_6", "ui/Icons/iconCrown", "ui/Icons/iconKingdom", "ui/Icons/iconCity",
				"ui/Icons/iconArmor", "ui/Icons/iconDamage", "ui/Icons/iconSpeed", "ui/Icons/iconAttackSpeed", "ui/Icons/iconRenown", "ui/Icons/iconMoon", "ui/Icons/iconSun", "ui/Icons/iconNight", "ui/Icons/iconDay", "ui/Icons/iconPause",
				"ui/Icons/iconPlay", "ui/Icons/iconBag", "ui/Icons/iconLoot", "ui/Icons/iconInventory", "ui/Icons/iconHeart", "ui/Icons/iconLovers", "ui/Icons/iconResMeat", "ui/Icons/iconResFish", "ui/Icons/iconResBread", "ui/Icons/iconResBerries",
				"ui/Icons/iconResSausage", "ui/Icons/actor_traits/iconSunblessed", "ui/Icons/actor_traits/iconMoonchild", "ui/Icons/actor_traits/iconNightchild", "ui/Icons/iconAges", "ui/Icons/iconWorldInfo", "ui/Icons/iconFavoriteStar", "ui/Icons/iconFavorite", "ui/Icons/iconBuildings", "ui/Icons/iconQuestion",
				"ui/Icons/iconTalk", "ui/Icons/iconSpeech", "ui/Icons/iconCraft", "ui/Icons/iconWarfare"
			};
			string[] array = new string[11]
			{
				"Hope", "Sun", "Dark", "Tears", "Moon", "Chaos", "Wonders", "Ice", "Ash", "Despair",
				"Unknown"
			};
			foreach (string text in array)
			{
				list.Add("ui/Icons/ages/iconAge" + text);
			}
			try
			{
				foreach (BaseStatAsset item in AssetManager.base_stats_library.list)
				{
					if (!string.IsNullOrEmpty(item.icon))
					{
						log.AppendLine("stat\t" + item.id + "\ticon=" + item.icon);
						list.Add(item.icon.Contains("/") ? item.icon : ("ui/Icons/" + item.icon));
					}
				}
			}
			catch (Exception ex)
			{
				log.AppendLine("stats: " + ex.Message);
			}
			try
			{
				foreach (WorldAgeAsset item2 in AssetManager.era_library.list)
				{
					log.AppendLine($"age\t{item2.id}\ticon={item2.path_icon}\tbg={item2.path_background}\ttitle={item2.title_color}\tlight={item2.light_color}");
				}
			}
			catch (Exception ex2)
			{
				log.AppendLine("ages: " + ex2.Message);
			}
			foreach (string item3 in list)
			{
				Sprite sprite = null;
				try
				{
					sprite = Resources.Load<Sprite>(item3);
				}
				catch
				{
				}
				if (sprite == null)
				{
					try
					{
						sprite = SpriteTextureLoader.getSprite(item3);
					}
					catch
					{
					}
				}
				log.AppendLine("path\t" + item3 + "\t" + ((sprite == null) ? "MISSING" : Describe(sprite)));
				if (sprite != null && saved.Add(sprite))
				{
					Save(sprite, Path.Combine(dir, "p_" + Safe(item3.Substring(item3.LastIndexOf('/') + 1)) + ".png"), log);
				}
			}
			yield return null;
			Dictionary<string, int> dictionary = new Dictionary<string, int>();
			Text[] array2 = Resources.FindObjectsOfTypeAll<Text>();
			foreach (Text text2 in array2)
			{
				if (!(text2 == null) && !(text2.font == null))
				{
					string key = text2.font.name + " (" + text2.font.dynamic + ")";
					dictionary[key] = ((!dictionary.TryGetValue(key, out var value)) ? 1 : (value + 1));
				}
			}
			foreach (KeyValuePair<string, int> item4 in dictionary)
			{
				log.AppendLine($"font\t{item4.Key}\tused by {item4.Value} texts");
			}
			UiWorldAgeInfo uiWorldAgeInfo = null;
			UiWorldAgeInfo[] array3 = Resources.FindObjectsOfTypeAll<UiWorldAgeInfo>();
			foreach (UiWorldAgeInfo uiWorldAgeInfo2 in array3)
			{
				if (uiWorldAgeInfo2 != null && uiWorldAgeInfo2.gameObject.scene.IsValid())
				{
					uiWorldAgeInfo = uiWorldAgeInfo2;
					break;
				}
			}
			if (uiWorldAgeInfo != null)
			{
				Transform t = ((uiWorldAgeInfo.transform.parent != null && uiWorldAgeInfo.transform.parent.parent != null) ? uiWorldAgeInfo.transform.parent.parent : uiWorldAgeInfo.transform);
				log.AppendLine("== age panel, from " + PathOf(t));
				Tree(t, 0, 6, log, dir, saved);
			}
			else
			{
				log.AppendLine("no UiWorldAgeInfo in the scene");
			}
			PossessionUI[] array4 = Resources.FindObjectsOfTypeAll<PossessionUI>();
			foreach (PossessionUI possessionUI in array4)
			{
				if (possessionUI != null && possessionUI.gameObject.scene.IsValid())
				{
					log.AppendLine("== possession UI, from " + PathOf(possessionUI.transform));
					Tree(possessionUI.transform, 0, 9, log, dir, saved);
					break;
				}
			}
			yield return null;
			int count = 0;
			Image[] array5 = Resources.FindObjectsOfTypeAll<Image>();
			foreach (Image image in array5)
			{
				if (!(image == null) && !(image.sprite == null) && !saved.Contains(image.sprite))
				{
					saved.Add(image.sprite);
					RectTransform rectTransform = image.rectTransform;
					log.AppendLine($"image\t{PathOf(image.transform)}\t{Describe(image.sprite)}\ttype={image.type}\tsize={rectTransform.rect.width:F0}x{rectTransform.rect.height:F0}\tcolor={image.color}");
					Save(image.sprite, Path.Combine(dir, "i_" + Safe(image.sprite.name) + ".png"), log);
					int i = count + 1;
					count = i;
					if (i % 25 == 0)
					{
						yield return null;
					}
				}
			}
			GameObject[] array6 = new GameObject[0];
			try
			{
				array6 = Resources.LoadAll<GameObject>("windows");
			}
			catch (Exception ex3)
			{
				log.AppendLine("windows: " + ex3.Message);
			}
			log.AppendLine($"== {array6.Length} window prefabs");
			GameObject[] array7 = array6;
			foreach (GameObject gameObject in array7)
			{
				if (gameObject != null && (gameObject.name == "settings" || gameObject.name == "quit_game"))
				{
					log.AppendLine("== window " + gameObject.name);
					Tree(gameObject.transform, 0, 14, log, dir, saved);
				}
			}
			GameObject[] array8 = array6;
			foreach (GameObject w in array8)
			{
				if (w == null)
				{
					continue;
				}
				array5 = w.GetComponentsInChildren<Image>(includeInactive: true);
				foreach (Image image2 in array5)
				{
					if (!(image2 == null) && !(image2.sprite == null) && !saved.Contains(image2.sprite))
					{
						saved.Add(image2.sprite);
						RectTransform rectTransform2 = image2.rectTransform;
						log.AppendLine($"window\t{w.name}/{PathOf(image2.transform, w.transform)}\t{Describe(image2.sprite)}\ttype={image2.type}\tsize={rectTransform2.rect.width:F0}x{rectTransform2.rect.height:F0}\tcolor={image2.color}");
						Save(image2.sprite, Path.Combine(dir, "w_" + Safe(image2.sprite.name) + ".png"), log);
						int i = count + 1;
						count = i;
						if (i % 25 == 0)
						{
							yield return null;
						}
					}
				}
			}
			File.WriteAllText(Path.Combine(root, "ui.tsv"), log.ToString());
			string[] array9 = new string[2] { "settings", "quit_game" };
			foreach (string id in array9)
			{
				try
				{
					ScrollWindow.showWindow(id);
				}
				catch (Exception ex4)
				{
					Log.Info("UIDUMP window " + id + ": " + ex4.Message);
					continue;
				}
				yield return new WaitForSecondsRealtime(1.2f);
				ScreenCapture.CaptureScreenshot(Path.Combine(root, "game_window_" + id + ".png"));
				yield return null;
				yield return null;
				try
				{
					ScrollWindow.hideAllEvent(pWithAnimation: false);
				}
				catch
				{
				}
				yield return new WaitForSecondsRealtime(0.6f);
			}
			Log.Info($"UIDUMP saved {saved.Count} sprites to {dir} DONE");
		}

		private static void Tree(Transform t, int depth, int max, StringBuilder log, string dir, HashSet<Sprite> saved)
		{
			RectTransform rectTransform = t as RectTransform;
			StringBuilder stringBuilder = new StringBuilder(new string(' ', depth * 2) + t.name + (t.gameObject.activeSelf ? "" : " (off)"));
			if (rectTransform != null)
			{
				stringBuilder.Append($"  rect={rectTransform.rect.width:F0}x{rectTransform.rect.height:F0} pos={rectTransform.anchoredPosition}");
			}
			Image component = t.GetComponent<Image>();
			if (component != null && component.sprite != null)
			{
				stringBuilder.Append($"  img={Describe(component.sprite)} type={component.type} color={component.color}");
				if (saved.Add(component.sprite))
				{
					Save(component.sprite, Path.Combine(dir, "a_" + Safe(component.sprite.name) + ".png"), log);
				}
			}
			Text component2 = t.GetComponent<Text>();
			if (component2 != null)
			{
				stringBuilder.Append($"  text=\"{component2.text}\" font={component2.font?.name} size={component2.fontSize} style={component2.fontStyle} color={component2.color} align={component2.alignment}");
			}
			Component[] components = t.GetComponents<Component>();
			foreach (Component component3 in components)
			{
				if (!(component3 == null))
				{
					if (component3 is Shadow shadow)
					{
						stringBuilder.Append($"  {component3.GetType().Name}({shadow.effectColor},{shadow.effectDistance})");
					}
					else if (component3 is Slider slider)
					{
						stringBuilder.Append("  Slider(fill=" + ((slider.fillRect != null) ? slider.fillRect.name : "-") + ", handle=" + ((slider.handleRect != null) ? slider.handleRect.name : "-") + ")");
					}
					else if (component3 is Selectable selectable)
					{
						stringBuilder.Append(string.Format("  {0}(transition={1}, normal={2}, hover={3}, press={4}, swap={5}/{6})", component3.GetType().Name, selectable.transition, selectable.colors.normalColor, selectable.colors.highlightedColor, selectable.colors.pressedColor, (selectable.spriteState.highlightedSprite != null) ? selectable.spriteState.highlightedSprite.name : "-", (selectable.spriteState.pressedSprite != null) ? selectable.spriteState.pressedSprite.name : "-"));
					}
					else if (component3 is LayoutGroup layoutGroup)
					{
						stringBuilder.Append(string.Format("  {0}(pad={1}, spacing={2})", component3.GetType().Name, layoutGroup.padding, (layoutGroup is HorizontalOrVerticalLayoutGroup horizontalOrVerticalLayoutGroup) ? horizontalOrVerticalLayoutGroup.spacing.ToString() : ((layoutGroup is GridLayoutGroup gridLayoutGroup) ? (gridLayoutGroup.cellSize.ToString() + "/" + gridLayoutGroup.spacing.ToString()) : "")));
					}
					else if (!(component3 is Transform) && !(component3 is CanvasRenderer) && !(component3 is Image) && !(component3 is Text))
					{
						stringBuilder.Append("  +" + component3.GetType().Name);
					}
				}
			}
			log.AppendLine(stringBuilder.ToString());
			if (depth < max)
			{
				for (int j = 0; j < t.childCount; j++)
				{
					Tree(t.GetChild(j), depth + 1, max, log, dir, saved);
				}
			}
		}

		private static string Describe(Sprite s)
		{
			Rect rect;
			try
			{
				rect = s.textureRect;
			}
			catch
			{
				rect = s.rect;
			}
			return $"{s.name} tex={s.texture?.name} {rect.width:F0}x{rect.height:F0} border={s.border} ppu={s.pixelsPerUnit} filter={s.texture?.filterMode}";
		}

		private static string PathOf(Transform t, Transform stop = null)
		{
			string text = t.name;
			Transform parent = t.parent;
			while (parent != null && parent != stop)
			{
				text = parent.name + "/" + text;
				parent = parent.parent;
			}
			return text;
		}

		private static string Safe(string s)
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (char c in s)
			{
				stringBuilder.Append((char.IsLetterOrDigit(c) || c == '_' || c == '-') ? c : '_');
			}
			return stringBuilder.ToString();
		}

		private static void Save(Sprite s, string file, StringBuilder log)
		{
			try
			{
				Texture2D texture = s.texture;
				if (texture == null)
				{
					return;
				}
				if (!_pixels.TryGetValue(texture, out var value))
				{
					RenderTexture temporary = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, (!texture.isDataSRGB) ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
					RenderTexture active = RenderTexture.active;
					Graphics.Blit(texture, temporary);
					RenderTexture.active = temporary;
					Texture2D texture2D = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, mipChain: false);
					texture2D.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0, recalculateMipMaps: false);
					RenderTexture.active = active;
					RenderTexture.ReleaseTemporary(temporary);
					value = texture2D.GetPixels32();
					UnityEngine.Object.Destroy(texture2D);
					_pixels[texture] = value;
				}
				Rect rect;
				try
				{
					rect = s.textureRect;
				}
				catch
				{
					rect = s.rect;
				}
				int num = Mathf.RoundToInt(rect.x);
				int num2 = Mathf.RoundToInt(rect.y);
				int num3 = Mathf.RoundToInt(rect.width);
				int num4 = Mathf.RoundToInt(rect.height);
				if (num3 <= 0 || num4 <= 0)
				{
					return;
				}
				int num5 = Mathf.Clamp(160 / Mathf.Max(num3, num4), 1, 8);
				Texture2D texture2D2 = new Texture2D(num3 * num5, num4 * num5, TextureFormat.RGBA32, mipChain: false);
				Color32[] array = new Color32[num3 * num5 * num4 * num5];
				for (int i = 0; i < num4 * num5; i++)
				{
					for (int j = 0; j < num3 * num5; j++)
					{
						int num6 = num + j / num5;
						int num7 = num2 + i / num5;
						array[i * num3 * num5 + j] = ((num6 < texture.width && num7 < texture.height) ? value[num7 * texture.width + num6] : default(Color32));
					}
				}
				texture2D2.SetPixels32(array);
				texture2D2.Apply(updateMipmaps: false);
				File.WriteAllBytes(file, texture2D2.EncodeToPNG());
				UnityEngine.Object.Destroy(texture2D2);
			}
			catch (Exception ex)
			{
				log.AppendLine("save " + s.name + ": " + ex.Message);
			}
		}
	}
}
