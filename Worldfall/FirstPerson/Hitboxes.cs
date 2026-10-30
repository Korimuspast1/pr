using System;
using System.Collections.Generic;
using FirstPerson.Core;
using UnityEngine;

namespace FirstPerson
{
	internal static class Hitboxes
	{
		private sealed class Entry
		{
			public Hitbox Box;

			public Vector2 Anchor;

			public float Lift;

			public int Frame;

			public HeadBox Head;

			public bool HasHead;

			public bool Lazy;

			public HeadBox.Snap Snap;

			public HeadBox Eager;

			public bool EagerSet;
		}

		internal static bool LazyHeads = true;

		private static readonly Dictionary<Actor, Entry> Boxes = new Dictionary<Actor, Entry>();

		private static readonly List<Actor> Stale = new List<Actor>();

		internal static Func<Actor, SpriteImage> SpriteOf;

		internal static Func<float, float, float> GroundAt;

		internal static List<Actor> Room;

		private const int KeepFrames = 240;

		private static int _cleaned;

		private static bool _verify;

		private static int _compared;

		private static int _differ;

		private static string _first;

		internal static int HeadsCompared => _compared;

		public static void Record(Actor a, MeshInstance body, bool person)
		{
			MeshModel meshModel = body?.Model;
			if (a == null || meshModel == null)
			{
				return;
			}
			Vec3 c = body.Basis.C0;
			Vec3 c2 = body.Basis.C1;
			Vec3 c3 = body.Basis.C2;
			float num = Mathf.Sqrt(c.X * c.X + c.Y * c.Y);
			float num2 = Mathf.Sqrt(c2.X * c2.X + c2.Y * c2.Y);
			if (!(num < 1E-05f) && !(num2 < 1E-05f))
			{
				Vec3 v = (meshModel.Min + meshModel.Max) * 0.5f;
				Vec3 vec = meshModel.Max - meshModel.Min;
				float num3 = (person ? 0.75f : 0.85f);
				float num4 = 0.85f;
				Hitbox box = new Hitbox
				{
					Centre = body.Basis.Mul(v) + body.Position,
					AX = c.X / num,
					AY = c.Y / num,
					HalfA = vec.X * 0.5f * num * num3,
					HalfB = vec.Y * 0.5f * num2 * num4,
					HalfUp = vec.Z * 0.5f * Mathf.Abs(c3.Z)
				};
				if (LazyHeads)
				{
					PutLazy(a, box, body);
					return;
				}
				HeadBox head;
				bool hasHead = HeadBox.Of(body, out head);
				Put(a, box, hasHead, head);
			}
		}

		private static void PutLazy(Actor a, Hitbox box, MeshInstance body)
		{
			if (_verify && Boxes.TryGetValue(a, out var value) && value.Lazy && value.HasHead && value.EagerSet && !value.Snap.Worked)
			{
				VerifyHead(value);
			}
			Entry entry = Put(a, box);
			if (entry == null)
			{
				return;
			}
			entry.Lazy = true;
			entry.HasHead = entry.Snap.Take(body);
			if (_verify)
			{
				bool flag = HeadBox.Of(body, out entry.Eager);
				entry.EagerSet = true;
				if (flag != entry.HasHead)
				{
					Differs($"{Name(a)}: has a head {flag} the old way, {entry.HasHead} lazily");
				}
			}
		}

		public static void RecordSprite(Actor a, float x, float y, float bottom, float width, float height, float heading)
		{
			if (a != null && height > 0f)
			{
				Hitbox box = new Hitbox
				{
					Centre = new Vec3(x, y, bottom + height * 0.5f),
					AX = Mathf.Cos(heading),
					AY = Mathf.Sin(heading),
					HalfA = Mathf.Max(0.08f, width * 0.32f),
					HalfB = Mathf.Max(0.06f, width * 0.2f),
					HalfUp = height * 0.5f
				};
				Put(a, box);
			}
		}

		private static Entry Put(Actor a, Hitbox box, bool hasHead = false, HeadBox head = default(HeadBox))
		{
			float num = 0f;
			Vector2 current_position;
			try
			{
				current_position = a.current_position;
				num = a.position_height;
			}
			catch
			{
				return null;
			}
			if (!Boxes.TryGetValue(a, out var value))
			{
				value = (Boxes[a] = new Entry());
			}
			int frameCount = Time.frameCount;
			value.Box = box;
			value.Anchor = current_position;
			value.Lift = num;
			value.Frame = frameCount;
			value.HasHead = hasHead;
			value.Head = head;
			value.Lazy = false;
			value.EagerSet = false;
			if (frameCount - _cleaned > 600)
			{
				Clean();
			}
			return value;
		}

