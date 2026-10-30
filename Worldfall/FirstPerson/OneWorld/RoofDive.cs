using System;
using UnityEngine;

namespace FirstPerson.OneWorld
{
	internal static class RoofDive
	{
		private static Building _house;

		private static float _until;

		internal static Building LastForTest;

		public static Building Open
		{
			get
			{
				if (_house == null || !(Time.unscaledTime < _until))
				{
					return null;
				}
				return _house;
			}
		}

		public static void LeftIndoors(Actor body, Building house, Vector2 at, float riseSeconds)
		{
			if (house == null || !house.isAlive())
			{
				return;
			}
			_house = house;
			_until = Time.unscaledTime + riseSeconds + 1.5f;
			LastForTest = house;
			if (body == null || !body.isAlive())
			{
				return;
			}
			bool flag = false;
			try
			{
				flag = ControllableUnit.isControllingUnit(body);
			}
			catch
			{
			}
			if (flag)
			{
				return;
			}
			try
			{
				body.stayInBuilding(house);
				body.timer_action = 15f;
				WorldTile tile;
				Vector2 current_position = InsideSpots.Clamp(house, at, out tile);
				if (tile != null)
				{
					body.setCurrentTilePosition(tile);
					body.current_position = current_position;
				}
			}
			catch (Exception ex)
			{
				Log.Error("through the roof: the body back in: " + ex.Message);
			}
		}
	}
}
