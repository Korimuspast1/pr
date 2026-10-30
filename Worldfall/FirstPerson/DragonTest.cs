using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class DragonTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private Actor _king;

		private Actor _dragon;

		private Vector2 _eye;

		private float _eyeMoved = -9f;

		private readonly StringBuilder _rec = new StringBuilder();

		private readonly HashSet<string> _acts = new HashSet<string>();

		private readonly Dictionary<string, float> _fireIn = new Dictionary<string, float>();

		private string _lastAct = "";

		private int _lastState = -1;

		private float _stateSince;

		private float _longestLanding;

		private float _maxHeld;

		private float _minAlt = 99f;

		private bool _bad;

		private int _shots;

		private readonly List<float> _shotAt = new List<float>();

		private float _t;

		private float _wt;

		private float _stateSinceWorld;

		private float _lastStatus;

		private float _burstUntil;

		private float _nextBurst;

		private double _world0 = -1.0;

		private string _burstAct = "";

		private bool _recStarted;

		private bool Alive
		{
			get
			{
				if (_dragon != null)
				{
					return _dragon.isAlive();
				}
				return false;
			}
		}

		private Dragon Game
		{
			get
			{
				if (_dragon == null)
				{
					return null;
				}
				return _dragon.getActorComponent<Dragon>();
			}
		}

		private int State
		{
			get
			{
				try
				{
					Dragon game = Game;
					return (int)((game != null) ? game.state : ((DragonState)(-1)));
				}
				catch
				{
					return -1;
				}
			}
		}

		private void FlushRec()
		{
			try
			{
				string path = Path.Combine(Log.Folder, "dragon_rec.txt");
				if (!_recStarted)
				{
					File.WriteAllText(path, "");
					_recStarted = true;
				}
				File.AppendAllText(path, _rec.ToString());
			}
			catch (Exception ex)
			{
				Log.Error("DRAGON rec: " + ex.Message);
			}
			_rec.Length = 0;
		}

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			string options = File.ReadAllText(path);
			bool quit = options.Contains("quit");
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
			Log.Info($"DRAGON world {MapBox.width}x{MapBox.height}, jointed model ready {Dragons.Ready}");
			foreach (WorldTile item in KingTest.GoodSites())
			{
				_king = KingTest.FoundKingdom(item, "human", 3, 0);
				if (_king != null)
				{
					break;
				}
			}
			if (_king == null)
			{
				Log.Error("DRAGON could not found a kingdom");
				Finish(quit);
				yield break;
			}
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("DRAGON first person did not start");
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
			if (!Open(new Vector2((float)MapBox.width * 0.5f, (float)MapBox.height * 0.5f), 40, out var site))
			{
				Log.Error("DRAGON no open land");
				Finish(quit);
				yield break;
			}
			WorldTile tile = World.world.GetTile((int)site.x + 30, (int)site.y);
			string pActorAssetID = (options.Contains("zombie") ? "zombie_dragon" : "dragon");
			try
			{
				_dragon = World.world.units.spawnNewUnit(pActorAssetID, tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("DRAGON spawn: " + ex.Message);
			}
			if (_dragon == null)
			{
				Finish(quit);
				yield break;
			}
			Log.Info($"DRAGON spawned at {tile.pos} over land at {site}");
			_eye = site + new Vector2(0f, -30f);
			_mod.DebugEye = _eye;
			_mod.DebugEyeSet = true;
			_mod.DebugEyeHeight = 1.6f;
			yield return Run("free flight", Free(14f));
			yield return Run("swoop", Swoop());
			yield return Run("land and burn", LandBurn());
			yield return Run("land, sleep, wake, take off", Sleep());
			yield return Run("swoop again", Swoop());
			yield return Run("killed in the air", Fall());
			Report();
			Finish(quit);
		}

		private IEnumerator Run(string name, IEnumerator part)
		{
			Log.Info("DRAGON --- " + name);
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
				}
				catch (Exception ex)
				{
					Log.Error("DRAGON " + name + " failed: " + ex);
					break;
				}
				yield return current;
			}
		}

		private void Finish(bool quit)
		{
			FlushRec();
			if (_mod != null)
			{
				_mod.DebugEyeSet = false;
				_mod.DebugEyeHeight = 0f;
				_mod.SetPitchDegrees(0f);
			}
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info("DRAGON DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private void Watch()
		{
			float deltaTime = Time.deltaTime;
			_t += deltaTime;
			double curWorldTime = World.world.getCurWorldTime();
			if (_world0 < 0.0)
			{
				_world0 = curWorldTime;
			}
			_wt = (float)(curWorldTime - _world0);
			if (_dragon == null)
			{
				return;
			}
			int state = State;
			bool alive = Alive;
			Vector2 vector = (alive ? _dragon.current_position : Vector2.zero);
			int num = 0;
			bool flag = false;
			bool flag2 = false;
			try
			{
				if (alive)
				{
					SpriteAnimation sprite_animation = _dragon.sprite_animation;
					num = ((sprite_animation != null) ? sprite_animation.currentFrameIndex : 0);
					flag = _dragon.flip;
					flag2 = ((BaseSimObject)_dragon).isFlying();
				}
			}
			catch
			{
			}
			Vec3 pos;
			string act;
			float alt;
			float fire;
			int hold;
			float held;
			bool flag3 = Dragons.Probe(_dragon, out pos, out act, out alt, out fire, out hold, out held);
			_rec.Append(string.Format(CultureInfo.InvariantCulture, "{0:F3} {1:F2} {2:F2} {3} {4} {5} {6} {7} {8:F2} {9:F2} {10:F2} {11} {12:F2} {13} {14:F2}\n", _t, vector.x, vector.y, state, num, flag ? 1 : 0, flag2 ? 1 : 0, alive ? 1 : 0, pos.X, pos.Y, pos.Z, act, fire, hold, held));
			if (state != _lastState)
			{
				if (_lastState == 6)
				{
					_longestLanding = Mathf.Max(_longestLanding, _wt - _stateSinceWorld);
				}
				_lastState = state;
				_stateSince = _t;
				_stateSinceWorld = _wt;
				Log.Info($"DRAGON t={_t:F1} game state {state} at {vector}");
			}
			if (_rec.Length > 20000)
			{
				FlushRec();
			}
			if (flag3 && _t - _lastStatus > 0.5f)
			{
				_lastStatus = _t;
				Log.Info($"DRAGON tick t={_t:F2} {act} pos {pos.X:F1},{pos.Y:F1},{pos.Z:F1} alt {alt:F1} fire {fire:F2} game {state}/{num} hold {hold} held {held:F1} faults {DragonFlight.Faults}");
			}
			if (!flag3)
			{
				return;
			}
			if (float.IsNaN(pos.X) || float.IsNaN(pos.Z))
			{
				_bad = true;
			}
			_minAlt = Mathf.Min(_minAlt, alt);
			_maxHeld = Mathf.Max(_maxHeld, held);
			if (fire > 0.5f)
			{
				_fireIn[act] = _t;
			}
			if (act != _lastAct)
			{
				Log.Info($"DRAGON t={_t:F1} drawn {act} (from {_lastAct}) at {pos.X:F0},{pos.Y:F0} alt {alt:F1}, game state {state} frame {num} hold {hold} held {held:F1}");
				_acts.Add(act);
				_lastAct = act;
				_burstUntil = _t + ((act == "Swoop") ? 5f : ((act == "Cruise") ? 1.5f : 4f));
				_burstAct = act;
			}
			if (_t < _burstUntil && _t >= _nextBurst && _shots < 700)
			{
				_nextBurst = _t + 0.1f;
				_mod.SaveFrame($"dragonb_{_shots:000}_{_burstAct}.png");
				_shots++;
			}
			int num2;
			if (alt < 4f)
			{
				switch (act)
				{
				default:
					num2 = ((act == "Dead") ? 1 : 0);
					break;
				case "Stand":
				case "Burn":
				case "Lie":
				case "Rise":
					num2 = 1;
					break;
				}
			}
			else
			{
				num2 = 0;
			}
			bool flag4 = (byte)num2 != 0;
			_mod.DebugEyeHeight = Mathf.MoveTowards(_mod.DebugEyeHeight, flag4 ? 14f : 1.6f, Time.deltaTime * 8f);
			float num3 = Vector2.Distance(_eye, new Vector2(pos.X, pos.Y));
			float num4 = ((act == "Swoop") ? 95f : 60f);
			if ((num3 > num4 || num3 < 14f || (!Sees(_eye, pos) && act != "Swoop")) && _t - _eyeMoved > 2.5f)
			{
				Vector2 vector2 = new Vector2(pos.X, pos.Y);
				bool flag5 = false;
				float[] array = new float[3] { 34f, 26f, 44f };
				foreach (float num5 in array)
				{
					for (int j = 0; j < 16; j++)
					{
						if (flag5)
						{
							break;
						}
						float f = (float)j * MathF.PI / 8f + 0.3f;
						Vector2 vector3 = vector2 + new Vector2(Mathf.Cos(f), Mathf.Sin(f)) * num5;
						if (Dry(vector3) && Sees(vector3, pos))
						{
							_eye = vector3;
							flag5 = true;
						}
					}
					if (flag5)
					{
						break;
					}
				}
				_mod.DebugEye = _eye;
				_eyeMoved = _t;
				Clear(_eye, 8);
			}
			float num6 = 0f;
			TerrainCache terrainForTest = _mod.TerrainForTest;
			if (terrainForTest != null)
			{
				num6 = terrainForTest.SampleHeight(_eye.x, _eye.y);
			}
			float num7 = pos.X - _eye.x;
			float num8 = pos.Y - _eye.y;
			float x = Mathf.Max(1f, Mathf.Sqrt(num7 * num7 + num8 * num8));
			_mod.SetYaw(Mathf.Atan2(num8, num7));
			_mod.SetPitchDegrees(Mathf.Clamp(Mathf.Atan2(pos.Z + 4f - (num6 + _mod.DebugEyeHeight), x) * 57.29578f, -30f, 48f));
			for (int num9 = _shotAt.Count - 1; num9 >= 0; num9--)
			{
				if (_t >= _shotAt[num9])
				{
					_shotAt.RemoveAt(num9);
					if (_shots < 160)
					{
						_mod.SaveFrame($"dragon_{_shots:000}_{_lastAct}.png");
						_shots++;
					}
				}
			}
		}

		private bool Sees(Vector2 e, Vec3 dragon)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			if (terrainForTest == null)
			{
				return true;
			}
			float num = terrainForTest.SampleHeight(e.x, e.y) + 1.6f;
			float num2 = dragon.Z + 3f;
			for (int i = 1; i < 24; i++)
			{
				float num3 = (float)i / 24f;
				float x = e.x + (dragon.X - e.x) * num3;
				float y = e.y + (dragon.Y - e.y) * num3;
				if (terrainForTest.SampleHeight(x, y) > num + (num2 - num) * num3)
				{
					return false;
				}
			}
			return true;
		}

		private static void Clear(Vector2 at, int r)
		{
			for (int i = -r; i <= r; i++)
			{
				for (int j = -r; j <= r; j++)
				{
					if (j * j + i * i > r * r)
					{
						continue;
					}
					WorldTile tile = World.world.GetTile((int)at.x + j, (int)at.y + i);
					try
					{
						if (tile != null && tile.building != null)
						{
							tile.building.startDestroyBuilding();
						}
					}
					catch
					{
					}
				}
			}
		}

		private bool Dry(Vector2 p)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			if (terrainForTest == null || p.x < 3f || p.y < 3f || p.x > (float)(terrainForTest.Width - 4) || p.y > (float)(terrainForTest.Height - 4))
			{
				return false;
			}
			int num = (int)p.y * terrainForTest.Width + (int)p.x;
			return (terrainForTest.Flags[num] & 3) == 0;
		}

		private bool Open(Vector2 near, int room, out Vector2 site)
		{
			site = near;
			float num = float.MaxValue;
			bool result = false;
			TerrainCache terrainForTest = _mod.TerrainForTest;
			if (terrainForTest == null)
			{
				return false;
			}
			for (int i = room; i < terrainForTest.Height - room; i += 6)
			{
				for (int j = room; j < terrainForTest.Width - room; j += 6)
				{
					int num2 = 0;
					for (int k = -room; k <= room; k += 5)
					{
						for (int l = -room; l <= room; l += 5)
						{
							if ((terrainForTest.Flags[(i + k) * terrainForTest.Width + j + l] & 3) != 0)
							{
								num2++;
							}
						}
					}
					float num3 = (float)num2 * 10f + (new Vector2(j, i) - near).magnitude * 0.05f;
					if (num3 < num)
					{
						num = num3;
						site = new Vector2((float)j + 0.5f, (float)i + 0.5f);
						result = true;
					}
				}
			}
			return result;
		}

		private IEnumerator Wait(float seconds, Func<bool> until = null)
		{
			float end = _t + seconds;
			while (_t < end)
			{
				Watch();
				if (until != null && until())
				{
					break;
				}
				yield return null;
			}
		}

		private bool Parked()
		{
			if (!Alive)
			{
				return false;
			}
			try
			{
				return ((BaseSimObject)_dragon).isFlying() && State == 0 && !_dragon.is_moving;
			}
			catch
			{
				return false;
			}
		}

		private Actor Sheep(Vector2 at)
		{
			WorldTile tile = World.world.GetTile(Mathf.Clamp((int)at.x, 1, MapBox.width - 2), Mathf.Clamp((int)at.y, 1, MapBox.height - 2));
			if (tile == null)
			{
				return null;
			}
			try
			{
				return World.world.units.spawnNewUnit("sheep", tile, pSpawnSound: false, pMiracleSpawn: false, 0f, null, pGiveOwnerlessItems: true, pAdultAge: true);
			}
			catch (Exception ex)
			{
				Log.Error("DRAGON sheep: " + ex.Message);
				return null;
			}
		}

		private IEnumerator Free(float seconds)
		{
			yield return Wait(seconds);
		}

		private IEnumerator Swoop()
		{
			yield return Wait(12f, Parked);
			if (!Alive)
			{
				yield break;
			}
			Vector2 current_position = _dragon.current_position;
			bool flag = Dry(current_position + new Vector2(20f, 0f));
			float num = (flag ? 1f : (-1f));
			Dragon game = Game;
			for (int i = 0; i < 3; i++)
			{
				Actor actor = Sheep(current_position + new Vector2(num * (12f + (float)i * 9f), 0f));
				if (actor != null && game != null)
				{
					game.aggroTargets.Add(actor);
				}
			}
			_dragon.setFlip(!flag);
			_dragon.setTask("dragon_slide");
			Log.Info(string.Format("DRAGON set to swoop {0} from {1}", flag ? "east" : "west", current_position));
			yield return Wait(1f);
			yield return Wait(14f, () => _lastAct == "Cruise");
			yield return Wait(3f);
		}

		private IEnumerator LandBurn()
		{
			yield return Wait(15f, () => Parked() && Dry(_dragon.current_position) && Dry(_dragon.current_position + new Vector2(0f, -9f)));
			if (!Alive)
			{
				yield break;
			}
			Vector2 current_position = _dragon.current_position;
			Dragon d = Game;
			for (int num = 0; num < 2; num++)
			{
				Actor actor = Sheep(current_position + new Vector2((float)num * 3f - 1.5f, -8f));
				if (actor != null && d != null)
				{
					d.aggroTargets.Add(actor);
				}
			}
			Clear(current_position, 13);
			_dragon.setTask("dragon_land");
			Log.Info($"DRAGON set to land by sheep at {current_position}");
			yield return Wait(30f, () => _lastAct == "Stand" || _lastAct == "Burn" || State == 9 || State == 1);
			yield return Wait(6f, () => !Alive || !((BaseSimObject)_dragon).isFlying() || State == 1);
			if (Alive && State != 1 && !((BaseSimObject)_dragon).isFlying())
			{
				Vector2 current_position2 = _dragon.current_position;
				for (int num2 = 0; num2 < 2; num2++)
				{
					Actor actor2 = Sheep(current_position2 + new Vector2((float)num2 * 3f - 1.5f, -7f));
					if (actor2 != null && d != null)
					{
						d.aggroTargets.Add(actor2);
					}
				}
				_dragon.setTask("dragon_land_attack");
				Log.Info("DRAGON set to burn");
			}
			yield return Wait(30f, () => _lastAct == "TakeOff" || (_lastAct == "Cruise" && _t - _stateSince > 3f && State == 0));
			yield return Wait(5f);
		}

		private IEnumerator Sleep()
		{
			yield return Wait(15f, () => Parked() && Dry(_dragon.current_position));
			if (Alive)
			{
				try
				{
					Game?.aggroTargets.Clear();
				}
				catch
				{
				}
				Clear(_dragon.current_position, 13);
				_dragon.setTask("dragon_land");
				Log.Info($"DRAGON set to land and sleep at {_dragon.current_position}");
				yield return Wait(20f, () => !((BaseSimObject)_dragon).isFlying() && _lastAct == "Stand");
				yield return Wait(2f);
				_dragon.setTask("dragon_sleep");
				yield return Wait(12f);
				_dragon.setTask("dragon_wakeup");
				yield return Wait(4f);
				_dragon.setTask("dragon_up");
				yield return Wait(10f);
			}
		}

		private IEnumerator Fall()
		{
			yield return Wait(12f, () => _lastAct == "Cruise");
			if (Alive && !((BaseSimObject)_dragon).isFlying())
			{
				try
				{
					Game?.aggroTargets.Clear();
				}
				catch
				{
				}
				_dragon.setTask("dragon_up");
			}
			Vec3 pos;
			string act;
			float alt;
			float fire;
			int hold;
			float held;
			yield return Wait(25f, () => _lastAct == "Cruise" && Dragons.Probe(_dragon, out pos, out act, out alt, out fire, out hold, out held) && alt > 8f);
			yield return Wait(1f);
			if (Alive)
			{
				try
				{
					((BaseSimObject)_dragon).getHitFullHealth(AttackType.Other);
				}
				catch (Exception ex)
				{
					Log.Error("DRAGON kill: " + ex.Message);
				}
				Log.Info("DRAGON killed in the air");
				yield return Wait(25f, () => _lastAct == "Dead");
				yield return Wait(4f);
			}
		}

		private void Report()
		{
			Check($"drawn as the jointed 3D dragon (ready {Dragons.Ready})", Dragons.Ready && _acts.Count > 0);
			string[] array = new string[10] { "Cruise", "Swoop", "Land", "Stand", "Burn", "TakeOff", "Lie", "Rise", "Fall", "Dead" };
			foreach (string text in array)
			{
				Check("did " + text, _acts.Contains(text));
			}
			Check("breathed fire swooping", _fireIn.ContainsKey("Swoop"));
			Check("breathed fire on the ground", _fireIn.ContainsKey("Burn"));
			Check($"game held at most 8 s (longest {_maxHeld:F1})", _maxHeld <= 8.2f);
			Check($"game's landing didn't stick (longest {_longestLanding:F1} s of world time)", _longestLanding < 10f);
			Check($"never below the ground (lowest {_minAlt:F2})", _minAlt > -1f);
			Check($"no NaN (faults caught {DragonFlight.Faults})", !_bad && DragonFlight.Faults == 0);
			int num = 0;
			int num2 = 0;
			try
			{
				num = _mod.Audio.DragonWingBeats;
				num2 = _mod.Audio.DragonRoars;
			}
			catch
			{
			}
			Check($"its sounds played with it (wingbeats {num}, roars {num2})", num > 5 && num2 > 0);
			Log.Info(string.Format("DRAGON acts seen: {0}; frames {1}", string.Join(", ", _acts), _shots));
			void Check(string what, bool ok)
			{
				Log.Info("DRAGON " + (ok ? "PASS" : "FAIL") + " " + what);
			}
		}
	}
}
