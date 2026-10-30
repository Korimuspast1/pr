using System;
using FirstPerson.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FirstPerson.OneWorld
{
	internal static class MapPeek
	{
		private const float Zoom = 25f;

		private const float Fade = 0.15f;

		private const float HoldTouch = 0.35f;

		private static Building _house;

		private static Building _shown;

		private static float _open;

		private static float _touchAt = -1f;

		private static Vector2 _touchFrom;

		internal static int PeopleForTest;

		internal static Building ForceForTest;

		internal static Building OpenForTest
		{
			get
			{
				if (!(_open > 0.01f))
				{
					return null;
				}
				return _shown;
			}
		}

		internal static bool Hides(Building b)
		{
			if (b != null && b == _shown)
			{
				return _open > 0f;
			}
			return false;
		}

		public static void Tick(Settings settings, bool flatMap)
		{
			Building building = null;
			if (flatMap && RoofDive.Open != null)
			{
				building = RoofDive.Open;
			}
			else if (flatMap && settings.LookInside && Close())
			{
				building = ForceForTest ?? Under();
			}
			if (building != _house)
			{
				if (_open > 0f && _shown != null && building != _shown)
				{
					_house = building;
				}
				else
				{
					_house = building;
				}
			}
			float num = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
			if (_shown != null && _shown != _house)
			{
				_open = Mathf.Max(0f, _open - num / 0.15f);
			}
			else if (_house != null)
			{
				if (_shown != _house)
				{
					_shown = _house;
					_open = 0f;
					MapPeekDraw.Begin(_house);
				}
				HouseWorld.Placement pose;
				RoomLayout roomLayout = InsideSpots.LayoutOf(_house, out pose);
				if (roomLayout == null)
				{
					InsideSpots.Want(_house);
				}
				_open = ((roomLayout != null) ? Mathf.Min(1f, _open + num / 0.15f) : 0f);
			}
			if (_open <= 0f && _shown != _house)
			{
				_shown = null;
			}
			if (_shown == null || !_shown.isAlive())
			{
				MapPeekDraw.Hide();
				PeopleForTest = 0;
				return;
			}
			PeopleForTest = MapPeekDraw.Show(_shown, _open);
			if (_open >= 1f)
			{
				Clicks();
			}
		}

		private static bool Close()
		{
			try
			{
				if (!MapBox.isRenderGameplay())
				{
					return false;
				}
				Camera camera = World.world.camera;
				return camera != null && camera.orthographic && camera.orthographicSize <= 25f;
			}
			catch
			{
				return false;
			}
		}

		private static Building Under()
		{
			try
			{
				if (Input.touchCount > 0)
				{
					Touch touch = Input.GetTouch(0);
					if (touch.phase == TouchPhase.Began)
					{
						_touchAt = Time.unscaledTime;
						_touchFrom = touch.position;
					}
					if ((touch.position - _touchFrom).magnitude > 30f)
					{
						_touchAt = -1f;
					}
					if (_touchAt < 0f || Time.unscaledTime - _touchAt < 0.35f)
					{
						return _house;
					}
					Vector3 vector = World.world.camera.ScreenToWorldPoint(touch.position);
					return HouseAt(World.world.GetTile((int)Mathf.Floor(vector.x), (int)Mathf.Floor(vector.y)));
				}
				_touchAt = -1f;
				if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
				{
					return null;
				}
				return HouseAt(World.world.getMouseTilePosCachedFrame());
			}
			catch
			{
				return null;
			}
		}

		private static Building HouseAt(WorldTile t)
		{
			Building building = t?.building;
			if (building == null || building.asset == null || building.asset.type != "type_house")
			{
				return null;
			}
			if (!building.isUsable() || building.isUnderConstruction() || building.isAbandoned())
			{
				return null;
			}
			return building;
		}

		private static void Clicks()
		{
			bool mouseButtonDown = Input.GetMouseButtonDown(0);
			Vector2 at;
			try
			{
				string selectedPowerID = World.world.getSelectedPowerID();
				if (!string.IsNullOrEmpty(selectedPowerID) && selectedPowerID.IndexOf("inspect", StringComparison.Ordinal) < 0)
				{
					return;
				}
				if (mouseButtonDown)
				{
					at = World.world.getMousePos();
				}
				else
				{
					if (Input.touchCount != 1 || Input.GetTouch(0).phase != TouchPhase.Ended)
					{
						return;
					}
					at = World.world.camera.ScreenToWorldPoint(Input.GetTouch(0).position);
				}
				if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
				{
					return;
				}
			}
			catch
			{
				return;
			}
			SelectAt(at);
		}

		internal static Actor SelectAt(Vector2 at)
		{
			Actor actor = MapPeekDraw.PersonAt(at);
			if (actor == null)
			{
				return null;
			}
			try
			{
				SelectedUnit.select(actor);
			}
			catch (Exception ex)
			{
				Log.Error("peek: select: " + ex.Message);
			}
			Log.Info("peek: selected " + WorldBoxMod.Describe(actor) + " inside " + _shown?.asset?.id);
			return actor;
		}

		internal static void Close_()
		{
			_house = (_shown = null);
			_open = 0f;
			MapPeekDraw.Hide();
		}
	}
}
