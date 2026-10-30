using System;
using System.Collections.Generic;
using System.Globalization;

namespace FirstPerson.Core
{
	public static class MutationLook
	{
		public enum Role
		{
			Citizen,
			King,
			Leader,
			Warrior
		}

		public struct Who
		{
			public string Skin;

			public bool Female;

			public bool Zombie;

			public bool Child;

			public Role Role;

			public int Head;

			public uint SkinColor;

			public uint Kingdom;

			public uint KingdomDark;
		}

		private sealed class Layout
		{
			public ulong Fem;

			public ulong Man;

			public ulong King;

			public ulong Leader;

			public ulong Warrior;

			public ulong Citizen;

			public ulong HairShort;

			public ulong HairLong;

			public ulong Beard;

			public int[] Spin;

			public int[] Bob;

			public int[] Split;

			public int[] Flick;

			public float[] SpinRate;

			public Vec3[] SpinAxis;

			public float Waddle;

			public int Torso = -1;

			public float BobAmount;

			public float SplitAmount;

			public int Skin = -1;

			public int SkinLight = -1;

			public int SkinDark = -1;

			public int SkinGlow = -1;

			public int SkinFlat = -1;

			public int Hair = -1;

			public int Kingdom = -1;

			public int KingdomDark = -1;

			public int KingdomGlow = -1;

			public int Eyes = -1;

			public int Head = -1;

			public float KingLift;

			public Vec3 Middle;

			public bool Egg;

			public bool Wobble;
		}

		public static readonly string[] Skins = new string[10] { "burger", "abomination", "blood_vortex", "energy", "fractal", "light_orb", "living_rock", "metalic_orb", "tentacle_horror", "void" };

		public static readonly string[] Eggs = new string[18]
		{
			"shell_plain", "shell_spotted", "colored", "roe", "face", "orb", "eyeball", "alien", "cocoon", "metal_box",
			"crystal", "ice", "blob", "candy", "bubble", "rainbow", "pumpkin", "flames"
		};

		private static readonly uint Ketchup = TerrainCache.Rgb(155, 19, 0);

		private static readonly uint Mustard = TerrainCache.Rgb(239, 203, 0);

		private static readonly Dictionary<string, uint[][]> Hairs = new Dictionary<string, uint[][]>();

		private static readonly Dictionary<MeshModel, Layout> Layouts = new Dictionary<MeshModel, Layout>();

		private static readonly uint ZombieSkin = TerrainCache.Rgb(104, 140, 62);

		public static float VisibleShare(string skin, bool child)
		{
			switch (skin)
			{
			case "abomination":
				if (!child)
				{
					return 0.8f;
				}
				return 0.636f;
			case "blood_vortex":
				if (!child)
				{
					return 0.533f;
				}
				return 0.636f;
			case "burger":
				if (!child)
				{
					return 0.467f;
				}
				return 0.455f;
			case "energy":
				if (!child)
				{
					return 0.867f;
				}
				return 0.636f;
			case "fractal":
				if (!child)
				{
					return 0.692f;
				}
				return 0.231f;
			case "light_orb":
				if (!child)
				{
					return 0.533f;
				}
				return 0.364f;
			case "living_rock":
				if (!child)
				{
					return 0.4f;
				}
				return 0.273f;
			case "metalic_orb":
				if (!child)
				{
					return 0.4f;
				}
				return 0.273f;
			case "tentacle_horror":
				if (!child)
				{
					return 0.8f;
				}
				return 0.636f;
			case "void":
				if (!child)
				{
					return 0.6f;
				}
				return 0.727f;
			default:
				return -1f;
			}
		}

		public static string SkinOf(string traitId)
		{
			if (traitId == null || !traitId.StartsWith("mutation_skin_", StringComparison.Ordinal))
			{
				return null;
			}
			string text = traitId.Substring(14);
			for (int i = 0; i < Skins.Length; i++)
			{
				if (Skins[i] == text)
				{
					return Skins[i];
				}
			}
			return null;
		}

