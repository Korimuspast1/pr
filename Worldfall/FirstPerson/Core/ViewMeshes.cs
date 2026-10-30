using System;
using System.Collections.Generic;
using System.Globalization;

namespace FirstPerson.Core
{
	public static class ViewMeshes
	{
		internal sealed class HeldModel
		{
			public readonly MeshModel[] Levels = new MeshModel[3];

			public uint String;

			public uint Glow;

			public bool StringGlow;

			public bool Lava;

			public Vec3 Tip;

			public Vec3 Head;

			public WeaponKind Kind;
		}

		public static readonly Vec3 CradleSeat = new Vec3(-0.0185f, 0.247f, 0f);

		public const float Forearm = 0.26f;

		public static readonly Vec3 Grip = new Vec3(-0.02f, 0.262f, 0f);

		public static readonly Vec3 Wrist = new Vec3(0f, 0.203f, 0f);

		public static readonly Vec3 FistCentre = new Vec3(-0.011f, 0.262f, 0f);

		private static readonly Dictionary<long, MeshModel> Arms = new Dictionary<long, MeshModel>();

		private static readonly Dictionary<long, MeshModel> Items = new Dictionary<long, MeshModel>();

		public static Func<string, MeshModel> Library;

		private static readonly Dictionary<long, MeshModel> Bows = new Dictionary<long, MeshModel>();

		private static readonly Dictionary<(int, int, bool, uint, uint, bool), MeshModel> Strings = new Dictionary<(int, int, bool, uint, uint, bool), MeshModel>();

		public static readonly string[] MetalVariants = MakeVariants();

		private static readonly HashSet<string> Variants = new HashSet<string>(MetalVariants);

		public static readonly string[] UniqueWeapons = new string[9] { "stick_wood", "flame_hammer", "ice_hammer", "necromancer_staff", "evil_staff", "white_staff", "plague_doctor_staff", "druid_staff", "alien_blaster" };

		private static readonly HashSet<string> Uniques = new HashSet<string>(UniqueWeapons);

		private static readonly HeldModel NoModel = new HeldModel();

		private static readonly Dictionary<string, HeldModel> HeldModels = new Dictionary<string, HeldModel>();

		private static Func<string, MeshModel> _heldLibrary;

		public const float BowGrip = 0.04f;

		public const float BowNock = 0.33f;

		private static readonly Dictionary<MeshModel, MeshModel[]> BentBows = new Dictionary<MeshModel, MeshModel[]>();

		public static Vec3 BowTip => BowTipAt(0f);

		private static void CradleFingers(MeshBuilder b, int sk, int skd, int knuck, int nail, float spread, float fine)
		{
			float[] array = new float[4]
			{
				0.0192f * spread,
				0.0064f * spread,
				-0.0064f * spread,
				-0.0186f * spread
			};
			float[] array2 = new float[4] { 0f, 0.0015f, 0f, -0.0045f };
			float[] array3 = new float[4] { 1f, 1.05f, 0.98f, 0.82f };
			float[] array4 = new float[4]
			{
				0.0068f * fine,
				0.007f * fine,
				0.0066f * fine,
				0.0058f * fine
			};
			for (int i = 0; i < 4; i++)
			{
				float num = array3[i];
				float num2 = array4[i];
				float fan = array[i] * 0.25f;
				Vec3 vec = new Vec3(-0.004f, 0.262f + array2[i], array[i]);
				Vec3 vec2 = vec + Bend(12f, fan) * (0.019f * num);
				Vec3 vec3 = vec2 + Bend(58f, fan) * (0.016f * num);
				Vec3 b2 = vec3 + Bend(100f, fan) * (0.012f * num);
				b.Blob(knuck, vec + new Vec3(0.004f, 0f, 0f), num2 * 1.1f, num2 * 1.05f, num2 * 1.02f);
				b.Capsule(sk, vec, vec2, num2 * 1.02f, num2 * 0.98f, 7);
				b.Capsule(skd, vec2, vec3, num2 * 0.95f, num2 * 0.9f, 7);
				b.Capsule(skd, vec3, b2, num2 * 0.88f, num2 * 0.8f);
			}
			Vec3 a = new Vec3(-0.006f, 0.214f, 0.012f);
			Vec3 vec4 = new Vec3(-0.012f, 0.228f, 0.029f);
			Vec3 vec5 = new Vec3(-0.022f, 0.24f, 0.0375f);
			Vec3 vec6 = new Vec3(-0.031f, 0.252f, 0.0355f);
			b.Capsule(sk, a, vec4, 0.0112f * fine, 0.0086f * fine, 7);
			b.Capsule(sk, vec4, vec5, 0.0082f * fine, 0.0077f * fine, 7);
			b.Capsule(skd, vec5, vec6, 0.0074f * fine, 0.0068f * fine, 7);
			Vec3 vec7 = (vec6 - vec5).Normalized();
			Vec3 vec8 = Vec3.Cross(vec7, new Vec3(1f, 0f, 0f)).Normalized();
			if (vec8.Z < 0f)
			{
				vec8 *= -1f;
			}
			b.Box(nail, vec6 - vec7 * 0.003f + vec8 * 0.0068f, vec7 * 0.0042f, Vec3.Cross(vec8, vec7) * 0.0038f, vec8 * 0.0012f);
		}

		private static Vec3 Bend(float degrees, float fan)
		{
			float num = degrees * MathF.PI / 180f;
			return new Vec3(0f - (float)Math.Sin(num), (float)Math.Cos(num), fan).Normalized();
		}

