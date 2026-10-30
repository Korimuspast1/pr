using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FirstPerson.Pad;
using FirstPerson.Vr;
using UnityEngine;

namespace FirstPerson
{
	internal static class GameArt
	{
		public static float P = 2f;

		public static Font Font;

		public static bool Ready;

		public static readonly Color Orange = new Color(1f, 0.607f, 0.11f);

		public static readonly Color KeyOrange = new Color32(243, 150, 31, byte.MaxValue);

		public static readonly Color Grey = new Color(0.678f, 0.678f, 0.678f);

		public static readonly Color Peach = new Color(1f, 0.737f, 0.4f);

		public static readonly Color AgeYellow = new Color(1f, 0.818f, 0.316f);

		public static readonly Color YearBlue = new Color(0f, 0.458f, 0.557f);

		public static readonly Color HealthGreen = new Color(0.137f, 0.613f, 0f);

		public static readonly Color Slate = new Color32(72, 78, 66, byte.MaxValue);

		public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.5f);

		public static Sprite BottomBar;

		public static Sprite Spectate;

		public static Sprite DarkField;

		public static Sprite Inner;

		public static Sprite Inner12;

		public static Sprite InnerSmall;

		public static Sprite Tooltip;

		public static Sprite ButtonBorder;

		public static Sprite Button;

		public static Sprite Button2;

		public static Sprite Tab;

		public static Sprite NamePlate;

		public static Sprite Well;

		public static Sprite CityElement;

		public static Sprite WindowBar;

		public static Sprite Heart;

		public static Sprite Meat;

		public static Sprite Hunger;

		public static Sprite Stamina;

		public static Sprite Mana;

		public static Sprite Age;

		public static Sprite Kills;

		public static Sprite Levels;

		public static Sprite Experience;

		public static Sprite Clock;

		public static Sprite Ages;

		public static Sprite Loot;

		public static Sprite Crown;

		public static Sprite Warfare;

		public static Sprite About;

		public static Sprite Male;

		public static Sprite Female;

		public static Sprite Skulls;

		public static Sprite Star;

		private static readonly string[] AtlasNames = new string[15]
		{
			"bottom_bar", "backgroundSpectateElement", "darkInputFieldEmpty", "windowInnerSliced", "windowInnerSliced12", "windowInnerSlicedSmall", "tooltip_rounded_4", "buttonBorder", "button", "button2",
			"tab_button", "windowNamePlate", "special_buttonGray_insides", "backgroundElementCity", "windowBar"
		};

		public static readonly Rect HeartPart = new Rect(6f, 5f, 17f, 16f);

		public static readonly Rect MeatPart = new Rect(8f, 9f, 13f, 11f);

		public static readonly Rect StaminaPart = new Rect(7f, 5f, 14f, 19f);

		public static readonly Rect ManaPart = new Rect(8f, 8f, 12f, 15f);

		public static readonly Rect XpPart = new Rect(8f, 10f, 13f, 7f);

		public static readonly Rect SunPart = new Rect(4f, 7f, 6f, 6f);

		public static readonly Rect MoonPart = new Rect(16f, 6f, 4f, 8f);

		private static float _nextCheck;

		private static int _tries;

		private static Canvas _canvas;

		private static RectTransform _bar;

		private static RectTransform _ageInfo;

		private static RectTransform _possessionPanel;

		private static bool _logged;

		public static Sprite WindowBig;

		public static Sprite CloseTab;

		public static Sprite SideTab;

		public static Sprite RedButton;

		public static Sprite GreyButton;

		public static Sprite SectionStrip;

		public static Sprite RoundMask;

		public static Sprite ScrollTrack;

		public static Sprite ScrollThumb;

		private static readonly string[] WindowNames = new string[9] { "windowBig", "close_background_0", "tab_button_vertical", "special_buttonRed", "special_buttonGray", "background_tab_content", "windowInnerSlicedRound", "ScrollBar_BG", "ScrollBar_Handle" };

		private static readonly Dictionary<string, Sprite> _named = new Dictionary<string, Sprite>();

		private static bool _windowTried;

		private static float _windowRetryAt;

		private static readonly float[] WindowYs = new float[4] { 0f, 96f, 210f, 265f };

		private static readonly bool[] WindowYStretch = new bool[3] { false, true, false };

		private static readonly float[][] WindowBandXs = new float[3][]
		{
			new float[6] { 0f, 34f, 35f, 178f, 179f, 216f },
			new float[4] { 0f, 6f, 210f, 216f },
			new float[4] { 0f, 60f, 141f, 216f }
		};

		private static readonly bool[][] WindowBandStretch = new bool[3][]
		{
			new bool[5] { false, true, false, true, false },
			new bool[3] { false, true, false },
			new bool[3] { false, true, false }
		};

		private static float _vrBoost = 1f;

		public static float GameP = 2f;

		private static float _userScale = 1f;

		private static readonly Vector3[] _corners = new Vector3[4];

		private static Material _silhouette;

		private static bool _silhouetteTried;

		private static readonly Dictionary<int, GUIStyle> _styles = new Dictionary<int, GUIStyle>();

		private static Font _stylesFont;

		private static readonly Regex Tags = new Regex("<[^>]+>");

		public static float VrBoost
		{
			get
			{
				return _vrBoost;
			}
			set
			{
				if (_vrBoost != value)
				{
					_vrBoost = value;
					P = Mathf.Clamp(GameP * UserScale * _vrBoost, 1f, 10f);
				}
			}
		}

