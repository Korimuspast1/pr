using System;
using System.Collections;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class ShotWallsTest : CivilTest
	{
		private static readonly string[] Foes = new string[3] { "bandit", "orc", "skeleton" };

		private const int Volley = 12;

		private Actor _you;

		private Actor _foe;

		private string _foeKind;

		protected override string Prefix => "WALLS";

		protected override IEnumerator Body()
		{
			WorldTile stage = CivilTest.Stage();
			if (!Check("open land", stage != null))
			{
				yield break;
			}
			CivilTest.ClearBox(stage.x - 14, stage.y - 10, stage.x + 14, stage.y + 10);
			_you = CivilTest.Spawn("human", new Vector2((float)stage.x + 0.5f, (float)stage.y - 4.5f));
			yield return Possess(_you);
			if (Check("first person in a villager", InControl(_you)))
			{
				Mod.SetYaw(MathF.PI / 2f);
				Mod.SetPitchDegrees(5f);
				yield return Part("a clear lane", Lane(new Vector2((float)stage.x - 8.5f, (float)stage.y - 4.5f), new Vector2((float)stage.x - 8.5f, (float)stage.y + 4.5f), hurts: true, "clear lane"));
				Building building = null;
				try
				{
					building = World.world.buildings.addBuilding("house_human_1", stage, pCheckForBuild: false, pSfx: false, BuildPlacingType.New);
					building?.completeConstruction();
				}
				catch (Exception ex)
				{
					Log.Error("WALLS house: " + ex.Message);
				}
				if (Check("a house between", building != null))
				{
					Vector2 you = new Vector2((float)stage.x + 0.5f, (float)stage.y - 4.5f);
					Vector2 foe = new Vector2((float)stage.x + 0.5f, (float)stage.y + 5.5f);
					ShotWalls.On = false;
					yield return Part("through the house as before the fix", Lane(you, foe, hurts: true, "house, fix off"));
					ShotWalls.On = true;
					ShotWalls.Trace = true;
					yield return Part("through the house", Lane(you, foe, hurts: false, "house"));
					CivilTest.Kill(_foe);
				}
			}
		}

		private bool Foe(Vector2 at)
		{
			if (_foe != null && _foe.isAlive())
			{
				return true;
			}
			string[] foes = Foes;
			foreach (string text in foes)
			{
				Actor actor = CivilTest.Spawn(text, at);
				if (actor != null)
				{
					bool flag = false;
					try
					{
						flag = actor.areFoes(_you);
					}
					catch
					{
					}
					if (flag)
					{
						_foe = actor;
						_foeKind = text;
						Note("enemy: a " + text);
						return true;
					}
					CivilTest.Kill(actor);
				}
			}
			return false;
		}

		private IEnumerator Lane(Vector2 at, Vector2 from, bool hurts, string what)
		{
			Put(_you, at);
			if (!Check("an enemy", Foe(from)))
			{
				yield break;
			}
			yield return CivilTest.Wait(1f);
			try
			{
				_you.setMaxHealth();
			}
			catch
			{
			}
			int hurt = 0;
			int stoppedBefore = ShotWalls.Stopped;
			int refusedBefore = ShotWalls.Refused;
			for (int i = 0; i < 12; i++)
			{
				if (!_you.isAlive())
				{
					break;
				}
				Put(_foe, from);
				try
				{
					_foe.setMaxHealth();
					_foe.clearAttackTarget();
				}
				catch
				{
				}
				Projectile p = null;
				try
				{
					p = World.world.projectiles.spawn(_foe, _you, "arrow", new Vector3(from.x, from.y, 0f), new Vector3(at.x, at.y, 0f), 0.4f, 0.6f);
				}
				catch (Exception ex)
				{
					Log.Error("WALLS arrow: " + ex.Message);
				}
				bool hit = false;
				float top = 0f;
				float mid = -1f;
				int stopped = ShotWalls.Stopped;
				int refused = ShotWalls.Refused;
				for (float t = 0f; t < 2f; t += Time.unscaledDeltaTime)
				{
					if (p == null)
					{
						break;
					}
					if (p._state != ProjectileState.Active)
					{
						break;
					}
					Put(_you, at);
					Put(_foe, from);
					try
					{
						Vector3 current_position_3d = p._current_position_3d;
						top = Mathf.Max(top, current_position_3d.z);
						if (Mathf.Abs(current_position_3d.y - (at.y + from.y) * 0.5f) < 1.5f)
						{
							mid = Mathf.Max(mid, current_position_3d.z);
						}
					}
					catch
					{
					}
					try
					{
						_you.setMaxHealth();
						_you.attackedBy = null;
					}
					catch
					{
					}
					float before = Health(_you);
					yield return null;
					bool flag = false;
					try
					{
						flag = _you.attackedBy == _foe;
					}
					catch
					{
					}
					if (Health(_you) < before && flag)
					{
						hit = true;
					}
					else if (Health(_you) < before)
					{
						Note("  (hurt by " + ((_you.attackedBy != null) ? _you.attackedBy.ToString() : "nothing") + ", not the arrow)");
					}
				}
				try
				{
					_you.setMaxHealth();
				}
				catch
				{
				}
				if (hit)
				{
					hurt++;
				}
				Note(string.Format("  arrow {0}: {1}, highest {2:F2}, over the middle {3:F2}, stopped {4}, refused {5}, state {6}, at {7}", i, hit ? "hurt" : "no hurt", top, mid, ShotWalls.Stopped - stopped, ShotWalls.Refused - refused, p?._state, p?._current_position_3d));
			}
			Note($"{what}: {hurt} of {12} arrows hurt you, {ShotWalls.Stopped - stoppedBefore} stopped at a wall, {ShotWalls.Refused - refusedBefore} hits refused");
			if (hurts)
			{
				Check(what + ": the arrows hurt you", hurt > 6, $"{hurt} of {12}");
				yield break;
			}
			Check(what + ": no arrow hurts you", hurt == 0, $"{hurt} of {12}");
			Check(what + ": the arrows stop at the house", ShotWalls.Stopped - stoppedBefore >= 6, $"{ShotWalls.Stopped - stoppedBefore} stopped");
		}

		private static void Put(Actor a, Vector2 at)
		{
			if (a != null)
			{
				a.current_position = at;
				a.dirty_current_tile = true;
			}
		}

		private static float Health(Actor a)
		{
			try
			{
				return (a != null && a.isAlive()) ? ((float)a.getHealth()) : 0f;
			}
			catch
			{
				return 0f;
			}
		}
	}
}
