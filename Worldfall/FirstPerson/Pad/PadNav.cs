using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FirstPerson.Pad
{
	internal static class PadNav
	{
		private struct SpotRect
		{
			public Rect R;

			public bool Tab;

			public bool Chosen;

			public Action<int> Step;
		}

		private static List<SpotRect> _drawing = new List<SpotRect>();

		private static List<SpotRect> _shown = new List<SpotRect>();

		private static int _drawingFrame = -1;

		public static bool VrOn;

		private static Func<Rect> _visible;

		private static bool _visibleLooked;

		public static bool Wanted
		{
			get
			{
				if ((PadControls.Shown || VrOn) && Event.current != null)
				{
					return Event.current.type == EventType.Repaint;
				}
				return false;
			}
		}

		private static List<SpotRect> Shown
		{
			get
			{
				if (Time.frameCount - _drawingFrame > 2)
				{
					_shown.Clear();
					_drawing.Clear();
				}
				if (Time.frameCount != _drawingFrame)
				{
					return _drawing;
				}
				return _shown;
			}
		}

		public static int Count => Shown.Count;

		public static void Spot(Rect gui, bool tab = false)
		{
			Add(gui, tab, chosen: false, null);
		}

		public static void Tab(Rect gui, bool chosen)
		{
			Add(gui, tab: true, chosen, null);
		}

		public static void Slider(Rect gui, Action<int> step)
		{
			Add(gui, tab: false, chosen: false, step);
		}

		private static void Add(Rect gui, bool tab, bool chosen, Action<int> step)
		{
			if ((!PadControls.Shown && !VrOn) || Event.current == null || Event.current.type != EventType.Repaint)
			{
				return;
			}
			Rect rect = Visible();
			gui = Rect.MinMaxRect(Mathf.Max(gui.xMin, rect.xMin), Mathf.Max(gui.yMin, rect.yMin), Mathf.Min(gui.xMax, rect.xMax), Mathf.Min(gui.yMax, rect.yMax));
			if (!(gui.width < 2f) && !(gui.height < 2f))
			{
				int frameCount = Time.frameCount;
				if (frameCount != _drawingFrame)
				{
					Flip();
					_drawingFrame = frameCount;
				}
				Vector2 vector = GUIUtility.GUIToScreenPoint(gui.min);
				Vector2 vector2 = GUIUtility.GUIToScreenPoint(gui.max);
				Rect r = Rect.MinMaxRect(Mathf.Min(vector.x, vector2.x), Mathf.Min(vector.y, vector2.y), Mathf.Max(vector.x, vector2.x), Mathf.Max(vector.y, vector2.y));
				if (!(r.xMax < 0f) && !(r.yMax < 0f) && !(r.x > (float)Screen.width) && !(r.y > (float)Screen.height))
				{
					_drawing.Add(new SpotRect
					{
						R = r,
						Tab = tab,
						Chosen = chosen,
						Step = step
					});
				}
			}
		}

		private static Rect Visible()
		{
			if (!_visibleLooked)
			{
				_visibleLooked = true;
				try
				{
					MethodInfo methodInfo = typeof(GUI).Assembly.GetType("UnityEngine.GUIClip")?.GetProperty("visibleRect", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetGetMethod(nonPublic: true);
					if (methodInfo != null)
					{
						_visible = (Func<Rect>)Delegate.CreateDelegate(typeof(Func<Rect>), methodInfo);
					}
				}
				catch (Exception ex)
				{
					Log.Error("pad: GUIClip.visibleRect: " + ex.Message);
				}
			}
			try
			{
				if (_visible != null)
				{
					return _visible();
				}
			}
			catch
			{
				_visible = null;
			}
			return new Rect(-1000000f, -1000000f, 2000000f, 2000000f);
		}

		public static bool StepSlider(Vector2 at, int dir)
		{
			List<SpotRect> shown = Shown;
			for (int num = shown.Count - 1; num >= 0; num--)
			{
				if (shown[num].R.Contains(at))
				{
					if (shown[num].Step == null)
					{
						return false;
					}
					shown[num].Step(dir);
					return true;
				}
			}
			return false;
		}

		private static void Flip()
		{
			List<SpotRect> shown = _shown;
			_shown = _drawing;
			_drawing = shown;
			_drawing.Clear();
		}

		internal static bool FirstSliderForTest(out Rect r)
		{
			foreach (SpotRect item in Shown)
			{
				if (item.Step != null)
				{
					r = item.R;
					return true;
				}
			}
			r = default(Rect);
			return false;
		}

		public static bool Under(Vector2 at, out Rect r)
		{
			List<SpotRect> shown = Shown;
			for (int num = shown.Count - 1; num >= 0; num--)
			{
				if (shown[num].R.Contains(at))
				{
					r = shown[num].R;
					return true;
				}
			}
			r = default(Rect);
			return false;
		}

		public static bool Hop(Vector2 from, Vector2Int dir, out Vector2 to)
		{
			List<SpotRect> shown = Shown;
			to = from;
			if (shown.Count == 0)
			{
				return false;
			}
			if (!Under(from, out var r))
			{
				float num = float.MaxValue;
				foreach (SpotRect item in shown)
				{
					Rect r2 = item.R;
					float sqrMagnitude = (r2.center - from).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						r2 = item.R;
						to = r2.center;
					}
				}
				return true;
			}
			Vector2 rhs = new Vector2(dir.x, dir.y);
			Vector2 center = r.center;
			float num2 = float.MaxValue;
			bool result = false;
			foreach (SpotRect item2 in shown)
			{
				if (item2.R == r)
				{
					continue;
				}
				Rect r2 = item2.R;
				Vector2 lhs = r2.center - center;
				float num3 = Vector2.Dot(lhs, rhs);
				float num4;
				if (dir.x == 0)
				{
					float height = r.height;
					r2 = item2.R;
					num4 = (height + r2.height) * 0.25f;
				}
				else
				{
					float width = r.width;
					r2 = item2.R;
					num4 = (width + r2.width) * 0.25f;
				}
				float num5 = num4;
				if (!(num3 <= num5 * 0.5f))
				{
					float num6 = Mathf.Abs((dir.x != 0) ? lhs.y : lhs.x);
					float num7 = num3 + num6 * 2f;
					if (num7 < num2)
					{
						num2 = num7;
						r2 = item2.R;
						to = r2.center;
						result = true;
					}
				}
			}
			return result;
		}

		public static bool TabStep(Vector2 from, int step, out Vector2 to)
		{
			to = from;
			List<SpotRect> list = new List<SpotRect>();
			foreach (SpotRect item in Shown)
			{
				if (item.Tab)
				{
					list.Add(item);
				}
			}
			if (list.Count < 2)
			{
				return false;
			}
			list.Sort((SpotRect a, SpotRect b) => (!(Mathf.Abs(a.R.y - b.R.y) > 4f)) ? a.R.x.CompareTo(b.R.x) : a.R.y.CompareTo(b.R.y));
			int num = list.FindIndex((SpotRect s) => s.Chosen);
			if (num < 0)
			{
				num = list.FindIndex((SpotRect s) => s.R.Contains(from));
			}
			if (num < 0)
			{
				float num2 = float.MaxValue;
				for (int num3 = 0; num3 < list.Count; num3++)
				{
					float sqrMagnitude = (list[num3].R.center - from).sqrMagnitude;
					if (sqrMagnitude < num2)
					{
						num2 = sqrMagnitude;
						num = num3;
					}
				}
			}
			int index = ((num + step) % list.Count + list.Count) % list.Count;
			to = list[index].R.center;
			return true;
		}
	}
}
