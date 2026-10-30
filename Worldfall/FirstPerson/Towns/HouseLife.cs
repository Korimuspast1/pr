using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson.Towns
{
	internal static class HouseLife
	{
		private sealed class Door
		{
			public float Open;

			public int Frame;

			public bool Posed;
		}

		private const float OpenFor = 2f;

		private const float Swing = 1.75f;

		private const float YouNear = 2f;

		private static readonly Dictionary<Building, Door> _doors = new Dictionary<Building, Door>();

		private static readonly Dictionary<MeshModel, int> _leaf = new Dictionary<MeshModel, int>();

		internal static Func<Building, Actor, bool> OpensFor;

		internal static Func<Building, bool> HeldOpen;

		private static object _world;

		private static int _forgotAt = -1000;

		private static readonly List<Building> _old = new List<Building>();

		internal static float OpenOf(Building b)
		{
			if (b == null || !_doors.TryGetValue(b, out var value))
			{
				return -1f;
			}
			return value.Open;
		}

		public static void Collect(TownScene s)
		{
			object obj = ((World.world != null) ? World.world.map_stats : null);
			if (obj != _world)
			{
				_world = obj;
				_doors.Clear();
			}
		}

		public static void Building(TownBuilding tb)
		{
			if (tb.Model == null)
			{
				return;
			}
			_doors.TryGetValue(tb.B, out var value);
			int num = ((tb.Ruin || !tb.Seen) ? (-1) : LeafOf(tb.Model));
			if (num < 0)
			{
				if (value != null && value.Posed && tb.Model.PartIndex("sails") < 0)
				{
					tb.Inst.PartRotation = null;
					tb.Inst.PartOffset = null;
					tb.Inst.PartScale = null;
				}
				if (value != null)
				{
					value.Posed = false;
					value.Open = 0f;
				}
				return;
			}
			if (value == null)
			{
				if (_doors.Count > 300 && Time.frameCount - _forgotAt >= 60)
				{
					Forget();
				}
				value = (_doors[tb.B] = new Door());
			}
			float num2 = Mathf.Clamp(tb.Scene.Dt, 0f, 0.1f);
			bool flag = Steering.SinceCrossing(tb.B) < 2f;
			if (!flag && tb.Scene.Host != null && Steering.DoorOf(tb.B, out var door2))
			{
				try
				{
					flag = (tb.Scene.Host.current_position - door2).sqrMagnitude < 4f && (OpensFor == null || OpensFor(tb.B, tb.Scene.Host));
				}
				catch
				{
				}
			}
			if (HeldOpen != null && HeldOpen(tb.B))
			{
				flag = true;
			}
			value.Open = Mathf.MoveTowards(value.Open, flag ? 1f : 0f, num2 * (flag ? 2.6f : 1.4f));
			value.Frame = Time.frameCount;
			MeshInstance inst = tb.Inst;
			if (value.Open <= 0f)
			{
				if (value.Posed && tb.Model.PartIndex("sails") < 0)
				{
					inst.PartRotation = null;
					inst.PartOffset = null;
					inst.PartScale = null;
				}
				value.Posed = false;
			}
			else
			{
				value.Posed = true;
				inst.EnsurePose();
				float num3 = value.Open * value.Open * (3f - 2f * value.Open);
				inst.PartRotation[num] = Mat3.RotZ(1.75f * num3);
				inst.Static = false;
			}
		}

		private static void Forget()
		{
			_forgotAt = Time.frameCount;
			_old.Clear();
			foreach (KeyValuePair<Building, Door> door in _doors)
			{
				if (door.Value.Frame < Time.frameCount - 600 && !door.Value.Posed)
				{
					_old.Add(door.Key);
				}
			}
			foreach (Building item in _old)
			{
				_doors.Remove(item);
			}
			_old.Clear();
		}

		private static int LeafOf(MeshModel m)
		{
			if (!_leaf.TryGetValue(m, out var value))
			{
				value = m.PartIndex("door_leaf");
				if (_leaf.Count > 600)
				{
					_leaf.Clear();
				}
				_leaf[m] = value;
			}
			return value;
		}
	}
}
