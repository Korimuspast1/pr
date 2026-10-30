using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstPerson
{
	internal sealed class Interactions
	{
		public const float Reach = 3f;

		private const float LineSeconds = 6.5f;

		private const float InsultPause = 1.2f;

		public Actor Candidate;

		public Actor Speaker;

		public string SpeakerName;

		public string Line;

		public float LineStart;

		public float LineUntil;

		public bool Romantic;

		public readonly List<Heart> Hearts = new List<Heart>();

		public Func<Actor, Actor, bool> Converse;

		public Action Closed;

		public bool Blocked;

		public bool Indoors;

		public int Insults;

		private Actor _you;

		private float _insultReady;

		private readonly Dictionary<long, int> _turns = new Dictionary<long, int>();

		private object _turnsWorld;

		private readonly System.Random _rng = new System.Random();

		public void Update(Actor host, BaseSimObject picked, Func<Actor, float> distanceTo, Func<Actor, Vector3> headOf, Dictionary<Actor, Vector2> lookAt, Vector2 yourPosition, Action<string> toast, float dt)
		{
			Candidate = null;
			_you = host;
			if (picked is Actor actor && actor != host && actor.isAlive() && (actor.asset == null || !actor.asset.is_boat) && distanceTo(actor) <= 3f)
			{
				Candidate = actor;
			}
			bool flag = PlayInput.GetKey(KeyCode.LeftShift) || PlayInput.GetKey(KeyCode.RightShift);
			if (Candidate != null && !Blocked && !flag && PlayInput.GetKeyDown(KeyCode.E))
			{
				Talk(host, Candidate);
			}
			if (Speaker != null)
			{
				if (!Speaker.isAlive() || Time.unscaledTime > LineUntil || distanceTo(Speaker) > 7.5f)
				{
					lookAt.Remove(Speaker);
					Speaker = null;
				}
				else
				{
					lookAt[Speaker] = yourPosition;
				}
			}
			for (int num = Hearts.Count - 1; num >= 0; num--)
			{
				Heart heart = Hearts[num];
				heart.Age += dt;
				heart.At += new Vector3(heart.DriftX * dt, heart.DriftY * dt, 0.45f * dt);
				if (heart.Age >= heart.Life)
				{
					Hearts.RemoveAt(num);
				}
			}
		}

		public void Reset(Dictionary<Actor, Vector2> lookAt)
		{
			if (Speaker != null)
			{
				lookAt.Remove(Speaker);
			}
			Speaker = null;
			Candidate = null;
			Hearts.Clear();
			Closed?.Invoke();
		}

		public void Talk(Actor host, Actor npc)
		{
			if (Converse == null || !Converse(host, npc))
			{
				object obj = World.world?.map_stats;
				if (obj != _turnsWorld || _turns.Count > 512)
				{
					_turnsWorld = obj;
					_turns.Clear();
				}
				long key = ((npc.data != null) ? npc.data.id : 0);
				_turns.TryGetValue(key, out var value);
				_turns[key] = value + 1;
				Say(npc, Dialogue.Line(npc, host, value), romantic: false);
				Pause(host, npc);
			}
		}

		public void Insult(Actor host, Actor npc)
		{
			float unscaledTime = Time.unscaledTime;
			if (unscaledTime < _insultReady || host == null || npc == null)
			{
				return;
			}
			_insultReady = unscaledTime + 1.2f;
			if (Dialogue.HasTrait(host, "mute"))
			{
				WorldBoxMod.Instance?.Toast("You are mute");
				return;
			}
			Insults++;
			bool flag = IsChild(npc);
			bool flag2 = Dialogue.HasTrait(npc, "hotheaded") || Dialogue.HasTrait(npc, "bloodlust") || Dialogue.HasTrait(npc, "psychopath");
			bool flag3 = !flag && flag2 && !Dialogue.Sovereign(npc, host);
			Chronicle.ChangeOpinion(npc, host, flag2 ? (-12) : (-8));
			Voice v = Dialogue.VoiceOf(npc, host);
			Say(npc, Dialogue.Speak(v, Dialogue.Retort(npc, host, v, flag3, flag)), romantic: false);
			if (flag3)
			{
				try
				{
					npc.addAggro(host);
				}
				catch
				{
				}
			}
			else
			{
				Pause(host, npc);
			}
			Log.Info("insult: " + WorldBoxMod.Describe(npc) + (flag3 ? " comes at you" : " answers back"));
		}

		public void HeartsOver(Actor a, Vector3 head)
		{
			SpawnHearts(head, 8);
		}

		public void Remark(Actor npc, string line)
		{
			if (npc != null && npc.isAlive() && !string.IsNullOrEmpty(line))
			{
				Say(npc, line, romantic: true);
			}
		}

		public void Aside(Actor npc, string line)
		{
			if (npc != null && npc.isAlive() && !string.IsNullOrEmpty(line))
			{
				Say(npc, line, romantic: false);
			}
		}

		private static bool IsChild(Actor a)
		{
			try
			{
				return !a.isAdult();
			}
			catch
			{
				return false;
			}
		}

		private void Say(Actor npc, string line, bool romantic)
		{
			Speaker = npc;
			SpeakerName = (Dialogue.IsPerson(npc) ? PeopleMet.Label(_you, npc) : WorldBoxMod.Describe(npc));
			Line = Renames.Apply(line);
			Romantic = romantic;
			LineStart = Time.unscaledTime;
			LineUntil = LineStart + 6.5f;
		}

		private static void Pause(Actor host, Actor npc)
		{
			try
			{
				npc.makeWait(6.5f);
				npc.lookTowardsPosition(host.current_position);
				host.lookTowardsPosition(npc.current_position);
			}
			catch
			{
			}
		}

		private void SpawnHearts(Vector3 at, int count)
		{
			for (int i = 0; i < count; i++)
			{
				Hearts.Add(new Heart
				{
					At = at + new Vector3((float)(_rng.NextDouble() - 0.5) * 0.4f, (float)(_rng.NextDouble() - 0.5) * 0.4f, (float)_rng.NextDouble() * 0.2f),
					Life = 1.4f + (float)_rng.NextDouble() * 0.8f,
					Age = (float)(-i) * 0.08f,
					DriftX = (float)(_rng.NextDouble() - 0.5) * 0.3f,
					DriftY = (float)(_rng.NextDouble() - 0.5) * 0.3f
				});
			}
		}
	}
}
