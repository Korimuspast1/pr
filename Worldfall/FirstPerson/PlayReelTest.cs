using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FirstPerson.Core;
using FirstPerson.Towns;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class PlayReelTest : CivilTest
	{
		private struct ClipDef
		{
			public string Name;

			public Func<IEnumerator> Run;

			public ClipDef(string name, Func<IEnumerator> run)
			{
				Name = name;
				Run = run;
			}
		}

		private sealed class TownStreetInfo
		{
			public Vector2 Mid;

			public Vector2 Along;

			public Vector2 Left;

			public float Half;

			public bool Vertical;

			public int Built;

			public readonly List<Building> Houses = new List<Building>();

			public Building Door;

			public Vector2 At(float along, float across = 0f)
			{
				return Mid + Along * along + Left * across;
			}

			public float AlongOf(Vector2 p)
			{
				return Vector2.Dot(p - Mid, Along);
			}

			public float AcrossOf(Vector2 p)
			{
				return Vector2.Dot(p - Mid, Left);
			}
		}

		private const int Fps = 30;

		private string _dir;

		private Actor _king;

		private City _town;

		private Vector2 _centre;

		private Actor _fightHero;

		private long _fightHeroId = -1L;

		private float _fightLane = float.NaN;

		private float _fightPitch;

		private readonly List<Actor> _fightFoes = new List<Actor>();

		private readonly List<long> _fightIds = new List<long>();

		private const int TownHalf = 12;

		private const float TownFace = 0.8f;

		private TownStreetInfo _townStreet;

		private bool _townStreetTried;

		private float _townPace;

		private Actor _townLastBody;

		private bool _townClock;

		private bool _townClockWas;

		private float _townOwnStep = -1f;

		private int _townPage = -1;

		private string _townLine;

		private float _townAge;

		private float _worldSavedDive = -1f;

		private bool? _worldSavedGpu;

		private readonly List<Actor> _worldHelpers = new List<Actor>();

		private bool _worldFires;

		private readonly List<Cloud> _worldClouds = new List<Cloud>();

		private readonly List<Vector2> _worldCloudAt = new List<Vector2>();

		private readonly List<string> _worldCloudKind = new List<string>();

		private int _worldCloudErrors;

		protected override string Prefix => "PREEL";

		private void AddTownClips(List<ClipDef> into)
		{
			into.Add(new ClipDef("walk", TownWalk));
			into.Add(new ClipDef("house", TownHouse));
			into.Add(new ClipDef("talk", TownTalk));
			into.Add(new ClipDef("torch", TownTorch));
		}

		private void AddFightClips(List<ClipDef> into)
		{
			into.Add(new ClipDef("archery", FightArchery));
			into.Add(new ClipDef("sword", FightSword));
		}

		private void AddWorldClips(List<ClipDef> into)
		{
			into.Add(new ClipDef("dive", () => WorldClip("dive", WorldDive)));
			into.Add(new ClipDef("fly", () => WorldClip("fly", WorldFly)));
			into.Add(new ClipDef("dragon", () => WorldClip("dragon", WorldDragon)));
			into.Add(new ClipDef("sail", () => WorldClip("sail", WorldSail)));
			into.Add(new ClipDef("storm", () => WorldClip("storm", WorldStorm)));
			into.Add(new ClipDef("night", () => WorldClip("night", WorldNight)));
		}

		private List<ClipDef> Clips()
		{
			List<ClipDef> list = new List<ClipDef>();
			AddTownClips(list);
			AddFightClips(list);
			AddWorldClips(list);
			return list;
		}

		private bool Wanted(string clip)
		{
			if (Options.Contains("only_"))
			{
				return Options.Contains("only_" + clip);
			}
			return true;
		}

		protected override IEnumerator Body()
		{
			Settings s = Mod.Settings;
			string settingsFile = Path.Combine(Log.Folder, "settings.txt");
			string savedSettings = null;
			try
			{
				savedSettings = (File.Exists(settingsFile) ? File.ReadAllText(settingsFile) : null);
			}
			catch
			{
			}
			int savedRes = s.Resolution;
			int savedTime = s.TimeMode;
			float savedDraw = s.DrawDistance;
			float savedZoom = s.GodViewZoom;
			bool savedGod = s.GodView;
			s.Resolution = WorldBoxMod.ResolutionWidths.Length - 1;
			s.TimeMode = 1;
			s.DrawDistance = Mathf.Max(s.DrawDistance, 200f);
			s.GodViewZoom = 40f;
			Mod.ForceRelight();
			_dir = Path.Combine(Log.Folder, "playreel");
			try
			{
				if (Directory.Exists(_dir))
				{
					Directory.Delete(_dir, recursive: true);
				}
			}
			catch
			{
			}
			Directory.CreateDirectory(_dir);
			Note(string.Format("screen {0}x{1}, renderer {2}", Screen.width, Screen.height, s.GpuRender ? "GPU" : "CPU"));
			CrownOffer.AutoAnswer = true;
			yield return Part("village", Setup());
			if (_king != null)
			{
				foreach (ClipDef c in Clips())
				{
					if (Wanted(c.Name))
					{
						yield return Part(c.Name, c.Run());
						yield return Part(c.Name + " (reset)", Reset());
					}
				}
			}
			Time.captureFramerate = 0;
			PossessionHooks.ScriptedInput = Vector2.zero;
			s.Resolution = savedRes;
			s.TimeMode = savedTime;
			s.DrawDistance = savedDraw;
			s.GodViewZoom = savedZoom;
			s.GodView = savedGod;
			CrownOffer.AutoAnswer = false;
			Mod.NoDive = true;
			if (savedSettings != null)
			{
				try
				{
					File.WriteAllText(settingsFile, savedSettings);
				}
				catch
				{
				}
			}
		}

		private IEnumerator Setup()
		{
			_king = Village("human", 12, 3);
			if (Check("a human village", _king != null))
			{
				_town = _king.city;
				_centre = _king.current_position;
				try
				{
					((BaseSimObject)_king).addStatusEffect("invincible", 3600f, pColorEffect: false);
				}
				catch
				{
				}
				for (int i = 0; i < 5; i++)
				{
					AddHouse((i % 2 == 0) ? "house_human_0" : "house_human_1", _town);
				}
				Note($"village {_town?.name} at {_centre}");
				yield return CivilTest.Wait(3f);
			}
		}

		private IEnumerator Reset()
		{
			Time.captureFramerate = 0;
			PossessionHooks.ScriptedInput = Vector2.zero;
			try
			{
				if (Mod.Flying)
				{
					Mod.LeaveFlyForTest();
				}
			}
			catch
			{
			}
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Mod.Settings.GodView = false;
			Mod.DebugEyeSet = false;
			Mod.NoDive = true;
			Hour(1);
			yield return CivilTest.Wait(1.5f);
		}

		private string Clip(string name)
		{
			string text = Path.Combine(_dir, name);
			try
			{
				if (Directory.Exists(text))
				{
					Directory.Delete(text, recursive: true);
				}
			}
			catch
			{
			}
			Directory.CreateDirectory(text);
			return text;
		}

		private IEnumerator Shoot(string dir, int k, bool hud)
		{
			if (hud && Mod.IsFirstPerson)
			{
				Mod.HudForTest.CaptureHudForTest(Path.Combine(dir, $"f_{k:0000}.jpg"));
				yield return null;
				for (int i = 0; i < 4; i++)
				{
					if (!Mod.HudForTest.CapturePending)
					{
						break;
					}
					yield return null;
				}
			}
			else
			{
				yield return new WaitForEndOfFrame();
				Frame(dir, k);
				yield return null;
			}
		}

		private void Frame(string dir, int k)
		{
			byte[] array = Mod.ShownPngForTest();
			if (array == null)
			{
				return;
			}
			Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
			try
			{
				if (texture2D.LoadImage(array))
				{
					File.WriteAllBytes(Path.Combine(dir, $"f_{k:0000}.jpg"), texture2D.EncodeToJPG(93));
				}
				if (k == 0)
				{
					Note($"{Path.GetFileName(dir)} frames {texture2D.width}x{texture2D.height}");
				}
			}
			finally
			{
				UnityEngine.Object.Destroy(texture2D);
			}
		}

		private static void Rolling()
		{
			Time.captureFramerate = 30;
		}

		private void Cut(string clip, int frames, string detail = null)
		{
			Time.captureFramerate = 0;
			PossessionHooks.ScriptedInput = Vector2.zero;
			Note($"{clip}: {frames} frames" + ((detail != null) ? (", " + detail) : ""));
		}

		private void Hour(int mode)
		{
			Mod.Settings.TimeMode = mode;
			Mod.ForceRelight();
		}

		private void EaseLook(Vector2 at, float pitchDegrees, float ease = 0.15f)
		{
			Vector2 vector = Mod.Host?.current_position ?? at;
			Vector2 vector2 = at - vector;
			if (!(vector2.sqrMagnitude < 0.0001f))
			{
				float b = Mathf.Atan2(vector2.y, vector2.x) * 57.29578f;
				Mod.SetYaw(Mathf.LerpAngle(Mod.ViewYaw * 57.29578f, b, ease) * (MathF.PI / 180f));
				Mod.SetPitchDegrees(pitchDegrees);
			}
		}

		private static Actor Nearest(List<Actor> list, Vector2 to)
		{
			Actor result = null;
			float num = float.MaxValue;
			foreach (Actor item in list)
			{
				if (item != null && item.isAlive())
				{
					float sqrMagnitude = (item.current_position - to).sqrMagnitude;
					if (sqrMagnitude < num)
					{
						num = sqrMagnitude;
						result = item;
					}
				}
			}
			return result;
		}

		private static bool Equip(Actor unit, EquipmentType type, string id)
		{
			try
			{
				EquipmentAsset equipmentAsset = AssetManager.items.get(id);
				if (equipmentAsset == null)
				{
					return false;
				}
				ActorEquipmentSlot slot = unit.equipment.getSlot(type);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				slot.setItem(World.world.items.generateItem(equipmentAsset, unit.kingdom, unit.getName(), 1, unit, 0, pByPlayer: true), unit);
				unit.setStatsDirty();
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static bool DryAt(Vector2 p)
		{
			WorldTile tile = World.world.GetTile((int)p.x, (int)p.y);
			if (tile != null && tile.main_type != null && !tile.main_type.liquid)
			{
				return !tile.main_type.block;
			}
			return false;
		}

		private IEnumerator FightArchery()
		{
			if (!FightSpot(26f, float.NaN, out var home, out var lane))
			{
				Check("archery: open land at the village's edge", ok: false);
				yield break;
			}
			_fightLane = lane;
			Vector2 dir = new Vector2(Mathf.Cos(lane), Mathf.Sin(lane));
			Vector2 side = new Vector2(0f - dir.y, dir.x);
			FightClearLane(home, dir, 26f, 3f);
			Actor you = FightHero(home);
			if (!Check("archery: an archer at the village's edge", you != null))
			{
				yield break;
			}
			long youId = _fightHeroId;
			bool holstered = Mod.Holstered;
			try
			{
				yield return Possess(you);
				if (!Check("archery: in the archer's eyes", InControl(you)))
				{
					yield break;
				}
				yield return FightSettle(you, home);
				Mod.Holstered = false;
				if (!Check("archery: a bow in hand", Equip(you, EquipmentType.Weapon, "bow_wood")))
				{
					yield break;
				}
				for (int i = 0; i < 90; i++)
				{
					if (Mod.Bow.InHand)
					{
						break;
					}
					FightPin(you, home);
					yield return null;
				}
				if (!Check("archery: the bow drawn by the mouse", Mod.Bow.InHand))
				{
					yield break;
				}
				float startYaw = lane + 1.2217305f;
				float startPitch = 1f;
				Mod.SetYaw(startYaw);
				_fightPitch = startPitch;
				Mod.SetPitchDegrees(_fightPitch);
				yield return FightHold(you, home, 0.6f);
				float[] array = new float[3] { 15f, 19f, 23f };
				float[] array2 = new float[3] { -2f, 2.5f, 0.5f };
				string[] array3 = new string[3] { "axe_iron", "sword_iron", "spear_iron" };
				for (int j = 0; j < array.Length; j++)
				{
					FightBandit(home + dir * array[j] + side * array2[j], array3[j], you);
				}
				if (!Check("archery: bandits coming", _fightFoes.Count > 0))
				{
					yield break;
				}
				Mod.ToastUntil = 0f;
				Mod.HintUntil = 0f;
				CombatFeedback fb = Mod.Feedback;
				int launched0 = FirstPersonShots.Launched;
				int struck0 = FirstPersonShots.Struck;
				int hits0 = fb.Hits;
				int kills0 = fb.Kills;
				int struckSeen = struck0;
				int onFoes = 0;
				int lodged = 0;
				int draws = 0;
				int waitedLine = 0;
				int noDraw = 0;
				int n = _fightFoes.Count;
				int down = 0;
				int allDown = -1;
				int tail = 0;
				int held = 0;
				int drawFor = 0;
				Vector2[] last = new Vector2[n];
				bool[] fell = new bool[n];
				for (int k = 0; k < n; k++)
				{
					last[k] = _fightFoes[k].current_position;
				}
				Vector2 fallen = home + dir * 8f;
				float tailYaw = 0f;
				float tailPitch = 0f;
				float tailTurn = 0f;
				string folder = Clip("archery");
				Rolling();
				int k2 = 0;
				while (true)
				{
					if (!InControl(you))
					{
						Note("archery: out of the archer's eyes mid-clip");
						break;
					}
					FightPin(you, home);
					FightKeepAfter(you);
					Actor actor = null;
					float num = float.MaxValue;
					for (int l = 0; l < n; l++)
					{
						if (fell[l])
						{
							continue;
						}
						if (!FightStanding(l))
						{
							fell[l] = true;
							down++;
							fallen = last[l];
							continue;
						}
						last[l] = _fightFoes[l].current_position;
						float magnitude = (last[l] - home).magnitude;
						if (magnitude < num)
						{
							num = magnitude;
							actor = _fightFoes[l];
						}
					}
					if (FirstPersonShots.Struck != struckSeen)
					{
						struckSeen = FirstPersonShots.Struck;
						int num2 = FightIndex(FirstPersonShots.LastVictim);
						if (num2 >= 0)
						{
							onFoes++;
							FightNearlyDown(num2);
						}
					}
					lodged = Math.Max(lodged, StuckArrows.LodgedCount);
					if (actor == null && allDown < 0)
					{
						allDown = k2;
						tail = Mathf.Clamp(270 - k2, 50, 100);
					}
					if ((allDown >= 0 && k2 >= allDown + tail) || (allDown < 0 && k2 >= 360))
					{
						break;
					}
					BowState state = Mod.Bow.State;
					if (actor == null)
					{
						Mod.Bow.HeldForTest = false;
						int num3 = k2 - allDown;
						int num4 = tail - 40;
						if (num3 < 40 || num4 < 40)
						{
							FightLookAt(new Vector3(fallen.x, fallen.y, Mod.GroundAt(fallen) + 0.2f), 0.07f);
						}
						else
						{
							if (num3 == 40)
							{
								tailYaw = Mod.ViewYaw * 57.29578f;
								tailPitch = _fightPitch;
								Vector3 eyeForTest = Mod.EyeForTest;
								float target = Mathf.Atan2(_centre.y - eyeForTest.y, _centre.x - eyeForTest.x) * 57.29578f;
								float num5 = Mathf.Min(110f, (float)num4 * 2f);
								tailTurn = Mathf.Clamp(Mathf.DeltaAngle(tailYaw, target), 0f - num5, num5);
							}
							float num6 = FightSmooth((float)(num3 - 40) / (float)num4);
							Mod.SetYaw((tailYaw + tailTurn * num6) * (MathF.PI / 180f));
							_fightPitch = Mathf.Lerp(tailPitch, 0f, num6);
							Mod.SetPitchDegrees(_fightPitch);
						}
					}
					else if (k2 < 55)
					{
						FightPan(startYaw, startPitch, FightChest(actor), FightSmooth((float)k2 / 55f));
					}
					else
					{
						Vector3 p = FightChest(actor);
						FightLookAt(p, Mod.Bow.HeldForTest ? 0.3f : 0.2f);
						float num7 = FightOff(p);
						if (!Mod.Bow.HeldForTest)
						{
							if (state.State == BowState.Phase.Ready && state.ArrowNocked && num < 26f && num7 < 8f)
							{
								Mod.Bow.HeldForTest = true;
								int num8 = 0;
								drawFor = 0;
								held = num8;
								draws++;
							}
						}
						else
						{
							drawFor++;
							if (state.State == BowState.Phase.Held)
							{
								held++;
							}
							bool num9 = FightLineClear(you, actor);
							if (!num9)
							{
								waitedLine++;
							}
							if (num9 && ((held >= 6 && num7 < 1.5f) || held >= 36))
							{
								Mod.Bow.HeldForTest = false;
							}
							else if (drawFor > 90 && held == 0)
							{
								Mod.Bow.HeldForTest = false;
								noDraw++;
							}
						}
					}
					yield return Shoot(folder, k2, hud: true);
					k2++;
				}
				Mod.Bow.HeldForTest = false;
				int num10 = FirstPersonShots.Launched - launched0;
				Cut("archery", k2, $"{num10} arrows loosed ({draws} draws), {FirstPersonShots.Struck - struck0} struck, {onFoes} on bandits; {down} of {n} bandits down, " + $"up to {lodged} arrows lodged in bodies, hits {fb.Hits - hits0}, kills {fb.Kills - kills0}" + ((waitedLine > 0) ? $", held {waitedLine} frames for a clear line" : "") + ((noDraw > 0) ? $", {noDraw} draws that never came" : ""));
				Check("archery: arrows loosed", num10 > 0, num10.ToString());
				Check("archery: arrows struck the bandits", onFoes > 0, $"{onFoes} of {num10}");
			}
			finally
			{
				PlayReelTest playReelTest = this;
				playReelTest.FightCleanup(you, youId);
				playReelTest.Mod.Holstered = holstered;
			}
		}

		private IEnumerator FightSword()
		{
			if (!FightSpot(12f, _fightLane, out var home, out var lane))
			{
				Check("sword: open land at the village's edge", ok: false);
				yield break;
			}
			Vector2 dir = new Vector2(Mathf.Cos(lane), Mathf.Sin(lane));
			Vector2 side = new Vector2(0f - dir.y, dir.x);
			FightClearLane(home, dir, 12f, 9f);
			Actor you = FightHero(home);
			if (!Check("sword: a swordsman at the village's edge", you != null))
			{
				yield break;
			}
			long youId = _fightHeroId;
			bool holstered = Mod.Holstered;
			int savedTiming = Mod.Settings.ParryTiming;
			bool savedWindups = Mod.Settings.EnemyWindups;
			string weapon = null;
			try
			{
				yield return Possess(you);
				if (!Check("sword: in the swordsman's eyes", InControl(you)))
				{
					yield break;
				}
				yield return FightSettle(you, home);
				Mod.Holstered = false;
				Mod.Settings.ParryTiming = 0;
				Mod.Settings.EnemyWindups = true;
				string[] array = new string[3] { "flame_sword", "sword_steel", "sword_iron" };
				foreach (string text in array)
				{
					if (Equip(you, EquipmentType.Weapon, text))
					{
						weapon = text;
						break;
					}
				}
				if (!Check("sword: a sword in hand", weapon != null))
				{
					yield break;
				}
				Hour(2);
				Mod.SetYaw(lane);
				_fightPitch = -3f;
				Mod.SetPitchDegrees(_fightPitch);
				yield return FightHold(you, home, 1.2f);
				float[] array2 = new float[3] { 6.5f, 8.5f, 10.5f };
				float[] array3 = new float[3] { -1.8f, 1.8f, 0f };
				string[] array4 = new string[3] { "sword_iron", "axe_iron", "sword_iron" };
				for (int j = 0; j < array2.Length; j++)
				{
					FightBandit(home + dir * array2[j] + side * array3[j], array4[j], you);
				}
				if (!Check("sword: bandits coming", _fightFoes.Count > 0))
				{
					yield break;
				}
				Mod.ToastUntil = 0f;
				Mod.HintUntil = 0f;
				CombatFeedback fb = Mod.Feedback;
				int blows0 = PossessionCombat.Blows;
				int blowsSeen = blows0;
				int cuts0 = Slicing.Cuts;
				int kills0 = fb.Kills;
				int hits0 = fb.Hits;
				int parried0 = GuardControl.Parried;
				int blocked0 = GuardControl.Blocked;
				int broken0 = GuardControl.Broken;
				int ripostes0 = GuardControl.Ripostes;
				int winds0 = WindUps.Begun;
				int n = _fightFoes.Count;
				int down = 0;
				int allDown = -1;
				int tail = 0;
				int swings = 0;
				int guards = 0;
				int lastSwing = -99;
				int guardAt = -99;
				int guardEnd = -99;
				int stoppedAt = -99;
				int guardSerial = 0;
				Vector2[] last = new Vector2[n];
				bool[] fell = new bool[n];
				int[] struck = new int[n];
				for (int k = 0; k < n; k++)
				{
					last[k] = _fightFoes[k].current_position;
				}
				Vector2 fallen = home + dir * 3f;
				bool guardUp = false;
				bool riposte = false;
				Actor guardFoe = null;
				double lastWorld = FightNow();
				float step = 1f / 30f;
				string folder = Clip("sword");
				Rolling();
				int k2 = 0;
				while (true)
				{
					if (!InControl(you))
					{
						Note("sword: out of the swordsman's eyes mid-clip");
						break;
					}
					double num = FightNow();
					float num2 = (float)(num - lastWorld);
					if (k2 > 0 && num2 > 0f && num2 < 1f)
					{
						step = Mathf.Lerp(step, num2, 0.3f);
					}
					lastWorld = num;
					FightKeepAfter(you);
					FightUnburn(you);
					FightHoldFire(you.current_position, 5, 12);
					FightTopUp(you);
					if (PossessionCombat.Blows != blowsSeen)
					{
						blowsSeen = PossessionCombat.Blows;
						int num3 = FightIndex(PossessionCombat.LastBlowAt);
						if (num3 >= 0 && ++struck[num3] >= 2)
						{
							FightNearlyDown(num3);
						}
					}
					Vector2 current_position = you.current_position;
					Actor actor = null;
					Actor actor2 = null;
					float num4 = float.MaxValue;
					float num5 = float.MaxValue;
					for (int l = 0; l < n; l++)
					{
						if (fell[l])
						{
							continue;
						}
						if (!FightStanding(l))
						{
							fell[l] = true;
							down++;
							fallen = last[l];
							continue;
						}
						Actor actor3 = _fightFoes[l];
						last[l] = actor3.current_position;
						float magnitude = (last[l] - current_position).magnitude;
						if (magnitude < num4)
						{
							num4 = magnitude;
							actor = actor3;
						}
						float num6 = WindUps.Left(actor3);
						if (num6 >= 0f && num6 / Mathf.Max(0.001f, step) < num5)
						{
							num5 = num6 / Mathf.Max(0.001f, step);
							actor2 = actor3;
						}
					}
					if (actor == null && allDown < 0)
					{
						allDown = k2;
						tail = Mathf.Clamp(270 - k2, 45, 90);
					}
					if ((allDown >= 0 && k2 >= allDown + tail) || (allDown < 0 && k2 >= 360))
					{
						break;
					}
					if (actor == null)
					{
						PossessionHooks.ScriptedInput = Vector2.zero;
						if (guardUp)
						{
							Mod.Guard.HeldForTest = false;
							guardUp = false;
						}
						if (k2 - allDown < tail - 25)
						{
							FightLookAt(new Vector3(fallen.x, fallen.y, Mod.GroundAt(fallen) + 0.15f), 0.06f);
						}
						else
						{
							_fightPitch = Mathf.Lerp(_fightPitch, -2f, 0.06f);
							Mod.SetPitchDegrees(_fightPitch);
						}
						yield return Shoot(folder, k2, hud: true);
					}
					else
					{
						int num7 = FightIndex(guardFoe);
						Actor actor4 = ((guardUp && num7 >= 0 && FightStanding(num7)) ? guardFoe : ((actor2 != null && num5 < 6f) ? actor2 : actor));
						float magnitude2 = (actor4.current_position - current_position).magnitude;
						FightTurnTo(actor4.current_position, Mathf.Lerp(-8f, -3f, Mathf.Clamp01((magnitude2 - 1f) / 5f)), (k2 < 15) ? 0.1f : 0.2f);
						PossessionHooks.ScriptedInput = ((!guardUp && num4 > 1.3f && k2 >= 10) ? new Vector2(0f, 1f) : Vector2.zero);
						StrikeState state = Mod.Melee.State;
						bool flag = state.Swinging && state.Time < state.Timing.StrikeEnd;
						if (!guardUp && actor2 != null && num5 <= 2.2f && !flag && k2 - guardEnd >= 8)
						{
							Mod.Guard.HeldForTest = true;
							Mod.Guard.PressedForTest = true;
							guardUp = true;
							guardAt = k2;
							guardFoe = actor2;
							guardSerial = Blocks.Newest;
							guards++;
						}
						else if (guardUp)
						{
							BlockEvent blockEvent = FightStopOnYou(guardSerial);
							if (blockEvent != null)
							{
								guardSerial = blockEvent.Serial;
								stoppedAt = k2;
								if (blockEvent.Kind == Stop.Parried)
								{
									riposte = true;
								}
							}
							if (riposte || (stoppedAt >= guardAt && k2 - stoppedAt >= 3) || k2 - guardAt >= 20)
							{
								Mod.Guard.HeldForTest = false;
								guardUp = false;
								guardEnd = k2;
							}
						}
						if (!guardUp && !state.Swinging)
						{
							Actor actor5 = FightInReach(you);
							bool num8 = ((actor5 != null) ? (FightIndex(actor5) >= 0) : (num4 < 1.5f));
							bool flag2 = actor2 != null && num5 < 5f && !riposte;
							if (num8 && (riposte || (k2 - lastSwing >= 14 && !flag2)))
							{
								Mod.Melee.PressForTest();
								lastSwing = k2;
								swings++;
								riposte = false;
							}
						}
						yield return Shoot(folder, k2, hud: true);
					}
					k2++;
				}
				PossessionHooks.ScriptedInput = Vector2.zero;
				Mod.Guard.HeldForTest = false;
				int num9 = PossessionCombat.Blows - blows0;
				int num10 = GuardControl.Parried - parried0 + GuardControl.Blocked - blocked0;
				Cut("sword", k2, $"{weapon}: {swings} swings, {num9} blows, hits {fb.Hits - hits0}; {WindUps.Begun - winds0} wind-ups at you, {guards} guards raised: " + $"{GuardControl.Parried - parried0} parried, {GuardControl.Blocked - blocked0} blocked, {GuardControl.Broken - broken0} broken, " + $"{GuardControl.Ripostes - ripostes0} ripostes; {down} of {n} bandits down, kills {fb.Kills - kills0}, cut in two {Slicing.Cuts - cuts0}");
				Check("sword: blows struck", num9 > 0, num9.ToString());
				Check("sword: a blow of theirs stopped on the guard", num10 > 0, $"{guards} guards raised");
			}
			finally
			{
				PlayReelTest playReelTest = this;
				playReelTest.FightCleanup(you, youId);
				playReelTest.Mod.Holstered = holstered;
				playReelTest.Mod.Settings.ParryTiming = savedTiming;
				playReelTest.Mod.Settings.EnemyWindups = savedWindups;
				FightPutOut(home, 18);
				try
				{
					if (you != null)
					{
						FightPutOut(you.current_position, 18);
					}
				}
				catch
				{
				}
				playReelTest.Hour(1);
			}
		}

		private bool FightSpot(float run, float avoid, out Vector2 at, out float yaw)
		{
			at = _centre;
			yaw = 0f;
			float num = float.MaxValue;
			for (int i = 0; i < 24; i++)
			{
				float num2 = (float)i * MathF.PI / 12f;
				if (!float.IsNaN(avoid) && Mathf.Abs(Mathf.DeltaAngle(num2 * 57.29578f, avoid * 57.29578f)) < 50f)
				{
					continue;
				}
				Vector2 vector = new Vector2(Mathf.Cos(num2), Mathf.Sin(num2));
				Vector2 vector2 = new Vector2(0f - vector.y, vector.x);
				for (float num3 = 12f; num3 <= 22f; num3 += 2.5f)
				{
					Vector2 vector3 = _centre + vector * num3;
					vector3 = new Vector2((float)(int)vector3.x + 0.5f, (float)(int)vector3.y + 0.5f);
					int num4 = 0;
					int num5 = 0;
					int num6 = int.MaxValue;
					int num7 = int.MinValue;
					bool flag = true;
					for (int j = -2; j <= (int)run && flag; j++)
					{
						for (int k = -2; k <= 2 && flag; k += 2)
						{
							Vector2 vector4 = vector3 + vector * j + vector2 * k;
							WorldTile tile = World.world.GetTile((int)vector4.x, (int)vector4.y);
							if (tile == null || tile.main_type == null)
							{
								flag = false;
								break;
							}
							if (tile.main_type.liquid || tile.main_type.block)
							{
								if (j <= 3)
								{
									flag = false;
								}
								num4++;
								continue;
							}
							num6 = Mathf.Min(num6, tile.main_type.height_min);
							num7 = Mathf.Max(num7, tile.main_type.height_min);
							Building building = tile.building;
							if (building != null && building.asset != null)
							{
								BuildingType building_type = building.asset.building_type;
								if (building_type == BuildingType.Building_Tree || building_type == BuildingType.Building_Mineral || building_type == BuildingType.Building_Plant)
								{
									num5++;
								}
								else if (AssetStyle.IsSolid(building))
								{
									flag = false;
								}
							}
						}
					}
					if (flag && num6 <= num7)
					{
						float num8 = (float)num4 * 100f + (float)(num7 - num6) + (float)num5 * 2f + num3;
						if (num8 < num)
						{
							num = num8;
							at = vector3;
							yaw = num2;
						}
					}
				}
			}
			return num < float.MaxValue;
		}

		private static void FightClearLane(Vector2 at, Vector2 dir, float run, float bare)
		{
			for (float num = -3f; num <= bare; num += 3f)
			{
				Vector2 vector = at + dir * num;
				CivilTest.ClearBox((int)vector.x - 3, (int)vector.y - 3, (int)vector.x + 3, (int)vector.y + 3);
			}
			Vector2 vector2 = new Vector2(0f - dir.y, dir.x);
			for (float num2 = 0f; num2 <= run + 1f; num2 += 0.5f)
			{
				for (float num3 = -3f; num3 <= 3f; num3 += 0.5f)
				{
					Vector2 vector3 = at + dir * num2 + vector2 * num3;
					WorldTile tile = World.world.GetTile((int)vector3.x, (int)vector3.y);
					if (tile == null || tile.building == null || tile.building.asset == null)
					{
						continue;
					}
					BuildingType building_type = tile.building.asset.building_type;
					if (building_type == BuildingType.Building_Tree || building_type == BuildingType.Building_Mineral)
					{
						try
						{
							tile.building.removeBuildingFinal();
						}
						catch
						{
						}
					}
				}
			}
		}

		private Actor FightHero(Vector2 at)
		{
			Actor actor = _fightHero;
			bool flag = false;
			try
			{
				flag = actor != null && actor.isAlive() && actor.data.id == _fightHeroId && !actor.is_inside_building;
			}
			catch
			{
			}
			if (!flag)
			{
				actor = CivilTest.SpawnNear("human", at);
				if (actor == null)
				{
					return null;
				}
				try
				{
					if (_town != null && actor.city != _town)
					{
						actor.joinCity(_town);
					}
				}
				catch (Exception ex)
				{
					Note("fight: joining the town: " + ex.Message);
				}
				_fightHero = actor;
				try
				{
					_fightHeroId = actor.data.id;
				}
				catch
				{
					_fightHeroId = -1L;
				}
			}
			actor.current_position = at;
			actor.dirty_current_tile = true;
			try
			{
				actor.cancelAllBeh();
				actor.makeWait(4f);
				((BaseSimObject)actor).addStatusEffect("invincible", 300f, pColorEffect: false);
			}
			catch
			{
			}
			return actor;
		}

		private Actor FightBandit(Vector2 at, string weapon, Actor you)
		{
			Actor actor = CivilTest.SpawnNear("bandit", at);
			if (actor == null)
			{
				return null;
			}
			if (!Equip(actor, EquipmentType.Weapon, weapon))
			{
				Equip(actor, EquipmentType.Weapon, "sword_iron");
			}
			try
			{
				long id = actor.data.id;
				actor.dirty_current_tile = true;
				actor.restoreHealth(actor.getMaxHealth());
				string[] array = new string[4] { "block", "dodge", "deflect_projectile", "slow" };
				foreach (string pTraitID in array)
				{
					if (actor.hasTrait(pTraitID))
					{
						actor.removeTrait(pTraitID);
					}
				}
				actor.attack_timer = 1f;
				actor.startFightingWith(you);
				_fightFoes.Add(actor);
				_fightIds.Add(id);
				return actor;
			}
			catch (Exception ex)
			{
				Note("fight: bandit: " + ex.Message);
				CivilTest.Kill(actor);
				return null;
			}
		}

		private bool FightSame(int i)
		{
			try
			{
				Actor actor = _fightFoes[i];
				return actor != null && actor.data != null && actor.data.id == _fightIds[i];
			}
			catch
			{
				return false;
			}
		}

		private bool FightStanding(int i)
		{
			try
			{
				return FightSame(i) && _fightFoes[i].isAlive();
			}
			catch
			{
				return false;
			}
		}

		private int FightIndex(Actor a)
		{
			if (a == null)
			{
				return -1;
			}
			for (int i = 0; i < _fightFoes.Count; i++)
			{
				if (_fightFoes[i] == a && FightSame(i))
				{
					return i;
				}
			}
			return -1;
		}

		private void FightNearlyDown(int i)
		{
			try
			{
				Actor actor = _fightFoes[i];
				if (FightStanding(i) && actor.data.health > 1)
				{
					actor.data.health = 1;
				}
			}
			catch
			{
			}
		}

		private void FightKeepAfter(Actor you)
		{
			for (int i = 0; i < _fightFoes.Count; i++)
			{
				if (!FightStanding(i))
				{
					continue;
				}
				try
				{
					Actor actor = _fightFoes[i];
					if (!actor.has_attack_target || actor.attack_target != you)
					{
						actor.startFightingWith(you);
					}
				}
				catch
				{
				}
			}
		}

		private void FightCleanup(Actor you, long youId)
		{
			Time.captureFramerate = 0;
			PossessionHooks.ScriptedInput = Vector2.zero;
			try
			{
				Mod.Bow.HeldForTest = false;
				Mod.Melee.HeldForTest = false;
				Mod.Guard.HeldForTest = false;
				Mod.Guard.PressedForTest = false;
			}
			catch
			{
			}
			for (int i = 0; i < _fightFoes.Count; i++)
			{
				if (FightStanding(i))
				{
					CivilTest.Kill(_fightFoes[i]);
				}
			}
			_fightFoes.Clear();
			_fightIds.Clear();
			bool flag = false;
			try
			{
				flag = you != null && you != _king && you.isAlive() && you.data.id == youId;
			}
			catch
			{
			}
			if (!flag)
			{
				return;
			}
			try
			{
				ActorEquipmentSlot slot = you.equipment.getSlot(EquipmentType.Weapon);
				if (slot.getItem() != null)
				{
					slot.takeAwayItem();
				}
				you.setStatsDirty();
			}
			catch
			{
			}
			try
			{
				you.finishStatusEffect("invincible");
			}
			catch
			{
			}
		}

		private IEnumerator FightSettle(Actor you, Vector2 at)
		{
			for (int i = 0; i < 400; i++)
			{
				if (!Mod.Diving)
				{
					break;
				}
				yield return null;
			}
			if (Mod.Interior.Inside)
			{
				Mod.ExitHouseForTest();
				yield return CivilTest.Wait(1f);
			}
			yield return FightHold(you, at, 1.2f);
		}

		private IEnumerator FightHold(Actor you, Vector2 at, float seconds)
		{
			float t = 0f;
			for (int i = 0; i < 600; i++)
			{
				if (!(t < seconds))
				{
					break;
				}
				FightPin(you, at);
				yield return null;
				t += Time.unscaledDeltaTime;
			}
		}

		private static void FightPin(Actor you, Vector2 at)
		{
			try
			{
				you.current_position = at;
				you.dirty_current_tile = true;
			}
			catch
			{
			}
		}

		private bool FightLineClear(Actor you, Actor target)
		{
			Vector2 current_position = you.current_position;
			Vector2 vector = target.current_position - current_position;
			float magnitude = vector.magnitude;
			if (magnitude < 1f)
			{
				return true;
			}
			Vector2 rhs = vector / magnitude;
			try
			{
				foreach (Actor simple in World.world.units.getSimpleList())
				{
					if (simple != null && simple != you && simple != target && !_fightFoes.Contains(simple) && simple.isAlive() && simple.city != null)
					{
						Vector2 lhs = simple.current_position - current_position;
						float num = Vector2.Dot(lhs, rhs);
						if (!(num < 0.6f) && !(num > magnitude + 0.5f) && Mathf.Abs(lhs.x * rhs.y - lhs.y * rhs.x) < 0.9f)
						{
							return false;
						}
					}
				}
			}
			catch
			{
			}
			return true;
		}

		private static Actor FightInReach(Actor you)
		{
			try
			{
				return FirstPersonAim.Melee(you, PossessionCombat.ReachOf(you));
			}
			catch
			{
				return null;
			}
		}

		private static BlockEvent FightStopOnYou(int after)
		{
			foreach (BlockEvent @event in Blocks.Events)
			{
				if (@event.Serial > after && @event.OnYou)
				{
					return @event;
				}
			}
			return null;
		}

		private static double FightNow()
		{
			try
			{
				return World.world.getCurWorldTime();
			}
			catch
			{
				return 0.0;
			}
		}

		private static void FightTopUp(Actor you)
		{
			try
			{
				int num = Mathf.RoundToInt(((BaseSimObject)you).stats["stamina"]);
				if ((float)you.getStamina() < (float)num * 0.4f)
				{
					you.setStamina(num);
				}
			}
			catch
			{
			}
		}

		private static void FightUnburn(Actor a)
		{
			try
			{
				if (((BaseSimObject)a).hasAnyStatusEffect() && ((BaseSimObject)a).hasStatus("burning"))
				{
					a.finishStatusEffect("burning");
				}
			}
			catch
			{
			}
		}

		private static void FightHoldFire(Vector2 at, int keep, int reach)
		{
			int num = (int)at.x;
			int num2 = (int)at.y;
			for (int i = -reach; i <= reach; i++)
			{
				for (int j = -reach; j <= reach; j++)
				{
					if (j * j + i * i <= keep * keep)
					{
						continue;
					}
					WorldTile tile = World.world.GetTile(num + j, num2 + i);
					try
					{
						if (tile != null && tile.isOnFire())
						{
							tile.stopFire();
						}
					}
					catch
					{
					}
				}
			}
		}

		private static void FightPutOut(Vector2 at, int r)
		{
			int num = (int)at.x;
			int num2 = (int)at.y;
			for (int i = -r; i <= r; i++)
			{
				for (int j = -r; j <= r; j++)
				{
					WorldTile tile = World.world.GetTile(num + j, num2 + i);
					try
					{
						if (tile != null && tile.isOnFire())
						{
							tile.stopFire();
						}
					}
					catch
					{
					}
				}
			}
			float num3 = r * r;
			try
			{
				foreach (Actor item in new List<Actor>(World.world.units.getSimpleList()))
				{
					try
					{
						if (item != null && item.isAlive() && (item.current_position - at).sqrMagnitude < num3)
						{
							FightUnburn(item);
						}
					}
					catch
					{
					}
				}
				foreach (Building item2 in new List<Building>(World.world.buildings.getSimpleList()))
				{
					try
					{
						if (item2 != null && item2.isAlive() && item2.current_tile != null && (new Vector2(item2.current_tile.x, item2.current_tile.y) - at).sqrMagnitude < num3 && ((BaseSimObject)item2).hasAnyStatusEffect() && ((BaseSimObject)item2).hasStatus("burning"))
						{
							item2.finishStatusEffect("burning");
						}
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
		}

		private Vector3 FightChest(Actor a)
		{
			if (Hitboxes.Get(a, out var box))
			{
				return new Vector3(box.Centre.X, box.Centre.Y, box.Centre.Z + box.HalfUp * 0.45f);
			}
			Vector2 current_position = a.current_position;
			return new Vector3(current_position.x, current_position.y, Mod.GroundAt(current_position) + 0.55f);
		}

		private void FightAngles(Vector3 p, out float yaw, out float pitch)
		{
			Vector3 eyeForTest = Mod.EyeForTest;
			float num = p.x - eyeForTest.x;
			float num2 = p.y - eyeForTest.y;
			float num3 = Mathf.Max(0.05f, Mathf.Sqrt(num * num + num2 * num2));
			yaw = Mathf.Atan2(num2, num) * 57.29578f;
			pitch = Mathf.Clamp(Mathf.Atan((p.z - eyeForTest.z) / num3) * 57.29578f, -40f, 30f);
		}

		private void FightLookAt(Vector3 p, float ease)
		{
			FightAngles(p, out var yaw, out var pitch);
			FightEaseYaw(yaw, ease);
			_fightPitch = Mathf.Lerp(_fightPitch, pitch, ease);
			Mod.SetPitchDegrees(_fightPitch);
		}

		private void FightTurnTo(Vector2 at, float pitchDegrees, float ease)
		{
			Vector3 eyeForTest = Mod.EyeForTest;
			Vector2 vector = at - new Vector2(eyeForTest.x, eyeForTest.y);
			if (vector.sqrMagnitude > 0.0001f)
			{
				FightEaseYaw(Mathf.Atan2(vector.y, vector.x) * 57.29578f, ease);
			}
			_fightPitch = Mathf.Lerp(_fightPitch, pitchDegrees, ease);
			Mod.SetPitchDegrees(_fightPitch);
		}

		private void FightEaseYaw(float want, float ease)
		{
			float num = Mod.ViewYaw * 57.29578f;
			float num2 = Mathf.Clamp(Mathf.DeltaAngle(num, want) * ease, -4.5f, 4.5f);
			Mod.SetYaw((num + num2) * (MathF.PI / 180f));
		}

		private void FightPan(float fromYaw, float fromPitch, Vector3 to, float s)
		{
			FightAngles(to, out var yaw, out var pitch);
			Mod.SetYaw(Mathf.LerpAngle(fromYaw * 57.29578f, yaw, s) * (MathF.PI / 180f));
			_fightPitch = Mathf.Lerp(fromPitch, pitch, s);
			Mod.SetPitchDegrees(_fightPitch);
		}

		private float FightOff(Vector3 p)
		{
			FightAngles(p, out var yaw, out var pitch);
			return Mathf.Max(Mathf.Abs(Mathf.DeltaAngle(Mod.ViewYaw * 57.29578f, yaw)), Mathf.Abs(pitch - _fightPitch));
		}

		private static float FightSmooth(float x)
		{
			x = Mathf.Clamp01(x);
			return x * x * (3f - 2f * x);
		}

		private IEnumerator TownWalk()
		{
			Actor you = TownBody(null);
			if (!Check("walk: a villager to be", you != null))
			{
				yield break;
			}
			Hour(2);
			yield return Possess(you);
			if (Mod.Interior.Inside)
			{
				Mod.ExitHouseForTest();
				yield return CivilTest.Wait(0.6f);
			}
			if (!Check("walk: in their eyes", InControl(you)))
			{
				yield break;
			}
			TownStreetInfo st = TownStreet();
			Check("walk: a street to walk", st != null, (st != null) ? TownDescribe(st) : "none by the village: a walk to its middle instead");
			yield return TownMeasure(you, st);
			float pace = Mathf.Min(_townPace, 2.4f);
			Vector2 end;
			Vector2 start;
			if (st != null)
			{
				float num = Mathf.Min(pace * 255f / 30f, st.Half * 2f - 1.5f);
				end = st.At(st.Half - 1f);
				start = st.At(st.Half - 1f - num);
				TownFolk(st, you, null, st.AlongOf(start) + 2f, st.AlongOf(end), soldiersFirst: false);
			}
			else
			{
				start = you.current_position;
				Vector2 vector = _centre - start;
				if (vector.sqrMagnitude < 4f)
				{
					vector = Vector2.right;
				}
				end = start + vector.normalized * pace * 255f / 30f;
			}
			bool glide = st != null;
			Vector2 way = (end - start).normalized;
			float len = (end - start).magnitude;
			float pathYaw = Mathf.Atan2(way.y, way.x) * 57.29578f;
			TownPut(you, start);
			Mod.SetYaw(pathYaw * (MathF.PI / 180f));
			Mod.SetPitchDegrees(-3f);
			yield return CivilTest.Wait(1.2f);
			string dir = Clip("walk");
			bool mouse = Mod.MouseLookOff;
			int shot = 0;
			int looked = 0;
			float s = 0f;
			float yaw = pathYaw;
			float pitch = -3f;
			float real = 0f;
			Building house = null;
			Actor someone = null;
			Mod.MouseLookOff = true;
			TownClockOn();
			Rolling();
			try
			{
				for (int k = 0; k < 315; k++)
				{
					if (k < 255 && s < len - 0.01f)
					{
						float num2 = Mathf.Min(1f, 0.3f + (float)k / 20f) * Mathf.Clamp((len - s) / (pace * 0.7f), 0.2f, 1f);
						s = Mathf.Min(len, s + pace / 30f * num2);
						PossessionHooks.ScriptedInput = TownKeys(way, yaw);
					}
					else
					{
						PossessionHooks.ScriptedInput = Vector2.zero;
					}
					if (glide)
					{
						TownPut(you, start + way * s);
					}
					else if (k < 255)
					{
						PossessionHooks.ScriptedInput = TownKeys((end - you.current_position).normalized, yaw);
					}
					Vector2 current_position = you.current_position;
					float b = pathYaw;
					float b2 = -3f;
					if (k == 45)
					{
						house = TownHouseAhead(st, current_position, way, 1f);
					}
					if (k == 150)
					{
						someone = TownSomeoneAhead(you, current_position, way);
						house = ((someone == null) ? TownHouseAhead(st, current_position, way, -1f) : null);
					}
					if (k >= 45 && k < 110 && house != null)
					{
						b = TownBearing(pathYaw, current_position, TownMiddle(house), 70f);
						b2 = 4f;
						if (k == 45)
						{
							looked++;
						}
					}
					else if (k >= 150 && k < 215 && someone != null && someone.isAlive())
					{
						b = TownBearing(pathYaw, current_position, someone.current_position, 70f);
						b2 = TownPitchTo(someone.current_position, 0.8f, current_position);
						if (k == 150)
						{
							looked++;
						}
					}
					else if (k >= 150 && k < 215 && house != null)
					{
						b = TownBearing(pathYaw, current_position, TownMiddle(house), 70f);
						b2 = 4f;
						if (k == 150)
						{
							looked++;
						}
					}
					else if (k >= 255)
					{
						b = pathYaw + 125f;
						b2 = -2f;
					}
					yaw = Mathf.LerpAngle(yaw, b, (k >= 255) ? 0.045f : 0.05f);
					pitch = Mathf.Lerp(pitch, b2, 0.06f);
					Mod.SetYaw(yaw * (MathF.PI / 180f));
					Mod.SetPitchDegrees(pitch);
					real += Time.unscaledDeltaTime;
					yield return Shoot(dir, k, hud: true);
					shot++;
				}
			}
			finally
			{
				TownEnd(mouse);
			}
			Cut("walk", shot, string.Format("walked {0:F1} of {1:F1} tiles at {2:F2} a second{3}, looked at {4} on the way, {5}", (you.current_position - start).magnitude, len, pace, glide ? "" : " (by the keys)", looked, TownReal(real, shot)));
		}

		private IEnumerator TownHouse()
		{
			Actor you = TownBody(null);
			if (!Check("house: a villager to be", you != null))
			{
				yield break;
			}
			Hour(1);
			yield return Possess(you);
			if (Mod.Interior.Inside)
			{
				Mod.ExitHouseForTest();
				yield return CivilTest.Wait(0.6f);
			}
			if (!Check("house: in their eyes", InControl(you)))
			{
				yield break;
			}
			TownStreetInfo st = TownStreet();
			yield return TownMeasure(you, st);
			Building b = ((st != null && TownStanding(st.Door)) ? st.Door : null);
			Building extra = null;
			if (b == null)
			{
				List<Building> list = new List<Building>();
				for (int i = 0; i < 6; i++)
				{
					if (b != null)
					{
						break;
					}
					Building building = AddHouse("house_human_2", _town);
					if (building == null)
					{
						break;
					}
					Vector2 vector = TownMiddle(building);
					if (TownOnLand(vector + new Vector2(0f, -3.5f)) && TownOnLand(vector + new Vector2(0f, -5f)))
					{
						b = building;
					}
					else
					{
						list.Add(building);
					}
				}
				foreach (Building item in list)
				{
					try
					{
						item.removeBuildingFinal();
					}
					catch
					{
					}
				}
				extra = b;
				Note($"house: no house of the street's to use, one built aside ({list.Count} spots before it faced water or rock)");
			}
			bool mouse = Mod.MouseLookOff;
			int windowView = Mod.Settings.WindowView;
			try
			{
				if (!Check("house: a house to walk into", b != null && HouseInterior.IsEnterable(b) && HouseInterior.WalksIn(b.asset)))
				{
					yield break;
				}
				Mod.Settings.WindowView = 1;
				try
				{
					Homes.SetOwner(b, you);
				}
				catch (Exception ex)
				{
					Note("house: owner " + ex.Message);
				}
				Vector2 vector2 = TownMiddle(b);
				Vector2 at = ((st != null && b == st.Door) ? st.At(st.AlongOf(vector2)) : (vector2 + new Vector2(0f, -4.5f)));
				TownPut(you, at);
				Mod.SetYaw(Mathf.Atan2(vector2.y - at.y, vector2.x - at.x));
				Mod.SetPitchDegrees(-2f);
				HouseWorld.Placement p = default(HouseWorld.Placement);
				for (float t = 0f; t < 4f; t += 0.2f)
				{
					if (HouseWorld.Of(b, out p))
					{
						break;
					}
					yield return CivilTest.Wait(0.2f);
				}
				yield return CivilTest.Wait(0.4f);
				if (!Check("house: drawn where it stands", HouseWorld.Of(b, out p)))
				{
					yield break;
				}
				HousePlan housePlan = null;
				try
				{
					housePlan = HouseWorld.PlanFor(p);
				}
				catch (Exception ex2)
				{
					Note("house: plan " + ex2.Message);
				}
				if (!Check("house: its model measured", housePlan != null, HousePlan.Why))
				{
					yield break;
				}
				float num = (p.Mirror ? (0f - p.Scale) : p.Scale);
				Vector2 face = new Vector2(p.X + housePlan.Door.X * num, p.Y + housePlan.Door.Y * p.Scale);
				Vector2 outw = new Vector2(housePlan.Door.OutX * (p.Mirror ? (-1f) : 1f), housePlan.Door.OutY).normalized;
				Vector2 vector3 = new Vector2(0f - outw.y, outw.x);
				Vector2 start = face + outw * 3.5f + vector3 * 1.3f;
				if (!TownOnLand(start))
				{
					start = face + outw * 3.5f - vector3 * 1.3f;
				}
				if (!TownOnLand(start))
				{
					start = face + outw * 3.2f;
				}
				if (!Check("house: a street before its door", TownOnLand(start) && TownOnLand(face + outw * 1.4f), $"door at {face}, out {outw}"))
				{
					yield break;
				}
				TownPut(you, face + outw * 1.6f);
				Mod.SetYaw(Mathf.Atan2(0f - outw.y, 0f - outw.x));
				float t2 = Time.unscaledTime;
				while (Time.unscaledTime - t2 < 6f && (!Mod.DoorRoom.Inside || !Mod.DoorRoom.Fitted || Mod.DoorRoom.House != b))
				{
					yield return CivilTest.Wait(0.1f);
				}
				Check("house: its room ready at the door", Mod.DoorRoom.Inside && Mod.DoorRoom.Fitted && Mod.DoorRoom.House == b, $"after {Time.unscaledTime - t2:F1} s");
				Vector2 doorAt = face + outw * 0.05f;
				TownClearWay(start, doorAt, you, null);
				TownPut(you, start);
				Vector2 way = (doorAt - start).normalized;
				float yaw = Mathf.Atan2(way.y, way.x) * 57.29578f;
				float pitch = -2f;
				Mod.SetYaw(yaw * (MathF.PI / 180f));
				Mod.SetPitchDegrees(pitch);
				yield return CivilTest.Wait(1.2f);
				string dir = Clip("house");
				int k = 0;
				int inStep = WorldBoxMod.DoorStepsIn;
				float len = (doorAt - start).magnitude;
				float s = 0f;
				float pace = Mathf.Min(_townPace, 2f) * 0.8f;
				float real = 0f;
				string how = "walked in";
				Mod.MouseLookOff = true;
				TownClockOn();
				Rolling();
				for (; k < 200; k++)
				{
					if (Mod.Interior.Inside)
					{
						break;
					}
					if (k < 150)
					{
						float num2 = Mathf.Min(1f, 0.3f + (float)k / 20f) * Mathf.Clamp((len - s) / 1.2f, 0.45f, 1f);
						s = Mathf.Min(len, s + pace / 30f * num2);
						TownPut(you, start + way * s);
					}
					PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
					Vector2 current_position = you.current_position;
					float num3 = Mathf.Clamp01(1f - Vector2.Dot(current_position - face, outw) / 2f);
					float b2 = Mathf.LerpAngle(Mathf.Atan2(face.y - current_position.y, face.x - current_position.x) * 57.29578f, Mathf.Atan2(0f - outw.y, 0f - outw.x) * 57.29578f, num3 * num3);
					yaw = Mathf.LerpAngle(yaw, b2, 0.1f);
					pitch = Mathf.Lerp(pitch, Mathf.Lerp(-1f, -5f, num3), 0.08f);
					Mod.SetYaw(yaw * (MathF.PI / 180f));
					Mod.SetPitchDegrees(pitch);
					real += Time.unscaledDeltaTime;
					yield return Shoot(dir, k, hud: true);
				}
				PossessionHooks.ScriptedInput = Vector2.zero;
				int tries;
				for (tries = 0; tries < 3; tries++)
				{
					if (Mod.Interior.Inside)
					{
						break;
					}
					how = "in by E at the door (didn't walk in)";
					try
					{
						Mod.EnterHouseForTest(b);
					}
					catch (Exception ex3)
					{
						Note("house: E at the door " + ex3.Message);
					}
					for (int j = 0; j < 15; j++)
					{
						if (Mod.Interior.Inside)
						{
							break;
						}
						yield return null;
					}
				}
				Check("house: walked in through the door", Mod.Interior.Inside && Mod.Interior.House == b && WorldBoxMod.DoorStepsIn > inStep, how);
				if (!Mod.Interior.Inside)
				{
					Cut("house", k, "never got in");
					yield break;
				}
				int inAt = k;
				Interior room = Mod.Interior.Room;
				Vector2 inWay = new Vector2(Mathf.Cos(yaw * (MathF.PI / 180f)), Mathf.Sin(yaw * (MathF.PI / 180f)));
				float moved = 0f;
				bool blocked = false;
				List<(float Bearing, Vector2 At, float Pitch)> looks = null;
				tries = 0;
				while (tries < 175 && Mod.Interior.Inside)
				{
					if (tries < 40 && moved < 1.1f && !blocked)
					{
						float num4 = 0.03666667f * Mathf.Min(1f, 0.4f + (float)tries / 10f) * Mathf.Clamp((1.1f - moved) / 0.5f, 0.25f, 1f);
						if (Mod.Interior.TryPut(Mod.Interior.Eye + inWay * num4))
						{
							moved += num4;
						}
						else
						{
							blocked = true;
						}
					}
					Vector2 eye = Mod.Interior.Eye;
					float b3 = Mathf.Atan2(inWay.y, inWay.x) * 57.29578f;
					float b4 = -6f;
					if (tries >= 40)
					{
						if (looks == null)
						{
							looks = TownRoomLooks(room, eye, yaw, Mod.View.Z);
						}
						(float, Vector2, float) tuple = looks[Mathf.Min(looks.Count - 1, (tries - 40) * looks.Count / 135)];
						b3 = Mathf.Atan2(tuple.Item2.y - eye.y, tuple.Item2.x - eye.x) * 57.29578f;
						b4 = tuple.Item3;
					}
					yaw = Mathf.LerpAngle(yaw, b3, 0.05f);
					pitch = Mathf.Lerp(pitch, b4, 0.06f);
					Mod.SetYaw(yaw * (MathF.PI / 180f));
					Mod.SetPitchDegrees(pitch);
					real += Time.unscaledDeltaTime;
					yield return Shoot(dir, k, hud: true);
					tries++;
					k++;
				}
				Cut("house", k, string.Format("{0} at frame {1}, {2}{3}, {4:F2} tiles in{5}, ", how, inAt, b.asset.id, (b == st?.Door) ? " of the street" : "", moved, blocked ? " (then something in the way)" : "") + $"looked at {looks?.Count ?? 0} things ({room.Windows.Count} windows, {room.Solids.Count} pieces of furniture), {TownReal(real, k)}");
			}
			finally
			{
				PlayReelTest playReelTest = this;
				playReelTest.TownEnd(mouse);
				if (playReelTest.Mod.Interior.Inside)
				{
					playReelTest.Mod.LeaveRoomForTest();
				}
				playReelTest.Mod.Settings.WindowView = windowView;
				if (extra != null)
				{
					try
					{
						extra.removeBuildingFinal();
					}
					catch
					{
					}
				}
			}
		}

		private static List<(float Bearing, Vector2 At, float Pitch)> TownRoomLooks(Interior room, Vector2 eye, float yawDeg, float eyeZ)
		{
			List<(float, Vector2, float)> list = new List<(float, Vector2, float)>();
			float num = ((room.Fit != null && room.Fit.StoreyZ.Length != 0) ? room.Fit.StoreyZ[0] : 0f);
			(float, Vector2, float) tuple = (0f, Vector2.zero, 0f);
			(float, Vector2, float) tuple2 = (0f, Vector2.zero, 0f);
			foreach (float[] solid in room.Solids)
			{
				if (solid.Length >= 6 && Mathf.Abs(solid[4] - num) > 0.3f)
				{
					continue;
				}
				Vector2 vector = new Vector2((solid[0] + solid[2]) * 0.5f, (solid[1] + solid[3]) * 0.5f);
				float num2 = (solid[2] - solid[0]) * (solid[3] - solid[1]);
				if (!((vector - eye).magnitude < 0.6f) && !(num2 < 0.04f))
				{
					float num3 = Mathf.DeltaAngle(yawDeg, Mathf.Atan2(vector.y - eye.y, vector.x - eye.x) * 57.29578f);
					if (num3 > 15f && num3 < 120f && num2 > tuple.Item3)
					{
						tuple = (num3, vector, num2);
					}
					if (num3 < -15f && num3 > -120f && num2 > tuple2.Item3)
					{
						tuple2 = (num3, vector, num2);
					}
				}
			}
			(float, Vector2, float)[] array = new(float, Vector2, float)[2] { tuple, tuple2 };
			for (int i = 0; i < array.Length; i++)
			{
				(float, Vector2, float) tuple3 = array[i];
				if (tuple3.Item3 > 0f)
				{
					list.Add((tuple3.Item1, tuple3.Item2, Mathf.Clamp(Mathf.Atan2(num + 0.45f - eyeZ, (tuple3.Item2 - eye).magnitude) * 57.29578f, -22f, -5f)));
				}
			}
			float num4 = 150f;
			(float, Vector2, float) item = (0f, Vector2.zero, 0f);
			bool flag = false;
			foreach (LightWindow window in room.Windows)
			{
				if (!(window.Center.Z > num + 1.6f))
				{
					Vector2 vector2 = new Vector2(window.Center.X, window.Center.Y);
					float num5 = Mathf.DeltaAngle(yawDeg, Mathf.Atan2(vector2.y - eye.y, vector2.x - eye.x) * 57.29578f);
					if (!(Mathf.Abs(num5) >= num4))
					{
						num4 = Mathf.Abs(num5);
						item = (num5, vector2, Mathf.Clamp(Mathf.Atan2(window.Center.Z - eyeZ, Mathf.Max(0.5f, (vector2 - eye).magnitude)) * 57.29578f, -8f, 12f));
						flag = true;
					}
				}
			}
			if (flag)
			{
				list.Add(item);
			}
			int num6 = 1;
			while (list.Count < 2)
			{
				float f = (yawDeg + 55f * (float)num6) * (MathF.PI / 180f);
				list.Add((55f * (float)num6, eye + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * 2f, -8f));
				num6 = -num6;
			}
			list.Sort(((float Bearing, Vector2 At, float Pitch) x, (float Bearing, Vector2 At, float Pitch) y) => y.Bearing.CompareTo(x.Bearing));
			return list;
		}

		private IEnumerator TownTalk()
		{
			TownPair(out var you, out var them, out var strangers);
			if (!Check("talk: two villagers, one to be and one to talk to", you != null && them != null))
			{
				yield break;
			}
			Hour(1);
			Actor[] array = new Actor[2] { you, them };
			foreach (Actor actor in array)
			{
				try
				{
					((BaseSimObject)actor).addStatusEffect("invincible", 600f, pColorEffect: false);
					if (actor.is_inside_building)
					{
						actor.exitBuilding();
					}
				}
				catch
				{
				}
			}
			yield return Possess(you);
			if (Mod.Interior.Inside)
			{
				Mod.ExitHouseForTest();
				yield return CivilTest.Wait(0.6f);
			}
			if (!Check("talk: in their eyes", InControl(you)))
			{
				yield break;
			}
			Mod.EndConversation();
			TownStreetInfo st = TownStreet();
			yield return TownMeasure(you, st);
			Vector2 way;
			Vector2 spot;
			if (st != null)
			{
				way = st.Along;
				spot = st.At(-3f, 0.2f);
			}
			else
			{
				way = Vector2.right;
				spot = you.current_position + way * 4.4f;
				for (int j = 0; j < 8; j++)
				{
					if (DryAt(spot) && DryAt(spot - way * 4.4f))
					{
						break;
					}
					float f = (float)j * MathF.PI / 4f;
					way = new Vector2(Mathf.Cos(f), Mathf.Sin(f));
					spot = you.current_position + way * 4.4f;
				}
			}
			Vector2 start = spot - way * 4.4f;
			Vector2 stop = spot - way * 1.35f;
			if (st != null)
			{
				TownFolk(st, you, them, st.AlongOf(spot) + 1.5f, st.AlongOf(spot) + 10f, soldiersFirst: false);
			}
			TownClearWay(start, spot, you, them);
			TownPut(you, start);
			TownHold(them, spot, start);
			Mod.SetYaw(Mathf.Atan2(way.y, way.x));
			Mod.SetPitchDegrees(-2f);
			yield return CivilTest.Wait(1.2f);
			string dir = Clip("talk");
			Conversation c = Mod.Converse;
			bool mouse = Mod.MouseLookOff;
			bool open = false;
			int k = 0;
			int arrived = -1;
			int opened = -1;
			int topics = -1;
			int steps = 0;
			float len = (stop - start).magnitude;
			float s = 0f;
			float pace = Mathf.Min(_townPace, 2f) * 0.7f;
			float real = 0f;
			float yaw = Mathf.Atan2(way.y, way.x) * 57.29578f;
			float pitch = -2f;
			string theirs = null;
			_townPage = -1;
			Mod.MouseLookOff = true;
			TownClockOn();
			Rolling();
			try
			{
				for (; k < 120; k++)
				{
					if (s < len - 0.005f)
					{
						float num = Mathf.Min(1f, 0.3f + (float)k / 20f) * Mathf.Clamp((len - s) / 0.9f, 0.2f, 1f);
						s = Mathf.Min(len, s + pace / 30f * num);
						PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
					}
					else
					{
						PossessionHooks.ScriptedInput = Vector2.zero;
						if (arrived < 0)
						{
							arrived = k;
						}
						if (k - arrived >= 8)
						{
							break;
						}
					}
					TownPut(you, start + way * s);
					Vector2 current_position = you.current_position;
					if (k % 10 == 0)
					{
						if ((them.current_position - spot).sqrMagnitude > 0.09f)
						{
							TownPut(them, spot);
						}
						try
						{
							them.makeWait();
							them.lookTowardsPosition(current_position);
						}
						catch
						{
						}
					}
					Vector2 current_position2 = them.current_position;
					yaw = Mathf.LerpAngle(yaw, Mathf.Atan2(current_position2.y - current_position.y, current_position2.x - current_position.x) * 57.29578f, 0.12f);
					pitch = Mathf.Lerp(pitch, TownPitchTo(current_position2, 0.8f, current_position), 0.12f);
					Mod.SetYaw(yaw * (MathF.PI / 180f));
					Mod.SetPitchDegrees(pitch);
					real += Time.unscaledDeltaTime;
					yield return Shoot(dir, k, hud: true);
				}
				PossessionHooks.ScriptedInput = Vector2.zero;
				try
				{
					open = (strangers ? Mod.HelloForTest(them) : Mod.StartConversationForTest(them)) && c.Open;
				}
				catch (Exception ex)
				{
					Note("talk: " + ex.Message);
				}
				bool flag = open && c.Stranger != null;
				Check("talk: E on them opens a talk", open, (!strangers) ? "known already: no hello" : (flag ? "a hello" : "no hello: known already"));
				if (open)
				{
					opened = k;
					for (int end = k + 180; k < end; k++)
					{
						if (!c.Open)
						{
							break;
						}
						TownTypeStep(c);
						if (theirs == null && !c.LineIsYours && c.Line != null)
						{
							theirs = c.Line;
						}
						if (c.MorePages && !c.LineIsYours && c.Typed && _townAge > (float)c.Line.Length / 55f + 1.2f)
						{
							c.Next();
						}
						if (topics < 0 && !c.LineIsYours && !c.MorePages && c.Typed && c.Choices.Count > 1)
						{
							topics = k;
						}
						if (topics >= 0)
						{
							int num2 = k - topics;
							if (num2 == 30 || num2 == 50)
							{
								WorldBoxMod.TalkWheelForTest = -1f;
								steps++;
							}
							if (num2 >= 80)
							{
								break;
							}
						}
						real += Time.unscaledDeltaTime;
						yield return Shoot(dir, k, hud: true);
					}
				}
			}
			finally
			{
				PlayReelTest playReelTest = this;
				playReelTest.TownEnd(mouse);
				playReelTest._townPage = -1;
			}
			string text = (c.Open ? c.Shown : null);
			int num3 = (c.Open ? c.Choices.Count : 0);
			int num4 = (c.Open ? c.Selected : (-1));
			Cut("talk", k, string.Format("{0}, their words \"{1}\", the card \"{2}\", {3} choices, the pick moved {4} to {5}, ", open ? $"talk open at frame {opened}" : "no talk", theirs, text, num3, steps, num4) + "talk camera " + (Mod.TalkHeadOnScreenForTest(them, out var x, out var y, out var card) ? $"their head at {x:F2},{y:F2} (card top {card:F2})" : "no head") + ", " + TownReal(real, k));
			Mod.EndConversation();
		}

		private void TownTypeStep(Conversation c)
		{
			if (c.Open)
			{
				if (c.Page != _townPage || (object)c.Line != _townLine)
				{
					_townPage = c.Page;
					_townLine = c.Line;
					_townAge = Mathf.Min(c.Age, 0.05f);
				}
				else
				{
					_townAge += 1f / 30f;
				}
				c.LineStart = Time.unscaledTime - _townAge;
			}
		}

		private void TownPair(out Actor you, out Actor them, out bool strangers)
		{
			you = (them = null);
			strangers = false;
			List<Actor> list = new List<Actor>();
			for (int i = 0; i < 2; i++)
			{
				foreach (Actor item in Founded)
				{
					if (item == null || item == _king || list.Contains(item))
					{
						continue;
					}
					try
					{
						if (!item.isAlive() || !item.isAdult() || item.isCityLeader() || (i == 0 && item.isWarrior()))
						{
							continue;
						}
						goto IL_0078;
					}
					catch
					{
					}
					continue;
					IL_0078:
					list.Add(item);
				}
			}
			foreach (Actor item2 in list)
			{
				if (!FirstPerson.Possess.CanJumpTo(item2))
				{
					continue;
				}
				foreach (Actor item3 in list)
				{
					if (item3 != item2)
					{
						bool flag = true;
						try
						{
							flag = PeopleMet.Knows(item2, item3) || item2.lover == item3;
						}
						catch
						{
						}
						if (!flag)
						{
							you = item2;
							them = item3;
							strangers = true;
							return;
						}
					}
				}
			}
			foreach (Actor h in list)
			{
				if (FirstPerson.Possess.CanJumpTo(h))
				{
					you = h;
					them = list.Find((Actor n) => n != h);
					break;
				}
			}
		}

		private IEnumerator TownTorch()
		{
			Actor you = TownBody(null);
			if (!Check("torch: a villager to be", you != null))
			{
				yield break;
			}
			bool mouse = Mod.MouseLookOff;
			Hour(3);
			SceneCollector.AllLit = true;
			Mod.TorchRainForTest = 0f;
			try
			{
				yield return Possess(you);
				if (Mod.Interior.Inside)
				{
					Mod.ExitHouseForTest();
					yield return CivilTest.Wait(0.6f);
				}
				if (!Check("torch: in their eyes", InControl(you)))
				{
					yield break;
				}
				TownStreetInfo st = TownStreet();
				yield return TownMeasure(you, st);
				bool up = false;
				try
				{
					if (Torches.Count(you) <= 0)
					{
						Supplies.Give(you, "tw_item_torch", 1);
					}
					TownUnarm(you);
					Torches.Out = false;
					Torches.SetLit(you, lit: true);
					Mod.Holstered = false;
					up = Torches.InMain(you) || Torches.Toggle(you, base.Note);
				}
				catch (Exception ex)
				{
					Note("torch: " + ex.Message);
				}
				float pace = Mathf.Min(_townPace * 0.8f, 1.9f);
				Vector2 start;
				Vector2 end;
				if (st != null)
				{
					float num = Mathf.Min(pace * 240f / 30f, st.Half * 2f - 1.5f);
					start = st.At(st.Half - 1f, 0.3f);
					end = st.At(st.Half - 1f - num, 0.3f);
					TownFolk(st, you, null, st.AlongOf(start) - 2f, st.AlongOf(end), soldiersFirst: true);
				}
				else
				{
					start = you.current_position;
					Vector2 vector = _centre - start;
					if (vector.sqrMagnitude < 4f)
					{
						vector = Vector2.left;
					}
					end = start + vector.normalized * pace * 240f / 30f;
				}
				bool glide = st != null;
				Vector2 way = (end - start).normalized;
				float len = (end - start).magnitude;
				float pathYaw = Mathf.Atan2(way.y, way.x) * 57.29578f;
				TownPut(you, start);
				Mod.SetYaw(pathYaw * (MathF.PI / 180f));
				Mod.SetPitchDegrees(-8f);
				yield return CivilTest.Wait(0.8f);
				Check("torch: in your hand", up && (Mod.HandsForTest.TorchShown || Mod.HandsForTest.TorchLeftShown), $"out {Torches.Out}, right {Mod.HandsForTest.TorchShown}, left {Mod.HandsForTest.TorchLeftShown}, lit {Torches.Lit(you)}");
				yield return CivilTest.Wait(1.2f);
				Check("torch: lit, its light on", Mod.TorchSpritesForTest >= 1 && Mod.TorchLightsForTest >= 1, $"{Mod.TorchSpritesForTest} flames, {Mod.TorchLightsForTest} lights");
				string dir = Clip("torch");
				int shot = 0;
				int others = 0;
				float s = 0f;
				float yaw = pathYaw;
				float pitch = -8f;
				float real = 0f;
				Building house = null;
				Mod.MouseLookOff = true;
				TownClockOn();
				Rolling();
				for (int k = 0; k < 270; k++)
				{
					if (k < 240 && s < len - 0.01f)
					{
						float num2 = Mathf.Min(1f, 0.3f + (float)k / 20f) * Mathf.Clamp((len - s) / (pace * 0.7f), 0.2f, 1f);
						s = Mathf.Min(len, s + pace / 30f * num2);
						PossessionHooks.ScriptedInput = TownKeys(way, yaw);
					}
					else
					{
						PossessionHooks.ScriptedInput = Vector2.zero;
					}
					if (glide)
					{
						TownPut(you, start + way * s);
					}
					else if (k < 240)
					{
						PossessionHooks.ScriptedInput = TownKeys((end - you.current_position).normalized, yaw);
					}
					Vector2 current_position = you.current_position;
					float b = pathYaw;
					float b2 = -9f;
					if (k == 50)
					{
						house = TownHouseAhead(st, current_position, way, 1f);
					}
					if (k == 150)
					{
						house = TownHouseAhead(st, current_position, way, -1f);
					}
					if (house != null && ((k >= 50 && k < 115) || (k >= 150 && k < 215)))
					{
						b = TownBearing(pathYaw, current_position, TownMiddle(house), 65f);
						b2 = 2f;
					}
					else if (k >= 215)
					{
						b2 = -12f;
					}
					yaw = Mathf.LerpAngle(yaw, b, 0.045f);
					pitch = Mathf.Lerp(pitch, b2, 0.05f);
					Mod.SetYaw(yaw * (MathF.PI / 180f));
					Mod.SetPitchDegrees(pitch);
					others = Mathf.Max(others, Mod.TorchOthersForTest);
					real += Time.unscaledDeltaTime;
					yield return Shoot(dir, k, hud: true);
					shot++;
				}
				Cut("torch", shot, $"walked {(you.current_position - start).magnitude:F1} of {len:F1} tiles, still lit {Torches.Lit(you)}, flames {Mod.TorchSpritesForTest}, " + $"its lights {Mod.TorchLightsForTest}, people's torch flames up to {others}, {TownReal(real, shot)}");
			}
			finally
			{
				PlayReelTest playReelTest = this;
				playReelTest.TownEnd(mouse);
				Torches.Out = false;
				playReelTest.Mod.TorchRainForTest = -1f;
				SceneCollector.AllLit = false;
			}
		}

		private static void TownUnarm(Actor a)
		{
			try
			{
				ActorEquipmentSlot slot = a.equipment.getSlot(EquipmentType.Weapon);
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

		private TownStreetInfo TownStreet()
		{
			if (_townStreet != null && !TownClear(_townStreet))
			{
				Note("street: built on since, another found");
				_townStreet = null;
				_townStreetTried = false;
			}
			if (!_townStreetTried)
			{
				_townStreetTried = true;
				try
				{
					_townStreet = TownStreetBuild();
				}
				catch (Exception ex)
				{
					Note("street failed: " + ex.Message);
					_townStreet = null;
				}
				if (_townStreet != null)
				{
					Note(TownDescribe(_townStreet));
				}
			}
			if (_townStreet != null)
			{
				TownClearTrees(_townStreet);
			}
			return _townStreet;
		}

		private TownStreetInfo TownStreetBuild()
		{
			int num = (int)_centre.x;
			int num2 = (int)_centre.y;
			float num3 = float.MaxValue;
			int num4 = 0;
			int num5 = 0;
			bool flag = false;
			bool flag2 = false;
			HashSet<Building> hashSet = new HashSet<Building>();
			for (int i = 0; i < 2; i++)
			{
				for (int j = -12; j <= 12; j++)
				{
					for (int k = -6; k <= 6; k += 2)
					{
						bool flag3 = i == 1;
						int num6 = (flag3 ? (num + j) : (num + k));
						int num7 = (flag3 ? (num2 + k) : (num2 + j));
						if (TownLine(num6, num7, flag3, out var trees, out var rise))
						{
							hashSet.Clear();
							TownBeside(num6, num7, flag3, hashSet);
							float num8 = (float)Mathf.Abs(j) + 0.5f * (float)Mathf.Abs(k) + (float)trees * 0.3f + rise * 2f - (float)Mathf.Min(hashSet.Count, 6) * 1.5f + (flag3 ? 2f : 0f);
							if (num8 < num3)
							{
								num3 = num8;
								num4 = num6;
								num5 = num7;
								flag = flag3;
								flag2 = true;
							}
						}
					}
				}
			}
			if (!flag2)
			{
				return null;
			}
			TownStreetInfo townStreetInfo = new TownStreetInfo
			{
				Mid = new Vector2((float)num4 + 0.5f, (float)num5 + 0.5f),
				Along = (flag ? Vector2.up : Vector2.right),
				Left = (flag ? Vector2.left : Vector2.up),
				Half = 12f,
				Vertical = flag
			};
			TownClearTrees(townStreetInfo);
			float num9 = 0f;
			for (int l = -12; l <= 12; l++)
			{
				num9 += TownGround(townStreetInfo.At(l));
			}
			num9 /= 25f;
			int[] array = new int[5] { -10, -5, 0, 5, 10 };
			string[] array2 = new string[5] { "house_human_1", "house_human_0", "house_human_2", "house_human_1", "house_human_0" };
			string[] array3 = new string[5] { "house_human_0", "house_human_2", "house_human_1", "house_human_2", "house_human_0" };
			for (int m = 0; m < array.Length; m++)
			{
				for (int num10 = 1; num10 >= -1; num10 -= 2)
				{
					Vector2 vector = townStreetInfo.At(array[m], 4f * (float)num10);
					int num11 = Mathf.RoundToInt(vector.x - 0.5f);
					int num12 = Mathf.RoundToInt(vector.y - 1.5f);
					if (!TownSlot(num11, num12, num9, out var there))
					{
						if (there != null && HouseInterior.IsEnterable(there) && !townStreetInfo.Houses.Contains(there))
						{
							townStreetInfo.Houses.Add(there);
						}
					}
					else
					{
						CivilTest.ClearBox(num11 - 1, num12, num11 + 1, num12 + 2);
						int turns = ((num10 > 0) ? (flag ? 1 : 0) : (flag ? 3 : 2));
						Building building = TownBuild((num10 > 0) ? array2[m] : array3[m], num11, num12, turns);
						if (building != null)
						{
							townStreetInfo.Built++;
							townStreetInfo.Houses.Add(building);
							if (m == 2 && num10 > 0)
							{
								townStreetInfo.Door = building;
							}
						}
					}
				}
			}
			return townStreetInfo;
		}

		private bool TownLine(int ox, int oy, bool vertical, out int trees, out float rise)
		{
			trees = 0;
			rise = 0f;
			float num = float.MaxValue;
			float num2 = float.MinValue;
			for (int i = -12; i <= 12; i++)
			{
				for (int j = -1; j <= 1; j++)
				{
					WorldTile worldTile = TownTile(ox, oy, vertical, i, j);
					if (!TownFloor(worldTile))
					{
						return false;
					}
					if (worldTile.building != null && AssetStyle.IsSolid(worldTile.building))
					{
						if (!TownFlora(worldTile.building))
						{
							return false;
						}
						trees++;
					}
				}
				float b = TownGround(new Vector2((float)(vertical ? ox : (ox + i)) + 0.5f, (float)(vertical ? (oy + i) : oy) + 0.5f));
				num = Mathf.Min(num, b);
				num2 = Mathf.Max(num2, b);
			}
			rise = num2 - num;
			return rise <= 1.5f;
		}

		private static void TownBeside(int ox, int oy, bool vertical, HashSet<Building> into)
		{
			for (int i = -11; i <= 11; i++)
			{
				for (int j = 2; j <= 7; j++)
				{
					for (int k = -1; k <= 1; k += 2)
					{
						WorldTile worldTile = TownTile(ox, oy, vertical, i, j * k);
						if (worldTile?.building != null && AssetStyle.IsSolid(worldTile.building) && !TownFlora(worldTile.building))
						{
							into.Add(worldTile.building);
						}
					}
				}
			}
		}

		private static WorldTile TownTile(int ox, int oy, bool vertical, int along, int across)
		{
			return World.world.GetTile(vertical ? (ox - across) : (ox + along), vertical ? (oy + along) : (oy + across));
		}

		private bool TownSlot(int tx, int ty, float level, out Building there)
		{
			there = null;
			for (int i = ty - 1; i <= ty + 3; i++)
			{
				for (int j = tx - 2; j <= tx + 2; j++)
				{
					WorldTile tile = World.world.GetTile(j, i);
					if (tile?.building != null && AssetStyle.IsSolid(tile.building) && !TownFlora(tile.building))
					{
						there = tile.building;
						return false;
					}
					if (j >= tx - 1 && j <= tx + 1 && i >= ty && i <= ty + 2 && (!TownFloor(tile) || Mathf.Abs(TownGround(new Vector2((float)j + 0.5f, (float)i + 0.5f)) - level) > 1.2f))
					{
						return false;
					}
				}
			}
			return true;
		}

		private Building TownBuild(string id, int x, int y, int turns)
		{
			WorldTile tile = World.world.GetTile(x, y);
			if (tile == null || _town == null || AssetManager.buildings.get(id) == null)
			{
				return null;
			}
			try
			{
				Building building = World.world.buildings.addBuilding(id, tile, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
				if (building == null)
				{
					return null;
				}
				try
				{
					if (tile.zone != null)
					{
						List<TileZone> list = new List<TileZone> { tile.zone };
						TileZone[] neighbours_all = tile.zone.neighbours_all;
						foreach (TileZone tileZone in neighbours_all)
						{
							if (tileZone != null)
							{
								list.Add(tileZone);
							}
						}
						foreach (TileZone item in list)
						{
							if (item.city == null)
							{
								_town.addZone(item);
							}
						}
					}
				}
				catch (Exception ex)
				{
					Note("street land: " + ex.Message);
				}
				try
				{
					building.completeConstruction();
					building.setKingdom(_town.kingdom);
				}
				catch
				{
				}
				if (building.city != _town)
				{
					_town.listBuilding(building);
				}
				if (turns != 0)
				{
					Facing.Set(building, turns);
				}
				return building;
			}
			catch (Exception ex2)
			{
				Note("street house " + id + ": " + ex2.Message);
				return null;
			}
		}

		private static bool TownClear(TownStreetInfo st)
		{
			for (int i = -(int)st.Half; (float)i <= st.Half; i++)
			{
				for (int j = -1; j <= 1; j++)
				{
					Vector2 vector = st.At(i, j);
					WorldTile tile = World.world.GetTile(Mathf.FloorToInt(vector.x), Mathf.FloorToInt(vector.y));
					if (tile == null)
					{
						return false;
					}
					Building building = tile.building;
					if (building == null || !AssetStyle.IsSolid(building) || TownFlora(building))
					{
						continue;
					}
					bool flag = false;
					try
					{
						flag = building.isUnderConstruction();
						if (flag)
						{
							building.removeBuildingFinal();
						}
					}
					catch
					{
						flag = false;
					}
					if (!flag)
					{
						return false;
					}
				}
			}
			return true;
		}

		private static void TownClearTrees(TownStreetInfo st)
		{
			Vector2 vector = st.At(0f - st.Half, -1f);
			Vector2 vector2 = st.At(st.Half, 1f);
			CivilTest.ClearBox(Mathf.FloorToInt(Mathf.Min(vector.x, vector2.x)), Mathf.FloorToInt(Mathf.Min(vector.y, vector2.y)), Mathf.FloorToInt(Mathf.Max(vector.x, vector2.x)), Mathf.FloorToInt(Mathf.Max(vector.y, vector2.y)));
		}

		private static string TownDescribe(TownStreetInfo st)
		{
			return string.Format("street {0} through {1}, {2:F0} tiles, {3} houses along it ({4} built for it), a door to walk in {5}", st.Vertical ? "north-south" : "east-west", st.Mid, st.Half * 2f, st.Houses.Count, st.Built, st.Door != null);
		}

		private Actor TownBody(Actor not)
		{
			Actor actor = null;
			if (not == null)
			{
				not = _townLastBody;
			}
			for (int i = 0; i < 2; i++)
			{
				if (actor != null)
				{
					break;
				}
				foreach (Actor item in Founded)
				{
					if (item == null || item == _king || item == not)
					{
						continue;
					}
					try
					{
						if (!item.isAlive() || !item.isAdult() || (i == 0 && item.isWarrior()) || !FirstPerson.Possess.CanJumpTo(item))
						{
							continue;
						}
						goto IL_0066;
					}
					catch
					{
					}
					continue;
					IL_0066:
					actor = item;
					break;
				}
			}
			if (actor == null && not != null && not == _townLastBody)
			{
				return TownBody(_king);
			}
			if (actor == null)
			{
				return null;
			}
			_townLastBody = actor;
			try
			{
				if (actor.is_inside_building)
				{
					actor.exitBuilding();
				}
				((BaseSimObject)actor).addStatusEffect("invincible", 600f, pColorEffect: false);
			}
			catch
			{
			}
			return actor;
		}

		private int TownFolk(TownStreetInfo st, Actor you, Actor not, float from, float to, bool soldiersFirst)
		{
			List<Actor> list = new List<Actor>();
			for (int i = 0; i < 3; i++)
			{
				foreach (Actor item in Founded)
				{
					if (item == null || item == you || item == not || list.Contains(item))
					{
						continue;
					}
					bool flag;
					bool flag2;
					try
					{
						if (!item.isAlive())
						{
							continue;
						}
						flag = item.isWarrior();
						flag2 = item.isAdult();
						goto IL_0064;
					}
					catch
					{
					}
					continue;
					IL_009f:
					list.Add(item);
					continue;
					IL_0064:
					bool num;
					if (i != 0)
					{
						if (i != 1)
						{
							goto IL_009f;
						}
						if (!flag2)
						{
							continue;
						}
						num = item != _king;
					}
					else
					{
						if (!flag2 || item == _king)
						{
							continue;
						}
						num = flag == soldiersFirst;
					}
					if (!num)
					{
						continue;
					}
					goto IL_009f;
				}
			}
			float num2 = ((to >= from) ? 1f : (-1f));
			float[] array = new float[7] { 0.12f, 0.27f, 0.42f, 0.42f, 0.57f, 0.72f, 0.88f };
			float[] array2 = new float[7] { 1.7f, -1.6f, 1.6f, 1.6f, -1.7f, 1.6f, -1.6f };
			int num3 = 0;
			for (int j = 0; j < array.Length; j++)
			{
				if (num3 >= list.Count)
				{
					break;
				}
				bool flag3 = j == 2 || j == 3;
				float num4 = Mathf.Lerp(from, to, array[j]) + ((j == 3) ? (0.9f * num2) : 0f);
				Vector2 vector = st.At(num4, array2[j]);
				if (!DryAt(vector) || TownSolidAt(vector))
				{
					continue;
				}
				Actor actor = list[num3++];
				try
				{
					if (actor.is_inside_building)
					{
						actor.exitBuilding();
					}
				}
				catch
				{
				}
				TownPut(actor, vector);
				try
				{
					actor.cancelAllBeh();
					if (flag3)
					{
						actor.makeWait(14f);
					}
					else if (j % 2 == 0)
					{
						actor.makeWait(UnityEngine.Random.Range(4f, 9f));
					}
					actor.lookTowardsPosition(flag3 ? st.At(num4 + ((j == 2) ? 0.9f : (-0.9f)) * num2, array2[j]) : st.At(num4));
				}
				catch
				{
				}
			}
			return num3;
		}

		private static void TownHold(Actor a, Vector2 at, Vector2 look)
		{
			TownPut(a, at);
			try
			{
				if (a.isAlive())
				{
					a.cancelAllBeh();
					a.makeWait(12f);
					a.lookTowardsPosition(look);
				}
			}
			catch
			{
			}
		}

		private void TownClearWay(Vector2 a, Vector2 b, Actor you, Actor keep)
		{
			Vector2 vector = b - a;
			float magnitude = vector.magnitude;
			if (magnitude < 0.01f)
			{
				return;
			}
			Vector2 vector2 = vector / magnitude;
			Vector2 vector3 = new Vector2(0f - vector2.y, vector2.x);
			foreach (Actor item in Founded)
			{
				if (item == null || item == you || item == keep)
				{
					continue;
				}
				try
				{
					if (!item.isAlive() || item.is_inside_building)
					{
						continue;
					}
					Vector2 current_position = item.current_position;
					float num = Mathf.Clamp(Vector2.Dot(current_position - a, vector2), 0f, magnitude);
					Vector2 vector4 = a + vector2 * num;
					if (!((current_position - vector4).magnitude > 1.2f))
					{
						Vector2 vector5 = vector4 + vector3 * ((Vector2.Dot(current_position - vector4, vector3) >= 0f) ? 2.6f : (-2.6f));
						if (!DryAt(vector5))
						{
							vector5 = vector4 - (vector5 - vector4);
						}
						if (DryAt(vector5))
						{
							TownPut(item, vector5);
						}
					}
				}
				catch
				{
				}
			}
		}

		private static Building TownHouseAhead(TownStreetInfo st, Vector2 here, Vector2 way, float side)
		{
			if (st == null)
			{
				return null;
			}
			Building result = null;
			float num = float.MaxValue;
			foreach (Building house in st.Houses)
			{
				if (TownStanding(house))
				{
					Vector2 lhs = TownMiddle(house) - here;
					float num2 = Vector2.Dot(lhs, way);
					float num3 = way.x * lhs.y - way.y * lhs.x;
					if (!(num2 < 2.5f) && !(num2 > 9f) && !(num3 * side <= 0f) && num2 < num)
					{
						num = num2;
						result = house;
					}
				}
			}
			return result;
		}

		private Actor TownSomeoneAhead(Actor you, Vector2 here, Vector2 way)
		{
			Actor result = null;
			float num = float.MaxValue;
			foreach (Actor item in Founded)
			{
				if (item == null || item == you)
				{
					continue;
				}
				try
				{
					if (!item.isAlive() || item.is_inside_building)
					{
						continue;
					}
					goto IL_0049;
				}
				catch
				{
				}
				continue;
				IL_0049:
				Vector2 lhs = item.current_position - here;
				float num2 = Vector2.Dot(lhs, way);
				float num3 = way.x * lhs.y - way.y * lhs.x;
				if (!(num2 < 2f) && !(num2 > 9f) && !(Mathf.Abs(num3) > 5f))
				{
					float num4 = num2 + ((num3 > 0f) ? 6f : 0f);
					if (num4 < num)
					{
						num = num4;
						result = item;
					}
				}
			}
			return result;
		}

		private IEnumerator TownMeasure(Actor you, TownStreetInfo st)
		{
			if (!(_townPace > 0f))
			{
				_townPace = 2f;
				Vector2 vector = st?.Along ?? new Vector2(Mathf.Cos(Mod.ViewYaw), Mathf.Sin(Mod.ViewYaw));
				if (st != null)
				{
					TownPut(you, st.At(0f - st.Half + 1f));
				}
				Mod.SetYaw(Mathf.Atan2(vector.y, vector.x));
				Mod.SetPitchDegrees(-3f);
				yield return CivilTest.Wait(0.3f);
				PossessionHooks.ScriptedInput = new Vector2(0f, 1f);
				yield return CivilTest.Wait(0.4f);
				Vector2 p0 = you.current_position;
				float t0 = Time.unscaledTime;
				yield return CivilTest.Wait(0.6f);
				float magnitude = (you.current_position - p0).magnitude;
				float num = Time.unscaledTime - t0;
				PossessionHooks.ScriptedInput = Vector2.zero;
				float num2 = ((num > 0.2f) ? (magnitude / num) : 0f);
				bool flag = num2 > 0.8f && num2 < 6f;
				if (flag)
				{
					_townPace = num2;
				}
				Note(string.Format("pace: you walk {0:F2} tiles a second ({1:F2} in {2:F2} s){3}", num2, magnitude, num, flag ? "" : ", taken as 2"));
				yield return CivilTest.Wait(0.3f);
			}
		}

		private static Vector2 TownKeys(Vector2 way, float yawDeg)
		{
			float f = yawDeg * (MathF.PI / 180f);
			float num = Mathf.Cos(f);
			float num2 = Mathf.Sin(f);
			return new Vector2(num2 * way.x - num * way.y, num * way.x + num2 * way.y);
		}

		private static float TownBearing(float pathYaw, Vector2 from, Vector2 to, float limit)
		{
			float target = Mathf.Atan2(to.y - from.y, to.x - from.x) * 57.29578f;
			return pathYaw + Mathf.Clamp(Mathf.DeltaAngle(pathYaw, target), 0f - limit, limit);
		}

		private float TownPitchTo(Vector2 at, float height, Vector2 from)
		{
			return Mathf.Clamp(Mathf.Atan2(TownGround(at) + height - Mod.View.Z, Mathf.Max(0.5f, (at - from).magnitude)) * 57.29578f, -15f, 12f);
		}

		private void TownClockOn()
		{
			if (!_townClock)
			{
				_townClock = true;
				_townClockWas = GameClock.OffForTest;
				GameClock.OffForTest = true;
				GameClock.Restore();
				_townOwnStep = Time.fixedDeltaTime;
				bool flag = false;
				try
				{
					flag = Config.fps_lock_30;
				}
				catch
				{
				}
				Time.fixedDeltaTime = (flag ? (1f / 60f) : (1f / 30f));
			}
		}

		private void TownClockOff()
		{
			if (_townClock)
			{
				_townClock = false;
				if (_townOwnStep > 0f)
				{
					Time.fixedDeltaTime = _townOwnStep;
				}
				GameClock.OffForTest = _townClockWas;
			}
		}

		private void TownEnd(bool mouse)
		{
			Time.captureFramerate = 0;
			PossessionHooks.ScriptedInput = Vector2.zero;
			TownClockOff();
			Mod.MouseLookOff = mouse;
		}

		private static string TownReal(float real, int frames)
		{
			return $"real {((frames > 0) ? (real / (float)frames * 1000f) : 0f):F0} ms a frame";
		}

		private static void TownPut(Actor a, Vector2 at)
		{
			try
			{
				a.current_position = at;
				a.dirty_current_tile = true;
			}
			catch
			{
			}
		}

		private float TownGround(Vector2 p)
		{
			try
			{
				return Mod.GroundForTest(p);
			}
			catch
			{
				return 0f;
			}
		}

		private static Vector2 TownMiddle(Building b)
		{
			try
			{
				WorldTile current_tile = b.current_tile;
				BuildingFundament fundament = b.asset.fundament;
				if (current_tile != null && fundament != null)
				{
					return new Vector2((float)current_tile.x + 0.5f + (float)(fundament.right - fundament.left) * 0.5f, (float)current_tile.y + 0.5f + (float)(fundament.top - fundament.bottom) * 0.5f);
				}
				return b.current_position;
			}
			catch
			{
				return Vector2.zero;
			}
		}

		private static bool TownStanding(Building b)
		{
			try
			{
				return b != null && b.isAlive() && !b.isRuin() && !b.isUnderConstruction();
			}
			catch
			{
				return false;
			}
		}

		private static bool TownFloor(WorldTile t)
		{
			if (t?.Type != null && t.Type.ground && !t.Type.liquid && !t.Type.block)
			{
				return !t.Type.lava;
			}
			return false;
		}

		private static bool TownOnLand(Vector2 p)
		{
			return TownFloor(World.world.GetTile((int)p.x, (int)p.y));
		}

		private static bool TownSolidAt(Vector2 p)
		{
			WorldTile tile = World.world.GetTile((int)p.x, (int)p.y);
			if (tile?.building != null)
			{
				return AssetStyle.IsSolid(tile.building);
			}
			return false;
		}

		private static bool TownFlora(Building b)
		{
			try
			{
				BuildingType building_type = b.asset.building_type;
				return building_type == BuildingType.Building_Tree || building_type == BuildingType.Building_Mineral || building_type == BuildingType.Building_Plant;
			}
			catch
			{
				return false;
			}
		}

		private IEnumerator WorldClip(string name, Func<IEnumerator> run)
		{
			IEnumerator clip = run();
			while (true)
			{
				object current;
				try
				{
					if (!clip.MoveNext())
					{
						break;
					}
					current = clip.Current;
					goto IL_007a;
				}
				catch (Exception arg)
				{
					Log.Error($"{Prefix} {name} failed: {arg}");
					Fail++;
				}
				break;
				IL_007a:
				yield return current;
			}
			WorldTidy();
		}

		private void WorldTidy()
		{
			Time.captureFramerate = 0;
			PossessionHooks.ScriptedInput = Vector2.zero;
			GodView.TestMouse = null;
			GodView.TestWheel = null;
			Mod.NoDive = true;
			Mod.MouseLookOff = false;
			Mod.HeldTime = -1f;
			Mod.DebugEyeSet = false;
			Mod.DebugEyeHeight = 0f;
			Mod.SetPitchDegrees(0f);
			FrameRenderer renderer = Mod.Renderer;
			if (renderer != null)
			{
				renderer.ForceComet = (renderer.ForceShower = (renderer.ForceAurora = -1f));
			}
			SceneCollector.AllLit = false;
			if (_worldSavedDive >= 0f)
			{
				Mod.Settings.DiveSpeed = _worldSavedDive;
				_worldSavedDive = -1f;
			}
			if (_worldSavedGpu.HasValue)
			{
				Mod.Settings.GpuRender = _worldSavedGpu.Value;
				_worldSavedGpu = null;
			}
			WorldKillClouds();
			foreach (Actor worldHelper in _worldHelpers)
			{
				if (worldHelper != null && Mod.Host == worldHelper)
				{
					try
					{
						ControllableUnit.clear(pCallKill: false);
					}
					catch
					{
					}
					break;
				}
			}
			foreach (Actor worldHelper2 in _worldHelpers)
			{
				if (worldHelper2 != null && worldHelper2.isAlive())
				{
					CivilTest.Kill(worldHelper2);
				}
			}
			_worldHelpers.Clear();
			if (_worldFires)
			{
				_worldFires = false;
				int num = WorldPutOutFires();
				if (num > 0)
				{
					Note($"{num} fires put out");
				}
			}
		}

		private IEnumerator WorldDive()
		{
			Settings settings = Mod.Settings;
			Actor target = CivilTest.SpawnNear("human", _centre + new Vector2(9f, -7f));
			try
			{
				if (target != null && _town != null && target.city != _town)
				{
					target.joinCity(_town);
				}
			}
			catch
			{
			}
			if (!Check("dive: a villager to dive into", target != null && FirstPerson.Possess.CanJumpTo(target)))
			{
				yield break;
			}
			Vector2 current_position = target.current_position;
			CivilTest.ClearBox((int)current_position.x - 6, (int)current_position.y - 6, (int)current_position.x + 6, (int)current_position.y + 6);
			WorldStill(target, 40f);
			_worldSavedDive = settings.DiveSpeed;
			settings.DiveSpeed = 1f;
			Mod.NoDive = true;
			Mod.HintUntil = 0f;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			try
			{
				ScrollWindow.hideAllEvent(pWithAnimation: false);
			}
			catch
			{
			}
			settings.GodView = true;
			GodView.TestMouse = new Vector2((float)Screen.width * 0.5f, (float)Screen.height * 0.5f);
			Vector2 mid = _centre;
			for (float t = 0f; t < 3f || (!Mod.GodShown && t < 6f); t += Time.unscaledDeltaTime)
			{
				ShowreelTest.Hold(mid, 38f);
				yield return null;
			}
			if (!Check("dive: the 3D god view is up", Mod.GodShown && Mod.GodAlpha >= 1f, $"shown {Mod.GodShown}, alpha {Mod.GodAlpha:F2}"))
			{
				yield break;
			}
			string dir = Clip("dive");
			int k = 0;
			float real0 = Time.realtimeSinceStartup;
			Rolling();
			for (int i = 1; i <= 110; i++)
			{
				float num = (float)i / 110f;
				float t2 = num * num * (3f - 2f * num);
				float num2 = Mathf.Exp(Mathf.Lerp(Mathf.Log(38f), Mathf.Log(16.6f), t2));
				float num3 = Mathf.Pow(Mathf.InverseLerp(16.6f, 38f, num2), 1.4f);
				Vector2 vector = (target.isAlive() ? target.current_position : _centre);
				ShowreelTest.Hold(vector + (mid - vector) * num3, num2);
				if (i % 30 == 0)
				{
					WorldStill(target, 40f);
				}
				yield return Shoot(dir, k++, hud: false);
			}
			for (int i = 1; i <= 40; i++)
			{
				ShowreelTest.ZoomOnly(Mathf.Lerp(16.6f, 10f, Mathf.SmoothStep(0f, 1f, (float)i / 40f)));
				GodView.TestWheel = 0.01f;
				yield return Shoot(dir, k++, hud: false);
			}
			Actor actor = Mod.GodLockForTest;
			Note(string.Format("dive: magnet held {0}, pull {1:F2}", (actor != null) ? WorldBoxMod.Describe(actor) : "nothing", Mod.GodPullForTest));
			if (actor != null && !FirstPerson.Possess.CanJumpTo(actor))
			{
				actor = null;
			}
			Actor into = actor ?? target;
			bool magnet = actor != null;
			Mod.NoDive = false;
			if (magnet)
			{
				for (int i = 0; i < 90; i++)
				{
					if (!(Mod.GodCloseTargetForTest > 0.9009f))
					{
						break;
					}
					if (Mod.GodLockForTest == null)
					{
						break;
					}
					GodView.TestWheel = 0.3f;
					yield return Shoot(dir, k++, hud: false);
				}
				float t = Time.realtimeSinceStartup;
				for (int i = 0; i < 60 && (i < 14 || Time.realtimeSinceStartup - t < 0.6f); i++)
				{
					yield return Shoot(dir, k++, hud: false);
				}
				if (Mod.GodLockForTest != null)
				{
					GodView.TestWheel = 1f;
				}
				else
				{
					magnet = false;
				}
			}
			if (!magnet)
			{
				Note("dive: no magnet hold, taking " + WorldBoxMod.Describe(into) + " directly (the dive from where the view stands)");
				try
				{
					ControllableUnit.setControllableCreature(into);
				}
				catch (Exception ex)
				{
					Note("dive: take " + ex.Message);
				}
			}
			int snapFrame = k;
			for (int i = 0; i < 180 && (!Mod.IsFirstPerson || Mod.Diving || i <= 8); i++)
			{
				if (i == 40 && !Mod.IsFirstPerson && !Mod.Diving)
				{
					Note("dive: the snap didn't take, taking the villager directly");
					try
					{
						ControllableUnit.setControllableCreature(into);
					}
					catch
					{
					}
				}
				yield return Shoot(dir, k++, hud: false);
			}
			bool flag = Mod.IsFirstPerson && !Mod.Diving;
			Check("dive: in the villager's eyes", flag, $"host {WorldBoxMod.Describe(Mod.Host)}, {k - snapFrame} frames from the snap");
			if (flag)
			{
				Mod.HintUntil = 0f;
				float t = Mod.ViewYaw;
				for (int i = 0; i < 60; i++)
				{
					float num4 = Mathf.SmoothStep(0f, 1f, (float)i / 59f);
					Mod.SetYaw(t + 0.7f * num4);
					Mod.SetPitchDegrees(Mathf.Lerp(-7f, -2f, num4));
					yield return Shoot(dir, k++, hud: false);
				}
			}
			Cut("dive", k, (magnet ? "by the magnet" : "taken directly") + ", " + WorldPace(real0, k) + ", " + WorldDistinct(dir));
		}

		private IEnumerator WorldFly()
		{
			Hour(2);
			Mod.NoDive = true;
			GodView.TestMouse = null;
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Vector2? vector = WorldCoast(_centre, 110f);
			Vector2 vector2 = (vector.HasValue ? (vector.Value - _centre) : (new Vector2((float)MapBox.width * 0.5f, (float)MapBox.height * 0.5f) - _centre));
			if (vector2.sqrMagnitude < 1f)
			{
				vector2 = new Vector2(1f, 0.3f);
			}
			float num = (vector.HasValue ? Mathf.Clamp(vector2.magnitude, 30f, 80f) : 60f);
			Vector2 normalized = vector2.normalized;
			Vector2 vector3 = new Vector2(0f - normalized.y, normalized.x);
			List<Vector2> knots = new List<Vector2>
			{
				WorldOnMap(_centre - normalized * 42f + vector3 * 16f),
				WorldOnMap(_centre - normalized * 30f + vector3 * 12f),
				WorldOnMap(_centre + vector3 * 1.5f),
				WorldOnMap(_centre + normalized * (num * 0.55f) - vector3 * 7f),
				WorldOnMap(_centre + normalized * (num + 14f) - vector3 * 4f),
				WorldOnMap(_centre + normalized * (num + 26f) - vector3 * 2f)
			};
			WorldFlightPath(knots, 300, out var at, out var yaw, out var pitch, out var length);
			Note(string.Format("fly: {0}, {1:F0} tiles of flight", vector.HasValue ? ("coast at " + vector.Value.ToString()) : "no coast within reach, out over the land", length));
			Mod.Settings.GodView = true;
			for (float t = 0f; t < 1.5f || ((!Mod.GodShown || !(Mod.GodAlpha >= 1f)) && t < 5f); t += Time.unscaledDeltaTime)
			{
				ShowreelTest.Hold(_centre, 28f);
				yield return null;
			}
			Mod.StartFlyForTest();
			yield return CivilTest.Wait(1f);
			if (!Check("fly: free flight", Mod.Flying))
			{
				yield break;
			}
			Mod.MouseLookOff = true;
			for (float t = 0f; t < 2.5f; t += Time.unscaledDeltaTime)
			{
				Mod.PlaceFlyForTest(at[0], yaw[0], pitch[0]);
				yield return null;
			}
			string dir = Clip("fly");
			float real0 = Time.realtimeSinceStartup;
			Rolling();
			for (int k = 0; k < 300; k++)
			{
				Mod.PlaceFlyForTest(at[k], yaw[k], pitch[k]);
				yield return Shoot(dir, k, hud: false);
			}
			Cut("fly", 300, $"still flying {Mod.Flying}, ended at {Mod.FlyPosition}, {WorldPace(real0, 300)}, {WorldDistinct(dir)}");
			Mod.MouseLookOff = false;
			Mod.LeaveFlyForTest();
			for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
			{
				if (!Mod.Flying)
				{
					break;
				}
				yield return null;
			}
		}

		private void WorldFlightPath(List<Vector2> knots, int frames, out Vector3[] at, out float[] yaw, out float[] pitch, out float length)
		{
			WorldCurveLine(knots, out var line, out var cum);
			length = cum[cum.Length - 1];
			Vector2[] array = new Vector2[frames];
			float[] array2 = new float[frames];
			float num = float.MaxValue;
			int num2 = frames / 2;
			for (int i = 0; i < frames; i++)
			{
				float num3 = (float)i / (float)(frames - 1);
				float num4 = length * (0.85f * num3 + 0.15f * num3 * num3 * (3f - 2f * num3));
				array[i] = WorldAlong(line, cum, num4);
				Vector2 vector = WorldAlong(line, cum, num4 + 3f) - array[i];
				Vector2 vector2 = ((vector.sqrMagnitude > 0.0001f) ? (new Vector2(0f - vector.y, vector.x).normalized * 2.5f) : Vector2.zero);
				array2[i] = Mathf.Max(Mathf.Max(WorldGround(array[i]), WorldGround(array[i] + vector2)), Mathf.Max(WorldGround(array[i] - vector2), WorldGround(array[i] + vector)));
				float sqrMagnitude = (array[i] - _centre).sqrMagnitude;
				if (sqrMagnitude < num)
				{
					num = sqrMagnitude;
					num2 = i;
				}
			}
			float b = length / (float)Mathf.Max(1, frames - 1);
			int num5 = Mathf.Clamp(Mathf.CeilToInt(9f / Mathf.Max(0.05f, b)), 3, 60);
			float[] array3 = new float[frames];
			for (int j = 0; j < frames; j++)
			{
				float num6 = array2[j];
				for (int k = Mathf.Max(0, j - num5); k <= Mathf.Min(frames - 1, j + num5); k++)
				{
					num6 = Mathf.Max(num6, array2[k]);
				}
				array3[j] = num6;
			}
			at = new Vector3[frames];
			yaw = new float[frames];
			pitch = new float[frames];
			float num7 = Mathf.Clamp((float)num2 / (float)(frames - 1), 0.1f, 0.9f);
			float num8 = 0f;
			float num9 = 0f;
			for (int l = 0; l < frames; l++)
			{
				float num10 = 0f;
				int num11 = 0;
				for (int m = Mathf.Max(0, l - num5); m <= Mathf.Min(frames - 1, l + num5); m++)
				{
					num10 += array3[m];
					num11++;
				}
				float num12 = (float)l / (float)(frames - 1);
				float num13 = ((num12 < num7) ? Mathf.Lerp(14f, 7.5f, Mathf.SmoothStep(0f, 1f, num12 / num7)) : Mathf.Lerp(7.5f, 9.5f, Mathf.SmoothStep(0f, 1f, (num12 - num7) / (1f - num7))));
				at[l] = new Vector3(array[l].x, array[l].y, num10 / (float)num11 + num13);
				float num14 = length * (0.85f * num12 + 0.15f * num12 * num12 * (3f - 2f * num12));
				Vector2 vector3 = WorldAlong(line, cum, num14 + 26f);
				Vector2 vector4 = vector3 - array[l];
				float num15 = Mathf.Atan2(vector4.y, vector4.x) * 57.29578f;
				float num16 = Mathf.Clamp((0f - Mathf.Atan2(at[l].z - WorldGround(vector3), Mathf.Max(1f, vector4.magnitude))) * 57.29578f, -38f, -6f);
				num8 = ((l == 0) ? num15 : Mathf.LerpAngle(num8, num15, 0.2f));
				num9 = ((l == 0) ? num16 : Mathf.Lerp(num9, num16, 0.2f));
				yaw[l] = num8 * (MathF.PI / 180f);
				pitch[l] = num9;
			}
		}

		private IEnumerator WorldDragon()
		{
			Hour(1);
			if (_king == null || !_king.isAlive())
			{
				Check("dragon: the king", ok: false);
				yield break;
			}
			yield return Possess(_king);
			if (!Check("dragon: the king in first person", InControl(_king)))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)_king).addStatusEffect("invincible", 900f, pColorEffect: false);
			}
			catch
			{
			}
			Mod.HintUntil = 0f;
			Vector2 vector = WorldDragonFrom();
			WorldTile tile = World.world.GetTile(Mathf.Clamp((int)vector.x, 2, MapBox.width - 3), Mathf.Clamp((int)vector.y, 2, MapBox.height - 3));
			Actor dragon = null;
			try
			{
				dragon = World.world.units.spawnNewUnit("dragon", tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Note("dragon: spawn " + ex.Message);
			}
			if (!Check("dragon: a dragon", dragon != null))
			{
				yield break;
			}
			_worldHelpers.Add(dragon);
			_worldFires = true;
			Dragon game = null;
			try
			{
				game = dragon.getActorComponent<Dragon>();
			}
			catch
			{
			}
			Note($"dragon: let loose at {dragon.current_position}, jointed model {Dragons.Ready}");
			Vector3 seen = Vector3.zero;
			bool parked = false;
			bool east = true;
			Vector2 eye = _centre;
			Vector2 g = dragon.current_position;
			float t0 = Time.realtimeSinceStartup;
			string act;
			float fire;
			while (Time.realtimeSinceStartup - t0 < 32f && dragon.isAlive())
			{
				seen = WorldFollowDragon(dragon, seen, 0.25f, out act, out fire);
				if (Time.realtimeSinceStartup - t0 > 5f && WorldParked(dragon, game))
				{
					g = dragon.current_position;
					if (WorldStrip(g, out east, out eye))
					{
						parked = true;
						break;
					}
				}
				yield return null;
			}
			if (!Check("dragon: it lived to fly", dragon.isAlive()))
			{
				yield break;
			}
			bool fieldClear = parked;
			if (!parked)
			{
				g = dragon.current_position;
				fieldClear = WorldStrip(g, out east, out eye);
				if (!fieldClear)
				{
					eye = WorldNearDry(g + (g - _centre).normalized * 10f + new Vector2(0f, -14f));
				}
				if (!fieldClear && Vector2.Distance(g, _centre) > 30f)
				{
					fieldClear = true;
					east = g.x > _centre.x;
				}
				Note("dragon: it never hung over a clear field: " + (fieldClear ? "set on from where it flies" : "filmed flying, not set on"));
			}
			float sgn = (east ? 1f : (-1f));
			Vector2 vector2 = new Vector2(g.x + sgn * 22f, g.y + 0.5f);
			CivilTest.ClearBox((int)Mathf.Min(eye.x, vector2.x) - 18, (int)Mathf.Min(eye.y, vector2.y - 1f) - 2, (int)Mathf.Max(eye.x, vector2.x) + 18, (int)Mathf.Max(eye.y, vector2.y + 2f) + 2);
			WorldPut(_king, eye);
			for (t0 = 0f; t0 < 1f; t0 += Time.unscaledDeltaTime)
			{
				seen = WorldFollowDragon(dragon, seen, 0.3f, out act, out fire);
				yield return null;
			}
			int num = 0;
			if (fieldClear)
			{
				for (int i = 0; i < 3; i++)
				{
					Vector2 vector3 = new Vector2(g.x + sgn * (12f + (float)i * 9f), g.y + 0.5f);
					if (!DryAt(vector3))
					{
						continue;
					}
					Actor actor = CivilTest.Spawn("sheep", vector3);
					if (actor != null)
					{
						_worldHelpers.Add(actor);
						num++;
						WorldStill(actor, 40f);
						try
						{
							game?.aggroTargets.Add(actor);
						}
						catch
						{
						}
					}
				}
				try
				{
					dragon.setFlip(!east);
				}
				catch
				{
				}
			}
			Note(string.Format("dragon: {0} at {1}, {2}, watched from {3}", parked ? "hanging" : "flying", g, fieldClear ? string.Format("to strafe {0} over {1} sheep", east ? "east" : "west", num) : "not set on", eye));
			string dir = Clip("dragon");
			float real0 = Time.realtimeSinceStartup;
			HashSet<string> acts = new HashSet<string>();
			int fireFrames = 0;
			int k = 0;
			int after = -1;
			bool swooped = false;
			bool slid = false;
			Rolling();
			for (; k < 330; k++)
			{
				if (k == 10 && fieldClear && dragon.isAlive())
				{
					Vector2 current_position = dragon.current_position;
					if (!WorldStripClear(current_position, east) && WorldStripClear(current_position, !east))
					{
						east = !east;
					}
					if (WorldStripClear(current_position, east) || Vector2.Distance(current_position, _centre) > 30f)
					{
						try
						{
							dragon.setFlip(!east);
							dragon.setTask("dragon_slide");
							slid = true;
						}
						catch (Exception ex2)
						{
							Note("dragon: slide " + ex2.Message);
						}
					}
					else
					{
						Note($"dragon: drifted to {current_position}, where its strip would cross the town: not set on");
					}
				}
				if (dragon.isAlive())
				{
					seen = WorldFollowDragon(dragon, seen, 0.22f, out var act2, out var fire2);
					if (act2 != "")
					{
						acts.Add(act2);
					}
					if (fire2 > 0.5f)
					{
						fireFrames++;
					}
					if (act2 == "Swoop" || act2 == "Burn" || fire2 > 0.5f)
					{
						swooped = true;
					}
					if (swooped && act2 == "Cruise" && after < 0)
					{
						after = k;
					}
				}
				if (k >= 240 && ((after >= 0 && k > after + 30) || !dragon.isAlive() || !slid))
				{
					break;
				}
				yield return Shoot(dir, k, hud: true);
			}
			Cut("dragon", k, string.Format("{0}, did {1}, fire in {2} frames, {3}, {4}", slid ? "set on its prey" : "flying free", string.Join("/", acts), fireFrames, WorldPace(real0, k), WorldDistinct(dir)));
			Check("dragon: it breathed fire on film", fireFrames > 0);
			WorldDragonDone();
			yield return CivilTest.Wait(0.5f);
		}

		private Vector2 WorldDragonFrom()
		{
			Vector2 result = _centre + new Vector2(34f, 12f);
			int num = -1;
			for (int i = 0; i < 16; i++)
			{
				float f = (float)i * MathF.PI / 8f;
				Vector2 vector = _centre + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * 34f;
				int num2 = 0;
				for (int j = -12; j <= 12; j += 4)
				{
					for (int k = -12; k <= 12; k += 4)
					{
						if (DryAt(vector + new Vector2(k, j)))
						{
							num2++;
						}
					}
				}
				if (num2 > num)
				{
					num = num2;
					result = vector;
				}
			}
			return result;
		}

		private static bool WorldParked(Actor dragon, Dragon game)
		{
			try
			{
				return ((BaseSimObject)dragon).isFlying() && game != null && game.state == DragonState.Fly && !dragon.is_moving;
			}
			catch
			{
				return false;
			}
		}

		private bool WorldStrip(Vector2 g, out bool east, out Vector2 eye)
		{
			east = true;
			eye = g;
			bool result = false;
			float num = float.MaxValue;
			bool[] array = new bool[2] { true, false };
			foreach (bool flag in array)
			{
				if (!WorldStripClear(g, flag, out var wet))
				{
					continue;
				}
				Vector2 vector = new Vector2(g.x + (flag ? 22f : (-22f)), g.y + 0.5f);
				float[] array2 = new float[6] { -12f, 12f, -14f, 14f, -16f, 16f };
				foreach (float num2 in array2)
				{
					Vector2 vector2 = vector + new Vector2(0f, num2);
					if (DryAt(vector2) && !WorldBuiltNear(vector2, 2))
					{
						float num3 = Vector2.Distance(vector2, _centre) + (float)wet * 2f + Mathf.Abs(num2);
						if (num3 < num)
						{
							num = num3;
							east = flag;
							eye = vector2;
							result = true;
						}
					}
				}
			}
			return result;
		}

		private bool WorldStripClear(Vector2 g, bool east)
		{
			int wet;
			return WorldStripClear(g, east, out wet);
		}

		private bool WorldStripClear(Vector2 g, bool east, out int wet)
		{
			int num = (east ? 1 : (-1));
			int num2 = 0;
			int num3 = 0;
			wet = 0;
			for (int i = 3; i <= 41; i++)
			{
				for (int j = -3; j <= 4; j++)
				{
					WorldTile tile = World.world.GetTile((int)g.x + num * i, (int)g.y + j);
					if (i >= 5 && i <= 39 && j >= -1 && j <= 2)
					{
						num2++;
						if (!WorldDryTile(tile))
						{
							wet++;
						}
					}
					if (tile != null && WorldBuilt(tile.building))
					{
						num3++;
					}
				}
			}
			Vector2 a = new Vector2(g.x + (float)num * 22f, g.y + 0.5f);
			if (num3 == 0 && wet <= num2 / 2)
			{
				return Vector2.Distance(a, _centre) >= 16f;
			}
			return false;
		}

		private Vector3 WorldFollowDragon(Actor dragon, Vector3 was, float ease, out string act, out float fire)
		{
			Vector2 p = dragon.current_position;
			float z = WorldGround(p) + 6f;
			act = "";
			fire = 0f;
			if (Dragons.Probe(dragon, out var pos, out var act2, out var _, out var fire2, out var _, out var _))
			{
				p = new Vector2(pos.X, pos.Y);
				z = pos.Z;
				act = act2;
				fire = fire2;
			}
			Vector3 vector = new Vector3(p.x, p.y, z);
			Vector3 result = ((was == Vector3.zero) ? vector : Vector3.Lerp(was, vector, ease));
			Vector3 eyeForTest = Mod.EyeForTest;
			float x = Mathf.Max(1f, new Vector2(result.x - eyeForTest.x, result.y - eyeForTest.y).magnitude);
			Mod.SetYaw(Mathf.Atan2(result.y - eyeForTest.y, result.x - eyeForTest.x));
			Mod.SetPitchDegrees(Mathf.Clamp(Mathf.Atan2(result.z + 1f - eyeForTest.z, x) * 57.29578f, -12f, 42f));
			return result;
		}

		private void WorldDragonDone()
		{
			WorldTidy();
			if (_king != null && _king.isAlive())
			{
				WorldPut(_king, WorldNearDry(_centre));
			}
		}

		private IEnumerator WorldSail()
		{
			if (!WorldSeaLane(_centre, out var start, out var heading, out var run, out var land))
			{
				Check("sail: open water along a coast", ok: false);
				yield break;
			}
			Note(string.Format("sail: from {0} towards {1:F0} degrees, {2:F0} tiles open, land {3}", start, heading * 57.29578f, run, (land > 0f) ? "to port" : ((land < 0f) ? "to starboard" : "out of sight")));
			Hour(2);
			if (_king == null || !_king.isAlive())
			{
				Check("sail: the king", ok: false);
				yield break;
			}
			yield return Possess(_king);
			if (!Check("sail: the king in first person", InControl(_king)))
			{
				yield break;
			}
			Actor ship = null;
			try
			{
				ship = World.world.units.createNewUnit("boat_transport_human", World.world.GetTile((int)start.x, (int)start.y));
				if (ship != null && _king.kingdom != null)
				{
					ship.setKingdom(_king.kingdom);
				}
			}
			catch (Exception ex)
			{
				Note("sail: ship " + ex.Message);
			}
			if (!Check("sail: a ship on open water", ship != null))
			{
				yield break;
			}
			_worldHelpers.Add(ship);
			try
			{
				ship.data.set("existence_check", (int)World.world.getCurWorldTime() + 1000000);
			}
			catch
			{
			}
			for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
			{
				WorldPin(ship, start);
				yield return null;
			}
			yield return Possess(ship, 2.5f);
			if (!Check("sail: at her helm", InControl(ship) && BoatHelm.Steers(ship), "host " + WorldBoxMod.Describe(Mod.Host)))
			{
				yield return WorldSailDone(ship);
				yield break;
			}
			BoatHelm.Heading = heading;
			Mod.SetYaw(heading + land * 0.55f);
			Mod.SetPitchDegrees(-6f);
			yield return CivilTest.Wait(2.6f);
			Mod.ToastUntil = 0f;
			Mod.HintUntil = 0f;
			BoatHelm.Sails = 0.6f;
			BoatHelm.Way = Mathf.Max(BoatHelm.Way, 0.4f);
			float turnWheel = ((land > 0f) ? 1f : (-1f));
			float[] keys = new float[6] { 0f, 70f, 150f, 200f, 265f, 329f };
			float[] look = new float[6]
			{
				land * 0.55f,
				0f,
				0f,
				(0f - turnWheel) * 0.3f,
				0f,
				land * 0.35f
			};
			float[] tilt = new float[6] { -6f, -4.5f, -4.5f, -5f, -4.5f, -5f };
			Vector2 from = ship.current_position;
			float lastH = BoatHelm.Heading;
			float turned = 0f;
			float mostWay = 0f;
			bool turning = false;
			bool turnDone = false;
			bool avoiding = false;
			int turnAt = -1;
			int bumps = 0;
			int avoided = 0;
			string dir = Clip("sail");
			float real0 = Time.realtimeSinceStartup;
			Rolling();
			int k;
			for (k = 0; k < 330; k++)
			{
				if (!ship.isAlive())
				{
					break;
				}
				if (!BoatHelm.Steers(ship))
				{
					break;
				}
				Vector2 current_position = ship.current_position;
				float heading2 = BoatHelm.Heading;
				float num = Mathf.Abs(Mathf.DeltaAngle(lastH * 57.29578f, heading2 * 57.29578f)) * (MathF.PI / 180f);
				lastH = heading2;
				if (turning)
				{
					turned += num;
				}
				if (k == 150)
				{
					if (WorldRun(current_position, heading2 - turnWheel * 0.9f, 40f) < 15f && WorldRun(current_position, heading2 + turnWheel * 0.9f, 40f) >= 15f)
					{
						turnWheel = 0f - turnWheel;
					}
					turning = true;
					turnAt = k;
					turned = 0f;
				}
				if (turning && (turned >= 1f || k - turnAt > 110))
				{
					turning = false;
					turnDone = true;
				}
				float num2 = WorldRun(current_position, heading2, 16f);
				if (num2 < 8f)
				{
					if (!avoiding)
					{
						avoided++;
					}
					avoiding = true;
				}
				else if (num2 >= 14f)
				{
					avoiding = false;
				}
				float x = 0f;
				if (avoiding && (!turning || !(num2 >= 5f)))
				{
					x = ((WorldRun(current_position, heading2 + 0.7f, 30f) >= WorldRun(current_position, heading2 - 0.7f, 30f)) ? (-1f) : 1f);
				}
				else if (turning)
				{
					x = turnWheel;
				}
				PossessionHooks.ScriptedInput = new Vector2(x, 0f);
				mostWay = Mathf.Max(mostWay, BoatHelm.Way);
				if (BoatHelm.Bump > 0.3f)
				{
					bumps++;
				}
				Mod.SetYaw(heading2 + WorldCurve(k, keys, look));
				Mod.SetPitchDegrees(WorldCurve(k, keys, tilt));
				yield return Shoot(dir, k, hud: true);
			}
			float num3 = (ship.isAlive() ? (ship.current_position - from).magnitude : 0f);
			Cut("sail", k, string.Format("{0:F0} tiles made good, most way {1:F2}, turned {2:F0} degrees{3}, kept off the land {4} times, bumps {5}, {6}, {7}", num3, mostWay, turned * 57.29578f, turnDone ? "" : " (turn unfinished)", avoided, bumps, WorldPace(real0, k), WorldDistinct(dir)));
			Check("sail: she sailed", num3 > 4f, $"{num3:F1} tiles");
			yield return WorldSailDone(ship);
		}

		private IEnumerator WorldSailDone(Actor ship)
		{
			PossessionHooks.ScriptedInput = Vector2.zero;
			if (_king != null && _king.isAlive())
			{
				yield return Possess(_king, 1.5f);
			}
			CivilTest.Kill(ship);
			yield return CivilTest.Wait(0.5f);
		}

		private bool WorldSeaLane(Vector2 near, out Vector2 start, out float heading, out float run, out float land)
		{
			start = near;
			heading = 0f;
			run = 0f;
			land = 0f;
			float num = float.MinValue;
			for (int i = 0; i < 2; i++)
			{
				if (num != float.MinValue)
				{
					break;
				}
				int num2 = ((i == 0) ? 100 : Mathf.Max(MapBox.width, MapBox.height));
				int num3 = ((i == 0) ? 4 : 7);
				for (int j = (int)near.y - num2; j <= (int)near.y + num2; j += num3)
				{
					for (int k = (int)near.x - num2; k <= (int)near.x + num2; k += num3)
					{
						if (k < 4 || j < 4 || k > MapBox.width - 5 || j > MapBox.height - 5 || !WorldOpenWater(k, j, 2))
						{
							continue;
						}
						Vector2 vector = new Vector2((float)k + 0.5f, (float)j + 0.5f);
						for (int l = 0; l < 16; l++)
						{
							float num4 = (float)l * MathF.PI / 8f;
							float num5 = WorldRun(vector, num4, 70f);
							if (!(num5 < 30f))
							{
								Vector2 p = vector + new Vector2(Mathf.Cos(num4), Mathf.Sin(num4)) * (num5 * 0.5f);
								float num6 = WorldLandOff(p, num4 + MathF.PI / 2f);
								float num7 = WorldLandOff(p, num4 - MathF.PI / 2f);
								float num8 = ((num6 <= num7) ? num6 : num7);
								float num9 = ((num8 > 20f) ? 0f : ((num6 <= num7) ? 1f : (-1f)));
								float num10 = Mathf.Min(num5, 70f) + ((num9 != 0f) ? (15f - Mathf.Abs(num8 - 9f)) : 0f) - 0.12f * (vector - near).magnitude;
								if (num10 > num)
								{
									num = num10;
									start = vector;
									heading = num4;
									run = num5;
									land = num9;
								}
							}
						}
					}
				}
			}
			return num > float.MinValue;
		}

		private static float WorldLandOff(Vector2 p, float a)
		{
			Vector2 vector = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
			for (float num = 1f; num <= 20f; num += 1f)
			{
				if (!WorldWet(p + vector * num))
				{
					if (!(num < 4f))
					{
						return num;
					}
					return 99f;
				}
			}
			return 99f;
		}

		private static float WorldRun(Vector2 p, float a, float max)
		{
			Vector2 vector = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
			Vector2 vector2 = new Vector2(0f - vector.y, vector.x) * 1.6f;
			float result = 0f;
			for (float num = 1f; num <= max; num += 1f)
			{
				Vector2 vector3 = p + vector * num;
				if (!WorldWet(vector3) || !WorldWet(vector3 + vector2) || !WorldWet(vector3 - vector2))
				{
					break;
				}
				result = num;
			}
			return result;
		}

		private static void WorldPin(Actor ship, Vector2 at)
		{
			try
			{
				if (ship != null && ship.isAlive() && !BoatHelm.Steers(ship))
				{
					ship.cancelAllBeh();
					ship.current_position = at;
					ship.dirty_current_tile = true;
				}
			}
			catch
			{
			}
		}

		private IEnumerator WorldStorm()
		{
			Hour(1);
			if (_king == null || !_king.isAlive())
			{
				Check("storm: the king", ok: false);
				yield break;
			}
			yield return Possess(_king);
			if (!Check("storm: the king in first person", InControl(_king)))
			{
				yield break;
			}
			try
			{
				((BaseSimObject)_king).addStatusEffect("invincible", 900f, pColorEffect: false);
			}
			catch
			{
			}
			_worldFires = true;
			List<Vector2> strikes = new List<Vector2>();
			float heading;
			Vector2 stand = WorldStreet(out heading, strikes);
			WorldPut(_king, stand);
			Mod.SetYaw(heading);
			Mod.SetPitchDegrees(4f);
			Note($"storm: in the street at {stand}, looking {heading * 57.29578f:F0} degrees, {strikes.Count} places for the lightning");
			Vector2 vector = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
			Vector2 vector2 = new Vector2(0f - vector.y, vector.x);
			bool flag = false;
			try
			{
				flag = AssetManager.clouds != null && AssetManager.clouds.get("cloud_lightning") != null;
			}
			catch
			{
			}
			WorldCloud("cloud_rain", stand);
			WorldCloud("cloud_rain", stand + vector * 10f + vector2 * 8f);
			WorldCloud("cloud_rain", stand + vector * 10f - vector2 * 8f);
			WorldCloud("cloud_rain", stand - vector * 8f);
			WorldCloud("cloud_rain", stand + vector * 26f);
			WorldCloud("cloud_rain", stand + vector * 30f + vector2 * 14f);
			WorldCloud(flag ? "cloud_lightning" : "cloud_rain", stand + vector * 40f - vector2 * 6f);
			for (float t = 0f; t < 4.5f; t += Time.unscaledDeltaTime)
			{
				WorldPinClouds();
				yield return null;
			}
			Mod.HintUntil = 0f;
			Mod.ToastUntil = 0f;
			int[] bolts = new int[6] { 20, 64, 108, 150, 188, 222 };
			int struck = 0;
			int most = 0;
			float flash = 0f;
			string dir = Clip("storm");
			float real0 = Time.realtimeSinceStartup;
			float clock = Time.unscaledTime;
			Rolling();
			for (int k = 0; k < 240; k++)
			{
				WorldPinClouds();
				Mod.HeldTime = clock + (float)k / 30f;
				float num = (float)k / 239f;
				float num2 = heading + Mathf.Lerp(-0.28f, 0.3f, Mathf.SmoothStep(0f, 1f, num));
				Mod.SetYaw(num2);
				Mod.SetPitchDegrees(4f + 10f * Mathf.Sin(MathF.PI * num));
				if (Array.IndexOf(bolts, k) >= 0 && strikes.Count > 0)
				{
					int index = 0;
					float num3 = float.MaxValue;
					for (int i = 0; i < strikes.Count; i++)
					{
						Vector2 vector3 = strikes[i] - stand;
						float num4 = Mathf.Abs(Mathf.DeltaAngle(num2 * 57.29578f, Mathf.Atan2(vector3.y, vector3.x) * 57.29578f));
						if (num4 < num3)
						{
							num3 = num4;
							index = i;
						}
					}
					Vector2 vector4 = strikes[index];
					strikes.RemoveAt(index);
					try
					{
						WorldTile tile = World.world.GetTile((int)vector4.x, (int)vector4.y);
						if (struck % 3 != 2)
						{
							MapBox.spawnLightningBig(tile);
						}
						else
						{
							MapBox.spawnLightningMedium(tile);
						}
						struck++;
					}
					catch (Exception ex)
					{
						Note("storm: lightning " + ex.Message);
					}
				}
				try
				{
					Weather weather = Mod.SceneForTest.Weather;
					most = Mathf.Max(most, weather.Bolts.Count);
					flash = Mathf.Max(flash, weather.Flash);
				}
				catch
				{
				}
				yield return Shoot(dir, k, hud: true);
			}
			int num5 = 0;
			try
			{
				num5 = Mod.SceneForTest.Weather.Storms.Count;
			}
			catch
			{
			}
			Cut("storm", 240, $"{struck} bolts called, at most {most} drawn at once, brightest flash {flash:F2}, {num5} storm clouds drawn, {WorldPace(real0, 240)}, {WorldDistinct(dir)}");
			Check("storm: lightning seen", most > 0 || flash > 0.05f);
			WorldTidy();
		}

		private Vector2 WorldStreet(out float heading, List<Vector2> strikes)
		{
			Vector2 vector = WorldNearDry(_centre);
			heading = 0f;
			float num = float.MinValue;
			for (int i = 0; i < 24; i++)
			{
				float num2 = (float)i * MathF.PI / 12f;
				int num3 = 0;
				int num4 = 0;
				for (float num5 = -0.45f; num5 <= 0.46f; num5 += 0.15f)
				{
					Vector2 vector2 = new Vector2(Mathf.Cos(num2 + num5), Mathf.Sin(num2 + num5));
					for (float num6 = 3f; num6 <= 14f; num6 += 1.5f)
					{
						WorldTile tile = World.world.GetTile((int)(vector.x + vector2.x * num6), (int)(vector.y + vector2.y * num6));
						if (tile != null && WorldBuilt(tile.building))
						{
							num3++;
							break;
						}
					}
					for (float num7 = 16f; num7 <= 28f; num7 += 4f)
					{
						if (WorldStrikeable(vector + vector2 * num7))
						{
							num4++;
						}
					}
				}
				float num8 = (float)Mathf.Min(num3, 4) * 2f + (float)Mathf.Min(num4, 14);
				if (num8 > num)
				{
					num = num8;
					heading = num2;
				}
			}
			for (int j = 0; j < 8; j++)
			{
				float f = heading + Mathf.Lerp(-0.4f, 0.4f, (float)(j * 5 % 8) / 7f);
				float num9 = 16f + (float)(j % 3) * 5f;
				Vector2 vector3 = vector + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num9;
				for (int k = 0; k < 9; k++)
				{
					Vector2 p = vector3 + new Vector2(k % 3 - 1, k / 3 - 1) * 2f;
					if (WorldStrikeable(p))
					{
						strikes.Add(new Vector2((float)(int)p.x + 0.5f, (float)(int)p.y + 0.5f));
						break;
					}
				}
			}
			return vector;
		}

		private static bool WorldStrikeable(Vector2 p)
		{
			if (DryAt(p))
			{
				return !WorldBuiltNear(p, 4);
			}
			return false;
		}

		private void WorldCloud(string kind, Vector2 at)
		{
			_worldClouds.Add(null);
			_worldCloudAt.Add(at);
			_worldCloudKind.Add(kind);
		}

		private void WorldPinClouds()
		{
			for (int i = 0; i < _worldClouds.Count; i++)
			{
				Cloud cloud = _worldClouds[i];
				try
				{
					if (!(cloud == null) && ((BaseEffect)cloud).active)
					{
						goto IL_00c7;
					}
					WorldTile tile = World.world.GetTile(Mathf.Clamp((int)_worldCloudAt[i].x, 1, MapBox.width - 2), Mathf.Clamp((int)_worldCloudAt[i].y, 1, MapBox.height - 2));
					cloud = (_worldClouds[i] = EffectsLibrary.spawn("fx_cloud", tile, _worldCloudKind[i]) as Cloud);
					if (cloud == null)
					{
						continue;
					}
					cloud.setLifespan(600f);
					goto IL_00c7;
					IL_00c7:
					Vector3 localPosition = cloud.transform.localPosition;
					float num = ((cloud.spriteShadow != null) ? cloud.spriteShadow.offset.y : 0f);
					cloud.transform.localPosition = new Vector3(_worldCloudAt[i].x, _worldCloudAt[i].y - num, localPosition.z);
				}
				catch (Exception ex)
				{
					if (_worldCloudErrors++ < 3)
					{
						Note("storm: cloud " + ex.Message);
					}
				}
			}
		}

		private void WorldKillClouds()
		{
			foreach (Cloud worldCloud in _worldClouds)
			{
				try
				{
					if (worldCloud != null && ((BaseEffect)worldCloud).active)
					{
						worldCloud.kill();
					}
				}
				catch
				{
				}
			}
			_worldClouds.Clear();
			_worldCloudAt.Clear();
			_worldCloudKind.Clear();
		}

		private IEnumerator WorldNight()
		{
			if (_king == null || !_king.isAlive())
			{
				Check("night: the king", ok: false);
				yield break;
			}
			yield return Possess(_king);
			if (!Check("night: the king in first person", InControl(_king)))
			{
				yield break;
			}
			Settings s = Mod.Settings;
			FrameRenderer r = Mod.Renderer;
			float lift;
			Vector2 eye = WorldHill(out lift);
			CivilTest.ClearBox((int)eye.x - 6, (int)eye.y - 6, (int)eye.x + 6, (int)eye.y + 6);
			float num = WorldGround(eye) + 3f;
			Vector2 vector = _centre - eye;
			float yawV = Mathf.Atan2(vector.y, vector.x);
			float pitchV = Mathf.Clamp(Mathf.Atan2(WorldGround(_centre) + 1.5f - num, Mathf.Max(1f, vector.magnitude)) * 57.29578f, -24f, 0f);
			Hour(3);
			SceneCollector.AllLit = true;
			Mod.ForceRelight();
			Mod.DebugEye = eye;
			Mod.DebugEyeSet = true;
			Mod.DebugEyeHeight = 3f;
			Mod.SetYaw(yawV);
			Mod.SetPitchDegrees(pitchV);
			r.ForceAurora = 1f;
			r.ForceShower = 1f;
			r.ForceComet = 1f;
			yield return CivilTest.Wait(2.5f);
			_worldSavedGpu = s.GpuRender;
			s.GpuRender = false;
			for (int i = 0; i < 4; i++)
			{
				yield return null;
			}
			Vec3 cd = r.CometDirection;
			s.GpuRender = _worldSavedGpu.Value;
			_worldSavedGpu = null;
			yield return CivilTest.Wait(2f);
			float num2 = Mathf.Atan2(cd.Y, cd.X);
			float num3 = Mathf.Asin(Mathf.Clamp(cd.Z, -1f, 1f)) * 57.29578f;
			float num4 = yawV + Mathf.DeltaAngle(yawV * 57.29578f, 0f) * (MathF.PI / 180f);
			bool flag = cd.X * cd.X + cd.Y * cd.Y + cd.Z * cd.Z > 0.25f && num3 > 6f && Mathf.Abs(Mathf.DeltaAngle(0f, num2 * 57.29578f)) < 100f;
			if (Mathf.Abs(num4 - yawV) > 1.6f)
			{
				num4 = yawV + Mathf.Sign(num4 - yawV) * 1.6f;
			}
			float num5 = (flag ? (num4 + Mathf.DeltaAngle(num4 * 57.29578f, num2 * 57.29578f) * (MathF.PI / 180f)) : (num4 + 0.45f));
			float num6 = (flag ? Mathf.Clamp(num3 * 0.8f, 8f, 26f) : 15f);
			Note(string.Format("night: on a hill at {0} ({1:F1} over the village), the village at {2:F0} degrees, comet {3}", eye, lift, yawV * 57.29578f, flag ? $"at {num2 * 57.29578f:F0} degrees, {num3:F0} up" : "out of the way"));
			float[] keys = new float[4] { 0f, 40f, 150f, 239f };
			float[] yaws = new float[4]
			{
				yawV,
				yawV + 0.04f,
				num4,
				num5
			};
			float[] pitches = new float[4]
			{
				pitchV,
				pitchV + 1.5f,
				12f,
				num6
			};
			int meteors = 0;
			string dir = Clip("night");
			float real0 = Time.realtimeSinceStartup;
			float clock = Time.unscaledTime;
			bool holstered = Mod.Holstered;
			Mod.Holstered = true;
			Rolling();
			for (int i = 0; i < 240; i++)
			{
				Mod.HeldTime = clock + (float)i / 30f;
				Mod.SetYaw(WorldCurve(i, keys, yaws));
				Mod.SetPitchDegrees(WorldCurve(i, keys, pitches));
				if (!s.GpuRender && r.MeteorsInSky > 0)
				{
					meteors++;
				}
				yield return Shoot(dir, i, hud: false);
			}
			Mod.Holstered = holstered;
			Cut("night", 240, string.Format("aurora {0:F2}, comet {1:F2}{2}, {3}, {4}", r.AuroraTonight, r.CometTonight, (!s.GpuRender) ? $", shooting stars in {meteors} frames" : "", WorldPace(real0, 240), WorldDistinct(dir)));
		}

		private Vector2 WorldHill(out float lift)
		{
			Vector2 vector = WorldNearDry(_centre - new Vector2(16f, 0f));
			float num = WorldGround(_centre);
			float num2 = float.MinValue;
			lift = WorldGround(vector) - num;
			for (float num3 = 12f; num3 <= 24f; num3 += 4f)
			{
				for (int i = 0; i < 32; i++)
				{
					float f = (float)i * MathF.PI / 16f;
					Vector2 vector2 = _centre + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num3;
					if (DryAt(vector2) && !WorldBuiltNear(vector2, 2))
					{
						float num4 = WorldGround(vector2) - num;
						Vector2 vector3 = _centre - vector2;
						float num5 = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(vector3.y, vector3.x) * 57.29578f, 0f)) * (MathF.PI / 180f);
						float num6 = Mathf.Min(num4, 8f) * 0.8f - num5 * 3f - (WorldSees(vector2, 3f, _centre) ? 0f : 10f);
						if (num6 > num2)
						{
							num2 = num6;
							vector = vector2;
							lift = num4;
						}
					}
				}
			}
			return vector;
		}

		private bool WorldSees(Vector2 eye, float height, Vector2 at)
		{
			float a = WorldGround(eye) + height;
			float b = WorldGround(at) + 1.5f;
			for (int i = 1; i < 20; i++)
			{
				float t = (float)i / 20f;
				if (WorldGround(Vector2.Lerp(eye, at, t)) > Mathf.Lerp(a, b, t))
				{
					return false;
				}
			}
			return true;
		}

		private float WorldGround(Vector2 p)
		{
			TerrainCache terrainForTest = Mod.TerrainForTest;
			if (terrainForTest == null || terrainForTest.Width <= 0)
			{
				return 0f;
			}
			return Mathf.Max(terrainForTest.SampleHeight(p.x, p.y), 0f);
		}

		private static Vector2 WorldOnMap(Vector2 p)
		{
			return new Vector2(Mathf.Clamp(p.x, 4f, (float)MapBox.width - 5f), Mathf.Clamp(p.y, 4f, (float)MapBox.height - 5f));
		}

		private static bool WorldWet(Vector2 p)
		{
			WorldTile tile = World.world.GetTile((int)p.x, (int)p.y);
			if (tile != null && tile.Type != null && tile.Type.liquid)
			{
				return !tile.Type.lava;
			}
			return false;
		}

		private static bool WorldOpenWater(int x, int y, int r)
		{
			for (int i = -r; i <= r; i++)
			{
				for (int j = -r; j <= r; j++)
				{
					if (!WorldWet(new Vector2((float)(x + j) + 0.5f, (float)(y + i) + 0.5f)))
					{
						return false;
					}
				}
			}
			return true;
		}

		private static bool WorldDryTile(WorldTile t)
		{
			if (t != null && t.Type != null && !t.Type.liquid && !t.Type.block)
			{
				return !t.Type.lava;
			}
			return false;
		}

		private static bool WorldBuilt(Building b)
		{
			if (b == null || b.asset == null)
			{
				return false;
			}
			BuildingType building_type = b.asset.building_type;
			if (building_type != BuildingType.Building_Tree && building_type != BuildingType.Building_Mineral && building_type != BuildingType.Building_Plant)
			{
				return building_type != BuildingType.Building_Wheat;
			}
			return false;
		}

		private static bool WorldBuiltNear(Vector2 p, int r)
		{
			for (int i = -r; i <= r; i++)
			{
				for (int j = -r; j <= r; j++)
				{
					WorldTile tile = World.world.GetTile((int)p.x + j, (int)p.y + i);
					if (tile != null && WorldBuilt(tile.building))
					{
						return true;
					}
				}
			}
			return false;
		}

		private static Vector2 WorldNearDry(Vector2 p)
		{
			for (float num = 0f; num < 16f; num += 1f)
			{
				for (int i = 0; i < 16; i++)
				{
					float f = (float)i * MathF.PI / 8f;
					Vector2 vector = new Vector2(p.x + Mathf.Cos(f) * num, p.y + Mathf.Sin(f) * num);
					WorldTile tile = World.world.GetTile((int)vector.x, (int)vector.y);
					if (WorldDryTile(tile) && (tile.building == null || !WorldBuilt(tile.building)))
					{
						return new Vector2((float)tile.x + 0.5f, (float)tile.y + 0.5f);
					}
					if (num == 0f)
					{
						break;
					}
				}
			}
			return p;
		}

		private static Vector2? WorldCoast(Vector2 from, float max)
		{
			for (float num = 8f; num <= max; num += 2f)
			{
				int num2 = Mathf.Max(24, (int)(num * 1.5f));
				for (int i = 0; i < num2; i++)
				{
					float f = (float)i * 2f * MathF.PI / (float)num2;
					Vector2 value = from + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num;
					if (!(value.x < 3f) && !(value.y < 3f) && !(value.x > (float)(MapBox.width - 4)) && !(value.y > (float)(MapBox.height - 4)) && WorldOpenWater((int)value.x, (int)value.y, 2))
					{
						return value;
					}
				}
			}
			return null;
		}

		private static void WorldPut(Actor a, Vector2 at)
		{
			try
			{
				WorldTile tile = World.world.GetTile((int)at.x, (int)at.y);
				if (tile != null)
				{
					a.spawnOn(tile, 0f);
				}
				a.current_position = at;
				a.dirty_current_tile = true;
			}
			catch
			{
			}
		}

		private static void WorldStill(Actor a, float seconds)
		{
			try
			{
				if (a != null && a.isAlive())
				{
					a.cancelAllBeh();
					a.makeWait(seconds);
				}
			}
			catch
			{
			}
		}

		private static int WorldPutOutFires()
		{
			int num = 0;
			for (int i = 0; i < MapBox.height; i++)
			{
				for (int j = 0; j < MapBox.width; j++)
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
			try
			{
				foreach (Building item in new List<Building>(World.world.buildings.getSimpleList()))
				{
					try
					{
						if (item != null && item.isAlive() && ((BaseSimObject)item).hasAnyStatusEffect() && ((BaseSimObject)item).hasStatus("burning"))
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
						if (item2 != null && item2.isAlive() && ((BaseSimObject)item2).hasAnyStatusEffect() && ((BaseSimObject)item2).hasStatus("burning"))
						{
							item2.finishStatusEffect("burning");
							num++;
						}
					}
					catch
					{
					}
				}
			}
			catch
			{
			}
			return num;
		}

		private static void WorldCurveLine(List<Vector2> knots, out List<Vector2> line, out float[] cum)
		{
			line = new List<Vector2>();
			for (int i = 1; i + 2 < knots.Count; i++)
			{
				for (int j = 0; j < 48; j++)
				{
					float num = (float)j / 48f;
					float num2 = num * num;
					float num3 = num2 * num;
					Vector2 vector = knots[i - 1];
					Vector2 vector2 = knots[i];
					Vector2 vector3 = knots[i + 1];
					Vector2 vector4 = knots[i + 2];
					line.Add(0.5f * (2f * vector2 + (vector3 - vector) * num + (2f * vector - 5f * vector2 + 4f * vector3 - vector4) * num2 + (3f * vector2 - vector - 3f * vector3 + vector4) * num3));
				}
			}
			line.Add(knots[knots.Count - 2]);
			cum = new float[line.Count];
			for (int k = 1; k < line.Count; k++)
			{
				cum[k] = cum[k - 1] + (line[k] - line[k - 1]).magnitude;
			}
		}

		private static Vector2 WorldAlong(List<Vector2> line, float[] cum, float s)
		{
			int num = line.Count - 1;
			if (num < 1)
			{
				if (line.Count <= 0)
				{
					return Vector2.zero;
				}
				return line[0];
			}
			if (s <= 0f)
			{
				return line[0];
			}
			if (s >= cum[num])
			{
				Vector2 vector = line[num] - line[num - 1];
				return line[num] + ((vector.sqrMagnitude > 1E-06f) ? vector.normalized : Vector2.zero) * (s - cum[num]);
			}
			int num2 = 0;
			int num3 = num;
			while (num3 - num2 > 1)
			{
				int num4 = (num2 + num3) / 2;
				if (cum[num4] <= s)
				{
					num2 = num4;
				}
				else
				{
					num3 = num4;
				}
			}
			float num5 = cum[num3] - cum[num2];
			return Vector2.Lerp(line[num2], line[num3], (num5 > 1E-06f) ? ((s - cum[num2]) / num5) : 0f);
		}

		private static float WorldCurve(float k, float[] at, float[] v)
		{
			int num = at.Length;
			if (k <= at[0])
			{
				return v[0];
			}
			if (k >= at[num - 1])
			{
				return v[num - 1];
			}
			int i;
			for (i = 0; i < num - 2 && k > at[i + 1]; i++)
			{
			}
			float num2 = at[i + 1] - at[i];
			float num3 = (k - at[i]) / num2;
			float num4 = num3 * num3;
			float num5 = num4 * num3;
			float num6 = ((i > 0) ? ((v[i + 1] - v[i - 1]) / (at[i + 1] - at[i - 1]) * num2) : 0f);
			float num7 = ((i + 2 < num) ? ((v[i + 2] - v[i]) / (at[i + 2] - at[i]) * num2) : 0f);
			return (2f * num5 - 3f * num4 + 1f) * v[i] + (num5 - 2f * num4 + num3) * num6 + (-2f * num5 + 3f * num4) * v[i + 1] + (num5 - num4) * num7;
		}

		private static string WorldPace(float real0, int frames)
		{
			return $"{(Time.realtimeSinceStartup - real0) / (float)Mathf.Max(1, frames):F2} s a frame";
		}

		private static string WorldDistinct(string dir)
		{
			try
			{
				HashSet<long> hashSet = new HashSet<long>();
				int num = 0;
				string[] files = Directory.GetFiles(dir, "f_*.jpg");
				foreach (string fileName in files)
				{
					hashSet.Add(new FileInfo(fileName).Length);
					num++;
				}
				return $"{hashSet.Count} different of {num} saved";
			}
			catch
			{
				return "frames unread";
			}
		}
	}
}
