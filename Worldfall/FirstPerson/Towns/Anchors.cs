using System;
using System.Collections.Generic;
using System.Globalization;
using FirstPerson.Core;

namespace FirstPerson.Towns
{
	internal static class Anchors
	{
		internal struct Spot
		{
			public Vec3 At;

			public float Yaw;

			public bool HasYaw;
		}

		private static readonly Dictionary<MeshModel, Dictionary<string, Spot[]>> Cache = new Dictionary<MeshModel, Dictionary<string, Spot[]>>();

		private static readonly Spot[] None = new Spot[0];

		private static readonly object Lock = new object();

		public static Vec3[] Of(MeshModel m, string name)
		{
			Spot[] array = Spots(m, name);
			Vec3[] array2 = new Vec3[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array2[i] = array[i].At;
			}
			return array2;
		}

		public static Spot[] Spots(MeshModel m, string name)
		{
			if (m == null || string.IsNullOrEmpty(name))
			{
				return None;
			}
			string key = (name.StartsWith("a_", StringComparison.Ordinal) ? name : ("a_" + name));
			lock (Lock)
			{
				if (!Cache.TryGetValue(m, out var value))
				{
					if (Cache.Count > 400)
					{
						Cache.Clear();
					}
					value = (Cache[m] = new Dictionary<string, Spot[]>());
				}
				if (value.TryGetValue(key, out var value2))
				{
					return value2;
				}
				return value[key] = Parse(m.Info(key));
			}
		}

		internal static Spot[] Parse(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return None;
			}
			List<Spot> list = new List<Spot>();
			string[] array = value.Split('/');
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split(',');
				if (array2.Length >= 3 && Num(array2[0], out var v) && Num(array2[1], out var v2) && Num(array2[2], out var v3))
				{
					Spot item = new Spot
					{
						At = new Vec3(v, v2, v3)
					};
					if (array2.Length >= 4 && Num(array2[3], out var v4))
					{
						item.Yaw = v4;
						item.HasYaw = true;
					}
					list.Add(item);
				}
			}
			if (list.Count <= 0)
			{
				return None;
			}
			return list.ToArray();
		}

		private static bool Num(string s, out float v)
		{
			return float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
		}

		public static Vec3 World(MeshInstance inst, Vec3 local)
		{
			return inst.Position + inst.Basis.Mul(local);
		}

		public static float WorldHeading(float heading, float yawDeg, bool mirrored)
		{
			return heading + (mirrored ? (0f - yawDeg) : yawDeg) * (MathF.PI / 180f);
		}
	}
}
