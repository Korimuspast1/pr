using System;
using System.Collections.Generic;
using FirstPerson.Core;
using FirstPerson.OneWorld;
using UnityEngine;
using ai.behaviours;

namespace FirstPerson
{
	internal static class Lounging
	{
		private sealed class Lounger
		{
			public float Still;

			public float Amount;

			public float Heading;

			public float Next;

			public float Sat;

			public float Length;

			public float RestSeat;

			public RestStyle Style;

			public bool Down;

			public bool Hang;

			public Vector2 Spot;

			public Vector2 Origin;

			public int Seen;

			public uint Rng;
		}

		private static readonly Dictionary<Actor, Lounger> All = new Dictionary<Actor, Lounger>();

		private static int _swept;

		internal const float Range = 60f;

		internal static int SatDownForTest;

		internal static bool OffForTest;

		internal static bool LogForTest;

		internal static int SeatedForTest => CountSeated();

		public static RestPose Pose(Actor a, Vector2 at, float heading, float speed, bool free, bool child, float body, float distance, float dt)
		{
			if (OffForTest || distance > 60f)
			{
				Forget(a);
				return default(RestPose);
			}
			int frameCount = Time.frameCount;
			if (frameCount - _swept > 600)
			{
				Sweep(frameCount);
			}
			bool flag;
			try
			{
				flag = a.is_moving || a.position_height > 0.05f;
			}
			catch
			{
				flag = true;
			}
			bool flag2 = free && speed < 0.05f && !flag;
			All.TryGetValue(a, out var value);
			if (value == null)
			{
				if (!flag2)
				{
					return default(RestPose);
				}
				value = new Lounger
				{
					Rng = (uint)((a.GetHashCode() * -1640531535) | 1),
					Heading = heading
				};
				value.Next = 1.5f + 3.5f * Next(value);
				All[a] = value;
			}
			value.Seen = frameCount;
			value.Still = (flag2 ? (value.Still + dt) : 0f);
			if (!value.Down)
			{
				if (flag2 && value.Still >= value.Next)
				{
					Vector2 fire;
					bool flag3 = Seats.Evening && Seats.FireNear(at, 6f, out fire);
					if (Next(value) < (flag3 ? 0.9f : 0.6f))
					{
						Choose(a, value, at, heading, child, body);
						if (RealActs.Sit(a, value.Spot, value.Length))
						{
							value.Origin = value.Spot;
						}
					}
					else
					{
						value.Next = value.Still + 5f + 7f * Next(value);
					}
				}
				if (!value.Down)
				{
					value.Amount = Mathf.MoveTowards(value.Amount, 0f, dt / 0.6f);
					value.Heading = heading;
					if (value.Amount <= 0f && !flag2)
					{
						All.Remove(a);
						return default(RestPose);
					}
					if (!(value.Amount > 0f))
					{
						return default(RestPose);
					}
					return RestPose.Of(value.Style, value.Amount, value.Heading, value.RestSeat, value.Hang);
				}
			}
			value.Sat += dt;
			Vector2 vector;
			try
			{
				vector = a.current_position;
			}
			catch
			{
				vector = at;
			}
			bool flag4 = RealActs.Sitting(a);
			if (!free || (flag && !flag4) || (!flag4 && (vector - value.Origin).sqrMagnitude > 0.36f) || value.Sat > value.Length)
			{
				value.Amount = Mathf.MoveTowards(value.Amount, 0f, dt / 0.6f);
				if (value.Amount <= 0f)
				{
					value.Down = false;
					value.Still = 0f;
					value.Next = 8f + 14f * Next(value);
					if (flag || !free)
					{
						All.Remove(a);
						return default(RestPose);
					}
				}
				return RestPose.Of(value.Style, value.Amount, value.Heading, value.RestSeat, value.Hang);
			}
			float num = Mathf.Abs(Mathf.DeltaAngle(heading * 57.29578f, value.Heading * 57.29578f));
			if ((at - value.Spot).sqrMagnitude < 0.0144f || value.Amount > 0f)
			{
				value.Amount = Mathf.MoveTowards(value.Amount, 1f, dt / 0.9f);
			}
			float heading2 = ((value.Amount > 0f || num < 20f) ? value.Heading : heading);
			return RestPose.Of(value.Style, value.Amount, heading2, value.RestSeat, value.Hang);
		}

		internal static bool Sits(Actor a)
		{
			if (All.Count > 0 && All.TryGetValue(a, out var value) && value.Down)
			{
				return value.Amount > 0.5f;
			}
			return false;
		}