		public static MeshModel Arm(uint skin, uint sleeve, bool left, bool bare, bool holding = false, ArmLook look = ArmLook.Man, string armor = null, bool cradle = false)
		{
			if (bare)
			{
				look = ArmLook.Man;
				armor = null;
			}
			if (armor != null && look == ArmLook.WomanBell)
			{
				look = ArmLook.Woman;
			}
			long key = (long)(((ulong)skin << 32) ^ sleeve ^ (ulong)(left ? 4611686018427387904L : 0) ^ (ulong)(bare ? 2305843009213693952L : 0) ^ (ulong)(holding ? 1152921504606846976L : 0) ^ ((ulong)look << 56) ^ (ulong)((armor != null) ? ((long)ArmorMeshes.MetalOf(armor) + 1L << 48) : 0)) ^ (cradle ? 576460752303423488L : 0);
			if (Arms.TryGetValue(key, out var value))
			{
				return value;
			}
			if (Arms.Count > 64)
			{
				Arms.Clear();
			}
			MeshBuilder meshBuilder = new MeshBuilder(left ? "arm_left" : "arm_right")
			{
				Mirror = left
			};
			int num = meshBuilder.Slot("skin", 8, skin, 0);
			int num2 = meshBuilder.Slot("skin_shade", 8, TerrainCache.Scale(skin, 0.9f), 0);
			int num3 = meshBuilder.Slot("knuckle", 8, TerrainCache.Lerp(skin, TerrainCache.Rgb(236, 150, 130), 0.22f), 0);
			int num4 = meshBuilder.Slot("sleeve", 7, sleeve, 0);
			int num5 = meshBuilder.Slot("cuff", 7, TerrainCache.Scale(sleeve, 0.72f), 0);
			int num6 = meshBuilder.Slot("nail", 0, TerrainCache.Lerp(skin, 4293978344u, 0.4f), 0);
			meshBuilder.Part("arm", -1, new Vec3(0f, 0f, 0f));
			Vec3 vec = new Vec3(1f, 0f, 0f);
			Vec3 vec2 = new Vec3(0f, 0f, 1f);
			bool flag = look != ArmLook.Man;
			float num7 = (flag ? 0.84f : 1f);
			float num8 = (flag ? 0.86f : 1f);
			float num9 = (flag ? 0.9f : 1f);
			if (flag)
			{
				WomansForearm(meshBuilder, look, num, num4, num5, num7, vec, vec2);
			}
			else if (!bare)
			{
				meshBuilder.Loft(num4, new Vec3[5]
				{
					new Vec3(0f, -0.36f, 0f),
					new Vec3(0f, -0.06f, 0f),
					new Vec3(0f, 0.06f, 0f),
					new Vec3(0f, 0.15f, 0f),
					new Vec3(0f, 0.172f, 0f)
				}, new float[5] { 0.036f, 0.033f, 0.031f, 0.028f, 0.027f }, new float[5] { 0.033f, 0.03f, 0.029f, 0.026f, 0.025f }, vec2, vec, 10, smooth: true);
				meshBuilder.Loft(num5, new Vec3[3]
				{
					new Vec3(0f, 0.166f, 0f),
					new Vec3(0f, 0.176f, 0f),
					new Vec3(0f, 0.19f, 0f)
				}, new float[3] { 0.0285f, 0.029f, 0.0275f }, new float[3] { 0.0265f, 0.027f, 0.0255f }, vec2, vec, 10, smooth: true);
				meshBuilder.Loft(num, new Vec3[2]
				{
					new Vec3(0f, 0.175f, 0f),
					new Vec3(0f, 0.207f, 0f)
				}, new float[2] { 0.0205f, 0.0185f }, new float[2] { 0.016f, 0.0145f }, vec2, vec, 8, smooth: true);
			}
			else
			{
				meshBuilder.Loft(num, new Vec3[4]
				{
					new Vec3(0f, -0.36f, 0f),
					new Vec3(0f, -0.06f, 0f),
					new Vec3(0f, 0.08f, 0f),
					new Vec3(0f, 0.207f, 0f)
				}, new float[4] { 0.04f, 0.033f, 0.029f, 0.0195f }, new float[4] { 0.036f, 0.029f, 0.025f, 0.015f }, vec2, vec, 10, smooth: true);
			}
			if (armor != null)
			{
				ArmorMeshes.FirstPersonBracer(meshBuilder, armor, flag);
			}
			meshBuilder.Part("hand", 0, Wrist);
			float num10 = (flag ? 0.9f : 1f);
			meshBuilder.Loft(num, new Vec3[5]
			{
				new Vec3(0f, 0.2f, 0f),
				new Vec3(-0.002f, 0.216f, 0.001f),
				new Vec3(-0.003f, 0.238f, 0.001f),
				new Vec3(-0.001f, 0.258f, 0f),
				new Vec3(0.001f, 0.268f, -0.001f)
			}, new float[5]
			{
				0.019f * num7,
				0.0235f * num10,
				0.0265f * num10,
				0.0262f * num10,
				0.0215f * num10
			}, new float[5]
			{
				0.0148f * num7,
				0.0172f * num10,
				0.0175f * num10,
				0.0152f * num10,
				0.0105f * num10
			}, vec2, vec, 10, smooth: true);
			if (cradle)
			{
				CradleFingers(meshBuilder, num, num2, num3, num6, num9, num8);
				value = meshBuilder.Build();
				Arms[key] = value;
				return value;
			}
			float[] array = new float[4]
			{
				0.0192f * num9,
				0.0064f * num9,
				-0.0064f * num9,
				-0.0186f * num9
			};
			float[] array2 = new float[4] { 0f, 0.0015f, 0f, -0.0045f };
			float[] array3 = new float[4] { 1f, 1.05f, 0.98f, 0.82f };
			float[] array4 = new float[4]
			{
				0.0068f * num8,
				0.007f * num8,
				0.0066f * num8,
				0.0058f * num8
			};
			Vec3 vec3 = (holding ? Grip : new Vec3(-0.0135f, 0.263f, 0f));
			float num11 = (holding ? 0.0195f : 0.0135f);
			float[] array5 = ((!holding) ? new float[4] { 0f, 92f, 182f, 236f } : new float[4] { 8f, 100f, 172f, 218f });
			for (int i = 0; i < 4; i++)
			{
				float R = num11 * array3[i];
				Vec3 centre = new Vec3(vec3.X + num11 - R, vec3.Y + array2[i], array[i]);
				Vec3 vec4 = J(array5[0]);
				Vec3 vec5 = J(array5[1]);
				Vec3 vec6 = J(array5[2]);
				Vec3 b = J(array5[3]);
				float num12 = array4[i];
				meshBuilder.Blob(num3, vec4 + new Vec3(0.002f, 0f, 0f), num12 * 1.1f, num12 * 1.05f, num12 * 1.02f);
				meshBuilder.Capsule(num, vec4, vec5, num12 * 1.02f, num12 * 0.98f, 7);
				meshBuilder.Capsule(num2, vec5, vec6, num12 * 0.95f, num12 * 0.9f, 7);
				meshBuilder.Capsule(num2, vec6, b, num12 * 0.88f, num12 * 0.8f);
				Vec3 J(float deg)
				{
					float num13 = deg * MathF.PI / 180f;
					return new Vec3(centre.X + R * (float)Math.Cos(num13), centre.Y + R * (float)Math.Sin(num13), centre.Z);
				}
			}
			Vec3 a = new Vec3(-0.006f, 0.214f, 0.011f);
			Vec3 vec7 = (holding ? new Vec3(-0.019f, 0.238f, 0.025f) : new Vec3(-0.015f, 0.24f, 0.023f));
			Vec3 vec8 = (holding ? new Vec3(-0.033f, 0.254f, 0.027f) : new Vec3(-0.026f, 0.26f, 0.022f));
			Vec3 vec9 = (holding ? new Vec3(-0.04f, 0.268f, 0.019f) : new Vec3(-0.031f, 0.27f, 0.008f));
			meshBuilder.Capsule(num, a, vec7, 0.0112f * num8, 0.0086f * num8, 7);
			meshBuilder.Capsule(num, vec7, vec8, 0.0082f * num8, 0.0077f * num8, 7);
			meshBuilder.Capsule(num2, vec8, vec9, 0.0074f * num8, 0.0068f * num8, 7);
			Vec3 vec10 = (vec9 - vec8).Normalized();
			Vec3 vec11 = Vec3.Cross(vec10, vec).Normalized();
			if (vec11.Z < 0f)
			{
				vec11 *= -1f;
			}
			meshBuilder.Box(num6, vec9 - vec10 * 0.003f + vec11 * 0.0068f, vec10 * 0.0042f, Vec3.Cross(vec11, vec10) * 0.0038f, vec11 * 0.0012f);
			value = meshBuilder.Build();
			Arms[key] = value;
			return value;
		}