		public static float UserScale
		{
			get
			{
				float userScale = 1f;
				try
				{
					userScale = Mathf.Clamp(WorldBoxMod.Instance?.Settings?.UiScale ?? 1f, 0.8f, 1.6f);
				}
				catch
				{
				}
				if (GUIUtility.hotControl == 0 || !Input.GetMouseButton(0))
				{
					_userScale = userScale;
				}
				return _userScale;
			}
		}

		private static bool Repaint
		{
			get
			{
				if (Event.current != null)
				{
					return Event.current.type == EventType.Repaint;
				}
				return true;
			}
		}

		public static void Ensure()
		{
			float unscaledTime = Time.unscaledTime;
			if (unscaledTime < _nextCheck)
			{
				return;
			}
			_nextCheck = unscaledTime + (Ready ? 1f : 1.5f);
			try
			{
				if (!Ready)
				{
					Load();
				}
				FollowScale();
			}
			catch (Exception ex)
			{
				if (_tries++ < 3)
				{
					Log.Error("hud art: " + ex.Message);
				}
			}
		}

		private static void Load()
		{
			if (!Config.game_loaded)
			{
				return;
			}
			_tries++;
			HashSet<string> hashSet = new HashSet<string>(AtlasNames);
			Dictionary<string, Sprite> found = new Dictionary<string, Sprite>();
			Sprite[] array = Resources.FindObjectsOfTypeAll<Sprite>();
			foreach (Sprite sprite in array)
			{
				if (!(sprite == null) && hashSet.Contains(sprite.name))
				{
					bool flag = sprite.texture != null && sprite.texture.name.StartsWith("sactx", StringComparison.Ordinal);
					if (!found.TryGetValue(sprite.name, out var value) || (flag && (!(value.texture != null) || !value.texture.name.StartsWith("sactx", StringComparison.Ordinal))))
					{
						found[sprite.name] = sprite;
					}
				}
			}
			BottomBar = Get("bottom_bar");
			Spectate = Get("backgroundSpectateElement");
			DarkField = Get("darkInputFieldEmpty");
			Inner = Get("windowInnerSliced");
			Inner12 = Get("windowInnerSliced12");
			InnerSmall = Get("windowInnerSlicedSmall");
			Tooltip = Get("tooltip_rounded_4");
			ButtonBorder = Get("buttonBorder");
			Button = Get("button");
			Button2 = Get("button2");
			Tab = Get("tab_button");
			NamePlate = Get("windowNamePlate");
			Well = Get("special_buttonGray_insides");
			CityElement = Get("backgroundElementCity");
			WindowBar = Get("windowBar");
			Heart = IconAt("iconHealth");
			Meat = IconAt("iconResMeat");
			Hunger = IconAt("iconHunger");
			Stamina = IconAt("iconStamina");
			Mana = IconAt("iconMana");
			Age = IconAt("iconAge");
			Kills = IconAt("iconKills");
			Levels = IconAt("iconLevels");
			Experience = IconAt("iconExperience");
			Clock = IconAt("iconClock");
			Ages = IconAt("iconAges");
			Loot = IconAt("iconLoot");
			Crown = IconAt("iconCrown");
			Warfare = IconAt("iconWarfare");
			About = IconAt("iconAbout");
			Male = IconAt("iconMale");
			Female = IconAt("iconFemale");
			Skulls = IconAt("iconSkulls");
			Star = IconAt("iconFavoriteStar");
			if (Font == null)
			{
				Font[] array2 = Resources.FindObjectsOfTypeAll<Font>();
				foreach (Font font in array2)
				{
					if (font != null && font.name == "Roboto-Bold")
					{
						Font = font;
						break;
					}
				}
			}
			Ready = (found.Count == AtlasNames.Length && Heart != null && Font != null) || _tries >= 6;
			if (Ready && !_logged)
			{
				_logged = true;
				FollowScale();
				Log.Info(string.Format("hud art: {0}/{1} panels, icons {2}, font {3}, game UI scale {4:F2}", found.Count, AtlasNames.Length, (Heart != null) ? "found" : "missing", (Font != null) ? Font.name : "none", P));
			}
			Sprite Get(string n)
			{
				if (!found.TryGetValue(n, out var value2))
				{
					return null;
				}
				return value2;
			}
		}

		private static Sprite IconAt(string name)
		{
			try
			{
				return Resources.Load<Sprite>("ui/Icons/" + name);
			}
			catch
			{
				return null;
			}
		}

