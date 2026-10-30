using System;
using UnityEngine;

namespace FirstPerson
{
	internal static class Skin
	{
		public static Texture2D RollTex;

		public static Texture2D RollEndTex;

		public static Texture2D FinialTex;

		public static Texture2D DeckleTex;

		public static Texture2D AgeTex;

		public static Texture2D InkRingTex;

		public static Texture2D InkLineTex;

		public static Texture2D InkDotTex;

		public static Texture2D SealTex;

		public static Texture2D RibbonTex;

		public static Texture2D InkBoxTex;

		public static Texture2D SoftShadowTex;

		public static Texture2D FadeTex;

		private static GUIStyle _inkBox;

		private static GUIStyle _softShadow;

		private static int _scrollFor = -1;

		public static float U = 1f;

		private static int _builtFor = -1;

		private static float _builtScale = 1f;

		public static Texture2D WoodTile;

		public static Texture2D PaperTile;

		public static Texture2D WoodFrame;

		public static Texture2D PaperFrame;

		public static Texture2D CardFrame;

		public static Texture2D InsetFrame;

		public static Texture2D KeyCapTex;

		public static Texture2D DiamondTex;

		public static Texture2D MedallionTex;

		public static Texture2D RingTex;

		public static Texture2D DiscTex;

		public static Texture2D BeadOn;

		public static Texture2D BeadOff;

		public static Texture2D ButtonTex;

		public static Texture2D ButtonHover;

		public static Texture2D ButtonDown;

		public static Texture2D ButtonOff;

		public static Texture2D TabOn;

		public static Texture2D Gloss;

		public static Texture2D White;

		public static Texture2D ShieldTex;

		private static GUIStyle _woodFrame;

		private static GUIStyle _paperFrame;

		private static GUIStyle _cardFrame;

		private static GUIStyle _insetFrame;

		private static GUIStyle _keyCap;

		public static GUIStyle ScrollTrack;

		public static GUIStyle ScrollThumb;

		public static Font Serif;

		private static int _frameBorder;

		private static int _paperBorder;

		private static int _cardBorder;

		private static int _insetBorder;

		private static int _keyBorder;

		public static readonly Color Cream = new Color32(242, 228, 196, byte.MaxValue);

		public static readonly Color CreamDim = new Color32(214, 196, 160, byte.MaxValue);

		public static readonly Color Gold = new Color32(232, 196, 120, byte.MaxValue);

		public static readonly Color Ink = new Color32(70, 48, 32, byte.MaxValue);

		public static readonly Color InkSoft = new Color32(112, 86, 60, byte.MaxValue);

		public static readonly Color InkRed = new Color32(150, 38, 28, byte.MaxValue);

		public static readonly Color InkGreen = new Color32(52, 104, 40, byte.MaxValue);

		public static readonly Color InkBlue = new Color32(40, 76, 128, byte.MaxValue);

		public static readonly Color InkGold = new Color32(146, 100, 34, byte.MaxValue);

		private static GUIStyle _savedBar;

		private static GUIStyle _savedThumb;

		private static bool Repaint => Event.current.type == EventType.Repaint;

		public static void EnsureScroll()
		{
			Ensure();
			if (_scrollFor != _builtFor || !(RollTex != null))
			{
				_scrollFor = _builtFor;
				BuildScroll();
			}
		}

		private static Color Shade(Color c, float k)
		{
			return new Color(c.r * k, c.g * k, c.b * k, c.a);
		}

