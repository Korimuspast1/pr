using System.IO;
using FirstPerson;
using UnityEngine;

namespace Worldfall
{
	public class WorldBoxMod : MonoBehaviour
	{
		private void Awake()
		{
			if (!File.Exists(Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.dll")))
			{
				base.gameObject.AddComponent<FirstPerson.WorldBoxMod>();
			}
			Object.Destroy(this);
		}
	}
}
