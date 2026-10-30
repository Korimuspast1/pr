using UnityEngine;

namespace FirstPerson
{
	[DefaultExecutionOrder(-31000)]
	internal sealed class WorldStepClock : MonoBehaviour
	{
		private void Update()
		{
			WorldBoxMod instance = WorldBoxMod.Instance;
			WorldSteps.BeginFrame(instance != null && instance.WorldStepsWanted);
			SimLod.BeginFrame(instance);
		}
	}
}