		private static void WomansForearm(MeshBuilder b, ArmLook look, int sk, int sl, int cuff, float slim, Vec3 X, Vec3 Z)
		{
			if (look != ArmLook.WomanBell)
			{
				if (look - 3 <= ArmLook.Woman)
				{
					if (look == ArmLook.WomanBare)
					{
						b.Loft(sl, new Vec3[3]
						{
							new Vec3(0f, -0.36f, 0f),
							new Vec3(0f, -0.08f, 0f),
							new Vec3(0f, -0.035f, 0f)
						}, new float[3] { 0.034f, 0.031f, 0.0305f }, new float[3] { 0.031f, 0.028f, 0.0275f }, Z, X, 10, smooth: true);
						b.Loft(cuff, new Vec3[2]
						{
							new Vec3(0f, -0.045f, 0f),
							new Vec3(0f, -0.03f, 0f)
						}, new float[2] { 0.032f, 0.0315f }, new float[2] { 0.029f, 0.0285f }, Z, X, 10, smooth: true);
					}
					b.Loft(sk, new Vec3[4]
					{
						new Vec3(0f, -0.36f, 0f),
						new Vec3(0f, -0.06f, 0f),
						new Vec3(0f, 0.08f, 0f),
						new Vec3(0f, 0.207f, 0f)
					}, new float[4] { 0.034f, 0.028f, 0.025f, 0.0168f }, new float[4] { 0.031f, 0.025f, 0.021f, 0.0128f }, Z, X, 10, smooth: true);
					if (look == ArmLook.WomanBracer)
					{
						int slot = b.Slot("bracer", 0, TerrainCache.Rgb(112, 76, 46), 0);
						int slot2 = b.Slot("lace", 0, TerrainCache.Rgb(70, 46, 28), 0);
						b.Loft(slot, new Vec3[3]
						{
							new Vec3(0f, 0.085f, 0f),
							new Vec3(0f, 0.14f, 0f),
							new Vec3(0f, 0.188f, 0f)
						}, new float[3] { 0.0262f, 0.0245f, 0.0218f }, new float[3] { 0.0225f, 0.0208f, 0.0185f }, Z, X, 10, smooth: true);
						for (int i = 0; i < 3; i++)
						{
							b.Box(slot2, new Vec3(0.0218f - (float)i * 0.0012f, 0.1f + (float)i * 0.03f, 0f), 0.0024f, 0.0035f, 0.012f);
						}
					}
				}
				else
				{
					b.Loft(sl, new Vec3[5]
					{
						new Vec3(0f, -0.36f, 0f),
						new Vec3(0f, -0.06f, 0f),
						new Vec3(0f, 0.06f, 0f),
						new Vec3(0f, 0.15f, 0f),
						new Vec3(0f, 0.172f, 0f)
					}, new float[5] { 0.033f, 0.03f, 0.0275f, 0.0245f, 0.0238f }, new float[5] { 0.0305f, 0.0275f, 0.0255f, 0.0228f, 0.022f }, Z, X, 10, smooth: true);
					b.Loft(cuff, new Vec3[3]
					{
						new Vec3(0f, 0.166f, 0f),
						new Vec3(0f, 0.176f, 0f),
						new Vec3(0f, 0.19f, 0f)
					}, new float[3] { 0.0252f, 0.0258f, 0.0245f }, new float[3] { 0.0234f, 0.024f, 0.0228f }, Z, X, 10, smooth: true);
					b.Loft(sk, new Vec3[2]
					{
						new Vec3(0f, 0.175f, 0f),
						new Vec3(0f, 0.207f, 0f)
					}, new float[2]
					{
						0.0205f * slim,
						0.0185f * slim
					}, new float[2]
					{
						0.016f * slim,
						0.0145f * slim
					}, Z, X, 8, smooth: true);
				}
			}
			else
			{
				b.Loft(sl, new Vec3[5]
				{
					new Vec3(0f, -0.36f, 0f),
					new Vec3(0f, -0.06f, 0f),
					new Vec3(0f, 0.06f, 0f),
					new Vec3(0f, 0.125f, 0f),
					new Vec3(0f, 0.16f, 0f)
				}, new float[5] { 0.033f, 0.03f, 0.031f, 0.0365f, 0.0415f }, new float[5] { 0.0305f, 0.0275f, 0.0285f, 0.0335f, 0.038f }, Z, X, 12, smooth: true);
				b.Loft(cuff, new Vec3[3]
				{
					new Vec3(0f, 0.152f, 0f),
					new Vec3(0f, 0.16f, 0f),
					new Vec3(0f, 0.17f, 0f)
				}, new float[3] { 0.0418f, 0.0436f, 0.0428f }, new float[3] { 0.0384f, 0.0402f, 0.0394f }, Z, X, 12, smooth: true);
				b.Loft(sk, new Vec3[2]
				{
					new Vec3(0f, 0.1f, 0f),
					new Vec3(0f, 0.207f, 0f)
				}, new float[2]
				{
					0.021f * slim,
					0.0185f * slim
				}, new float[2]
				{
					0.017f * slim,
					0.0145f * slim
				}, Z, X, 8, smooth: true);
			}
		}

