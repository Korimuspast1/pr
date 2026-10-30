using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class BuildSite
	{
		public const int Stages = 4;

		private static readonly Dictionary<MeshModel, MeshModel[]> Made = new Dictionary<MeshModel, MeshModel[]>();

		private static readonly object Lock = new object();

		public static int StageOf(int progress, int needed)
		{
			if (needed <= 0)
			{
				return 3;
			}
			int num = (int)((long)Math.Max(0, progress) * 4L / needed);
			if (num >= 0)
			{
				if (num < 4)
				{
					return num;
				}
				return 3;
			}
			return 0;
		}

		public static MeshModel At(MeshModel site, int stage, bool far = false)
		{
			if (site == null)
			{
				return null;
			}
			stage = Math.Max(0, Math.Min(3, stage));
			int num = stage + (far ? 4 : 0);
			lock (Lock)
			{
				if (!Made.TryGetValue(site, out var value))
				{
					if (Made.Count > 400)
					{
						Made.Clear();
					}
					MeshModel[] array = (Made[site] = new MeshModel[8]);
					value = array;
				}
				if (value[num] == null)
				{
					List<MeshPart> list = new List<MeshPart>();
					MeshPart[] parts = site.Parts;
					foreach (MeshPart meshPart in parts)
					{
						if (Shows(meshPart.Name, stage) && (!far || !IsClutter(meshPart.Name)))
						{
							list.Add(new MeshPart
							{
								Name = meshPart.Name,
								Parent = -1,
								Pivot = meshPart.Pivot,
								Pos = meshPart.Pos,
								Nrm = meshPart.Nrm,
								Tri = meshPart.Tri,
								Slot = meshPart.Slot,
								Axis = meshPart.Axis,
								Uv = meshPart.Uv
							});
						}
					}
					value[num] = site.Derive(site.Name + "@" + stage + (far ? "_far" : ""), list.ToArray());
				}
				return value[num];
			}
		}

		public static bool IsClutter(string part)
		{
			int num = part?.LastIndexOf('@') ?? (-1);
			if (num > 0)
			{
				return part[num - 1] == '~';
			}
			return false;
		}

		public static bool Shows(string part, int stage)
		{
			int num = part?.LastIndexOf('@') ?? (-1);
			if (num < 0)
			{
				return true;
			}
			int num2 = part.IndexOf('-', num);
			int num3 = Number(part, num + 1, (num2 < 0) ? part.Length : num2);
			int num4 = ((num2 < 0) ? 3 : Number(part, num2 + 1, part.Length));
			if (stage >= num3)
			{
				return stage <= num4;
			}
			return false;
		}

		private static int Number(string s, int from, int to)
		{
			int num = 0;
			for (int i = from; i < to; i++)
			{
				char c = s[i];
				if (c < '0' || c > '9')
				{
					break;
				}
				num = num * 10 + (c - 48);
			}
			return num;
		}
	}
}