		public static bool Seen(Actor a)
		{
			if (a != null && Boxes.TryGetValue(a, out var value))
			{
				return Time.frameCount - value.Frame <= 1;
			}
			return false;
		}

		public static bool Get(Actor a, out Hitbox box)
		{
			box = default(Hitbox);
			if (a == null)
			{
				return false;
			}
			Vector2 current_position;
			float position_height;
			try
			{
				if (!a.isAlive())
				{
					return false;
				}
				current_position = a.current_position;
				position_height = a.position_height;
			}
			catch
			{
				return false;
			}
			if (Room != null && Room.Contains(a))
			{
				if (!Boxes.TryGetValue(a, out var value) || Time.frameCount - value.Frame > 2)
				{
					return false;
				}
				box = value.Box;
				return true;
			}
			if (Boxes.TryGetValue(a, out var value2) && Time.frameCount - value2.Frame <= 240)
			{
				box = value2.Box;
				float num = 0f;
				if (Time.frameCount - value2.Frame > 1 && GroundAt != null)
				{
					num = GroundAt(current_position.x, current_position.y) - GroundAt(value2.Anchor.x, value2.Anchor.y) + (position_height - value2.Lift);
				}
				box.Centre = new Vec3(box.Centre.X + current_position.x - value2.Anchor.x, box.Centre.Y + current_position.y - value2.Anchor.y, box.Centre.Z + num);
				return true;
			}
			return Estimate(a, current_position, position_height, out box);
		}

		public static bool GetHead(Actor a, out HeadBox head)
		{
			head = default(HeadBox);
			if (a == null || !Boxes.TryGetValue(a, out var value) || !value.HasHead || Time.frameCount - value.Frame > 240)
			{
				return false;
			}
			if (Room != null && Room.Contains(a))
			{
				if (Time.frameCount - value.Frame > 2)
				{
					return false;
				}
				try
				{
					if (!a.isAlive())
					{
						return false;
					}
				}
				catch
				{
					return false;
				}
				head = (value.Lazy ? LazyHead(value) : value.Head);
				return true;
			}
			Vector2 current_position;
			float position_height;
			try
			{
				if (!a.isAlive())
				{
					return false;
				}
				current_position = a.current_position;
				position_height = a.position_height;
			}
			catch
			{
				return false;
			}
			float dz = 0f;
			if (Time.frameCount - value.Frame > 1 && GroundAt != null)
			{
				dz = GroundAt(current_position.x, current_position.y) - GroundAt(value.Anchor.x, value.Anchor.y) + (position_height - value.Lift);
			}
			head = (value.Lazy ? LazyHead(value) : value.Head).Moved(current_position.x - value.Anchor.x, current_position.y - value.Anchor.y, dz);
			return true;
		}

		private static HeadBox LazyHead(Entry e)
		{
			bool flag = !e.Snap.Worked;
			e.Snap.Get(out var head);
			if (_verify && flag && e.EagerSet)
			{
				Compare(e.Eager, head, "asked for");
			}
			return head;
		}

		internal static void VerifyHeads(bool on)
		{
			_verify = on;
			if (on)
			{
				_compared = (_differ = 0);
				_first = null;
			}
		}

		internal static string HeadVerdict()
		{
			if (_differ > 0)
			{
				return $"{_differ} of {_compared} heads differ; first: {_first}";
			}
			if (_compared != 0)
			{
				return null;
			}
			return "no head was compared";
		}

		private static void VerifyHead(Entry e)
		{
			e.Snap.Get(out var head);
			Compare(e.Eager, head, "a frame on");
		}

		private static void Compare(HeadBox eager, HeadBox lazy, string when)
		{
			_compared++;
			string text = NpcTune.Differs(eager.Centre, lazy.Centre, "Centre.") ?? NpcTune.Differs(eager.X, lazy.X, "X.") ?? NpcTune.Differs(eager.Y, lazy.Y, "Y.") ?? NpcTune.Differs(eager.Z, lazy.Z, "Z.");
			if (text != null)
			{
				Differs("worked out " + when + ": " + text + " (old way first)");
			}
		}

		private static void Differs(string what)
		{
			_differ++;
			if (_first == null)
			{
				_first = what;
			}
		}

