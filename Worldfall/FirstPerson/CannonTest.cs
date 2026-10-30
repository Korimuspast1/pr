using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class CannonTest : MonoBehaviour
	{
		private WorldBoxMod _mod;

		private Actor _king;

		private Actor _ship;

		private Actor _other;

		private Kingdom _wild;

		private Vector2 _shipAt;

		private Vector2 _land;

		private Vector2 _sea;

		private Vector2 _otherAt;

		private Vector2 _eye;

		private Vector2 _u;

		private Vector2 _perp;

		private bool _fired;

		private bool _again;

		private int _passed;

		private int _failed;

		private IEnumerator Start()
		{
			string path = Path.Combine(Path.Combine(Application.streamingAssetsPath, "mods"), "FirstPerson.selftest");
			bool quit = File.ReadAllText(path).Contains("quit");
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
			SceneCollector.CannonTrace = true;
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
				Log.Error("CANNON could not found a kingdom");
				Application.Quit();
				yield break;
			}
			ControllableUnit.setControllableCreature(_king);
			yield return new WaitForSecondsRealtime(2.5f);
			if (!_mod.IsFirstPerson)
			{
				Log.Error("CANNON first person did not start");
				Application.Quit();
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
			try
			{
				_wild = World.world.kingdoms_wild.get("possessed");
			}
			catch
			{
			}
			Settings s = _mod.Settings;
			int savedMode = s.TimeMode;
			s.TimeMode = 1;
			_mod.ForceRelight();
			if (!Coast())
			{
				Log.Error("CANNON no coast with open water");
				Finish(quit);
			}
			else
			{
				yield return Run(Shots());
				s.TimeMode = savedMode;
				Finish(quit);
			}
		}

		private void Finish(bool quit)
		{
			_mod.DebugEyeSet = false;
			_mod.DebugEyeHeight = 0f;
			_mod.SetPitchDegrees(0f);
			try
			{
				ControllableUnit.clear(pCallKill: false);
			}
			catch
			{
			}
			Log.Info($"CANNON {_passed} passed, {_failed} failed");
			Log.Info("CANNON DONE");
			if (quit)
			{
				Application.Quit();
			}
		}

		private IEnumerator Run(IEnumerator part)
		{
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
					Log.Error("CANNON failed: " + ex);
					break;
				}
				yield return current;
			}
		}

		private void Check(string what, bool ok)
		{
			if (ok)
			{
				_passed++;
			}
			else
			{
				_failed++;
			}
			Log.Info((ok ? "CANNON PASS " : "CANNON FAIL ") + what);
		}

		private static bool Wet(TerrainCache c, float x, float y)
		{
			int num = (int)x;
			int num2 = (int)y;
			if (num < 0 || num2 < 0 || num >= c.Width || num2 >= c.Height)
			{
				return false;
			}
			return (c.Flags[num2 * c.Width + num] & 1) != 0;
		}

		private static bool Open(TerrainCache c, Vector2 p, int r)
		{
			for (int i = -r; i <= r; i++)
			{
				for (int j = -r; j <= r; j++)
				{
					if (!Wet(c, p.x + (float)j, p.y + (float)i))
					{
						return false;
					}
				}
			}
			return true;
		}

		private bool Coast()
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			if (terrainForTest == null || terrainForTest.Width == 0)
			{
				return false;
			}
			float num = float.MaxValue;
			Vector2 current_position = _king.current_position;
			for (int i = 20; i < terrainForTest.Height - 20; i += 3)
			{
				for (int j = 20; j < terrainForTest.Width - 20; j += 3)
				{
					Vector2 vector = new Vector2((float)j + 0.5f, (float)i + 0.5f);
					if (!Open(terrainForTest, vector, 4))
					{
						continue;
					}
					for (int k = 0; k < 16; k++)
					{
						float f = (float)k * MathF.PI / 8f;
						Vector2 vector2 = new Vector2(Mathf.Cos(f), Mathf.Sin(f));
						float num2 = 0f;
						for (float num3 = 5f; num3 < 14f; num3 += 0.5f)
						{
							if (!Wet(terrainForTest, vector.x + vector2.x * num3, vector.y + vector2.y * num3))
							{
								num2 = num3;
								break;
							}
						}
						if (num2 < 9f)
						{
							continue;
						}
						Vector2 land = vector + vector2 * (num2 + 2f);
						if (Wet(terrainForTest, land.x, land.y))
						{
							continue;
						}
						Vector2 vector3 = vector - vector2 * 10f;
						if (!Open(terrainForTest, vector3, 1))
						{
							continue;
						}
						Vector2 vector4 = new Vector2(0f - vector2.y, vector2.x);
						Vector2 vector5 = vector + vector4 * 9f;
						if (!Open(terrainForTest, vector5, 2))
						{
							vector4 = -vector4;
							vector5 = vector + vector4 * 9f;
						}
						if (!Open(terrainForTest, vector5, 2))
						{
							continue;
						}
						Vector2 vector6 = vector - vector4 * 9f - vector2 * 3f;
						if (Open(terrainForTest, vector6, 1))
						{
							float num4 = Mathf.Abs(num2 - 11f) + (vector - current_position).magnitude * 0.002f;
							if (num4 < num)
							{
								num = num4;
								_shipAt = vector;
								_land = land;
								_sea = vector3;
								_otherAt = vector5;
								_eye = vector6;
								_u = vector2;
								_perp = vector4;
							}
						}
					}
				}
			}
			if (num == float.MaxValue)
			{
				return false;
			}
			Log.Info($"CANNON ship at {_shipAt}, shore target {_land}, sea target {_sea}, second ship at {_otherAt}, watched from {_eye}");
			return true;
		}

		private Actor Spawn(Vector2 at)
		{
			Actor result = null;
			try
			{
				result = World.world.units.createNewUnit("boat_transport_human", World.world.GetTile((int)at.x, (int)at.y));
			}
			catch (Exception ex)
			{
				Log.Error("CANNON spawn: " + ex.Message);
			}
			return result;
		}

		private void Hold(Actor a)
		{
			try
			{
				if (a != null && a.isAlive())
				{
					a.stopMovement();
					a.cancelAllBeh();
				}
			}
			catch
			{
			}
		}

		private void Watch(Vector2 eye, Vector2 look, float height, float pitch)
		{
			_mod.DebugEye = eye;
			_mod.DebugEyeSet = true;
			TerrainCache terrainForTest = _mod.TerrainForTest;
			float num = terrainForTest?.SampleHeight(eye.x, eye.y) ?? 0f;
			_mod.DebugEyeHeight = (Wet(terrainForTest, eye.x, eye.y) ? (0f + height - num) : height);
			_mod.SetYaw(Mathf.Atan2(look.y - eye.y, look.x - eye.x));
			_mod.SetPitchDegrees(pitch);
		}

		private int Cannonballs(Actor by)
		{
			int num = 0;
			foreach (Projectile item in World.world.projectiles.list)
			{
				if (item != null && item.asset != null && item.asset.id == "cannonball" && item.by_who == by)
				{
					num++;
				}
			}
			return num;
		}

		private void Attack(Vector2 at, BaseSimObject target)
		{
			try
			{
				_ship.tryToAttack(target, pDoChecks: false, (Action)null, new Vector3(at.x, at.y, 0f), _wild, World.world.GetTile((int)at.x, (int)at.y), 0f);
			}
			catch (Exception ex)
			{
				Log.Error("CANNON attack: " + ex.Message);
			}
		}

		private IEnumerator Fire(Vector2 at, BaseSimObject target)
		{
			_fired = false;
			if (_ship == null || !_ship.isAlive())
			{
				yield break;
			}
			int before = Cannonballs(_ship);
			int guns = SceneCollector.CannonMuzzles;
			Attack(at, target);
			for (int k = 0; k < 4; k++)
			{
				if (_fired)
				{
					break;
				}
				yield return null;
				_fired = Cannonballs(_ship) > before || SceneCollector.CannonMuzzles > guns;
			}
			if (_fired)
			{
				yield break;
			}
			try
			{
				Vector2 current_position = _ship.current_position;
				Vector2 normalized = (at - current_position).normalized;
				Vector3 pLaunchPosition = new Vector3(current_position.x + normalized.x, current_position.y + normalized.y, 0f);
				World.world.projectiles.spawn(_ship, target, "cannonball", pLaunchPosition, new Vector3(at.x, at.y, 0f), 0f, 0.6f, null, _wild);
				Log.Info("CANNON (fired by hand: the attack sent no ball)");
			}
			catch (Exception ex)
			{
				Log.Error("CANNON spawn ball: " + ex.Message);
				yield break;
			}
			for (int k = 0; k < 4; k++)
			{
				if (_fired)
				{
					break;
				}
				yield return null;
				_fired = Cannonballs(_ship) > before || SceneCollector.CannonMuzzles > guns;
			}
		}

		private IEnumerator Shot(string name, Vector2 at, BaseSimObject target, string expect, float[] frames, bool mayRetry = false)
		{
			_again = false;
			int muzzles = SceneCollector.CannonMuzzles;
			int balls = SceneCollector.CannonBallFrames;
			int strikes = SceneCollector.CannonStrikes;
			int hidden = SceneCollector.CannonSpritesHidden;
			Hold(_ship);
			float quiet = 0f;
			for (int k = 0; k < 20; k++)
			{
				yield return null;
				quiet += Time.unscaledDeltaTime;
			}
			quiet /= 20f;
			float start = Time.unscaledTime;
			float heel = 0f;
			float busy = 0f;
			yield return Fire(at, target);
			if (!_fired)
			{
				Check(name + ": the ship fired", ok: false);
				yield break;
			}
			int next = 0;
			int frameCount = 0;
			int mostParticles = 0;
			while (Time.unscaledTime - start < 3.6f)
			{
				float num = Time.unscaledTime - start;
				heel = Mathf.Max(heel, Mathf.Abs(SceneCollector.ShipHeel(_ship)));
				Hold(_ship);
				Hold(_other);
				mostParticles = Mathf.Max(mostParticles, SceneCollector.BlastParticleCount);
				if (next < frames.Length && num >= frames[next])
				{
					_mod.SaveFrame($"cannon_{name}_{next++}.png");
				}
				yield return null;
				busy += Time.unscaledDeltaTime;
				frameCount++;
			}
			busy /= (float)Mathf.Max(1, frameCount);
			int num2 = SceneCollector.CannonMuzzles - muzzles;
			Log.Info($"CANNON {name}: guns fired {num2}, port {SceneCollector.LastMuzzleGap:F2} tiles from the game's launch (side {SceneCollector.LastMuzzleSide}), ball drawn {SceneCollector.CannonBallFrames - balls} frames, " + $"strikes {SceneCollector.CannonStrikes - strikes} (last {SceneCollector.LastCannonStrike}), game sprites left out {SceneCollector.CannonSpritesHidden - hidden}, " + $"heel {heel * 57.29578f:F1} deg, particles at most {mostParticles}, frame {quiet * 1000f:F1} ms quiet, {busy * 1000f:F1} ms firing");
			if (mayRetry && expect != "Water" && SceneCollector.CannonStrikes - strikes == 1 && SceneCollector.LastCannonStrike.StartsWith("Water"))
			{
				_again = true;
				Log.Info("CANNON (" + name + ": the game's scatter put the ball in the water beside her: again)");
				yield break;
			}
			Check(name + ": a gun port fired, off the hull's middle", num2 == 1 && SceneCollector.LastMuzzleGap > 0.3f);
			Check(name + ": the ball was drawn in flight", SceneCollector.CannonBallFrames - balls >= 3);
			Check(name + ": it struck " + expect, SceneCollector.CannonStrikes - strikes == 1 && SceneCollector.LastCannonStrike.StartsWith(expect));
			Check(name + ": she heeled from the recoil", heel > 0.025f);
		}

		private bool SeaSpot(Vector2 from, Vector2 dir, float dist, int room, out Vector2 spot)
		{
			TerrainCache terrainForTest = _mod.TerrainForTest;
			for (int i = 0; i < 9; i++)
			{
				float f = ((i % 2 == 0) ? 1f : (-1f)) * (float)((i + 1) / 2) * 0.2f;
				Vector2 vector = new Vector2(dir.x * Mathf.Cos(f) - dir.y * Mathf.Sin(f), dir.x * Mathf.Sin(f) + dir.y * Mathf.Cos(f));
				spot = from + vector * dist;
				if (Open(terrainForTest, spot, room))
				{
					return true;
				}
			}
			spot = from + dir * dist;
			return false;
		}

		private Vector2 EyeOn(Vector2 ship)
		{
			if (SeaSpot(ship, (-_perp * 3f - _u).normalized, 9.5f, 1, out var spot))
			{
				return spot;
			}
			if (SeaSpot(ship, (_perp * 3f - _u).normalized, 9.5f, 1, out spot))
			{
				return spot;
			}
			return _eye;
		}

		private IEnumerator EnsureShip()
		{
			bool flag = false;
			try
			{
				flag = _ship != null && _ship.isAlive();
			}
			catch
			{
			}
			if (!flag)
			{
				Log.Info("CANNON (the ship was lost: a new one)");
				_ship = Spawn(_shipAt);
				yield return new WaitForSecondsRealtime(1.2f);
			}
		}

		private IEnumerator Shots()
		{
			_ship = Spawn(_shipAt);
			if (_ship == null)
			{
				yield break;
			}
			yield return new WaitForSecondsRealtime(1.5f);
			float[] frames = new float[7] { 0.03f, 0.1f, 0.25f, 0.5f, 0.9f, 1.4f, 2.4f };
			Vector2 current_position = _ship.current_position;
			Watch(EyeOn(current_position), (current_position + _land) * 0.5f, 1.6f, 2f);
			yield return new WaitForSecondsRealtime(0.6f);
			yield return Shot("land", _land, null, "Land", frames);
			Check("land: the game's firebomb explosion was left out", SceneCollector.CannonSpritesHidden > 0);
			if (_ship == null || !_ship.isAlive())
			{
				yield break;
			}
			current_position = _ship.current_position;
			if (!SeaSpot(current_position, -_u, 10f, 1, out var sea))
			{
				Log.Info("CANNON (no open sea off her far side)");
			}
			Watch(EyeOn(current_position), (current_position + sea) * 0.5f, 1.6f, 2f);
			yield return new WaitForSecondsRealtime(0.5f);
			yield return Shot("sea", sea, null, "Water", frames);
			current_position = _ship.current_position;
			if (SeaSpot(current_position, _perp, 9f, 2, out var spot) || SeaSpot(current_position, -_perp, 9f, 2, out spot))
			{
				_other = Spawn(spot);
			}
			yield return new WaitForSecondsRealtime(1f);
			int k;
			if (_other != null && _other.isAlive())
			{
				for (k = 0; k < 3; k++)
				{
					if (k > 0 && (_ship == null || !_ship.isAlive() || _other == null || !_other.isAlive()))
					{
						Check("hull: it struck Hull", ok: false);
						break;
					}
					Hold(_other);
					current_position = _ship.current_position;
					Watch(EyeOn(current_position), (current_position + _other.current_position) * 0.5f, 1.6f, 2f);
					yield return new WaitForSecondsRealtime(0.4f);
					yield return Shot("hull", _other.current_position, _other, "Hull", frames, k < 2);
					if (!_again)
					{
						break;
					}
				}
				try
				{
					if (_other != null && _other.isAlive())
					{
						_other.dieAndDestroy(AttackType.Other);
					}
				}
				catch
				{
				}
				_other = null;
			}
			else
			{
				Log.Info("CANNON no second ship to fire at");
			}
			yield return EnsureShip();
			string[] f;
			if (_ship != null && _ship.isAlive())
			{
				Vector2 eye = _ship.current_position;
				float height = 1.9f;
				MeshInstance meshInstance = Creatures.BoatPlaced(_ship);
				string text = ((meshInstance != null && meshInstance.Model != null) ? meshInstance.Model.Info("helm") : null);
				if (text != null)
				{
					f = text.Split(',');
					Vec3 vec = meshInstance.Position + meshInstance.Basis.Mul(new Vec3(F(0), F(1), F(2) + F(3)));
					eye = new Vector2(vec.X, vec.Y);
					height = vec.Z - 0f;
				}
				Watch(eye, _land, height, 1f);
				yield return new WaitForSecondsRealtime(0.6f);
				yield return Shot("deck", _land, null, "Land", frames);
			}
			yield return EnsureShip();
			if (_ship == null || !_ship.isAlive())
			{
				yield break;
			}
			current_position = _ship.current_position;
			Watch(EyeOn(current_position), current_position, 1.6f, 2f);
			SeaSpot(current_position, -_u, 10f, 1, out sea);
			yield return new WaitForSecondsRealtime(0.5f);
			k = SceneCollector.CannonMuzzles;
			float worst = 0f;
			float start = Time.unscaledTime;
			int sent = 0;
			int shots = 0;
			HashSet<(Projectile, Vector2)> balls = new HashSet<(Projectile, Vector2)>();
			while (Time.unscaledTime - start < 4f)
			{
				float num = Time.unscaledTime - start;
				Hold(_ship);
				foreach (Projectile item in World.world.projectiles.list)
				{
					if (item != null && item.asset != null && item.asset.id == "cannonball" && item.by_who == _ship)
					{
						balls.Add((item, item.getStartVector()));
					}
				}
				if (sent < 5 && num > (float)sent * 0.4f)
				{
					Attack(sea + _perp * ((float)(sent - 2) * 2f), null);
					shots++;
					sent++;
				}
				if (num > 1.75f && num - Time.unscaledDeltaTime <= 1.75f)
				{
					_mod.SaveFrame("cannon_broadside_0.png");
				}
				if (num > 3f && num - Time.unscaledDeltaTime <= 3f)
				{
					_mod.SaveFrame("cannon_broadside_1.png");
				}
				worst = Mathf.Max(worst, Time.unscaledDeltaTime);
				yield return null;
			}
			Log.Info($"CANNON broadside: {shots} attacks, {balls.Count} balls sent, guns fired {SceneCollector.CannonMuzzles - k}, particles {SceneCollector.BlastParticleCount}, worst frame {worst * 1000f:F0} ms");
			Check("broadside: every ball sent fired a gun", SceneCollector.CannonMuzzles - k == balls.Count && balls.Count >= 2);
			float F(int i)
			{
				return float.Parse(f[i].Trim(), CultureInfo.InvariantCulture);
			}
		}
	}
}
