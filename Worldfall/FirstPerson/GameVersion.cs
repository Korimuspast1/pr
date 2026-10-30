using System;
using UnityEngine;

namespace FirstPerson
{
	internal static class GameVersion
	{
		private static readonly int[] Oldest = new int[3] { 0, 51, 0 };

		public const string OldestText = "0.51";

		public static string Current
		{
			get
			{
				string environmentVariable = Environment.GetEnvironmentVariable("FIRSTPERSON_GAMEVERSION");
				if (!string.IsNullOrEmpty(environmentVariable))
				{
					return environmentVariable.Trim();
				}
				try
				{
					return (Application.version ?? "").Trim();
				}
				catch
				{
					return "";
				}
			}
		}

		public static bool TooOld(string version)
		{
			int[] array = Parse(version);
			if (array == null)
			{
				return false;
			}
			for (int i = 0; i < Oldest.Length; i++)
			{
				if (array[i] != Oldest[i])
				{
					return array[i] < Oldest[i];
				}
			}
			return false;
		}

		private static int[] Parse(string version)
		{
			if (string.IsNullOrEmpty(version))
			{
				return null;
			}
			int i;
			for (i = 0; i < version.Length && (char.IsDigit(version[i]) || version[i] == '.'); i++)
			{
			}
			string[] array = version.Substring(0, i).Split(new char[1] { '.' }, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length < 2)
			{
				return null;
			}
			int[] array2 = new int[3];
			for (int j = 0; j < 3 && j < array.Length; j++)
			{
				if (!int.TryParse(array[j], out array2[j]))
				{
					return null;
				}
			}
			return array2;
		}
	}
}