		private static uint[] HairsOf(string skin, bool female)
		{
			if (!(skin == "burger"))
			{
				if (skin == "abomination")
				{
					if (female)
					{
						return new uint[6]
						{
							TerrainCache.Rgb(245, 195, 8),
							TerrainCache.Rgb(138, 35, 53),
							TerrainCache.Rgb(122, 76, 45),
							TerrainCache.Rgb(172, 125, 58),
							TerrainCache.Rgb(245, 195, 8),
							TerrainCache.Rgb(122, 76, 45)
						};
					}
					return new uint[6]
					{
						TerrainCache.Rgb(75, 46, 27),
						TerrainCache.Rgb(195, 128, 14),
						TerrainCache.Rgb(173, 54, 64),
						TerrainCache.Rgb(172, 125, 58),
						TerrainCache.Rgb(75, 46, 27),
						TerrainCache.Rgb(173, 54, 64)
					};
				}
				return null;
			}
			if (female)
			{
				return new uint[4] { Ketchup, Ketchup, Mustard, Ketchup };
			}
			return new uint[4] { Ketchup, Mustard, Mustard, Ketchup };
		}

		public static uint HairOf(string skin, bool female, int head)
		{
			if (skin == null)
			{
				return 0u;
			}
			uint[][] value;
			lock (Hairs)
			{
				if (!Hairs.TryGetValue(skin, out value))
				{
					Dictionary<string, uint[][]> hairs = Hairs;
					uint[][] obj = new uint[2][]
					{
						HairsOf(skin, female: false),
						HairsOf(skin, female: true)
					};
					value = obj;
					hairs[skin] = obj;
				}
			}
			uint[] array = value[female ? 1u : 0u];
			if (array == null || array.Length == 0)
			{
				return 0u;
			}
			return array[(head % array.Length + array.Length) % array.Length];
		}

		private static Layout LayoutOf(MeshModel m)
		{
			lock (Layouts)
			{
				if (Layouts.TryGetValue(m, out var value))
				{
					return value;
				}
				value = new Layout();
				List<int> list = new List<int>();
				List<float> list2 = new List<float>();
				List<Vec3> list3 = new List<Vec3>();
				List<int> list4 = new List<int>();
				List<int> list5 = new List<int>();
				List<int> list6 = new List<int>();
				for (int i = 0; i < m.Parts.Length; i++)
				{
					string text = m.Parts[i].Name ?? "";
					ulong num = (ulong)((i < 64) ? (1L << i) : 0);
					if (text.IndexOf("citizen", StringComparison.Ordinal) >= 0)
					{
						value.Citizen |= num;
					}
					if (text.StartsWith("fem_", StringComparison.Ordinal) || text.IndexOf("_fem_", StringComparison.Ordinal) >= 0)
					{
						value.Fem |= num;
					}
					else if (text.StartsWith("man_", StringComparison.Ordinal) || text.IndexOf("_man_", StringComparison.Ordinal) >= 0)
					{
						value.Man |= num;
					}
					else if (text.IndexOf("role_king", StringComparison.Ordinal) >= 0)
					{
						value.King |= num;
					}
					else if (text.IndexOf("role_leader", StringComparison.Ordinal) >= 0)
					{
						value.Leader |= num;
					}
					else if (text.IndexOf("role_warrior", StringComparison.Ordinal) >= 0)
					{
						value.Warrior |= num;
					}
					else
					{
						switch (text)
						{
						case "hair_short":
							value.HairShort |= num;
							break;
						case "hair_long":
							value.HairLong |= num;
							break;
						case "beard":
							value.Beard |= num;
							break;
						}
					}
					if (text.StartsWith("spin", StringComparison.Ordinal))
					{
						list.Add(i);
						list2.Add(m.InfoFloat("spin_" + text, m.InfoFloat("spin", 1.5f)));
						list3.Add(Axis(m.Info("axis_" + text)));
					}
					else if (text.StartsWith("bob", StringComparison.Ordinal))
					{
						list4.Add(i);
					}
					else if (text.StartsWith("split", StringComparison.Ordinal))
					{
						list5.Add(i);
					}
					else if (text.StartsWith("flick_", StringComparison.Ordinal))
					{
						list6.Add(i);
					}
				}
				value.Spin = list.ToArray();
				value.SpinRate = list2.ToArray();
				value.SpinAxis = list3.ToArray();
				value.Bob = list4.ToArray();
				value.Split = list5.ToArray();
				value.Flick = list6.ToArray();
				value.Waddle = m.InfoFloat("waddle", 0f);
				value.Torso = m.PartIndex("torso");
				value.BobAmount = m.InfoFloat("bob", 0.04f);
				value.SplitAmount = m.InfoFloat("split", 0.12f);
				value.Skin = m.SlotIndex("skin");
				value.SkinLight = m.SlotIndex("skin_light");
				value.SkinDark = m.SlotIndex("skin_dark");
				value.SkinGlow = m.SlotIndex("skin_glow");
				value.SkinFlat = m.SlotIndex("skin_flat");
				value.Head = m.PartIndex("head");
				value.KingLift = m.InfoFloat("king_lift", 0f);
				value.Hair = m.SlotIndex("hair");
				value.Kingdom = m.SlotIndex("kingdom");
				value.KingdomDark = m.SlotIndex("kingdom_dark");
				value.KingdomGlow = m.SlotIndex("kingdom_glow");
				value.Eyes = m.SlotIndex("eyes");
				value.Middle = new Vec3((m.Min.X + m.Max.X) * 0.5f, (m.Min.Y + m.Max.Y) * 0.5f, 0f);
				value.Egg = m.Info("kind") == "egg";
				value.Wobble = value.Egg;
				Layouts[m] = value;
				return value;
			}
		}

