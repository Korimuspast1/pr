using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using FirstPerson.Core;
using FirstPerson.Gpu;
using FirstPerson.Towns;
using UnityEngine;
using UnityEngine.Profiling;
using ai.behaviours;

namespace FirstPerson
{
	internal sealed class RegaliaTest : CivilTest
	{
		private struct PhaseStats
		{
			public double Median;

			public double P90;

			public double RegaliaMs;

			public double MeshMs;

			public int Models;

			public int Copies;

			public int Draws;

			public int Instances;

			public int Triangles;

			public int Gen0;

			public long HeapSlope;

			public float Reach;

			public float Detail;
		}

		private sealed class Realm
		{
			public string Name;

			public Kingdom K;

			public City Town;

			public Actor Founder;

			public readonly List<Actor> People = new List<Actor>();

			public Vector2 Centre;
		}

		private struct Why
		{
			public bool Ok;

			public bool[] Nb;

			public int[] Used;

			public float[] Far;

			public ulong[] Mix;

			public int Pick;

			public bool FarTie;

			public int Rule()
			{
				int num = 0;
				if (Better(1, 0))
				{
					num = 1;
				}
				if ((num == 0) ? Better(2, 0) : Better(2, num))
				{
					num = 2;
				}
				return num;
			}

			private bool Better(int a, int b)
			{
				if (Nb[a] != Nb[b])
				{
					return !Nb[a];
				}
				if (Used[a] != Used[b])
				{
					return Used[a] < Used[b];
				}
				if (Far[a] != Far[b])
				{
					if (Mathf.Abs(Far[a] - Far[b]) < 0.11f && !float.IsInfinity(Far[a]))
					{
						FarTie = true;
					}
					return Far[a] > Far[b];
				}
				return Mix[a] < Mix[b];
			}
		}

		private const int PhaseFrames = 150;

		private static readonly Regex StatsRx = new Regex("models (\\d+) \\((\\d+) copies, (\\d+) draws\\)");

		private Realm _k1;

		private Realm _k2;

		private Realm _k3;

		private Realm _k4;

		private Realm _orc;

		private Realm _cat;

		private Actor _photographer;

		private Vector2 _stage;

		private bool _realmsReady;

		private Actor _leader;

		private Actor _heir;

		private Actor _captain;

		private Actor _champion;

		private Actor _guard;

		private Actor _coif;

		private Actor _coifChest;

		private Actor _queen;

		private readonly Actor[] _seats = new Actor[5];

		private readonly List<Actor> _helmeted = new List<Actor>();

		private readonly string[] _helmetOf = new string[5] { "helmet_leather", "helmet_bronze", "helmet_iron", "helmet_mythril", "helmet_adamantine" };

		private bool _rolesReady;

		private static readonly string[] RealmParts = new string[15]
		{
			"roles", "helms", "hair", "titles", "range", "sway", "corpse", "figure", "ownbody", "room",
			"aim", "styles", "council", "save", "reuse"
		};

		private readonly Dictionary<Actor, Vector2> _held = new Dictionary<Actor, Vector2>();

		private readonly List<Actor> _heldDead = new List<Actor>();

		private int _holdFrame;

		private readonly HashSet<Actor> _facing = new HashSet<Actor>();

		private readonly HashSet<Actor> _soldiers = new HashSet<Actor>();

		private readonly List<Actor> _soldiersGone = new List<Actor>();

		private readonly HashSet<Actor> _captains = new HashSet<Actor>();

		private readonly Queue<Event> _events = new Queue<Event>();

		private readonly List<Actor> _spare = new List<Actor>();

		private Actor _plain;

		private Actor _child;

		private Actor _k2Soldier;

		private static readonly string[] HelmetStates = new string[9] { null, "helmet_leather", "helmet_copper", "helmet_bronze", "helmet_silver", "helmet_iron", "helmet_steel", "helmet_mythril", "helmet_adamantine" };

		private readonly SpriteCache _heads = new SpriteCache();

		private static readonly uint[] CrownGolds = new uint[3]
		{
			TerrainCache.Rgb(239, 199, 0),
			TerrainCache.Rgb(211, 176, 0),
			TerrainCache.Rgb(255, 184, 0)
		};

		private static readonly float[] GateDistances = new float[5] { 8f, 20f, 45f, 90f, 150f };

		private static readonly float[] PhotoDistances = new float[4] { 12f, 30f, 60f, 120f };

		protected override string Prefix => "RG";

		private IEnumerator Measure(bool on, PhaseStats[] into, int slot)
		{
			People.RegaliaOn = on;
			yield return Frames(20);
			List<double> ms = new List<double>(150);
			double rg = 0.0;
			double mesh = 0.0;
			int inst = 0;
			int tris = 0;
			int gc0 = GC.CollectionCount(0);
			long heap0 = Profiler.GetMonoUsedSizeLong();
			GpuRenderer gpu = (Mod.Settings.GpuRender ? Mod.GpuRendererForTest : null);
			gpu?.TakeTimes();
			for (int f = 0; f < 150; f++)
			{
				yield return null;
				ms.Add(Mod.FrameMs);
				rg += People.RegaliaMs;
				inst += People.RegaliaInstances;
				tris += People.RegaliaTriangles;
				mesh += (double)Mod.Renderer.MeshMs;
			}
			PhaseStats phaseStats = default(PhaseStats);
			ms.Sort();
			phaseStats.Median = ms[ms.Count / 2];
			phaseStats.P90 = ms[(int)((float)ms.Count * 0.9f)];
			phaseStats.RegaliaMs = rg / 150.0;
			phaseStats.MeshMs = mesh / 150.0;
			phaseStats.Instances = inst / 150;
			phaseStats.Triangles = tris / 150;
			phaseStats.Gen0 = GC.CollectionCount(0) - gc0;
			phaseStats.HeapSlope = (Profiler.GetMonoUsedSizeLong() - heap0) / 150;
			phaseStats.Reach = Mod.SceneForTest.CreatureRange;
			phaseStats.Detail = Mod.SceneForTest.Detail;
			if (gpu != null)
			{
				Match match = StatsRx.Match(gpu.Stats ?? "");
				if (match.Success)
				{
					phaseStats.Models = int.Parse(match.Groups[1].Value);
					phaseStats.Copies = int.Parse(match.Groups[2].Value);
					phaseStats.Draws = int.Parse(match.Groups[3].Value);
				}
			}
			into[slot] = phaseStats;
		}

		private IEnumerator AB(string what, PhaseStats[] st, List<string> rows)
		{
			for (int i = 0; i < 4; i++)
			{
				yield return Measure(i % 2 == 1, st, i);
			}
			People.RegaliaOn = true;
			double num = (st[0].Median + st[2].Median) * 0.5;
			double num2 = (st[1].Median + st[3].Median) * 0.5;
			bool flag = st[0].Reach == st[1].Reach && st[1].Reach == st[2].Reach && st[2].Reach == st[3].Reach && st[0].Detail == st[1].Detail && st[1].Detail == st[3].Detail;
			string text = $"{what}: frame median off {num:F2} on {num2:F2} (+{num2 - num:F2} ms), p90 off {(st[0].P90 + st[2].P90) * 0.5:F2} on {(st[1].P90 + st[3].P90) * 0.5:F2}; " + $"models {st[0].Models}/{st[1].Models}, copies {st[0].Copies}/{st[1].Copies}, draws {st[0].Draws}/{st[1].Draws}; " + $"regalia {st[1].Instances} instances, {st[1].Triangles} tris, {st[1].RegaliaMs:F2} ms; mesh ms {(st[0].MeshMs + st[2].MeshMs) * 0.5:F2}/{(st[1].MeshMs + st[3].MeshMs) * 0.5:F2}; " + string.Format("gen0 {0}/{1}, heap B/frame {2}/{3}; reach {4:F0} detail {5:F2}{6}", st[0].Gen0 + st[2].Gen0, st[1].Gen0 + st[3].Gen0, st[0].HeapSlope, st[1].HeapSlope, st[0].Reach, st[0].Detail, flag ? "" : " (MOVED: pair rejected)");
			Log.Info("RG perf " + text);
			rows.Add(text);
		}

		private IEnumerator ArmyPart()
		{
			Settings s = Mod.Settings;
			bool cpu = Options.Contains("cpu");
			if (cpu)
			{
				s.GpuRender = false;
				s.Resolution = 1;
			}
			int[] sizes = ((!cpu) ? ((!Options.Contains("army200")) ? new int[2] { 60, 120 } : new int[3] { 60, 120, 200 }) : new int[1] { 60 });
			Realm a = Found("A", "human", 6, 0, null, 0f);
			Realm b = ((a != null) ? Found("B", "human", 6, 0, a.Founder.current_tile, 30f) : null);
			if (!Check("two human realms for the armies", a != null && b != null))
			{
				yield break;
			}
			Actor viewer = Newcomer(a, female: false, a.Centre);
			yield return Possess(viewer);
			if (!Check("in control of someone", InControl(viewer)))
			{
				yield break;
			}
			Vector2 vector = StageNear(a.Centre);
			Vector2 fwd = ClearHeading(vector, 125f);
			Vector2 right = new Vector2(fwd.y, 0f - fwd.x);
			Vector2 eye = vector - fwd * 2f;
			ClearCorridor(eye, fwd, 125f, 9);
			Hold(viewer, eye - fwd * 3f);
			Mod.FixDetailForTest(on: true);
			Note(string.Format("renderer {0}, view {1} wide, the line {2}, creature reach {3:F0}", s.GpuRender ? "GPU" : "CPU", WorldBoxMod.ResolutionWidths[s.Resolution], fwd, Mod.SceneForTest.CreatureRange));
			List<Actor> armyA = new List<Actor>();
			List<Actor> armyB = new List<Actor>();
			List<string> rows = new List<string>();
			Dictionary<string, int> drawsDelta = new Dictionary<string, int>();
			Dictionary<int, double> msDelta = new Dictionary<int, double>();
			int captains = 0;
			float[][] bands = new float[5][]
			{
				new float[2] { 8f, 20f },
				new float[2] { 25f, 45f },
				new float[2] { 45f, 60f },
				new float[2] { 60f, 120f },
				new float[2] { 10f, 120f }
			};
			int[] array = sizes;
			for (int i = 0; i < array.Length; i++)
			{
				int n = array[i];
				while (armyA.Count < n)
				{
					Actor actor = Soldier(a, eye, armyA.Count, ref captains);
					if (actor == null)
					{
						break;
					}
					armyA.Add(actor);
				}
				while (armyB.Count < n)
				{
					Actor actor2 = Soldier(b, eye, armyB.Count, ref captains);
					if (actor2 == null)
					{
						break;
					}
					armyB.Add(actor2);
				}
				int count = armyA.FindAll((Actor x) => x.isArmyGroupLeader()).Count;
				int count2 = armyB.FindAll((Actor x) => x.isArmyGroupLeader()).Count;
				Note($"{n} a realm: {armyA.Count} + {armyB.Count} soldiers, captains {count} + {count2}");
				double worst = double.MinValue;
				float[][] array2 = bands;
				foreach (float[] band in array2)
				{
					Place(armyA, eye, fwd, right, band[0], band[1], -1f);
					Place(armyB, eye, fwd, right, band[0], band[1], 1f);
					Look(eye, eye + fwd * ((band[0] + band[1]) * 0.5f), (band[1] > 60f) ? (-1f) : (-3f), 2f);
					Config.paused = false;
					yield return CivilTest.Wait(1.6f);
					Config.paused = true;
					PhaseStats[] st = new PhaseStats[4];
					string what = string.Format("{0}{1} a realm, {2:F0}-{3:F0} tiles{4}", cpu ? "CPU " : "", n, band[0], band[1], (band[0] == 10f) ? " (mixed)" : "");
					yield return AB(what, st, rows);
					Frame($"rg_army_{n}_{band[0]:F0}_{band[1]:F0}.png");
					drawsDelta[n + "_" + band[0]] = st[1].Draws - st[0].Draws;
					worst = Math.Max(worst, (st[1].Median + st[3].Median - st[0].Median - st[2].Median) * 0.5);
					if (cpu)
					{
						msDelta[n * 1000 + (int)band[0]] = (st[1].MeshMs + st[3].MeshMs - st[0].MeshMs - st[2].MeshMs) * 0.5;
					}
				}
				msDelta[n] = worst;
				Place(armyA, eye, fwd, right, 25f, 45f, -1f);
				Place(armyB, eye, fwd, right, 25f, 45f, 1f);
				foreach (Actor item in armyA)
				{
					Release(item);
				}
				foreach (Actor item2 in armyB)
				{
					Release(item2);
				}
				Royal.RegisterTask();
				WorldTile goal = LandNear(eye + fwd * 100f);
				foreach (Actor item3 in armyA)
				{
					March(item3, goal);
				}
				foreach (Actor item4 in armyB)
				{
					March(item4, goal);
				}
				Config.paused = false;
				Look(eye, eye + fwd * 40f, -3f, 2f);
				yield return CivilTest.Wait(1f);
				PhaseStats[] st2 = new PhaseStats[4];
				yield return AB(string.Format("{0}{1} a realm, marching from 25-45 tiles", cpu ? "CPU " : "", n), st2, rows);
				Frame($"rg_army_{n}_march.png");
				foreach (Actor item5 in armyA)
				{
					try
					{
						item5.cancelAllBeh();
					}
					catch
					{
					}
				}
				foreach (Actor item6 in armyB)
				{
					try
					{
						item6.cancelAllBeh();
					}
					catch
					{
					}
				}
			}
			Config.paused = false;
			Mod.FixDetailForTest(on: false);
			double value5;
			if (!cpu)
			{
				if (msDelta.TryGetValue(60, out var value))
				{
					Check("perf: at 60 a realm the worst band costs at most +1.2 ms", value <= 1.2, $"+{value:F2} ms");
				}
				if (msDelta.TryGetValue(200, out var value2))
				{
					Check("perf: at 200 a realm the worst band costs at most +3.0 ms", value2 <= 3.0, $"+{value2:F2} ms");
				}
				int num2 = int.MinValue;
				foreach (KeyValuePair<string, int> item7 in drawsDelta)
				{
					num2 = Math.Max(num2, item7.Value);
				}
				Check("perf: regalia adds at most 30 draws", num2 <= 30, "worst +" + num2);
				int num3 = sizes[sizes.Length - 1];
				string text = "";
				bool ok = true;
				float[][] array3 = bands;
				foreach (float[] array4 in array3)
				{
					if (drawsDelta.TryGetValue("60_" + array4[0], out var value3) && drawsDelta.TryGetValue(num3 + "_" + array4[0], out var value4))
					{
						text += $"{array4[0]:F0}: +{value3} at 60, +{value4} at {num3}; ";
						if (value4 - value3 > 4)
						{
							ok = false;
						}
					}
				}
				Check("perf: the draws regalia adds don't grow with the crowd", ok, text);
			}
			else if (msDelta.TryGetValue(60008, out value5))
			{
				Check("perf (CPU): models at most +3 ms at 60 a realm, 8-20 tiles", value5 <= 3.0, $"+{value5:F2} ms");
			}
			Check("perf: no regalia set skipped", People.RegaliaSkipped == 0, People.RegaliaSkipped.ToString());
			foreach (Actor item8 in armyA)
			{
				Release(item8);
				Die(item8);
			}
			foreach (Actor item9 in armyB)
			{
				Release(item9);
				Die(item9);
			}
		}

		private Actor Soldier(Realm r, Vector2 at, int index, ref int captains)
		{
			Actor actor = Recruit(r, at, (index % 2 == 0) ? "helmet_iron" : null);
			if (actor == null)
			{
				return null;
			}
			CivilTest.SetSex(actor, index % 3 == 2);
			if (index % 10 == 5)
			{
				try
				{
					Army army = World.world.armies.newArmy(actor, r.Town);
					if (army != null)
					{
						if (actor.army != army)
						{
							actor.setArmy(army);
						}
						if (army.getCaptain() != actor)
						{
							army.setCaptain(actor);
						}
						if (actor.is_army_captain)
						{
							captains++;
						}
					}
				}
				catch (Exception ex)
				{
					Note("army: " + ex.Message);
				}
			}
			return actor;
		}

		private void Place(List<Actor> army, Vector2 eye, Vector2 fwd, Vector2 right, float near, float far, float side)
		{
			int count = army.Count;
			int num = 6;
			for (int i = 0; i < count; i++)
			{
				float num2 = near + (far - near) * ((float)(i / num) + 0.5f) / (float)Mathf.Max(1, (count + num - 1) / num);
				float num3 = side * (2.5f + (float)(i % num) * 1.1f);
				WorldTile worldTile = LandNear(eye + fwd * num2 + right * num3);
				Hold(army[i], (worldTile != null) ? new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f) : (eye + fwd * num2 + right * num3));
			}
		}

		private static void March(Actor a, WorldTile goal)
		{
			try
			{
				a.cancelAllBeh();
				a.setTask("fp_royal_march");
				a.beh_tile_target = goal;
			}
			catch
			{
			}
		}

		private IEnumerator BigPart()
		{
			int slot = 29;
			int num = Options.IndexOf("map", StringComparison.Ordinal);
			if (num >= 0)
			{
				int num2 = 0;
				int num3 = num + 3;
				while (num3 < Options.Length && char.IsDigit(Options[num3]))
				{
					num2 = num2 * 10 + (Options[num3++] - 48);
				}
				if (num2 > 0)
				{
					slot = num2;
				}
			}
			string text = Path.Combine(Path.Combine(Path.Combine(Application.persistentDataPath, "saves"), "save" + slot), "map.wbox");
			if (!File.Exists(text))
			{
				Note("no save " + slot + " (" + text + "): the big world not checked");
				yield break;
			}
			Config.autosaves = false;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Regalia.ResetForTest();
			try
			{
				SaveManager.loadMapFromBytes(File.ReadAllBytes(text));
			}
			catch (Exception ex)
			{
				Check("the save loaded", ok: false, ex.Message);
				yield break;
			}
			int loadingFrames = 0;
			int errors = 0;
			string firstError = null;
			for (float t = 0f; t < 120f; t += Time.unscaledDeltaTime)
			{
				if (WorldBoxMod.WorldLoading)
				{
					loadingFrames++;
				}
				try
				{
					foreach (Kingdom item in World.world.kingdoms.list)
					{
						Regalia.LookOf(item);
					}
					int num4 = 0;
					foreach (Actor unit in World.world.units)
					{
						if (++num4 <= 800)
						{
							Regalia.RoleOf(unit);
							Regalia.GearOf(unit, handsBusy: false);
							continue;
						}
						break;
					}
				}
				catch (Exception ex2)
				{
					errors++;
					if (firstError == null)
					{
						firstError = ex2.Message;
					}
				}
				yield return null;
				bool flag = true;
				try
				{
					flag = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag && Config.game_loaded && t > 3f)
				{
					break;
				}
			}
			Config.autosaves = false;
			Note($"save {slot} loaded: {MapBox.width}x{MapBox.height}, {World.world.units.Count} units, {World.world.kingdoms.list.Count} realms, {loadingFrames} loading frames");
			Check("big world: asked about mid-load", Regalia.QueriedWhileLoadingForTest > 0 || loadingFrames == 0, $"{Regalia.QueriedWhileLoadingForTest} asks");
			Check("big world: no style given out mid-load", Regalia.AssignedWhileLoadingForTest == 0, Regalia.AssignedWhileLoadingForTest.ToString());
			Check("big world: no exceptions from the asks", errors == 0, errors + " " + firstError);
			int civ = 0;
			int num5 = 0;
			foreach (Kingdom item2 in World.world.kingdoms.list)
			{
				try
				{
					if (item2.isAlive() && item2.isCiv())
					{
						civ++;
						if (Stored(item2) != KingdomStyle.None)
						{
							num5++;
						}
					}
				}
				catch
				{
				}
			}
			Note($"{civ} living civilised realms, {num5} with a style before the settle (an old save has none)");
			for (float t = 0f; t < 8f; t += 0.5f)
			{
				yield return CivilTest.Wait(0.5f);
				foreach (Kingdom item3 in World.world.kingdoms.list)
				{
					Regalia.LookOf(item3);
				}
			}
			int num6 = 0;
			int num7 = 0;
			int num8 = 0;
			foreach (Kingdom item4 in World.world.kingdoms.list)
			{
				try
				{
					if (!item4.isAlive() || !item4.isCiv())
					{
						continue;
					}
					if (Stored(item4) != KingdomStyle.None)
					{
						num6++;
					}
					if (Regalia.Styled(item4))
					{
						num7++;
						if (Stored(item4) != KingdomStyle.None)
						{
							num8++;
						}
					}
				}
				catch
				{
				}
			}
			Check("big world: every living realm has a style once settled", num6 == civ, $"{num6}/{civ} ({num8}/{num7} human-founded)");
			Kingdom best = null;
			int num9 = 0;
			foreach (Kingdom item5 in World.world.kingdoms.list)
			{
				try
				{
					if (item5.isAlive() && Regalia.Styled(item5) && item5.units.Count > num9)
					{
						num9 = item5.units.Count;
						best = item5;
					}
				}
				catch
				{
				}
			}
			if (best == null)
			{
				Note("no human realm in the save: no views measured");
				yield break;
			}
			Actor viewer = null;
			foreach (Actor unit2 in best.units)
			{
				if (unit2 != null && unit2.isAlive() && unit2.isAdult() && !unit2.isKing())
				{
					viewer = unit2;
					break;
				}
			}
			yield return Possess(viewer);
			if (Check("big world: in control of one of " + best.name, InControl(viewer)))
			{
				Hold(viewer, viewer.current_position);
				Mod.FixDetailForTest(on: true);
				List<string> rows = new List<string>();
				List<double> deltas = new List<double>();
				int fills = Regalia.HeavyFillsForTest;
				float start = Time.unscaledTime;
				for (int v = 0; v < 5; v++)
				{
					LookAway();
					Mod.SetYaw((float)v * MathF.PI * 0.4f);
					Mod.SetPitchDegrees(-4f);
					Config.paused = true;
					yield return Frames(30);
					PhaseStats[] st = new PhaseStats[4];
					yield return AB($"save {slot}, view {v + 1} of {best.name}", st, rows);
					double num10 = (st[0].Median + st[2].Median) * 0.5;
					double num11 = (st[1].Median + st[3].Median) * 0.5;
					deltas.Add((num10 > 0.0) ? ((num11 - num10) / num10) : 0.0);
					Frame($"rg_big_view{v + 1}.png");
				}
				Config.paused = false;
				Mod.FixDetailForTest(on: false);
				deltas.Sort();
				double num12 = deltas[deltas.Count / 2];
				float num13 = Mathf.Max(1f, Time.unscaledTime - start);
				Note($"big world: {Regalia.PeopleTablesForTest} realm tables, {(float)(Regalia.HeavyFillsForTest - fills) / num13:F1} table fills a second");
				Check("big world: regalia costs at most +10% of the frame (median of five views)", num12 <= 0.1, string.Format("{0:F1}% ({1})", num12 * 100.0, string.Join(", ", deltas.ConvertAll((double d) => (d * 100.0).ToString("F1") + "%").ToArray())));
				Note("god view at zooms 34/70/111: not measured here");
				Release(viewer);
			}
		}

