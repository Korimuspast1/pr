using UnityEngine;

namespace FirstPerson.Towns
{
	internal sealed class PromptAsk
	{
		public Actor Host;

		public Vector2 Pos;

		public float Yaw;

		public BaseSimObject Picked;

		public HouseInterior Room;

		public Building House;

		public Vector2 Eye;

		public string Prompt;
	}
}