		public static MeshModel Item(WeaponLook look, int detail = 0)
		{
			if (look.Model != null)
			{
				MeshModel meshModel = OwnModel(look.Model, detail);
				if (meshModel != null)
				{
					return meshModel;
				}
			}
			long key = (long)(((ulong)look.Kind << 56) ^ ((ulong)look.Head << 24) ^ look.Handle ^ ((ulong)look.Accent << 8) ^ (ulong)(look.Flame ? 36028797018963968L : 0) ^ (ulong)(look.Ice ? 18014398509481984L : 0)) ^ (look.Glow ? 9007199254740992L : 0);
			if (Items.TryGetValue(key, out var value))
			{
				return value;
			}
			if (Items.Count > 64)
			{
				Items.Clear();
			}
			MeshBuilder meshBuilder = new MeshBuilder("held_" + look.Kind);
			int slot = meshBuilder.Slot("head", (byte)(look.Ice ? 12 : (look.Flame ? 11 : 10)), look.Head, (byte)((look.Flame || look.Glow) ? 1u : 0u));
			int slot2 = meshBuilder.Slot("head_dark", 10, TerrainCache.Scale(look.Head, 0.72f), 0);
			int slot3 = meshBuilder.Slot("edge", 10, TerrainCache.Lerp(look.Head, uint.MaxValue, 0.35f), look.Flame ? ((byte)1) : ((byte)0));
			int num = meshBuilder.Slot("handle", 3, look.Handle, 0);
			int num2 = meshBuilder.Slot("wrap", 7, TerrainCache.Scale(look.Handle, 0.7f), 0);
			int num3 = meshBuilder.Slot("accent", (byte)(look.Glow ? 12 : 10), look.Accent, look.Glow ? ((byte)1) : ((byte)0));
			meshBuilder.Part("item", -1, new Vec3(0f, 0f, 0f));
			Vec3 ax = new Vec3(1f, 0f, 0f);
			Vec3 vec = new Vec3(0f, 1f, 0f);
			Vec3 v = new Vec3(0f, 0f, 1f);
			switch (look.Kind)
			{
			case WeaponKind.Sword:
			case WeaponKind.Dagger:
			{
				float num5 = ((look.Kind == WeaponKind.Dagger) ? 0.6f : 1f);
				meshBuilder.Tube(num2, new Vec3(0f, 0f, -0.04f), new Vec3(0f, 0f, 0.052f), 0.0105f, 0.0098f, 8);
				meshBuilder.Blob(slot2, new Vec3(0f, 0f, -0.048f), 0.014f, 0.014f, 0.012f);
				float num6 = 0.062f * num5 + 0.012f;
				meshBuilder.Capsule(slot2, new Vec3(0f, 0f - num6, 0.061f), new Vec3(0f, num6, 0.061f), 0.0085f, 0.0085f);
				meshBuilder.Blob(slot2, new Vec3(0f, 0f, 0.064f), 0.012f, 0.016f, 0.011f);
				float num7 = 0.073f;
				float num8 = 0.44f * num5;
				meshBuilder.Loft(slot, new Vec3[3]
				{
					new Vec3(0f, 0f, num7),
					new Vec3(0f, 0f, num7 + num8 * 0.82f),
					new Vec3(0f, 0f, num7 + num8)
				}, new float[3] { 0.0065f, 0.0055f, 0f }, new float[3] { 0.024f, 0.019f, 0f }, ax, vec, 4, smooth: false);
				meshBuilder.Box(slot2, new Vec3(0f, 0f, num7 + num8 * 0.4f), 0.0068f, 0.004f, num8 * 0.36f);
				break;
			}
			case WeaponKind.Axe:
				meshBuilder.Tube(num, new Vec3(0f, 0f, -0.07f), new Vec3(0f, 0f, 0.4f), 0.0115f, 0.0105f, 8);
				meshBuilder.Blob(num, new Vec3(0f, 0f, -0.072f), 0.013f, 0.013f, 0.01f);
				meshBuilder.Box(slot2, new Vec3(0f, 0.002f, 0.355f), 0.017f, 0.024f, 0.035f);
				meshBuilder.Extrude(slot, new Vec3(0f, 0f, 0f), vec, v, 0.008f, 0.02f, 0.33f, 0.09f, 0.29f, 0.125f, 0.305f, 0.135f, 0.36f, 0.125f, 0.415f, 0.09f, 0.43f, 0.02f, 0.385f);
				meshBuilder.Box(slot3, new Vec3(0f, 0.13f, 0.36f), 0.0085f, 0.006f, 0.05f);
				meshBuilder.Box(slot2, new Vec3(0f, -0.035f, 0.355f), 0.014f, 0.016f, 0.02f);
				break;
			case WeaponKind.Spear:
				meshBuilder.Tube(num, new Vec3(0f, 0f, -0.55f), new Vec3(0f, 0f, 0.52f), 0.0105f, 0.0098f, 8);
				meshBuilder.Tube(slot2, new Vec3(0f, 0f, 0.5f), new Vec3(0f, 0f, 0.54f), 0.014f, 0.012f, 6);
				meshBuilder.Loft(slot, new Vec3[3]
				{
					new Vec3(0f, 0f, 0.535f),
					new Vec3(0f, 0f, 0.585f),
					new Vec3(0f, 0f, 0.68f)
				}, new float[3] { 0.007f, 0.007f, 0f }, new float[3] { 0.014f, 0.03f, 0f }, ax, vec, 4, smooth: false);
				break;
			case WeaponKind.Hammer:
				meshBuilder.Tube(num, new Vec3(0f, 0f, -0.06f), new Vec3(0f, 0f, 0.36f), 0.0118f, 0.011f, 8);
				meshBuilder.Blob(num, new Vec3(0f, 0f, -0.062f), 0.013f, 0.013f, 0.01f);
				meshBuilder.Box(slot, new Vec3(0f, 0.012f, 0.375f), 0.036f, 0.07f, 0.042f);
				meshBuilder.Box(slot2, new Vec3(0f, 0.087f, 0.375f), 0.03f, 0.006f, 0.036f);
				meshBuilder.Box(slot2, new Vec3(0f, -0.063f, 0.375f), 0.03f, 0.006f, 0.036f);
				meshBuilder.Box(slot2, new Vec3(0f, 0.012f, 0.42f), 0.02f, 0.03f, 0.006f);
				break;
			case WeaponKind.Stick:
				meshBuilder.Tube(num, new Vec3(0f, 0f, -0.06f), new Vec3(0f, 0f, 0.43f), 0.0135f, 0.0105f, 7);
				meshBuilder.Box(num, new Vec3(0f, 0.012f, 0.25f), 0.006f, 0.014f, 0.006f);
				break;
			case WeaponKind.Staff:
			{
				meshBuilder.Tube(num, new Vec3(0f, 0f, -0.45f), new Vec3(0f, 0f, 0.4f), 0.0105f, 0.0115f, 8);
				for (int i = 0; i < 3; i++)
				{
					float num4 = (float)i * 2.094f;
					Vec3 vec2 = new Vec3((float)Math.Cos(num4) * 0.026f, (float)Math.Sin(num4) * 0.026f, 0f);
					meshBuilder.Tube(slot2, new Vec3(0f, 0f, 0.39f), new Vec3(vec2.X, vec2.Y, 0.46f), 0.006f, 0.004f, 4);
				}
				meshBuilder.Loft(num3, new Vec3[5]
				{
					new Vec3(0f, 0f, 0.415f),
					new Vec3(0f, 0f, 0.432f),
					new Vec3(0f, 0f, 0.46f),
					new Vec3(0f, 0f, 0.488f),
					new Vec3(0f, 0f, 0.505f)
				}, new float[5] { 0f, 0.025f, 0.034f, 0.025f, 0f }, new float[5] { 0f, 0.025f, 0.034f, 0.025f, 0f }, ax, vec, 8, smooth: true);
				break;
			}
			case WeaponKind.Gun:
				meshBuilder.Box(num, new Vec3(0f, -0.004f, -0.002f), new Vec3(0.0115f, 0f, 0f), new Vec3(0f, 0.016f, 0.004f), new Vec3(0f, -0.006f, 0.04f));
				meshBuilder.Box(slot, new Vec3(0f, 0.03f, 0.054f), 0.017f, 0.07f, 0.02f);
				meshBuilder.Tube(slot2, new Vec3(0f, 0.095f, 0.056f), new Vec3(0f, 0.17f, 0.056f), 0.011f, 0.011f, 8);
				meshBuilder.Tube(num3, new Vec3(0f, 0.168f, 0.056f), new Vec3(0f, 0.176f, 0.056f), 0.0135f, 0.0135f, 8);
				meshBuilder.Box(num3, new Vec3(0f, 0.02f, 0.077f), 0.007f, 0.05f, 0.004f);
				meshBuilder.Box(slot2, new Vec3(0f, 0f, 0.028f), 0.006f, 0.012f, 0.006f);
				break;
			case WeaponKind.Bow:
				BowLimbs(meshBuilder, num, num2, num3, 0f);
				break;
			case WeaponKind.Shotgun:
			{
				int slot4 = meshBuilder.Slot("bone", 10, TerrainCache.Rgb(216, 192, 180), 0);
				int slot5 = meshBuilder.Slot("crimson", 3, TerrainCache.Rgb(150, 72, 78), 0);
				meshBuilder.Tube(slot4, new Vec3(0f, 0.09f, 0.0215f), new Vec3(0f, 0.502f, 0.0215f), 0.01f, 0.01f, 10);
				meshBuilder.Tube(slot4, new Vec3(0f, 0.09f, -0.0005f), new Vec3(0f, 0.39f, -0.0005f), 0.0095f, 0.0095f, 8);
				meshBuilder.Box(slot4, new Vec3(0f, 0.055f, 0.006f), 0.0128f, 0.043f, 0.0225f);
				meshBuilder.Box(slot5, new Vec3(0f, 0.212f, 0f), 0.015f, 0.071f, 0.0165f);
				meshBuilder.Box(slot5, new Vec3(0f, -0.05f, -0.012f), 0.012f, 0.065f, 0.0225f);
				meshBuilder.Box(slot2, new Vec3(0f, 0.055f, -0.028f), 0.003f, 0.025f, 0.006f);
				break;
			}
			}
			value = meshBuilder.Build();
			Items[key] = value;
			return value;
		}

