using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class HousePlanTurn
	{
		private static readonly Dictionary<string, HousePlan[]> Known = new Dictionary<string, HousePlan[]>();

		public static HousePlan Of(MeshModel model, int turns)
		{
			HousePlan housePlan = HousePlan.Of(model);
			turns = (turns % 4 + 4) % 4;
			if (housePlan == null || turns == 0)
			{
				return housePlan;
			}
			HousePlan[] value;
			lock (Known)
			{
				if (!Known.TryGetValue(model.Name, out value))
				{
					value = (Known[model.Name] = new HousePlan[4]);
				}
				if (value[turns] != null)
				{
					return value[turns];
				}
			}
			HousePlan housePlan2 = Turned(housePlan, turns);
			lock (Known)
			{
				value[turns] = housePlan2;
				return housePlan2;
			}
		}

		public static void Turn(int turns, ref float x, ref float y)
		{
			float num = x;
			float num2 = y;
			switch ((turns % 4 + 4) % 4)
			{
			case 1:
				x = 0f - num2;
				y = num;
				break;
			case 2:
				x = 0f - num;
				y = 0f - num2;
				break;
			case 3:
				x = num2;
				y = 0f - num;
				break;
			}
		}

		private static HousePlan Turned(HousePlan p, int q)
		{
			HousePlan housePlan = new HousePlan
			{
				Model = p.Model + "@" + q,
				FloorZ = p.FloorZ
			};
			bool flag = (q & 1) == 1;
			housePlan.W = (flag ? p.H : p.W);
			housePlan.H = (flag ? p.W : p.H);
			switch (q)
			{
			case 1:
				housePlan.X0 = 0f - (p.Y0 + (float)p.H * 0.05f);
				housePlan.Y0 = p.X0;
				break;
			case 2:
				housePlan.X0 = 0f - (p.X0 + (float)p.W * 0.05f);
				housePlan.Y0 = 0f - (p.Y0 + (float)p.H * 0.05f);
				break;
			default:
				housePlan.X0 = p.Y0;
				housePlan.Y0 = 0f - (p.X0 + (float)p.W * 0.05f);
				break;
			}
			foreach (HousePlan.Storey storey2 in p.Storeys)
			{
				HousePlan.Storey storey = new HousePlan.Storey
				{
					Floor = storey2.Floor,
					Top = storey2.Top,
					Area = storey2.Area
				};
				if (storey2.Inside != null)
				{
					storey.Inside = new bool[housePlan.W * housePlan.H];
				}
				if (storey2.Ceiling != null)
				{
					storey.Ceiling = new float[housePlan.W * housePlan.H];
				}
				for (int i = 0; i < housePlan.H; i++)
				{
					for (int j = 0; j < housePlan.W; j++)
					{
						int num;
						int num2;
						switch (q)
						{
						case 1:
							num = i;
							num2 = p.H - 1 - j;
							break;
						case 2:
							num = p.W - 1 - j;
							num2 = p.H - 1 - i;
							break;
						default:
							num = p.W - 1 - i;
							num2 = j;
							break;
						}
						int num3 = num2 * p.W + num;
						int num4 = i * housePlan.W + j;
						if (storey.Inside != null)
						{
							storey.Inside[num4] = storey2.Inside[num3];
						}
						if (storey.Ceiling != null)
						{
							storey.Ceiling[num4] = storey2.Ceiling[num3];
						}
					}
				}
				foreach (List<HousePlan.P2> loop in storey2.Loops)
				{
					List<HousePlan.P2> list = new List<HousePlan.P2>(loop.Count);
					foreach (HousePlan.P2 item in loop)
					{
						list.Add(Point(item, q));
					}
					storey.Loops.Add(list);
				}
				foreach (HousePlan.P2 passage in storey2.Passages)
				{
					storey.Passages.Add(Point(passage, q));
				}
				housePlan.Storeys.Add(storey);
			}
			housePlan.Door = Opening(p.Door, q);
			foreach (HousePlan.Opening window in p.Windows)
			{
				housePlan.Windows.Add(Opening(window, q));
			}
			housePlan.ChimneyX = p.ChimneyX;
			housePlan.ChimneyY = p.ChimneyY;
			if (!float.IsNaN(housePlan.ChimneyX))
			{
				Turn(q, ref housePlan.ChimneyX, ref housePlan.ChimneyY);
			}
			return housePlan;
		}

		private static HousePlan.P2 Point(HousePlan.P2 pt, int q)
		{
			float x = pt.X;
			float y = pt.Y;
			Turn(q, ref x, ref y);
			return new HousePlan.P2(x, y);
		}

		private static HousePlan.Opening Opening(HousePlan.Opening o, int q)
		{
			Turn(q, ref o.X, ref o.Y);
			Turn(q, ref o.OutX, ref o.OutY);
			return o;
		}
	}
}
