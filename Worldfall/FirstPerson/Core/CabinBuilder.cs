using System;
using System.Collections.Generic;
using System.Globalization;

namespace FirstPerson.Core
{
	public static class CabinBuilder
	{
		public const float Scale = 0.5714286f;

		private static MeshModel _plainFrom;

		private static MeshModel _plain;

		public static string CabinFor(string boatFamily)
		{
			if (boatFamily == null || !boatFamily.StartsWith("boat_", StringComparison.Ordinal))
			{
				return null;
			}
			return "cabin_" + boatFamily.Substring(5);
		}

		public static MeshModel PlainOpenings(MeshModel model)
		{
			if (model == null)
			{
				return null;
			}
			if (_plainFrom == model)
			{
				return _plain;
			}
			MeshModel meshModel = model.Derive(model.Name, model.Parts);
			meshModel.SlotKind = (byte[])model.SlotKind.Clone();
			meshModel.SlotFlags = (byte[])model.SlotFlags.Clone();
			for (int i = 0; i < meshModel.SlotFlags.Length; i++)
			{
				if ((meshModel.SlotFlags[i] & 4) != 0)
				{
					meshModel.SlotFlags[i] = (byte)(meshModel.SlotFlags[i] & -5);
					meshModel.SlotKind[i] = 0;
				}
			}
			_plainFrom = model;
			_plain = meshModel;
			return meshModel;
		}

