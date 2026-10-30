using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class SettingsMenu
	{
		internal sealed class Row
		{
			public string Name;

			public string Note;

			public string Help;

			public string Heading;

			public string[] Choices;

			public Func<Settings, int> Chosen;

			public Action<Settings, int> Choose;

			public bool Dropdown;

			public bool Fixed;

			public string[] Codes;

			public float Min;

			public float Max;

			public float Step;

			public bool Ratio;

			public Func<Settings, float> Value;

			public Action<Settings, float> SetValue;

			public Func<float, string> Show;

			public bool IsChoice => Choices != null;

			public float ToSlider(float v)
			{
				v = Mathf.Clamp(v, Min, Max);
				if (!Ratio)
				{
					return (v - Min) / (Max - Min);
				}
				return Mathf.Log(v / Min) / Mathf.Log(Max / Min);
			}

			public float FromSlider(float t)
			{
				t = Mathf.Clamp01(t);
				float num = (Ratio ? (Min * Mathf.Pow(Max / Min, t)) : Mathf.Lerp(Min, Max, t));
				if (Step > 0f)
				{
					num = Mathf.Round(num / Step) * Step;
				}
				return Mathf.Clamp(num, Min, Max);
			}
		}

		public static readonly string[] Tabs = new string[8] { "Graphics", "Controls", "Game", "Sound", "VR", "Character", "Multiplayer", "Mods" };

		public const int GraphicsTab = 0;

		public const int ControlsTab = 1;

		public const int GameTab = 2;

		public const int VrTab = 4;

		public const int CharacterTab = 5;

		public const int MultiplayerTab = 6;

		public const int ModsTab = 7;

		public bool Open;

		public int Tab;

		public int Shelf;

		public Vector2 Scroll;

		public bool Changed;

		public float Unroll;

		public Row Drop;

		public Row HelpRow;

		public int DropLit;

		public float DropScroll;

		public bool DropFollow;

		private int _dropShutFrame = -1;

		private List<Row>[] _rows;

		public Row VrRow;

		internal static readonly string[] QualityNames = new string[5] { "Low", "Medium", "High", "Ultra", "Custom" };

		private static readonly float[] PresetDraw = new float[4] { 180f, 270f, 390f, 600f };

		private static readonly float[] PresetDetail = new float[4] { 50f, 80f, 120f, 200f };

		private static readonly float[] PresetCreature = new float[4] { 120f, 200f, 400f, 800f };

		private static readonly float[] PresetShadowReach = new float[4] { 32f, 64f, 96f, 128f };

		private static readonly float[] PresetParticles = new float[4] { 0.3f, 1f, 1f, 1.5f };

		private static readonly int[] PresetShadows = new int[4] { 0, 2, 3, 4 };

		private static readonly int[] PresetWindows = new int[4] { 0, 1, 1, 1 };

		private static readonly int[] PresetFarModels = new int[4] { 0, 1, 1, 1 };

		private static readonly bool[] PresetFlare = new bool[4] { false, true, true, true };

		public void ToggleHelp(Row row)
		{
			HelpRow = ((HelpRow == row) ? null : row);
		}

		public void OpenDrop(Row row, Settings s)
		{
			Drop = row;
			DropLit = Mathf.Clamp(row.Chosen(s), 0, row.Choices.Length - 1);
			DropScroll = -1f;
			DropFollow = true;
		}

		public void ShutDrop()
		{
			if (Drop != null)
			{
				Drop = null;
				_dropShutFrame = Time.frameCount;
			}
		}

		public void DropMove(int by)
		{
			if (Drop != null)
			{
				DropLit = Mathf.Clamp(DropLit + by, 0, Drop.Choices.Length - 1);
				DropFollow = true;
			}
		}

		public bool EscapeDrop()
		{
			if (Drop != null)
			{
				ShutDrop();
				return true;
			}
			return _dropShutFrame == Time.frameCount;
		}

		public List<Row> RowsOf(int tab)
		{
			if (_rows == null || tab < 0 || tab >= _rows.Length)
			{
				return null;
			}
			return _rows[tab];
		}

		public void Build(WorldBoxMod mod)
		{
			if (_rows != null)
			{
				return;
			}
			string[] choices = new string[2] { "Off", "On" };
			Func<float, string> show = (float v) => Mathf.RoundToInt(v * 100f) + "%";
			Func<float, string> show2 = (float v) => (!(v <= 0f)) ? (Mathf.RoundToInt(v * 100f) + "%") : "Off";
			List<Row> list = new List<Row>();
			list.Add(Sectioned("Presets", LightRow()));
			list.Add(QualityRow());
			list.Add(Sectioned("Renderer", new Row
			{
				Name = "Renderer",
				Note = "GPU recommended. Faster, looks fully up and down, rooms included",
				Help = "GPU draws the view on the graphics card. It is faster, looks straight up and down, and the look rows below need it. CPU is the older drawing, for machines with no graphics card to spare",
				Choices = new string[2] { "CPU", "GPU" },
				Chosen = (Settings s) => s.GpuRender ? 1 : 0,
				Choose = delegate(Settings s, int i)
				{
					s.GpuRender = i == 1;
				}
			}));
			list.Add(new Row
			{
				Name = "Resolution",
				Note = WorldBoxMod.ResolutionName(WorldBoxMod.SharpWidth) + " shows each pixel as 2x2. The CPU stops at " + WorldBoxMod.ResolutionName(640),
				Help = "The size of the picture before it is stretched to the screen. Lower is blockier and faster. Higher is sharper and costs frames. The look, not the quality preset",
				Choices = Names(WorldBoxMod.ResolutionWidths, WorldBoxMod.ResolutionName),
				Chosen = (Settings s) => s.Resolution,
				Choose = delegate(Settings s, int i)
				{
					s.Resolution = i;
				}
			});
			list.Add(new Row
			{
				Name = "Vertical sync",
				Note = "Prevents tearing. Off for a higher frame rate",
				Help = "On waits for the screen between frames, so the picture never tears, and the frame rate is held at the screen's. Off draws as fast as it can",
				Choices = choices,
				Chosen = (Settings s) => s.VSync ? 1 : 0,
				Choose = delegate(Settings s, int i)
				{
					s.VSync = i == 1;
				}
			});
			list.Add(new Row
			{
				Name = "Frame rate cap",
				Note = "Most frames a second in 3D with vertical sync off. Lower saves power",
				Help = "Stops the 3D view drawing more frames a second than this. A lower cap runs cooler and quieter on a laptop or the Deck. Game's own keeps WorldBox's setting",
				Choices = FrameCaps.Names,
				Dropdown = true,
				Chosen = (Settings s) => s.FrameCap,
				Choose = delegate(Settings s, int i)
				{
					s.FrameCap = i;
				}
			});
			list.Add(new Row
			{
				Name = "Draw while the game runs",
				Note = "Renders alongside the game for a higher frame rate",
				Help = "The view is drawn on other threads while the game runs its own frame, for more frames a second. Turn it off if the picture stutters or lags a step behind your mouse",
				Choices = choices,
				Chosen = (Settings s) => s.AsyncRender ? 1 : 0,
				Choose = delegate(Settings s, int i)
				{
					s.AsyncRender = i == 1;
				}
			});
			list.Add(new Row
			{
				Name = "World steps",
				Note = "In the 3D world view, the world steps 30 times a second. Smoother in big worlds",
				Help = "How often the world moves on while the 3D map shows. 30 a second gives the drawing more time in big worlds and creatures still move smoothly. Every frame is the game's own pace",
				Choices = new string[2] { "Every frame", "30 a second" },
				Chosen = (Settings s) => s.WorldSteps,
				Choose = delegate(Settings s, int i)
				{
					s.WorldSteps = i;
				}
			});
			list.Add(new Row
			{
				Name = "Show frame rate",
				Note = "Frames a second in the corner of the screen",
				Help = "A small counter of frames a second in a corner. Changes nothing else",
				Choices = choices,
				Chosen = (Settings s) => s.ShowFps ? 1 : 0,
				Choose = delegate(Settings s, int i)
				{
					s.ShowFps = i == 1;
				}
			});
			list.Add(Sectioned("Distance", new Row
			{
				Name = "Draw distance",
				Note = "How far the land is drawn",
				Help = "How far the land is drawn before fog closes in, in tiles. Whole map shows all of it from a mountain top. Further costs more each frame, most of all on the CPU",
				Min = 120f,
				Max = 1200f,
				Step = 10f,
				Ratio = true,
				Value = (Settings s) => s.DrawDistance,
				SetValue = delegate(Settings s, float v)
				{
					s.DrawDistance = v;
				},
				Show = (float v) => (!(v >= 1200f)) ? (v.ToString("0") + " tiles") : "Whole map"
			}));
			list.Add(new Row
			{
				Name = "Detail distance",
				Note = "Range of full detail. Beyond it, simpler models or sprites",
				Help = "Out to this many tiles, trees and buildings are full 3D models. Past it they are the simpler far models or flat sprites. The biggest single cost in a city",
				Min = 30f,
				Max = 240f,
				Step = 5f,
				Ratio = true,
				Value = (Settings s) => s.DetailDistance,
				SetValue = delegate(Settings s, float v)
				{
					s.DetailDistance = v;
				},
				Show = (float v) => v.ToString("0") + " tiles"
			});
			list.Add(new Row
			{
				Name = "Far trees and buildings",
				Note = "Beyond the detail distance. 3D models need the GPU renderer",
				Help = "What stands past the detail distance. Sprites are flat pictures that always face you. 3D models keep their shape as you move and cost a little more",
				Choices = new string[2] { "Sprites", "3D models" },
				Chosen = (Settings s) => s.FarModels,
				Choose = delegate(Settings s, int i)
				{
					s.FarModels = i;
				}
			});
			list.Add(new Row
			{
				Name = "Creature distance",
				Note = "Range of 3D creatures. Beyond it, distant specks",
				Help = "Out to this many tiles, people and animals are 3D figures that walk and fight. Past it they are specks. Lower it if towns and battles slow the frame",
				Min = 60f,
				Max = 1200f,
				Step = 10f,
				Ratio = true,
				Value = (Settings s) => s.CreatureDistance,
				SetValue = delegate(Settings s, float v)
				{
					s.CreatureDistance = v;
				},
				Show = (float v) => (!(v >= 1200f)) ? (v.ToString("0") + " tiles") : "As far as you see"
			});
			list.Add(Sectioned("Light and shadow", new Row
			{
				Name = "Sun",
				Note = "Natural has seasons and moon phases. Auto uses it on the GPU",
				Help = "Classic is one fixed sun as the game has always had. Natural moves the sun with the seasons, gives the moon its phases, and lights the sky and clouds by it. Auto picks Natural on the GPU",
				Choices = new string[3] { "Auto", "Classic", "Natural" },
				Chosen = (Settings s) => s.SunStyle,
				Choose = delegate(Settings s, int i)
				{
					s.SunStyle = i;
					mod.ForceRelight();
				}
			}));
			list.Add(new Row
			{
				Name = "Lens flare",
				Note = "Light streaks when you face the sun. Natural sun only",
				Help = "Streaks and rings of light across the view when the sun is in it. Off removes them. Cheap either way",
				Choices = choices,
				Chosen = (Settings s) => s.LensFlare ? 1 : 0,
				Choose = delegate(Settings s, int i)
				{
					s.LensFlare = i == 1;
				}
			});
			list.Add(new Row
			{
				Name = "Shadows",
				Note = "Shadows of trees, buildings and creatures. High and Ultra add sharper ones near you",
				Help = "Off draws no cast shadows. Low and Medium cast them from the sun and moon at a coarser or finer grain. High and Ultra add a second, sharp map around you. Each step costs more each frame",
				Choices = new string[5] { "Off", "Low", "Medium", "High", "Ultra" },
				Chosen = (Settings s) => s.Shadows,
				Choose = delegate(Settings s, int i)
				{
					s.Shadows = i;
				}
			});
			list.Add(new Row
			{
				Name = "Shadow distance",
				Note = "How far from you shadows are drawn",
				Help = "How far out shadows are cast, in tiles. Further spreads the same shadow map thinner, so near shadows get softer, and costs more. Shorter is sharper and cheaper",
				Min = 24f,
				Max = 192f,
				Step = 8f,
				Ratio = true,
				Value = (Settings s) => s.ShadowDistance,
				SetValue = delegate(Settings s, float v)
				{
					s.ShadowDistance = v;
				},
				Show = (float v) => v.ToString("0") + " tiles"
			});
			list.Add(new Row
			{
				Name = "Shadow softness",
				Note = "Natural: sharp where a shadow meets its caster, softer further off. GPU only",
				Help = "Crisp gives hard edges. Soft blurs every edge the same. Natural keeps a shadow sharp at the foot of what casts it and softens it with distance, as the sun does. Natural costs a little more",
				Choices = new string[3] { "Crisp", "Soft", "Natural" },
				Chosen = (Settings s) => s.ShadowSoftness,
				Choose = delegate(Settings s, int i)
				{
					s.ShadowSoftness = i;
				}
			});
			list.Add(new Row
			{
				Name = "Shadow darkness",
				Note = "How dark shadows fall",
				Help = "How much a shadow darkens what it falls on. 0% is no visible shadow, 100% is black. Costs nothing",
				Min = 0f,
				Max = 1f,
				Step = 0.05f,
				Value = (Settings s) => s.ShadowDarkness,
				SetValue = delegate(Settings s, float v)
				{
					s.ShadowDarkness = v;
				},
				Show = show
			});
			list.Add(Sectioned("Look", new Row
			{
				Name = "Palette filter",
				Note = "Reduced color palette with ordered dithering",
				Help = "Rounds every color to a small fixed palette and dithers between them, like an old display. Off keeps the full color. Costs nothing",
				Choices = choices,
				Chosen = (Settings s) => s.Palette,
				Choose = delegate(Settings s, int i)
				{
					s.Palette = i;
				}
			}));
			list.Add(new Row
			{
				Name = "Dark fantasy",
				Note = "Moody night-forest colors and glowing water. GPU only",
				Help = "Regrades the whole picture toward dark greens and steel, with deeper shadows and water that glows at night. Off is the game's own colors",
				Choices = choices,
				Chosen = (Settings s) => s.DarkFantasy,
				Choose = delegate(Settings s, int i)
				{
					s.DarkFantasy = i;
				}
			});
			list.Add(new Row
			{
				Name = "Classic",
				Note = "Retro look: the distance in crisp pixels, dark blue nights. GPU only",
				Help = "The far land in crisp pixel blocks and nights in deep blue, as the CPU renderer drew them. Off is the smooth GPU picture. Costs nothing",
				Choices = choices,
				Chosen = (Settings s) => s.PixelDistance,
				Choose = delegate(Settings s, int i)
				{
					s.PixelDistance = i;
				}
			});
			list.Add(new Row
			{
				Name = "House windows",
				Note = "The town outside a house's windows and open door. Costs an outdoor frame. GPU only",
				Help = "Town shows the real street through a house's windows and open door, drawn as a second frame while you are inside. Sky shows only the sky through them and is cheaper",
				Choices = new string[2] { "Sky", "Town" },
				Chosen = (Settings s) => s.WindowView,
				Choose = delegate(Settings s, int i)
				{
					s.WindowView = i;
				}
			});
			list.Add(Sectioned("Air and sky", new Row
			{
				Name = "Biome effects",
				Note = "Each land's own air, light and particles",
				Help = "Each biome colors the air and light in its own way, with mist in swamps, embers over lava, motes in forests. Subtle keeps it faint. Off is plain air everywhere",
				Choices = new string[3] { "Off", "Subtle", "Full" },
				Chosen = (Settings s) => s.BiomeFx,
				Choose = delegate(Settings s, int i)
				{
					s.BiomeFx = i;
				}
			}));
			list.Add(new Row
			{
				Name = "Biome particles",
				Note = "Amount of mist, embers and motes in the air",
				Help = "How many motes, embers, leaves and flakes drift in the air. More is thicker and costs more each frame. Off removes them",
				Min = 0f,
				Max = 2f,
				Step = 0.1f,
				Value = (Settings s) => s.BiomeParticles,
				SetValue = delegate(Settings s, float v)
				{
					s.BiomeParticles = v;
				},
				Show = show2
			});
			list.Add(new Row
			{
				Name = "Gusts of wind",
				Note = "Occasional gusts that bend the trees and carry leaves",
				Help = "Now and then the wind rises. Trees bend, leaves and grass fly, and the wind's sound rises with it. Off keeps the air still",
				Choices = choices,
				Chosen = (Settings s) => s.WindGusts ? 1 : 0,
				Choose = delegate(Settings s, int i)
				{
					s.WindGusts = i == 1;
				}
			});
			list.Add(Sectioned("Crowds", new Row
			{
				Name = "Crowd detail",
				Note = "Far people animated on the graphics card",
				Help = "People too small on screen to show their limbs are posed on the graphics card instead. High does it under 6 pixels tall, Medium under 12, Low under 24. Full poses everyone in full",
				Choices = new string[4] { "Full", "High", "Medium", "Low" },
				Chosen = (Settings s) => s.CrowdDetail,
				Choose = delegate(Settings s, int i)
				{
					s.CrowdDetail = i;
				}
			}));
			list.Add(new Row
			{
				Name = "Far creatures",
				Note = "Creatures far off and out of view think and move every 2nd to 4th step. Faster in big worlds",
				Help = "Budgeted lets creatures far from you think and move every 2nd to 4th step of the game instead of every one. Nothing near you changes. Full runs them all every step",
				Choices = new string[2] { "Full", "Budgeted" },
				Chosen = (Settings s) => s.FarCreatures,
				Choose = delegate(Settings s, int i)
				{
					s.FarCreatures = i;
				}
			});
			list.Add(new Row
			{
				Name = "Far poses",
				Note = "Distant people's limbs update less often. Bodies still move every frame",
				Help = "Reduced poses the limbs of people past 60 tiles every 2nd frame and past 120 every 4th. Their bodies still move every frame. Every frame poses everyone every frame",
				Choices = new string[2] { "Every frame", "Reduced" },
				Chosen = (Settings s) => s.FarPoses,
				Choose = delegate(Settings s, int i)
				{
					s.FarPoses = i;
				}
			});
			list.Add(new Row
			{
				Name = "Off-screen creatures",
				Note = "Creatures behind you skip motion smoothing. Faster in big worlds",
				Help = "Cheap skips the smooth walking of creatures well out of your view. You see no difference until you turn, and half a town is behind you. Full smooths them all",
				Choices = new string[2] { "Full", "Cheap" },
				Chosen = (Settings s) => s.OffscreenMotion,
				Choose = delegate(Settings s, int i)
				{
					s.OffscreenMotion = i;
				}
			});
			List<Row> list2 = list;
			list = new List<Row>();
			list.Add(Sectioned("View", new Row
			{
				Name = "Field of view",
				Note = "Horizontal view angle",
				Help = "How wide a slice of the world fits on the screen, in degrees. Wider shows more and stretches the edges. Narrower looks zoomed in. The mouse wheel changes it too",
				Min = 50f,
				Max = 110f,
				Step = 1f,
				Value = (Settings s) => s.FieldOfView,
				SetValue = delegate(Settings s, float v)
				{
					s.FieldOfView = v;
				},
				Show = (float v) => v.ToString("0") + "°"
			}));
			list.Add(new Row
			{
				Name = "Eye height",
				Note = "Eye level above the ground",
				Help = "Where your eyes sit, as a share of your creature's own height. Lower is closer to the ground, higher looks over walls and crowds",
				Min = 0.3f,
				Max = 4f,
				Step = 0.05f,
				Ratio = true,
				Value = (Settings s) => s.EyeHeight,
				SetValue = delegate(Settings s, float v)
				{
					s.EyeHeight = v;
				},
				Show = show
			});
			list.Add(new Row
			{
				Name = "Head bob",
				Note = "Camera sway while walking",
				Help = "How much the view rises and sways with each step. Off holds it level, which helps if walking makes you queasy",
				Min = 0f,
				Max = 1f,
				Step = 0.05f,
				Value = (Settings s) => s.HeadBob,
				SetValue = delegate(Settings s, float v)
				{
					s.HeadBob = v;
				},
				Show = (float v) => (!(v <= 0f)) ? (Mathf.RoundToInt(v * 100f) + "%") : "Off"
			});
			list.Add(new Row
			{
				Name = "Screen shake",
				Note = "Camera shake when you take damage",
				Help = "The view jolts when you are hit, land hard or stand near a blast. Off keeps it still",
				Choices = choices,
				Chosen = (Settings s) => s.ScreenShake ? 1 : 0,
				Choose = delegate(Settings s, int i)
				{
					s.ScreenShake = i == 1;
				}
			});
			list.Add(new Row
			{
				Name = "Show your body",
				Note = "Your shadow, and your legs when you look down",
				Help = "Shadow draws your own shadow on the ground before you. Shadow and legs also shows your legs and feet when you look down. Off draws neither",
				Choices = new string[3] { "Off", "Shadow", "Shadow and legs" },
				Chosen = (Settings s) => s.ShowBody,
				Choose = delegate(Settings s, int i)
				{
					s.ShowBody = i;
				}
			});
			list.Add(Sectioned("Camera", new Row
			{
				Name = "Pose view",
				Note = "Where you see yourself sit or kneel from. Mouse wheel switches",
				Help = "Where the view starts when you sit or kneel (P). Third person looks at you from behind. First person stays in your eyes. The mouse wheel moves between them while you rest",
				Choices = new string[2] { "First person", "Third person" },
				Chosen = (Settings s) => s.PoseView,
				Choose = delegate(Settings s, int i)
				{
					s.PoseView = i;
				}
			}));
			list.Add(Sectioned("Mouse", new Row
			{
				Name = "Mouse sensitivity",
				Note = "Mouse look speed",
				Help = "How far the view turns for each inch the mouse moves. Higher turns faster",
				Min = 0.2f,
				Max = 10f,
				Step = 0.1f,
				Ratio = true,
				Value = (Settings s) => s.MouseSensitivity,
				SetValue = delegate(Settings s, float v)
				{
					s.MouseSensitivity = v;
				},
				Show = (float v) => v.ToString("0.0")
			}));
			list.Add(Sectioned("Touch", new Row
			{
				Name = "Touch controls",
				Note = "On-screen stick and buttons. Auto shows them on touchscreens",
				Help = "A stick and buttons drawn on the screen for a finger. Auto shows them only on a touchscreen. On always shows them, Off never does",
				Choices = new string[3] { "Auto", "On", "Off" },
				Chosen = (Settings s) => s.TouchControls,
				Choose = delegate(Settings s, int i)
				{
					s.TouchControls = i;
				}
			}));
			list.Add(new Row
			{
				Name = "Touch look speed",
				Note = "Turn speed for finger drags",
				Help = "How far the view turns as a finger drags across the screen. Higher turns faster",
				Min = 0.3f,
				Max = 3f,
				Step = 0.05f,
				Ratio = true,
				Value = (Settings s) => s.TouchLook,
				SetValue = delegate(Settings s, float v)
				{
					s.TouchLook = v;
				},
				Show = (float v) => v.ToString("0.00")
			});
			list.Add(Sectioned("Controller", new Row
			{
				Name = "Controller",
				Note = "Gamepad and Steam Deck controls in first person and on the map",
				Help = "Reads a gamepad or the Steam Deck's own sticks and buttons, in first person and on the map. Off ignores them and the keyboard and mouse do everything",
				Choices = choices,
				Chosen = (Settings s) => s.Controller ? 1 : 0,
				Choose = delegate(Settings s, int i)
				{
					s.Controller = i == 1;
				}
			}));
			list.Add(new Row
			{
				Name = "Stick look speed",
				Note = "Turn speed for the right stick",
				Help = "How fast the view turns with the right stick pushed all the way. Higher turns faster",
				Min = 0.3f,
				Max = 3f,
				Step = 0.05f,
				Ratio = true,
				Value = (Settings s) => s.PadLook,
				SetValue = delegate(Settings s, float v)
				{
					s.PadLook = v;
				},
				Show = (float v) => v.ToString("0.00")
			});
			list.Add(new Row
			{
				Name = "Invert stick look",
				Note = "Pushing the right stick up looks down",
				Help = "On flips the right stick's up and down, as in flight games. Off looks up when you push up",
				Choices = choices,
				Chosen = (Settings s) => s.PadInvert ? 1 : 0,
				Choose = delegate(Settings s, int i)
				{
					s.PadInvert = i == 1;
				}
			});
			list.Add(new Row
			{
				Name = "Aim slowdown",
				Note = "Slower turning over a target with a bow or gun",
				Help = "With a bow or gun in hand the stick turns slower while the crosshair is over a creature, so a shot is easier to hold. Higher slows it more. Off turns at full speed",
				Min = 0.2f,
				Max = 1f,
				Step = 0.05f,
				Value = (Settings s) => 1.2f - s.PadAimSlow,
				SetValue = delegate(Settings s, float v)
				{
					s.PadAimSlow = 1.2f - v;
				},
				Show = (float v) => (!(v <= 0.2001f)) ? (Mathf.RoundToInt((v - 0.2f) * 100f) + "%") : "Off"
			});
			list.Add(new Row
			{
				Name = "Button pictures",
				Note = "Buttons shown in prompts. Auto follows the pad in use",
				Help = "Which pad's buttons the prompts draw. Auto follows the pad you are holding. Pick one if it guesses wrong",
				Choices = new string[4] { "Auto", "Steam Deck", "Xbox", "PlayStation" },
				Chosen = (Settings s) => s.PadPictures,
				Choose = delegate(Settings s, int i)
				{
					s.PadPictures = i;
				}
			});
			list.Add(Sectioned("Interface", new Row
			{
				Name = "Interface size",
				Note = "Size of the HUD, text and windows. Raise it on a sharp screen",
				Help = "Scales the HUD, every window and their text. Raise it on a small sharp screen or from across the room, lower it to see more of the world",
				Min = 0.8f,
				Max = 1.6f,
				Step = 0.05f,
				Value = (Settings s) => s.UiScale,
				SetValue = delegate(Settings s, float v)
				{
					s.UiScale = v;
				},
				Show = show
			}));
			List<Row> list3 = list;
			List<KeyValuePair<string, string>> langs = Lang.Available();
			string[] array = new string[langs.Count + 1];
			string[] array2 = new string[langs.Count + 1];
			array[0] = "Auto";
			for (int num = 0; num < langs.Count; num++)
			{
				array[num + 1] = langs[num].Value;
				array2[num + 1] = langs[num].Key;
			}
			list = new List<Row>();
			list.Add(Sectioned("Language", new Row
			{
				Name = "Language",
				Note = "Language of the mod's text. Auto follows WorldBox",
				Help = "The language of the mod's own text, from the lang folder. Auto follows the language WorldBox is set to. Text with no translation stays in English",
				Choices = array,
				Codes = array2,
				Dropdown = true,
				Chosen = (Settings s) => (!(s.Language == "auto")) ? (1 + Math.Max(0, langs.FindIndex((KeyValuePair<string, string> l) => l.Key == s.Language))) : 0,
				Choose = delegate(Settings s, int i)
				{
					s.Language = ((i == 0) ? "auto" : langs[i - 1].Key);
				}
			}));
			list.Add(Sectioned("Movement", new Row
			{
				Name = "Run speed",
				Note = "The pace of W, as a share of the creature's full speed. Shift to sprint, Alt to walk",
				Help = "How fast W takes you, as a share of the creature's full speed. Shift sprints faster than this and a tap of Alt walks slowly whatever it is. Lower is calmer and easier to steer",
				Min = 0.2f,
				Max = 1f,
				Step = 0.05f,
				Value = (Settings s) => s.RunSpeed,
				SetValue = delegate(Settings s, float v)
				{
					s.RunSpeed = v;
				},
				Show = show
			}));
			list.Add(new Row
			{
				Name = "Others' pace",
				Note = "Speed of creatures near you. Fighting and fleeing keep full speed",
				Help = "Slows the creatures around you to a walk you can keep up with and talk to. Anyone fighting or fleeing keeps full speed. 100% is the game's own pace",
				Min = 0.3f,
				Max = 1f,
				Step = 0.05f,
				Value = (Settings s) => s.FolkPace,
				SetValue = delegate(Settings s, float v)
				{
					s.FolkPace = v;
				},
				Show = show
			});
			list.Add(Sectioned("Time", new Row
			{
				Name = "Time of day",
				Note = "Full cycle, or a fixed hour (F7)",
				Help = "Cycle runs days and nights. Day, Golden and Night hold the sun at one hour for as long as you like. The game's own clock keeps running underneath",
				Choices = new string[4] { "Cycle", "Day", "Golden", "Night" },
				Chosen = (Settings s) => s.TimeMode,
				Choose = delegate(Settings s, int i)
				{
					s.TimeMode = i;
					mod.ForceRelight();
				}
			}));
			list.Add(new Row
			{
				Name = "Length of a day",
				Note = "Real time for one day and night",
				Help = "How many real minutes one day and night take in first person. Longer gives slow dawns and long nights. It does not change how fast years pass",
				Min = 2f,
				Max = 240f,
				Step = 1f,
				Ratio = true,
				Value = (Settings s) => s.DayMinutes,
				SetValue = delegate(Settings s, float v)
				{
					s.DayMinutes = v;
				},
				Show = (float v) => (!(v < 60f)) ? ((v / 60f).ToString("0.#") + " h") : (v.ToString("0") + " min")
			});
			list.Add(new Row
			{
				Name = "Night darkness",
				Note = "How dark nights get away from fires and lamps",
				Help = "How dark the land gets at night away from fires, lamps and lit windows. Classic is the game's own blue night. Very dark needs a torch in the woods",
				Choices = new string[3] { "Classic", "Dark", "Very dark" },
				Chosen = (Settings s) => s.NightDark,
				Choose = delegate(Settings s, int i)
				{
					s.NightDark = i;
				}
			});
			list.Add(Sectioned("World", new Row
			{
				Name = "Years pass",
				Note = "Slower years: longer lives, rarer wars. Walking and fighting keep their pace. Also in the map's hourglass",
				Help = "Slows the world's clock so a year takes longer. Lives, pregnancies, harvests, wars and the seasons all stretch with it. Walking, fighting and talking keep their pace. The map's hourglass sets the same thing",
				Choices = new string[4] { "Normal", "2× slower", "5× slower", "10× slower" },
				Chosen = (Settings s) => s.WorldPace,
				Choose = delegate(Settings s, int i)
				{
					s.WorldPace = i;
					WorldPace.Apply(i);
				}
			}));
			list.Add(new Row
			{
				Name = "Land",
				Note = "This world's land, chosen on its first visit. It can't be changed",
				Help = "Flat is the game's own level ground. Terrainy raises hills, gullies and basins from the map's heights. It is asked on a world's first visit in first person and then stays, since towns are built on it",
				Choices = new string[3] { "Not chosen", "Flat", "Terrainy" },
				Chosen = (Settings s) => ReliefWorld.Land,
				Choose = delegate
				{
				},
				Fixed = true
			});
			list.Add(Sectioned("Map", new Row
			{
				Name = "3D map",
				Note = "Show the world map in 3D (V)",
				Help = "Zoomed in, the map is drawn as the 3D world seen from above, with the game's interface over it. Off keeps the flat map. V switches while you play",
				Choices = choices,
				Chosen = (Settings s) => s.GodView ? 1 : 0,
				Choose = delegate(Settings s, int i)
				{
					mod.SetGodView(i == 1);
				}
			}));
			list.Add(new Row
			{
				Name = "3D map reach",
				Note = "Zoom at which the map returns to flat",
				Help = "How far out you can zoom before the 3D map fades back to the flat one. Higher keeps 3D longer and costs more at the far end",
				Min = 12f,
				Max = 150f,
				Step = 1f,
				Ratio = true,
				Value = (Settings s) => s.GodViewZoom,
				SetValue = delegate(Settings s, float v)
				{
					s.GodViewZoom = v;
				},
				Show = (float v) => v.ToString("0")
			});
			list.Add(new Row
			{
				Name = "Camera dive",
				Note = "Speed of the transition between map and first person",
				Help = "The camera's dive from the map into a creature's eyes and back out. Higher is faster. Cut skips it and switches at once",
				Min = 0f,
				Max = 4f,
				Step = 0.25f,
				Value = (Settings s) => s.DiveSpeed,
				SetValue = delegate(Settings s, float v)
				{
					s.DiveSpeed = v;
				},
				Show = (float v) => (!(v <= 0f)) ? (v.ToString("0.##") + "×") : "Cut"
			});
			list.Add(new Row
			{
				Name = "Houses on the map",
				Note = "Lit windows, smoke and shapes as in 3D",
				Help = "Lights draws lit windows, chimney smoke, doors and curtains on the flat map's houses. Full also gives them the 3D shapes. Off is the game's own houses",
				Choices = new string[3] { "Off", "Lights", "Full" },
				Chosen = (Settings s) => s.MapHouses,
				Choose = delegate(Settings s, int i)
				{
					s.MapHouses = i;
				}
			});
			list.Add(new Row
			{
				Name = "Look inside",
				Note = "Roofs fade under the mouse when zoomed in",
				Help = "Zoomed in close, the roof under the mouse fades so you see who is inside. Off keeps every roof on",
				Choices = choices,
				Chosen = (Settings s) => s.LookInside ? 1 : 0,
				Choose = delegate(Settings s, int i)
				{
					s.LookInside = i == 1;
				}
			});
			list.Add(new Row
			{
				Name = "Map night",
				Note = "The map darkens with the time of day",
				Help = "The flat map darkens as night falls and lights come on in the towns. Light is half as dark as the game's Age of Dark, Full is night. Off keeps the map in daylight",
				Choices = new string[3] { "Off", "Light", "Full" },
				Chosen = (Settings s) => s.MapNight,
				Choose = delegate(Settings s, int i)
				{
					s.MapNight = i;
				}
			});
			List<Row> list4 = list;
			list = new List<Row>();
			list.Add(Sectioned("Volume", new Row
			{
				Name = "Master volume",
				Note = "Overall volume in first person, game sound included",
				Help = "Everything you hear in first person, the game's own sounds and music too. On the map the game's own volume is back",
				Min = 0f,
				Max = 1f,
				Step = 0.05f,
				Value = (Settings s) => s.MasterVolume,
				SetValue = delegate(Settings s, float v)
				{
					s.MasterVolume = v;
				},
				Show = show2
			}));
			list.Add(new Row
			{
				Name = "Music",
				Note = "Scaled by the game's own music setting",
				Help = "The mod's own music in first person, over the game's music setting. Above 100% is louder than the game's. Off plays none",
				Min = 0f,
				Max = 2f,
				Step = 0.05f,
				Value = (Settings s) => s.MusicVolume,
				SetValue = delegate(Settings s, float v)
				{
					s.MusicVolume = v;
				},
				Show = show2
			});
			list.Add(new Row
			{
				Name = "Sound effects",
				Note = "Combat, weapons and weather",
				Help = "The mod's own sounds. Blows, bows and guns, blocks, wind and rain, doors, crafting and the rest. Above 100% is louder than the game's",
				Min = 0f,
				Max = 2f,
				Step = 0.05f,
				Value = (Settings s) => s.EffectsVolume,
				SetValue = delegate(Settings s, float v)
				{
					s.EffectsVolume = v;
				},
				Show = show2
			});
			list.Add(new Row
			{
				Name = "Footsteps",
				Note = "Volume of your own steps",
				Help = "Your own footsteps on grass, sand, stone, snow, boards and water. Others' steps follow the sound effects",
				Min = 0f,
				Max = 1f,
				Step = 0.05f,
				Value = (Settings s) => s.StepVolume,
				SetValue = delegate(Settings s, float v)
				{
					s.StepVolume = v;
				},
				Show = show2
			});
			list.Add(new Row
			{
				Name = "Birds and night sounds",
				Note = "Wildlife ambience by day and night",
				Help = "Birds by day, crickets and owls by night, the sea on a shore and the wind in the trees. Off leaves only what things do",
				Min = 0f,
				Max = 2f,
				Step = 0.05f,
				Value = (Settings s) => s.AmbientVolume,
				SetValue = delegate(Settings s, float v)
				{
					s.AmbientVolume = v;
				},
				Show = show2
			});
			list.Add(Sectioned("Voices", new Row
			{
				Name = "Voices",
				Note = "People speaking in talks and in town",
				Help = "The mumbled voices people speak in talks with you and to each other in town. Off leaves the words on screen only",
				Min = 0f,
				Max = 1f,
				Step = 0.05f,
				Value = (Settings s) => s.VoicesVolume,
				SetValue = delegate(Settings s, float v)
				{
					s.VoicesVolume = v;
				},
				Show = show2
			}));
			list.Add(new Row
			{
				Name = "Voice style",
				Note = "How much of each line is spoken",
				Help = "Whole line mumbles every line to its end. Opening words speaks only the first few and lets you read the rest. Off speaks nothing",
				Choices = new string[3] { "Whole line", "Opening words", "Off" },
				Chosen = (Settings s) => s.VoiceStyle,
				Choose = delegate(Settings s, int i)
				{
					s.VoiceStyle = i;
				}
			});
			list.Add(Sectioned("Output", new Row
			{
				Name = "3D sound",
				Note = "Positional audio in every direction. Best with headphones",
				Help = "Places every sound where it comes from, behind and above you included, so you can hear where a blow or a shout came from. Off is plain left and right",
				Choices = choices,
				Chosen = (Settings s) => s.SpatialSound ? 1 : 0,
				Choose = delegate(Settings s, int i)
				{
					s.SpatialSound = i == 1;
				}
			}));
			List<Row> list5 = list;
			list = new List<Row>();
			list.Add(Sectioned("Headset", VrRow = new Row
			{
				Name = "VR headset",
				Note = "For Quest Link, Air Link, SteamVR or Virtual Desktop",
				Help = "Draws first person and the map into a headset over OpenXR. Works with Quest Link, Air Link, SteamVR and Virtual Desktop. The screen keeps showing the game",
				Choices = choices,
				Chosen = (Settings s) => s.Vr ? 1 : 0,
				Choose = delegate(Settings s, int i)
				{
					s.Vr = i == 1;
				}
			}));
			list.Add(new Row
			{
				Name = "Eye resolution",
				Note = "Render width per eye. Higher is sharper but slower",
				Help = "The width of each eye's picture in pixels. Higher is sharper and costs more, and both eyes are drawn every frame",
				Choices = Names(WorldBoxMod.VrEyeWidths, (int w) => w.ToString()),
				Chosen = (Settings s) => s.VrEyeWidth,
				Choose = delegate(Settings s, int i)
				{
					s.VrEyeWidth = i;
				}
			});
			list.Add(new Row
			{
				Name = "Eye detail",
				Note = "Fine renders at double size for cleaner edges. Four times the cost",
				Help = "Fine draws each eye at twice the size and shrinks it, which smooths jagged edges and far detail. It costs four times the work of Fast",
				Choices = new string[2] { "Fast", "Fine" },
				Chosen = (Settings s) => (s.VrSupersample >= 2) ? 1 : 0,
				Choose = delegate(Settings s, int i)
				{
					s.VrSupersample = i + 1;
				}
			});
			list.Add(Sectioned("Turning", new Row
			{
				Name = "Turning",
				Note = "Right stick turns in fixed steps or continuously",
				Help = "Snap turns in fixed steps, which most people find easier on the stomach. Smooth turns as long as the stick is held",
				Choices = new string[2] { "Snap", "Smooth" },
				Chosen = (Settings s) => s.VrTurn,
				Choose = delegate(Settings s, int i)
				{
					s.VrTurn = i;
				}
			}));
			list.Add(new Row
			{
				Name = "Snap angle",
				Note = "Angle of each snap turn",
				Help = "How far one snap turns you, in degrees. 45 is the usual",
				Min = 15f,
				Max = 90f,
				Step = 15f,
				Value = (Settings s) => s.VrSnapAngle,
				SetValue = delegate(Settings s, float v)
				{
					s.VrSnapAngle = v;
				},
				Show = (float v) => v.ToString("0") + "°"
			});
			list.Add(new Row
			{
				Name = "Smooth turn speed",
				Note = "Rotation speed for smooth turning",
				Help = "How fast a smooth turn goes with the stick held all the way, in degrees a second",
				Min = 30f,
				Max = 360f,
				Step = 10f,
				Value = (Settings s) => s.VrTurnSpeed,
				SetValue = delegate(Settings s, float v)
				{
					s.VrTurnSpeed = v;
				},
				Show = (float v) => v.ToString("0") + "°/s"
			});
			list.Add(Sectioned("Body", new Row
			{
				Name = "World size",
				Note = "Scale of the world. At 100% your creature matches your height",
				Help = "How big the world is around you. At 100% your creature is your own height. Smaller makes you a giant over a toy town, bigger makes the houses loom",
				Min = 0.5f,
				Max = 3f,
				Step = 0.05f,
				Ratio = true,
				Value = (Settings s) => s.VrWorldSize,
				SetValue = delegate(Settings s, float v)
				{
					s.VrWorldSize = v;
				},
				Show = show
			}));
			list.Add(new Row
			{
				Name = "Hands",
				Note = "Controllers: your arms follow them and shots go where the hand points. Fixed: held before you, shots at the crosshair",
				Help = "Controllers tracks both hands. Your arms follow them, a swing is your swing and a bow shoots where the hand points. Fixed holds your hands before you and shoots at the crosshair",
				Choices = new string[2] { "Controllers", "Fixed" },
				Chosen = (Settings s) => s.VrHands,
				Choose = delegate(Settings s, int i)
				{
					s.VrHands = i;
				}
			});
			list.Add(new Row
			{
				Name = "Main hand",
				Note = "The hand that attacks, uses and holds the weapon. Left swaps every button and stick",
				Help = "Which hand holds the weapon, attacks and uses things. Left swaps every button and both sticks between the controllers",
				Choices = new string[2] { "Right", "Left" },
				Chosen = (Settings s) => s.VrHanded,
				Choose = delegate(Settings s, int i)
				{
					s.VrHanded = i;
				}
			});
			list.Add(new Row
			{
				Name = "Taking control",
				Note = "How the view goes into a creature and back out to the map",
				Help = "Fade goes dark for a moment between the map and a creature's eyes. Dive flies the camera down into them as on the screen, which some find dizzying",
				Choices = new string[2] { "Fade", "Dive" },
				Chosen = (Settings s) => s.VrDive,
				Choose = delegate(Settings s, int i)
				{
					s.VrDive = i;
				}
			});
			list.Add(Sectioned("Interface", new Row
			{
				Name = "HUD size",
				Note = "How much of your view the HUD, windows and the map fill. 100% is all of it",
				Help = "How much of the headset's view the HUD, the windows and the map fill. Lower keeps them in the middle where the lenses are sharpest",
				Min = 0.5f,
				Max = 1f,
				Step = 0.05f,
				Value = (Settings s) => s.VrHudView,
				SetValue = delegate(Settings s, float v)
				{
					s.VrHudView = v;
				},
				Show = show
			}));
			List<Row> list6 = list;
			list = new List<Row>();
			list.Add(Sectioned("Fighting", new Row
			{
				Name = "Friendly fire",
				Note = "Your attacks can harm allies",
				Help = "On, your blows, arrows and spells hurt your own side when they land on them, and they hold it against you. Off, they pass allies by",
				Choices = choices,
				Chosen = (Settings s) => s.FriendlyFire ? 1 : 0,
				Choose = delegate(Settings s, int i)
				{
					s.FriendlyFire = i == 1;
				}
			}));
			list.Add(new Row
			{
				Name = "Parry timing",
				Note = "Time to parry after raising your guard (right click)",
				Help = "How long after raising your guard a blow still counts as parried. Easy gives the most time, Hard a short window. A parry stuns the attacker and makes your next blow a critical",
				Choices = new string[3] { "Easy", "Normal", "Hard" },
				Chosen = (Settings s) => s.ParryTiming,
				Choose = delegate(Settings s, int i)
				{
					s.ParryTiming = i;
				}
			});
			list.Add(new Row
			{
				Name = "Enemy wind-ups",
				Note = "Enemies draw back before striking you",
				Help = "On, an enemy draws its weapon back with a sound before a blow at you, so you can block or parry it. Off, blows land as fast as the game throws them",
				Choices = choices,
				Chosen = (Settings s) => s.EnemyWindups ? 1 : 0,
				Choose = delegate(Settings s, int i)
				{
					s.EnemyWindups = i == 1;
				}
			});
			list.Add(new Row
			{
				Name = "Allies answer pings",
				Note = "Nearby warriors attack enemies you ping",
				Help = "On, warriors of your side near you go for an enemy you ping (C). Off, a ping only marks it",
				Choices = choices,
				Chosen = (Settings s) => s.PingAllies ? 1 : 0,
				Choose = delegate(Settings s, int i)
				{
					s.PingAllies = i == 1;
				}
			});
			list.Add(Sectioned("Company", new Row
			{
				Name = "Family follows you",
				Note = "Your partner and children travel and settle with you",
				Help = "On, your partner and children walk with you when you leave a town and settle where you settle. Off, they stay with their own town",
				Choices = choices,
				Chosen = (Settings s) => s.FamilyFollows ? 1 : 0,
				Choose = delegate(Settings s, int i)
				{
					s.FamilyFollows = i == 1;
				}
			}));
			list.Add(Sectioned("On screen", new Row
			{
				Name = "Markers",
				Note = "Overhead markers for enemies, allies and family",
				Help = "Small marks over people's heads. Foes marks enemies only. Both also marks allies and your family. Off draws none",
				Choices = new string[3] { "Off", "Foes", "Both" },
				Chosen = (Settings s) => s.Markers,
				Choose = delegate(Settings s, int i)
				{
					s.Markers = i;
				}
			}));
			list.Add(new Row
			{
				Name = "Minimap",
				Note = "The land your creature has explored (N)",
				Help = "A small map in the corner of the land your creature has seen, with your pings and marks on it. Off hides it. N shows and hides it while you play",
				Choices = choices,
				Chosen = (Settings s) => s.Minimap ? 1 : 0,
				Choose = delegate(Settings s, int i)
				{
					s.Minimap = i == 1;
				}
			});
			list.Add(new Row
			{
				Name = "Minimap reach",
				Note = "Radius in tiles",
				Help = "How many tiles out from you the minimap shows. Wider shows more land at a smaller scale",
				Min = 16f,
				Max = 160f,
				Step = 1f,
				Ratio = true,
				Value = (Settings s) => s.MinimapReach,
				SetValue = delegate(Settings s, float v)
				{
					s.MinimapReach = v;
				},
				Show = (float v) => v.ToString("0")
			});
			list.Add(new Row
			{
				Name = "News alerts",
				Note = "One banner a minute at most. Full record on M > News",
				Help = "A small banner under the compass for your realm's news. Attacks shows wars declared on you and cities of yours under attack. Realm news adds your king's fate, wars ended and alliances. Everything is listed under M in any case",
				Choices = new string[3] { "Off", "Attacks", "Realm news" },
				Chosen = (Settings s) => s.NewsAlerts,
				Choose = delegate(Settings s, int i)
				{
					s.NewsAlerts = i;
				}
			});
			List<Row> list7 = list;
			_rows = new List<Row>[8]
			{
				list2,
				list3,
				list4,
				list5,
				list6,
				list7,
				new List<Row>(),
				new List<Row>()
			};
		}

		private static Row Sectioned(string heading, Row row)
		{
			row.Heading = heading;
			return row;
		}

		internal static int QualityOf(Settings s)
		{
			for (int i = 0; i < 4; i++)
			{
				if (!(Mathf.Abs(s.DrawDistance - PresetDraw[i]) > 1f) && !(Mathf.Abs(s.DetailDistance - PresetDetail[i]) > 1f) && !(Mathf.Abs(s.CreatureDistance - PresetCreature[i]) > 1f) && s.Shadows == PresetShadows[i] && !(Mathf.Abs(s.ShadowDistance - PresetShadowReach[i]) > 1f) && !(Mathf.Abs(s.BiomeParticles - PresetParticles[i]) > 0.01f) && s.WindowView == PresetWindows[i] && s.LensFlare == PresetFlare[i] && s.FarModels == PresetFarModels[i])
				{
					return i;
				}
			}
			return 4;
		}

		internal static void SetQuality(Settings s, int i)
		{
			if (i >= 0 && i <= 3)
			{
				s.DrawDistance = PresetDraw[i];
				s.DetailDistance = PresetDetail[i];
				s.CreatureDistance = PresetCreature[i];
				s.Shadows = PresetShadows[i];
				s.ShadowDistance = PresetShadowReach[i];
				s.BiomeParticles = PresetParticles[i];
				s.WindowView = PresetWindows[i];
				s.LensFlare = PresetFlare[i];
				s.FarModels = PresetFarModels[i];
			}
		}

		private static Row LightRow()
		{
			Row row = new Row();
			row.Name = "Light mode";
			row.Note = "Turns off costly effects for a higher frame rate. Auto is on for phones";
			row.Help = "On turns off the costliest effects at once, the shadow map, the finish, the far reach, and the rows it overrides show its values while it lasts. Auto turns it on for phones only. Off leaves every row as you set it";
			row.Choices = new string[3] { "Auto", "On", "Off" };
			row.Chosen = (Settings s) => s.Light;
			row.Choose = delegate(Settings s, int i)
			{
				s.Light = i;
			};
			return row;
		}

		private static Row QualityRow()
		{
			return new Row
			{
				Name = "Quality",
				Note = "Sets the distances, shadows and particles below. Changing one makes it Custom",
				Help = "One press sets the draw, detail, creature and shadow distances, the shadow level, the particles, lens flare, far models and house windows together. Medium is a new player's defaults. Change any of them by hand and it reads Custom",
				Choices = QualityNames,
				Chosen = QualityOf,
				Choose = SetQuality
			};
		}

		private static string[] Names<T>(T[] values, Func<T, string> name)
		{
			string[] array = new string[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				array[i] = name(values[i]);
			}
			return array;
		}

		public void Pick(Row row, Settings s, int choice)
		{
			if (!row.Fixed && row.Chosen(s) != choice)
			{
				row.Choose(s, choice);
				Changed = true;
			}
		}

		public void Slide(Row row, Settings s, float value)
		{
			if (!(Mathf.Abs(row.Value(s) - value) < row.Step * 0.25f + 1E-05f))
			{
				row.SetValue(s, value);
				Changed = true;
			}
		}

		public void Defaults(Settings s)
		{
			List<Row> list = RowsOf(Tab);
			if (list == null)
			{
				return;
			}
			Settings arg = new Settings();
			foreach (Row item in list)
			{
				if (item.IsChoice)
				{
					Pick(item, s, item.Chosen(arg));
				}
				else
				{
					Slide(item, s, item.Value(arg));
				}
			}
		}
	}
}