		protected override IEnumerator Body()
		{
			Settings s = Mod.Settings;
			int timeMode = s.TimeMode;
			int resolution = s.Resolution;
			bool gpu = s.GpuRender;
			s.TimeMode = 1;
			s.Resolution = WorldBoxMod.ResolutionWidths.Length - 1;
			People.RegaliaSkipped = 0;
			bool lounging = Lounging.OffForTest;
			Lounging.OffForTest = true;
			Note(string.Format("renderer {0}, palette {1}, resolution {2}, async {3}", s.GpuRender ? "GPU" : "CPU", s.Palette, WorldBoxMod.ResolutionWidths[s.Resolution], Options.Contains("async")));
			if (Want("army"))
			{
				yield return Part("army", Flat(ArmyPart()));
			}
			if (Want("big"))
			{
				yield return Part("big", Flat(BigPart()));
			}
			bool flag = false;
			string[] realmParts = RealmParts;
			foreach (string part in realmParts)
			{
				flag |= Want(part);
			}
			if (flag)
			{
				yield return Part("setup", Flat(SetupPart()));
				if (!_realmsReady)
				{
					Check("the test world was set up", ok: false);
				}
				else
				{
					if (Want("roles"))
					{
						yield return Part("roles", Flat(RolesPart()));
					}
					if (Want("helms"))
					{
						yield return Part("helms", Flat(HelmsPart()));
					}
					if (Want("hair"))
					{
						yield return Part("hair", Flat(HairPart()));
					}
					if (Want("titles"))
					{
						yield return Part("titles", Flat(TitlesPart()));
					}
					if (Want("range"))
					{
						yield return Part("range", Flat(RangePart()));
					}
					if (Want("sway"))
					{
						yield return Part("sway", Flat(SwayPart()));
					}
					if (Want("corpse"))
					{
						yield return Part("corpse", Flat(CorpsePart()));
					}
					if (Want("figure"))
					{
						yield return Part("figure", Flat(FigurePart()));
					}
					if (Want("ownbody"))
					{
						yield return Part("ownbody", Flat(OwnBodyPart()));
					}
					if (Want("room"))
					{
						yield return Part("room", Flat(RoomPart()));
					}
					if (Want("aim"))
					{
						yield return Part("aim", Flat(AimPart()));
					}
					if (Want("styles"))
					{
						yield return Part("styles", Flat(StylesPart()));
					}
					if (Want("council"))
					{
						yield return Part("council", Flat(CouncilPart()));
					}
					if (Want("reuse"))
					{
						yield return Part("reuse", Flat(ReusePart()));
					}
					if (Want("save"))
					{
						yield return Part("save", Flat(SavePart()));
					}
					Check("no regalia set left out for a missing or unbound model", People.RegaliaSkipped == 0, People.RegaliaSkipped + " skipped");
				}
			}
			People.RegaliaOn = true;
			People.GuardHalberds = true;
			Mod.FixDetailForTest(on: false);
			_held.Clear();
			foreach (Actor item in _facing)
			{
				Mod.SceneForTest.LookAt.Remove(item);
			}
			_facing.Clear();
			_soldiers.Clear();
			_captains.Clear();
			Lounging.OffForTest = lounging;
			s.TimeMode = timeMode;
			s.Resolution = resolution;
			s.GpuRender = gpu;
		}

		private bool Want(string part)
		{
			if (Options.Contains("only" + part))
			{
				return true;
			}
			if (part == "army" || part == "big")
			{
				return false;
			}
			return !Options.Contains("only");
		}

		private IEnumerator SetupPart()
		{
			_k1 = Found("K1", "human", 24, 2, null, 0f);
			if (_k1 == null)
			{
				yield break;
			}
			_k2 = Found("K2", "human", 16, 2, _k1.Founder.current_tile, 30f);
			_k3 = Found("K3", "human", 16, 2, (_k2 ?? _k1).Founder.current_tile, 30f);
			_k4 = Found("K4", "human", 12, 0, _k1.Founder.current_tile, 16f);
			_orc = Found("orc", "orc", 8, 0, null, 0f);
			_cat = Found("cat", "civ_cat", 6, 0, null, 0f);
			if (!Check("four human realms founded", _k2 != null && _k3 != null && _k4 != null, $"K2 {_k2 != null}, K3 {_k3 != null}, K4 {_k4 != null}"))
			{
				yield break;
			}
			if (_orc == null)
			{
				Note("no orc realm could be founded");
			}
			if (_cat == null)
			{
				Note("no cat-folk realm could be founded (not needed by the RG parts)");
			}
			for (int i = 0; i < 9; i++)
			{
				Join(_k1, CivilTest.SpawnNear("human", _k1.Centre, 3f));
			}
			for (int j = 0; j < 4; j++)
			{
				Join(_k3, CivilTest.SpawnNear("human", _k3.Centre, 3f));
			}
			foreach (Realm item in AllRealms())
			{
				foreach (Actor person in item.People)
				{
					Invincible(person);
				}
			}
			_photographer = null;
			foreach (Actor person2 in _k1.People)
			{
				if (person2 != _k1.Founder && person2.isAlive() && person2.isAdult() && !person2.isWarrior())
				{
					_photographer = person2;
					break;
				}
			}
			if (!Check("a K1 commoner to look through", _photographer != null))
			{
				yield break;
			}
			CivilTest.SetSex(_photographer, female: false);
			_stage = StageNear(_k1.Centre);
			Note($"the stage: {_stage.x:F0},{_stage.y:F0} ({(_stage - _k1.Centre).magnitude:F0} tiles from K1's middle)");
			yield return Possess(_photographer);
			if (!Check("in control of the photographer", InControl(_photographer)))
			{
				yield break;
			}
			Hold(_photographer, _stage + new Vector2(0f, -9f));
			foreach (Realm item2 in AllRealms())
			{
				Regalia.LookOf(item2.K);
				foreach (Actor person3 in item2.People)
				{
					Regalia.RoleOf(person3);
				}
			}
			for (float t = 0f; t < 15f; t += 0.5f)
			{
				if (AllStyled())
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
			}
			Check("every realm has a style after the settle", AllStyled(), StylesLine());
			foreach (Realm item3 in AllRealms())
			{
				ColorAsset colorAsset = null;
				try
				{
					colorAsset = item3.K.getColor();
				}
				catch
				{
				}
				Note($"{item3.Name} = {item3.K.name} #{item3.K.data.id} ({item3.People.Count} people, town {item3.Town?.name}): style {Regalia.LookOf(item3.K).Style}, styled {Regalia.Styled(item3.K)}, " + $"colour {colorAsset?.id}, banner {item3.K.data.banner_background_id}/{item3.K.data.banner_icon_id}, why {Regalia.WhyForTest(item3.K)}");
			}
			_realmsReady = true;
		}

		private IEnumerable<Realm> AllRealms()
		{
			Realm[] array = new Realm[6] { _k1, _k2, _k3, _k4, _orc, _cat };
			foreach (Realm realm in array)
			{
				if (realm != null)
				{
					yield return realm;
				}
			}
		}

		private bool AllStyled()
		{
			foreach (Realm item in AllRealms())
			{
				if (Regalia.LookOf(item.K).Style == KingdomStyle.None)
				{
					return false;
				}
			}
			return true;
		}