		private static void BuildScroll()
		{
			float s = U;
			Color paperRoll = Rgb(212, 184, 134);
			Color outline = Rgb(66, 44, 28);
			int rh = Mathf.CeilToInt(38f * s);
			RollTex = Make(4, rh, delegate(float x, float y)
			{
				float num = y / (float)rh;
				float num2 = num * 2f - 1f;
				float num3 = (1f - Mathf.Abs(num2)) * (float)rh * 0.5f;
				float num4 = Mathf.Sqrt(Mathf.Max(0f, 1f - num2 * num2));
				float k = 0.42f + 0.52f * num4 - 0.12f * num2 + 0.2f * Mathf.Exp(0f - Mathf.Pow((num - 0.3f) / 0.07f, 2f));
				Color result = ((num3 < 1.3f * s) ? outline : Shade(paperRoll, k));
				result.a = Mathf.Clamp01(num3 + 0.5f);
				return result;
			});
			int ew = Mathf.CeilToInt(12f * s);
			RollEndTex = Make(ew, rh, delegate(float x, float y)
			{
				float num = (x - (float)ew * 0.5f) / ((float)ew * 0.5f);
				float num2 = (y - (float)rh * 0.5f) / ((float)rh * 0.5f);
				float num3 = Mathf.Sqrt(num * num + num2 * num2);
				float num4 = Mathf.Clamp01((1f - num3) * (float)rh * 0.5f + 0.5f);
				if (num4 <= 0f)
				{
					return Color.clear;
				}
				Color result;
				if (num3 > 1f - 2.4f / (float)rh * 2f)
				{
					result = outline;
				}
				else if (num3 < 0.3f)
				{
					result = Rgb(58, 36, 20);
				}
				else
				{
					float num5 = 0.5f + 0.5f * Mathf.Cos((num3 * 8f + Mathf.Atan2(num2, num) / (MathF.PI * 2f)) * 2f * MathF.PI);
					result = Shade(Rgb(196, 166, 116), 0.72f + 0.2f * num5 - 0.12f * num2);
				}
				result.a = num4;
				return result;
			});
			int fw = Mathf.CeilToInt(30f * s);
			int fh = Mathf.CeilToInt(32f * s);
			FinialTex = Make(fw, fh, delegate(float x, float y)
			{
				float num = (float)fh * 0.5f;
				float num2 = Mathf.Min(0.31f * (float)fw, 0.46f * (float)fh);
				float num3 = (float)fw - num2 - 1f;
				float num4 = 0.3f * (float)fw;
				float num5 = 0.44f * (float)fw;
				float num6 = 0.2f * (float)fh;
				float num7 = 0.36f * (float)fh;
				float a = BoxDistance(x, y, num4 * 0.5f - 2f, num, num4 * 0.5f + 2f, num6);
				float num8 = BoxDistance(x, y, (num4 + num5) * 0.5f, num, (num5 - num4) * 0.5f, num7) - 1.5f * s;
				float num9 = x - num3;
				float num10 = y - num;
				float num11 = Mathf.Sqrt(num9 * num9 + num10 * num10) - num2;
				float num12 = Mathf.Min(a, Mathf.Min(num8, num11));
				float num13 = Mathf.Clamp01(0.5f - num12);
				if (num13 <= 0f)
				{
					return Color.clear;
				}
				Color result;
				if (0f - num12 < 1.2f * s)
				{
					result = Rgb(30, 18, 10);
				}
				else if (num12 == num11)
				{
					float num14 = num9 / num2;
					float num15 = num10 / num2;
					float num16 = Mathf.Sqrt(Mathf.Max(0f, 1f - num14 * num14 - num15 * num15));
					float num17 = Mathf.Max(0f, -0.45f * num14 - 0.6f * num15 + 0.66f * num16);
					result = Shade(Rgb(104, 64, 34), 0.38f + 0.75f * num17);
					result = Color.Lerp(result, Color.white, 0.35f * Mathf.Pow(num17, 18f));
				}
				else if (num12 == num8)
				{
					result = Brass(Mathf.Abs(y - num) / num7, 1f - y / (float)fh);
				}
				else
				{
					float num18 = (y - num) / num6;
					result = Shade(Rgb(96, 60, 32), 0.45f + 0.5f * Mathf.Sqrt(Mathf.Max(0f, 1f - num18 * num18)) - 0.15f * num18);
				}
				result.a = num13;
				return result;
			});
			int dw = Mathf.CeilToInt(26f * s);
			int dh = Mathf.CeilToInt(256f * s);
			DeckleTex = Make(dw, dh, delegate(float x, float y)
			{
				float num = Noise(y / (float)dh * 10f, 0.5f, 10);
				float num2 = Noise(y / (float)dh * 40f, 2.5f, 40);
				float num3 = 2.5f * s + 6f * s * num + 2f * s * num2;
				float num4 = x - num3;
				float num5 = Mathf.Clamp01(num4 + 0.5f);
				if (num5 <= 0f)
				{
					return Color.clear;
				}
				float num6 = Mathf.Clamp01(1f - num4 / (15f * s));
				Color result = Color.Lerp(Shade(Rgb(234, 217, 178), 0.96f + 0.05f * Noise(x / (float)dw * 6f, y / (float)dh * 40f, 40)), Rgb(140, 92, 46), num6 * num6 * 0.6f);
				if (num4 < 1.4f * s)
				{
					result = Rgb(112, 72, 36);
				}
				if (x > (float)dw * 0.5f)
				{
					num5 *= Mathf.Clamp01(((float)dw - x) / ((float)dw * 0.5f));
				}
				result.a = num5;
				return result;
			}, repeat: true);
			AgeTex = Make(64, 64, delegate(float x, float y)
			{
				float f = x / 32f - 1f;
				float f2 = y / 32f - 1f;
				float num = Mathf.Clamp01(Mathf.Pow(Mathf.Abs(f), 4f) + Mathf.Pow(Mathf.Abs(f2), 4f));
				float num2 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Noise(x * 0.11f, y * 0.11f, 7) - 0.6f) / 0.2f));
				return new Color(0.47f, 0.31f, 0.13f, num * 0.3f + num2 * 0.1f);
			});
			InkRingTex = Make(160, 64, delegate(float x, float y)
			{
				float num = 74f;
				float num2 = 27f;
				float num3 = (x - 80f) / num;
				float num4 = (y - 32f) / num2;
				float num5 = Mathf.Sqrt(num3 * num3 + num4 * num4);
				float num6 = Mathf.Atan2(num4, num3);
				float num7 = Mathf.Lerp(num2, num, Mathf.Abs(Mathf.Cos(num6)));
				float a = 0f;
				float num8;
				for (num8 = num6 - 3.4f; num8 < 0f; num8 += MathF.PI * 2f)
				{
				}
				int num9 = 0;
				while (num9 < 2 && !(num8 > 7.0371675f))
				{
					float num10 = 0.965f + 0.07f * num8 / (MathF.PI * 2f);
					float f = Mathf.Clamp01(Mathf.Min(num8 / 0.6f, (7.0371675f - num8) / 0.6f));
					float num11 = (2.2f + 1.6f * Mathf.Sin(num8 * 1.7f + 0.5f)) * Mathf.Sqrt(f);
					a = Mathf.Max(a, Mathf.Clamp01(num11 * 0.5f - Mathf.Abs(num5 - num10) * num7 + 0.5f));
					num9++;
					num8 += MathF.PI * 2f;
				}
				return new Color(1f, 1f, 1f, a);
			});
			InkLineTex = Make(256, 16, delegate(float x, float y)
			{
				float num = Mathf.Sqrt(Mathf.Sin(MathF.PI * x / 256f));
				float num2 = 5.2f * num * (0.85f + 0.15f * Mathf.Sin(x * 0.07f));
				float num3 = 0.7f * Mathf.Sin(x * 0.031f + 1f);
				return new Color(1f, 1f, 1f, Mathf.Clamp01(num2 * 0.5f - Mathf.Abs(y - 8f - num3) + 0.5f));
			});
			InkDotTex = Make(24, 24, (float x, float y) => new Color(1f, 1f, 1f, Mathf.Clamp01(9.5f - (Mathf.Abs(x - 12f) + Mathf.Abs(y - 12f)) + 0.5f)));
			int ss = Mathf.CeilToInt(96f * s);
			SealTex = Make(ss, ss, delegate(float x, float y)
			{
				float num = x - (float)ss * 0.5f;
				float num2 = y - (float)ss * 0.5f;
				float num3 = Mathf.Sqrt(num * num + num2 * num2);
				float num4 = Mathf.Atan2(num2, num);
				float num5 = (float)ss * 0.44f * (1f + 0.035f * Mathf.Sin(num4 * 5f + 1f) + 0.025f * Mathf.Sin(num4 * 9f + 2f) + 0.02f * Mathf.Sin(num4 * 14f));
				float num6 = Mathf.Clamp01(num5 - num3 + 0.5f);
				if (num6 <= 0f)
				{
					return Color.clear;
				}
				float num7 = ((num3 > 0.001f) ? ((0f - (num + num2)) / (num3 * 1.414f)) : 0f);
				float num9;
				if (num3 > 0.7f * num5)
				{
					float num8 = Mathf.Cos(MathF.PI * (num3 - 0.7f * num5) / (0.3f * num5));
					num9 = 1f + 0.38f * num8 * num7 - 0.25f * Mathf.Clamp01((num3 - 0.9f * num5) / (0.1f * num5));
				}
				else if (num3 > 0.6f * num5)
				{
					num9 = 0.68f - 0.15f * num7;
				}
				else
				{
					num9 = 0.92f + 0.12f * ((0f - (num + num2)) / (num5 * 1.414f));
					if (Mathf.Abs(num3 - 0.38f * num5) < 0.9f * s)
					{
						num9 += 0.12f;
					}
				}
				Color a = Shade(Rgb(150, 30, 24), num9);
				float num10 = num + 0.32f * num5;
				float num11 = num2 + 0.42f * num5;
				a = Color.Lerp(a, Rgb(255, 190, 170), 0.3f * Mathf.Clamp01(1f - Mathf.Sqrt(num10 * num10 + num11 * num11) / (0.16f * num5)));
				if (num5 - num3 < 1.2f * s)
				{
					a = Rgb(80, 12, 8);
				}
				a.a = num6;
				return a;
			});
			int w = Mathf.CeilToInt(150f * s);
			int bh = Mathf.CeilToInt(44f * s);
			RibbonTex = Make(w, bh, delegate(float x, float y)
			{
				float num = (float)bh * 0.5f;
				float num2 = 0.32f * (float)bh * (1f - Mathf.Abs(y - num) / num);
				float num3 = x - num2;
				float num4 = Mathf.Clamp01(num3 + 0.5f) * Mathf.Clamp01(y + 0.5f) * Mathf.Clamp01((float)bh - y + 0.5f);
				if (num4 <= 0f)
				{
					return Color.clear;
				}
				float num5 = 0.95f - 0.16f * y / (float)bh + 0.05f * (Noise(x * 0.7f, y * 0.7f, 97) - 0.5f);
				if (y < 3f * s || y > (float)bh - 3f * s)
				{
					num5 *= 0.8f;
				}
				if ((Mathf.Abs(y - 5f * s) < 0.6f * s || Mathf.Abs(y - ((float)bh - 5f * s)) < 0.6f * s) && Mathf.Repeat(x, 6f * s) < 3.5f * s)
				{
					num5 *= 1.2f;
				}
				if (num3 < 1.5f * s)
				{
					num5 *= 0.62f;
				}
				return new Color(num5, num5, num5, num4);
			});
			int ib = Mathf.CeilToInt(40f * s);
			int border = Mathf.CeilToInt(12f * s);
			InkBoxTex = Make(ib, ib, delegate(float x, float y)
			{
				float num = 0f - RoundRect(x, y, ib, ib, 5f * s);
				float a = 0f;
				if (num > -0.5f && num < 2f * s)
				{
					a = Mathf.Clamp01(num + 0.5f) * Mathf.Clamp01(2f * s - num + 0.5f) * 0.9f;
				}
				else if (num > 3.6f * s && num < 4.6f * s)
				{
					a = 0.5f;
				}
				return new Color(1f, 1f, 1f, a);
			});
			_inkBox = Nine(InkBoxTex, border);
			SoftShadowTex = Make(64, 64, delegate(float x, float y)
			{
				float num = 0f - RoundRect(x, y, 64f, 64f, 30f);
				return new Color(0f, 0f, 0f, Mathf.Pow(Mathf.Clamp01(num / 26f), 1.5f) * 0.55f);
			});
			_softShadow = Nine(SoftShadowTex, 30);
			FadeTex = Make(2, 32, (float x, float y) => new Color(0.3f, 0.18f, 0.08f, Mathf.Pow(1f - y / 32f, 2f) * 0.5f));
		}

		private static float BoxDistance(float x, float y, float cx, float cy, float hx, float hy)
		{
			float a = Mathf.Abs(x - cx) - hx;
			float num = Mathf.Abs(y - cy) - hy;
			float num2 = Mathf.Max(a, 0f);
			float num3 = Mathf.Max(num, 0f);
			return Mathf.Sqrt(num2 * num2 + num3 * num3) + Mathf.Min(Mathf.Max(a, num), 0f);
		}

		private static void Draw(Rect r, Texture2D tex, Color c)
		{
			Color color = GUI.color;
			GUI.color = c;
			GUI.DrawTexture(r, tex);
			GUI.color = color;
		}

		public static void ScrollPaper(Rect band, Rect full)
		{
			if (Repaint && !(band.height <= 0f))
			{
				float num = DeckleTex.width;
				float num2 = DeckleTex.height;
				Tile(new Rect(band.x + num * 0.5f, band.y, band.width - num, band.height), PaperTile);
				float num3 = (band.y - full.y) / full.height;
				float num4 = (band.yMax - full.y) / full.height;
				GUI.DrawTextureWithTexCoords(band, AgeTex, new Rect(0f, 1f - num4, 1f, num4 - num3));
				Rect texCoords = new Rect(0f, (0f - band.y) / num2, 1f, band.height / num2);
				GUI.DrawTextureWithTexCoords(new Rect(band.x, band.y, num, band.height), DeckleTex, texCoords);
				GUI.DrawTextureWithTexCoords(new Rect(band.xMax - num, band.y, num, band.height), DeckleTex, new Rect(1f, texCoords.y + 0.37f, -1f, texCoords.height));
			}
		}

		public static void Roll(Rect r)
		{
			if (Repaint)
			{
				float num = (float)RollEndTex.width * r.height / (float)RollEndTex.height;
				float num2 = (float)FinialTex.width * r.height / (float)RollTex.height;
				float num3 = (float)FinialTex.height * r.height / (float)RollTex.height;
				float y = r.center.y - num3 * 0.5f;
				GUI.DrawTextureWithTexCoords(new Rect(r.x - num2 + num * 0.3f, y, num2, num3), FinialTex, new Rect(1f, 0f, -1f, 1f));
				GUI.DrawTexture(new Rect(r.xMax - num * 0.3f, y, num2, num3), FinialTex);
				GUI.DrawTexture(new Rect(r.x + num * 0.5f, r.y, r.width - num, r.height), RollTex);
				GUI.DrawTexture(new Rect(r.x, r.y, num, r.height), RollEndTex);
				GUI.DrawTexture(new Rect(r.xMax - num, r.y, num, r.height), RollEndTex);
			}
		}

		public static void RollShade(Rect r, bool below)
		{
			if (Repaint && !(r.height <= 0f))
			{
				GUI.DrawTextureWithTexCoords(r, FadeTex, below ? new Rect(0f, 0f, 1f, 1f) : new Rect(0f, 1f, 1f, -1f));
			}
		}

		public static void InkLine(Rect r, Color c)
		{
			if (Repaint)
			{
				Draw(r, InkLineTex, c);
			}
		}

		public static void InkRing(Rect r, Color c)
		{
			if (Repaint)
			{
				Draw(r, InkRingTex, c);
			}
		}

		public static void InkDot(Vector2 at, float size, Color c)
		{
			if (Repaint)
			{
				Draw(new Rect(at.x - size * 0.5f, at.y - size * 0.5f, size, size), InkDotTex, c);
			}
		}

		public static void Seal(Rect r, float alpha = 1f)
		{
			if (Repaint)
			{
				Draw(r, SealTex, new Color(1f, 1f, 1f, alpha));
			}
		}

		public static void Ribbon(Rect r, Color c)
		{
			if (Repaint)
			{
				Draw(r, RibbonTex, c);
			}
		}

		public static void InkBox(Rect r, Color c)
		{
			if (Repaint)
			{
				Color color = GUI.color;
				GUI.color = c;
				_inkBox.Draw(r, isHover: false, isActive: false, on: false, hasKeyboardFocus: false);
				GUI.color = color;
			}
		}

		public static void SoftShadow(Rect r, float alpha)
		{
			if (Repaint)
			{
				Color color = GUI.color;
				GUI.color = new Color(1f, 1f, 1f, alpha);
				_softShadow.Draw(r, isHover: false, isActive: false, on: false, hasKeyboardFocus: false);
				GUI.color = color;
			}
		}

		public static void Ensure()
		{
			int height = Screen.height;
			float hudScale = Device.HudScale;
			if (height != _builtFor || hudScale != _builtScale || !(White != null))
			{
				_builtFor = height;
				_builtScale = hudScale;
				U = Mathf.Max(0.6f, (float)height / 1080f) * hudScale;
				Build();
			}
		}

		private static Texture2D Make(int w, int h, Func<float, float, Color> pixel, bool repeat = false)
		{
			Texture2D texture2D = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = ((!repeat) ? TextureWrapMode.Clamp : TextureWrapMode.Repeat),
				hideFlags = HideFlags.HideAndDontSave
			};
			Color32[] array = new Color32[w * h];
			for (int i = 0; i < h; i++)
			{
				for (int j = 0; j < w; j++)
				{
					array[(h - 1 - i) * w + j] = pixel((float)j + 0.5f, (float)i + 0.5f);
				}
			}
			texture2D.SetPixels32(array);
			texture2D.Apply(updateMipmaps: false);
			return texture2D;
		}

		private static float RoundRect(float x, float y, float w, float h, float r)
		{
			float a = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r);
			float num = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
			float num2 = Mathf.Max(a, 0f);
			float num3 = Mathf.Max(num, 0f);
			return Mathf.Sqrt(num2 * num2 + num3 * num3) + Mathf.Min(Mathf.Max(a, num), 0f) - r;
		}

		private static float Hash(int x, int y)
		{
			int num = x * 374761393 + y * 668265263;
			uint num2 = ((uint)num ^ ((uint)num >> 13)) * 1274126177;
			return (float)((num2 ^ (num2 >> 16)) & 0xFFFFFF) / 16777216f;
		}

		private static float Noise(float x, float y, int period)
		{
			int num = Mathf.FloorToInt(x);
			int num2 = Mathf.FloorToInt(y);
			float num3 = x - (float)num;
			float num4 = y - (float)num2;
			num3 = num3 * num3 * (3f - 2f * num3);
			num4 = num4 * num4 * (3f - 2f * num4);
			int num5 = (num % period + period) % period;
			int num6 = (num2 % period + period) % period;
			int x2 = (num5 + 1) % period;
			int y2 = (num6 + 1) % period;
			float a = Hash(num5, num6);
			float b = Hash(x2, num6);
			float a2 = Hash(num5, y2);
			float b2 = Hash(x2, y2);
			return Mathf.Lerp(Mathf.Lerp(a, b, num3), Mathf.Lerp(a2, b2, num3), num4);
		}

		private static Color Rgb(int r, int g, int b, float a = 1f)
		{
			return new Color((float)r / 255f, (float)g / 255f, (float)b / 255f, a);
		}

		private static Color Brass(float t, float lit)
		{
			float value = 1f - Mathf.Abs(t - 0.35f) / 0.65f;
			Color color = Color.Lerp(Rgb(120, 84, 40), Rgb(246, 214, 140), Mathf.Clamp01(value));
			return Color.Lerp(color * 0.8f, Color.Lerp(color, Color.white, 0.15f), lit);
		}

		private static void Build()
		{
			float s = U;
			White = Make(2, 2, (float x, float y) => Color.white);
			WoodTile = Make(128, 128, delegate(float x, float y)
			{
				float num2 = Noise(x * 0.06f, y * 0.55f, 8) * 0.65f + Noise(x * 0.2f, y * 1.6f, 26) * 0.35f;
				float num3 = ((Mathf.FloorToInt(y / 32f) % 2 == 0) ? 1f : 0.93f);
				float num4 = ((Mathf.Repeat(y, 32f) < 1.2f) ? 0.72f : 1f);
				float num5 = (0.8f + 0.32f * num2) * num3 * num4;
				return new Color(0.245f * num5, 0.172f * num5, 0.118f * num5, 1f);
			}, repeat: true);
			PaperTile = Make(128, 128, delegate(float x, float y)
			{
				float num2 = Noise(x * 0.04f, y * 0.04f, 5) * 0.6f + Noise(x * 0.12f, y * 0.12f, 15) * 0.4f;
				float num3 = Noise(x * 0.5f, y * 0.08f, 64) * 0.5f + 0.5f;
				float num4 = 0.93f + 0.07f * num2 + 0.02f * num3;
				return new Color(0.918f * num4, 0.851f * num4, 0.7f * num4, 1f);
			}, repeat: true);
			_frameBorder = Mathf.CeilToInt(16f * s);
			int fw = _frameBorder * 2 + 4;
			float fr = 7f * s;
			WoodFrame = Make(fw, fw, delegate(float x, float y)
			{
				float num2 = RoundRect(x, y, fw, fw, fr);
				float num3 = Mathf.Clamp01(0.5f - num2);
				if (num3 <= 0f)
				{
					return Color.clear;
				}
				float num4 = 0f - num2;
				float lit = 1f - y / (float)fw;
				Color result;
				if (num4 < 1.2f * s)
				{
					result = Rgb(26, 17, 11);
				}
				else if (num4 < 4.2f * s)
				{
					result = Brass((num4 - 1.2f * s) / (3f * s), lit);
				}
				else
				{
					if (!(num4 < 5.4f * s))
					{
						float num5 = Mathf.Clamp01(1f - (num4 - 5.4f * s) / (9f * s));
						return new Color(0.05f, 0.03f, 0.02f, 0.55f * num5 * num5);
					}
					result = Rgb(30, 20, 13);
				}
				result.a = num3;
				return result;
			});
			_woodFrame = Nine(WoodFrame, _frameBorder);
			_paperBorder = Mathf.CeilToInt(12f * s);
			int pw = _paperBorder * 2 + 4;
			float pr = 5f * s;
			PaperFrame = Make(pw, pw, delegate(float x, float y)
			{
				float num2 = RoundRect(x, y, pw, pw, pr);
				float num3 = Mathf.Clamp01(0.5f - num2);
				if (num3 <= 0f)
				{
					return Color.clear;
				}
				float num4 = 0f - num2;
				Color result;
				if (num4 < 1.4f * s)
				{
					result = Rgb(96, 66, 38);
				}
				else
				{
					if (!(num4 < 2.4f * s))
					{
						float num5 = Mathf.Clamp01(1f - (num4 - 2.4f * s) / (8f * s));
						return new Color(0.42f, 0.26f, 0.12f, 0.4f * num5 * num5);
					}
					result = Rgb(250, 238, 210, 0.9f);
				}
				result.a *= num3;
				return result;
			});
			_paperFrame = Nine(PaperFrame, _paperBorder);
			_cardBorder = Mathf.CeilToInt(9f * s);
			int cw = _cardBorder * 2 + 4;
			CardFrame = Make(cw, cw, delegate(float x, float y)
			{
				float num2 = RoundRect(x, y, cw, cw, 4f * s);
				float num3 = Mathf.Clamp01(0.5f - num2);
				if (num3 <= 0f)
				{
					return Color.clear;
				}
				float num4 = 0f - num2;
				Color result;
				if (num4 < 1.6f * s)
				{
					result = Rgb(110, 76, 44);
				}
				else
				{
					if (!(num4 < 2.6f * s))
					{
						float num5 = Mathf.Clamp01(1f - (num4 - 2.6f * s) / (6f * s));
						return new Color(0.45f, 0.3f, 0.15f, 0.28f * num5);
					}
					result = Rgb(196, 160, 106);
				}
				result.a = num3;
				return result;
			});
			_cardFrame = Nine(CardFrame, _cardBorder);
			_insetBorder = Mathf.CeilToInt(8f * s);
			int iw = _insetBorder * 2 + 4;
			InsetFrame = Make(iw, iw, delegate(float x, float y)
			{
				float num2 = RoundRect(x, y, iw, iw, 4f * s);
				float num3 = Mathf.Clamp01(0.5f - num2);
				if (num3 <= 0f)
				{
					return Color.clear;
				}
				float num4 = 0f - num2;
				float num5 = y / (float)iw;
				Color result;
				if (num4 < 1.2f * s)
				{
					result = Color.Lerp(Rgb(20, 13, 8), Rgb(150, 112, 62), num5 * num5);
				}
				else
				{
					float t = Mathf.Clamp01(1f - (num4 - 1.2f * s) / (5f * s)) * (1.2f - num5);
					result = Color.Lerp(Rgb(24, 16, 11), Rgb(8, 5, 3), t);
				}
				result.a = num3;
				return result;
			});
			_insetFrame = Nine(InsetFrame, _insetBorder);
			int num = Mathf.CeilToInt(10f * s) * 2 + 4;
			ButtonTex = MakeButton(num, Rgb(128, 90, 58), Rgb(80, 54, 34), pressed: false);
			ButtonHover = MakeButton(num, Rgb(156, 112, 72), Rgb(98, 68, 42), pressed: false);
			ButtonDown = MakeButton(num, Rgb(70, 48, 30), Rgb(104, 72, 46), pressed: true);
			ButtonOff = MakeButton(num, Rgb(96, 86, 76), Rgb(70, 62, 54), pressed: false);
			TabOn = MakeButton(num, Rgb(238, 220, 180), Rgb(210, 186, 140), pressed: false);
			_keyBorder = Mathf.CeilToInt(7f * s);
			int kw = _keyBorder * 2 + 4;
			KeyCapTex = Make(kw, kw, delegate(float x, float y)
			{
				float num2 = RoundRect(x, y, kw, kw, 4f * s);
				float num3 = Mathf.Clamp01(0.5f - num2);
				if (num3 <= 0f)
				{
					return Color.clear;
				}
				float num4 = 0f - num2;
				float num5 = y / (float)kw;
				Color result = ((num4 < 1f * s) ? Rgb(16, 10, 6) : ((!(num4 < 2.4f * s)) ? Color.Lerp(Rgb(64, 46, 32), Rgb(38, 26, 18), num5) : Brass((num4 - s) / (1.4f * s), 1f - num5)));
				if (num5 > 0.82f && num4 > 2.4f * s)
				{
					result = Rgb(26, 17, 11);
				}
				result.a = num3;
				return result;
			});
			_keyCap = Nine(KeyCapTex, _keyBorder);
			int dw = Mathf.CeilToInt(18f * s);
			DiamondTex = Make(dw, dw, delegate(float x, float y)
			{
				float num2 = x - (float)dw * 0.5f;
				float num3 = y - (float)dw * 0.5f;
				float num4 = (Mathf.Abs(num2) + Mathf.Abs(num3)) * 0.7071f - (float)dw * 0.34f;
				float num5 = Mathf.Clamp01(0.5f - num4);
				if (num5 <= 0f)
				{
					return Color.clear;
				}
				Color result = ((0f - num4 < 1.1f * s) ? Rgb(26, 17, 11) : Brass(0.2f + 0.6f * Mathf.Clamp01((num2 + num3) / (float)dw + 0.5f), (num3 < 0f) ? 1f : 0.3f));
				result.a = num5;
				return result;
			});
			int mw = Mathf.CeilToInt(128f * s);
			MedallionTex = Make(mw, mw, delegate(float x, float y)
			{
				float num2 = x - (float)mw * 0.5f;
				float num3 = y - (float)mw * 0.5f;
				float num4 = Mathf.Sqrt(num2 * num2 + num3 * num3);
				float num5 = (float)mw * 0.5f - 1f;
				float num6 = Mathf.Clamp01(num5 - num4 + 0.5f);
				if (num6 <= 0f)
				{
					return Color.clear;
				}
				float num7 = num5 - num4;
				float lit = 1f - y / (float)mw;
				Color result;
				if (num7 < 2f * s)
				{
					result = Rgb(24, 15, 9);
				}
				else if (num7 < 11f * s)
				{
					result = Brass((num7 - 2f * s) / (9f * s), lit);
				}
				else if (num7 < 13.5f * s)
				{
					result = Rgb(26, 17, 11);
				}
				else
				{
					float num8 = Mathf.Clamp01(num4 / (num5 - 13.5f * s));
					result = Color.Lerp(Rgb(62, 104, 96), Rgb(22, 40, 40), num8 * num8);
				}
				result.a = num6;
				return result;
			});
			RingTex = Make(mw, mw, delegate(float x, float y)
			{
				float num2 = x - (float)mw * 0.5f;
				float num3 = y - (float)mw * 0.5f;
				float num4 = Mathf.Sqrt(num2 * num2 + num3 * num3);
				float num5 = (float)mw * 0.5f - 1f;
				float num6 = Mathf.Clamp01(num5 - num4 + 0.5f);
				if (num6 <= 0f)
				{
					return Color.clear;
				}
				float num7 = num5 - num4;
				float lit = 1f - y / (float)mw;
				Color result;
				if (num7 < 2f * s)
				{
					result = Rgb(24, 15, 9);
				}
				else if (num7 < 11f * s)
				{
					result = Brass((num7 - 2f * s) / (9f * s), lit);
				}
				else
				{
					if (!(num7 < 13.5f * s))
					{
						float num8 = Mathf.Clamp01(1f - (num7 - 13.5f * s) / (10f * s));
						return new Color(0f, 0f, 0f, 0.45f * num8 * num8);
					}
					result = Rgb(26, 17, 11);
				}
				result.a = num6;
				return result;
			});
			DiscTex = Make(mw, mw, delegate(float x, float y)
			{
				float num2 = x - (float)mw * 0.5f;
				float num3 = y - (float)mw * 0.5f;
				float num4 = Mathf.Sqrt(num2 * num2 + num3 * num3);
				float num5 = (float)mw * 0.5f - 1f;
				return new Color(1f, 1f, 1f, Mathf.Clamp01(num5 - num4 + 0.5f));
			});
			int bead = Mathf.CeilToInt(12f * s);
			BeadOn = Make(bead, bead, (float x, float y) => Ball(x, y, bead, Rgb(250, 222, 140), Rgb(150, 104, 40)));
			BeadOff = Make(bead, bead, (float x, float y) => Ball(x, y, bead, Rgb(70, 52, 36), Rgb(30, 20, 13)));
			int sw = Mathf.CeilToInt(40f * s);
			ShieldTex = Make(sw, sw, delegate(float x, float y)
			{
				float f = x / (float)sw - 0.5f;
				float num2 = y / (float)sw;
				float num3 = ((num2 < 0.55f) ? 0.42f : (0.42f * Mathf.Sqrt(Mathf.Max(0f, 1f - (num2 - 0.55f) / 0.45f))));
				float num4 = Mathf.Max(Mathf.Abs(f) - num3, Mathf.Max(0.04f - num2, num2 - 0.98f)) * (float)sw;
				float num5 = Mathf.Clamp01(0.5f - num4);
				if (num5 <= 0f)
				{
					return Color.clear;
				}
				Color result = ((0f - num4 < 1.4f * s) ? Rgb(26, 17, 11) : ((0f - num4 < 3.2f * s) ? Brass(0.5f, 1f - num2) : (Color.white * (0.75f + 0.25f * (1f - num2)))));
				result.a = num5;
				return result;
			});
			Gloss = Make(2, 16, (float x, float y) => new Color(1f, 1f, 1f, Mathf.Clamp01(0.45f - y / 16f) * 0.9f));
			ScrollTrack = new GUIStyle
			{
				normal = 
				{
					background = InsetFrame
				},
				border = new RectOffset(_insetBorder, _insetBorder, _insetBorder, _insetBorder),
				fixedWidth = 0f
			};
			ScrollThumb = new GUIStyle
			{
				normal = 
				{
					background = ButtonTex
				},
				border = new RectOffset(num / 2 - 1, num / 2 - 1, num / 2 - 1, num / 2 - 1)
			};
			if (Serif == null)
			{
				try
				{
					Serif = Font.CreateDynamicFontFromOSFont(new string[6] { "Georgia", "Palatino Linotype", "Book Antiqua", "Constantia", "Cambria", "Times New Roman" }, 32);
				}
				catch (Exception ex)
				{
					Log.Error("HUD font: " + ex.Message);
				}
			}
		}

		private static Color Ball(float x, float y, int n, Color hi, Color lo)
		{
			float num = x - (float)n * 0.5f;
			float num2 = y - (float)n * 0.5f;
			float num3 = (float)n * 0.5f - 0.5f;
			float num4 = Mathf.Sqrt(num * num + num2 * num2);
			float num5 = Mathf.Clamp01(num3 - num4 + 0.5f);
			if (num5 <= 0f)
			{
				return Color.clear;
			}
			Color result = ((num3 - num4 < 1.1f * U) ? Rgb(20, 13, 8) : Color.Lerp(hi, lo, Mathf.Clamp01((num + num2) / (float)n + 0.55f)));
			result.a = num5;
			return result;
		}

		private static Texture2D MakeButton(int n, Color top, Color bottom, bool pressed)
		{
			float s = U;
			return Make(n, n, delegate(float x, float y)
			{
				float num = RoundRect(x, y, n, n, 4.5f * s);
				float num2 = Mathf.Clamp01(0.5f - num);
				if (num2 <= 0f)
				{
					return Color.clear;
				}
				float num3 = 0f - num;
				float num4 = y / (float)n;
				Color color;
				if (num3 < 1.1f * s)
				{
					color = Rgb(22, 14, 9);
				}
				else if (num3 < 2.6f * s)
				{
					color = Brass((num3 - 1.1f * s) / (1.5f * s), pressed ? num4 : (1f - num4));
				}
				else
				{
					color = Color.Lerp(top, bottom, num4);
					if (!pressed && num3 < 3.6f * s && num4 < 0.5f)
					{
						color = Color.Lerp(color, Color.white, 0.18f);
					}
				}
				color.a = num2;
				return color;
			});
		}

		private static GUIStyle Nine(Texture2D tex, int border)
		{
			return new GUIStyle
			{
				normal = 
				{
					background = tex
				},
				border = new RectOffset(border, border, border, border)
			};
		}

		public static GUIStyle Text(float size, bool bold = false, TextAnchor anchor = TextAnchor.UpperLeft, bool wrap = false)
		{
			return new GUIStyle(GUI.skin.label)
			{
				font = Serif,
				fontSize = Mathf.Max(9, Mathf.RoundToInt(size)),
				fontStyle = (bold ? FontStyle.Bold : FontStyle.Normal),
				alignment = anchor,
				wordWrap = wrap,
				clipping = TextClipping.Overflow,
				padding = new RectOffset(0, 0, 0, 0),
				margin = new RectOffset(0, 0, 0, 0)
			};
		}

		public static GUIStyle Button(float size)
		{
			int num = Mathf.CeilToInt(10f * U);
			GUIStyle obj = new GUIStyle(GUI.skin.button)
			{
				font = Serif,
				fontSize = Mathf.Max(9, Mathf.RoundToInt(size)),
				fontStyle = FontStyle.Bold,
				alignment = TextAnchor.MiddleCenter,
				border = new RectOffset(num, num, num, num),
				padding = new RectOffset(2, 2, 1, 1),
				clipping = TextClipping.Clip,
				wordWrap = false,
				normal = 
				{
					background = ButtonTex,
					textColor = Cream
				},
				hover = 
				{
					background = ButtonHover,
					textColor = Color.white
				},
				active = 
				{
					background = ButtonDown,
					textColor = Gold
				},
				focused = 
				{
					background = ButtonTex,
					textColor = Cream
				}
			};
			obj.onNormal = obj.normal;
			obj.onHover = obj.hover;
			obj.onActive = obj.active;
			return obj;
		}

		public static GUIStyle Tab(float size, bool on)
		{
			GUIStyle gUIStyle = Button(size);
			if (on)
			{
				gUIStyle.normal.background = TabOn;
				gUIStyle.normal.textColor = Ink;
				gUIStyle.hover.background = TabOn;
				gUIStyle.hover.textColor = Ink;
				gUIStyle.active.background = TabOn;
				gUIStyle.active.textColor = Ink;
			}
			return gUIStyle;
		}

		public static void Disabled(GUIStyle st)
		{
			st.normal.background = ButtonOff;
			st.normal.textColor = CreamDim;
		}

		private static void Tile(Rect r, Texture2D tile)
		{
			float num = 128f * U;
			GUI.DrawTextureWithTexCoords(r, tile, new Rect(r.x / num, (0f - r.y) / num, r.width / num, r.height / num));
		}

		public static void Wood(Rect r, float alpha = 1f)
		{
			if (Repaint)
			{
				Color color = GUI.color;
				GUI.color = new Color(1f, 1f, 1f, alpha);
				float num = 3f * U;
				Tile(new Rect(r.x + num, r.y + num, r.width - num * 2f, r.height - num * 2f), WoodTile);
				_woodFrame.Draw(r, isHover: false, isActive: false, on: false, hasKeyboardFocus: false);
				GUI.color = color;
			}
		}

		public static void Paper(Rect r, float alpha = 1f)
		{
			if (Repaint)
			{
				Color color = GUI.color;
				GUI.color = new Color(1f, 1f, 1f, alpha);
				float num = 1.5f * U;
				Tile(new Rect(r.x + num, r.y + num, r.width - num * 2f, r.height - num * 2f), PaperTile);
				_paperFrame.Draw(r, isHover: false, isActive: false, on: false, hasKeyboardFocus: false);
				GUI.color = color;
			}
		}

		public static void Card(Rect r, Color tint, float alpha = 1f)
		{
			if (Repaint)
			{
				Color color = GUI.color;
				GUI.color = new Color(tint.r, tint.g, tint.b, alpha);
				float num = 1.5f * U;
				Tile(new Rect(r.x + num, r.y + num, r.width - num * 2f, r.height - num * 2f), PaperTile);
				GUI.color = new Color(1f, 1f, 1f, alpha);
				_cardFrame.Draw(r, isHover: false, isActive: false, on: false, hasKeyboardFocus: false);
				GUI.color = color;
			}
		}

		public static void Inset(Rect r, float alpha = 1f)
		{
			if (Repaint)
			{
				Color color = GUI.color;
				GUI.color = new Color(1f, 1f, 1f, alpha);
				_insetFrame.Draw(r, isHover: false, isActive: false, on: false, hasKeyboardFocus: false);
				GUI.color = color;
			}
		}

		public static void Bar(Rect r, float fraction, Color color, float alpha = 1f)
		{
			if (Repaint)
			{
				Inset(r, alpha);
				float num = Mathf.Max(1f, 2f * U);
				Rect position = new Rect(r.x + num, r.y + num, (r.width - num * 2f) * Mathf.Clamp01(fraction), r.height - num * 2f);
				if (!(position.width <= 0.5f))
				{
					Color color2 = GUI.color;
					GUI.color = new Color(color.r, color.g, color.b, color.a * alpha);
					GUI.DrawTexture(position, White);
					GUI.color = new Color(1f, 1f, 1f, alpha);
					GUI.DrawTexture(position, Gloss);
					GUI.color = new Color(0f, 0f, 0f, 0.35f * alpha);
					GUI.DrawTexture(new Rect(position.xMax - Mathf.Max(1f, U), position.y, Mathf.Max(1f, U), position.height), White);
					GUI.color = color2;
				}
			}
		}

		public static void KeyCap(Rect r, float alpha = 1f)
		{
			if (Repaint)
			{
				Color color = GUI.color;
				GUI.color = new Color(1f, 1f, 1f, alpha);
				_keyCap.Draw(r, isHover: false, isActive: false, on: false, hasKeyboardFocus: false);
				GUI.color = color;
			}
		}

		public static void Diamond(Vector2 c, float size, float alpha = 1f)
		{
			if (Repaint)
			{
				Color color = GUI.color;
				GUI.color = new Color(1f, 1f, 1f, alpha);
				GUI.DrawTexture(new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size), DiamondTex);
				GUI.color = color;
			}
		}

		public static void Medallion(Vector2 c, float radius, float alpha = 1f)
		{
			if (Repaint)
			{
				Color color = GUI.color;
				GUI.color = new Color(1f, 1f, 1f, alpha);
				GUI.DrawTexture(new Rect(c.x - radius, c.y - radius, radius * 2f, radius * 2f), MedallionTex);
				GUI.color = color;
			}
		}

		public static void Globe(Vector2 c, float radius, Color sky, Color horizon, Vector2 body, Color bodyColor, float bodySize)
		{
			if (Repaint)
			{
				Color color = GUI.color;
				float num = radius * 0.8f;
				GUI.color = horizon;
				GUI.DrawTexture(new Rect(c.x - num, c.y - num, num * 2f, num * 2f), DiscTex);
				GUI.color = new Color(sky.r, sky.g, sky.b, 0.85f);
				GUI.DrawTexture(new Rect(c.x - num * 0.92f, c.y - num * 1.05f, num * 1.84f, num * 1.6f), DiscTex);
				GUI.color = bodyColor;
				GUI.DrawTexture(new Rect(body.x - bodySize, body.y - bodySize, bodySize * 2f, bodySize * 2f), DiscTex);
				GUI.color = new Color(0.16f, 0.26f, 0.14f, 1f);
				GUI.DrawTexture(new Rect(c.x - num * 0.75f, c.y + num * 0.38f, num * 1.5f, num * 0.7f), DiscTex);
				GUI.color = Color.white;
				GUI.DrawTexture(new Rect(c.x - radius, c.y - radius, radius * 2f, radius * 2f), RingTex);
				GUI.color = color;
			}
		}

		public static void Shield(Rect r, Color color)
		{
			if (Repaint)
			{
				Color color2 = GUI.color;
				GUI.color = color;
				GUI.DrawTexture(r, ShieldTex);
				GUI.color = color2;
			}
		}

		public static void Rule(Rect r, bool vertical)
		{
			if (Repaint)
			{
				Color color = GUI.color;
				GUI.color = new Color(0.1f, 0.06f, 0.04f, 0.9f);
				GUI.DrawTexture(r, White);
				GUI.color = Rgb(196, 156, 92);
				GUI.DrawTexture(vertical ? new Rect(r.x + r.width * 0.5f - 0.5f * U, r.y, Mathf.Max(1f, U), r.height) : new Rect(r.x, r.y + r.height * 0.5f - 0.5f * U, r.width, Mathf.Max(1f, U)), White);
				GUI.color = color;
			}
		}

		public static void TitleBar(Rect r, string title, GUIStyle style, float alpha = 1f)
		{
			Wood(r, alpha);
			if (Repaint)
			{
				float size = r.height * 0.55f;
				Diamond(new Vector2(r.x + r.height * 0.42f, r.center.y), size, alpha);
				Diamond(new Vector2(r.xMax - r.height * 0.42f, r.center.y), size, alpha);
				Shadowed(new Rect(r.x + r.height * 0.8f, r.y, r.width - r.height * 1.6f, r.height), title, style, new Color(Cream.r, Cream.g, Cream.b, alpha));
			}
		}

		public static void Shadowed(Rect r, string text, GUIStyle style, Color color)
		{
			if (!string.IsNullOrEmpty(text) && Repaint)
			{
				text = Lang.T(text);
				Font wasFont;
				FontStyle wasStyle;
				bool num = Lang.Fit(style, text, out wasFont, out wasStyle);
				float num2 = Mathf.Max(1f, 1.4f * U);
				style.normal.textColor = new Color(0.06f, 0.03f, 0.02f, 0.85f * color.a);
				GUI.Label(new Rect(r.x + num2, r.y + num2, r.width, r.height), text, style);
				style.normal.textColor = color;
				GUI.Label(r, text, style);
				if (num)
				{
					Lang.Restore(style, wasFont, wasStyle);
				}
			}
		}

		public static void Inked(Rect r, string text, GUIStyle style, Color color)
		{
			if (!string.IsNullOrEmpty(text) && Repaint)
			{
				style.normal.textColor = color;
				Lang.Label(r, text, style);
			}
		}

		public static Vector2 BeginScroll(Rect view, Vector2 scroll, Rect content)
		{
			_savedBar = GUI.skin.verticalScrollbar;
			_savedThumb = GUI.skin.verticalScrollbarThumb;
			float fixedWidth = Mathf.Max(8f, 12f * U);
			GUIStyle verticalScrollbar = new GUIStyle(ScrollTrack)
			{
				fixedWidth = fixedWidth,
				margin = new RectOffset(2, 0, 0, 0)
			};
			GUIStyle verticalScrollbarThumb = new GUIStyle(ScrollThumb)
			{
				fixedWidth = fixedWidth
			};
			GUI.skin.verticalScrollbar = verticalScrollbar;
			GUI.skin.verticalScrollbarThumb = verticalScrollbarThumb;
			return GUI.BeginScrollView(view, scroll, content, alwaysShowHorizontal: false, alwaysShowVertical: false, GUIStyle.none, verticalScrollbar);
		}

		public static void EndScroll()
		{
			GUI.EndScrollView();
			if (_savedBar != null)
			{
				GUI.skin.verticalScrollbar = _savedBar;
			}
			if (_savedThumb != null)
			{
				GUI.skin.verticalScrollbarThumb = _savedThumb;
			}
		}
	}
}
