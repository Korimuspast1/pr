using System;
using UnityEngine;
using UnityEngine.UI;

namespace FirstPerson.Net
{
	internal static class PanelBlocker
	{
		private static GameObject _root;

		private static RectTransform _rect;

		private static int _setFrame = -1;

		public static void Cover(Rect screen)
		{
			try
			{
				if (_root == null)
				{
					_root = new GameObject("FirstPersonNetPanelBlocker");
					UnityEngine.Object.DontDestroyOnLoad(_root);
					Canvas canvas = _root.AddComponent<Canvas>();
					canvas.renderMode = RenderMode.ScreenSpaceOverlay;
					canvas.sortingOrder = 31999;
					_root.AddComponent<GraphicRaycaster>();
					GameObject gameObject = new GameObject("Sheet");
					gameObject.transform.SetParent(_root.transform, worldPositionStays: false);
					Image image = gameObject.AddComponent<Image>();
					image.color = new Color(0f, 0f, 0f, 0f);
					image.raycastTarget = true;
					_rect = image.rectTransform;
					RectTransform rect = _rect;
					Vector2 anchorMin = (_rect.anchorMax = Vector2.zero);
					rect.anchorMin = anchorMin;
					_rect.pivot = Vector2.zero;
				}
				_rect.anchoredPosition = new Vector2(screen.x, (float)Screen.height - screen.yMax);
				_rect.sizeDelta = new Vector2(screen.width, screen.height);
				if (!_root.activeSelf)
				{
					_root.SetActive(value: true);
				}
				_setFrame = Time.frameCount;
			}
			catch (Exception ex)
			{
				Log.Error("NET panel blocker: " + ex.Message);
			}
		}

		public static void Tick()
		{
			if (_root != null && _root.activeSelf && Time.frameCount - _setFrame > 1)
			{
				_root.SetActive(value: false);
			}
		}
	}
}
