using System;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal sealed class JournalEntry
	{
		public string Key;

		public string Title;

		public string Status;

		public string TimeLeft;

		public string Promise;

		public bool Ready;

		public bool HasWay;

		public Vector2 Way;

		public Action GiveUp;
	}
}