		public static float BowCurve(float t, float flex = 0f)
		{
			return -0.085f * (1f + 0.55f * flex) * t * t;
		}

		public static Vec3 BowTipAt(float flex)
		{
			return new Vec3(0f, BowCurve(1f, flex), 0.33f * (1f - 0.045f * flex));
		}

		public static MeshModel Bow(WeaponLook look, float flex, int detail = 0)
		{
			int num = Math.Max(0, Math.Min(8, (int)Math.Round(flex * 8f)));
			if (look.Model != null)
			{
				MeshModel meshModel = OwnModel(look.Model, detail);
				if (meshModel != null)
				{
					return Bent(meshModel, num);
				}
			}
			long key = (long)(((ulong)look.Head << 24) ^ look.Handle ^ ((ulong)look.Accent << 8)) ^ ((long)num << 58);
			if (Bows.TryGetValue(key, out var value))
			{
				return value;
			}
			if (Bows.Count > 64)
			{
				Bows.Clear();
			}
			MeshBuilder meshBuilder = new MeshBuilder("held_bow");
			meshBuilder.Slot("head", 10, look.Head, 0);
			meshBuilder.Slot("head_dark", 10, TerrainCache.Scale(look.Head, 0.72f), 0);
			meshBuilder.Slot("edge", 10, TerrainCache.Lerp(look.Head, uint.MaxValue, 0.35f), 0);
			int wood = meshBuilder.Slot("handle", 3, look.Handle, 0);
			int wrap = meshBuilder.Slot("wrap", 7, TerrainCache.Scale(look.Handle, 0.7f), 0);
			int accent = meshBuilder.Slot("accent", 10, look.Accent, 0);
			meshBuilder.Part("item", -1, new Vec3(0f, 0f, 0f));
			BowLimbs(meshBuilder, wood, wrap, accent, (float)num / 8f);
			value = meshBuilder.Build();
			Bows[key] = value;
			return value;
		}

		private static void BowLimbs(MeshBuilder b, int wood, int wrap, int accent, float flex)
		{
			Vec3 ax = new Vec3(1f, 0f, 0f);
			Vec3 ay = new Vec3(0f, 1f, 0f);
			Vec3 vec = BowTipAt(flex);
			for (int i = -1; i <= 1; i += 2)
			{
				Vec3[] array = new Vec3[7];
				float[] array2 = new float[7];
				float[] array3 = new float[7];
				for (int j = 0; j <= 6; j++)
				{
					float num = (float)j / 6f;
					array[j] = new Vec3(0f, BowCurve(num, flex), (float)i * (0.04f + (vec.Z - 0.04f) * num));
					array2[j] = 0.013f - 0.0068f * num;
					array3[j] = 0.0095f - 0.0045f * num;
				}
				if (i < 0)
				{
					Array.Reverse(array);
					Array.Reverse(array2);
					Array.Reverse(array3);
				}
				b.Loft(wood, array, array2, array3, ax, ay, 4, smooth: false, 0.785f);
			}
			b.Tube(wrap, new Vec3(0f, 0f, -0.045f), new Vec3(0f, 0f, 0.045f), 0.0135f, 0.0135f, 8);
			b.Box(accent, new Vec3(0f, vec.Y, vec.Z), 0.006f, 0.006f, 0.009f);
			b.Box(accent, new Vec3(0f, vec.Y, 0f - vec.Z), 0.006f, 0.006f, 0.009f);
		}