		private static Vec3 Axis(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return new Vec3(0f, 0f, 0f);
			}
			string[] array = text.Split(',');
			if (array.Length != 3)
			{
				return new Vec3(0f, 0f, 0f);
			}
			CultureInfo invariantCulture = CultureInfo.InvariantCulture;
			float.TryParse(array[0], NumberStyles.Float, invariantCulture, out var result);
			float.TryParse(array[1], NumberStyles.Float, invariantCulture, out var result2);
			float.TryParse(array[2], NumberStyles.Float, invariantCulture, out var result3);
			float num = (float)Math.Sqrt(result * result + result2 * result2 + result3 * result3);
			if (!(num < 1E-05f))
			{
				return new Vec3(result / num, result2 / num, result3 / num);
			}
			return new Vec3(0f, 0f, 0f);
		}

		public static bool Owns(MeshModel m)
		{
			if (m != null)
			{
				if (m.Info("mutation") == null)
				{
					return m.Info("kind") == "egg";
				}
				return true;
			}
			return false;
		}

		public static void Dress(MeshInstance inst, in Who who)
		{
			MeshModel model = inst.Model;
			if (model == null)
			{
				return;
			}
			Layout layout = LayoutOf(model);
			int num = model.SlotNames.Length;
			if (inst.Palette == null || inst.Palette.Length != num)
			{
				inst.Palette = new uint[num];
			}
			uint num2 = (who.Zombie ? ZombieSkin : who.SkinColor);
			if (num2 != 0)
			{
				uint num3 = num2 | 0xFF000000u;
				Put(inst, layout.Skin, num3);
				Put(inst, layout.SkinLight, TerrainCache.Lerp(num3, TerrainCache.Rgb(255, 250, 238), 0.3f));
				Put(inst, layout.SkinDark, TerrainCache.Scale(num3, 0.72f));
				Put(inst, layout.SkinGlow, TerrainCache.Lerp(num3, TerrainCache.Rgb(255, 255, 255), 0.35f));
				Put(inst, layout.SkinFlat, num3);
			}
			else
			{
				Put(inst, layout.Skin, 0u);
				Put(inst, layout.SkinLight, 0u);
				Put(inst, layout.SkinDark, 0u);
				Put(inst, layout.SkinGlow, 0u);
				Put(inst, layout.SkinFlat, 0u);
			}
			uint num4 = HairOf(who.Skin, who.Female, who.Head);
			Put(inst, layout.Hair, (num4 != 0) ? (num4 | 0xFF000000u) : 0u);
			Put(inst, layout.Eyes, 0u);
			if (who.Kingdom != 0)
			{
				Put(inst, layout.Kingdom, who.Kingdom | 0xFF000000u);
				Put(inst, layout.KingdomGlow, TerrainCache.Lerp(who.Kingdom | 0xFF000000u, TerrainCache.Rgb(255, 255, 255), 0.15f));
				Put(inst, layout.KingdomDark, ((who.KingdomDark != 0) ? who.KingdomDark : TerrainCache.Scale(who.Kingdom, 0.62f)) | 0xFF000000u);
			}
			else
			{
				Put(inst, layout.Kingdom, 0u);
				Put(inst, layout.KingdomGlow, 0u);
				Put(inst, layout.KingdomDark, 0u);
			}
			for (int i = 0; i < num; i++)
			{
				if (i != layout.Skin && i != layout.SkinLight && i != layout.SkinDark && i != layout.SkinGlow && i != layout.SkinFlat && i != layout.Hair && i != layout.Kingdom && i != layout.KingdomDark && i != layout.KingdomGlow)
				{
					inst.Palette[i] = 0u;
				}
			}
			if (who.Zombie)
			{
				for (int j = 0; j < num; j++)
				{
					if (j != layout.Skin && j != layout.SkinLight && j != layout.SkinDark && j != layout.SkinFlat)
					{
						uint c = ((inst.Palette[j] != 0) ? inst.Palette[j] : (model.SlotColor[j] | 0xFF000000u));
						inst.Palette[j] = Rotten(c, model.SlotFlags[j]);
					}
				}
			}
			ulong num5 = inst.Hidden & ~(layout.Fem | layout.Man | layout.King | layout.Leader | layout.Warrior | layout.Citizen | layout.HairShort | layout.HairLong | layout.Beard);
			num5 |= (who.Female ? layout.Man : layout.Fem);
			num5 |= layout.Beard;
			num5 |= (who.Female ? layout.HairShort : layout.HairLong);
			if (who.Role != Role.King || who.Child)
			{
				num5 |= layout.King;
			}
			if (who.Role != Role.Leader || who.Child)
			{
				num5 |= layout.Leader;
			}
			if (who.Role != Role.Warrior || who.Child)
			{
				num5 |= layout.Warrior;
			}
			if (who.Role != Role.Citizen && !who.Child)
			{
				num5 |= layout.Citizen;
			}
			inst.Hidden = num5;
		}