		public static void EnsureWindowArt()
		{
			if ((WindowBig != null && RedButton != null) || (_windowTried && Time.unscaledTime < _windowRetryAt))
			{
				return;
			}
			_windowTried = true;
			_windowRetryAt = Time.unscaledTime + 5f;
			try
			{
				Resources.Load<GameObject>("windows/settings");
			}
			catch
			{
			}
			HashSet<string> hashSet = new HashSet<string>(WindowNames);
			Sprite[] array = Resources.FindObjectsOfTypeAll<Sprite>();
			foreach (Sprite sprite in array)
			{
				if (sprite == null || string.IsNullOrEmpty(sprite.name))
				{
					continue;
				}
				if (hashSet.Contains(sprite.name))
				{
					bool flag = sprite.texture != null && sprite.texture.name.StartsWith("sactx", StringComparison.Ordinal);
					if (!_named.TryGetValue(sprite.name, out var value) || (flag && (!(value.texture != null) || !value.texture.name.StartsWith("sactx", StringComparison.Ordinal))))
					{
						_named[sprite.name] = sprite;
					}
				}
				else if (!_named.ContainsKey(sprite.name) && (sprite.name.StartsWith("icon", StringComparison.Ordinal) || sprite.name.StartsWith("art_", StringComparison.Ordinal)))
				{
					_named[sprite.name] = sprite;
				}
			}
			WindowBig = Get("windowBig");
			CloseTab = Get("close_background_0");
			SideTab = Get("tab_button_vertical");
			RedButton = Get("special_buttonRed");
			GreyButton = Get("special_buttonGray");
			SectionStrip = Get("background_tab_content");
			RoundMask = Get("windowInnerSlicedRound");
			ScrollTrack = Get("ScrollBar_BG");
			ScrollThumb = Get("ScrollBar_Handle");
			int num = 0;
			string[] windowNames = WindowNames;
			for (int i = 0; i < windowNames.Length; i++)
			{
				if (Get(windowNames[i]) != null)
				{
					num++;
				}
			}
			Log.Info($"hud art: window {num}/{WindowNames.Length} pieces, {_named.Count} named sprites");
			Sprite Get(string n)
			{
				if (!_named.TryGetValue(n, out var value2))
				{
					return null;
				}
				return value2;
			}
		}