		public static MeshModel BowString(float nockY, float flex, bool arrow, uint headColor, uint stringColor = 0u, bool stringGlow = false)
		{
			int num = (int)Math.Round(nockY * 400f);
			int num2 = (int)Math.Round(flex * 8f);
			(int, int, bool, uint, uint, bool) key = (num, num2, arrow, headColor, stringColor, stringGlow);
			if (Strings.TryGetValue(key, out var value))
			{
				return value;
			}
			if (Strings.Count > 400)
			{
				Strings.Clear();
			}
			MeshBuilder meshBuilder = new MeshBuilder("bow_string");
			int slot = meshBuilder.Slot("string", 0, (stringColor != 0) ? (stringColor | 0xFF000000u) : TerrainCache.Rgb(232, 226, 210), stringGlow ? ((byte)1) : ((byte)0));
			int slot2 = meshBuilder.Slot("shaft", 3, TerrainCache.Rgb(176, 136, 86), 0);
			int slot3 = meshBuilder.Slot("tip", 10, headColor, 0);
			int slot4 = meshBuilder.Slot("fletch", 7, TerrainCache.Rgb(226, 222, 214), 0);
			meshBuilder.Part("string", -1, new Vec3(0f, 0f, 0f));
			Vec3 a = BowTipAt((float)num2 / 8f);
			Vec3 b = new Vec3(0f, a.Y, 0f - a.Z);
			Vec3 vec = new Vec3(0f, (float)num / 400f, 0f);
			meshBuilder.Tube(slot, a, vec, 0.0022f, 0.0022f, 3, smooth: false);
			meshBuilder.Tube(slot, vec, b, 0.0022f, 0.0022f, 3, smooth: false);
			if (arrow)
			{
				float num3 = vec.Y + 0.43f;
				meshBuilder.Tube(slot2, vec, new Vec3(0f, num3, 0f), 0.0035f, 0.0035f, 4, smooth: false);
				meshBuilder.Loft(slot3, new Vec3[2]
				{
					new Vec3(0f, num3, 0f),
					new Vec3(0f, num3 + 0.04f, 0f)
				}, new float[2] { 0.007f, 0f }, new float[2] { 0.007f, 0f }, new Vec3(0f, 0f, 1f), new Vec3(1f, 0f, 0f), 4, smooth: false);
				meshBuilder.Extrude(slot4, vec, new Vec3(0f, 1f, 0f), new Vec3(0f, 0f, 1f), 0.0015f, 0.005f, -0.012f, 0.05f, -0.004f, 0.05f, 0.004f, 0.005f, 0.012f);
			}
			MeshModel meshModel = meshBuilder.Build();
			Strings[key] = meshModel;
			return meshModel;
		}

		public static WeaponKind KindOf(string id)
		{
			id = id ?? "";
			if (id.Contains("bow"))
			{
				return WeaponKind.Bow;
			}
			if (id.Contains("shotgun"))
			{
				return WeaponKind.Shotgun;
			}
			if (id.Contains("blaster") || id.Contains("gun"))
			{
				return WeaponKind.Gun;
			}
			if (id.Contains("staff"))
			{
				return WeaponKind.Staff;
			}
			if (id.Contains("dagger") || id.Contains("knife"))
			{
				return WeaponKind.Dagger;
			}
			if (id.Contains("sword"))
			{
				return WeaponKind.Sword;
			}
			if (id.Contains("pickaxe"))
			{
				return WeaponKind.Axe;
			}
			if (id.Contains("axe"))
			{
				return WeaponKind.Axe;
			}
			if (id.Contains("spear") || id.Contains("pike") || id.Contains("trident"))
			{
				return WeaponKind.Spear;
			}
			if (id.Contains("hammer") || id.Contains("mace"))
			{
				return WeaponKind.Hammer;
			}
			if (id.Contains("stick") || id.Contains("club") || id.Contains("branch"))
			{
				return WeaponKind.Stick;
			}
			return WeaponKind.None;
		}

		public static WeaponLook LookOf(string id, SpriteImage icon)
		{
			id = id ?? "";
			WeaponLook look = new WeaponLook
			{
				Kind = KindOf(id)
			};
			look.Flame = id.Contains("flame") || id.Contains("fire");
			if (FlameSword.IsItem(id))
			{
				look.Model = "held_flame_sword";
			}
			else if (HasOwnModel(id))
			{
				look.Model = "held_" + id;
			}
			look.Ice = id.Contains("ice") || id.Contains("frost");
			look.Glow = look.Kind == WeaponKind.Staff || id.Contains("alien") || id.Contains("crystal");
			look.Head = MaterialColor(id);
			look.Handle = ((id.Contains("wood") || look.Kind == WeaponKind.Stick) ? TerrainCache.Rgb(150, 104, 60) : TerrainCache.Rgb(106, 72, 44));
			look.Accent = TerrainCache.Rgb(214, 180, 90);
			if (icon != null && !IsMetalVariant(id))
			{
				FromIcon(icon, ref look);
			}
			if (look.Flame)
			{
				look.Head = TerrainCache.Rgb(255, 150, 40);
				look.Accent = TerrainCache.Rgb(255, 220, 90);
			}
			if (look.Ice)
			{
				look.Head = TerrainCache.Rgb(150, 220, 255);
			}
			if (id.Contains("necromancer"))
			{
				look.Accent = TerrainCache.Rgb(120, 255, 140);
			}
			else if (id.Contains("evil"))
			{
				look.Accent = TerrainCache.Rgb(255, 60, 60);
			}
			else if (id.Contains("white"))
			{
				look.Accent = TerrainCache.Rgb(220, 240, 255);
			}
			else if (look.Kind == WeaponKind.Staff)
			{
				look.Accent = TerrainCache.Rgb(150, 200, 255);
			}
			return look;
		}

