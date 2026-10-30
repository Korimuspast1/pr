using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class CrabFire
	{
		internal sealed class Shot
		{
			public readonly bool[] On = new bool[2];

			public readonly bool[] Strikes = new bool[2];

			public readonly Vec3[] At = new Vec3[2];

			public float Seen = -99f;

			public float Clock;

			public bool Any
			{
				get
				{
					if (!On[0])
					{
						return On[1];
					}
					return true;
				}
			}
		}

		private const float Fresh = 0.35f;

		private static readonly Dictionary<long, Shot> _shots = new Dictionary<long, Shot>();

		private static readonly Shot _local = new Shot();

		private static readonly List<long> _old = new List<long>();

		private static float Now => Time.realtimeSinceStartup;

		public static Shot Heard(long crabId)
		{
			if (!_shots.TryGetValue(crabId, out var value))
			{
				value = (_shots[crabId] = new Shot());
			}
			value.Seen = Now;
			if (_shots.Count > 16)
			{
				Prune();
			}
			return value;
		}

		private static void Prune()
		{
			_old.Clear();
			foreach (KeyValuePair<long, Shot> shot in _shots)
			{
				if (Now - shot.Value.Seen > 5f)
				{
					_old.Add(shot.Key);
				}
			}
			foreach (long item in _old)
			{
				_shots.Remove(item);
			}
		}

		public static bool Of(Actor crab, out Shot shot)
		{
			shot = null;
			if (crab?.data == null)
			{
				return false;
			}
			if (_shots.TryGetValue(crab.data.id, out var value) && Now - value.Seen < 0.35f)
			{
				shot = value;
				return true;
			}
			if (GameLaser(crab, _local))
			{
				shot = _local;
				return true;
			}
			return false;
		}

		public static bool Mine(out Actor crab, Shot into)
		{
			crab = null;
			try
			{
				Actor you = Crabzillas.You;
				if (you != null && you.isAlive())
				{
					crab = you;
					for (int i = 0; i < 2; i++)
					{
						into.On[i] = Crabzillas.Firing[i];
						into.At[i] = Crabzillas.TargetOf(i);
						into.Strikes[i] = Crabzillas.StrikesAt(i);
					}
					return true;
				}
				Actor unit_main = ControllableUnit._unit_main;
				if (unit_main == null || !Crabzillas.Is(unit_main) || !unit_main.isAlive())
				{
					return false;
				}
				crab = unit_main;
				if (!GameLaser(unit_main, into))
				{
					into.On[0] = (into.On[1] = false);
				}
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static bool GameLaser(Actor crab, Shot into)
		{
			try
			{
				if (Crabzillas.You == crab || !ControllableUnit.isControllingUnit(crab))
				{
					return false;
				}
				Crabzilla actorComponent = crab.getActorComponent<Crabzilla>();
				if (actorComponent == null || !actorComponent._beam_enabled)
				{
					return false;
				}
				CrabArm[] array = new CrabArm[2] { actorComponent.arm1, actorComponent.arm2 };
				for (int i = 0; i < 2; i++)
				{
					Vector3 vector = ((array[i] != null && array[i].laserPoint != null) ? array[i].laserPoint.position : actorComponent.armTarget.transform.position);
					into.On[i] = true;
					into.Strikes[i] = true;
					into.At[i] = new Vec3(vector.x, vector.y, float.NaN);
				}
				into.Seen = Now;
				return true;
			}
			catch
			{
				return false;
			}
		}

		public static Vector2 MapPoint(Shot s)
		{
			if (!s.On[0] && s.On[1])
			{
				return new Vector2(s.At[1].X, s.At[1].Y);
			}
			return new Vector2(s.At[0].X, s.At[0].Y);
		}

		public static void Clear()
		{
			_shots.Clear();
		}
	}
}
