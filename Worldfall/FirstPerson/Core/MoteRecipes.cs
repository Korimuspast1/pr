using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FirstPerson.Core
{
	public static class MoteRecipes
	{
		[Serializable]
		[CompilerGenerated]
		private sealed class <>c
		{
			public static readonly <>c <>9 = new <>c();

			public static Func<int, SpriteImage> <>9__5_0;

			public static Func<int, SpriteImage> <>9__5_1;

			public static Func<int, SpriteImage> <>9__5_2;

			internal SpriteImage <Build>b__5_0(int _)
			{
				return BlastFx.GlowSprite;
			}

			internal SpriteImage <Build>b__5_1(int f)
			{
				return AmbientArt.Fluff((f & 1) == 1);
			}

			internal SpriteImage <Build>b__5_2(int _)
			{
				return Flames.Ember;
			}
		}

		private static readonly List<MoteRecipe>[] ByBiome = Build();

		public static IEnumerable<MoteRecipe> All
		{
			get
			{
				List<MoteRecipe>[] byBiome = ByBiome;
				foreach (List<MoteRecipe> list in byBiome)
				{
					if (list == null)
					{
						continue;
					}
					foreach (MoteRecipe item in list)
					{
						yield return item;
					}
				}
			}
		}

		private static uint C(int r, int g, int b)
		{
			return TerrainCache.Rgb(r, g, b);
		}

		public static List<MoteRecipe> Of(BiomeKind k)
		{
			return ByBiome[(uint)k];
		}

		private static List<MoteRecipe>[] Build()
		{
			List<MoteRecipe> list = new List<MoteRecipe>();
			Func<int, SpriteImage> sprite = BiomeArt.Dot;
			if (<>c.<>9__5_0 == null)
			{
				<>c.<>9__5_0 = (int _) => BlastFx.GlowSprite;
			}
			Func<int, SpriteImage> sprite2 = BlastFx.Soft;
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Savanna,
				Name = "gold dust",
				Style = MoteStyle.Drift,
				Slots = 32,
				RMin = 1.2f,
				RMax = 13f,
				ZMin = 0.2f,
				ZMax = 2.6f,
				LifeMin = 6f,
				LifeMax = 12f,
				SizeMin = 0.045f,
				SizeMax = 0.075f,
				Colors = new uint[2]
				{
					C(255, 222, 150),
					C(244, 200, 128)
				},
				Alpha = 180,
				Sprite = sprite,
				Speed = -0.02f,
				Drift = 0.25f,
				Wind = 0.8f,
				Twinkle = 1.3f,
				DayW = 1f,
				NightW = 0.25f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Desert,
				Name = "sand ribbons",
				Style = MoteStyle.Ribbon,
				Slots = 28,
				RMin = 1.5f,
				RMax = 16f,
				ZMin = 0.02f,
				ZMax = 0.22f,
				LifeMin = 2.5f,
				LifeMax = 5f,
				FadeIn = 0.8f,
				FadeOut = 1.2f,
				SizeMin = 0.8f,
				SizeMax = 1.4f,
				Aspect = 0.16f,
				Colors = new uint[2]
				{
					C(255, 240, 206),
					C(248, 228, 186)
				},
				Alpha = 130,
				Sprite = sprite2,
				Speed = 3.2f,
				Wind = 0.5f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Desert,
				Name = "sand glints",
				Style = MoteStyle.Drift,
				Slots = 18,
				RMin = 1f,
				RMax = 8f,
				ZMin = 0.05f,
				ZMax = 0.6f,
				LifeMin = 1.5f,
				LifeMax = 3f,
				FadeIn = 0.4f,
				FadeOut = 0.6f,
				SizeMin = 0.05f,
				SizeMax = 0.07f,
				Colors = new uint[1] { C(255, 250, 222) },
				Alpha = 210,
				Additive = true,
				Emissive = true,
				Starry = true,
				Twinkle = 4f,
				Speed = -0.02f,
				Drift = 0.1f,
				Wind = 1f,
				DayW = 1f,
				NightW = 0f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Jungle,
				Name = "mist",
				Style = MoteStyle.Puff,
				Slots = 16,
				RMin = 5f,
				RMax = 26f,
				ZMin = 0.3f,
				ZMax = 2f,
				LifeMin = 18f,
				LifeMax = 30f,
				FadeIn = 4f,
				FadeOut = 5f,
				SizeMin = 3.5f,
				SizeMax = 6f,
				Aspect = 0.42f,
				Grow = 0.3f,
				Colors = new uint[1] { C(232, 248, 236) },
				Alpha = 86,
				Drift = 0.15f,
				Wind = 0.15f,
				DayW = 0.8f,
				NightW = 1f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Jungle,
				Name = "morphos",
				Style = MoteStyle.Flutter,
				Slots = 7,
				RMin = 2f,
				RMax = 14f,
				ZMin = 0.7f,
				ZMax = 3f,
				LifeMin = 14f,
				LifeMax = 24f,
				SizeMin = 0.22f,
				SizeMax = 0.3f,
				Colors = new uint[3]
				{
					C(70, 150, 255),
					C(40, 110, 240),
					C(90, 190, 255)
				},
				Alpha = byte.MaxValue,
				Emissive = true,
				Sprite = BiomeArt.Butterfly,
				Speed = 1.1f,
				FrameRate = 9f,
				DayW = 1f,
				NightW = 0f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Swamp,
				Name = "ground fog",
				Style = MoteStyle.Puff,
				Slots = 16,
				RMin = 3f,
				RMax = 28f,
				ZMin = -0.05f,
				ZMax = 0.3f,
				LifeMin = 20f,
				LifeMax = 34f,
				FadeIn = 5f,
				FadeOut = 6f,
				SizeMin = 4f,
				SizeMax = 7f,
				Aspect = 0.22f,
				Grow = 0.25f,
				Colors = new uint[2]
				{
					C(214, 224, 204),
					C(200, 214, 192)
				},
				Alpha = 104,
				Drift = 0.12f,
				Wind = 0.12f,
				DayW = 0.75f,
				NightW = 1.2f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Swamp,
				Name = "bubbles",
				Style = MoteStyle.Bubble,
				Slots = 8,
				RMin = 1.5f,
				RMax = 10f,
				ZMin = 0f,
				ZMax = 0.05f,
				LifeMin = 1.2f,
				LifeMax = 2.6f,
				FadeIn = 0.3f,
				FadeOut = 0.05f,
				SizeMin = 0.07f,
				SizeMax = 0.12f,
				Colors = new uint[1] { C(126, 146, 96) },
				Alpha = 210,
				Speed = 0.25f,
				Drift = 0.02f,
				Wind = 0f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Swamp,
				Name = "wisps",
				Style = MoteStyle.Glow,
				Slots = 4,
				RMin = 9f,
				RMax = 26f,
				ZMin = 0.6f,
				ZMax = 2f,
				LifeMin = 25f,
				LifeMax = 40f,
				FadeIn = 3f,
				FadeOut = 4f,
				SizeMin = 0.5f,
				SizeMax = 0.7f,
				Colors = new uint[2]
				{
					C(150, 255, 215),
					C(120, 220, 255)
				},
				Alpha = 230,
				Additive = true,
				Emissive = true,
				Flee = true,
				Trail = true,
				Light = 3.5f,
				LightColor = C(110, 255, 200),
				Drift = 0.3f,
				Wind = 0.1f,
				DayW = 0f,
				NightW = 1f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Permafrost,
				Name = "diamond dust",
				Style = MoteStyle.Drift,
				Slots = 72,
				RMin = 0.8f,
				RMax = 9f,
				ZMin = 0.3f,
				ZMax = 4f,
				LifeMin = 6f,
				LifeMax = 12f,
				SizeMin = 0.05f,
				SizeMax = 0.075f,
				Colors = new uint[3]
				{
					C(232, 246, 255),
					C(255, 255, 255),
					C(200, 232, 255)
				},
				Alpha = 230,
				Additive = true,
				Emissive = true,
				Starry = true,
				Twinkle = 3.2f,
				Speed = -0.06f,
				Drift = 0.12f,
				Wind = 0.5f,
				DayW = 1f,
				NightW = 0.45f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Birch,
				Name = "catkin fluff",
				Style = MoteStyle.Drift,
				Slots = 26,
				RMin = 1f,
				RMax = 10f,
				ZMin = 0.4f,
				ZMax = 3.4f,
				LifeMin = 8f,
				LifeMax = 14f,
				SizeMin = 0.08f,
				SizeMax = 0.11f,
				Colors = new uint[1] { C(255, 255, 250) },
				Alpha = 228,
				Sprite = (int f) => AmbientArt.Fluff((f & 1) == 1),
				Frames = 2,
				FrameRate = 0.5f,
				Speed = -0.05f,
				Drift = 0.25f,
				Wind = 0.9f,
				DayW = 1f,
				NightW = 0.3f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Birch,
				Name = "moths",
				Style = MoteStyle.Flutter,
				Slots = 5,
				RMin = 1.5f,
				RMax = 7f,
				ZMin = 0.5f,
				ZMax = 2.4f,
				LifeMin = 12f,
				LifeMax = 20f,
				SizeMin = 0.13f,
				SizeMax = 0.17f,
				Colors = new uint[1] { C(240, 236, 214) },
				Alpha = 240,
				Sprite = BiomeArt.Moth,
				Speed = 0.9f,
				FrameRate = 12f,
				DayW = 0f,
				NightW = 1f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Maple,
				Name = "maple seeds",
				Style = MoteStyle.Fall,
				Slots = 14,
				RMin = 1.5f,
				RMax = 12f,
				ZMin = 3f,
				ZMax = 7f,
				LifeMin = 9f,
				LifeMax = 14f,
				SizeMin = 0.15f,
				SizeMax = 0.19f,
				Colors = new uint[2]
				{
					C(198, 120, 60),
					C(178, 94, 46)
				},
				Alpha = byte.MaxValue,
				Sprite = BiomeArt.Samara,
				Frames = 4,
				FrameRate = 11f,
				Speed = 0.55f,
				Drift = 0.25f,
				Wind = 0.6f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Maple,
				Name = "red leaves",
				Style = MoteStyle.Fall,
				Slots = 12,
				RMin = 1.5f,
				RMax = 12f,
				ZMin = 2f,
				ZMax = 6f,
				LifeMin = 8f,
				LifeMax = 14f,
				SizeMin = 0.14f,
				SizeMax = 0.19f,
				Colors = new uint[4]
				{
					C(220, 70, 40),
					C(240, 130, 40),
					C(200, 50, 50),
					C(250, 170, 60)
				},
				Alpha = byte.MaxValue,
				Sprite = WindArt.Leaf,
				Frames = 3,
				FrameRate = 2.2f,
				Speed = 0.45f,
				Drift = 0.5f,
				Wind = 0.8f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Rocklands,
				Name = "grit",
				Style = MoteStyle.Drift,
				Slots = 20,
				RMin = 1f,
				RMax = 12f,
				ZMin = 0.05f,
				ZMax = 0.7f,
				LifeMin = 2f,
				LifeMax = 4f,
				FadeIn = 0.5f,
				FadeOut = 0.8f,
				SizeMin = 0.035f,
				SizeMax = 0.055f,
				Colors = new uint[2]
				{
					C(124, 116, 106),
					C(96, 90, 84)
				},
				Alpha = 210,
				Sprite = sprite,
				Speed = -0.1f,
				Drift = 0.6f,
				Wind = 1.6f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Rocklands,
				Name = "dust",
				Style = MoteStyle.Puff,
				Slots = 5,
				RMin = 4f,
				RMax = 18f,
				ZMin = 0f,
				ZMax = 0.3f,
				LifeMin = 5f,
				LifeMax = 9f,
				FadeIn = 1.5f,
				FadeOut = 3f,
				SizeMin = 0.8f,
				SizeMax = 1.5f,
				Aspect = 0.5f,
				Grow = 0.8f,
				Colors = new uint[1] { C(196, 188, 176) },
				Alpha = 60,
				Drift = 0.4f,
				Wind = 0.8f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Clover,
				Name = "clover leaves",
				Style = MoteStyle.Fall,
				Slots = 12,
				RMin = 1f,
				RMax = 10f,
				ZMin = 0.5f,
				ZMax = 3f,
				LifeMin = 8f,
				LifeMax = 14f,
				SizeMin = 0.12f,
				SizeMax = 0.16f,
				Colors = new uint[2]
				{
					C(96, 196, 112),
					C(70, 170, 96)
				},
				Alpha = byte.MaxValue,
				Sprite = BiomeArt.Clover,
				Frames = 2,
				FrameRate = 2.5f,
				Speed = 0.3f,
				Drift = 0.4f,
				Wind = 0.7f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Clover,
				Name = "lucky glints",
				Style = MoteStyle.Drift,
				Slots = 18,
				RMin = 1f,
				RMax = 9f,
				ZMin = 0.2f,
				ZMax = 2f,
				LifeMin = 3f,
				LifeMax = 6f,
				SizeMin = 0.06f,
				SizeMax = 0.08f,
				Colors = new uint[2]
				{
					C(140, 255, 170),
					C(220, 255, 150)
				},
				Alpha = 220,
				Additive = true,
				Emissive = true,
				Starry = true,
				Twinkle = 3f,
				Speed = 0.05f,
				DayW = 1f,
				NightW = 0.6f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Flower,
				Name = "petals",
				Style = MoteStyle.Fall,
				Slots = 40,
				RMin = 1f,
				RMax = 12f,
				ZMin = 0.3f,
				ZMax = 5f,
				LifeMin = 9f,
				LifeMax = 16f,
				SizeMin = 0.1f,
				SizeMax = 0.14f,
				Colors = new uint[5]
				{
					C(255, 150, 200),
					C(255, 246, 246),
					C(255, 226, 110),
					C(212, 160, 255),
					C(255, 140, 120)
				},
				Alpha = byte.MaxValue,
				Sprite = BiomeArt.Petal,
				Frames = 4,
				FrameRate = 3f,
				Speed = 0.28f,
				Drift = 0.35f,
				Wind = 0.7f,
				Swirl = 0.9f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Flower,
				Name = "butterflies",
				Style = MoteStyle.Flutter,
				Slots = 5,
				RMin = 1.5f,
				RMax = 10f,
				ZMin = 0.5f,
				ZMax = 2.5f,
				LifeMin = 14f,
				LifeMax = 24f,
				SizeMin = 0.17f,
				SizeMax = 0.22f,
				Colors = new uint[3]
				{
					C(255, 232, 90),
					C(255, 255, 255),
					C(255, 150, 60)
				},
				Alpha = byte.MaxValue,
				Sprite = BiomeArt.Butterfly,
				Speed = 1f,
				FrameRate = 8f,
				DayW = 1f,
				NightW = 0f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Lemon,
				Name = "zest sparkles",
				Style = MoteStyle.Drift,
				Slots = 32,
				RMin = 1f,
				RMax = 10f,
				ZMin = 0.3f,
				ZMax = 3f,
				LifeMin = 3f,
				LifeMax = 7f,
				SizeMin = 0.05f,
				SizeMax = 0.08f,
				Colors = new uint[2]
				{
					C(255, 250, 150),
					C(255, 236, 90)
				},
				Alpha = 220,
				Additive = true,
				Emissive = true,
				Starry = true,
				Twinkle = 3.6f,
				Speed = 0.04f,
				Drift = 0.2f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Lemon,
				Name = "citrus mist",
				Style = MoteStyle.Puff,
				Slots = 6,
				RMin = 4f,
				RMax = 16f,
				ZMin = 0.2f,
				ZMax = 1.5f,
				LifeMin = 10f,
				LifeMax = 18f,
				FadeIn = 3f,
				FadeOut = 4f,
				SizeMin = 1.6f,
				SizeMax = 2.6f,
				Aspect = 0.5f,
				Grow = 0.4f,
				Colors = new uint[1] { C(255, 248, 182) },
				Alpha = 42,
				Drift = 0.2f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Garlic,
				Name = "wisps",
				Style = MoteStyle.Puff,
				Slots = 14,
				RMin = 2f,
				RMax = 16f,
				ZMin = 0f,
				ZMax = 0.3f,
				LifeMin = 6f,
				LifeMax = 10f,
				FadeIn = 1.5f,
				FadeOut = 3f,
				SizeMin = 0.45f,
				SizeMax = 0.7f,
				Aspect = 0.9f,
				Grow = 1.4f,
				Colors = new uint[2]
				{
					C(238, 242, 204),
					C(220, 230, 176)
				},
				Alpha = 92,
				Speed = 0.28f,
				Drift = 0.2f,
				Wind = 0.4f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Garlic,
				Name = "whiffs",
				Style = MoteStyle.Rise,
				Slots = 8,
				RMin = 1.5f,
				RMax = 8f,
				ZMin = 0.1f,
				ZMax = 0.6f,
				Top = 2.5f,
				LifeMin = 2.5f,
				LifeMax = 4f,
				FadeIn = 0.6f,
				FadeOut = 1f,
				SizeMin = 0.12f,
				SizeMax = 0.16f,
				Aspect = 2.4f,
				Colors = new uint[1] { C(196, 214, 116) },
				Alpha = 170,
				Sprite = BiomeArt.Whiff,
				Frames = 2,
				FrameRate = 3f,
				Speed = 0.35f,
				Drift = 0.1f,
				Wind = 0.3f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Candy,
				Name = "bubbles",
				Style = MoteStyle.Bubble,
				Slots = 22,
				RMin = 1.5f,
				RMax = 14f,
				ZMin = 0.2f,
				ZMax = 1.2f,
				LifeMin = 5f,
				LifeMax = 10f,
				FadeIn = 0.8f,
				FadeOut = 0.05f,
				SizeMin = 0.22f,
				SizeMax = 0.55f,
				Colors = new uint[1] { C(255, 255, 255) },
				Hue = 0.15f,
				Alpha = 205,
				Speed = 0.38f,
				Drift = 0.25f,
				Wind = 0.5f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Candy,
				Name = "sprinkles",
				Style = MoteStyle.Fall,
				Slots = 32,
				RMin = 1f,
				RMax = 10f,
				ZMin = 0.5f,
				ZMax = 4.5f,
				LifeMin = 7f,
				LifeMax = 12f,
				SizeMin = 0.07f,
				SizeMax = 0.1f,
				Colors = new uint[6]
				{
					C(255, 120, 170),
					C(120, 200, 255),
					C(255, 236, 120),
					C(150, 240, 150),
					C(255, 255, 255),
					C(200, 150, 255)
				},
				Alpha = byte.MaxValue,
				Sprite = BiomeArt.Sprinkle,
				Frames = 3,
				FrameRate = 4f,
				Speed = 0.3f,
				Drift = 0.2f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Candy,
				Name = "sugar clouds",
				Style = MoteStyle.Puff,
				Slots = 6,
				RMin = 6f,
				RMax = 24f,
				ZMin = 1f,
				ZMax = 4f,
				LifeMin = 14f,
				LifeMax = 22f,
				FadeIn = 4f,
				FadeOut = 5f,
				SizeMin = 2f,
				SizeMax = 3.4f,
				Aspect = 0.62f,
				Grow = 0.3f,
				Colors = new uint[2]
				{
					C(255, 192, 226),
					C(196, 224, 255)
				},
				Alpha = 64,
				Drift = 0.2f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Mushroom,
				Name = "spores",
				Style = MoteStyle.Rise,
				Slots = 54,
				RMin = 1f,
				RMax = 14f,
				ZMin = 0.05f,
				ZMax = 2.2f,
				Top = 6f,
				LifeMin = 8f,
				LifeMax = 15f,
				SizeMin = 0.13f,
				SizeMax = 0.2f,
				Colors = new uint[3]
				{
					C(90, 255, 222),
					C(196, 136, 255),
					C(255, 222, 120)
				},
				Alpha = 190,
				Additive = true,
				Emissive = true,
				DayBright = 0.6f,
				NightBright = 2f,
				Pulse = true,
				Speed = 0.12f,
				Drift = 0.18f,
				Wind = 0.35f,
				Light = 2.4f,
				LightColor = C(90, 255, 222)
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Mushroom,
				Name = "spore puffs",
				Style = MoteStyle.Puff,
				Slots = 5,
				RMin = 3f,
				RMax = 14f,
				ZMin = 0.1f,
				ZMax = 0.6f,
				LifeMin = 6f,
				LifeMax = 10f,
				FadeIn = 1.5f,
				FadeOut = 3f,
				SizeMin = 0.6f,
				SizeMax = 1f,
				Aspect = 0.8f,
				Grow = 1.2f,
				Colors = new uint[2]
				{
					C(160, 230, 210),
					C(210, 180, 255)
				},
				Alpha = 72,
				Speed = 0.15f,
				Drift = 0.15f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Crystal,
				Name = "prism shards",
				Style = MoteStyle.Drift,
				Slots = 18,
				RMin = 1.5f,
				RMax = 12f,
				ZMin = 0.5f,
				ZMax = 3.4f,
				LifeMin = 12f,
				LifeMax = 20f,
				SizeMin = 0.1f,
				SizeMax = 0.15f,
				Aspect = 1.6f,
				Colors = new uint[2]
				{
					C(160, 255, 250),
					C(255, 170, 230)
				},
				Hue = 0.05f,
				Alpha = 235,
				Emissive = true,
				Glint = true,
				Sprite = BiomeArt.Shard,
				Frames = 4,
				FrameRate = 2f,
				Speed = 0f,
				Drift = 0.12f,
				Wind = 0.2f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Crystal,
				Name = "rainbow glints",
				Style = MoteStyle.Drift,
				Slots = 28,
				RMin = 1f,
				RMax = 10f,
				ZMin = 0.2f,
				ZMax = 3f,
				LifeMin = 3f,
				LifeMax = 6f,
				SizeMin = 0.05f,
				SizeMax = 0.08f,
				Colors = new uint[1] { C(255, 160, 160) },
				Hue = 0.3f,
				Alpha = 220,
				Additive = true,
				Emissive = true,
				Starry = true,
				Twinkle = 3f,
				Speed = 0.02f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Enchanted,
				Name = "fairy lights",
				Style = MoteStyle.Glow,
				Slots = 24,
				RMin = 1.5f,
				RMax = 14f,
				ZMin = 0.2f,
				ZMax = 1.4f,
				Top = 7f,
				LifeMin = 9f,
				LifeMax = 16f,
				FadeIn = 1.5f,
				FadeOut = 2f,
				SizeMin = 0.24f,
				SizeMax = 0.4f,
				Colors = new uint[3]
				{
					C(255, 222, 140),
					C(226, 176, 255),
					C(170, 255, 222)
				},
				Alpha = 220,
				Additive = true,
				Emissive = true,
				Speed = 0.22f,
				Drift = 0.15f,
				Trail = true,
				Light = 2.2f,
				LightColor = C(255, 220, 170),
				DayBright = 0.8f,
				NightBright = 1.3f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Enchanted,
				Name = "sparkles",
				Style = MoteStyle.Drift,
				Slots = 18,
				RMin = 1f,
				RMax = 9f,
				ZMin = 0.2f,
				ZMax = 2.5f,
				LifeMin = 2f,
				LifeMax = 5f,
				SizeMin = 0.05f,
				SizeMax = 0.07f,
				Colors = new uint[2]
				{
					C(255, 240, 200),
					C(240, 210, 255)
				},
				Alpha = 220,
				Additive = true,
				Emissive = true,
				Starry = true,
				Twinkle = 4f,
				Speed = 0.04f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Corrupted,
				Name = "rising motes",
				Style = MoteStyle.Rise,
				Slots = 54,
				RMin = 1f,
				RMax = 16f,
				ZMin = 0f,
				ZMax = 1.4f,
				Top = 8f,
				LifeMin = 6f,
				LifeMax = 11f,
				SizeMin = 0.06f,
				SizeMax = 0.09f,
				Colors = new uint[2]
				{
					C(34, 16, 44),
					C(52, 24, 62)
				},
				Alpha = 232,
				Sprite = sprite,
				Speed = 0.5f,
				Drift = 0.12f,
				Wind = 0.3f,
				Jitter = 1.4f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Corrupted,
				Name = "violet sparks",
				Style = MoteStyle.Rise,
				Slots = 14,
				RMin = 1f,
				RMax = 14f,
				ZMin = 0f,
				ZMax = 1.4f,
				Top = 8f,
				LifeMin = 5f,
				LifeMax = 9f,
				SizeMin = 0.15f,
				SizeMax = 0.22f,
				Colors = new uint[2]
				{
					C(170, 80, 220),
					C(120, 60, 200)
				},
				Alpha = 180,
				Additive = true,
				Emissive = true,
				NightBright = 1.4f,
				Speed = 0.45f,
				Drift = 0.12f,
				Wind = 0.3f,
				Jitter = 1.4f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Corrupted,
				Name = "murk",
				Style = MoteStyle.Puff,
				Slots = 9,
				RMin = 4f,
				RMax = 24f,
				ZMin = -0.05f,
				ZMax = 0.3f,
				LifeMin = 16f,
				LifeMax = 26f,
				FadeIn = 4f,
				FadeOut = 5f,
				SizeMin = 3f,
				SizeMax = 5f,
				Aspect = 0.26f,
				Grow = 0.3f,
				Colors = new uint[1] { C(92, 52, 108) },
				Alpha = 104,
				Drift = 0.1f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Infernal,
				Name = "embers",
				Style = MoteStyle.Rise,
				Slots = 42,
				RMin = 1f,
				RMax = 16f,
				ZMin = 0f,
				ZMax = 0.8f,
				Top = 9f,
				LifeMin = 3f,
				LifeMax = 6f,
				FadeIn = 0.4f,
				FadeOut = 1.2f,
				SizeMin = 0.06f,
				SizeMax = 0.09f,
				Colors = new uint[3]
				{
					C(255, 170, 80),
					C(255, 120, 40),
					C(255, 220, 120)
				},
				Alpha = byte.MaxValue,
				Additive = true,
				Emissive = true,
				Sprite = (int _) => Flames.Ember,
				Speed = 1.2f,
				Drift = 0.5f,
				Wind = 0.6f,
				Twinkle = 7f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Infernal,
				Name = "ash",
				Style = MoteStyle.Fall,
				Slots = 22,
				RMin = 1f,
				RMax = 12f,
				ZMin = 1f,
				ZMax = 6f,
				LifeMin = 8f,
				LifeMax = 14f,
				SizeMin = 0.045f,
				SizeMax = 0.065f,
				Colors = new uint[2]
				{
					C(96, 90, 88),
					C(140, 132, 128)
				},
				Alpha = 200,
				Sprite = sprite,
				Speed = 0.25f,
				Drift = 0.3f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Infernal,
				Name = "glowing cracks",
				Style = MoteStyle.Glow,
				Slots = 4,
				RMin = 3f,
				RMax = 14f,
				ZMin = 0.1f,
				ZMax = 0.2f,
				LifeMin = 4f,
				LifeMax = 8f,
				FadeIn = 1.5f,
				FadeOut = 2f,
				Colors = new uint[1] { C(255, 90, 30) },
				Alpha = 0,
				Drift = 0f,
				Wind = 0f,
				Light = 4f,
				LightColor = C(255, 90, 30),
				DayW = 0.3f,
				NightW = 1f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Wasteland,
				Name = "toxic motes",
				Style = MoteStyle.Drift,
				Slots = 32,
				RMin = 1f,
				RMax = 12f,
				ZMin = 0.2f,
				ZMax = 2.6f,
				LifeMin = 5f,
				LifeMax = 10f,
				SizeMin = 0.13f,
				SizeMax = 0.19f,
				Colors = new uint[2]
				{
					C(170, 255, 90),
					C(210, 255, 120)
				},
				Alpha = 170,
				Additive = true,
				Emissive = true,
				DayBright = 0.6f,
				NightBright = 1.6f,
				Speed = 0.03f,
				Drift = 0.2f,
				Twinkle = 0.9f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Wasteland,
				Name = "acid bubbles",
				Style = MoteStyle.Bubble,
				Slots = 8,
				RMin = 1.5f,
				RMax = 10f,
				ZMin = 0f,
				ZMax = 0.05f,
				LifeMin = 1.2f,
				LifeMax = 2.4f,
				FadeIn = 0.3f,
				FadeOut = 0.05f,
				SizeMin = 0.07f,
				SizeMax = 0.11f,
				Colors = new uint[1] { C(150, 230, 80) },
				Alpha = 220,
				Speed = 0.3f,
				Drift = 0.02f,
				Wind = 0f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Wasteland,
				Name = "sick mist",
				Style = MoteStyle.Puff,
				Slots = 8,
				RMin = 4f,
				RMax = 22f,
				ZMin = 0f,
				ZMax = 0.4f,
				LifeMin = 14f,
				LifeMax = 24f,
				FadeIn = 4f,
				FadeOut = 5f,
				SizeMin = 2.5f,
				SizeMax = 4f,
				Aspect = 0.3f,
				Grow = 0.3f,
				Colors = new uint[1] { C(206, 224, 132) },
				Alpha = 80,
				Drift = 0.12f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Celestial,
				Name = "star motes",
				Style = MoteStyle.Fall,
				Slots = 44,
				RMin = 1f,
				RMax = 12f,
				ZMin = 1f,
				ZMax = 7f,
				LifeMin = 10f,
				LifeMax = 18f,
				SizeMin = 0.08f,
				SizeMax = 0.11f,
				Colors = new uint[3]
				{
					C(255, 255, 255),
					C(226, 200, 255),
					C(255, 236, 190)
				},
				Alpha = 240,
				Additive = true,
				Emissive = true,
				Starry = true,
				Twinkle = 2f,
				Speed = 0.18f,
				Drift = 0.2f,
				Wind = 0.4f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Singularity,
				Name = "infall",
				Style = MoteStyle.Spiral,
				Slots = 64,
				RMin = 1f,
				RMax = 18f,
				SizeMin = 0.05f,
				SizeMax = 0.08f,
				FadeIn = 1f,
				Colors = new uint[3]
				{
					C(190, 130, 255),
					C(240, 236, 255),
					C(120, 80, 220)
				},
				Alpha = 220,
				Additive = true,
				Emissive = true,
				Sprite = sprite,
				Speed = 1f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Singularity,
				Name = "floating stones",
				Style = MoteStyle.Orbit,
				Slots = 6,
				RMin = 1f,
				RMax = 18f,
				LifeMin = 20f,
				LifeMax = 40f,
				FadeIn = 3f,
				FadeOut = 3f,
				SizeMin = 0.14f,
				SizeMax = 0.26f,
				Colors = new uint[1] { C(236, 222, 255) },
				Alpha = 240,
				Speed = 1f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Paradox,
				Name = "rain upwards",
				Style = MoteStyle.UpRain,
				Slots = 64,
				RMin = 1.6f,
				RMax = 13f,
				Top = 9f,
				FadeIn = 0.15f,
				SizeMin = 0.024f,
				SizeMax = 0.03f,
				Aspect = 20f,
				Colors = new uint[1] { C(200, 226, 255) },
				Hue = 0.02f,
				Alpha = 215,
				Emissive = true,
				Speed = 7f,
				Wind = 0.4f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Paradox,
				Name = "echoes",
				Style = MoteStyle.Echo,
				Slots = 14,
				RMin = 1.5f,
				RMax = 10f,
				ZMin = 0.5f,
				ZMax = 2.5f,
				LifeMin = 6f,
				LifeMax = 10f,
				SizeMin = 0.2f,
				SizeMax = 0.28f,
				Colors = new uint[1] { C(255, 170, 170) },
				Hue = 0.05f,
				Alpha = 210,
				Additive = true,
				Emissive = true,
				Speed = 0.8f,
				Drift = 0.1f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Tumor,
				Name = "flesh spores",
				Style = MoteStyle.Drift,
				Slots = 26,
				RMin = 1f,
				RMax = 12f,
				ZMin = 0.3f,
				ZMax = 2.6f,
				LifeMin = 6f,
				LifeMax = 12f,
				SizeMin = 0.1f,
				SizeMax = 0.18f,
				Colors = new uint[2]
				{
					C(232, 124, 134),
					C(200, 90, 110)
				},
				Alpha = 175,
				Sprite = sprite2,
				Speed = 0.04f,
				Drift = 0.15f,
				Beat = true
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Biomass,
				Name = "spores",
				Style = MoteStyle.Drift,
				Slots = 26,
				RMin = 1f,
				RMax = 12f,
				ZMin = 0.3f,
				ZMax = 2.6f,
				LifeMin = 6f,
				LifeMax = 12f,
				SizeMin = 0.1f,
				SizeMax = 0.16f,
				Colors = new uint[2]
				{
					C(150, 176, 82),
					C(196, 150, 92)
				},
				Alpha = 165,
				Sprite = sprite2,
				Speed = 0.04f,
				Drift = 0.15f,
				Beat = true
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Biomass,
				Name = "fibres",
				Style = MoteStyle.Drift,
				Slots = 16,
				RMin = 1f,
				RMax = 9f,
				ZMin = 0.2f,
				ZMax = 2.4f,
				LifeMin = 5f,
				LifeMax = 9f,
				SizeMin = 0.035f,
				SizeMax = 0.05f,
				Colors = new uint[1] { C(210, 200, 150) },
				Alpha = 190,
				Sprite = sprite,
				Speed = -0.02f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Pumpkin,
				Name = "wisps",
				Style = MoteStyle.Glow,
				Slots = 8,
				RMin = 4f,
				RMax = 20f,
				ZMin = 0.6f,
				ZMax = 2f,
				LifeMin = 18f,
				LifeMax = 30f,
				FadeIn = 3f,
				FadeOut = 4f,
				SizeMin = 0.45f,
				SizeMax = 0.6f,
				Colors = new uint[2]
				{
					C(255, 150, 40),
					C(255, 190, 80)
				},
				Alpha = 210,
				Additive = true,
				Emissive = true,
				Trail = true,
				Light = 3f,
				LightColor = C(255, 140, 40),
				Drift = 0.3f,
				DayW = 0.2f,
				NightW = 1f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Pumpkin,
				Name = "dry leaves",
				Style = MoteStyle.Fall,
				Slots = 10,
				RMin = 1.5f,
				RMax = 12f,
				ZMin = 2f,
				ZMax = 5f,
				LifeMin = 8f,
				LifeMax = 14f,
				SizeMin = 0.14f,
				SizeMax = 0.18f,
				Colors = new uint[3]
				{
					C(200, 110, 40),
					C(160, 90, 40),
					C(230, 150, 50)
				},
				Alpha = byte.MaxValue,
				Sprite = WindArt.Leaf,
				Frames = 3,
				FrameRate = 2.2f,
				Speed = 0.45f,
				Drift = 0.5f
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Cybertile,
				Name = "bits",
				Style = MoteStyle.Glitch,
				Slots = 48,
				RMin = 2.2f,
				RMax = 13f,
				ZMin = 0.1f,
				ZMax = 3f,
				Top = 5f,
				LifeMin = 4f,
				LifeMax = 8f,
				FadeIn = 0.3f,
				FadeOut = 0.6f,
				SizeMin = 0.06f,
				SizeMax = 0.08f,
				Colors = new uint[3]
				{
					C(80, 240, 255),
					C(255, 80, 220),
					C(220, 255, 255)
				},
				Alpha = 230,
				Additive = true,
				Emissive = true,
				Sprite = BiomeArt.Bit
			});
			list.Add(new MoteRecipe
			{
				Biome = BiomeKind.Cybertile,
				Name = "data columns",
				Style = MoteStyle.Column,
				Slots = 8,
				RMin = 3.5f,
				RMax = 16f,
				Top = 5f,
				LifeMin = 30f,
				LifeMax = 30f,
				FadeIn = 0.2f,
				SizeMin = 0.07f,
				SizeMax = 0.07f,
				Colors = new uint[1] { C(80, 240, 255) },
				Alpha = 230,
				Additive = true,
				Emissive = true,
				Sprite = BiomeArt.Bit
			});
			BiomeKind[] array = new BiomeKind[10]
			{
				BiomeKind.Grass,
				BiomeKind.Savanna,
				BiomeKind.Jungle,
				BiomeKind.Swamp,
				BiomeKind.Birch,
				BiomeKind.Maple,
				BiomeKind.Clover,
				BiomeKind.Flower,
				BiomeKind.Lemon,
				BiomeKind.Mushroom
			};
			foreach (BiomeKind biomeKind in array)
			{
				bool flag = biomeKind == BiomeKind.Jungle || biomeKind == BiomeKind.Swamp;
				list.Add(new MoteRecipe
				{
					Biome = biomeKind,
					Name = "fireflies",
					Style = MoteStyle.Glow,
					Slots = (flag ? 54 : 40),
					RMin = 1.8f,
					RMax = 18f,
					ZMin = 0.25f,
					ZMax = 1.3f,
					Top = 3f,
					LifeMin = 10f,
					LifeMax = 18f,
					FadeIn = 1.5f,
					FadeOut = 2f,
					SizeMin = 0.32f,
					SizeMax = 0.44f,
					Colors = new uint[3]
					{
						C(214, 255, 110),
						C(188, 246, 96),
						C(240, 255, 150)
					},
					Alpha = 235,
					Additive = true,
					Emissive = true,
					Drift = 0.22f,
					Wind = 0.25f,
					Blink = 0.55f,
					Warm = true,
					DayW = 0f,
					NightW = 1f
				});
				if (biomeKind != BiomeKind.Swamp && biomeKind != BiomeKind.Jungle)
				{
					list.Add(new MoteRecipe
					{
						Biome = biomeKind,
						Name = "night mist",
						Style = MoteStyle.Puff,
						Slots = 7,
						RMin = 6f,
						RMax = 26f,
						ZMin = -0.05f,
						ZMax = 0.25f,
						LifeMin = 20f,
						LifeMax = 34f,
						FadeIn = 6f,
						FadeOut = 6f,
						SizeMin = 3.5f,
						SizeMax = 6f,
						Aspect = 0.2f,
						Grow = 0.25f,
						Colors = new uint[2]
						{
							C(70, 80, 108),
							C(78, 88, 116)
						},
						Alpha = 56,
						Emissive = true,
						Drift = 0.1f,
						Wind = 0.12f,
						DayW = 0f,
						NightW = 1f
					});
				}
			}
			List<MoteRecipe>[] array2 = new List<MoteRecipe>[28];
			foreach (MoteRecipe item in list)
			{
				if (array2[(uint)item.Biome] == null)
				{
					array2[(uint)item.Biome] = new List<MoteRecipe>();
				}
				array2[(uint)item.Biome].Add(item);
			}
			return array2;
		}
	}
}
