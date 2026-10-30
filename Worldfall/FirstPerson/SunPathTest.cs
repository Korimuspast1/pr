using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using FirstPerson.Core;
using FirstPerson.Gpu;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class SunPathTest : MonoBehaviour
	{
		private struct View
		{
			public string Tag;

			public Vector2 Eye;

			public Vector2 Dir;

			public float Pitch;

			public float Up;
		}

		private struct Fixed
		{
			public Vec3 Sun;

			public Vec3 Moon;

			public Vec3 Light;

			public Vec3 Cast;

			public Vec3 Land;

			public float Hour;

			public static Fixed Now(WorldBoxMod mod)
			{
				Atmosphere air = mod.Day.Air;
				return new Fixed
				{
					Sun = air.SunDisc,
					Moon = air.MoonDisc,
					Light = air.Light,
					Cast = air.CastLight,
					Land = mod.SceneTerrain.LightDir,
					Hour = mod.Day.Hour
				};
			}

			public float Apart(Fixed b)
			{
				return Mathf.Max(Differ(Sun, b.Sun), Differ(Moon, b.Moon), Differ(Light, b.Light), Differ(Cast, b.Cast), Differ(Land, b.Land));
			}

			public override string ToString()
			{
				return $"hour {Hour:F3}, sun {Up(Sun):F2} up from {Compass(Sun):F2}, moon {Up(Moon):F2} up from {Compass(Moon):F2}, light {Up(Light):F2} up from {Compass(Light):F2}, " + $"shadows cast from {Up(Cast):F2} up from {Compass(Cast):F2}, the land lit from {Up(Land):F2} up from {Compass(Land):F2}";
			}
		}

		private sealed class StillFrame
		{
			public int Index;

			public int Field;

			public int FieldPrev;

			public int Shown;

			public int EasedFrom;

			public int Waits;

			public int Patches;

			public int Day;

			public int Look;

			public float Changed;

			public float Ms;

			public float Blend;

			public float CardBlend;

			public Vec3 Cast;

			public Vector4 MapK;

			public Vector4 MapC;

			public bool Card;

			public string What = "";

			private static readonly FieldInfo CardField = typeof(GpuRenderer).GetField("_sunField", BindingFlags.Instance | BindingFlags.NonPublic);

			public static StillFrame Now(SunPathTest t)
			{
				WorldBoxMod mod = t._mod;
				TerrainCache sceneTerrain = mod.SceneTerrain;
				GpuRenderer gpuRendererForTest = mod.GpuRendererForTest;
				StillFrame stillFrame = new StillFrame
				{
					Ms = Time.unscaledDeltaTime * 1000f,
					Cast = mod.Day.Air.CastLight,
					Field = ((sceneTerrain.SunField != null) ? sceneTerrain.SunField.Version : 0),
					FieldPrev = ((sceneTerrain.SunFieldPrev != null) ? sceneTerrain.SunFieldPrev.Version : 0),
					Blend = sceneTerrain.SunFieldBlend,
					Patches = mod.SamplerForTest.SunPatches,
					Card = (gpuRendererForTest != null && gpuRendererForTest.Output != null && mod.ShownFrame == gpuRendererForTest.Shown),
					MapK = Shader.GetGlobalVector("_FP_ShadowK"),
					MapC = Shader.GetGlobalVector("_FP_ShadowC"),
					Day = Calendar.Day,
					Look = SeasonLook.Stamp
				};
				GpuSunField gpuSunField = ((gpuRendererForTest != null && CardField != null) ? (CardField.GetValue(gpuRendererForTest) as GpuSunField) : null);
				if (gpuSunField != null)
				{
					stillFrame.Shown = gpuSunField.ShownVersion;
					stillFrame.EasedFrom = gpuSunField.EasedFromVersion;
					stillFrame.CardBlend = gpuSunField.ShownBlend;
					stillFrame.Waits = gpuSunField.Waits;
				}
				return stillFrame;
			}

			public string Since(StillFrame b)
			{
				List<string> list = new List<string>();
				if (Cast.X != b.Cast.X || Cast.Y != b.Cast.Y || Cast.Z != b.Cast.Z)
				{
					list.Add("the shadows' light stepped on");
				}
				if (Field != b.Field)
				{
					list.Add((FieldPrev == 0) ? $"field {Field} put in at once" : ((FieldPrev == b.Field) ? $"field {Field} put in, eased in from {FieldPrev}" : $"field {Field} put in its place (was {b.Field})"));
				}
				if (Patches != b.Patches)
				{
					list.Add("(for changed land or what stands on it)");
				}
				if (Shown != b.Shown || EasedFrom != b.EasedFrom)
				{
					list.Add((Shown == 0) ? "the card draws no field" : ((EasedFrom != 0) ? $"the card draws field {Shown} eased in from {EasedFrom} ({CardBlend:F2})" : $"the card draws field {Shown} whole (the cache's eased {Blend:F2})"));
				}
				if (Waits != b.Waits)
				{
					list.Add("the card waited for a field's half floats");
				}
				if (MapK != b.MapK)
				{
					list.Add($"the shadow map fitted afresh ({1f / Mathf.Max(1E-06f, b.MapK.x):F4} to {1f / Mathf.Max(1E-06f, MapK.x):F4} tiles a texel up it)");
				}
				if ((Vector3)MapC != (Vector3)b.MapC)
				{
					list.Add($"the shadow map moved ({Vector3.Distance(MapC, b.MapC):F3} tiles)");
				}
				if (!Card)
				{
					list.Add("the CPU's picture shown");
				}
				if (Ms > 50f)
				{
					list.Add($"a slow frame ({Ms:F0} ms)");
				}
				if (Day != b.Day)
				{
					list.Add($"the date {b.Day} to {Day}");
				}
				if (Look != b.Look)
				{
					list.Add("the seasons' look moved");
				}
				return string.Join("; ", list);
			}
		}

		private sealed class Publishes
		{
			public int Snaps;

			public int Eases;

			public int Patches;

			private int _version = -1;

			private Vec3 _light;

			public void Watch(TerrainCache c)
			{
				SunShadowField sunField = c.SunField;
				int num = sunField?.Version ?? 0;
				if (_version >= 0 && num != _version && sunField != null)
				{
					Vec3 light = sunField.Light;
					if (c.SunFieldPrev == null || c.SunFieldPrev.Version != _version)
					{
						Snaps++;
					}
					else if (light.X == _light.X && light.Y == _light.Y && light.Z == _light.Z)
					{
						Patches++;
					}
					else
					{
						Eases++;
					}
				}
				_version = num;
				if (sunField != null)
				{
					_light = sunField.Light;
				}
			}

			public override string ToString()
			{
				return $"{Snaps} put in at once, {Eases} eased in, {Patches} eased in for changed land";
			}
		}

		private WorldBoxMod _mod;

		private int _pass;

		private int _fail;

		private bool _cpu;

		private int _heldDay = -1;

		private int _savedDay = -1;

		private readonly List<View> _views = new List<View>();

		private readonly HashSet<Building> _street = new HashSet<Building>();

		private Vector2 _ridge;

		private static readonly (string Tag, float T, float Decl, float Moon, float Lit)[] Hours = new(string, float, float, float, float)[12]
		{
			("t027", 0.27f, 0f, 0.5f, -1f),
			("t036", 0.36f, 0f, 0.5f, -1f),
			("t050", 0.5f, 0f, 0.5f, -1f),
			("t072", 0.72f, 0f, 0.5f, -1f),
			("t0745", 0.745f, 0f, 0.5f, -1f),
			("night_moon005", 0.02f, 0f, 0.05f, -1f),
			("night_moon025", 0.8333f, 0f, 0.25f, -1f),
			("night_moon050", 0.02f, 0f, 0.5f, -1f),
			("evening_moon005", 0.8333f, 0f, 0.25f, SunPath.MoonLit(0.05f)),
			("evening_moon050", 0.8333f, 0f, 0.25f, 1f),
			("summer_noon", 0.5f, 12f, 0.5f, -1f),
			("winter_noon", 0.5f, -12f, 0.5f, -1f)
		};

		private const float HalfMoonUp = 30f;

		private static readonly (string Tag, int Style, bool Gpu) Natural = (Tag: "natural", Style: 2, Gpu: true);

		private static readonly (string Tag, int Style, bool Gpu) Classic = (Tag: "classic", Style: 1, Gpu: true);

		private const float NormalDayMinutes = 20f;

		private const float FieldWait = 6f;

		private const double BakeMsMax = 200.0;

		private const float JumpLevels = 2f;

		private const float JumpTimes = 5f;

		private const float FpsShare = 0.85f;

		private const float NewMoonMost = 0.75f;

		private const float NewMoonLeast = 0.35f;

		private const float CardNewMoonLeast = 0.18f;

		private const float NotBlack = 1.5f;

		private const float NoonLeast = 0.8f;

		private const float NoonMost = 1.15f;

		private const int FlareGap = 20;

		private const float HandoverStepMost = 1f / 30f;

		private const float SlowMs = 50f;

		private const int StillFramesLeast = 100;

		private int _savedShadows = -1;

		private static readonly (string Tag, float Cycle)[] Phases = new(string, float)[3]
		{
			("crescent", 0.15f),
			("half", 0.25f),
			("full", 0.5f)
		};

		private const float MoonNightSun = -12f;

		private const float MoonDaysHour = 0.875f;

		private const int ForestBox = 16;

		private const float ForestOff = 40f;

		private static readonly float[] ForestUps = new float[4] { 16f, 24f, 32f, 48f };

		private const float NearestMap = 24f;

		private const float ForestCrown = 4f;

		private float _steadyAfter;

		private bool _steady;

		private const float FlareFireCost = 20f;

		private const float FlareWaterCost = 16f;

		private const int FpsRounds = 5;

		private const float FpsSeconds = 2.5f;

		private static readonly FieldInfo CalendarLast = typeof(Calendar).GetField("_last", BindingFlags.Static | BindingFlags.NonPublic);

		private float _readyAfter;

		private int[] _standing = new int[0];

		private const float ArmsTop = 0.3f;

		private const float ArmsGapLeft = 0.4f;

		private const float ArmsGapRight = 0.6f;

		private const int WarmLevels = 16;

		private const int MoonlessLevels = 2;

		private const int FlareLevels = 16;

		private int _grabW;

		private int _grabH;

		private IEnumerator Start()
		{
			string text = File.ReadAllText(Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest"));
			bool quit = text.Contains("quit");
			_cpu = text.Contains("cpu");
			while (!Config.game_loaded || World.world == null || World.world.units == null)
			{
				yield return null;
			}
			for (float t = 0f; t < 60f; t += 1f)
			{
				bool flag = false;
				try
				{
					flag = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag && t >= 4f)
				{
					break;
				}
				yield return new WaitForSecondsRealtime(1f);
			}
			Config.autosaves = false;
			_mod = WorldBoxMod.Instance;
			Actor king = null;
			foreach (WorldTile item in KingTest.GoodSites())
			{
				king = KingTest.FoundKingdom(item, "human", 10, 2);
				if (king != null)
				{
					Log.Info($"SUNPATH village at {item.x},{item.y}");
					break;
				}
			}
			if (king == null)
			{
				Log.Error("SUNPATH could not found a village");
				Finish(quit);
				yield break;
			}
			yield return new WaitForSecondsRealtime(3f);
			ControllableUnit.setControllableCreature(king);
			yield return new WaitForSecondsRealtime(3f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("SUNPATH first person did not start");
				Finish(quit);
				yield break;
			}
			_mod.HintUntil = 0f;
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			Settings s = _mod.Settings;
			(int SunStyle, bool LensFlare, int TimeMode, bool GpuRender, float DayMinutes, int Resolution, bool GodView) saved = (SunStyle: s.SunStyle, LensFlare: s.LensFlare, TimeMode: s.TimeMode, GpuRender: s.GpuRender, DayMinutes: s.DayMinutes, Resolution: s.Resolution, GodView: s.GodView);
			int savedRate = Application.targetFrameRate;
			int savedSync = QualitySettings.vSyncCount;
			float savedHour = _mod.Day.TimeOfDay;
			float savedMotes = s.BiomeParticles;
			bool savedGusts = s.WindGusts;
			Application.targetFrameRate = 1000;
			QualitySettings.vSyncCount = 0;
			s.TimeMode = 0;
			s.GpuRender = true;
			s.Resolution = 1;
			Config.paused = true;
			_mod.HeldTime = Time.unscaledTime;
			s.BiomeParticles = 0f;
			_mod.NoFirefliesForTest = true;
			s.WindGusts = false;
			yield return HoldDate();
			int built;
			Vector2 house = BuildStreet(king.current_position, out built);
			yield return new WaitForSecondsRealtime(1f);
			PlanViews(house);
			Log.Info(string.Format("SUNPATH {0} houses at {1}; views {2}; ", built, house, string.Join(", ", _views.ConvertAll((View v) => $"{v.Tag} from {v.Eye} toward {v.Dir}, {v.Up} up, pitch {v.Pitch:F0}"))) + $"{SystemInfo.graphicsDeviceName}, {_mod.SceneTerrain.Width}x{_mod.SceneTerrain.Height} tiles, shadows {s.Shadows}, night darkness {s.NightDark}, look {s.GpuLook}, palette {s.Palette}");
			yield return Run("path", PathChecks());
			yield return Run("stills", Stills());
			yield return Run("handover", Handover());
			yield return Run("stability", Stability());
			yield return Run("contact", Contact());
			if (_savedShadows >= 0)
			{
				s.Shadows = _savedShadows;
				_savedShadows = -1;
			}
			yield return Run("nofield", NoField());
			_mod.NoLandFieldForTest = false;
			yield return Run("fixed", FixedHours());
			int heldDay = _heldDay;
			yield return Run("moon", Moon());
			_heldDay = heldDay;
			yield return SeasonBack();
			float savedDistance = s.ShadowDistance;
			yield return Run("far", Far());
			s.ShadowDistance = savedDistance;
			if (_savedShadows >= 0)
			{
				s.Shadows = _savedShadows;
				_savedShadows = -1;
			}
			yield return Run("rooms", Rooms());
			if (_mod.Interior.Inside)
			{
				_mod.LeaveRoomForTest();
			}
			yield return Run("flare", Flare());
			yield return Run("fps", Fps());
			yield return Run("god", God());
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			_mod.HeldTime = -1f;
			_mod.Day.TestDeclination = (_mod.Day.TestMoonCycle = (_mod.Day.TestMoonLit = null));
			Settings settings = s;
			Settings settings2 = s;
			Settings settings3 = s;
			Settings settings4 = s;
			Settings settings5 = s;
			Settings settings6 = s;
			(int, bool, int, bool, float, int, bool) tuple = saved;
			s.SunStyle = tuple.Item1;
			settings.LensFlare = tuple.Item2;
			settings2.TimeMode = tuple.Item3;
			settings3.GpuRender = tuple.Item4;
			settings4.DayMinutes = tuple.Item5;
			settings5.Resolution = tuple.Item6;
			settings6.GodView = tuple.Item7;
			s.BiomeParticles = savedMotes;
			_mod.NoFirefliesForTest = false;
			s.WindGusts = savedGusts;
			_mod.Day.TimeOfDay = savedHour;
			try
			{
				CalendarLast?.SetValue(null, savedHour);
			}
			catch
			{
			}
			_heldDay = -1;
			if (_savedDay >= 0)
			{
				Calendar.SetDayForTest(_savedDay);
			}
			Config.paused = false;
			_mod.ForceRelight();
			Application.targetFrameRate = savedRate;
			QualitySettings.vSyncCount = savedSync;
			if (_mod.GpuRendererForTest != null)
			{
				Log.Info("SUNPATH the field on the card: " + _mod.GpuRendererForTest.SunFieldTimes);
			}
			Log.Info($"SUNPATH {_pass} passed, {_fail} failed");
			Finish(quit);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("SUNPATH --- " + name);
			while (true)
			{
				object current;
				try
				{
					if (!part.MoveNext())
					{
						break;
					}
					current = part.Current;
					goto IL_0071;
				}
				catch (Exception ex)
				{
					Check(name + " ran", ok: false, ex.ToString());
				}
				break;
				IL_0071:
				yield return current;
			}
			Config.paused = true;
			_mod.Settings.DayMinutes = 20f;
		}

		private IEnumerator HoldDate()
		{
			_savedDay = Calendar.Day;
			_heldDay = -1;
			Season[] array = new Season[3]
			{
				Season.Summer,
				Season.Spring,
				Season.Autumn
			};
			int i;
			for (i = 0; i < array.Length; i++)
			{
				Season season = array[i];
				for (int j = _savedDay; j < _savedDay + 16; j++)
				{
					if (_heldDay >= 0)
					{
						break;
					}
					if (Calendar.SeasonOf(j, out var dayIn, out var length) == season && dayIn == 0 && length >= 2)
					{
						_heldDay = j;
					}
				}
				if (_heldDay >= 0)
				{
					break;
				}
			}
			if (_heldDay < 0)
			{
				_heldDay = _savedDay;
			}
			KeepDate();
			int length2;
			Season season2 = Calendar.SeasonOf(_heldDay, out i, out length2);
			float start = Time.unscaledTime;
			while (Time.unscaledTime - start < 8f && Calendar.LookForTest[(int)season2] < 0.99f)
			{
				yield return null;
			}
			_mod.ForceRelight();
			yield return new WaitForSecondsRealtime(2f);
			float[] lookForTest = Calendar.LookForTest;
			Log.Info($"SUNPATH the date held at day {_heldDay} ({season2}, its first day; the world's was {_savedDay}); the seasons' look " + $"{lookForTest[0]:F2}/{lookForTest[1]:F2}/{lookForTest[2]:F2}/{lookForTest[3]:F2} after {Time.unscaledTime - start:F1} s");
		}

		private void Update()
		{
			if (_heldDay >= 0)
			{
				KeepDate();
			}
		}

		private void KeepDate()
		{
			if (_heldDay < 0)
			{
				return;
			}
			try
			{
				if (Calendar.Day != _heldDay)
				{
					Calendar.SetDayForTest(_heldDay);
				}
			}
			catch (Exception ex)
			{
				Log.Error("SUNPATH the date: " + ex.Message);
				_heldDay = -1;
			}
		}

		private IEnumerator PathChecks()
		{
			Settings settings = _mod.Settings;
			settings.SunStyle = Natural.Style;
			settings.GpuRender = true;
			_mod.ForceRelight();
			for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
			{
				if (_mod.Day.NaturalCycle)
				{
					break;
				}
				yield return null;
			}
			Check("the day runs the natural sun with F2 Sun on Natural", _mod.Day.NaturalCycle);
			float[] array = new float[3] { 0f, 12f, -12f };
			for (int i = 0; i < array.Length; i++)
			{
				float t = array[i];
				_mod.Day.TestDeclination = t * (MathF.PI / 180f);
				for (int j = 0; j < 3; j++)
				{
					yield return null;
				}
				DayCycle day = _mod.Day;
				float num = day.SkyAt(0.25f).SunElevation * 57.29578f;
				float num2 = day.SkyAt(0.75f).SunElevation * 57.29578f;
				Atmosphere atmosphere = day.SkyAt(0.5f);
				float num3 = atmosphere.SunElevation * 57.29578f;
				Check($"declination {t:0}: the sun comes up at 0.25 and goes down at 0.75", Mathf.Abs(num) < 0.3f && Mathf.Abs(num2) < 0.3f, $"{num:F2} and {num2:F2} degrees");
				Check($"declination {t:0}: noon {64f + t:0} degrees high in the north", Mathf.Abs(num3 - (64f + t)) < 0.3f && atmosphere.SunDisc.Y > 0f, $"{num3:F2} degrees, {Compass(atmosphere.SunDisc):F0} on the compass");
				Log.Info($"SUNPATH declination {t:0}: sunrise from {Compass(day.SkyAt(0.25f).SunDisc):F0}, sunset to {Compass(day.SkyAt(0.75f).SunDisc):F0} on the compass; " + $"0.27 {day.SkyAt(0.27f).SunElevation * 57.29578f:F1}, 0.72 {day.SkyAt(0.72f).SunElevation * 57.29578f:F1}, 0.745 {day.SkyAt(0.745f).SunElevation * 57.29578f:F1} degrees up");
			}
			array = new float[3] { 0.05f, 0.25f, 0.5f };
			for (int i = 0; i < array.Length; i++)
			{
				float t = array[i];
				_mod.Day.TestMoonCycle = t;
				for (int j = 0; j < 3; j++)
				{
					yield return null;
				}
				float moonLit = _mod.Day.SkyAt(0.02f).MoonLit;
				float num4 = (1f - Mathf.Cos(t * 2f * MathF.PI)) * 0.5f;
				Check($"moon at {t:0.00} of its round: {num4 * 100f:F0}% lit", Mathf.Abs(moonLit - num4) < 0.01f, $"{moonLit:F3}");
			}
			_mod.Day.TestDeclination = (_mod.Day.TestMoonCycle = null);
		}

		private IEnumerator Stills()
		{
			List<(string Tag, int Style, bool Gpu)> modes = new List<(string, int, bool)> { Natural, Classic };
			if (_cpu)
			{
				modes.Add(("cpu_natural", 2, false));
				modes.Add(("cpu_classic", 1, false));
			}
			Dictionary<string, float> means = new Dictionary<string, float>();
			Dictionary<string, float> lows = new Dictionary<string, float>();
			Dictionary<string, float> lands = new Dictionary<string, float>();
			Dictionary<string, float> moonUp = new Dictionary<string, float>();
			Dictionary<string, Color32[]> newMoonPx = new Dictionary<string, Color32[]>();
			Dictionary<string, float> litLand = new Dictionary<string, float>();
			Dictionary<string, (float New, float Full)> moonLands = new Dictionary<string, (float, float)>();
			HashSet<string> used = new HashSet<string>();
			int fields = 0;
			int late = 0;
			int drawn = -1;
			float slowest = 0f;
			foreach (View v in _views)
			{
				Look(v);
				yield return new WaitForSecondsRealtime(2f);
				(string Tag, float T, float Decl, float Moon, float Lit)[] hours = Hours;
				for (int i = 0; i < hours.Length; i++)
				{
					(string Tag, float T, float Decl, float Moon, float Lit) h = hours[i];
					for (int j = 0; j < modes.Count; j++)
					{
						int at = ((drawn < 0) ? j : ((drawn + j) % modes.Count));
						(string Tag, int Style, bool Gpu) m = modes[at];
						if (at != drawn)
						{
							yield return Use(m, used.Add(m.Tag));
							drawn = at;
						}
						SetSky(h.T, h.Decl, h.Moon, h.Lit);
						yield return Settle(m.Style == 2);
						if (m.Style == 2)
						{
							fields++;
							if (!FieldReady())
							{
								late++;
							}
							else
							{
								slowest = Mathf.Max(slowest, _readyAfter);
							}
						}
						KeepDate();
						_mod.SaveFrame("sunpath_" + v.Tag + "_" + m.Tag + "_" + h.Tag + ".png");
						Color32[] array = Grab();
						float num = Mean(array);
						float num2 = MeanLow(array);
						means[v.Tag + " " + m.Tag + " " + h.Tag] = num;
						lows[v.Tag + " " + m.Tag + " " + h.Tag] = num2;
						lands[v.Tag + " " + m.Tag + " " + h.Tag] = MeanLand(array);
						string key = v.Tag + " " + m.Tag;
						Color32[] value;
						if (h.Tag == "night_moon005")
						{
							newMoonPx[key] = array;
						}
						else if (h.Tag == "night_moon050" && newMoonPx.TryGetValue(key, out value))
						{
							litLand[key] = LitLandShare(value, array);
							moonLands[key] = (MeanLand(value, array), MeanLand(array, value));
							newMoonPx.Remove(key);
						}
						Atmosphere air = _mod.Day.Air;
						moonUp[v.Tag + " " + m.Tag + " " + h.Tag] = Up(air.MoonDisc);
						Log.Info($"SUNPATH {v.Tag} {m.Tag} {h.Tag}: brightness {num:F1} (lower third {num2:F1}); sun {Up(air.SunDisc):F1} degrees up from {Compass(air.SunDisc):F0}, " + $"shadows cast from {Up(air.CastLight):F1} up, direct light {air.LightR:F2}/{air.LightG:F2}/{air.LightB:F2}, fill {air.AmbR:F2}/{air.AmbG:F2}/{air.AmbB:F2} " + $"(the card's {air.AmbR * air.CardFillR:F2}/{air.AmbG * air.CardFillG:F2}/{air.AmbB * air.CardFillB:F2}), " + $"moon {air.MoonLit:F2} lit {Up(air.MoonDisc):F0} degrees up; day {Calendar.Day}");
					}
				}
			}
			Check("the land's field for each new light came in", late == 0, $"{fields - late} of {fields} within {6f:F0} s, the slowest after {slowest:F2} s");
			foreach (View view in _views)
			{
				foreach (var item in modes)
				{
					string text = view.Tag + " " + item.Tag + " ";
					if (!means.TryGetValue(text + "night_moon005", out var value2) || !means.TryGetValue(text + "night_moon050", out var value3))
					{
						continue;
					}
					means.TryGetValue(text + "summer_noon", out var value4);
					means.TryGetValue(text + "winter_noon", out var value5);
					lows.TryGetValue(text + "night_moon005", out var value6);
					lows.TryGetValue(text + "night_moon050", out var value7);
					if (item.Style == 2)
					{
						float num3 = ((value7 > 0f) ? (value6 / value7) : 0f);
						float num4 = (item.Gpu ? 0.18f : 0.35f);
						float value8;
						float num5 = (litLand.TryGetValue(view.Tag + " " + item.Tag, out value8) ? value8 : num3);
						Check(view.Tag + " " + item.Tag + ": a new-moon night clearly darker than a full-moon night where the full moon lights the land", num5 < 0.75f, $"the lower third's brightest tenth under the full moon {num5 * 100f:F0}% as bright under the new one, at most {75f:F0}%; " + $"the land's brightness {value6:F1} against {value7:F1}: {num3 * 100f:F0}% (the whole picture {value2:F1} against {value3:F1})");
						Check(view.Tag + " " + item.Tag + ": a new-moon night not pitch black", value6 >= value7 * num4 && value6 > 1.5f, $"the land's brightness {value6:F1}: {num3 * 100f:F0}% of the full moon's, at least {num4 * 100f:F0}% and over {1.5f:F1} of 255");
						if (lows.TryGetValue(text + "night_moon025", out var value9) && moonUp.TryGetValue(text + "night_moon025", out var value10) && lows.TryGetValue(text + "evening_moon005", out var value11) && lows.TryGetValue(text + "evening_moon050", out var value12))
						{
							Check(view.Tag + " " + item.Tag + ": the half moon up when its night is drawn", value10 >= 30f, $"{value10:F0} degrees up, at least {30f:F0}");
							Check(view.Tag + " " + item.Tag + ": a half-moon night between a new and a full moon's light from its place", value9 > value11 && value9 < value12 * 1.05f, $"the land's brightness {value9:F1}, new {value11:F1}, full {value12:F1} (at midnight: new {value6:F1}, full {value7:F1})");
						}
						Log.Info($"SUNPATH {view.Tag} {item.Tag}: summer noon brightness {value4:F1}, winter noon {value5:F1}");
					}
					else
					{
						lands.TryGetValue(text + "night_moon005", out var value13);
						lands.TryGetValue(text + "night_moon050", out var value14);
						if (moonLands.TryGetValue(view.Tag + " " + item.Tag, out (float, float) value15))
						{
							(value13, value14) = value15;
						}
						lands.TryGetValue(text + "summer_noon", out var value16);
						lands.TryGetValue(text + "winter_noon", out var value17);
						Check(view.Tag + " " + item.Tag + ": the moon always full", Mathf.Abs(value13 - value14) < Mathf.Max(0.5f, value14 * 0.02f), $"the land's brightness {value13:F1} against {value14:F1}, your arms left out (with them {value6:F1} against {value7:F1}; the whole picture {value2:F1} against {value3:F1})");
						Check(view.Tag + " " + item.Tag + ": no seasons", Mathf.Abs(value16 - value17) < Mathf.Max(0.5f, value17 * 0.02f), $"the land's brightness {value16:F1} against {value17:F1}, your arms left out (the whole picture {value4:F1} against {value5:F1})");
					}
				}
			}
			foreach (View view2 in _views)
			{
				List<string> list = new List<string>();
				(string, float, float, float, float)[] hours2 = Hours;
				for (int k = 0; k < hours2.Length; k++)
				{
					(string, float, float, float, float) tuple2 = hours2[k];
					if (means.TryGetValue(view2.Tag + " natural " + tuple2.Item1, out var value18) && means.TryGetValue(view2.Tag + " classic " + tuple2.Item1, out var value19))
					{
						list.Add($"{tuple2.Item1} {value18:F0}/{value19:F0}");
					}
				}
				Log.Info("SUNPATH " + view2.Tag + " brightness natural/classic: " + string.Join(", ", list));
				if (means.TryGetValue(view2.Tag + " natural t050", out var value20) && means.TryGetValue(view2.Tag + " classic t050", out var value21) && value21 > 0f)
				{
					Check(view2.Tag + ": the natural sun's equinox noon as bright as the classic's", value20 >= value21 * 0.8f && value20 <= value21 * 1.15f, $"brightness {value20:F1} against {value21:F1}: {value20 / value21 * 100f:F0}%");
				}
			}
		}

		private IEnumerator Handover()
		{
			Settings s = _mod.Settings;
			List<(string Tag, int Style, bool Gpu)> modes = new List<(string, int, bool)> { Natural, Classic };
			if (_cpu)
			{
				modes.Add(("cpu_natural", 2, false));
			}
			foreach (View v in _views)
			{
				if (v.Tag == "water")
				{
					continue;
				}
				foreach (var m in modes)
				{
					yield return Use(m, check: false);
					Look(v);
					yield return new WaitForSecondsRealtime(1.5f);
					s.DayMinutes = 2f;
					SetSky(0.725f, 0f, 0.5f);
					yield return Settle(m.Style == 2);
					List<float> hours = new List<float>();
					List<float> bright = new List<float>();
					Publishes seen = new Publishes();
					seen.Watch(_mod.SceneTerrain);
					float start = Time.unscaledTime;
					bool ran = false;
					while (Time.unscaledTime - start < 60f)
					{
						_mod.Day.TimeOfDay += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f) * _mod.Day.Rate(s);
						yield return null;
						seen.Watch(_mod.SceneTerrain);
						float hour = _mod.Day.Hour;
						if (hour != 0.725f)
						{
							ran = true;
						}
						if (hour < 0.7f || hour >= 0.76f)
						{
							break;
						}
						if (!(hour < 0.73f))
						{
							hours.Add(hour);
							bright.Add(Mean(Grab()));
						}
					}
					Config.paused = true;
					s.DayMinutes = 20f;
					if (!ran)
					{
						Check(v.Tag + " " + m.Tag + ": the clock ran for the sunset", ok: false);
					}
					else
					{
						JudgeHandover(v.Tag + " " + m.Tag, hours, bright, seen, m.Style == 2);
					}
				}
			}
		}

		private void JudgeHandover(string what, List<float> hours, List<float> bright, Publishes seen, bool natural)
		{
			if (natural)
			{
				Check(what + ": the moon's field eased in at the sunset, none put in at once", seen.Snaps == 0, $"fields {seen}");
			}
			if (bright.Count < 10)
			{
				Check(what + ": the sunset watched frame by frame", ok: false, $"only {bright.Count} frames between 0.73 and 0.76");
				return;
			}
			List<float> list = new List<float>();
			for (int i = 1; i < bright.Count; i++)
			{
				float num = hours[i] - hours[i - 1];
				if (num > 0f)
				{
					list.Add(Mathf.Abs(bright[i] - bright[i - 1]) / num);
				}
			}
			list.Sort();
			float num2 = ((list.Count > 0) ? list[list.Count / 2] : 0f);
			float num3 = 0f;
			float num4 = 0f;
			float num5 = 0f;
			float num6 = 2f;
			for (int j = 1; j < bright.Count; j++)
			{
				float num7 = Mathf.Abs(bright[j] - bright[j - 1]);
				float num8 = Mathf.Max(2f, 5f * num2 * Mathf.Max(0f, hours[j] - hours[j - 1]));
				if (num7 / num8 > num3)
				{
					num3 = num7 / num8;
					num4 = num7;
					num5 = hours[j];
					num6 = num8;
				}
			}
			string text = $"{bright.Count} frames, brightness {bright[0]:F1} to {bright[bright.Count - 1]:F1}, steadily {num2 * 0.001f:F2} levels each 0.001 of a day, " + $"the biggest step {num4:F2} levels at {num5:F4} (allowed {num6:F2}); fields {seen}";
			if (natural)
			{
				Check(what + ": no jump in brightness through the sunset", num3 <= 1f, text);
				return;
			}
			Log.Info("SUNPATH " + what + " (for comparison, not checked): " + ((num3 <= 1f) ? "no jump" : "a jump") + "; " + text);
		}

		private IEnumerator Stability()
		{
			View v = _views[0];
			float classicWorst = -1f;
			int classicCalm = 0;
			List<(float P99, int Calm)> naturalWorst = new List<(float, int)>();
			(string Tag, int Style, bool Gpu)[] array = new(string, int, bool)[3] { Natural, Classic, Natural };
			for (int i = 0; i < array.Length; i++)
			{
				(string Tag, int Style, bool Gpu) m = array[i];
				yield return Use(m, check: false);
				Look(v);
				yield return new WaitForSecondsRealtime(1.5f);
				SetSky(0.66f, 0f, 0.5f);
				yield return Settle(m.Style == 2);
				float steadyAfter = 0f;
				for (int tries = 0; tries < 3; tries++)
				{
					yield return Steady();
					steadyAfter += _steadyAfter;
					if (_steady)
					{
						break;
					}
				}
				bool held = _steady;
				Publishes seen = new Publishes();
				seen.Watch(_mod.SceneTerrain);
				List<StillFrame> frames = new List<StillFrame>();
				StillFrame was = StillFrame.Now(this);
				Color32[] last = Grab();
				float pace = 0.00083333335f;
				float start = Time.unscaledTime;
				float hourWas = _mod.Day.TimeOfDay;
				int calm = 0;
				while (Time.unscaledTime - start < 5f || (calm < 100 && Time.unscaledTime - start < 15f))
				{
					_mod.Day.TimeOfDay += pace / 60f;
					yield return null;
					seen.Watch(_mod.SceneTerrain);
					Color32[] array2 = Grab();
					StillFrame stillFrame = StillFrame.Now(this);
					if (last != null && array2.Length == last.Length && array2.Length != 0)
					{
						stillFrame.Index = frames.Count + 1;
						stillFrame.Changed = (float)Diff(last, array2) * 100f / (float)array2.Length;
						stillFrame.What = stillFrame.Since(was);
						frames.Add(stillFrame);
						if (stillFrame.Ms <= 50f)
						{
							calm++;
						}
					}
					last = array2;
					was = stillFrame;
				}
				float num = 0f;
				float num2 = 0f;
				int num3 = 0;
				foreach (StillFrame item in frames)
				{
					num += item.Changed;
					num2 = Mathf.Max(num2, item.Changed);
					if (item.Changed > 0.05f)
					{
						num3++;
					}
				}
				num /= (float)Mathf.Max(1, frames.Count);
				List<float> list = frames.ConvertAll((StillFrame f) => f.Changed);
				list.Sort();
				float num4 = ((list.Count > 0) ? list[list.Count / 2] : 0f);
				List<float> list2 = frames.FindAll((StillFrame f) => f.Ms <= 50f).ConvertAll((StillFrame f) => f.Changed);
				list2.Sort();
				float num5 = ((list2.Count > 0) ? list2[Mathf.Min(list2.Count - 1, (int)((float)list2.Count * 0.99f))] : num2);
				Log.Info($"SUNPATH stability {m.Tag}: {frames.Count} frames over {(_mod.Day.TimeOfDay - hourWas) * 4000f:F1} of the natural sun's shadow steps (the picture " + string.Format("{0} after {1:F1} s); {2:F3}% of the pixels change from frame to frame on average (median {3:F3}%), {4:F2}% at most, ", held ? "steady" : "NOT steady", steadyAfter, num, num4, num2) + $"{num5:F2}% at the 99th percentile of the {list2.Count} not slow; {num3} frames changed more than 0.05%; fields {seen}");
				List<StillFrame> list3 = new List<StillFrame>(frames);
				list3.Sort((StillFrame a, StillFrame b) => b.Changed.CompareTo(a.Changed));
				for (int num6 = 0; num6 < list3.Count; num6++)
				{
					if (num6 < 3 || list3[num6].Changed > Mathf.Max(3f, num4 * 6f))
					{
						Log.Info(string.Format("SUNPATH stability {0}: frame {1} changed {2:F2}% ({3:F0} ms): {4}", m.Tag, list3[num6].Index, list3[num6].Changed, list3[num6].Ms, (list3[num6].What.Length > 0) ? list3[num6].What : "nothing else seen"));
					}
				}
				if (m.Style == 2)
				{
					Check("natural: at a normal day's pace the land's shadows ease from field to field, never jump", seen.Snaps == 0, seen.ToString());
					naturalWorst.Add((num5, list2.Count));
				}
				else
				{
					classicWorst = num5;
					classicCalm = list2.Count;
				}
			}
			if (!(classicWorst >= 0f))
			{
				yield break;
			}
			foreach (var item2 in naturalWorst)
			{
				string text = "natural: the sun creeping on changes no more of the picture than the classic sun (99th percentile, slow frames left out)";
				string text2 = $"{item2.P99:F2}% against {classicWorst:F2}%, of {item2.Calm} and {classicCalm} frames not slow";
				if (item2.Calm < 100 || classicCalm < 100)
				{
					Log.Info($"SUNPATH (not judged: under {100} frames not slow, the machine busy) {text} ({text2})");
				}
				else
				{
					Check(text, item2.P99 <= Mathf.Max(classicWorst * 1.2f, classicWorst + 1f), text2);
				}
			}
		}

		private IEnumerator Contact()
		{
			Settings s = _mod.Settings;
			GpuRenderer g = _mod.GpuRendererForTest;
			if (g == null)
			{
				Log.Info("SUNPATH contact: the graphics card draws nothing");
				yield break;
			}
			_savedShadows = s.Shadows;
			s.Shadows = 3;
			yield return Use(Natural, check: false);
			Look(_views[0]);
			yield return new WaitForSecondsRealtime(1.5f);
			(string, float)[] array = new(string, float)[2]
			{
				("t036", 0.36f),
				("t072", 0.72f)
			};
			for (int i = 0; i < array.Length; i++)
			{
				(string, float) h = array[i];
				SetSky(h.Item2, 0f, 0.5f);
				yield return Settle(natural: true);
				yield return Steady();
				g.ContactShade = true;
				yield return Frames(4);
				Color32[] on = Grab();
				_mod.SaveFrame("sunpath_contact_high_" + h.Item1 + "_on.png");
				g.ContactShade = false;
				yield return Frames(4);
				Color32[] off = Grab();
				_mod.SaveFrame("sunpath_contact_high_" + h.Item1 + "_off.png");
				g.ContactShade = true;
				yield return Frames(4);
				Color32[] b = Grab();
				int num = Diff(on, off);
				int num2 = Diff(on, b);
				Log.Info($"SUNPATH contact {h.Item1} (Shadows High): contact shadows and creases change {num} pixels ({(float)num * 100f / (float)Mathf.Max(1, on.Length):F2}% of the picture), " + $"{num2} between two pictures alike; the picture's brightness {Mean(on):F1} with them, {Mean(off):F1} without");
			}
			g.ContactShade = true;
			s.Shadows = _savedShadows;
			_savedShadows = -1;
		}

		private IEnumerator NoField()
		{
			View v = _views.Find((View x) => x.Tag == "valley");
			if (v.Tag == null)
			{
				Log.Info("SUNPATH nofield: no valley view on this map");
				yield break;
			}
			yield return Use(Natural, check: false);
			Look(v);
			yield return new WaitForSecondsRealtime(1.5f);
			SetSky(0.36f, 0f, 0.5f);
			yield return Settle(natural: true);
			_mod.SaveFrame("sunpath_valley_natural_t036_field.png");
			_mod.NoLandFieldForTest = true;
			_mod.ForceRelight();
			for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
			{
				yield return null;
			}
			_mod.SaveFrame("sunpath_valley_natural_t036_nofield.png");
			Log.Info("SUNPATH nofield: the valley at 0.36 saved with the land's field and without (the field " + ((_mod.SceneTerrain.SunField != null) ? "still there" : "dropped") + ")");
			_mod.NoLandFieldForTest = false;
			_mod.ForceRelight();
			yield return Settle(natural: true);
		}

		private IEnumerator FixedHours()
		{
			Settings s = _mod.Settings;
			string[] names = new string[4] { "", "day", "golden", "night" };
			Dictionary<int, Fixed> classic = new Dictionary<int, Fixed>();
			(string Tag, int Style, bool Gpu)[] array = new(string, int, bool)[2] { Classic, Natural };
			for (int i = 0; i < array.Length; i++)
			{
				(string Tag, int Style, bool Gpu) m = array[i];
				yield return Use(m, check: false);
				Look(_views[0]);
				yield return new WaitForSecondsRealtime(1f);
				for (int mode = 1; mode <= 3; mode++)
				{
					Config.paused = true;
					s.TimeMode = mode;
					_mod.ForceRelight();
					yield return Settle(m.Style == 2);
					Fixed obj = Fixed.Now(_mod);
					_mod.SaveFrame("sunpath_f7_" + names[mode] + "_" + m.Tag + ".png");
					Log.Info(string.Format("SUNPATH F7 {0} {1}: {2}; the day {3}", DayCycle.ModeNames[mode], m.Tag, obj, _mod.Day.NaturalCycle ? "runs the natural sun" : "keeps the classic clock"));
					if (m.Style != 2)
					{
						classic[mode] = obj;
						continue;
					}
					TerrainCache sceneTerrain = _mod.SceneTerrain;
					Check("F7 " + DayCycle.ModeNames[mode] + ": the land's soft shadows still on under the natural sun", sceneTerrain.SoftLandShadows && sceneTerrain.SunField != null);
					if (classic.TryGetValue(mode, out var value))
					{
						Check("F7 " + DayCycle.ModeNames[mode] + ": the natural sun stands where the classic does", obj.Apart(value) < 0.0001f && obj.Hour == value.Hour, $"directions at most {obj.Apart(value):G3} apart; natural {obj}; classic {value}");
					}
				}
			}
			s.TimeMode = 0;
			_mod.ForceRelight();
		}

		private IEnumerator Moon()
		{
			yield return Use(Natural, check: false);
			List<float> shares = new List<float>();
			List<string> seen = new List<string>();
			(string Tag, float Cycle)[] phases = Phases;
			for (int i = 0; i < phases.Length; i++)
			{
				(string Tag, float Cycle) ph = phases[i];
				float hour = MoonHour(ph.Cycle);
				SetSky(hour, 0f, ph.Cycle);
				for (int j = 0; j < 3; j++)
				{
					yield return null;
				}
				Atmosphere air = _mod.Day.Air;
				Vec3 moon = air.MoonDisc;
				View v = new View
				{
					Tag = "moon",
					Eye = _views[0].Eye,
					Dir = new Vector2(moon.X, moon.Y).normalized,
					Pitch = Up(moon),
					Up = 0f
				};
				int cleared = ClearCone(v.Eye, v.Dir, 10f, 40f, 3f);
				Look(v);
				yield return Settle(natural: true);
				for (int j = 0; j < 2; j++)
				{
					FrameProjection shown = _mod.Shown;
					if (shown.Focal <= 0f || !shown.Project(shown.View.X + moon.X * 500f, shown.View.Y + moon.Y * 500f, shown.View.Z + moon.Z * 500f, out var _, out var row, out var _))
					{
						break;
					}
					v.Pitch -= Mathf.Atan((row - (float)shown.Height * 0.5f) / shown.Focal) * 57.29578f;
					Look(v);
					for (int k = 0; k < 5; k++)
					{
						yield return null;
					}
				}
				yield return Steady();
				_mod.SaveFrame("sunpath_moon_" + ph.Tag + ".png");
				string how;
				float num = LitShare(Grab(), moon, air.MoonScale, out how);
				shares.Add(num);
				seen.Add(string.Format("{0} {1} ({2}; {3} trees and the like cleared that way)", ph.Tag, (num >= 0f) ? $"{num * 100f:F0}%" : "not seen", how, cleared));
				float num2 = Up(moon);
				float num3 = SunPath.MoonLit(ph.Cycle);
				Check($"moon {ph.Tag}: at least {30f:F0} degrees up where it is looked at", num2 >= 30f, $"{num2:F1} degrees up from {Compass(moon):F0} at {hour:F4}, the sun {Up(air.SunDisc):F1} degrees up");
				Check($"moon {ph.Tag}: {num3 * 100f:F0}% lit", Mathf.Abs(air.MoonLit - num3) < 0.01f, $"{air.MoonLit:F3}, its light {air.LightR:F2}/{air.LightG:F2}/{air.LightB:F2}");
				if (_cpu)
				{
					_mod.Settings.GpuRender = false;
					yield return new WaitForSecondsRealtime(1f);
					_mod.SaveFrame("sunpath_moon_" + ph.Tag + "_cpu.png");
					_mod.Settings.GpuRender = true;
					yield return new WaitForSecondsRealtime(0.5f);
				}
			}
			if (shares.Count == 3 && shares.TrueForAll((float x) => x >= 0f) && shares[2] >= 0.3f)
			{
				Check("moon: its lit face in the picture grows with the phase", shares[0] < shares[1] && shares[1] < shares[2], string.Format("{0} of the disc; as its phase {1:F0}%, {2:F0}%, 100%", string.Join(", ", seen), SunPath.MoonLit(Phases[0].Cycle) * 100f, SunPath.MoonLit(Phases[1].Cycle) * 100f));
			}
			else
			{
				Log.Info("SUNPATH moon: its lit face not judged, a disc not seen (a cloud or something in front of it?): " + string.Join(", ", seen));
			}
			int first = _heldDay;
			if (first < 0)
			{
				Log.Info("SUNPATH moon: the date isn't held, the days not run");
				yield break;
			}
			List<float> east = new List<float>();
			List<float> rises = new List<float>();
			List<string> rows = new List<string>();
			for (int i = 0; i < 3; i++)
			{
				_heldDay = first + i;
				KeepDate();
				SetSky(0.875f, 0f, null);
				for (int cleared = 0; cleared < 4; cleared++)
				{
					yield return null;
				}
				DayCycle day = _mod.Day;
				Vec3 moonDisc = day.Air.MoonDisc;
				float num4 = HourAngleOf(moonDisc) * 57.29578f;
				float num5 = Moonrise(day.MoonCycle, Calendar.SinceDawn, day.Declination);
				east.Add(num4);
				rises.Add(num5);
				rows.Add($"day {Calendar.Day}: its round at {day.MoonCycle:F3} ({day.Air.MoonLit * 100f:F0}% lit), {Up(moonDisc):F1} degrees up from {Compass(moonDisc):F0}, " + string.Format("hour angle {0:F1}, rising {1}", num4, (num5 >= 0f) ? $"at {(0.25f + num5) % 1f:F3} ({num5:F3} of the day after its dawn)" : "not this day"));
			}
			_heldDay = first;
			KeepDate();
			Log.Info(string.Format("SUNPATH the moon at {0} on three days running: {1}", 0.875f, string.Join("; ", rows)));
			bool ok = true;
			List<string> list = new List<string>();
			for (int num6 = 1; num6 < east.Count; num6++)
			{
				float num7 = Mathf.DeltaAngle(east[num6 - 1], east[num6]);
				list.Add($"{num7:F1}");
				if (num7 > -40f || num7 < -50f)
				{
					ok = false;
				}
			}
			Check("moon: at the same hour it stands 45 degrees further east each day", ok, "its hour angle moved " + string.Join(" and ", list) + " degrees");
			bool ok2 = true;
			int num8 = 0;
			List<string> list2 = new List<string>();
			for (int num9 = 1; num9 < rises.Count; num9++)
			{
				if (!(rises[num9 - 1] < 0f) && !(rises[num9] < 0f))
				{
					float num10 = rises[num9] - rises[num9 - 1];
					if (num10 < 0f)
					{
						num10 += 1f;
					}
					num8++;
					list2.Add($"{num10:F3}");
					if (num10 < 0.08f || num10 > 0.25f)
					{
						ok2 = false;
					}
				}
			}
			if (num8 > 0)
			{
				Check("moon: it rises later each night (by about a seventh of a day)", ok2, "later by " + string.Join(" and ", list2) + " of a day");
			}
			else
			{
				Log.Info("SUNPATH moon: no two days running with a moonrise each");
			}
		}

		private static float MoonHour(float cycle)
		{
			float result = 0f;
			float num = float.MinValue;
			for (int i = 0; i < 2000; i++)
			{
				float num2 = (float)i / 2000f;
				float num3 = SunPath.HourAngle(num2, 0f);
				if (!(Up(SunPath.Direction(num3, 0f)) > -12f))
				{
					SunPath.Moon(num3, 0f, cycle, out var hourAngle, out var moonDeclination);
					float num4 = Up(SunPath.Direction(hourAngle, moonDeclination));
					if (num4 > num)
					{
						num = num4;
						result = num2;
					}
				}
			}
			return result;
		}

		private static float Moonrise(float cycleNow, float sinceNow, float declination)
		{
			float num = 0f;
			for (int i = 0; i <= 2000; i++)
			{
				float num2 = (float)i / 2000f;
				SunPath.Moon(SunPath.HourAngle(0.25f + num2, declination), declination, cycleNow + (num2 - sinceNow) / 8f, out var hourAngle, out var moonDeclination);
				float z = SunPath.Direction(hourAngle, moonDeclination).Z;
				if (i > 0 && num < 0f && z >= 0f)
				{
					return num2;
				}
				num = z;
			}
			return -1f;
		}

		private static float HourAngleOf(Vec3 d)
		{
			float num = Mathf.Sin(0.4537856f);
			float num2 = Mathf.Cos(0.4537856f);
			return Mathf.Atan2(0f - d.X, num * d.Y + num2 * d.Z);
		}

		private float LitShare(Color32[] px, Vec3 moon, float scale, out string how)
		{
			how = "";
			FrameProjection shown = _mod.Shown;
			int grabW = _grabW;
			int grabH = _grabH;
			if (px.Length == 0 || grabW <= 0 || grabH <= 0 || grabW * grabH != px.Length || shown.Focal <= 0f || shown.Width <= 0 || shown.Height <= 0)
			{
				how = "no picture";
				return -1f;
			}
			if (!shown.Project(shown.View.X + moon.X * 500f, shown.View.Y + moon.Y * 500f, shown.View.Z + moon.Z * 500f, out var col, out var row, out var _))
			{
				how = "not in the picture";
				return -1f;
			}
			float num = col * (float)grabW / (float)shown.Width;
			float num2 = (1f - row / (float)shown.Height) * (float)grabH;
			float num3 = shown.Focal * Mathf.Tan(0.022f * Mathf.Max(0.5f, scale)) * (float)grabW / (float)shown.Width;
			double num4 = 0.0;
			int num5 = 0;
			int num6 = 0;
			int num7 = 0;
			float num8 = 0f;
			int num9 = Mathf.Max(0, (int)(num - num3 * 2.5f));
			int num10 = Mathf.Min(grabW - 1, (int)(num + num3 * 2.5f));
			int num11 = Mathf.Max(0, (int)(num2 - num3 * 2.5f));
			int num12 = Mathf.Min(grabH - 1, (int)(num2 + num3 * 2.5f));
			for (int i = 0; i < 2; i++)
			{
				float num13 = ((num5 > 0) ? ((float)(num4 / (double)num5)) : 0f);
				float num14 = num13 + (num8 - num13) * 0.5f;
				for (int j = num11; j <= num12; j++)
				{
					for (int k = num9; k <= num10; k++)
					{
						float num15 = (float)k + 0.5f - num;
						float num16 = (float)j + 0.5f - num2;
						float num17 = Mathf.Sqrt(num15 * num15 + num16 * num16);
						Color32 color = px[j * grabW + k];
						float num18 = 0.2126f * (float)(int)color.r + 0.7152f * (float)(int)color.g + 0.0722f * (float)(int)color.b;
						if (i == 0)
						{
							if (num17 >= num3 * 1.6f && num17 <= num3 * 2.4f)
							{
								num4 += (double)num18;
								num5++;
							}
							else if (num17 <= num3)
							{
								num8 = Mathf.Max(num8, num18);
							}
						}
						else if (num17 <= num3)
						{
							num6++;
							if (num18 > num14)
							{
								num7++;
							}
						}
					}
				}
			}
			float num19 = ((num5 > 0) ? ((float)(num4 / (double)num5)) : 0f);
			how = $"at {num:F0},{num2:F0} of {grabW}x{grabH}, {num3:F1} pixels round; the sky round it {num19:F0}, the disc's brightest {num8:F0}; {num7} of {num6} pixels lit";
			if (num6 == 0 || num8 - num19 < 30f)
			{
				return -1f;
			}
			return (float)num7 / (float)num6;
		}

		private IEnumerator SeasonBack()
		{
			if (_heldDay >= 0)
			{
				KeepDate();
				int dayIn;
				int length;
				Season season = Calendar.SeasonOf(_heldDay, out dayIn, out length);
				float start = Time.unscaledTime;
				while (Time.unscaledTime - start < 8f && Calendar.LookForTest[(int)season] < 0.99f)
				{
					yield return null;
				}
				_mod.ForceRelight();
				yield return new WaitForSecondsRealtime(1f);
			}
		}

		private IEnumerator Far()
		{
			Settings s = _mod.Settings;
			TerrainCache c = _mod.SceneTerrain;
			if (!FindForest(out var forest, out var trees))
			{
				Log.Info($"SUNPATH far: no forest on this map ({trees} trees at most in {16} tiles square)");
				yield break;
			}
			Vec3 sunDisc = Atmosphere.NaturalAt(0.72f, 0f, 0.5f).SunDisc;
			Vector2 normalized = new Vector2(0f - sunDisc.Y, sunDisc.X).normalized;
			View v = default(View);
			bool flag = false;
			float[] forestUps = ForestUps;
			foreach (float up in forestUps)
			{
				float num = 2f;
				float[] array = new float[2] { 1f, -1f };
				foreach (float num2 in array)
				{
					Vector2 vector = normalized * num2;
					Vector2 eye = forest - vector * 40f;
					float num3 = Mathf.Min(Mathf.Min(eye.x, eye.y), Mathf.Min((float)c.Width - eye.x, (float)c.Height - eye.y));
					if (!(num3 <= num) && SeesForest(eye, vector, up, forest))
					{
						num = num3;
						v = ForestView(eye, vector, up, forest);
						flag = true;
					}
				}
				if (flag)
				{
					break;
				}
			}
			if (!flag)
			{
				float num4 = float.MinValue;
				forestUps = new float[2] { 1f, -1f };
				foreach (float num5 in forestUps)
				{
					Vector2 vector2 = normalized * num5;
					Vector2 eye2 = forest - vector2 * 40f;
					float num6 = Mathf.Min(Mathf.Min(eye2.x, eye2.y), Mathf.Min((float)c.Width - eye2.x, (float)c.Height - eye2.y));
					if (!(num6 <= num4))
					{
						num4 = num6;
						v = ForestView(eye2, vector2, ForestUps[0], forest);
					}
				}
			}
			int num7 = ClearCone(v.Eye, v.Dir, 32f, 30f, 3f);
			Log.Info($"SUNPATH far: {trees} trees in {16} tiles square round {forest}, watched from {v.Eye} toward {v.Dir}, {v.Up} up, pitch {v.Pitch:F0}" + (flag ? ", its floor's middle in sight" : ", its floor's middle NOT in sight from any height") + $"; {num7} trees and the like cleared in front");
			if (s.Shadows <= 0)
			{
				_savedShadows = s.Shadows;
				s.Shadows = 2;
			}
			float shadowDistance = s.ShadowDistance;
			List<(string, (string, int, bool), float)> list = new List<(string, (string, int, bool), float)>
			{
				("natural_t072", Natural, 24f),
				("classic_t072", Classic, 24f)
			};
			if (Mathf.Abs(shadowDistance - 24f) > 1f)
			{
				list.Add(($"natural_t072_d{shadowDistance:0}", Natural, shadowDistance));
			}
			foreach (var r in list)
			{
				s.ShadowDistance = r.Item3;
				yield return Use(r.Item2, check: false);
				Look(v);
				yield return new WaitForSecondsRealtime(2f);
				SetSky(0.72f, 0f, 0.5f);
				yield return Settle(r.Item2.Item2 == 2);
				if (r.Item2.Item2 == 2)
				{
					float start = Time.unscaledTime;
					while (Time.unscaledTime - start < 15f && (!FieldReady() || !HasCanopy()))
					{
						yield return null;
					}
				}
				yield return Steady();
				_mod.SaveFrame("sunpath_far_" + r.Item1 + ".png");
				Color32[] px = Grab();
				string text = "";
				if (r.Item2.Item2 == 2 && r.Item3 == 24f)
				{
					int num8 = 0;
					int num9 = 0;
					int num10 = 0;
					for (int k = (int)forest.y - 8; k < (int)forest.y + 8; k++)
					{
						for (int l = (int)forest.x - 8; l < (int)forest.x + 8; l++)
						{
							float z = c.SampleHeight((float)l + 0.5f, (float)k + 0.5f);
							num8++;
							if (SunShadowField.Visibility(c, (float)l + 0.5f, (float)k + 0.5f, z, withCanopy: false) < 0.5f)
							{
								num10++;
							}
							else if (SunShadowField.Visibility(c, (float)l + 0.5f, (float)k + 0.5f, z, withCanopy: true) < 0.5f)
							{
								num9++;
							}
						}
					}
					text = $"; of the forest's {num8} tiles {num9} in its trees' shadow, {num10} in the land's";
					Check("far: the forest's floor shaded by its trees in the land's field (what shades the ground past the shadow map)", HasCanopy() && num9 * 10 >= num8, $"{num9} of {num8} tiles, {_mod.SamplerForTest.CanopyCount} things standing in the field");
				}
				Log.Info($"SUNPATH far {r.Item1} (Shadows distance {r.Item3:0}): brightness {Mean(px):F1} (lower third {MeanLow(px):F1}); sun {Up(_mod.Day.Air.SunDisc):F1} degrees up from {Compass(_mod.Day.Air.SunDisc):F0}{text}");
			}
		}

		private bool SeesForest(Vector2 eye, Vector2 dir, float up, Vector2 forest)
		{
			TerrainCache sceneTerrain = _mod.SceneTerrain;
			float num = sceneTerrain.SampleHeight(eye.x, eye.y) + up;
			float num2 = sceneTerrain.SampleHeight(forest.x, forest.y) + 0.5f;
			for (float num3 = 2f; num3 < 32f; num3 += 1f)
			{
				Vector2 vector = eye + dir * num3;
				int num4 = (int)vector.x;
				int num5 = (int)vector.y;
				float num6 = sceneTerrain.SampleHeight(vector.x, vector.y);
				if (num4 >= 0 && num5 >= 0 && num4 < sceneTerrain.Width && num5 < sceneTerrain.Height && _standing.Length == sceneTerrain.Width * sceneTerrain.Height && _standing[num5 * sceneTerrain.Width + num4] > 0)
				{
					num6 += 4f;
				}
				if (num6 > num + (num2 - num) * (num3 / 40f) - 0.5f)
				{
					return false;
				}
			}
			return true;
		}

		private View ForestView(Vector2 eye, Vector2 dir, float up, Vector2 forest)
		{
			TerrainCache sceneTerrain = _mod.SceneTerrain;
			float y = sceneTerrain.SampleHeight(eye.x, eye.y) + up - sceneTerrain.SampleHeight(forest.x, forest.y);
			return new View
			{
				Tag = "far",
				Eye = eye,
				Dir = dir,
				Pitch = (0f - Mathf.Atan2(y, 40f)) * 57.29578f,
				Up = up
			};
		}

		private bool FindForest(out Vector2 middle, out int trees)
		{
			middle = default(Vector2);
			trees = 0;
			TerrainCache sceneTerrain = _mod.SceneTerrain;
			int width = sceneTerrain.Width;
			int height = sceneTerrain.Height;
			if (width < 84 || height < 84)
			{
				return false;
			}
			int[] array = new int[(width + 1) * (height + 1)];
			foreach (Building simple in World.world.buildings.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple.current_tile != null && simple.asset != null && !(simple.asset.type != "type_tree"))
				{
					int x = simple.current_tile.x;
					int y = simple.current_tile.y;
					if (x >= 0 && y >= 0 && x < width && y < height)
					{
						array[(y + 1) * (width + 1) + x + 1]++;
					}
				}
			}
			for (int i = 1; i <= height; i++)
			{
				for (int j = 1; j <= width; j++)
				{
					array[i * (width + 1) + j] += array[(i - 1) * (width + 1) + j] + array[i * (width + 1) + j - 1] - array[(i - 1) * (width + 1) + j - 1];
				}
			}
			for (int k = 34; k + 16 <= height - 34; k += 2)
			{
				for (int l = 34; l + 16 <= width - 34; l += 2)
				{
					int num = array[(k + 16) * (width + 1) + l + 16] - array[k * (width + 1) + l + 16] - array[(k + 16) * (width + 1) + l] + array[k * (width + 1) + l];
					if (num > trees)
					{
						trees = num;
						middle = new Vector2((float)l + 8f, (float)k + 8f);
					}
				}
			}
			return trees >= 16;
		}

		private IEnumerator Rooms()
		{
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			List<(string Hour, Vec3 Light)> natural = new List<(string, Vec3)>();
			List<(string Hour, Vec3 Light)> classic = new List<(string, Vec3)>();
			(string Tag, int Style, bool Gpu)[] array = new(string, int, bool)[2] { Natural, Classic };
			for (int i = 0; i < array.Length; i++)
			{
				(string Tag, int Style, bool Gpu) m = array[i];
				yield return Use(m, check: false);
				bool flag = true;
				try
				{
					_mod.EnterRoomForTest(new List<Actor>(), "human", 3, "human_stone");
				}
				catch (Exception ex)
				{
					Check("rooms " + m.Tag + ": into the house", ok: false, ex.Message);
					flag = false;
				}
				if (!flag)
				{
					continue;
				}
				yield return new WaitForSecondsRealtime(0.8f);
				if (!_mod.Interior.Inside || _mod.Interior.Room == null)
				{
					Check("rooms " + m.Tag + ": into the house", ok: false, "no room");
					continue;
				}
				Interior room = _mod.Interior.Room;
				float yaw0 = _mod.ViewYaw;
				(string, float, bool)[] array2 = ((m.Style != 2) ? new(string, float, bool)[3]
				{
					("t030", 0.3f, false),
					("t042", 0.42f, true),
					("t070", 0.7f, false)
				} : new(string, float, bool)[3]
				{
					("t030", 0.3f, true),
					("t050", 0.5f, true),
					("t070", 0.7f, true)
				});
				(string, float, bool)[] array3 = array2;
				for (int j = 0; j < array3.Length; j++)
				{
					var (tag, t, frames) = array3[j];
					SetSky(t, 0f, 0.5f);
					_mod.SetYaw(yaw0);
					_mod.SetPitchDegrees(-22f);
					for (int k = 0; k < 10; k++)
					{
						yield return null;
					}
					Atmosphere air = _mod.Renderer.Air;
					Vec3 sunDisc = _mod.Day.Air.SunDisc;
					Vec3 light = air.Light;
					Vec3 lightDir = room.Floor.LightDir;
					((m.Style == 2) ? natural : classic).Add((tag, light));
					Log.Info(string.Format("SUNPATH room {0} {1}: {2} light from {3:F1} up, {4:F0} on the compass; the floor lit from ", m.Tag, tag, air.Indoors ? "the room's" : "NOT the room's", Up(light), Compass(light)) + $"{Up(lightDir):F1} up, {Compass(lightDir):F0}; the sun {Up(sunDisc):F1} up, {Compass(sunDisc):F0}; {room.Windows.Count} windows, the direct light {air.LightR:F2}/{air.LightG:F2}/{air.LightB:F2}");
					if (m.Style == 2)
					{
						float num = Mathf.Abs(Mathf.DeltaAngle(Compass(light), Compass(sunDisc)));
						float num2 = Mathf.Abs(Up(light) - Mathf.Max(8f, Up(sunDisc)));
						Check("room natural " + tag + ": the daylight comes in from where the sun stands", air.Indoors && num < 1f && num2 < 0.5f, $"{num:F2} degrees off its heading, {num2:F2} off its height (8 at least)");
						Check("room natural " + tag + ": the floor lit from it", Vec3.Dot(lightDir, light) >= 0.999f, $"{Angle(lightDir, light):F2} degrees apart");
					}
					if (frames)
					{
						(string, float)[] array4 = new(string, float)[4]
						{
							("", 0f),
							("_left", 1.2f),
							("_right", -1.2f),
							("_back", MathF.PI)
						};
						for (int k = 0; k < array4.Length; k++)
						{
							var (view, num3) = array4[k];
							_mod.SetYaw(yaw0 + num3);
							yield return new WaitForSecondsRealtime(0.35f);
							_mod.SaveFrame("sunpath_room_" + m.Tag + "_" + tag + view + ".png");
						}
						if (_cpu)
						{
							_mod.SetYaw(yaw0);
							_mod.Settings.GpuRender = false;
							yield return new WaitForSecondsRealtime(0.5f);
							_mod.SaveFrame("sunpath_room_" + m.Tag + "_" + tag + "_cpu.png");
							_mod.Settings.GpuRender = true;
							yield return new WaitForSecondsRealtime(0.3f);
						}
					}
				}
				_mod.SetYaw(yaw0);
				_mod.LeaveRoomForTest();
				yield return new WaitForSecondsRealtime(0.5f);
			}
			if (natural.Count == 3)
			{
				float num4 = 180f;
				List<string> list = new List<string>();
				for (int l = 0; l < 3; l++)
				{
					for (int n = l + 1; n < 3; n++)
					{
						float num5 = Angle(natural[l].Light, natural[n].Light);
						num4 = Mathf.Min(num4, num5);
						list.Add($"{natural[l].Hour}-{natural[n].Hour} {num5:F0}");
					}
				}
				Check("rooms: under the natural sun the daylight turns with the hour", num4 >= 20f, "degrees apart: " + string.Join(", ", list));
			}
			if (classic.Count == 3)
			{
				float num6 = Mathf.Max(Differ(classic[0].Light, classic[1].Light), Differ(classic[0].Light, classic[2].Light));
				Check("rooms: under the classic sun the daylight stays put whatever the hour", num6 < 1E-05f, $"from {Up(classic[0].Light):F1} up, {Compass(classic[0].Light):F0}; at most {num6:G3} apart in any axis");
			}
		}

		private static float Differ(Vec3 a, Vec3 b)
		{
			return Mathf.Max(Mathf.Abs(a.X - b.X), Mathf.Max(Mathf.Abs(a.Y - b.Y), Mathf.Abs(a.Z - b.Z)));
		}

		private static float Angle(Vec3 a, Vec3 b)
		{
			return Mathf.Acos(Mathf.Clamp(Vec3.Dot(a.Normalized(), b.Normalized()), -1f, 1f)) * 57.29578f;
		}

		private IEnumerator Steady()
		{
			Color32[] last = null;
			int calm = 0;
			float start = Time.unscaledTime;
			while (Time.unscaledTime - start < 8f && calm < 10)
			{
				yield return null;
				Color32[] array = Grab();
				if (last != null && array.Length == last.Length && array.Length != 0)
				{
					calm = ((Diff(last, array) * 1000 < array.Length * 3) ? (calm + 1) : 0);
				}
				last = array;
			}
			_steadyAfter = Time.unscaledTime - start;
			_steady = calm >= 10;
		}

		private IEnumerator Flare()
		{
			Settings s = _mod.Settings;
			int num = PutOutFires();
			if (num > 0)
			{
				Log.Info($"SUNPATH {num} fires put out before the lens flare");
			}
			(string Tag, int Style, bool Gpu)[] array = new(string, int, bool)[2] { Natural, Classic };
			for (int i = 0; i < array.Length; i++)
			{
				(string Tag, int Style, bool Gpu) m = array[i];
				yield return Use(m, check: false);
				SetSky(0.72f, 0f, 0.5f);
				yield return Settle(m.Style == 2);
				Vec3 sun = _mod.Day.Air.SunDisc;
				float horizon;
				int cleared;
				int flickers;
				View v = FlareView(sun, out horizon, out cleared, out flickers);
				float wet = Wet(v.Eye, v.Dir);
				Look(v);
				yield return new WaitForSecondsRealtime((cleared > 0) ? 3.5f : 1.5f);
				yield return Settle(m.Style == 2);
				float depth;
				for (int k = 0; k < 2; k++)
				{
					FrameProjection shown = _mod.Shown;
					if (shown.Focal <= 0f || !shown.Project(shown.View.X + sun.X * 500f, shown.View.Y + sun.Y * 500f, shown.View.Z + sun.Z * 500f, out var _, out var row, out depth))
					{
						break;
					}
					float num2 = (float)shown.Height * 0.25f;
					v.Pitch -= Mathf.Atan((row - num2) / shown.Focal) * 57.29578f;
					Look(v);
					for (int j = 0; j < 5; j++)
					{
						yield return null;
					}
				}
				yield return Steady();
				string at = "not in the picture";
				float sunX = -1f;
				float sunY = -1f;
				FrameProjection shown2 = _mod.Shown;
				if (shown2.Focal > 0f && shown2.Project(shown2.View.X + sun.X * 500f, shown2.View.Y + sun.Y * 500f, shown2.View.Z + sun.Z * 500f, out var col2, out var row2, out depth))
				{
					at = $"at {col2 / (float)Mathf.Max(1, shown2.Width):F2}, {row2 / (float)Mathf.Max(1, shown2.Height):F2} of the picture";
					sunX = col2 / (float)Mathf.Max(1, shown2.Width);
					sunY = 1f - row2 / (float)Mathf.Max(1, shown2.Height);
				}
				s.LensFlare = false;
				yield return Frames(20);
				Color32[] off = Grab();
				yield return Frames(20);
				Color32[] offAgain = Grab();
				_mod.SaveFrame("sunpath_flare_" + m.Tag + "_off.png");
				s.LensFlare = true;
				yield return Frames(20);
				Color32[] on = Grab();
				_mod.SaveFrame("sunpath_flare_" + m.Tag + "_on.png");
				yield return Frames(20);
				Color32[] array2 = Grab();
				int num3 = Apart(off, offAgain, on, array2, sunX, sunY);
				int num4 = Math.Max(Apart(off, on, offAgain, array2, sunX, sunY), Apart(off, array2, offAgain, on, sunX, sunY));
				string how;
				bool flag = SunShows(off, sunX, sunY, out how);
				string text = $"sun {Up(sun):F1} degrees up {at} ({how}), the land {horizon:F1} up that way ({wet * 100f:F0}% of it water), {cleared} trees and the like cleared, " + $"{flickers} fires or bonfires that way; {num3} pixels in patches along the sun's line through the middle held still within the pictures " + $"off and within those on and differ by more than {16} levels between them, {num4} with the pictures paired the other ways " + $"(the whole picture: {Diff(offAgain, on)} changed with the flare, {Math.Max(Diff(off, offAgain), Diff(on, array2))} between two alike)";
				string text2 = ((m.Style == 2) ? "natural: a lens flare facing the sun (Lens flare on against off)" : "classic: no lens flare (Lens flare on and off alike)");
				if (!flag)
				{
					Log.Info("SUNPATH (not judged: the sun's disc not seen at its place) " + text2 + " (" + text + ")");
				}
				else if (m.Style == 2)
				{
					Check(text2, num3 > num4 * 2 + 50, text);
				}
				else
				{
					Check(text2, num3 <= num4 * 3 / 2 + 50, text);
				}
			}
		}

		private static IEnumerator Frames(int n)
		{
			for (int i = 0; i < n; i++)
			{
				yield return null;
			}
		}

		private View FlareView(Vec3 sun, out float horizon, out int cleared, out int flickers)
		{
			Vector2 normalized = new Vector2(sun.X, sun.Y).normalized;
			float num = Up(sun);
			List<Vector2> list = new List<Vector2>();
			SceneCollector sceneForTest = _mod.SceneForTest;
			foreach (Building simple in World.world.buildings.getSimpleList())
			{
				if (simple == null || !simple.isAlive() || simple.current_tile == null)
				{
					continue;
				}
				try
				{
					if ((simple.asset != null && simple.asset.id != null && simple.asset.id.Contains("bonfire")) || Burning(simple) || Smouldering(sceneForTest, simple))
					{
						list.Add(new Vector2((float)simple.current_tile.x + 0.5f, (float)simple.current_tile.y + 0.5f));
					}
				}
				catch
				{
				}
			}
			List<Vector2> list2 = new List<Vector2>();
			foreach (View view in _views)
			{
				list2.Add(view.Eye);
			}
			Vector2 vector = ((_views.Count > 0) ? _views[0].Eye : Vector2.zero);
			for (int i = -60; i <= 60; i += 6)
			{
				for (int j = -60; j <= 60; j += 6)
				{
					WorldTile tile = World.world.GetTile((int)vector.x + j, (int)vector.y + i);
					if (Open(tile))
					{
						list2.Add(new Vector2((float)tile.x + 0.5f, (float)tile.y + 0.5f));
					}
				}
			}
			Vector2 vector2 = vector;
			float num2 = float.MinValue;
			horizon = 90f;
			flickers = 0;
			foreach (Vector2 item in list2)
			{
				float num3 = Horizon(item, 4f, normalized);
				int num4 = FiresIn(list, item, normalized, 14f, 48f, 6f);
				float num5 = num - num3 - (float)Standing(item, normalized, 14f, 40f) * 0.15f - Vector2.Distance(item, vector) * 0.01f - Wet(item, normalized) * 16f - (float)num4 * 20f - (StreetIn(item, normalized, 14f, 40f, 6f) ? 100f : 0f);
				if (num5 > num2)
				{
					num2 = num5;
					vector2 = item;
					horizon = num3;
					flickers = num4;
				}
			}
			cleared = ClearCone(vector2, normalized, 14f, 40f, 6f);
			foreach (Building item2 in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item2 == null || !item2.isAlive() || item2.current_tile == null)
				{
					continue;
				}
				try
				{
					if (!item2.isKingdomCiv() && !_street.Contains(item2) && sceneForTest != null && sceneForTest.SmoulderingForTest(item2))
					{
						Vector2 vector3 = new Vector2((float)item2.current_tile.x + 0.5f, (float)item2.current_tile.y + 0.5f);
						float num6 = Vector2.Distance(vector3, vector2);
						if (InCone(vector3, vector2, normalized, 14f + ((num6 > 1f) ? (Mathf.Atan2(7f, num6) * 57.29578f) : 90f), 48f, 6f))
						{
							item2.removeBuildingFinal();
							cleared++;
							flickers = Math.Max(0, flickers - 1);
						}
					}
				}
				catch
				{
				}
			}
			return new View
			{
				Tag = "flare",
				Eye = vector2,
				Dir = normalized,
				Pitch = num,
				Up = 4f
			};
		}

		private static bool Smouldering(SceneCollector scene, Building b)
		{
			if (scene == null || !scene.SmoulderingForTest(b))
			{
				if (b.isRuin() && b.asset != null)
				{
					return b.asset.building_type != BuildingType.Building_Tree;
				}
				return false;
			}
			return true;
		}

		private static int FiresIn(List<Vector2> fires, Vector2 eye, Vector2 dir, float halfDeg, float reach, float around)
		{
			int num = 0;
			foreach (Vector2 fire in fires)
			{
				float num2 = Vector2.Distance(fire, eye);
				if (InCone(fire, eye, dir, halfDeg + ((num2 > 1f) ? (Mathf.Atan2(7f, num2) * 57.29578f) : 90f), reach, around))
				{
					num++;
				}
			}
			return num;
		}

		private static float Wet(Vector2 eye, Vector2 toward)
		{
			int num = 0;
			int num2 = 0;
			for (int i = -1; i <= 1; i++)
			{
				Vector2 vector = Turn(toward, (float)i * 4f);
				for (float num3 = 3f; num3 <= 120f; num3 += 3f)
				{
					WorldTile tile = World.world.GetTile((int)(eye.x + vector.x * num3), (int)(eye.y + vector.y * num3));
					if (tile != null && tile.Type != null)
					{
						num2++;
						if (tile.Type.liquid && !tile.Type.lava)
						{
							num++;
						}
					}
				}
			}
			if (num2 <= 0)
			{
				return 0f;
			}
			return (float)num / (float)num2;
		}

		private IEnumerator Fps()
		{
			View v = _views[0];
			List<bool> list = new List<bool> { true };
			if (_cpu)
			{
				list.Add(item: false);
			}
			int others = OtherGames();
			foreach (bool gpu in list)
			{
				float[] array = new float[2] { 0.5f, 0.72f };
				foreach (float t in array)
				{
					List<float> natural = new List<float>();
					List<float> classic = new List<float>();
					double[,] cost = new double[2, 3];
					float seconds = (gpu ? 2.5f : 5f);
					for (int round = 0; round < 5; round++)
					{
						int[] array2 = ((round % 2 != 0) ? new int[2] { 1, 2 } : new int[2] { 2, 1 });
						foreach (int style in array2)
						{
							yield return Use((Tag: (style == 2) ? "natural" : "classic", Style: style, Gpu: gpu), check: false);
							Look(v);
							SetSky(t, 0f, 0.5f);
							yield return Settle(style == 2);
							yield return new WaitForSecondsRealtime(0.5f);
							float pace = 0.00083333335f;
							float start = Time.unscaledTime;
							int frames = 0;
							if (gpu)
							{
								_mod.RestartPerfWindowsForTest();
							}
							while (Time.unscaledTime - start < seconds)
							{
								_mod.Day.TimeOfDay += Time.unscaledDeltaTime * pace;
								yield return null;
								frames++;
							}
							((style == 2) ? natural : classic).Add((float)frames / Mathf.Max(0.01f, Time.unscaledTime - start));
							if (gpu)
							{
								_mod.GpuSumsForTest(out var scene, out var submit, out var frames2);
								int num = ((style != 2) ? 1 : 0);
								cost[num, 0] += scene;
								cost[num, 1] += submit;
								cost[num, 2] += frames2;
							}
						}
					}
					float num2 = Best(natural);
					float num3 = Best(classic);
					string text = string.Format("{0} at {1}: the natural sun keeps {2:F0}% of the classic's frame rate", gpu ? "GPU" : "CPU", t, 85f);
					string text2 = $"natural {num2:F0} fps, classic {num3:F0} fps (uncapped, the best of {5} rounds of {seconds:F0} s, the middle {Middle(natural):F0} and {Middle(classic):F0}: natural " + string.Join("/", natural.ConvertAll((float f) => f.ToString("F0"))) + ", classic " + string.Join("/", classic.ConvertAll((float f) => f.ToString("F0"))) + ")";
					if (gpu)
					{
						text2 = text2 + $"; the card's path on the main thread a frame: natural {cost[0, 0] / Math.Max(1.0, cost[0, 2]):F2} ms gathering + {cost[0, 1] / Math.Max(1.0, cost[0, 2]):F2} sending, " + $"classic {cost[1, 0] / Math.Max(1.0, cost[1, 2]):F2} + {cost[1, 1] / Math.Max(1.0, cost[1, 2]):F2}";
					}
					if (others > 0)
					{
						Log.Info($"SUNPATH (not judged: {others} other copies of the game running) {text} ({text2})");
					}
					else
					{
						Check(text, num2 >= num3 * 0.85f, text2);
					}
				}
			}
		}

		private static int OtherGames()
		{
			try
			{
				int id = Process.GetCurrentProcess().Id;
				int num = 0;
				Process[] processesByName = Process.GetProcessesByName("worldbox");
				foreach (Process obj in processesByName)
				{
					if (obj.Id != id)
					{
						num++;
					}
					obj.Dispose();
				}
				return num;
			}
			catch
			{
				return 0;
			}
		}

		private static float Best(List<float> all)
		{
			float num = 0f;
			foreach (float item in all)
			{
				num = Mathf.Max(num, item);
			}
			return num;
		}

		private static float Middle(List<float> all)
		{
			if (all.Count == 0)
			{
				return 0f;
			}
			List<float> list = new List<float>(all);
			list.Sort();
			return list[list.Count / 2];
		}

		private IEnumerator God()
		{
			Settings s = _mod.Settings;
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			s.GodView = true;
			s.GpuRender = true;
			float zoom = Mathf.Min(25f, s.GodViewZoom * 0.8f);
			for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
			{
				HoldCamera(_ridge, zoom);
				yield return null;
			}
			int shown = 0;
			(string Tag, int Style, bool Gpu)[] array = new(string, int, bool)[2] { Natural, Classic };
			for (int i = 0; i < array.Length; i++)
			{
				(string Tag, int Style, bool Gpu) m = array[i];
				s.SunStyle = m.Style;
				(string, float)[] array2 = new(string, float)[3]
				{
					("t027", 0.27f),
					("t050", 0.5f),
					("t072", 0.72f)
				};
				for (int j = 0; j < array2.Length; j++)
				{
					(string, float) h = array2[j];
					SetSky(h.Item2, 0f, 0.5f);
					float t = Time.unscaledTime;
					while (Time.unscaledTime - t < 1f || (m.Style == 2 && !FieldReady() && Time.unscaledTime - t < 6f))
					{
						HoldCamera(_ridge, zoom);
						yield return null;
					}
					for (float t2 = 0f; t2 < 0.4f; t2 += Time.unscaledDeltaTime)
					{
						HoldCamera(_ridge, zoom);
						yield return null;
					}
					if (!_mod.GodShown)
					{
						Log.Info($"SUNPATH god view not shown at zoom {zoom:F0} ({m.Tag} {h.Item1})");
						continue;
					}
					shown++;
					_mod.SaveFrame("sunpath_god_" + m.Tag + "_" + h.Item1 + ".png");
				}
			}
			Check("the god view drawn over the ridge under each sun", shown == 6, $"{shown} of 6 frames");
		}

		private static void HoldCamera(Vector2 at, float zoom)
		{
			try
			{
				Camera camera = World.world.camera;
				camera.transform.position = new Vector3(at.x, at.y, camera.transform.position.z);
				camera.orthographicSize = zoom;
				if (World.world.move_camera != null)
				{
					World.world.move_camera.setTargetZoom(zoom);
				}
				World.world.setZoomOrthographic(zoom);
			}
			catch (Exception ex)
			{
				Log.Error("SUNPATH camera: " + ex.Message);
			}
		}

		private IEnumerator Use((string Tag, int Style, bool Gpu) m, bool check)
		{
			Settings settings = _mod.Settings;
			settings.SunStyle = m.Style;
			settings.GpuRender = m.Gpu;
			_mod.ForceRelight();
			for (int i = 0; i < 10; i++)
			{
				yield return null;
			}
			TerrainCache c = _mod.SceneTerrain;
			WorldSampler w = _mod.SamplerForTest;
			if (m.Style == 2)
			{
				float start = Time.unscaledTime;
				while (Time.unscaledTime - start < 15f && (!FieldReady() || (!HasCanopy() && w.CanopyWanted)))
				{
					yield return null;
				}
				if (check)
				{
					Check(m.Tag + ": the day runs the natural sun", _mod.Day.NaturalCycle);
					Check(m.Tag + ": the land's shadow field is baked", c.SoftLandShadows && c.SunField != null);
					if (w.CanopyWanted)
					{
						Check(m.Tag + ": what stands on the land is in the field", HasCanopy(), $"{w.CanopyCount} things");
					}
					else
					{
						Log.Info($"SUNPATH {m.Tag}: no shadow map of late (shadows {_mod.Settings.Shadows}, darkness {_mod.Settings.ShadowDarkness:0.##}): what stands on the land isn't gathered");
					}
					Log.Info($"SUNPATH {m.Tag}: the field ({c.Width}x{c.Height} tiles) baked in {w.SunBakeMs:F1} ms on a worker; what stands on the land, {w.CanopyCount} things, " + $"gathered in {w.CanopyMs:F1} ms over as many frames as it took (at most 0.5 ms a frame)");
					Check($"{m.Tag}: a bake takes under {200.0:F0} ms", w.SunBakeMs < 200.0, $"{w.SunBakeMs:F1} ms");
				}
			}
			else
			{
				for (int i = 0; i < 10; i++)
				{
					yield return null;
				}
				if (check)
				{
					Check(m.Tag + ": the day keeps the classic sun", !_mod.Day.NaturalCycle);
					Check(m.Tag + ": no land shadow field", !c.SoftLandShadows && c.SunField == null && c.SunFieldPrev == null);
				}
			}
		}

		private void SetSky(float t, float declDegrees, float? moon, float lit = -1f)
		{
			Config.paused = true;
			_mod.Settings.TimeMode = 0;
			_mod.Day.TestDeclination = declDegrees * (MathF.PI / 180f);
			_mod.Day.TestMoonCycle = moon;
			_mod.Day.TestMoonLit = ((lit >= 0f) ? new float?(lit) : ((float?)null));
			_mod.Day.TimeOfDay = t;
			try
			{
				CalendarLast?.SetValue(null, t);
			}
			catch
			{
			}
			_mod.ForceRelight();
			KeepDate();
		}

		private IEnumerator Settle(bool natural)
		{
			float start = Time.unscaledTime;
			for (int i = 0; i < 3; i++)
			{
				yield return null;
			}
			if (natural)
			{
				while (!FieldReady() && Time.unscaledTime - start < 6f)
				{
					yield return null;
				}
			}
			_readyAfter = Time.unscaledTime - start;
			yield return new WaitForSecondsRealtime(0.4f);
		}

		private bool FieldReady()
		{
			TerrainCache sceneTerrain = _mod.SceneTerrain;
			SunShadowField sunField = sceneTerrain.SunField;
			if (sceneTerrain.SoftLandShadows && sunField != null && sceneTerrain.SunFieldBlend >= 1f)
			{
				return Vec3.Dot(sunField.Light, sceneTerrain.LightDir) > 0.99999f;
			}
			return false;
		}

		private bool HasCanopy()
		{
			SunShadowField sunField = _mod.SceneTerrain.SunField;
			if (sunField != null && _mod.SamplerForTest.CanopyCount > 0)
			{
				return sunField.SAll != sunField.SLand;
			}
			return false;
		}

		private void Look(View v)
		{
			_mod.DebugEyeSet = true;
			_mod.DebugEye = v.Eye;
			_mod.DebugEyeHeight = v.Up;
			_mod.SetYaw(Mathf.Atan2(v.Dir.y, v.Dir.x));
			_mod.SetPitchDegrees(v.Pitch);
		}

		private void PlanViews(Vector2 house)
		{
			int num = PutOutFires();
			if (num > 0)
			{
				Log.Info($"SUNPATH {num} fires put out (tiles, and trees, buildings and creatures alight)");
			}
			CountStanding();
			Vector2 vector = Turn(new Vector2(0f, -1f), 35f);
			_views.Add(new View
			{
				Tag = "street",
				Eye = house - vector * 10f,
				Dir = vector,
				Pitch = -9f,
				Up = 0f
			});
			_ridge = house;
			if (FindRidge(out var view, out var peak))
			{
				int num2 = ClearCone(view.Eye, view.Dir, 32f, Mathf.Min(24f, Vector2.Distance(view.Eye, peak) - 8f), 4f);
				Log.Info($"SUNPATH valley: {num2} trees and the like cleared in front of the eye");
				_views.Add(view);
				_ridge = peak;
			}
			else
			{
				Log.Info("SUNPATH no ridge stands 3 tiles over a valley in sight on this map: no valley view");
			}
			if (FindShore(house, out var view2, out var shore, out var across))
			{
				int num3 = ClearCone(view2.Eye, view2.Dir, 40f, Mathf.Max(4f, shore), 3f);
				Log.Info($"SUNPATH water: the water from {shore:F0} tiles off, the land across it rising {across:F1} degrees; {num3} trees and the like cleared in front of the eye");
				_views.Add(view2);
			}
			else
			{
				Log.Info("SUNPATH no shore with land across the water on this map: no water view");
			}
		}

		private void CountStanding()
		{
			TerrainCache sceneTerrain = _mod.SceneTerrain;
			_standing = new int[Math.Max(0, sceneTerrain.Width * sceneTerrain.Height)];
			foreach (Building simple in World.world.buildings.getSimpleList())
			{
				if (simple != null && simple.isAlive() && simple.current_tile != null && !simple.isKingdomCiv() && !_street.Contains(simple))
				{
					int x = simple.current_tile.x;
					int y = simple.current_tile.y;
					if (x >= 0 && y >= 0 && x < sceneTerrain.Width && y < sceneTerrain.Height)
					{
						_standing[y * sceneTerrain.Width + x]++;
					}
				}
			}
		}

		private int Standing(Vector2 eye, Vector2 dir, float halfDeg, float reach)
		{
			TerrainCache sceneTerrain = _mod.SceneTerrain;
			if (_standing.Length < sceneTerrain.Width * sceneTerrain.Height || sceneTerrain.Width == 0)
			{
				return 0;
			}
			int num = 0;
			int num2 = Mathf.Max(0, (int)(eye.x - reach));
			int num3 = Mathf.Min(sceneTerrain.Width - 1, (int)(eye.x + reach));
			int num4 = Mathf.Max(0, (int)(eye.y - reach));
			int num5 = Mathf.Min(sceneTerrain.Height - 1, (int)(eye.y + reach));
			for (int i = num4; i <= num5; i++)
			{
				for (int j = num2; j <= num3; j++)
				{
					if (_standing[i * sceneTerrain.Width + j] > 0 && InCone(new Vector2((float)j + 0.5f, (float)i + 0.5f), eye, dir, halfDeg, reach, 1.5f))
					{
						num += _standing[i * sceneTerrain.Width + j];
					}
				}
			}
			return num;
		}

		private bool StreetIn(Vector2 eye, Vector2 dir, float halfDeg, float reach, float around)
		{
			foreach (Building item in _street)
			{
				if (item != null && item.isAlive() && item.current_tile != null)
				{
					Vector2 vector = new Vector2((float)item.current_tile.x + 0.5f, (float)item.current_tile.y + 0.5f);
					float num = Vector2.Distance(vector, eye);
					if (InCone(vector, eye, dir, halfDeg + ((num > 1f) ? (Mathf.Atan2(2f, num) * 57.29578f) : 90f), reach + 2f, around + 2f))
					{
						return true;
					}
				}
			}
			return false;
		}

		private static bool InCone(Vector2 p, Vector2 eye, Vector2 dir, float halfDeg, float reach, float around)
		{
			Vector2 lhs = p - eye;
			float magnitude = lhs.magnitude;
			if (magnitude <= around)
			{
				return true;
			}
			if (magnitude > reach)
			{
				return false;
			}
			return Vector2.Dot(lhs, dir) >= magnitude * Mathf.Cos(halfDeg * (MathF.PI / 180f));
		}

		private int ClearCone(Vector2 eye, Vector2 dir, float halfDeg, float reach, float around)
		{
			int num = 0;
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item == null || !item.isAlive() || item.current_tile == null)
				{
					continue;
				}
				try
				{
					if (!item.isKingdomCiv() && !_street.Contains(item) && InCone(new Vector2((float)item.current_tile.x + 0.5f, (float)item.current_tile.y + 0.5f), eye, dir, halfDeg, reach, around))
					{
						item.removeBuildingFinal();
						num++;
					}
				}
				catch
				{
				}
			}
			if (num > 0)
			{
				CountStanding();
			}
			return num;
		}

		private bool FindRidge(out View view, out Vector2 peak)
		{
			view = default(View);
			peak = default(Vector2);
			TerrainCache sceneTerrain = _mod.SceneTerrain;
			int width = sceneTerrain.Width;
			int height = sceneTerrain.Height;
			if (width == 0 || sceneTerrain.Heights.Length < width * height)
			{
				return false;
			}
			List<(float, int, int)> list = new List<(float, int, int)>();
			for (int i = 8; i < height - 8; i += 3)
			{
				for (int j = 8; j < width - 8; j += 3)
				{
					float num = sceneTerrain.Heights[i * width + j];
					if (num >= 3f)
					{
						list.Add((num, j, i));
					}
				}
			}
			list.Sort(((float Z, int X, int Y) a, (float Z, int X, int Y) b) => b.Z.CompareTo(a.Z));
			List<(float, int, int)> list2 = new List<(float, int, int)>();
			foreach (var item in list)
			{
				if (list2.Count >= 40)
				{
					break;
				}
				bool flag = false;
				foreach (var item2 in list2)
				{
					if ((item.Item2 - item2.Item2) * (item.Item2 - item2.Item2) + (item.Item3 - item2.Item3) * (item.Item3 - item2.Item3) < 400)
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					list2.Add(item);
				}
			}
			float num2 = float.MinValue;
			foreach (var item3 in list2)
			{
				Vector2 vector = new Vector2((float)item3.Item2 + 0.5f, (float)item3.Item3 + 0.5f);
				for (int num3 = 0; num3 < 16; num3++)
				{
					for (float num4 = 30f; num4 <= 70f; num4 += 10f)
					{
						float f = (float)num3 * MathF.PI / 8f;
						Vector2 vector2 = new Vector2(Mathf.Cos(f), Mathf.Sin(f));
						Vector2 vector3 = vector - vector2 * num4;
						if (!Open(World.world.GetTile((int)vector3.x, (int)vector3.y)))
						{
							continue;
						}
						float num5 = sceneTerrain.SampleHeight(vector3.x, vector3.y);
						float num6 = item3.Item1 - num5;
						float num7 = num5 + 3f;
						if (num6 < 3f)
						{
							continue;
						}
						bool flag2 = true;
						for (float num8 = 2f; num8 < num4 - 3f && flag2; num8 += 1f)
						{
							Vector2 vector4 = vector3 + vector2 * num8;
							if (sceneTerrain.SampleHeight(vector4.x, vector4.y) > num7 + (item3.Item1 - num7) * (num8 / num4) + 0.2f)
							{
								flag2 = false;
							}
						}
						if (flag2)
						{
							float num9 = num6 - num4 * 0.05f - (float)Standing(vector3, vector2, 32f, Mathf.Min(24f, num4 - 8f)) * 0.1f - (StreetIn(vector3, vector2, 32f, Mathf.Min(24f, num4 - 8f), 4f) ? 100f : 0f);
							if (!(num9 <= num2))
							{
								num2 = num9;
								peak = vector;
								float value = Mathf.Atan2((item3.Item1 - num7) * 0.6f, num4) * 57.29578f;
								view = new View
								{
									Tag = "valley",
									Eye = vector3,
									Dir = vector2,
									Pitch = Mathf.Clamp(value, -6f, 18f),
									Up = 3f
								};
							}
						}
					}
				}
			}
			if (num2 > float.MinValue)
			{
				Log.Info($"SUNPATH valley: the ridge at {peak} stands {sceneTerrain.SampleHeight(peak.x, peak.y) - sceneTerrain.SampleHeight(view.Eye.x, view.Eye.y):F1} over the ground at the eye {Vector2.Distance(view.Eye, peak):F0} tiles off, " + $"{Standing(view.Eye, view.Dir, 32f, 24f)} trees and the like in front");
			}
			return num2 > float.MinValue;
		}

		private bool FindShore(Vector2 near, out View view, out float shore, out float across)
		{
			view = default(View);
			shore = (across = 0f);
			TerrainCache sceneTerrain = _mod.SceneTerrain;
			int width = sceneTerrain.Width;
			int height = sceneTerrain.Height;
			float num = float.MinValue;
			for (int i = 6; i < height - 6; i += 5)
			{
				for (int j = 6; j < width - 6; j += 5)
				{
					if (!Open(World.world.GetTile(j, i)) || !NearWater(j, i))
					{
						continue;
					}
					Vector2 vector = new Vector2((float)j + 0.5f, (float)i + 0.5f);
					float num2 = sceneTerrain.SampleHeight(vector.x, vector.y) + 2f;
					for (int k = 0; k < 16; k++)
					{
						float num3 = (float)k * MathF.PI / 8f;
						int num4 = 0;
						int num5 = 0;
						float num6 = -90f;
						float num7 = 0f;
						for (int l = -2; l <= 2; l++)
						{
							float f = num3 + (float)l * 10f * (MathF.PI / 180f);
							Vector2 vector2 = new Vector2(Mathf.Cos(f), Mathf.Sin(f));
							bool flag = false;
							for (float num8 = 3f; num8 <= 72f; num8 += 3f)
							{
								Vector2 vector3 = vector + vector2 * num8;
								WorldTile tile = World.world.GetTile((int)vector3.x, (int)vector3.y);
								if (tile == null || tile.Type == null || tile.Type.lava)
								{
									continue;
								}
								if (tile.Type.liquid)
								{
									if (num8 <= 48f)
									{
										num4++;
									}
									if (!flag && l == 0)
									{
										num7 = num8;
									}
									flag = true;
								}
								else
								{
									if (num8 <= 48f)
									{
										num5++;
									}
									if (flag)
									{
										num6 = Mathf.Max(num6, Mathf.Atan2(sceneTerrain.SampleHeight(vector3.x, vector3.y) - num2, num8) * 57.29578f);
									}
								}
							}
						}
						int num9 = num4 + num5;
						if (num9 < 40)
						{
							continue;
						}
						float num10 = (float)num4 / (float)num9;
						if (!(num10 < 0.35f) && !(num10 > 0.85f))
						{
							Vector2 dir = new Vector2(Mathf.Cos(num3), Mathf.Sin(num3));
							float num11 = num10 * (1f - num10) * 4f * (1f + Mathf.Clamp(num6, -2f, 12f) / 4f) - (float)Standing(vector, dir, 40f, Mathf.Max(4f, num7)) * 0.05f - (vector - near).magnitude * 0.0005f - (StreetIn(vector, dir, 40f, Mathf.Max(4f, num7), 3f) ? 100f : 0f);
							if (!(num11 <= num))
							{
								num = num11;
								shore = num7;
								across = num6;
								view = new View
								{
									Tag = "water",
									Eye = vector,
									Dir = dir,
									Pitch = -5f,
									Up = 2f
								};
							}
						}
					}
				}
			}
			return num > float.MinValue;
		}

		private static bool Open(WorldTile t)
		{
			if (t != null && t.Type != null && !t.Type.liquid && !t.Type.block)
			{
				return !t.Type.lava;
			}
			return false;
		}

		private int PutOutFires()
		{
			TerrainCache sceneTerrain = _mod.SceneTerrain;
			int num = 0;
			for (int i = 0; i < sceneTerrain.Height; i++)
			{
				for (int j = 0; j < sceneTerrain.Width; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					try
					{
						if (tile != null && tile.isOnFire())
						{
							tile.stopFire();
							num++;
						}
					}
					catch
					{
					}
				}
			}
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				try
				{
					if (item != null && item.isAlive() && Burning(item))
					{
						item.finishStatusEffect("burning");
						num++;
					}
				}
				catch
				{
				}
			}
			foreach (Actor item2 in new List<Actor>(World.world.units.getSimpleList()))
			{
				try
				{
					if (item2 != null && item2.isAlive() && Burning(item2))
					{
						item2.finishStatusEffect("burning");
						num++;
					}
				}
				catch
				{
				}
			}
			return num;
		}

		private static bool Burning(BaseSimObject o)
		{
			if (o.hasAnyStatusEffect())
			{
				return o.hasStatus("burning");
			}
			return false;
		}

		private static bool NearWater(int x, int y)
		{
			for (int i = 2; i <= 4; i += 2)
			{
				for (int j = 0; j < 8; j++)
				{
					float f = (float)j * MathF.PI / 4f;
					WorldTile tile = World.world.GetTile(x + Mathf.RoundToInt(Mathf.Cos(f) * (float)i), y + Mathf.RoundToInt(Mathf.Sin(f) * (float)i));
					if (tile != null && tile.Type != null && tile.Type.liquid && !tile.Type.lava)
					{
						return true;
					}
				}
			}
			return false;
		}

		private float Horizon(Vector2 eye, float up, Vector2 toward)
		{
			TerrainCache sceneTerrain = _mod.SceneTerrain;
			float num = sceneTerrain.SampleHeight(eye.x, eye.y) + up;
			float num2 = -90f;
			for (float num3 = 2f; num3 < 150f; num3 += 1f)
			{
				Vector2 vector = eye + toward * num3;
				num2 = Mathf.Max(num2, Mathf.Atan2(sceneTerrain.SampleHeight(vector.x, vector.y) - num, num3) * 57.29578f);
			}
			return num2;
		}

		private Vector2 BuildStreet(Vector2 home, out int built)
		{
			built = 0;
			WorldTile worldTile = Dry(home + new Vector2(0f, -12f), 6, 60);
			if (worldTile == null)
			{
				return home;
			}
			Clear(worldTile, 14);
			Vector2 result = new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f);
			int[] array = new int[3] { -1, 0, 1 };
			foreach (int num in array)
			{
				Building building = Build((num == 0) ? "house_human_3" : "house_human_1", World.world.GetTile(worldTile.x + num * 5, worldTile.y));
				if (building != null)
				{
					_street.Add(building);
					built++;
					if (num == 0)
					{
						result = building.current_position;
					}
				}
			}
			return result;
		}

		private static void Clear(WorldTile at, int r)
		{
			foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
			{
				if (item == null || !item.isAlive() || item.current_tile == null)
				{
					continue;
				}
				int num = item.current_tile.x - at.x;
				int num2 = item.current_tile.y - at.y;
				try
				{
					if (num * num + num2 * num2 <= r * r && !item.isKingdomCiv())
					{
						item.removeBuildingFinal();
					}
				}
				catch
				{
				}
			}
		}

		private static Building Build(string id, WorldTile at)
		{
			if (at == null)
			{
				return null;
			}
			try
			{
				Building building = World.world.buildings.addBuilding(id, at, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				building?.completeConstruction();
				return building;
			}
			catch (Exception ex)
			{
				Log.Error("SUNPATH could not build " + id + ": " + ex.Message);
				return null;
			}
		}

		private static WorldTile Dry(Vector2 c, int r, int reach)
		{
			for (int i = 0; i < reach; i += 2)
			{
				for (int j = 0; j < 32; j++)
				{
					float f = (float)j / 32f * MathF.PI * 2f;
					int num = (int)(c.x + Mathf.Cos(f) * (float)i);
					int num2 = (int)(c.y + Mathf.Sin(f) * (float)i);
					bool flag = true;
					for (int k = -r; k <= r + 1 && flag; k++)
					{
						for (int l = -r; l <= r && flag; l++)
						{
							WorldTile tile = World.world.GetTile(num + l, num2 + k);
							if (tile == null || tile.Type == null || tile.Type.liquid || tile.Type.block || tile.Type.lava || (tile.building != null && tile.building.isKingdomCiv()))
							{
								flag = false;
							}
						}
					}
					if (flag)
					{
						return World.world.GetTile(num, num2);
					}
					if (i == 0)
					{
						break;
					}
				}
			}
			return null;
		}

		private Color32[] Grab()
		{
			GpuRenderer gpuRendererForTest = _mod.GpuRendererForTest;
			if (gpuRendererForTest != null && gpuRendererForTest.Output != null && _mod.ShownFrame == gpuRendererForTest.Shown)
			{
				Texture2D texture2D = gpuRendererForTest.ReadOutput();
				if (texture2D == null)
				{
					return new Color32[0];
				}
				Color32[] pixels = texture2D.GetPixels32();
				_grabW = texture2D.width;
				_grabH = texture2D.height;
				UnityEngine.Object.Destroy(texture2D);
				return pixels;
			}
			if (_mod.Frame == null)
			{
				return new Color32[0];
			}
			_grabW = _mod.Frame.width;
			_grabH = _mod.Frame.height;
			return _mod.Frame.GetPixels32();
		}

		private static float Mean(Color32[] px)
		{
			return Mean(px, px.Length);
		}

		private static float MeanLow(Color32[] px)
		{
			return Mean(px, px.Length / 3);
		}

		private float MeanLand(Color32[] px)
		{
			return MeanLand(px, null);
		}

		private float MeanLand(Color32[] px, Color32[] other)
		{
			int grabW = _grabW;
			int grabH = _grabH;
			if (grabW <= 0 || grabH <= 0 || grabW * grabH != px.Length)
			{
				return MeanLow(px);
			}
			if (other != null && other.Length != px.Length)
			{
				other = null;
			}
			int num = grabH / 3;
			int num2 = (int)((float)grabH * 0.3f);
			int num3 = (int)((float)grabW * 0.4f);
			int num4 = (int)((float)grabW * 0.6f);
			double num5 = 0.0;
			int num6 = 0;
			for (int i = 0; i < num; i++)
			{
				bool num7 = i < num2;
				int j = (num7 ? num3 : 0);
				for (int num8 = (num7 ? num4 : grabW); j < num8; j++)
				{
					Color32 p = px[i * grabW + j];
					if (other == null || (!Warm(p) && !Warm(other[i * grabW + j])))
					{
						num5 += 0.2126 * (double)(int)p.r + 0.7152 * (double)(int)p.g + 0.0722 * (double)(int)p.b;
						num6++;
					}
				}
			}
			if (num6 <= 0)
			{
				return 0f;
			}
			return (float)(num5 / (double)num6);
		}

		private static bool Warm(Color32 p)
		{
			return p.r > p.b + 16;
		}

		private static float Mean(Color32[] px, int n)
		{
			if (n <= 0)
			{
				return 0f;
			}
			double num = 0.0;
			for (int i = 0; i < n; i++)
			{
				num += 0.2126 * (double)(int)px[i].r + 0.7152 * (double)(int)px[i].g + 0.0722 * (double)(int)px[i].b;
			}
			return (float)(num / (double)n);
		}

		private static int Luma(Color32 p)
		{
			return Mathf.Clamp((int)(0.2126f * (float)(int)p.r + 0.7152f * (float)(int)p.g + 0.0722f * (float)(int)p.b), 0, 255);
		}

		private static float LitLandShare(Color32[] dark, Color32[] full)
		{
			int num = full.Length / 3;
			if (num <= 0 || dark.Length != full.Length)
			{
				return 1f;
			}
			int[] array = new int[256];
			bool[] array2 = new bool[num];
			int num2 = 0;
			for (int i = 0; i < num; i++)
			{
				int num3 = Luma(full[i]);
				if (!Warm(full[i]) && !Warm(dark[i]) && Math.Abs(num3 - Luma(dark[i])) > 2)
				{
					array2[i] = true;
					array[num3]++;
					num2++;
				}
			}
			if (num2 <= 0)
			{
				return 1f;
			}
			int num4 = 255;
			int num5 = array[255];
			while (num4 > 0 && num5 * 10 < num2)
			{
				num4--;
				num5 += array[num4];
			}
			double num6 = 0.0;
			double num7 = 0.0;
			for (int j = 0; j < num; j++)
			{
				if (array2[j] && Luma(full[j]) >= num4)
				{
					num6 += 0.2126 * (double)(int)dark[j].r + 0.7152 * (double)(int)dark[j].g + 0.0722 * (double)(int)dark[j].b;
					num7 += 0.2126 * (double)(int)full[j].r + 0.7152 * (double)(int)full[j].g + 0.0722 * (double)(int)full[j].b;
				}
			}
			if (!(num7 > 0.0))
			{
				return 1f;
			}
			return (float)(num6 / num7);
		}

		private static int Diff(Color32[] a, Color32[] b)
		{
			if (a.Length != b.Length)
			{
				return Math.Max(a.Length, b.Length);
			}
			int num = 0;
			for (int i = 0; i < a.Length; i++)
			{
				if (Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b) > 12)
				{
					num++;
				}
			}
			return num;
		}

		private int Apart(Color32[] a1, Color32[] a2, Color32[] b1, Color32[] b2, float sx, float sy)
		{
			int num = a1.Length;
			if (a2.Length != num || b1.Length != num || b2.Length != num)
			{
				return num;
			}
			int grabW = _grabW;
			int grabH = _grabH;
			bool flag = grabW > 0 && grabH > 0 && grabW * grabH == num;
			bool flag2 = sx >= 0f && flag;
			float num2 = sx * (float)grabW;
			float num3 = sy * (float)grabH;
			float num4 = (float)grabW * 0.5f - num2;
			float num5 = (float)grabH * 0.5f - num3;
			float num6 = Mathf.Sqrt(num4 * num4 + num5 * num5);
			float num7 = (float)grabH * 0.1f;
			if (num6 < 1f)
			{
				flag2 = false;
			}
			bool[] array = new bool[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = !Changed(a1, a2, i) && !Changed(b1, b2, i) && Changed(a1, b1, i) && Changed(a1, b2, i) && Changed(a2, b1, i) && Changed(a2, b2, i);
			}
			int num8 = 0;
			for (int j = 0; j < num; j++)
			{
				if (!array[j])
				{
					continue;
				}
				if (flag2)
				{
					float num9 = (float)(j % grabW) + 0.5f - num2;
					float num10 = (float)(j / grabW) + 0.5f - num3;
					if (Mathf.Abs(num4 * num10 - num5 * num9) / num6 > num7)
					{
						continue;
					}
				}
				if (flag)
				{
					int num11 = j % grabW;
					int num12 = j / grabW;
					int num13 = 0;
					if (num11 > 0 && array[j - 1])
					{
						num13++;
					}
					if (num11 < grabW - 1 && array[j + 1])
					{
						num13++;
					}
					if (num12 > 0 && array[j - grabW])
					{
						num13++;
					}
					if (num12 < grabH - 1 && array[j + grabW])
					{
						num13++;
					}
					if (num13 < 2)
					{
						continue;
					}
				}
				num8++;
			}
			return num8;
		}

		private static bool Changed(Color32[] a, Color32[] b, int i)
		{
			return Math.Max(Math.Abs(a[i].r - b[i].r), Math.Max(Math.Abs(a[i].g - b[i].g), Math.Abs(a[i].b - b[i].b))) > 16;
		}

		private bool SunShows(Color32[] px, float sx, float sy, out string how)
		{
			how = "its disc not in the picture";
			int grabW = _grabW;
			int grabH = _grabH;
			if (sx < 0f || grabW <= 0 || grabH <= 0 || grabW * grabH != px.Length)
			{
				return false;
			}
			float num = sx * (float)grabW;
			float num2 = sy * (float)grabH;
			float num3 = Mathf.Max(2f, (float)grabH * 0.012f);
			float num4 = (float)grabH * 0.3f;
			float num5 = (float)grabH * 0.4f;
			int num6 = 0;
			int num7 = 0;
			double num8 = 0.0;
			int num9 = Mathf.Max(0, (int)(num - num5));
			int num10 = Mathf.Min(grabW - 1, (int)(num + num5));
			int num11 = Mathf.Max(0, (int)(num2 - num5));
			int num12 = Mathf.Min(grabH - 1, (int)(num2 + num5));
			for (int i = num11; i <= num12; i++)
			{
				for (int j = num9; j <= num10; j++)
				{
					float num13 = (float)j + 0.5f - num;
					float num14 = (float)i + 0.5f - num2;
					float num15 = Mathf.Sqrt(num13 * num13 + num14 * num14);
					int num16 = Luma(px[i * grabW + j]);
					if (num15 <= num3)
					{
						num6 = Math.Max(num6, num16);
					}
					else if (num15 >= num4 && num15 <= num5)
					{
						num8 += (double)num16;
						num7++;
					}
				}
			}
			float num17 = ((num7 > 0) ? ((float)(num8 / (double)num7)) : 0f);
			how = $"its disc's brightest {num6}, the sky round it {num17:F0}";
			if (num6 >= 150)
			{
				return (float)num6 - num17 >= 30f;
			}
			return false;
		}

		private static Vector2 Turn(Vector2 v, float deg)
		{
			float f = deg * (MathF.PI / 180f);
			float num = Mathf.Cos(f);
			float num2 = Mathf.Sin(f);
			return new Vector2(v.x * num - v.y * num2, v.x * num2 + v.y * num);
		}

		private static float Compass(Vec3 d)
		{
			return (Mathf.Atan2(d.X, d.Y) * 57.29578f + 360f) % 360f;
		}

		private static float Up(Vec3 d)
		{
			return Mathf.Asin(Mathf.Clamp(d.Z, -1f, 1f)) * 57.29578f;
		}

		private void Check(string what, bool ok, string detail = null)
		{
			if (ok)
			{
				_pass++;
			}
			else
			{
				_fail++;
			}
			string message = "SUNPATH " + (ok ? "PASS" : "FAIL") + " " + what + ((detail != null) ? (" (" + detail + ")") : "");
			if (ok)
			{
				Log.Info(message);
			}
			else
			{
				Log.Error(message);
			}
		}

		private static void Finish(bool quit)
		{
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("SUNPATH DONE");
			if (quit)
			{
				Application.Quit();
			}
		}
	}
}