		private static uint MaterialColor(string id)
		{
			if (id.Contains("adamantine"))
			{
				return TerrainCache.Rgb(52, 50, 56);
			}
			if (id.Contains("mythril"))
			{
				return TerrainCache.Rgb(46, 94, 170);
			}
			if (id.Contains("steel"))
			{
				return TerrainCache.Rgb(172, 180, 192);
			}
			if (id.Contains("iron"))
			{
				return TerrainCache.Rgb(118, 120, 126);
			}
			if (id.Contains("silver"))
			{
				return TerrainCache.Rgb(204, 208, 216);
			}
			if (id.Contains("bronze"))
			{
				return TerrainCache.Rgb(184, 138, 64);
			}
			if (id.Contains("copper"))
			{
				return TerrainCache.Rgb(190, 108, 62);
			}
			if (id.Contains("stone"))
			{
				return TerrainCache.Rgb(140, 136, 130);
			}
			if (id.Contains("gold"))
			{
				return TerrainCache.Rgb(240, 200, 80);
			}
			if (id.Contains("wood"))
			{
				return TerrainCache.Rgb(166, 118, 70);
			}
			return TerrainCache.Rgb(168, 172, 180);
		}

		private static void FromIcon(SpriteImage icon, ref WeaponLook look)
		{
			Dictionary<uint, int> dictionary = new Dictionary<uint, int>();
			uint[] pixels = icon.Pixels;
			foreach (uint num in pixels)
			{
				if (num >> 24 >= 128)
				{
					uint key = (num & 0xF0F0F0) | 0xFF000000u;
					dictionary.TryGetValue(key, out var value);
					dictionary[key] = value + 1;
				}
			}
			uint num2 = 0u;
			uint num3 = 0u;
			int num4 = 0;
			int num5 = 0;
			foreach (KeyValuePair<uint, int> item in dictionary)
			{
				uint num6 = item.Key | 0x80808;
				float num7 = (float)(num6 & 0xFF) / 255f;
				float num8 = (float)((num6 >> 8) & 0xFF) / 255f;
				float num9 = (float)((num6 >> 16) & 0xFF) / 255f;
				float num10 = Math.Max(num7, Math.Max(num8, num9));
				float num11 = Math.Min(num7, Math.Min(num8, num9));
				if (num7 * 0.3f + num8 * 0.59f + num9 * 0.11f < 0.22f)
				{
					continue;
				}
				if (num7 > num8 && num8 > num9 && (num10 - num11) / Math.Max(0.001f, num10) > 0.25f && num10 < 0.78f && num7 - num9 > 0.15f)
				{
					if (item.Value > num5)
					{
						num5 = item.Value;
						num3 = num6;
					}
				}
				else if (item.Value > num4)
				{
					num4 = item.Value;
					num2 = num6;
				}
			}
			if (num2 != 0 && num4 >= 3)
			{
				look.Head = TerrainCache.Lerp(look.Head, num2, 0.6f);
			}
			if (num3 != 0 && num5 >= 2)
			{
				look.Handle = TerrainCache.Lerp(look.Handle, num3, 0.6f);
			}
		}

		private static string[] MakeVariants()
		{
			string[] array = new string[9] { "wood", "stone", "copper", "bronze", "silver", "iron", "steel", "mythril", "adamantine" };
			List<string> list = new List<string>();
			string[] array2 = new string[5] { "sword", "axe", "spear", "hammer", "bow" };
			foreach (string text in array2)
			{
				string[] array3 = array;
				foreach (string text2 in array3)
				{
					if (!(text == "bow") || !(text2 == "stone"))
					{
						list.Add(text + "_" + text2);
					}
				}
			}
			return list.ToArray();
		}

		public static bool IsMetalVariant(string id)
		{
			if (id != null)
			{
				return Variants.Contains(id);
			}
			return false;
		}

		public static bool HasOwnModel(string id)
		{
			if (id != null)
			{
				if (!Variants.Contains(id))
				{
					return Uniques.Contains(id);
				}
				return true;
			}
			return false;
		}

		internal static HeldModel HeldOf(string name)
		{
			Func<string, MeshModel> library = Library;
			if (name == null || library == null)
			{
				return NoModel;
			}
			if ((object)library != _heldLibrary)
			{
				HeldModels.Clear();
				BentBows.Clear();
				_heldLibrary = library;
			}
			if (HeldModels.TryGetValue(name, out var value))
			{
				return value;
			}
			value = new HeldModel();
			MeshModel meshModel = library(name);
			MeshModel meshModel2 = library(name + "_m");
			MeshModel meshModel3 = library(name + "_f");
			value.Levels[0] = meshModel;
			value.Levels[1] = meshModel2 ?? meshModel;
			value.Levels[2] = meshModel3 ?? meshModel2 ?? meshModel;
			MeshModel meshModel4 = meshModel ?? meshModel2 ?? meshModel3;
			string text = meshModel4?.Info("kind");
			HeldModel heldModel = value;
			int kind;
			switch (text)
			{
			default:
				kind = (int)KindOf(name.StartsWith("held_", StringComparison.Ordinal) ? name.Substring(5) : name);
				break;
			case "stick":
				kind = 6;
				break;
			case "staff":
				kind = 7;
				break;
			case "bow":
				kind = 5;
				break;
			case "hammer":
				kind = 4;
				break;
			case "spear":
				kind = 3;
				break;
			case "axe":
				kind = 2;
				break;
			case "sword":
				kind = 1;
				break;
			}
			heldModel.Kind = (WeaponKind)kind;
			value.Tip = InfoVector(meshModel4?.Info("tip"), DefaultTip(value.Kind));
			value.Head = ((value.Kind == WeaponKind.Bow) ? new Vec3(0f, 0f, 0f) : (value.Tip * 0.85f));
			if (meshModel4 != null)
			{
				value.String = Colour(meshModel4.Info("string"));
				value.StringGlow = Flag(meshModel4.Info("string_glow"));
				value.Glow = Colour(meshModel4.Info("glow"));
				value.Lava = meshModel4.Info("material") == "adamantine" || Flag(meshModel4.Info("flicker"));
			}
			if (HeldModels.Count > 512)
			{
				HeldModels.Clear();
			}
			HeldModels[name] = value;
			return value;
		}

		public static MeshModel OwnModel(string name, int detail = 0)
		{
			return HeldOf(name).Levels[(detail >= 0) ? ((detail > 2) ? 2 : detail) : 0];
		}

		public static bool HeldGlow(MeshModel model, out uint colour, out Vec3 head, out bool lava)
		{
			colour = 0u;
			head = default(Vec3);
			lava = false;
			string text = model?.Name;
			if (text == null || !text.StartsWith("held_", StringComparison.Ordinal))
			{
				return false;
			}
			HeldModel heldModel = HeldOf(text);
			if (heldModel.Glow == 0)
			{
				return false;
			}
			colour = heldModel.Glow;
			head = heldModel.Head;
			lava = heldModel.Lava;
			return true;
		}