		public static Sprite IconNamed(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return null;
			}
			if (_named.TryGetValue(name, out var value) && value != null)
			{
				return value;
			}
			try
			{
				value = Resources.Load<Sprite>("ui/Icons/" + name);
			}
			catch
			{
				value = null;
			}
			if (value != null)
			{
				_named[name] = value;
			}
			return value;
		}

		public static Sprite Art(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return null;
			}
			if (_named.TryGetValue(name, out var value) && value != null)
			{
				return value;
			}
			try
			{
				value = Resources.Load<Sprite>("ui/illustrations/" + name);
			}
			catch
			{
				value = null;
			}
			if (value != null)
			{
				_named[name] = value;
			}
			return value;
		}

		public static void SlicedGrid(Rect r, Sprite s, float[] xs, bool[] xStretch, float[] ys, bool[] yStretch, Color tint, float scale = 0f)
		{
			if (!Repaint || r.width <= 0f || r.height <= 0f)
			{
				return;
			}
			if (s == null || s.texture == null)
			{
				Flat(r, new Color(0.2f, 0.21f, 0.18f, 0.95f * tint.a));
				return;
			}
			float k = ((scale > 0f) ? scale : P);
			Rect rect = TexRect(s);
			Texture texture = s.texture;
			float[] array = Spans(xs, xStretch, r.width, k);
			float[] array2 = Spans(ys, yStretch, r.height, k);
			Color color = GUI.color;
			GUI.color = tint * color;
			float num = r.y;
			for (int i = 0; i < array2.Length; i++)
			{
				float num2 = ((i == array2.Length - 1) ? r.yMax : Mathf.Round(num + array2[i]));
				float num3 = r.x;
				for (int j = 0; j < array.Length; j++)
				{
					float num4 = ((j == array.Length - 1) ? r.xMax : Mathf.Round(num3 + array[j]));
					if (num4 - num3 >= 0.5f && num2 - num >= 0.5f)
					{
						float num5 = rect.x + xs[j];
						float num6 = rect.x + xs[j + 1];
						float num7 = rect.yMax - ys[i + 1];
						float num8 = rect.yMax - ys[i];
						GUI.DrawTextureWithTexCoords(new Rect(num3, num, num4 - num3, num2 - num), texture, new Rect(num5 / (float)texture.width, num7 / (float)texture.height, (num6 - num5) / (float)texture.width, (num8 - num7) / (float)texture.height));
					}
					num3 = num4;
				}
				num = num2;
			}
			GUI.color = color;
		}

		private static float[] Spans(float[] cuts, bool[] stretch, float length, float k)
		{
			int num = cuts.Length - 1;
			float num2 = 0f;
			float num3 = 0f;
			for (int i = 0; i < num; i++)
			{
				float num4 = cuts[i + 1] - cuts[i];
				if (stretch[i])
				{
					num3 += num4;
				}
				else
				{
					num2 += num4;
				}
			}
			float num5 = Mathf.Min(1f, length / Mathf.Max(1f, num2 * k));
			float num6 = Mathf.Max(0f, length - num2 * k * num5);
			float[] array = new float[num];
			for (int j = 0; j < num; j++)
			{
				float num7 = cuts[j + 1] - cuts[j];
				array[j] = (stretch[j] ? ((num3 > 0f) ? (num6 * num7 / num3) : 0f) : (num7 * k * num5));
			}
			return array;
		}

		public static void Window(Rect r, Color tint)
		{
			if (WindowBig == null)
			{
				Sliced(r, Inner, tint);
			}
			else
			{
				if (!Repaint)
				{
					return;
				}
				float[] array = Spans(WindowYs, WindowYStretch, r.height, P);
				float num = r.y;
				for (int i = 0; i < 3; i++)
				{
					float num2 = ((i == 2) ? r.yMax : Mathf.Round(num + array[i]));
					if (num2 > num)
					{
						SlicedGrid(new Rect(r.x, num, r.width, num2 - num), WindowBig, WindowBandXs[i], WindowBandStretch[i], new float[2]
						{
							WindowYs[i],
							WindowYs[i + 1]
						}, new bool[1] { true }, tint);
					}
					num = num2;
				}
			}
		}

		public static void Cover(Rect r, Sprite s, Color tint, float panX = 0f, float panY = 0f)
		{
			if (Repaint && !(s == null) && !(s.texture == null) && !(r.width <= 0f) && !(r.height <= 0f))
			{
				Rect rect = TexRect(s);
				float num = r.width / r.height;
				float num2 = rect.width / rect.height;
				float num3 = rect.width;
				float num4 = rect.height;
				if (num2 > num)
				{
					num3 = num4 * num;
				}
				else
				{
					num4 = num3 / num;
				}
				float num5 = rect.x + (rect.width - num3) * 0.5f * (1f + Mathf.Clamp(panX, -1f, 1f));
				float num6 = rect.y + (rect.height - num4) * 0.5f * (1f + Mathf.Clamp(panY, -1f, 1f));
				Texture texture = s.texture;
				Color color = GUI.color;
				GUI.color = tint * color;
				GUI.DrawTextureWithTexCoords(r, texture, new Rect(num5 / (float)texture.width, num6 / (float)texture.height, num3 / (float)texture.width, num4 / (float)texture.height));
				GUI.color = color;
			}
		}

		public static void FlatButton(Rect r, bool red, string label, float size, float brightness = 1f, Sprite icon = null)
		{
			if (!Repaint)
			{
				return;
			}
			Color tint = new Color(brightness, brightness, brightness, 1f);
			if (red)
			{
				Sliced(r, RedButton, tint, 5f, 5f, 5f, 6f, Mathf.Min(P, r.height / 14f));
			}
			else
			{
				Sliced(r, GreyButton, tint, 3f, 3f, 3f, 3f, Mathf.Min(P, r.height / 14f));
			}
			float num = r.x;
			float num2 = r.width;
			if (icon != null && string.IsNullOrEmpty(label))
			{
				float num3 = Mathf.Round(Mathf.Min(r.width, r.height) * 0.9f);
				Rect r2 = new Rect(Mathf.Round(r.center.x - num3 * 0.5f), Mathf.Round(r.center.y - num3 * 0.5f), num3, num3);
				Icon(new Rect(r2.x + P * 0.5f, r2.y + P * 0.5f, num3, num3), icon, new Color(0f, 0f, 0f, 0.3f));
				Icon(r2, icon, Color.white);
				return;
			}
			if (icon != null)
			{
				float num4 = Mathf.Round(r.height * 0.95f);
				Icon(new Rect(r.x + r.height * 0.15f + P * 0.5f, r.center.y - num4 * 0.5f + P * 0.5f, num4, num4), icon, new Color(0f, 0f, 0f, 0.25f));
				Icon(new Rect(r.x + r.height * 0.15f, r.center.y - num4 * 0.5f, num4, num4), icon, Color.white);
				num += num4 * 0.8f;
				num2 -= num4 * 0.8f;
			}
			if (!string.IsNullOrEmpty(label))
			{
				Label(new Rect(num, r.y - P * 0.5f, num2, r.height), label, size, TextAnchor.MiddleCenter, (red || brightness > 0.99f) ? Color.white : new Color(0.92f, 0.92f, 0.9f));
			}
		}

		private static void FollowScale()
		{
			if (_canvas == null)
			{
				UiWorldAgeInfo[] array = Resources.FindObjectsOfTypeAll<UiWorldAgeInfo>();
				foreach (UiWorldAgeInfo uiWorldAgeInfo in array)
				{
					if (uiWorldAgeInfo == null || !uiWorldAgeInfo.gameObject.scene.IsValid())
					{
						continue;
					}
					_ageInfo = uiWorldAgeInfo.transform as RectTransform;
					Transform transform = uiWorldAgeInfo.transform;
					while (transform != null)
					{
						Canvas component = transform.GetComponent<Canvas>();
						if (component != null)
						{
							_canvas = component;
						}
						if (transform.name == "BottomElementsMover")
						{
							_bar = transform.Find("MainBarGraphics") as RectTransform;
						}
						transform = transform.parent;
					}
					break;
				}
			}
			if (_possessionPanel == null)
			{
				try
				{
					if (PossessionUI.instance != null)
					{
						_possessionPanel = PossessionUI.instance.transform.Find("Inner/Character Panel") as RectTransform;
					}
				}
				catch
				{
				}
			}
			float num = 0f;
			try
			{
				if (_canvas != null)
				{
					num = _canvas.rootCanvas.scaleFactor;
				}
			}
			catch
			{
			}
			GameP = ((num > 0.3f) ? Mathf.Clamp(num, 1f, 8f) : Mathf.Max(1f, (float)Screen.height / 400f));
			P = Mathf.Clamp(GameP * UserScale * _vrBoost, 1f, 10f);
		}

		public static bool GameBarRect(out Rect r)
		{
			return ScreenRect(_bar, out r);
		}

		public static bool GamePossessionRect(out Rect r)
		{
			return ScreenRect(_possessionPanel, out r);
		}

		public static bool GameAgeRect(out Rect r)
		{
			return ScreenRect(_ageInfo, out r);
		}

		private static bool ScreenRect(RectTransform rt, out Rect r)
		{
			r = default(Rect);
			try
			{
				if (rt == null || !rt.gameObject.activeInHierarchy)
				{
					return false;
				}
				rt.GetWorldCorners(_corners);
				Camera cam = ((_canvas != null && _canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _canvas.rootCanvas.worldCamera : null);
				Vector2 vector = RectTransformUtility.WorldToScreenPoint(cam, _corners[0]);
				Vector2 vector2 = RectTransformUtility.WorldToScreenPoint(cam, _corners[2]);
				r = Rect.MinMaxRect(vector.x, (float)Screen.height - vector2.y, vector2.x, (float)Screen.height - vector.y);
				return r.width > 4f && r.height > 4f && r.xMax > 0f && r.x < (float)Screen.width && r.yMax > 0f && r.y < (float)Screen.height;
			}
			catch
			{
				return false;
			}
		}

		private static Rect TexRect(Sprite s)
		{
			try
			{
				return s.textureRect;
			}
			catch
			{
				return s.rect;
			}
		}

		public static void Flat(Rect r, Color c)
		{
			if (Repaint)
			{
				Color color = GUI.color;
				GUI.color = c * color;
				GUI.DrawTexture(r, Skin.White ?? Texture2D.whiteTexture);
				GUI.color = color;
			}
		}

		public static void Sliced(Rect r, Sprite s, Color tint, float l = -1f, float rt = -1f, float t = -1f, float b = -1f, float scale = 0f)
		{
			if (!Repaint || r.width <= 0f || r.height <= 0f)
			{
				return;
			}
			if (s == null || s.texture == null)
			{
				Flat(r, new Color(0.2f, 0.21f, 0.18f, 0.92f * tint.a));
				return;
			}
			Vector4 border = s.border;
			float num = ((l >= 0f) ? l : border.x);
			float num2 = ((rt >= 0f) ? rt : border.z);
			float num3 = ((t >= 0f) ? t : border.w);
			float num4 = ((b >= 0f) ? b : border.y);
			float num5 = ((scale > 0f) ? scale : P);
			Rect rect = TexRect(s);
			Texture2D tex = s.texture;
			float num6 = num * num5;
			float num7 = num2 * num5;
			float num8 = num3 * num5;
			float num9 = num4 * num5;
			if (num6 + num7 > r.width)
			{
				float num10 = r.width / (num6 + num7);
				num6 *= num10;
				num7 *= num10;
			}
			if (num8 + num9 > r.height)
			{
				float num11 = r.height / (num8 + num9);
				num8 *= num11;
				num9 *= num11;
			}
			float ax = Mathf.Round(r.x);
			float num12 = Mathf.Round(r.x + num6);
			float num13 = Mathf.Round(r.xMax - num7);
			float bx = Mathf.Round(r.xMax);
			float ay = Mathf.Round(r.y);
			float num14 = Mathf.Round(r.y + num8);
			float num15 = Mathf.Round(r.yMax - num9);
			float num16 = Mathf.Round(r.yMax);
			float x = rect.x;
			float num17 = rect.x + num;
			float num18 = rect.xMax - num2;
			float xMax = rect.xMax;
			float yMax = rect.yMax;
			float num19 = rect.yMax - num3;
			float num20 = rect.y + num4;
			float y = rect.y;
			float num21 = num17;
			float num22 = num18;
			if (num22 - num21 < 0.5f)
			{
				num21 = num17 - 0.75f;
				num22 = num17 - 0.25f;
			}
			else
			{
				float num23 = (num13 - num12) / num5;
				if (num23 < num22 - num21)
				{
					num22 = num21 + Mathf.Max(0.5f, num23);
				}
			}
			float num24 = num20;
			float num25 = num19;
			if (num25 - num24 < 0.5f)
			{
				num24 = num19 + 0.25f;
				num25 = num19 + 0.75f;
			}
			else
			{
				float num26 = (num15 - num14) / num5;
				if (num26 < num25 - num24)
				{
					num24 = num25 - Mathf.Max(0.5f, num26);
				}
			}
			Color color = GUI.color;
			GUI.color = tint * color;
			float tw = tex.width;
			float th = tex.height;
			Cell(ax, ay, num12, num14, x, num17, num19, yMax);
			Cell(num12, ay, num13, num14, num21, num22, num19, yMax);
			Cell(num13, ay, bx, num14, num18, xMax, num19, yMax);
			Cell(ax, num14, num12, num15, x, num17, num24, num25);
			Cell(num12, num14, num13, num15, num21, num22, num24, num25);
			Cell(num13, num14, bx, num15, num18, xMax, num24, num25);
			Cell(ax, num15, num12, num16, x, num17, y, num20);
			Cell(num12, num15, num13, num16, num21, num22, y, num20);
			Cell(num13, num15, bx, num16, num18, xMax, y, num20);
			GUI.color = color;
			void Cell(float num28, float num29, float num27, float by, float u0, float u1, float v0, float v1)
			{
				if (!(num27 - num28 < 0.5f) && !(by - num29 < 0.5f) && !(u1 - u0 <= 0f) && !(v1 - v0 <= 0f))
				{
					GUI.DrawTextureWithTexCoords(new Rect(num28, num29, num27 - num28, by - num29), tex, new Rect(u0 / tw, v0 / th, (u1 - u0) / tw, (v1 - v0) / th));
				}
			}
		}

		public static void Icon(Rect r, Sprite s, Color tint)
		{
			if (Repaint && !(s == null) && !(s.texture == null))
			{
				Rect rect = TexRect(s);
				Texture texture = s.texture;
				Color color = GUI.color;
				GUI.color = tint * color;
				GUI.DrawTextureWithTexCoords(r, texture, new Rect(rect.x / (float)texture.width, rect.y / (float)texture.height, rect.width / (float)texture.width, rect.height / (float)texture.height));
				GUI.color = color;
			}
		}

		public static void Icon(Rect r, Sprite s)
		{
			Icon(r, s, Color.white);
		}

		public static void IconPart(Rect r, Sprite s, Rect part, Color tint)
		{
			if (Repaint && !(s == null) && !(s.texture == null))
			{
				Rect rect = TexRect(s);
				Texture texture = s.texture;
				float num = rect.x + part.x;
				float num2 = rect.yMax - part.yMax;
				Color color = GUI.color;
				GUI.color = tint * color;
				GUI.DrawTextureWithTexCoords(r, texture, new Rect(num / (float)texture.width, num2 / (float)texture.height, part.width / (float)texture.width, part.height / (float)texture.height));
				GUI.color = color;
			}
		}

		public static void Silhouette(Rect r, Sprite s, Rect part, Color c)
		{
			if (!Repaint || s == null || s.texture == null || c.a <= 0.005f)
			{
				return;
			}
			if (!_silhouetteTried)
			{
				_silhouetteTried = true;
				Shader shader = Shader.Find("GUI/Text Shader");
				if (shader != null)
				{
					_silhouette = new Material(shader)
					{
						name = "FirstPersonSilhouette",
						hideFlags = HideFlags.HideAndDontSave
					};
				}
				else
				{
					Log.Info("hud art: no text shader; hits don't flash white");
				}
			}
			if (!(_silhouette == null))
			{
				Rect rect = TexRect(s);
				Texture texture = s.texture;
				Rect sourceRect = new Rect((rect.x + part.x) / (float)texture.width, (rect.yMax - part.yMax) / (float)texture.height, part.width / (float)texture.width, part.height / (float)texture.height);
				_silhouette.mainTexture = texture;
				Graphics.DrawTexture(r, texture, sourceRect, 0, 0, 0, 0, c * GUI.color, _silhouette);
			}
		}

		public static void IconFit(Rect r, Sprite s, Color tint)
		{
			if (!(s == null))
			{
				Rect rect = TexRect(s);
				float num = rect.width / Mathf.Max(1f, rect.height);
				Icon((num >= 1f) ? new Rect(r.x, r.y + r.height * (1f - 1f / num) * 0.5f, r.width, r.height / num) : new Rect(r.x + r.width * (1f - num) * 0.5f, r.y, r.width * num, r.height), s, tint);
			}
		}

		public static GUIStyle Style(float size, TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = false)
		{
			int num = Mathf.Max(8, Mathf.RoundToInt(size * P));
			int key = num * 64 + (int)anchor * 2 + (wrap ? 1 : 0);
			Font font = Font ?? Skin.Serif;
			if (_stylesFont != font)
			{
				_styles.Clear();
				_stylesFont = font;
			}
			if (_styles.TryGetValue(key, out var value))
			{
				return value;
			}
			value = new GUIStyle(GUI.skin.label)
			{
				font = font,
				fontSize = num,
				fontStyle = ((!(Font != null)) ? FontStyle.Bold : FontStyle.Normal),
				alignment = anchor,
				wordWrap = wrap,
				richText = true,
				clipping = TextClipping.Overflow,
				padding = new RectOffset(0, 0, 0, 0),
				margin = new RectOffset(0, 0, 0, 0)
			};
			_styles[key] = value;
			return value;
		}

		public static Vector2 Measure(string text, float size, bool translate = true)
		{
			if (string.IsNullOrEmpty(text))
			{
				return Vector2.zero;
			}
			if (!translate)
			{
				return Lang.SizeAsIs(Style(size), text);
			}
			return Lang.Size(Style(size), text);
		}

		public static float MeasureHeight(string text, float size, float width, bool translate = true)
		{
			if (string.IsNullOrEmpty(text))
			{
				return 0f;
			}
			if (translate)
			{
				text = Lang.T(text);
			}
			GUIStyle gUIStyle = Style(size, TextAnchor.UpperLeft, wrap: true);
			Font wasFont;
			FontStyle wasStyle;
			bool flag = Lang.Fit(gUIStyle, text, out wasFont, out wasStyle);
			float result = gUIStyle.CalcHeight(new GUIContent(text), width);
			if (flag)
			{
				Lang.Restore(gUIStyle, wasFont, wasStyle);
			}
			return result;
		}

		public static void Label(Rect r, string text, float size, TextAnchor anchor, Color color, bool wrap = false, bool translate = true)
		{
			if (!string.IsNullOrEmpty(text) && Repaint && !(color.a <= 0.005f))
			{
				if (translate)
				{
					text = Lang.T(text);
				}
				GUIStyle gUIStyle = Style(size, anchor, wrap);
				Font wasFont;
				FontStyle wasStyle;
				bool num = Lang.Fit(gUIStyle, text, out wasFont, out wasStyle);
				float num2 = Mathf.Max(1f, Mathf.Round(P * 0.75f));
				Color color2 = GUI.color;
				gUIStyle.normal.textColor = new Color(0f, 0f, 0f, 0.5f * color.a);
				GUI.Label(new Rect(r.x + num2, r.y + num2, r.width, r.height), (text.IndexOf('<') >= 0) ? Tags.Replace(text, "") : text, gUIStyle);
				gUIStyle.normal.textColor = color;
				GUI.Label(r, text, gUIStyle);
				GUI.color = color2;
				if (num)
				{
					Lang.Restore(gUIStyle, wasFont, wasStyle);
				}
			}
		}

		public static void Bar(Rect r, float frac, Color fill, float trail = 0f, string text = null, float alpha = 1f, float textSize = 5f)
		{
			if (!Repaint)
			{
				return;
			}
			Sliced(r, DarkField, new Color(1f, 1f, 1f, 0.92f * alpha), 3f, 3f, 3f, 3f);
			float p = P;
			Rect rect = new Rect(Mathf.Round(r.x + p), Mathf.Round(r.y + p), 0f, 0f);
			rect.xMax = Mathf.Round(r.xMax - p);
			rect.yMax = Mathf.Round(r.yMax - p);
			frac = Mathf.Clamp01(frac);
			if (trail > frac + 0.001f)
			{
				Flat(new Rect(rect.x + rect.width * frac, rect.y, rect.width * (Mathf.Min(1f, trail) - frac), rect.height), new Color(1f, 0.88f, 0.72f, 0.5f * alpha));
			}
			if (frac > 0f)
			{
				Rect r2 = new Rect(rect.x, rect.y, Mathf.Max(1f, Mathf.Round(rect.width * frac)), rect.height);
				Flat(r2, new Color(fill.r, fill.g, fill.b, alpha));
				float num = Mathf.Max(1f, Mathf.Round(p * 0.75f));
				if (r2.height > num * 3f)
				{
					Flat(new Rect(r2.x, r2.y, r2.width, num), new Color(1f, 1f, 1f, 0.28f * alpha));
					Flat(new Rect(r2.x, r2.yMax - num, r2.width, num), new Color(0f, 0f, 0f, 0.22f * alpha));
				}
			}
			if (!string.IsNullOrEmpty(text))
			{
				Label(new Rect(r.x, r.y - p * 0.25f, r.width, r.height), text, textSize, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, alpha));
			}
		}

		public static void StoneButton(Rect r, Sprite icon, float alpha = 1f, bool gems = true, Color? iconTint = null)
		{
			if (Repaint)
			{
				Sliced(r, gems ? Button2 : Button, new Color(1f, 1f, 1f, alpha), 6f, 7f, 7f, 7f, r.width / 32f);
				if (!(icon == null))
				{
					float num = r.width * 0.06f;
					Color tint = iconTint ?? Color.white;
					tint.a *= alpha;
					Rect r2 = new Rect(r.x + num, r.y + num, r.width - num * 2f, r.height - num * 2f);
					Icon(new Rect(r2.x + P, r2.y + P, r2.width, r2.height), icon, new Color(0f, 0f, 0f, 0.5f * tint.a));
					Icon(r2, icon, tint);
				}
			}
		}

		public static string VrKey(string key)
		{
			if (string.IsNullOrEmpty(key))
			{
				return key;
			}
			if (!VrControls.Active)
			{
				if (!PadControls.Active || !PadControls.Shown)
				{
					return key;
				}
				return PadKey(key);
			}
			if (key != null)
			{
				switch (key.Length)
				{
				case 1:
					switch (key[0])
					{
					case 'E':
						return "Grip";
					case 'R':
						return "L grip";
					case 'X':
						break;
					case 'M':
						return "Y";
					case 'Z':
						return "R stick";
					case 'B':
					case 'C':
					case 'G':
					case 'N':
					case 'Q':
					case 'V':
						goto IL_0253;
					default:
						goto end_IL_0038;
					}
					goto IL_0223;
				case 6:
					switch (key[5])
					{
					case 'E':
						if (!(key == "Hold E"))
						{
							break;
						}
						return "Hold grip";
					case 'Q':
						if (!(key == "Hold Q"))
						{
							break;
						}
						return "Hold L stick";
					}
					break;
				case 3:
				{
					char c = key[0];
					if ((uint)c <= 76u)
					{
						if (c != 'E')
						{
							if (c != 'L' || !(key == "LMB"))
							{
								break;
							}
							return "Trigger";
						}
						if (!(key == "Esc"))
						{
							break;
						}
						goto IL_0223;
					}
					if (c != 'M')
					{
						switch (c)
						{
						case 'R':
							if (!(key == "RMB"))
							{
								break;
							}
							return "L trigger";
						case 'T':
							if (!(key == "Tab"))
							{
								break;
							}
							return "X";
						}
						break;
					}
					if (!(key == "MMB"))
					{
						break;
					}
					goto IL_0253;
				}
				case 5:
				{
					char c = key[1];
					if (c != 'h')
					{
						if (c != 'p' || !(key == "Space"))
						{
							break;
						}
						return "A";
					}
					if (!(key == "Shift"))
					{
						break;
					}
					goto IL_0247;
				}
				case 2:
					if (!(key == "F2"))
					{
						break;
					}
					return "Menu";
				case 4:
					{
						if (!(key == "WASD"))
						{
							break;
						}
						goto IL_0247;
					}
					IL_0223:
					return "B";
					IL_0253:
					return "A menu";
					IL_0247:
					return "L stick";
					end_IL_0038:
					break;
				}
			}
			return key;
		}

		public static string PadKey(string key)
		{
			PadControls.Pictures kind = PadControls.Kind;
			bool flag = kind == PadControls.Pictures.Deck;
			bool flag2 = kind == PadControls.Pictures.PlayStation;
			string result = (flag ? "L5" : (flag2 ? "Hold Options" : "Hold Menu"));
			if (key != null)
			{
				switch (key.Length)
				{
				case 1:
					switch (key[0])
					{
					case 'E':
						break;
					case 'X':
						goto IL_02f5;
					case 'C':
						return "R3";
					case 'M':
						return "D-pad up";
					case 'N':
						return "D-pad left";
					case 'B':
						return "D-pad right";
					case 'R':
						goto IL_0370;
					case 'V':
						goto IL_037b;
					case 'Z':
						goto IL_0386;
					case 'G':
					case 'Q':
					case '[':
					case ']':
						goto IL_0391;
					default:
						goto end_IL_0038;
					}
					if (!flag2)
					{
						return "Y";
					}
					return "Triangle";
				case 6:
					switch (key[5])
					{
					case 'E':
						if (!(key == "Hold E"))
						{
							break;
						}
						if (!flag2)
						{
							return "Hold Y";
						}
						return "Hold Triangle";
					case 'Q':
						if (!(key == "Hold Q"))
						{
							break;
						}
						return "D-pad down";
					}
					break;
				case 3:
				{
					char c = key[0];
					if ((uint)c <= 76u)
					{
						if (c != 'E')
						{
							if (c != 'L' || !(key == "LMB"))
							{
								break;
							}
							if (!flag2)
							{
								return "RT";
							}
							return "R2";
						}
						if (!(key == "Esc"))
						{
							break;
						}
						goto IL_0304;
					}
					if (c != 'M')
					{
						switch (c)
						{
						case 'R':
							if (!(key == "RMB"))
							{
								break;
							}
							if (!flag2)
							{
								return "LT";
							}
							return "L2";
						case 'T':
							if (!(key == "Tab"))
							{
								break;
							}
							if (!flag2)
							{
								return "View";
							}
							return "Create";
						}
						break;
					}
					if (!(key == "MMB"))
					{
						break;
					}
					goto IL_0391;
				}
				case 5:
				{
					char c = key[1];
					if (c != 'h')
					{
						if (c != 'n')
						{
							if (c != 'p' || !(key == "Space"))
							{
								break;
							}
						}
						else if (!(key == "Enter"))
						{
							break;
						}
						if (!flag2)
						{
							return "A";
						}
						return "Cross";
					}
					if (!(key == "Shift"))
					{
						break;
					}
					return "L3";
				}
				case 4:
					switch (key[0])
					{
					case 'W':
						if (!(key == "WASD"))
						{
							break;
						}
						return "L stick";
					case 'C':
						if (!(key == "Ctrl"))
						{
							break;
						}
						return "Back x2";
					}
					break;
				case 9:
					if (!(key == "Backspace"))
					{
						break;
					}
					goto IL_0304;
				case 2:
					{
						if (!(key == "F2"))
						{
							break;
						}
						if (!flag2)
						{
							return "Menu";
						}
						return "Options";
					}
					IL_0386:
					if (!flag)
					{
						return result;
					}
					return "R pad";
					IL_02f5:
					if (!flag2)
					{
						return "B";
					}
					return "Circle";
					IL_0304:
					if (!flag2)
					{
						return "B";
					}
					return "Circle";
					IL_0370:
					if (!flag)
					{
						return result;
					}
					return "R4";
					IL_037b:
					if (!flag)
					{
						return result;
					}
					return "R5";
					IL_0391:
					return result;
					end_IL_0038:
					break;
				}
			}
			return key;
		}

		public static string PadCap(string key)
		{
			if (!PadControls.Active || !PadControls.Shown || VrControls.Active)
			{
				return key;
			}
			string text = PadKey((key == "Esc") ? "F2" : key);
			if (text.StartsWith("D-pad "))
			{
				return "D-" + text.Substring(6);
			}
			if (text.StartsWith("Hold "))
			{
				return text.Substring(5) + "+";
			}
			return text;
		}

		public static float KeyCap(float x, float cy, string key, float size, float alpha = 1f)
		{
			Vector2 vector = Measure(key, size);
			float num = Mathf.Round((size + 5f) * P);
			float f = Mathf.Max(num, vector.x + 4f * P);
			Rect r = new Rect(Mathf.Round(x), Mathf.Round(cy - num * 0.5f), Mathf.Round(f), num);
			Sliced(r, Button, new Color(1f, 1f, 1f, alpha), 6f, 6f, 6f, 6f, num / 32f);
			Label(new Rect(r.x, r.y - P * 0.5f, r.width, r.height), key, size, TextAnchor.MiddleCenter, new Color(KeyOrange.r, KeyOrange.g, KeyOrange.b, alpha));
			return r.width;
		}

		public static void TipPanel(Rect r, float alpha = 1f)
		{
			Sliced(r, Tooltip, new Color(1f, 1f, 1f, 0.94f * alpha), 7f, 7f, 7f, 7f);
		}

		public static void StonePanel(Rect r, float alpha = 1f)
		{
			Sliced(r, Inner, new Color(1f, 1f, 1f, alpha));
		}

		public static void BarSlab(Rect r, float alpha = 1f)
		{
			Sliced(r, BottomBar, new Color(1f, 1f, 1f, alpha), 16f, 16f, 10f, 4f);
		}
	}
}
