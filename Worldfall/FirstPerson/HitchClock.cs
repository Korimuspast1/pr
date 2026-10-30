using UnityEngine;

namespace FirstPerson
{
	[DefaultExecutionOrder(-32000)]
	internal sealed class HitchClock : MonoBehaviour
	{
		private void Update()
		{
			Hitch.FrameStart();
		}
	}
}