		public static Vec3 TipOf(string name)
		{
			if (name == null)
			{
				return default(Vec3);
			}
			return HeldOf(name).Tip;
		}

		private static Vec3 DefaultTip(WeaponKind kind)
		{
			switch (kind)
			{
			case WeaponKind.Sword:
				return new Vec3(0f, 0f, 0.51f);
			case WeaponKind.Axe:
				return new Vec3(0f, 0.135f, 0.36f);
			case WeaponKind.Spear:
				return new Vec3(0f, 0f, 0.68f);
			case WeaponKind.Hammer:
				return new Vec3(0f, 0.09f, 0.375f);
			case WeaponKind.Bow:
				return new Vec3(0f, BowCurve(1f), 0.33f);
			default:
				return new Vec3(0f, 0f, 0.4f);
			}
		}

		private static uint Colour(string v)
		{
			if (string.IsNullOrEmpty(v))
			{
				return 0u;
			}
			string[] array = v.Split(',');
			if (array.Length < 3)
			{
				return 0u;
			}
			float[] array2 = new float[3];
			bool flag = v.IndexOf('.') >= 0;
			for (int i = 0; i < 3; i++)
			{
				if (!float.TryParse(array[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out array2[i]))
				{
					return 0u;
				}
				if (array2[i] > 1f)
				{
					flag = false;
				}
			}
			float k = (flag ? 255f : 1f);
			return TerrainCache.Rgb(Ch(array2[0]), Ch(array2[1]), Ch(array2[2]));
			int Ch(float x)
			{
				return Math.Max(0, Math.Min(255, (int)Math.Round(x * k)));
			}
		}

		private static Vec3 InfoVector(string v, Vec3 fallback)
		{
			if (string.IsNullOrEmpty(v))
			{
				return fallback;
			}
			string[] array = v.Split(',');
			if (array.Length == 3 && float.TryParse(array[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && float.TryParse(array[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result2) && float.TryParse(array[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result3))
			{
				return new Vec3(result, result2, result3);
			}
			return fallback;
		}

		private static bool Flag(string v)
		{
			if (!(v == "1") && !string.Equals(v, "true", StringComparison.OrdinalIgnoreCase))
			{
				return string.Equals(v, "yes", StringComparison.OrdinalIgnoreCase);
			}
			return true;
		}

		private static MeshModel Bent(MeshModel rest, int step)
		{
			if (step <= 0)
			{
				return rest;
			}
			if (step > 8)
			{
				step = 8;
			}
			if (!BentBows.TryGetValue(rest, out var value))
			{
				if (BentBows.Count >= 128)
				{
					BentBows.Clear();
				}
				value = (BentBows[rest] = new MeshModel[9]);
			}
			return value[step] ?? (value[step] = BendBow(rest, (float)step / 8f));
		}

		public static MeshModel BendBow(MeshModel rest, float flex)
		{
			float num = 0f - BowCurve(1f);
			float num2 = 0f - BowCurve(1f, flex);
			float num3 = num - num2;
			Vec3 vec = BowTipAt(flex);
			float num4 = 0.29000002f;
			float num5 = (vec.Z - 0.04f) / num4;
			float num6 = float.MaxValue;
			float num7 = float.MaxValue;
			float num8 = float.MaxValue;
			float num9 = float.MinValue;
			float num10 = float.MinValue;
			float num11 = float.MinValue;
			MeshPart[] array = new MeshPart[rest.Parts.Length];
			for (int i = 0; i < array.Length; i++)
			{
				MeshPart meshPart = rest.Parts[i];
				float[] pos = meshPart.Pos;
				float[] nrm = meshPart.Nrm;
				float[] array2 = new float[pos.Length];
				float[] array3 = new float[nrm.Length];
				for (int j = 0; j + 2 < pos.Length; j += 3)
				{
					float num12 = pos[j];
					float num13 = pos[j + 1];
					float num14 = pos[j + 2];
					float num15 = nrm[j];
					float num16 = nrm[j + 1];
					float num17 = nrm[j + 2];
					float num18 = ((num14 < 0f) ? (-1f) : 1f);
					float num19 = num14 * num18;
					if (num19 > 0.04f)
					{
						float num21;
						if (num19 >= 0.33f)
						{
							float num20 = num19 - 0.33f;
							num21 = 2f * num3 / num4;
							num19 = vec.Z + num20 * num5;
							num13 = num13 + num3 + num21 * num20;
						}
						else
						{
							float num22 = (num19 - 0.04f) / num4;
							num21 = 2f * num3 * num22 / num4;
							num19 = 0.04f + (vec.Z - 0.04f) * num22;
							num13 += num3 * num22 * num22;
						}
						num14 = num19 * num18;
						float num23 = num17 * num18;
						float num24 = num15 * num5;
						float num25 = num16 * num5;
						float num26 = num23 - num21 * num16;
						float num27 = (float)Math.Sqrt(num24 * num24 + num25 * num25 + num26 * num26);
						if (num27 > 1E-12f)
						{
							num15 = num24 / num27;
							num16 = num25 / num27;
							num17 = num26 / num27 * num18;
						}
					}
					array2[j] = num12;
					array2[j + 1] = num13;
					array2[j + 2] = num14;
					array3[j] = num15;
					array3[j + 1] = num16;
					array3[j + 2] = num17;
					if (num12 < num6)
					{
						num6 = num12;
					}
					if (num13 < num7)
					{
						num7 = num13;
					}
					if (num14 < num8)
					{
						num8 = num14;
					}
					if (num12 > num9)
					{
						num9 = num12;
					}
					if (num13 > num10)
					{
						num10 = num13;
					}
					if (num14 > num11)
					{
						num11 = num14;
					}
				}
				array[i] = new MeshPart
				{
					Name = meshPart.Name,
					Parent = meshPart.Parent,
					Pivot = meshPart.Pivot,
					Pos = array2,
					Nrm = array3,
					Tri = meshPart.Tri,
					Slot = meshPart.Slot,
					Axis = meshPart.Axis,
					Uv = meshPart.Uv
				};
			}
			MeshModel meshModel = rest.Derive(rest.Name, array);
			if (num6 <= num9)
			{
				meshModel.Min = new Vec3(num6, num7, num8);
				meshModel.Max = new Vec3(num9, num10, num11);
			}
			return meshModel;
		}
	}
}