		private static void Put(MeshInstance inst, int slot, uint c)
		{
			if (slot >= 0 && slot < inst.Palette.Length)
			{
				inst.Palette[slot] = c;
			}
		}

		public static uint Rotten(uint c, byte flags)
		{
			int num = (int)(c & 0xFF);
			int num2 = (int)((c >> 8) & 0xFF);
			int num3 = (int)((c >> 16) & 0xFF);
			float num4 = (0.3f * (float)num + 0.59f * (float)num2 + 0.11f * (float)num3) / 255f;
			uint a = ((num > num2 + 40 && num > num3 + 30) ? TerrainCache.Rgb((int)(70f + 90f * num4), (int)(40f + 40f * num4), (int)(48f + 40f * num4)) : TerrainCache.Rgb((int)(36f + 120f * num4), (int)(46f + 140f * num4), (int)(34f + 100f * num4)));
			float t = (((flags & 1) != 0) ? 0.45f : 0.2f);
			return TerrainCache.Lerp(a, c | 0xFF000000u, t) | 0xFF000000u;
		}

		public static void Animate(MeshInstance inst, in Who who, float time, float phase, float stride, float seed)
		{
			MeshModel model = inst.Model;
			if (model == null)
			{
				return;
			}
			Layout layout = LayoutOf(model);
			bool flag = layout.KingLift > 0f && who.Role == Role.King && !who.Child && layout.Head >= 0;
			if (layout.Spin.Length == 0 && layout.Bob.Length == 0 && layout.Split.Length == 0 && layout.Flick.Length == 0 && !layout.Wobble && !flag && layout.Waddle <= 0f)
			{
				return;
			}
			inst.EnsurePose();
			if (flag)
			{
				inst.PartOffset[layout.Head] = inst.PartOffset[layout.Head] + new Vec3(0f, 0f, layout.KingLift);
			}
			for (int i = 0; i < layout.Spin.Length; i++)
			{
				int num = layout.Spin[i];
				float num2 = time * layout.SpinRate[i] + seed * 6.283f + (float)i * 1.7f;
				Vec3 axis = layout.SpinAxis[i];
				inst.PartRotation[num] = ((axis.X == 0f && axis.Y == 0f && axis.Z == 0f) ? Mat3.RotZ(num2) : Mat3.Rotation(axis, num2));
			}
			if (layout.Waddle > 0f && layout.Torso >= 0)
			{
				float a = layout.Waddle * (float)Math.Sin(phase) * stride + 0.02f * (float)Math.Sin(time * 1.1f + seed * 6.283f);
				inst.PartRotation[layout.Torso] = inst.PartRotation[layout.Torso] * Mat3.RotY(a);
			}
			for (int j = 0; j < layout.Bob.Length; j++)
			{
				int num3 = layout.Bob[j];
				inst.PartOffset[num3] = new Vec3(0f, 0f, layout.BobAmount * (float)Math.Sin(time * 2.2f + seed * 6.283f + (float)j * 2.1f));
			}
			if (layout.Split.Length != 0)
			{
				float num4 = layout.SplitAmount * (stride * Math.Abs((float)Math.Sin(phase)) + 0.12f * (0.5f + 0.5f * (float)Math.Sin(time * 1.3f + seed * 6.283f)));
				for (int k = 0; k < layout.Split.Length; k++)
				{
					int num5 = layout.Split[k];
					Vec3 pivot = model.Parts[num5].Pivot;
					Vec3 vec = new Vec3(pivot.X - layout.Middle.X, pivot.Y - layout.Middle.Y, pivot.Z - model.InfoFloat("split_z", pivot.Z));
					float num6 = (float)Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y + vec.Z * vec.Z);
					if (!(num6 < 0.0001f))
					{
						inst.PartOffset[num5] = vec * (num4 / num6);
					}
				}
			}
			if (layout.Flick.Length != 0)
			{
				int num7 = (int)Math.Floor(time * 11f + seed * 37f) % layout.Flick.Length;
				for (int l = 0; l < layout.Flick.Length; l++)
				{
					int num8 = layout.Flick[l];
					if (num8 < 64)
					{
						if (l == num7)
						{
							inst.Hidden &= (ulong)(~(1L << num8));
						}
						else
						{
							inst.Hidden |= (ulong)(1L << num8);
						}
					}
				}
			}
			if (layout.Wobble && model.Parts.Length != 0)
			{
				float num9 = (time * 0.31f + seed * 3.7f) % 1f;
				float a2 = ((num9 < 0.18f) ? ((float)Math.Sin((double)(num9 / 0.18f) * Math.PI * 3.0) * 0.12f * (1f - num9 / 0.18f)) : 0f);
				inst.PartRotation[0] = inst.PartRotation[0] * Mat3.RotY(a2);
			}
		}

		public static void LookAbout(MeshInstance inst, int head, float time, float seed)
		{
			if (head >= 0)
			{
				inst.EnsurePose();
				float num = time * 0.7f + seed * 9f;
				float a = 0.5f * (float)Math.Sin(num) * (float)Math.Abs(Math.Sin(num * 0.37f));
				float a2 = 0.25f * (float)Math.Sin(num * 1.3f + 1f);
				inst.PartRotation[head] = Mat3.RotZ(a) * Mat3.RotX(a2);
			}
		}
	}
}
