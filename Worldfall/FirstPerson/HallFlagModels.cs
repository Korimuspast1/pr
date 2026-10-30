using System;
using System.Collections.Generic;
using System.Diagnostics;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class HallFlagModels
	{
		private struct Key : IEquatable<Key>
		{
			public string Set;

			public int A;

			public int B;

			public byte Kind;

			public byte Shape;

			public sbyte Lod;

			public sbyte Torn;

			public bool Equals(Key o)
			{
				if (A == o.A && B == o.B && Kind == o.Kind && Shape == o.Shape && Lod == o.Lod && Torn == o.Torn)
				{
					return string.Equals(Set, o.Set);
				}
				return false;
			}

			public override bool Equals(object o)
			{
				if (o is Key o2)
				{
					return Equals(o2);
				}
				return false;
			}

			public override int GetHashCode()
			{
				return ((((Set != null) ? Set.GetHashCode() : 0) * 31 + A) * 31 + B) * 31 + (Kind | (Shape << 8) | ((byte)Lod << 16) | ((byte)Torn << 24));
			}
		}

		public const int SoftCap = 600;

		public const int BuildsPerFrame = 2;

		private const double BuildMsPerFrame = 1.5;

		internal static int Built;

		internal static int BuildsThisFrame;

		internal static int StandInsBuilt;

		internal static double BuildMs;

		private const byte KindCloth = 0;

		private const byte KindBanner = 1;

		private const byte KindIcon = 2;

		private const byte KindFull = 3;

		private const byte KindBack = 4;

		private static readonly Dictionary<Key, MeshModel> Flags = new Dictionary<Key, MeshModel>();

		private static readonly Dictionary<Key, FlagEmblem> Emblems = new Dictionary<Key, FlagEmblem>();

		private static readonly MeshModel[] Plains = new MeshModel[60];

		private static readonly Stopwatch Watch = new Stopwatch();

		private static int _frame = -1;

		private static double _msThisFrame;

		public const int PoleWood = 0;

		public const int PoleMetal = 1;

		public const int PoleBroken = 2;

		public const int FinialBall = 3;

		public const int FinialSpike = 4;

		public const int FinialWing = 5;

		public const int FinialOnion = 6;

		public const int RodEnd = 7;

		public const int Bracket = 8;

		public const int Standard = 9;

		private const int HardwareCount = 10;

		private static readonly string[] HardwareNames = new string[10] { "rg_hallpole_wood", "rg_hallpole_metal", "rg_hallpole_broken", "rg_finial_n", "rg_finial_a", "rg_finial_b", "rg_finial_c", "rg_bannerrod_end", "rg_bannerbracket", "rg_standard" };

		private static readonly string[,] Names = MakeNames();

		private static readonly MeshModel[,] StandIns = new MeshModel[10, 3];

		private static readonly Dictionary<MeshModel, int> ShaftSlots = new Dictionary<MeshModel, int>();

		private static readonly uint Wood = TerrainCache.Rgb(96, 66, 40);

		private static readonly uint Iron = TerrainCache.Rgb(58, 60, 66);

		private static readonly uint IronHi = TerrainCache.Rgb(104, 108, 116);

		private static readonly uint IronDark = TerrainCache.Rgb(40, 41, 46);

		private static readonly uint Gold = TerrainCache.Rgb(222, 178, 64);

		private static readonly uint GoldDark = TerrainCache.Rgb(170, 128, 44);

		private static readonly uint Silver = TerrainCache.Rgb(200, 204, 210);

		private static readonly uint Char = TerrainCache.Rgb(50, 41, 35);

		private static readonly uint Oak = TerrainCache.Rgb(124, 86, 52);

		internal static int Cached => Flags.Count;

		public static void NewFrame(int frame)
		{
			if (frame != _frame)
			{
				_frame = frame;
				BuildsThisFrame = 0;
				_msThisFrame = 0.0;
			}
		}

		private static bool Budget()
		{
			if (BuildsThisFrame < 2)
			{
				return _msThisFrame < 1.5;
			}
			return false;
		}

		private static void Spent()
		{
			Watch.Stop();
			double totalMilliseconds = Watch.Elapsed.TotalMilliseconds;
			_msThisFrame += totalMilliseconds;
			BuildMs += totalMilliseconds;
			BuildsThisFrame++;
		}

		private static Sprite SpriteOf(bool background, string set, int index)
		{
			if (set == null)
			{
				return null;
			}
			try
			{
				GenericBannerLibrary kingdom_banners_library = AssetManager.kingdom_banners_library;
				if (kingdom_banners_library == null)
				{
					return null;
				}
				return background ? kingdom_banners_library.getSpriteBackground(index, set) : kingdom_banners_library.getSpriteIcon(index, set);
			}
			catch
			{
				return null;
			}
		}

		private static bool Emblem(byte kind, string set, int bg, int icon, SpriteCache sprites, out FlagEmblem e)
		{
			Key key = new Key
			{
				Kind = kind,
				Set = set,
				A = ((kind != 2) ? bg : 0),
				B = ((kind != 4) ? icon : 0)
			};
			if (Emblems.TryGetValue(key, out e))
			{
				return true;
			}
			e = null;
			if (sprites == null)
			{
				return false;
			}
			SpriteImage background = null;
			SpriteImage icon2 = null;
			if (kind != 2)
			{
				Sprite sprite = SpriteOf(background: true, set, bg);
				if ((object)sprite != null && sprite != null && (background = sprites.Get(sprite)) == null)
				{
					return false;
				}
			}
			if (kind != 4)
			{
				Sprite sprite2 = SpriteOf(background: false, set, icon);
				if ((object)sprite2 != null && sprite2 != null && (icon2 = sprites.Get(sprite2)) == null)
				{
					return false;
				}
			}
			FlagEmblem flagEmblem;
			switch (kind)
			{
			default:
				flagEmblem = FlagEmblem.FromBanner(background, icon2);
				break;
			case 4:
				flagEmblem = FlagEmblem.FromBanner(background, null);
				break;
			case 2:
				flagEmblem = FlagEmblem.FromIcon(icon2);
				break;
			}
			e = flagEmblem;
			if (Emblems.Count > 4096)
			{
				Emblems.Clear();
			}
			Emblems[key] = e;
			return true;
		}

		public static bool Greys(string set, int bg, int icon, SpriteCache sprites, out byte field, out byte dark, out byte emblem)
		{
			field = byte.MaxValue;
			dark = 200;
			emblem = byte.MaxValue;
			if (!Emblem(4, set, bg, icon, sprites, out var e) || !Emblem(2, set, bg, icon, sprites, out var e2))
			{
				return false;
			}
			if (e != null)
			{
				field = e.GreyField;
				dark = e.GreyDark;
			}
			if (e2 != null)
			{
				emblem = e2.GreyEmblem;
			}
			return true;
		}

		public static MeshModel Cloth(in FlagLook look, FlagShape shape, int lod, int torn, SpriteCache sprites)
		{
			Key key = new Key
			{
				Kind = 0,
				Set = look.Set,
				A = look.Icon,
				Shape = (byte)shape,
				Lod = (sbyte)lod,
				Torn = (sbyte)torn
			};
			if (Flags.TryGetValue(key, out var value))
			{
				return value;
			}
			if (Flags.Count >= 600 || !Budget())
			{
				return null;
			}
			if (!Emblem(2, look.Set, look.Bg, look.Icon, sprites, out var e))
			{
				return null;
			}
			Watch.Restart();
			try
			{
				value = FlagCloth.Flag(FlagCloth.ClothName(look.Set ?? "none", look.Icon, shape, lod, torn), shape, e, lod, torn);
			}
			catch (Exception ex)
			{
				Log.Error("hall flags: cloth: " + ex.Message);
				value = null;
			}
			Spent();
			if (value == null)
			{
				return null;
			}
			Flags[key] = value;
			Built++;
			return value;
		}

		public static MeshModel Banner(in FlagLook look, int lod, SpriteCache sprites)
		{
			Key key = new Key
			{
				Kind = 1,
				Set = look.Set,
				A = look.Bg,
				B = look.Icon,
				Lod = (sbyte)lod
			};
			if (Flags.TryGetValue(key, out var value))
			{
				return value;
			}
			if (Flags.Count >= 600 || !Budget())
			{
				return null;
			}
			if (!Emblem(3, look.Set, look.Bg, look.Icon, sprites, out var e) || e == null || !e.Box(out var _, out var _, out var _, out var _))
			{
				return null;
			}
			Watch.Restart();
			try
			{
				value = FlagCloth.Banner(FlagCloth.BannerName(look.Set ?? "none", look.Bg, look.Icon, lod), e, lod);
			}
			catch (Exception ex)
			{
				Log.Error("hall flags: banner: " + ex.Message);
				value = null;
			}
			Spent();
			if (value == null)
			{
				return null;
			}
			Flags[key] = value;
			Built++;
			return value;
		}

		public static MeshModel Plain(FlagShape shape, int lod, int torn)
		{
			lod = ((lod >= 0) ? ((lod > 2) ? 2 : lod) : 0);
			int num = ((int)shape * 3 + lod) * 5 + ((torn >= 0) ? (1 + (torn & 3)) : 0);
			MeshModel meshModel = Plains[num];
			if (meshModel != null)
			{
				return meshModel;
			}
			meshModel = (Plains[num] = FlagCloth.Plain(shape, lod, (torn < 0) ? (-1) : (torn & 3)));
			BuildsThisFrame++;
			return meshModel;
		}

		private static string[,] MakeNames()
		{
			string[,] array = new string[10, 3];
			for (int i = 0; i < 10; i++)
			{
				for (int j = 0; j < 3; j++)
				{
					array[i, j] = HardwareNames[i] + RegaliaKit.LodSuffix(j);
				}
			}
			return array;
		}

		public static int FinialOf(FlagTop top)
		{
			switch (top)
			{
			default:
				return 3;
			case FlagTop.Onion:
				return 6;
			case FlagTop.Wing:
				return 5;
			case FlagTop.Spike:
				return 4;
			}
		}

		public static bool Real(int piece, int lod)
		{
			return Models.Get(Names[piece, (lod >= 0) ? ((lod > 2) ? 2 : lod) : 0]) != null;
		}

		public static MeshModel Hardware(int piece, int lod)
		{
			lod = ((lod >= 0) ? ((lod > 2) ? 2 : lod) : 0);
			MeshModel meshModel = Models.Get(Names[piece, lod]) ?? ((lod > 0) ? Models.Get(Names[piece, 0]) : null);
			if (meshModel != null)
			{
				return meshModel;
			}
			meshModel = StandIns[piece, lod];
			if (meshModel != null)
			{
				return meshModel;
			}
			try
			{
				meshModel = StandIn(piece, lod);
			}
			catch (Exception ex)
			{
				Log.Error("hall flags: stand-in " + HardwareNames[piece] + ": " + ex.Message);
			}
			StandIns[piece, lod] = meshModel;
			StandInsBuilt++;
			BuildsThisFrame++;
			return meshModel;
		}

		public static int ShaftSlot(MeshModel m)
		{
			if (m == null)
			{
				return -1;
			}
			if (ShaftSlots.TryGetValue(m, out var value))
			{
				return value;
			}
			value = ((m.SlotNames != null) ? Array.IndexOf(m.SlotNames, "shaft") : (-1));
			ShaftSlots[m] = value;
			return value;
		}

		private static MeshModel StandIn(int piece, int lod)
		{
			int num;
			switch (lod)
			{
			default:
				num = 4;
				break;
			case 1:
				num = 6;
				break;
			case 0:
				num = 8;
				break;
			}
			int num2 = num;
			MeshBuilder meshBuilder = new MeshBuilder(Names[piece, lod] + "~standin");
			Vec3 vec = new Vec3(0f, 0f, 0f);
			int num3;
			int slot13;
			int slot14;
			switch (piece)
			{
			case 0:
				num3 = meshBuilder.Slot("shaft", 3, Wood, 0);
				goto IL_00a0;
			case 1:
				num3 = meshBuilder.Slot("shaft", 10, Iron, 16);
				goto IL_00a0;
			case 2:
			{
				int num9 = meshBuilder.Slot("shaft", 3, Wood, 0);
				int num10 = meshBuilder.Slot("char", 3, Char, 0);
				meshBuilder.Part("pole", -1, vec);
				meshBuilder.Tube(num9, vec, new Vec3(0f, 0f, 0.86f), 0.045f, 0.039f, num2);
				int num11;
				switch (lod)
				{
				default:
					num11 = 2;
					break;
				case 1:
					num11 = 3;
					break;
				case 0:
					num11 = 5;
					break;
				}
				int num12 = num11;
				for (int l = 0; l < num12; l++)
				{
					float num13 = (float)l * 6.2832f / (float)num12 + 0.4f;
					float num14 = 0.022f;
					Vec3 vec2 = new Vec3(num14 * (float)Math.Cos(num13), num14 * (float)Math.Sin(num13), 0.84f);
					Vec3 vec3 = new Vec3(vec2.X * 1.3f, vec2.Y * 1.3f, 0.9f + 0.1f * ((float)(l * 37 % 10) / 10f));
					meshBuilder.Loft((l % 2 == 0) ? num10 : num9, new Vec3[2] { vec2, vec3 }, new float[2] { 0.02f, 0f }, new float[2] { 0.014f, 0f }, new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), 4, smooth: false);
				}
				break;
			}
			case 3:
			{
				int slot5 = meshBuilder.Slot("iron", 10, Iron, 16);
				int slot6 = meshBuilder.Slot("iron_dark", 10, IronDark, 0);
				meshBuilder.Part("finial", -1, vec);
				meshBuilder.Tube(slot6, new Vec3(0f, 0f, -0.01f), new Vec3(0f, 0f, 0.06f), 0.055f, 0.05f, num2);
				meshBuilder.Tube(slot6, new Vec3(0f, 0f, 0.06f), new Vec3(0f, 0f, 0.11f), 0.022f, 0.022f, Math.Max(4, num2 - 2));
				meshBuilder.Blob(slot5, new Vec3(0f, 0f, 0.175f), 0.075f, 0.075f, 0.075f, num2);
				break;
			}
			case 4:
			{
				int slot11 = meshBuilder.Slot("iron", 10, Iron, 16);
				int slot12 = meshBuilder.Slot("iron_hi", 10, IronHi, 16);
				meshBuilder.Part("finial", -1, vec);
				meshBuilder.Tube(slot11, new Vec3(0f, 0f, -0.01f), new Vec3(0f, 0f, 0.08f), 0.06f, 0.058f, num2);
				if (lod < 2)
				{
					for (int k = 0; k < 6; k++)
					{
						float num6 = (float)k * 6.2832f / 6f;
						float num7 = (float)Math.Cos(num6);
						float num8 = (float)Math.Sin(num6);
						meshBuilder.Loft(slot12, new Vec3[2]
						{
							new Vec3(0.05f * num7, 0.05f * num8, 0.06f),
							new Vec3(0.085f * num7, 0.085f * num8, 0.18f)
						}, new float[2] { 0.012f, 0f }, new float[2] { 0.012f, 0f }, new Vec3(num7, num8, 0f), new Vec3(0f - num8, num7, 0f), 4, smooth: false);
					}
				}
				meshBuilder.Loft(slot12, new Vec3[3]
				{
					new Vec3(0f, 0f, 0.07f),
					new Vec3(0f, 0f, 0.22f),
					new Vec3(0f, 0f, 0.62f)
				}, new float[3] { 0.016f, 0.05f, 0f }, new float[3] { 0.016f, 0.014f, 0f }, new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), 4, smooth: false);
				break;
			}
			case 5:
			{
				int num15 = meshBuilder.Slot("gold", 10, Gold, 16);
				int num16 = meshBuilder.Slot("gold_dark", 10, GoldDark, 16);
				meshBuilder.Part("finial", -1, vec);
				meshBuilder.Tube(num16, new Vec3(0f, 0f, -0.01f), new Vec3(0f, 0f, 0.06f), 0.055f, 0.05f, num2);
				meshBuilder.Blob(num15, new Vec3(0f, 0f, 0.13f), 0.07f, 0.07f, 0.07f, num2);
				int num17 = ((lod == 2) ? 3 : 5);
				for (int m = -1; m <= 1; m += 2)
				{
					for (int n = 0; n < num17; n++)
					{
						float num18 = 0.25f + 1f * (float)n / (float)Math.Max(1, num17 - 1);
						float num19 = 0.09f;
						float num20 = 0.2f + 0.04f * (float)Math.Sin((float)n * 0.7f);
						Vec3 origin = new Vec3((float)m * 0.03f, 0f, 0.14f);
						float num21 = (float)Math.Cos(num18);
						float num22 = (float)Math.Sin(num18);
						float num23 = (float)Math.Cos(num18 - num19);
						float num24 = (float)Math.Sin(num18 - num19);
						float num25 = (float)Math.Cos(num18 + num19);
						float num26 = (float)Math.Sin(num18 + num19);
						meshBuilder.Extrude((n % 2 == 0) ? num15 : num16, origin, new Vec3(m, 0f, 0f), new Vec3(0f, 0f, 1f), 0.008f, 0f, 0f, num20 * num23, num20 * num24, num20 * 1.05f * num21, num20 * 1.05f * num22, num20 * num25, num20 * num26);
					}
				}
				break;
			}
			case 6:
			{
				int slot9 = meshBuilder.Slot("gold", 10, Gold, 16);
				int slot10 = meshBuilder.Slot("silver", 10, Silver, 16);
				meshBuilder.Part("finial", -1, vec);
				meshBuilder.Tube(slot9, new Vec3(0f, 0f, -0.01f), new Vec3(0f, 0f, 0.05f), 0.05f, 0.048f, num2);
				Vec3[] array = new Vec3[6]
				{
					new Vec3(0f, 0f, 0.05f),
					new Vec3(0f, 0f, 0.1f),
					new Vec3(0f, 0f, 0.17f),
					new Vec3(0f, 0f, 0.24f),
					new Vec3(0f, 0f, 0.3f),
					new Vec3(0f, 0f, 0.35f)
				};
				float[] array2 = new float[6] { 0.045f, 0.095f, 0.11f, 0.085f, 0.04f, 0.012f };
				meshBuilder.Loft(slot10, array, array2, array2, new Vec3(1f, 0f, 0f), new Vec3(0f, 1f, 0f), num2, smooth: true);
				if (lod < 2)
				{
					for (int i = 0; i < 4; i++)
					{
						float num5 = (float)i * 3.1416f / 4f;
						Vec3 ax = new Vec3((float)Math.Cos(num5), (float)Math.Sin(num5), 0f);
						Vec3 ay = new Vec3(0f - ax.Y, ax.X, 0f);
						Vec3[] array3 = new Vec3[array.Length];
						float[] array4 = new float[array.Length];
						float[] array5 = new float[array.Length];
						for (int j = 0; j < array.Length; j++)
						{
							array3[j] = array[j];
							array4[j] = array2[j] + 0.006f;
							array5[j] = 0.008f;
						}
						meshBuilder.Loft(slot9, array3, array4, array5, ax, ay, 4, smooth: false);
					}
				}
				meshBuilder.Blob(slot9, new Vec3(0f, 0f, 0.37f), 0.03f, 0.03f, 0.03f, Math.Max(4, num2 - 2));
				meshBuilder.Tube(slot9, new Vec3(0f, 0f, 0.39f), new Vec3(0f, 0f, 0.55f), 0.01f, 0f, 4, smooth: false);
				break;
			}
			case 7:
			{
				int slot7 = meshBuilder.Slot("gold", 10, Gold, 16);
				int slot8 = meshBuilder.Slot("iron", 10, Iron, 0);
				meshBuilder.Part("rod_end", -1, vec);
				meshBuilder.Blob(slot7, new Vec3(-0.4f, 0f, 0f), 0.9f, 0.9f, 0.9f, num2);
				if (lod < 2)
				{
					meshBuilder.Box(slot8, new Vec3(0.3f, 1.1f, 0f), 0.22f, 1.1f, 0.22f);
					meshBuilder.Box(slot8, new Vec3(0.3f, 2.25f, 0f), 0.6f, 0.08f, 0.9f);
				}
				break;
			}
			case 8:
			{
				int slot4 = meshBuilder.Slot("iron", 10, Iron, 0);
				meshBuilder.Part("bracket", -1, vec);
				meshBuilder.Box(slot4, new Vec3(0f, -0.015f, -0.08f), 0.08f, 0.015f, 0.16f);
				float num4 = 0.6f;
				meshBuilder.Box(slot4, new Vec3(0f, -0.5f * num4 - 0.02f, 0f), 0.018f, 0.5f * num4 + 0.02f, 0.018f);
				meshBuilder.Tube(slot4, new Vec3(0f, -0.02f, -0.22f), new Vec3(0f, -0.62f * num4, -0.01f), 0.012f, 0.012f, Math.Max(4, num2 - 2), smooth: false);
				if (lod < 2)
				{
					meshBuilder.Blob(slot4, new Vec3(0f, 0f - num4 - 0.03f, 0.01f), 0.03f, 0.03f, 0.03f, Math.Max(4, num2 - 2));
					meshBuilder.Box(slot4, new Vec3(0f, 0f - num4, -0.05f), 0.006f, 0.006f, 0.05f);
				}
				break;
			}
			case 9:
				{
					int slot = meshBuilder.Slot("wood", 3, Oak, 0);
					int slot2 = meshBuilder.Slot("iron", 10, Iron, 0);
					int slot3 = meshBuilder.Slot("gold", 10, Gold, 16);
					meshBuilder.Part("standard", -1, vec);
					meshBuilder.Tube(slot, new Vec3(0f, 0f, 0f), new Vec3(0f, 0f, 64f), 0.65f, 0.55f, num2);
					meshBuilder.Box(slot, new Vec3(0f, 0f, 60.2f), 22f, 0.45f, 0.45f);
					meshBuilder.Blob(slot3, new Vec3(0f, 0f, 64.9f), 1f, 1f, 1f, num2);
					if (lod < 2)
					{
						meshBuilder.Tube(slot2, new Vec3(0f, 0f, -0.2f), new Vec3(0f, 0f, 1.6f), 0.75f, 0.72f, num2);
						meshBuilder.Box(slot3, new Vec3(-22.2f, 0f, 60.2f), 0.4f, 0.6f, 0.6f);
						meshBuilder.Box(slot3, new Vec3(22.2f, 0f, 60.2f), 0.4f, 0.6f, 0.6f);
					}
					break;
				}
				IL_00a0:
				slot13 = num3;
				slot14 = meshBuilder.Slot("iron", 10, IronDark, 0);
				meshBuilder.Part("pole", -1, vec);
				meshBuilder.Tube(slot13, vec, new Vec3(0f, 0f, 1f), 0.045f, 0.035f, num2);
				if (lod < 2)
				{
					meshBuilder.Tube(slot14, new Vec3(0f, 0f, -0.005f), new Vec3(0f, 0f, 0.05f), 0.052f, 0.05f, num2);
				}
				break;
			}
			MeshModel meshModel = meshBuilder.Build();
			meshModel.ParseInfo("kind=hallflag_hardware;standin=1");
			return meshModel;
		}
	}
}