		public static Interior Build(MeshModel model, uint cloth)
		{
			Interior interior = new Interior
			{
				Race = "cabin"
			};
			float num = model.Min.X * 0.5714286f;
			float num2 = model.Max.X * 0.5714286f;
			float num3 = model.Min.Y * 0.5714286f;
			float num4 = model.Max.Y * 0.5714286f;
			int num5 = (int)Math.Ceiling(num2 - num) + 2;
			int num6 = (int)Math.Ceiling(num4 - num3) + 2;
			float num7 = (float)num5 * 0.5f - (num + num2) * 0.5f;
			float num8 = (float)num6 * 0.5f - (num3 + num4) * 0.5f;
			new Vec3(num7, num8, 0f);
			TerrainCache terrainCache = new TerrainCache();
			terrainCache.Allocate(num5, num6);
			terrainCache.CastShadows = false;
			terrainCache.DitherEdges = false;
			terrainCache.OutsideIsWater = false;
			float num9 = (terrainCache.OutsideHeight = model.InfoFloat("top", 2.2f) * 0.5714286f);
			uint num10 = TerrainCache.Rgb(96, 68, 44);
			terrainCache.OutsideColor = TerrainCache.Scale(num10, 0.6f);
			for (int i = 0; i < num5 * num6; i++)
			{
				terrainCache.BaseColors[i] = num10;
				terrainCache.Materials[i] = 8;
				terrainCache.Heights[i] = (terrainCache.Ground[i] = -0.03f);
			}
			terrainCache.Relight(0, 0, num5, num6);
			interior.Floor = terrainCache;
			interior.Top = num9 + 0.1f;
			interior.CeilingTop = TerrainCache.Rgb(54, 38, 26);
			interior.CeilingBottom = TerrainCache.Rgb(78, 56, 38);
			uint[] array = new uint[model.SlotNames.Length];
			for (int j = 0; j < array.Length; j++)
			{
				array[j] = model.SlotColor[j];
				if (cloth != 0 && model.SlotNames[j] == "cloth")
				{
					array[j] = TerrainCache.Lerp(cloth, TerrainCache.Rgb(150, 40, 40), 0.25f);
				}
			}
			MeshInstance meshInstance = new MeshInstance
			{
				Model = model,
				Palette = array,
				NoShadow = true,
				Static = false,
				Tag = -1
			};
			meshInstance.Place(num7, num8, 0f, -MathF.PI / 2f, 0.5714286f, 0.5714286f, 0.5714286f);
			interior.Meshes.Add(meshInstance);
			interior.Shell = meshInstance;
			interior.GlassSlot = model.SlotIndex("porthole");
			float[] array2 = Floats(model.Info("floor"));
			if (array2 != null && array2.Length >= 4)
			{
				interior.MinX = num7 + array2[0] * 0.5714286f;
				interior.MinY = num8 + array2[1] * 0.5714286f;
				interior.MaxX = num7 + array2[2] * 0.5714286f;
				interior.MaxY = num8 + array2[3] * 0.5714286f;
			}
			else
			{
				interior.MinX = num7 + num + 0.3f;
				interior.MaxX = num7 + num2 - 0.3f;
				interior.MinY = num8 + num3 + 0.3f;
				interior.MaxY = num8 + num4 - 0.3f;
			}
			float[] array3 = Floats(model.Info("door"));
			interior.DoorX = num7 + ((array3 != null) ? array3[0] : 0f) * 0.5714286f;
			interior.DoorY = num8 + ((array3 != null) ? array3[1] : (num3 / 0.5714286f + 0.6f)) * 0.5714286f;
			float[] array4 = Floats(model.Info("spawn"));
			interior.SpawnX = ((array4 != null) ? (num7 + array4[0] * 0.5714286f) : interior.DoorX);
			interior.SpawnY = ((array4 != null) ? (num8 + array4[1] * 0.5714286f) : interior.DoorY);
			interior.SpawnYaw = ((array4 != null && array4.Length > 2) ? array4[2] : 90f) * MathF.PI / 180f;
			float[] array5 = Floats(model.Info("hearth"));
			interior.HearthX = ((array5 != null) ? (num7 + array5[0] * 0.5714286f) : (-100f));
			interior.HearthY = ((array5 != null) ? (num8 + array5[1] * 0.5714286f) : (-100f));
			foreach (float[] item in List(model.Info("solids")))
			{
				if (item.Length >= 4)
				{
					interior.Solids.Add(new float[4]
					{
						num7 + item[0] * 0.5714286f,
						num8 + item[1] * 0.5714286f,
						num7 + item[2] * 0.5714286f,
						num8 + item[3] * 0.5714286f
					});
				}
			}
			foreach (float[] item2 in List(model.Info("spots")))
			{
				if (item2.Length >= 3)
				{
					interior.Spots.Add(new float[3]
					{
						num7 + item2[0] * 0.5714286f,
						num8 + item2[1] * 0.5714286f,
						item2[2]
					});
				}
			}
			foreach (float[] item3 in List(model.Info("lamps")))
			{
				if (item3.Length >= 4)
				{
					bool flag = item3[3] > 0.5f;
					interior.Lights.Add(new RoomLight
					{
						Fire = flag,
						Light = new PointLight
						{
							X = num7 + item3[0] * 0.5714286f,
							Y = num8 + item3[1] * 0.5714286f,
							Z = item3[2] * 0.5714286f,
							Radius = (flag ? 1.6f : 2.6f),
							Glow = (flag ? 0.04f : 0.07f),
							R = (flag ? 0.5f : 0.52f),
							G = (flag ? 0.3f : 0.37f),
							B = (flag ? 0.14f : 0.2f)
						}
					});
				}
			}
			foreach (float[] item4 in List(model.Info("windows")))
			{
				if (item4.Length >= 7)
				{
					Vec3 inward = new Vec3(item4[3], item4[4], 0f).Normalized();
					interior.Windows.Add(new LightWindow
					{
						Center = new Vec3(num7 + item4[0] * 0.5714286f, num8 + item4[1] * 0.5714286f, item4[2] * 0.5714286f),
						Right = new Vec3(0f - inward.Y, inward.X, 0f),
						Inward = inward,
						HalfWidth = item4[5] * 0.5714286f,
						HalfHeight = item4[6] * 0.5714286f
					});
				}
			}
			return interior;
		}

		public static List<int> SwingingParts(MeshModel model)
		{
			List<int> list = new List<int>();
			for (int i = 0; i < model.Parts.Length; i++)
			{
				if (model.Parts[i].Name != null && model.Parts[i].Name.StartsWith("lamp_", StringComparison.Ordinal))
				{
					list.Add(i);
				}
			}
			return list;
		}

		internal static float[] Floats(string s)
		{
			if (string.IsNullOrEmpty(s))
			{
				return null;
			}
			string[] array = s.Split(',');
			float[] array2 = new float[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				if (!float.TryParse(array[i], NumberStyles.Float, CultureInfo.InvariantCulture, out array2[i]))
				{
					return null;
				}
			}
			return array2;
		}

		private static IEnumerable<float[]> List(string s)
		{
			if (string.IsNullOrEmpty(s))
			{
				yield break;
			}
			string[] array = s.Split('|');
			for (int i = 0; i < array.Length; i++)
			{
				float[] array2 = Floats(array[i]);
				if (array2 != null)
				{
					yield return array2;
				}
			}
		}
	}
}
