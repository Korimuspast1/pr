using System;
using System.Collections.Generic;

namespace FirstPerson.Core
{
	public static class ToolMeshes
	{
		public enum Hold
		{
			Handle,
			Hang,
			Upright,
			Small
		}

		private static readonly Dictionary<string, MeshModel> Cache = new Dictionary<string, MeshModel>();

		public static Hold HoldOf(string tool)
		{
			switch (tool)
			{
			case "basket":
			case "bucket":
				return Hold.Hang;
			case "flag":
				return Hold.Upright;
			case "book":
			case "notebook":
			case "coffee_cup":
				return Hold.Small;
			default:
				return Hold.Handle;
			}
		}

		public static WeaponKind SwingOf(string tool)
		{
			switch (tool)
			{
			case "axe":
			case "pickaxe":
			case "shovel":
				return WeaponKind.Axe;
			case "hoe":
			case "hammer":
				return WeaponKind.Hammer;
			default:
				return WeaponKind.None;
			}
		}

		public static MeshModel Tool(string tool)
		{
			if (tool == null)
			{
				return null;
			}
			if (Cache.TryGetValue(tool, out var value))
			{
				return value;
			}
			value = BuildTool(tool);
			Cache[tool] = value;
			return value;
		}

		public static MeshModel Fresh(string tool)
		{
			if (tool != null)
			{
				return BuildTool(tool);
			}
			return null;
		}

		public static MeshModel Load(string resource, uint color)
		{
			string text = ((resource == null) ? "sack" : (resource.Contains("wood") ? "logs" : (IsMineral(resource) ? "ore" : "sack")));
			string text2 = "load_" + text + ((text == "ore") ? ("_" + color.ToString("x")) : "");
			if (Cache.TryGetValue(text2, out var value))
			{
				return value;
			}
			if (Cache.Count > 96)
			{
				Cache.Clear();
			}
			MeshBuilder meshBuilder = new MeshBuilder(text2);
			meshBuilder.Part("item", -1, new Vec3(0f, 0f, 0f));
			Vec3 ax = new Vec3(1f, 0f, 0f);
			Vec3 vec = new Vec3(0f, 1f, 0f);
			new Vec3(0f, 0f, 1f);
			if (!(text == "logs"))
			{
				if (text == "ore")
				{
					int slot = meshBuilder.Slot("ore", 14, color, 0);
					int slot2 = meshBuilder.Slot("ore_dark", 14, TerrainCache.Scale(color, 0.7f), 0);
					meshBuilder.Blob(slot, new Vec3(0f, 0f, -0.06f), 0.05f, 0.045f, 0.042f, 7);
					meshBuilder.Blob(slot2, new Vec3(0.03f, 0.02f, -0.075f), 0.03f, 0.028f, 0.026f, 6);
				}
				else
				{
					int slot3 = meshBuilder.Slot("sack", 7, TerrainCache.Rgb(178, 146, 98), 0);
					int slot4 = meshBuilder.Slot("tie", 7, TerrainCache.Rgb(120, 86, 52), 0);
					meshBuilder.Loft(slot3, new Vec3[5]
					{
						new Vec3(0f, 0f, -0.2f),
						new Vec3(0f, 0f, -0.16f),
						new Vec3(0f, 0f, -0.08f),
						new Vec3(0f, 0f, -0.035f),
						new Vec3(0f, 0f, -0.01f)
					}, new float[5] { 0.045f, 0.075f, 0.07f, 0.03f, 0.022f }, new float[5] { 0.04f, 0.065f, 0.06f, 0.026f, 0.02f }, ax, vec, 9, smooth: true);
					meshBuilder.Tube(slot4, new Vec3(0f, 0f, -0.04f), new Vec3(0f, 0f, -0.025f), 0.026f, 0.026f, 8);
				}
			}
			else
			{
				int slot5 = meshBuilder.Slot("bark", 2, TerrainCache.Rgb(112, 78, 48), 0);
				int slot6 = meshBuilder.Slot("cut", 3, TerrainCache.Rgb(206, 170, 112), 0);
				float[,] array = new float[3, 2]
				{
					{ -0.028f, -0.05f },
					{ 0.028f, -0.05f },
					{ 0f, -0.098f }
				};
				for (int i = 0; i < 3; i++)
				{
					Vec3 vec2 = new Vec3(array[i, 0], 0f, array[i, 1]);
					meshBuilder.Tube(slot5, vec2 - vec * 0.12f, vec2 + vec * 0.12f, 0.026f, 0.025f, 7);
					meshBuilder.Tube(slot6, vec2 + vec * 0.12f, vec2 + vec * 0.123f, 0.022f, 0.02f, 7);
					meshBuilder.Tube(slot6, vec2 - vec * 0.123f, vec2 - vec * 0.12f, 0.02f, 0.022f, 7);
				}
			}
			value = meshBuilder.Build();
			Cache[text2] = value;
			return value;
		}