		private string StylesLine()
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (Realm item in AllRealms())
			{
				stringBuilder.Append(item.Name).Append(' ').Append(Regalia.LookOf(item.K).Style)
					.Append("  ");
			}
			return stringBuilder.ToString();
		}

		private Realm Found(string name, string species, int adults, int children, WorldTile near, float minAway)
		{
			Actor actor = Village(species, adults, children, near, minAway);
			if (actor == null && near != null)
			{
				actor = Village(species, adults, children);
			}
			if (actor == null)
			{
				Note(name + ": no " + species + " village could be founded");
				return null;
			}
			Realm realm = new Realm
			{
				Name = name,
				K = actor.kingdom,
				Town = actor.city,
				Founder = actor
			};
			realm.People.AddRange(Founded);
			try
			{
				Vector2 centre;
				if (realm.Town == null)
				{
					centre = actor.current_position;
				}
				else
				{
					WorldTile tile = realm.Town.getTile();
					centre = ((tile != null) ? new Vector2((float)tile.x + 0.5f, (float)tile.y + 0.5f) : actor.current_position);
				}
				realm.Centre = centre;
			}
			catch
			{
				realm.Centre = actor.current_position;
			}
			return realm;
		}

		private void Join(Realm r, Actor a)
		{
			if (r == null || a == null)
			{
				return;
			}
			try
			{
				if (a.city != r.Town)
				{
					a.joinCity(r.Town);
				}
			}
			catch (Exception ex)
			{
				Note("join: " + ex.Message);
			}
			r.People.Add(a);
		}

		private static void Invincible(Actor a)
		{
			try
			{
				if (a != null && a.isAlive())
				{
					((BaseSimObject)a).addStatusEffect("invincible", 1800f, pColorEffect: false);
				}
			}
			catch
			{
			}
		}

		private static Vector2 StageNear(Vector2 at)
		{
			Vector2 vector = at;
			float num = float.MaxValue;
			for (int i = 0; i < 24; i++)
			{
				for (float num2 = 8f; num2 <= 30f; num2 += 2f)
				{
					float f = (float)i * MathF.PI / 12f;
					Vector2 vector2 = at + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num2;
					int num3 = int.MaxValue;
					int num4 = int.MinValue;
					int num5 = 0;
					for (int j = (int)vector2.y - 10; j <= (int)vector2.y + 5; j++)
					{
						for (int k = (int)vector2.x - 7; k <= (int)vector2.x + 7; k++)
						{
							WorldTile tile = World.world.GetTile(k, j);
							if (tile == null || tile.main_type == null)
							{
								num5 += 5;
								continue;
							}
							if (tile.main_type.liquid || tile.main_type.block)
							{
								num5 += 3;
							}
							if (tile.building != null && AssetStyle.IsSolid(tile.building) && tile.building.asset != null && tile.building.asset.building_type != BuildingType.Building_Tree && tile.building.asset.building_type != BuildingType.Building_Mineral && tile.building.asset.building_type != BuildingType.Building_Plant)
							{
								num5 += 4;
							}
							num3 = Mathf.Min(num3, tile.main_type.height_min);
							num4 = Mathf.Max(num4, tile.main_type.height_min);
						}
					}
					float num6 = (float)num5 * 10f + (float)(num4 - num3) + num2 * 0.2f;
					if (num6 < num)
					{
						num = num6;
						vector = vector2;
					}
				}
			}
			CivilTest.ClearBox((int)vector.x - 8, (int)vector.y - 12, (int)vector.x + 8, (int)vector.y + 6);
			return new Vector2((float)(int)vector.x + 0.5f, (float)(int)vector.y + 0.5f);
		}

		private void Hold(Actor a, Vector2 at)
		{
			if (a == null)
			{
				return;
			}
			_held[a] = at;
			Out(a);
			try
			{
				a.current_position = at;
				a.cancelAllBeh();
				a.makeWait(8f);
			}
			catch
			{
			}
		}

		private void Release(Actor a)
		{
			if (a != null)
			{
				_held.Remove(a);
			}
			Unface(a);
		}

		private void Face(Actor a, Vector2 at)
		{
			if (a != null)
			{
				Mod.SceneForTest.LookAt[a] = at;
				_facing.Add(a);
			}
		}

		private void Unface(Actor a)
		{
			if (a != null && _facing.Remove(a))
			{
				Mod.SceneForTest.LookAt.Remove(a);
			}
		}

		private IEnumerator Group(string name, IList<Actor> who, bool backs = false, float distance = 3.2f)
		{
			Vector2 vector = _stage + new Vector2(0f, -6f);
			Vector2 vector2 = vector + new Vector2(0f, 0f - distance);
			Dictionary<Actor, Vector2> keep = new Dictionary<Actor, Vector2>();
			foreach (Actor item in who)
			{
				if (item != null && _held.TryGetValue(item, out var value))
				{
					keep[item] = value;
				}
			}
			Row(who, vector, vector2, 0.85f);
			foreach (Actor item2 in who)
			{
				Face(item2, backs ? (vector + (vector - vector2) * 3f) : vector2);
			}
			Look(vector2, vector, -3f, 1.05f);
			Config.paused = false;
			yield return Shown(10);
			yield return CivilTest.Wait(1f);
			Config.paused = true;
			yield return Frames(3);
			Frame(name);
			Config.paused = false;
			foreach (Actor item3 in who)
			{
				if (item3 != null)
				{
					Unface(item3);
					if (keep.TryGetValue(item3, out var value2))
					{
						Hold(item3, value2);
					}
					else
					{
						Release(item3);
					}
				}
			}
		}

		private void KeepSoldiers()
		{
			if (_soldiers.Count == 0)
			{
				return;
			}
			_soldiersGone.Clear();
			foreach (Actor soldier in _soldiers)
			{
				try
				{
					if (soldier == null || !soldier.isAlive())
					{
						_soldiersGone.Add(soldier);
					}
					else if (!soldier.isWarrior() && soldier.city != null)
					{
						soldier.city.makeWarrior(soldier);
						if (_captains.Contains(soldier))
						{
							MakeCaptain(soldier);
						}
					}
				}
				catch
				{
					_soldiersGone.Add(soldier);
				}
			}
			foreach (Actor item in _soldiersGone)
			{
				_soldiers.Remove(item);
				_captains.Remove(item);
			}
		}

		private void Update()
		{
			KeepSoldiers();
			if (_held.Count == 0)
			{
				return;
			}
			bool flag = ++_holdFrame % 20 == 0;
			_heldDead.Clear();
			foreach (KeyValuePair<Actor, Vector2> item in _held)
			{
				Actor key = item.Key;
				try
				{
					if (key == null || !key.isAlive())
					{
						_heldDead.Add(key);
						continue;
					}
					key.current_position = item.Value;
					if (flag)
					{
						Out(key);
						key.cancelAllBeh();
						key.makeWait(8f);
					}
				}
				catch
				{
					_heldDead.Add(key);
				}
			}
			foreach (Actor item2 in _heldDead)
			{
				_held.Remove(item2);
			}
		}

		private void Row(IList<Actor> who, Vector2 middle, Vector2 eye, float gap)
		{
			Vector2 normalized = (middle - eye).normalized;
			Vector2 vector = new Vector2(normalized.y, 0f - normalized.x);
			for (int i = 0; i < who.Count; i++)
			{
				if (who[i] != null)
				{
					Hold(who[i], middle + vector * (((float)i - (float)(who.Count - 1) * 0.5f) * gap));
				}
			}
		}

		private void Look(Vector2 eye, Vector2 at, float pitch = -6f, float height = 0f)
		{
			Mod.DebugEye = eye;
			Mod.DebugEyeSet = true;
			Mod.DebugEyeHeight = height;
			Mod.SetYaw(Mathf.Atan2(at.y - eye.y, at.x - eye.x));
			Mod.SetPitchDegrees(pitch);
		}

		private void LookAway()
		{
			Mod.DebugEyeSet = false;
			Mod.DebugEyeHeight = 0f;
			Mod.SetPitchDegrees(0f);
		}

		private static IEnumerator Frames(int n)
		{
			for (int i = 0; i < n; i++)
			{
				yield return null;
			}
		}

		private IEnumerator Shown(int n, float most = 8f)
		{
			int start = Mod.FramesShown;
			for (float t = 0f; t < most; t += Time.unscaledDeltaTime)
			{
				if (Mod.FramesShown - start >= n)
				{
					break;
				}
				yield return null;
			}
		}

		private IEnumerator Refresh(IEnumerable<Actor> who, float seconds = 1.6f)
		{
			Config.paused = false;
			foreach (Actor item in who)
			{
				People.RecheckForTest(item);
			}
			yield return Shown(10);
			yield return CivilTest.Wait(seconds);
			foreach (Actor item2 in who)
			{
				People.RecheckForTest(item2);
			}
			yield return Shown(6);
		}

		private static IEnumerator Flat(IEnumerator root)
		{
			Stack<IEnumerator> stack = new Stack<IEnumerator>();
			stack.Push(root);
			while (stack.Count > 0)
			{
				IEnumerator enumerator = stack.Peek();
				if (!enumerator.MoveNext())
				{
					stack.Pop();
				}
				else if (enumerator.Current is IEnumerator item)
				{
					stack.Push(item);
				}
				else
				{
					yield return enumerator.Current;
				}
			}
		}

		private static string D(Actor a)
		{
			try
			{
				return (a == null) ? "nobody" : WorldBoxMod.Describe(a);
			}
			catch
			{
				return "?";
			}
		}

		private static string[] ShownOf(Actor a)
		{
			return People.RegaliaShown(a) ?? new string[0];
		}

		private static string S(Actor a)
		{
			string[] array = People.RegaliaShown(a);
			if (array != null)
			{
				if (array.Length != 0)
				{
					return string.Join(",", array);
				}
				return "(nothing)";
			}
			return "(not drawn: " + Who(a) + ")";
		}

		private static string Who(Actor a)
		{
			if (a == null)
			{
				return "nobody";
			}
			People.LegsForTest(a, out var mesh, out var _, out var age);
			RoleInfo roleInfo = Regalia.RoleOf(a);
			string text = "";
			try
			{
				text = ((!a.isAlive()) ? ", dead" : (a.is_inside_building ? ", inside a building" : ""));
			}
			catch
			{
			}
			return string.Format("{0} {1}{2}, {3} as {4}{5}", D(a), roleInfo.Bits, roleInfo.Wears ? "" : " wears nothing", (age > 100000) ? "never drawn" : ("drawn " + age + " frames ago"), mesh?.Model?.Name, text);
		}

		private static void Out(Actor a)
		{
			try
			{
				if (a != null && a.is_inside_building)
				{
					a.exitBuilding();
				}
			}
			catch
			{
			}
		}

		private static bool Has(string[] list, string piece)
		{
			if (list == null)
			{
				return false;
			}
			for (int i = 0; i < list.Length; i++)
			{
				if (list[i] == piece)
				{
					return true;
				}
			}
			return false;
		}

		private static bool HasAny(string[] list, string prefix)
		{
			if (list == null)
			{
				return false;
			}
			for (int i = 0; i < list.Length; i++)
			{
				if (list[i].StartsWith(prefix, StringComparison.Ordinal))
				{
					return true;
				}
			}
			return false;
		}

		private static bool None(Actor a)
		{
			string[] array = People.RegaliaShown(a);
			if (array != null)
			{
				return array.Length == 0;
			}
			return true;
		}

		private static uint Rgb(uint c)
		{
			return c & 0xFFFFFF;
		}

		private static string Hex(uint c)
		{
			return "#" + (((c & 0xFF) << 16) | (c & 0xFF00) | ((c >> 16) & 0xFF)).ToString("X6");
		}

		private static float DeltaE(uint a, uint b)
		{
			Lab(a, out var l, out var a2, out var b2);
			Lab(b, out var l2, out var a3, out var b3);
			return Mathf.Sqrt((l - l2) * (l - l2) + (a2 - a3) * (a2 - a3) + (b2 - b3) * (b2 - b3));
		}

		private static void Lab(uint c, out float l, out float a, out float b)
		{
			float num = Lin((float)(c & 0xFF) / 255f);
			float num2 = Lin((float)((c >> 8) & 0xFF) / 255f);
			float num3 = Lin((float)((c >> 16) & 0xFF) / 255f);
			float t = (0.4124f * num + 0.3576f * num2 + 0.1805f * num3) / 0.95047f;
			float t2 = 0.2126f * num + 0.7152f * num2 + 0.0722f * num3;
			float t3 = (0.0193f * num + 0.1192f * num2 + 0.9505f * num3) / 1.08883f;
			t = F(t);
			t2 = F(t2);
			t3 = F(t3);
			l = 116f * t2 - 16f;
			a = 500f * (t - t2);
			b = 200f * (t2 - t3);
		}

		private static float Lin(float v)
		{
			if (!(v <= 0.04045f))
			{
				return Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
			}
			return v / 12.92f;
		}

		private static float F(float t)
		{
			if (!(t > 0.008856f))
			{
				return 7.787f * t + 0.13793103f;
			}
			return Mathf.Pow(t, 1f / 3f);
		}

		private static string Letter(Kingdom k)
		{
			return RegaliaKit.StyleLetter(Regalia.LookOf(k).Style) ?? "?";
		}

		private static string HairShowing(Actor a, bool beard)
		{
			People.LegsForTest(a, out var mesh, out var _, out var age);
			if (mesh?.Model == null || age > 3)
			{
				return "(not drawn)";
			}
			List<string> list = new List<string>();
			MeshPart[] parts = mesh.Model.Parts;
			for (int i = 0; i < parts.Length && i < 64; i++)
			{
				string text = parts[i].Name;
				if (text != null && parts[i].VertexCount != 0 && (text.StartsWith("hair_", StringComparison.Ordinal) || (beard && text.StartsWith("beard_", StringComparison.Ordinal))) && (mesh.Hidden & (ulong)(1L << i)) == 0L)
				{
					list.Add(text);
				}
			}
			return string.Join(",", list.ToArray());
		}

		private static void Put(Actor a, string id)
		{
			if (a == null || id == null)
			{
				return;
			}
			try
			{
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				if (equipmentAsset == null || a.equipment == null)
				{
					Log.Error("RG no item " + id);
					return;
				}
				ActorEquipmentSlot slot = a.equipment.getSlot(equipmentAsset.equipment_type);
				Item item = World.world.items.generateItem(equipmentAsset, a.kingdom, a.getName(), 1, a, 0, pByPlayer: true);
				if (item == null)
				{
					Log.Error("RG item not made " + id);
					return;
				}
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				slot.setItem(item, a);
				a.setStatsDirty();
			}
			catch (Exception ex)
			{
				Log.Error("RG wear " + id + ": " + ex.Message);
			}
		}

		private static void TakeOff(Actor a, EquipmentType type)
		{
			try
			{
				ActorEquipmentSlot slot = a.equipment.getSlot(type);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				a.setStatsDirty();
			}
			catch
			{
			}
		}

		private static bool HelmetOn(Actor a)
		{
			try
			{
				return a.equipment.getSlot(EquipmentType.Helmet).getItem() != null;
			}
			catch
			{
				return false;
			}
		}

		private Actor Newcomer(Realm r, bool female, Vector2 at)
		{
			Actor actor = CivilTest.SpawnNear("human", at, 1f);
			if (actor == null)
			{
				return null;
			}
			CivilTest.SetSex(actor, female);
			Join(r, actor);
			Invincible(actor);
			return actor;
		}

		private Actor Recruit(Realm r, Vector2 at, string helmet = null)
		{
			Actor actor = CivilTest.SpawnNear((r.Founder != null && r.Founder.asset != null) ? r.Founder.asset.id : "human", at, 1f);
			if (actor == null)
			{
				return null;
			}
			Join(r, actor);
			try
			{
				r.Town.makeWarrior(actor);
			}
			catch (Exception ex)
			{
				Note("warrior: " + ex.Message);
			}
			_soldiers.Add(actor);
			Invincible(actor);
			if (helmet != null)
			{
				Put(actor, helmet);
			}
			return actor;
		}

		private bool MakeCaptain(Actor a)
		{
			try
			{
				City city = a.city;
				if (city == null)
				{
					return false;
				}
				Army army = (city.hasArmy() ? city.army : World.world.armies.newArmy(a, city));
				if (army == null)
				{
					return false;
				}
				if (a.army != army)
				{
					a.setArmy(army);
				}
				if (army.getCaptain() != a)
				{
					army.setCaptain(a);
				}
				if (a.is_army_captain)
				{
					_captains.Add(a);
					_soldiers.Add(a);
				}
				return a.is_army_captain;
			}
			catch (Exception ex)
			{
				Note("captain: " + ex.Message);
				return false;
			}
		}

		private bool MakeHeir(Realm r, Actor h)
		{
			try
			{
				Actor king = r.K.king;
				if (king == null || h == null)
				{
					return false;
				}
				Clan clan = World.world.clans.newClan(king, pAddDefaultTraits: false);
				if (clan == null)
				{
					return false;
				}
				king.setClan(clan);
				h.setClan(clan);
				r.K.data.royal_clan_id = clan.id;
				return SuccessionTool.findNextHeir(r.K, king) == h;
			}
			catch (Exception ex)
			{
				Note("heir: " + ex.Message);
				return false;
			}
		}

		private static bool Fem(Actor a)
		{
			return Models.IsFemale(a);
		}

		private static bool ModelIs(string name, RegaliaKit.Set set, KingdomStyle style, bool female)
		{
			for (int i = 0; i < 3; i++)
			{
				if (name != null && name == RegaliaKit.ModelName(set, style, female, i))
				{
					return true;
				}
			}
			return false;
		}

		private static void Die(Actor a)
		{
			if (a == null)
			{
				return;
			}
			try
			{
				a.finishStatusEffect("invincible");
			}
			catch
			{
			}
			CivilTest.Kill(a);
			try
			{
				if (a.isAlive())
				{
					((BaseSimObject)a).getHit((float)a.getHealth() + 99999f, pFlash: true, AttackType.Other, (BaseSimObject)null, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: false);
				}
			}
			catch
			{
			}
		}

		private Color32[] Grab(out int width, out int height)
		{
			width = (height = 0);
			Texture2D texture2D = ((!Mod.Settings.GpuRender) ? null : Mod.GpuRendererForTest?.ReadOutput());
			if (texture2D != null)
			{
				width = texture2D.width;
				height = texture2D.height;
				Color32[] pixels = texture2D.GetPixels32();
				UnityEngine.Object.Destroy(texture2D);
				return pixels;
			}
			Texture2D frameTexture = Mod.FrameTexture;
			if (frameTexture == null)
			{
				return null;
			}
			width = frameTexture.width;
			height = frameTexture.height;
			return frameTexture.GetPixels32();
		}

		private IEnumerator Capture(string name, float wait = 0.3f)
		{
			if (wait > 0f)
			{
				yield return CivilTest.Wait(wait);
			}
			Mod.HudForTest.CaptureHudForTest(Path.Combine(Log.Folder, name));
			for (int i = 0; i < 20; i++)
			{
				if (!Mod.HudForTest.CapturePending)
				{
					break;
				}
				yield return null;
			}
			Note("captured " + name);
		}

		private void Frame(string name)
		{
			Mod.SaveFrame(name);
		}

		private static float Field(object o, string name)
		{
			FieldInfo fieldInfo = o?.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (!(fieldInfo != null) || !(fieldInfo.FieldType == typeof(float)))
			{
				return -1f;
			}
			return (float)fieldInfo.GetValue(o);
		}

		private static RegaliaDress.Wearer WearerOf(Actor a)
		{
			try
			{
				object obj = typeof(People).GetMethod("WornFor", BindingFlags.Static | BindingFlags.NonPublic)?.Invoke(null, new object[1] { a });
				return (obj?.GetType().GetField("W", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))?.GetValue(obj) as RegaliaDress.Wearer;
			}
			catch
			{
				return null;
			}
		}

		private IEnumerator Press(Vector2 at)
		{
			_events.Enqueue(new Event
			{
				type = EventType.MouseDown,
				button = 0,
				mousePosition = at,
				clickCount = 1
			});
			_events.Enqueue(new Event
			{
				type = EventType.MouseUp,
				button = 0,
				mousePosition = at,
				clickCount = 1
			});
			for (int i = 0; i < 300; i++)
			{
				if (_events.Count <= 0)
				{
					break;
				}
				yield return null;
			}
			yield return null;
			yield return null;
		}

		private void OnGUI()
		{
			if (Mod == null || _events.Count == 0)
			{
				return;
			}
			Event current = Event.current;
			try
			{
				Event.current = _events.Dequeue();
				if (Mod.Royal.Open)
				{
					Mod.HudForTest.DrawCouncil();
				}
			}
			catch (Exception ex)
			{
				Log.Error("RG events: " + ex);
			}
			finally
			{
				Event.current = current;
			}
		}

		private IEnumerator EnsureRoles()
		{
			if (_rolesReady)
			{
				yield break;
			}
			Kingdom k = _k1.K;
			Actor king = k.king;
			List<Actor> list = new List<Actor>();
			List<Actor> list2 = new List<Actor>();
			foreach (Actor person in _k1.People)
			{
				if (person == null || !person.isAlive() || person == king || person == _photographer)
				{
					continue;
				}
				if (!person.isAdult())
				{
					if (_child == null)
					{
						_child = person;
					}
				}
				else if (person.isWarrior())
				{
					list2.Add(person);
				}
				else
				{
					list.Add(person);
				}
			}
			while (list2.Count < 22)
			{
				Actor actor = Recruit(_k1, _k1.Centre);
				if (actor == null)
				{
					break;
				}
				list2.Add(actor);
			}
			while (list.Count < 8)
			{
				Actor actor2 = Newcomer(_k1, female: false, _k1.Centre);
				if (actor2 == null)
				{
					break;
				}
				list.Add(actor2);
			}
			if (!Check("people enough for every role", king != null && list.Count >= 8 && list2.Count >= 22, $"king {king != null}, {list.Count} plain, {list2.Count} warriors"))
			{
				yield break;
			}
			foreach (Actor item in list2)
			{
				TakeOff(item, EquipmentType.Helmet);
				TakeOff(item, EquipmentType.Armor);
				item.data.kills = 0;
				_soldiers.Add(item);
			}
			king.data.kills = 0;
			_leader = list[0];
			try
			{
				_k1.Town.removeLeader();
				_k1.Town.setLeader(_leader, pNew: true);
			}
			catch (Exception ex)
			{
				Note("leader: " + ex.Message);
			}
			_heir = list[1];
			bool heir = MakeHeir(_k1, _heir);
			for (int i = 0; i < 5; i++)
			{
				_seats[i] = list[2 + i];
				if (!Court.Appoint(k, (Seat)i, _seats[i]))
				{
					RegaliaTest regaliaTest = this;
					Seat seat = (Seat)i;
					regaliaTest.Note("could not appoint " + seat);
				}
			}
			Court.Invalidate(k);
			_plain = list[7];
			_captain = list2[0];
			Put(_captain, "helmet_iron");
			bool flag = MakeCaptain(_captain);
			_champion = list2[1];
			Put(_champion, "helmet_steel");
			_champion.data.kills = 50;
			_helmeted.Clear();
			for (int j = 0; j < _helmetOf.Length; j++)
			{
				_helmeted.Add(list2[2 + j]);
				Put(list2[2 + j], _helmetOf[j]);
			}
			_coif = list2[7];
			_coifChest = list2[8];
			Put(_coifChest, "armor_iron");
			_guard = list2[9];
			try
			{
				Note($"K1's town: {_k1.Town.status.warriors_current} warriors of {_k1.Town.status.warrior_slots} slots");
			}
			catch
			{
			}
			CivilTest.SetSex(_guard, female: true);
			People.ForgetLookForTest(_guard);
			_spare.Clear();
			for (int l = 10; l < list2.Count; l++)
			{
				_spare.Add(list2[l]);
			}
			_queen = null;
			foreach (Actor person2 in _k2.People)
			{
				if (person2 != null && person2.isAlive() && person2.isAdult() && !person2.isWarrior() && person2 != _k2.K.king && Fem(person2))
				{
					_queen = person2;
					break;
				}
			}
			if (_queen == null)
			{
				_queen = Newcomer(_k2, female: true, _k2.Centre);
			}
			try
			{
				if (_queen != null)
				{
					_k2.K.setKing(_queen);
				}
			}
			catch (Exception ex2)
			{
				Note("queen: " + ex2.Message);
			}
			Check("the captain leads the town's army", flag && _captain.isArmyGroupLeader(), D(_k1.Town.army?.getCaptain()));
			Check("the leader leads the town", _leader.isCityLeader());
			Check("the queen rules K2", _queen != null && _queen.isKing() && Fem(_queen), D(_k2.K.king));
			string text = "";
			for (int m = 0; m < 5; m++)
			{
				string[] obj2 = new string[5] { text, null, null, null, null };
				Seat seat = (Seat)m;
				obj2[1] = seat.ToString();
				obj2[2] = " ";
				obj2[3] = ((Court.Holder(k, (Seat)m) == _seats[m]) ? "ok" : D(Court.Holder(k, (Seat)m)));
				obj2[4] = "; ";
				text = string.Concat(obj2);
			}
			Check("the five seats appointed", Court.Holder(k, Seat.Marshal) == _seats[0] && Court.Holder(k, Seat.Steward) == _seats[1] && Court.Holder(k, Seat.Chancellor) == _seats[2] && Court.Holder(k, Seat.Spymaster) == _seats[3] && Court.Holder(k, Seat.Mage) == _seats[4], text);
			_rolesReady = true;
			foreach (Realm item2 in AllRealms())
			{
				foreach (Actor person3 in item2.People)
				{
					Regalia.RoleOf(person3);
				}
			}
			yield return Refresh(_k1.People, 3.5f);
			Actor actor3 = SuccessionTool.findNextHeir(k, k.king);
			Check("the heir is who the game names (royal clan)", actor3 == _heir && Regalia.RoleOf(_heir, forFigure: true).Has(RoleBits.Heir), $"named {D(actor3)}, made {D(_heir)} (at once: {heir})");
			_k2Soldier = PlainSoldier(_k2);
		}

		private Actor PlainSoldier(Realm r, params Actor[] not)
		{
			foreach (Actor person in r.People)
			{
				if (person != null && person.isAlive() && person.isWarrior() && person != r.K.king && Array.IndexOf(not, person) < 0)
				{
					RoleInfo roleInfo = Regalia.RoleOf(person, forFigure: true);
					if ((roleInfo.Bits & (RoleBits.King | RoleBits.Heir | RoleBits.Leader | RoleBits.Councillor | RoleBits.Champion | RoleBits.Captain)) == 0 && roleInfo.Has(RoleBits.Soldier))
					{
						return person;
					}
				}
			}
			return Recruit(r, r.Centre);
		}

		private IEnumerator RolesPart()
		{
			yield return EnsureRoles();
			if (!_rolesReady)
			{
				yield break;
			}
			Kingdom k = _k1.K;
			KingdomLook look = Regalia.LookOf(k);
			Actor king = k.king;
			Actor orcSoldier = null;
			if (_orc != null)
			{
				foreach (Actor person in _orc.People)
				{
					if (person != null && person.isAlive() && person.isWarrior())
					{
						orcSoldier = person;
						break;
					}
				}
			}
			List<Actor> list = new List<Actor>
			{
				king,
				_queen,
				_leader,
				_heir,
				_seats[0],
				_seats[1],
				_seats[2],
				_seats[3],
				_seats[4]
			};
			List<Actor> list2 = new List<Actor> { _captain, _champion };
			list2.AddRange(_helmeted);
			list2.AddRange(new Actor[7] { _coif, _coifChest, _guard, _k2Soldier, _plain, _child, orcSoldier });
			list2.RemoveAll((Actor actor) => actor == null);
			Vector2 vector = _stage + new Vector2(0f, -1.6f);
			Vector2 backAt = _stage + new Vector2(0f, 1.6f);
			Vector2 eye = vector + new Vector2(0f, -8f);
			Vector2 eyeN = backAt + new Vector2(0f, 9f);
			Row(list, vector, eye, 1.1f);
			Row(list2, backAt, eyeN, 0.95f);
			List<Actor> list3 = new List<Actor>(list);
			list3.AddRange(list2);
			Look(eye, vector);
			yield return Refresh(list3, 1.5f);
			Config.paused = true;
			yield return Frames(4);
			string[] list4 = ShownOf(king);
			Check("king: open crown, ermine yoke, mantle and hem", Has(list4, "royal:crown") && Has(list4, "royal:mantle_yoke") && Has(list4, "royal:mantle") && Has(list4, "royal:mantle_hem"), S(king));
			string text = RegaliaKit.ModelName(RegaliaKit.Set.Royal, look.Style, Fem(king), 0);
			Check("king: his realm's style", ModelIs(People.RegaliaModelForTest(king, RegaliaKit.Set.Royal), RegaliaKit.Set.Royal, look.Style, Fem(king)), People.RegaliaModelForTest(king, RegaliaKit.Set.Royal) + " want " + text);
			Check("king: no sash, no soldier's set", !HasAny(list4, "soldier:") && !HasAny(list4, "captain:"), S(king));
			Check("king's head shows his crown", People.HelmetLookForTest(king) == "crown:" + Letter(k), People.HelmetLookForTest(king));
			string text2 = People.RegaliaModelForTest(_queen, RegaliaKit.Set.Royal);
			Check("queen: the queen's royal set of her realm's style", ModelIs(text2, RegaliaKit.Set.Royal, Regalia.LookOf(_k2.K).Style, female: true), text2 + "; " + S(_queen));
			list4 = ShownOf(_leader);
			Check("leader: hat and chain of office", Has(list4, "court:hat") && (Has(list4, "court:chain") || Has(list4, "court:chain_over")), S(_leader));
			Check("leader: the hat in the realm's colour", Rgb(People.RegaliaSlotForTest(_leader, RegaliaKit.Set.Court, "banner")) == Rgb(look.Colors.Main), Hex(People.RegaliaSlotForTest(_leader, RegaliaKit.Set.Court, "banner")) + " main " + Hex(look.Colors.Main));
			Check("heir: circlet", HasAny(ShownOf(_heir), "honour:circlet"), S(_heir));
			list4 = ShownOf(_seats[0]);
			Check("marshal: baton and gold sash", Has(list4, "court:baton") && Has(list4, "soldier:sash") && Rgb(People.RegaliaSlotForTest(_seats[0], RegaliaKit.Set.Soldier, "sash")) == Rgb(RegaliaKit.SashGold), S(_seats[0]) + " sash " + Hex(People.RegaliaSlotForTest(_seats[0], RegaliaKit.Set.Soldier, "sash")));
			list4 = ShownOf(_seats[1]);
			Check("steward: keys and ledger", (Has(list4, "court:keys") || Has(list4, "court:keys_over")) && Has(list4, "court:ledger"), S(_seats[1]));
			Check("chancellor: seal chain", HasAny(ShownOf(_seats[2]), "court:seal"), S(_seats[2]));
			list4 = ShownOf(_seats[3]);
			Check("spymaster: dark hood, no coif or plume", Has(list4, "court:hood") && !Has(list4, "soldier:coif") && !Has(list4, "soldier:plume"), S(_seats[3]));
			list4 = ShownOf(_seats[4]);
			Check("court mage: starry robe and staff", Has(list4, "robe:robe") && Has(list4, "robe:robe_hem") && HasAny(list4, "robe:robe_yoke") && Has(list4, "robe:staff"), S(_seats[4]));
			Frame("rg_lineup_front.png");
			Look(eyeN, backAt);
			Config.paused = false;
			yield return Shown(8);
			yield return CivilTest.Wait(0.8f);
			Config.paused = true;
			yield return Frames(4);
			list4 = ShownOf(_captain);
			text = RegaliaKit.ModelName(RegaliaKit.Set.Captain, look.Style, Fem(_captain), 0);
			Check("captain: pole, harness, banner of his realm's style", Has(list4, "captain:pole") && Has(list4, "captain:banner") && Has(list4, "captain:banner_tail") && HasAny(list4, "captain:harness") && ModelIs(People.RegaliaModelForTest(_captain, RegaliaKit.Set.Captain), RegaliaKit.Set.Captain, look.Style, Fem(_captain)), S(_captain) + " model " + People.RegaliaModelForTest(_captain, RegaliaKit.Set.Captain) + " want " + text);
			Check("captain: tall plume on his helmet, gold-edged sash", Has(list4, "soldier:plume") && Has(list4, "soldier:sash") && Rgb(People.RegaliaSlotForTest(_captain, RegaliaKit.Set.Soldier, "edge")) == Rgb(RegaliaKit.Gold), "edge " + Hex(People.RegaliaSlotForTest(_captain, RegaliaKit.Set.Soldier, "edge")));
			list4 = ShownOf(_champion);
			Check("champion: laurel over the helmet, no plume", HasAny(list4, "honour:laurel") && !Has(list4, "soldier:plume") && Has(list4, "soldier:sash"), S(_champion) + " champion " + D(Regalia.ChampionOf(k)));
			Check("champion: gold-edged sash", Rgb(People.RegaliaSlotForTest(_champion, RegaliaKit.Set.Soldier, "edge")) == Rgb(RegaliaKit.Gold), Hex(People.RegaliaSlotForTest(_champion, RegaliaKit.Set.Soldier, "edge")));
			for (int num = 0; num < _helmeted.Count; num++)
			{
				Actor a = _helmeted[num];
				list4 = ShownOf(a);
				Check("soldier in " + _helmetOf[num] + ": sash, knot and plume in the realm's colour", Has(list4, "soldier:sash") && Has(list4, "soldier:sashknot") && Has(list4, "soldier:plume") && Rgb(People.RegaliaSlotForTest(a, RegaliaKit.Set.Soldier, "sash")) == Rgb(look.Colors.Main), S(a) + " sash " + Hex(People.RegaliaSlotForTest(a, RegaliaKit.Set.Soldier, "sash")));
			}
			list4 = ShownOf(_coif);
			Check("bare soldier: mail coif with its cape, sash, no plume", Has(list4, "soldier:coif") && Has(list4, "soldier:coif_cape") && Has(list4, "soldier:sash") && !Has(list4, "soldier:plume"), S(_coif));
			string text3 = HairShowing(_coif, beard: true);
			Check("bare soldier: hair and beard hidden under the coif", text3 == "", "showing: " + text3);
			list4 = ShownOf(_coifChest);
			Check("bare soldier in a breastplate: coif without the cape", Has(list4, "soldier:coif") && !Has(list4, "soldier:coif_cape"), S(_coifChest));
			list4 = ShownOf(_guard);
			uint c = look.Colors.Slot("stripe", SashGear.Striped, 0u);
			Check("guard at home: halberd and striped sash", People.HalberdForTest(_guard) && Rgb(People.RegaliaSlotForTest(_guard, RegaliaKit.Set.Soldier, "stripe")) == Rgb(c), S(_guard) + " stripe " + Hex(People.RegaliaSlotForTest(_guard, RegaliaKit.Set.Soldier, "stripe")) + " want " + Hex(c) + ", weapon " + People.WeaponForTest(_guard, out var _));
			Check("a woman soldier: the women's soldier set, sash and coif", Fem(_guard) && (People.RegaliaModelForTest(_guard, RegaliaKit.Set.Soldier) ?? "").Contains("_human_fem") && Has(list4, "soldier:sash") && Has(list4, "soldier:coif"), People.RegaliaModelForTest(_guard, RegaliaKit.Set.Soldier) + " " + S(_guard));
			Check("two bare guards of a realm look the same", string.Join(",", ShownOf(_coif)) == string.Join(",", ShownOf(_guard)).Replace("halberd:halberd", "").TrimEnd(',') || string.Join(",", ShownOf(_coif)) == string.Join(",", ShownOf(_guard)), S(_coif) + " | " + S(_guard));
			Check("...and wear the same colours", Rgb(People.RegaliaSlotForTest(_coif, RegaliaKit.Set.Soldier, "sash")) == Rgb(People.RegaliaSlotForTest(_guard, RegaliaKit.Set.Soldier, "sash")));
			if (_k2Soldier != null)
			{
				uint num2 = People.RegaliaSlotForTest(_coif, RegaliaKit.Set.Soldier, "sash");
				uint num3 = People.RegaliaSlotForTest(_k2Soldier, RegaliaKit.Set.Soldier, "sash");
				Check("a K2 soldier wears K2's colour", Rgb(num3) == Rgb(Regalia.LookOf(_k2.K).Colors.Main), Hex(num3) + " K2 main " + Hex(Regalia.LookOf(_k2.K).Colors.Main) + "; " + S(_k2Soldier));
				Check("the two realms' sashes differ (dE > 10)", DeltaE(num2, num3) > 10f, $"{Hex(num2)} vs {Hex(num3)}: dE {DeltaE(num2, num3):F1}");
			}
			Check("a plain townsman wears nothing", None(_plain), S(_plain));
			if (_child != null)
			{
				Check("a child wears nothing", None(_child), S(_child));
			}
			if (orcSoldier != null)
			{
				Check("an orc soldier wears nothing (humans only)", None(orcSoldier), S(orcSoldier));
			}
			Frame("rg_lineup_back.png");
			Config.paused = false;
			List<Actor> who = new List<Actor>
			{
				king,
				_queen,
				_k3.K.king,
				_k4.K.king
			};
			yield return Group("rg_close_rulers.png", who);
			yield return Group("rg_close_court1.png", new List<Actor>
			{
				_leader,
				_heir,
				_seats[0],
				_seats[1]
			});
			yield return Group("rg_close_court2.png", new List<Actor>
			{
				_seats[2],
				_seats[3],
				_seats[4],
				_champion
			});
			yield return Group("rg_close_soldiers.png", new List<Actor>(_helmeted), backs: false, 4.4f);
			yield return Group("rg_close_coifs.png", new List<Actor> { _coif, _coifChest, _guard, _k2Soldier });
			yield return Group("rg_close_captain.png", new List<Actor> { _captain });
			yield return Group("rg_close_captain_back.png", new List<Actor> { _captain }, backs: true, 3.6f);
			yield return Campaigns(look);
			yield return GuardFight();
			yield return Champions(k);
			yield return GuardAbout();
			yield return Zombie();
		}

		private IEnumerator Campaigns(KingdomLook look)
		{
			if (_spare.Count < 5)
			{
				Note("not enough spare soldiers for the campaign checks");
				yield break;
			}
			Actor marcher = _spare[0];
			Actor holder = _spare[1];
			Vector2 at = _stage + new Vector2(-6f, 4f);
			Hold(marcher, at);
			Hold(holder, at + new Vector2(1.5f, 0f));
			Look(at + new Vector2(0.75f, -6f), at);
			yield return Refresh(new Actor[2] { marcher, holder }, 1.2f);
			Check("a soldier at home is a guard (halberd)", People.HalberdForTest(marcher) && !Regalia.OnCampaignForTest(marcher), S(marcher));
			Release(marcher);
			Release(holder);
			Royal.RegisterTask();
			WorldTile beh_tile_target = LandNear(at + new Vector2(-28f, 10f));
			WorldTile flag = LandNear(at + new Vector2(-8f, -6f));
			try
			{
				marcher.cancelAllBeh();
				marcher.setTask("fp_royal_march");
				marcher.beh_tile_target = beh_tile_target;
			}
			catch (Exception ex)
			{
				Note("march: " + ex.Message);
			}
			bool held = Royal.Hold(holder, flag, 2f);
			for (float t = 0f; t < 2.5f; t += Time.unscaledDeltaTime)
			{
				Vector2 current_position = marcher.current_position;
				Look(current_position + new Vector2(2f, -5f), current_position);
				yield return null;
			}
			People.RecheckForTest(marcher);
			yield return Shown(4);
			uint plainStripe = look.Colors.Slot("stripe", SashGear.Plain, 0u);
			Check("marching (war map order): plain sash, no halberd", Regalia.OnCampaignForTest(marcher) && !People.HalberdForTest(marcher) && Has(ShownOf(marcher), "soldier:sash") && Rgb(People.RegaliaSlotForTest(marcher, RegaliaKit.Set.Soldier, "stripe")) == Rgb(plainStripe), string.Format("task {0}, campaign {1}, {2}, stripe {3}", ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)marcher.ai)?.task?.id, Regalia.OnCampaignForTest(marcher), S(marcher), Hex(People.RegaliaSlotForTest(marcher, RegaliaKit.Set.Soldier, "stripe"))));
			Vector2 current_position2 = holder.current_position;
			Look(current_position2 + new Vector2(2f, -5f), current_position2);
			People.RecheckForTest(holder);
			yield return Shown(6);
			yield return CivilTest.Wait(0.3f);
			Check("holding a flag: plain sash, no halberd", held && Royal.IsHolding(holder) && Regalia.OnCampaignForTest(holder) && !People.HalberdForTest(holder), $"holding {Royal.IsHolding(holder)}, {S(holder)}");
			try
			{
				marcher.cancelAllBeh();
				holder.cancelAllBeh();
			}
			catch
			{
			}
			if (_orc == null)
			{
				Note("no orc realm: the game's own campaign is not checked");
				yield break;
			}
			Actor king = _k1.K.king;
			Actor a1 = _spare[2];
			Actor a2 = _spare[3];
			Actor home = _spare[4];
			Army army = _k1.Town.army;
			try
			{
				Mod.Royal.DeclareWar(king, _orc.K, delegate(string m)
				{
					Note("toast: " + m);
				}, delegate
				{
				});
				if (army != null)
				{
					a1.setArmy(army);
					a2.setArmy(army);
				}
				home.setArmy(null);
				WorldTile tile = _orc.Town.getTile();
				_k1.Town.target_attack_city = _orc.Town;
				_k1.Town.target_attack_zone = tile?.zone;
			}
			catch (Exception ex2)
			{
				Note("war: " + ex2.Message);
			}
			Vector2 vector = _stage + new Vector2(6f, 4f);
			Hold(a1, vector);
			Hold(a2, vector + new Vector2(1.2f, 0f));
			Hold(home, vector + new Vector2(2.4f, 0f));
			Look(vector + new Vector2(1.2f, -6f), vector + new Vector2(1.2f, 0f));
			yield return Refresh(new Actor[3] { a1, a2, home }, 1.5f);
			bool flag2 = Regalia.OnCampaignForTest(a1);
			bool flag3 = Regalia.OnCampaignForTest(a2);
			Check("the game's army sent out: its soldiers wear the plain sash, no halberd", flag2 && flag3 && !People.HalberdForTest(a1) && Rgb(People.RegaliaSlotForTest(a1, RegaliaKit.Set.Soldier, "stripe")) == Rgb(plainStripe), $"army {a1.isArmyGroupWarrior()}/{a2.isArmyGroupWarrior()}, order {_k1.Town.hasAttackZoneOrder()}, campaign {flag2}/{flag3}; {S(a1)}");
			Check("the soldier left at home (no army) keeps the halberd", !Regalia.OnCampaignForTest(home) && People.HalberdForTest(home), $"army {home.army != null}; {S(home)}");
			Frame("rg_campaign.png");
			try
			{
				_k1.Town.target_attack_zone = null;
				_k1.Town.target_attack_city = null;
			}
			catch
			{
			}
			EndWars(_k1.K);
			Release(a1);
			Release(a2);
			Release(home);
		}

		private void EndWars(Kingdom k)
		{
			try
			{
				List<War> list = new List<War>();
				foreach (War war in World.world.wars.getWars(k))
				{
					if (war != null && !war.hasEnded())
					{
						list.Add(war);
					}
				}
				foreach (War item in list)
				{
					World.world.wars.endWar(item, WarWinner.Peace);
				}
				if (list.Count > 0)
				{
					Note($"{list.Count} war(s) of {k.name} ended");
				}
			}
			catch (Exception ex)
			{
				Note("ending wars: " + ex.Message);
			}
		}

		private IEnumerator GuardFight()
		{
			if (_spare.Count < 7)
			{
				Note("not enough spare soldiers for the fight checks");
				yield break;
			}
			Actor guard = _spare[5];
			Actor archer = _spare[6];
			Put(guard, "sword_iron");
			Put(archer, "bow_wood");
			Vector2 at = _stage + new Vector2(-3f, -5f);
			Hold(guard, at);
			Hold(archer, at + new Vector2(-1.4f, 0.6f));
			Look(at + new Vector2(0f, -5.5f), at);
			yield return Refresh(new Actor[2] { guard, archer }, 1.2f);
			int detail;
			string text = People.WeaponForTest(guard, out detail);
			Check("a guard at rest: the halberd in place of his sword", People.HalberdForTest(guard), "weapon " + text + "; " + S(guard));
			Check("an archer guard keeps his bow (no halberd)", !People.HalberdForTest(archer), "weapon " + People.WeaponForTest(archer, out detail) + "; " + S(archer));
			Actor foe = CivilTest.SpawnNear("bandit", at + new Vector2(1.2f, 0.4f), 0.5f);
			if (!Check("a bandit to fight", foe != null))
			{
				yield break;
			}
			Release(guard);
			try
			{
				guard.addAggro(foe);
				guard.startFightingWith(foe);
				foe.startFightingWith(guard);
			}
			catch (Exception ex)
			{
				Note("fight: " + ex.Message);
			}
			string inFight = null;
			for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime)
			{
				yield return null;
				try
				{
					if (guard.has_attack_target && !guard.isFighting())
					{
						guard.startFightingWith(foe);
					}
				}
				catch
				{
				}
				string text2 = People.WeaponForTest(guard, out detail);
				if (text2 != null && !text2.StartsWith("rg_halberd", StringComparison.Ordinal) && !People.HalberdForTest(guard))
				{
					inFight = text2;
					break;
				}
			}
			Check("in a fight: his own weapon within 1 s", inFight != null, $"weapon {People.WeaponForTest(guard, out detail)}, target {guard.has_attack_target}");
			Frame("rg_guard_fight.png");
			Die(foe);
			yield return null;
			Note($"the bandit after the blow: alive {foe.isAlive()}");
			bool flag = false;
			for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
			{
				if (flag)
				{
					break;
				}
				yield return null;
				flag = People.HalberdForTest(guard);
			}
			string text3 = Busy(guard);
			BaseSimObject baseSimObject = null;
			try
			{
				baseSimObject = ((guard.has_attack_target && guard.attack_target != null && guard.attack_target.isAlive()) ? guard.attack_target : null);
			}
			catch
			{
			}
			if (!flag && baseSimObject != null)
			{
				Note("the guard fights on: " + text3);
				Hold(guard, at);
				for (float t = 0f; t < 3f; t += Time.unscaledDeltaTime)
				{
					if (flag)
					{
						break;
					}
					yield return null;
					flag = People.HalberdForTest(guard);
				}
				Check("the halberd again within 3 s once his fighting ends", flag, "weapon " + People.WeaponForTest(guard, out detail) + "; " + Busy(guard) + "; " + S(guard));
			}
			else
			{
				Check("the halberd again within 3 s of the kill", flag, "weapon " + People.WeaponForTest(guard, out detail) + "; " + text3 + "; " + S(guard));
			}
			Release(guard);
			Release(archer);
		}

		private static string Busy(Actor a)
		{
			try
			{
				BaseSimObject attack_target = a.attack_target;
				string text = ((attack_target == null) ? "none" : ((attack_target is Actor) ? (D((Actor)attack_target) + (attack_target.isAlive() ? " alive" : " dead")) : attack_target.ToString()));
				RegaliaDress.Wearer wearer = WearerOf(a);
				return $"target {text} (has {a.has_attack_target}), task {((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.id} (tool {((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.force_hand_tool}), carrying {a.isCarryingResources()}, " + $"gear hand {Regalia.GearOf(a, handsBusy: false).Hand}, plan halberd {wearer?.Plan.Halberd}, inside {a.is_inside_building}";
			}
			catch (Exception ex)
			{
				return "(" + ex.Message + ")";
			}
		}

		private IEnumerator Champions(Kingdom k)
		{
			if (_spare.Count < 8)
			{
				yield break;
			}
			Actor ch = _champion;
			Actor v = _spare[7];
			Vector2 vector = _stage + new Vector2(3f, -5f);
			Hold(ch, vector);
			Hold(v, vector + new Vector2(1.3f, 0f));
			Look(vector + new Vector2(0.6f, -5f), vector);
			yield return Refresh(new Actor[2] { ch, v }, 1f);
			Check("the champion is the realm's best", Regalia.ChampionOf(k) == ch && HasAny(ShownOf(ch), "honour:laurel"), D(Regalia.ChampionOf(k)) + "; " + S(ch));
			v.data.kills = 80;
			Actor now = null;
			for (float t = 0f; t < 8f; t += 0.25f)
			{
				if (now == v)
				{
					break;
				}
				yield return CivilTest.Wait(0.25f);
				foreach (Actor person in _k1.People)
				{
					Regalia.RoleOf(person);
				}
				now = Regalia.ChampionOf(k);
			}
			yield return Refresh(new Actor[2] { ch, v }, 0.5f);
			Check("a better warrior takes the laurel", now == v && HasAny(ShownOf(v), "honour:laurel") && !HasAny(ShownOf(ch), "honour:laurel"), D(now) + "; " + S(v) + " | " + S(ch));
			ch.data.level = v.data.level;
			ch.data.kills = v.data.kills;
			yield return CivilTest.Wait(4.5f);
			foreach (Actor person2 in _k1.People)
			{
				Regalia.RoleOf(person2);
			}
			yield return CivilTest.Wait(0.5f);
			Check("a tie doesn't move it", Regalia.ChampionOf(k) == v, D(Regalia.ChampionOf(k)));
			k.king.data.kills = 999;
			yield return CivilTest.Wait(4.5f);
			foreach (Actor person3 in _k1.People)
			{
				Regalia.RoleOf(person3);
			}
			yield return CivilTest.Wait(0.5f);
			Check("the king is never champion", Regalia.ChampionOf(k) != k.king && Regalia.ChampionOf(k) != null, D(Regalia.ChampionOf(k)));
			k.king.data.kills = 0;
			ch.data.kills = 50;
			ch.data.level = 1;
			v.data.kills = 0;
			Release(v);
			Hold(ch, _stage + new Vector2(0f, 1.6f));
		}

		private Actor CurrentGuard()
		{
			List<Actor> list = new List<Actor>();
			list.Add(_coif);
			list.Add(_coifChest);
			list.Add(_guard);
			list.AddRange(_spare);
			foreach (Actor item in list)
			{
				if (item != null && item.isAlive())
				{
					RoleInfo roleInfo = Regalia.RoleOf(item, forFigure: true);
					if (roleInfo.Has(RoleBits.Guard) && !roleInfo.Has(RoleBits.OnCampaign) && (roleInfo.Bits & (RoleBits.Heir | RoleBits.Councillor | RoleBits.Champion | RoleBits.Captain)) == 0)
					{
						return item;
					}
				}
			}
			return null;
		}

		private IEnumerator GuardAbout()
		{
			Actor g = CurrentGuard();
			if (g == null)
			{
				Note("no guard at home to let go");
				yield break;
			}
			Release(g);
			try
			{
				g.cancelAllBeh();
			}
			catch
			{
			}
			int frames = 0;
			int free = 0;
			int halberd = 0;
			string seen = "";
			for (float t = 0f; t < 12f; t += Time.unscaledDeltaTime)
			{
				Vector2 current_position = g.current_position;
				Look(current_position + new Vector2(1.5f, -5f), current_position, -4f);
				yield return null;
				if (t < 1f)
				{
					continue;
				}
				frames++;
				string text = null;
				string text2 = null;
				bool flag = false;
				bool flag2 = false;
				try
				{
					text = ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)g.ai)?.task?.id;
					text2 = ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)g.ai)?.task?.force_hand_tool;
					flag = g.isCarryingResources();
					flag2 = g.has_attack_target;
				}
				catch
				{
				}
				if (text != null && !seen.Contains(text))
				{
					seen = seen + text + "(tool '" + text2 + "') ";
				}
				if (!(text == null || !string.IsNullOrEmpty(text2) || flag || flag2) && !g.is_inside_building)
				{
					free++;
					if (People.HalberdForTest(g))
					{
						halberd++;
					}
				}
			}
			Note($"a guard about town for 11 s: tasks {seen}; {free} of {frames} frames on a task with no tool, the halberd in {halberd} of them");
			if (free > 20)
			{
				Check("a guard about his business at home (a task with no tool) holds his halberd", (float)halberd >= (float)free * 0.8f, $"{halberd} of {free} frames; {Busy(g)}; {S(g)}");
			}
			else
			{
				Note("the guard spent too little time on toolless tasks to judge");
			}
			Hold(g, _stage + new Vector2(4f, 1.6f));
		}

		private IEnumerator Zombie()
		{
			if (_spare.Count < 9)
			{
				yield break;
			}
			Actor actor = _spare[8];
			HashSet<long> before = new HashSet<long>();
			foreach (Actor unit in World.world.units)
			{
				try
				{
					if (unit.asset != null && unit.asset.unit_zombie)
					{
						before.Add(unit.data.id);
					}
				}
				catch
				{
				}
			}
			Vector2 at = _stage + new Vector2(0f, -5f);
			Release(actor);
			_soldiers.Remove(actor);
			try
			{
				actor.finishStatusEffect("invincible");
				actor.current_position = at;
				actor.addTrait("infected");
				((BaseSimObject)actor).getHit((float)actor.getHealth() + 99999f, pFlash: true, AttackType.Other, (BaseSimObject)null, pSkipIfShake: false, pMetallicWeapon: false, pCheckDamageReduction: false);
				if (actor.isAlive() && !actor.isAlreadyTransformed())
				{
					ActionLibrary.turnIntoZombie(actor, actor.current_tile);
				}
			}
			catch (Exception ex)
			{
				Note("zombie: " + ex.Message);
			}
			Actor z = null;
			for (int f = 0; f < 60; f++)
			{
				if (z != null)
				{
					break;
				}
				yield return null;
				foreach (Actor unit2 in World.world.units)
				{
					try
					{
						if (unit2.isAlive() && unit2.asset != null && unit2.asset.unit_zombie && !before.Contains(unit2.data.id))
						{
							z = unit2;
							break;
						}
					}
					catch
					{
					}
				}
			}
			if (Check("a soldier raised as a zombie", z != null))
			{
				Hold(z, at);
				Look(at + new Vector2(0f, -4f), at);
				yield return Shown(10);
				yield return CivilTest.Wait(0.6f);
				People.LegsForTest(z, out var mesh, out var _, out var age);
				Check("the zombie wears nothing of his realm", None(z), S(z) + $" (drawn {age} frames ago as {mesh?.Model?.Name})");
				Release(z);
				Die(z);
			}
		}

		private static WorldTile LandNear(Vector2 p)
		{
			for (int i = 0; i <= 8; i++)
			{
				for (int j = -i; j <= i; j++)
				{
					for (int k = -i; k <= i; k++)
					{
						if (Mathf.Max(Mathf.Abs(k), Mathf.Abs(j)) == i)
						{
							WorldTile tile = World.world.GetTile((int)p.x + k, (int)p.y + j);
							if (tile != null && tile.main_type != null && !tile.main_type.liquid && !tile.main_type.block)
							{
								return tile;
							}
						}
					}
				}
			}
			return World.world.GetTile((int)p.x, (int)p.y);
		}

		private IEnumerator HelmsPart()
		{
			yield return EnsureRoles();
			if (!_rolesReady)
			{
				yield break;
			}
			Actor[] rulers = new Actor[3]
			{
				_k1.K.king,
				_k2.K.king,
				_k3.K.king
			};
			Kingdom[] realms = new Kingdom[3] { _k1.K, _k2.K, _k3.K };
			if (!Check("three rulers", rulers[0] != null && rulers[1] != null && rulers[2] != null))
			{
				yield break;
			}
			Vector2 at = _stage + new Vector2(0f, -1.6f);
			Vector2 eye = at + new Vector2(0f, -2.6f);
			Row(rulers, at, eye, 0.9f);
			Actor[] array = rulers;
			foreach (Actor a in array)
			{
				Face(a, eye);
			}
			Hold(_leader, at + new Vector2(2.6f, 0.8f));
			Hold(_seats[3], at + new Vector2(-2.6f, 0.8f));
			Look(eye, at, -2f, 1.05f);
			string[] helmetStates = HelmetStates;
			foreach (string h in helmetStates)
			{
				array = rulers;
				foreach (Actor a2 in array)
				{
					if (h == null)
					{
						TakeOff(a2, EquipmentType.Helmet);
					}
					else
					{
						Put(a2, h);
					}
				}
				yield return Refresh(rulers, 0.3f);
				Config.paused = true;
				yield return Frames(3);
				string text = ((h == null) ? "none" : h.Substring(7));
				bool flag = RegaliaRules.HighHelmet(h);
				for (int k = 0; k < 3; k++)
				{
					Actor a3 = rulers[k];
					string text2 = Letter(realms[k]);
					string text3 = People.HelmetLookForTest(a3);
					string text4 = ((h == null) ? ("crown:" + text2) : (flag ? ("crownhelm:" + text2) : ("crown:" + text2 + "+" + h)));
					string text5 = $"armour shown {People.ArmorShown(a3)}, helmet worn {HelmetOn(a3)}";
					bool flag2 = text3 == text4;
					if (h != null && !flag)
					{
						flag2 &= People.ArmorShown(a3) >= 1;
					}
					if (flag)
					{
						string text6 = HairShowing(a3, beard: false);
						text5 = text5 + ", hair showing '" + text6 + "'";
						flag2 &= HelmetOn(a3) && text6 == "";
					}
					Check((Fem(a3) ? "queen" : "king") + " of " + realms[k].name + " (" + text2 + ") in " + text + ": " + text4, flag2, text3 + "; " + text5 + (flag2 ? "" : ("; " + S(a3))));
				}
				Frame("rg_kingheads_" + text + ".png");
				Config.paused = false;
			}
			Put(_leader, "helmet_iron");
			Put(_seats[3], "helmet_iron");
			Face(_leader, eye + new Vector2(0f, -2f));
			Face(_seats[3], eye + new Vector2(0f, -2f));
			Look(eye + new Vector2(0f, -2f), at, -2f, 1.05f);
			yield return Refresh(new Actor[2]
			{
				_leader,
				_seats[3]
			}, 0.6f);
			Check("leader in an iron helmet: the hat shows in its place", People.HelmetLookForTest(_leader) == "hat", People.HelmetLookForTest(_leader) + "; " + S(_leader));
			Check("spymaster in an iron helmet: the hood shows in its place", People.HelmetLookForTest(_seats[3]) == "hood", People.HelmetLookForTest(_seats[3]) + "; " + S(_seats[3]));
			Frame("rg_hat_hood_helmet.png");
			TakeOff(_leader, EquipmentType.Helmet);
			TakeOff(_seats[3], EquipmentType.Helmet);
			array = rulers;
			for (int i = 0; i < array.Length; i++)
			{
				TakeOff(array[i], EquipmentType.Helmet);
			}
			KingdomStyle[] array2 = new KingdomStyle[3]
			{
				KingdomStyle.IronSpikes,
				KingdomStyle.GoldWings,
				KingdomStyle.GildedSpire
			};
			foreach (KingdomStyle kingdomStyle in array2)
			{
				MeshModel m = Models.Get(RegaliaKit.ModelName(RegaliaKit.Set.Royal, kingdomStyle, female: false, 0));
				MeshModel m2 = Models.Get(RegaliaKit.ModelName(RegaliaKit.Set.Royal, kingdomStyle, female: true, 0));
				float num = PartHeight(m, "crown");
				float num2 = PartHeight(m2, "crown");
				Check("the queen's " + RegaliaKit.StyleLetter(kingdomStyle) + " crown is about 1.22 x the king's height", num > 0f && Mathf.Abs(num2 / num - 1.22f) <= 0.13f, $"king {num:F3}, queen {num2:F3}: {((num > 0f) ? (num2 / num) : 0f):F2}x");
			}
			Config.paused = false;
			Release(_leader);
			Release(_seats[3]);
			array = rulers;
			foreach (Actor a4 in array)
			{
				Unface(a4);
			}
		}

		private SpriteImage OwnHead(Actor a, out int index)
		{
			index = -1;
			ActorTextureSubAsset textureAsset = a.getTextureAsset();
			if (textureAsset == null)
			{
				return null;
			}
			bool flag = Fem(a);
			index = a.data.head;
			if (index < 0)
			{
				Sprite[] array = ((!flag) ? a.animation_container?.heads_male : a.animation_container?.heads_female);
				if (array == null || array.Length == 0)
				{
					array = a.animation_container?.heads;
				}
				index = ((array != null && array.Length != 0) ? AnimationHelper.getSpriteIndex(a.data.id, array.Length) : 0);
			}
			Sprite head = ActorAnimationLoader.getHead(flag ? textureAsset.texture_heads_female : textureAsset.texture_heads_male, index);
			if (!(head != null))
			{
				return null;
			}
			return _heads.Get(head);
		}

		private uint OwnHair(Actor a)
		{
			try
			{
				int index;
				SpriteImage spriteImage = OwnHead(a, out index);
				People.PaletteForTest(a, out var p);
				return (spriteImage != null) ? RegaliaHair.FromHead(spriteImage, p.Skin) : 0u;
			}
			catch
			{
				return 0u;
			}
		}

		private string HeadColours(Actor a)
		{
			try
			{
				int index;
				SpriteImage spriteImage = OwnHead(a, out index);
				if (spriteImage?.Pixels == null)
				{
					return "(not readable yet)";
				}
				Dictionary<uint, int> dictionary = new Dictionary<uint, int>();
				uint[] pixels = spriteImage.Pixels;
				foreach (uint num in pixels)
				{
					if (num >> 24 >= 128)
					{
						dictionary.TryGetValue(num | 0xFF000000u, out var value);
						dictionary[num | 0xFF000000u] = value + 1;
					}
				}
				List<KeyValuePair<uint, int>> list = new List<KeyValuePair<uint, int>>(dictionary);
				list.Sort((KeyValuePair<uint, int> x, KeyValuePair<uint, int> y) => y.Value.CompareTo(x.Value));
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.Append(spriteImage.Width).Append('x').Append(spriteImage.Height)
					.Append(" head ")
					.Append(index)
					.Append(" (data.head ")
					.Append(a.data.head)
					.Append("):");
				for (int num2 = 0; num2 < list.Count && num2 < 8; num2++)
				{
					stringBuilder.Append(' ').Append(Hex(list[num2].Key)).Append(' ')
						.Append(list[num2].Value);
				}
				return stringBuilder.ToString();
			}
			catch (Exception ex)
			{
				return "(" + ex.Message + ")";
			}
		}

		private static float PartHeight(MeshModel m, string part)
		{
			int num = m?.PartIndex(part) ?? (-1);
			if (num < 0)
			{
				return 0f;
			}
			MeshPart meshPart = m.Parts[num];
			float num2 = float.MaxValue;
			float num3 = float.MinValue;
			for (int i = 0; i < meshPart.VertexCount; i++)
			{
				float b = meshPart.Pos[i * 3 + 2];
				num2 = Mathf.Min(num2, b);
				num3 = Mathf.Max(num3, b);
			}
			if (!(num3 > num2))
			{
				return 0f;
			}
			return num3 - num2;
		}

		private static float NearestGold(uint c)
		{
			float num = float.MaxValue;
			uint[] crownGolds = CrownGolds;
			foreach (uint b in crownGolds)
			{
				num = Mathf.Min(num, DeltaE(c, b));
			}
			return num;
		}

		private static float NearestRealm(uint c, Kingdom k)
		{
			float num = float.MaxValue;
			try
			{
				ColorAsset color = k.getColor();
				Color[] array = new Color[4]
				{
					color.getColorMain(),
					color.getColorMainSecond(),
					color.getColorBanner(),
					color.getColorText()
				};
				for (int i = 0; i < array.Length; i++)
				{
					Color color2 = array[i];
					num = Mathf.Min(num, DeltaE(c, TerrainCache.Rgb((int)(color2.r * 255f), (int)(color2.g * 255f), (int)(color2.b * 255f))));
				}
			}
			catch
			{
			}
			return num;
		}

		private IEnumerator HairPart()
		{
			Kingdom k3 = _k3.K;
			Actor old = k3.king;
			Actor m = Newcomer(_k3, female: false, _stage);
			if (!Check("a K3 man to crown", m != null && old != null))
			{
				yield break;
			}
			Vector2 at = _stage + new Vector2(0f, -1.6f);
			Hold(m, at);
			Hold(old, at + new Vector2(1.2f, 0f));
			Face(m, at + new Vector2(0.6f, -2.8f));
			Face(old, at + new Vector2(0.6f, -2.8f));
			Look(at + new Vector2(0.6f, -2.8f), at, -2f, 1.05f);
			Config.paused = false;
			yield return Shown(20);
			yield return CivilTest.Wait(1.5f);
			if (!People.PaletteForTest(m, out var p0))
			{
				Check("the man drawn before his crowning", ok: false);
				yield break;
			}
			uint h0 = p0.Hair;
			Frame("rg_hair_before.png");
			Note($"before the crown: hair {Hex(p0.Hair)}, skin {Hex(p0.Skin)}, eyes {Hex(p0.Eyes)}, long {p0.LongHair}, beard {p0.Beard}; his own head sprite: {HeadColours(m)}");
			try
			{
				k3.setKing(m);
			}
			catch (Exception ex)
			{
				Note("crown: " + ex.Message);
			}
			yield return CivilTest.Wait(1.5f);
			SpriteImage spriteImage = Mod.SpriteForTest(m);
			uint c = ((spriteImage != null) ? Humanoid.Extract(spriteImage, "human", p0.Skin).Hair : 0u);
			float num = NearestGold(c);
			float num2 = NearestRealm(c, k3);
			Check("the artefact is there: his crowned sprite reads as gold or realm-coloured hair", spriteImage != null && (num < 12f || num2 < 12f), $"read {Hex(c)}: dE {num:F1} to crown gold, {num2:F1} to the realm's colours; commoner hair {Hex(h0)}");
			People.ForgetLookForTest(m);
			People.RecheckForTest(m);
			yield return Shown(20);
			yield return CivilTest.Wait(2f);
			uint fixedHair = People.HairFixedForTest(m);
			uint num3 = OwnHair(m);
			Check("crowned, his hair is read from his own plain head (not the crown)", m.isKing() && fixedHair != 0 && num3 != 0 && DeltaE(fixedHair, num3) < 6f, "fixed " + Hex(fixedHair) + ", his head sprite's " + Hex(num3));
			Check("crowned, his hair matches his commoner look (dE < 6)", fixedHair != 0 && DeltaE(fixedHair, h0) < 6f, $"fixed {Hex(fixedHair)}, as a commoner {Hex(h0)}: dE {DeltaE(fixedHair, h0):F1} (the commoner's is read off his whole sprite, Humanoid.Extract)");
			Check("...not crown gold", fixedHair != 0 && NearestGold(fixedHair) > 8f, $"dE to gold {NearestGold(fixedHair):F1}");
			string[] list = ShownOf(old);
			Check("the deposed old king wears nothing royal", !HasAny(list, "royal:"), S(old));
			Frame("rg_hair_crowned.png");
			try
			{
				k3.setKing(old);
			}
			catch (Exception ex2)
			{
				Note("crown back: " + ex2.Message);
			}
			yield return Refresh(new Actor[2] { m, old }, 1.5f);
			Check("deposed, he keeps his own hair", People.HairFixedForTest(m) == fixedHair, Hex(People.HairFixedForTest(m)));
			Check("deposed, he wears no mantle or crown", !HasAny(ShownOf(m), "royal:"), S(m));
			Check("the old king wears his crown again", Has(ShownOf(old), "royal:crown"), S(old));
			Release(m);
			Release(old);
			Actor founder = _k4.K.king;
			if (founder != null && founder.isAlive())
			{
				Hold(founder, at);
				Face(founder, at + new Vector2(0.6f, -2.8f));
				People.ForgetLookForTest(founder);
				yield return Refresh(new Actor[1] { founder }, 2f);
				uint num4 = People.HairFixedForTest(founder);
				uint num5 = OwnHair(founder);
				Note("K4's founding king's own head sprite: " + HeadColours(founder));
				Check("a human king crowned at the founding: his own hair within 2 s", num4 != 0 && num5 != 0 && DeltaE(num4, num5) < 6f, $"fixed {Hex(num4)} (0: not yet), own head's {Hex(num5)}, data.head {founder.data.head}");
				Frame("rg_hair_founder.png");
				Release(founder);
			}
			string[] array = new string[2] { "elf", "dwarf" };
			foreach (string race in array)
			{
				Realm r = Found(race, race, 6, 0, null, 0f);
				if (r == null)
				{
					Note("no " + race + " realm founded: not checked");
					continue;
				}
				foreach (Actor person in r.People)
				{
					Invincible(person);
				}
				Actor king = r.K.king;
				Actor soldier = r.People.Find((Actor a) => a != null && a.isAlive() && a.isWarrior() && a != king);
				Hold(king, at);
				Face(king, at + new Vector2(0.6f, -2.8f));
				if (soldier != null)
				{
					Hold(soldier, at + new Vector2(1.2f, 0f));
				}
				People.ForgetLookForTest(king);
				yield return Refresh(new Actor[2] { king, soldier }, 2f);
				uint num6 = People.HairFixedForTest(king);
				uint num7 = OwnHair(king);
				Note(race + " king's own head sprite: " + HeadColours(king));
				Check(race + " king (crowned at the founding): his own hair within 2 s, not crown gold or his realm's colour", num6 != 0 && NearestGold(num6) > 8f && NearestRealm(num6, r.K) > 8f && (num7 == 0 || DeltaE(num6, num7) < 6f), $"fixed {Hex(num6)} (0: not yet), own head's {Hex(num7)}: dE {NearestGold(num6):F1} to gold, {NearestRealm(num6, r.K):F1} to the realm");
				if (soldier != null)
				{
					Check(race + " soldier wears nothing (humans only)", None(soldier), S(soldier));
				}
				Frame("rg_hair_" + race + ".png");
				Release(king);
				Release(soldier);
			}
		}

		private IEnumerator TitlesPart()
		{
			yield return EnsureRoles();
			if (!_rolesReady)
			{
				yield break;
			}
			Actor you = _photographer;
			string realm = _k1.K.name;
			string town = _k1.Town.name;
			foreach (Actor person in _k1.People)
			{
				Regalia.RoleOf(person);
			}
			yield return CivilTest.Wait(0.5f);
			Title("heir", _heir, "Heir of " + realm);
			Title("marshal", _seats[0], "Marshal of " + realm);
			Title("steward", _seats[1], "Steward of " + realm);
			Title("chancellor", _seats[2], "Chancellor of " + realm);
			Title("court mage", _seats[4], "Court mage of " + realm);
			Title("champion", _champion, "Champion of " + realm);
			Title("captain", _captain, "Captain of " + _captain.city.name + "'s army");
			Actor guard = CurrentGuard();
			if (guard != null)
			{
				Title("guard", guard, "Guard of " + town);
			}
			else
			{
				Check("a K1 soldier at home to call a guard", ok: false);
			}
			Title("king", _k1.K.king, (Fem(_k1.K.king) ? "Queen of " : "King of ") + realm);
			Title("leader", _leader, "Leader of " + town);
			Actor npc = _seats[3];
			string text = Dialogue.RelationTo(npc, you);
			Check("the spymaster goes unnamed to one who doesn't know him", PeopleMet.Knows(you, npc) || !text.StartsWith("Spymaster", StringComparison.Ordinal), text);
			PeopleMet.Note(you, npc);
			Title("spymaster, once met", npc, "Spymaster of " + realm);
			if (_k2Soldier != null)
			{
				Title("K2 guard", _k2Soldier, "Guard of " + _k2Soldier.city.name);
				Royal.RegisterTask();
				Release(_k2Soldier);
				try
				{
					_k2Soldier.cancelAllBeh();
					_k2Soldier.setTask("fp_royal_march");
					_k2Soldier.beh_tile_target = LandNear(_k2Soldier.current_position + new Vector2(20f, 0f));
				}
				catch
				{
				}
				yield return CivilTest.Wait(1.2f);
				Regalia.RoleOf(_k2Soldier, forFigure: true);
				Title("K2 soldier on the march", _k2Soldier, "Soldier of " + _k2.K.name);
				try
				{
					_k2Soldier.cancelAllBeh();
				}
				catch
				{
				}
			}
			long parent_id_ = _coif.data.parent_id_1;
			_coif.data.parent_id_1 = you.data.id;
			Title("a guard who is your child", _coif, Fem(_coif) ? "your daughter" : "your son");
			_coif.data.parent_id_1 = parent_id_;
			if (_orc != null)
			{
				foreach (Actor person2 in _orc.People)
				{
					Regalia.RoleOf(person2);
				}
				yield return CivilTest.Wait(1f);
				Actor orcCaptain = PlainSoldier(_orc);
				Actor orcSoldier = PlainSoldier(_orc, orcCaptain);
				if (orcCaptain != null && MakeCaptain(orcCaptain))
				{
					Mod.Royal.DeclareWar(_k1.K.king, _orc.K, delegate(string m)
					{
						Note("toast: " + m);
					}, delegate
					{
					});
					yield return CivilTest.Wait(4f);
					Regalia.RoleOf(orcCaptain, forFigure: true);
					string text2 = Regalia.TitleFor(orcCaptain, you, high: true);
					string text3 = Dialogue.RelationTo(orcCaptain, you);
					Check("an enemy captain keeps his title (not 'an enemy')", text2 != null && text3 == text2, $"{text3} (title {text2}) [{Regalia.RoleOf(orcCaptain).Bits}]");
					if (orcSoldier != null)
					{
						Regalia.RoleOf(orcSoldier, forFigure: true);
						Title("an enemy soldier", orcSoldier, "an enemy");
					}
					EndWars(_k1.K);
				}
				else
				{
					Note("no orc captain: the enemy title not checked");
				}
			}
			LookAway();
			Release(you);
			Hold(_seats[0], you.current_position + new Vector2(Mathf.Cos(Mod.ViewYaw), Mathf.Sin(Mod.ViewYaw)) * 1.4f);
			yield return CivilTest.Wait(0.5f);
			if (Check("a talk with the marshal", Mod.StartConversationForTest(_seats[0])))
			{
				yield return CivilTest.Wait(1.2f);
				Check("the talk card names the marshal", Mod.Converse.Relation != null && Mod.Converse.Relation.StartsWith("Marshal of " + realm, StringComparison.Ordinal), Mod.Converse.Relation);
				yield return Capture("rg_talk_marshal.png", 0.2f);
				Mod.EndConversation();
				yield return CivilTest.Wait(0.5f);
			}
			Release(_seats[0]);
			if (guard != null)
			{
				Release(guard);
				yield return Possess(guard);
				if (InControl(guard))
				{
					yield return CivilTest.Wait(1f);
					string text4 = Hud.RoleLine(guard);
					Check("your nameplate as a guard: 'Guard of T  ·  R'", text4 == "Guard of " + guard.city.name + "  ·  " + realm, text4);
					yield return Capture("rg_nameplate_guard.png");
				}
				yield return Possess(_photographer);
				Hold(_photographer, _stage + new Vector2(0f, -9f));
				yield return Korean(realm, town);
			}
			void Title(string who, Actor actor, string want)
			{
				Check(who + ": '" + want + "'", Dialogue.RelationTo(actor, you) == want, Dialogue.RelationTo(actor, you) + " [" + Regalia.RoleOf(actor).Bits.ToString() + "]");
			}
		}

		private IEnumerator Korean(string realm, string town)
		{
			Settings s = Mod.Settings;
			string was = s.Language;
			s.Language = "ko";
			yield return Frames(3);
			Lang.Tick(s.Language);
			if (Lang.Code != "ko")
			{
				Note("Korean could not be chosen (" + Lang.Code + "): not checked");
				s.Language = was;
				yield break;
			}
			List<KeyValuePair<string, string[]>> list = new List<KeyValuePair<string, string[]>>();
			list.Add(new KeyValuePair<string, string[]>("Heir of {0}", new string[1] { realm }));
			list.Add(new KeyValuePair<string, string[]>("Marshal of {0}", new string[1] { realm }));
			list.Add(new KeyValuePair<string, string[]>("Steward of {0}", new string[1] { realm }));
			list.Add(new KeyValuePair<string, string[]>("Chancellor of {0}", new string[1] { realm }));
			list.Add(new KeyValuePair<string, string[]>("Spymaster of {0}", new string[1] { realm }));
			list.Add(new KeyValuePair<string, string[]>("Court mage of {0}", new string[1] { realm }));
			list.Add(new KeyValuePair<string, string[]>("Captain of {0}'s army", new string[1] { town }));
			list.Add(new KeyValuePair<string, string[]>("Guard of {0}", new string[1] { town }));
			list.Add(new KeyValuePair<string, string[]>("Soldier of {0}", new string[1] { realm }));
			list.Add(new KeyValuePair<string, string[]>("Champion of {0}", new string[1] { realm }));
			list.Add(new KeyValuePair<string, string[]>("Heir of {0}  ·  {1}", new string[2] { realm, town }));
			list.Add(new KeyValuePair<string, string[]>("Guard of {0}  ·  {1}", new string[2] { town, realm }));
			list.Add(new KeyValuePair<string, string[]>("Captain of {0}'s army  ·  {1}", new string[2] { town, realm }));
			list.Add(new KeyValuePair<string, string[]>("Soldier of {0}  ·  {1}", new string[2] { realm, town }));
			List<KeyValuePair<string, string[]>> list2 = list;
			int num = 0;
			foreach (KeyValuePair<string, string[]> item in list2)
			{
				string text = item.Key.Replace("{0}", item.Value[0]);
				if (item.Value.Length > 1)
				{
					text = text.Replace("{1}", item.Value[1]);
				}
				if (Lang.Template(item.Key) == item.Key)
				{
					num++;
					continue;
				}
				string text2 = Lang.T(text);
				string text3 = Lang.Format(item.Key, item.Value);
				Check("ko '" + item.Key + "' as drawn = its template filled", text2 == text3 && text2 != text, "'" + text2 + "' want '" + text3 + "'");
			}
			if (num > 0)
			{
				Note($"ko: {num} of {list2.Count} title templates have no Korean line yet (W18): not checked");
			}
			s.Language = was;
			yield return Frames(2);
			Lang.Tick(s.Language);
		}

		private IEnumerator RangePart()
		{
			yield return EnsureRoles();
			if (!_rolesReady)
			{
				yield break;
			}
			Mod.FixDetailForTest(on: true);
			Note($"creature reach {Mod.SceneForTest.CreatureRange:F0}, detail {Mod.SceneForTest.Detail:F2}, LOD zoom {Models.LodZoom:F2}");
			Vector2 fwd = ClearHeading(_stage, 160f);
			Vector2 right = new Vector2(fwd.y, 0f - fwd.x);
			Vector2 eye = _stage - fwd * 4f;
			ClearCorridor(eye, fwd, 160f, 4);
			Actor king = _k1.K.king;
			Actor helm = _helmeted[2];
			Actor bare = _coif;
			Actor[] four = new Actor[4] { _captain, helm, bare, king };
			float[] gateDistances = GateDistances;
			foreach (float d in gateDistances)
			{
				Vector2 vector = eye + fwd * d;
				for (int j = 0; j < four.Length; j++)
				{
					WorldTile worldTile = LandNear(vector + right * (((float)j - 1.5f) * 1.6f));
					Hold(four[j], new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f));
				}
				Look(eye, vector, (d > 60f) ? (-1f) : (-3f), 2.5f);
				yield return Refresh(four, 1.2f);
				Config.paused = true;
				yield return Frames(3);
				string arg = ((d < 11f) ? "" : ((d < 30f) ? "_m" : "_f"));
				string text = People.RegaliaModelForTest(helm, RegaliaKit.Set.Soldier);
				string text2 = People.RegaliaModelForTest(king, RegaliaKit.Set.Royal);
				string[] list = ShownOf(_captain);
				string[] list2 = ShownOf(helm);
				string[] list3 = ShownOf(king);
				string detail = "soldier " + text + ", royal " + text2 + "; captain " + S(_captain) + "; helmeted " + S(helm) + "; bare " + S(bare) + "; king " + S(king);
				if (d < 60f)
				{
					Check($"at {d:F0} tiles: the sets follow the body's LOD ('{arg}')", text == RegaliaKit.ModelName(RegaliaKit.Set.Soldier, KingdomStyle.None, Fem(helm), (!(d < 11f)) ? ((d < 30f) ? 1 : 2) : 0) && text2 == RegaliaKit.ModelName(RegaliaKit.Set.Royal, Regalia.LookOf(_k1.K).Style, Fem(king), (!(d < 11f)) ? ((d < 30f) ? 1 : 2) : 0), detail);
					Check($"at {d:F0} tiles: sashes and coif drawn", Has(list2, "soldier:sash") && Has(ShownOf(bare), "soldier:coif"), detail);
				}
				else
				{
					Check($"at {d:F0} tiles: no soldier's set past 60", text == null && !HasAny(ShownOf(bare), "soldier:"), detail);
				}
				if (d > 40f && d < 60f)
				{
					Check($"at {d:F0} tiles: the plume on a cap in the helmet's colour", Has(list2, "soldier:helmcap") && Has(list2, "soldier:plume"), detail);
				}
				People.LegsForTest(king, out var mesh, out var rig, out var age);
				People.LegsForTest(_captain, out mesh, out rig, out var age2);
				if (d < 120f)
				{
					Check($"at {d:F0} tiles: the captain's banner and the king's mantle", Has(list, "captain:banner") && Has(list3, "royal:mantle"), detail);
				}
				else if (age <= 3 && age2 <= 3)
				{
					Check($"at {d:F0} tiles: no banner or mantle past 120", !Has(list, "captain:banner") && !Has(list3, "royal:mantle"), detail);
				}
				else
				{
					Note($"at {d:F0} tiles the king and captain weren't drawn at all (ages {age}/{age2}): the 120-tile cut not checked");
				}
				Frame($"rg_gate_{d:F0}.png");
				Config.paused = false;
			}
			Actor[] array = four;
			foreach (Actor a in array)
			{
				Release(a);
			}
			yield return Pixels(eye, fwd, right);
			Mod.FixDetailForTest(on: false);
		}

		private static Vector2 ClearHeading(Vector2 from, float length)
		{
			Vector2 result = Vector2.up;
			float num = float.MinValue;
			for (int i = 0; i < 24; i++)
			{
				float f = (float)i * MathF.PI / 12f;
				Vector2 vector = new Vector2(Mathf.Cos(f), Mathf.Sin(f));
				Vector2 vector2 = new Vector2(0f - vector.y, vector.x);
				int num2 = 0;
				int num3 = 0;
				int num4 = int.MaxValue;
				int num5 = int.MinValue;
				bool flag = false;
				for (float num6 = -4f; num6 <= length; num6 += 1f)
				{
					if (flag)
					{
						break;
					}
					for (float num7 = -2f; num7 <= 2f; num7 += 1f)
					{
						Vector2 vector3 = from + vector * num6 + vector2 * num7;
						WorldTile tile = World.world.GetTile((int)vector3.x, (int)vector3.y);
						if (tile == null || tile.main_type == null)
						{
							flag = true;
							break;
						}
						if (num7 != 0f)
						{
							if (tile.building != null && AssetStyle.IsSolid(tile.building))
							{
								num3++;
							}
							continue;
						}
						if (!tile.main_type.liquid && !tile.main_type.block)
						{
							num2++;
						}
						num4 = Mathf.Min(num4, tile.main_type.height_min);
						num5 = Mathf.Max(num5, tile.main_type.height_min);
					}
				}
				float num8 = (flag ? (-10000f) : 0f) + (float)num2 * 4f - (float)num3 - (float)(num5 - num4) * 0.5f;
				if (num8 > num)
				{
					num = num8;
					result = vector;
				}
			}
			return result;
		}

		private static void ClearCorridor(Vector2 from, Vector2 f, float length, int half)
		{
			Vector2 vector = new Vector2(0f - f.y, f.x);
			for (float num = 0f; num <= length; num += 1f)
			{
				for (int i = -half; i <= half; i++)
				{
					Vector2 vector2 = from + f * num + vector * i;
					CivilTest.ClearBox((int)vector2.x, (int)vector2.y, (int)vector2.x, (int)vector2.y);
				}
			}
		}

		private IEnumerator Pixels(Vector2 eye, Vector2 fwd, Vector2 right)
		{
			List<Actor> blockA = new List<Actor>();
			List<Actor> blockB = new List<Actor>();
			for (int i = 0; i < 12; i++)
			{
				Actor actor = Recruit(_k1, _stage, (i % 2 == 0) ? "helmet_iron" : null);
				Actor actor2 = Recruit(_k2, _stage, (i % 2 == 0) ? "helmet_iron" : null);
				if (actor != null)
				{
					blockA.Add(actor);
				}
				if (actor2 != null)
				{
					blockB.Add(actor2);
				}
			}
			Actor actor3 = Recruit(_k2, _stage, "helmet_iron");
			if (actor3 != null)
			{
				MakeCaptain(actor3);
				blockB.Add(actor3);
			}
			blockA.Add(_captain);
			Note($"blocks of {blockA.Count} (K1) and {blockB.Count} (K2) soldiers, captains {_captain.isArmyGroupLeader()}/{actor3?.isArmyGroupLeader()}");
			List<Actor> parked = Park(eye, fwd, right, blockA, blockB);
			bool halberds = People.GuardHalberds;
			People.GuardHalberds = false;
			ColorAsset was1 = null;
			ColorAsset was2 = null;
			try
			{
				was1 = _k1.K.getColor();
				was2 = _k2.K.getColor();
			}
			catch
			{
			}
			List<string[]> list = new List<string[]>();
			list.Add(new string[2] { "blue", "navy" });
			list.Add(new string[2] { "teal", "cyan" });
			List<string[]> list2 = list;
			string[] array = WorstPair();
			if (array != null)
			{
				list2.Add(array);
			}
			Settings s = Mod.Settings;
			int palette = s.Palette;
			List<KeyValuePair<string[], int>> list3 = new List<KeyValuePair<string[], int>>();
			foreach (string[] item in list2)
			{
				list3.Add(new KeyValuePair<string[], int>(item, 1));
			}
			list3.Add(new KeyValuePair<string[], int>(list2[0], 0));
			foreach (KeyValuePair<string[], int> item2 in list3)
			{
				string[] key = item2.Key;
				s.Palette = item2.Value;
				ColorAsset colorAsset = AssetManager.kingdom_colors_library.get(key[0]);
				ColorAsset colorAsset2 = AssetManager.kingdom_colors_library.get(key[1]);
				if (colorAsset == null || colorAsset2 == null)
				{
					Note("no colours " + key[0] + "/" + key[1]);
					continue;
				}
				try
				{
					_k1.K.updateColor(colorAsset);
					_k2.K.updateColor(colorAsset2);
				}
				catch (Exception ex)
				{
					Note("recolour: " + ex.Message);
				}
				string tag = key[0] + "_" + key[1] + ((item2.Value == 0) ? "_p0" : "");
				Vector3 refA = Vector3.zero;
				Vector3 refB = Vector3.zero;
				bool haveRef = false;
				float[] photoDistances = PhotoDistances;
				for (int j = 0; j < photoDistances.Length; j++)
				{
					float d = photoDistances[j];
					Vector2 vector = eye + fwd * (d + 4f);
					Formation(blockA, vector - right * 3.5f, fwd, right);
					Formation(blockB, vector + right * 3.5f, fwd, right);
					float num = 2.4f + d * 0.02f;
					float num2 = Mod.TerrainForTest.SampleHeight(eye.x, eye.y) + num;
					float num3 = Mod.TerrainForTest.SampleHeight(vector.x, vector.y) + 0.6f;
					Look(eye, vector, (0f - Mathf.Atan2(num2 - num3, d + 4f)) * 57.29578f, num);
					List<Actor> list4 = new List<Actor>(blockA);
					list4.AddRange(blockB);
					yield return Refresh(list4, 3.6f);
					Config.paused = true;
					People.RegaliaOn = false;
					yield return Frames(5);
					int width;
					int height;
					Color32[] noise = Grab(out width, out height);
					yield return Frames(3);
					int w;
					int h;
					Color32[] off = Grab(out w, out h);
					Frame($"rg_range_{d:F0}_{tag}_off.png");
					People.RegaliaOn = true;
					yield return Frames(5);
					int width2;
					int height2;
					Color32[] array2 = Grab(out width2, out height2);
					Frame($"rg_range_{d:F0}_{tag}_on.png");
					Config.paused = false;
					if (off == null || array2 == null || noise == null || w != width2 || h != height2 || noise.Length != off.Length)
					{
						Note("no frames to compare at " + d);
						continue;
					}
					Changed(blockA, noise, off, array2, w, h, out var count, out var mean);
					Changed(blockB, noise, off, array2, w, h, out var count2, out var mean2);
					if (!haveRef)
					{
						refA = mean;
						refB = mean2;
						haveRef = count > 0 && count2 > 0;
					}
					float num4 = ((d <= 30f) ? (2f * (float)blockA.Count) : 6f);
					bool flag = (mean - refA).sqrMagnitude <= (mean - refB).sqrMagnitude;
					bool flag2 = (mean2 - refB).sqrMagnitude <= (mean2 - refA).sqrMagnitude;
					string text = $"K1 {count} px mean {V(mean)} (own ref {V(refA)}), K2 {count2} px mean {V(mean2)} (own ref {V(refB)}), need {num4:F0}";
					Check($"range px {tag} at {d:F0}: each block gains pixels from its regalia", (float)count >= num4 && (float)count2 >= num4, text);
					if (d > 12f && d + 4f < 60f)
					{
						Check($"range px {tag} at {d:F0}: each block's gain reads as its own realm", flag && flag2, text);
					}
					else if (d > 12f)
					{
						Note($"range px {tag} at {d:F0} (banners only): reads as its own realm {flag}/{flag2}; {text}");
					}
				}
			}
			People.GuardHalberds = halberds;
			s.Palette = palette;
			try
			{
				if (was1 != null)
				{
					_k1.K.updateColor(was1);
				}
				if (was2 != null)
				{
					_k2.K.updateColor(was2);
				}
			}
			catch
			{
			}
			foreach (Actor item3 in blockA)
			{
				if (item3 != _captain)
				{
					Release(item3);
					Die(item3);
				}
			}
			foreach (Actor item4 in blockB)
			{
				Release(item4);
				Die(item4);
			}
			foreach (Actor item5 in parked)
			{
				Release(item5);
			}
			Release(_captain);
		}

		private static string V(Vector3 c)
		{
			return $"({c.x:F0},{c.y:F0},{c.z:F0})";
		}

		private List<Actor> Park(Vector2 eye, Vector2 fwd, Vector2 right, List<Actor> a, List<Actor> b)
		{
			List<Actor> list = new List<Actor>();
			HashSet<Actor> hashSet = new HashSet<Actor>(a);
			hashSet.UnionWith(b);
			hashSet.Add(_photographer);
			Vector2 vector = eye - right * 26f;
			foreach (Actor unit in World.world.units)
			{
				try
				{
					if (unit != null && unit.isAlive() && !hashSet.Contains(unit) && unit.asset != null && !unit.asset.is_boat)
					{
						Vector2 lhs = unit.current_position - eye;
						float num = Vector2.Dot(lhs, fwd);
						float f = Vector2.Dot(lhs, right);
						if (!(num < -6f) && !(num > 130f) && !(Mathf.Abs(f) > 12f))
						{
							int count = list.Count;
							WorldTile worldTile = LandNear(vector + fwd * ((float)(count / 8) * 1.3f) - right * ((float)(count % 8) * 1.3f));
							Hold(unit, (worldTile != null) ? new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f) : vector);
							list.Add(unit);
						}
					}
				}
				catch
				{
				}
			}
			Note(list.Count + " bystanders held off the photo line");
			return list;
		}

		private void Formation(List<Actor> block, Vector2 middle, Vector2 fwd, Vector2 right)
		{
			for (int i = 0; i < block.Count; i++)
			{
				int num = i / 4;
				int num2 = i % 4;
				Vector2 vector = middle + right * (((float)num2 - 1.5f) * 1.2f) + fwd * ((float)num * 1.2f);
				WorldTile worldTile = LandNear(vector);
				Hold(block[i], (worldTile != null) ? new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f) : vector);
			}
		}

		private void Changed(List<Actor> block, Color32[] noise, Color32[] off, Color32[] on, int w, int h, out int count, out Vector3 mean)
		{
			count = 0;
			mean = Vector3.zero;
			FrameProjection shown = Mod.Shown;
			float num = float.MaxValue;
			float num2 = float.MinValue;
			float num3 = float.MaxValue;
			float num4 = float.MinValue;
			foreach (Actor item in block)
			{
				if (item == null || !item.isAlive())
				{
					continue;
				}
				Vector2 current_position = item.current_position;
				float num5 = Mod.TerrainForTest.SampleHeight(current_position.x, current_position.y);
				float[] array = new float[2] { 0f, 1.5f };
				foreach (float num6 in array)
				{
					if (shown.Project(current_position.x, current_position.y, num5 + num6, out var col, out var row, out var depth) && depth > 0f)
					{
						num = Mathf.Min(num, col);
						num2 = Mathf.Max(num2, col);
						num3 = Mathf.Min(num3, row);
						num4 = Mathf.Max(num4, row);
					}
				}
			}
			if (num > num2 || shown.Width <= 0)
			{
				return;
			}
			float num7 = (float)w / (float)shown.Width;
			float num8 = (float)h / (float)shown.Height;
			int num9 = Mathf.Clamp((int)((num - 2f) * num7), 0, w - 1);
			int num10 = Mathf.Clamp((int)((num2 + 2f) * num7), 0, w - 1);
			int num11 = Mathf.Clamp((int)((num3 - 2f) * num8), 0, h - 1);
			int num12 = Mathf.Clamp((int)((num4 + 2f) * num8), 0, h - 1);
			for (int j = num11; j <= num12; j++)
			{
				for (int k = num9; k <= num10; k++)
				{
					int num13 = (h - 1 - j) * w + k;
					Color32 color = off[num13];
					Color32 color2 = on[num13];
					Color32 color3 = noise[num13];
					if (Mathf.Abs(color.r - color3.r) <= 8 && Mathf.Abs(color.g - color3.g) <= 8 && Mathf.Abs(color.b - color3.b) <= 8 && (Mathf.Abs(color.r - color2.r) > 8 || Mathf.Abs(color.g - color2.g) > 8 || Mathf.Abs(color.b - color2.b) > 8))
					{
						count++;
						mean += new Vector3((int)color2.r, (int)color2.g, (int)color2.b);
					}
				}
			}
			if (count > 0)
			{
				mean /= (float)count;
			}
		}

		private static string[] WorstPair()
		{
			List<ColorAsset> list = null;
			try
			{
				list = AssetManager.kingdom_colors_library.list;
			}
			catch
			{
			}
			if (list == null || list.Count < 2)
			{
				return null;
			}
			List<RegaliaColors> list2 = new List<RegaliaColors>();
			foreach (ColorAsset item in list)
			{
				uint main = Pack(item.getColorMain());
				uint second = Pack(item.getColorMainSecond());
				uint banner = Pack(item.getColorBanner());
				list2.Add(RegaliaColors.From(main, second, banner));
			}
			float num = float.MaxValue;
			string[] array = null;
			for (int i = 0; i < list.Count; i++)
			{
				for (int j = i + 1; j < list.Count; j++)
				{
					float num2 = Mathf.Max(DeltaE(list2[i].Main, list2[j].Main), Mathf.Max(DeltaE(list2[i].Second, list2[j].Second), DeltaE(list2[i].Emblem, list2[j].Emblem)));
					if (num2 < num)
					{
						num = num2;
						array = new string[2]
						{
							list[i].id,
							list[j].id
						};
					}
				}
			}
			Log.Info($"RG the closest regalia colour pair: {((array != null) ? array[0] : null)} / {((array != null) ? array[1] : null)} (dE {num:F1} at the farthest slot)");
			return array;
		}

		private static uint Pack(Color c)
		{
			return TerrainCache.Rgb(Mathf.Clamp((int)(c.r * 255f + 0.5f), 0, 255), Mathf.Clamp((int)(c.g * 255f + 0.5f), 0, 255), Mathf.Clamp((int)(c.b * 255f + 0.5f), 0, 255));
		}

		private IEnumerator SwayPart()
		{
			yield return EnsureRoles();
			if (!_rolesReady)
			{
				yield break;
			}
			Actor king = _k1.K.king;
			TakeOff(king, EquipmentType.Helmet);
			Vector2 start = _stage + new Vector2(-7f, -4f);
			Vector2 dir = Vector2.right;
			float worstClear = float.PositiveInfinity;
			int cleared = 0;
			float[] amp = new float[3];
			float[] lo = new float[3] { 9f, 9f, 9f };
			float[] hi = new float[3] { -9f, -9f, -9f };
			int[] n = new int[3];
			float[] speeds = new float[3] { 0f, 1.1f, 2.8f };
			float[] seconds = new float[3] { 2f, 5f, 4f };
			Vector2 at = start;
			Config.paused = false;
			Hold(king, at);
			Look(at + new Vector2(0f, -5f), at, -4f);
			yield return Refresh(new Actor[1] { king }, 1f);
			string standing = null;
			for (int phase = 0; phase < 3; phase++)
			{
				if (phase == 2)
				{
					at = start;
				}
				for (float t = 0f; t < seconds[phase]; t += Time.unscaledDeltaTime)
				{
					at += dir * speeds[phase] * Time.unscaledDeltaTime;
					Hold(king, at);
					Look(at + new Vector2(0f, -5f), at, -4f);
					yield return null;
					if (!(t < 0.6f))
					{
						float num = People.MantleClearForTest(king);
						if (!float.IsPositiveInfinity(num) && phase > 0)
						{
							worstClear = Mathf.Min(worstClear, num);
							cleared++;
						}
						RegaliaDress.Wearer wearer = WearerOf(king);
						if (wearer != null && Has(ShownOf(king), "royal:mantle"))
						{
							float a = wearer.Upper.A;
							amp[phase] += a;
							n[phase]++;
							lo[phase] = Mathf.Min(lo[phase], a);
							hi[phase] = Mathf.Max(hi[phase], a);
						}
					}
				}
				if (phase == 0)
				{
					standing = S(king) + "; " + Busy(king);
				}
				if (phase == 1)
				{
					Frame("rg_sway_walk.png");
				}
				if (phase == 2)
				{
					Frame("rg_sway_run.png");
				}
			}
			for (int i = 0; i < 3; i++)
			{
				if (n[i] > 0)
				{
					amp[i] /= n[i];
				}
			}
			string text = $"mantle angle standing {amp[0]:F3} ({lo[0]:F3}..{hi[0]:F3}), walking {amp[1]:F3} ({lo[1]:F3}..{hi[1]:F3}), running {amp[2]:F3} ({lo[2]:F3}..{hi[2]:F3}) rad";
			Check("walking and running, the mantle clears his legs (every frame >= -5 mm)", cleared > 20 && worstClear >= -0.005f, $"least {worstClear * 1000f:F1} mm over {cleared} frames");
			Check("the mantle swings out more as he goes", n[0] > 0 && n[1] > 0 && n[2] > 0 && amp[1] > amp[0] + 0.01f && amp[2] > amp[0] + 0.02f, text + "; standing: " + standing);
			Note("god view flutter: not checked here (the god view's wind is synthetic)");
			Release(king);
		}

		private IEnumerator CorpsePart()
		{
			Realm r = _k4;
			Actor king = r.K.king;
			foreach (Actor person in r.People)
			{
				Regalia.RoleOf(person);
			}
			yield return CivilTest.Wait(1.5f);
			Actor cap = PlainSoldier(r);
			Actor guard = PlainSoldier(r, cap);
			if (!Check("K4's king and two soldiers", king != null && cap != null && guard != null))
			{
				yield break;
			}
			Put(cap, "helmet_iron");
			MakeCaptain(cap);
			Put(guard, "sword_iron");
			Vector2 vector = _stage + new Vector2(0f, -1.6f);
			Hold(king, vector);
			Hold(cap, vector + new Vector2(1.4f, 0.3f));
			Hold(guard, vector + new Vector2(-1.4f, 0.3f));
			Look(vector + new Vector2(0f, -3.2f), vector, -14f);
			yield return Refresh(new Actor[3] { king, cap, guard }, 1.5f);
			string[] list = ShownOf(king);
			string[] list2 = ShownOf(cap);
			Check("alive: the king in crown and mantle, the captain with his banner, the guard his halberd", Has(list, "royal:crown") && Has(list, "royal:mantle") && Has(list2, "captain:banner") && People.HalberdForTest(guard), S(king) + " | " + S(cap) + " | " + S(guard));
			Frame("rg_corpse_alive.png");
			long kid = king.data.id;
			long cid = cap.data.id;
			int before = Corpses.Count;
			Release(king);
			Release(cap);
			Release(guard);
			Die(king);
			Die(cap);
			Die(guard);
			string[] array = null;
			string[] array2 = null;
			for (int f = 0; f < 120; f++)
			{
				if (array != null && array2 != null)
				{
					break;
				}
				yield return null;
				array = People.FallenRegaliaShown(kid);
				array2 = People.FallenRegaliaShown(cid);
			}
			yield return CivilTest.Wait(0.7f);
			array = People.FallenRegaliaShown(kid);
			array2 = People.FallenRegaliaShown(cid);
			Check("the king's corpse keeps crown, mantle and hem", Has(array, "royal:crown") && Has(array, "royal:mantle") && Has(array, "royal:mantle_hem"), (array == null) ? "(none)" : string.Join(",", array));
			Check("the captain's corpse: no banner, no plume, the sash", array2 != null && !HasAny(array2, "captain:") && !Has(array2, "soldier:plume") && Has(array2, "soldier:sash"), (array2 == null) ? "(none)" : string.Join(",", array2));
			string text = FallenWeapons();
			Check("the guard drops his halberd (it falls as his weapon)", text.Contains("rg_halberd"), $"fallen weapons: {text}; corpses {before} -> {Corpses.Count}");
			Frame("rg_corpses.png");
			int lie = 0;
			bool gone = false;
			for (float t = 0f; t < 45f; t += 0.5f)
			{
				if (gone)
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
				if (People.FallenRegaliaShown(kid) != null)
				{
					lie++;
				}
				else
				{
					gone = lie > 0;
				}
			}
			Check("the crown and mantle lie with the body, then go with it", lie >= 10 && gone, $"{lie} half-seconds seen, gone {gone}");
		}

		private static string FallenWeapons()
		{
			try
			{
				if (!(typeof(Corpses).GetField("Bodies", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) is IList list))
				{
					return "(unreadable)";
				}
				List<string> list2 = new List<string>();
				foreach (object item in list)
				{
					MeshInstance meshInstance = item.GetType().GetField("Weapon")?.GetValue(item) as MeshInstance;
					if (meshInstance?.Model != null)
					{
						list2.Add(meshInstance.Model.Name);
					}
				}
				return (list2.Count == 0) ? "(none)" : string.Join(",", list2.ToArray());
			}
			catch (Exception ex)
			{
				return "(" + ex.Message + ")";
			}
		}

		private IEnumerator FigurePart()
		{
			Actor king = _k1.K.king;
			Release(king);
			TakeOff(king, EquipmentType.Helmet);
			yield return Possess(king);
			if (!Check("in control of the K1 king", InControl(king)))
			{
				yield break;
			}
			CharacterView view = Mod.YouView;
			view.Turn = 0f;
			Mod.Craft.Open = true;
			yield return CivilTest.Wait(2f);
			float tallOn = Field(view, "_tall");
			Check("the figure wears the royal set", view.PartNames.Contains("rg_royal_"), view.PartNames);
			Check("...and no halberd or banner", !view.PartNames.Contains("rg_halberd") && !view.PartNames.Contains("rg_captain_"), view.PartNames);
			yield return Capture("rg_figure_king.png");
			KingdomStyle own = Regalia.LookOf(_k1.K).Style;
			KingdomStyle other = ((own == KingdomStyle.IronSpikes) ? KingdomStyle.GoldWings : KingdomStyle.IronSpikes);
			string want = "rg_royal_" + RegaliaKit.StyleLetter(other) + "_";
			bool seen = false;
			for (int f = 0; f < 90; f++)
			{
				Regalia.PreviewStyle(_k1.K, other);
				yield return null;
				if (view.PartNames.Contains(want))
				{
					seen = true;
				}
			}
			Check("the council's style preview shows on the figure", seen, want + " in " + view.PartNames);
			yield return CivilTest.Wait(1.2f);
			Check("...and goes with the hover", view.PartNames.Contains("rg_royal_" + RegaliaKit.StyleLetter(own) + "_"), view.PartNames);
			People.RegaliaOn = false;
			yield return CivilTest.Wait(2.5f);
			float tallOff = Field(view, "_tall");
			People.RegaliaOn = true;
			yield return CivilTest.Wait(2.5f);
			Check("framed on what shows (the crown, not hidden pieces)", tallOn > 0f && tallOn / Mathf.Max(0.01f, tallOff) < 1.22f, $"tall {tallOn:F3} with, {tallOff:F3} without");
			Put(king, "helmet_iron");
			People.RecheckForTest(king);
			yield return CivilTest.Wait(2.5f);
			float num = Field(view, "_tall");
			Check("crown-helm on the figure, framed on what shows", view.PartNames.Contains("rg_royal_") && num / Mathf.Max(0.01f, tallOff) < 1.3f, $"{People.HelmetLookForTest(king)}: tall {num:F3} vs {tallOff:F3}");
			yield return Capture("rg_figure_king_helm.png");
			TakeOff(king, EquipmentType.Helmet);
			Mod.Craft.Open = false;
			yield return CivilTest.Wait(0.5f);
			yield return Possess(_photographer);
			Hold(_photographer, _stage + new Vector2(0f, -9f));
		}

		private IEnumerator OwnBodyPart()
		{
			Actor king = _k1.K.king;
			Release(king);
			yield return Possess(king);
			if (Check("in control of the K1 king", InControl(king)))
			{
				Settings s = Mod.Settings;
				int showBody = s.ShowBody;
				int time = s.TimeMode;
				s.ShowBody = 2;
				s.TimeMode = 2;
				SceneCollector scene = Mod.SceneForTest;
				LookAway();
				Mod.SetPitchDegrees(-32f);
				int most = 0;
				for (int k = 0; k < 4; k++)
				{
					Mod.SetYaw((float)k * MathF.PI * 0.5f);
					yield return CivilTest.Wait(0.8f);
					most = Mathf.Max(most, scene.OwnShadowsForTest);
				}
				Mod.SaveFrame("rg_own_shadow.png");
				Check("your shadow carries the royal set (body + set)", most >= 2, most + " shadow meshes");
				People.RegaliaOn = false;
				yield return CivilTest.Wait(0.8f);
				int off = scene.OwnShadowsForTest;
				People.RegaliaOn = true;
				yield return CivilTest.Wait(0.8f);
				Check("one shadow mesh fewer with regalia off", off == most - 1, off + " vs " + most);
				Mod.SetPitchDegrees(-60f);
				yield return CivilTest.Wait(1f);
				Mod.SaveFrame("rg_own_legs.png");
				Check("looking 60 degrees down: your legs, without regalia copies", scene.OwnLegsForTest == 1, scene.OwnLegsForTest + " legs meshes");
				Mod.SetPitchDegrees(0f);
				s.ShowBody = showBody;
				s.TimeMode = time;
				yield return Possess(_photographer);
				Hold(_photographer, _stage + new Vector2(0f, -9f));
			}
		}

		private IEnumerator RoomPart()
		{
			yield return EnsureRoles();
			if (!_rolesReady)
			{
				yield break;
			}
			Actor king = _k1.K.king;
			Actor marshal = _seats[0];
			Actor mage = _seats[4];
			Actor cap = _captain;
			Actor guard = _coif;
			Actor[] array = new Actor[5] { king, marshal, mage, cap, guard };
			foreach (Actor a in array)
			{
				Hold(a, _stage + new Vector2(0f, 3f));
			}
			yield return Frames(3);
			array = new Actor[5] { king, marshal, mage, cap, guard };
			foreach (Actor a2 in array)
			{
				Release(a2);
			}
			LookAway();
			Mod.EnterRoomForTest(new List<Actor>(), "human", 4);
			yield return CivilTest.Wait(0.5f);
			Interior room = Mod.Interior.Room;
			if (!Check("in a room", room != null && Mod.Interior.Inside))
			{
				yield break;
			}
			Vector2 eye = Mod.Interior.Eye;
			float spawnYaw = room.SpawnYaw;
			float num = spawnYaw * 57.29578f + 180f;
			Vector2 vector = new Vector2(Mathf.Cos(spawnYaw), Mathf.Sin(spawnYaw));
			Vector2 vector2 = new Vector2(0f - vector.y, vector.x);
			Vector2 vector3 = eye + vector * 2.6f;
			Vector2 vector4 = eye + vector * 2.2f + vector2 * 0.9f;
			Vector2 vector5 = eye + vector * 2.2f - vector2 * 0.9f;
			Vector2 vector6 = eye + vector * 1.6f + vector2 * 1.6f;
			Vector2 vector7 = eye + vector * 1.6f - vector2 * 1.6f;
			List<RoomSeat> seats = new List<RoomSeat>
			{
				new RoomSeat
				{
					Who = king,
					X = vector3.x,
					Y = vector3.y,
					Yaw = num,
					Sit = 1f
				},
				new RoomSeat
				{
					Who = marshal,
					X = vector4.x,
					Y = vector4.y,
					Yaw = num - 30f,
					Sit = 1f
				},
				new RoomSeat
				{
					Who = mage,
					X = vector5.x,
					Y = vector5.y,
					Yaw = num + 30f,
					Sit = 1f
				},
				new RoomSeat
				{
					Who = cap,
					X = vector6.x,
					Y = vector6.y,
					Yaw = num,
					Sit = 0f
				},
				new RoomSeat
				{
					Who = guard,
					X = vector7.x,
					Y = vector7.y,
					Yaw = num,
					Sit = 0f
				}
			};
			Mod.Interior.PinForTest(seats);
			yield return CivilTest.Wait(1.6f);
			string[] list = ShownOf(king);
			string[] list2 = ShownOf(marshal);
			string[] list3 = ShownOf(mage);
			ShownOf(cap);
			Check("seated king: his crown, no mantle hanging", Has(list, "royal:crown") && !Has(list, "royal:mantle") && !Has(list, "royal:mantle_hem"), S(king));
			Check("seated marshal: no baton in hand", !Has(list2, "court:baton") && Has(list2, "soldier:sash"), S(marshal));
			Check("seated court mage: collar, no hanging robe or staff", !Has(list3, "robe:robe") && !Has(list3, "robe:robe_hem") && !Has(list3, "robe:staff") && HasAny(list3, "robe:robe_yoke"), S(mage));
			Note($"generated room, standing: captain {S(cap)}, guard halberd {People.HalberdForTest(guard)} (inside a building for the game: {cap.is_inside_building}/{guard.is_inside_building})");
			Mod.SaveFrame("rg_room.png");
			Mod.Interior.PinForTest(new List<RoomSeat>());
			Mod.LeaveRoomForTest();
			yield return CivilTest.Wait(0.5f);
			Building house = AddHouse("house_human_1", _k1.Town);
			if (house == null)
			{
				Note("no house for the indoor check");
				yield break;
			}
			array = new Actor[2] { cap, guard };
			foreach (Actor actor in array)
			{
				Release(actor);
				try
				{
					actor.cancelAllBeh();
					actor.stayInBuilding(house);
					actor.timer_action = 300f;
				}
				catch (Exception ex)
				{
					Note("in: " + ex.Message);
				}
			}
			Release(_photographer);
			for (float t = 0f; t < 5f; t += 0.5f)
			{
				if (Mod.Interior.Inside)
				{
					break;
				}
				Mod.EnterHouseForTest(house);
				yield return CivilTest.Wait(0.5f);
			}
			if (Check("inside a house with a captain and a guard staying in it", Mod.Interior.Inside && cap.is_inside_building && guard.is_inside_building, $"in {Mod.Interior.Inside}, captain inside {cap.is_inside_building}, guard inside {guard.is_inside_building}"))
			{
				yield return CivilTest.Wait(2f);
				People.LegsForTest(cap, out var mesh, out var rig, out var age);
				People.LegsForTest(guard, out mesh, out rig, out var age2);
				if (age <= 3)
				{
					Check("a captain indoors: no banner", !HasAny(ShownOf(cap), "captain:") && Has(ShownOf(cap), "soldier:sash"), S(cap));
				}
				else
				{
					Note("the captain wasn't drawn in the house: " + Who(cap));
				}
				if (age2 <= 3)
				{
					Check("a guard indoors: no halberd", !People.HalberdForTest(guard) && Has(ShownOf(guard), "soldier:sash"), S(guard));
				}
				else
				{
					Note("the guard wasn't drawn in the house: " + Who(guard));
				}
				Mod.SaveFrame("rg_room_house.png");
			}
			if (Mod.Interior.Inside)
			{
				Mod.LeaveRoomForTest();
			}
			yield return CivilTest.Wait(0.5f);
			array = new Actor[2] { cap, guard };
			foreach (Actor actor2 in array)
			{
				try
				{
					actor2.exitBuilding();
				}
				catch
				{
				}
			}
			Hold(_photographer, _stage + new Vector2(0f, -9f));
		}

		private IEnumerator AimPart()
		{
			yield return EnsureRoles();
			if (_rolesReady)
			{
				Actor king = _k1.K.king;
				Actor cap = _captain;
				Put(king, "helmet_iron");
				Vector2 vector = _stage + new Vector2(0f, 2f);
				Hold(cap, vector);
				Hold(king, vector + new Vector2(2.5f, 0f));
				Look(vector + new Vector2(0f, -8f), vector, 0f);
				yield return Refresh(new Actor[2] { king, cap }, 1.2f);
				yield return AimAt(cap, RegaliaKit.Set.Captain, "banner", "the captain's banner");
				yield return AimAt(king, RegaliaKit.Set.Royal, "helm", "the king's crown-helm");
				TakeOff(king, EquipmentType.Helmet);
				Release(king);
				Release(cap);
			}
		}

		private IEnumerator AimAt(Actor who, RegaliaKit.Set set, string part, string what)
		{
			RegaliaDress.Wearer wearer = WearerOf(who);
			MeshInstance meshInstance = ((wearer != null) ? wearer.Sets[(uint)set] : null);
			int num = ((meshInstance?.Model != null) ? meshInstance.Model.PartIndex(part) : (-1));
			if (num < 0 || wearer.Shown[(uint)set] == 0L)
			{
				Note("aim at " + what + ": not drawn (" + S(who) + ")");
				yield break;
			}
			MeshPart meshPart = meshInstance.Model.Parts[num];
			Vec3 v = default(Vec3);
			float num2 = float.MinValue;
			for (int i = 0; i < meshPart.VertexCount; i++)
			{
				Vec3 vec = new Vec3(meshPart.Pos[i * 3], meshPart.Pos[i * 3 + 1], meshPart.Pos[i * 3 + 2]);
				v += vec * (1f / (float)meshPart.VertexCount);
				num2 = Mathf.Max(num2, vec.Z);
			}
			if (part == "helm")
			{
				v = new Vec3(v.X, v.Y, v.Z * 0.3f + num2 * 0.7f);
			}
			Vec3 world = meshInstance.Position + meshInstance.Basis.Mul(v);
			Vector2 vector = new Vector2(world.X, world.Y);
			Vector2 eye = vector + new Vector2(0f, -8f);
			float height = Mathf.Max(0.5f, world.Z - Mod.TerrainForTest.SampleHeight(eye.x, eye.y));
			Look(eye, vector, 0f, height);
			yield return Frames(8);
			Vec3 point;
			Actor creature;
			string text = (FirstPersonAim.Point(out point, out creature) ? $"({point.X:F2},{point.Y:F2},{point.Z:F2})" : "sky");
			string text2 = ((creature == who) ? "the wearer" : ((creature == null) ? "nobody" : "someone else"));
			Note($"aim at {what} ({world.X:F2},{world.Y:F2},{world.Z:F2}): point {text}, creature {D(creature)} ({text2}), prompt '{Mod.Prompt}'");
			Frame("rg_aim_" + ((set == RegaliaKit.Set.Captain) ? "banner" : "crownhelm") + ".png");
		}

		private static Why ParseWhy(string s)
		{
			Why result = new Why
			{
				Nb = new bool[3],
				Used = new int[3],
				Far = new float[3],
				Mix = new ulong[3],
				Pick = -1
			};
			if (string.IsNullOrEmpty(s))
			{
				return result;
			}
			try
			{
				string[] array = s.Split(' ');
				foreach (string text in array)
				{
					int num = text.IndexOf('=');
					if (num <= 0)
					{
						continue;
					}
					string text2 = text.Substring(0, num);
					string text3 = text.Substring(num + 1);
					string[] array2 = text3.Split(',');
					switch (text2)
					{
					case "nb":
					{
						for (int m = 0; m < 3; m++)
						{
							result.Nb[m] = array2[m] == "1";
						}
						break;
					}
					case "used":
					{
						for (int k = 0; k < 3; k++)
						{
							result.Used[k] = int.Parse(array2[k], CultureInfo.InvariantCulture);
						}
						break;
					}
					case "far":
					{
						for (int l = 0; l < 3; l++)
						{
							result.Far[l] = ((array2[l] == "inf") ? float.PositiveInfinity : float.Parse(array2[l], CultureInfo.InvariantCulture));
						}
						break;
					}
					case "mix":
					{
						for (int j = 0; j < 3; j++)
						{
							result.Mix[j] = ulong.Parse(array2[j], CultureInfo.InvariantCulture);
						}
						break;
					}
					case "pick":
						result.Pick = int.Parse(text3, CultureInfo.InvariantCulture);
						break;
					}
				}
				result.Ok = result.Pick >= 0;
			}
			catch
			{
				result.Ok = false;
			}
			return result;
		}

		private static KingdomStyle Stored(Kingdom k)
		{
			try
			{
				k.data.get("fp_style", out var pResult, -1);
				return (pResult >= 0 && pResult <= 2) ? ((KingdomStyle)pResult) : KingdomStyle.None;
			}
			catch
			{
				return KingdomStyle.None;
			}
		}

		private void CheckPick(string name, Kingdom k)
		{
			string text = Regalia.WhyForTest(k);
			Why why = ParseWhy(text);
			KingdomStyle kingdomStyle = Stored(k);
			if (!why.Ok)
			{
				Check(name + ": its style's inputs are known", ok: false, text);
				return;
			}
			int num = why.Rule();
			bool flag = num == why.Pick && (int)kingdomStyle == why.Pick;
			if (!flag && why.FarTie && (int)kingdomStyle == why.Pick)
			{
				Note($"{name}: a near tie on distance, the rule's pick {num} vs {why.Pick} ({text})");
			}
			else
			{
				Check(name + " wears the rule's pick (" + Regalia.StyleName(kingdomStyle) + ")", flag, $"stored {kingdomStyle}, rule {num}, {text}");
			}
		}

		private IEnumerator StylesPart()
		{
			List<KeyValuePair<string, Kingdom>> list = new List<KeyValuePair<string, Kingdom>>();
			foreach (Realm item in AllRealms())
			{
				list.Add(new KeyValuePair<string, Kingdom>(item.Name, item.K));
			}
			foreach (KeyValuePair<string, Kingdom> item2 in list)
			{
				CheckPick(item2.Key, item2.Value);
			}
			Why why = ParseWhy(Regalia.WhyForTest(_k1.K));
			KingdomStyle kingdomStyle = Stored(_k1.K);
			KingdomStyle kingdomStyle2 = Stored(_k2.K);
			KingdomStyle kingdomStyle3 = Stored(_k3.K);
			KingdomStyle kingdomStyle4 = Stored(_k4.K);
			if (why.Ok && why.Used[0] == why.Used[1] && why.Used[1] == why.Used[2])
			{
				Check("K1..K3 wear all three styles (they started from equal counts)", kingdomStyle != kingdomStyle2 && kingdomStyle2 != kingdomStyle3 && kingdomStyle != kingdomStyle3, $"{kingdomStyle} {kingdomStyle2} {kingdomStyle3}; K1 {Regalia.WhyForTest(_k1.K)}");
			}
			else
			{
				Note("the counts were not equal when K1 took its style: all three not required (" + Regalia.WhyForTest(_k1.K) + ")");
			}
			Why why2 = ParseWhy(Regalia.WhyForTest(_k4.K));
			float magnitude = (_k4.Centre - _k1.Centre).magnitude;
			if (why2.Ok && why2.Nb[(int)kingdomStyle])
			{
				Check("K4 avoids its neighbour K1's style where it can", kingdomStyle4 != kingdomStyle || (why2.Nb[0] && why2.Nb[1] && why2.Nb[2]), $"K4 {kingdomStyle4}, K1 {kingdomStyle}, {magnitude:F0} tiles apart; {Regalia.WhyForTest(_k4.K)}");
			}
			else
			{
				Note($"K1 is not among K4's neighbours ({magnitude:F0} tiles apart): {Regalia.WhyForTest(_k4.K)}");
			}
			Realm[] array = new Realm[2] { _orc, _cat };
			foreach (Realm realm in array)
			{
				if (realm != null)
				{
					KingdomLook kingdomLook = Regalia.LookOf(realm.K);
					Check(realm.Name + ": a style kept but not shown (not human-founded)", Stored(realm.K) != KingdomStyle.None && !kingdomLook.Styled && !Regalia.Styled(realm.K), $"stored {Stored(realm.K)}, styled {kingdomLook.Styled}");
				}
			}
			foreach (Realm item3 in AllRealms())
			{
				KingdomLook kingdomLook2 = Regalia.LookOf(item3.K);
				ColorAsset color = item3.K.getColor();
				RegaliaColors regaliaColors = RegaliaColors.From(Pack(color.getColorMain()), Pack(color.getColorMainSecond()), Pack(color.getColorBanner()));
				Check(item3.Name + ": its regalia colours are its own", Rgb(kingdomLook2.Colors.Main) == Rgb(regaliaColors.Main) && Rgb(kingdomLook2.Colors.Second) == Rgb(regaliaColors.Second) && Rgb(kingdomLook2.Colors.Emblem) == Rgb(regaliaColors.Emblem), Hex(kingdomLook2.Colors.Main) + "/" + Hex(kingdomLook2.Colors.Second) + "/" + Hex(kingdomLook2.Colors.Emblem) + " want " + Hex(regaliaColors.Main) + "/" + Hex(regaliaColors.Second) + "/" + Hex(regaliaColors.Emblem) + " (" + color.id + ")");
			}
			Dictionary<long, KingdomStyle> before = new Dictionary<long, KingdomStyle>();
			foreach (Realm item4 in AllRealms())
			{
				before[item4.K.data.id] = Stored(item4.K);
			}
			yield return CivilTest.Wait(10f);
			Realm k5 = Found("K5", "human", 8, 0, null, 0f);
			if (k5 != null)
			{
				foreach (Actor person in k5.People)
				{
					Invincible(person);
				}
				Regalia.LookOf(k5.K);
				for (float t = 0f; t < 8f; t += 0.5f)
				{
					if (Stored(k5.K) != KingdomStyle.None)
					{
						break;
					}
					yield return CivilTest.Wait(0.5f);
				}
				CheckPick("K5 (founded later)", k5.K);
			}
			string text = Changed(before);
			Check("no realm's style changed in 10 s nor with a new realm", text == "", text);
			Actor king = _k1.K.king;
			Release(king);
			Die(king);
			Actor next = null;
			for (float t = 0f; t < 25f; t += 0.5f)
			{
				if (next != null)
				{
					break;
				}
				yield return CivilTest.Wait(0.5f);
				try
				{
					if (_k1.K.isAlive() && _k1.K.king != null && _k1.K.king != king && _k1.K.king.isAlive())
					{
						next = _k1.K.king;
					}
				}
				catch
				{
				}
			}
			Note("K1's king died; the new king: " + D(next));
			text = Changed(before);
			Check("K1 keeps its style through its king's death", Stored(_k1.K) == before[_k1.K.data.id] && text == "", $"{Stored(_k1.K)} was {before[_k1.K.data.id]}; {text}");
			if (next == null)
			{
				next = _k1.People.Find((Actor a) => a != null && a.isAlive() && a.isAdult() && !a.isWarrior() && a != _photographer && a != _leader && a != _captain);
				try
				{
					if (next != null)
					{
						_k1.K.setKing(next);
					}
				}
				catch (Exception ex)
				{
					Note("crown: " + ex.Message);
				}
				Note("no successor in 25 s; crowned " + D(next) + " for the parts after");
			}
			if (next != null)
			{
				Invincible(next);
			}
		}

		private string Changed(Dictionary<long, KingdomStyle> before)
		{
			string text = "";
			foreach (Realm item in AllRealms())
			{
				if (before.TryGetValue(item.K.data.id, out var value) && Stored(item.K) != value)
				{
					text += $"{item.Name} {value} -> {Stored(item.K)}; ";
				}
			}
			return text;
		}

		private IEnumerator CouncilPart()
		{
			yield return EnsureRoles();
			if (!_rolesReady)
			{
				yield break;
			}
			Kingdom k = _k1.K;
			Actor king = k.king;
			Release(king);
			TakeOff(king, EquipmentType.Helmet);
			yield return Possess(king);
			if (!Check("in control of the K1 king", InControl(king)))
			{
				yield break;
			}
			Building hall = Court.HallOf(_k1.Town);
			if (hall == null)
			{
				hall = AddHouse("hall_human_0", _k1.Town);
				yield return CivilTest.Wait(1f);
				if (Court.HallOf(_k1.Town) == null)
				{
					Note("the hall put up isn't the town's hall yet: " + hall?.asset?.id);
				}
			}
			int flagBefore = -9;
			if (hall != null)
			{
				Vector2 vector = ((hall.current_tile != null) ? new Vector2((float)hall.current_tile.x + 0.5f, (float)hall.current_tile.y + 0.5f) : _k1.Centre);
				Look(vector + new Vector2(0f, -14f), vector, -4f, 1.5f);
				yield return CivilTest.Wait(3f);
				SceneCollector.HallFlagState hallFlagState = Mod.SceneForTest.HallFlagForTest(hall);
				if (hallFlagState != null)
				{
					flagBefore = hallFlagState.Shown.Style;
				}
				Note("the hall's flag before: " + ((hallFlagState == null) ? "not drawn" : ("style " + hallFlagState.Shown.Style)));
			}
			LookAway();
			WorldTile worldTile = LandNear(king.current_position + new Vector2(3f, 0f));
			Vector2 at = ((worldTile != null) ? new Vector2((float)worldTile.x + 0.5f, (float)worldTile.y + 0.5f) : (king.current_position + new Vector2(3f, 0f)));
			Hold(_captain, at);
			Mod.SetYaw(Mathf.Atan2(at.y - king.current_position.y, at.x - king.current_position.x));
			Mod.Royal.Open = true;
			yield return CivilTest.Wait(0.6f);
			Hud hud = Mod.HudForTest;
			RegaliaTest regaliaTest = this;
			bool ok = hud.StyleRectsForTest[0].width > 0f && hud.StyleRectsForTest[2].xMax > hud.StyleRectsForTest[0].xMax;
			Rect rect = hud.StyleRectsForTest[0];
			string text = rect.ToString();
			rect = hud.StyleRectsForTest[2];
			regaliaTest.Check("the style row is drawn", ok, text + " " + rect.ToString());
			KingdomStyle kingdomStyle = Stored(k);
			int next = (int)(kingdomStyle + 1) % 3;
			yield return Capture("rg_council.png");
			hud.StyleHoverForTest = next;
			yield return CivilTest.Wait(0.3f);
			yield return Capture("rg_council_style.png");
			hud.StyleHoverForTest = -1;
			int version = Regalia.Version;
			int serial = Regalia.StyleSerial(k);
			yield return Press(hud.StyleRectsForTest[next].center);
			yield return CivilTest.Wait(0.3f);
			string letter = RegaliaKit.StyleLetter((KingdomStyle)next);
			Check("a click picks the style: stored, serial and version up", (int)Stored(k) == next && Regalia.Version != version && Regalia.StyleSerial(k) == serial + 1, $"stored {Stored(k)} want {(KingdomStyle)next}, serial {serial} -> {Regalia.StyleSerial(k)}");
			Check("...and says so", Mod.ToastText != null && Mod.ToastText.StartsWith("Royal style", StringComparison.Ordinal), Mod.ToastText);
			int again = Regalia.Version;
			yield return Press(hud.StyleRectsForTest[next].center);
			Check("the chosen one takes no click", Regalia.Version == again);
			Mod.Royal.Open = false;
			yield return CivilTest.Wait(0.2f);
			string text2 = null;
			string text3 = null;
			for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime)
			{
				yield return null;
				text2 = People.HelmetLookForTest(king);
				text3 = People.RegaliaModelForTest(_captain, RegaliaKit.Set.Captain);
				if (text2 == "crown:" + letter && text3 != null && text3.StartsWith("rg_captain_" + letter + "_", StringComparison.Ordinal))
				{
					break;
				}
			}
			Check("within 2 s his crown is the new style", text2 == "crown:" + letter, text2);
			Check("within 2 s his captain's banner is the new style", text3?.StartsWith("rg_captain_" + letter + "_", StringComparison.Ordinal) ?? false, text3 + "; " + S(_captain));
			if (hall != null)
			{
				Vector2 vector2 = ((hall.current_tile != null) ? new Vector2((float)hall.current_tile.x + 0.5f, (float)hall.current_tile.y + 0.5f) : _k1.Centre);
				Look(vector2 + new Vector2(0f, -14f), vector2, -4f, 1.5f);
				yield return CivilTest.Wait(3f);
				SceneCollector.HallFlagState hallFlagState2 = Mod.SceneForTest.HallFlagForTest(hall);
				if (hallFlagState2 == null)
				{
					Note("the town hall's flag was not drawn: not checked");
				}
				else
				{
					Check("the hall's flag takes the new style", (hallFlagState2.Next.Style == next || hallFlagState2.Shown.Style == next) && flagBefore != next, $"before {flagBefore}; shown {hallFlagState2.Shown.Style}, next {hallFlagState2.Next.Style}, swap {hallFlagState2.SwapPhase}");
				}
				Frame("rg_council_hallflag.png");
				LookAway();
			}
			else
			{
				Check("a town hall for the flag", ok: false);
			}
			Mod.Craft.Open = true;
			yield return CivilTest.Wait(1.5f);
			Check("the Tab figure wears the new style", Mod.YouView.PartNames.Contains("rg_royal_" + letter + "_"), Mod.YouView.PartNames);
			Mod.Craft.Open = false;
			if (_k2.K.king != null)
			{
				Check("another realm's king can't choose (not yours)", !Regalia.SetStyle(_k2.K.king, (KingdomStyle)((int)(Stored(_k2.K) + 1) % 3)));
			}
			Check("the same style again is no change", !Regalia.SetStyle(king, (KingdomStyle)next));
			Release(_captain);
			yield return Possess(_leader);
			if (InControl(_leader))
			{
				Check("a town's leader can't choose", !Regalia.SetStyle(_leader, (KingdomStyle)((int)(Stored(k) + 1) % 3)));
			}
			if (_orc != null && _orc.K.king != null)
			{
				yield return Possess(_orc.K.king);
				if (InControl(_orc.K.king))
				{
					Mod.Royal.Open = true;
					yield return CivilTest.Wait(0.6f);
					RegaliaTest regaliaTest2 = this;
					bool ok2 = Mod.Royal.Open && hud.StyleRectsForTest[0].width == 0f;
					rect = hud.StyleRectsForTest[0];
					regaliaTest2.Check("an orc king sees no style row", ok2, "rect " + rect.ToString());
					yield return Capture("rg_council_orc.png");
					Mod.Royal.Open = false;
				}
			}
			yield return Possess(_photographer);
			Hold(_photographer, _stage + new Vector2(0f, -9f));
		}

		private IEnumerator ReusePart()
		{
			Actor cap = PlainSoldier(_k2, _k2Soldier);
			if (cap != null && MakeCaptain(cap))
			{
				Put(cap, "helmet_iron");
				Vector2 at = _stage + new Vector2(0f, -1.6f);
				Hold(cap, at);
				Look(at + new Vector2(0f, -4f), at);
				yield return Refresh(new Actor[1] { cap }, 1.2f);
				Check("a K2 captain with his banner", Has(ShownOf(cap), "captain:banner"), S(cap));
				long id = cap.data.id;
				Release(cap);
				Die(cap);
				Actor fresh = CivilTest.Spawn("human", at);
				Note(string.Format("spawned at once: {0}, the dead captain's object {1} (id {2} -> {3})", D(fresh), (fresh == cap) ? "reused" : "not reused", id, fresh?.data.id));
				if (fresh != null)
				{
					Join(_k2, fresh);
					Invincible(fresh);
					Hold(fresh, at);
					yield return Shown(10);
					yield return CivilTest.Wait(1.3f);
					Check("the newcomer shows nothing of the dead captain's", !HasAny(ShownOf(fresh), "captain:") && !HasAny(ShownOf(fresh), "soldier:"), S(fresh));
					Release(fresh);
				}
			}
			else
			{
				Note("no K2 captain made: the actor reuse not checked");
			}
			Kingdom oldK4 = _k4.K;
			long oldId = oldK4.data.id;
			KingdomStyle oldStyle = Stored(oldK4);
			List<Actor> soldiers = _k4.People.FindAll((Actor a) => a != null && a.isAlive() && a.isWarrior());
			try
			{
				_k4.Town.joinAnotherKingdom(_k1.K, pCaptured: true);
			}
			catch (Exception ex)
			{
				Note("take K4's town: " + ex.Message);
			}
			yield return null;
			bool flag = true;
			try
			{
				flag = !oldK4.isAlive();
			}
			catch
			{
			}
			Note("K4 after losing its last town: " + (flag ? "gone" : "still alive"));
			Realm k5 = Found("K5", "human", 8, 0, null, 0f);
			if (k5 != null)
			{
				foreach (Actor person in k5.People)
				{
					Invincible(person);
				}
				Note(string.Format("K5 #{0}: {1} (K4 was #{2}, {3})", k5.K.data.id, (k5.K == oldK4) ? "K4's object reused" : "a new object", oldId, oldStyle));
				Regalia.LookOf(k5.K);
				for (float t = 0f; t < 8f; t += 0.5f)
				{
					if (Stored(k5.K) != KingdomStyle.None)
					{
						break;
					}
					yield return CivilTest.Wait(0.5f);
				}
				CheckPick("K5 (founded as K4 fell)", k5.K);
			}
			if (soldiers.Count > 0)
			{
				Actor fresh = soldiers[0];
				Vector2 vector = _stage + new Vector2(2f, -1.6f);
				Hold(fresh, vector);
				Look(vector + new Vector2(0f, -4f), vector);
				yield return Refresh(new Actor[1] { fresh }, 3.5f);
				uint c = People.RegaliaSlotForTest(fresh, RegaliaKit.Set.Soldier, "sash");
				Check("a soldier of the taken town wears K1's colour", fresh.kingdom == _k1.K && Rgb(c) == Rgb(Regalia.LookOf(_k1.K).Colors.Main), "realm " + fresh.kingdom?.name + ", sash " + Hex(c) + ", K1 main " + Hex(Regalia.LookOf(_k1.K).Colors.Main) + "; " + S(fresh));
				Release(fresh);
			}
		}

		private IEnumerator SavePart()
		{
			Dictionary<long, KingdomStyle> ids = new Dictionary<long, KingdomStyle>();
			foreach (Realm item in AllRealms())
			{
				try
				{
					if (item.K.isAlive() && item.K != _k3.K)
					{
						ids[item.K.data.id] = Stored(item.K);
					}
				}
				catch
				{
				}
			}
			long k1 = _k1.K.data.id;
			long k3 = _k3.K.data.id;
			byte[] zip;
			try
			{
				_k3.K.data.removeInt("fp_style");
				SavedMap savedMap = SaveManager.currentWorldToSavedMap();
				string text = savedMap.toJson();
				Check("the save holds the styles", text.Contains("fp_style"), text.Length + " chars");
				zip = savedMap.toZip();
			}
			catch (Exception ex)
			{
				Check("the world saved to bytes", ok: false, ex.Message);
				yield break;
			}
			Note($"saved {zip.Length} bytes; loading it back, asking about everyone each frame");
			_held.Clear();
			LookAway();
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			yield return null;
			Regalia.ResetForTest();
			try
			{
				SaveManager.loadMapFromBytes(zip);
			}
			catch (Exception ex2)
			{
				Check("the world loaded from bytes", ok: false, ex2.Message);
				yield break;
			}
			int frames = 0;
			int loadingFrames = 0;
			int errors = 0;
			string firstError = null;
			for (float t = 0f; t < 60f; t += Time.unscaledDeltaTime)
			{
				if (WorldBoxMod.WorldLoading)
				{
					loadingFrames++;
				}
				try
				{
					foreach (Kingdom item2 in World.world.kingdoms.list)
					{
						Regalia.LookOf(item2);
					}
					int num = 0;
					foreach (Actor unit in World.world.units)
					{
						if (++num <= 600)
						{
							Regalia.RoleOf(unit);
							Regalia.GearOf(unit, handsBusy: false);
							continue;
						}
						break;
					}
					frames++;
				}
				catch (Exception ex3)
				{
					errors++;
					if (firstError == null)
					{
						firstError = ex3.Message;
					}
				}
				yield return null;
				bool flag = true;
				try
				{
					flag = SmoothLoader.isLoading();
				}
				catch
				{
				}
				if (!flag && Config.game_loaded && t > 2f)
				{
					break;
				}
			}
			Check("asked about mid-load", Regalia.QueriedWhileLoadingForTest > 0 || loadingFrames == 0, $"{Regalia.QueriedWhileLoadingForTest} asks over {loadingFrames} loading frames ({frames} frames asked)");
			if (loadingFrames == 0)
			{
				Note("the load passed in one frame: no mid-load asks possible");
			}
			Check("no style given out mid-load", Regalia.AssignedWhileLoadingForTest == 0, Regalia.AssignedWhileLoadingForTest.ToString());
			Check("no exceptions from the asks mid-load", errors == 0, errors + " " + firstError);
			yield return CivilTest.Wait(1f);
			string text2 = "";
			foreach (KeyValuePair<long, KingdomStyle> item3 in ids)
			{
				Kingdom kingdom = null;
				try
				{
					kingdom = World.world.kingdoms.get(item3.Key);
				}
				catch
				{
				}
				if (kingdom == null)
				{
					text2 += $"#{item3.Key} missing; ";
				}
				else if (Stored(kingdom) != item3.Value)
				{
					text2 += $"#{item3.Key} {item3.Value} -> {Stored(kingdom)}; ";
				}
			}
			Check("the styles came back by realm", text2 == "", text2);
			Kingdom r3 = null;
			try
			{
				r3 = World.world.kingdoms.get(k3);
			}
			catch
			{
			}
			if (r3 != null)
			{
				Check("K3 (as from an old save) loads with no style, none given in the settle", Stored(r3) == KingdomStyle.None, Stored(r3).ToString());
				Regalia.LookOf(r3);
				for (float t = 0f; t < 10f; t += 0.5f)
				{
					if (Stored(r3) != KingdomStyle.None)
					{
						break;
					}
					yield return CivilTest.Wait(0.5f);
				}
				CheckPick("K3 after the reload (given out after the settle)", r3);
			}
			else
			{
				Check("K3 is back", ok: false);
			}
			Kingdom kingdom2 = null;
			try
			{
				kingdom2 = World.world.kingdoms.get(k1);
			}
			catch
			{
			}
			Actor king = kingdom2?.king;
			Actor viewer = null;
			if (kingdom2 != null)
			{
				foreach (Actor unit2 in kingdom2.units)
				{
					if (unit2 != null && unit2.isAlive() && unit2 != king && unit2.isAdult() && !unit2.isWarrior())
					{
						viewer = unit2;
						break;
					}
				}
			}
			if (Check("K1's king and a townsman after the reload", king != null && viewer != null))
			{
				Hold(viewer, _stage + new Vector2(0f, -3f));
				yield return Possess(viewer);
				if (Mod.Interior.Inside)
				{
					Mod.LeaveRoomForTest();
					yield return CivilTest.Wait(0.5f);
				}
				Vector2 vector = viewer.current_position + new Vector2(0f, 3f);
				Hold(king, vector);
				Look(vector + new Vector2(0f, -3f), vector);
				Config.paused = false;
				yield return Shown(20);
				yield return CivilTest.Wait(3f);
				Check("regalia drawn again after the reload", Has(ShownOf(king), "royal:crown") || Has(ShownOf(king), "royal:helm"), S(king));
				Frame("rg_after_reload.png");
				Release(king);
			}
		}
	}
}
