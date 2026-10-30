using System;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class Facing
	{
		public const string Key = "fp_face";

		public static int Of(Building b)
		{
			if (b == null)
			{
				return 0;
			}
			try
			{
				b.data.get("fp_face", out var pResult, 0);
				if ((pResult & 3) == 0)
				{
					return 0;
				}
				if (!Turns(b.asset))
				{
					return 0;
				}
				return Quarter(b.asset) ? (pResult & 3) : (pResult & 2);
			}
			catch
			{
				return 0;
			}
		}

		public static void Set(Building b, int turns)
		{
			if (b == null)
			{
				return;
			}
			try
			{
				if ((turns & 3) == 0)
				{
					b.data.removeInt("fp_face");
				}
				else
				{
					b.data.set("fp_face", turns & 3);
				}
			}
			catch
			{
			}
		}

		public static bool DockTurn(Building b, out int turns)
		{
			turns = 0;
			if (b == null || b.asset == null || !b.asset.docks)
			{
				return false;
			}
			try
			{
				b.data.get("fp_face", out var pResult, 0);
				if ((pResult & 4) == 0)
				{
					return false;
				}
				turns = pResult & 3;
				return true;
			}
			catch
			{
				return false;
			}
		}

		public static void SetDock(Building b, int turns)
		{
			if (b == null)
			{
				return;
			}
			try
			{
				if (turns < 0)
				{
					b.data.removeInt("fp_face");
				}
				else
				{
					b.data.set("fp_face", 4 | (turns & 3));
				}
			}
			catch
			{
			}
		}

		public static bool Turns(BuildingAsset a)
		{
			if (a == null)
			{
				return false;
			}
			string text = a.id ?? "";
			if (!text.StartsWith("docks") && !text.StartsWith("mine") && !text.Contains("_docks"))
			{
				return !text.Contains("mine_");
			}
			return false;
		}

		public static bool Quarter(BuildingAsset a)
		{
			BuildingFundament buildingFundament = a?.fundament;
			if (buildingFundament != null)
			{
				return buildingFundament.left + buildingFundament.right == buildingFundament.top + buildingFundament.bottom;
			}
			return true;
		}

		public static int Next(BuildingAsset a, int turns)
		{
			if (!Turns(a))
			{
				return 0;
			}
			if (!Quarter(a))
			{
				return (turns + 2) & 3;
			}
			return (turns + 1) & 3;
		}

		public static float Heading(int turns)
		{
			return -MathF.PI / 2f + (float)(turns & 3) * MathF.PI * 0.5f;
		}

		public static Vector2 Turn(Vector2 v, int turns)
		{
			float x = v.x;
			float y = v.y;
			HousePlanTurn.Turn(turns, ref x, ref y);
			return new Vector2(x, y);
		}

		public static int PlanTurns(int turns, bool mirror)
		{
			if (!mirror)
			{
				return turns & 3;
			}
			return (4 - (turns & 3)) & 3;
		}
	}
}