		private static bool IsMineral(string id)
		{
			if (!id.Contains("stone") && !id.Contains("gold") && !id.Contains("silver") && !id.Contains("metal") && !id.Contains("mythril") && !id.Contains("adamantine") && !id.Contains("gem") && !id.Contains("ore") && !id.Contains("iron"))
			{
				return id.Contains("copper");
			}
			return true;
		}

		private static MeshModel BuildTool(string tool)
		{
			MeshBuilder meshBuilder = new MeshBuilder("tool_" + tool);
			int slot = meshBuilder.Slot("handle", 3, TerrainCache.Rgb(146, 102, 60), 0);
			int slot2 = meshBuilder.Slot("head", 10, TerrainCache.Rgb(150, 154, 162), 0);
			int slot3 = meshBuilder.Slot("head_dark", 10, TerrainCache.Rgb(104, 108, 116), 0);
			int slot4 = meshBuilder.Slot("edge", 10, TerrainCache.Rgb(208, 212, 220), 0);
			int slot5 = meshBuilder.Slot("cloth", 7, TerrainCache.Rgb(196, 64, 54), 0);
			int slot6 = meshBuilder.Slot("wicker", 6, TerrainCache.Rgb(196, 158, 94), 0);
			int slot7 = meshBuilder.Slot("fill", 1, TerrainCache.Rgb(178, 52, 64), 0);
			int slot8 = meshBuilder.Slot("water", 12, TerrainCache.Rgb(70, 130, 190), 0);
			int slot9 = meshBuilder.Slot("paper", 0, TerrainCache.Rgb(236, 226, 196), 0);
			meshBuilder.Part("item", -1, new Vec3(0f, 0f, 0f));
			Vec3 vec = new Vec3(1f, 0f, 0f);
			Vec3 vec2 = new Vec3(0f, 1f, 0f);
			Vec3 vec3 = new Vec3(0f, 0f, 1f);
			switch (tool)
			{
			case "axe":
				meshBuilder.Tube(slot, new Vec3(0f, 0f, -0.08f), new Vec3(0f, 0f, 0.42f), 0.0115f, 0.0105f, 8);
				meshBuilder.Blob(slot, new Vec3(0f, 0f, -0.082f), 0.013f, 0.013f, 0.01f);
				meshBuilder.Box(slot3, new Vec3(0f, 0.004f, 0.385f), 0.016f, 0.026f, 0.032f);
				meshBuilder.Extrude(slot2, new Vec3(0f, 0f, 0f), vec2, vec3, 0.008f, 0.02f, 0.36f, 0.075f, 0.325f, 0.112f, 0.33f, 0.118f, 0.39f, 0.112f, 0.44f, 0.075f, 0.43f, 0.02f, 0.405f);
				meshBuilder.Box(slot4, new Vec3(0f, 0.116f, 0.385f), 0.0085f, 0.005f, 0.05f);
				meshBuilder.Box(slot3, new Vec3(0f, -0.032f, 0.385f), 0.014f, 0.014f, 0.022f);
				break;
			case "pickaxe":
				meshBuilder.Tube(slot, new Vec3(0f, 0f, -0.07f), new Vec3(0f, 0f, 0.4f), 0.011f, 0.0105f, 8);
				meshBuilder.Box(slot3, new Vec3(0f, 0f, 0.39f), 0.016f, 0.02f, 0.026f);
				meshBuilder.Loft(slot2, new Vec3[4]
				{
					new Vec3(0f, 0.012f, 0.395f),
					new Vec3(0f, 0.07f, 0.392f),
					new Vec3(0f, 0.125f, 0.372f),
					new Vec3(0f, 0.168f, 0.338f)
				}, new float[4] { 0.012f, 0.0095f, 0.006f, 0f }, new float[4] { 0.011f, 0.009f, 0.006f, 0f }, vec3, vec, 6, smooth: true);
				meshBuilder.Loft(slot2, new Vec3[4]
				{
					new Vec3(0f, -0.012f, 0.395f),
					new Vec3(0f, -0.07f, 0.392f),
					new Vec3(0f, -0.122f, 0.374f),
					new Vec3(0f, -0.152f, 0.352f)
				}, new float[4] { 0.012f, 0.0095f, 0.008f, 0.004f }, new float[4] { 0.011f, 0.012f, 0.014f, 0.016f }, vec, vec3, 6, smooth: true);
				break;
			case "shovel":
				meshBuilder.Tube(slot, new Vec3(0f, 0f, -0.12f), new Vec3(0f, 0f, 0.5f), 0.0108f, 0.0102f, 8);
				meshBuilder.Box(slot, new Vec3(0f, 0f, -0.13f), 0.032f, 0.009f, 0.009f);
				meshBuilder.Tube(slot3, new Vec3(0f, 0f, 0.48f), new Vec3(0f, 0f, 0.53f), 0.0125f, 0.016f, 8);
				meshBuilder.Extrude(slot2, new Vec3(0f, 0f, 0f), vec, vec3, 0.0042f, -0.05f, 0.515f, 0.05f, 0.515f, 0.058f, 0.62f, 0.046f, 0.7f, 0f, 0.738f, -0.046f, 0.7f, -0.058f, 0.62f);
				meshBuilder.Box(slot4, new Vec3(0f, 0f, 0.728f), 0.03f, 0.0046f, 0.008f);
				meshBuilder.Box(slot3, new Vec3(0f, 0.0045f, 0.535f), 0.05f, 0.0028f, 0.012f);
				break;
			case "hoe":
			{
				meshBuilder.Tube(slot, new Vec3(0f, 0f, -0.14f), new Vec3(0f, 0f, 0.56f), 0.0105f, 0.0098f, 8);
				meshBuilder.Tube(slot3, new Vec3(0f, 0f, 0.545f), new Vec3(0f, 0.036f, 0.565f), 0.008f, 0.007f, 6);
				Vec3 vec4 = new Vec3(0f, 0.28f, -0.96f).Normalized();
				Vec3 vec5 = Vec3.Cross(vec4, vec);
				Vec3 vec6 = new Vec3(0f, 0.036f, 0.565f) + vec4 * 0.05f;
				meshBuilder.Box(slot2, vec6, vec * 0.048f, vec5 * 0.004f, vec4 * 0.05f);
				meshBuilder.Box(slot4, vec6 + vec4 * 0.05f, vec * 0.048f, vec5 * 0.0045f, vec4 * 0.006f);
				break;
			}
			case "hammer":
				meshBuilder.Tube(slot, new Vec3(0f, 0f, -0.05f), new Vec3(0f, 0f, 0.235f), 0.0095f, 0.009f, 8);
				meshBuilder.Box(slot2, new Vec3(0f, 0.014f, 0.24f), 0.016f, 0.036f, 0.016f);
				meshBuilder.Box(slot4, new Vec3(0f, 0.052f, 0.24f), 0.018f, 0.004f, 0.018f);
				meshBuilder.Loft(slot3, new Vec3[3]
				{
					new Vec3(0f, -0.02f, 0.24f),
					new Vec3(0f, -0.05f, 0.232f),
					new Vec3(0f, -0.07f, 0.212f)
				}, new float[3] { 0.012f, 0.009f, 0.003f }, new float[3] { 0.01f, 0.007f, 0.003f }, vec, vec3, 5, smooth: false);
				break;
			case "basket":
			{
				meshBuilder.Loft(slot6, new Vec3[3]
				{
					new Vec3(0f, 0f, -0.2f),
					new Vec3(0f, 0f, -0.17f),
					new Vec3(0f, 0f, -0.1f)
				}, new float[3] { 0.06f, 0.08f, 0.09f }, new float[3] { 0.048f, 0.062f, 0.07f }, vec, vec2, 10, smooth: false);
				meshBuilder.Blob(slot7, new Vec3(0f, 0f, -0.1f), 0.075f, 0.058f, 0.022f);
				Vec3[] array2 = new Vec3[7];
				for (int k = 0; k < array2.Length; k++)
				{
					float num2 = MathF.PI * (float)k / (float)(array2.Length - 1);
					array2[k] = new Vec3((0f - (float)Math.Cos(num2)) * 0.088f, 0f, -0.1f + (float)Math.Sin(num2) * 0.105f);
				}
				for (int l = 0; l + 1 < array2.Length; l++)
				{
					meshBuilder.Tube(slot6, array2[l], array2[l + 1], 0.0065f, 0.0065f, 5);
				}
				break;
			}
			case "bucket":
			{
				meshBuilder.Loft(slot, new Vec3[2]
				{
					new Vec3(0f, 0f, -0.22f),
					new Vec3(0f, 0f, -0.1f)
				}, new float[2] { 0.056f, 0.068f }, new float[2] { 0.056f, 0.068f }, vec, vec2, 12, smooth: false);
				meshBuilder.Loft(slot3, new Vec3[2]
				{
					new Vec3(0f, 0f, -0.205f),
					new Vec3(0f, 0f, -0.195f)
				}, new float[2] { 0.06f, 0.061f }, new float[2] { 0.06f, 0.061f }, vec, vec2, 12, smooth: false);
				meshBuilder.Loft(slot3, new Vec3[2]
				{
					new Vec3(0f, 0f, -0.125f),
					new Vec3(0f, 0f, -0.115f)
				}, new float[2] { 0.068f, 0.069f }, new float[2] { 0.068f, 0.069f }, vec, vec2, 12, smooth: false);
				meshBuilder.Loft(slot8, new Vec3[2]
				{
					new Vec3(0f, 0f, -0.112f),
					new Vec3(0f, 0f, -0.108f)
				}, new float[2] { 0.064f, 0.064f }, new float[2] { 0.064f, 0.064f }, vec, vec2, 12, smooth: false);
				Vec3[] array = new Vec3[7];
				for (int i = 0; i < array.Length; i++)
				{
					float num = MathF.PI * (float)i / (float)(array.Length - 1);
					array[i] = new Vec3((0f - (float)Math.Cos(num)) * 0.068f, 0f, -0.1f + (float)Math.Sin(num) * 0.1f);
				}
				for (int j = 0; j + 1 < array.Length; j++)
				{
					meshBuilder.Tube(slot3, array[j], array[j + 1], 0.004f, 0.004f, 4);
				}
				break;
			}
			case "flag":
				meshBuilder.Tube(slot, new Vec3(0f, 0f, -0.32f), new Vec3(0f, 0f, 0.78f), 0.01f, 0.009f, 7);
				meshBuilder.Blob(slot4, new Vec3(0f, 0f, 0.79f), 0.016f, 0.016f, 0.016f, 7);
				meshBuilder.Extrude(slot5, new Vec3(0f, 0f, 0f), vec2, vec3, 0.004f, 0.008f, 0.5f, 0.27f, 0.52f, 0.235f, 0.625f, 0.27f, 0.74f, 0.008f, 0.76f);
				break;
			case "book":
			case "notebook":
				meshBuilder.Box(slot5, new Vec3(0f, 0.02f, 0.02f), 0.012f, 0.045f, 0.06f);
				meshBuilder.Box(slot9, new Vec3(0.003f, 0.022f, 0.02f), 0.011f, 0.041f, 0.056f);
				break;
			case "coffee_cup":
				meshBuilder.Loft(slot9, new Vec3[2]
				{
					new Vec3(0f, 0.03f, -0.01f),
					new Vec3(0f, 0.03f, 0.05f)
				}, new float[2] { 0.022f, 0.026f }, new float[2] { 0.022f, 0.026f }, vec, vec2, 8, smooth: true);
				break;
			default:
				return null;
			}
			return meshBuilder.Build();
		}
	}
}