		private static string Name(Actor a)
		{
			try
			{
				return "unit " + a.data.id + " " + a.asset.id;
			}
			catch
			{
				return "a unit";
			}
		}

		private static bool Estimate(Actor a, Vector2 at, float lift, out Hitbox box)
		{
			box = default(Hitbox);
			if (SpriteOf == null || GroundAt == null)
			{
				return false;
			}
			SpriteImage spriteImage;
			Vector3 current_scale;
			try
			{
				spriteImage = SpriteOf(a);
				current_scale = a.current_scale;
			}
			catch
			{
				return false;
			}
			if (spriteImage == null)
			{
				return false;
			}
			float num = Stature.Peek(a, spriteImage.UnitHeight * spriteImage.VisibleFraction * Mathf.Abs(current_scale.y));
			float num2 = spriteImage.UnitWidth * Mathf.Abs(current_scale.x);
			float f = (a.flip ? 0f : MathF.PI);
			box = new Hitbox
			{
				Centre = new Vec3(at.x, at.y, GroundAt(at.x, at.y) + lift + num * 0.5f),
				AX = Mathf.Cos(f),
				AY = Mathf.Sin(f),
				HalfA = Mathf.Max(0.08f, num2 * 0.32f),
				HalfB = Mathf.Max(0.06f, num2 * 0.2f),
				HalfUp = Mathf.Max(0.1f, num * 0.5f)
			};
			return true;
		}

		public static void Near(Vec3 a, Vec3 b, float margin, Actor skip, List<Actor> into)
		{
			into.Clear();
			if (Room != null)
			{
				float num = b.X - a.X;
				float num2 = b.Y - a.Y;
				float num3 = num * num + num2 * num2;
				{
					foreach (Actor item in Room)
					{
						if (item != null && item != skip && Get(item, out var box))
						{
							float num4 = box.Centre.X - a.X;
							float num5 = box.Centre.Y - a.Y;
							float num6 = ((num3 > 1E-08f) ? Mathf.Clamp01((num4 * num + num5 * num2) / num3) : 0f);
							float num7 = num4 - num * num6;
							float num8 = num5 - num2 * num6;
							float num9 = margin + Mathf.Max(box.HalfA, box.HalfB);
							if (num7 * num7 + num8 * num8 <= num9 * num9)
							{
								into.Add(item);
							}
						}
					}
					return;
				}
			}
			MapBox world = World.world;
			if (world == null || world.units == null)
			{
				return;
			}
			List<Actor> simpleList = world.units.getSimpleList();
			float num10 = b.X - a.X;
			float num11 = b.Y - a.Y;
			float num12 = num10 * num10 + num11 * num11;
			float num13 = margin * margin;
			for (int i = 0; i < simpleList.Count; i++)
			{
				Actor actor = simpleList[i];
				if (actor == null || actor == skip)
				{
					continue;
				}
				Vector2 at = actor.current_position;
				bool flag = false;
				try
				{
					if (actor.is_inside_building)
					{
						if (!Doorstep.Drawn(actor, out at))
						{
							continue;
						}
						flag = true;
					}
				}
				catch
				{
					continue;
				}
				float num14 = at.x - a.X;
				float num15 = at.y - a.Y;
				float num16 = ((num12 > 1E-08f) ? Mathf.Clamp01((num14 * num10 + num15 * num11) / num12) : 0f);
				float num17 = num14 - num10 * num16;
				float num18 = num15 - num11 * num16;
				if (num17 * num17 + num18 * num18 > num13)
				{
					continue;
				}
				try
				{
					if (!actor.isAlive() || (actor.is_inside_building && !flag) || actor.is_inside_boat)
					{
						continue;
					}
					goto IL_0245;
				}
				catch
				{
				}
				continue;
				IL_0245:
				into.Add(actor);
			}
		}

		public static void Clear()
		{
			Boxes.Clear();
		}

		public static void Forget(Actor a)
		{
			if (a != null)
			{
				Boxes.Remove(a);
			}
		}

		private static void Clean()
		{
			Stale.Clear();
			int num = (_cleaned = Time.frameCount);
			foreach (KeyValuePair<Actor, Entry> box in Boxes)
			{
				if (num - box.Value.Frame > 240)
				{
					Stale.Add(box.Key);
				}
			}
			foreach (Actor item in Stale)
			{
				Boxes.Remove(item);
			}
		}
	}
}
