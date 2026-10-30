using System;
using FirstPerson.Pad;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class RadialWheel
	{
		public delegate bool Pointer(out Vector2 gui);

		public const int Most = 8;

		public readonly string[] Labels = new string[8];

		public readonly bool[] Dim = new bool[8];

		public readonly string[] Notes = new string[8];

		public int Count;

		public bool Open;

		public int Choice = -1;

		public float OpenedAt;

		public string Tip;

		public Color Tint = new Color(0.86f, 0.74f, 0.5f);

		private Vector2 _aim;

		public Vector2 ShownAt;

		public float ShownOuter;

		public static Pointer PointAt;

		public void Clear()
		{
			Count = 0;
		}

		public void Add(string label, bool dim = false, string note = null)
		{
			if (Count < 8)
			{
				Labels[Count] = label;
				Dim[Count] = dim;
				Notes[Count] = note;
				Count++;
			}
		}

		public void Show()
		{
			Open = true;
			_aim = Vector2.zero;
			Choice = -1;
			OpenedAt = Time.unscaledTime;
			Sounds.PlayUI("event:/SFX/UI/WindowWhoosh");
		}

		public int Close()
		{
			Open = false;
			int result = ((Choice >= 0 && Choice < Count && !Dim[Choice]) ? Choice : (-1));
			Choice = -1;
			return result;
		}

		public bool Steal(bool frozen)
		{
			if (!Open)
			{
				return false;
			}
			float x = 0f;
			float y = 0f;
			try
			{
				x = Input.GetAxisRaw("Mouse X");
				y = Input.GetAxisRaw("Mouse Y");
			}
			catch (ArgumentException)
			{
			}
			if (frozen)
			{
				x = (y = 0f);
			}
			if (PointAt != null && ShownOuter > 1f && PointAt(out var gui))
			{
				Vector2 vector = gui - ShownAt;
				_aim = new Vector2(vector.x, 0f - vector.y) / ShownOuter;
			}
			else
			{
				_aim += new Vector2(x, y) * 0.12f;
				Vector2 vector2 = PadControls.TakeLook();
				if (vector2.sqrMagnitude > 0f)
				{
					_aim += vector2 * 0.02f;
				}
			}
			if (_aim.magnitude > 1f)
			{
				_aim = _aim.normalized;
			}
			int choice = Choice;
			Choice = WorldBoxMod.WheelSegment(_aim, Count);
			if (Choice != choice && Choice >= 0)
			{
				Sounds.PlayUI("event:/SFX/Ui/Click");
			}
			if (Cursor.lockState != CursorLockMode.Locked)
			{
				Cursor.lockState = CursorLockMode.Locked;
			}
			Cursor.visible = false;
			return true;
		}

		public void PointForTest(int slice)
		{
			Choice = slice;
			if (slice >= 0 && Count > 0)
			{
				float f = (float)slice * (360f / (float)Count) * (MathF.PI / 180f);
				_aim = new Vector2(Mathf.Sin(f), Mathf.Cos(f));
			}
		}
	}
}
