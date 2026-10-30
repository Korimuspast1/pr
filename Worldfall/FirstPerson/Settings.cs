using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace FirstPerson
{
	internal sealed class Settings
	{
		public float EyeHeight = 1f;

		public float FieldOfView = 75f;

		public float MouseSensitivity = 2.2f;

		public int Resolution;

		public bool ResolutionTrial;

		public float DrawDistance = 270f;

		public float DetailDistance = 80f;

		public float CreatureDistance = 200f;

		public int FarModels = 1;

		public bool ScreenShake = true;

		public int TimeMode;

		public float DayMinutes = 20f;

		public int Palette = 1;

		public float RunSpeed = 0.6f;

		public int ShowBody = 1;

		public int PoseView = 1;

		public float FolkPace = 0.5f;

		public bool WindGusts = true;

		public int BiomeFx = 2;

		public float BiomeParticles = 1f;

		public int NightDark = 1;

		public float StepVolume = 0.8f;

		public float AmbientVolume = 1f;

		public float MusicVolume = 1f;

		public float MasterVolume = 1f;

		public float EffectsVolume = 1f;

		public bool SpatialSound = true;

		public float VoicesVolume = 1f;

		public int VoiceStyle;

		public float HeadBob = 1f;

		public float DiveSpeed = 1f;

		public bool TipsShown;

		public bool GodView;

		public float GodViewZoom = 40f;

		public float FlySpeed = 1f;

		public bool DigSeen;

		public bool MenuSeen;

		public bool AsyncRender = true;

		public bool VSync = true;

		public int FrameCap;

		public bool ShowFps;

		public bool GpuRender = true;

		public int GpuLook = 1;

		public int PixelDistance;

		public int OffscreenMotion = 1;

		public int FarPoses;

		public int FarCreatures = 1;

		public int ChunkWalk;

		public int CrowdDetail;

		public int WorldSteps;

		public int WindowView = 1;

		public int DarkFantasy;

		public int Shadows = 2;

		public float ShadowDistance = 64f;

		public int ShadowSoftness;

		public float ShadowDarkness = 1f;

		public int SunStyle;

		public bool LensFlare = true;

		public bool Minimap = true;

		public float MinimapReach = 40f;

		public bool PingAllies = true;

		public int NewsAlerts = 1;

		public int MapHouses = 2;

		public bool LookInside = true;

		public int MapNight = 1;

		public int WorldPace;

		public bool FriendlyFire;

		public int ParryTiming = 1;

		public bool EnemyWindups = true;

		public float UiScale = 1f;

		public bool FamilyFollows;

		public int Markers;

		public int TouchControls;

		public float TouchLook = 1f;

		public bool Controller = true;

		public float PadLook = 1f;

		public bool PadInvert;

		public float PadAimSlow = 0.5f;

		public int PadPictures;

		public bool PhoneTuned;

		public int Light;

		public bool LightTold;

		public bool Vr;

		public int VrTurn;

		public float VrSnapAngle = 30f;

		public float VrTurnSpeed = 150f;

		public int VrEyeWidth = 2;

		public int VrSupersample = 1;

		public float VrHudView = 1f;

		public float VrWorldSize = 1f;

		public int VrDive;

		public int VrHands;

		public int VrHanded;

		public string Packs = "";

		public string Language = "auto";

		public const float WholeMap = 1200f;

		private static readonly float[] OldViewDistances = new float[3] { 180f, 270f, 390f };

		private const int Version = 6;

		private static readonly int[] OldResolutions = new int[3] { 0, 0, 1 };

		public bool Upgraded;

		public bool Fresh;

		private static string FilePath => Path.Combine(Log.Folder, "settings.txt");

		public static Settings Load()
		{
			Settings settings = new Settings();
			try
			{
				if (!File.Exists(FilePath))
				{
					settings.Fresh = true;
					return settings;
				}
				int num = 1;
				bool flag = false;
				string[] array = File.ReadAllLines(FilePath);
				foreach (string text in array)
				{
					int num2 = text.IndexOf('=');
					if (num2 > 0)
					{
						string text2 = text.Substring(0, num2).Trim();
						string text3 = text.Substring(num2 + 1).Trim();
						switch (text2)
						{
						case "version":
							num = (int)ParseFloat(text3, 1f);
							break;
						case "eye_height":
							settings.EyeHeight = Clamp(ParseFloat(text3, settings.EyeHeight), 0.3f, 4f);
							break;
						case "fov":
							settings.FieldOfView = Clamp(ParseFloat(text3, settings.FieldOfView), 50f, 110f);
							break;
						case "mouse_sensitivity":
							settings.MouseSensitivity = Clamp(ParseFloat(text3, settings.MouseSensitivity), 0.2f, 10f);
							break;
						case "resolution":
							settings.Resolution = (int)ParseFloat(text3, settings.Resolution);
							break;
						case "resolution_trial":
							settings.ResolutionTrial = IsOn(text3);
							break;
						case "view_distance":
							settings.DrawDistance = OldViewDistances[Math.Max(0, Math.Min(2, (int)ParseFloat(text3, 1f)))];
							break;
						case "draw_distance":
							settings.DrawDistance = (text3.Equals("whole", StringComparison.OrdinalIgnoreCase) ? 1200f : Clamp(ParseFloat(text3, settings.DrawDistance), 100f, 1200f));
							break;
						case "detail_distance":
							settings.DetailDistance = Clamp(ParseFloat(text3, settings.DetailDistance), 30f, 240f);
							break;
						case "creature_distance":
							settings.CreatureDistance = Clamp(ParseFloat(text3, settings.CreatureDistance), 60f, 1200f);
							break;
						case "far_models":
							settings.FarModels = Math.Max(0, Math.Min(1, (int)ParseFloat(text3, settings.FarModels)));
							flag = true;
							break;
						case "screen_shake":
							settings.ScreenShake = IsOn(text3);
							break;
						case "time_of_day":
							settings.TimeMode = Math.Max(0, Math.Min(3, (int)ParseFloat(text3, settings.TimeMode)));
							break;
						case "day_minutes":
							settings.DayMinutes = Clamp(ParseFloat(text3, settings.DayMinutes), 2f, 240f);
							break;
						case "palette":
						case "retro":
							settings.Palette = Math.Max(0, Math.Min(1, (int)ParseFloat(text3, settings.Palette)));
							break;
						case "walk_speed":
							settings.RunSpeed = Clamp(ParseFloat(text3, settings.RunSpeed), 0.2f, 1f);
							break;
						case "own_body":
							settings.ShowBody = Math.Max(0, Math.Min(2, (int)ParseFloat(text3, settings.ShowBody)));
							break;
						case "pose_view":
							settings.PoseView = Math.Max(0, Math.Min(1, (int)ParseFloat(text3, settings.PoseView)));
							break;
						case "folk_pace":
							settings.FolkPace = Clamp(ParseFloat(text3, settings.FolkPace), 0.3f, 1f);
							break;
						case "wind_gusts":
							settings.WindGusts = IsOn(text3);
							break;
						case "biome_fx":
							settings.BiomeFx = Math.Max(0, Math.Min(2, (int)ParseFloat(text3, settings.BiomeFx)));
							break;
						case "biome_particles":
							settings.BiomeParticles = Clamp(ParseFloat(text3, settings.BiomeParticles), 0f, 2f);
							break;
						case "night_dark":
							settings.NightDark = Math.Max(0, Math.Min(2, (int)ParseFloat(text3, settings.NightDark)));
							break;
						case "step_volume":
							settings.StepVolume = Clamp(ParseFloat(text3, settings.StepVolume), 0f, 1f);
							break;
						case "ambient_volume":
							settings.AmbientVolume = Clamp(ParseFloat(text3, settings.AmbientVolume), 0f, 2f);
							break;
						case "music_volume":
							settings.MusicVolume = Clamp(ParseFloat(text3, settings.MusicVolume), 0f, 2f);
							break;
						case "master_volume":
							settings.MasterVolume = Clamp(ParseFloat(text3, settings.MasterVolume), 0f, 1f);
							break;
						case "effects_volume":
							settings.EffectsVolume = Clamp(ParseFloat(text3, settings.EffectsVolume), 0f, 2f);
							break;
						case "spatial_sound":
							settings.SpatialSound = IsOn(text3);
							break;
						case "voices_volume":
							settings.VoicesVolume = Clamp(ParseFloat(text3, settings.VoicesVolume), 0f, 1f);
							break;
						case "voice_style":
							settings.VoiceStyle = Math.Max(0, Math.Min(2, (int)ParseFloat(text3, settings.VoiceStyle)));
							break;
						case "head_bob":
							settings.HeadBob = Clamp(ParseFloat(text3, settings.HeadBob), 0f, 1f);
							break;
						case "dive_speed":
							settings.DiveSpeed = Clamp(ParseFloat(text3, settings.DiveSpeed), 0f, 4f);
							break;
						case "tips_shown":
							settings.TipsShown = IsOn(text3);
							break;
						case "god_view":
							settings.GodView = IsOn(text3);
							break;
						case "god_view_zoom":
							settings.GodViewZoom = Clamp(ParseFloat(text3, settings.GodViewZoom), 12f, 150f);
							break;
						case "fly_speed":
							settings.FlySpeed = Clamp(ParseFloat(text3, settings.FlySpeed), 0.1f, 20f);
							break;
						case "menu_seen":
							settings.MenuSeen = IsOn(text3);
							break;
						case "dig_seen":
							settings.DigSeen = IsOn(text3);
							break;
						case "async_render":
							settings.AsyncRender = IsOn(text3);
							break;
						case "gpu_render":
							settings.GpuRender = IsOn(text3);
							break;
						case "pixel_distance":
							settings.PixelDistance = Math.Max(0, Math.Min(1, (int)ParseFloat(text3, settings.PixelDistance)));
							break;
						case "offscreen_motion":
							settings.OffscreenMotion = Math.Max(0, Math.Min(1, (int)ParseFloat(text3, settings.OffscreenMotion)));
							break;
						case "far_poses":
							settings.FarPoses = Math.Max(0, Math.Min(1, (int)ParseFloat(text3, settings.FarPoses)));
							break;
						case "far_creatures":
							settings.FarCreatures = Math.Max(0, Math.Min(1, (int)ParseFloat(text3, settings.FarCreatures)));
							break;
						case "chunk_walk":
							settings.ChunkWalk = Math.Max(0, Math.Min(1, (int)ParseFloat(text3, settings.ChunkWalk)));
							break;
						case "crowd_detail":
							settings.CrowdDetail = Math.Max(0, Math.Min(3, (int)ParseFloat(text3, settings.CrowdDetail)));
							break;
						case "world_steps":
							settings.WorldSteps = Math.Max(0, Math.Min(1, (int)ParseFloat(text3, settings.WorldSteps)));
							break;
						case "window_view":
							settings.WindowView = Math.Max(0, Math.Min(1, (int)ParseFloat(text3, settings.WindowView)));
							break;
						case "dark_fantasy":
							settings.DarkFantasy = Math.Max(0, Math.Min(1, (int)ParseFloat(text3, settings.DarkFantasy)));
							break;
						case "shadows":
							settings.Shadows = Math.Max(0, Math.Min(4, (int)ParseFloat(text3, settings.Shadows)));
							break;
						case "shadow_distance":
							settings.ShadowDistance = Clamp(ParseFloat(text3, settings.ShadowDistance), 24f, 192f);
							break;
						case "shadow_softness":
							settings.ShadowSoftness = Math.Max(0, Math.Min(2, (int)ParseFloat(text3, settings.ShadowSoftness)));
							break;
						case "shadow_darkness":
							settings.ShadowDarkness = Clamp(ParseFloat(text3, settings.ShadowDarkness), 0f, 1f);
							break;
						case "sun_style":
							settings.SunStyle = Math.Max(0, Math.Min(2, (int)ParseFloat(text3, settings.SunStyle)));
							break;
						case "lens_flare":
							settings.LensFlare = IsOn(text3);
							break;
						case "vsync":
							settings.VSync = IsOn(text3);
							break;
						case "frame_cap":
							settings.FrameCap = Math.Max(0, Math.Min(FrameCaps.Rates.Length - 1, (int)ParseFloat(text3, settings.FrameCap)));
							break;
						case "show_fps":
							settings.ShowFps = IsOn(text3);
							break;
						case "minimap":
							settings.Minimap = IsOn(text3);
							break;
						case "minimap_reach":
							settings.MinimapReach = Clamp(ParseFloat(text3, settings.MinimapReach), 16f, 160f);
							break;
						case "ping_allies":
							settings.PingAllies = IsOn(text3);
							break;
						case "news_alerts":
							settings.NewsAlerts = Math.Max(0, Math.Min(2, (int)ParseFloat(text3, settings.NewsAlerts)));
							break;
						case "map_houses":
							settings.MapHouses = Math.Max(0, Math.Min(2, (int)ParseFloat(text3, settings.MapHouses)));
							break;
						case "look_inside":
							settings.LookInside = IsOn(text3);
							break;
						case "map_night":
							settings.MapNight = Math.Max(0, Math.Min(2, (int)ParseFloat(text3, settings.MapNight)));
							break;
						case "world_pace":
							settings.WorldPace = Math.Max(0, Math.Min(3, (int)ParseFloat(text3, settings.WorldPace)));
							break;
						case "friendly_fire":
							settings.FriendlyFire = IsOn(text3);
							break;
						case "parry_timing":
							settings.ParryTiming = Math.Max(0, Math.Min(2, (int)ParseFloat(text3, settings.ParryTiming)));
							break;
						case "enemy_windups":
							settings.EnemyWindups = IsOn(text3);
							break;
						case "ui_scale":
							settings.UiScale = Clamp(ParseFloat(text3, settings.UiScale), 0.8f, 1.6f);
							break;
						case "family_follows":
							settings.FamilyFollows = IsOn(text3);
							break;
						case "markers":
							settings.Markers = Math.Max(0, Math.Min(2, (int)ParseFloat(text3, settings.Markers)));
							break;
						case "touch_controls":
							settings.TouchControls = Math.Max(0, Math.Min(2, (int)ParseFloat(text3, settings.TouchControls)));
							break;
						case "touch_look":
							settings.TouchLook = Clamp(ParseFloat(text3, settings.TouchLook), 0.3f, 3f);
							break;
						case "controller":
							settings.Controller = IsOn(text3);
							break;
						case "pad_look":
							settings.PadLook = Clamp(ParseFloat(text3, settings.PadLook), 0.3f, 3f);
							break;
						case "pad_invert":
							settings.PadInvert = IsOn(text3);
							break;
						case "pad_aim_slow":
							settings.PadAimSlow = Clamp(ParseFloat(text3, settings.PadAimSlow), 0.2f, 1f);
							break;
						case "pad_pictures":
							settings.PadPictures = Math.Max(0, Math.Min(3, (int)ParseFloat(text3, settings.PadPictures)));
							break;
						case "phone_tuned":
							settings.PhoneTuned = IsOn(text3);
							break;
						case "light_mode":
							settings.Light = Math.Max(0, Math.Min(2, (int)ParseFloat(text3, settings.Light)));
							break;
						case "light_told":
							settings.LightTold = IsOn(text3);
							break;
						case "vr":
							settings.Vr = IsOn(text3);
							break;
						case "vr_turn":
							settings.VrTurn = Math.Max(0, Math.Min(1, (int)ParseFloat(text3, settings.VrTurn)));
							break;
						case "vr_snap_angle":
							settings.VrSnapAngle = Clamp(ParseFloat(text3, settings.VrSnapAngle), 10f, 90f);
							break;
						case "vr_turn_speed":
							settings.VrTurnSpeed = Clamp(ParseFloat(text3, settings.VrTurnSpeed), 30f, 360f);
							break;
						case "vr_eye_width":
							settings.VrEyeWidth = Math.Max(0, Math.Min(4, (int)ParseFloat(text3, settings.VrEyeWidth)));
							break;
						case "vr_supersample":
							settings.VrSupersample = Math.Max(1, Math.Min(2, (int)ParseFloat(text3, settings.VrSupersample)));
							break;
						case "vr_hud_view":
							settings.VrHudView = Clamp(ParseFloat(text3, settings.VrHudView), 0.5f, 1f);
							break;
						case "vr_world_size":
							settings.VrWorldSize = Clamp(ParseFloat(text3, settings.VrWorldSize), 0.5f, 3f);
							break;
						case "vr_dive":
							settings.VrDive = Math.Max(0, Math.Min(1, (int)ParseFloat(text3, settings.VrDive)));
							break;
						case "vr_hands":
							settings.VrHands = Math.Max(0, Math.Min(1, (int)ParseFloat(text3, settings.VrHands)));
							break;
						case "vr_handed":
							settings.VrHanded = Math.Max(0, Math.Min(1, (int)ParseFloat(text3, settings.VrHanded)));
							break;
						case "language":
							settings.Language = ((text3.Length == 0) ? "auto" : text3);
							break;
						case "packs":
							settings.Packs = text3;
							break;
						}
					}
				}
				if (num < 2)
				{
					settings.FieldOfView = 75f;
					settings.EyeHeight = 1f;
					settings.Upgraded = true;
					Log.Info("settings upgraded: field of view and eye height reset to the new defaults");
				}
				if (num < 3)
				{
					settings.GpuRender = true;
					settings.Resolution = OldResolutions[Math.Max(0, Math.Min(OldResolutions.Length - 1, settings.Resolution))];
					settings.Upgraded = true;
					Log.Info("settings upgraded: drawn on the graphics card, resolution index " + settings.Resolution);
				}
				if (num < 4 && settings.Resolution == 2)
				{
					settings.Resolution = 3;
					settings.Upgraded = true;
					Log.Info("settings upgraded: resolution index 2 (1920) is now 3");
				}
				if (num < 5 && settings.OffscreenMotion == 0)
				{
					settings.OffscreenMotion = 1;
					settings.Upgraded = true;
					Log.Info("settings upgraded: off-screen creatures cheap");
				}
				if (num < 6 && (settings.OffscreenMotion == 0 || settings.FarCreatures == 0))
				{
					settings.OffscreenMotion = 1;
					settings.FarCreatures = 1;
					settings.Upgraded = true;
					Log.Info("settings upgraded: off-screen creatures cheap, far creatures budgeted (F2 Graphics turns either back)");
				}
				settings.Resolution = Math.Max(0, Math.Min(3, settings.Resolution));
				if (!flag)
				{
					settings.FarModels = 0;
					if (SettingsMenu.QualityOf(settings) != 0)
					{
						settings.FarModels = 1;
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("could not read settings: " + ex.Message);
			}
			return settings;
		}

		public void Save()
		{
			LightMode.Swap(this, light: false);
			try
			{
				SaveFile();
			}
			finally
			{
				LightMode.Swap(this, light: true);
			}
		}

		private void SaveFile()
		{
			try
			{
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.AppendLine("# First Person mod settings: F2 in the game changes them (edit this file only while the game is closed)");
				stringBuilder.AppendLine("version=" + 6);
				stringBuilder.AppendLine("eye_height=" + EyeHeight.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("fov=" + FieldOfView.ToString("0", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("mouse_sensitivity=" + MouseSensitivity.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# the picture: 0 = 480 wide (270p on a 16:9 screen), 1 = 640 (360p), 2 = half the screen (each pixel 2x2 on it),");
				stringBuilder.AppendLine("# 3 = 1920 (1080p); the CPU renderer stops at 640");
				stringBuilder.AppendLine("resolution=" + Resolution);
				if (ResolutionTrial)
				{
					stringBuilder.AppendLine("resolution_trial=1");
				}
				stringBuilder.AppendLine("# how far you see, in tiles (the further, the thinner the haze); whole: every part of the map, from wherever you stand");
				stringBuilder.AppendLine("draw_distance=" + ((DrawDistance >= 1200f) ? "whole" : DrawDistance.ToString("0", CultureInfo.InvariantCulture)));
				stringBuilder.AppendLine("# things are drawn whole (3D) out to here, in tiles; further off trees and buildings are simpler models or their sprites (far_models), creatures specks of colour");
				stringBuilder.AppendLine("detail_distance=" + DetailDistance.ToString("0", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# creatures (people, animals, monsters) stay 3D out to here, in tiles, also past the detail distance; " + 1200f.ToString("0", CultureInfo.InvariantCulture) + " = as far as you see");
				stringBuilder.AppendLine("creature_distance=" + CreatureDistance.ToString("0", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# trees and buildings past the detail distance: 0 their sprites, 1 their 3D models (drawn on the graphics card only)");
				stringBuilder.AppendLine("far_models=" + FarModels);
				stringBuilder.AppendLine("screen_shake=" + (ScreenShake ? "1" : "0"));
				stringBuilder.AppendLine("# 0 day and night, 1 always day, 2 golden hour, 3 always night (F7 in game)");
				stringBuilder.AppendLine("time_of_day=" + TimeMode);
				stringBuilder.AppendLine("# minutes of play for a whole day and night");
				stringBuilder.AppendLine("day_minutes=" + DayMinutes.ToString("0.#", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# palette filter: 0 off, 1 reduced colour palette with ordered dithering");
				stringBuilder.AppendLine("palette=" + Palette);
				stringBuilder.AppendLine("# run speed as a share of the creature's full speed; Shift sprints, Alt walks");
				stringBuilder.AppendLine("walk_speed=" + RunSpeed.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("own_body=" + ShowBody);
				stringBuilder.AppendLine("# sitting or kneeling (P) seen from your eyes (0) or from behind you (1); the mouse wheel switches while you rest");
				stringBuilder.AppendLine("pose_view=" + PoseView);
				stringBuilder.AppendLine("# in first person, the creatures near you walk at this share of their pace (fights and fleeing stay full speed; the world's clock is untouched)");
				stringBuilder.AppendLine("folk_pace=" + FolkPace.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# now and then a gust of wind: streaks of wind, leaves and grass whirled along, the grass and trees bending");
				stringBuilder.AppendLine("wind_gusts=" + (WindGusts ? "1" : "0"));
				stringBuilder.AppendLine("# each biome's own air, light and particles (mist, embers, petals, wisps...): 0 off, 1 subtle, 2 full");
				stringBuilder.AppendLine("biome_fx=" + BiomeFx);
				stringBuilder.AppendLine("# how many of the biomes' particles: 0 none, 1 as made, 2 twice as many");
				stringBuilder.AppendLine("biome_particles=" + BiomeParticles.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("night_dark=" + NightDark);
				stringBuilder.AppendLine("# footstep loudness, 0 silent .. 1 full");
				stringBuilder.AppendLine("step_volume=" + StepVolume.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# birdsong by day, crickets, owls and frogs by night: 0 silent, 1 normal (soft), 2 louder");
				stringBuilder.AppendLine("ambient_volume=" + AmbientVolume.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# first-person music (calm pieces now and then, battle music in a fight): 0 off, 1 normal, 2 louder");
				stringBuilder.AppendLine("music_volume=" + MusicVolume.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# everything you hear in first person, the game's own sounds too: 0 silent .. 1 full");
				stringBuilder.AppendLine("master_volume=" + MasterVolume.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# the mod's own sound effects (swings, hits, the bow, steps, wind and rain): 0 off, 1 normal, 2 louder");
				stringBuilder.AppendLine("effects_volume=" + EffectsVolume.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# 3D sound: each sound comes from where it is, in front, behind, above or below you (best with headphones): 1 on, 0 off");
				stringBuilder.AppendLine("spatial_sound=" + (SpatialSound ? "1" : "0"));
				stringBuilder.AppendLine("voices_volume=" + VoicesVolume.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("voice_style=" + VoiceStyle);
				stringBuilder.AppendLine("# how much the view bobs as you walk, 0 none .. 1 full");
				stringBuilder.AppendLine("head_bob=" + HeadBob.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# the camera's dive from the map into the creature's eyes and back: 1 normal, 2 twice as fast, 0 a straight cut");
				stringBuilder.AppendLine("dive_speed=" + DiveSpeed.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# the controls are shown the first time only (F1 shows them again); 0 to see them once more");
				stringBuilder.AppendLine("tips_shown=" + (TipsShown ? "1" : "0"));
				stringBuilder.AppendLine("# the map drawn as the 3D world (V on the map turns it on and off)");
				stringBuilder.AppendLine("god_view=" + (GodView ? "1" : "0"));
				stringBuilder.AppendLine("# how far out the 3D map shows (the map camera's zoom); further out it fades to the flat map. Higher costs frames");
				stringBuilder.AppendLine("god_view_zoom=" + GodViewZoom.ToString("0", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# free flight's speed (F over the map flies; the mouse wheel sets this while flying)");
				stringBuilder.AppendLine("fly_speed=" + FlySpeed.ToString("0.###", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# the settings menu (F2) has been opened: the map stops reminding you of it");
				stringBuilder.AppendLine("menu_seen=" + (MenuSeen ? "1" : "0"));
				stringBuilder.AppendLine("dig_seen=" + (DigSeen ? "1" : "0"));
				stringBuilder.AppendLine("# draw the first-person view while the game runs its own frame: more frames, the picture moved with your mouse so it keeps up; 0 draws it in step");
				stringBuilder.AppendLine("async_render=" + (AsyncRender ? "1" : "0"));
				stringBuilder.AppendLine("# 1 draws first person on the graphics card (looks right up and down, 1080p), rooms and cabins included; 0 on the CPU");
				stringBuilder.AppendLine("gpu_render=" + (GpuRender ? "1" : "0"));
				stringBuilder.AppendLine("# Classic: the graphics card draws the distance in crisp pixels, a 2D backdrop, and nights dark blue; 0 off");
				stringBuilder.AppendLine("pixel_distance=" + PixelDistance);
				stringBuilder.AppendLine("# creatures well out of view (behind you, off to the side) skip their body's smoothing each frame: 1 cheaper in big worlds, 0 as in view");
				stringBuilder.AppendLine("offscreen_motion=" + OffscreenMotion);
				stringBuilder.AppendLine("# far people's arms and legs are posed every 2nd frame past 60 tiles, every 4th past 120 (their bodies still move every frame): 1 on, 0 every frame");
				stringBuilder.AppendLine("far_poses=" + FarPoses);
				stringBuilder.AppendLine("# while drawn in 3D, creatures far from you and out of the frame think and move every 2nd to 4th step (1), or every step as the game does (0)");
				stringBuilder.AppendLine("far_creatures=" + FarCreatures);
				stringBuilder.AppendLine("# first person finds the creatures to draw in the map's chunks within view (1) instead of the game's whole list (0): about 1 ms a frame in a big world; 0 until it has been played with for a while");
				stringBuilder.AppendLine("chunk_walk=" + ChunkWalk);
				stringBuilder.AppendLine("# far people small on screen walked and posed by the graphics card from recorded motions: 0 Full (never), 1 High (under 6 pixels tall), 2 Medium (12), 3 Low (24)");
				stringBuilder.AppendLine("crowd_detail=" + CrowdDetail);
				stringBuilder.AppendLine("# the 3D god view: the world stepped every frame (0), or 30 times a second with two frames' time each, drawn at the full frame rate (1): smoother in big worlds");
				stringBuilder.AppendLine("world_steps=" + WorldSteps);
				stringBuilder.AppendLine("# in a house, its windows and open door show the town outside (1) or only the light of the sky (0); graphics card only");
				stringBuilder.AppendLine("window_view=" + WindowView);
				stringBuilder.AppendLine("# the dark fantasy look over the graphics card's finish: blue-violet fog, emerald greens, water glowing at night; 0 off");
				stringBuilder.AppendLine("dark_fantasy=" + DarkFantasy);
				stringBuilder.AppendLine("# the sun's shadows of trees, buildings and creatures: 0 off, 1 low, 2 medium, 3 high (a sharper map near you), 4 ultra");
				stringBuilder.AppendLine("shadows=" + Shadows);
				stringBuilder.AppendLine("# tiles round you that get those shadows (24..192); softness 0 crisp, 1 soft, 2 natural (graphics card only); darkness 0..1");
				stringBuilder.AppendLine("shadow_distance=" + ShadowDistance.ToString("0", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("shadow_softness=" + ShadowSoftness);
				stringBuilder.AppendLine("shadow_darkness=" + ShadowDarkness.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# the sun: 0 auto (natural while the graphics card draws), 1 classic (as before), 2 natural (seasons, moon phases, long shadows)");
				stringBuilder.AppendLine("sun_style=" + SunStyle);
				stringBuilder.AppendLine("# a lens flare when you face the natural sun: 1 on, 0 off");
				stringBuilder.AppendLine("lens_flare=" + (LensFlare ? "1" : "0"));
				stringBuilder.AppendLine("# wait for the screen's refresh while the world is drawn in 3D: no tearing when you turn; 0 can give a few more frames");
				stringBuilder.AppendLine("vsync=" + (VSync ? "1" : "0"));
				stringBuilder.AppendLine("# the most frames a second while the world is drawn in 3D: 0 the game's own cap, 1 30, 2 60, 3 90, 4 120, 5 144, 6 no cap");
				stringBuilder.AppendLine("frame_cap=" + FrameCap);
				stringBuilder.AppendLine("# a frame counter in the corner: 1 on, 0 off");
				stringBuilder.AppendLine("show_fps=" + (ShowFps ? "1" : "0"));
				stringBuilder.AppendLine("# the minimap top right in first person (N shows or hides it; hold N for the whole map you've explored)");
				stringBuilder.AppendLine("minimap=" + (Minimap ? "1" : "0"));
				stringBuilder.AppendLine("# how far the minimap reaches, in tiles from you to its rim");
				stringBuilder.AppendLine("minimap_reach=" + MinimapReach.ToString("0", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# your warriors near an enemy you ping (middle click) go for it");
				stringBuilder.AppendLine("ping_allies=" + (PingAllies ? "1" : "0"));
				stringBuilder.AppendLine("# news while you walk (M > News has it all): 0 no banners, 1 attacks on your realm (sieges, cities lost, wars on it), 2 also its crown and wars' ends; one small banner a minute at most");
				stringBuilder.AppendLine("news_alerts=" + NewsAlerts);
				stringBuilder.AppendLine("map_houses=" + MapHouses);
				stringBuilder.AppendLine("look_inside=" + (LookInside ? "1" : "0"));
				stringBuilder.AppendLine("map_night=" + MapNight);
				stringBuilder.AppendLine("world_pace=" + WorldPace);
				stringBuilder.AppendLine("# your blows and shots hurt your own side too (0: they pass over your kingdom and its allies)");
				stringBuilder.AppendLine("friendly_fire=" + (FriendlyFire ? "1" : "0"));
				stringBuilder.AppendLine("# how long after you raise your guard (right click) a blow is parried: 0 easy (0.3 s), 1 normal (0.2 s), 2 hard (0.12 s)");
				stringBuilder.AppendLine("parry_timing=" + ParryTiming);
				stringBuilder.AppendLine("# creatures about to strike you draw back first, with a sound (0: their blows land at once, as the game has them)");
				stringBuilder.AppendLine("enemy_windups=" + (EnemyWindups ? "1" : "0"));
				stringBuilder.AppendLine("# the HUD's and windows' size, over the game's own UI scale: 1 = as the game, up to 1.6 for a sharp screen");
				stringBuilder.AppendLine("ui_scale=" + UiScale.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# your lover and children walk with you, and leave or join a town when you do");
				stringBuilder.AppendLine("family_follows=" + (FamilyFollows ? "1" : "0"));
				stringBuilder.AppendLine("# marks over heads in first person: 0 none, 1 foes (red), 2 foes and friends (blue)");
				stringBuilder.AppendLine("markers=" + Markers);
				stringBuilder.AppendLine("# on-screen stick and buttons: 0 auto (on a phone, or once you touch the screen), 1 always, 2 never");
				stringBuilder.AppendLine("touch_controls=" + TouchControls);
				stringBuilder.AppendLine("# how fast a drag turns the view on a touchscreen: 1 = the screen's height is half a turn");
				stringBuilder.AppendLine("touch_look=" + TouchLook.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# a gamepad or the Steam Deck plays first person (0: the mod leaves the pad alone)");
				stringBuilder.AppendLine("controller=" + (Controller ? "1" : "0"));
				stringBuilder.AppendLine("# how fast the right stick turns the view (1: 170 degrees a second at a full push)");
				stringBuilder.AppendLine("pad_look=" + PadLook.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("pad_invert=" + (PadInvert ? "1" : "0"));
				stringBuilder.AppendLine("# a bow or a gun over a creature: the stick turns this much as fast (1: no slowing)");
				stringBuilder.AppendLine("pad_aim_slow=" + PadAimSlow.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# the buttons prompts show: 0 the pad in use, 1 Steam Deck, 2 Xbox, 3 PlayStation");
				stringBuilder.AppendLine("pad_pictures=" + PadPictures);
				stringBuilder.AppendLine("# the lighter settings for a phone were set once (0: set them again next time on a phone)");
				stringBuilder.AppendLine("phone_tuned=" + (PhoneTuned ? "1" : "0"));
				stringBuilder.AppendLine("light_mode=" + Light);
				stringBuilder.AppendLine("light_told=" + (LightTold ? "1" : "0"));
				stringBuilder.AppendLine("# play in a VR headset (OpenXR: Quest Link or Air Link, SteamVR, Virtual Desktop, Windows Mixed Reality); FIRSTPERSON_VR=1 turns it on too");
				stringBuilder.AppendLine("vr=" + (Vr ? "1" : "0"));
				stringBuilder.AppendLine("# the right stick turns you: 0 in snaps of vr_snap_angle degrees, 1 smoothly at vr_turn_speed degrees a second");
				stringBuilder.AppendLine("vr_turn=" + VrTurn);
				stringBuilder.AppendLine("vr_snap_angle=" + VrSnapAngle.ToString("0", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("vr_turn_speed=" + VrTurnSpeed.ToString("0", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# each eye's picture: 0 640, 1 800, 2 1024, 3 1280, 4 1600 pixels wide (higher is sharper and slower)");
				stringBuilder.AppendLine("vr_eye_width=" + VrEyeWidth);
				stringBuilder.AppendLine("# each eye drawn finer and brought down (2: four times the work), or as it is (1)");
				stringBuilder.AppendLine("vr_supersample=" + VrSupersample);
				stringBuilder.AppendLine("# how much of your view the HUD, windows and the map fill in a headset: 1 all of it, down to 0.5");
				stringBuilder.AppendLine("vr_hud_view=" + VrHudView.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# how big the world feels: 1 the creature you are is your height, 2 everything twice as big");
				stringBuilder.AppendLine("vr_world_size=" + VrWorldSize.ToString("0.##", CultureInfo.InvariantCulture));
				stringBuilder.AppendLine("# taking control of a creature or letting go, in a headset: 0 a quick fade, 1 the dive down into its eyes");
				stringBuilder.AppendLine("vr_dive=" + VrDive);
				stringBuilder.AppendLine("# your arms in a headset: 0 where the controllers are (shots go where the hand points), 1 held before you as on the screen");
				stringBuilder.AppendLine("vr_hands=" + VrHands);
				stringBuilder.AppendLine("# which hand fights in a headset: 0 the right, 1 the left (the controllers' buttons and sticks swapped)");
				stringBuilder.AppendLine("vr_handed=" + VrHanded);
				stringBuilder.AppendLine("# the mod's text: auto = the game's language, en = English, or the code of a file in lang/ (cz = Simplified Chinese)");
				stringBuilder.AppendLine("language=" + Language);
				stringBuilder.AppendLine("# add-on packs turned on, the top one first (F2 > Mods)");
				stringBuilder.AppendLine("packs=" + Packs);
				File.WriteAllText(FilePath, stringBuilder.ToString());
				Upgraded = false;
			}
			catch (Exception ex)
			{
				Log.Error("could not save settings: " + ex.Message);
			}
		}

		private static bool IsOn(string value)
		{
			if (value != "0")
			{
				return !value.Equals("false", StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}

		private static float ParseFloat(string s, float fallback)
		{
			if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
			{
				return fallback;
			}
			return result;
		}

		private static float Clamp(float v, float min, float max)
		{
			if (!(v < min))
			{
				if (!(v > max))
				{
					return v;
				}
				return max;
			}
			return min;
		}
	}
}