		internal static Vector2 Spot(Actor a, Vector2 p)
		{
			if (All.Count == 0 || !All.TryGetValue(a, out var value) || Time.frameCount - value.Seen > 60)
			{
				return p;
			}
			if (!value.Down && !(value.Amount > 0f))
			{
				return p;
			}
			return value.Spot;
		}

		private static void Choose(Actor a, Lounger l, Vector2 at, float heading, bool child, float body)
		{
			l.Down = true;
			l.Sat = 0f;
			l.Length = 14f + 30f * Next(l);
			l.Spot = at;
			try
			{
				l.Origin = a.current_position;
			}
			catch
			{
				l.Origin = at;
			}
			l.Heading = heading;
			l.RestSeat = 0f;
			l.Hang = false;
			float num = Next(l);
			l.Style = ((num < 0.45f) ? RestStyle.Ground : ((num < 0.75f) ? RestStyle.CrossLegged : RestStyle.SitBack));
			SatDownForTest++;
			string text = null;
			try
			{
				text = ((AiSystem<Actor, ActorJob, BehaviourTaskActor, BehaviourActionActor, BehaviourActorCondition>)a.ai)?.task?.id;
			}
			catch
			{
			}
			if (text != null && text.StartsWith("wait", StringComparison.Ordinal))
			{
				try
				{
					a.makeWait(l.Length);
				}
				catch
				{
				}
			}
			if (LogForTest)
			{
				Log.Info(string.Format("lounge: {0} sits down ({1}, still {2:F1} s)", WorldBoxMod.Describe(a), text ?? "no task", l.Still));
			}
			if (Seats.Evening && Seats.FireNear(at, 6f, out var fire))
			{
				Vector2 vector = at - fire;
				if (vector.sqrMagnitude < 0.0001f)
				{
					vector = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
				}
				float num2 = 1.35f + 0.35f * Next(l);
				Vector2 vector2 = fire + vector.normalized * num2;
				if ((vector2 - at).magnitude < 4f)
				{
					l.Spot = vector2;
					l.Heading = Mathf.Atan2(0f - vector.y, 0f - vector.x);
					l.Style = ((num < 0.5f) ? RestStyle.Ground : RestStyle.CrossLegged);
					l.Length += 20f;
					Log.Info($"lounge: {WorldBoxMod.Describe(a)} sits by the fire, {(vector2 - at).magnitude:F1} tiles over");
					return;
				}
			}
			if (Attention.FaceTo(a, out var face))
			{
				l.Heading = Mathf.Atan2(face.y - at.y, face.x - at.x);
			}
			WorldBoxMod instance = WorldBoxMod.Instance;
			TerrainCache terrainCache = ((instance != null) ? instance.Land : null);
			if (terrainCache == null || child)
			{
				return;
			}
			float ground = terrainCache.SampleHeight(at.x, at.y);
			for (int i = 0; i < 4; i++)
			{
				float yaw = l.Heading + MathF.PI + ((float)i - 1.5f) * 0.9f;
				if (Seats.Ledge(at, ground, body, yaw, out var seat) && !((seat.At - at).magnitude > 1.1f))
				{
					l.Spot = seat.At;
					l.Heading = seat.Heading;
					l.Style = RestStyle.Seat;
					l.RestSeat = seat.Height;
					break;
				}
			}
		}

		private static void Forget(Actor a)
		{
			if (All.Count > 0)
			{
				All.Remove(a);
			}
		}

		private static void Sweep(int frame)
		{
			_swept = frame;
			List<Actor> list = null;
			foreach (KeyValuePair<Actor, Lounger> item in All)
			{
				if (frame - item.Value.Seen > 120)
				{
					(list ?? (list = new List<Actor>())).Add(item.Key);
				}
			}
			if (list == null)
			{
				return;
			}
			foreach (Actor item2 in list)
			{
				All.Remove(item2);
			}
		}

		private static int CountSeated()
		{
			int num = 0;
			int frameCount = Time.frameCount;
			foreach (Lounger value in All.Values)
			{
				if (value.Down && value.Amount > 0.5f && frameCount - value.Seen < 5)
				{
					num++;
				}
			}
			return num;
		}

		internal static void Clear()
		{
			All.Clear();
			SatDownForTest = 0;
		}

		private static float Next(Lounger l)
		{
			l.Rng ^= l.Rng << 13;
			l.Rng ^= l.Rng >> 17;
			l.Rng ^= l.Rng << 5;
			return (float)(l.Rng & 0xFFFFFF) / 16777216f;
		}
	}
}
